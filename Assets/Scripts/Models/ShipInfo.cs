using System;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

[Serializable]
public struct ShipInfo
{
    public string name;
    public float thrust;
    public float rotationSpeed;
    public DisplayInfo displayInfo;
}

[Serializable]
public struct ShipInfos
{
    public ShipInfo[] ships;
    private Dictionary<string, ShipInfo> dict;

    public Dictionary<string, ShipInfo> ToDictionary()
    {
        if (dict == null)
        {
            dict = new Dictionary<string, ShipInfo>();
            foreach (ShipInfo shipInfo in ships)
            {
                dict[shipInfo.name] = shipInfo;
            }
        }
        return dict;
    }

    public static ShipInfos FromJsonFile(string fileName)
    {
        return Utils.FromJsonFile<ShipInfos>(fileName);
    }
}
