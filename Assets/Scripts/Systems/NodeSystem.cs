using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using static UnityEditor.PlayerSettings;

public struct GridNode : IComponentData
{
    public float3 velocity;
    public bool isDead;
    public bool isBorder;
}

[BurstCompile]
public partial struct UpdateNodesWithStationsJob: IJobEntity
{
    [ReadOnly] public NativeArray<Entity> stationEntities;
    [ReadOnly] public ComponentDataFromEntity<Translation> translationData;
    [ReadOnly] public ComponentDataFromEntity<Station> stationData;
    void Execute(ref GridNode gridNode, in Entity e)
    {
        if (gridNode.isBorder) { return; }

        float3 nodePos = translationData[e].Value;

        gridNode.velocity = float3.zero;
        for (int i = 0; i < stationEntities.Length; ++i)
        {
            float3 stationPos = translationData[stationEntities[i]].Value;
            Station station = stationData[stationEntities[i]];

            for (int j = 0; j < station.modules.Count; ++j)
            {
                StationModule sm = station.modules.Get(j);
                switch (sm.type)
                {
                    //Only the NodePuller type affects nodes
                    case StationModuleType.NodePuller:

                        float distSq = math.distancesq(stationPos, nodePos);
                        if (distSq < station.size * station.size)
                        {
                            gridNode.isDead = true;
                            break;
                        }

                        float order = sm.GetParam(0);
                        float pullStrength = sm.GetParam(1);
                        float perpendicularStrength = sm.GetParam(2);

                        float3 dir = stationPos - nodePos;
                        float3 dir2 = math.cross(dir, new float3(0, 0, 1));
                        //Inverse r squared law generalizes to inverse r^(dim-1)
                        //However, we need to multiply denom by dir.magnitude to normalize dir
                        //So that cancels with the fObj.dimension - 1, removing the - 1
                        //However #2, dir.sqrMagnitude is cheaper, but will require bringing back the - 1
                        float denom = math.pow(distSq, (order - 1f));
                        gridNode.velocity += (pullStrength / denom) * dir + (perpendicularStrength / denom) * dir2;
                        break;
                    default:
                        break;
                }

            }
        }
    }

    private static void UpdateNodeWithStation(ref GridNode gridNode, Station station, float3 nodePos, float3 stationPos)
    {
        
    }
}

[BurstCompile]
public partial struct RenderNodesJob: IJobEntity
{
    void Execute(in GridNode gridNode, in Translation translation)
    {
        float3 velVec = gridNode.velocity;
        if (math.distancesq(velVec, float3.zero) < 0.000002f)
        {
            Utils.DebugDrawCircle(translation.Value, 0.02f, Color.white, 3);
        }
        else
        {
            Debug.DrawRay(translation.Value, -velVec * 30f);
        }
    }
}

[BurstCompile]
public partial struct UpdateNodePositionsJob: IJobEntity
{
    void Execute(ref Translation translation, in GridNode gridNode)
    {
        translation.Value += gridNode.velocity;
    }
}

[BurstCompile]
public partial struct RemoveDeadNodesJob: IJobEntity
{
    public EntityCommandBuffer.ParallelWriter ecb;
    void Execute(in Entity e, [EntityInQueryIndex] int entityInQueryIndex, in GridNode gridNode)
    {
        if (gridNode.isDead)
        {
            ecb.DestroyEntity(entityInQueryIndex, e);
        }
    }
}

public partial class NodeSystem : SystemBase
{
    private EndSimulationEntityCommandBufferSystem ecbSystem;

    protected override void OnCreate()
    {
        ecbSystem = World.GetOrCreateSystem<EndSimulationEntityCommandBufferSystem>();
    }

    [BurstCompile]
    protected override void OnUpdate()
    {
        EntityCommandBuffer.ParallelWriter ecb = ecbSystem.CreateCommandBuffer().AsParallelWriter();

        ComponentDataFromEntity<Translation> translationData = GetComponentDataFromEntity<Translation>();
        ComponentDataFromEntity<Station> stationData = GetComponentDataFromEntity<Station>();
        ComponentDataFromEntity<GridNode> nodeData = GetComponentDataFromEntity<GridNode>();

        //Needs dispose (stations)
        NativeArray<Entity> stations = GetEntityQuery(typeof(Station), typeof(Translation)).ToEntityArray(Allocator.TempJob);

        Dependency = new UpdateNodesWithStationsJob { stationEntities = stations, translationData = translationData, stationData = stationData }.ScheduleParallel(Dependency);

        //Gets disposed (stations)
        Dependency = stations.Dispose(Dependency);

        Dependency = new RenderNodesJob().ScheduleParallel(Dependency);

        Dependency = new UpdateNodePositionsJob().ScheduleParallel(Dependency);

        Dependency = new RemoveDeadNodesJob { ecb = ecbSystem.CreateCommandBuffer().AsParallelWriter() }.ScheduleParallel(Dependency);
        ecbSystem.AddJobHandleForProducer(Dependency);
    }

}