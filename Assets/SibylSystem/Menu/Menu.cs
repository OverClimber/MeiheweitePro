using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using UnityEngine;
using UnityEngine.Networking;

public class Menu : WindowServantSP
{
    /// <summary>主菜单版本号文本；version_ 标签平时显示它，检查/下载数据时临时让位给进度。
    ///
    /// 口径（用户 2026-09-25 定稿）：**只显示本产品版本**（v1.3），不再对外显示上游协议号
    /// （原「v2.4.d 1036.2」）。已核实该上游串只是显示文案（全工程仅 3 处 UI 赋值），
    /// 真正送服务器的协议版本走 <c>Config.ClientVersion</c>（MyCard.cs 里十六进制编码），
    /// 二者无关 ⇒ 改这里不影响联机兼容。
    /// 版本号常量在 <see cref="ClientSelfUpdate.ClientVersionText"/>，这里是显示文案。</summary>
    private const string VersionBaseText = "MeiheweitePro v" + ClientSelfUpdate.ClientVersionText;

    /// <summary>主菜单 version_ 标签，运行时从 prefab 取；兼作在线数据更新的状态显示。</summary>
    private UILabel _versionLabel = null;

    /// <summary>失败提示正在占着版本号的起始时刻（&lt;0 表示当前没有失败提示）。</summary>
    private float _failNoticeSince = -1f;

    /// <summary>失败提示在版本号位置停留的秒数，过后还给版本号（完整原因聊天框里已有一份）。</summary>
    private const float FailNoticeSeconds = 15f;

    private bool isPreDownloading = false;
    private bool isClouseupDownloading = false;
    private bool _isCancellationRequested = false;
    private const string CloseupDirectory = "picture/closeup";
    
    // 超先行卡更新检查相关
    private const string SuperPreDownloadUrl = "https://cdntx2.moecube.com/ygopro-super-pre/archive/ygopro-super-pre.ypk";
    private const string SuperPreLocalPath = "downloads/ygopro-super-pre.ypk";
    private GameObject _newBadge = null;
    private bool _hasSuperPreUpdate = false;
    private bool _isCheckingUpdate = false;
    private bool _isSuperPreUpdateRequested = false;

    public bool HasSuperPreUpdate
    {
        get { return _hasSuperPreUpdate; }
    }

    // 用于在协程间传递结果的成员变量
    private int _closeupSuccessCount;
    private int _closeupFailureCount;
    private List<string> _pendingCloseupDownloads;
    private Coroutine _currentDownloadCoroutine = null;

    // ==================== RD 模式入口 / 徽标（_plan_rdmode.md §2.1） ====================
    //
    // 交互口径（示意稿已确认 + 用户 2026-09-19 定稿）：
    //   · 主菜单**左上角**常驻小按钮（用户 2026-09-19 定稿：与面板分开摆、且不随菜单移动），
    //     **不进 MenuItemOrder 十项**（独立挂节点）
    //     —— 进/出模式都必须只动这一颗，不复制整套菜单。
    //   · 点击 = 立即切换（数据启动时已全装，零加载）。**不弹任何提示**：
    //     按钮文案报的是**当前**在哪一边（OCG 下写「OCG 模式」、RD 下写「RD 模式」，
    //     2026-09-19 由用户定稿；原先是反的，报「点一下会切到哪一边」，最容易看错），
    //     当前模式同时由**主色调**（OCG 暖金 / RD 琥珀）+ 角标 + 「RD 下只留 RD 入口」体现。
    //     原来切模式弹的那条 toast 是噪音，已删（连 ShowRdToast/TickRdToast 一起）。
    //   · 「编辑卡组」「联机模式」两项尾部挂小 RD 角标。
    //   · RD 下主菜单只留 RD 能用的入口（见 RdVisibleItems），其余隐藏并**紧凑重排**。

    private const string RdChipNode = "rdChip";
    private const string RdChipButton = "rdChip_";

    /// <summary>模式徽标那块深色底板的节点名（见 <see cref="CreateRdChipPlate"/>）。</summary>
    private const string RdChipPlateNode = "rdChipPlate";

    /// <summary>底板用的精灵名（模板里 <c>back</c> 用的就是它；取不到 back 时才回落这个）。</summary>
    private const string RdChipPlateSprite = "darkTransparented";

    /// <summary>
    /// RD 入口按钮在主菜单根坐标系里的位置（**左上角，与主菜单面板分开摆**）。
    ///
    /// 用户 2026-09-19 定稿：「放在左上角里主菜单稍远的位置，不随主菜单移动而移动」，
    /// 并在同日追加：「ocg 模式和 rd 模式这几个字**不和主菜单一起移动**，我希望它是单独的，
    /// 以及也配上主菜单一样的黑色底色」——后半条见 <see cref="PinRdChip"/> 与
    /// <see cref="CreateRdChipPlate"/>。
    /// 上一版（198 / 149）贴在面板**右**上角、和版本号同一水平带，有两个毛病：
    ///   · 右缘 224 已经探出面板裁剪区（模板 210 宽 ⇒ 局部 x ∈ [−105, +105]），
    ///     要靠 FitMenuBack 专门往右放宽裁剪区才看得见，一动就整颗被 SoftClip 切没；
    ///   · y 要**跟着面板重心补偿走**（`RdChipY − MenuVerticalCenterOffset()`），
    ///     而 RD 的可见项只有 7 个、OCG 是 10 个 ⇒ 切一次模式它就跳 120 个局部单位。
    ///
    /// 现在的口径：
    ///   · x 取**负值**（面板中心左侧），与 180 宽的面板拉开约 180 个局部单位，
    ///     既不压住面板、也不跑到屏幕外（主菜单相机世界宽约 ±681，见 TraceChipDiag 的量法）；
    ///   · y 是**常量**，不减任何补偿 —— 菜单换模式时面板会改高度并整体重排，
    ///     这颗按钮不参与，就在左上角待着（这是本条需求的重点）。
    ///   · 这两个数描述的是**窗口落定之后**的位置；窗口滑入途中由 PinRdChip 每帧反算。
    ///
    /// ⚠ 改动这两个数要连带跑 _verify_rdmenu / _verify_rdbadge：两份脚本都按
    ///   `[rd] chip screen=` 的落点点击，位置一动，脚本拿到的是新坐标，但**截图判据**
    ///   （面板右缘、chip 是否被裁）要人眼复核一遍。
    /// </summary>
    private const float RdChipX = -320f;

    /// <summary>
    /// RD 入口按钮在主菜单根坐标系里的纵向位置。**固定值，不随菜单重排变化**
    /// （用户口径：按钮不动、菜单动）。屏幕高度 986 时约在顶边下方 113 px。
    /// </summary>
    private const float RdChipY = 270f;

    /// <summary>
    /// 面板裁剪区在**芯片那一侧**要多留的余量（局部单位，≈ 1.4 px）。
    ///
    /// 原委：`FitMenuBack()` 只按模板给的裁剪区来遮，而 RD 入口按设计要摆在面板**外面**
    /// （这一版摆在左侧），于是整颗按钮被 SoftClip 切掉 —— 症状极隐蔽：
    /// `[rd] chip` 探针**照样报得出一组坐标**（那是按钮几何中心的投影），
    /// 但那一像素 hover=none、截图里什么都没画，脚本照它点击就是「点了没反应」，
    /// 2026-09-19 验收里表现为「点 3 次 RD 入口都没切到 RD」。
    /// </summary>
    private const float MenuClipMargin = 12f;

    /// <summary>RD 入口整体缩放（克隆自主菜单行按钮，缩到 0.62 才是「小按钮」）。</summary>
    private const float RdChipScale = 0.62f;

    /// <summary>RD 入口文字字号（会被上面那个 scale 乘掉，28×0.62≈17px）。</summary>
    private const int RdChipFontSize = 28;

    /// <summary>
    /// 底板相对文字四周的**等宽**留白（局部单位；芯片自身再 ×0.62、UI 再 ×Screen.height/700，
    /// 屏幕上约 18×0.62×1.41≈16px）。
    /// </summary>
    private const int RdChipPlatePadX = 18;
    private const int RdChipPlatePadY = 10;

    /// <summary>
    /// 底板最小尺寸。取 164 = 「OCG 模式」实测文字宽 128 + 两侧各 18 —— 两种模式的文案宽窄不同
    /// （RD 比 OCG 窄一个字），取最小值兜住，**切换模式时底板不跳动**，而字始终在正中、两边留白相等。
    /// </summary>
    private const int RdChipPlateMinW = 164;
    private const int RdChipPlateMinH = 48;

    /// <summary>底板 / 标签的引用（建底板时记下，之后每帧对账用，见 <see cref="FitRdChipPlate"/>）。</summary>
    private UISprite rdChipPlate;
    private UILabel rdChipLabel;

    // ==================== 「支持作者」按钮（最右下角，点击跳转爱发电）====================

    private const string SponsorNode = "sponsor";
    private const string SponsorButton = "sponsor_";

    /// <summary>点击「支持作者」要打开的爱发电主页。</summary>
    private const string SponsorUrl = "https://afdian.com/a/MeiheweitePro";

    /// <summary>
    /// 贴角余量（根坐标系局部单位，1 单位 ≈ Screen.height/700 px）：按钮**中心**到屏幕
    /// 右/下边的距离。x 向 = 文字半宽（「支持作者」≈40）+ 10；y 向 = 文字半高（≈17）+ 7。
    /// 用户 2026-09-24 定稿：「在页面的最右下角」——文字要贴到角上，余量只留一线
    /// （按钮碰撞盒 180×50×0.62 会探出屏幕边缘几 px，NGUI 拾取不受影响，且裁剪区已放宽）。
    /// </summary>
    private const float SponsorEdgeMarginX = 50f;
    private const float SponsorEdgeMarginY = 24f;

    /// <summary>
    /// 「支持作者」在主菜单根坐标系里的落点：**最右下角**，按当前窗口宽高比现算 ——
    /// 根坐标系满屏高 700（±350），满屏宽 700×aspect。每帧重算（PinSponsor），
    /// 窗口一拉大按钮就跟着贴到新的角上。口径仍是「菜单动、按钮不动」。
    /// </summary>
    private static Vector2 SponsorPos()
    {
        float aspect = Screen.height > 0 ? (float)Screen.width / Screen.height : (16f / 9f);
        return new Vector2(350f * aspect - SponsorEdgeMarginX, -350f + SponsorEdgeMarginY);
    }

    /// <summary>
    /// <see cref="FitMenuBack"/> 算出来的**静态**裁剪区（模板面板 + 徽标落点常量）。
    /// 每帧的芯片跟随只在这条基线上做并集，绝不就地累加 —— 面板回到正位后裁剪区要能自己收回去。
    /// </summary>
    private Vector4 menuClipBase = new Vector4(float.NaN, 0f, 0f, 0f);

    /// <summary>OCG 主色调（暖金）。</summary>
    private static readonly Color ModeColorOCG = new Color(0.85f, 0.74f, 0.44f, 1f);

    /// <summary>RD 主色调（琥珀）。</summary>
    private static readonly Color ModeColorRD = new Color(0.98f, 0.64f, 0.22f, 1f);

    /// <summary>挂 RD 角标的两项（_plan_rdmode.md：卡组编辑 / 联机模式；2026-09-19 扩：人机 / 录像；2026-09-20 扩：资源更新）。</summary>
    private static readonly string[] RdBadgeItems = { "ai", "deck", "online", "replay", "rdUpdate" };

    /// <summary>角标节点名 → 节点（RD 时亮、OCG 时灭）。</summary>
    private readonly Dictionary<string, GameObject> rdBadges = new Dictionary<string, GameObject>();

    /// <summary>菜单项文字的原值（挂角标不改文字，这里是给将来要加后缀时留的锚）。</summary>
    private readonly Dictionary<string, string> rdItemOriginalText = new Dictionary<string, string>();

    /// <summary>
    /// RD 模式下**保留**的主菜单项（用户 2026-09-19 定稿，同日扩项）。
    ///
    /// 口径：RD 下只留「RD 能用的入口」，其余（单人/我的卡/超先行卡/资源下载）
    /// 一律隐藏并紧凑重排 —— 摆在 RD 菜单里点了也用不了，是纯误导。
    ///
    /// `ai`（人机）与 `replay`（录像）**在列**：两者都有 RD 实现了 ——
    ///   · `ai`     → AIRoom 按 GameModeManager 取 rd/bot.conf / rd/ai/ 起进程（见 EnsureBots）；
    ///   · `replay` → selectReplay 只列 rd/replay/（RD 局录像也落这里，见 GameModeManager.ReplayDir）。
    /// 这两项还挂 RD 角标（见 RdBadgeItems），提示「点进去的是 RD 那一套」。
    ///
    /// `rdUpdate`（资源更新）也在列：RD 数据的 ypk 更新通道（见 RdDataUpdater）——
    /// 点一下热更新 rd/update/ 里的待应用包并把文件夹打开，**只有 RD 有这一项**
    /// （OCG 那边的在线更新走 supreCards「资源下载」，两条通道互不掺和）。
    ///
    /// ⚠ 两项是**必须留**的，别顺手一起藏了：
    ///   · `setting` —— RD 的「召唤前询问」「盖放前询问」开关就在设置里（而且按模式分键，
    ///     见 GameModeManager.KeyAskMset），藏了就再也改不了 RD 的这两项；
    ///   · `exit`   —— 藏了就没有关闭游戏的入口（只能点窗口右上角的叉）。
    /// 展示顺序仍按 <see cref="MenuItemOrder"/>（这里只做过滤，不另定义一个顺序）。
    /// </summary>
    private static readonly string[] RdVisibleItems = { "ai", "replay", "deck", "online", "setting", "rdUpdate", "exit" };

    //GameObject screen;
    public override void initialize()
    {
        // 这个提示文本是早期版本遗留的，当前并未被使用。
        // 注意：config/hint.conf 由内置资源包解出，如果它缺失，直接 ReadAllText 会抛异常，
        // 导致整个 initialize 中断、主菜单完全不显示，所以这里做容错读取。
        string hint = File.Exists("config/hint.conf")
            ? File.ReadAllText("config/hint.conf")
            : string.Empty;
        createWindow(Program.I().new_ui_menu);
        CreateSuperPreMenuItem();
        CreateExitMenuItem();
        CreateRdUpdateMenuItem();
        CreateRdChip();
        CreateSponsorButton();
        ArrangeMenuItems();
        UIHelper.registEvent(gameObject, "setting_", onClickSetting);
        UIHelper.registEvent(gameObject, "deck_", onClickSelectDeck);
        UIHelper.registEvent(gameObject, "superPre_", onClickSuperPre);
        UIHelper.registEvent(gameObject, "online_", onClickOnline);
        UIHelper.registEvent(gameObject, "myCard_", onClickMyCard);
        UIHelper.registEvent(gameObject, "replay_", onClickReplay);
        UIHelper.registEvent(gameObject, "single_", onClickPizzle);
        UIHelper.registEvent(gameObject, "ai_", onClickAI);
        UIHelper.registEvent(gameObject, "exit_", onClickExit);
        // 将 onClickDownload 的事件注册指向我们新的实现
        UIHelper.registEvent(gameObject, "supreCards_", onClickUpdateResources);
        // RD 专属「资源更新」的回调在 CreateRdUpdateMenuItem 里就地挂（OCG 下该行
        // 已被 SetActive(false)，registEvent 的 getByName 只搜激活对象、永远找不到它）。

        // 客户端本体版本检查（v1.3）：**每次进程只静默查一次**（initialize 会因返回主菜单重跑）。
        // 用静态标志挡重复；失败静默（不影响游戏），有新版才发一次提示（ClientSelfUpdate 内部按版本去重）。
        if (!_clientUpdateChecked)
        {
            _clientUpdateChecked = true;
            ClientSelfUpdate.CheckAsync(true);
        }
    }

    /// <summary>本次进程是否已发起过客户端更新检查（initialize 会被重复调用）。</summary>
    private static bool _clientUpdateChecked = false;

    // ============================ 主菜单视觉规格 ============================
    // 一律以 YGOPro2（PC 原生）的 Assets/transUI/prefab/trans_menu.prefab 为准：
    //     节点 x = 48，首项 y = 103.4，项间距 40，标签字号 20，
    //     图标挂在节点的 "Texture" 子节点（x = -110, y = 3），
    //     按钮是该节点的子节点（名为「节点名 + _」），文字挂在按钮下面，
    //     背景板 back = 210 x 344 且居中原点（上边距 68.6 / 下边距 35.4，顶部留给版本号）。
    // 移动版分支（KoishiPro2）把字号放大到 30、间距拉到 47.3、按钮整体左移 13，
    // 这三处正是「一眼看着不像 YGOPro2」的来源，已全部改回原生取值。

    /// <summary>主菜单各项的展示顺序（自上而下）。rdUpdate 只在 RD 下可见（见 OcgHiddenItems）。</summary>
    private static readonly string[] MenuItemOrder =
    {
        "ai",
        "online",
        "myCard",
        "replay",
        "single",
        "deck",
        "superPre",
        "setting",
        "supreCards",
        "rdUpdate",
        "exit",
    };

    /// <summary>OCG 模式下**隐藏**的菜单项（RD 专属入口放这里，OCG 看不到）。</summary>
    private static readonly string[] OcgHiddenItems = { "rdUpdate" };

    /// <summary>
    /// 每项使用的图标精灵名（同一张 transAtlas）。
    /// 前 7 项即 YGOPro2 原生取值，顺带修掉上游模板的两处重复：
    /// online 原来错写成 ai（与「人机模式」同为显示器图标），
    /// 运行时代码克隆 setting 造「退出游戏」时又没换图标（两个齿轮）。
    /// 末 3 项 YGOPro2 没有对应入口，从同一张图集里另选体量相近的图标，
    /// 并避开已被占用的语义：MyCard 不用 wlan（地球已归「联机模式」）而用 link，
    /// 超先行卡沿用 new，资源下载沿用 file。
    /// </summary>
    private static readonly string[] MenuItemIcon =
    {
        "ai", "wlan", "link", "replay", "puzzle", "deck", "new", "setting", "file", "file", "exit",
    };

    /// <summary>
    /// 图标色调补偿，与 MenuItemIcon 一一对应。
    ///
    /// transAtlas 里只有 YGOPro2 原生那 7 个图标是 #D6D6D6（214），
    /// 其余精灵（含借来的 link / new / file）都是 #FFFFFF（255）。
    /// 直接摆在一起，MyCard / 超先行卡 / 资源下载 会比同级项亮一档，一眼看出是「拼进来的」。
    /// UISprite.color 只能往下压，所以把偏白的这三个乘到原生灰。
    /// </summary>
    private static readonly float[] MenuItemIconTone =
    {
        1f, 1f, UIHelper.NativeIconTone, 1f, 1f, 1f, UIHelper.NativeIconTone, 1f,
        UIHelper.NativeIconTone, UIHelper.NativeIconTone, 1f,
    };

    /// <summary>按钮子节点“节点名_”相对节点原点的 x（YGOPro2 原值，随图标宽度略有差异）。</summary>
    private static readonly float[] MenuItemButtonX =
    {
        -36f, -34f, -34f, -34f, -34f, -35f, -35f, -35f, -35f, -35f, -33f,
    };

    /// <summary>文字相对按钮原点的 x（YGOPro2 原值）。</summary>
    private static readonly float[] MenuItemLabelX =
    {
        31f, 29f, 29f, 29f, 29f, 31f, 31f, 32f, 32f, 32f, 30f,
    };

    /// <summary>菜单节点整体的横向位置（YGOPro2 原值）。</summary>
    private const float MenuItemX = 48f;

    /// <summary>最上方菜单项的 y 坐标（YGOPro2 原值）。</summary>
    private const float MenuItemTopY = 103.4f;

    /// <summary>相邻菜单项之间的垂直间距（YGOPro2 原值）。</summary>
    private const float MenuItemSpacing = 40f;

    /// <summary>菜单文字字号（YGOPro2 原值；移动版分支是 30，会把字撑得几乎顶满面板）。</summary>
    private const int MenuItemFontSize = 20;

    /// <summary>
    /// 图标统一画在 32x32 的方框里（YGOPro2 原值：原生 7 个图标的 UISprite 全是 32x32）。
    /// 移动版分支把它整体放大到了 48x48，于是图标比 20 号字大出一圈。
    /// YGOPro2 对非方形图标也是这样拉进方框的，所以这里对借用的 new / file / link 一视同仁。
    /// </summary>
    private const int MenuItemIconSize = 32;

