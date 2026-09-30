using UnityEngine;

/// <summary>
/// 俯视角取景与手牌排的**唯一**度量层（2026-09-28「俯视角重做 v3」）。
///
/// <para><b>为什么单独一个类</b>：旧俯视角把度量散成 <c>topDownCoverZ / topDownPanZ /
/// topDownPanRestZ / topDownPanMaxZ / topDownPanNeed / topDownPanAuto / topDownPanUserTook /
/// topDownPanStep / topDownPanMargin / topDownRowOuter / topDownHandMaxScale / topDownHandBand</c>
/// 十三个静态量，它们之间的相互关系被 13 轮用户反馈反复改坏（一半返工都出在这些量之间）。
/// 现在全部收进这里，且**只返回数值**：不持 GameObject、不做 MonoBehaviour、不写相机。
/// 相机姿态的消费点仍然只有 <see cref="Program.tableauCameraPosition"/> /
/// <see cref="Program.tableauCameraRotation"/> 一处。</para>
///
/// <para><b>本文件最关键的一条：手牌行位置是常量，取景由它反算</b>。
/// 旧口径把行位置写成 <c>(cover − 0.5) − 2s</c> —— 也就是**由取景反算手牌位置**，
/// 于是手牌外沿恒等于 <c>CoverZ − 0.5</c>，屏上余量恒为 0.5 世界，**与 CoverZ 取什么值无关**。
/// 把 CoverZ 调大只会让盘面更小、手牌离盘面更远；调小则手牌出屏 —— 需求②在旧结构里
/// **结构性地无解**。现在反过来：先定 <see cref="HandRowAbs"/>（贴着盘面外沿、留一道缝），
/// 再由 <see cref="HandOuterAbs"/> + <see cref="ScreenMargin"/> 推出 <see cref="CoverZ"/>。</para>
///
/// <para><b>关态纪律</b>：所有消费点一律写成
/// <c>if (!Program.topDownLike) return &lt;60° 原值&gt;;</c> —— 逐字段、逐调用点核对。
/// 本类只在俯视角分支被调用。</para>
///
/// <para>⛔ 字段纪律：本类是 <c>static class</c>，**没有一个实例字段**，
/// 不会改变任何已烘焙资源的序列化布局（MEMORY 红线 13）。</para>
/// </summary>
public static class TableauLayout
{
    // ── 常量：每一个都要能指到一行定义（MEMORY 判据铁律 ⑰）────────────────

    /// <summary>基准卡的**世界**宽 / 长（卡 prefab 烘焙值，平铺时 3 × 4，厚度 0.15）。</summary>
    public const float CardW = 3f;
    public const float CardH = 4f;
    /// <summary>基准卡的半长 = <see cref="CardH"/> ÷ 2。</summary>
    public const float CardHalf = 2f;

    /// <summary>
    /// 手牌 / 摊开行的显示倍数。60° 时手牌靠在相机跟前、透视把它放大到牌桌牌的 **1.9×**；
    /// 正俯视没有透视 ⇒ 不放大手牌就与牌桌上的牌一样大。取 **1.5**：刻意比 1.9 收一点，
    /// 别喧宾夺主（notes topdown §2.3 的定案，本轮沿用）。
    /// </summary>
    // 🔑 2026-09-29：1.5 → **1.79**，卡宽对齐**常规视角**。
    //   用户原话：「为什么要把卡宽缩小了？**我没允许你这么干**！而且现在空位大了应该考虑把卡做的和常规视角一样大」。
    //   ⚠ 我之前把「卡变小」当成「几何零和的必然代价」接受了 —— 那是越权，本轮改回来。
    //   解过程（不猜）：`3s × px每世界 = 95.6`，而 `px每世界 = 493 / CoverZ`、
    //   `CoverZ = HandOuterAbs / 0.91`、`HandOuterAbs = MatHalfZ + HandGap + 3s = 19.8 + 3s`
    //   ⇒ `1345.9 s / (19.8 + 3s) = 95.6` ⇒ **s = 1.79**。
    //   ⇒ 带来的代价（不藏）：`HandSpreadK` 自动变 8.63→**8.49**（不与卡组/额外列穿模），
    //   行内节距 ≈106px vs 卡宽 95.6px ⇒ 卡之间只剩 **≈10px** 缝。
    // 🔑 2026-09-29：1.5 → **1.96**，卡宽对齐**常规视角**。
    //   用户原话：「为什么要把卡宽缩小了？**我没允许你这么干**！而且现在空位大了应该考虑把卡做的和常规视角一样大」。
    //   ⚠ 我之前把「卡变小」当成「几何零和的必然代价」接受了 —— 那是越权，本轮改回来。
    //
    //   解过程（第一版我把 `CardHalfS` 当成 `3s`，实际是 `CardHalf × s = 2s` ⇒ 算出 1.79，
    //   实测卡宽只有 89.4px，差 6.2px）：
    //     `px每世界 = 493 / CoverZ`、`CoverZ = HandOuterAbs / 0.91`、
    //     `HandOuterAbs = MatHalfZ + HandGap + 2·CardHalf·s = 19.8 + 4s`、卡宽 = `3s × px每世界`
    //     ⇒ `1345.9 s / (19.8 + 4s) = 95.6` ⇒ **s = 1.96**。
    //   ⇒ `HandSpreadK` 自动变 8.63→**6.26**（不与卡组/额外列穿模），
    //   行内节距 ≈99.5px vs 卡宽 95.4px ⇒ 卡之间只剩 **≈4px**。⚠ 很紧，但卡宽是你要的。
    // 🔑 2026-09-30：1.96 → **1.80**（用户：「手牌大小可以适当压缩」）。
    //   与「扩大场地」联动：手牌外沿 = MatHalfZ + HandGap + 2·CardHalf·s，
    //   手牌卡半长超越小→外沿越靠近场地→整块内容越紧凑→取景可以放大。
    //   实测：卡宽 95.5→**92.3px**（60° 基准 95.6 的 96.6%）、
    //   场地屏上高 571→**601px**（+5.3%）、边距 44→**31.5px**。
    //   节距/卡宽比不变（K=10 → 1.667，与 60° 一致）。
    public const float HandScale = 1.80f;

