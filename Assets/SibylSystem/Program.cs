using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System;
using Ionic.Zip;
using System.Text;

public class Program : MonoBehaviour
{

    #region Resources
    public Camera main_camera;
    public facer face;
    public Light light;
    public AudioSource audio;
    public AudioClip zhankai;
    public GameObject mod_ui_2d;
    public GameObject mod_ui_3d;
    public GameObject mod_winExplode;
    public GameObject mod_loseExplode;
    public GameObject mod_audio_effect;
    public GameObject mod_ocgcore_card;
    public GameObject mod_ocgcore_card_cloude;
    public GameObject mod_ocgcore_card_number_shower;
    public GameObject mod_ocgcore_card_figure_line;
    public GameObject mod_ocgcore_hidden_button;
    public GameObject mod_ocgcore_coin;
    public GameObject mod_ocgcore_dice;
    public GameObject mod_simple_quad;
    public GameObject mod_simple_ngui_background_texture;
    public GameObject mod_simple_ngui_text;
    public GameObject mod_ocgcore_number;
    public GameObject mod_ocgcore_decoration_chain_selecting;
    public GameObject mod_ocgcore_decoration_card_selected;
    public GameObject mod_ocgcore_decoration_card_selecting;
    public GameObject mod_ocgcore_decoration_card_active;
    public GameObject mod_ocgcore_decoration_spsummon;
    public GameObject mod_ocgcore_decoration_thunder;
    public GameObject mod_ocgcore_decoration_trap_activated;
    public GameObject mod_ocgcore_decoration_magic_activated;
    public GameObject mod_ocgcore_decoration_magic_zhuangbei;
    public GameObject mod_ocgcore_decoration_removed;
    public GameObject mod_ocgcore_decoration_tograve;
    public GameObject mod_ocgcore_decoration_card_setted;
    public GameObject mod_ocgcore_blood;
    public GameObject mod_ocgcore_blood_screen;
    public GameObject mod_ocgcore_bs_atk_decoration;
    public GameObject mod_ocgcore_bs_atk_line_earth;
    public GameObject mod_ocgcore_bs_atk_line_water;
    public GameObject mod_ocgcore_bs_atk_line_fire;
    public GameObject mod_ocgcore_bs_atk_line_wind;
    public GameObject mod_ocgcore_bs_atk_line_dark;
    public GameObject mod_ocgcore_bs_atk_line_light;
    public GameObject mod_ocgcore_cs_chaining;
    public GameObject mod_ocgcore_cs_end;
    public GameObject mod_ocgcore_cs_bomb;
    public GameObject mod_ocgcore_cs_negated;
    public GameObject mod_ocgcore_cs_mon_earth;
    public GameObject mod_ocgcore_cs_mon_water;
    public GameObject mod_ocgcore_cs_mon_fire;
    public GameObject mod_ocgcore_cs_mon_wind;
    public GameObject mod_ocgcore_cs_mon_light;
    public GameObject mod_ocgcore_cs_mon_dark;
    public GameObject mod_ocgcore_ss_summon_earth;
    public GameObject mod_ocgcore_ss_summon_water;
    public GameObject mod_ocgcore_ss_summon_fire;
    public GameObject mod_ocgcore_ss_summon_wind;
    public GameObject mod_ocgcore_ss_summon_dark;
    public GameObject mod_ocgcore_ss_summon_light;
    public GameObject mod_ocgcore_ol_earth;
    public GameObject mod_ocgcore_ol_water;
    public GameObject mod_ocgcore_ol_fire;
    public GameObject mod_ocgcore_ol_wind;
    public GameObject mod_ocgcore_ol_dark;
    public GameObject mod_ocgcore_ol_light;
    public GameObject mod_ocgcore_ss_spsummon_normal;
    public GameObject mod_ocgcore_ss_spsummon_ronghe;
    public GameObject mod_ocgcore_ss_spsummon_tongtiao;
    public GameObject mod_ocgcore_ss_spsummon_yishi;
    public GameObject mod_ocgcore_ss_spsummon_link;
    public GameObject mod_ocgcore_ss_p_idle_effect;
    public GameObject mod_ocgcore_ss_p_sum_effect;
    public GameObject mod_ocgcore_ss_dark_hole;
    public GameObject mod_ocgcore_ss_link_mark;
    public GameObject new_ui_menu;
    public GameObject new_ui_setting;
    public GameObject new_ui_book;
    public GameObject new_ui_selectServer;
    // ⚠ 占位字段，**不要删**。
    //
    // Unity 的 MonoBehaviour 序列化数据是按字段声明顺序读写的（字段名不进数据流），
    // 而这个字段后面跟着 new_ui_gameInfo / new_ui_cardDescription / … / remaster_* 一长串
    // UnityEngine.Object 引用。少一个字段，后面整块就错位：运行期会打印
    // 「A scripted object (probably Program?) has a different serialization layout
    //  when loading」，然后那批引用全部读成 null —— 表现为 selectDeck 构造时空引用、
    // 主菜单永远不显示，卡在启动画面。
    //
    // 本版不使用它，只求把位置占住。字段名可以随意改（名字不参与序列化）。
    public GameObject new_ui_legacySlot;
    public GameObject new_ui_gameInfo;
    public GameObject new_ui_cardDescription;
    public GameObject new_ui_search;
    public GameObject new_ui_searchDetailed;
    public GameObject new_ui_cardOnSearchList;
    public GameObject new_bar_changeSide;
    public GameObject new_bar_duel;
    public GameObject new_bar_room;
    public GameObject new_bar_editDeck;
    public GameObject new_bar_watchDuel;
    public GameObject new_bar_watchRecord;
    public GameObject new_mod_cardInDeckManager;
    public GameObject new_mod_tableInDeckManager;
    public GameObject new_ui_handShower;
    public GameObject new_ui_textMesh;
    public GameObject new_ui_superButton;
    public GameObject new_ui_superButtonTransparent;
    public GameObject new_ui_aiRoom;
    public GameObject new_ocgcore_field;
    public GameObject new_ocgcore_chainCircle;
    public GameObject new_ocgcore_wait;
    public GameObject new_mouse;
    public GameObject remaster_deckManager;
    public GameObject remaster_replayManager;
    public GameObject remaster_puzzleManager;
    public GameObject remaster_tagRoom;
    public GameObject remaster_room;
    public GameObject ES_1;
    public GameObject ES_2;
    public GameObject ES_2Force;
    public GameObject ES_3cancle;
    public GameObject ES_Single_multiple_window;
    public GameObject ES_Single_option;
    public GameObject ES_multiple_option;
    public GameObject ES_input;
    public GameObject ES_position;
    public GameObject ES_position3;
    public GameObject ES_Tp;
    public GameObject ES_Face;
    public GameObject ES_FS;
    public GameObject Pro1_CardShower;
    public GameObject Pro1_superCardShower;
    public GameObject Pro1_superCardShowerA;
    public GameObject New_arrow;
    public GameObject New_selectKuang;
    public GameObject New_chainKuang;
    public GameObject New_phase;
    public GameObject New_decker;
    public GameObject New_winCaculator;
    public GameObject New_winCaculatorRecord;
    public GameObject New_ocgcore_placeSelector;
    #endregion

    #region Initializement

    private static Program instance;

    public static Program I()
    {
        return instance;
    }

    public static int TimePassed()
    {
        return (int)(Time.time * 1000f);
    }

    private List<GameObject> allObjects = new List<GameObject>();

    void loadResource(GameObject g)
    {
        try
        {
            GameObject obj = GameObject.Instantiate(g) as GameObject;
            obj.SetActive(false);
            allObjects.Add(obj);
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.Log(e);
        }
    }

    void loadResources()
    {

        loadResource(mod_audio_effect);
        loadResource(mod_ocgcore_card);
        loadResource(mod_ocgcore_card_cloude);
        loadResource(mod_ocgcore_card_number_shower);
        loadResource(mod_ocgcore_card_figure_line);
        loadResource(mod_ocgcore_hidden_button);
        loadResource(mod_ocgcore_coin);
        loadResource(mod_ocgcore_dice);

        loadResource(mod_ocgcore_decoration_chain_selecting);
        loadResource(mod_ocgcore_decoration_card_selected);
        loadResource(mod_ocgcore_decoration_card_selecting);
        loadResource(mod_ocgcore_decoration_card_active);
        loadResource(mod_ocgcore_decoration_spsummon);
        loadResource(mod_ocgcore_decoration_thunder);
        loadResource(mod_ocgcore_cs_mon_earth);
        loadResource(mod_ocgcore_cs_mon_water);
        loadResource(mod_ocgcore_cs_mon_fire);
        loadResource(mod_ocgcore_cs_mon_wind);
        loadResource(mod_ocgcore_cs_mon_light);
        loadResource(mod_ocgcore_cs_mon_dark);
        loadResource(mod_ocgcore_decoration_trap_activated);
        loadResource(mod_ocgcore_decoration_magic_activated);
        loadResource(mod_ocgcore_decoration_magic_zhuangbei);

        loadResource(mod_ocgcore_decoration_removed);
        loadResource(mod_ocgcore_decoration_tograve);
        loadResource(mod_ocgcore_decoration_card_setted);
        loadResource(mod_ocgcore_blood);
        loadResource(mod_ocgcore_blood_screen);


        loadResource(mod_ocgcore_bs_atk_decoration);
        loadResource(mod_ocgcore_bs_atk_line_earth);
        loadResource(mod_ocgcore_bs_atk_line_water);
        loadResource(mod_ocgcore_bs_atk_line_fire);
        loadResource(mod_ocgcore_bs_atk_line_wind);
        loadResource(mod_ocgcore_bs_atk_line_dark);
        loadResource(mod_ocgcore_bs_atk_line_light);

        loadResource(mod_ocgcore_cs_chaining);
        loadResource(mod_ocgcore_cs_end);
        loadResource(mod_ocgcore_cs_bomb);
        loadResource(mod_ocgcore_cs_negated);

        loadResource(mod_ocgcore_ss_summon_earth);
        loadResource(mod_ocgcore_ss_summon_water);
        loadResource(mod_ocgcore_ss_summon_fire);
        loadResource(mod_ocgcore_ss_summon_wind);
        loadResource(mod_ocgcore_ss_summon_dark);
        loadResource(mod_ocgcore_ss_summon_light);

        loadResource(mod_ocgcore_ol_earth);
        loadResource(mod_ocgcore_ol_water);
        loadResource(mod_ocgcore_ol_fire);
        loadResource(mod_ocgcore_ol_wind);
        loadResource(mod_ocgcore_ol_dark);
        loadResource(mod_ocgcore_ol_light);

        loadResource(mod_ocgcore_ss_spsummon_normal);
        loadResource(mod_ocgcore_ss_spsummon_ronghe);
        loadResource(mod_ocgcore_ss_spsummon_tongtiao);
        loadResource(mod_ocgcore_ss_spsummon_link);
        loadResource(mod_ocgcore_ss_spsummon_yishi);
        loadResource(mod_ocgcore_ss_p_idle_effect);
        loadResource(mod_ocgcore_ss_p_sum_effect);
        loadResource(mod_ocgcore_ss_dark_hole);
        loadResource(mod_ocgcore_ss_link_mark);
    }

    public static float transparency = 0;

    //public static bool YGOPro1 = true;

    public static float getVerticalTransparency()
    {
        if (I().setting.setting.closeUp.value == false)
        {
            return 0;
        }
        return transparency;
    }

    public static GameObject ui_back_ground_2d = null;
    public static Camera camera_back_ground_2d = null;
    public static GameObject ui_container_3d = null;
    public static Camera camera_container_3d = null;
    public static Camera camera_game_main = null;
    public static GameObject ui_windows_2d = null;
    public static Camera camera_windows_2d = null;
    public static GameObject ui_main_2d = null;
    public static Camera camera_main_2d = null;
    public static GameObject ui_main_3d = null;
    public static Camera camera_main_3d = null;

    public static Vector3 cameraPosition = new Vector3(0, 23, -23);
    public static Vector3 cameraRotation = new Vector3(60, 0, 0);
    public static bool cameraFacing = false;

    // ================= 「俯视角（平面图）」全局杠杆 =================
    // 盘面本来就是**平铺**的（卡 prefab 的 card 子节点烘焙 X+90、get_world_rotation 给的 x 恒 0），
    // 倾斜感 100% 来自相机 pitch=60。所以「俯视角」= 相机抬到 90 + 把「面向镜头」的那批
    // 对象角度一起跟到 90（见 tableauFrontX / tableauAngle）。开关关掉时全部原样返回
    // ⇒ 与旧实现逐字段等价，旧判据一条都不用改。