    /// <summary>背景板上边缘到首项中心的距离（YGOPro2：172 - 103.4）。</summary>
    private const float MenuPanelTopMargin = 68.6f;

    /// <summary>背景板下边缘到末项中心的距离（YGOPro2：172 - 136.6）。</summary>
    private const float MenuPanelBottomMargin = 35.4f;

    /// <summary>
    /// 背景板几何中心相对根原点的偏移量。
    ///
    /// YGOPro2 原生是 7 项，代入后上缘 172、下缘 -172，中心恰好 0（面板天然居中）；
    /// 本项目多出 MyCard / 超先行卡 / 资源下载 3 项，背景板向下多伸 3×40=120，
    /// 几何中心就下沉 60。把这个偏移从所有子元素的 y 上统一减掉，
    /// 面板即在默认根坐标（0,0 = 屏幕中心）下精确居中，且项数再变动时自动保持居中。
    ///
    /// ⚠ 项数取的是**当前模式可见项**，不是 MenuItemOrder.Length ——
    ///   RD 下只留 4 项（见 RdVisibleItems），用 10 项算出来的偏移会把整块面板顶偏。
    /// </summary>
    private static float MenuVerticalCenterOffset()
    {
        float top = MenuItemTopY + MenuPanelTopMargin;
        float bottom = MenuItemTopY
            - MenuItemSpacing * (VisibleMenuItems().Length - 1)
            - MenuPanelBottomMargin;
        return (top + bottom) * 0.5f;
    }

    /// <summary>
    /// 当前模式下**显示**的菜单项，顺序沿用 <see cref="MenuItemOrder"/>。
    ///
    /// OCG = 全部十项减去 <see cref="OcgHiddenItems"/>（RD 专属入口不露头）；
    /// RD = 只留 <see cref="RdVisibleItems"/> 里的几项。
    /// 位置相关的计算（纵向居中偏移、面板高度、按钮 y）全部走这里，
    /// 别再直接读 MenuItemOrder.Length，否则 RD 下会按 11 项排版、按钮与面板对不上。
    /// </summary>
    private static string[] VisibleMenuItems()
    {
        List<string> list = new List<string>();
        for (int i = 0; i < MenuItemOrder.Length; i++)
        {
            if (!GameModeManager.IsRD)
            {
                if (Array.IndexOf(OcgHiddenItems, MenuItemOrder[i]) < 0)
                {
                    list.Add(MenuItemOrder[i]);
                }
            }
            else if (Array.IndexOf(RdVisibleItems, MenuItemOrder[i]) >= 0)
            {
                list.Add(MenuItemOrder[i]);
            }
        }
        return list.ToArray();
    }

    /// <summary>该项在当前模式下可见吗（= 它在可见清单里的序号，-1 表示本模式隐藏）。</summary>
    private static int VisibleRank(string itemName, string[] visible)
    {
        for (int i = 0; i < visible.Length; i++)
        {
            if (visible[i] == itemName)
            {
                return i;
            }
        }
        return -1;
    }

    /// <summary>
    /// 把根节点拉回客户区正中。
    ///
    /// WindowServantSP.createWindow 会把 config.conf 里的
    /// x_&lt;窗口名&gt; / y_&lt;窗口名&gt; × 屏幕尺寸 直接写进根节点 localPosition，
    /// 效果就是整块菜单在客户区里平移。历史上主菜单的存档被写坏过
    /// （实测 y = −4563，即整块菜单被压低约 45 px），这就是「主菜单默认偏下」的另一半原因。
    ///
    /// 注意这里**不能**把根节点的 y 当成"再减一次"加进子元素的补偿里：根节点
    /// localPosition 是父节点（≈屏幕像素）单位，而子元素坐标会被根节点自身的
    /// localScale（= Screen.height / 700，见 WindowServantSP.fixScreenProblem）
    /// 放大约 1.41 倍，两者单位不同，直接相减会多减 0.41 倍。
    /// 所以这里直接把根节点归零——本工程窗口不可拖动，存档里只可能留有历史值，
    /// 归零后 ES_quit 还会把它写回 0，状态自洽。
    /// </summary>
    private void NormalizeMenuRoot()
    {
        Transform root = gameObject.transform;
        Vector3 p = root.localPosition;
        if (p.x != 0f || p.y != 0f)
        {
            root.localPosition = new Vector3(0f, 0f, p.z);
        }
    }

    /// <summary>
    /// 按 YGOPro2 的规格统一排布主菜单：位置、图标、字号、文字偏移、背景板一次性算好。
    ///
    /// 上游模板把 ai / single 两个节点留在了与其它项冲突的坐标上
    /// （ai(49.6) 与 myCard(50.3) 几乎重合），所以位置一律由这里算出，
    /// 模板里的坐标只当占位。
    ///
    /// 只在 `initialize` 里调（本工程的窗口不可拖动）。**模式切换时不要调这个**，
    /// 调 <see cref="ApplyItemLayout"/> —— 区别是前者还会把根节点归零，
    /// 那是「首次进场」才需要的一次性动作。
    /// </summary>
    private void ArrangeMenuItems()
    {
        // 先把根节点归零，再按面板重心补偿子元素，两级都回到客户区正中。
        NormalizeMenuRoot();
        ApplyItemLayout();
    }

    /// <summary>
    /// 只做「可见项 + 位置 + 皮肤 + 面板高度」这一段，**幂等**，可以随模式切换反复调。
    ///
    /// ⚠ 位置一律按「**可见序号**」算，不是 MenuItemOrder 的下标 ——
    ///   RD 下把 6 项藏掉之后，留下的 4 项必须**紧凑**排（中间不留空档），并整体重新居中。
    /// </summary>
    private void ApplyItemLayout()
    {
        float verticalOffset = MenuVerticalCenterOffset();
        string[] visible = VisibleMenuItems();
        Transform root = gameObject.transform;
        for (int i = 0; i < MenuItemOrder.Length; i++)
        {
            Transform item = root.Find(MenuItemOrder[i]);
            if (item == null)
            {
                UnityEngine.Debug.LogWarning("[Menu] 缺少菜单项节点：" + MenuItemOrder[i]);
                continue;
            }
            int rank = VisibleRank(MenuItemOrder[i], visible);
            // 激活态就是「本模式可不可见」：隐藏的项连图标/按钮一起收起来，
            // 否则它们虽然排在面板外，仍会被光标命中（NGUI 的碰撞盒不看可见性）。
            bool show = rank >= 0;
            if (item.gameObject.activeSelf != show)
            {
                item.gameObject.SetActive(show);
            }
            if (show)
            {
                item.localPosition = new Vector3(
                    MenuItemX,
                    MenuItemTopY - MenuItemSpacing * rank - verticalOffset,
                    item.localPosition.z
                );
            }
            // 皮肤照刷（含隐藏项）：切回 OCG 时要立刻是正确形态，不能等下一轮。
            // ApplyItemSkin 的下标必须是 MenuItemOrder 的下标（图标表/按钮名都按它索引）。
            ApplyItemSkin(item, i);
        }

        // 版本号恢复 YGOPro2 的原位与缩放（面板顶部居中偏右、整体缩小到 0.87）。
        // 文字也在这里换成本产品名（prefab 会被运行时覆盖，别改 prefab 里的 mText）；
        // 版本号只显示本产品版本 v1.3（上游协议号走 Config.ClientVersion，不在此暴露）。
        Transform version = root.Find("version_");
        if (version != null)
        {
            UILabel verLabel = version.GetComponentInChildren<UILabel>();
            if (verLabel != null)
            {
                verLabel.text = VersionBaseText;
                _versionLabel = verLabel;
            }
            version.localPosition = new Vector3(2f, 151.4f - verticalOffset, version.localPosition.z);
            version.localScale = new Vector3(0.87f, 0.87f, 0.87f);
        }

        FitMenuBack();
    }

    /// <summary>把单项的图标、字号与横向偏移调回 YGOPro2 的比例。</summary>
    private void ApplyItemSkin(Transform item, int index)
    {
        if (index >= MenuItemIcon.Length)
        {
            return;
        }

        UISprite icon = UIHelper.getByName<UISprite>(item.gameObject, "Texture");
        if (icon != null)
        {
            icon.spriteName = MenuItemIcon[index];
            icon.width = MenuItemIconSize;
            icon.height = MenuItemIconSize;
            float tone = index < MenuItemIconTone.Length ? MenuItemIconTone[index] : 1f;
            icon.color = new Color(tone, tone, tone, 1f);
            TraceIcon(item, index, icon, tone);
        }

        Transform button = item.Find(MenuItemOrder[index] + "_");
        if (button == null)
        {
            return;
        }

        Vector3 buttonPosition = button.localPosition;
        button.localPosition = new Vector3(MenuItemButtonX[index], buttonPosition.y, buttonPosition.z);

        UILabel label = button.GetComponentInChildren<UILabel>();
        if (label == null)
        {
            return;
        }
        label.fontSize = MenuItemFontSize;
        Vector3 labelPosition = label.transform.localPosition;
        label.transform.localPosition = new Vector3(MenuItemLabelX[index], labelPosition.y, labelPosition.z);
    }

    /// <summary>
    /// 把菜单项图标的精灵名、色调与屏幕坐标落一条轨迹。
    ///
    /// 排查用：图标色调只在 UISprite.color 上体现，截图量灰度又会被hover高亮、
    /// 版本号文字、彩色图标（地球/MyCard）带偏。直接报「用了哪个精灵 + 乘了多少」
    /// 才是可判定的证据，屏幕坐标则让验收脚本能在正确的位置取样。
    /// </summary>
    private static void TraceIcon(Transform item, int index, UISprite icon, float tone)
    {
        if (icon == null || !QuickTestTrace.Enabled)
        {
            return;
        }
        Vector3 sp = Program.camera_main_2d.WorldToScreenPoint(icon.transform.position);
        QuickTestTrace.Log("icon", item.name
            + " sprite=" + MenuItemIcon[index]
            + " tone=" + tone.ToString("0.###")
            + " screen=(" + Mathf.RoundToInt(sp.x) + "," + Mathf.RoundToInt(Screen.height - sp.y) + ")");
    }

    /// <summary>菜单显示后重报一次坐标：此时布局与分辨率都已落定，取样点才准。</summary>
    private void TraceAllIcons()
    {
        if (gameObject == null)
        {
            return;
        }
        Transform root = gameObject.transform;
        for (int i = 0; i < MenuItemOrder.Length; i++)
        {
            Transform item = root.Find(MenuItemOrder[i]);
            if (item == null)
            {
                continue;
            }
            float tone = i < MenuItemIconTone.Length ? MenuItemIconTone[i] : 1f;
            TraceIcon(item, i, UIHelper.getByName<UISprite>(item.gameObject, "Texture"), tone);

            // 验收脚本要用真实光标按下去，而 [icon] 报的是图标 —— 图标在节点里偏左 75 局部单位，
            // 拿它去点会落在按钮外。这里把按钮自己的落定坐标一并报出来。
            Transform button = item.Find(MenuItemOrder[i] + "_");
            if (button != null)
            {
                Vector3 bp = Program.camera_main_2d.WorldToScreenPoint(button.position);
                // ⚠ 一定要报 active：RD 下有一半菜单项是**隐藏**的，
                //   它们的坐标仍然算得出来（变换还在），照着坐标去点等于点空。
                //   验收脚本判「RD 只留四项」就靠这个字段，别删。
                QuickTestTrace.Log("btn", MenuItemOrder[i] + "_ screen=("
                    + Mathf.RoundToInt(bp.x) + "," + Mathf.RoundToInt(Screen.height - bp.y) + ")"
                    + " active=" + item.gameObject.activeInHierarchy);
            }
        }
    }

    /// <summary>
    /// 背景板按项目数量一次性算好：上边缘固定在与 YGOPro2 七项版相同的高度，
    /// 向下扩展到恰好容纳全部项目（下边距沿用原生比例）。
    /// 取代上游「每插入一项就加高一次、并把整个菜单对象上移一次」的累加写法——
    /// 那种写法让面板高度与位置依赖插入顺序，且两项补偿会重复叠加。
    /// </summary>
    private void FitMenuBack()
    {
        float verticalOffset = MenuVerticalCenterOffset();
        float lastItemY = MenuItemTopY
            - MenuItemSpacing * (VisibleMenuItems().Length - 1)
            - verticalOffset;
        float top = MenuItemTopY + MenuPanelTopMargin - verticalOffset;
        float bottom = lastItemY - MenuPanelBottomMargin;
        float height = top - bottom;
        float centerY = (top + bottom) * 0.5f;

        Transform back = gameObject.transform.Find("back");
        if (back != null)
        {
            back.localPosition = new Vector3(back.localPosition.x, centerY, back.localPosition.z);
            UIWidget widget = back.GetComponent<UIWidget>();
            if (widget != null)
            {
                widget.height = Mathf.RoundToInt(height);
            }
            BoxCollider collider = back.GetComponent<BoxCollider>();
            if (collider != null)
            {
                collider.size = new Vector3(collider.size.x, height, collider.size.z);
                collider.center = Vector3.zero;
            }
        }

        UIPanel panel = gameObject.GetComponent<UIPanel>();
        if (panel != null)
        {
            Vector4 clip = panel.baseClipRegion;
            // NGUI 的 baseClipRegion 是 (中心x, 中心y, 宽, 高) —— **x/y 是中心、不是左/下边**，
            // 所以「往某一侧放宽」= 改中心并对称扩宽。
            //
            // 横向：模板给的 210 只够放下十行按钮（180 宽）与版本号，**放不下 RD 入口**
            // （这一版入口摆在面板**左**侧外）。这里按芯片的实际几何把裁剪区**双向**放宽 ——
            // 只放宽裁剪区、**不动 back 的宽度** ⇒ 灰色底板外观不变，入口就是「浮在板外」。
            // 式子是自洽幂等的：读到的是刚放宽后的值，第二次调用不会再涨。
            Vector4 box = ChipButtonBoxInPanel();
            // 「支持作者」钉在右下角面板外，同 RD 入口一样会被 SoftClip 切掉，一并并进来。
            Vector4 sbox = SponsorButtonBoxInPanel();
            float clipLeft = Mathf.Min(clip.x - clip.z * 0.5f, Mathf.Min(box.x, sbox.x) - MenuClipMargin);
            float clipRight = Mathf.Max(clip.x + clip.z * 0.5f, Mathf.Max(box.z, sbox.z) + MenuClipMargin);
            // 纵向同理合并：面板高度按可见项个数算（OCG 10 项 / RD 7 项差 120 个单位），
            // 而两颗按钮的 y 都是**常量** —— 不并进来，面板一矮（切到 RD）就被裁掉。
            float clipBottom = Mathf.Min(centerY - height * 0.5f, Mathf.Min(box.y, sbox.y) - MenuClipMargin);
            float clipTop = Mathf.Max(centerY + height * 0.5f, Mathf.Max(box.w, sbox.w) + MenuClipMargin);

            clip.x = (clipLeft + clipRight) * 0.5f;
            clip.z = clipRight - clipLeft;
            clip.y = (clipBottom + clipTop) * 0.5f;
            clip.w = clipTop - clipBottom;
            panel.baseClipRegion = clip;
            // 静态基线换了 ⇒ 让 FitMenuClipForChip 下一帧重新认一次（它按这条基线做并集）。
            menuClipBase = new Vector4(float.NaN, 0f, 0f, 0f);
        }
    }

    /// <summary>
    /// 每帧把裁剪区并上**芯片当前**的包围盒。
    ///
    /// 用户 2026-09-19：「切换模式的按钮在**主菜单移动距离大**时还是会被隐藏」。
    ///
    /// 为什么会被藏：徽标是**世界坐标钉死**的（<see cref="PinRdChip"/>），而菜单面板自己
    /// 会整体平移/缩放 ⇒ 徽标在**面板局部坐标**里的位置不再等于常量 (RdChipX, RdChipY)。
    /// 而 <see cref="FitMenuBack"/> 的裁剪区是照**常量**算的（它只保证「菜单停在正位时」盖得住），
    /// 面板一挪，徽标就落到裁剪区外 —— 菜单挪得越多，被 SoftClip 切掉的越多，整颗切没就是「被隐藏」。
    /// 面板的位移在工程内确实存在：`WindowServantSP.createWindow()` 会把配置里的
    /// `x_/y_<窗口名>`（用户上一次退出时的落点）写进根节点 localPosition。
    ///
    /// 口径：**徽标钉在哪，裁剪区就跟到哪**。从 <see cref="menuClipBase"/> 起算（不就地累加），
    /// 所以面板回到正位时裁剪区也会自己收回去，不会越并越大。
    /// 代价只是一次四角换算 + 一次比较；数值没变时 `baseClipRegion` 的 setter 自己会跳过，
    /// 不会每帧触发 NGUI 重建。
    /// </summary>
    private void FitMenuClipForChip()
    {
        UIPanel panel = gameObject.GetComponent<UIPanel>();
        if (panel == null)
        {
            return;
        }
        if (float.IsNaN(menuClipBase.x))
        {
            menuClipBase = panel.baseClipRegion;
        }

        // 两颗固定按钮（RD 入口 / 支持作者）各自（含底板）的世界包围盒 → 面板局部
        // （支持作者没有底板，sponsorPlate 位传 null，CollectFixedChipCorners 自己会跳过）
        float minX = float.MaxValue;
        float minY = float.MaxValue;
        float maxX = float.MinValue;
        float maxY = float.MinValue;
        CollectFixedChipCorners(panel, RdChipNode, RdChipButton, rdChipPlate,
            ref minX, ref minY, ref maxX, ref maxY);
        CollectFixedChipCorners(panel, SponsorNode, SponsorButton, null,
            ref minX, ref minY, ref maxX, ref maxY);
        if (minX == float.MaxValue)
        {
            // 一颗都没采到（按钮节点都还没造）——保持基线不动。
            return;
        }

        Vector4 clip = menuClipBase;
        float left = Mathf.Min(clip.x - clip.z * 0.5f, minX - MenuClipMargin);
        float right = Mathf.Max(clip.x + clip.z * 0.5f, maxX + MenuClipMargin);
        float bottom = Mathf.Min(clip.y - clip.w * 0.5f, minY - MenuClipMargin);
        float top = Mathf.Max(clip.y + clip.w * 0.5f, maxY + MenuClipMargin);
        panel.baseClipRegion = new Vector4(
            (left + right) * 0.5f, (bottom + top) * 0.5f, right - left, top - bottom);
    }

    /// <summary>
    /// 把一颗固定按钮（含底板）的世界包围盒并入 (minX..maxY)。找不到节点/控件就静默跳过，
    /// 不影响另一颗 —— 两颗按钮彼此独立，谁在谁不在都不该拖垮裁剪区对账。
    /// </summary>
    private void CollectFixedChipCorners(
        UIPanel panel, string nodeName, string buttonName, UISprite plate,
        ref float minX, ref float minY, ref float maxX, ref float maxY)
    {
        Transform chip = gameObject.transform.Find(nodeName);
        if (chip == null)
        {
            return;
        }
        Transform button = UIHelper.getByName<Transform>(chip.gameObject, buttonName);
        UIWidget w = (button != null ? button.gameObject : chip.gameObject).GetComponent<UIWidget>();
        if (w != null)
        {
            Vector3[] corners = w.worldCorners;   // NGUI 内部复用数组，不产生 GC
            for (int i = 0; i < corners.Length; i++)
            {
                Vector3 lp = panel.transform.InverseTransformPoint(corners[i]);
                if (lp.x < minX) minX = lp.x;
                if (lp.y < minY) minY = lp.y;
                if (lp.x > maxX) maxX = lp.x;
                if (lp.y > maxY) maxY = lp.y;
            }
        }
        if (plate != null)
        {
            Vector3[] pc = plate.worldCorners;
            for (int i = 0; i < pc.Length; i++)
            {
                Vector3 lp = panel.transform.InverseTransformPoint(pc[i]);
                if (lp.x < minX) minX = lp.x;
                if (lp.y < minY) minY = lp.y;
                if (lp.x > maxX) maxX = lp.x;
                if (lp.y > maxY) maxY = lp.y;
            }
        }
    }

