using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using UnityEngine;

public class AIRoom : WindowServantSP
{
    #region ui
    UIselectableList superScrollView = null;
    string sort = "sortByTimeDeck";
    System.Diagnostics.Process serverProcess;
    System.Diagnostics.Process botProcess;

    public class BotInfo
    {
        public string name;
        public string command;
        public string desc;
        public string[] flags;
    }
    private IList<BotInfo> Bots = new List<BotInfo>();

    /// <summary>当前这份 Bots 是按哪个模式读出来的（null = 还没读）。见 <see cref="EnsureBots"/>。</summary>
    private string botsModeKey = null;

    private void ReadBots(string confPath)
    {
        // 读不出来只记一条日志、不抛：名单文件缺失是「这个模式还没铺数据」，
        // 该表现成「列表空、给玩家一句提示」，而不是在 initialize 里炸掉整个界面。
        Bots = new List<BotInfo>();
        if (!System.IO.File.Exists(confPath))
        {
            QuickTestTrace.Log("ai", "bot 名单不存在: " + confPath
                + "（mode=" + GameModeManager.ModeLabel + "）");
            return;
        }
        try
        {
            StreamReader reader = new StreamReader(new FileStream(confPath, FileMode.Open, FileAccess.Read));
            while (!reader.EndOfStream)
            {
                string line = reader.ReadLine().Trim();
                if (line.Length > 0 && line[0] == '!')
                {
                    BotInfo newBot = new BotInfo();
                    newBot.name = line.TrimStart('!');
                    newBot.command = reader.ReadLine().Trim();
                    newBot.desc = reader.ReadLine().Trim();
                    line = reader.ReadLine().Trim();
                    newBot.flags = line.Split(' ');
                    if (Array.IndexOf(newBot.flags, "SELECT_DECKFILE") < 0)
                        Bots.Add(newBot);
                }
            }
            reader.Close();
        }
        catch (System.Exception e)
        {
            QuickTestTrace.Log("ai", "读 bot 名单失败: " + confPath + " -> " + e.Message);
            Bots = new List<BotInfo>();
        }
    }

    /// <summary>
    /// 保证手里的机器人名单是**当前模式**那一份。
    ///
    /// 为什么不能只在 initialize 里读一次：名单文件是按模式分家的
    /// （OCG = config/bot.conf，RD = rd/bot.conf），而模式是**运行期随时可切**的瞬时时，
    /// 界面却在启动时就建好了。这里按模式缓存，切模式后第一次用到就自动换名单。
    /// </summary>
    private void EnsureBots()
    {
        string key = GameModeManager.IsRD ? "rd" : "ocg";
        if (botsModeKey == key && Bots.Count > 0)
        {
            return;
        }
        string path = GameModeManager.BotConf;
        ReadBots(path);
        botsModeKey = key;
        QuickTestTrace.Log("ai", "bot 名单 reload mode=" + GameModeManager.ModeLabel
            + " conf=" + path + " bots=" + Bots.Count);
    }

    /// <summary>
    /// 切换模式后的刷新：名单换了、列表也得换。
    /// 界面不在场时只记日志，等下次 show() 的 printFile 去取（EnsureBots 会认模式）。
    /// </summary>
    private void OnGameModeChanged(GameModeManager.Mode mode)
    {
        botsModeKey = null;          // 作废缓存，下次用到时重读
        EnsureBots();
        if (isShowed)
        {
            printFile();
            onSelected();
        }
        QuickTestTrace.Log("ai", "mode -> " + GameModeManager.ModeLabel
            + " bots=" + Bots.Count + " conf=" + GameModeManager.BotConf);
    }

    private string GetRandomBot(string flag)
    {
        IList<BotInfo> foundBots = new List<BotInfo>();
        foreach (var bot in Bots)
        {
            if (Array.IndexOf(bot.flags, flag) >= 0) foundBots.Add(bot);
        }
        if (foundBots.Count > 0)
        {
            System.Random rand = new System.Random();
            BotInfo bot = foundBots[rand.Next(foundBots.Count)];
            return bot.command;
        }
        return "";
    }

    public override void initialize()
    {
        createWindow(Program.I().new_ui_aiRoom);
        superScrollView = gameObject.GetComponentInChildren<UIselectableList>();
        superScrollView.selectedAction = onSelected;
        UIHelper.registEvent(gameObject, "start_", onStart);
        UIHelper.registEvent(gameObject, "exit_", onClickExit);
        UIHelper.trySetLableText(gameObject, "percyHint", InterString.Get("人机模式"));
        UIHelper.trySetLableText(gameObject, "botdesc_", InterString.Get("请选择对手。"));
        superScrollView.install();
        EnsureBots();
        GameModeManager.Changed += OnGameModeChanged;
        SetActiveFalse();
    }

    void onSelected()
    {
        int sel = superScrollView.selectedIndex;
        if (sel >= 0 && sel < Bots.Count)
            UIHelper.trySetLableText(gameObject, "botdesc_", Bots[sel].desc);
        else
            UIHelper.trySetLableText(gameObject, "botdesc_", InterString.Get("请选择对手。"));
    }

    void onSave()
    {
        //Config.Set("list_aideck", list_aideck.value);
        //Config.Set("list_airank", list_airank.value);
    }

    void onClickExit()
    {
        QuickTestTrace.Exit("aiRoom.onClickExit exitOnReturn=" + Program.exitOnReturn);
        killServerProcess();
        if (Program.exitOnReturn)
            Program.I().menu.onClickExit();
        else
            Program.I().shiftToServant(Program.I().menu);
    }

    public void killServerProcess()
    {
        // 机器人也要一起收。它平时是靠「服务器没了、socket 断开」自己退出的，
        // 但撤回重开是**紧接着**就起新服务器的，而 AI.Server 绑的是固定端口 ——
        // 旧机器人还没反应过来就可能会被新服务器收下，一局里冒出两个 WindBot。
        // 这里显式杀掉，把「谁连上了新服务器」这件事变成确定的。
        KillQuietly(botProcess);
        botProcess = null;
        if (serverProcess != null && !serverProcess.HasExited)
        {
            serverProcess.Kill();
            // Kill 是异步的：不等它真的退出，紧接着新起的那个进程可能还抢不到端口。
            try { serverProcess.WaitForExit(3000); } catch (System.Exception) { }
        }
        serverProcess = null;
        IsAiSessionLive = false;
        // 撤回用的时间线到此为止。launch() 一上来也会调这里，于是「开新局」= 先收上一条。
        DuelTimeline.End();
    }

    #region AI 对局看门狗（2026-09-19）

    /// <summary>
    /// 本进程是不是正在跑一局**本机 AI 对局**（AI.Server + WindBot 都由我们起）。
    ///
    /// 为什么需要它：断线收尾要分两种。联机局断线是「对方跑了，您截个图」；
    /// AI 局的对手是本机进程，它没了就是真没了 —— 应该收干净、回界面，而不是把玩家留在场上。
    /// </summary>
    public static bool IsAiSessionLive = false;

    /// <summary>
    /// 决斗中多久没有任何入站包就认为对手卡住了。取 2 分钟：
    /// WindBot 正常出手是毫秒级，服务器也不会沉默这么久；而玩家自己想事情时
    /// 服务器同样不发包，所以这条**只提示、不自动收局**，避免误伤正常思考。
    /// </summary>
    private const int StallSilenceMs = 120000;

    private int lastWatchCheckMs = 0;
    private int lastSeenInbound = -1;
    private int lastInboundSeenMs = 0;
    private bool stallNotified = false;

    /// <summary>本局两个子进程的输出留证器（launch 里赋值；看门狗判定卡死时取尾部快照）。</summary>
    private StdoutDrain serverOut;
    private StdoutDrain botOut;

