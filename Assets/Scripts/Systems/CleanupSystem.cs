using Unity.Burst;
using Unity.Collections;
using Unity.Core;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

public struct NeedsDestroy: IComponentData
{
    public readonly static NeedsDestroy Now = new NeedsDestroy { destroyTime = 0, confirmDestroy = true };
    public double destroyTime;
    public bool confirmDestroy;
}

//This job should be near all the other accelerating jobs, not in this system
[BurstCompile]
public partial struct ClearAccelerationJob : IJobEntity
{
    void Execute(ref Accelerating a)
    {
        a.prevAccel = a.accel;
        a.accel = float3.zero;
    }
}

[BurstCompile]
public partial struct DestroyNeededEntitiesJob : IJobEntity
{
    [ReadOnly] public TimeData timeData;
    [ReadOnly] public NativeArray<Entity> entitiesThatHaveParents;
    [ReadOnly] public ComponentLookup<Parent> parentData;
    public EntityCommandBuffer.ParallelWriter ecb;
    void Execute(in NeedsDestroy nd, in Entity e, [EntityIndexInQuery] int entityInQueryIndex)
    {
        if (timeData.ElapsedTime < nd.destroyTime || nd.confirmDestroy == false) { return; }

        for(int i = 0; i < entitiesThatHaveParents.Length; ++i)
        {
            Entity child = entitiesThatHaveParents[i];
            if (parentData[child].Value == e)
            {
                ecb.DestroyEntity(entityInQueryIndex, child);
            }
        }
        ecb.DestroyEntity(entityInQueryIndex, e);
    }
}

[BurstCompile]
public partial struct DestroyAllEntitiesJob : IJobEntity
{
    public EntityCommandBuffer.ParallelWriter ecb;
    void Execute(in DestroyOnLevelUnload dolu, in Entity e, [EntityIndexInQuery] int entityInQueryIndex)
    {
        ecb.DestroyEntity(entityInQueryIndex, e);
    }
}

[UpdateInGroup(typeof(PresentationSystemGroup), OrderFirst = true)]
[BurstCompile]
public partial struct CleanupSystem : ISystem
{
    [ReadOnly] private ComponentLookup<Parent> parentData;
    private EntityQuery childEntityQuery;

    [BurstCompile]
    public void OnCreate(ref SystemState systemState)
    {
        parentData = SystemAPI.GetComponentLookup<Parent>();
        childEntityQuery = new EntityQueryBuilder(Allocator.Temp).WithAll<Parent>().Build(ref systemState);
    }
    [BurstCompile]
    public void OnDestroy(ref SystemState systemState)
    {

    }


    [BurstCompile]
    public void OnUpdate(ref SystemState systemState)
    {
        parentData.Update(ref systemState);

        BeginSimulationEntityCommandBufferSystem.Singleton ecbSystem = SystemAPI.GetSingleton<BeginSimulationEntityCommandBufferSystem.Singleton>();

        NativeArray<Entity> childEntities = childEntityQuery.ToEntityArray(systemState.WorldUpdateAllocator);

        systemState.Dependency = new ClearAccelerationJob().ScheduleParallel(systemState.Dependency);

        systemState.Dependency = new DestroyNeededEntitiesJob { timeData = SystemAPI.Time, entitiesThatHaveParents = childEntities, parentData = parentData, ecb = ecbSystem.CreateCommandBuffer(systemState.WorldUnmanaged).AsParallelWriter() }.ScheduleParallel(systemState.Dependency);

        if (!Globals.sharedLevelInfo.Data.needsDestroy) { return; }

        systemState.Dependency = new DestroyAllEntitiesJob { ecb = ecbSystem.CreateCommandBuffer(systemState.WorldUnmanaged).AsParallelWriter() }.ScheduleParallel(systemState.Dependency);
    }
}
