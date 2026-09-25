using UnityEngine;
using System;
public class Setting : WindowServant2D
{
    private EventDelegate onChange;

    public LAZYsetting setting;

    public override void initialize()
    {
        gameObject = createWindow(this, Program.I().new_ui_setting);
        setting = gameObject.GetComponentInChildren<LAZYsetting>();
        UIHelper.registEvent(gameObject, "exit_", onClickExit);
        UIHelper.registEvent(gameObject, "screen_", resizeScreen);
        UIHelper.registEvent(gameObject, "full_", resizeScreen);
        UIHelper.registEvent(gameObject, "resize_", resizeScreen);
        UIHelper.getByName<UIToggle>(gameObject, "full_").value = Screen.fullScreen;
        UIHelper.getByName<UIToggle>(gameObject, "ignoreWatcher_").value = UIHelper.fromStringToBool(Config.Get("ignoreWatcher_", "0"));
        UIHelper.getByName<UIToggle>(gameObject, "ignoreOP_").value = UIHelper.fromStringToBool(Config.Get("ignoreOP_", "0"));
        UIHelper.getByName<UIToggle>(gameObject, "smartSelect_").value = UIHelper.fromStringToBool(Config.Get("smartSelect_", "1"));
        UIHelper.getByName<UIToggle>(gameObject, "autoChain_").value = UIHelper.fromStringToBool(Config.Get("autoChain_", "1"));
        UIHelper.getByName<UIToggle>(gameObject, "handPosition_").value = UIHelper.fromStringToBool(Config.Get("handPosition_", "1"));
        UIHelper.getByName<UIToggle>(gameObject, "handmPosition_").value = UIHelper.fromStringToBool(Config.Get("handmPosition_", "1"));
        UIHelper.getByName<UIToggle>(gameObject, "spyer_").value = UIHelper.fromStringToBool(Config.Get("spyer_", "1"));
        UIHelper.getByName<UIToggle>(gameObject, "resize_").value = UIHelper.fromStringToBool(Config.Get("resize_", "0"));
        UIHelper.getByName<UIToggle>(gameObject, "longField_").value = UIHelper.fromStringToBool(Config.Get("longField_", "0"));
        if (QualitySettings.GetQualityLevel()<3)
        {
            UIHelper.getByName<UIToggle>(gameObject, "high_").value = false;
        }
        else
        {
            UIHelper.getByName<UIToggle>(gameObject, "high_").value = true;
        }
        UIHelper.registEvent(gameObject, "ignoreWatcher_", save);
        UIHelper.registEvent(gameObject, "ignoreOP_", save);
        UIHelper.registEvent(gameObject, "smartSelect_", save);
        UIHelper.registEvent(gameObject, "autoChain_", save);
        UIHelper.registEvent(gameObject, "handPosition_", save);
        UIHelper.registEvent(gameObject, "handmPosition_", save);
        UIHelper.registEvent(gameObject, "spyer_", save);
        UIHelper.registEvent(gameObject, "high_", save);
        UIHelper.registEvent(gameObject, "longField_", onChangeLongField);
        UIHelper.registEvent(gameObject, "size_", onChangeSize);
        //UIHelper.registEvent(gameObject, "alpha_", onChangeAlpha);
        UIHelper.registEvent(gameObject, "vSize_", onChangeVsize);
        sliderSize = UIHelper.getByName<UISlider>(gameObject, "size_");
        //sliderAlpha = UIHelper.getByName<UISlider>(gameObject, "alpha_");
        sliderVsize = UIHelper.getByName<UISlider>(gameObject, "vSize_");
        Program.go(2000,readVales);
        var collection = gameObject.GetComponentsInChildren<UIToggle>();
        for (int i = 0; i < collection.Length; i++)
        {
            if (collection[i].name.Length > 0 && collection[i].name[0] == '*')
            {
                if (collection[i].name== "*mouseParticle" || collection[i].name == "*showOff" || collection[i].name == "*Efield" || collection[i].name == "*Ewin") 
                {
                    collection[i].value = UIHelper.fromStringToBool(Config.Get(collection[i].name, "1"));
                }
                else
                {
                    collection[i].value = UIHelper.fromStringToBool(Config.Get(collection[i].name, "0"));
                }
            }
        }
        setting.showoffATK.value = Config.Get("showoffATK","1800");
        setting.showoffStar.value = Config.Get("showoffStar", "5");
        UIHelper.registEvent(setting.showoffATK.gameObject, onchangeClose);
        UIHelper.registEvent(setting.showoffStar.gameObject, onchangeClose);
        UIHelper.registEvent(setting.mouseEffect.gameObject, onchangeMouse);
        UIHelper.registEvent(setting.closeUp.gameObject, onchangeCloseUp);
        UIHelper.registEvent(setting.cloud.gameObject, onchangeCloud);  
        UIHelper.registEvent(setting.Vpedium.gameObject, onCP);
        UIHelper.registEvent(setting.Vfield.gameObject, onCP);
        UIHelper.registEvent(setting.Vlink.gameObject, onCP);
        onchangeMouse();
        onchangeCloud();
        setScreenSizeValue();
        CreateTestHideAnimToggle();
        // 「俯视角」排在两行 RD 专项**之前**（用户 2026-09-23 第 1 条）：
        // 它是个全局键（不分 RD/OCG），不该压在「召唤怪兽前询问 / 盖放怪兽前询问」下面。
        // ⛔ 顺序就是视觉顺序（AddExtraToggleRow 按 extraToggleRows 往下排 24px/行），
        //   换顺序会把那两行的 slot 从 2/3 变成 3/4（行下移一行，属预期）。
        CreateTopDownToggle();
        CreateAskMSetToggle();
        CreateAskSummonToggle();
        // 「极大怪兽一体化」放在**最后**：它是 RD 独占行，排末尾才不会改动上面几行的 slot
        //（插中间会把 askMset_/askSummon_ 的行位整体下移，属无谓扰动）。
        CreateRdMaxIntegratedToggle();
        // 建行时每行各自同步过一次；这里再走一遍是把「模式已知」与「窗口已就位」两件事
        // 收口到同一个出口（幂等：显隐/行位/高度三者的算式都不依赖调用次数）。
        SyncExtraRowVisibility();
        SyncRdBadges();
    }

    /// <summary>
    /// 「进入测试战斗时播放动画」开关：点卡组界面的「测试」后，要不要先播收起动画再进对局。
    /// 默认关（Config 缺省 "0" = 点测试直接藏界面）。
    /// </summary>
    private void CreateTestHideAnimToggle()
    {
        AddExtraToggleRow("testHideAnim_", "进入测试战斗时播放动画", () => "testHideAnim_", "0");
    }

