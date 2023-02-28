using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

public struct RelativeTransform: IComponentData
{
    public float4x4 Value;
    public float4x4 lastParentValue;
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
public partial struct UpdateChildTransformJob: IJobEntity
{
    void Execute(ref LocalToWorld transform, in RelativeTransform rt)
    {
        transform.Value = math.mul(rt.lastParentValue, rt.Value);
    }
}

[BurstCompile]
public partial struct UpdateChildTransformSystem : ISystem
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
    }
}
