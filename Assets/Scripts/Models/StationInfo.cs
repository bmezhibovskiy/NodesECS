using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public struct StationTypeInfo
{
    public string name;
    public DisplayInfo displayInfo;
}

[Serializable]
public struct StationTypeInfos
{
    public StationTypeInfo[] stations;
    private Dictionary<string, StationTypeInfo> dict;

    public Dictionary<string, StationTypeInfo> ToDictionary()
    {
        if (dict == null)
        {
            dict = new Dictionary<string, StationTypeInfo>();
            foreach (StationTypeInfo stationInfo in stations)
            {
                dict[stationInfo.name] = stationInfo;
            }
        }
        return dict;
    }

    public static StationTypeInfos FromJsonFile(string fileName)
    {
        return Utils.FromJsonFile<StationTypeInfos>(fileName);
    }
}