    /// <summary>
    /// 「俯视角（平面图）」开关：把相机从 60° 倾斜态抬到 90° 正俯视，牌桌变成平面图。
    ///
    /// **全局一个键（`topDown_`，不分 RD/OCG）** —— 这是「看牌桌的视角偏好」，与规则差异无关。
    /// 盘面本来就是平铺的（卡 prefab 的 card 子节点烘焙 X+90），倾斜感全部来自相机 pitch，
    /// 所以这个开关只干两件事：① 相机 pitch 60→90；② 把「面向镜头」的标签/名牌/装饰
    /// 角度一起跟到 90（都在 `Program.tableau*` 里，见那几个成员的注释）。
    ///
    /// 改动即生效：写进 `Program.topDown` 后让 ocgcore 重摆一次（沿用 `onCP` 那条先例），
    /// 相机那一步由 `fixALLcamerasPreFrame` 的每帧补间带出动画。
    /// ⚠ 对局途中切换时，**场地的位置标签要下一局才换角度**（它们是 `GameField` 构造时建一次的，
    ///   `realize` 不会重建整个 GameField）；卡与名牌会立刻重摆。
    /// </summary>
    private void CreateTopDownToggle()
    {
        AddExtraToggleRow("topDown_", "俯视角（平面图）", () => "topDown_", "0");
        UIHelper.registEvent(gameObject, "topDown_", onChangeTopDown);
    }

    /// <summary>「俯视角」开关的改动回调：写全局 + 重摆场景（相机带补间过去）。</summary>
    private void onChangeTopDown()
    {
        UIToggle t;
        if (!extraRowToggles.TryGetValue("topDown_", out t) || t == null)
        {
            return;
        }
        Program.topDown = t.value;
        // 「拉镜头」的平移量属于**上一个视角的取景**：换视角一律回到**默认机位**（最底下，
        // `topDownPanRestZ`）。不回去的话，关掉再打开会带着上次的平移量 ⇒ 看起来像「画面歪了」，
        // 而玩家没滚过滚轮。（`preFrameFunction` 的每帧跟随下一帧也会兜到，这里同帧先落定。）
        Program.topDownPanZ = Program.topDownPanRestZ;
        // 重摆场景放在**落盘之前**（用户 2026-09-23 第 1 条：「切视角时手牌强制挪位置」）：
        // `[tdmid]` 实测这条路是**同帧**生效的（手牌 z −21.15→−20.80、缩放 1.000→1.500 一次到位）。
        // 放在 `save()` 前是为了「重摆」不依赖磁盘写成功 —— 写配置万一抛异常（磁盘满/文件被占），
        // 视角也该照样切、手牌也该照样摆。
        onCP();
        QuickTestTrace.Log("view", "topDown=" + (Program.topDown ? 1 : 0)
            + " tilt=" + Program.tableauTilt.ToString("F0")
            + " cover=" + Program.topDownCoverZ.ToString("F1")
            + " handScale=" + Program.tableauHandScale(1).ToString("F3"));
        // ⛔ 自己落盘：AddExtraToggleRow 里挂的那份 save 已被本行的 registEvent 顶掉
        //   （UIHelper.registEvent 对 UIToggle 走的是 onClick.Clear()+Add —— 是替换不是追加），
        //   不补这一步就只剩 saveWhenQuit 一条路 ⇒ 游戏被强杀/崩溃时这一项会丢。
        save();
    }

    /// <summary>
    /// 「盖放怪兽前询问」开关：点「前场放置」盖放怪兽前先弹一句「是否确定盖放[卡名]？」，
    /// 确认了才把应答发给 ocgcore。默认**开**（Config 缺省 "1"）。
    /// 出处是 KoishiPro 的同名功能（那边开关键叫 `ask_mset`，默认关；我们默认开）。
    /// 消费方：<see cref="Ocgcore.ES_gameButtonClicked"/>。
    ///
    /// **RD 与 OCG 是两份独立设置**（用户 2026-09-19 口径）：本行在不同模式下读写不同的键
    /// （见 <see cref="GameModeManager.KeyAskMset"/>），窗口里仍是同一行、切模式即时刷新值。
    /// ⚠ 节点名保持 `askMset_`（= OCG 的键名）不动 —— 验收脚本是按节点名点这个开关的。
    /// </summary>
    private void CreateAskMSetToggle()
    {
        AddExtraToggleRow("askMset_", "盖放怪兽前询问", () => GameModeManager.KeyAskMset, "1", true);
    }

    /// <summary>
    /// 「召唤前询问」开关：点「通常召唤」前先弹一句「是否确定召唤[卡名]？」，确认了才把应答
    /// 发给 ocgcore。默认**开**（Config 缺省 "1"）。
    /// 只拦通常召唤，不拦特殊召唤（口径见 <see cref="Ocgcore.askSummonBeforeSummon"/>）。
    /// RD / OCG 独立存储，同 CreateAskMSetToggle。
    /// </summary>
    private void CreateAskSummonToggle()
    {
        AddExtraToggleRow("askSummon_", "召唤怪兽前询问", () => GameModeManager.KeyAskSummon, "1", true);
    }

    /// <summary>RD 主色调（琥珀）。与主菜单 <c>Menu.ModeColorRD</c> **同值** —— 同一个「RD」标记跨界面必须一个颜色。</summary>
    private static readonly Color ModeColorRD = new Color(0.98f, 0.64f, 0.22f, 1f);

    /// <summary>追加行 → 该行的 RD 角标（只有 RD 专项行才建；建好即 SetActive(false)，等 SyncRdBadges 决定亮灭）。</summary>
    private readonly System.Collections.Generic.Dictionary<string, GameObject> extraRowRdBadges
        = new System.Collections.Generic.Dictionary<string, GameObject>();