    private static bool topDownValue = false;
    private static bool topDownConfigLoaded = false;

    /// <summary>
    /// 「俯视角（平面图）」开关，持久化在 Config 的 <c>topDown_</c>。
    /// **全局一个键（不分 RD/OCG）** —— 这是「看牌桌的视角偏好」，与规则差异无关。
    /// 读值自带惰性加载：没打开过设置窗口也能拿到存档值（`longField` 就漏了这一步）。
    /// </summary>
    public static bool topDown
    {
        get { EnsureTopDownLoaded(); return topDownValue; }
        set { topDownConfigLoaded = true; topDownValue = value; }
    }

    /// <summary>从 Config 读一次开关（幂等）。</summary>
    public static void EnsureTopDownLoaded()
    {
        if (topDownConfigLoaded)
        {
            return;
        }
        topDownConfigLoaded = true;
        topDownValue = UIHelper.fromStringToBool(Config.Get("topDown_", "0"));
    }

    /// <summary>牌桌倾角（= 相机 pitch）：<b>60 = 原来的倾斜态</b>，<b>90 = 俯视角（平面图）</b>。</summary>
    public static float tableauTilt
    {
        get { EnsureTopDownLoaded(); return topDownValue ? 90f : 60f; }
    }

    /// <summary>
    /// 「正向面对镜头」的 X 角：60° 相机下就是 60；俯视角下是 90 ——
    /// 正俯视（相机 pitch 90）下「正对镜头」= 平铺朝天，所以文字/标签照样正着读。
    /// 原本硬编码 <c>new Vector3(60, 0, 0)</c> 的标签、名牌、竖立绘、阶段大字等全部换成它。
    /// </summary>
    public static float tableauFrontX
    {
        get { return tableauTilt; }
    }

    /// <summary>
    /// 把「相对镜头的 X 角」换算到当前倾角：俯视角下整体 +30（保住「相对镜头的观感」不变）。
    /// 用在**卡牌自己的展示角**上（手牌/牌堆 -30、展示卡 -60、立起的 -90）。
    /// ⛔ 盘面上平铺的卡（<c>get_world_rotation</c> 给的 x 恒 0）**不要**用它 ——
    /// 平铺才是平面图；给它们 +30 会让卡背向镜头仰过去。
    /// </summary>
    public static float tableauAngle(float x)
    {
        return x + (tableauTilt - 60f);
    }

    /// <summary>
    /// 俯视角下**压平**（90），其余情况原样。
    /// 给那些「本来斜着立起来、但在平面图里应该贴回桌面」的装饰用 —— 目前是灵摆刻度数字
    /// （`gameField.relocatePnums`）：它们 p=true 时是 (30,±45)/(0,±45) 那种斜插的摆法，
    /// 正俯视下会缩成一半高，压平反而更清楚。
    /// ⛔ 与 <see cref="tableauAngle"/> 的区别是刻意的：标签类要「正对镜头」，
    ///    而这类数字要「贴桌面」。
    /// </summary>
    public static float tableauLayFlat(float x)
    {
        return topDown ? 90f : x;
    }

    /// <summary>
    /// 世界空间「**卡面上方**」的偏移 —— 卡上闪光、卡上数字、可发动按钮锚点**共用这一个入口**。
    ///
    /// <paramref name="half"/> = **这张卡当前的卡面半长（世界单位）**：基准卡是 2（face localScale
    /// y=4 ⇒ 半长 2），手牌/摊开行带在俯视角整体放大 1.5 倍 ⇒ 半长 3（`gameCard.faceHalfWorld`）。
    /// 「卡面上方 h」= 沿卡面上方向挪 `h × half` —— 即相对距离以**这张卡自己的半长**为单位，
    /// 跟着显示倍数走。
    ///
    /// ⛔⛔ 为什么必须带 half（用户 2026-09-23 第 5 轮实机）：
    ///   第 4 轮的写法是固定 `2h` 世界 —— 60° 下手牌不放大（透视负责放大），half=2，
    ///   偏移 2.4 = 半长的 1.2 倍 ⇒ 闪光悬在卡上沿之外 0.2 半长（`[cardup]` 实测 upPx=76 / halfPx=64）；
    ///   俯视角手牌放大 1.5 倍（half=3）后偏移没跟着长，2.4 < 3 ⇒ 闪光**掉回卡面里**
    ///   （实测 upPx=49 / halfPx=61，差 −12px vs 关态 +12px）—— 这就是「闪光还是不够高」。
    ///   偏移乘上 half 之后两视角都回到 +0.2 半长，与斜视角的相对距离一致。
    ///
    /// 方向为什么分视角：`(0, h, h·1.732)` 的 `1.732 = tan60°`，而 `(0, 1, 1.732)/2 = (0, 0.5, 0.866)`
    /// 正是**卡对象自己的上方向** —— 卡对象的显示角是 `tableauAngle(-30)`（相对桌面仰起 30°）。
    /// 正俯视（90°）下卡面被压平（`tableauAngle(-30)` → 0），卡面上方向变成**世界 +z**
    /// （= 屏幕上方：`[view] proj` 实测 20.3 px/世界）。
    /// </summary>
    public static Vector3 cardUpOffset(float h, float half)
    {
        return cardUpOffsetLen(h * half);
    }

    /// <summary>
    /// 沿「卡面上方向」挪 **L 世界**的偏移（不管卡的缩放，调用方自己乘好长度）——
    /// <see cref="cardUpOffset"/> 的核，也是非卡片锚点（`card_verticle_drawing` 上方的按钮行 /
    /// 等级数字）共用的入口：那些锚点的偏移量按展示图自己的尺寸算好了长度，直接给 L。
    /// 关态 = `(0, L/2, L/2·1.732)`（卡面上方向）；俯视 = `(0, 0, L)`（世界 +z）。
    /// </summary>
    public static Vector3 cardUpOffsetLen(float L)
    {
        if (!topDown)
        {
            float k = L * 0.5f;
            return new Vector3(0f, k, k * 1.732f);
        }
        return new Vector3(0f, 0f, L);
    }

    /// <summary>
    /// 俯视角下的取景：把 60° 语义的机位抬到**盘面中心（原点）正上方**，高度按「要装下多大范围」算。
    ///
    /// 公式只有一条：正俯视下相机在竖直方向能看到的**地面 z 半跨度** = `机高 × tan(fov/2)`，
    /// 也就是 `[view]` 探针里那个 <c>span</c>。所以取景不是「拉远系数」，而是直接声明
    /// **要看多大的范围** —— <see cref="topDownCoverZ"/>。想放大就调小它。
    ///
    /// ⛔ 为什么不沿用「60° 语义的机距 × 一个拉远系数」：那条路要求 <c>sqrt(y²+z²)</c> 稳定，
    ///   而 `cameraPosition.z` 会被 `Ocgcore.toNearest()` 在 -21.7 与 -17.5 之间来回夹
    ///   ⇒ 同一个系数算出的机高在 31.0 / 28.3 之间跳（实机日志 09:28:44 相邻两帧可见），
    ///   取景随镜头松紧漂移。改成按 fov 反算后，机高只由 coverZ 与 fov 决定。
    /// ⚠ fov 读**相机当前值**而不是写死 75：fov 是设置项，写死会让「覆盖范围」随 fov 漂。
    ///
    /// ⚠ **只在「对局视角」这一个消费点调用**（`fixALLcamerasPreFrame` 里带 isShowed 闸）：
    /// 卡组编辑界面自己也在写 `cameraPosition`，那台相机本来就是 90° 俯视，不能再换算一次。
    /// 好处是相机原有的写入点（初始化 / Ocgcore.show / toNearest）一行都不用动，
    /// `cameraPosition` 保持「60° 语义」的单一含义，也不会出现「换算两次」的漂移。
    /// </summary>
    public static Vector3 tableauCameraPosition(Vector3 pos60)
    {
        if (!topDown)
        {
            return pos60;
        }
        // 正俯视的注视点 = **盘面中心（原点）**。
        // ⛔ 不能沿用 60° 那个注视点：它是为斜视构图挑的，落点在自己这一侧（本机登录值
        //    z≈-8.4），正俯视下会把整块盘面顶到屏幕上半 —— 实机图
        //    `_probe_topdown/on.png` 一眼可见（上半屏是盘、下半屏是背景）。
        //    盘面本身以原点为中心（`[field] piles` 里 me deck z=-13.67 / op deck z=+13.67），
        //    所以「正对原点」就是「正对盘面中心」，不需要任何魔数。
        float fov = (camera_game_main != null && camera_game_main.fieldOfView > 1f)
            ? camera_game_main.fieldOfView : 75f;
        float height = topDownCoverZ / Mathf.Tan(fov * 0.5f * Mathf.Deg2Rad);
        // z 这一维就是「拉镜头」：正俯视（pitch 90 / yaw 0）下沿 z 平移相机 = **纯平移**，
        // 高度不变 ⇒ 屏上所有卡的大小一个像素都不变，只是取景窗口在桌面上滑动。
        // 量来自 <see cref="topDownPanZ"/>（滚轮驱动），与 60° 语义的 pos60.z 无关 ——
        // 这也让 `[view]` 那条日志的 `want.z` 直接就是当前平移量，探针不用另开字段。
        return new Vector3(0f, height, topDownPanZ);
    }

    /// <summary>
    /// 正俯视要装下的**地面 z 半跨度**（世界单位）—— 「俯视角看得多大」的**唯一取景旋钮**。
    /// 相机高度由它反算：`height = topDownCoverZ / tan(fov/2)`（见 tableauCameraPosition）；
    /// 屏幕尺度 = `493 / cover` px/世界（屏高 986 折半，正俯视与 fov 无关）。
    ///
    /// ⛔⛔ **一条几何事实（别再试图绕过它）**：正俯视下 y=0 平面是**纯缩放**，所以
    ///   「牌桌占屏高 = 14.6 / cover × 2」与「牌桌牌宽 = 3 × 493 / cover」**绑死**：
    ///   `占屏 × 牌宽 ≈ 常数`。想让牌桌占屏更小、同时牌桌上的卡更大 —— **数学上做不到**。
    ///   唯一能兼得的是保留透视（松倾角），但整表算过：75° 会把对方手牌压到 44px、牌桌牌
    ///   压到 58px（90° 是 70/70，60° 是 30/50）⇒ **不划算，倾角固定 90°**
    ///   （用户 2026-09-23「场地占了屏幕太大且牌又太小」反馈后的定案）。
    ///   ⇒ 手牌另走一条路：它在牌桌**外侧**，位置与缩放各自独立，见 <see cref="topDownHandMaxScale"/>。
    ///
    /// 取值沿革：**21.4**（首版：牌桌占屏 68%、牌桌牌 69px、手牌 69px —— 手牌与牌桌牌被拉平，
    /// 用户实测「手牌不易读」）⇒ **24.3**（用户选定：牌桌占屏 60%、牌桌牌 61px、手牌 91px）。
    /// 关态不受影响（`tableauCameraPosition` 直接返回 60° 机位）。
    /// </summary>
    public static float topDownCoverZ = 24.3f;

