using UnityEngine;
using System;
using System.IO;
public class BackGroundPic : Servant
{
    GameObject backGround;

    /// <summary>RD 模式的独立背景图。放工程根 texture/common/（随构建合并铺入运行目录）。</summary>
    const string OcgPicPath = "texture/common/desk.jpg";
    const string RdPicPath = "texture/common/desk_rd.jpg";

    Texture2D ocgTexture;
    Texture2D rdTexture;

    public override void initialize()
    {
        backGround = create(Program.I().mod_simple_ngui_background_texture, Vector3.zero, Vector3.zero, false, Program.ui_back_ground_2d);
        ocgTexture = LoadTexture(OcgPicPath);
        // RD 图缺失不致命：回落到 OCG 那张，两种模式共用，只是没有"独立背景"而已。
        rdTexture = LoadTexture(RdPicPath);
        if (rdTexture == null)
        {
            rdTexture = ocgTexture;
        }
        backGround.GetComponent<UITexture>().mainTexture = ocgTexture;
        backGround.GetComponent<UITexture>().depth = -100;
        // 模式切换时换图。Changed 只在真正变化时触发（Set 里同值早退），
        // 启动期的初始模式在这里手动补上。
        GameModeManager.Changed += OnModeChanged;
        ApplyMode(GameModeManager.Current, "init");
    }

    /// <summary>按当前模式换 mainTexture 并重排版（两张图宽高比可能不同）。</summary>
    void OnModeChanged(GameModeManager.Mode mode)
    {
        ApplyMode(mode, "changed");
    }

    void ApplyMode(GameModeManager.Mode mode, string why)
    {
        if (backGround == null)
        {
            return;
        }
        Texture2D pic = mode == GameModeManager.Mode.RD ? rdTexture : ocgTexture;
        if (pic == null)
        {
            return;
        }
        backGround.GetComponent<UITexture>().mainTexture = pic;
        applyShowArrangement();
        QuickTestTrace.Log("bg", "mode=" + GameModeManager.ModeLabel
            + " why=" + why
            + " tex=" + pic.width + "x" + pic.height
            + " path=" + (mode == GameModeManager.Mode.RD ? RdPicPath : OcgPicPath));
    }

    /// <summary>
    /// 同步读盘解码一张 JPG/PNG。只在启动期调用（两张合计 <100ms），
    /// 切换时只换引用、绝不走这里 —— 切换瞬间解码 20~60ms 是会掉帧的。
    /// </summary>
    static Texture2D LoadTexture(string path)
    {
        try
        {
            if (!File.Exists(path))
            {
                QuickTestTrace.Log("bg", "missing " + path);
                return null;
            }
            byte[] data = File.ReadAllBytes(path);
            // 与原实现一致保留 mipmap（8K 图缩到屏幕尺寸时没 mip 会闪烁）；LoadImage 会按
            // 图片实际尺寸覆盖这里的占位尺寸。
            Texture2D pic = new Texture2D(2, 2);
            if (!pic.LoadImage(data))
            {
                UnityEngine.Debug.Log("BackGroundPic 解码失败：" + path);
                return null;
            }
            return pic;
        }
        catch (Exception e)
        {
            UnityEngine.Debug.Log(e);
            return null;
        }
    }

    public override void applyShowArrangement()
    {
        UIRoot root = Program.ui_back_ground_2d.GetComponent<UIRoot>();
        float s = (float)root.activeHeight / Screen.height;
        var tex = backGround.GetComponent<UITexture>().mainTexture;
        if (tex == null)
        {
            return;
        }
        float ss = (float)tex.height / (float)tex.width;
        int width = (int)(Screen.width * s);
        int height = (int)(width * ss);
        if (height < Screen.height)
        {
            height = (int)(Screen.height * s);
            width = (int)(height / ss);
        }
        backGround.GetComponent<UITexture>().height = height+2;
        backGround.GetComponent<UITexture>().width = width+2;
    }

    public override void applyHideArrangement()
    {
        applyShowArrangement();
    }
}
