using UnityEngine;
using System;
using System.Collections.Generic;
using System.IO;
using YGOSharp.Network.Enums;

public class Room : WindowServantSP
{
    UIselectableList superScrollView = null;

    string sort = "sortByTimeDeck";

    /// <summary>
    /// 「卡组测试」直开局标志。由 AIRoom.launchQuickTest() 置位，Room.ini() 消费一次。
    /// 置位后本次进房会自动「决斗准备」并（房主时）自动「开始游戏」，
    /// 猜拳与先后攻选择全部自动应答，玩家点一下就直达自己的第一个回合。
    /// </summary>
    public static bool quickStart = false;

    /// <summary>
    /// 本次房间是否为测试直开局（由 quickStart 一次性转换而来）。
    /// 对局结束时要靠它区分「AI 测试局」和「正常联机对局」，见 Ocgcore.onDuelResultConfirmed。
    /// </summary>
    public bool quickThisDuel = false;

    /// <summary>
    /// 本次房间是不是「撤回重开」进来的（主菜单人机对战那条线，见 DuelUndo.replayingPremove）。
    ///
    /// 它**不是**测试局：玩家是从主菜单进的，打完之后该回人机界面、房间界面也不该出现；
    /// 但开局前那几步（准备 / 猜拳 / 先后攻）要照**录下来的**再走一遍，
    /// 而不是照测试局的常量 —— 否则重开的这一局与原来那一局先手不同，撤回当场失效。
    /// </summary>
    public bool undoRestartThisDuel = false;

    /// <summary>
    /// 本次房间要不要「进房即自动准备/开局」：测试直开局，或撤回重开。
    /// 两条线共用同一段自动流程（<see cref="quickStartFlow"/>），差别只在答案从哪来。
    /// </summary>
    public bool autoStartThisDuel = false;

    /// <summary>测试局固定选先攻。</summary>
    private const bool QuickStartGoFirst = true;

    /// <summary>
    /// 测试局固定出的猜拳值。取值 1=剪刀 2=石头 3=布（与 ES_RMS 里 jiandao/shitou/bu 一致）。
    /// launchQuickTest 会用 Hand=1 把 WindBot 的猜拳锁成「剪刀」，这里出「石头」必胜，
    /// 于是服务器必定把先攻选择权交给我们（single_duel.cpp 的 HandResult 判定）。
    /// </summary>
    private const int QuickStartHand = 2;

    /// <summary>测试局自动猜拳是否已应答，避免重复发包。</summary>
    private bool quickHandAnswered = false;

    /// <summary>测试局的自动流程是否已经拉起，避免重复跑。</summary>
    private bool quickFlowStarted = false;

    /// <summary>本次测试局是否已经真的进入决斗（收到 DuelStart）。</summary>
    private bool quickDuelStarted = false;

    public override void initialize()
    {
        instanceHide = true;

        SetBar(Program.I().new_bar_room, 0, 0);
    }

    void onSelected()
    {
        GameModeManager.SetDeckInUse(superScrollView.selectedString);
        if (selftype < realPlayers.Length && realPlayers[selftype] != null && realPlayers[selftype].getIfPreped())
        {
            TcpHelper.CtosMessage_HsNotReady();
            TcpHelper.CtosMessage_UpdateDeck(new YGOSharp.Deck(GameModeManager.DeckPath(superScrollView.selectedString)));
            TcpHelper.CtosMessage_HsReady();
        }
    }

    void printFile()
    {
        string deckInUse = GameModeManager.DeckInUse;
        superScrollView.clear();
        FileInfo[] fileInfos = (new DirectoryInfo(GameModeManager.DeckDir)).GetFiles();
        if (Config.Get(sort,"1") == "1")
        {
            Array.Sort(fileInfos, UIHelper.CompareTime);
        }
        else
        {
            Array.Sort(fileInfos, UIHelper.CompareName);
        }
        for (int i = 0; i < fileInfos.Length; i++)
        {
            if (fileInfos[i].Name.Length > 4)
            {
                if (fileInfos[i].Name.Substring(fileInfos[i].Name.Length - 4, 4) == ".ydk")
                {
                    if (fileInfos[i].Name.Substring(0, fileInfos[i].Name.Length - 4) == deckInUse)
                    {
                        superScrollView.add(fileInfos[i].Name.Substring(0, fileInfos[i].Name.Length - 4));
                    }
                }
            }
        }
        for (int i = 0; i < fileInfos.Length; i++)
        {
            if (fileInfos[i].Name.Length > 4)
            {
                if (fileInfos[i].Name.Substring(fileInfos[i].Name.Length - 4, 4) == ".ydk")
                {
                    if (fileInfos[i].Name.Substring(0, fileInfos[i].Name.Length - 4) != deckInUse)
                    {
                        superScrollView.add(fileInfos[i].Name.Substring(0, fileInfos[i].Name.Length - 4));
                    }
                }
            }
        }
    }

    public override void show()
    {
        if (isShowed == true)
        {
            Menu.deleteShell();
        }
        base.show();
        Program.I().ocgcore.handler = handler;
        UIHelper.registEvent(toolBar, "input_", onChat);
        Program.charge();
    }

    /// <summary>
    /// 自动开局（测试直开局 / 撤回重开）该发哪一副卡组。
    ///
    /// 撤回重开必须发**当初那一份**（见 DuelTimeline.DeckCodes）：靠卡组名回读 ydk 文件
    /// 不可靠 —— 主菜单人机对战里玩家可以在房间界面临时换一副（`onSelected` 只改 Config），
    /// 文件本身也可能被改过；卡组差一张，重开后摸牌就与录下来的入站流对不上，第一条就分叉。
    /// 录不到（异常情况）才退回按卡组名读文件。
    /// </summary>
    YGOSharp.Deck DeckForAutoStart()
    {
        if (undoRestartThisDuel && DuelUndo.saved != null)
        {
            YGOSharp.Deck recorded = DuelUndo.saved.BuildDeck();
            if (recorded != null)
            {
                QuickTestTrace.Log("flow", "undo restart: reuse recorded deck ("
                    + DuelUndo.saved.deck.Brief() + ")");
                return recorded;
            }
            QuickTestTrace.Log("flow", "undo restart: 没录到卡组，退回按卡组名读文件");
        }
        return new YGOSharp.Deck(GameModeManager.DeckPath(GameModeManager.DeckInUse));
    }