    /// <summary>
    /// 俯视角下的「拉镜头」＝把取景**沿桌面纵深平移**（世界单位；**负 = 看向自己这一侧**、
    /// 正 = 看向对方那一侧）。挂在鼠标滚轮上，消费点只有一处（`Ocgcore.preFrameFunction`）。
    ///
    /// **为什么是「平移」而不是「缩放」**（用户 2026-09-23 第 2 条口径）：
    /// 正俯视（pitch 90 / yaw 0）下相机**高度**只决定「一次看多大」= <see cref="topDownCoverZ"/>，
    /// 而沿 z 平移相机既不改高度、也不改朝向 ⇒ 屏上所有卡的大小**一个像素都不变**，
    /// 只是取景窗口在桌面上滑动。这正是用户要的「像斜位视角一样能拉镜头、**而不是**改变卡的大小」：
    /// 60° 时滚轮走 `cameraPosition.z`（相机沿桌面纵深滑，透视顺带把近处的卡放大），
    /// 俯视角下没有透视，同一根滚轮就退化成纯平移 —— 手感一致、没有副作用。
    ///
    /// 用途（用户点名的场景）：屏幕外沿那两排（我方手牌 |z|≈20.8、对方 ≈22.99，
    /// 以及**摊开展示、等你点「确认完毕」的那批确认卡**）—— 摊开那批会一排排往外堆到 |z| ≈ 50，
    /// 靠这根滚轮把取景挪过去看。
    ///
    /// ⛔⛔ **第 5 轮口径（2026-09-23，覆盖第 3/4 轮）**——单边行程 + 默认机位压到最底下：
    ///   ①「给下面的卡留点空位，把**默认机位**调整为**最底下**」⇒ 平时取景中心就停在
    ///     <see cref="topDownPanRestZ"/>（手牌行底缘之外再留 <see cref="topDownPanMargin"/> 的空位）；
    ///   ②「默认应该是没有什么**往上滑动**的机会，**不要去加上面的滑动距离**」⇒ 行程是**单边**的：
    ///     量程 = [自动值, 默认机位]，**上限就是默认机位本身**、绝不往上加
    ///     （旧写法 `Clamp(±lim)` 的 +lim 那半边就是「上面的滑动距离」，已删）；
    ///   ③「确认卡牌时计算卡牌数量再往下增加滑动范围，并**一口气弹到底**」⇒ 摊开态下取景
    ///     每帧贴住 <see cref="topDownPanNeed"/>（= 最外一行的底缘 + 空位），用户一滚轮就把
    ///     控制权交给玩家（<see cref="topDownPanUserTook"/>），收摊自动回到默认机位。
    ///   （第 4 轮的 `topDownPanIdleLimit`「平时留一格行程」随之作废：默认机位本身已在最底下，
    ///   量程退化成一个点，自然「没有什么滑动的机会」。）
    ///
    /// **⛔ 第 7 轮口径（2026-09-23，在 第 5 轮之上放宽一条）**——量程上限从「默认机位」放宽到
    ///   <see cref="topDownPanMaxZ"/>（+2.0，与 rest 对称的「对手手牌上缘空位」）：
    ///   「允许上滑一点（大致到让对手手牌离屏幕的距离相当于默认的自己手牌到屏幕边缘的距离）」。
    ///   闲态量程 = [rest, maxZ]（4 世界，滚一格就到顶）；摊开态量程 = [自动值, maxZ]。
    ///   另外「查看对方手牌」（对方手牌行出现公开牌面，检测条件与 realize 的朝向判据同一）
    ///   时**自动机位 = maxZ**、公开结束回到 rest —— 见 `Ocgcore.preFrameFunction` 的每帧跟随。
    ///
    /// ⛔ **不复用 `cameraPosition.z` 当平移量**，两个理由：
    ///   ① 那个值会被 `Ocgcore.toNearest()` 在 `[camera_min, camera_max]` 里夹，
    ///      fieldSize=1 时量程只有 3.15 世界宽 —— 根本不够把边上那排挪到中间；
    ///   ② 它还会被出牌/抽牌顺手改写 ⇒ 平移量跟着对局节奏乱跳，玩家滚一下、下一拍就弹回去。
    ///   独立一个量、独立一条量程之后，取景仍然只由 `topDownCoverZ` 与它两个旋钮决定。
    /// ⛔ 也不要拿它去改 `cameraPosition`：那个静态量必须保持「60° 语义」，
    ///   否则关掉俯视角时相机会落在被污染的位置上（`tableauCameraPosition` 的注释里有整条理由）。
    /// </summary>
    public static float topDownPanZ = 0f;

    /// <summary>
    /// 当前取景里**最外那一行的 z**（**带符号**；由 `Ocgcore.realize` 的行带循环算出来，每次 realize 重算）。
    /// 只为 <see cref="topDownPanNeed"/> 服务：行程要够得着最外那一行。
    /// </summary>
    public static float topDownRowOuter = 0f;

    /// <summary>
    /// 最外那张卡的**屏下空位**（世界单位，≈51px）：摊开/手牌行带的最外一张，它的**底缘**
    /// 与屏幕下沿之间留多少。用户 2026-09-23 第 5 轮：「底部距离也太短了」——
    /// 旧行程 `|outer| + 2 − cover` 恰好把最外一张的底缘压在屏幕边上（空位 = 0）。
    /// </summary>
    public static float topDownPanMargin = 2.5f;

    /// <summary>
    /// **默认机位**（「最底下」，带符号的取景中心 z）：正常情况下（行带只有手牌那一行）取景停在这里。
    ///
    /// 推导（全是现成常量，不引入新魔数）：手牌行 0 中心 |z| = `(cover − 0.5) − 2s` = 20.8
    /// （s = 1.5，见 `tableauHandRowAbs` 的行带模型）；显示倍数 1.5 的卡半长 = 3 ⇒ **底缘 23.8**；
    /// 再留 <see cref="topDownPanMargin"/> 的空位 ⇒ 机位 = −(23.8 + 2.5 − 24.3) = **−2.0**。
    /// 用户 2026-09-23 第 5 轮原话：「给下面的卡留点空位，所以把俯视角正常情况的机位的默认调整为最底下」
    /// —— 默认态下自动值 == 默认机位 ⇒ 量程退化成一个点，滚轮自然「没有什么滑动的机会」。
    /// </summary>
    public static float topDownPanRestZ
    {
        get
        {
            float row0 = (topDownCoverZ - 0.5f) - 2f * 1.5f;
            float edge = row0 + 2f * 1.5f;
            float r = edge + topDownPanMargin - topDownCoverZ;
            return r > 0f ? -r : 0f;
        }
    }

    /// <summary>
    /// **上滑上限**（第 7 轮新增，带符号的取景中心 z）：把**对方**手牌行的**上缘**挪到离屏幕上沿
    /// <see cref="topDownPanMargin"/> 空位所需的机位 z —— 与 <see cref="topDownPanRestZ"/> **完全对称**
    /// （对方手牌行与我方共用同一条行带模型 ⇒ 它的上缘 23.8 就是我方手牌行的底缘，
    /// 两个旋钮算出来互为相反数：rest = −2.0、max = +2.0）。
    /// 用户 2026-09-23 第 7 轮原话：「允许上滑一点（大致到让对手手牌离屏幕的距离相当于
    /// 默认的自己手牌到屏幕边缘的距离）」。滚轮一格（<see cref="topDownPanStep"/>=5）大于量程宽
    /// （4 世界）⇒ 闲态一滚就到顶，恰好是「查看对方手牌时默认滑动到那个距离」的那个距离。
    /// 「查看对方手牌」（对方手牌行出现公开牌面）的**自动机位**也用它，见
    /// `Ocgcore.preFrameFunction` 的每帧跟随。
    /// </summary>
    public static float topDownPanMaxZ
    {
        get
        {
            float row0 = (topDownCoverZ - 0.5f) - 2f * 1.5f; // 对方手牌行 |z| = 20.8（与我方同一行带）
            float edge = row0 + 2f * 1.5f;                   // 上缘 23.8（= 我方手牌行的底缘）
            float r = edge + topDownPanMargin - topDownCoverZ;
            return r > 0f ? r : 0f;
        }
    }

    /// <summary>
    /// 「拉镜头」的**自动值**（带符号）：把最外那一行的**底缘 + 空位**挪进取景所需的机位 z。
    /// ・正常态（最外 = 手牌行 0，|z| = 20.8）：算出来恰是 <see cref="topDownPanRestZ"/>
    ///   ⇒ 量程一个点，滚轮不动（用户：「默认没有什么往上滑动的机会」）。
    /// ・摊开等确认（最外 |z| ≈ 50.8）：−50.8 − 3 − 2.5 + 24.3 = **−32**，比旧口径的 −28.5
    ///   更深（卡半长按显示倍数算 3 而不是 2、外加 <see cref="topDownPanMargin"/>）。
    /// ⛔ 结果**朝默认机位方向夹取**：摊开只把量程往**那一侧**撑，绝不越过默认机位
    ///   （用户：「不要去加上面的滑动距离」）。
    /// </summary>
    public static float topDownPanNeed()
    {
        float z = topDownRowOuter;
        float half = 2f * 1.5f; // 行带显示倍数 1.5 × 基准卡面半长 2
        if (z < 0f)
        {
            float t = z - half - topDownPanMargin + topDownCoverZ;
            return t < topDownPanRestZ ? t : topDownPanRestZ;
        }
        if (z > 0f)
        {
            float t = z + half + topDownPanMargin - topDownCoverZ;
            return t > topDownPanRestZ ? t : topDownPanRestZ;
        }
        return topDownPanRestZ;
    }

    /// <summary>
    /// 「回到最近的取景」在俯视角下的等价动作 —— <c>Ocgcore.toNearest()</c> 里调它：
    /// 把平移量设回自动值 <see cref="topDownPanNeed"/>（摊开态 = 弹到最外一行、平时 = 默认机位）。
    /// ⚠ 第 5 轮起它不再是「弹到底」的唯一保证：`Ocgcore.preFrameFunction` 里有**每帧跟随**
    /// （摊开态且用户没滚过 ⇒ 每帧贴住自动值），实机里「有的摊开路径不走 toNearest /
    /// 调用时机早于 `someCardIsShowed` 翻转或 `topDownRowOuter` 重算」导致「自动弹到底没生效」
    /// 的场合由那条兜住。
    /// </summary>
    public static void topDownPanAuto()
    {
        if (!topDown)
        {
            return;
        }
        topDownPanZ = topDownPanNeed();
    }

    /// <summary>
    /// 用户**手动滚过没有**（本状态段里）：滚过就把取景的控制权交给玩家（每帧跟随停手），
    /// 直到**状态切换**（摊开↔收摊、「查看对方手牌」开始↔结束，见
    /// `Ocgcore.preFrameFunction`）或显式复位点（clearAllShowed / 新对局 / 设置里切视角）才交回。
    /// 第 7 轮起闲态也吃这个标志 —— 闲态上滑到 <see cref="topDownPanMaxZ"/> 后要**停留**，
    /// 不能每帧被贴回 rest（那就是「滚不动」）。
    /// </summary>
    public static bool topDownPanUserTook = false;

    /// <summary>
    /// 滚轮**一格**挪多少世界单位。取 5（≈102 px）：摊开量程 30 世界 ⇒ 一格 ≈ 1/6，拉起来有分辨率。
    ///
    /// ⛔ 这里**只**用它的**大小当步长**，与 `Program.wheelValue` 的量纲**无关** ——
    ///   一格实际报多少随机器与轴灵敏度变（本机实测 20，而 60° 那条路的量程只有 3.15 世界），
    ///   所以 `topDownPanBy` **只取 `wheelValue` 的方向**，不拿它的绝对值做比例换算。
    /// </summary>
    public static float topDownPanStep = 5f;

    /// <summary>滚轮驱动一次平移：**一格挪一步**（只取方向），量程 = [自动值, 上滑上限]（第 7 轮起上限 =
    /// <see cref="topDownPanMaxZ"/>，与默认机位对称的「对手手牌空位」；第 5 轮时上限曾是默认机位本身）。</summary>
    public static void topDownPanBy(float wheel)
    {
        // ⛔ 只取 `wheel` 的**方向**，不按它的绝对值换算：`Program.wheelValue` 一格有多大随机器
        //    与轴灵敏度变（本机实测 **20**）。Unity 的 `ScrollWheel` 轴是「本帧增量」⇒
        //    一个非零 delta 天然就是一格；急滚只挪一步，但每帧一步 ≈101px 已经足够顺。
        if (Mathf.Abs(wheel) < 0.0001f)
        {
            return;
        }
        float need = topDownPanNeed();
        float lo = need < topDownPanRestZ ? need : topDownPanRestZ;
        // 第 7 轮：上限从「默认机位 rest」放宽到 **topDownPanMaxZ**（+2.0，对手手牌上缘空位）——
        // 闲态也允许上滑一格看一眼对面手牌行（用户：「允许上滑一点…查看对方手牌时默认滑动到
        // 那个距离」）。摊开把行程往 −z 侧撑（need ≤ rest 恒成立），+z 侧只到 maxZ 为止。
        float hi = need > topDownPanMaxZ ? need : topDownPanMaxZ;
        float dir = wheel > 0f ? 1f : -1f;
        topDownPanZ = Mathf.Clamp(topDownPanZ + dir * topDownPanStep, lo, hi);
        topDownPanUserTook = true;
    }

