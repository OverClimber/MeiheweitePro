using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 「卡名翻译」的操作面（需求 5 的 UI）。
///
/// ── 它在界面里长什么样 ────────────────────────────────────────────────────
/// 入口只有一颗 40×40 的小钮，挂在**卡牌说明面板左侧那一列按钮的最下面**
/// （那列原本是 ↑ ↓ A+ A−，见 CardDescription.installNameTranslationButton）。
/// 图标区放一个「译」字，悬停出提示条「卡名翻译」。点它弹一个单选项菜单：
///   * <b>全局译名：xxx</b>   → 进二级菜单挑 ygo原生翻译 / nw / cnocg / 简中 …
///   * <b>本卡改名：xxx</b>   → 弹输入框，给**当前这张卡**取自定义外号（留空 = 还原）
///   * <b>本卡系列改名…</b>   → （需求 3，卡上挂着系列才出现）改**系列（字段）**的译名；
///                              按翻译表分家存、只做纯手改，改完作用于简介正文与系列行
///   * <b>恢复本卡默认译名</b> → 只在这张卡真的有自定义外号时才出现
///
/// 为什么不放进「系统设置」窗口：那边每一行都进 save() 的落盘循环、行数还是既有验收
/// 脚本的判据（见 Setting.AddExtraToggleRow 的注释），加一行会把「rows == 3」那类断言
/// 打红；而改卡名本来就该在「看着这张卡」的时候做，说明面板是唯一有卡片上下文的地方。
///
/// ── 为什么它不需要 Program 字段 ───────────────────────────────────────────
/// 这个 Servant 不摆自己的窗口：它只负责开 <see cref="Servant.RMSshow_singleChoice"/> /
/// <see cref="Servant.RMSshow_input"/> 那套现成对话框，并用 <see cref="ES_RMS"/> 收结果。
/// 因此用惰性单例（<see cref="Self"/>）就够，Program.initializeALLservants 一个字都不用改。
///
/// ── 性能（需求 6）────────────────────────────────────────────────────────
/// 没有任何每帧开销：Servant.Update 只在 isShowed 时才跑，而这个 Servant 永远不 show；
/// 改一次译名只重算**一遍**全卡池（CardsManager.ReapplyNameTranslation → ApplyPool），
/// 之后所有显示都是查表，富文本缓存由 NameStamp 整体失效一次。
/// </summary>
public class NameTranslationUI : Servant
{
    // ---------------------------------------------------------------- 对话框的 hash

    const string HashMenu = "nametrans.menu";
    const string HashPack = "nametrans.pack";
    const string HashAlias = "nametrans.alias";
    const string HashCardPack = "nametrans.cardpack";

    /// <summary>「本卡系列改名」的选择器 hash（需求 3，该卡有多个系列时先挑一个）。</summary>
    const string HashField = "nametrans.field";

    /// <summary>「本卡系列改名」的输入框 hash。</summary>
    const string HashFieldInput = "nametrans.fieldinput";


    /// <summary>
    /// **批量**译名的选择器 hash（用户 2026-10-03：检索窗里的「译」钮）。
    /// 与 <see cref="HashCardPack"/> 分开：那个作用于**一张**卡，这个作用于
    /// 检索窗当前那一整批（几千张），两者落盘键相同但入口与收尾路径不同。
    /// </summary>
    const string HashBatchPack = "nametrans.batchpack";

    /// <summary>「跟随全局」在几个选择器里共用的哨兵值（不落盘）。</summary>
    const string FollowGlobal = "global";

    /// <summary>说明面板上那个「译」钮的点按目标名（也是它的 GameObject 名）。</summary>
    public const string ButtonName = "translate_";

    // ---------------------------------------------------------------- 单例

    static NameTranslationUI self;

    static NameTranslationUI Self
    {
        get
        {
            if (self == null)
            {
                self = new NameTranslationUI();
            }
            return self;
        }
    }

    public override void initialize()
    {
        // 不摆窗口：这个 Servant 只做「开对话框 + 收结果」。
    }

    public override void applyShowArrangement()
    {
    }

    public override void applyHideArrangement()
    {
    }

    // ---------------------------------------------------------------- 当前卡

    /// <summary>打开菜单那一刻记下的卡 —— 菜单/输入框往返期间可能已经换了显示内容，必须留档。</summary>
    static int pendingCardId;
    static string pendingCardName = "";
    static string pendingCardNative = "";

    /// <summary>打开菜单那一刻记下的系列段（0 = 这张卡没有系列）；改名全程用它。</summary>
    static ulong pendingCardSetcode;

    /// <summary>「本卡系列改名」进行中的那个原生系列名（选择器/输入框往返期间留档）。</summary>
    static string pendingFieldNative = "";

    static YGOSharp.Card CurrentCard()
    {
        try
        {
            if (Program.I() == null)
            {
                return null;
            }
            CardDescription d = Program.I().cardDescription;
            return d != null ? d.showingCard : null;
        }
        catch (Exception)
        {
            return null;
        }
    }

    // ---------------------------------------------------------------- 菜单

    /// <summary>说明面板上那颗「译」钮点下来的口子。</summary>
    public static void OpenMenu()
    {
        try
        {
            YGOSharp.Card card = CurrentCard();
            pendingCardId = card != null ? card.Id : 0;
            pendingCardName = card != null ? (card.Name ?? "") : "";
            pendingCardNative = card != null ? (card.nativeName ?? card.Name ?? "") : "";
            pendingCardSetcode = card != null ? card.Setcode : 0;

            List<messageSystemValue> options = new List<messageSystemValue>();
            options.Add(new messageSystemValue
            {
                value = "pack",
                hint = "全局译名：" + YGOSharp.CardNameTranslation.CurrentLabel,
            });
            if (pendingCardId > 0)
            {
                // 改名是**按异画组**生效的（同名的异画一起改），菜单上把这件事说清楚。
                int altArts = YGOSharp.CardsManager.AliasGroupSize(pendingCardId);
                // ⛔ 「本卡译名表」只在 OCG 出现（2026-10-02 用户口径）：RD 只有原生一份表，
                //   给一张 RD 卡"挑一份表"是没有意义的动作 —— 摆出来只会让玩家点进去发现
                //   列表里孤零零一项。RD 保留的只有「本卡改名」（玩家自己写的别名）与
                //   「恢复本卡默认译名」。
                if (!GameModeManager.IsRD)
                {
                    options.Add(new messageSystemValue
                    {
                        value = "cardpack",
                        hint = "本卡译名表：" + CardPackLabel(pendingCardId),
                    });
                }
                options.Add(new messageSystemValue
                {
                    value = "alias",
                    hint = "本卡改名：" + Shrink(pendingCardName, 8)
                        + (altArts > 1 ? "（含 " + altArts + " 张异画）" : ""),
                });
                // 系列（字段）改名（需求 3）：这张卡真的挂在系列上才出现 ——
                // RD 卡也有系列（银河 / 电子人…），所以这里**不按模式收**，两个模式都开放。
                if (pendingCardSetcode > 0)
                {
                    options.Add(new messageSystemValue
                    {
                        value = "field",
                        hint = FieldMenuItemHint(),
                    });
                }
                if (YGOSharp.CardNameTranslation.HasOverride(pendingCardId))
                {
                    options.Add(new messageSystemValue
                    {
                        value = "reset",
                        hint = "恢复本卡默认译名",
                    });
                }
            }
            ShowChoice(HashMenu, options);
            // 把**真正建出来的**那一串菜单项也落盘（判据是"RD 下没有 cardpack 这一格"，
            // 靠数 options 比靠源码走查可靠）。
            System.Text.StringBuilder ov = new System.Text.StringBuilder();
            for (int i = 0; i < options.Count; i++)
            {
                if (ov.Length > 0)
                {
                    ov.Append('|');
                }
                ov.Append(options[i] != null ? options[i].value : "null");
            }
            QuickTestTrace.Log("nametrans", "menu card=" + pendingCardId
                + " rd=" + (GameModeManager.IsRD ? 1 : 0)
                + " pack=" + YGOSharp.CardNameTranslation.CurrentLabel
                + " items=[" + ov + "]");
        }
        catch (Exception e)
        {
            Program.DEBUGLOG(e);
        }
    }

