using System;
using System.Collections.Generic;
using YGOSharp;
using YGOSharp.OCGWrapper.Enums;

public class GameStringHelper
{
    public static string fen = "/";
    public static string xilie = "";
    public static string opHint = "";
    public static string licechuwai = "";  
    public static string biaoceewai = "";
    public static string teshuzhaohuan = "";
    public static string yijingqueren = "";

    public static string _zhukazu = "";
    public static string _fukazu = "";
    public static string _ewaikazu = "";
    public static string _guaishou = "";
    public static string _mofa = "";
    public static string _xianjing = "";
    public static string _ronghe = "";
    public static string _lianjie = "";
    public static string _tongtiao = "";
    public static string _chaoliang = "";

    public static string _wofang = "";
    public static string _duifang = "";

    public static string kazu = "";
    public static string mudi = "";
    public static string chuwai = "";
    public static string ewai = "";
    public static string SemiNomi = "";

    public static bool differ(long a, long b)
    {
        bool r = false;
        if ((a & b) > 0) r = true;
        return r;
    }

    public static string attribute(long a)
    {
        string r = "";
        bool passFirst = false;
        for (int i = 0; i < 7; i++)
        {
            if ((a & (1 << i)) > 0)
            {
                if (passFirst)
                {
                    r += fen;
                }
                r += GameStringManager.get_unsafe(1010 + i);
                passFirst = true;
            }
        }
        return r;
    }

    public static string race(long a)
    {
        // 位掩码先截成 32 位：卡表里 Race 是 sqlite 的 **int32**，RD 的「电子人」用的正是
        // 最高位 0x80000000，读出来是负数，直接参与运算会被**符号扩展**成
        // 0xFFFFFFFF80000000 —— 那样 bit31 永远命不中（而其它的高位又不会误报，症状很隐蔽：
        // 只有「电子人」这一类显示为空）。截成 32 位就干净了。
        long mask = a & 0xFFFFFFFFL;
        string r = "";
        bool passFirst = false;
        for (int i = 0; i < RaceCount; i++)
        {
            // ⚠ 必须是 `1L << i`：`1 << 31` 在 C# 里是 int.MinValue，转 long 会符号扩展到
            //   0xFFFFFFFF80000000，AND 出来恒为 0 —— bit31（电子人）会被静默吃掉。
            if ((mask & (1L << i)) == 0)
            {
                continue;
            }
            string name = raceName(i);
            if (name.Length == 0)
            {
                continue;
            }
            if (passFirst)
            {
                r += fen;
            }
            r += name;
            passFirst = true;
        }
        return r;
    }

    // ============================ 种族表（含 RD 新种族） ============================
    //
    // RD 在 OCG 的 26 个种族（bit0..25）之后**接排了 6 个新种族**，位值是 bit26..31。
    // 实测 rd/cdb 的三份卡表（_probe_rdrace.py）：共 252 张卡用到，分布是
    //   银河 201 / 天界战士 16 / 多头龙 11 / 电子人 10 / 魔导骑士 9 / 欧米茄念动力 5
    // 位值取 RD 规则库自己的口径（RDBase.lua 的 RACE_* 常量），不是我们发明的：
    //   0x04000000 魔导骑士 · 0x08000000 多头龙 · 0x10000000 欧米茄念动力
    //   0x20000000 天界战士 · 0x40000000 银河   · 0x80000000 电子人
    //
    // 文案**写死在代码里**而不是补进 config/strings.conf，理由有两条：
    //   ① strings.conf 走在线更新（koishipro/content 三件套之一，ETag == 文件 MD5），
    //      远端那份没有这几条 ⇒ 补进去的条目会被更新悄悄冲掉，
    //      而症状是「某天开始种族名变空白」，很难往更新上想；
    //   ② 这几个种族**只有 RD 卡会用**，写完就固定，没有本地化/热更需求。
    // 但如果哪天 strings.conf 里真出现了 1046..1051，以 conf 的为准（下面的取值顺序如此）。

    /// <summary>RD 新种族：位序（bit26..31）→ 中文名。</summary>
    private static readonly string[] RdRaceNames =
    {
        "魔导骑士",      // bit26 0x04000000
        "多头龙",        // bit27 0x08000000
        "欧米茄念动力",  // bit28 0x10000000
        "天界战士",      // bit29 0x20000000
        "银河",          // bit30 0x40000000
        "电子人",        // bit31 0x80000000
    };

    private const int RaceBitCountOCG = 26;

    /// <summary>当前模式该认多少个种族位（OCG 26 个；RD 多 6 个 = 32）。</summary>
    public static int RaceCount
    {
        get { return GameModeManager.IsRD ? RaceBitCountOCG + RdRaceNames.Length : RaceBitCountOCG; }
    }

