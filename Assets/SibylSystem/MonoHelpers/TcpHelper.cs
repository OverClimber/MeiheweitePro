using System;
using System.Collections.Concurrent;
using System.Net.Sockets;
using YGOSharp.Network.Enums;
using UnityEngine;
using System.IO;
using System.Threading;
using System.Text;
using System.Collections.Generic;
using YGOSharp.OCGWrapper.Enums;

public static class TcpHelper
{
    public static TcpClient tcpClient = null;

    static  NetworkStream networkStream = null;

    static bool canjoin = true;

    // ===================== 连接层健壮性（对齐 hex 版 ygopro2 的 TcpHelper）=====================
    // 做的事：把「一条连接」显式建成带**代际**的对象；收发由「每包新建一个线程 + 共享 List」
    //         换成「单发送线程 + 每连接独立队列」；补上三种超时、TCP 保活、队列上限、瞬态错误重试。
    //
    // ⛔⛔ 移植红线 —— 以下 ours 定制必须逐条保住，丢任何一条都是回归：
    //   · QuickTestTrace 的 "stoc"/"ctos" 落点（验收脚本判据）
    //   · inboundCount / lastInboundMs（AIRoom.WatchdogTick 判对手卡死）
    //   · DuelUndo.selfDisconnect 静默跳过、AIRoom.IsAiSessionLive 分支
    //   · addDateJumoLine 保持 public（precy.cs 录像回放注入）
    //   · packagesInRecord / lastRecordName / GameModeManager.ReplayDir（Ocgcore 直接读写）
    //   · Disconnect(bool userInitiated = true) 的签名与「断开 ⇒ 走一次断线收尾」语义
    //   · tcpClient 保持 public static TcpClient（5 个文件在直接 switch/Close 它）
    //
    // ✅ 应用层空闲心跳（TryDuelIdleHeartbeat）已按 hex 移植（2026-09-25 补）。
    //   此前把它删掉的理由（"会往人机局入站流掺包、破坏 DuelTimeline 的同种子判据"）**不成立**：
    //   hex 原版有守卫 `ocgcore.condition != Condition.duel → return`，只在**决斗中**才发，
    //   而本工程 AI 侧（AI.Server / WindBot）根本不消费 CtosMessage.TimeConfirm ⇒ 不会掺包。
    //   与 TCP KeepAlive（ConfigureSocket 里开）互补：那个防 NAT 掐空闲，这个防服务端按"客户端无响应"判掉线。

    const int SioKeepAliveVals = -1744830460;

    static readonly object stateLock = new object();
    static ConnectionState state = null;

    /// <summary>离线核心 / 录像回放注入的入站包，走独立一条队列（不挂在任何连接上）。</summary>
    static readonly ConcurrentQueue<byte[]> injectedIncoming = new ConcurrentQueue<byte[]>();
    static long injectedIncomingBytes = 0;
    static int injectedIncomingPackets = 0;

    /// <summary>join 重入闸：一条连接正在建立时，后来的 join 直接丢弃（CAS 抢 0→1）。</summary>
    static int joinInProgress = 0;
    static int generationCounter = 0;
    /// <summary>待通知主线程的断线来自哪一代；0 = 没有待处理断线。</summary>
    static int disconnectGeneration = 0;

    static Thread senderThread = null;

    public static int ConnectTimeoutMs = 10000;
    public static int KeepAliveTimeMs = 20000;
    public static int KeepAliveIntervalMs = 5000;
    /// <summary>决斗中空闲多久补一条 TimeConfirm（对齐 hex）。见 TryDuelIdleHeartbeat。</summary>
    public static int DuelIdleHeartbeatMs = 15000;
    public static int SendRetryDelayMs = 50;
    public static int MaxTransientSendRetry = 3;

    public static int SendTimeoutMs = 9999 * 1000;
    public static int ReceiveTimeoutMs = 9999 * 1000;
    public static bool TcpNoDelay = true;
    public static bool TcpKeepAlive = true;

    public static int OutgoingQueueLimitBytes = 4 * 1024 * 1024;
    public static int OutgoingQueueLimitPackets = 4096;
    public static int IncomingQueueLimitBytes = 8 * 1024 * 1024;
    public static int IncomingQueueLimitPackets = 8192;

    /// <summary>连的是「房间列表」这种一次性用途（密码 "L"）时，掉线提示不该弹。</summary>
    static bool roomListChecking = false;

    /// <summary>
    /// 一条活连接。带 <see cref="Generation"/> 是为了让**旧连接的迟到事件失效**：
    /// 撤回重开（关旧连、开新连）时旧 receiver 线程可能还会报一次断线，
    /// 比代际就能把它丢掉 —— 顺带减少对 DuelUndo.selfDisconnect 那种补丁的依赖。
    /// </summary>
    sealed class ConnectionState : IDisposable
    {
        public readonly int Generation;
        public readonly TcpClient Client;
        public readonly NetworkStream Stream;
        public readonly Socket Socket;
        public readonly ConcurrentQueue<byte[]> Incoming = new ConcurrentQueue<byte[]>();
        public readonly ConcurrentQueue<byte[]> Outgoing = new ConcurrentQueue<byte[]>();
        public readonly AutoResetEvent OutgoingSignal = new AutoResetEvent(false);
        public readonly CancellationTokenSource Cts = new CancellationTokenSource();

        public volatile bool Closing = false;
        public int DisconnectRequested = 0;

        public long IncomingBytes = 0;
        public int IncomingPackets = 0;
        public long OutgoingBytes = 0;
        public int OutgoingPackets = 0;

        // 三个 tick 共同决定「这条连接是不是空闲到该补心跳」。
        // 收/发任一方向有流量都算「活着」，避免对手本回合不出牌时误发心跳。
        public int LastReceiveTick = 0;
        public int LastSendTick = 0;
        public int LastHeartbeatTick = 0;

        public ConnectionState(int generation, TcpClient client)
        {
            Generation = generation;
            Client = client;
            Stream = client.GetStream();
            Socket = client.Client;

            int now = Environment.TickCount;
            LastReceiveTick = now;
            LastSendTick = now;
            LastHeartbeatTick = now;
        }

        public void Dispose()
        {
            try { OutgoingSignal.Dispose(); } catch { }
            try { Cts.Dispose(); } catch { }
        }
    }

    static int TickNow()
    {
        return Environment.TickCount;
    }

    static bool IsElapsed(int fromTick, int durationMs)
    {
        return unchecked(TickNow() - fromTick) >= durationMs;
    }