    /// <summary>
    /// 手牌排**内沿 ↔ 盘面外沿**的缝（世界单位）。
    /// 60° 原生是 4.55 世界（但那是透视，屏上 ≈100px）；现役俯视是 3.20 世界（65px）。
    /// 收到 <b>1.40</b>（28px）＝ 与摊开行距 <see cref="RowPitch"/>×s 留出的缝（1.5 世界）
    /// 观感一致 ⇒ 手牌行 / 摊开行 / 盘面三者的缝是**同一个数**，视觉节奏统一。
    /// </summary>
    // 🔑 2026-09-29：1.4 → **2.2**，为的是**预留出阶段提示行那条带**。
    //   俯视下 gameField.label（「T1 我方的主要阶段1」）按用户要求放在**手牌上方**，
    //   而「手牌上方」就是 MatHalfZ 与手牌内沿之间那条缝。
    //   实测字高 21px = 1.17 世界 ⇒ 缝 1.4 世界（25px）时上下只剩 **4px**，太挤。
    //   ⇒ 缝给到 2.2 世界（≈38px）⇒ 字上下各留 ≈8px：
    //   **相对顺序对、缝也真的是空的**（用户原话：「要预留出原本就有的空间」）。
    //   ⚠ 代价（几何零和）：HandRowAbs 22.0→22.8、CoverZ 27.47→28.35、
    //   px/世界 18.00→17.39（卡宽 80.8→78.2px）。
    // ⭐ 2026-09-30 用户「场地做不到 70% 就做到上限」⇒ 2.2 → **1.5**。
    //   `HandOuterAbs = MatHalfZ + HandGap + 4s`（MatHalfZ=17.6、s=1.80 ⇒ 底线 24.80）、
    //   `CoverZ = HandOuterAbs/(1−2p)`、占屏比 = 半半径/CoverZ
    //   ⇒ CoverZ 28.85 → **28.10**，卡格 50.6% → **52.0%**、纹理 61.0% → **62.6%**。
    // ⛔ 为什么停在 1.5 而不是 0（0 才是几何上限 55.1%）：**`HandGap` 还是「阶段提示行」的槽** ——
    //   那行字屏上 24px，要 `HandGap × px每世界 ≥ 28` 才放得下（用户 2026-09-29 明确要
    //   「提示行放到卡牌上方并预留出空间」）。`HandGap=0` ⇒ 提示行无处可放。
    //   ⇒ 1.5 是「保需求③ + 提示行 + 需求②的 31px 边距」三者同时成立下的上限。
    // ⓘ 边距**两侧天然对称**（`HandOuterAbs` 取 ±），所以用户「优先对手的、自己的也差不多对称」
    //   这条自动满足，不需要额外分支。
    public const float HandGap = 1.5f;

    /// <summary>
    /// 手牌排**外沿 → 屏幕上下沿**的留白（世界单位）。这是需求②唯一要动的旋钮。
    /// ⛔ 它是**设计值**；验收判据不咬它，咬的是 <see cref="MarginScreenPct"/> 那条下限。
    /// </summary>
    /// <summary>
    /// 手牌外沿到屏幕上下沿的留白，**以世界单位表示**。🔑 v3 返工：**由屏高比例反算**，不再是常量。
    ///
    /// <para>⛔⛔ 为什么不能是常量（2026-09-28 实测踩到）：屏上留白的**像素**值 =
    /// <c>(CoverZ − HandOuterAbs) × (屏高/2) / CoverZ</c>。若 <see cref="ScreenMargin"/> 是**世界常量**，
    /// 那么任何让 <see cref="CoverZ"/> 变大的改动（这轮把手牌行基准从格线 14.6 抬到贴图 17.6，
    /// CoverZ 24.35 → 27.47）都会**按比例缩小 px/世界**，留白的像素值跟着掉 ——
    /// 实测从 47px 掉到 **43px**，**跌破需求②自己定的 4.5% 下限 44px**。
    /// 几何零和（§1.1）在这里同样成立：留白与「占屏高」是此消彼长的，
    /// 只有把留白**定义成屏高比例**才两头都对。</para>
    ///
    /// <para>⇒ <c>ScreenMargin = CoverZ × 2 × <see cref="MarginScreenPct"/></c>，
    /// 于是 <c>CoverZ = HandOuterAbs / (1 − 2 × MarginScreenPct)</c>（见 <see cref="CoverZ"/>）。
    /// 验算：HandOuterAbs 25.0 ⇒ CoverZ 27.47、留白 2.47 世界 × 17.95 px/世界 = <b>44.4px</b>
    /// ＝ 屏高 986 的 4.5% ✅（与 CoverZ 取多少无关）。</para>
    /// </summary>
    public static float ScreenMargin
    {
        get { return CoverZ * 2f * MarginScreenPct; }
    }

    /// <summary>
    /// 需求②的**验收下限**：手牌外沿到屏幕上下沿 ≥ 屏高 × 这个比例。
    /// 屏高 986px ⇒ 44.4px。⛔ 别把它和 <see cref="ScreenMargin"/> 混用：后者是设计值（会随
    /// 手牌放大倍数变），前者是与「屏」有关的物理下限。
    //   用户原话：「场地又太小了，可以**合理压缩一下边距**以扩大场地」。
    //   压边距 ⇒ `CoverZ = HandOuterAbs / (1 − 2p)` 变小 ⇒ px/世界变大 ⇒ 场地变大。
    //   ⚠ 这个数下限是用户改口径的（原前提过一次“边距太极限了”），
    //   判据的下限一并跟着调。
    public const float MarginScreenPct = 0.032f;

    /// <summary>摊开相邻两行的节距，**以 <see cref="HandScale"/> 为倍数**：行距 = 5 × s = 7.5。</summary>
    // 🔑 2026-09-30：5 → **6.5**。从 `mid_pan.png` 实拍看出的根因：
    //   行距 5s = 9.0 世界 ≈ 153px，而「卡高 7.2 世界 ≈ 123px + 卡下那行字 ≈ 40px」= 163px **大于** 153px
    //   ⇒ 字压在下一排卡上。一个根因同时解释了三个现象：
    //     · 深度打架 ⇒ **卡组字闪烁**（2026-09-29 报的，当时没定位到根因）
    //     · 最外一排被切 ⇒ **卡组等字看不到**
    //     · 悬停放大后的细烁 ⇒ **字很模糊**（TMP 字图集被放大）
    //   改成 6.5s = 11.7 世界 ≈ 200px ⇒ 每排有 200px，裸需 163px，剩 37px 缝。
    public const float RowPitch = 6.5f;

