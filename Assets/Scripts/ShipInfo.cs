using System;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

public class LightInfoBehavior : MonoBehaviour
{
    public LightInfo lightInfo;
}

[Serializable]
public struct LightInfo
{
    public float[] relativePos;
    public float[] relativeFacing;
    public float spotAngleDegrees;
    public float spotInnerAngleDegrees;
    public float[] colorRGBA;
    public float intensity;
    public LightInfoBehavior AddToGameObject(GameObject go)
    {
        LightInfoBehavior behavior = go.AddComponent<LightInfoBehavior>();
        behavior.lightInfo = this;
        return behavior;
    }
    public bool IsPoint()
    {
        return spotAngleDegrees == 0 && spotInnerAngleDegrees == 0;
    }
    public float3 RelativePos()
    {
        return new float3(relativePos[0], relativePos[1], relativePos[2]);
    }

    public float3 RelativeFacing()
    {
        return new float3(relativeFacing[0], relativeFacing[1], relativeFacing[2]);
    }

    public Color GetColor()
    {
        return new Color(colorRGBA[0], colorRGBA[1], colorRGBA[2], colorRGBA[3]);
    }
}

[Serializable]
public struct ShipPartInfo
{
    public string mesh;
    public string material;
}

[Serializable]
public struct ShipInfo
{
    public string name;
    public float thrust;
    public float rotationSpeed;
    public float initialScale;
    public float[] initialRotationDegrees;
    public string path;
    public string meshBundle;
    public ShipPartInfo[] parts;
    public LightInfo[] lights;
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
