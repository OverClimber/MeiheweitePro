using UnityEngine;

/// <summary>Pure geometry for deck gestures; no rigidbody simulation.</summary>
public static class DeckDragGeometry
{
    // Halfway between extra (-6.2) and side (-12), rather than an invisible
    // boundary biased toward the extra row. A card type still decides main/extra.
    public const float SideBoundary = -9.1f;
    public const float RemovalEdge = 16.4f;

    public static float FollowFactor(float seconds)
    {
        return 1f - Mathf.Exp(-Mathf.Max(0f, seconds) / 0.025f);
    }

    public static bool Moved(Vector2 start, Vector2 current, float screenHeight)
    {
        float threshold = Mathf.Clamp(screenHeight / 180f, 5f, 12f);
        return (current - start).sqrMagnitude >= threshold * threshold;
    }

    public static bool TryProject(Ray ray, float height, out Vector3 point)
    {
        point = Vector3.zero;
        if (Mathf.Abs(ray.direction.y) < 0.00001f) return false;
        float distance = (height - ray.origin.y) / ray.direction.y;
        if (distance < 0 || float.IsNaN(distance) || float.IsInfinity(distance)) return false;
        point = ray.GetPoint(distance);
        return !float.IsNaN(point.x) && !float.IsInfinity(point.x)
            && !float.IsNaN(point.z) && !float.IsInfinity(point.z);
    }

    public static bool Outside(Vector3 point)
    {
        return Mathf.Abs(point.x) > RemovalEdge;
    }
}
