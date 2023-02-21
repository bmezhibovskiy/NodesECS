using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

public struct EntityComparerWithEM : IComparer<Entity>
{
    public EntityManager em;
    public float3 pos;
    public int Compare(Entity a, Entity b)
    {
        float3 tA = em.GetComponentData<Translation>(a).Value;
        float3 tB = em.GetComponentData<Translation>(b).Value;
        float distA = math.distancesq(tA, pos);
        float distB = math.distancesq(tB, pos);
        return distA.CompareTo(distB);
    }
}

public struct EntityComparerWithTD : IComparer<Entity>
{
    [ReadOnly] public float3 pos;
    [ReadOnly] public ComponentDataFromEntity<Translation> translationData;

    public int Compare(Entity x, Entity y)
    {
        if (x == y) { return 0; }
        if (x == Entity.Null) { return 1; }
        if (y == Entity.Null) { return -1; }

        float3 fX = translationData[x].Value;
        float3 fY = translationData[y].Value;
        float sqDistX = math.distancesq(fX, pos);
        float sqDistY = math.distancesq(fY, pos);
        return sqDistX.CompareTo(sqDistY);
    }
}