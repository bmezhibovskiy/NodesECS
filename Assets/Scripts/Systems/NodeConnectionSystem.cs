using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

[BurstCompile]
public partial struct UpdateConnectionsJob : IJobEntity
{
    [ReadOnly] public ComponentLookup<LocalToWorld> transformData;
    [ReadOnly] public ComponentLookup<GridNode> nodeData;
    public EntityCommandBuffer.ParallelWriter ecb;

    void Execute(ref NodeConnection nc, in Entity e, [EntityIndexInQuery] int entityInQueryIndex)
    {
        if(nc.IsInvalid()) { return; }

        float maxDist = 2.0f * Globals.sharedLevelInfo.Data.nodeDistance;
        if(maxDist <= 0) { return;  }

        float3 posA = transformData[nc.a].Position;
        float3 posB = transformData[nc.b].Position;

        if (math.distancesq(posA, posB) > maxDist * maxDist)
        {
            Globals.sharedEntityFactory.Data.CreateNodeAsync(entityInQueryIndex, ecb, 0.5f * (posA + posB), e);

            if (nodeData[nc.a].isBorder)
            {
                nc.b = Entity.Null;
            }
            else
            {
                nc.a = Entity.Null;
            }
        }
    }
}

[BurstCompile]
public partial struct ConnectToBorderJob : IJobEntity
{
    [ReadOnly] public NativeArray<Entity> entitiesThatNeedConnection;
    [ReadOnly] public ComponentLookup<NeedsConnection> needsConnectionData;
    public EntityCommandBuffer.ParallelWriter ecb;

    void Execute(ref NodeConnection nc, Entity e, [EntityIndexInQuery] int entityInQueryIndex)
    {
        for(int i = 0; i < entitiesThatNeedConnection.Length; ++i)
        {
            Entity candidate = entitiesThatNeedConnection[i];
            if (needsConnectionData[candidate].connection == e)
            {
                nc.ReplaceNullEntity(candidate);
                ecb.RemoveComponent<NeedsConnection>(entityInQueryIndex, candidate);
                break;
            }
        }
    }
}

[BurstCompile]
public partial struct NodeConnectionSystem : ISystem
{
    [ReadOnly] private ComponentLookup<LocalToWorld> transformData;
    [ReadOnly] private ComponentLookup<GridNode> nodeData;
    [ReadOnly] private ComponentLookup<NeedsConnection> needsConnectionData;

    private EntityQuery connectionEntitiesQuery;

    [BurstCompile]
    public void OnCreate(ref SystemState systemState)
    {
        transformData = SystemAPI.GetComponentLookup<LocalToWorld>();
        nodeData = SystemAPI.GetComponentLookup<GridNode>();
        needsConnectionData = SystemAPI.GetComponentLookup<NeedsConnection>();

        connectionEntitiesQuery = new EntityQueryBuilder(Allocator.Temp).WithAll<NeedsConnection>().Build(ref systemState);
    }

    [BurstCompile]
    public void OnDestroy(ref SystemState systemState)
    {

    }

    [BurstCompile]
    public void OnUpdate(ref SystemState systemState)
    {
        transformData.Update(ref systemState);
        nodeData.Update(ref systemState);
        needsConnectionData.Update(ref systemState);

        EndSimulationEntityCommandBufferSystem.Singleton ecbSystem = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>();

        NativeArray<Entity> entitiesThatNeedConnection = connectionEntitiesQuery.ToEntityArray(systemState.WorldUpdateAllocator);

        systemState.Dependency = new ConnectToBorderJob { entitiesThatNeedConnection = entitiesThatNeedConnection, needsConnectionData = needsConnectionData, ecb = ecbSystem.CreateCommandBuffer(systemState.WorldUnmanaged).AsParallelWriter() }.ScheduleParallel(systemState.Dependency);

        systemState.Dependency = new UpdateConnectionsJob{ transformData = transformData, nodeData = nodeData, ecb = ecbSystem.CreateCommandBuffer(systemState.WorldUnmanaged).AsParallelWriter() }.ScheduleParallel(systemState.Dependency);
        
    }
}
