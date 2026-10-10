using UnityEngine;
using System;
using System.Collections.Generic;
using YGOSharp.OCGWrapper.Enums;

/// <summary>
/// 「空格键锁定卡牌简介」——需求 2026-10-09 第 2 条的**唯一状态机**。
///
/// <para><b>要做什么</b>：把光标指到一张「能弹简介」的卡上按**空格**，就把这张卡的简介
/// <b>钉住</b>（贴白色高亮 + 简介不再被别的卡顶掉）；再按一次空格（或光标不在卡上按空格）解除；
/// 按在另一张卡上则**切换**。</para>
///
/// <para><b>为什么是空格</b>（全项目按键占用盘点，2026-10-09）：
/// 左键在三个场景都被占用（编辑器 <c>beginDrag()</c> 拿卡拖拽 / 决斗 <c>ES_cardClicked</c>
/// <b>可能真把应答发给内核</b> / 列表行加卡），右键 = 全局关闭·取消，
/// 中键与 Tab 都被 <c>Program.I().book</c> 与 <c>onClickDetail()</c> 占着
/// （<c>Ocgcore.cs</c> 与 <c>DeckManager.cs</c> 各一处）—— <b>空格全项目 0 处游戏绑定</b>。
/// ⛔ 唯一要避开的场合是文本输入框（检索框/聊天框里空格是正常字符），判据用
/// NGUI 自带的 <c>UIInput.selection != null</c>，见 <see cref="TextInputActive"/>。</para>
///
/// <para><b>高亮（2026-10-09 第三版。用户口径：「选中不用现在这个框了，改成贴一层白色半透明的
/// 锁状图标（右侧的话就显示在行中间那里），边缘也围起来一圈白」，第三轮又补了一句
/// 「周遭一圈的特效要换成白色的」）</b>：3D 卡 = 一圈白（<see cref="AddLockRing"/>，自画贴图）
/// + 卡面正中一枚白色半透明锁图标（<see cref="AddLockIcon"/>）；
/// 检索列表 = 整条四边围一圈白 + 行中间一枚同样的锁图标。
/// <b>这两样要用的图都不是美术资源，而是运行期算出来的</b>（<see cref="RingTexture"/> /
/// <see cref="LockIconTexture"/> / <see cref="WhitePixel"/>）——
/// ⛔ 桌面线是「冻结播放器 + 只换 DLL」（<c>main.unity</c> 自 09-15、<c>sharedassets0.assets</c>
/// 自 09-20 未动），新贴图进不了包；而现有美术里**根本没有锁**（全工程 <c>*lock*</c> 只有 DemoLib
/// 编辑器图标）。同理「蓝框」<c>bule_kuang.prefab</c> 全工程 0 引用 ⇒ 被 Unity 剔除、
/// 不在构建产物里（实测 <c>sharedassets0.assets</c> 里搜不到 <c>bule_kuang</c>）——
/// ⚠ 但**现成的那几个框也不能拿来染白**（四种颜色的差别烘在贴图里，材质色本来就是白的，
/// 见 <see cref="AddLockRing"/> 头注：第二版就是这么栽的）。</para>
///
/// <para><b>高亮为什么是「轮询」而不是「显式开关」</b>：三个宿主（<see cref="gameCard"/>、
/// <see cref="MonoCardInDeckManager"/>、<see cref="cardPicLoader"/>）都是**对象池**里的实例，
/// 会被回收去装别的卡。轮询式判据 <c>FrameOn/RowOn</c> 里带上了「这张卡/这一行**现在**的
/// id 必须还是被锁的那个」这一条 ⇒ 回收后自己就灭了，不需要谁去记得撤销。</para>
///
/// <para><b>为什么锁要「每帧抢回面板」（<see cref="EnsureDesc"/>）</b>：用户实测两条丢简介的路径
/// —— ① 决斗里点空白处：<c>Ocgcore.ES_mouseUpEmpty</c> 在「全屏游戏」档下会
/// <c>shiftCardShower(false)</c> 把说明面板收起、让位给「查看墓地/额外」那一览；
/// ② 卡换位置（特招）：卡面数据/尾巴被重刷，面板跟着变。压制 <c>setData</c> 只挡得住
/// 「别人来抢」，挡不住「面板自己收起来 / 被别的视图接管」⇒ 锁着时每帧补一次
/// （判据＝面板在亮 且 摆的正是被锁那张，见 <see cref="CardDescription.descVisible"/>）。</para>
/// </summary>
public static class CardDescLock
{
    // ───────────────────────── 配置 ─────────────────────────

    /// <summary>落盘键。OCG / RD **通用**一份（用户 2026-10-09 口径）。</summary>
    public const string ConfigKey = "descLock_";

    /// <summary>功能档位。数值就是落盘值。</summary>
    public enum Mode
    {
        Off = 0,
        DeckOnly = 1,
        DuelOnly = 2,
        All = 3,
    }

    public const Mode ModeDefault = Mode.All;

    /// <summary>下拉框的**显示顺序**（用户口径：全部 / 仅卡组编辑 / 仅决斗中 / 关闭）。</summary>
    static readonly Mode[] ModeOrder = { Mode.All, Mode.DeckOnly, Mode.DuelOnly, Mode.Off };

    static readonly string[] ModeLabels = { "关闭", "仅卡组编辑", "仅决斗中", "全部" };

    public static int ModeCount { get { return ModeOrder.Length; } }

    public static Mode ModeAt(int dropdownIndex)
    {
        if (dropdownIndex < 0 || dropdownIndex >= ModeOrder.Length)
        {
            return ModeDefault;
        }
        return ModeOrder[dropdownIndex];
    }

    public static int IndexOf(Mode m)
    {
        for (int i = 0; i < ModeOrder.Length; i++)
        {
            if (ModeOrder[i] == m)
            {
                return i;
            }
        }
        return IndexOf(ModeDefault);
    }

    public static string LabelOf(Mode m)
    {
        int i = (int)m;
        return (i >= 0 && i < ModeLabels.Length) ? ModeLabels[i] : ModeLabels[(int)ModeDefault];
    }

    public static Mode ParseLabel(string label)
    {
        for (int i = 0; i < ModeOrder.Length; i++)
        {
            if (ModeLabels[(int)ModeOrder[i]] == label)
            {
                return ModeOrder[i];
            }
        }
        return ModeDefault;
    }

    static Mode mode = ModeDefault;
    static bool modeLoaded = false;

    /// <summary>
    /// 惰性读盘：第一次被用到时从 Config 读一次。
    /// 这样调用方（设置页建行、每帧 Tick）都不用关心「谁先谁后」，
    /// 也不会出现「设置页还没建行就被 Tick 用到、拿默认值把玩家的选择盖掉」。
    /// </summary>
    static void EnsureLoaded()
    {
        if (modeLoaded)
        {
            return;
        }
        modeLoaded = true;
        LoadMode();
    }

    public static Mode Current { get { EnsureLoaded(); return mode; } }

    /// <summary>从 Config 读一次（惰性调用，也可由设置页初始化时显式调）。</summary>
    public static void LoadMode()
    {
        modeLoaded = true;
        int v;
        string raw = Config.Get(ConfigKey, ((int)ModeDefault).ToString());
        if (int.TryParse(raw, out v) && v >= 0 && v <= 3)
        {
            mode = (Mode)v;
        }
        else
        {
            mode = ModeDefault;
        }
        if (mode == Mode.Off)
        {
            Unlock();
        }
    }

    /// <summary>下拉框选了第 <paramref name="dropdownIndex"/> 项：改档位 + 落盘 + 清当前锁。</summary>
    public static void SetModeByIndex(int dropdownIndex)
    {
        mode = ModeAt(dropdownIndex);
        Config.Set(ConfigKey, ((int)mode).ToString());
        Unlock();
    }

    // ───────────────────────── 场景闸 ─────────────────────────

    /// <summary>当前所在场景（决斗 / 卡组编辑）。两者互斥，按 servant 的 isShowed 判。</summary>
    public static bool InDuel()
    {
        return Program.I() != null && Program.I().ocgcore != null && Program.I().ocgcore.isShowed;
    }

    /// <summary>卡组编辑侧：编辑器面板**或**「点链接弹出的检索窗」开着都算。</summary>
    public static bool InDeckEditor()
    {
        if (Program.I() == null)
        {
            return false;
        }
        bool deck = Program.I().deckManager != null && Program.I().deckManager.isShowed;
        bool search = Program.I().cardSearch != null && Program.I().cardSearch.isShowed;
        return deck || search;
    }

    /// <summary>当前档位允许在这个场景生效吗。</summary>
    public static bool SceneAllowed()
    {
        EnsureLoaded();
        if (mode == Mode.Off)
        {
            return false;
        }
        if (InDuel())
        {
            return mode == Mode.All || mode == Mode.DuelOnly;
        }
        if (InDeckEditor())
        {
            return mode == Mode.All || mode == Mode.DeckOnly;
        }
        return false;
    }

    /// <summary>NGUI 有输入框在编辑中 ⇒ 空格是正常字符，别当快捷键。</summary>
    public static bool TextInputActive()
    {
        return UIInput.selection != null;
    }

    // ───────────────────────── 锁状态 ─────────────────────────

    static bool locked = false;
    static int lockedId = 0;
    /// <summary>被锁的 3D 卡根物体（决斗 gameCard / 编辑器 MonoCardInDeckManager）。</summary>
    static GameObject lockedRoot = null;
    /// <summary>被锁的检索列表**条目行根**。</summary>
    static GameObject lockedRow = null;
    /// <summary>被锁那张的卡数据（三个宿主都往里存一份，用来刷简介）。</summary>
    static YGOSharp.Card lockedCard = null;
    /// <summary>宿主类别：1＝卡组编辑器桌面卡，2＝检索列表条目行，3＝决斗场 3D 卡。</summary>
    static int lockedKind = 0;

    /// <summary>
    /// 决斗 3D 卡的**实例锚**（只在 <see cref="lockedKind"/>==3 时有意义）。
    ///
    /// <para><b>⛔ 为什么必须有这个字段（2026-10-09 第四轮，实机取证得出）</b>：<c>gameCard</c>
    /// /<c>OCGobject</c> 是**纯 C# 类**（<c>public class OCGobject { public GameObject gameObject; }</c>），
    /// <b>不是 MonoBehaviour</b>。而 Unity 的泛型版 <c>GetComponent&lt;T&gt;()</c> **没有
    /// <c>where T : Component</c> 约束**（所以能编译过），对非 Component 类型**运行期恒返回 null**。
    /// 本轮第一版写的正是 <c>lockedRoot.GetComponent&lt;gameCard&gt;()</c> ⇒ 每帧第一次校验就判
    /// 「实例被销毁」⇒ 立刻解锁 ⇒ 用户实机症状「战斗里不能锁定了」（v2 靠按卡号重找的兜底
    /// <c>FindDuelCard</c> 掩盖了它，代价是同名卡乱亮）。</para>
    ///
    /// <para>本字段在 <see cref="Lock"/> 时由 <see cref="ResolveDuelCard"/> 按
    /// <b>GameObject 引用相等</b>从 <c>ocgcore.cards</c>（对象池，实例稳定）里查出并缓存；
    /// <see cref="ValidateHost"/> 每帧优先用它，锚对不上时再查一次。
    /// ⛔ <b>全程不按卡号找</b> —— 一副卡组有多张同号卡，按 Id 找回来的不是被锁那一个。</para>
    /// </summary>
    static gameCard lockedDuelCard = null;

    /// <summary>本轮锁是否已经记过「保住了」的日志（只记一次，避免刷屏）。</summary>
    static bool holdLogged = false;