    /// <summary>
    /// 第 bit 个种族位的文案（0 = 战士 … 25 = 幻想魔；RD 另有 26..31）。
    /// 找不到就返回空串 —— 调用方靠「空串 = 跳过一个位」来容错，别改成返回 "?"。
    /// </summary>
    public static string raceName(int bit)
    {
        if (bit < 0 || bit >= RaceCount)
        {
            return "";
        }
        // ⚠ RD 的 6 个新种族**绝不能去查 strings.conf**（哪怕查到了也要丢掉）。
        // 老惯例是「种族 = 1020 + 位」，照算 bit30/31 → 1050/1051，而这两个号在文案表里
        // 是**类型**段的「怪兽 / 魔法」：
        //     bit30 (银河)   → conf 1050 → "怪兽"   ← 会印成「怪兽」而不是「银河」
        //     bit31 (电子人) → conf 1051 → "魔法"   ← 会印成「魔法」而不是「电子人」
        // 症状特别阴：不是空白（空白一眼可疑），而是「看着像正常文案」的错值。
        // 参照实现也印证了这点 —— _rdclient/KoishiPro/strings.conf 里 1020..1045 之后
        // 直接跳到 1050（类型段），**根本没有 RD 新种族的词条**，6 个名字只写在
        // script/RDBase.lua 的常量注释里 ⇒ 只能由代码持有（RdRaceNames）。
        int rd = bit - RaceBitCountOCG;
        if (rd >= 0)
        {
            return (rd < RdRaceNames.Length) ? RdRaceNames[rd] : "";
        }
        return GameStringManager.get_unsafe(1020 + bit);
    }

    public static string zone(long data)
    {
        List<string> strs = new List<string>();
        for (long filter = 0x1L; filter <= (0x1L << 32); filter <<= 1)
        {
            string str = "";
            long s = filter & data;
            if (s != 0)
            {
                if ((s & 0x60) != 0)
                {
                    str += GameStringManager.get_unsafe(1081);
                    data &= ~0x600000;
                }
                else if ((s & 0xffff) != 0)
                    str += GameStringManager.get_unsafe(102);
                else if ((s & 0xffff0000) != 0)
                {
                    str += GameStringManager.get_unsafe(103);
                    s >>= 16;
                }
                if ((s & 0x1f) != 0)
                    str += GameStringManager.get_unsafe(1002);
                else if ((s & 0xff00) != 0)
                {
                    s >>= 8;
                    if ((s & 0x1f) != 0)
                        str += GameStringManager.get_unsafe(1003);
                    else if ((s & 0x20) != 0)
                        str += GameStringManager.get_unsafe(1008);
                    else if ((s & 0xc0) != 0)
                        str += GameStringManager.get_unsafe(1009);
                }
                int seq = 1;
                for (int i = 0x1; i < 0x100; i <<= 1)
                {
                    if ((s & i) != 0)
                        break;
                    ++seq;
                }
                str += "(" + seq.ToString() + ")";
                strs.Add(str);
            }
        }
        return String.Join(", ", strs.ToArray());
    }

    public static string mainType(long a)
    {
        string r = "";
        bool passFirst = false;
        for (int i = 0; i < 3; i++)
        {
            if ((a & (1 << i)) > 0)
            {
                if (passFirst)
                {
                    r += fen;
                }
                r += GameStringManager.get_unsafe(1050 + i);
                passFirst = true;
            }
        }
        return r;
    }

    public static string secondType(long a)
    {
        string r = "";
        bool passFirst = false;
        // RD 的卡表多用了两个原本空着的位（实测 _probe_rdrace.py）：
        //   bit3  (0x8)    = 传说卡（121 张）—— OCG 不认这个位，所以老代码从 bit4 起扫，
        //                              文案表里 1053 是「？？？」；
        //   bit15 (0x8000) = 极大怪兽（92 张）—— 文案表里 1065 是「？？？」，
        //                              直接在 RD 卡上印出「？？？」显然不对。
        // 两条都只在 RD 下生效：OCG 的位5起扫、文案照旧走 strings.conf，
        // 老卡面一个字都不变（改通用路径必须连带跑 OCG 回归）。
        int firstBit = GameModeManager.IsRD ? 3 : 4;
        for (int i = firstBit; i < 27; i++)
        {
            if ((a & (1L << i)) == 0)
            {
                continue;
            }
            string name = typeName(i);
            if (name.Length == 0)
            {
                continue;
            }
            if (passFirst)
            {
                r += fen;
            }
            r += name;
            passFirst = true;
        }
        if (r == "")
        {
            r += GameStringManager.get_unsafe(1054);
        }
        return r;
    }

