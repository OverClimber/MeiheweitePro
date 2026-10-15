using System;
using UnityEngine;
public class WindowServant2D : Servant
{
    Vector3 windowScale = Vector3.one;

    public static Vector3 ArrangeWindow(GameObject window, Camera camera, Rect viewport, bool shown, Vector3 originalScale)
    {
        window.transform.localScale = originalScale;
        Bounds bounds = NGUIMath.CalculateRelativeWidgetBounds(window.transform);
        Rect projected = ProjectWindow(window.transform, camera, bounds);
        float scale = Mathf.Min(1f, Mathf.Min(Mathf.Max(1f, viewport.width - 16f) / Mathf.Max(1f, projected.width),
            Mathf.Max(1f, viewport.height - 16f) / Mathf.Max(1f, projected.height)));
        window.transform.localScale = originalScale * scale;
        projected = ProjectWindow(window.transform, camera, bounds);
        Vector3 origin = camera.WorldToScreenPoint(window.transform.position);
        Vector2 center = shown ? viewport.center : new Vector2(viewport.center.x, Screen.height + projected.height / 2f + 8f);
        return camera.ScreenToWorldPoint(new Vector3(origin.x + center.x - projected.center.x,
            origin.y + center.y - projected.center.y, origin.z));
    }

    static Rect ProjectWindow(Transform window, Camera camera, Bounds bounds)
    {
        Vector2 min = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
        Vector2 max = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
        for (int i = 0; i < 8; i++)
        {
            Vector3 corner = bounds.center + Vector3.Scale(bounds.extents,
                new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
            Vector3 point = camera.WorldToScreenPoint(window.TransformPoint(corner));
            min = Vector2.Min(min, point); max = Vector2.Max(max, point);
        }
        return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
    }

    Vector3 WindowPosition(bool shown)
    {
        Rect viewport = new Rect(0, 0, Screen.width, Screen.height);
#if UNITY_ANDROID && !UNITY_EDITOR
        viewport = Screen.safeArea;
#endif
        return ArrangeWindow(gameObject, Program.camera_main_2d, viewport, shown, windowScale);
    }

    // Call after dynamically adding settings rows, before the first rendered frame.
    public void PlaceHiddenWindow()
    {
        if (gameObject != null && !isShowed)
        {
            UIHelper.clearITWeen(gameObject);
            gameObject.transform.position = WindowPosition(false);
        }
    }

    public override void applyHideArrangement()
    {
        if (gameObject != null)
        {
            UIHelper.clearITWeen(gameObject);
            iTween.MoveTo(gameObject, WindowPosition(false), 0.6f);
        }
    }

    public override void applyShowArrangement()
    {
        if (gameObject != null)
        {
            UIHelper.clearITWeen(gameObject);
            iTween.MoveTo(gameObject, WindowPosition(true), 0.6f);
        }
    }

    public override void hide()
    {
        base.hide();
        TraceVisible2D(false);
        Program.ShiftUIenabled(Program.ui_main_3d, true);
    }

    public override void show()
    {
        base.show();
        TraceVisible2D(true);
        Program.ShiftUIenabled(Program.ui_main_3d, false);
    }

    /// <summary>
    /// 记录窗口显示/隐藏时刻（仅 log/qt_debug.on 存在时）。
    /// 2D 窗口与 SP 不同：不 SetActive，而是 createWindow 时放在屏幕外 1.5 倍高处、
    /// show 时 iTween 滑入 —— 所以这里的时刻是「开始滑入」，落定还要 +0.6 秒。
    /// </summary>
    private void TraceVisible2D(bool shown)
    {
        if (gameObject == null || !QuickTestTrace.Enabled)
        {
            return;
        }
        QuickTestTrace.Log("vis", gameObject.name + " -> " + (shown ? "true" : "false")
            + " frame=" + Time.frameCount);
    }

    public static GameObject createWindow(Servant servant, GameObject mod)
    {
        GameObject re = servant.create
            (
            mod,
            Program.camera_main_2d.ScreenToWorldPoint(new Vector3(Screen.width / 2, Screen.height * 1.5f, 600)),
            new Vector3(0, 0, 0),
            false,
            Program.ui_main_2d
            );
        UIHelper.InterGameObject(re);
        var window = servant as WindowServant2D;
        if (window != null)
        {
            window.windowScale = re.transform.localScale;
            re.transform.position = ArrangeWindow(re, Program.camera_main_2d,
                new Rect(0, 0, Screen.width, Screen.height), false, window.windowScale);
        }
        return re;
    }



}
