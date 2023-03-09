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
    public enum Type { None, HoldPosition }
    public Type type;
    public float3 targetPos;
    public double thrustUntil;

}

[BurstCompile]
public partial struct UpdateAIJob : IJobEntity
{
    [ReadOnly] public TimeData timeData;
    [ReadOnly] public ComponentLookup<Ship> shipData;
    void Execute(ref Accelerating ac, ref NextTransform nt, ref AIData ai, in Entity e)
    {
        float dt = timeData.DeltaTime;
        switch(ai.type)
        {
            case AIData.Type.HoldPosition:
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

                if (speed <= 0) { break; }

                float3 targetFacing = -vel/speed;
                
                //Cosmetic rotation
                nt.facing = Vector3.RotateTowards(nt.facing, targetFacing, rotationSpeed, 0);

                //Cheating for now
                ac.accel += -vel / (dt * dt);

                break;
            default:
                return;
        }
    }
}


[BurstCompile]
public partial struct AISystem : ISystem
{
    [ReadOnly] private ComponentLookup<Ship> shipData;

    [BurstCompile]
    public void OnCreate(ref SystemState systemState)
    {
        shipData = SystemAPI.GetComponentLookup<Ship>();
    }

    [BurstCompile]
    public void OnDestroy(ref SystemState systemState)
    {

    }

    [BurstCompile]
    public void OnUpdate(ref SystemState systemState)
    {
        shipData.Update(ref systemState);

        systemState.Dependency = new UpdateAIJob { timeData = SystemAPI.Time, shipData = shipData }.ScheduleParallel(systemState.Dependency);
    }
}
