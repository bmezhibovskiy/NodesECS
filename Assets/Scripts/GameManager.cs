using UnityEngine;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Jobs;
using Unity.Transforms;
using Unity.Burst;
using Unity.Rendering.HybridV2;
using static Unity.Properties.PropertyPath;
using Unity.VisualScripting;
using static UnityEngine.EventSystems.EventTrigger;
using System.Collections.Generic;

public struct GridNode: IComponentData
{
    public float3 velocity;
    public bool isDead;
}

public struct SectorObject: IComponentData
{
    public float radius;
}


public partial class NodeSystem : SystemBase
{
    private JobHandle updateNodeVelocities;
    private JobHandle disposeSectorObjectsArray;
    private JobHandle renderNodes;
    private JobHandle updateGridNodePositions;
    private JobHandle removeDeadNodes;

    private static float3 NodeVelocityAt(float3 nodePosition, float3 sectorObjectPosition)
    {
        float order = 2f;
        float perpendicularStrength = 0f;
        float pullStrength = 0.01f;

        Vector3 dir = sectorObjectPosition - nodePosition;
        Vector3 dir2 = Vector3.Cross(dir, Vector3.forward);
        //Inverse r squared law generalizes to inverse r^(dim-1)
        //However, we need to multiply denom by dir.magnitude to normalize dir
        //So that cancels with the fObj.dimension - 1, removing the - 1
        //However #2, dir.sqrMagnitude is cheaper, but will require bringing back the - 1
        float denom = Mathf.Pow(dir.sqrMagnitude, (order - 1f));
        return (pullStrength / denom) * dir + (perpendicularStrength / denom) * dir2;
    }

    private EndSimulationEntityCommandBufferSystem ecbSystem;

    protected override void OnCreate()
    {
        ecbSystem = World.GetOrCreateSystem<EndSimulationEntityCommandBufferSystem>();
    }

    [BurstCompile]
    protected override void OnUpdate()
    {
        updateNodeVelocities.Complete();
        disposeSectorObjectsArray.Complete();
        renderNodes.Complete();
        updateGridNodePositions.Complete();
        removeDeadNodes.Complete();

        Unity.Collections.NativeArray<Entity> sectorObjects = GetEntityQuery(typeof(SectorObject), typeof(Translation)).ToEntityArray(Unity.Collections.Allocator.TempJob);

        updateNodeVelocities = Entities
            .WithAll<GridNode, Translation>()
            .WithReadOnly(sectorObjects)
            .ForEach(
            (ref GridNode gridNode, in Translation translation) =>
            {
                gridNode.velocity = float3.zero;
                for(int i = 0; i < sectorObjects.Length; ++i)
                {
                    Translation soTranslation = GetComponent<Translation>(sectorObjects[i]);
                    SectorObject soComponent = GetComponent<SectorObject>(sectorObjects[i]);
                    Vector3 dist = translation.Value - soTranslation.Value;
                    if(dist.magnitude < soComponent.radius)
                    {
                        gridNode.isDead = true;
                    }
                    else
                    {
                        gridNode.velocity += NodeVelocityAt(translation.Value, soTranslation.Value);
                    }
                }
            }
        ).ScheduleParallel(Dependency);

        disposeSectorObjectsArray = sectorObjects.Dispose(updateNodeVelocities);

        renderNodes = Entities
            .WithAll<GridNode, Translation>()
            .ForEach(
            (in GridNode gridNode, in Translation translation) =>
            {
                Vector3 velVec = gridNode.velocity;
                if(velVec.magnitude < 0.001f)
                {
                    velVec = Vector3.right * 0.001f;
                }
                Debug.DrawRay(translation.Value, velVec * 20f);
            }
        ).ScheduleParallel(disposeSectorObjectsArray);

        updateGridNodePositions = Entities
            .WithAll<Translation, GridNode>()
            .ForEach(
            (ref Translation translation, in GridNode gridNode) =>
            {   
                translation.Value += gridNode.velocity;
            }
        ).ScheduleParallel(renderNodes);

        EntityCommandBuffer ecb = ecbSystem.CreateCommandBuffer();
        removeDeadNodes = Entities.ForEach((Entity e, int entityInQueryIndex, in GridNode gridNode) =>
        {
            if (gridNode.isDead)
            {
                ecb.DestroyEntity(e);
            }
            
        }).Schedule(updateGridNodePositions);
        ecbSystem.AddJobHandleForProducer(removeDeadNodes);
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



    // Start is called before the first frame update
    void Start()
    {
        mainCamera.orthographic = true;
        GenerateNodes();
        AddSectorObject(new Vector3(-2, 0, 0));
        AddSectorObject(new Vector3(2, 0, 0));
        //World.DefaultGameObjectInjectionWorld.AddSystem<NodeSystem>(new NodeSystem());    
    }

    // Update is called once per frame
    void Update()
    {
        UpdateFPSCounter();
    }



    float nodeDistance = 1.2f;
    Vector3 nodeOffset = Vector3.up * 1.2f;
    int numSideNodes = 201;
    int numNodes = 201 * 201;
    bool is3d = false;
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
                AddNode(new Vector3(x, y, z) + nodeOffset, IsBorder(raw));
            }
            else
            {
                AddNode(new Vector3(x, y, 0) + nodeOffset, IsBorder(raw));
            }
        }
    }

    private void AddNode(Vector3 pos, bool isBorder)
    {
        EntityManager em = World.DefaultGameObjectInjectionWorld.EntityManager;
        EntityArchetype ea = em.CreateArchetype(typeof(Translation), typeof(GridNode));
        Entity e = em.CreateEntity(ea);
        em.AddComponentData(e, new Translation { Value = new float3(pos.x, pos.y, pos.z) });
        em.AddComponentData(e, new GridNode { velocity = float3.zero, isDead = false });
    }

    private void AddSectorObject(Vector3 pos)
    {
        EntityManager em = World.DefaultGameObjectInjectionWorld.EntityManager;
        EntityArchetype ea = em.CreateArchetype(typeof(Translation), typeof(SectorObject));
        Entity e = em.CreateEntity(ea);
        em.AddComponentData(e, new Translation { Value = new float3(pos.x, pos.y, pos.z) });
        em.AddComponentData(e, new SectorObject { radius = 1.0f }); ;
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



    private const int maxFpsHistoryCount = 30;
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