    /// <summary>
    /// 自动开局的自动流程：自动「决斗准备」→（房主时）自动「开始游戏」。
    ///
    /// 两条线共用：卡组界面的「测试」（`quickThisDuel`）与主菜单人机对战的「撤回重开」
    /// （`undoRestartThisDuel`）。房间界面在这两种情况下全程不出场
    /// （对齐 MDPro3 的 RoomServant.FromHandTest），所以这个协程由 StocMessage_JoinGame
    /// 直接拉起，而不是等 show()。
    /// </summary>
    private System.Collections.IEnumerator quickStartFlow()
    {
        if (quickFlowStarted)
        {
            yield break;
        }
        quickFlowStarted = true;
        QuickTestTrace.Log("flow", "coroutine start");
        // 等房间状态铺完，否则 UpdateDeck 会在服务器还没登记玩家时发出去
        yield return new WaitForSeconds(0.5f);
        TcpHelper.CtosMessage_UpdateDeck(DeckForAutoStart());
        TcpHelper.CtosMessage_HsReady();
        QuickTestTrace.Log("flow", "UpdateDeck + HsReady sent, deckInUse=" + GameModeManager.DeckInUse
            + " undoRestart=" + undoRestartThisDuel);
        // AI 由服务器代管，就绪状态要等它回包；抢跑开局的 HsStart 会被服务器丢掉，
        // 所以这里不赌单次，改为「每隔 1 秒重发一次 HsStart，直到真的收到 DuelStart」。
        for (int i = 0; i < 8; i++)
        {
            yield return new WaitForSeconds(1.0f);
            if (quickDuelStarted)
            {
                QuickTestTrace.Log("flow", "duel started, stop retrying at #" + i);
                yield break;
            }
            if (is_host)
            {
                TcpHelper.CtosMessage_HsStart();
            }
            QuickTestTrace.Log("flow", "retry#" + i + " is_host=" + is_host + " sent HsStart");
        }
        QuickTestTrace.Log("flow", "GIVE UP: DuelStart never arrived");
    }

    public void onSubmit(string val)
    {
        if (val != "")
        {
            TcpHelper.CtosMessage_Chat(val);
            //AddChatMsg(val, -1);
        }
    }

    public void onChat()
    {
        onSubmit(UIHelper.getByName<UIInput>(toolBar, "input_").value);
        UIHelper.getByName<UIInput>(toolBar, "input_").value = "";
    }

    void handler(byte[] buffer)
    {
        TcpHelper.CtosMessage_Response(buffer);
    }

    #region STOC

    int animationTime = 0;

    public void StocMessage_HsWatchChange(BinaryReader r)
    {
        countOfObserver = r.ReadUInt16();
        realize();
    }

    public void StocMessage_HsPlayerChange(BinaryReader r)
    {
        int status = r.ReadByte();
        int pos = (status >> 4) & 0xf;
        int state = status & 0xf;
        if (pos < 4)
        {
            if (state < 8)
            {
                roomPlayers[state] = roomPlayers[pos];
                roomPlayers[pos] = null;
            }
            if (state == 0x9)
            {
                roomPlayers[pos].prep = true;
            }
            if (state == 0xa)
            {
                roomPlayers[pos].prep = false;
            }
            if (state == 0xb)
            {
                roomPlayers[pos] = null;
            }
            if (state == 0x8)
            {
                roomPlayers[pos] = null;
                countOfObserver++;
            }
            realize();
        }
    }

    public void StocMessage_HsPlayerEnter(BinaryReader r)
    {
        string name = r.ReadUnicode(20);
        int pos = r.ReadByte()&3;//Fuck this
        RoomPlayer player = new RoomPlayer();
        player.name = name;
        player.prep = false;
        roomPlayers[pos] = player;
        realize();
        UIHelper.Flash();
    }

    public void StocMessage_Chat(BinaryReader r)
    {
        int player = r.ReadInt16();
        long length = r.BaseStream.Length - 3;
        string str = r.ReadUnicode((int)length);

        if (player < 4)
        {
            if (UIHelper.fromStringToBool(Config.Get("ignoreOP_","0")) == true)
                return;
            if (mode != 2)
            {
                if (Program.I().ocgcore.isShowed)
                    player = Program.I().ocgcore.localPlayer(player);
            }
            else
            {
                if (Program.I().ocgcore.isShowed && !Program.I().ocgcore.isFirst)
                    player ^= 2;
                if (player == 0)
                    player = 0;
                else if (player == 1)
                    player = 2;
                else if (player == 2)
                    player = 1;
                else if (player == 3)
                    player = 3;
                else
                    player = 10;
            }
        }
        else
        {
            if (UIHelper.fromStringToBool(Config.Get("ignoreWatcher_","0")) == true)
                return;
        }
        AddChatMsg(str, player);
    }

    public void StocMessage_DeckCount(BinaryReader r)
    {
        int deck0=r.ReadInt16();
        int extra0=r.ReadInt16();
        int side0=r.ReadInt16();
        int deck1=r.ReadInt16();
        int extra1=r.ReadInt16();
        int side1=r.ReadInt16();
        string str= InterString.Get("对方主卡组：[?]张", deck1.ToString()) +
            InterString.Get("，额外卡组：[?]张", extra1.ToString()) +
            InterString.Get("，副卡组：[?]张", side1.ToString());
        AddChatMsg(str, 10);
    }

