using Unity.Burst;
using Unity.Collections;
using Unity.Core;
using Unity.Entities;
using Unity.Entities.UniversalDelegates;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

[BurstCompile]
public partial struct ResetNodeVelocitiesJob : IJobEntity
{
    void Execute(ref GridNode node)
    {
        node.velocity = float3.zero;
    }
}
[BurstCompile]
public partial struct UpdateNodesWithShipsJob : IJobEntity
{
    [ReadOnly] public NativeArray<Entity> shipEntities;
    [ReadOnly] public ComponentLookup<LocalToWorld> transformData;
    [ReadOnly] public ComponentLookup<Ship> shipData;
    public EntityCommandBuffer.ParallelWriter ecb;

    void Execute(ref GridNode gridNode, in LocalToWorld t, Entity e, [EntityIndexInQuery] int entityInQueryIndex)
    {
        if (gridNode.isDead || gridNode.isBorder) { return; }

        float3 pos = t.Position;
        for(int i = 0; i < shipEntities.Length; ++i)
        {
            Entity shipEntity = shipEntities[i];
            if(!shipData.HasComponent(shipEntity) || !transformData.HasComponent(shipEntity)) { continue; }

            Ship ship = shipData[shipEntity];

            if(!ship.PreparingHyperspace()) { continue; }

            LocalToWorld shipTransform = transformData[shipEntity];
            float3 shipPos = shipTransform.Position;

            float3 dir = shipPos - pos;
            float distSq = math.lengthsq(dir);

            if (distSq < ship.size * ship.size)
            {
                gridNode.isDead = true;

                ship.hyperspaceNodesGathered += 1;
                ecb.SetComponent(entityInQueryIndex, shipEntity, ship);
            }
            else
            {
                float order = 2;
                float pullStrength = 0.02f;
                float denom = math.pow(distSq, (order - 1f));
                gridNode.velocity += (pullStrength / denom) * dir;
            }

        }
        
    }
}
[BurstCompile]
public partial struct UpdateNodesWithStationsJob: IJobEntity
{
    [ReadOnly] public NativeArray<Entity> stationEntities;
    [ReadOnly] public ComponentLookup<LocalToWorld> transformData;
    [ReadOnly] public ComponentLookup<Station> stationData;
    void Execute(ref GridNode gridNode, in Entity e)
    {
        if (gridNode.isBorder || gridNode.isDead) { return; }

        float3 nodePos = transformData[e].Position;

        for (int i = 0; i < stationEntities.Length; ++i)
        {
            Station station = stationData[stationEntities[i]];
            float3 stationPos = transformData[stationEntities[i]].Position;
            float distSq = math.distancesq(stationPos, nodePos);

            for (int j = 0; j < station.modules.Count; ++j)
            {
                StationModule sm = station.modules.Get(j);
                switch (sm.type)
                {
                    case StationModuleType.NodePuller:
                        float order = sm.GetParam(0);
                        float pullStrength = sm.GetParam(1);
                        float perpendicularStrength = sm.GetParam(2);

                        float3 dir = stationPos - nodePos;
                        float3 dir2 = math.cross(dir, new float3(0, 0, 1));
                        //Inverse r squared law generalizes to inverse r^(dim-1)
                        //However, we need to multiply denom by dir.magnitude to normalize dir
                        //So that cancels with the fObj.dimension - 1, removing the - 1
                        //However #2, dir.sqrMagnitude is cheaper, but will require bringing back the - 1
                        float denom = math.pow(distSq, (order - 1f));
                        gridNode.velocity += (pullStrength / denom) * dir + (perpendicularStrength / denom) * dir2;
                        break;
                    case StationModuleType.NodeEater:
                        if (distSq < station.size * station.size)
                        {
                            gridNode.isDead = true;
                            break;
                        }
                        break;
                    default:
                        break;
                }

            }
        }
    }
}

[BurstCompile]
public partial struct UpdateNodeTransformsJob: IJobEntity
{
    [ReadOnly] public TimeData timeData;
    void Execute(ref LocalToWorld transform, in GridNode gridNode)
    {
        if(gridNode.isBorder || gridNode.isDead) { return; }

        float defaultScale = Globals.sharedLevelInfo.Data.nodeSize;
        float stretchX = math.length(gridNode.velocity) * 30f;
        float4x4 scale = float4x4.Scale(stretchX + defaultScale, defaultScale, defaultScale);

        float angle = math.radians(Vector3.SignedAngle(Vector3.right, gridNode.velocity, Vector3.forward));
        float4x4 rotation = float4x4.RotateZ(angle);

        float4x4 translation = float4x4.Translate(transform.Position + gridNode.velocity);

        transform.Value = math.mul(translation, math.mul(rotation, scale));
    }
}

[BurstCompile]
public partial struct RemoveDeadNodesJob: IJobEntity
{
    public EntityCommandBuffer.ParallelWriter ecb;
    void Execute(in Entity e, [EntityIndexInQuery] int entityInQueryIndex, in GridNode gridNode)
    {
        if (gridNode.isDead)
        {
            ecb.DestroyEntity(entityInQueryIndex, e);
        }
    }
}

[BurstCompile]
public partial struct NodeSystem : ISystem
{
    [ReadOnly] private ComponentLookup<LocalToWorld> transformData;
    [ReadOnly] private ComponentLookup<Station> stationData;
    [ReadOnly] private ComponentLookup<Ship> shipData;

    private EntityQuery stationQuery;
    private EntityQuery shipQuery;

    [BurstCompile]
    public void OnCreate(ref SystemState systemState)
    {
        transformData = systemState.GetComponentLookup<LocalToWorld>();
        stationData = systemState.GetComponentLookup<Station>();
        shipData = systemState.GetComponentLookup<Ship>();

        stationQuery = new EntityQueryBuilder(Allocator.Temp).WithAll<Station, LocalToWorld>().Build(ref systemState);
        shipQuery = new EntityQueryBuilder(Allocator.Temp).WithAll<Ship, LocalToWorld>().Build(ref systemState);
    }

    [BurstCompile]
    public void OnDestroy(ref SystemState systemState)
    {

    }

    [BurstCompile]
    public void OnUpdate(ref SystemState systemState)
    {
        transformData.Update(ref systemState);
        stationData.Update(ref systemState);
        shipData.Update(ref systemState);

        EndSimulationEntityCommandBufferSystem.Singleton ecbSystem = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>();
        
        NativeArray<Entity> stations = stationQuery.ToEntityArray(systemState.WorldUpdateAllocator);
        NativeArray<Entity> ships = shipQuery.ToEntityArray(systemState.WorldUpdateAllocator);

        systemState.Dependency = new ResetNodeVelocitiesJob().ScheduleParallel(systemState.Dependency);

        systemState.Dependency = new UpdateNodesWithStationsJob { stationEntities = stations, transformData = transformData, stationData = stationData }.ScheduleParallel(systemState.Dependency);

        systemState.Dependency = new UpdateNodesWithShipsJob { shipEntities = ships, shipData = shipData, transformData = transformData, ecb = ecbSystem.CreateCommandBuffer(systemState.WorldUnmanaged).AsParallelWriter() }.ScheduleParallel(systemState.Dependency);

        systemState.Dependency = new UpdateNodeTransformsJob { timeData = SystemAPI.Time }.ScheduleParallel(systemState.Dependency);

        systemState.Dependency = new RemoveDeadNodesJob { ecb = ecbSystem.CreateCommandBuffer(systemState.WorldUnmanaged).AsParallelWriter() }.ScheduleParallel(systemState.Dependency);
    }

}