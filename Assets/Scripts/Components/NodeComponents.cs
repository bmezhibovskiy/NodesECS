using Unity.Entities;
using Unity.Mathematics;

public struct GridNode : IComponentData
{
    public float3 velocity;
    public bool isDead;
    public bool isBorder;
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
