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
    private class InputStateKey { }

    static Globals()
    {
        sharedInputState.Data.Initialize();
    }
}

public struct InputState
{
    public bool isSpaceDown;
    public bool isIKeyDown;
    public void Initialize()
    {
        isSpaceDown = false;
        isIKeyDown = false;
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
    int numSideNodes = 99;
    int numNodes = 99 * 99;
    bool is3d = false;

    // Start is called before the first frame update
    void Start()
    {
        float cellSize = 0.5f;
        float sideSize = nodeDistance * numSideNodes * 1.25f;
        SpatialHasher.sharedInstance.Initialize(cellSize, sideSize);

        mainCamera.orthographic = true;
 
        GenerateNodes();
        AddSectorObject(new float3(-10, 0, 0));
        AddShip(new float3(2, 2, 0));
        AddShip(new float3(-2, -2, 0));
    }

    private void OnDestroy()
    {
        SpatialHasher.sharedInstance.Dispose();
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

        if (Input.GetKey(KeyCode.I))
        {
            Globals.sharedInputState.Data.isIKeyDown = true;
        }
        else
        {
            Globals.sharedInputState.Data.isIKeyDown = false;
        }

        UpdateFPSCounter();
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

    private void AddNode(float3 pos, bool isBorder)
    {
        EntityManager em = World.DefaultGameObjectInjectionWorld.EntityManager;
        EntityArchetype ea = em.CreateArchetype(typeof(Translation), typeof(GridNode));
        Entity e = em.CreateEntity(ea);
        em.AddComponentData(e, new Translation { Value = pos });
        em.AddComponentData(e, new GridNode { velocity = float3.zero, isDead = false });
        SpatiallyHashed hashed = new SpatiallyHashed { };
        SpatialHasher.sharedInstance.Add(ref hashed, e, pos);
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
