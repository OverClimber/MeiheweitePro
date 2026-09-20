using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using YGOSharp.OCGWrapper.Enums;
public class DeckManager : ServantWithCardDescription
{
    #region UI

    public enum Condition
    {
        editDeck = 1,
        changeSide = 2,
    }

    public Condition condition = Condition.editDeck;

    public GameObject gameObjectSearch;

    public GameObject gameObjectDetailedSearch;

    UIPopupList UIPopupList_main;

    UIPopupList UIPopupList_ban;    

    UIPopupList UIPopupList_second;

    UIPopupList UIPopupList_race;

    UIPopupList UIPopupList_attribute;

    UIPopupList UIPopupList_pack;

    UIInput UIInput_level;

    UIInput UIInput_atk;

    UIInput UIInput_def;

    UIInput UIInput_search;

    UIToggle[] UIToggle_effects = new UIToggle[32];

    SuperScrollView superScrollView = null;

    UIPopupList UIPopupList_banlist;

    public override void initialize()
    {
        gameObjectSearch = create
            (
            Program.I().new_ui_search,
            Program.camera_back_ground_2d.ScreenToWorldPoint(new Vector3(Screen.width + 600, Screen.height / 2, 600)),
            new Vector3(0, 0, 0),
            false,
            Program.ui_back_ground_2d
            );
        gameObjectDetailedSearch = create
            (
            Program.I().new_ui_searchDetailed,
            Program.camera_main_2d.ScreenToWorldPoint(new Vector3(Screen.width, Screen.height * 2f, 0)),
            new Vector3(0, 0, 0),
            false,
            Program.ui_main_2d
            );
        UIHelper.InterGameObject(gameObjectSearch);
        UIHelper.InterGameObject(gameObjectDetailedSearch);
        shiftCondition(Condition.editDeck);
        UIHelper.registEvent(gameObjectSearch, "detailed_", onClickDetail);
        UIHelper.registEvent(gameObjectSearch, "search_", onClickSearch);
        UIPopupList_main = UIHelper.getByName<UIPopupList>(gameObjectDetailedSearch, "main_");
        UIPopupList_ban = UIHelper.getByName<UIPopupList>(gameObjectDetailedSearch, "ban_");
        UIPopupList_second = UIHelper.getByName<UIPopupList>(gameObjectDetailedSearch, "second_");
        UIPopupList_race = UIHelper.getByName<UIPopupList>(gameObjectDetailedSearch, "race_");
        UIPopupList_attribute = UIHelper.getByName<UIPopupList>(gameObjectDetailedSearch, "attribute_");
        UIPopupList_pack = UIHelper.getByName<UIPopupList>(gameObjectDetailedSearch, "pack_");
        UIInput_search = UIHelper.getByName<UIInput>(gameObjectSearch, "input_");
        UIInput_search.value = "";
        UIInput_level = UIHelper.getByName<UIInput>(gameObjectDetailedSearch, "level_");
        UIInput_atk = UIHelper.getByName<UIInput>(gameObjectDetailedSearch, "atk_");
        UIInput_def = UIHelper.getByName<UIInput>(gameObjectDetailedSearch, "def_");
        for (int i = 0; i < 32; i++)
        {
            UIToggle_effects[i] = UIHelper.getByName<UIToggle>(gameObjectDetailedSearch, "T (" + (i+1).ToString() + ")");
            UIHelper.trySetLableText(UIToggle_effects[i].gameObject, GameStringManager.get_unsafe(1100 + i));
            UIToggle_effects[i].GetComponentInChildren<UILabel>().overflowMethod = UILabel.Overflow.ClampContent;
        }
        UIPopupList_pack.Clear();
        UIPopupList_pack.AddItem(GameStringManager.get_unsafe(1310));
        foreach (var item in YGOSharp.PacksManager.packs)
        {
            UIPopupList_pack.AddItem(item.fullName);
        }
        UIPopupList_main.Clear();
        UIPopupList_main.AddItem(GameStringManager.get_unsafe(1310));
        UIPopupList_main.AddItem(GameStringManager.get_unsafe(1312));
        UIPopupList_main.AddItem(GameStringManager.get_unsafe(1313));
        UIPopupList_main.AddItem(GameStringManager.get_unsafe(1314));
        UIPopupList_ban.Clear();
        UIPopupList_ban.AddItem(GameStringManager.get_unsafe(1310));
        UIPopupList_ban.AddItem(GameStringManager.get_unsafe(1316));
        UIPopupList_ban.AddItem(GameStringManager.get_unsafe(1317));
        UIPopupList_ban.AddItem(GameStringManager.get_unsafe(1318));
        UIPopupList_ban.AddItem(GameStringManager.get_unsafe(1481));
        UIPopupList_ban.AddItem(GameStringManager.get_unsafe(1482));
        UIPopupList_ban.AddItem(GameStringManager.get_unsafe(1483));
        UIPopupList_ban.AddItem(GameStringManager.get_unsafe(1484));
        UIPopupList_ban.AddItem(GameStringManager.get_unsafe(1485));
        clearAll();
        UIHelper.registEvent(UIPopupList_main.gameObject, onUIPopupList_main);
        UIHelper.registEvent(UIPopupList_second.gameObject, onUIPopupList_second);
        superScrollView = new SuperScrollView
           (
           UIHelper.getByName<UIPanel>(gameObjectSearch, "panel_"),
           UIHelper.getByName<UIScrollBar>(gameObjectSearch, "bar_"),
           itemOnListProducer,
           86
           );
        Program.go(500, () => {
            List<MonoCardInDeckManager> cs = new List<MonoCardInDeckManager>();
            for (int i = 0; i < 300; i++)
            {
                cs.Add(createCard());
            }
            for (int i = 0; i < 300; i++)
            {
                destroyCard(cs[i]);
            }
        });


    }

    GameObject itemOnListProducer(string[] Args)
    {
        GameObject returnValue = null;
        returnValue = create(Program.I().new_ui_cardOnSearchList, Vector3.zero, Vector3.zero, false, Program.ui_back_ground_2d);
        UIHelper.getRealEventGameObject(returnValue).name = Args[0];
        UIHelper.trySetLableText(returnValue, Args[2]);
        cardPicLoader cardPicLoader_ = UIHelper.getRealEventGameObject(returnValue).AddComponent<cardPicLoader>();
        cardPicLoader_.code = int.Parse(Args[0]);
        cardPicLoader_.data = YGOSharp.CardsManager.Get(int.Parse(Args[0]));
        cardPicLoader_.uiTexture = UIHelper.getByName<UITexture>(returnValue, "pic_");
        cardPicLoader_.ico = UIHelper.getByName<ban_icon>(returnValue);
        cardPicLoader_.ico.show(3);
        return returnValue;
    }

    public override void applyHideArrangement()
    {
        base.applyHideArrangement();
        Program.cameraFacing = false;
        iTween.MoveTo(gameObjectSearch, Program.camera_main_2d.ScreenToWorldPoint(new Vector3(Screen.width + 600, Screen.height / 2, 600)), 1.2f);
        refreshDetail();
    }

    public override void applyShowArrangement()
    {
        base.applyShowArrangement();
        Program.cameraFacing = true;
        UITexture tex = UIHelper.getByName<UITexture>(gameObjectSearch, "under_");
        tex.height = Screen.height;
        iTween.MoveTo(gameObjectSearch, Program.camera_main_2d.ScreenToWorldPoint(new Vector3(Screen.width - tex.width / 2, Screen.height / 2, 0)), 1.2f);
        refreshDetail();
    }

    void onLF()
    {
        currentBanlist = YGOSharp.BanlistManager.GetByName(UIPopupList_banlist.value);
    }

    public void shiftCondition(Condition condition)
    {
        this.condition = condition;
        switch (condition)
        {
            case Condition.editDeck:
                UIHelper.setParent(gameObjectSearch, Program.ui_back_ground_2d);
                SetBar(Program.I().new_bar_editDeck, 0, 230);
                UIPopupList_banlist = UIHelper.getByName<UIPopupList>(toolBar, "lfList_");
                // 禁限表选择器**按模式过滤**：OCG 看不到 RD 表，RD 只看到 RD 表（_plan_rdmode.md 清单 5）。
                // 过滤后为空会回落成完整清单，见 GameModeManager.FilterBanlistNames。
                // ⚠ 这条分支每次进编辑卡组都会重跑，所以模式切换不需要额外的刷新钩子；
                //   也**不许**用「表数量」做下标假设（表清单随模式变长变短）。
                var banlistNames = GameModeManager.FilterBanlistNames(YGOSharp.BanlistManager.getAllName());
                UIPopupList_banlist.items = banlistNames;
                UIPopupList_banlist.value = UIPopupList_banlist.items[0];
                currentBanlist = YGOSharp.BanlistManager.GetByName(UIPopupList_banlist.items[0]);
                // 清单全量落盘：RD 线的判据是「RD 表在 / 不在这个清单里」，
                // 光凭条数判不了（OCG 的表有几十张，加一减一看不出来）。
                // ⚠ `picked=` **必须是这一行的最后一个字段**：RD 表名带空格（`2026.7 RD`），
                //   验收侧按「等号后一直到行尾」取值；夹在中间会被空格切断，
                //   读到 `2026.7` 这种半个名字，B5b 就会假红（2026-09-19 踩过）。
                QuickTestTrace.Log("mode", "banlist items=" + banlistNames.Count
                    + " all=[" + string.Join("|", banlistNames.ToArray()) + "]"
                    + " mode=" + GameModeManager.ModeLabel
                    + " picked=" + UIPopupList_banlist.items[0]);
                UIHelper.registEvent(toolBar, "rand_", rand);
                UIHelper.registEvent(toolBar, "sort_", sort);
                UIHelper.registEvent(toolBar, "clear_", clear);
                UIHelper.registEvent(toolBar, "home_", home);
                UIHelper.registEvent(toolBar, "save_", () => { onSave(); });
                UIHelper.registEvent(toolBar, "lfList_", onLF);
                UIHelper.registEvent(toolBar, "copy_", onCopy);
                createTestButton();
                break;
            case Condition.changeSide:
                UIHelper.setParent(gameObjectSearch, Program.ui_main_2d);
                SetBar(Program.I().new_bar_changeSide, 0, 230);
                UIPopupList_banlist = null;
                UIHelper.registEvent(toolBar, "rand_", rand);
                UIHelper.registEvent(toolBar, "sort_", sort);
                UIHelper.registEvent(toolBar, "finish_", home);
                UIHelper.registEvent(toolBar, "input_", onChat);
                break;
            default:
                break;
        }
    }

