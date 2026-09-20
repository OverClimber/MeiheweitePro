using System;
using UnityEngine;

/// <summary>
/// 人机对局的「真撤回」。
///
/// 与工具条上原有的 left_ / right_（ReadingSteiner 世界线回看）的区别
/// ──────────────────────────────────────────────────────────────
/// left_ 只是在**客户端**把已经收到的消息往回放一遍，服务器那头还是推进过的状态 ——
/// 所以它能「看」，不能「改」，回过去之后没法继续操作。
///
/// 本类的撤回是**真的把这一手抹掉**：用同一颗种子重开一局，把已经发生过的输入原样喂回去，
/// 停在你要退回去的那个决策点之前，然后把控制权交还给你。
///
/// 观感（对齐 duel-undo-mod）
/// ──────────────────────────
/// ocgcore 跑在独立的 AI.Server.exe 里，没有任何 rewind 接口，所以撤回必然要
/// 「同种子重开 + 把历史输入喂回去」这段重建。duel-undo-mod 的做法是
/// **候选重建全程不触发 UI 动画/音效**，只在最后把界面切到重建好的状态 ——
/// 玩家看到的是「盘面一下子退回去了」，而不是把整局重演一遍。
/// 本类照这个口径做：
///   1. 按下撤回的那一帧就**本地重建**（<see cref="RewindLocally"/>）——
///      用录下来的入站流在客户端把模型重放到撤回点，立刻把画面退回上一步；
///   2. 服务器在后台重开并追赶（<see cref="silent"/> 期间不演出、不推进节拍）；
///   3. 追赶期间收到的入站包**只校验、不上屏**（<see cref="SwallowInbound"/>）——
///      因为这段盘面第 1 步已经画好了，重放不会第二次出现在屏幕上；
///   4. 整个过程盖一层遮罩（<see cref="ShowMask"/>：压暗 + 转圈 + 一行字），
///      玩家看到的是「一点，盘面就退回去了」，而不是把这段对局又演一遍。
///      —— 用户口径：不需要演示「从开局到撤回点」的过程，直接退过去；
///      但重建总归要几百毫秒，所以给个加载圈样的遮罩，别让人对着不动的画面发愣。
///
/// 为什么不直接重放「我方应答」
/// ────────────────────────────
/// 我方应答是**位置型**数据（「选第 3、5 张」）。一旦盘面在撤回点之前就分叉了，
/// 把旧字节原样发出去等于替你打出你没做过的操作，会直接打乱对局。
/// 所以重放由**录下来的入站消息流**驱动：每收一条就和录制逐条比对链式摘要，
/// 一致才继续代打；不一致就地停手（<see cref="OnFork"/>），按活流重建后把控制权交还给你。
///   ・没分叉 → 盘面与按下撤回前逐条一致；
///   ・分叉了 → 你拿到的是「同一副起手、同一次洗牌」的另一条线，停在分叉点继续玩。
/// 用户已确认接受后者（「撤回了 AI 走别的线都行」）。
///
/// 与 duel-undo-mod 的对应关系
/// ──────────────────────────
/// 同口径：只认**人工**决策点（<see cref="DuelTimeline.Decision.manual"/>），自动代答不算；
/// 重建期间不发网络包、不出动画、不配音效（<see cref="silent"/>）；重建逐条校验边界。
/// 不同处：它是 C++ 客户端里的双内核（旧会话保留到候选校验通过），
/// 我们是外挂服务器 + 独立机器人进程，做不到双开内核，于是改成「重启 + 入站流校验」。
/// </summary>
public static class DuelUndo
{
    /// <summary>一次撤回会话是否在进行中（重开已发起、还没停手）。</summary>
    public static bool active = false;

    /// <summary>
    /// 静默模式：服务器追赶期间**不演出、不推进节拍**。
    ///
    /// 这是「撤回不再从头布局」的关键 —— 追赶会把整局的消息再走一遍，
    /// 若照常演出，玩家看到的就是整局重演。静默期间只走逻辑层，
    /// 画面由 <see cref="RewindLocally"/> 在按下撤回那一刻一次性画好。
    /// </summary>
    public static bool silent = false;

    /// <summary>正在吞入站包：这段流按下撤回时已经在本地回溯过了，只校验+录制，不再上屏。</summary>
    public static bool catchingUp = false;

    /// <summary>正在本地重建模型。此间**绝不能**发应答、也绝不能录制（会污染新一局的时间线）。</summary>
    public static bool rebuilding = false;

    /// <summary>当前这次 sendReturn 是撤回器代发的（放行输入闸门，见 Ocgcore.sendReturn）。</summary>
    public static bool replayingResponse = false;

    /// <summary>按下撤回那一刻摘出来的那一局（重开会清空 DuelTimeline，所以必须留一份）。</summary>
    public static DuelTimeline.Snapshot saved = null;

    /// <summary>保留 decisions[0 .. targetKeep-1]，即退回到第 targetKeep 个决策点之前。</summary>
    public static int targetKeep = 0;

    /// <summary>本地回溯到录制入站流的下标（含）；-1 = 不回溯（撤回点在最前面）。</summary>
    public static int rebuildUpTo = -1;

    /// <summary>追赶期间要吞掉的入站包条数（= 触发目标请求那条消息之前的所有消息）。</summary>
    public static int swallowCount = 0;

    /// <summary>追赶中已经收到（并吞掉）的入站包条数。</summary>
    public static int catchIndex = 0;

    /// <summary>重放中已经代打的应答条数。</summary>
    public static int replayedDecisions = 0;

    /// <summary>这一轮重放是否撞上了分叉（对手走了别的线）。</summary>
    public static bool forked = false;

    /// <summary>
    /// 握手期分叉的自动重开次数（本次撤回会话内）。
    /// 「锁定手牌」关着且机器人赢猜拳时，它的先后攻选择是随机的 —— 重开一次五成对上，
    /// 见 <see cref="OnFork"/>。新会话（BeginSession）清零。
    /// </summary>
    private static int forkRetries = 0;

    /// <summary>重开是否已经发起（重开期间 ocgcore 会短暂隐藏，不跑 preFrameFunction）。</summary>
    public static bool restarted = false;