    /// <summary>
    /// 第 bit 个类型位的文案（0 = 怪兽、1 = 魔法、2 = 陷阱、3 = 传说卡(RD)、4 = 通常…）。
    /// RD 专有的两个位（传说卡 / 极大）写死在代码里，理由同 <see cref="RdRaceNames"/>。
    /// </summary>
    public static string typeName(int bit)
    {
        if (GameModeManager.IsRD)
        {
            if (bit == 3)
            {
                string legend = GameStringManager.get_unsafe(1053);
                return (!string.IsNullOrEmpty(legend) && legend != "？？？") ? legend : "传说卡";
            }
            if (bit == 15)
            {
                string maximum = GameStringManager.get_unsafe(1065);
                return (!string.IsNullOrEmpty(maximum) && maximum != "？？？") ? maximum : "极大";
            }
        }
        return GameStringManager.get_unsafe(1050 + bit);
    }

    // ============================ 「种类」二级位表（卡组编辑器的筛选下拉） ============================
    //
    // 卡组编辑器「高级搜索」里主类（怪兽/魔法/陷阱）下面那一档就是「种类」。
    // 它的**候选项**与**反向映射**必须读同一张表 —— 这是种族表那套纪律的翻版
    // （见上面 raceName 的注释）：两边各写一份，早晚会出现「下拉里有、勾了搜不到」。
    //
    // 表里的数是**位序**，与 <see cref="typeName"/> 同口径；`-1` 表示「通常」那一档 ——
    // 魔法/陷阱的通常卡没有二级位，掩码就是主类本身。

    /// <summary>主类下标：0 = 怪兽、1 = 魔法、2 = 陷阱（= conf 1312/1313/1314 的顺序）。</summary>
    public const int SecondTypeMainMonster = 0;
    public const int SecondTypeMainSpell = 1;
    public const int SecondTypeMainTrap = 2;

    private const int SecondTypeMainCount = 3;

    /// <summary>
    /// OCG 的老清单，**顺序沿用 YGOPro2**（改造前后逐项一致，一个字不动）。
    /// 分成三组是因为同一档位在不同主类下含义不同：例如 bit7 在怪兽里是「仪式怪兽」、
    /// 在魔法里是「仪式魔法」；bit17 在魔法里是「永续」、在陷阱里也是「永续」。
    /// </summary>
    private static readonly int[][] SecondTypeBitsOCG =
    {
        new int[] { 4, 5, 6, 7, 13, 23, 12, 11, 10, 9, 21, 22, 24, 25, 26 }, // 怪兽
        new int[] { -1, 16, 17, 7, 18, 19 },                                 // 魔法
        new int[] { -1, 17, 20 },                                            // 陷阱
    };

    /// <summary>
    /// RD 比 OCG 多出来的两个种类位（OCG 的位表里根本没有这两档）。
    /// 15 = 极大、3 = 传说卡，位值口径见 <see cref="typeName"/>；
    /// 只在池子里真有卡时才追加（实测 rd/cdb 三表：极大 92 张、传说卡 77+38+17 张）。
    /// </summary>
    private static readonly int[] RdExtraSecondTypeBits = { 15, 3 };

    /// <summary>主类下标 → 主类掩码（CardType.Monster / Spell / Trap）。</summary>
    public static uint secondTypeMainMask(int mainIndex)
    {
        if (mainIndex == SecondTypeMainSpell)
        {
            return (uint)CardType.Spell;
        }
        if (mainIndex == SecondTypeMainTrap)
        {
            return (uint)CardType.Trap;
        }
        return (uint)CardType.Monster;
    }

    /// <summary>某一档种类显示成什么字（`-1` = 「通常」，其余走 <see cref="typeName"/>）。</summary>
    public static string secondTypeText(int bit)
    {
        return bit < 0 ? GameStringManager.get_unsafe(1054) : typeName(bit);
    }

    /// <summary>
    /// 某一档种类对应的类型掩码。
    /// 「通常」要连 <see cref="CardType.Normal"/> 一起给（怪兽的「通常」就是 bit4，
    /// 但按老口径写成 `Monster + Normal`，与 <c>getTypeFilter2</c> 逐字一致）。
    /// </summary>
    public static uint secondTypeMask(int mainIndex, int bit)
    {
        uint main = secondTypeMainMask(mainIndex);
        if (bit < 0)
        {
            return mainIndex == SecondTypeMainMonster ? (main | (uint)CardType.Normal) : main;
        }
        return main | (1u << bit);
    }

