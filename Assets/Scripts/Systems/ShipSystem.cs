using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Core;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

[BurstCompile]
public partial struct FindClosestNodesJob : IJobEntity
{
    [ReadOnly] public ComponentLookup<LocalToWorld> transformData;
    [ReadOnly] public ComponentLookup<GridNode> nodeData;
    [ReadOnly] public NativeArray<Entity> nodes;

    void Execute(ref Ship s, in NextTransform nt)
    {
        float3 shipPos = nt.nextPos;
        s.closestNodes = ClosestNodes.empty;

        //Instead of just sorting the nodes array,
        //It should be faster to just find the closest K nodes (currently 3)
        //So, this algorithm has K*N iterations, where N is the total number of nodes
        //Since K is very small, this has a O(N).
        //Also, it doesn't require copying an entire array to sort it.

        for (int i = 0; i < nodes.Length; ++i)
        {
            GridNode nodeComponent = nodeData[nodes[i]];
            if (nodeComponent.isDead || nodeComponent.isBorder) { continue; }

            float3 nodePos = transformData[nodes[i]].Position;
            float newSqMag = math.distancesq(nodePos, shipPos);

            for (int j = 0; j < ClosestNodes.numClosestNodes; ++j)
            {
                Entity currentClosest = s.closestNodes.Get(j);
                if (!transformData.HasComponent(currentClosest))
                {
                    s.closestNodes.Set(nodes[i], j);
                    break;
                }

                float3 currentPos = transformData[currentClosest].Position;
                float currentSqMag = math.distancesq(currentPos, shipPos);
                if (newSqMag < currentSqMag)
                {
                    s.closestNodes.Set(nodes[i], j);
                    break;
                }
            }
        }
        s.nodeOffset = shipPos - s.AverageNodePos(transformData);
    }
}

[BurstCompile]
public partial struct UpdatePlayerShipJob: IJobEntity
{
    [ReadOnly] public TimeData timeData; 
    void Execute(ref Ship ship, ref NextTransform nt, ref Docked docked, in Player p)
    {
        float dt = timeData.DeltaTime;
        float rspeed = ship.rotationSpeed * dt;
        float fspeed = ship.thrust;
        if (docked.dockedAt == Entity.Null && !ship.PreparingHyperspace())
        {
            if (Globals.sharedInputState.Data.RotateLeftKeyDown)
            {
                nt.Rotate(rspeed);
            }
            if (Globals.sharedInputState.Data.RotateRightKeyDown)
            {
                nt.Rotate(-rspeed);
            }
            if (Globals.sharedInputState.Data.ForwardThrustKeyDown)
            {
                ship.AddThrust(fspeed * nt.facing);
            }
            if (Globals.sharedInputState.Data.ReverseThrustKeyDown)
            {
                ship.AddThrust(-fspeed * nt.facing);
            }
        }
        if (Globals.sharedInputState.Data.AfterburnerKeyDown)
        {
            if (docked.dockedAt == Entity.Null)
            {
                ship.AddThrust(fspeed * 2.0f * nt.facing);
            }
            else
            {
                docked.isUndocking = true;
            }
        }
        if (Globals.sharedInputState.Data.HyperspaceKeyDown)
        {
            if(!ship.PreparingHyperspace())
            {
                int targetIndex = 0;
                if(Globals.sharedLevelInfo.Data.sectorIndex == 0)
                {
                    targetIndex = 3;
                }
                ship.StartHyperspace(targetIndex, 50);
            }
        }
        if(Globals.sharedInputState.Data.LightsKeyPressed)
        {
            ship.lightsOn = !ship.lightsOn;
        }
    }
}

[BurstCompile]
public partial struct UpdateShipsWithStationsJob: IJobEntity
{
    [ReadOnly] public NativeArray<Entity> stationEntities;
    [ReadOnly] public ComponentLookup<LocalToWorld> transformData;
    [ReadOnly] public ComponentLookup<Station> stationData;
    [NativeDisableContainerSafetyRestriction] [ReadOnly] public ComponentLookup<NextTransform> nextTransformData;
    [ReadOnly] public TimeData timeData;