    public void AddChatMsg(string msg, int player)
    {
        string result = "";
        switch (player)
        {
            case -1: //local name
                result += Program.I().selectServer.name;
                result += ":";
                break;
            case 0: //from host
                result += Program.I().ocgcore.name_0;
                result += ":";
                break;
            case 1: //from client
                result += Program.I().ocgcore.name_1;
                result += ":";
                break;
            case 2: //host tag
                result += Program.I().ocgcore.name_0_tag;
                result += ":";
                break;
            case 3: //client tag
                result += Program.I().ocgcore.name_1_tag;
                result += ":";
                break;
            case 7: //---
                result += "[---]";
                result += ":";
                break;
            case 8: //system custom message, no prefix.
                result += "[System]";
                result += ":";
                break;
            case 9: //error message
                result += "[Script error]";
                result += ":";
                break;
            default: //from watcher or unknown
                result += "[---]";
                result += ":";
                break;
        }
        result += msg;
        string res = "[888888]" + result + "[-]";
        Program.I().book.add(res);
        Package p = new Package();
        p.Fuction = (int)YGOSharp.OCGWrapper.Enums.GameMessage.sibyl_chat;
        p.Data = new BinaryMaster();
        p.Data.writer.WriteUnicode(res, res.Length + 1);
        TcpHelper.AddRecordLine(p);
        switch ((PlayerType)player)
        {
            case PlayerType.Red:
                result = "[FF3030]" + result + "[-]";
                break;
            case PlayerType.Green:
                result = "[7CFC00]" + result + "[-]";
                break;
            case PlayerType.Blue:
                result = "[4876FF]" + result + "[-]";
                break;
            case PlayerType.BabyBlue:
                result = "[63B8FF]" + result + "[-]";
                break;
            case PlayerType.Pink:
                result = "[EED2EE]" + result + "[-]";
                break;
            case PlayerType.Yellow:
                result = "[EEEE00]" + result + "[-]";
                break;
            case PlayerType.White:
                result = "[FAF0E6]" + result + "[-]";
                break;
            case PlayerType.Gray:
                result = "[CDC9C9]" + result + "[-]";
                break;
        }
        RMSshow_none(result);
    }

    public void StocMessage_Replay(BinaryReader r)
    {
        byte[] data = r.ReadToEnd();
        Package p = new Package();
        p.Fuction = (int)YGOSharp.OCGWrapper.Enums.GameMessage.sibyl_replay;
        p.Data = new BinaryMaster();
        p.Data.writer.Write(data);
        TcpHelper.AddRecordLine(p);
        TcpHelper.SaveRecord(); 
    }

    public bool duelEnded = false;

    public void StocMessage_DuelEnd(BinaryReader r)
    {
        duelEnded = true;
        Program.I().ocgcore.forceMSquit();
    }

    public void StocMessage_DuelStart(BinaryReader r)
    {
        quickDuelStarted = true;
        QuickTestTrace.Log("duel", "DuelStart received (quick=" + quickThisDuel
            + " undoRestart=" + undoRestartThisDuel + ")");
        // 开局前握手到此结束：猜拳/先后攻都答完了，撤回重开的「照录下来的再答一次」
        // 这一段用完即止，别影响后续（比如中途换备后重回房间）的界面。
        DuelUndo.replayingPremove = false;
        needSide = false;
        joinWithReconnect = true;
        if (Program.I().deckManager.isShowed)
        {
            Program.I().deckManager.hide();
            // 测试直开局不是「换副卡组」，别弹这句提示（对齐 MDPro3 的 FromHandTest 处理）
            if (!quickThisDuel)
            {
                RMSshow_onlyYes("",InterString.Get("更换副卡组成功，请等待对手更换副卡组。"),null);
            }
        }
        if (isShowed)
        {
            hide();
        }
        if (selftype < 4)
        {
            Program.I().ocgcore.shiftCondition(Ocgcore.Condition.duel);
        }
        else
        {
            Program.I().ocgcore.shiftCondition(Ocgcore.Condition.watch);
        }
        Program.I().ocgcore.showBarOnly();
    }

    public void StocMessage_TypeChange(BinaryReader r)
    {
        int type = r.ReadByte();
        selftype = type & 0xf;
        is_host = ((type >> 4) & 0xf) != 0;
        QuickTestTrace.Log("type", "selftype=" + selftype + " is_host=" + is_host);
        if (is_host)
        {
            if (selftype < 4 && roomPlayers[selftype] != null) {
                roomPlayers[selftype].prep = false;
            }
            UIHelper.shiftButton(startButton(), true);
            lazyRoom.start.localScale = Vector3.one;
            lazyRoom.ready.localPosition = new Vector3(lazyRoom.duelist.localPosition.x, -94.2f + 30f, 0);
            lazyRoom.duelist.localPosition = new Vector3(lazyRoom.duelist.localPosition.x, -94.2f, 0);
            lazyRoom.observer.localPosition = new Vector3(lazyRoom.duelist.localPosition.x, -94.2f - 30f, 0);
            lazyRoom.start.localPosition = new Vector3(lazyRoom.duelist.localPosition.x, -94.2f - 30f - 30f, 0);
        }
        else
        {
            UIHelper.shiftButton(startButton(), false);
            lazyRoom.start.localScale = Vector3.zero;
            lazyRoom.ready.localPosition = new Vector3(lazyRoom.duelist.localPosition.x, -94.2f, 0);
            lazyRoom.duelist.localPosition = new Vector3(lazyRoom.duelist.localPosition.x, -94.2f - 30f, 0);
            lazyRoom.observer.localPosition = new Vector3(lazyRoom.duelist.localPosition.x, -94.2f - 30f - 30f, 0);
            lazyRoom.start.localPosition = new Vector3(lazyRoom.duelist.localPosition.x, -94.2f - 30f - 30f - 30f, 0);
        }
        realize();
    }

