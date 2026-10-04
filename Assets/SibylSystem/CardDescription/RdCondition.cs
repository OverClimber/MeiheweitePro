using System;
using System.Collections.Generic;
using YGOSharp;

/// <summary>
/// RD 简介里那对括号 —— <c>怪兽（银河族）</c> / <c>怪兽（地属性/机械族）</c> /
/// <c>怪兽（8星以下）</c> / <c>怪兽（攻击力1000以下）</c> / <c>怪兽（恶魔族以外）</c> ——
/// 的**解析结果 + 命中判据**（用户 2026-10-02 需求 4）。
///
/// ⛔ 为什么单独一个文件、而不是各写一份：这个条件要用在**两个地方** ——
///   * 生成链接的一端（<see cref="CardTextLinker"/>）：要判「这对括号到底是不是一个条件」，
///     不是就不该做成链接（RD 卡文里的括号有一大半是注释，实测 3473 处括号里 544 处是
///     「注：…」「异画」「不能把多余怪兽作为素材」这类）；并算出主类掩码。
///   * 点链接之后检索的一端（<see cref="CardSearchWindow"/>）：要按同一个条件筛真卡。
///   两边各写一份解析，早晚会出现「能点、但点了搜出来的卡不符合括号里的条件」——
///   这正是本工程在种族表 / 种类表上吃过两次亏的那类不一致（见 GameStringHelper 的注释）。
///
/// 口径全部由**实测**定（rd_standard + rd_alternate + rd_patch 三表 3484 张，
/// 括号 3473 处 / 762 个不同内容）：
///   * 分隔符：`/`（含全角 `／`）、`·`（含 `或者`）—— 「银河族/守备力1900或者2600」；
///   * 区间：`3～8星`（全角 `～` 与半角 `~` 都有）；
///   * 裸数字：`攻击力500·1900` / `银河族/守备力1900或者2600` 里那个裸数字**继承上一个字段**
///     （所以要先按 `/` 分组、组内再按 `·`/`或者` 拆，见 <see cref="Parse"/> 的注释）；
///   * `7·8星` 这种：裸数字 + 组内**末尾那个带单位的词**共用单位（`7` 是等级 7）；
///   * 否定：`X以外`（种族 / 属性 / 类型位 / 等级 / 攻击力 / 守备力 都能带）；
///   * `怪兽·魔法·陷阱`：主类**并集**（任一张），不是交集。
///
/// 判据语义与卡组编辑器那套 <c>searchAdvanced</c> 一致，但这里有两点必须自己算：
///   ① 主类位要**取并集**（`(Type & 掩码) == 掩码` 那种「全都要」对 `怪兽·魔法·陷阱` 恒不命中）；
///   ② 「以外」是**排除**语义，`searchAdvanced` 只会做「与掩码有交集」。
///   顺带把等级/攻击力/守备力也留在自己这边 —— 三处都在同一趟里判，省得两边口径分叉。
/// </summary>
public class RdCondition
{
    /// <summary>「这个筛选项没出现」的哨兵值 —— 与 <c>CardsManager.judgeint</c> 同款。</summary>
    public const int Unset = -233;

    /// <summary>主类位的掩码（怪兽 1 / 魔法 2 / 陷阱 4）。</summary>
    const int MainBits = 0x7;

    /// <summary><see cref="LevelBits"/> 的「这不是等级词」哨兵。
    /// ⛔ 不能复用 −1：−1 是「N星以下」的合法返回值（2026-10-02 实证，见 LevelBits 注释）。</summary>
    const int NoLevelWord = -99;

    /// <summary>类型位掩码：主类位取并集、其余位取交集（口径见 <see cref="Matches"/>）。</summary>
    public int typeMask;

    /// <summary>种族掩码：与卡片的 Race 有交集即可（0 = 不限）。</summary>
    public uint raceMask;

    /// <summary>属性掩码：与卡片的 Attribute 有交集即可（0 = 不限）。</summary>
    public uint attributeMask;

