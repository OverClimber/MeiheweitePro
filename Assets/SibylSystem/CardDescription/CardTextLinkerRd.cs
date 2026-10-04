using System;
using System.Collections.Generic;
using System.Text;

/// <summary>
/// RD 简介的括号口径（用户 2026-10-02 需求 4）—— <see cref="CardTextLinker"/> 的 RD 那一半。
///
/// RD 的卡文**不用**「」引用卡名，它用一对全角括号写「限定条件」：
///   自己场上1只表侧表示<b>怪兽（银河族）</b>解放才能发动。
///   选自己墓地1只<b>怪兽（地属性/机械族）</b>。
///   对方把<b>怪兽（攻击力1000以上）</b>召唤时才能发动。
///   对方宣言卡的种类（<b>怪兽·魔法·陷阱</b>）。
/// 括号里那串字就是「什么样的卡」，所以把它做成链接、点一下 = 把这批卡**搜出来**，
/// 正好与「点卡名/字段 → 检索」是同一套手感。
///
/// 三个必须踩准的点（都实测过，见 RdCondition 的类注释）：
///   ① **不是所有括号都是条件**。三张卡表 3473 处括号里 544 处是注释 ——
///      「（限制类效果可在基本分处查看）」「（异画）」「（不能把多余怪兽作为素材）」，
///      做成链接就会点出一个毫无意义的结果。判据是"括号里每个词都得是条件词"，
///      由 <see cref="RdCondition.TryParse"/> 回答。
///   ② 括号**前面**那个词带主类：「怪兽（…）」= 只要怪兽，「怪兽卡（…）」同理；
///      没有前导词时（`（怪兽·魔法·陷阱）`）主类由括号内容自己给。
///   ③ 划线的范围：把紧挨着的 `怪兽` / `怪兽卡` 一起划进去（用户口径写的就是
///      「把「怪兽（…）」做成可点跳转」），更左边的 `表侧表示` / `对方把` 留在外面。
///
/// 另外 RD 卡文首行那颗稀有度角标 <c>&lt;传说卡&gt;</c> 里的**传说卡三个字整体**也做成链接
/// （用户口径：「传说卡三字一起标记」）→ 点一下搜出全部传说卡。
///
/// ⛔ 只在 RD 下扫括号：OCG 卡文里的全角括号是**注释**（`（同名卡的卡名记述）` 之类），
///   按 RD 的条件语法去解析即使大部分解析不出来，也不该冒这个险。
/// </summary>
public static partial class CardTextLinker
{
    /// <summary>RD 条件的 payload 种类前缀：<c>r&lt;主类掩码&gt;:&lt;括号里那串字&gt;</c>。</summary>
    public const char KindRd = 'r';

    /// <summary>RD 条件链接的高亮色（用户 2026-10-03：主类词「怪兽（…）」用**红色**，别用紫）。</summary>
    const string ColorRd = "[FF0000]";

    /// <summary>RD 括号条件富化后的缓存（与 OCG 那套共用同一张表的下标空间，见 Linkify）。</summary>
    public static string LinkifyRd(string text)
    {
        if (string.IsNullOrEmpty(text) || !GameModeManager.IsRD)
        {
            return text;
        }
        StringBuilder sb = new StringBuilder(text.Length + 64);
        int scan = 0;
        while (true)
        {
            // 已经生成好的链接整段原样搬运 —— 里面的显示文字（可能是卡名）不许再被扫一遍，
            // 否则会嵌出 `[url=[url=…]…[/url]]` 这种把 NGUI 标签咬断的东西。
            int link = text.IndexOf("[url=", scan, StringComparison.Ordinal);
            int limit = link < 0 ? text.Length : link;
            AppendPlain(text, scan, limit, sb);
            if (link < 0)
            {
                break;
            }
            int end = text.IndexOf("[/url]", link, StringComparison.Ordinal);
            if (end < 0)
            {
                sb.Append(text, link, text.Length - link);
                break;
            }
            int endClose = end + 6;
            sb.Append(text, link, endClose - link);
            scan = endClose;
        }
        return sb.ToString();
    }