    /// <summary>
    /// 排查用：把卡组编辑器工具条上几个按钮的屏幕坐标落到轨迹里。
    ///
    /// 两个坑都在 <c>Servant.showBarOnly()</c> 里：
    ///   ・定位用的是 <c>Program.camera_back_ground_2d</c>（不是 camera_main_2d），
    ///     相机用错，报出来的坐标就是错的；
    ///   ・入场是 0.6 秒的 iTween，而且整体缩放 <c>Screen.height / 700</c>（1920x986 下约 1.41 倍）——
    ///     show() 刚回来时读到的还是「藏在屏幕下方」的中途位置。
    /// 所以这里由每帧节流调用（见 preFrameFunction），读到的永远是停稳之后的位置。
    /// Unity 屏幕坐标是左下原点，这里换算成 Windows 的上左原点。
    /// </summary>
    private void dumpEditDeckButtonPositions()
    {
        if (!QuickTestTrace.Enabled || toolBar == null || Program.camera_back_ground_2d == null)
        {
            return;
        }
        string[] names = new string[] { "home_", "save_", "test_", "copy_", "lfList_" };
        for (int i = 0; i < names.Length; i++)
        {
            UIButton b = UIHelper.getByName<UIButton>(toolBar, names[i]);
            if (b == null)
            {
                QuickTestTrace.Log("btnpos", "editor " + names[i] + " = null");
                continue;
            }
            Vector3 sp = Program.camera_back_ground_2d.WorldToScreenPoint(b.transform.position);
            QuickTestTrace.Log("btnpos", "editor " + names[i]
                + " screen=(" + Mathf.RoundToInt(sp.x) + ","
                + Mathf.RoundToInt(Screen.height - sp.y) + ")"
                + " scr=" + Screen.width + "x" + Screen.height
                + " active=" + b.gameObject.activeInHierarchy
                + " enabled=" + b.isEnabled);
        }

        // 搜索按钮不在 toolBar 上，它挂在 gameObjectSearch（同一张 ui_back_ground_2d 下），
        // 所以单独报一条 —— RD 线的池判据靠「点它一次、看 [mode] search 的 n=」，
        // 脚本没有这个坐标就只能猜（同 _verify_askconfirm 里 [opt] 给按钮坐标的用意）。
        UIButton searchBtn = UIHelper.getByName<UIButton>(gameObjectSearch, "search_");
        if (searchBtn == null)
        {
            QuickTestTrace.Log("btnpos", "editor search_ = null");
        }
        else
        {
            Vector3 sp = Program.camera_back_ground_2d.WorldToScreenPoint(searchBtn.transform.position);
            QuickTestTrace.Log("btnpos", "editor search_"
                + " screen=(" + Mathf.RoundToInt(sp.x) + ","
                + Mathf.RoundToInt(Screen.height - sp.y) + ")"
                + " active=" + searchBtn.gameObject.activeInHierarchy
                + " enabled=" + searchBtn.isEnabled);
        }

        // 关键字输入框（UIInput input_）也要报坐标：RD 线「编辑器搜得到先行卡」这条判据，
        // 脚本必须把关键字真打进这个框里，而键盘注入前得先点它一下拿焦点 —— 点完 search_
        // 焦点会从输入框上掉下来（doSearch → process 里才重新 isSelected=true），
        // 第二次搜索前不重新点一次就会「打字没反应」。同样只在落定后报（节流调用）。
        Transform inputT = UIHelper.getByName<Transform>(gameObjectSearch, "input_");
        if (inputT == null)
        {
            QuickTestTrace.Log("btnpos", "editor input_ = null");
        }
        else
        {
            Vector3 ip = Program.camera_back_ground_2d.WorldToScreenPoint(inputT.position);
            QuickTestTrace.Log("btnpos", "editor input_"
                + " screen=(" + Mathf.RoundToInt(ip.x) + ","
                + Mathf.RoundToInt(Screen.height - ip.y) + ")"
                + " active=" + inputT.gameObject.activeInHierarchy);
        }

        // 「高级搜索」入口 + 面板里的两个下拉（主类 / 种类）。
        // 为什么要这三个坐标：种类下拉现在**按卡池收敛**（RD 去掉同调/速攻、补上极大），
        // 而收敛结果只在面板展开后才看得见 —— 脚本要能自己点开「高级搜索」、再点开
        // 「种类」把那张清单拍下来人眼复核（自动判据走 [mode] secondItems / typeItems）。
        // ⚠ 面板挂在 ui_main_2d 下（见 initialize 里 gameObjectDetailedSearch 的 create），
        //   换算必须用 camera_main_2d —— 与 gameObjectSearch 那批（back_ground）不是一台相机。
        ReportButtonPos("editor detailed_",
            UIHelper.getByName<UIButton>(gameObjectSearch, "detailed_"),
            Program.camera_back_ground_2d);
        ReportButtonPos("editor main_",
            UIHelper.getByName<UIButton>(gameObjectDetailedSearch, "main_"),
            Program.camera_main_2d);
        ReportButtonPos("editor second_",
            UIHelper.getByName<UIButton>(gameObjectDetailedSearch, "second_"),
            Program.camera_main_2d);
    }

    /// <summary>报一个按钮的屏幕落点（`[btnpos]` 的口径只有这一处，别再各写一遍）。</summary>
    private void ReportButtonPos(string label, UIButton b, Camera cam)
    {
        if (b == null)
        {
            QuickTestTrace.Log("btnpos", label + " = null");
            return;
        }
        Vector3 sp = cam != null
            ? cam.WorldToScreenPoint(b.transform.position)
            : Vector3.zero;
        QuickTestTrace.Log("btnpos", label
            + " screen=(" + Mathf.RoundToInt(sp.x) + ","
            + Mathf.RoundToInt(Screen.height - sp.y) + ")"
            + " active=" + b.gameObject.activeInHierarchy
            + " enabled=" + b.isEnabled);
    }

    int lastBarDumpMs = -1;

    void onCopy()
    {
        string deckName = GameModeManager.DeckInUse;
        string newname = InterString.Get("[?]的副本", deckName);
        string newnamer = newname;
        int i = 1;
        while (File.Exists(GameModeManager.DeckPath(newnamer)))
        {
            newnamer = newname + i.ToString();
            i++;
        }
        RMSshow_input("onRename", InterString.Get("新的卡组名"), newnamer);
    }

    /// <summary>
    /// MDPro3 式「测试」按钮（参考 MDPro3 v1.3.7 更新日志：以第一位 WindBot 为对手，不洗牌进行决斗）。
    /// 克隆工具栏的 save_ 按钮生成，摆在最右端。
    /// 图标用 **texture/ui/test.png** —— 它是 go.png（▶）的**独立副本**，不跟对战工具条
    /// 上那个 go 共用同一张图：两边各自持有自己的素材，以后单独调哪一边都不会连带改到另一边。
    /// 点击 = 先保存当前卡组（AI 对局读取 deck/deckInUse.ydk），再一键不洗牌开局。
    /// </summary>
    private void createTestButton()
    {
        if (toolBar == null)
        {
            QuickTestTrace.Log("btn", "createTestButton abort: toolBar is null");
            return;
        }
        Transform saveT = toolBar.transform.Find("save_");
        if (saveT == null || toolBar.transform.Find("test_") != null)
        {
            QuickTestTrace.Log("btn", "createTestButton abort: saveT=" + (saveT != null)
                + " existing=" + (toolBar.transform.Find("test_") != null));
            return;
        }
        GameObject testBtn = UnityEngine.Object.Instantiate(saveT.gameObject);
        testBtn.name = "test_";
        testBtn.transform.SetParent(toolBar.transform, false);
        // 与 Ocgcore.tryCreateUndoButton 同一个坑：Instantiate 会把 save_ 运行时挂进
        // UIEventTrigger 的 hinter 委托一并复制，克隆体的 hinter.Start 会再挂一遍，
        // 导致悬停提示一次造两个、出悬停只消一个，残留的那个要在屏幕上赖几秒。
        UIEventTrigger testTrigger = testBtn.GetComponent<UIEventTrigger>();
        if (testTrigger != null)
        {
            testTrigger.onHoverOver.Clear();
            testTrigger.onHoverOut.Clear();
            testTrigger.onPress.Clear();
        }
        Vector3 p = saveT.localPosition;
        // 工具条按钮本地间距 40 UI（lfList_ -501.3 / clear_ -270 / rand_ -230 / sort_ -190
        // / save_ -150 / copy_ -110 / home_ -70 / setting_ -30）。
        // 测试按钮固定插在 save_ 左边一格（= save_.x - 40），
        // 原本占着这一格及更左边的控件（sort_ / rand_ / clear_ / lfList_）整体左移 40 让位。
        const float ToolBarSpacing = 40f;
        float testX = p.x - ToolBarSpacing;
        foreach (Transform child in toolBar.transform)
        {
            if (child == testBtn.transform)
            {
                continue;
            }
            Vector3 c = child.localPosition;
            if (c.x <= testX + 0.01f)
            {
                child.localPosition = new Vector3(c.x - ToolBarSpacing, c.y, c.z);
            }
        }
        testBtn.transform.localPosition = new Vector3(testX, p.y, p.z);
        UITexture tex = testBtn.GetComponentInChildren<UITexture>();
        if (tex != null)
        {
            // 「测试」独立素材：test.png = go.png 的副本（见方法注释）。
            tex.path = "test";
            Texture2D icon = GameTextureManager.get("test");
            if (icon != null)
            {
                tex.mainTexture = icon;
            }
        }
        hinter hint = testBtn.GetComponent<hinter>();
        if (hint != null)
        {
            // 与 MDPro3 的卡组界面一致：只显示「测试」两个字
            hint.str = InterString.Get("测试");
        }
        UIHelper.registEvent(toolBar, "test_", onClickTest);
        QuickTestTrace.Log("btn", "created test button name=" + testBtn.name
            + " localPos=" + testBtn.transform.localPosition
            + " texture=" + (tex != null ? tex.path : "null")
            + " toolbarChildren=" + toolBar.transform.childCount);
    }

    void onClickTest()
    {
        QuickTestTrace.Log("click", "onClickTest enter");
        if (!SettledForTest())
        {
            return;
        }
        onClickTestCore();
    }

    /// <summary>卡组界面最近一次显示的时刻（Program.TimePassed 毫秒）。见 <see cref="EditorSettleMs"/>。</summary>
    private int editorShownMs = -1;

    /// <summary>
    /// 「测试」按钮要等界面停稳多久才接受点击（毫秒）。
    ///
    /// 为什么需要：编辑器入场是 0.6 秒的 iTween（工具条下滑、列表滑出、3D 视角转），
    /// 期间整块面板都在动，而 NGUI 的碰撞体是**跟着 transform 走**的 —— 没停稳时，
    /// 「测试」的碰撞体扫过的位置与玩家看到的按钮位置并不一致，
    /// 在别处（比如右上角的筛选/搜索那一带）点一下就可能正好落在它身上。
    /// 这类误触的形态就是「偶尔发生 + 位置说不通」，用一个停稳门槛直接掐掉。
    /// </summary>
    private const int EditorSettleMs = 900;

    /// <summary>界面停稳了吗（见 <see cref="EditorSettleMs"/>）。只挡「点按钮」这条路径。</summary>
    private bool SettledForTest()
    {
        if (editorShownMs < 0)
        {
            return true;
        }
        int age = Program.TimePassed() - editorShownMs;
        // age < 0 = 计时基准被重置过（TimePassed 随场景重载复位），判不了就放行 ——
        // 宁可漏挡一次，也不能把「测试」按钮变成点不动。
        if (age >= 0 && age < EditorSettleMs)
        {
            QuickTestTrace.Log("click", "卡组界面入场才 " + age + "ms（< " + EditorSettleMs
                + "ms，还没停稳），忽略这次「测试」点击");
            return false;
        }
        return true;
    }

    /// <summary>
    /// 「测试」真正的动作体。与按钮那条路分开，是因为排查钩子（qt_endduel.on）
    /// 也要用它 —— 钩子已经等到 canSave 且卡组非空（那必然是停稳之后），不需要再等。
    /// </summary>
    void onClickTestCore()
    {
        if (testLaunching)
        {
            QuickTestTrace.Log("click", "转场进行中，忽略这次点击");
            return;
        }
        if (deck == null || deck.IMain.Count == 0)
        {
            QuickTestTrace.Log("click", "empty main deck -> abort");
            RMSshow_none(InterString.Get("主卡组是空的，先加点卡再测试。"));
            return;
        }
        // onSave 成功 = 当前卡组已写入 deck/deckInUse.ydk，AI 房间会读这份文件
        bool saved = onSave();
        QuickTestTrace.Log("click", "onSave=" + saved);
        if (saved)
        {
            startTestLaunch();
        }
    }