    /// <summary>
    /// 决斗中的空闲心跳（对齐 hex TcpHelper.TryDuelIdleHeartbeat）。
    /// 收/发任一方向空闲超过 DuelIdleHeartbeatMs 就补一条 TimeConfirm，让服务端知道客户端还活着
    /// —— 长局（天梯十几分钟）里某一方长时间不出牌时，没有它容易被按"无响应"判掉线。
    /// <para>⛔ 守卫 `condition == Condition.duel` 必须保留：非决斗态（组卡/房内等待/人机收尾）
    /// 发这个是协议噪音，且会让"人机局入站流 = 种子决定的确定流"的判据产生错觉。</para>
    /// </summary>
    static void TryDuelIdleHeartbeat(ConnectionState localState)
    {
        if (localState == null || localState.Closing)
            return;

        Program program = Program.I();
        if (program == null || program.ocgcore == null)
            return;

        if (program.ocgcore.condition != Ocgcore.Condition.duel)
            return;

        int lastReceive = Volatile.Read(ref localState.LastReceiveTick);
        int lastSend = Volatile.Read(ref localState.LastSendTick);
        // 收发任一方向有动作都算"活着"：timeout 用 int 差再比大小，避免直接比 TickCount 溢出。
        int lastActivity = unchecked(lastSend - lastReceive) > 0 ? lastSend : lastReceive;

        if (!IsElapsed(lastActivity, DuelIdleHeartbeatMs))
            return;

        int lastHeartbeat = Volatile.Read(ref localState.LastHeartbeatTick);
        if (!IsElapsed(lastHeartbeat, DuelIdleHeartbeatMs))
            return;

        Volatile.Write(ref localState.LastHeartbeatTick, TickNow());
        QuickTestTrace.Log("net", "idle heartbeat gen=" + localState.Generation + " → TimeConfirm");
        CtosMessage_TimeConfirm();
    }

    static bool IsTransientSocketError(SocketError socketError)
    {
        return socketError == SocketError.WouldBlock
            || socketError == SocketError.IOPending
            || socketError == SocketError.NoBufferSpaceAvailable
            || socketError == SocketError.TimedOut
            || socketError == SocketError.Interrupted
            || socketError == SocketError.InProgress
            || socketError == SocketError.TryAgain;
    }


    public static void join(string ipString, string name, string portString, string pswString, string version)
    {
        // 重入闸（对齐 hex 的 joinInProgress CAS）：同一时刻只允许一条连接在建立。
        if (Interlocked.CompareExchange(ref joinInProgress, 1, 0) != 0)
        {
            Program.DEBUGLOG("onDisConnected 1");
            return;
        }

        TcpClient client = null;
        ConnectionState newState = null;

        try
        {
            // ⛔ 保留 ours 原语义：已经有一条活连接时静默忽略本次 join。
            //    hex 是无条件「断旧连再新建」，那会在「人已在房里又点到进服」时把对局踢掉。
            if (tcpClient != null && tcpClient.Connected)
            {
                Program.DEBUGLOG("onDisConnected 1");
                return;
            }

            onDisConnected = false;
            roomListChecking = pswString == "L";

            CloseActiveConnection();

            int port = int.Parse(portString);
            client = new TcpClientWithTimeout(ipString, port, ConnectTimeoutMs).Connect();

            ConfigureSocket(client);

            int generation = Interlocked.Increment(ref generationCounter);
            newState = new ConnectionState(generation, client);
            try { newState.Stream.ReadTimeout = ReceiveTimeoutMs; } catch { }
            try { newState.Stream.WriteTimeout = SendTimeoutMs; } catch { }

            lock (stateLock)
            {
                state = newState;
                tcpClient = client;
                networkStream = newState.Stream;
            }

            Thread receiverThread = new Thread(ReceiverLoop);
            receiverThread.IsBackground = true;
            receiverThread.Start(newState);

            senderThread = new Thread(SenderLoop);
            senderThread.IsBackground = true;
            senderThread.Start(newState);

            QuickTestTrace.Log("ctos", "join " + ipString + ":" + port + " gen=" + generation
                + " psw=" + (string.IsNullOrEmpty(pswString) ? "(空)" : "(有)")
                + " matching=" + (Program.I().mycard != null && Program.I().mycard.isMatching));

            CtosMessage_ExternalAddress(ipString);
            CtosMessage_PlayerInfo(name);
            CtosMessage_JoinGame(pswString, version);
        }
        catch (Exception e)
        {
            onDisConnected = true;
            Program.DEBUGLOG("onDisConnected 10: " + e.Message);
            try { if (client != null) client.Close(); } catch { }
            try { if (newState != null) newState.Dispose(); } catch { }
            CloseActiveConnection();
        }
        finally
        {
            Interlocked.Exchange(ref joinInProgress, 0);
        }
    }

    /// <summary>按 TcpHelper 的静态开关配置套接字：NoDelay + TCP 保活 + 收发超时。</summary>
    static void ConfigureSocket(TcpClient client)
    {
        if (client == null)
            return;

        try { client.NoDelay = TcpNoDelay; } catch { }
        try { client.Client.NoDelay = TcpNoDelay; } catch { }

        try
        {
            client.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, TcpKeepAlive);
        }
        catch { }

        try
        {
            // Windows 下用 IOControl 才调得到保活间隔（默认 2 小时等于没开）：
            // 20s 空闲开始探测、每 5s 一次 —— 防公网 NAT 把长时间思考的静默连接掐掉。
            byte[] keepAlive = new byte[12];
            BitConverter.GetBytes((uint)1).CopyTo(keepAlive, 0);
            BitConverter.GetBytes((uint)KeepAliveTimeMs).CopyTo(keepAlive, 4);
            BitConverter.GetBytes((uint)KeepAliveIntervalMs).CopyTo(keepAlive, 8);
            client.Client.IOControl((IOControlCode)SioKeepAliveVals, keepAlive, null);
        }
        catch { }

