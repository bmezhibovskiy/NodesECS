using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEditor.MemoryProfiler;
using UnityEngine;

public struct NeedsConnection: IComponentData
{
    public Entity connection;
}

public struct NodeConnection : IComponentData
{
    public Entity a;
    public Entity b;
}

[BurstCompile]
public partial struct UpdateConnectionsJob : IJobEntity
{
    [ReadOnly] public ComponentDataFromEntity<Translation> translationData;
    [ReadOnly] public ComponentDataFromEntity<GridNode> nodeData;
    public EntityCommandBuffer.ParallelWriter ecb;

    void Execute(ref NodeConnection nc, in Entity e, [EntityInQueryIndex] int entityInQueryIndex)
    {
        if(nc.a == Entity.Null || nc.b == Entity.Null) { return; }

        float3 posA = translationData[nc.a].Value;
        float3 posB = translationData[nc.b].Value;

        Debug.DrawLine(posA, posB);

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

    void Execute(ref NodeConnection nc, Entity e)
    {
        for(int i = 0; i < entitiesThatNeedConnection.Length; ++i)
        {
            if (needsConnectionData[entitiesThatNeedConnection[i]].connection == e)
            {
                if(nc.a == Entity.Null)
                {
                    nc.a = entitiesThatNeedConnection[i];
                }
                else
                {
                    nc.b = entitiesThatNeedConnection[i];
                }
            }
        }
    }
}

[BurstCompile]
public partial struct RemoveNeedsConnectionJob: IJobEntity
{
    public EntityCommandBuffer.ParallelWriter ecb;
    void Execute(Entity e, [EntityInQueryIndex] int entityInQueryIndex)
    {
        ecb.RemoveComponent<NeedsConnection>(entityInQueryIndex, e);       
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

        //Needs dispose (entitiesThatNeedConnection)
        NativeArray<Entity> entitiesThatNeedConnection = GetEntityQuery(typeof(NeedsConnection)).ToEntityArray(Allocator.TempJob);

        Dependency = new UpdateConnectionsJob{ translationData = translationData, nodeData = nodeData, ecb = ecbSystem.CreateCommandBuffer().AsParallelWriter() }.ScheduleParallel(Dependency);
        ecbSystem.AddJobHandleForProducer(Dependency);

        Dependency = new ConnectToBorderJob { entitiesThatNeedConnection = entitiesThatNeedConnection, needsConnectionData = needsConnectionData }.ScheduleParallel(Dependency);

        //Gets disposed (entitiesThatNeedConnection)
        Dependency = entitiesThatNeedConnection.Dispose(Dependency);

        Dependency = new RemoveNeedsConnectionJob { ecb = ecbSystem.CreateCommandBuffer().AsParallelWriter() }.ScheduleParallel(Dependency);
        ecbSystem.AddJobHandleForProducer(Dependency);
        
    }
}