    /// <summary>
    /// 决斗 3D 卡宿主「暂时看不见」的宽限帧数。卡换位置（特招 / 回牌组 / 进墓地）那几帧它会被
    /// <c>hide()</c>（<c>erase_data()</c> 把 <c>data.Id</c> 清成 0），但**同一个 gameCard 对象
    /// 还会回来**（<c>GCS_cardMove</c> 搬的就是它，见 <c>gameCard.set_code</c> 头注）⇒ 这段时间
    /// 不能判「卡没了」，先等着；等超了才解锁。约 3 秒（60fps 口径）。
    /// </summary>
    const int HostMissGrace = 180;

    /// <summary>宿主连续「暂时看不见」了多少帧（<see cref="HostMissGrace"/> 的计时器）。</summary>
    static int hostMissFrames = 0;

    /// <summary>
    /// 这次锁归属的**检索面板根**（<c>new_search_remaster</c> 的克隆体：卡组编辑器右侧那块搜索栏、
    /// 或点卡名弹出的检索窗）。只在 <see cref="lockedKind"/>==2 **且那一行确实长在某块检索面板下**
    /// 时才非 null。
    ///
    /// <para><b>为什么必须有它（用户 2026-10-10 原话：「现在右侧搜索栏的锁有问题，锁了的卡超出
    /// 屏幕范围或不在右侧栏了就自动解锁了，要求不能自动解锁」）</b>：检索列表是**行池**
    /// （<see cref="VirtualScrollView"/>）—— 一行滚出裁剪区就 <c>Recycle</c>
    /// （<c>SetActive(false)</c>），再被改派去装别的卡（<c>cardPicLoader.reCode</c>）。
    /// 旧判据「那一行还在不在」于是把「滚出去」误判成「这张卡没了」⇒ 把锁解掉。</para>
    ///
    /// <para>正解＝**把锁挂在那块列表上，而不是某一行的壳上**：
    /// 列表还开着 ⇒ 锁就在（被锁那张此刻在屏上，由行标记报信；不在屏上，由
    /// <see cref="UpdateSearchLock"/> 在最上面那个列表的上边缘标一枚锁符号），
    /// 只有**整块列表下来了**（关窗 / 换场景）才解锁。</para>
    ///
    /// <para>⚠ 决斗里「看点空白处弹出的墓地/除外一览」的缩略格（<c>CardDescription.quickCards</c>）
    /// 与 <c>selectDeck</c> 的行**也**是 <c>cardPicLoader</c>，但它们不在这两支检索面板下 ⇒
    /// 本字段为 null ⇒ 仍按「行还在不在」判（那边没有「滚出视口」这回事）。</para>
    /// </summary>
    static GameObject lockedSearchRoot = null;

    public static bool IsLocked { get { return locked; } }
    public static int LockedId { get { return lockedId; } }

    /// <summary>
    /// 锁着的时候说明面板归本功能管 —— **不许**被别的视图收走。
    ///
    /// <para>用户 2026-10-09 第三轮：「战斗里锁定了还是可以切场地全览的问题没解决」。他指的就是
    /// <c>Ocgcore.ES_mouseUpEmpty</c>：决斗里点一下空白处，在「全屏游戏」档下会
    /// <c>shiftCardShower(false)</c> 把说明面板收起来、让位给「我方/对方 墓地·除外·额外」那一览
    /// —— 玩家刚钉住的那份简介就没了。唯一调用点在 <c>Ocgcore.ES_mouseUpEmpty</c>。</para>
    /// </summary>
    public static bool HoldsPanel()
    {
        return locked;
    }

    /// <summary>这张 3D 卡此刻该不该贴白色高亮（<paramref name="id"/> = 它**现在**装的卡）。</summary>
    public static bool FrameOn(GameObject root, int id)
    {
        return locked && lockedRoot == root && lockedId == id;
    }

    /// <summary>
    /// 这一条目行此刻该不该整条围白 + 行中间亮一枚锁。
    ///
    /// <para>两条命中路径：① 就是当初锁的那一行（<see cref="lockedRow"/>）；
    /// ② 它属于**同一块检索列表**（<see cref="lockedSearchRoot"/>）且现在装的正是被锁那张。
    /// ② 是 2026-10-10 补的：检索列表是行池，被锁那张滚出视口再滚回来时，池子交给它的
    /// <b>往往不是当初那个行壳</b>（同一个索引换了对象）—— 只认①的话「滚回来标记就没了」，
    /// 看起来跟没锁一样。而一个检索结果里同一张卡只出现一次 ⇒ ② 命中的行唯一，不会到处开花。</para>
    /// </summary>
    public static bool RowOn(GameObject rowRoot, int id)
    {
        if (!locked || lockedKind != 2 || lockedId != id || rowRoot == null)
        {
            return false;
        }
        if (lockedRow == rowRoot)
        {
            return true;
        }
        return lockedSearchRoot != null
            && rowRoot.transform.IsChildOf(lockedSearchRoot.transform);
    }

    /// <summary>
    /// 这张决斗 3D 卡此刻是不是落在**堆叠区**（卡组 / 除外 / 墓地 / 额外）。
    ///
    /// <para>用途：<see cref="UpdateZoneLock"/> 据此决定「要不要在区域上方再飘一枚锁」。</para>
    ///
    /// <para><b>历史（10-09 第六轮 → 10-10 第二轮，两次都栽在同一个数上）</b>：第六轮用户报
    /// 「墓地里 a 在 b 下面，a 被锁后锁跑到 b 表面」——当时的根因是 <see cref="IconZ"/> 给到
    /// <c>0.25</c>（卡面高的 6%），远大于堆叠区相邻两层折到视线方向的间距
    /// （<c>0.03×sin30° ≈ 0.015</c>）⇒ 下层标记必然盖到上层卡面。当时的处置是「这几区干脆不贴面、
    /// 改在区域上方飘锁」，结果 10-10 用户又报**「卡看着和没锁定一样」**（飘锁漂在区外、卡上一点
    /// 痕迹不留）⇒ 正解其实是「贴面照旧，只把偏移压到 0.015 以下」（现 <c>RingZ/IconZ = 0.003/0.006</c>，
    /// 见 <see cref="RingZ"/> 头注）。区域飘锁**保留**：卡被压在堆里看不见时，仍靠它报「这区有卡被锁」。</para>
    ///
    /// <para>⚠ <c>Overlay</c>（XYZ 素材）不列入：素材不是靠 <c>p.location</c> 堆叠的独立卡位，
    /// 且实战里素材卡本身不参与「点住看简介」这条路径。</para>
    /// </summary>
    public static bool InStackZone(gameCard c)
    {
        if (c == null)
        {
            return false;
        }
        uint loc = c.p.location;
        return (loc & (uint)CardLocation.Deck) != 0
            || (loc & (uint)CardLocation.Extra) != 0
            || (loc & (uint)CardLocation.Grave) != 0
            || (loc & (uint)CardLocation.Removed) != 0;
    }

    /// <summary>
    /// 简介面板要不要**压掉**这次 <c>setData</c>。
    /// 只有「锁着」且「进来的不是被锁那张」才压 —— 被锁那张自己回来时必须放行
    /// （否则把光标从别的卡挪回被锁卡，面板会停在别的卡上）。
    /// </summary>
    public static bool ShouldSuppress(int id)
    {
        if (!locked)
        {
            return false;
        }
        return id != lockedId;
    }

    public static void Unlock()
    {
        if (!locked)
        {
            return;
        }
        locked = false;
        lockedId = 0;
        lockedRoot = null;
        lockedRow = null;
        lockedCard = null;
        lockedKind = 0;
        lockedDuelCard = null;      // 实例锚一并清掉（见字段头注）
        lockedSearchRoot = null;   // 检索列表的归属（见字段头注）
        holdLogged = false;
        HideZoneLock();             // 区域飘锁跟着锁一起收（见「区域飘锁」一节）
        HideSearchLock();           // 检索列表上边缘那枚锁符号同理（见「检索列表锁符号」一节）
        zoneLockLogged = false;
    }

    /// <summary>
    /// 锁的那一行**被行池回收去装别的卡了**（<see cref="cardPicLoader.reCode"/>/<c>clear</c>）。
    ///
    /// <para>⛔ 2026-10-10 改口径：**检索列表里的锁不解**。用户原话：「锁了的卡超出屏幕范围或
    /// 不在右侧栏了就自动解锁了，要求不能自动解锁」—— 行被回收只是「那张卡此刻不在屏上」，
    /// 不是「这张卡没了」。列表还开着就继续锁着，并在列表上边缘标一枚锁符号
    /// （见 <see cref="UpdateSearchLock"/>）；锁真的失效只发生在整块列表下来的时候
    /// （<see cref="ValidateHost"/> 判）。</para>
    ///
    /// <para>其它 kind==2 宿主（决斗里「墓地/除外/额外」一览的缩略格、<c>selectDeck</c> 的列表）
    /// 保持原样：那边没有「滚动复用」这回事，行没了就是真没了。</para>
    /// </summary>
    public static void NotifyRowRecycled(GameObject rowRoot)
    {
        if (!locked || lockedKind != 2 || lockedRow == null || lockedRow != rowRoot)
        {
            return;
        }
        if (lockedSearchRoot != null)
        {
            return;
        }
        Unlock();
    }

    // ───────────────────────── 按键入口 ─────────────────────────

    /// <summary>
    /// 空格按下。语义（对应用户口径）：
    /// 指在**可弹简介的卡**上 ⇒ 锁它 / 同一张再按则解除 / 另一张则切换；
    /// 指在别的地方（空白处、非卡控件）⇒ <b>单纯解锁</b>，不做任何别的事
    /// （用户 2026-10-09：「鼠标不指卡时按空格单纯解锁」）。
    /// </summary>
    public static void OnSpacePressed()
    {
        if (TextInputActive())
        {
            return;
        }
        if (!SceneAllowed())
        {
            Unlock();
            return;
        }

        GameObject p = Program.pointedGameObject;
        if (p == null)
        {
            Unlock();                       // 指在空白处：只解锁
            return;
        }

        // ① 卡组编辑器桌面上的 3D 卡（组件挂在卡根上）
        MonoCardInDeckManager deckCard = p.GetComponent<MonoCardInDeckManager>();
        if (deckCard != null && deckCard.cardData != null)
        {
            Lock(deckCard.cardData.Id, deckCard.gameObject, null, deckCard.cardData, 1);
            return;
        }

        // ② 右侧检索列表的条目行（组件挂在行内带碰撞盒的那个事件节点上）
        cardPicLoader loader = p.GetComponent<cardPicLoader>();
        if (loader != null && loader.data != null)
        {
            Lock(loader.data.Id, null, RowRootOf(loader), loader.data, 2);
            return;
        }

        // ③ 决斗场上的 3D 卡
        if (InDuel())
        {
            List<gameCard> cards = Program.I().ocgcore.cards;
            for (int i = 0; i < cards.Count; i++)
            {
                gameCard c = cards[i];
                if (!PointedOnTable(c))
                {
                    continue;
                }
                YGOSharp.Card d = c.get_data();
                Lock(d != null ? d.Id : 0, c.gameObject, null, d, 3);
                return;
            }
        }

        // 都不是 ⇒ 指在非卡处：只解锁
        Unlock();
    }