    /// <summary>
    /// 这一档在当前模式的卡池里**真的有卡**吗。
    ///
    /// 为什么需要它：RD 和 OCG 共用同一份位表，而 RD 没有同调/超量/灵摆/连接，
    /// 魔法里也没有速攻 —— 列出来就是个死项：勾了永远是 0 条，
    /// 玩家只会以为「我的卡不在库里」。反过来 RD 有的「极大/传说卡」OCG 没有，
    /// 也要在 RD 下补上。**按池子里实际的卡数决定**，卡片包一更新它自己就跟着变。
    ///
    /// OCG 一律返回 true：老清单一个字不改（改通用路径必须连带跑 OCG 回归）。
    /// </summary>
    public static bool secondTypeMeaningful(int mainIndex, int bit)
    {
        if (!GameModeManager.IsRD)
        {
            return true;
        }
        return YGOSharp.CardsManager.CountOfType(bit, secondTypeMainMask(mainIndex)) > 0;
    }

    /// <summary>
    /// 当前模式、当前主类该列出来的种类位（顺序 = 下拉里的显示顺序）。
    /// RD = 「老清单里真有的」按原顺序 + 末尾追加 RD 专有且真有卡的。
    /// </summary>
    public static List<int> secondTypeBits(int mainIndex)
    {
        List<int> list = new List<int>();
        if (mainIndex < 0 || mainIndex >= SecondTypeMainCount)
        {
            return list;
        }
        int[] baseBits = SecondTypeBitsOCG[mainIndex];
        for (int i = 0; i < baseBits.Length; i++)
        {
            if (secondTypeMeaningful(mainIndex, baseBits[i]))
            {
                list.Add(baseBits[i]);
            }
        }
        if (GameModeManager.IsRD)
        {
            for (int i = 0; i < RdExtraSecondTypeBits.Length; i++)
            {
                if (secondTypeMeaningful(mainIndex, RdExtraSecondTypeBits[i]))
                {
                    list.Add(RdExtraSecondTypeBits[i]);
                }
            }
        }
        return list;
    }

    /// <summary>
    /// 三个主类的种类清单拼成一行（探针用）：`怪兽[通常|效果|…] 魔法[…] 陷阱[…]`。
    /// 验收脚本靠它判「RD 下没有同调/速攻，有极大」—— 界面上的下拉点开才有内容，
    /// 落盘一份才可离线判。
    /// </summary>
    public static string SecondTypeTableSummary()
    {
        string[] names = { "怪兽", "魔法", "陷阱" };
        string r = "";
        for (int m = 0; m < SecondTypeMainCount; m++)
        {
            if (m > 0)
            {
                r += " ";
            }
            List<int> bits = secondTypeBits(m);
            string items = "";
            for (int i = 0; i < bits.Count; i++)
            {
                if (i > 0)
                {
                    items += "|";
                }
                items += secondTypeText(bits[i]);
            }
            r += names[m] + "[" + items + "]";
        }
        return r;
    }

    public static string getName(YGOSharp.Card card)
    {
        string limitot = "";
        if ((card.Ot & 0x1) > 0)
            limitot += "[OCG]";
        if ((card.Ot & 0x2) > 0)
            limitot += "[TCG]";
        if ((card.Ot & 0x4) > 0)
            limitot += "[Custom]";
        if ((card.Ot & 0x8) > 0)
            limitot += InterString.Get("[简中]");
        string re = "";
        try
        {
            re += "[b]" + card.Name + "[/b]";
            re += "\n";
            re += "[sup]" + limitot + "[/sup]";
            re += "\n";
            re += "[sup]" + card.Id.ToString() + "[/sup]";
            re += "\n";
        }
        catch (Exception e)
        {
        }

        if (differ(card.Attribute, (int)CardAttribute.Earth))     
        {
            re = "[F4A460]" + re + "[-]";
        }
        if (differ(card.Attribute, (int)CardAttribute.Water))
        {
            re = "[D1EEEE]" + re + "[-]";
        }
        if (differ(card.Attribute, (int)CardAttribute.Fire))
        {
            re = "[F08080]" + re + "[-]";
        }
        if (differ(card.Attribute, (int)CardAttribute.Wind))
        {
            re = "[B3EE3A]" + re + "[-]";
        }
        if (differ(card.Attribute, (int)CardAttribute.Light))
        {
            re = "[EEEE00]" + re + "[-]";
        }
        if (differ(card.Attribute, (int)CardAttribute.Dark))
        {
            re = "[FF00FF]" + re + "[-]";
        }

        return re;
    }

    public static string getType(Card card)
    {
        string re = "";
        try
        {
            if (differ(card.Type, (long)CardType.Monster)) re += "[ff8000]" + mainType(card.Type);
            else if (differ(card.Type, (long)CardType.Spell)) re += "[7FFF00]" + mainType(card.Type);
            else if (differ(card.Type, (long)CardType.Trap)) re += "[dda0dd]" + mainType(card.Type);
            else re += "[ff8000]" + mainType(card.Type);
            re += "[-]";
        }
        catch (Exception e)
        {
        }

        return re;
    }

