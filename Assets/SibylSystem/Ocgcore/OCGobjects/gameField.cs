using Ionic.Zip;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;
using YGOSharp.OCGWrapper.Enums;

public class GameField : OCGobject
{
    public GameObject me_left_p_num;
    public GameObject me_right_p_num;
    public GameObject op_left_p_num;
    public GameObject op_right_p_num;
    GameObject p_hole_me = null;
    GameObject p_hole_op = null;
    Transform p_hole_mel = null;
    Transform p_hole_opl = null;
    Transform p_hole_mer = null;
    Transform p_hole_opr = null;
    public phaser Phase = null; 
    public bool mePHole = false;
    public bool opPHole = false;

    public List<thunder_locator> thunders = new List<thunder_locator>();

    UILabel label = null;

    List<gameHiddenButton> gameHiddenButtons = new List<gameHiddenButton>();

    public TMPro.TextMeshPro LOCATION_DECK_0;
    public TMPro.TextMeshPro LOCATION_EXTRA_0;
    public TMPro.TextMeshPro LOCATION_GRAVE_0;
    public TMPro.TextMeshPro LOCATION_REMOVED_0;
    public TMPro.TextMeshPro LOCATION_DECK_1;
    public TMPro.TextMeshPro LOCATION_EXTRA_1;
    public TMPro.TextMeshPro LOCATION_GRAVE_1;
    public TMPro.TextMeshPro LOCATION_REMOVED_1;

    UITexture leftT;
    UITexture midT;
    UITexture rightT;
    UITexture phaseTexure;

    public int retOfbp = -1;
    void onBP()
    {
        var m = new BinaryMaster();
        m.writer.Write(retOfbp);
        Program.I().ocgcore.sendReturn(m.get());
    }

    public int retOfEp = -1;
    void onEP()
    {
        var m = new BinaryMaster();
        m.writer.Write(retOfEp);
        Program.I().ocgcore.sendReturn(m.get());
    }

    public int retOfMp = -1;    
    void onMP() 
    {
        var m = new BinaryMaster();
        m.writer.Write(retOfMp);
        Program.I().ocgcore.sendReturn(m.get());
    }

    public GameField()
    {
        gameObject = create(Program.I().new_ocgcore_field, getGoodPosition(), Vector3.zero, false, Program.ui_container_3d, false);
        UIHelper.getByName(gameObject, "obj_0").transform.localScale = Vector3.zero;
        UIHelper.getByName(gameObject, "obj_1").transform.localScale = Vector3.zero;
        Phase = gameObject.GetComponentInChildren<phaser>();
        Phase.bpAction = onBP;
        Phase.epAction = onEP;
        Phase.mp2Action = onMP;
        leftT = UIHelper.getByName<UITexture>(gameObject, "leftT");
        midT = UIHelper.getByName<UITexture>(gameObject, "midT");
        rightT = UIHelper.getByName<UITexture>(gameObject, "rightT");
        phaseTexure = UIHelper.getByName<UITexture>(gameObject, "phaseT");
        midT.border = new Vector4(0, 500, 0, 230);

        leftT.mainTexture = null;
        midT.mainTexture = null;
        rightT.mainTexture = null;
        phaseTexure.mainTexture = null;

        me_left_p_num = create(Program.I().mod_ocgcore_number);
        me_right_p_num = create(Program.I().mod_ocgcore_number);
        op_left_p_num = create(Program.I().mod_ocgcore_number);
        op_right_p_num = create(Program.I().mod_ocgcore_number);

        Program.I().ocgcore.AddUpdateAction_s(Update);

        gameHiddenButtons.Add(new gameHiddenButton(CardLocation.Deck, 0));
        gameHiddenButtons.Add(new gameHiddenButton(CardLocation.Extra, 0));
        gameHiddenButtons.Add(new gameHiddenButton(CardLocation.Grave, 0));
        gameHiddenButtons.Add(new gameHiddenButton(CardLocation.Removed, 0));
        gameHiddenButtons.Add(new gameHiddenButton(CardLocation.Deck, 1));
        gameHiddenButtons.Add(new gameHiddenButton(CardLocation.Extra, 1));
        gameHiddenButtons.Add(new gameHiddenButton(CardLocation.Grave, 1));
        gameHiddenButtons.Add(new gameHiddenButton(CardLocation.Removed, 1));

        LOCATION_DECK_0 = create(Program.I().new_ui_textMesh, Vector3.zero, new Vector3(Program.tableauFrontX, 0, 0)).GetComponent<TMPro.TextMeshPro>();
        LOCATION_EXTRA_0 = create(Program.I().new_ui_textMesh, Vector3.zero, new Vector3(Program.tableauFrontX, 0, 0)).GetComponent<TMPro.TextMeshPro>();
        LOCATION_GRAVE_0 = create(Program.I().new_ui_textMesh, Vector3.zero, new Vector3(Program.tableauFrontX, 0, 0)).GetComponent<TMPro.TextMeshPro>();
        LOCATION_REMOVED_0 = create(Program.I().new_ui_textMesh, Vector3.zero, new Vector3(Program.tableauFrontX, 0, 0)).GetComponent<TMPro.TextMeshPro>();





        LOCATION_DECK_1 = create(Program.I().new_ui_textMesh, Vector3.zero, new Vector3(Program.tableauFrontX, 0, 0)).GetComponent<TMPro.TextMeshPro>();
        LOCATION_EXTRA_1 = create(Program.I().new_ui_textMesh, Vector3.zero, new Vector3(Program.tableauFrontX, 0, 0)).GetComponent<TMPro.TextMeshPro>();
        LOCATION_GRAVE_1 = create(Program.I().new_ui_textMesh, Vector3.zero, new Vector3(Program.tableauFrontX, 0, 0)).GetComponent<TMPro.TextMeshPro>();
        LOCATION_REMOVED_1 = create(Program.I().new_ui_textMesh, Vector3.zero, new Vector3(Program.tableauFrontX, 0, 0)).GetComponent<TMPro.TextMeshPro>();



        label = create(Program.I().mod_simple_ngui_text, new Vector3(0, 0, -14.5f), new Vector3(Program.tableauFrontX, 0, 0), false, Program.ui_container_3d, false).GetComponent<UILabel>();
        label.fontSize = 40;
        label.overflowMethod = UILabel.Overflow.ShrinkContent;
        label.alignment = NGUIText.Alignment.Left;
        label.width = 800;
        label.height = 40;
        label.transform.localScale = new Vector3(0.03f, 0.03f, 0.03f);
        label.text = "";
        overCount = 0;

        loadNewField();
    }

    /// <summary>OCG 场地贴图（MR4 起用的那张：5 列 + 中区 2 个额外怪兽区 + 外挂列）。</summary>
    public const string NewFieldPathOCG = "texture/duel/newfield.png";

    /// <summary>OCG 场地贴图（MR3 用的那张，构图与 newfield 同格网）。</summary>
    public const string OldFieldPathOCG = "texture/duel/field.png";

    /// <summary>
    /// RD 场地贴图 —— **紧凑盘面**（2026-09-21 重做）：中区 3 列 × 2 排，
    /// 左右各借一列放牌堆（左＝额外卡组/场地魔法，右＝卡组/墓地）。
    /// 中区额外怪兽区与外挂列整块擦掉；牌堆格与主格**等大**（同一块切片、同一缩放）。
    /// 格位坐标在 <c>Ocgcore.get_point_worldposition_rd</c>，生成脚本
    /// <c>devtools/make_rd_field.py</c>（可复跑：不带参数出对照预览，--apply 写文件）。
    /// </summary>
    public const string NewFieldPathRD = "texture/duel/newfield_rd.png";

    /// <summary>
    /// RD 本局该用哪张 —— 就是 <see cref="NewFieldPathRD"/>；它缺了才回落 OCG 那张
    /// （宁可多显示用不到的格子，也不能整块场地白板）。
    /// </summary>
    public static string RdFieldPath
    {
        get
        {
            if (File.Exists(NewFieldPathRD))
            {
                return NewFieldPathRD;
            }
            QuickTestTrace.Log("field", "RD 场地图缺 " + NewFieldPathRD + " → 回落 " + NewFieldPathOCG);
            return NewFieldPathOCG;
        }
    }

    /// <summary>
    /// 本局该用哪张新场地贴图。RD 走 <see cref="RdFieldPath"/>，
    /// OCG 就是原生那张。
    /// </summary>
    public static string NewFieldPath
    {
        get { return GameModeManager.IsRD ? RdFieldPath : NewFieldPathOCG; }
    }

    /// <summary>
    /// MR3 那条线该用哪张。**RD 与 OCG 用的图不一样**：OCG 是原生的 field.png，
    /// RD 换成上面那张紧凑盘面（RD 的格位坐标与 OCG 不同，必须配同一张图）。
    /// </summary>
    public static string OldFieldPath
    {
        get { return GameModeManager.IsRD ? RdFieldPath : OldFieldPathOCG; }
    }

