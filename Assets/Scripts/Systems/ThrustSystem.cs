using System.Collections;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Core;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.VisualScripting;
using UnityEngine;

public struct Thrust: IComponentData
{
    public readonly static float3 baseScaleVec = new float3(1, 1, 3);
    public static float4x4 BaseTransform(float scale = 1f)
    {
        return math.mul(float4x4.RotateX(math.radians(270)), math.mul(float4x4.Scale(baseScaleVec * scale), float4x4.Translate(new float3(0, 0, -0.01f))));
    }
    public int thrusterNumber;
}

public struct ThrustHaver : IComponentData
{
    public readonly static ThrustHaver Empty = new ThrustHaver { thrustEntity1 = Entity.Null, thrustEntity2 = Entity.Null, thrustEntity3 = Entity.Null, numThrusters = 0, shouldShowThrust = false };
    public static ThrustHaver One(float3 pos, float rotation, float scale, bool onByDefault)
    {
        ThrustHaver th = Empty;
        th.numThrusters = 1;
        th.thrustPos1 = pos;
        th.shouldShowThrust = onByDefault;
        th.scale = scale;
        th.rotation = rotation;
        return th;
    }
    public static ThrustHaver Two(float3 pos1, float3 pos2, float rotation, float scale, bool onByDefault)
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

    public static ThrustHaver Three(float3 pos1, float3 pos2, float pos3, float rotation, float scale, bool onByDefault)
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
    public float scale;
    public float rotation;
    public int numThrusters;
    public bool shouldShowThrust;

    public float4x4 Transform(int index, float thrustScaleAmount)
    {
        float4x4 thTransform = math.mul(float4x4.Translate(GetPos(index)), float4x4.RotateZ(math.radians(rotation)));
        return math.mul(thTransform, Thrust.BaseTransform(scale * thrustScaleAmount));
    }

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
public partial struct UpdateThrustTransformJob : IJobEntity
{
    [ReadOnly] public ComponentLookup<ThrustHaver> thrustHaverData;
    [ReadOnly] public ComponentLookup<Accelerating> acceleratingData;
    [ReadOnly] public TimeData timeData;
    void Execute(ref RelativeTransform rt, in Thrust t, in Parent p)
    {
        ThrustHaver th = thrustHaverData[p.Value];
        Accelerating ac = acceleratingData[p.Value];

        float thrustScale = 1 - 1 / (1 + math.length(ac.prevAccel)); // 1-1/(1+x) is the same as x/(x+1)

        float4x4 transform = th.Transform(t.thrusterNumber, thrustScale);

        rt.Value = transform;
    }
}

[BurstCompile]
public partial struct UpdateThrustDisplayJob : IJobEntity
{
    void Execute(ref ThrustHaver th, in Accelerating ac)
    {
        th.shouldShowThrust = math.lengthsq(ac.accel) > 0;
    }
}

[BurstCompile]
public partial struct ThrustSystem : ISystem
{
    [ReadOnly] private ComponentLookup<NeedsAssignThrustEntity> needsAssignThrustData;
    [ReadOnly] private ComponentLookup<ThrustHaver> thrustHaverData;
    [ReadOnly] private ComponentLookup<Accelerating> acceleratingData;
    private EntityQuery assignThrustEntitiesQuery;

    [BurstCompile]
    public void OnCreate(ref SystemState systemState)
    {
        needsAssignThrustData = SystemAPI.GetComponentLookup<NeedsAssignThrustEntity>();
        thrustHaverData = SystemAPI.GetComponentLookup<ThrustHaver>();
        acceleratingData = SystemAPI.GetComponentLookup<Accelerating>();
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
        thrustHaverData.Update(ref systemState);
        acceleratingData.Update(ref systemState);
        NativeArray<Entity> entitiesThatNeedAssignThrust = assignThrustEntitiesQuery.ToEntityArray(systemState.WorldUpdateAllocator);

        EndSimulationEntityCommandBufferSystem.Singleton ecbSystem = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>();

        systemState.Dependency = new AssignThrustEntityJob { entitiesThatNeedAssignThrust = entitiesThatNeedAssignThrust, needsAssignThrustData = needsAssignThrustData, ecb = ecbSystem.CreateCommandBuffer(systemState.WorldUnmanaged).AsParallelWriter() }.ScheduleParallel(systemState.Dependency);

        systemState.Dependency = new CreateThrustJob { ecb = ecbSystem.CreateCommandBuffer(systemState.WorldUnmanaged).AsParallelWriter() }.ScheduleParallel(systemState.Dependency);

        systemState.Dependency = new UpdateThrustTransformJob { thrustHaverData = thrustHaverData, acceleratingData = acceleratingData, timeData = SystemAPI.Time }.ScheduleParallel(systemState.Dependency);

        systemState.Dependency = new UpdateThrustDisplayJob().ScheduleParallel(systemState.Dependency);
    }
}