    /// <summary>
    /// 「这次 socket 断开是我们自己发起的」—— 撤回重开必须关掉旧连接（见 <see cref="Restart"/>），
    /// 而那个断开会被 TcpHelper 的断线分支判成「AI 局的对手进程没了」。
    ///
    /// ⛔ 为什么非有这个标记不可（2026-09-20 实测）：
    ///   关旧连接只会让 receiver 置上 onDisConnected，而那个标记要到**下一帧**
    ///   `TcpHelper.preFrameFunction` 才被消费 —— 那时 `Restart()` 已经同步跑完
    ///   `launch()`，`AIRoom.IsAiSessionLive` 被重新置成 true，而新一局还没开始
    ///   （`room.duelEnded` 仍是 false）。于是「AI 局 + 局没结束 + socket 断」三条全中：
    ///   TcpHelper 判定对手进程没了 -> `AIRoom.EndAiSessionAndReturn("socket 断开")`
    ///   -> 玩家被切回 `returnServant`（人机界面 / 卡组界面），而撤回遮罩还盖着、
    ///   输入闸门还关着 ⇒ 玩家看到的就是「一按撤回就退回战斗前的界面，然后卡住」。
    ///   现场轨迹：`restart launched` 之后 14ms 就出现「[wd] AI 局收尾（socket 断开）」。
    ///
    /// 生命周期：<see cref="Restart"/> 置位 → TcpHelper 消费一次即清 →
    /// <see cref="Finish"/> / <see cref="Fail"/> / <see cref="Reset"/> 兜底清零
    /// （万一那次 onDisConnected 根本没来，也不会漏到下一局去）。
    /// </summary>
    public static bool selfDisconnect = false;

    /// <summary>
    /// 撤回重开后、新一局第一条 <c>Start</c> 到达之前，旧连接的尾巴还会再吐出几条 GameMsg
    /// （socket 接收缓冲区里已经躺着的数据）。实测：按下撤回后约 0.4 秒会飘来一条
    /// <c>SelectChain</c>，落在新时间线的最前面。
    ///
    /// 不挡掉的后果不是「多一条记录」这么轻 —— 它会把新时间线的下标整体推后，
    /// 于是逐条摘要比对在第 0 条就判成不一致，表现是「一重开就分叉、盘面被清空」。
    /// 判据用 <c>Start</c>：一局的游戏消息流永远从 Start 开始，它是唯一可靠的锚点。
    /// </summary>
    private static bool awaitingRestartStart = false;

    /// <summary>
    /// 是否要丢掉这条「重开前残留」的入站消息（见 <see cref="awaitingRestartStart"/>）。
    /// 遇到 <c>Start</c> 就解除等待并放行 —— 那才是新一局的起点。
    /// </summary>
    public static bool DropStalePreStart(int fuction)
    {
        if (!awaitingRestartStart)
        {
            return false;
        }
        if (fuction == (int)YGOSharp.OCGWrapper.Enums.GameMessage.Start)
        {
            awaitingRestartStart = false;
            return false;
        }
        QuickTestTrace.Log("undo", "drop stale pre-Start msg="
            + (YGOSharp.OCGWrapper.Enums.GameMessage)fuction);
        return true;
    }

    /// <summary>
    /// 整局重放模式（只在排查入口下为 true）：目标不是「退到某一格」，而是把录下的整局走完，
    /// 一路逐条校验入站流。用于机械验收「同种子重开是否复现同一条消息流」。
    /// </summary>
    public static bool replayWholeDuel = false;

    /// <summary>
    /// 重放期间的演出节拍缩放 —— 只对**非静默**路径（整局重放排查入口）有效。
    /// 正常撤回走 <see cref="silent"/>，根本不演出，用不到这个。
    /// </summary>
    public static float paceScale = 0.18f;

    /// <summary>输入闸门：回溯/追赶期间玩家的操作一律丢掉（此时服务器正在重建）。</summary>
    public static bool BlockInput
    {
        get { return rebuildSession || (active && catchingUp); }
    }

    /// <summary>本地回溯是否正在进行（见 <see cref="BlockInput"/>）。</summary>
    private static bool rebuildSession = false;

    /// <summary>
    /// 这一局是不是「可以撤回的人机对局」。
    ///
    /// 判据用 <see cref="DuelTimeline.active"/> —— 它只在 `AIRoom.launch()` 里开、在
    /// `killServerProcess()` 里关，而**全部人机对局都从那一处开**：卡组界面的「测试」
    /// （`tryLaunchQuickTest`）与主菜单「人机对战」（`AIRoom.onStart`）走的是同一个 `launch()`，
    /// 区别只在开局前的握手（前者常量应答，后者玩家点选）与收尾去向。
    ///
    /// 以前这里写的是 `Room.quickThisDuel`（只有卡组测试局为真），于是主菜单人机对战
    /// **连撤回按钮都不会被创建** —— 玩家看到的就是「这条模式不能撤回」。
    ///
    /// 观战 / 录像 / 联机对局都不会经过 AIRoom.launch，所以天然被排除。
    /// </summary>
    public static bool IsUndoableDuel
    {
        get { return DuelTimeline.active && Program.I().room != null; }
    }

    /// <summary>
    /// 撤回重开后的「开局前握手」阶段：房间界面不再出场，准备/猜拳/先后攻一律照录下来的再答一次。
    ///
    /// 由 <see cref="Restart"/> 置位，`Room.StocMessage_DuelStart` 收位（那一帧握手已经结束）。
    /// 主菜单人机对战靠它才能无人值守地重开 —— 否则玩家会被丢回房间界面重新点一遍准备，
    /// 而猜拳/先后攻如果重答得与原来不一样，整条入站流当场分叉。
    /// </summary>
    public static bool replayingPremove = false;

    /// <summary>重开时已经重答过几次猜拳（下标，见 Room.StocMessage_SelectHand）。
    /// 先后攻不再走游标：它直接答出战局真值（见 ReplayGoFirst）。</summary>
    private static int premoveHandIndex = 0;

