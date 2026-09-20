using System;
using System.Collections.Generic;
using System.IO;

/// <summary>
/// RD（超速决斗）模式总闸 —— 单客户端双模式的唯一开关。
///
/// 口径（见 <c>_plan_rdmode.md</c>，2026-09-17 定稿 + 2026-09-19 追加）：
///   · **冷启动永远 OCG**（确定性，避免「上次忘切回来以为卡池坏了」）；
///     **不持久化**，模式只在进程内保持 —— 想记住再加 config 一行即可。
///   · 切换是**瞬时的**：装载层保持「启动全装」（RD 与 OCG 各进各的池），
///     切模式只改「查询走哪一池」，零加载等待。
///   · 本类**只放模式判定与模式化口径**，不碰 UI ——
///     主菜单徽标/toast 在 <c>Menu</c>，设置窗口在 <c>Setting</c>，
///     池在 <c>YGOSharp.CardsManager</c>，禁限表在 <c>YGOSharp.BanlistManager</c>。
///
/// ⚠ 两条模式线共用 <c>AIRoom.launch()</c> / <c>HashedButtons</c> / 记牌等一切现有机制 ——
/// 凡 RD 改动碰到通用路径，OCG 全回归必须跑（约束 20 的教训）。
/// </summary>
public static class GameModeManager
{
    public enum Mode
    {
        OCG = 0,
        RD = 1,
    }

    private static Mode current = Mode.OCG;   // 冷启动永远 OCG

    /// <summary>当前模式。别缓存到字段里，随时会变。</summary>
    public static Mode Current
    {
        get { return current; }
    }

    public static bool IsRD
    {
        get { return current == Mode.RD; }
    }

    /// <summary>切换后的广播。UI 层（菜单徽标 / 设置窗口开关行 / 卡组编辑器）都在这里刷新。</summary>
    public static event Action<Mode> Changed;

    public static string ModeLabel
    {
        get { return IsRD ? "RD" : "OCG"; }
    }

    // ============================ 切换 ============================

    /// <summary>切到指定模式。同模式重复调用是空操作（不会重复广播）。</summary>
    public static void Set(Mode mode)
    {
        if (current == mode)
        {
            return;
        }
        current = mode;
        EnsureDeckDir();
        if (QuickTestTrace.Enabled)
        {
            QuickTestTrace.Log("mode", "set -> " + ModeLabel
                + " deck=" + DeckDir
                + " ocgCards=" + YGOSharp.CardsManager.CountOf(false)
                + " rdCards=" + YGOSharp.CardsManager.CountOf(true)
                + " " + RdRaceProbe());
        }
        Action<Mode> handler = Changed;
        if (handler != null)
        {
            try
            {
                handler(mode);
            }
            catch (Exception e)
            {
                UnityEngine.Debug.Log(e);
            }
        }
    }

    public static void Toggle()
    {
        Set(IsRD ? Mode.OCG : Mode.RD);
    }

    /// <summary>
    /// 种族表的探针（只在 qt_debug.on 下走）：一条行同时答三个问题 ——
    ///   ① `raceBits=` 当前模式认多少个种族位（OCG 26 / RD 32）；
    ///   ② `rdRaceNames=` RD 那 6 个新种族的**文案**（RD 才有，OCG 下为空）；
    ///   ③ `rdRaceHits=26:魔导骑士:9|…` 每个新种族位在**当前池**里能搜到几张
    ///      （走 CardsManager.CountOfRace，与编辑器搜索同一套位运算）。
    /// ③ 是给「bit31 符号扩展」那条坑站岗的：勾了电子人搜不到时，这里会直接是 0。
    /// 离线参照（离线读 rd/cdb 的同一口径，见 _probe_rdrace.py）：
    ///   魔导骑士 9 · 多头龙 11 · 欧米茄念动力 5 · 天界战士 16 · 银河 201 · 电子人 10
    /// </summary>
    private static string RdRaceProbe()
    {
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        sb.Append("raceBits=").Append(GameStringHelper.RaceCount);
        sb.Append(" rdRaceNames=[");
        System.Text.StringBuilder hits = new System.Text.StringBuilder();
        for (int bit = 26; bit < GameStringHelper.RaceCount; bit++)
        {
            string name = GameStringHelper.raceName(bit);
            if (name.Length == 0)
            {
                continue;
            }
            if (sb[sb.Length - 1] != '[')
            {
                sb.Append('|');
            }
            sb.Append(name);
            if (hits.Length > 0)
            {
                hits.Append('|');
            }
            hits.Append(bit).Append(':').Append(name).Append(':')
                .Append(IsRD ? YGOSharp.CardsManager.CountOfRace(1u << bit) : 0);
        }
        sb.Append(']').Append(" rdRaceHits=").Append(hits.Length > 0 ? hits.ToString() : "-");
        // 卡组编辑器「种类」下拉在当前模式下会列出哪些档位 —— 判据是
        // 「RD 下没有同调/速射…、有极大/传说卡」，而下拉要点开才看得见，
        // 所以把这份清单也落盘（表的口径见 GameStringHelper.secondTypeBits）。
        sb.Append(" typeItems=[")
            .Append(GameStringHelper.SecondTypeTableSummary())
            .Append(']');
        return sb.ToString();
    }

