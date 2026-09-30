using System;
using System.IO;
using UnityEngine;

/// <summary>
/// 战斗界面右侧的「俯视角（平面图）↔ 正常视角」切换钮（设计稿方案 C，56×56 方钮）。
///
/// ── 落位（为什么不跟按钮列走）────────────────────────────────────────────
/// 右侧按钮列面板是**随按键数往下长**的：<c>gameInfo.Update</c> 里
/// <c>height = 132 + 50*rank</c>，撤回组满档时底边 y=926，而右下功能图标行顶边 938 ——
/// 中间只剩 12px。所以这颗钮既不能进按钮列、也不能放列下方。
/// 它落在右上信息条（底 y=205）与复选区面板（顶 y=348）之间那条 298×132 的空档里，
/// **右缘钉在按钮列面板的右缘**（1920×986 下 = 1905px），中心比面板原点高 80px。
///
/// ── 坐标是算出来的，不是写死的 ──────────────────────────────────────────
/// 右缘 = <c>instance_btnPan</c> 自身变换的 <c>TransformPoint(width/2, 0, 0)</c>
/// （width 读的是 NGUI 控件自己的宽度 ⇒ prefab 改了宽度它自动跟）。
/// 再用 UI 相机 WorldToScreenPoint → ScreenToWorldPoint 换算到自建面板的局部单位。
/// ⇒ 换分辨率、换 cardDescription 宽度（<c>ksb</c> 0.73~1.2）都对得上。
///
/// ── 三条必须守住的红线 ─────────────────────────────────────────────────
/// ① **不新增任何实例字段**。本工程发布 DLL 的字段布局必须与已烘焙资源逐字段对齐，
///    所以全部状态走 <c>static</c>（同 <c>Program.viewStamp</c> 的做法），
///    且不往 <c>gameInfo</c> / <c>Setting</c> 等既有类上加字段。
/// ② **自建 UIPanel 而不是挂到 <c>instance_btnPan</c> 上**。NGUI 一个 panel 一份材质，
///    往已有 panel 里塞不同贴图的控件有排不出来的风险；<c>gameInfo.ensureGrayTip</c>
///    与 <c>DuelUndo</c> 的遮罩都是自建 panel（深度取全场最大 + 余量），照那套走。
/// ③ **运行时 new 出来的 UITexture 必须**给自建偶数尺寸白纹理 + 显式 <c>path=""</c>
///    + 抄现成 shader，否则不渲染（<c>UITexture.OnStart</c> 会用 <c>mOutPath</c>(null≠"")
///    把贴图冲成 null；纯色块也绝不能用 <c>Texture2D.whiteTexture</c>）。见 <see cref="BoxTex"/>。
///
/// ── 状态与设置页同源 ───────────────────────────────────────────────────
/// 真源是 <see cref="Program.view"/>（<c>TableauView.Tilt60 / TopDown</c>），
/// 持久化键 <c>topDown_</c> 与「系统设置 → 俯视角（平面图）」那个开关**共用** ⇒ 两边天然同状态。
/// 点击链路照 <c>Setting.onChangeTopDown</c> 原样走一遍（写 view → ResetPan → 重摆 → 落盘），
/// 只不过入口换成按钮，并且顺手把设置页那个 toggle 的值也刷一遍。
/// </summary>
public static class ViewToggleButton
{
    // ── 设计稿常量（1920×986 基准，UI 单位＝像素）─────────────────────────
    /// <summary>方钮边长（屏幕像素，恒定，不随 ksb 缩放 —— 保证任何分辨率下都是同一个点按目标）。</summary>
    public const int BoxPx = 56;
    /// <summary>方钮中心比按钮列面板原点高多少屏幕像素（实测 80 ⇒ 落在 y212~344 那条空档内）。</summary>
    public const float AbovePanelPx = 80f;