    /// <summary>滚轮一格挪多少世界（只在摊开时有效）。</summary>
    public const float PanStep = 5f;

    /// <summary>
    /// 盘面（场地外沿）的 |z|。**格位唯一来源是 <c>Ocgcore.get_point_worldposition()</c>**
    /// （网线画在贴图里 ⇒ 坐标表与 <c>texture/duel/newfield*.png</c> 必须同改）。
    /// 这里取的是「贴图里最外那条格线的位置」，与旧 <c>topDownHandBand()</c> 用的 14.6 同一个数。
    /// RD 的怪兽/魔陷区只有 3 列 ⇒ 盘面更浅（11.5）。
    /// </summary>
    public const float BoardHalfZOCG = 14.6f;
    public const float BoardHalfZRD = 11.5f;

    // ── 由常量推出（不做模式分支的地方一律走这些）──────────────────────────

    public static float BoardHalfZ
    {
        get { return GameModeManager.IsRD ? BoardHalfZRD : BoardHalfZOCG; }
    }

    /// <summary>
    /// **场地贴图本身**（不是格位）的 |z| 半高 —— v3 返工新增（2026-09-28 用户报「手牌最好不要遮挡场地」）。
    ///
    /// <para>ⓘ <b>为什么不能拿 <see cref="BoardHalfZ"/> 当手牌的内沿基准</b>：
    /// 14.6 是「最外那条**格线**」的位置，也就是**牌堆锚点**那一圈。可是**贴图比格线大**——
    /// `newfield*.png` 是 999×800，四周还有一圈外框/留白，整块铺在更大的一个面上。
    /// 实测（俯视截图 + `[view] proj` 标尺，20.246 px/世界）：贴图下沿落在 z ≈ −17.4，
    /// 比牌堆锚点还外 2.8 世界（≈57px）。⇒ 用 14.6 排手牌，手牌**内沿**（z = −16）
    /// 就在贴图下沿**上面** 1.4 世界（≈28px）⇒ **手牌压在场地边框上**（用户报的就是这个）。</para>
    ///
    /// <para>⚠ 换 <c>size_</c>（<c>fieldSize</c>）时贴图与格位一起缩放，这个数也要跟着缩；
    /// 这里按本轮实测值（<c>fieldSize = 1.210</c>）写死，与既有 <see cref="BoardHalfZOCG"/>
    /// 同样是「OCG 定值」的做法，RD 另给。</para>
    /// </summary>
    public const float MatHalfZOCG = 17.6f;

    /// <summary>
    /// 🔑 **RD 的场地下沿实测 = 17.6**（2026-09-30 改；原值 14.5 是错的，用户 2026-09-30 报障
    /// 「俯视角 RD 手牌和场地重合了」）。
    ///
    /// <para><b>怎么量的</b>：实拍 RD 俯视截图里，格线描边的颜色是 <c>rgb(71,143,231)</c>（不是 OCG 那个
    /// <c>rgb(15,68,176)</c>，别用错颜色去量），取下半屏「连续 ≥250px 的长横线」——
    /// 实测最底一条在**屏幕行 833**，而 [view] proj 标尺给的 px/世界 = 19.89、盘心在行 492
    /// ⇒ 格线底边 = (833−492)/19.89 = <b>17.15 世界</b>。</para>
    ///
    /// <para><b>对照 OCG</b>：同样量法得 <b>17.16</b>，而 <see cref="MatHalfZOCG"/> = 17.6
    /// （比实测高 0.44，留一点余量）。⇒ <b>两张场地图的格线外沿其实一样深</b>，
    /// 只是 RD 那个值当初写成了 14.5（大约是「RD 盘面比 OCG 浅」这句话的字面值，
    /// **没量贴图**）⇒ 手牌内沿 14.5+1.5 = 16.0 落在格线 17.15 **里面** 1.15 世界
    /// （实测 22px）⇒ 手牌压住格线。</para>
    ///
    /// <para>⇒ 这里改成与 OCG 同值 17.6。副作用（都算得出来、且都验过）：
    /// <c>HandRowAbs(RD) 19.6→22.7</c>、<c>CoverZ(RD) 24.79→28.10</c>（与 OCG 同量级）、
    /// px/世界 19.89→17.55 ⇒ 盘面占屏高 58.5%→**61.0%**（仍在 55~66% 带内，
    /// 而且这才是「整块格线真的都在屏内」的那份数——改之前的 58.5% 量的是
    /// <c>MatHalfZ=14.5</c>，没算上被手牌切掉的那一截）。</para>
    ///
    /// <para>⛔ OCG 一个数都没动。</para>
    /// </summary>
    public const float MatHalfZRD = 17.6f;

    /// <summary>场地贴图的 |z| 半高（按模式分支）。手牌行的**内沿**必须排在它之外。</summary>
    public static float MatHalfZ
    {
        get { return GameModeManager.IsRD ? MatHalfZRD : MatHalfZOCG; }
    }

    /// <summary>
    /// 手牌排**单侧**的横向铺开半宽，单位是「<see cref="HandScale"/> 的倍数」K
    /// （<c>get_left_right_indexEnhanced(-K·s, +K·s, …)</c> 里的那个 K）。
    ///
    /// <para>🔑 <b>2026-09-29 定稿：K 恒等于 10</b>，**不再由 <c>BoardHalfX</c> 推</b>。
    /// 用户原话：「<b>卡间距要调整好</b>」。</para>
    ///
    /// <para>⛔⛔ <b>我 2026-09-28 把它绑到 <c>BoardHalfX/HandScale − 1.5</c> 上，是个错误</b>，
    /// 错在两处：</para>
    /// <para>① <b>那个「穿模」是我误判的</b>。卡组/额外那两列在 |x| ∈ [15.92, 18.92]，
    /// 而它们所在的 **z 是 −14.6（屏上 y≈729）**；手牌排在 **z ≈ −23.7（屏上 y≈822）** ——
    /// <b>两者 z 就不一样、屏上纵向差 90px，根本不会相交</b>。我却按 x 去判「穿模」。
    /// ⇒ 那个约束本来就不该存在。</para>
    /// <para>② <b>更要命的是它把「间距比」也一起改了</b>。实测 60° 的
    /// <c>节距/卡宽 = 159.2/95.6 = 1.665</c>，而 K=10 时正好是这个比
    /// （节距 <c>= K·s/2</c>、卡宽 <c>= 3s</c> ⇒ <c>K/6 = 1.667</c>）——
    /// <b>原来的 K=10 间距比本来就是对的</b>。我按 x 收窄到 K≈6.3 之后，
    /// 卡宽放大到 95.5px、节距却只剩 99.5px ⇒ <b>卡间只剩 4px</b>（用户报「间距要调整好」）。</para>
    ///
    /// <para>⇒ 定稿：<b>K 固定 10</b>，间距比回到 60° 的 1.667 ⇒ 卡间 ≈64px，与常规视角同比例。
    /// 代价：手牌排变宽（5 张时中心跨度 4×159 = 636px，屏上 x≈1038±318），
    /// 左端会伸到左栏面板那一带 —— 但那一带在 y 822~942 上**没有可见内容**
    /// （左栏文字止于 y≈640），与 60° 的观感一致。</para>
    /// </summary>
    public static float HandSpreadK
    {
        get { return 10f; }
    }

