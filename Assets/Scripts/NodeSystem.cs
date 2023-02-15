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

public partial class NodeSystem : SystemBase
{
    private JobHandle updateNodeVelocities;
    private JobHandle disposeSectorObjectsArray;
    private JobHandle renderNodes;
    private JobHandle updateGridNodePositions;
    private JobHandle updateSpatialHasher;
    private JobHandle removeDeadNodes;

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

    private EndSimulationEntityCommandBufferSystem ecbSystem;

    protected override void OnCreate()
    {
        ecbSystem = World.GetOrCreateSystem<EndSimulationEntityCommandBufferSystem>();
    }

    [BurstCompile]
    protected override void OnUpdate()
    {
        //updateNodeVelocities.Complete();
        //disposeSectorObjectsArray.Complete();
        //renderNodes.Complete();
        //updateGridNodePositions.Complete();
        //removeDeadNodes.Complete();

        NativeArray<Entity> sectorObjects = GetEntityQuery(typeof(SectorObject), typeof(Translation)).ToEntityArray(Allocator.TempJob);

        updateNodeVelocities = Entities
            .WithAll<GridNode>()
            .WithReadOnly(sectorObjects)
            .ForEach(
            (ref Entity e, ref GridNode gridNode) =>
            {
                Translation translation = GetComponent<Translation>(e);
                gridNode.velocity = float3.zero;
                for (int i = 0; i < sectorObjects.Length; ++i)
                {
                    Translation soTranslation = GetComponent<Translation>(sectorObjects[i]);
                    SectorObject soComponent = GetComponent<SectorObject>(sectorObjects[i]);
                    float distSq = math.distancesq(translation.Value, soTranslation.Value);
                    if (distSq < soComponent.radius * soComponent.radius)
                    {
                        gridNode.isDead = true;
                    }
                    else
                    {
                        float multiplier = 1f;
                        if(Globals.sharedInputState.Data.isSpaceDown) { multiplier = -1f; }
                        gridNode.velocity += multiplier * NodeVelocityAt(translation.Value, soTranslation.Value);
                    }
                }
            }
        ).ScheduleParallel(Dependency);
        Dependency = updateNodeVelocities;

        disposeSectorObjectsArray = sectorObjects.Dispose(updateNodeVelocities);
        Dependency = disposeSectorObjectsArray;

        renderNodes = Entities
            .WithAll<GridNode, Translation>()
            .ForEach(
            (in GridNode gridNode, in Translation translation) =>
            {
                float3 velVec = gridNode.velocity;
                if (math.distancesq(velVec, float3.zero) < 0.00001f)
                {
                    velVec = new float3(1, 0, 0) * 0.001f;
                }
                Debug.DrawRay(translation.Value, velVec * 20f);
            }
        ).ScheduleParallel(disposeSectorObjectsArray);
        Dependency = renderNodes;

        updateGridNodePositions = Entities
            .WithAll<Translation, GridNode>()
            .ForEach(
            (ref Translation translation, in GridNode gridNode) =>
            {
                translation.Value += gridNode.velocity;
            }
        ).ScheduleParallel(renderNodes);
        Dependency = updateGridNodePositions;

        updateSpatialHasher = Entities.WithAll<SpatiallyHashed, Translation>().ForEach(
            (ref SpatiallyHashed hashed, in Entity e, in Translation translation) =>
            {
                Globals.sharedSpatialHasher.Data.Update(ref hashed, e, translation.Value);
            }
            ).ScheduleParallel(updateGridNodePositions);
        Dependency = updateSpatialHasher;

        EntityCommandBuffer.ParallelWriter ecb = ecbSystem.CreateCommandBuffer().AsParallelWriter();
        removeDeadNodes = Entities
            .WithAll<GridNode, Translation, SpatiallyHashed>()
            .ForEach(
            (ref SpatiallyHashed hashed, in Entity e, in int entityInQueryIndex, in GridNode gridNode, in Translation translation) =>
            {
                if (gridNode.isDead)
                {
                    Globals.sharedSpatialHasher.Data.Remove(hashed, e);
                    ecb.DestroyEntity(entityInQueryIndex, e);
                }
            }
        ).ScheduleParallel(updateSpatialHasher);
        ecbSystem.AddJobHandleForProducer(removeDeadNodes);
        Dependency = removeDeadNodes;
    }

}