using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

public struct GridNode : IComponentData
{
    public float3 velocity;
    public bool isBorder;
}

public struct ClosestNodes
{
    public readonly static int numClosestNodes = 3;
    public readonly static ClosestNodes empty = new ClosestNodes
    {
        closestNode1 = Entity.Null,
        closestNode2 = Entity.Null,
        closestNode3 = Entity.Null
    };

    public Entity closestNode1;
    public Entity closestNode2;
    public Entity closestNode3;

    public Entity Get(int index)
    {
        switch (index)
        {
            case 0:
                return closestNode1;
            case 1:
                return closestNode2;
            case 2:
                return closestNode3;
            default:
                return Entity.Null;
        }
    }

    public void Set(Entity e, int index)
    {
        switch (index)
        {
            case 0:
                closestNode3 = closestNode2;
                closestNode2 = closestNode1;
                closestNode1 = e;
                break;
            case 1:
                closestNode3 = closestNode2;
                closestNode2 = e;
                break;
            case 2:
                closestNode3 = e;
                break;
            default:
                break;
        }
    }
}

public struct AffectedByNodes: IComponentData
{
    public ClosestNodes closestNodes;
    public float3 nodeOffset;
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
        return avgPos;
    }

    public float3 GridPosition(ComponentLookup<LocalToWorld> transformData)
    {
        return AverageNodePos(transformData) + nodeOffset;
    }
}

public struct NeedsConnection : IComponentData
{
    public Entity connection;
}

public struct NodeConnection : IComponentData
{
    public Entity a;
    public Entity b;

    public bool IsInvalid()
    {
        return a == Entity.Null || b == Entity.Null;
    }

    public void ReplaceNullEntity(Entity newEntity)
    {
        if (a == Entity.Null)
        {
            a = newEntity;
        }
        else if (b == Entity.Null)
        {
            b = newEntity;
        }
    }
}