    public void loadOldField()
    {
        string path = OldFieldPath;
        QuickTestTrace.Log("field", "loadOld MasterRule=" + Program.I().ocgcore.MasterRule
            + " mode=" + GameModeManager.ModeLabel + " path=" + path);
        installFieldTextures(path);
        applyPhaseBarLayout("loadOld");
        logPileProbe("loadOld");
    }

    // ═════════════════ RD 极大怪兽「大框」（用户 2026-09-22）═════════════════
    //
    // 要求：「极大怪兽召唤时将前场的格子完全隐藏，并把三张极大怪兽无缝拼接，单独做一个大框，
    //      极大怪兽离场以后变回正常状态，要求这个切换必须是无延迟的」。
    //
    // 做法：**把大框画进场地贴图**（`midT` 那一片），而不是新加 3D 物件 ——
    //   ① 两条怪兽排的格区本来就是烘焙在 `texture/duel/newfield_rd.png` 里的
    //      （见 NewFieldPathRD），换图 = 像素级替换 ⇒ 不存在「盖不住 / 露一条边」；
    //   ② 图层顺序与现在**完全一致**（大框就是场地的一部分，永远在卡下面），不引入新的
    //      渲染顺序问题（场地是 NGUI 的 UITexture，卡是世界空间四边形，两者不是一个体系）；
    //   ③ 切换 = 一次 `mainTexture` 赋值 ⇒ 没有动画/实例化/加载，**当帧生效**。
    //
    // 变体图由 `devtools/make_rd_maxband.py` 生成，做的两件事：
    //   · **半透明片 = 框那一块**（`maximumBandFrame()` 那个矩形，逐 texel 相同）：片只许在
    //     框内、且必须把框填满（2026-09-22 用户口径「那层半透明的片必须限制在框内并填满框，
    //     极限怪兽下去后和大框一起复原」）。三格那块则**整体清空**（三条竖描边、两条横描边、
    //     格间那两道缝、以及**框外那两条 36 texel 的带子**全部置透明）—— 格子要「完全隐藏」，
    //     而片不许渗出框外。
    //     ⛔⛔ 片的矩形**不许**跟着「三格」走（那是被用户打回的一版）：框(257x121) 与本排三格
    //     (327x116) **不是同一个矩形** —— 框横向窄 36 texel/边、纵向又朝场地中央多探出 14±6。
    //     片按三格铺 ⇒ 框的左右竖边之外各多出一条 36 texel（实机 ≈37px）的带子（实机亮度
    //     102.7 vs 旁边正常格子 113.5）⇒ 玩家看到的就是「半透明的那一层还是超出框外了」。
    //     ⛔ 也不许「把框内部额外再铺一层」（更早那版）：片的足迹会变成「三格 ∪ 框」的带凸台
    //     矩形，凸台正好落在框的左右竖边处留下 13px 方形台阶。两版都由判据 **6w** 咬住
    //     （片足迹 == 框内沿 ±1.5 texel 且铺满）。
    //   · **画大框**：框的尺寸与位置按「三张卡贴紧后有多大」算（见 MaxBandRect 的说明），
    //     所以框是**贴住三张卡**的，不是「把三格框起来」—— 后者明显宽于卡，一眼假。
    // 三张变体（我方/对手/两侧）在建场时就备好，之后只换引用 —— 真机上「第一次召唤时
    // 现加载」会卡一下，那就不叫无延迟了。

    /// <summary>RD 大框：我方那侧的变体贴图（生成器见 devtools/make_rd_maxband.py）。</summary>
    public const string MaxBandPathMe = "texture/duel/rd_maxband_me.png";

    /// <summary>RD 大框：对手那侧的变体贴图（描边是红的，与场上格线同色）。</summary>
    public const string MaxBandPathOp = "texture/duel/rd_maxband_op.png";

    /// <summary>中片的**基图**（`sliceField` 切出来的那一片），= 「没有极大怪兽」时的样子。</summary>
    Texture2D midBaseTexture;

    /// <summary>下标即掩码：1=我方、2=对手、3=两侧（0 不用，那档走 <see cref="midBaseTexture"/>）。</summary>
    Texture2D[] maxBandTextures = new Texture2D[4];

    /// <summary>当前挂在 <see cref="midT"/> 上的是哪一档（0=基图）。改它只在 <see cref="setMaximumBand"/> 里。</summary>
    int maxBandState = 0;

    /// <summary>中区三格的**外沿**（999x800 原始空间）：这块要整体洗成一片底色，格线一条不留。</summary>
    private const float BandCellsX0 = 330f;

    /// <inheritdoc cref="BandCellsX0"/>
    private const float BandCellsX1 = 657f;

    /// <summary>三张极大怪兽贴紧后的世界尺寸换算成 texel（推导见 MaxBandRect 的说明）。</summary>
    private const float BandTrioW = 244f;

    /// <summary>纵向足迹：卡与场地**共面** ⇒ 与横向同一个 texel/世界 比（5.8 × 18.72 = 108.6）。</summary>
    private const float BandTrioH = 108.6f;

    /// <summary>中列格心（999x800 原始空间）—— 本体就摆在这一列。</summary>
    private const float BandCenterX = 493.5f;

    /// <summary>场地中央那条分界（两排之间，999 空间 y 朝下）—— 偏移方向的判据。</summary>
    private const float BandFieldCenterY = 400.5f;

    /// <summary>
    /// 卡的**贴图足迹中心**离本排格心的距离（texel），方向一律**朝场地中央**：
    /// 对手那排在上方 ⇒ 向下 +14；我方那排 ⇒ 向上 −14。
    ///
    /// 实测（2026-09-22 重测，`frect` + 截图上四条格线做射影标定后反解）：对手排三张卡的
    /// 足迹 = texel y 219.6..327.5，中心 275.8，而本排格心 261.5 ⇒ +14.3。
    /// ⛔ 方向**必须分侧**：第一版两侧都「向下 +10」，我方侧就整整低了 2×14 texel——
    ///   框底（613）越过本排魔陷排顶线（603）把它吃掉、框顶又罩不住卡，正是用户报的
    ///   「框太大、下面的格子顶线被挤掉」。分侧后两侧到本排魔陷排的净空都是 18 texel。
    /// </summary>
    private const float BandCenterYOff = 14f;

    /// <summary>框到卡外沿的横向余量（texel ≈ 屏幕像素）。</summary>
    private const float BandMarginX = 6f;

    /// <summary>纵向余量（texel）：横向 6 在屏幕上 ≈6px，纵向 ≈4~7px（透视把纵向压扁）。</summary>
    private const float BandMarginY = 6f;

    /// <summary>出血：吃掉 SX/SY 舍入的 ±1 texel，别让旧描边露一丝。</summary>
    private const int BandBleed = 1;

    /// <summary>
    /// 大框的矩形（999x800 原始空间，y 朝下）：`x0, y0, x1, y1`。
    ///
    /// = 三张卡的**贴图足迹**（中列格心 ± 半个「三张卡」尺寸，纵向再朝场地中央挪
    ///   <see cref="BandCenterYOff"/>）+ <see cref="BandMarginX"/> / <see cref="BandMarginY"/>。
    /// 一律取整，**与生成器 `frame_rect()` 逐 texel 相同**（它是先取整再和格区取并集）。
    /// </summary>
    private static void maximumBandFrame(int controller, out int x0, out int y0, out int x1, out int y1)
    {
        float rowC = (controller == 0 ? (481f + 596f) : (204f + 319f)) * 0.5f;
        // 朝场地中央：我方那排在下方 ⇒ 向上（−y），对手那排在上方 ⇒ 向下（+y）
        float cy = rowC + (rowC < BandFieldCenterY ? BandCenterYOff : -BandCenterYOff);
        x0 = (int)Math.Round(BandCenterX - BandTrioW * 0.5f - BandMarginX);
        x1 = (int)Math.Round(BandCenterX + BandTrioW * 0.5f + BandMarginX);
        y0 = (int)Math.Round(cy - BandTrioH * 0.5f - BandMarginY);
        y1 = (int)Math.Round(cy + BandTrioH * 0.5f + BandMarginY);
    }

