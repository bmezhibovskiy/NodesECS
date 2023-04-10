using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Core;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

[BurstCompile]
public partial struct UpdatePlayerShipJob: IJobEntity
{
    [ReadOnly] public TimeData timeData; 
    void Execute(ref Ship ship, ref NextTransform nt, ref Docked docked, ref Accelerating a, ref ThrustHaver th, in Player p)
    {
        float dt = timeData.DeltaTime;
        float rspeed = ship.rotationSpeed * dt;
        float fspeed = ship.maxThrust * (Globals.sharedInputState.Data.AfterburnerKeyDown ? 2 : 1);
        float jerk = ship.jerk * dt;
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

            if(Globals.sharedInputState.Data.ForwardThrustKeyDown)
            {
                if (a.accelStartedAt == 0)
                {
                    a.accelStartedAt = timeData.ElapsedTime;
                }

                float accelMagnitude = math.min(fspeed, jerk * (float)(timeData.ElapsedTime - a.accelStartedAt));
                a.accel += accelMagnitude * nt.facing;
            }
            else
            {
                a.accelStartedAt = 0;
            }

            ship.shootingPrimary = Globals.sharedInputState.Data.PrimaryWeaponKeyDown;
            ship.shootingSecondary = Globals.sharedInputState.Data.SecondaryWeaponKeyDown;
        }
        if (Globals.sharedInputState.Data.AfterburnerKeyDown && docked.dockedAt != Entity.Null)
        {
            docked.isUndocking = true;
        }
        if (Globals.sharedInputState.Data.HyperspaceKeyDown)
        {
            if (!ship.PreparingHyperspace())
            {
                int targetIndex = 0;
                if (Globals.sharedLevelInfo.Data.sectorIndex == 0)
                {
                    targetIndex = 3;
                }
                ship.StartHyperspace(targetIndex, 50);
            }
        }
        if (Globals.sharedInputState.Data.LightsKeyPressed)
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

