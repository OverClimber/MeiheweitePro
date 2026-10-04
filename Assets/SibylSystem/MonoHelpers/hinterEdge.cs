using System;
using UnityEngine;

/// <summary>
/// **贴边安全 + 永不被挡**的悬停提示，观感与**战斗界面的俯视角切换钮**
/// （<c>ArtSystem/gameInfo/viewToggleButton.cs</c>）**同源**。
///
/// <para><b>为什么不能直接用 <see cref="hinter"/></b>：那一支有两处在这个场景下都会失效 ——
/// ① 它把提示固定摆在控件**上方 45px**（<c>screenPosition.y += 45</c>），而检索窗的按钮
/// 钉在窗体顶缘下 18px、窗体顶缘又是屏幕顶缘（窗高等于屏高）⇒ 提示整条落在屏幕外，
/// 「代码写了提示、屏幕上什么都没有」；
/// ② 它用 <c>Program.I().create(mod_simple_ngui_text, …)</c> 那种**通用预制体**，
/// 归到 <c>ui_main_2d</c> 那套面板里 ⇒ **会被检索窗自己的面板压住**（用户 2026-10-03 实测：
/// 「既固定又被关联卡搜索栏挡住」）。</para>
///
/// <para>本组件照 <c>viewToggleButton</c> 的做法重做了一遍，两条都绕开了：
/// <list type="bullet">
/// <item><b>自建 UIPanel</b>，<c>depth = 场景里最高的面板 + 20</c> ⇒ 任何窗口都压不住它；</item>
/// <item><b>跟着鼠标</b>（不是钉在控件上），且先按「鼠标 +22/+22」试、越界就翻边，
/// 两个方向都判 ⇒ 贴右缘、贴上缘、贴角都出得来。</item>
/// </list></para>
///
/// <para>外观沿用那颗钮的：黑底 (0.05,0.05,0.07,0.94) + 白字、字号 22、居中、
/// 底板 4×4 自建白纹理（⛔ 偶数尺寸是硬要求，1×1 会让 NGUI 绘制矩形宽高双双归零）。
/// 局部单位按 <c>viewToggleButton.PanelUnit()</c> 的同一口径：<b>1 单位 = 1 屏幕像素</b>。</para>
///
/// <para>挂法与 <see cref="hinter"/> 一样：自己 <c>AddComponent&lt;UIEventTrigger&gt;</c>
/// 再接 onHoverOver / onHoverOut / onPress。⚠ 一颗钮**只能有一个**提示组件 ——
/// 两个都挂会一次悬停弹出两条、只消一条。</para>
/// </summary>
public class hinterEdge : MonoBehaviour
{
    /// <summary>提示正文（纯文本；不像 <see cref="hinter"/> 那样过 <c>InterString</c>）。</summary>
    public string str;

    /// <summary>
    /// true = <paramref name="str"/> 是**待翻译**的原文键，等 <c>InterString.loaded</c>
    /// 后过一遍 <c>InterString.Get</c>（与 <see cref="hinter"/> 的行为对齐 ——
    /// 简介面板从 prefab 带过来的旧提示是键不是成品字）。默认 false：
    /// 检索窗那颗给的是**成品字**，不过表（过了表反而会往翻译补档里写一行垃圾）。
    /// </summary>
    public bool translate = false;
    bool translatedOnce = false;

    /// <summary>提示摆在鼠标右下时的偏移（与 viewToggleButton 同值）。</summary>
    const float TipOffset = 22f;

    // ── 节点（每颗钮自己一套）──────────────────────────────────────────────
    GameObject tipRoot = null;
    UITexture tipBg = null;
    UILabel tipLabel = null;
    string tipShown = null;
    bool hovering = false;
    static Texture2D boxTex = null;

    void Start()
    {
        UIEventTrigger trigger = GetComponent<UIEventTrigger>();
        if (trigger == null)
        {
            trigger = gameObject.AddComponent<UIEventTrigger>();
        }
        trigger.onHoverOver.Add(new EventDelegate(this, "in_"));
        trigger.onHoverOut.Add(new EventDelegate(this, "out_"));
        trigger.onPress.Add(new EventDelegate(this, "out_"));
    }

    void OnDestroy()
    {
        // 提示根是自建的（不归任何窗口管）⇒ 必须自己收掉，否则会留在屏幕上。
        if (tipRoot != null)
        {
            Destroy(tipRoot);
            tipRoot = null;
        }
    }

    void in_()
    {
        hovering = true;
        if (QuickTestTrace.Enabled)
        {
            QuickTestTrace.Log("tipedge", "hover on \"" + str + "\"");
        }
    }