    /// <summary>「收起界面」动画的时长（毫秒）。取界面自带关窗动画里最长的一条（列表滑出 1.2 秒）。</summary>
    private const int TestHideAnimMs = 1250;

    /// <summary>
    /// 「进入测试战斗时播放动画」开关，读设置窗口里的 testHideAnim_（系统设置 → 最底下一行）。
    /// 默认关 = 点「测试」直接藏界面就开局；开了 = 先播收起动画（1.25 秒）再开局。
    /// </summary>
    private static bool TestHideAnim
    {
        get { return UIHelper.fromStringToBool(Config.Get("testHideAnim_", "0")); }
    }

    /// <summary>正在走「收起界面 + 开局」的转场，用来挡住连点。</summary>
    private bool testLaunching = false;

    /// <summary>
    /// 点「测试」之后的转场：先把卡组界面**带动画地收起来**，动画放完再走原来的开局链路。
    ///
    /// 为什么必须等动画放完：launchQuickTest() 里有两段**同步等待**（起 AI.Server、
    /// 等 WindBot 就绪），主线程会被阻塞约 0.7 秒 —— 这段时间 Unity 一帧都渲染不了。
    /// 动画放一半撞上阻塞就会定格在半路，比不做还难看。
    ///
    /// 用的是界面自带的关窗动画（applyHideArrangement）：工具条下滑 0.6 秒、
    /// 卡牌列表滑出 1.2 秒、3D 视角转走。这一步**不销毁任何数据**，
    /// 真正的清理由随后那次 hide() 完成 —— 和原来在 DuelStart 时做的是同一件事，
    /// 只是提前到了这里（此前那 4.9 秒里卡组界面一直杵在屏幕上、玩家什么都点不动）。
    ///
    /// 开局起不来（AI.Server / WindBot 没起来）时要把卡组界面放回去再报错：
    /// 提示是弹在卡牌说明面板里的，界面没回来就等于弹在空屏上。
    /// </summary>
    void startTestLaunch()
    {
        testLaunching = true;
        if (!TestHideAnim)
        {
            // 对比用：跳过收起动画，直接藏掉就开局
            QuickTestTrace.Log("click", "收起动画已禁用（TestHideAnim=false），直接 hide 后开局");
            hide();
            tryLaunchWithRecovery();
            return;
        }
        QuickTestTrace.Log("click", "先收起卡组界面，动画 " + TestHideAnimMs + "ms 后再开局");
        applyHideArrangement();
        Program.go(TestHideAnimMs, () =>
        {
            hide();
            tryLaunchWithRecovery();
        });
    }

    /// <summary>开局 + 失败恢复：起不来就把卡组界面放回来再报错（提示弹在说明面板里，空屏上看不见）。</summary>
    private void tryLaunchWithRecovery()
    {
        string failHint;
        bool ok = Program.I().aiRoom.tryLaunchQuickTest(true, out failHint);
        QuickTestTrace.Log("click", "tryLaunchQuickTest -> " + ok
            + (ok ? "" : "，原因=" + failHint));
        if (!ok)
        {
            // 起不来：把卡组界面放回来再报错，别让玩家对着空屏
            show();
            loadDeckFromYDK(GameModeManager.DeckPath(GameModeManager.DeckInUse));
            if (!string.IsNullOrEmpty(failHint))
            {
                RMSshow_none(failHint);
            }
        }
        testLaunching = false;
    }

    public override void ES_RMS(string hashCode, List<messageSystemValue> result) 
    {
        base.ES_RMS(hashCode, result);
        if (hashCode == "onRename")
        {
            string raw = GameModeManager.DeckInUse;
            GameModeManager.SetDeckInUse(result[0].value);
            if (onSave()) 
            {
                ((CardDescription)Program.I().cardDescription).setTitle(result[0].value);
            }
            else
            {
                GameModeManager.SetDeckInUse(raw);
            }
        }
    }

    public Action returnAction = null;

    public bool onSave()    
    {
        try
        {
            if (
           deck.IMain.Count <= 60
           &&
           deck.IExtra.Count <= 15
           &&
           deck.ISide.Count <= 15
           )
            {
                string deckInUse = GameModeManager.DeckInUse;
                QuickTestTrace.Log("save", "canSave=" + canSave
                    + " IMain=" + deck.IMain.Count + " Main=" + deck.Main.Count
                    + " DeckO_Main=" + deck.Deck_O.Main.Count
                    + " deckInUse=" + deckInUse);
                // 兜底：卡组界面还没把 .ydk 读进来时（deck 全空），
                // 绝不能用空卡组覆盖磁盘上已有的存档 —— 这是不可逆的数据丢失。
                if (deck.IMain.Count == 0 && deck.Deck_O.Main.Count == 0)
                {
                    string target = GameModeManager.DeckPath(deckInUse);
                    if (File.Exists(target) && new FileInfo(target).Length > 64)
                    {
                        QuickTestTrace.Log("save", "BLOCKED: 拒绝用空卡组覆盖 " + target);
                        RMSshow_none(InterString.Get("卡组还没有载入，已阻止用空卡组覆盖存档。"));
                        return false;
                    }
                }
                if (canSave)
                {
                    ArrangeObjectDeck();
                    FromObjectDeckToCodedDeck(true);
                    string value = "#created by ygopro2\r\n#main\r\n";
                    for (int i = 0; i < deck.Main.Count; i++)
                    {
                        value += deck.Main[i].ToString() + "\r\n";
                    }
                    value += "#extra\r\n";
                    for (int i = 0; i < deck.Extra.Count; i++)
                    {
                        value += deck.Extra[i].ToString() + "\r\n";
                    }
                    value += "!side\r\n";
                    for (int i = 0; i < deck.Side.Count; i++)
                    {
                        value += deck.Side[i].ToString() + "\r\n";
                    }
                    System.IO.File.WriteAllText(GameModeManager.DeckPath(deckInUse), value, System.Text.Encoding.UTF8);
                }
                else
                {
                    string value = "#created by ygopro2\r\n#main\r\n";
                    for (int i = 0; i < deck.Deck_O.Main.Count; i++)
                    {
                        value += deck.Deck_O.Main[i].ToString() + "\r\n";
                    }
                    value += "#extra\r\n";
                    for (int i = 0; i < deck.Deck_O.Extra.Count; i++)
                    {
                        value += deck.Deck_O.Extra[i].ToString() + "\r\n";
                    }
                    value += "!side\r\n";
                    for (int i = 0; i < deck.Deck_O.Side.Count; i++)
                    {
                        value += deck.Deck_O.Side[i].ToString() + "\r\n";
                    }
                    System.IO.File.WriteAllText(GameModeManager.DeckPath(deckInUse), value, System.Text.Encoding.UTF8);
                }
                deckDirty = false;
                RMSshow_none(InterString.Get("卡组[?]已经被保存。", deckInUse));
                return true;
            }
            else
            {
                RMSshow_none(InterString.Get("卡组内卡片张数超过限制。"));
                return false;
            }
        }
        catch (Exception)
        {
            RMSshow_none(InterString.Get("保存失败！"));
            return false;
        }
    }

    public void onChat()
    {
        Program.I().room.onSubmit(UIHelper.getByName<UIInput>(toolBar, "input_").value);
        UIHelper.getByName<UIInput>(toolBar, "input_").value = "";
    }

    void home()
    {
        // 工具条上的「返回」。曾经这里点下去毫无反应 —— returnAction 是 null
        //（测试局收尾回编辑器时把它清成了 null），而 home() 只有这一个出口。
        // 留一行轨迹，验收脚本据此判定「点到了、且被路由了」。
        QuickTestTrace.Log("home", "clicked returnAction=" + (returnAction != null));
        if (returnAction != null)
        {
            returnAction();
        }
    }

    void sort()
    {
        //animationCameraPan();
        ArrangeObjectDeck();
        SortObjectDeck();
        ShowObjectDeck();
    }

    void rand()
    {
        //animationCameraPan();
        ArrangeObjectDeck();
        RandObjectDeck();
        ShowObjectDeck();
    }

    /// <summary>
    /// 清空卡组。原实现是给每张卡 AddForce 把它们从桌上甩出去，靠「落点飞出桌面
    /// → getIfAlive()==false → 归入 IRemoved」来离场。幽灵卡没有刚体，改成纯逻辑：
    /// 直接 killIt()（置 dying、隐藏）并从三个卡组桶里摘掉，进 IRemoved，
    /// 这样 hide() 时仍会正常回收它们。
    /// </summary>
    void clear()
    {
        var deckTemp = deck.getAllObjectCard();
        foreach (var item in deckTemp)  
        {
            try
            {
                UIHelper.clearITWeen(item.gameObject);
                item.killIt();
                deck.IRemoved.Add(item);
            }
            catch (Exception e)
            {
                Debug.Log(e);
            }
        }
        deck.IMain.Clear();
        deck.IExtra.Clear();
        deck.ISide.Clear();
        deckDirty = true;
    }

    bool detailShowed = false;

    void showDetail()
    {
        detailShowed = true;
        refreshDetail();
    }

    void hideDetail()
    {
        clearAll();
        detailShowed = false;
        refreshDetail();
    }


    bool detailPanelShiftedTemp = false;
    void shiftDetailPanel(bool dragged) 
    {
        detailPanelShiftedTemp = dragged;
        if (isShowed&&detailShowed) 
        {
            if (dragged)
            {
                gameObjectDetailedSearch.GetComponent<UITexture>().color = new Color(1,1,1,0.7f);
            }
            else
            {
                gameObjectDetailedSearch.GetComponent<UITexture>().color = Color.white;
            }
        }
    }


    void refreshDetail()
    {
        if (gameObjectDetailedSearch!=null) 
        {
            if (isShowed)
            {
                if (Screen.height < 700)
                {
                    gameObjectDetailedSearch.transform.localScale = new Vector3(Screen.height / 700f, Screen.height / 700f, Screen.height / 700f);
                    if (detailShowed)
                    {
                        gameObjectDetailedSearch.GetComponent<UITexture>().height = 700;
                        iTween.MoveTo(gameObjectDetailedSearch, Program.camera_main_2d.ScreenToWorldPoint(new Vector3(Screen.width - 230 - 115f * Screen.height / 700f, Screen.height * 0.5f, 0)), 0.6f);
                        reShowBar(0, 230 + 230 * Screen.height / 700f);
                    }
                    else
                    {
                        gameObjectDetailedSearch.GetComponent<UITexture>().height = 700;
                        iTween.MoveTo(gameObjectDetailedSearch, Program.camera_main_2d.ScreenToWorldPoint(new Vector3(Screen.width - 230 - 115f * Screen.height / 700f, Screen.height * 1.5f, 0)), 0.6f);
                        reShowBar(0, 230);
                    }
                }
                else
                {
                    gameObjectDetailedSearch.transform.localScale = Vector3.one;
                    if (detailShowed)
                    {
                        gameObjectDetailedSearch.GetComponent<UITexture>().height = Screen.height;
                        iTween.MoveTo(gameObjectDetailedSearch, Program.camera_main_2d.ScreenToWorldPoint(new Vector3(Screen.width - 345f, Screen.height * 0.5f, 0)), 0.6f);
                        reShowBar(0, 460);
                    }
                    else
                    {
                        gameObjectDetailedSearch.GetComponent<UITexture>().height = Screen.height;
                        iTween.MoveTo(gameObjectDetailedSearch, Program.camera_main_2d.ScreenToWorldPoint(new Vector3(Screen.width - 345f, Screen.height * 1.5f, 0)), 0.6f);
                        reShowBar(0, 230);
                    }
                }

            }
            else
            {
                gameObjectDetailedSearch.transform.localScale = Vector3.zero;
            }
        }
    }