        try
        {
            client.Client.SendTimeout = SendTimeoutMs;
            client.Client.ReceiveTimeout = ReceiveTimeoutMs;
        }
        catch { }
    }

    /// <summary>
    /// 收包线程（每条连接一个，绑定到它自己的 ConnectionState）。
    /// ⛔ 不再在这里直接置 onDisConnected —— 统一走 RequestDisconnect 并带上代际，
    ///    旧连接迟到的断线事件由主线程按代际丢掉。
    /// </summary>
    static void ReceiverLoop(object obj)
    {
        var localState = (ConnectionState)obj;
        try
        {
            var token = localState.Cts.Token;
            while (!token.IsCancellationRequested && Program.Running)
            {
                byte[] data = SocketMaster.ReadPacket(localState.Stream, token);
                if (data == null)
                {
                    RequestDisconnect(localState, "onDisConnected 2");
                    break;
                }

                Volatile.Write(ref localState.LastReceiveTick, TickNow());

                if (!TryEnqueueIncoming(localState, data))
                {
                    break;
                }
            }
        }
        catch (Exception e)
        {
            RequestDisconnect(localState, "onDisConnected 3: " + e.Message);
        }
    }

    static bool TryEnqueueIncoming(ConnectionState localState, byte[] data)
    {
        if (data == null)
            return false;

        long bytes = Interlocked.Add(ref localState.IncomingBytes, data.Length);
        int packets = Interlocked.Increment(ref localState.IncomingPackets);

        if (bytes > IncomingQueueLimitBytes || packets > IncomingQueueLimitPackets)
        {
            RequestDisconnect(localState, "onDisConnected incoming overflow");
            return false;
        }

        localState.Incoming.Enqueue(data);
        return true;
    }

    /// <summary>
    /// 把一个入站包注入主线程派发队列（离线核心 / 录像回放用）。
    /// ⛔ 必须保持 public static、签名不变：precy.cs 在调它。
    ///    传 null 直接丢（原实现会把 null 塞进列表，等消费端炸）。
    /// </summary>
    public static void addDateJumoLine(byte[] data)
    {
        if (data == null)
            return;

        long bytes = Interlocked.Add(ref injectedIncomingBytes, data.Length);
        int packets = Interlocked.Increment(ref injectedIncomingPackets);
        if (bytes > IncomingQueueLimitBytes || packets > IncomingQueueLimitPackets)
        {
            Interlocked.Add(ref injectedIncomingBytes, -data.Length);
            Interlocked.Decrement(ref injectedIncomingPackets);
            return;
        }

        injectedIncoming.Enqueue(data);
    }

    public static bool onDisConnected = false;

    /// <summary>收到的入站包总数（含每一条 GameMsg）。看门狗用「它还动不动」判对手是不是卡死了。</summary>
    public static int inboundCount = 0;

    /// <summary>最后一个入站包到达的时刻（Program.TimePassed 毫秒）。</summary>
    public static int lastInboundMs = 0;

    /// <summary>
    /// 主动断开当前连接。
    /// ⛔ 对外语义保持不变（ours 原版）：无论 userInitiated 取值，断开之后都要走一次断线收尾
    ///    —— MyCard.onClickExit / AIRoom 都依赖 <see cref="preFrameFunction"/> 里那个收尾分支。
    ///    实现换成「直接关掉这条连接（取消收发线程 + 清 state）」再置位 onDisConnected，
    ///    不再依赖 receiver 线程事后发现断开（那条路在代际模型里会被判成旧连接事件）。
    /// </summary>
    public static void Disconnect(bool userInitiated = true)
    {
        bool hadConnection = tcpClient != null;
        try
        {
            CloseActiveConnection();
        }
        catch (System.Exception e)
        {
            Program.DEBUGLOG("Disconnect error: " + e.Message);
        }

        if (hadConnection || userInitiated == false)
        {
            onDisConnected = true;
        }
    }

    /// <summary>
    /// 请求断开一条具体连接（收发线程内部调用）。带代际：只有「当前这条」才通知主线程，
    /// 旧连接迟到的事件直接丢弃。debugLog 沿用 ours 的 "onDisConnected N" 文案，便于日志比对。
    /// </summary>
    static void RequestDisconnect(ConnectionState localState, string debugLog)
    {
        if (localState == null)
            return;

        if (Interlocked.Exchange(ref localState.DisconnectRequested, 1) != 0)
            return;

        localState.Closing = true;
        try { localState.Cts.Cancel(); } catch { }
        try { localState.OutgoingSignal.Set(); } catch { }

        bool shouldNotifyMainThread;
        lock (stateLock)
        {
            shouldNotifyMainThread = ReferenceEquals(state, localState);
        }

        if (shouldNotifyMainThread)
        {
            Interlocked.Exchange(ref disconnectGeneration, localState.Generation);
            onDisConnected = true;
            if (!string.IsNullOrEmpty(debugLog))
                Program.DEBUGLOG(debugLog);
        }
    }

    /// <summary>关掉当前连接：取消收发线程、Shutdown/Close 套接字、清掉 state 与 tcpClient。</summary>
    static void CloseActiveConnection()
    {
        ConnectionState oldState;
        lock (stateLock)
        {
            oldState = state;
            state = null;
        }

        if (oldState != null)
        {
            oldState.Closing = true;
            try { oldState.Cts.Cancel(); } catch { }
            try { oldState.OutgoingSignal.Set(); } catch { }

            try
            {
                if (oldState.Client != null)
                {
                    try
                    {
                        if (oldState.Client.Connected)
                        {
                            oldState.Socket.Shutdown(SocketShutdown.Both);
                        }
                    }
                    catch { }

                    try { oldState.Client.Close(); } catch { }
                }
            }
            catch { }

            try { oldState.Stream.Close(); } catch { }

            oldState.Dispose();
        }

        tcpClient = null;
        networkStream = null;
    }

    /// <summary>
    /// 主线程独占的派发缓冲：每帧先把两条来源的包搬进来，再统一走一遍 switch。
    /// （原来它由 receiver 线程直接写、需要 Monitor 保护；现在收包线程只写自己连接的队列，
    ///   这个 List 只有主线程碰，Monitor 保留只是为了不动下面那段派发代码。）
    /// </summary>
    static List<byte[]> datas = new List<byte[]>();

    public static void preFrameFunction()
    {
        // ① 注入包（离线核心 / 录像回放，来自 precy.cs 的 addDateJumoLine）—— 与连接无关。
        while (injectedIncoming.TryDequeue(out var injectedPacket))
        {
            Interlocked.Add(ref injectedIncomingBytes, -injectedPacket.Length);
            Interlocked.Decrement(ref injectedIncomingPackets);
            datas.Add(injectedPacket);
        }

        // ② 当前连接的入站包。按 state 取而不是读某个全局流 —— 旧连接的残留包不会再被算进来，
        //    这正是撤回重开（关旧连、开新连）时最容易串味的地方。
        ConnectionState localState;
        lock (stateLock)
        {
            localState = state;
        }

        // 决斗空闲心跳（对齐 hex：取到当前连接状态后立刻判）—— 只在 condition==duel 时才会真发。
        TryDuelIdleHeartbeat(localState);

        if (localState != null)
        {
            while (localState.Incoming.TryDequeue(out var incomingPacket))
            {
                Interlocked.Add(ref localState.IncomingBytes, -incomingPacket.Length);
                Interlocked.Decrement(ref localState.IncomingPackets);
                datas.Add(incomingPacket);
            }
        }

        if (datas.Count>0)
        {
            if (Monitor.TryEnter(datas))
            {
                for (int i = 0; i < datas.Count; i++)
                {
                    // 看门狗的心跳：只要还有包进来，就说明对手活着（见 AIRoom.WatchdogTick）。
                    inboundCount++;
                    lastInboundMs = Program.TimePassed();
                    try
                    {
                        MemoryStream memoryStream = new MemoryStream(datas[i]);
                        BinaryReader r = new BinaryReader(memoryStream);
                        var ms = (StocMessage)(r.ReadByte());
                        // 带上包体摘要：用于验证「同一随机种子 → 同一条对局消息流」，
                        // 这是撤回功能「重建到过去某一步」的前提（见 duel-undo 设计）。
                        // 阈值放到 80：几个关键的「问玩家要什么」的包（SelectIdleCmd 等）正好落在
                        // 40~50 字节，卡在 40 上就看不到内容，排查时等于瞎。80 以内都还算短的。
                        QuickTestTrace.Log("stoc", "recv " + ms + " len=" + datas[i].Length
                            + " sha=" + QuickTestTrace.Hash(datas[i])
                            + (datas[i].Length <= 80 ? " hex=" + QuickTestTrace.Hex(datas[i]) : ""));
                        switch (ms)
                        {
                            case StocMessage.GameMsg:
                                Program.I().room.StocMessage_GameMsg(r);
                                break;
                            case StocMessage.ErrorMsg:
                                Program.I().room.StocMessage_ErrorMsg(r);
                                break;
                            case StocMessage.SelectHand:
                                Program.I().room.StocMessage_SelectHand(r);
                                break;
                            case StocMessage.SelectTp:
                                Program.I().room.StocMessage_SelectTp(r);
                                break;
                            case StocMessage.HandResult:
                                Program.I().room.StocMessage_HandResult(r);
                                break;
                            case StocMessage.TpResult:
                                Program.I().room.StocMessage_TpResult(r);
                                break;
                            case StocMessage.ChangeSide:
                                Program.I().room.StocMessage_ChangeSide(r);
                                TcpHelper.SaveRecord();
                                break;
                            case StocMessage.WaitingSide:
                                Program.I().room.StocMessage_WaitingSide(r);
                                TcpHelper.SaveRecord();
                                break;
                            case StocMessage.DeckCount:
                                Program.I().room.StocMessage_DeckCount(r);
                                break;
                            case StocMessage.CreateGame:
                                Program.I().room.StocMessage_CreateGame(r);
                                break;
                            case StocMessage.JoinGame:
                                Program.I().room.StocMessage_JoinGame(r);
                                break;
                            case StocMessage.TypeChange:
                                Program.I().room.StocMessage_TypeChange(r);
                                break;
                            case StocMessage.LeaveGame:
                                Program.I().room.StocMessage_LeaveGame(r);
                                break;
                            case StocMessage.DuelStart:
                                Program.I().room.StocMessage_DuelStart(r);
                                break;
                            case StocMessage.DuelEnd:
                                Program.I().room.StocMessage_DuelEnd(r);
                                TcpHelper.SaveRecord();
                                break;
                            case StocMessage.Replay:
                                Program.I().room.StocMessage_Replay(r);
                                TcpHelper.SaveRecord();
                                break;
                            case StocMessage.TimeLimit:
                                Program.I().ocgcore.StocMessage_TimeLimit(r);
                                break;
                            case StocMessage.Chat:
                                Program.I().room.StocMessage_Chat(r);
                                break;
                            case StocMessage.HsPlayerEnter:
                                Program.I().room.StocMessage_HsPlayerEnter(r);
                                break;
                            case StocMessage.HsPlayerChange:
                                Program.I().room.StocMessage_HsPlayerChange(r);
                                break;
                            case StocMessage.HsWatchChange:
                                Program.I().room.StocMessage_HsWatchChange(r);
                                break;
                            case StocMessage.TeammateSurrender:
                                Program.I().room.StocMessage_TeammateSurrender(r);
                                break;
                            default:
                                break;
                        }
                    }
                    catch (System.Exception e)
                    {
                       // Program.DEBUGLOG(e);
                    }
                }
                datas.Clear();
                Monitor.Exit(datas);
            }
        }
        if (onDisConnected == true)
        {
            onDisConnected = false;

            // 代际闸（对齐 hex）：只认「当前这条连接」的断线。
            // 撤回重开是「关旧连 + 开新连」，旧 receiver 线程可能还会报一次断线 ——
            // 那种迟到事件如果照样跑下面的收尾，就会在玩家已经进了新局之后
            // 又弹一次提示 / 再切一次界面。比对代际直接丢掉。
            int gen = Interlocked.Exchange(ref disconnectGeneration, 0);
            bool closeNow;
            lock (stateLock)
            {
                closeNow = gen != 0 && state != null && state.Generation == gen;
            }
            if (gen != 0 && !closeNow)
            {
                QuickTestTrace.Log("net", "stale disconnect ignored gen=" + gen);
                return;
            }

            if (closeNow)
            {
                CloseActiveConnection();
            }
            else if (TcpHelper.tcpClient != null)
            {
                if (TcpHelper.tcpClient.Connected)
                {
                    tcpClient.Client.Shutdown(0);
                    tcpClient.Close();
                }
                tcpClient = null;
            }
            else
            {
                tcpClient = null;
            }

            // 🔑 撤回重开主动断的旧连接：静默跳过。
            //   这一段断开的语义是「后台重建的一部分」—— 既不是对手进程没了，也不是连接故障，
            //   所以弹提示、清录像缓冲、切界面全是误伤。
            //   ⛔ 不跳过的后果（2026-09-20 实测）：撤回关旧连接后这一个标记要到下一帧才被消费，
            //   而那时 launch() 已经跑完、IsAiSessionLive 已被重新置 true、新局还没开始
            //   ⇒ 下面 IsAiSessionLive 那条把玩家切回战斗前界面，撤回遮罩却还盖着
            //   ⇒ 玩家看到「一按撤回就退回人机/卡组界面，然后卡住」。
            if (DuelUndo.selfDisconnect)
            {
                DuelUndo.selfDisconnect = false;
                QuickTestTrace.Log("undo", "self-disconnect swallowed（撤回重开的旧连接）");
                return;
            }

            // 天梯回环（对齐 hex 版 TcpHelper.preFrameFunction 的断线分支）：
            // 匹配中掉线要把人送回竞技场界面 —— 否则当前是「被丢回服务器列表，而竞技场还停在匹配中」。
            // ⛔ 插在 DuelUndo.selfDisconnect 静默跳过之后：撤回重开主动断的旧连接不算掉线。
            // ⛔ 非匹配态此调用不改任何去向（人机局 AIRoom 分支因此不受影响）。
            Program.I().ocgcore.setDefaultReturnServant();

            if (Program.I().ocgcore.isShowed == false)
            {
                if (Program.I().menu.isShowed == false) 
                {
                    if (Program.I().ocgcore.returnServant != null)
                        Program.I().shiftToServant(Program.I().ocgcore.returnServant);
                    else
                        Program.I().shiftToServant(Program.I().selectServer);
                }
                Program.I().cardDescription.RMSshow_none(InterString.Get("连接被断开。"));
                packagesInRecord.Clear();
            }
            else
            {
                // AI 局的「对手」是本机进程（AI.Server + WindBot）：它没了就是真没了，
                // 没有「您截个图留念」的语义。这一条是「断开连接不把我踢出来」的正面修复。
                if (AIRoom.IsAiSessionLive)
                {
                    // ⛔ 必须分「局结束了没有」两种，2026-09-19 实测踩到：
                    //    RD 侧 WindBot 断线时，AI.Server 会**先把这一局判完**（发 GameMsg 胜 +
                    //    Replay + DuelEnd），再退出、socket 才断。也就是说 socket 断的时候
                    //    duelEnded 已经 = true、结算面板已经在玩家面前了。
                    //    · 局没结束就断 = 真故障（对手进程没了 / 卡死），这一局永远不会有结果
                    //      —— 收干净、回界面，别把玩家留在场上；
                    //    · 局已结束 = 正常收尾，**绝不能**把人弹走：结算面板刚出来，
                    //      弹走等于连「保存录像 / 看清胜负」都点不到。
                    //    两种情况的文案都必须用 AI 口径，不能再说「对方离开游戏，您可以截图」
                    //    —— AI 的对手是本机进程，玩家截给谁看？（实测就是这么误导人的。）
                    if (!Program.I().room.duelEnded)
                    {
                        Program.I().cardDescription.RMSshow_none(InterString.Get("电脑对手已停止响应，本局已结束。"));
                        packagesInRecord.Clear();
                        AIRoom.EndAiSessionAndReturn("socket 断开");
                    }
                    else
                    {
                        Program.I().cardDescription.RMSshow_none(InterString.Get("电脑对手已停止响应，本局已结束。"));
                        packagesInRecord.Clear();
                    }
                }
                else
                {
                    Program.I().cardDescription.RMSshow_none(InterString.Get("对方离开游戏，您现在可以截图。"));
                    packagesInRecord.Clear();
                    Program.I().ocgcore.forceMSquit();
                }
            }

        }
    }

    /// <summary>
    /// 出站包入队（对齐 hex 的单发送线程模型）。
    /// 原来每发一个包就 new 一个 Thread —— 长局里每回合几十条 Response/TimeConfirm，
    /// 线程创建本身就成了开销；改成一个常驻 sender 线程从队列取。
    /// ⛔ 发送顺序由 FIFO 队列保证，与原来 lock 串行等价。
    /// ⛔ QuickTestTrace 的 "ctos" 落点保留（验收脚本判据），仍在「真正写进 socket 之后」记。
    /// </summary>
    public static void Send(Package message)
    {
        ConnectionState localState;
        lock (stateLock)
        {
            localState = state;
        }

        if (localState == null || localState.Closing)
            return;

        try
        {
            byte[] frame = BuildFrame(message);
            if (frame == null)
                return;

            long bytes = Interlocked.Add(ref localState.OutgoingBytes, frame.Length);
            int packets = Interlocked.Increment(ref localState.OutgoingPackets);
            if (bytes > OutgoingQueueLimitBytes || packets > OutgoingQueueLimitPackets)
            {
                Interlocked.Add(ref localState.OutgoingBytes, -frame.Length);
                Interlocked.Decrement(ref localState.OutgoingPackets);
                RequestDisconnect(localState, "onDisConnected outgoing overflow");
                return;
            }

            localState.Outgoing.Enqueue(frame);
            localState.OutgoingSignal.Set();
        }
        catch (Exception e)
        {
            RequestDisconnect(localState, "onDisConnected 5: " + e.Message);
        }
    }

    /// <summary>拼线上帧：2 字节长度 + 1 字节功能码 + 包体；长度字段 = 包体长度 + 1（含功能码）。</summary>
    static byte[] BuildFrame(Package message)
    {
        if (message == null || message.Data == null)
            return null;

        byte[] data = message.Data.get();

        byte[] s = new byte[2 + 1 + data.Length];
        ushort len = (ushort)(data.Length + 1);
        s[0] = (byte)(len & 0xFF);
        s[1] = (byte)((len >> 8) & 0xFF);
        s[2] = (byte)message.Fuction;
        Buffer.BlockCopy(data, 0, s, 3, data.Length);
        return s;
    }

    /// <summary>常驻发送线程：从队列取帧写进 socket；瞬态错误退避重试，失败即请求断开。</summary>
    static void SenderLoop(object obj)
    {
        var localState = (ConnectionState)obj;
        var token = localState.Cts.Token;

        try
        {
            while (!token.IsCancellationRequested && Program.Running)
            {
                if (!localState.Outgoing.TryDequeue(out var frame))
                {
                    localState.OutgoingSignal.WaitOne(100);
                    continue;
                }

                Interlocked.Add(ref localState.OutgoingBytes, -frame.Length);
                Interlocked.Decrement(ref localState.OutgoingPackets);

                try
                {
                    SendAll(localState.Socket, frame, token);
                    Volatile.Write(ref localState.LastSendTick, TickNow());
                    // 功能码在帧的 [2] 上（前两字节是长度）。
                    QuickTestTrace.Log("ctos", "sent " + ((CtosMessage)frame[2]) + " (" + frame.Length + "B)");
                }
                catch (Exception e)
                {
                    QuickTestTrace.Log("ctos", "send FAILED: " + e.Message);
                    RequestDisconnect(localState, "onDisConnected 5: " + e.Message);
                    break;
                }
            }
        }
        catch (Exception e)
        {
            RequestDisconnect(localState, "onDisConnected sender loop: " + e.Message);
        }
    }

    /// <summary>把一帧完整写进 socket；瞬态错误（WouldBlock/TimedOut 等）退避重试。</summary>
    static void SendAll(Socket socket, byte[] buffer, CancellationToken token)
    {
        if (socket == null || buffer == null)
            return;

        int offset = 0;
        int retry = 0;
        while (offset < buffer.Length)
        {
            if (token.IsCancellationRequested)
                return;

            try
            {
                int sent = socket.Send(buffer, offset, buffer.Length - offset, SocketFlags.None);
                if (sent <= 0)
                    throw new IOException("socket send returned 0");

                offset += sent;
                retry = 0;
            }
            catch (SocketException socketException)
            {
                if (IsTransientSocketError(socketException.SocketErrorCode) && retry < MaxTransientSendRetry)
                {
                    retry++;
                    Thread.Sleep(SendRetryDelayMs * retry);
                    continue;
                }

                throw;
            }
        }
    }

    public static void CtosMessage_Response(byte[] response)
    {

        Package message = new Package();
        message.Fuction = (int)CtosMessage.Response;
        message.Data.writer.Write(response);
        Send(message);
    }

    public static YGOSharp.Deck deck;
    public static void CtosMessage_UpdateDeck(YGOSharp.Deck deckFor)
    {
        if (deckFor.Main.Count == 0)
        {
            QuickTestTrace.Log("ctos", "UpdateDeck SKIPPED: main deck empty -> server never learns our deck");
            return;
        }
        deckStrings.Clear();
        deck = deckFor;
        // 人机对局要把「开局发出去的这份卡组」留一份：撤回重开必须原样再发一份，
        // 否则重开后摸到的牌与录下来的入站流对不上，第一条就分叉（见 DuelTimeline.DeckCodes）。
        DuelTimeline.NoteDeckSent(deckFor);
        Package message = new Package();
        message.Fuction = (int)CtosMessage.UpdateDeck;
        message.Data.writer.Write(deckFor.Main.Count + deckFor.Extra.Count);
        message.Data.writer.Write(deckFor.Side.Count);
        for (int i = 0; i < deckFor.Main.Count; i++)
        {
            message.Data.writer.Write(deckFor.Main[i]);
            var c = YGOSharp.CardsManager.Get(deckFor.Main[i]);
            deckStrings.Add(c.Name);
        }
        for (int i = 0; i < deckFor.Extra.Count; i++)
        {
            message.Data.writer.Write(deckFor.Extra[i]);
        }
        for (int i = 0; i < deckFor.Side.Count; i++)
        {
            message.Data.writer.Write(deckFor.Side[i]);
        }
        Send(message);
    }

    public static void CtosMessage_HandResult(int res)
    {
        // 记下这一手：撤回重开时客户端要照原样再答一次（见 DuelTimeline.handAnswers）。
        DuelTimeline.NoteHandAnswer(res);
        Package message = new Package();
        message.Fuction = (int)CtosMessage.HandResult;
        message.Data.writer.Write((byte)res);
        Send(message);
    }

    public static void CtosMessage_TpResult(bool tp)
    {
        // 同上：先后攻是位置型选择，重开必须照答。
        DuelTimeline.NoteTpAnswer(tp);
        Package message = new Package();
        message.Fuction = (int)CtosMessage.TpResult;
        if (tp)
        {
            message.Data.writer.Write((byte)1);
        }
        else
        {
            message.Data.writer.Write((byte)0);
        }
        Send(message);
    }

    public static void CtosMessage_ExternalAddress(string hostname)
    {
        Package message = new Package();
        message.Fuction = (int)CtosMessage.ExternalAddress;
        message.Data.writer.Write((UInt32)0);
        message.Data.writer.WriteUnicode(hostname, hostname.Length + 1);
        Send(message);
    }

    public static void CtosMessage_PlayerInfo(string name)
    {
        Package message = new Package();
        message.Fuction = (int)CtosMessage.PlayerInfo;
        message.Data.writer.WriteUnicode(name, 20);
        Send(message);
    }

    public static void CtosMessage_CreateGame()
    {
    }

    public static List<string> deckStrings = new List<string>();
    public static void CtosMessage_JoinGame(string psw,string version)
    {
        deckStrings.Clear();
        Package message = new Package();
        message.Fuction = (int)CtosMessage.JoinGame;
        //Config.ClientVersion = (uint)GameStringManager.helper_stringToInt(version);
        message.Data.writer.Write((Int16)Config.ClientVersion);
        message.Data.writer.Write((byte)204);
        message.Data.writer.Write((byte)204);
        message.Data.writer.Write(0);
        message.Data.writer.WriteUnicode(psw, 20);
        Send(message);
    }

    public static void CtosMessage_LeaveGame()
    {
        Package message = new Package();
        message.Fuction = (int)CtosMessage.LeaveGame;
        Send(message);
    }

    public static void CtosMessage_Surrender()
    {
        Package message = new Package();
        message.Fuction = (int)CtosMessage.Surrender;
        Send(message);
    }

    public static void CtosMessage_TimeConfirm()
    {
        Package message = new Package();
        message.Fuction = (int)CtosMessage.TimeConfirm;
        Send(message);
    }

    public static void CtosMessage_Chat(string str)
    {
        Package message = new Package();
        message.Fuction = (int)CtosMessage.Chat;
        message.Data.writer.WriteUnicode(str, str.Length + 1);
        Send(message);
    }

    public static void CtosMessage_HsToDuelist()
    {
        Package message = new Package();
        message.Fuction = (int)CtosMessage.HsToDuelist;
        Send(message);
    }

    public static void CtosMessage_HsToObserver()
    {
        Package message = new Package();
        message.Fuction = (int)CtosMessage.HsToObserver;
        Send(message);
    }

    public static void CtosMessage_HsReady()
    {
        Package message = new Package();
        message.Fuction = (int)CtosMessage.HsReady;
        Send(message);
    }

    public static void CtosMessage_HsNotReady()
    {
        Package message = new Package();
        message.Fuction = (int)CtosMessage.HsNotReady;
        Send(message);
    }

    public static void CtosMessage_HsKick(int pos)
    {
        Package message = new Package();
        message.Fuction = (int)CtosMessage.HsKick;
        message.Data.writer.Write((byte)pos);
        Send(message);
    }

    public static void CtosMessage_HsStart()
    {
        Package message = new Package();
        message.Fuction = (int)CtosMessage.HsStart;
        Send(message);
    }

    public static List<Package> packagesInRecord = new List<Package>();

    public static List<Package> readPackagesInRecord(string path)
    {
        List<Package> re = null;
        try
        {
            re = getPackages(File.ReadAllBytes(path));
        }
        catch (System.Exception e)
        {
            re = new List<Package>();
            UnityEngine.Debug.Log(e);
        }
        return re;
    }

    public static List<Package> getPackages(byte[] buffer)
    {
        List<Package> re = new List<Package>();
        try
        {
            BinaryReader reader;
            using (reader = new BinaryReader(new MemoryStream(buffer)))
            {
                while (reader.BaseStream.Position < reader.BaseStream.Length)
                {
                    Package p = new Package();
                    p.Fuction = reader.ReadByte();
                    p.Data = new BinaryMaster(reader.ReadBytes((int)(reader.ReadUInt32())));
                    re.Add(p);
                }
            }
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.Log(e);
        }
        return re;
    }

    public static string lastRecordName = "";   

    /// <summary>
    /// 玩家在结算面板上已经做过选择（保存 / 放弃）之后，把这一局攒下的包丢掉。
    ///
    /// 为什么必须丢：<see cref="SaveRecord"/> 不是「追加写」，而是「删掉上一份、按**当前时间**
    /// 另写一份」，而且包列表从头到尾**没有**被清空过（SaveRecord 末尾那行 Clear 是注释掉的）。
    /// 于是玩家点完「放弃录像」之后，收尾路径上的 onExit() → returnTo() 还会再调一次
    /// SaveRecord，凭空又写出一份新的时间戳录像 —— 玩家看到的就是「我明明点了否，
    /// replay 里还是多了一份」。点「是」那条路同理：收尾会再写一份，同一局留下两个文件。
    ///
    /// 只在这两个出口调用，其他时机（中途退出、掉线补存）保持原样。
    /// </summary>
    public static void ClearRecordBuffer()
    {
        packagesInRecord.Clear();
        lastRecordName = "";
    }

    public static void SaveRecord()
    {
        try
        {
            if (packagesInRecord.Count > 10)
            {
                bool write = false;
                int i = 0;
                int startI = 0;
                foreach (var item in packagesInRecord)
                {
                    i++;
                    try
                    {
                        if (item.Fuction == (int)GameMessage.Start)
                        {
                            write = true;
                            startI = i;
                        }
                        if (item.Fuction == (int)GameMessage.ReloadField)
                        {
                            write = true;
                            startI = i;
                        }
                    }
                    catch (System.Exception e)
                    {
                        UnityEngine.Debug.Log(e);
                    }
                }
                if (write)
                {
                    if (startI > packagesInRecord.Count)
                    {
                        startI = packagesInRecord.Count;
                    }
                    packagesInRecord.Insert(startI, Program.I().ocgcore.getNamePacket());
                    // 录像落盘按模式分目录（RD = rd/replay/，OCG = replay/，见 GameModeManager.ReplayDir）。
                    // rd/replay 不在构建铺设名单里，RD 录下第一局之前还不存在 ⇒ 这里惰性建一次。
                    string dir = GameModeManager.ReplayDir;
                    Directory.CreateDirectory(dir);
                    if (File.Exists(dir + "/" + lastRecordName + ".yrp3d"))
                    {
                        File.Delete(dir + "/" + lastRecordName + ".yrp3d");
                    }
                    lastRecordName = UIHelper.getTimeString();
                    FileStream stream = File.Create(dir + "/" + lastRecordName + ".yrp3d");
                    BinaryWriter writer = new BinaryWriter(stream);
                    foreach (var item in packagesInRecord)
                    {
                        writer.Write((byte)item.Fuction);
                        writer.Write((UInt32)item.Data.getLength());
                        writer.Write(item.Data.get());
                    }
                    stream.Flush();
                    writer.Close();
                    stream.Close();
                }
            }
            //packagesInRecord.Clear();
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.Log(e);
        }
    }

    public static void AddRecordLine(Package p)
    {
        if (Program.I().ocgcore.condition != Ocgcore.Condition.record)
        {
            packagesInRecord.Add(p);
        }
    }
}

