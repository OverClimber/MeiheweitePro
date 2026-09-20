using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

/// <summary>
/// 对局时间线：把「重建一局所需的全部输入」录下来，供人机对局的「撤回」重放。
///
/// 为什么需要它
/// ────────────
/// ocgcore 跑在独立的 AI.Server.exe 进程里，**没有 rewind 接口**，撤回只能
/// 「用同一颗种子重开一局，把已经发生过的输入原样喂回去」。一局对局的输入只有三类：
///
///   1. 洗牌 / 掷骰 / 效果随机 —— 由**种子**决定（AI.Server 的 argv[13] 是 pre_seed，
///      pre_seed&gt;0 时它不再调 rd()）。种子在 <see cref="NoteOpen"/> 里记。
///   2. 我方每一次应答 —— 唯一出口是 Ocgcore.sendReturn -&gt; Room.handler
///      -&gt; TcpHelper.CtosMessage_Response，见 <see cref="NoteResponse"/>。
///   3. 对手（WindBot）每一次应答 —— **无法录制**：它是独立进程，字节流不出现在我们这边；
///      而且它的决策路径会用自己那支按时间播种的随机数
///      （Program.Rand = new Random()，见 AIUtil.ShuffleListInPlace / Executor.OnSelectCard 等），
///      本身就不可复现。
///
/// 所以这里**不假装**对手可复现，而是把「我们看到的入站消息流」也录下来，重放时由它驱动：
/// 边喂边比，一旦入站流与录制不符，说明对手走了另一条线，就地停止代打、把控制权交还玩家
/// （详见撤回重放器）。这样
///   ・没分叉（多数情况）→ 玩家拿到的盘面与按下撤回前逐条一致；
///   ・分叉了           → 玩家拿到的是「同一副起手、同一次洗牌」的另一条线，
///                        绝不会把录下来的旧字节当新选择瞎按（那是位置型数据，
///                        盘面一变就指向别的东西，会打出玩家没做过的操作）。
///
/// 生命周期
/// ────────
/// 只在人机对局里录制（AIRoom.launch 开、killServerProcess 关），整条链路只活在内存里 ——
/// 撤回发生在同一局游戏进程内，不需要落盘。排查时由 <see cref="Dump"/> 按需写文件。
/// </summary>
public static class DuelTimeline
{
    /// <summary>
    /// 一条入站的**游戏消息**（不是 TCP 包 —— 一个 GameMsg 包里可能塞了好几条消息，
    /// 而 Ocgcore 是一条一条跨帧处理的，所以粒度必须按消息算；否则「第几条之后该代发应答」
    /// 会算在消息还没被处理的时候，代发出的就是个错位的应答）。
    /// </summary>
    public class Inbound
    {
        /// <summary>GameMessage 枚举值。</summary>
        public int fuction;

        /// <summary>消息负载原始字节。</summary>
        public byte[] data;

        /// <summary>到这条消息为止的链式摘要。</summary>
        public string digest;
    }

    /// <summary>一次「我方应答」的记录。</summary>
    public class Decision
    {
        /// <summary>触发这次应答的游戏消息（Ocgcore.currentMessage）。</summary>
        public int message;

        /// <summary>我方实际发出的应答字节。</summary>
        public byte[] response;

        /// <summary>
        /// 发出这次应答时，已经处理过的游戏消息条数（含触发它的那一条）。
        /// 重放时用它定位「第几条消息之后该把这一应答代发出去」。
        /// </summary>
        public int inboundCount;

        /// <summary>发出这次应答时的链式摘要（用于分叉检测）。</summary>
        public string digestAtResponse;

        /// <summary>
        /// 这次应答是不是**玩家点出来的**。
        ///
        /// 撤回只认人工决策点（口径同 duel-undo-mod 的 Origin::Manual / Automatic）：
        /// 客户端会在「玩家根本没得选」时自动替玩家应答 —— smartSelect 的强制代选
        /// （<c>autoSendCards</c>）、只有一个选项的 SelectOption 等，它们也走 sendReturn。
        /// 若把它们也算成撤回点，玩家按撤回会退到「自己没动过手」的位置，
        /// 而那里又会立刻被同一段代码自动答掉，看起来就是「按了没反应」。
        /// </summary>
        public bool manual = true;
    }