    void onClickDetail()
    {
        if (detailShowed)
        {
            hideDetail();
        }
        else
        {
            showDetail();
        }
    }

    public override void ES_mouseDownEmpty()
    {
        //if (detailShowed)
        //{
        //    hideDetail();
        //}
    }

    void onExitDetail()
    {
        if (detailShowed)
        {
            hideDetail();
        }
    }

    void clearAll()
    {
        try
        {
            seconds.Clear();
            for (int i = 0; i < 32; i++)
            {
                UIToggle_effects[i].value = false;
            }
            UIPopupList_pack.value = GameStringManager.get_unsafe(1310);
            UIPopupList_main.value = GameStringManager.get_unsafe(1310);
            UIPopupList_ban.value = GameStringManager.get_unsafe(1310);
            UIPopupList_second.Clear();
            UIPopupList_second.AddItem(GameStringManager.get_unsafe(1310));
            UIPopupList_second.value = GameStringManager.get_unsafe(1310);
            UIPopupList_race.Clear();
            UIPopupList_race.AddItem(GameStringManager.get_unsafe(1310));
            UIPopupList_race.value = GameStringManager.get_unsafe(1310);
            UIPopupList_attribute.Clear();
            UIPopupList_attribute.AddItem(GameStringManager.get_unsafe(1310));
            UIPopupList_attribute.value = GameStringManager.get_unsafe(1310);
            UIHelper.getByName<UIInput>(gameObjectDetailedSearch, "stars_").value = "";
            UIHelper.getByName<UIInput>(gameObjectDetailedSearch, "p_").value = "";
            UIHelper.getByName<UIInput>(gameObjectDetailedSearch, "atk_").value = "";
            UIHelper.getByName<UIInput>(gameObjectDetailedSearch, "def_").value = "";
            UIHelper.getByName<UIInput>(gameObjectDetailedSearch, "year_").value = "";

            UIHelper.getByName<UIInput>(gameObjectDetailedSearch, "stars_UP").value = "";
            UIHelper.getByName<UIInput>(gameObjectDetailedSearch, "p_UP").value = "";
            UIHelper.getByName<UIInput>(gameObjectDetailedSearch, "atk_UP").value = "";
            UIHelper.getByName<UIInput>(gameObjectDetailedSearch, "def_UP").value = "";
            UIHelper.getByName<UIInput>(gameObjectDetailedSearch, "year_UP").value = "";

        }
        catch (System.Exception e)
        {
            //UnityEngine.Debug.Log(e);
        }
    }

    List<string> seconds = new List<string>();
    void onUIPopupList_second()
    {
        Program.notGo(printSecond);
        Program.go(100, printSecond);
    }

    private void printSecond()
    {
        Program.go(50, tempStep2);
        if (UIPopupList_main.value == GameStringManager.get_unsafe(1312))
        {
            if (UIPopupList_second.value == GameStringManager.get_unsafe(1310))
            {
                seconds.Clear();
            }
            else
            {
                seconds.Remove(UIPopupList_second.value);
                seconds.Add(UIPopupList_second.value);
                string all = "";
                foreach (var item in seconds)
                {
                    all += item + " ";
                }
                if (all == "")
                {
                    all = GameStringManager.get_unsafe(1310);
                }
                UIPopupList_second.value = all;
            }
        }
        else
        {
            seconds.Clear();
            seconds.Add(UIPopupList_second.value);
        }
    }

    void tempStep2()
    {
        Program.notGo(printSecond);
    }

    void onUIPopupList_main()
    {
        UIPopupList_second.Clear();
        UIPopupList_second.AddItem(GameStringManager.get_unsafe(1310));
        UIPopupList_second.value = GameStringManager.get_unsafe(1310);
        UIPopupList_race.Clear();
        UIPopupList_race.AddItem(GameStringManager.get_unsafe(1310));
        UIPopupList_race.value = GameStringManager.get_unsafe(1310);
        UIPopupList_attribute.Clear();
        UIPopupList_attribute.AddItem(GameStringManager.get_unsafe(1310));
        UIPopupList_attribute.value = GameStringManager.get_unsafe(1310);
        // 「种类」下拉：候选项与反向映射（getTypeFilter2）读的是**同一张表**
        // （GameStringHelper.secondTypeBits），别再各写一份清单 —— 那种写法的下场
        // 就是「下拉里有、勾了搜不到」，种族表已经踩过一次（见 raceName 的注释）。
        //
        // 用户口径（2026-09-19）：「RD 的筛选要去掉它没有的种类（怪兽里的同调、
        // 魔法里的速攻），补上它有的（极大）」。表的收敛由卡池实际卡数决定，
        // 所以卡片包一更新、这个下拉自己就跟着变，不必再手改清单。
        int main = MainTypeIndex();
        if (main >= 0)
        {
            List<int> secondBits = GameStringHelper.secondTypeBits(main);
            string secondDump = "";
            for (int i = 0; i < secondBits.Count; i++)
            {
                UIPopupList_second.AddItem(GameStringHelper.secondTypeText(secondBits[i]));
                if (i > 0)
                {
                    secondDump += "|";
                }
                secondDump += GameStringHelper.secondTypeText(secondBits[i])
                    + ":" + secondBits[i];
            }
            // 落盘一份**下拉里真实装进去的项**（不是表本身）：验收脚本要判的是界面，
            // 表对了但没装进去等于没改。
            QuickTestTrace.Log("mode", "secondItems main=" + main
                + " mode=" + GameModeManager.ModeLabel
                + " n=" + secondBits.Count + " items=[" + secondDump + "]");
            // 种族 / 属性只跟着怪兽给（老口径，一个字不改）。
            if (main == GameStringHelper.SecondTypeMainMonster)
            {
                // 种族下拉的项数与文案都走 GameStringHelper：RD 下是 32 个（多出银河/天界战士/
                // 多头龙/欧米茄念动力/魔导骑士/电子人），OCG 下仍是原来的 26 个。
                // 反向映射见 getRaceFilter —— 两边**必须**读同一个函数，否则按文案比对会漏项。
                for (int i = 0; i < GameStringHelper.RaceCount; i++)
                {
                    string raceItem = GameStringHelper.raceName(i);
                    if (raceItem.Length > 0)
                    {
                        UIPopupList_race.AddItem(raceItem);
                    }
                }
                for (int i = 1010; i <= 1016; i++)
                {
                    UIPopupList_attribute.AddItem(GameStringManager.get_unsafe(i));
                }
            }
        }
    }

    /// <summary>
    /// 主类下拉（怪兽/魔法/陷阱）当前选中哪一档：0 / 1 / 2，没选中返回 -1。
    ///
    /// 下拉里还有一个「(All)」（conf 1310），那一档不参与二级种类 —— 老代码靠三个
    /// `if (value == …)` 各写一遍，这里收成一个出口，省得再复制第三遍。
    /// </summary>
    private int MainTypeIndex()
    {
        if (UIPopupList_main.value == GameStringManager.get_unsafe(1312))
        {
            return GameStringHelper.SecondTypeMainMonster;
        }
        if (UIPopupList_main.value == GameStringManager.get_unsafe(1313))
        {
            return GameStringHelper.SecondTypeMainSpell;
        }
        if (UIPopupList_main.value == GameStringManager.get_unsafe(1314))
        {
            return GameStringHelper.SecondTypeMainTrap;
        }
        return -1;
    }

    void onClickSearch()
    {
        doSearch();
    }

    int lastRefreshTime = 0;

    string UIInput_searchValueLast = "";

    void doSearch()
    {
        superScrollView.toTop();
        Program.go(50, process);
    }

    private void process()
    {
        List<YGOSharp.Card> result = YGOSharp.CardsManager.searchAdvanced
                    (
                    getName(),
                    getLevel(),
                    getAttack(),
                    getDefence(),
                    getP(),
                    getYear(),
                    getLevel_UP(),
                    getAttack_UP(),
                    getDefence_UP(),
                    getP_UP(),
                    getYear_UP(),
                    getOT(),
                    getPack(),
                    getBanFilter(),
                    currentBanlist,
                    getTypeFilter(),
                    getTypeFilter2(),
                    getRaceFilter(),
                    getAttributeFilter(),
                    getCatagoryFilter()
                    );
        print(result);
        UIHelper.trySetLableText(gameObjectSearch, "title_", result.Count.ToString());
        UIInput_search.isSelected = true;
        // 「编辑器是不是只在搜当前模式的池」是 RD 线的核心判据之一，光看界面数不出来 ——
        // 这里把池、关键字、命中数与头几张卡一起落盘（验收脚本按这条判，别去截图数卡）。
        if (QuickTestTrace.Enabled)
        {
            System.Text.StringBuilder head = new System.Text.StringBuilder();
            for (int i = 0; i < result.Count && i < 3; i++)
            {
                head.Append(' ').Append(result[i].Id).Append(':').Append(result[i].Name).Append(';');
            }
            QuickTestTrace.Log("mode", "search pool=" + GameModeManager.ModeLabel
                + " key=[" + getName() + "] n=" + result.Count
                + " banlist=" + (currentBanlist != null ? currentBanlist.Name : "null")
                + " head=" + (head.Length > 0 ? head.ToString() : "-"));
        }
    }

    public YGOSharp.Banlist currentBanlist = null;

    bool checkBanlistAvail(int cardid)
    {
        return deck.GetCardCount(cardid) < currentBanlist.GetQuantity(cardid);
    }

    bool isBanned(int cardid)
    {
        return currentBanlist.GetQuantity(cardid) == 0;
    }

    List<YGOSharp.Card> PrintedResult = new List<YGOSharp.Card>();

    void print(List<YGOSharp.Card> result)
    {
        if (superScrollView!=null)
        {
            PrintedResult = result;
            if (condition == Condition.editDeck)
            {
                currentBanlist = YGOSharp.BanlistManager.GetByName(UIPopupList_banlist.value);
            }
            if (condition == Condition.changeSide)
            {
                currentBanlist = YGOSharp.BanlistManager.GetByHash(Program.I().room.lflist);
            }
            List<string[]> args = new List<string[]>();
            foreach (var item in result)
            {
                string[] arg = new string[5];
                arg[0] = item.Id.ToString();
                arg[1] = "3";
                arg[2] = item.Name + "\n" + GameStringHelper.getSearchResult(item);
                args.Add(arg);
            }
            superScrollView.print(args);
            superScrollView.toTop();
        }
    }

    bool ifType(string str)
    {
        bool re = false;
        foreach (var item in seconds)   
        {
            if (str==item)
            {
                re = true;
                break;
            }
        }
        return re;
    }

    UInt32 getTypeFilter()
    {
        int main = MainTypeIndex();
        return main < 0 ? 0u : GameStringHelper.secondTypeMainMask(main);
    }