public class Package
{
    public int Fuction = 0;
    public BinaryMaster Data = null;
    public Package()
    {
        Fuction = (int)CtosMessage.Response;
        Data = new BinaryMaster();
    }
}

public class BinaryMaster
{
    MemoryStream memstream = null;
    public BinaryReader reader = null;
    public BinaryWriter writer = null;
    public BinaryMaster(byte[] raw = null)
    {
        if (raw == null)
        {
            memstream = new MemoryStream();
        }
        else
        {
            memstream = new MemoryStream(raw);
        }
        reader = new BinaryReader(memstream);
        writer = new BinaryWriter(memstream);
    }
    public void set(byte[] raw)
    {
        memstream = new MemoryStream(raw);
        reader = new BinaryReader(memstream);
        writer = new BinaryWriter(memstream);
    }
    public byte[] get()
    {
        byte[] bytes = memstream.ToArray();
        return bytes;
    }
    public int getLength()
    {
        return (int)memstream.Length;
    }
    public override string ToString()
    {
        string return_value = "";
        byte[] bytes = get();
        for (int i = 0; i < bytes.Length; i++)
        {
            return_value += ((int)bytes[i]).ToString();
            if (i < bytes.Length - 1) return_value += ",";
        }
        return return_value;
    }

}

public static class BinaryExtensions
{
    public static void WriteUnicode(this BinaryWriter writer, string text, int len)
    {
        try
        {
            byte[] unicode = Encoding.Unicode.GetBytes(text);
            byte[] result = new byte[len * 2];
            for (int i = 0; i < result.Length; i++)
            {
                result[i] = 204;
            }
            int max = len * 2 - 2;
            Array.Copy(unicode, result, unicode.Length > max ? max : unicode.Length);
            result[unicode.Length] = 0;
            result[unicode.Length + 1] = 0;
            writer.Write(result);
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.Log(e);
        }

    }