    /// <summary>
    /// 该侧变体图要**覆盖**的那块矩形 `(x, y, w, h)`，y 自下往上（Unity 口径）。
    ///
    /// 切片空间 = `UIHelper.sliceField` 先把场地缩到 1024x819、再切三片之后的那套坐标
    /// （中片保留 x 69/320..247/320 这一段）。实测常量（999x800 原始空间）：
    ///   中区三列 x 330..657、怪兽排 y 我方 481..596 / 对手 204..319。
    ///
    /// ⚠ 覆盖范围 = **三格 ∪ 大框**，不是「三格」：
    ///   · 三格那块（330..657 × 该排）要**整体清空**（三条竖描边、两条横描边、格与格之间
    ///     那两道缝、以及**框外那两条 36 texel 的带子**全置透明）—— 这才是用户要的
    ///     「格子完全隐藏」，同时也是「片不许渗出框外」的前提；
    ///   · 大框比三格**窄**（244 texel vs 327）⇒ 补丁必须横着罩到三格的两端，否则框的左右
    ///     两侧会留着格线；纵向则**朝场地中央**探出本排一截（对手排向下、我方排向上，见
    ///     BandCenterYOff）⇒ 也得罩得住框，别把框裁掉一条边。
    ///   ⚠ 半透明片（`(255,255,255,51)` 那层）**只铺在框内、且必须铺满框**（用户 2026-09-22
    ///     口径）；本补丁矩形（三格 ∪ 框）比片大是**故意的**，多出来那部分一律透明。
    ///   ⛔ 别把这块退化成「三格」：那样框会被裁掉一截，或者得把框改成「罩住三格」——
    ///      后者就是 2026-09-22 第一版的错法（框明显宽于卡，实机截图一眼假）。
    ///
    /// 「三张卡」那几个尺寸怎么来的（每一档都有实测支撑，见 make_rd_maxband.py 的注释）：
    ///   · 世界尺寸 13.05 × 5.8（卡面 Quad(1x1) × face.localScale(3,4) × 1.45，三张贴紧）；
    ///   · 横向 110 texel = 一列 = 5.876 世界 ⇒ 18.72 texel/世界；
    ///   · 纵向同一个比（卡与场地共面）：5.8 × 18.72 = 108.6。
    ///     ⛔ 别用「排距 20.96」或「屏幕折算 19.14」那两档 —— 那是第一版 111 的来源，实测偏大。
    ///
    /// ⚠ 与 `devtools/make_rd_maxband.py` 的 `band_rect_slice()` 是**同一套算式**：
    ///   改这里的数字必须同时改那边（生成器产出的图必须与这里算出的 w/h 一模一样）。
    /// </summary>
    private static int[] MaxBandRect(int controller)
    {
        const float SX = 1024f / 999f;
        const float SY = 819f / 800f;
        int by0 = controller == 0 ? 481 : 204;
        int by1 = controller == 0 ? 596 : 319;
        int fx0, fy0, fx1, fy1;
        maximumBandFrame(controller, out fx0, out fy0, out fx1, out fy1);
        int x0 = Math.Min((int)BandCellsX0, fx0) - BandBleed;
        int x1 = Math.Max((int)BandCellsX1, fx1) + BandBleed;
        int y0 = Math.Min(by0, fy0) - BandBleed;
        int y1 = Math.Max(by1, fy1) + BandBleed;
        int sx0 = (int)Math.Round(x0 * SX);
        int sx1 = (int)Math.Round(x1 * SX);
        int syLo = 819 - 1 - (int)Math.Round(y1 * SY);
        int syHi = 819 - 1 - (int)Math.Round(y0 * SY);
        return new int[] { sx0, syLo, sx1 - sx0 + 1, syHi - syLo + 1 };
    }

    /// <summary>
    /// 按「哪一侧场上有**成型的极大怪兽**」换 <see cref="midT"/> 那张图。
    /// 掩码：bit0=我方、bit1=对手；<c>0</c> = 基图（正常的三格）。
    ///
    /// <paramref name="mask"/> 传的是**三件齐**那一档（中区三列站着同一侧的三件，不看位置）——
    /// 口径就是「框的侧别 == 卡**实际画在**哪一侧」，因为卡画在哪一侧由 `c.p.controller`
    /// 决定（摆位用的也是它），而框是画在这张场地贴图上的。
    /// ⛔ 不许改成「等三张落稳」：真机 L/R 是 tween 过去的，等落稳要 1~2 秒，那段时间格子
    ///   还照原样画着 ⇒ 用户 2026-09-22 第 2 条「刚完成时还保留着小框」。
    ///   出场飞行期会先亮「错侧」170ms，但那一侧**此刻真的画着这三张卡**（造局日志 54.019
    ///   本体落在 z=-675），所以框跟着卡走是对的，见 Ocgcore 里换图调用点的注释。
    /// <paramref name="settledMask"/> 只是**诊断用**（写进日志 `settled=`），不参与换图：
    /// `settled=0 mask=3` 就是「三件齐了、但三张还没到位」的痕迹。
    /// </summary>
    public void setMaximumBand(int mask, int settledMask = -1)
    {
        mask &= 3;
        if (mask != 0 && maxBandTextures[mask] == null)
        {
            return;                       // 变体没备起来（非 RD / 缺图）：保持基图，别换成空图
        }
        if (midT == null || mask == maxBandState)
        {
            return;
        }
        maxBandState = mask;
        midT.mainTexture = mask == 0 ? midBaseTexture : maxBandTextures[mask];
        // ⚠ settled= 放在 mask= 之后：验收脚本 6u/6r 用 `mid=(\w+) mask=(\d)` 读这条，别插中间。
        QuickTestTrace.Log("maxband", "mid=" + maximumBandName() + " mask=" + mask
            + " me=" + ((mask & 1) != 0 ? 1 : 0) + " op=" + ((mask & 2) != 0 ? 1 : 0)
            + " settled=" + (settledMask < 0 ? "?" : (settledMask & 3).ToString()));
    }

    /// <summary>当前场地中片挂的是哪一档 —— **按引用相等判**（口径同 <see cref="PhaseBarStripName"/>，贴图没有 name）。</summary>
    public string maximumBandName()
    {
        if (midT == null || midT.mainTexture == null)
        {
            return "none";
        }
        if (ReferenceEquals(midT.mainTexture, midBaseTexture))
        {
            return "base";
        }
        if (maxBandTextures[3] != null && ReferenceEquals(midT.mainTexture, maxBandTextures[3]))
        {
            return "both";
        }
        if (maxBandTextures[1] != null && ReferenceEquals(midT.mainTexture, maxBandTextures[1]))
        {
            return "me";
        }
        if (maxBandTextures[2] != null && ReferenceEquals(midT.mainTexture, maxBandTextures[2]))
        {
            return "op";
        }
        return "missing";
    }

    /// <summary>
    /// 落一行「大框现在挂的是哪一档」的探针（只在 qt_debug.on 下走）；验收脚本咬这一行。
    /// 判据要咬**选择本身**，不能咬「格子看不见了」—— 那是截图才有的事。
    /// </summary>
    public void logMaximumBand(string tag)
    {
        if (!QuickTestTrace.Enabled)
        {
            return;
        }
        QuickTestTrace.Log("maxband", tag + " mid=" + maximumBandName() + " mask=" + maxBandState);
    }

    /// <summary>
    /// 建三张变体图（我方/对手/两侧）。基图的像素**照抄**（`GetPixels32`）再只覆盖那一块矩形，
    /// 于是变体与基图在矩形之外逐像素相同 ⇒ 换图的瞬间场地别处一个像素都不会动。
    /// </summary>
    private void installMaximumBand(Texture2D midBase)
    {
        midBaseTexture = midBase;
        maxBandState = 0;
        for (int i = 0; i < maxBandTextures.Length; i++)
        {
            destroyMaximumBandTexture(i);
        }
        if (!GameModeManager.IsRD || midBase == null)
        {
            return;
        }
        Texture2D me = UIHelper.getTexture2D(MaxBandPathMe);
        Texture2D op = UIHelper.getTexture2D(MaxBandPathOp);
        int[] rm = MaxBandRect(0);
        int[] ro = MaxBandRect(1);
        if (me == null || op == null
            || me.width != rm[2] || me.height != rm[3]
            || op.width != ro[2] || op.height != ro[3])
        {
            // 缺图 / 尺寸对不上：不报错、不换图，只留一行痕迹（否则会静默地「大框不出来」）
            QuickTestTrace.Log("maxband", "变体图不可用 me=" + (me == null ? "缺" : me.width + "x" + me.height)
                + " want=" + rm[2] + "x" + rm[3]
                + " op=" + (op == null ? "缺" : op.width + "x" + op.height)
                + " want=" + ro[2] + "x" + ro[3]
                + "（生成器 devtools/make_rd_maxband.py --apply）");
            return;
        }
        Color32[] basePx = midBase.GetPixels32();
        Color32[] mePx = me.GetPixels32();
        Color32[] opPx = op.GetPixels32();
        for (int mask = 1; mask <= 3; mask++)
        {
            Texture2D t = new Texture2D(midBase.width, midBase.height);
            t.SetPixels32(basePx);
            if ((mask & 1) != 0)
            {
                t.SetPixels32(rm[0], rm[1], rm[2], rm[3], mePx);
            }
            if ((mask & 2) != 0)
            {
                t.SetPixels32(ro[0], ro[1], ro[2], ro[3], opPx);
            }
            t.Apply();
            maxBandTextures[mask] = t;
        }
        QuickTestTrace.Log("maxband", "built base=" + midBase.width + "x" + midBase.height
            + " meRect=" + rm[0] + "," + rm[1] + "," + rm[2] + "," + rm[3]
            + " opRect=" + ro[0] + "," + ro[1] + "," + ro[2] + "," + ro[3]
            // 框本身（999 空间，y 朝下）—— 与「三格」摆一起报，验收脚本就能一眼看出
            // 「框有没有被算成罩住三格」（那正是第一版错法）。
            + " meFrame=" + FrameStr(0) + " opFrame=" + FrameStr(1));
    }