    /// <summary>重开时这一手猜拳该答什么。录不到就返回 0（调用方回退到测试局常量）。</summary>
    public static int NextHandAnswer()
    {
        int v = saved == null ? 0 : saved.HandAnswerAt(premoveHandIndex);
        premoveHandIndex++;
        QuickTestTrace.Log("undo", "premove hand#" + (premoveHandIndex - 1) + " -> " + v);
        return v;
    }

    /// <summary>
    /// 重开时这一次「先后攻」该答什么 —— 直接答出**原局的先手归属**（`Start` 的 `playertype`）。
    ///
    /// 🔑 为什么不再照录「我们当初点的那一手」：先后攻的选择权只在**猜拳赢的一方**手里。
    /// 机器人赢猜拳时我们根本收不到那个询问，`tpAnswers` 是空的、退回默认值就等于把先手
    /// 重掷一次骰子（同种子重开一次只有五成对上，正是旧注释里说的「两次重试 ≈ 87.5%」）。
    ///
    /// 现在重开的猜拳被钉成「我方第一手必胜」（见 Snapshot.BotHandOverride）⇒ 先手**必定**
    /// 由我方的这一次应答决定 ⇒ 照原局真值答回去即可，完全确定，一次成功、不需要重试。
    /// </summary>
    public static bool ReplayGoFirst()
    {
        bool v = saved == null || saved.startFirst;
        QuickTestTrace.Log("undo", "premove go-first -> " + v);
        return v;
    }

    /// <summary>这一局人机对局里还能不能撤回。</summary>
    public static bool CanUndo
    {
        get
        {
            return IsUndoableDuel && DuelTimeline.LastManualIndex() >= 0;
        }
    }

    /// <summary>
    /// 撤回入口（工具条的撤回按钮 / Ctrl+Z 都走这里）。
    /// </summary>
    public static void Request()
    {
        if (Program.I().ocgcore == null)
        {
            return;
        }
        if (!IsUndoableDuel)
        {
            Say("撤回只在人机对局里可用。");
            QuickTestTrace.Log("undo", "request rejected: not ai duel");
            return;
        }
        if (active)
        {
            // 正在回溯/追赶：这一段是重建过程，不接受新的撤回请求。
            // 连按撤回由「回溯完成后再按一次」实现 —— 那时最近的人工决策点已经变成
            // 上一次撤回的目标，再撤就是再往前一格。
            Say("正在回溯中…");
            QuickTestTrace.Log("undo", "request ignored: session in progress");
            return;
        }

        int keep = DuelTimeline.LastManualIndex();
        if (keep < 0)
        {
            Say("还没有可以撤回的选择。");
            QuickTestTrace.Log("undo", "request rejected: no manual decision, decisions="
                + DuelTimeline.decisions.Count);
            return;
        }
        BeginSession(keep, "undo");
    }

    /// <summary>
    /// 排查用：把整局录下来的输入从头重放一遍（不退任何东西）。
    ///
    /// 只验证一件事 —— 撤回的地基：「同一颗种子重开，入站消息流是否逐条一致」。
    /// 只有 log/qt_debug.on 与 log/qt_undoreplay.on 同时存在时才走得到。
    /// </summary>
    public static void RequestReplayAll()
    {
        if (Program.I().ocgcore == null || !IsUndoableDuel)
        {
            return;
        }
        if (active)
        {
            return;
        }
        if (DuelTimeline.decisions.Count == 0)
        {
            QuickTestTrace.Log("undo", "replayall: 没有录到任何应答，只重放开局");
        }
        BeginSession(DuelTimeline.decisions.Count, "replayall");
    }

    /// <summary>起一次撤回会话：摘快照 → 定目标 → 本地回溯 → 后台重开。</summary>
    private static void BeginSession(int keep, string why)
    {
        DuelTimeline.Snapshot snap = DuelTimeline.Take();
        if (snap.seed == 0)
        {
            Say("这一局没有记下种子，无法重建。");
            QuickTestTrace.Log("undo", "request rejected: seed=0");
            return;
        }
        saved = snap;
        targetKeep = keep;
        replayedDecisions = 0;
        forked = false;
        forkRetries = 0;
        restarted = false;
        catchIndex = 0;
        premoveHandIndex = 0;
        replayWholeDuel = (why == "replayall");

        // 目标点换算成「入站流下标」：
        //   decisions[k].inboundCount = N 表示这条应答是在处理完第 N 条消息后发出去的，
        //   也就是触发它的那条请求消息是下标 N-1。撤回要退到「那条请求还没被回答」的时候，
        //   所以要吞 0..N-2（共 N-1 条），并把本地模型回溯到 N-2；
        //   下标 N-1 那条（请求本身）留给正常路径处理 —— 玩家要看到那个选择界面。
        if (keep < snap.decisions.Count)
        {
            int n = snap.decisions[keep].inboundCount;
            swallowCount = Math.Max(0, n - 1);
            rebuildUpTo = n - 2;
        }
        else
        {
            // 整局重放：整条流都吞掉（画面已在按下时按当时录到的部分画好）。
            swallowCount = snap.inbound.Count;
            rebuildUpTo = snap.inbound.Count - 1;
        }
        if (rebuildUpTo >= snap.inbound.Count)
        {
            rebuildUpTo = snap.inbound.Count - 1;
        }

        active = true;
        silent = true;
        catchingUp = true;
        // 从这里开始，旧连接的残留 GameMsg 一律丢掉，直到新一局自己的 Start 到达。
        awaitingRestartStart = true;
        sessionStartMs = Program.TimePassed();
        // 遮罩先上：本地回溯是「一口气重放几百条消息」，会占掉一点时间，
        // 遮罩盖住这一段，玩家不会看到界面在中间抽一下。
        ShowMask();

        QuickTestTrace.Log("undo", "begin(" + why + ") keep=" + keep
            + " decisions=" + snap.decisions.Count
            + " inbound=" + snap.inbound.Count
            + " swallow=" + swallowCount
            + " rebuildUpTo=" + rebuildUpTo
            + " seed=" + snap.seed
            + " deck=" + snap.deckName
            + " whole=" + replayWholeDuel);

        Say("正在回溯世界线…");
        RewindLocally();
        Restart();
    }