    /// <summary>
    /// 二级「种类」选中的档位 → 类型掩码。
    ///
    /// ⚠ 这里**必须**和填充下拉的那段读同一张表（<see cref="GameStringHelper.secondTypeBits"/>）：
    ///   老写法是这边手写 21 个 `ifType(文案)`、那边手写 3 份 `AddItem` 清单，
    ///   两处一旦不同步就是「下拉里有、勾了搜不到」，而且不会报任何错。
    /// </summary>
    UInt32 getTypeFilter2()
    {
        UInt32 returnValue = 0;
        int main = MainTypeIndex();
        if (main < 0)
        {
            return 0;
        }
        List<int> bits = GameStringHelper.secondTypeBits(main);
        for (int i = 0; i < bits.Count; i++)
        {
            if (ifType(GameStringHelper.secondTypeText(bits[i])))
            {
                returnValue |= GameStringHelper.secondTypeMask(main, bits[i]);
            }
        }
        return returnValue;
    }

    int getBanFilter()
    {
        int returnValue = -233;
        if (UIPopupList_ban.value == GameStringManager.get_unsafe(1316))
        {
            returnValue = 0;
        }
        if (UIPopupList_ban.value == GameStringManager.get_unsafe(1317))
        {
            returnValue = 1;
        }
        if (UIPopupList_ban.value == GameStringManager.get_unsafe(1318))
        {
            returnValue = 2;
        }
        return returnValue;
    }

    int getOT()
    {
        int returnValue = -233;
        if (UIPopupList_ban.value == GameStringManager.get_unsafe(1481))
        {
            returnValue = 1;
        }
        if (UIPopupList_ban.value == GameStringManager.get_unsafe(1482))
        {
            returnValue = 2;
        }
        if (UIPopupList_ban.value == GameStringManager.get_unsafe(1483))
        {
            returnValue = 8;
        }
        if (UIPopupList_ban.value == GameStringManager.get_unsafe(1484))
        {
            returnValue = 4;
        }
        if (UIPopupList_ban.value == GameStringManager.get_unsafe(1485))
        {
            returnValue = 3;
        }
        return returnValue;
    }

    UInt32 getRaceFilter()
    {
        UInt32 returnValue = 0;
        // 范围跟着 GameStringHelper.RaceCount 走（OCG 26 / RD 32），
        // 文案取值也必须走 raceName —— 与下拉填充是同一个函数，才不会有「填进去的点不中」。
        for (int i = 0; i < GameStringHelper.RaceCount; i++)
        {
            string raceItem = GameStringHelper.raceName(i);
            if (raceItem.Length > 0 && UIPopupList_race.value == raceItem)
            {
                // `1u << 31` 是合法的（0x80000000），别改成 1 << i 或 Math.Pow：
                // 前者在 int 下对 31 位是溢出，后者返回 double 还得转一次。
                returnValue |= 1u << i;
            }
        }
        return returnValue;
    }

    UInt32 getAttributeFilter()
    {
        UInt32 returnValue = 0;
        for (int i = 0; i < 7; i++)
        {
            if (UIPopupList_attribute.value == GameStringManager.get_unsafe(1010 + i))
            {
                returnValue |= (UInt32)Math.Pow(2, i);
            }
        }
        return returnValue;
    }

    UInt32 getCatagoryFilter()
    {
        UInt32 returnValue = 0;
        for (int i = 0; i < 32; i++)
        {
            if (UIToggle_effects[i].value == true)
            {
                returnValue |= (UInt32)Math.Pow(2, i);
            }
        }
        return returnValue;
    }

    int getAttack()
    {
        int returnValue = 0;
        try
        {
            returnValue = int.Parse(UIHelper.getByName<UIInput>(gameObjectDetailedSearch, "atk_").value);
        }
        catch (Exception)
        {
            returnValue = -2;
        }
        if (UIHelper.getByName<UIInput>(gameObjectDetailedSearch, "atk_").value == "")
        {
            returnValue = -233;
        }
        return returnValue;
    }

    int getDefence()
    {
        int returnValue = 0;
        try
        {
            returnValue = int.Parse(UIHelper.getByName<UIInput>(gameObjectDetailedSearch, "def_").value);
        }
        catch (Exception)
        {
            returnValue = -2;
        }
        if (UIHelper.getByName<UIInput>(gameObjectDetailedSearch, "def_").value == "")
        {
            returnValue = -233;
        }
        return returnValue;
    }

    int getLevel()
    {
        int returnValue = 0;
        try
        {
            returnValue = int.Parse(UIHelper.getByName<UIInput>(gameObjectDetailedSearch, "stars_").value);
        }
        catch (Exception)
        {
            returnValue = 0;
        }
        if (UIHelper.getByName<UIInput>(gameObjectDetailedSearch, "stars_").value == "")
        {
            returnValue = -233;
        }
        return returnValue;
    }

    int getP()
    {
        int returnValue = 0;
        try
        {
            returnValue = int.Parse(UIHelper.getByName<UIInput>(gameObjectDetailedSearch, "p_").value);
        }
        catch (Exception)
        {
            returnValue = 0;
        }
        if (UIHelper.getByName<UIInput>(gameObjectDetailedSearch, "p_").value == "")
        {
            returnValue = -233;
        }
        return returnValue;
    }

    int getYear()
    {
        int returnValue = 0;
        try
        {
            returnValue = int.Parse(UIHelper.getByName<UIInput>(gameObjectDetailedSearch, "year_").value);
        }
        catch (Exception)
        {
            returnValue = 0;
        }
        if (UIHelper.getByName<UIInput>(gameObjectDetailedSearch, "year_").value == "")
        {
            returnValue = -233;
        }
        return returnValue;
    }

    int getAttack_UP()
    {
        int returnValue = 0;
        try
        {
            returnValue = int.Parse(UIHelper.getByName<UIInput>(gameObjectDetailedSearch, "atk_UP").value);
        }
        catch (Exception)
        {
            returnValue = -2;
        }
        if (UIHelper.getByName<UIInput>(gameObjectDetailedSearch, "atk_UP").value == "")
        {
            returnValue = -233;
        }
        return returnValue;
    }

    int getDefence_UP()
    {
        int returnValue = 0;
        try
        {
            returnValue = int.Parse(UIHelper.getByName<UIInput>(gameObjectDetailedSearch, "def_UP").value);
        }
        catch (Exception)
        {
            returnValue = -2;
        }
        if (UIHelper.getByName<UIInput>(gameObjectDetailedSearch, "def_UP").value == "")
        {
            returnValue = -233;
        }
        return returnValue;
    }

    int getLevel_UP()
    {
        int returnValue = 0;
        try
        {
            returnValue = int.Parse(UIHelper.getByName<UIInput>(gameObjectDetailedSearch, "stars_UP").value);
        }
        catch (Exception)
        {
            returnValue = 0;
        }
        if (UIHelper.getByName<UIInput>(gameObjectDetailedSearch, "stars_UP").value == "")
        {
            returnValue = -233;
        }
        return returnValue;
    }

    int getP_UP()
    {
        int returnValue = 0;
        try
        {
            returnValue = int.Parse(UIHelper.getByName<UIInput>(gameObjectDetailedSearch, "p_UP").value);
        }
        catch (Exception)
        {
            returnValue = 0;
        }
        if (UIHelper.getByName<UIInput>(gameObjectDetailedSearch, "p_UP").value == "")
        {
            returnValue = -233;
        }
        return returnValue;
    }

    int getYear_UP()
    {
        int returnValue = 0;
        try
        {
            returnValue = int.Parse(UIHelper.getByName<UIInput>(gameObjectDetailedSearch, "year_UP").value);
        }
        catch (Exception)
        {
            returnValue = 0;
        }
        if (UIHelper.getByName<UIInput>(gameObjectDetailedSearch, "year_UP").value == "")
        {
            returnValue = -233;
        }
        return returnValue;
    }

    string getName()
    {
        return UIInput_search.value;
    }

    string getPack()
    {
        if (UIPopupList_pack.value == GameStringManager.get_unsafe(1310))
        {
            return "";
        }
        return UIPopupList_pack.value;
    }

    #endregion

    GameObject gameObjectDesk = null;

    number_loader main_unmber;

    number_loader side_number;

    number_loader extra_unmber;

    number_loader m_unmber;

    number_loader s_number;

    number_loader t_unmber;

    public override void show()
    {
        base.show();
        Program.camera_game_main.transform.position = new Vector3(0, 35, 0);
        Program.camera_game_main.transform.localEulerAngles = new Vector3(90, 0, 0);
        cameraAngle = 90;
        Program.cameraFacing = true;
        Program.cameraPosition = Program.camera_game_main.transform.position;
        camrem();
        Program.I().light.transform.eulerAngles = new Vector3(50, 0, 0);
        gameObjectDesk = create_s(Program.I().new_mod_tableInDeckManager);
        gameObjectDesk.layer = 16;
        gameObjectDesk.transform.position = new Vector3(0, 0, 0);
        gameObjectDesk.transform.eulerAngles = new Vector3(90, 0, 0);
        gameObjectDesk.transform.localScale = new Vector3(30, 30, 1);
        gameObjectDesk.GetComponent<Renderer>().material.mainTexture = Program.GetTextureViaPath("texture/duel/deckTable.png");
        //UIHelper.SetMaterialRenderingMode(gameObjectDesk.GetComponent<Renderer>().material, UIHelper.RenderingMode.Transparent);
        Rigidbody rigidbody = gameObjectDesk.AddComponent<Rigidbody>();
        rigidbody.useGravity = false;
        rigidbody.isKinematic = true;
        rigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        BoxCollider boxCollider = gameObjectDesk.AddComponent<BoxCollider>();
        main_unmber = create_s(Program.I().mod_ocgcore_number, new Vector3(-16.5f, 0, 13.6f), new Vector3(90, 0, 0), true).GetComponent<number_loader>();
        m_unmber = create_s(Program.I().mod_ocgcore_number, new Vector3(-16.5f, 0, 6.6f), new Vector3(90, 0, 0), true).GetComponent<number_loader>();
        s_number = create_s(Program.I().mod_ocgcore_number, new Vector3(-16.5f, 0, 4.6f), new Vector3(90, 0, 0), true).GetComponent<number_loader>();
        t_unmber = create_s(Program.I().mod_ocgcore_number, new Vector3(-16.5f, 0, 2.6f), new Vector3(90, 0, 0), true).GetComponent<number_loader>();
        extra_unmber = create_s(Program.I().mod_ocgcore_number, new Vector3(-16.5f, 0, -5.3f), new Vector3(90, 0, 0), true).GetComponent<number_loader>();
        side_number = create_s(Program.I().mod_ocgcore_number, new Vector3(-16.5f, 0, -11f), new Vector3(90, 0, 0), true).GetComponent<number_loader>();
        switch (condition)  
        {
            case Condition.editDeck:
                boxCollider.size = new Vector3(1, 1, 1);
                break;
            case Condition.changeSide:
                boxCollider.size = new Vector3(100, 100, 1);
                break;
            default:
                break;
        }
        clearAll();
    }

    public override void hide()
    {
        if (isShowed)
        {
            hideDetail();
        }
        for (int i = 0; i < deck.IMain.Count; i++)
        {
            destroyCard(deck.IMain[i]);
        }
        for (int i = 0; i < deck.IExtra.Count; i++)
        {
            destroyCard(deck.IExtra[i]);
        }
        for (int i = 0; i < deck.ISide.Count; i++)
        {
            destroyCard(deck.ISide[i]);
        }
        for (int i = 0; i < deck.IRemoved.Count; i++)
        {
            destroyCard(deck.IRemoved[i]);
        }
        deck = new YGOSharp.Deck();
        deckDirty = false;
        ((CardDescription)Program.I().cardDescription).setTitle("");
        base.hide();
    }

    float cameraDistance = Vector3.Distance(new Vector3(0, 23f, -17.5f), Vector3.zero);

