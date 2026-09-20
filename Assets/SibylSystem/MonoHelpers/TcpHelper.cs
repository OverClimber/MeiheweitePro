using System;
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

    public static void join(string ipString, string name, string portString, string pswString, string version)
    {
        if (canjoin)
        {
            if (tcpClient == null || tcpClient.Connected == false)
            {
                canjoin = false;
                try
                {
                    tcpClient = new TcpClientWithTimeout(ipString, int.Parse(portString), 3000).Connect();
                    networkStream = tcpClient.GetStream();
                    Thread t = new Thread(receiver);
                    t.Start();
                    CtosMessage_ExternalAddress(ipString);
                    CtosMessage_PlayerInfo(name);
                    CtosMessage_JoinGame(pswString, version);
                }
                catch (Exception e)
                {
                    Program.DEBUGLOG("onDisConnected 10");
                }
                canjoin = true;
            }
        }
        else
        {
            onDisConnected = true;
            Program.DEBUGLOG("onDisConnected 1");
        }
    }

    public static void receiver()
    {
        try
        {
            while (tcpClient != null && networkStream != null && tcpClient.Connected && Program.Running)
            {
                byte[] data = SocketMaster.ReadPacket(networkStream);
                addDateJumoLine(data);
            }
            onDisConnected = true;
            Program.DEBUGLOG("onDisConnected 2");
        }
        catch (Exception e)
        {
            onDisConnected = true;
            Program.DEBUGLOG("onDisConnected 3");
        }

    }

    public static void addDateJumoLine(byte[] data)
    {
        Monitor.Enter(datas);
        try
        {
            datas.Add(data);
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.Log(e);
        }
        Monitor.Exit(datas);
    }

    public static bool onDisConnected = false;

    /// <summary>收到的入站包总数（含每一条 GameMsg）。看门狗用「它还动不动」判对手是不是卡死了。</summary>
    public static int inboundCount = 0;

    /// <summary>最后一个入站包到达的时刻（Program.TimePassed 毫秒）。</summary>
    public static int lastInboundMs = 0;

    /// <summary>
    /// 主动断开当前连接。
    /// userInitiated 为 true 表示用户主动退出（例如从服务器选择界面返回），
    /// 此时只关闭套接字，由 receiver 线程走正常的收尾流程，
    /// 不再额外置位 onDisConnected，避免重复触发断线处理。
    /// </summary>
    public static void Disconnect(bool userInitiated = true)
    {
        try
        {
            if (tcpClient != null && tcpClient.Connected)
            {
                tcpClient.Client.Shutdown(SocketShutdown.Both);
                tcpClient.Close();
            }
        }
        catch (System.Exception e)
        {
            Program.DEBUGLOG("Disconnect error: " + e.Message);
        }
        tcpClient = null;
        networkStream = null;
        if (userInitiated == false)
        {
            onDisConnected = true;
        }
    }

    static List<byte[]> datas = new List<byte[]>();

    public static void preFrameFunction()
    {
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
            if (TcpHelper.tcpClient != null)
            {
                if (TcpHelper.tcpClient.Connected)
                {
                    tcpClient.Client.Shutdown(0);
                    tcpClient.Close();
                }
            }

            tcpClient = null;

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

    public static void Send(Package message)
    {
        if (tcpClient != null && tcpClient.Connected)
        {
            Thread t = new Thread(sender);
            t.Start(message);
        }
    }

    static object locker = new object();

    static void sender(object o)
    {
        try
        {
            lock (locker)
            {
                Package message = (Package)o;
                byte[] data = message.Data.get();
                MemoryStream memstream = new MemoryStream();
                BinaryWriter b = new BinaryWriter(memstream);
                b.Write(BitConverter.GetBytes((Int16)data.Length + 1), 0, 2);
                b.Write(BitConverter.GetBytes((byte)message.Fuction), 0, 1);
                b.Write(data, 0, data.Length);
                byte[] s = memstream.ToArray();
                tcpClient.Client.Send(s);
                QuickTestTrace.Log("ctos", "sent " + ((CtosMessage)message.Fuction) + " (" + s.Length + "B)");
            }
        }
        catch (Exception e)
        {
            QuickTestTrace.Log("ctos", "send FAILED: " + e.Message);
            onDisConnected = true;
            Program.DEBUGLOG("onDisConnected 5");
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
    static byte[] ReadFull(NetworkStream stream, int length)
    {
        var buf = new byte[length];
        int rlen = 0;
        while (rlen < buf.Length)
        {
            int currentLength = stream.Read(buf, rlen, buf.Length - rlen);
            rlen += currentLength;
            if (currentLength == 0)
            {
                TcpHelper.onDisConnected = true;
                Program.DEBUGLOG("onDisConnected 6");
                break;
            }
        }

        return buf;
    }

    public static byte[] ReadPacket(NetworkStream stream)
    {
        var hdr = ReadFull(stream, 2);
        var plen = BitConverter.ToUInt16(hdr, 0);
        var buf = ReadFull(stream, plen);
        return buf;
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
        Thread thread = new Thread(new ThreadStart(BeginConnect));
        thread.IsBackground = true; // 作为后台线程处理
                                    // 不会占用机器太长的时间
        thread.Start();

        // 等待如下的时间
        thread.Join(_timeout_milliseconds);

        if (connected == true)
        {
            // 如果成功就返回TcpClient对象
            thread.Abort();
            return connection;
        }
        if (exception != null)
        {
            // 如果失败就抛出错误
            thread.Abort();
            TcpHelper.onDisConnected = true;
            Program.DEBUGLOG("onDisConnected 7");
            throw exception;
        }
        else
        {
            // 同样地抛出错误
            thread.Abort();
            string message = string.Format("TcpClient connection to {0}:{1} timed out",
              _hostname, _port);
            TcpHelper.onDisConnected = true;
            Program.DEBUGLOG("onDisConnected 8");
            throw new TimeoutException(message);
        }
    }
    protected void BeginConnect()
    {
        try
        {
            connection = new TcpClient(_hostname, _port);
            // 标记成功，返回调用者
            connected = true;
        }
        catch (Exception ex)
        {
            // 标记失败
            exception = ex;
        }
    }
}