    /// <summary>手牌 / 摊开行里**一张卡**的半长（世界）= <see cref="CardHalf"/> × <see cref="HandScale"/>。</summary>
    public static float CardHalfS
    {
        get { return CardHalf * HandScale; }
    }

    /// <summary>
    /// 盘面 10 列的**横向**外沿 |x|。OCG 一列 3.04 世界 × 10 列 = 30.4 ⇒ 半 15.2，**列间零缝**。
    /// RD 只有 3 列 ⇒ 9.12。
    /// ⛔ 与 <see cref="BoardHalfZ"/> 一样是「格线」那一圈，**不含**卡组/额外那两列
    /// （那两列更外，|x| ≈ 17.4~18.1）。<see cref="HandSpreadK"/> 正是靠这一点把手牌排
    /// 收窄到不与它们相交。
    /// </summary>
    public const float BoardHalfXOCG = 15.2f;
    public const float BoardHalfXRD = 9.12f;

    /// <summary>盘面横向外沿 |x|（按模式分支）。</summary>
    public static float BoardHalfX
    {
        get { return GameModeManager.IsRD ? BoardHalfXRD : BoardHalfXOCG; }
    }

    /// <summary>
    /// 手牌排行心的 |z| ＝ **场地贴图**外沿 ＋ 缝 ＋ 卡半长。
    ///
    /// <para>🔑 v3 返工：基准从 <see cref="BoardHalfZ"/>（14.6，格线/牌堆那一圈）改成
    /// <see cref="MatHalfZ"/>（17.6，**贴图本身**的外沿）。理由见 <see cref="MatHalfZ"/>：
    /// 贴图比格线大 2.8 世界，用 14.6 排 ⇒ 手牌**内沿**压在场地边框上（用户 2026-09-28 报障）。
    /// ⛔ **需求②不受影响**：下留白量是 <c>CoverZ − HandOuterAbs</c>，而 CoverZ 仍由
    /// <c>HandOuterAbs + ScreenMargin</c> 推出 ⇒ 恒等于 <see cref="ScreenMargin"/>，
    /// 抬高基准只是让整块内容一起变小、纵向零和照旧（§1.1 的那条账）。</para>
    /// </summary>
    public static float HandRowAbs
    {
        get { return MatHalfZ + HandGap + CardHalfS; }
    }

    /// <summary>手牌排**外沿**的 |z|（＝ 屏上那条要留 <see cref="ScreenMargin"/> 的边）。</summary>
    public static float HandOuterAbs
    {
        get { return HandRowAbs + CardHalfS; }
    }

    /// <summary>
    /// 正俯视要装下的**地面 z 半跨度**。相机高度由它反算：
    /// <c>height = CoverZ / tan(fov/2)</c>，屏上尺度 = <c>Screen.height / (2·CoverZ)</c> px/世界
    /// （与 fov、与 viewport rect 都无关 —— 这是「正俯视下 y=0 平面是纯缩放」的推论）。
    /// </summary>
    public static float CoverZ
    {
        // 🔑 v3 返工：解 <c>CoverZ = HandOuterAbs + ScreenMargin</c> 与
        //   <c>ScreenMargin = CoverZ × 2 × MarginScreenPct</c> 这两条联立方程（否则是循环依赖）：
        //   ⇒ <c>CoverZ(1 − 2p) = HandOuterAbs</c>。
        // ⛔ 这一步是需求②能**在任何缩放下都成立**的关键：留白按屏高比例定义（见
        //   <see cref="ScreenMargin"/> 的注释），而不是按世界单位钉死。
        get { return HandOuterAbs / (1f - 2f * MarginScreenPct); }
    }

    // ── 取景状态：只三个量（旧口径是十三个）────────────────────────────────

    /// <summary>
    /// 取景沿桌面纵深的**平移**（世界单位；负 = 看向自己这一侧）。
    /// 正俯视（pitch 90 / yaw 0）下沿 z 平移相机**既不改高度也不改朝向**
    /// ⇒ 屏上所有卡的大小一个像素都不变，只是取景窗口在桌面上滑动。
    /// ⛔ 两侧手牌行对称 ⇒ 内容的几何中点**必然是 0** ⇒ 静息态恒 0。
    /// 旧口径那个 <c>topDownPanRestZ = −2.0</c> 是第 5 轮「默认机位压到最底下」的补丁，
    /// 实测（S0）**底边多赚 40px、顶边赔掉 40px ⇒ 对方手牌被切出屏 30px**。
    /// 任何非零的静息机位都是一边赚一边赔，不可能在两侧同时满足需求②。
    /// </summary>
    public static float PanZ { get; set; }

    /// <summary>
    /// 当前行带里**最外那一行**的 z（**带符号**；由 <c>Ocgcore.realize</c> 每拍按实测行 z 写入）。
    /// 摊开我方卡堆那一行在 −z 侧、摊开对方的在 +z 侧 ⇒ 必须带符号，否则「弹过去」会往反方向。
    /// </summary>
    public static float RowOuter { get; set; }

    /// <summary>用户**手动滚过没有**（本状态段里）。滚过就把控制权交给玩家，每帧跟随停手。</summary>
    public static bool PanUserTook { get; set; }