    /// <summary>
    /// 验收用：把「译」菜单 / 两个选择器在当前模式下**会列出什么**摊成一行日志（不弹窗）。
    ///
    /// 为什么要有它：菜单内容只有点开才看得见，而验收脚本点不到「译」钮（它在说明面板左侧、
    /// 需要先有卡上下文），所以把"会列出什么"直接在进程内算出来。
    /// 判据：OCG 下 `menu=[pack|cardpack|alias…]`、`packs=[native|nw|cnocg|cn]`；
    ///        RD 下 `menu=[pack|alias…]`（无 cardpack）、`packs=[native]`。
    /// 三个清单都从**与真菜单同一处的判据**算：`menu` 复刻 OpenMenu 的分支、
    /// `packs` 直接用 AvailablePacks。
    /// ⛔ 别改成"调 OpenMenu 再读对话框"：那会真弹一个对话框出来，把截图与后续探针搅乱。
    /// </summary>
    public static string ProbeMenuContents()
    {
        try
        {
            YGOSharp.Card card = CurrentCard();
            int id = card != null ? card.Id : 0;

            System.Text.StringBuilder menu = new System.Text.StringBuilder();
            menu.Append("pack");
            if (id > 0)
            {
                if (!GameModeManager.IsRD)
                {
                    menu.Append("|cardpack");
                }
                menu.Append("|alias");
                // 系列（字段）改名：卡上真的挂着系列才出现（两个模式都开放）。
                if (card != null && card.Setcode > 0)
                {
                    menu.Append("|field");
                }
                if (YGOSharp.CardNameTranslation.HasOverride(id))
                {
                    menu.Append("|reset");
                }
            }

            // 系列改名（需求 3）那一格依赖「这张卡真的挂着系列」—— 说明面板当前显示的
            // 卡未必带（探针里那张就 Setcode==0），照抄它会让 field 格永远验不到。
            // 所以再对一张**真带系列**的卡复刻一遍菜单（- = 池里没有带系列的卡）。
            string menuWithField = "-";
            if (id >= 0)
            {
                YGOSharp.CardsManager.ForEachActiveCard((cid, c) =>
                {
                    if (menuWithField != "-" || c == null || c.Setcode == 0)
                    {
                        return;
                    }
                    System.Text.StringBuilder mb = new System.Text.StringBuilder();
                    mb.Append("pack");
                    if (!GameModeManager.IsRD)
                    {
                        mb.Append("|cardpack");
                    }
                    mb.Append("|alias|field");
                    if (YGOSharp.CardNameTranslation.HasOverride(cid))
                    {
                        mb.Append("|reset");
                    }
                    menuWithField = mb.ToString();
                });
            }

            System.Text.StringBuilder packs = new System.Text.StringBuilder();
            List<YGOSharp.CardNameTranslation.Pack> avail = YGOSharp.CardNameTranslation.AvailablePacks;
            for (int i = 0; i < avail.Count; i++)
            {
                if (packs.Length > 0)
                {
                    packs.Append('|');
                }
                packs.Append(avail[i].key);
            }

            // `ver=` 与 `stamp=` 是「切模式真的让卡池重算了一遍名字」的物证：
            // GameModeManager.Set 会调 CardNameTranslation.InvalidateForMode()（Bump 版本号），
            // 卡池的 EnsureNameTranslation 靠它比对决定重算。三档跑下来 ver 必须**逐档 +1**，
            // 否则「RD 恒原生」只停在数据层 —— 池里那张 Card.Name 还是 OCG 那一刻的快照。
            return "rdmenu rd=" + (GameModeManager.IsRD ? 1 : 0)
                + " card=" + id
                + " ver=" + YGOSharp.CardNameTranslation.Version
                + " stamp=" + YGOSharp.CardsManager.NameStamp
                + " menu=[" + menu + "]"
                + " menuWithField=[" + menuWithField + "]"
                + " packs=[" + packs + "]"
                + " cur=" + YGOSharp.CardNameTranslation.CurrentKey
                + " stored=" + YGOSharp.CardNameTranslation.Packs.Count
                + " cardpack=" + (id > 0 ? (YGOSharp.CardNameTranslation.GetCardPack(id) ?? "-") : "-")
                + " fieldOvr=" + YGOSharp.CardNameTranslation.FieldOverrideCount;
        }
        catch (Exception e)
        {
            return "rdmenu failed " + e.GetType().Name + ": " + e.Message;
        }
    }

    /// <summary>
    /// 把「译」钮那个菜单 / 选择器 / 改名框收掉。
    /// 验收探针要用：那几个对话框是**独立 Servant 的窗口**，会盖住设置窗口里
    /// 追加行所在的那一列，不收掉就截不出「卡名翻译」那一行。
    /// </summary>
    public static void CloseMenuForProbe()
    {
        try
        {
            Self.RMSshow_clear();
        }
        catch (Exception e)
        {
            Program.DEBUGLOG(e);
        }
    }

    /// <summary>全局译名的二级菜单。列表里有什么，由 translation/ 目录里真实存在的数据决定。</summary>
    static void OpenPackChooser()
    {
        // ⛔ 走 AvailablePacks（按模式收窄），不是 Packs（磁盘上装了什么）：
        //   RD 下这里只剩「ygo原生翻译」一项，见 CardNameTranslation.AvailablePacks。
        List<YGOSharp.CardNameTranslation.Pack> packs = YGOSharp.CardNameTranslation.AvailablePacks;
        List<messageSystemValue> options = new List<messageSystemValue>();
        string cur = YGOSharp.CardNameTranslation.CurrentKey;
        for (int i = 0; i < packs.Count; i++)
        {
            YGOSharp.CardNameTranslation.Pack p = packs[i];
            string hint = (p.key == cur ? "● " : "") + p.label;
            if (p.missing)
            {
                // 玩家点名要的表，但 translation/ 里还没有数据 —— 明说，别让人以为坏了。
                // 能靠下载装上的那几份（nw / cnocg / 简中）额外标一句：这一项点了不是死路。
                hint += YGOSharp.CardTranslationDownloader.IsRemotePack(p.key)
                    ? "（未装数据，可下载）" : "（未装数据）";
            }
            else if (p.Count > 0)
            {
                hint += "（" + p.Count + "）";
            }
            options.Add(new messageSystemValue { value = p.key, hint = hint });
        }
        // 有"未装数据"的项时，把「怎么加」也摆进菜单，免得玩家对着灰项发呆。
        // ⛔ RD 下不摆这一项：那边**本来就只有原生一份**（packs.Count 恒为 1），
        //   照着"只剩一项就问怎么加"的老判据会把一条与 RD 无关的说明塞进菜单。
        if (!GameModeManager.IsRD && (packs.Count <= 1 || HasMissing(packs)))
        {
            options.Add(new messageSystemValue
            {
                value = "help",
                hint = "怎么加入 nw / cnocg / 简中 译名？",
            });
        }
        ShowChoice(HashPack, options);
    }

    /// <summary>
    /// 设置界面那一行「卡名翻译」点下来的口子 —— 与说明面板「译」钮里的
    /// 「全局译名」是**同一个选择器**（同一个 <see cref="HashPack"/> 收结果），
    /// 所以两条入口永远不会显示成两份不一样的状态。
    /// </summary>
    public static void OpenGlobalPackChooser()
    {
        try
        {
            OpenPackChooser();
        }
        catch (Exception e)
        {
            Program.DEBUGLOG(e);
        }
    }

    /// <summary>这张卡此刻"用的是哪一份"，给菜单标题用。没指定就说"跟随全局（xxx）"。</summary>
    static string CardPackLabel(int id)
    {
        string key = YGOSharp.CardNameTranslation.GetCardPack(id);
        if (string.IsNullOrEmpty(key))
        {
            return "跟随全局（" + YGOSharp.CardNameTranslation.CurrentLabel + "）";
        }
        List<YGOSharp.CardNameTranslation.Pack> packs = YGOSharp.CardNameTranslation.Packs;
        for (int i = 0; i < packs.Count; i++)
        {
            if (packs[i].key == key)
            {
                return packs[i].label + (packs[i].missing ? "（未装数据）" : "");
            }
        }
        return key;
    }