    /// <summary>是否正在录制（仅人机对局）。</summary>
    public static bool active = false;

    /// <summary>
    /// 本局是不是从**卡组界面「测试」**发起的（= `Room.quickThisDuel` 的来源）。
    ///
    /// 撤回重开必须还原回同一个入口：卡组测试局重开后仍走测试直开局、结束后回卡组界面；
    /// 主菜单「人机对战」重开后仍走房间那套自动流程、结束后回人机界面。
    /// 两者共用 <see cref="AIRoom.launch"/>，靠这一个标记区分。
    /// </summary>
    public static bool quickTest = false;

    /// <summary>
    /// 一份卡组（Main / Extra / Side 的 code 列表）的副本。
    ///
    /// 为什么要副本：`TcpHelper.deck` 是 static、会被下一局覆盖，而撤回重开要发的必须是
    /// **当初那一份**。靠「卡组名 + 回读 ydk 文件」不可靠 —— 主菜单人机对战里玩家可以在
    /// 房间界面临时换一副（`Room.onSelected` 只改 Config），文件本身也可能被改过。
    /// 卡组差一张，摸牌就分叉，撤回当场失效。所以直接把 code 列表摘一份留着。
    /// </summary>
    public class DeckCodes
    {
        public List<int> main = new List<int>();
        public List<int> extra = new List<int>();
        public List<int> side = new List<int>();

        public static DeckCodes From(YGOSharp.Deck d)
        {
            DeckCodes c = new DeckCodes();
            if (d == null)
            {
                return c;
            }
            if (d.Main != null)
            {
                c.main.AddRange(d.Main);
            }
            if (d.Extra != null)
            {
                c.extra.AddRange(d.Extra);
            }
            if (d.Side != null)
            {
                c.side.AddRange(d.Side);
            }
            return c;
        }

        public YGOSharp.Deck Build()
        {
            YGOSharp.Deck d = new YGOSharp.Deck();
            for (int i = 0; i < main.Count; i++)
            {
                d.Main.Add(main[i]);
            }
            for (int i = 0; i < extra.Count; i++)
            {
                d.Extra.Add(extra[i]);
            }
            for (int i = 0; i < side.Count; i++)
            {
                d.Side.Add(side[i]);
            }
            return d;
        }

        public bool IsEmpty()
        {
            return main.Count == 0 && extra.Count == 0 && side.Count == 0;
        }

        public string Brief()
        {
            return "main=" + main.Count + " extra=" + extra.Count + " side=" + side.Count;
        }
    }

    /// <summary>
    /// 本局**开局时**发出去的那份卡组（撤回重开照原样再发一次）。
    ///
    /// 只记第一次：一局中途的换备（ChangeSide）也会发 UpdateDeck，但重开要还原的是
    /// **开局前**的握手，换备后的那一份与「重开到开局」无关。
    /// </summary>
    public static DeckCodes sentDeck = null;

    /// <summary>
    /// 猜拳应答，按发生顺序（1 = 剪刀 / 2 = 石头 / 3 = 布）。
    ///
    /// 卡组测试局里它是常量（`Room.QuickStartHand`），但主菜单人机对战里是**玩家点出来的** ——
    /// 不录下来的话重开时会随机再出一次（服务器那头 AI 被 Hand=1 锁成剪刀）：赢了变平局、
    /// 或者先手换了人，整条入站流从第 1 条就分叉，撤回等于当场失效。
    /// </summary>
    public static readonly List<int> handAnswers = new List<int>();

    /// <summary>先后攻应答，按发生顺序（赢下猜拳的那一方才有）。撤回重开要逐次照答。</summary>
    public static readonly List<bool> tpAnswers = new List<bool>();