    void Execute(ref Ship ship, ref NextTransform nt, ref Docked docked, ref Accelerating a)
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
                            float3? intersection = Utils.LineSegmentCircleIntersection(stationPos, totalCollisionSize, a.prevPos, shipPos);
                            if (intersection != null)
                            {
                                float bounciness = sm.GetParam(1);
                                nt.nextPos = intersection.Value;
                                a.HandleCollisionAt(intersection.Value, math.normalize(intersection.Value - stationPos), bounciness);
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
                        a.accel += (pullStrength / denom) * dir + (perpendicularStrength / denom) * dir2;
                        break;
                    case StationModuleType.Dock:
                        float maxDockingSpeed = sm.GetParam(0);
                        float sqrMaxDockingSpeed = maxDockingSpeed * maxDockingSpeed;
                        float dockDistance = sm.GetParam(1);
                        float undockDistance = dockDistance * 1.1f;
                        float undockThrust = sm.GetParam(2);
                        float totalUndockSize = station.size + ship.size + undockDistance;

                        if (docked.dockedAt == Entity.Null && math.distancesq(a.vel, float3.zero) / dt2 < sqrMaxDockingSpeed)
                        {
                            if (distSq < totalUndockSize * totalUndockSize)
                            {
                                docked.isUndocking = false;
                                docked.dockedAt = stationEntity;
                                docked.initialPos = shipPos;
                                docked.initialFacing = nextTransformData[stationEntity].facing;
                                a.prevPos = shipPos;
                                a.accel = float3.zero;
                                a.vel = float3.zero;
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
                                    a.accel += (nt.facing * undockThrust);
                                }
                                else
                                {
                                    docked.dockedAt = Entity.Null;
                                    docked.isUndocking = false;
                                }
                            }
                            else
                            {

                                a.prevPos = nt.nextPos;
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
public partial struct ShootWeaponsJob: IJobEntity
{
    [ReadOnly] public TimeData timeData;
    public EntityCommandBuffer.ParallelWriter ecb;
    void Execute(ref Ship ship, in NextTransform nt, in Entity e, [EntityIndexInQuery] int entityInQueryIndex)
    {
        double currentTime = timeData.ElapsedTime;
        for(int i = 0; i < WeaponSlots.MaxWeaponSlots; ++i)
        {
            WeaponSlot current = ship.weaponSlots.Get(i);
            if(current.Equals(WeaponSlot.Empty)) { continue; }
            if(currentTime - current.lastFireSeconds < current.secondsBetweenFire) { continue; }
            if(current.isSecondary && !ship.shootingSecondary || !current.isSecondary && !ship.shootingPrimary) { continue; }
            ship.weaponSlots.Fire(i, (float)currentTime);
            float3 newPos = nt.nextPos + current.relativePos;
            switch (current.type)
            {
                case WeaponType.StraightRocket:
                    Globals.sharedEntityFactory.Data.CreateRocket1Async(entityInQueryIndex, e, ecb, newPos, nt.facing, timeData.ElapsedTime);
                    break;
                default:
                    break;
            }
        }
    }
}

[BurstCompile]
public partial struct CollideWithWeaponShotsJob : IJobEntity
{
    [ReadOnly] public TimeData timeData;
    [ReadOnly] public NativeArray<Entity> shipEntities;
    [ReadOnly] public ComponentLookup<LocalToWorld> transformData;
    [ReadOnly] public ComponentLookup<Ship> shipData;
    void Execute(ref NeedsDestroy nd, in WeaponShot ws, in Entity e)
    {
        float3 pos = transformData[e].Position;
        for(int i = 0; i < shipEntities.Length; ++i)
        {
            Entity shipEntity = shipEntities[i];

            if(shipEntity == ws.Shooter) { continue; }

            float3 shipPos = transformData[shipEntity].Position;
            Ship ship = shipData[shipEntity];
            float size = ship.size + ws.size;
            if(math.distancesq(pos, shipPos) < size * size)
            {
                nd.destroyTime = timeData.ElapsedTime;
            }
        }
    }
}

[BurstCompile]
public partial struct AOEAffectShipJob : IJobEntity
{
    [ReadOnly] public TimeData timeData;
    [ReadOnly] public NativeArray<Entity> aoeEntities;
    [ReadOnly] public ComponentLookup<AreaOfEffect> aoeData;
    [ReadOnly] public ComponentLookup<LocalToWorld> transformData;
    public EntityCommandBuffer.ParallelWriter ecb;
    void Execute(ref Accelerating ac, in Entity e, [EntityIndexInQuery] int entityInQueryIndex)
    {
        float3 pos = transformData[e].Position;
        for(int i = 0; i < aoeEntities.Length; ++i)
        {
            Entity aoeEntity = aoeEntities[i];
            float r = aoeData[aoeEntity].radius;
            float3 c = transformData[aoeEntity].Position;
            float distSq = math.distancesq(pos, c);
            if(distSq < r * r && ac.beingHitUntil < timeData.ElapsedTime)
            {
                ac.beingHitUntil = timeData.ElapsedTime + 1.5;
                ac.accel += 100f * math.normalize(pos - c);
                Globals.sharedEntityFactory.Data.CreateShieldHitAsync(entityInQueryIndex, ecb, ac.beingHitUntil, e, transformData[e].Value);
            }
        }
    }
}

[BurstCompile]
public partial struct ShipSystem : ISystem
{
    [ReadOnly] private ComponentLookup<LocalToWorld> transformData;
    [ReadOnly] private ComponentLookup<NextTransform> nextTransformData;
    [ReadOnly] private ComponentLookup<Station> stationData;
    [ReadOnly] private ComponentLookup<Ship> shipData;
    [ReadOnly] private ComponentLookup<AreaOfEffect> aoeData;

    private EntityQuery stationsQuery;
    private EntityQuery shipsQuery;
    private EntityQuery aoeQuery;

    [BurstCompile]
    public void OnCreate(ref SystemState systemState)
    {
        transformData = SystemAPI.GetComponentLookup<LocalToWorld>();
        nextTransformData = SystemAPI.GetComponentLookup<NextTransform>();
        stationData = SystemAPI.GetComponentLookup<Station>();
        shipData = SystemAPI.GetComponentLookup<Ship>();
        aoeData = SystemAPI.GetComponentLookup<AreaOfEffect>();

        stationsQuery = new EntityQueryBuilder(Allocator.Temp).WithAll<Station, LocalToWorld>().Build(ref systemState);
        shipsQuery = new EntityQueryBuilder(Allocator.Temp).WithAll<Ship, LocalToWorld>().Build(ref systemState);
        aoeQuery = new EntityQueryBuilder(Allocator.Temp).WithAll<AreaOfEffect, LocalToWorld>().Build(ref systemState);
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
        shipData.Update(ref systemState);
        aoeData.Update(ref systemState);

        EndSimulationEntityCommandBufferSystem.Singleton ecbSystem = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>();

        NativeArray<Entity> stationEntities = stationsQuery.ToEntityArray(systemState.WorldUpdateAllocator);
        NativeArray<Entity> shipEntities = shipsQuery.ToEntityArray(systemState.WorldUpdateAllocator);
        NativeArray<Entity> aoeEntities = aoeQuery.ToEntityArray(systemState.WorldUpdateAllocator);

        systemState.Dependency = new UpdatePlayerShipJob { timeData = SystemAPI.Time }.ScheduleParallel(systemState.Dependency);

        systemState.Dependency = new AOEAffectShipJob { timeData = SystemAPI.Time, aoeEntities = aoeEntities, transformData = transformData, aoeData = aoeData, ecb = ecbSystem.CreateCommandBuffer(systemState.WorldUnmanaged).AsParallelWriter() }.ScheduleParallel(systemState.Dependency);

        systemState.Dependency = new ShootWeaponsJob { timeData = SystemAPI.Time, ecb = ecbSystem.CreateCommandBuffer(systemState.WorldUnmanaged).AsParallelWriter() }.ScheduleParallel(systemState.Dependency);

        systemState.Dependency = new UpdateShipsWithStationsJob { stationEntities = stationEntities, stationData = stationData, transformData = transformData, timeData = SystemAPI.Time, nextTransformData = nextTransformData }.ScheduleParallel(systemState.Dependency);

        systemState.Dependency = new CollideWithWeaponShotsJob { timeData = SystemAPI.Time, shipEntities = shipEntities, transformData = transformData, shipData = shipData }.ScheduleParallel(systemState.Dependency);

        
    }
}

