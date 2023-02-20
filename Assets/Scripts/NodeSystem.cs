using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

public struct GridNode : IComponentData
{
    public float3 velocity;
    public bool isDead;
}

public struct SectorObject : IComponentData
{
    public float radius;
}

[BurstCompile]
public partial struct UpdateNodeVelocitiesJob: IJobEntity
{
    [ReadOnly] public NativeArray<Entity> sectorObjects;
    [ReadOnly] public ComponentDataFromEntity<Translation> translationData;
    [ReadOnly] public ComponentDataFromEntity<SectorObject> sectorObjectData;
    void Execute(ref GridNode gridNode, in Entity e)
    {
        Translation translation = translationData[e];
        gridNode.velocity = float3.zero;
        for (int i = 0; i < sectorObjects.Length; ++i)
        {
            Translation soTranslation = translationData[sectorObjects[i]];
            SectorObject soComponent = sectorObjectData[sectorObjects[i]];
            float distSq = math.distancesq(translation.Value, soTranslation.Value);
            if (distSq < soComponent.radius * soComponent.radius)
            {
                gridNode.isDead = true;
            }
            else
            {
                float multiplier = 1f;
                if (Globals.sharedInputState.Data.isSpaceDown) { multiplier = -1f; }
                gridNode.velocity += multiplier * NodeVelocityAt(translation.Value, soTranslation.Value);
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
        if (math.distancesq(velVec, float3.zero) < 0.00001f)
        {
            velVec = new float3(1, 0, 0) * 0.001f;
        }
        Debug.DrawRay(translation.Value, velVec * 20f);
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
    private JobHandle updateNodeVelocities;
    private JobHandle disposeSectorObjectsArray;
    private JobHandle renderNodes;
    private JobHandle updateGridNodePositions;
    private JobHandle removeDeadNodes;

    private EndSimulationEntityCommandBufferSystem ecbSystem;

    protected override void OnCreate()
    {
        ecbSystem = World.GetOrCreateSystem<EndSimulationEntityCommandBufferSystem>();
    }

    [BurstCompile]
    protected override void OnUpdate()
    {
        ComponentDataFromEntity<Translation> translationData = GetComponentDataFromEntity<Translation>();
        ComponentDataFromEntity<SectorObject> sectorObjectData = GetComponentDataFromEntity<SectorObject>();

        NativeArray<Entity> sectorObjects = GetEntityQuery(typeof(SectorObject), typeof(Translation)).ToEntityArray(Allocator.TempJob);

        updateNodeVelocities = new UpdateNodeVelocitiesJob { sectorObjects = sectorObjects, translationData = translationData, sectorObjectData = sectorObjectData }.ScheduleParallel(Dependency);
        Dependency = updateNodeVelocities;

        disposeSectorObjectsArray = sectorObjects.Dispose(Dependency);
        Dependency = disposeSectorObjectsArray;

        renderNodes = new RenderNodesJob().ScheduleParallel(Dependency);
        Dependency = renderNodes;

        updateGridNodePositions = new UpdateNodePositionsJob().ScheduleParallel(Dependency);
        Dependency = updateGridNodePositions;

        removeDeadNodes = new RemoveDeadNodesJob { ecb = ecbSystem.CreateCommandBuffer().AsParallelWriter() }.ScheduleParallel(Dependency);
        ecbSystem.AddJobHandleForProducer(removeDeadNodes);
        Dependency = removeDeadNodes;
    }

}