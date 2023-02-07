using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Utils
{
    public static int to1D(int x, int y, int z, int numSideNodes)
    {
        return (z * numSideNodes * numSideNodes) + (y * numSideNodes) + x;
    }

    public static int[] to3D(int idx, int numSideNodes)
    {
        int z = idx / (numSideNodes * numSideNodes);
        idx -= (z * numSideNodes * numSideNodes);
        int y = idx / numSideNodes;
        int x = idx % numSideNodes;
        return new int[] { x, y, z };
    }

    public static int to1D(int x, int y, int numSideNodes)
    {
        return (y * numSideNodes) + x;
    }

    public static int[] to2D(int idx, int numSideNodes)
    {
        int y = idx / numSideNodes;
        int x = idx % numSideNodes;
        return new int[] { x, y };
    }
    public static Vector3? LineSegmentCircleIntersection(Vector3 center, float r, Vector3 start, Vector3 end)
    {
        Vector3 d = end - start;
        Vector3 f = start - center;

        float e = 0.0001f;
        float a = Vector3.Dot(d, d);
        float b = 2 * Vector3.Dot(f, d);
        float c = Vector3.Dot(f, f) - r * r;

        //Solve using quadratic formula
        float discriminant = b * b - 4 * a * c;

        if (discriminant < 0)
        {
            return null;
        }
        discriminant = Mathf.Sqrt(discriminant);
        float t1 = (-b - discriminant) / (2 * a);
        float t2 = (-b + discriminant) / (2 * a);
        if (t1 >= -e && t1 <= 1 + e)
        {
            return start + t1 * d;
        }

        //Some other strange intersection case where the start is inside or past the circle
        Vector3 dir = start - center;
        return center + dir.normalized * r;
    }
}
