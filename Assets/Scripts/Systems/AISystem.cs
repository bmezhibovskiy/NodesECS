using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.Core;

public struct AIData: IComponentData
{
    public enum Type { None, HoldPosition, GoToPosition, Follow }
    public Type type;
    public float3 targetPos;
    public double thrustUntil;
    public Entity target;

}

[BurstCompile]
public partial struct UpdateAIJob : IJobEntity
{
    [ReadOnly] public TimeData timeData;
    [ReadOnly] public ComponentLookup<Ship> shipData;
    [ReadOnly] public ComponentLookup<LocalToWorld> transformData;
    void Execute(ref Accelerating ac, ref NextTransform nt, ref AIData ai, in Entity e)
    {
        float dt = timeData.DeltaTime;
        float rotationSpeed = 1.0f;
        float thrust = 1.0f;
        if (shipData.HasComponent(e))
        {
            Ship ship = shipData[e];
            rotationSpeed = ship.rotationSpeed;
            thrust = ship.thrust;
        }
        rotationSpeed *= dt;

        float3 vel = ac.vel;
        float speed = math.length(vel);
        float3 targetFacing = nt.facing;
        switch (ai.type)
        {
            case AIData.Type.HoldPosition:
                if (speed <= 0) { break; }

                //Cosmetic rotation
                targetFacing = -vel/speed;

                //Cheating for now
                ac.accel += -vel / (dt * dt);

                break;
            case AIData.Type.GoToPosition:
                //Two phases:
                //1a: turn towards target
                //1b: accelerate to speed relative to distance
                //2a: turn away from target
                //2b: accelerate down to zero speed, stopping at target
                break;
            case AIData.Type.Follow:
                if (speed > 0.05f)
                {
                    targetFacing = -vel / speed;
                    float angle = Vector3.Angle(nt.facing, targetFacing);
                    if (angle < 1)
                    {
                        ac.accel += nt.facing * thrust;
                    }
                }
                else
                {
                    float3 followPos = transformData[ai.target].Position;
                    float3 dist = followPos - nt.nextPos;
                    float distMag = math.length(dist);
                    targetFacing = dist / distMag;
                    if (distMag > 1.0)
                    {
                        ac.accel += nt.facing * thrust;
                    }
                }

                


                break;
            default:
                break;
        }

        nt.facing = Vector3.RotateTowards(nt.facing, targetFacing, rotationSpeed, 0);
    }
}


[BurstCompile]
public partial struct AISystem : ISystem
{
    [ReadOnly] private ComponentLookup<Ship> shipData;
    [ReadOnly] private ComponentLookup<LocalToWorld> transformData;

    [BurstCompile]
    public void OnCreate(ref SystemState systemState)
    {
        shipData = SystemAPI.GetComponentLookup<Ship>();
        transformData = SystemAPI.GetComponentLookup<LocalToWorld>();
    }

    [BurstCompile]
    public void OnDestroy(ref SystemState systemState)
    {

    }

    [BurstCompile]
    public void OnUpdate(ref SystemState systemState)
    {
        shipData.Update(ref systemState);
        transformData.Update(ref systemState);

        systemState.Dependency = new UpdateAIJob { timeData = SystemAPI.Time, shipData = shipData, transformData = transformData }.ScheduleParallel(systemState.Dependency);
    }
}
