using Unity.Burst;
using Unity.Collections;
using Unity.Core;
using Unity.Entities;
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
        if (gridNode.isBorder) { return; }

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
                ecb.AddComponent(entityInQueryIndex, e, NeedsDestroy.Now);

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
    public EntityCommandBuffer.ParallelWriter ecb;
    void Execute(ref GridNode gridNode, in Entity e, [EntityIndexInQuery] int entityInQueryIndex)
    {
        if (gridNode.isBorder) { return; }

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
                            ecb.AddComponent(entityInQueryIndex, e, NeedsDestroy.Now);
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
        if(gridNode.isBorder) { return; }

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
public partial struct FindClosestNodesJob : IJobEntity
{
    [ReadOnly] public ComponentLookup<LocalToWorld> transformData;
    [ReadOnly] public ComponentLookup<GridNode> nodeData;
    [ReadOnly] public NativeArray<Entity> nodes;

    void Execute(ref AffectedByNodes a, in NextTransform nt)
    {
        float3 aPos = nt.nextPos;
        a.closestNodes = ClosestNodes.empty;

        //Instead of just sorting the nodes array,
        //It should be faster to just find the closest K nodes (currently 3)
        //So, this algorithm has K*N iterations, where N is the total number of nodes
        //Since K is very small, this has a O(N).
        //Also, it doesn't require copying an entire array to sort it.

        for (int i = 0; i < nodes.Length; ++i)
        {
            GridNode nodeComponent = nodeData[nodes[i]];
            if (nodeComponent.isBorder) { continue; }

            float3 nodePos = transformData[nodes[i]].Position;
            float newSqMag = math.distancesq(nodePos, aPos);

            for (int j = 0; j < ClosestNodes.numClosestNodes; ++j)
            {
                Entity currentClosest = a.closestNodes.Get(j);
                if (!transformData.HasComponent(currentClosest))
                {
                    a.closestNodes.Set(nodes[i], j);
                    break;
                }

                float3 currentPos = transformData[currentClosest].Position;
                float currentSqMag = math.distancesq(currentPos, aPos);
                if (newSqMag < currentSqMag)
                {
                    a.closestNodes.Set(nodes[i], j);
                    break;
                }
            }
        }
        a.nodeOffset = aPos - a.AverageNodePos(transformData);
    }
}

[BurstCompile]
public partial struct NodeSystem : ISystem
{
    [ReadOnly] private ComponentLookup<LocalToWorld> transformData;
    [ReadOnly] private ComponentLookup<Station> stationData;
    [ReadOnly] private ComponentLookup<Ship> shipData;
    [ReadOnly] private ComponentLookup<GridNode> nodeData;

    private EntityQuery stationQuery;
    private EntityQuery shipQuery;
    private EntityQuery nodesQuery;

    [BurstCompile]
    public void OnCreate(ref SystemState systemState)
    {
        transformData = systemState.GetComponentLookup<LocalToWorld>();
        stationData = systemState.GetComponentLookup<Station>();
        shipData = systemState.GetComponentLookup<Ship>();
        nodeData = systemState.GetComponentLookup<GridNode>();

        stationQuery = new EntityQueryBuilder(Allocator.Temp).WithAll<Station, LocalToWorld>().Build(ref systemState);
        shipQuery = new EntityQueryBuilder(Allocator.Temp).WithAll<Ship, LocalToWorld>().Build(ref systemState);
        nodesQuery = new EntityQueryBuilder(Allocator.Temp).WithAll<GridNode, LocalToWorld>().Build(ref systemState);
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
        nodeData.Update(ref systemState);

        EndSimulationEntityCommandBufferSystem.Singleton ecbSystem = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>();
        
        NativeArray<Entity> stations = stationQuery.ToEntityArray(systemState.WorldUpdateAllocator);
        NativeArray<Entity> ships = shipQuery.ToEntityArray(systemState.WorldUpdateAllocator);
        NativeArray<Entity> nodes = nodesQuery.ToEntityArray(systemState.WorldUpdateAllocator);

        systemState.Dependency = new ResetNodeVelocitiesJob().ScheduleParallel(systemState.Dependency);

        systemState.Dependency = new FindClosestNodesJob { transformData = transformData, nodes = nodes, nodeData = nodeData }.ScheduleParallel(systemState.Dependency);

        systemState.Dependency = new UpdateNodesWithStationsJob { stationEntities = stations, transformData = transformData, stationData = stationData, ecb = ecbSystem.CreateCommandBuffer(systemState.WorldUnmanaged).AsParallelWriter() }.ScheduleParallel(systemState.Dependency);

        systemState.Dependency = new UpdateNodesWithShipsJob { shipEntities = ships, shipData = shipData, transformData = transformData, ecb = ecbSystem.CreateCommandBuffer(systemState.WorldUnmanaged).AsParallelWriter() }.ScheduleParallel(systemState.Dependency);

        systemState.Dependency = new UpdateNodeTransformsJob { timeData = SystemAPI.Time }.ScheduleParallel(systemState.Dependency);
        
    }

}