    /// <summary>
    /// RD 入口按钮的包围盒（面板局部坐标，x=左 y=下 z=右 w=上），供
    /// <see cref="FitMenuBack"/> 把裁剪区放宽到能容下它。
    ///
    /// 芯片是主菜单行按钮的克隆体，子按钮的局部位置/尺寸都是**模板值**
    /// （ArrangeMenuItems 不管 chip 内部），所以盒子 = 芯片根位置 + 按钮局部几何 × 芯片缩放。
    ///
    /// ⚠ 读的是**常量** RdChipX / RdChipY，不是芯片当前的 localPosition ——
    ///   这样算式与「PositionRdChip 有没有先跑」无关（首次造芯片时 FitMenuBack 可能先被调到）。
    ///   芯片几何若变（换克隆源、改 scale），这四个数要跟着核一遍。
    /// </summary>
    private static Vector4 ChipButtonBoxInPanel()
    {
        return ChipButtonBoxInPanelAt(RdChipX, RdChipY);
    }

    /// <summary>
    /// 通用版：<see cref="ChipButtonBoxInPanel"/> 的算式对任何「克隆 setting 行、
    /// 同缩放（RdChipScale）、位置钉在 (chipX, chipY)」的固定按钮都成立
    /// （「支持作者」按钮即 <see cref="SponsorButtonBoxInPanel"/>）。
    /// </summary>
    private static Vector4 ChipButtonBoxInPanelAt(float chipX, float chipY)
    {
        // 克隆源是「系统设置」那一行：子按钮局部 (−48, 0)、尺寸 180×50（模板值，2026-09-19 实测）。
        const float buttonLocalX = -48f;
        const float buttonLocalY = 0f;
        const float buttonWidth = 180f;
        const float buttonHeight = 50f;
        float cx = chipX + buttonLocalX * RdChipScale;
        float cy = chipY + buttonLocalY * RdChipScale;
        float hw = buttonWidth * 0.5f * RdChipScale;
        float hh = buttonHeight * 0.5f * RdChipScale;
        return new Vector4(cx - hw, cy - hh, cx + hw, cy + hh);
    }

    /// <summary>「支持作者」按钮的包围盒（面板局部坐标，口径同 <see cref="ChipButtonBoxInPanel"/>）。</summary>
    private static Vector4 SponsorButtonBoxInPanel()
    {
        Vector2 sp = SponsorPos();
        return ChipButtonBoxInPanelAt(sp.x, sp.y);
    }

    /// <summary>
    /// 主菜单模板里没有「超先行卡」入口，这里克隆「编辑卡组」一行补上。
    /// 本方法只负责把节点造出来；位置、图标、字号与背景板高度统一由
    /// ArrangeMenuItems() 按 YGOPro2 的规格决定，避免两处布局逻辑互相覆盖。
    /// </summary>
    private void CreateSuperPreMenuItem()
    {
        Transform deck = gameObject.transform.Find("deck");
        Transform setting = gameObject.transform.Find("setting");
        Transform resources = gameObject.transform.Find("supreCards");
        if (deck == null || setting == null || resources == null)
        {
            UnityEngine.Debug.LogWarning("[SuperPre] 主菜单模板不完整，无法创建超先行卡入口。");
            return;
        }

        if (gameObject.transform.Find("superPre") != null)
        {
            // 已经创建过（例如从子界面返回主菜单时再次 initialize），不要重复插入。
            return;
        }

        GameObject superPre = UnityEngine.Object.Instantiate(
            deck.gameObject,
            gameObject.transform,
            false
        );
        superPre.name = "superPre";
        // 这里的位置只是占位，最终由 ArrangeMenuItems() 统一排布。
        superPre.transform.localPosition = new Vector3(
            deck.localPosition.x,
            deck.localPosition.y - MenuItemSpacing,
            deck.localPosition.z
        );
        UISprite icon = UIHelper.getByName<UISprite>(superPre, "Texture");
        if (icon != null)
        {
            icon.spriteName = "new";
        }

        Transform button = UIHelper.getByName<Transform>(superPre, "deck_");
        if (button != null)
        {
            button.name = "superPre_";
            UILabel label = button.GetComponentInChildren<UILabel>();
            if (label != null)
            {
                label.text = InterString.Get("超先行卡");
            }
        }

        // 图标 / 字号 / 横向偏移 / 背景板高度统一由 ArrangeMenuItems() -> ApplyItemSkin() / FitMenuBack()
        // 按 YGOPro2 的规格处理，这里不再各自补偿，避免两处布局逻辑互相叠加。
    }

    /// <summary>
    /// 上游主菜单模板没有「退出游戏」入口（移动版靠系统返回键退出），
    /// 这里克隆「系统设置」一行补上。图标不再沿用 setting 的齿轮，
    /// 改由 ApplyItemSkin() 指定 YGOPro2 原生的 exit；位置同样交给
    /// ArrangeMenuItems() 统一决定，避免两处布局逻辑互相覆盖。
    /// </summary>
    private void CreateExitMenuItem()
    {
        Transform setting = gameObject.transform.Find("setting");
        Transform resources = gameObject.transform.Find("supreCards");
        if (setting == null || resources == null)
        {
            UnityEngine.Debug.LogWarning("[Exit] 主菜单模板不完整，无法创建退出入口。");
            return;
        }

        if (gameObject.transform.Find("exit") != null)
        {
            return;
        }

        GameObject exit = UnityEngine.Object.Instantiate(
            setting.gameObject,
            gameObject.transform,
            false
        );
        exit.name = "exit";
        // 这里的位置只是占位，最终由 ArrangeMenuItems() 统一排布。
        exit.transform.localPosition = new Vector3(
            setting.localPosition.x,
            setting.localPosition.y - MenuItemSpacing,
            setting.localPosition.z
        );

        Transform button = UIHelper.getByName<Transform>(exit, "setting_");
        if (button != null)
        {
            button.name = "exit_";
            UILabel label = button.GetComponentInChildren<UILabel>();
            if (label != null)
            {
                label.text = InterString.Get("退出游戏");
            }
        }

        // 图标 / 字号 / 横向偏移 / 背景板高度统一由 ArrangeMenuItems() -> ApplyItemSkin() / FitMenuBack()
        // 按 YGOPro2 的规格处理，这里不再各自补偿，避免两处布局逻辑互相叠加。
    }

    /// <summary>
    /// 上游主菜单模板没有「资源更新」入口，这里克隆「资源下载」（supreCards）一行补上。
    /// **RD 专属**：OCG 那边的在线更新就是 supreCards 本尊，两条通道互不掺和 ——
    /// 所以本项在 OCG 下被 <see cref="OcgHiddenItems"/> 藏掉（见 VisibleMenuItems）。
    /// 位置 / 图标 / 字号照旧交给 ArrangeMenuItems() 统一排布。
    /// </summary>
    private void CreateRdUpdateMenuItem()
    {
        Transform resources = gameObject.transform.Find("supreCards");
        if (resources == null)
        {
            UnityEngine.Debug.LogWarning("[RdUpdate] 主菜单模板不完整，无法创建资源更新入口。");
            return;
        }

        if (gameObject.transform.Find("rdUpdate") != null)
        {
            // 已经创建过（例如从子界面返回主菜单时再次 initialize），不要重复插入。
            return;
        }

        GameObject rdUpdate = UnityEngine.Object.Instantiate(
            resources.gameObject,
            gameObject.transform,
            false
        );
        rdUpdate.name = "rdUpdate";
        // 位置只是占位，最终由 ArrangeMenuItems() 统一排布。
        rdUpdate.transform.localPosition = new Vector3(
            resources.localPosition.x,
            resources.localPosition.y - MenuItemSpacing,
            resources.localPosition.z
        );

        Transform button = UIHelper.getByName<Transform>(rdUpdate, "supreCards_");
        if (button != null)
        {
            button.name = "rdUpdate_";
            UILabel label = button.GetComponentInChildren<UILabel>();
            if (label != null)
            {
                label.text = InterString.Get("资源更新");
            }
            // ⚠ 回调必须在这里就地挂，不能走 initialize 末尾的 UIHelper.registEvent ——
            // registEvent 内部 getByName 用 GetComponentsInChildren<T>()（**默认只搜激活对象**），
            // 而 ArrangeMenuItems（initialize 里先于 registEvent 执行）在 OCG 启动时就把本行
            // SetActive(false) ⇒ registEvent 永远找不到本按钮、静默失败；切到 RD 后点下去
            // 命中的是克隆残留的 onClick → 克隆 MonoDelegate 的 actionInMono（托管委托
            // 不被 Instantiate 序列化）= null → 按下/悬停全通、回调静默无反应（2026-09-20 定位）。
            UIButton uiButton = button.GetComponent<UIButton>();
            if (uiButton != null)
            {
                MonoDelegate d = button.gameObject.GetComponent<MonoDelegate>();
                if (d == null)
                {
                    d = button.gameObject.AddComponent<MonoDelegate>();
                }
                d.actionInMono = onClickRdUpdate;
                uiButton.onClick.Clear();
                uiButton.onClick.Add(new EventDelegate(d, "function"));
            }
        }
        QuickTestTrace.Log("rd", "rdUpdate created btn=" + (button != null)
            + " uibutton=" + (UIHelper.getByName<UIButton>(gameObject, "rdUpdate_") != null));
    }

    // ============================ RD 模式入口 / 徽标 / toast ============================

    /// <summary>
    /// 造「RD 模式」入口：克隆「系统设置」那一行，砍掉图标、缩小、重新命名，挂在**左上角**
    /// （位置口径见 <see cref="RdChipX"/>：与主菜单面板分开、且不随菜单重排移动）。
    ///
    /// 为什么克隆整行而不是从零 new 一个按钮：主菜单行的按钮自带正确的字体、切片贴图、
    /// 碰撞盒与 UIEventTrigger —— 这些从零拼要么缺素材要么踩 NGUI 默认值的坑
    /// （同 CreateSuperPreMenuItem / CreateExitMenuItem 的做法）。
    ///
    /// ⚠ 它**不在** MenuItemOrder 里，所以 ArrangeMenuItems() 不会摆它 ——
    ///   位置只能在这里自己钉，并且要跟着面板重心补偿一起减（同 version_）。
    /// </summary>
    private void CreateRdChip()
    {
        if (gameObject.transform.Find(RdChipNode) != null)
        {
            // 已经造过（从子界面返回主菜单时 initialize 会再跑一次），别插第二颗。
            return;
        }
        Transform setting = gameObject.transform.Find("setting");
        if (setting == null)
        {
            UnityEngine.Debug.LogWarning("[RD] 主菜单模板不完整，无法创建 RD 入口。");
            return;
        }

        GameObject chip = UnityEngine.Object.Instantiate(setting.gameObject, gameObject.transform, false);
        chip.name = RdChipNode;
        chip.transform.localScale = new Vector3(RdChipScale, RdChipScale, 1f);
        PositionRdChip();

        // 小按钮不要图标（克隆来的图标是设置齿轮，留着反而像第二个「系统设置」）。
        Transform icon = chip.transform.Find("Texture");
        if (icon != null)
        {
            UnityEngine.Object.Destroy(icon.gameObject);
        }

        Transform button = UIHelper.getByName<Transform>(chip, "setting_");
        if (button != null)
        {
            button.name = RdChipButton;
            UILabel label = button.GetComponentInChildren<UILabel>();
            if (label != null)
            {
                label.fontSize = RdChipFontSize;
            }
            UIHelper.registEvent(gameObject, RdChipButton, onClickRdChip);
        }
        else
        {
            UnityEngine.Debug.LogWarning("[RD] 找不到克隆按钮 setting_，RD 入口点不动。");
        }

        CreateRdChipPlate(button);
        CreateRdBadges();
        RefreshRdUi();
    }

    /// <summary>
    /// 造「支持作者」入口（最右下角，点击跳爱发电）：<see cref="CreateRdChip"/> 的同款做法 ——
    /// 克隆「系统设置」一行（自带正确的字体、切片贴图、碰撞盒与 UIEventTrigger，
    /// 从零拼要么缺素材要么踩 NGUI 默认值的坑），砍掉图标、缩小、改文案，钉在角上。
    ///
    /// ⚠ **不带深色底板**（用户 2026-09-24 定稿：「不要有黑色背景」）——
    ///   RD 徽标那块底板是用户点名要的，这里用户点名不要，两者口径相反是各自的定稿。
    ///
    /// ⚠ 它**不在** MenuItemOrder 里，所以 ArrangeMenuItems() 不会摆它；
    ///   位置由 <see cref="PinSponsor"/> 每帧钉（随窗口宽高比贴角），
    ///   裁剪区由 <see cref="FitMenuBack"/> / <see cref="FitMenuClipForChip"/> 并上
    ///   <see cref="SponsorButtonBoxInPanel"/> 放宽（否则整颗被 SoftClip 切没）。
    ///
    /// ⚠ OCG / RD 两种模式下**都显示**（支持作者不分模式），不需要进 RdVisibleItems。
    /// </summary>
    private void CreateSponsorButton()
    {
        if (gameObject.transform.Find(SponsorNode) != null)
        {
            // 已经造过（从子界面返回主菜单时 initialize 会再跑一次），别插第二颗。
            return;
        }
        Transform setting = gameObject.transform.Find("setting");
        if (setting == null)
        {
            UnityEngine.Debug.LogWarning("[Sponsor] 主菜单模板不完整，无法创建支持作者入口。");
            return;
        }

        GameObject sponsor = UnityEngine.Object.Instantiate(setting.gameObject, gameObject.transform, false);
        sponsor.name = SponsorNode;
        sponsor.transform.localScale = new Vector3(RdChipScale, RdChipScale, 1f);
        PinSponsor();

        // 小按钮不要图标（克隆来的图标是设置齿轮，留着反而像第二个「系统设置」）。
        Transform icon = sponsor.transform.Find("Texture");
        if (icon != null)
        {
            UnityEngine.Object.Destroy(icon.gameObject);
        }

        Transform button = UIHelper.getByName<Transform>(sponsor, "setting_");
        if (button != null)
        {
            button.name = SponsorButton;
            UILabel label = button.GetComponentInChildren<UILabel>();
            if (label != null)
            {
                label.fontSize = RdChipFontSize;
                label.text = InterString.Get("支持作者");
                // 没有底板对账，但克隆源那一行的标签是「为图标让位」偏右的（MenuItemLabelX≈+31），
                // 不归中字会探到按钮右缘外。pivot 归中 + 原点归零 = 文字压住按钮正中
                // （口径同 FitChipPlateCore ①，只做一次，文字不会再变）。
                label.pivot = UIWidget.Pivot.Center;
                label.transform.localPosition = Vector3.zero;
                // 金字悬在天空背景上（用户 2026-09-24 定稿无底板），补黑描边保证亮背景下的可读性
                // （口径同 Menu.cs 里 NEW 角标 / Setting.cs 的 Outline 用法）。
                label.effectStyle = UILabel.Effect.Outline;
                label.effectColor = Color.black;
                label.effectDistance = new Vector2(1.5f, 1.5f);
            }
            UIHelper.registEvent(gameObject, SponsorButton, onClickSponsor);
        }
        else
        {
            UnityEngine.Debug.LogWarning("[Sponsor] 找不到克隆按钮 setting_，支持作者入口点不动。");
        }
    }

    /// <summary>点击「支持作者」：用系统默认浏览器打开爱发电主页，并把浏览器窗口拉到前台。</summary>
    public void onClickSponsor()
    {
        // 探针：分清「回调没被 NGUI 点着」和「回调跑了、OpenURL 没效果」（验收脚本靠这行判）。
        QuickTestTrace.Log("sponsor", "click -> " + SponsorUrl);
        Application.OpenURL(SponsorUrl);
        BringSponsorPageToFront();
    }

    /// <summary>「客户端更新」检查发现新版 / 玩家点「打开下载页」时调：把下载页窗口拉到前台。
    /// 与「支持作者」同一套前台逻辑（见 <see cref="BringSponsorPageToFrontCoroutine"/>），
    /// 只是匹配词换成网盘相关（夸克 / quark）。由 ClientSelfUpdate.OpenDownloadPage 调用。</summary>
    public void BringDownloadPageToFront()
    {
        Program.I().StartCoroutine(BringDownloadPageToFrontCoroutine());
    }

    private System.Collections.IEnumerator BringDownloadPageToFrontCoroutine()
    {
        float deadline = Time.realtimeSinceStartup + SponsorForegroundTimeout;
        System.IntPtr found = System.IntPtr.Zero;
        string foundTitle = null;
        int scanned = 0;
        while (Time.realtimeSinceStartup < deadline)
        {
            scanned++;
            found = FindDownloadPageWindow(out foundTitle);
            if (found != System.IntPtr.Zero)
            {
                break;
            }
            yield return new WaitForSeconds(0.25f);
        }
        if (found == System.IntPtr.Zero)
        {
            QuickTestTrace.Log("clientupdate", "fg miss polls=" + scanned);
            yield break;
        }
        if (IsIconic(found))
        {
            ShowWindow(found, SponsorSWRestore);
        }
        SetForegroundWindow(found);
        yield return new WaitForSeconds(0.5f);
        System.IntPtr fg = GetForegroundWindow();
        System.Text.StringBuilder fgsb = new System.Text.StringBuilder(256);
        GetWindowText(fg, fgsb, 256);
        QuickTestTrace.Log("clientupdate",
            "fg verify " + (fg == found ? "ok" : "fail") + " title=" + fgsb.ToString());
    }

    /// <summary>枚举顶层可见窗口，取第一个标题含「夸克 / quark / pan.quark」的（下载页）。</summary>
    private System.IntPtr FindDownloadPageWindow(out string matchedTitle)
    {
        downloadFoundWindows.Clear();
        downloadMatchedTitle = null;
        EnumWindows(delegate (System.IntPtr h, System.IntPtr _)
        {
            if (!IsWindowVisible(h))
            {
                return true;
            }
            System.Text.StringBuilder sb = new System.Text.StringBuilder(512);
            GetWindowText(h, sb, 512);
            string t = sb.ToString();
            string low = t.ToLowerInvariant();
            if (low.Contains("quark") || t.Contains("夸克") || low.Contains("pan.quark"))
            {
                downloadFoundWindows.Add(h);
                downloadMatchedTitle = t;
                return false;
            }
            return true;
        }, System.IntPtr.Zero);
        matchedTitle = downloadMatchedTitle;
        return downloadFoundWindows.Count > 0 ? downloadFoundWindows[0] : System.IntPtr.Zero;
    }

    private string downloadMatchedTitle;
    private readonly System.Collections.Generic.List<System.IntPtr> downloadFoundWindows
        = new System.Collections.Generic.List<System.IntPtr>();

    // -------- OpenURL 之后把浏览器窗口带到前台（用户 2026-09-24 定稿：「打开网页后要把窗口跳到浏览器」）--------
    //
    // Windows 的防抢焦点规则经常让 ShellExecute 打开的页面**留在游戏后面**——尤其默认浏览器
    // 已经在跑时只是往现有进程开新标签，前台仍是游戏窗口，玩家根本不知道页面开了。
    // 这里开完 URL 后轮询几秒，找到标题含 afdian / 爱发电 的顶层可见窗口，
    // 最小化就先还原，再 SetForegroundWindow 拉到游戏前面。

    private const float SponsorForegroundTimeout = 12f;

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(System.IntPtr hWnd);

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool IsIconic(System.IntPtr hWnd);

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool ShowWindow(System.IntPtr hWnd, int nCmdShow);

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool IsWindowVisible(System.IntPtr hWnd);