    public void StocMessage_JoinGame(BinaryReader r)
    {
        lflist = r.ReadUInt32();
        rule = r.ReadByte();
        mode = r.ReadByte();
        Program.I().ocgcore.MasterRule = r.ReadChar();
        QuickTestTrace.Log("rule", "JoinGame -> MasterRule=" + Program.I().ocgcore.MasterRule);
        no_check_deck = r.ReadBoolean();
        no_shuffle_deck = r.ReadBoolean();
        r.ReadByte();
        r.ReadByte();
        r.ReadByte();
        start_lp = r.ReadInt32();
        start_hand = r.ReadByte();
        draw_count = r.ReadByte();
        time_limit = r.ReadInt16();
        QuickTestTrace.Log("join", "JoinGame before ini: quickStart=" + quickStart + " quickThisDuel=" + quickThisDuel);
        ini();
        QuickTestTrace.Log("join", "JoinGame after ini: quickThisDuel=" + quickThisDuel
            + " undoRestart=" + undoRestartThisDuel);
        if (autoStartThisDuel)
        {
            // 自动开局（测试直开局 / 撤回重开）：房间（选卡组 / 等人 / 准备 / 开始）界面
            // 全程不出场。测试局对齐 MDPro3 的 RoomServant.FromHandTest；撤回重开更必须如此 ——
            // 玩家按下撤回时盘面已经退回去了，这里再把他丢回房间界面等「决斗准备」就前功尽弃。
            //
            // 注意：ini() 里的 createWindow 只是 Instantiate，并不会把窗口收起来
            // （对照 AIRoom.initialize 末尾那句 SetActiveFalse），所以必须在这里手动关掉。
            // 本方法跑在 preFrameFunction 里、同一帧内完成，所以窗口不会被渲染出来。
            SetActiveFalse();
            // 包的收发通道要照常接上，否则收不到 STOC 也发不出响应
            Program.I().ocgcore.handler = handler;
            Program.charge();
            Program.I().StartCoroutine(quickStartFlow());
            QuickTestTrace.Log("join", "auto start: room hidden, flow coroutine started (quick="
                + quickThisDuel + " undoRestart=" + undoRestartThisDuel + ")");
        }
        else
        {
            Program.I().shiftToServant(Program.I().room);
            QuickTestTrace.Log("join", "normal mode: shifted to room");
        }
    }

    public bool sideWaitingObserver = false;

    public void StocMessage_WaitingSide(BinaryReader r)
    {
        sideWaitingObserver = true;
        RMSshow_none(InterString.Get("请耐心等待双方玩家更换副卡组。"));
    }

    public void StocMessage_TeammateSurrender(BinaryReader r)
    {
        if(Program.I().ocgcore.surrended)
            RMSshow_none(InterString.Get("已申请投降，请等待队友同意。"));
        else
            RMSshow_none(InterString.Get("队友申请投降。若您同意，请点击投降按钮。"));
    }

    public bool needSide = false;

    public bool joinWithReconnect = false;

    public void StocMessage_ChangeSide(BinaryReader r)
    {
        Program.I().ocgcore.surrended = false;
        Program.I().ocgcore.returnServant = Program.I().deckManager;
        needSide = true;
        if(Program.I().ocgcore.condition != Ocgcore.Condition.duel || joinWithReconnect) { //Change side when reconnect
            Program.I().ocgcore.onDuelResultConfirmed();
        }
    }

    GameObject handres = null;
    public void StocMessage_HandResult(BinaryReader r)
    {
        int meResult = r.ReadByte();
        int opResult = r.ReadByte();
        // 记下这一轮猜拳的结果：它决定先手，而先手会写进第一条 GameMessage.Start 的 playertype
        // （由 Ocgcore 解析后交给 DuelTimeline.NoteStartFirst 记成权威真值），
        // 重开时若钉不回去，整条流从第 0 条摘要不符。
        // 见 DuelTimeline.startFirst / Snapshot.BotHandOverride / AIRoom.forcedBotHand。
        DuelTimeline.NoteRpsResult(meResult, opResult);

        // 🔑 平局会**在同一局内再问一次** SelectHand —— 收到这一轮的结果就把「已答」的闩松开。
        //    以前这个闩只在 initialize() 里复位（一局一次），于是平局时第二次 SelectHand 被静默吞掉：
        //    服务器在等 HandResult、我们永远不发 ⇒ 整局停在猜拳那一步（日志签名：
        //    两条 recv SelectHand 之间夹着一条 HandResult，第二条后面 is answered=True 且再无 [stoc]）。
        //    对局僵在开局 ⇒ 撤回按钮当然没创建、[opt] 一次都不出现，看起来像「功能全崩」，
        //    其实跟功能毫无关系。对手那一手是随机的，平局约 1/3 ⇒ 三成的验收跑会随机暴死。
        //    松开闩不影响它本来的作用：重复的 SelectHand 是连着来的、中间没有 HandResult。
        quickHandAnswered = false;

        // 自动开局（测试直开局 / 撤回重开）：连猜拳揭示动画也省掉，直接进先后攻判定。
        // 撤回重开时这一段的画面本来就该跳过 —— 玩家刚按完撤回，不该被塞一段猜拳动画。
        // qt_autojoin.on（验收通道）也走这一支：没人点，动画只是白等 1.3 秒。
        if (autoStartThisDuel || autoJoinOn())
        {
            QuickTestTrace.Log("rps", "handresult me=" + meResult + " op=" + opResult
                + " quick=" + quickThisDuel + " undoRestart=" + undoRestartThisDuel);
            if (quickThisDuel)
            {
                // 落一行轨迹，便于日后核对「必胜猜拳」是否真的成立
                // （期望 me=2 op=1，即我们出石头、WindBot 被 Hand=1 锁成剪刀）
                try
                {
                    System.IO.File.WriteAllText(QuickTestTrace.LogPath("quicktest_rps.log"),
                        "me=" + meResult + " op=" + opResult + "\r\n"
                        + "expect me=2 (shi tou/rock) op=1 (jian dao/scissors)\r\n");
                }
                catch (System.Exception)
                {
                }
            }
            animationTime = 0;
            return;
        }
        if (isShowed)
        {
            hide();
        }
        Program.I().new_ui_handShower.GetComponent<handShower>().me = meResult - 1;
        Program.I().new_ui_handShower.GetComponent<handShower>().op = opResult - 1;
        handres = create(Program.I().new_ui_handShower, Vector3.zero, Vector3.zero, false, Program.ui_main_2d);
        destroy(handres, 10f);
        animationTime = 1300;
    }

