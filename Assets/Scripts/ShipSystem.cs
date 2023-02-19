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
    [ReadOnly] public const int numClosestNodes = 3;
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
}

[BurstCompile]
public partial struct FindClosestNodesJob : IJobEntity
{
    [ReadOnly] public ComponentDataFromEntity<Translation> translationData;
    [ReadOnly] public NativeArray<Entity> nodes;

    void Execute(ref Ship s, in Translation t)
    {
        float3 shipPos = t.Value;
        s.closestNodes = new ClosestNodes { closestNode1 = Entity.Null, closestNode2 = Entity.Null, closestNode3 = Entity.Null };

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

        //Once we've updated the closest nodes, we can draw lines for debug visualization
        for (int i = 0; i < ClosestNodes.numClosestNodes; ++i)
        {
            Entity closest = s.closestNodes.Get(i);
            if (translationData.HasComponent(closest))
            {
                float3 nodePos = translationData[closest].Value;
                Debug.DrawLine(shipPos, nodePos);
            }
        }
    }
}

[BurstCompile]
public partial struct FindClosestNodesECSJob: IJobEntity
{
    [ReadOnly] public ComponentDataFromEntity<Translation> translationData;
    [ReadOnly] public SpatialHasher spatialHasher;

    void Execute(ref Ship s, in Translation t)
    {
        float3 shipPos = t.Value;
        s.closestNodes = new ClosestNodes { closestNode1 = Entity.Null, closestNode2 = Entity.Null, closestNode3 = Entity.Null };


        NativeArray<Entity> closestArray = spatialHasher.ClosestNodes(shipPos, 3, translationData);
        for (int i = 0; i < closestArray.Length && i < ClosestNodes.numClosestNodes; ++i)
        {
            s.closestNodes.Set(closestArray[i], i);
        }

        //Once we've updated the closest nodes, we can draw lines for debug visualization
        for (int i = 0; i < ClosestNodes.numClosestNodes; ++i)
        {
            Entity closest = s.closestNodes.Get(i);
            if (translationData.HasComponent(closest))
            {
                float3 nodePos = translationData[closest].Value;
                Debug.DrawLine(shipPos, nodePos);
            }
        }
    }
}

public partial class ShipSystem : SystemBase
{
    private JobHandle updateShips;
    private JobHandle disposeNodesArray;

    [BurstCompile]
    protected override void OnUpdate()
    {
        ComponentDataFromEntity<Translation> translationData = GetComponentDataFromEntity<Translation>();

        if (Globals.sharedInputState.Data.isIKeyDown)
        {
            updateShips = new FindClosestNodesECSJob { translationData = translationData, spatialHasher = SpatialHasher.sharedInstance }.ScheduleParallel(Dependency);
            Dependency = updateShips;
        }
        else
        {
            NativeArray<Entity> nodes = GetEntityQuery(typeof(GridNode), typeof(Translation)).ToEntityArray(Allocator.TempJob);

            updateShips = new FindClosestNodesJob { translationData = translationData, nodes = nodes }.ScheduleParallel(Dependency);
            Dependency = updateShips;

            disposeNodesArray = nodes.Dispose(updateShips);
            Dependency = disposeNodesArray;
        }
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