    [System.Runtime.InteropServices.DllImport("user32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
    private static extern int GetWindowText(System.IntPtr hWnd, [System.Runtime.InteropServices.Out] System.Text.StringBuilder lpString, int nMaxCount);

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool EnumWindows(SponsorEnumProc lpEnumFunc, System.IntPtr lParam);

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern System.IntPtr GetForegroundWindow();

    private delegate bool SponsorEnumProc(System.IntPtr hWnd, System.IntPtr lParam);

    private const int SponsorSWRestore = 9;

    /// <summary>轮询期间扫到的候选窗口（FindSponsorWindow 的回调往里塞）。</summary>
    private readonly System.Collections.Generic.List<System.IntPtr> sponsorFoundWindows
        = new System.Collections.Generic.List<System.IntPtr>();

    private void BringSponsorPageToFront()
    {
        // Menu 不是协程宿主（无 StartCoroutine），走 Program 全局宿主 —— 与 Ocgcore 里的下载协程同口径。
        Program.I().StartCoroutine(BringSponsorPageToFrontCoroutine());
    }

    private System.Collections.IEnumerator BringSponsorPageToFrontCoroutine()
    {
        float deadline = Time.realtimeSinceStartup + SponsorForegroundTimeout;
        System.IntPtr found = System.IntPtr.Zero;
        string foundTitle = null;
        int scanned = 0;
        while (Time.realtimeSinceStartup < deadline)
        {
            scanned++;
            found = FindSponsorWindow(out foundTitle);
            if (found != System.IntPtr.Zero)
            {
                break;
            }
            yield return new WaitForSeconds(0.25f);
        }
        if (found == System.IntPtr.Zero)
        {
            // 12s 还没见着页面窗口（浏览器极慢 / 被 URL 拦截器拦了）——不硬拉，别把别的窗口误提到前台。
            // 探针报枚举轮数：轮数多而找不到 = 窗口真没出现；轮数少 = 别的原因，看日志再判。
            QuickTestTrace.Log("sponsor", "fg miss polls=" + scanned);
            yield break;
        }
        if (IsIconic(found))
        {
            ShowWindow(found, SponsorSWRestore);
        }
        SetForegroundWindow(found);
        // 回读验证：SetForegroundWindow 可能被系统防抢焦点规则拒掉 —— 0.5s 后看前台到底是谁。
        yield return new WaitForSeconds(0.5f);
        System.IntPtr fg = GetForegroundWindow();
        System.Text.StringBuilder fgsb = new System.Text.StringBuilder(256);
        GetWindowText(fg, fgsb, 256);
        QuickTestTrace.Log("sponsor",
            "fg verify " + (fg == found ? "ok" : "fail") + " title=" + fgsb.ToString());
    }

    /// <summary>枚举顶层可见窗口，取第一个标题含 afdian / 爱发电 的（游戏窗口标题不含这些词，不会误中）。
    /// 扫到的顶层窗口总数走 out 参数（诊断枚举本身是否可用）。</summary>
    private System.IntPtr FindSponsorWindow(out string matchedTitle)
    {
        matchedTitle = null;
        sponsorFoundWindows.Clear();
        sponsorSeenWindows = 0;
        EnumWindows(delegate (System.IntPtr h, System.IntPtr _)
        {
            sponsorSeenWindows++;
            if (!IsWindowVisible(h))
            {
                return true;
            }
            System.Text.StringBuilder sb = new System.Text.StringBuilder(512);
            GetWindowText(h, sb, 512);
            string t = sb.ToString();
            if (t.ToLower().Contains("afdian") || t.Contains("爱发电"))
            {
                sponsorFoundWindows.Add(h);
                sponsorMatchedTitle = t;
                return false;
            }
            return true;
        }, System.IntPtr.Zero);
        matchedTitle = sponsorMatchedTitle;
        return sponsorFoundWindows.Count > 0 ? sponsorFoundWindows[0] : System.IntPtr.Zero;
    }

    /// <summary>枚举诊断：上一轮扫到的顶层窗口总数 / 命中标题（日志用）。</summary>
    private int sponsorSeenWindows;
    private string sponsorMatchedTitle;

    /// <summary>
    /// 给模式徽标配一块**和主菜单同款的深色底板**（用户 2026-09-19：「也配上主菜单一样的黑色底色」）。
    ///
    /// 为什么原来没有：克隆源「系统设置」那一行的按钮虽然带 UISprite，但**精灵名在模板里是空的**
    /// ⇒ 一直是不可见的（RefreshRdUi 里给它上的主色调乘算全落在空气上）。
    /// 于是徽标就是「两行金字直接浮在天空背景上」，看着跟菜单不是一套东西。
    /// 这里借 `back` 那块板的图集与精灵名新造一片，尺寸取按钮自身的 widget 尺寸（180×50）——
    /// 与菜单行同一个观感，也不新增素材。
    ///
    /// ⚠ 底板必须是**白**色：深是精灵自带的。跟着主色调乘算会变成一块暗金，就不是「主菜单那块的底色」了。
    ///
    /// ⚠ 尺寸/位置**不在这里定死**，交给 <see cref="FitRdChipPlate"/> —— 用户 2026-09-19 追加口径：
    ///   「背景阴影与字重合度低，前面有一长部分阴影无字，后面又有字和阴影边缘很近」。
    ///   原委是克隆源那一行**为了让开左侧图标，标签是往右偏的**，而底板是按按钮（180 宽）居中的
    ///   ⇒ 字贴在底板右缘、左边空一大片。底板尺寸这里只给个兜底值，真正的尺寸每帧对账。
    /// </summary>
    private void CreateRdChipPlate(Transform button)
    {
        if (button == null || button.Find(RdChipPlateNode) != null)
        {
            return;
        }
        rdChipPlate = CreateChipPlate(button, RdChipPlateNode);
        rdChipLabel = button.GetComponentInChildren<UILabel>();
        FitRdChipPlate();
    }

    /// <summary>
    /// <see cref="CreateRdChipPlate"/> 的共用实现：
    /// 借 `back` 那块板的图集与精灵名，在 button 下新造一片深色底板并返回。
    /// （「支持作者」用户定稿不要底板，见 CreateSponsorButton —— 共用实现只服务 RD 徽标。）
    /// </summary>
    private UISprite CreateChipPlate(Transform button, string plateNodeName)
    {
        Transform back = gameObject.transform.Find("back");
        UISprite backSprite = back != null ? back.GetComponent<UISprite>() : null;

        GameObject plate = new GameObject(plateNodeName);
        plate.layer = button.gameObject.layer;
        plate.transform.SetParent(button, false);
        plate.transform.localPosition = Vector3.zero;
        plate.transform.localScale = Vector3.one;

        UISprite sp = plate.AddComponent<UISprite>();
        if (backSprite != null)
        {
            sp.atlas = backSprite.atlas;
            sp.spriteName = backSprite.spriteName;
        }
        else
        {
            sp.spriteName = RdChipPlateSprite;
        }
        sp.color = Color.white;
        UIWidget host = button.GetComponent<UIWidget>();
        sp.width = host != null ? host.width : 180;
        sp.height = host != null ? host.height : 50;

        // 压住文字必须比标签**低一层**：NGUI 先按 depth 再按层级顺序画，
        // 底板是后造的（层级顺序在标签之后），同 depth 会盖在字上面。
        // 所以不动底板、把标签抬一层 —— 这样两者都留在原来的 depth 邻域里，
        // 不会因为给底板一个负 depth 而掉到 back 板下面去。
        UILabel label = button.GetComponentInChildren<UILabel>();
        if (label != null)
        {
            sp.depth = label.depth;
            label.depth = label.depth + 1;
        }

        return sp;
    }

    /// <summary>
    /// 把底板与标签摆成**同心**，并让底板尺寸 = 文字 + 四边等宽留白。
    /// （用户 2026-09-19：「背景阴影与字重合度低，前面有一长部分阴影无字，后面又有字和阴影边缘很近」）
    ///
    /// 两个成因，都要在这里修：
    ///   ① **标签偏右**：底板是克隆「系统设置」那一行的按钮做的，那一行为了让开左侧图标，
    ///      标签在按钮里是往右偏的（实测字贴底板右缘、左边空 ≈60px）。徽标已经把图标删了，
    ///      字就该回到正中 ⇒ 标签 pivot 归中 + 原点锚到按钮原点（世界坐标，与父链缩放无关）。
    ///   ② **底板按按钮尺寸走**：按钮 180×50 是「行按钮」的尺寸，跟这几个字没关系 ⇒
    ///      底板尺寸改成按**实测文字**算（`ResizeFreely` 下 label.width/height 就是文字尺寸），
    ///      四周留同样的白，并抬一个最小值兜住两种模式文案的宽窄差。
    ///
    /// 幂等、可每帧调（尺寸/位置没变就直接返回，NGUI 那边也不会因此标脏）。
    /// 之所以要每帧对账：`label.text` 是**下一帧**才排版出新尺寸的，切模式当帧量到的是旧值。
    /// 实现已抽到通用版 <see cref="FitChipPlateCore"/>（「支持作者」底板共用同一套）。
    /// </summary>
    private void FitRdChipPlate()
    {
        FitChipPlateCore(rdChipPlate, rdChipLabel, RdChipPlateMinW, RdChipPlateMinH);
    }

    /// <summary>
    /// <see cref="FitRdChipPlate"/> 的共用实现：对 (plate, label) 这一对做「同心 + 尺寸=文字+留白」。
    /// 任一为 null 直接返回（按钮/底板还没造好时是常态）。
    /// </summary>
    private static void FitChipPlateCore(UISprite plate, UILabel label, int minW, int minH)
    {
        if (plate == null || label == null)
        {
            return;
        }
        Transform button = plate.transform.parent;
        if (button == null)
        {
            return;
        }

        // ① 标签回到按钮正中
        if (label.pivot != UIWidget.Pivot.Center)
        {
            label.pivot = UIWidget.Pivot.Center;
        }
        Vector3 bp = button.position;
        Vector3 lp = label.transform.position;
        if (Mathf.Abs(lp.x - bp.x) > 0.01f || Mathf.Abs(lp.y - bp.y) > 0.01f)
        {
            label.transform.position = new Vector3(bp.x, bp.y, lp.z);
        }

        // ② 底板 = 文字 + 等宽留白（有最小值兜底，两种模式不跳）
        int w = Mathf.Max(minW, Mathf.RoundToInt(label.width) + RdChipPlatePadX * 2);
        int h = Mathf.Max(minH, Mathf.RoundToInt(label.height) + RdChipPlatePadY * 2);
        if (plate.width != w)
        {
            plate.width = w;
        }
        if (plate.height != h)
        {
            plate.height = h;
        }
        // 底板是按钮的子节点、原点就在按钮原点 ⇒ 局部零点即同心。
        if (plate.transform.localPosition != Vector3.zero)
        {
            plate.transform.localPosition = Vector3.zero;
        }
    }

    /// <summary>
    /// 把模式徽标钉在**固定位置**上 —— 用户 2026-09-19 追加口径：
    /// 「ocg 模式和 rd 模式这几个字不和主菜单一起移动，我希望它是单独的」。
    ///
    /// 为什么非要每帧钉：整张主菜单窗口是 `WindowServant2D.show()` 用
    /// `iTween.MoveTo(gameObject, camera_main_2d.ScreenToWorldPoint(Screen.width/2, Screen.height*1.5 → /2), 0.6f)`
    /// **从屏幕下方滑进来**的（菜单自己那句「窗口是 iTween 滑入的」说的就是它），
    /// 徽标是窗口的子节点 ⇒ 天然跟着滑。这里每帧把它的世界位置重新算成
    /// 「窗口停在正位时该在的地方」，于是菜单滑、徽标不滑。
    ///
    /// 落定位置就是相机屏幕中心（iTween 的终点），所以基准点直接用同一个换算取，
    /// 不依赖父节点在哪、缩放到多少。`root.TransformVector` 负责把根坐标系里的
    /// RdChipX/RdChipY 换成世界偏移（含根节点缩放 ≈ Screen.height/700）。
    /// 实现已抽到通用版 <see cref="PinChipAt"/>（「支持作者」按钮共用同一套）。
    /// </summary>

    /// <summary>上一次采样时徽标 / 参照菜单项的屏幕坐标（`(-9999,-9999)` = 还没采过）。</summary>
    private Vector2 chipSlideLastChip = new Vector2(-9999f, -9999f);
    private Vector2 chipSlideLastRef = new Vector2(-9999f, -9999f);

    /// <summary>帧窗里还剩几帧要**无条件**逐帧报（window 内不判「动了没」）。</summary>
    private int chipSlideFramesLeft = 0;

    /// <summary>本帧窗内已报的行号（窗口外补记的行号打 `*`）。</summary>
    private int chipSlideFrameNo = 0;

    /// <summary>全场累计行数（封顶，免得长会话把日志刷爆）。</summary>
    private int chipSlideLines = 0;

    /// <summary>帧窗长度：60 帧 ≈ 1s，罩得住一次进场 / 一次模式重排。</summary>
    private const int ChipSlideWindowFrames = 60;

    /// <summary>全场行数上限（≈6 次进场 × 60 行）。</summary>
    private const int ChipSlideMaxLines = 800;

    /// <summary>
    /// 滑入 / 重排采样（只在 qt_debug.on 下）：**同一行**同时报徽标、一个参照菜单项、
    /// 以及根节点自身的位置与缩放。
    ///
    /// 为什么要这一条：用户口径是「这行字不和主菜单一起移动」——「不动」只有在
    /// **菜单确实在动**的前提下才有意义。只报徽标一个坐标、看到它不变，
    /// 分不清是「钉住了」还是「整张菜单压根没动」，两种情况的修法完全不同。
    /// 所以每行都把参照物一起报出来。
    ///
    /// ⚠ 参照物**不能用菜单根节点**（实测 2026-09-19）：进场时根节点的位置本来就在落点上
    ///   （量出来的是「菜单没动」），所以取**第一个可见菜单项**的按钮——它是子节点，
    ///   父链任何平移/缩放都逃不掉，量到的就是用户眼睛里看到的那个位移。
    ///   根节点的位置/缩放仍然一起报出来，用来区分「根也在动」还是「只有子项在重排」。
    ///
    /// ⚠ 采样策略改过两轮，两种都被实测否掉：
    ///   ① `show()` 后开一个 N 毫秒的时间窗、每 80ms 采一次 —— 有些进场在 show() 返回后的
    ///      第一帧之前就结束了，整段一行都没落；
    ///   ② 改成「只在坐标变化时才记」—— 结果每段只落 1~2 行（19:19 那轮：OCG 1 行 / RD 2 行），
    ///      不足以判「菜单在动」。
    ///   现版是**帧窗 + 变化补记**：`show()` 里开 60 帧窗，窗内逐帧无条件落行（拿到完整轨迹）；
    ///   窗外只在坐标真的变了时补一行（OCG↔RD 切换的面板重排不走 `show()`，靠这条兜住）。
    ///
    /// 🔑 帧窗跑下来量到的真相（2026-09-19，242 行采样）：**主菜单不滑入，是跳变**。
    ///   `show()` 之后 60 帧里 `root=` 恒为 `(0.00,0.00)`、`scale=0.003`，`menu=` 一动不动；
    ///   整场唯一出现过的位移是 OCG↔RD 切换那一下 —— `menu=` 只有两个取值
    ///   `(977,263)`(OCG，10 项) 与 `(977,376)`(RD，6 项)，差整整 113px，与项数差
    ///   4×28.25 吻合（面板按项数重新居中），而且是一帧内到位（所以只留 1 行补记）。
    ///   而 `chip=` 在全部 242 行里恒为 `(467,113)` —— 这才是「徽标不和主菜单一起移动」的正面证据。
    ///
    /// 判据口径（验收脚本按这个读）：**同一场会话内，参照菜单项的屏幕位置极差 ≥ 40px
    /// 而徽标的极差 ≤ 2px** —— 菜单动、徽标不动。
    /// </summary>
    private void TraceChipSlide()
    {
        if (chipSlideLines >= ChipSlideMaxLines)
        {
            return;
        }
        if (!QuickTestTrace.Enabled)
        {
            chipSlideFramesLeft = 0;
            return;
        }
        if (Program.camera_main_2d == null)
        {
            return;
        }

        bool inWindow = chipSlideFramesLeft > 0;

        Transform chip = gameObject.transform.Find(RdChipNode);
        Transform button = chip == null
            ? null
            : UIHelper.getByName<Transform>(chip.gameObject, RdChipButton);
        Transform refItem = MenuRefItem();

        float chipX = float.NaN;
        float chipY = float.NaN;
        float menuX = float.NaN;
        float menuY = float.NaN;
        if (button != null || chip != null)
        {
            Vector3 cp = Program.camera_main_2d.WorldToScreenPoint(
                button != null ? button.position : chip.position);
            chipX = cp.x;
            chipY = Screen.height - cp.y;
        }
        if (refItem != null)
        {
            Vector3 rp = Program.camera_main_2d.WorldToScreenPoint(refItem.position);
            menuX = rp.x;
            menuY = Screen.height - rp.y;
        }

        Vector2 c = new Vector2(chipX, chipY);
        Vector2 r = new Vector2(menuX, menuY);
        bool first = chipSlideLastRef.x < -9000f;
        bool moved = inWindow
            || first
            || Mathf.Abs(c.x - chipSlideLastChip.x) > 0.5f
            || Mathf.Abs(c.y - chipSlideLastChip.y) > 0.5f
            || Mathf.Abs(r.x - chipSlideLastRef.x) > 0.5f
            || Mathf.Abs(r.y - chipSlideLastRef.y) > 0.5f;
        if (!moved)
        {
            return;
        }
        if (inWindow)
        {
            chipSlideFramesLeft--;
        }
        chipSlideLastChip = c;
        chipSlideLastRef = r;
        chipSlideLines++;

        Transform root = gameObject.transform;
        QuickTestTrace.Log("chipslide",
            "mode=" + GameModeManager.ModeLabel
            + " f=" + (inWindow ? chipSlideFrameNo.ToString() : "*")
            + " ref=" + (refItem != null ? refItem.name : "null")
            + " chip=(" + TraceScreenFmt(chipX) + "," + TraceScreenFmt(chipY) + ")"
            + " menu=(" + TraceScreenFmt(menuX) + "," + TraceScreenFmt(menuY) + ")"
            + " root=(" + root.position.x.ToString("F2") + "," + root.position.y.ToString("F2") + ")"
            + " scale=" + root.lossyScale.x.ToString("F3"));
        chipSlideFrameNo++;
    }

    /// <summary>采样行的屏幕坐标格式化（NaN 原样报，别伪装成 0）。</summary>
    private static string TraceScreenFmt(float v)
    {
        return float.IsNaN(v) ? "NaN" : Mathf.RoundToInt(v).ToString();
    }

    /// <summary>取样用的参照菜单项（第一个可见项的按钮）；一项都取不到就返回 null。</summary>
    private Transform MenuRefItem()
    {
        string[] visible = VisibleMenuItems();
        for (int i = 0; i < visible.Length; i++)
        {
            Transform item = gameObject.transform.Find(visible[i]);
            if (item == null)
            {
                continue;
            }
            Transform button = UIHelper.getByName<Transform>(item.gameObject, visible[i] + "_");
            if (button != null)
            {
                return button;
            }
        }
        return null;
    }

    /// <summary>
    /// 把 RD 入口钉回左上角（位置只有这一处，别再散开写）。
    ///
    /// ⚠ **y 是常量，不减 <see cref="MenuVerticalCenterOffset"/>**（用户 2026-09-19 定稿：
    ///   「不随主菜单移动而移动」）。菜单换模式时面板会改高度、十项/六项整体重排，
    ///   这颗按钮不参与 —— 它就在左上角待着。
    ///   历史上它跟着补偿走，于是 OCG↔RD 一切就跳 80 个局部单位（209 → 129），
    ///   看起来像「按钮飘了」；现在的口径是「菜单动、按钮不动」。
    /// 它**不在** MenuItemOrder 里，所以 ArrangeMenuItems() 不管它（同理也得自己钉）。
    /// 位置变了要让 FitMenuBack 重新放宽裁剪区 —— ApplyItemLayout 里是「先 FitMenuBack
    /// 后 PositionRdChip」，但 FitMenuBack 读的是常量，所以顺序无影响。
    /// </summary>
    private void PositionRdChip()
    {
        Transform chip = gameObject.transform.Find(RdChipNode);
        if (chip == null)
        {
            return;
        }
        chip.localScale = new Vector3(RdChipScale, RdChipScale, 1f);
        PinRdChip();
    }

    /// <summary>
    /// 固定按钮钉位通用版：<see cref="PinRdChip"/> 的算式对任何「挂在菜单窗口下、
    /// 位置常量为 (x, y)、不随窗口/面板动」的按钮都成立（「支持作者」即 PinSponsor）。
    /// </summary>
    private void PinChipAt(string nodeName, float x, float y)
    {
        Transform chip = gameObject.transform.Find(nodeName);
        if (chip == null)
        {
            return;
        }
        if (Program.camera_main_2d == null)
        {
            return;
        }
        Vector3 restWorld = Program.camera_main_2d.ScreenToWorldPoint(
            new Vector3(Screen.width * 0.5f, Screen.height * 0.5f, 0f));
        Vector3 want = restWorld
            + gameObject.transform.TransformVector(new Vector3(x, y, 0f));
        // z 保留它自己的：NGUI 的层序看 depth/panel，不看 z，别在这里引入漂移。
        chip.position = new Vector3(want.x, want.y, chip.position.z);
    }

    private void PinRdChip()
    {
        PinChipAt(RdChipNode, RdChipX, RdChipY);
    }

    /// <summary>
    /// 把「支持作者」钉在**最右下角**（<see cref="SponsorPos"/> 按当前窗口宽高比现算），
    /// 口径同 <see cref="PinRdChip"/>：菜单动、按钮不动；窗口一变，按钮跟着贴新角。
    /// </summary>
    private void PinSponsor()
    {
        Vector2 sp = SponsorPos();
        PinChipAt(SponsorNode, sp.x, sp.y);
    }

    /// <summary>
    /// 「编辑卡组」「联机模式」两项尾部的 RD 角标。
    /// 造法与超先行卡的 NEW 角标同款（运行时 new 一个 UILabel、借现有标签的字体），
    /// 只是位置贴在行按钮右端、平时 SetActive(false)。
    /// </summary>
    private void CreateRdBadges()
    {
        for (int i = 0; i < RdBadgeItems.Length; i++)
        {
            string itemName = RdBadgeItems[i];
            if (rdBadges.ContainsKey(itemName))
            {
                continue;
            }
            Transform item = gameObject.transform.Find(itemName);
            if (item == null)
            {
                continue;
            }
            Transform button = UIHelper.getByName<Transform>(item.gameObject, itemName + "_");
            if (button == null)
            {
                continue;
            }
            UILabel existing = button.GetComponentInChildren<UILabel>();
            if (existing == null)
            {
                continue;
            }
            rdItemOriginalText[itemName] = existing.text;

            GameObject badge = new GameObject("rdBadge_" + itemName);
            badge.layer = button.gameObject.layer;
            badge.transform.SetParent(button, false);
            badge.transform.localPosition = new Vector3(74f, 15f, 0f);
            badge.transform.localScale = Vector3.one;

            UILabel label = badge.AddComponent<UILabel>();
            label.text = "RD";
            label.color = ModeColorRD;
            label.fontSize = 16;
            if (existing.bitmapFont != null)
            {
                label.bitmapFont = existing.bitmapFont;
            }
            else
            {
                label.trueTypeFont = existing.trueTypeFont;
            }
            label.overflowMethod = UILabel.Overflow.ResizeFreely;
            label.effectStyle = UILabel.Effect.Outline;
            label.effectColor = new Color(0.25f, 0.12f, 0f, 1f);
            label.effectDistance = new Vector2(1, 1);
            label.depth = existing.depth + 1;
            badge.SetActive(false);
            rdBadges[itemName] = badge;
        }
    }

    /// <summary>
    /// 按当前模式把**整块主菜单**刷成对应形态。模式切换后必须调一次（谁切谁负责调）。
    ///
    /// 刷三件事，缺一不可：
    ///   1. **可见项与排布** —— RD 只留 RdVisibleItems 那几项，其余隐藏并紧凑重排、
    ///      面板重新居中（→ <see cref="ArrangeMenuItems"/>）；
    ///   2. **入口按钮** —— 文案报「点一下会切到哪一边」、配色随模式、位置重钉；
    ///   3. **RD 角标** —— 卡组编辑 / 联机模式两项尾部。
    ///
    /// ⚠ 第 1 条是关键：早期只做了 2/3，切到 RD 后「人机/单人/录像…」照旧躺在菜单里，
    ///   点了进不去（RD 没有对应实现）—— 留着就是纯误导。
    /// </summary>
    public void RefreshRdUi()
    {
        bool rd = GameModeManager.IsRD;
        Color tone = rd ? ModeColorRD : ModeColorOCG;

        // 可见项 + 排布 + 面板高度：一次算完（幂等，重复调用无副作用）。
        // ⚠ 调的是 ApplyItemLayout 而不是 ArrangeMenuItems：后者还会把根节点归零，
        //   那是「首次进场」的一次性动作，每次切模式都做没有必要。
        ApplyItemLayout();
        PositionRdChip();

        Transform chip = gameObject.transform.Find(RdChipNode);
        if (chip != null)
        {
            Transform button = UIHelper.getByName<Transform>(chip.gameObject, RdChipButton);
            if (button != null)
            {
                UILabel label = button.GetComponentInChildren<UILabel>();
                if (label != null)
                {
                    // 文案报**当前**是哪一边（用户 2026-09-19 定稿）。
                    // 原先是反的 —— OCG 下写「RD 模式 ▸」（报「点一下切到哪边」），
                    // 而屏幕上其它所有模式提示（角标、配色、保留项）说的都是「现在是哪边」，
                    // 两种口径混在同一个位置，玩家最容易看错的就是这一颗。
                    // 现在统一成「报当前模式」，箭头也一并去掉 —— 留着箭头就等于还在暗示「点我过去」。
                    label.text = rd ? "RD 模式" : "OCG 模式";
                    label.color = tone;
                    // ⛔ 不许换行：克隆来的标签宽度是按「系统设置」那四个字定的，
                    //    「OCG 模式」比它宽，默认会折成两行（「模式」单独一行）。
                    //    ResizeFreely 会把 NGUI 的 regionWidth 放到 1000000（见 UILabel.ProcessText），
                    //    于是**永远不折行**，标签宽高跟着文字自己走，居中不变。
                    //    这条也是验收判据（[rd] chipdiag 里报 overflowMethod）。
                    label.overflowMethod = UILabel.Overflow.ResizeFreely;
                }
                // 按钮底色跟着走（UIButton 的颜色是叠在切片贴图上的乘算，压暗即可，不会换皮）。
                UISprite btnSprite = button.GetComponent<UISprite>();
                if (btnSprite != null)
                {
                    btnSprite.color = new Color(tone.r * 0.55f, tone.g * 0.55f, tone.b * 0.55f, 1f);
                }
            }
            chip.localScale = new Vector3(RdChipScale, RdChipScale, 1f);
        }

        foreach (KeyValuePair<string, GameObject> pair in rdBadges)
        {
            if (pair.Value != null)
            {
                pair.Value.SetActive(rd);
            }
        }

        if (QuickTestTrace.Enabled)
        {
            // 入口按钮的屏幕落点：验收脚本按这个坐标点它，比按猜的算可靠。
            Vector3 sp = Vector3.zero;
            UILabel chipLabel = null;
            Transform chipT = gameObject.transform.Find(RdChipNode);
            if (chipT != null)
            {
                Transform chipBtn = UIHelper.getByName<Transform>(chipT.gameObject, RdChipButton);
                if (chipBtn != null)
                {
                    chipLabel = chipBtn.GetComponentInChildren<UILabel>();
                    if (Program.camera_main_2d != null)
                    {
                        sp = Program.camera_main_2d.WorldToScreenPoint(chipBtn.position);
                    }
                }
            }
            QuickTestTrace.Log("rd", "ui mode=" + GameModeManager.ModeLabel
                + " chipText=" + (chipLabel != null ? chipLabel.text : "null")
                // ⚠ 这里是**动画途中的生坐标**（initialize / show 当刻菜单还在滑入），
                //   只当参考。要按下去请用落定之后的 [rd] chip screen（见 show() 的
                //   Program.go 采样），否则会点空。
                + " chipRaw=(" + Mathf.RoundToInt(sp.x) + "," + Mathf.RoundToInt(Screen.height - sp.y) + ")"
                // 角标报**亮着的个数**（不是字典大小）：验收判据是「OCG 下没有 RD 角标 /
                // RD 下有」，而 rdBadges.Count 恒等于 5，照着判什么都判不出来。
                // ⚠ 数的是 activeSelf（角标**自己**的亮灭），并另报 live= 是 activeInHierarchy
                //   （连父级也算）。原先只报后者 ⇒ **主菜单根被盖住时（比如设置窗口开着、
                //   菜单正在滑入）会报 0/5**，明明 RD 下五枚都亮着（2026-09-21 实测：
                //   RD 下同一段日志里既有 5/5 也有 0/5，全看采样那一刻菜单露没露）。
                //   判据要的是逻辑态，所以 badges= 取 activeSelf；live= 留给排查用。
                + " badges=" + BadgesOn() + "/" + rdBadges.Count
                + " live=" + BadgesLive() + "/" + rdBadges.Count
                + " ocgCards=" + YGOSharp.CardsManager.CountOf(false)
                + " rdCards=" + YGOSharp.CardsManager.CountOf(true)
                // 可见 / 隐藏清单：一条行就判得出「RD 只留 RD 入口」，不必去逐项读 [btn] active。
                + " visible=[" + string.Join("|", VisibleMenuItems()) + "]"
                + " hidden=[" + HiddenMenuItems() + "]");
        }
    }

    /// <summary>当前模式下被隐藏的菜单项（按 MenuItemOrder 顺序拼成一行，供探针与排查用）。</summary>
    private static string HiddenMenuItems()
    {
        string[] visible = VisibleMenuItems();
        List<string> hidden = new List<string>();
        for (int i = 0; i < MenuItemOrder.Length; i++)
        {
            if (VisibleRank(MenuItemOrder[i], visible) < 0)
            {
                hidden.Add(MenuItemOrder[i]);
            }
        }
        return string.Join("|", hidden.ToArray());
    }

    /// <summary>
    /// 当前**亮着**的 RD 角标个数（角标自己的 activeSelf）。
    ///
    /// ⚠ 这里刻意**不**用 activeInHierarchy：主菜单根被别的窗口盖住、或菜单正在滑入时，
    ///   activeInHierarchy 全为 false，会把「RD 下五枚都亮着」报成 0/5（2026-09-21 实测）。
    ///   角标与菜单根是两件事，判据要的是前者；后者另有 [vis]/[chipslide] 那批探针盯着。
    ///   「屏幕上真的能看见几枚」用 <see cref="BadgesLive"/>。
    /// </summary>
    private int BadgesOn()
    {
        int n = 0;
        foreach (KeyValuePair<string, GameObject> pair in rdBadges)
        {
            if (pair.Value != null && pair.Value.activeSelf)
            {
                n++;
            }
        }
        return n;
    }

    /// <summary>当前**在层级里活着**（连父级一起算）的 RD 角标个数 —— 只作排查用的旁证。</summary>
    private int BadgesLive()
    {
        int n = 0;
        foreach (KeyValuePair<string, GameObject> pair in rdBadges)
        {
            if (pair.Value != null && pair.Value.activeInHierarchy)
            {
                n++;
            }
        }
        return n;
    }

    /// <summary>
    /// 点 RD 入口：立刻切换模式（数据启动时就全装好了，零加载）。
    ///
    /// **不弹提示**（用户 2026-09-19 定稿）：模式由按钮文案（点一下会切到哪一边）、
    /// 主色调、以及保留项本身体现 —— 切一下弹一行小字是噪音。
    /// </summary>
    public void onClickRdChip()
    {
        GameModeManager.Toggle();
        RefreshRdUi();
        // 切完立刻重报一遍 [btn]（含 active 字段）与 [icon]：
        // 那批探针原来只在 show() 里报，而菜单内的切换**不会**重新 show()，
        // 不补这一下，验收脚本读到的还是切换前那一批（「RD 下哪些项被藏了」就判不出来）。
        // 此刻菜单早已停稳（不是 show() 那种滑入途中），报出来的坐标可以直接用。
        TraceAllIcons();
        TraceRdChip();
    }

    /// <summary>
    /// 单独报一次入口按钮的屏幕落点。
    ///
    /// ⚠ 为什么不能复用 RefreshRdUi 里那条 [rd] ui：那个在 initialize / show() 里被调，
    ///   而这两处窗口都还没滑到位（iTween 0.6s）—— 报出来的坐标会让脚本点空。
    ///   必须跟 [icon] / [btnpos] 一个口径：**等落定之后**再报（见 show() 里的 Program.go）。
    /// </summary>
    private void TraceRdChip()
    {
        if (!QuickTestTrace.Enabled)
        {
            return;
        }
        Transform chip = gameObject.transform.Find(RdChipNode);
        if (chip == null)
        {
            QuickTestTrace.Log("rd", "chip = null");
            return;
        }
        Transform button = UIHelper.getByName<Transform>(chip.gameObject, RdChipButton);
        UILabel label = button != null ? button.GetComponentInChildren<UILabel>() : null;
        Vector3 sp = Vector3.zero;
        if (button != null && Program.camera_main_2d != null)
        {
            sp = Program.camera_main_2d.WorldToScreenPoint(button.position);
        }
        // ⚠ `text=` **必须是这一行的最后一个字段**：文案带空格（「OCG 模式 ▸」），
        //   验收侧按「等号后一直到行尾」取值。夹在中间会被空格切断，读到「OCG」这种半截值
        //   （同一类坑在禁限表探针的 picked 上已经踩过一次，2026-09-19）。
        QuickTestTrace.Log("rd", "chip screen=(" + Mathf.RoundToInt(sp.x) + "," + Mathf.RoundToInt(Screen.height - sp.y) + ")"
            + " active=" + chip.gameObject.activeInHierarchy
            + " mode=" + GameModeManager.ModeLabel
            + " text=" + (label != null ? label.text : "null"));
        TraceChipDiag();
    }

    /// <summary>节点的完整父子路径（排查「同名节点有好几个」用）。</summary>
    private static string FullPathOf(Transform t)
    {
        string p = t.name;
        Transform up = t.parent;
        for (int i = 0; i < 8 && up != null; i++)
        {
            p = up.name + "/" + p;
            up = up.parent;
        }
        return p;
    }

    /// <summary>
    /// RD 入口 chip 的几何诊断（2026-09-19 加入）。
    ///
    /// 起因：`[rd] chip` 报 screen=(1141,199)，但
    ///   ① 截图里那一像素**什么都没画**（面板右边界在 ~1108）；
    ///   ② 逐点扫 hover 发现 chip 的碰撞盒其实在 screen x≈1070~1090（**能命中**）。
    /// 也就是「探针报的位置」和「真实可点的位置」差了 ~60px。
    /// 而 <see cref="UIHelper.getByName{T}(GameObject, string)"/> 有个坑：
    /// 它遍历 `GetComponentsInChildren` 后**返回最后一个**同名匹配（循环里没有提前 return），
    /// 而 `Transform.Find` 取的是**第一个**同名子节点 —— 两条查找路径可能指到不同对象。
    /// 这份 dump 把每个叫 rdChip / rdChip_ 的节点的路径、激活态、局部/世界/屏幕坐标、
    /// widget 可见性与碰撞盒尺寸一次性列出来，用来判断到底是「有几个同名节点」还是
    /// 「碰撞盒与变换不一致」。
    /// </summary>
    private void TraceChipDiag()
    {
        if (!QuickTestTrace.Enabled)
        {
            return;
        }
        UIPanel panel = gameObject.GetComponent<UIPanel>();
        if (panel != null)
        {
            Vector4 b = panel.baseClipRegion;
            Vector4 f = panel.finalClipRegion;
            QuickTestTrace.Log("rd", "chipdiag panel clipping=" + panel.clipping
                + " base=(" + b.x.ToString("F1") + "," + b.y.ToString("F1") + ","
                + b.z.ToString("F1") + "," + b.w.ToString("F1") + ")"
                + " final=(" + f.x.ToString("F1") + "," + f.y.ToString("F1") + ","
                + f.z.ToString("F1") + "," + f.w.ToString("F1") + ")");
        }

        Transform[] all = gameObject.transform.GetComponentsInChildren<Transform>(true);
        int n = 0;
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i].name != RdChipNode && all[i].name != RdChipButton)
            {
                continue;
            }
            n++;
            Transform t = all[i];
            Vector3 wp = t.position;
            Vector3 sp = Program.camera_main_2d != null
                ? Program.camera_main_2d.WorldToScreenPoint(wp)
                : Vector3.zero;
            UIWidget w = t.GetComponent<UIWidget>();
            BoxCollider bc = t.GetComponent<BoxCollider>();
            QuickTestTrace.Log("rd", "chipdiag #" + n + " " + FullPathOf(t)
                + " self=" + t.gameObject.activeSelf
                + " inHier=" + t.gameObject.activeInHierarchy
                + " local=(" + t.localPosition.x.ToString("F1") + ","
                + t.localPosition.y.ToString("F1") + ","
                + t.localPosition.z.ToString("F1") + ")"
                + " scale=(" + t.localScale.x.ToString("F2") + ","
                + t.localScale.y.ToString("F2") + ")"
                + " world=(" + wp.x.ToString("F1") + "," + wp.y.ToString("F1") + ","
                + wp.z.ToString("F1") + ")"
                + " screen=(" + Mathf.RoundToInt(sp.x) + ","
                + Mathf.RoundToInt(Screen.height - sp.y) + ")"
                + " widget=" + (w != null
                    ? ("vis=" + w.isVisible + " alpha=" + w.alpha.ToString("F2")
                       + " depth=" + w.depth + " wh=" + w.width + "x" + w.height)
                    : "null")
                + " collider=" + (bc != null
                    ? ("en=" + bc.enabled + " center=(" + bc.center.x.ToString("F1") + ","
                       + bc.center.y.ToString("F1") + ") size=(" + bc.size.x.ToString("F1")
                       + "," + bc.size.y.ToString("F1") + ")")
                    : "null"));
            UILabel lb = t.GetComponentInChildren<UILabel>();
            if (lb != null)
            {
                // printed= 是 NGUI 按当前字体排出来的实际文字尺寸：折了行就会是两倍行高，
                // 所以「有没有换行」是一个**可离线判的数**（判据见 overflow= / printed=），
                // 不必靠肉眼看截图。overflow 必须是 ResizeFreely，否则克隆来的宽度还会折行。
                QuickTestTrace.Log("rd", "chipdiag #" + n + " label text=[" + lb.text + "]"
                    + " color=" + lb.color.ToString("F2")
                    + " enabled=" + lb.enabled
                    + " inHier=" + lb.gameObject.activeInHierarchy
                    + " overflow=" + lb.overflowMethod
                    + " fontSize=" + lb.fontSize
                    + " wh=" + lb.width + "x" + lb.height
                    + " printed=(" + Mathf.RoundToInt(lb.printedSize.x) + ","
                    + Mathf.RoundToInt(lb.printedSize.y) + ")");
            }
        }
        QuickTestTrace.Log("rd", "chipdiag total=" + n);