    /// <summary>
    /// 把 <c>[from, to)</c> 这段「没有链接的普通文本」里的括号条件 / 传说卡做成链接，
    /// 结果追加到 <paramref name="sb"/>。
    ///
    /// ⚠ <paramref name="to"/> 是**下一个已有链接的起点**：跨过它的括号不能处理，
    ///   这一段的切分由 <see cref="LinkifyRd"/> 负责。
    /// </summary>
    static void AppendPlain(string text, int from, int to, StringBuilder sb)
    {
        int i = from;
        while (i < to)
        {
            int p = text.IndexOf('（', i);
            int l = text.IndexOf(GameStringHelper.RdLegendWord, i, StringComparison.Ordinal);
            if (p < 0 || p >= to)
            {
                p = -1;
            }
            if (l < 0 || l + GameStringHelper.RdLegendWord.Length > to)
            {
                l = -1;
            }
            if (p < 0 && l < 0)
            {
                break;
            }
            int at;
            bool isParen;
            if (p >= 0 && (l < 0 || p < l))
            {
                at = p;
                isParen = true;
            }
            else
            {
                at = l;
                isParen = false;
            }

            if (!isParen)
            {
                // 传说卡三字整体一个链接（用户口径：整体标记，不是三个字各标一次）。
                sb.Append(text, i, at - i);
                sb.Append(Link(KindRd, "0:" + GameStringHelper.RdLegendWord,
                    ColorRd, GameStringHelper.RdLegendWord));
                i = at + GameStringHelper.RdLegendWord.Length;
                continue;
            }

            int close = text.IndexOf('）', at + 1);
            if (close < 0 || close >= to)
            {
                // 配对不上（或被已有链接截断）：这一个 `（` 当普通字符，往后继续找。
                // ⛔ 必须先把 `[i, at]`（含这个 `（`）搬过去再推进 i —— 只写 `i = at + 1`
                //   会把 `（` 左侧这段还没落盘的普通文本**整段吞掉**（见下面同名注释）。
                sb.Append(text, i, at + 1 - i);
                i = at + 1;
                continue;
            }
            string inner = text.Substring(at + 1, close - at - 1);
            int mainMask = PrecedingMainMask(text, at);
            RdCondition cond;
            if (!RdCondition.TryParse(inner, mainMask, out cond))
            {
                // 不是条件（注释 / 异画 / 说明文）：整对括号原样保留。
                //
                // ⛔⛔ 这里**不能**只写 `i = at + 1` 然后 continue：`AppendPlain` 是「边扫边追加」的，
                //    推进 i 之前没搬的 `[i, at)` 就永远没人搬了（末尾那次 flush 只补 `[i, to)`）。
                //    2026-10-02 实测：`这张卡有（异画）版本。` 被富化成 `异画）版本。` ——
                //    左侧「这张卡有（」凭空消失。**546 处非条件括号在真卡上都会吃掉左侧文字。**
                //    正确做法＝把 `[i, close+1)`（前缀 + 整对括号）一次搬完，再从右括号之后继续。
                sb.Append(text, i, close + 1 - i);
                i = close + 1;
                continue;
            }

            // 把紧挨着的「怪兽」/「怪兽卡」一起划进链接，更左边的词留在外面。
            int start = at;
            if (mainMask != 0)
            {
                if (EndsWith(text, start, "怪兽卡"))
                {
                    start -= 3;
                }
                else if (EndsWith(text, start, "怪兽"))
                {
                    start -= 2;
                }
                if (start < i)
                {
                    start = i;
                }
            }
            if (start > i)
            {
                sb.Append(text, i, start - i);
            }
            // 括号**前面**那截（「怪兽」/「怪兽卡」，主类词）跟着一起高亮；
            // 两头的（）按用户 2026-10-02 第三轮口径：只一起画下划线、**取消高亮**。
            string head = start < at ? text.Substring(start, at - start) : "";
            sb.Append(LinkBracketed(KindRd, mainMask + ":" + inner, ColorRd, "（", inner, "）", head));
            i = close + 1;
        }
        if (i < to)
        {
            sb.Append(text, i, to - i);
        }
    }

    /// <summary>
    /// 括号**前面**紧挨着的词带的主类掩码：`怪兽`/`怪兽卡` → 1（怪兽），其余 → 0（不限）。
    /// 实测 RD 卡文里括号前的写法有 100 多种（`自己场上的表侧表示怪兽` `对方把怪兽`
    /// `只怪兽` …），但**主语一律落在最后那两个/三个字上**，所以只看紧邻的前缀即可。
    /// </summary>
    static int PrecedingMainMask(string text, int at)
    {
        if (EndsWith(text, at, "怪兽卡") || EndsWith(text, at, "怪兽"))
        {
            return 1;
        }
        return 0;
    }

    /// <summary><paramref name="at"/> 之前紧邻的字符是不是 <paramref name="word"/>。</summary>
    static bool EndsWith(string text, int at, string word)
    {
        if (at < word.Length || at > text.Length)
        {
            return false;
        }
        int p = at - word.Length;
        for (int i = 0; i < word.Length; i++)
        {
            if (text[p + i] != word[i])
            {
                return false;
            }
        }
        return true;
    }

    // ------------------------------------------------------------ 验收自检