    public static string ReadUnicode(this BinaryReader reader, int len)
    {
        byte[] unicode = reader.ReadBytes(len * 2);
        string text = Encoding.Unicode.GetString(unicode);
        text = text.Substring(0, text.IndexOf('\0'));
        return text;
    }

    public static string ReadALLUnicode(this BinaryReader reader)
    {
        byte[] unicode = reader.ReadToEnd();
        string text = Encoding.Unicode.GetString(unicode);
        text = text.Substring(0, text.IndexOf('\0'));
        return text;
    }

    public static byte[] ReadToEnd(this BinaryReader reader)
    {
        return reader.ReadBytes((int)(reader.BaseStream.Length - reader.BaseStream.Position));
    }

    public static GPS ReadGPS(this BinaryReader reader)
    {
        GPS a = new GPS();
        a.controller = (UInt32)Program.I().ocgcore.localPlayer(reader.ReadByte());
        a.location = reader.ReadByte();
        a.sequence = reader.ReadByte();
        a.position = reader.ReadByte();
        return a;
    }

    public static GPS ReadShortGPS(this BinaryReader reader)
    {
        GPS a = new GPS();
        a.controller = (UInt32)Program.I().ocgcore.localPlayer(reader.ReadByte());
        a.location = reader.ReadByte();
        a.sequence = reader.ReadByte();
        a.position = (int)CardPosition.FaceUpAttack;
        return a;
    }

