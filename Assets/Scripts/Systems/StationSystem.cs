using UnityEngine;
using Unity.Collections;
using Unity.Entities;
using Unity.Burst;
using Unity.Transforms;
using Unity.Mathematics;
using Unity.Collections.LowLevel.Unsafe;

[BurstCompile]
public partial struct RotateStationsJob : IJobEntity
{
    void Execute(ref NextTransform nt, in Station s)
    {
        float rSpeed = 0.01f;
        nt.Rotate(rSpeed);
    }
}

[BurstCompile]
public partial struct UpdateDockedJob : IJobEntity
{
    [ReadOnly] public ComponentLookup<LocalToWorld> transformData;
    [NativeDisableContainerSafetyRestriction] [ReadOnly] public ComponentLookup<NextTransform> nextTransformData;
    void Execute(ref NextTransform nt, in Docked docked)
    {
        if(docked.dockedAt == Entity.Null || docked.isUndocking) { return; }

        float3 stationFacing = nextTransformData[docked.dockedAt].facing;
        float3 stationPos = transformData[docked.dockedAt].Position;
        float angle = math.radians(Vector3.SignedAngle(docked.initialFacing, stationFacing, Vector3.forward));

        float4x4 initialPos = float4x4.Translate(docked.initialPos);
        float4x4 inversePos = float4x4.Translate(-stationPos);
        float4x4 rotation = float4x4.RotateZ(angle);
        float4x4 pos = float4x4.Translate(stationPos);

        float4 nextPos = math.mul(pos, math.mul(rotation, math.mul(inversePos, initialPos))).c3;
        nt.nextPos = new float3(nextPos.x, nextPos.y, nextPos.z);
        nt.facing = math.normalize(nt.nextPos - stationPos);
        

    }
}

[BurstCompile]
public partial struct RenderStationsJob: IJobEntity
{
    void Execute(in Station s, in LocalToWorld t)
    {
        Utils.DebugDrawCircle(t.Position, s.size, Color.white, 20);
    }
}

[BurstCompile]
public partial struct StationSystem : ISystem
{
    [ReadOnly] private ComponentLookup<LocalToWorld> transformData;
    [ReadOnly] private ComponentLookup<NextTransform> nextTransformData;

    [BurstCompile]
    public void OnCreate(ref SystemState systemState)
    {
        transformData = SystemAPI.GetComponentLookup<LocalToWorld>();
        nextTransformData = SystemAPI.GetComponentLookup<NextTransform>();
    }

    [BurstCompile]
    public void OnDestroy(ref SystemState systemState)
    {
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState systemState)
    {
        transformData.Update(ref systemState);
        nextTransformData.Update(ref systemState);

        systemState.Dependency = new RotateStationsJob().ScheduleParallel(systemState.Dependency);

        systemState.Dependency = new UpdateDockedJob { transformData = transformData, nextTransformData = nextTransformData }.ScheduleParallel(systemState.Dependency);

        systemState.Dependency = new RenderStationsJob().ScheduleParallel(systemState.Dependency);   
    }
}