    /// <summary>
    /// 按下撤回的**当帧**就把盘面退回撤回点。
    ///
    /// 走的是「同种子重开」那条路上客户端本来就要做的事（把入站流重放一遍），
    /// 只是这里提前做、只走逻辑层、一口气做完：清场 → logicalize 重放 → realize 归位。
    /// 全程不演出，所以玩家看到的是盘面「一下子退回去了」，而不是整局重演。
    ///
    /// 隐藏 + 立刻显示（同一个调用栈内，中间不会被渲染）：复用的是每次对局起止都在用的
    /// hide()/show() 清场路径，比自己手写一份「要清哪些字段」可靠。
    /// </summary>
    private static void RewindLocally()
    {
        int seq = ++rewindSeq;
        int ms = Program.TimePassed();
        rebuildSession = true;
        try
        {
            Program.I().ocgcore.hide();
            Program.I().shiftToServant(Program.I().ocgcore);
            Program.I().ocgcore.rebuildFromSnapshot(saved, rebuildUpTo);
        }
        catch (Exception e)
        {
            QuickTestTrace.Log("undo", "rewind FAILED: " + e.Message);
        }
        finally
        {
            rebuildSession = false;
        }
        QuickTestTrace.Log("undo", "rewind#" + seq + " upTo=" + rebuildUpTo
            + " took=" + (Program.TimePassed() - ms) + "ms");
    }

    /// <summary>
    /// 拆掉当前对局并用同一颗种子重开 —— 但**不离开决斗场景**。
    ///
    /// 关键点：不能走 onExit() 的完整路径。那条路径会 returnTo() → shiftToServant(deckManager)
    /// → Ocgcore.hide() 把整张场面清空、并且把玩家丢回卡组编辑器，画面于是「从头布局」。
    /// 这里只做真正的收尾动作：关 socket、杀 AI.Server/WindBot，决斗 servant 留在原地。
    /// </summary>
    private static void Restart()
    {
        DuelTimeline.Snapshot snap = saved;
        // 先置位再动：onExit() 与 launch() 内部都会经过 AIRoom，靠这个标记区分
        // 「这是撤回重开」和「这是一局全新的对局」——置晚了会被当成新局把重放状态清掉。
        restarted = true;
        AIRoom.forcedSeed = snap.seed;
        // 对手的猜拳钉成**我方第一手必胜的那一张**（`锁定手牌` 关着时它本来是自己随便出的）：
        // 于是第一轮必定分胜负、先手归我方 ⇒ 先手可以由我方的应答完全确定，见 ReplayGoFirst。
        // 见 AIRoom.forcedBotHand / DuelTimeline.Snapshot.BotHandOverride。
        // ⚠ launch() 会把它消费掉（用完清 0），所以这里先留一份给下面的日志。
        int botHand = snap.BotHandOverride(snap.lockHand);
        AIRoom.forcedBotHand = botHand;
        if (botHand == 0 && !snap.lockHand)
        {
            QuickTestTrace.Log("undo", "对手猜拳没有可钉的值（录到 "
                + snap.rpsBotAnswers.Count + " 轮 / 我方 "
                + snap.handAnswers.Count + " 手）—— 这次重开可能一开局就分叉");
        }

        // ① 关掉与旧服务器的连接（等价于 Ocgcore.onExit 的前半段，但不切 servant）
        //
        // ⛔ 置位必须在这一步**之前**：这一关会让 receiver 置 onDisConnected，而它要到下一帧
        //    才被消费，那时 IsAiSessionLive 已被 launch 重新置 true ⇒ 会被误判成对手断线、
        //    当场收尾回战斗前界面（详见 selfDisconnect 的注释）。
        selfDisconnect = true;
        try
        {
            if (TcpHelper.tcpClient != null)
            {
                if (TcpHelper.tcpClient.Connected)
                {
                    TcpHelper.tcpClient.Client.Shutdown(0);
                    TcpHelper.tcpClient.Close();
                }
                TcpHelper.tcpClient = null;
            }
        }
        catch (Exception e)
        {
            QuickTestTrace.Log("undo", "close socket: " + e.Message);
        }

        // ② 杀掉旧服务器与旧机器人（killServerProcess 内部会 DuelTimeline.End()）
        Program.I().aiRoom.killServerProcess();

        // 撤回重开也要按**原来那个入口**还原，否则两种局的差别会在重开那一刻暴露出来：
        //   ・卡组测试局 → 仍走测试直开局（Room.quickStart=true），结束后回卡组界面；
        //   ・主菜单人机对战 → 走「重开自动流程」（replayingPremove），结束后回人机界面。
        // 后者若照抄 quickStart=true，重开的这一局会被当成测试局：猜拳被锁成常量、
        // 打完成绩直接把人丢回卡组编辑器 —— 玩家是从主菜单进来的，那不是他该回去的地方。
        //
        // ⚠ quickStart 的置位必须在 launch() **之后**（见下面 ③ 之后那段）：
        //   launch() 开头会把它清回 false（普通开局要走完整房间流程）。曾经把它放在
        //   launch() 之前 —— 结果测试局撤回重开被当成普通开局：房间（准备/开始）界面
        //   出场，而撤回遮罩还盖着、输入闸门还关着，玩家看到的就是
        //   「一撤回就卡在进战斗前的界面」。主菜单那条线的 replayingPremove 挂在
        //   DuelUndo 上、launch() 不碰它，所以那条线没暴露这个问题。
        if (!snap.quickTest)
        {
            replayingPremove = true;
            premoveHandIndex = 0;
        }

        // ③ 重开。失败要有明确出路，绝不能让玩家停在「服务器没起来、画面还在等」的状态。
        bool ok = Program.I().aiRoom.launch(snap.botCommand, snap.lockHand, snap.noCheck, snap.noShuffle, snap.quickTest);
        if (!ok)
        {
            replayingPremove = false;
            Fail("回溯失败：对手或服务器没能起来，已放弃本次撤回。");
            return;
        }
        // launch() 会把 returnServant 指向 aiRoom（人机界面），主菜单人机对战重开后正好回到那里；
        // 卡组测试局要的是「进房即开局 + 打完回卡组界面」，所以在它之后再置位。
        // 与 tryLaunchQuickTest 同一套路：连接是 launch 里延时 500ms 另起线程发起的，
        // 这里置位不会被抢先消费（ini() 要等 JoinGame 回包才读 quickStart）。
        if (snap.quickTest)
        {
            Room.quickStart = true;
            Program.I().ocgcore.returnServant = Program.I().deckManager;
        }
        QuickTestTrace.Log("undo", "restart launched seed=" + snap.seed
            + " quickTest=" + snap.quickTest
            + " returnServant=" + ServantName(Program.I().ocgcore.returnServant)
            + " botHand=" + (snap.lockHand ? "locked(1)" : botHand.ToString())
            + " deck=" + (snap.deck == null ? "(未录到)" : snap.deck.Brief())
            + " hand=" + snap.handAnswers.Count + " tp=" + snap.tpAnswers.Count
            + " bot=" + snap.botCommand
            + " lockHand=" + snap.lockHand + " noCheck=" + snap.noCheck
            + " noShuffle=" + snap.noShuffle);
    }

