using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AISpwaner : MonoBehaviour
{
    [SerializeField]
    [Range(0,100)]
    float minSpawnInterval = defaultMinSpawnInterval;

    [SerializeField]
    [Range(0, 100)]
    float maxSpawnInterval = defaultMaxSpawnInterval;

    private static float defaultMinSpawnInterval = 1.0f;
    private static float defaultMaxSpawnInterval = 10.0f;
    private static float RandomSpawnTime(float minSpawnInterval, float maxSpawnInterval)
    {
        if(maxSpawnInterval <= minSpawnInterval)
        {
            return Time.time;
        }
        return Time.time + Random.Range(minSpawnInterval, maxSpawnInterval);
    }

    private float nextSpawnTime;

    void Start()
    {
        nextSpawnTime = RandomSpawnTime();
    }

    void Update()
    {
        if(Time.time >= nextSpawnTime)
        {
            SpawnAIShip(Globals.sharedLevelInfo.Data);
            nextSpawnTime = RandomSpawnTime();
        }
    }

    private float RandomSpawnTime()
    {
        return RandomSpawnTime(minSpawnInterval, maxSpawnInterval);
    }

    private void SpawnAIShip(LevelInfo levelInfo)
    {
        //Debug.Log("Spawn Ship");
    }
}