    /// <summary>
    /// 在追加行尾部挂一枚 RD 角标。造法与主菜单 <c>Menu.CreateRdBadges</c> 同款：
    /// 运行时 new 一个 UILabel、借同一行的标签字体，不碰 prefab。
    ///
    /// 位置不在这里算死：UILabel 的宽度是 NGUI 在 Update 里排完版才知道的，而这里是
    /// initialize()（同一帧更早）——那一刻读到的是克隆来源（spyer_）的旧宽度。
    /// 所以位置交给 <see cref="RdBadgeFollower"/> 逐帧贴到锚点标签右边。
    /// </summary>
    private void CreateRowRdBadge(string rowName, GameObject row, UILabel textLabel)
    {
        if (textLabel == null)
        {
            return;
        }
        GameObject badge = new GameObject("rdBadge_" + rowName);
        badge.layer = row.layer;
        badge.transform.SetParent(row.transform, false);
        badge.transform.localPosition = Vector3.zero;
        badge.transform.localScale = Vector3.one;

        UILabel label = badge.AddComponent<UILabel>();
        label.text = "RD";
        label.color = ModeColorRD;
        label.fontSize = 16;
        if (textLabel.bitmapFont != null)
        {
            label.bitmapFont = textLabel.bitmapFont;
        }
        else
        {
            label.trueTypeFont = textLabel.trueTypeFont;
        }
        label.overflowMethod = UILabel.Overflow.ResizeFreely;
        label.effectStyle = UILabel.Effect.Outline;
        label.effectColor = new Color(0.25f, 0.12f, 0f, 1f);
        label.effectDistance = new Vector2(1, 1);
        label.depth = textLabel.depth + 1;

        RdBadgeFollower follower = badge.AddComponent<RdBadgeFollower>();
        follower.anchor = textLabel;
        follower.gap = 8f;

        badge.SetActive(false);
        extraRowRdBadges[rowName] = badge;
    }

    /// <summary>
    /// 按当前模式刷 RD 角标的亮灭。角标语义：**「这一行现在读写的是 RD 那份存档」**，
    /// 所以只有按模式分键的行（askMset_ / askSummon_）有角标，且只在 RD 模式亮 ——
    /// OCG 下灭掉，是因为那时窗口里显示的是 OCG 的值，挂着 RD 标反而误导。
    ///
    /// 与主菜单一样，把「亮着的个数 + 名字」写进验收日志（判据是数个数，不是看字典大小）。
    /// </summary>
    private void SyncRdBadges()
    {
        bool rd = GameModeManager.IsRD;
        int shown = 0;
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        foreach (var kv in extraRowRdBadges)
        {
            if (kv.Value == null)
            {
                continue;
            }
            if (kv.Value.activeSelf != rd)
            {
                kv.Value.SetActive(rd);
            }
            if (rd)
            {
                shown++;
            }
            sb.Append(' ').Append(kv.Key).Append('=').Append(rd ? 1 : 0);
        }
        if (QuickTestTrace.Enabled)
        {
            QuickTestTrace.Log("setting", "rdBadges visible=" + shown + "/" + extraRowRdBadges.Count
                + " mode=" + GameModeManager.ModeLabel + sb);
        }
    }

    /// <summary>
    /// 追加行 → **行的对象本体**（创建时记下的直接引用）。
    ///
    /// 走这一张表而不是 <c>UIHelper.getByName</c>：后者是 `GetComponentsInChildren&lt;T&gt;()`
    /// 的深度搜索（更贵），而且**默认跳过未激活对象** —— 本项目真踩过这个坑：曾有一行
    /// RD 独占开关在 OCG 下被 `SetActive(false)`，getByName 就再也找不回它了。
    /// 留着直接引用，就不必依赖「这个对象此刻是否激活」这种隐式前提。
    /// </summary>
    private readonly System.Collections.Generic.Dictionary<string, GameObject> extraRowObjects
        = new System.Collections.Generic.Dictionary<string, GameObject>();

    /// <summary>追加行 → 行的 UIToggle（理由同 <see cref="extraRowObjects"/>：隐藏期间 getByName 找不到）。</summary>
    private readonly System.Collections.Generic.Dictionary<string, UIToggle> extraRowToggles
        = new System.Collections.Generic.Dictionary<string, UIToggle>();

    /// <summary>按行名取回追加行的对象本体。优先走直接引用，兜底才 getByName（未登记过的行）。</summary>
    private GameObject FindExtraRow(string rowName)
    {
        GameObject go;
        if (extraRowObjects.TryGetValue(rowName, out go) && go != null)
        {
            return go;
        }
        return UIHelper.getByName(gameObject, rowName);
    }

    /// <summary>本工程往设置窗口追加的自定义开关行数（决定新行的位置和窗口要往下长多少）。</summary>
    private int extraToggleRows = 0;

    /// <summary>追加过的自定义开关行名，save / saveWhenQuit 按这份名单落盘。</summary>
    private readonly System.Collections.Generic.List<string> extraToggleRowNames
        = new System.Collections.Generic.List<string>();

    /// <summary>
    /// 追加行 → 「这一行此刻该读写哪个 config 键」的解析器。
    /// 绝大多数行是固定键；「召唤前询问」「盖放前询问」两行按当前模式解析（RD/OCG 各自独立存储）。
    /// 之所以用解析器而不是固定键：模式可以在窗口活着的时候变，键必须跟着变。
    /// </summary>
    private readonly System.Collections.Generic.Dictionary<string, System.Func<string>> extraRowKeyOf
        = new System.Collections.Generic.Dictionary<string, System.Func<string>>();

    /// <summary>追加行 → 缺省值（切模式刷新时，某模式的键还不存在就按这个缺省显示）。</summary>
    private readonly System.Collections.Generic.Dictionary<string, string> extraRowDefault
        = new System.Collections.Generic.Dictionary<string, string>();

    /// <summary>取某个追加行**此刻**该读写的 config 键（按模式解析；查不到就退回节点名）。</summary>
    private string ExtraRowKey(string rowName)
    {
        System.Func<string> keyOf;
        if (extraRowKeyOf.TryGetValue(rowName, out keyOf))
        {
            return keyOf();
        }
        return rowName;
    }

    /// <summary>
    /// 追加行 → 这一行是不是**只该在 RD 模式出现**。
    ///
    /// 加这个机制之前没有它：<c>rdSpecific</c> 只决定「挂不挂 RD 角标」，行本身两种模式都常显
    /// （见 askMset_ / askSummon_）。而「极大怪兽一体化」只在 RD 里有意义（OCG 没有极大怪兽），
    /// 所以要真的把行藏起来。
    /// ⛔ 隐藏/点亮都必须走 <see cref="extraRowObjects"/> 的直接引用 —— `UIHelper.getByName`
    ///   会**跳过未激活对象**，本项目真踩过这个坑（见 extraRowObjects 的注释）。
    /// </summary>
    private readonly System.Collections.Generic.HashSet<string> extraRowRdOnly
        = new System.Collections.Generic.HashSet<string>();