    /// <summary>
    /// 排查用：给 servant 起个能读的名字（落进轨迹，验收脚本据此判「打完回哪个界面」）。
    ///
    /// 为什么不直接拿 gameObject.name：这几个 servant 的窗口都是运行时克隆出来的，
    /// 名字是 prefab 里的东西（实测都是 "GameObject"），分不出谁是谁。
    /// </summary>
    public static string ServantName(Servant s)
    {
        if (s == null)
        {
            return "null";
        }
        if (s == Program.I().aiRoom) return "aiRoom";
        if (s == Program.I().deckManager) return "deckManager";
        if (s == Program.I().room) return "room";
        if (s == Program.I().menu) return "menu";
        if (s == Program.I().ocgcore) return "ocgcore";
        if (s == Program.I().selectDeck) return "selectDeck";
        return "other";
    }

    /// <summary>
    /// 追赶期间拦下入站包。
    ///
    /// 返回 true = 这个包不要交给正常路径（画面已经在按下撤回那一刻画好了）。
    /// 但仍然要：① 录进新一局的时间线（否则新一局没法再撤回）；② 与录制流逐条比对摘要。
    /// </summary>
    public static bool SwallowInbound(Package p)
    {
        if (!active || !catchingUp)
        {
            return false;
        }
        if (catchIndex >= swallowCount)
        {
            EndCatchUp();
            return false;
        }
        // 兜底：目标那条请求**必须**走正常路径 —— 玩家要看到那个选择界面。
        // 正常情况下它正好落在边界上（下标 swallowCount）；这里再按消息类型护一格，
        // 万一离线记的边界偏了一位，也不会把「轮到你选」这条吞掉而让人卡在选择框前面。
        if (targetKeep < saved.decisions.Count
            && p.Fuction == saved.decisions[targetKeep].message
            && catchIndex + 1 >= swallowCount)
        {
            EndCatchUp();
            return false;
        }
        int idx = catchIndex;
        catchIndex++;

        // 录进新一局的时间线。位置必须在这里（包刚到达、Data 还没被读走），
        // 且必须与「吞掉」成对 —— 正常路径下这一步在 sibyl() 里做。
        DuelTimeline.NoteGameMessage(p.Fuction, p.Data.get());

        if (idx >= saved.inbound.Count)
        {
            // 录制流已经用完（目标比录制还靠后），之后属于新分支，不算分叉。
            EndCatchUp();
            return true;
        }
        if (DuelTimeline.inbound[idx].digest != saved.inbound[idx].digest)
        {
            OnFork(idx, p);
            // 分叉那条按正常路径走：模型已经按「到 idx-1 为止」重建过，正合适。
            return false;
        }
        return true;
    }

    /// <summary>追赶到点：退出静默，恢复演出。</summary>
    private static void EndCatchUp()
    {
        if (!catchingUp)
        {
            return;
        }
        catchingUp = false;
        silent = false;
        QuickTestTrace.Log("undo", "catchup done at inbound=" + catchIndex
            + "/" + swallowCount);
    }

    /// <summary>
    /// 分叉：对手走了另一条线。
    ///
    /// 本地模型里现在装的是**旧线**的盘面，而服务器已经在新线上，两者对不上了 ——
    /// 于是就地按「已经确认一致的那一段」重建一次模型，再把控制权交还玩家。
    /// 关键是**不能挂着等**（那是「卡住」的来源）：立刻收手，让玩家接着玩。
    /// </summary>
    private static void OnFork(int idx, Package p)
    {
        // 握手期分叉（Start 的先后攻字节对不上）：真因只可能是「先后攻没对上」——
        // 猜拳已经被钉成我方必胜、先手也照原局真值答了（见 Snapshot.BotHandOverride /
        // ReplayGoFirst），所以正常情况下这里不该再进来。真进来了就再重开一次试试：
        // 此时对局还没真正开始，本地重建毫无意义（客户端会停在空盘面），
        // 重开比「停在分叉点」强。两次重试仍不中才落到下面的降级。
        if (idx <= 2 && forkRetries < 2 && saved != null)
        {
            forkRetries++;
            QuickTestTrace.Log("undo", "fork at handshake idx=" + idx
                + " → 自动重开重试（第 " + forkRetries + " 次，最多 2 次）");
            forked = false;
            catchingUp = true;
            silent = true;
            active = true;
            awaitingRestartStart = true;
            catchIndex = 0;
            ShowMask();
            RewindLocally();
            Restart();
            return;
        }
        forked = true;
        catchingUp = false;
        silent = false;
        active = false;
        awaitingRestartStart = false;
        HideMask();
        QuickTestTrace.Log("undo", "FORK at inbound #" + idx
            + " live=" + DuelTimeline.inbound[idx].digest
            + " recorded=" + saved.inbound[idx].digest
            + " msg=" + (YGOSharp.OCGWrapper.Enums.GameMessage)p.Fuction
            + " replayed=" + replayedDecisions);
        try
        {
            rebuildSession = true;
            Program.I().ocgcore.hide();
            Program.I().shiftToServant(Program.I().ocgcore);
            // 到 idx-1 为止两条线逐条一致，用录制流重建这一段是安全的。
            Program.I().ocgcore.rebuildFromSnapshot(saved, idx - 1);
        }
        catch (Exception e)
        {
            QuickTestTrace.Log("undo", "fork rebuild FAILED: " + e.Message);
        }
        finally
        {
            rebuildSession = false;
        }
        Say("对手走了另一条线，已停在分叉点，可以继续操作。");
    }