    /// <summary>
    /// **单卡**用哪一份翻译。列表与全局那份同源，另外多两项：
    /// 「跟随全局」与「自定义名字…」。按异画组生效（同名的异画一起换）。
    /// </summary>
    static void OpenCardPackChooser()
    {
        if (pendingCardId <= 0)
        {
            return;
        }
        List<YGOSharp.CardNameTranslation.Pack> packs = YGOSharp.CardNameTranslation.AvailablePacks;
        List<messageSystemValue> options = new List<messageSystemValue>();
        options.Add(new messageSystemValue
        {
            value = FollowGlobal,
            hint = "跟随全局（" + YGOSharp.CardNameTranslation.CurrentLabel + "）"
                + (YGOSharp.CardNameTranslation.HasCardPack(pendingCardId) ? "" : "　●当前"),
        });
        string cur = YGOSharp.CardNameTranslation.GetCardPack(pendingCardId);
        for (int i = 0; i < packs.Count; i++)
        {
            YGOSharp.CardNameTranslation.Pack p = packs[i];
            // 已经自定义过外号的卡，换表没有意义（外号优先级最高）——明说，别让玩家白点。
            string hint = (p.key == cur ? "● " : "") + p.label;
            if (p.missing)
            {
                hint += YGOSharp.CardTranslationDownloader.IsRemotePack(p.key)
                    ? "（未装数据，可下载）" : "（未装数据）";
            }
            else if (p.Count > 0)
            {
                hint += "（" + p.Count + "）";
            }
            options.Add(new messageSystemValue { value = p.key, hint = hint });
        }
        options.Add(new messageSystemValue
        {
            value = "alias",
            hint = YGOSharp.CardNameTranslation.HasOverride(pendingCardId)
                ? "自定义名字：" + Shrink(YGOSharp.CardNameTranslation.GetOverride(pendingCardId), 8)
                : "自定义名字…（自己起一个）",
        });
        int altArts = YGOSharp.CardsManager.AliasGroupSize(pendingCardId);
        ShowChoice(HashCardPack, options);
        QuickTestTrace.Log("nametrans", "cardpack chooser id=" + pendingCardId
            + " altArts=" + altArts + " cur=" + (cur ?? "(跟随全局)"));
    }

    /// <summary>给当前这张卡取自定义外号（弹出输入框，预填当前名字）。</summary>
    static void OpenAliasInput()
    {
        if (pendingCardId <= 0)
        {
            return;
        }
        string def = YGOSharp.CardNameTranslation.HasOverride(pendingCardId)
            ? YGOSharp.CardNameTranslation.GetOverride(pendingCardId)
            : pendingCardName;
        int altArts = YGOSharp.CardsManager.AliasGroupSize(pendingCardId);
        ShowInputBox(HashAlias,
            "给「" + Shrink(pendingCardNative, 10) + "」定个名字"
                + (altArts > 1 ? "（" + altArts + " 张异画一起改）" : "")
                + "（留空 = 恢复默认）", def);
    }

    // ---------------------------------------------------------------- 批量译名（检索窗「译」钮）

    /// <summary>
    /// 打开菜单那一刻记下的那**一批**卡号（检索窗当前结果集）。
    /// ⛔ 必须是**拷贝**：调用方（检索窗）手里那份列表在对话框往返期间可能被
    /// 重新检索覆盖（<c>Search()</c> 会重跑一次并重建 list），直接持引用会指到
    /// 另一批卡上 —— 表现为"点开译字、选完发现改的不是眼前这批"。
    /// </summary>
    static List<int> pendingBatchIds = new List<int>();

    /// <summary>
    /// 检索窗「译」钮点下来的口子：给当前那一批关联卡**统一**决定用哪一份译名。
    ///
    /// <para>为什么要有这个（用户 2026-10-03）：单卡换表只能一张张点
    /// （<see cref="OpenCardPackChooser"/> 走的是"说明面板当前显示的那张"），
    /// 而检索窗弹出来天生就是**一批**关联卡（同名的异画、卡文里互相记述的那一片），
    /// 玩家要的是"这一批整体换一份译名"。</para>
    ///
    /// <para>列表与 <see cref="OpenCardPackChooser"/> 同源（走
    /// <see cref="YGOSharp.CardNameTranslation.AvailablePacks"/>，按模式收窄），
    /// 额外多一项「跟随全局」。</para>
    /// </summary>
    public static void OpenBatchPackChooser(List<int> cardIds, string batchTitle)
    {
        try
        {
            pendingBatchIds = cardIds != null
                ? new List<int>(cardIds)
                : new List<int>();
            // 剔掉非法卡号，免得"这一批有多少张"报数与实际落盘对不上
            for (int i = pendingBatchIds.Count - 1; i >= 0; i--)
            {
                if (pendingBatchIds[i] <= 0)
                {
                    pendingBatchIds.RemoveAt(i);
                }
            }
            if (pendingBatchIds.Count <= 0)
            {
                Self.RMSshow_none("这一批里没有卡，译名无从谈起。");
                return;
            }
            if (GameModeManager.IsRD)
            {
                // RD 恒原生、也不做单卡换表（口径与「译」菜单里不出现 cardpack 相同）。
                // 说明白比摆一个点了没反应的菜单好。
                Self.RMSshow_none(
                    "RD 卡只有原生一份译名（nw / cnocg / 简中收的都是 OCG 卡），"
                    + "所以这一批不提供换表。\n"
                    + "要改译名请在 OCG 模式，或用「本卡改名」给单张卡起别名。");
                return;
            }

            List<YGOSharp.CardNameTranslation.Pack> packs =
                YGOSharp.CardNameTranslation.AvailablePacks;
            List<messageSystemValue> options = new List<messageSystemValue>();
            string cur = YGOSharp.CardNameTranslation.BatchPackKey(pendingBatchIds);
            options.Add(new messageSystemValue
            {
                value = FollowGlobal,
                hint = "跟随全局（" + YGOSharp.CardNameTranslation.CurrentLabel + "）"
                    + (cur == FollowGlobal || cur.Length == 0 ? "　●当前" : ""),
            });
            for (int i = 0; i < packs.Count; i++)
            {
                YGOSharp.CardNameTranslation.Pack p = packs[i];
                string hint = (p.key == cur ? "● " : "") + p.label;
                if (p.missing)
                {
                    hint += YGOSharp.CardTranslationDownloader.IsRemotePack(p.key)
                        ? "（未装数据，可下载）" : "（未装数据）";
                }
                else if (p.Count > 0)
                {
                    hint += "（" + p.Count + "）";
                }
                options.Add(new messageSystemValue { value = p.key, hint = hint });
            }
            ShowChoice(HashBatchPack, options);
            System.Text.StringBuilder ov = new System.Text.StringBuilder();
            for (int i = 0; i < options.Count; i++)
            {
                if (ov.Length > 0)
                {
                    ov.Append('|');
                }
                ov.Append(options[i] != null ? options[i].value : "null");
            }
            QuickTestTrace.Log("nametrans", "batchpack chooser n=" + pendingBatchIds.Count
                + " rd=" + (GameModeManager.IsRD ? 1 : 0)
                + " cur=" + (string.IsNullOrEmpty(cur) ? "(混/无)" : cur)
                + " items=[" + ov + "]  " + (batchTitle ?? ""));
        }
        catch (Exception e)
        {
            Program.DEBUGLOG(e);
        }
    }

    // ---------------------------------------------------------------- 系列改名（需求 3）

