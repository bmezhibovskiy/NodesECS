using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

public struct DestroyOnLevelUnload : IComponentData
{

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
}

public struct Accelerating: IComponentData
{
    public float3 prevPos;
    public float3 prevAccel;
    public float3 accel;
    public float3 vel;
    public double accelStartedAt;

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

public struct WeaponShot : IComponentData
{
    public Entity Shooter;
    public float size;
}

public struct AreaOfEffect: IComponentData
{
    public float radius;
}

public struct Player : IComponentData
{

}
