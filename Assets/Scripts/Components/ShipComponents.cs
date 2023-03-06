using System;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

public enum WeaponType
{
    StraightRocket, HomingRocket, StraightLaser, TurretLaser
}

public struct WeaponSlot: IEquatable<WeaponSlot>
{
    public readonly static WeaponSlot Empty = new WeaponSlot { slotIndex = 0 };

    public int slotIndex;
    public float3 relativePos;

    public WeaponType type;
    public float speed;
    public float damage;
    public bool isSecondary;
    public float secondsBetweenFire;
    public float lastFireSeconds;

    public bool Equals(WeaponSlot other)
    {
        return slotIndex == other.slotIndex;
    }
}

public struct WeaponSlots
{
    public readonly static int MaxWeaponSlots = 3;
    public readonly static WeaponSlots Empty = new WeaponSlots { slot1 = WeaponSlot.Empty, slot2 = WeaponSlot.Empty, slot3 = WeaponSlot.Empty };

    public WeaponSlot slot1;
    public WeaponSlot slot2;
    public WeaponSlot slot3;

    public WeaponSlot Get(int index)
    {
        switch (index)
        {
            case 0:
                return slot1;
            case 1:
                return slot2;
            case 2:
                return slot3;
            default:
                return WeaponSlot.Empty;
        }
    }

    public void Set(WeaponSlot w, int index)
    {
        w.slotIndex = index + 1;
        switch (index)
        {
            case 0:
                slot1 = w;
                break;
            case 1:
                slot2 = w;
                break;
            case 2:
                slot3 = w;
                break;
            default:
                break;
        }
    }

    public void Fire(int index, float currentTime)
    {
        WeaponSlot w = Get(index);
        Set(new WeaponSlot
        {
            slotIndex = w.slotIndex,
            damage = w.damage,
            isSecondary = w.isSecondary,
            lastFireSeconds = currentTime,
            relativePos = w.relativePos,
            secondsBetweenFire = w.secondsBetweenFire,
            speed = w.speed,
            type = w.type
        }, index);
    }
}

public struct Ship : IComponentData
{
    public float size;

    public ClosestNodes closestNodes;
    public float3 nodeOffset;
    public float3 prevPos;
    public float3 accel;
    public float3 vel;//derived from translation and prevPos
    public float thrust;
    public float rotationSpeed;

    public int hyperspaceNodesRequired;
    public int hyperspaceNodesGathered;
    public int hyperspaceTarget;

    public bool lightsOn;

    public WeaponSlots weaponSlots;
    public bool shootingPrimary;
    public bool shootingSecondary;
    public Entity target;

    public void AddThrust(float3 thrust)
    {
        accel += thrust;
    }

    public float3 AverageNodePos(ComponentLookup<LocalToWorld> transformData)
    {
        float3 avgPos = float3.zero;
        int numClosest = 0;
        for (int i = 0; i < ClosestNodes.numClosestNodes; ++i)
        {
            Entity closest = closestNodes.Get(i);
            if (transformData.HasComponent(closest))
            {
                avgPos += transformData[closest].Position;
                ++numClosest;
            }
        }
        if (numClosest > 0)
        {
            return avgPos / (float)numClosest;
        }
        return prevPos + vel;
    }

    public float3 GridPosition(ComponentLookup<LocalToWorld> transformData)
    {
        return AverageNodePos(transformData) + nodeOffset;
    }
    public void HandleCollisionAt(float3 collisionPos, float3 normal, float bounciness = 0.5f)
    {
        if (math.lengthsq(vel) < 0.00002f)
        {
            //Velocity too small, set to 0 instead of bouncing forever, which can cause instability
            prevPos = collisionPos;
            return;
        }

        //Reflect vel about normal
        vel = (vel - 2f * Vector3.Dot(vel, normal) * normal) * bounciness;

        //Would need time independent accel because otherwise we would need next frame's deltaTime to get the correct bounce
        //Verlet integration doesn't seem good for velocity based forces, since velocity is derived.
        //timeIndependentAccel += (-2 * normal * Vector3.Dot(vel, normal)) * bounciness;

        prevPos = collisionPos - vel;
    }

    public void StartHyperspace(int target, int nodesRequired)
    {
        hyperspaceTarget = target;
        hyperspaceNodesRequired = nodesRequired;
        hyperspaceNodesGathered = 0;
    }

    public bool PreparingHyperspace()
    {
        return hyperspaceNodesRequired > 0;
    }

    public bool ShouldJumpNow()
    {
        return PreparingHyperspace() && hyperspaceNodesGathered >= hyperspaceNodesRequired;
    }
}