    /// <summary>
    /// 每帧问一次「这一格该代打了吗」（Ocgcore.preFrameFunction 末尾调用）。
    /// 触发条件用**入站消息条数**对齐：录这条应答时已经处理过几条消息，
    /// 现在就等到同样条数再发 —— 这样发出的永远是「那一刻」的那个应答。
    /// </summary>
    /// <returns>需要代发的那条决策（含应答字节与原局的 manual 归属）；null = 本帧不发。</returns>
    public static DuelTimeline.Decision Tick()
    {
        if (!active)
        {
            return null;
        }
        // 兜底：重建总时长超过上限就放弃这次撤回，把控制权交还玩家并撤掉遮罩。
        // 「卡住」是用户对旧撤回最不满的一点，任何等不到服务器/机器人的情况
        // 都不能让玩家对着遮罩干等 —— 宁可这次不撤，也不许卡。
        if (sessionStartMs > 0 && Program.TimePassed() - sessionStartMs > SessionTimeoutMs)
        {
            Fail("回溯超时（服务器没有跟上），已取消本次撤回。");
            return null;
        }
        // 新局还没开始收消息，先别急着收手（否则会在进决斗场之前就报「已回到」）。
        if (DuelTimeline.inbound.Count == 0)
        {
            return null;
        }

        if (replayedDecisions < saved.decisions.Count)
        {
            // 普通撤回：代打到目标那一格之前就收手 —— 目标那一次是玩家要重新做的决定，
            // 代它答了就等于没撤回。
            //
            // 但「收手」必须等到**服务器追平**（catchingUp 结束）之后：代打完最后一个前置应答时，
            // 服务器还没把「轮到你了」那条请求发过来，此时收手会让中间那一段（对手的回合）
            // 退回正常演出 —— 又变成可见的重放。追平的终点恰好就是那条请求。
            if (!replayWholeDuel && replayedDecisions >= targetKeep)
            {
                if (!catchingUp)
                {
                    Finish("已回到上一步。");
                }
                return null;
            }
            DuelTimeline.Decision d = saved.decisions[replayedDecisions];
            if (DuelTimeline.inbound.Count < d.inboundCount)
            {
                return null;
            }
            int index = replayedDecisions;
            replayedDecisions++;
            QuickTestTrace.Log("undo", "replay resp #" + index
                + " msg=" + (YGOSharp.OCGWrapper.Enums.GameMessage)d.message
                + " manual=" + d.manual
                + " at inbound=" + DuelTimeline.inbound.Count + "/" + d.inboundCount
                + " left=" + (targetKeep - replayedDecisions));
            return d;
        }

        // 录下的应答已经全部代打完。
        if (replayWholeDuel)
        {
            // 整局重放：继续只做逐条校验，等录制流用完再收手。
            if (DuelTimeline.inbound.Count >= saved.inbound.Count)
            {
                Finish("整局重放完成。");
            }
            return null;
        }
        if (!catchingUp)
        {
            Finish("已回到上一步。");
        }
        return null;
    }

    /// <summary>重放到点：退出静默、收手，把控制权交还玩家。</summary>
    public static void Finish(string message)
    {
        if (!active)
        {
            return;
        }
        active = false;
        catchingUp = false;
        silent = false;
        awaitingRestartStart = false;
        selfDisconnect = false;      // 兜底：万一那次 onDisConnected 没来，别漏到下一局
        HideMask();
        QuickTestTrace.Log("undo", "finish forked=False replayed=" + replayedDecisions + "/" + targetKeep
            + " inbound=" + DuelTimeline.inbound.Count
            + " msg=" + message);
        // 追赶期间没有演出，画面是靠 RewindLocally 一次性画好的；这里再归位一次，
        // 把这段流里被吞掉的消息在画面上造成的任何残留（未归位的卡、残留的高亮）抹平。
        try
        {
            Program.I().ocgcore.realize();
            Program.I().ocgcore.toNearest();
        }
        catch (Exception e)
        {
            QuickTestTrace.Log("undo", "finish realize: " + e.Message);
        }
        Say(message);
    }

    /// <summary>
    /// 撤回过程中出硬伤（例如服务器/机器人没起来）：放弃本次撤回并把玩家放回可操作状态。
    /// 绝不允许「停在等不到服务器的地方」——那在玩家看来就是卡死。
    /// </summary>
    public static void Fail(string reason)
    {
        active = false;
        catchingUp = false;
        silent = false;
        forked = false;
        awaitingRestartStart = false;
        selfDisconnect = false;      // 兜底（见字段注释）
        AIRoom.forcedSeed = 0;
        // 与 forcedSeed 同族的会话残留：重开钉的对手猜拳。正常路径它由 launch() 消费，
        // 但 Fail 可能在 launch 早退 / 会话超时等任何一步触发 —— 这里兜底清零，
        // 别让它漏进下一局（漏了会把下一局的先手判定钉在旧值上）。
        AIRoom.forcedBotHand = 0;
        HideMask();
        QuickTestTrace.Log("undo", "FAIL: " + reason);
        try
        {
            Program.I().ocgcore.hide();
            Program.I().shiftToServant(Program.I().deckManager);
        }
        catch (Exception)
        {
        }
        Say(reason);
    }

    /// <summary>非人机对局里不该留下上一次的残留状态。</summary>
    public static void Reset()
    {
        active = false;
        silent = false;
        catchingUp = false;
        rebuilding = false;
        saved = null;
        targetKeep = 0;
        rebuildUpTo = -1;
        swallowCount = 0;
        catchIndex = 0;
        replayedDecisions = 0;
        forked = false;
        restarted = false;
        replayWholeDuel = false;
        awaitingRestartStart = false;
        replayingPremove = false;
        selfDisconnect = false;      // 兜底（见字段注释）
        premoveHandIndex = 0;
        AIRoom.forcedSeed = 0;
        AIRoom.forcedBotHand = 0;
        sessionStartMs = 0;
        HideMask();
    }

