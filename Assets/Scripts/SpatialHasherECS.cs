using UnityEngine;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Newtonsoft.Json.Linq;

public struct SpatiallyHashed : IComponentData
{
    public int bucketIndex;
}

public struct SpatialHasher
{
    public readonly static int maxEntitiesInBucket = 64;
    public NativeSlice<Entity> entitiesInBuckets; //Flattened 2d array
    public NativeSlice<int> bucketCounts;


    public NativeSlice<int2> flattenedSearchCoords;
    public NativeSlice<int> searchCoordLengths;

    public float inverseBucketSize;
    public int numSideBuckets;
    public float offset;
    public int numBuckets;

    public static int NumBuckets(float bucketSize, float totalSideLength)
    {
        int numSideBuckets = (int)(totalSideLength / bucketSize);
        return numSideBuckets * numSideBuckets;
    }

    public void Initialize(ref NativeArray<Entity> entitiesArray, ref NativeArray<int> bucketCountsArray, ref NativeArray<int2> flattenedSearchCoordsArray, ref NativeArray<int> searchCoordLengthsArray, float bucketSize, float totalSideLength)
    {
        inverseBucketSize = 1 / bucketSize;
        numSideBuckets = (int)(totalSideLength * inverseBucketSize);
        offset = totalSideLength * 0.5f;
        numBuckets = numSideBuckets * numSideBuckets;

        for (int i = 0; i < numBuckets; ++i)
        {
            bucketCountsArray[i] = 0;
        }

        for (int i = 0; i < numBuckets * maxEntitiesInBucket; ++i)
        {
            entitiesArray[i] = Entity.Null;
        }

        entitiesInBuckets = new NativeSlice<Entity>(entitiesArray);
        bucketCounts = new NativeSlice<int>(bucketCountsArray);
        flattenedSearchCoords = new NativeSlice<int2>(flattenedSearchCoordsArray);
        searchCoordLengths = new NativeSlice<int>(searchCoordLengthsArray);
    }

    public void Add(ref SpatiallyHashed hashed, Entity e, float3 pos)
    {
        int hash = Hash(pos);
        int newEntityIndex = bucketCounts[hash];

        if (newEntityIndex >= maxEntitiesInBucket) { return; }

        bucketCounts[hash] = newEntityIndex + 1;

        int flattenedIndex = Utils.to1D(newEntityIndex, hash, maxEntitiesInBucket);

        entitiesInBuckets[flattenedIndex] = e;

        hashed.bucketIndex = hash;
    }

    public void Update(ref SpatiallyHashed hashed, Entity e, float3 newPos)
    {
        int oldHash = hashed.bucketIndex;
        int newHash = Hash(newPos);
        if (oldHash != newHash)
        {
            Remove(hashed, e);
            Add(ref hashed, e, newPos);
        }
    }

    public void Remove(SpatiallyHashed hashed, Entity e)
    {
        int hash = hashed.bucketIndex;
        int numEntitiesInBucket = bucketCounts[hash];
        int flattenedIndex = 0;
        for (int entityIndex = 0; entityIndex < numEntitiesInBucket; ++entityIndex)
        {
            flattenedIndex = Utils.to1D(entityIndex, hash, maxEntitiesInBucket);
            if (entitiesInBuckets[flattenedIndex] == e) { break; }
        }

        int oldLastEntityIndex = numEntitiesInBucket - 1;
        int flattenedLastIndex = Utils.to1D(oldLastEntityIndex, hash, maxEntitiesInBucket);
        //Copy over the last entity to this one's position, and decrease count
        entitiesInBuckets[flattenedIndex] = entitiesInBuckets[flattenedLastIndex];
        bucketCounts[hash] = oldLastEntityIndex;
    }

    public NativeArray<Entity> ClosestNodes(float3 point, int numberOfObjectsToFetch, ComponentDataFromEntity<Translation> translationData)
    {
        NativeArray<Entity> closestObjects = new NativeArray<Entity>(numberOfObjectsToFetch, Allocator.Temp);
        int2 hashCoords = Utils.to2D(Hash(point), numSideBuckets);
        int level = 0;
        int numFetched = 0;
        int maxCoordsInShell = searchCoordLengths[searchCoordLengths.Length - 1];
        while (numFetched < numberOfObjectsToFetch)
        {
            NativeArray<Entity> shellEntities = new NativeArray<Entity>(numberOfObjectsToFetch, Allocator.Temp);
            int shellEntityIndex = 0;

            int coordsInShell = searchCoordLengths[level];
            for (int i = 0; i < coordsInShell; ++i)
            {
                int2 shellCoord2d = flattenedSearchCoords[Utils.to1D(i, level, maxCoordsInShell)];

                int2 nextBucketCoord = hashCoords + shellCoord2d;
                int nextBucketHash = Utils.to1D(nextBucketCoord.x, nextBucketCoord.y, numSideBuckets);
                if (nextBucketHash < 0 || nextBucketHash >= numBuckets)
                {
                    continue;
                }
                int numEntitiesInBucket = bucketCounts[nextBucketHash];
                for (int j = 0; j < numEntitiesInBucket && shellEntityIndex < numberOfObjectsToFetch; ++j)
                {
                    Entity bucketEntity = entitiesInBuckets[Utils.to1D(j, nextBucketHash, maxEntitiesInBucket)];
                    shellEntities[shellEntityIndex++] = bucketEntity;
                }
            }
            //Sort Shell entities
            EntityComparer comparer = new EntityComparer { pos = point, translationData = translationData };
            shellEntities.Sort(comparer);

            //Copy Shell entities into closest entities
            for (int i = 0; i < shellEntityIndex && numFetched < numberOfObjectsToFetch; i++)
            {
                closestObjects[numFetched++] = shellEntities[i];
            }

            ++level;
        }

        return closestObjects;
    }

    private int Hash(float3 point)
    {
        int x = (int)((point.x + offset) * inverseBucketSize);
        int y = (int)((point.y + offset) * inverseBucketSize);

        int hash = Utils.to1D(x, y, numSideBuckets);
        hash = math.clamp(hash, 0, numBuckets);

        return hash;
    }
}
public class SearchCoords
{
    public static int2[][] GenerateShells(int numSideCells)
    {
        int2[][] shells = new int2[NumTotalShells(numSideCells)][];
        for (int i = 0; i < shells.Length; ++i)
        {
            shells[i] = Generate2DShell(i, numSideCells);
        }
        return shells;
    }

    private static int NumTotalShells(int numSideCells)
    {
        return (numSideCells + 1) / 2;
    }

    private static int2[] Generate2DShell(int level, int numSideCells)
    {
        //Slow and inefficient brute force algorithm, because this will be precomputed

        List<int2> shell = new List<int2>();

        int max = level;
        int min = -max;
        for (int i = min; i <= max; ++i)
        {
            for (int j = min; j <= max; ++j)
            {
                if (i == min || j == min || i == max || j == max)
                {
                    shell.Add(new int2 { x = i, y = j });
                }
            }
        }
        shell.Sort((int2 a, int2 b) =>
        {
            float distA = math.distancesq((float2)a, float2.zero);
            float distB = math.distancesq((float2)b, float2.zero);
            return distA.CompareTo(distB);
        });

        return shell.ToArray();
    }
}