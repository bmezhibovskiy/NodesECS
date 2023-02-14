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
using Unity.Collections;
using static UnityEditor.UIElements.CurveField;
using System.Linq;
using Unity.Entities.UniversalDelegates;
using Unity.Assertions;
using static UnityEditor.PlayerSettings;

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

        float3 dir = sectorObjectPosition - nodePosition;
        float3 dir2 = math.cross(dir, new float3(0, 0, 1));
        //Inverse r squared law generalizes to inverse r^(dim-1)
        //However, we need to multiply denom by dir.magnitude to normalize dir
        //So that cancels with the fObj.dimension - 1, removing the - 1
        //However #2, dir.sqrMagnitude is cheaper, but will require bringing back the - 1
        float denom = Mathf.Pow(math.distancesq(sectorObjectPosition, nodePosition), (order - 1f));
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
        //updateNodeVelocities.Complete();
        //disposeSectorObjectsArray.Complete();
        //renderNodes.Complete();
        //updateGridNodePositions.Complete();
        //removeDeadNodes.Complete();

        NativeArray<Entity> sectorObjects = GetEntityQuery(typeof(SectorObject), typeof(Translation)).ToEntityArray(Allocator.TempJob);

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
                    float distSq = math.distancesq(translation.Value, soTranslation.Value);
                    if(distSq < soComponent.radius * soComponent.radius)
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
        Dependency = updateNodeVelocities;

        disposeSectorObjectsArray = sectorObjects.Dispose(updateNodeVelocities);
        Dependency = disposeSectorObjectsArray;

        renderNodes = Entities
            .WithAll<GridNode, Translation>()
            .ForEach(
            (in GridNode gridNode, in Translation translation) =>
            {
                float3 velVec = gridNode.velocity;
                if(math.distancesq(velVec, float3.zero) < 0.00001f)
                {
                    velVec = new float3(1, 0, 0) * 0.001f;
                }
                Debug.DrawRay(translation.Value, velVec * 20f);
            }
        ).ScheduleParallel(disposeSectorObjectsArray);
        Dependency = renderNodes;

        updateGridNodePositions = Entities
            .WithAll<Translation, GridNode>()
            .ForEach(
            (ref Translation translation, in Entity e, in GridNode gridNode) =>
            {   
                translation.Value += gridNode.velocity;
            }
        ).ScheduleParallel(renderNodes);
        Dependency = updateGridNodePositions;

        EntityCommandBuffer ecb = ecbSystem.CreateCommandBuffer();
        removeDeadNodes = Entities
            .WithAll<GridNode>()
            .ForEach(
            (in Entity e, in GridNode gridNode) =>
            {
                if (gridNode.isDead)
                {
                    ecb.DestroyEntity(e);
                }

            }
        ).Schedule(updateGridNodePositions);
        ecbSystem.AddJobHandleForProducer(removeDeadNodes);
        Dependency = removeDeadNodes;
    }

}

public struct ClosestNodes
{
    [ReadOnly] public const int numClosestNodes = 3;
    public Entity closestNode1;
    public Entity closestNode2;
    public Entity closestNode3;
    public Entity Get(int index)
    {
        switch (index)
        {
            case 0:
                return closestNode1;
            case 1:
                return closestNode2;
            case 2:
                return closestNode3;
            default:
                return Entity.Null;
        }
    }

    public void Set(Entity e, int index)
    {
        switch (index)
        {
            case 0:
                closestNode3 = closestNode2;
                closestNode2 = closestNode1;
                closestNode1 = e;
                break;
            case 1:
                closestNode3 = closestNode2;
                closestNode2 = e;
                break;
            case 2:
                closestNode3 = e;
                break;
            default:
                break;
        }
    }
}
public struct Ship : IComponentData
{
    public ClosestNodes closestNodes;
    public float3 nodeOffset;
}

public partial class ShipSystem : SystemBase
{
    private JobHandle updateShips;
    private JobHandle disposeNodesArray;

    [BurstCompile]
    protected override void OnUpdate()
    {
        ComponentDataFromEntity<Translation> translationData = GetComponentDataFromEntity<Translation>();

        NativeArray<Entity> nodes = GetEntityQuery(typeof(GridNode), typeof(Translation)).ToEntityArray(Allocator.TempJob);

        updateShips = Entities
            .WithAll<Ship, Translation>()
            .WithReadOnly(nodes)
            .WithReadOnly(translationData)
            .ForEach(
            (ref Ship s, in Translation t) =>
            {
                //Instead of just sorting the nodes array,
                //It should be faster to just find the closest K nodes (currently 3)
                //So, this algorithm has K*N iterations, where N is the total number of nodes
                //Since K is very small, this has a O(N).
                //Also, it doesn't require copying an entire array to sort it.
                float3 shipPos = t.Value;
                s.closestNodes = new ClosestNodes { closestNode1 = Entity.Null, closestNode2 = Entity.Null, closestNode3 = Entity.Null };
                for (int i = 0; i < nodes.Length; ++i)
                {
                    float3 nodePos = translationData[nodes[i]].Value;
                    float newSqMag = math.distancesq(nodePos, shipPos);

                    for (int j = 0; j < ClosestNodes.numClosestNodes; ++j)
                    {
                        Entity currentClosest = s.closestNodes.Get(j);
                        if (!translationData.HasComponent(currentClosest))
                        {
                            s.closestNodes.Set(nodes[i], j);
                            break;
                        }

                        float3 currentPos = translationData[currentClosest].Value;
                        float currentSqMag = math.distancesq(currentPos, shipPos);
                        if (newSqMag < currentSqMag)
                        {
                            s.closestNodes.Set(nodes[i], j);
                            break;
                        }
                    }
                }

                //Once we've updated the closest nodes, we can draw lines for debug visualization
                for (int i = 0; i < ClosestNodes.numClosestNodes; ++i)
                {
                    float3 nodePos = translationData[s.closestNodes.Get(i)].Value;
                    Debug.DrawLine(shipPos, nodePos);
                }
            }
            ).ScheduleParallel(Dependency);
        Dependency = updateShips;

        disposeNodesArray = nodes.Dispose(updateShips);
        Dependency = disposeNodesArray;
    }
}
public struct EntityComparer: IComparer<Entity>
{
    [ReadOnly] public float3 pos;
    [ReadOnly] public ComponentDataFromEntity<Translation> translationData;

    public int Compare(Entity x, Entity y)
    {
        float3 fX = translationData[x].Value;
        float3 fY = translationData[y].Value;
        float sqDistX = math.distancesq(fX, pos);
        float sqDistY = math.distancesq(fY, pos);
        return sqDistX.CompareTo(sqDistY);
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
        AddSectorObject(new float3(-2, 0, 0));
        AddSectorObject(new float3(2, 0, 0));
        AddShip(new float3(2, 2, 0));
        AddShip(new float3(-2, -2, 0));
    }

    // Update is called once per frame
    void Update()
    {
        UpdateFPSCounter();
    }



    float nodeDistance = 1.2f;
    float3 nodeOffset = new float3(0, 1, 0) * 1.2f;
    int numSideNodes = 101;
    int numNodes = 101 * 101;
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
