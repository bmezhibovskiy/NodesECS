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

    public float thrust;
    public float rotationSpeed;

    public bool afterburnerOn;

    public int hyperspaceNodesRequired;
    public int hyperspaceNodesGathered;
    public int hyperspaceTarget;

    public bool lightsOn;

    public WeaponSlots weaponSlots;
    public bool shootingPrimary;
    public bool shootingSecondary;
    public Entity target;

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