    void Execute(ref Ship ship, ref NextTransform nt, ref Docked docked)
    {
        float3 shipPos = nt.nextPos;
        float dt = timeData.DeltaTime;
        float dt2 = dt * dt;
        for(int i = 0; i < stationEntities.Length; ++i)
        {
            Entity stationEntity = stationEntities[i];
            Station station = stationData[stationEntity];
            float3 stationPos = transformData[stationEntity].Position;
            float distSq = math.distancesq(shipPos, stationPos);
            float3 dir = shipPos - stationPos;
            float3 normalizedDir = math.normalize(dir);

            for (int j = 0; j < station.modules.Count; ++j)
            {
                StationModule sm = station.modules.Get(j);
                switch (sm.type)
                {
                    case StationModuleType.ShipSphereCollider:
                        float size = sm.GetParam(0);
                        float totalCollisionSize = size + ship.size;
                        if (distSq < totalCollisionSize * totalCollisionSize)
                        {
                            float3? intersection = Utils.LineSegmentCircleIntersection(stationPos, totalCollisionSize, ship.prevPos, shipPos);
                            if (intersection != null)
                            {
                                float bounciness = sm.GetParam(1);
                                nt.nextPos = intersection.Value;
                                ship.HandleCollisionAt(intersection.Value, math.normalize(intersection.Value - stationPos), bounciness);
                            }
                        }
                        break;
                    case StationModuleType.ShipRepellent:
                        float order = sm.GetParam(0);
                        float pullStrength = sm.GetParam(1);
                        float perpendicularStrength = sm.GetParam(2);

                        float3 dir2 = math.cross(dir, new float3(0, 0, 1));
                        //Inverse r squared law generalizes to inverse r^(dim-1)
                        //However, we need to multiply denom by dir.magnitude to normalize dir
                        //So that cancels with the fObj.dimension - 1, removing the - 1
                        //However #2, dir.sqrMagnitude is cheaper, but will require bringing back the - 1
                        float denom = math.pow(distSq, (order - 1f));
                        ship.accel += (pullStrength / denom) * dir + (perpendicularStrength / denom) * dir2;
                        break;
                    case StationModuleType.Dock:
                        float maxDockingSpeed = sm.GetParam(0);
                        float sqrMaxDockingSpeed = maxDockingSpeed * maxDockingSpeed;
                        float dockDistance = sm.GetParam(1);
                        float undockDistance = dockDistance * 1.1f;
                        float undockThrust = sm.GetParam(2);
                        float totalUndockSize = station.size + ship.size + undockDistance;

                        if (docked.dockedAt == Entity.Null && math.distancesq(ship.vel, float3.zero) / dt2 < sqrMaxDockingSpeed)
                        {
                            if (distSq < totalUndockSize * totalUndockSize)
                            {
                                docked.isUndocking = false;
                                docked.dockedAt = stationEntity;
                                docked.initialPos = shipPos;
                                docked.initialFacing = nextTransformData[stationEntity].facing;
                                ship.prevPos = shipPos;
                                ship.accel = float3.zero;
                                ship.vel = float3.zero;
                                nt.nextPos = shipPos;
                                nt.facing = normalizedDir;
                            }
                        }
                        else if (docked.dockedAt == stationEntity)
                        {
                            if (docked.isUndocking)
                            {
                                nt.facing = normalizedDir;
                                if (distSq < totalUndockSize * totalUndockSize)
                                {
                                    ship.AddThrust(nt.facing * undockThrust);
                                }
                                else
                                {
                                    docked.dockedAt = Entity.Null;
                                    docked.isUndocking = false;
                                }
                            }
                            else
                            {

                                ship.prevPos = nt.nextPos;
                            }
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
public partial struct IntegrateShipsJob: IJobEntity
{
    [ReadOnly] public TimeData timeData;
    [ReadOnly] public ComponentLookup<LocalToWorld> transformData;
    void Execute(ref Ship ship, ref NextTransform nt, in Docked docked)
    {
        if(docked.dockedAt != Entity.Null && !docked.isUndocking) { return; }
        if(ship.PreparingHyperspace()) { return; }

        float dt = timeData.DeltaTime;
        float3 shipPos = nt.nextPos;
        float3 current = shipPos + (ship.GridPosition(transformData) - shipPos) * dt;
        nt.nextPos = 2 * current - ship.prevPos + ship.accel * (dt * dt);
        ship.accel = float3.zero;
        ship.prevPos = current;
        ship.vel = nt.nextPos - current;
    }
}

[BurstCompile]
public partial struct ShipSystem : ISystem
{
    [ReadOnly] private ComponentLookup<LocalToWorld> transformData;
    [ReadOnly] private ComponentLookup<NextTransform> nextTransformData;
    [ReadOnly] private ComponentLookup<Station> stationData;
    [ReadOnly] private ComponentLookup<GridNode> nodeData;

    private EntityQuery stationsQuery;
    private EntityQuery nodesQuery;

    [BurstCompile]
    public void OnCreate(ref SystemState systemState)
    {
        transformData = SystemAPI.GetComponentLookup<LocalToWorld>();
        nextTransformData = SystemAPI.GetComponentLookup<NextTransform>();
        stationData = SystemAPI.GetComponentLookup<Station>();
        nodeData = SystemAPI.GetComponentLookup<GridNode>();

        stationsQuery = new EntityQueryBuilder(Allocator.Temp).WithAll<Station, LocalToWorld>().Build(ref systemState);
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
        nextTransformData.Update(ref systemState);
        stationData.Update(ref systemState);
        nodeData.Update(ref systemState);

        NativeArray<Entity> stationEntities = stationsQuery.ToEntityArray(systemState.WorldUpdateAllocator);
        NativeArray<Entity> nodes = nodesQuery.ToEntityArray(systemState.WorldUpdateAllocator);

        systemState.Dependency = new UpdatePlayerShipJob { timeData = SystemAPI.Time }.ScheduleParallel(systemState.Dependency);

        systemState.Dependency = new UpdateShipsWithStationsJob { stationEntities = stationEntities, stationData = stationData, transformData = transformData, timeData = SystemAPI.Time, nextTransformData = nextTransformData }.ScheduleParallel(systemState.Dependency);
        
        systemState.Dependency = new IntegrateShipsJob { timeData = SystemAPI.Time, transformData = transformData }.ScheduleParallel(systemState.Dependency);

        systemState.Dependency = new FindClosestNodesJob { transformData = transformData, nodes = nodes, nodeData = nodeData }.ScheduleParallel(systemState.Dependency);
    }
}