    public void StocMessage_SelectTp(BinaryReader r)
    {
        // 自动开局：不弹「先攻/后攻」。测试直开局固定选先攻；撤回重开答出**原局的先手归属**，
        // 否则重开的这一局先手会换人，整条入站流从第 0 条就分叉（撤回当场失效）。
        // 为什么是「原局先手」而不是「照录我们当初点的那一手」：见 DuelUndo.ReplayGoFirst。
        if (autoStartThisDuel)
        {
            bool answer = undoRestartThisDuel ? DuelUndo.ReplayGoFirst() : QuickStartGoFirst;
            QuickTestTrace.Log("tp", "SelectTp -> answer first=" + answer
                + " quick=" + quickThisDuel + " undoRestart=" + undoRestartThisDuel);
            TcpHelper.CtosMessage_TpResult(answer);
            return;
        }
        // 验收通道：qt_autojoin.on 连这个弹窗也替玩家点掉（理由见 autoJoinOn）。
        if (autoJoinOn())
        {
            QuickTestTrace.Log("autojoin", "SelectTp -> auto answer first=" + QuickStartGoFirst);
            TcpHelper.CtosMessage_TpResult(QuickStartGoFirst);
            return;
        }
        if (animationTime != 0)
        {
            Program.go(animationTime, () =>
            {
                RMSshow_FS("StocMessage_SelectTp", new messageSystemValue { hint = InterString.Get("先攻"), value = "first" }, new messageSystemValue { hint = InterString.Get("后攻"), value = "second" });
            });
            animationTime = 0;
        }
        else
        {
            RMSshow_FS("StocMessage_SelectTp", new messageSystemValue { hint = InterString.Get("先攻"), value = "first" }, new messageSystemValue { hint = InterString.Get("后攻"), value = "second" });
        }
    }

    public override void ES_RMS(string hashCode, List<messageSystemValue> result)
    {
        base.ES_RMS(hashCode, result);
        if (hashCode == "StocMessage_SelectTp")
        {
            if (result[0].value == "first")
            {
                TcpHelper.CtosMessage_TpResult(true);
            }
            if (result[0].value == "second")
            {
                TcpHelper.CtosMessage_TpResult(false);
            }
        }
        if (hashCode == "StocMessage_SelectHand")
        {
            if (result[0].value == "jiandao")
            {
                TcpHelper.CtosMessage_HandResult(1);
            }
            if (result[0].value == "shitou")
            {
                TcpHelper.CtosMessage_HandResult(2);
            }
            if (result[0].value == "bu")
            {
                TcpHelper.CtosMessage_HandResult(3);
            }
        }
    }

    public void StocMessage_SelectHand(BinaryReader r)
    {
        // 自动开局：不弹猜拳界面。
        //  ・测试直开局 → 固定出「石头」把对手（被 Hand=1 锁成剪刀）吃掉（那次就是常量应答）；
        //  ・撤回重开   → 照**录下来的**那一手逐次再答。这里是主菜单人机对战能不能撤回的
        //                关键：原来那一手是玩家点出来的，重答得不一样就会「石头变布」，
        //                服务器判先手的结果随之改变，录下来的入站流从第 1 条就对不上。
        if (autoStartThisDuel)
        {
            if (undoRestartThisDuel)
            {
                int res = DuelUndo.NextHandAnswer();
                if (res < 1 || res > 3)
                {
                    // 录不到（老录像/异常）时退回常量，至少不卡在这一步。
                    res = QuickStartHand;
                    QuickTestTrace.Log("hand", "SelectHand -> 没录到猜拳应答，退回常量 " + res);
                }
                QuickTestTrace.Log("hand", "SelectHand -> answer " + res + " (undo restart)");
                TcpHelper.CtosMessage_HandResult(res);
                return;
            }
            QuickTestTrace.Log("hand", "SelectHand -> answer " + QuickStartHand + " (answered=" + quickHandAnswered + ")");
            if (!quickHandAnswered)
            {
                quickHandAnswered = true;
                TcpHelper.CtosMessage_HandResult(QuickStartHand);
            }
            return;
        }

        // 验收通道：qt_autojoin.on 连这个弹窗也替玩家点掉（理由见 autoJoinOn）。
        // 答的是常量「石头」，与测试局同口径 —— 反正这一手会被记进时间线，
        // 撤回重开时照它再答一次。
        if (autoJoinOn())
        {
            QuickTestTrace.Log("autojoin", "SelectHand -> auto answer " + QuickStartHand
                + " (answered=" + quickHandAnswered + ")");
            if (!quickHandAnswered)
            {
                quickHandAnswered = true;
                TcpHelper.CtosMessage_HandResult(QuickStartHand);
            }
            return;
        }

        if (animationTime != 0)
        {
            Program.go(animationTime, () =>
            {
                hide();
                RMSshow_tp("StocMessage_SelectHand"
                    , new messageSystemValue { hint = "jiandao", value = "jiandao" }
                    , new messageSystemValue { hint = "shitou", value = "shitou" }
                    , new messageSystemValue { hint = "bu", value = "bu" });
            });
            animationTime = 0;
        }
        else
        {
            hide();
            RMSshow_tp("StocMessage_SelectHand"
                    , new messageSystemValue { hint = "jiandao", value = "jiandao" }
                    , new messageSystemValue { hint = "shitou", value = "shitou" }
                    , new messageSystemValue { hint = "bu", value = "bu" });
        }
    }