    public static void readCardData(this BinaryReader r, gameCard cardTemp=null)     
    {
        gameCard cardToRefresh = cardTemp;
        int flag = r.ReadInt32();
        int code = 0;
        GPS gps = new GPS();

        if ((flag & (int)Query.Code) != 0)
        {
            code= r.ReadInt32();
        }
        if ((flag & (int)Query.Position) != 0)
        {
            gps = r.ReadGPS();
            cardToRefresh = null;
            cardToRefresh = Program.I().ocgcore.GCS_cardGet(gps,false);
        }

        if (cardToRefresh == null)
        {
            return;
        }

        YGOSharp.Card data = cardToRefresh.get_data();

        if ((flag & (int)Query.Code) != 0)
        {
            if (data.Id != code)
            {
                data = YGOSharp.CardsManager.Get(code);
                data.Id = code;
            }
        }
        if ((flag & (int)Query.Position) != 0)
        {
            cardToRefresh.p = gps;
        }


        if (data.Id > 0)
        {
            if ((cardToRefresh.p.location & (UInt32)CardLocation.Hand) > 0)
            {
                if (cardToRefresh.p.controller == 1)
                {
                    cardToRefresh.p.position = (Int32)CardPosition.FaceUpAttack;
                }
            }
        }

        if ((flag & (int)Query.Alias) != 0)
            data.Alias = r.ReadInt32();
        if ((flag & (int)Query.Type) != 0)
            data.Type = r.ReadInt32();

        int l1 = 0;
        if ((flag & (int)Query.Level) != 0)
        {
            l1 = r.ReadInt32();
        }
        int l2 = 0;
        if ((flag & (int)Query.Rank) != 0)
        {
            l2 = r.ReadInt32();
        }

        if ((flag & (int)Query.Attribute) != 0)
            data.Attribute = r.ReadInt32();
        if ((flag & (int)Query.Race) != 0)
            data.Race = r.ReadInt32();
        if ((flag & (int)Query.Attack) != 0)
            data.Attack = r.ReadInt32();
        if ((flag & (int)Query.Defence) != 0)
            data.Defense = r.ReadInt32();
        if ((flag & (int)Query.BaseAttack) != 0)
            r.ReadInt32();
        if ((flag & (int)Query.BaseDefence) != 0)
            r.ReadInt32();
        if ((flag & (int)Query.Reason) != 0)
            r.ReadInt32();
        if ((flag & (int)Query.ReasonCard) != 0)
            r.ReadInt32();
        if ((flag & (int)Query.EquipCard) != 0)
        {
            cardToRefresh.addTarget(Program.I().ocgcore.GCS_cardGet(r.ReadGPS(), false));
        }
        if ((flag & (int)Query.TargetCard) != 0)
        {
            int count = r.ReadInt32();
            for (int i = 0; i < count; ++i)
            {
                cardToRefresh.addTarget(Program.I().ocgcore.GCS_cardGet(r.ReadGPS(), false));
            }
        }
        if ((flag & (int)Query.OverlayCard) != 0)
        {
            var overs = Program.I().ocgcore.GCS_cardGetOverlayElements(cardToRefresh);
            int count = r.ReadInt32();
            for (int i = 0; i < count; ++i)
            {
                if (i < overs.Count)
                {
                    overs[i].set_code(r.ReadInt32());
                }
                else
                {
                    r.ReadInt32();
                }
            }
        }
        if ((flag & (int)Query.Counters) != 0)
        {
            int count = r.ReadInt32();
            for (int i = 0; i < count; ++i)
                r.ReadInt32();
        }
        if ((flag & (int)Query.Owner) != 0)
            r.ReadInt32();
        if ((flag & (int)Query.Status) != 0)
        {
            int status = r.ReadInt32();
            cardToRefresh.disabled = (status & 0x0001) == 0x0001;
            cardToRefresh.SemiNomiSummoned = (status & 0x0008) == 0x0008;
        }
        if ((flag & (int)Query.LScale) != 0)
            data.LScale = r.ReadInt32();
        if ((flag & (int)Query.RScale) != 0)
            data.RScale = r.ReadInt32();
        int l3 = 0;
        if ((flag & (int)Query.Link) != 0)
        {
            l3 = r.ReadInt32(); //link value
            data.LinkMarker = r.ReadInt32();
        }
        if (((flag & (int)Query.Level) != 0) || ((flag & (int)Query.Rank) != 0) || ((flag & (int)Query.Link) != 0))
        {
            if (l1 > l2)
            {
                data.Level = l1;
            }
            else
            {
                data.Level = l2;
            }
            if(l3 > data.Level)
                data.Level = l3;
        }

        cardToRefresh.set_data(data);
        //
    }
}

