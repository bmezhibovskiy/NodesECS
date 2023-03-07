using System.Collections;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.VisualScripting;
using UnityEngine;

public struct ThrustHaver : IComponentData
{
    public readonly static ThrustHaver Empty = new ThrustHaver { thrustEntity1 = Entity.Null, thrustEntity2 = Entity.Null, thrustEntity3 = Entity.Null, numThrusters = 0, shouldShowThrust = false };
    public static ThrustHaver One(float3 pos, float4x4 rotation, float4x4 scale, bool onByDefault)
    {
        ThrustHaver th = Empty;
        th.numThrusters = 1;
        th.thrustPos1 = pos;
        th.shouldShowThrust = onByDefault;
        th.scale = scale;
        th.rotation = rotation;
        return th;
    }
    public static ThrustHaver Two(float3 pos1, float3 pos2, float4x4 rotation, float4x4 scale, bool onByDefault)
    {
        ThrustHaver th = Empty;
        th.numThrusters = 2;
        th.thrustPos1 = pos1;
        th.thrustPos2 = pos2;
        th.shouldShowThrust = onByDefault;
        th.scale = scale;
        th.rotation = rotation;
        return th;
    }

    public static ThrustHaver Three(float3 pos1, float3 pos2, float pos3, float4x4 rotation, float4x4 scale, bool onByDefault)
    {
        ThrustHaver th = Empty;
        th.numThrusters = 3;
        th.thrustPos1 = pos1;
        th.thrustPos2 = pos2;
        th.thrustPos3 = pos3;
        th.shouldShowThrust = onByDefault;
        th.scale = scale;
        th.rotation = rotation;
        return th;
    }
    public Entity thrustEntity1;
    public Entity thrustEntity2;
    public Entity thrustEntity3;
    public float3 thrustPos1;
    public float3 thrustPos2;
    public float3 thrustPos3;
    public float4x4 scale;
    public float4x4 rotation;
    public int numThrusters;
    public bool shouldShowThrust;

    public float3 GetPos(int index)
    {
        switch(index)
        {
            case 0:
                return thrustPos1;
            case 1:
                return thrustPos2;
            case 3:
                return thrustPos3;
            default:
                return float3.zero;
        }
    }
    public Entity Get(int index)
    {
        switch(index)
        {
            case 0:
                return thrustEntity1;
            case 1:
                return thrustEntity2;
            case 2:
                return thrustEntity3;
            default:
                return Entity.Null;
        }
    }

    public void Set(int index, Entity e)
    {
        switch (index)
        {
            case 0:
                thrustEntity1 = e;
                break;
            case 1:
                thrustEntity2 = e;
                break;
            case 3:
                thrustEntity3 = e;
                break;
            default:
                break;
        }
    }
}

public struct NeedsAssignThrustEntity: IComponentData
{
    public Entity parentEntity;
    public int thrusterNumber;
}

[BurstCompile]
public partial struct DestroyThrustJob : IJobEntity
{
    public EntityCommandBuffer.ParallelWriter ecb;
    void Execute(ref ThrustHaver th, [EntityIndexInQuery] int entityInQueryIndex)
    {
        if(th.shouldShowThrust) { return; }
        if(th.thrustEntity1 == Entity.Null) { return; }
        for (int i = 0; i < th.numThrusters; ++i)
        {
            Entity toDestroy = th.Get(i);
            if ( toDestroy.Index < 0 ) { continue; }
            ecb.DestroyEntity(entityInQueryIndex, toDestroy);
            th.Set(i, Entity.Null);
        }
    }
}

[BurstCompile]
public partial struct CreateThrustJob : IJobEntity
{
    public EntityCommandBuffer.ParallelWriter ecb;
    void Execute(ref ThrustHaver th, in LocalToWorld ltw, in Entity e, [EntityIndexInQuery] int entityInQueryIndex)
    {
        if (!th.shouldShowThrust) { return; }
        if (th.thrustEntity1 != Entity.Null) { return; }

        for (int i = 0; i < th.numThrusters; ++i)
        {
            Entity newThrust = Globals.sharedEntityFactory.Data.CreateThrust1Async(entityInQueryIndex, ecb, e, th, i, ltw.Value);

            //Will get replaced with the real Entity in another job. This will serve to be different than Entity.Null
            th.Set(i, newThrust);
        }
    }
}

[BurstCompile]
public partial struct AssignThrustEntityJob : IJobEntity
{
    [ReadOnly] public NativeArray<Entity> entitiesThatNeedAssignThrust;
    [ReadOnly] public ComponentLookup<NeedsAssignThrustEntity> needsAssignThrustData;
    public EntityCommandBuffer.ParallelWriter ecb;

    void Execute(ref ThrustHaver th, Entity e, [EntityIndexInQuery] int entityInQueryIndex)
    {
        for (int i = 0; i < entitiesThatNeedAssignThrust.Length; ++i)
        {
            Entity candidate = entitiesThatNeedAssignThrust[i];
            NeedsAssignThrustEntity nate = needsAssignThrustData[candidate];
            if (nate.parentEntity == e)
            {
                th.Set(nate.thrusterNumber, candidate);
                ecb.RemoveComponent<NeedsAssignThrustEntity>(entityInQueryIndex, candidate);
                break;
            }
        }
    }
}

    [BurstCompile]
public partial struct ThrustSystem : ISystem
{
    [ReadOnly] private ComponentLookup<NeedsAssignThrustEntity> needsAssignThrustData;
    private EntityQuery assignThrustEntitiesQuery;

    [BurstCompile]
    public void OnCreate(ref SystemState systemState)
    {
        needsAssignThrustData = SystemAPI.GetComponentLookup<NeedsAssignThrustEntity>();
        assignThrustEntitiesQuery = new EntityQueryBuilder(Allocator.Temp).WithAll<NeedsAssignThrustEntity>().Build(ref systemState);
    }

    [BurstCompile]
    public void OnDestroy(ref SystemState systemState)
    {

    }

    [BurstCompile]
    public void OnUpdate(ref SystemState systemState)
    {
        needsAssignThrustData.Update(ref systemState);
        NativeArray<Entity> entitiesThatNeedAssignThrust = assignThrustEntitiesQuery.ToEntityArray(systemState.WorldUpdateAllocator);

        EndSimulationEntityCommandBufferSystem.Singleton ecbSystem = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>();

        systemState.Dependency = new AssignThrustEntityJob { entitiesThatNeedAssignThrust = entitiesThatNeedAssignThrust, needsAssignThrustData = needsAssignThrustData, ecb = ecbSystem.CreateCommandBuffer(systemState.WorldUnmanaged).AsParallelWriter() }.ScheduleParallel(systemState.Dependency);

        systemState.Dependency = new DestroyThrustJob { ecb = ecbSystem.CreateCommandBuffer(systemState.WorldUnmanaged).AsParallelWriter() }.ScheduleParallel(systemState.Dependency);

        systemState.Dependency = new CreateThrustJob { ecb = ecbSystem.CreateCommandBuffer(systemState.WorldUnmanaged).AsParallelWriter() }.ScheduleParallel(systemState.Dependency);
    }
}