    /// <summary>
    /// **对手**在猜拳里出的手，按发生顺序（1 = 剪刀 / 2 = 石头 / 3 = 布）。
    ///
    /// 为什么连对手的也要记：猜拳的胜负直接写进第一条 `GameMessage.Start` 的
    /// `playertype`（`isFirst` 就是从那一个字节读出来的），先手换了人，整条入站流
    /// 从第 0 条就摘要不符。
    ///
    /// 而主菜单「人机对战」的 `锁定手牌` 默认**是关的**：WindBot 命令行里没有 `Hand=`，
    /// 它的猜拳是它自己随便出的 —— 光把**我方**那一手录下来重答，等于把胜负重新掷一次骰子
    /// （1/3 相同、1/3 平局、1/3 反过来），撤回在多数情况下当场分叉。
    /// 记下对手出的手，重开时用 `Hand=&lt;它&gt;` 把它也钉回同一个值（见 AIRoom.forcedBotHand）。
    /// </summary>
    public static readonly List<int> rpsBotAnswers = new List<int>();

    /// <summary>
    /// 这一局**我方是不是先手** —— 直接取自第一条 `GameMessage.Start` 的 `playertype`
    /// （`Ocgcore` 收到它时算出 `isFirst`：`((playertype & 0xf) > 0) ? false : true`）。
    ///
    /// 🔑 撤回重开要钉的就是**这一个布尔**，而不是「我们当初点了先攻还是后攻」：
    /// 猜拳只有**赢的那一方**才拿到先后攻选择权。机器人赢猜拳时我们根本收不到那个询问，
    /// 也就没有「我方那一手」可录（`tpAnswers` 是空的，退回默认值 = 把先手重掷一次骰子）。
    /// 只有 Start 里这一个字节在两种情形下都是权威真值，所以它才是判据。
    /// </summary>
    public static bool startFirst = true;

    // ── 重建一局所需的开局参数 ────────────────────────────────────────────────
    public static uint seed = 0;

    /// <summary>WindBot 命令行**模板**，不含 Port=（端口每次启动都不一样）。</summary>
    public static string botCommand = "";

    public static string deckName = "";
    public static bool lockHand = true;
    public static bool noCheck = false;
    public static bool noShuffle = false;

    /// <summary>我方的全部应答，按发生顺序。</summary>
    public static readonly List<Decision> decisions = new List<Decision>();

    /// <summary>入站游戏消息，按处理顺序（重放的驱动程序）。</summary>
    public static readonly List<Inbound> inbound = new List<Inbound>();

    /// <summary>
    /// 入站消息流的链式摘要：digest_i = SHA1(digest_{i-1} || fuction || data_i)。
    /// 用链式而不是「每包把历史全量重算」，是因为一局下来有几千条消息，
    /// 全量重算是 O(n²)；链式每条只算一小段。两遍跑的链值可以逐条直接比对。
    /// </summary>
    public static string digest = "";

    /// <summary>Dump() 的序号（不随 Reset 归零：同一进程内多次 dump 不能互相覆盖）。</summary>
    private static int dumpSeq = 0;

    /// <summary>
    /// 开一局新的人机对局：清空上一条时间线并记下重建所需的参数。
    /// 由 AIRoom.launch() 调用 —— 那条路径上种子、机器人命令行都是刚生成的，
    /// 且此处 command 尚未拼上 Port=。
    /// </summary>
    public static void NoteOpen(uint seedForDuel, string botCommandForDuel, string deckNameForDuel,
        bool lockHandForDuel, bool noCheckForDuel, bool noShuffleForDuel, bool quickTestForDuel)
    {
        Reset();
        active = true;
        seed = seedForDuel;
        botCommand = botCommandForDuel ?? "";
        deckName = deckNameForDuel ?? "";
        lockHand = lockHandForDuel;
        noCheck = noCheckForDuel;
        noShuffle = noShuffleForDuel;
        quickTest = quickTestForDuel;
        QuickTestTrace.Log("tl", "open seed=" + seed + " deck=" + deckName
            + " lockHand=" + lockHand + " noCheck=" + noCheck + " noShuffle=" + noShuffle
            + " quickTest=" + quickTest
            + " bot=" + botCommand);
    }

