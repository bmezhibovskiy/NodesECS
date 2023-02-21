using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

public struct NeedsConnection: IComponentData
{
    public Entity connection;
}

public struct NodeConnection : IComponentData
{
    public Entity a;
    public Entity b;

    public bool IsInvalid()
    {
        return a == Entity.Null || b == Entity.Null;
    }

    public void ReplaceNullEntity(Entity newEntity)
    {
        if(a == Entity.Null)
        {
            a = newEntity;
        }
        else if(b == Entity.Null)
        {
            b = newEntity;
        }
    }
}

[BurstCompile]
public partial struct UpdateConnectionsJob : IJobEntity
{
    [ReadOnly] public ComponentDataFromEntity<Translation> translationData;
    [ReadOnly] public ComponentDataFromEntity<GridNode> nodeData;
    public EntityCommandBuffer.ParallelWriter ecb;

    void Execute(ref NodeConnection nc, in Entity e, [EntityInQueryIndex] int entityInQueryIndex)
    {
        if(nc.IsInvalid()) { return; }

        float3 posA = translationData[nc.a].Value;
        float3 posB = translationData[nc.b].Value;

        Debug.DrawLine(posA, posB, Color.gray);

        if (math.distancesq(posA, posB) > 1.8f * 1.8f)
        {
            float3 newPos = 0.5f * (posA + posB);

            Entity newNode = ecb.CreateEntity(entityInQueryIndex);
            ecb.AddComponent(entityInQueryIndex, newNode, new GridNode { velocity = float3.zero, isDead = false, isBorder = false });
            ecb.AddComponent(entityInQueryIndex, newNode, new Translation { Value = newPos });
            ecb.AddComponent(entityInQueryIndex, newNode, new NeedsConnection { connection = e });

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
    [ReadOnly] public ComponentDataFromEntity<NeedsConnection> needsConnectionData;
    public EntityCommandBuffer.ParallelWriter ecb;

    void Execute(ref NodeConnection nc, Entity e, [EntityInQueryIndex] int entityInQueryIndex)
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

public partial class NodeConnectionSystem : SystemBase
{
    private EndSimulationEntityCommandBufferSystem ecbSystem;

    protected override void OnCreate()
    {
        ecbSystem = World.GetOrCreateSystem<EndSimulationEntityCommandBufferSystem>();
    }

    [BurstCompile]
    protected override void OnUpdate()
    {
        ComponentDataFromEntity<Translation> translationData = GetComponentDataFromEntity<Translation>();
        ComponentDataFromEntity<GridNode> nodeData = GetComponentDataFromEntity<GridNode>();
        ComponentDataFromEntity<NeedsConnection> needsConnectionData = GetComponentDataFromEntity<NeedsConnection>();

        Dependency = new UpdateConnectionsJob{ translationData = translationData, nodeData = nodeData, ecb = ecbSystem.CreateCommandBuffer().AsParallelWriter() }.ScheduleParallel(Dependency);
        ecbSystem.AddJobHandleForProducer(Dependency);

        //Needs dispose (entitiesThatNeedConnection)
        NativeArray<Entity> entitiesThatNeedConnection = GetEntityQuery(typeof(NeedsConnection)).ToEntityArray(Allocator.TempJob);

        Dependency = new ConnectToBorderJob { entitiesThatNeedConnection = entitiesThatNeedConnection, needsConnectionData = needsConnectionData, ecb = ecbSystem.CreateCommandBuffer().AsParallelWriter() }.ScheduleParallel(Dependency);
        ecbSystem.AddJobHandleForProducer(Dependency);

        //Gets disposed (entitiesThatNeedConnection)
        Dependency = entitiesThatNeedConnection.Dispose(Dependency);
    }
}