    /// <summary>「本卡系列改名」那一格的标题：报有几个系列、以及已经手改了哪些。</summary>
    static string FieldMenuItemHint()
    {
        List<string> names = GameStringHelper.getSetNames(pendingCardSetcode);
        if (names.Count == 0)
        {
            return "本卡系列改名…";
        }
        List<string> changed = new List<string>();
        for (int i = 0; i < names.Count && changed.Count < 2; i++)
        {
            string ov = YGOSharp.CardNameTranslation.GetFieldOverride(names[i]);
            if (!string.IsNullOrEmpty(ov))
            {
                changed.Add(Shrink(names[i], 8) + "→" + Shrink(ov, 8));
            }
        }
        if (changed.Count == 0)
        {
            return names.Count > 1
                ? "本卡系列改名…（" + names.Count + " 个系列）"
                : "本卡系列改名：" + Shrink(names[0], 10);
        }
        string hint = "本卡系列改名：" + string.Join("、", changed.ToArray());
        if (names.Count > changed.Count)
        {
            hint += " 等";
        }
        return hint;
    }

    /// <summary>
    /// 挑要改名的那个系列。只有一个系列就直接进输入框（少一步无意义的点按）；
    /// 多个就列出来让玩家挑 —— 列的是**原生系列名**（与落盘键一致），
    /// 已手改过的在后面标出当前译名，一眼看得出改过没有。
    /// </summary>
    static void OpenFieldChooser()
    {
        if (pendingCardId <= 0 || pendingCardSetcode == 0)
        {
            return;
        }
        List<string> names = GameStringHelper.getSetNames(pendingCardSetcode);
        if (names.Count == 0)
        {
            return;
        }
        if (names.Count == 1)
        {
            OpenFieldInput(names[0]);
            return;
        }
        List<messageSystemValue> options = new List<messageSystemValue>();
        for (int i = 0; i < names.Count; i++)
        {
            string ov = YGOSharp.CardNameTranslation.GetFieldOverride(names[i]);
            string hint = (ov != null ? "● " : "") + names[i]
                + (ov != null ? "　→　" + ov : "");
            options.Add(new messageSystemValue { value = names[i], hint = hint });
        }
        ShowChoice(HashField, options);
        QuickTestTrace.Log("nametrans", "field chooser card=" + pendingCardId
            + " setcode=" + pendingCardSetcode + " n=" + names.Count);
    }

    /// <summary>
    /// **直接**对某个系列（字段）改名 —— 用户 2026-10-04 第二条：在简介里对**亮起的字段**
    /// 按右键（无论是系列行那个名字，还是效果正文里被「」框起来的部分）就进这里。
    ///
    /// <para>与「译」菜单那条路（<see cref="OpenFieldChooser"/>）的分别：那条是"这张卡挂着的
    /// 系列"里挑一个，而这里点的是**卡文里出现的任意字段** —— 它可能根本不是这张卡的系列
    /// （比如一张普通卡的效果里提到「英雄」）。<c>SetFieldOverride</c> 的落盘键就是
    /// <b>原生系列名</b>，所以这里只要求调用方给对名字（右键那条路用 payload 里的**表内码**
    /// 回查原生名，见 <c>CardTextLinker.FieldNativeOf</c>，译名改过也不会串）。</para>
    /// </summary>
    public static void OpenFieldRename(string nativeField)
    {
        try
        {
            if (string.IsNullOrEmpty(nativeField))
            {
                return;
            }
            // pendingCardId 只影响日志与"这张卡"的上下文，改系列本身不依赖它；
            // 顺手对齐成"当前正在看的那张卡"，日志好读。
            CardDescription cd = Program.I() != null ? Program.I().cardDescription : null;
            if (cd != null && cd.showingCard != null && cd.showingCard.Id > 0)
            {
                pendingCardId = cd.showingCard.Id;
                pendingCardSetcode = cd.showingCard.Setcode;
            }
            // 【用户 2026-10-04 第五条】这里**不再**弹确认框（上一条加的那道按新口径撤掉）：
            //   右键一击直达改名输入框；想反悔就点框右上角那颗**圆形 ×**、或直接按右键把框关掉
            //   —— 两者都是"什么都不提交地退出来"。
            QuickTestTrace.Log("nametrans", "field rightclick field=[" + nativeField
                + "] card=" + pendingCardId);
            OpenFieldInput(nativeField);
        }
        catch (Exception e)
        {
            Program.DEBUGLOG(e);
        }
    }

    // ---------------------------------------------------------------- 对话框外壳（右上角圆形 ×）
    //
    // 【用户 2026-10-04 第五条】「译」菜单开出来的那几个框，右上角要有**同一颗圆形 ×**
    //   —— 与**录像界面**（trans_selectReplay 的 exit_）用的是**同一张图**：
    //   transAtlas 的 close 精灵（浅灰圆底 + 深色 ×）。
    //   做法是直接克隆 <c>ES_3cancle</c> 预制体里那颗 <c>cancle_</c>：它本来就是那颗圆形 ×
    //   （同一精灵、20×20、带悬停/按下反馈与音效），克隆＝天生一致，不用自己画。
    //   ⛔ 唯一要自己定的东西是**位置**：锚到这张窗口**可见底板 glass** 的右上角
    //   —— 各张框的底板宽度并不一样（ES2 402 宽、ES3 / ES_input 302 宽），
    //   照抄 ES3 里面那颗的 localPosition 会在宽框上离开角，锚点才稳。
    //   另外挂 <see cref="TransDialogClose"/>：**右键**也关掉这张框。

    /// <summary>我们插进去那颗关闭钮的节点名（同时是幂等判据）。</summary>
    const string CloseNodeName = "transclose_";

    /// <summary>关闭钮的边长与它离底板右/上缘的距离（px）。8px ⇒ 贴上角但不压边。</summary>
    const float CloseSize = 20f;
    const float CloseInset = 8f;

    static void ShowChoice(string hash, List<messageSystemValue> options)
    {
        Self.RMSshow_singleChoice(hash, options);
        DecorateMsWindow();
    }

    static void ShowInputBox(string hash, string hint, string def)
    {
        Self.RMSshow_input(hash, hint, def);
        DecorateMsWindow();
    }

    static void ShowYesNo(string hash, string hint, messageSystemValue yes, messageSystemValue no)
    {
        Self.RMSshow_yesOrNo(hash, hint, yes, no);
        DecorateMsWindow();
    }

    /// <summary>给刚开出来的对话框装上右上角的圆形 × 与「右键关闭」。幂等。</summary>
    static void DecorateMsWindow()
    {
        try
        {
            GameObject w = Self.CurrentMsWindow;
            if (w == null)
            {
                return;
            }
            if (FindDeep(w.transform, CloseNodeName) != null)
            {
                return;
            }
            // 底板（可见的深色圆角面）当锚点参照；pan 是 UIPanel（300×200 裁剪框），
            // 各张框的可见宽度由 glass 决定，所以锚 glass 而不是锚 pan。
            GameObject glass = FindDeep(w.transform, "glass");
            GameObject pan = FindDeep(w.transform, "pan");
            GameObject srcRoot = Program.I() != null ? Program.I().ES_3cancle : null;
            GameObject src = srcRoot != null ? FindDeep(srcRoot.transform, "cancle_") : null;
            UIRect refR = glass != null ? glass.GetComponent<UIRect>() : null;
            GameObject father = pan != null ? pan : w;
            if (src == null || refR == null)
            {
                if (QuickTestTrace.Enabled)
                {
                    QuickTestTrace.Log("nametrans", "dialog close ABORTED src="
                        + (src != null ? 1 : 0) + " ref=" + (refR != null ? 1 : 0));
                }
                return;
            }
            GameObject btn = Program.I().create(src, Vector3.zero, Vector3.zero, false, father, false);
            btn.name = CloseNodeName;
            // 显式激活：克隆体继承源的 activeSelf，源若被藏着就白挂（见 ngui-button-conventions §2）。
            btn.SetActive(true);
            AnchorOffRightTop(btn.GetComponent<UIRect>(), refR,
                CloseSize + CloseInset, CloseInset, CloseInset, CloseSize + CloseInset);
            // 克隆带过来的旧回调要清（源钮在别的窗口里注册过东西时尤其重要）。
            UIButton ub = btn.GetComponent<UIButton>();
            if (ub != null)
            {
                ub.onClick.Clear();
                ub.tweenTarget = btn;
            }
            UIHelper.registEventbtn(btn, CloseNodeName, () => Self.RMSshow_clear());

            TransDialogClose esc = w.GetComponent<TransDialogClose>();
            if (esc == null)
            {
                esc = w.AddComponent<TransDialogClose>();
            }
            esc.onClose = () => Self.RMSshow_clear();
            // ⛔ 必须"上闸"：开这个框的那一击本身就是右键（右键简介里亮起的字段），
            //   而 AddComponent 之后 Update 可能同帧就跑到 ⇒ 不上闸会开了立刻自己关掉。
            esc.Arm();

            if (QuickTestTrace.Enabled)
            {
                QuickTestTrace.Log("nametrans", "dialog close " + ProbeCloseInfo());
            }
        }
        catch (Exception e)
        {
            Program.DEBUGLOG(e);
        }
    }