    float cameraAngle = Mathf.Atan(23 / 17.5f);

    // 排查用：qt_endduel.on 的第一阶段闩锁（见 preFrameFunction / Ocgcore.preFrameFunction）
    bool qtTestStartTriggered = false;

    public override void preFrameFunction()
    {
        base.preFrameFunction();

        // 排查用：编辑器工具条按钮的屏幕坐标，每 2 秒报一次（见 dumpEditDeckButtonPositions）。
        // 必须在停稳之后读 —— show() 会带 0.6 秒入场动画，那时候读到的位置是错的。
        if (QuickTestTrace.Enabled && condition == Condition.editDeck)
        {
            if (Program.TimePassed() - lastBarDumpMs > 2000)
            {
                lastBarDumpMs = Program.TimePassed();
                dumpEditDeckButtonPositions();
            }
        }

        // 排查用（仅 log/qt_debug.on 生效）：完整复刻用户报的路径 ——
        // 打开卡组编辑器 -> 点「测试」-> 打完一局。这里替掉「点按钮」那一下，
        // 第二阶段在 Ocgcore.preFrameFunction 里触发一次「对局结束确认」。
        // 等 canSave 且主卡组非空再点：卡片是分批 tween 生成的，太早点等于点空卡组。
        if (QuickTestTrace.Enabled)
        {
            if (!QuickTestTrace.SwitchOn("qt_endduel.on"))
            {
                qtTestStartTriggered = false;
            }
            else if (!qtTestStartTriggered
                && condition == Condition.editDeck
                && canSave && deck != null && deck.IMain.Count > 0)
            {
                qtTestStartTriggered = true;
                QuickTestTrace.Log("end", "qt_endduel.on -> onClickTest() from editor, IMain="
                    + deck.IMain.Count);
                // 走动作体而不是 onClickTest()：这条是排查钩子，不必再等界面停稳
                // （能走到这里就说明 canSave 且卡组已读进来，界面早停稳了）。
                onClickTestCore();
                return;
            }
        }

        if (cardInDragging != null)
        {
            if (detailPanelShiftedTemp == false)
            {
                shiftDetailPanel(true);
            }
        }
        else
        {
            if (detailPanelShiftedTemp == true)
            {
                shiftDetailPanel(false);
            }
        }
        camrem();
        if (Input.mousePosition.x < Screen.width - 280)
        {
            if (Input.mousePosition.x > 250)
            {
                cameraAngle += Program.wheelValue * 1.2f;
                if (cameraAngle < 0f)
                {
                    cameraAngle = 0f;
                }
                if (cameraAngle > 90f)
                {
                    cameraAngle = 90f;
                }
            }
        }
        cameraDistance = 29 - 3.1415926f / 180f * (cameraAngle - 60f) * 13f;
        Program.cameraPosition = new Vector3(0, cameraDistance * Mathf.Sin(3.1415926f / 180f * cameraAngle), -cameraDistance * Mathf.Cos(3.1415926f / 180f * cameraAngle));
        if (Program.TimePassed() - lastRefreshTime > 80)
        {
            lastRefreshTime = Program.TimePassed();
            FromObjectDeckToCodedDeck();
            main_unmber.set_number(deck.Main.Count, 3);
            side_number.set_number(deck.Side.Count, 4);
            extra_unmber.set_number(deck.Extra.Count, 0);
            int m = 0, s = 0, t = 0;
            foreach (var item in deck.IMain)
            {
                if ((item.cardData.Type & (int)CardType.Monster) > 0) m++;
                if ((item.cardData.Type & (int)CardType.Spell) > 0) s++;
                if ((item.cardData.Type & (int)CardType.Trap) > 0) t++;
            }
            m_unmber.set_number(m, 1);
            s_number.set_number(s, 2);
            t_unmber.set_number(t, 5);
        }
        if (Program.InputEnterDown)
        {
            if (condition == Condition.editDeck)
            {
                onClickSearch();
            }
        }
        if (Input.GetKeyDown(KeyCode.Tab) || Input.GetMouseButtonDown(2))
        {
            onClickDetail();
        }
    }

    private void camrem()
    {
        float l = Program.I().cardDescription.width + ((float)Screen.width) * 0.03f;
        float r = Screen.width - 230f;
        if (detailShowed)
        {
            if (gameObjectDetailedSearch != null)
            {
                r -= 230 * gameObjectDetailedSearch.transform.localScale.x;
            }
        }
        Program.reMoveCam((l + r) / 2f);
    }

    public override void ES_HoverOverGameObject(GameObject gameObject)
    {
        MonoCardInDeckManager cardInDeck = gameObject.GetComponent<MonoCardInDeckManager>();
        if (cardInDeck != null)
        {
            ((CardDescription)(Program.I().cardDescription)).setData(cardInDeck.cardData, GameTextureManager.myBack);
        }
        cardPicLoader cardInSearchResult = gameObject.GetComponent<cardPicLoader>();
        if (cardInSearchResult != null)
        {
            ((CardDescription)(Program.I().cardDescription)).setData(cardInSearchResult.data, GameTextureManager.myBack);
        }
    }

    public MonoCardInDeckManager cardInDragging = null;
    int timeLastDown = 0;
    GameObject goLast = null;

    public override void ES_mouseDownGameObject(GameObject gameObject)
    {
        bool doubleClick = false;
        if (goLast == gameObject)
        {
            if (Program.TimePassed() - timeLastDown < 300)
            {
                doubleClick = true;
            }
        }
        goLast = gameObject;
        timeLastDown = Program.TimePassed();
        MonoCardInDeckManager cardInDeck = gameObject.GetComponent<MonoCardInDeckManager>();
        cardPicLoader cardInSearchResult = gameObject.GetComponent<cardPicLoader>();
        if (cardInDeck != null && !cardInDeck.dying)
        {
            if (doubleClick && condition == Condition.editDeck && checkBanlistAvail(cardInDeck.cardData.Id))
            {
                MonoCardInDeckManager card = createCard();
                card.transform.position = cardInDeck.transform.position;
                cardInDeck.cardData.cloneTo(card.cardData);
                card.gameObject.layer = 16;
                // 插在源卡后面。原实现是「放到源卡同一个位置 + 按位置排序」，
                // 而同坐标时比较器从不返回 0，落点其实是不确定的；这里改成确定性插入。
                int atSrc = deck.IMain.IndexOf(cardInDeck);
                deck.IMain.Insert(atSrc < 0 ? deck.IMain.Count : atSrc + 1, card);
                deckDirty = true;
                ArrangeObjectDeck(true);
                ShowObjectDeck();
            }
            else
            {
                cardInDragging = cardInDeck;
                cardInDeck.beginDrag();
            }
        }
        else if (cardInSearchResult != null)
        {
            if (condition == Condition.editDeck)
            {
                if (checkBanlistAvail(cardInSearchResult.data.Id))
                {
                    if ((cardInSearchResult.data.Type & (UInt32)CardType.Token) == 0)
                    {
                        MonoCardInDeckManager card = createCard();
                        card.transform.position = card.getGoodPosition(4);
                        card.cardData = cardInSearchResult.data;
                        card.gameObject.layer = 16;
                        deck.IMain.Add(card);
                        cardInDragging = card;
                        card.beginDrag();
                    }
                }
            }
        }
    }

    /// <summary>
    /// 松手 = 网格吸附落位（MDPro3「幽灵卡 + 吸附」的做法）。
    ///
    /// 落点只决定「插到第几位」（NearestMainIndex / NearestSingleRowIndex），
    /// 顺序本身由列表决定 —— 不需要物理，也不会因为落点被求解器顶偏而串格。
    /// 丢到桌面之外、或按住 Ctrl 松手 = 直接逻辑移除（原实现是用 AddForce 把卡甩出去）。
    /// </summary>
    public override void ES_mouseUp()
    {
        if (cardInDragging == null)
        {
            return;
        }
        MonoCardInDeckManager card = cardInDragging;
        cardInDragging = null;

        bool ctrl = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
        bool discard = condition != Condition.changeSide && (ctrl || card.getIfAlive() == false);

        if (discard)
        {
            QuickTestTrace.Log("drag", "discard card " + card.cardData.Id
                + " ctrl=" + ctrl + " alive=" + card.getIfAlive());
            card.killIt();
            card.endDrag();
            ArrangeObjectDeck();
            ShowObjectDeck();
            return;
        }

        if (card.getIfAlive())
        {
            deckDirty = true;
        }
        card.endDrag();
        Vector3 dropAt = card.transform.position;
        SnapDraggedCardIntoDeck(card);
        ShowObjectDeck();
        QuickTestTrace.Log("drag", "drop card " + card.cardData.Id
            + " at x=" + dropAt.x.ToString("F2") + " z=" + dropAt.z.ToString("F2")
            + " -> main=" + deck.IMain.Count + " extra=" + deck.IExtra.Count
            + " side=" + deck.ISide.Count);
        // 吸附后复采一次：确认整盘重排后每张卡仍精确落在自己的槽位上（无空位/重叠）
        safeGogo(900, () => { SampleSlotLanding("drop"); });
    }

    public override void ES_mouseUpRight()
    {
        if (Program.pointedGameObject != null)
        {
            if (condition == Condition.editDeck)
            {
                MonoCardInDeckManager cardInDeck = Program.pointedGameObject.GetComponent<MonoCardInDeckManager>();
                if (cardInDeck != null)
                {
                    cardInDeck.killIt();
                    ArrangeObjectDeck(true);
                    ShowObjectDeck();
                }
                cardPicLoader cardInSearchResult = Program.pointedGameObject.GetComponent<cardPicLoader>();
                if (cardInSearchResult != null)
                {
                    CreateMonoCard(cardInSearchResult.data);
                    ShowObjectDeck();
                }
            }
            else
            {
                MonoCardInDeckManager cardInDeck = Program.pointedGameObject.GetComponent<MonoCardInDeckManager>();
                if (cardInDeck != null)
                {
                    bool isSide = false;
                    for (int i = 0; i < deck.ISide.Count; i++)  
                    {
                        if (cardInDeck== deck.ISide[i])
                        {
                            isSide = true;
                        }
                    }
                    if (isSide)
                    {
                        if (cardInDeck.cardData.IsExtraCard())
                        {
                            deck.IExtra.Add(cardInDeck);
                            deck.ISide.Remove(cardInDeck);
                        }
                        else
                        {
                            deck.IMain.Add(cardInDeck);
                            deck.ISide.Remove(cardInDeck);
                        }
                    }
                    else
                    {
                        deck.ISide.Add(cardInDeck);
                        deck.IMain.Remove(cardInDeck);
                        deck.IExtra.Remove(cardInDeck);
                    }
                    ShowObjectDeck();
                }
            }
        }
    }

    private void CreateMonoCard(YGOSharp.Card data)
    {
        if (checkBanlistAvail(data.Id))
        {
            MonoCardInDeckManager card = createCard();
            card.transform.position = card.getGoodPosition(4);
            card.cardData = data;
            card.gameObject.layer = 16;
            if (data.IsExtraCard())
            {
                deck.IExtra.Add(card);
                deck.Extra.Add(card.cardData.Id);
            }
            else
            {
                deck.IMain.Add(card);
                deck.Main.Add(card.cardData.Id);
            }
            deckDirty = true;
        }
    }

    public YGOSharp.Deck deck = new YGOSharp.Deck();
    public bool deckDirty = false;

    public void loadDeckFromYDK(string path)
    {
        FromYDKtoCodedDeck(path, out deck);
        FormCodedDeckToObjectDeck();
        deckDirty = false;
        VerifySlotLanding();
    }