    // ============================ 卡组目录 ============================

    /// <summary>OCG 卡组目录（老目录，不动）。</summary>
    public const string DeckDirOCG = "deck";

    /// <summary>RD 卡组目录（与 OCG 分家：同名卡组互不覆盖，卡表也不同）。</summary>
    public const string DeckDirRD = "deck_rd";

    public static string DeckDir
    {
        get { return IsRD ? DeckDirRD : DeckDirOCG; }
    }

    public static string DeckDirOf(Mode mode)
    {
        return mode == Mode.RD ? DeckDirRD : DeckDirOCG;
    }

    /// <summary>卡组 ydk 的完整相对路径。全工程统一走这里，别再各写 "deck/"。</summary>
    public static string DeckPath(string deckName)
    {
        return DeckDir + "/" + deckName + ".ydk";
    }

    /// <summary>
    /// 保证当前模式的卡组目录存在。deck/ 由构建铺设（BuildHelper.RuntimeDataDirectories）本来就有，
    /// deck_rd/ 是运行期新建 —— 在切模式与读目录前都要调一次，否则 DirectoryInfo 会抛。
    /// </summary>
    public static void EnsureDeckDir()
    {
        try
        {
            if (!Directory.Exists(DeckDir))
            {
                Directory.CreateDirectory(DeckDir);
            }
        }
        catch (Exception e)
        {
            UnityEngine.Debug.Log(e);
        }
    }

    // ============================ 「动手前先问一句」的设置键 ============================
    //
    // 用户口径（2026-09-19）：**RD 模式下的「召唤前询问」「盖放前询问」与 OCG 独立。**
    // OCG 沿用历史键（老配置零迁移、行为零变化），RD 用带 _rd 后缀的独立键；
    // 设置窗口仍是同一组开关行，值随当前模式刷新，不新增行、不加高窗口。
    // 消费方：Ocgcore.askMSetBeforeSet / askSummonBeforeSummon（每次实时读，不缓存）。

    public const string KeyAskMsetOCG = "askMset_";
    public const string KeyAskMsetRD = "askMset_rd";
    public const string KeyAskSummonOCG = "askSummon_";
    public const string KeyAskSummonRD = "askSummon_rd";

    /// <summary>「盖放怪兽前询问」当前模式该读的那个键。</summary>
    public static string KeyAskMset
    {
        get { return IsRD ? KeyAskMsetRD : KeyAskMsetOCG; }
    }

    /// <summary>「召唤前询问」当前模式该读的那个键。</summary>
    public static string KeyAskSummon
    {
        get { return IsRD ? KeyAskSummonRD : KeyAskSummonOCG; }
    }

    // ============================ 「上次用的卡组」的设置键 ============================
    //
    // 用户口径（2026-09-19）：**OCG 与 RD 各记各的**。
    // 两边卡组目录本来就分家（deck/ 与 deck_rd/，见 DeckDir），名字却共用一个键时，
    // 切到 RD 打开卡组界面会拿 OCG 的卡组名去 deck_rd/ 里找 —— 找不到，列表退回「按时间排」，
    // 玩家每次都要自己再滚一遍。分开存之后，卡组界面一打开就落到上次那一副上。
    // 与上面那组一样：OCG 沿用历史键（老配置零迁移），RD 用带 _rd 后缀的独立键。

    public const string KeyDeckInUseOCG = "deckInUse";
    public const string KeyDeckInUseRD = "deckInUse_rd";

    /// <summary>「上次用的卡组」当前模式该读的那个键。</summary>
    public static string KeyDeckInUse
    {
        get { return IsRD ? KeyDeckInUseRD : KeyDeckInUseOCG; }
    }

