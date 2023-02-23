using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

public struct EntityComparerWithEM : IComparer<Entity>
{
    [ReadOnly] public EntityManager em;
    [ReadOnly] public float3 pos;
    public int Compare(Entity a, Entity b)
    {
        float3 tA = em.GetComponentData<LocalTransform>(a).Position;
        float3 tB = em.GetComponentData<LocalTransform>(b).Position;
        float distA = math.distancesq(tA, pos);
        float distB = math.distancesq(tB, pos);
        return distA.CompareTo(distB);
    }
}

public struct EntityComparerWithTD : IComparer<Entity>
{
    [ReadOnly] public float3 pos;
    [ReadOnly] public ComponentLookup<LocalTransform> transformData;

    public int Compare(Entity x, Entity y)
    {
        if (x == y) { return 0; }
        if (x == Entity.Null) { return 1; }
        if (y == Entity.Null) { return -1; }

        float3 fX = transformData[x].Position;
        float3 fY = transformData[y].Position;
        float sqDistX = math.distancesq(fX, pos);
        float sqDistY = math.distancesq(fY, pos);
        return sqDistX.CompareTo(sqDistY);
    }
}