    /// <summary>
    /// 落格自检（纯日志，不截屏）。
    ///
    /// 两条判据都直接从 .ydk 文件取基准，不依赖内存里的任何中间状态：
    ///   1. **顺序**：FromObjectDeckToCodedDeck() 之后的 deck.Main 必须与 .ydk 的
    ///      #main 段逐位一致（orderDiffVsYdk==0）；
    ///   2. **位置**：每张卡的实际 (x,z) 必须落在它自己的槽位 slotTarget 上。
    /// 采样两次（+3s / +8s）——位置现在只由 iTween 驱动，两次必须完全一致（无漂移）。
    /// 只写 qt_&lt;pid&gt;.log，需要 output/Windows/qt_debug.on 才输出。
    /// </summary>
    void VerifySlotLanding()
    {
        safeGogo(3000, () => { SampleSlotLanding("t3"); });
        safeGogo(8000, () => { SampleSlotLanding("t8"); });
    }

    void SampleSlotLanding(string when)
    {
        try
        {
            System.Collections.Generic.IList<int> expectMain = new List<int>();
            int expectExtra = 0, expectSide = 0;
            string dn = GameModeManager.DeckInUse;
            string path = GameModeManager.DeckPath(dn);
            if (File.Exists(path))
            {
                YGOSharp.Deck refDeck;
                FromYDKtoCodedDeck(path, out refDeck);
                expectMain = refDeck.Main;
                expectExtra = refDeck.Extra.Count;
                expectSide = refDeck.Side.Count;
            }
            FromObjectDeckToCodedDeck();
            int diff = 0;
            int n = Mathf.Min(expectMain.Count, deck.Main.Count);
            for (int i = 0; i < n; i++)
            {
                if (expectMain[i] != deck.Main[i])
                {
                    diff++;
                }
            }
            QuickTestTrace.Log("slot", when
                + " deck=" + dn
                + " main=" + deck.Main.Count + "/" + expectMain.Count
                + " extra=" + deck.Extra.Count + "/" + expectExtra
                + " side=" + deck.Side.Count + "/" + expectSide
                + " orderDiffVsYdk=" + diff);
            DumpSlotGeometry(when, "M", deck.IMain);
            DumpSlotGeometry(when, "E", deck.IExtra);
            DumpSlotGeometry(when, "S", deck.ISide);
            QuickTestTrace.Log("order", when + " main=" + JoinIds(deck.Main));
            QuickTestTrace.Log("order", when + " extra=" + JoinIds(deck.Extra));
            QuickTestTrace.Log("order", when + " side=" + JoinIds(deck.Side));
        }
        catch (Exception e)
        {
            QuickTestTrace.Log("slot", when + " err " + e.Message);
        }
    }

    /// <summary>把一串卡号拼成一行（用于从日志逐位核对卡组顺序）。</summary>
    static string JoinIds(System.Collections.Generic.IList<int> ids)
    {
        System.Text.StringBuilder sb = new System.Text.StringBuilder(ids.Count * 10);
        for (int i = 0; i < ids.Count; i++)
        {
            if (i > 0)
            {
                sb.Append(',');
            }
            sb.Append(ids[i]);
        }
        return sb.ToString();
    }

    /// <summary>
    /// 逐张明细：kind,i,id,实际(x,y,z),槽位(x,y,z)；
    /// [slotgeo] 行给出该桶「实际位置 vs 槽位」的最大偏差。
    /// </summary>
    void DumpSlotGeometry(string when, string kind, System.Collections.Generic.IList<MonoCardInDeckManager> list)
    {
        float maxD = 0f;
        float minY = 999f, maxY = -999f;
        for (int i = 0; i < list.Count; i++)
        {
            MonoCardInDeckManager c = list[i];
            if (c == null)
            {
                continue;
            }
            Vector3 p = c.transform.position;
            maxD = Mathf.Max(maxD, Mathf.Max(
                Mathf.Abs(p.x - c.slotTarget.x), Mathf.Abs(p.z - c.slotTarget.z)));
            if (p.y < minY) minY = p.y;
            if (p.y > maxY) maxY = p.y;
            QuickTestTrace.Log("card", kind + "," + i + "," + c.cardData.Id
                + "," + p.x.ToString("F3") + "," + p.y.ToString("F3") + "," + p.z.ToString("F3")
                + "," + c.slotTarget.x.ToString("F3") + "," + c.slotTarget.y.ToString("F3")
                + "," + c.slotTarget.z.ToString("F3"));
        }
        QuickTestTrace.Log("slotgeo", when + " " + kind + " count=" + list.Count
            + " maxDxz=" + maxD.ToString("F4")
            + " yMin=" + (list.Count > 0 ? minY.ToString("F2") : "-")
            + " yMax=" + (list.Count > 0 ? maxY.ToString("F2") : "-"));
    }

    public static void FromYDKtoCodedDeck(string path, out YGOSharp.Deck deck)
    {
        deck = new YGOSharp.Deck();
        try
        {
            string text = System.IO.File.ReadAllText(path);
            string st = text.Replace("\r", "");
            string[] lines = st.Split(new string[] { "\n" }, StringSplitOptions.RemoveEmptyEntries);
            int flag = -1;
            foreach (string line in lines)
            {
                if (line == "#main")
                {
                    flag = 1;
                }
                else if (line == "#extra")
                {
                    flag = 2;
                }
                else if (line == "!side")
                {
                    flag = 3;
                }
                else
                {
                    int code = 0;
                    try
                    {
                        code = Int32.Parse(line);
                    }
                    catch (Exception)
                    {

                    }
                    if (code > 100)
                    {
                        YGOSharp.Card card = YGOSharp.CardsManager.Get(code);
                        if (card.Id > 0 && flag != 3)
                        {
                            if (card.IsExtraCard())
                            {
                                deck.Extra.Add(code);
                                deck.Deck_O.Extra.Add(code);
                            }
                            else
                            {
                                deck.Main.Add(code);
                                deck.Deck_O.Main.Add(code);
                            }
                        }
                        else
                        switch (flag)
                        {
                            case 1:
                                {
                                    deck.Main.Add(code);
                                    deck.Deck_O.Main.Add(code);
                                }
                                break;
                            case 2:
                                {
                                    deck.Extra.Add(code);
                                    deck.Deck_O.Extra.Add(code);
                                }
                                break;
                            case 3:
                                {
                                    deck.Side.Add(code);
                                    deck.Deck_O.Side.Add(code);
                                }
                                break;
                            default:
                                break;
                        }
                    }
                }
            }
        }
        catch (Exception e)
        {
        }
    }

    public YGOSharp.Deck getRealDeck()
    {
        if (canSave)
        {
            return deck;
        }
        else
        {
            YGOSharp.Deck r = new YGOSharp.Deck();
            foreach (var item in deck.Deck_O.Main)  
            {
                r.Main.Add(item);
                r.Deck_O.Main.Add(item);
            }
            foreach (var item in deck.Deck_O.Side)
            {
                r.Side.Add(item);
                r.Deck_O.Side.Add(item);
            }
            foreach (var item in deck.Deck_O.Extra)
            {
                r.Extra.Add(item);
                r.Deck_O.Extra.Add(item);
            }
            return r;   
        }
    }

    /// <summary>
    /// 某一排里第 index 张卡的槽位姿态（位置 + 欧拉角）—— **单一真源**。
    ///
    /// 「首次落位」（FormCodedDeckToObjectDeck）与「重排」（ShowObjectDeck、
    /// 拖拽吸附后）共用这一份公式，避免两套口径漂出偏差。
    ///
    /// 排头（index==0）正对镜头 (90,0,0)；其余立起来 (87,-90,-90) 叠在它身后，
    /// 就形成了 ygopro 卡组编辑器那种「一张正面 + 一串侧棱」的扇形观感 ——
    /// 均匀、可复现，且不依赖任何物理沉降。
    /// </summary>
    /// <param name="index">这一排里的序号（0 起）</param>
    /// <param name="rowCount">这一排共几张</param>
    /// <param name="z">这一排的桌面深度（主卡组 11.8/7.8/3.8/-0.2，额外 -6.2，副卡组 -12）</param>
    public static void RowSlotPose(int index, int rowCount, float z, out Vector3 pos, out Vector3 angle)
    {
        // 立起来的卡要按倾角微抬一点，免得半张插进桌面（原 ShowObjectDeck 里的 k）
        float k = (float)(1.5 * 0.1 / 0.130733633);
        angle = new Vector3(90, 0, 0);
        if (index > 0)
        {
            angle = new Vector3(87, -90, -90);
            if (rowCount > 10)
            {
                angle = new Vector3(87f - (rowCount - 10f) * 0.4f, -90, -90);
            }
        }
        pos = new Vector3(
            UIHelper.get_left_right_indexZuo(-12.5f, 12.5f, index, rowCount, 10),
            0.6f + Mathf.Sin((90 - angle.x) / 180f * Mathf.PI) * k,
            z);
    }

    /// <summary>
    /// 按**当前位置**把对象重新归桶（主卡组 / 额外 / 副卡组 / 已移除）。
    ///
    /// 关键：这里**不再按位置排序**。原实现的 order=true 会用世界坐标的 x/z 反推顺序，
    /// 而落点会随物理求解器漂移（参考成品 PhysX 3.4 / 本工程 PhysX 4.x 结果不同），
    /// 于是「随便点一下卡」就可能把整副卡组的顺序打乱 —— 用户看到的「卡落到不该在的
    /// 格子」就是这么来的。现在顺序只由 deck.IMain/IExtra/ISide 三个列表决定；
    /// 拖拽时用网格吸附把卡插到落点对应的槽位（见 SnapDraggedCardIntoDeck），
    /// 落点只影响「插到第几位」，不再影响「谁是第几位」。
    ///
    /// order 参数仅为兼容既有调用保留，不再有排序语义。
    /// </summary>
    void ArrangeObjectDeck(bool order = false)
    {
        var deckTemp = deck.getAllObjectCardAndDeload();
        for (int i = 0; i < deckTemp.Count; i++)
        {
            Vector3 p = deckTemp[i].gameObject.transform.position;
            if (deckTemp[i].getIfAlive() == true)
            {
                if (p.z > -8)
                {
                    if (deckTemp[i].cardData.IsExtraCard())
                    {
                        deck.IExtra.Add(deckTemp[i]);
                    }
                    else
                    {
                        deck.IMain.Add(deckTemp[i]);
                    }
                }
                else
                {
                    deck.ISide.Add(deckTemp[i]);
                }
            }
            else
            {
                deck.IRemoved.Add(deckTemp[i]);
            }
        }
    }