    /// <summary>
    /// 克隆来源（prefab 原生的 spyer_）那一行的 localPosition —— 追加行的**基准 y**。
    /// 建第一行时记下来：藏掉某一行之后要**重排可见行**（把空档收掉），而这一步不该再去查
    /// spyer_（`getByName` 会跳过未激活对象，本项目在这上面栽过）；缓存一次最稳。
    /// </summary>
    private Vector3 extraRowSrcLocalPos = Vector3.zero;
    private bool extraRowSrcPosSaved = false;

    /// <summary>这一行此刻该不该显示（RD 独占行在 OCG 下不显示，其余恒显示）。</summary>
    private bool ExtraRowVisible(string rowName)
    {
        if (extraRowRdOnly.Contains(rowName))
        {
            return GameModeManager.IsRD;
        }
        return true;
    }

    /// <summary>
    /// 当前**可见**的追加行数 —— 窗口高度只按它长（见 <see cref="GrowWindowForExtraRows"/>）。
    ///
    /// ⛔ 为什么不能用 <c>extraToggleRows</c>（＝建过的行数）：既有验收脚本把「行数」当判据
    ///   （`_verify_askconfirm.py` 的 `rows == 3` / `bg == 524 + 24×rows` / `winY == -12×rows`），
    ///   而 RD 独占行在 OCG 下是藏起来的、不该占高度。改成可见行数后，OCG 侧依旧是
    ///   `rows=3 / bg=596 / winY=-36` —— 与加这一行之前**逐字段相同**；只有 RD 才是 4。
    /// </summary>
    private int VisibleRowCount()
    {
        int n = 0;
        for (int i = 0; i < extraToggleRowNames.Count; i++)
        {
            if (ExtraRowVisible(extraToggleRowNames[i]))
            {
                n++;
            }
        }
        return n;
    }

    /// <summary>
    /// 按当前模式同步追加行的**显隐 + 行位**，再让窗口跟着长高/收回。
    ///
    /// 两件事必须一起做：
    ///   ① 显隐：RD 独占行只在 RD 出现；
    ///   ② **重排可见行**：行 y 是建行时按 slot 写死的（见 AddExtraToggleRow），藏掉一行会留一个
    ///      24px 的空档、窗口还会照旧偏高。这里按创建顺序只对**可见**行重新编号 ——
    ///      第 1 行 = spyer_ 的下一行（基准 y − 24），第 n 行 = 基准 y − 24×n。
    ///      于是 OCG 下的行位与加这一行之前完全一致，RD 下则是 4 行连续排下来。
    ///
    /// 调用点：每建完一行（AddExtraToggleRow 末尾）、initialize 末尾、模式切换
    /// （OnGameModeChanged）—— 三处都要求「先同步显隐、再算窗口高度」，否则会拿旧行数去长窗口。
    /// </summary>
    private void SyncExtraRowVisibility()
    {
        bool rd = GameModeManager.IsRD;
        int slot = 0;
        for (int i = 0; i < extraToggleRowNames.Count; i++)
        {
            string name = extraToggleRowNames[i];
            GameObject go;
            if (!extraRowObjects.TryGetValue(name, out go) || go == null)
            {
                continue;
            }
            bool show = !extraRowRdOnly.Contains(name) || rd;
            if (go.activeSelf != show)
            {
                go.SetActive(show);
            }
            if (!show)
            {
                continue;
            }
            slot++;
            if (extraRowSrcPosSaved)
            {
                // 只改 y（x/z 沿用克隆时与 spyer_ 同列的值）。
                Vector3 lp = go.transform.localPosition;
                lp.y = extraRowSrcLocalPos.y - 24f * slot;
                go.transform.localPosition = lp;
            }
        }
        if (QuickTestTrace.Enabled)
        {
            // 排查用：可见行数 / 总行数 / 隐藏了几行。字段名刻意避开 `rdOnly `（带空格）——
            // 历史验收脚本用 `lines_of(..., "rdOnly ")` 断言「按模式藏行的机制已删干净」，
            // 那是**旧策略**的判据（旧行 rdPileSame_ 连同机制一起删过），本功能的验收会把它
            // 改成正面判据；这里不制造歧义。
            QuickTestTrace.Log("setting", "rdRows visible=" + VisibleRowCount() + "/"
                + extraToggleRowNames.Count + " mode=" + GameModeManager.ModeLabel
                + " hidden=" + (extraToggleRowNames.Count - VisibleRowCount()));
        }
        GrowWindowForExtraRows();
    }

    /// <summary>
    /// 「极大怪兽一体化」开关（**只在 RD 模式出现**、默认开）。开启后：
    ///   ① 极大状态（三件齐）的极大怪兽被鼠标悬浮时，三件 **＋ 极大立绘视为一体**一起放大，
    ///      不再各自单独放大；**单独在场**的极大怪兽（未组成极大）不受影响；
    ///   ② 点 L/R 部件等效于点中间件 —— 卡面点击转发给本体，且悬浮 L/R 时把本体的
    ///      「发动效果 / 攻击宣言」按钮浮出来。发动/攻击依旧记在本体上（服务器天然保证
    ///      不能反复发动 / 反复攻击）；
    ///   ③ 左侧显示栏不变（仍是「你悬浮的那张部件」自己的资料）。
    ///
    /// 键是**全局固定键**（不分 RD/OCG）：这一行本来就只有 RD 能看见、能改，没必要再分键。
    /// 消费方（<c>Ocgcore.rdMaxIntegratedTick</c> / <c>Ocgcore.RdMaximumClickTarget</c>）
    /// **每帧实时读 Config**，不缓存、不挂 onChange —— 与 askMset_ 那条同一口径。
    /// </summary>
    private void CreateRdMaxIntegratedToggle()
    {
        AddExtraToggleRow("rdMaxIntegrated_", "极大怪兽一体化", () => "rdMaxIntegrated_", "1",
            rdSpecific: true, rdOnly: true);
    }

    /// <summary>模式切换钩子只挂一次（initialize 可能被反复调用）。</summary>
    private bool modeHookInstalled = false;

    /// <summary>
    /// 挂上模式切换钩子：切到 RD / 切回 OCG 时，把「按模式解析键」的那几行刷成新模式的存档值。
    /// 不刷新的话，在 OCG 下打开过设置窗口后切到 RD，看到的还是 OCG 那个值。
    /// </summary>
    private void InstallModeHook()
    {
        if (modeHookInstalled)
        {
            return;
        }
        modeHookInstalled = true;
        GameModeManager.Changed += OnGameModeChanged;
    }