    public static string getSmall(YGOSharp.Card data)
    {
        string re = "";

        try
        {
            if ((data.Type & (int)CardType.Monster) > 0)
            {
                re += "[ff8000]";
                re += "["+secondType(data.Type)+"]";

                if ((data.Type & (int)CardType.Link) == 0)
                {
                    if ((data.Type & (int)CardType.Xyz) > 0)
                    {
                        re += " " + race(data.Race) + fen + attribute(data.Attribute) + fen + data.Level.ToString() + "[sup]☆[/sup]";
                    }
                    else
                    {
                        re += " " + race(data.Race) + fen + attribute(data.Attribute) + fen + data.Level.ToString() + "[sup]★[/sup]";
                    }
                }
                else
                {
                    re += " " + race(data.Race) + fen + attribute(data.Attribute) ;
                }

                if (data.LScale > 0) re += fen + data.LScale.ToString() + "[sup]P[/sup]";
                re += "\n";
                if (data.Attack < 0)
                {
                    re += "[sup]ATK[/sup]?  ";
                }
                else
                {
                    if (data.rAttack>0) 
                    {
                        int pos = data.Attack - data.rAttack;
                        if (pos>0)  
                        {
                            re += "[sup]ATK[/sup]" + data.Attack.ToString() + "(↑" + pos.ToString() + ")  ";
                        }
                        if (pos < 0)
                        {
                            re += "[sup]ATK[/sup]" + data.Attack.ToString() + "(↓" + (-pos).ToString() + ")  ";
                        }
                        if (pos == 0)
                        {
                            re += "[sup]ATK[/sup]" + data.Attack.ToString() + "  ";
                        }
                    }
                    else
                    {
                        re += "[sup]ATK[/sup]" + data.Attack.ToString() + "  ";
                    }
                }
                if ((data.Type & (int)CardType.Link) == 0)
                {
                    if (data.Defense < 0)
                    {
                        re += "[sup]DEF[/sup]?";
                    }
                    else
                    {
                        if (data.rAttack > 0)
                        {
                            int pos = data.Defense - data.rDefense;
                            if (pos > 0)
                            {
                                re += "[sup]DEF[/sup]" + data.Defense.ToString() + "(↑" + pos.ToString() + ")";
                            }
                            if (pos < 0)
                            {
                                re += "[sup]DEF[/sup]" + data.Defense.ToString() + "(↓" + (-pos).ToString() + ")";
                            }
                            if (pos == 0)
                            {
                                re += "[sup]DEF[/sup]" + data.Defense.ToString();
                            }
                        }
                        else
                        {
                            re += "[sup]DEF[/sup]" + data.Defense.ToString();
                        }
                    }
                }
                else
                {
                    re += "[sup]LINK[/sup]" + data.Level.ToString();
                }
            }
            else if ((data.Type & ((int)CardType.Spell + (int)CardType.Trap)) > 0)
            {
                re += (data.Type & (int)CardType.Spell) > 0 ? "[7FFF00]" : "[DDA0DD]";
                re += secondType(data.Type);
                if (data.LScale > 0) re += fen + data.LScale.ToString() + "[sup]P[/sup]";

                YGOSharp.Card original = YGOSharp.CardsManager.GetCard(data.Id);

                if ((original.Type & (int)CardType.Monster) > 0)
                {
                    re += "\n";
                    re += "[999999]";
                    re += "[" + secondType(original.Type) + "]";

                    if ((original.Type & (int)CardType.Link) == 0)
                    {
                        if ((original.Type & (int)CardType.Xyz) > 0)
                        {
                            re += " " + race(original.Race) + fen + attribute(original.Attribute) + fen + original.Level.ToString() + "[sup]☆[/sup]";
                        }
                        else
                        {
                            re += " " + race(original.Race) + fen + attribute(original.Attribute) + fen + original.Level.ToString() + "[sup]★[/sup]";
                        }
                    }
                    else
                    {
                        re += " " + race(original.Race) + fen + attribute(original.Attribute);
                    }

                    if (original.LScale > 0) re += fen + original.LScale.ToString() + "[sup]P[/sup]";
                    re += "\n";
                    if (original.Attack < 0)
                    {
                        re += "[sup]ATK[/sup]?  ";
                    }
                    else
                    {
                        re += "[sup]ATK[/sup]" + original.Attack.ToString() + "  ";
                    }
                    if ((original.Type & (int)CardType.Link) == 0)
                    {
                        if (original.Defense < 0)
                        {
                            re += "[sup]DEF[/sup]?";
                        }
                        else
                        {
                            re += "[sup]DEF[/sup]" + original.Defense.ToString();
                        }
                    }
                    else
                    {
                        re += "[sup]LINK[/sup]" + original.Level.ToString();
                    }
                    re += "[-]";
                }
            }
            else
            {
                re += "[ff8000]";
            }
            if (data.Alias > 0)
            {
                if (data.Alias != data.Id)
                {
                    string name = YGOSharp.CardsManager.Get(data.Alias).Name;
                    if (name != data.Name)
                    {
                        re += "\n[" + YGOSharp.CardsManager.Get(data.Alias).Name + "]";
                    }
                }
            }
            if (data.Setcode > 0)
            {
                re += "\n";
                re += xilie;
                // 需求 3：系列行走**显示版**（系列译名改了这里跟着变）；
                // 检索用的 getSetName（原生口径）保持不动。
                re += getSetNameDisplay(data.Setcode);
            }
            re += "[-]";
        }
        catch (Exception e)
        {
        }
        return re;
    }

