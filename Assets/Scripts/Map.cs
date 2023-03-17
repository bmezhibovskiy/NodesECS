using System;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.Assertions;
using com.borismez.ShockwavesHDRP;

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
    private ShipInfos shipInfos;
    private StationTypeInfos stationInfos;

    private Camera mainCamera;

    private EntityManager em;
    private EntityQuery allEntitiesQuery;

    private ShockwaveManager shockwaveManager;

    Dictionary<string, PartsRenderInfo> partsRenderInfos;

    public void Instantiate(Camera mainCamera, Dictionary<string, PartsRenderInfo> partsRenderInfos, ShipInfos shipInfos, StationTypeInfos stationInfos, ShockwaveManager shockwaveManager)
    {
        this.mainCamera = mainCamera;
        this.partsRenderInfos = partsRenderInfos;
        this.shipInfos = shipInfos;
        this.stationInfos = stationInfos;
        this.shockwaveManager = shockwaveManager;
        this.mapInfo = MapInfo.FromJsonFile(infoFilename);
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
        sectorComponent.Initialize(sectorInfo, mainCamera, this, partsRenderInfos, shipInfos, stationInfos, shockwaveManager);
        currentSector = newSector;
    }
}
