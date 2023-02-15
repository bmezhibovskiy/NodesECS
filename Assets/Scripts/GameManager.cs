using UnityEngine;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.Burst;
using System.Collections.Generic;
using Unity.Collections;


public struct SpatiallyHashed: IComponentData
{
    public int bucketIndex;
    public int entityIndex;
}

public struct SpatialHasher
{
    public readonly static int maxEntitiesInBucket = 64;
    public NativeSlice<Entity> entitiesInBuckets; //Flattened 2d array
    public NativeSlice<int> bucketCounts;

    public float inverseBucketSize;
    public int numSideBuckets;
    public float offset;
    public int numBuckets;

    public void Initialize(float bucketSize, float totalSideLength)
    {
        inverseBucketSize = 1 / bucketSize;
        numSideBuckets = (int)(totalSideLength * inverseBucketSize);
        offset = totalSideLength * 0.5f;

        numBuckets = numSideBuckets * numSideBuckets;
        NativeArray<Entity> entitiesArray = new NativeArray<Entity>(numBuckets * maxEntitiesInBucket, Allocator.Persistent);//Should this be Temp?
        NativeArray<int> bucketCountsArray = new NativeArray<int>(numBuckets, Allocator.Persistent);
        for(int i = 0; i < numBuckets; ++i)
        {
            bucketCountsArray[i] = 0;
        }
        entitiesInBuckets = new NativeSlice<Entity>(entitiesArray);
        bucketCounts = new NativeSlice<int>(bucketCountsArray);
    }

    public void Add(ref SpatiallyHashed hashed, Entity e, float3 pos)
    {
        int hash = Hash(pos);
        int numEntities = bucketCounts[hash];
        ++bucketCounts[hash];

        int newEntityIndex = numEntities;

        int flattenedIndex = Utils.to1D(hash, newEntityIndex, maxEntitiesInBucket);

        if(flattenedIndex < numBuckets * maxEntitiesInBucket)
        {
            entitiesInBuckets[flattenedIndex] = e;
            hashed.bucketIndex = hash;
            hashed.entityIndex = newEntityIndex;
        }
    }

    public void Update(ref SpatiallyHashed hashed, Entity e, float3 newPos)
    {
        int oldHash = hashed.bucketIndex;
        int newHash = Hash(newPos);
        if(oldHash != newHash)
        {
            Remove(hashed, e);
            Add(ref hashed, e, newPos);
        }
    }

    public void Remove(SpatiallyHashed hashed, Entity e)
    {
        int hash = hashed.bucketIndex;
        int flattenedIndex = Utils.to1D(hash, hashed.entityIndex, maxEntitiesInBucket);
        int lastEntityIndex = bucketCounts[hash] - 1;
        int flattenedLastIndex = Utils.to1D(hash, lastEntityIndex, maxEntitiesInBucket);
        //Copy over the last entity to this one's position, and decrease count
        entitiesInBuckets[flattenedIndex] = entitiesInBuckets[flattenedLastIndex];
        --bucketCounts[hash];
    }

    public NativeArray<Entity> ClosestNodes(float3 pos)
    {
        int hash = Hash(pos);
        int numEntities = bucketCounts[hash];
        NativeArray<Entity> closestEntities = new NativeArray<Entity>(numEntities, Allocator.Temp);
        for(int i = 0; i < numEntities; ++i)
        {
            closestEntities[i] = entitiesInBuckets[Utils.to1D(hash, i, maxEntitiesInBucket)];
        }
        return closestEntities;
    }

    private int Hash(float3 point)
    {
        int x = (int)((point.x + offset) * inverseBucketSize);
        int y = (int)((point.y + offset) * inverseBucketSize);

        int hash = Utils.to1D(x, y, numSideBuckets);
        hash = math.clamp(hash, 0, numBuckets);

        return hash;
    }
}


public static class Globals
{
    public readonly static SharedStatic<InputState> sharedInputState = SharedStatic<InputState>.GetOrCreate<InputStateKey>();
    public readonly static SharedStatic<SpatialHasher> sharedSpatialHasher = SharedStatic<SpatialHasher>.GetOrCreate<SpatialHasherKey>();
    private class InputStateKey { }
    private class SpatialHasherKey { }

    static Globals()
    {
        sharedInputState.Data.Initialize();
    }
}

public struct InputState
{
    public bool isSpaceDown;
    public void Initialize()
    {
        isSpaceDown = false;
    }
}

public class GameManager : MonoBehaviour
{
    [SerializeField]
    Camera mainCamera;

