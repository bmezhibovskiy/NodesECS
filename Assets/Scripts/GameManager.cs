using UnityEngine;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.Burst;
using System.Collections.Generic;
using Unity.Collections;

public static class Globals
{
    public readonly static SharedStatic<InputState> sharedInputState = SharedStatic<InputState>.GetOrCreate<InputStateKey>();
    public readonly static SharedStatic<TimeState> sharedTimeState = SharedStatic<TimeState>.GetOrCreate<TimeStateKey>();
    private class InputStateKey { }
    private class TimeStateKey { }

    static Globals()
    {
        sharedInputState.Data.Initialize();
        sharedTimeState.Data.Initialize();
    }
}
public struct TimeState
{
    public float deltaTime;
    public void Initialize()
    {
        deltaTime = 0.0167f;
    }
}

public struct InputState
{
    public bool isSpaceDown;
    public bool isIKeyDown;
    public bool isUpKeyDown;
    public bool isDownKeyDown;
    public bool isLeftKeyDown;
    public bool isRightKeyDown;
    public void Initialize()
    {
        isSpaceDown = false;
        isIKeyDown = false;
        isUpKeyDown = false;
        isDownKeyDown = false;
        isLeftKeyDown = false;
        isRightKeyDown = false;
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

    //TODO: Get these from file
    private float nodeDistance = 1.2f;
    private float3 nodeOffset = new float3(0, 1, 0) * 1.2f;
    private int numSideNodes = 17;
    private int numNodes = 17 * 17;
    private bool is3d = false;
    //

    private EntityManager em;
    private Entity playerEntity;

    void Start()
    {
        em = World.DefaultGameObjectInjectionWorld.EntityManager;

        mainCamera.orthographic = true;
 
        GenerateNodes();
        GenerateConnections();
        AddSectorObject(new float3(-2, 0, 0));
        playerEntity = AddShip(new float3(0, 0, 0), true);
    }

    // Update is called once per frame
    void Update()
    {
        Globals.sharedTimeState.Data.deltaTime = Time.deltaTime;
 
        Vector3 shipPos = em.GetComponentData<Translation>(playerEntity).Value;
        mainCamera.transform.position = new Vector3(shipPos.x, shipPos.y, mainCamera.transform.position.z);

        UpdateInput();

        UpdateFPSCounter();
    }

    private void UpdateInput()
    {
        Globals.sharedInputState.Data.isSpaceDown = Input.GetKey(KeyCode.Space);
        Globals.sharedInputState.Data.isIKeyDown = Input.GetKey(KeyCode.I);
        Globals.sharedInputState.Data.isUpKeyDown = Input.GetKey(KeyCode.UpArrow);
        Globals.sharedInputState.Data.isDownKeyDown = Input.GetKey(KeyCode.DownArrow);
        Globals.sharedInputState.Data.isLeftKeyDown = Input.GetKey(KeyCode.LeftArrow);
        Globals.sharedInputState.Data.isRightKeyDown = Input.GetKey(KeyCode.RightArrow);
    }

    private void GenerateNodes()
    {
        for (int i = 0; i < numNodes; i++)
        {
            int2 raw2D = Utils.to2D(i, numSideNodes);
            int3 raw3D = Utils.to3D(i, numSideNodes);

            int[] raw = is3d ? new int[] { raw3D.x, raw3D.y, raw3D.z } : new int[] { raw2D.x, raw2D.y };
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
    private void GenerateConnections()
    {
        NativeArray<Entity> entities = em.GetAllEntities();
        for(int i = 0; i < entities.Length; ++i)
        {
            if (!em.HasComponent<GridNode>(entities[i])) { continue; }

            GridNode gridNode = em.GetComponentData<GridNode>(entities[i]);

            if (!gridNode.isBorder) { continue; }

            float3 gridNodePos = em.GetComponentData<Translation>(entities[i]).Value;

            NativeArray<Entity> closest = AllClosestNodes(gridNodePos);
            for(int j = 0; j < closest.Length; ++j)
            {
                GridNode closestGridNode = em.GetComponentData<GridNode>(closest[j]);
                if(!closestGridNode.isBorder)
                {
                    AddConnection(entities[i], closest[j]);
                    break;
                }
            }   
        }
    }

    private void AddConnection(Entity a, Entity b)
    {
        EntityArchetype ea = em.CreateArchetype(typeof(NodeConnection));
        Entity e = em.CreateEntity(ea);
        em.AddComponentData(e, new NodeConnection { a = a, b = b });
    }

    private NativeArray<Entity> AllClosestNodes(float3 pos)
    {
        NativeArray<Entity> allEntities = em.GetAllEntities();
        int numNodes = 0;
        for (int i = 0; i < allEntities.Length; ++i)
        {
            if (em.HasComponent<GridNode>(allEntities[i]))
            {
                ++numNodes;
            }
        }
        NativeArray<Entity> nodes = new NativeArray<Entity>(numNodes, Allocator.Temp);
        int currentNode = 0;
        for (int i = 0; i < allEntities.Length; ++i)
        {
            if (em.HasComponent<GridNode>(allEntities[i]))
            {
                nodes[currentNode++] = allEntities[i];
            }
        }

        nodes.Sort(new EntityComparerWithEM { em = em, pos = pos });
        return nodes;
    }

    private void AddNode(float3 pos, bool isBorder)
    {
        EntityArchetype ea = em.CreateArchetype(typeof(Translation), typeof(GridNode));
        Entity e = em.CreateEntity(ea);
        em.AddComponentData(e, new Translation { Value = pos });
        em.AddComponentData(e, new GridNode { velocity = float3.zero, isDead = false, isBorder = isBorder });
    }

    private void AddSectorObject(float3 pos)
    {
        EntityArchetype ea = em.CreateArchetype(typeof(Translation), typeof(Station));
        Entity e = em.CreateEntity(ea);
        em.AddComponentData(e, new Translation { Value = pos });
        em.AddComponentData(e, new Station { size = 1f });
    }

    private Entity AddShip(float3 pos, bool isPlayer)
    {
        EntityArchetype ea = isPlayer ? 
            em.CreateArchetype(typeof(Translation), typeof(Ship), typeof(Player)) :
            em.CreateArchetype(typeof(Translation), typeof(Ship));

        Entity e = em.CreateEntity(ea);
        em.AddComponentData(e, new Translation { Value = pos });
        em.AddComponentData(e, new Ship {
            closestNodes = ClosestNodes.empty,
            nodeOffset = float3.zero,
            prevPos = pos,
            nextPos = pos,
            facing = new float3(0,1,0),
            accel = float3.zero,
            vel = float3.zero
        }); 
        if(isPlayer)
        {
            em.AddComponentData(e, new Player { });
        }
        return e;
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