    public readonly RdNum level = new RdNum();
    public readonly RdNum atk = new RdNum();
    public readonly RdNum def = new RdNum();

    readonly List<int> exType = new List<int>();
    readonly List<int> exRace = new List<int>();
    readonly List<int> exAttr = new List<int>();

    /// <summary>「传说卡」——卡文里出现这三个字（<c>&lt;传说卡&gt;</c> 是 RD 卡文首行的稀有度标记）。</summary>
    public bool legend;

    /// <summary>一个数值条件：区间 / 精确值集合 / 排除值，三者可叠加。</summary>
    public class RdNum
    {
        /// <summary>这个字段被提到过没有（没提到 = 不过滤）。</summary>
        public bool any;

        /// <summary>闭区间端点（都用 <see cref="Unset"/> 表示"没给"）。</summary>
        public int lo = Unset;

        public int hi = Unset;

        /// <summary>精确值集合（`守备力1900或者2600` 这种 OR）——给了它就以它为准，不看区间。</summary>
        public List<int> vals;

        /// <summary>「以外」排除掉的值。</summary>
        public List<int> ex;

        public void Exact(int v)
        {
            any = true;
            if (vals == null)
            {
                vals = new List<int>();
            }
            vals.Add(v);
        }

        public void Range(int a, int b)
        {
            any = true;
            lo = a;
            hi = b;
        }

        public void Exclude(int v)
        {
            any = true;
            if (ex == null)
            {
                ex = new List<int>();
            }
            ex.Add(v);
        }

        public bool Allows(int raw)
        {
            if (!any)
            {
                return true;
            }
            if (ex != null && ex.Contains(raw))
            {
                return false;
            }
            if (vals != null)
            {
                return vals.Contains(raw);
            }
            if (lo != Unset && hi != Unset)
            {
                return lo <= raw && raw <= hi;
            }
            return true;
        }
    }

    // ------------------------------------------------------------ 解析

    /// <summary>
    /// 括号里的内容 → 条件。<paramref name="mainMask"/> 是括号**前面**那个词带的主类
    /// （「怪兽（…）」→ 1 / 「怪兽卡（…）」→ 1，没有 → 0）。
    /// 返回 false = 这不是一个条件（注释 / 异画 / 说明文），**不该做成链接**。
    ///
    /// 判据是「**每一个**分段都认得出来」——只要有一个词解析不了就整体放弃。
    /// 这一条把注释挡在外面非常好用：「不能把多余怪兽作为素材」「限制类效果可在基本分处查看」
    /// 里的词全都不是条件词，一个都认不出来。
    /// </summary>
    public static bool TryParse(string qualifier, int mainMask, out RdCondition c)
    {
        c = null;
        if (string.IsNullOrEmpty(qualifier) || qualifier.Length > 48)
        {
            return false;
        }
        RdCondition r = new RdCondition();
        r.typeMask = mainMask & MainBits;
        int atoms = 0;

        string[] groups = qualifier.Split(new char[] { '/', '／' });
        for (int g = 0; g < groups.Length; g++)
        {
            string grp = (groups[g] ?? "").Trim();
            if (grp.Length == 0)
            {
                return false;
            }
            // 「或者」先归一成 `·`，组内再按 `·` 拆 ——
            // 实测这两种写法在同一张卡里混着用：「银河族/守备力1900或者2600」。
            grp = grp.Replace("或者", "·");
            string[] parts = grp.Split(new char[] { '·' });
            // 组内**末尾**那个词的「单位」会给前面的裸数字兜底：`7·8星` = 7星 或 8星。
            // （不能只看最后一段有没有单位就套给全部 —— 但实测里带单位的写法都在末尾。）
            int tailBits = parts.Length > 0 ? LevelBits(parts[parts.Length - 1]) : NoLevelWord;

            int prevField = 0;   // 0 无 / 1 lv / 2 atk / 3 def
            for (int i = 0; i < parts.Length; i++)
            {
                string tk = (parts[i] ?? "").Trim();
                if (tk.Length == 0)
                {
                    return false;
                }
                // 裸数字：组内还有别的段 + 末尾带单位 ⇒ 按末尾那个单位算（`7·8星`）。
                if (i < parts.Length - 1 && tailBits != NoLevelWord && IsAllDigits(tk))
                {
                    r.PutLevel(int.Parse(tk), tailBits, false);
                    prevField = 1;
                    atoms++;
                    continue;
                }
                int field = r.Atom(tk, prevField);
                if (field < 0)
                {
                    return false;
                }
                prevField = field;
                atoms++;
            }
        }
        if (atoms == 0)
        {
            return false;
        }
        c = r;
        return true;
    }