    /// <summary>
    /// 只在 <c>log/qt_debug.on</c> 存在时写的自检（正式包零开销）。
    ///
    /// 判据全是可离线判读数：把几段**真卡文原句**喂进来，看富化结果里
    /// ① 条件括号有没有变成 <c>[url=r1:…]</c>；② 注释括号有没有**保持原样**；
    /// ③ 传说卡有没有被整体做成链接；④ 带「以外」「区间」「组合」的写法有没有被认出来。
    /// 单靠肉眼在游戏里翻卡验证这几条太贵（要翻到带那种写法的卡），所以在这里一次性钉住。
    /// </summary>
    public static void RdSelfTest()
    {
        if (!QuickTestTrace.Enabled)
        {
            return;
        }
        if (!GameModeManager.IsRD)
        {
            // OCG 下这一整套（括号口径）根本不参与富化，探针跑了也只会全 XX，落盘反而误导。
            QuickTestTrace.Log("rdlink", "skip selftest (not RD)");
            return;
        }
        try
        {
            // ① 无否定、单条件
            Probe("银河族", "从自己卡组上面把3张卡翻开，给双方确认。自己从翻开的卡之中把怪兽（银河族）全部送去墓地。", 1);
            // ② 组合：属性/种族
            Probe("地属性/机械族", "选自己场上1只表侧表示怪兽（地属性/机械族）送去墓地。", 1);
            // ③ 数值：攻击力以下
            Probe("攻击力1000以下", "对方把怪兽（攻击力1000以下）召唤时才能发动。", 1);
            // ④ 否定：以外
            Probe("恶魔族以外", "自己墓地1只怪兽（恶魔族以外）特殊召唤。", 1);
            // ⑤ 等级区间 + 裸数字共享单位
            Probe("7·8星", "选自己场上1只怪兽（7·8星）解放。", 1);
            // ⑥ 主类由括号内容自己给（前面不是怪兽）——括号内三个主类词并集，仍是条件 ⇒ 成链接
            Probe("怪兽·魔法·陷阱", "对方宣言卡的种类（怪兽·魔法·陷阱）。", 1);
            // ⑦ 传说卡整体标记（首行角标）——卡文里那三个字整个一个链接
            Probe("<传说卡>", "RD/SJMP-JP039\t<传说卡>\n【条件】可以给自己场上1只表侧表示怪兽装备。", 1);
            // ⑧ 注释括号必须原样保留（不能变成链接）
            Probe("限制类效果可在基本分处查看", "（限制类效果可在基本分处查看）这张卡很强。", 0);
            Probe("异画", "这张卡有（异画）版本。", 0);
            Probe("不能把多余怪兽作为素材", "仪式术召唤（不能把多余怪兽作为素材）才能特殊召唤。", 0);
            // ⑨ 注释括号**在**条件括号之前 —— 顺序敏感，注释那对不许吃掉左侧文字
            Probe("注释在前+条件在后", "这张卡有（异画）版本。选自己场上1只怪兽（银河族）解放。", 1);
            // ⑩ 注释括号紧贴链接之前
            Probe("注释紧邻条件", "（不能把多余怪兽作为素材）把怪兽（银河族）送去墓地。", 1);
            // ⑪ 整段都是注释括号（一个链接都不该有，文字必须原样）
            Probe("全是注释括号", "（异画）这张卡有（限制类效果可在基本分处查看）才能发动。", 0);
            Inventory();
            // 【2026-10-04 第六轮】RD 池也过一遍「富化不许丢字 + 结构不许缺段」
            // （Inventory 只数链接条数与 textBad，这里把名字块/系列行/收色标记一起点一遍）。
            CardTextLinker.PoolNoDropSelfTest();
        }
        catch (Exception e)
        {
            Program.DEBUGLOG(e);
        }
    }