    void out_()
    {
        hovering = false;
        if (tipRoot != null)
        {
            tipRoot.SetActive(false);
        }
        tipShown = null;
    }

    /// <summary>验收探针：强制进入"悬停中"（合成鼠标事件送不进这个客户端，见 harness）。</summary>
    public void ProbeHover()
    {
        in_();
    }

    /// <summary>验收探针：强制退出悬停。</summary>
    public void ProbeUnhover()
    {
        out_();
    }

    void Update()
    {
        if (translate && !translatedOnce && InterString.loaded)
        {
            translatedOnce = true;
            str = InterString.Get(str);
            tipShown = null;   // 译好的字与原文不同长 ⇒ 底板宽度下一拍重算
        }
        if (!hovering)
        {
            return;
        }
        if (string.IsNullOrEmpty(str))
        {
            if (tipRoot != null && tipRoot.activeSelf)
            {
                tipRoot.SetActive(false);
            }
            return;
        }
        if (!EnsureTip(str))
        {
            return;
        }
        // 位置：跟鼠标，两个方向都判越界翻边（同 viewToggleButton.UpdateHover）。
        Camera cam = Program.camera_main_2d;
        if (cam == null)
        {
            return;
        }
        int tw = tipBg != null ? tipBg.width : 0;
        int th = tipBg != null ? tipBg.height : 0;
        float mx = Input.mousePosition.x;
        float my = Input.mousePosition.y;
        float ox, oy;
        if (mx + TipOffset + tw > Screen.width)
        {
            ox = mx - TipOffset - tw;         // 靠右缘 ⇒ 翻到鼠标左侧
        }
        else
        {
            ox = mx + TipOffset;
        }
        if (my + TipOffset + th > Screen.height)
        {
            oy = my - TipOffset - th;         // 靠上缘 ⇒ 翻到鼠标下方
        }
        else
        {
            oy = my + TipOffset;
        }
        // ⛔ tipRoot 没有父节点 ⇒ 给**世界坐标**即可，别除 unit（除会把落点放大 ~500 倍）。
        tipRoot.transform.position = cam.ScreenToWorldPoint(new Vector3(ox, oy, 0f));
        tipRoot.SetActive(true);
        if (tipShown != str)
        {
            tipShown = str;
            UIPanel pnl = tipRoot.GetComponent<UIPanel>();
            QuickTestTrace.Log("tipedge", "show \"" + str + "\""
                + " mouse=" + Mathf.RoundToInt(mx) + "," + Mathf.RoundToInt(my)
                + " at=" + Mathf.RoundToInt(ox) + "," + Mathf.RoundToInt(oy)
                + " flipX=" + (ox < mx) + " flipY=" + (oy < my)
                + " depth=" + (pnl != null ? pnl.depth.ToString() : "-")
                + " onScreen=" + (ox >= 0 && ox <= Screen.width && oy >= 0 && oy <= Screen.height));
        }
    }

    bool EnsureTip(string txt)
    {
        try
        {
            Camera cam = Program.camera_main_2d;
            float unit = PanelUnit();
            if (cam == null || unit <= 0f || Program.ui_main_2d == null)
            {
                return false;
            }
            if (tipRoot == null)
            {
                // ⛔ depth 必须**高过场景里所有面板**，否则会被检索窗/决斗场的面板盖住
                //   （这正是通用预制体那条路失败的原因）。
                int depth = 1000;
                for (int i = 0; i < UIPanel.list.Count; i++)
                {
                    if (UIPanel.list[i] != null && UIPanel.list[i].depth + 20 > depth)
                    {
                        depth = UIPanel.list[i].depth + 20;
                    }
                }
                tipRoot = new GameObject("hinter_edge_tip");
                tipRoot.layer = Program.ui_main_2d.layer;
                tipRoot.transform.localScale = new Vector3(unit, unit, unit);
                UIPanel panel = tipRoot.AddComponent<UIPanel>();
                panel.depth = depth;
                panel.clipping = UIDrawCall.Clipping.None;

                GameObject bg = new GameObject("hinter_edge_tip_bg");
                bg.layer = tipRoot.layer;
                bg.transform.SetParent(tipRoot.transform, false);
                tipBg = bg.AddComponent<UITexture>();
                tipBg.mainTexture = BoxTex();
                tipBg.path = "";               // ⛔ 必须显式置 ""，否则 OnStart 用 mOutPath 冲掉贴图
                Shader sh = DuelUndo.UiTexShader();
                if (sh != null)
                {
                    tipBg.shader = sh;
                }
                tipBg.color = new Color(0.05f, 0.05f, 0.07f, 0.94f);   // 与俯视角钮同色
                tipBg.depth = 0;

                GameObject tx = new GameObject("hinter_edge_tip_text");
                tx.layer = tipRoot.layer;
                tx.transform.SetParent(tipRoot.transform, false);
                tipLabel = tx.AddComponent<UILabel>();
                UILabel tpl = DuelUndo.FindLabelTemplate();
                if (tpl != null)
                {
                    tipLabel.bitmapFont = tpl.bitmapFont;
                    tipLabel.trueTypeFont = tpl.trueTypeFont;
                    tipLabel.fontStyle = tpl.fontStyle;
                    tipLabel.applyGradient = tpl.applyGradient;
                    tipLabel.gradientTop = tpl.gradientTop;
                    tipLabel.gradientBottom = tpl.gradientBottom;
                }
                tipLabel.fontSize = 22;
                tipLabel.alignment = NGUIText.Alignment.Center;
                tipLabel.pivot = UIWidget.Pivot.Center;
                tipLabel.color = Color.white;
                tipLabel.depth = 1;
                // ⛔ 运行期新建的 panel/控件要显式重建一次，否则**不产生 drawcall**
                //   （控件在、逻辑对、屏幕上什么都没有 —— 本项目最难查的那一类假象）。
                panel.RebuildAllDrawCalls();
            }
            if (tipLabel.text != txt)
            {
                tipLabel.text = txt;
                const int h = 40;
                int w = EstimateWidth(txt);
                tipBg.SetDimensions(w, h);
                tipBg.transform.localPosition = new Vector3(w / 2, -h / 2, 0f);
                tipLabel.SetDimensions(w - 12, h - 6);
                tipLabel.transform.localPosition = new Vector3(w / 2, -h / 2, 0f);
            }
            return true;
        }
        catch (Exception e)
        {
            QuickTestTrace.Log("tipedge", "ensure failed " + e.Message);
            return false;
        }
    }

