using System;
using System.Text;
using UnityEngine;

/// <summary>
/// 简介链接的**悬停提亮**（用户 2026-10-04：「可以点击的这些文字在鼠标移上去后
/// 都会有亮度变化提示玩家」）。挂在简介的 <c>UILabel</c> 上 —— 与
/// <see cref="CardLinkClick"/> 同一个对象、同一套命中口径。
///
/// <para><b>原理</b>：悬停时把鼠标下那个 <c>[url=…][/url]</c> 区间里的**颜色标签**
/// 整体换亮（ toward 白 45%），再把这段文本写回 label。换出来的串**与原文等长**
/// （<c>[RRGGBB]</c> → <c>[RRGGBB]</c>，永远 6 位十六进制）⇒
/// <see cref="CardTextLinker.ResolveClick"/> 依赖的「原文下标 = processedText 下标」
/// 对齐关系一个字符都没动，点击/命中/顺序号全部照旧。</para>
///
/// <para><b>为什么要提亮而不是加下划线</b>：链接本身已经带下划线
/// （<see cref="CardTextLinker"/> 的 <c>Link</c>/<c>LinkBracketed</c> 都在 <c>[u]</c> 里），
/// 再叠一层就没有"多出来的信号"了；而**整段变亮**是玩家一眼能认出的"这个可以点"。
/// 提亮的颜色标签在 <c>[u]</c> **之前**，所以下划线也跟着一起变亮 —— 整个链接一体反馈。</para>
///
/// <para><b>性能</b>：命中检测（<c>GetCharacterIndexAtPosition</c>）只在**鼠标动了**或
/// **文本被重建了**（滚动 / 换卡 / 译名刷新）时跑一次；鼠标不动就一拍都不算。</para>
///
/// <para><b>与 UITextList 的共存</b>：简介是 <c>UITextList</c>，它会在滚动 / 改宽时
/// 重写 <c>textLabel.text</c> —— 那一刻提亮版被冲掉，本组件把它当作"新的底稿"重新提亮
/// （鼠标若还悬在原链接上，下一拍就恢复提亮，肉眼无感）；移开时把**底稿**原样写回。</para>
/// </summary>
public class CardLinkHover : MonoBehaviour
{
    /// <summary>向白提亮的比例。0.45 ≈ 一眼可见、又不至于把深色链接顶成白字。</summary>
    const float Brighten = 0.45f;

    UILabel label;
    /// <summary>未提亮的当前底稿（滚动/换卡重建后从这里重新出发）。</summary>
    string baseText;
    /// <summary>已经写进 label 的串（== label.text 时 = 状态是本组件写的）。</summary>
    string appliedText;
    /// <summary>当前提亮的是第几个链接（-1 = 没有）。</summary>
    int hotOrdinal = -1;

    Vector3 lastMouse;
    bool hasMouse;
    bool mouseDirty = true;

    void OnDestroy()
    {
        // 别把提亮版留在 label 上（组件被拆时文本还得是干净的）。
        Restore();
    }

    void Update()
    {
        try
        {
            RunTick(UICamera.lastWorldPosition, false);
        }
        catch (Exception e)
        {
            Program.DEBUGLOG(e);
        }
    }

    /// <summary>
    /// 验收探针：在 <paramref name="worldPos"/> 处**强制**跑一拍检测（合成鼠标事件
    /// 送不进这个客户端，见 notes/harness.md，所以悬停只能进程内直喂坐标）。
    /// 返回跑完后的 label 文本（提亮版 / 原文），失败返回 null。
    /// ⛔ 探针路径**绕过** <see cref="Hovering"/> —— 那一格查的是真实
    /// <c>UICamera.hoveredObject</c>，进程内没法伪造，与「喂坐标」这步是两回事。
    /// </summary>
    public string ProbeTick(Vector3 worldPos)
    {
        try
        {
            if (label == null)
            {
                label = GetComponent<UILabel>();
            }
            if (label == null)
            {
                return null;
            }
            RunTick(worldPos, true);
            return label.text;
        }
        catch (Exception e)
        {
            QuickTestTrace.Log("serieslink", "probetick failed " + e.Message);
            return null;
        }
    }

    /// <summary>验收探针：强制取消提亮。返回还原后的 label 文本。</summary>
    public string ProbeRestore()
    {
        Restore();
        return label != null ? label.text : null;
    }