    /// <summary>
    /// 「系统设置 → 俯视角（平面图）」**下面**那一行的键名：显示视角切换按钮，**默认开**。
    ///
    /// 用 `static` 存而**不加实例字段**（发布 DLL 的字段布局必须与已烘焙资源逐字段对齐）。
    /// 读值自带惰性加载：设置窗口没开过也能拿到存档值（同 <c>Program.EnsureViewLoaded</c> 的做法）。
    /// </summary>
    public const string SettingKey = "viewToggleBtn_";

    static bool settingLoaded = false;
    static bool settingVisible = true;

    /// <summary>设置项真源：要不要在战斗里显示这颗钮。<b>默认 true（开）</b>。</summary>
    public static bool SettingVisible
    {
        get
        {
            if (!settingLoaded)
            {
                settingLoaded = true;
                settingVisible = UIHelper.fromStringToBool(Config.Get(SettingKey, "1"));
            }
            return settingVisible;
        }
        set
        {
            settingLoaded = true;
            settingVisible = value;
        }
    }

    // ── 贴图 ────────────────────────────────────────────────────────────
    const string TexNormal = "texture/ui/viewC_box_normal_56.png";
    const string TexTop = "texture/ui/viewC_box_top_56.png";

    // ⛔ 纯色/底板用不到，但**万一**要用纯色矩形，尺寸必须偶数（1×1 会让绘制矩形归零）。
    //    这里保留一个 4×4 的共享白纹理，抄 DuelUndo.WhiteTex 的结论。
    static Texture2D boxTex = null;
    static Texture2D texNormal = null;
    static Texture2D texTop = null;
    static bool texTried = false;

    // ── 节点 ────────────────────────────────────────────────────────────
    static GameObject root = null;      // 自建 UIPanel（1 单位 = 1 像素）
    static UITexture box = null;        // 方钮本体
    static BoxCollider hit = null;      // 点按命中区
    static bool lastTop = false;
    static bool built = false;
    static bool lastVisible = false;
    static bool visibleLogged = false;
    static float lastVisLogAt = -99f;

    // ── 悬停提示（复用 gameInfo 那套黑底白字跟随提示的观感）────────────────
    static GameObject tipRoot = null;
    static UITexture tipBg = null;
    static UILabel tipLabel = null;
    static string tipShown = null;