    /// <summary>框的矩形报成 `x0,y0,x1,y1`（999 空间，y 朝下）—— 只给探针/日志用。</summary>
    private static string FrameStr(int controller)
    {
        int fx0, fy0, fx1, fy1;
        maximumBandFrame(controller, out fx0, out fy0, out fx1, out fy1);
        return fx0 + "," + fy0 + "," + fx1 + "," + fy1;
    }

    private void destroyMaximumBandTexture(int i)
    {
        if (maxBandTextures[i] != null)
        {
            UnityEngine.Object.Destroy(maxBandTextures[i]);
            maxBandTextures[i] = null;
        }
    }

    /// <summary>两张场地贴图（新/旧）共用的装载：切三片 → 挂上去 → 备大框变体。</summary>
    private void installFieldTextures(string path)
    {
        Texture2D midBase = null;
        if (File.Exists(path))
        {
            Texture2D textureField = UIHelper.getTexture2D(path);
            Texture2D[] textureFieldSliced = UIHelper.sliceField(textureField);
            leftT.mainTexture = textureFieldSliced[0];
            midT.mainTexture = textureFieldSliced[1];
            rightT.mainTexture = textureFieldSliced[2];
            midBase = textureFieldSliced[1];
        }
        else
        {
            leftT.mainTexture = new Texture2D(100, 100);
            midT.mainTexture = new Texture2D(100, 100);
            rightT.mainTexture = new Texture2D(100, 100);
        }
        installMaximumBand(midBase);
    }

    /// <summary>
    /// RD 模式下**整格不显示**的点：双方怪兽区/魔陷区的最左最右各一格
    /// （RD 只有中区 3 列；那两列在贴图里已经变成卡组/额外的格子，见
    /// <see cref="NewFieldPathRD"/>），以及灵摆区（SpellZone seq 6/7）。
    ///
    /// 这些点不该再挂「禁用」标记 —— core 在 RD 局里确实会把它们标成
    /// field_disabled，格子都已经不存在了，红圈只会悬在空处（2026-09-20 实测截图所见）。
    /// </summary>
    private static bool hiddenInRD(GPS gps)
    {
        if ((gps.location & (UInt32)CardLocation.MonsterZone) > 0)
        {
            return gps.sequence == 0 || gps.sequence == 4;
        }
        if ((gps.location & (UInt32)CardLocation.SpellZone) > 0)
        {
            return gps.sequence == 0 || gps.sequence == 4
                || gps.sequence == 6 || gps.sequence == 7;
        }
        return false;
    }

    public void loadNewField()
    {
        string path = NewFieldPath;
        QuickTestTrace.Log("field", "loadNew MasterRule=" + Program.I().ocgcore.MasterRule
            + " mode=" + GameModeManager.ModeLabel + " path=" + path);
        installFieldTextures(path);
        applyPhaseBarLayout("loadNew");
        logPileProbe("loadNew");
    }

    /// <summary>
    /// 落一行「牌堆那几格现在摆在哪」的探针（只在 qt_debug.on 下走）。验收脚本咬这一行。
    ///
    /// 为什么单独立一条：「贴图与坐标必须成对改」是这条线上最容易出的事故
    /// ⇒ 判据也得成对咬（贴图路径 6e 咬一张，坐标这里咬一次）。
    /// 一次报全双方各 5 个点（卡组 / 额外 / 墓地 / 场地魔法 / 除外）。
    /// </summary>
    private void logPileProbe(string tag)
    {
        if (!QuickTestTrace.Enabled)
        {
            return;
        }
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        sb.Append("piles ").Append(tag)
          .Append(" rule=").Append(Program.I().ocgcore.MasterRule);
        for (int c = 0; c < 2; c++)
        {
            sb.Append(c == 0 ? " me" : " op");
            sb.Append(" deck=").Append(PileXZ(c, CardLocation.Deck, 0));
            sb.Append(" extra=").Append(PileXZ(c, CardLocation.Extra, 0));
            sb.Append(" grave=").Append(PileXZ(c, CardLocation.Grave, 0));
            sb.Append(" field=").Append(PileXZ(c, CardLocation.SpellZone, 5));
            sb.Append(" removed=").Append(PileXZ(c, CardLocation.Removed, 0));
        }
        QuickTestTrace.Log("field", sb.ToString());
    }

    /// <summary>某个牌堆点的落点 "(x,z)"，给探针用 —— 口径与卡一致（同一个 get_point_worldposition）。</summary>
    private static string PileXZ(int controller, CardLocation loc, uint seq)
    {
        Vector3 v = Program.I().ocgcore.get_point_worldposition(new GPS
        {
            controller = (uint)controller,
            location = (uint)loc,
            sequence = seq
        });
        return "(" + v.x.ToString("F2") + "," + v.z.ToString("F2") + ")";
    }

    /// <summary>
    /// 阶段条该长什么样 —— **唯一**的落点（底板贴图 + 六个标签的摆位）。
    ///
    /// 为什么要抽出来：阶段条由「模式 + 规则号」共同决定，而场地重载有**两个入口**
    /// （`loadNewField` / `loadOldField`），再加上 `Ocgcore.show()` 每次都
    /// `new GameField()` —— 而构造函数里无条件调 `loadNewField()`。三处各写一份
    /// 「什么模式摆什么」必然分叉。2026-09-21 就是这么坏的：
    ///
    ///   撤回重开 → `RewindLocally()` → `hide()` + `shiftToServant(ocgcore)` → `show()`
    ///   → `new GameField()` → `loadNewField()` ⇒ 阶段条落回 **MR4/5 档**（六格分两组、
    ///   中间留空、底板不画、文案 M1/M2）。而重开那一局的 `Start` 是在
    ///   `DuelUndo.silent` 期间到的 —— `sibyl()` 里 `practicalizeMessage` 被整段跳过
    ///   （画面按撤回那一刻画好的，不再重演）⇒ **没人把它摆回 RD 档**，
    ///   阶段条从此停在「中间像是被链接格子卡开」的样子（用户实测所见）。
    ///
    /// 口径：
    ///   · **RD 看模式，不看 MasterRule**：RD 的规则号（3）本来就不参与盘面判定
    ///     （格位走 `get_point_worldposition_rd`、场地图走 `OldFieldPath`），
    ///     而它的阶段条与 OCG 的 MR3 档也不一样（只有四个阶段 + 四格底板）；
    ///   · OCG 仍按 MasterRule 分 MR4+ / MR≤3 两档（原生行为，不动）。
    /// </summary>
    private void applyPhaseBarLayout(string tag)
    {
        lazyBTNMOVER mover = gameObject.GetComponentInChildren<lazyBTNMOVER>();
        if (mover == null)
        {
            return;
        }
        if (GameModeManager.IsRD)
        {
            if (GameTextureManager.phaseRD == null)
            {
                QuickTestTrace.Log("field",
                    "RD 阶段条底板缺 texture/duel/phase/phase_rd.png → 不画底板"
                    + "（四格布局照旧；生成器 devtools/make_rd_phase.py）");
            }
            phaseTexure.mainTexture = GameTextureManager.phaseRD;
            mover.shiftRD();
            phaseBarStyle = "RD4";
        }
        else if (Program.I().ocgcore.MasterRule >= 4)
        {
            phaseTexure.mainTexture = null;
            mover.shift(true);
            phaseBarStyle = "MR4";
        }
        else
        {
            phaseTexure.mainTexture = GameTextureManager.phase;
            mover.shift(false);
            phaseBarStyle = "MR3";
        }
        logPhaseBar(tag);
    }

