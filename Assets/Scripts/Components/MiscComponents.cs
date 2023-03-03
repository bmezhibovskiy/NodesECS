using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

public struct NextTransform: IComponentData
{
    public float3 facing;
    public float3 nextPos;
    public void Rotate(float speed)
    {
        facing = math.rotate(quaternion.RotateZ(speed), facing);
    }
}

public struct InitialTransform : IComponentData
{
    public float4x4 initialScale;
    public float4x4 initialRotation;
}

public struct RelativeTransform : IComponentData
{
    public float4x4 Value;
    public float4x4 lastParentValue;
}

public struct Player : IComponentData
{

}