    // =====================================================================
    // 每帧入口（由 gameInfo.Update 调用）
    // =====================================================================
    public static void Tick(UITexture pan)
    {
        if (pan == null)
        {
            return;
        }
        // ── 可见性门：① 设置项开着 ② **确实在战斗里** ③ 按钮列自己在显示 ──
        // ⛔ ② 的判据必须是 `ocgcore.isShowed`，不能只看按钮列的 activeInHierarchy：
        //    按钮列在主菜单/大厅/卡组编辑器一带也会处于激活态，光看它这颗钮会漏到
        //    对局外面去。`isShowed` 正是 `Program.fixALLcamerasPreFrame` 里那道
        //    `duelView`（换算只在「对局视角」一个消费点做）用的同一个判据 ⇒ 口径一致。
        bool visible = SettingVisible && InDuel() && pan.gameObject.activeInHierarchy;
        // 可见性自述：**第一次就打**，之后才是「变了才打」。
        // ⛔ 原本只打「变化」⇒ 初始就隐藏时（设置项=关）一次都不打 ——
        //    日志里连一条 `visible=… cfg=… duel=…` 都没有，验收只能靠
        //    「没有绘制回执」这种间接证据，缺一句「它知道自己被关掉了」的自述。
        // 心跳 5s 一次：可见性长期不变时也要有读数，否则「战斗外」与「战斗内」
        // 两个时刻的判据无从分别（关掉设置项那一档整局一条都不打）。
        float nowT = Time.realtimeSinceStartup;
        if (!visibleLogged || visible != lastVisible || nowT - lastVisLogAt >= 5f)
        {
            visibleLogged = true;
            lastVisible = visible;
            lastVisLogAt = nowT;
            QuickTestTrace.Log("viewbtn", "visible=" + (visible ? 1 : 0)
                + " cfg=" + (SettingVisible ? 1 : 0)
                + " duel=" + (InDuel() ? 1 : 0)
                + " pan=" + (pan.gameObject.activeInHierarchy ? 1 : 0));
        }
        if (!visible)
        {
            if (root != null && root.activeSelf)
            {
                root.SetActive(false);
            }
            // 掉出战斗时顺手收起提示，别让它挂在空处
            if (tipRoot != null && tipRoot.activeSelf)
            {
                tipRoot.SetActive(false);
            }
            return;
        }
        bool want = Program.topDown;
        if (built && lastTop != want)
        {
            lastTop = want;
            ApplyState();
        }
        if (!built)
        {
            if (!Build(pan))
            {
                return;
            }
        }
        if (!root.activeSelf)
        {
            root.SetActive(true);
        }
        float unit = PanelUnit();
        if (unit <= 0f)
        {
            return;
        }
        // 右缘钉在按钮列面板右缘；中心比面板原点高 AbovePanelPx。
        Vector3 edgeWorld = pan.transform.TransformPoint(new Vector3(pan.width * 0.5f, 0f, 0f));
        Camera cam = Program.camera_main_2d;
        if (cam == null)
        {
            return;
        }
        Vector3 edge = cam.WorldToScreenPoint(edgeWorld);
        // ⚠ 面板几何**不许只打一次**：第一版在第一次 Tick 就打，那一刻窗口分辨率还没落定
        //   （实测 Screen.width 仍是 2520，orgScreen.x 报到 2115），
        //   拿它当「面板右缘」基准就整整差 300px。改成跟着绘制回执一起打 ——
        //   两条日志读的是**同一帧**，探针才敢拿它们互相对拍。
        panelW = pan.width;
        panelEdge = edge;
        panelOrg = cam.WorldToScreenPoint(pan.transform.position);
        Vector3 wantScreen = new Vector3(edge.x - BoxPx * 0.5f, edge.y + AbovePanelPx, 0f);
        // ⛔ 这里写的是**世界坐标**，不要再除 unit。
        //   `root` 没有父节点 ⇒ localPosition 就是 worldPosition；而 `root` 自己的 scale=unit
        //   负责把它的**子节点**（像素单位）换算成世界。所以除一次会把落位放大 unit 倍
        //   （本机 unit≈0.0019，实测屏幕落点从 (1877,268) 跑到 (599138,111390)，
        //    探针 `inFrustum=False drawCall=True` —— 控件建好了，就是没进视锥）。
        root.transform.position = cam.ScreenToWorldPoint(wantScreen);
        ProbeDraw("tick");

        // 悬停判定：与 gameInfo.updateGrayTip 同一口径（UI 相机 Raycast 命中谁）。
        bool over = UICamera.Raycast(Input.mousePosition)
            && UICamera.lastHit.collider != null
            && UICamera.lastHit.collider.gameObject == hit.gameObject;
        UpdateHover(over);
    }