    /// <summary>
    /// 落一行「阶段条现在长什么样」的探针（只在 qt_debug.on 下走）。
    ///
    /// 为什么要单独立一条：阶段条的排布**只有一个改法** —— `lazyBTNMOVER.shift()`，
    /// 而它是在**每次场地重载**时被调用的（`loadNewField` / `loadOldField`）。
    /// 而「场地重载」不止发生在开局：`Ocgcore.show()` 每次都 `new GameField()`
    /// （构造函数里无条件 `loadNewField()`），撤回重开也会走一遍。
    /// ⇒ 「谁最后把它摆成哪一档」必须看得见，光看代码看不出顺序。
    ///
    /// 一次报全：规则号 / 模式 / 底板贴图 / 六个标签的 x、宽、显隐、文本。
    /// `x=` 那串的顺序见 `names=`（dp/sp/mp1/bp/mp2/ep）。
    /// </summary>
    public void logPhaseBar(string tag)
    {
        if (!QuickTestTrace.Enabled)
        {
            return;
        }
        if (Phase == null || Phase.labDp == null)
        {
            QuickTestTrace.Log("field", "phasebar " + tag + " Phase=null");
            return;
        }
        UILabel[] ls = new UILabel[] { Phase.labDp, Phase.labSp, Phase.labMp1, Phase.labBp, Phase.labMp2, Phase.labEp };
        System.Text.StringBuilder xs = new System.Text.StringBuilder();
        System.Text.StringBuilder ws = new System.Text.StringBuilder();
        System.Text.StringBuilder ons = new System.Text.StringBuilder();
        System.Text.StringBuilder txs = new System.Text.StringBuilder();
        for (int i = 0; i < ls.Length; i++)
        {
            if (i > 0)
            {
                xs.Append('/');
                ws.Append('/');
                ons.Append('/');
                txs.Append('/');
            }
            xs.Append(ls[i].transform.localPosition.x.ToString("F1"));
            ws.Append(ls[i].width);
            ons.Append(ls[i].gameObject.activeSelf ? "1" : "0");
            txs.Append(ls[i].text);
        }
        // 可见标签的 x（按 dp→ep 顺序挑出 activeSelf 的），供「四格是否等距」直接验收。
        System.Text.StringBuilder vis = new System.Text.StringBuilder();
        for (int i = 0; i < ls.Length; i++)
        {
            if (!ls[i].gameObject.activeSelf)
            {
                continue;
            }
            if (vis.Length > 0)
            {
                vis.Append('/');
            }
            vis.Append(ls[i].transform.localPosition.x.ToString("F1"));
        }
        QuickTestTrace.Log("field", "phasebar " + tag
            + " rule=" + Program.I().ocgcore.MasterRule
            + " mode=" + GameModeManager.ModeLabel
            + " style=" + phaseBarStyle
            + " strip=" + PhaseBarStripName()
            + " names=dp/sp/mp1/bp/mp2/ep"
            + " x=" + xs + " w=" + ws + " on=" + ons + " text=" + txs
            + " vis=" + vis);
    }

    /// <summary>
    /// 底板上挂的到底是哪一张 —— **按引用相等判**，不看 <c>Texture2D.name</c>。
    ///
    /// 为什么不能用 name：贴图一律走 <c>UIHelper.getTexture2D()</c>，而它只
    /// <c>new Texture2D</c> + <c>LoadImage</c>，**从不设 name** ⇒ name 恒为空串。
    /// 早先这里回落成 "宽x高"，结果 phase 与 phase_rd 两张都是 1024×128，
    /// 日志里一样是 "1024x128" —— 底板换没换根本判不出来（2026-09-21 发现）。
    /// 引用相等才是硬判据：两张图是两个不同的 Texture2D 对象。
    /// </summary>
    private string PhaseBarStripName()
    {
        if (phaseTexure == null)
        {
            return "?";
        }
        if (phaseTexure.mainTexture == null)
        {
            return "none";
        }
        if (GameTextureManager.phaseRD != null
            && ReferenceEquals(phaseTexure.mainTexture, GameTextureManager.phaseRD))
        {
            return "phase_rd";
        }
        if (GameTextureManager.phase != null
            && ReferenceEquals(phaseTexure.mainTexture, GameTextureManager.phase))
        {
            return "phase";
        }
        return "foreign-" + phaseTexure.mainTexture.GetInstanceID();
    }

    /// <summary>
    /// `applyPhaseBarLayout` 最后选了哪一档 —— 只给探针看。
    /// 判据要咬「选择」本身，而不是它的副作用（摆位/底板各自都能被别处改回）。
    /// </summary>
    private string phaseBarStyle = "?";

    /// <summary>上一次落盘的阶段条快照 —— `realize()` 里只在**它变了**的时候才落一行。</summary>
    private string lastPhaseSig = null;

    /// <summary>被 `realize()` 每帧调用的「变了才报」钩子（见 <see cref="logPhaseBar"/>）。</summary>
    private void logPhaseBarWhenChanged()
    {
        if (!QuickTestTrace.Enabled || Phase == null || Phase.labDp == null)
        {
            return;
        }
        string sig = Phase.labDp.transform.localPosition.x.ToString("F1")
            + "|" + Phase.labSp.transform.localPosition.x.ToString("F1")
            + "|" + Phase.labMp1.transform.localPosition.x.ToString("F1")
            + "|" + Phase.labBp.transform.localPosition.x.ToString("F1")
            + "|" + Phase.labMp2.transform.localPosition.x.ToString("F1")
            + "|" + Phase.labEp.transform.localPosition.x.ToString("F1")
            + "|" + (Phase.labSp.gameObject.activeSelf ? 1 : 0)
            + (Phase.labMp2.gameObject.activeSelf ? 1 : 0)
            + "|" + phaseBarStyle
            + "|" + PhaseBarStripName();
        if (sig == lastPhaseSig)
        {
            return;
        }
        lastPhaseSig = sig;
        logPhaseBar("realize-change");
    }

    /// <summary>
    /// 待机特效（`p_hole_*`，灵摆区那两处）与 LP 数字该用哪一套落点：
    ///   true  = 「盘面内侧」那套（第 1/5 列的格位：x=∓10.1/9.62、z=∓11.5）
    ///   false = 「盘面外侧」那套（外挂列的位置：x=∓15.2/14.65、z=∓9）
    ///
    /// 判据不是 MasterRule，而是**牌堆占着哪一处**：
    ///   · OCG MR4+ ：牌堆在外侧 ⇒ 用内侧（老代码就是这个分支）
    ///   · OCG MR≤3 ：牌堆在外侧 ⇒ 用外侧
    ///   · **RD ⇒ 一律用外侧**：附属格（卡组/墓地/场地魔法/额外卡组）占着主区
    ///     第 1/5 列那一带，数字/特效落在盘面外沿的空白处 —— 那两列 RD 贴图不画格子。
    ///     「内侧」（x=∓10.1/9.62、z=∓9）那一点正好落在**两排之间的缝**上（那里是空的，
    ///     但塞进两格牌堆中间会显得挤）⇒ 用外侧。
    /// ⚠ 这几个点是**每帧重摆**的（Update 里）。
    /// </summary>
    private static bool useInnerPnumPositions
    {
        get
        {
            if (GameModeManager.IsRD)
            {
                return false;
            }
            return Program.I().ocgcore.MasterRule >= 4;
        }
    }

    bool P = false;
    public void relocatePnums(bool p)
    {
        if (Program.I().ocgcore.MasterRule >= 4)
        {
            P = p;
            if (p)
            {
                me_left_p_num.transform.localScale = new Vector3(2, 2, 2);
                me_left_p_num.transform.eulerAngles = new Vector3(Program.tableauLayFlat(30f), -45, 0);
                me_right_p_num.transform.localScale = new Vector3(2, 2, 2);
                me_right_p_num.transform.eulerAngles = new Vector3(Program.tableauLayFlat(30f), 45, 0);
                op_left_p_num.transform.localScale = new Vector3(2, 2, 2);
                op_left_p_num.transform.eulerAngles = new Vector3(Program.tableauLayFlat(0f), -45, 0);
                op_right_p_num.transform.localScale = new Vector3(2, 2, 2);
                op_right_p_num.transform.eulerAngles = new Vector3(Program.tableauLayFlat(0f), 45, 0);
            }
            else
            {
                me_left_p_num.transform.localScale = new Vector3(2, 2, 2);
                me_left_p_num.transform.eulerAngles = new Vector3(90, 0, 0);
                me_right_p_num.transform.localScale = new Vector3(2, 2, 2);
                me_right_p_num.transform.eulerAngles = new Vector3(90, 0, 0);
                op_left_p_num.transform.localScale = new Vector3(2, 2, 2);
                op_left_p_num.transform.eulerAngles = new Vector3(90, 0, 0);
                op_right_p_num.transform.localScale = new Vector3(2, 2, 2);
                op_right_p_num.transform.eulerAngles = new Vector3(90, 0, 0);
            }
        }
        else
        {
            P = p;
            if (p)
            {
                me_left_p_num.transform.localScale = new Vector3(2, 2, 2);
                me_left_p_num.transform.eulerAngles = new Vector3(Program.tableauLayFlat(0f), -45, 0);
                me_right_p_num.transform.localScale = new Vector3(2, 2, 2);
                me_right_p_num.transform.eulerAngles = new Vector3(Program.tableauLayFlat(0f), 45, 0);
                op_left_p_num.transform.localScale = new Vector3(2, 2, 2);
                op_left_p_num.transform.eulerAngles = new Vector3(Program.tableauLayFlat(0f), -45, 0);
                op_right_p_num.transform.localScale = new Vector3(2, 2, 2);
                op_right_p_num.transform.eulerAngles = new Vector3(Program.tableauLayFlat(0f), 45, 0);
            }
            else
            {
                me_left_p_num.transform.localScale = new Vector3(2, 2, 2);
                me_left_p_num.transform.eulerAngles = new Vector3(90, 0, 0);
                me_right_p_num.transform.localScale = new Vector3(2, 2, 2);
                me_right_p_num.transform.eulerAngles = new Vector3(90, 0, 0);
                op_left_p_num.transform.localScale = new Vector3(2, 2, 2);
                op_left_p_num.transform.eulerAngles = new Vector3(90, 0, 0);
                op_right_p_num.transform.localScale = new Vector3(2, 2, 2);
                op_right_p_num.transform.eulerAngles = new Vector3(90, 0, 0);
            }
        }

    }