    /// <summary>单个词 → 条件（返回它落在哪个字段；-1 = 认不出来）。</summary>
    int Atom(string tk, int prevField)
    {
        bool neg = false;
        if (tk.EndsWith("以外", StringComparison.Ordinal))
        {
            neg = true;
            tk = tk.Substring(0, tk.Length - 2);
        }
        if (tk.Length == 0)
        {
            return -1;
        }

        // 等级：`3～8星` / `8星` / `8星以上` / `8星以下`
        int dash = IndexOfLevelDash(tk);
        if (dash > 0 && tk.EndsWith("星", StringComparison.Ordinal))
        {
            string a = tk.Substring(0, dash);
            string b = tk.Substring(dash + 1, tk.Length - dash - 2);
            if (IsAllDigits(a) && IsAllDigits(b))
            {
                if (neg)
                {
                    // `3～8星以外` 实测没有；真出现就两边都排除，语义仍然对。
                    level.Exclude(int.Parse(a));
                    level.Exclude(int.Parse(b));
                }
                else
                {
                    level.Range(int.Parse(a), int.Parse(b));
                }
                return 1;
            }
            return -1;
        }
        int lvBits = LevelBits(tk);
        if (lvBits != NoLevelWord)
        {
            PutLevel(int.Parse(CutSuffix(tk)), lvBits, neg);
            return 1;
        }

        // 攻击力 / 守备力：`攻击力1000以下` / `守备力1900` / `攻击力500以外`
        if (tk.StartsWith("攻击力", StringComparison.Ordinal) || tk.StartsWith("守备力", StringComparison.Ordinal))
        {
            bool isAtk = tk[0] == '攻';
            string rest = tk.Substring(3);
            int dir = 0;
            if (rest.EndsWith("以上", StringComparison.Ordinal))
            {
                dir = 1;
                rest = rest.Substring(0, rest.Length - 2);
            }
            else if (rest.EndsWith("以下", StringComparison.Ordinal))
            {
                dir = -1;
                rest = rest.Substring(0, rest.Length - 2);
            }
            if (!IsAllDigits(rest))
            {
                return -1;
            }
            int v = int.Parse(rest);
            RdNum f = isAtk ? atk : def;
            if (neg)
            {
                f.Exclude(v);
            }
            else if (dir > 0)
            {
                f.Range(v, 99999);
            }
            else if (dir < 0)
            {
                f.Range(0, v);
            }
            else
            {
                f.Exact(v);
            }
            return isAtk ? 2 : 3;
        }

        // 属性 / 种族（`族` 必须比 `属性` 后判 —— 「幻想魔族」不含「属性」，但顺序写死更省心）
        if (tk.EndsWith("属性", StringComparison.Ordinal))
        {
            int b = GameStringHelper.attributeBitOf(tk);
            if (b < 0)
            {
                return -1;
            }
            if (neg)
            {
                exAttr.Add(b);
            }
            else
            {
                attributeMask |= 1u << b;
            }
            return 4;
        }
        if (tk.EndsWith("族", StringComparison.Ordinal))
        {
            int b = GameStringHelper.raceBitOf(tk);
            if (b < 0)
            {
                return -1;
            }
            if (neg)
            {
                exRace.Add(b);
            }
            else
            {
                raceMask |= 1u << b;
            }
            return 5;
        }

        // 主类（`怪兽·魔法·陷阱` 里那三个词）
        if (tk == "怪兽")
        {
            if (neg)
            {
                return -1;
            }
            typeMask |= 1;
            return 6;
        }
        if (tk == "魔法")
        {
            if (neg)
            {
                return -1;
            }
            typeMask |= 2;
            return 6;
        }
        if (tk == "陷阱")
        {
            if (neg)
            {
                return -1;
            }
            typeMask |= 4;
            return 6;
        }

        // 传说卡：卡文里那三个字（**不是**类型位 —— 实测 121 张传说卡里只有 2 张的
        // type 带了 bit3，靠位掩码会漏掉 119 张，见 GameStringHelper.RdLegendWord）。
        if (tk == GameStringHelper.RdLegendWord || tk == "传说")
        {
            if (neg)
            {
                return -1;
            }
            legend = true;
            return 7;
        }

        // 其余类型位（实测 RD 卡文里会作为条件的）
        int bit = TypeBitOf(tk);
        if (bit >= 0)
        {
            if (neg)
            {
                exType.Add(bit);
            }
            else
            {
                typeMask |= 1 << bit;
            }
            return 8;
        }

        // 组内其余的裸数字：**继承上一个字段**（`攻击力500·1900` 的 1900 还是攻击力）。
        // ⚠ 只有在组内前面已经出现过字段时才认，否则 `7` 这种无主数字会变成「什么都不过滤」。
        if (IsAllDigits(tk) && prevField >= 1)
        {
            int v = int.Parse(tk);
            if (prevField == 1)
            {
                if (neg) { level.Exclude(v); } else { level.Exact(v); }
            }
            else if (prevField == 2)
            {
                if (neg) { atk.Exclude(v); } else { atk.Exact(v); }
            }
            else
            {
                if (neg) { def.Exclude(v); } else { def.Exact(v); }
            }
            return prevField;
        }
        return -1;
    }