    /// <summary>
    /// 开局前把卡组发给服务器时调一次（见 TcpHelper.CtosMessage_UpdateDeck）。
    /// 只记第一次 —— 重开要还原的是开局前那一份，中途换备发出的不算。
    /// </summary>
    public static void NoteDeckSent(YGOSharp.Deck d)
    {
        if (!active || sentDeck != null || d == null)
        {
            return;
        }
        sentDeck = DeckCodes.From(d);
        QuickTestTrace.Log("tl", "deck sent " + sentDeck.Brief());
    }

    /// <summary>我方一次猜拳应答（见 TcpHelper.CtosMessage_HandResult）。</summary>
    public static void NoteHandAnswer(int res)
    {
        if (!active)
        {
            return;
        }
        handAnswers.Add(res);
        QuickTestTrace.Log("tl", "hand answer #" + (handAnswers.Count - 1) + " res=" + res);
    }

    /// <summary>我方一次先后攻应答（见 TcpHelper.CtosMessage_TpResult）。</summary>
    public static void NoteTpAnswer(bool first)
    {
        if (!active)
        {
            return;
        }
        tpAnswers.Add(first);
        QuickTestTrace.Log("tl", "tp answer #" + (tpAnswers.Count - 1) + " first=" + first);
    }

    /// <summary>
    /// 一轮猜拳的结果（见 Room.StocMessage_HandResult）。只记对手那一手 ——
    /// 我方那一手已经在 <see cref="NoteHandAnswer"/> 里记过，这里是服务器回显的权威值。
    /// </summary>
    public static void NoteRpsResult(int me, int op)
    {
        if (!active)
        {
            return;
        }
        rpsBotAnswers.Add(op);
        QuickTestTrace.Log("tl", "rps #" + (rpsBotAnswers.Count - 1) + " me=" + me + " op=" + op);
    }

    /// <summary>我方是否先手（见 <see cref="startFirst"/>）。由 Ocgcore 解析 Start 时调用。</summary>
    public static void NoteStartFirst(bool first)
    {
        if (!active)
        {
            return;
        }
        startFirst = first;
        QuickTestTrace.Log("tl", "start first=" + first);
    }

    /// <summary>对局收尾（onExit / 杀进程）时停录。</summary>
    public static void End()
    {
        if (!active)
        {
            return;
        }
        active = false;
        QuickTestTrace.Log("tl", "end decisions=" + decisions.Count + " inbound=" + inbound.Count);
        Dump();
    }

    public static void Reset()
    {
        decisions.Clear();
        inbound.Clear();
        digest = "";
        seed = 0;
        botCommand = "";
        deckName = "";
        quickTest = false;
        sentDeck = null;
        handAnswers.Clear();
        tpAnswers.Clear();
        rpsBotAnswers.Clear();
        startFirst = true;
    }