    [SerializeField]
    Mesh nodeMesh;

    [SerializeField]
    Material nodeMaterial;

    float nodeDistance = 1.2f;
    float3 nodeOffset = new float3(0, 1, 0) * 1.2f;
    int numSideNodes = 45;
    int numNodes = 45 * 45;
    bool is3d = false;

    // Start is called before the first frame update
    void Start()
    {
        Globals.sharedSpatialHasher.Data.Initialize(3.0f, nodeDistance * numSideNodes * 1.25f);
        mainCamera.orthographic = true;
        GenerateNodes();
        AddSectorObject(new float3(-2, 0, 0));
        AddSectorObject(new float3(2, 0, 0));
        AddShip(new float3(2, 2, 0));
        AddShip(new float3(-2, -2, 0));
    }

    // Update is called once per frame
    void Update()
    {
        if(Input.GetKey(KeyCode.Space))
        {
            Globals.sharedInputState.Data.isSpaceDown = true;
        }
        else
        {
            Globals.sharedInputState.Data.isSpaceDown = false;
        }

        UpdateFPSCounter();
    }

    private void GenerateNodes()
    {

        for (int i = 0; i < numNodes; i++)
        {
            int[] raw = is3d ? Utils.to3D(i, numSideNodes) : Utils.to2D(i, numSideNodes);
            float x = (float)(raw[0] - numSideNodes / 2) * nodeDistance;
            float y = (float)(raw[1] - numSideNodes / 2) * nodeDistance;
            if (is3d)
            {
                float z = (float)(raw[2] - numSideNodes / 2) * nodeDistance;
                AddNode(new float3(x, y, z) + nodeOffset, IsBorder(raw));
            }
            else
            {
                AddNode(new float3(x, y, 0) + nodeOffset, IsBorder(raw));
            }
        }
    }

    private void AddNode(float3 pos, bool isBorder)
    {
        EntityManager em = World.DefaultGameObjectInjectionWorld.EntityManager;
        EntityArchetype ea = em.CreateArchetype(typeof(Translation), typeof(GridNode));
        Entity e = em.CreateEntity(ea);
        em.AddComponentData(e, new Translation { Value = pos });
        em.AddComponentData(e, new GridNode { velocity = float3.zero, isDead = false });
        SpatiallyHashed hashed = new SpatiallyHashed { };
        Globals.sharedSpatialHasher.Data.Add(ref hashed, e, pos);
        em.AddComponentData(e, hashed);
    }

    private void AddSectorObject(float3 pos)
    {
        EntityManager em = World.DefaultGameObjectInjectionWorld.EntityManager;
        EntityArchetype ea = em.CreateArchetype(typeof(Translation), typeof(SectorObject));
        Entity e = em.CreateEntity(ea);
        em.AddComponentData(e, new Translation { Value = pos });
        em.AddComponentData(e, new SectorObject { radius = 1.0f }); ;
    }

    private void AddShip(float3 pos)
    {
        EntityManager em = World.DefaultGameObjectInjectionWorld.EntityManager;
        EntityArchetype ea = em.CreateArchetype(typeof(Translation), typeof(Ship));
        Entity e = em.CreateEntity(ea);
        em.AddComponentData(e, new Translation { Value = pos });
        em.AddComponentData(e, new Ship { });
    }

    private bool IsBorder(int[] raw)
    {
        foreach (int i in raw)
        {
            if (i == 0 || i == numSideNodes - 1)
            {
                return true;
            }
        }
        return false;
    }



    private const int maxFpsHistoryCount = 60;
    private List<float> fpsHistory = new List<float>();
    private float fps = 0;
    private void UpdateFPSCounter()
    {
        fpsHistory.Add(1f / Time.deltaTime);
        if (fpsHistory.Count > maxFpsHistoryCount) { fpsHistory.RemoveAt(0); }
        if (Time.frameCount % maxFpsHistoryCount == 0)
        {
            float total = 0;
            foreach (float f in fpsHistory)
            {
                total += f;
            }
            fps = total / fpsHistory.Count;
        }
    }

    private void OnDrawGizmos()
    {
        GUIStyle style = GUI.skin.label;
        style.fontSize = 6;
        Vector3 camPos = mainCamera.transform.position;
        Vector3 labelPos = new Vector3(camPos.x - 4.3f, camPos.y - 4.4f, 0);
        UnityEditor.Handles.Label(labelPos, "FPS: " + (int)(fps), style);
        
    }
}