    /// <summary>
    /// 决斗场上这张 <see cref="gameCard"/> 此刻「在桌上、且光标正指着它的任一交互面」吗
    /// —— <see cref="OnSpacePressed"/> ③ 的门槛。
    ///
    /// <para><b>⛔⛔ 这里原来写的是 <c>c.isShowed</c>，那就是「场上怪兽贴不上标记」的根因</b>
    /// （用户 2026-10-09 第五轮：「发现对于在场上的卡如怪兽没有成功渲染这一层。检查」）。
    /// <c>isShowed</c> 的语义**不是「在场可见」**，而是「属于底部**展示行**」（手牌/检索结果那一排
    /// 的排布中间态）—— 实锤在 <c>Ocgcore.realize()</c>：它对每一张 <c>location</c> 带
    /// <c>MonsterZone</c> 或 <c>SpellZone</c> 的卡**无条件 <c>isShowed = false</c>**
    /// （<c>Ocgcore.cs:12238-12245</c>，「非 Overlay 且落在怪兽/魔陷区 ⇒ 清掉」那一段）。
    /// ⇒ 场上怪兽/魔陷的 <c>isShowed</c> 恒为 <c>false</c> ⇒ 这一条 <c>continue</c> 把它们
    /// **全部跳过** ⇒ 玩家指着怪兽按空格时循环一次都没命中，直接落到末尾的 <c>Unlock()</c>。
    /// 探针同源实测：残局里场上有 2 只对手怪兽，而 <c>ocgcore.cards</c> 150 张里带
    /// <c>isShowed=true</c> 的只有 2 张、**且都是手牌**（<c>loc=2</c>）。</para>
    ///
    /// <para>改成「在桌上 + 能被指到」的实际判据：对象活着 + 卡面数据有 id + 它自己的交互面
    /// （<c>card/event</c> / <c>card_bed</c> / 立绘）正在光标下。可用性由工程自己维护 ——
    /// 卡被收回牌组/盖住时 <c>card/event</c> 的 MeshCollider 会被 <c>UA_give_condition</c> 关掉
    /// （<c>gameCard.cs:1792</c>）⇒ 根本指不到，不会误锁。</para>
    ///
    /// <para>⛔ 别退回 <c>isShowed</c>、也别用 <c>p.location</c> 白名单（「只认怪兽区」那种）：
    /// 前者如上所述是错的语义；后者会把魔陷区、场上摊开的额外/墓地一览一起挡掉，
    /// 而用户的诉求是「**场上的卡**」。</para>
    /// </summary>
    static bool PointedOnTable(gameCard c)
    {
        if (c == null || c.gameObject == null || !c.gameObject.activeInHierarchy)
        {
            return false;
        }
        YGOSharp.Card d = c.get_data();
        if (d == null || d.Id == 0)
        {
            return false;        // 池化对象刚被回收、数据已被 erase_data 清掉
        }
        return c.ES_pointed_any();
    }

    /// <summary>按一次空格要执行的「锁 / 切换 / 解除」。</summary>
    static void Lock(int id, GameObject root3D, GameObject rowRoot, YGOSharp.Card card, int kind)
    {
        bool same = locked
            && lockedId == id
            && lockedRoot == root3D
            && lockedRow == rowRoot;
        if (same)
        {
            Unlock();       // 再按一次同一张 ⇒ 解除
            return;
        }
        locked = true;
        lockedId = id;
        lockedRoot = root3D;
        lockedRow = rowRoot;
        lockedCard = card;
        lockedKind = kind;
        hostMissFrames = 0;                 // 新锁 ⇒ 宽限计时从头算
        holdLogged = false;
        // 检索列表的行 ⇒ 记下**它所属的那块检索面板**（不是这一行的壳）：
        // 「行滚出视口」与「卡没了」由此分开判，见 lockedSearchRoot 头注。
        lockedSearchRoot = (kind == 2) ? SearchPanelHolding(rowRoot) : null;
        // 决斗 3D 卡：立刻把「实例锚」解析出来并缓存。
        // ⛔ 只能按 GameObject 引用反查对象池 —— 不能 GetComponent（纯 C# 类恒 null）、
        //    更不能按卡号（同号卡有多张）。见 ResolveDuelCard / lockedDuelCard 头注。
        lockedDuelCard = (kind == 3) ? ResolveDuelCard(root3D) : null;
        ReflashDesc();
        if (QuickTestTrace.Enabled)
        {
            QuickTestTrace.Log("ms", "lock id=" + id + " kind=" + kind);
        }
    }

    /// <summary>
    /// 锁 / 切换之后**立刻**把被锁那张的资料推上左侧面板。
    ///
    /// <para>为什么非有这一步不可（用户 2026-10-09：「空格按下后没有及时切换简介，要鼠标移出去
    /// 再移动回来才行」）：三条悬停路径都是**悬停对象发生变化时**才派发的
    /// （<c>Servant.Update</c> 里的 <c>preHover != pointedGameObject</c> 判据，
    /// <c>Servant.cs:216</c>）—— 按空格**不改变**悬停对象 ⇒ 一次 <c>setData</c> 都不会进来。
    /// 被锁那张自己回来的那次是**放行**的（见 <see cref="ShouldSuppress"/>），可它压根没被调用。</para>
    ///
    /// <para>⛔ 用 <c>force:true</c>：<c>CardDescription.setData</c> 在面板 <c>alpha==0</c> 时
    /// 会直接早退（决斗里面板平时就是收起的，原生悬停那一下也正是靠 force 才显示）。</para>
    /// </summary>
    static void ReflashDesc()
    {
        try
        {
            if (lockedKind == 3)
            {
                // 决斗 3D 卡：走它自己那条原生路（def 要看 controller，尾巴要带 tails），
                // 只是把 force 打开 —— 不去复制那段 def 选择逻辑。
                // ⛔ 取实例只能走 ResolveDuelCard（按 GameObject 引用反查）：
                //   原来这里是 lockedRoot.GetComponent<gameCard>()，恒为 null
                //   ⇒ 这段「立刻刷新简介」在决斗里**从来没执行过**（第四轮实机取证发现）。
                gameCard c = lockedDuelCard;
                if (c == null || c.gameObject != lockedRoot)
                {
                    c = ResolveDuelCard(lockedRoot);
                    lockedDuelCard = c;
                }
                if (c != null)
                {
                    lockedCard = c.get_data();
                    c.ShowMeLeftForce();
                }
                return;
            }
            if (lockedCard == null || Program.I() == null || Program.I().cardDescription == null)
            {
                return;
            }
            Program.I().cardDescription.setData(lockedCard, GameTextureManager.myBack, "", true);
        }
        catch (System.Exception e)
        {
            Program.DEBUGLOG(e);
        }
    }

    /// <summary>条目行根 = 事件节点（带 BoxCollider 的那个）的父节点 —— 整条并集。</summary>
    public static GameObject RowRootOf(cardPicLoader loader)
    {
        if (loader == null)
        {
            return null;
        }
        Transform t = loader.transform.parent;
        return t != null ? t.gameObject : loader.gameObject;
    }

    // ───────────────────────── 每帧维护 ─────────────────────────

    /// <summary>每帧一次（由 <see cref="Program"/> 的输入块调）：宿主还在不在 + 把面板抢回来。</summary>
    public static void Tick()
    {
        if (!locked)
        {
            HideZoneLock();
            HideSearchLock();
            return;
        }
        if (!SceneAllowed())
        {
            Unlock();       // 换场景 / 关掉功能
            HideZoneLock();
            HideSearchLock();
            return;
        }
        if (!ValidateHost())
        {
            Unlock();       // 被锁的那张真没了
            HideZoneLock();
            HideSearchLock();
            return;
        }
        EnsureDesc();
        UpdateZoneLock();
        UpdateSearchLock();
    }

    /// <summary>
    /// 帧尾再补一次（servant 段跑完之后）。理由：把面板抢回来这件事必须排在**所有可能改动面板的
    /// 代码之后** —— 决斗里点空白处走的 <c>Ocgcore.ES_mouseUpEmpty</c>、卡换位置走的
    /// <c>gameCard.set_data</c> 都在 servant 段里，只在帧头补的话会看到一帧「别的东西」。
    /// </summary>
    public static void LateTick()
    {
        if (!locked)
        {
            return;
        }
        EnsureDesc();
        // 区域飘锁在**帧尾**再摆一次（幂等）：卡是在 servant 段里被搬的（抽牌/送墓/回牌组），
        // 帧头摆完再被搬走会留下一帧错位。见「区域飘锁」一节。
        UpdateZoneLock();
        // 检索列表上边缘那枚锁符号同理：列表自己会滚动/重排（VirtualScrollView 的 clipMove），
        // 帧头量到的上边缘未必是本帧最终的。
        UpdateSearchLock();
    }

    /// <summary>
    /// 把一个 3D 卡根物体反查回它的 <see cref="gameCard"/> 实例（对象池里那一个）。
    ///
    /// <para><b>⛔ 另外两条路都是死的</b>：① <c>go.GetComponent&lt;gameCard&gt;()</c> ——
    /// gameCard/OCGobject 是**纯 C# 类、不是 Component**，Unity 泛型版 <c>GetComponent&lt;T&gt;()</c>
    /// 运行期**恒返回 null**（第四轮实机症状「战斗里不能锁定」的根因）；
    /// ② 按卡号 <c>Id</c> 在场上找 —— 一副卡组同号卡有多张，找回来的**不是**被锁那一个
    /// （第二轮症状「同名卡乱亮、跟鼠标跑」的根因）。</para>
    ///
    /// <para>⇒ 只能按 <b>GameObject 引用相等</b>反查：<c>ocgcore.cards</c> 是对象池，
    /// 卡换位置时 <c>GCS_cardMove</c> 搬的就是同一个对象 ⇒ 引用稳定且唯一。</para>
    ///
    /// <para>查不到返回 null，调用方按「暂时看不见」走宽限，不猜。</para>
    /// </summary>
    static gameCard ResolveDuelCard(GameObject go)
    {
        if (go == null)
        {
            return null;
        }
        Program prog = Program.I();
        if (prog == null || prog.ocgcore == null)
        {
            return null;
        }
        List<gameCard> cards = prog.ocgcore.cards;
        if (cards == null)
        {
            return null;
        }
        for (int i = 0; i < cards.Count; i++)
        {
            gameCard c = cards[i];
            if (c != null && c.gameObject == go)
            {
                return c;
            }
        }
        return null;
    }

