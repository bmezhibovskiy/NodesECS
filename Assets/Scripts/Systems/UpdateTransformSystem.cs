using System.Net.Sockets;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Core;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

[BurstCompile]
public partial struct UpdateConstantThrustJob: IJobEntity
{
    void Execute(ref Accelerating a, in ConstantThrust c)
    {
        a.accel = c.thrust;
    }
}

[BurstCompile]
public partial struct IntegrateAcceleratingJob : IJobEntity
{
    [ReadOnly] public TimeData timeData;
    [ReadOnly] public ComponentLookup<LocalToWorld> transformData;
    [ReadOnly] public ComponentLookup<Docked> dockedData;
    [ReadOnly] public ComponentLookup<Ship> shipData;
    [ReadOnly] public ComponentLookup<AffectedByNodes> affectedByNodesData;

    void Execute(ref Accelerating a, ref NextTransform nt, in Entity e)
    {
        //TODO: Refactor this to be in a different system, and maybe give Accelerating a locked bool.
        if (dockedData.HasComponent(e))
        {
            Docked docked = dockedData[e];
            if (docked.dockedAt != Entity.Null && !docked.isUndocking) { return; }
        }

        if (shipData.HasComponent(e))
        {
            Ship ship = shipData[e];
            if (ship.PreparingHyperspace()) { return; }
        }
        
        float dt = timeData.DeltaTime;
        float3 shipPos = nt.nextPos;

        float3 current = shipPos;
        if (affectedByNodesData.HasComponent(e))
        {
            //Before integrating, we want to move the current position towards the grid point it's supposed to be at
            //Since we're not updating the prevPos, this effectively increases velocity in the direction of that point
            //That grid point is recalculated every frame in another job, so the difference is very small.
            //This roughly achieves orbit-like mechanics, which moving the position (by updating prevPos) can't do.
            current += (affectedByNodesData[e].GridPosition(transformData) - shipPos) * dt;
        }

        nt.nextPos = 2 * current - a.prevPos + a.accel * (dt * dt);
        a.prevPos = current;
        a.vel = nt.nextPos - current;
    }
}

[BurstCompile]
public partial struct UpdateTransformsJob : IJobEntity
{
    void Execute(ref LocalToWorld t, in NextTransform nt, in Entity e)
    {
        float4x4 translation = float4x4.Translate(nt.nextPos);
        float angle = math.radians(Vector3.SignedAngle(Vector3.right, nt.facing, Vector3.forward));
        float4x4 rotation = float4x4.RotateZ(angle);
        float4x4 scale = float4x4.Scale(nt.scale);
        t.Value = math.mul(translation, math.mul(rotation, scale));
    }
}

[BurstCompile]
public partial struct UpdateChildTransformJob : IJobEntity
{
    [NativeDisableContainerSafetyRestriction] [ReadOnly] public ComponentLookup<LocalToWorld> transformData;
    void Execute(ref LocalToWorld transform, in RelativeTransform rt, in Parent p)
    {
        transform.Value = math.mul(transformData[p.Value].Value, rt.Value);
    }
}

[BurstCompile]
public partial struct UpdateTransformSystem : ISystem
{
    [ReadOnly] private ComponentLookup<LocalToWorld> transformData;
    [ReadOnly] public ComponentLookup<Docked> dockedData;
    [ReadOnly] public ComponentLookup<Ship> shipData;
    [ReadOnly] public ComponentLookup<AffectedByNodes> affectedByNodesData;


    [BurstCompile]
    public void OnCreate(ref SystemState systemState)
    {
        transformData = systemState.GetComponentLookup<LocalToWorld>();
        dockedData = systemState.GetComponentLookup<Docked>();
        shipData = systemState.GetComponentLookup<Ship>();
        affectedByNodesData = systemState.GetComponentLookup<AffectedByNodes>();
    }

    [BurstCompile]
    public void OnDestroy(ref SystemState systemState)
    {

    }

    [BurstCompile]
    public void OnUpdate(ref SystemState systemState)
    {
        transformData.Update(ref systemState);
        dockedData.Update(ref systemState);
        shipData.Update(ref systemState);
        affectedByNodesData.Update(ref systemState);

        systemState.Dependency = new UpdateChildTransformJob { transformData = transformData }.ScheduleParallel(systemState.Dependency);

        systemState.Dependency = new UpdateTransformsJob().ScheduleParallel(systemState.Dependency);

        systemState.Dependency = new UpdateConstantThrustJob().ScheduleParallel(systemState.Dependency);

        systemState.Dependency = new IntegrateAcceleratingJob { timeData = SystemAPI.Time, transformData = transformData, dockedData = dockedData, shipData = shipData, affectedByNodesData = affectedByNodesData }.ScheduleParallel(systemState.Dependency);
    }
}