    void RunTick(Vector3 m, bool forced)
    {
        if (label == null)
        {
            label = GetComponent<UILabel>();
            if (label == null)
            {
                enabled = false;
                return;
            }
        }

        // 文本被别人重建了（滚动 / 换卡 / 译名刷新）⇒ 换底稿 + 强制重新判一次鼠标。
        string cur = label.text;
        if (cur != appliedText)
        {
            baseText = cur;
            appliedText = cur;
            mouseDirty = true;
        }

        if (!forced && hasMouse && !mouseDirty
            && (m - lastMouse).sqrMagnitude <= 1e-6f)
        {
            return;   // 鼠标没动、文本没变 ⇒ 悬停对象不可能变，一拍都不算
        }
        hasMouse = true;
        lastMouse = m;
        mouseDirty = false;

        // ⛔ 三目运算符的假分支不赋值 ⇒ out 参数会报 CS0165，写成 if/else。
        int ordinal = -1;
        string payload = null;
        if (forced || Hovering())
        {
            payload = CardTextLinker.ResolveClick(label, m, out ordinal);
        }
        if (string.IsNullOrEmpty(payload) || ordinal < 0)
        {
            Restore();
            return;
        }
        if (ordinal == hotOrdinal)
        {
            return;   // 已经提亮的就是它
        }
        string want = Highlighted(baseText, ordinal);
        if (want == null)
        {
            return;   // 区间没找着（不该发生）：保持原样，别把文本写坏
        }
        hotOrdinal = ordinal;
        label.text = want;
        appliedText = want;
    }

    /// <summary>取消提亮，把底稿原样写回（只在本组件"还握着文本"时才写）。</summary>
    void Restore()
    {
        if (hotOrdinal < 0)
        {
            return;
        }
        hotOrdinal = -1;
        if (label != null && baseText != null && label.text == appliedText)
        {
            label.text = baseText;
        }
        appliedText = baseText;
    }

    /// <summary>鼠标现在是不是悬在本对象（或它的子物体）上。</summary>
    bool Hovering()
    {
        GameObject h = UICamera.hoveredObject;
        if (h == null)
        {
            return false;
        }
        Transform t = h.transform;
        while (t != null)
        {
            if (t.gameObject == gameObject)
            {
                return true;
            }
            t = t.parent;
        }
        return false;
    }

    /// <summary>
    /// 把 <paramref name="raw"/> 里第 <paramref name="ordinal"/> 个（0 起算）
    /// <c>[url=…][/url]</c> 区间内的颜色标签整体提亮；其余字节原样。
    /// 找不到该区间返回 null。
    /// </summary>
    static string Highlighted(string raw, int ordinal)
    {
        if (string.IsNullOrEmpty(raw) || ordinal < 0)
        {
            return null;
        }
        int from = 0;
        int nth = 0;
        while (true)
        {
            int start = raw.IndexOf("[url=", from, StringComparison.Ordinal);
            if (start < 0)
            {
                return null;
            }
            int tagEnd = raw.IndexOf(']', start + 5);
            if (tagEnd < 0)
            {
                return null;
            }
            int close = raw.IndexOf("[/url]", tagEnd, StringComparison.Ordinal);
            if (close < 0)
            {
                return null;
            }
            if (nth == ordinal)
            {
                return BrightenRange(raw, tagEnd + 1, close);
            }
            from = tagEnd + 1;
            nth++;
        }
    }

    /// <summary>
    /// 区间内每个 <c>[RRGGBB]</c> 都提亮（<b>等长替换</b>，见类注释 —— 下标对齐是命中的命根）。
    /// <c>[-]</c>/下划线/括号这些结构标签一概不动。
    /// </summary>
    static string BrightenRange(string raw, int from, int to)
    {
        StringBuilder sb = new StringBuilder(raw.Length);
        sb.Append(raw, 0, from);
        int i = from;
        while (i < to)
        {
            if (raw[i] == '[' && i + 7 < to && raw[i + 7] == ']' && IsHex6(raw, i + 1))
            {
                sb.Append('[').Append(BrightHex(raw, i + 1)).Append(']');
                i += 8;
            }
            else
            {
                sb.Append(raw[i]);
                i++;
            }
        }
        sb.Append(raw, to, raw.Length - to);
        return sb.ToString();
    }

    static bool IsHex6(string s, int at)
    {
        for (int i = 0; i < 6; i++)
        {
            char c = s[at + i];
            bool hex = (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f')
                || (c >= 'A' && c <= 'F');
            if (!hex)
            {
                return false;
            }
        }
        return true;
    }

    /// <summary>把 <paramref name="at"/> 起的 6 位十六进制颜色向白提亮，返回 6 位结果。</summary>
    static string BrightHex(string s, int at)
    {
        char[] outp = new char[6];
        for (int k = 0; k < 3; k++)
        {
            int v = HexVal(s[at + k * 2]) * 16 + HexVal(s[at + k * 2 + 1]);
            int b = v + (int)((255 - v) * Brighten);
            if (b > 255)
            {
                b = 255;
            }
            outp[k * 2] = HexDigit(b / 16);
            outp[k * 2 + 1] = HexDigit(b % 16);
        }
        return new string(outp);
    }

    static int HexVal(char c)
    {
        if (c >= '0' && c <= '9')
        {
            return c - '0';
        }
        if (c >= 'a' && c <= 'f')
        {
            return c - 'a' + 10;
        }
        if (c >= 'A' && c <= 'F')
        {
            return c - 'A' + 10;
        }
        return 0;
    }

    static char HexDigit(int v)
    {
        return v < 10 ? (char)('0' + v) : (char)('A' + v - 10);
    }
}