    /// <summary>
    /// 提示底板宽度（像素）。⛔ 不能照抄 <c>viewToggleButton</c> 的 <c>Length * 24</c>：
    /// 那是拉丁字母的估法，而本项目的提示全是中文（一个字符宽 ≈ 一个汉字宽 = 字号），
    /// 按 Length 估会**偏窄** ⇒ 白字顶出黑底。改成逐字累加：全角按字号、其余按 0.55 字号。
    /// </summary>
    static int EstimateWidth(string txt)
    {
        if (string.IsNullOrEmpty(txt))
        {
            return 30;
        }
        const int fontSize = 22;
        float w = 0f;
        for (int i = 0; i < txt.Length; i++)
        {
            w += IsWide(txt[i]) ? fontSize : fontSize * 0.55f;
        }
        return Mathf.RoundToInt(w) + 30;
    }

    /// <summary>是不是全角/宽字符（CJK、假名、全角标点）。</summary>
    static bool IsWide(char c)
    {
        return (c >= 0x1100 && c <= 0x115F)     // 谚文字母
            || (c >= 0x2E80 && c <= 0xA4CF)     // CJK 部首 ~ 注音
            || (c >= 0xAC00 && c <= 0xD7A3)     // 谚文音节
            || (c >= 0xF900 && c <= 0xFAFF)     // CJK 兼容表意
            || (c >= 0xFE30 && c <= 0xFE6F)     // CJK 兼容形式
            || (c >= 0xFF00 && c <= 0xFF60)     // 全角 ASCII
            || (c >= 0xFFE0 && c <= 0xFFE6);
    }

    /// <summary>4×4 白纹理（<b>偶数尺寸是硬要求</b>：1×1 会让 NGUI 的绘制矩形宽高双双归零）。</summary>
    static Texture2D BoxTex()
    {
        if (boxTex == null)
        {
            boxTex = new Texture2D(4, 4, TextureFormat.RGBA32, false);
            Color[] px = new Color[16];
            for (int i = 0; i < px.Length; i++)
            {
                px[i] = Color.white;
            }
            boxTex.SetPixels(px);
            boxTex.Apply();
        }
        return boxTex;
    }

    /// <summary>
    /// 自建面板的「1 局部单位 = 多少世界单位」＝每像素多少世界单位。
    /// 与 <c>viewToggleButton.PanelUnit()</c> 逐字同口径。
    /// </summary>
    float PanelUnit()
    {
        Camera cam = Program.camera_main_2d;
        if (cam == null)
        {
            return 0f;
        }
        Vector3 bl = cam.ScreenToWorldPoint(new Vector3(0f, 0f, 0f));
        Vector3 tr = cam.ScreenToWorldPoint(new Vector3(Screen.width, Screen.height, 0f));
        return Mathf.Abs(tr.y - bl.y) / Mathf.Max(1, Screen.height);
    }
}