    private static int rewindSeq = 0;

    // ── 回溯遮罩 ────────────────────────────────────────────────────────────────
    //
    // 用户口径：撤回不需要演示「从开局到撤回点」的过程，直接退过去就行；
    // 但重建有几百毫秒，所以给一个加载圈样的遮罩。
    //
    // 实现照工程里既有的口径来（SuperPreList 也在运行时造 UI）：
    //   ・自建一个 UIPanel 当容器：NGUI 的面板是按 **layer** 找相机的
    //     （UIRect.ResetAnchors → NGUITools.FindCameraForLayer），而 Program.ui_main_2d
    //     在初始化时被显式置成 layer 11，正好是 camera_main_2d 的 cullingMask；
    //     所以只要挂在 11 层，自建面板就会被那个 UI 相机画出来，不必去改场景里的层级。
    //   ・压暗用 UITexture + Texture2D.whiteTexture 拉伸（1x1 白图乘上颜色即可），
    //     不必往工程里加任何美术资源。
    //   ・尺寸不写死：从 UI 相机把「屏幕四角」换算成世界坐标量出来，
    //     分辨率/窗口大小怎么变都对得上（工程里其它 UI 也是这个换算口径）。
    //   ・转圈是运行时画出来的一张环形贴图，靠 UndoSpinner 每帧自转。
    //   ・顺带挂个 BoxCollider 把点击挡住：这段时间盘面还没稳，不该接受操作。

    /// <summary>重建的时长上限（毫秒）。超过就放弃本次撤回，免得玩家对着遮罩干等。</summary>
    private const int SessionTimeoutMs = 25000;

    private static float sessionStartMs = 0f;
    private static GameObject maskRoot = null;

    /// <summary>遮罩深度：只要求盖住对战界面（工具条那一层的 panel）与 3D 盘面即可。</summary>
    private const int MaskPanelDepth = 800;

    private static readonly Color MaskColor = new Color(0.04f, 0.05f, 0.07f, 0.72f);

    private static Texture2D spinnerTex = null;

    /// <summary>UI 相机认的层（见上面的说明）。取不到就退回 11。</summary>
    private static int UiLayer()
    {
        try
        {
            if (Program.ui_main_2d != null)
            {
                return Program.ui_main_2d.layer;
            }
        }
        catch (Exception)
        {
        }
        return 11;
    }

    private static void ShowMask()
    {
        try
        {
            if (maskRoot != null)
            {
                return;
            }
            Camera cam = Program.camera_main_2d;
            if (cam == null || Program.ui_main_2d == null)
            {
                QuickTestTrace.Log("undo", "mask skipped: no ui camera");
                return;
            }
            // 这块 2D UI 相机是正交的（mod_2d_ui.prefab：orthographic 1、near -1000、far 1000），
            // 所以「屏幕四角 → 世界」与 z 无关，直接量出整个屏幕在世界单位下有多大。
            Vector3 bl = cam.ScreenToWorldPoint(new Vector3(0f, 0f, 0f));
            Vector3 tr = cam.ScreenToWorldPoint(new Vector3(Screen.width, Screen.height, 0f));
            Vector3 center = cam.ScreenToWorldPoint(new Vector3(Screen.width / 2f, Screen.height / 2f, 0f));
            float spanX = Mathf.Abs(tr.x - bl.x);
            float spanY = Mathf.Abs(tr.y - bl.y);
            if (spanX <= 0f || spanY <= 0f)
            {
                QuickTestTrace.Log("undo", "mask skipped: bad span " + spanX + "x" + spanY);
                return;
            }

            // 遮罩是挂在场景根上的独立对象，**不继承 UIRoot 的缩放**（UIRoot 那套是
            // 2/manualHeight ≈ 0.0019），所以面板单位此刻等于世界单位 ——
            // 而整个世界只有 2 个单位高（正交 size 1）。因此先把根缩到「1 单位 = 1 像素」，
            // 下面的尺寸/字号才能照工程里其它 UI 的老习惯按像素写。
            float unit = spanY / Mathf.Max(1, Screen.height);
            Vector3 c = center / unit;                 // 屏幕中心，换算到面板局部单位

            maskRoot = new GameObject("undo_mask");
            maskRoot.layer = UiLayer();      // 必须在 AddComponent 之前设好：面板在启用时就按层找相机
            maskRoot.transform.localScale = new Vector3(unit, unit, unit);
            UIPanel panel = maskRoot.AddComponent<UIPanel>();
            panel.depth = MaskPanelDepth;
            panel.clipping = UIDrawCall.Clipping.None;

            // ① 压暗全屏（顺带当点击挡板）
            int qw = Mathf.CeilToInt(Screen.width * 1.3f);     // 留余量，别在边缘露出缝
            int qh = Mathf.CeilToInt(Screen.height * 1.3f);
            GameObject quad = new GameObject("undo_mask_quad");
            quad.layer = maskRoot.layer;
            quad.transform.SetParent(maskRoot.transform, false);
            UITexture tex = quad.AddComponent<UITexture>();
            tex.mainTexture = Texture2D.whiteTexture;
            tex.color = MaskColor;
            tex.depth = 0;
            tex.SetDimensions(qw, qh);
            // 往相机方向挪一点：正交相机 near=-1000，挪过去不会被裁，
            // 但射线检测会先打到它 —— 重建这段时间的点击就被挡住。
            quad.transform.localPosition = new Vector3(c.x, c.y, c.z - 10f / unit);
            BoxCollider blocker = quad.AddComponent<BoxCollider>();
            // 厚度按世界单位给够（面板自己缩得很小，写 1 就只有 0.002 个世界单位，
            // 射线有可能打不中）：10 个世界单位厚，稳落在相机前方。
            blocker.size = new Vector3(qw, qh, 10f / unit);

            // ② 转圈（按像素写，112 ≈ 1080 高的十分之一）
            const int ring = 112;
            GameObject spin = new GameObject("undo_spinner");
            spin.layer = maskRoot.layer;
            spin.transform.SetParent(maskRoot.transform, false);
            UITexture circle = spin.AddComponent<UITexture>();
            circle.mainTexture = SpinnerTexture();
            circle.depth = 1;
            circle.SetDimensions(ring, ring);
            spin.transform.localPosition = c;
            spin.AddComponent<UndoSpinner>();

            // ③ 一行字。字体模板从现成的 UILabel 上抄（不新增资源，也不改任何 prefab）。
            UILabel tpl = FindLabelTemplate();
            GameObject line = new GameObject("undo_mask_text");
            line.layer = maskRoot.layer;
            line.transform.SetParent(maskRoot.transform, false);
            UILabel lab = line.AddComponent<UILabel>();
            if (tpl != null)
            {
                lab.bitmapFont = tpl.bitmapFont;
                lab.trueTypeFont = tpl.trueTypeFont;
                lab.fontStyle = tpl.fontStyle;
                lab.applyGradient = tpl.applyGradient;
                lab.gradientTop = tpl.gradientTop;
                lab.gradientBottom = tpl.gradientBottom;
            }
            lab.text = InterString.Get("正在回溯世界线…");       // 与全工程口径一致：UI 文案统一走翻译表
            lab.fontSize = 26;
            lab.alignment = NGUIText.Alignment.Center;
            lab.pivot = UIWidget.Pivot.Center;
            lab.color = new Color(1f, 1f, 1f, 0.92f);
            lab.depth = 2;
            lab.SetDimensions(600, 50);
            line.transform.localPosition = new Vector3(c.x, c.y - ring * 0.95f, c.z);

            QuickTestTrace.Log("undo", "mask shown " + qw + "x" + qh + "px ring=" + ring
                + "px unit=" + unit.ToString("F6") + " layer=" + maskRoot.layer
                + " tpl=" + (tpl != null));
        }
        catch (Exception e)
        {
            QuickTestTrace.Log("undo", "mask FAILED: " + e.Message);
        }
    }

