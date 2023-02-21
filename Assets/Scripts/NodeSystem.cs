using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using static UnityEditor.PlayerSettings;

public struct GridNode : IComponentData
{
    public float3 velocity;
    public bool isDead;
    public bool isBorder;
}

[BurstCompile]
public partial struct UpdateNodeVelocitiesJob: IJobEntity
{
    [ReadOnly] public NativeArray<Entity> stationEntities;
    [ReadOnly] public ComponentDataFromEntity<Translation> translationData;
    [ReadOnly] public ComponentDataFromEntity<Station> stationData;
    void Execute(ref GridNode gridNode, in Entity e)
    {
        if (gridNode.isBorder) { return; }
        float3 nodePos = translationData[e].Value;
        gridNode.velocity = float3.zero;
        for (int i = 0; i < stationEntities.Length; ++i)
        {
            Translation sTranslation = translationData[stationEntities[i]];
            Station station = stationData[stationEntities[i]];
            float distSq = math.distancesq(nodePos, sTranslation.Value);
            if (distSq < station.size * station.size)
            {
                gridNode.isDead = true;
            }
            else
            {
                gridNode.velocity += NodeVelocityAt(nodePos, sTranslation.Value);
            }
        }
    }

    private static float3 NodeVelocityAt(float3 nodePosition, float3 sectorObjectPosition)
    {
        float order = 2f;
        float perpendicularStrength = 0f;
        float pullStrength = 0.01f;

        float3 dir = sectorObjectPosition - nodePosition;
        float3 dir2 = math.cross(dir, new float3(0, 0, 1));
        //Inverse r squared law generalizes to inverse r^(dim-1)
        //However, we need to multiply denom by dir.magnitude to normalize dir
        //So that cancels with the fObj.dimension - 1, removing the - 1
        //However #2, dir.sqrMagnitude is cheaper, but will require bringing back the - 1
        float denom = Mathf.Pow(math.distancesq(sectorObjectPosition, nodePosition), (order - 1f));
        return (pullStrength / denom) * dir + (perpendicularStrength / denom) * dir2;
    }
}

[BurstCompile]
public partial struct RenderNodesJob: IJobEntity
{
    void Execute(in GridNode gridNode, in Translation translation)
    {
        float3 velVec = gridNode.velocity;
        if (math.distancesq(velVec, float3.zero) < 0.000002f)
        {
            Utils.DebugDrawCircle(translation.Value, 0.02f, Color.white, 3);
        }
        else
        {
            Debug.DrawRay(translation.Value, -velVec * 30f);
        }
    }
}

[BurstCompile]
public partial struct UpdateNodePositionsJob: IJobEntity
{
    void Execute(ref Translation translation, in GridNode gridNode)
    {
        translation.Value += gridNode.velocity;
    }
}

[BurstCompile]
public partial struct RemoveDeadNodesJob: IJobEntity
{
    public EntityCommandBuffer.ParallelWriter ecb;
    void Execute(in Entity e, [EntityInQueryIndex] int entityInQueryIndex, in GridNode gridNode)
    {
        if (gridNode.isDead)
        {
            ecb.DestroyEntity(entityInQueryIndex, e);
        }
    }
}

public partial class NodeSystem : SystemBase
{
    private EndSimulationEntityCommandBufferSystem ecbSystem;

    protected override void OnCreate()
    {
        ecbSystem = World.GetOrCreateSystem<EndSimulationEntityCommandBufferSystem>();
    }

    [BurstCompile]
    protected override void OnUpdate()
    {
        EntityCommandBuffer.ParallelWriter ecb = ecbSystem.CreateCommandBuffer().AsParallelWriter();

        ComponentDataFromEntity<Translation> translationData = GetComponentDataFromEntity<Translation>();
        ComponentDataFromEntity<Station> stationData = GetComponentDataFromEntity<Station>();
        ComponentDataFromEntity<GridNode> nodeData = GetComponentDataFromEntity<GridNode>();

        //Needs dispose (stations)
        NativeArray<Entity> stations = GetEntityQuery(typeof(Station), typeof(Translation)).ToEntityArray(Allocator.TempJob);

        Dependency = new UpdateNodeVelocitiesJob { stationEntities = stations, translationData = translationData, stationData = stationData }.ScheduleParallel(Dependency);

        //Gets disposed (stations)
        Dependency = stations.Dispose(Dependency);

        Dependency = new RenderNodesJob().ScheduleParallel(Dependency);

        Dependency = new UpdateNodePositionsJob().ScheduleParallel(Dependency);

        Dependency = new RemoveDeadNodesJob { ecb = ecbSystem.CreateCommandBuffer().AsParallelWriter() }.ScheduleParallel(Dependency);
        ecbSystem.AddJobHandleForProducer(Dependency);
    }

}