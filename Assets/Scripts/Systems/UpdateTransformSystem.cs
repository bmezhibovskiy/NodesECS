using Unity.Burst;
using Unity.Collections;
using Unity.Core;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

[BurstCompile]
public partial struct UpdateConstantThrustJob: IJobEntity
{
    void Execute(ref Accelerating a, in ConstantThrust c)
    {
        a.accel = c.thrust;
    }
}

[BurstCompile]
public partial struct FindClosestNodesToAcceleratingJob : IJobEntity
{
    [ReadOnly] public ComponentLookup<LocalToWorld> transformData;
    [ReadOnly] public ComponentLookup<GridNode> nodeData;
    [ReadOnly] public NativeArray<Entity> nodes;

    void Execute(ref Accelerating a, in NextTransform nt)
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
            if (nodeComponent.isDead || nodeComponent.isBorder) { continue; }

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
public partial struct IntegrateAcceleratingJob : IJobEntity
{
    [ReadOnly] public TimeData timeData;
    [ReadOnly] public ComponentLookup<LocalToWorld> transformData;
    void Execute(ref Accelerating a, ref NextTransform nt)
    {
        float dt = timeData.DeltaTime;
        float3 shipPos = nt.nextPos;
        float3 current = shipPos + (a.GridPosition(transformData) - shipPos) * dt;
        nt.nextPos = 2 * current - a.prevPos + a.accel * (dt * dt);
        a.accel = float3.zero;
        a.prevPos = current;
        a.vel = nt.nextPos - current;
    }
}

[BurstCompile]
public partial struct UpdateTransformsJob : IJobEntity
{
    void Execute(ref LocalToWorld t, in NextTransform nt)
    {
        float4x4 translation = float4x4.Translate(nt.nextPos);
        float angle = math.radians(Vector3.SignedAngle(Vector3.right, nt.facing, Vector3.forward));
        float4x4 rotation = float4x4.RotateZ(angle);
        float4x4 scale = float4x4.Scale(nt.scale);
        t.Value = math.mul(translation, math.mul(rotation, scale));
    }
}

[BurstCompile]
public partial struct PopulateParentTransformJob : IJobEntity
{
    [ReadOnly] public ComponentLookup<LocalToWorld> transformData;
    void Execute(ref RelativeTransform rt, in Parent p)
    {
        rt.lastParentValue = transformData[p.Value].Value;
    }
}

[BurstCompile]
public partial struct UpdateChildTransformJob : IJobEntity
{
    void Execute(ref LocalToWorld transform, in RelativeTransform rt)
    {
        transform.Value = math.mul(rt.lastParentValue, rt.Value);
    }
}

[BurstCompile]
public partial struct UpdateTransformSystem : ISystem
{
    [ReadOnly] private ComponentLookup<LocalToWorld> transformData;
    [ReadOnly] private ComponentLookup<GridNode> nodeData;

    private EntityQuery nodesQuery;

    [BurstCompile]
    public void OnCreate(ref SystemState systemState)
    {
        transformData = SystemAPI.GetComponentLookup<LocalToWorld>();
        nodeData = SystemAPI.GetComponentLookup<GridNode>();
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
        nodeData.Update(ref systemState);

        systemState.Dependency = new PopulateParentTransformJob { transformData = transformData }.ScheduleParallel(systemState.Dependency);

        systemState.Dependency = new UpdateChildTransformJob().ScheduleParallel(systemState.Dependency);

        systemState.Dependency = new UpdateTransformsJob().ScheduleParallel(systemState.Dependency);

        systemState.Dependency = new UpdateConstantThrustJob().ScheduleParallel(systemState.Dependency);

        systemState.Dependency = new IntegrateAcceleratingJob { timeData = SystemAPI.Time, transformData = transformData }.ScheduleParallel(systemState.Dependency);

        NativeArray<Entity> nodes = nodesQuery.ToEntityArray(systemState.WorldUpdateAllocator);

        systemState.Dependency = new FindClosestNodesToAcceleratingJob { transformData = transformData, nodes = nodes, nodeData = nodeData }.ScheduleParallel(systemState.Dependency);
    }
}