    /// <summary>
    /// 按名字找子物体（**含失活节点**）——统一走 <see cref="CardSearchWindow.FindDeep"/>，
    /// 不再自留一份递归实现（两处口径一致：都含失活、都按深度优先前序取首个同名节点）。
    /// ⛔ 不能用 <c>UIHelper.getByName</c>：它走 <c>GetComponentsInChildren&lt;T&gt;()</c>
    ///   的默认重载、会跳过失活对象；而这里要的两样（预制体资源里的 <c>cancle_</c>、框里的
    ///   <c>glass</c>）都不是"场景里活着的"那个意思。
    /// </summary>
    static GameObject FindDeep(Transform root, string name)
    {
        return CardSearchWindow.FindDeep(root.gameObject, name);
    }

    /// <summary>
    /// 把 <paramref name="self"/> 的四条边锚到 <paramref name="refR"/> 的**右缘 / 上缘**，
    /// 四个参数都是「距参照物那条边的距离（正数）」。与 <c>CardSearchWindow.AnchorOffRightTop</c>
    /// 同一套口径（两份各留一份：两处互不依赖，改一处不会牵连另一处）。
    /// ⚠ 参数顺序是「**左**缘距 / 右缘距」，写反了控件会左右颠倒而 NGUI 不报错。
    /// </summary>
    static void AnchorOffRightTop(UIRect self, UIRect refR,
        float leftFromRight, float rightFromRight, float topBelowTop, float bottomBelowTop)
    {
        if (self == null || refR == null)
        {
            return;
        }
        self.leftAnchor.target = refR.transform;
        self.leftAnchor.relative = 1f;
        self.leftAnchor.absolute = -Mathf.RoundToInt(leftFromRight);
        self.rightAnchor.target = refR.transform;
        self.rightAnchor.relative = 1f;
        self.rightAnchor.absolute = -Mathf.RoundToInt(rightFromRight);
        self.topAnchor.target = refR.transform;
        self.topAnchor.relative = 1f;
        self.topAnchor.absolute = -Mathf.RoundToInt(topBelowTop);
        self.bottomAnchor.target = refR.transform;
        self.bottomAnchor.relative = 1f;
        self.bottomAnchor.absolute = -Mathf.RoundToInt(bottomBelowTop);
        self.ResetAndUpdateAnchors();
    }

    /// <summary>
    /// 验收用：把当前对话框右上角那颗圆形 × 摊成一行 —— 节点在不在、用的**是不是同一张图**
    /// （<c>sprite=close</c>：与录像界面那颗同源）、落点是不是真在底板右上角、
    /// 以及窗口上有没有挂好「右键关闭」。
    /// </summary>
    public static string ProbeCloseInfo()
    {
        try
        {
            GameObject w = Self.CurrentMsWindow;
            if (w == null)
            {
                return "no window";
            }
            GameObject btn = FindDeep(w.transform, CloseNodeName);
            if (btn == null)
            {
                return "no close node";
            }
            UISprite sp = btn.GetComponent<UISprite>();
            UIWidget uw = btn.GetComponent<UIWidget>();
            GameObject glass = FindDeep(w.transform, "glass");
            UIRect gr = glass != null ? glass.GetComponent<UIRect>() : null;
            bool topRight = false;
            if (uw != null && gr != null)
            {
                // 角序 BL/TL/TR/BR（UIWidget.worldCorners）⇒ 右上角是 [2]。
                Vector3[] a = uw.worldCorners;
                Vector3[] b = gr.worldCorners;
                topRight = Mathf.Abs(a[2].x - b[2].x) <= 30f
                    && Mathf.Abs(a[2].y - b[2].y) <= 30f;
            }
            return "node=1 sprite=" + (sp != null ? sp.spriteName : "?")
                + " size=" + (uw != null ? uw.width + "x" + uw.height : "?")
                + " topRight=" + (topRight ? 1 : 0)
                + " esc=" + (w.GetComponent<TransDialogClose>() != null ? 1 : 0)
                + " x=(" + RectText(uw) + ") glass=(" + RectText(gr) + ")";
        }
        catch (Exception e)
        {
            return "probe failed " + e.GetType().Name;
        }
    }

    /// <summary>验收用：矩形摊成屏幕像素串 <c>x0,y0,x1,y1</c>（图像 y 向下，与实拍同一口径）。</summary>
    static string RectText(UIRect r)
    {
        if (r == null)
        {
            return "none";
        }
        // ⛔ worldCorners 的角序是 BL/TL/TR/BR ⇒ 直接取 [0]/[2] 会得到一个 y 递减的"矩形"
        //   （屏幕 y 翻过来之后 y0 > y1），"钮在底板之内"这种判据会整个判反。四个角一起取 min/max。
        Vector3[] c = r.worldCorners;
        float x0 = float.MaxValue, y0 = float.MaxValue;
        float x1 = float.MinValue, y1 = float.MinValue;
        for (int i = 0; i < 4; i++)
        {
            Vector3 p = Program.camera_main_2d.WorldToScreenPoint(c[i]);
            float sx = p.x;
            float sy = Screen.height - p.y;
            if (sx < x0) { x0 = sx; }
            if (sx > x1) { x1 = sx; }
            if (sy < y0) { y0 = sy; }
            if (sy > y1) { y1 = sy; }
        }
        return Mathf.RoundToInt(x0) + "," + Mathf.RoundToInt(y0)
            + "," + Mathf.RoundToInt(x1) + "," + Mathf.RoundToInt(y1);
    }

    /// <summary>
    /// 验收用：走一遍「右键关闭」的判据（就是 <see cref="TransDialogClose.Tick"/> 那一份），
    /// 返回 <c>age=… closed=… gone=…</c>。进程内送不进真右键（见 notes/harness.md），
    /// 所以这里**把右键状态当参数喂进去** —— 顺带能验证"开框那一帧不许关"那道闸：
    /// 同一帧喂 true 必须 <c>closed=0</c>（否则真机上框会刚开就自己关掉）。
    /// </summary>
    public static string ProbeRightClickTick(bool rightDown)
    {
        try
        {
            GameObject w = Self.CurrentMsWindow;
            if (w == null)
            {
                return "no window";
            }
            TransDialogClose esc = w.GetComponent<TransDialogClose>();
            if (esc == null)
            {
                return "no esc";
            }
            int age = esc.ProbeArmedAge();
            bool closed = esc.Tick(rightDown);
            return "age=" + age + " closed=" + (closed ? 1 : 0)
                + " gone=" + (Self.CurrentMsWindow == null ? 1 : 0);
        }
        catch (Exception e)
        {
            return "probe failed " + e.GetType().Name;
        }
    }

    /// <summary>验收用：点一次那颗圆形 ×（走它自己的 onClick 委托），返回框是不是真没了。</summary>
    public static int ProbeClickClose()
    {
        GameObject w = Self.CurrentMsWindow;
        if (w == null)
        {
            return 0;
        }
        GameObject btn = FindDeep(w.transform, CloseNodeName);
        if (btn == null)
        {
            return 0;
        }
        MonoDelegate d = btn.GetComponent<MonoDelegate>();
        if (d == null || d.actionInMono == null)
        {
            return 0;
        }
        d.actionInMono();
        return Self.CurrentMsWindow == null ? 1 : 0;
    }