    /// <summary>
    /// 取景沿桌面**横向**的平移（世界单位）。静息态 = **0**。
    ///
    /// <para><b>v3 返工（2026-09-28）：这里曾经是 8，那是错的。</b>
    /// 用户报「俯视角的整个棋盘和手牌都过度偏向左侧了」。查下来左偏**根本不是这里造成的** ——
    /// <c>PanX = 8</c> 只会让内容**更往左**（正俯视下 camera.x 变大 ⇒ 世界原点落得更靠屏幕左边）。
    /// 真正的元凶是 <c>Program.reMoveCam(getScreenCenter())</c>：它把
    /// <c>camera_game_main.rect</c> 设成 <c>(val, 0, 1, 1)</c>，而
    /// <c>getScreenCenter() = (Screen.width + cardDescription.width − gameInfo.width) / 2</c>
    /// 是**按 2D 面板宽度算出来**的居中点。本机 <c>gameInfo</c> 比 <c>cardDescription</c>
    /// 宽 80px ⇒ <c>getScreenCenter() ≈ 920</c>（屏幕中线 960）⇒ 视口整体左移 <b>80px</b>。</para>
    ///
    /// <para>ⓘ 那句「让 3D 盘面居中于左右两个 2D 面板之间」是**60° 时代**的口径：斜视角下 3D 区域
    /// 确实被两个面板夹着，偏一点才对。正俯视下 3D 内容**横跨整屏**，再偏 80px
    /// 就是用户说的「过度偏左」。⇒ 需求①那句「两者没有任何互相影响」在这里同样成立：
    /// **2D 面板的宽度不参与俯视取景。**</para>
    ///
    /// <para>⇒ 两处一起改：① 这里 = <b>−4.3</b>；② 俯视下由
    /// <c>Program.fixALLcamerasPreFrame</c> 每帧把视口强制拉回整屏
    /// （<see cref="tableauViewportFullScreen"/>）+ `Ocgcore.preFrameFunction` 不再把
    /// <c>getScreenCenter()</c> 那个 2D 面板居中点喂给 <c>reMoveCam</c>。</para>
    ///
    /// <para>⚠ <b>−4.3 是量出来的，不是推出来的</b>：用户 2026-09-28 第二次报「俯视角的场地还是比
    /// 正常视角偏左」。上一轮把 <c>PanX</c> 归 0 之后，实测俯视手牌排中点落在 x=960（**正中**），
    /// 而同一局**正常视角**的实测中点是 x≈1037 ⇒ 差 77px ⇒ 要对齐就得
    /// <c>PanX = (960 − 1037)/px每世界 = −77/17.95 = −4.3</c>。
    /// ⛔ 之前两次都推错：先归咎 <c>PanX=8</c>（它只会让内容更左），再归咎视口居中（那是 −80px 的
    /// 另一个来源，已单独修掉）。**第三次改成量**，判据也改成直接对账两个视角的中点。</para>
    ///
    /// <para>⚠ px/世界 随 <see cref="CoverZ"/> 变（这轮 20.25 → 17.95），所以这个数**跟着
    /// <c>CoverZ</c> 走**才对；写死 −4.3 是按本轮 <c>CoverZ=27.47</c> 算的，换手牌行基准要重算。
    /// 判据（`_probe_topdown.py`）咬的是**两个视角的中点之差 ≤ 40px**，不是这个常量本身。</para>
    /// </summary>
    public const float PanX = -4.3f;

    /// <summary>
    /// 俯视角下把 3D 相机的**视口**强制拉回整屏，抵消 <c>reMoveCam</c> 那套 2D 面板居中。
    /// 见 <see cref="PanX"/> 的注释：60° 时代「盘面居中于左右两个 2D 面板之间」是对的，
    /// 正俯视下 3D 内容横跨整屏，偏 80px 就是「过度偏左」。
    /// ⛔ 只改 <c>rect</c>（投影窗口），**不改相机位置与朝向** ⇒ 不动 v3 的取景几何，
    /// 也**不改卡的大小**（视口平移是屏幕空间的）。关态一个数都不动。
    /// </summary>
    public static void tableauViewportFullScreen()
    {
        if (!Program.topDownLike)
        {
            return;
        }
        Rect full = new Rect(0f, 0f, 1f, 1f);
        if (Program.camera_game_main != null && Program.camera_game_main.rect != full)
        {
            Program.camera_game_main.rect = full;
        }
        if (Program.camera_container_3d != null && Program.camera_container_3d.rect != full)
        {
            Program.camera_container_3d.rect = full;
        }
        if (Program.camera_main_3d != null && Program.camera_main_3d.rect != full)
        {
            Program.camera_main_3d.rect = full;
        }
    }

    /// <summary>
    /// ⛔⛔ **已停用**（2026-09-30）：俯视阶段大字的落点常量 —— **位置已回到「与正常视角一致」**。
    ///
    /// <para><b>这件事来回改过三轮，坑全留在这儿</b>（免得日后有人照旧注释又接回去）：</para>
    /// <para>① <b>2026-09-28</b>（v3 需求③O1）定成 <c>new Vector3(38.6f, 0f, 0f)</c>，
    /// 理由 =「俯视下盘面正中会压住阶段条 + 魔陷区那一行」⇒ 挪到「右侧空列」。</para>
    /// <para>② <b>2026-09-30 第一次修</b>用错了坐标空间：这张大字是 <c>ui_main_2d</c> 下的
    /// <b>UI 元素</b>，世界坐标活在 <b>2D UI 相机</b>那个空间，而 UIRoot 的缩放让
    /// <b>1 世界单位 ≈ 493 像素</b>（3D 盘面相机只有 17.5~19.9 像素/世界）—— 两者不是一回事。
    /// 于是 38.6 落到 <c>screen.x = 960 + 38.6×493 ≈ 19989</c>（屏宽 1920）
    /// ⇒ <b>整条飞出屏幕右侧，OCG 与 RD 两档都看不到这张字</b>
    /// （物证：<c>_verify_viewbtn/ptext_{ocg,rd}.png</c>）。
    /// ⛔ 顺带订正旧注释里那句「落点是世界坐标 ⇒ 与 CoverZ/PanX 无关，不会漂」——
    /// 它<b>忽略了这个对象活在另一个相机空间</b>。</para>
    /// <para>③ <b>2026-09-30 第二次修</b>改成屏幕坐标，落点 screen.x=1520（离右侧按钮列 205px），
    /// 数字上没问题了，<b>但用户判它错</b>：「放在俯视角的场地外是不对的」。</para>
    /// <para>⇒ <b>定案：俯视与正常视角同一个落点（盘面正中）</b>。
    /// 位置逻辑已从 <c>animation_screen_lock2.Update()</c> 里整个删掉，
    /// 这里<b>不再有任何调用点</b>，保留定义只为记录这三轮教训。</para>
    /// </summary>
    public static Vector3 PhaseTextAnchor
    {
        get { return new Vector3(38.6f, 0f, 0f); }
    }