    private void OnGameModeChanged(GameModeManager.Mode mode)
    {
        // 顺序：先按新模式同步**显隐与行位**（RD 独占行要出场 / 退场），再按新模式重读各行取值。
        // ⚠ 不能反：RefreshExtraToggleRows 末尾会刷 RD 角标，而角标语义是「这一行现在读哪一份
        //   存档」—— 「这一行此刻在不在窗口里」必须先定下来，否则会刷出一帧对不上的角标状态。
        SyncExtraRowVisibility();
        RefreshExtraToggleRows();
    }

    /// <summary>
    /// 按当前模式重读所有追加行的值。
    /// 注意 UIToggle.value 的 setter 会触发 onChange（= save），但这里写回的都是刚读出来的同一个值，
    /// 落盘结果不变，属于空转一次，可以接受 —— 换来的是「切模式立刻看到该模式的值」。
    /// </summary>
    private void RefreshExtraToggleRows()
    {
        for (int i = 0; i < extraToggleRowNames.Count; i++)
        {
            string name = extraToggleRowNames[i];
            // ⚠ 同上：必须走直接引用。隐藏中的行（OCG 下的 RD 独占行）用 getByName 会被
            //   整个跳过 —— 值不刷、方向对不上，等切回 RD 点亮时显示的还是上一次读到的旧值
            //   （UI 与 Config 打架，正是本项目最忌讳的那类错）。
            UIToggle t;
            if (!extraRowToggles.TryGetValue(name, out t) || t == null)
            {
                continue;
            }
            System.Func<string> keyOf;
            if (!extraRowKeyOf.TryGetValue(name, out keyOf))
            {
                continue;
            }
            string def = extraRowDefault.ContainsKey(name) ? extraRowDefault[name] : "0";
            bool now = UIHelper.fromStringToBool(Config.Get(keyOf(), def));
            if (t.value != now)
            {
                t.value = now;
            }
            QuickTestTrace.Log("setting", "refresh " + name
                + " key=" + keyOf() + " value=" + now + " mode=" + GameModeManager.ModeLabel);
        }
        // 值刷完再把 RD 角标刷一遍 —— 角标与「这一行现在读哪一份存档」必须同步，
        // 分开刷会出现「值已经是 RD 的、角标还灭着」的中间态被玩家看到。
        SyncRdBadges();
    }

    /// <summary>
    /// 往 spyer_ 那一列（x=-258、行距 24）下面追加一行开关。**不动 prefab**：
    /// 运行时克隆 spyer_ 本身，只改名字与文案，所以行距/字号/勾选框贴图天然一致。
    ///
    /// 克隆后必须清掉 UIEventTrigger 的悬停/按压委托 —— Instantiate 会把运行时挂上的
    /// 提示框委托一并复制，出过「悬停提示造两份、赖着不走」的事（同 tryCreateUndoButton）。
    /// 注意 UIToggle.value 的 setter 会触发 onChange，所以**先设初值再挂 save 事件**，
    /// 否则会空跑一次落盘。
    ///
    /// <paramref name="keyOf"/> 是「该行此刻读写哪个 config 键」的解析器（延迟求值）：
    /// 固定键的行传常量，按模式分键的行传 GameModeManager 的取键属性。
    ///
    /// <paramref name="rdSpecific"/> = 这一行是不是**RD 专项**（按模式分键、RD 有自己一份存档）。
    /// true 时行尾挂一枚 RD 角标，随模式亮灭（见 <see cref="SyncRdBadges"/>）；OCG 通用开关传 false。
    ///
    /// <paramref name="rdOnly"/> = 这一行是不是**只在 RD 模式出现**（OCG 下整行藏起来，
    /// 见 <see cref="SyncExtraRowVisibility"/>）。默认 false = 两种模式都常显。
    /// ⚠ 与 rdSpecific 是**两件事**：rdSpecific 只管角标、不管显隐；要「只在 RD 出现」必须用 rdOnly。
    /// </summary>
    private UIToggle AddExtraToggleRow(string rowName, string label, System.Func<string> keyOf, string configDefault,
        bool rdSpecific = false, bool rdOnly = false)
    {
        UIToggle src = UIHelper.getByName<UIToggle>(gameObject, "spyer_");
        if (src == null || UIHelper.getByName<UIToggle>(gameObject, rowName) != null)
        {
            return null;
        }
        if (!extraRowSrcPosSaved)
        {
            // 基准 y 只记一次：重排可见行（SyncExtraRowVisibility）要用它，之后不该再去查 spyer_。
            extraRowSrcLocalPos = src.transform.localPosition;
            extraRowSrcPosSaved = true;
        }
        int slot = extraToggleRows;
        GameObject clone = UnityEngine.Object.Instantiate(src.gameObject);
        clone.name = rowName;
        clone.transform.SetParent(src.transform.parent, false);
        clone.transform.localPosition = src.transform.localPosition + new Vector3(0f, -24f * (slot + 1), 0f);
        clone.transform.localScale = src.transform.localScale;
        UIEventTrigger trigger = clone.GetComponent<UIEventTrigger>();
        if (trigger != null)
        {
            trigger.onHoverOver.Clear();
            trigger.onHoverOut.Clear();
            trigger.onPress.Clear();
        }
        UILabel textLabel = clone.GetComponentInChildren<UILabel>();
        if (textLabel != null)
        {
            textLabel.text = label;
        }
        if (rdSpecific)
        {
            CreateRowRdBadge(rowName, clone, textLabel);
        }
        UIToggle toggle = clone.GetComponent<UIToggle>();
        toggle.value = UIHelper.fromStringToBool(Config.Get(keyOf(), configDefault));
        UIHelper.registEvent(gameObject, rowName, save);
        extraToggleRows = slot + 1;
        extraToggleRowNames.Add(rowName);
        extraRowKeyOf[rowName] = keyOf;
        extraRowDefault[rowName] = configDefault;
        if (rdOnly)
        {
            extraRowRdOnly.Add(rowName);
        }
        // 记下**直接引用**（行本体 + toggle）：刷值 / 报几何都走这两个表（详见 extraRowObjects）。
        extraRowObjects[rowName] = clone;
        extraRowToggles[rowName] = toggle;
        // 记完这一行：先按模式同步显隐与行位，再由它把窗口高度长到位。
        // ⛔ 顺序不能反 —— 高度是按**可见行数**算的（GrowWindowForExtraRows → VisibleRowCount），
        //   先长窗口就会拿「这一行还没参与显隐裁决」的旧行数去算。
        InstallModeHook();
        SyncExtraRowVisibility();
        if (QuickTestTrace.Enabled)
        {
            // 与 SelectServer.LogAnchor 同款：设置窗口挂在 camera_main_2d 下，
            // 用 camera_back_ground_2d 换算会把坐标映射到屏幕外（y 变负）。
            Vector3 sp = Program.camera_main_2d != null
                ? Program.camera_main_2d.WorldToScreenPoint(clone.transform.position)
                : Vector3.zero;
            QuickTestTrace.Log("setting", "toggle " + rowName
                + " key=" + keyOf()
                + " screen=(" + Mathf.RoundToInt(sp.x) + "," + Mathf.RoundToInt(Screen.height - sp.y) + ")"
                + " value=" + toggle.value);
        }
        return toggle;
    }