    public void StocMessage_ErrorMsg(BinaryReader r)
    {
        int msg = r.ReadByte();
        QuickTestTrace.Log("error", "ErrorMsg kind=" + msg);
        int code = 0;
        switch (msg)    
        {
            case 1:
                r.ReadByte();
                r.ReadByte();
                r.ReadByte();
                code = r.ReadInt32();
                switch (code)
                {
                    case 0:
                        RMSshow_onlyYes("", GameStringManager.get_unsafe(1403), null);
                        break;
                    case 1:
                        RMSshow_onlyYes("", GameStringManager.get_unsafe(1404), null);
                        break;
                    case 2:
                        RMSshow_onlyYes("", GameStringManager.get_unsafe(1405), null);
                        break;
                }
                break;
            case 2:
                r.ReadByte();
                r.ReadByte();
                r.ReadByte();
                code = r.ReadInt32();
                int flag = code >> 28;
                code = code & 0xFFFFFFF;
                switch (flag)
                {
                    case 1: // DECKERROR_LFLIST
                        RMSshow_onlyYes("", InterString.Get("卡组非法，请检查：[?]", YGOSharp.CardsManager.Get(code).Name) + "\r\n" + InterString.Get("（数量不符合禁限卡表）"), null);
                        break;
                    case 2: // DECKERROR_OCGONLY
                        RMSshow_onlyYes("", InterString.Get("卡组非法，请检查：[?]", YGOSharp.CardsManager.Get(code).Name) + "\r\n" + InterString.Get("（OCG独有卡，不能在当前设置使用）"), null);
                        break;
                    case 3: // DECKERROR_TCGONLY
                        RMSshow_onlyYes("", InterString.Get("卡组非法，请检查：[?]", YGOSharp.CardsManager.Get(code).Name) + "\r\n" + InterString.Get("（TCG独有卡，不能在当前设置使用）"), null);
                        break;
                    case 4: // DECKERROR_UNKNOWNCARD
                        if (code < 100000000)
                            RMSshow_onlyYes("", InterString.Get("卡组非法，请检查：[?]", YGOSharp.CardsManager.Get(code).Name) + "\r\n" + InterString.Get("（服务器无法识别此卡，可能是服务器未更新）"), null);
                        else
                            RMSshow_onlyYes("", InterString.Get("卡组非法，请检查：[?]", YGOSharp.CardsManager.Get(code).Name) + "\r\n" + InterString.Get("（服务器无法识别此卡，可能是服务器不支持先行卡或此先行卡已正式更新）"), null);
                        break;
                    case 5: // DECKERROR_CARDCOUNT
                        RMSshow_onlyYes("", InterString.Get("卡组非法，请检查：[?]", YGOSharp.CardsManager.Get(code).Name) + "\r\n" + InterString.Get("（数量过多）"), null);
                        break;
                    case 6: // DECKERROR_MAINCOUNT
                        RMSshow_onlyYes("", InterString.Get("主卡组数量应为40-60张"), null);
                        break;
                    case 7: // DECKERROR_EXTRACOUNT
                        RMSshow_onlyYes("", InterString.Get("额外卡组数量应为0-15张"), null);
                        break;
                    case 8: // DECKERROR_SIDECOUNT
                        RMSshow_onlyYes("", InterString.Get("副卡组数量应为0-15"), null);
                        break;
                    default:
                        RMSshow_onlyYes("", GameStringManager.get_unsafe(1406), null);
                        break;
                }
                break;
            case 3:
                RMSshow_onlyYes("", InterString.Get("更换副卡组失败，请检查卡片张数是否一致。"), null);
                break;
            case 4:
                r.ReadByte();
                r.ReadByte();
                r.ReadByte();
                code = r.ReadInt32();
                //string hexOutput = "0x"+String.Format("{0:X}", code);
                //Program.I().selectServer.set_version(hexOutput);
                //RMSshow_none(InterString.Get("你输入的版本号和服务器不一致,[7CFC00]YGOPro2已经智能切换版本号[-]，请重新连接。"));
                break;
            default:
                break;
        }
    }

    public void StocMessage_GameMsg(BinaryReader r)
    {
        showOcgcore();  
        Package p = new Package();
        p.Fuction = r.ReadByte();
        p.Data = new BinaryMaster(r.ReadToEnd());
        Program.I().ocgcore.addPackage(p);
    }

    void showOcgcore()
    {
        if (handres != null)
        {
            destroy(handres);
            handres = null;
        }
        if (Program.I().ocgcore.isShowed == false)
        {
            Program.camera_game_main.transform.position = new Vector3(0, 230, -230);
            if (mode != 2)
            {
                if (selftype == 1)
                {
                    Program.I().ocgcore.name_0 = roomPlayers[1].name;
                    Program.I().ocgcore.name_1 = roomPlayers[0].name;
                    Program.I().ocgcore.name_0_tag = "---";
                    Program.I().ocgcore.name_1_tag = "---";
                }
                else
                {
                    Program.I().ocgcore.name_0 = roomPlayers[0].name;
                    Program.I().ocgcore.name_1 = roomPlayers[1].name;
                    Program.I().ocgcore.name_0_tag = "---";
                    Program.I().ocgcore.name_1_tag = "---";
                }
            }
            else
            {
                if (selftype == 2 || selftype == 3)
                {
                    Program.I().ocgcore.name_0 = roomPlayers[2].name;
                    Program.I().ocgcore.name_1 = roomPlayers[0].name;
                    Program.I().ocgcore.name_0_tag = roomPlayers[3].name;
                    Program.I().ocgcore.name_1_tag = roomPlayers[1].name;
                }
                else
                {
                    Program.I().ocgcore.name_0 = roomPlayers[0].name;
                    Program.I().ocgcore.name_1 = roomPlayers[2].name;
                    Program.I().ocgcore.name_0_tag = roomPlayers[1].name;
                    Program.I().ocgcore.name_1_tag = roomPlayers[3].name;
                }
            }
            Program.I().ocgcore.timeLimit = time_limit;
            Program.I().ocgcore.lpLimit = start_lp;
            Program.I().ocgcore.InAI = false;
            Program.notGo(showCoreHandler);
            Program.go(10, showCoreHandler);
        }
    }

    private static void showCoreHandler()
    {
        Program.I().shiftToServant(Program.I().ocgcore);
    }

    public void StocMessage_CreateGame(BinaryReader r)
    {
    }

    public void StocMessage_LeaveGame(BinaryReader r)
    {
    }

    public void StocMessage_TpResult(BinaryReader r)
    {
    }

    public class RoomPlayer
    {
        public string name = "";
        public bool prep = false;
    }

    public RoomPlayer[] roomPlayers = new RoomPlayer[32];

    lazyPlayer[] realPlayers = new lazyPlayer[4];

    public UInt32 lflist;
    public byte rule;
    public byte mode;
    public bool no_check_deck;
    public bool no_shuffle_deck;
    public int start_lp = 8000;
    public byte start_hand;
    public byte draw_count;
    public short time_limit = 180;
    public int countOfObserver = 0;

    public int selftype;
    public bool is_host;