    /// <summary>
    /// 找一个现成的 UILabel 当字体模板 —— NGUI 没有全局默认字体：
    /// UILabel 的 bitmapFont 与 trueTypeFont 都为空时是不出字的，所以必须抄一份真的字体引用。
    /// 工程里运行时新建标签都这么做（Menu.UpdateNewBadge、SuperPreList.CreateLabel 都是
    /// 从现成节点 GetComponentInChildren&lt;UILabel&gt; 抄字体），这里沿用同一口径：
    /// 不新增美术资源、不改任何 prefab。
    /// </summary>
    private static UILabel FindLabelTemplate()
    {
        try
        {
            Servant s = Program.I() != null ? Program.I().ocgcore : null;
            if (s != null)
            {
                if (s.gameObject != null)
                {
                    UILabel l = s.gameObject.GetComponentInChildren<UILabel>(true);
                    if (l != null) return l;
                }
                if (s.toolBar != null)
                {
                    UILabel l = s.toolBar.GetComponentInChildren<UILabel>(true);
                    if (l != null) return l;
                }
            }
            if (Program.ui_main_2d != null)
            {
                UILabel l = Program.ui_main_2d.GetComponentInChildren<UILabel>(true);
                if (l != null) return l;
            }
        }
        catch (Exception)
        {
        }

        // 兜底：扫一遍场景根（含未激活对象）。UI 面板未必挂在上面那几个根节点下面，
        // 决斗中开场板/按钮上的字都有可能藏在这里。
        try
        {
            GameObject[] roots = UnityEngine.SceneManagement.SceneManager
                .GetActiveScene().GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                if (roots[i] == null || roots[i] == maskRoot) continue;
                UILabel l = roots[i].GetComponentInChildren<UILabel>(true);
                if (l != null) return l;
            }
        }
        catch (Exception)
        {
        }
        return null;
    }

    private static void HideMask()
    {
        if (maskRoot == null)
        {
            return;
        }
        try
        {
            UnityEngine.Object.Destroy(maskRoot);
        }
        catch (Exception)
        {
        }
        maskRoot = null;
        QuickTestTrace.Log("undo", "mask hidden");
    }

    /// <summary>运行时画一张「加载圈」：圆环 + 沿圆周的头亮尾淡，转起来就像在加载。</summary>
    private static Texture2D SpinnerTexture()
    {
        if (spinnerTex != null)
        {
            return spinnerTex;
        }
        const int n = 128;
        spinnerTex = new Texture2D(n, n, TextureFormat.RGBA32, false);
        spinnerTex.wrapMode = TextureWrapMode.Clamp;
        float half = (n - 1) * 0.5f;
        float outer = half - 1f;
        float inner = outer * 0.74f;
        for (int y = 0; y < n; y++)
        {
            for (int x = 0; x < n; x++)
            {
                float dx = x - half;
                float dy = y - half;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                float a = 0f;
                if (d <= outer && d >= inner)
                {
                    // 绕一圈由淡到亮：起点最淡，接近起点处最亮，看起来像有头有尾
                    float t = (Mathf.Atan2(dy, dx) + Mathf.PI) / (2f * Mathf.PI);
                    a = Mathf.Clamp01(0.12f + 0.88f * t);
                    // 内外边缘各软一格，别出锯齿
                    a *= Mathf.Clamp01((outer - d) / 1.5f) * Mathf.Clamp01((d - inner) / 1.5f);
                }
                spinnerTex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        }
        spinnerTex.Apply();
        return spinnerTex;
    }

    private static void Say(string text)
    {
        try
        {
            if (Program.I().ocgcore != null)
            {
                Program.I().ocgcore.RMSshow_none(InterString.Get(text));
            }
        }
        catch (Exception)
        {
        }
    }
}

/// <summary>
/// 让撤回遮罩上的加载圈自转。
///
/// 单独一个组件（而不是塞进 DuelUndo 的每帧逻辑）是因为遮罩是**运行时现造**的 UI 对象，
/// 它的生命周期只到本次撤回结束；转圈也跟着它一起活，被 Destroy 就自然停了。
/// 用 Time.deltaTime 而不是 unscaleTime：这里要的就是「跟着游戏一起转」。
/// </summary>
public class UndoSpinner : MonoBehaviour
{
    void Update()
    {
        transform.Rotate(0f, 0f, -360f * Time.deltaTime);
    }
}