    /// <summary>
    /// 被锁那张还在不在。
    ///
    /// <para><b>⛔ 这里绝对不许「按卡号去场上另找一张顶上」</b> —— 那是 2026-10-09 第二轮的写法，
    /// 被用户实机打回：「战斗里选中一个怪，其他同名卡莫名其妙显示锁了，而且选中哪个哪个就显示锁」。
    /// 根因：一副卡组里同号卡本来就有多张，按卡号找回来的**不是**被锁那一个实例；更糟的是那版还会
    /// 优先挑 <c>ES_pointed_any()</c>（光标正指着的那张）⇒ 锁标记跟着鼠标在同名卡之间跳。
    /// ⇒ 判据只认**实例**（<see cref="lockedRoot"/> 这个 gameCard 对象本身）；实例真没了就
    ///   **老实解锁**，绝不猜。</para>
    ///
    /// <para><b>为什么还需要宽限（<see cref="HostMissGrace"/>）</b>：卡换位置（特招 / 回牌组 /
    /// 进墓地）那几帧它会被 <c>hide()</c>（<c>erase_data()</c> 把 <c>data.Id</c> 清成 0），但
    /// **同一个 gameCard 对象还会回来** —— 这正是用户第一轮提的「卡的位置变化如特招时左边卡牌
    /// 简介也会掉」。所以「暂时看不见」不算没了，先等；等超了才解锁。</para>
    /// </summary>
    static bool ValidateHost()
    {
        if (lockedKind == 3)
        {
            // ⛔ 实例解析只走 ResolveDuelCard（按 GameObject 引用反查对象池）。
            //   绝不要 GetComponent<gameCard>()：gameCard/OCGobject 是纯 C# 类、不是 Component，
            //   Unity 泛型版 GetComponent<T>() 对非 Component 类型**运行期恒返回 null**
            //   ⇒ 每帧第一次校验就判「实例被销毁」⇒ 自己把自己解锁（第四轮实机症状的根因）。
            //   详见 lockedDuelCard 字段头注。
            gameCard cur = lockedDuelCard;
            if (cur == null || cur.gameObject != lockedRoot)
            {
                cur = ResolveDuelCard(lockedRoot);      // 池实例可能被换过 ⇒ 按锚再查一次
                lockedDuelCard = cur;
            }
            if (cur == null && lockedRoot == null)
            {
                DiagMiss("root destroyed");
                return false;       // 根物体真被销毁了（Unity 的 == 对已销毁对象等于 null）⇒ 解锁
            }
            if (cur == null)
            {
                // 根还在、只是这一帧没在池里查到配对。**不许立刻解锁** ——
                // 那正是「战斗里锁不上」的观感来源。当「暂时看不见」走宽限，超时才解。
                hostMissFrames++;
                if (QuickTestTrace.Enabled && hostMissFrames == 1)
                {
                    QuickTestTrace.Log("ms", "lock miss f=1 lookup-fail want=" + lockedId);
                }
                return hostMissFrames <= HostMissGrace;
            }
            if (cur.gameObject.activeInHierarchy)
            {
                YGOSharp.Card cd = cur.get_data();
                if (cd != null && cd.Id == lockedId)
                {
                    lockedCard = cd;
                    hostMissFrames = 0;
                    if (QuickTestTrace.Enabled && !holdLogged)
                    {
                        holdLogged = true;
                        QuickTestTrace.Log("ms", "lock hold id=" + lockedId);
                    }
                    return true;    // 宿主好好的，装的还是被锁那张
                }
                if (cd != null && cd.Id != 0)
                {
                    // 同一个对象被拿去装**别的卡**了（GCS_cardCreate 会复用对象）⇒ 锁的对象没了
                    DiagMiss("reused cdId=" + cd.Id + " want=" + lockedId);
                    return false;
                }
            }
            // 剩下两种都是「暂时看不见」：① 卡换位置时被 hide（p.location 变 Unknown ⇒ erase_data
            // ⇒ data.Id 归 0）② 被回牌组的那一瞬。这不是「没了」⇒ 宽限等待，超时才解。
            // ⛔ 宽限期内**不要**去别处找同号卡顶上 —— 见上面头注。
            hostMissFrames++;
            if (QuickTestTrace.Enabled
                && (hostMissFrames == 1 || hostMissFrames == 2 || hostMissFrames == 60
                    || hostMissFrames == 120 || hostMissFrames == HostMissGrace))
            {
                YGOSharp.Card cdx = cur.get_data();
                QuickTestTrace.Log("ms", "lock miss f=" + hostMissFrames
                    + " want=" + lockedId
                    + " active=" + (cur.gameObject.activeInHierarchy ? 1 : 0)
                    + " showed=" + (cur.isShowed ? 1 : 0)
                    + " cdNull=" + (cdx == null ? 1 : 0)
                    + " cdId=" + (cdx != null ? cdx.Id : -1)
                    + " loc=" + cur.p.location);
            }
            return hostMissFrames <= HostMissGrace;
        }
        // 编辑器桌面卡 / 列表行：宿主的对象是稳的，只有「整块被销毁或失活」才算没了。
        // ⛔ 别写成 `if (lockedRoot != null && !lockedRoot.activeInHierarchy) return false;`：
        //   Unity 的 `==` 对**已销毁**对象返回「等于 null」⇒ 那样写会把「行/卡被销毁了」
        //   判成「还在」（第一半就是 false），锁永远解不掉、简介被永久压住。
        if (lockedKind == 2)
        {
            // 检索列表（<see cref="lockedSearchRoot"/> 非 null）：**只要那块列表还开着就锁着**，
            // 被锁那张滚到哪、还在不在结果里都不影响（用户 2026-10-10：不许自动解锁）。
            // ⛔ 别退回「lockedRow.activeInHierarchy」：行一旦滚出裁剪区就被行池 SetActive(false)，
            //   那正是用户报的「超出屏幕范围就自动解锁」。
            if (lockedSearchRoot != null)
            {
                return SearchPanelOpen(lockedSearchRoot);
            }
            // 其它检索/一览宿主（决斗「墓地·除外」一览缩略格、selectDeck）没有滚动复用，
            // 行还在就还在。
            return lockedRow != null && lockedRow.activeInHierarchy;
        }
        if (lockedKind == 1)
        {
            return lockedRoot != null && lockedRoot.activeInHierarchy;
        }
        return true;
    }

    /// <summary>排查用：<see cref="ValidateHost"/> 判失败时把原因写进日志（只在探针开关生效时输出）。</summary>
    static void DiagMiss(string why)
    {
        if (QuickTestTrace.Enabled)
        {
            QuickTestTrace.Log("ms", "lock fail " + why);
        }
    }

    /// <summary>
    /// 锁着的时候每帧保证「左侧面板在亮、而且摆的正是被锁那张」。
    /// 不是的话就补一次（<see cref="ReflashDesc"/> 会顺带把面板重新点亮 ——
    /// <c>CardDescription.apply</c> 末尾自己会 <c>shiftCardShower(true)</c>）。
    /// </summary>
    static void EnsureDesc()
    {
        if (!locked)
        {
            return;
        }
        CardDescription cd = Program.I() != null ? Program.I().cardDescription : null;
        if (cd == null)
        {
            return;
        }
        YGOSharp.Card cur = cd.showingCard;
        if (cd.descVisible && cur != null && cur.Id == lockedId)
        {
            return;                 // 已经在正确的位置上，什么都不用做
        }
        ReflashDesc();
        if (QuickTestTrace.Enabled)
        {
            QuickTestTrace.Log("ms", "lock reassert id=" + lockedId
                + " kind=" + lockedKind
                + " visible=" + (cd.descVisible ? 1 : 0)
                + " was=" + (cur != null ? cur.Id : 0));
        }
    }

    // ───────────────────────── 标记配色 / 尺寸（要调整外观只看这一段）─────────────────────

    /// <summary>3D 卡那一圈白框的颜色（渲染时按材质实例上色，白色会让辉光最亮）。</summary>
    public static readonly Color MarkWhite = new Color(1f, 1f, 1f, 1f);

    /// <summary>列表条目行四边围白的不透明度（NGUI 顶点色，带 alpha）。</summary>
    public static readonly Color RowEdgeColor = new Color(1f, 1f, 1f, 0.85f);

    /// <summary>锁图标贴图里**填充**的白（半透明，用户口径）。</summary>
    static readonly Color IconFill = new Color(1f, 1f, 1f, 0.72f);

    /// <summary>锁图标贴图里那一圈**深色描边**（纯白图标压在亮卡图上会看不见，描一圈才立得住）。</summary>
    static readonly Color IconEdge = new Color(0f, 0f, 0f, 0.45f);

    /// <summary>3D 卡上锁图标的大小：占卡面**高**的比例。</summary>
    const float IconCardHeightRatio = 0.42f;

    /// <summary>列表行里锁图标的大小：占**行高**的比例。</summary>
    const float IconRowHeightRatio = 0.55f;

    /// <summary>列表行四边白条的粗细：占行高的比例（另有 2px 下限）。</summary>
    const float RowEdgeHeightRatio = 0.055f;

    /// <summary>列表行标记的绘制深度（行内自带的控件是 0~3，压不过它就要比它们大）。</summary>
    public const int RowMarkDepth = 20;

    // ───────────────────────── 高亮：3D 卡（白框 + 锁图标）─────────────────────────

    /// <summary>
    /// 造一份 3D 卡高亮（一圈白 + 卡面锁图标），挂到 <paramref name="face"/> 下，默认关着。
    /// 两个宿主（<see cref="gameCard"/> / <see cref="MonoCardInDeckManager"/>）各自持有一份、
    /// 每帧按 <see cref="FrameOn"/> 显隐。
    ///
    /// <para>两件东西都是**运行期自己造**的，不依赖任何新美术（桌面线加不了资源）：
    /// ① 一圈白＝<see cref="AddLockRing"/>——自画的白框贴图铺满整面卡；
    /// ② 卡面正中的锁＝<see cref="AddLockIcon"/>，贴图是 <see cref="LockIconTexture"/> 逐像素画的。</para>
    ///
    /// <para>⛔ 第二版（2026-10-09 上午）那圈白是「克隆现成的紫框 prefab 再把材质染白」，
    /// 实机被用户一句话打回（「周遭一圈的特效要换成白色的！」）—— 染不动：框的四种颜色**全烘在
    /// 贴图里**，材质 <c>_Color</c> 本来就是纯白。根因记在 <see cref="AddLockRing"/> 的头注里。</para>
    ///
    /// <para>⛔ 上色一律走**实例材质**（<c>Renderer.material</c> 会自动克隆）——
    /// 碰 <c>sharedMaterial</c> 会连累整局所有卡（把游戏真正的「决斗选卡紫框」也一起改掉）。</para>
    /// </summary>
    public static GameObject NewLockMark(Transform face)
    {
        if (face == null || Program.I() == null)
        {
            return null;
        }
        GameObject root = new GameObject("lock_mark");
        root.layer = face.gameObject.layer;
        root.transform.SetParent(face, false);
        root.transform.localPosition = Vector3.zero;
        root.transform.localRotation = Quaternion.identity;
        root.transform.localScale = Vector3.one;

        // ① 一圈白框：**自己画**的白圈贴图铺满整面卡（见 AddLockRing 头注 —— 紫框那套染不白）
        AddLockRing(root.transform, face);

        // ② 卡面正中的锁图标（贴一层白色半透明的锁）
        // ⛔ z 用 IconZ（≈贴面），**不是**旧的 -0.25 —— 那个偏移是卡面高的 6%，
        //   斜视角下就是用户看到的「浮起」（见 IconZ / RingZ 的头注）。
        AddLockIcon(root.transform, face, new Vector3(0f, 0f, IconZ), IconCardHeightRatio);

        root.SetActive(false);
        return root;
    }

    /// <summary>
    /// 往 <paramref name="parent"/> 下加一枚锁图标方块。
    /// 摆法与工程里现成的 <c>gameCard.nagaSign</c>（那张「无效」的大叉）完全一致：
    /// 贴到卡面同一平面、局部旋转归零、尺寸用「目标世界尺寸 ÷ 卡面 localScale」，这样
    /// 手牌/摊开行整体放大时图标跟着放大，不会跑出卡面。
    /// </summary>
    static void AddLockIcon(Transform parent, Transform face, Vector3 localPos, float heightRatio)
    {
        if (Program.I().mod_simple_quad == null)
        {
            return;
        }
        GameObject icon = Program.I().create(Program.I().mod_simple_quad);
        if (icon == null)
        {
            return;
        }
        icon.name = "lock_icon";
        icon.layer = parent.gameObject.layer;
        icon.transform.SetParent(parent, false);
        icon.transform.localRotation = Quaternion.identity;
        Vector3 fs = face.localScale;
        if (Mathf.Abs(fs.x) < 0.0001f) { fs.x = 1f; }
        if (Mathf.Abs(fs.y) < 0.0001f) { fs.y = 1f; }
        if (Mathf.Abs(fs.z) < 0.0001f) { fs.z = 1f; }
        float world = Mathf.Abs(fs.y) * heightRatio;
        icon.transform.localScale = new Vector3(world / fs.x, world / fs.y, world / fs.z);
        icon.transform.localPosition = localPos;
        Renderer r = icon.GetComponent<Renderer>();
        if (r == null)
        {
            return;
        }
        Material m = LockMaterial();
        if (m != null)
        {
            r.sharedMaterial = m;
        }
        else if (r.material != null)
        {
            // 兜底：真找不到透明 shader 时至少把贴图换上去（会带一层不透明底，属于降级）
            r.material.mainTexture = LockIconTexture();
        }
    }

    // ───────────────────────── 高亮：3D 卡那一圈白（自己画贴图，见 AddLockRing）─────────────────────────

    /// <summary>白圈的白色（含不透明度 —— 用户口径「白色半透明」）。</summary>
    static readonly Color RingWhite = new Color(1f, 1f, 1f, 0.78f);