    /// <summary>
    /// 入站游戏消息（Ocgcore.sibyl() 里，过了「重要消息的动画节流」之后、
    /// 逻辑/表现处理之前调用 —— 位置见那里的注释，早了会重复计、晚了数据已被读走）。
    ///
    /// 只收游戏消息，不收握手类包（JoinGame / HsReady / HandResult 等）—— 那些不含对局内容，
    /// 且部分字段两遍跑必然不同，收进来只会干扰比对。猜拳与先后攻在测试局里是常量应答
    /// （Room.QuickStartHand / QuickStartGoFirst），重放时会由同一段代码再答一次，不需要录。
    /// </summary>
    public static void NoteGameMessage(int fuction, byte[] data)
    {
        if (!active)
        {
            return;
        }
        // 撤回重开后、新一局 Start 之前飘来的残留消息也不能录：合成包（forceMSquit 造的
        // sibyl_quit）绕过 addPackage 直接进 Packages，走的是 sibyl() 这条录制路径，
        // 漏记这一道同样会把新时间线的下标推后、让比对在第 0 条就判不一致。
        if (DuelUndo.DropStalePreStart(fuction))
        {
            return;
        }
        Inbound item = new Inbound();
        item.fuction = fuction;
        byte[] copy = new byte[data == null ? 0 : data.Length];
        if (data != null)
        {
            Array.Copy(data, copy, data.Length);
        }
        item.data = copy;

        byte[] prev = digest.Length == 0 ? new byte[0] : Encoding.ASCII.GetBytes(digest);
        byte[] combined = new byte[prev.Length + 4 + copy.Length];
        Array.Copy(prev, 0, combined, 0, prev.Length);
        combined[prev.Length] = (byte)(fuction & 0xff);
        combined[prev.Length + 1] = (byte)((fuction >> 8) & 0xff);
        combined[prev.Length + 2] = (byte)((fuction >> 16) & 0xff);
        combined[prev.Length + 3] = (byte)((fuction >> 24) & 0xff);
        Array.Copy(copy, 0, combined, prev.Length + 4, copy.Length);

        digest = Hash(combined);
        item.digest = digest;
        inbound.Add(item);
    }

    /// <summary>
    /// 我方一次应答（Ocgcore.sendReturn 里、clearResponse() 之前调用 ——
    /// 那时 currentMessage 还是触发这次应答的那条消息）。
    /// </summary>
    /// <param name="manual">是否玩家点的（false = 客户端自动代答，见 <see cref="Decision.manual"/>）。</param>
    public static void NoteResponse(int message, byte[] response, bool manual)
    {
        if (!active)
        {
            return;
        }
        // 撤回的本地回溯会把录下来的历史在客户端重放一遍，那段重放不是真实对局：
        // 记进来的话，新一局的时间线会在开头就多出一堆重复决策点。
        if (DuelUndo.rebuilding)
        {
            return;
        }
        Decision d = new Decision();
        d.message = message;
        byte[] copy = new byte[response == null ? 0 : response.Length];
        if (response != null)
        {
            Array.Copy(response, copy, response.Length);
        }
        d.response = copy;
        d.inboundCount = inbound.Count;
        d.digestAtResponse = digest;
        d.manual = manual;
        decisions.Add(d);
        QuickTestTrace.Log("tl", "resp #" + (decisions.Count - 1)
            + " msg=" + (YGOSharp.OCGWrapper.Enums.GameMessage)message
            + " len=" + copy.Length
            + " inbound=" + d.inboundCount
            + " manual=" + manual
            + " sha=" + Hash(copy));
    }

    /// <summary>
    /// 最后一个「玩家点的」决策点的下标；没有则返回 -1。
    /// 撤回退的就是它 —— 它之后的自动代答不构成撤回点，也随它一起被丢弃。
    /// </summary>
    public static int LastManualIndex()
    {
        for (int i = decisions.Count - 1; i >= 0; i--)
        {
            if (decisions[i].manual)
            {
                return i;
            }
        }
        return -1;
    }

    /// <summary>
    /// 时间线快照。
    ///
    /// 撤回要先「拆掉当前对局再重开」，而拆分走的 onExit() 会调 killServerProcess() →
    /// <see cref="End"/>()，把当前这条时间线清掉/封存。所以必须在拆之前把这一局摘出来。
    /// Decision / Inbound 生成之后就不再改动，所以浅拷贝列表即可。
    /// </summary>
    public class Snapshot
    {
        public uint seed;
        public string botCommand;
        public string deckName;
        public bool lockHand;
        public bool noCheck;
        public bool noShuffle;
        public bool quickTest;
        public List<Decision> decisions;
        public List<Inbound> inbound;

        /// <summary>开局前握手：发出去的那份卡组 + 猜拳/先后攻的应答（见各自注释）。</summary>
        public DeckCodes deck;
        public List<int> handAnswers;
        public List<bool> tpAnswers;
        public List<int> rpsBotAnswers;

