using System.Collections.Generic;
using UnityEngine;
using Screen = UnityEngine.Device.Screen;
using SystemInfo = UnityEngine.Device.SystemInfo;

/// <summary>在同一坐标系内，求一行文字避开遮挡后可用的最长区间。</summary>
public static class NotchTextLayout
{
    public static void GetLocalCutouts(Transform target, float padding, float fallbackHeightRatio,
        List<Rect> result)
    {
        result.Clear();
        Camera camera = NGUITools.FindCameraForLayer(target.gameObject.layer);
        if (camera == null) return;
        float depth = camera.WorldToScreenPoint(target.position).z;
        foreach (Rect cutout in GetScreenCutouts(fallbackHeightRatio))
        {
            if (cutout.width <= 0 || cutout.height <= 0) continue;
            Vector3 min = target.InverseTransformPoint(camera.ScreenToWorldPoint(
                new Vector3(cutout.xMin - padding, cutout.yMin - padding, depth)));
            Vector3 max = target.InverseTransformPoint(camera.ScreenToWorldPoint(
                new Vector3(cutout.xMax + padding, cutout.yMax + padding, depth)));
            result.Add(Rect.MinMaxRect(min.x, min.y, max.x, max.y));
        }
    }

    public static Rect[] GetScreenCutouts(float fallbackHeightRatio = 0.5f)
    {
        Rect[] cutouts = Screen.cutouts;
        if (HasCutouts(cutouts)) return cutouts;
        // 部分 iOS 设备和 Unity Device Simulator 只提供 safeArea。
        // 用居中的保守避让带覆盖刘海，仍保留上下空间；高度可在组件中调整。
        if (SystemInfo.deviceModel.StartsWith("iPhone", System.StringComparison.OrdinalIgnoreCase)
            && Screen.width > Screen.height)
        {
            Rect fallback = GetIPhoneFallback(Screen.width, Screen.height, Screen.safeArea,
                Screen.orientation, fallbackHeightRatio);
            if (fallback.width > 0) return new[] { fallback };
        }
        return cutouts ?? new Rect[0];
    }

    public static Rect GetIPhoneFallback(int width, int height, Rect safeArea,
        ScreenOrientation orientation, float heightRatio)
    {
        float left = Mathf.Max(0, safeArea.xMin);
        float right = Mathf.Max(0, width - safeArea.xMax);
        if (Mathf.Max(left, right) < 1f || width <= height) return new Rect();
        // 分辨率缩放可能令左右安全边距相差 1 像素，不应据此判断刘海朝向。
        bool onLeft = Mathf.Abs(left - right) > 2f ? left > right
            : orientation != ScreenOrientation.LandscapeRight;
        float cutoutHeight = height * Mathf.Clamp(heightRatio, 0.1f, 1f);
        return new Rect(onLeft ? 0 : width - right, (height - cutoutHeight) * 0.5f,
            onLeft ? left : right, cutoutHeight);
    }

    public static Rect GetRowBounds(Rect row, IList<Rect> exclusions)
    {
        float start = row.xMin;
        Rect best = new Rect(start, row.y, 0, row.height);
        // 遮挡数量很少。逐个寻找最近的区间，无需每帧分配或排序。
        while (start < row.xMax)
        {
            float next = row.xMax;
            float end = start;
            for (int i = 0; i < exclusions.Count; i++)
            {
                Rect cutout = exclusions[i];
                if (cutout.yMax <= row.yMin || cutout.yMin >= row.yMax
                    || cutout.xMax <= start || cutout.xMin >= row.xMax) continue;
                if (cutout.xMin <= start) end = Mathf.Max(end, cutout.xMax);
                else next = Mathf.Min(next, cutout.xMin);
            }
            if (end > start)
            {
                start = end;
                continue;
            }
            if (next - start > best.width)
                best = new Rect(start, row.y, next - start, row.height);
            start = next;
        }
        return best;
    }

    public static bool HasCutouts(Rect[] cutouts)
    {
        if (cutouts == null) return false;
        for (int i = 0; i < cutouts.Length; i++)
            if (cutouts[i].width > 0 && cutouts[i].height > 0) return true;
        return false;
    }
}