    /// <summary>
    /// 卡死/子进程异常退出时，把两个子进程输出的**最后一段**快照进
    /// <c>log/ai_stall.log</c>（追加）。落在这里而不是只进 qt 日志：
    /// 报告到达时用户多半没开 qt_debug，现场又转瞬即逝 —— 这份文件必须无条件存在。
    /// 幂等留证：一局内同一原因只写一次（快照里有时间戳与原因）。
    /// </summary>
    private void DumpAiStallSnapshot(string why)
    {
        try
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            sb.AppendLine("==== " + System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
                + " " + why
                + " | server hasExited=" + SafeHasExited(serverProcess)
                + " bot hasExited=" + SafeHasExited(botProcess)
                + " mode=" + GameModeManager.ModeLabel);
            string s = serverOut != null ? serverOut.Tail(40) : "";
            if (s.Length == 0) s = "(无输出)";
            sb.AppendLine("-- AI.Server 最后 40 行 --").AppendLine(s);
            s = botOut != null ? botOut.Tail(40) : "";
            if (s.Length == 0) s = "(无输出)";
            sb.AppendLine("-- WindBot 最后 40 行 --").AppendLine(s);
            System.IO.File.AppendAllText(QuickTestTrace.LogPath("ai_stall.log"),
                sb.ToString(), System.Text.Encoding.UTF8);
        }
        catch (System.Exception)
        {
            // 留证失败不能影响收尾流程。
        }
    }

    private string DeadChild()
    {
        if (botProcess != null)
        {
            string s = SafeHasExited(botProcess);
            if (s != "no" && s != "?")
            {
                return "WindBot(" + s + ")";
            }
        }
        if (serverProcess != null)
        {
            string s = SafeHasExited(serverProcess);
            if (s != "no" && s != "?")
            {
                return "AI.Server(" + s + ")";
            }
        }
        return null;
    }

    /// <summary>
    /// AI 对局的看门狗，每帧由 <c>Program.Update</c> 调（内部每秒才真跑一次）。
    ///
    /// 解决的是用户报的「RD 人机经常卡住」：RD 侧的 WindBot 是与 OCG 侧不同的另一个版本
    /// （1.52MB vs 2.25MB），崩起来更常见。此前它一崩，客户端没有任何人发现 ——
    /// 服务器照着「对手还没答」等下去，玩家看到的就是**永远不动的对局**，
    /// 而且连退出都靠猜。这里把「子进程死没死」变成可观测、可自动收尾的事实。
    ///
    /// 分两档：
    ///   · 子进程已退出（确定）      -> 立刻收尾并回界面（见 EndAiSessionAndReturn）；
    ///   · 子进程都活着但长时间没包（可疑）-> 只记日志 + 给一句提示，不替玩家做决定。
    /// </summary>
    public void WatchdogTick()
    {
        if (!IsAiSessionLive || serverProcess == null)
        {
            lastSeenInbound = -1;
            lastInboundSeenMs = 0;
            stallNotified = false;
            return;
        }
        int now = Program.TimePassed();
        if (now - lastWatchCheckMs < 1000)
        {
            return;
        }
        lastWatchCheckMs = now;

        string dead = DeadChild();
        if (dead != null)
        {
            // 一局正常打完（duelEnded）之后，服务器与机器人本来就会自己退出 ——
            // 那不是卡死，别把玩家从结算窗口弹走。只有「局还没结束进程就没了」才收尾。
            if (Program.I().room != null && Program.I().room.duelEnded)
            {
                return;
            }
            QuickTestTrace.Log("wd", "看门狗：AI 子进程已退出 -> " + dead + "，本局收尾");
            DumpAiStallSnapshot("子进程退出 " + dead);
            EndAiSessionAndReturn("子进程退出 " + dead);
            return;
        }

        int inbound = TcpHelper.inboundCount;
        if (lastSeenInbound < 0)
        {
            lastSeenInbound = inbound;
            lastInboundSeenMs = now;
            return;
        }
        if (inbound != lastSeenInbound)
        {
            lastSeenInbound = inbound;
            lastInboundSeenMs = now;
            stallNotified = false;
            return;
        }
        // 只有「正在决斗场里、且这一局还没结束」才判静默：
        // 房间界面/加载阶段本来就可能很久没有包，结算窗口更是只等玩家点。
        if (Program.I().ocgcore == null || !Program.I().ocgcore.isShowed
            || (Program.I().room != null && Program.I().room.duelEnded))
        {
            lastInboundSeenMs = now;
            return;
        }
        int silence = now - lastInboundSeenMs;
        if (silence > StallSilenceMs && !stallNotified)
        {
            stallNotified = true;
            QuickTestTrace.Log("wd", "看门狗：决斗中已 " + (silence / 1000)
                + "s 没有入站包（包总数停在 " + inbound + "），对手疑似卡死");
            DumpAiStallSnapshot("决斗静默 " + (silence / 1000) + "s（包总数停在 " + inbound + "）");
            RMSshow_none(InterString.Get("电脑对手已 2 分钟没有响应，可认输或退出后重开。"));
        }
    }

    /// <summary>
    /// 结束当前 AI 对局并回到玩家来的那个界面。
    ///
    /// 回哪儿不是拍脑袋：开局时 <c>Ocgcore.returnServant</c> 已经记下了入口
    /// （卡组界面「测试」开局 = 回卡组编辑器；主菜单「人机对战」= 回人机界面），
    /// 拿它当目标两边都对。取不到才兜底回人机界面。
    /// </summary>
    public static void EndAiSessionAndReturn(string why)
    {
        // 撤回重建期间被收尾（真故障 / 看门狗 / 会话超时兜底）：先把撤回器收干净。
        // 遮罩与输入闸门都是它挂的，不撤掉的话玩家会被弹回界面却点不动 —— 那正是
        // 「撤回之后卡住」的观感。正常撤回走不到这里：关旧连接那一下已被
        // DuelUndo.selfDisconnect 放行，不会被判成「对手断线」。
        if (DuelUndo.active)
        {
            QuickTestTrace.Log("wd", "收尾时撤回会话仍在进行，先取消它（" + why + "）");
            DuelUndo.Fail("对局已中断，本次撤回取消。");
        }
        AIRoom room = Program.I() != null ? Program.I().aiRoom : null;
        Servant target = room;
        if (Program.I().ocgcore != null && Program.I().ocgcore.returnServant != null)
        {
            target = Program.I().ocgcore.returnServant;
        }
        QuickTestTrace.Log("wd", "AI 局收尾（" + why + "）：清进程 + 断开 + 回 "
            + (target != null ? target.GetType().Name : "null"));
        if (room != null)
        {
            room.killServerProcess();
        }
        // 标记成「用户主动断开」：否则 socket 关闭会让 receiver 再走一遍断线流程。
        TcpHelper.Disconnect(true);
        if (Program.I().ocgcore != null)
        {
            Program.I().ocgcore.forceMSquit();
        }
        if (target != null && Program.I().ocgcore != null && Program.I().ocgcore.isShowed)
        {
            Program.I().shiftToServant(target);
        }
    }

    #endregion

    /// <summary>
    /// 后台把子进程的 stdout/stderr 读干，顺手捉住第一行，并把全部内容**落盘留证**。
    ///
    /// 为什么必须读干：这两个子进程都是 `RedirectStandardOutput = true` 起的，
    /// 管道缓冲区（约 4KB）写满之后**写方会阻塞**。原先只读第一行、之后再也不读 ——
    /// WindBot 在中盘多打几行日志就会把自己卡住，表现是「对局突然不动了」，
    /// 而且重新开局甚至撤回重开都可能再来一次。这是个隐藏已久的卡死来源。
    ///
    /// 为什么 2026-09-19 起不再丢弃、要落盘（`log/ai_<角色>_<pid>.log`）：
    /// 「RD 人机时不时卡住」这类报告到达时**现场已经没了** —— 子进程的 stdout 里
    /// 常有 lua 报错/协议异常的第一手信息，丢掉就只剩瞎猜。行数很稀（一回合几行），
    /// 每行一 flush 的代价可以忽略；写失败静默吞掉（排查基建绝不能反过来弄崩对局）。
    /// stderr 同样接管并读干（前缀 `[err] `）—— 不接管的话它继承的是无效句柄，
    /// 报错直接消失；接管了却不读干反而会复刻 4KB 阻塞，所以两股必须一起泵。
    ///
    /// 另在内存里留**最后 N 行**（<see cref="Tail"/>）：看门狗判定卡死时把现场快照
    /// 写进 `log/ai_stall.log`，不依赖 qt_debug 开没开。
    /// </summary>
    private class StdoutDrain
    {
        public volatile string firstLine = null;
        public volatile bool gotFirst = false;
        private readonly object gate = new object();
        private System.IO.StreamWriter writer;
        private readonly System.Collections.Generic.List<string> recent
            = new System.Collections.Generic.List<string>();
        private const int RecentMax = 60;

        public void Start(System.Diagnostics.Process proc, string logPath)
        {
            try
            {
                string dir = System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(logPath));
                if (!System.IO.Directory.Exists(dir))
                {
                    System.IO.Directory.CreateDirectory(dir);
                }
                writer = new System.IO.StreamWriter(logPath, false, System.Text.Encoding.UTF8);
                writer.AutoFlush = true;
            }
            catch (System.Exception)
            {
                writer = null;   // 没有留证条件就退回「读干即弃」，绝不影响对局
            }
            Pump(proc.StandardOutput, "");
            // stderr 与 stdout 各占一根管道，都必须有人读干（见类头），这里各起一根线程。
            try
            {
                Pump(proc.StandardError, "[err] ");
            }
            catch (System.Exception)
            {
                // 老产物没重定向 stderr 时 StandardError 访问会抛 —— 那就只泵 stdout。
            }
        }

        private void Pump(System.IO.StreamReader reader, string tag)
        {
            System.Threading.Thread t = new System.Threading.Thread(() =>
            {
                try
                {
                    string line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        if (!gotFirst)
                        {
                            firstLine = line;
                            gotFirst = true;
                        }
                        lock (gate)
                        {
                            recent.Add(tag + line);
                            if (recent.Count > RecentMax)
                            {
                                recent.RemoveRange(0, recent.Count - RecentMax);
                            }
                            if (writer != null)
                            {
                                writer.WriteLine(tag + line);
                            }
                        }
                    }
                }
                catch (System.Exception)
                {
                }
            });
            t.IsBackground = true;
            t.Start();
        }

        /// <summary>等第一行，超时返回 false（绝不再无限阻塞）。</summary>
        public bool WaitFirst(int milliseconds)
        {
            int waited = 0;
            while (!gotFirst && waited < milliseconds)
            {
                System.Threading.Thread.Sleep(20);
                waited += 20;
            }
            return gotFirst;
        }

        /// <summary>留证的最后 n 行（换行拼接；没有就返回空串）。看门狗快照用。</summary>
        public string Tail(int n)
        {
            lock (gate)
            {
                if (recent.Count == 0)
                {
                    return "";
                }
                int from = recent.Count > n ? recent.Count - n : 0;
                return string.Join("\n", recent.GetRange(from, recent.Count - from).ToArray());
            }
        }
    }

    /// <summary>
    /// 收尾：杀掉某个子进程并等它真正退出。
    /// </summary>
    private static void KillQuietly(System.Diagnostics.Process proc)
    {
        if (proc == null)
        {
            return;
        }
        try
        {
            if (!proc.HasExited)
            {
                proc.Kill();
                proc.WaitForExit(3000);
            }
        }
        catch (System.Exception)
        {
        }
    }

    void onStart()
    {
        if (!isShowed)
        {
            return;
        }
        int sel = superScrollView.selectedIndex;
        if (sel < 0 || sel >= Bots.Count)
        {
            return;
        }

        string aiCommand = Bots[sel].command;
        Match match = Regex.Match(aiCommand, "Random=(\\w+)");
        if (match.Success)
        {
            string randomFlag = match.Groups[1].Value;
            string command = GetRandomBot(randomFlag);
            if (command != "")
            {
                aiCommand = command;
            }
        }

        // quickTestEntry=false：这是主菜单「人机对战」，走完整的房间流程，
        // 打完之后回人机界面（而不是像卡组界面的「测试」那样回卡组编辑器）。
        if (!launch(aiCommand, UIHelper.getByName<UIToggle>(gameObject, "lockhand_").value, UIHelper.getByName<UIToggle>(gameObject, "nocheck_").value, UIHelper.getByName<UIToggle>(gameObject, "noshuffle_").value, false))
        {
            RMSshow_none(InterString.Get("AI 服务器或 WindBot 没起来，已放弃这一局。"));
        }
    }

    /// <summary>
    /// 「主菜单人机对战」的无界面入口（验收/排查用，见 Menu 的 `ai` 命令）。
    ///
    /// 和 <see cref="onStart"/> 做的是同一件事（选一个机器人 → launch，且**不**置
    /// `Room.quickStart`），只是把「点开人机界面、在列表里选对手、读三个开关」这几步
    /// 换成参数 —— 验收脚本没法可靠点中那几颗按钮。
    ///
    /// 房间（准备/开始）界面随后照常出场，玩家（或 qt_autojoin.on）照常走。
    /// </summary>
    public bool launchMenuStyleDuel(int botIndex, bool lockhand, bool nocheck, bool noshuffle)
    {
        EnsureBots();
        if (Bots.Count == 0)
        {
            QuickTestTrace.Log("ai", "launchMenuStyleDuel: 没有可用机器人（mode="
                + GameModeManager.ModeLabel + " conf=" + GameModeManager.BotConf + "）");
            return false;
        }
        if (botIndex < 0 || botIndex >= Bots.Count)
        {
            botIndex = 0;
        }
        string cmd = ResolveBotCommand(Bots[botIndex].command);
        QuickTestTrace.Log("ai", "launchMenuStyleDuel bot#" + botIndex + " cmd=" + cmd
            + " lockhand=" + lockhand + " nocheck=" + nocheck + " noshuffle=" + noshuffle);
        return launch(cmd, lockhand, nocheck, noshuffle, false);
    }

    void printFile()
    {
        superScrollView.clear();
        foreach (var bot in Bots)
        {
            superScrollView.add(bot.name);
        }
    }

    public override void show()
    {
        base.show();
        printFile();
        onSelected();
        Program.charge();
    }

    #endregion

    PrecyOcg precy;

    /// <summary>最近一次人机开局的随机种子（见 launch 里的说明）。</summary>
    public static uint lastSeed = 0;

    /// <summary>最近一次人机开局最终传给 WindBot 的完整命令行（含 Deck=/Hand=/Port=）。</summary>
    public static string lastBotCommand = "";

    /// <summary>最近一次人机开局的「不检查卡组」开关。</summary>
    public static bool lastNoCheck = false;

    /// <summary>最近一次人机开局的「不洗切卡组」开关。</summary>
    public static bool lastNoShuffle = false;

    /// <summary>
    /// 把「Random=AI_LVn」这类随机对手解析成一条确定的 WindBot 命令行。
    ///
    /// bot.conf 里的 AI_LVn 是**机器人条目**上的标签，随机发生在客户端这一侧：
    /// 解析结果必定带着明确的 Deck=xxx，也就是说对手卡组在开局前就已经定下来了。
    /// 撤回要靠这一点：只要记下这条最终命令，重建时就能让对手用回同一副卡组。
    ///
    /// 调试用：可执行文件同目录下存在 qt_bot.txt 时，直接用它里面的命令（实验用固定对手）。
    /// </summary>
    private string ResolveBotCommand(string raw)
    {
        try
        {
            if (System.IO.File.Exists(QuickTestTrace.LogPath("qt_bot.txt")))
            {
                string forced = System.IO.File.ReadAllText(QuickTestTrace.LogPath("qt_bot.txt")).Trim();
                if (forced.Length > 0)
                {
                    QuickTestTrace.Log("bot", "qt_bot.txt 强制对手: " + forced);
                    return forced;
                }
            }
        }
        catch (System.Exception)
        {
        }
        // 展开 Random=标签：从 Bots 里挑一个带该标签的具体对手。
        // 必须在客户端这边定死 —— 老版 WindBot 收到 Random= 时会自己再随机一次，
        // 两次启动可能选出不同卡组，撤回重建的对局就会从第一包起对不上。
        if (raw != null && raw.StartsWith("Random=", StringComparison.Ordinal))
        {
            string flag = raw.Substring(7).Trim();
            IList<BotInfo> found = new List<BotInfo>();
            foreach (var bot in Bots)
            {
                if (bot.command != null && bot.command.StartsWith("Random=", StringComparison.Ordinal))
                {
                    continue;   // 跳过「随机-xx」条目本身，只考虑具体对手
                }
                if (Array.IndexOf(bot.flags, flag) >= 0)
                {
                    found.Add(bot);
                }
            }
            if (found.Count > 0)
            {
                BotInfo pick = found[UnityEngine.Random.Range(0, found.Count)];
                QuickTestTrace.Log("bot", "Random=" + flag + " -> " + pick.command
                    + " (候选 " + found.Count + " 个)");
                return pick.command;
            }
            QuickTestTrace.Log("bot", "Random=" + flag + " 无候选，原文下发");
        }
        return raw;
    }

    /// <summary>
    /// 撤回重开用的强制种子：非 0 时 <see cref="NextSeed"/> 直接用它。
    ///
    /// 重开必须复用**这一局原本那颗种子**（洗牌、掷骰、效果随机全靠它），
    /// 而 NextSeed 平时是随机取的 —— 没有这个口子就重现不出同一局。
    /// 由 DuelUndo.Restart() 置位；一局正常开局时由 DuelUndo.Reset() 清回 0。
    /// </summary>
    public static uint forcedSeed = 0;

    /// <summary>
    /// 撤回重开时强制给 WindBot 的 `Hand=`（1/2/3），0 = 不发。
    ///
    /// 为什么需要：猜拳的胜负会写进第一条 `GameMessage.Start` 的 `playertype`，先手换了人
    /// 整条入站流就从第 0 条对不上。而 `锁定手牌` 关着时 WindBot 的猜拳是**它自己随便出的**，
    /// 只把玩家那一手录下来重答，等于把胜负重掷一次骰子。这里把对手也钉回它当初出的那一手
    /// （取自已录到的那一轮结果，见 DuelTimeline.Snapshot.BotHandOverride）。
    ///
    /// ⚠ 只在 `lockHand == false` 时才会置位：锁定手牌那条路已经会往命令行追加 `Hand=1`，
    /// 再来一个是**重复键**，WindBot 的 Config.LoadArgs 会直接抛异常、机器人启动即崩。
    ///
    /// 由 DuelUndo.Restart() 置位，launch() 消费一次（用完清 0），DuelUndo.Reset() 兜底清零。
    /// </summary>
    public static int forcedBotHand = 0;

    /// <summary>
    /// 生成本局的随机种子。
    /// 调试用：可执行文件同目录下存在 qt_seed.txt 时，固定使用文件里的数值 ——
    /// 用来验证「同一颗种子跑两遍，摸牌与整条消息流是否完全一致」。
    /// </summary>
    private static uint NextSeed()
    {
        if (forcedSeed != 0)
        {
            QuickTestTrace.Log("seed", "forcedSeed=" + forcedSeed + "（撤回重开）");
            return forcedSeed;
        }
        try
        {
            if (QuickTestTrace.FreshDataFile("qt_seed.txt"))
            {
                uint fixedSeed;
                if (uint.TryParse(System.IO.File.ReadAllText(QuickTestTrace.LogPath("qt_seed.txt")).Trim(), out fixedSeed) && fixedSeed > 0)
                {
                    return fixedSeed;
                }
            }
        }
        catch (System.Exception)
        {
        }
        uint seed = 0;
        System.Random r = new System.Random();
        while (seed == 0)
        {
            seed = (uint)r.Next(1, int.MaxValue);
        }
        return seed;
    }

    /// <summary>
    /// 开一局人机对局。返回 false = 服务器或机器人没能起来（调用方负责给玩家一条出路）。
    ///
    /// 撤回重开也走这里。以前这条路径的失败方式是**静默**的：端口那一行、机器人首行
    /// 都用阻塞的 ReadLine()，任何一步出问题就永远停在那儿；即使侥幸过了，也要等
    /// HsStart 重试 8 次才 GIVE UP。玩家看到的就是「按了撤回以后卡住」。
    /// 现在每一步都有超时、都有明确的失败返回值。
    /// </summary>
    /// <param name="quickTestEntry">
    /// 这一局是不是「卡组界面测试」入口（`tryLaunchQuickTest`）。它只影响两件事：
    /// 开局后回哪个界面、以及撤回重开时还原成哪一种局（见 <see cref="DuelTimeline.quickTest"/>）。
    /// 主菜单「人机对战」传 false。
    /// </param>
    public bool launch(string command, bool lockhand, bool nocheck, bool noshuffle, bool quickTestEntry)
    {
        // 普通开局走完整流程（房间窗口手动准备 + 猜拳 + 选先后攻）
        Room.quickStart = false;
        killServerProcess();

        // 撤回重开会再次走这里。那一轮要保留重放状态（saved / targetKeep / active），
        // 其余情况都是「全新一局」，必须把上一局的撤回状态连同 forcedSeed 一起清掉 ——
        // forcedSeed 不清的话，之后每一局都会一直用同一颗种子。
        if (DuelUndo.restarted)
        {
            DuelUndo.restarted = false;
        }
        else
        {
            DuelUndo.Reset();
        }
        command = command.Replace("'", "\"");

        // 随机种子由我们（客户端）决定并随参数下发，而不是让服务端自己乱选。
        // AI.Server 的参数表里 argv[13..15] 就是 pre_seed（见 ygoserver/gframe.cpp），
        // pre_seed>0 时它不再调用 rd()，整局洗牌/随机效果都由这个种子决定。
        // 撤回就是拿着同一颗种子重建一局、把历史响应重放回去 —— 没有这颗种子就无从重建。
        uint seed = NextSeed();

        // 撤回用的时间线：在这里开录。录的是**命令行模板**，两个东西都必须在拼上去之前录 ——
        //  ・Port=  ：服务器每次启动现给，重放时要换成新的那个；
        //  ・Hand=1 ：由 lockHand 单独记录，重开时再拼一次。录进来的话会拼成
        //            "Hand=1 Hand=1"，而 WindBot 的 Config.LoadArgs 对重复键是**直接抛异常**
        //            （Config.cs: duplicate key），机器人启动即崩、连不上来，
        //            对局就卡在 HsStart 重试到 GIVE UP（实测踩过）。
        DuelTimeline.NoteOpen(seed, command, GameModeManager.DeckInUse, lockhand, nocheck, noshuffle, quickTestEntry);

        if (lockhand) command += " Hand=1";
        else if (forcedBotHand >= 1 && forcedBotHand <= 3)
        {
            // 撤回重开：把**对手**的猜拳钉成「我方第一手必胜」的那一张，于是第一轮必定分胜负、
            // 先手归我方 ⇒ 先手就由我方的先后攻应答完全决定（见 DuelTimeline.Snapshot.BotHandOverride）。
            // 不钉的话它每次随便出，先手就换人，整条入站流从第 0 条摘要不符。
            command += " Hand=" + forcedBotHand;
            QuickTestTrace.Log("launch", "forcedBotHand=" + forcedBotHand + " 已拼进 WindBot 命令行");
        }
        // 一次性：无论用没用上，都在这里清掉，免得漏到下一局。
        forcedBotHand = 0;

        // ① 服务器：起进程 → 等它报出端口（stdout 首行）。超时就放弃，绝不无限等。
        //
        // 路径与 cwd 都按模式走（见 GameModeManager 的「人机路径」段）：AI.Server 的脚本/卡表
        // 路径是相对 cwd 解析的，RD 那套规则脚本只能待在自己的 rd/ai/ 里。这里用
        // GetFullPath 钉成绝对路径 —— 免得「FileName 是相对路径 + WorkingDirectory 另设」
        // 时到底按哪个目录找 exe 这件事取决于 Windows 的搜索顺序。
        string serverExe = System.IO.Path.GetFullPath(GameModeManager.AiServerExe);
        string serverDir = System.IO.Path.GetFullPath(GameModeManager.AiServerDir);
        if (!System.IO.File.Exists(serverExe))
        {
            QuickTestTrace.Log("launch", "AI.Server 不存在: " + serverExe
                + "（mode=" + GameModeManager.ModeLabel
                + (GameModeManager.IsRD ? "；rd/ai/ 由 devtools/unpack_rd.py 铺，重建 output/ 后要重跑" : "")
                + "）");
            return false;
        }
        serverProcess = new System.Diagnostics.Process();
        serverProcess.StartInfo.UseShellExecute = false;
        serverProcess.StartInfo.FileName = serverExe;
        serverProcess.StartInfo.WorkingDirectory = serverDir;
        // 参数表（第 1 格 = 端口，不含 exe）。2026-09-21 用直连探针把每一格的落点实测了一遍
        // （`_probe_argtable.py`：一次只动一格，看回过来的 STOC_JoinGame 哪个字段跟着动）：
        //   1  [port]             7911
        //   2  [?]                -1        ← 改成 -9 无任何字段变化，用途未定
        //   3  [卡池]             5         ← JoinGame.rule：0 OCG/1 TCG/2 简中/3 自制/4 无独有/5 混合
        //   4  [mode]             0         ← JoinGame.mode：0 单局/1 比赛/2 双打
        //   5  **[duel_rule]**    ← 按模式取（RD=3 对齐联机，OCG=5）→ 客户端读成 Ocgcore.MasterRule
        //   6  [nocheck]          T/F       ← JoinGame.no_check_deck
        //   7  [noshuffle]        T/F       ← JoinGame.no_shuffle_deck
        //   8  [start_lp]         8000
        //   9  **[初始手牌]**     ← 按模式取（RD=4 / OCG=5）
        //   10 [draw_count]       1
        //   11 [time_limit]       0
        //   12 [?]                0         ← 改成 9 无任何字段变化，用途未定
        //   13 [seed]
        // ⛔ 第 3 格是**卡池**不是规则号，别把 DuelRule 塞回那里（细节与实测见
        //   GameModeManager.DuelRule 的注释）。
        // 初始手牌按模式取：OCG 5 张、RD 4 张（RD 规则「初期手卡 4 张」）。
        // 这里喂错一张的连锁是「整局起手多一张」，且**改 lua 补不回来**（起手先于任何 lua 效果），
        // 判定性实测与理由见 GameModeManager.StartHand 的注释。
        serverProcess.StartInfo.Arguments = "7911 -1 5 0 " + GameModeManager.DuelRule + " " + (nocheck ? "T" : "F") + " " + (noshuffle ? "T" : "F") + " 8000 " + GameModeManager.StartHand + " 1 0 0 " + seed;
        serverProcess.StartInfo.CreateNoWindow = true;
        serverProcess.StartInfo.RedirectStandardOutput = true;
        // stderr 也接管（同样会被 StdoutDrain 读干）：AI.Server 的 lua 报错走这里，
        // 不接管就继承无效句柄、报错直接消失，「卡住」时永远缺第一手证据。
        serverProcess.StartInfo.RedirectStandardError = true;
        // 这两个子进程的输出是 **GBK**（2026-09-19 手动抓原始字节坐实：「齿车戒龙」=
        // B3DD B3B5 BDE4 C1FA）。Unity/Mono 的 Encoding.Default 是 UTF-8，用它解 GBK
        // 会得到一串 U+FFFD —— 中文全废。I18N.CJK.dll 已随包（游戏本来就读 GBK 的
        // conf），GetEncoding(936) 可用；万一哪天被裁，退回 UTF-8 只是乱码、不崩。
        serverProcess.StartInfo.StandardOutputEncoding = AiChildEncoding();
        serverProcess.StartInfo.StandardErrorEncoding = AiChildEncoding();
        StdoutDrain serverOut = new StdoutDrain();
        try
        {
            serverProcess.Start();
            serverOut.Start(serverProcess, QuickTestTrace.LogPath("ai_server_"
                + serverProcess.Id + ".log"));
        }
        catch (System.Exception e)
        {
            QuickTestTrace.Log("launch", "AI.Server 起不来: " + e.Message);
            serverProcess = null;
            return false;
        }
        if (!serverOut.WaitFirst(15000))
        {
            QuickTestTrace.Log("launch", "AI.Server 15s 内没报出端口 (hasExited="
                + SafeHasExited(serverProcess) + ") -> 放弃");
            KillQuietly(serverProcess);
            serverProcess = null;
            return false;
        }
        string port = serverOut.firstLine.Trim();
        command += " Port=" + port;

        // ② 机器人：起进程 → 等它就绪（首行）。它启动即崩是「永远等不到 DuelStart」
        //    最常见的原因，这里当场就能看出来，不用等 HsStart 重试到 GIVE UP。
        string botExe = System.IO.Path.GetFullPath(GameModeManager.WindBotExe);
        string botDir = System.IO.Path.GetFullPath(GameModeManager.WindBotDir);
        if (!System.IO.File.Exists(botExe))
        {
            QuickTestTrace.Log("launch", "WindBot 不存在: " + botExe
                + "（mode=" + GameModeManager.ModeLabel + "）");
            KillQuietly(serverProcess);
            serverProcess = null;
            return false;
        }
        botProcess = new System.Diagnostics.Process();
        botProcess.StartInfo.UseShellExecute = false;
        botProcess.StartInfo.FileName = botExe;
        botProcess.StartInfo.WorkingDirectory = botDir;
        botProcess.StartInfo.Arguments = command;
        botProcess.StartInfo.CreateNoWindow = true;
        botProcess.StartInfo.RedirectStandardOutput = true;
        botProcess.StartInfo.RedirectStandardError = true;
        botProcess.StartInfo.StandardOutputEncoding = AiChildEncoding();
        botProcess.StartInfo.StandardErrorEncoding = AiChildEncoding();
        StdoutDrain botOut = new StdoutDrain();
        try
        {
            botProcess.Start();
            botOut.Start(botProcess, QuickTestTrace.LogPath("ai_windbot_"
                + botProcess.Id + ".log"));
        }
        catch (System.Exception e)
        {
            QuickTestTrace.Log("launch", "WindBot 起不来: " + e.Message);
            KillQuietly(botProcess);
            KillQuietly(serverProcess);
            botProcess = null;
            serverProcess = null;
            return false;
        }
        if (!botOut.WaitFirst(20000))
        {
            QuickTestTrace.Log("launch", "WindBot 20s 内没就绪 (hasExited="
                + SafeHasExited(botProcess) + ") -> 放弃; cmd=" + command);
            KillQuietly(botProcess);
            KillQuietly(serverProcess);
            botProcess = null;
            serverProcess = null;
            return false;
        }

        QuickTestTrace.Log("launch", "bot=" + command + " lockhand=" + lockhand + " nocheck=" + nocheck + " noshuffle=" + noshuffle
            + " mode=" + GameModeManager.ModeLabel + " server=" + serverExe + " botdir=" + botDir
            // 初始手牌是「开局抽几张」的唯一来源（命令行第 9 格），离线排查时看这一条就够，
            // 不必去猜 core。验收脚本也按它判「RD 给的是 4 不是 5」。
            + " startHand=" + GameModeManager.StartHand
            // 规则号（命令行第 5 格）：开局后 core 下发的 MasterRule 就是它 ——
            // 验收探针 [rule]/[field] 那几行与本行对照着看。
            + " duelRule=" + GameModeManager.DuelRule);

        ChildProcessTracker.AddProcess(serverProcess);
        ChildProcessTracker.AddProcess(botProcess);
        // 留证器挂到字段上：看门狗判「卡死/子进程退出」时要用它取输出尾部快照。
        this.serverOut = serverOut;
        this.botOut = botOut;
        // 从这一刻起「这是一局本机 AI 对局」：断线收尾与看门狗都据此分流。
        IsAiSessionLive = true;

        string name = Config.Get("name", "一秒一咕机会");
        Program.I().ocgcore.returnServant = Program.I().aiRoom;
        (new Thread(() => { Thread.Sleep(500); TcpHelper.join("127.0.0.1", name, port, "", ""); })).Start();
        RMSshow_none(InterString.Get("您在AI模式下遇到的BUG也极有可能会在联机的时候出现，所以请务必向我们报告。"));
        return true;
    }

    /// <summary>AI 子进程输出的解码口径：GBK（见 launch 里的字节级证据），裁了就退 UTF-8。</summary>
    private static System.Text.Encoding AiChildEncoding()
    {
        try
        {
            return System.Text.Encoding.GetEncoding(936);
        }
        catch (System.Exception)
        {
            return System.Text.Encoding.UTF8;
        }
    }

    private static string SafeHasExited(System.Diagnostics.Process proc)
    {
        try
        {
            return proc.HasExited ? ("yes/" + proc.ExitCode) : "no";
        }
        catch (System.Exception)
        {
            return "?";
        }
    }

    /// <summary>
    /// MDPro3 式卡组测试入口：跳过对手选择界面，直接以第一个 WindBot 开局。
    ///
    /// 参数口径对齐 MDPro3 的 StartAIForHandTest：
    ///   lockHand  = 开 —— 给 WindBot 传 Hand=1，把它的猜拳锁成「剪刀」；
    ///                     配合 Room.QuickStartHand=2（石头）必胜猜拳，先攻必定归玩家。
    ///   noCheck   = 开 —— 测试中的卡组往往未满编，不检查卡组；
    ///   noShuffle = 默认开 —— 按卡组顺序摸牌，便于验证起手与展开。
    ///
    /// 对局读取的是 deck/deckInUse.ydk（TcpHelper/Room 的既有逻辑），调用方需先保存当前卡组。
    /// 另外置位 Room.quickStart：进房后自动准备/开局，房间界面全程不出场，直达玩家第一个回合。
    /// 连接是在 launch() 里另起线程延时 500ms 才发起的，所以这里在 launch() 之后置位不会被抢先消费。
    /// </summary>
    public void launchQuickTest()
    {
        launchQuickTest(true);
    }

    /// <summary>
    /// 同上，但可指定是否洗切。noShuffle=false 时就是一场「正常随机洗牌」的人机对局，
    /// 用来验证「同一颗种子 → 同一局」（撤回功能重建对局的前提）。
    /// </summary>
    public void launchQuickTest(bool noShuffle)
    {
        string failHint;
        if (!tryLaunchQuickTest(noShuffle, out failHint) && !string.IsNullOrEmpty(failHint))
        {
            RMSshow_none(failHint);
        }
    }

    /// <summary>
    /// 真正干活的那个：返回这一局有没有起来；起不来时把原因写进 failHint，**不在这里弹提示**。
    ///
    /// 为什么要把「弹提示」摘出去：卡组界面的「点测试 → 收起界面 → 开局」转场会先把
    /// 界面收掉再去开局，而提示是弹在卡牌说明面板里的（RMSshow_none → cardDescription.mLog）。
    /// 界面没放回来就弹提示，等于弹在空屏上，玩家根本看不见 —— 所以要由调用方
    /// 决定「先把界面放回来，再报错」。
    /// </summary>
    public bool tryLaunchQuickTest(bool noShuffle, out string failHint)
    {
        failHint = null;
        QuickTestTrace.Log("quick", "launchQuickTest enter, Bots=" + Bots.Count + " noShuffle=" + noShuffle);
        EnsureBots();
        if (Bots.Count == 0)
        {
            QuickTestTrace.Log("quick", "no bots -> abort (mode=" + GameModeManager.ModeLabel
                + " conf=" + GameModeManager.BotConf + ")");
            failHint = InterString.Get("找不到可用的 WindBot。");
            return false;
        }
        if (!launch(ResolveBotCommand(Bots[0].command), true, true, noShuffle, true))
        {
            QuickTestTrace.Log("quick", "launch failed -> abort");
            failHint = InterString.Get("AI 服务器或 WindBot 没起来，已放弃这一局。");
            return false;
        }
        Room.quickStart = true;
        QuickTestTrace.Log("quick", "launch() done, Room.quickStart=true");
        // 测试是从卡组界面发起的，打完回卡组界面继续改卡组（MDPro3 同样回 deckEditor）
        Program.I().ocgcore.returnServant = Program.I().deckManager;
        return true;
    }

    /// <summary>
    /// 卡组测试的「Shell 直开局」入口。
    ///
    /// 走的是和点「测试」按钮完全相同的两条调用：先 onSave() 把当前卡组写进
    /// deck/deckInUse.ydk，再 launchQuickTest() 开局。
    /// 存在的意义是把「按钮点击」这一段从排查链路里摘出去 —— 命令通道不依赖鼠标坐标，
    /// 若本入口能直达决斗，就说明问题在按钮/坐标；若也不能，才在开局链路里找。
    /// </summary>
    public void quickTestFromShell()
    {
        quickTestFromShell(true);
    }

    /// <summary>同上；noShuffle=false 时按正常随机洗牌开局（用于「同种子 = 同一局」验证）。</summary>
    public void quickTestFromShell(bool noShuffle)
    {
        QuickTestTrace.Log("shell", "quickTestFromShell enter noShuffle=" + noShuffle);
        // 这是排查/回归用的命令入口（只在主菜单显示时可用，见 Servant.Update 的 isShowed 门槛）：
        // 直接按当前模式记的那个卡组（deckInUse / deckInUse_rd）从 disk 读卡组开局，
        // 绕过卡组编辑器，这样「按钮点击」和「卡组加载」两段都不会干扰对开局链路本身的验证。
        string deckName = GameModeManager.DeckInUse;
        if (deckName != "" && System.IO.File.Exists(GameModeManager.DeckPath(deckName)))
        {
            QuickTestTrace.Log("shell", "launch directly with " + GameModeManager.DeckPath(deckName) + " mode=" + GameModeManager.ModeLabel);
            launchQuickTest(noShuffle);
            return;
        }
        if (Program.I().deckManager == null)
        {
            QuickTestTrace.Log("shell", "deckManager is null -> abort");
            return;
        }
        if (!Program.I().deckManager.onSave())
        {
            QuickTestTrace.Log("shell", "onSave failed -> abort");
            return;
        }
        QuickTestTrace.Log("shell", "onSave ok, launching");
        launchQuickTest(noShuffle);
    }

    public override void preFrameFunction()
    {
        base.preFrameFunction();
        Menu.checkCommend();
    }
}