    /// <summary>
    /// 俯视角下**手牌 / 展示行**的放大倍数上限（卡面 3.0 世界 × s = 屏上宽度）。
    ///
    /// **为什么必须有这个旋钮**：60° 时手牌正好靠在相机跟前，透视把它放大到 31.9 px/世界
    /// （卡宽 95.6px、节距 159.3px，探针 `[hc]` 落点实测），而同一屏里牌桌上的卡只有
    /// 16.5 px/世界（卡宽 ≈50px）⇒ **60° 的手牌天生是牌桌牌的 1.9 倍**。正俯视没有透视，
    /// 全场一律 20.3 px/世界 ⇒ 手牌退化到与牌桌牌一样大（各 61~69px）—— 这就是用户
    /// 「手牌不易读」的**全部**原因。手牌排在牌桌外侧、位置与缩放都独立 ⇒ 单独放大它，
    /// 把 60° 的那条观感做回来（这是唯一不与 `topDownCoverZ` 的零和约束冲突的杠杆）。
    ///
    /// ⛔ **放大必须「卡 + 行距」一起放大**：`get_left_right_indexEnhanced` 的落点按 left/right
    ///   半宽**线性分配**，所以 `Ocgcore` 里 x 半宽也要乘 s。只放大卡不放大行距就会互相压住 ——
    ///   line0 在**恰 6 张**那一档行距只有 `20/(6-1) = 4.0`（≤5 张才是 5.0），而卡宽 3×1.5 = 4.5。
    ///   ⛔ 别信「`(…, 5)` 那个参数 = 行距 5」：它是 `illusion`（几档不压缩的阈值），不是间距。
    ///
    /// 取 **1.5**：卡宽 91.1px、节距 151.9px —— 对照 60° 的 95.6 / 159.3，两样都追到 95%。
    /// 再往大只是「更大」，没有几何上限拦着（行距跟着走，行带还能到 2.2）——
    /// 1.5 是刻意保守（手牌/牌桌牌 = 1.5，比 60° 的 1.9 收一点，别喧宾夺主）。
    /// ⛔ 真正生效的值还要被「行带深度」再压一手（多行时更小）：见 <see cref="tableauHandScale"/>。
    /// </summary>
    public static float topDownHandMaxScale = 1.5f;

    /// <summary>
    /// 俯视角下「手牌 / 展示行」这一带可用的世界深度：
    /// 内沿 = 牌桌外沿 14.6 + 0.4 余量，外沿 = 屏幕边（cover）− 0.5 余量。
    /// </summary>
    public static float topDownHandBand()
    {
        return (topDownCoverZ - 0.5f) - (14.6f + 0.4f);
    }

    /// <summary>
    /// 该侧手牌 / 展示行在俯视角下的**放大倍数** —— **恒为** <see cref="topDownHandMaxScale"/>。
    ///
    /// ⛔⛔ **不再按行数缩小**（用户 2026-09-23 第二次实测后否掉了旧口径）。
    ///   旧口径是「nLines 行都塞进 <see cref="topDownHandBand"/>：`4·s·n ≤ band`，塞不下就整体缩小」，
    ///   它解决了「展示行第二行整行出屏」，但代价在**摊开**时爆炸：点自己卡堆摊开一副 40 张的卡组
    ///   ⇒ 每行 8 张 ⇒ **6 行** ⇒ `s = 8.8/24 = 0.367`，卡宽只剩 3.0×0.367 = **18 px**
    ///   （手牌是 91 px）。用户的观感就是「一确认，卡全被缩小了」。
    ///   定稿口径：「像斜视角一样」—— 斜视角（关态）根本没有这条缩放（本函数关态恒返回 1），
    ///   它是「行往外堆、出屏就出屏，想看就把镜头拉过去」⇒ 见 <see cref="tableauHandRowAbs"/>
    ///   与 <see cref="topDownPanZ"/>（滚轮拉镜头：**平时行程很小，摊开时自动扩并弹过去**）。
    ///
    /// ⚠ 关态恒返回 1 ⇒ 逐字段等价于旧实现（`UA_give_scale(1)` 只还原自己放大过的卡）。
    /// </summary>
    public static float tableauHandScale(int nLines)
    {
        if (!topDown) return 1f;
        return topDownHandMaxScale;
    }

    /// <summary>
    /// 该侧手牌 / 展示行里**第 k 行**的 |z|（k=0 = 贴屏幕外沿那一行，k 越大越**往外** = 会出屏那侧）。
    /// 行距 = 5s（第 8 轮起；= 60° 原生展示行的节距 5，别再压缩）：`|z| = (cover − 0.5) − 2s + 5k·s`。
    ///
    /// ⛔ **第 8 轮口径（2026-09-23）**：行距从 4s 放宽到 5s。旧行距下相邻两行的边缘**恰好相切**
    ///   （间距 4s = 6，而放大后一张卡占 2s + 2s = 6，s = 1.5）⇒ 摊开那排（确认卡）与手牌行
    ///   贴死，用户报「被选择的牌又和手牌太近」。5s 与 60° 原生节距一致（60°：节距 5、卡半长
    ///   2+2 = 4 ⇒ 缝 1 世界；俯视角 s = 1.5：节距 7.5、占 6 ⇒ 缝 1.5 世界）。k = 0（手牌行
    ///   本身）不受影响（5·0·s = 4·0·s = 0），rest/maxZ 也不动；摊开自动行程吃
    ///   <see cref="topDownRowOuter"/> 现算，行距变宽它自动跟着长。
    ///
    /// 🔑 **与 60°（关态）同向**：第 0 行贴外沿、第二行更往外 —— 这正是用户 2026-09-23 说的
    ///   「参考斜视角正常的做法」：斜视角里摊开那批卡本来就是往外的、出屏就出屏，
    ///   想看把镜头拉过去。俯视角把这件「拉镜头」做成了滚轮（<see cref="topDownPanZ"/>）——
    ///   **摊开这类会出屏的场景**量程自动扩到够得着（<see cref="topDownPanNeed"/>），
    ///   摊开期间取景每帧贴住自动值（`Ocgcore.preFrameFunction` 的每帧跟随）⇒ 出屏的行**够得着**。
    /// ⛔ 曾经是「往里堆」（`(cover−0.5) − (2+4k)s`）＋「按行数缩小」：那套能保证所有行都在屏内，
    ///   代价是 40 张卡组摊开（6 行）被压到 0.367 倍（卡宽 18 px）—— 用户实测否掉了。
    ///   行改成往外之后不再需要塞进 <see cref="topDownHandBand"/> ⇒ 缩放恒满档。
    /// 关态恒返回原生值 ⇒ 逐字段等价于旧实现。
    /// </summary>
    public static float tableauHandRowAbs(float nativeAbsZ, int nLines, int k, float s)
    {
        if (!topDown) return nativeAbsZ;
        return (topDownCoverZ - 0.5f) - 2f * s + 5f * k * s;
    }

    /// <summary>
    /// 直接攻击（打玩家）时箭头指到的 |z|：关态 = 原生那两条式子，开态 = 俯视角手牌排的**外沿**。
    /// ⛔ 不换的话箭头会飞到取景范围之外（原生 −23.15 / +24.99 vs cover 24.3）⇒ 看不见。
    /// </summary>
    public static float tableauAttackTargetAbsZ(float nativeAbsZ)
    {
        if (!topDown) return nativeAbsZ;
        return topDownCoverZ - 0.5f;
    }

    /// <summary>相机姿态：只把 pitch 抬到当前倾角，偏航/翻滚照旧（原来恒 <c>(60,0,0)</c>）。</summary>
    public static Vector3 tableauCameraRotation(Vector3 rot60)
    {
        return new Vector3(tableauTilt, rot60.y, rot60.z);
    }

    public static float verticleScale = 5f;

    void initialize()
    {

        go(1, () =>
        {
            UIHelper.iniFaces();
            initializeALLcameras();
            fixALLcamerasPreFrame();
            backGroundPic = new BackGroundPic();
            servants.Add(backGroundPic);
            backGroundPic.fixScreenProblem();
        });
        go(300, () =>
        {
            InterString.initialize("config/translation.conf");
            GameTextureManager.initialize();
            Config.initialize("config/config.conf");
            // 卡组目录是运行期依赖：OCG 的 deck/ 由构建铺设，RD 的 deck_rd/ 只能运行期建。
            // 冷启动也要保证在（选卡组界面直接 DirectoryInfo 枚举，目录不在会抛）。
            GameModeManager.EnsureDeckDir();

            // 读盘之前先兑现上次没走完的数据更新事务：否则可能把「新一半旧一半」的
            // 三件套读进内存。回滚失败会置 Failed，之后的更新流程不会再动数据。
            ClientDataUpdater.RecoverTransaction();

            // RD 数据的 ypk 更新通道：rd/update/*.ypk 在任何数据库装载之前应用，
            // 装载读到的就是新数据（见 RdDataUpdater 头注释的包格式与失败口径）。
            RdDataUpdater.ApplyPendingPacks();

            if (!Directory.Exists("expansions"))
            {
                try
                {
                    Directory.CreateDirectory("expansions");
                }
                catch
                {
                }
            }

            if (!Directory.Exists("replay"))
            {
                try
                {
                    Directory.CreateDirectory("replay");
                }
                catch
                {
                }
            }

            var fileInfos = new FileInfo[0];

            // 起手先把上一轮遗留的调试开关作废掉（见 QuickTestTrace.SweepStaleSwitches）：
            // 残留的 qt_endduel.on 会让卡组编辑器一打开就自动开局，
            // 残留的 qt_autojoin.on 会让房间自动准备并自动答掉猜拳。
            QuickTestTrace.SweepStaleSwitches();

            // 启动耗时台账（只记数，不参与逻辑）：两池各花多久、各多少张。
            // 用来回答「RD 那套数据有没有拖慢 OCG 的启动」。
            System.Diagnostics.Stopwatch bootWatch = System.Diagnostics.Stopwatch.StartNew();

            if (Directory.Exists("expansions"))
            {
                fileInfos = (new DirectoryInfo("expansions")).GetFiles();
                foreach (FileInfo file in fileInfos)
                {
                    if (file.Name.ToLower().EndsWith(".ypk"))
                    {
                        GameZipManager.Zips.Add(new Ionic.Zip.ZipFile("expansions/" + file.Name));
                    }
                    if (file.Name.ToLower().EndsWith(".conf"))
                    {
                        GameStringManager.initialize("expansions/" + file.Name);
                    }
                    if (file.Name.ToLower().EndsWith(".cdb"))
                    {
                        YGOSharp.CardsManager.initialize("expansions/" + file.Name);
                    }
                }
            }

            if (Directory.Exists("cdb"))
            {
                fileInfos = (new DirectoryInfo("cdb")).GetFiles();
                foreach (FileInfo file in fileInfos)
                {
                    if (file.Name.ToLower().EndsWith(".conf"))
                    {
                        GameStringManager.initialize("cdb/" + file.Name);
                    }
                    if (file.Name.ToLower().EndsWith(".cdb"))
                    {
                        YGOSharp.CardsManager.initialize("cdb/" + file.Name);
                    }
                }
            }

            if (Directory.Exists("diy"))
            {
                fileInfos = (new DirectoryInfo("diy")).GetFiles();
                foreach (FileInfo file in fileInfos)
                {
                    if (file.Name.ToLower().EndsWith(".conf"))
                    {
                        GameStringManager.initialize("diy/" + file.Name);
                    }
                    if (file.Name.ToLower().EndsWith(".cdb"))
                    {
                        YGOSharp.CardsManager.initialize("diy/" + file.Name, true);
                    }
                }
            }

            if (Directory.Exists("data"))
            {
                fileInfos = (new DirectoryInfo("data")).GetFiles();
                foreach (FileInfo file in fileInfos)
                {
                    if (file.Name.ToLower().EndsWith(".zip"))
                    {
                        GameZipManager.Zips.Add(new Ionic.Zip.ZipFile("data/" + file.Name));
                    }
                }
            }

            foreach (ZipFile zip in GameZipManager.Zips)
            {
                if (zip.Name.ToLower().EndsWith("script.zip"))
                    continue;
                foreach (string file in zip.EntryFileNames)
                {
                    if (file.ToLower().EndsWith(".conf"))
                    {
                        MemoryStream ms = new MemoryStream();
                        ZipEntry e = zip[file];
                        e.Extract(ms);
                        GameStringManager.initializeContent(Encoding.UTF8.GetString(ms.ToArray()));
                    }
                    if (file.ToLower().EndsWith(".cdb"))
                    {
                        ZipEntry e = zip[file];
                        string tempfile = Path.Combine(Path.GetTempPath(), file);
                        e.Extract(Path.GetTempPath(), ExtractExistingFileAction.OverwriteSilently);
                        YGOSharp.CardsManager.initialize(tempfile, true);
                        File.Delete(tempfile);
                    }
                }
            }

            // ===== RD（超速决斗）数据通道：只认 rd/ 子目录，全部进 RD 池 =====
            // 路径与文件由 _unpack_rd.py 分发（见 _plan_rdmode.md §2「数据分发」）。
            // 目录不存在 = 没装 RD 数据 → 静默跳过：RD 模式会显示空卡池，而不是起不来。
            QuickTestTrace.Log("boot", "OCG 池装载 " + YGOSharp.CardsManager.CountOf(false)
                + " 张，耗时 " + bootWatch.ElapsedMilliseconds + "ms");
            bootWatch.Restart();
            LoadRdDatabase();
            QuickTestTrace.Log("boot", "RD 池装载 " + YGOSharp.CardsManager.CountOf(true)
                + " 张，耗时 " + bootWatch.ElapsedMilliseconds + "ms"
                + "；两池合计 " + (YGOSharp.CardsManager.CountOf(false) + YGOSharp.CardsManager.CountOf(true))
                + " 张");

            GameStringManager.initialize("config/strings.conf");
            YGOSharp.BanlistManager.initialize("config/lflist.conf");
            // RD 禁限表走**单独一个文件**追加，不并进 config/lflist.conf：
            // config/ 每次构建都被工程模板覆盖，在线卡表更新（ClientDataUpdater）也会整文件替换
            // —— 并进去的 RD 表会隔三差五消失。rd/ 是随包分发的数据，稳。
            YGOSharp.BanlistManager.AppendIfExists("rd/lflist.conf");

            YGOSharp.CardsManager.updateSetNames();

            if (Directory.Exists("pack"))
            {
                fileInfos = (new DirectoryInfo("pack")).GetFiles();
                foreach (FileInfo file in fileInfos)
                {
                    if (file.Name.ToLower().EndsWith(".db"))
                    {
                        YGOSharp.PacksManager.initialize("pack/" + file.Name);
                    }
                }
                YGOSharp.PacksManager.initializeSec();
            }

            initializeALLservants();
            loadResources();
            readParams();

            // 启动即检查在线数据更新（卡片库/禁限表/卡牌文本三件套）。
            // 不阻塞启动；CDN 不可达时只是把状态标成失败，不影响进游戏。
            StartCoroutine(ClientDataUpdater.UpdateCoroutine());
        });

    }

