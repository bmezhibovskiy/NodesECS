using Unity.Burst;
using Unity.Collections;
using Unity.Core;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using static Unity.Properties.PropertyPath;
using UnityEngine.UIElements;

public struct ClosestNodes
{
    public readonly static int numClosestNodes = 3;
    public readonly static ClosestNodes empty = new ClosestNodes
    {
        closestNode1 = Entity.Null,
        closestNode2 = Entity.Null,
        closestNode3 = Entity.Null
    };

    public Entity closestNode1;
    public Entity closestNode2;
    public Entity closestNode3;

    public Entity Get(int index)
    {
        switch (index)
        {
            case 0:
                return closestNode1;
            case 1:
                return closestNode2;
            case 2:
                return closestNode3;
            default:
                return Entity.Null;
        }
    }

    public void Set(Entity e, int index)
    {
        switch (index)
        {
            case 0:
                closestNode3 = closestNode2;
                closestNode2 = closestNode1;
                closestNode1 = e;
                break;
            case 1:
                closestNode3 = closestNode2;
                closestNode2 = e;
                break;
            case 2:
                closestNode3 = e;
                break;
            default:
                break;
        }
    }
}

public struct Ship : IComponentData
{
    public float size;

    public ClosestNodes closestNodes;
    public float3 nodeOffset;
    public float3 prevPos;
    public float3 nextPos;
    public float3 facing;
    public float3 accel;
    public float3 vel;//derived from translation and prevPos

    public Entity dockedAt;
    public bool isUndocking;

    public void Rotate(float speed)
    {
        facing = math.rotate(quaternion.RotateZ(speed), facing);
    }
    public void AddThrust(float strength)
    {
        accel += facing * strength;
    }
    public float3 AverageNodePos(ComponentDataFromEntity<Translation> translationData)
    {
        float3 avgPos = float3.zero;
        int numClosest = 0;
        for(int i = 0; i < ClosestNodes.numClosestNodes; ++i)
        {
            Entity closest = closestNodes.Get(i);
            if (translationData.HasComponent(closest))
            {
                avgPos += translationData[closest].Value;
                ++numClosest;
            }
        }
        if (numClosest > 0)
        {
            return avgPos / (float)numClosest;
        }
        return prevPos;
    }

    public float3 GridPosition(ComponentDataFromEntity<Translation> translationData)
    {
        return AverageNodePos(translationData) + nodeOffset;
    }
    public void HandleCollisionAt(float3 collisionPos, float3 normal, float bounciness = 0.5f)
    {
        nextPos = collisionPos;
        if (math.distancesq(vel,float3.zero) < 0.00002f)
        {
            //Velocity too small, set to 0 instead of bouncing forever, which can cause instability
            prevPos = collisionPos;
            return;
        }

        //Reflect vel about normal
        vel = (vel - 2f * Vector3.Dot(vel, normal) * normal) * bounciness;

        //Would need time independent accel because otherwise we would need next frame's deltaTime to get the correct bounce
        //Verlet integration doesn't seem good for velocity based forces, since velocity is derived.
        //timeIndependentAccel += (-2 * normal * Vector3.Dot(vel, normal)) * bounciness;

        prevPos = collisionPos - vel;
    }
}

public struct Player: IComponentData
{

}

[BurstCompile]
public partial struct FindClosestNodesJob : IJobEntity
{
    [ReadOnly] public ComponentDataFromEntity<Translation> translationData;
    [ReadOnly] public ComponentDataFromEntity<GridNode> nodeData;
    [ReadOnly] public NativeArray<Entity> nodes;

    void Execute(ref Ship s, in Translation t)
    {
        float3 shipPos = t.Value;
        s.closestNodes = ClosestNodes.empty;

        //Instead of just sorting the nodes array,
        //It should be faster to just find the closest K nodes (currently 3)
        //So, this algorithm has K*N iterations, where N is the total number of nodes
        //Since K is very small, this has a O(N).
        //Also, it doesn't require copying an entire array to sort it.

        for (int i = 0; i < nodes.Length; ++i)
        {
            GridNode nodeComponent = nodeData[nodes[i]];
            if (nodeComponent.isDead) { continue; }

            float3 nodePos = translationData[nodes[i]].Value;
            float newSqMag = math.distancesq(nodePos, shipPos);

            for (int j = 0; j < ClosestNodes.numClosestNodes; ++j)
            {
                Entity currentClosest = s.closestNodes.Get(j);
                if (!translationData.HasComponent(currentClosest))
                {
                    s.closestNodes.Set(nodes[i], j);
                    break;
                }

                float3 currentPos = translationData[currentClosest].Value;
                float currentSqMag = math.distancesq(currentPos, shipPos);
                if (newSqMag < currentSqMag)
                {
                    s.closestNodes.Set(nodes[i], j);
                    break;
                }
            }
        }
        s.nodeOffset = shipPos - s.AverageNodePos(translationData);


        for (int i = 0; i < ClosestNodes.numClosestNodes; ++i)
        {
            if(translationData.HasComponent(s.closestNodes.Get(i)))
            {
                Debug.DrawLine(shipPos, translationData[s.closestNodes.Get(i)].Value);
            }
        }
    }
}