/// <summary>
/// 卡组测试直开局的关键节点轨迹。
///
/// 本工程的 Debug.Log 既不进 Player.log 也不进 stdout，排查只能靠自己写文件。
/// 文件名带进程号，保证每次运行都是一个全新的文件（避免踩到「已存在文件不可写」的坑）。
///
/// 所有排查产物（轨迹、时间线 dump、开关文件）一律落在 exe 同级的 log/ 子目录里，
/// 不直接堆在产物根目录 —— 产物根目录只应出现游戏本体。
/// </summary>
public static class QuickTestTrace
{
    /// <summary>排查产物的存放目录（相对 exe 同级）。</summary>
    public const string Dir = "log";

    private static string fileName = null;
    private static int enabled = -1;   // -1 未知 / 0 关 / 1 开
    private static bool dirReady = false;

    /// <summary>
    /// 把排查用的文件/开关名解析成 log/ 下的路径，并保证目录存在。
    /// 开关文件（qt_debug.on 等）也在这里读，所以调试时把它们放进 log/ 即可。
    /// </summary>
    public static string LogPath(string name)
    {
        if (!dirReady)
        {
            try
            {
                System.IO.Directory.CreateDirectory(Dir);
            }
            catch (System.Exception)
            {
            }
            dirReady = true;
        }
        return Dir + "/" + name;
    }

