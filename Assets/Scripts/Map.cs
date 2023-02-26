using System;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.Assertions;

public class SectorConnection : IEquatable<SectorConnection>
{
    public int id1, id2;

    public bool Equals(SectorConnection other)
    {
        return id1 == other.id1 && id2 == other.id2 || id1 == other.id2 && id2 == other.id1;
    }
}

public class Map : MonoBehaviour
{
    HashSet<SectorConnection> connections = new HashSet<SectorConnection>();
    GameObject currentSector;
    int currentSectorIndex = 0;
    bool needsLoad = true;

    private static string infoFilename = "Map1.json";
    private MapInfo mapInfo;

    private Camera mainCamera;

    private EntityManager em;
    private EntityQuery allEntitiesQuery;

    public void Instantiate(Camera mainCamera)
    {
        this.mainCamera = mainCamera;
        this.mapInfo = MapInfo.fromJsonFile(infoFilename);
        currentSectorIndex = mapInfo.startingSectorIndex;
        Assert.IsTrue(mapInfo.sectorInfos.Length > currentSectorIndex);

        em = World.DefaultGameObjectInjectionWorld.EntityManager;
        allEntitiesQuery = new EntityQueryBuilder(Unity.Collections.Allocator.Temp).WithAll<DestroyOnLevelUnload>().Build(em);
    }

    public void Jump(int newSectorIndex)
    {
        Destroy(currentSector);
        currentSectorIndex = newSectorIndex;
        Globals.sharedLevelInfo.Data.needsDestroy = true;
        needsLoad = true;
    }
    void Update()
    {
        int numEntities = allEntitiesQuery.CalculateEntityCount();
        
        if (needsLoad && numEntities <= 0)
        {
            needsLoad = false;
            Globals.sharedLevelInfo.Data.needsDestroy = false;
            LoadCurrentSector();
        }
    }
    private void LoadCurrentSector()
    {
        SectorInfo sectorInfo = mapInfo.sectorInfos[currentSectorIndex];
        GameObject newSector = new GameObject("Sector " + currentSectorIndex.ToString());
        Sector sectorComponent = newSector.AddComponent<Sector>();
        sectorComponent.Initialize(sectorInfo, mainCamera, this);
        currentSector = newSector;
    }
}