        /// <summary>原局的先手归属（见 <see cref="DuelTimeline.startFirst"/>）—— 重开要钉的就是它。</summary>
        public bool startFirst = true;

        /// <summary>
        /// 重开时要发给 WindBot 的 `Hand=`，用来把对手的猜拳**钉成我方第一手必胜的那一张**；0 = 不用发。
        ///
        /// 🔑 为什么不是「钉回它原来出的那一手」：那只保证**第一轮**一样。一旦原局是平局
        /// （猜拳打了不止一轮，约 1/3 概率），第二轮对手仍是随机的 ⇒ 整条握手序列重现不了
        /// （实测约 1/9），两次重试也才 ~30%，多半落到「停在分叉点」——而握手期的分叉意味着
        /// 本地重建毫无意义（客户端停在空盘面），等于把玩家卡死。
        ///
        /// 改钉「我方第一手必胜」那一张 ⇒ 第一轮**必定分胜负**、只打一轮，且先手归我方；
        /// 于是先手改由**我方的先后攻应答**决定，而那是我们照原局真值重答的（见
        /// <see cref="DuelUndo.ReplayGoFirst"/>）⇒ 完全确定，一次成功、不需要重试。
        /// 这与「卡组测试局」用的是同一套必胜猜拳（那条路靠命令行 `Hand=1` 钉死）。
        ///
        /// ⚠ `lockHand` 为真时返回 0：命令行里已经有 `Hand=1` 了，再发一个是**重复键**，
        /// WindBot 的 Config.LoadArgs 会直接抛异常、机器人启动即崩。那一手已经是「我方必胜」。
        ///
        /// 编码：1 = 剪刀 / 2 = 石头 / 3 = 布（石头砸剪刀、剪刀剪布、布包石头）。
        /// </summary>
        public int BotHandOverride(bool lockHandForDuel)
        {
            if (lockHandForDuel)
            {
                return 0;
            }
            // 没录到我方第一手（异常路径）时按「出石头」算，与测试局常量同口径。
            int mine = (handAnswers != null && handAnswers.Count > 0) ? handAnswers[0] : 2;
            if (mine < 1 || mine > 3)
            {
                mine = 2;
            }
            return mine == 1 ? 3 : (mine == 2 ? 1 : 2);
        }

        /// <summary>重开时要发的卡组；没录到（异常情况）返回 null，调用方回退到按卡组名读文件。</summary>
        public YGOSharp.Deck BuildDeck()
        {
            return (deck == null || deck.IsEmpty()) ? null : deck.Build();
        }

        /// <summary>重开时第 i 次猜拳该答什么；越界返回 0（调用方回退到测试局常量）。</summary>
        public int HandAnswerAt(int i)
        {
            if (handAnswers == null || i < 0 || i >= handAnswers.Count)
            {
                return 0;
            }
            return handAnswers[i];
        }

        /// <summary>
        /// 把快照里的第 i 条入站消息还原成一个可以直接喂给 Ocgcore 的包。
        ///
        /// 撤回按下那一刻，客户端的「模型」要按这条流重建一次（见撤回器的本地回溯）——
        /// 那时现场只剩快照（实时列表会被重开清掉），所以必须能从字节重建包。
        /// 重建出来的包与当初收到的那个逐字节相同（fuction + 原样负载）。
        ///
        /// 放在 Snapshot 上而不是 DuelTimeline 上，是因为调用方只有「拿快照重建」这一处
        /// （Ocgcore.rebuildFromSnapshot）；放进 DuelTimeline 会让静态类多出一个实例成员。
        /// </summary>
        public Package MakePackage(int i)
        {
            Inbound item = inbound[i];
            Package p = new Package();
            p.Fuction = item.fuction;
            p.Data = new BinaryMaster(item.data);
            return p;
        }
    }