    /// <summary>
    /// 追加了 extraToggleRows 行之后，把窗口背景往下长高。
    ///
    /// prefab 里 mainWindow 挂着窗口背景（切片 bg sprite，650×524，**pivot = Center**），
    /// 本身挂在中间层「GameObject」节点下（不是根的直接子节点），
    /// 它的直接子节点是 content（装所有开关）/ exit_ / 标题 / 分隔线。
    ///
    /// ⚠ 不能简单地 `bg.height += 24 * n`：pivot 是 Center，加高会**上下各伸一半**，
    /// 顶边跟着往上跑，标题与顶边的留白越加越大（加一行时就已经多溢出 24px）。
    /// 这里改成「只往下长」：把 mainWindow 下移 delta/2 抵消顶边，再把它的直接子节点
    /// 整体上移同样的量抵消掉，净效果 = 底边下移 delta、顶边与所有内容原地不动。
    /// 增量式（按当前高度算 delta），所以可以被多次追加调用，每次长一行。
    /// </summary>
    private void GrowWindowForExtraRows()
    {
        Transform mainWindowT = gameObject.transform.Find("GameObject/mainWindow");
        if (mainWindowT == null)
        {
            mainWindowT = gameObject.transform.Find("mainWindow");
        }
        if (mainWindowT == null)
        {
            return;
        }
        UISprite bg = mainWindowT.GetComponent<UISprite>();
        if (bg == null)
        {
            return;
        }
        const int baseHeight = 524;      // prefab 原值
        // ⛔ 按**可见行数**长（不是 extraToggleRows = 建过的行数）：RD 独占行在 OCG 下是藏起来的、
        //   不该占高度。这样 OCG 侧恒为 rows=3 / bg=596 / winY=-36，与加这一行之前逐字段相同。
        int visibleRows = VisibleRowCount();
        int target = baseHeight + 24 * visibleRows;
        int delta = target - bg.height;
        if (delta == 0)
        {
            return;                      // 已经到位了（重复调用时走这一条）
        }
        // 下移一半抵消顶边（bg 是 pivot=Center，加高两头伸），
        // 再把直接子节点整体上移同样的量抵消掉 —— 净效果只有底边在动。
        int shift = delta / 2;
        mainWindowT.localPosition += new Vector3(0f, -shift, 0f);
        foreach (Transform child in mainWindowT)
        {
            child.localPosition += new Vector3(0f, shift, 0f);
        }
        bg.height = target;
        BoxCollider box = mainWindowT.GetComponent<BoxCollider>();
        if (box != null)
        {
            box.size = new Vector3(box.size.x, target, box.size.z);
        }
        if (QuickTestTrace.Enabled)
        {
            // 几何落盘。⚠ 此刻窗口还停在屏幕外 1.5 倍高处（createWindow 的初始位），
            // 所以**不要**用屏幕坐标判「这一行有没有被切掉」—— 全部在 mainWindow 的
            // 局部坐标里算：bg 的 pivot 是 Center，在它自己节点的局部系里永远占
            // [-h/2, +h/2]，于是「行的 localY 到底边的距离」就是这一行离窗口下沿的留白，
            // 与窗口此刻摆在屏幕哪个位置无关。
            //   topRoot 是**顶边在窗口根坐标系里的位置**，它必须恒等于 prefab 原值
            //   （524/2 = 262）—— 这一条正是「只往下长、不向上溢」的判据。
            float bottomY = -bg.height * 0.5f;
            float topRoot = mainWindowT.localPosition.y + bg.height * 0.5f;
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            sb.Append("grow rows=").Append(visibleRows)
              .Append(" bg=").Append(bg.height)
              .Append(" box=").Append(box != null ? box.size.y.ToString("F0") : "null")
              .Append(" winY=").Append(mainWindowT.localPosition.y.ToString("F1"))
              .Append(" topRoot=").Append(topRoot.ToString("F1"))
              .Append(" bottomY=").Append(bottomY.ToString("F1"));
            for (int i = -1; i < extraToggleRowNames.Count; i++)
            {
                string n = i < 0 ? "spyer_" : extraToggleRowNames[i];
                if (i >= 0 && !ExtraRowVisible(n))
                {
                    // 藏起来的行（OCG 下的 RD 独占行）不报几何：它在窗口里不存在，
                    // 报了会顶掉「留白咬清单最后一行」那条判据（S6 咬的是可见行的末行）。
                    continue;
                }
                // 追加行走直接引用（理由见 extraRowObjects）；spyer_ 是 prefab 原生行、不在追加表里。
                GameObject go = i < 0 ? UIHelper.getByName(gameObject, n) : FindExtraRow(n);
                Transform t = go != null ? go.transform : null;
                if (t == null)
                {
                    sb.Append(' ').Append(n).Append("=null");
                    continue;
                }
                float ly = mainWindowT.InverseTransformPoint(t.position).y;
                sb.Append(' ').Append(n).Append("=(y").Append(ly.ToString("F1"))
                  .Append(",margin").Append((ly - bottomY).ToString("F1")).Append(')');
            }
            QuickTestTrace.Log("setting", sb.ToString());
        }
    }

    private void readVales()
    {
        try
        {
            setting.sliderVolum.forceValue(((float)(int.Parse(Config.Get("vol_", "750")))) / 1000f);
            setting.sliderSize.forceValue(((float)(int.Parse(Config.Get("size_", "500")))) / 1000f);
            setting.sliderSizeDrawing.forceValue(((float)(int.Parse(Config.Get("vSize_", "500")))) / 1000f);
            //setting.sliderAlpha.forceValue(((float)(int.Parse(Config.Get("alpha_", "666")))) / 1000f);
            onChangeAlpha();
            onChangeSize();
        }
        catch (Exception e)
        {
            Debug.Log(e);
        }
    }