    /// <summary>只有 log/ 下存在 qt_debug.on（且没过期）时才写轨迹，正式跑零开销。</summary>
    public static bool Enabled
    {
        get
        {
            if (enabled < 0)
            {
                enabled = SwitchOn(MasterSwitch) ? 1 : 0;
            }
            return enabled == 1;
        }
    }

    // ============================ 调试开关的统一入口 ============================

    /// <summary>总开关：它不在就什么都不记（正式包零开销的前提）。</summary>
    public const string MasterSwitch = "qt_debug.on";

    /// <summary>
    /// 开关文件允许比本进程「年长」多少秒，超了就当**上一轮遗留**作废。
    ///
    /// 为什么必须有这一条（2026-09-19 用户实测）：验收脚本是「先写开关、再起游戏」，
    /// 而脚本中途挂掉 / 被沙箱删除守卫拦下时，log/ 里会留下一枚 `qt_endduel.on`。
    /// 它下一次会被**当成这次会话的意图**照单执行 —— 卡组编辑器一打开就自动点「测试」
    /// （qt_endduel.on），人机房间不进也自动准备并自动答掉猜拳（qt_autojoin.on）。
    /// 玩家看到的是「我什么都没点，它自己打起来了」，而且**只在残留那几次复现**。
    ///
    /// 判据用「mtime 是否早于本进程启动时刻太多」而不是「绝对时长」：
    /// 合法用法是起游戏前几秒写开关（远小于宽限），残留是上一轮写的（分钟级起步），
    /// 而且**与本次会话跑多久无关** —— 长会话不会因为跑过 10 分钟就把自己的开关判过期。
    ///
    /// 宽限取 180s（2026-09-19 由 600s 收紧）：所有合法写入方都是
    /// 「写完开关下一行就启游戏」（见 _verify_rdmode/_verify_rdmenu/_verify_rdai_game），
    /// 秒级完成，180s 留了三四十倍余量；收紧的收益是**残留开关的存活窗口从 10 分钟缩到 3 分钟**
    /// —— 用户报的现象正是「上一轮脚本挂掉后紧接着再开游戏」，窗口越短越难撞上。
    /// 真需要更长的会话请在开关文件里写一个正整数秒数覆盖（见 <see cref="SwitchGrace"/>）。
    /// </summary>
    public const int SwitchGraceSeconds = 180;