    /// <summary>
    /// 白圈贴在卡面**前方**多少（卡面自身局部 z；负 = 朝相机那侧，与 nagaSign 同向）。
    ///
    /// <para><b>⛔⛔ 这个值必须「小到看不出来」——用户 2026-10-09 第五轮原话：「我要求这层渲染要有
    /// 和卡本身一样的渲染，例如在卡组界面里它要正常的和卡一起倾斜和被遮挡而不是浮起」。</b>
    /// 倾斜本来就是对的（探针实测 <c>lock_mark</c> 的 <c>lossyRot</c> 与卡面逐位相同、
    /// <c>localRot=(0,0,0)</c>）——用户看到的「浮起 / 不被遮挡」是**这一个偏移量**造成的。</para>
    ///
    /// <para><b>机制（探针实测，卡面世界高 4 单位）</b>：标记是**独立 quad**，材质
    /// <c>Unlit/Transparent</c>（<c>renderQueue=3000</c>、不写深度），卡面 <c>face_pic</c> 是
    /// <c>Unlit/Texture</c>（<c>queue=2000</c>、写深度）⇒ 谁在前**只由深度测试**决定，
    /// 于是「标记往前挪多少」直接等于「它能盖住多近的邻卡」：
    /// <list type="bullet">
    /// <item>旧值 <c>-0.08</c>（卡面高的 2%）＋锁图标旧值 <c>-0.25</c>（<b>6%</b>）——
    ///   斜视角下这 6% 的**法向偏移会投影成肉眼可见的视差** ⇒ 白锁像一枚浮在卡面之上的贴纸；</item>
    /// <item>更麻烦的是它把前后判定一起改掉了：只要邻卡离这张卡不足 0.25 单位，
    ///   邻卡的卡面**已经**挡住了卡面，标记却仍比邻卡靠前 ⇒ 白框/白锁照画在邻卡身上
    ///   （用户看到的「不被遮挡」）。</item>
    /// </list>
    /// 现在压到卡面高的 <b>0.25%</b>（0.01 / 4）⇒ 视差亚像素；且只对「比它近 0.01 单位」的东西让步，
    /// 而工程里任意两张卡的实战间距都远大于它 ⇒ 观感＝画在卡面上，遮挡行为＝与卡面一致。
    /// <b>这就是「和卡本身一样的渲染」的落地口径。</b></para>
    ///
    /// <para><b>⛔ 别改成 0</b>：卡面是**不透明**材质（先画、写深度），标记是**透明**材质（后画），
    /// 完全同深度会与卡面抢同一个深度值（浮点变换后逐像素不等 ⇒ 出现麻点）。
    /// 这 0.01 是给深度测试留的余量，不是给眼睛看的 —— 桌面线深度缓冲 24 位、卡距相机约 20 单位时
    /// 分辨率约 <c>8e-5</c> 单位，0.01 有 100 倍以上余量。⛔ 反向（16 位深度缓冲）需重测。</para>
    ///
    /// <para><b>⛔ 也别把标记的 <c>renderQueue</c> 改成与卡面一样的 2000</b>：标记带 alpha 混合，
    /// 与不透明卡面同队列时会先写深度、把卡面自己挡掉。必须保持「队列更大 ⇒ 一定后画」，
    /// 才能稳定地画在**自己的**卡面上；遮挡照样由深度测试负责，与队列无关。</para>
    ///
    /// <para><b>⛔⛔ 2026-10-10 再压一档（0.01 → 0.003）：这是「堆叠区标记不串到上层卡」的
    /// 唯一旋钮。</b>堆叠区（卡组/墓地/除外/额外）同一格的卡按 <c>Ocgcore.get_point_worldposition</c>
    /// 的 <c>y += sequence * 0.03</c> 摞起来；默认视角卡面绕 x 轴 60°（<c>Program.tableauFrontX</c>）
    /// ⇒ 相机视线俯角≈30°，这 0.03 的世界间距**折到视线方向上只剩 <c>0.03×sin30° ≈ 0.015</c>**。
    /// 标记「朝相机偏多少」一旦超过它，下层卡的标记就会**画到上层卡的卡面之上**（用户 10-09 看到的
    /// 「a 被锁，锁跑到 b 表面」）。0.003 只有它的 1/5 ⇒ 上层卡在深度上稳稳更靠前，
    /// 遮挡行为与卡面自身一致 —— 这正是用户第五轮要的「和卡本身一样的渲染」。⛔ 调大前先读这段。</para>
    /// </summary>
    const float RingZ = -0.003f;

    /// <summary>
    /// 白锁比白圈再往前一丝（0.006 对 0.003）。
    ///
    /// <para>唯一作用是把「圈 / 锁」的先后**定死**：两者几何中心重合（都挂在 <c>lock_mark</c> 原点），
    /// 同深度时透明队列按到相机的距离排序、距离完全相同 ⇒ 顺序不稳定（会逐帧翻转）。
    /// 差一个极小值即可确定，视觉上等同同一平面。⛔ 别把它当成「浮起」的调节旋钮。</para>
    ///
    /// <para>⛔ 同样受「堆叠区不串位」约束（见 <see cref="RingZ"/> 头注）：必须远小于堆叠区相邻
    /// 两层折到视线方向的间距（默认视角 ≈ <c>0.03×sin30° = 0.015</c>）。</para>
    /// </summary>
    const float IconZ = -0.006f;

    static Texture2D ringTex = null;

    /// <summary>
    /// 往 <paramref name="parent"/> 下铺一张「白框」quad：占满整面卡，贴图是运行期画出来的
    /// <see cref="RingTexture"/>。
    ///
    /// <para><b>为什么自己做白圈，而不是克隆现成的框 prefab 再染色</b>（第二版的做法，实机打回）：
    /// ① 框的四种颜色**全烘在贴图里** —— 实测 <c>kuang1/2/3/4.mat</c> 四份材质的 <c>_Color</c>
    ///    都是纯白 <c>(1,1,1,1)</c>、<c>_TintColor</c> 也全同，唯一差别是 <c>_MainTex</c>：
    ///    那是 ExtremeFX 的 <c>flash_flare_pink.png</c> 粉色光斑 ⇒ 改材质颜色等于什么都没改，
    ///    用户看到的当然还是粉紫的（「周遭一圈的特效要换成白色的！」）。
    /// ② <c>kuang.3DS</c>（1391 字节）经 <c>_dev_probe/_parse_3ds.py</c> 解析 chunk 得到的真相是：
    ///    object <c>Plane01</c>、**32 顶点**，本身就是一圈**中空边框环**（外 36×46 / 内 30×40，
    ///    边宽 3 单位，中间还插了分段点供 UV 翻页）—— **框的形状是 mesh 给的**，不是靠贴图 alpha 抠的。
    ///    （最初误判成「一整块 quad、换白贴图会糊成白板」，已按实锤纠正。）</para>
    ///
    /// <para><b>那为什么不干脆克隆现成框、只把它的贴图换纯白？</b> 那确实做得到，但原生框的环宽被
    /// 钉死在 3/36，位置/朝向还要跟着它的 Animator 与实例材质走；自画贴图粗细可控、也不碰任何既有
    /// 资源。（2026-10-09 的备选方案，留待用户对观感拍板后再定。）</para>
    ///
    /// <para>几何口径同 <c>nagaSign</c>（工程里现成的「贴面 quad」样板）：卡面 <c>card/face</c> 是
    /// Unity 内置 Quad（1×1 单位、面内坐标 ±0.5），<c>localScale ≈ (3,4,1)</c> —— 实锤：
    /// <c>gameCard.faceHalfWorld()</c> 取的就是 <c>lossyScale.y * 0.5</c>（默认值 2 ⇒ 世界高 4、宽 3）。
    /// 所以子物体 <c>localScale = 1</c> 正好盖满整面；贴图按卡面 3:4 画，四条边落地粗细一致。</para>
    /// </summary>
    static void AddLockRing(Transform parent, Transform face)
    {
        if (Program.I() == null || Program.I().mod_simple_quad == null || face == null)
        {
            return;
        }
        GameObject ring = Program.I().create(Program.I().mod_simple_quad);
        if (ring == null)
        {
            return;
        }
        ring.name = "lock_ring";
        ring.layer = parent.gameObject.layer;
        ring.transform.SetParent(parent, false);
        ring.transform.localRotation = Quaternion.identity;
        ring.transform.localScale = Vector3.one;                 // 1×1 面内单位 = 盖满整面
        ring.transform.localPosition = new Vector3(0f, 0f, RingZ);
        Renderer r = ring.GetComponent<Renderer>();
        if (r == null)
        {
            return;
        }
        Material m = r.material;      // 实例材质（自动克隆；绝不碰 sharedMaterial，那会连累整局所有卡）
        if (m == null)
        {
            return;
        }
        m.mainTexture = RingTexture();
        if (m.HasProperty("_Color"))
        {
            m.SetColor("_Color", MarkWhite);
        }
        if (m.HasProperty("_TintColor"))
        {
            m.SetColor("_TintColor", new Color(1f, 1f, 1f, 0.8f));
        }
    }

    /// <summary>
    /// 白圈贴图：**96×128**（＝卡面 3:4），RGBA，运行期逐像素算 —— 边缘一圈实心白 + 往内渐隐。
    /// 粗细/柔和度/颜色都在这一个函数里，改外观只改这里：
    /// <see cref="RingWhite"/> 定色，<c>edge</c> 定实心边宽，<c>fade</c> 定往里渐隐的宽度。
    ///
    /// <para>⛔ 别拿现成美术来当这个框：桌面线是「冻结播放器 + 只换 DLL」，新贴图进不了包
    /// （口径同 <see cref="LockIconTexture"/>）。</para>
    /// </summary>
    public static Texture2D RingTexture()
    {
        if (ringTex != null)
        {
            return ringTex;
        }
        const int W = 96;
        const int H = 128;
        const int edge = 4;         // 实心白边宽（像素）
        const int fade = 12;        // 自实心边往内渐隐到 0 的宽度（像素）
        Texture2D t = new Texture2D(W, H, TextureFormat.RGBA32, false);
        t.name = "descLock_ring";
        t.wrapMode = TextureWrapMode.Clamp;
        t.filterMode = FilterMode.Bilinear;
        Color[] px = new Color[W * H];
        for (int y = 0; y < H; y++)
        {
            for (int x = 0; x < W; x++)
            {
                int dx = Mathf.Min(x, W - 1 - x);        // 到最近的那条竖边的距离
                int dy = Mathf.Min(y, H - 1 - y);        // 到最近的那条横边的距离
                int d = Mathf.Min(dx, dy);
                float a;
                if (d < edge)
                {
                    a = 1f;
                }
                else if (d < edge + fade)
                {
                    a = 1f - (d - edge) / (float)fade;
                }
                else
                {
                    a = 0f;
                }
                px[y * W + x] = new Color(RingWhite.r, RingWhite.g, RingWhite.b, a * RingWhite.a);
            }
        }
        t.SetPixels(px);
        t.Apply(false, false);
        ringTex = t;
        return t;
    }

    // ───────────────────────── 高亮：检索列表的条目行 ─────────────────────────