    void PutLevel(int v, int bits, bool neg)
    {
        if (neg)
        {
            level.Exclude(v);
            return;
        }
        if (bits == 0)
        {
            level.Exact(v);
        }
        else if (bits > 0)
        {
            level.Range(v, 99999);
        }
        else
        {
            level.Range(0, v);
        }
    }

    /// <summary>`N星` → 0 恰好 / `N星以上` → +1 / `N星以下` → −1；
    /// 不是这种写法返回 <see cref="NoLevelWord"/>。
    /// ⛔ 这里**绝不能**用 −1 兼表「不是等级词」：−1 本身是「N星以下」的合法返回值，
    /// 一旦复用，调用处的 `>= 0` 判据会把整类 `N星以下` 当成非等级词拒掉
    /// （2026-10-02 实证：3480 张池里整整少 400 条括号链接、`8星以下` 检索得 0）。</summary>
    static int LevelBits(string tk)
    {
        if (tk.EndsWith("星以上", StringComparison.Ordinal) && IsAllDigits(Cut(tk, 3)))
        {
            return 1;
        }
        if (tk.EndsWith("星以下", StringComparison.Ordinal) && IsAllDigits(Cut(tk, 3)))
        {
            return -1;
        }
        if (tk.EndsWith("星", StringComparison.Ordinal) && IsAllDigits(Cut(tk, 1)))
        {
            return 0;
        }
        return NoLevelWord;
    }

    /// <summary>去掉 `星以上`/`星以下`/`星` 尾巴（只在 <see cref="LevelBits"/> 已判定命中后调）。</summary>
    static string CutSuffix(string tk)
    {
        if (tk.EndsWith("星以上", StringComparison.Ordinal) || tk.EndsWith("星以下", StringComparison.Ordinal))
        {
            return tk.Substring(0, tk.Length - 3);
        }
        return tk.Substring(0, tk.Length - 1);
    }