    /// <summary>把当前时间线整体摘出来（见 <see cref="Snapshot"/>）。</summary>
    public static Snapshot Take()
    {
        Snapshot s = new Snapshot();
        s.seed = seed;
        s.botCommand = botCommand;
        s.deckName = deckName;
        s.lockHand = lockHand;
        s.noCheck = noCheck;
        s.noShuffle = noShuffle;
        s.quickTest = quickTest;
        s.decisions = new List<Decision>(decisions);
        s.inbound = new List<Inbound>(inbound);
        s.deck = sentDeck;
        s.handAnswers = new List<int>(handAnswers);
        s.tpAnswers = new List<bool>(tpAnswers);
        s.rpsBotAnswers = new List<int>(rpsBotAnswers);
        s.startFirst = startFirst;
        return s;
    }

    /// <summary>
    /// 排查用留档：只在 log/qt_debug.on 存在时写 log/duel_timeline_&lt;pid&gt;_&lt;序号&gt;.txt。
    /// 纯文本、给人看的，不打算被程序读回。
    ///
    /// 带序号是因为「撤回」会在同一进程里重开一局并再 dump 一次 ——
    /// 文件名固定的话第二份会把原局那份盖掉，而这两份的逐条比对正是撤回的验收依据。
    /// </summary>
    public static void Dump()
    {
        if (!QuickTestTrace.Enabled)
        {
            return;
        }
        try
        {
            string path = QuickTestTrace.LogPath(
                "duel_timeline_" + System.Diagnostics.Process.GetCurrentProcess().Id
                + "_" + (++dumpSeq) + ".txt");
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("# seed=" + seed + " deck=" + deckName
                + " lockHand=" + lockHand + " noCheck=" + noCheck + " noShuffle=" + noShuffle
                + " quickTest=" + quickTest);
            sb.AppendLine("# bot=" + botCommand);
            sb.AppendLine("# deckSent=" + (sentDeck == null ? "none" : sentDeck.Brief())
                + " hand=" + string.Join(",", handAnswers.ConvertAll(x => x.ToString()).ToArray())
                + " tp=" + string.Join(",", tpAnswers.ConvertAll(x => x ? "first" : "second").ToArray())
                + " rpsBot=" + string.Join(",", rpsBotAnswers.ConvertAll(x => x.ToString()).ToArray())
                + " first=" + (startFirst ? "first" : "second"));
            int di = 0;
            for (int i = 0; i < inbound.Count; i++)
            {
                sb.AppendLine("in  #" + i + " msg=" + (YGOSharp.OCGWrapper.Enums.GameMessage)inbound[i].fuction
                    + " len=" + inbound[i].data.Length + " sha=" + Hash(inbound[i].data));
                while (di < decisions.Count && decisions[di].inboundCount == i + 1)
                {
                    Decision d = decisions[di];
                    sb.AppendLine("out #" + di + " msg=" + (YGOSharp.OCGWrapper.Enums.GameMessage)d.message
                        + " manual=" + d.manual
                        + " len=" + d.response.Length + " sha=" + Hash(d.response)
                        + " hex=" + Hex(d.response));
                    di++;
                }
            }
            File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
            QuickTestTrace.Log("tl", "dump -> " + path + " inbound=" + inbound.Count
                + " decisions=" + decisions.Count);
        }
        catch (Exception e)
        {
            QuickTestTrace.Log("tl", "dump FAILED: " + e.Message);
        }
    }

    /// <summary>SHA1 前 6 字节的十六进制（与 QuickTestTrace.Hash 同口径，便于直接对照日志）。</summary>
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
                StringBuilder sb = new StringBuilder();
                for (int i = 0; i < 6 && i < h.Length; i++)
                {
                    sb.Append(h[i].ToString("x2"));
                }
                return sb.ToString();
            }
        }
        catch (Exception)
        {
            return "-";
        }
    }

    public static string Hex(byte[] data)
    {
        if (data == null)
        {
            return "";
        }
        StringBuilder sb = new StringBuilder();
        for (int i = 0; i < data.Length; i++)
        {
            sb.Append(data[i].ToString("x2"));
        }
        return sb.ToString();
    }
}
