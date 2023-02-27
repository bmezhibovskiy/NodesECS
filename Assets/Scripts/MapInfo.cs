using System;
using UnityEditor;
using UnityEngine;

[Serializable]
public struct SectorObjectModuleInfo
{
    public string type;
    public float[] parameters;
}

[Serializable]
public struct SectorObjectInfo
{
    public string name;
    public Vector3 position;
    public float size;
    public int factionIndex;
    public SectorObjectModuleInfo[] moduleInfos;
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
    public SectorObjectInfo[] sectorObjectInfos;
}

[Serializable]
public struct MapInfo
{
    public int startingSectorIndex;
    public SectorInfo[] sectorInfos;

    public static MapInfo fromJsonFile(string fileName)
    {
        TextAsset textAsset = (TextAsset)AssetDatabase.LoadAssetAtPath("Assets/Config/" + fileName, typeof(TextAsset));
        return JsonUtility.FromJson<MapInfo>(textAsset.text);
    }
}