    // =====================================================================
    // 懒创建
    // =====================================================================
    static bool Build(UITexture pan)
    {
        try
        {
            Camera cam = Program.camera_main_2d;
            if (cam == null || Program.ui_main_2d == null)
            {
                return false;
            }
            float unit = PanelUnit();
            if (unit <= 0f)
            {
                return false;
            }
            int depth = 900;
            for (int i = 0; i < UIPanel.list.Count; i++)
            {
                if (UIPanel.list[i] != null && UIPanel.list[i].depth + 20 > depth)
                {
                    depth = UIPanel.list[i].depth + 20;
                }
            }

            root = new GameObject("view_toggle_panel");
            root.layer = Program.ui_main_2d.layer;
            root.transform.localScale = new Vector3(unit, unit, unit);
            UIPanel panel = root.AddComponent<UIPanel>();
            panel.depth = depth;
            panel.clipping = UIDrawCall.Clipping.None;

            GameObject go = new GameObject("view_toggle_box");
            go.layer = root.layer;
            go.transform.SetParent(root.transform, false);
            // UITexture（不是 UISprite）：贴图是整张 PNG、不走图集。
            box = go.AddComponent<UITexture>();
            box.pivot = UIWidget.Pivot.Center;
            // ⛔ 运行时 new 的 UITexture 必须显式置 path=""，否则 OnStart 下一帧
            //    会因 mOutPath==null(≠"") 把 mainTexture 冲成 null ⇒ 底图不渲染。
            box.path = "";
            Shader sh = DuelUndo.UiTexShader();
            if (sh != null)
            {
                box.shader = sh;
            }
            box.depth = 0;
            box.SetDimensions(BoxPx, BoxPx);

            hit = go.AddComponent<BoxCollider>();
            hit.size = new Vector3(BoxPx, BoxPx, 1f);
            UIHelper.registUIEventTriggerForClick(go, OnClicked);
            // ⛔⛔ **必须显式重建一次**。运行期 new 出来的 UIPanel 不会自动把手上的控件
            //    编进绘制列表：不调这一句，widget 的 `drawCall` 恒为 null ——
            //    尺寸/位置/视锥/贴图全都对，日志里也一切正常，**屏幕上却一个像素都不画**
            //    （同 DuelUndo 遮罩那次「压暗没实现」的坑，见 mask quad 的 drawCall 探针）。
            panel.RebuildAllDrawCalls();
            ProbeDraw("built");

            built = true;
            lastTop = Program.topDown;
            ApplyState();
            EnsureTextures();
            return true;
        }
        catch (Exception e)
        {
            QuickTestTrace.Log("viewbtn", "build failed " + e);
            built = false;
            return false;
        }
    }

    // =====================================================================
    // 绘制回执探针
    // ---------------------------------------------------------------------
    // 「建好了但一个像素都不画」是这个设计最阴的失败模式：尺寸、位置、视锥、贴图全对，
    // 探针也一路报绿，屏幕上就是空的。所以单独把「真正被填进顶点的那块矩形」记出来
    // —— `worldCorners` 是按 width/height 算的，顶点退化时它照样报满尺寸，抓不到故障。
    static float probeAt = 0f;
    static float panelW = 0f;
    static Vector3 panelEdge = Vector3.zero;
    static Vector3 panelOrg = Vector3.zero;

    static void ProbeDraw(string why)
    {
        try
        {
            float now = Time.realtimeSinceStartup;
            if (why != "built" && now - probeAt < 2f)
            {
                return;
            }
            probeAt = now;
            if (box == null)
            {
                return;
            }
            UIPanel panel = root != null ? root.GetComponent<UIPanel>() : null;
            Vector4 dd = box.drawingDimensions;
            Vector3[] wc = box.worldCorners;
            Camera cam = Program.camera_main_2d;
            bool inFrustum = false;
            if (cam != null)
            {
                Bounds wb = new Bounds(wc[0], Vector3.zero);
                for (int i = 1; i < wc.Length; i++)
                {
                    wb.Encapsulate(wc[i]);
                }
                inFrustum = GeometryUtility.TestPlanesAABB(
                    GeometryUtility.CalculateFrustumPlanes(cam), wb);
            }
            Vector3 sp = new Vector3(-999f, -999f, -1f);
            if (cam != null)
            {
                sp = cam.WorldToScreenPoint(wc[0]);
            }
            QuickTestTrace.Log("viewbtn", "draw why=" + why
                + " panel=" + (panel == null ? "NULL" : "d" + panel.depth)
                + " active=" + (root != null && root.activeInHierarchy)
                + " visible=" + box.isVisible
                + " en=" + box.enabled
                + " tex=" + (box.mainTexture == null ? "NULL"
                             : box.mainTexture.width + "x" + box.mainTexture.height)
                + " rectW=" + (dd.z - dd.x).ToString("F1")
                + " rectH=" + (dd.w - dd.y).ToString("F1")
                + " drawCall=" + (box.drawCall != null)
                + " inFrustum=" + inFrustum
                + " screen=" + (int)sp.x + "," + (int)sp.y
                + " colA=" + box.color.a.ToString("F2")
                + " layer=" + root.layer
                + " panelW=" + panelW.ToString("F0")
                + " panelOrgScreen=" + (int)panelOrg.x + "," + (int)panelOrg.y
                + " panelEdgeScreen=" + (int)panelEdge.x + "," + (int)panelEdge.y
                + " scr=" + Screen.width + "x" + Screen.height);
        }
        catch (Exception e)
        {
            QuickTestTrace.Log("viewbtn", "probe failed " + e.Message);
        }
    }