    /// <summary>
    /// RD 卡表装载：rd/cdb/*.cdb → CardsManager 的 **RD 池**（与 OCG 池彻底分离）。
    ///
    /// 启动链与 ReloadGameDatabases 共用这一处 —— 别再各写一份（同一个坑会修两遍）。
    /// rd/ 不在构建铺设名单里（卡图/立绘 ~700MB，不想每次构建都拷），
    /// 由 <c>_unpack_rd.py</c> 分发；重建 output/ 之后必须重跑那个脚本。
    /// </summary>
    private void LoadRdDatabase()
    {
        if (!Directory.Exists("rd/cdb"))
        {
            return;
        }
        FileInfo[] files = (new DirectoryInfo("rd/cdb")).GetFiles();
        for (int i = 0; i < files.Length; i++)
        {
            if (files[i].Name.ToLower().EndsWith(".cdb"))
            {
                YGOSharp.CardsManager.initializeRD("rd/cdb/" + files[i].Name);
            }
        }
    }

    /// <summary>
    /// 按启动时完全相同的顺序重建内存里的数据：清空三件套后重放
    /// expansions → cdb → diy → data 里的 zip → config，最后 updateSetNames。
    /// 顺序不能改：后面几层是叠加在 cdb/cards.cdb 之上的补充与覆盖。
    /// 只重读文件、不重装 Unity 资源，所以可以在游戏内直接生效、无需重启。
    /// </summary>
    public bool ReloadGameDatabases()
    {
        try
        {
            GameStringManager.Reset();
            YGOSharp.CardsManager.Reset();
            YGOSharp.BanlistManager.Reset();

            var fileInfos = new FileInfo[0];

            if (Directory.Exists("expansions"))
            {
                fileInfos = (new DirectoryInfo("expansions")).GetFiles();
                foreach (FileInfo file in fileInfos)
                {
                    if (file.Name.ToLower().EndsWith(".conf"))
                    {
                        GameStringManager.initialize("expansions/" + file.Name);
                    }
                    if (file.Name.ToLower().EndsWith(".cdb"))
                    {
                        YGOSharp.CardsManager.initialize("expansions/" + file.Name);
                    }
                }
            }

            if (Directory.Exists("cdb"))
            {
                fileInfos = (new DirectoryInfo("cdb")).GetFiles();
                foreach (FileInfo file in fileInfos)
                {
                    if (file.Name.ToLower().EndsWith(".conf"))
                    {
                        GameStringManager.initialize("cdb/" + file.Name);
                    }
                    if (file.Name.ToLower().EndsWith(".cdb"))
                    {
                        YGOSharp.CardsManager.initialize("cdb/" + file.Name);
                    }
                }
            }

            if (Directory.Exists("diy"))
            {
                fileInfos = (new DirectoryInfo("diy")).GetFiles();
                foreach (FileInfo file in fileInfos)
                {
                    if (file.Name.ToLower().EndsWith(".conf"))
                    {
                        GameStringManager.initialize("diy/" + file.Name);
                    }
                    if (file.Name.ToLower().EndsWith(".cdb"))
                    {
                        YGOSharp.CardsManager.initialize("diy/" + file.Name, true);
                    }
                }
            }

            // zip 句柄启动时已装进 GameZipManager.Zips，这里只重放内容，不重新打开文件。
            foreach (ZipFile zip in GameZipManager.Zips)
            {
                if (zip.Name.ToLower().EndsWith("script.zip"))
                {
                    continue;
                }
                foreach (string entry in zip.EntryFileNames)
                {
                    if (entry.ToLower().EndsWith(".conf"))
                    {
                        MemoryStream ms = new MemoryStream();
                        zip[entry].Extract(ms);
                        GameStringManager.initializeContent(Encoding.UTF8.GetString(ms.ToArray()));
                    }
                    if (entry.ToLower().EndsWith(".cdb"))
                    {
                        string tempfile = Path.Combine(Path.GetTempPath(), entry);
                        zip[entry].Extract(Path.GetTempPath(), ExtractExistingFileAction.OverwriteSilently);
                        YGOSharp.CardsManager.initialize(tempfile, true);
                        File.Delete(tempfile);
                    }
                }
            }

            LoadRdDatabase();

            GameStringManager.initialize("config/strings.conf");
            YGOSharp.BanlistManager.initialize("config/lflist.conf");
            YGOSharp.BanlistManager.AppendIfExists("rd/lflist.conf");
            YGOSharp.CardsManager.updateSetNames();

            return true;
        }
        catch (Exception e)
        {
            DEBUGLOG(e);
            return false;
        }
    }