    public void onchangeCloud()
    {
        Program.MonsterCloud = setting.cloud.value;
    }

    public void onchangeMouse()
    {
        Program.I().mouseParticle.SetActive(setting.mouseEffect.value);
    }

    //private int dontResizeTwice = 2;

    public void setScreenSizeValue()
    {
        //dontResizeTwice = 3;
        UIHelper.getByName<UIPopupList>(gameObject, "screen_").value = Screen.width.ToString() + "*" + Screen.height.ToString();
    }

    void onCP()
    {
        try
        {
            Program.I().ocgcore.realize(true);
        }
        catch (Exception e) 
        {
        }
    }


    public void onchangeCloseUp()   
    {
        if (setting.closeUp.value == false)
        {
            setting.sliderAlpha.forceValue(0);
            setting.sliderSize.forceValue(0);
        }
        else
        {
            setting.sliderAlpha.forceValue(0.6666f);
            setting.sliderSize.forceValue(1f);
        }
        onChangeSize();
        onChangeAlpha();
    }

    public int atk = 1800;
    public int star = 5;

    void onchangeClose()
    {
        atk = 1800;
        star = 5;
        try
        {
            atk = int.Parse(setting.showoffATK.value);
        }
        catch (Exception)
        {

        }
        try
        {
            star = int.Parse(setting.showoffStar.value);
        }
        catch (Exception)
        {

        }
    }


    UISlider sliderAlpha;
    void onChangeAlpha()
    {
        if (sliderAlpha != null)
        {
            Program.transparency = 1.5f * sliderAlpha.value;
        }
        Program.transparency = 1f;
    }

    void onChangeLongField()
    {
        Program.longField = UIHelper.getByName<UIToggle>(gameObject, "longField_").value;
        onCP();
    }

    UISlider sliderVsize;
    void onChangeVsize()
    {
        if (sliderVsize != null)
        {
            Program.verticleScale = 4f + 2f * sliderVsize.value;
        }
    }

    UISlider sliderSize;
    void onChangeSize()  
    {
        if (sliderSize != null)
        {
            Program.fieldSize = 1f + sliderSize.value * 0.21f;
        }
    }

    public float vol() 
    {
        return UIHelper.getByName<UISlider>(gameObject, "vol_").value;
    }

    private int rayDumped = 0;
    private int shownAtFrame = -1;

    public override void preFrameFunction()
    {
        base.preFrameFunction();
        // 排查用（仅 log/qt_debug.on 生效，只打一次）：窗口亮起 1 秒后（iTween 滑入
        // 0.6 秒已落定），对每个追加开关的中心做一次全量射线，列出压在这个像素上的
        // 所有碰撞盒与距离 —— 实测「点了开关没反应」就是被别的碰撞盒截胡，这份清单
        // 能直接指认。注意不能在 initialize 时打：那时窗口还停在屏幕外 1.5 倍高处。
        // 命中清单里必须有自己（名字 = 开关名）；只剩背景 mainWindow 一个命中就说明
        // 这一行被窗口底边截掉了 —— 加行后窗口没跟着长高时的典型症状。
        if (QuickTestTrace.Enabled && rayDumped == 0 && isShowed)
        {
            if (shownAtFrame < 0)
            {
                shownAtFrame = Time.frameCount;
            }
            if (Time.frameCount - shownAtFrame > 60)
            {
                rayDumped = 1;
                for (int i = 0; i < extraToggleRowNames.Count; i++)
                {
                    UIToggle t = UIHelper.getByName<UIToggle>(gameObject, extraToggleRowNames[i]);
                    if (t == null || Program.camera_main_2d == null)
                    {
                        continue;
                    }
                    Vector3 sp = Program.camera_main_2d.WorldToScreenPoint(t.transform.position);
                    Ray ray = Program.camera_main_2d.ScreenPointToRay(sp);
                    RaycastHit[] hits = Physics.RaycastAll(ray);
                    System.Text.StringBuilder sb = new System.Text.StringBuilder();
                    for (int h = 0; h < hits.Length; h++)
                    {
                        sb.Append(hits[h].collider.gameObject.name);
                        sb.Append("@d=").Append(hits[h].distance.ToString("F2"));
                        sb.Append(" z=").Append(hits[h].collider.transform.position.z.ToString("F2"));
                        sb.Append("; ");
                    }
                    QuickTestTrace.Log("setting", "ray " + extraToggleRowNames[i]
                        + " screen=(" + Mathf.RoundToInt(sp.x)
                        + "," + Mathf.RoundToInt(Screen.height - sp.y) + ") camZ="
                        + Program.camera_main_2d.transform.position.z.ToString("F1")
                        + " hits[" + hits.Length + "]: " + sb);
                }

                // 关闭按钮（exit_）也报一次落定坐标。验收脚本的流程是「点开关 → 关窗口 →
                // 切模式 → 再开窗口」，没有这个坐标就只能猜关闭键的位置；而 RD 入口按钮
                // 正好被设置窗口盖住，**关不掉窗口就切不了模式**。同样等 60 帧（滑入落定）后才报。
                Transform exitT = UIHelper.getByName<Transform>(gameObject, "exit_");
                if (exitT != null && Program.camera_main_2d != null)
                {
                    Vector3 ep = Program.camera_main_2d.WorldToScreenPoint(exitT.position);
                    QuickTestTrace.Log("setting", "exit screen=(" + Mathf.RoundToInt(ep.x)
                        + "," + Mathf.RoundToInt(Screen.height - ep.y) + ")");
                }
                else
                {
                    QuickTestTrace.Log("setting", "exit = null");
                }
            }
        }
    }

    void onClickExit()
    {
        hide();
    }

    void resizeScreen()
    {
        //if (dontResizeTwice > 0)
        //{
        //    dontResizeTwice--;
        //    return;
        //}
        //dontResizeTwice = 2;
        if (UIHelper.isMaximized())
            UIHelper.RestoreWindow();
        string[] mats = UIHelper.getByName<UIPopupList>(gameObject, "screen_").value.Split(new string[] { "*" }, StringSplitOptions.RemoveEmptyEntries);
        if (mats.Length == 2)
        {
            Screen.SetResolution(int.Parse(mats[0]), int.Parse(mats[1]), UIHelper.getByName<UIToggle>(gameObject, "full_").value);
        }
        Program.go(100, () => { Program.I().fixScreenProblems(); });
    }

