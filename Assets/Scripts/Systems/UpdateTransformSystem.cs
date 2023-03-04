using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

[BurstCompile]
public partial struct UpdateTransformsJob : IJobEntity
{
    void Execute(ref LocalToWorld t, in NextTransform nt)
    {
        float4x4 translation = float4x4.Translate(nt.nextPos);
        float angle = math.radians(Vector3.SignedAngle(Vector3.right, nt.facing, Vector3.forward));
        float4x4 rotation = float4x4.RotateZ(angle);
        float4x4 scale = float4x4.Scale(nt.scale);
        t.Value = math.mul(translation, math.mul(rotation, scale));
    }
}

[BurstCompile]
public partial struct PopulateParentTransformJob : IJobEntity
{
    [ReadOnly] public ComponentLookup<LocalToWorld> transformData;
    void Execute(ref RelativeTransform rt, in Parent p)
    {
        rt.lastParentValue = transformData[p.Value].Value;
    }
}

[BurstCompile]
public partial struct UpdateChildTransformJob : IJobEntity
{
    void Execute(ref LocalToWorld transform, in RelativeTransform rt)
    {
        transform.Value = math.mul(rt.lastParentValue, rt.Value);
    }
}

[BurstCompile]
public partial struct UpdateTransformSystem : ISystem
{
    [ReadOnly] private ComponentLookup<LocalToWorld> transformData;


    [BurstCompile]
    public void OnCreate(ref SystemState systemState)
    {
        transformData = SystemAPI.GetComponentLookup<LocalToWorld>();
    }

    [BurstCompile]
    public void OnDestroy(ref SystemState systemState)
    {

    }

    [BurstCompile]
    public void OnUpdate(ref SystemState systemState)
    {
        transformData.Update(ref systemState);

        systemState.Dependency = new PopulateParentTransformJob { transformData = transformData }.ScheduleParallel(systemState.Dependency);

        systemState.Dependency = new UpdateChildTransformJob().ScheduleParallel(systemState.Dependency);

        systemState.Dependency = new UpdateTransformsJob().ScheduleParallel(systemState.Dependency);
    }
}