public class SocketMaster
{
    const int HeaderLength = 2;
    const int MaxPayloadLength = 0xFFFF;

    /// <summary>
    /// 读满 length 字节。返回 null 表示「这条连接完了」（对端关闭 / 被取消 / 套接字已释放）——
    /// 调用方据此请求断开，不再像旧实现那样直接置全局 onDisConnected 还返回半截缓冲。
    /// 读超时不是断线：ReceiveTimeoutMs 到了只说明这段空闲没包，继续等。
    /// </summary>
    static byte[] ReadFull(NetworkStream stream, int length, CancellationToken token)
    {
        if (stream == null)
            return null;
        if (length == 0)
            return Array.Empty<byte>();
        if (length < 0)
            return null;

        var buf = new byte[length];
        int rlen = 0;
        while (rlen < buf.Length)
        {
            if (token.IsCancellationRequested)
                return null;

            int currentLength;
            try
            {
                currentLength = stream.Read(buf, rlen, buf.Length - rlen);
            }
            catch (IOException ioEx) when (IsTimeout(ioEx))
            {
                continue;
            }
            catch (SocketException se) when (se.SocketErrorCode == SocketError.TimedOut)
            {
                continue;
            }
            catch (ObjectDisposedException)
            {
                return null;
            }

            if (currentLength == 0)
                return null;

            rlen += currentLength;
        }

        return buf;
    }