    void realize()
    {
        // 自动开局（测试直开局 / 撤回重开）下房间界面从未被激活，而 UIHelper.getByName 用的是
        // GetComponentsInChildren<T>()（不含 inactive），realPlayers 会全取成 null。
        // 玩家名由 showOcgcore() 自己从 roomPlayers 里取，所以这里直接跳过
        // （对齐 MDPro3 的 RoomServant.Realize 早退）。
        if (autoStartThisDuel)
        {
            return;
        }
        GameModeManager.SetDeckInUse(superScrollView.selectedString);
        string description = "";
        if (mode == 0)
        {
            description += InterString.Get("单局模式");
        }
        if (mode == 1)
        {
            description += InterString.Get("比赛模式");
        }
        if (mode == 2)
        {
            description += InterString.Get("双打模式");
        }
        if (Program.I().ocgcore.MasterRule == 5)
        {
            description += InterString.Get("/大师规则2020") + "\r\n";
        }
        else if (Program.I().ocgcore.MasterRule == 4)
        {
            description += InterString.Get("/新大师规则") + "\r\n";
        }
        else
        {
            description += InterString.Get("/大师规则[?]", Program.I().ocgcore.MasterRule.ToString()) + "\r\n";
        }
        description += InterString.Get("禁限卡表:[?]", YGOSharp.BanlistManager.GetName(lflist)) + "\r\n";
        if (rule == 0)
        {
            description += InterString.Get("(OCG卡池)") + "\r\n";
        }
        if (rule == 1)
        {
            description += InterString.Get("(TCG卡池)") + "\r\n";
        }
        if (rule == 2)
        {
            description += InterString.Get("(简中卡池)") + "\r\n";
        }
        if (rule == 3)
        {
            description += InterString.Get("(自制卡卡池)") + "\r\n";
        }
        if (rule == 4)
        {
            description += InterString.Get("(无独有卡卡池)") + "\r\n";
        }
        if (rule == 5)
        {
            description += InterString.Get("(混合卡池)") + "\r\n";
        }
        if (no_check_deck)
        {
            description += InterString.Get("*不检查卡组") + "\r\n";
        }
        if (no_shuffle_deck)
        {
            description += InterString.Get("*不洗牌") + "\r\n";
        }
        description += InterString.Get("LP:[?]", start_lp.ToString()) + " ";
        description += InterString.Get("手牌:[?]", start_hand.ToString()) + " \r\n";
        description += InterString.Get("抽卡:[?]", draw_count.ToString()) + " ";
        description += InterString.Get("时间:[?]", time_limit.ToString()) + "\r\n";
        description += InterString.Get("观战者人数:[?]", countOfObserver.ToString());
        UIHelper.trySetLableText(gameObject, "description_", description);
        Program.I().ocgcore.name_0 = "---";
        Program.I().ocgcore.name_1 = "---";
        Program.I().ocgcore.name_0_tag = "---";
        Program.I().ocgcore.name_1_tag = "---";
        for (int i = 0; i < 4; i++)
        {
            realPlayers[i] = UIHelper.getByName<lazyPlayer>(gameObject, i.ToString());
            if (roomPlayers[i] == null)
            {
                realPlayers[i].SetNotNull(false);
            }
            else
            {
                realPlayers[i].SetNotNull(true);
                realPlayers[i].setName(roomPlayers[i].name);
                realPlayers[i].SetIFcanKick(is_host&&(i != selftype));
                realPlayers[i].setIfMe(i == selftype);
                realPlayers[i].setIfprepared(roomPlayers[i].prep);
                if (mode != 2)
                {
                    if (i == 0)
                    {
                        Program.I().ocgcore.name_0 = roomPlayers[i].name;
                    }
                    if (i == 1)
                    {
                        Program.I().ocgcore.name_1 = roomPlayers[i].name;
                    }
                    Program.I().ocgcore.name_0_tag = "---";
                    Program.I().ocgcore.name_1_tag = "---";
                }
                else
                {
                    if (i == 0)
                    {
                        Program.I().ocgcore.name_0 = roomPlayers[i].name;
                    }
                    if (i == 1)
                    {
                        Program.I().ocgcore.name_0_tag = roomPlayers[i].name;
                    }
                    if (i == 2)
                    {
                        Program.I().ocgcore.name_1 = roomPlayers[i].name;
                    }
                    if (i == 3)
                    {
                        Program.I().ocgcore.name_1_tag = roomPlayers[i].name;
                    }
                }
            }
        }
    }

    lazyRoom lazyRoom = null;
    void ini()
    {
        // 「测试直开局」是一次性的：进房就消费掉，避免影响之后正常的联机对局
        quickThisDuel = quickStart;
        quickStart = false;
        // 「撤回重开」同样是一次性的，来源是 DuelUndo（它管的是「哪一条入口重开的」）。
        undoRestartThisDuel = DuelUndo.replayingPremove;
        DuelUndo.replayingPremove = false;
        autoStartThisDuel = quickThisDuel || undoRestartThisDuel;
        quickHandAnswered = false;
        quickFlowStarted = false;
        quickDuelStarted = false;
        QuickTestTrace.Log("ini", "quickThisDuel=" + quickThisDuel
            + " undoRestart=" + undoRestartThisDuel
            + " autoStart=" + autoStartThisDuel);
        for (int i = 0; i < 4; i++)
        {
            roomPlayers[i] = null;
        }
        if (gameObject != null)
        {
            ES_quit();
        }
        MonoBehaviour.DestroyImmediate(gameObject);
        if (mode == 2)
        {
            createWindow(Program.I().remaster_tagRoom);
        }
        else
        {
            createWindow(Program.I().remaster_room);
        }
        lazyRoom = gameObject.GetComponent<lazyRoom>();
        fixScreenProblem();
        superScrollView = gameObject.GetComponentInChildren<UIselectableList>();
        superScrollView.selectedAction = onSelected;
        superScrollView.install();
        printFile();
        superScrollView.selectedString = GameModeManager.DeckInUse;
        superScrollView.toTop();
        if (mode == 0)
        {
            UIHelper.trySetLableText(gameObject, "Rname_", InterString.Get("单局房间"));
        }
        if (mode == 1)
        {
            UIHelper.trySetLableText(gameObject, "Rname_", InterString.Get("比赛房间"));
        }
        if (mode == 2)
        {
            UIHelper.trySetLableText(gameObject, "Rname_", InterString.Get("双打房间"));
        }

        UIHelper.trySetLableText(gameObject, "description_", "");
        for (int i = 0; i < 4; i++) 
        {
            realPlayers[i] = UIHelper.getByName<lazyPlayer>(gameObject, i.ToString());
        }

        for (int i = 0; i < 4; i++)
        {
            realPlayers[i].ini();
            realPlayers[i].onKick = OnKick;
            realPlayers[i].onPrepareChanged = onPrepareChanged;
        }

        UIHelper.shiftButton(startButton(), false);
        UIHelper.registUIEventTriggerForClick(startButton().gameObject, listenerForClicked);
        UIHelper.registUIEventTriggerForClick(exitButton().gameObject, listenerForClicked);
        UIHelper.registUIEventTriggerForClick(duelistButton().gameObject, listenerForClicked);
        UIHelper.registUIEventTriggerForClick(observerButton().gameObject, listenerForClicked);
        UIHelper.registUIEventTriggerForClick(readyButton().gameObject, listenerForClicked);
        realize();
        superScrollView.refreshForOneFrame();
    }

