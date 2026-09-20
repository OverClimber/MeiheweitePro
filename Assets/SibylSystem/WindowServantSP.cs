using System;
using UnityEngine;
public class WindowServantSP : Servant  
{
    public bool instanceHide = false;

    public override void hide()
    {
        base.hide();
        if (instanceHide)
        {
            if (gameObject != null)
            {
                var glass = gameObject.transform.Find("glass");
                UIPanel pan = gameObject.GetComponentInChildren<UIPanel>();
                if (pan != null)
                {
                    pan.alpha = 0;
                }
                if (glass != null)
                {
                    glass.gameObject.SetActive(false);
                }
                SetActiveFalse();
            }
        }
    }

    public override void applyHideArrangement()
    {
        base.applyHideArrangement();
        if (gameObject != null)
        {
            if (instanceHide)   
            {
                return;
            }
            var glass = gameObject.transform.Find("glass");
            var panelKIller = gameObject.GetComponent<panelKIller>();
            if (panelKIller == null)
            {
                panelKIller = gameObject.AddComponent<panelKIller>();
            }
            panelKIller.set(false);
            Program.go(1000, SetActiveFalse);
            if (glass != null)
            {
                glass.gameObject.SetActive(false);
            }
        }
        resize();
    }

    public void SetActiveFalse()   
    {
        TraceVisible(false);
        gameObject.SetActive(false);
    }

    public void SetActiveTrue()
    {
        TraceVisible(true);
        gameObject.SetActive(true);
    }

    /// <summary>
    /// 记录窗口真正的显示/隐藏时刻（仅 log/qt_debug.on 存在时）。
    ///
    /// 排查用：启动瞬间「某个窗口整页闪了一下」这类问题，事后再截图是抓不住的，
    /// 看 isShowed 也分辨不出（isShowed 与 SetActive 之间有 1000ms 的延迟任务）。
    /// 这里只在 active 状态真的翻转时记一行，于是启动时间轴上「谁在什么时候亮过」
    /// 就是一份可以直接对账的证据。
    /// </summary>
    private void TraceVisible(bool active)
    {
        if (gameObject == null || gameObject.activeSelf == active || !QuickTestTrace.Enabled)
        {
            return;
        }
        QuickTestTrace.Log("vis", gameObject.name + " -> " + (active ? "true" : "false")
            + " frame=" + Time.frameCount);
    }

    public override void applyShowArrangement()
    {
        base.applyShowArrangement();
        if (gameObject != null)
        {
            Program.notGo(SetActiveFalse);
            SetActiveTrue();
            var panelKIller = gameObject.GetComponent<panelKIller>();
            if (panelKIller == null)
            {
                panelKIller = gameObject.AddComponent<panelKIller>();
            }
            panelKIller.set(true);
            var glass = gameObject.transform.Find("glass");
            if (glass != null)
            {
                glass.gameObject.SetActive(true);
            }
        }
        resize();
    }

    void resize()
    {
        if (gameObject != null)
        {
            if (Program.I().setting.setting.resize.value)
            {
                float f = Screen.height / 700f;
                gameObject.transform.localScale = new Vector3(f, f, f);
            }
            else
            {
                gameObject.transform.localScale = new Vector3(1, 1, 1);
            }
        }
    }

    public void createWindow(GameObject mod)
    {
        gameObject = create
            (
            mod,
            Vector3.zero,
            Vector3.zero,
            false,
            Program.ui_windows_2d
            );
        UIHelper.InterGameObject(gameObject);
        Vector3 v=new Vector3();
        v.x = Mathf.Clamp(Config.getFloat("x_" + gameObject.name), -0.5f, 0.5f) * (float)Screen.width;
        v.y = Mathf.Clamp(Config.getFloat("y_" + gameObject.name), -0.5f, 0.5f) * (float)Screen.height;
        gameObject.transform.localPosition = v;
        var panelKIller = gameObject.GetComponent<panelKIller>();
        if (panelKIller == null)
        {
            panelKIller = gameObject.AddComponent<panelKIller>();
        }
        panelKIller.ini();
    }

    public override void ES_quit()
    {
        base.ES_quit();
        if (gameObject != null)
        {
            Config.setFloat("x_" + gameObject.name, gameObject.transform.localPosition.x / (float)Screen.width);
            Config.setFloat("y_" + gameObject.name, gameObject.transform.localPosition.y / (float)Screen.height);
        }
    }
}
