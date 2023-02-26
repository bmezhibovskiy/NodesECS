using Unity.Burst;
using Unity.Collections;
using Unity.Core;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

[BurstCompile]
public partial struct DestroyAllEntitiesJob: IJobEntity
{
    public EntityCommandBuffer.ParallelWriter ecb;
    void Execute(in DestroyOnLevelUnload dolu, in Entity e, [EntityIndexInQuery] int entityInQueryIndex)
    {
        ecb.DestroyEntity(entityInQueryIndex, e);
    }
}

[BurstCompile]
public partial struct CleanupSystem : ISystem
{
    [BurstCompile]
    public void OnCreate(ref SystemState systemState)
    {

    }
    [BurstCompile]
    public void OnDestroy(ref SystemState systemState)
    {

    }
    [BurstCompile]
    public void OnUpdate(ref SystemState systemState)
    {
        if(!Globals.sharedLevelInfo.Data.needsDestroy) { return; }

        BeginSimulationEntityCommandBufferSystem.Singleton ecbSystem = SystemAPI.GetSingleton<BeginSimulationEntityCommandBufferSystem.Singleton>();
        systemState.Dependency = new DestroyAllEntitiesJob { ecb = ecbSystem.CreateCommandBuffer(systemState.WorldUnmanaged).AsParallelWriter() }.ScheduleParallel(systemState.Dependency);
    }
}