    public void dispose()
    {
        Program.I().ocgcore.RemoveUpdateAction_s(Update);
        // 大框的三张变体是**每局现建**的（`new Texture2D` + `SetPixels32`，各 1024x819 ≈ 3.3MB），
        // 不显式销毁就会随「每局 new GameField()」一路堆积（撤回重开也走这条路）。
        for (int i = 0; i < maxBandTextures.Length; i++)
        {
            destroyMaximumBandTexture(i);
        }
    }

    private static Vector3 getGoodPosition()
    {
        return new Vector3(0, 0, 0);
    }

    bool prelong = false;
    float fieldSprite_height = 819f;
    public float delat = 0;

    float prereal = 0;

    public void Update()
    {
        delat = ((isLong ? (40f + 60f * ((1.21f - Program.fieldSize) / 0.21f)) :0f))/110f*5f;
        fieldSprite_height += ((isLong ? (819f + 40f + 60f * ((1.21f-Program.fieldSize) / 0.21f)) : 819f) - fieldSprite_height) * (Program.deltaTime * 4);
        midT.height = (int)fieldSprite_height;

        Vector3 position = midT.gameObject.transform.localPosition;
        position.y = fieldSprite_height - 819f;
        position.y /= 2;
        midT.gameObject.transform.localPosition = position;

        gameObject.transform.localPosition = getGoodPosition();
        gameObject.transform.localScale = new Vector3(Program.fieldSize, Program.fieldSize, Program.fieldSize);
        leftT.transform.localScale = new Vector3(1f / Program.fieldSize, 1f / Program.fieldSize, 1f / Program.fieldSize);
        leftT.transform.localPosition = new Vector3(((-1f + 1f / Program.fieldSize) * (float)(leftT.width)) / 3.5f, 0, 0);
        rightT.transform.localScale = new Vector3(1f / Program.fieldSize, 1f / Program.fieldSize, 1f / Program.fieldSize);
        rightT.transform.localPosition = new Vector3(((1f - 1f / Program.fieldSize) * (float)(rightT.width)) / 3.5f, 0, 0);

        relocateTextMesh(LOCATION_DECK_0, 0, CardLocation.Deck, new Vector3(0, 0, -3f));
        relocateTextMesh(LOCATION_EXTRA_0, 0, CardLocation.Extra, new Vector3(0, 0, -3f));
        relocateTextMesh(LOCATION_REMOVED_0, 0, CardLocation.Removed, new Vector3(0, 0, -3f));
        relocateTextMesh(LOCATION_GRAVE_0, 0, CardLocation.Grave, new Vector3(0, 0, -3f));

        relocateTextMesh(LOCATION_DECK_1, 1, CardLocation.Deck, new Vector3(0, 0, -3f));
        relocateTextMesh(LOCATION_EXTRA_1, 1, CardLocation.Extra, new Vector3(0, 0, -3f));
        relocateTextMesh(LOCATION_REMOVED_1, 1, CardLocation.Removed, new Vector3(0, 0, -3f));
        relocateTextMesh(LOCATION_GRAVE_1, 1, CardLocation.Grave, new Vector3(0, 0, -3f));

        label.transform.localPosition = new Vector3(-5f * (Program.fieldSize - 1), 0, -15.5f * Program.fieldSize);
        // ⛔ 介绍文字（「我方的 主要阶段1」这行）的旋转同样每帧跟 `tableauFrontX`：
        //    NGUI label 的 mod_simple_ngui_text prefab 根也带 60° 烘焙，切俯视角不重建
        //    ⇒ 只在 awake 写一次会停在 60°（同 relocateTextMesh，用户 2026-09-23 第 6 轮）。
        label.transform.eulerAngles = new Vector3(Program.tableauFrontX, 0, 0);

        if (prelong != isLong)
        {
            prelong = isLong;
            for (int i = 0; i < field_disabled_containers.Count; i++)
            {
                if (field_disabled_containers[i].p.location == (UInt32)CardLocation.SpellZone)
                {
                    if (field_disabled_containers[i].p.controller == 1)
                    {
                        field_disabled_containers[i].position = Program.I().ocgcore.get_point_worldposition(field_disabled_containers[i].p);
                        if (field_disabled_containers[i].game_object != null)
                        {
                            field_disabled_containers[i].game_object.transform.position = field_disabled_containers[i].position;
                        }
                    }
                }
            }
        }

        float real = (Program.fieldSize - 1) * 0.9f + 1f;
        if (mePHole)
        {
            if (p_hole_me == null)
            {
                p_hole_me = create(Program.I().mod_ocgcore_ss_p_idle_effect, new Vector3(0, 0, 0));
                p_hole_mel = p_hole_me.transform.Find("l");
                p_hole_mer = p_hole_me.transform.Find("r");
                prereal = 0;
            }
        }
        else
        {
            if (p_hole_me != null)
            {
                destroy(p_hole_me, 0, false, true);
                p_hole_mel = null;
                p_hole_mer = null;
                prereal = 0;
            }
        }
        if (opPHole)
        {
            if (p_hole_op == null)
            {
                p_hole_op = create(Program.I().mod_ocgcore_ss_p_idle_effect, new Vector3(0, 0, 0));
                p_hole_opl = p_hole_op.transform.Find("l");
                p_hole_opr = p_hole_op.transform.Find("r");
                prereal = 0;
            }
        }
        else
        {
            if (p_hole_op != null)
            {
                destroy(p_hole_op, 0, false, true);
                p_hole_opl = null;
                p_hole_opr = null;
                prereal = 0;
            }
        }
        if (prereal != real)
        {
            prereal = real;
            // 待机特效（灵摆区那两处）摆哪一侧：判据是**牌堆占着哪一处**，
            // 见 useInnerPnumPositions —— RD 一律让到外侧
            // （RD 没有灵摆，这个特效本来也不会亮，纯粹是别跟牌堆叠一起）。
            if (useInnerPnumPositions)
            {
                if (p_hole_mel != null && p_hole_mer != null)
                {
                    p_hole_mel.localPosition = new Vector3(-10.1f * real, 0, -11.5f * real);
                    p_hole_mer.localPosition = new Vector3(9.62f * real, 0, -11.5f * real);
                }
                if (p_hole_opl != null && p_hole_opr != null)
                {
                    p_hole_opl.localPosition = new Vector3(-10.1f * real, 0, 11.5f * real);
                    p_hole_opr.localPosition = new Vector3(9.62f * real, 0, 11.5f * real);
                }
            }
            else
            {
                if (p_hole_mel != null && p_hole_mer != null)
                {
                    p_hole_mel.localPosition = new Vector3(-15.2f * real, 0, -9f);
                    p_hole_mer.localPosition = new Vector3(14.65f * real, 0, -9f);
                }
                if (p_hole_opl != null && p_hole_opr != null)
                {
                    p_hole_opl.localPosition = new Vector3(-15.2f * real, 0, 9f);
                    p_hole_opr.localPosition = new Vector3(14.65f * real, 0, 9f);
                }
            }

        }
        // 生命值数字：同样跟牌堆「互相让位」（见 useInnerPnumPositions）——
        // RD 一律贴盘面外侧的空白处（牌堆占着第 1/5 列那一带）。
        if (useInnerPnumPositions)
        {
            if (P)
            {
                me_left_p_num.transform.position = UIHelper.getCamGoodPosition(new Vector3(-10.1f * real + 1, 5, -11.5f * real - 1), -3f);
                me_right_p_num.transform.position = UIHelper.getCamGoodPosition(new Vector3(9.62f * real - 1, 5, -11.5f * real - 1), -3f);
                op_left_p_num.transform.position = UIHelper.getCamGoodPosition(new Vector3(-10.1f * real + 1, 5, 11.5f * real - 1), -3f);
                op_right_p_num.transform.position = UIHelper.getCamGoodPosition(new Vector3(9.62f * real - 1, 5, 11.5f * real - 1), -3f);
            }
            else
            {
                me_left_p_num.transform.position = UIHelper.getCamGoodPosition(new Vector3(-10.1f * real, 0, -11.5f * real), -1f);
                me_right_p_num.transform.position = UIHelper.getCamGoodPosition(new Vector3(9.62f * real, 0, -11.5f * real), -1f);
                op_left_p_num.transform.position = UIHelper.getCamGoodPosition(new Vector3(-10.1f * real, 0, 11.5f * real), -1f);
                op_right_p_num.transform.position = UIHelper.getCamGoodPosition(new Vector3(9.62f * real, 0, 11.5f * real), -1f);
            }
        }
        else
        {
            if (P)
            {
                me_left_p_num.transform.position = UIHelper.getCamGoodPosition(new Vector3(-15.2f * real, 5, -10f), -3f);
                me_right_p_num.transform.position = UIHelper.getCamGoodPosition(new Vector3(14.65f * real, 5, -10f), -3f);
                op_left_p_num.transform.position = UIHelper.getCamGoodPosition(new Vector3(-15.2f * real, 5, 8f), -3f);
                op_right_p_num.transform.position = UIHelper.getCamGoodPosition(new Vector3(14.65f * real, 5, 8f), -3f);
            }
            else
            {
                me_left_p_num.transform.position = UIHelper.getCamGoodPosition(new Vector3(-15.2f * real, 0f, -9f), -1f);
                me_right_p_num.transform.position = UIHelper.getCamGoodPosition(new Vector3(14.65f * real, 0f, -9f), -1f);
                op_left_p_num.transform.position = UIHelper.getCamGoodPosition(new Vector3(-15.2f * real, 0f, 9f), -1f);
                op_right_p_num.transform.position = UIHelper.getCamGoodPosition(new Vector3(14.65f * real, 0f, 9f), -1f);
            }
        }

    }