[BurstCompile]
public partial struct UpdatePlayerShipJob: IJobEntity
{
    [ReadOnly] public TimeData timeData; 
    void Execute(ref Ship ship, Player p)
    {
        float dt = timeData.DeltaTime;
        float rspeed = 2f * dt;
        float fspeed = 2f;
        if (ship.dockedAt == Entity.Null)
        {
            if (Globals.sharedInputState.Data.RotateLeftKeyDown)
            {
                ship.Rotate(rspeed);
            }
            if (Globals.sharedInputState.Data.RotateRightKeyDown)
            {
                ship.Rotate(-rspeed);
            }
            if (Globals.sharedInputState.Data.ForwardThrustKeyDown)
            {
                ship.AddThrust(fspeed);
            }
            if (Globals.sharedInputState.Data.ReverseThrustKeyDown)
            {
                ship.AddThrust(-fspeed);
            }
        }
        if (Globals.sharedInputState.Data.AfterburnerKeyDown)
        {
            if (ship.dockedAt == Entity.Null)
            {
                ship.AddThrust(fspeed * 2.0f);
            }
            else
            {
                ship.isUndocking = true;
            }
        }
    }
}

[BurstCompile]
public partial struct UpdateShipsWithStationsJob: IJobEntity
{
    [ReadOnly] public NativeArray<Entity> stationEntities;
    [ReadOnly] public ComponentDataFromEntity<Translation> translationData;
    [ReadOnly] public ComponentDataFromEntity<Station> stationData;
    [ReadOnly] public TimeData timeData;

    void Execute(ref Ship ship)
    {
        float3 shipPos = ship.nextPos;
        float dt = timeData.DeltaTime;
        float dt2 = dt * dt;
        for(int i = 0; i < stationEntities.Length; ++i)
        {
            Entity stationEntity = stationEntities[i];
            Station station = stationData[stationEntity];
            float3 stationPos = translationData[stationEntity].Value;
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
                                ship.HandleCollisionAt(intersection.Value, math.normalize(intersection.Value - stationPos), bounciness);
                            }
                        }
                        break;
                    case StationModuleType.ShipRepellent:
                        float order = sm.GetParam(0);
                        float pullStrength = sm.GetParam(1);
                        float perpendicularStrength = sm.GetParam(2);

                        float3 dir2 = math.cross(dir, new float3(0,0,1));
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

                        if (ship.dockedAt == Entity.Null && math.distancesq(ship.vel, float3.zero) / dt2 < sqrMaxDockingSpeed)
                        {
                            if (distSq < totalUndockSize * totalUndockSize)
                            {   
                                ship.isUndocking = false;
                                ship.dockedAt = stationEntity;
                                ship.prevPos = shipPos;
                                ship.nextPos = shipPos;
                                ship.accel = float3.zero;
                                ship.vel = float3.zero;
                                ship.facing = normalizedDir;
                            }
                        }
                        else if (ship.isUndocking && ship.dockedAt == stationEntity)
                        {
                            ship.facing = normalizedDir;
                            if (distSq < totalUndockSize * totalUndockSize)
                            {
                                ship.AddThrust(undockThrust);
                            }
                            else
                            {
                                ship.dockedAt = Entity.Null;
                                ship.isUndocking = false;
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
    [ReadOnly] public ComponentDataFromEntity<Translation> translationData;
    void Execute(ref Ship ship)
    {
        if(ship.dockedAt != Entity.Null && !ship.isUndocking) { return; }

        float dt = timeData.DeltaTime;
        float3 shipPos = ship.nextPos;
        float3 current = shipPos + (ship.GridPosition(translationData) - shipPos) * dt;
        ship.nextPos = 2 * current - ship.prevPos + ship.accel * (dt * dt);
        ship.accel = float3.zero;
        ship.prevPos = current;
        ship.vel = ship.nextPos - current;
    }
}

[BurstCompile]
public partial struct UpdateShipPositionsJob : IJobEntity
{
    void Execute(ref Translation translation, in Ship ship)
    {
        translation.Value = ship.nextPos;
    }
}

[BurstCompile]
public partial struct RenderShipsJob : IJobEntity
{
    void Execute(in Ship ship, in Translation translation)
    {
        float3 shipPos = translation.Value;
        Debug.DrawRay(shipPos, ship.facing, Color.green);
        Debug.DrawRay(shipPos, ship.vel * 60f, Color.red);
        Color c = ship.dockedAt == Entity.Null ? Color.green : Color.blue;
        Utils.DebugDrawCircle(shipPos, ship.size, c, 10);
    }
}
public partial class ShipSystem : SystemBase
{

    [BurstCompile]
    protected override void OnUpdate()
    {
        ComponentDataFromEntity<Translation> translationData = GetComponentDataFromEntity<Translation>();

        Dependency = new UpdatePlayerShipJob { timeData = Time }.ScheduleParallel(Dependency);

        ComponentDataFromEntity<Station> stationData = GetComponentDataFromEntity<Station>();

        //Needs dispose (stationEntities)
        NativeArray<Entity> stationEntities = GetEntityQuery(typeof(Station), typeof(Translation)).ToEntityArray(Allocator.TempJob);

        Dependency = new UpdateShipsWithStationsJob { stationEntities = stationEntities, stationData = stationData, translationData = translationData, timeData = Time }.ScheduleParallel(Dependency);

        //Gets disposed (stationEntities)
        Dependency = stationEntities.Dispose(Dependency);

        Dependency = new IntegrateShipsJob { timeData = Time, translationData = translationData }.ScheduleParallel(Dependency);

        Dependency = new UpdateShipPositionsJob().ScheduleParallel(Dependency);

        //Needs dispose (nodes)
        NativeArray<Entity> nodes = GetEntityQuery(typeof(GridNode), typeof(Translation)).ToEntityArray(Allocator.TempJob);
        ComponentDataFromEntity<GridNode> nodeData = GetComponentDataFromEntity<GridNode>();

        Dependency = new FindClosestNodesJob { translationData = translationData, nodes = nodes, nodeData = nodeData }.ScheduleParallel(Dependency);

        //Gets disposed (nodes)
        Dependency = nodes.Dispose(Dependency);

        Dependency = new RenderShipsJob().ScheduleParallel(Dependency);
    }
}