    static bool IsTimeout(IOException ioEx)
    {
        if (ioEx == null)
            return false;
        if (ioEx.InnerException is SocketException se)
            return se.SocketErrorCode == SocketError.TimedOut;
        return false;
    }

    public static byte[] ReadPacket(NetworkStream stream, CancellationToken token)
    {
        var hdr = ReadFull(stream, HeaderLength, token);
        if (hdr == null)
            return null;

        var plen = BitConverter.ToUInt16(hdr, 0);
        if (plen == 0 || plen > MaxPayloadLength)
            return null;

        return ReadFull(stream, plen, token);
    }
}

public class TcpClientWithTimeout
{
    protected string _hostname;
    protected int _port;
    protected int _timeout_milliseconds;
    protected TcpClient connection;
    protected bool connected;
    protected Exception exception;

    /// <summary>
    /// 合作式取消标志（对齐 hex）：超时后不再对本线程 <c>Thread.Abort()</c>。
    /// Abort 是会破坏运行时状态的 API —— 它可能把线程停在 DNS 解析 / socket 连接的中途，
    /// 留下半开的句柄；而这里的目标只是「别等它了」，让后台线程自己 Close 掉即可。
    /// </summary>
    private volatile bool _isCancelled = false;

    public TcpClientWithTimeout(string hostname, int port, int timeout_milliseconds)
    {
        _hostname = hostname;
        _port = port;
        _timeout_milliseconds = timeout_milliseconds;
    }
    public TcpClient Connect()
    {
        // kick off the thread that tries to connect
        connected = false;
        exception = null;
        _isCancelled = false;
        Thread thread = new Thread(new ThreadStart(BeginConnect));
        thread.IsBackground = true; // 作为后台线程处理
                                    // 不会占用机器太长的时间
        thread.Start();

        // 等待如下的时间
        if (thread.Join(_timeout_milliseconds))
        {
            if (exception != null)
            {
                // 如果失败就抛出错误
                TcpHelper.onDisConnected = true;
                Program.DEBUGLOG("onDisConnected 7");
                throw exception;
            }
            // 如果成功就返回TcpClient对象
            return connection;
        }

        // 超时：给后台线程发取消信号，让它自己收尾（⛔ 不 Abort）
        _isCancelled = true;
        TcpHelper.onDisConnected = true;
        Program.DEBUGLOG("onDisConnected 8");
        throw new TimeoutException(string.Format("TcpClient connection to {0}:{1} timed out",
          _hostname, _port));
    }
    protected void BeginConnect()
    {
        try
        {
            // TcpClient 构造函数本身可能卡很久（DNS 解析），所以先建空的、再用异步连接。
            var client = new TcpClient();
            if (_isCancelled)
            {
                client.Close();
                return;
            }

            IAsyncResult result = client.BeginConnect(_hostname, _port, null, null);
            WaitHandle handle = result.AsyncWaitHandle;
            if (handle.WaitOne(_timeout_milliseconds))
            {
                if (_isCancelled)
                {
                    client.Close();
                    return;
                }

                // 这一步会把连接失败的原因抛出来
                client.EndConnect(result);
                connection = client;
                connected = true;
            }
            else
            {
                client.Close();
                if (!_isCancelled)
                {
                    exception = new TimeoutException("Inner connection timeout.");
                }
            }
        }
        catch (Exception ex)
        {
            // 标记失败
            exception = ex;
        }
    }
}