    /// <summary>
    /// 验收用：本次进程里改名输入框被开出来的次数。判据是"确认之前不变、点了「修改」之后 +1"
    /// —— 光看 <c>[nametrans] field input</c> 那一行证明不了"没确认就不会开"。
    /// </summary>
    public static int ProbeFieldInputCount()
    {
        return probeFieldInputCount;
    }

    /// <summary>验收用：收掉当前对话框（探针别给玩家留一个开着的框）。</summary>
    public static void ProbeClearDialog()
    {
        Self.RMSshow_clear();
    }

    static int probeFieldInputCount;

    /// <summary>给选中的系列改名的输入框（预填当前显示名；留空 = 还原原生名）。</summary>
    static void OpenFieldInput(string nativeField)
    {
        if (string.IsNullOrEmpty(nativeField))
        {
            return;
        }
        pendingFieldNative = nativeField;
        probeFieldInputCount++;
        string cur = YGOSharp.CardNameTranslation.GetFieldOverride(nativeField);
        string def = cur != null ? cur : nativeField;
        ShowInputBox(HashFieldInput,
            "把系列「" + Shrink(nativeField, 10) + "」改叫什么"
            + "（当前生效：" + YGOSharp.CardNameTranslation.CurrentLabel + "）\n"
            + "留空 = 恢复默认；改名会作用在简介里「系列名」的引用上", def);
        QuickTestTrace.Log("nametrans", "field input card=" + pendingCardId
            + " field=" + nativeField + " cur=" + (cur ?? "(原生)"));
    }

    /// <summary>
    /// 验收用：把「这一批选中某个 key 会怎样」摊成一行，**不真的改盘**。
    /// 判据是「菜单列出的项 = <c>AvailablePacks</c> + 跟随全局，且 RD 下只有原生」，
    /// 以及「● 标在当前口径上」—— 这两样决定玩家看到的菜单对不对。
    /// </summary>
    public static string ProbeBatchPackPlan(List<int> ids)
    {
        try
        {
            List<YGOSharp.CardNameTranslation.Pack> packs =
                YGOSharp.CardNameTranslation.AvailablePacks;
            System.Text.StringBuilder items = new System.Text.StringBuilder();
            items.Append("global(跟随全局)");
            for (int i = 0; i < packs.Count; i++)
            {
                items.Append('|').Append(packs[i].key);
                if (packs[i].missing)
                {
                    items.Append("(缺)");
                }
            }
            return "chooser n=" + (ids != null ? ids.Count : 0)
                + " rd=" + (GameModeManager.IsRD ? 1 : 0)
                + " items=[" + items + "]"
                + " cur=" + YGOSharp.CardNameTranslation.CurrentKey
                + " " + ProbeBatchKey(ids);
        }
        catch (Exception e)
        {
            return "chooser failed " + e.GetType().Name + ": " + e.Message;
        }
    }

    /// <summary>
    /// 验收用：真的把 <paramref name="ids"/> 这一批设成 <paramref name="key"/>
    /// （走玩家那条路：<see cref="ApplyBatchPack"/> 含收尾刷新）。
    /// 探针专用 —— 正式流程里没有调用点。
    /// </summary>
    public static void ProbeApplyBatch(List<int> ids, string key)
    {
        try
        {
            pendingBatchIds = ids != null ? new List<int>(ids) : new List<int>();
            ApplyBatchPack(key);
        }
        catch (Exception e)
        {
            QuickTestTrace.Log("packtrans", "apply failed " + e.Message);
        }
    }

    // ---------------------------------------------------------------- 收结果

    public override void ES_RMS(string hashCode, List<messageSystemValue> result)
    {
        base.ES_RMS(hashCode, result);      // 基类这一步会把当前对话框收掉
        try
        {
            string v = (result != null && result.Count > 0 && result[0] != null)
                ? (result[0].value ?? "") : "";
            if (hashCode == HashMenu)
            {
                if (v == "pack")
                {
                    OpenPackChooser();
                }
                else if (v == "cardpack")
                {
                    OpenCardPackChooser();
                }
                else if (v == "alias")
                {
                    OpenAliasInput();
                }
                else if (v == "field")
                {
                    OpenFieldChooser();
                }
                else if (v == "reset")
                {
                    SetOverride(pendingCardId, "");
                }
            }
            else if (hashCode == HashPack)
            {
                if (v == "help")
                {
                    Self.RMSshow_none(HelpText());
                }
                else if (IsMissing(v))
                {
                    // 缺数据 ≠ 死路：三张表**出厂就带**（随包分发，2026-10-03），
                    // 走到这个分支说明玩家的 translation/ 被清过、或表被删了 ——
                    // 所以第一件事是把「要不要下一份」摆出来，而不是只回一句"还没装"。
                    if (YGOSharp.CardTranslationDownloader.IsRemotePack(v))
                    {
                        AskDownload(v, ModeGlobal);
                    }
                    else
                    {
                        Self.RMSshow_none("这一份还没有数据，名字会先用原生翻译。\n" + HelpText());
                    }
                }
                else if (!YGOSharp.CardNameTranslation.SetCurrent(v))
                {
                    Self.RMSshow_none("读不到这份翻译表：" + v);
                }
                else
                {
                    ApplyChanged("pack=" + YGOSharp.CardNameTranslation.CurrentLabel);
                }
            }
            else if (hashCode == HashCardPack)
            {
                if (v == "alias")
                {
                    // 自定义名字优先于任何翻译表，选完顺手把外号撤掉，
                    // 否则玩家会以为"选了表却没生效"。
                    if (YGOSharp.CardNameTranslation.HasOverride(pendingCardId))
                    {
                        SetOverride(pendingCardId, "");
                    }
                    OpenAliasInput();
                }
                else if (IsMissing(v) && YGOSharp.CardTranslationDownloader.IsRemotePack(v))
                {
                    AskDownload(v, ModeCard);
                }
                else
                {
                    SetCardPack(pendingCardId, v);
                }
            }
            else if (hashCode == HashBatchPack)
            {
                if (IsMissing(v) && YGOSharp.CardTranslationDownloader.IsRemotePack(v))
                {
                    // 缺数据不是死路：先问要不要下，下完按玩家点它时的意图把**这一批**换掉。
                    AskDownload(v, ModeBatch);
                }
                else
                {
                    ApplyBatchPack(v);
                }
            }
            else if (hashCode == HashDownload)
            {
                if (v == "yes")
                {
                    BeginDownload();
                }
            }
            else if (hashCode == HashAlias)
            {
                string typed = (v ?? "").Trim();
                // ⚠ 输入框是**预填当前显示名**的（见 OpenAliasInput），而提交时
                //   MonoListenerRMS_ized.function() 会把**框里当前的文字**当成结果回传
                //   （value 传 null 也不管用 —— 它只要身上有 UIInput 就覆盖掉）。
                //   所以「打开改名框 → 什么都没改 → 确定」原先照样会写一条
                //   override，值还正好等于当前卡名 —— 于是这张卡从此 HasOverride==true，
                //   菜单里从此永远挂着一项「恢复本卡默认译名」。
                //   实测就是这个：a 卡点开改名、什么都不改退出，再点 a 卡就冒出「恢复」。
                // 现在按"真的换了个名字"才落盘；留空仍然是"恢复默认"。
                if (typed.Length > 0 && typed == pendingCardName)
                {
                    QuickTestTrace.Log("nametrans", "alias unchanged id=" + pendingCardId
                        + " name=" + typed + " -> 不落盘");
                    return;
                }
                SetOverride(pendingCardId, typed);
            }
            else if (hashCode == HashField)
            {
                // 选择器回传的就是**原生系列名**（value = getSetNames 的元素），
                // 与落盘键同源，不需要再反查。
                OpenFieldInput((v ?? "").Trim());
            }
            else if (hashCode == HashFieldInput)
            {
                string typed = (v ?? "").Trim();
                // 与 alias 同款的坑：输入框预填的是当前显示名，MonoListenerRMS_ized 提交时
                // 会把框里现在的文字原样回传 —— 所以「打开 → 什么都没改 → 确定」必须判掉。
                // 「留空」与「手输回原生名」都算还原（SetFieldOverride 里两种都会清记录）。
                bool isRestore = typed.Length == 0 || typed == pendingFieldNative;
                if (isRestore && !YGOSharp.CardNameTranslation.HasFieldOverride(pendingFieldNative))
                {
                    QuickTestTrace.Log("nametrans", "field unchanged card=" + pendingCardId
                        + " field=" + pendingFieldNative + " -> 不落盘");
                    return;
                }
                YGOSharp.CardNameTranslation.SetFieldOverride(pendingFieldNative, isRestore ? "" : typed);
                ApplyChanged("field " + pendingFieldNative + " -> "
                    + (isRestore ? "(原生)" : typed));
            }
        }
        catch (Exception e)
        {
            Program.DEBUGLOG(e);
        }
    }