    /// <summary>
    /// 拿**当前卡池真的卡文**过一遍（只在 qt_debug 下跑），把两类链接的总数落盘。
    ///
    /// 这一条是给离线验收做**跨实现对拍**用的：同一份 RD 卡池，另有一份纯 Python 的
    /// 参考实现（<c>_probe_rdlink_proto.py</c>）算出同样的两个数 ——
    /// 一样的卡池 ⇒ 一样的数；数不一样就说明 C# 这份解析器跟参考实现有出入
    /// （比"在游戏里翻到某张卡肉眼看"要硬得多，也不用去猜该翻哪张）。
    ///
    /// 另外还兜一道**纯文本不变式**（<c>textBad</c>）：富化只许加标记，
    /// 剥掉标记后必须与卡文逐字相同 —— 这是"数链接条数"抓不到的一类缺陷。
    ///
    /// 参照值（rd_standard + rd_alternate + rd_patch，剔除衍生物）：
    ///   pool=3480  rdUrls=3094（条件括号 2929 + 传说卡 165）  textBad=0
    /// </summary>
    static void Inventory()
    {
        try
        {
            List<YGOSharp.Card> pool = YGOSharp.CardsManager.searchAdvanced
                (
                "", -233, -233, -233, -233, -233,
                -233, -233, -233, -233, -233,
                -233, "", -233, null,
                0u, 0u, 0u, 0u, 0u
                );
            int urls = 0;
            int legend = 0;
            // 纯文本不变式（全池兜底）：富化只许加标记，剥掉标记后必须与卡文逐字相同。
            // ⛔ 这是「数链接条数」抓不到的一类缺陷 —— 吃字 / 多字它都照报 OK。
            int textBad = 0;
            string firstBad = "";
            string legendTag = "[url=" + KindRd + "0:" + GameStringHelper.RdLegendWord + "]";
            for (int i = 0; i < pool.Count; i++)
            {
                YGOSharp.Card c = pool[i];
                if (c == null)
                {
                    continue;
                }
                string src = c.Desc;
                string rich = Linkify(src, 0);
                urls += CountOccur(rich, "[url=" + KindRd);
                legend += CountOccur(rich, legendTag);
                string plain = PlainOf(rich);
                if (plain != src)
                {
                    textBad++;
                    if (firstBad.Length == 0)
                    {
                        firstBad = " id=" + c.Id + " in=" + (src ?? "").Length
                            + " out=" + (plain ?? "").Length;
                    }
                }
            }
            QuickTestTrace.Log("rdlink", "inv pool=" + pool.Count
                + " rdUrls=" + urls + " legendUrls=" + legend
                + " parenUrls=" + (urls - legend)
                + " textBad=" + textBad + firstBad);
        }
        catch (Exception e)
        {
            Program.DEBUGLOG(e);
        }
    }

    /// <summary>
    /// 把 <see cref="Link"/> 打上的 NGUI 标记（<c>[url=…]</c>/<c>[/url]</c>/<c>[颜色]</c>/<c>[u]</c>/<c>[-]</c>）
    /// 全部剥掉，还原成纯文本。
    ///
    /// 用途是**纯文本不变式**：富化只许「加标记」，不许增删一个字 ——
    /// 只数链接条数会漏掉「文本被吃掉」这一类（2026-10-02 正是靠这条才发现
    /// 非条件括号会把左侧文字整段吞掉）。
    /// </summary>
    static string PlainOf(string rich)
    {
        if (string.IsNullOrEmpty(rich))
        {
            return rich;
        }
        StringBuilder sb = new StringBuilder(rich.Length);
        int i = 0;
        while (i < rich.Length)
        {
            char c = rich[i];
            if (c == '[')
            {
                if (StartsWith(rich, i, "[/url]")) { i += 6; continue; }
                if (StartsWith(rich, i, "[url="))
                {
                    int e = rich.IndexOf(']', i);
                    if (e > 0) { i = e + 1; continue; }
                }
                if (StartsWith(rich, i, "[u]")) { i += 3; continue; }
                if (StartsWith(rich, i, "[/u]")) { i += 4; continue; }
                if (StartsWith(rich, i, "[-]")) { i += 3; continue; }
                // 六位十六进制颜色标签 [RRGGBB]
                if (i + 7 < rich.Length && rich[i + 7] == ']')
                {
                    bool hex = true;
                    for (int k = 1; k <= 6; k++)
                    {
                        char h = rich[i + k];
                        bool ok = (h >= '0' && h <= '9') || (h >= 'a' && h <= 'f') || (h >= 'A' && h <= 'F');
                        if (!ok) { hex = false; break; }
                    }
                    if (hex) { i += 8; continue; }
                }
            }
            sb.Append(c);
            i++;
        }
        return sb.ToString();
    }

    /// <summary>把一段原文富化一遍并把「链接条数 + 纯文本是否被改动」落盘。</summary>
    static void Probe(string label, string desc, int expectLinks)
    {
        string rich = LinkifyRd(desc);
        int urls = CountOccur(rich, "[url=");
        int rdUrls = CountOccur(rich, "[url=" + KindRd);
        // 纯文本不变式：剥掉标记后必须与原文逐字相同。
        string plain = PlainOf(rich);
        bool textOk = plain == desc;
        QuickTestTrace.Log("rdlink", "probe [" + label + "] urls=" + urls
            + " rd=" + rdUrls + " expect=" + expectLinks
            + (urls == expectLinks ? " OK" : " XX")
            + (textOk ? " text=OK" : " XX text=XX plain="
                + plain.Replace("\n", "\\n").Replace("\t", "\\t"))
            + " out=" + rich.Replace("\n", "\\n").Replace("\t", "\\t"));
    }
}