    /// <summary>name -> 是否已判为「过期作废」。只缓存这一个结论：过期是永久的，没过期要随文件消失而失效。</summary>
    private static readonly System.Collections.Generic.Dictionary<string, bool> switchStale
        = new System.Collections.Generic.Dictionary<string, bool>();

    /// <summary>
    /// 「上次探的时候这枚文件不在」可以沿用多久（毫秒），把每帧一次的文件探测摊薄成每 0.5s 一次。
    ///
    /// 为什么只缓存「不在」：正式包里所有 qt_* 文件都不存在，热路径上问的就是这个方向；
    /// 而「在」的那个方向必须每次都真探 —— 脚本删掉开关之后，游戏得**下一帧**就停手，
    /// 否则「残留开关」那类问题又会以「删了还在生效」的形式回来。
    /// 0.5s 对合法用法远远够用：所有写入方都是「写完开关，然后在秒级/几十秒的等待里观察」。
    /// </summary>
    public const int SwitchPollMs = 500;

    /// <summary>name -> 上次探到「它不在」的 TickCount。</summary>
    private static readonly System.Collections.Generic.Dictionary<string, int> switchMissingAt
        = new System.Collections.Generic.Dictionary<string, int>();

    /// <summary>本进程的启动时刻（判开关新旧用）。取不到就退化成「第一次问开关的时刻」。</summary>
    private static System.DateTime processStart = System.DateTime.MinValue;