    // ---------------------------------------------------------------- 按需下载

    /// <summary>下载确认框的 hash。</summary>
    const string HashDownload = "nametrans.download";

    /// <summary>下完之后回到「全局换表」那条路。</summary>
    const string ModeGlobal = "global";

    /// <summary>下完之后回到「本卡换表」那条路。</summary>
    const string ModeCard = "card";

    /// <summary>下完之后回到「这一批换表」那条路（检索窗的「译」钮）。</summary>
    const string ModeBatch = "batch";

    static string pendingDownloadKey = "";
    static string pendingDownloadMode = ModeGlobal;
    static bool downloadTickOn;

    /// <summary>
    /// 玩家点了一份「还没装数据、但可以下载」的表：先问一句再动手。
    /// ⚠ 要流量就必须让玩家自己点头，不能后台偷跑。
    ///   （数据**已经**随包分发了 —— 2026-10-03 改口径，见 CardTranslationDownloader
    ///   头注释里两次口径的留档；走到这里说明玩家的表被清过。）
    /// </summary>
    static void AskDownload(string key, string mode)
    {
        pendingDownloadKey = key;
        pendingDownloadMode = mode;
        if (YGOSharp.CardTranslationDownloader.IsBusy)
        {
            MLog("译名表正在下载中，稍等一下。");
            return;
        }
        ShowYesNo(HashDownload,
            "「" + PackLabel(key) + "」还没有数据。\n要从"
            + YGOSharp.CardTranslationDownloader.SourceLabel + "下载一份吗？\n"
            + "（约 2.4 MB，只会下一次；一份里同时有 nw / cnocg / 简中 三套译名，会一起装好）",
            new messageSystemValue { value = "yes", hint = "下载" },
            new messageSystemValue { value = "no", hint = "以后再说" });
        QuickTestTrace.Log("nametrans", "askDownload key=" + key + " mode=" + mode);
    }

    static void BeginDownload()
    {
        MLog("开始下载译名表…");
        YGOSharp.CardTranslationDownloader.Start(OnDownloadDone);
        if (!downloadTickOn)
        {
            downloadTickOn = true;
            Program.go(24, tickDownload);
        }
        QuickTestTrace.Log("nametrans", "beginDownload key=" + pendingDownloadKey
            + " mode=" + pendingDownloadMode);
    }

    /// <summary>下载期间把进度刷到提示条的**最后一行**（每 ~0.4s 一次，只覆盖，不刷屏）。</summary>
    static void tickDownload()
    {
        if (!YGOSharp.CardTranslationDownloader.IsBusy)
        {
            downloadTickOn = false;
            return;
        }
        string st = YGOSharp.CardTranslationDownloader.StatusText();
        if (!string.IsNullOrEmpty(st))
        {
            MLogReplaceLast(st);
        }
        Program.go(24, tickDownload);
    }

    static void OnDownloadDone(bool ok, string summary)
    {
        downloadTickOn = false;
        string key = pendingDownloadKey;
        string mode = pendingDownloadMode;
        MLogReplaceLast(summary ?? "");
        QuickTestTrace.Log("nametrans", "onDownloadDone ok=" + ok + " key=" + key + " mode=" + mode);
        if (!ok)
        {
            return;
        }
        // 下完那份表现在真的有数据了 —— 按玩家点它时的意图走完最后一步。
        if (IsMissing(key))
        {
            MLog("下完了，但这份表还是没数据：" + PackLabel(key));
            return;
        }
        if (mode == ModeCard)
        {
            SetCardPack(pendingCardId, key);
        }
        else if (mode == ModeBatch)
        {
            ApplyBatchPack(key);
        }
        else if (YGOSharp.CardNameTranslation.SetCurrent(key))
        {
            ApplyChanged("pack=" + YGOSharp.CardNameTranslation.CurrentLabel);
        }
    }

    /// <summary>
    /// 设置页的下拉框（<c>Setting.transPack_</c>）选了哪份全局翻译 —— 2026-10-02 需求3。
    /// 与「译」菜单里选全局翻译**走同一条路**（缺数据先问要不要下载、换完重算全池），
    /// 不另起一套判据；两条入口唯一的差别是界面形态。
    /// </summary>
    public static void ApplyGlobalPackFromSetting(string key)
    {
        try
        {
            if (string.IsNullOrEmpty(key))
            {
                return;
            }
            if (IsMissing(key))
            {
                if (YGOSharp.CardTranslationDownloader.IsRemotePack(key))
                {
                    AskDownload(key, ModeGlobal);
                }
                else
                {
                    Self.RMSshow_none("这一份还没有数据，名字会先用原生翻译。\n" + HelpText());
                }
                return;
            }
            if (!YGOSharp.CardNameTranslation.SetCurrent(key))
            {
                Self.RMSshow_none("读不到这份翻译表：" + key);
                return;
            }
            ApplyChanged("pack(setting)=" + YGOSharp.CardNameTranslation.CurrentLabel);
        }
        catch (Exception e)
        {
            Program.DEBUGLOG(e);
        }
    }

    /// <summary>提示条上写一行（走 CardDescription 那条现成的日志区）。</summary>
    static void MLog(string s)
    {
        try
        {
            if (Program.I() != null && Program.I().cardDescription != null)
            {
                Program.I().cardDescription.mLog(s);
            }
        }
        catch (Exception e)
        {
            Program.DEBUGLOG(e);
        }
    }

    /// <summary>提示条**最后一行**改写（下载进度用；不会因为刷新越刷越长）。</summary>
    static void MLogReplaceLast(string s)
    {
        try
        {
            if (Program.I() != null && Program.I().cardDescription != null)
            {
                Program.I().cardDescription.mLogReplaceLast(s);
            }
        }
        catch (Exception e)
        {
            Program.DEBUGLOG(e);
        }
    }

    /// <summary>这个 key 的显示名（列表里有的按 label，没有就原样）。</summary>
    static string PackLabel(string key)
    {
        List<YGOSharp.CardNameTranslation.Pack> packs = YGOSharp.CardNameTranslation.Packs;
        for (int i = 0; i < packs.Count; i++)
        {
            if (packs[i].key == key)
            {
                return packs[i].label;
            }
        }
        return key;
    }

    static void SetOverride(int id, string name)
    {
        if (id <= 0)
        {
            return;
        }
        YGOSharp.CardNameTranslation.SetOverride(id, name);
        ApplyChanged("override id=" + id + " -> " + (string.IsNullOrEmpty(name) ? "(默认)" : name));
    }

    /// <summary>
    /// 单卡换翻译表的收尾。走 <see cref="SetOverride"/> 那条同款路径
    /// （重算卡池 → 刷新说明面板 / 检索窗 / 卡组），所以单卡与全局换表的效果完全一致。
    /// </summary>
    static void SetCardPack(int id, string key)
    {
        if (id <= 0)
        {
            return;
        }
        YGOSharp.CardNameTranslation.SetCardPack(id, key);
        ApplyChanged("cardpack id=" + id + " -> " + CardPackLabel(id));
    }

