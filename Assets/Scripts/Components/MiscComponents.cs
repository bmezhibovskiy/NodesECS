using Unity.Entities;
using Unity.Mathematics;

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
    public float4x4 lastParentValue;
}

public struct Player : IComponentData
{

}