    private static void relocateTextMesh(TMPro.TextMeshPro obj, uint con, CardLocation loc,Vector3 poi)
    {
        // ⛔ 旋转必须**每帧跟** `Program.tableauFrontX`（60° 视角=60、俯视角=90），不能只在
        //    awake 创建时写一次：文字 prefab（new_ui_textMesh）的根节点**自带 60° X 烘焙倾斜**，
        //    玩家在设置里切「俯视角（平面图）」时不会重建 gameField ⇒ 只写一次的话这批
        //    牌堆/墓地/除外的**计数文字会停在 60°**，正俯视下斜着 30°（用户 2026-09-23 第 6 轮）。
        //    Update() 本来就每帧重摆 position（见 877 行起），旋转跟着走一行不亏。
        obj.transform.eulerAngles = new Vector3(Program.tableauFrontX, 0, 0);
        obj.transform.position = UIHelper.getCamGoodPosition(Program.I().ocgcore.get_point_worldposition(new GPS
        {
            controller = con,
            location = (UInt32)loc
        }) + poi, -2);
    }

    public bool isLong = false;

    int[] fieldCode = new int[2] { 0, 0 };

    public void set(int player, int code)
    {
        if (player >= 0) if (player < 2)
            {
                if (fieldCode[player] != code)
                {
                    fieldCode[player] = code;
                    if (code > 0)
                    {
                        Texture2D tex = null;
                        bool found = false;
                        foreach (ZipFile zip in GameZipManager.Zips)
                        {
                            if (zip.Name.ToLower().EndsWith("script.zip"))
                                continue;
                            foreach (string file in zip.EntryFileNames)
                            {
                                if (Regex.IsMatch(file.ToLower(), "field/" + code.ToString() + "\\.(jpg|png)$"))
                                {
                                    MemoryStream ms = new MemoryStream();
                                    ZipEntry e = zip[file];
                                    e.Extract(ms);
                                    tex = new Texture2D(1024, 600);
                                    tex.LoadImage(ms.ToArray());
                                    found = true;
                                    break;
                                }
                            }
                            if (found)
                                break;
                        }
                        if (tex == null)
                        {
                            tex = UIHelper.getTexture2D("picture/field/" + code.ToString() + ".png");
                        }
                        if (tex == null)
                        {
                            tex = UIHelper.getTexture2D("picture/field/" + code.ToString() + ".jpg");
                        }
                        if (tex != null)
                        {
                            UIHelper.getByName<UITexture>(gameObject, "field_" + player.ToString()).mainTexture = tex;
                            UIHelper.clearITWeen(UIHelper.getByName(gameObject, "obj_" + player.ToString()));
                            iTween.ScaleTo(UIHelper.getByName(gameObject, "obj_" + player.ToString()), new Vector3(1, 1, 1), 0.5f);
                        }
                        else
                        {
                            UIHelper.clearITWeen(UIHelper.getByName(gameObject, "obj_" + player.ToString()));
                            iTween.ScaleTo(UIHelper.getByName(gameObject, "obj_" + player.ToString()), new Vector3(0, 0, 0), 0.5f);
                        }
                    }
                    else
                    {
                        UIHelper.clearITWeen(UIHelper.getByName(gameObject, "obj_" + player.ToString()));
                        iTween.ScaleTo(UIHelper.getByName(gameObject, "obj_" + player.ToString()), new Vector3(0, 0, 0), 0.5f);
                    }
                }
            }
    }

    GameObject cookie_dark_hole;

    int overCount = 0;
    public void shiftBlackHole(int a, Vector3 v = default(Vector3))
    {
        overCount += a;
        if (overCount < 0)
        {
            overCount = 0;
        }
        if (overCount > 2)
        {
            overCount = 2;
        }
        if (overCount == 2)
        {
            shiftBlackHole(true, v);
        }
    }

    public void shiftBlackHole(bool on,Vector3 v=default(Vector3))
    {
        if (on)
        {
            if (cookie_dark_hole == null)
            {
                Program.I().mod_ocgcore_ss_dark_hole.transform.localScale = Vector3.zero;
                cookie_dark_hole = create(Program.I().mod_ocgcore_ss_dark_hole, v);
                iTween.ScaleTo(cookie_dark_hole, new Vector3(6, 6, 6), 1f);
                cookie_dark_hole.transform.eulerAngles = new Vector3(90, 0, 0);
            }
        }
        else
        {
            if (cookie_dark_hole != null)
            {
                iTween.ScaleTo(cookie_dark_hole, iTween.Hash(
                                   "delay", 1f,
                                   "x", 0,
                                   "y", 0,
                                   "z", 0,
                                   "time", 1f
                                   ));
                iTween.MoveTo(cookie_dark_hole, iTween.Hash(
                                  "delay", 1f,
                                  "position", v,
                                  "time", 1f
                                  ));
                destroy(cookie_dark_hole, 1.4f);
            }
        }
    }

    string currentString = "";
    public void setHint(string hint)
    {
        currentString = "T" + Program.I().ocgcore.turns.ToString() + " " + hint;
        realize();
    }
    public void setHintLogical(string hint)
    {
        currentString = "T" + Program.I().ocgcore.turns.ToString() + " " + hint;
    }
    GameObject big_string;

    public void animation_show_big_string(Texture2D tex,bool only=false)    
    {
        if (Ocgcore.inSkiping) 
        {
            return;
        }
        if (only)   
        {
            destroy(big_string);
        }
        big_string = create(Program.I().New_phase,Program.I().ocgcore.centre(),Vector3.zero,false,Program.ui_main_2d,true,new Vector3(Screen.height / 1000f* Program.fieldSize, Screen.height / 1000f * Program.fieldSize, Screen.height / 1000f * Program.fieldSize));
        big_string.GetComponentInChildren<UITexture>().mainTexture = tex;
        Program.I().ocgcore.Sleep(40);
        big_string.AddComponent<animation_screen_lock2>();
        destroy(big_string, 3f);
    }

    //GameObject big_string;
    //public void animation_show_big_string(string str)
    //{
    //    if (this.big_string!=null) 
    //    {
    //        destroy(this.big_string);
    //    }
    //    big_string = create(Program.I().mod_ocgcore_card_number_shower);
    //    TMPro.TextMeshPro text_mesh = big_string.GetComponent<TMPro.TextMeshPro>();
    //    TMPro.TextContainer text_container = big_string.GetComponent<TMPro.TextContainer>();
    //    text_container.width = 60;
    //    text_container.height = 10;
    //    text_mesh.text = str;
    //    text_mesh.alignment = TMPro.TextAlignmentOptions.Center;


