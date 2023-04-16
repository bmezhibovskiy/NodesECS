using Unity.Burst;
using Unity.Entities;
using UnityEngine;

[BurstCompile]
public partial struct AISpawnerSystem : ISystem
{
    private const float defaultMinSpawnInterval = 2.0f;
    private const float defaultMaxSpawnInterval = 8.0f;
    private double nextSpawnTime;

    [BurstCompile]
    public void OnCreate(ref SystemState systemState)
    {
        nextSpawnTime = RandomSpawnTime(systemState);
    }

    [BurstCompile]
    public void OnDestroy(ref SystemState systemState)
    {

    }

    [BurstCompile]
    public void OnUpdate(ref SystemState systemState)
    {
        if (SystemAPI.Time.ElapsedTime >= nextSpawnTime)
        {
            BeginSimulationEntityCommandBufferSystem.Singleton ecbSystem = SystemAPI.GetSingleton<BeginSimulationEntityCommandBufferSystem.Singleton>();
            SpawnAIShip(ecbSystem.CreateCommandBuffer(systemState.WorldUnmanaged).AsParallelWriter(), Globals.sharedLevelInfo.Data);
            nextSpawnTime = RandomSpawnTime(systemState);
        }
    }

    [BurstCompile]
    private double RandomSpawnTime(SystemState systemState, float minSpawnInterval = defaultMinSpawnInterval, float maxSpawnInterval = defaultMaxSpawnInterval)
    {
        double currentTime = systemState.WorldUnmanaged.Time.ElapsedTime;
        if (maxSpawnInterval <= minSpawnInterval)
        {
            return currentTime;
        }
        double randomDelay = Globals.sharedRandom.Data.NextDouble(minSpawnInterval, maxSpawnInterval);
        return currentTime + randomDelay;
    }

    [BurstCompile]
    private void SpawnAIShip(EntityCommandBuffer.ParallelWriter ecb, LevelInfo levelInfo)
    {
        //Debug.Log("Spawn Ship");
    }
}