    public static string getSearchResult(YGOSharp.Card data)
    {
        string re = "";

        try
        {
            if ((data.Type & 0x1) > 0)
            {
                re += "[ff8000]";

                if ((data.Type & (int)CardType.Link) == 0)
                {
                    if ((data.Type & (int)CardType.Xyz) > 0)
                    {
                        re += race(data.Race) + fen + attribute(data.Attribute) + fen + data.Level.ToString() + "[sup]☆[/sup]";
                    }
                    else
                    {
                        re += race(data.Race) + fen + attribute(data.Attribute) + fen + data.Level.ToString() + "[sup]★[/sup]";
                    }
                }
                else
                {
                    re += race(data.Race) + fen + attribute(data.Attribute);
                }

                if (data.LScale > 0) re += fen + data.LScale.ToString() + "[sup]P[/sup]";
                re += "\n";
                if (data.Attack < 0)
                {
                    re += "[sup]ATK[/sup]?  ";
                }
                else
                {
                    if (data.rAttack > 0)
                    {
                        int pos = data.Attack - data.rAttack;
                        if (pos > 0)
                        {
                            re += "[sup]ATK[/sup]" + data.Attack.ToString() + "(↑" + pos.ToString() + ")  ";
                        }
                        if (pos < 0)
                        {
                            re += "[sup]ATK[/sup]" + data.Attack.ToString() + "(↓" + (-pos).ToString() + ")  ";
                        }
                        if (pos == 0)
                        {
                            re += "[sup]ATK[/sup]" + data.Attack.ToString() + "  ";
                        }
                    }
                    else
                    {
                        re += "[sup]ATK[/sup]" + data.Attack.ToString() + "  ";
                    }
                }
                if ((data.Type & (int)CardType.Link) == 0)
                {
                    if (data.Defense < 0)
                    {
                        re += "[sup]DEF[/sup]?";
                    }
                    else
                    {
                        if (data.rAttack > 0)
                        {
                            int pos = data.Defense - data.rDefense;
                            if (pos > 0)
                            {
                                re += "[sup]DEF[/sup]" + data.Defense.ToString() + "(↑" + pos.ToString() + ")";
                            }
                            if (pos < 0)
                            {
                                re += "[sup]DEF[/sup]" + data.Defense.ToString() + "(↓" + (-pos).ToString() + ")";
                            }
                            if (pos == 0)
                            {
                                re += "[sup]DEF[/sup]" + data.Defense.ToString();
                            }
                        }
                        else
                        {
                            re += "[sup]DEF[/sup]" + data.Defense.ToString();
                        }
                    }
                }
                else
                {
                    re += "[sup]LINK[/sup]" + data.Level.ToString();
                }
            }
            else if ((data.Type & 0x2) > 0)
            {
                re += "[7FFF00]";
                re += secondType(data.Type);
                if (data.LScale > 0) re += fen + data.LScale.ToString() + "[sup]P[/sup]";
            }
            else if ((data.Type & 0x4) > 0)
            {
                re += "[dda0dd]";
                re += secondType(data.Type);
            }
            else
            {
                re += "[ff8000]";
            }
            if (data.Alias > 0)
            {
                if (data.Alias != data.Id)
                {
                    string name = YGOSharp.CardsManager.Get(data.Alias).Name;
                    if (name != data.Name)
                    {
                        re += "\n[" + YGOSharp.CardsManager.Get(data.Alias).Name + "]";
                    }
                }
            }
            re += "[-]";
        }
        catch (Exception e)
        {
        }
        return re;
    }

