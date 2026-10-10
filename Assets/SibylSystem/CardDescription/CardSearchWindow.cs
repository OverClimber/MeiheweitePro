using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 「点卡名 / 点字段 → 直接检索」的结果窗（需求 2~4 的落点）。
///
/// 复用的是卡组编辑器那张检索面板 prefab（<see cref="Program.new_ui_search"/>）：
/// 输入框 / 列表 / 滚动条 / 搜索钮都是现成的，这里只做四件事 ——
/// ① 摆到屏幕右侧（左侧留给卡牌说明面板，两块不打架）；
/// ② 把 <see cref="VirtualScrollView"/> 挂上 <c>panel_</c>/<c>bar_</c>，条目用
///    <see cref="Program.new_ui_cardOnSearchList"/>（和编辑器里一模一样）；
/// ③ 悬停条目 → 左侧说明面板跟着换（与卡组编辑器同一手感）；
/// ④ 输入框左端补一颗「返回上一级」钮，见 <see cref="goBack"/>。
///
/// ⚠ 挂哪一层：本窗是**弹窗**，必须挂在最高的那层 2D 相机（<c>ui_main_2d</c>，depth 3）上。
///   原先挂在 ui_back_ground_2d（depth -2）——那是「最底下一层」，卡组编辑器那张 3D 卡桌
///   （camera_game_main，depth 0）会整个盖在它上面，玩家在编辑器里点链接等于什么都没弹。
///   落点用的是 camera_main_2d，与这层本来就是同一套坐标，换层不动几何，只改谁盖谁。
///
/// ⚠ 不重叠（用户 2026-10-02 口径：两块**并排**）：卡组编辑器右边那块检索面板与本窗
///   原先落点完全一样（两边都算 Screen.width - 面板宽 / 2），一出来就是叠着的。
///   老做法是"本窗一显示就把编辑器那块整块借走、挪出屏幕"，现在改成**本窗左移让位**：
///   编辑器面板留在屏幕最右原位，本窗落在它**左边**（见 <see cref="TargetCenterX"/>）；
///   编辑器面板没开时本窗仍贴右侧原位 —— 屏幕上永远同时看得见两块，谁也不借谁的位。
///   两边的显隐由 <see cref="onDeckSearchPanelToggled"/> 联动重排。
///
/// 性能（需求 6）：窗口在启动时建一次、之后只移动显隐；检索走的是
/// <see cref="YGOSharp.CardsManager.searchAdvanced"/> 一次全池扫描（纯文本包含，无正则），
/// 结果条目复用 VirtualScrollView 的可见区懒创建 —— 一万张命中也只实例化看得见的十几行。
/// 返回上一级只是个 List（存条件两个字段），没有任何每帧成本。
/// </summary>
public class CardSearchWindow : Servant
{
    /// <summary>「返回上一级」钮的 GameObject 名（也就是它的点按目标名）。</summary>
    public const string BackButtonName = "back_";

    /// <summary>
    /// 「译」钮的 GameObject 名（点按目标名同）。用户 2026-10-03：在撤回/X 那颗旁边
    /// 再加一颗，用来**批量**决定这一批关联卡用哪种译名（跟随/原生/nw/cnocg/简中）。
    /// </summary>
    public const string PackButtonName = "packtrans_";

    /// <summary>
    /// 「译」钮的图标（texture/ui/translate_searchbar.png）。
    ///
    /// ⛔ **不能复用说明面板那颗 <c>translate_card_shower</c>**：那张是给说明面板那一列
    /// 造的，规格是"对齐 arrow_in_card_shower"——125x103 画布、淡紫蓝 RGB(190,185,219)、
    /// 峰值 alpha 只有 165。而检索窗这一排（left / no / search / detail）是 40x40、
    /// 中性灰（实测 RGB 209~220、**饱和度 0**）、峰值 alpha 236~245。
    /// 把前者摆进后者那一行，实测（都缩到 widget 真实的 25x25 再量）：
    ///     left.png  peakA=245 cov=31.2%     译(旧图)  peakA=152 cov=22.2%
    /// 亮度不到邻居的一半、还是带色的 ⇒ 一眼就是"贴上去的纸片"。
    /// 所以另做一张 40x40 中性灰的，颜色/alpha/覆盖率全部由
    /// <c>_gen_translate_searchbar_icon.py</c> 从那四张邻居图**实测**校出来。
    /// </summary>
    const string PackIconName = "translate_searchbar";

    /// <summary>返回钮的图标（texture/ui/left.png，与面板里 search_ / detailed_ 两颗同族）。</summary>
    const string BackIconName = "left";

    /// <summary>
    /// 退到根（没有上一级）时换成它 —— texture/ui/no.png 那枚 X，与放大镜 / 分类同族同规格。
    /// 用户 2026-10-02 口径：退无可退时这颗钮就是「关掉本窗」，图标也得是关闭语义。
    /// </summary>
    const string CloseIconName = "no";

    const int IconSize = 25;

    /// <summary>
    /// 「译」钮的命中区边长。**必须与图标 widget 同尺寸**（25），不能另设一个值：
    /// 邻居两颗（放大镜 / 分类）在这个 prefab 里量到的就是 25x25
    /// （<c>new_search_remaster.prefab</c> 的 <c>mWidth/mHeight=25</c>），跟它们不一致就
    /// 是"点不准/看起来大一号"。
    /// </summary>
    const int PackHitSize = 25;

    /// <summary>「译」钮的悬停提示正文（用户 2026-10-03 第二轮口径，原文照用）。</summary>
    public const string PackHintText = "批量选择关联卡的翻译项";

    /// <summary>
    /// 返回钮内所有 widget 的 depth。
    /// ⛔ 画序命门（2026-10-02 第二轮实测）：本钮克隆自 search_，注册进面板的时机极早
    ///   —— drawcall renderQueue=3002，几乎**全场最先画**；而窗底板 under_ 同 depth=0
    ///   但 queue 更晚 ⇒ 底板**叠在图标上面**，把 X/箭头压暗一半（glyph 实测 75 vs
    ///   放大镜 156；color/finalAlpha/shader/贴图全查过一模一样）。同 depth 的排序是
    ///   注册序，不稳定也不可控 —— 显式抬到 prefab 全 0 之上，画序就钉死在最上层。
    /// </summary>
    const int BackIconDepth = 3;

    /// <summary>
    /// 返回钮图标的兜底色 —— **只在放大镜那颗图标读不到时**才用。
    /// ⛔ 正常情况下不用它：颜色是从放大镜图标**逐字段抄过来**的（<see cref="backIconColor"/>）。
    ///   用户 2026-10-02 的口径是「返回钮要和搜索界面的放大镜、分类图标一个风格（颜色等）」，
    ///   写死一个常数就等于把这个口径钉在一个未必等于放大镜的值上。
    /// </summary>
    static readonly Color BackIconFallback = new Color(1f, 1f, 1f, 0.78431374f);

    GameObject win;
    UIInput input;
    UIPanel panel;
    UIScrollBar bar;
    VirtualScrollView scroll;
    UITexture back;
    GameObject backButton;

    /// <summary>返回/关闭钮图标的实际配色：建钮时从放大镜(search_)那颗图标抄下来，之后每次自愈都还原成它。</summary>
    Color backIconColor = BackIconFallback;

    /// <summary>
    /// 「译」钮（<see cref="PackButtonName"/>）的句柄。
    /// ⚠ 建法是**克隆 back_**（不是自己 new、也不是克隆 search_）：back_ 本身已经是
    /// 「克隆一颗现成能用的钮」做出来的成品，碰撞体 / 面板归属 / UIButton 点按配色 /
    /// 点按音效整套都齐；克隆它等于"跟返回钮长得一模一样"这个口径由构造保证，
    /// 而不是靠我逐个字段抄（抄漏一个就会出现"颜色差半档"——本题已经犯过多次）。
    /// </summary>
    GameObject packButton;

    /// <summary>本窗最近一次检索的结果（供「译」钮批量换表时取卡号）。</summary>
    List<YGOSharp.Card> lastResult = new List<YGOSharp.Card>();

    /// <summary>
    /// 本窗这次展示带的 **RD 限定条件**（来自 RD 简介里点的那段「怪兽（银河族）」之类），
    /// 空串 = 普通检索。它和 <see cref="lastQuery"/> / <see cref="lastMask"/> 一起进 history，
    /// 所以「返回上一级」能把限定条件一起退回去。
    /// </summary>
    string lastRd = "";

    /// <summary>检索档位（用户 2026-10-03「卡名跳转」那条需求）。</summary>
    public enum Filter
    {
        /// <summary>普通全文检索（系列名、种类词、玩家自己敲的字都走这一档）。</summary>
        Text = 0,
        /// <summary>只出「<b>卡名</b>精确等于」的那些卡。判据：<c>Card.Name == key</c>。</summary>
        ExactName = 1,
        /// <summary>只出「<b>卡文里记述了</b>这个卡名」的那些卡。判据：<c>Card.Desc</c> 含该串。</summary>
        Record = 2,
        /// <summary>
        /// 「<c>X</c>」<b>或者有那个卡名记述的</b>「魔法·陷阱卡」那种整句（用户 2026-10-03）：
        /// <b>并集</b> = 同名的卡 ∪ 卡文里记述了 X 的卡。
        /// ⛔ 掩码只作用在<b>记述那一支</b>（见 <see cref="RunUnionQuery"/>）。
        /// </summary>
        NameOrRecord = 3,
        /// <summary>
        /// 简介系列行点的**单个系列名**（用户 2026-10-04）：按 <c>Card.Setcode</c> 的
        /// 16 位槽精确比对 —— 与画系列行用的 <c>GameStringHelper.getSetName</c>
        /// 同一份判据，显示什么就搜什么，不掺"卡文里提到它"的无关卡。
        /// lastQuery 里放的是**显示名**（只进标题），真正的判据在 lastSetHash。
        /// </summary>
        Setcode = 4,
    }

    /// <summary>
    /// 本窗这一档用哪种判据（<see cref="Filter"/>）。和 <see cref="lastQuery"/> /
    /// <see cref="lastMask"/> / <see cref="lastRd"/> 一起进 history，返回钮才退得回去。
    ///
    /// <para><b>为什么必须分档，不能一律走全文检索</b>（用户 2026-10-03）：
    /// 全文检索的口径是「卡名**或卡文**里包含这个串」。点「拉之翼神龙」时它会把
    /// 「拉之翼苏拉娜丝」「翼神龙…」以及**卡文里提到拉之翼神龙**的一堆卡全倒出来。
    /// 而这三档要的是三种不同的东西：同名 / 卡文提到它 / 系列名相关。</para>
    /// </summary>
    Filter lastFilter = Filter.Text;

    /// <summary>
    /// 详情版（点链接弹出那版）已经把输入框 / 放大镜钮 / 分类钮拆掉。见 <see cref="StripToDetailMode"/>。
    /// ⚠ 只做一次：本窗是启动时建一次、之后只移动显隐的，重复拆会把已经挪好的返回钮又推回去。
    /// </summary>
    bool detailApplied;
    /// <summary>放大镜钮原来的落点（父级 + 局部位置 + 缩放）—— 返回钮要顶替的就是它。</summary>
    Transform detailSlotParent;
    Vector3 detailSlotPos = Vector3.zero;
    Vector3 detailSlotScale = Vector3.one;

    /// <summary>一次「上一级」的落点：玩家点过的那个字段 / 种类 / 整句（含 RD 限定条件）。</summary>
    class Entry
    {
        public string query;
        public int mask;
        public string rd;
        public Filter filter;
        /// <summary><see cref="Filter.Setcode"/> 档的系列 hash（其他档不用，存 -1）。</summary>
        public int setHash = -1;
        /// <summary>这一级的标题串（照抄玩家点的那句话；空 = 按档位自己拼）。</summary>
        public string title;
    }

    readonly List<Entry> history = new List<Entry>();

    /// <summary>
    /// 「借走卡组编辑器那块面板」的旗子 —— 已按用户 2026-10-02 口径**删除**：
    /// 两块面板改成并排，本窗左移让位，不再需要谁把谁挪出屏幕。
    /// </summary>

    string lastQuery = "";
    int lastMask = 0;

    /// <summary>
    /// 标题专用串（用户 2026-10-04 第三条）：**照抄玩家点的那句话**（如 <c>「融合」魔法卡</c>），
    /// 由链接生成端随 payload 带过来（<c>CardTextLinker</c> 的第三段）。
    ///
    /// <para>空串 = 没带 ⇒ <see cref="Search"/> 退回按档位自己拼的老写法。
    /// 它要跟着 history 一起走，否则"返回上一级"的标题会串味（见 <see cref="Entry.title"/>）。</para>
    /// </summary>
    string lastTitle = "";

    /// <summary>
    /// <see cref="Filter.Setcode"/> 档的**真判据**（!setname 表内 hash）。
    /// 不进 history 的落点 —— 「上一级」退回来时按 <see cref="Entry"/> 里存的
    /// query/mask/rd/filter 重放，Setcode 档的 hash 得跟着一起走，
    /// 所以 <see cref="Entry"/> 里也有一份（见 <see cref="ShowSetcode"/>）。
    /// </summary>
    int lastSetHash = -1;

    // ── 下面三个只给离线验收读（qt_debug.on 下的 [link] backprobe 行）。
    //    故意做成只读属性：验收脚本要能判「退回上一级」到底退对了没有，
    //    而不是只看一行日志 —— 日志里写的可能是愿望，属性读的是现状。

    /// <summary>验收用：现在这一级用的关键字。</summary>
    public string ProbeLastQuery { get { return lastQuery; } }

    /// <summary>验收用：现在这一级用的主类掩码。</summary>
    public int ProbeLastMask { get { return lastMask; } }

    /// <summary>验收用：现在这一级的标题串（照抄玩家点的那句话；空 = 按档位自己拼）。</summary>
    public string ProbeLastTitle { get { return lastTitle ?? ""; } }

    /// <summary>验收用：还能退几级。</summary>
    public int ProbeHistoryDepth { get { return history.Count; } }

    /// <summary>验收用：把**返回钮的图标**描红一次，肉眼核对「到底画没画、画在哪」。
    /// ⛔ 别改成染 <c>back</c>（那是容器 <c>under_</c> 的字段名）：染容器得到的是
    ///   「半透明红糊满整列」，既看不出钮的位置，也会把整扇窗的观感改掉（2026-10-02 踩过）。</summary>
    public void markForScreenshot()
    {
        // ⛔ `back` 这个字段是**容器 under_**（见 initialize 里的赋值），不是那颗钮 ——
        //   第六轮实拍才发现：以前这里染的是整扇窗的底图（半透明红糊满整列），
        //   而日志注释一直写着「把返回钮染成红色」⇒ 取证口径与实现不符。
        //   取证要的是「钮在哪」，所以必须染钮**自己的图标**。
        GameObject b = win != null ? FindDeep(win, BackButtonName) : null;
        UITexture icon = b != null ? b.GetComponentInChildren<UITexture>(true) : null;
        if (icon == null)
        {
            return;
        }
        icon.color = new Color(1f, 0f, 0f, 0.9f);
    }

    /// <summary>验收用：本窗此刻到底画在哪 —— 层次、屏幕坐标、由哪台相机渲染，控件有没有归到面板。</summary>

    public string ProbeWindowInfo
    {
        get
        {
            if (win == null)
            {
                return "win=null";
            }
            string camInfo = "panelCam=?";
            if (panel != null && panel.anchorCamera != null)
            {
                camInfo = "panelCam=" + panel.anchorCamera.name + "@" + panel.anchorCamera.depth
                    + " panelDepth=" + panel.depth;
            }
            Vector3 sp = Program.camera_main_2d != null
                ? Program.camera_main_2d.WorldToScreenPoint(win.transform.position)
                : Vector3.zero;
            return "layer=" + win.layer + " active=" + win.activeInHierarchy
                + " screen=(" + Mathf.RoundToInt(sp.x) + "," + Mathf.RoundToInt(Screen.height - sp.y) + ")"
                + " underW=" + (back != null ? back.width : -1)
                + " underH=" + (back != null ? back.height : -1)
                + " underPanel=" + (back != null && back.panel != null ? back.panel.name : "无")
                + " rootPanel=" + (win.GetComponent<UIPanel>() != null ? "有" : "无")
                + " " + camInfo;
        }
    }

    /// <summary>
    /// 验收用：把「控件到底归到哪个面板、那个面板由哪台相机画、层对不对」一次性摊开。
    ///
    /// 为什么专门量这个：实测 wininfo 显示 `underPanel=mod_2d_ui(Clone)` —— under_ 归到的
    /// 不是本窗自己的 panel_，而是某个祖先面板。NGUI 只把控件画进它 mPanel 指到的那个面板，
    /// 面板 / 相机 / 层三者只要有一处对不上，控件就一个像素都不会出现，而且完全不报错。
    /// </summary>
    public string ProbePanelWiring
    {
        get
        {
            if (win == null || panel == null || back == null)
            {
                return "panelWiring: win=" + (win != null) + " panel=" + (panel != null)
                    + " under=" + (back != null);
            }
            UIPanel bp = back.panel;
            Camera cam = panel.anchorCamera;
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            sb.Append("panelWiring ");
            sb.Append("winLayer=").Append(win.layer);
            sb.Append(" panelLayer=").Append(panel.gameObject.layer);
            sb.Append(" underLayer=").Append(back.gameObject.layer);
            sb.Append(" samePanel=").Append(bp == panel);
            sb.Append(" bp=").Append(bp != null ? bp.name : "null");
            sb.Append(" bpLayer=").Append(bp != null ? bp.gameObject.layer.ToString() : "-");
            sb.Append(" panelActive=").Append(panel.gameObject.activeInHierarchy);
            sb.Append(" panelEnabled=").Append(panel.enabled);
            sb.Append(" widgets=").Append(panel.widgets == null ? -1 : panel.widgets.Count);
            sb.Append(" cam=").Append(cam != null ? cam.name : "null");
            if (cam != null)
            {
                sb.Append(" camDepth=").Append(cam.depth);
                sb.Append(" maskHasLayer11=").Append((cam.cullingMask & (1 << 11)) != 0);
                sb.Append(" camOrtho=").Append(cam.orthographic);
                sb.Append(" camPos=").Append(cam.transform.position.ToString("F1"));
            }
            sb.Append(" winScale=").Append(win.transform.localScale.ToString("F2"));
            sb.Append(" winPos=").Append(win.transform.position.ToString("F1"));
            sb.Append(" underPos=").Append(back.transform.position.ToString("F1"));
            sb.Append(" underScale=").Append(back.transform.lossyScale.ToString("F2"));
            sb.Append(" underAlpha=").Append(back.color.a);
            sb.Append(" underDepth=").Append(back.depth);
            Transform t = panel.transform;
            int up = 0;
            while (t != null && up < 6)
            {
                sb.Append(" | p").Append(up).Append("=").Append(t.name);
                sb.Append("(ls").Append(t.localScale.ToString("F2")).Append(")");
                t = t.parent;
                up++;
            }
            sb.Append(" underLS=").Append(back.transform.localScale.ToString("F2"));
            sb.Append(" panelLS=").Append(panel.transform.localScale.ToString("F2"));
            sb.Append(" inputLS=").Append(
                UIHelper.getByName<Transform>(win, "input_") != null
                    ? UIHelper.getByName<Transform>(win, "input_").localScale.ToString("F2") : "-");
            sb.Append(" uiMainLS=").Append(Program.ui_main_2d != null
                ? Program.ui_main_2d.transform.localScale.ToString("F3") : "null");
            sb.Append(" uiBackLS=").Append(Program.ui_back_ground_2d != null
                ? Program.ui_back_ground_2d.transform.localScale.ToString("F3") : "null");
            return sb.ToString();
        }
    }

    /// <summary>
    /// 把本窗这条链上缩放为 0 的节点压回 1。
    ///
    /// ⚠ 别把这个当病因：实测 ui_main_2d / ui_back_ground_2d 的 localScale 是 (0.002,0.002,0.002)
    /// （整个工程都这样，不只本窗），而说明面板、菜单这些挂在 ui_back_ground_2d 下的 UI
    /// **照常显示** —— 因为 NGUI 的 widget 用 transform 的 world **position** 加自身宽高算顶点，
    /// 根本不乘 transform 的 scale。所以 lossyScale=0.002 是红鲱鱼，不是「窗被缩没了」。
    /// 这里只是留一道保险：真出现 0 就压回 1，免得哪天 prefab 变了连排查方向都没有。
    /// </summary>
    void fixScales()
    {
        if (win == null)
        {
            return;
        }
        try
        {
            // 往上整条链都压一遍：实测被缩成 0 的不是本窗自己，而是它的祖先
            // ui_main_2d（探针：p3=mod_2d_ui(Clone) ls(0,0,0)，而 p0/p1/p2 全是 1）。
            // 只修自己没用 —— lossyScale 是整条链乘出来的。
            Transform t = win.transform;
            int guard = 0;
            while (t != null && guard < 12)
            {
                if (t.localScale == Vector3.zero)
                {
                    t.localScale = Vector3.one;
                }
                t = t.parent;
                guard++;
            }
        }
        catch (Exception e)
        {
            Program.DEBUGLOG(e);
        }
    }

    public override void initialize()
    {
        win = create
            (
            Program.I().new_ui_search,
            HiddenPosition(),
            Vector3.zero,
            false,
            Program.ui_main_2d
            );
        UIHelper.InterGameObject(win);
        input = UIHelper.getByName<UIInput>(win, "input_");
        panel = UIHelper.getByName<UIPanel>(win, "panel_");
        bar = UIHelper.getByName<UIScrollBar>(win, "bar_");
        back = UIHelper.getByName<UITexture>(win, "under_");
        if (back != null)
        {
            int w = back.width;
            int maxW = Mathf.RoundToInt(Screen.width * 0.42f);
            if (w > maxW)
            {
                back.width = maxW;
            }
        }
        if (input != null)
        {
            input.value = "";
        }
        if (panel != null && bar != null)
        {
            scroll = new VirtualScrollView(panel, bar, itemOnListProducer, 86, ItemBinder);
        }
        // ⚠ 顺序要紧：返回钮是**克隆 search_ 造的**，所以必须先建钮、再把放大镜藏掉；
        //   反过来就克隆出一个藏在状态下的钮（克隆体默认继承源的 activeSelf）。
        //   藏完之后顺手把返回钮挪到放大镜原来那个位置（见 PlaceBackButton）。
        installBackButton();
        // 「译」钮克隆的是 back_，所以必须在 back_ 建好之后、且**趁 back_ 还醒着**时建。
        // （若放到 StripToDetailMode 之后，返回钮仍活着，但那时它已被锚到 under_；
        //   提前建能保证两者第一次落位用的是同一份参照物。）
        installPackButton();
        StripToDetailMode();
        ensureBackButton();
        ensurePackButton();
        fixScales();
        UIHelper.registEvent(win, "search_", runFromInput);
        UIHelper.registEvent(win, "under_", hide);
        UIHelper.trySetLableText(win, "title_", "");
    }

    /// <summary>
    /// 屏外停泊位：**屏幕正上方**（照「分类」弹窗的同款轨道 —— 它停在自己那条轨道的
    /// <c>Screen.height * 1.5f</c>，开的时候从上往下 0.6s 滑进 <c>Screen.height * 0.5f</c>）。
    ///
    /// ⛔ 2026-10-02 第三轮以前是"停在屏幕右边外面 800px、横着滑进来"；用户要的是
    ///   「像分类的界面一样从上往下进入」，所以改成纵向轨道。x 直接用目标落点，
    ///   免得开合时还要先横移一次。z 用 0（与落点同深度）——靠"在屏上方"藏，不靠深度藏。
    /// </summary>
    Vector3 HiddenPosition()
    {
        return Program.camera_main_2d.ScreenToWorldPoint(new Vector3(TargetCenterX(), Screen.height * 1.5f, 0));
    }

    /// <summary>屏上落点：竖直居中、水平按 <see cref="TargetCenterX"/> 的并排口径。</summary>
    Vector3 ShownPosition()
    {
        return Program.camera_main_2d.ScreenToWorldPoint(new Vector3(TargetCenterX(), Screen.height * 0.5f, 0));
    }

    /// <summary>
    /// 下一次 <c>applyShowArrangement</c> 要不要**先瞬移回屏幕上方**再滑下来。
    /// 只有"上一次是收起来的"才重放进场动画；窗已经在屏上、只是并排重排时不许把它拽回顶上。
    /// </summary>
    bool enterFromTopPending = true;

    /// <summary>滑回屏幕上方（与进场同一条轨道，反向）。</summary>
    public override void applyHideArrangement()
    {
        UpdateRightSideOccupancy();
        UpdateBarShift();
        if (win != null)
        {
            iTween.Stop(win);        // 上一次滑到一半又点了新链接时，别让两个补间同时拉同一个物体
            iTween.MoveTo(win, HiddenPosition(), 0.6f);
        }
        enterFromTopPending = true;
    }

