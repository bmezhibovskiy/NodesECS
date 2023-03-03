using System;
using UnityEngine;

[Serializable]
public struct StationModuleInfo
{
    public string type;
    public float[] parameters;
}

[Serializable]
public struct StationInfo
{
    public string name;
    public string type;
    public Vector3 position;
    public float size;
    public int factionIndex;
    public StationModuleInfo[] moduleInfos;
}

[Serializable]
public struct SectorInfo
{
    public int sectorId;
    public string name;
    public int[] connectedSectorIds;
    public bool is3d;
    public float nodeSize;
    public float sideLength;
    public int sideNodes;
    public Vector3 startPosition;
    public StationInfo[] stationInfos;
}

[Serializable]
public struct MapInfo
{
    public int startingSectorIndex;
    public SectorInfo[] sectorInfos;

    public static MapInfo FromJsonFile(string fileName)
    {
        return Utils.FromJsonFile<MapInfo>(fileName);
    }
}