    private static System.DateTime ProcessStart
    {
        get
        {
            if (processStart == System.DateTime.MinValue)
            {
                try
                {
                    processStart = System.Diagnostics.Process.GetCurrentProcess().StartTime;
                }
                catch (System.Exception)
                {
                    processStart = System.DateTime.Now;
                }
            }
            return processStart;
        }
    }

    /// <summary>开关文件里写个正整数可以覆盖宽限秒数（长时间会话用），留空就用 <see cref="SwitchGraceSeconds"/>。</summary>
    private static int SwitchGrace(string path)
    {
        try
        {
            string s = System.IO.File.ReadAllText(path).Trim();
            int v;
            if (s.Length > 0 && int.TryParse(s, out v) && v > 0)
            {
                return v;
            }
        }
        catch (System.Exception)
        {
        }
        return SwitchGraceSeconds;
    }

    /// <summary>
    /// 读一枚调试开关（log/ 下的 qt_xxx.on）。**所有开关都必须走这里**，不要再直接 File.Exists：
    /// 直接判存在就会把上一轮遗留的开关当成这次的意图，见 <see cref="SwitchGraceSeconds"/>。
    /// </summary>
    public static bool SwitchOn(string name)
    {
        return Fresh(name, "开关");
    }

    /// <summary>
    /// 同上，给「内容型」调试文件用（qt_seed.txt 固定种子 / qt_bot.txt 固定对手）。
    /// 它们不是开关而是参数：残留一枚就会让之后每一局都拿着同一个种子开，
    /// 表现是「洗牌怎么老是这样」—— 比开关更难看出来，所以同样要判过期。
    /// </summary>
    public static bool FreshDataFile(string name)
    {
        return Fresh(name, "排查数据文件");
    }