    // =====================================================================
    // 状态：换贴图
    // =====================================================================
    static void ApplyState()
    {
        EnsureTextures();
        if (box == null)
        {
            return;
        }
        Texture2D t = lastTop ? texTop : texNormal;
        if (t != null)
        {
            box.mainTexture = t;
        }
        QuickTestTrace.Log("viewbtn", "state top=" + (lastTop ? 1 : 0)
            + " tex=" + (t == null ? "NULL" : t.width + "x" + t.height)
            + " at=" + root.transform.localPosition);
    }

    /// <summary>两张 56×56 一次读盘解码，之后只换引用（切换瞬间绝不解码，会掉帧）。</summary>
    static void EnsureTextures()
    {
        if (texTried)
        {
            return;
        }
        texTried = true;
        texNormal = Load(TexNormal);
        texTop = Load(TexTop);
        QuickTestTrace.Log("viewbtn", "tex normal=" + (texNormal == null ? "NULL" : "ok")
            + " top=" + (texTop == null ? "NULL" : "ok"));
    }

    static Texture2D Load(string path)
    {
        try
        {
            if (!File.Exists(path))
            {
                return null;
            }
            byte[] data = File.ReadAllBytes(path);
            // 2×2 占位：LoadImage 会按图片实际尺寸覆盖。
            Texture2D pic = new Texture2D(2, 2);
            if (!pic.LoadImage(data))
            {
                return null;
            }
            return pic;
        }
        catch (Exception e)
        {
            QuickTestTrace.Log("viewbtn", "load " + path + " failed " + e.Message);
            return null;
        }
    }

    // =====================================================================
    // 点击：不二次确认，直接切（视角只动相机 pitch 与附属物朝向，不动手牌/卡位）
    // =====================================================================
    static void OnClicked(GameObject obj)
    {
        try
        {
            bool next = !Program.topDown;
            QuickTestTrace.Log("viewbtn", "click top=" + (Program.topDown ? 1 : 0)
                + " -> " + (next ? 1 : 0));
            ApplyTopDown(next);
            lastTop = next;
            ApplyState();
        }
        catch (Exception e)
        {
            QuickTestTrace.Log("viewbtn", "click failed " + e);
        }
    }

    /// <summary>
    /// 切视角的**唯一出口**：按钮与设置页都走它，所以两边的副作用顺序完全一致。
    /// 顺序照抄 <c>Setting.onChangeTopDown</c>：写 view → ResetPan → 重摆 → 落盘。
    /// </summary>
    public static void ApplyTopDown(bool value)
    {
        Program.EnsureViewLoaded();
        Program.view = value ? Program.TableauView.TopDown : Program.TableauView.Tilt60;
        // 「拉镜头」属于上一个视角的取景，换视角一律复位（不清会出现「画面歪了」）。
        TableauLayout.ResetPan();
        // 重摆场景放在落盘之前：写配置万一抛异常，视角也该照样切。
        try
        {
            if (Program.I() != null && Program.I().ocgcore != null)
            {
                Program.I().ocgcore.realize(true);
            }
        }
        catch (Exception e)
        {
            QuickTestTrace.Log("viewbtn", "realize failed " + e.Message);
        }
        Config.Set("topDown_", UIHelper.fromBoolToString(value));
        // 设置页那个 toggle 也刷一遍（它可能没开过窗口，那边 SyncTopDownRow 会自己 return）。
        Setting st = Program.I() != null ? Program.I().setting : null;
        if (st != null)
        {
            st.SyncTopDownRow(value);
        }
        try
        {
            if (st != null)
            {
                st.save();
            }
        }
        catch (Exception e)
        {
            QuickTestTrace.Log("viewbtn", "save failed " + e.Message);
        }
        QuickTestTrace.Log("view", "viewbtn topDown=" + (Program.topDown ? 1 : 0)
            + " stamp=" + Program.viewStamp);
    }

