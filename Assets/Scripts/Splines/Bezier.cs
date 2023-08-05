using UnityEngine;

public static class Bezier
{
    public static Vector3 GetPoint(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
    {
        t = Mathf.Clamp01(t);
        float inv = 1f - t;

        return inv * inv * inv * p0 
               + 3f * inv * inv * t * p1 
               + 3f * inv * t * t * p2 
               + t * t * t * p3;
    }

    public static Vector3 GetFirstDerivative(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
    {
        t = Mathf.Clamp01(t);
        float inv = 1f - t;
        return 3f * inv * inv * (p1 - p0) +
               6f * inv * t * (p2 - p1) +
               3f * t * t * (p3 - p2);
    }
}