    /// <summary>
    /// 配置里还没这一项时的默认名。
    /// OCG 沿用历史默认（"miaowu"，从来就没有这副卡，实际效果 = 列表按时间排）；
    /// RD 给空串 —— RD 的卡组目录是空的或只有玩家自己放的，猜一个名字不如空着，
    /// 列表那边本来就有「没命中就选第一项」的兜底（selectDeck.printFile）。
    /// </summary>
    public static string DefaultDeckInUse
    {
        get { return IsRD ? "" : "miaowu"; }
    }

    /// <summary>读「上次用的卡组」（当前模式那一份）。全工程只此一处拼默认值。</summary>
    public static string DeckInUse
    {
        get { return Config.Get(KeyDeckInUse, DefaultDeckInUse); }
    }

    /// <summary>写「上次用的卡组」（当前模式那一份，不会串到另一边）。</summary>
    public static void SetDeckInUse(string deckName)
    {
        Config.Set(KeyDeckInUse, deckName == null ? "" : deckName);
    }

    // ============================ 禁限表 ============================

    /// <summary>
    /// 判断一张禁限表是不是 RD 表。
    ///
    /// 口径：表名里 **"RD" 作为独立词**出现（现行 RD 补丁给的是 `!2026.7 RD`），
    /// 或者表名**以 RD 开头**（防「RD先行卡」这类写法）。
    /// 不用 `Contains("RD")` 是为了不误伤将来某个含 "RD" 子串的 OCG 表名 ——
    /// 判定只此一处，口径要改就改这里。
    /// </summary>
    public static bool IsRdBanlistName(string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return false;
        }
        string t = name.Trim();
        if (t.StartsWith("RD", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }
        string[] parts = t.Split(
            new char[] { ' ', '\t', '-', '_', '/', '.', '+', '|', '(', ')', '[', ']', '（', '）' },
            StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < parts.Length; i++)
        {
            if (string.Equals(parts[i], "RD", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// 按模式过滤禁限表名清单：OCG 看不到 RD 表，RD 只看到 RD 表。
    /// 过滤后为空时**回落成原清单**（rd/lflist.conf 缺失时 RD 模式仍要能用，不能白屏）。
    /// </summary>
    public static List<string> FilterBanlistNames(List<string> all)
    {
        List<string> filtered = new List<string>();
        for (int i = 0; i < all.Count; i++)
        {
            if (IsRdBanlistName(all[i]) == IsRD)
            {
                filtered.Add(all[i]);
            }
        }
        if (filtered.Count == 0)
        {
            QuickTestTrace.Log("mode", "banlist filter 落空（mode=" + ModeLabel
                + " all=" + all.Count + "）→ 回落成完整清单");
            return all;
        }
        return filtered;
    }

    // ============================ 卡图 / 立绘路径 ============================

    /// <summary>
    /// 卡图与立绘的查找根目录。**全部走这里，别再各写 "picture/"**。
    ///
    /// · OCG：`picture`（老目录，由构建铺设，不动）。
    /// · RD ：`rd/picture`（随包分发，`_unpack_rd.py --all` 铺，约 780MB）。
    ///
    /// 为什么必须分开：RD 卡的码域是 12xxxxxxxx，与 OCG 天然不冲突，两套图并排放着
    /// 也不会串味；但**让 RD 去 picture/ 找**就等于「查不到图」，反之让 OCG 去 rd/picture
    /// 找则会把「RD 数据在 OCG 下不可见」这条口径破坏掉（卡图也是 RD 数据的一部分）。
    /// </summary>
    public static string PictureRoot
    {
        get { return IsRD ? "rd/picture" : "picture"; }
    }

    /// <summary>卡图目录（`<根>/card`）。RD 侧没有 8 版卡图那套历史素材，见 GameTextureManager。</summary>
    public static string CardPictureDir
    {
        get { return PictureRoot + "/card"; }
    }

    /// <summary>立绘目录（`<根>/closeup`）。</summary>
    public static string CloseupPictureDir
    {
        get { return PictureRoot + "/closeup"; }
    }

    // ============================ 人机（AI.Server + WindBot）路径 ============================
    //
    // 为什么要按模式分家（2026-09-19）：AI.Server.exe 的脚本与卡表路径**全是相对 cwd** 的
    // （实测二进制里的字面量：`./script/constant.lua`、`./script/utility.lua`、
    // `./script/procedure.lua`、`./script/special.lua`、`./script/c%d.lua`、
    // `./cdb/%s` + "cards.cdb"、`config/lflist.conf`），而 RD 那套
    // constant/utility/procedure 与 OCG 不是一份东西、不能共存 ⇒
    // **RD 的人机整包放在 rd/ai/，起进程时把 cwd 指过去**（AI.Server.exe 也拷进去一份）。
    // WindBot 同理：Decks/Dialogs 是相对它自己所在目录，而卡表读 `../cdb/cards.cdb` ——
    // 把 RD WindBot 放在 rd/ai/WindBot/ 正好让 `../cdb/` 落在 rd/ai/cdb/，
    // 与 AI.Server 共用同一份合并卡表。
    //
    // ⚠ 下面这些路径与 devtools/unpack_rd.py 的 AI_* 常量、rd_data_layout.md 的布局表
    //   **必须逐字一致**；改一处要同时改三处。

    public const string AiServerExeOCG = "AI.Server.exe";
    public const string AiServerExeRD = "rd/ai/AI.Server.exe";
    /// <summary>AI.Server 的工作目录。RD 必须指到 rd/ai，OCG 就用产物根（"."）。</summary>
    public const string AiServerDirOCG = ".";
    public const string AiServerDirRD = "rd/ai";
    public const string WindBotExeOCG = "WindBot/WindBot.exe";
    public const string WindBotExeRD = "rd/ai/WindBot/WindBot.exe";
    public const string WindBotDirOCG = "WindBot";
    public const string WindBotDirRD = "rd/ai/WindBot";
    public const string BotConfOCG = "config/bot.conf";
    public const string BotConfRD = "rd/bot.conf";

    public static string AiServerExe
    {
        get { return IsRD ? AiServerExeRD : AiServerExeOCG; }
    }

    public static string AiServerDir
    {
        get { return IsRD ? AiServerDirRD : AiServerDirOCG; }
    }

    public static string WindBotExe
    {
        get { return IsRD ? WindBotExeRD : WindBotExeOCG; }
    }

    public static string WindBotDir
    {
        get { return IsRD ? WindBotDirRD : WindBotDirOCG; }
    }

    /// <summary>
    /// 机器人名单。OCG 沿用 config/bot.conf（构建铺设的老位置），
    /// RD 读 rd/bot.conf（unpack_rd.py 从 RD 客户端包里带出来的那一份）。
    /// </summary>
    public static string BotConf
    {
        get { return IsRD ? BotConfRD : BotConfOCG; }
    }

    public const string ReplayDirOCG = "replay";
    /// <summary>RD 录像目录。unpack_rd.py 不铺它 ⇒ 首次写/列时惰性建目录。</summary>
    public const string ReplayDirRD = "rd/replay";

    /// <summary>
    /// 录像目录。RD 与 OCG 分家：RD 局的 .yrp3d 落 rd/replay/，回放窗口也只列本模式那一份，
    /// 两边互不看见（SaveRecord / selectReplay / Ocgcore 收尾改名全走这里，别再写死 "replay/"）。
    /// </summary>
    public static string ReplayDir
    {
        get { return IsRD ? ReplayDirRD : ReplayDirOCG; }
    }

    // ============================ 人机开局参数 ============================
    //
    // 2026-09-19 修：**RD 的初期手卡是 4 张，不是 5 张**（《RUSH DUEL基本规则 2025.4.pdf》
    // 「游戏开始 · 初期手卡：4 张」，贴吧/官网口径一致）。
    //
    // 这个数**不在 core 的规则层**里，而是 AI.Server 的启动参数 ——
    // 见 AIRoom 拼的那串 `7911 -1 5 0 F ?  ?  8000 <初始手牌> 1 0 0 <seed>`，
    // 位置是 argv[8]（同 ygoserver/gframe.cpp 的「start_lp / start_hand / draw_count」口径）。
    // 判定性实测（_probe_rddeal.py，同一份 rd/ai、只改这一个数）：
    //     argv[8]=5 → 第 1 回合手牌 6/5（5 张起手 + 先攻规则抽 1）
    //     argv[8]=4 → 第 1 回合手牌 5/4（**RD 正确值**：4 张起手 + 先攻规则抽 1）
    // 也就是说 RD 的「多抽一张」从来不是 core 的锅，是这里多喂了一张。
    //
    // ⚠ 别把这个数交给 lua 去补：core 的起手是**先于**任何 lua 效果发的牌，
    //   RDRule.lua 那一段（先攻第 1 回合 `Duel.Draw(0,1)`）是在 4/5 张之后**再加** 1 张，
    //   所以只有把起手压到 4 才是「4 + 规则抽 1 = 5」。

    public const int StartHandOCG = 5;
    public const int StartHandRD = 4;

    /// <summary>本局人机的初始手牌数（AIRoom 拼命令行时读它，别再写死字面量）。</summary>
    public static int StartHand
    {
        get { return IsRD ? StartHandRD : StartHandOCG; }
    }
}
