using UnityEngine;
using Unity.Collections;
using Unity.Entities;
using Unity.Burst;
using Unity.Transforms;
using Unity.Mathematics;

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

    [BurstCompile]
    public void OnCreate(ref SystemState systemState)
    {
    }

    [BurstCompile]
    public void OnDestroy(ref SystemState systemState)
    {
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState systemState)
    {
        systemState.Dependency = new RenderStationsJob().ScheduleParallel(systemState.Dependency);   
    }
}