    private void onPrepareChanged(int arg1, bool arg2)
    {
        if (roomPlayers[arg1] != null)
        {
            roomPlayers[arg1].prep = arg2;
        }
        if (arg2)
        {
            TcpHelper.CtosMessage_UpdateDeck(new YGOSharp.Deck(GameModeManager.DeckPath(GameModeManager.DeckInUse)));
            TcpHelper.CtosMessage_HsReady();
        }
        else
        {
            TcpHelper.CtosMessage_HsNotReady();
        }
    }

    private void OnKick(int pos)
    {
        TcpHelper.CtosMessage_HsKick(pos);
    }

    private UIButton startButton()
    {
        return UIHelper.getByName<UIButton>(gameObject, "start_");
    }

    private UIButton exitButton()
    {
        return UIHelper.getByName<UIButton>(gameObject, "exit_");
    }

    private UIButton duelistButton()
    {
        return UIHelper.getByName<UIButton>(gameObject, "duelist_");
    }

    private UIButton observerButton()
    {
        return UIHelper.getByName<UIButton>(gameObject, "observer_");
    }

    private UIButton readyButton()
    {
        return UIHelper.getByName<UIButton>(gameObject, "ready_");
    }

    void listenerForClicked(GameObject gameObjectListened)
    {
        if (gameObjectListened.name == "exit_")
        {
            Program.I().ocgcore.onExit();
        }
        if (gameObjectListened.name == "ready_")
        {
            if (selftype < realPlayers.Length && realPlayers[selftype] != null)
            {
                if (realPlayers[selftype].getIfPreped())
                {
                    TcpHelper.CtosMessage_HsNotReady();
                }
                else
                {
                    TcpHelper.CtosMessage_UpdateDeck(new YGOSharp.Deck(GameModeManager.DeckPath(GameModeManager.DeckInUse)));
                    TcpHelper.CtosMessage_HsReady();
                }
            }
        }
        if (gameObjectListened.name == "duelist_")
        {
            TcpHelper.CtosMessage_HsToDuelist();
        }
        if (gameObjectListened.name == "observer_")
        {
            TcpHelper.CtosMessage_HsToObserver();
        }
        if (gameObjectListened.name == "start_")
        {
            TcpHelper.CtosMessage_HsStart();
        }
    }

    #endregion

    #region 验收通道：无人值守进房（qt_autojoin.on）

    /// <summary>下一次允许自动点的时间（节流用）。</summary>
    private int autoJoinAtMs = 0;

    /// <summary>
    /// `log/qt_autojoin.on` 是否在生效。双重门槛：必须 `qt_debug.on` 存在才可能为真，
    /// 正式包零影响（<see cref="QuickTestTrace.Enabled"/> 只判一次 File.Exists；
    /// 正式包下它缓存为 false ⇒ `&amp;&amp;` 直接短路，第二项根本不会被求值）。
    ///
    /// ⚠ 本函数在 `preFrameFunction` 里被**每帧**调用，所以第二项不能是裸 `File.Exists`：
    /// `SwitchOn` 自带「它不在」方向的 500ms 节流（见 `QuickTestTrace.SwitchPollMs`）
    /// —— 实测裸 File.Exists 是 17us/次，每帧一次就吃掉一帧预算的 0.1%（60fps）。
    /// </summary>
    bool autoJoinOn()
    {
        return QuickTestTrace.Enabled
            && QuickTestTrace.SwitchOn("qt_autojoin.on");
    }

    /// <summary>
    /// 验收/排查用：`log/qt_autojoin.on` 存在时，替玩家在房间界面点「决斗准备」→「开始游戏」。
    ///
    /// 为什么需要它：主菜单「人机对战」这条路径必须先在房间界面点两颗按钮才能进决斗场，
    /// 而验收脚本没法可靠点中它们（窗口位置、分辨率、按钮排布都会变）。这里只是把
    /// 「点击」这一段换成确定性的发包，**不代替玩家做对局里的任何选择**，
    /// 真实对局内容仍由服务器/机器人给。
    ///
    /// 猜拳 / 先后攻那两个弹窗也一并自动答（见 StocMessage_SelectHand / SelectTp）——
    /// 它们是同一个「进决斗场之前必须点掉」的环节，手点不了的话整条路径仍然跑不到底。
    /// </summary>
    public override void preFrameFunction()
    {
        base.preFrameFunction();
        if (!isShowed || autoStartThisDuel || !autoJoinOn())
        {
            return;
        }
        if (Program.TimePassed() < autoJoinAtMs)
        {
            return;
        }
        autoJoinAtMs = Program.TimePassed() + 1000;

        bool mePreped = selftype < realPlayers.Length && realPlayers[selftype] != null
            && realPlayers[selftype].getIfPreped();
        if (!mePreped)
        {
            QuickTestTrace.Log("autojoin", "send UpdateDeck + HsReady (selftype=" + selftype + ")");
            TcpHelper.CtosMessage_UpdateDeck(new YGOSharp.Deck(GameModeManager.DeckPath(GameModeManager.DeckInUse)));
            TcpHelper.CtosMessage_HsReady();
            return;
        }
        // 抢跑的 HsStart 会被服务器丢掉（对手还没就绪时），所以按节拍重发即可，
        // 不需要自己去猜「双方都准备好了没有」。
        if (is_host)
        {
            QuickTestTrace.Log("autojoin", "send HsStart");
            TcpHelper.CtosMessage_HsStart();
        }
    }

    #endregion

}