    /// <summary>
    /// 判「这份排查用文件还属于本次会话吗」。过期的就地删掉（并把这件事记进 log/qt_stale.log），
    /// 免得它继续毒害后面每一次启动。判定规则见 <see cref="SwitchGraceSeconds"/>。
    ///
    /// ⚡ 性能：这里有一个**只缓存「不在」这一个方向**的节流（见 <see cref="SwitchPollMs"/>）——
    /// 因为本函数会被调在每帧的热路径上（`Program.Update` 的 runInBackground 兜底、
    /// `Room.preFrameFunction` 的自动进房）。实测一次 `File.Exists` 是 17us（`GetFileAttributesW`，
    /// 杀软过滤驱动会拖慢），每帧一次 = 一帧预算的 0.1%（60fps）；而正式包里这些文件**永远不存在**，
    /// 那就是纯浪费。反方向（文件在）**不缓存**，每次真探：脚本一删开关，游戏下一帧就停手，
    /// 语义与本轮改造前完全一致。
    /// </summary>
    private static bool Fresh(string name, string kind)
    {
        bool stale;
        if (switchStale.TryGetValue(name, out stale) && stale)
        {
            return false;
        }
        int missingSeen;
        // Environment.TickCount 而不是 Program.TimePassed()：后者基于 Time.time，
        // 会被 Time.timeScale=0 冻住 ⇒ 暂停期间写下的开关会永远看不到。
        // int 相减天然按二进制补码回绕，跨越 TickCount 的 24.9 天周期也算得对。
        if (switchMissingAt.TryGetValue(name, out missingSeen)
            && System.Environment.TickCount - missingSeen < SwitchPollMs)
        {
            return false;
        }
        string path = LogPath(name);
        bool exists;
        try
        {
            exists = System.IO.File.Exists(path);
        }
        catch (System.Exception)
        {
            return false;
        }
        if (!exists)
        {
            switchMissingAt[name] = System.Environment.TickCount;
            return false;
        }
        switchMissingAt.Remove(name);
        if (!switchStale.ContainsKey(name))
        {
            try
            {
                int age = (int)(ProcessStart - System.IO.File.GetLastWriteTime(path)).TotalSeconds;
                int grace = SwitchGrace(path);
                if (age > grace)
                {
                    switchStale[name] = true;
                    StaleNote(name + "（" + kind + "）比本次启动早 " + age + "s（宽限 " + grace
                        + "s）-> 判为上一轮遗留，本次忽略");
                    try
                    {
                        System.IO.File.Delete(path);
                        StaleNote(name + " 已删除，避免下次再被误认");
                    }
                    catch (System.Exception e)
                    {
                        StaleNote(name + " 删除失败：" + e.Message);
                    }
                    return false;
                }
            }
            catch (System.Exception)
            {
            }
        }
        return true;
    }

    /// <summary>
    /// 作废开关的留痕。**不能走 Log()** —— 它在 <see cref="Enabled"/> 里面被调用，
    /// 而 Enabled 又是通过 SwitchOn 判出来的，那就递归了。这里固定写一个独立小文件。
    /// </summary>
    private static void StaleNote(string msg)
    {
        try
        {
            System.IO.File.AppendAllText(Dir + "/qt_stale.log",
                System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " " + msg + "\r\n");
        }
        catch (System.Exception)
        {
        }
    }

    /// <summary>
    /// 起手把已知开关扫一遍：过期的就地删掉并留痕。每次启动只做一次。
    ///
    /// 单独有这个「扫」而不只是靠 SwitchOn 的懒判定：有的开关（qt_endduel.on）
    /// 可能要几分钟后才有人问，那时候才删就已经太晚了 —— 它中间的每一次 File.Exists
    /// 都可能已经触发过一次动作。
    /// </summary>
    public static void SweepStaleSwitches()
    {
        string[] known = new string[]
        {
            MasterSwitch, "qt_autojoin.on", "qt_endduel.on",
            "qt_undo.on", "qt_undoreplay.on", "qt_memo_order.on",
            UiDumpSwitch,
            "qt_seed.txt", "qt_bot.txt",
        };
        for (int i = 0; i < known.Length; i++)
        {
            SwitchOn(known[i]);
        }
    }

    private static string FileName
    {
        get
        {
            if (fileName == null)
            {
                try
                {
                    fileName = LogPath("qt_" + System.Diagnostics.Process.GetCurrentProcess().Id + ".log");
                }
                catch (System.Exception)
                {
                    fileName = LogPath("qt_default.log");
                }
            }
            return fileName;
        }
    }

    public static void Log(string tag, string msg)
    {
        if (!Enabled)
        {
            return;
        }
        try
        {
            System.IO.File.AppendAllText(FileName,
                System.DateTime.Now.ToString("HH:mm:ss.fff") + " [" + tag + "] " + msg + "\r\n");
        }
        catch (System.Exception)
        {
        }
    }

