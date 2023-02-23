using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

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
    [ReadOnly] public ComponentLookup<LocalTransform> transformData;
    [ReadOnly] public ComponentLookup<Station> stationData;
    void Execute(ref GridNode gridNode, in Entity e)
    {
        if (gridNode.isBorder) { return; }

        float3 nodePos = transformData[e].Position;

        gridNode.velocity = float3.zero;
        for (int i = 0; i < stationEntities.Length; ++i)
        {
            Station station = stationData[stationEntities[i]];
            float3 stationPos = transformData[stationEntities[i]].Position;
            float distSq = math.distancesq(stationPos, nodePos);

            for (int j = 0; j < station.modules.Count; ++j)
            {
                StationModule sm = station.modules.Get(j);
                switch (sm.type)
                {
                    case StationModuleType.NodePuller:
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
                    case StationModuleType.NodeEater:
                        if (distSq < station.size * station.size)
                        {
                            gridNode.isDead = true;
                            break;
                        }
                        break;
                    default:
                        break;
                }

            }
        }
    }
}

[BurstCompile]
public partial struct RenderNodesJob: IJobEntity
{
    void Execute(in GridNode gridNode, in LocalTransform t)
    {
        float3 velVec = gridNode.velocity;
        if (math.distancesq(velVec, float3.zero) < 0.000002f)
        {
            Utils.DebugDrawCircle(t.Position, 0.02f, Color.white, 3);
        }
        else
        {
            Debug.DrawRay(t.Position, -velVec * 30f);
        }
    }
}

[BurstCompile]
public partial struct UpdateNodePositionsJob: IJobEntity
{
    void Execute(ref LocalTransform translation, in GridNode gridNode)
    {
        translation.Position += gridNode.velocity;
    }
}

[BurstCompile]
public partial struct RemoveDeadNodesJob: IJobEntity
{
    public EntityCommandBuffer.ParallelWriter ecb;
    void Execute(in Entity e, [EntityIndexInQuery] int entityInQueryIndex, in GridNode gridNode)
    {
        if (gridNode.isDead)
        {
            ecb.DestroyEntity(entityInQueryIndex, e);
        }
    }
}

[BurstCompile]
public partial struct NodeSystem : ISystem
{
    [ReadOnly] private ComponentLookup<LocalTransform> transformData;
    [ReadOnly] private ComponentLookup<Station> stationData;

    private EntityQuery stationQuery;

    [BurstCompile]
    public void OnCreate(ref SystemState systemState)
    {
        transformData = systemState.GetComponentLookup<LocalTransform>();
        stationData = systemState.GetComponentLookup<Station>();

        stationQuery = new EntityQueryBuilder(Allocator.Temp).WithAll<Station, LocalTransform>().Build(ref systemState);
    }

    [BurstCompile]
    public void OnDestroy(ref SystemState systemState)
    {

    }

    [BurstCompile]
    public void OnUpdate(ref SystemState systemState)
    {
        transformData.Update(ref systemState);
        stationData.Update(ref systemState);

        EndSimulationEntityCommandBufferSystem.Singleton ecbSystem = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>();
        
        NativeArray<Entity> stations = stationQuery.ToEntityArray(systemState.WorldUpdateAllocator);

        systemState.Dependency = new UpdateNodesWithStationsJob { stationEntities = stations, transformData = transformData, stationData = stationData }.ScheduleParallel(systemState.Dependency);

        systemState.Dependency = new RenderNodesJob().ScheduleParallel(systemState.Dependency);

        systemState.Dependency = new UpdateNodePositionsJob().ScheduleParallel(systemState.Dependency);

        systemState.Dependency = new RemoveDeadNodesJob { ecb = ecbSystem.CreateCommandBuffer(systemState.WorldUnmanaged).AsParallelWriter() }.ScheduleParallel(systemState.Dependency);
    }

}