    static string Cut(string tk, int n)
    {
        return tk.Length > n ? tk.Substring(0, tk.Length - n) : "";
    }

    static int IndexOfLevelDash(string tk)
    {
        int i = tk.IndexOf('～');
        if (i < 0)
        {
            i = tk.IndexOf('~');
        }
        return i;
    }

    static bool IsAllDigits(string s)
    {
        if (string.IsNullOrEmpty(s))
        {
            return false;
        }
        for (int i = 0; i < s.Length; i++)
        {
            if (!char.IsDigit(s[i]))
            {
                return false;
            }
        }
        return true;
    }

    /// <summary>类型位（与 <c>GameStringHelper.typeName</c> 的位序同口径，只列 RD 卡文里会出现的）。</summary>
    static int TypeBitOf(string tk)
    {
        if (tk == "通常") { return 4; }
        if (tk == "效果") { return 5; }
        if (tk == "仪式") { return 6; }
        if (tk == "融合") { return 7; }
        if (tk == "灵摆") { return 10; }
        if (tk == "极大") { return 15; }
        return -1;
    }

    // ------------------------------------------------------------ 判据

    /// <summary>这张卡符不符合本条件。</summary>
    public bool Matches(Card card)
    {
        if (card == null)
        {
            return false;
        }
        uint t = (uint)card.Type;
        int main = typeMask & MainBits;
        if (main != 0 && (t & (uint)main) == 0)
        {
            return false;
        }
        int sub = typeMask & ~MainBits;
        if (sub != 0 && (t & (uint)sub) != (uint)sub)
        {
            return false;
        }
        for (int i = 0; i < exType.Count; i++)
        {
            if ((t & (1u << exType[i])) != 0)
            {
                return false;
            }
        }
        // ⚠ 一律先转 uint：Race 是 sqlite 的 int32，RD 的「电子人」用的是 0x80000000，
        //   带符号参与运算会被符号扩展成 0xFFFFFFFF80000000，bit31 永远命不中。
        uint race = (uint)card.Race;
        uint attr = (uint)card.Attribute;
        if (raceMask != 0 && (race & raceMask) == 0)
        {
            return false;
        }
        if (attributeMask != 0 && (attr & attributeMask) == 0)
        {
            return false;
        }
        for (int i = 0; i < exRace.Count; i++)
        {
            if ((race & (1u << exRace[i])) != 0)
            {
                return false;
            }
        }
        for (int i = 0; i < exAttr.Count; i++)
        {
            if ((attr & (1u << exAttr[i])) != 0)
            {
                return false;
            }
        }
        if (!level.Allows(card.Level) || !atk.Allows(card.Attack) || !def.Allows(card.Defense))
        {
            return false;
        }
        if (legend && !GameStringHelper.isLegendCard(card))
        {
            return false;
        }
        return true;
    }

    /// <summary>给人看的短描述（落盘给离线验收判读用）。</summary>
    public string Describe()
    {
        return "typeMask=" + typeMask
            + " race=" + raceMask
            + " attr=" + attributeMask
            + " lv=" + V(level)
            + " atk=" + V(atk)
            + " def=" + V(def)
            + " legend=" + (legend ? 1 : 0)
            + " ex(t/r/a)=" + exType.Count + "/" + exRace.Count + "/" + exAttr.Count;
    }

    static string V(RdNum n)
    {
        if (!n.any)
        {
            return "-";
        }
        List<string> s = new List<string>();
        if (n.vals != null)
        {
            for (int i = 0; i < n.vals.Count; i++)
            {
                s.Add(n.vals[i].ToString());
            }
        }
        if (n.lo != Unset && n.hi != Unset)
        {
            s.Add(n.lo + ".." + n.hi);
        }
        if (n.ex != null)
        {
            for (int i = 0; i < n.ex.Count; i++)
            {
                s.Add("!" + n.ex[i]);
            }
        }
        return string.Join("|", s.ToArray());
    }
}