    /// <summary>
    /// ⛔⛔ **已停用**（2026-09-30）：阶段大字在俯视角下的缩放系数（原 0.40）。
    ///
    /// <para><b>它和「挪到右侧空列」是同一个决定的两半</b>，那边的落点方案被推翻时
    /// 这半个也必须一起删 —— 只删落点会留下「为挪位而设的尺寸」。</para>
    ///
    /// <para>用户口径：「阶段大字太小了。要求其在俯视角下也要有正常视角一样大的占比」
    /// ⇒ 缩放**整段删除**（<c>gameField.animation_show_big_string</c> 里那处乘法已去掉），
    /// 两档用同一个 <c>localScale</c>。<b>这里不再有调用点</b>，
    /// 保留定义只为记下这轮实测：</para>
    ///
    /// <para>实测（<c>[view] ptext box=</c>，2D UI 相机投影，1920×986）：
    /// 60° = <b>716×419</b>（占屏 <b>37.3% × 42.5%</b>）／俯视旧值 = 286×168（14.9% × 17.0%）。
    /// 比值 2.503 / 2.494 ≈ 1/0.40 ⇒ <b>同占比的系数就是 1.0</b>。</para>
    ///
    /// <para>⛔ 顺带钉一条几何事实：这张字挂在 <c>ui_main_2d</c> 下、活在 <b>2D UI 相机</b>空间，
    /// 而视角切换<b>只动 3D 相机</b>（<c>camera_game_main</c> / <c>camera_container_3d</c>）
    /// ⇒ 同一个 <c>localScale</c> 在两档就是同一个屏占，<b>不需要任何补偿系数</b>。
    /// （这与 §<see cref="PhaseTextAnchor"/> 记的坑是同一件事的两面：
    /// 那次是<b>坐标</b>用错了相机空间，这次是<b>尺寸</b>误以为要跟着视角变。）</para>
    /// </summary>
    public const float PhaseTextScaleFlat = 0.40f;

    /// <summary>
    /// 「拉镜头」的**自动值**：把最外那一行的**外沿 ＋ 留白**正好贴进取景窗口的下沿。
    /// ・闲态：最外行就是手牌行本身 ⇒ 算出来**恰是 0**（不需要额外闸门，公式天然自洽；
    ///   旧口径要单独加一道「<c>|z| ≤ CoverZ ⇒ 0</c>」的闸，就是因为行位置是反算的）。
    /// ・摊开：行往外堆 ⇒ 值变负，弹过去。
    /// ⛔ **卡的大小一个像素都不变**（沿 z 平移不改高度）—— 这是用户 2026-09-23 第 2 条的口径。
    /// </summary>
    /// <summary>
    /// 摊开时**给「最外那一行」留的余量**（以 <see cref="ScreenMargin"/> 为单位）。
    ///
    /// <para>🔑 v3 返工（2026-09-29 用户连报三条，其中两条是「极限太紧」）：
    /// 把镜头拉到极限时会同时发生两件坏事 ——
    /// ① 最外那一行的**下沿正好压在屏幕边**上（用户原话：「离屏幕的距离**太极限了，要留一点**」）；
    /// ② 场地那半边连同它的**牌堆计数字样**（`35` / `14(0)` 那一行）被推出屏幕
    /// （用户原话：「确认卡时有些极限了，**卡组之类的字样没显示出来**」）。</para>
    ///
    /// <para>⚠ 这和 2026-09-28 那句「极限是下边缘与屏幕的边缘、不能显示差一截」是**同一条需求的两面**：
    /// 那天我把 <c>− ScreenMargin</c> 整个删掉去做「贴边」，结果贴过头了。
    /// 定稿 = <b>半个 <see cref="ScreenMargin"/></b>：明显留了一点（≈22px），
    /// 又不至于「差一截」到看不见那一行。</para>
    /// </summary>
    public const float PanEdgeSlackK = 0.5f;

    /// <summary>
    /// ⭐ 2026-09-29：摊开时**每排卡下面那行字**（「卡组」等位置信息）要占的世界高度。
    ///
    /// <para>这就是用户连着报三次的「最底下还是被遮住 / 卡组之类的字样没显示」的**真正病根**：
    /// <see cref="PanAuto"/> 只按 <see cref="CardHalfS"/>（卡片半长）算极限，**没算卡下面那行字**。
    /// 2026-09-28 我曾用一个行程夹取想掩盖它 ⇒ 方向错了（把「预留」做成了「限制行程」），已撤回。</para>
    ///
    /// <para>实测那行字高 ≈21px ≈ 1.22 世界，给 1.7 世界（含上下各 0.24 的呼吸）。</para>
    /// </summary>
    // ⚠ 2029-09-30 再加：1.7 → **3.0**。用户在 1.7 那版之后仍报「卡组等字还是看不到」
    //   ⇒ 1.7 的留位**不够**（它只够住下那行字自己的高度，没给它下面留空）。
    //   给 3.0：字高 ≈1.22 + 下方空 ≈1.8。
    // ⚠ 2026-09-30 再校准：3.0 → **4.6**。实拍里那行字的中心在卡下约 **1.0 世界**（
    //   卡半长 3.6），字高 ≈ 1.2 世界 ⇒ 完全清下需 ≈4.6。
    //   行距加到 6.5s 后每排有 200px，裸需 79px（字）+ 123px（卡）= 202px ⇒ 刚好接着，
    //   所以极限处的留位就取 4.6（不多花行程）。
    public const float RowLabelRoom = 4.6f;