    void readParams()
    {
        var args = Environment.GetCommandLineArgs();
        string nick = null;
        string host = null;
        string port = null;
        string password = null;
        string deck = null;
        string replay = null;
        string puzzle = null;
        bool join = false;
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i].ToLower() == "-n" && args.Length > i + 1)
            {
                nick = args[++i];
                if (nick.Contains(" "))
                    nick = "\"" + nick + "\"";
            }
            if (args[i].ToLower() == "-h" && args.Length > i + 1)
            {
                host = args[++i];
            }
            if (args[i].ToLower() == "-p" && args.Length > i + 1)
            {
                port = args[++i];
            }
            if (args[i].ToLower() == "-w" && args.Length > i + 1)
            {
                password = args[++i];
                if (password.Contains(" "))
                    password = "\"" + password + "\"";
            }
            if (args[i].ToLower() == "-d" && args.Length > i + 1)
            {
                deck = args[++i];
                if (deck.Contains(" "))
                    deck = "\"" + deck + "\"";
            }
            if (args[i].ToLower() == "-r" && args.Length > i + 1)
            {
                replay = args[++i];
                if (replay.Contains(" "))
                    replay = "\"" + replay + "\"";
            }
            if (args[i].ToLower() == "-s" && args.Length > i + 1)
            {
                puzzle = args[++i];
                if (puzzle.Contains(" "))
                    puzzle = "\"" + puzzle + "\"";
            }
            if (args[i].ToLower() == "-j")
            {
                join = true;
                GameModeManager.SetDeckInUse(deck);
            }
        }
        string cmdFile = "commamd.shell";
        if (join)
        {
            File.WriteAllText(cmdFile, "online " + nick + " " + host + " " + port + " 0x233 " + password, Encoding.UTF8);
            Program.exitOnReturn = true;
        }
        else if (deck != null)
        {
            File.WriteAllText(cmdFile, "edit " + deck, Encoding.UTF8);
            Program.exitOnReturn = true;
        }
        else if (replay != null)
        {
            File.WriteAllText(cmdFile, "replay " + replay, Encoding.UTF8);
            Program.exitOnReturn = true;
        }
        else if (puzzle != null)
        {
            File.WriteAllText(cmdFile, "puzzle " + puzzle, Encoding.UTF8);
            Program.exitOnReturn = true;
        }
    }

    public GameObject mouseParticle;

    static int lastChargeTime = 0;
    public static void charge()
    {
        if (Program.TimePassed() - lastChargeTime > 5 * 60 * 1000)
        {
            lastChargeTime = Program.TimePassed();
            try
            {
                GameTextureManager.clearAll();
                Resources.UnloadUnusedAssets();
                GC.Collect();
            }
            catch (System.Exception e)
            {
                UnityEngine.Debug.Log(e);
            }
        }
    }

    #endregion

    #region Tools

    public static GameObject pointedGameObject = null;

    public static Collider pointedCollider = null;

    public static bool InputGetMouseButtonDown_0;

    public static bool InputGetMouseButton_0;

    public static bool InputGetMouseButtonUp_0;

    public static bool InputGetMouseButtonDown_1;

    public static bool InputGetMouseButtonUp_1;

    public static bool InputEnterDown = false;

    public static float wheelValue = 0;

    public class delayedTask
    {
        public int timeToBeDone;
        public Action act;
    }

    static List<delayedTask> delayedTasks = new List<delayedTask>();

    public static void go(int delay_, Action act_)
    {
        delayedTasks.Add(new delayedTask
        {
            act = act_,
            timeToBeDone = delay_ + Program.TimePassed(),
        });
    }

    public static void notGo(Action act_)
    {
        List<delayedTask> rem = new List<delayedTask>();
        for (int i = 0; i < delayedTasks.Count; i++)
        {
            if (delayedTasks[i].act == act_)
            {
                rem.Add(delayedTasks[i]);
            }
        }
        for (int i = 0; i < rem.Count; i++)
        {
            delayedTasks.Remove(rem[i]);
        }
        rem.Clear();
    }

    int rayFilter = 0;

    public void initializeALLcameras()
    {
        for (int i = 0; i < 32; i++)
        {
            if (i == 15)
            {
                continue;
            }
            rayFilter |= (int)Math.Pow(2, i);
        }

        if (camera_game_main == null)
        {
            camera_game_main = this.main_camera;
        }
        camera_game_main.transform.position = new Vector3(0, 23, -23);
        camera_game_main.transform.eulerAngles = new Vector3(60, 0, 0);
        camera_game_main.transform.localScale = new Vector3(1, 1, 1);
        camera_game_main.rect = new Rect(0, 0, 1, 1);
        camera_game_main.depth = 0;
        camera_game_main.gameObject.layer = 0;
        camera_game_main.clearFlags = CameraClearFlags.Depth;

        if (ui_back_ground_2d == null)
        {
            ui_back_ground_2d = create(mod_ui_2d);
            camera_back_ground_2d = ui_back_ground_2d.transform.Find("Camera").GetComponent<Camera>();
        }
        camera_back_ground_2d.depth = -2;
        ui_back_ground_2d.layer = 8;
        ui_back_ground_2d.transform.Find("Camera").gameObject.layer = 8;
        camera_back_ground_2d.cullingMask = (int)Mathf.Pow(2, 8);
        camera_back_ground_2d.clearFlags = CameraClearFlags.Depth;

        if (ui_container_3d == null)
        {
            ui_container_3d = create(mod_ui_3d);
            camera_container_3d = ui_container_3d.transform.Find("Camera").GetComponent<Camera>();
        }
        camera_container_3d.depth = -1;
        ui_container_3d.layer = 9;
        ui_container_3d.transform.Find("Camera").gameObject.layer = 9;
        camera_container_3d.cullingMask = (int)Mathf.Pow(2, 9);
        camera_container_3d.fieldOfView = 75;
        camera_container_3d.rect = camera_game_main.rect;
        camera_container_3d.transform.position = new Vector3(0, 23, -23);
        camera_container_3d.transform.eulerAngles = new Vector3(60, 0, 0);
        camera_container_3d.transform.localScale = new Vector3(1, 1, 1);
        camera_container_3d.rect = new Rect(0, 0, 1, 1);
        camera_container_3d.clearFlags = CameraClearFlags.Depth;



        if (ui_main_2d == null)
        {
            ui_main_2d = create(mod_ui_2d);
            camera_main_2d = ui_main_2d.transform.Find("Camera").GetComponent<Camera>();
        }
        camera_main_2d.depth = 3;
        ui_main_2d.layer = 11;
        ui_main_2d.transform.Find("Camera").gameObject.layer = 11;
        camera_main_2d.cullingMask = (int)Mathf.Pow(2, 11);
        camera_main_2d.clearFlags = CameraClearFlags.Depth;


        if (ui_windows_2d == null)
        {
            ui_windows_2d = create(mod_ui_2d);
            camera_windows_2d = ui_windows_2d.transform.Find("Camera").GetComponent<Camera>();
        }
        camera_windows_2d.depth = 2;
        ui_windows_2d.layer = 19;
        ui_windows_2d.transform.Find("Camera").gameObject.layer = 19;
        camera_windows_2d.cullingMask = (int)Mathf.Pow(2, 19);
        camera_windows_2d.clearFlags = CameraClearFlags.Depth;


        if (ui_main_3d == null)
        {
            ui_main_3d = create(mod_ui_3d);
            camera_main_3d = ui_main_3d.transform.Find("Camera").GetComponent<Camera>();
        }
        camera_main_3d.depth = 1;
        ui_main_3d.layer = 10;
        ui_main_3d.transform.Find("Camera").gameObject.layer = 10;
        camera_main_3d.cullingMask = (int)Mathf.Pow(2, 10);
        camera_main_3d.fieldOfView = 75;
        camera_main_3d.rect = new Rect(0, 0, 1, 1);
        camera_main_3d.transform.position = new Vector3(0, 23, -23);
        camera_main_3d.transform.eulerAngles = new Vector3(60, 0, 0);
        camera_main_3d.transform.localScale = new Vector3(1, 1, 1);
        camera_main_3d.clearFlags = CameraClearFlags.Depth;




        camera_main_3d.transform.localPosition = camera_game_main.transform.position;
        camera_container_3d.transform.localPosition = camera_game_main.transform.position;

        camera_main_3d.transform.localEulerAngles = camera_game_main.transform.localEulerAngles;
        camera_container_3d.transform.localEulerAngles = camera_game_main.transform.localEulerAngles;

        camera_main_3d.fieldOfView = camera_game_main.fieldOfView;
        camera_container_3d.fieldOfView = camera_game_main.fieldOfView;

        camera_main_3d.rect = camera_game_main.rect;
        camera_container_3d.rect = camera_game_main.rect;
    }

    public static float deltaTime = 1f / 120f;

    /// <summary>探针用：上一次落盘的镜头姿态（镜头只在变化时打 <c>[view]</c> 行）。</summary>
    private static Vector3 lastViewPos = new Vector3(-9999f, -9999f, -9999f);
    private static Vector3 lastViewRot = new Vector3(-9999f, -9999f, -9999f);
    private static bool lastViewDuel = false;

    /// <summary>探针用：上一次落盘的「地面 z → 屏幕 y」投影标尺（同一姿态只打一次）。</summary>
    private static string lastProjKey = "";

    public void fixALLcamerasPreFrame()
    {
        deltaTime = Time.deltaTime;
        if (deltaTime > 1f / 40f)
        {
            deltaTime = 1f / 40f;
        }
        if (camera_game_main != null)
        {
            // 「俯视角」**唯一的换算点**（见 tableauCameraPosition 的注释）。
            // 闸门 = 对局视角正在显示：卡组编辑界面也写同一个 cameraPosition 驱动相机，
            // 而那台相机本来就是 90° 俯视，再换算一次就跑到别的点上去了。
            // ⛔ cameraPosition / cameraRotation 两个静态量始终保持「60° 语义」，
            //    所以 Ocgcore.show / toNearest / 初始化那几处写入点一行都不用动。
            bool duelView = Program.I() != null && Program.I().ocgcore != null
                && Program.I().ocgcore.isShowed;
            Vector3 wantPos = duelView ? tableauCameraPosition(cameraPosition) : cameraPosition;
            Vector3 wantRot = duelView ? tableauCameraRotation(cameraRotation) : cameraRotation;

            if (QuickTestTrace.Enabled
                && (wantPos != lastViewPos || wantRot != lastViewRot || lastViewDuel != duelView))
            {
                lastViewPos = wantPos;
                lastViewRot = wantRot;
                lastViewDuel = duelView;
                QuickTestTrace.Log("view", "topDown=" + (topDown ? 1 : 0)
                    + " tilt=" + tableauTilt.ToString("F0")
                    + " cover=" + topDownCoverZ.ToString("F1")
                    + " handScale=" + tableauHandScale(1).ToString("F3")
                    + " duel=" + (duelView ? 1 : 0)
                    + " facing=" + (cameraFacing ? 1 : 0)
                    + " want=(" + wantPos.x.ToString("F1") + "," + wantPos.y.ToString("F1") + "," + wantPos.z.ToString("F1") + ")"
                    + " rot=(" + wantRot.x.ToString("F0") + "," + wantRot.y.ToString("F0") + "," + wantRot.z.ToString("F0") + ")"
                    // span = 正俯视时**盘面所在平面上**可见的 z 半跨度（世界单位）。
                    // 它就是「俯视角看得多大」：正俯视下 = 机高 × tan(fov/2)，且等于 topDownCoverZ。
                    // 「场地与两手牌都在不在屏内」不咬这个数（它是**声明值**，咬它等于咬自己），
                    // 咬的是下面那条 `[view] proj` 实测投影标尺 —— 见 _probe_topdown.py 抬头。
                    + " fov=" + camera_game_main.fieldOfView.ToString("F0")
                    + " span=" + (wantPos.y * Mathf.Tan(camera_game_main.fieldOfView * 0.5f * Mathf.Deg2Rad)).ToString("F1"));
                if (topDown)
                {
                    // 手牌/展示行的**行带拟合**：n=1..3 行各自的放大倍数 s 与每行 |z| 一次打全。
                    // 为什么值得落一条日志：套数不能只验 n=1（探针那局恰好 5 张手牌）。
                    // 「手牌 ≥7 张 / 手牌+被公开的卡」会出现 n≥2，而 n≥2 正是旧口径下
                    // **整行出屏**的那条路径 ⇒ 让探针逐行核「每行的内外沿都落在这条带子里」，
                    // 不咬具体数值（换个 cover 也成立）。
                    System.Text.StringBuilder hf = new System.Text.StringBuilder();
                    for (int n = 1; n <= 3; n++)
                    {
                        float s = tableauHandScale(n);
                        hf.Append(" n").Append(n).Append("=s").Append(s.ToString("F3")).Append("/z");
                        for (int k = 0; k < n; k++)
                        {
                            hf.Append(k == 0 ? "" : "+").Append(tableauHandRowAbs(0f, n, k, s).ToString("F2"));
                        }
                    }
                    QuickTestTrace.Log("view", "handfit band=" + topDownHandBand().ToString("F2")
                        + " inner=" + (14.6f + 0.4f).ToString("F1")
                        + " outer=" + (topDownCoverZ - 0.5f).ToString("F1")
                        + hf);
                }
            }

            camera_game_main.transform.position += (wantPos - camera_game_main.transform.position) * deltaTime * 3.5f;
            camera_container_3d.transform.localPosition = camera_game_main.transform.position;

            // 探针：镜头**落定之后**打一条「地面 z → 屏幕 y」的标尺。
            // 「整张牌桌在不在屏内、盘面在不在屏幕中间」这两件事不能靠看截图估 ——
            // 直接量：盘面 z 跨 ±13.67（两端牌堆，见 `[field] piles`）、两手牌更外，
            // 换算是 `y = Screen.height - WorldToScreenPoint(0,0,z).y`（与全场探针同口径）。
            if (QuickTestTrace.Enabled && duelView
                && Vector3.Distance(camera_game_main.transform.position, wantPos) < 0.05f)
            {
                string projKey = wantPos.ToString("F2") + "|" + wantRot.ToString("F2");
                if (projKey != lastProjKey)
                {
                    lastProjKey = projKey;
                    System.Text.StringBuilder sb = new System.Text.StringBuilder();
                    for (int z = -28; z <= 28; z += 4)
                    {
                        Vector3 sp = camera_game_main.WorldToScreenPoint(new Vector3(0f, 0f, z));
                        sb.Append(' ').Append(z).Append('=').Append(Mathf.RoundToInt(Screen.height - sp.y));
                    }
                    QuickTestTrace.Log("view", "proj h=" + Screen.height + " z->screenY:" + sb);
                    // 卡上浮标（**超量素材光点**）相对卡面的**屏幕位移**：`off` 就是 `gameCard` 真正
                    // 用的那个世界偏移，投影到屏幕求差分 ⇒ 直接量出「光点会不会浮在卡面之上」。
                    // 60° 下 `(0,1.8,0)` 在屏幕上是向上的；**正俯视下世界 +y 的屏幕位移恰好为 0**
                    // （光点糊在卡面正中 —— 用户 2026-09-23 报的「变成在卡牌中间」），
                    // 所以俯视角改用世界 +z（= 屏幕上方，见上面那条 proj 标尺）。
                    // 判据咬这条：`dy_px` 必须显著为负（屏幕上方），不许 ≈ 0。
                    {
                        Vector3 o0 = camera_game_main.WorldToScreenPoint(Vector3.zero);
                        Vector3 olOff = gameCard.overlayLightOffset;
                        Vector3 o1 = camera_game_main.WorldToScreenPoint(olOff);
                        QuickTestTrace.Log("view", "oloff topDown=" + (topDown ? 1 : 0)
                            + " off=(" + olOff.x.ToString("F1") + "," + olOff.y.ToString("F1")
                            + "," + olOff.z.ToString("F1") + ")"
                            + " dx_px=" + Mathf.RoundToInt(o1.x - o0.x)
                            + " dy_px=" + Mathf.RoundToInt((Screen.height - o1.y) - (Screen.height - o0.y)));
                    }
                    // 卡上偏移（**可发动闪光 / 卡上数字**那一族）的观感自证：
                    // 比值 = 偏移的屏上长度 ÷ 卡片沿自己上方向的半高屏上长度 —— 与视角无关
                    // （见 `Program.cardUpOffset` 注释推导），所以「两视角比值一致」就是
                    // 「闪光落在卡面的同一位置」的等价说法。咬一张**怪兽区的卡**（两视角同一张）。
                    {
                        gameCard pc = null;
                        int bestRank = 99;
                        System.Collections.Generic.List<gameCard> cs =
                            (Program.I() != null && Program.I().ocgcore != null) ? Program.I().ocgcore.cards : null;
                        if (cs != null)
                        {
                            for (int i = 0; i < cs.Count; i++)
                            {
                                gameCard c = cs[i];
                                if (c == null || c.gameObject == null || !c.gameObject.activeInHierarchy)
                                {
                                    continue;
                                }
                                // 取样顺序：我方怪兽区的卡（闪光真正出现的地方）→ 怪兽区 → 我方任意卡。
                                // ⛔ 必须留退路：**开局场上还没有怪兽**，只认怪兽区的话这条探针根本不出现，
                                //    而「关 / 开」两遍都是开局快照 ⇒ 没有样本可对咬。
                                //    退到手牌样本照样成立：比值 = 偏移屏长 ÷ **卡片自己的**半高屏长，
                                //    卡被放大（手牌 s=1.5）时分子分母同比放大，比值不变。
                                bool mon = (c.p.location & (UInt32)YGOSharp.OCGWrapper.Enums.CardLocation.MonsterZone) != 0;
                                bool ours = c.p.controller == 0;
                                int rank = (mon ? 0 : 2) + (ours ? 0 : 1);
                                if (rank < bestRank)
                                {
                                    bestRank = rank;
                                    pc = c;
                                }
                            }
                        }
                        if (pc != null)
                        {
                            float r12, r24;
                            string d12, d24;
                            if (pc.upOffsetPixelRatio(1.2f, out r12, out d12)
                                && pc.upOffsetPixelRatio(2.4f, out r24, out d24))
                            {
                                bool monNow = (pc.p.location
                                    & (UInt32)YGOSharp.OCGWrapper.Enums.CardLocation.MonsterZone) != 0;
                                QuickTestTrace.Log("cardup", "topDown=" + (topDown ? 1 : 0)
                                    + " zone=" + (monNow ? "mon" : "hand")
                                    + " pos=" + pc.p.location + " ctrl=" + pc.p.controller
                                    + " seq=" + pc.p.sequence
                                    + " h1.2 ratio=" + r12.ToString("F3") + d12
                                    + " | h2.4 ratio=" + r24.ToString("F3") + d24);
                            }
                        }
                    }
                    // 【fieldui】盘面 UI 的**俯视角适配**探针（用户 2026-09-23 第 6 轮）：
                    // ・牌堆/墓地/除外计数文字（gameField.LOCATION_*，每帧跟 tableauFrontX）
                    // ・选卡三角标志（card_selecting 装饰物，俯视角根 30° + 子 Quad 烘焙 60° = 平铺）
                    // ・第 8 轮补：连锁选择标志（chainEuler，根 30° 才平铺）与
                    //   已选蓝光（selectedGap，与卡面中心的屏幕 px 距离，收近后应 ≤ 25）
                    {
                        string tri = "none";
                        string chain = "none";
                        string selGap = "none";
                        System.Collections.Generic.List<gameCard> cs2 =
                            (Program.I() != null && Program.I().ocgcore != null) ? Program.I().ocgcore.cards : null;
                        if (cs2 != null)
                        {
                            for (int i = 0; i < cs2.Count; i++)
                            {
                                if (cs2[i] == null) continue;
                                string t = cs2[i].probe_selecting_mark_euler();
                                if (t != "none")
                                {
                                    tri = t;
                                }
                                string ch = cs2[i].probe_chain_mark_euler();
                                if (ch != "none")
                                {
                                    chain = ch;
                                }
                                string sg = cs2[i].probe_selected_mark_gap();
                                if (sg != "none")
                                {
                                    selGap = sg;
                                }
                            }
                        }
                        TMPro.TextMeshPro deckTxt = (Program.I() != null && Program.I().ocgcore != null
                            && Program.I().ocgcore.gameField != null)
                            ? Program.I().ocgcore.gameField.LOCATION_DECK_0 : null;
                        string deckE = deckTxt != null
                            ? ((int)System.Math.Round(deckTxt.transform.eulerAngles.x)) + ","
                              + ((int)System.Math.Round(deckTxt.transform.eulerAngles.y)) + ","
                              + ((int)System.Math.Round(deckTxt.transform.eulerAngles.z))
                            : "none";
                        QuickTestTrace.Log("fieldui", "topDown=" + (topDown ? 1 : 0)
                            + " deckEuler=" + deckE
                            + " selectingEuler=" + tri
                            + " chainEuler=" + chain
                            + " selectedGap=" + selGap);
                    }
                }
            }
            if (cameraFacing == false)
            {
                camera_game_main.transform.localEulerAngles += (wantRot - camera_game_main.transform.localEulerAngles) * deltaTime * 3.5f;
            }
            else
            {
                camera_game_main.transform.LookAt(Vector3.zero);
            }
            camera_container_3d.transform.localEulerAngles = camera_game_main.transform.localEulerAngles;
            // camera_main_3d（layer 10）原先只在初始化时同步过一次，这里一并跟上，
            // 免得开了俯视角之后它自己还停在 60° 上。
            if (camera_main_3d != null)
            {
                camera_main_3d.transform.localPosition = camera_game_main.transform.position;
                camera_main_3d.transform.localEulerAngles = camera_game_main.transform.localEulerAngles;
            }
            camera_container_3d.fieldOfView = camera_game_main.fieldOfView;
            camera_container_3d.rect = camera_game_main.rect;
        }
    }

    public void fixScreenProblems()
    {
        for (int i = 0; i < servants.Count; i++)
        {
            servants[i].fixScreenProblem();
        }
    }

    public GameObject create(
        GameObject mod,
        Vector3 position = default(Vector3),
        Vector3 rotation = default(Vector3),
        bool fade = false,
        GameObject father = null,
        bool allParamsInWorld = true,
        Vector3 wantScale = default(Vector3)
        )
    {
        Vector3 scale = mod.transform.localScale;
        if (wantScale != default(Vector3))
        {
            scale = wantScale;
        }
        GameObject return_value = (GameObject)MonoBehaviour.Instantiate(mod);
        if (position != default(Vector3))
        {
            return_value.transform.position = position;
        }
        else
        {
            return_value.transform.position = Vector3.zero;
        }
        if (rotation != default(Vector3))
        {
            return_value.transform.eulerAngles = rotation;
        }
        else
        {
            return_value.transform.eulerAngles = Vector3.zero;
        }
        if (father != null)
        {
            return_value.transform.SetParent(father.transform, false);
            return_value.layer = father.layer;
            if (allParamsInWorld == true)
            {
                return_value.transform.position = position;
                return_value.transform.localScale = scale;
                return_value.transform.eulerAngles = rotation;
            }
            else
            {
                return_value.transform.localPosition = position;
                return_value.transform.localScale = scale;
                return_value.transform.localEulerAngles = rotation;
            }
        }
        else
        {
            return_value.layer = 0;
        }
        Transform[] Transforms = return_value.GetComponentsInChildren<Transform>();
        foreach (Transform child in Transforms)
        {
            child.gameObject.layer = return_value.layer;
        }
        if (fade == true)
        {
            return_value.transform.localScale = Vector3.zero;
            iTween.ScaleToE(return_value, scale, 0.3f);
        }
        return return_value;
    }

    public void destroy(GameObject obj, float time = 0, bool fade = false, bool instantNull = false)
    {
        try
        {
            if (obj != null)
            {
                if (fade)
                {
                    iTween.ScaleTo(obj, Vector3.zero, 0.4f);
                    MonoBehaviour.Destroy(obj, 0.6f);
                }
                else
                {
                    if (time != 0) MonoBehaviour.Destroy(obj, time);
                    else MonoBehaviour.Destroy(obj);
                }
                if (instantNull)
                {
                    obj = null;
                }
            }
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.Log(e);
        }
    }

    //public static void shiftCameraPan(Camera camera, bool enabled)
    //{
    //    cameraPaning = enabled;
    //    PanWithMouse panWithMouse = camera.gameObject.GetComponent<PanWithMouse>();
    //    if (panWithMouse == null)
    //    {
    //        panWithMouse = camera.gameObject.AddComponent<PanWithMouse>();
    //    }
    //    panWithMouse.enabled = enabled;
    //    if (enabled == false)
    //    {
    //        iTween.RotateTo(camera.gameObject, new Vector3(60, 0, 0), 0.6f);
    //    }
    //}

    public static void reMoveCam(float xINscreen)
    {
        float all = (float)Screen.width / 2f;
        float it = xINscreen - (float)Screen.width / 2f;
        float val = it / all;
        camera_game_main.rect = new Rect(val, 0, 1, 1);
        camera_container_3d.rect = camera_game_main.rect;
        camera_main_3d.rect = camera_game_main.rect;
    }

    public static void ShiftUIenabled(GameObject ui, bool enabled)
    {
        var all = ui.GetComponentsInChildren<BoxCollider>();
        for (int i = 0; i < all.Length; i++)
        {
            all[i].enabled = enabled;
        }
    }

    public static Texture2D GetTextureViaPath(string path)
    {
        FileStream file = new FileStream(path, FileMode.Open, FileAccess.Read);
        file.Seek(0, SeekOrigin.Begin);
        byte[] data = new byte[file.Length];
        file.Read(data, 0, (int)file.Length);
        file.Close();
        file.Dispose();
        file = null;
        Texture2D pic = new Texture2D(1024, 600);
        pic.LoadImage(data);
        return pic;
    }

    /// <summary>
    /// 用 SharpZipLib 把内存中的 zip/ypk 数据解压到指定目录（超先行卡包安装用）。
    /// 这里一律使用完全限定名，避免与工程既有的 Ionic.Zip.ZipFile 产生类型歧义。
    /// </summary>
    public void ExtractZipFile(byte[] data, string outFolder)
    {
        ICSharpCode.SharpZipLib.Zip.ZipFile zf = null;
        try
        {
            //use MemoryStream!!!!
            using (MemoryStream mstrm = new MemoryStream(data))
            {
                zf = new ICSharpCode.SharpZipLib.Zip.ZipFile(mstrm);

                foreach (ICSharpCode.SharpZipLib.Zip.ZipEntry zipEntry in zf)
                {
                    if (!zipEntry.IsFile)
                    {
                        continue;
                    }

                    string entryFileName = zipEntry.Name;
                    byte[] buffer = new byte[4096]; // 4K is optimum
                    Stream zipStream = zf.GetInputStream(zipEntry);

                    string fullZipToPath = Path.Combine(outFolder, entryFileName);
                    string directoryName = Path.GetDirectoryName(fullZipToPath);
                    if (directoryName.Length > 0)
                    {
                        Directory.CreateDirectory(directoryName);
                    }
                    using (FileStream streamWriter = File.Create(fullZipToPath))
                    {
                        ICSharpCode.SharpZipLib.Core.StreamUtils.Copy(zipStream, streamWriter, buffer);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Debug.Log(ex);
        }
        finally
        {
            if (zf != null)
            {
                zf.IsStreamOwner = true;
                zf.Close();
            }
        }
    }

    #endregion

    #region Servants

    List<Servant> servants = new List<Servant>();

    public Servant backGroundPic;
    public Menu menu;
    public SuperPreList superPreList;
    public Setting setting;
    public selectDeck selectDeck;
    public selectReplay selectReplay;
    public Room room;
    public CardDescription cardDescription;
    public DeckManager deckManager;
    public Ocgcore ocgcore;
    public SelectServer selectServer;
    public Book book;
    public puzzleMode puzzleMode;
    public AIRoom aiRoom;

    void initializeALLservants()
    {
        menu = new Menu();
        servants.Add(menu);
        superPreList = new SuperPreList();
        servants.Add(superPreList);
        setting = new Setting();
        servants.Add(setting);
        selectDeck = new selectDeck();
        servants.Add(selectDeck);
        room = new Room();
        servants.Add(room);
        cardDescription = new CardDescription();
        deckManager = new DeckManager();
        servants.Add(deckManager);
        ocgcore = new Ocgcore();
        servants.Add(ocgcore);
        selectServer = new SelectServer();
        servants.Add(selectServer);

        book = new Book();
        servants.Add(book);
        selectReplay = new selectReplay();
        servants.Add(selectReplay);
        puzzleMode = new puzzleMode();
        servants.Add(puzzleMode);
        aiRoom = new AIRoom();
        servants.Add(aiRoom);
    }

    public void shiftToServant(Servant to)
    {
        if (to != backGroundPic && backGroundPic.isShowed)
        {
            backGroundPic.hide();
        }
        if (to != menu && menu.isShowed)
        {
            menu.hide();
        }
        if (to != superPreList && superPreList.isShowed)
        {
            superPreList.hide();
        }
        if (to != setting && setting.isShowed)
        {
            setting.hide();
        }
        if (to != selectDeck && selectDeck.isShowed)
        {
            selectDeck.hide();
        }
        if (to != room && room.isShowed)
        {
            room.hide();
        }
        if (to != deckManager && deckManager.isShowed)
        {
            deckManager.hide();
        }
        if (to != ocgcore && ocgcore.isShowed)
        {
            ocgcore.hide();
        }
        if (to != selectServer && selectServer.isShowed)
        {
            selectServer.hide();
        }

        if (to != selectReplay && selectReplay.isShowed)
        {
            selectReplay.hide();
        }
        if (to != puzzleMode && puzzleMode.isShowed)
        {
            puzzleMode.hide();
        }
        if (to != aiRoom && aiRoom.isShowed)
        {
            aiRoom.hide();
        }

        if (to == backGroundPic && backGroundPic.isShowed == false) backGroundPic.show();
        if (to == menu && menu.isShowed == false) menu.show();
        if (to == superPreList && superPreList.isShowed == false) superPreList.show();
        if (to == setting && setting.isShowed == false) setting.show();
        if (to == selectDeck && selectDeck.isShowed == false) selectDeck.show();
        if (to == room && room.isShowed == false) room.show();
        if (to == deckManager && deckManager.isShowed == false) deckManager.show();
        if (to == ocgcore && ocgcore.isShowed == false) ocgcore.show();
        if (to == selectServer && selectServer.isShowed == false) selectServer.show();
        if (to == selectReplay && selectReplay.isShowed == false) selectReplay.show();
        if (to == puzzleMode && puzzleMode.isShowed == false) puzzleMode.show();
        if (to == aiRoom && aiRoom.isShowed == false) aiRoom.show();

    }

    #endregion

    #region MonoBehaviors

    void Start()
    {
        if (Screen.width < 100 || Screen.height < 100)
        {
            Screen.SetResolution(1300, 700, false);
        }
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = 144;
        mouseParticle = Instantiate(new_mouse);
        instance = this;
        initialize();
        go(500, () => { gameStart(); });
    }

    int preWid = 0;

    int preheight = 0;

    public static float _padScroll = 0;

    void OnGUI()
    {
        if (Event.current.type == EventType.ScrollWheel)
            _padScroll = -Event.current.delta.y / 100;
        else
            _padScroll = 0;
    }

    // ── 帧循环心跳（仅排查用）────────────────────────────────────────────────
    // 「对局明明开始了、界面上却什么都不发生」时，第一件必须分清的事是：帧循环还活着
    // （只是服务器/机器人没消息），还是主线程卡在某一行。收包是主线程在 Update 末尾
    // （TcpHelper.preFrameFunction）驱动的，所以主线程一卡，日志会连着 [stoc] 一起断掉，
    // 从日志上看不出是「没人说话」还是「听不见了」。这里每约 2 秒写一行，并带上阶段名
    // （走到哪个 servant），一跑就能把卡点框出来。只在 log/qt_debug.on 存在时有输出。
    private static float hbLast = -1000f;
    private static long hbCalls = 0;

    public static void Heartbeat(string stage)
    {
        hbCalls++;
        if (!QuickTestTrace.Enabled)
        {
            return;
        }
        float now = Time.realtimeSinceStartup;
        if (now - hbLast < 2f)
        {
            return;
        }
        hbLast = now;
        QuickTestTrace.Log("hb", stage
            + " calls=" + hbCalls
            + " rt=" + now.ToString("F1")
            + " time=" + Time.time.ToString("F1")
            + " ts=" + Time.timeScale.ToString("F2")
            + " frame=" + Time.frameCount
            + " focus=" + (Application.isFocused ? 1 : 0)
            + " scr=" + Screen.width + "x" + Screen.height);
    }

    /// <summary>
    /// 排查用（log/qt_debug.on）：把「光标在哪、NGUI 在那里命中了谁」落盘。
    ///
    /// 为什么需要它：验收脚本是用真光标（SetCursorPos + mouse_event）点按钮的，
    /// 一旦点不中，从服务端看就是「什么都没发生」，分不清是
    /// ①坐标算错、②被别的控件挡住、还是 ③按钮自己没注册回调。
    /// 这里复用的正是游戏每帧自己做悬停检测的那次 UICamera.Raycast 结果，
    /// 所以脚本把光标挪过去之后，轨迹里就能直接看到 Unity 认定的命中对象。
    /// 只在光标动了或按下左键时写，避免刷屏。
    /// </summary>
    static Vector3 probeMouseLast = new Vector3(-9999f, -9999f, 0f);
    static int probeMouseMs = -1;

    static void ProbeMouse(GameObject hoverobject)
    {
        if (!QuickTestTrace.Enabled)
        {
            return;
        }
        Vector3 m = Input.mousePosition;
        bool down = Input.GetMouseButtonDown(0);
        bool moved = Mathf.Abs(m.x - probeMouseLast.x) > 1f
            || Mathf.Abs(m.y - probeMouseLast.y) > 1f;
        if (!moved && !down)
        {
            return;
        }
        int now = TimePassed();
        if (!down && now - probeMouseMs < 60)
        {
            return;
        }
        probeMouseMs = now;
        probeMouseLast = m;
        QuickTestTrace.Log("mouse",
            "pos=(" + Mathf.RoundToInt(m.x) + "," + Mathf.RoundToInt(m.y) + ")"
            + " winPos=(" + Mathf.RoundToInt(m.x) + ","
            + Mathf.RoundToInt(Screen.height - m.y) + ")"
            + " down=" + (down ? 1 : 0)
            + " hover=" + ProbePathOf(hoverobject)
            + " scr=" + Screen.width + "x" + Screen.height);
    }

    /// <summary>把命中对象写成「父/子」路径，一眼看出是工具条上哪个按钮。</summary>
    static string ProbePathOf(GameObject go)
    {
        if (go == null)
        {
            return "none";
        }
        string p = go.name;
        Transform t = go.transform.parent;
        for (int i = 0; i < 3 && t != null; i++)
        {
            p = t.name + "/" + p;
            t = t.parent;
        }
        return p;
    }

    void Update()
    {
        Heartbeat("update-begin");
        // 排查用（log/qt_uidump.on）：把此刻屏幕上所有 UILabel 的文案倒进轨迹。
        // 挂在**这里**而不是 servant 的 preFrameFunction：那个钩子被
        // ServantWithCardDescription 这类不调 base 的 override 挡掉（卡组编辑器/对局整块漏导），
        // 而且够不着「高级搜索」那种独立根节点的窗口 —— 详见 QuickTestTrace.UiDumpAll 的头注。
        // 开关不在时零开销（内部带节流的 SwitchOn，2.5 次/秒上限）。
        QuickTestTrace.UiDumpAll();
        // 数据更新完成后的内存重载 + 事务提交。菜单是否可见由 ClientDataUpdater 自己判断
        // （不在主菜单就一直挂着），这里每帧兜底问一次 —— 不依赖菜单自身的生命周期。
        if (ClientDataUpdater.ReloadPending)
        {
            ClientDataUpdater.ApplyPendingReload();
        }
        // 自动化验收（log/qt_debug.on）时不许因为失焦就停循环。
        // Unity 播放器默认失焦即暂停：Time.time 不走、入站消息也不再处理，脚本又没法保证
        // 一直占着前台（SetForegroundWindow 会被系统拒绝），验收就会莫名其妙卡在半路
        // ——表现为「对局明明开着却什么也不发生」。只在这个调试开关存在时改，正常玩不受影响。
        // ⚠ 这一行在**每帧**跑，第二个操作数必须是带节流的 `SwitchOn`（内部「它不在」方向
        //   缓存 500ms）而不是裸 `File.Exists`：实测后者 17us/次，60fps 下就是单核 0.1% ——
        //   而正常玩这个文件永远不存在，白烧。见 QuickTestTrace.SwitchPollMs。
        if (!Application.runInBackground
            && QuickTestTrace.SwitchOn(QuickTestTrace.MasterSwitch))
        {
            Application.runInBackground = true;
            QuickTestTrace.Log("app", "qt_debug.on -> Application.runInBackground=true（失焦也继续跑）");
        }

        if (preWid != Screen.width || preheight != Screen.height)
        {
            Resources.UnloadUnusedAssets();
            onRESIZED();
        }
        fixALLcamerasPreFrame();
        wheelValue = UICamera.GetAxis("Mouse ScrollWheel") * 50;
        pointedGameObject = null;
        pointedCollider = null;
        Ray line = Camera.main.ScreenPointToRay(Input.mousePosition);
        RaycastHit hit;
        if (Physics.Raycast(line, out hit, (float)1000, rayFilter))
        {
            pointedGameObject = hit.collider.gameObject;
            pointedCollider = hit.collider;
        }
        GameObject hoverobject = UICamera.Raycast(Input.mousePosition) ? UICamera.lastHit.collider.gameObject : null;
        if (hoverobject != null)
        {
            if (hoverobject.layer == 11 || pointedGameObject == null)
            {
                pointedGameObject = hoverobject;
                pointedCollider = UICamera.lastHit.collider;
            }
        }
        ProbeMouse(hoverobject);
        InputGetMouseButtonDown_0 = Input.GetMouseButtonDown(0);
        InputGetMouseButtonUp_0 = Input.GetMouseButtonUp(0);
        InputGetMouseButtonDown_1 = Input.GetMouseButtonDown(1);
        InputGetMouseButtonUp_1 = Input.GetMouseButtonUp(1);
        InputEnterDown = Input.GetKeyDown(KeyCode.Return);
        InputGetMouseButton_0 = Input.GetMouseButton(0);
        for (int i = 0; i < servants.Count; i++)
        {
            Heartbeat("servant#" + i + " " + (servants[i] == null
                ? "null" : servants[i].GetType().Name) + "（进入）");
            servants[i].Update();
        }
        TcpHelper.preFrameFunction();
        // AI 对局的看门狗：子进程崩了 / 长时间没包时把玩家从卡死的对局里放出来。
        if (aiRoom != null)
        {
            aiRoom.WatchdogTick();
        }
        Heartbeat("update-end");
        delayedTask remove = null;
        while (true)
        {
            remove = null;
            for (int i = 0; i < delayedTasks.Count; i++)
            {
                if (Program.TimePassed() > delayedTasks[i].timeToBeDone)
                {
                    remove = delayedTasks[i];
                    try
                    {
                        remove.act();
                    }
                    catch (System.Exception e)
                    {
                        UnityEngine.Debug.Log(e);
                    }
                    break;
                }
            }
            if (remove != null)
            {
                delayedTasks.Remove(remove);
            }
            else
            {
                break;
            }
        }

    }

    private void onRESIZED()
    {
        preWid = Screen.width;
        preheight = Screen.height;
        //if (setting != null)
        //    setting.setScreenSizeValue();
        Program.notGo(fixScreenProblems);
        Program.go(500, fixScreenProblems);
    }

    public static void DEBUGLOG(object o)
    {
#if UNITY_EDITOR
        Debug.Log(o);
#endif
    }

    public static void PrintToChat(object o)
    {
        try
        {
            instance.cardDescription.mLog(o.ToString());
        }
        catch
        {
            DEBUGLOG(o);
        }
    }

    void gameStart()
    {
        if (UIHelper.shouldMaximize())
        {
            UIHelper.MaximizeWindow();
        }
        backGroundPic.show();
        shiftToServant(menu);
        // 启动落定后把每个 servant 的可见性结算一次（仅 log/qt_debug.on）。
        //
        // 排查用：窗口类 servant 的 initialize() 末尾都要自己 SetActiveFalse()，
        // 谁漏了就会一直亮在客户区中央（「开游戏先弹一下某个界面」多半是这个）。
        // 这种「创建出来就没藏过」不会有状态翻转，光看显示/隐藏轨迹是看不出来的，
        // 必须在启动后结算一次稳态，才能一眼看出谁亮着。
        Program.go(3000, DumpServantVisibility);
    }

    private void DumpServantVisibility()
    {
        if (!QuickTestTrace.Enabled)
        {
            return;
        }
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        for (int i = 0; i < servants.Count; i++)
        {
            Servant s = servants[i];
            if (s == null)
            {
                continue;
            }
            sb.Append(s.GetType().Name)
              .Append("=")
              .Append(s.gameObject != null && s.gameObject.activeSelf ? "亮" : "藏")
              .Append(" ");
        }
        QuickTestTrace.Log("vis", "steady " + sb.ToString());
    }

    public static bool Running = true;

    public static bool MonsterCloud = false;
    public static float fieldSize = 1;
    public static bool longField = false;

    public static bool noAccess = false;

    public static bool exitOnReturn = false;

    void OnApplicationQuit()
    {
        TcpHelper.SaveRecord();
        cardDescription.save();
        setting.saveWhenQuit();
        for (int i = 0; i < servants.Count; i++)
        {
            servants[i].OnQuit();
        }
        Running = false;
        try
        {
            TcpHelper.tcpClient.Close();
        }
        catch (System.Exception e)
        {
            //adeUnityEngine.Debug.Log(e);
        }
        Menu.deleteShell();
        foreach (ZipFile zip in GameZipManager.Zips)
        {
            zip.Dispose();
        }
        aiRoom.killServerProcess();
    }

    public void quit()
    {
        OnApplicationQuit();
    }

    #endregion

    public static void gugugu()
    {
        PrintToChat(InterString.Get("非常抱歉，因为技术原因，此功能暂时无法使用。请关注官方网站获取更多消息。"));
    }
}