    // =====================================================================
    // 悬停：提亮 + 黑底白字跟随提示（提示写的是「点下去会变成」的目标状态）
    // =====================================================================
    static void UpdateHover(bool over)
    {
        if (box != null)
        {
            Color c = Color.white;
            c.a = over ? 1f : 0.86f;
            box.color = c;
        }
        string want = null;
        if (over)
        {
            // 提示写的是「点下去会变成」的**目标状态**，前缀「切换到」（用户 2026-09-30 定）。
            want = Program.topDown
                ? InterString.Get("切换到正常视角@ui")
                : InterString.Get("切换到俯视角（平面图）@ui");
        }
        if (string.IsNullOrEmpty(want))
        {
            if (tipRoot != null && tipRoot.activeSelf)
            {
                tipRoot.SetActive(false);
            }
            tipShown = null;
            return;
        }
        if (!EnsureTip(want))
        {
            return;
        }
        Camera cam = Program.camera_main_2d;
        float unit = PanelUnit();
        if (cam == null || unit <= 0f)
        {
            return;
        }
        // ⛔ 同上：tipRoot 也没有父节点 ⇒ 直接给世界坐标，别除 unit。
        // ⚠ 这颗钮贴的是**屏幕右缘**（右缘 1905 / 屏宽 1920），照「鼠标 +22/+22」
        //   摆提示会整条掉出屏外（实跑第一版就是这样：`tip=正常视角` 打了日志、屏幕上什么都没有）。
        //   ⇒ 靠右缘时翻到鼠标左侧，靠上缘时翻到下方；两个方向都做，提示永不出屏。
        int tw = tipBg != null ? tipBg.width : 0;
        int th = tipBg != null ? tipBg.height : 0;
        float mx = Input.mousePosition.x;
        float my = Input.mousePosition.y;
        float ox, oy;
        if (mx + 22f + tw > Screen.width)
        {
            ox = mx - 22f - tw;
        }
        else
        {
            ox = mx + 22f;
        }
        if (my + 22f + th > Screen.height)
        {
            oy = my - 22f - th;
        }
        else
        {
            oy = my + 22f;
        }
        tipRoot.transform.position = cam.ScreenToWorldPoint(new Vector3(ox, oy, 0f));
        tipRoot.SetActive(true);
        if (tipShown != want)
        {
            tipShown = want;
            QuickTestTrace.Log("viewbtn", "tip=" + want
                + " mouse=" + (int)mx + "," + (int)my
                + " tipAt=" + (int)ox + "," + (int)oy
                + " flipX=" + (ox < mx) + " flipY=" + (oy < my));
        }
    }