    public static string getSetName(ulong Setcode)
    {
        var setcodes = new int[4];
        for(var j = 0; j < 4; j++)
        {
            setcodes[j] = (int)((Setcode >> j * 16) & 0xffff);
        }
        var returnValue = new List<string>();
        for (var i = 0; i < GameStringManager.xilies.Count; i++)
        {
            var currentHash = GameStringManager.xilies[i].hashCode;
            for(var j = 0; j < 4; j++)
            {
                if (currentHash == setcodes[j])
                {
                    var setArray = GameStringManager.xilies[i].content.Split('\t');
                    var setString = setArray[0];
                    //if (setArray.Length > 1)
                    //{
                    //    setString += "[sup]" + setArray[1] + "[/sup]";
                    //}
                    returnValue.Add(setString);
                }
            }
        }
        return String.Join("|", returnValue.ToArray());
    }

    /// <summary>
    /// **简介系列行的显示版**（需求 3）：与 <see cref="getSetName"/> 同一张表、同一段位拆法，
    /// 唯一差别是每段先取原生名、再过一遍系列译名（<c>CardNameTranslation.FieldNameDisplay</c>
    /// —— 没手改就原样）。搜索用的 <see cref="getSetName"/> **一个字都不动**：
    /// <c>Card.strSetName</c> 保持原生口径，免得把检索语义搅了。
    /// </summary>
    public static string getSetNameDisplay(ulong Setcode)
    {
        var setcodes = new int[4];
        for(var j = 0; j < 4; j++)
        {
            setcodes[j] = (int)((Setcode >> j * 16) & 0xffff);
        }
        var returnValue = new List<string>();
        for (var i = 0; i < GameStringManager.xilies.Count; i++)
        {
            var currentHash = GameStringManager.xilies[i].hashCode;
            for(var j = 0; j < 4; j++)
            {
                if (currentHash == setcodes[j])
                {
                    var setArray = GameStringManager.xilies[i].content.Split('\t');
                    returnValue.Add(YGOSharp.CardNameTranslation.FieldNameDisplay(setArray[0]));
                }
            }
        }
        return String.Join("|", returnValue.ToArray());
    }

    /// <summary>一个系列的两副面孔：<c>native</c> = !setname 表里的原名（检索口径），
    /// <c>display</c> = 过了译名表的显示版（画在简介里那副）。</summary>
    public struct SetNamePair
    {
        public string native;
        public string display;
        /// <summary>这一系列在 !setname 表里的 hashCode（简介系列行链接用它精确检索）。</summary>
        public int hash;
    }

    /// <summary>
    /// 这张卡挂的**全部系列**，原名 + 显示名 + 表内 hashCode **成对**给出。
    ///
    /// <para><b>为什么要成对</b>：简介系列行（<see cref="getSmall"/> 末尾「系列：A|B」）
    /// 要做成可点链接（用户 2026-10-04），而点下去要的是**精确按系列检索** ——
    /// 靠显示名全文检索会把"卡文里提到它"的一堆无关卡也倒出来。所以链接 payload 里
    /// 直接带表内 hash，检索端按 <c>Setcode</c> 的 16 位槽逐位比对，一个字都不用猜。</para>
    ///
    /// <para>迭代口径与 <see cref="getSetNameDisplay"/> **逐字一致**（同一张表、同一段位拆法、
    /// 同一个顺序）⇒ 按下标与显示行一一对齐，不会错位。</para>
    /// </summary>
    public static List<SetNamePair> getSetNamePairs(ulong Setcode)
    {
        var setcodes = new int[4];
        for (var j = 0; j < 4; j++)
        {
            setcodes[j] = (int)((Setcode >> j * 16) & 0xffff);
        }
        var returnValue = new List<SetNamePair>();
        for (var i = 0; i < GameStringManager.xilies.Count; i++)
        {
            var currentHash = GameStringManager.xilies[i].hashCode;
            for (var j = 0; j < 4; j++)
            {
                if (currentHash == setcodes[j])
                {
                    var setArray = GameStringManager.xilies[i].content.Split('\t');
                    SetNamePair p = new SetNamePair();
                    p.native = setArray[0];
                    p.display = YGOSharp.CardNameTranslation.FieldNameDisplay(setArray[0]);
                    p.hash = currentHash;
                    returnValue.Add(p);
                }
            }
        }
        return returnValue;
    }

