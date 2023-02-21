using System;
using System.Collections.Generic;
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

    private static string infoFilename = "Map1.json";
    private MapInfo mapInfo;

    private Camera mainCamera;

    public void Instantiate(Camera mainCamera)
    {
        this.mainCamera = mainCamera;
        this.mapInfo = MapInfo.fromJsonFile(infoFilename);
        currentSectorIndex = mapInfo.startingSectorIndex;
        Assert.IsTrue(mapInfo.sectorInfos.Length > currentSectorIndex);
        LoadSector(currentSectorIndex);
    }

    public void LoadSector(int sectorIndex)
    {
        SectorInfo sectorInfo = mapInfo.sectorInfos[sectorIndex];
        GameObject newSector = new GameObject("Sector " + sectorIndex.ToString());
        Sector sectorComponent = newSector.AddComponent<Sector>();
        sectorComponent.Initialize(sectorInfo, mainCamera);
        currentSector = newSector;
    }
}