    /// <summary>
    /// 造一份「整条围白 + 行中间一枚锁」的标记（默认关着）。
    ///
    /// <para>四条边不是贴图，而是 4 条 <see cref="UITexture"/>（贴自建纯白）拼出来的框 ——
    /// 这样粗细是按像素算的，行宽高比变化也不会出现「横边比竖边粗」。
    /// 行的矩形直接从**背景控件**（<paramref name="rectWidget"/>，就是带 BoxCollider 的那个
    /// 事件节点自己）的 <c>worldCorners</c> 现量，不写死 200×90。</para>
    ///
    /// <para><b>⛔⛔ 运行期 new 出来的 UITexture 必须显式置 <c>path = ""</c></b> —— 这是第一版
    /// 「右侧栏没显示锁定效果」的**唯一**根因：<c>UITexture.OnStart</c> 里那句
    /// <c>if (mOutPath != "") mainTexture = GameTextureManager.get(mOutPath);</c>
    /// （<c>NGUI/Scripts/UI/UITexture.cs:46</c>）对 <c>mOutPath == null</c> 的新控件也成立
    /// （<c>null != ""</c>）⇒ 下一帧就把这里刚设上去的贴图**冲成 null**，
    /// 于是白条与锁图标「建好了、深度位置全对、屏幕上什么都没有」。
    /// 口径同 <c>ViewToggleButton</c> 那条注释（它踩的是同一个坑）。</para>
    ///
    /// <para>深度也不写死：取本行**已有控件**的最大 depth + 4（见下面的扫描）——
    /// 同一份 <see cref="cardPicLoader"/> 有三种宿主，深度基线各不相同。</para>
    /// </summary>
    public static GameObject NewRowMark(GameObject rowRoot, UIWidget rectWidget)
    {
        if (rowRoot == null || rectWidget == null)
        {
            return null;
        }
        // 标记深度基线：扫本行**现有**控件（必须在创建任何子物体之前扫，否则会把标记自己算进去）。
        // 宿主三种：① 编辑器检索列表（行内控件 depth 0~3）② 决斗「墓地/额外」一览的缩略格
        // （CardDescription.quickCards，relayer(i) 把 uiTexture.depth 设成 50+2i）
        // ③ selectDeck 那一版。写死 20 会在②③里被行自己的图压住 ⇒ 白框和锁图标都看不见。
        int baseDepth = RowMarkDepth;
        UIWidget[] rowWidgets = rowRoot.GetComponentsInChildren<UIWidget>(true);
        for (int i = 0; i < rowWidgets.Length; i++)
        {
            if (rowWidgets[i] != null && rowWidgets[i].depth + 4 > baseDepth)
            {
                baseDepth = rowWidgets[i].depth + 4;
            }
        }

        GameObject root = new GameObject("lock_row_mark");
        root.layer = rowRoot.layer;
        root.transform.SetParent(rowRoot.transform, false);
        root.transform.localPosition = Vector3.zero;
        root.transform.localRotation = Quaternion.identity;
        root.transform.localScale = Vector3.one;

        // 行的四条边（量到本行根节点的局部空间里；子节点也是同一空间 ⇒ 尺寸可直接用）
        Vector3[] wc = rectWidget.worldCorners;       // 0=bl 1=tl 2=tr 3=br
        Transform t = rowRoot.transform;
        Vector3 bl = t.InverseTransformPoint(wc[0]);
        Vector3 tl = t.InverseTransformPoint(wc[1]);
        Vector3 br = t.InverseTransformPoint(wc[3]);
        Vector3 up = tl - bl;
        Vector3 right = br - bl;
        float h = up.magnitude;
        float w = right.magnitude;
        if (h < 1f || w < 1f)
        {
            UnityEngine.Object.Destroy(root);
            return null;
        }
        up /= h;
        right /= w;
        float edge = Mathf.Max(2f, h * RowEdgeHeightRatio);

        AddStrip(root, "t", bl + right * (w * 0.5f) + up * (h - edge * 0.5f), w, edge, baseDepth);
        AddStrip(root, "b", bl + right * (w * 0.5f) + up * (edge * 0.5f), w, edge, baseDepth);
        AddStrip(root, "l", bl + right * (edge * 0.5f) + up * (h * 0.5f), edge, h, baseDepth);
        AddStrip(root, "r", bl + right * (w - edge * 0.5f) + up * (h * 0.5f), edge, h, baseDepth);

        // 行中间的锁图标：行内没有 3D 卡面可贴，用 UITexture 贴在条目正中
        float side = Mathf.Max(12f, h * IconRowHeightRatio);
        GameObject icon = new GameObject("lock_icon");
        icon.layer = rowRoot.layer;
        icon.transform.SetParent(root.transform, false);
        icon.transform.localPosition = bl + right * (w * 0.5f) + up * (h * 0.5f);
        icon.transform.localRotation = Quaternion.identity;
        icon.transform.localScale = Vector3.one;
        UITexture ut = icon.AddComponent<UITexture>();
        ut.path = "";                     // ⛔ 见头注：不置空，OnStart 会把贴图冲掉
        ut.mainTexture = LockIconTexture();
        ut.color = Color.white;
        ut.depth = baseDepth + 1;
        ut.width = Mathf.RoundToInt(side);
        ut.height = Mathf.RoundToInt(side);

        root.SetActive(false);
        return root;
    }

    /// <summary>
    /// 行标记**刚显示出来**时调一次：强制所属 <see cref="UIPanel"/> 重建绘制列表。
    ///
    /// <para><b>⚠ 这一句是「保险」，不是「右侧栏没显示锁定效果」的根因。</b>
    /// 第四轮一度把它当根因写在这里 —— <b>那是错的</b>，2026-10-09 当晚实拍已推翻：
    /// 当时的假阴性来自**截图口径**，不是渲染。截图脚本在游戏把分辨率
    /// 「启动参数 1600x900 → 1920x986」切完**之前**量了窗口客户区
    /// （实测心跳 `[hb] frame=20 scr=1920x986`），于是只截到真实客户区**上面 900 像素**，
    /// 而被锁那一行正好在 `y=926..1016` —— 两帧都没截到标记 ⇒ 相减恒为 0。
    /// 把口径改成「等客户区稳定 + 抓图前重新量 + 按日志里的行矩形裁」之后，
    /// 同一次实拍**肉眼可见**白框与白锁都在（`_dev_probe/EV_A_locked_row.png`）。</para>
    ///
    /// <para>保留它的理由：运行期 <c>new</c> 的 NGUI 控件**是否/何时**进面板绘制列表，
    /// 本工程有一次确凿的踩坑先例 —— <c>ViewToggleButton.cs:254</c>（那句是踩过
    /// <c>DuelUndo</c> 遮罩「压暗没实现」之后写下的）。探针实测：调过之后标记的
    /// <c>drawCall</c> 非空（<c>dc=1</c>），<c>isVisible/hasVertices/mainTexture</c> 全绿。
    /// 代价是每轮「由隐到显」多一次面板绘制列表重建（本面板 49 个控件量级），可接受。</para>
    ///
    /// <para>⛔ 别只在 <see cref="NewRowMark"/> 建完时调 —— 那时标记还是 <c>SetActive(false)</c>，
    /// 重建会把禁用中的控件排除在外；要等 <b>真正显示的那一拍</b>再重建。</para>
    /// </summary>
    public static void OnRowMarkShown(GameObject mark)
    {
        if (mark == null)
        {
            return;
        }
        UIPanel panel = UIPanel.Find(mark.transform);
        if (panel != null)
        {
            panel.RebuildAllDrawCalls();
        }
    }

    /// <summary>行框的一条边（自建纯白贴图 + 控件色 ⇒ 纯色半透明，不需要新美术）。</summary>
    static void AddStrip(GameObject parent, string name, Vector3 pos, float w, float h, int depth)
    {
        GameObject o = new GameObject("edge_" + name);
        o.layer = parent.layer;
        o.transform.SetParent(parent.transform, false);
        o.transform.localPosition = pos;
        o.transform.localRotation = Quaternion.identity;
        o.transform.localScale = Vector3.one;
        UITexture ut = o.AddComponent<UITexture>();
        ut.path = "";                     // ⛔ 见 NewRowMark 头注（OnStart 冲贴图的坑）
        ut.mainTexture = WhitePixel();
        ut.color = RowEdgeColor;
        ut.depth = depth;
        ut.width = Mathf.Max(1, Mathf.RoundToInt(w));
        ut.height = Mathf.Max(1, Mathf.RoundToInt(h));
    }

    static Texture2D whitePixel = null;

    /// <summary>
    /// 自建的纯白小贴图（4×4）。
    /// ⛔ 别用 <c>Texture2D.whiteTexture</c>：本工程实测那个内置 1×1 贴图在运行期 new 出来的
    /// NGUI 控件上不渲染（口径同 <c>ViewToggleButton.BoxTex</c> 的结论），而且尺寸必须**偶数**。
    /// </summary>
    public static Texture2D WhitePixel()
    {
        if (whitePixel != null)
        {
            return whitePixel;
        }
        whitePixel = new Texture2D(4, 4, TextureFormat.RGBA32, false);
        whitePixel.name = "descLock_white";
        whitePixel.wrapMode = TextureWrapMode.Clamp;
        whitePixel.filterMode = FilterMode.Bilinear;
        Color[] px = new Color[16];
        for (int i = 0; i < px.Length; i++)
        {
            px[i] = Color.white;
        }
        whitePixel.SetPixels(px);
        whitePixel.Apply(false, false);
        return whitePixel;
    }

    // ───────────────────────── 锁图标（运行期画，不依赖任何美术资源）─────────────────────────

    static Texture2D lockIconTex = null;
    static Material lockMat = null;

    /// <summary>
    /// 锁图标贴图：128×128、RGBA，按「圆角矩形锁体 + 上半圆环锁梁 + 挖空钥匙孔」在三倍超采样下
    /// 逐像素**算出来**（不是加载资源 —— 桌面线加不了资源，见类头注）。
    ///
    /// <para>颜色/粗细/形状都在这个函数里，改外观改这里即可：
    /// 填充色 <see cref="IconFill"/>、描边色 <see cref="IconEdge"/>；
    /// 形状参数见 <see cref="InLockBody"/> / <see cref="InKeyhole"/>。</para>
    /// </summary>
    public static Texture2D LockIconTexture()
    {
        if (lockIconTex != null)
        {
            return lockIconTex;
        }
        const int S = 128;
        const int SS = 3;                       // 每像素 3×3 超采样（边缘抗锯齿）
        Texture2D t = new Texture2D(S, S, TextureFormat.RGBA32, false);
        t.name = "descLock_icon";
        t.wrapMode = TextureWrapMode.Clamp;
        t.filterMode = FilterMode.Bilinear;
        Color[] px = new Color[S * S];
        float inv = 1f / (SS * SS);
        for (int y = 0; y < S; y++)
        {
            for (int x = 0; x < S; x++)
            {
                float ar = 0f, ag = 0f, ab = 0f, aa = 0f;
                for (int sy = 0; sy < SS; sy++)
                {
                    for (int sx = 0; sx < SS; sx++)
                    {
                        float u = (x + (sx + 0.5f) / SS) / S;
                        float v = (y + (sy + 0.5f) / SS) / S;
                        if (InsideLock(u, v))
                        {
                            ar += IconFill.r * IconFill.a;
                            ag += IconFill.g * IconFill.a;
                            ab += IconFill.b * IconFill.a;
                            aa += IconFill.a;
                        }
                        else if (InsideLockOutline(u, v))
                        {
                            ar += IconEdge.r * IconEdge.a;
                            ag += IconEdge.g * IconEdge.a;
                            ab += IconEdge.b * IconEdge.a;
                            aa += IconEdge.a;
                        }
                    }
                }
                ar *= inv;
                ag *= inv;
                ab *= inv;
                aa *= inv;
                px[y * S + x] = aa > 0.0005f
                    ? new Color(ar / aa, ag / aa, ab / aa, aa)
                    : new Color(0f, 0f, 0f, 0f);
            }
        }
        t.SetPixels(px);
        t.Apply(false, false);
        lockIconTex = t;
        return t;
    }

    /// <summary>锁的实体形状（锁体 ∪ 锁梁）减掉钥匙孔。坐标是 0..1、v 朝上。</summary>
    static bool InsideLock(float u, float v)
    {
        if (InKeyhole(u, v, 0f))
        {
            return false;
        }
        return InLockBody(u, v, 0f);
    }

    /// <summary>形状向外扩一圈（钥匙孔反向内缩）⇒ 描边带。宽度＝<c>pad</c>（约贴图 4 像素）。</summary>
    static bool InsideLockOutline(float u, float v)
    {
        const float pad = 0.032f;
        // 「放大后的形状」减去「原形状」＝贴着轮廓的那一圈（外缘与钥匙孔边各一条）
        bool grown = InLockBody(u, v, pad) && !InKeyhole(u, v, -pad);
        if (!grown)
        {
            return false;
        }
        return !InsideLock(u, v);
    }