    //    Vector3 screenP = Program.camera_game_main.WorldToScreenPoint(Vector3.zero);
    //    screenP.z = 18f;
    //    int bun = Screen.height / 3;
    //    if (screenP.y > Screen.height / 2 + bun)
    //    {
    //        screenP.y = Screen.height / 2 + bun;
    //    }
    //    if (screenP.y < Screen.height / 2 - bun)
    //    {
    //        screenP.y = Screen.height / 2 - bun;
    //    }
    //    big_string.transform.position = Program.camera_game_main.ScreenToWorldPoint(screenP);

    //    big_string.AddComponent<animation_screen_lock2>();
    //    big_string.transform.localScale = Vector3.zero;
    //    iTween.ScaleTo(big_string, new Vector3(0.7f, 0.7f, 0.7f), 0.3f);
    //    iTween.RotateTo(big_string, new Vector3(60, 0, 0), 0.3f);
    //    iTween.ScaleTo(big_string, iTween.Hash(
    //                       "delay", 0.6f,
    //                       "x", 0,
    //                       "y", 0,
    //                       "z", 0,
    //                       "time", 0.3f
    //                       ));
    //    destroy(big_string, 3f);
    //    Program.I().ocgcore.Sleep(30);
    //}

    class field_disabled_container
    {
        public GPS p;
        public Vector3 position;
        public GameObject game_object;
        public bool disabled = false;
    }

    List<field_disabled_container> field_disabled_containers = new List<field_disabled_container>();

    public void set_point_disabled(GPS gps, bool disabled)
    {
        // RD：已经整格不显示的区域（最左最右两列）不再挂「禁用」标记 ——
        // 否则红圈会悬在空白的背景上（格子贴图已擦掉）。
        if (GameModeManager.IsRD && hiddenInRD(gps))
        {
            return;
        }

        //temp

        /*if (Program.I().ocgcore.MasterRule >= 4)
        {
            if (gps.location == (int)CardLocation.SpellZone)
            {
                if (gps.position == 0 || gps.position == 4)
                {
                    disabled = false;
                }
            }
        }*/

        field_disabled_container container = null;

        foreach (field_disabled_container cont in field_disabled_containers)
        {
            if (cont.p.controller == gps.controller)
            {
                if (cont.p.location == gps.location)
                {
                    if (cont.p.sequence == gps.sequence)
                    {
                        container = cont;
                        break;
                    }
                }
            }
        }

        if (container == null)
        {
            container = new field_disabled_container
            {
                p = gps,
                position = Program.I().ocgcore.get_point_worldposition(gps)
                
            };
            field_disabled_containers.Add(container);
        }

        container.disabled = disabled;


    }

    public enum ph { dp, sp, mp1, bp, mp2, ep };

    public ph currentPhase;

    public void realize()
    {
        if (Phase.colliderBp.enabled)   
        {
            Phase.labBp.gradientTop = Color.white;
        }
        else
        {
            Phase.labBp.gradientTop = Color.grey;
        }
        if (Phase.colliderEp.enabled)
        {
            Phase.labEp.gradientTop = Color.white;
        }
        else
        {
            Phase.labEp.gradientTop = Color.grey;
        }
        if (Phase.colliderMp2.enabled)
        {
            Phase.labMp2.gradientTop = Color.white;
        }
        else
        {
            Phase.labMp2.gradientTop = Color.grey;
        }
        Phase.labDp.gradientTop = Color.grey;
        Phase.labSp.gradientTop = Color.grey;
        Phase.labMp1.gradientTop = Color.grey;
        switch (currentPhase)
        {
            case ph.dp:
                Phase.labDp.gradientTop = Color.green;
                break;
            case ph.sp:
                Phase.labSp.gradientTop = Color.green;
                break;
            case ph.mp1:
                Phase.labMp1.gradientTop = Color.green;
                break;
            case ph.bp:
                Phase.labBp.gradientTop = Color.green;
                break;
            case ph.mp2:
                Phase.labMp2.gradientTop = Color.green;
                break;
            case ph.ep:
                Phase.labEp.gradientTop = Color.green;
                break;
        }
        for (int i = 0; i < field_disabled_containers.Count; i++)   
        {
            if (field_disabled_containers[i].disabled)
            {
                if (field_disabled_containers[i].game_object == null)
                {
                    field_disabled_containers[i].game_object = create(Program.I().mod_simple_quad, field_disabled_containers[i].position,new Vector3(90,0,0),false,null,true);
                    field_disabled_containers[i].game_object.transform.localScale = Vector3.zero;
                    iTween.ScaleTo(field_disabled_containers[i].game_object, new Vector3(4, 4, 4), 1f);
                    field_disabled_containers[i].game_object.GetComponent<Renderer>().material.mainTexture = GameTextureManager.negated;
                }
            }
            else
            {
                destroy(field_disabled_containers[i].game_object,0.6f,true,true);
            }
        }

        label.text = currentString;

        logPhaseBarWhenChanged();

        //if (Program.I().setting.setting.closeUp.value)
        //{
        //    if (label.gameObject.activeInHierarchy==false)  
        //    {
        //        label.gameObject.SetActive(true);
        //    }
        //    label.text = currentString;
        //}
        //else
        //{
        //    if (label.gameObject.activeInHierarchy == true)
        //    {
        //        label.gameObject.SetActive(false);
        //    }
        //}
    }

    public void clearDisabled() 
    {
        for (int i = 0; i < field_disabled_containers.Count; i++)
        {
            field_disabled_containers[i].disabled = false;
        }
    }

    public void animation_show_lp_num(int player, bool up, int count)   
    {
        int color = 0;
        if (up)
        {
            color = 3;
        }
        Vector3 position;
        Vector3 screen_p;
        if (player==0)
        {
            screen_p = new Vector3(Program.I().ocgcore.getScreenCenter(), 100f, 5);
            position = Program.camera_game_main.ScreenToWorldPoint(new Vector3(Program.I().ocgcore.getScreenCenter(), 100f, 5));
        }
        else
        {
            screen_p = new Vector3(Program.I().ocgcore.getScreenCenter(), Screen.height - 100f, 5);
            position = Program.camera_game_main.ScreenToWorldPoint(new Vector3(Program.I().ocgcore.getScreenCenter(), Screen.height - 100f, 5));
        }




        GameObject obj = create(Program.I().mod_ocgcore_number);
        obj.GetComponent<number_loader>().set_number(count, color);
        obj.AddComponent<animation_screen_lock>().screen_point = screen_p;
        obj.transform.position = position;
        obj.transform.localScale = Vector3.zero;
        obj.transform.eulerAngles = new Vector3(Program.tableauFrontX, 0, 0);
        iTween.ScaleTo(obj, new Vector3(1, 1, 1), 0.18f);
        destroy(obj, 1f);
    }


    public void animation_screen_blood(int player, int amount_) 
    {
        int amount = amount_;
        if (amount > 8000)
        {
            amount = 8000;
        }
        int count = ((int)amount) / 250;
        for (int i = 0; i < count; i++)
        {
            if (player == 0)
            {
                create(
                Program.I().mod_ocgcore_blood,
                new Vector3(
                    UnityEngine.Random.Range(-20, 20),
                    0,
                    UnityEngine.Random.Range(-5, -25)
                    )
                    );
            }
            else
            {
                create(
               Program.I().mod_ocgcore_blood,
                new Vector3(
                    UnityEngine.Random.Range(-20, 20),
                    0,
                    UnityEngine.Random.Range(5, 25)
                    )
                    );
            }
        }
        if (player == 0)
        {
            Program.I().ocgcore.Sleep((int)(60 * (float)amount / 2500f));
            iTween.ShakePosition(Program.camera_game_main.gameObject, iTween.Hash(
                "x", (float)amount / 1500f,
                "y", (float)amount / 1500f,
                "z", (float)amount / 1500f,
                "time", (float)amount / 2500f
                ));
            GameObject obj_ = create(Program.I().mod_ocgcore_blood_screen);
            obj_.AddComponent<animation_screen_lock>().screen_point =
                new Vector3(
                    Program.I().ocgcore.getScreenCenter(),
                    100f,
                    0.5f + 4000f / (float)amount);
            destroy(obj_, 2.5f);
            for (int i = 0; i < (int)amount / 1000; i++)
            {
                GameObject obj = create(Program.I().mod_ocgcore_blood_screen);
                obj.AddComponent<animation_screen_lock>().screen_point =
                    new Vector3(
                        (float)Screen.width / (float)UnityEngine.Random.Range(10, 30) * 10f,
                        (float)Screen.height / (float)UnityEngine.Random.Range(10, 30) * 10f,
                        0.5f + 4000f / (float)amount);
                destroy(obj, 2.5f);
            }
        }
    }

}