    // ============================ UI 文案全量导出（QtUiDump） ============================

    /// <summary>UIDump 的开关：`log/qt_uidump.on`。</summary>
    public const string UiDumpSwitch = "qt_uidump.on";

    /// <summary>两次导出之间的最小间隔（毫秒）。导出要遍历整棵树，别每帧都做。</summary>
    private const int UiDumpIntervalMs = 400;

    private static int uiDumpAtMs = -100000;

    /// <summary>本次会话已经写了多少个块。全局扫的块很大（一屏一两百行），封顶免得刷爆日志。</summary>
    private static int uiDumpBlocks = 0;

    private const int UiDumpMaxBlocks = 2000;

    /// <summary>scene -> 上一次导出出来的整块文本。**变了才写**（与 [opt] 探针同一套口径）。</summary>
    private static readonly System.Collections.Generic.Dictionary<string, string> uiDumpLast
        = new System.Collections.Generic.Dictionary<string, string>();

    /// <summary>
    /// 排查用（`log/qt_uidump.on`）：把**此刻屏幕上所有 UILabel 的文案**落到轨迹里。
    ///
    /// 为什么需要它：界面上出现一行「不该出现的字」时，靠读源码是查不出来的 ——
    /// 那句话可能是别处的翻译表、可能是运行期拼出来的、也可能是某个我以为空着的标签。
    /// 把「这台界面上此刻到底有哪些字、各在哪」原样倒出来，比在几千个字符串里猜快得多，
    /// 而且是**唯一能证明「已经没了」**的证据（导出里搜不到 = 真的不在屏幕上了）。
    ///
    /// ⚠ 为什么挂在 `Program.Update` 上、用 `FindObjectsOfType` 全局扫，而不是挂 servant 的
    ///   preFrameFunction 逐界面导（2026-09-19 实测的教训）：
    ///     ① `ServantWithCardDescription.preFrameFunction` **不调 base**，而卡组编辑器与对局
    ///        都继承它 ⇒ 挂基类时「编辑器整块界面」根本不会被导出，漏掉正是最该看的那一屏；
    ///     ② 「高级搜索」面板是**独立的根节点**（`create(..., ui_main_2d)`），压根不在
    ///        DeckManager 那棵树里，逐 servant 导也够不着。
    ///   全局扫没有这两个盲区，而且 `FindObjectsOfType` 天然只返回**激活**的对象 ——
    ///   正合本题：「界面上有的字」= 屏幕上的字（藏起来的节点不算）。
    ///
    /// 口径：
    ///   · 一行一个标签，带**屏幕坐标**（用 camera_main_2d 换算，与其它坐标探针同源），
    ///     这样才能和截图对上是屏幕上哪一行；
    ///   · 内容与上一次**完全一致就不落行**，所以日志里最后一块就是当前界面的定稿。
    /// </summary>
    public static void UiDumpAll()
    {
        if (!SwitchOn(UiDumpSwitch) || uiDumpBlocks >= UiDumpMaxBlocks)
        {
            return;
        }
        int now = System.Environment.TickCount;
        if (now - uiDumpAtMs < UiDumpIntervalMs)
        {
            return;
        }
        uiDumpAtMs = now;

        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        try
        {
            // ⚠ UILabel 在**全局命名空间**（NGUI 的老代码风格，不在 UnityEngine 下），
            //   所以这里不能写 UnityEngine.UILabel —— 会报 CS0234。
            UILabel[] labels = UnityEngine.Object.FindObjectsOfType<UILabel>();
            sb.Append("scr=").Append(UnityEngine.Screen.width).Append("x").Append(UnityEngine.Screen.height)
              .Append(" labels=").Append(labels.Length)
              .Append(" cam=").Append(Program.camera_main_2d != null ? "main2d" : "null");
            for (int i = 0; i < labels.Length; i++)
            {
                UILabel lab = labels[i];
                if (lab == null)
                {
                    continue;
                }
                string text = lab.text;
                if (string.IsNullOrEmpty(text))
                {
                    continue;
                }
                string pos = "?";
                if (Program.camera_main_2d != null)
                {
                    UnityEngine.Vector3 sp = Program.camera_main_2d.WorldToScreenPoint(lab.transform.position);
                    pos = "(" + UnityEngine.Mathf.RoundToInt(sp.x) + ","
                        + UnityEngine.Mathf.RoundToInt(UnityEngine.Screen.height - sp.y) + ")";
                }
                sb.Append("\r\n    ").Append(i)
                  .Append(" pos=").Append(pos)
                  .Append(" path=").Append(PathOfLabel(lab.transform, null))
                  .Append(" text=").Append(text.Replace("\r", "").Replace("\n", "\\n"));
            }
        }
        catch (System.Exception e)
        {
            sb.Append(" 导出失败：").Append(e.Message);
        }

        string block = sb.ToString();
        if (uiDumpLast.TryGetValue("ALL", out string prev) && prev == block)
        {
            return;
        }
        uiDumpLast["ALL"] = block;
        uiDumpBlocks++;
        Log("uidump", "ALL " + block);
    }

    /// <summary>
    /// 从标签往上走到根，拼成「根/…/子」路径（最多 6 层）。
    /// <paramref name="root"/> 传 null 表示一直走到顶层 —— 全局扫时要的就是它，
    /// 这样一眼看出这一行属于哪扇窗（`x_trans_menu(Clone)/…` / `new_ui_searchDetailed(Clone)/…`）。
    /// </summary>
    private static string PathOfLabel(Transform t, Transform root)
    {
        string p = t.name;
        Transform cur = t.parent;
        for (int i = 0; i < 6 && cur != null && cur != root; i++)
        {
            p = cur.name + "/" + p;
            cur = cur.parent;
        }
        return p;
    }

    /// <summary>
    /// 退出路径的取证探针：谁把游戏关掉了。
    ///
    /// 为什么必须记**调用栈**：全工程能优雅退出的路只有两条，而且它们互相调用 ——
    /// 「主菜单退出图标」和「对局收尾回不去时顺手退出」（<c>Ocgcore.returnTo</c> 里
    /// <c>exitOnReturn</c> 为真时转调同一个 <c>menu.onClickExit</c>）。只看一行「谁被调了」
    /// 分不清是玩家点的还是收尾逻辑转过去的，而这两件事的修法完全不同。
    /// 栈只留最上面三帧，够指出调用来源又不会把日志刷爆。
    /// </summary>
    public static void Exit(string msg)
    {
        if (!Enabled)
        {
            return;
        }
        string st = "";
        try
        {
            string[] frames = new System.Diagnostics.StackTrace(1, false).ToString().Split('\n');
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            for (int i = 0; i < frames.Length && i < 4; i++)
            {
                string f = frames[i].Trim();
                if (f.Length == 0)
                {
                    continue;
                }
                int at = f.IndexOf(" in ");
                if (at > 0)
                {
                    f = f.Substring(0, at);
                }
                sb.Append(i == 0 ? " via " : " <- ").Append(f);
            }
            st = sb.ToString();
        }
        catch (System.Exception)
        {
        }
        Log("exit", msg + st);
    }

    /// <summary>包体摘要，用于跨进程/跨次运行比对同一条消息流是否一致。</summary>
    public static string Hash(byte[] data)
    {
        if (data == null)
        {
            return "-";
        }
        try
        {
            using (System.Security.Cryptography.SHA1 sha = System.Security.Cryptography.SHA1.Create())
            {
                byte[] h = sha.ComputeHash(data);
                System.Text.StringBuilder sb = new System.Text.StringBuilder(12);
                for (int i = 0; i < 6; i++)
                {
                    sb.Append(h[i].ToString("x2"));
                }
                return sb.ToString();
            }
        }
        catch (System.Exception)
        {
            return "-";
        }
    }

    /// <summary>短包体的十六进制明细，用来逐字节比对（撤回重建时的校验也靠这个）。</summary>
    public static string Hex(byte[] data)
    {
        if (data == null)
        {
            return "-";
        }
        System.Text.StringBuilder sb = new System.Text.StringBuilder(data.Length * 3);
        for (int i = 0; i < data.Length; i++)
        {
            sb.Append(data[i].ToString("x2"));
        }
        return sb.ToString();
    }
}