    /// <summary>
    /// 这张卡的**原生系列名清单**（去重、保序；不带 <c>!setname</c> 里 Tab 后面的副名）。
    /// 给「译」菜单的「本卡系列改名」选择器用 —— 玩家从真实存在的系列里挑，不用手输原名。
    /// </summary>
    public static List<string> getSetNames(ulong Setcode)
    {
        var setcodes = new int[4];
        for(var j = 0; j < 4; j++)
        {
            setcodes[j] = (int)((Setcode >> j * 16) & 0xffff);
        }
        var returnValue = new List<string>();
        for (var i = 0; i < GameStringManager.xilies.Count; i++)
        {
            var currentHash = GameStringManager.xilies[i].hashCode;
            for(var j = 0; j < 4; j++)
            {
                if (currentHash == setcodes[j])
                {
                    var setArray = GameStringManager.xilies[i].content.Split('\t');
                    var setString = setArray[0].Trim();
                    if (setString.Length > 0 && !returnValue.Contains(setString))
                    {
                        returnValue.Add(setString);
                    }
                }
            }
        }
        return returnValue;
    }

    // ============================ 卡文写法 → 位序 的反查（RD 简介链接/条件检索共用） ============================
    //
    // RD 卡文的括号里写的是**卡面上的字**（「银河族」「地属性」），而检索要比的是**位掩码**。
    // 两边必须有且只有一份换算：卡文解析端（RdCondition）与将来的任何筛选器都调这里。
    // ⛔ 别照着 `族` 尾巴自己算下标：`raceName` 给的是「战士」而卡文写「战士族」，
    //   而且 RD 那 6 个新种族里有一个连用字都不一样（见下）。

    /// <summary>
    /// 卡文里的种族写法（`银河族` / `幻想魔族`）→ 位序；认不出来返回 -1。
    /// 传进来的可以带 `族` 也可以不带（`raceName` 给的就是不带的那种）。
    /// </summary>
    public static int raceBitOf(string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return -1;
        }
        string n = name.Trim();
        if (n.EndsWith("族", StringComparison.Ordinal))
        {
            n = n.Substring(0, n.Length - 1);
        }
        if (n.Length == 0)
        {
            return -1;
        }
        // 先按卡面文案等值比（raceName 已经是「位序 → 文案」的唯一定义）。
        for (int i = 0; i < RaceCount; i++)
        {
            string show = raceName(i);
            if (show.Length > 0 && show == n)
            {
                return i;
            }
        }
        // 卡文与卡面用字不一致的那个（实测 11 处）：
        //   卡文「欧米伽念动力族」，而 RdRaceNames 给的是「欧米茄念动力」（伽/茄）。
        //   等值比较找不着，只能单列 —— 别把 RdRaceNames 改成「伽」，
        //   那个数组是卡面显示口径，改了卡面上的种族名也跟着变。
        if (n == "欧米伽念动力")
        {
            return RaceBitCountOCG + 2;   // bit28，见 RdRaceNames 的位序表
        }
        return -1;
    }

    /// <summary>卡文里的属性写法（`地属性` / `光属性`）→ 位序；认不出来返回 -1。</summary>
    public static int attributeBitOf(string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return -1;
        }
        string n = name.Trim();
        if (n.EndsWith("属性", StringComparison.Ordinal))
        {
            n = n.Substring(0, n.Length - 2);
        }
        if (n.Length == 0)
        {
            return -1;
        }
        // 属性文案表在 strings.conf 的 1010..1016（地 水 炎 风 光 暗 神），
        // 与 attribute() 用的是同一批 —— 那边印卡面，这边反查，口径自然一致。
        for (int i = 0; i < 7; i++)
        {
            string show = GameStringManager.get_unsafe(1010 + i);
            if (!string.IsNullOrEmpty(show) && show == n)
            {
                return i;
            }
        }
        return -1;
    }

    /// <summary>
    /// RD「传说卡」在卡文里的字面标记 —— 卡文首行那颗稀有度角标
    /// （实测形如 <c>RD/ECG1-JP002\t&lt;传说卡&gt;</c>），也就是界面上看得见的那三个字。
    /// </summary>
    public const string RdLegendWord = "传说卡";

    /// <summary>
    /// RD 传说卡判据：**卡文里出现「传说卡」三个字**。
    ///
    /// ⛔ 不能拿类型位判 —— 老注释写着「bit3 (0x8) = 传说卡（121 张）」，实测是错的：
    ///   三张卡表 3484 张里带 `&lt;传说卡&gt;` 的有 121 张（rd_standard），
    ///   而 type 真的带了 bit3 的**只有 2 张**（`0x4000a`：魔导师之力 / 团结之力 这两张
    ///   装备魔法）。按位掩码筛传说卡会漏掉 119 张。所以这里认卡文。
    /// </summary>
    public static bool isLegendCard(YGOSharp.Card card)
    {
        return card != null
            && !string.IsNullOrEmpty(card.Desc)
            && card.Desc.IndexOf(RdLegendWord, StringComparison.Ordinal) >= 0;
    }
}