    /// <summary>锁体：圆角矩形（半宽 0.265 / 半高 0.225，圆角 0.07）+ 上半圆环锁梁（外径 0.19 / 内径 0.115）。</summary>
    static bool InLockBody(float u, float v, float pad)
    {
        if (InRoundRect(u, v, 0.5f, 0.335f, 0.265f + pad, 0.225f + pad, 0.07f + pad))
        {
            return true;
        }
        return InRing(u, v, 0.5f, 0.585f, 0.19f + pad, Mathf.Max(0f, 0.115f - pad));
    }

    /// <summary>钥匙孔：上方圆 + 下方梯形（<paramref name="pad"/> 为正＝放大，负＝缩小）。</summary>
    static bool InKeyhole(float u, float v, float pad)
    {
        float dx = u - 0.5f;
        float r = 0.052f + pad;
        float dy = v - 0.365f;
        if (r > 0f && dx * dx + dy * dy <= r * r)
        {
            return true;
        }
        float top = 0.365f + pad;
        float bottom = 0.235f - pad;
        if (v <= top && v >= bottom)
        {
            float k = (top - v) / Mathf.Max(0.0001f, top - bottom);
            float hw = Mathf.Lerp(0.030f, 0.052f, Mathf.Clamp01(k)) + pad;
            return hw > 0f && Mathf.Abs(dx) <= hw;
        }
        return false;
    }

    static bool InRoundRect(float u, float v, float cx, float cy, float hw, float hh, float r)
    {
        float dx = Mathf.Abs(u - cx);
        float dy = Mathf.Abs(v - cy);
        if (dx > hw || dy > hh)
        {
            return false;
        }
        float qx = dx - (hw - r);
        float qy = dy - (hh - r);
        if (qx <= 0f || qy <= 0f)
        {
            return true;
        }
        return qx * qx + qy * qy <= r * r;
    }

    static bool InRing(float u, float v, float cx, float cy, float ro, float ri)
    {
        float dx = u - cx;
        float dy = v - cy;
        float d2 = dx * dx + dy * dy;
        return d2 <= ro * ro && d2 >= ri * ri;
    }

    /// <summary>锁图标专用材质（全体共用一份：贴图与颜色都一样，共用还能合批）。</summary>
    public static Material LockMaterial()
    {
        if (lockMat != null)
        {
            return lockMat;
        }
        Shader s = null;
        // ⛔ 名字必须在**构建产物里**搜得到，否则 Shader.Find 返回 null（冻结播放器不许新增 shader）。
        //   实测（在 output/Windows/MeiheweitePro_Data/*.assets 里逐个 grep 名字串）：
        //   `Unlit/Transparent Colored` 在 resources.assets（NGUI 自己就用它当 UITexture 的默认
        //   shader，见 NGUI/Scripts/UI/UITexture.cs:115 ⇒ 必然在包里）、`Sprites/Default` 在
        //   globalgamemanagers（恒含列表）、`Legacy Shaders/Transparent/Diffuse` 在 sharedassets0。
        //   ⚠ `Unlit/Transparent`（不带 Colored）**不在**包里，别写进来当第一顺位。
        string[] names = { "Unlit/Transparent Colored", "Sprites/Default",
                           "Legacy Shaders/Transparent/Diffuse" };
        for (int i = 0; i < names.Length && s == null; i++)
        {
            s = Shader.Find(names[i]);
        }
        if (s == null)
        {
            return null;
        }
        lockMat = new Material(s);
        lockMat.name = "descLock_icon_mat";
        lockMat.mainTexture = LockIconTexture();
        return lockMat;
    }

    // ───────────────────────── 高亮：堆叠区（卡组/墓地/除外/额外）的「区域飘锁」─────────────────────────
    //
    // 用户口径（2026-10-10 第二次返工）：「要求位于墓地/卡组/额外/除外这种区域的卡被锁了以后
    // **本身的表现不变**，并且是在**对应区域的上方（玩家视角的上方，而不是现在这种相对的上方）**
    // 飘一个**正对着玩家**的锁型图标（不是现在这样躺着），而且要适配卡组记牌功能」。
    //
    // 上一版（10-09 第六轮）把飘锁做成**卡面贴面标记的兄弟**：挂在 `card/face` 下、局部旋转归零、
    // 用 `Program.cardUpOffset`（**卡自己的「上」**）往上挪。三处都不对，用户逐一否掉：
    //   ① 挂在卡面下 ⇒ 观感上仍是「这张卡身上多了个东西」，卡被搬走/缩放它跟着走；
    //   ② `localRotation = identity` ⇒ 图标与卡面**共面**（斜视角下就是「躺着」），
    //      卡面平铺时图标也平铺、不朝玩家；
    //   ③ 位置跟着**卡**走而不是跟着**区域**走 ⇒ 卡组被摊开（记牌）时图标跟着卡片跑，
    //      而不是待在区域上方。
    //
    // 现在改成**场景级单例**（全工程只有一枚，见 <see cref="zoneLock"/>）：
    //   位置 = 区域锚点 + **玩家视角上方向**（<see cref="Program.cardUpOffsetLen"/>）；
    //   朝向 = <c>Quaternion.Euler(Program.tableauFrontX,0,0)</c> —— 照抄 gameField 那批
    //   **区域计数文字**（`LOCATION_DECK/GRAVE/EXTRA/REMOVED_*`，每帧跟 tableauFrontX），
    //   那正是「正对玩家」在本工程里唯一的既有口径（也就是用户说的「适配卡组记牌功能」：
    //   与那批计数文字同一套定位/朝向，互不遮挡）。
    //   卡本体**一根手指都不碰**：`gameCard` 对堆叠区只把贴面标记关掉，不再有任何飘锁子物体。

    /// <summary>区域飘锁图标的世界高度（quad 是 1×1 单位 ⇒ localScale 直接等于世界尺寸）。</summary>
    const float ZoneIconWorldSize = 2.2f;

    /// <summary>
    /// 区域飘锁离区域锚点**玩家视角上方**多少（世界单位）。
    ///
    /// <para>方向用 <see cref="Program.cardUpOffsetLen"/> —— 60° 视角下它就是**相机上方向**
    /// <c>(0,0.5,0.866)</c>（与 <c>cardUpOffset</c> 同一份推导），正俯视下是世界 <c>+z</c>
    /// （＝屏幕上方）。<b>不是</b>卡自己的上方向：用户 2026-10-10 明确否掉了「相对的上方」。</para>
    ///
    /// <para>取 3.4：比区域计数文字那点位移（世界 (0,0,-3)）大一截，图标稳稳落在计数文字**上方**、
    /// 不与它叠字（那就是「适配卡组记牌功能」的落地含义）。</para>
    /// </summary>
    const float ZoneUpWorld = 3.4f;

    /// <summary>场景级飘锁（全工程唯一一枚；被锁那张落在堆叠区时亮，其余时候关着）。</summary>
    static GameObject zoneLock = null;

    /// <summary>本轮锁是否已经记过「区域飘锁已就位」的日志（只记一次，避免刷屏）。</summary>
    static bool zoneLockLogged = false;

    /// <summary>
    /// 每帧维护「区域飘锁」：被锁那张落在堆叠区（卡组/除外/墓地/额外）就摆到**区域上方**，否则收起来。
    /// 由 <see cref="Tick"/> / <see cref="LateTick"/> 每帧调用（**不是**让每张卡自己挂一份 —— 见本节头注）。
    /// </summary>
    public static void UpdateZoneLock()
    {
        if (!locked || lockedKind != 3 || lockedDuelCard == null
            || Program.I() == null || Program.I().ocgcore == null
            || Program.camera_game_main == null)
        {
            // ⛔ camera_game_main 也要判：下面 getCamGoodPosition 用它换算，
            //   决斗没起来/正在切场景时它可能是空的（防一手 NRE，别让锁把主循环打断）。
            HideZoneLock();
            return;
        }
        gameCard c = lockedDuelCard;
        if (c.gameObject == null || !c.gameObject.activeInHierarchy || !InStackZone(c))
        {
            HideZoneLock();
            return;
        }
        uint loc = ZoneLocationOf(c.p.location);
        if (loc == 0)
        {
            HideZoneLock();
            return;
        }
        GameObject root = EnsureZoneLock();
        if (root == null)
        {
            return;
        }
        if (!root.activeSelf)
        {
            root.SetActive(true);
        }
        GPS gps = new GPS
        {
            controller = c.p.controller,
            location = loc,
            sequence = 0,
            position = 0
        };
        Vector3 anchor = Program.I().ocgcore.get_point_worldposition(gps);
        // 与区域计数文字（gameField.relocateTextMesh）同一套「贴到玩家眼前」的手法：
        //   position = getCamGoodPosition(锚点 + 偏移, 往相机侧拉 2 世界)，
        //   euler    = (tableauFrontX, 0, 0) ⇒ 正对玩家。
        // ⚠ 偏移方向用 cardUpOffsetLen（**屏幕上方**），不是计数文字那个 (0,0,-3)（朝玩家挪）——
        //   后者会与计数文字叠在一起，而用户要的是「在区域**上方**」。
        root.transform.position = UIHelper.getCamGoodPosition(
            anchor + Program.cardUpOffsetLen(ZoneUpWorld), -2f);
        root.transform.eulerAngles = new Vector3(Program.tableauFrontX, 0f, 0f);
        if (QuickTestTrace.Enabled && !zoneLockLogged)
        {
            zoneLockLogged = true;
            Vector3 wp = root.transform.position;
            QuickTestTrace.Log("ms", "lock zone loc=" + loc + " ctrl=" + c.p.controller
                + " pos=(" + wp.x.ToString("0.00") + "," + wp.y.ToString("0.00") + "," + wp.z.ToString("0.00") + ")"
                + " up=" + ZoneUpWorld + " facing=" + Program.tableauFrontX);
        }
    }

    /// <summary>把区域飘锁收起来（解锁 / 离开堆叠区 / 换场景 / 卡没了）。</summary>
    static void HideZoneLock()
    {
        if (zoneLock != null && zoneLock.activeSelf)
        {
            zoneLock.SetActive(false);
        }
    }

    /// <summary>懒建那枚场景级飘锁（材质/贴图与贴面锁图标同源，都是运行期算出来的，见类头注）。</summary>
    static GameObject EnsureZoneLock()
    {
        if (zoneLock != null)
        {
            return zoneLock;
        }
        if (Program.I() == null || Program.I().mod_simple_quad == null)
        {
            return null;
        }
        GameObject root = Program.I().create(Program.I().mod_simple_quad);
        if (root == null)
        {
            return null;
        }
        root.name = "descLock_zone";
        Renderer r = root.GetComponent<Renderer>();
        if (r != null)
        {
            Material m = LockMaterial();
            if (m != null)
            {
                r.sharedMaterial = m;
            }
            else if (r.material != null)
            {
                r.material.mainTexture = LockIconTexture();
            }
        }
        root.transform.localScale = new Vector3(ZoneIconWorldSize, ZoneIconWorldSize, 1f);
        root.SetActive(false);
        zoneLock = root;
        return root;
    }

    /// <summary>
    /// 把 <c>p.location</c> 收敛成**单个**堆叠区枚举位（一张卡只会落在其中一个区）。
    /// 判据与顺序都与 <see cref="InStackZone"/> 一致。
    /// </summary>
    static uint ZoneLocationOf(uint loc)
    {
        if ((loc & (uint)CardLocation.Deck) != 0) return (uint)CardLocation.Deck;
        if ((loc & (uint)CardLocation.Extra) != 0) return (uint)CardLocation.Extra;
        if ((loc & (uint)CardLocation.Grave) != 0) return (uint)CardLocation.Grave;
        if ((loc & (uint)CardLocation.Removed) != 0) return (uint)CardLocation.Removed;
        return 0;
    }