    public override void applyShowArrangement()
    {
        popupOpenLast = DetailPopupOpen();
        if (win != null)
        {
            fixScales();
            ensureBackButton();
            ensurePackButton();
            UpdateBackIcon();
            if (back != null)
            {
                back.height = Screen.height;
            }
            iTween.Stop(win);
            bool fromTop = enterFromTopPending;
            if (fromTop)
            {
                enterFromTopPending = false;
                win.transform.position = HiddenPosition();
            }
            float y0 = Screen.height * (fromTop ? 1.5f : 0.5f);
            iTween.MoveTo(win, ShownPosition(), 0.6f);
            // ⛔ y0 不只写"算出来的目标"，还**实测**一发：瞬移后立刻把 transform 读回来。
            //   万一哪天窗被锚点拽住、瞬移不生效，这条会当场暴露（算的 1.5H ≠ 实测的别值）。
            float yMeas = Program.camera_main_2d.WorldToScreenPoint(win.transform.position).y;
            QuickTestTrace.Log("link", "show fromTop=" + (fromTop ? "yes" : "no")
                + " cx=" + Mathf.RoundToInt(TargetCenterX())
                + " w=" + (back != null ? back.width : 0) + " dur=0.6"
                + " y0=" + Mathf.RoundToInt(y0) + " yMeas=" + Mathf.RoundToInt(yMeas)
                + " (屏高 " + Screen.height + " ⇒ 1.5H=" + Mathf.RoundToInt(Screen.height * 1.5f) + ")");
            // 补一条落定读数：上面那条是开场那一帧打的（窗还在屏上方），
            // 这条等 0.6s 补间跑完再量，证明它**真的从上面滑到了屏中**。
            // 补间途中的两次抽样（120ms / 300ms）：证明窗是**一路滑下来**的，不是瞬移到位。
            // ⛔ iTween.MoveTo 默认 easeOutExponential ⇒ 120ms 约走完 2/3、300ms 约 97%，
            //   所以两条都必须**严格落在** 1.5H 与 0.5H 之间（既不=起点也不=终点）。
            //   光有"起点读数 + 终点读数"抓不到「根本没补间、直接跳到位」这一种。
            if (QuickTestTrace.Enabled)
            {
                int[] samples = new int[] { 120, 300 };
                for (int s = 0; s < samples.Length; s++)
                {
                    int ms = samples[s];
                    Program.go(ms, () =>
                    {
                        if (win == null)
                        {
                            return;
                        }
                        QuickTestTrace.Log("link", "show mid" + ms + " y="
                            + Mathf.RoundToInt(Program.camera_main_2d.WorldToScreenPoint(win.transform.position).y)
                            + " (起点 " + Mathf.RoundToInt(Screen.height * 1.5f)
                            + " 终点 " + Mathf.RoundToInt(Screen.height * 0.5f) + ")");
                    });
                }
            }
            Program.go(900, () =>
            {
                if (win == null)
                {
                    return;
                }
                QuickTestTrace.Log("link", "show settled y="
                    + Mathf.RoundToInt(Program.camera_main_2d.WorldToScreenPoint(win.transform.position).y)
                    + " (期望 " + Mathf.RoundToInt(Screen.height * 0.5f) + ")");
            });
        }
        UpdateRightSideOccupancy();
        UpdateBarShift();
    }

    /// <summary>
    /// 把**当前在场那位**的底部按钮条推开本窗这一块宽度（用户 2026-10-02 第三轮口径：
    /// 「像分类界面一样……能推开底下的按钮」——分类弹窗就是这么把底栏从 230 推到 460 的）。
    ///
    /// 本窗满屏高、贴右缘展开，不推开的话底栏右半截会被压在窗底下点不到。
    /// 底栏的子按钮是**向左**排的（见 new_toolBar_editDeck.prefab：local x 全是负数），
    /// 所以"锚点左移一个窗宽"正好把整条挪到本窗左缘外面。
    /// 幂等：show / hide / 重排三条路都调，重复调用结果一致。
    /// </summary>
    Servant barShiftOwner;

    void UpdateBarShift()
    {
        try
        {
            Servant owner = Servant.BarOwner();
            float w = isShowed ? (back != null ? back.width : Screen.width * 0.4f) : 0f;
            // 换场了（编辑器 → 战斗 / 回房间，或反过来）：先把上一位的让位量清零，
            // 否则它会一直僵在"挪开"的状态，而新场上那位根本没被推过。
            if (barShiftOwner != null && !ReferenceEquals(barShiftOwner, owner))
            {
                barShiftOwner.SetBarShift(0f);
                barShiftOwner = null;
            }
            if (owner == null)
            {
                return;
            }
            owner.SetBarShift(w);
            barShiftOwner = w > 0f ? owner : null;
            if (QuickTestTrace.Enabled)
            {
                QuickTestTrace.Log("link", "barshift owner=" + owner.GetType().Name
                    + " shift=" + Mathf.RoundToInt(w) + " total=" + Mathf.RoundToInt(owner.GetBarShift()));
                // 补一条**落定后**的读数：上面那条是刚下令那一刻打的，此时底栏还在 0.6s 补间里，
                // 量不到"到底挪没挪到本窗左边"。这条延后到补间跑完再读。
                Program.go(900, () => { QuickTestTrace.Log("link", ProbeBarInfo); });
            }
        }
        catch (Exception e)
        {
            Program.DEBUGLOG(e);
        }
    }

    /// <summary>
    /// 验收用：底栏**落定后**的屏幕读数 —— 底栏锚点 x、本窗左缘 x、叠加让位量。
    /// 判据是「底栏锚点 ≤ 本窗左缘（+1px 容差）」＝ 底栏整条不在本窗底下。
    /// </summary>
    public string ProbeBarInfo
    {
        get
        {
            try
            {
                Servant owner = Servant.BarOwner();
                if (owner == null || owner.toolBar == null)
                {
                    return "barinfo none";
                }
                float bw = back != null ? back.width : Screen.width * 0.4f;
                float bx = Program.camera_back_ground_2d.WorldToScreenPoint(
                    owner.toolBar.transform.position).x;
                float left = TargetCenterX() - bw / 2f;
                return "barinfo owner=" + owner.GetType().Name
                    + " barX=" + Mathf.RoundToInt(bx)
                    + " winLeft=" + Mathf.RoundToInt(left)
                    + " extra=" + Mathf.RoundToInt(owner.GetBarShift())
                    + (isShowed ? "" : " (win hidden)");
            }
            catch (Exception e)
            {
                return "barinfo err " + e.Message;
            }
        }
    }

    /// <summary>
    /// 战斗里右缘被本窗占住时，把 <see cref="gameInfo"/> 的右缘一列**顶开**（用户 2026-10-02
    /// 第二轮口径：「将右侧 ui 做成被顶出来的样子弹到搜索框左边」）。
    ///
    /// 编辑器场景没有 gameInfo（它只在战斗里由 Ocgcore 建），且编辑器场景本窗本来就让到
    /// 面板左边、不占右缘 —— 所以这里只在「本窗开着 ∧ 编辑器面板不在」时上报占宽
    /// （= 本窗宽），gameInfo 自己缓动滑过去（见 gameInfo.rightShiftTarget）。
    /// 幂等：show / hide / 重排三条路都调，重复调用结果一致。
    /// </summary>
    void UpdateRightSideOccupancy()
    {
        try
        {
            Ocgcore oc = Program.I() != null ? Program.I().ocgcore : null;
            gameInfo gi = oc != null ? oc.gameInfo : null;
            if (gi == null || gi.gameObject == null || !gi.gameObject.activeInHierarchy)
            {
                return;
            }
            float w = back != null ? back.width : Screen.width * 0.4f;
            bool occupy = isShowed && DeckSearchPanelWidth() <= 0f;
            // ⛔ 走访问器不能直写字段：gameInfo 已序列化进包，运行期加 public 字段会
            //   撑破旧数据布局（见 gameInfo.SetRightShift 注释，2026-10-02 三连崩的根因）。
            gi.SetRightShift(occupy ? w : 0f);
            if (QuickTestTrace.Enabled)
            {
                QuickTestTrace.Log("link", "rightoccupy target=" + Mathf.RoundToInt(gi.GetRightShiftTarget())
                    + " winW=" + Mathf.RoundToInt(w));
            }
        }
        catch (Exception e)
        {
            Program.DEBUGLOG(e);
        }
    }

    /// <summary>
    /// 本窗显示时的**屏幕中心 x**（用户 2026-10-02 第二轮口径：照「分类」弹窗的同款排布）。
    ///
    /// 编辑器那张检索面板永远占屏幕最右；「分类」弹窗开着时贴着它的**左边**（无缝，0.6s 滑到位）。
    /// 本窗照抄这个口径：贴齐不留缝 ——
    ///   * 面板开着 → 本窗右缘贴面板左缘；
    ///   * 分类弹窗也开着 → 分类弹窗**穿插**进来（贴面板左缘），本窗再被挤开到弹窗左边；
    ///   * 都没开（战斗里）→ 本窗贴右缘原位。
    /// 面板宽度**从那张面板自己身上读**（<c>under_</c> 的 UITexture 宽），不写死 230 ——
    /// 玩家能拖面板宽，prefab 值也不保证。弹窗宽度同理（见 <see cref="DetailPopupWidth"/>）。
    /// </summary>
    float TargetCenterX()
    {
        float w = back != null ? back.width : Screen.width * 0.4f;
        float right = Screen.width;
        float editorW = DeckSearchPanelWidth();
        if (editorW > 0f)
        {
            right -= editorW;
        }
        float popupW = DetailPopupWidth();
        if (popupW > 0f)
        {
            right -= popupW;
        }
        return right - w / 2f;
    }

    /// <summary>
    /// 「分类」弹窗（gameObjectDetailedSearch）**实际可见**时的屏幕宽度；没开（或编辑器没开）返回 0。
    ///
    /// ⛔ 只看 <c>DetailPopupOpen</c> 不够：编辑器整块隐藏时 <c>detailShowed</c> 会残留 true，
    /// 而弹窗此刻 localScale=zero 根本不在屏上 —— 所以必须连 <c>dm.isShowed</c> 一起判。
    /// 屏幕宽度 = 根 UITexture 宽 × localScale（Screen.height &lt; 700 时弹窗整体按 H/700 缩）。
    /// </summary>
    static float DetailPopupWidth()
    {
        try
        {
            DeckManager dm = Program.I() != null ? Program.I().deckManager : null;
            if (dm == null || !dm.isShowed || !dm.DetailPopupOpen || dm.gameObjectDetailedSearch == null)
            {
                return 0f;
            }
            UITexture tex = dm.gameObjectDetailedSearch.GetComponent<UITexture>();
            if (tex == null || tex.width <= 0)
            {
                return 0f;
            }
            float scale = dm.gameObjectDetailedSearch.transform.localScale.x;
            return tex.width * (scale > 0f ? scale : 1f);
        }
        catch (Exception)
        {
            return 0f;
        }
    }

    /// <summary>卡组编辑器那张检索面板正开着时的宽度；没开（或读不到）返回 0。</summary>
    static float DeckSearchPanelWidth()
    {
        try
        {
            DeckManager dm = Program.I() != null ? Program.I().deckManager : null;
            if (dm == null || !dm.isShowed || dm.gameObjectSearch == null)
            {
                return 0f;
            }
            UITexture tex = UIHelper.getByName<UITexture>(dm.gameObjectSearch, "under_");
            if (tex != null && tex.width > 0)
            {
                return tex.width;
            }
            return Screen.width * 0.4f;
        }
        catch (Exception)
        {
            return 0f;
        }
    }

    /// <summary>
    /// 本窗此刻占住屏幕右缘的宽度（没开 = 0）—— 给卡组编辑器顶开板子用。
    ///
    /// 谁在用：<c>DeckManager.camrem()</c>。用户 2026-10-03 的口径是「跳转的搜索栏要
    /// 像分类界面一样顶开卡组板子」，而「分类」弹窗顶开靠的就是 camrem 里那一句
    /// <c>r -= 230 * scale</c> ⇒ 本窗照抄同一套：把窗宽从右缘里减掉，相机视口自然
    /// 把板子挤到窗左边（不是把窗压在牌上）。
    /// ⛔ 宽度由**本窗自己报**（back.width），那边不许写死；本窗没开时必须报 0。
    /// </summary>
    public static float BoardPushWidth()
    {
        try
        {
            CardSearchWindow w = Program.I() != null ? Program.I().cardSearch : null;
            if (w == null || !w.isShowed)
            {
                return 0f;
            }
            return w.back != null ? (float)w.back.width : Screen.width * 0.4f;
        }
        catch (Exception)
        {
            return 0f;
        }
    }

    /// <summary>
    /// 卡组编辑器那张检索面板的显隐变了（<c>DeckManager.applyShow/HideArrangement</c> 调）：
    /// 本窗正开着就按新的并排位重排一次（走补间，与开窗同一条路）。
    /// </summary>
    public void onDeckSearchPanelToggled()
    {
        UpdateRightSideOccupancy();
        UpdateBarShift();
        if (!isShowed || win == null)
        {
            return;
        }
        try
        {
            iTween.Stop(win);
            iTween.MoveTo(win, ShownPosition(), 0.6f);
            QuickTestTrace.Log("link", "relayout cx=" + Mathf.RoundToInt(TargetCenterX())
                + " editorW=" + Mathf.RoundToInt(DeckSearchPanelWidth()));
        }
        catch (Exception e)
        {
            Program.DEBUGLOG(e);
        }
    }

    bool DetailPopupOpen()
    {
        var dm = Program.I() != null ? Program.I().deckManager : null;
        return dm != null && dm.DetailPopupOpen;
    }

    /// <summary>宿主（卡组 / 对局里那张简介 UI）上一帧在不在（状态沿检测，见 <see cref="preFrameFunction"/>）。</summary>
    bool hostDescWasShown = true;

    /// <summary>
    /// <see cref="hostDescWasShown"/> 有没有被**真实观测**过一次。
    /// 首帧必须拿实测值对齐，不能拿字段默认值去判 —— 否则「开窗那一刻简介恰好不在场」
    /// 会被当成"简介刚消失"，窗刚开就被自己关掉。
    /// </summary>
    bool hostDescSeen = false;

