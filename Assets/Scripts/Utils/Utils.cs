using UnityEngine;
using Unity.Mathematics;
using System.Drawing;
using UnityEngine.UIElements;
using UnityEditor;

public class Utils
{
    public static int to1D(int x, int y, int z, int numSideNodes)
    {
        return (z * numSideNodes * numSideNodes) + (y * numSideNodes) + x;
    }

    public static int3 to3D(int idx, int numSideNodes)
    {
        int z = idx / (numSideNodes * numSideNodes);
        idx -= (z * numSideNodes * numSideNodes);
        int y = idx / numSideNodes;
        int x = idx % numSideNodes;
        return new int3 { x = x, y = y, z = z };
    }

    public static int to1D(int x, int y, int numSideNodes)
    {
        return (y * numSideNodes) + x;
    }

    public static int2 to2D(int idx, int numSideNodes)
    {
        int y = idx / numSideNodes;
        int x = idx % numSideNodes;
        return new int2 { x = x, y = y };
    }
    public static float3? LineSegmentCircleIntersection(float3 center, float r, float3 start, float3 end)
    {
        float3 d = end - start;
        float3 f = start - center;

        float e = 0.0001f;
        float a = math.dot(d, d);
        float b = 2 * math.dot(f, d);
        float c = math.dot(f, f) - r * r;

        //Solve using quadratic formula
        float discriminant = b * b - 4 * a * c;

        if (discriminant < 0)
        {
            return null;
        }
        discriminant = math.sqrt(discriminant);
        float t1 = (-b - discriminant) / (2 * a);
        float t2 = (-b + discriminant) / (2 * a);
        if (t1 >= -e && t1 <= 1 + e)
        {
            return start + t1 * d;
        }

        //Some other strange intersection case where the start is inside or past the circle
        float3 dir = start - center;
        return center + math.normalize(dir) * r;
    }
    public static T FromJsonFile<T>(string fileName)
    {
        TextAsset textAsset = (TextAsset)AssetDatabase.LoadAssetAtPath("Assets/Resources/Config/" + fileName, typeof(TextAsset));
        return JsonUtility.FromJson<T>(textAsset.text);
    }

    //Adapted from https://dev-tut.com/2022/unity-draw-a-circle-part2/
    public static void DebugDrawCircle(float3 c, float r, UnityEngine.Color color, int segments)
    {
        float angleStep = 2f * math.PI / (float)segments;

        // lineStart and lineEnd variables are declared outside of the following for loop
        float3 lineStart = float3.zero;
        float3 lineEnd = float3.zero;

        for (int i = 0; i < segments; ++i)
        {
            // Line start is defined as starting angle of the current segment (i)
            lineStart.x = math.cos(angleStep * i);
            lineStart.y = math.sin(angleStep * i);

            // Line end is defined by the angle of the next segment (i+1)
            lineEnd.x = math.cos(angleStep * (i + 1));
            lineEnd.y = math.sin(angleStep * (i + 1));

            // Results are multiplied so they match the desired radius
            lineStart *= r;
            lineEnd *= r;

            // Results are offset by the desired position/origin 
            lineStart += c;
            lineEnd += c;

            // Points are connected using DrawLine method and using the passed color
            Debug.DrawLine(lineStart, lineEnd, color);
        }
    }
}