    /// <summary>
    /// 网格吸附：把拖拽落点换算成槽位序号，然后把这张卡插到那个位置。
    ///
    /// 这是 MDPro3「幽灵卡 + 吸附」的核心一步 —— 落点只用来决定**插入到第几位**，
    /// 之后位置完全由 RowSlotPose 给出。所以既不需要物理，也不存在
    /// 「落点被求解器顶偏 → 串格 / 顺序乱」。
    /// </summary>
    void SnapDraggedCardIntoDeck(MonoCardInDeckManager card)
    {
        if (card == null)
        {
            return;
        }
        // 1) 先按落点重新归桶（主 / 额外 / 副 / 已移除），其它卡保持各自相对顺序
        ArrangeObjectDeck();

        // 2) 把这张卡从所有桶里摘出来（它此刻已在某个桶里），准备按落点重新插入
        deck.IMain.Remove(card);
        deck.IExtra.Remove(card);
        deck.ISide.Remove(card);
        deck.IRemoved.Remove(card);

        if (card.getIfAlive() == false)
        {
            // 丢到桌面外了：进「已移除」，不再重新摆位
            deck.IRemoved.Add(card);
            return;
        }

        Vector3 p = card.transform.position;
        if (p.z > -8f)
        {
            if (card.cardData.IsExtraCard())
            {
                int idx = NearestSingleRowIndex(p, deck.IExtra.Count + 1);
                deck.IExtra.Insert(idx, card);
                QuickTestTrace.Log("snap", "extra#" + idx + " total=" + deck.IExtra.Count);
            }
            else
            {
                int idx = NearestMainIndex(p, deck.IMain.Count + 1);
                deck.IMain.Insert(idx, card);
                QuickTestTrace.Log("snap", "main#" + idx + " total=" + deck.IMain.Count);
            }
        }
        else
        {
            int idx = NearestSingleRowIndex(p, deck.ISide.Count + 1);
            deck.ISide.Insert(idx, card);
            QuickTestTrace.Log("snap", "side#" + idx + " total=" + deck.ISide.Count);
        }
    }

    /// <summary>
    /// 主卡组：给定落点与「插入后的总张数」，算出该插到第几位（0 .. total-1）。
    /// 用与摆位公式完全相同的槽位坐标做「就近吸附」。
    /// </summary>
    static int NearestMainIndex(Vector3 p, int total)
    {
        if (total <= 0)
        {
            return 0;
        }
        int[] hangshu = UIHelper.get_decklieshuArray(total);
        int best = 0;
        float bestD = float.MaxValue;
        int baseIndex = 0;
        for (int row = 0; row < 4; row++)
        {
            float rz = 11.8f - row * 4f;
            for (int col = 0; col < hangshu[row]; col++)
            {
                int idx = baseIndex + col;
                if (idx >= total)
                {
                    break;
                }
                float sx = UIHelper.get_left_right_index(-12.5f, 12.5f, col, hangshu[row]);
                float d = Mathf.Abs(sx - p.x) + Mathf.Abs(rz - p.z);
                if (d < bestD)
                {
                    bestD = d;
                    best = idx;
                }
            }
            baseIndex += hangshu[row];
        }
        return best;
    }

    /// <summary>额外 / 副卡组：单排，给定落点与总张数算插入位。</summary>
    static int NearestSingleRowIndex(Vector3 p, int total)
    {
        if (total <= 0)
        {
            return 0;
        }
        int best = 0;
        float bestD = float.MaxValue;
        for (int i = 0; i < total; i++)
        {
            float sx = UIHelper.get_left_right_indexZuo(-12.5f, 12.5f, i, total, 10);
            float d = Mathf.Abs(sx - p.x);
            if (d < bestD)
            {
                bestD = d;
                best = i;
            }
        }
        return best;
    }

    void SortObjectDeck()
    {
        YGOSharp.Deck.sort((List<MonoCardInDeckManager>)deck.IMain);
        YGOSharp.Deck.sort((List<MonoCardInDeckManager>)deck.IExtra);
        YGOSharp.Deck.sort((List<MonoCardInDeckManager>)deck.ISide);
        deckDirty = true;
    }

    void RandObjectDeck()
    {
        YGOSharp.Deck.rand((List<MonoCardInDeckManager>)deck.IMain);
        deckDirty = true;
    }



    List<GameObject> diedCards = new List<GameObject>();

    MonoCardInDeckManager createCard()
    {
        MonoCardInDeckManager r = null;
        if (diedCards.Count>0)
        {
            r = diedCards[0].AddComponent<MonoCardInDeckManager>();
            diedCards.RemoveAt(0);
        }
        if (r == null)
        {
            r = Program.I().create(Program.I().new_mod_cardInDeckManager).AddComponent<MonoCardInDeckManager>();
            r.gameObject.transform.Find("back").gameObject.GetComponent<Renderer>().material.mainTexture = GameTextureManager.myBack;
            r.gameObject.transform.Find("face").gameObject.GetComponent<Renderer>().material.mainTexture = GameTextureManager.myBack;
        }
        r.gameObject.transform.position = new Vector3(0, 5, 0);
        r.gameObject.transform.eulerAngles = new Vector3(90, 0, 0);
        r.gameObject.transform.localScale = new Vector3(0, 0, 0);
        iTween.ScaleTo(r.gameObject, new Vector3(3, 4, 1), 0.4f);
        r.gameObject.SetActive(true);
        return r;
    }

    void destroyCard(MonoCardInDeckManager c)
    {
        try
        {
            c.gameObject.SetActive(false);
            diedCards.Add(c.gameObject);
            MonoBehaviour.DestroyImmediate(c);
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.Log(e);
        }
    }

    bool canSave = false;

    public void FormCodedDeckToObjectDeck()
    {
        canSave = false;
        safeGogo(4000, () =>
        {
            canSave = true;
        });
        int indexOfLogic = 0;
        int[] hangshu = UIHelper.get_decklieshuArray(deck.Main.Count);
        foreach (var item in deck.Main) 
        {
            Vector2 v = UIHelper.get_hang_lieArry(indexOfLogic, hangshu);
            Vector3 toVector, toAngle;
            RowSlotPose((int)v.y, hangshu[(int)v.x], 11.8f - v.x * 4f, out toVector, out toAngle);
            YGOSharp.Card data = YGOSharp.CardsManager.Get(item);
            safeGogo(indexOfLogic * 25, () =>
            {
                MonoCardInDeckManager card = createCard();
                card.cardData = data;
                card.gameObject.layer = 16;
                // 幽灵卡没有重力：先落在槽位正上方一点点，再由纯变换 tween 下来
                card.transform.position = new Vector3(toVector.x, toVector.y + 1.6f, toVector.z);
                deck.IMain.Add(card);
                card.tweenToVectorAndFall(toVector, toAngle);
            });
            indexOfLogic++;
        }
        indexOfLogic = 0;
        foreach (var item in deck.Extra)
        {
            Vector3 toVector, toAngle;
            RowSlotPose(indexOfLogic, deck.Extra.Count, -6.2f, out toVector, out toAngle);
            YGOSharp.Card data = YGOSharp.CardsManager.Get(item);
            safeGogo(indexOfLogic * 90, () =>
            {
                MonoCardInDeckManager card = createCard();
                card.cardData = data;
                card.gameObject.layer = 16;
                card.transform.position = new Vector3(toVector.x, toVector.y + 1.6f, toVector.z);
                deck.IExtra.Add(card);
                card.tweenToVectorAndFall(toVector, toAngle);
            });
            indexOfLogic++;
        }
        indexOfLogic = 0;
        foreach (var item in deck.Side)
        {
            Vector3 toVector, toAngle;
            RowSlotPose(indexOfLogic, deck.Side.Count, -12f, out toVector, out toAngle);
            YGOSharp.Card data = YGOSharp.CardsManager.Get(item);
            safeGogo(indexOfLogic * 90, () =>
            {
                MonoCardInDeckManager card = createCard();
                card.cardData = data;
                card.gameObject.layer = 16;
                card.transform.position = new Vector3(toVector.x, toVector.y + 1.6f, toVector.z);
                deck.ISide.Add(card);
                card.tweenToVectorAndFall(toVector, toAngle);
            });
            indexOfLogic++;
        }
        // 收尾归一化：卡片是分批生成的（主卡组每张间隔 25ms，40 张要约 1s），
        // 而 preFrameFunction 每 80ms 就会调一次 FromObjectDeckToCodedDeck()
        // → ArrangeObjectDeck()，后者按**当前位置**归桶。刚生成、还没 tween 到位的卡
        // 位置还在 (0,5,0)（z=0 → 判成主卡组），列表顺序在一次刷新里可能被挪动。
        // 等全部落位后重新归桶 + 重排一次，把顺序归一化成 .ydk 的原始顺序 ——
        // 否则用户「打开编辑器后不做任何点击、直接存档」会把乱序写回磁盘。
        safeGogo(2600, () =>
        {
            ArrangeObjectDeck();
            ShowObjectDeck();
            // 全部卡都已落位 —— 此刻起存档写的才是「当前编辑状态」。
            // 原来 canSave 只由一个 safeGogo(4000) 置位：若用户在 4 秒内按存档，
            // canSave 还是 false，onSave() 会走另一个分支把 deck.Deck_O（= 磁盘原文件
            // 的内容）写回去，等于把这次编辑丢掉。落位完成即置位更准也更稳。
            canSave = true;
            QuickTestTrace.Log("slot", "normalized after load");
        });
    }

    void ShowObjectDeck()
    {
        int[] hangshu = UIHelper.get_decklieshuArray(deck.IMain.Count);
        for (int i = 0; i < deck.IMain.Count; i++)
        {
            Vector2 v = UIHelper.get_hang_lieArry(i, hangshu);
            Vector3 toVector, toAngle;
            RowSlotPose((int)v.y, hangshu[(int)v.x], 11.8f - v.x * 4f, out toVector, out toAngle);
            deck.IMain[i].tweenToVectorAndFall(toVector, toAngle);
        }
        for (int i = 0; i < deck.IExtra.Count; i++)
        {
            Vector3 toVector, toAngle;
            RowSlotPose(i, deck.IExtra.Count, -6.2f, out toVector, out toAngle);
            deck.IExtra[i].tweenToVectorAndFall(toVector, toAngle);
        }
        for (int i = 0; i < deck.ISide.Count; i++)
        {
            Vector3 toVector, toAngle;
            RowSlotPose(i, deck.ISide.Count, -12f, out toVector, out toAngle);
            deck.ISide[i].tweenToVectorAndFall(toVector, toAngle);
        }
    }

    public void FromObjectDeckToCodedDeck(bool order=false)
    {
        ArrangeObjectDeck(order);
        deck.Main.Clear();
        deck.Extra.Clear();
        deck.Side.Clear();
        foreach (var item in deck.IMain)
        {
            deck.Main.Add(item.cardData.Id);
        }
        foreach (var item in deck.IExtra)
        {
            deck.Extra.Add(item.cardData.Id);
        }
        foreach (var item in deck.ISide)
        {
            deck.Side.Add(item.cardData.Id);
        }
    }

    public void setGoodLooking(bool side=false) 
    {
        try
        {
            ((CardDescription)(Program.I().cardDescription)).setData(YGOSharp.CardsManager.Get(deck.Main[0]), GameTextureManager.myBack);
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.Log(e);
        }
        if (side)   
        {
            List<YGOSharp.Card> result = new List<YGOSharp.Card>();
            foreach (var item in Program.I().ocgcore.sideReference) 
            {
                result.Add(YGOSharp.CardsManager.Get(item.Value));
            }
            print(result);
            UIHelper.trySetLableText(gameObjectSearch, "title_", result.Count.ToString());
        }
        else
        {
            UIHelper.trySetLableText(gameObjectSearch, "title_", InterString.Get("在此搜索卡片，拖动加入卡组"));
        }
        Program.go(50, superScrollView.toTop);
        Program.go(100, superScrollView.toTop);
        Program.go(200, superScrollView.toTop);
        Program.go(300, superScrollView.toTop);
        Program.go(400, superScrollView.toTop);
        Program.go(500, superScrollView.toTop);
        if (side)   
        {
            UIInput_search.value = InterString.Get("对手使用过的卡↓");
            UIInput_search.isSelected = false;
        }
        else
        {
            UIInput_search.value = "";
            UIInput_search.isSelected = true;
        }
    }
}