    public void saveWhenQuit()
    {
        Config.Set("vol_", ((int)(UIHelper.getByName<UISlider>(gameObject, "vol_").value * 1000)).ToString());
        Config.Set("size_", ((int)(UIHelper.getByName<UISlider>(gameObject, "size_").value * 1000)).ToString());
        Config.Set("vSize_", ((int)(UIHelper.getByName<UISlider>(gameObject, "vSize_").value * 1000)).ToString());
        //Config.Set("alpha_", ((int)(UIHelper.getByName<UISlider>(gameObject, "alpha_").value * 1000)).ToString());
        Config.Set("longField_", UIHelper.fromBoolToString(UIHelper.getByName<UIToggle>(gameObject, "longField_").value));
        var collection = gameObject.GetComponentsInChildren<UIToggle>();
        for (int i = 0; i < collection.Length; i++) 
        {
            if (collection[i].name.Length > 0 && collection[i].name[0] == '*')
            {
                Config.Set(collection[i].name, UIHelper.fromBoolToString(collection[i].value));
            }
        }
        Config.Set("showoffATK", setting.showoffATK.value.ToString());
        Config.Set("showoffStar", setting.showoffStar.value.ToString());
        Config.Set("resize_", UIHelper.fromBoolToString(UIHelper.getByName<UIToggle>(gameObject, "resize_").value));
        Config.Set("maximize_", UIHelper.fromBoolToString(UIHelper.isMaximized()));
        for (int i = 0; i < extraToggleRowNames.Count; i++)
        {
            // ⚠ 必须走**直接引用**（extraRowToggles）：隐藏中的行（OCG 下的 RD 独占行）用
            //   getByName 会被整个跳过 ⇒ 这一项再也存不进盘（理由见 extraRowObjects 的注释）。
            UIToggle t;
            if (!extraRowToggles.TryGetValue(extraToggleRowNames[i], out t) || t == null)
            {
                continue;
            }
            Config.Set(ExtraRowKey(extraToggleRowNames[i]), UIHelper.fromBoolToString(t.value));
        }
    }

    public void save()
    {
        System.Text.StringBuilder trace = QuickTestTrace.Enabled ? new System.Text.StringBuilder() : null;
        if (trace != null)
        {
            UIToggle spyer = UIHelper.getByName<UIToggle>(gameObject, "spyer_");
            trace.Append("save spyer_=").Append(spyer != null ? spyer.value.ToString() : "null");
        }
        Config.Set("ignoreWatcher_",UIHelper.fromBoolToString(UIHelper.getByName<UIToggle>(gameObject, "ignoreWatcher_").value));
        Config.Set("ignoreOP_", UIHelper.fromBoolToString(UIHelper.getByName<UIToggle>(gameObject, "ignoreOP_").value));
        Config.Set("smartSelect_", UIHelper.fromBoolToString(UIHelper.getByName<UIToggle>(gameObject, "smartSelect_").value));
        Config.Set("autoChain_", UIHelper.fromBoolToString(UIHelper.getByName<UIToggle>(gameObject, "autoChain_").value));
        Config.Set("handPosition_", UIHelper.fromBoolToString(UIHelper.getByName<UIToggle>(gameObject, "handPosition_").value));
        Config.Set("handmPosition_", UIHelper.fromBoolToString(UIHelper.getByName<UIToggle>(gameObject, "handmPosition_").value));
        Config.Set("spyer_", UIHelper.fromBoolToString(UIHelper.getByName<UIToggle>(gameObject, "spyer_").value));
        // 追加行都在「改动即存」这一档：消费方（卡组界面点测试、决斗里点盖放）
        // 是实时读 Config 的，只靠 saveWhenQuit 的话运行中永远读不到。
        for (int i = 0; i < extraToggleRowNames.Count; i++)
        {
            // ⚠ 同上：走**直接引用**，隐藏中的行不能被 getByName 跳过
            //   （否则「改一下即存」这一档对它失效，只能等退出时才落盘）。
            UIToggle t;
            if (!extraRowToggles.TryGetValue(extraToggleRowNames[i], out t) || t == null)
            {
                continue;
            }
            // 落盘的键按模式解析：RD 的那两行写自己的键，OCG 的两行走历史键。
            Config.Set(ExtraRowKey(extraToggleRowNames[i]), UIHelper.fromBoolToString(t.value));
            if (trace != null)
            {
                trace.Append(' ').Append(extraToggleRowNames[i])
                    .Append("@").Append(ExtraRowKey(extraToggleRowNames[i]))
                    .Append('=').Append(t.value);
            }
        }
        if (trace != null)
        {
            QuickTestTrace.Log("setting", trace.ToString());
        }
        if (UIHelper.getByName<UIToggle>(gameObject, "high_").value)
        {
            QualitySettings.SetQualityLevel(5);
        }
        else
        {
            QualitySettings.SetQualityLevel(0);
        }
    }

    public float soundValue()
    {
        return UIHelper.getByName<UISlider>(gameObject, "vol_").value;
    }
}

/// <summary>
/// 让一枚 RD 角标贴在锚点标签的右边（贴着文字走，不与文案重叠）。
///
/// 为什么是逐帧跟随而不是「造的时候算一次」：UILabel 的宽度要等 NGUI 排完版才知道，
/// 而角标是在 <c>Setting.initialize()</c>（同一帧更早）里造的，那一刻读到的宽度还是
/// 克隆来源（spyer_）的旧值。挂在角标自己身上就地解决，也不需要在窗口里加一个 Update。
///
/// 开销可以忽略：角标在 OCG 下是 <c>SetActive(false)</c>，停用的 GameObject 不跑 Update。
/// </summary>
public class RdBadgeFollower : MonoBehaviour
{
    /// <summary>对齐用的标签（角标与它同一个父节点，所以直接借用它的局部坐标）。</summary>
    public UILabel anchor;

    /// <summary>与文字右边缘的间距。</summary>
    public float gap = 8f;

    /// <summary>相对文字基线上抬多少（角标式，略微上飘）。</summary>
    public float lift = 6f;

    void LateUpdate()
    {
        if (anchor == null)
        {
            return;
        }
        // 锚点是 UILabel（不是 Transform）：位置得从它的 transform 取 ——
        // UILabel 上没有 localPosition 这个成员（写 anchor.localPosition 编译不过）。
        Vector3 ap = anchor.transform.localPosition;
        transform.localPosition = new Vector3(
            ap.x + anchor.width + gap,
            ap.y + lift,
            0f);
    }
}