    /// <summary>
    /// **这一批**卡统一换译名的收尾（检索窗「译」钮选中之后）。
    /// 走 <see cref="SetCardPackBatch"/>（一次 bump + 一次落盘，见那条的注释）
    /// ＋ 与单卡换表**同一个** <see cref="ApplyChanged"/> 收尾，所以批量与单卡换表的
    /// 界面效果完全一致：卡池重算 → 说明面板 / 检索窗 / 卡组三处一起刷新。
    /// </summary>
    static void ApplyBatchPack(string key)
    {
        try
        {
            if (pendingBatchIds == null || pendingBatchIds.Count == 0)
            {
                return;
            }
            int n = pendingBatchIds.Count;
            int changed = YGOSharp.CardNameTranslation.SetCardPackBatch(pendingBatchIds, key);
            // 键的显示名：跟随全局没有 pack label，报成「跟随全局（当前那份）」。
            string label = string.IsNullOrEmpty(key) || key == FollowGlobal
                ? "跟随全局（" + YGOSharp.CardNameTranslation.CurrentLabel + "）"
                : PackLabel(key);
            ApplyChanged("batchpack n=" + n + " changed=" + changed + " -> " + label);
            if (changed == 0)
            {
                MLog("这一批本来就都是「" + label + "」，没改动。");
            }
            else
            {
                MLog("这一批 " + n + " 张卡已改成「" + label + "」（" + changed + " 组）。");
            }
        }
        catch (Exception e)
        {
            Program.DEBUGLOG(e);
        }
    }

    /// <summary>
    /// 验收用：把「这一批现在各自跟着哪一份」摊成一行（不真的改盘）。
    /// 判据是「同一批里点某一项之后，菜单上的 ● 落在那一项」——
    /// 只看 <c>CardPackCount</c> 判不出「改的是不是眼前这批」。
    /// </summary>
    public static string ProbeBatchKey(List<int> ids)
    {
        try
        {
            if (ids == null || ids.Count == 0)
            {
                return "batchkey n=0";
            }
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            int shown = 0;
            for (int i = 0; i < ids.Count && shown < 6; i++)
            {
                int id = ids[i];
                if (id <= 0)
                {
                    continue;
                }
                if (sb.Length > 0)
                {
                    sb.Append('|');
                }
                sb.Append(id).Append('=')
                  .Append(YGOSharp.CardNameTranslation.GetCardPack(id) ?? "(跟随)");
                shown++;
            }
            return "batchkey n=" + ids.Count
                + " common=[" + YGOSharp.CardNameTranslation.BatchPackKey(ids) + "]"
                + " cardPacks=" + YGOSharp.CardNameTranslation.CardPackCount
                + " sample=[" + sb + "]"
                + (ids.Count > 6 ? " …" : "");
        }
        catch (Exception e)
        {
            return "batchkey failed " + e.GetType().Name + ": " + e.Message;
        }
    }

    /// <summary>
    /// 译名变了之后的统一收尾：重算卡池 → 把界面上还开着的地方换成新名字。
    /// 顺序不能反 —— 刷新必须发生在 ReapplyNameTranslation 之后，否则拿到的是旧卡对象。
    /// </summary>
    static void ApplyChanged(string why)
    {
        try
        {
            YGOSharp.CardsManager.ReapplyNameTranslation();

            if (Program.I() != null)
            {
                if (Program.I().cardDescription != null)
                {
                    Program.I().cardDescription.refreshCardNames();
                }
                if (Program.I().cardSearch != null)
                {
                    Program.I().cardSearch.refreshIfShown();
                }
                if (Program.I().deckManager != null)
                {
                    Program.I().deckManager.refreshNames();
                    // 桌面上的卡是 clone 副本，光重印列表救不了它们（见 refreshCardCopies）。
                    Program.I().deckManager.refreshCardCopies();
                }
            }
            QuickTestTrace.Log("nametrans", "apply " + why
                + " pack=" + YGOSharp.CardNameTranslation.CurrentLabel
                + " overrides=" + YGOSharp.CardNameTranslation.OverrideCount);
        }
        catch (Exception e)
        {
            Program.DEBUGLOG(e);
        }
    }

    // ---------------------------------------------------------------- 小工具

    /// <summary>
    /// 验收用：照玩家那条路径走一遍 —— 打开改名框 → **什么都不改** → 确定 → 再看这张卡。
    /// 改之前这一步会把一条「值正好等于当前卡名」的 override 写进盘，于是菜单从那以后
    /// 永远挂着一项「恢复本卡默认译名」。合成的鼠标事件送不进客户端，所以直接在进程内
    /// 复现「提交框里当前文字」这条路径。
    /// </summary>
    public static void ProbeUnchangedSubmit()
    {
        try
        {
            YGOSharp.Card card = CurrentCard();
            if (card == null)
            {
                QuickTestTrace.Log("nametrans", "probeUnchanged: no current card");
                return;
            }
            int id = card.Id;
            pendingCardId = id;
            pendingCardName = card.Name ?? "";
            pendingCardNative = card.nativeName ?? card.Name ?? "";
            string shown = pendingCardName;

            bool hasBefore = YGOSharp.CardNameTranslation.HasOverride(id);
            ShowInputBox(HashAlias, "probe", shown);

            // RMSshow_input 是「带框的」：提交时 MonoListenerRMS_ized 会把框里的文字回传。
            // 这里直接走同一条 ES_RMS 入口，参数就是框里那串预填文字。
            Self.ES_RMS(HashAlias, new List<messageSystemValue> {
                new messageSystemValue { value = shown } });

            bool hasAfter = YGOSharp.CardNameTranslation.HasOverride(id);
            QuickTestTrace.Log("nametrans", "probeUnchanged id=" + id
                + " name=" + shown
                + " hasBefore=" + hasBefore
                + " hasAfter=" + hasAfter
                + " (期望 hasAfter=false)"
                + " overrides=" + YGOSharp.CardNameTranslation.OverrideCount);
        }
        catch (Exception e)
        {
            QuickTestTrace.Log("nametrans", "probeUnchanged failed " + e.Message);
        }
    }

    static string Shrink(string s, int max)
    {
        if (string.IsNullOrEmpty(s) || s.Length <= max)
        {
            return s ?? "";
        }
        return s.Substring(0, max) + "…";
    }

    /// <summary>列表里有没有"玩家点名要、但还没装数据"的翻译表。</summary>
    static bool HasMissing(List<YGOSharp.CardNameTranslation.Pack> packs)
    {
        for (int i = 0; i < packs.Count; i++)
        {
            if (packs[i].missing)
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>这个 key 是不是"还没装数据"的那一份。</summary>
    static bool IsMissing(string key)
    {
        List<YGOSharp.CardNameTranslation.Pack> packs = YGOSharp.CardNameTranslation.Packs;
        for (int i = 0; i < packs.Count; i++)
        {
            if (packs[i].key == key)
            {
                return packs[i].missing;
            }
        }
        return false;
    }

    static string HelpText()
    {
        // ⛔ 2026-10-03：原文第一句是「译名数据不随安装包分发」，那是 **10-02 的初版口径**。
        //   当天量过体积（三张表 845~1013 KB，gzip 336 KB，与一张卡面同量级）就改成
        //   **随完整包分发**，所以这句对玩家来说已经是**错的**——他们出厂就有这三份。
        return "nw / cnocg / 简中 三份译名表**已随安装包附带**，在列表里点一下就能切换；"
             + "删掉了或想更新到新数据，可以在列表里点标着「可下载」的那一项，"
             + "由客户端从" + YGOSharp.CardTranslationDownloader.SourceLabel + "取一份（不会覆盖你已有的）；"
             + "也可以自己放：把整份 cards.cdb 丢进游戏目录的 translation/ 文件夹，"
             + "或写 *.tsv，每行「卡号<TAB>译名」或「=原卡名<TAB>译名」，"
             + "开头可用 #label 中文名 给它起个显示名。";
    }
}
