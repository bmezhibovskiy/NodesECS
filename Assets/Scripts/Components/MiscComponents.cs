using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

public struct DestroyOnLevelUnload : IComponentData
{

}

public struct InitialTransform: IComponentData
{
    public float4x4 Value;
}

public struct NextTransform: IComponentData
{
    public float3 facing;
    public float3 nextPos;
    public float scale;

    public void Rotate(float speed)
    {
        facing = math.rotate(quaternion.RotateZ(speed), facing);
    }
}

public struct RelativeTransform : IComponentData
{
    public float4x4 Value;
    public float4x4 lastParentValue;
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

public struct Accelerating: IComponentData
{
    public float3 prevPos;
    public float3 prevAccel;
    public float3 accel;
    public float3 vel;
    public ClosestNodes closestNodes;
    public float3 nodeOffset;
    public double accelStartedAt;

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
        vel = (vel - 2f * math.dot(vel, normal) * normal) * bounciness;

        //Would need time independent accel because otherwise we would need next frame's deltaTime to get the correct bounce
        //Verlet integration doesn't seem good for velocity based forces, since velocity is derived.
        //timeIndependentAccel += (-2 * normal * Vector3.Dot(vel, normal)) * bounciness;

        prevPos = collisionPos - vel;
    }
}

public struct ConstantThrust: IComponentData
{
    public float3 thrust;
}

public struct Player : IComponentData
{

}