    /// <summary>
    /// 牌堆计数/名称字样在场地**外沿之外**还要占的世界宽度。
    /// 实测那行字就贴在场地贴图外框边上，而贴图半高 <see cref="MatHalfZ"/> **不含**它们
    /// ⇒ 拉镜头时它们是最先出屏的（用户 2026-09-29 报障）。
    /// </summary>
    public const float LabelRoom = 2.2f;

    /// <summary>
    /// 取景纵向平移的**绝对上限** = <c>CoverZ − MatHalfZ − <see cref="LabelRoom"/></c>，
    /// 保证「场地远端 + 它的字样」始终留在屏内。本轮实测 27.47 − 17.6 − 2.2 = <b>7.67</b> 世界（≈138px）。
    ///
    /// <para>⚠ 与 <see cref="PanAuto"/> 的分工：自动值可以更大（它的职责是「把最外一行拉进视野」），
    /// 但**夹取一律被这条截断** ⇒ 摊开摊到很远时，玩家最多只能拉到「场地还在」的位置。
    /// 这正是用户要的取舍：**「要留一点」优先于「把每一行都拉到眼前」**。</para>
    /// </summary>
    public static float PanFieldLimit
    {
        get
        {
            // ⚠ 2026-09-29 再修：原来只减 `LabelRoom`，结果场地远端连同字样正好**贴住窗口沿**
            //   （实测字样落在 y≈5~10）⇒ 用户报「卡组之类的位置信息都看不到」。
            //   ⇒ 这里**再减一整个 `ScreenMargin`**，让远端 + 字样离窗口沿还剩一条留白。
            //   本轮实测：27.47 − 2.47 − 17.6 − 2.2 = **5.20** 世界（≈94px 的纵向行程）。
            float v = CoverZ - ScreenMargin - MatHalfZ - LabelRoom;
            return v > 0f ? v : 0f;
        }
    }

    public static float PanAuto()
    {
        float z = RowOuter;
        // ⭐ 2026-09-30 修一个**我自己引入的真回归**：`RowOuter` 在**闲态**下并不是 0。
        //   `Ocgcore` 那个循环会把**手牌行自己**的 z（−HandRowAbs）也写进去（`|tdRowZ[0]| > 0`）。
        //   我新增的 `− RowLabelRoom` 因此在闲态也被扣了一次 ⇒ 自动值变成 **−1.80**
        //   ⇒ 镜头静息就偏下 ⇒ **对方手牌被推出屏幕上沿**（实测边距 −1px）。
        // ⇒ 加一道闸：**只有真的有行摊到手牌行之外**（|z| > HandRowAbs）才启动「摊开」那套逻辑；
        //   闲态恒返 0（就是需求里「Normal 不能滑」的要求）。
        if (z > -HandRowAbs - 0.01f)
        {
            return 0f;
        }
        // ⎣ 极限 = 最外那一行的下沿停在**离屏边半个留白**处。
        //   下面还要扣掉 `RowLabelRoom`（卡下那行「卡组」字），
        //   否则最外那一排的字永远被切（用户连报三次）。
        float t = z - CardHalfS - RowLabelRoom + ScreenMargin * PanEdgeSlackK + CoverZ;
        return t < 0f ? t : 0f;
    }

    /// <summary>量程宽度 = |<see cref="PanHi"/> − <see cref="PanLo"/>|。
    /// ⛔ **只用来报数**，夹取一律走 <see cref="ClampPan"/>。</summary>
    public static float PanLimit()
    {
        float w = PanHi() - PanLo();
        return w < 0f ? -w : w;
    }

    /// <summary>
    /// 量程的**下界**（v3 返工，2026-09-28 用户报「能过度上划到对手手牌后面的空白处」；
    /// 2026-09-29 再返工，**再收一次**，见下面 ⛔）。
    ///
    /// <para>⛔⛔⛔ 2026-09-29 撤回的那一版：<c>PanLo = min(PanAuto, −ScreenMargin)</c>、
    /// <c>PanHi = max(PanAuto, +ScreenMargin)</c>。那版是拿「所有内容仍在屏内的全体平移量」
    /// 当量程，推理本身没错，但**取到的界是错的**：它允许往 +z（对手那侧）多滑一条留白带那么宽
    /// ⇒ 正俯视下 <see cref="CoverZ"/> 之外什么都没有，于是「滑过头看到一大片空白」又回来了。</para>
    ///
    /// <para>🔑 **定稿（2026-09-29 之后，别再改回去）**：量程 = <b>静息位与摊开位之间的那一段</b>。
    /// <c>PanAuto()</c> 的符号 = 摊开的那批卡在哪一侧（<c>RowOuter</c> 带符号存），
    /// 所以自动值只会往<b>那一侧</b>弹；反方向回 0 即可 ——
    /// <c>PanLo = min(PanAuto, 0)</c>、<c>PanHi = max(PanAuto, 0)</c>。</para>
    ///
    /// <para>于是三个状态的量程（这三条就是全部判据，别再指望第四种）：
    /// <list type="bullet">
    /// <item><b>闲态</b>（没摊开）：<c>PanAuto()=0</c> ⇒ <c>PanLo=PanHi=0</c> ⇒
    ///       <b>上下一格都滑不动</b>。这正是需求「正常情况不能滑」。</item>
    /// <item><b>摊开</b>（<c>PanAuto()&lt;0</c>）：<c>PanLo=PanAuto</c>、<c>PanHi=0</c>
    ///       ⇒ 往下能拉到底（把最外那排连它的字拉进视野），往回能滑回静息。</item>
    /// <item><b>往 +z</b>（对手那侧）：<c>PanHi=0</c> ⇒ <b>一点都滑不过去</b>，
    ///       结构上不可能滑进「对手手牌上方的空白」。</item>
    /// </list></para>
    ///
    /// <para>⛔ 「极限处留一点空白」是 <see cref="PanAuto"/> 内部的
    /// <c>ScreenMargin × <see cref="PanEdgeSlackK"/></c>，<b>不是</b>这里的量程
    /// —— 2026-09-29 已经因为把它做成「限制行程」被用户驳回过一次（见下面的注释）。</para>
    /// </summary>
    public static float PanLo()
    {
        float a = PanAuto();
        // ⛔⛔⛔ 2026-09-29 **撤回**上一版那个 PanFieldLimit 夹取。
        //   上一版把 PanAuto ≈ −33.8（摊开 6 行时把最外一行拉进来所需的位移）夹到 −5.2
        //   ⇒ 玩家只够看 1 行，**比原问题更严重**。用户原话：
        //   「第二个问题的本质就是让玩家**能全部拉的到**的前提下有预留」。
        //   ⇒ 「预留」属于**极限处留一点**（在 PanAuto 里，已用 PanEdgeSlackK 做了），
        //   **不属于**「限制能拉多远」。这里恢复成不夹。
        return a < 0f ? a : 0f;
    }

