using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

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
    public ClosestNodes closestNodes;
    public float3 nodeOffset;
    public float3 prevPos;
    public float3 nextPos;
    public float3 facing;
    public float3 accel;
    public float3 vel;//derived from translation and prevPos

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
}

public struct Player: IComponentData
{

}

[BurstCompile]
public partial struct FindClosestNodesJob : IJobEntity
{
    [ReadOnly] public ComponentDataFromEntity<Translation> translationData;
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
    }
}

public partial struct UpdatePlayerShipJob: IJobEntity
{
    void Execute(ref Ship ship, Player p)
    {
        float dt = Globals.sharedTimeState.Data.deltaTime;
        float rspeed = 2f * dt;
        float fspeed = 2f;
        if (Globals.sharedInputState.Data.isLeftKeyDown)
        {
            ship.Rotate(rspeed);
        }
        if (Globals.sharedInputState.Data.isRightKeyDown)
        {
            ship.Rotate(-rspeed);
        }
        if (Globals.sharedInputState.Data.isUpKeyDown)
        {
            ship.AddThrust(fspeed);
        }
        if (Globals.sharedInputState.Data.isDownKeyDown)
        {
            ship.AddThrust(-fspeed);
        }
        if (Globals.sharedInputState.Data.isSpaceDown)
        {
            ship.AddThrust(fspeed * 2.0f);
        }
    }
}

public partial struct IntegrateShipsJob: IJobEntity
{
    [ReadOnly] public ComponentDataFromEntity<Translation> translationData;
    void Execute(ref Ship ship, in Entity e)
    {
        float dt = Globals.sharedTimeState.Data.deltaTime;
        float3 shipPos = translationData[e].Value;
        float3 current = shipPos + (ship.GridPosition(translationData) - shipPos) * dt;
        ship.nextPos = 2 * current - ship.prevPos + ship.accel * (dt * dt);
        ship.accel = float3.zero;
        ship.prevPos = current;
        ship.vel = ship.nextPos - current;
    }
}
public partial struct UpdateShipPositionsJob : IJobEntity
{
    void Execute(ref Translation translation, in Ship ship)
    {
        translation.Value = ship.nextPos;
    }
}

public partial struct RenderShipsJob : IJobEntity
{
    void Execute(in Ship ship, in Translation translation)
    {
        Debug.DrawRay(translation.Value, ship.facing);
    }
}
public partial class ShipSystem : SystemBase
{
    private JobHandle updateClosestNodes;
    private JobHandle disposeNodesArray;
    private JobHandle updatePlayerShip;
    private JobHandle integrateShips;
    private JobHandle updateShipPositions;
    private JobHandle renderShips;

    [BurstCompile]
    protected override void OnUpdate()
    {
        ComponentDataFromEntity<Translation> translationData = GetComponentDataFromEntity<Translation>();
        
        updatePlayerShip = new UpdatePlayerShipJob().ScheduleParallel(Dependency);
        Dependency = updatePlayerShip;

        integrateShips = new IntegrateShipsJob { translationData = translationData }.ScheduleParallel(Dependency);
        Dependency = integrateShips;

        updateShipPositions = new UpdateShipPositionsJob().ScheduleParallel(Dependency);
        Dependency = updateShipPositions;

        NativeArray<Entity> nodes = GetEntityQuery(typeof(GridNode), typeof(Translation)).ToEntityArray(Allocator.TempJob);

        updateClosestNodes = new FindClosestNodesJob { translationData = translationData, nodes = nodes }.ScheduleParallel(Dependency);
        Dependency = updateClosestNodes;

        disposeNodesArray = nodes.Dispose(Dependency);
        Dependency = disposeNodesArray;

        renderShips = new RenderShipsJob().ScheduleParallel(Dependency);
        Dependency = renderShips;
    }
}
public struct EntityComparer : IComparer<Entity>
{
    [ReadOnly] public float3 pos;
    [ReadOnly] public ComponentDataFromEntity<Translation> translationData;

    public int Compare(Entity x, Entity y)
    {
        if (x == y) { return 0; }
        if (x == Entity.Null) { return 1; }
        if (y == Entity.Null) { return -1; }

        float3 fX = translationData[x].Value;
        float3 fY = translationData[y].Value;
        float sqDistX = math.distancesq(fX, pos);
        float sqDistY = math.distancesq(fY, pos);
        return sqDistX.CompareTo(sqDistY);
    }
}
