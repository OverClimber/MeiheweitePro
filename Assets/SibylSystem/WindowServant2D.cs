using System;
using UnityEngine;
public class WindowServant2D : Servant
{
    public override void applyHideArrangement()
    {
        if (gameObject != null)
        {
            UIHelper.clearITWeen(gameObject);
            iTween.MoveTo(gameObject, Program.camera_main_2d.ScreenToWorldPoint(new Vector3(Screen.width / 2, Screen.height * 1.5f, 0)), 0.6f);
        }
    }

    public override void applyShowArrangement()
    {
        if (gameObject != null)
        {
            UIHelper.clearITWeen(gameObject);
            iTween.MoveTo(gameObject, Program.camera_main_2d.ScreenToWorldPoint(new Vector3(Screen.width / 2, Screen.height / 2, 0)), 0.6f);
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
        return re;
    }



}