    /// <summary>
    /// 量程的**上界** = <see cref="PanAuto"/>，且**恒不小于 0** ⇒ 摊开我方卡堆时上界**恰好 0**。
    ///
    /// <para>ⓘ 这就是用户「**不能漏出太多空白**」那条：<c>PanHi</c> 永远 ≤ <c>PanAuto</c>，
    /// 而 <c>PanAuto</c> 只在**有行摊到 −z 那一侧**时才为负 ⇒ 那一侧的上界就是 0
    /// ⇒ 往 +z（对手那一侧）**一点都滑不动**，绝对滑不进「对手手牌上方的空白」。
    /// 闲态两者都是 0 ⇒「正常不能滑」。</para>
    /// </summary>
    public static float PanHi()
    {
        return PanAuto() > 0f ? PanAuto() : 0f;   // 同 PanLo：不夹（理由见那里）
    }

    /// <summary>把候选平移量夹进**单边**量程。<see cref="PanLo"/>／<see cref="PanHi"/> 的唯一实现。</summary>
    public static float ClampPan(float v)
    {
        float lo = PanLo();
        float hi = PanHi();
        return v < lo ? lo : (v > hi ? hi : v);
    }

    /// <summary>
    /// 每帧夹取：**「机位自动回家」的实现本体**。
    /// ⛔ 必须放在**每帧求机位那一处**（<c>Program.fixALLcamerasPreFrame</c>）：
    /// <see cref="PanZ"/> 只在 setter 被夹，摊开集合一变（行程缩短）就没有别的调用点
    /// 会把旧平移量夹回去。放在这里同时**替代**了旧口径在
    /// <c>Ocgcore.show / clearAllShowed / Setting</c> 三处各写一遍归零的做法 ——
    /// 少三个能忘的点。
    /// </summary>
    public static void NormalizePan()
    {
        if (!Program.topDownLike)
        {
            PanZ = 0f;
            return;
        }
        if (!PanUserTook)
        {
            // ⛔⛔ 自动值**也要**被 `PanFieldLimit` 截断（2026-09-29 实测踩到）。
            //   原来这里直接 `PanZ = PanAuto()` **不夹**：摊开 40 张卡组时最外行到 |z|≈59.5
            //   ⇒ PanAuto ≈ **−33.8 世界**，而 `PanFieldLimit` 只有 5.20
            //   ⇒ 镜头直接飞出屏幕，场地远端连同牌堆计数字样全不见
            //   （用户原话：「确认卡时有些极限了，卡组之类的字样没显示出来」）。
            //   ⚠ 只夹用户那一支是不够的：摊开是**自动**弹过去的，用户根本没碰滚轮。
            PanZ = ClampPan(PanAuto());
        }
        else
        {
            // ⛔ 单边夹取（v3 返工）：原来是 ±PanLimit() 的**对称**夹取，于是能往
            //   「没有内容的那一侧」滑出去、划过对手手牌继续滑进纯空白 ——
            //   用户 2026-09-28 原话「能过度上画到对手手牌后的空白处」。
            //   理由与新量程见 `PanLo` 的注释。
            PanZ = ClampPan(PanZ);
        }
    }

    /// <summary>显式复位（换视角 / 新对局）。收摊那一路由 <see cref="NormalizePan"/> 覆盖，不必单独写。</summary>
    public static void ResetPan()
    {
        PanZ = 0f;
        PanUserTook = false;
        RowOuter = 0f;
    }

    /// <summary>滚轮驱动一次平移：<b>一格挪一步</b>（只取方向），量程 ±<see cref="PanLimit"/>。</summary>
    public static void PanBy(float wheel)
    {
        // ⛔ 只取 `wheel` 的**方向**，不按绝对值换算：`Program.wheelValue` 一格有多大随机器与
        //   轴灵敏度变（本机实测 20）。Unity 的 ScrollWheel 是「本帧增量」⇒ 一个非零 delta
        //   天然就是一格；急滚只挪一步，但每帧一步已足够顺，且与机器无关。
        if (wheel > 0.0001f || wheel < -0.0001f)
        {
            float next = PanZ + (wheel > 0f ? PanStep : -PanStep);
            // ⛔ 单边量程（v3 返工，见 `PanLo` 的注释）：不是 ±|PanAuto()|，
            //   而是只准往摊开的那一侧滑 ⇒ 滑不过对手手牌、也进不了空白区。
            PanZ = ClampPan(next);
            PanUserTook = true;
        }
    }

    // ── 手牌排的行带 ──────────────────────────────────────────────────────

    /// <summary>
    /// 手牌 / 摊开行里**第 k 行**的 |z|（k=0 = 手牌排本身，k 越大越**往外** = 会出屏那侧）。
    /// 行距 = <see cref="RowPitch"/> × <see cref="HandScale"/> = 7.5（= 60° 原生展示行节距 5 的观感）。
    /// ⛔ **绝不按行数缩小**（用户 2026-09-23 明确否掉旧口径：40 张卡组 6 行会被压到 0.367 倍、
    ///   卡宽只剩 18px）。出屏的行交给 <see cref="NormalizePan"/> 拉过去看。
    /// </summary>
    public static float RowAbs(int k)
    {
        return HandRowAbs + RowPitch * HandScale * k;
    }

    /// <summary>
    /// 直接攻击（打玩家）时箭头指到的 |z|。关态 = 原生那两条式子；开态 = **手牌排的外沿**
    /// （旧口径写死 <c>CoverZ − 0.5</c>，其实是「手牌外沿」的另一种写法，现在由常量直接给出）。
    /// ⛔ 不换的话箭头会飞到取景范围之外（原生 −23.15 / +24.99）⇒ 看不见。
    /// </summary>
    public static float AttackTargetAbsZ(float nativeAbsZ)
    {
        return HandOuterAbs;
    }
}