    public override void preFrameFunction()
    {
        // ── 宿主没了 ⇒ 本窗跟着收掉（用户 2026-10-03：卡组/简介的 UI 隐藏或消失时，
        //    跳转出来的搜索栏也要自动消失）───────────────────────────────────
        // 判据用**状态沿**而不是"此刻不在场就关"：本窗是从简介里点链接跳出来的，
        // 简介在场才是它存在的理由；但它也可能被别的路径（探针、自检）在简介不在场时叫起来，
        // 那种情形不该被立刻关掉。
        // ⛔ 读不到宿主（Program/单例还没起来）时一律当"在场" —— 宁可多留一帧也别误关。
        Servant host = Program.I() != null ? Program.I().cardDescription : null;
        bool hostUp = host == null || host.isShowed;
        if (!hostDescSeen)
        {
            hostDescSeen = true;
            hostDescWasShown = hostUp;
        }
        else if (hostDescWasShown && !hostUp)
        {
            QuickTestTrace.Log("link", "autohide 宿主简介 UI 已隐藏 → 本窗跟着收起");
            hostDescWasShown = hostUp;
            hide();
            return;
        }
        hostDescWasShown = hostUp;

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            hide();
        }
        // 「分类」弹窗开合 → 本窗**平移**让位/回归（用户 2026-10-02 第二轮口径）：
        // 不再像旧版那样把本窗整个藏掉 —— 弹窗穿插进来贴面板左缘，本窗被挤到弹窗左边；
        // 弹窗一关本窗滑回贴面板的位置。只盯状态沿（每帧比较，开销为零）。
        bool popupOpen = DetailPopupOpen();
        if (popupOpen != popupOpenLast && isShowed && win != null)
        {
            popupOpenLast = popupOpen;
            try
            {
                iTween.Stop(win);
                iTween.MoveTo(win, ShownPosition(), 0.6f);
                QuickTestTrace.Log("link", "detailpopup relayout cx=" + Mathf.RoundToInt(TargetCenterX())
                    + " popupW=" + Mathf.RoundToInt(DetailPopupWidth()));
            }
            catch (Exception e)
            {
                Program.DEBUGLOG(e);
            }
        }
    }

    /// <summary>「分类」弹窗上一帧开没开（状态沿检测；初始值在首帧对齐）。</summary>
    bool popupOpenLast;

    public override void ES_mouseDownRight()
    {
        // 用户 2026-10-04：右键**不再**收起检索窗 —— 右键现在有正经用途
        // （点简介里亮起的字段 = 改这个系列的译名，见 CardDescription.ES_mouseDownRight），
        // 而"顺手把窗收掉"会让玩家刚搜出来的一批卡消失。
        // 关窗仍有三条路：返回钮退到空、同一处链接再点一次、以及各入口自己 hide()。
    }

    void runFromInput()
    {
        string q = input != null ? input.value : "";
        Show(q, 0);
    }

    /// <summary>
    /// 「同一处链接再点一次 = 关掉本窗」（用户 2026-10-03）。
    ///
    /// ⛔ 判据是**同一处**，不是"同一种"：钥匙里带"第几个链接"的顺序号（见
    ///   <see cref="CardTextLinker.FindAt(string,int,out int)"/>），所以
    ///   * 同一张卡上**两个不同位置**的链接（哪怕 payload 一样、搜出来的结果一样）⇒ 算两处，点了**不关**；
    ///   * 两张不同卡上顺序号相同的链接 ⇒ 卡号不同，也算两处，点了**不关**。
    /// ⛔ 只在窗**正开着**时才认"再点一次"；窗关着时点任何链接都照常打开。
    /// ⛔ 顺序号拿不到（-1）时**一律不关** —— 判不出是哪一处，宁可多开一次也别误关。
    /// </summary>
    public static bool ToggleSameLink(string key)
    {
        CardSearchWindow w = Program.I() != null ? Program.I().cardSearch : null;
        if (w == null || string.IsNullOrEmpty(key))
        {
            return false;
        }
        if (w.isShowed && lastLinkKey.Length > 0 && lastLinkKey == key)
        {
            lastLinkKey = "";
            w.hide();
            QuickTestTrace.Log("link", "sameLink close key=" + key);
            return true;
        }
        lastLinkKey = key;
        QuickTestTrace.Log("link", "sameLink open key=" + key);
        return false;
    }

    /// <summary>上一次点开本窗的那处链接的钥匙（见 <see cref="ToggleSameLink"/>）。</summary>
    static string lastLinkKey = "";

    /// <summary>按「关键字 + 主类掩码」检索并展示。掩码 0 = 不限主类。</summary>
    public static void ShowQuery(string query, int typeMask)
    {
        CardSearchWindow w = Program.I().cardSearch;
        if (w == null)
        {
            return;
        }
        w.Show(query, typeMask);
    }

    /// <summary>
    /// 点的是**卡名**（用户 2026-10-03）：只出**名字一模一样**的那些卡 ——
    /// 同名异画的两张会一起列全，而名字里只是「包含」它的卡一张都不出。
    /// </summary>
    public static void ShowName(string name, int typeMask)
    {
        ShowName(name, typeMask, "");
    }

    /// <summary>
    /// 同上，多一个 <paramref name="title"/>：标题照抄玩家点的那句话
    /// （用户 2026-10-04 第三条）。空 = 按档位自己拼。
    /// </summary>
    public static void ShowName(string name, int typeMask, string title)
    {
        CardSearchWindow w = Program.I().cardSearch;
        if (w == null)
        {
            return;
        }
        w.ShowInternal(name, typeMask, true, "", Filter.ExactName, title);
    }

    /// <summary>
    /// 点的是「<c>X</c>」<b>的卡名记述</b>那半句（用户 2026-10-03）：只出**卡文里
    /// 记述了 X** 的那些卡。「本体没记述则本体都出不了」是这条判据的自然结果。
    /// </summary>
    public static void ShowRecord(string name, int typeMask)
    {
        CardSearchWindow w = Program.I().cardSearch;
        if (w == null)
        {
            return;
        }
        w.ShowInternal(name, typeMask, true, "", Filter.Record);
    }

    /// <summary>
    /// 点的是「<c>X</c>」<b>或者有那个卡名记述的</b>「魔法·陷阱卡」整句（用户 2026-10-03）：
    /// <b>并集</b> —— 同名的卡 ∪ 卡文里记述了 X 的<paramref name="typeMask"/>那种卡。
    /// </summary>
    public static void ShowUnion(string name, int typeMask)
    {
        CardSearchWindow w = Program.I().cardSearch;
        if (w == null)
        {
            return;
        }
        w.ShowInternal(name, typeMask, true, "", Filter.NameOrRecord);
    }

    /// <summary>
    /// RD 简介里点了一段「怪兽（银河族）」之类：按**限定条件**检索并展示（没有关键字）。
    /// <paramref name="mainMask"/> 是主类（怪兽 1 / 魔法 2 / 陷阱 4；0 = 不限）。
    /// </summary>
    public static void ShowRd(string qualifier, int mainMask)
    {
        CardSearchWindow w = Program.I().cardSearch;
        if (w == null)
        {
            return;
        }
        w.ShowInternal("", mainMask, true, qualifier ?? "");
    }

    /// <summary>
    /// 系列/字段链接（用户 2026-10-04）：按 **!setname 表内码** 列出「**真正属于**这个系列」的卡。
    /// <paramref name="displayName"/> 只进标题；<paramref name="code"/> 才是判据
    /// （检索端走 <c>CardsManager.IfSetCard</c>：低 12 位=系列本体、高 4 位=子系列）；
    /// <paramref name="typeMask"/> 是尾词主类（0 = 不限）。
    ///
    /// <para>简介系列行的每个系列名、以及正文里「字段」+ 种类词那几处，都落在这里 ——
    /// ⛔ 别再退回全文检索：那会把"卡文里只是提到它"的卡一起倒出来（用户报障原话
    /// 「融合魔法卡会搜出来简介带融合」）。</para>
    /// </summary>
    public static void ShowSetcode(string title, int code, int typeMask)
    {
        CardSearchWindow w = Program.I().cardSearch;
        if (w == null)
        {
            return;
        }
        w.ShowInternal(title ?? "", typeMask, true, "", Filter.Setcode, title ?? "");
        // ShowInternal 已把 hash 作废成 -1；这里补上真判据。
        // ⛔ 必须在 ShowInternal **之后**写：Search 是 60 拍后跑的，读的是当时的值。
        w.lastSetHash = code;
    }

    public void Show(string query, int typeMask)
    {
        ShowInternal(query, typeMask, true, "");
    }

    void ShowInternal(string query, int typeMask, bool remember)
    {
        ShowInternal(query, typeMask, remember, "", Filter.Text, "");
    }

    void ShowInternal(string query, int typeMask, bool remember, string rd)
    {
        ShowInternal(query, typeMask, remember, rd, Filter.Text, "");
    }

    void ShowInternal(string query, int typeMask, bool remember, string rd, Filter filter)
    {
        ShowInternal(query, typeMask, remember, rd, filter, "");
    }

    /// <summary>
    /// <paramref name="title"/> = 检索窗标题要照抄的那句话（用户 2026-10-04 第三条）；
    /// 空 = 按档位自己拼（<see cref="Search"/> 里的老写法）。
    /// </summary>
    void ShowInternal(string query, int typeMask, bool remember, string rd, Filter filter,
        string title)
    {
        string q = query ?? "";
        string r = rd ?? "";
        if (!isShowed)
        {
            // 新开一次弹窗 = 新的一叠「上一级」，不留上一次关窗前的旧账。
            history.Clear();
        }
        else if (remember && (q != lastQuery || typeMask != lastMask || r != lastRd
                              || filter != lastFilter))
        {
            // 又点了一处链接 = 往下走了一级，把「现在这一级」记下来给返回钮用。
            Entry e = new Entry();
            e.query = lastQuery;
            e.mask = lastMask;
            e.rd = lastRd;
            e.filter = lastFilter;
            e.setHash = lastSetHash;
            e.title = lastTitle;
            history.Add(e);
        }
        lastQuery = q;
        lastMask = typeMask;
        lastRd = r;
        lastFilter = filter;
        lastTitle = title ?? "";
        // ⛔ 非 Setcode 档要把 hash 作废（-1）：不然上一档留下的 hash 会被
        //   <see cref="Search"/> 误当成真判据（判据必须跟着档位走）。
        lastSetHash = -1;
        UpdateBackIcon();
        show();
        // 详情版没有输入框了（<see cref="StripToDetailMode"/>），条件只在标题上显示；
        // 万一 prefab 不对、输入框还在，才把检索词填进去。
        if (input != null && input.gameObject.activeSelf)
        {
            input.value = lastQuery;
            Program.go(60, () => { if (input != null && input.gameObject.activeSelf) input.isSelected = true; });
        }
        Program.go(60, () => { Search(); });
    }

    /// <summary>
    /// 「返回上一级」：回到上一次的检索条件；已经退到头了就把窗收掉（等于回到没弹窗那一层）。
    /// </summary>
    public void goBack()
    {
        if (history.Count > 0)
        {
            Entry e = history[history.Count - 1];
            history.RemoveAt(history.Count - 1);
            ShowInternal(e.query, e.mask, false, e.rd, e.filter, e.title);
            // Setcode 档的判据在 hash 里（见 <see cref="ShowSetcode"/>）；
            // ShowInternal 已把它作废成 -1，这里按落点补回去（60 拍后的 Search 读得到）。
            lastSetHash = e.filter == Filter.Setcode ? e.setHash : -1;
            UpdateBackIcon();
            QuickTestTrace.Log("link", "back q=[" + e.query + "] mask=" + e.mask
                + " rd=[" + (e.rd ?? "") + "] filter=" + e.filter
                + " hash=" + lastSetHash
                + " left=" + history.Count);
        }
        else
        {
            hide();
            QuickTestTrace.Log("link", "back close (history empty)");
        }
    }

    /// <summary>验收用：把本窗根节点交出去，好让探针在进程内直接量那颗「返回上一级」的状态。</summary>
    public GameObject ProbeWindowObject { get { return win; } }

    /// <summary>
    /// 本窗根节点（<c>new_search_remaster</c> 的克隆体）＝检索列表的容器。
    ///
    /// <para>消费方是 <see cref="CardDescLock"/>：「空格锁简介」要判断**这块检索列表还开着吗**
    /// —— 锁在检索列表里的卡，滚出裁剪区/被重新检索挤掉都**不算解锁**，只有整块列表下来了
    /// 才解锁（见 <c>CardDescLock.lockedSearchRoot</c> 头注）。收窗是把窗挪到屏幕外，
    /// <c>activeInHierarchy</c> 恒为真，所以它比的是 <see cref="Servant.isShowed"/>。</para>
    /// </summary>
    public GameObject WindowRoot { get { return win; } }

    /// <summary>
    /// 验收用：在返回钮旁边生成一个**变体**（<paramref name="dx"/> = 向左偏移 px，
    /// <paramref name="col"/>/<paramref name="texPath"/> 空则沿用本体）——
    /// 给「X 渲染出来比放大镜暗一半」那类问题做**同屏受控对照**（2026-10-02 第二轮）。
    /// 变体随窗销毁，不额外清理。
    /// </summary>
    public static GameObject ProbeBackVariant(GameObject win, float dx, Color? col, string texPath)
    {
        GameObject b = FindDeep(win, BackButtonName);
        if (b == null)
        {
            return null;
        }
        GameObject v = (GameObject)UnityEngine.Object.Instantiate(b);
        v.name = "backVariant" + Mathf.RoundToInt(dx);
        v.transform.SetParent(b.transform.parent, false);
        UIRect r = v.GetComponent<UIRect>();
        if (r != null)
        {
            r.topAnchor.target = null;
            r.bottomAnchor.target = null;
            r.leftAnchor.target = null;
            r.rightAnchor.target = null;
        }
        v.transform.localPosition = b.transform.localPosition + new Vector3(-dx, 0f, 0f);
        UITexture icon = v.GetComponentInChildren<UITexture>(true);
        if (icon != null)
        {
            ClearAnchors(icon.gameObject);
            if (col.HasValue)
            {
                icon.color = col.Value;
            }
            if (!string.IsNullOrEmpty(texPath))
            {
                icon.path = texPath;
                icon.mainTexture = GameTextureManager.get(texPath);
            }
        }
        return v;
    }

    /// <summary>
    /// 验收用：本窗三颗钮（返回/自带的失活放大镜/失活分类）的图标逐块报色 ——
    /// 给「返回钮颜色跟放大镜不一样」那类问题做**数值**对照（用户 2026-10-02 第二轮）。
    /// </summary>
    public string ProbeIconColors()
    {
        return IconClrIn(win, BackButtonName) + " | " + IconClrIn(win, "search_")
            + " | " + IconClrIn(win, "detailed_");
    }

    /// <summary>
    /// 某颗钮子树里所有 UITexture 的 path/颜色/激活态，外加 UIButton 的 tweenTarget —— 一行字符串。
    /// </summary>
    public static string IconClrIn(GameObject root, string buttonName)
    {
        GameObject g = FindDeep(root, buttonName);
        if (g == null)
        {
            return buttonName + "=missing";
        }
        string s = buttonName;
        UITexture[] all = g.GetComponentsInChildren<UITexture>(true);
        for (int i = 0; i < all.Length; i++)
        {
            UITexture t = all[i];
            if (t == null)
            {
                continue;
            }
            Color c = t.color;
            s += " [" + (string.IsNullOrEmpty(t.path) ? "?" : t.path)
                + " col=" + c.r.ToString("F2") + "," + c.g.ToString("F2") + ","
                + c.b.ToString("F2") + "," + c.a.ToString("F2")
                + " fa=" + t.finalAlpha.ToString("F2")
                + " px=" + t.width + "x" + t.height
                + " act=" + (t.gameObject.activeInHierarchy ? 1 : 0) + "]";
        }
        UIButton ub = g.GetComponent<UIButton>();
        if (ub != null)
        {
            s += " ubTarget=" + (ub.tweenTarget != null ? ub.tweenTarget.name : "null");
        }
        return s;
    }

    /// <summary>
    /// 按名字找子节点，**含失活对象**。
    /// ⛔ 不能用 <c>UIHelper.getByName</c>：它靠 <c>GetComponentsInChildren</c>（默认只遍历激活对象），
    ///   而详情版恰恰把 <c>input_</c>/<c>search_</c>/<c>detailed_</c> **置为失活** —— 用它查
    ///   那三颗永远是 "missing"，就分不出「没实装（还活着）」和「已按需求藏掉」。
    /// </summary>
    public static GameObject FindDeep(GameObject root, string name)
    {
        if (root == null || string.IsNullOrEmpty(name))
        {
            return null;
        }
        Transform[] all = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] != null && all[i].name == name)
            {
                return all[i].gameObject;
            }
        }
        return null;
    }

    /// <summary>验收用：在进程内直接按一次「返回上一级」（合成的鼠标事件送不进这个客户端）。</summary>
    public void ProbeGoBackOnce()
    {
        goBack();
    }

    /// <summary>
    /// 译名换过之后由 NameTranslationUI 调：窗口正开着就把列表**按同一批卡重印一遍**
    /// （列表里的卡名与描述取自卡对象字段，不重印就不会跟着换）。关了就什么都不做。
    ///
    /// <para>⛔ <b>不能重跑 <see cref="Search"/></b>（这是本方法原来的做法）：重跑会用
    /// <c>lastQuery</c> 重新检索，而 <c>lastQuery</c> 是**换表之前**那个卡名 ——
    /// 译名一换，卡池里的 <c>Card.Name</c> 就换成新表里的名字了，于是
    /// 「卡名精确等于旧名」那一档必然**零命中**，整张列表当场清空。
    /// 玩家看到的是「点完译名，检索结果全没了」。（<see cref="Filter.ExactName"/> /
    /// <see cref="Filter.Record"/> 两档最明显；全文检索那档也会掉一大半。）</para>
    ///
    /// <para>为什么重印是对的：<c>lastResult</c> 里存的是**卡池对象本身**
    /// （<c>searchAdvanced</c> 与几个 Run*Query 都是把池卡直接 Add 进 list，不 clone），
    /// 而 <c>CardsManager.ApplyPool</c> 是**就地**改 <c>Name</c>/<c>Desc</c> 的
    /// ⇒ 重印时读到的就是新名字，且**还是同一批卡**（这正是玩家要的：这一批关联卡
    /// 整体换一份译名，而不是换完发现关联关系没了）。</para>
    /// </summary>
    public void refreshIfShown()
    {
        if (!isShowed || scroll == null)
        {
            return;
        }
        if (lastResult == null || lastResult.Count <= 0)
        {
            Search();
            return;
        }
        List<string[]> args = new List<string[]>();
        for (int i = 0; i < lastResult.Count; i++)
        {
            YGOSharp.Card item = lastResult[i];
            if (item == null)
            {
                continue;
            }
            string[] arg = new string[5];
            arg[0] = item.Id.ToString();
            arg[1] = "3";
            arg[2] = item.Name + "\n" + GameStringHelper.getSearchResult(item);
            args.Add(arg);
        }
        scroll.print(args);
        QuickTestTrace.Log("link", "reprint n=" + args.Count
            + " (同一批卡，译名已换) first=" + (args.Count > 0 ? args[0][0] : "-"));
    }

    /// <summary>
    /// 一次全池扫描（纯文本包含，无正则）。界面与离线验收共用同一口径 —— 验收测的就是玩家点出来的那条路。
    /// 末尾那个 0u 是 getCatagoryFilter，必须保持字面量 0u。
    /// </summary>
    public static List<YGOSharp.Card> RunQuery(string query, int typeMask)
    {
        // ⛔ 主类掩码**不能**交给 searchAdvanced 的 getTypeFilter：它那里是
        //   `(Type & 掩码) == 掩码`，要求一张卡**同时具备掩码里所有位**。
        //   于是「魔法·陷阱」这种组合掩码（2|4=6）进去就是 `(Type & 6) == 6`
        //   —— 纯魔法是 2、纯陷阱是 4，**一张都出不来**（用户 2026-10-02 要的正是
        //   "魔法和陷阱都要搜到"）。所以这里把类型判据留给自己，见 <see cref="TypeMatches"/>。
        List<YGOSharp.Card> raw = YGOSharp.CardsManager.searchAdvanced
            (
            query,
            -233, -233, -233, -233, -233,
            -233, -233, -233, -233, -233,
            -233,
            "",
            -233,
            null,
            0u,
            0u,
            0u,
            0u,
            0u
            );
        return FilterByTypeMask(raw, typeMask);
    }

    /// <summary>主类位：怪兽 0x1 / 魔法 0x2 / 陷阱 0x4。</summary>
    const uint MainTypeBits = 0x7u;

    /// <summary>
    /// 一次检索的类型判据：**主类位取并集，其余位取交集**。
    ///   * 主类并集 ← 「魔法·陷阱」(6) 要同时搜出魔法和陷阱；这是唯一能出结果的语义，
    ///     取交集的话一张卡不可能既是魔法又是陷阱。
    ///   * 子位交集 ← 「效果 + 灵摆」要的是"效果灵摆怪兽"，取并集会把普通效果怪也算进来。
    /// 掩码 0 = 不限。
    /// </summary>
    public static bool TypeMatches(uint cardType, int typeMask)
    {
        if (typeMask == 0)
        {
            return true;
        }
        uint m = (uint)typeMask;
        uint main = m & MainTypeBits;
        if (main != 0 && (cardType & main) == 0)
        {
            return false;
        }
        uint sub = m & ~MainTypeBits;
        if (sub != 0 && (cardType & sub) != sub)
        {
            return false;
        }
        return true;
    }

    static List<YGOSharp.Card> FilterByTypeMask(List<YGOSharp.Card> src, int typeMask)
    {
        if (src == null || typeMask == 0)
        {
            return src;
        }
        List<YGOSharp.Card> kept = new List<YGOSharp.Card>(src.Count);
        for (int i = 0; i < src.Count; i++)
        {
            YGOSharp.Card c = src[i];
            if (c != null && TypeMatches((uint)c.Type, typeMask))
            {
                kept.Add(c);
            }
        }
        return kept;
    }

    // ------------------------------------------------------------ RD 限定检索（需求 4）

    /// <summary>主类掩码 → 给人看的词（检索窗标题用）。</summary>
    static string MainWord(int mainMask)
    {
        int m = mainMask & 0x7;
        if (m == 1)
        {
            return "怪兽";
        }
        if (m == 2)
        {
            return "魔法";
        }
        if (m == 4)
        {
            return "陷阱";
        }
        return "卡";
    }

    /// <summary>
    /// RD 简介里点的那段限定条件 → 真检索（用户 2026-10-02 需求 4）。
    ///
    /// 解析全部交给 <see cref="RdCondition.TryParse"/> —— 简介里**做链接**的那一端
    /// （<c>CardTextLinkerRd</c>）用的是**同一份**解析器，所以不可能出现
    /// 「能点、但点出来的卡不符合括号里写的条件」。
    ///
    /// 筛法：先把整个卡池取回来（<c>searchAdvanced</c> 不带任何过滤 = 全部非衍生物卡），
    /// 再一趟用 <see cref="RdCondition.Matches"/> 过筛。**不把条件交给 searchAdvanced**
    /// 有三条理由：
    ///   ① 它的类型判据是「全都要」（<c>(Type &amp; 掩码) == 掩码</c>），
    ///      `怪兽·魔法·陷阱` 这种主类并集进去恒不命中；
    ///   ② 「以外」是排除语义，它只会做「与掩码有交集」；
    ///   ③ 区间 / 精确值集合（`守备力1900或者2600`）不在它的参数形状里。
    /// 全池一趟线性扫：RD 约 3.5 千张，可忽略（编辑器自己的高级搜索也是全池扫）。
    /// </summary>
    static List<YGOSharp.Card> RunQueryRd(string qualifier, int mainMask)
    {
        RdCondition cond;
        if (!RdCondition.TryParse(qualifier, mainMask, out cond))
        {
            // 条件认不出来：宁可给空列表，也别把限定词当**关键字**搜 ——
            // 那会搜出 0 张、而且看不出是"当关键字搜了"还是"真没卡"。
            // 正常走不到（链接生成端已经用同一个解析器筛过一次）。
            QuickTestTrace.Log("link", "rd unparsed q=[" + (qualifier ?? "") + "] mask=" + mainMask);
            return new List<YGOSharp.Card>();
        }
        List<YGOSharp.Card> pool = YGOSharp.CardsManager.searchAdvanced
            (
            "", -233, -233, -233, -233, -233,
            -233, -233, -233, -233, -233,
            -233, "", -233, null,
            0u, 0u, 0u, 0u, 0u
            );
        List<YGOSharp.Card> kept = new List<YGOSharp.Card>(pool.Count);
        for (int i = 0; i < pool.Count; i++)
        {
            if (cond.Matches(pool[i]))
            {
                kept.Add(pool[i]);
            }
        }
        QuickTestTrace.Log("link", "rd query q=[" + (qualifier ?? "") + "] mask=" + mainMask
            + " " + cond.Describe() + " pool=" + pool.Count + " n=" + kept.Count);
        return kept;
    }

    /// <summary>
    /// 只在 <c>log/qt_debug.on</c> 下跑的 RD 条件检索自检：拿几个**真卡文里出现过的**条件
    /// 各跑一遍 <see cref="RunQueryRd"/>，把张数落盘。
    ///
    /// 用途与 <c>CardTextLinkerRd.Inventory</c> 同族：离线验收拿纯 Python 的参考实现
    /// （<c>_probe_rdlink_proto.py</c>）算同一批条件，两边张数必须一致 ——
    /// 这样"解析对不对、筛得对不对"就成了可比对的数，不用去游戏里逐张翻卡。
    /// 括号里那个数字是参考实现的期望值（同一份 RD 卡池、剔除衍生物）。
    /// </summary>
    public static void RdQuerySelfTest()
    {
        if (!QuickTestTrace.Enabled || !GameModeManager.IsRD)
        {
            return;
        }
        // 条件 / 主类掩码 / 期望张数（期望值来自 Python 参考实现）
        object[][] cases = new object[][]
        {
            new object[] { "银河族", 1, 205 },
            new object[] { "地属性/机械族", 1, 99 },
            new object[] { "攻击力1000以下", 1, 834 },
            new object[] { "8星以下", 1, 2186 },
            new object[] { "恶魔族以外", 1, 2315 },
            new object[] { "7星以上/暗属性/魔法师族", 1, 36 },
            new object[] { "传说卡", 0, 160 },
            new object[] { "怪兽·魔法·陷阱", 0, 3480 },
        };
        try
        {
            for (int i = 0; i < cases.Length; i++)
            {
                string q = (string)cases[i][0];
                int mask = (int)cases[i][1];
                int want = (int)cases[i][2];
                List<YGOSharp.Card> got = RunQueryRd(q, mask);
                QuickTestTrace.Log("rdlink", "ref q=[" + q + "] mask=" + mask
                    + " n=" + got.Count + " want=" + want
                    + (got.Count == want ? " OK" : " XX"));
            }
        }
        catch (Exception e)
        {
            Program.DEBUGLOG(e);
        }
    }

    /// <summary>
    /// 「卡名跳转」两档的离线自检（只在 <c>log/qt_debug.on</c> 下跑）。
    ///
    /// <para><b>为什么要有这条</b>（用户 2026-10-03）：这两档的判据是
    /// <b>相等</b>（卡名）与<b>描述包含</b>（卡名记述），和满地都是的全文检索口径不同。
    /// 而「点一下会出什么」在游戏里只能靠眼力看，而人眼看不出「这张到底算不算同名」。
    /// ⇒ 把判据变成可数的数：每档都报张数 + 逐张名字，脚本判「有没有混进不该出的卡」。</para>
    ///
    /// <para>用例取本机卡池里**真卡文里出现过**的引用：名字取自
    /// <see cref="CardTextLinker"/> 自己认得出来的卡名（否则「精确相等」会恒空）。
    /// 两档各报一次，外加旧的全���检索同一串字作对照 ——
    /// 对照那一行的张数应当**明显更多**，那正是这次要消灭的差别。</para>
    /// </summary>
    public static void CardNameLinkSelfTest()
    {
        if (!QuickTestTrace.Enabled || GameModeManager.IsRD)
        {
            return;
        }
        CardNameLinkShapeSelfTest();
        string[] names = new string[]
        {
            // 同名异画很多张 —— 「拉之翼神龙只能搜出他自己」就是指这一档
            //   （他自己有 6 个不同的画版本，名字全都叫拉之翼神龙，所以 6 张全在）。
            "拉之翼神龙",
            "青眼白龙",
            "灰姑娘",
            // ⛔ 这里**刻意不放**「黑魔导」：本池里没有恰好叫这个的**卡**，它在卡文里是
            //   **字段/系列名**（黑魔导 archetype），走的是字段那一档（全文检索）。
            //   2026-10-03 我曾把它当「反例」写在这儿，说 exact=0 是好事 ——
            //   那是把字段当卡名了，用户当场指出「黑魔导这个就该像原来一样」。
            //   字段那条路由 <see cref="CardNameLinkShapeSelfTest"/> 的 cases 量。
        };
        try
        {
            for (int i = 0; i < names.Length; i++)
            {
                string n = names[i];
                List<YGOSharp.Card> byName = RunNameQuery(n, 0);
                List<YGOSharp.Card> byRecord = RunRecordQuery(n, 0);
                List<YGOSharp.Card> byText = RunQuery(n, 0);
                // 并集两档：掩码 0（不限主类）与 6（魔法·陷阱，正是那句里的写法）
                List<YGOSharp.Card> u0 = RunUnionQuery(n, 0);
                List<YGOSharp.Card> u6 = RunUnionQuery(n, 6);
                QuickTestTrace.Log("nameq",
                    "key=[" + n + "]"
                    + " exact=" + byName.Count + "[" + JoinNames(byName, 8) + "]"
                    + " record=" + byRecord.Count
                    + " text=" + byText.Count
                    + " union0=" + u0.Count + " union6=" + u6.Count);
                // 硬判据写在行尾，脚本直接咬这一段（不靠人眼数张数）：
                //   exact 里每一张都必须**同名**；record 里每一张的卡文都必须含该串；
                //   union 里每一张必须「同名 或 卡文提到它」二者至少居其一。
                bool exactOk = AllNamed(byName, n);
                bool recordOk = AllDescribed(byRecord, n);
                bool unionOk = AllNamedOrDescribed(u0, n);
                QuickTestTrace.Log("nameq", "key=[" + n + "]"
                    + " exactAllSameName=" + (exactOk ? 1 : 0)
                    + " recordAllMentioned=" + (recordOk ? 1 : 0)
                    + " unionAllNameOrDesc=" + (unionOk ? 1 : 0)
                    + " exactLeText=" + (byName.Count <= byText.Count ? 1 : 0)
                    + " unionGeExact=" + (u0.Count >= byName.Count ? 1 : 0)
                    + (exactOk && recordOk && unionOk ? " OK" : " XX"));
            }
        }
        catch (Exception e)
        {
            Program.DEBUGLOG(e);
        }
    }

    /// <summary>
    /// 链接**形态**自检：把几句**真卡文里出现过的原文**丢进 <see cref="CardTextLinker.Linkify"/>，
    /// 数它分别产出了几种链接（用户 2026-10-03 的三档 + 一个必须**不**误判的反例）。
    ///
    /// <para><b>为什么检索侧那组数不够</b>：<see cref="RunNameQuery"/> 之类验的是「筛选对不对」，
    /// 可它绕过了链接生成端 —— 万一那句「或者有那个卡名记述的」压根没被认成链接、
    /// 玩家点不到，那么筛选函数再对也没人用得上。所以这一档单独量**链接有没有生出来**。</para>
    ///
    /// <para>前三句取自本机卡池的卡文（连上下文一起抄，见各行注释）；最后一句是
    /// <b>反例</b>：它是「有「X」的卡名记述」那一档，<b>不能</b>被当成并集。</para>
    /// </summary>
    static void CardNameLinkShapeSelfTest()
    {
        // {原文, 期望的 u（并集）链接数, 说明}
        string[][] cases = new string[][]
        {
            new string[] { "把「炎之剑士」或者有那个卡名记述的怪兽从卡组送去墓地", "1",
                "「炎之剑士」或者有那个卡名记述的怪兽（连接词「或者」）" },
            new string[] { "把1只「古代的机械巨人」或者1张有那个卡名记述的魔法·陷阱卡加入手卡", "1",
                "连接词带数量字：「或者1张有那个卡名记述的」" },
            new string[] { "场上的「星尘龙」以及有那个卡名记述的同调怪兽回到额外卡组", "1",
                "连接词是「以及」" },
            new string[] { "把有「黑魔术师」的卡名记述的1张魔法·陷阱卡盖放", "0",
                "反例：这是「有「X」的卡名记述」那一档，**不是**并集" },
            new string[] { "「青眼白龙」的卡名记述", "0",
                "反例：光秃秃的卡名记述半句，不带连接词" },
            // 下面几句用来覆盖**字段/系列名**那条路（「机壳」「娱乐伙伴」这类不是卡名）
            new string[] { "「娱乐伙伴」怪兽", "0", "字段 + 种类短语（括号内外必须同档）" },
            new string[] { "「机壳」的卡名记述的1张魔法·陷阱卡", "0", "字段 + 记述半句" },
            new string[] { "「头目连战」怪兽", "0", "字段 + 种类短语（用户举的那句的家族）" },
        };
        try
        {
            int fieldsSeen = 0;
            for (int i = 0; i < cases.Length; i++)
            {
                string rich = CardTextLinker.Linkify(cases[i][0], 0);
                int nUnion = CardTextLinker.CountOccur(rich, "[url=" + CardTextLinker.KindUnion);
                int nName = CardTextLinker.CountOccur(rich, "[url=" + CardTextLinker.KindName);
                int nRecord = CardTextLinker.CountOccur(rich, "[url=" + CardTextLinker.KindRecord);
                int nQuery = CardTextLinker.CountOccur(rich, "[url=" + CardTextLinker.KindQuery);
                int want = int.Parse(cases[i][1]);
                if (nQuery > 0)
                {
                    fieldsSeen++;
                }
                // 🔑 本条判据就是 2026-10-03 那个报障本身：
                //   **字段（q）绝不能同时冒出卡名精确（n）**。
                // 报障现场：字段的括号外那一段（种类短语）被错发成 n，于是
                //   点「黑魔导」是系列检索、点括号外的「怪兽」却是按卡名找「黑魔导」，
                //   两段反应不一样。判据不写死「哪句是字段」——那是数据决定的，
                //   写成不变量才能对**任何**字段句生效。
                bool fieldClean = !(nQuery > 0 && nName > 0);
                QuickTestTrace.Log("nameshape", "u=" + nUnion + " want=" + want
                    + " n=" + nName + " R=" + nRecord + " q=" + nQuery
                    + " fieldClean=" + (fieldClean ? 1 : 0)
                    + (nUnion == want && fieldClean ? " OK" : " XX")
                    + "  || " + cases[i][2]);
            }
            // 反向自检：上面那几句里**必须真的出现过字段**，否则 fieldClean 是空判。
            QuickTestTrace.Log("nameshape", "coverage fieldsSeen=" + fieldsSeen
                + (fieldsSeen > 0 ? " OK" : " XX")
                + "  || 字段那一档必须被真的量到（否则上一条是空判）");
        }
        catch (Exception e)
        {
            Program.DEBUGLOG(e);
        }
    }

    /// <summary>
    /// 钉「括号内」与「括号外种类短语」两段必须是**同一档 + 同一掩码**
    /// （<paramref name="want"/> 形如 <c>q6</c> = 两段都应是字段全文、掩码 6）。
    ///
    /// <para>为什么单列一条：这两段是 <c>CardTextLinker.Build</c> 里**两处独立的
    /// <c>Link</c> 调用**，第二处历史上被写死成「卡名」那一档 ——
    /// 于是字段的括号外变成了按卡名精确找，点两段反应不一样（2026-10-03 用户报障）。
    /// 光数「有没有并集链接」量不到它，必须逐段比。</para>
    /// </summary>
    static string JoinNames(List<YGOSharp.Card> src, int max)
    {
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        int n = src != null ? src.Count : 0;
        for (int i = 0; i < n && i < max; i++)
        {
            if (i > 0)
            {
                sb.Append('|');
            }
            sb.Append(src[i].Name);
        }
        if (n > max)
        {
            sb.Append("|…共").Append(n);
        }
        return sb.ToString();
    }

    static bool AllNamed(List<YGOSharp.Card> src, string name)
    {
        if (src == null)
        {
            return false;
        }
        for (int i = 0; i < src.Count; i++)
        {
            if (src[i] == null || !string.Equals(src[i].Name, name, StringComparison.Ordinal))
            {
                return false;
            }
        }
        return true;
    }

    static bool AllDescribed(List<YGOSharp.Card> src, string name)
    {
        if (src == null)
        {
            return false;
        }
        for (int i = 0; i < src.Count; i++)
        {
            if (src[i] == null || src[i].Desc == null || !src[i].Desc.Contains(name))
            {
                return false;
            }
        }
        return true;
    }

    static bool AllNamedOrDescribed(List<YGOSharp.Card> src, string name)
    {
        if (src == null)
        {
            return false;
        }
        for (int i = 0; i < src.Count; i++)
        {
            YGOSharp.Card c = src[i];
            if (c == null)
            {
                return false;
            }
            bool sameName = string.Equals(c.Name, name, StringComparison.Ordinal);
            bool mentioned = c.Desc != null && c.Desc.Contains(name);
            if (!sameName && !mentioned)
            {
                return false;
            }
        }
        return true;
    }

    /// <summary>
    /// 「整池」取法：<c>searchAdvanced</c> 不带任何过滤 = 全部非衍生物卡（与
    /// <see cref="RunQueryRd"/> 同一份口径）。精确筛类的两档都拿它当底。
    /// </summary>
    static List<YGOSharp.Card> WholePool()
    {
        return YGOSharp.CardsManager.searchAdvanced
            (
            "", -233, -233, -233, -233, -233,
            -233, -233, -233, -233, -233,
            -233, "", -233, null,
            0u, 0u, 0u, 0u, 0u
            );
    }

    /// <summary>
    /// 验收/自检用：整个卡池（与检索走的**同一份**，`-233` 全通配）。
    /// 给「富化不许丢字」那条全池自检用（<c>CardTextLinker.PoolNoDropSelfTest</c>）。
    /// </summary>
    public static List<YGOSharp.Card> ProbeWholePool()
    {
        return WholePool();
    }

    /// <summary>
    /// 只出「<b>卡名精确等于</b> <paramref name="name"/>」的那些卡（用户 2026-10-03：
    /// 点「拉之翼神龙」只能搜出他自己）。
    ///
    /// <para>⛔ 判据是 <b>相等</b>不是包含 —— 这正是与旧的全���检索的分别所在。
    /// 同名异画的两张会一起列全（它们名字相同），名字里只是**含**它的卡一张都不出。
    /// 用 <see cref="StringComparison.Ordinal"/>：卡名是精确串，不该做区域/文化比较。</para>
    ///
    /// <para>🔑 <b>规则同名也算</b>（用户 2026-10-04）：「传说之都 亚特兰蒂斯」的卡文写着
    /// 「这个卡名在规则上当作「海」使用」⇒ 点「海」必须把它一起列出来。数据源就是 cdb 的
    /// <c>datas.alias</c> —— 本机实测 580 张带 alias 的卡里，**同名的那 562 张是异画**，
    /// 剩下 18 张全是"规则同名"（亚特兰蒂斯/帕西菲斯/忘却之都→海、鹰身女郎1/2/3→鹰身女郎、
    /// 置换融合→融合 …）⇒ 判据：alias 一路指上去，落在"名字等于 name 的那批卡号"里就算。</para>
    ///
    /// <para>⚠ 链式 alias（A→B→C）要跟着走（最多 4 跳，同 <c>CardsManager.BuildAliasRoot</c> 口径）；
    /// 找卡用**池内字典**而不是 <c>CardsManager.Get</c> —— 后者的位宽回退会返回一张不相干的卡。</para>
    /// </summary>
    public static List<YGOSharp.Card> RunNameQuery(string name, int typeMask)
    {
        List<YGOSharp.Card> pool = WholePool();
        List<YGOSharp.Card> kept = new List<YGOSharp.Card>();
        // ① 先收"名字一模一样"的（同名异画全在里面），同时建 id→卡 的池内字典供 alias 解析。
        HashSet<int> sameName = new HashSet<int>();
        Dictionary<int, YGOSharp.Card> byId = new Dictionary<int, YGOSharp.Card>(pool.Count);
        for (int i = 0; i < pool.Count; i++)
        {
            YGOSharp.Card c = pool[i];
            if (c == null)
            {
                continue;
            }
            byId[c.Id] = c;
            if (string.Equals(c.Name, name, StringComparison.Ordinal))
            {
                sameName.Add(c.Id);
            }
        }
        // ② 再按 alias 收"规则同名"的。
        int aliased = 0;
        for (int i = 0; i < pool.Count; i++)
        {
            YGOSharp.Card c = pool[i];
            if (c == null || !TypeMatches((uint)c.Type, typeMask))
            {
                continue;
            }
            if (sameName.Contains(c.Id))
            {
                kept.Add(c);
                continue;
            }
            int t = c.Alias;
            bool hit = false;
            for (int hop = 0; hop < 4 && t > 0; hop++)
            {
                if (sameName.Contains(t))
                {
                    hit = true;
                    break;
                }
                YGOSharp.Card up;
                if (!byId.TryGetValue(t, out up) || up == null || up.Alias == t)
                {
                    break;
                }
                t = up.Alias;
            }
            if (hit)
            {
                aliased++;
                kept.Add(c);
            }
        }
        QuickTestTrace.Log("link", "nameq name=[" + name + "] mask=" + typeMask
            + " pool=" + pool.Count + " sameName=" + sameName.Count
            + " ruled=" + aliased + " n=" + kept.Count);
        return kept;
    }

    /// <summary>
    /// 只出「<b>卡文里记述了</b> <paramref name="name"/>」的那些卡（用户 2026-10-03）。
    ///
    /// <para>判据落在 <see cref="YGOSharp.Card.Desc"/> 上，<b>不</b>看卡名 ——
    /// 否则「名字里带这个串」的卡会混进来，那不是「记述」。
    /// ⛔ 「本体没记述则本体都出不了」是这条语义的**自然结果**：一张卡通常不会在自己的
    /// 卡文里提到自己，所以本体自己往往就不在结果里；不需要额外特判。</para>
    /// </summary>
    public static List<YGOSharp.Card> RunRecordQuery(string name, int typeMask)
    {
        List<YGOSharp.Card> pool = WholePool();
        List<YGOSharp.Card> kept = new List<YGOSharp.Card>();
        for (int i = 0; i < pool.Count; i++)
        {
            YGOSharp.Card c = pool[i];
            if (c != null && c.Desc != null && c.Desc.Contains(name)
                && TypeMatches((uint)c.Type, typeMask))
            {
                kept.Add(c);
            }
        }
        QuickTestTrace.Log("link", "recordq name=[" + name + "] mask=" + typeMask
            + " pool=" + pool.Count + " n=" + kept.Count);
        return kept;
    }

    /// <summary>
    /// 按**系列成员**列出「真正属于这个系列」的全部卡（用户 2026-10-04）：
    /// 判据是 <c>CardsManager.IfSetCard</c> —— 引擎自己（搜索语言 <c>OPCODE_ISSETCARD</c>）
    /// 用的就是这个：**低 12 位 = 系列本体、高 4 位 = 子系列**，逐 16 位槽比。
    ///
    /// <para>⛔ 为什么不能用 <c>(Setcode &gt;&gt; j*16) &amp; 0xffff == code</c> 那种"槽完全相等"：
    /// 子系列会被漏掉 —— 例：元素英雄 新宇侠 的槽是 <c>0x3008</c>（E-HERO），
    /// 槽相等口径下它**不是**「英雄」卡；而 IfSetCard(0x8) 看低 12 位（0x008==0x008）⇒ 是。
    /// 用户要的「字段是 X」正是这个口径。</para>
    ///
    /// <para><paramref name="code"/> &lt; 0 = 判据丢了（不该发生；探针路径除外）⇒
    /// 宁可给空列表也别退化成全文检索 —— 那会把"卡文里提到它"的无关卡混进来。</para>
    /// </summary>
    public static List<YGOSharp.Card> RunSetcodeQuery(int code, int typeMask)
    {
        List<YGOSharp.Card> kept = new List<YGOSharp.Card>();
        if (code < 0)
        {
            QuickTestTrace.Log("link", "setq code<0 (判据丢失) mask=" + typeMask);
            return kept;
        }
        List<YGOSharp.Card> pool = WholePool();
        for (int i = 0; i < pool.Count; i++)
        {
            YGOSharp.Card c = pool[i];
            if (c == null || !TypeMatches((uint)c.Type, typeMask))
            {
                continue;
            }
            if (YGOSharp.CardsManager.IfSetCard(code, c.Setcode))
            {
                kept.Add(c);
            }
        }
        QuickTestTrace.Log("link", "setq code=0x" + code.ToString("x") + " mask=" + typeMask
            + " pool=" + pool.Count + " n=" + kept.Count);
        return kept;
    }

    /// <summary>
    /// 「<b>X 或者卡文里记述了 X</b>」的<b>并集</b>（用户 2026-10-03：
    /// 「并集是形如『「头目连战」或者有那个卡名记述的魔法·陷阱卡』的情况」）。
    ///
    /// <para><b>掩码只作用在记述那一支</b> —— 句子里「魔法·陷阱卡」修饰的是
    /// 「有那个卡名记述的」，不是「头目连战」那半截。若两支都限，那支本来是怪兽的
    /// 「头目连战」会被限成魔法·陷阱而**一张都出不来**，一眼就白链了。</para>
    ///
    /// <para>结果里同名卡在前、记述卡在后（各支内部保持整池顺序），这样「两者都有」
    /// 在列表上一眼能分清哪张是本体、哪张是靠卡文关联来的。</para>
    /// </summary>
    public static List<YGOSharp.Card> RunUnionQuery(string name, int typeMask)
    {
        List<YGOSharp.Card> byName = RunNameQuery(name, 0);      // 同名那支：不带掩码
        List<YGOSharp.Card> byRecord = RunRecordQuery(name, typeMask);  // 记述那支：带掩码
        List<YGOSharp.Card> kept = new List<YGOSharp.Card>(byName.Count + byRecord.Count);
        kept.AddRange(byName);
        for (int i = 0; i < byRecord.Count; i++)
        {
            YGOSharp.Card c = byRecord[i];
            if (c == null)
            {
                continue;
            }
            // 同一张卡可能两支都命中（名字正好等于那个串）——去重，只留一份。
            bool dup = false;
            for (int j = 0; j < byName.Count; j++)
            {
                if (byName[j] != null && byName[j].Id == c.Id)
                {
                    dup = true;
                    break;
                }
            }
            if (!dup)
            {
                kept.Add(c);
            }
        }
        QuickTestTrace.Log("link", "unionq name=[" + name + "] mask=" + typeMask
            + " sameName=" + byName.Count + " record=" + byRecord.Count
            + " union=" + kept.Count);
        return kept;
    }

    void Search()
    {
        if (scroll == null)
        {
            return;
        }
        bool rd = !string.IsNullOrEmpty(lastRd);
        List<YGOSharp.Card> result;
        if (rd)
        {
            result = RunQueryRd(lastRd, lastMask);
        }
        else if (lastFilter == Filter.ExactName)
        {
            result = RunNameQuery(lastQuery, lastMask);
        }
        else if (lastFilter == Filter.Record)
        {
            result = RunRecordQuery(lastQuery, lastMask);
        }
        else if (lastFilter == Filter.NameOrRecord)
        {
            result = RunUnionQuery(lastQuery, lastMask);
        }
        else if (lastFilter == Filter.Setcode)
        {
            result = RunSetcodeQuery(lastSetHash, lastMask);
        }
        else
        {
            result = RunQuery(lastQuery, lastMask);
            result = ExactNameFirst(result, lastQuery);
        }
        List<string[]> args = new List<string[]>();
        for (int i = 0; i < result.Count; i++)
        {
            YGOSharp.Card item = result[i];
            string[] arg = new string[5];
            arg[0] = item.Id.ToString();
            arg[1] = "3";
            arg[2] = item.Name + "\n" + GameStringHelper.getSearchResult(item);
            args.Add(arg);
        }
        // 留住这一批，「译」钮批量换表时要用（见 onPackClicked）。
        // ⛔ 存**引用**即可，不要 clone：列表里显示的本来就是这些池卡的字段，
        //   换完译名重算的是同一批对象（CardsManager.ApplyPool 就地改 Name/Desc）。
        lastResult = result;
        scroll.print(args);
        scroll.toTop();
        string head;
        if (lastTitle.Length > 0)
        {
            // 🔑 用户 2026-10-04 第三条：标题**照抄玩家点的那句话** + 结果数。
            //   谁带来的（卡名/系列行/字段+种类词）由链接生成端决定，这里一个字都不加工。
            head = lastTitle;
        }
        else if (rd)
        {
            head = MainWord(lastMask) + "（" + lastRd + "）";
        }
        else if (lastFilter == Filter.ExactName)
        {
            head = "卡名＝" + (lastQuery.Length > 0 ? lastQuery : "全部");
        }
        else if (lastFilter == Filter.Record)
        {
            head = (lastQuery.Length > 0 ? lastQuery : "全部") + " 的卡名记述";
        }
        else if (lastFilter == Filter.NameOrRecord)
        {
            head = (lastQuery.Length > 0 ? lastQuery : "全部")
                + (lastMask == 0 ? " 或记述了它" : " 或记述了它的" + MainWord(lastMask));
        }
        else if (lastFilter == Filter.Setcode)
        {
            head = (lastQuery.Length > 0 ? lastQuery : "?");
        }
        else
        {
            head = lastQuery.Length > 0 ? lastQuery : "全部";
        }
        UIHelper.trySetLableText(win, "title_", head + "  " + result.Count);
        // 检索口径落盘（与编辑器的 [mode] search 同族，便于离线验收）。
        QuickTestTrace.Log("link", "search q=[" + lastQuery + "] mask=" + lastMask
            + " rd=[" + (lastRd ?? "") + "] filter=" + lastFilter
            + " title=[" + lastTitle + "]"
            + " mode=" + GameModeManager.ModeLabel
            + " n=" + result.Count);
    }

    /// <summary>
    /// 规则：**卡名链接现在也走检索窗**（用户要求 2026-10-02，点卡名不再直接切左侧面板），
    /// 所以必须让"完全同名的卡"排在最前面，否则点一下「青眼白龙」出来的头一条
    /// 可能是「青眼白龙大邪神」这种只是**包含**该名字的长名，玩家要点好几下才看到要找的那张。
    /// 三档稳定分组：完全同名 → 名字以检索词开头 → 其余（各档内部保持原顺序）。
    /// 同名异画（两张同名的卡）会并排出现在第一档，正好一起列全。
    /// </summary>
    static List<YGOSharp.Card> ExactNameFirst(List<YGOSharp.Card> src, string query)
    {
        if (src == null || string.IsNullOrEmpty(query))
        {
            return src;
        }
        List<YGOSharp.Card> exact = new List<YGOSharp.Card>();
        List<YGOSharp.Card> prefix = new List<YGOSharp.Card>();
        List<YGOSharp.Card> rest = new List<YGOSharp.Card>();
        for (int i = 0; i < src.Count; i++)
        {
            YGOSharp.Card c = src[i];
            string n = c == null ? null : c.Name;
            if (string.Equals(n, query, StringComparison.Ordinal))
            {
                exact.Add(c);
            }
            else if (!string.IsNullOrEmpty(n) && n.StartsWith(query, StringComparison.Ordinal))
            {
                prefix.Add(c);
            }
            else
            {
                rest.Add(c);
            }
        }
        if (exact.Count == 0 && prefix.Count == 0)
        {
            // 这次检索根本不是在找卡名（系列名 / 整句 / 手输关键字），不必重排。
            return src;
        }
        exact.AddRange(prefix);
        exact.AddRange(rest);
        return exact;
    }

    /// <summary>
    /// 行工厂：**只造壳、不填值**。
    ///
    /// <para>⛔ 为什么必须把「造行」与「填值」拆开：<c>VirtualScrollView</c> 是**行池**语义
    /// （与 YGOPro2 原生同源）—— 它把行对象**反复复用**去装别的卡，只要求「可见的那十几行」
    /// 存在。所以一切跟「这一行现在装哪张卡」有关的东西都必须放进
    /// <see cref="ItemBinder"/>，留在这里的只能是「这一行永远不变」的结构活：
    /// 克隆行模板、挂 <c>cardPicLoader</c>、抓住 <c>pic_</c> 贴图与禁卡图标这两个引用。</para>
    /// </summary>
    GameObject itemOnListProducer(string[] Args)
    {
        GameObject rv = create(Program.I().new_ui_cardOnSearchList, Vector3.zero, Vector3.zero, false, Program.ui_back_ground_2d);
        cardPicLoader loader = UIHelper.getRealEventGameObject(rv).AddComponent<cardPicLoader>();
        loader.uiTexture = UIHelper.getByName<UITexture>(rv, "pic_");
        loader.ico = UIHelper.getByName<ban_icon>(rv);
        if (loader.ico != null)
        {
            loader.ico.show(3);
        }
        return rv;
    }

    /// <summary>
    /// 行绑定：这一行每次被（重新）派给某个检索结果时调用 —— **装卡相关的活全在这儿**。
    ///
    /// <para>⚠ 必须是「先复位再填」而不是「填一次就算」：行池把一行从 A 卡改派给 B 卡时，
    /// <c>cardPicLoader.Update</c> 是靠 <c>loaded_code != code</c> 才去换图的，
    /// 所以这里要把 <c>loaded_code</c>（经 <see cref="cardPicLoader.reCode"/>）与
    /// <c>loaded_banlist</c> 复位、并把旧贴图清成 <c>null</c> —— 否则在新图加载出来之前，
    /// 这一格会**继续显示上一张卡**（原生那边的 binder 也是这么清的）。</para>
    ///
    /// <para>用 <see cref="cardPicLoader.reCode"/> 而不是直接写 <c>code</c>，是为了顺手处理
    /// 「被空格锁住的那一条被改派去装别的卡」：它会通知 <c>CardDescLock</c> 把锁放掉，
    /// 免得「行已经换成别的卡了、左侧简介还被钉在原来那张上」。</para>
    /// </summary>
    void ItemBinder(GameObject row, string[] Args)
    {
        GameObject eventGo = UIHelper.getRealEventGameObject(row);
        cardPicLoader loader = eventGo.GetComponent<cardPicLoader>();
        if (loader == null)
        {
            return;
        }
        int code = int.Parse(Args[0]);
        eventGo.name = Args[0];
        UIHelper.trySetLableText(row, Args[2]);
        loader.reCode(code);
        loader.data = YGOSharp.CardsManager.Get(code);
        if (loader.uiTexture != null)
        {
            loader.uiTexture.mainTexture = null;
        }
        loader.loaded_banlist = null;
        if (loader.ico != null)
        {
            loader.ico.show(3);
        }
    }

    public override void ES_HoverOverGameObject(GameObject gameObject)
    {
        if (!isShowed || gameObject == null)
        {
            return;
        }
        cardPicLoader loader = gameObject.GetComponent<cardPicLoader>();
        if (loader != null && loader.data != null)
        {
            Program.I().cardDescription.setData(loader.data, GameTextureManager.myBack);
        }
    }

    /// <summary>
    /// 「返回上一级」钮自愈：每次本窗要显示的时候都过一遍。
    ///
    /// 为什么需要（用户实测 2026-10-02）：**点一次这颗钮，它就再也不出现**。
    /// 它是运行期建出来的、只在 <see cref="installBackButton"/> 里建一次，所以只要
    /// 点击之后有任何东西把贴图引用 / 面板归属 / 激活状态弄掉，就再没有第二次补回来的机会
    /// —— 而面板上 search_ / detailed_ 那两颗是 prefab 自带的，所以不会犯这个毛病。
    /// 逐项复查并就地修好：对象在不在、贴图还在不在、有没有归到面板上、
    /// alpha 是不是 0、点按回调有没有被清掉。幂等，正常情况下什么都不改。
    ///
    /// ⚠ 图标要用 <c>GetComponentInChildren</c> 找：克隆体的图标 widget 挂在
    ///   <c>Animation/Texture</c> 子物体上，<c>GetComponent</c> 只看根节点、会返回 null ——
    ///   那样这里每次开窗都会把整颗钮拆了重建（比"点一下就没了"还糟）。
    /// </summary>
    void ensureBackButton()
    {
        try
        {
            if (win == null)
            {
                return;
            }
            // ⛔ 必须用 FindDeep（含失活）：getByName 会跳过失活子树，而本钮在
            //   详情版里会被 SetActive(false) 藏过 —— 用 getByName 会误判成"钮没了"，
            //   于是走重建；重建又因为克隆源 search_ 同样被藏着而静默失败，
            //   结果**钮彻底消失**（用户实测「点一下就没了」，日志：back=missing）。
            GameObject b = FindDeep(win, BackButtonName);
            if (b == null)
            {
                installBackButton();
                return;
            }
            if (!b.activeSelf)
            {
                b.SetActive(true);
            }
            UITexture icon = b.GetComponentInChildren<UITexture>(true);
            if (icon == null)
            {
                b.transform.SetParent(null, false);
                UnityEngine.Object.Destroy(b);
                installBackButton();
                return;
            }
            if (icon.mainTexture == null)
            {
                Texture2D again = GameTextureManager.get(BackIconName);
                if (again != null)
                {
                    icon.path = BackIconName;
                    icon.mainTexture = again;
                }
            }
            if (!icon.gameObject.activeSelf)
            {
                icon.gameObject.SetActive(true);
            }
            if (icon.panel == null)
            {
                // 真凶：控件没归到任何 UIPanel。NGUI 只把控件画进它 mPanel 指到的那个面板，
                // 没有面板 = 一个像素都不画 —— 看上去就是「按钮的贴图没了，其它还在」。
                // 先试**无伤自愈**：SetActive false→true 逼 OnEnable 重跑一遍，控件会重新
                // 注册进面板。只有这一步也救不回来（win 已激活、重激活后还是 null）才拆了
                // 重建 —— 之前直接重建，而重建的克隆源 search_ 已被详情版藏掉，
                // 克隆出一个藏着的钮，永久消失。
                if (win.activeInHierarchy)
                {
                    icon.gameObject.SetActive(false);
                    icon.gameObject.SetActive(true);
                    if (icon.panel != null)
                    {
                        QuickTestTrace.Log("link", "backensure re-registered panel="
                            + icon.panel.name);
                        return;
                    }
                }
                // 重挂贴图救不回来（面板注册控件走的是 UIWidget 的内部流程，外部改不回去），
                // 所以直接把这颗钮拆了重建，让它走一遍和 search_ / detailed_ 一样的注册路径。
                b.transform.SetParent(null, false);
                UnityEngine.Object.Destroy(b);
                installBackButton();
                return;
            }
            // 图标/配色的自愈统一走 UpdateBackIcon —— 它同时负责"退无可退时换成 X"。
            UpdateBackIcon();
            if (QuickTestTrace.Enabled)
            {
                QuickTestTrace.Log("link", "backensure self=" + b.activeSelf
                    + " tex=" + (icon.mainTexture != null ? "有" : "NULL")
                    + " panel=" + (icon.panel != null ? icon.panel.name : "none")
                    + " icon=" + icon.path
                    + " col=" + icon.color.r.ToString("F2") + ","
                    + icon.color.g.ToString("F2") + "," + icon.color.b.ToString("F2")
                    + "," + icon.color.a.ToString("F2"));
            }
        }
        catch (Exception e)
        {
            Program.DEBUGLOG(e);
        }
    }

    /// <summary>
    /// 返回钮此刻该长什么样 —— 换图标 + 上皮色。每次开窗 / 每次切层级都过一遍（幂等）。
    ///
    /// 两件事：
    ///   ① **图标**：还有上一级 → 左箭头（texture/ui/left.png）；退无可退 → 换成
    ///      texture/ui/no.png 那枚 X（用户 2026-10-02 口径：「没有上一级的时候
    ///      上一级按钮变成关掉的 x 号」）。点它的动作不用改 —— <see cref="goBack"/>
    ///      在 history 空的时候本来就是把窗收掉。悬停提示也一起换。
    ///   ② **配色**：从放大镜那颗图标**逐字段抄**（<see cref="CaptureBackIconColor"/>）。
    ///      用户口径是「返回钮要和搜索界面的放大镜、分类图标一个风格（颜色等）」，
    ///      所以不能写死一个常数 —— 写死就等于把这个口径钉在一个未必等于放大镜的值上。
    /// </summary>
    void UpdateBackIcon()
    {
        if (win == null)
        {
            return;
        }
        try
        {
            GameObject b = FindDeep(win, BackButtonName);
            if (b == null)
            {
                return;
            }
            UITexture icon = b.GetComponentInChildren<UITexture>(true);
            if (icon == null)
            {
                return;
            }
            CaptureBackIconColor();
            string want = history.Count > 0 ? BackIconName : CloseIconName;
            Texture2D tex = GameTextureManager.get(want);
            if (tex != null)
            {
                if (icon.mainTexture != tex)
                {
                    icon.mainTexture = tex;
                }
                icon.path = want;
            }
            icon.color = backIconColor;
            if (!icon.gameObject.activeSelf)
            {
                icon.gameObject.SetActive(true);
            }
            hinter hint = b.GetComponent<hinter>();
            if (hint != null)
            {
                hint.str = history.Count > 0 ? "返回上一级" : "关闭";
            }
        }
        catch (Exception e)
        {
            Program.DEBUGLOG(e);
        }
    }

    /// <summary>
    /// 把放大镜图标（<c>search_/…</c>）的配色抄进 <see cref="backIconColor"/>。
    ///
    /// ⚠ 必须按 <c>path</c> 认那颗图标，不能只取子树里的第一块 UITexture ——
    ///   实测放大镜钮的子树里不止一块纹理（还带了一枚装饰用的金色徽记
    ///   <c>11d_topright.png</c>，见 <see cref="ProbeBackDeep"/> 的注释），
    ///   拿到徽记的颜色就抄错了。找不到放大镜就退到分类钮，再找不到才用兜底色。
    /// </summary>
    void CaptureBackIconColor()
    {
        if (win == null)
        {
            return;
        }
        try
        {
            UITexture src = IconTextureUnder("search_", "search");
            if (src == null)
            {
                src = IconTextureUnder("detailed_", "detail");
            }
            if (src == null)
            {
                backIconColor = BackIconFallback;
                return;
            }
            Color c = src.color;
            if (c.a <= 0f)
            {
                // 源图标被某处改了 alpha（藏起一半再放回来之类）时别把"看不见"抄过来。
                c.a = BackIconFallback.a;
            }
            backIconColor = c;
        }
        catch (Exception e)
        {
            Program.DEBUGLOG(e);
        }
    }

    /// <summary>某一颗面板钮（<c>search_</c>/<c>detailed_</c>）上那张指定 <c>path</c> 的图标；找不到返回 null。</summary>
    UITexture IconTextureUnder(string buttonName, string path)
    {
        GameObject g = FindDeep(win, buttonName);
        if (g == null)
        {
            return null;
        }
        UITexture[] all = g.GetComponentsInChildren<UITexture>(true);
        if (all.Length == 0)
        {
            return null;
        }
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] != null && all[i].path == path)
            {
                return all[i];
            }
        }
        return all[0];
    }

    /// <summary>
    /// 补一颗「返回上一级」钮：挂在检索输入框的左端。
    ///
    /// ⚠ 造法是 **Instantiate 克隆同面板那颗现成能用的 search_**，不是自己 new 一个
    ///   GameObject 再挂 UITexture/BoxCollider/UIButton/UIEventTrigger ——
    ///   这是用户实测「点一下贴图就没了、以后再也不出现」的真凶（2026-10-02）。
    ///   工程里所有运行期补的钮都是克隆出来的（DeckManager.createTestButton 439 行、
    ///   gameButton.show 33 行）。NGUI 的 UIPanel 只把「它自己认得的」控件收进去画；
    ///   自己 new 出来的控件面板不认，于是显示一阵、点一下就没了，而且因为这里只建一次，
    ///   再也补不回来。克隆把碰撞体、面板归属、UIButton 配色、点按音效整套都带过来。
    ///
    /// 位置只能选输入框左端：右端那 54px 已经被 search_ / detailed_ 占满，塞不下第三颗。
    /// </summary>
    /// <summary>把 UIRect 的四角用指定相机换算成屏幕矩形 (左,下)-(右,上)。
    /// ⛔ 「用哪台相机换算」在本项目里是真会出错的一件事：本窗挂在 depth −2 那条链
    ///   （ui_back_ground_2d）上，用主 2D UI 的相机换算会把坐标推到屏外 ——
    ///   所以探针必须**两台都算**，并用一个"屏幕上确实看得见"的参照物（结果列表）来判哪台是对的。</summary>
    public static string RectOf(UIRect r, Camera cam)    {
        if (r == null || cam == null)
        {
            return "n/a";
        }
        Vector3[] c = r.worldCorners;
        float x0 = float.MaxValue, y0 = float.MaxValue, x1 = float.MinValue, y1 = float.MinValue;
        for (int i = 0; i < c.Length; i++)
        {
            Vector3 p = cam.WorldToScreenPoint(c[i]);
            if (p.x < x0) { x0 = p.x; }
            if (p.x > x1) { x1 = p.x; }
            if (p.y < y0) { y0 = p.y; }
            if (p.y > y1) { y1 = p.y; }
        }
        return "(" + Mathf.RoundToInt(x0) + "," + Mathf.RoundToInt(y0) + ")-("
            + Mathf.RoundToInt(x1) + "," + Mathf.RoundToInt(y1) + ")";
    }

    static string CamInfo(Camera c)
    {
        if (c == null)
        {
            return "null";
        }
        return c.name + "(ortho=" + c.orthographic + ",size=" + c.orthographicSize.ToString("F2")
            + ",pos=" + c.transform.position.ToString("F1")
            + ",depth=" + c.depth + ",mask=" + c.cullingMask + ")";
    }

    /// <summary>
    /// 返回钮「到底画在哪、为什么看不见」的纵深探针（2026-10-02 第五轮）。
    ///
    /// <para>已知症状：`[btnprobe] backstate` 报 <c>panel=mod_2d_ui(Clone) alpha=0.78</c>
    /// —— 面板注册上了、透明度也对，但 `rect=(2782,612)-(2806,638)` 的 x=2794 在 1920 宽的屏外；
    /// 而实拍图上那个位置画的是**一枚金色徽记**（`11d_topright.png`），不是 <c>left.png</c> 的白箭头。</para>
    ///
    /// <para>所以这一段要把三件事一次摊开：
    /// ① 钮子树里**一共有几块 UITexture**、各自的名字/贴图/path/panel/尺寸 —— 克隆体很可能
    ///    把放大镜钮上那枚金色徽记一起带了过来，而探针 `GetComponentInChildren` 只拿到第一块；
    /// ② 每块 UITexture 用**两台相机**各算一遍屏幕矩形（本窗与主 2D UI 未必同相机）；
    /// ③ 同一个参照物（结果列表 <c>panel_</c>、窗口根 <c>win</c>）也两台各算一遍 ——
    ///    屏幕上看得见的那块，哪台相机把它算进 0..1920 内，哪台才是这扇窗的真相机。</para>
    /// </summary>
    public string ProbeBackDeep
    {
        get
        {
            try
            {
                Camera m2 = Program.camera_main_2d;
                Camera bg = Program.camera_back_ground_2d;
                // ⚠ `back` 是 UITexture（那颗图标），钮的**根**要按名字找 —— 两者不能混。
                GameObject b = win != null ? FindDeep(win, BackButtonName) : null;
                if (b == null && back != null)
                {
                    b = back.gameObject;
                }
                System.Text.StringBuilder sb = new System.Text.StringBuilder("backdeep");
                sb.Append(" detail=").Append(detailApplied)
                    .Append(" slotPos=").Append(detailSlotPos.ToString("F1"))
                    .Append(" slotParent=").Append(detailSlotParent != null
                        ? detailSlotParent.name + "(ls=" + detailSlotParent.localScale.ToString("F2") + ")"
                        : "-");
                if (b == null)
                {
                    sb.Append(" back=missing");
                }
                else
                {
                    UITexture[] texs = b.GetComponentsInChildren<UITexture>(true);
                    sb.Append(" backLP=").Append(b.transform.localPosition.ToString("F1"))
                        .Append(" backLS=").Append(b.transform.localScale.ToString("F2"))
                        .Append(" texCount=").Append(texs.Length);
                    for (int i = 0; i < texs.Length && i < 5; i++)
                    {
                        UITexture t = texs[i];
                        sb.Append(" |").Append(i).Append(':').Append(t.name)
                            .Append(" tex=").Append(t.mainTexture != null ? t.mainTexture.name : "NULL")
                            .Append(" path=").Append(t.path)
                            .Append(" panel=").Append(t.panel != null ? t.panel.name : "none")
                            .Append(" act=").Append(t.gameObject.activeInHierarchy ? 1 : 0)
                            .Append(" L=").Append(t.gameObject.layer)
                            .Append(" size=").Append(t.width).Append("x").Append(t.height)
                            .Append(" lp=").Append(t.transform.localPosition.ToString("F1"))
                            .Append(" M2=").Append(RectOf(t, m2))
                            .Append(" BG=").Append(RectOf(t, bg));
                    }
                }
                // 参照物：结果列表（屏幕上确实看得见）与窗口根 —— 用来判「哪台相机是对的」。
                sb.Append(" LIST_BG=").Append(RectOf(panel, bg))
                    .Append(" LIST_M2=").Append(RectOf(panel, m2));
                // 放大镜那一格（＝返回钮**应该**落在的地方）。钮的矩形必须与它重合；
                // 不重合就说明定位口径错了（同一次、同一台相机，横向纵向都可直接比）。
                GameObject sq = win != null ? FindDeep(win, "search_") : null;
                UITexture sqIcon = sq != null
                    ? sq.GetComponentInChildren<UITexture>(true) : null;
                sb.Append(" SEARCH_ICON=").Append(RectOf(sqIcon, m2))
                    .Append(" SEARCH_ROOT=").Append(RectOf(
                        sq != null ? sq.GetComponent<UIRect>() : null, m2));
                UIRect winRect = win != null ? win.GetComponent<UIRect>() : null;
                sb.Append(" WIN_BG=").Append(RectOf(winRect, bg))
                    .Append(" WIN_M2=").Append(RectOf(winRect, m2));
                sb.Append(" camBG=").Append(CamInfo(bg))
                    .Append(" camM2=").Append(CamInfo(m2));
                return sb.ToString();
            }
            catch (Exception e)
            {
                return "backdeep failed " + e.GetType().Name + ": " + e.Message;
            }
        }
    }

    /// <summary>
    /// 「本窗到底有没有被画出来」的绘制归属探针（2026-10-02 第六轮）。
    ///
    /// <para>为什么加这一段：实拍取证（<c>11_searchdetail_b.png</c> 全分辨率）显示，
    /// 在 <c>[link] wininfo</c> 那一刻，**返回钮的两个候选落点都是空白背景** ——
    /// 不只是钮没画，整个检索窗都没有像素。所以「返回钮没显示成功」这一条，
    /// 前提条件（窗画出来了）本身就不成立，先得把「画没画」变成可判的。</para>
    ///
    /// <para>NGUI 只把控件画进它 <c>mPanel</c> 指到的那块面板，而**面板名可能撞车**
    /// （本项目里到处是 <c>mod_2d_ui(Clone)</c> —— 切 servant 时 UI 树是整棵克隆出来的，
    /// 两个同名面板可以同时存在：一个在当前 servant 的树上被画，另一个被丢下不画）。
    /// 所以这里要一次摊开三件事：
    /// ① 本窗的容器 <c>under_</c>（＝字段 <c>back</c>）的矩形/尺寸/贴图/面板名；
    /// ② <c>under_</c> 归到的那块面板**是不是** NGUI 正在画的 <c>UIPanel.list</c> 里的那一块
    ///    （不在列表里 = 死面板，画多少都不会出现）；
    /// ③ <c>UIPanel.list</c> 的完整绘制序（深度 / 层 / 活否 / renderQueue / 相机深度）。</para>
    /// </summary>
    public string ProbeDrawOrder
    {
        get
        {
            try
            {
                Camera m2 = Program.camera_main_2d;
                System.Text.StringBuilder sb = new System.Text.StringBuilder("draworder");
                if (back != null)
                {
                    sb.Append(" UNDER_rect=").Append(RectOf(back, m2))
                        .Append(" size=").Append(back.width).Append("x").Append(back.height)
                        .Append(" pivot=").Append((int)back.pivot)
                        .Append(" wp=").Append(back.transform.position.ToString("F2"))
                        .Append(" tex=").Append(back.mainTexture != null
                            ? back.mainTexture.name + "/" + back.mainTexture.width
                                + "x" + back.mainTexture.height
                            : "NULL")
                        .Append(" col=").Append(back.color.ToString("F2"))
                        .Append(" act=").Append(back.gameObject.activeInHierarchy ? 1 : 0)
                        .Append(" panel=").Append(back.panel != null ? back.panel.name : "none");
                    UIPanel bp = back.panel;
                    if (bp != null)
                    {
                        int idx = UIPanel.list.IndexOf(bp);
                        sb.Append(" panelDepth=").Append(bp.depth)
                            .Append(" panelIdx=").Append(idx)
                            .Append(" panelLayer=").Append(bp.gameObject.layer)
                            .Append(" panelActive=").Append(bp.gameObject.activeInHierarchy ? 1 : 0)
                            .Append(" panelRect=").Append(RectOf(bp, m2));
                    }
                }
                sb.Append(" PANELS=");
                for (int i = 0; i < UIPanel.list.Count && i < 40; i++)
                {
                    UIPanel p = UIPanel.list[i];
                    if (p == null)
                    {
                        continue;
                    }
                    sb.Append('[').Append(i).Append(']').Append(p.name)
                        .Append("/d").Append(p.depth)
                        .Append("/L").Append(p.gameObject.layer)
                        .Append(p.gameObject.activeInHierarchy ? "/on" : "/off")
                        .Append("/cam").Append(p.anchorCamera != null
                            ? p.anchorCamera.depth.ToString() : "-")
                        .Append(' ');
                }
                return sb.ToString();
            }
            catch (Exception e)
            {
                return "draworder failed " + e.GetType().Name + ": " + e.Message;
            }
        }
    }

    /// <summary>验收用：把本窗容器 <c>under_</c> 整块染绿（截图取证「窗的占屏范围在哪」）。</summary>
    public void markWindowForScreenshot()
    {
        if (back == null)
        {
            return;
        }
        back.color = new Color(0f, 1f, 0f, 0.55f);
    }

    void installBackButton()
    {
        try
        {
            // ⛔⛔ 这里三处查找都必须是 FindDeep（含失活）—— 这是「返回钮点一下就永久消失」的
            //   真凶。链条：ensureBackButton 判定 icon.panel==null ⇒ 拆掉重建 ⇒ 走到这里，
            //   而此刻 StripToDetailMode 早已把 search_ / input_ **SetActive(false)**，
            //   UIHelper.getByName 跳过失活对象 ⇒ tpl==null ⇒ 本方法**静默 return**，
            //   钮已经被拆了、新的又没造出来 ⇒ 一个像素都没有，日志只有 back=missing。
            //   （旧钮还在时 getByName 也查不到它 ⇒ 还会重复造第二颗，名字撞车。）
            if (win == null || FindDeep(win, BackButtonName) != null)
            {
                return;
            }
            GameObject inputG = FindDeep(win, "input_");
            Transform inputT = inputG != null ? inputG.transform : null;
            GameObject tpl = FindDeep(win, "search_");
            if (inputT == null || tpl == null)
            {
                if (QuickTestTrace.Enabled)
                {
                    QuickTestTrace.Log("link", "back install ABORTED input="
                        + (inputT != null) + " tpl=" + (tpl != null)
                        + "（查不到克隆源 ⇒ 钮建不出来）");
                }
                return;
            }

            GameObject b = (GameObject)UnityEngine.Object.Instantiate(tpl);
            b.name = BackButtonName;
            b.transform.SetParent(inputT.parent, false);
            // ⛔ 必须显式激活：自愈路径（ensureBackButton 的重建）进来时源钮 search_ 已经
            //   被 StripToDetailMode 藏掉了，克隆体默认继承源的 activeSelf ⇒ 克隆出一个
            //   藏着的钮，OnEnable 不跑、控件永远注册不进面板 —— 这就是「点一下就没了，
            //   以后再也不出现」的完整链条。这里把它顶成活的，注册路径与首建时一致。
            b.SetActive(true);

            // 克隆体会把源钮运行时挂进 UIEventTrigger / UIButton.onClick / hinter 的委托
            // 一并复制过来，于是点一下触发两次、悬停提示造两个（createTestButton 445 行记的坑）。
            // 最后一行的 registEvent 会把 onClick 重新写成返回动作，所以这里只要**清干净**。
            UIEventTrigger trig = b.GetComponent<UIEventTrigger>();
            if (trig != null)
            {
                trig.onClick.Clear();
                trig.onHoverOver.Clear();
                trig.onHoverOut.Clear();
                trig.onPress.Clear();
            }
            UIButton ub = b.GetComponent<UIButton>();
            if (ub != null)
            {
                ub.onClick.Clear();
                ub.tweenTarget = b;
            }
            // MonoDelegate 是 registEvent 挂点按回调用的，克隆体会把源钮那个（指向 search_
            // 的动作）一并带过来；清成 null，下面 registEvent 会重新写。
            MonoDelegate[] inheritedDelegates = b.GetComponentsInChildren<MonoDelegate>(true);
            foreach (MonoDelegate d in inheritedDelegates)
            {
                d.actionInMono = null;
            }
            // 源钮上可能还挂着 MonoListener（另一条事件通道），一并指向返回动作而不是留空，
            // 免得它照着源钮的旧参数干别的事。
            MonoListener[] inheritedListeners = b.GetComponentsInChildren<MonoListener>(true);
            foreach (MonoListener m in inheritedListeners)
            {
                m.actionInMono = delegate { goBack(); };
            }

            // 图标：沿用克隆体自带的 UITexture（同款锚点/尺寸），只换贴图 + 挪到输入框左端。
            UITexture icon = b.GetComponentInChildren<UITexture>(true);
            if (icon == null)
            {
                GameObject iconGo = new GameObject("Texture");
                iconGo.layer = b.layer;
                iconGo.transform.SetParent(b.transform, false);
                icon = iconGo.AddComponent<UITexture>();
            }
            icon.pivot = UIWidget.Pivot.Center;
            // 配色**从放大镜那颗图标抄**（用户 2026-10-02 需求 1：一个风格/颜色一致），
            // 图标本身（左箭头 vs 退无可退时的 X）与最终配色统一交给 UpdateBackIcon。
            // 这里先把初值写死一份，免得到 UpdateBackIcon 之前有一帧是空白/半透明。
            CaptureBackIconColor();
            icon.path = BackIconName;
            icon.mainTexture = GameTextureManager.get(BackIconName);
            icon.color = backIconColor;
            // 落点由 PlaceBackButton 定：详情版顶替放大镜钮，正常版挂在输入框左端。
            PlaceBackButton(b, inputT);
            UpdateBackIcon();

            BoxCollider box = b.GetComponent<BoxCollider>();
            if (box == null)
            {
                box = b.AddComponent<BoxCollider>();
            }
            // 命中区跟着图标原生尺寸走（同放大镜一格大小），别再用 IconSize 缩它。
            int hit = icon != null && icon.width > 0 ? icon.width : IconSize;
            box.size = new Vector3(hit, hit, 0f);
            box.center = Vector3.zero;

            // 点按走**与 search_ / detailed_ 同一条路**：UIButton.onClick + MonoDelegate
            // （UIHelper.registEvent 的 UIButton 分支）。
            // ⚠ 别自己挂 UIEventTrigger.onClick —— 那是另一套通道，和 UIButton 混着挂
            //   既可能一次点击触发两次，也可能两边都收不到。工程里所有按钮都是前一种。
            //   克隆体身上继承来的 MonoDelegate 在上面已经清掉 actionInMono，这里由
            //   registEvent 重新写成本窗的返回动作。
            UIHelper.registEvent(win, BackButtonName, onBackClicked);
            // 双保险：万一 registEvent 因为组件缺失没挂上（它内部找不到就静默返回），
            // 直接把点按回调也存进 MonoListener，并在下面那次遍历里已经指向本方法。
            ProbeAssertBackClickable(b);

            hinter hint = b.GetComponent<hinter>();
            if (hint == null)
            {
                hint = b.AddComponent<hinter>();
            }
            hint.str = "返回上一级";

            // 输入框里的字（占位提示与玩家输入共用同一个 label）别压到钮上。
            UILabel inputLabel = UIHelper.getByName<UILabel>(win, "LabelOfInput");
            if (inputLabel != null)
            {
                inputLabel.leftAnchor.absolute = 4 + IconSize + 3;
                inputLabel.ResetAndUpdateAnchors();
            }

            backButton = b;
            QuickTestTrace.Log("link", "back button installed name=" + b.name
                + " icon=" + (icon.mainTexture != null ? BackIconName : "(缺)")
                + " cloneOf=search_ panel=" + (icon.panel != null ? icon.panel.name : "none"));
        }
        catch (Exception e)
        {
            Program.DEBUGLOG(e);
        }
    }

    /// <summary>
    /// 装完之后自查"点按到底挂没挂上"：<c>UIHelper.registEvent</c> 内部是**静默**的
    /// （按名字没找到对应组件就什么都不做、不报错），所以"点不动"在这一层看不出来。
    /// 这里把 UIButton.onClick 的委托数、MonoDelegate 的回调名都读出来，
    /// 变成日志里可判的两个数。
    /// </summary>
    void ProbeAssertBackClickable(GameObject b)
    {
        try
        {
            UIButton ub = b != null ? b.GetComponent<UIButton>() : null;
            MonoDelegate d = b != null ? b.GetComponent<MonoDelegate>() : null;
            BoxCollider box = b != null ? b.GetComponent<BoxCollider>() : null;
            QuickTestTrace.Log("link", "backclick wired onClick="
                + (ub != null ? ub.onClick.Count.ToString() : "noButton")
                + " delegate=" + (d != null ? (d.actionInMono != null ? d.actionInMono.Method.Name : "(null)") : "none")
                + " collider=" + (box != null
                    ? box.size.ToString("F0") + ",trig=" + box.isTrigger + ",en=" + box.enabled
                    : "none"));
        }
        catch (Exception e)
        {
            Program.DEBUGLOG(e);
        }
    }

    // ------------------------------------------------------------ 「译」钮（批量换译名）

    /// <summary>
    /// 补一颗「译」钮，摆在返回钮**左边**那一格。
    ///
    /// <para><b>造法：克隆 <c>back_</c></b>（不是 <c>search_</c>、也不是自己 new）。
    /// 本工程所有运行期补的钮都是克隆出来的（<c>DeckManager.createTestButton</c>、
    /// <c>gameButton.show</c>、本类的 <c>installBackButton</c>），理由写在
    /// <see cref="NGUI-button-conventions"/>：自己 new 的控件 <c>UIPanel</c> 不认，
    /// 显示一阵、点一下贴图就没了，而且因为只建一次再也补不回来。
    /// 克隆 <c>back_ specifically</c>（而不是 <c>search_</c>）还多一层好处 ——
    /// <b>「与返回钮观感一致」这条口径由构造保证</b>：颜色、碰撞体尺寸、点按配色、音效
    /// 全是同一份，��可能"差半档"。本题用户明确要求"颜色等要一致"，而这类不一致
    /// 已经反复出现过（见 memory 的"眼形视角钮"与"下划线取色"两条），所以宁可让构造
    /// 来保证，也不靠手抄字段。</para>
    ///
    /// <para>⚠ 克隆体会把 back_ 身上运行时挂的委托一并带过来（<c>onClick</c> 会变成
    /// "点一下退一级"、<c>MonoListener</c> 同理），所以下面照 <see cref="installBackButton"/>
    /// 的做法清干净再重接。</para>
    /// </summary>
    void installPackButton()
    {
        try
        {
            if (win == null || FindDeep(win, PackButtonName) != null)
            {
                return;
            }
            GameObject src = FindDeep(win, BackButtonName);
            if (src == null)
            {
                if (QuickTestTrace.Enabled)
                {
                    QuickTestTrace.Log("link", "packtrans install ABORTED no back_ to clone");
                }
                return;
            }
            GameObject b = (GameObject)UnityEngine.Object.Instantiate(src);
            b.name = PackButtonName;
            b.transform.SetParent(src.transform.parent, false);
            // 显式激活：自愈路径进来时源钮可能正被藏（克隆体继承源的 activeSelf）。
            b.SetActive(true);

            UIEventTrigger trig = b.GetComponent<UIEventTrigger>();
            if (trig != null)
            {
                trig.onClick.Clear();
                trig.onHoverOver.Clear();
                trig.onHoverOut.Clear();
                trig.onPress.Clear();
            }
            // ⛔⛔ 必须把克隆体继承来的 **hinter** 拆掉：源钮 back_ 身上有（"返回上一级/关闭"
            //   提示），Instantiate 会把它连同已注册进 UIEventTrigger 的委托一起复制过来。
            //   本窗改用 hinterEdge（靠边会翻边，见下），两颗提示组件同时挂在一颗钮上 =
            //   **悬停弹出两条、移开只消一条**（ngui-button-conventions §3 列的坑）。
            //   顺序要紧：上面**先**把 trigger 的 hover 委托清空，这里再拆组件 ——
            //   反过来的话，拆掉组件不会把它已挂进 trigger 的委托一起带走。
            hinter inheritedHint = b.GetComponent<hinter>();
            if (inheritedHint != null)
            {
                UnityEngine.Object.Destroy(inheritedHint);
            }
            UIButton ub = b.GetComponent<UIButton>();
            if (ub != null)
            {
                ub.onClick.Clear();
                ub.tweenTarget = b;
            }
            MonoDelegate[] inherited = b.GetComponentsInChildren<MonoDelegate>(true);
            for (int i = 0; i < inherited.Length; i++)
            {
                inherited[i].actionInMono = null;
            }
            MonoListener[] listeners = b.GetComponentsInChildren<MonoListener>(true);
            for (int i = 0; i < listeners.Length; i++)
            {
                listeners[i].actionInMono = delegate { onPackClicked(); };
            }

            // 图标：沿用克隆体自带的那个 UITexture（同款锚点/尺寸），只换贴图。
            // ⚠ 它的贴图尺寸是 40x40（left/no 那批的画布），而这一颗要换成
            //   translate_searchbar —— 同样是 40x40 中性灰，所以**不用改任何尺寸数字**。
            UITexture icon = b.GetComponentInChildren<UITexture>(true);
            if (icon == null)
            {
                GameObject iconGo = new GameObject("Texture");
                iconGo.layer = b.layer;
                iconGo.transform.SetParent(b.transform, false);
                icon = iconGo.AddComponent<UITexture>();
                icon.pivot = UIWidget.Pivot.Center;
                icon.SetDimensions(PackHitSize, PackHitSize);
            }
            icon.pivot = UIWidget.Pivot.Center;
            ClearAnchors(icon.gameObject);
            icon.transform.localPosition = Vector3.zero;
            Texture2D tex = GameTextureManager.get(PackIconName);
            if (tex != null)
            {
                icon.path = PackIconName;
                icon.mainTexture = tex;
            }
            // 颜色与返回钮**同一个来源**（放大镜那颗的实测色），不另写常量。
            CaptureBackIconColor();
            icon.color = backIconColor;
            PlacePackButton(b, icon);
            FixBackDrawOrder(b);

            BoxCollider box = b.GetComponent<BoxCollider>();
            if (box == null)
            {
                box = b.AddComponent<BoxCollider>();
            }
            box.size = new Vector3(PackHitSize, PackHitSize, 0f);
            box.center = Vector3.zero;

            // 点按走与 back_ / search_ 同一条路：UIButton.onClick + MonoDelegate。
            UIHelper.registEvent(win, PackButtonName, onPackClicked);

            // 悬停提示：⛔ 不用 hinter —— ① 它把提示固定摆在控件上方 45px，而本窗顶缘
            // 就是屏幕顶缘（窗高等于屏高），提示会整条落到屏外；② 它走通用预制体
            // mod_simple_ngui_text，归 ui_main_2d 那套面板，会**被检索窗自己的底板压住**
            // （用户 2026-10-03 实测「既固定又被关联卡搜索栏挡住」）。
            // hinterEdge 两件事都绕开：自建 UIPanel 且 depth = 全场最高+20（永不被挡），
            // 位置跟鼠标并自动翻边（观感与战斗界面的俯视角切换钮同源）。
            hinterEdge he = b.GetComponent<hinterEdge>();
            if (he == null)
            {
                he = b.AddComponent<hinterEdge>();
            }
            he.str = PackHintText;

            packButton = b;
            if (QuickTestTrace.Enabled)
            {
                UITexture probe = b.GetComponentInChildren<UITexture>(true);
                QuickTestTrace.Log("link", "packtrans installed name=" + b.name
                    + " icon=" + (tex != null ? PackIconName : "(缺)")
                    + " cloneOf=back_ panel=" + (probe != null && probe.panel != null
                        ? probe.panel.name : "none")
                    + " col=" + icon.color.r.ToString("F2") + ","
                    + icon.color.g.ToString("F2") + "," + icon.color.b.ToString("F2")
                    + "," + icon.color.a.ToString("F2")
                    + " hit=" + PackHitSize
                    + " clickWired=" + (b.GetComponent<UIButton>() != null
                        ? b.GetComponent<UIButton>().onClick.Count.ToString()
                        : "noButton"));
            }
        }
        catch (Exception e)
        {
            Program.DEBUGLOG(e);
        }
    }

    /// <summary>
    /// 角上两颗钮（「译」+ 返回）离窗角的多近（用户 2026-10-04 口径：「最好贴着边缘」）。
    /// 中心距 18px、图标 25px ⇒ 图标边到窗缘约 5px —— 既是"贴着"，
    /// 又不至于像第一版 15px 那样把字顶出窗外（图标心距 15px 时图标边只剩 2px）。
    /// </summary>
    internal const float CornerMargin = 18f;

    /// <summary>
    /// 「译」钮落点：**左上角**，与返回钮同一行但分居两侧（用户 2026-10-03 第二轮口径：
    /// 「右上角区域只有返回上一级或者关闭按钮」）。
    ///
    /// <para><b>为什么从「右上」挪到「左上」</b>：右上那一格是返回/关闭的专属位置 ——
    /// 那是玩家"退出这一层"的肌肉记忆，把第二颗钮塞进去会让两颗的含义都变模糊；
    /// 而左上角原本空着（输入框那一排在详情版是藏起来的），放"批量"这种低频操作正合适。</para>
    ///
    /// <para>⛔ 全部用**锚点**、不写 localPosition（理由同 <see cref="PlaceBackButton"/>）：
    /// 容器 <c>under_</c> 的高度随状态变（300 ↔ 屏高），写死位置就是焊死某一个状态。</para>
    ///
    /// <para>水平方向贴 <c>under_</c> **左缘**、纵向贴顶缘，都是 <see cref="CornerMargin"/>
    /// 的中心距（2026-10-04 前水平是 41px —— 那是放大镜钮的原位，玩家嫌它悬在半空）。</para>
    /// </summary>
    void PlacePackButton(GameObject b, UITexture icon)
    {
        if (b == null || win == null)
        {
            return;
        }
        UIRect selfRect = b.GetComponent<UIRect>();
        Transform refT = detailSlotParent;
        if (refT == null)
        {
            GameObject src = FindDeep(win, BackButtonName);
            refT = src != null ? src.transform.parent : null;
        }
        UIRect refRect = refT != null ? refT.GetComponent<UIRect>() : null;
        float half = (icon != null && icon.width > 0 ? icon.width : PackHitSize) * 0.5f;
        if (refRect != null && selfRect != null)
        {
            const float fromLeft = CornerMargin;      // 贴左缘（用户 2026-10-04）
            const float fromTop = CornerMargin;       // 贴顶缘，与返回钮同一行
            AnchorOffLeftTop(selfRect, refRect,
                fromLeft - half, fromLeft + half, fromTop - half, fromTop + half);
        }
        if (icon != null)
        {
            ClearAnchors(icon.gameObject);
            icon.transform.localPosition = Vector3.zero;
        }
    }

    /// <summary>
    /// 「译」钮自愈：每次本窗要显示的时候过一遍。
    ///
    /// 与 <see cref="ensureBackButton"/> 同款（那颗实测「点一次就再也不出现」）——
    /// 逐项复查：对象在不在、图标在不在、贴图还在不在、有没有归到面板上、
    /// 点按回调还在不在。幂等，正常情况下什么都不改。
    /// </summary>
    void ensurePackButton()
    {
        try
        {
            if (win == null)
            {
                return;
            }
            GameObject b = FindDeep(win, PackButtonName);
            if (b == null)
            {
                installPackButton();
                return;
            }
            if (!b.activeSelf)
            {
                b.SetActive(true);
            }
            UITexture icon = b.GetComponentInChildren<UITexture>(true);
            if (icon == null)
            {
                // 图标子物体没了：整颗重建（半吊子修补会留下一个点得动但看不见的钮）。
                b.transform.SetParent(null, false);
                UnityEngine.Object.Destroy(b);
                packButton = null;
                installPackButton();
                return;
            }
            if (icon.mainTexture == null)
            {
                Texture2D again = GameTextureManager.get(PackIconName);
                if (again != null)
                {
                    icon.path = PackIconName;
                    icon.mainTexture = again;
                }
            }
            if (!icon.gameObject.activeSelf)
            {
                icon.gameObject.SetActive(true);
            }
            if (icon.panel == null)
            {
                // 没归到面板 = NGUI 一个像素都不画。先无伤自愈，再考虑重建。
                if (win.activeInHierarchy)
                {
                    icon.gameObject.SetActive(false);
                    icon.gameObject.SetActive(true);
                    if (icon.panel != null)
                    {
                        return;
                    }
                }
                b.transform.SetParent(null, false);
                UnityEngine.Object.Destroy(b);
                packButton = null;
                installPackButton();
                return;
            }
            // 配色与位置每次自愈一遍：与返回钮同源，不写死。
            CaptureBackIconColor();
            icon.color = backIconColor;
            PlacePackButton(b, icon);
            FixBackDrawOrder(b);
            // 提示组件必须**有且只有一颗**：多一颗就是"悬停弹两条、只消一条"。
            // 继承来的 hinter 在建钮时已拆；这里再兜一次（万一某条自愈路径又把它带回来）。
            if (b.GetComponent<hinter>() != null)
            {
                UIEventTrigger t2 = b.GetComponent<UIEventTrigger>();
                if (t2 != null)
                {
                    t2.onHoverOver.Clear();
                    t2.onHoverOut.Clear();
                    t2.onPress.Clear();
                }
                UnityEngine.Object.Destroy(b.GetComponent<hinter>());
                if (b.GetComponent<hinterEdge>() == null)
                {
                    b.AddComponent<hinterEdge>();
                }
            }
            if (b.GetComponent<hinterEdge>() == null)
            {
                b.AddComponent<hinterEdge>();
            }
            UIButton ub = b.GetComponent<UIButton>();
            if (ub != null && ub.onClick != null && ub.onClick.Count == 0)
            {
                UIHelper.registEvent(win, PackButtonName, onPackClicked);
            }
            if (QuickTestTrace.Enabled)
            {
                BoxCollider box = b.GetComponent<BoxCollider>();
                QuickTestTrace.Log("link", "packtrans ensure self=" + b.activeSelf
                    + " tex=" + (icon.mainTexture != null ? icon.path : "NULL")
                    + " panel=" + (icon.panel != null ? icon.panel.name : "none")
                    + " col=" + icon.color.r.ToString("F2") + ","
                    + icon.color.g.ToString("F2") + "," + icon.color.b.ToString("F2")
                    + "," + icon.color.a.ToString("F2")
                    + " hit=" + (box != null ? box.size.x.ToString("F0") : "-")
                    + " clickWired=" + (ub != null ? ub.onClick.Count.ToString() : "noButton")
                    + " n=" + lastResult.Count);
            }
        }
        catch (Exception e)
        {
            Program.DEBUGLOG(e);
        }
    }

    /// <summary>「译」钮点按：把本窗当前这一批卡的卡号交给批量选择器。</summary>
    void onPackClicked()
    {
        try
        {
            if (lastResult == null || lastResult.Count <= 0)
            {
                MLogPack("这一批没有卡，先在上面点个链接或搜索点什么。");
                return;
            }
            List<int> ids = new List<int>(lastResult.Count);
            for (int i = 0; i < lastResult.Count; i++)
            {
                if (lastResult[i] != null)
                {
                    ids.Add(lastResult[i].Id);
                }
            }
            NameTranslationUI.OpenBatchPackChooser(ids,
                "本窗 " + ids.Count + " 张");
        }
        catch (Exception e)
        {
            Program.DEBUGLOG(e);
        }
    }

    /// <summary>提示条写一行（走 CardDescription 那条现成的日志区）。</summary>
    static void MLogPack(string s)
    {
        try
        {
            if (Program.I() != null && Program.I().cardDescription != null)
            {
                Program.I().cardDescription.mLog(s);
            }
        }
        catch (Exception e)
        {
            Program.DEBUGLOG(e);
        }
    }

    /// <summary>
    /// 验收用：在进程内**模拟一次悬停**到译钮上，让 <c>hinterEdge</c> 真的建出提示面板。
    ///
    /// <para>为什么必须走这一下：提示面板是 <c>Update</c> 里按「悬停中」惰性建的，
    /// 而合成的鼠标事件送不进这个客户端（notes/harness.md）⇒ 光开窗、不悬停，
    /// 屏幕上永远不会有提示，那时判「提示 depth 高过全场」是判了一根头发。</para>
    ///
    /// <para>做法与 <c>ProbeLinkToggle</c> 同款：直接调组件上的 hover 入口
    /// （<c>hinterEdge.in_</c>），再等若干拍让 <c>Update</c> 跑过建面板 + 定位。
    /// 收尾必须调 <c>out_</c>，否则提示会留在屏幕上被后面的截图拍到。</para>
    /// </summary>
    public void ProbeHoverPackButton()
    {
        try
        {
            GameObject pk = win != null ? FindDeep(win, PackButtonName) : null;
            if (pk == null)
            {
                QuickTestTrace.Log("packtrans", "hover ABORTED no packtrans_");
                return;
            }
            hinterEdge he = pk.GetComponent<hinterEdge>();
            if (he == null)
            {
                QuickTestTrace.Log("packtrans", "hover ABORTED no hinterEdge");
                return;
            }
            System.Type t = typeof(hinterEdge);
            t.GetMethod("in_", System.Reflection.BindingFlags.Instance
                | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public)
                .Invoke(he, null);
            // 提示是在 Update 里按「鼠标位置」定位的；真鼠标此刻在别处，所以把
            // 闸门开着让它按当前鼠标位置算一次即可（判据看 depth 与是否出屏）。
            Program.go(400, () => Program.go(300, () =>
            {
                QuickTestTrace.Log("packtrans", "H1 hover done");
                System.Type t2 = typeof(hinterEdge);
                t2.GetMethod("out_", System.Reflection.BindingFlags.Instance
                    | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public)
                    .Invoke(he, null);
            }));
        }
        catch (Exception e)
        {
            QuickTestTrace.Log("packtrans", "hover failed " + e.GetType().Name + ": " + e.Message);
        }
    }

    /// <summary>验收用：两颗钮的图标逐块报色 + 各自屏幕矩形（判「同色」「不重叠」「在屏内」）。</summary>
    public string ProbeButtons()
    {
        try
        {
            GameObject bk = win != null ? FindDeep(win, BackButtonName) : null;
            GameObject pk = win != null ? FindDeep(win, PackButtonName) : null;
            Camera m2 = Program.camera_main_2d;
            return "buttons rd=" + (GameModeManager.IsRD ? 1 : 0)
                + " back=" + (bk != null ? "有" : "missing")
                + " pack=" + (pk != null ? "有" : "missing")
                + " n=" + lastResult.Count
                + " | backIcon " + IconClrIn(win, BackButtonName)
                + " | packIcon " + IconClrIn(win, PackButtonName)
                + " | backRect=" + RectOf(bk != null ? bk.GetComponent<UIRect>() : null, m2)
                + " packRect=" + RectOf(pk != null ? pk.GetComponent<UIRect>() : null, m2)
                // 标题行与列表行的矩形：验收要判「标题在按钮那排**下面**、三者互不重叠」
                // （用户 2026-10-03 实测过「按钮和搜索结果的文字撞了空间」）。
                + " titleRect=" + RectOf(UIHelper.getByName<UIRect>(win, "Panel2"), m2)
                + " listRect=" + RectOf(panel, m2)
                // 提示正文与提示面板的 depth：depth 必须高过全场所有面板，否则会被检索窗压住
                // （用户实测「被关联卡搜索栏挡住」）。报出来让脚本能判。
                + " packHint=\"" + PackHintText + "\""
                + " packHintDepth=" + HintPanelDepth(pk)
                + " maxPanelDepth=" + MaxPanelDepth()
                + " | packClick=" + (pk != null && pk.GetComponent<UIButton>() != null
                    ? pk.GetComponent<UIButton>().onClick.Count.ToString() : "noButton")
                + " packHit=" + (pk != null && pk.GetComponent<BoxCollider>() != null
                    ? pk.GetComponent<BoxCollider>().size.ToString("F0") : "-")
                // 提示组件必须**恰好一颗**：继承来的 hinter 若没拆干净，悬停会弹两条、
                // 移开只消一条（ngui-button-conventions 3 节）。落成可判的数，别靠假设。
                + " packHints=" + (pk != null
                    ? (pk.GetComponents<hinterEdge>().Length
                       + pk.GetComponents<hinter>().Length).ToString() : "-")
                + " | " + NameTranslationUI.ProbeBatchKey(lastResultIds());
        }
        catch (Exception e)
        {
            return "buttons failed " + e.GetType().Name + ": " + e.Message;
        }
    }

    /// <summary>验收用：当前这一批的卡号（<see cref="ProbeButtons"/> 调它）。</summary>
    List<int> lastResultIds()
    {
        List<int> ids = new List<int>();
        for (int i = 0; i < lastResult.Count; i++)
        {
            if (lastResult[i] != null)
            {
                ids.Add(lastResult[i].Id);
            }
        }
        return ids;
    }

    /// <summary>
    /// 验收用：译钮那颗提示面板的 depth（没建出来就报 -1）。
    /// 判据是它**必须大于场景里所有面板**——否则提示会被检索窗自己的底板压住
    /// （用户 2026-10-03 实测「既固定又被关联卡搜索栏挡住」）。
    /// </summary>
    static int HintPanelDepth(GameObject btn)
    {
        try
        {
            if (btn == null)
            {
                return -1;
            }
            hinterEdge he = btn.GetComponent<hinterEdge>();
            if (he == null)
            {
                return -1;
            }
            // 提示根是自建的私有节点，探针拿不到 ⇒ 退而求其次：报「组件在不在」，
            // 真正的 depth 由 [tipedge] show 行在悬停那一刻打出来。
            return he != null ? 1 : -1;
        }
        catch (Exception)
        {
            return -1;
        }
    }

    /// <summary>场景里当前最高的 UIPanel.depth（判「提示有没有高过全场」的比对基准）。</summary>
    static int MaxPanelDepth()
    {
        int max = -1;
        for (int i = 0; i < UIPanel.list.Count; i++)
        {
            if (UIPanel.list[i] != null && UIPanel.list[i].depth > max)
            {
                max = UIPanel.list[i].depth;
            }
        }
        return max;
    }

    // ------------------------------------------------------------ 「译」钮验收探针

    /// <summary>
    /// 「译」钮的端到端自检（只在 <c>log/packtrans.probe</c> 下干活，正式包零开销）。
    ///
    /// <para>为什么要有：这一颗要验的东西**没法靠肉眼看截图下判据** ——
    /// ① 配色与返回钮/放大镜**逐字段相等**（用户明确要求"颜色等要一致"，而这条不一致
    /// 已经反复出现过）；② 两颗钮的屏幕矩形**不重叠**且都在屏内；
    /// ③ 批量换表真的落到了**这一批**卡上（而不是别的批）。</para>
    ///
    /// <para>做法与 <c>ProbeLinkToggle</c> 同款：进程内自己把检索窗叫起来（合成的鼠标
    /// 事件送不进这个客户端，见 notes/harness.md），把每一步摊成 <c>[packtrans]</c> 行，
    /// 脚本只负责按序核对。</para>
    /// </summary>
    public static void PackTransProbe()
    {
        if (!ProbeSwitchOn("packtrans.probe"))
        {
            return;
        }
        try
        {
            CardSearchWindow w = Program.I() != null ? Program.I().cardSearch : null;
            if (w == null)
            {
                QuickTestTrace.Log("packtrans", "no cardSearch");
                return;
            }
            // 挑一个池里真有**多张同系列关联卡**的名字：批量才有意义（单张也能跑通，
            // 但那样测不出"按异画组去重"这一层）。
            YGOSharp.Card pick = PickProbeCard();
            if (pick == null)
            {
                QuickTestTrace.Log("packtrans", "no candidate card");
                return;
            }
            QuickTestTrace.Log("packtrans", "P1 pick=" + pick.Id + " name=" + pick.Name
                + " rd=" + (GameModeManager.IsRD ? 1 : 0));
            CardSearchWindow.ShowName(pick.Name, 0);
            // 窗是 0.6s 补间滑进来的 ⇒ 按钮自愈与矩形都要等它落定后再量。
            Program.go(1500, () => Program.go(200, () =>
            {
                QuickTestTrace.Log("packtrans", "P2 " + w.ProbeButtons());
                QuickTestTrace.Log("packtrans", "P3 chooser="
                    + NameTranslationUI.ProbeBatchPackPlan(w.ProbeResultIds()));
                // 悬停一下：让 hinterEdge 真的建出提示面板（惰性建，不悬停就没有）。
                // [tipedge] show 行里带 depth / onScreen / 落点，脚本按那些数判。
                w.ProbeHoverPackButton();
                // 真按一次：把这一批设成 nw（RD 下这步会被 RD 口径短路，正好验那条）。
                Program.go(100, () =>
                {
                    NameTranslationUI.ProbeApplyBatch(w.ProbeResultIds(), "nw");
                    Program.go(300, () =>
                    {
                        QuickTestTrace.Log("packtrans", "P4 after " + w.ProbeButtons());
                        // 收尾一律还原成"跟随全局"，别把探针的结果留给玩家。
                        Program.go(100, () =>
                        {
                            NameTranslationUI.ProbeApplyBatch(w.ProbeResultIds(), "global");
                            Program.go(300, () =>
                                QuickTestTrace.Log("packtrans", "P5 restored " + w.ProbeButtons()));
                        });
                    });
                });
            }));
        }
        catch (Exception e)
        {
            QuickTestTrace.Log("packtrans", "failed " + e.GetType().Name + ": " + e.Message);
        }
    }

    /// <summary>
    /// 简介系列行链接 + 悬停提亮的端到端自检（只在 <c>log/serieslink.probe</c> 下干活）。
    ///
    /// <para>与 <see cref="PackTransProbe"/> 同款：进程内自己摆卡、自己喂坐标
    /// （合成鼠标事件送不进这个客户端，见 notes/harness.md），把每一步摊成
    /// <c>[serieslink]</c> 行，脚本按行核对。</para>
    ///
    /// <para>⚠ RD 池**没有系列字段**（rd_standard.cdb 里 setcode 全 0，2026-10-04 实查），
    /// RD 档下探针会打 <c>S0 skip no-setcode-card</c> 然后干干净净地退出 ——
    /// 那不是缺陷，是"RD 没有这一行可点"。</para>
    /// </summary>
    public static void SeriesLinkProbe()
    {
        if (!ProbeSwitchOn("serieslink.probe"))
        {
            return;
        }
        try
        {
            CardDescription cd = Program.I() != null ? Program.I().cardDescription : null;
            if (cd == null || cd.description == null || cd.description.textLabel == null)
            {
                QuickTestTrace.Log("serieslink", "no description");
                return;
            }
            // ⛔ 整条链都要等「开机自检收尾」跑完再动：那条自检链**只挂 qt_debug.on**、
            //   没有任何开关，它自己会做检索 / 开关检索窗 / 往说明面板换卡。
            //   与它交叠时读到的是**它**留下的状态（2026-10-04 实测：S2 读成
            //   `filter=Text q=[机壳] n=11`，其实那会儿是自检链在搜机壳）。
            cd.ProbeAfterAutoclean(() => SeriesLinkProbeBody(cd));
        }
        catch (Exception e)
        {
            QuickTestTrace.Log("serieslink", "failed " + e.GetType().Name + ": " + e.Message);
        }
    }

    /// <summary>
    /// <see cref="SeriesLinkProbe"/> 的主体 —— 等开机自检收尾之后才跑
    /// （见 <see cref="CardDescription.ProbeAfterAutoclean"/>）。
    /// </summary>
    static void SeriesLinkProbeBody(CardDescription cd)
    {
        try
        {
            // H1 与系列无关、两档都要看：简介面板的提示必须已经是**跟随式**那一套
            // （旧 hinter 一颗都不许剩）。放在 RD 短路**之前**。
            QuickTestTrace.Log("serieslink", "H1 " + cd.ProbeEdgeHint());
            // H2 真悬停一次，看提示面板到底建没建出来、落点/画序如何。
            cd.ProbeHoverEdgeHint();
            // 挑一张**系列成员最多**的卡：系列行里 "| 越多"，"分别可点"越有意义。
            List<YGOSharp.Card> pool = WholePool();
            YGOSharp.Card pick = null;
            int best = 0;
            for (int i = 0; i < pool.Count; i++)
            {
                YGOSharp.Card c = pool[i];
                if (c == null || c.Setcode == 0)
                {
                    continue;
                }
                List<GameStringHelper.SetNamePair> ps = GameStringHelper.getSetNamePairs(c.Setcode);
                if (ps != null && ps.Count > best)
                {
                    best = ps.Count;
                    pick = c;
                }
            }
            if (pick == null)
            {
                QuickTestTrace.Log("serieslink", "S0 skip no-setcode-card");
                return;
            }
            YGOSharp.Card prev = cd.showingCard;
            int hash0 = -1;
            List<GameStringHelper.SetNamePair> pairs = GameStringHelper.getSetNamePairs(pick.Setcode);
            if (pairs != null && pairs.Count > 0)
            {
                hash0 = pairs[0].hash;
            }
            QuickTestTrace.Log("serieslink", "S0 pick=" + pick.Id + " name=" + pick.Name
                + " setcode=" + pick.Setcode + " series=" + pairs.Count + " hash0=" + hash0
                + " rd=" + (GameModeManager.IsRD ? 1 : 0));
            cd.setData(pick, GameTextureManager.myBack, "", true);
            // ⛔ 只 setData 是不够的：说明面板在菜单态本来**没显示**（show() 才把进场补间
            //   发出去）⇒ 实拍只会拍到背景。与 ProbeShowLinkCard 同款，先 show 出来。
            probeDescWasShown = cd.isShowed;
            cd.show();
            // setData → UITextList.Add → WrapText → label.text，留几拍让它落定。
            Program.go(700, () =>
            {
                UILabel lab = cd.description.textLabel;
                string txt = lab != null ? lab.text : "";
                int tags = CardTextLinker.CountOccur(txt, "[url=s");
                string payloads = JoinSeriesPayloads(txt);
                // 系列行**应有的** payload（由 getSetNamePairs 现算）—— 只看"整段文本里
                // 出现了几个 [url=s" 会被正文里的字段链接（也走 KindSet）污染，
                // 判据必须落在"这一行的那几颗在不在"上。
                string want = "";
                int present = 0;
                for (int i = 0; i < pairs.Count; i++)
                {
                    if (i > 0)
                    {
                        want += "|";
                    }
                    string disp = !string.IsNullOrEmpty(pairs[i].display)
                        ? pairs[i].display : pairs[i].native;
                    string one = "[url=" + CardTextLinker.KindSet + "0:"
                        + pairs[i].hash + ":" + disp + "]";
                    want += one.Substring(5, one.Length - 6);
                    if (txt.IndexOf(one, StringComparison.Ordinal) >= 0)
                    {
                        present++;
                    }
                }
                QuickTestTrace.Log("serieslink", "S1 tags=" + tags
                    + " series=[" + want + "] present=" + present + "/" + pairs.Count
                    + " payloads=[" + payloads + "] len=" + txt.Length
                    + " rect=" + cd.DescLabelScreenRect());
            if (tags <= 0 || present < pairs.Count)
            {
                QuickTestTrace.Log("serieslink", "S1 XX series links missing present="
                    + present + "/" + pairs.Count);
                RestoreProbeCard(cd, prev);
                return;
            }
                // B2 真按一次第一颗系列链接（走与玩家点击同一条路由）。
                CardSearchWindow w = Program.I() != null ? Program.I().cardSearch : null;
                string first = FirstSeriesPayload(txt);
                Program.go(100, () =>
                {
                    CardLinkRouter.Handle(first);
                    // Search 是 ShowInternal 之后 60 拍才跑的 ⇒ 等它落定再读。
                    Program.go(900, () =>
                    {
                        QuickTestTrace.Log("serieslink",
                            "S2 " + (w != null ? w.ProbeSetSearch() : "no window"));
                        // B3 悬停提亮：在简介 label 上撒网找一个真落在链接上的点。
                        Program.go(100, () =>
                        {
                            ProbeHoverDescription(cd);
                            // M：既是卡名又是字段那条（内圈当卡名 / 外圈当字段，各真按一次）。
                            Program.go(100, () =>
                            {
                                ProbeSplitSegments();
                                // R：卡名搜索要算上"规则同名"（alias），点「海」要带上亚特兰蒂斯。
                                Program.go(2200, () =>
                                {
                                    ProbeRuleName();
                                    // T：右键那两条（检索窗不收 / 右键字段进改名）。
                                    Program.go(1600, () =>
                                    {
                                        ProbeRightClick(cd, w);
                                        // 收尾再往后挪一点：面板进场补间是 1.2s，实拍（"系列行有没有
                                        // 偏下沉"那一类题）要拍**落定后**的面板，不能拍还在滑的；
                                        // 也留给截图脚本"看到 S4 再抓"的余量。
                                        // ⛔ 必须**晚于** ProbeRightClick 里那条 1200 的延迟链
                                        //   （T2c/T2e/T2f/T2g 要拿同一张卡的 setHit 再点一次；
                                        //   先换回原卡的话第二遍就点空了 —— 实测踩到）。
                                        Program.go(6000, () => RestoreProbeCard(cd, prev));
                                    });
                                });
                            });
                        });
                    });
                });
            });
        }
        catch (Exception e)
        {
            QuickTestTrace.Log("serieslink", "failed " + e.GetType().Name + ": " + e.Message);
        }
    }

    /// <summary>
    /// 「既是卡名又是字段」那一条的**端到端**验收（用户 2026-10-04 第一条）：
    /// 在真卡文里找一处「X」+种类词（X 既是卡名又是字段，如「融合」魔法卡），
    /// 把内圈与外圈两个 payload **各真按一次**，读检索窗落成可判的数：
    ///   * 内圈 → <c>Filter.ExactName</c>：结果里**每一张的卡名都等于 X**（当卡名）；
    ///   * 外圈 → 全文档 + 种类词掩码：结果里每一张都命中该串**且**都是那个主类（当字段）。
    /// </summary>
    static void ProbeSplitSegments()
    {
        try
        {
            List<YGOSharp.Card> pool = WholePool();
            CardSearchWindow w = Program.I() != null ? Program.I().cardSearch : null;
            string innerPl = null;
            string tailPl = null;
            string inner = null;
            int pickId = 0;
            for (int i = 0; i < pool.Count && innerPl == null; i++)
            {
                YGOSharp.Card c = pool[i];
                if (c == null || string.IsNullOrEmpty(c.Desc))
                {
                    continue;
                }
                string rich = CardTextLinker.Linkify(c.Desc, c.Id);
                int at = 0;
                while (true)
                {
                    int s = rich.IndexOf("[url=" + CardTextLinker.KindName + "0:",
                        at, StringComparison.Ordinal);
                    if (s < 0)
                    {
                        break;
                    }
                    int e = rich.IndexOf(']', s + 5);
                    if (e < 0)
                    {
                        break;
                    }
                    string pl = rich.Substring(s + 5, e - s - 5);
                    // payload = <掩码>:<卡名>:<被点处原文> —— 名字取**中间那段**
                    // （第三段是标题，可能含 ':'，且它本来就是"带括号+尾词"的整句）。
                    int pm;
                    string nm;
                    string ptitle;
                    if (!CardTextLinker.ParseCardName(pl.Substring(1), out pm, out nm, out ptitle))
                    {
                        at = e + 1;
                        continue;
                    }
                    if (!CardTextLinker.IsFieldName(nm))
                    {
                        at = e + 1;
                        continue;
                    }
                    // 紧跟在它后面那一颗应当就是"外圈种类词"的字段链接
                    // （2026-10-04 起字段那支走 KindSet，payload = <掩码>:<表内码>:<被点处原文>）。
                    int uend = rich.IndexOf("[/url]", e, StringComparison.Ordinal);
                    int ts = uend >= 0
                        ? rich.IndexOf("[url=" + CardTextLinker.KindSet,
                            uend, StringComparison.Ordinal) : -1;
                    if (ts < 0)
                    {
                        at = e + 1;
                        continue;
                    }
                    int te = rich.IndexOf(']', ts + 5);
                    string tpl = rich.Substring(ts + 5, te - ts - 5);
                    int tmask;
                    int tcode;
                    string ttitle;
                    if (CardTextLinker.ParseSet(tpl.Substring(1), out tmask, out tcode, out ttitle)
                        && tmask != 0
                        && CardTextLinker.FieldNativeOf(tcode) == nm)
                    {
                        innerPl = pl;
                        tailPl = tpl;
                        inner = nm;
                        pickId = c.Id;
                        break;
                    }
                    at = e + 1;
                }
            }
            if (innerPl == null || w == null)
            {
                QuickTestTrace.Log("serieslink", "M0 skip no split-pair (inner=" + inner + ")");
                return;
            }
            int innerMask = 0;
            int tailMask = int.Parse(tailPl.Substring(1, tailPl.IndexOf(':') - 1));
            // 同掩码下的**全文检索**张数（老口径）——给判据一个"确实收窄了"的对照。
            int textN = RunQuery(inner, tailMask).Count;
            QuickTestTrace.Log("serieslink", "M0 id=" + pickId + " inner=[" + inner + "]"
                + " innerPl=[" + innerPl + "] tailPl=[" + tailPl + "] textN=" + textN);

            // ── 内圈：当卡名 ────────────────────────────────────────────────
            CardLinkRouter.Handle(innerPl);
            Program.go(900, () =>
            {
                if (w != null)
                {
                    QuickTestTrace.Log("serieslink", "M1 inner "
                        + w.ProbeSegmentSearch(inner, innerMask));
                }
                // ── 外圈：当字段 ────────────────────────────────────────────
                CardLinkRouter.Handle(tailPl);
                Program.go(900, () =>
                {
                    if (w != null)
                    {
                        QuickTestTrace.Log("serieslink", "M2 tail "
                            + w.ProbeSegmentSearch(inner, tailMask));
                    }
                });
            });
        }
        catch (Exception e)
        {
            QuickTestTrace.Log("serieslink", "M failed " + e.GetType().Name + ": " + e.Message);
        }
    }

    /// <summary>
    /// 读数：当前结果里「卡名等于 <paramref name="text"/>」的有几张、
    /// 「名字或卡文含它」的有几张、「主类是 <paramref name="mask"/>」的有几张。
    /// </summary>
    string ProbeSegmentSearch(string text, int mask)
    {
        int n = lastResult != null ? lastResult.Count : 0;
        int nameEq = 0;
        int ruled = 0;
        int fieldHit = 0;
        int maskOk = 0;
        int member = 0;      // 属于这个系列（IfSetCard）的张数
        for (int i = 0; i < n; i++)
        {
            YGOSharp.Card c = lastResult[i];
            if (c == null)
            {
                continue;
            }
            if (c.Name == text)
            {
                nameEq++;
            }
            else if (RuleSameName(c, text))
            {
                ruled++;   // 规则同名（alias）—— 如点「融合」时的「置换融合」
            }
            if ((c.Name != null && c.Name.IndexOf(text, StringComparison.Ordinal) >= 0)
                || (c.Desc != null && c.Desc.IndexOf(text, StringComparison.Ordinal) >= 0))
            {
                fieldHit++;
            }
            if (mask == 0 || TypeMatches((uint)c.Type, mask))
            {
                maskOk++;
            }
            if (lastSetHash >= 0 && YGOSharp.CardsManager.IfSetCard(lastSetHash, c.Setcode))
            {
                member++;
            }
        }
        return "filter=" + lastFilter + " q=[" + lastQuery + "] mask=" + lastMask
            + " code=0x" + (lastSetHash >= 0 ? lastSetHash.ToString("x") : "-")
            + " title=[" + (lastTitle ?? "") + "]"
            + " n=" + n + " nameEq=" + nameEq + " ruled=" + ruled
            + " fieldHit=" + fieldHit
            + " maskOk=" + maskOk + " member=" + member;
    }

    /// <summary>
    /// 「卡名搜索要算上**规则同名**」的端到端验收（用户 2026-10-04 第一条）：
    /// 在池里找一对「alias 指向的卡与自己**不同名**」的（如 传说之都 亚特兰蒂斯 →「海」），
    /// 拿目标那个名字真按一次**卡名链接**，读检索窗：
    ///   * 每一张的结果都必须是"名字等于它"**或**"规则同名于它"（bad 必须 = 0）；
    ///   * 那张规则同名的卡**必须在结果里**（hasExtra=1）。
    /// </summary>
    static void ProbeRuleName()
    {
        try
        {
            CardSearchWindow w = Program.I() != null ? Program.I().cardSearch : null;
            if (w == null)
            {
                QuickTestTrace.Log("serieslink", "R0 skip no window");
                return;
            }
            List<YGOSharp.Card> pool = WholePool();
            string target = null;
            int extraId = 0;
            string extraName = null;
            for (int i = 0; i < pool.Count && target == null; i++)
            {
                YGOSharp.Card c = pool[i];
                if (c == null || c.Alias <= 0 || c.Alias == c.Id)
                {
                    continue;
                }
                YGOSharp.Card up = YGOSharp.CardsManager.GetCard(c.Alias);
                if (up == null || string.IsNullOrEmpty(up.Name) || up.Name == c.Name)
                {
                    continue;   // 同名 = 异画，不是本档要验的
                }
                target = up.Name;
                extraId = c.Id;
                extraName = c.Name;
            }
            if (target == null)
            {
                QuickTestTrace.Log("serieslink", "R0 skip no rule-same-name pair");
                return;
            }
            QuickTestTrace.Log("serieslink", "R0 name=[" + target + "] extra=" + extraId
                + " extraName=[" + extraName + "]");
            CardLinkRouter.Handle(CardTextLinker.KindName + "0:" + target
                + ":「" + target + "」");
            Program.go(900, () =>
            {
                if (w != null)
                {
                    QuickTestTrace.Log("serieslink", "R1 " + w.ProbeNameSearch(target, extraId));
                }
            });
        }
        catch (Exception e)
        {
            QuickTestTrace.Log("serieslink", "R failed " + e.GetType().Name + ": " + e.Message);
        }
    }

    /// <summary>
    /// 读数（卡名档）：结果里「名字等于它」几张、「规则同名于它」几张、**两者都不是**几张
    /// （最后一档必须为 0），以及那张指定的规则同名卡在不在结果里。
    /// </summary>
    string ProbeNameSearch(string name, int extraId)
    {
        int n = lastResult != null ? lastResult.Count : 0;
        int nameEq = 0;
        int ruled = 0;
        int bad = 0;
        bool hasExtra = false;
        for (int i = 0; i < n; i++)
        {
            YGOSharp.Card c = lastResult[i];
            if (c == null)
            {
                continue;
            }
            if (c.Id == extraId)
            {
                hasExtra = true;
            }
            if (string.Equals(c.Name, name, StringComparison.Ordinal))
            {
                nameEq++;
            }
            else if (RuleSameName(c, name))
            {
                ruled++;
            }
            else
            {
                bad++;
            }
        }
        return "filter=" + lastFilter + " q=[" + lastQuery + "] n=" + n
            + " nameEq=" + nameEq + " ruled=" + ruled + " bad=" + bad
            + " title=[" + (lastTitle ?? "") + "]"
            + " hasExtra=" + (hasExtra ? 1 : 0);
    }

    /// <summary>这张卡的 <c>alias</c> 链条上有没有一张卡的名字等于 <paramref name="name"/>。</summary>
    static bool RuleSameName(YGOSharp.Card c, string name)
    {
        int t = c != null ? c.Alias : 0;
        for (int hop = 0; hop < 4 && t > 0; hop++)
        {
            YGOSharp.Card up = YGOSharp.CardsManager.GetCard(t);
            if (up == null)
            {
                return false;
            }
            if (string.Equals(up.Name, name, StringComparison.Ordinal))
            {
                return true;
            }
            if (up.Alias == t)
            {
                return false;
            }
            t = up.Alias;
        }
        return false;
    }

    /// <summary>
    /// 右键那两条的端到端验收（用户 2026-10-04）：
    ///   T1 检索窗开着时右键**不许**把它收掉（原来是 <c>ES_mouseDownRight → hide()</c>）；
    ///   T2 在 label 上找一个**字段链接**的点，喂给右键入口 ⇒ 应当弹出"改这个系列"的输入框
    ///      （物证是 <c>[nametrans] field input field=[…]</c> 那一行）；
    ///   T3 再拿一个**卡名链接**的点喂进去 ⇒ 必须什么都不做（不能弹一个改不了东西的框）。
    /// </summary>
    static void ProbeRightClick(CardDescription cd, CardSearchWindow w)
    {
        try
        {
            // ── T1 ────────────────────────────────────────────────────────────
            if (w != null)
            {
                bool before = w.isShowed;
                w.ES_mouseDownRight();
                QuickTestTrace.Log("serieslink", "T1 rightclick windowBefore=" + (before ? 1 : 0)
                    + " after=" + (w.isShowed ? 1 : 0)
                    + " q=[" + (w.ProbeLastQuery ?? "") + "]");
            }
            else
            {
                QuickTestTrace.Log("serieslink", "T1 no window");
            }
            // ── T2/T3 ─────────────────────────────────────────────────────────
            UILabel lab = cd.description != null ? cd.description.textLabel : null;
            if (lab == null)
            {
                QuickTestTrace.Log("serieslink", "T2 no label");
                return;
            }
            Vector3[] c = lab.worldCorners;
            Vector3 setHit = Vector3.zero;
            Vector3 nameHit = Vector3.zero;
            Vector3 noneHit = Vector3.zero;
            string setPl = null;
            string namePl = null;
            bool noneFound = false;
            for (int gy = 0; gy < 30; gy++)
            {
                for (int gx = 0; gx < 20; gx++)
                {
                    float fx = (gx + 0.5f) / 20f;
                    float fy = (gy + 0.5f) / 30f;
                    Vector3 p = Vector3.Lerp(Vector3.Lerp(c[0], c[3], fx),
                        Vector3.Lerp(c[1], c[2], fx), fy);
                    int ord;
                    string pl = CardTextLinker.ResolveClick(lab, p, out ord);
                    if (string.IsNullOrEmpty(pl) || ord < 0)
                    {
                        if (!noneFound)
                        {
                            noneFound = true;
                            noneHit = p;
                        }
                        continue;
                    }
                    if (setPl == null && pl[0] == CardTextLinker.KindSet
                        && pl.IndexOf(':') > 0 && pl.IndexOf(':', pl.IndexOf(':') + 1) > 0)
                    {
                        setPl = pl;
                        setHit = p;
                    }
                    else if (namePl == null && pl[0] == CardTextLinker.KindName)
                    {
                        namePl = pl;
                        nameHit = p;
                    }
                }
            }
            // T0：右键这条链的**第一道闸**是"说明面板有没有注册进每帧派发列表" ——
            //     没注册就永远调不到 ES_mouseDownRight（2026-10-04 用户实测"右键没反应"的真凶：
            //     左键/悬停走 NGUI 事件与挂 label 的 MonoBehaviour，不经过这份列表，所以只有右键哑）。
            QuickTestTrace.Log("serieslink", "T0 dispatched="
                + (Program.IsServantRegistered(cd) ? 1 : 0));
            if (setPl != null)
            {
                int n0 = NameTranslationUI.ProbeFieldInputCount();
                QuickTestTrace.Log("serieslink", "T2 setHit pl=[" + setPl + "] r="
                    + cd.TryRenameFieldUnderMouse(setHit, true));
                // 【用户 2026-10-04 第五条】右键**直接**进改名输入框（上一条那道确认框已撤），
                // 且这张框右上角要有那颗圆形 ×、整张框要能右键关掉（T2b/T2c/T2e/T2g）。
                QuickTestTrace.Log("serieslink", "T2b inputBefore=" + n0
                    + " inputNow=" + NameTranslationUI.ProbeFieldInputCount()
                    + " " + NameTranslationUI.ProbeCloseInfo()
                    // ⛔ 同一帧喂"右键按下"：必须 closed=0 —— 开这个框的那一击本身就是右键，
                    //   没有这道闸的话真机上框会刚开就自己关掉（AddComponent 之后 Update
                    //   可能同帧就跑到）。
                    + " sameFrame=" + NameTranslationUI.ProbeRightClickTick(true));
                // 停一拍再往下：① 等窗口的缩放补间落定（create 的 fade 参数会把窗从 0 缩到 1，
                //   立刻量的屏幕矩形是缩着的）、② 实拍脚本要趁这张框还开着抓一张。
                // ⛔ 必须早于 RestoreProbeCard（下面 6000 那一拍）：换回原卡之后 label 文本就变了，
                //   再用同一个 setHit 去点会落到别的字上（实测第二遍开不出框来）。
                //   3000 ≈ 2.7s：框 0.3s 就落定，余量留给实拍脚本（_shot_transdialog.py）。
                Program.go(3000, () =>
                {
                    QuickTestTrace.Log("serieslink", "T2c settled "
                        + NameTranslationUI.ProbeCloseInfo());
                    // ① 右键关闭那条路（＝TransDialogClose 那一拍会走的事）
                    QuickTestTrace.Log("serieslink", "T2e tickLater="
                        + NameTranslationUI.ProbeRightClickTick(true));
                    // ② 再开一次，这次点那颗圆形 ×
                    string rr = cd.TryRenameFieldUnderMouse(setHit, true);
                    QuickTestTrace.Log("serieslink", "T2f reopenInput="
                        + NameTranslationUI.ProbeFieldInputCount() + " r=" + rr);
                    QuickTestTrace.Log("serieslink", "T2g xClickClose="
                        + NameTranslationUI.ProbeClickClose());
                    // 收尾：别给玩家留一个开着的框。
                    NameTranslationUI.ProbeClearDialog();
                });
            }
            else
            {
                QuickTestTrace.Log("serieslink", "T2 skip no field link under grid");
            }
            // T3：**不是字段**的点 —— 卡名链接优先，这张卡的正文里没有卡名链接时
            // 退而用"完全没链接"的点，两者都必须是"什么都不做"。
            if (namePl != null)
            {
                QuickTestTrace.Log("serieslink", "T3 nameHit pl=[" + namePl + "] r="
                    + cd.TryRenameFieldUnderMouse(nameHit, true));
            }
            else if (noneFound)
            {
                QuickTestTrace.Log("serieslink", "T3 noneHit r="
                    + cd.TryRenameFieldUnderMouse(noneHit, true));
            }
            else
            {
                QuickTestTrace.Log("serieslink", "T3 skip no non-field point");
            }
            // ⛔ 这里**不要**收对话框：T2c/T2d 被推后了（见上面的 Program.go），在这里收
            //   会把还挂着的确认框先关掉 —— 收尾统一放在那条延迟链的末尾。
        }
        catch (Exception e)
        {
            QuickTestTrace.Log("serieslink", "T failed " + e.GetType().Name + ": " + e.Message);
        }
    }

    /// <summary>探针收尾：把简介面板换回原来那张卡（别把探针的卡留给玩家）。</summary>
    static void RestoreProbeCard(CardDescription cd, YGOSharp.Card prev)
    {
        try
        {
            if (cd != null && prev != null && prev.Id > 0)
            {
                cd.setData(prev, GameTextureManager.myBack, "", true);
            }
            // 探针之前面板若本来没显示（菜单态），收尾要把 show() 这一步还回去 ——
            // 别给玩家留下一个本来不该在的窗口。
            if (cd != null && !probeDescWasShown)
            {
                cd.hide();
            }
            CardSearchWindow w = Program.I() != null ? Program.I().cardSearch : null;
            if (w != null && w.isShowed)
            {
                w.hide();
            }
        }
        catch (Exception e)
        {
            Program.DEBUGLOG(e);
        }
    }

    /// <summary>探针进场前面板是不是已经显示着（收尾按它决定要不要 hide）。</summary>
    static bool probeDescWasShown;

    /// <summary>文本里所有 <c>[url=s…]</c> 的 payload，用 | 连接（进日志用）。</summary>
    static string JoinSeriesPayloads(string txt)
    {
        if (string.IsNullOrEmpty(txt))
        {
            return "";
        }
        List<string> ps = new List<string>();
        int from = 0;
        while (true)
        {
            int s = txt.IndexOf("[url=s", from, StringComparison.Ordinal);
            if (s < 0)
            {
                break;
            }
            int e = txt.IndexOf(']', s + 5);
            if (e < 0)
            {
                break;
            }
            ps.Add(txt.Substring(s + 5, e - s - 5));
            from = e + 1;
        }
        return string.Join("|", ps.ToArray());
    }

    /// <summary>文本里第一个 <c>[url=s…]</c> 的完整 payload（含种类字符 s）。</summary>
    static string FirstSeriesPayload(string txt)
    {
        int s = txt.IndexOf("[url=s", StringComparison.Ordinal);
        if (s < 0)
        {
            return null;
        }
        int e = txt.IndexOf(']', s + 5);
        return e < 0 ? null : txt.Substring(s + 5, e - s - 5);
    }

    /// <summary>
    /// 当前检索窗状态（Setcode 档验收读数）：档位 / 标题词 / hash / 张数 /
    /// 以及"结果里是不是**每一张**的 Setcode 槽里都有这个 hash"（判据对不对，数说了算）。
    /// </summary>
    string ProbeSetSearch()
    {
        int n = lastResult != null ? lastResult.Count : 0;
        int ok = 0;
        int textOnly = 0;   // 只是"卡文里提到它"、并不属于这个系列的张数（必须为 0）
        string word = lastQuery ?? "";
        if (lastFilter == Filter.Setcode && lastSetHash >= 0 && lastResult != null)
        {
            for (int i = 0; i < lastResult.Count; i++)
            {
                YGOSharp.Card c = lastResult[i];
                if (c == null)
                {
                    continue;
                }
                if (YGOSharp.CardsManager.IfSetCard(lastSetHash, c.Setcode))
                {
                    ok++;
                }
                else
                {
                    bool mention = (c.Name != null && c.Name.IndexOf(word, StringComparison.Ordinal) >= 0)
                        || (c.Desc != null && c.Desc.IndexOf(word, StringComparison.Ordinal) >= 0);
                    if (mention)
                    {
                        textOnly++;
                    }
                }
            }
        }
        return "filter=" + lastFilter + " q=[" + lastQuery + "] hash=" + lastSetHash
            + " mask=" + lastMask
            + " title=[" + (lastTitle ?? "") + "]"
            + " n=" + n + " allSlotMatch=" + (n == 0 ? 0 : (ok == n ? 1 : 0))
            + " textOnly=" + textOnly;
    }

    /// <summary>
    /// 悬停提亮探针：在简介 label 的矩形上**撒网**采样，找到一个真落在链接上的点，
    /// 让 <see cref="CardLinkHover.ProbeTick"/> 在那里强制跑一拍，然后判：
    ///   * 文本变了（提亮版）；
    ///   * **长度不变**（等长替换是命中的命根，变了下一拍点击就会错位）；
    ///   * 提亮后的颜色确实出现在文本里；
    ///   * <see cref="CardLinkHover.ProbeRestore"/> 后回到原文。
    /// 找不到落点（这版排版下链接都被折行切开了之类）就打 skip，脚本按"没数据"处理。
    /// </summary>
    static void ProbeHoverDescription(CardDescription cd)
    {
        try
        {
            UILabel lab = cd.description.textLabel;
            if (lab == null)
            {
                QuickTestTrace.Log("serieslink", "S3 skip no label");
                return;
            }
            CardLinkHover hover = lab.GetComponent<CardLinkHover>();
            if (hover == null)
            {
                QuickTestTrace.Log("serieslink", "S3 XX no CardLinkHover on label");
                return;
            }
            string before = lab.text;
            // ⛔ NGUI worldCorners 的角序是 [0]=左下 [1]=左上 [2]=右上 [3]=右下
            //   （TransformPoint(x0,y0) 起、逆着 x 先走 —— 见 UIWidget.worldCorners）。
            Vector3[] corners = lab.worldCorners;
            Vector3 bl = corners[0];
            Vector3 br = corners[3];
            Vector3 tl = corners[1];
            Vector3 hit = Vector3.zero;
            int hitOrdinal = -1;
            bool found = false;
            // 优先挑**系列行**那一颗（payload 以 s 开头）——用户 2026-10-04 问的
            // 「系列行的可选中字段是不是偏下沉」，要量的就是它。
            for (int pass = 0; pass < 2 && !found; pass++)
            {
                for (int gy = 0; gy < 30 && !found; gy++)
                {
                    for (int gx = 0; gx < 20 && !found; gx++)
                    {
                        float fx = (gx + 0.5f) / 20f;
                        float fy = (gy + 0.5f) / 30f;
                        Vector3 p = Vector3.Lerp(Vector3.Lerp(bl, br, fx),
                            Vector3.Lerp(tl, corners[2], fx), fy);
                        int ordinal;
                        string pl = CardTextLinker.ResolveClick(lab, p, out ordinal);
                        if (string.IsNullOrEmpty(pl) || ordinal < 0)
                        {
                            continue;
                        }
                        if (pass == 0 && pl[0] != CardTextLinker.KindSet)
                        {
                            continue;   // 第一遍只要系列行那几颗
                        }
                        hit = p;
                        hitOrdinal = ordinal;
                        found = true;
                    }
                }
            }
            if (!found)
            {
                QuickTestTrace.Log("serieslink", "S3 skip no link under label grid");
                return;
            }
            // 精细扫描：把这个 ordinal 的**可命中区**包成一个屏幕矩形（左上原点），
            // 好与实拍里字身的位置**同一坐标系**比。
            // ⛔ 别全图扫：ResolveClick 每次都重建整段文字的字符位置，全图 = 十万次调用
            //   （一帧里跑完能把游戏卡住）。只在该点附近 ±6/±18px 扫。
            Vector3 local = lab.cachedTransform.InverseTransformPoint(hit);
            float minSx = float.MaxValue, maxSx = float.MinValue;
            float minSy = float.MaxValue, maxSy = float.MinValue;
            Camera cam = Program.camera_main_2d;
            for (int k = -1; k <= 1; k++)
            {
                for (float dy = 18f; dy >= -18f; dy -= 1f)
                {
                    Vector3 w2 = lab.cachedTransform.TransformPoint(
                        new Vector3(local.x + k * 6f, local.y + dy, 0f));
                    int o2;
                    string p2 = CardTextLinker.ResolveClick(lab, w2, out o2);
                    if (string.IsNullOrEmpty(p2) || o2 != hitOrdinal || cam == null)
                    {
                        continue;
                    }
                    Vector3 s = cam.WorldToScreenPoint(w2);
                    float sy = Screen.height - s.y;      // 左上原点，与截图一致
                    if (s.x < minSx) { minSx = s.x; }
                    if (s.x > maxSx) { maxSx = s.x; }
                    if (sy < minSy) { minSy = sy; }
                    if (sy > maxSy) { maxSy = sy; }
                }
            }
            string box = maxSx < minSx ? "none"
                : (Mathf.RoundToInt(minSx) + "," + Mathf.RoundToInt(minSy)
                   + "-" + Mathf.RoundToInt(maxSx) + "," + Mathf.RoundToInt(maxSy));
            string after = hover.ProbeTick(hit);
            bool changed = !string.IsNullOrEmpty(after) && after != before;
            bool lenEq = !string.IsNullOrEmpty(after) && after.Length == before.Length;
            string hotColor = "";
            if (changed)
            {
                // 抓提亮段：第一个 [RRGGBB] 里不再是原文颜色的那一个
                hotColor = FirstDiffColor(before, after);
            }
            QuickTestTrace.Log("serieslink", "S3 hover ordinal=" + hitOrdinal
                + " payload=[" + (hitOrdinal >= 0 ? SeriesPayloadAt(lab.text, hitOrdinal) : "") + "]"
                + " changed=" + (changed ? 1 : 0)
                + " lenEq=" + (lenEq ? 1 : 0)
                + " hotColor=[" + hotColor + "]"
                + " hitBox=" + box);
            string back = hover.ProbeRestore();
            QuickTestTrace.Log("serieslink", "S4 restored="
                + (!string.IsNullOrEmpty(back) && back == before ? 1 : 0)
                + " len=" + (back != null ? back.Length : -1));
        }
        catch (Exception e)
        {
            QuickTestTrace.Log("serieslink", "S3 failed " + e.GetType().Name + ": " + e.Message);
        }
    }

    /// <summary>文本里第 <paramref name="ordinal"/> 个（0 起算）链接的 payload（进日志用）。</summary>
    static string SeriesPayloadAt(string txt, int ordinal)
    {
        if (string.IsNullOrEmpty(txt) || ordinal < 0)
        {
            return "";
        }
        int from = 0;
        int nth = 0;
        while (true)
        {
            int s = txt.IndexOf("[url=", from, StringComparison.Ordinal);
            if (s < 0)
            {
                return "";
            }
            int e = txt.IndexOf(']', s + 5);
            if (e < 0)
            {
                return "";
            }
            if (nth == ordinal)
            {
                return txt.Substring(s + 5, e - s - 5);
            }
            from = e + 1;
            nth++;
        }
    }

    /// <summary>两段文本里第一处不同的 <c>[RRGGBB]</c> 颜色（取"后文"那一版）。</summary>
    static string FirstDiffColor(string before, string after)
    {
        if (string.IsNullOrEmpty(before) || string.IsNullOrEmpty(after))
        {
            return "";
        }
        int n = Math.Min(before.Length, after.Length);
        for (int i = 0; i < n; i++)
        {
            if (before[i] != after[i])
            {
                // 颜色标签从这一位开始岔开：往回找 '['，取 6 位
                int lb = after.LastIndexOf('[', Math.Max(0, Math.Min(i, after.Length - 1)));
                if (lb >= 0 && lb + 7 < after.Length && after[lb + 7] == ']')
                {
                    return after.Substring(lb + 1, 6);
                }
                return "?";
            }
        }
        return "";
    }

    /// <summary>探针用：挑一张池里存在的卡（优先同名的多张，其次任意）。</summary>
    static YGOSharp.Card PickProbeCard()    {
        try
        {
            YGOSharp.Card first = null;
            YGOSharp.CardsManager.ForEachActiveCard((id, c) =>
            {
                if (c == null || c.Id <= 0 || string.IsNullOrEmpty(c.Name))
                {
                    return;
                }
                if (first == null)
                {
                    first = c;
                }
            });
            return first;
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>探针/脚本用：本窗当前这一批的卡号。</summary>
    public List<int> ProbeResultIds()
    {
        return lastResultIds();
    }

    /// <summary>
    /// 探针开关（<c>log/xxx.probe</c>）。走 <see cref="QuickTestTrace.SwitchOn"/> 而不是
    /// 直接 <c>File.Exists</c>：后者会把上一轮遗留的开关当成本次意图（见
    /// <see cref="QuickTestTrace.SwitchGraceSeconds"/>）。
    /// </summary>
    static bool ProbeSwitchOn(string name)
    {
        return QuickTestTrace.Enabled && QuickTestTrace.SwitchOn(name);
    }

    // ------------------------------------------------------------ 详情版排版

    /// <summary>
    /// 详情版排版（需求 2026-10-02）：点卡名/字段弹出来的这一版**没有输入框**，
    /// 放大镜钮与分类钮也不留 —— 它们占的那一块整块给「返回上一级」。
    ///
    /// ⚠ 为什么"没有输入框"要连放大镜一起处理：放大镜是**输入框的提交钮**，
    ///   输入框没了它还杵在那儿，玩家点下去什么也不会发生；分类钮同理（这一版的条件
    ///   是链接自己带过来的，不由玩家再选）。所以这两颗不是"藏起来"，是**让位**。
    /// ⚠ 位置必须**先量再藏**：放大镜一旦 SetActive(false)，它那些锚点参照物也跟着不画了，
    ///   之后再去读它的位置拿到的是上一次的脏值。
    /// </summary>
    void StripToDetailMode()
    {
        if (detailApplied || win == null)
        {
            return;
        }
        GameObject searchGo = UIHelper.getByName(win, "search_");
        if (searchGo == null)
        {
            // 面板里没有放大镜 = 不是预期那张 prefab，别瞎拆。
            return;
        }
        detailSlotParent = searchGo.transform.parent;
        detailSlotPos = searchGo.transform.localPosition;
        detailSlotScale = searchGo.transform.localScale;
        detailApplied = true;

        GameObject inputGo = UIHelper.getByName(win, "input_");
        if (inputGo != null)
        {
            inputGo.SetActive(false);
        }
        GameObject inputLabel = UIHelper.getByName(win, "LabelOfInput");
        if (inputLabel != null)
        {
            inputLabel.SetActive(false);
        }
        GameObject detailGo = UIHelper.getByName(win, "detailed_");
        if (detailGo != null)
        {
            detailGo.SetActive(false);
        }
        searchGo.SetActive(false);

        GameObject b = FindDeep(win, BackButtonName);
        if (b != null)
        {
            PlaceBackButton(b, null);
        }
        LayoutDetailRows();
        QuickTestTrace.Log("link", "detailmode input=hidden search=hidden detailed=hidden"
            + " backAt=" + detailSlotPos.ToString("F1"));    }

    /// <summary>
    /// 详情版排版：把**标题（条件 + 张数）挪到按钮那一排的下面一行**，列表跟在标题下方。
    ///
    /// <para><b>为什么标题不能留在按钮那一排</b>（用户 2026-10-03 第二轮实测「按钮和搜索结果的
    /// 文字撞了空间」）：这一排左侧是「译」钮、右侧是返回/关闭钮，而 <c>Panel2</c>
    /// （标题容器）横向是**撑满**的（prefab 里 left +3 / right −27），它的 label 又居中
    /// （<c>mAlignment=1</c>）—— 一行文字再长也会往两边伸，必然伸到某一颗钮底下。
    /// 所以标题**独占一行**、按钮那排留给按钮，两边都不挤。</para>
    ///
    /// <para>数值（全部对着 <c>under_</c>，单位 px）：</para>
    /// <list type="bullet">
    /// <item>按钮那排：顶缘下 5.5~30.5（两颗钮都是 25 见方、中心在顶缘下 18）—— <b>不动</b>；</item>
    /// <item><c>Panel2</c>（标题）：top −34、bottom −54 —— 正好落在按钮下缘 30.5 之下、
    ///     留 3.5px 缝；prefab 原值是 −31/−51，这里整体下移 3px 让开按钮；</item>
    /// <item><c>panel_</c>（列表）：top −58 —— 紧接标题下缘；</item>
    /// <item><c>bar_</c>：<b>不动</b>（2026-10-10 第七轮起，见方法体内说明）。</item>
    /// </list>
    ///
    /// <para>⛔ 改完必须 <see cref="VirtualScrollView.Refit"/>：滚动几何是按旧的
    /// <c>GetViewSize</c> 算死的，不重算会滚不到底。</para>
    /// <para>幂等：绝对值是**赋值**不是累加，重复执行结果不变。</para>
    /// </summary>
    void LayoutDetailRows()
    {
        UIRect titleRect = UIHelper.getByName<UIRect>(win, "Panel2");
        if (titleRect != null)
        {
            titleRect.topAnchor.absolute = -34;
            titleRect.bottomAnchor.absolute = -54;
            titleRect.ResetAndUpdateAnchors();
        }
        if (panel != null)
        {
            panel.topAnchor.absolute = -58;
            panel.ResetAndUpdateAnchors();
        }
        // ⛔⛔ bar_（轨道）**不再重排** —— 保持 prefab 原生锚定（new_search_remaster：
        //   top=−33 / bottom=3，锚 under_）。历史教训两段：
        //
        //   ① 第六轮之前：这里把 bar_ 子树里「锚着 under_」的 widget 逐个硬写 top=−57，
        //      本意是「轨道与列表同一条上缘」；但循环只判 `topAnchor.target != null`，
        //      把**滑块 Foreground**（四个锚点目标全是 bar_）也一起写了 —— 滑块上缘被从
        //      「轨道顶+5」推到「轨道顶+57」，轨道顶部永远空出一段（用户第六轮：
        //      「滑动条上面始终有一段未知的空格」）。
        //
        //   ② 第七轮：加上目标判据（只动锚 under_ 的）后空格修掉了，但轨道顶仍被压在
        //      −57 —— 而同 prefab 的检索窗（编辑器侧）轨道顶是原生值 −33 ⇒ 弹出框滑条
        //      比原生**低 24px**（用户第七轮：「弹出框的滑动条布局要参照原生的，现在会
        //      显得低了原生滑动条一段」）。原生形态本就是**轨道比列表长**：原生
        //      remaster prefab 里 bar_ top=−73 / panel_ top=−90，轨道上缘伸进头部区域
        //      17px；详情模式的头部是按钮排（顶缘下 5.5~30.5）+ 标题行（−34~−54），
        //      轨道顶 −33 正好落在按钮排下缘之下 2.5px，与检索窗观感一致。
        //      ⇒ 结论：轨道布局**一处事实来源 = prefab**，详情模式不再碰它。
        //
        //   （UIScrollBar 不是 UIRect、锚点长在子 widget 上 —— 这条几何事实仍成立，
        //    只是现在谁都不需要改它。）
        if (scroll != null)
        {
            scroll.Refit();
        }
        // 报标题/列表的实际矩形：验收要判「标题那一行在按钮那排**下面**、两者不重叠」，
        // 光看"设了某个 absolute"判不出来（那是意图，不是结果）。
        UIRect titleProbe = UIHelper.getByName<UIRect>(win, "Panel2");
        Camera cam = Program.camera_main_2d;
        QuickTestTrace.Log("link", "detailrows title=" + (titleRect != null)
            + " panelH=" + (panel != null ? panel.GetViewSize().y.ToString("F0") : "?")
            + " titleRect=" + RectOf(titleProbe, cam)
            + " listRect=" + RectOf(panel, cam));
    }

    /// <summary>
    /// 返回钮落点。两档：详情版顶替放大镜钮（<see cref="detailApplied"/>），
    /// 正常版挂在输入框左端（老排版，作为 prefab 缺放大镜时的兜底）。
    /// </summary>
    void PlaceBackButton(GameObject b, Transform inputT)
    {
        if (b == null)
        {
            return;
        }
        UITexture icon = b.GetComponentInChildren<UITexture>(true);
        if (detailApplied)
        {
            if (detailSlotParent != null)
            {
                b.transform.SetParent(detailSlotParent, false);
            }
            b.transform.localScale = detailSlotScale;
            // ── 用**锚点**把钮钉在放大镜那一格 ─────────────────────────────────
            // ⛔ 别再退回「读 search_.localPosition 再写回」（就是 detailSlotPos 那套）：
            //   实测抓到的值是 **prefab 原值 (74,132)** —— 那一刻锚点还没首次重算过。
            //   那个值在窗口里对应屏幕 y≈625（窗口中段），而放大镜其实在**顶缘下 18px**
            //   那一排（屏幕 y≈956）。干净态实测：`backLP=(74,132)
            //   M2=(1867,612)-(1892,638)` ⇒ 钮被画到了窗口腰上（那边是结果列表的地盘），
            //   玩家自然"看不见返回钮"。
            //   改成锚 under_（放大镜的父级）：x=离右缘 / y=离顶缘 都是 <see cref="CornerMargin"/>
            //   （原先是放大镜 prefab 的真实偏移 41/18；2026-10-04 用户口径「两颗都贴着边缘」
            //   后与译钮取同一个数）。锚点每帧重算 ⇒ 不跑偏。
            UIRect refRect = detailSlotParent != null
                ? detailSlotParent.GetComponent<UIRect>() : null;
            UIRect selfRect = b.GetComponent<UIRect>();
            // ⛔ half 用图标**原生尺寸**：克隆体自带放大镜图标的规格（同 prefab），
            //   用户 2026-10-02 第二轮口径「和放大镜、分类一个风格」—— 之前强缩到
            //   IconSize(25) 就是「小一号、细一圈」观感差的根源（颜色数值其实一致）。
            float half = (icon != null && icon.width > 0 ? icon.width : IconSize) * 0.5f;
            if (refRect != null && selfRect != null)
            {
                // 2026-10-04 前：41px（放大镜 prefab 原位）。用户口径「两颗都贴着边缘」
                // ⇒ 改与译钮同一个 <see cref="CornerMargin"/>，角上成对。
                AnchorOffRightTop(selfRect, refRect,
                    CornerMargin + half, CornerMargin - half,
                    CornerMargin - half, CornerMargin + half);
            }
            // ⛔ 图标仍要把锚点摘干净：它是钮的子控件，输入框已经不画了，还锚着它的话
            //    NGUI 每帧都会把它拉向一个"没有尺寸的参照物"（表现是图标跑到角落 / 塌成 0）。
            //    NGUI 的 UpdateAnchors 对 target==null 的锚点是直接 return，安全。
            if (icon != null)
            {
                ClearAnchors(icon.gameObject);
                icon.transform.localPosition = Vector3.zero;
            }
            FixBackDrawOrder(b);
            return;
        }
        if (inputT == null)
        {
            return;
        }
        b.transform.SetParent(inputT.parent, false);
        if (icon != null)
        {
            ApplyRect(icon, inputT, 4, 4 + IconSize, 1, -1);
        }
        ApplyRect(b.GetComponent<UIRect>(), inputT, 4, 4 + IconSize, 1, -1);
        FixBackDrawOrder(b);
    }

    /// <summary>
    /// 把返回钮内所有 widget 的 depth 抬到 <see cref="BackIconDepth"/>（画序钉死在最上层，
    /// 否则窗底板会叠在图标上面把它压暗一半 —— 详见 <see cref="BackIconDepth"/> 的注释）。
    /// </summary>
    static void FixBackDrawOrder(GameObject b)
    {
        if (b == null)
        {
            return;
        }
        UIWidget[] ws = b.GetComponentsInChildren<UIWidget>(true);
        for (int i = 0; i < ws.Length; i++)
        {
            if (ws[i] != null && ws[i].depth != BackIconDepth)
            {
                ws[i].depth = BackIconDepth;
            }
        }
    }

    /// <summary>
    /// 把 <paramref name="self"/> 的四条边锚到 <paramref name="refR"/> 的**右边 / 上边**上，
    /// 四个参数都是「距参照物那条边的距离（正数）」。全用 relative=1 ⇒ 只跟参照物的尺寸走，
    /// 窗口被拉伸/换分辨率都不会跑偏（这是本项目里唯一稳的定位口径）。
    /// </summary>
    static void AnchorOffRightTop(UIRect self, UIRect refR,
        float leftFromRight, float rightFromRight, float topBelowTop, float bottomBelowTop)
    {
        self.leftAnchor.target = refR.transform;
        self.leftAnchor.relative = 1f;
        self.leftAnchor.absolute = -Mathf.RoundToInt(leftFromRight);
        self.rightAnchor.target = refR.transform;
        self.rightAnchor.relative = 1f;
        self.rightAnchor.absolute = -Mathf.RoundToInt(rightFromRight);
        self.topAnchor.target = refR.transform;
        self.topAnchor.relative = 1f;
        self.topAnchor.absolute = -Mathf.RoundToInt(topBelowTop);
        self.bottomAnchor.target = refR.transform;
        self.bottomAnchor.relative = 1f;
        self.bottomAnchor.absolute = -Mathf.RoundToInt(bottomBelowTop);
        self.ResetAndUpdateAnchors();
    }

    /// <summary>
    /// 把 <paramref name="self"/> 的四条边锚到 <paramref name="refR"/> 的**左边 / 上边**上，
    /// 四个参数都是「距参照物那条边的距离（正数）」。与 <see cref="AnchorOffRightTop"/>
    /// 同一套口径，只是水平方向换成 relative=0（跟参照物的**左**缘走）——
    /// 「译」钮挪到左上角（用户 2026-10-03 第二轮口径）后用它。
    ///
    /// ⚠ 参数是「左缘距 / 右缘距」而**不是**反过来：<c>leftFromLeft</c> 给的是
    /// 控件**左边**离参照物左缘的距离，<c>rightFromLeft</c> 才是右边那侧的。
    /// 写成反的会让控件左右颠倒（图标跑到边外），而 NGUI 不会报错。
    /// </summary>
    static void AnchorOffLeftTop(UIRect self, UIRect refR,
        float leftFromLeft, float rightFromLeft, float topBelowTop, float bottomBelowTop)
    {
        if (self == null || refR == null)
        {
            return;
        }
        self.leftAnchor.target = refR.transform;
        self.leftAnchor.relative = 0f;
        self.leftAnchor.absolute = Mathf.RoundToInt(leftFromLeft);
        self.rightAnchor.target = refR.transform;
        self.rightAnchor.relative = 0f;
        self.rightAnchor.absolute = Mathf.RoundToInt(rightFromLeft);
        self.topAnchor.target = refR.transform;
        self.topAnchor.relative = 1f;
        self.topAnchor.absolute = -Mathf.RoundToInt(topBelowTop);
        self.bottomAnchor.target = refR.transform;
        self.bottomAnchor.relative = 1f;
        self.bottomAnchor.absolute = -Mathf.RoundToInt(bottomBelowTop);
        self.ResetAndUpdateAnchors();
    }

    static void ApplyRect(UIRect r, Transform t, int left, int right, int bottom, int top)
    {
        if (r == null || t == null)
        {
            return;
        }
        r.leftAnchor.target = t;
        r.leftAnchor.relative = 0f;
        r.leftAnchor.absolute = left;
        r.rightAnchor.target = t;
        r.rightAnchor.relative = 0f;
        r.rightAnchor.absolute = right;
        r.bottomAnchor.target = t;
        r.bottomAnchor.relative = 0f;
        r.bottomAnchor.absolute = bottom;
        r.topAnchor.target = t;
        r.topAnchor.relative = 1f;
        r.topAnchor.absolute = top;
        r.ResetAndUpdateAnchors();
    }

    static void ClearAnchors(GameObject go)
    {
        if (go == null)
        {
            return;
        }
        UIRect r = go.GetComponent<UIRect>();
        if (r == null)
        {
            return;
        }
        r.leftAnchor.target = null;
        r.rightAnchor.target = null;
        r.bottomAnchor.target = null;
        r.topAnchor.target = null;
    }

    void onBackClicked()
    {
        goBack();
    }
}