    // ───────────────────────── 检索列表：归属判定 + 列表上边缘的锁符号 ─────────────────────────

    /// <summary>
    /// 这一行长在**哪一块检索面板**下（卡组编辑器右侧那块搜索栏 / 点卡名弹出的检索窗），
    /// 两块都不沾就返回 null。
    ///
    /// <para>用「祖先链里有没有那一块」判，而不是看行自己的字段：行是行池里的壳，
    /// 重新检索一次就可能被改派，但它**一定还挂在同一块面板的 <c>panel_</c> 下**
    /// （<see cref="VirtualScrollView"/> 的 <c>Bind</c> 里 <c>SetParent(panel.transform)</c>）。</para>
    /// </summary>
    static GameObject SearchPanelHolding(GameObject rowRoot)
    {
        if (rowRoot == null || Program.I() == null)
        {
            return null;
        }
        Program p = Program.I();
        GameObject a = p.cardSearch != null ? p.cardSearch.WindowRoot : null;
        if (a != null && rowRoot.transform.IsChildOf(a.transform))
        {
            return a;
        }
        GameObject b = p.deckManager != null ? p.deckManager.gameObjectSearch : null;
        if (b != null && rowRoot.transform.IsChildOf(b.transform))
        {
            return b;
        }
        return null;
    }

    /// <summary>
    /// 那块检索面板此刻**还开着**吗（＝锁还该不该在）。
    ///
    /// <para>两块宿主「收起」时都是拿 iTween 把自己挪到屏幕外，**不是** <c>SetActive(false)</c>
    /// ⇒ 只看 <c>activeInHierarchy</c> 恒为真。判据取它们各自的 <see cref="Servant"/> 状态：
    /// 编辑器的＝<c>DeckManager.isShowed</c>，检索窗的＝<c>CardSearchWindow.isShowed</c>。</para>
    ///
    /// <para>两个都认不出来（将来换宿主）时退回 <c>activeInHierarchy</c>，别把锁判死。</para>
    /// </summary>
    static bool SearchPanelOpen(GameObject root)
    {
        if (root == null)
        {
            return false;
        }
        Program p = Program.I();
        if (p == null)
        {
            return false;
        }
        if (p.deckManager != null && root == p.deckManager.gameObjectSearch)
        {
            return p.deckManager.isShowed;
        }
        if (p.cardSearch != null && root == p.cardSearch.WindowRoot)
        {
            return p.cardSearch.isShowed;
        }
        return root.activeInHierarchy;
    }

    /// <summary>那块检索面板里的列表控件（<c>panel_</c>）—— 它的裁剪区就是「卡显示处」的边界。</summary>
    static UIPanel ListPanelOf(GameObject searchRoot)
    {
        return searchRoot == null ? null : UIHelper.getByName<UIPanel>(searchRoot, "panel_");
    }

    /// <summary>那一行壳现在装的还是被锁那张吗（行池改派之后就不是了）。</summary>
    static bool RowShowsLocked()
    {
        if (lockedRow == null)
        {
            return false;
        }
        cardPicLoader l = lockedRow.GetComponentInChildren<cardPicLoader>(true);
        return l != null && l.code == lockedId;
    }

    /// <summary>
    /// 被锁那张此刻**已经在检索列表里看得见**吗（行活着 + 装的就是它 + 没被列表裁剪掉）。
    ///
    /// <para>为什么要连裁剪一起量：<see cref="VirtualScrollView"/> 会多绑一行 overscan
    /// （<c>Overscan = 1</c>）—— 那一行是 active 的、行标记也亮着，但它整个在裁剪区外，
    /// 屏幕上什么都没有。只看 <c>activeInHierarchy</c> 会漏掉这一行宽的窗口期，
    /// 于是「明明看不见却不报」。</para>
    /// </summary>
    static bool SearchRowOnScreen()
    {
        if (lockedRow == null || !lockedRow.activeInHierarchy || !RowShowsLocked())
        {
            return false;
        }
        if (lockedSearchRoot != null
            && !lockedRow.transform.IsChildOf(lockedSearchRoot.transform))
        {
            return false;               // 壳被改派到别的列表去了
        }
        cardPicLoader l = lockedRow.GetComponentInChildren<cardPicLoader>(true);
        UIWidget w = l != null ? l.GetComponent<UIWidget>() : null;
        UIPanel p = w != null ? w.panel : null;
        return p != null && p.IsVisible(w);
    }

    /// <summary>锁符号的边长（控件单位 —— 本窗树里 1 单位 = 1 屏幕像素，见 <see cref="UpdateSearchLock"/>）。</summary>
    const float SearchLockIconPx = 36f;

    /// <summary>
    /// 锁符号中心离列表**上边缘**往里多少像素。必须 ≥ 半个图标：符号挂在列表面板里面，
    /// 出了裁剪区就会被裁掉（见 <see cref="UpdateSearchLock"/>）。
    /// </summary>
    const float SearchLockInsetTopPx = 20f;

    /// <summary>锁符号中心离列表**右边缘**往里多少像素（右侧那条滚动条在裁剪区外，留点余地更好看）。</summary>
    const float SearchLockInsetRightPx = 34f;

    static GameObject searchLock = null;
    static GameObject searchLockHost = null;
    static bool searchLockLogged = false;

    /// <summary>
    /// 在检索列表的**上边缘**（＝「卡显示处」的最上面，也就是搜索栏正下方那一条）标一枚锁符号：
    /// 只在「锁着 **且** 锁在检索列表里 **且** 被锁那张此刻不在列表上」时亮。
    ///
    /// <para>用户 2026-10-10 原话：「现在右侧搜索栏的锁有问题，锁了的卡超出屏幕范围或不在右侧栏了
    /// 就自动解锁了，要求不能自动解锁，有超出屏幕范围或不在右侧栏的锁定就在搜索栏底下标个锁符号
    /// （就是位于卡显示处的上边缘）且不能自动解锁」。</para>
    ///
    /// <para><b>位置逐帧现量</b>：从列表 <c>panel_</c> 的 <c>finalClipRegion</c>
    /// （＝ (中心 x, 中心 y, 宽, 高)，**面板局部**坐标，宽高是**全**宽/全高）取上、右两条边，
    /// 再 <c>TransformPoint</c> 成世界点写进 <c>transform.position</c>。
    /// ⛔ 不写死 prefab 坐标 —— 详情版排版（<c>CardSearchWindow.LayoutDetailRows</c>）会改
    /// <c>panel_</c> 的锚点，窗口自己也会滑进滑出。写世界坐标还顺带抵消了列表的滚动位移
    /// （滚动时动的是 <c>panel_</c> 的 <c>clipOffset</c> 与 transform，裁剪区在世界里是钉住的）。</para>
    ///
    /// <para><b>为什么挂在 <c>panel_</c> 里面</b>：① NGUI 的 drawCall 排序只在**同一个面板内**
    /// 比 widget 深度，挂到父节点上会被整块列表压住；② 直接复用行标记那套
    /// （<see cref="NewRowMark"/> 的深度基线 + <see cref="OnRowMarkShown"/> 的重建绘制列表）。
    /// 代价是会被列表裁剪 ⇒ 落点必须整个落在裁剪区内（<see cref="SearchLockInsetTopPx"/>）。</para>
    /// </summary>
    public static void UpdateSearchLock()
    {
        if (!locked || lockedKind != 2 || lockedSearchRoot == null
            || !SearchPanelOpen(lockedSearchRoot) || SearchRowOnScreen())
        {
            HideSearchLock();
            return;
        }
        UIPanel list = ListPanelOf(lockedSearchRoot);
        if (list == null)
        {
            HideSearchLock();
            return;
        }
        GameObject icon = EnsureSearchLock(list);
        if (icon == null)
        {
            return;
        }
        Vector4 cr = list.finalClipRegion;
        Vector3 local = new Vector3(
            cr.x + cr.z * 0.5f - SearchLockInsetRightPx,
            cr.y + cr.w * 0.5f - SearchLockInsetTopPx,
            0f);
        icon.transform.position = list.transform.TransformPoint(local);
        if (!icon.activeSelf)
        {
            icon.SetActive(true);
            OnRowMarkShown(icon);   // 运行期 new 的 NGUI 控件何时进绘制列表没有保证（同 NewRowMark）
        }
        if (QuickTestTrace.Enabled && !searchLockLogged)
        {
            searchLockLogged = true;
            QuickTestTrace.Log("ms", ProbeSearchLockInfo);
        }
    }

    /// <summary>收起检索列表上边缘那枚锁符号（解锁 / 被锁那张回到列表上 / 换场景）。</summary>
    static void HideSearchLock()
    {
        if (searchLock != null && searchLock.activeSelf)
        {
            searchLock.SetActive(false);
        }
        searchLockLogged = false;
    }

    /// <summary>懒建那枚列表锁符号（换了一块列表就重建 —— 宿主是行池外的另一个面板）。</summary>
    static GameObject EnsureSearchLock(UIPanel list)
    {
        if (searchLock != null && searchLockHost == list.gameObject)
        {
            return searchLock;
        }
        if (searchLock != null)
        {
            UnityEngine.Object.Destroy(searchLock);
            searchLock = null;
            searchLockHost = null;
        }
        int baseDepth = RowMarkDepth;         // 深度基线口径同 NewRowMark：扫现有控件，不写死
        UIWidget[] ws = list.GetComponentsInChildren<UIWidget>(true);
        for (int i = 0; i < ws.Length; i++)
        {
            if (ws[i] != null && ws[i].depth + 4 > baseDepth)
            {
                baseDepth = ws[i].depth + 4;
            }
        }
        GameObject o = new GameObject("lock_search_mark");
        o.layer = list.gameObject.layer;
        o.transform.SetParent(list.transform, false);
        o.transform.localRotation = Quaternion.identity;
        o.transform.localScale = Vector3.one;
        UITexture ut = o.AddComponent<UITexture>();
        ut.path = "";                          // ⛔ 见 NewRowMark 头注（OnStart 把贴图冲成 null 的坑）
        ut.mainTexture = LockIconTexture();
        ut.color = Color.white;
        ut.depth = baseDepth + 4;
        ut.width = Mathf.RoundToInt(SearchLockIconPx);
        ut.height = Mathf.RoundToInt(SearchLockIconPx);
        o.SetActive(false);
        searchLock = o;
        searchLockHost = list.gameObject;
        return o;
    }

    /// <summary>验收用：检索列表锁定状态的一句话快照（读的是现状，不是愿望）。</summary>
    public static string ProbeSearchLockInfo
    {
        get
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            sb.Append("lock id=").Append(lockedId);
            sb.Append(" list=").Append(lockedSearchRoot == null ? "none"
                : (SearchPanelOpen(lockedSearchRoot) ? "open" : "closed"));
            sb.Append(" rowAlive=").Append(lockedRow != null && lockedRow.activeInHierarchy ? 1 : 0);
            sb.Append(" rowShows=").Append(RowShowsLocked() ? 1 : 0);
            sb.Append(" onScreen=").Append(SearchRowOnScreen() ? 1 : 0);
            sb.Append(" iconOn=").Append(searchLock != null && searchLock.activeSelf ? 1 : 0);
            UIPanel list = ListPanelOf(lockedSearchRoot);
            if (list != null)
            {
                Camera cam = list.anchorCamera != null ? list.anchorCamera : Program.camera_main_2d;
                sb.Append(" listRect=").Append(CardSearchWindow.RectOf(list, cam));
                sb.Append(" cam=").Append(cam != null ? cam.name : "null");
            }
            if (searchLock != null)
            {
                UITexture ut = searchLock.GetComponent<UITexture>();
                UIPanel lp = ut != null ? ut.panel : null;
                Camera cam2 = lp != null && lp.anchorCamera != null
                    ? lp.anchorCamera : Program.camera_main_2d;
                sb.Append(" iconRect=").Append(CardSearchWindow.RectOf(ut, cam2));
            }
            return sb.ToString();
        }
    }

}