    static bool EnsureTip(string txt)
    {
        try
        {
            if (tipRoot == null)
            {
                Camera cam = Program.camera_main_2d;
                float unit = PanelUnit();
                if (cam == null || Program.ui_main_2d == null || unit <= 0f)
                {
                    return false;
                }
                int depth = 1000;
                for (int i = 0; i < UIPanel.list.Count; i++)
                {
                    if (UIPanel.list[i] != null && UIPanel.list[i].depth + 20 > depth)
                    {
                        depth = UIPanel.list[i].depth + 20;
                    }
                }
                tipRoot = new GameObject("view_toggle_tip");
                tipRoot.layer = Program.ui_main_2d.layer;
                tipRoot.transform.localScale = new Vector3(unit, unit, unit);
                UIPanel panel = tipRoot.AddComponent<UIPanel>();
                panel.depth = depth;
                panel.clipping = UIDrawCall.Clipping.None;

                GameObject bg = new GameObject("view_toggle_tip_bg");
                bg.layer = tipRoot.layer;
                bg.transform.SetParent(tipRoot.transform, false);
                tipBg = bg.AddComponent<UITexture>();
                // ⛔ 纯色底必须用自建的偶数尺寸白纹理（Texture2D.whiteTexture 取不到），
                //    且 path 要显式置 ""（见 EnsureTextures 上方的说明）。
                tipBg.mainTexture = BoxTex();
                tipBg.path = "";
                Shader sh = DuelUndo.UiTexShader();
                if (sh != null)
                {
                    tipBg.shader = sh;
                }
                tipBg.color = new Color(0.05f, 0.05f, 0.07f, 0.94f);
                tipBg.depth = 0;

                GameObject tx = new GameObject("view_toggle_tip_text");
                tx.layer = tipRoot.layer;
                tx.transform.SetParent(tipRoot.transform, false);
                tipLabel = tx.AddComponent<UILabel>();
                UILabel tpl = DuelUndo.FindLabelTemplate();
                if (tpl != null)
                {
                    tipLabel.bitmapFont = tpl.bitmapFont;
                    tipLabel.trueTypeFont = tpl.trueTypeFont;
                    tipLabel.fontStyle = tpl.fontStyle;
                    tipLabel.applyGradient = tpl.applyGradient;
                    tipLabel.gradientTop = tpl.gradientTop;
                    tipLabel.gradientBottom = tpl.gradientBottom;
                }
                tipLabel.fontSize = 22;
                tipLabel.alignment = NGUIText.Alignment.Center;
                tipLabel.pivot = UIWidget.Pivot.Center;
                tipLabel.color = Color.white;
                tipLabel.depth = 1;
                // 同上：运行期新建的 panel/控件要显式重建一次，否则不产生 drawcall。
                panel.RebuildAllDrawCalls();
            }
            if (tipLabel.text != txt)
            {
                tipLabel.text = txt;
                int w = txt.Length * 24 + 30;
                int h = 40;
                tipBg.SetDimensions(w, h);
                tipBg.transform.localPosition = new Vector3(w / 2, -h / 2, 0f);
                tipLabel.SetDimensions(w - 12, h - 6);
                tipLabel.transform.localPosition = new Vector3(w / 2, -h / 2, 0f);
            }
            return true;
        }
        catch (Exception e)
        {
            QuickTestTrace.Log("viewbtn", "tip failed " + e.Message);
            return false;
        }
    }

    /// <summary>4×4 白纹理（<b>偶数尺寸是硬要求</b>：1×1 会让 NGUI 的绘制矩形宽高双双归零）。</summary>
    static Texture2D BoxTex()
    {
        if (boxTex == null)
        {
            boxTex = new Texture2D(4, 4, TextureFormat.RGBA32, false);
            Color[] px = new Color[16];
            for (int i = 0; i < px.Length; i++)
            {
                px[i] = Color.white;
            }
            boxTex.SetPixels(px);
            boxTex.Apply();
        }
        return boxTex;
    }

    /// <summary>自建面板的「1 局部单位 = 多少世界单位」＝每像素多少世界单位。</summary>
    static float PanelUnit()
    {
        Camera cam = Program.camera_main_2d;
        if (cam == null)
        {
            return 0f;
        }
        Vector3 bl = cam.ScreenToWorldPoint(new Vector3(0f, 0f, 0f));
        Vector3 tr = cam.ScreenToWorldPoint(new Vector3(Screen.width, Screen.height, 0f));
        float unit = Mathf.Abs(tr.y - bl.y) / Mathf.Max(1, Screen.height);
        return unit;
    }

    /// <summary>
    /// 现在是不是真的在打一局对战。判据与 <c>Program.fixALLcamerasPreFrame</c> 的
    /// <c>duelView</c> 逐项一致（Program 活着 + ocgcore 活着 + isShowed）。
    /// </summary>
    public static bool InDuel()
    {
        try
        {
            return Program.I() != null && Program.I().ocgcore != null
                && Program.I().ocgcore.isShowed;
        }
        catch (Exception)
        {
            return false;
        }
    }
}