        // 模式徽标的深色底板（用户 2026-09-19：「也配上主菜单一样的黑色底色」）。
        // 「有底板」不能只看截图 —— 底板是**纯黑半透明**的，压在深色天空上看不出来也说不定。
        // 这里把它的存在、精灵名、尺寸、depth 与**和标签的相对层序**一起报出来：
        // 判据能落到「有这一片、精灵与 back 同源、尺寸 = 按钮 widget、depth 比标签低 1」。
        Transform chipBtn = UIHelper.getByName<Transform>(gameObject, RdChipButton);
        Transform plate = chipBtn != null ? chipBtn.Find(RdChipPlateNode) : null;
        if (plate == null)
        {
            QuickTestTrace.Log("rd", "plate = null（徽标没有底板）");
        }
        else
        {
            UISprite sp = plate.GetComponent<UISprite>();
            // ⚠ 标签取「同一按钮下的」而不是 plate 的父子标签：plate 与 label 是**兄弟**，
            //   都挂在按钮下（见 CreateRdChipPlate），GetComponentInParent 会一路找到别处去。
            UILabel chipLabel = chipBtn.GetComponentInChildren<UILabel>();
            QuickTestTrace.Log("rd", "plate node=" + FullPathOf(plate)
                + " sprite=" + (sp != null ? sp.spriteName : "null")
                + " atlas=" + (sp != null && sp.atlas != null ? sp.atlas.name : "null")
                + " wh=" + (sp != null ? (sp.width + "x" + sp.height) : "?")
                + " depth=" + (sp != null ? sp.depth.ToString() : "?")
                + " color=" + (sp != null ? sp.color.ToString("F2") : "?")
                + " visible=" + (sp != null ? sp.isVisible.ToString() : "?")
                + " labelDepth=" + (chipLabel != null ? chipLabel.depth.ToString() : "?")
                + " (label 必须 > depth 才压得住字)");

            // 底板与文字的**重合度**（用户 2026-09-19：「背景阴影与字重合度低，前面有一长部分
            // 阴影无字，后面又有字和阴影边缘很近」）。判据要的是数字，不是肉眼：
            // 把文字框与底板框都投影到**按钮局部坐标**，报四边各自的余量 —— 四边相等才算重合。
            if (sp != null && chipLabel != null && chipBtn != null)
            {
                Vector4 tBox = BoxOf(chipLabel.worldCorners, chipBtn);
                Vector4 pBox = BoxOf(sp.worldCorners, chipBtn);
                QuickTestTrace.Log("rd", "platefit textBox=(" + Fmt(tBox) + ")"
                    + " plateBox=(" + Fmt(pBox) + ")"
                    + " margin=(左" + Mathf.RoundToInt(tBox.x - pBox.x)
                    + ",右" + Mathf.RoundToInt(pBox.z - tBox.z)
                    + ",下" + Mathf.RoundToInt(tBox.y - pBox.y)
                    + ",上" + Mathf.RoundToInt(pBox.w - tBox.w) + ")"
                    + " offset=(字心-板心" + Mathf.RoundToInt((tBox.x + tBox.z) * 0.5f - (pBox.x + pBox.z) * 0.5f)
                    + "," + Mathf.RoundToInt((tBox.y + tBox.w) * 0.5f - (pBox.y + pBox.w) * 0.5f) + ")");
            }
        }
    }

    /// <summary>把一组世界坐标角点投影到 <paramref name="space"/> 的局部坐标，返回包围盒 (左,下,右,上)。</summary>
    private static Vector4 BoxOf(Vector3[] corners, Transform space)
    {
        float l = float.MaxValue;
        float b = float.MaxValue;
        float r = float.MinValue;
        float t = float.MinValue;
        for (int i = 0; i < corners.Length; i++)
        {
            Vector3 v = space.InverseTransformPoint(corners[i]);
            if (v.x < l) l = v.x;
            if (v.y < b) b = v.y;
            if (v.x > r) r = v.x;
            if (v.y > t) t = v.y;
        }
        return new Vector4(l, b, r, t);
    }

    /// <summary>包围盒的报数格式（整数，便于判据逐字比对）。</summary>
    private static string Fmt(Vector4 box)
    {
        return Mathf.RoundToInt(box.x) + "," + Mathf.RoundToInt(box.y) + ","
            + Mathf.RoundToInt(box.z) + "," + Mathf.RoundToInt(box.w);
    }

    public override void show()
    {
        base.show();
        Program.charge();

        // 回主菜单时按当前模式重刷入口/角标：模式可以在别处（对局收尾、设置窗口）被改掉，
        // 这里刷一次就不用给每个切换点都挂一遍回调。
        RefreshRdUi();

        // 清掉滑入采样的「上一次读数」，让下一帧必定落一行基准 —— 否则从子界面回来时
        // 徽标恰好和上次读数相同（它被钉住了，本来就恒定），会被「没动」判据吞掉。
        chipSlideLastChip = new Vector2(-9999f, -9999f);
        chipSlideLastRef = new Vector2(-9999f, -9999f);
        // 开帧窗：接下来 60 帧逐帧无条件落行，拿到整段滑入轨迹（0.6s 由这里的 +50ms 触发）。
        chipSlideFramesLeft = ChipSlideWindowFrames;
        chipSlideFrameNo = 0;

        // 布局/分辨率都定下来之后再报一次图标坐标，验收脚本按这个位置取样。
        // 菜单是 tween 移入的，只报一批会落在动画途中（[icon] 两批能差出 60+ px，
        // 照那坐标按下去必然点空）。所以连报几批，脚本取**最后一批** —— 与 [btnpos] 的
        // 「多拍采样」同一口径。
        Program.go(300, TraceAllIcons);
        Program.go(420, TraceAllIcons);
        Program.go(540, TraceAllIcons);
        Program.go(660, TraceAllIcons);

        // RD 入口按钮也按同一口径**多拍采样**：它的坐标在 RefreshRdUi 里报的那一份是
        // 菜单滑动途中的（[rd] ui 的 chipRaw），照它点必然点空 —— 验收脚本必须取这里
        // 落定之后报的 [rd] chip screen，且取**最后一批**。
        Program.go(300, TraceRdChip);
        Program.go(420, TraceRdChip);
        Program.go(540, TraceRdChip);
        Program.go(660, TraceRdChip);

        
        // 自动检查超先行卡更新
        if (!_isCheckingUpdate && !isPreDownloading)
        {
            Program.I().StartCoroutine(CheckSuperPreUpdateCoroutine());
        }
    }

    public override void hide()
    {
        base.hide();
    }

    
    /// <summary>
    /// 检查超先行卡是否有更新的协程
    /// </summary>
    private IEnumerator CheckSuperPreUpdateCoroutine()
    {
        _isCheckingUpdate = true;

        // 避免网络异常导致 NEW 标记卡在旧状态
        HideNewBadge();

        try
        {
            bool hasUpdate = false;
            yield return Program.I().StartCoroutine(UnityFileDownloader.CheckForUpdateAsync(
                SuperPreDownloadUrl,
                SuperPreLocalPath,
                (result) => { hasUpdate = result; }
            ));

            _hasSuperPreUpdate = hasUpdate;
            if (_hasSuperPreUpdate)
            {
                ShowNewBadge();
            }
        }
        finally
        {
            _isCheckingUpdate = false;
        }
    }
    
    /// <summary>
    /// 显示 "NEW" 标记
    /// </summary>
    private void ShowNewBadge()
    {
        if (_newBadge != null)
        {
            _newBadge.SetActive(true);
            return;
        }
        
        // NEW 只提示超先行卡栏目，不再附着到资源下载。
        Transform buttonTransform = UIHelper.getByName<Transform>(gameObject, "superPre_");
        if (buttonTransform == null) return;
        
        // 获取按钮上的现有 Label 来复用字体
        UILabel existingLabel = buttonTransform.GetComponentInChildren<UILabel>();
        if (existingLabel == null) return;
        
        // 创建 NEW 标记
        _newBadge = new GameObject("NewBadge");
        _newBadge.layer = buttonTransform.gameObject.layer;
        _newBadge.transform.SetParent(buttonTransform, false);
        _newBadge.transform.localPosition = new Vector3(80, 18, 0); // 右上角位置
        _newBadge.transform.localScale = Vector3.one;
        
        UILabel label = _newBadge.AddComponent<UILabel>();
        label.text = "NEW";
        label.color = new Color(1f, 0.2f, 0.2f, 1f); // 醒目的红色
        label.fontSize = 18;
        if (existingLabel.bitmapFont != null)
        {
            label.bitmapFont = existingLabel.bitmapFont;
        }
        else
        {
            label.trueTypeFont = existingLabel.trueTypeFont;
        }
        label.overflowMethod = UILabel.Overflow.ResizeFreely;
        label.effectStyle = UILabel.Effect.Outline;
        label.effectColor = new Color(0.5f, 0f, 0f, 1f); // 深红色描边
        label.effectDistance = new Vector2(1, 1);
        label.depth = existingLabel.depth + 1;
    }
    
    /// <summary>
    /// 隐藏 "NEW" 标记
    /// </summary>
    private void HideNewBadge()
    {
        if (_newBadge != null)
        {
            _newBadge.SetActive(false);
        }
        _hasSuperPreUpdate = false;
    }

    private void onClickUpdateResources()
    {
        if (isClouseupDownloading)
        {
            // 如果下载正在进行，弹出一个非阻塞的选择框，而不是简单打印文字
            RMSshow_yesOrNo(
                "HANDLE_ONGOING_DOWNLOAD",          // 新的哈希码，用于区分事件
                $"立绘下载正在后台进行中...\n进度 {_closeupSuccessCount}/{_pendingCloseupDownloads.Count}\n点击”是“继续下载\n点击”否“取消下载",
                new messageSystemValue { value = "continue", hint = "继续下载" }, // 选项1：让它继续
                new messageSystemValue { value = "cancel", hint = "取消下载" }   // 选项2：取消它
            );
            return; // 阻止后续逻辑执行
        }
        if (isPreDownloading)
        {
            RMSshow_onlyYes(
                "HANDLE_ONGOING_DOWNLOAD",
                "超先行卡下载正在后台进行中...",
                new messageSystemValue { value = "continue", hint = "继续下载" }
            );
            return;
        }
        // 卡牌数据更新（启动时也会自动跑一次）。这里只是给它一个手动入口，
        // 否则数据更新是「看不见也点不到」的。
        if (ClientDataUpdater.IsBusy)
        {
            RMSshow_onlyYes(
                "HANDLE_ONGOING_DOWNLOAD",
                "卡牌数据正在后台更新中...",
                new messageSystemValue { value = "continue", hint = "知道了" }
            );
            return;
        }
        var options = new List<messageSystemValue>
        {
            new messageSystemValue { value = "closeup", hint = "下载/更新立绘" },
            // 按 ETag 判增量，顺带会用真解析器校验本地文件 —— 本地数据被改坏会在此自动重下。
            new messageSystemValue { value = "clientData", hint = "检查并更新卡牌数据" },
            // 忽略本地记录、无条件重下：用于「文件本身合法但不是想要的那份」这类检查发现不了的情况。
            new messageSystemValue { value = "clientDataForce", hint = "强制重下卡牌数据（修复）" },
            // 客户端本体版本检查（2026-09-25 新增，v1.3）：只查版本 + 指路夸克网盘，不自动下载。
            new messageSystemValue { value = "clientSelf", hint = "检查客户端更新" },
            new messageSystemValue { value = "cancel", hint = "取消" }
        };
        RMSshow_singleChoice("UPDATE_RESOURCES", options);
    }

    /// <summary>
    /// 点主菜单「资源更新」（用户 2026-09-20 定稿）：**先弹选择窗口**让玩家挑 ——
    ///   ① 打开更新文件夹 —— 资源管理器落到 rd/update/，把包丢进去后游戏自动弹确认（轮询那条链）；
    ///   ② 选择安装包…   —— 原生文件选择器（NativeFilePicker），从任意目录挑包当场安装。
    /// 关掉弹窗 = 什么都不做。
    /// </summary>
    private void onClickRdUpdate()
    {
        QuickTestTrace.Log("rdupdate", "clicked");
        List<messageSystemValue> options = new List<messageSystemValue>();
        options.Add(new messageSystemValue { value = "folder", hint = "打开更新文件夹" });
        options.Add(new messageSystemValue { value = "pick", hint = "选择安装包…" });
        RMSshow_singleChoice("RD_UPDATE_CHOICE", options);
    }

    /// <summary>把资源管理器开到 rd/update/（不存在就先建）。选择窗口选项①。</summary>
    private void OpenRdUpdateFolder()
    {
        try
        {
            string dir = Path.GetFullPath(RdDataUpdater.UpdateDir);
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }
            System.Diagnostics.Process.Start("explorer.exe", "\"" + dir + "\"");
        }
        catch (Exception e)
        {
            Program.PrintToChat("资源更新：打开文件夹失败：" + e.Message);
            UnityEngine.Debug.Log(e);
        }
    }

    /// <summary>文件选择器在途标志（BeginPick 忽略在途的重复发起，这里挡的是结果回调重复）。</summary>
    private bool rdUpdatePickRunning;

    /// <summary>选择窗口选项②：起原生文件选择器（后台线程，不卡主循环）。</summary>
    private void BeginPickRdUpdatePacks()
    {
        if (rdUpdatePickRunning)
        {
            return;
        }
        rdUpdatePickRunning = true;
        string dir = null;
        try
        {
            dir = Path.GetFullPath(RdDataUpdater.UpdateDir);
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }
        }
        catch (Exception)
        {
            dir = null; // 目录建不出来也无所谓，对话框自己会记上次目录
        }
        NativeFilePicker.BeginPick("选择安装的 RD 更新包", dir, "更新包 (*.ypk)\0*.ypk\0全部文件 (*.*)\0*.*\0");
        QuickTestTrace.Log("rdupdate", "pick-dialog open");
    }

    /// <summary>每帧收文件选择器的结果（preFrameFunction 调）：取消提示一行；有包当场安装+热刷新。</summary>
    private void FinishPickRdUpdatePacks()
    {
        if (!rdUpdatePickRunning)
        {
            return;
        }
        string[] picks;
        if (!NativeFilePicker.TryTakeResult(out picks))
        {
            return;
        }
        rdUpdatePickRunning = false;
        if (picks == null || picks.Length == 0)
        {
            QuickTestTrace.Log("rdupdate", "pick-cancelled");
            Program.PrintToChat("资源更新：未选择安装包。");
            return;
        }
        QuickTestTrace.Log("rdupdate", "picked count=" + picks.Length);
        int applied = RdDataUpdater.ApplyPacks(new List<string>(picks));
        HandleRdUpdateApplied(applied);
    }

    /// <summary>
    /// 把 rd/update/ 里现存的包**当场应用**并热刷新（轮询确认弹窗走这条）：
    /// 有包被应用就清卡图缓存 + 整体重放数据库（RD 池/禁限表/卡图当场换新），结果落聊天栏。
    /// </summary>
    private void ApplyRdUpdatePacksNow()
    {
        HandleRdUpdateApplied(RdDataUpdater.ApplyPendingPacks());
    }

    /// <summary>应用结果收尾（应用了几个包 → 热刷新 + 聊天栏播报）。各条链路共用。</summary>
    private void HandleRdUpdateApplied(int applied)
    {
        if (applied > 0)
        {
            GameTextureManager.clearAll();
            if (Program.I().ReloadGameDatabases())
            {
                Program.PrintToChat("资源更新：已应用 " + applied + " 个更新包，卡表与卡图已热更新。");
            }
            else
            {
                Program.PrintToChat("资源更新：包已应用，但数据重载失败 —— 请重启游戏后使用新数据。");
            }
        }
        else
        {
            Program.PrintToChat("资源更新：暂无待应用的更新包（把 ypk 放进打开的 rd/update 文件夹即可）。");
        }
    }

    /// <summary>轮询节流（毫秒）：主菜单对 rd/update/ 的扫描间隔。</summary>
    private const int RdUpdatePollMs = 1500;

    /// <summary>上次轮询时刻（Environment.TickCount，Time.time 会被 timeScale=0 冻住）。</summary>
    private int rdUpdatePollAtMs = -100000;

    /// <summary>上一轮看到的包签名（确认「复制完成」：两轮一致才弹窗，避免对半截文件弹）。</summary>
    private string rdUpdateLastSeen = "";

    /// <summary>弹过（或被拒绝）时的签名 —— 包集合没变就不重复打扰。</summary>
    private string rdUpdateDismissed = "";

    /// <summary>
    /// 主菜单轮询 rd/update/（用户 2026-09-20 定稿的交互）：玩家从打开的文件夹把包拖进来后，
    /// 游戏里**弹确认框**当场应用，不用再点「资源更新」。细节：
    ///   · 签名 = 包名+大小；**连续两轮一致**才弹 —— 拖拽复制中大小一直在涨，弹了也应用不了；
    ///   · 弹过即记签名，拒绝/关掉后不再重复弹，直到包集合再变化；
    ///   · 只在主菜单活着时轮询（preFrameFunction），对局/子界面里静默，回主菜单自然补弹。
    /// </summary>
    private void PollRdUpdatePacks()
    {
        int now = Environment.TickCount;
        if (now - rdUpdatePollAtMs < RdUpdatePollMs)
        {
            return;
        }
        rdUpdatePollAtMs = now;
        List<string> packs = RdDataUpdater.PendingPacks();
        if (packs.Count == 0)
        {
            rdUpdateLastSeen = "";
            rdUpdateDismissed = "";
            return;
        }
        long total = 0;
        StringBuilder sig = new StringBuilder();
        foreach (string name in packs)
        {
            long len;
            try
            {
                len = new FileInfo(RdDataUpdater.UpdateDir + "/" + name).Length;
            }
            catch (Exception)
            {
                return; // 文件正好被占用/消失，下一轮再看
            }
            total += len;
            sig.Append(name).Append(':').Append(len).Append(';');
        }
        string current = sig.ToString();
        if (current != rdUpdateLastSeen)
        {
            rdUpdateLastSeen = current; // 先记下，下一轮（1.5s 后）一致才弹
            return;
        }
        if (current == rdUpdateDismissed)
        {
            return;
        }
        rdUpdateDismissed = current;
        QuickTestTrace.Log("rupd", "confirm-shown packs=" + packs.Count + " bytes=" + total);
        RMSshow_yesOrNo(
            "RD_UPDATE_CONFIRM",
            "发现 " + packs.Count + " 个RD更新包（" + (total / 1024 / 1024) + " MB），现在应用吗？\r\n"
            + string.Join("、", packs.ToArray()),
            new messageSystemValue { value = "yes", hint = "应用" },
            new messageSystemValue { value = "no", hint = "稍后" });
    }

    public override void ES_RMS(string hashCode, List<messageSystemValue> result)
    {
        base.ES_RMS(hashCode, result);

        switch (hashCode)
        {
            case "RD_UPDATE_CHOICE": // 「资源更新」按钮弹出的选择窗口
                if (result[0].value == "folder")
                {
                    OpenRdUpdateFolder();
                }
                else if (result[0].value == "pick")
                {
                    BeginPickRdUpdatePacks();
                }
                break;
            case "RD_UPDATE_CONFIRM": // rd/update/ 轮询发现新包，玩家确认当场应用
                if (result[0].value == "yes")
                {
                    ApplyRdUpdatePacksNow();
                }
                else
                {
                    Program.PrintToChat("已暂缓 —— 点主菜单「资源更新」随时可以应用。");
                }
                break;
            case "UPDATE_RESOURCES":
                string choice = result[0].value;
                if (choice == "closeup")
                {
                    Program.I().StartCoroutine(DownloadCloseupsCoroutine());
                }
                else if (choice == "clientData")
                {
                    Program.I().StartCoroutine(ClientDataUpdater.UpdateCoroutine(false, true));
                }
                else if (choice == "clientDataForce")
                {
                    Program.I().StartCoroutine(ClientDataUpdater.UpdateCoroutine(true, true));
                }
                else if (choice == "clientSelf")
                {
                    // 客户端本体版本检查（v1.3）。检查完由 ClientSelfUpdate 的 Sink 处理；
                    // 「有新版」时 Sink 已发提示，这里再补一个「打开下载页」入口。
                    ClientSelfUpdate.CheckAsync(false);
                    RMSshow_yesOrNo(
                        "CLIENT_UPDATE_PAGE",
                        "正在检查客户端更新…\n若稍后提示有新版，可点「是」直接前往下载页\n（夸克网盘：" + ClientSelfUpdate.UpdatePageUrl() + "）",
                        new messageSystemValue { value = "yes", hint = "打开下载页" },
                        new messageSystemValue { value = "no", hint = "取消" });
                }
                break;
            case "CLIENT_UPDATE_PAGE":
                if (result[0].value == "yes")
                {
                    ClientSelfUpdate.OpenDownloadPage();
                }
                break;
            case "CONFIRM_FORCE_DOWNLOAD_SUPER_PRE": // 确认强制重新下载
                if (result[0].value == "yes")
                {
                    Program.I().StartCoroutine(ForceDownloadSuperPrePackCoroutine());
                }
                else
                {
                    Program.PrintToChat("操作已取消。");
                }
                break;
            case "CONFIRM_CLOSEUP_DOWNLOAD": // 用户确认下载N个文件
                if (result[0].value == "yes")
                {
                    StartCloseupDownload();
                }
                else
                {
                    isClouseupDownloading = false;
                    _pendingCloseupDownloads = null;
                    Program.PrintToChat("操作已取消。");
                }
                break;

            case "CANCEL_DOWNLOAD_TASK":
                _isCancellationRequested = true;
                Program.PrintToChat("正在取消下载，请稍候...");
                RMSshow_clear();
                break;
            case "HANDLE_ONGOING_DOWNLOAD":
                if (result[0].value == "cancel")
                {
                    // 如果用户选择取消
                    if (_currentDownloadCoroutine != null)
                    {
                        _isCancellationRequested = true;
                        Program.PrintToChat("正在请求取消下载，请稍候...");
                    }
                }
                break;
        }
    }

    #region 立绘下载 (已应用包装器模式)

    /// <summary>
    /// [准备阶段] 检查立绘资源并向用户确认
    /// </summary>
    private IEnumerator DownloadCloseupsCoroutine()
    {
        if (isClouseupDownloading) yield break;
        isClouseupDownloading = true;
        string downloadsDir = "downloads";
        if (!Directory.Exists(downloadsDir)) Directory.CreateDirectory(downloadsDir);

        List<string> filesToDownload = null;
        string filelistInDownloadPath = Path.Combine(downloadsDir, "filelist.txt");

        Program.PrintToChat("正在检查立绘资源列表...");
        bool listDownloaded = false;
        yield return Program.I().StartCoroutine(UnityFileDownloader.DownloadFileWithHeadCheck(
            "https://cdntx2.moecube.com/ygopro2-closeup/filelist.txt",
            filelistInDownloadPath,
            (success) => { listDownloaded = success; }
        ));

        if (!listDownloaded)
        {
            Program.PrintToChat("获取立绘列表失败，请检查网络或稍后再试。");
            isClouseupDownloading = false;
            _pendingCloseupDownloads = null;
            yield break;
        }

        Program.PrintToChat("解析文件列表并与本地文件比对...");
        try
        {
            if (!Directory.Exists(CloseupDirectory)) Directory.CreateDirectory(CloseupDirectory);
            var serverFiles = File.ReadAllLines(filelistInDownloadPath).Where(line => !string.IsNullOrWhiteSpace(line));
            var localFiles = new HashSet<string>(Directory.GetFiles(CloseupDirectory, "*.png").Select(Path.GetFileName));
            // 有一张图片是 .PNG 大写结尾的 39272762.PNG iOS区分大小写，统一兼容一下小写保存
            filesToDownload = serverFiles.Where(f => !localFiles.Contains(f.ToLowerInvariant())).ToList();
        }
        catch (Exception e)
        {
            Program.PrintToChat($"处理文件列表时发生错误: {e.Message}");
            isClouseupDownloading = false;
            _pendingCloseupDownloads = null;
            yield break;
        }

        if (filesToDownload.Count == 0)
        {
            Program.PrintToChat("立绘资源已是最新，无需下载。");
            isClouseupDownloading = false;
            _pendingCloseupDownloads = null;
            yield break;
        }
        if (filesToDownload.Count == 1)
        {
            Program.PrintToChat($"{filesToDownload[0]}");
        }

        _pendingCloseupDownloads = filesToDownload;

        string confirmationMessage = $"发现 {filesToDownload.Count} 个新立绘需要下载。\n这可能会消耗较多时间和数据流量，是否继续？";
        RMSshow_yesOrNo(
            "CONFIRM_CLOSEUP_DOWNLOAD",
            confirmationMessage,
            new messageSystemValue { value = "yes" },
            new messageSystemValue { value = "no" }
        );
    }

    private void StartCloseupDownload()
    {
        // 双重检查，确保有东西可下且没有任务在进行
        if (_pendingCloseupDownloads == null || _pendingCloseupDownloads.Count == 0 || _currentDownloadCoroutine != null)
        {
            isClouseupDownloading = false;
            return;
        }

        // 1. 设置初始状态
        isClouseupDownloading = true; // 保持这个标记，用于重复点击判断
        _isCancellationRequested = false;
        _closeupSuccessCount = 0;
        _closeupFailureCount = 0;

        // 2. 打印开始信息
        Program.PrintToChat($"开始后台下载 {_pendingCloseupDownloads.Count} 个立绘...");

        // 3. 启动 "工作者" 协程，并将其实例存起来
        _currentDownloadCoroutine = Program.I().StartCoroutine(ExecuteCloseupDownload_Worker());
    }


    /// <summary>
    /// [处理器] 立绘下载的包装器协程
    /// </summary>
    private IEnumerator ExecuteCloseupDownload()
    {
        // 1. 设置初始状态
        _closeupSuccessCount = 0;
        _closeupFailureCount = 0;
        RMSshow_onlyYes("CANCEL_DOWNLOAD_TASK", "下载进行中...", new messageSystemValue { hint = "确认" });
        var worker = ExecuteCloseupDownload_Worker();

        // 2. 监视并执行工作者
        while (true)
        {
            try
            {
                if (!worker.MoveNext()) break;
            }
            catch (Exception e)
            {
                Program.PrintToChat($"下载过程中发生严重错误: {e.Message}");
                break;
            }
            yield return worker.Current;
        }

        // 3. 最终清理
        RMSshow_clear();
        _isCancellationRequested = false;
        isClouseupDownloading = false;
        _pendingCloseupDownloads = null;

        // 4. 最终报告
        if (_closeupFailureCount > 0)
        {
            Program.PrintToChat($"下载完成。成功: {_closeupSuccessCount}，失败: {_closeupFailureCount}。您可以稍后重新运行以下载失败的文件。");
        }
        else if (_closeupSuccessCount > 0)
        {
            Program.PrintToChat($"下载完成！共 {_closeupSuccessCount} 个新立绘已成功下载。");
        }
    }

    /// <summary>
    /// [工作者] 立绘下载的实际工作协程
    /// </summary>
    private IEnumerator ExecuteCloseupDownload_Worker()
    {
        var filesToDownload = _pendingCloseupDownloads;
        var totaToDownloadCount = filesToDownload.Count;
        double nextReportPercentage = 0;
        for (int i = 0; i < totaToDownloadCount; i++)
        {
            if (_isCancellationRequested)
            {
                Program.PrintToChat("下载任务已被用户取消。");
                break; // 跳出循环，进入最终清理阶段
            }

            string filename = filesToDownload[i];
            string downloadUrl = "https://cdntx2.moecube.com/ygopro2-closeup/closeup/" + filename;
            string localPath = Path.Combine(CloseupDirectory, filename.ToLowerInvariant());

            float currentProgress = (float)(i + 1) / totaToDownloadCount * 100f;
            // 如果当前进度超过了我们设定的下一个报告点
            if (currentProgress >= nextReportPercentage)
            {
                Program.PrintToChat($"下载进度: {nextReportPercentage}%, {_closeupSuccessCount} / {totaToDownloadCount}");
                nextReportPercentage += 10;
            }

            bool singleFileDownloaded = false;

            yield return Program.I().StartCoroutine(UnityFileDownloader.DownloadFileAsync(
                downloadUrl,
                localPath,
                (success) => { singleFileDownloaded = success; }
            ));

            if (singleFileDownloaded) _closeupSuccessCount++;
            else _closeupFailureCount++;
        }

        // --- 核心优化点 4: 下载结束后，在这里统一清理状态和报告结果 ---
        string finalMessage;
        if (_closeupFailureCount > 0)
        {
            finalMessage = $"下载完成。成功: {_closeupSuccessCount}，失败: {_closeupFailureCount}。可稍后重试。";
        }
        else
        {
            finalMessage = $"下载完成！共 {_closeupSuccessCount} 个新立绘已成功下载。";
        }
        Program.PrintToChat(finalMessage);

        // 重置所有状态，为下一次下载做准备
        isClouseupDownloading = false;
        _isCancellationRequested = false;
        _pendingCloseupDownloads = null;
        _currentDownloadCoroutine = null; // 清理协程引用
    }

    #endregion

    public override void preFrameFunction()
    {
        base.preFrameFunction();
        // 徽标是菜单面板的子节点 ⇒ 面板一移它就跟着移（用户 2026-09-19：「不和主菜单一起
        // 移动，它是单独的」）。面板的位移实测有三种，三种都不该带着徽标走：
        //   ① show() 时 iTween.MoveTo(落点) —— 落点就是它已经在的地方，本机上是空操作；
        //   ② OCG↔RD 切换时按项数（10/6）**重新居中**，菜单项整体跳 113px（这是常态、
        //      也是用户真正看到的那一次）；
        //   ③ WindowServantSP.createWindow() 把配置里的窗口落点写进根节点 localPosition。
        // 每帧重算世界位置（是滑是跳一样），再把裁剪区跟到它当前所在处 —— 只钉位置不够，
        // 裁剪区跟不上它就会被 SoftClip 切掉（用户 2026-09-19：「移动距离大时还是会被隐藏」）。
        DebugShiftMenu();
        PinRdChip();
        FitRdChipPlate();
        PinSponsor();
        FitMenuClipForChip();
        TraceSponsorVis();
        // 钉完再采样（帧窗内逐帧落行；窗外只在坐标变化时补记）。
        TraceChipSlide();
        TraceChipVis();
        ProbeRdUpdateWiring();
        PollRdUpdatePacks();
        FinishPickRdUpdatePacks();
        RefreshOnlineDataStatus();
        Menu.checkCommend();
    }

    /// <summary>探针节流计数（约 0.5s 落一行，避免刷屏）。</summary>
    private int rupdProbeTick;

    /// <summary>
    /// [rupd] 诊断探针：每 0.5s 对账一次 `rdUpdate_` 按钮的**回调链路**（deck_ 作对照）——
    /// UIButton 在不在、克隆残留的 MonoDelegate 的 actionInMono 是不是 null（Instantiate
    /// 不序列化托管委托 ⇒ 克隆按钮的回调链天生是空的，registEvent 没接上就会「点了没反应」）。
    /// </summary>
    private void ProbeRdUpdateWiring()
    {
        if ((++rupdProbeTick) % 30 != 0)
        {
            return;
        }
        // 注意：rdUpdate 行在 OCG 下是 SetActive(false) 的，必须搜**含失活**的组件
        //（GetComponentsInChildren<T>(true)），否则探针自己就先瞎了。
        UIButton b = null;
        MonoDelegate d = null;
        BoxCollider c = null;
        bool live = false;
        UIButton dk = null;
        MonoDelegate dkD = null;
        UIButton[] all = gameObject.transform.GetComponentsInChildren<UIButton>(true);
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i].name == "rdUpdate_")
            {
                b = all[i];
                d = all[i].GetComponent<MonoDelegate>();
                c = all[i].GetComponent<BoxCollider>();
                live = all[i].gameObject.activeInHierarchy;
            }
            else if (all[i].name == "deck_")
            {
                dk = all[i];
                dkD = all[i].GetComponent<MonoDelegate>();
            }
        }
        if (b == null)
        {
            QuickTestTrace.Log("rupd", "rdUpdate_ not found");
            return;
        }
        int ocCount = 0;
        string d0 = "none";
        if (b.onClick != null)
        {
            ocCount = b.onClick.Count;
            if (ocCount > 0 && b.onClick[0] != null)
            {
                d0 = (b.onClick[0].target == null ? "null" : b.onClick[0].target.GetType().Name);
            }
        }
        QuickTestTrace.Log("rupd", "btn=T"
            + " mono=" + (d != null)
            + " act=" + (d != null && d.actionInMono != null)
            + " col=" + (c != null && c.enabled)
            + " live=" + live
            + " onClick=" + ocCount + " d0=" + d0
            + " | deck act=" + (dkD != null && dkD.actionInMono != null));
    }

    // ============================ 徽标：验收钩子 / 可见性对账 ============================

    /// <summary>当前生效的菜单推距；<see cref="DebugShiftMenu"/> 用它算增量。</summary>
    private Vector2 debugShiftNow = Vector2.zero;

    /// <summary>上次探 qt_menushift.on 的时刻（自己再节流一层，文件「在」的那一侧 SwitchOn 不缓存）。</summary>
    private int debugShiftPollAtMs = -100000;

    /// <summary>
    /// 验收钩子：把主菜单整体**推开**一段（文件 `log/qt_menushift.on`，内容 `dx,dy`，
    /// 面板局部单位；留空则用默认推距）。
    ///
    /// 为什么需要它：「徽标在主菜单移动距离大时被隐藏」这条口径**不把菜单挪开就复现不出来** ——
    /// 菜单停在正位时裁剪区永远盖得住徽标（<see cref="FitMenuBack"/> 就是照落点常量放宽的）。
    /// 工程内唯一会平移根节点的是 `WindowServantSP.createWindow()` 读配置那一次
    /// （本机配置里是 (7,-45)，盖得住，看不太出来），所以给验收脚本一枚能把菜单推远的开关：
    /// 推开之后，徽标必须**仍在原来的屏幕位置、而且仍然可见**。
    /// 与其它 qt_* 一样：文件不在就零开销，在就 500ms 探一次。
    /// </summary>
    private void DebugShiftMenu()
    {
        if (!QuickTestTrace.Enabled)
        {
            return;
        }
        int now = Environment.TickCount;
        if (now - debugShiftPollAtMs < 500)
        {
            return;
        }
        debugShiftPollAtMs = now;

        Vector2 want = Vector2.zero;
        if (QuickTestTrace.SwitchOn("qt_menushift.on"))
        {
            want = new Vector2(260f, 160f);   // 默认推距：足够把徽标推出原裁剪区
            try
            {
                string s = System.IO.File.ReadAllText(QuickTestTrace.LogPath("qt_menushift.on")).Trim();
                string[] parts = s.Split(',');
                float dx;
                float dy;
                if (parts.Length == 2 && float.TryParse(parts[0], out dx) && float.TryParse(parts[1], out dy))
                {
                    want = new Vector2(dx, dy);
                }
            }
            catch (System.Exception)
            {
            }
        }

        Vector2 delta = want - debugShiftNow;
        if (delta.x == 0f && delta.y == 0f)
        {
            return;
        }
        debugShiftNow = want;
        Vector3 p = gameObject.transform.localPosition;
        gameObject.transform.localPosition = new Vector3(p.x + delta.x, p.y + delta.y, p.z);
        QuickTestTrace.Log("menushift", "shift=(" + want.x + "," + want.y + ")");
    }

    /// <summary>上次 [chipvis] 的判据串与时刻（变化才落行，另加 2s 心跳）。</summary>
    private string chipVisKey = null;
    private int chipVisAtMs = -100000;
    private int chipVisLines = 0;

    /// <summary>
    /// 徽标「到底画没画」的每帧对账（仅 qt_debug.on）。同一行报三样东西：
    ///   · `vis=` NGUI **自己**的判定（`UIPanel.IsVisible(widget)`，裁剪就是它说了算）；
    ///   · `inClip=` 按几何独立算的同一个结论（两者不一致就是我对 NGUI 的理解错了）；
    ///   · 芯片的面板局部坐标 / 裁剪区 / 根节点推距 / 屏幕落点。
    ///
    /// 为什么非要有它：`[rd] chip screen=` 只说明「几何中心算出来在哪」——
    /// 被 SoftClip 整颗切掉的徽标**照样报得出一组坐标**（那时照它点击就是「点了没反应」，
    /// 2026-09-19 那次「点 3 次 RD 入口都没切到 RD」就是这么来的）。
    /// 坐标 + 可见性一起看，才分得清「徽标在、点不到」和「徽标压根没画」。
    /// </summary>
    private void TraceChipVis()
    {
        if (!QuickTestTrace.Enabled || chipVisLines >= 200 || Program.camera_main_2d == null)
        {
            return;
        }
        UIPanel panel = gameObject.GetComponent<UIPanel>();
        Transform chip = gameObject.transform.Find(RdChipNode);
        if (panel == null || chip == null)
        {
            return;
        }
        Transform button = UIHelper.getByName<Transform>(chip.gameObject, RdChipButton);
        Vector3 anchor = button != null ? button.position : chip.position;
        UIWidget w = (button != null ? button.gameObject : chip.gameObject).GetComponent<UIWidget>();

        bool vis = w != null && panel.IsVisible(w);
        Vector3 lp = panel.transform.InverseTransformPoint(anchor);
        Vector4 cr = panel.finalClipRegion;
        float l = cr.x - cr.z * 0.5f;
        float r = cr.x + cr.z * 0.5f;
        float b = cr.y - cr.w * 0.5f;
        float t = cr.y + cr.w * 0.5f;
        bool inClip = lp.x >= l && lp.x <= r && lp.y >= b && lp.y <= t;
        Vector3 sp = Program.camera_main_2d.WorldToScreenPoint(anchor);

        string key = (vis ? "1" : "0") + (inClip ? "1" : "0")
            + Mathf.RoundToInt(lp.x) + "," + Mathf.RoundToInt(lp.y)
            + "|" + Mathf.RoundToInt(l) + "," + Mathf.RoundToInt(b) + "," + Mathf.RoundToInt(r) + "," + Mathf.RoundToInt(t)
            + "|" + Mathf.RoundToInt(sp.x) + "," + Mathf.RoundToInt(sp.y);
        int now = Environment.TickCount;
        if (key == chipVisKey && now - chipVisAtMs < 2000)
        {
            return;
        }
        chipVisKey = key;
        chipVisAtMs = now;
        chipVisLines++;

        Vector3 rootLocal = gameObject.transform.localPosition;
        QuickTestTrace.Log("chipvis",
            "mode=" + GameModeManager.ModeLabel
            + " vis=" + vis
            + " inClip=" + inClip
            + " chip=(" + Mathf.RoundToInt(sp.x) + "," + Mathf.RoundToInt(Screen.height - sp.y) + ")"
            + " local=(" + Mathf.RoundToInt(lp.x) + "," + Mathf.RoundToInt(lp.y) + ")"
            + " clip=(" + Mathf.RoundToInt(l) + "," + Mathf.RoundToInt(b) + "," + Mathf.RoundToInt(r) + "," + Mathf.RoundToInt(t) + ")"
            + " rootLocal=(" + Mathf.RoundToInt(rootLocal.x) + "," + Mathf.RoundToInt(rootLocal.y) + ")"
            + " shift=(" + Mathf.RoundToInt(debugShiftNow.x) + "," + Mathf.RoundToInt(debugShiftNow.y) + ")");
    }

    /// <summary>上次 [sponsor] 的判据串与时刻（变化才落行，另加 2s 心跳；口径同 chipVisKey）。</summary>
    private string sponsorVisKey = null;
    private int sponsorVisAtMs = -100000;
    private int sponsorVisLines = 0;

    /// <summary>
    /// 「支持作者」按钮的每帧对账探针（仅 qt_debug.on，口径同 <see cref="TraceChipVis"/>）：
    /// 报屏幕落点 + NGUI 自己的可见判定。坐标单独看不出来「画没画」——
    /// 被 SoftClip 整颗切掉的按钮照样报得出一组坐标，必须连 vis= 一起看。
    /// 验收脚本按 `screen=` 的落点做真实点击 / 截图判据。
    /// </summary>
    private void TraceSponsorVis()
    {
        if (!QuickTestTrace.Enabled || sponsorVisLines >= 200 || Program.camera_main_2d == null)
        {
            return;
        }
        UIPanel panel = gameObject.GetComponent<UIPanel>();
        Transform chip = gameObject.transform.Find(SponsorNode);
        if (panel == null || chip == null)
        {
            return;
        }
        Transform button = UIHelper.getByName<Transform>(chip.gameObject, SponsorButton);
        Vector3 anchor = button != null ? button.position : chip.position;
        UIWidget w = (button != null ? button.gameObject : chip.gameObject).GetComponent<UIWidget>();
        bool vis = w != null && panel.IsVisible(w);
        Vector3 sp = Program.camera_main_2d.WorldToScreenPoint(anchor);

        string key = (vis ? "1" : "0") + Mathf.RoundToInt(sp.x) + "," + Mathf.RoundToInt(sp.y);
        int now = Environment.TickCount;
        if (key == sponsorVisKey && now - sponsorVisAtMs < 2000)
        {
            return;
        }
        sponsorVisKey = key;
        sponsorVisAtMs = now;
        sponsorVisLines++;

        QuickTestTrace.Log("sponsor",
            "screen=(" + Mathf.RoundToInt(sp.x) + "," + Mathf.RoundToInt(Screen.height - sp.y) + ")"
            + " vis=" + vis);
    }

    /// <summary>主菜单 version_ 标签兼作在线数据更新状态显示，并在回到主菜单时兑现挂起的重载。</summary>
    private void RefreshOnlineDataStatus()
    {
        // 数据已落盘但当时不在主菜单（例如刚打完一局），回到主菜单这一帧补上内存重载。
        if (ClientDataUpdater.ReloadPending)
        {
            ClientDataUpdater.ApplyPendingReload();
        }

        if (_versionLabel == null)
        {
            return;
        }

        string status = ClientDataUpdater.StatusText();
        if (status == null)
        {
            _versionLabel.text = VersionBaseText;
            return;
        }

        // 「失败」不是瞬时过程，不能像进度那样一直占着版本号 —— 停留一会儿就还回去。
        if (ClientDataUpdater.State == ClientDataUpdater.UpdateState.Failed)
        {
            if (_failNoticeSince < 0f)
            {
                _failNoticeSince = Time.realtimeSinceStartup;
            }
            if (Time.realtimeSinceStartup - _failNoticeSince >= FailNoticeSeconds)
            {
                _versionLabel.text = VersionBaseText;
                return;
            }
        }
        else
        {
            _failNoticeSince = -1f;
        }

        _versionLabel.text = status;
    }

    public void onClickExit()
    {
        QuickTestTrace.Exit("menu.onClickExit exitOnReturn=" + Program.exitOnReturn);
        Program.I().quit();
        Program.Running = false;
        TcpHelper.SaveRecord();
#if !UNITY_EDITOR && (UNITY_ANDROID || UNITY_IPHONE) // IL2CPP 使用此方法才能退出
        Application.Quit();
#else
        Process.GetCurrentProcess().Kill();
#endif
    }

    void onClickOnline()
    {
        Program.I().shiftToServant(Program.I().selectServer);
    }

    void onClickMyCard()
    {
        Program.I().shiftToServant(Program.I().mycard);
    }

    void onClickAI()
    {
        Program.I().shiftToServant(Program.I().aiRoom);
    }

    void onClickPizzle()
    {
        Program.I().shiftToServant(Program.I().puzzleMode);
    }

    void onClickReplay()
    {
        Program.I().shiftToServant(Program.I().selectReplay);
    }

    void onClickSetting()
    {
        Program.I().setting.show();
    }

    void onClickSuperPre()
    {
        Program.I().shiftToServant(Program.I().superPreList);
    }

    public void StartSuperPreUpdateFromList()
    {
        if (isPreDownloading || _isSuperPreUpdateRequested)
        {
            Program.PrintToChat("超先行卡正在检查或下载，请稍候...");
            return;
        }

        _isSuperPreUpdateRequested = true;
        HideNewBadge();
        Program.I().StartCoroutine(CheckAndDownloadSuperPreFromListCoroutine());
    }

    private IEnumerator CheckAndDownloadSuperPreFromListCoroutine()
    {
        while (_isCheckingUpdate)
        {
            yield return null;
        }

        HideNewBadge();
        _isCheckingUpdate = true;
        try
        {
            yield return Program.I().StartCoroutine(CheckAndDownloadSuperPreCoroutine());
        }
        finally
        {
            _isCheckingUpdate = false;
            _isSuperPreUpdateRequested = false;
        }
    }

    void onClickSelectDeck()
    {
        Program.I().shiftToServant(Program.I().selectDeck);
    }

    void onClickDownloadSuperPre()
    {
        if (isPreDownloading)
        {
            Program.PrintToChat("正在处理中，请稍候...");
            return;
        }
        // 启动包装器协程
        Program.I().StartCoroutine(DownloadAndApplySuperPrePackCoroutine());
    }

    /// <summary>
    /// 检查超先行卡是否有更新，如果有则直接下载，如果没有则询问是否强制下载
    /// </summary>
    private IEnumerator CheckAndDownloadSuperPreCoroutine()
    {
        if (isPreDownloading)
        {
            Program.PrintToChat("正在处理中，请稍候...");
            yield break;
        }
        
        Program.PrintToChat("正在检查超先行卡更新...");
        
        bool hasUpdate = false;
        yield return Program.I().StartCoroutine(UnityFileDownloader.CheckForUpdateAsync(
            SuperPreDownloadUrl,
            SuperPreLocalPath,
            (result) => { hasUpdate = result; }
        ));
        
        if (hasUpdate)
        {
            // 有更新，直接下载
            Program.I().StartCoroutine(DownloadAndApplySuperPrePackCoroutine());
        }
        else
        {
            // 没有更新，询问是否强制重新下载
            RMSshow_yesOrNo(
                "CONFIRM_FORCE_DOWNLOAD_SUPER_PRE",
                "您的超先行卡包已是最新版本。\n是否仍然重新下载并安装？",
                new messageSystemValue { value = "yes", hint = "重新下载" },
                new messageSystemValue { value = "no", hint = "取消" }
            );
        }
    }
    
    /// <summary>
    /// 强制下载超先行卡包（跳过版本检查，直接下载）
    /// </summary>
    private IEnumerator ForceDownloadSuperPrePackCoroutine()
    {
        if (isPreDownloading)
        {
            Program.PrintToChat("正在处理中，请稍候...");
            yield break;
        }

        isPreDownloading = true;

        try
        {
            string downloadsDir = "downloads";
            string expansionsDir = "expansions";
            string ypkFileName = "ygopro-super-pre.ypk";
            string ypkFilePathInDownloads = Path.Combine(downloadsDir, ypkFileName);

            if (!Directory.Exists(downloadsDir)) Directory.CreateDirectory(downloadsDir);
            if (!Directory.Exists(expansionsDir)) Directory.CreateDirectory(expansionsDir);

            Program.PrintToChat("开始强制重新下载超先行卡包...\n请注意超先行卡更新会清空原来的 expansions 文件夹");

            bool downloadCompleted = false;
            string downloadFailureReason = null;
            int lastReportedProgress = -10;

            // HEAD 仅用于取得文件大小；强制下载不比较 ETag，大文件仍走 Range 分段。
            yield return Program.I().StartCoroutine(UnityFileDownloader.DownloadFileWithHeadCheck(
                SuperPreDownloadUrl,
                ypkFilePathInDownloads,
                (success) => { downloadCompleted = success; },
                (progress) =>
                {
                    int currentProgress = Mathf.FloorToInt(progress * 100);
                    if (currentProgress >= lastReportedProgress + 10)
                    {
                        Program.PrintToChat($"下载进度: {currentProgress}%");
                        lastReportedProgress = currentProgress;
                    }
                },
                forceDownload: true,
                onError: (reason) => { downloadFailureReason = reason; }
            ));

            if (!downloadCompleted)
            {
                Program.PrintToChat(string.IsNullOrEmpty(downloadFailureReason)
                    ? "下载失败，请检查网络或稍后再试。"
                    : $"下载失败：{downloadFailureReason}");
                yield break;
            }

            // 应用更新
            Program.PrintToChat("下载完成，开始应用更新...");
            yield return null;

            Program.PrintToChat("正在清理旧文件...");
            try
            {
                ReleaseExpansionZipHandles(expansionsDir);
                ClearDirectory(expansionsDir);
            }
            catch (Exception e)
            {
                Program.PrintToChat($"清理旧文件失败: {e.Message}");
                yield break;
            }
            yield return null;

            Program.PrintToChat("正在安装新文件...");
            try
            {
                InstallSuperPrePack(ypkFilePathInDownloads, expansionsDir);
                ReloadExpansionAfterUpdate(expansionsDir);

                Program.PrintToChat("超先行卡包安装完成，卡包数据已重新载入。");
            }
            catch (Exception e)
            {
                Program.PrintToChat($"安装失败: {e.Message}");
            }
        }
        finally
        {
            isPreDownloading = false;
        }
    }

    private IEnumerator DownloadAndApplySuperPrePackCoroutine()
    {
        isPreDownloading = true;

        var workerEnumerator = DownloadAndApplySuperPrePack_Worker();

        while (true)
        {
            object current = null;
            try
            {
                if (!workerEnumerator.MoveNext())
                {
                    break; // 工作完成，跳出循环
                }
                current = workerEnumerator.Current;
            }
            catch (Exception e)
            {
                Program.PrintToChat($"处理更新时发生严重错误: {e.Message}");
                UnityEngine.Debug.LogError($"[SuperPreUpdate] Error: {e.ToString()}");
                break; // 发生错误，跳出循环
            }

            yield return current;
        }

        isPreDownloading = false;
    }

    private IEnumerator DownloadAndApplySuperPrePack_Worker()
    {
        // --- 1. 定义常量和路径 ---
        string downloadsDir = "downloads";
        string expansionsDir = "expansions";
        string ypkFileName = "ygopro-super-pre.ypk";
        string ypkFilePathInDownloads = Path.Combine(downloadsDir, ypkFileName);

        // --- 2. 确保目录存在 ---
        // (同步操作，如果失败会直接抛出 IO 异常)
        if (!Directory.Exists(downloadsDir)) Directory.CreateDirectory(downloadsDir);
        if (!Directory.Exists(expansionsDir)) Directory.CreateDirectory(expansionsDir);

        Program.PrintToChat("开始检查超先行卡更新...\n请注意超先行卡更新会清空原来的 expansions 文件夹");

        // --- 3. 下载文件 ---
        bool downloadCompleted = false;
        bool newVersionDownloaded = false;
        string downloadFailureReason = null;
        int lastReportedProgress = -10;

        // 使用一个临时变量来接收 StartCoroutine 的结果，因为我们需要 yield 它
        var downloadCoroutine = UnityFileDownloader.DownloadFileWithHeadCheck(
            SuperPreDownloadUrl,
            ypkFilePathInDownloads,
            (success) => { downloadCompleted = success; },
            (progress) =>
            {
                int currentProgress = Mathf.FloorToInt(progress * 100);
                if (currentProgress >= lastReportedProgress + 10)
                {
                    Program.PrintToChat($"下载进度: {currentProgress}%");
                    lastReportedProgress = currentProgress;
                }
            },
            () =>
            {
                newVersionDownloaded = true;
                Program.PrintToChat("检测到超先行卡包有更新，开始下载...");
            },
            null,
            (reason) => { downloadFailureReason = reason; }
        );

        yield return Program.I().StartCoroutine(downloadCoroutine);

        // --- 4. 处理下载结果 ---
        if (!downloadCompleted)
        {
            throw new Exception(string.IsNullOrEmpty(downloadFailureReason)
                ? "文件下载失败，请检查网络或稍后再试。"
                : "文件下载失败：" + downloadFailureReason);
        }

        // --- 5. 应用更新 ---
        if (newVersionDownloaded)
        {
            Program.PrintToChat("下载完成，开始应用更新...");
            yield return null;

            Program.PrintToChat("正在清理旧文件...");
            // 同步操作，如果失败会抛出异常
            ReleaseExpansionZipHandles(expansionsDir);
            ClearDirectory(expansionsDir);
            yield return null;

            Program.PrintToChat("正在安装新文件...");
            InstallSuperPrePack(ypkFilePathInDownloads, expansionsDir);
            ReloadExpansionAfterUpdate(expansionsDir);

            Program.PrintToChat("超先行卡包安装完成，卡包数据已重新载入。");
        }
        else
        {
            Program.PrintToChat("您的超先行卡包已是最新版本，无需更新。");
        }
    }


    /// <summary>
    /// [辅助方法] 释放游戏自己持有的 expansions 卡包句柄。
    /// 游戏启动时会把 expansions/*.ypk 交给 DotNetZip（GameZipManager.Zips），
    /// 句柄一直握着且共享模式不允许删除 ⇒ 直接 File.Delete 必然「共享冲突」，
    /// 表现就是「清理旧文件失败」。替换卡包前必须先释放。
    /// </summary>
    private void ReleaseExpansionZipHandles(string dirPath)
    {
        int released = GameZipManager.ReleaseZipsUnder(dirPath);
        if (released > 0)
        {
            Program.PrintToChat($"已释放 {released} 个旧卡包句柄（否则无法清理）");
        }
    }

    /// <summary>
    /// [辅助方法] 把下载到的超先行卡包安装进 expansions。
    ///
    /// 不做解压：PC 端消费超先行卡包的方式是「整包当 zip 交给 GameZipManager」——
    /// script/ 走 Percy 的 zip 查找、pics/ 走 GameTextureManager 的 zip 查找、
    /// test-*.cdb 与 test-strings.conf 由 Program 重放 zip 条目（启动和 ReloadGameDatabases
    /// 两条路都会重放）。解压成 expansions/ 下的子目录没有任何代码会去读，
    /// 反而会把原来能生效的整包删掉。
    /// </summary>
    private void InstallSuperPrePack(string packFilePath, string dirPath)
    {
        string target = Path.Combine(dirPath, Path.GetFileName(packFilePath));
        File.Copy(packFilePath, target, true);
        Program.PrintToChat($"已安装 {Path.GetFileName(packFilePath)}（{new FileInfo(target).Length / 1048576} MB）");
    }

    /// <summary>
    /// [辅助方法] 卡包替换完成后重新登记句柄并整体重放数据，
    /// 让新包在本次运行内生效（不必重启游戏）。
    /// </summary>
    private void ReloadExpansionAfterUpdate(string dirPath)
    {
        int opened = GameZipManager.OpenZipsUnder(dirPath);
        if (opened <= 0)
        {
            Program.PrintToChat("警告：新卡包句柄未能重新登记，请重启游戏后再使用先行卡。");
            return;
        }
        if (!Program.I().ReloadGameDatabases())
        {
            Program.PrintToChat("警告：卡包数据重载失败，请重启游戏后再使用先行卡。");
        }
    }

    /// <summary>
    /// [辅助方法] 安全地清空一个目录下的所有文件和子目录。
    /// 逐个删、失败不中断，最后把「删不掉的」连原因一起抛出去，便于定位是谁占着文件。
    /// </summary>
    private void ClearDirectory(string dirPath)
    {
        if (!Directory.Exists(dirPath)) return;
        DirectoryInfo di = new DirectoryInfo(dirPath);
        List<string> failures = new List<string>();
        foreach (FileInfo file in di.GetFiles())
        {
            try
            {
                file.Delete();
            }
            catch (Exception e)
            {
                failures.Add($"{file.Name}（{e.GetType().Name}: {e.Message}）");
            }
        }
        foreach (DirectoryInfo dir in di.GetDirectories())
        {
            try
            {
                dir.Delete(true);
            }
            catch (Exception e)
            {
                failures.Add($"{dir.Name}/（{e.GetType().Name}: {e.Message}）");
            }
        }
        if (failures.Count > 0)
        {
            throw new IOException(
                "以下文件删不掉，多半仍被占用（重启游戏后再试）：" + string.Join("；", failures));
        }
    }

    public static void deleteShell()
    {
        try
        {
            if (File.Exists("commamd.shell") == true)
            {
                File.Delete("commamd.shell");
            }
        }
        catch (Exception) { }
    }

    static int lastTime = 0;

    public static void checkCommend()
    {
        if (Program.TimePassed() - lastTime > 1000)
        {
            lastTime = Program.TimePassed();
            if (Program.I().selectDeck == null)
            {
                return;
            }
            if (Program.I().selectReplay == null)
            {
                return;
            }
            if (Program.I().puzzleMode == null)
            {
                return;
            }
            if (Program.I().selectServer == null)
            {
                return;
            }
            if (Program.I().mycard == null)
            {
                return;
            }
            // 命令文件是「外部写入、游戏消费」的单向通道：
            //   * 启动参数 -j/-d/-r/-s 由 Program 自己写（见 Program.Start 里的 cmdFile）；
            //   * 调试/验收脚本直接在游戏根目录写它。
            // ⚠ 这里**只读不建**。上游版本缺文件时会 File.Create，于是成品 exe 一进主菜单
            // 就在游戏根目录留下一个 0KB 的 commamd.shell，删掉下一秒又冒出来 —— 现在没有这个文件
            // 就什么都不做，不会有任何残留。
            if (File.Exists("commamd.shell") == false)
            {
                return;
            }
            string all = "";
            try
            {
                all = File.ReadAllText("commamd.shell", Encoding.UTF8);
                if (all.Trim() == "")
                {
                    // 空文件不是命令（也包含旧版本留下的 0KB 残骸），顺手清掉。
                    File.Delete("commamd.shell");
                    return;
                }
                string[] mats = all.Split(" ");
                if (mats.Length > 0)
                {
                    switch (mats[0])
                    {
                        case "online":
                            if (mats.Length == 5)
                            {
                                UIHelper.iniFaces(); //加载用户头像
                                Program
                                    .I()
                                    .selectServer.KF_onlineGame(mats[1], mats[2], mats[3], mats[4]);
                            }
                            if (mats.Length == 6)
                            {
                                UIHelper.iniFaces();
                                Program
                                    .I()
                                    .selectServer.KF_onlineGame(
                                        mats[1],
                                        mats[2],
                                        mats[3],
                                        mats[4],
                                        mats[5]
                                    );
                            }
                            break;
                        case "edit":
                            if (mats.Length == 2)
                            {
                                Program.I().selectDeck.KF_editDeck(mats[1]); //编辑卡组
                            }
                            break;
                        case "replay":
                            if (mats.Length == 2)
                            {
                                UIHelper.iniFaces();
                                Program.I().selectReplay.KF_replay(mats[1]); //编辑录像
                            }
                            break;
                        case "puzzle":
                            if (mats.Length == 2)
                            {
                                UIHelper.iniFaces();
                                Program.I().puzzleMode.KF_puzzle(mats[1]); //运行残局
                            }
                            break;
                        case "test":
                            // 卡组测试直开局（调试用）：等价于点卡组界面的「测试」按钮，
                            // 不依赖鼠标坐标，用来把「点击」这一段从排查链路里摘出去。
                            // 只在该 servant 处于显示状态时才会被消费（Servant.Update 的 isShowed 门槛），
                            // 所以这条命令只在主菜单界面有效，在卡组编辑器里写是没用的。
                            if (mats.Length >= 2)
                            {
                                GameModeManager.SetDeckInUse(mats[1]);
                            }
                            Program.I().aiRoom.quickTestFromShell();
                            break;
                        case "testshuffle":
                            // 同 test，但按正常随机洗牌开局。用于验证
                            // 「同一颗种子(qt_seed.txt) → 同一条对局消息流」。
                            if (mats.Length >= 2)
                            {
                                GameModeManager.SetDeckInUse(mats[1]);
                            }
                            Program.I().aiRoom.quickTestFromShell(false);
                            break;
                        case "ai":
                            // 主菜单「人机对战」的直开局（验收/排查用）。
                            //
                            // 与点开人机界面 → 选一个对手 → 点「开始」完全等价（同一条
                            // AIRoom.launch，且**不**置 Room.quickStart）：之后的房间界面
                            // 照常出场，「决斗准备」/「开始游戏」由玩家手点，或由
                            // qt_autojoin.on 自动走（见 Room.preFrameFunction）。
                            //
                            // 存在的意义是把「点菜单、点对手、点开始」这三段从排查链路里摘出去 ——
                            // 与 `test` 对卡组界面「测试」按钮做的事一样。
                            // 用法：commamd.shell 写
                            //   ai [卡组名] [对手序号] [lockhand 0/1] [nocheck 0/1] [noshuffle 0/1]
                            // 省略时：对手序号 0、lockhand 开、nocheck 关、noshuffle 关。
                            {
                                if (mats.Length >= 2 && mats[1] != "")
                                {
                                    GameModeManager.SetDeckInUse(mats[1]);
                                }
                                int botIdx = 0;
                                if (mats.Length >= 3)
                                {
                                    int.TryParse(mats[2], out botIdx);
                                }
                                bool lh = mats.Length < 4 || mats[3] != "0";
                                bool nc = mats.Length >= 5 && mats[4] == "1";
                                bool ns = mats.Length >= 6 && mats[5] == "1";
                                Program.I().aiRoom.launchMenuStyleDuel(botIdx, lh, nc, ns);
                            }
                            break;
                        case "mode":
                            // 模式直切（验收/排查用）：等价于点 RD 入口按钮（onClickRdChip 同一套
                            // UI 刷新 + 探针重报），但不依赖鼠标注入 —— 2026-09-24 下午实锤过
                            // 「SetCursorPos 生效、hover 探针正常，mouse_event 的按下从未到达
                            // Input 层」（down=1 全程 0 行），此时验收用这条兜底继续走链路。
                            // 用法：commamd.shell 写「mode RD」或「mode OCG」。
                            // ⚠ 冷启动恒 OCG，同模式重复 Set 是空操作（不会打 [mode] set 行）。
                            {
                                GameModeManager.Set(mats.Length >= 2 && mats[1] == "OCG"
                                    ? GameModeManager.Mode.OCG
                                    : GameModeManager.Mode.RD);
                                // shell 处理在静态方法里，UI 刷新/探针重报要走单例（与 onClickRdChip 同一套）。
                                Program.I().menu.RefreshRdUi();
                                Program.I().menu.TraceAllIcons();
                                Program.I().menu.TraceRdChip();
                            }
                            break;
                        default:
                            break;
                    }
                }
            }
            catch (Exception e)
            {
                // Debug.Log(e);
            }
            try
            {
                // 命令已消费：直接删文件而不是倒成空串。根目录不留空壳，
                // 同一拍里也不会把还没被消费掉的同一条命令再执行一遍。
                if (File.Exists("commamd.shell") == true)
                {
                    File.Delete("commamd.shell");
                }
            }
            catch (Exception e)
            {
                // UnityEngine.Debug.Log(e);
            }
        }
    }
}

