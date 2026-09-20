using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;
using YGOSharp.OCGWrapper.Enums;
public class Ocgcore : ServantWithCardDescription
{
    public enum Condition
    {
        N=0,
        duel = 1,
        watch = 2,
        record = 3,
    }

    public Condition condition = Condition.duel;

    public gameInfo gameInfo;

    public GameObject waitObject;

    public List<gameCard> cards = new List<gameCard>();

    bool flagForTimeConfirm = false;

    bool flagForCancleChain = false;

    public float getScreenCenter()
    {
        return ((float)Screen.width + Program.I().cardDescription.width - gameInfo.width) / 2f;
    }

    public int MasterRule = 0;

    class linkMask
    {
        public GPS p;
        public GameObject gameObject;
        public bool eff = false;
    }

    List<linkMask> linkMaskList = new List<linkMask>();

    linkMask makeLinkMask(GPS p)
    {
        linkMask ma = new linkMask();
        ma.p = p;
        ma.eff = !Program.I().setting.setting.Vlink.value;
        shift_effect(ma, Program.I().setting.setting.Vlink.value);
        return ma;
    }

    void shift_effect(linkMask target, bool value)
    {
        if (target.eff != value)
        {
            if (target.gameObject != null)
            {
                destroy(target.gameObject);
            }
            if (value)
            {
                target.gameObject = create_s(Program.I().mod_ocgcore_ss_link_mark, get_point_worldposition(target.p) + new Vector3(0, -0.1f, 0), Vector3.zero, true, null, true);
            }
            else
            {
                target.gameObject = create_s(Program.I().mod_simple_quad, get_point_worldposition(target.p) + new Vector3(0, -0.1f, 0), new Vector3(90, 0, 0), false, null, true);
                target.gameObject.transform.localScale = new Vector3(4, 4, 4);
                target.gameObject.GetComponent<Renderer>().material.mainTexture = GameTextureManager.LINKm;
                target.gameObject.GetComponent<Renderer>().material.color = new Color(1, 1, 1, 0.8f);
            }
            target.eff = value;
        }
    }

    gameCardCondition get_point_worldcondition(GPS p)
    {
        gameCardCondition return_value = gameCardCondition.floating_clickable;
        if ((p.location & (UInt32)CardLocation.Deck) > 0)
        {
            return_value = gameCardCondition.still_unclickable;
        }
        if ((p.location & (UInt32)CardLocation.Extra) > 0)
        {
            return_value = gameCardCondition.still_unclickable;
        }
        if ((p.location & (UInt32)CardLocation.MonsterZone) > 0)
        {
            return_value = gameCardCondition.floating_clickable;
            if ((p.position & (UInt32)CardPosition.FaceUp) > 0)
            {
                return_value = gameCardCondition.verticle_clickable;
            }
        }
        if ((p.location & (UInt32)CardLocation.SpellZone) > 0)
        {
            return_value = gameCardCondition.floating_clickable;
        }
        if ((p.location & (UInt32)CardLocation.Grave) > 0)
        {
            return_value = gameCardCondition.still_unclickable;
        }
        if ((p.location & (UInt32)CardLocation.Hand) > 0)
        {
            return_value = gameCardCondition.floating_clickable;
        }
        if ((p.location & (UInt32)CardLocation.Removed) > 0)
        {
            return_value = gameCardCondition.still_unclickable;
        }
        if ((p.location & (UInt32)CardLocation.Overlay) > 0)
        {
            return_value = gameCardCondition.still_unclickable;
        }
        return return_value;
    }

    public Vector3 get_point_worldposition(GPS p, gameCard c = null)
    {
        Vector3 return_value = Vector3.zero;
        float real = (Program.fieldSize - 1) * 0.9f + 1f;
        if ((p.location & (UInt32)CardLocation.Deck) > 0)
        {
            if (p.controller==0)    
            {
                return_value = new Vector3(14.65f * real, 0, -14.6f);
            }
            else
            {
                return_value = new Vector3(-15.2f * real, 0, 14.6f);
            }
            return_value.y += p.sequence * 0.03f;
        }
        if ((p.location & (UInt32)CardLocation.Extra) > 0)
        {
            if (p.controller == 0)
            {
                return_value = new Vector3(-15.2f * real, 0, -14.6f);
            }
            else
            {
                return_value = new Vector3(14.65f * real, 0, 14.6f);
            }
            return_value.y += p.sequence * 0.03f;
        }
        if ((p.location & (UInt32)CardLocation.Grave) > 0)
        {
            if (MasterRule >= 4)
            {
                if (p.controller == 0)
                {
                    return_value = new Vector3(14.65f * real, 0, -9f);
                }
                else
                {
                    return_value = new Vector3(-15.2f * real, 0, 9f);
                }
            }
            else
            {
                if (p.controller == 0)
                {
                    return_value = new Vector3(14.65f * real, 0, -3f);
                }
                else
                {
                    return_value = new Vector3(-15.2f * real, 0, 3f);
                }
            }

            return_value.y += p.sequence * 0.03f;
        }
        if ((p.location & (UInt32)CardLocation.Removed) > 0)
        {
            if (MasterRule >= 4)
            {
                if (p.controller == 0)
                {
                    return_value = new Vector3(14.65f * real, 0, -3f);
                }
                else
                {
                    return_value = new Vector3(-15.2f * real, 0, 3f);
                }
            }
            else
            {
                if (p.controller == 0)
                {
                    return_value = new Vector3(14.65f * real + 19.15f - 14.65f, 0, -3f);
                }
                else
                {
                    return_value = new Vector3(-15.2f * real - 19.6f + 15.2f, 0, 3f);
                }
            }

            return_value.y += p.sequence * 0.03f;
        }
        if ((p.location & (UInt32)CardLocation.MonsterZone) > 0)
        {
            UInt32 realIndex = p.sequence;
            if (p.controller==0)    
            {
                realIndex = p.sequence;
                return_value.y = 0;
                return_value.z = -5.68f * real;
            }
            else
            {
                if (realIndex <= 4)
                {
                    realIndex = 4 - p.sequence;
                }else
                if (realIndex == 5)
                {
                    realIndex = 6;
                }else
                if (realIndex == 6)
                {
                    realIndex = 5;
                }
                return_value.y = 0;
                return_value.z = 5.65f * real;
            }
            switch (realIndex)  
            {
                case 0:
                    return_value.x = -10.1f;
                    break;
                case 1:
                    return_value.x = -5.17f;
                    break;
                case 2:
                    return_value.x = -0.27f;
                    break;
                case 3:
                    return_value.x = 4.72f;
                    break;
                case 4:
                    return_value.x = 9.62f;
                    break;
                case 5:
                    return_value.x = -5.17f;
                    return_value.z = 0;
                    break;
                case 6:
                    return_value.x = 4.72f;
                    return_value.z = 0;
                    break;
            }
            return_value.x *= real;
        }
        if ((p.location & (UInt32)CardLocation.SpellZone) > 0)
        {
            if (p.sequence < 5 || ((p.sequence == 6 || p.sequence == 7) && MasterRule >= 4))
            {
                UInt32 realIndex = p.sequence;
                if (p.controller == 0)
                {
                    realIndex = p.sequence;
                    return_value.y = 0;
                    return_value.z = -11.5f * real;
                }
                else
                {
                    if (realIndex <= 4)
                    {
                        realIndex = 4 - p.sequence;
                    }else
                    if (realIndex == 7)
                    {
                        realIndex = 6;
                    }else
                    if (realIndex == 6)
                    {
                        realIndex = 7;
                    }
                    return_value.y = 0;
                    return_value.z = 11.5f * real;
                }
                switch (realIndex)
                {
                    case 0:
                        return_value.x = -10.1f;
                        break;
                    case 1:
                        return_value.x = -5.17f;
                        break;
                    case 2:
                        return_value.x = -0.27f;
                        break;
                    case 3:
                        return_value.x = 4.72f;
                        break;
                    case 4:
                        return_value.x = 9.62f;
                        break;
                    case 6:
                        return_value.x = -10.1f;
                        break;
                    case 7:
                        return_value.x = 9.62f;
                        break;
                }
                return_value.x *= real;
                if (gameField.isLong)
                {
                    if (p.controller == 1)
                    {
                        if (5.85f * real < 10f)
                        {
                            return_value.z = return_value.z - 5.85f * real + 10f;
                        }
                    }
                }
            }
            if (p.sequence == 5)
            {
                if (MasterRule >= 4)
                {
                    if (p.controller == 0)
                    {
                        return_value = new Vector3(-15.2f * real, 0, -9f);
                    }
                    else
                    {
                        return_value = new Vector3(14.65f * real, 0, 9f);
                    }
                }
                else
                {
                    if (p.controller == 0)
                    {
                        return_value = new Vector3(-15.2f * real, 0, -2.7f);
                    }
                    else
                    {
                        return_value = new Vector3(14.65f * real, 0, 2.75f);
                    }
                }
            }
            if (MasterRule <= 3)
            {
                if (p.sequence == 6)
                {
                    if (p.controller == 0)
                    {
                        return_value = new Vector3(-15.2f * real, 0, -9f);
                    }
                    else
                    {
                        return_value = new Vector3(14.65f * real, 0, 9f);
                    }
                }
                if (p.sequence == 7)
                {
                    if (p.controller == 0)
                    {
                        return_value = new Vector3(14.65f * real, 0, -9f);
                    }
                    else
                    {
                        return_value = new Vector3(-15.2f * real, 0, 9f);
                    }
                }
            }
        }
        if ((p.location & (UInt32)CardLocation.Overlay) > 0)
        {
            if (c != null)
            {
                int pposition = c.overFatherCount - 1 - p.position;
                return_value.y -= (pposition + 2) * 0.25f;
                return_value.x += (pposition + 1) * 0.15f;
            }
            else
            {
                return_value.y -= (p.position + 2) * 0.25f;
                return_value.x += (p.position + 1) * 0.15f;
            }

        }
        return return_value;
    }

    arrow Arrow;

    bool replayShowAll = false;
    bool reportShowAll = false;
    public override void initialize()
    {
        Arrow = ((GameObject)MonoBehaviour.Instantiate(Program.I().New_arrow)).GetComponent<arrow>();
        Arrow.gameObject.SetActive(false);
        replayShowAll = Config.Get("replayShowAll", "0") != "0";
        reportShowAll = Config.Get("reportShowAll", "0") != "0";
        gameInfo = create
            (
            Program.I().new_ui_gameInfo,
            Vector3.zero,
            Vector3.zero,
            false,
            Program.ui_back_ground_2d
            ).GetComponent<gameInfo>();
        gameInfo.ini();
        UIHelper.InterGameObject(gameInfo.gameObject);
        shiftCondition(Condition.duel);

        Program.go(1, () =>
        {
            MHS_creatBundle(60, localPlayer(0), CardLocation.Deck);
            MHS_creatBundle(15, localPlayer(0), CardLocation.Extra);
            MHS_creatBundle(60, localPlayer(1), CardLocation.Deck);
            MHS_creatBundle(15, localPlayer(1), CardLocation.Extra);
            for (int i = 0; i < cards.Count; i++)
            {
                cards[i].hide();
            }
        });
    }

    public override void applyHideArrangement()
    {
        base.applyHideArrangement();
        gameInfo.gameObject.SetActive(false);
        hideCaculator();
    }

    public override void applyShowArrangement()
    {
        base.applyShowArrangement();
        if (gameInfo.gameObject.activeInHierarchy == false)
        {
            gameInfo.gameObject.transform.localPosition = new Vector3(300, 0, 0);
            gameInfo.gameObject.SetActive(true);
            iTween.MoveToLocal(gameInfo.gameObject, Vector3.zero, 0.6f);
            gameInfo.ini();
            UIHelper.getByName<UIToggle>(gameInfo.gameObject, "ignore_").value = false;
            UIHelper.getByName<UIToggle>(gameInfo.gameObject, "watch_").value = false;
        }
    }

    public void shiftCondition(Condition condition)
    {
        this.condition = condition;
        // 换条件会整套换掉工具条（SetBar），撤回按钮随之消失，所以这里放开重新建。
        undoButtonCreated = false;
        // 自测开关（qt_undo.on）一局只按一次：换条件意味着新的一局，重新放行。
        undoAutoFired = false;
        switch (condition)
        {
            case Condition.duel:
                SetBar(Program.I().new_bar_duel, 0, 0);
                UIHelper.registEvent(toolBar, "input_", onChat);
                UIHelper.registEvent(toolBar, "gg_", onSurrenderOrEnd);
                UIHelper.registEvent(toolBar, "left_", on_left);
                UIHelper.registEvent(toolBar, "right_", on_right);
                UIHelper.registEvent(toolBar, "rush_", on_rush);
                UIHelper.addButtonEvent_toolShift(toolBar, "go_", on_go);
                UIHelper.addButtonEvent_toolShift(toolBar, "stop_", on_stop);
                dumpUndoButtonPositions();
                break;
            case Condition.watch:
                SetBar(Program.I().new_bar_watchDuel, 0, 0);
                UIHelper.registEvent(toolBar, "input_", onChat);
                UIHelper.registEvent(toolBar, "exit_", onExit);
                UIHelper.registEvent(toolBar, "left_", on_left);
                UIHelper.registEvent(toolBar, "right_", on_right);
                UIHelper.addButtonEvent_toolShift(toolBar, "go_", on_go);
                UIHelper.addButtonEvent_toolShift(toolBar, "stop_", on_stop);
                break;
            case Condition.record:
                SetBar(Program.I().new_bar_watchRecord, 0, 0);
                UIHelper.registEvent(toolBar, "home_", onHome);
                UIHelper.registEvent(toolBar, "left_", on_left);
                UIHelper.registEvent(toolBar, "right_", on_right);
                UIHelper.addButtonEvent_toolShift(toolBar, "go_", on_go);
                UIHelper.addButtonEvent_toolShift(toolBar, "stop_", on_stop);
                break;
            default:
                break;
        }
    }


    int currentMessageIndex = -1;


    public void dangerTicking()
    {
        if (paused == true)
        {
            RMSshow_none(InterString.Get("您的时间不足无法使用ReadingSteiner，时间线强制收束！"));
            on_rush();
        }
    }


    public static bool inSkiping = false;

    /// <summary>
    /// 排查用：把对战工具条上几个按钮的实际屏幕坐标落到轨迹里。
    /// Unity 的屏幕坐标是左下原点，这里已经换算成 Windows 的上左原点。
    ///
    /// ⚠ 相机必须用 <c>Program.camera_back_ground_2d</c>：工具条是 <c>showBarOnly()</c>
    /// 用这台相机摆的，换成 <c>Camera.main</c> 换算出来的坐标整个是错的
    /// （实测报 y=337，而按钮实实在在贴在窗口底部 y≈950）—— 与卡组编辑器那份探针
    /// （<c>DeckManager.dumpEditDeckButtonPositions</c>）保持同一个口径。
    /// </summary>
    private void dumpUndoButtonPositions()
    {
        if (!QuickTestTrace.Enabled)
        {
            return;
        }
        string[] names = new string[] { "undo_", "left_", "right_", "go_", "stop_", "rush_" };
        for (int i = 0; i < names.Length; i++)
        {
            UIButton b = UIHelper.getByName<UIButton>(toolBar, names[i]);
            if (b == null)
            {
                QuickTestTrace.Log("btnpos", names[i] + " = null");
                continue;
            }
            Vector3 sp = Program.camera_back_ground_2d.WorldToScreenPoint(b.transform.position);
            QuickTestTrace.Log("btnpos", names[i]
                + " screen=(" + Mathf.RoundToInt(sp.x) + "," + Mathf.RoundToInt(Screen.height - sp.y) + ")"
                + " winScreenH=" + Screen.height
                + " active=" + b.gameObject.activeInHierarchy
                + " enabled=" + b.isEnabled
                + " alpha=" + b.GetComponentInChildren<UIWidget>()?.alpha);
        }
    }

    /// <summary>上一次落盘工具条坐标的时刻（毫秒），用于每帧节流。</summary>
    int lastBarDumpMs = -1;

    /// <summary>
    /// 工具条上的「撤回」按钮。
    ///
    /// 用户口径：图标与「上一步」(left_) 完全相同，位置摆在它**更左边**；另外 Ctrl+Z 同效。
    /// 做法沿用卡组界面「测试」按钮那一套 —— 克隆既有按钮，插到 left_ 左边一格（工具条间距 40），
    /// 原本占着这一格及更左边的控件（chat_ / input_ 容器）整体左移让位。
    ///
    /// 图标虽然和 left_ 长得一样，但**不再共用 left_ 那张图**：克隆后会把图标换成
    /// texture/ui/undo.png（= left.png 的独立副本），每个按钮各持一份，互不干扰。
    ///
    /// 撤回是人机对局专有功能（联机时对手已经看到你的操作，退不回去），
    /// 所以只在人机对局里建这个按钮 —— 卡组界面的「测试」与主菜单的「人机对战」
    /// 都算，判据见 <see cref="DuelUndo.IsUndoableDuel"/>。
    /// </summary>
    void tryCreateUndoButton()
    {
        if (undoButtonCreated || toolBar == null)
        {
            return;
        }
        if (!DuelUndo.IsUndoableDuel)
        {
            return;
        }
        Transform leftT = toolBar.transform.Find("left_");
        if (leftT == null || toolBar.transform.Find("undo_") != null)
        {
            return;
        }
        GameObject undoBtn = UnityEngine.Object.Instantiate(leftT.gameObject);
        undoBtn.name = "undo_";
        undoBtn.transform.SetParent(toolBar.transform, false);

        // Instantiate 会把 left_ 在运行时加进 UIEventTrigger 的委托（hinter.Start 挂的
        // 悬停提示）一起复制过来；克隆体的 hinter.Start 随后会再挂一遍 → 每次悬停
        // in_() 触发两次、造出两个提示框，而出悬停只淡出一个，另一个要在屏幕上
        // 赖好几秒才消失（用户实测：撤回按钮的提示「一直挂着」）。
        // 这里先把复制来的委托清掉，hinter.Start 会重新挂回唯一的一份。
        UIEventTrigger undoTrigger = undoBtn.GetComponent<UIEventTrigger>();
        if (undoTrigger != null)
        {
            undoTrigger.onHoverOver.Clear();
            undoTrigger.onHoverOut.Clear();
            undoTrigger.onPress.Clear();
        }

        const float ToolBarSpacing = 40f;
        Vector3 p = leftT.localPosition;
        float undoX = p.x - ToolBarSpacing;
        foreach (Transform child in toolBar.transform)
        {
            if (child == undoBtn.transform)
            {
                continue;
            }
            Vector3 c = child.localPosition;
            if (c.x <= undoX + 0.01f)
            {
                child.localPosition = new Vector3(c.x - ToolBarSpacing, c.y, c.z);
            }
        }
        undoBtn.transform.localPosition = new Vector3(undoX, p.y, p.z);

        // registEvent 内部会先 onClick.Clear()，所以克隆带过来的 left_ 那个
        // 「book」回调会被替换掉，不会出现「点撤回顺手把书翻开」。
        UIHelper.registEvent(toolBar, "undo_", on_undo);

        hinter hint = undoBtn.GetComponent<hinter>();
        if (hint != null)
        {
            hint.str = InterString.Get("撤回");
        }

        // 图标换成**自己的一份独立素材**：texture/ui/undo.png（内容 = left.png 的副本）。
        // 撤回按钮是从 left_ 克隆来的，图标原本跟着 left_ 那张图走；改成独立一份之后，
        // 以后单独调撤回的图标不会连带把 left_ 一起改掉 —— 与卡组界面「测试」按钮
        // （texture/ui/test.png = go.png 的副本）同一个口径：每个按钮各持一份，互不干扰。
        UITexture undoTex = null;
        string uiTexDump = "";
        foreach (UITexture t in undoBtn.GetComponentsInChildren<UITexture>(true))
        {
            if (uiTexDump.Length > 0)
            {
                uiTexDump += "|";
            }
            uiTexDump += (t.path == null || t.path.Length == 0) ? "(空)" : t.path;
            if (undoTex == null)
            {
                undoTex = t;
            }
        }
        if (undoTex != null)
        {
            undoTex.path = "undo";
            Texture2D undoIcon = GameTextureManager.get("undo");
            if (undoIcon != null)
            {
                undoTex.mainTexture = undoIcon;
            }
        }
        // 落盘「换成功没有」的硬证据（工具条压在卡图上、还是半透明的，截图形状判不出来，
        // 所以判据走这里）：贴图非空 + 与 left 那张**不是同一个实例**。
        string texAfter = "null";
        if (undoTex != null)
        {
            Texture2D bound = undoTex.mainTexture as Texture2D;
            if (bound == null)
            {
                texAfter = "(空)";
            }
            else
            {
                texAfter = bound.width + "x" + bound.height
                    + " sameAsLeft=" + (GameTextureManager.get("left") == bound);
            }
        }

        undoButtonCreated = true;
        // 顺带把它的屏幕坐标也报出来：验收脚本要能真的点到这个键（坐标不能靠猜）。
        Vector3 usp = Program.camera_main_2d.WorldToScreenPoint(undoBtn.transform.position);
        QuickTestTrace.Log("undo", "created undo button localPos=" + undoBtn.transform.localPosition
            + " leftPos=" + leftT.localPosition
            + " screen=(" + Mathf.RoundToInt(usp.x) + "," + Mathf.RoundToInt(Screen.height - usp.y) + ")"
            + " texture=" + (undoTex != null ? undoTex.path : "null")
            + " texAfter=" + texAfter
            + " uiTexBefore=" + uiTexDump
            + " toolbarChildren=" + toolBar.transform.childCount);
    }

    /// <summary>是否已经建过撤回按钮（避免每帧去 Find）。</summary>
    bool undoButtonCreated = false;

    void on_undo()
    {
        QuickTestTrace.Log("undo", "on_undo clicked: decisions=" + DuelTimeline.decisions.Count
            + " lastManual=" + DuelTimeline.LastManualIndex());
        DuelUndo.Request();
    }

    void on_left()
    {
        QuickTestTrace.Log("undo", "on_left enter: condition=" + condition
            + " keys=" + keys.Count + " Packages_ALL=" + Packages_ALL.Count
            + " currentMessageIndex=" + currentMessageIndex + " paused=" + paused);
        if (winCaculator != null)
        {
            destroy(winCaculator.gameObject);
        }
        int preStepPackagesIndex = 0;
        for (int i = 0; i < keys.Count; i++)
        {
            if (keys[i] < currentMessageIndex)
            {
                preStepPackagesIndex = keys[i];
                break;
            }
        }
        if (keys.Count>0)   
        {
            if (keys[0]!= currentMessageIndex)  
            {
                for (int i = 0; i < keys.Count; i++)
                {
                    if (keys[i] < preStepPackagesIndex)
                    {
                        preStepPackagesIndex = keys[i];
                        break;
                    }
                }
            }
        }
        if (Packages_ALL.Count <= preStepPackagesIndex)
        {
            return;
        }
        if (condition == Condition.duel)
        {
            if (cantCheckGrave)
            {
                RMSshow_none(InterString.Get("不能确认墓地里的卡，无法跨越时间线！"));
                return;
            }
            if (gameInfo.amIdanger())
            {
                RMSshow_none(InterString.Get("您的时间不足无法使用ReadingSteiner！"));
                return;
            }
        }
        bool needSwap = gameInfo.swaped;
        right = false;
        if (paused == false)
        {
            EventDelegate.Execute(UIHelper.getByName<UIButton>(toolBar, "stop_").onClick);
        }
        keys.Clear();
        currentMessageIndex = -1;
        Program.I().book.clear();
        inSkiping = true;
        for (int i = 0; i <= preStepPackagesIndex; i++)
        {
            if (i == preStepPackagesIndex)
            {
                currentMessage = (GameMessage)Packages_ALL[i].Fuction;
                try
                {
                    logicalizeMessage(Packages_ALL[i]);
                }
                catch (System.Exception e)
                {
                    UnityEngine.Debug.Log(e);
                }
                if (needSwap)
                {
                    GCS_swapALL(false);
                }
                try
                {
                    practicalizeMessage(Packages_ALL[i]);
                }
                catch (System.Exception e)
                {
                    UnityEngine.Debug.Log(e);
                }
                clearResponse();
            }
            else
            {
                currentMessage = (GameMessage)Packages_ALL[i].Fuction;
                try
                {
                    logicalizeMessage(Packages_ALL[i]);
                }
                catch (System.Exception e)
                {
                    UnityEngine.Debug.Log(e);
                }
            }
        }
        Packages.Clear();
        for (int i = 0; i < Packages_ALL.Count - preStepPackagesIndex - 1; i++)
        {
            Packages.Add(Packages_ALL[i + preStepPackagesIndex + 1]);
        }
        specialLR();
        inSkiping = false;
        QuickTestTrace.Log("undo", "on_left done: rewound to " + preStepPackagesIndex
            + " / " + Packages_ALL.Count + " leftInQueue=" + Packages.Count);
    }

    public static GameObject LRCgo = null;

    private void specialLR()
    {
        try
        {
            if (LRCgo != null)
            {
                destroy(LRCgo);
            }
            if (gameField != null)
            {
                gameField.shiftBlackHole(false, new Vector3(0, 0, 0));
            }
            Nconfirm();
            cardsForConfirm.Clear();
            if (flagForTimeConfirm)
            {
                flagForTimeConfirm = false;
                MessageBeginTime = Program.TimePassed();
                clearAllShowed();
            }
        }
        catch (Exception e)
        {
            Debug.Log(e);
        }
    }

    bool right = false;
    int keysTempCount = 0;

    void on_right()
    {
        QuickTestTrace.Log("undo", "on_right enter: keys=" + keys.Count + " Packages=" + Packages.Count);
        specialLR();
        if (right)
        {
            inSkiping = true;
            while (keys.Count == keysTempCount && Packages.Count > 0)
            {
                currentMessage = (GameMessage)Packages[0].Fuction;
                try
                {
                    logicalizeMessage(Packages[0]);
                }
                catch (System.Exception e)
                {
                    UnityEngine.Debug.Log(e);
                }
                try
                {
                    practicalizeMessage(Packages[0]);
                }
                catch (System.Exception e)
                {
                    UnityEngine.Debug.Log(e);
                }
                Packages.RemoveAt(0);
            }
            inSkiping = false;
        }
        right = true;
        keysTempCount = keys.Count;
    }

    void on_rush()  
    {
        specialLR();
        while (Packages.Count > 0)
        {
            currentMessage = (GameMessage)Packages[0].Fuction;
            try
            {
                logicalizeMessage(Packages[0]);
            }
            catch (System.Exception e)
            {
                UnityEngine.Debug.Log(e);
            }
            if (Packages.Count==1)  
            {
                try
                {
                    practicalizeMessage(Packages[0]);
                    realize();
                    toNearest();
                }
                catch (System.Exception e)
                {
                    UnityEngine.Debug.Log(e);
                }
            }
            Packages.RemoveAt(0);
        }
        keysTempCount = keys.Count;
        if (paused == true)
        {
            EventDelegate.Execute(UIHelper.getByName<UIButton>(toolBar, "go_").onClick);
        }
    }

    void on_go()
    {
        paused = false;
        if (condition == Condition.duel)
        {
            if (isShowed)
            {
                UIHelper.playSound("phase", 1f);
                gameField.animation_show_big_string(GameTextureManager.ts, true);
            }
            //Program.I().cardDescription.clearAllLog();
            RMSshow_none(InterString.Get("[7CFC00]ReadingSteiner结束，回归到主时间轴。[-]"));
            ((CardDescription)Program.I().cardDescription).setTitle("");
        }
    }

    void on_stop()
    {
        if (cantCheckGrave)
        {
            RMSshow_none(InterString.Get("不能确认墓地里的卡，无法跨越时间线！"));
            return;
        }
        if (paused == false)
        {
            destroy(waitObject, 0, false, true);
            paused = true;
            if (currentMessageIndex > theWorldIndex)
            {
                theWorldIndex = currentMessageIndex;
            }
        }
        if (condition== Condition.record)   
        {
            return;
        }
        if (condition == Condition.duel)
        {
            if (isShowed)
            {
                UIHelper.playSound("nextturn", 1f);
                gameField.animation_show_big_string(GameTextureManager.rs, true);
            }
            Program.I().cardDescription.clearAllLog();
            RMSshow_none(InterString.Get("[FF3030]ReadingSteiner被启动成功！您现在可以随意操作时间。@n长按按钮跳跃时间，闪电按钮回到现在。[-]"));
            ((CardDescription)Program.I().cardDescription).setTitle(InterString.Get("[FF3030]ReadingSteiner 正在跨越时间线[-]"));
        }
    }

    public void onHome()
    {
        returnTo();
    }


    public Servant returnServant;
    public void returnTo()
    {
        TcpHelper.SaveRecord();
        QuickTestTrace.Exit("ocgcore.returnTo exitOnReturn=" + Program.exitOnReturn
            + " returnServant=" + (returnServant != null ? returnServant.GetType().Name : "null"));
        if (Program.exitOnReturn && returnServant != Program.I().deckManager)
        {
            Program.I().menu.onClickExit();
        }
        else if (returnServant != null)
        {
            Program.I().shiftToServant(returnServant);
        }
        else
        {
            Program.I().shiftToServant(Program.I().selectServer);
        }
    }

    public void onExit()
    {
        QuickTestTrace.Exit("ocgcore.onExit isShowed=" + isShowed
            + " returnServant=" + (returnServant != null ? returnServant.GetType().Name : "null"));
        if (TcpHelper.tcpClient != null)
        {
            if (TcpHelper.tcpClient.Connected)
            {
                TcpHelper.tcpClient.Client.Shutdown(0);
                TcpHelper.tcpClient.Close();
            }
            TcpHelper.tcpClient = null;
        }
        Program.I().aiRoom.killServerProcess();
        returnTo();
    }

    public bool surrended = false;

    public void onChat()
    {
        Program.I().room.onSubmit(UIHelper.getByName<UIInput>(toolBar, "input_").value);
        UIHelper.getByName<UIInput>(toolBar, "input_").value = "";
    }

    public int lpLimit = 8000;

    public int timeLimit = 180;

    public string name_0_c = "";

    public string name_1_c = "";

    public string name_0 = "";

    public string name_1 = "";

    public string name_0_tag = "";

    public string name_1_tag = "";

    public int life_0;

    public int life_1;

    List<Package> Packages = new List<Package>();
    List<Package> Packages_ALL = new List<Package>();

    public void addPackage(Package p)
    {
        // 撤回重开后，旧连接的接收缓冲区里可能还躺着几条 GameMsg（实测按下撤回约 0.4 秒后
        // 会飘来一条 SelectChain）。它们不属于新一局，却会占掉新时间线的第 0 条，
        // 把逐条摘要比对整体推后一位 —— 表现就是「一重开就分叉」。直接丢掉。
        if (DuelUndo.DropStalePreStart(p.Fuction))
        {
            return;
        }
        // 撤回追赶：这段入站流在按下撤回那一刻就已经在本地回溯过、并且已经画到屏幕上了，
        // 所以只做「校验 + 录制」，不再交给正常路径（否则就是第二次播放，也就是从头布局）。
        if (DuelUndo.SwallowInbound(p))
        {
            // 🔑 录进**录像包列表**这一步不能省（原来这里直接 return，录制其实没做）。
            //    新一局的开场 GameMessage.Start 就落在追赶窗口里，漏录的后果是连锁的：
            //      SaveRecord 找不到 Start ⇒ 整个不落盘 ⇒ 撤回之后打完既没有录像文件，
            //      也因为没有「待处理的录像」而**不弹是否保存录像的结算面板**
            //      （TcpHelper.SaveRecord 只在扫到 Start/ReloadField 时才写盘）。
            //    旧线（被撤掉那一局）的包不用担心混进来：撤回时旧连接断开，
            //    断线处理里已经把包列表清空了。
            TcpHelper.AddRecordLine(p);
            return;
        }
        TcpHelper.AddRecordLine(p);
        Packages.Add(p);
        Packages_ALL.Add(p);
    }

    public void flushPackages(List<Package> ps)
    {
        Packages.Clear();
        Packages = null;
        Packages = ps;
        Packages_ALL.Clear();
        foreach (var item in Packages)
        {
            Packages_ALL.Add(item);
        }
        // 验收判据：回放数据真的灌进来了（selectReplay.KF_replay → pushCollection 走这里）。
        if (QuickTestTrace.Enabled)
        {
            QuickTestTrace.Log("record", "flush packages=" + Packages.Count
                + " mode=" + GameModeManager.ModeLabel
                + " condition=" + condition);
        }
    }

    int MessageBeginTime = 0;

    int lastReszieTime = 0;

    public GameMessage currentMessage = GameMessage.Waiting;

    public bool paused = false;

    float lastSize = 0;
    float lastAlpha = 0;    

    public List<GameObject> allChainPanelFixedContainer = new List<GameObject>();

    void pre200Frame()
    {
        lastReszieTime = Program.TimePassed();
        if (lastSize != Program.fieldSize || lastAlpha != Program.getVerticalTransparency())
        {
            lastSize = Program.fieldSize;
            lastAlpha = Program.getVerticalTransparency();
            reSize();
        }
        if (allChainPanelFixedContainer.Count > 0)
        {
            allChainPanelFixedContainer.RemoveAll((a) => { return a == null; });
            for (int i = 0; i < allChainPanelFixedContainer.Count; i++)
            {
                allChainPanelFixedContainer[i].transform.localPosition = Vector3.zero;
            }
            List<List<GameObject>> groups = new List<List<GameObject>>();
            for (int i = 0; i < allChainPanelFixedContainer.Count; i++)
            {
                GameObject currentGameobject = allChainPanelFixedContainer[i];
                List<GameObject> toList = null;
                for (int a = 0; a < groups.Count; a++)  
                {
                    if (UIHelper.getScreenDistance(groups[a][0], currentGameobject) < 5f * ((float)Screen.height) / 700f)
                    {
                        toList = groups[a];
                    }
                }
                if (toList==null)   
                {
                    toList = new List<GameObject>();
                    groups.Add(toList);
                }
                toList.Add(currentGameobject);
            }
            for (int a = 0; a < groups.Count; a++)
            {
                for (int b = 0; b < groups[a].Count; b++)   
                {
                    groups[a][b].transform.localPosition = new Vector3(0.35f * (groups[a].Count - b - 1), 0, -0.05f * b - 0.2f);
                }
            }
        }
    }


    public override void preFrameFunction()
    {
        base.preFrameFunction();

        // 排查用：对战工具条按钮的屏幕坐标，每 2 秒报一次（见 dumpUndoButtonPositions）。
        // 必须由「每帧」来报 —— shiftCondition 里那次是在 SetBar 的入场动画中途采的，
        // 报出来的位置还没有停稳，验收脚本照着去点/去取样会采空（实测踩过）。
        if (QuickTestTrace.Enabled && toolBar != null && condition == Condition.duel)
        {
            if (Program.TimePassed() - lastBarDumpMs > 2000)
            {
                lastBarDumpMs = Program.TimePassed();
                dumpUndoButtonPositions();
            }
        }

        // 排查用：对局中把 log/qt_endduel.on 放进产物目录，就会触发一次「对局结束确认」，
        // 用来验证结束后的收尾路径（回卡组编辑器 / 回人机界面 / 换备 / 之后的撤回重开），
        // 不必真的把一局打完。只在 log/qt_debug.on 存在时生效，正式包零影响。
        //
        // 判据是全套人机对局（<see cref="DuelUndo.IsUndoableDuel"/>），不只卡组测试局：
        // 「主菜单人机对战打完该回人机界面」这条收尾路径同样需要无人值守地验到。
        if (QuickTestTrace.Enabled)
        {
            bool want = QuickTestTrace.SwitchOn("qt_endduel.on");
            if (!want)
            {
                endDuelTriggered = false;
                endDuelArmMs = -1;
            }
            else if (!endDuelTriggered
                && DuelUndo.IsUndoableDuel
                && Program.I().deckManager != null && !Program.I().deckManager.isShowed)
            {
                // 可选延时：qt_endduel.wait 里写秒数（浮点）。用于「先让对局跑一会儿再收尾」——
                // 验证时间线录制这类「需要真的有入站消息」的场景时要把对局放够时间。
                // 文件不存在 / 解析失败 = 0 秒，即立刻触发（保持原有测试收尾行为不变）。
                if (endDuelArmMs < 0)
                {
                    endDuelArmMs = Program.TimePassed();
                }
                if (Program.TimePassed() - endDuelArmMs >= EndDuelDelayMs())
                {
                    // 第二阶段：已经真的进到决斗场了（ocgcore 显示 + 卡组编辑器收起），
                    // 直接触发一次「对局结束确认」，替掉「把一局真打完」。
                    endDuelTriggered = true;
                    // 必须把 duelEnded 置真，否则走到的是**投降确认**那一支
                    //（onDuelResultConfirmed 同时是 gg_ 按钮的处理函数，尾段弹「你确定要投降吗」）。
                    // 卡组测试局那边不看这个标记（它的收尾分支会自己清零）。
                    Program.I().room.duelEnded = true;
                    QuickTestTrace.Log("end", "qt_endduel.on -> onDuelResultConfirmed() condition="
                        + condition + " quick=" + Program.I().room.quickThisDuel);
                    onDuelResultConfirmed();
                }
            }
        }

        // 排查用：log/qt_undoreplay.on 存在时，把整局录下来的输入从头重放一遍。
        //
        // 它验收的是撤回的地基 —— 「同一颗种子重开，入站消息流是否逐条一致」。
        // 重放的是**真实录下来的输入**（种子 + 我方应答 + 入站流），不伪造任何操作，
        // 也不替玩家做任何选择；正常玩法下这个开关文件不存在，走不到这里。
        if (QuickTestTrace.Enabled)
        {
            bool wantReplay = QuickTestTrace.SwitchOn("qt_undoreplay.on");
            if (!wantReplay)
            {
                replayAllTriggered = false;
                replayAllArmMs = -1;
            }
            else if (!replayAllTriggered && !DuelUndo.active
                && condition == Condition.duel
                && DuelUndo.IsUndoableDuel
                && DuelTimeline.inbound.Count > 0)
            {
                // 先让对局跑几秒，把开局那段（洗牌/摸牌）收进来，重放比对才有内容。
                if (replayAllArmMs < 0)
                {
                    replayAllArmMs = Program.TimePassed();
                }
                if (Program.TimePassed() - replayAllArmMs >= 3000)
                {
                    replayAllTriggered = true;
                    QuickTestTrace.Log("undo", "qt_undoreplay.on -> 整局重放，录到 inbound="
                        + DuelTimeline.inbound.Count + " decisions=" + DuelTimeline.decisions.Count);
                    DuelUndo.RequestReplayAll();
                }
            }
        }

        Program.reMoveCam(getScreenCenter());
        Program.cameraPosition.z += Program.wheelValue;
        if (Program.cameraPosition.z < camera_min)
        {
            Program.cameraPosition.z = camera_min;
        }
        if (Program.cameraPosition.z > camera_max)
        {
            Program.cameraPosition.z = camera_max;
        }

        if (Input.GetKeyDown(KeyCode.C) == true)
        {
            gameInfo.set_condition(gameInfo.chainCondition.smart);
        }
        if (Input.GetKeyDown(KeyCode.A) == true)
        {
            gameInfo.set_condition(gameInfo.chainCondition.all);
        }
        if (Input.GetKeyDown(KeyCode.S) == true)
        {
            gameInfo.set_condition(gameInfo.chainCondition.no);
        }

        if (Input.GetKeyUp(KeyCode.C) == true)
        {
            gameInfo.set_condition(gameInfo.chainCondition.standard);
        }
        if (Input.GetKeyUp(KeyCode.A) == true)
        {
            gameInfo.set_condition(gameInfo.chainCondition.standard);
        }
        if (Input.GetKeyUp(KeyCode.S) == true)
        {
            gameInfo.set_condition(gameInfo.chainCondition.standard);
        }

        if (Input.GetMouseButtonDown(2))
        {
            if (Program.I().book.isShowed)
            {
                Program.I().book.hide();
            }
            else
            {
                Program.I().book.show();
            }
        }
        if (Input.GetKeyDown(KeyCode.Tab))  
        {
            if (Program.I().book.isShowed==false)
            {
                Program.I().book.show();
            }
        }
        if (Input.GetKeyUp(KeyCode.Tab))
        {
            if (Program.I().book.isShowed==true)
            {
                Program.I().book.hide();
            }
        }
        if (paused == false)
        {
            sibyl();
        }
        if (right == true)
        {
            if (keys.Count == keysTempCount && Packages.Count > 0)
            {
                sibyl();
            }
            else
            {
                right = false;
            }
        }
        if (Program.TimePassed() > lastReszieTime + 200)
        {
            pre200Frame();
        }

        // 撤回重放与热键放在帧末：sibyl() 已经跑完，本帧处理到的消息都算进 inbound 计数了。
        // 撤回按钮在人机对局里按需创建（进决斗场那一刻 quickThisDuel 才是最终值，
        // shiftCondition 时机未必可靠，所以用「建过没有」自己收敛）。
        if (condition == Condition.duel)
        {
            tryCreateUndoButton();
        }
        undoReplayTick();
        undoAutoTick();
        undoHotkeyTick();
        optionDumpTick();
        deckMemoToggleTick();
        deckMemoButtonTick();
        deckMemoPicsTick();
        duelStateTick();
        handCardTick();
        pointerTick();
    }

    /// <summary>是否已经自动按过撤回了（自测开关 qt_undo.on，见 <see cref="undoAutoTick"/>）。</summary>
    bool undoAutoFired = false;

    /// <summary>
    /// 自测用：log/qt_undo.on 里写一个数字 N，则录到第 N 个**玩家决策点**之后自动按一次撤回。
    ///
    /// 它替玩家按的只是「撤回」那个按钮本身（入口仍是 DuelUndo.Request()），
    /// **不代替玩家做对局里的任何选择** —— 要它是因为验收脚本没法可靠点中工具条上的撤回键
    /// （按钮坐标要靠模板匹配），而撤回的入口本来只有一个。
    /// 正常玩的时候这个开关文件不存在，走不到这里。
    /// </summary>
    void undoAutoTick()
    {
        if (undoAutoFired || !QuickTestTrace.Enabled)
        {
            return;
        }
        if (condition != Condition.duel)
        {
            return;
        }
        if (!DuelUndo.IsUndoableDuel)
        {
            return;
        }
        int want = 0;
        try
        {
            string p = QuickTestTrace.LogPath("qt_undo.on");
            if (!QuickTestTrace.SwitchOn("qt_undo.on"))
            {
                return;
            }
            int.TryParse(System.IO.File.ReadAllText(p).Trim(), out want);
        }
        catch (System.Exception)
        {
            return;
        }
        if (want <= 0)
        {
            return;
        }
        int have = DuelTimeline.LastManualIndex() + 1;
        if (have < want)
        {
            return;
        }
        undoAutoFired = true;
        QuickTestTrace.Log("undo", "qt_undo.on -> 自动按下撤回（已录到 " + have
            + " 个玩家决策点，目标 " + want + "）");
        // 用完即把开关写回 0。
        // 不写回的话会变成「撤完又撤」的死循环：撤回重开的那局会重新满足条件
        //（shiftCondition 会复位 undoAutoFired），于是每 5 秒撤一次、永不停止。
        // 那还会让「收手之后玩家又操作了一次」这条判据失效 —— 轨迹里会混进撤回
        // 自己重放的那条 manual 应答，分不清是新操作还是重放。
        try
        {
            System.IO.File.WriteAllText(QuickTestTrace.LogPath("qt_undo.on"), "0");
        }
        catch (System.Exception)
        {
        }
        DuelUndo.Request();
    }

    int lastOptionDumpMs = -1;
    string lastOptionDump = "";

    /// <summary>
    /// 排查用：把当前「可点的选项按钮」连同屏幕坐标落到轨迹里。
    ///
    /// 为什么需要它：验收脚本要在对局里产生一次**玩家操作**（撤回只认人工决策点），
    /// 而选项按钮挂在场上/手牌的卡片上，位置随盘面与分辨率变，脚本没法靠猜坐标点中。
    /// 这里把坐标报出来，脚本照着点即可。没有可点选项时什么都不写，所以不会刷日志。
    /// </summary>
    void optionDumpTick()
    {
        if (!QuickTestTrace.Enabled || condition != Condition.duel)
        {
            return;
        }
        if (Program.TimePassed() - lastOptionDumpMs < 400)
        {
            return;
        }
        lastOptionDumpMs = Program.TimePassed();
        string s = "";
        for (int i = 0; i < cards.Count; i++)
        {
            gameCard c = cards[i];
            if (c == null || c.allButtons == null)
            {
                continue;
            }
            for (int j = 0; j < c.allButtons.Count; j++)
            {
                gameButton b = c.allButtons[j];
                if (b == null || b.gameObject == null || !b.gameObject.activeInHierarchy)
                {
                    continue;
                }
                Vector3 sp = Program.camera_main_2d.WorldToScreenPoint(b.gameObject.transform.position);
                s += "[hint=" + b.hint + " screen=(" + Mathf.RoundToInt(sp.x)
                    + "," + Mathf.RoundToInt(Screen.height - sp.y) + ") src=card]";
            }
        }
        // 卡片按钮之外还有 gameInfo 的「哈希按钮」（战斗阶段 / 结束回合 / 洗切手牌 / 完成选择）。
        // 漏掉它们会正好卡在「手牌没有可发动效果」的整个主要阶段上：那时盘面上唯一能点的就是
        // 结束回合和战斗阶段，而它们不挂在任何卡片上，只枚举卡片就等于「一个可选操作都没有」。
        if (gameInfo != null && gameInfo.allHashedButtons != null)
        {
            for (int i = 0; i < gameInfo.allHashedButtons.Count; i++)
            {
                gameUIbutton hb = gameInfo.allHashedButtons[i];
                if (hb == null || hb.gameObject == null || hb.dying
                    || !hb.gameObject.activeInHierarchy)
                {
                    continue;
                }
                Vector3 hsp = Program.camera_main_2d.WorldToScreenPoint(ButtonWorldCenter(hb.gameObject));
                s += "[hint=" + HashButtonHint(hb) + " screen=(" + Mathf.RoundToInt(hsp.x)
                    + "," + Mathf.RoundToInt(Screen.height - hsp.y) + ") src=hash]";
            }
        }
        // 按钮集合变空时也要落一行。否则脚本读到的是最后一条「有按钮」的旧行，
        // 会照着过期坐标一直点下去 —— 而那时按钮早就不存在了。
        if (s == lastOptionDump)
        {
            return;
        }
        lastOptionDump = s;
        QuickTestTrace.Log("opt", "可选按钮 " + (s.Length == 0 ? "（无）" : s));
    }

    /// <summary>
    /// 哈希按钮的可见中心。优先取碰撞盒中心：这些按钮的贴图锚点不一定在正中，
    /// 只报 transform.position 会让脚本点到按钮边缘之外。取不到才退回 transform.position。
    ///
    /// public：弹窗选项（RMSshow_singleChoice）的坐标探针也要用同一套换算 ——
    /// 那类按钮的文字标签同样不在碰撞盒正中，按标签位置点会落到背景板上（实测踩过）。
    /// </summary>
    public static Vector3 ButtonWorldCenter(GameObject go)
    {
        BoxCollider bc = go.GetComponent<BoxCollider>();
        if (bc == null)
        {
            bc = go.GetComponentInChildren<BoxCollider>();
        }
        if (bc != null)
        {
            return bc.transform.TransformPoint(bc.center);
        }
        return go.transform.position;
    }

    /// <summary>
    /// 哈希按钮的文案。按钮对象自己没存 hint，文案写在它的子标签 hint_ 上，只能从标签读。
    /// 方括号必须去掉 —— [opt] 行是靠方括号分隔各字段的，文案里混进方括号会把解析搞乱。
    /// </summary>
    static string HashButtonHint(gameUIbutton hb)
    {
        string t = hb.hashString;
        try
        {
            UILabel lab = UIHelper.getByName<UILabel>(hb.gameObject, "hint_");
            if (lab != null && !string.IsNullOrEmpty(lab.text))
            {
                t = lab.text;
            }
        }
        catch (System.Exception)
        {
        }
        return t.Replace("[", "").Replace("]", "");
    }

    /// <summary>
    /// 排查用：对局中每约 2 秒把判断「消息泵到底在不在跑 / 是不是在等玩家 / 提示挂起来没有」
    /// 所需的关键字段写成一行。
    ///
    /// 为什么需要：只靠 [stoc] 断没断分不清两种截然不同的状况 ——
    ///   ① 服务器/机器人没消息（客户端在正常等）；
    ///   ② 客户端自己没在消费队列（sibyl 被 paused 挡住、或工具箱/提示没建起来）。
    /// 两者表面都是「对局开着却什么都不发生」。这里把 paused / Packages / currentMessage /
    /// 激活按钮数一并写出来，一眼就能分开。只在 log/qt_debug.on 存在时有输出。
    /// </summary>
    int lastStateDumpMs = -1;

    void duelStateTick()
    {
        if (!QuickTestTrace.Enabled)
        {
            return;
        }
        if (Program.TimePassed() - lastStateDumpMs < 2000)
        {
            return;
        }
        lastStateDumpMs = Program.TimePassed();
        // 两样都要数：allButtons 为 0 = 按钮压根没建；建了但 activeInHierarchy 为假 =
        // 建出来了却没显示。两者的修法完全不同，所以不能只报一个数。
        int activeBtns = 0;
        int totalBtns = 0;
        int shownCards = 0;
        for (int i = 0; i < cards.Count; i++)
        {
            gameCard c = cards[i];
            if (c == null)
            {
                continue;
            }
            if (c.gameObject != null && c.gameObject.activeInHierarchy)
            {
                shownCards++;
            }
            if (c.allButtons == null)
            {
                continue;
            }
            for (int j = 0; j < c.allButtons.Count; j++)
            {
                gameButton b = c.allButtons[j];
                if (b == null || b.gameObject == null)
                {
                    continue;
                }
                totalBtns++;
                if (b.gameObject.activeInHierarchy)
                {
                    activeBtns++;
                }
            }
        }
        // 手牌台账。选项按钮是按 (controller, location, sequence) 反查卡片、查到了才挂上去的
        // （GCS_cardGet 还额外要求卡片 gameObject.activeInHierarchy），所以「提示来了却一个按钮
        // 都没有」时要看的第一件事就是：客户端认为手上有什么、在哪个槽、可见吗、挂没挂按钮。
        string handDesc = "";
        for (int i = 0; i < cards.Count; i++)
        {
            gameCard hc = cards[i];
            if (hc == null || hc.p.location != (UInt32)CardLocation.Hand)
            {
                continue;
            }
            handDesc += "[c" + hc.p.controller + " s" + hc.p.sequence + " pos" + hc.p.position
                + (hc.gameObject != null && hc.gameObject.activeInHierarchy ? " vis" : " hid")
                + " btn" + (hc.allButtons == null ? -1 : hc.allButtons.Count) + "]";
        }
        QuickTestTrace.Log("sd", "cond=" + condition
            + " paused=" + paused
            + " showed=" + isShowed
            + " pkg=" + Packages.Count + "/" + Packages_ALL.Count
            + " keys=" + keys.Count
            + " cur=" + currentMessage
            + " msgIdx=" + currentMessageIndex
            + " cards=" + cards.Count + "(shown=" + shownCards + ")"
            + " btns=" + activeBtns + "/" + totalBtns
            + " dec=" + DuelTimeline.decisions.Count
            + " in=" + DuelTimeline.inbound.Count
            + " undoActive=" + DuelUndo.active
            + " rebuilding=" + DuelUndo.rebuilding
            + " skip=" + inSkiping
            + " scr=" + Screen.width + "x" + Screen.height
            + " hand=" + handDesc
            + " barChildren=" + (toolBar == null ? -1 : toolBar.transform.childCount));
    }

    string lastHandDump = "";
    int lastHandDumpMs = -1;
    string lastPtrDump = "";
    int lastPtrDumpMs = -1;

    /// <summary>
    /// 我方手牌的屏幕落点（给验收脚本当「悬停坐标」用）。
    ///
    /// 为什么需要它：卡片上的选项按钮**不是**服务端消息一到就建好的。gameCard 只在鼠标
    /// 真的悬停到卡上时才进入 excited 态，进而调 ES_excited_handler_button_shower 把按钮
    /// 建出来（按钮的 gameObject 由 gameButton.show 惰性创建）。而「鼠标是否在卡上」比的是
    /// Program.pointedGameObject，那个值来自 Input.mousePosition 的射线 —— 也就是说，
    /// 脚本必须先把真实光标移上去，按钮才会存在。
    ///
    /// 落点取 card/event 的碰撞盒中心：手牌卡面的锚点不在正中，只报 transform.position
    /// 会让光标落到卡外。客户端坐标、左上原点，与 [opt] 一致。
    /// </summary>
    void handCardTick()
    {
        if (!QuickTestTrace.Enabled || condition != Condition.duel)
        {
            return;
        }
        if (Program.TimePassed() - lastHandDumpMs < 1000)
        {
            return;
        }
        lastHandDumpMs = Program.TimePassed();
        string s = "";
        for (int i = 0; i < cards.Count; i++)
        {
            gameCard hc = cards[i];
            if (hc == null || hc.p.location != (UInt32)CardLocation.Hand || hc.p.controller != 0)
            {
                continue;
            }
            if (hc.gameObject == null || !hc.gameObject.activeInHierarchy)
            {
                continue;
            }
            Vector3 aim = hc.gameObject.transform.position;
            Transform evt = hc.gameObject.transform.Find("card");
            if (evt != null)
            {
                evt = evt.Find("event");
            }
            MeshCollider mc = null;
            if (evt != null)
            {
                mc = evt.GetComponent<MeshCollider>();
                aim = mc != null ? mc.bounds.center : evt.position;
            }
            Vector3 sp = Program.camera_game_main.WorldToScreenPoint(aim);
            string nm = "";
            YGOSharp.Card d = hc.get_data();
            if (d != null)
            {
                nm = d.Name;
            }
            s += "[s" + hc.p.sequence + " \"" + nm + "\" screen=(" + Mathf.RoundToInt(sp.x)
                + "," + Mathf.RoundToInt(Screen.height - sp.y) + ")"
                + " cond=" + hc.condition
                + (mc == null ? " 无碰撞盒" : (mc.enabled ? " 可点" : " 碰撞盒禁用"))
                + " es=" + hc.ES_diag()
                + " btn=" + (hc.allButtons == null ? -1 : hc.allButtons.Count)
                + "]";
        }
        if (s.Length == 0 || s == lastHandDump)
        {
            return;
        }
        lastHandDump = s;
        QuickTestTrace.Log("hc", "我方手牌落点 " + s);
    }

    /// <summary>
    /// 光标指向什么（给「脚本移了光标，游戏到底有没有察觉」这个问题用）。
    ///
    /// Program.pointedGameObject 由 Program.Update 里 Input.mousePosition 的射线算出，
    /// 而卡片进入 excited 态（并因此把选项按钮建出来）的唯一判据就是它等于该卡的事件对象。
    /// 所以脚本把光标移上去之后，看这一行就知道是「游戏没收到鼠标」还是「收到了但卡不可点」。
    /// 只在指向变化时输出，避免刷屏。
    /// </summary>
    void pointerTick()
    {
        if (!QuickTestTrace.Enabled || condition != Condition.duel)
        {
            return;
        }
        if (Program.TimePassed() - lastPtrDumpMs < 300)
        {
            return;
        }
        lastPtrDumpMs = Program.TimePassed();
        GameObject p = Program.pointedGameObject;
        string s = (p == null ? "null" : ("\"" + p.name + "\" layer=" + p.layer))
            + " mouse=(" + Mathf.RoundToInt(Input.mousePosition.x) + ","
            + Mathf.RoundToInt(Screen.height - Input.mousePosition.y) + ")"
            + " down=" + Program.InputGetMouseButton_0
            + " up=" + Program.InputGetMouseButtonUp_0;
        if (s == lastPtrDump)
        {
            return;
        }
        lastPtrDump = s;
        QuickTestTrace.Log("ptr", "指向 " + s);
    }

    /// <summary>
    /// 撤回重放的一帧：到点就把录下来的应答原样代发出去。
    ///
    /// 「到点」用入站消息条数对齐 —— 录这条应答时已经处理过几条消息，现在就等到同样条数再发，
    /// 这样发出的必定是「那一刻」的那个应答（位置型数据，早发晚发都会指向别的东西）。
    /// </summary>
    void undoReplayTick()
    {
        DuelTimeline.Decision d = DuelUndo.Tick();
        if (d == null)
        {
            return;
        }
        // 代发时保持原局的 manual 归属：原局是玩家点的就仍记成人工决策点，
        // 否则重放会把「撤回点」一个个抹掉，连按撤回就没得退了。
        autoResponding = !d.manual;
        try
        {
            SendReplayedReturn(d.response);
        }
        finally
        {
            autoResponding = false;
        }
    }

    /// <summary>Ctrl+Z：与工具条上的撤回按钮同一条路径，只在人机对局里生效。</summary>
    void undoHotkeyTick()
    {
        if (condition != Condition.duel)
        {
            return;
        }
        if (!DuelUndo.IsUndoableDuel)
        {
            return;
        }
        if (Input.GetKey(KeyCode.LeftControl) == false
            && Input.GetKey(KeyCode.RightControl) == false)
        {
            return;
        }
        if (Input.GetKeyDown(KeyCode.Z) == false)
        {
            return;
        }
        // 正在聊天框里打字时不抢热键（Ctrl+Z 在那里是撤销输入）。
        UIInput chat = UIHelper.getByName<UIInput>(toolBar, "input_");
        if (chat != null && chat.isSelected)
        {
            return;
        }
        DuelUndo.Request();
    }

    /// <summary>
    /// 撤回的本地回溯：把客户端的「模型」从零重建到快照的第 upTo 条入站消息为止。
    ///
    /// 走的全是既有机制：
    ///   ・清场用 hide()+show()（每次对局起止都在用的那条路径，不必自己抄一份「要清哪些字段」）；
    ///   ・重建用 logicalizeMessage（消息的**逻辑层**：卡片增删与位置、各堆数量都在这里），
    ///     表现层 practicalizeMessage 负责的动画/音效/落位这一段一概不跑，所以整段重建不出戏；
    ///   ・上屏只在最后 realize() 一次 —— 一帧内完成，玩家看到的是盘面「一下子退回去」。
    ///
    /// 必须置 inSkiping：logicalize 内部有些分支会顺手放动画，静默重建期间要压住。
    /// 也必须置 DuelUndo.rebuilding：这段重放**不是**真实对局，
    /// 绝不能把应答发出去、也不能记进时间线。
    /// </summary>
    public void rebuildFromSnapshot(DuelTimeline.Snapshot snap, int upTo)
    {
        bool oldSkip = inSkiping;
        inSkiping = true;
        DuelUndo.rebuilding = true;
        int made = 0;
        try
        {
            int end = upTo;
            if (end > snap.inbound.Count - 1)
            {
                end = snap.inbound.Count - 1;
            }
            for (int i = 0; i <= end; i++)
            {
                Package p = snap.MakePackage(i);
                currentMessage = (GameMessage)p.Fuction;
                try
                {
                    logicalizeMessage(p);
                }
                catch (Exception e)
                {
                    Debug.Log(e);
                }
                // 世界线回看（left_/right_）要的包队列，照正常路径一起铺好。
                Packages_ALL.Add(p);
                made++;
            }
            result = duelResult.disLink;
            realize();
            toNearest();
        }
        finally
        {
            DuelUndo.rebuilding = false;
            inSkiping = oldSkip;
        }
        QuickTestTrace.Log("undo", "rebuild model upTo=" + upTo + " applied=" + made
            + " keys=" + keys.Count + " currentMessageIndex=" + currentMessageIndex);
    }

    void sibyl()
    {
        try
        {
            bool messageIsHandled = false;
            while (true)
            {
                if (Packages.Count == 0)
                {
                    break;
                }
                Package currentPackage = Packages[0];
                currentMessage = (GameMessage)currentPackage.Fuction;
                if (ifMessageImportant(currentPackage))
                {
                    if (Program.TimePassed() < MessageBeginTime)
                    {
                        break;
                    }
                }
                // 撤回用的时间线录制：入站游戏消息流就是重放的驱动程序。
                // 必须收在这里 —— 一个 GameMsg 包里可能塞好几条消息，而这里是
                // 「一条一条跨帧处理」的那一条，粒度才对得上（见 DuelTimeline.Inbound）。
                // 位置要在上面那道「重要消息的动画节流」之后：被 break 掉的这条下一帧会再进来，
                // 收在节流之前就会重复计一条。也要在处理之前收，此时 Data.reader 还没被读走。
                // （撤回追赶期间，同一件事由 DuelUndo.SwallowInbound 在包到达时就做掉了。）
                DuelTimeline.NoteGameMessage(currentPackage.Fuction, currentPackage.Data.get());
                messageIsHandled = true;
                try
                {
                    logicalizeMessage(Packages[0]);
                }
                catch (System.Exception e)
                {
                    UnityEngine.Debug.Log(e);
                    // 正式包里 Debug.Log 进不了任何地方，异常等于被吞掉 ——
                    // 「消息消费了但没有按钮 / 盘面不动」这类症状就是这么来的，所以也写进轨迹。
                    QuickTestTrace.Log("err", "logicalize " + currentMessage + ": " + e);
                }
                // 撤回追赶期间只走逻辑层：这段流的画面在按下撤回那一刻已经画好了，
                // 再演一遍就是「撤回后从头布局」。静默到追平为止。
                if (!DuelUndo.silent)
                {
                    try
                    {
                        practicalizeMessage(Packages[0]);
                    }
                    catch (System.Exception e)
                    {
                        UnityEngine.Debug.Log(e);
                        QuickTestTrace.Log("err", "practicalize " + currentMessage + ": " + e);
                    }
                }
                Packages.RemoveAt(0);
            }
            //if (messageIsHandled)
            //{
            //    realize(false);
            //}
            if (messageIsHandled)
            {
                if (condition == Condition.record)
                {
                    if (Packages.Count == 0)
                    {
                        RMSshow_none(InterString.Get("录像播放结束。"));
                    }
                }
            }
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.Log(e);
            QuickTestTrace.Log("err", "sibyl outer: " + e);
        }
    }

    string winReason="";

    bool ifMessageImportant(Package package)
    {
        BinaryReader r = package.Data.reader;
        r.BaseStream.Seek(0, 0);
        GameMessage msg = (GameMessage)Packages[0].Fuction;
        switch (msg)    
        {
            case GameMessage.Start:
            case GameMessage.Win:
            case GameMessage.ConfirmDecktop:
            case GameMessage.ConfirmCards:
            case GameMessage.ShuffleDeck:
            case GameMessage.ShuffleHand:
            case GameMessage.SwapGraveDeck:
            case GameMessage.ShuffleSetCard:
            case GameMessage.ReverseDeck:
            case GameMessage.DeckTop:
            case GameMessage.NewTurn:
            case GameMessage.NewPhase:
            case GameMessage.Move:
            case GameMessage.PosChange:
            case GameMessage.Swap:
            case GameMessage.ChainSolved:
            case GameMessage.ChainNegated:
            case GameMessage.ChainDisabled:
            case GameMessage.RandomSelected:
            case GameMessage.BecomeTarget:
            case GameMessage.Draw:
            case GameMessage.Damage:
            case GameMessage.Recover:
            case GameMessage.PayLpCost:
            case GameMessage.TossCoin:
            case GameMessage.TossDice:
            case GameMessage.TagSwap:
            case GameMessage.ReloadField:
                return true;
            case GameMessage.FlipSummoning:
            case GameMessage.Summoning:
            case GameMessage.SpSummoning:
            case GameMessage.Chaining:
                return true;
            case GameMessage.Hint:
                int type = r.ReadChar();
                if (type == 8)
                {
                    return true;
                }
                if (type == 10)
                {
                    return true;
                }
                return false;
            case GameMessage.CardHint:
                r.ReadGPS();
                int ctype = r.ReadByte();
                if (ctype == 1)
                {
                    return true;
                }
                return false;
            case GameMessage.SelectBattleCmd:
            case GameMessage.SelectIdleCmd:
            case GameMessage.SelectEffectYn:
            case GameMessage.SelectYesNo:
            case GameMessage.SelectOption:
            case GameMessage.SelectCard:
            case GameMessage.SelectPosition:
            case GameMessage.SelectTribute:
            case GameMessage.SortChain:
            case GameMessage.SelectCounter:
            case GameMessage.SelectSum:
            case GameMessage.SortCard:
            case GameMessage.AnnounceRace:
            case GameMessage.AnnounceAttrib:
            case GameMessage.AnnounceCard:
            case GameMessage.AnnounceNumber:
            case GameMessage.SelectDisfield:
            case GameMessage.SelectPlace:
                if (inIgnoranceReplay() || currentMessageIndex + 1 < theWorldIndex)
                {
                    return false;
                }
                return true;
            case GameMessage.SelectChain:
                if (inIgnoranceReplay() || currentMessageIndex + 1 < theWorldIndex)
                {
                    return false;
                }
                r.ReadChar();
                int count = r.ReadByte();
                int spcount = r.ReadByte();
                int hint0 = r.ReadInt32();
                int hint1 = r.ReadInt32();
                bool ignore = false;
                bool forced = false;
                for (int i = 0; i < count; i++)
                {
                    r.ReadByte(); // flag
                    int f = r.ReadByte(); // forced
                    if (f == 1) forced = true;
                    r.ReadInt32(); // card id
                    r.ReadGPS();
                    r.ReadInt32(); // desc
                }
                if (!forced)
                {
                    var condition = gameInfo.get_condition();
                    if (condition == gameInfo.chainCondition.no)
                    {
                        ignore = true;
                    }
                    else
                    {
                        if (condition == gameInfo.chainCondition.all)
                        {
                            ignore = false;
                        }
                        else
                        {
                            if (condition == gameInfo.chainCondition.smart)
                            {
                                if (count == 0)
                                {
                                    ignore = true;
                                }
                                else
                                {
                                    ignore = false;
                                }
                            }
                            else
                            {
                                if (spcount == 0)
                                {
                                    ignore = true;
                                }
                                else
                                {
                                    ignore = false;
                                }
                            }
                        }
                    }
                }
                if (ignore)      
                {
                    return false;
                }
                return true;
            case GameMessage.Attack:
                return true;
                //case GameMessage.Attack:
                //    if (Program.I().setting.setting.Vbattle.value)
                //    {
                //        return true;
                //    }
                //    else
                //    {
                //        return false;
                //    }
                //case GameMessage.Battle:
                //    if (Program.I().setting.setting.Vbattle.value)
                //    {
                //        return false;
                //    }
                //    else
                //    {
                //        return true;
                //    }
        }
        return false;
    }

    public void forceMSquit()
    {
        Package p = new Package();
        p.Fuction = (int)GameMessage.sibyl_quit;
        Packages.Add(p);
    }

    //handle messages
    enum autoForceChainHandlerType
    {
        autoHandleAll,manDoAll,afterClickManDo
    }
    autoForceChainHandlerType autoForceChainHandler = autoForceChainHandlerType.manDoAll;
    List<gameCard> chainCards = new List<gameCard>();
    bool deckReserved = false;
    public bool cantCheckGrave = false;
    public int turns = 0;
    public List<string> confirmedCards = new List<string>();
    void logicalizeMessage(Package p)
    {
        currentMessageIndex++;
        BinaryReader r = p.Data.reader;
        r.BaseStream.Seek(0, 0);
        int code = 0;
        int count = 0;
        int controller = 0;
        int location = 0;
        int sequence = 0;
        int player = 0;
        int data = 0;
        int type = 0;
        GPS gps;
        gameCard game_card;
        GPS from;
        GPS to;
        gameCard card;
        int val;
        string name;
        surrended = false;
        switch ((GameMessage)p.Fuction)
        {
            case GameMessage.sibyl_chat:
                printDuelLog(r.ReadALLUnicode());
                break;
            case GameMessage.sibyl_name:
                name_0 = r.ReadUnicode(50);
                name_0_tag = r.ReadUnicode(50);
                name_0_c = r.ReadUnicode(50);
                name_1 = r.ReadUnicode(50);
                name_1_tag = r.ReadUnicode(50);
                name_1_c = r.ReadUnicode(50);
                bool isTag = !(name_0_tag == "---" && name_1_tag == "---" && name_0 == name_0_c && name_1 == name_1_c);
                if (isTag)
                {
                    if (isFirst)
                    {
                        name_0_c = name_0;
                        name_1_c = name_1_tag;
                    }
                    else
                    {
                        name_0_c = name_0_tag;
                        name_1_c = name_1;
                    }
                }
                if (r.BaseStream.Position < r.BaseStream.Length)
                {
                    MasterRule = r.ReadInt32();
                }
                else
                {
                    MasterRule = 3;
                }
                break;
            case GameMessage.AiName:
                int length = r.ReadUInt16();
                byte[] buffer = r.ReadBytes(length + 1);
                string n = System.Text.Encoding.UTF8.GetString(buffer, 0, buffer.Length);
                name_1 = n;
                name_1_tag = n;
                name_1_c = n;
                break;
            case GameMessage.Win:
                deckReserved = false;
                cantCheckGrave = false;
                player = localPlayer(r.ReadByte());
                int winType = r.ReadByte();
                keys.Insert(0, currentMessageIndex);
                if (player == 2)
                {
                    result = duelResult.draw;
                    printDuelLog(InterString.Get("游戏平局！"));
                }
                else if (player == 0 || winType == 4)
                {
                    result = duelResult.win;
                    if (cookie_matchKill > 0)
                    {
                        winReason = YGOSharp.CardsManager.Get(cookie_matchKill).Name;
                        printDuelLog(InterString.Get("比赛胜利，卡片：[?]", winReason));
                    }
                    else
                    {
                        winReason = GameStringManager.get("victory", winType);
                        printDuelLog(InterString.Get("游戏胜利，原因：[?]", winReason));
                    }
                }
                else
                {
                    result = duelResult.lose;
                    if (cookie_matchKill > 0)
                    {
                        winReason = YGOSharp.CardsManager.Get(cookie_matchKill).Name;
                        printDuelLog(InterString.Get("比赛败北，卡片：[?]", winReason));
                    }
                    else
                    {
                        winReason = GameStringManager.get("victory", winType);
                        printDuelLog(InterString.Get("游戏败北，原因：[?]", winReason));
                    }
                }
                break;
            case GameMessage.Start:
                confirmedCards.Clear();
                gameField.currentPhase = GameField.ph.dp;
                result = duelResult.disLink;
                logicalClearChain();
                surrended = false;
                // 新的一局 = 录像选择重新开放（测试局的 gg_ 闸门看它，见 onSurrenderOrEnd）。
                // 撤回重开也会重放 Start，所以重开后的那一局同样能重新选一次。
                replayChoiceMade = false;
                Program.I().room.duelEnded = false;
                Program.I().room.joinWithReconnect = false;
                turns = 0;
                deckReserved = false;
                cantCheckGrave = false;
                keys.Insert(0, currentMessageIndex);
                RMSshow_clear();
                md5Maker = 0;
                for (int i = 0; i < cards.Count; i++)
                {
                    cards[i].p.location = (UInt32)CardLocation.Unknown;
                }
                int playertype = r.ReadByte();
                isFirst = ((playertype & 0xf) > 0) ? false : true;
                // 先手归属的**权威真值**：猜拳赢的那一方才有选择权，所以「我们当初点了先攻还是后攻」
                // 在对手赢猜拳时根本没录到 —— 只有这一个字节两种情形下都成立。
                // 撤回重开要靠它把先手钉回去（见 DuelTimeline.startFirst / DuelUndo.ReplayGoFirst）。
                DuelTimeline.NoteStartFirst(isFirst);
                gameInfo.swaped = false;
                isObserver = ((playertype & 0xf0) > 0) ? true : false;
                if (r.BaseStream.Length > 17) // dumb fix for yrp3d replay older than v1.034.9
                    MasterRule = r.ReadByte(); // duel_rule
                life_0 = r.ReadInt32();
                life_1 = r.ReadInt32();
                lpLimit = life_0;
                name_0_c = name_0;
                name_1_c = name_1;
                if (Program.I().room.mode == 2)
                {
                    if (isFirst)
                    {
                        name_1_c = name_1_tag;
                    }
                    else
                    {
                        name_0_c = name_0_tag;
                    }
                }
                cookie_matchKill = 0;
                MHS_creatBundle(r.ReadInt16(), localPlayer(0), CardLocation.Deck);
                MHS_creatBundle(r.ReadInt16(), localPlayer(0), CardLocation.Extra);
                MHS_creatBundle(r.ReadInt16(), localPlayer(1), CardLocation.Deck);
                MHS_creatBundle(r.ReadInt16(), localPlayer(1), CardLocation.Extra);
                gameField.clearDisabled();
                if (Program.I().room.mode == 0)
                {
                    printDuelLog(InterString.Get("单局模式 决斗开始！"));
                }
                if (Program.I().room.mode == 1)
                {
                    printDuelLog(InterString.Get("比赛模式 决斗开始！"));
                }
                if (Program.I().room.mode == 2)
                {
                    printDuelLog(InterString.Get("双打模式 决斗开始！"));
                }
                printDuelLog(InterString.Get("双方生命值：[?]", lpLimit.ToString()));
                printDuelLog(InterString.Get("Tip：鼠标中键/[FF0000]TAB键[-]可以打开/关闭哦。"));
                printDuelLog(InterString.Get("Tip：强烈建议使用[FF0000]TAB键[-]。"));
                arrangeCards();
                Sleep(21);
                break;
            case GameMessage.ReloadField:
                MasterRule = r.ReadByte() + 1;
                if (MasterRule > 255)
                {
                    MasterRule -= 255;
                }
                confirmedCards.Clear();
                gameField.currentPhase = GameField.ph.dp;
                result = duelResult.disLink;
                deckReserved = false;
                //isFirst = true;
                gameInfo.swaped = false;
                logicalClearChain();
                surrended = false;
                Program.I().room.duelEnded = false;
                turns = 0;
                keys.Insert(0, currentMessageIndex);
                RMSshow_clear();
                md5Maker = 0;
                for (int i = 0; i < cards.Count; i++) if (cards[i].gameObject.activeInHierarchy)
                    {
                        cards[i].p.location = (UInt32)CardLocation.Unknown;
                    }
                cookie_matchKill = 0;
                
                if (Program.I().room.mode == 0)
                {
                    printDuelLog(InterString.Get("单局模式 决斗开始！"));
                }
                if (Program.I().room.mode == 1)
                {
                    printDuelLog(InterString.Get("比赛模式 决斗开始！"));
                }
                if (Program.I().room.mode == 2)
                {
                    printDuelLog(InterString.Get("双打模式 决斗开始！"));
                }
                printDuelLog(InterString.Get("双方生命值：[?]", lpLimit.ToString()));
                printDuelLog(InterString.Get("Tip：鼠标中键/[FF0000]TAB键[-]可以打开/关闭哦。"));
                printDuelLog(InterString.Get("Tip：强烈建议使用[FF0000]TAB键[-]。"));
                for (int p_ = 0; p_ < 2; p_++)
                {
                    player = localPlayer(p_);
                    if (player == 0)
                    {
                        life_0 = r.ReadInt32();
                    }
                    else
                    {
                        life_1 = r.ReadInt32();
                    }
                    for (int i = 0; i < 7; i++)
                    {
                        val = r.ReadByte();
                        if (val > 0)
                        {
                            gps = new GPS
                            {
                                controller = (UInt32)player,
                                location = (UInt32)CardLocation.MonsterZone,
                                position = (int)r.ReadByte(),
                                sequence = (UInt32)i,
                            };
                            GCS_cardCreate(gps);
                            val = r.ReadByte();
                            for (int xyz = 0; xyz < val; ++xyz)
                            {
                                gps.location |= (UInt32)CardLocation.Overlay;
                                gps.position = xyz;
                                GCS_cardCreate(gps);
                            }
                        }
                    }
                    for (int i = 0; i < 8; i++)
                    {
                        val = r.ReadByte();
                        if (val > 0)
                        {
                            gps = new GPS
                            {
                                controller = (UInt32)player,
                                location = (UInt32)CardLocation.SpellZone,
                                position = (int)r.ReadByte(),
                                sequence = (UInt32)i,
                            };
                            GCS_cardCreate(gps);
                        }
                    }
                    val = r.ReadByte();
                    for (int i = 0; i < val; i++)
                    {
                        gps = new GPS
                        {
                            controller = (UInt32)player,
                            location = (UInt32)CardLocation.Deck,
                            position = (int)CardPosition.FaceDownAttack,
                            sequence = (UInt32)i,
                        };
                        GCS_cardCreate(gps);
                    }
                    val = r.ReadByte();
                    for (int i = 0; i < val; i++)
                    {
                        gps = new GPS
                        {
                            controller = (UInt32)player,
                            location = (UInt32)CardLocation.Hand,
                            position = (int)CardPosition.FaceDownAttack,
                            sequence = (UInt32)i,
                        };
                        GCS_cardCreate(gps);
                    }
                    val = r.ReadByte();
                    for (int i = 0; i < val; i++)
                    {
                        gps = new GPS
                        {
                            controller = (UInt32)player,
                            location = (UInt32)CardLocation.Grave,
                            position = (int)CardPosition.FaceUpAttack,
                            sequence = (UInt32)i,
                        };
                        GCS_cardCreate(gps);
                    }
                    val = r.ReadByte();
                    for (int i = 0; i < val; i++)
                    {
                        gps = new GPS
                        {
                            controller = (UInt32)player,
                            location = (UInt32)CardLocation.Removed,
                            position = (int)CardPosition.FaceUpAttack,
                            sequence = (UInt32)i,
                        };
                        GCS_cardCreate(gps);
                    }
                    val = r.ReadByte();
                    int val_up = r.ReadByte();
                    for (int i = 0; i < val - val_up; i++)
                    {
                        gps = new GPS
                        {
                            controller = (UInt32)player,
                            location = (UInt32)CardLocation.Extra,
                            position = (int)CardPosition.FaceDownAttack,
                            sequence = (UInt32)i,
                        };
                        GCS_cardCreate(gps);
                    }
                    for (int i = 0; i < val_up; i++)
                    {
                        gps = new GPS
                        {
                            controller = (UInt32)player,
                            location = (UInt32)CardLocation.Extra,
                            position = (int)CardPosition.FaceUpAttack,
                            sequence = (UInt32)(val + i),
                        };
                        GCS_cardCreate(gps);
                    }
                }
                gameField.clearDisabled();
                arrangeCards();
                break;
            case GameMessage.UpdateData:
                controller = localPlayer(r.ReadChar());
                location = r.ReadChar();
                try
                {
                    while (true)
                    {
                        int len = r.ReadInt32();
                        if (len == 4) continue;
                        long pos = r.BaseStream.Position;
                        r.readCardData();
                        r.BaseStream.Position = pos + len - 4;
                    }
                }
                catch (System.Exception e)
                {
                   // UnityEngine.Debug.Log(e);
                }
                break;
            case GameMessage.UpdateCard:
                gps = r.ReadShortGPS();
                gameCard cardToRefresh = GCS_cardGet(gps, false);
                r.ReadUInt32();
                r.readCardData(cardToRefresh);
                break;
            case GameMessage.ReverseDeck:
                deckReserved = !deckReserved;
                break;
            case GameMessage.Move:
                keys.Insert(0, currentMessageIndex);
                code = r.ReadInt32();
                from = r.ReadGPS();
                to = r.ReadGPS();
                card = GCS_cardGet(from, false);
                if (card != null)
                {
                    card.set_code(code);
                }
                GCS_cardMove(from, to);
                break;
            case GameMessage.PosChange:
                keys.Insert(0, currentMessageIndex);
                ES_hint = GameStringManager.get_unsafe(1600);
                code = r.ReadInt32();
                from = r.ReadGPS();
                to = from;
                to.position = r.ReadByte();
                card = GCS_cardGet(from, false);
                if (card != null)
                {
                    card.set_code(code);
                }
                GCS_cardMove(from, to);
                break;
            case GameMessage.Set:
                ES_hint = GameStringManager.get_unsafe(1601);
                break;
            case GameMessage.Swap:
                keys.Insert(0, currentMessageIndex);
                ES_hint = GameStringManager.get_unsafe(1602);
                code = r.ReadInt32();
                from = r.ReadGPS();
                code = r.ReadInt32();
                to = r.ReadGPS();
                GCS_cardMove(from, to, true, true);
                break;
            case GameMessage.FlipSummoned:
                ES_hint = GameStringManager.get_unsafe(1608);
                break;
            case GameMessage.Summoned:
                ES_hint = GameStringManager.get_unsafe(1604);
                break;
            case GameMessage.SpSummoned:
                ES_hint = GameStringManager.get_unsafe(1606);
                break;
            case GameMessage.Chaining:
                code = r.ReadInt32();
                gps = r.ReadGPS();
                card = GCS_cardGet(gps, false);
                if (card != null)
                {
                    card.set_code(code);
                    cardsInChain.Add(card);
                    if (cardsInChain.Count == 1)
                    {
                        cardsInChain[0].CS_showBall();
                    }
                    else
                    {
                        cardsInChain[0].CS_ballToNumber();
                        cardsInChain[cardsInChain.Count - 1].CS_addChainNumber(cardsInChain.Count);
                    }
                    ES_hint = InterString.Get("「[?]」被发动时", card.get_data().Name);
                    if (card.p.controller == 0)
                    {
                        ///printDuelLog("●" + InterString.Get("[?]被发动", UIHelper.getGPSstringName(card)));
                    }
                    else
                    {
                       // printDuelLog("●" + InterString.Get("[?]被对方发动", UIHelper.getGPSstringName(card)));
                    }
                }
                break;
            case GameMessage.ChainSolved:
                int id = r.ReadByte() - 1;
                if (id < 0)
                {
                    id = 0;
                }
                if (id < cardsInChain.Count)
                {
                    card = cardsInChain[id];
                    card.CS_hideBall();
                    card.CS_removeOneChainNumber();
                }
                break;
            case GameMessage.ChainEnd:
                logicalClearChain();
                break;
            case GameMessage.ChainNegated:
            case GameMessage.ChainDisabled:
                int id_ = r.ReadByte() - 1;
                if (id_ < 0)
                {
                    id_ = 0;
                }
                if (id_ < cardsInChain.Count)
                {
                    card = cardsInChain[id_];
                    card.CS_hideBall();
                    card.CS_removeOneChainNumber();
                }
                break;
            case GameMessage.Damage:
                ES_hint = InterString.Get("玩家受到伤害时");
                player = localPlayer(r.ReadByte());
                player = unSwapPlayer(player);
                val = r.ReadInt32();
                if (player == 0)
                {
                    //printDuelLog(InterString.Get("受到伤害[?]", val.ToString()));
                }
                else
                {
                    //printDuelLog(InterString.Get("对方受到伤害[?]", val.ToString()));
                }
                if (player == 0)
                {
                    life_0 -= val;
                }
                else
                {
                    life_1 -= val;
                }
                break;
            case GameMessage.PayLpCost:
                player = localPlayer(r.ReadByte());
                player = unSwapPlayer(player);
                val = r.ReadInt32();
                if (player == 0)
                {
                    //printDuelLog(InterString.Get("支付生命值[?]", val.ToString()));
                }
                else
                {
                    //printDuelLog(InterString.Get("对方支付生命值[?]", val.ToString()));
                }
                if (player == 0)
                {
                    life_0 -= val;
                }
                else
                {
                    life_1 -= val;
                }
                break;
            case GameMessage.Recover:
                ES_hint = InterString.Get("玩家生命值回复时");
                player = localPlayer(r.ReadByte());
                player = unSwapPlayer(player);
                val = r.ReadInt32();
                if (player == 0)
                {
                    //printDuelLog(InterString.Get("回复生命值[?]", val.ToString()));
                }
                else
                {
                    //printDuelLog(InterString.Get("对方回复生命值[?]", val.ToString()));
                }
                if (player == 0)
                {
                    life_0 += val;
                }
                else
                {
                    life_1 += val;
                }
                break;
            case GameMessage.LpUpdate:
                player = localPlayer(r.ReadByte());
                player = unSwapPlayer(player);
                val = r.ReadInt32();
                if (player == 0)
                {
                    //printDuelLog(InterString.Get("刷新生命值[?]", val.ToString()));
                }
                else
                {
                    //printDuelLog(InterString.Get("对方刷新生命值[?]", val.ToString()));
                }
                if (player == 0)
                {
                    life_0 = val;
                }
                else
                {
                    life_1 = val;
                }
                break;
            case GameMessage.RandomSelected:
                player = localPlayer(r.ReadByte());
                count = r.ReadByte();
                for (int i = 0; i < count; i++)
                {
                    gps = r.ReadGPS();
                    card = GCS_cardGet(gps, false);
                    if (card != null)
                    {
                        printDuelLog(InterString.Get("对象选择：[?]", UIHelper.getGPSstringName(card)));
                    }
                }
                break;
            case GameMessage.BecomeTarget:
                count = r.ReadByte();
                for (int i = 0; i < count; i++)
                {
                    gps = r.ReadGPS();
                    card = GCS_cardGet(gps, false);
                    if (card != null)
                    {
                        printDuelLog(InterString.Get("对象选择：[?]", UIHelper.getGPSstringName(card)));
                    }
                }
                break;
            case GameMessage.TossCoin:
                player = r.ReadByte();
                count = r.ReadByte();
                for (int i = 0; i < count; i++)
                {
                    data = r.ReadByte();
                    if (data == 0)
                    {
                        printDuelLog(InterString.Get("硬币反面"));
                    }
                    else
                    {
                        printDuelLog(InterString.Get("硬币正面"));
                    }
                }
                break;
            case GameMessage.TossDice:
                player = r.ReadByte();
                count = r.ReadByte();
                for (int i = 0; i < count; i++)
                {
                    data = r.ReadByte();
                    printDuelLog(InterString.Get("骰子结果：[?]", data.ToString()));
                }
                break;
            case GameMessage.HandResult:
                data = r.ReadByte();
                int res1 = (data & 0x3) - 1;
                int res2 = ((data >> 2) & 0x3) - 1;
                if (isFirst)
                {
                    Program.I().new_ui_handShower.GetComponent<handShower>().me = res1;
                    Program.I().new_ui_handShower.GetComponent<handShower>().op = res2;
                }
                else
                {
                    Program.I().new_ui_handShower.GetComponent<handShower>().me = res2;
                    Program.I().new_ui_handShower.GetComponent<handShower>().op = res1;
                }
                GameObject handres = create(Program.I().new_ui_handShower, Vector3.zero, Vector3.zero, false, Program.ui_main_2d);
                destroy(handres, 10f);
                Sleep(60);
                break;
            case GameMessage.Attack:
                game_card = GCS_cardGet(r.ReadGPS(), false);
                string derectattack = "";
                if (game_card != null)
                {
                    name = game_card.get_data().Name;
                    ES_hint = InterString.Get("「[?]」攻击时", game_card.get_data().Name);
                    //printDuelLog("●" + InterString.Get("[?]发动攻击！", UIHelper.getGPSstringLocation(game_card.p) + UIHelper.getGPSstringName(game_card)));
                    if (game_card.p.controller == 0)
                    {
                        derectattack = "●" + InterString.Get("对方被直接攻击！");
                    }
                    else
                    {
                        derectattack = "●" + InterString.Get("被直接攻击！");
                    }
                }
                game_card = GCS_cardGet(r.ReadGPS(), false);
                if (game_card != null)
                {
                    name = game_card.get_data().Name;
                    //printDuelLog("●" + InterString.Get("[?]被攻击！", UIHelper.getGPSstringLocation(game_card.p) + UIHelper.getGPSstringName(game_card)));
                }
                else
                {
                    //printDuelLog(derectattack);
                }
                break;
            case GameMessage.AttackDisabled:
                ES_hint = InterString.Get("攻击被无效时");
                //printDuelLog(InterString.Get("攻击被无效"));
                break;
            case GameMessage.Battle:
                break;
            case GameMessage.FlipSummoning:
                code = r.ReadInt32();
                name = YGOSharp.CardsManager.Get(code).Name;
                card = GCS_cardGet(r.ReadShortGPS(), false);
                if (card != null)
                {
                    card.set_code(code);
                    card.p.position = (int)CardPosition.FaceUpAttack;
                    card.refreshData();
                    ES_hint = InterString.Get("「[?]」反转召唤宣言时", card.get_data().Name);
                    if (card.p.controller == 0)
                    {
                        //printDuelLog("●" + InterString.Get("[?]被反转召唤", UIHelper.getGPSstringName(card)));
                    }
                    else
                    {
                        //printDuelLog("●" + InterString.Get("[?]被对方反转召唤", UIHelper.getGPSstringName(card)));
                    }
                }
                break;
            case GameMessage.Summoning:
                code = r.ReadInt32();
                name = YGOSharp.CardsManager.Get(code).Name;
                card = GCS_cardGet(r.ReadShortGPS(), false);
                if (card != null)
                {
                    card.set_code(code);
                    ES_hint = InterString.Get("「[?]」通常召唤宣言时", card.get_data().Name);

                    if (card.p.controller == 0)
                    {
                        //printDuelLog("●" + InterString.Get("[?]被通常召唤", UIHelper.getGPSstringName(card)));
                    }
                    else
                    {
                        //printDuelLog("●" + InterString.Get("[?]被对方通常召唤", UIHelper.getGPSstringName(card)));
                    }
                }
                break;
            case GameMessage.SpSummoning:
                code = r.ReadInt32();
                name = YGOSharp.CardsManager.Get(code).Name;
                card = GCS_cardGet(r.ReadShortGPS(), false);
                if (card != null)
                {
                    card.set_code(code);
                    card.add_string_tail(GameStringHelper.teshuzhaohuan);
                    ES_hint = InterString.Get("「[?]」特殊召唤宣言时", card.get_data().Name);

                    if (card.p.controller == 0)
                    {
                        //printDuelLog("●" + InterString.Get("[?]被特殊召唤", UIHelper.getGPSstringName(card)));
                    }
                    else
                    {
                        //printDuelLog("●" + InterString.Get("[?]被对方特殊召唤", UIHelper.getGPSstringName(card)));
                    }
                }
                break;
            case GameMessage.Draw:
                keys.Insert(0, currentMessageIndex);
                ES_hint = InterString.Get("玩家抽卡时");
                controller = localPlayer(r.ReadByte());
                count = r.ReadByte();
                int deckCC = MHS_getBundle(controller, (int)CardLocation.Deck).Count;
                for (int isa = 0; isa < count; isa++)
                {
                    card = GCS_cardMove(
                        new GPS
                        {
                            controller = (UInt32)controller,
                            location = (UInt32)CardLocation.Deck,
                            sequence = (UInt32)(deckCC - 1 - isa),
                            position = (int)CardPosition.FaceDownAttack,
                        }
                    ,
                    new GPS
                    {
                        controller = (UInt32)controller,
                        location = (UInt32)CardLocation.Hand,
                        sequence = (UInt32)(1000),
                        position = (int)CardPosition.FaceDownAttack,
                    }
                    , false);
                    card.set_code(r.ReadInt32() & 0x7fffffff);
                    if (controller == 0)
                    {
                        //printDuelLog(InterString.Get("抽卡[?]", UIHelper.getGPSstringName(card)));
                    }
                    else
                    {
                        //printDuelLog(InterString.Get("对方抽卡[?]", UIHelper.getGPSstringName(card)));
                    }
                }
                break;
            case GameMessage.TagSwap:
                keys.Insert(0, currentMessageIndex);
                controller = localPlayer(r.ReadByte());
                if (controller == 0)
                {
                    if (name_0_c == name_0)
                    {
                        name_0_c = name_0_tag;
                    }
                    else
                    {
                        name_0_c = name_0;
                    }
                }
                else
                {
                    if (name_1_c == name_1)
                    {
                        name_1_c = name_1_tag;
                    }
                    else
                    {
                        name_1_c = name_1;
                    }
                }
                int mcount = r.ReadByte();
                var cardsInDeck = MHS_resizeBundle(mcount, controller, CardLocation.Deck);
                int ecount = r.ReadByte();
                var cardsInExtra = MHS_resizeBundle(ecount, controller, CardLocation.Extra);
                int pcount = r.ReadByte();
                int hcount = r.ReadByte();
                var cardsInHand = MHS_resizeBundle(hcount, controller, CardLocation.Hand);
                if (cardsInDeck.Count > 0)
                {
                    cardsInDeck[cardsInDeck.Count - 1].set_code(r.ReadInt32());
                }
                for (int i = 0; i < cardsInHand.Count; i++)
                {
                    cardsInHand[i].set_code(r.ReadInt32());
                }
                for (int i = 0; i < cardsInExtra.Count; i++)
                {
                    cardsInExtra[i].set_code(r.ReadInt32() & 0x7fffffff);
                }
                for (int i = 0; i < pcount; i++)
                {
                    if (cardsInExtra.Count - 1 - i > 0)
                    {
                        cardsInExtra[cardsInExtra.Count - 1 - i].p.position = (int)CardPosition.FaceUpAttack;
                    }
                }
                if (controller == 0)
                {
                    //printDuelLog(InterString.Get("切换玩家，手牌张数变为[?]", hcount.ToString()));
                }
                else
                {
                    //printDuelLog(InterString.Get("对方切换玩家，手牌张数变为[?]", hcount.ToString()));
                }
                //Program.DEBUGLOG("TAG SWAP->controller:" + controller + "mcount:" + mcount + "ecount:" + ecount + "pcount:" + pcount + "hcount:" + hcount);
                break;
            case GameMessage.MatchKill:
                cookie_matchKill = r.ReadInt32();
                break;
            case GameMessage.PlayerHint:
                controller = localPlayer(r.ReadByte());
                int ptype = r.ReadByte();
                int pvalue = r.ReadInt32();
                string valstring = GameStringManager.get(pvalue);
                if (pvalue == 38723936)
                {
                    valstring = InterString.Get("不能确认墓地里的卡");
                }
                if (ptype == 6)
                {
                    if (controller==0)  
                    {
                        printDuelLog(InterString.Get("我方状态：[?]", valstring));
                    }
                    else
                    {
                        printDuelLog(InterString.Get("对方状态：[?]", valstring));
                    }
                }
                else if (ptype == 7)
                {
                    if (controller == 0)
                    {
                        printDuelLog(InterString.Get("我方取消状态：[?]", valstring));
                    }
                    else
                    {
                        printDuelLog(InterString.Get("对方取消状态：[?]", valstring));
                    }
                }
                break;
            case GameMessage.CardHint:
                game_card = GCS_cardGet(r.ReadGPS(), false);
                int ctype = r.ReadByte();
                int value = r.ReadInt32();
                if (game_card != null)
                {
                    if (ctype == 1)
                    {
                        game_card.del_one_tail(InterString.Get("数字记录："));
                        game_card.add_string_tail(InterString.Get("数字记录：") + value.ToString());
                    }
                    if (ctype == 2)
                    {
                        game_card.del_one_tail(InterString.Get("卡片记录："));
                        game_card.add_string_tail(InterString.Get("卡片记录：") + UIHelper.getSuperName(YGOSharp.CardsManager.Get(value).Name, value));
                    }
                    if (ctype == 3)
                    {
                        game_card.del_one_tail(InterString.Get("种族记录："));
                        game_card.add_string_tail(InterString.Get("种族记录：") + GameStringHelper.race(value));
                    }
                    if (ctype == 4)
                    {
                        game_card.del_one_tail(InterString.Get("属性记录："));
                        game_card.add_string_tail(InterString.Get("属性记录：") + GameStringHelper.attribute(value));
                    }
                    if (ctype == 5)
                    {
                        game_card.del_one_tail(InterString.Get("数字记录："));
                        game_card.add_string_tail(InterString.Get("数字记录：") + value.ToString());
                    }
                    if (ctype == 6)
                    {
                        game_card.add_string_tail(GameStringManager.get(value));
                    }
                    if (ctype == 7)
                    {
                        game_card.del_one_tail(GameStringManager.get(value));
                    }
                }
                break;
           case GameMessage.Hint:
                Es_selectMSGHintType = r.ReadChar();
                Es_selectMSGHintPlayer = localPlayer(r.ReadChar());
                Es_selectMSGHintData = r.ReadInt32();
                type = Es_selectMSGHintType;
                player = Es_selectMSGHintPlayer;
                data = Es_selectMSGHintData;
                if (type == 1)
                {
                    ES_hint = GameStringManager.get(data);
                }
                if (type == 2)
                {
                    printDuelLog(GameStringManager.get(data));
                }
                if (type == 3)
                {
                    ES_selectHint = GameStringManager.get(data);
                }
                if (type == 4)
                {
                    printDuelLog(InterString.Get("效果选择：[?]", GameStringManager.get(data)));
                }
                if (type == 5)
                {
                    printDuelLog(GameStringManager.get(data));
                }
                if (type == 6)
                {
                    printDuelLog(InterString.Get("种族选择：[?]", GameStringHelper.race(data)));
                }
                if (type == 7)
                {
                    printDuelLog(InterString.Get("属性选择：[?]", GameStringHelper.attribute(data)));
                }
                if (type == 8)
                {
                    printDuelLog(InterString.Get("卡片展示：[?]", UIHelper.getSuperName(YGOSharp.CardsManager.Get(data).Name, data)));
                }
                if (type == 9)
                {
                    printDuelLog(InterString.Get("数字选择：[?]", data.ToString()));
                }
                if (type == 10)
                {
                    printDuelLog(InterString.Get("卡片展示：[?]", UIHelper.getSuperName(YGOSharp.CardsManager.Get(data).Name, data)));
                }
                if (type == 11)
                {
                    if (player == 1)
                        data = (data >> 16) | (data << 16);
                    printDuelLog(InterString.Get("区域选择：[?]", GameStringHelper.zone(data)));
                }
                ES_selectCardFromFieldFirstFlag = (type == 3 && data == 575);
                break;
            case GameMessage.MissedEffect:
                r.ReadInt32();
                code = r.ReadInt32();
                printDuelLog(InterString.Get("「[?]」失去了时点。", UIHelper.getSuperName(YGOSharp.CardsManager.Get(code).Name, code)));
                break;
            case GameMessage.NewTurn:
                toDefaultHintLogical();
                gameField.currentPhase = GameField.ph.dp;
                //  keys.Insert(0, currentMessageIndex);
                player = localPlayer(r.ReadByte());
                if (player == 0)
                {
                    ES_turnString = InterString.Get("我方的");
                }
                else
                {
                    ES_turnString = InterString.Get("对方的");
                }
                turns++;
                ES_phaseString = InterString.Get("回合");
                //printDuelLog(InterString.Get("进入[?]", ES_turnString + ES_phaseString)+"  "+ InterString.Get("回合计数[?]", turns.ToString()));
                ES_hint = ES_turnString + ES_phaseString;
                break;
            case GameMessage.NewPhase:
                toDefaultHintLogical();
                autoForceChainHandler =  autoForceChainHandlerType.manDoAll;
               // keys.Insert(0, currentMessageIndex);
                ushort ph = r.ReadUInt16();
                if (ph == 0x01)
                {
                    ES_phaseString = InterString.Get("抽卡阶段");
                    gameField.currentPhase = GameField.ph.dp;
                }
                if (ph == 0x02)
                {
                    ES_phaseString = InterString.Get("准备阶段");
                    gameField.currentPhase = GameField.ph.sp;
                }
                if (ph == 0x04)
                {
                    ES_phaseString = InterString.Get("主要阶段1");
                    gameField.currentPhase = GameField.ph.mp1;
                }
                if (ph == 0x08)
                {
                    ES_phaseString = InterString.Get("战斗阶段");
                    gameField.currentPhase = GameField.ph.bp;
                }
                if (ph == 0x10)
                {
                    ES_phaseString = InterString.Get("战斗步骤");
                    gameField.currentPhase = GameField.ph.bp;
                }
                if (ph == 0x20)
                {
                    ES_phaseString = InterString.Get("伤害步骤");
                    gameField.currentPhase = GameField.ph.bp;
                }
                if (ph == 0x40)
                {
                    ES_phaseString = InterString.Get("伤害判定时");
                    gameField.currentPhase = GameField.ph.bp;
                }
                if (ph == 0x80)
                {
                    ES_phaseString = InterString.Get("战斗阶段");
                    gameField.currentPhase = GameField.ph.bp;
                }
                if (ph == 0x100)
                {
                    ES_phaseString = InterString.Get("主要阶段2");
                    gameField.currentPhase = GameField.ph.mp2;
                }
                if (ph == 0x200)
                {
                    ES_phaseString = InterString.Get("结束阶段");
                    gameField.currentPhase = GameField.ph.ep;
                }
                //printDuelLog(InterString.Get("进入[?]", ES_turnString + ES_phaseString));
                ES_hint = ES_turnString + ES_phaseString;
                break;
            case GameMessage.ConfirmDecktop:
                player = localPlayer(r.ReadByte());
                count = r.ReadByte();
                int countOfDeck = countLocation(player, CardLocation.Deck);
                for (int i = 0; i < count; i++)
                {
                    code = r.ReadInt32();
                    gps = r.ReadShortGPS();
                    card = GCS_cardGet(new GPS
                    {
                        controller = (UInt32)player,
                        location = (UInt32)CardLocation.Deck,
                        sequence = (UInt32)(countOfDeck - 1 - i),
                    }, false);
                    if (card != null)
                    {
                        card.set_code(code);
                        printDuelLog(InterString.Get("[ff0000]确认卡片：[?][-]", UIHelper.getGPSstringName(card, true)));
                        confirmedCards.Add("「" + UIHelper.getSuperName(card.get_data().Name, card.get_data().Id) + "」");
                        if (confirmedCards.Count>=6)    
                        {
                            confirmedCards.RemoveAt(0);
                        }
                    }
                }
                break;
            case GameMessage.ConfirmCards:
                player = localPlayer(r.ReadByte());
                bool skip_panel = r.ReadByte() == 1;
                count = r.ReadByte();
                for (int i = 0; i < count; i++)
                {
                    code = r.ReadInt32();
                    gps = r.ReadShortGPS();
                    card = GCS_cardGet(gps, false);
                    if (card != null)
                    {
                        card.set_code(code);
                        printDuelLog(InterString.Get("[ff0000]确认卡片：[?][-]", UIHelper.getGPSstringName(card, true)));
                        confirmedCards.Add("「" + UIHelper.getSuperName(card.get_data().Name, card.get_data().Id) + "」");
                        if (confirmedCards.Count >= 6)
                        {
                            confirmedCards.RemoveAt(0);
                        }
                    }
                }
                break;
            case GameMessage.DeckTop:
                player = localPlayer(r.ReadByte());
                int countOfDeck_ = countLocation(player, CardLocation.Deck);
                gps = new GPS
                {
                    controller = (UInt32)player,
                    location = (UInt32)CardLocation.Deck,
                    sequence = (UInt32)(countOfDeck_ - 1 - r.ReadByte()),
                };
                code = r.ReadInt32();
                card = GCS_cardGet(gps, false);
                if (card != null)
                {
                    card.set_code(code);
                    printDuelLog(InterString.Get("确认卡片：[?]", UIHelper.getGPSstringName(card)));
                }
                break;
            case GameMessage.RefreshDeck:
            case GameMessage.ShuffleDeck:
                player = localPlayer(r.ReadByte());
                if (player == 0)
                {
                    //printDuelLog(InterString.Get("洗牌"));
                }
                else
                {
                    //printDuelLog(InterString.Get("对方洗牌"));
                }
                for (int i = 0; i < cards.Count; i++) if (cards[i].gameObject.activeInHierarchy)
                    {
                        if ((cards[i].p.location & (UInt32)CardLocation.Deck) > 0)
                        {
                            if (cards[i].p.controller == player)
                            {
                                cards[i].erase_data();
                            }
                        }
                    }
                break;
            case GameMessage.ShuffleHand:
                player = localPlayer(r.ReadByte());
                for (int i = 0; i < cards.Count; i++) if (cards[i].gameObject.activeInHierarchy)
                    {
                        if ((cards[i].p.location & (UInt32)CardLocation.Hand) > 0)
                        {
                            if (cards[i].p.controller == player)
                            {
                                cards[i].erase_data();
                            }
                        }
                    }
                break;
            case GameMessage.SwapGraveDeck:
                player = localPlayer(r.ReadByte());
                for (int i = 0; i < cards.Count; i++) if (cards[i].gameObject.activeInHierarchy)
                    {
                        if (cards[i].p.controller == player)
                        {
                            if ((cards[i].p.location & (UInt32)CardLocation.Deck) > 0)
                            {
                                if (cards[i].p.controller == player)
                                {
                                    cards[i].p.location = (UInt32)CardLocation.Grave;
                                }
                            }
                            else if ((cards[i].p.location & (UInt32)CardLocation.Grave) > 0)
                            {
                                if (cards[i].p.controller == player)
                                {
                                    if(cards[i].IsExtraCard())
                                        cards[i].p.location = (UInt32)CardLocation.Extra;
                                    else
                                        cards[i].p.location = (UInt32)CardLocation.Deck;
                                }
                            }
                        }
                    }
                break;
            case GameMessage.ShuffleSetCard:
                location = r.ReadByte();
                count = r.ReadByte();
                List<GPS> gpss = new List<GPS>();
                for (int i = 0; i < count; i++)
                {
                    gps = r.ReadGPS();
                    gpss.Add(gps);
                    card = GCS_cardGet(gps, false);
                    if (card != null)
                    {
                        card.erase_data();
                    }
                }
                for (int i = 0; i < count; i++)
                {
                    gps = r.ReadGPS();
                    if (gps.location > 0)
                    {
                        GCS_cardMove(gpss[i], gps);
                    }
                }
                break;
            case GameMessage.FieldDisabled:
                UInt32 selectable_field = r.ReadUInt32();
                int filter = 0x1;
                for (int i = 0; i < 5; ++i, filter <<= 1)
                {
                    gps = new GPS
                    {
                        controller = (UInt32)localPlayer(0),
                        location = (UInt32)CardLocation.MonsterZone,
                        sequence = (UInt32)i
                    };
                    if ((selectable_field & filter) > 0)
                    {
                        gameField.set_point_disabled(gps, true);
                    }
                    else
                    {
                        gameField.set_point_disabled(gps, false);
                    }
                }
                filter = 0x100;
                for (int i = 0; i < 8; ++i, filter <<= 1)
                {
                    gps = new GPS
                    {
                        controller = (UInt32)localPlayer(0),
                        location = (UInt32)CardLocation.SpellZone,
                        sequence = (UInt32)i
                    };
                    if ((selectable_field & filter) > 0)
                    {
                        gameField.set_point_disabled(gps, true);
                    }
                    else
                    {
                        gameField.set_point_disabled(gps, false);
                    }
                }
                filter = 0x10000;
                for (int i = 0; i < 5; ++i, filter <<= 1)
                {
                    gps = new GPS
                    {
                        controller = (UInt32)localPlayer(1),
                        location = (UInt32)CardLocation.MonsterZone,
                        sequence = (UInt32)i
                    };
                    if ((selectable_field & filter) > 0)
                    {
                        gameField.set_point_disabled(gps, true);
                    }
                    else
                    {
                        gameField.set_point_disabled(gps, false);
                    }
                }
                filter = 0x1000000;
                for (int i = 0; i < 8; ++i, filter <<= 1)
                {
                    gps = new GPS
                    {
                        controller = (UInt32)localPlayer(1),
                        location = (UInt32)CardLocation.SpellZone,
                        sequence = (UInt32)i
                    };
                    if ((selectable_field & filter) > 0)
                    {
                        gameField.set_point_disabled(gps, true);
                    }
                    else
                    {
                        gameField.set_point_disabled(gps, false);
                    }
                }
                break;
            case GameMessage.CardTarget:
            case GameMessage.Equip:
                from = r.ReadGPS();
                to = r.ReadGPS();
                gameCard card_from = GCS_cardGet(from, false);
                gameCard card_to = GCS_cardGet(to, false);
                if (card_from != null)
                {
                    if ((int)GameMessage.Equip == p.Fuction)
                    {
                        card_from.target.Clear();
                    }
                    card_from.addTarget(card_to);
                }
                break;
            case GameMessage.CancelTarget:
            case GameMessage.Unequip:
                from = r.ReadGPS();
                card = GCS_cardGet(from, false);
                card.target.Clear();
                break;
            case GameMessage.AddCounter:
                type = r.ReadUInt16();
                gps = r.ReadShortGPS();
                card = GCS_cardGet(gps, false);
                count = r.ReadUInt16();
                if (card != null)
                {
                    name = GameStringManager.get("counter", type);
                    for (int i = 0; i < count; i++)
                    {
                        card.add_string_tail(name);
                    }
                }
                break;
            case GameMessage.RemoveCounter:
                type = r.ReadUInt16();
                gps = r.ReadShortGPS();
                card = GCS_cardGet(gps, false);
                count = r.ReadUInt16();
                if (card != null)
                {
                    name = GameStringManager.get("counter", type);
                    for (int i = 0; i < count; i++)
                    {
                        card.del_one_tail(name);
                    }
                }
                break;
        }
        r.BaseStream.Seek(0, 0);
    }

    private int unSwapPlayer(int player)
    {
        if (gameInfo.swaped)
        {
            return 1 - player;
        }
        else
        {
            return player;
        }
    }

    public Package getNamePacket()
    {
        Package p__ = new Package();
        p__.Fuction = (int)GameMessage.sibyl_name;
        p__.Data = new BinaryMaster();
        p__.Data.writer.WriteUnicode(name_0, 50);
        p__.Data.writer.WriteUnicode(name_0_tag, 50);
        p__.Data.writer.WriteUnicode(name_0_c!=""? name_0_c: name_0, 50);
        p__.Data.writer.WriteUnicode(name_1, 50);
        p__.Data.writer.WriteUnicode(name_1_tag, 50);
        p__.Data.writer.WriteUnicode(name_1_c != "" ? name_1_c : name_1, 50);
        p__.Data.writer.Write(Program.I().ocgcore.MasterRule);
        return p__;
    }

    private static void printDuelLog(string toPrint)
    {
        Program.I().book.add(toPrint);
    }

    private int countLocation(int player, CardLocation location_)
    {
        int re = 0;

        for (int i = 0; i < cards.Count; i++) if (cards[i].gameObject.activeInHierarchy)
            {
                if ((cards[i].p.location & (UInt32)location_) > 0)
                {
                    if (cards[i].p.controller == player)
                    {
                        re++;
                    }
                }
            }

        return re;
    }

    private int countLocationSequence(int player, CardLocation location_)  
    {
        int re = 0;

        for (int i = 0; i < cards.Count; i++) if (cards[i].gameObject.activeInHierarchy)
            {
                if ((cards[i].p.location & (UInt32)location_) > 0)
                {
                    if (cards[i].p.controller == player)
                    {
                        if (cards[i].p.sequence > re)
                        {
                            re = (int)cards[i].p.sequence;
                        }
                    }
                }
            }

        return re;
    }

    public bool inIgnoranceReplay()
    {
        return InAI == false && condition != Condition.duel;
    }

    public void reSize()
    {
        realize(true);
    }

    static void shiftArrowHandlerF()
    {
        if (Program.I().ocgcore.Arrow != null)
        {
            Program.I().ocgcore.Arrow.gameObject.SetActive(false);
        }
    }

    static void shiftArrowHandlerT()
    {
        if (Program.I().ocgcore.Arrow != null)
        {
            Program.I().ocgcore.Arrow.gameObject.SetActive(true);
        }
    }

    void shiftArrow(Vector3 from, Vector3 to, bool on, int delay)
    {
        Program.notGo(shiftArrowHandlerT);
        Program.notGo(shiftArrowHandlerF);
        if (on)
        {
            Program.go(delay, shiftArrowHandlerT);
        }
        else
        {
            Program.go(delay, shiftArrowHandlerF);
        }
        if (on)
        {
            Arrow.from.position = from;
            Arrow.to.position = to;
        }
        else
        {
            Arrow.from.position = new Vector3(25, 0, 0);
            Arrow.to.position = new Vector3(25, 0, 5);
        }
        var collection = Arrow.GetComponentsInChildren<Transform>(true);
        foreach (var item in collection)
        {
            item.gameObject.layer = on ? 0 : 4;
        }
    }

    lazyWin winCaculator = null;

    /// <summary>
    /// 本局结算面板上的录像选择（是/否）玩家做过没有。每局 Start 复位。
    ///
    /// 用来让**测试局**的收尾也走一遍录像选择：测试局从卡组界面点「测试」发起，
    /// 工具条上的 gg_（结束/认输）原来是一下子就收口回卡组编辑器的 —— 于是刚录好的
    /// 那一局会以时间戳名（<c>09-18「13：47：26」.yrp3d</c>）自己留在 replay 目录里，
    /// 玩家根本没有「保存还是丢掉」的机会；而正常战斗里这一步是先弹结算面板的。
    /// 见 <see cref="onSurrenderOrEnd"/>。
    /// </summary>
    bool replayChoiceMade = false;

    /// <summary>本局有没有一份「已经落盘、还没被玩家处理」的录像等着做选择。</summary>
    bool hasReplayToDecide()
    {
        string name = TcpHelper.lastRecordName;
        if (string.IsNullOrEmpty(name)) return false;
        return File.Exists(GameModeManager.ReplayDir + "/" + name + ".yrp3d");
    }

    // ── 结算面板按钮坐标探针（只排查用）────────────────────────────────────
    //
    // 验收脚本要在真光标下点面板上的「是/否」，而面板是 iTween 弹出来的、位置还在飞，
    // 脚本没法靠猜坐标点中。口径与 Servant.TraceMSButtons / dumpUndoButtonPositions 一致：
    // 面板挂在 camera_main_2d 下，必须用这台相机换算；并且**变了才写**，
    // 一段窗口内多拍采样 ⇒ 最后一行必然是落定值。
    private string caTraceLast = "";
    private int caTraceTicksLeft = 0;
    private const int CA_TRACE_INTERVAL = 150;
    private const int CA_TRACE_TICKS = 10;

    void traceCaculatorButtons()
    {
        if (!QuickTestTrace.Enabled) return;
        caTraceLast = "";
        caTraceTicksLeft = CA_TRACE_TICKS;
        caTraceSample();
    }

    void caTraceSample()
    {
        if (!QuickTestTrace.Enabled || winCaculator == null || caTraceTicksLeft <= 0) return;
        caTraceTicksLeft--;
        string now = caCoord("yes_") + " | " + caCoord("no_") + " | " + caCoord("input");
        if (now != caTraceLast)
        {
            caTraceLast = now;
            QuickTestTrace.Log("win", "结算面板 " + now);
        }
        if (caTraceTicksLeft > 0)
        {
            Program.go(CA_TRACE_INTERVAL, () => caTraceSample());
        }
    }

    string caCoord(string name)
    {
        Transform t = UIHelper.getByName<Transform>(winCaculator.gameObject, name);
        if (t == null) return name + "=null";
        Vector3 sp = Program.camera_main_2d.WorldToScreenPoint(t.position);
        return name + "=(" + Mathf.RoundToInt(sp.x) + "," + Mathf.RoundToInt(Screen.height - sp.y) + ")";
    }

    /// <summary>
    /// 工具条 gg_（结束/认输）的处理函数。
    ///
    /// 对**测试局**补一道录像闸门：本局有录像待处理、而玩家还没在结算面板上做过选择时，
    /// 先弹结算面板（与正常战斗同一个：输入名字 + 是/否），玩家点完再继续原来的收尾。
    /// 没有这一道，测试局就是「点一下 → 录像自动留在 replay 里 → 直接回卡组编辑器」。
    ///
    /// 三条边界：
    ///   ・SaveRecord 是幂等的（本局包不够 10 条就不落盘），所以先调一次不会有副作用；
    ///   ・没录像可处理（开局就退）时直接走原路，不弹空面板；
    ///   ・面板已经弹着（自然打完那次弹的）时 showCaculator 只刷新文案，玩家接着点就是。
    ///
    /// ⚠ 这条闸门依赖「本局录像能写出来」，而写盘要求包列表里有 GameMessage.Start ——
    /// 撤回重开那一局的 Start 是在**追赶窗口**里到的，曾经因为 SwallowInbound 的提前 return
    /// 而没被录进包列表，于是撤回之后打完既不落盘也不弹面板（见 Ocgcore.addPackage）。
    /// 这条依赖关系很隐蔽：闸门本身的代码没错，错在它依赖的数据在另一条路径上缺了一块。
    /// </summary>
    void onSurrenderOrEnd()
    {
        if (Program.I().room != null && Program.I().room.quickThisDuel && !replayChoiceMade)
        {
            TcpHelper.SaveRecord();
            if (hasReplayToDecide())
            {
                // ⚠ 只在「还没分出胜负」时把结果记成认输：自然打完那次面板已经弹着、
                //   文案已经写成 You Win/Lose 了，这时玩家改点工具条的 gg_ 不能把胜负改写掉。
                if (result == duelResult.disLink) result = duelResult.lose;
                showCaculator();
                return;
            }
            // 自报原因：进了闸门却没弹面板时把卡点写清楚（包数 / 有没有 Start / 录像名）。
            // 「测试局撤回之后打完不弹面板」那次的根因就是 hasStart=False ——
            // 追赶期的入站流（含 Start）原来没录进包列表，SaveRecord 于是整个不落盘。
            // 判据与 hasReplayToDecide 同源，见 replayDeclineReason 的注释。
            QuickTestTrace.Log("win", "测试局收尾未弹面板 why=" + replayDeclineReason());
        }
        onDuelResultConfirmed();
    }

    /// <summary>
    /// 没弹面板的原因（排查用）。与 <see cref="hasReplayToDecide"/> 同源 ——
    /// 同样只看「包列表里有没有开局标记」和「录像名」这两件事，不另立一套条件。
    /// </summary>
    string replayDeclineReason()
    {
        int count = TcpHelper.packagesInRecord.Count;
        bool hasStart = false;
        foreach (Package p in TcpHelper.packagesInRecord)
        {
            if (p.Fuction == (int)GameMessage.Start || p.Fuction == (int)GameMessage.ReloadField)
            {
                hasStart = true;
                break;
            }
        }
        return "packs=" + count + " hasStart=" + hasStart
            + " (SaveRecord 只认 Start/ReloadField，且要 >10 包)"
            + " lastRecordName=\"" + TcpHelper.lastRecordName + "\"";
    }

    void showCaculator()
    {
        if (winCaculator == null)
        {
            if (condition == Condition.watch)
            {
                if (paused == false)
                {
                    EventDelegate.Execute(UIHelper.getByName<UIButton>(toolBar, "stop_").onClick);
                }
            }
            RMSshow_clear();
            float real = (Program.fieldSize - 1) * 0.9f + 1f;
            var point = Program.camera_game_main.WorldToScreenPoint(new Vector3(0, 0, -5.65f * real));
            point.z = 2;
            if (Program.I().setting.setting.Vwin.value)
            {
                UIHelper.playSound("explode", 0.4f);
                GameObject explode = create(result == duelResult.win ? Program.I().mod_winExplode : Program.I().mod_loseExplode);
                var co = explode.AddComponent<animation_screen_lock>();
                co.screen_point = point;
                explode.transform.position = Camera.main.ScreenToWorldPoint(point);
            }
            if (condition == Condition.record)
            {
                winCaculator = create
                (
                Program.I().New_winCaculatorRecord,
                Program.camera_main_2d.ScreenToWorldPoint(point),
                new Vector3(0, 0, 0),
                true,
                Program.ui_main_2d,
                true,
                new Vector3(((float)Screen.height) / 700f, ((float)Screen.height) / 700f, ((float)Screen.height) / 700f)
                ).GetComponent<lazyWin>();
            }
            else
            {
                winCaculator = create
                (
                Program.I().New_winCaculator,
                Program.camera_main_2d.ScreenToWorldPoint(point),
                new Vector3(0, 0, 0),
                true,
                Program.ui_main_2d,
                true,
                new Vector3(((float)Screen.height) / 700f, ((float)Screen.height) / 700f, ((float)Screen.height) / 700f)
                ).GetComponent<lazyWin>();
                UIHelper.InterGameObject(winCaculator.gameObject);
                winCaculator.input.value = UIHelper.getTimeString();
                UIHelper.registEvent(winCaculator.gameObject, "yes_", onSaveReplay);
                UIHelper.registEvent(winCaculator.gameObject, "no_", onGiveUpReplay);
            }
            switch (result)
            {
                case duelResult.disLink:
                    winCaculator.win.text = "Disconnected";
                    break;
                case duelResult.win:
                    winCaculator.win.text = "You Win";
                    break;
                case duelResult.lose:
                    winCaculator.win.text = "You Lose";
                    break;
                case duelResult.draw:
                    winCaculator.win.text = "Draw Game";
                    break;
                default:
                    winCaculator.win.text = "Disconnected";
                    break;
            }
        }
        else
        {
            switch (result)
            {
                case duelResult.win:
                    winCaculator.win.text = "You Win";
                    break;
                case duelResult.lose:
                    winCaculator.win.text = "You Lose";
                    break;
                case duelResult.draw:
                    winCaculator.win.text = "Draw Game";
                    break;
            }
        }
        winCaculator.reason.text = winReason;
        traceCaculatorButtons();
    }

    void onSaveReplay()
    {
        replayChoiceMade = true;
        if (winCaculator != null)
        {
            // 收尾改名也按模式走各自目录（RD = rd/replay/，见 GameModeManager.ReplayDir）。
            string dir = GameModeManager.ReplayDir;
            try
            {
                if (File.Exists(dir + "/" + TcpHelper.lastRecordName + ".yrp3d"))
                {
                    if (TcpHelper.lastRecordName != winCaculator.input.value)
                    {
                        if (File.Exists(dir + "/" + winCaculator.input.value + ".yrp3d"))
                        {
                            File.Delete(dir + "/" + winCaculator.input.value + ".yrp3d");
                        }
                    }
                    File.Move(dir + "/" + TcpHelper.lastRecordName + ".yrp3d", dir + "/" + winCaculator.input.value + ".yrp3d");
                }
            }
            catch (Exception e)   
            {
                RMSshow_none(e.ToString());
            }
        }
        // 录像已经保存落地，这一局的包不再需要。不清掉的话收尾路径（onExit → returnTo）
        // 还会再调一次 SaveRecord、按当前时间另写一份 —— 同一局在 replay/ 里留下两个文件。
        TcpHelper.ClearRecordBuffer();
        onDuelResultConfirmed();
    }

    void onGiveUpReplay()
    {
        replayChoiceMade = true;
        if (winCaculator != null)
        {
            // 放弃录像挪去 -lastReplay 留底，同样按模式走各自目录。
            string dir = GameModeManager.ReplayDir;
            try
            {
                if (File.Exists(dir + "/" + TcpHelper.lastRecordName + ".yrp3d"))
                {
                    if (File.Exists(dir + "/" + "-lastReplay" + ".yrp3d"))
                    {
                        File.Delete(dir + "/" + "-lastReplay" + ".yrp3d");
                    }
                    File.Move(dir + "/" + TcpHelper.lastRecordName + ".yrp3d", dir + "/-lastReplay.yrp3d");
                }
            }
            catch (Exception e)
            {
                RMSshow_none(e.ToString());
            }
        }
        // 玩家选了「不留」：这一局的包必须丢掉。不然收尾的 SaveRecord 会凭空再写出一份
        // 新的时间戳录像 —— 「我明明点了放弃，replay 里还是多了一份」就是这么来的。
        TcpHelper.ClearRecordBuffer();
        onDuelResultConfirmed();
    }

    void hideCaculator()
    {
        if (winCaculator != null)
        {
            if (condition == Condition.watch)
            {
                if (paused == true)
                {
                    EventDelegate.Execute(UIHelper.getByName<UIButton>(toolBar, "go_").onClick);
                }
            }
            destroy(winCaculator.gameObject);
        }
    }

    void practicalizeMessage(Package p)
    {
        int player = 0;
        int count = 0;
        int code = 0;
        int min = 0;
        int max = 0;
        bool cancalable = false;
        GPS gps;
        gameCard card;
        BinaryReader r = p.Data.reader;
        r.BaseStream.Seek(0, 0);
        gameButton btn;
        string desc = "";
        UInt32 available;
        BinaryMaster binaryMaster;
        Vector3 VectorAttackCard;
        Vector3 VectorAttackTarget;
        char type;
        Int32 data;
        int val;
        int cctype;
        GameObject tempobj;
        bool psum = false;
        bool pIN = false;
        BinaryMaster bin;
        long length_of_message = r.BaseStream.Length;
        List<messageSystemValue> values;
        switch ((GameMessage)p.Fuction)
        {
            //case GameMessage.sibyl_clear:
            //    clearResponse();
            //    break;
            case GameMessage.sibyl_quit:
                Program.I().room.duelEnded = true;
                result = duelResult.disLink;
                showCaculator();
                break;
            case GameMessage.Retry:
                Debug.Log("Retry");
                break;
            //case GameMessage.sibyl_delay:
            //    if (inIgnoranceReplay())
            //    {
            //        break;
            //    }
            //    player = localPlayer(r.ReadChar());
            //    gameInfo.setTime(player, Program.I().room.time_limit);
            //    break;
            case GameMessage.sibyl_chat:
                string sss = r.ReadALLUnicode();
                RMSshow_none(sss);
                break;
            case GameMessage.ShowHint:
                int length = r.ReadUInt16();
                byte[] buffer = r.ReadToEnd();
                string n = System.Text.Encoding.UTF8.GetString(buffer, 0, buffer.Length);
                RMSshow_none(n);
                break;
            case GameMessage.sibyl_name:
                gameInfo.realize();
                if (MasterRule >= 4)
                {
                    gameField.loadNewField();
                }
                else
                {
                    gameField.loadOldField();
                }
                break;
            case GameMessage.Hint:
                type = r.ReadChar();
                player = r.ReadChar();
                data = r.ReadInt32();
                if (type == 1)
                {
                    ES_hint = GameStringManager.get(data);
                }
                if (type == 2)
                {
                    RMSshow_none(GameStringManager.get(data));
                }
                if (type == 3)
                {
                    ES_selectHint = GameStringManager.get(data);
                }
                if (type == 4)
                {
                    RMSshow_none(InterString.Get("效果选择：[?]", GameStringManager.get(data)));
                }
                if (type == 5)
                {
                    RMSshow_none(GameStringManager.get(data));
                }
                if (type == 6)
                {
                    RMSshow_none(InterString.Get("种族选择：[?]", GameStringHelper.race(data)));
                }
                if (type == 7)
                {
                    RMSshow_none(InterString.Get("属性选择：[?]", GameStringHelper.attribute(data)));
                }
                if (type == 8)
                {
                    animation_show_card_code(data);
                }
                if (type == 9)
                {
                    RMSshow_none(InterString.Get("数字选择：[?]", data.ToString()));
                }
                if (type == 10)
                {
                    animation_show_card_code(data);
                }
                if (type == 11)
                {
                    if (localPlayer(player) == 1)
                        data = (data >> 16) | (data << 16);
                    RMSshow_none(InterString.Get("区域选择：[?]", GameStringHelper.zone(data)));
                }
                break;
            case GameMessage.MissedEffect:
                break;
            case GameMessage.Waiting:
                if (inIgnoranceReplay() || inTheWorld())
                {
                    break;
                }
                showWait();
                break;
            case GameMessage.Start:
                // 新一局：记牌态一并不带过去（口径：状态只在内存、不跨局）。
                //
                // ⚠ 例外：**撤回**（`DuelUndo` 的「同种子重开 + 把入站流喂回去」）对玩家来说
                // 不是新一局，只是把世界线退回去 —— 走的是同一条 `Start`。用户口径是
                // 「记牌开了就一直开，直到自己点不再记牌」，所以这一路的 Start 不许把开关吃掉，
                // 否则每按一次撤回就得重新点一遍。重放期间旧卡面一律作废（那些牌对象已经换过
                // 一轮了），擦掉等玩家再摊开时自己铺上。
                if (DuelUndo.active == false)
                {
                    endDeckMemo();
                }
                else
                {
                    eraseDeckMemoFaces();
                }
                if (MasterRule >= 4)
                {
                    gameField.loadNewField();
                }
                else
                {
                    gameField.loadOldField();
                }
                realize(true);
                if (condition != Condition.record)
                {
                    if (isObserver)
                    {
                        if (condition != Condition.watch)
                        {
                            shiftCondition(Condition.watch);
                        }
                    }
                    else
                    {
                        if (condition != Condition.duel)
                        {
                            shiftCondition(Condition.duel);
                        }
                    }
                }
                else
                {
                    if (condition != Condition.record)
                    {
                        shiftCondition(Condition.record);
                    }
                }
                card = GCS_cardGet(new GPS
                {
                    controller = (UInt32)0,
                    location = (UInt32)CardLocation.Deck,
                    position = (int)CardPosition.FaceDownAttack,
                    sequence = (UInt32)0,
                }, false);
                if (card != null)
                {
                    Program.I().cardDescription.setData(card.get_data(), card.p.controller == 0 ? GameTextureManager.myBack : GameTextureManager.opBack, card.tails.managedString);
                }
                clearChainEnd();
                hideCaculator();
                break;
            case GameMessage.ReloadField:
                if (MasterRule >= 4)
                {
                    gameField.loadNewField();
                }
                else
                {
                    gameField.loadOldField();
                }
                realize(true);
                if (condition != Condition.record)
                {
                    if (isObserver)
                    {
                        if (condition != Condition.watch)
                        {
                            shiftCondition(Condition.watch);
                        }
                    }
                    else
                    {
                        if (condition != Condition.duel)
                        {
                            shiftCondition(Condition.duel);
                        }
                    }
                }
                else
                {
                    if (condition != Condition.record)
                    {
                        shiftCondition(Condition.record);
                    }
                }

                card = GCS_cardGet(new GPS
                {
                    controller = (UInt32)0,
                    location = (UInt32)CardLocation.Hand,
                    position = (int)CardPosition.FaceDownAttack,
                    sequence = (UInt32)0,
                }, false);
                if (card != null)
                {
                    Program.I().cardDescription.setData(card.get_data(), card.p.controller == 0 ? GameTextureManager.myBack : GameTextureManager.opBack, card.tails.managedString);
                }
                clearChainEnd();
                hideCaculator();
                break;
            case GameMessage.Win:
                player = localPlayer(r.ReadByte());
                int winType = r.ReadByte();
                showCaculator();
                Sleep(120);
                if (player == 2)
                {
                    RMSshow_none(InterString.Get("游戏平局！"));
                }
                else if (player == 0 || winType == 4)
                {
                    if (cookie_matchKill > 0)
                    {
                        RMSshow_none(InterString.Get("比赛胜利，卡片：[?]", winReason));
                    }
                    else
                    {
                        RMSshow_none(InterString.Get("游戏胜利，原因：[?]", winReason));
                    }
                }
                else
                {
                    if (cookie_matchKill > 0)
                    {
                        RMSshow_none(InterString.Get("比赛败北，卡片：[?]", winReason));
                    }
                    else
                    {
                        RMSshow_none(InterString.Get("游戏败北，原因：[?]", winReason));
                    }
                }
                break;
            case GameMessage.RequestDeck:
                break;
            case GameMessage.SelectBattleCmd:
                if (inIgnoranceReplay() || inTheWorld())
                {
                    break;
                }
                if (condition == Condition.record)
                {
                    Sleep(20);
                }
                destroy(waitObject, 0, false, true);
                toDefaultHint();
                player = localPlayer(r.ReadChar());
                count = r.ReadByte();
                for (int i = 0; i < count; i++)
                {
                    code = r.ReadInt32();
                    gps = r.ReadShortGPS();
                    desc = GameStringManager.get(r.ReadInt32());
                    card = GCS_cardGet(gps, false);
                    if (card != null)
                    {
                        card.set_code(code);
                        card.prefered = true;
                        Effect eff = new Effect();
                        eff.ptr = ((i << 16) + 0);
                        eff.desc = desc;
                        card.effects.Add(eff);
                        if (card.query_hint_button(InterString.Get("发动效果@ui")) == false)
                        {
                            btn = new gameButton(((i << 16) + 0), InterString.Get("发动效果@ui"), superButtonType.act);
                            btn.cookieCard = card;
                            card.add_one_button(btn);
                            if (card.condition != gameCardCondition.verticle_clickable)
                            {
                                card.add_one_decoration(Program.I().mod_ocgcore_decoration_card_active, 2, Vector3.zero, "active", true, true);
                                if (card.isHided())
                                    card.currentFlash = gameCard.flashType.Active;
                            }
                        }
                    }
                }
                count = r.ReadByte();
                for (int i = 0; i < count; i++)
                {
                    code = r.ReadInt32();
                    gps = r.ReadShortGPS();
                    r.ReadByte();
                    card = GCS_cardGet(gps, false);
                    if (card != null)
                    {
                        card.set_code(code);
                        btn = new gameButton(((i << 16) + 1), InterString.Get("攻击宣言@ui"), superButtonType.attack);
                        card.add_one_button(btn);
                        card.add_one_decoration(Program.I().mod_ocgcore_bs_atk_decoration, 5, Vector3.zero, "atk");
                    }
                }
                byte mp = r.ReadByte();
                byte ep = r.ReadByte();
                if (mp == 1)
                {
                    gameInfo.addHashedButton("", 2, superButtonType.mp, InterString.Get("主要阶段@ui"));
                    gameField.retOfMp = 2;
                    gameField.Phase.colliderMp2.enabled = true;
                }
                if (ep == 1)
                {
                    gameInfo.addHashedButton("", 3, superButtonType.ep, InterString.Get("结束回合@ui"));
                    gameField.retOfEp = 3;
                    gameField.Phase.colliderEp.enabled = true;
                }
                realize();
                break;
            case GameMessage.SelectIdleCmd:
                if (inIgnoranceReplay() || inTheWorld())
                {
                    break;
                }
                if (condition == Condition.record)
                {
                    Sleep(20);
                }
                destroy(waitObject, 0, false, true);
                toDefaultHint();
                player = localPlayer(r.ReadChar());
                count = r.ReadByte();
                for (int i = 0; i < count; i++)
                {
                    code = r.ReadInt32();
                    gps = r.ReadShortGPS();
                    card = GCS_cardGet(gps, false);
                    if (card != null)
                    {
                        card.set_code(code);
                        btn = new gameButton(((i << 16) + 0), InterString.Get("通常召唤@ui"), superButtonType.summon);
                        card.add_one_button(btn);
                    }
                }
                count = r.ReadByte();
                for (int i = 0; i < count; i++)
                {
                    code = r.ReadInt32();
                    gps = r.ReadShortGPS();
                    card = GCS_cardGet(gps, false);
                    if (card != null)
                    {
                        card.set_code(code);
                        card.prefered = true;
                        if (card.query_hint_button(InterString.Get("特殊召唤@ui")) == false)
                        {
                            btn = new gameButton(((i << 16) + 1), InterString.Get("特殊召唤@ui"), superButtonType.spsummon);
                            card.add_one_button(btn);
                            if (card.condition != gameCardCondition.verticle_clickable)
                            {
                                card.add_one_decoration(Program.I().mod_ocgcore_decoration_spsummon, 2, Vector3.zero, "chain_selecting", true, true);
                                if (card.isHided())
                                    card.currentFlash = gameCard.flashType.SpSummon;
                            }
                        }
                    }
                }
                count = r.ReadByte();
                for (int i = 0; i < count; i++)
                {
                    code = r.ReadInt32();
                    gps = r.ReadShortGPS();
                    card = GCS_cardGet(gps, false);
                    if (card != null)
                    {
                        card.set_code(code);
                        btn = new gameButton(((i << 16) + 2), InterString.Get("表示形式@ui"), superButtonType.change);
                        card.add_one_button(btn);
                    }
                }
                count = r.ReadByte();
                for (int i = 0; i < count; i++)
                {
                    code = r.ReadInt32();
                    gps = r.ReadShortGPS();
                    card = GCS_cardGet(gps, false);
                    if (card != null)
                    {
                        card.set_code(code);
                        btn = new gameButton(((i << 16) + 3), InterString.Get("前场放置@ui"), superButtonType.set);
                        card.add_one_button(btn);
                    }
                }
                count = r.ReadByte();
                for (int i = 0; i < count; i++)
                {
                    code = r.ReadInt32();
                    gps = r.ReadShortGPS();
                    card = GCS_cardGet(gps, false);
                    if (card != null)
                    {
                        card.set_code(code);
                        btn = new gameButton(((i << 16) + 4), InterString.Get("后场放置@ui"), superButtonType.set);
                        card.add_one_button(btn);
                    }
                }
                count = r.ReadByte();
                for (int i = 0; i < count; i++)
                {
                    code = r.ReadInt32();
                    gps = r.ReadShortGPS();
                    int descP = r.ReadInt32();
                    desc = GameStringManager.get(descP);
                    card = GCS_cardGet(gps, false);
                    // 排查用：第 6 组（手上发动的效果）是「服务端给了、客户端却没按钮」最容易出事的一组，
                    // 逐条记下它给出的槽位和反查结果，就能分清是槽位对不上还是卡不可见。
                    QuickTestTrace.Log("g6", "i=" + i + " code=" + code
                        + " slot=c" + gps.controller + " l" + gps.location + " s" + gps.sequence
                        + " -> " + (card == null ? "null" : "ok"));
                    if (card != null)
                    {
                        card.set_code(code);
                        card.prefered = true;
                        if (descP == 1160)
                        {
                            btn = new gameButton(((i << 16) + 5), InterString.Get("灵摆发动@ui"), superButtonType.act);
                            card.add_one_button(btn);
                            if (card.condition != gameCardCondition.verticle_clickable)
                            {
                                card.add_one_decoration(Program.I().mod_ocgcore_decoration_card_active, 2, Vector3.zero, "active", true, true);
                                if (card.isHided())
                                    card.currentFlash = gameCard.flashType.Active;
                            }
                        }
                        else
                        {
                            Effect eff = new Effect();
                            eff.ptr = ((i << 16) + 5);
                            eff.desc = desc;
                            card.effects.Add(eff);
                            if (card.query_hint_button(InterString.Get("发动效果@ui")) == false)
                            {
                                btn = new gameButton(((i << 16) + 5), InterString.Get("发动效果@ui"), superButtonType.act);
                                btn.cookieCard = card;
                                card.add_one_button(btn);
                                if (card.condition != gameCardCondition.verticle_clickable)
                                {
                                    card.add_one_decoration(Program.I().mod_ocgcore_decoration_card_active, 2, Vector3.zero, "active", true, true);
                                    if (card.isHided())
                                        card.currentFlash = gameCard.flashType.Active;
                                }
                            }
                        }
                    }
                }
                byte bp = r.ReadByte();
                byte ep2 = r.ReadByte();
                byte shuffle = r.ReadByte();
                if (bp == 1)
                {
                    gameInfo.addHashedButton("", 6, superButtonType.bp, InterString.Get("战斗阶段@ui"));
                    gameField.retOfbp = 6;
                    gameField.Phase.colliderBp.enabled = true;
                }
                if (ep2 == 1)
                {
                    gameInfo.addHashedButton("", 7, superButtonType.ep, InterString.Get("结束回合@ui"));
                    gameField.retOfEp = 7;
                    gameField.Phase.colliderEp.enabled = true;
                }
                if (shuffle == 1)
                {
                    gameInfo.addHashedButton("", 8, superButtonType.change, InterString.Get("洗切手牌@ui"));
                }
                realize();
                break;
            case GameMessage.SelectEffectYn:
                if (inIgnoranceReplay() || inTheWorld())
                {
                    break;
                }
                if (condition == Condition.record)
                {
                    Sleep(20);
                }
                destroy(waitObject, 0, false, true);
                player = localPlayer(r.ReadByte());
                code = r.ReadInt32();
                gps = r.ReadShortGPS();
                r.ReadByte();
                int cr = r.ReadInt32();
                card = GCS_cardGet(gps, false);
                if (card != null)
                {
                    string displayname = "「" + card.get_data().Name + "」";
                    if (cr == 0)
                    {
                        desc = GameStringManager.get(200);
                        Regex forReplaceFirst = new Regex("\\[%ls\\]");
                        desc = forReplaceFirst.Replace(desc, GameStringManager.formatLocation(gps), 1);
                        desc = forReplaceFirst.Replace(desc, displayname, 1);
                    }
                    else if (cr == 221)
                    {
                        desc = GameStringManager.get(221);
                        Regex forReplaceFirst = new Regex("\\[%ls\\]");
                        desc = forReplaceFirst.Replace(desc, GameStringManager.formatLocation(gps), 1);
                        desc = forReplaceFirst.Replace(desc, displayname, 1);
                        desc = desc + "\n" + GameStringManager.get(223);
                    }
                    else
                    {
                        desc = GameStringManager.get(cr);
                        Regex forReplaceFirst = new Regex("\\[%ls\\]");
                        desc = forReplaceFirst.Replace(desc, displayname, 1);
                    }
                    string hin = ES_hint + "，\n" + desc;
                    RMSshow_yesOrNo("return", hin, new messageSystemValue { value = "1", hint = "yes" }, new messageSystemValue { value = "0", hint = "no" });
                    card.add_one_decoration(Program.I().mod_ocgcore_decoration_chain_selecting, 4, Vector3.zero, "chain_selecting");
                    card.currentFlash = gameCard.flashType.Active;
                }
                break;
            case GameMessage.SelectYesNo:
                if (inIgnoranceReplay() || inTheWorld())
                {
                    break;
                }
                if (condition == Condition.record)
                {
                    Sleep(20);
                }
                destroy(waitObject, 0, false, true);
                player = localPlayer(r.ReadByte());
                desc = GameStringManager.get(r.ReadInt32());
                RMSshow_yesOrNo("return", desc, new messageSystemValue { value = "1", hint = "yes" }, new messageSystemValue { value = "0", hint = "no" });
                break;
            case GameMessage.SelectOption:
                if (inIgnoranceReplay() || inTheWorld())
                {
                    break;
                }
                if (condition == Condition.record)
                {
                    Sleep(60);
                }
                destroy(waitObject, 0, false, true);
                player = localPlayer(r.ReadByte());
                count = r.ReadByte();
                if (count > 1)
                {
                    values = new List<messageSystemValue>();
                    for (int i = 0; i < count; i++)
                    {
                        desc = GameStringManager.get(r.ReadInt32());
                        values.Add(new messageSystemValue { hint = desc, value = i.ToString() });
                    }
                    RMSshow_singleChoice("return", values);
                }
                else
                {
                    // 只有一个选项 = 玩家没得选，直接替答。这不算玩家的决策点，
                    // 走自动出口，别让它成为撤回点。
                    binaryMaster = new BinaryMaster();
                    binaryMaster.writer.Write(0);
                    sendReturnAuto(binaryMaster.get());
                }

                break;
            case GameMessage.SelectTribute:
                if (inIgnoranceReplay() || inTheWorld())
                {
                    break;
                }
                if (condition == Condition.record)
                {
                    Sleep(60);
                }
                destroy(waitObject, 0, false, true);
                player = localPlayer(r.ReadByte());
                cancalable = (r.ReadByte() != 0);
                ES_min = r.ReadByte();
                ES_max = r.ReadByte();
                ES_level = 0;
                count = r.ReadByte();
                for (int i = 0; i < count; i++)
                {
                    code = r.ReadInt32();
                    gps = r.ReadShortGPS();
                    card = GCS_cardGet(gps, false);
                    if (card != null)
                    {
                        card.set_code(code);
                        card.prefered = true;
                        card.forSelect = true;
                        card.selectPtr = i;
                        int para = r.ReadByte();
                        card.levelForSelect_1 = para;
                        card.levelForSelect_2 = para;
                        allCardsInSelectMessage.Add(card);
                    }
                }
                if (cancalable)
                {
                    gameInfo.addHashedButton("cancleSelected", -1, superButtonType.no, InterString.Get("取消选择@ui"));
                }
                realizeCardsForSelect();
                if (ES_selectHint != "")
                {
                    gameField.setHint(ES_selectHint + " " + ES_min.ToString() + "-" + ES_max.ToString());
                }
                else
                {
                    gameField.setHint(InterString.Get("请选择卡片。") + " " + ES_min.ToString() + "-" + ES_max.ToString());
                }
                break;
            case GameMessage.SelectCard:
                if (inIgnoranceReplay() || inTheWorld())
                {
                    break;
                }
                if (condition == Condition.record)
                {
                    Sleep(60);
                }
                destroy(waitObject, 0, false, true);
                player = localPlayer(r.ReadByte());
                cancalable = (r.ReadByte() != 0);
                ES_min = r.ReadByte();
                ES_max = r.ReadByte();
                ES_level = 0;
                count = r.ReadByte();
                for (int i = 0; i < count; i++)
                {
                    code = r.ReadInt32();
                    gps = r.ReadGPS();
                    card = GCS_cardGet(gps, false);
                    if (card != null)
                    {
                        card.set_code(code);
                        card.prefered = true;
                        card.forSelect = true;
                        card.selectPtr = i;
                        allCardsInSelectMessage.Add(card);
                    }
                }
                if(ES_selectCardFromFieldFirstFlag && cancalable)
                {
                    ES_selectCardFromFieldFirstFlag = false;
                    binaryMaster = new BinaryMaster();
                    binaryMaster.writer.Write(-1);
                    sendReturnAuto(binaryMaster.get());
                    break;
                }
                if (cancalable)
                {
                    gameInfo.addHashedButton("cancleSelected", -1, superButtonType.no, InterString.Get("取消选择@ui"));
                }
                realizeCardsForSelect();
                if (ES_selectHint != "")
                {
                    gameField.setHint(ES_selectHint + " " + ES_min.ToString() + "-" + ES_max.ToString());
                }
                else
                {
                    gameField.setHint(InterString.Get("请选择卡片。") + " " + ES_min.ToString() + "-" + ES_max.ToString());
                }
                break;
            case GameMessage.SelectUnselect:
                if (inIgnoranceReplay() || inTheWorld())
                {
                    break;
                }
                if (condition == Condition.record)
                {
                    Sleep(60);
                }
                destroy(waitObject, 0, false, true);
                player = localPlayer(r.ReadByte());
                bool finishable = (r.ReadByte() != 0);
                cancalable = (r.ReadByte() != 0) || finishable;
                ES_min = r.ReadByte();
                ES_max = r.ReadByte();
                ES_level = 0;
                count = r.ReadByte();
                for (int i = 0; i < count; i++)
                {
                    code = r.ReadInt32();
                    gps = r.ReadGPS();
                    card = GCS_cardGet(gps, false);
                    if (card != null)
                    {
                        card.set_code(code);
                        card.prefered = true;
                        card.forSelect = true;
                        card.selectPtr = i;
                        allCardsInSelectMessage.Add(card);
                    }
                }
                cardsSelected.Clear();
                int count2 = r.ReadByte();
                for (int i = count; i < count + count2; i++)
                {
                    code = r.ReadInt32();
                    gps = r.ReadGPS();
                    card = GCS_cardGet(gps, false);
                    if (card != null)
                    {
                        card.set_code(code);
                        card.prefered = true;
                        card.forSelect = true;
                        card.selectPtr = i;
                        allCardsInSelectMessage.Add(card);
                        cardsSelected.Add(card);
                    }
                }
                if (cancalable && !finishable)
                {
                    gameInfo.addHashedButton("cancleSelected", -1, superButtonType.no, InterString.Get("取消选择@ui"));
                }
                if (finishable)
                {
                    gameInfo.addHashedButton("sendSelected", 0, superButtonType.yes, InterString.Get("完成选择@ui"));
                }
                realizeCardsForSelect();
                cardsSelected.Clear();
                if (ES_selectHint != "")
                    ES_selectUnselectHint = ES_selectHint;
                if (ES_selectUnselectHint != "")
                {
                    gameField.setHint(ES_selectUnselectHint + " " + ES_min.ToString() + "-" + ES_max.ToString());
                }
                else
                {
                    gameField.setHint(InterString.Get("请选择卡片。") + " " + ES_min.ToString() + "-" + ES_max.ToString());
                }
                break;
            case GameMessage.SelectChain:
                if (inIgnoranceReplay() || inTheWorld())
                {
                    break;
                }
                destroy(waitObject, 0, false, true);
                player = localPlayer(r.ReadChar());
                count = r.ReadByte();
                int spcount = r.ReadByte();
                int hint0 = r.ReadInt32();
                int hint1 = r.ReadInt32();
                chainCards = new List<gameCard>();
                int forceCount = 0;
                for (int i = 0; i < count; i++)
                {
                    int flag = r.ReadChar();
                    int forced = r.ReadByte();
                    forceCount += forced;
                    code = r.ReadInt32() % 1000000000;
                    gps = r.ReadGPS();
                    desc = GameStringManager.get(r.ReadInt32());
                    card = GCS_cardGet(gps, false);
                    if (card != null)
                    {
                        chainCards.Add(card);
                        card.set_code(code);
                        card.prefered = true;
                        Effect eff = new Effect();
                        eff.flag = flag;
                        eff.ptr = i;
                        eff.desc = desc;
                        eff.forced = forced > 0;
                        card.effects.Add(eff);
                    }
                }
                var chain_condition = gameInfo.get_condition(); 
                int handle_flag = 0;
                if (forceCount == 0)
                {
                    //无强制发动的卡
                    if (spcount == 0)
                    {
                        //无关键卡
                        if (chain_condition == gameInfo.chainCondition.no)
                        {
                            //无关键卡 连锁被无视 直接回答---
                            handle_flag = 0;
                        }
                        else if (chain_condition == gameInfo.chainCondition.all)
                        {
                            //无关键卡但是连锁被监控
                            if (chainCards.Count == 0)
                            {
                                //欺骗--
                                handle_flag = -1;
                            }
                            else
                            {
                                if (chainCards.Count == 1 && chainCards[0].effects.Count == 1)
                                {
                                    //只有一张要处理的卡 常规处理 一张---
                                    handle_flag = 1;
                                }
                                else
                                {
                                    //常规处理 多张---
                                    handle_flag = 2;
                                }
                            }
                        }
                        else if (chain_condition == gameInfo.chainCondition.smart)
                        {
                            //无关键卡但是连锁被智能过滤
                            if (chainCards.Count == 0)
                            {
                                //根本没卡 直接回答---
                                handle_flag = 0;
                            }
                            else
                            {
                                if (chainCards.Count == 1 && chainCards[0].effects.Count == 1)
                                {
                                    //只有一张要处理的卡 常规处理 一张---
                                    handle_flag = 1;
                                }
                                else
                                {
                                    //常规处理 多张---
                                    handle_flag = 2;
                                }
                            }
                        }
                        else
                        {
                            //无关键卡而且连锁没有被监控    直接回答---
                            handle_flag = 0;
                        }
                    }
                    else
                    {
                        //有关键卡
                        if (chainCards.Count == 0)
                        {
                            //根本没卡 直接回答---
                            handle_flag = 0;
                            if (chain_condition == gameInfo.chainCondition.all)
                            {
                                //欺骗--
                                handle_flag = -1;
                            }
                        }
                        else if (chain_condition == gameInfo.chainCondition.no)
                        {
                            //有关键卡 连锁被无视 直接回答---
                            handle_flag = 0;
                        }
                        else
                        {
                            if (chainCards.Count == 1 && chainCards[0].effects.Count == 1)
                            {
                                //只有一张要处理的卡 常规处理 一张---
                                handle_flag = 1;
                            }
                            else
                            {
                                //常规处理 多张---
                                handle_flag = 2;
                            }
                        }
                    }
                }
                else
                {
                    if (chainCards.Count == 1 && chainCards[0].effects.Count == 1)
                    {
                        //有一张强制发动的卡 回应--
                        handle_flag = 4;
                    }
                    else
                    {
                        //有强制发动的卡 处理强制发动的卡--
                        handle_flag = 3;
                        if (autoForceChainHandler== autoForceChainHandlerType.autoHandleAll)
                        {
                            handle_flag = 4;
                        }
                        if (autoForceChainHandler == autoForceChainHandlerType.afterClickManDo)
                        {
                            handle_flag = 5;
                        }
                    }
                    if (UIHelper.fromStringToBool(Config.Get("autoChain_", "0")) == true)
                    {
                        //自动回应--
                        handle_flag = 4;
                    }
                }
                if (handle_flag == -1)
                {
                    //欺骗
                    RMSshow_onlyYes("return", InterString.Get("[?]，@n没有卡片可以连锁。", ES_hint), new messageSystemValue { hint = "yes", value = "-1" });
                    flagForCancleChain = true;
                    if (condition == Condition.record)
                    {
                        Sleep(60);
                    }
                }
                if (handle_flag == 0)
                {
                    //直接回答
                    //这是「没有卡可以连锁 / 连锁被无视」时客户端替玩家答的 -1，
                    //玩家根本没出手 —— 必须走 sendReturnAuto，否则会被记成一个人工决策点，
                    //撤回就退到这一格（画面看不出任何变化，像是没撤）。
                    binaryMaster = new BinaryMaster();
                    binaryMaster.writer.Write((Int32)(-1));
                    sendReturnAuto(binaryMaster.get());
                }
                if (handle_flag == 1)
                {
                    //处理一张   废除
                    handle_flag = 2;
                }
                if (handle_flag == 2)
                {
                    //处理多张
                    for (int i = 0; i < chainCards.Count; i++)
                    {
                        chainCards[i].add_one_decoration(Program.I().mod_ocgcore_decoration_chain_selecting, 4, Vector3.zero, "chain_selecting");
                        chainCards[i].forSelect = true;
                        chainCards[i].currentFlash = gameCard.flashType.Active;
                    }
                    flagForCancleChain = true;
                    RMSshow_yesOrNo("return", InterString.Get("[?]，@n是否连锁？", ES_hint), new messageSystemValue { value = "hide", hint = "yes" }, new messageSystemValue { value = "-1", hint = "no" });
                    gameInfo.addHashedButton("cancleChain", -1, superButtonType.no, InterString.Get("取消连锁@ui"));
                    if (condition == Condition.record)
                    {
                        Sleep(60);
                    }
                }
                if (handle_flag == 3)
                {
                    //处理强制发动的卡
                    for (int i = 0; i < chainCards.Count; i++)
                    {
                        chainCards[i].add_one_decoration(Program.I().mod_ocgcore_decoration_chain_selecting, 4, Vector3.zero, "chain_selecting");
                        chainCards[i].forSelect = true;
                        chainCards[i].currentFlash = gameCard.flashType.Active;
                    }
                    RMSshow_yesOrNo("autoForceChainHandler", InterString.Get("[?]，@n自动处理强制发动的卡？", ES_hint), new messageSystemValue { value = "yes", hint = "yes" }, new messageSystemValue { value = "no", hint = "no" });
                    if (condition == Condition.record)
                    {
                        Sleep(60);
                    }
                }
                if (handle_flag == 5)
                {
                    //处理强制发动的卡 AfterClick
                    for (int i = 0; i < chainCards.Count; i++)
                    {
                        chainCards[i].add_one_decoration(Program.I().mod_ocgcore_decoration_chain_selecting, 4, Vector3.zero, "chain_selecting");
                        chainCards[i].forSelect = true;
                        chainCards[i].currentFlash = gameCard.flashType.Active;
                    }
                }
                if (handle_flag == 4)
                {
                    //有一张强制发动的卡 回应--
                    int answer = -1;
                    foreach (var ccard in chainCards)
                    {
                        foreach (var effect in ccard.effects)
                        {
                            if (effect.forced)
                            {
                                answer = effect.ptr;
                                break;
                            }
                        }
                        if (answer >= 0) break;
                    }
                    binaryMaster = new BinaryMaster();
                    binaryMaster.writer.Write(answer >= 0 ? answer : 0);
                    sendReturnAuto(binaryMaster.get());
                }
                break;
            case GameMessage.SelectPosition:
                if (inIgnoranceReplay() || inTheWorld())
                {
                    break;
                }
                if (condition == Condition.record)
                {
                    Sleep(60);
                }
                destroy(waitObject, 0, false, true);
                player = localPlayer(r.ReadByte());
                code = r.ReadInt32();
                int positions = r.ReadByte();
                int op1 = 0x1;
                int op2 = 0x4;
                if (positions == 0x1 || positions == 0x2 || positions == 0x4 || positions == 0x8)
                {
                    //只有一种表示形式可选 —— 客户端替玩家答了，不算人工决策点。
                    binaryMaster = new BinaryMaster();
                    binaryMaster.writer.Write(positions);
                    sendReturnAuto(binaryMaster.get());
                }
                if (positions == (0x1 | 0x4 | 0x8))
                {
                    RMSshow_position3("return", code);
                }
                else
                {
                    if ((positions & 0x1) > 0)
                    {
                        op1 = 0x1;
                    }
                    if ((positions & 0x2) > 0)
                    {
                        op1 = 0x2;
                    }
                    if ((positions & 0x4) > 0)
                    {
                        op2 = 0x4;
                    }
                    if ((positions & 0x8) > 0)
                    {
                        if ((positions & 0x4) > 0)
                        {
                            op1 = 0x4;
                        }
                        op2 = 0x8;
                    }
                    RMSshow_position("return", code, new messageSystemValue { value = op1.ToString(), hint = "atk" }, new messageSystemValue { value = op2.ToString(), hint = "def" });
                }
                break;
            case GameMessage.SortCard:
            case GameMessage.SortChain:
                if (inIgnoranceReplay() || inTheWorld())
                {
                    break;
                }
                if (condition == Condition.record)
                {
                    Sleep(60);
                }
                destroy(waitObject, 0, false, true);
                player = localPlayer(r.ReadByte());
                ES_sortSum = 0;
                count = r.ReadByte();
                cardsInSort.Clear();
                ES_sortResult.Clear();
                for (int i = 0; i < count; i++)
                {
                    code = r.ReadInt32();
                    gps = r.ReadShortGPS();
                    card = GCS_cardGet(gps, false);
                    if (card != null)
                    {
                        card.set_code(code);
                        card.prefered = true;
                        card.forSelect = true;
                        card.isShowed = true;
                        card.sortOptions.Add(i);
                        cardsInSort.Remove(card);
                        cardsInSort.Add(card);
                        ES_sortSum++;
                        card.add_one_decoration(Program.I().mod_ocgcore_decoration_card_selecting, 2, Vector3.zero, "card_selecting");
                        card.currentFlash = gameCard.flashType.Select;
                    }
                }
                if (UIHelper.fromStringToBool(Config.Get("autoChain_", "0")) == true)
                {
                    if (currentMessage == GameMessage.SortChain)
                    {
                        bin = new BinaryMaster();
                        for (int i = 0; i < count; i++)
                        {
                            bin.writer.Write((byte)(i));
                        }
                        sendReturnAuto(bin.get());
                    }
                }
                realize();
                toNearest();
                if (currentMessage == GameMessage.SortCard)
                {
                    gameField.setHint(InterString.Get("请为卡片排序。"));
                }
                else
                {
                    gameField.setHint(InterString.Get("请为连锁手动排序。"));
                }
                break;
            case GameMessage.SelectCounter:
                if (inIgnoranceReplay() || inTheWorld())
                {
                    break;
                }
                bool Version1033b = (length_of_message - 5) % 8 == 0;
                if (condition == Condition.record)
                {
                    Sleep(60);
                }
                destroy(waitObject, 0, false, true);
                player = localPlayer(r.ReadByte());
                r.ReadInt16();
                if (Version1033b)   
                {
                    ES_min = r.ReadByte();
                }
                else
                {
                    ES_min = r.ReadUInt16();
                }
                count = r.ReadByte();
                for (int i = 0; i < count; i++)
                {
                    code = r.ReadInt32();
                    gps = r.ReadShortGPS();
                    card = GCS_cardGet(gps, false);
                    int pew = 0;
                    if (Version1033b)
                    {
                        pew = r.ReadByte();
                    }
                    else
                    {
                        pew = r.ReadUInt16();
                    }
                    if (card != null)
                    {
                        card.set_code(code);
                        card.prefered = true;
                        card.counterCANcount = pew;
                        card.counterSELcount = 0;
                        allCardsInSelectMessage.Add(card);
                        card.selectPtr = i;
                        card.forSelect = true;
                        card.add_one_decoration(Program.I().mod_ocgcore_decoration_card_selecting, 2, Vector3.zero, "card_selecting");
                        card.isShowed = true;
                        card.currentFlash = gameCard.flashType.Select;
                    }
                }
                if (gameInfo.queryHashedButton("clearCounter") == false)
                {
                    gameInfo.addHashedButton("clearCounter", 0, superButtonType.no, InterString.Get("重新选择@ui"));
                }
                realize();
                toNearest();
                gameField.setHint(InterString.Get("请移除[?]个指示物。", ES_min.ToString()));
                break;
            case GameMessage.SelectSum:
                if (inIgnoranceReplay() || inTheWorld())
                {
                    break;
                }
                if (condition == Condition.record)
                {
                    Sleep(60);
                }
                destroy(waitObject, 0, false, true);
                ES_overFlow = r.ReadByte() != 0;
                player = localPlayer(r.ReadByte());
                ES_level = r.ReadInt32();
                ES_min = r.ReadByte();
                ES_max = r.ReadByte();
                if (ES_min < 1)
                {
                    ES_min = 1;
                }
                if (ES_max < 1)
                {
                    ES_max = 99;
                }
                count = r.ReadByte();
                for (int i = 0; i < count; i++)
                {
                    code = r.ReadInt32();
                    gps = r.ReadShortGPS();
                    int para = r.ReadInt32();
                    card = GCS_cardGet(gps, false);
                    if (card != null)
                    {
                        card.set_code(code);
                        card.selectPtr = i;
                        card.levelForSelect_1 = para & 0xffff;
                        card.levelForSelect_2 = para >> 16;
                        if ((para & 0x80000000) > 0)
                        {
                            card.levelForSelect_1 = para & 0x7fffffff;
                            card.levelForSelect_2 = card.levelForSelect_1;
                        }
                        if (card.levelForSelect_2 == 0)
                        {
                            card.levelForSelect_2 = card.levelForSelect_1;
                        }
                        allCardsInSelectMessage.Add(card);
                        cardsMustBeSelected.Add(card);
                        card.forSelect = true;
                    }
                }
                count = r.ReadByte();
                for (int i = 0; i < count; i++)
                {
                    code = r.ReadInt32();
                    gps = r.ReadShortGPS();
                    int para = r.ReadInt32();
                    card = GCS_cardGet(gps, false);
                    if (card != null)
                    {
                        card.set_code(code);
                        card.prefered = true;
                        card.selectPtr = i;
                        card.levelForSelect_1 = para & 0xffff;
                        card.levelForSelect_2 = para >> 16;
                        if ((para & 0x80000000) > 0)
                        {
                            card.levelForSelect_1 = para & 0x7fffffff;
                            card.levelForSelect_2 = card.levelForSelect_1;
                        }
                        if (card.levelForSelect_2 == 0)
                        {
                            card.levelForSelect_2 = card.levelForSelect_1;
                        }
                        allCardsInSelectMessage.Add(card);
                        card.forSelect = true;
                    }
                }
                realizeCardsForSelect();
                gameField.setHint(ES_selectHint);
                break;
            case GameMessage.SelectPlace:
            case GameMessage.SelectDisfield:
                if (inIgnoranceReplay() || inTheWorld())
                {
                    break;
                }
                destroy(waitObject, 0, false, true);
                binaryMaster = new BinaryMaster();
                player = r.ReadByte();
                min = r.ReadByte();
                bool cancelable = false;
                if (min == 0)
                {
                    cancelable = true;
                    min = 1;
                }
                uint _field = ~r.ReadUInt32();
                if (Program.I().setting.setting.hand.value == true || Program.I().setting.setting.handm.value == true || currentMessage == GameMessage.SelectDisfield)
                {
                    ES_min = min;
                    for (int i = 0; i < min; i++)
                    {
                        byte[] resp = new byte[3];
                        uint filter;

                        for (int j = 0; j < 2; j++)
                        {
                            resp = new byte[3];
                            filter = 0;
                            uint field;

                            if (j == 0)
                            {
                                resp[0] = (byte)player;
                                field = _field & 0xffff;
                            }
                            else
                            {
                                resp[0] = (byte)(1 - player);
                                field = _field >> 16;
                            }

                            if ((field & 0x7f) != 0)
                            {
                                resp[1] = (byte)CardLocation.MonsterZone;
                                filter = field & 0x7f;
                                for (int k = 0; k < 7; k++)
                                {
                                    if ((filter & (1u << k)) != 0)
                                    {
                                        resp[2] = (byte)k;
                                        createPlaceSelector(resp);
                                    }
                                }
                            }
                            if ((field & 0x1f00) != 0)
                            {
                                resp[1] = (byte)CardLocation.SpellZone;
                                filter = (field >> 8) & 0x1f;
                                for (int k = 0; k < 5; k++)
                                {
                                    if ((filter & (1u << k)) != 0)
                                    {
                                        resp[2] = (byte)k;
                                        createPlaceSelector(resp);
                                    }
                                }
                            }
                            if ((field & 0x2000) != 0)
                            {
                                resp[1] = (byte)CardLocation.SpellZone;
                                filter = (field >> 8) & 0x20;
                                resp[2] = 5;
                                createPlaceSelector(resp);
                            }
                            if ((field & 0xc000) != 0)
                            {
                                resp[1] = (byte)CardLocation.SpellZone;
                                filter = (field >> 14) & 0x3;
                                if ((filter & 0x2) != 0)
                                {
                                    resp[2] = 7;
                                    createPlaceSelector(resp);
                                }
                                if ((filter & 0x1) != 0)
                                {
                                    resp[2] = 6;
                                    createPlaceSelector(resp);
                                }
                            }
                        }
                    }

                    if (currentMessage == GameMessage.SelectPlace)
                    {
                        if (Es_selectMSGHintType == 3)
                        {
                            if (Es_selectMSGHintPlayer == 0)
                            {
                                gameField.setHint(InterString.Get("请为我方的「[?]」选择位置。", YGOSharp.CardsManager.Get(Es_selectMSGHintData).Name));
                            }
                            else
                            {
                                gameField.setHint(InterString.Get("请为对方的「[?]」选择位置。", YGOSharp.CardsManager.Get(Es_selectMSGHintData).Name));
                            }
                        }
                    }
                    else
                    {
                        if (ES_selectHint != "")
                        {
                            gameField.setHint(ES_selectHint);
                        }
                        else
                        {
                            gameField.setHint(GameStringManager.get_unsafe(570));
                        }
                    }
                    if (cancelable)
                    {
                        gameInfo.addHashedButton("cancelPlace", -1, superButtonType.no, InterString.Get("取消操作@ui"));
                    }
                }
                else
                {
                    uint field = _field;
                    for (int i = 0; i < min; i++)
                    {
                        byte[] resp = new byte[3];
                        bool pendulumZone = false;
                        uint filter;

                        if ((field & 0x7f0000) != 0)
                        {
                            resp[0] = (byte)(1 - player);
                            resp[1] = (byte)CardLocation.MonsterZone;
                            filter = (field >> 16) & 0x7f;
                        }
                        else if ((field & 0x1f000000) != 0)
                        {
                            resp[0] = (byte)(1 - player);
                            resp[1] = (byte)CardLocation.SpellZone;
                            filter = (field >> 24) & 0x1f;
                        }
                        else if ((field & 0xc0000000) != 0)
                        {
                            resp[0] = (byte)(1 - player);
                            resp[1] = (byte)CardLocation.SpellZone;
                            filter = (field >> 30) & 0x3;
                            pendulumZone = true;
                        }
                        else if ((field & 0x7f) != 0)
                        {
                            resp[0] = (byte)player;
                            resp[1] = (byte)CardLocation.MonsterZone;
                            filter = field & 0x7f;
                        }
                        else if ((field & 0x1f00) != 0)
                        {
                            resp[0] = (byte)player;
                            resp[1] = (byte)CardLocation.SpellZone;
                            filter = (field >> 8) & 0x1f;
                        }
                        else if ((field & 0x2000) != 0)
                        {
                            resp[0] = (byte)player;
                            resp[1] = (byte)CardLocation.SpellZone;
                            filter = (field >> 8) & 0x20;
                        }
                        else
                        {
                            resp[0] = (byte)player;
                            resp[1] = (byte)CardLocation.SpellZone;
                            filter = (field >> 14) & 0x3;
                            pendulumZone = true;
                        }

                        if (!pendulumZone)
                        {
                            if ((filter & 0x4) != 0) resp[2] = 2;
                            else if ((filter & 0x2) != 0) resp[2] = 1;
                            else if ((filter & 0x8) != 0) resp[2] = 3;
                            else if ((filter & 0x1) != 0) resp[2] = 0;
                            else if ((filter & 0x10) != 0) resp[2] = 4;
                            else
                            {
                                if (resp[1] == (byte)CardLocation.MonsterZone)
                                {
                                    if ((filter & 0x20) != 0) resp[2] = 5;
                                    else if ((filter & 0x40) != 0) resp[2] = 6;
                                }
                                else
                                {
                                    if ((filter & 0x20) != 0) resp[2] = 5;
                                }
                            }
                        }
                        else
                        {
                            if ((filter & 0x2) != 0) resp[2] = 7;
                            if ((filter & 0x1) != 0) resp[2] = 6;
                        }
                        binaryMaster.writer.Write(resp);
                    }
                    //位置被服务端定死了（只有一个候选），客户端替玩家答的 —— 不算人工决策点。
                    sendReturnAuto(binaryMaster.get());
                }
                break;
            case GameMessage.RockPaperScissors:
                if (inIgnoranceReplay() || inTheWorld())
                {
                    break;
                }
                if (condition == Condition.record)
                {
                    Sleep(60);
                }
                destroy(waitObject, 0, false, true);
                player = localPlayer(r.ReadByte());
                RMSshow_tp("RockPaperScissors"
                    , new messageSystemValue { hint = "jiandao", value = "1" }
                    , new messageSystemValue { hint = "shitou", value = "2" }
                    , new messageSystemValue { hint = "bu", value = "3" });
                break;
            case GameMessage.ConfirmDecktop:
                player = localPlayer(r.ReadByte());
                count = r.ReadByte();
                int countOfDeck = countLocation(player, CardLocation.Deck);
                for (int i = 0; i < count; i++)
                {
                    code = r.ReadInt32();
                    gps = r.ReadShortGPS();
                    gps = new GPS
                    {
                        controller = (UInt32)player,
                        location = (UInt32)CardLocation.Deck,
                        sequence = (UInt32)(countOfDeck - 1 - i),
                    };
                    card = GCS_cardGet(gps, false);
                    if (card != null)
                    {
                        confirm(card);
                    }
                }
                Sleep(count * 40);
                break;
            case GameMessage.ConfirmCards:
                player = localPlayer(r.ReadByte());
                bool skip_panel = r.ReadByte() == 1;
                count = r.ReadByte();
                int t2 = 0;
                int t3 = 0;
                bool pan_mode = false;
                for (int i = 0; i < count; i++)
                {
                    code = r.ReadInt32();
                    gps = r.ReadShortGPS();
                    card = GCS_cardGet(gps, false);
                    bool showC = false;
                    if (gps.controller!=0)  
                    {
                        showC = true;
                    }
                    else
                    {
                        if (gps.location != (int)CardLocation.Hand)   
                        {
                            showC = true;
                        }
                        if (Program.I().room.mode == 2) 
                        {
                            showC = true;
                        }
                        if (condition != Condition.duel)
                        {
                            if (InAI == false)  
                            {
                                showC = true;
                            }
                        }
                    }
                    if (showC)  
                    {
                        if (card != null)
                        {
                            if (
                                (card.p.location & (UInt32)CardLocation.Deck) > 0
                                ||
                                (card.p.location & (UInt32)CardLocation.Grave) > 0
                                ||
                                (card.p.location & (UInt32)CardLocation.Extra) > 0
                                ||
                                (card.p.location & (UInt32)CardLocation.Removed) > 0
                                )
                            {
                                card.currentKuang = gameCard.kuangType.selected;
                                cardsInSelectAnimation.Add(card);
                                card.isShowed = true;
                                pan_mode = true;
                                if (condition != Condition.record)
                                {
                                    t2 += 100000;
                                    clearTimeFlag = true;
                                }
                                t3++;
                            }
                            else if (card.condition != gameCardCondition.verticle_clickable)
                            {
                                if ((card.p.location & (UInt32)CardLocation.Hand) > 0)
                                {
                                    if (i==0)   
                                    {
                                        confirm(card);
                                        t2 += 50;
                                    }
                                    else
                                    {
                                        Nconfirm();
                                        t2 = 50;
                                    }
                                }
                                else
                                {
                                    confirm(card);
                                    t2 += 50;
                                }
                            }
                            else
                            {
                                card.currentKuang = gameCard.kuangType.selected;
                                cardsInSelectAnimation.Add(card);
                            }
                        }
                    }
                }
                realize();
                toNearest();
                if (pan_mode)
                {
                    clearAllShowedB = true;
                    flagForTimeConfirm = true;
                    gameField.setHint(InterString.Get("请确认[?]张卡片。", t3.ToString()));
                    if (inIgnoranceReplay()||inTheWorld())
                    {
                        t2 = 0;
                        clearResponse();
                    }
                    else if (skip_panel)
                    {
                        Sleep(t2);
                        t2 = 0;
                        clearResponse();
                    }
                }
                Sleep(t2);
                break;
            case GameMessage.RefreshDeck:
            case GameMessage.ShuffleDeck:
                UIHelper.playSound("shuffle", 1f);
                player = localPlayer(r.ReadByte());
                for (int i = 0; i < cards.Count; i++) if (cards[i].gameObject.activeInHierarchy)
                    {
                        if ((cards[i].p.location & (UInt32)CardLocation.Deck) > 0)
                        {
                            if (cards[i].p.controller == player)
                            {
                                if (i % 2 == 0) cards[i].animation_shake_to(1.2f);
                            }
                        }
                    }
                Sleep(30);
                break;
            case GameMessage.ShuffleHand:
                realize();
                UIHelper.playSound("shuffle", 1f);
                player = localPlayer(r.ReadByte());
                animation_suffleHand(player);
                Sleep(21);
                break;
            case GameMessage.SwapGraveDeck:
                realize();
                Sleep(120);
                break;
            case GameMessage.ShuffleSetCard:
                UIHelper.playSound("shuffle", 1f);
                count = r.ReadByte();
                List<GPS> gpss = new List<GPS>();
                for (int i = 0; i < count; i++)
                {
                    gps = r.ReadGPS();
                    card = GCS_cardGet(gps, false);
                    if (card != null)
                    {
                        Vector3 position = Vector3.zero;
                        if (card.p.controller == 1)
                        {
                            card.animation_confirm(new Vector3(0, 5, 5), new Vector3(0, 90, 180), 0.2f, 0.01f);
                        }
                        else
                        {
                            card.animation_confirm(new Vector3(0, 5, -5), new Vector3(0, -90, 180), 0.2f, 0.01f);
                        }
                    }
                }
                Sleep(30);
                break;
            case GameMessage.ReverseDeck:
                break;
            case GameMessage.DeckTop:
                break;
            case GameMessage.NewTurn:
                removeSelectedAnimations();
                player = localPlayer(r.ReadByte());
                if (condition != Condition.duel)
                {
                    gameInfo.setTimeStill(player);
                }
                //else
                //{
                //    gameInfo.setTime(player, timeLimit);
                //}
                toDefaultHint();
                UIHelper.playSound("nextturn", 1f);
                gameField.animation_show_big_string(GameTextureManager.nt);
                //if (player == 1 && InAI == true)
                //{
                //    showWait();
                //}
                gameInfo.setExcited((turns % 2 == (isFirst ? 0 : 1)) ? 1 : 0);
                break;
            case GameMessage.NewPhase:
                removeSelectedAnimations();
                toDefaultHint();
                UIHelper.playSound("phase", 1f);
                int phrase = r.ReadUInt16();
                if (GameStringHelper.differ(phrase, (long)DuelPhase.BattleStart))
                {
                    gameField.animation_show_big_string(GameTextureManager.bp);
                }
                if (GameStringHelper.differ(phrase, (long)DuelPhase.Draw))
                {
                    gameField.animation_show_big_string(GameTextureManager.dp);
                }
                if (GameStringHelper.differ(phrase, (long)DuelPhase.End))
                {
                    gameField.animation_show_big_string(GameTextureManager.ep);
                }
                if (GameStringHelper.differ(phrase, (long)DuelPhase.Main1))
                {
                    gameField.animation_show_big_string(GameTextureManager.mp1);
                }
                if (GameStringHelper.differ(phrase, (long)DuelPhase.Main2))
                {
                    gameField.animation_show_big_string(GameTextureManager.mp2);
                }
                if (GameStringHelper.differ(phrase, (long)DuelPhase.Standby))
                {
                    gameField.animation_show_big_string(GameTextureManager.sp);
                }
                gameField.realize();
                break;
            case GameMessage.Move:
                realize();
                code = r.ReadInt32();
                GPS from = r.ReadGPS();
                GPS to = r.ReadGPS();
                card = GCS_cardGet(to, false);
                if ((to.location == ((UInt32)CardLocation.Overlay | (UInt32)CardLocation.Extra)) && ((from.location & (UInt32)CardLocation.Overlay) == 0) && Program.I().setting.setting.Vxyz.value == true)
                {
                    Vector3 vDarkHole = Vector3.zero;
                    float real = (Program.fieldSize - 1) * 0.9f + 1f;
                    if (to.controller == 0)
                    {
                        vDarkHole = new Vector3(0, 0, -7f * real);
                    }
                    if (to.controller == 1)
                    {
                        vDarkHole = new Vector3(0, 0, 7f * real);
                    }
                    gameField.shiftBlackHole(1, vDarkHole);
                }
                else
                {
                    gameField.shiftBlackHole(-1);
                }
                if (card != null)
                {
                    if ((to.position & (int)CardPosition.FaceDown) > 0)
                    {
                        if (to.location == (UInt32)CardLocation.MonsterZone || to.location == (UInt32)CardLocation.SpellZone)
                        {
                            if (Program.I().setting.setting.Vset.value == true)
                                card.positionEffect(Program.I().mod_ocgcore_decoration_card_setted);
                            UIHelper.playSound("set", 1f);
                        }
                    }
                    if (to.location == (UInt32)CardLocation.Grave)
                    {
                        if ((from.location & (UInt32)CardLocation.MonsterZone) > 0) UIHelper.playSound("destroyed", 1f);
                        if (Program.I().setting.setting.Vmove.value == true)
                            MonoBehaviour.Destroy((GameObject)MonoBehaviour.Instantiate(Program.I().mod_ocgcore_decoration_tograve, card.gameObject.transform.position, Quaternion.identity), 5f);
                    }
                    if (to.location == (UInt32)CardLocation.Removed)
                    {
                        UIHelper.playSound("destroyed", 1f);
                        if (Program.I().setting.setting.Vmove.value == true)
                            card.fast_decoration(Program.I().mod_ocgcore_decoration_removed);
                    }
                }
                break;
            case GameMessage.PosChange:
                realize();
                break;
            case GameMessage.Set:
                break;
            case GameMessage.Swap:
                realize();
                break;
            case GameMessage.FieldDisabled:
                realize();
                break;
            case GameMessage.Summoning:
                code = r.ReadInt32();
                gps = r.ReadGPS();
                card = GCS_cardGet(gps, false);
                removeSelectedAnimations();
                if (card != null)
                {
                    card.set_code(code);
                    UIHelper.playSound("summon", 1f);
                    if (Program.I().setting.setting.Vsum.value == true)
                    {
                        GameObject mod = Program.I().mod_ocgcore_ss_spsummon_normal;
                        card.animationEffect(mod);
                    }
                    card.animation_show_off( true);
                }
                break;
            case GameMessage.Summoned:
                break;
            case GameMessage.SpSummoning:
                code = r.ReadInt32();
                gps = r.ReadGPS();
                removeSelectedAnimations();
                gameField.shiftBlackHole(false, get_point_worldposition(gps));
                card = GCS_cardGet(gps, false);
                if (card != null)
                {
                    card.set_code(code);
                    if (Program.I().setting.setting.Vspsum.value==true)
                    {
                        GameObject mod = Program.I().mod_ocgcore_ss_summon_light;
                        if (GameStringHelper.differ(card.get_data().Attribute, (long)CardAttribute.Earth))
                            mod = Program.I().mod_ocgcore_ss_summon_earth;
                        if (GameStringHelper.differ(card.get_data().Attribute, (long)CardAttribute.Dark))
                            mod = Program.I().mod_ocgcore_ss_summon_dark;
                        if (GameStringHelper.differ(card.get_data().Attribute, (long)CardAttribute.Divine))
                            mod = Program.I().mod_ocgcore_ss_summon_light;
                        if (GameStringHelper.differ(card.get_data().Attribute, (long)CardAttribute.Fire))
                            mod = Program.I().mod_ocgcore_ss_summon_fire;
                        if (GameStringHelper.differ(card.get_data().Attribute, (long)CardAttribute.Light))
                            mod = Program.I().mod_ocgcore_ss_summon_light;
                        if (GameStringHelper.differ(card.get_data().Attribute, (long)CardAttribute.Water))
                            mod = Program.I().mod_ocgcore_ss_summon_water;
                        if (GameStringHelper.differ(card.get_data().Attribute, (long)CardAttribute.Wind))
                            mod = Program.I().mod_ocgcore_ss_summon_wind;
                        if (GameStringHelper.differ(card.get_data().Type, (long)CardType.Fusion))
                        {
                            if (Program.I().setting.setting.Vfusion.value == true)
                            {
                                mod = Program.I().mod_ocgcore_ss_spsummon_ronghe;
                            }
                            UIHelper.playSound("specialsummon2", 1f);
                        }
                        else if (GameStringHelper.differ(card.get_data().Type, (long)CardType.Synchro))
                        {
                            if (Program.I().setting.setting.Vsync.value == true)
                            {
                                mod = Program.I().mod_ocgcore_ss_spsummon_tongtiao;
                            }
                            UIHelper.playSound("specialsummon2", 1f);

                        }
                        else if (GameStringHelper.differ(card.get_data().Type, (long)CardType.Ritual))
                        {
                            if (Program.I().setting.setting.Vrution.value == true)
                            {
                                mod = Program.I().mod_ocgcore_ss_spsummon_yishi;
                            }
                            UIHelper.playSound("specialsummon2", 1f);
                        }
                        else if (GameStringHelper.differ(card.get_data().Type, (long)CardType.Link))
                        {
                            if (Program.I().setting.setting.Vlink.value == true)
                            {
                                float sc = Mathf.Clamp(card.get_data().Attack, 0, 3500) / 3000f;
                                Program.I().mod_ocgcore_ss_spsummon_link.GetComponent<partical_scaler>().scale = sc * 4f;
                                Program.I().mod_ocgcore_ss_spsummon_link.transform.localScale = Vector3.one * (sc * 4f);
                                card.animationEffect(Program.I().mod_ocgcore_ss_spsummon_link);
                                mod.GetComponent<partical_scaler>().scale = Mathf.Clamp(card.get_data().Attack, 0, 3500) / 3000f * 3f;
                            }
                            UIHelper.playSound("specialsummon2", 1f);
                        }
                        else
                        {
                            UIHelper.playSound("specialsummon", 1f);
                            mod.GetComponent<partical_scaler>().scale = Mathf.Clamp(card.get_data().Attack, 0, 3500) / 3000f * 3f;
                        }
                        card.animationEffect(mod);
                    }
                    else
                    {
                        if (GameStringHelper.differ(card.get_data().Type, (long)CardType.Fusion))
                        {
                            UIHelper.playSound("specialsummon2", 1f);
                        }
                        else if (GameStringHelper.differ(card.get_data().Type, (long)CardType.Synchro))
                        {
                            UIHelper.playSound("specialsummon2", 1f);
                        }
                        else if (GameStringHelper.differ(card.get_data().Type, (long)CardType.Ritual))
                        {
                            UIHelper.playSound("specialsummon2", 1f);
                        }
                        else
                        {
                            UIHelper.playSound("specialsummon", 1f);
                        }
                    }
                    card.animation_show_off( true);
                }
                break;
            case GameMessage.SpSummoned:
                break;
            case GameMessage.FlipSummoning:
                realize();
                removeSelectedAnimations();
                code = r.ReadInt32();
                gps = r.ReadGPS();
                card = GCS_cardGet(gps, false);
                if (card != null)
                {
                    card.set_code(code);
                    UIHelper.playSound("summon", 1f);
                    if (Program.I().setting.setting.Vflip.value == true)
                    {
                        GameObject mod = Program.I().mod_ocgcore_ss_spsummon_normal;
                        card.animationEffect(mod);
                    }
                    card.animation_show_off( true);
                }
                break;
            case GameMessage.FlipSummoned:
                break;
            case GameMessage.Chaining:
                //removeAttackHandler();
                code = r.ReadInt32();
                gps = r.ReadGPS();
                card = GCS_cardGet(gps, false);
                if (card != null)
                {
                    card.set_code(code);
                    UIHelper.playSound("activate", 1);
                    card.animation_show_off( false);
                    if ((card.get_data().Type & (int)CardType.Monster) > 0)
                    {
                        if (Program.I().setting.setting.Vactm.value == true)
                        {
                            GameObject mod = Program.I().mod_ocgcore_cs_mon_light;
                            if ((card.get_data().Attribute & (int)CardAttribute.Earth) > 0)
                            {
                                mod = Program.I().mod_ocgcore_cs_mon_earth;
                            }
                            if ((card.get_data().Attribute & (int)CardAttribute.Water) > 0)
                            {
                                mod = Program.I().mod_ocgcore_cs_mon_water;
                            }
                            if ((card.get_data().Attribute & (int)CardAttribute.Fire) > 0)
                            {
                                mod = Program.I().mod_ocgcore_cs_mon_fire;
                            }
                            if ((card.get_data().Attribute & (int)CardAttribute.Wind) > 0)
                            {
                                mod = Program.I().mod_ocgcore_cs_mon_wind;
                            }
                            if ((card.get_data().Attribute & (int)CardAttribute.Light) > 0)
                            {
                                mod = Program.I().mod_ocgcore_cs_mon_light;
                            }
                            if ((card.get_data().Attribute & (int)CardAttribute.Dark) > 0)
                            {
                                mod = Program.I().mod_ocgcore_cs_mon_dark;
                            }
                            mod.GetComponent<partical_scaler>().scale = 2f + Mathf.Clamp(card.get_data().Attack,0,3500) / 3000f * 5f;
                            card.fast_decoration(mod);
                        }
                    }
                    if ((card.get_data().Type & (int)CardType.Spell) > 0)
                    {
                        if (Program.I().setting.setting.Vacts.value == true)
                        {
                            card.positionEffect(Program.I().mod_ocgcore_decoration_magic_activated);
                        }
                    }
                    if ((card.get_data().Type & (int)CardType.Trap) > 0)
                    {
                        if (Program.I().setting.setting.Vactt.value == true)
                        {
                            card.positionShot(Program.I().mod_ocgcore_decoration_trap_activated);
                        }
                    }
                }
                realize();
                break;
            case GameMessage.Chained:
                Sleep(20);
                break;
            case GameMessage.ChainSolved:
                int id = r.ReadByte() - 1   ;
                if (id < 0)
                {
                    id = 0;
                }
                card = null;
                if (id < cardsInChain.Count)
                {
                    card = cardsInChain[id];
                    if (id >= 1)
                    {
                        if (Program.I().setting.setting.Vchain.value == true)
                        {
                            MonoBehaviour.Destroy((GameObject)MonoBehaviour.Instantiate(Program.I().mod_ocgcore_cs_bomb, card.gameObject.transform.position, Quaternion.identity), 5f);
                        }
                    }
                }
                if (card != null)
                {
                    if (card.isShowed == true)
                    {
                        card.isShowed = false;
                        realize();
                        toNearest(true);
                    }
                }
                Sleep(17);
                break;
            case GameMessage.ChainEnd:
                clearChainEnd();
                break;
            case GameMessage.ChainNegated:
            case GameMessage.ChainDisabled:
                int id_ = r.ReadByte() - 1;
                if (id_ < 0)
                {
                    id_ = 0;
                }
                card = null;
                if (id_ < cardsInChain.Count)
                {
                    card = cardsInChain[id_];
                    if (Program.I().setting.setting.Vchain.value == true)
                    {
                        card.fast_decoration(Program.I().mod_ocgcore_cs_negated);
                        Sleep(30);
                    }
                    card.animation_show_off(false, true);
                }
                if (card != null)
                {
                    if (card.isShowed == true)
                    {
                        card.isShowed = false;
                        realize();
                        toNearest(true);
                    }
                }
                break;
            case GameMessage.CardSelected:
                break;
            case GameMessage.RandomSelected:
                pIN = false;
                psum = false;
                player = localPlayer(r.ReadByte());
                count = r.ReadByte();
                for (int i = 0; i < count; i++)
                {
                    gps = r.ReadGPS();
                    card = GCS_cardGet(gps, false);
                    if (card != null)
                    {
                        if (card.p.location == (UInt32)CardLocation.SpellZone)
                        {
                            if (card.p.sequence == 6 || card.p.sequence == 7)
                            {
                                pIN = true;
                            }
                        }
                        cardsInSelectAnimation.Add(card);
                        card.currentKuang = gameCard.kuangType.selected;
                        if (Program.I().setting.setting.Vchain.value == true)
                            card.add_one_decoration(Program.I().mod_ocgcore_decoration_card_selected, 3, Vector3.zero, "selected", false);
                        if (Program.I().setting.setting.Vpedium.value == true)
                        {
                            Vector3 pvector = Vector3.zero;
                            if (cardsInChain.Count == 0)
                            {
                                if (cardsInSelectAnimation.Count == 2)
                                {
                                    if (cardsInSelectAnimation[0].p.location == (UInt32)CardLocation.SpellZone)
                                    {
                                        if (cardsInSelectAnimation[1].p.location == (UInt32)CardLocation.SpellZone)
                                        {
                                            if (cardsInSelectAnimation[1].p.sequence == 6 || cardsInSelectAnimation[1].p.sequence == 7)
                                            {
                                                if (cardsInSelectAnimation[0].p.sequence == 6 || cardsInSelectAnimation[0].p.sequence == 7)
                                                {
                                                    if (cardsInSelectAnimation[0].p.controller == cardsInSelectAnimation[0].p.controller)
                                                    {
                                                        psum = true;
                                                        if (cardsInSelectAnimation[0].p.controller == 0)
                                                        {
                                                            pvector = new Vector3(0, 0, -9f);
                                                        }
                                                        else
                                                        {
                                                            pvector = new Vector3(0, 0, 9f);
                                                        }
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                            if (psum)
                            {
                                float real = (Program.fieldSize - 1) * 0.9f + 1f;
                                Program.I().mod_ocgcore_ss_p_sum_effect.transform.Find("l").localPosition = new Vector3(-15.2f * real, 0, 0);
                                Program.I().mod_ocgcore_ss_p_sum_effect.transform.Find("r").localPosition = new Vector3(14.65f * real, 0, 0);
                                MonoBehaviour.Destroy((GameObject)MonoBehaviour.Instantiate(Program.I().mod_ocgcore_ss_p_sum_effect, pvector, Quaternion.identity), 5f);
                            }
                        }
                    }
                }
                if (!pIN)  
                {
                    Sleep(30);
                }
                break;
            case GameMessage.BecomeTarget:
                int targetTime = 0;
                psum = false;
                count = r.ReadByte();
                for (int i = 0; i < count; i++)
                {
                    gps = r.ReadGPS();
                    card = GCS_cardGet(gps, false);
                    if (card != null)
                    {
                        if ((card.p.location == (UInt32)CardLocation.SpellZone) && (card.p.sequence == 6 || card.p.sequence == 7))
                        {
                            targetTime += 0;
                        }
                        else if ((card.p.location & (UInt32)CardLocation.Onfield) > 0)
                        {
                            targetTime += 30;
                        }
                        else
                        {
                            targetTime += 50;
                        }
                        cardsInSelectAnimation.Add(card);
                        card.currentKuang = gameCard.kuangType.selected;
                        if (Program.I().setting.setting.Vchain.value == true)
                            card.add_one_decoration(Program.I().mod_ocgcore_decoration_card_selected, 3, Vector3.zero, "selected", false);
                        if (Program.I().setting.setting.Vpedium.value == true)
                        {
                            Vector3 pvector = Vector3.zero;
                            if (cardsInChain.Count == 0)
                            {
                                if (cardsInSelectAnimation.Count == 2)
                                {
                                    if (cardsInSelectAnimation[0].p.location == (UInt32)CardLocation.SpellZone)
                                    {
                                        if (cardsInSelectAnimation[1].p.location == (UInt32)CardLocation.SpellZone)
                                        {
                                            if (cardsInSelectAnimation[1].p.sequence == 6 || cardsInSelectAnimation[1].p.sequence == 7)
                                            {
                                                if (cardsInSelectAnimation[0].p.sequence == 6 || cardsInSelectAnimation[0].p.sequence == 7)
                                                {
                                                    if (cardsInSelectAnimation[0].p.controller == cardsInSelectAnimation[0].p.controller)
                                                    {
                                                        psum = true;
                                                        if (cardsInSelectAnimation[0].p.controller == 0)
                                                        {
                                                            pvector = new Vector3(0, 0, -9f);
                                                        }
                                                        else
                                                        {
                                                            pvector = new Vector3(0, 0, 9f);
                                                        }
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                            if (psum)
                            {
                                float real = (Program.fieldSize - 1) * 0.9f + 1f;
                                Program.I().mod_ocgcore_ss_p_sum_effect.transform.Find("l").localPosition = new Vector3(-15.2f * real, 0, 0);
                                Program.I().mod_ocgcore_ss_p_sum_effect.transform.Find("r").localPosition = new Vector3(14.65f * real, 0, 0);
                                MonoBehaviour.Destroy((GameObject)MonoBehaviour.Instantiate(Program.I().mod_ocgcore_ss_p_sum_effect, pvector, Quaternion.identity), 5f);
                            }

                        }
                    }
                }
                Sleep(targetTime);
                break;
            case GameMessage.Draw:
                UIHelper.playSound("draw", 1);
                realize();
                Sleep(10);
                break;
            case GameMessage.PayLpCost:
            case GameMessage.Damage:
                gameInfo.realize();
                player = localPlayer(r.ReadByte());
                val = r.ReadInt32();
                UIHelper.playSound("damage", 1f);
                gameField.animation_show_lp_num(player, false, (int)val);
                if (Program.I().setting.setting.Vdamage.value == true)
                {
                    gameField.animation_screen_blood(player, (int)val);
                }
                Sleep(60);
                break;
            case GameMessage.Recover:
                gameInfo.realize();
                player = localPlayer(r.ReadByte());
                val = r.ReadInt32();
                UIHelper.playSound("gainlp", 1f);
                gameField.animation_show_lp_num(player, true, (int)val);
                Sleep(60);
                break;
            case GameMessage.CardTarget:
            case GameMessage.Equip:
                realize();
                from = r.ReadGPS();
                to = r.ReadGPS();
                gameCard card_from = GCS_cardGet(from, false);
                gameCard card_to = GCS_cardGet(to, false);
                if (card_from != null)
                {
                    UIHelper.playSound("equip", 1f);
                    if (Program.I().setting.setting.Veqquip.value == true)
                    {
                        card_from.fast_decoration(Program.I().mod_ocgcore_decoration_magic_zhuangbei);
                    }
                }
                break;
            case GameMessage.LpUpdate:
                gameInfo.realize();
                break;
            case GameMessage.CancelTarget:
            case GameMessage.Unequip:
                realize();
                break;

            case GameMessage.AddCounter:
                cctype = r.ReadUInt16();
                gps = r.ReadShortGPS();
                card = GCS_cardGet(gps, false);
                count = r.ReadUInt16();
                string name2 = GameStringManager.get("counter", cctype);

                if (card != null)
                {
                    for (int i = 0; i < count; i++)
                    {
                        UIHelper.playSound("addcounter", 1);
                        //if (Program.YGOPro1 == false)
                        {
                            Vector3 pos = UIHelper.get_close(card.gameObject.transform.position, Program.camera_game_main, 5);
                            MonoBehaviour.Destroy((GameObject)MonoBehaviour.Instantiate(Program.I().mod_ocgcore_cs_end, pos, Quaternion.identity), 5f);
                        }
                    }
                }
                RMSshow_none(card.get_data().Name + "  " + InterString.Get("增加指示物：[?]", name2)+" *"+count.ToString());
                Sleep(10);
                break;
            case GameMessage.RemoveCounter:
                cctype = r.ReadUInt16();
                gps = r.ReadShortGPS();
                card = GCS_cardGet(gps, false);
                count = r.ReadUInt16();
                string name = GameStringManager.get("counter", cctype);
                if (card != null)
                {
                    for (int i = 0; i < count; i++)
                    {
                        UIHelper.playSound("removecounter", 1);
                        //if (Program.YGOPro1 == false)
                        {
                            Vector3 pos = UIHelper.get_close(card.gameObject.transform.position, Program.camera_game_main, 5);
                            MonoBehaviour.Destroy((GameObject)MonoBehaviour.Instantiate(Program.I().mod_ocgcore_cs_end, pos, Quaternion.identity), 5f);
                        }
                    }
                }
                RMSshow_none(card.get_data().Name + "  " + InterString.Get("减少指示物：[?]", name) + " *" + count.ToString());
                Sleep(10);
                break;
            case GameMessage.Attack:
                UIHelper.playSound("attack", 1);
                GPS p1 = r.ReadGPS();
                GPS p2 = r.ReadGPS();
                VectorAttackCard = get_point_worldposition(p1);
                VectorAttackTarget = Vector3.zero;
                if (p2.location == 0)
                {
                    bool attacker_bool_me = (p1.controller == 0);
                    if (!attacker_bool_me)
                    {
                        VectorAttackTarget = new Vector3(0, 3, -5f - 15f * Program.fieldSize);
                    }
                    else
                    {
                        if (gameField.isLong)
                        {
                            VectorAttackTarget = new Vector3(0, 3, 2f + (19f + gameField.delat) * Program.fieldSize);
                        }
                        else
                        {
                            VectorAttackTarget = new Vector3(0, 3, 2f + (19f) * Program.fieldSize);
                        }
                    }
                }
                else
                {
                    VectorAttackTarget = get_point_worldposition(p2);
                }
                Arrow.speed = 10;
                Arrow.updateSpeed();
                Sleep(40);



                //shiftArrow(VectorAttackCard, VectorAttackTarget, true, 50);
                //Program.notGo(removeAttackHandler);
                //Program.go(666, removeAttackHandler);



                if (Program.I().setting.setting.Vbattle.value == false)
                {
                    shiftArrow(VectorAttackCard, VectorAttackTarget, true, 50);
                    Program.notGo(removeAttackHandler);
                    Program.go(666, removeAttackHandler);
                }
                else
                {
                    shiftArrow(VectorAttackCard, VectorAttackTarget, true, 200);
                    Program.notGo(removeAttackHandler);
                    Program.go(800, removeAttackHandler);
                }






                //if (Program.I().setting.setting.Vbattle.value == false)
                //{
                //    Arrow.speed = 10;
                //    Arrow.updateSpeed();
                //    Sleep(40);
                //    shiftArrow(VectorAttackCard, VectorAttackTarget, true,50);
                //    Program.notGo(removeAttackHandler);
                //    Program.go(666, removeAttackHandler);
                //}
                //else
                //{
                //    Arrow.speed = 5;
                //    Arrow.updateSpeed();
                //    shiftArrow(VectorAttackCard, VectorAttackTarget, true, 200);
                //    //Program.notGo(removeAttackHandler);
                //    //Program.go(1000, removeAttackHandler);
                //}
                break;
            case GameMessage.Battle:
                if (Program.I().setting.setting.Vbattle.value == true)
                {
                    removeAttackHandler();
                    GPS gpsAttacker = r.ReadShortGPS();
                    r.ReadByte();
                    gameCard attackCard = GCS_cardGet(gpsAttacker, false);
                    if (attackCard != null)
                    {
                        YGOSharp.Card data2 = attackCard.get_data();
                        data2.Attack = r.ReadInt32();
                        data2.Defense = r.ReadInt32();
                        attackCard.set_data(data2);
                    }
                    else
                    {
                        r.ReadInt32();
                        r.ReadInt32();
                    }
                    r.ReadByte();
                    GPS gpsAttacked = r.ReadShortGPS();
                    r.ReadByte();
                    gameCard attackedCard = GCS_cardGet(gpsAttacked, false);
                    if (attackedCard != null && gpsAttacked.location != 0)
                    {
                        YGOSharp.Card data2 = attackedCard.get_data();
                        data2.Attack = r.ReadInt32();
                        data2.Defense = r.ReadInt32();
                        attackedCard.set_data(data2);
                    }
                    else
                    {
                        r.ReadInt32();
                        r.ReadInt32();
                    }
                    r.ReadByte();
                    UIHelper.playSound("explode", 0.4f);
                    int amount = (int)(Mathf.Clamp(attackCard.get_data().Attack, 0, 3500) * 0.8f);
                    iTween.ShakePosition(Program.camera_game_main.gameObject, iTween.Hash(
                                            "x", (float)amount / 1500f,
                                            "y", (float)amount / 1500f,
                                            "z", (float)amount / 1500f,
                                            "time", (float)amount / 2500f
                                            ));
                    VectorAttackCard = get_point_worldposition(gpsAttacker);
                    if (attackedCard == null || gpsAttacked.location == 0)
                    {
                        bool attacker_bool_me = gpsAttacker.controller == 0;
                        if (attacker_bool_me)
                        {
                            VectorAttackTarget = new Vector3(0, 0, 20);
                        }
                        else
                        {
                            VectorAttackTarget = new Vector3(0, 0, -20);
                        }
                    }
                    else
                    {
                        VectorAttackTarget = get_point_worldposition(gpsAttacked);
                        VectorAttackTarget += (VectorAttackTarget - VectorAttackCard) * 0.3f;
                    }
                    if ((attackedCard != null && gpsAttacked.location != 0) && (attackedCard.p.position & (UInt32)CardPosition.FaceUpAttack) > 0)
                    {
                        if (attackCard.get_data().Attack > attackedCard.get_data().Attack)
                        {
                            animation_battle(VectorAttackCard, VectorAttackTarget, attackCard);
                        }
                        else
                        {
                            animation_battle(VectorAttackTarget, VectorAttackCard, attackedCard);
                        }
                    }
                    else
                    {
                        animation_battle(VectorAttackCard, VectorAttackTarget, attackCard);
                    }
                    Sleep(40);
                }
                break;
            case GameMessage.AttackDisabled:
                //removeAttackHandler();
                break;
            case GameMessage.DamageStepStart:
                break;
            case GameMessage.DamageStepEnd:
                break;
            case GameMessage.BeChainTarget:
                break;
            case GameMessage.CreateRelation:
                break;
            case GameMessage.ReleaseRelation:
                break;
            case GameMessage.TossCoin:
                player = r.ReadByte();
                count = r.ReadByte();
                for (int i = 0; i < count; i++)
                {
                    data = r.ReadByte();
                    if (i == 0)
                    {
                        tempobj = create_s(Program.I().mod_ocgcore_coin);
                        tempobj.AddComponent<animation_screen_lock>().screen_point = new Vector3(getScreenCenter(), Screen.height / 2, 1);
                        tempobj.GetComponent<coiner>().coin_app();
                        if (data == 0)
                        {
                            tempobj.GetComponent<coiner>().tocoin(false);
                        }
                        else
                        {
                            tempobj.GetComponent<coiner>().tocoin(true);
                        }
                        destroy(tempobj, 7);
                    }
                    if (data == 0)
                    {
                        RMSshow_none(InterString.Get("硬币反面"));
                    }
                    else
                    {
                        RMSshow_none(InterString.Get("硬币正面"));
                    }
                }
                Sleep(280);
                break;
            case GameMessage.TossDice:
                player = r.ReadByte();
                count = r.ReadByte();
                for (int i = 0; i < count; i++)
                {
                    data = r.ReadByte();
                    if (i == 0)
                    {
                        tempobj = create_s(Program.I().mod_ocgcore_dice);
                        tempobj.AddComponent<animation_screen_lock>().screen_point = new Vector3(getScreenCenter(), Screen.height / 2, 1);
                        tempobj.GetComponent<coiner>().dice_app();
                        tempobj.GetComponent<coiner>().todice(data);
                        destroy(tempobj, 7);
                    }
                    RMSshow_none(InterString.Get("骰子结果：[?]", data.ToString()));
                }
                Sleep(280);
                break;
            case GameMessage.AnnounceRace:
                if (inIgnoranceReplay() || inTheWorld())
                {
                    break;
                }
                if (condition == Condition.record)
                {
                    Sleep(60);
                }
                destroy(waitObject, 0, false, true);
                player = localPlayer(r.ReadByte());
                ES_min = r.ReadByte();
                available = r.ReadUInt32();
                values = new List<messageSystemValue>();
                // 位宽与文案都按模式走：OCG 26 位、RD 32 位。
                // ⚠ 老代码写死 `i < 26` ⇒ RD 的银河/电子人等在 bit26..31，
                //   这 6 个种族**整个不会出现在宣言列表里**（连空条目都没有，直接缺项）。
                // ⚠ `1u << i`：bit31 用 `1 << i` 是负数，写回时上游是
                //   `UInt32.Parse(item.value)`，拿到 "-2147483648" 会抛 OverflowException。
                // 文案走 GameStringHelper.raceName —— 它已经避开 1050/1051 与类型段撞号。
                // OCG 下 RaceCount == 26、位全 ≤ 25，与老写法逐字等价（回归不受影响）。
                for (int i = 0; i < GameStringHelper.RaceCount; i++)
                {
                    if ((available & (1u << i)) != 0)
                    {
                        string rn = GameStringHelper.raceName(i);
                        if (rn.Length == 0)
                        {
                            continue;
                        }
                        values.Add(new messageSystemValue { hint = rn, value = (1u << i).ToString() });
                    }
                }
                RMSshow_multipleChoice("returnMultiple", ES_min, values);
                break;
            case GameMessage.AnnounceAttrib:
                if (inIgnoranceReplay() || inTheWorld())
                {
                    break;
                }
                if (condition == Condition.record)
                {
                    Sleep(60);
                }
                destroy(waitObject, 0, false, true);
                player = localPlayer(r.ReadByte());
                ES_min = r.ReadByte();
                available = r.ReadUInt32();
                values = new List<messageSystemValue>();
                for (int i = 0; i < 7; i++)
                {
                    if ((available & (1 << i)) > 0)
                    {
                        values.Add(new messageSystemValue { hint = GameStringManager.get_unsafe(1010 + i), value = (1 << i).ToString() });
                    }
                }
                RMSshow_multipleChoice("returnMultiple", ES_min, values);
                break;
            case GameMessage.AnnounceCard:
                if (inIgnoranceReplay() || inTheWorld())
                {
                    break;
                }
                if (condition == Condition.record)
                {
                    Sleep(60);
                }
                destroy(waitObject, 0, false, true);
                player = localPlayer(r.ReadByte());
                ES_searchCode.Clear();
                count = r.ReadByte();
                for (int i = 0; i < count; i++) 
                {
                    int take = r.ReadInt32();
                    ES_searchCode.Add(take);
                }
                //values = new List<messageSystemValue>();
                //values.Add(new messageSystemValue { value = "", hint = "" });
                //ES_RMS("AnnounceCard", values);
                RMSshow_input("AnnounceCard", InterString.Get("请输入关键字。"), "");
                break;
            case GameMessage.AnnounceNumber:
                if (inIgnoranceReplay() || inTheWorld())
                {
                    break;
                }
                if (condition == Condition.record)
                {
                    Sleep(60);
                }
                destroy(waitObject, 0, false, true);
                player = localPlayer(r.ReadByte());
                count = r.ReadByte();
                ES_min = 1;
                values = new List<messageSystemValue>();
                for (int i = 0; i < count; i++)
                {
                    values.Add(new messageSystemValue { hint = r.ReadUInt32().ToString(), value = i.ToString() });
                }
                RMSshow_multipleChoice("return", 1, values);
                break;
            case GameMessage.PlayerHint:
                player = localPlayer(r.ReadByte());
                int ptype = r.ReadByte();
                int pvalue = r.ReadInt32();
                string valstring = GameStringManager.get(pvalue);
                if (pvalue == 38723936)
                {
                    valstring = InterString.Get("不能确认墓地里的卡");
                    if (player == 0)
                    {
                        if (ptype == 6)
                        {
                            clearAllShowed();
                            Program.I().cardDescription.setData(YGOSharp.CardsManager.Get(38723936), GameTextureManager.opBack, "", true);
                            cantCheckGrave = true;
                        }
                        if (ptype == 7)
                            cantCheckGrave = false;
                    }
                }
                if (ptype == 6)
                {
                    if (player == 0)
                    {
                        RMSshow_none(InterString.Get("我方状态：[?]", valstring));
                    }
                    else
                    {
                        RMSshow_none(InterString.Get("对方状态：[?]", valstring));
                    }
                }
                else if (ptype == 7)
                {
                    if (player == 0)
                    {
                        RMSshow_none(InterString.Get("我方取消状态：[?]", valstring));
                    }
                    else
                    {
                        RMSshow_none(InterString.Get("对方取消状态：[?]", valstring));
                    }
                }
                break;
            case GameMessage.CardHint:
                gameCard game_card = GCS_cardGet(r.ReadGPS(), false);
                int ctype = r.ReadByte();
                int value = r.ReadInt32();
                if (game_card != null)
                {
                    if (ctype == 1)
                    {
                        animation_confirm(game_card);
                        var number = game_card.add_one_decoration(Program.I().mod_ocgcore_number, 3, new Vector3(60, 0, 0), "number", false);
                        number.game_object.GetComponent<number_loader>().set_number((int)value, 3);
                        number.scale_change_ignored = true;
                        number.game_object.transform.localScale = new Vector3(1, 1, 1);
                        number.game_object.transform.eulerAngles = new Vector3(60, 0, 0);
                        destroy(number.game_object, 2.2f);
                        Sleep(42);
                    }
                }
                break;
            case GameMessage.TagSwap:
                realize(true);
                arrangeCards();
                player = localPlayer(r.ReadByte());
                animation_suffleHand(player);
                Sleep(21);
                break;
            case GameMessage.AiName:
                break;
            case GameMessage.MatchKill:
                break;
            case GameMessage.CustomMsg:
                break;
            case GameMessage.DuelWinner:
                break;
            default:
                break;
        }
        r.BaseStream.Seek(0, 0);
    }

    private void createPlaceSelector(byte[] resp)
    {
        for (int i = 0; i < placeSelectors.Count; i++)
        {
            if (placeSelectors[i].data[0] == resp[0])
            {
                if (placeSelectors[i].data[1] == resp[1])
                {
                    if (placeSelectors[i].data[2] == resp[2])
                    {
                        return;
                    }
                }
            }
        }
        uint player_m = (uint)localPlayer(resp[0]);
        uint location = resp[1];
        uint index = resp[2];
        GPS newP = new GPS();
        newP.controller = player_m;
        newP.location = location;
        newP.sequence = index;
        newP.position = 0;
        Vector3 worldVector = get_point_worldposition(newP, null);
        var placs = create(Program.I().New_ocgcore_placeSelector, worldVector, Vector3.zero, false, null, true, Vector3.one).GetComponent<placeSelector>();
        placs.data = new byte[3];
        placs.data[0] = resp[0];
        placs.data[1] = resp[1];
        placs.data[2] = resp[2];
        placeSelectors.Add(placs);
        if (location == (uint)CardLocation.MonsterZone && Program.I().setting.setting.hand.value == false)
        {
            ES_placeSelected(placs);
        }
        if (location == (uint)CardLocation.SpellZone && Program.I().setting.setting.handm.value == false)
        {
            ES_placeSelected(placs);
        }
    }

    private void animation_suffleHand(int player)
    {
        for (int i = 0; i < cards.Count; i++) if (cards[i].gameObject.activeInHierarchy)
            {
                if ((cards[i].p.location & (UInt32)CardLocation.Hand) > 0)
                {
                    if (cards[i].p.controller == player)
                    {
                        Vector3 position;
                        if (cards[i].p.controller == 0)
                        {
                            position = new Vector3(0, 0, -3f - 15f * Program.fieldSize);
                        }
                        else
                        {
                            if (gameField.isLong)
                            {
                                position = new Vector3(0, 0, (19f + gameField.delat) * Program.fieldSize);
                            }
                            else
                            {
                                position = new Vector3(0, 0, (19f) * Program.fieldSize);
                            }
                        }
                        cards[i].animation_rush_to(position, new Vector3(-30, 0, 180));
                    }
                }
            }
    }

    private void clearChainEnd()
    {
        //removeAttackHandler();
        removeSelectedAnimations();
    }

    private void logicalClearChain()
    {
        for (int i = 0; i < cardsInChain.Count; i++)
        {
            cardsInChain[i].CS_clear();
        }
        cardsInChain.Clear();
    }

    private void showWait()
    {
        if (waitObject == null)
        {
            waitObject = create_s(Program.I().new_ocgcore_wait, Program.camera_main_2d.ScreenToWorldPoint(new Vector3(getScreenCenter(), Screen.height - 15f - 15f * (1.21f - Program.fieldSize) / 0.21f)), Vector3.zero, true, Program.ui_main_2d, true);
        }
    }

    void removeAttackHandler()
    {
        shiftArrow(Vector3.zero,Vector3.zero,false,50);
    }

    private void removeSelectedAnimations() 
    {
        for (int i = 0; i < cardsInSelectAnimation.Count; i++)
        {
            try
            {
                cardsInSelectAnimation[i].del_all_decoration_by_string("selected");
                cardsInSelectAnimation[i].currentKuang = gameCard.kuangType.none;
            }
            catch (System.Exception e)
            {
                UnityEngine.Debug.Log(e);
            }
        }
        cardsInSelectAnimation.Clear();
    }


    private void confirm(gameCard card)
    {
        Program.go(cardsForConfirm.Count * 700, confirmGPS);
        cardsForConfirm.Add(card);
    }

    private void Nconfirm()
    {
        Program.notGo(confirmGPS);
        cardsForConfirm.Clear();
    }

    List<gameCard> cardsForConfirm = new List<gameCard>();

    void confirmGPS()
    {
        if (cardsForConfirm.Count > 0)
        {
            animation_confirm(cardsForConfirm[0]);
            cardsForConfirm.RemoveAt(0);
        }
    }

    string ES_hint = "";

    string ES_selectHint = "";
    int Es_selectMSGHintType = 0;
    int Es_selectMSGHintPlayer = 0;
    int Es_selectMSGHintData = 0;

    List<gameCard> MHS_getBundle(int controller, int location)
    {
        List<gameCard> cardsInLocation = new List<gameCard>();
        for (int i = 0; i < cards.Count; i++) if (cards[i].gameObject.activeInHierarchy)
            {
                if (cards[i].p.location == location)
                {
                    if (cards[i].p.controller == controller)
                    {
                        cardsInLocation.Add(cards[i]);
                    }
                }
            }

        return cardsInLocation;
    }

    void MHS_creatBundle(int count, int player, CardLocation location)
    {
        // 排查用：GameMessage.Start 里服务器报的「各区当前张数」（这是权威值，
        // 客户端按它建占位卡）。记牌对账最终以它为准 —— 客户端自己数出来的张数
        // 有可能少（占位卡没建出来），不能拿来当基准。
        QuickTestTrace.Log("start", "bundle count=" + count + " player=" + player + " loc=" + location);
        for (int i = 0; i < count; i++)
        {
            GCS_cardCreate(new GPS
            {
                controller = (UInt32)player,
                location = (UInt32)location,
                position = (int)CardPosition.FaceDownAttack,
                sequence = (UInt32)i,
            });
        }
    }

    List<gameCard> MHS_resizeBundle(int count, int player, CardLocation location)
    {
        List<gameCard> cardBow = new List<gameCard>();
        List<gameCard> waterOutOfBow = new List<gameCard>();
        for (int i = 0; i < cards.Count; i++)
            if (cards[i].gameObject.activeInHierarchy)
            {
                if ((cards[i].p.location & (UInt32)location) > 0)
                {
                    if (cards[i].p.controller == player)
                    {
                        if (cardBow.Count < count)
                        {
                            cardBow.Add(cards[i]);
                        }
                        else
                        {
                            waterOutOfBow.Add(cards[i]);
                        }
                    }
                }
            }

        for (int i = 0; i < waterOutOfBow.Count; i++)
        {
            waterOutOfBow[i].hide();
        }
        while (cardBow.Count < count)
        {
            cardBow.Add(GCS_cardCreate(new GPS
            {
                controller = (UInt32)player,
                location = (UInt32)location,
                position = (int)CardPosition.FaceDownAttack,
                sequence = (UInt32)(cardBow.Count),
            }));
        }
        for (int i = 0; i < cardBow.Count; i++)
        {
            cardBow[i].erase_data();
            cardBow[i].p.position = (int)CardPosition.FaceDownAttack;
        }
        return cardBow;
    }

    void animation_battle(Vector3 VectorAttackedCard, Vector3 VectorAttackTarget, gameCard attackCard)
    {
        cookie_AttackEffect = (GameObject)MonoBehaviour.Instantiate(prewarmAttackEffect(attackCard, VectorAttackedCard, VectorAttackTarget), Vector3.zero, Quaternion.identity);
        cookie_AttackEffect.AddComponent<partical_scaler>().scale = 10f * Mathf.Clamp(attackCard.get_data().Attack, 0, 3500) / 1500f;
        MonoBehaviour.Destroy(cookie_AttackEffect, 3);
    }

    int ES_min = 0;

    int ES_max = 0;

    int ES_level = 0;

    bool ES_overFlow = false;

    int ES_sortSum = 0;

    List<int> ES_searchCode = new List<int>();


    class sortResult
    {
        public gameCard card = null;
        public int option = 0;
    }

    List<sortResult> ES_sortResult = new List<sortResult>();

    List<gameCard> cardsInChain = new List<gameCard>();

    List<gameCard> cardsInSelectAnimation = new List<gameCard>();

    List<gameCard> allCardsInSelectMessage = new List<gameCard>();

    List<gameCard> cardsSelected = new List<gameCard>();

    List<gameCard> cardsMustBeSelected = new List<gameCard>();

    List<gameCard> cardsSelectable = new List<gameCard>();

    List<gameCard> cardsInSort = new List<gameCard>();

    GameObject cookie_AttackEffect = null;

    GameObject prewarmAttackEffect(gameCard card, Vector3 from, Vector3 to)
    {
        GameObject mod = Program.I().mod_ocgcore_bs_atk_line_earth;
        if (GameStringHelper.differ(card.get_data().Attribute, (long)CardAttribute.Earth))
            mod = Program.I().mod_ocgcore_bs_atk_line_earth;
        if (GameStringHelper.differ(card.get_data().Attribute, (long)CardAttribute.Water))
            mod = Program.I().mod_ocgcore_bs_atk_line_water;
        if (GameStringHelper.differ(card.get_data().Attribute, (long)CardAttribute.Fire))
            mod = Program.I().mod_ocgcore_bs_atk_line_fire;
        if (GameStringHelper.differ(card.get_data().Attribute, (long)CardAttribute.Wind))
            mod = Program.I().mod_ocgcore_bs_atk_line_wind;
        if (GameStringHelper.differ(card.get_data().Attribute, (long)CardAttribute.Dark))
            mod = Program.I().mod_ocgcore_bs_atk_line_dark;
        if (GameStringHelper.differ(card.get_data().Attribute, (long)CardAttribute.Light))
            mod = Program.I().mod_ocgcore_bs_atk_line_light;
        if (GameStringHelper.differ(card.get_data().Attribute, (long)CardAttribute.Divine))
            mod = Program.I().mod_ocgcore_bs_atk_line_light;
        mod.transform.GetChild(0).localPosition = to;
        mod.transform.GetChild(1).localPosition = from;
        return mod;
    }

    public void realizeCardsForSelect()
    {

        for (int i = 0; i < allCardsInSelectMessage.Count; i++)
        {
            allCardsInSelectMessage[i].del_all_decoration();
            allCardsInSelectMessage[i].isShowed = false;
            allCardsInSelectMessage[i].show_number(0);
            allCardsInSelectMessage[i].currentFlash = gameCard.flashType.none;
        }

        cardsSelectable.Clear();

        getSelectableCards();

        if (cardsSelected.Count == 0)
        {
            if (UIHelper.fromStringToBool(Config.Get("smartSelect_", "1")))
            {
                switch (currentMessage)
                {
                    case GameMessage.SelectTribute:
                        if (cardsSelectable.Count == 1)
                        {
                            autoSendCards();
                            return;
                        }
                        int all = 0;
                        for (int i = 0; i < cardsSelectable.Count; i++)
                        {
                            all += cardsSelectable[i].levelForSelect_1;
                        }
                        if (all == ES_min)
                        {
                            autoSendCards();
                            return;
                        }
                        break;
                    case GameMessage.SelectCard:
                        if (cardsSelectable.Count <= ES_min)
                        {
                            autoSendCards();
                            return;
                        }
                        if (ES_min == ES_max)
                        {
                            if (ifAllCardsInSameCode(cardsSelectable))
                            {
                                if (ifAllCardsInSameController(cardsSelectable))
                                {
                                    if (ifAllCardsInSameLocation(cardsSelectable))
                                    {
                                        autoSendCards();
                                        return;
                                    }
                                }
                            }
                        }
                        break;
                    case GameMessage.SelectSum:
                        if (cardsSelectable.Count <= ES_min)
                        {
                            autoSendCards();
                            return;
                        }
                        bool allSame = true;
                        int selectableLevel = 0;
                        for (int x = 0; x < cardsMustBeSelected.Count; x++)
                        {
                            selectableLevel += cardsMustBeSelected[x].levelForSelect_1;
                        }
                        for (int x = 0; x < cardsSelectable.Count; x++)
                        {
                            selectableLevel += cardsSelectable[x].levelForSelect_1;
                        }
                        if (selectableLevel != ES_level)
                        {
                            allSame = false;
                        }
                        selectableLevel = 0;
                        for (int x = 0; x < cardsMustBeSelected.Count; x++)
                        {
                            selectableLevel += cardsMustBeSelected[x].levelForSelect_2;
                        }
                        for (int x = 0; x < cardsSelectable.Count; x++)
                        {
                            selectableLevel += cardsSelectable[x].levelForSelect_2;
                        }
                        if (selectableLevel != ES_level)
                        {
                            allSame = false;
                        }
                        if (allSame)
                        {
                            autoSendCards();
                            return;
                        }
                        break;
                }
            }
        }

        for (int i = 0; i < cardsSelectable.Count; i++)
        {
            cardsSelectable[i].add_one_decoration(Program.I().mod_ocgcore_decoration_card_selecting, 2, Vector3.zero, "card_selecting");
            cardsSelectable[i].isShowed = true;
            cardsSelectable[i].currentFlash = gameCard.flashType.Select;
        }

        for (int x = 0; x < cardsMustBeSelected.Count; x++)
        {
            if (currentMessage == GameMessage.SelectSum)
            {
                cardsMustBeSelected[x].show_number((int)(cardsMustBeSelected[x].levelForSelect_2));
            }
            else
            {
                cardsMustBeSelected[x].show_number((int)(x + 1));
            }
            cardsMustBeSelected[x].isShowed = true;
        }

        for (int x = 0; x < cardsSelected.Count; x++)
        {
            if (currentMessage == GameMessage.SelectSum)
            {
                cardsSelected[x].show_number((int)(cardsSelected[x].levelForSelect_2));
            }
            else
            {
                cardsSelected[x].show_number((int)(x + 1));
            }
            cardsSelected[x].isShowed = true;
        }

        bool sendable = false;
        bool real_send = false;

        if (currentMessage == GameMessage.SelectSum)
        {
            if (cardsSelected.Count == ES_max)
            {
                sendable = true;
            }
            int selectedLevel = 0;
            for (int x = 0; x < cardsMustBeSelected.Count; x++)
            {
                selectedLevel += cardsMustBeSelected[x].levelForSelect_1;
            }
            for (int x = 0; x < cardsSelected.Count; x++)
            {
                selectedLevel += cardsSelected[x].levelForSelect_1;
            }
            if (ES_overFlow)
            {
                if (selectedLevel >= ES_level)
                {
                    sendable = true;
                    real_send = true;
                }
            }
            else
            {
                if (selectedLevel == ES_level)
                {
                    sendable = true;
                }
            }
            selectedLevel = 0;
            for (int x = 0; x < cardsMustBeSelected.Count; x++)
            {
                selectedLevel += cardsMustBeSelected[x].levelForSelect_2;
            }
            for (int x = 0; x < cardsSelected.Count; x++)
            {
                selectedLevel += cardsSelected[x].levelForSelect_2;
            }
            if (ES_overFlow)
            {
                if (selectedLevel >= ES_level)
                {
                    sendable = true;
                    real_send = true;
                }
            }
            else
            {
                if (selectedLevel == ES_level)
                {
                    sendable = true;
                }
            }
            if (cardsSelectable.Count == 0)
            {
                sendable = true;
                real_send = true;
            }
        }
        if (currentMessage == GameMessage.SelectCard)
        {
            if (cardsSelected.Count >= ES_min)
            {
                sendable = true;
            }
            if (cardsSelected.Count == ES_max || cardsSelected.Count == cardsSelectable.Count)
            {
                sendable = true;
                real_send = true;
            }
        }
        if (currentMessage == GameMessage.SelectTribute)
        {
            int all = 0;
            for (int i = 0; i < cardsSelected.Count; i++)
            {
                all += cardsSelected[i].levelForSelect_1;
            }
            if (all >= ES_min)
            {
                sendable = true;
            }
            if (all >= ES_max)
            {
                sendable = true;
                if (cardsSelectable.Count == 1)
                {
                    real_send = true;
                }
            }
            if (cardsSelected.Count == cardsSelectable.Count)
            {
                sendable = true;
                real_send = true;
            }
            if (cardsSelected.Count == ES_max)
            {
                sendable = true;
                real_send = true;
            }
        }

        if (sendable)
        {
            if (real_send)
            {
                gameInfo.removeHashedButton("sendSelected");
                sendSelectedCards();
            }
            else
            {
                if (gameInfo.queryHashedButton("sendSelected") == false)
                {
                    gameInfo.addHashedButton("sendSelected", 0, superButtonType.yes, InterString.Get("完成选择@ui"));
                }
            }
        }
        else if (currentMessage != GameMessage.SelectUnselect)
        {
            gameInfo.removeHashedButton("sendSelected");
        }


        realize();
        toNearest();
    }

    private void getSelectableCards()
    {
        if (currentMessage == GameMessage.SelectCard || currentMessage == GameMessage.SelectUnselect)
        {
            for (int i = 0; i < allCardsInSelectMessage.Count; i++)
            {
                cardsSelectable.Add(allCardsInSelectMessage[i]);
            }
        }
        if (currentMessage == GameMessage.SelectTribute)
        {
            for (int i = 0; i < allCardsInSelectMessage.Count; i++)
            {
                cardsSelectable.Add(allCardsInSelectMessage[i]);
            }
        }
        if (currentMessage == GameMessage.SelectSum)
        {
            int selectedLevel = 0;
            for (int x = 0; x < cardsMustBeSelected.Count; x++)
            {
                selectedLevel += cardsMustBeSelected[x].levelForSelect_1;
            }
            for (int x = 0; x < cardsSelected.Count; x++)
            {
                selectedLevel += cardsSelected[x].levelForSelect_1;
            }
            checkSum(selectedLevel);
            selectedLevel = 0;
            for (int x = 0; x < cardsMustBeSelected.Count; x++)
            {
                selectedLevel += cardsMustBeSelected[x].levelForSelect_2;
            }
            for (int x = 0; x < cardsSelected.Count; x++)
            {
                selectedLevel += cardsSelected[x].levelForSelect_2;
            }
            checkSum(selectedLevel);
        }
    }

    private static bool ifAllCardsInSameLocation(List<gameCard> cards)
    {
        bool re = true;
        if (cards.Count > 0)
        {
            UInt32 loc = cards[0].p.location;
            if (loc != (UInt32)CardLocation.Deck)
            {
                return false;
            }
            for (int i = 0; i < cards.Count; i++)
            {
                if (cards[i].p.location != loc)
                {
                    re = false;
                }
            }
        }
        return re;
    }

    private static bool ifAllCardsInSameController(List<gameCard> cards)
    {
        bool re = true;
        if (cards.Count > 0)
        {
            UInt32 con = cards[0].p.controller;
            for (int i = 0; i < cards.Count; i++)
            {
                if (cards[i].p.controller != con)
                {
                    re = false;
                }
            }
        }
        return re;
    }

    private static bool ifAllCardsInSameCode(List<gameCard> cards)
    {
        bool re = true;
        if (cards.Count > 0)
        {
            int code = cards[0].get_data().Id;
            for (int i = 0; i < cards.Count; i++)
            {
                if (cards[i].get_data().Id != code)
                {
                    re = false;
                }
                if (cards[i].get_data().Id == 0)
                {
                    re = false;
                }
            }
        }
        return re;
    }

    public static List<List<gameCard>> GetCombination(List<gameCard> t, int n)//卡片全部放到t里面，n是小于selectMax的任意整数，返回卡片张数为n的卡片全组合
    {
        if (t.Count < n)
        {
            return null;
        }
        int[] temp = new int[n];
        List<List<gameCard>> list = new List<List<gameCard>>();
        GetCombination(ref list, t, t.Count, n, temp, n);
        return list;
    }

    private static void GetCombination(ref List<List<gameCard>> list, List<gameCard> t, int n, int m, int[] b, int M)
    {
        for (int i = n; i >= m; i--)
        {
            b[m - 1] = i - 1;
            if (m > 1)
            {
                GetCombination(ref list, t, i - 1, m - 1, b, M);
            }
            else
            {
                if (list == null)
                {
                    list = new List<List<gameCard>>();
                }
                List<gameCard> temp = new List<gameCard>();
                for (int j = 0; j < b.Length; j++)
                {
                    temp.Add(t[b[j]]);
                }
                list.Add(temp);
            }
        }
    }

    private bool queryCorrectOverSumList(List<gameCard> temp,int sumlevel)  
    {
        int illusionCount = temp.Count - cardsMustBeSelected.Count;
        if (illusionCount < ES_min)
        {
            return false;
        }
        if (illusionCount > ES_max)
        {
            return false;
        }
        int okCount = 0;
        for (int i = 1; i <= temp.Count; i++)
        {
            List<List<gameCard>> totalCobination = GetCombination(temp, i);
            for (int i2 = 0; i2 < totalCobination.Count; i2++)
            {
                bool re = false;
                int sumillustration = 0;
                for (int i3 = 0; i3 < totalCobination[i2].Count; i3++)
                {
                    sumillustration += totalCobination[i2][i3].levelForSelect_1;
                }
                if (sumillustration >= sumlevel)
                {
                    re = true;
                }
                sumillustration = 0;
                for (int i3 = 0; i3 < totalCobination[i2].Count; i3++)
                {
                    sumillustration += totalCobination[i2][i3].levelForSelect_2;
                }
                if (sumillustration >= sumlevel)
                {
                    re = true;
                }
                if (re)
                {
                    okCount++;
                }
            }
        }
        return (okCount == 1);
    }

    void checkSum(int star)
    {
        List<gameCard> cards_remain_unselected = getUnselectedCards();
        if (ES_overFlow)
        {
            for (int i = 1; i <= cards_remain_unselected.Count; i++)
            {
                List<List<gameCard>> totalCobination = GetCombination(cards_remain_unselected, i);
                for (int i2 = 0; i2 < totalCobination.Count; i2++)
                {
                    List<gameCard> selectIllusion = new List<gameCard>();
                    for (int x = 0; x < totalCobination[i2].Count; x++)
                    {
                        selectIllusion.Add(totalCobination[i2][x]);
                    }
                    for (int x = 0; x < cardsSelected.Count; x++)
                    {
                        selectIllusion.Add(cardsSelected[x]);
                    }
                    for (int x = 0; x < cardsMustBeSelected.Count; x++)
                    {
                        selectIllusion.Add(cardsMustBeSelected[x]);
                    }
                    if (queryCorrectOverSumList(selectIllusion, ES_level) == true)
                    {
                        for (int i3 = 0; i3 < totalCobination[i2].Count; i3++)
                        {
                            cardsSelectable.Remove(totalCobination[i2][i3]);
                            cardsSelectable.Add(totalCobination[i2][i3]);
                        }
                    }
                }
            }
        }
        else
        {
            for (int i = 0; i < cards_remain_unselected.Count; i++)
            {
                List<gameCard> selectIllusion = new List<gameCard>();
                for (int x = 0; x < cards_remain_unselected.Count; x++)
                {
                    if (x != i)
                    {
                        selectIllusion.Add(cards_remain_unselected[x]);
                    }
                }
                bool r = checkSum_process(selectIllusion, (int)ES_level - star - cards_remain_unselected[i].levelForSelect_1, cardsSelected.Count + 1);
                if (!r && cards_remain_unselected[i].levelForSelect_1 != cards_remain_unselected[i].levelForSelect_2)
                {
                    r = checkSum_process(selectIllusion, (int)ES_level - star - cards_remain_unselected[i].levelForSelect_2,cardsSelected.Count + 1);
                }
                if (r)
                {
                    cardsSelectable.Remove(cards_remain_unselected[i]);
                    cardsSelectable.Add(cards_remain_unselected[i]);
                }
            }
        }

    }

    private List<gameCard> getUnselectedCards()
    {
        List<gameCard> cards_remain_unselected = new List<gameCard>();
        for (int x = 0; x < allCardsInSelectMessage.Count; x++)
        {
            cards_remain_unselected.Add(allCardsInSelectMessage[x]);
        }
        for (int x = 0; x < cardsSelected.Count; x++)
        {
            cards_remain_unselected.Remove(cardsSelected[x]);
        }
        for (int x = 0; x < cardsMustBeSelected.Count; x++)
        {
            cards_remain_unselected.Remove(cardsMustBeSelected[x]);
        }

        return cards_remain_unselected;
    }

    bool checkSum_process(List<gameCard> cards_temp, int sum, int selectedCount)    
    {
        if (sum == 0)
        {
            if (selectedCount < ES_min)
            {
                return false;
            }
            if (selectedCount > ES_max)
            {
                return false;
            }
            return true;
        }
        if (sum < 0)
        {
            return false;
        }

        for (int i = 0; i < cards_temp.Count; i++)
        {
            List<gameCard> new_cards = new List<gameCard>();
            for (int x = 0; x < cards_temp.Count; x++)
            {
                if (x != i)
                {
                    new_cards.Add(cards_temp[x]);
                }
            }
            bool r = checkSum_process(new_cards, sum - cards_temp[i].levelForSelect_1, selectedCount + 1);
            if (!r && cards_temp[i].levelForSelect_1 != cards_temp[i].levelForSelect_2)
            {
                r = checkSum_process(new_cards, sum - cards_temp[i].levelForSelect_2, selectedCount + 1);
            }
            if (r)
            {
                return r;
            }
        }

        return false;
    }

    void autoSendCards()
    {
        BinaryMaster m = new BinaryMaster();
        switch (currentMessage)
        {
            case GameMessage.SelectCard:
            case GameMessage.SelectUnselect:
            case GameMessage.SelectTribute:
                int c = ES_min;
                if (cardsSelectable.Count < c)
                {
                    c = cardsSelectable.Count;
                }
                m.writer.Write((byte)(c));
                for (int i = 0; i < c; i++)
                {
                    m.writer.Write((byte)(cardsSelectable[i].selectPtr));
                    lastExcitedController = (int)cardsSelectable[i].p.controller;
                    lastExcitedLocation = (int)cardsSelectable[i].p.location;
                }
                sendReturnAuto(m.get());
                break;
            case GameMessage.SelectSum:
                m = new BinaryMaster();
                m.writer.Write((byte)(cardsMustBeSelected.Count + cardsSelectable.Count));
                for (int i = 0; i < cardsMustBeSelected.Count; i++)
                {
                    m.writer.Write((byte)i);
                }
                for (int i = 0; i < cardsSelectable.Count; i++)
                {
                    m.writer.Write((byte)(cardsSelectable[i].selectPtr));
                    lastExcitedController = (int)cardsSelectable[i].p.controller;
                    lastExcitedLocation = (int)cardsSelectable[i].p.location;
                }
                sendReturnAuto(m.get());
                break;
        }
    }

    void sendSelectedCards()
    {
        BinaryMaster m;
        switch (currentMessage)
        {
            case GameMessage.SelectCard:
            case GameMessage.SelectUnselect:
            case GameMessage.SelectTribute:
            case GameMessage.SelectSum:
                m = new BinaryMaster();
                if (currentMessage == GameMessage.SelectUnselect && cardsSelected.Count == 0)
                {
                    m.writer.Write((Int32)(-1));
                    sendReturn(m.get());
                    break;
                }
                m.writer.Write((byte)(cardsMustBeSelected.Count + cardsSelected.Count));
                for (int i = 0; i < cardsMustBeSelected.Count; i++)
                {
                    m.writer.Write((byte)i);
                }
                for (int i = 0; i < cardsSelected.Count; i++)
                {
                    m.writer.Write((byte)(cardsSelected[i].selectPtr));
                    lastExcitedController = (int)cardsSelected[i].p.controller;
                    lastExcitedLocation = (int)cardsSelected[i].p.location;
                }
                sendReturn(m.get());
                break;
        }
    }

    int lastExcitedLocation = -1;
    int lastExcitedController = -1;
    bool clearAllShowedB = false;
    bool clearTimeFlag = false;

    void clearResponse()
    {

        flagForTimeConfirm = false;
        flagForCancleChain = false;
        //Package p = new Package();
        //p.Fuction = (int)GameMessage.sibyl_clear;
        //TcpHelper.AddRecordLine(p);
        if (clearTimeFlag)
        {
            clearTimeFlag = false;
            MessageBeginTime = 0;
        }
        ES_selectHint = "";
        cardsInSort.Clear();
        allCardsInSelectMessage.Clear();
        cardsSelected.Clear();
        cardsMustBeSelected.Clear();
        cardsSelectable.Clear();
        ES_sortResult.Clear();
        //cardsForConfirm.Clear();
        //Program.notGo(confirmGPS);
        gameField.Phase.colliderMp2.enabled = false;
        gameField.Phase.colliderBp.enabled = false;
        gameField.Phase.colliderEp.enabled = false;

        toDefaultHint();

        clearAllSelectPlace();

        int myMaxDeck = countLocationSequence(0, CardLocation.Deck);

        for (int i = 0; i < cards.Count; i++) if (cards[i].gameObject.activeInHierarchy)
            {
                cards[i].remove_all_cookie_button();
                cards[i].show_number(0);
                cards[i].del_all_decoration();
                cards[i].sortOptions.Clear();
                cards[i].currentFlash = gameCard.flashType.none;
                cards[i].prefered = false;
                if (cards[i].forSelect)
                {
                    cards[i].forSelect = false;
                    cards[i].isShowed = false;
                    if ((cards[i].p.location & (UInt32)CardLocation.Deck) > 0)
                    {
                        if (deckReserved == false || cards[i].p.controller != 0 || cards[i].p.sequence != myMaxDeck)
                        {
                            cards[i].erase_data();
                        }
                    }
                }
                cards[i].effects.Clear();
                if ((int)cards[i].p.location == lastExcitedLocation)
                {
                    if ((int)cards[i].p.controller == lastExcitedController)
                    {
                        cards[i].isShowed = false;
                    }
                }
                if (cards[i].p.location == (uint)CardLocation.Deck)
                {
                    cards[i].isShowed = false;
                }
                if (clearAllShowedB)
                {
                    cards[i].isShowed = false;
                }
            }
        clearAllShowedB = false;
        lastExcitedLocation = -1;
        lastExcitedController = -1;
        List<gameCard> to_clear = new List<gameCard>();
        for (int i = 0; i < cards.Count; i++) if (cards[i].gameObject.activeInHierarchy)
            {
                if (cards[i].p.location == (uint)CardLocation.Search)
                {
                    to_clear.Add(cards[i]);
                }
            }

        for (int i = 0; i < to_clear.Count; i++)
        {
            to_clear[i].hide();
            to_clear[i].p.location = (UInt32)CardLocation.Unknown;
        }
        gameInfo.removeAll();
        RMSshow_clear();
        realize();
        toNearest();
    }

    private void clearAllSelectPlace()
    {
        for (int i = 0; i < placeSelectors.Count; i++)
        {
            if (placeSelectors[i] != null)
            {
                if (placeSelectors[i].gameObject != null)
                {
                    MonoBehaviour.DestroyImmediate(placeSelectors[i].gameObject);
                }
            }
        }
        placeSelectors.Clear();
    }

    public void Sleep(int framsIn60)    
    {
        // 撤回追赶期间**完全不出演出**：不推进 MessageBeginTime，于是 ifMessageImportant 那道
        // 动画节流永远放行，整段流不会被摊到成百上千帧里去。配合 sibyl() 跳过表现层，
        // 效果就是「服务器在后台默默追平，屏幕上什么都不发生」。
        if (DuelUndo.silent || DuelUndo.rebuilding)
        {
            return;
        }
        // 整局重放（排查入口）走的是非静默路径，那条路才需要快进节拍。
        if (DuelUndo.active && !DuelUndo.silent && framsIn60 > 0)
        {
            int scaled = (int)(framsIn60 * DuelUndo.paceScale);
            framsIn60 = scaled < 1 ? 1 : scaled;
        }
        int illustion = (int)(Program.TimePassed() + framsIn60 * 1000f / 60f);
        if (illustion > MessageBeginTime)
        {
            MessageBeginTime = illustion;
        }
    }

    public bool isFirst = false;

    public bool isObserver = false;

    public void StocMessage_TimeLimit(BinaryReader r)
    {
        int player = r.ReadByte();
        r.ReadByte();
        int time_limit = r.ReadInt16();
        TcpHelper.CtosMessage_TimeConfirm();
        gameInfo.setTime(unSwapPlayer(localPlayer(player)), time_limit);
        if (unSwapPlayer(localPlayer(player)) == 0)
        {
            destroy(waitObject, 0, false, true);
        }
    }

    public int localPlayer(int p)
    {
        if (p == 0 || p == 1)
        {
            if (isFirst)
            {
                return p;
            }
            else
            {
                return 1 - p;
            }
        }
        else
        {
            return p;
        }
    }

    bool someCardIsShowed = false;

    #region 卡组记牌（按钮 + 卡面）

    /// <summary>
    /// 记牌态是否打开。
    ///
    /// 口径（用户 2026-09-17 拍板）：**开了就一直开**，直到玩家自己点「不再记牌」。
    /// 「确认完毕」把卡组收了**不算关**（那只擦卡面，见 `clearAllShowed`）；
    /// 转视角、名单一时取不到也只擦卡面、状态留着。只有三处会复位成 false：
    /// ・玩家点「不再记牌」（`toggleDeckMemo`）；
    /// ・新一局（`GameMessage.Start`；撤回重放除外）；
    /// ・观战 / 双打这类根本不适用的场景（`applyDeckMemoCodes_core` 的排除分支）。
    /// 只在内存里，不落盘、不写 Config。
    ///
    /// ⚠ 「态持久」≠「按钮常驻」（用户 2026-09-17 追加口径）：那颗按钮跟「卡组记牌」共用
    /// 出现条件，只在牌摊开时冒出来；收摊它一起收，但本字段不受影响 —— 下次再确认卡组，
    /// 按钮带着「不再记牌」的文案回来。
    /// </summary>
    public bool deckMemoOn = false;

    /// <summary>记牌态下被我们改过卡面的那些牌（退出去时要原样擦回未知）。</summary>
    readonly List<gameCard> deckMemoTouched = new List<gameCard>();

    /// <summary>
    /// 点击「卡组记牌 / 不再记牌」后的**待办**：由按钮回调置位，帧末执行真正的切换。
    ///
    /// ⛔ 为什么必须延后一拍：见 `ES_gameUIbuttonClicked` 里 `deck_memo` 分支的注释。
    /// 在 NGUI 的点击派发里增删同一个按钮会诱发无限重入，主线程直接卡死。
    /// </summary>
    bool deckMemoTogglePending = false;

    /// <summary>帧末消费 <see cref="deckMemoTogglePending"/>（放在 preFrameFunction 末尾的 tick 段里）。</summary>
    void deckMemoToggleTick()
    {
        if (deckMemoTogglePending == false)
        {
            return;
        }
        deckMemoTogglePending = false;
        // 防抖：`listenerForClicked` 是「按 hashString 匹配就派发」，同一次物理点击有可能
        // 被派发两遍（相邻两帧各一次），那样按钮会「开→关」连翻两下，看起来像没反应。
        // 250ms 内的第二次直接丢掉 —— 人不可能想双击这个开关。
        float now = Time.realtimeSinceStartup;
        if (now - deckMemoLastToggleAt < 0.25f)
        {
            return;
        }
        deckMemoLastToggleAt = now;
        toggleDeckMemo();
    }

    /// <summary>上一次真正执行切换的时刻（`Time.realtimeSinceStartup`，不随场景重载复位）。</summary>
    float deckMemoLastToggleAt = -100f;

    /// <summary>右侧按钮当前挂的是哪一态的文字：-1 没挂、0「卡组记牌」、1「不再记牌」。</summary>
    int deckMemoHintState = -1;

    /// <summary>
    /// 本局**我方主卡组的有序 code 表**（顺序即 ydk 原始顺序）。三种模式各一条路：
    /// ・联机对局 / AI 测试局 → `TcpHelper.deck.Main`（点准备时 `CtosMessage_UpdateDeck` 写入；
    ///   ⚠ AI 局**同样**会发 UpdateDeck（`Room.quickStartFlow`），实测 deckStrings 是齐的，
    ///   所以不需要另写一个 ydk 读取器）；
    /// ・录像回放 → 内嵌 YRP 的 `playerData[isFirst ? 0 : 1].main`（只有带内嵌 YRP 的录像才有，
    ///   见 `selectReplay.lastReplayYrp` 的注释）。
    /// 拿不到就返回 null（观战者、没上报过卡组、旧格式录像……），调用方据此不显示按钮/不摆浮标——
    /// 宁可不出，也别显示错的。
    /// </summary>
    List<int> deckMemoOrder(out string src)
    {
        src = "none";
        if (condition == Condition.record)
        {
            // ⚠ 必须用类型名访问静态字段（`selectReplay.lastReplayYrp`），不能用实例 —— CS0176。
            if (Program.I().selectReplay == null || selectReplay.lastReplayYrp == null)
            {
                return null;
            }
            if (selectReplay.lastReplayYrpCount != 1)
            {
                // 0 = 这段录像里没有内嵌 YRP（AI 对局录的没有 STOC_REPLAY）；
                // >1 = MATCH 录像有多份，包流里没有可靠对应关系 → 说不清是谁的卡组。
                // 两种情况都宁可不显示。
                return null;
            }
            Percy.YRP yrp = selectReplay.lastReplayYrp;
            if (yrp.playerData == null || yrp.playerData.Count == 4)
            {
                return null;   // 双打录像（4 份 playerData）语义不通用，先不做
            }
            int idx = isFirst ? 0 : 1;
            if (yrp.playerData.Count <= idx || yrp.playerData[idx].main.Count == 0)
            {
                return null;
            }
            src = "replay";
            return new List<int>(yrp.playerData[idx].main);
        }
        if (TcpHelper.deck == null || TcpHelper.deck.Main == null)
        {
            return null;
        }
        if (TcpHelper.deck.Main.Count == 0 || TcpHelper.deckStrings.Count != TcpHelper.deck.Main.Count)
        {
            return null;
        }
        src = (Program.I().room != null ? Program.I().room.GetType().Name : "?") + "/tcp";
        return new List<int>(TcpHelper.deck.Main);
    }

    /// <summary>本局我方主卡组**还剩哪些牌**（ydk 原始顺序的 code 列表）。拿不到返回 null。</summary>
    List<int> deckMemoRemainingCodes(out string src, out string dropped, List<string> consumed = null)
    {
        src = "none";
        dropped = "";
        List<int> order = deckMemoOrder(out src);
        if (order == null)
        {
            return null;
        }
        List<int> idx = Book.memoRemainingIndexes(order, out dropped, consumed);
        if (idx == null)
        {
            return null;
        }
        List<int> codes = new List<int>(idx.Count);
        for (int i = 0; i < idx.Count; i++)
        {
            codes.Add(order[idx[i]]);
        }
        return codes;
    }

    /// <summary>
    /// 按 `cards` 列表顺序收集「正在展示中的、我方卡组的牌」。
    /// 必须用 `cards` 的顺序而不是 `p.sequence`：`realize()` 排布展示行时走的就是 `cards` 顺序，
    /// 我们按同一个顺序赋值，屏幕上的排列才等于我们要的顺序。
    /// </summary>
    void collectShowedMyDeckCards(List<gameCard> into)
    {
        into.Clear();
        for (int i = 0; i < cards.Count; i++)
        {
            if (cards[i].gameObject == null || cards[i].gameObject.activeInHierarchy == false)
            {
                continue;
            }
            if (cards[i].isShowed == false)
            {
                continue;
            }
            if ((cards[i].p.location & (UInt32)CardLocation.Deck) == 0)
            {
                continue;
            }
            if (cards[i].p.controller != 0)
            {
                continue;
            }
            into.Add(cards[i]);
        }
    }

    bool hasShowedMyDeckCard()
    {
        for (int i = 0; i < cards.Count; i++)
        {
            if (cards[i].gameObject == null || cards[i].gameObject.activeInHierarchy == false)
            {
                continue;
            }
            if (cards[i].isShowed
                && (cards[i].p.location & (UInt32)CardLocation.Deck) > 0
                && cards[i].p.controller == 0)
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// 记牌态的**显示层**：把「还剩哪些牌」按 ydk 原始顺序铺到正在展示的那批卡组牌上。
    ///
    /// 关键事实：展示行里的卡是**正面朝向**的（`realize()` 给展示行的旋转固定是 (-30,0,0)），
    /// 之所以看起来还是背面，只是因为占位卡的 `data.Id == 0` 时
    /// `gameCard.card_picture_handler()` 把**卡面贴图**换成了卡背。
    /// 所以「让这批牌显示真卡面」= 只要把 code 赋上去，不用碰朝向、不用碰位置。
    ///
    /// 「不再记牌」＝把赋上去的码 `erase_data()` 擦回未知（卡仍摊着，见口径 #2）；
    /// 张数比场上少的那些槽位保持空白背面（口径 #3，多出来的不提示、不裁切）。
    ///
    /// ⛔ 本函数**不许调 `realize()`**：它是在 `realize()` 内部被调用的（每拍现算），调了就是递归。
    /// </summary>
    void applyDeckMemoCodes()
    {
        int dbgDepth = ++deckMemoDbgDepth;
        int dbgCall = ++deckMemoDbgApply;
        if (QuickTestTrace.Enabled && (dbgCall <= 8 || dbgCall == 60))
        {
            string extra = "";
            if (dbgCall == 60)
            {
                string[] st = System.Environment.StackTrace.Split('\n');
                for (int k = 1; k < st.Length && k <= 22; k++)
                {
                    extra += " || " + st[k].Trim();
                }
            }
            QuickTestTrace.Log("memo", "dbg apply#" + dbgCall + " depth=" + dbgDepth
                + " tid=" + System.Threading.Thread.CurrentThread.ManagedThreadId
                + " inst=" + GetHashCode() + " on=" + deckMemoOn
                + " last='" + deckMemoLastApplied + "' touched=" + deckMemoTouched.Count
                + extra);
        }
        try
        {
            applyDeckMemoCodes_body();
        }
        finally
        {
            deckMemoDbgDepth--;
        }
    }

    int deckMemoDbgDepth = 0;
    int deckMemoDbgApply = 0;
    int deckMemoDbgEnd = 0;
    int deckMemoDbgToggle = 0;

    void applyDeckMemoCodes_body()
    {
        // 重入护栏：`realize()` 是每拍多处调用的，万一有个 callee（`set_code`/`set_data` 那条链）
        // 又转回来调 realize，这里必须直接退，否则就是无限递归。
        if (deckMemoApplying)
        {
            return;
        }
        deckMemoApplying = true;
        try
        {
            applyDeckMemoCodes_core();
        }
        finally
        {
            deckMemoApplying = false;
        }
    }

    bool deckMemoApplying = false;

    void applyDeckMemoCodes_core()
    {
        // 转视角：`p.controller` 被整体翻转后，画面下方的卡堆其实**是对手的**，
        // 继续铺就是把我方卡组的剩余码铺到对手卡堆上（比不显示更糟）→ 把卡面擦掉。
        // 但**状态留着**：转视角只是临时换个看法，用户口径是「除非自己点不再记牌，
        // 否则不许自动关」，所以转回来自己会重新铺上。
        if (gameInfo.swaped)
        {
            eraseDeckMemoFaces();
            return;
        }
        // 观战 / 双打：这个功能根本无从谈起（拿不到「我方卡组」的归属）→ 直接退干净。
        if (isObserver || Program.I().room == null || Program.I().room.mode == 2)
        {
            endDeckMemo();
            return;
        }
        string src;
        string dropped;
        List<string> consumed = QuickTestTrace.Enabled ? new List<string>() : null;
        List<int> codes = deckMemoRemainingCodes(out src, out dropped, consumed);
        if (codes == null)
        {
            // 这一拍拿不到卡组名单（换备瞬间 `deckStrings` 与 `deck.Main` 不同步、录像信息还没就绪……）
            // → 只擦卡面，**不关记牌**。理由同转视角：用户口径是「除非自己点不再记牌，否则不许自动关」，
            // 一个瞬时的数据问题不该把玩家的开关吃掉。只在「确实有卡面要擦」的那一拍落一行痕迹。
            if (deckMemoLastApplied.Length > 0)
            {
                QuickTestTrace.Log("memo", "deck_memo source missing -> faces erased (state kept)");
            }
            eraseDeckMemoFaces();
            return;
        }
        List<gameCard> target = new List<gameCard>();
        collectShowedMyDeckCards(target);
        // 上一拍碰过、这一拍已经不在展示集合里的牌：擦回未知（例：刚被抽走的那张）。
        //
        // ⛔ 必须用 `erase_memo_code()`（只擦 `memoFaceOwned == true` 的）而**不是** `erase_data()`。
        // 抽卡/检索/回收走的是 `GCS_cardMove`，搬的是**同一个 gameCard 对象**（Deck → Hand 就地搬），
        // 引擎紧接着 `set_code(真码)` 认定了它；照「不在展示集合里」就 erase_data 的话，
        // 手牌上那张牌会被擦成未知 —— 且 `data.Id == 0` 时点它发不出应答，于是「拿到手却不能用」。
        // 归属标记由 `gameCard.set_code(code>0)` 自动交还，所以这里天然只擦自己写过的东西。
        for (int i = 0; i < deckMemoTouched.Count; i++)
        {
            gameCard c = deckMemoTouched[i];
            if (c == null || c.gameObject == null)
            {
                continue;
            }
            if (target.Contains(c) == false)
            {
                c.erase_memo_code();
            }
        }
        // ⚠ 这里**故意不清空** `deckMemoTouched`。
        //
        // 清空会在同一帧把刚擦掉的牌从跟踪表里摘掉，而 `deckMemoPicsTick` 只看表里的牌 ——
        // 于是「贴图回落成卡背」这一步再也报不出来：数据确实擦干净了，却拿不出任何
        // 「玩家看到的是卡背」的证据（验收判据 E 就是这样被噎住的，看着像没擦，其实是没报）。
        // 现在改成：擦完先留在表里，由 picsTick 亲眼看它们回落成 0 之后再摘。
        // （留着无害：`erase_memo_code()` 认归属标记，对已经交还所有权的牌是空操作。）
        for (int i = 0; i < target.Count; i++)
        {
            gameCard c = target[i];
            if (i < codes.Count)
            {
                // 只写「还未知的」或「本来就是记牌写的」：引擎已经认定过编号的牌**绝不覆盖**
                // （例：卡组里被效果公开过的牌，真码比我们的临时码权威，覆盖只会把真信息弄丢）。
                if (c.memoFaceOwned || c.get_data().Id == 0)
                {
                    c.set_memo_code(codes[i]);
                }
                else if (QuickTestTrace.Enabled)
                {
                    // 记牌期间出现这条 = 「展示集合里混进了引擎已认定的牌」，
                    // 正是抽卡/检索/回收在动卡组留下的痕迹（排查抽卡那类 bug 就看它）。
                    QuickTestTrace.Log("memo", "deck_memo skip-engine-owned id=" + c.get_data().Id
                        + " loc=" + c.p.location + " ctrl=" + c.p.controller + " slot=" + i);
                }
            }
            else
            {
                // 场上比名单多出来的槽位 → 未知卡（口径 #3）
                c.erase_memo_code();
            }
            // 跟踪表只装「面归我们管」的牌：picsTick 的举证与退出时的擦除都只看它。
            // ⚠ 必须去重：擦除后旧条目会留在表里（好让 picsTick 看到回落），这批牌被重新铺上时
            // 会再走一次这里 —— 重复入表会让 pics 签名多出一份、与逐槽 ids 对不上
            // （判据 D2/G2 就会假失败）。
            if (c.memoFaceOwned && deckMemoTouched.Contains(c) == false)
            {
                deckMemoTouched.Add(c);
            }
        }
        if (QuickTestTrace.Enabled)
        {
            // 只在「铺上去的结果变了」时落盘，否则每拍 realize 都会刷一行。
            string sig = "";
            for (int i = 0; i < target.Count; i++)
            {
                sig += (i == 0 ? "" : ",") + target[i].get_data().Id;
            }
            if (sig != deckMemoLastApplied)
            {
                deckMemoLastApplied = sig;
                QuickTestTrace.Log("memo", "deck_memo applied: src=" + src
                    + " left=" + codes.Count + " slots=" + target.Count
                    + " open=" + deckMemoOn
                    + " dropped=" + (dropped.Length > 0 ? dropped : "(none)")
                    + " ids=" + (sig.Length > 0 ? sig : "(none)"));
                // 同拍把「被算作已离场」的牌逐张报一份（算法自己的扫描结果，不是另写的一套）：
                // 脚本拿它从 ydk 名单里减掉，算出的多重集必须逐项等于上面的 ids。
                string c = "";
                for (int i = 0; i < consumed.Count; i++)
                {
                    c += (i == 0 ? "" : " ") + consumed[i];
                }
                QuickTestTrace.Log("memo", "deck_memo consumed " + (c.Length > 0 ? c : "(none)"));
            }
        }
    }

    /// <summary>上一拍铺上去的逐槽 Id（逗号分隔），用于「只看变化」的落盘节流。空串 = 还没铺过。</summary>
    string deckMemoLastApplied = "";

    /// <summary>上一拍看到的「卡面实际贴图 code」签名，用于证明贴图真的跟着换了。</summary>
    string deckMemoLastPics = "";

    /// <summary>
    /// 排查用：报一次「铺上去之后，卡面贴图实际变成了什么」。
    ///
    /// 为什么单开一条：`set_code()` 只改 `data.Id`，贴图是 `gameCard.card_picture_handler()`
    /// 在**之后某一帧**才换的；只看 `[memo] deck_memo applied` 的 ids 只能证明「数据改了」，
    /// 证明不了「玩家真的看到卡面了」。这里报 `loaded_cardPictureCode`，它变成真 code 才算数。
    /// 只在签名变化时落盘，所以不会刷日志。
    /// </summary>
    void deckMemoPicsTick()
    {
        if (!QuickTestTrace.Enabled)
        {
            return;
        }
        // 先把已经死掉的对象摘掉：对象池回收之后它们既报不出贴图，还会往签名里塞 -1，
        // 把「逐槽全 0」这个判据顶掉（判据 E 要的正是逐槽都是 0）。
        for (int i = deckMemoTouched.Count - 1; i >= 0; i--)
        {
            gameCard d = deckMemoTouched[i];
            if (d == null || d.gameObject == null)
            {
                deckMemoTouched.RemoveAt(i);
            }
        }
        if (deckMemoTouched.Count == 0)
        {
            return;
        }
        string s = "";
        bool alive = false;
        bool allZero = true;
        for (int i = 0; i < deckMemoTouched.Count; i++)
        {
            gameCard c = deckMemoTouched[i];
            int pic = c.debugFacePictureCode;
            alive = true;
            if (pic != 0)
            {
                allZero = false;
            }
            s += (i == 0 ? "" : ",") + pic;
        }
        if (s != deckMemoLastPics)
        {
            deckMemoLastPics = s;
            QuickTestTrace.Log("memo", "deck_memo pics=" + s
                + " state=" + (deckMemoOn ? "on" : "reverting"));
        }
        // 贴图**真的**回落到未知、而且这些牌已经不再归我们管（记牌态关了，或者刚被
        // `applyDeckMemoCodes_core` 擦掉）→ 这一步已经报出去了，可以把它们从表里摘掉。
        //
        // 摘空时顺手复位签名：同一批牌下次再被铺上时，pics 必须能重新报一次。
        // 不复位的话签名没变就永远不落盘 —— 验收判据 G2（「重新摊开后贴图又变成真 code」）
        // 会看不到任何新行，看着像没生效。
        if (alive && allZero)
        {
            for (int i = deckMemoTouched.Count - 1; i >= 0; i--)
            {
                if (deckMemoTouched[i].memoFaceOwned == false)
                {
                    deckMemoTouched.RemoveAt(i);
                }
            }
            if (deckMemoTouched.Count == 0)
            {
                deckMemoLastPics = "";
            }
        }
    }

    /// <summary>
    /// 只把记牌铺上去的卡面擦回未知，**不动记牌态本身**。收摊（「确认完毕」）与
    /// 退出记牌都走这里，区别只在调用方要不要顺带把 `deckMemoOn` 复位。
    ///
    /// ⛔ 擦除必须走 `erase_memo_code()`：它认 `memoFaceOwned`，只擦记牌自己写过的面。
    /// 直接 `erase_data()` 会把「引擎已经认定过编号的牌」也擦成 0。
    /// </summary>
    void eraseDeckMemoFaces()
    {
        deckMemoLastApplied = "";
        for (int i = 0; i < deckMemoTouched.Count; i++)
        {
            gameCard c = deckMemoTouched[i];
            if (c == null || c.gameObject == null)
            {
                continue;
            }
            c.erase_memo_code();
        }
        // ⚠ 这里**不**清空 deckMemoTouched：贴图要再等一两帧才换回卡背，
        // 留着跟踪表好让 deckMemoPicsTick 把「回落到未知」这件事也报出来，
        // 由它在确认全 0 之后自行清掉。
    }

    /// <summary>
    /// 退出记牌态：擦回卡面 + 复位**记牌数据状态**。
    ///
    /// ⛔ 这里**不碰按钮**（不摘、不改 `deckMemoHintState`）：按钮的挂/摘/换文案统一由
    /// `syncDeckMemoButton()` 每拍决定。原因有二：
    /// ・本函数在「牌还摊着」时也会被调（点「不再记牌」正是这种情形），此时按钮应当
    ///   继续留在原位只是换个文案；在这里摘掉的话 `syncDeckMemoButton` 下一拍又得重挂，
    ///   按钮会「缩回底部再滑上来」闪一下，而且白多一轮销毁/新建；
    /// ・`syncDeckMemoButton` 判「有没有挂过」靠 `deckMemoHintState != -1`，
    ///   这里把它复位成 -1 会让那条路径再也报不出 `button off`（验收判据 F 就看不到了）。
    /// </summary>
    public void endDeckMemo()
    {
        int dbgCall = ++deckMemoDbgEnd;
        if (QuickTestTrace.Enabled && (dbgCall <= 8 || dbgCall % 200 == 0))
        {
            QuickTestTrace.Log("memo", "dbg end#" + dbgCall
                + " tid=" + System.Threading.Thread.CurrentThread.ManagedThreadId
                + " inst=" + GetHashCode() + " on(entry)=" + deckMemoOn
                + " touched=" + deckMemoTouched.Count);
        }
        deckMemoOn = false;
        eraseDeckMemoFaces();
    }

    /// <summary>
    /// 记牌在**当前这一局**到底适不适用。与按钮出现条件共用同一套排除项，所以抽出来单放：
    /// ・转换视角（`gameInfo.swaped`）：`controllerBased` 和 `p.controller` 被整体翻转
    ///   （`GCS_swapALL()`），**画面下方的卡堆已经不是我的卡组**了 —— 继续铺的话会把我方
    ///   卡组的剩余码铺到对手的卡堆上，比不显示更糟；
    /// ・观战（`isObserver`）：自己没上报过卡组，`deckMemoOrder()` 本来也拿不到；
    /// ・双打（`room.mode == 2`）：四人局里「我方」的归属与视角都会错位。
    /// </summary>
    bool deckMemoContextOk()
    {
        if (gameInfo.swaped || isObserver)
        {
            return false;
        }
        if (Program.I().room == null || Program.I().room.mode == 2)
        {
            return false;
        }
        return true;
    }

    /// <summary>
    /// 右侧那个按钮该不该出现。出现条件（见计划 3 节）：
    /// 场景可用（`deckMemoContextOk()`：非转视角、非观战、非双打），且「有牌在展示」
    /// 并**其中包含我方的卡组牌**（不然按钮点了没意义）；
    /// 另外还得真的拿得到本局卡组的顺序表（拿不到就不出，避免点了没反应）。
    ///
    /// ⚠ 两态按钮**共用这一套出现条件**（用户口径 2026-09-17 修订）：**不常驻**，
    /// 跟「卡组记牌」一样只在「确认过卡组、牌摊在场上」时冒出来；开着的时候文案是「不再记牌」。
    /// 记牌态本身仍然是**持久**的（「确认完毕」收摊不关态，见 `clearAllShowed`），但按钮跟着
    /// 摊位一起收——下次再确认卡组，它会带「不再记牌」的文案重新出现，玩家随时能关。
    /// </summary>
    /// <summary>
    /// 上面那条的带理由版本：`why` 一定与真正的判据同源（走的同一段代码），
    /// 排查「按钮怎么没出来」时直接看它，不要另写一套条件去猜。
    /// 取值：`ok` / `ctx`（转视角·观战·双打）/ `not-shown`（一张牌都没摊开）/
    /// `no-my-deck`（摊着的里面没有我方卡组牌）/ `no-order`（拿不到本局卡组名单）。
    /// </summary>
    bool deckMemoButtonWanted(out string why)
    {
        why = "ctx";
        if (deckMemoContextOk() == false)
        {
            return false;
        }
        why = "not-shown";
        if (someCardIsShowed == false)
        {
            return false;
        }
        why = "no-my-deck";
        if (hasShowedMyDeckCard() == false)
        {
            return false;
        }
        why = "no-order";
        string src;
        if (deckMemoOrder(out src) == null)
        {
            return false;
        }
        why = "ok";
        return true;
    }

    bool deckMemoButtonWanted()
    {
        string why;
        return deckMemoButtonWanted(out why);
    }

    /// <summary>当前挂着的那颗记牌按钮（跳过正在淡出的死链）。</summary>
    gameUIbutton findDeckMemoButton()
    {
        if (gameInfo == null)
        {
            return null;
        }
        for (int i = 0; i < gameInfo.allHashedButtons.Count; i++)
        {
            gameUIbutton hb = gameInfo.allHashedButtons[i];
            if (hb != null && hb.dying == false && hb.gameObject != null && hb.hashString == "deck_memo")
            {
                return hb;
            }
        }
        return null;
    }

    /// <summary>两态文案。</summary>
    static string deckMemoText(int want)
    {
        return InterString.Get(want == 1 ? "不再记牌@ui" : "卡组记牌@ui");
    }

    /// <summary>
    /// 就地换文案：**不摘不挂**。
    ///
    /// ⛔ 别改回「remove + add」：`removeHashedButton` 只是把条目标成 `dying`（条目要等
    /// `gameInfo.Update()` 跑到才移除），`addHashedButton` 又新建一个同名的 GameObject，
    /// 于是同一瞬间列表里会**同时存在两条 hashString=="deck_memo"** —— 而
    /// `gameInfo.listenerForClicked` 是「按 hashString+response 匹配、匹配上就派发」，
    /// 不比对对象身份，一次物理点击会被派发多次，每次又各自对称地摘/挂，越滚越多。
    /// 文案本来就地改一个 UILabel 就够了。
    /// </summary>
    void setDeckMemoButtonText(gameUIbutton hb, int want)
    {
        string t = deckMemoText(want);
        UIHelper.trySetLableText(hb.gameObject, "hint_", t);
        iconSetForButton ic = hb.gameObject.GetComponent<iconSetForButton>();
        if (ic != null)
        {
            ic.setText(t);
        }
    }

    /// <summary>每拍同步右侧那个按钮（挂/摘 + 两态文案切换）。</summary>
    void syncDeckMemoButton()
    {
        if (deckMemoButtonWanted() == false)
        {
            if (deckMemoHintState != -1)
            {
                deckMemoHintState = -1;
                gameInfo.removeHashedButton("deck_memo");
                // ⚠ 必须带上记牌态：按钮现在跟摊位同生共死，收摊时它也会被摘 ——
                // 光看 `button off` 分不出「玩家关了记牌」还是「只是把卡组收起来了」。
                QuickTestTrace.Log("memo", "button off state=" + (deckMemoOn ? "on" : "off"));
            }
            return;
        }
        int want = deckMemoOn ? 1 : 0;
        gameUIbutton cur = findDeckMemoButton();
        if (deckMemoHintState == want && cur != null)
        {
            return;
        }
        if (cur == null)
        {
            // 还没有 → 挂一颗（文案按当前态给）。
            gameInfo.addHashedButton("deck_memo", 0, superButtonType.see, deckMemoText(want));
        }
        else
        {
            // 有了、只是换了态 → 就地改文案（理由见 setDeckMemoButtonText）。
            setDeckMemoButtonText(cur, want);
        }
        deckMemoHintState = want;
    }

    int lastMemoBtnDumpMs = -1;
    string lastMemoBtnDump = "";

    /// <summary>
    /// 排查用：报记牌按钮当前的文案与**屏幕坐标**，位置变了就报一次（不刷日志）。
    ///
    /// ⚠ 为什么必须做成「每拍盯位置」而不是在 `addHashedButton` 之后报一次：
    /// 新挂的哈希按钮初始 `localPosition = (0,-120,0)`（`gameInfo.cs:206`），
    /// **之后才 tween 到它在右侧按钮栏里的最终槽位**（`gameInfo.cs:142` 的 `-145 - j*50`）。
    /// 在挂上去那一刻报出来的坐标是「入场途中」的位置，照着点会点到旁边那颗按钮
    /// （实测：报 y=492，那个高度上其实是「确认完毕」，一按就把展示收掉了）。
    /// 坐标给的是**碰撞盒中心**的客户区坐标（左上原点），与 `[opt]` 同一套换算。
    ///
    /// 也顺带覆盖「别的按钮进出导致整列重排」的情况 —— 那种时候按钮也会挪位置。
    /// </summary>
    void deckMemoButtonTick()
    {
        if (!QuickTestTrace.Enabled || gameInfo == null)
        {
            return;
        }
        if (Program.TimePassed() - lastMemoBtnDumpMs < 250)
        {
            return;
        }
        lastMemoBtnDumpMs = Program.TimePassed();
        // 「按钮不在」也要带上**为什么不在**：两态按钮共用出现条件之后，
        // ・`none state=on`  = 收摊后按钮跟着收走（判据 E3 的正例）；
        // ・`none why=...`   = 确实不该出现，理由与真正的判据同源（`deckMemoButtonWanted(out why)`）。
        // 光报 none 分不出「关了」「只是没摊开」「卡组名单还没就绪」——上一轮就是这么白等 9 秒的。
        string why;
        deckMemoButtonWanted(out why);
        string s = "btn deck_memo none state=" + (deckMemoOn ? "on" : "off")
            + " why=" + why
            + " showed=" + someCardIsShowed
            + " mydeck=" + hasShowedMyDeckCard();
        for (int i = 0; i < gameInfo.allHashedButtons.Count; i++)
        {
            gameUIbutton hb = gameInfo.allHashedButtons[i];
            if (hb == null || hb.gameObject == null || hb.dying || hb.hashString != "deck_memo"
                || hb.gameObject.activeInHierarchy == false)
            {
                continue;
            }
            Vector3 sp = Program.camera_main_2d.WorldToScreenPoint(ButtonWorldCenter(hb.gameObject));
            s = "btn deck_memo hint=" + (deckMemoOn ? "不再记牌" : "卡组记牌")
                + " screen=(" + Mathf.RoundToInt(sp.x) + "," + Mathf.RoundToInt(Screen.height - sp.y) + ")"
                + " on=" + deckMemoOn
                + " local=(" + Mathf.RoundToInt(hb.gameObject.transform.localPosition.x)
                + "," + Mathf.RoundToInt(hb.gameObject.transform.localPosition.y) + ")";
            break;
        }
        if (s == lastMemoBtnDump)
        {
            return;
        }
        lastMemoBtnDump = s;
        QuickTestTrace.Log("memo", s);
    }

    /// <summary>点「卡组记牌」/「不再记牌」：两态互切。只在帧末被调（见 deckMemoToggleTick）。</summary>
    public void toggleDeckMemo()
    {
        int dbgCall = ++deckMemoDbgToggle;
        if (QuickTestTrace.Enabled && (dbgCall <= 8 || dbgCall % 50 == 0))
        {
            QuickTestTrace.Log("memo", "dbg toggle#" + dbgCall
                + " tid=" + System.Threading.Thread.CurrentThread.ManagedThreadId
                + " inst=" + GetHashCode() + " on(entry)=" + deckMemoOn);
        }
        if (deckMemoOn)
        {
            endDeckMemo();
        }
        else
        {
            deckMemoOn = true;
            applyDeckMemoCodes();   // 立刻铺上卡面，不用等下一拍 realize
        }
        // ⛔ 不要在这里把 deckMemoHintState 置 -1「逼重挂」：换了态只需就地改文案，
        // syncDeckMemoButton 下一拍会自己比对 hintState != want 并就地改。
        // 置 -1 反而会触发一次 remove + add（按钮缩回底部再滑上来闪一下）。
        realize();
        toNearest();
    }

    #endregion

    /// <summary>排查用：记牌态调试计数器（只在 qt_debug.on 下写 log）。</summary>
    public static int deckMemoDbgRealize = 0;

    public void realize(bool rush = false)
    {
        deckMemoDbgRealize++;
        if (QuickTestTrace.Enabled && deckMemoDbgRealize % 200 == 0)
        {
            QuickTestTrace.Log("memo", "dbg realize#" + deckMemoDbgRealize
                + " tid=" + System.Threading.Thread.CurrentThread.ManagedThreadId
                + " inst=" + GetHashCode() + " on=" + deckMemoOn);
        }
        someCardIsShowed = false;
        float real = (Program.fieldSize - 1) * 0.9f + 1f;
        for (int i = 0; i < cards.Count; i++) if (cards[i].gameObject.activeInHierarchy)
            {
                cards[i].cookie_cared = false;
                cards[i].p_line_off();
                cards[i].sortButtons();
                cards[i].opMonsterWithBackGroundCard = false;
                cards[i].isMinBlockMode = false;
                cards[i].overFatherCount = 0;
            }

        List<gameCard> to_clear = new List<gameCard>();
        for (int i = 0; i < cards.Count; i++) if (cards[i].gameObject.activeInHierarchy)
            {
                if (cards[i].p.location == (uint)CardLocation.Unknown)
                {
                    to_clear.Add(cards[i]);
                }
            }

        for (int i = 0; i < to_clear.Count; i++)
        {
            to_clear[i].hide();
        }

        //for (int i = 0; i < cards.Count; i++) if (cards[i].gameObject.activeInHierarchy)
        //        if (cards[i].cookie_cared == false)
        //        {
        //            if (winner == 2 || (winner != -1 && cards[i].p.controller != winner))
        //            {
        //                cards[i].cookie_cared = true;
        //                cards[i].UA_give_condition(gameCardCondition.still_unclickable);
        //                if (cards[i].p.controller == 0)
        //                {
        //                    cards[i].UA_give_position(new Vector3(UnityEngine.Random.Range(-15f, 15f), UnityEngine.Random.Range(-5f, 5f), UnityEngine.Random.Range(-5f, -25f)));

        //                }
        //                else
        //                {
        //                    cards[i].UA_give_position(new Vector3(UnityEngine.Random.Range(-20f, 20f), UnityEngine.Random.Range(0f, 5f), UnityEngine.Random.Range(5f, 22f)));

        //                }
        //                cards[i].UA_give_rotation(new Vector3(UnityEngine.Random.Range(-180f, 180f), UnityEngine.Random.Range(-180f, 180f), UnityEngine.Random.Range(-180f, 180f)));
        //                cards[i].UA_flush_all_gived_witn_lock(rush);
        //            }
        //        }

        for (int i = 0; i < cards.Count; i++) if (cards[i].gameObject.activeInHierarchy)
                if (cards[i].cookie_cared == false)
                {
                    if ((cards[i].p.location & (UInt32)CardLocation.Overlay) == 0)
                    {
                        if ((cards[i].p.location & (UInt32)CardLocation.SpellZone) > 0)
                        {
                            cards[i].isShowed = false;
                        }
                        if ((cards[i].p.location & (UInt32)CardLocation.MonsterZone) > 0)
                        {
                            cards[i].isShowed = false;
                        }
                    }
                    if ((((cards[i].p.location & (UInt32)CardLocation.Hand) > 0) && (cards[i].p.controller == 0)) || ((cards[i].p.location & (UInt32)CardLocation.Unknown) > 0))
                    {
                        cards[i].isShowed = true;
                    }
                    else
                    {
                        if (cards[i].isShowed && cards[i].forSelect == false)
                        {
                            someCardIsShowed = true;
                        }
                    }
                }

        for (int i = 0; i < cards.Count; i++) if (cards[i].gameObject.activeInHierarchy)
            {
                if (cards[i].p.location == (uint)CardLocation.Search)
                {
                    cards[i].isShowed = true;
                }
            }

        // 记牌态：在「哪些牌在展示」定下来之后、排布展示行之前，把卡面码铺上去。
        // 放在这里而不是按钮点击处，是为了每拍从现状重算 —— 抽卡/检索/回收/撤回都自动跟上，
        // 不需要监听任何增量消息（见计划 4.2）。`someCardIsShowed` 在上面的循环里已经算好。
        if (deckMemoOn)
        {
            applyDeckMemoCodes();
        }

        List<List<gameCard>> lines = new List<List<gameCard>>();
        UInt32 preController = 9999;
        UInt32 preLocation = 9999;
        for (int i = 0; i < cards.Count; i++) if (cards[i].gameObject.activeInHierarchy)
                if (cards[i].cookie_cared == false)
                {
                    if (cards[i].isShowed == true)
                    {
                        int lineMax = 8;
                        if (lines.Count <= 1)
                        {
                            lineMax = 6;
                        }
                        if (
                        preController != cards[i].p.controller
                        ||
                        preLocation != cards[i].p.location
                        ||
                        lines[lines.Count - 1].Count == lineMax
                            )
                        {
                            lines.Add(new List<gameCard>());
                        }
                        lines[lines.Count - 1].Add(cards[i]);
                        preController = cards[i].p.controller;
                        preLocation = cards[i].p.location;
                    }
                }

        if (lines.Count >= 2)
        {
            var lastLine = lines[lines.Count - 1];
            var preLine = lines[lines.Count - 2];
            if (lastLine.Count == 1)
            {
                if (preLine.Count > 0)   
                {
                    if (lastLine[0].p.controller == preLine[0].p.controller)
                    {
                        if (lastLine[0].p.location == preLine[0].p.location)
                        {
                            preLine.Add(lastLine[0]);
                            lines.Remove(lastLine);
                        }
                    }
                }
            }
        }

        for (int line_index = 0; line_index < lines.Count; line_index++)
        {
            for (int index = 0; index < lines[line_index].Count; index++)
            {
                Vector3 want_position = Vector3.zero;
                want_position.y = 0;
                want_position.z = -line_index * 5 - 3f - 15f * Program.fieldSize;
                if (line_index == 0)
                {
                    want_position.x = UIHelper.get_left_right_indexEnhanced(-10, 10, index, lines[line_index].Count, 5);
                }
                else
                {
                    want_position.x = UIHelper.get_left_right_indexEnhanced(-15, 15, index, lines[line_index].Count, 7);
                }
                lines[line_index][index].cookie_cared = true;
                lines[line_index][index].UA_give_condition(gameCardCondition.floating_clickable);
                lines[line_index][index].UA_give_position(want_position);
                lines[line_index][index].UA_give_rotation(new Vector3(-30, 0, 0));
                lines[line_index][index].UA_flush_all_gived_witn_lock(rush);
            }
        }

        gameField.isLong = false;

        List<gameCard> op_m = new List<gameCard>();

        List<gameCard> op_s = new List<gameCard>();

        for (int i = 0; i < cards.Count; i++) if (cards[i].gameObject.activeInHierarchy)
                {
                    if (cards[i].p.controller == 1)
                    {
                        if ((cards[i].p.location & (UInt32)CardLocation.Overlay) == 0)
                        {
                            if ((cards[i].p.location & (UInt32)CardLocation.MonsterZone) > 0)
                            {
                                op_m.Add(cards[i]);
                            }
                            if ((cards[i].p.location & (UInt32)CardLocation.SpellZone) > 0)
                            {
                                op_s.Add(cards[i]);
                            }
                        }
                    }
                }
        for (int m = 0; m < op_m.Count; m++)
        {
            if ((op_m[m].p.position & (UInt32)CardPosition.FaceUp) > 0)
            {
                for (int s = 0; s < op_s.Count; s++)
                {
                    if (op_m[m].p.sequence == op_s[s].p.sequence)
                    {
                        if (op_m[m].p.sequence < 5)
                        {
                            op_m[m].opMonsterWithBackGroundCard = true;
                            //op_m[m].isMinBlockMode = true;
                            if (Program.getVerticalTransparency() >= 0.5f)
                            {
                                gameField.isLong = Program.longField;    //这个设定恢复（？）了
                            }
                        }
                    }
                }
            }
        }

        gameCard[] opM = new gameCard[7];
        gameCard[] meM = new gameCard[7];
        for (int i = 0; i < 7; i++)
        {
            opM[i] = null;
            meM[i] = null;
        }
        for (int i = 0; i < cards.Count; i++) if (cards[i].gameObject.activeInHierarchy)
            {
                if ((cards[i].p.location & (UInt32)CardLocation.Overlay) == 0)
                {
                    if ((cards[i].p.location & (UInt32)CardLocation.MonsterZone) > 0)
                    {
                        if (cards[i].p.sequence >= 0 && cards[i].p.sequence <= 6)
                        {
                            if ((cards[i].p.position & (UInt32)CardPosition.FaceUp) > 0)
                            {
                                if (cards[i].p.controller == 1)
                                {
                                    opM[cards[i].p.sequence] = cards[i];
                                }
                                else
                                {
                                    meM[cards[i].p.sequence] = cards[i];
                                }
                            }
                        }
                    }
                }
            }

        if (opM[1] != null)
        {
            if (opM[5]!=null)
            {
                opM[5].isMinBlockMode = true;
            }
            if (meM[6] != null)
            {
                meM[6].isMinBlockMode = true;
            }
        }

        if (opM[3] != null)
        {
            if (opM[6] != null)
            {
                opM[6].isMinBlockMode = true;
            }
            if (meM[5] != null)
            {
                meM[5].isMinBlockMode = true;
            }
        }

        if (opM[6] != null || meM[5] != null)
        {
            if (meM[1] != null)
            {
                meM[1].isMinBlockMode = true;
            }
        }

        if (opM[5] != null || meM[6] != null)
        {
            if (meM[3] != null)
            {
                meM[3].isMinBlockMode = true;
            }
        }


        gameCard[,] vvv = new gameCard[10,10];

        for (int i = 0; i < cards.Count; i++) if (cards[i].gameObject.activeInHierarchy)
            {
                if ((cards[i].p.location & (UInt32)CardLocation.Overlay) == 0)
                {
                    if ((cards[i].p.location & (UInt32)CardLocation.MonsterZone) > 0)
                    {
                        if (cards[i].p.sequence >= 0 && cards[i].p.sequence <= 6)
                        {
                            if ((cards[i].get_data().Type & (UInt32)CardType.Link) > 0)
                            {
                                if ((cards[i].p.position & (UInt32)CardPosition.FaceUp) > 0)
                                {
                                    if (cards[i].p.controller == 1)
                                    {
                                        if (cards[i].p.sequence >= 0 && cards[i].p.sequence <= 4)
                                        {
                                            vvv[4, 4 - cards[i].p.sequence] = cards[i];
                                        }
                                        if (cards[i].p.sequence == 5)
                                        {
                                            vvv[3, 3] = cards[i];
                                        }
                                        if (cards[i].p.sequence == 6)
                                        {
                                            vvv[3, 1] = cards[i];
                                        }
                                    }
                                    else
                                    {
                                        if (cards[i].p.sequence >= 0 && cards[i].p.sequence <= 4)
                                        {
                                            vvv[2, cards[i].p.sequence] = cards[i];
                                        }
                                        if (cards[i].p.sequence == 5)
                                        {
                                            vvv[3, 1] = cards[i];
                                        }
                                        if (cards[i].p.sequence == 6)
                                        {
                                            vvv[3, 3] = cards[i];
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }


        List<GPS> linkPs = new List<GPS>();


        for (int curHang = 2; curHang <= 4; curHang++)
        {
            for (int curLie = 0; curLie <= 4; curLie++)
            {
                //if (vvv[curHang, curLie] != null)
                {
                    GPS currentGPS = new GPS();
                    currentGPS.location = (int)CardLocation.MonsterZone;
                    if (curHang == 4)
                    {
                        currentGPS.controller = 1;
                        currentGPS.sequence = (uint)(4 - curLie);
                    }
                    if (curHang == 3)
                    {
                        currentGPS.controller = 0;
                        if (currentGPS.sequence == 0)
                        {
                            continue;
                        }
                        if (currentGPS.sequence == 1)
                        {
                            currentGPS.sequence = 5;
                        }
                        if (currentGPS.sequence == 2)
                        {
                            continue;
                        }
                        if (currentGPS.sequence == 3)
                        {
                            currentGPS.sequence = 6;
                        }
                        if (currentGPS.sequence == 4)
                        {
                            continue;
                        }
                    }
                    if (curHang == 2)
                    {
                        currentGPS.controller = 0;
                        currentGPS.sequence = (uint)(curLie);
                    }

                    bool lighted = false;

                    if (curHang - 1 >= 0)
                        if (curLie - 1 >= 0)
                            if (vvv[curHang - 1, curLie - 1] != null)
                    {
                        gameCard card = vvv[curHang - 1, curLie - 1];
                        if (card.p.controller == 0)
                            if (card.get_data().HasLinkMarker(CardLinkMarker.TopRight))
                                lighted = true;
                        if (card.p.controller == 1)
                            if (card.get_data().HasLinkMarker(CardLinkMarker.BottomLeft))
                                lighted = true;
                    }

                        if (curLie - 1 >= 0)
                            if (vvv[curHang, curLie - 1] != null)
                    {
                            gameCard card = vvv[curHang, curLie - 1];
                        if (card.p.controller == 0)
                            if (card.get_data().HasLinkMarker(CardLinkMarker.Right))
                                lighted = true;
                        if (card.p.controller == 1)
                            if (card.get_data().HasLinkMarker(CardLinkMarker.Left))
                                lighted = true;
                    }
                        if (curLie - 1 >= 0)
                            if (vvv[curHang+1, curLie - 1] != null)
                    {
                            gameCard card = vvv[curHang + 1, curLie - 1];
                        if (card.p.controller == 0)
                            if (card.get_data().HasLinkMarker(CardLinkMarker.BottomRight))
                                lighted = true;
                        if (card.p.controller == 1)
                            if (card.get_data().HasLinkMarker(CardLinkMarker.TopLeft))
                                lighted = true;
                    }
                    if (curHang - 1 >= 0)
                            if (vvv[curHang - 1, curLie] != null)
                    {
                            gameCard card = vvv[curHang - 1, curLie];
                        if (card.p.controller == 0)
                            if (card.get_data().HasLinkMarker(CardLinkMarker.Top))
                                lighted = true;
                        if (card.p.controller == 1)
                            if (card.get_data().HasLinkMarker(CardLinkMarker.Bottom))
                                lighted = true;
                    }

                    if (vvv[curHang + 1, curLie] != null)
                    {
                        gameCard card = vvv[curHang + 1, curLie];
                        if (card.p.controller == 0)
                            if (card.get_data().HasLinkMarker(CardLinkMarker.Bottom))
                                lighted = true;
                        if (card.p.controller == 1)
                            if (card.get_data().HasLinkMarker(CardLinkMarker.Top))
                                lighted = true;
                    }
                    if (curHang - 1 >= 0)
                            if (vvv[curHang - 1, curLie + 1] != null)
                    {
                            gameCard card = vvv[curHang - 1, curLie + 1];
                        if (card.p.controller == 0)
                            if (card.get_data().HasLinkMarker(CardLinkMarker.TopLeft))
                                lighted = true;
                        if (card.p.controller == 1)
                            if (card.get_data().HasLinkMarker(CardLinkMarker.BottomRight))
                                lighted = true;
                    }

                    if (vvv[curHang, curLie + 1] != null)
                    {
                        gameCard card = vvv[curHang, curLie + 1];
                        if (card.p.controller == 0)
                            if (card.get_data().HasLinkMarker(CardLinkMarker.Left))
                                lighted = true;
                        if (card.p.controller == 1)
                            if (card.get_data().HasLinkMarker(CardLinkMarker.Right))
                                lighted = true;
                    }

                    if (vvv[curHang + 1, curLie + 1] != null)
                    {
                        gameCard card = vvv[curHang + 1, curLie + 1];
                        if (card.p.controller == 0)
                            if (card.get_data().HasLinkMarker(CardLinkMarker.BottomLeft))
                                lighted = true;
                        if (card.p.controller == 1)
                            if (card.get_data().HasLinkMarker(CardLinkMarker.TopRight))
                                lighted = true;
                    }

                    if (lighted)
                    {
                        linkPs.Add(currentGPS);
                    }

                }
            }
        }

        for (int i = 0; i < linkPs.Count; i++)
        {
            bool showed = false;
            for (int a = 0; a < linkMaskList.Count; a++)
            {
                if (linkMaskList[a].p.controller == linkPs[i].controller && linkMaskList[a].p.sequence == linkPs[i].sequence)
                {
                    showed = true;
                }
            }
            if (showed == false)
            {
                linkMaskList.Add(makeLinkMask(linkPs[i]));
            }
        }

        List<linkMask> removeList = new List<linkMask>();

        for (int i = 0; i < linkMaskList.Count; i++)
        {
            bool deleted = true;
            for (int a = 0; a < linkPs.Count; a++)
            {
                if (linkMaskList[i].p.controller == linkPs[a].controller && linkMaskList[i].p.sequence == linkPs[a].sequence)
                {
                    deleted = false;
                }
            }
            if (deleted == true)
            {
                removeList.Add(linkMaskList[i]);
            }
        }

        for (int i = 0; i < removeList.Count; i++)
        {
            linkMaskList.Remove(removeList[i]);
            destroy(removeList[i].gameObject);
        }

        removeList.Clear();
        removeList = null;

        for (int i = 0; i < linkMaskList.Count; i++)
        {
            shift_effect(linkMaskList[i],Program.I().setting.setting.Vlink.value);
        }

        gameField.Update();
        //op hand
        List<gameCard> line = new List<gameCard>();
        for (int i = 0; i < cards.Count; i++) if (cards[i].gameObject.activeInHierarchy)
                if (cards[i].cookie_cared == false)
                {
                    if ((cards[i].p.location & (UInt32)CardLocation.Hand) > 0 && cards[i].p.controller == 1)
                    {
                        line.Add(cards[i]);
                    }
                }
        for (int index = 0; index < line.Count; index++)
        {
            Vector3 want_position = Vector3.zero;
            want_position.y = 0;
            if (gameField.isLong)
            {
                want_position.z = (19f + gameField.delat) * Program.fieldSize + index * 0.015f;
            }
            else
            {
                want_position.z = (19f) * Program.fieldSize + index * 0.015f;
            }
            want_position.x = UIHelper.get_left_right_indexEnhanced(10, -10, index, line.Count, 5);
            line[index].cookie_cared = true;
            line[index].UA_give_position(want_position);
            if (line[index].get_data().Id > 0)
            {
                line[index].UA_give_rotation(new Vector3(-30, 0, 0));
            }
            else
            {
                line[index].UA_give_rotation(new Vector3(-30, 0, 180));
            }
            line[index].UA_give_condition(gameCardCondition.floating_clickable);
            line[index].UA_flush_all_gived_witn_lock(rush);
        }

        //effects
        for (int i = 0; i < gameField.thunders.Count; i++)
        {
            gameField.thunders[i].needDestroy = true;
        }

        for (int i = 0; i < cards.Count; i++) if (cards[i].gameObject.activeInHierarchy)
            {
                List<gameCard> overlayed_cards = GCS_cardGetOverlayElements(cards[i]);
                int overC = 0;
                if (Program.getVerticalTransparency() > 0.5f)
                {
                    if ((cards[i].p.position & (Int32)CardPosition.FaceUp) > 0 && (cards[i].p.location & (Int32)CardLocation.Onfield) > 0)
                    {
                        overC = overlayed_cards.Count;
                    }
                }
                cards[i].set_overlay_light(overC);
                cards[i].set_overlay_see_button(overlayed_cards.Count > 0);
                for (int x = 0; x < overlayed_cards.Count; x++)
                {
                    overlayed_cards[x].overFatherCount = overlayed_cards.Count;
                    if (overlayed_cards[x].isShowed)
                    {
                        animation_thunder(overlayed_cards[x].gameObject, cards[i].gameObject);
                    }
                }
                foreach (var item in cards[i].target)
                {
                    if ((item.p.location & (UInt32)CardLocation.SpellZone) > 0 || (item.p.location & (UInt32)CardLocation.MonsterZone) > 0)
                    {
                        animation_thunder(item.gameObject, cards[i].gameObject);
                    }
                }
            }

        List<thunder_locator> needRemoveThunder = new List<thunder_locator>();
        for (int i = 0; i < gameField.thunders.Count; i++)
        {
            if (gameField.thunders[i].needDestroy == true)
            {
                needRemoveThunder.Add(gameField.thunders[i]);
            }
        }
        for (int i = 0; i < needRemoveThunder.Count; i++)
        {
            gameField.thunders.Remove(needRemoveThunder[i]);
            destroy(needRemoveThunder[i].gameObject);
        }
        needRemoveThunder.Clear();


        //p effect
        gameField.relocatePnums(Program.I().setting.setting.Vpedium.value);
        if (Program.I().setting.setting.Vpedium.value == true) 
        {
            List<gameCard> my_p_cards = new List<gameCard>();

            List<gameCard> op_p_cards = new List<gameCard>();

            for (int i = 0; i < cards.Count; i++) if (cards[i].gameObject.activeInHierarchy)
                    if (cards[i].cookie_cared == false)
                    {
                        if ((cards[i].p.location & (UInt32)CardLocation.SpellZone) > 0)
                        {
                            if (cards[i].p.sequence == 0 || cards[i].p.sequence == 4)
                            {
                                if ((cards[i].get_data().Type & (int)CardType.Pendulum) > 0)
                                {
                                    if (cards[i].p.controller == 0)
                                    {
                                        my_p_cards.Add(cards[i]);
                                    }
                                    else
                                    {
                                        op_p_cards.Add(cards[i]);
                                    }
                                }
                            }
                        }
                    }

            if (MasterRule >= 4)
            {
                if (my_p_cards.Count == 2)
                {
                    Debug.Log("oh");
                    gameField.me_left_p_num.GetComponent<number_loader>().set_number((int)my_p_cards[0].get_data().LScale, 3);
                    gameField.me_right_p_num.GetComponent<number_loader>().set_number((int)my_p_cards[1].get_data().LScale, 0);
                    gameField.mePHole = true;
                    my_p_cards[0].cookie_cared = true;
                    my_p_cards[0].UA_give_position(new Vector3(-10.1f * real + 1, 5, -11.5f * real - 1));
                    my_p_cards[0].UA_give_rotation(new Vector3(-60, -45, 0));
                    my_p_cards[0].UA_give_condition(gameCardCondition.floating_clickable);
                    my_p_cards[0].UA_flush_all_gived_witn_lock(rush);
                    my_p_cards[1].cookie_cared = true;
                    my_p_cards[1].UA_give_position(new Vector3(9.62f * real - 1, 5, -11.5f * real - 1));
                    my_p_cards[1].UA_give_rotation(new Vector3(-60, 45, 0));
                    my_p_cards[1].UA_give_condition(gameCardCondition.floating_clickable);
                    my_p_cards[1].UA_flush_all_gived_witn_lock(rush);
                    my_p_cards[0].p_line_on();
                    my_p_cards[1].p_line_on();
                }
                else
                {
                    gameField.me_left_p_num.GetComponent<number_loader>().set_number(-1, 3);
                    gameField.me_right_p_num.GetComponent<number_loader>().set_number(-1, 3);
                    gameField.mePHole = false;
                }
                if (op_p_cards.Count == 2)
                {
                    gameField.op_left_p_num.GetComponent<number_loader>().set_number((int)op_p_cards[1].get_data().LScale, 0);
                    gameField.op_right_p_num.GetComponent<number_loader>().set_number((int)op_p_cards[0].get_data().LScale, 3);
                    gameField.opPHole = true;
                    op_p_cards[0].cookie_cared = true;
                    op_p_cards[0].UA_give_position(new Vector3(9.62f * real - 1, 5, 11.5f * real - 1));
                    op_p_cards[0].UA_give_rotation(new Vector3(-90, 45, 0));
                    op_p_cards[0].UA_give_condition(gameCardCondition.floating_clickable);
                    op_p_cards[0].UA_flush_all_gived_witn_lock(rush);
                    op_p_cards[1].cookie_cared = true;
                    op_p_cards[1].UA_give_position(new Vector3(-10.1f * real + 1, 5, 11.5f * real - 1));
                    op_p_cards[1].UA_give_rotation(new Vector3(-90, -45, 0));
                    op_p_cards[1].UA_give_condition(gameCardCondition.floating_clickable);
                    op_p_cards[1].UA_flush_all_gived_witn_lock(rush);
                    op_p_cards[0].p_line_on();
                    op_p_cards[1].p_line_on();

                }
                else
                {
                    gameField.op_left_p_num.GetComponent<number_loader>().set_number(-1, 3);
                    gameField.op_right_p_num.GetComponent<number_loader>().set_number(-1, 3);
                    gameField.opPHole = false;
                }
            }
            else
            {
                if (my_p_cards.Count == 2)
                {
                    gameField.me_left_p_num.GetComponent<number_loader>().set_number((int)my_p_cards[0].get_data().LScale, 3);
                    gameField.me_right_p_num.GetComponent<number_loader>().set_number((int)my_p_cards[1].get_data().LScale, 3);
                    gameField.mePHole = true;
                    my_p_cards[0].cookie_cared = true;
                    my_p_cards[0].UA_give_position(new Vector3(-15.2f * real, 5, -10f));
                    my_p_cards[0].UA_give_rotation(new Vector3(-90, -45, 0));
                    my_p_cards[0].UA_give_condition(gameCardCondition.floating_clickable);
                    my_p_cards[0].UA_flush_all_gived_witn_lock(rush);
                    my_p_cards[1].cookie_cared = true;
                    my_p_cards[1].UA_give_position(new Vector3(14.65f * real, 5, -10f));
                    my_p_cards[1].UA_give_rotation(new Vector3(-90, 45, 0));
                    my_p_cards[1].UA_give_condition(gameCardCondition.floating_clickable);
                    my_p_cards[1].UA_flush_all_gived_witn_lock(rush);
                    my_p_cards[0].p_line_on();
                    my_p_cards[1].p_line_on();
                }
                else
                {
                    gameField.me_left_p_num.GetComponent<number_loader>().set_number(-1, 3);
                    gameField.me_right_p_num.GetComponent<number_loader>().set_number(-1, 3);
                    gameField.mePHole = false;
                }
                if (op_p_cards.Count == 2)
                {
                    gameField.op_left_p_num.GetComponent<number_loader>().set_number((int)op_p_cards[1].get_data().LScale, 3);
                    gameField.op_right_p_num.GetComponent<number_loader>().set_number((int)op_p_cards[0].get_data().LScale, 3);
                    gameField.opPHole = true;
                    op_p_cards[0].cookie_cared = true;
                    op_p_cards[0].UA_give_position(new Vector3(14.65f * real, 5, 8f));
                    op_p_cards[0].UA_give_rotation(new Vector3(-90, 45, 0));
                    op_p_cards[0].UA_give_condition(gameCardCondition.floating_clickable);
                    op_p_cards[0].UA_flush_all_gived_witn_lock(rush);
                    op_p_cards[1].cookie_cared = true;
                    op_p_cards[1].UA_give_position(new Vector3(-15.2f * real, 5, 8f));
                    op_p_cards[1].UA_give_rotation(new Vector3(-90, -45, 0));
                    op_p_cards[1].UA_give_condition(gameCardCondition.floating_clickable);
                    op_p_cards[1].UA_flush_all_gived_witn_lock(rush);
                    op_p_cards[0].p_line_on();
                    op_p_cards[1].p_line_on();

                }
                else
                {
                    gameField.op_left_p_num.GetComponent<number_loader>().set_number(-1, 3);
                    gameField.op_right_p_num.GetComponent<number_loader>().set_number(-1, 3);
                    gameField.opPHole = false;
                }
            }
           
        }
        else
        {
            //p effect pain

            List<gameCard> my_p_cards = new List<gameCard>();

            List<gameCard> op_p_cards = new List<gameCard>();

            for (int i = 0; i < cards.Count; i++) if (cards[i].gameObject.activeInHierarchy)
                    if (cards[i].cookie_cared == false)
                    {
                        if ((cards[i].p.location & (UInt32)CardLocation.SpellZone) > 0)
                        {
                            if (cards[i].p.sequence == 6 || cards[i].p.sequence == 7)
                            {
                                if (cards[i].p.controller == 0)
                                {
                                    my_p_cards.Add(cards[i]);
                                }
                                else
                                {
                                    op_p_cards.Add(cards[i]);
                                }
                            }
                        }
                    }

            gameField.mePHole = false;
            gameField.opPHole = false;

            if (my_p_cards.Count == 2)
            {
                gameField.me_left_p_num.GetComponent<number_loader>().set_number((int)my_p_cards[0].get_data().LScale, 3);
                gameField.me_right_p_num.GetComponent<number_loader>().set_number((int)my_p_cards[1].get_data().LScale, 0);
            }
            else
            {
                gameField.me_left_p_num.GetComponent<number_loader>().set_number(-1, 3);
                gameField.me_right_p_num.GetComponent<number_loader>().set_number(-1, 3);
            }
            if (op_p_cards.Count == 2)
            {
                gameField.op_left_p_num.GetComponent<number_loader>().set_number((int)op_p_cards[1].get_data().LScale, 0);
                gameField.op_right_p_num.GetComponent<number_loader>().set_number((int)op_p_cards[0].get_data().LScale, 3);
            }
            else
            {
                gameField.op_left_p_num.GetComponent<number_loader>().set_number(-1, 3);
                gameField.op_right_p_num.GetComponent<number_loader>().set_number(-1, 3);
            }

        }
        
        for (int i = 0; i < cards.Count; i++) if (cards[i].gameObject.activeInHierarchy)
                if (cards[i].cookie_cared == false)
                {
                    if ((cards[i].p.location & (UInt32)CardLocation.Overlay) > 0)
                    {
                        if ((cards[i].p.location & (UInt32)CardLocation.Extra) > 0)
                        {
                            cards[i].cookie_cared = true;
                            cards[i].UA_give_condition(get_point_worldcondition(cards[i].p));
                            Vector3 temp = get_point_worldposition(cards[i].p_beforeOverLayed);
                            temp.y = 0;
                            temp.y -= 2.1f + (cards[i].p.position) * 0.05f;
                            cards[i].UA_give_position(temp);
                            cards[i].UA_give_rotation(get_world_rotation(cards[i]));
                            cards[i].UA_flush_all_gived_witn_lock(rush);
                        }
                    }
                }

        for (int i = 0; i < cards.Count; i++) if (cards[i].gameObject.activeInHierarchy)
                if (cards[i].cookie_cared == false)
                {
                    cards[i].UA_give_condition(get_point_worldcondition(cards[i].p));
                    cards[i].UA_give_position(get_point_worldposition(cards[i].p, cards[i]));
                    cards[i].UA_give_rotation(get_world_rotation(cards[i]));
                    cards[i].UA_flush_all_gived_witn_lock(rush);
                }

        
        if (Program.I().setting.setting.Vfield.value)
        {
            int code = 0;

            for (int i = 0; i < cards.Count; i++) if (cards[i].gameObject.activeInHierarchy)
                {
                    if (((cards[i].p.location & (UInt32)CardLocation.SpellZone) > 0) && cards[i].p.sequence == 5)
                    {
                        if (cards[i].p.controller == 0)
                        {
                            if ((cards[i].p.position & (Int32)CardPosition.FaceUp) > 0)
                            {
                                code = cards[i].get_data().Id;
                            }
                        }
                    }
                }

            gameField.set(0, code);

            code = 0;

            for (int i = 0; i < cards.Count; i++) if (cards[i].gameObject.activeInHierarchy)
                {
                    if (((cards[i].p.location & (UInt32)CardLocation.SpellZone) > 0) && cards[i].p.sequence == 5)
                    {
                        if (cards[i].p.controller == 1)
                        {
                            if ((cards[i].p.position & (Int32)CardPosition.FaceUp) > 0)
                            {
                                code = cards[i].get_data().Id;
                            }
                        }
                    }
                }

            gameField.set(1, code);
        }
        else
        {
            gameField.set(0, 0);
            gameField.set(1, 0);
        }


        //camera
        float nearest_z = 0;
        for (int i = 0; i < cards.Count; i++) if (cards[i].gameObject.activeInHierarchy)
            {
                if (nearest_z > cards[i].UA_get_accurate_position().z)
                {
                    nearest_z = cards[i].UA_get_accurate_position().z;
                }
            }
        camera_max = -3.5f - 15f * Program.fieldSize;
        camera_min = nearest_z-0.5f;
        if (camera_min > camera_max)
        {
            camera_min = camera_max;
        }

        if (InAI==false)    
        {
            if (condition != Condition.duel)
            {
                toNearest();
            }
        }

        if (someCardIsShowed)
        {
            if (gameInfo.queryHashedButton("hide_all_card") == false)
            {
                gameInfo.addHashedButton("hide_all_card", 0, superButtonType.see, InterString.Get("确认完毕@ui"));
            }
        }
        else
        {
            gameInfo.removeHashedButton("hide_all_card");
        }

        // 「卡组记牌」按钮：紧跟「确认完毕」之后同步一次。
        // 右侧按钮栏是按 HashedButtons 列表顺序自上而下排的（gameInfo.cs:142 的 `-145 - j*50`），
        // 所以在这里 add 出来的那一颗正好落在「确认完毕」下面、「转换视角」上面。
        // `someCardIsShowed` 刚在上面算完，这里读才是本拍的值。
        syncDeckMemoButton();

        if (InAI == false && condition != Condition.duel)
        {
            if (gameInfo.queryHashedButton("swap") == false)
            {
                gameInfo.addHashedButton("swap", 0, superButtonType.change, InterString.Get("转换视角@ui"));
            }
        }
        else
        {
            gameInfo.removeHashedButton("swap");
        }


        animation_count(gameField.LOCATION_DECK_0, CardLocation.Deck, 0);
        animation_count(gameField.LOCATION_EXTRA_0, CardLocation.Extra, 0);
        animation_count(gameField.LOCATION_GRAVE_0, CardLocation.Grave, 0);
        animation_count(gameField.LOCATION_REMOVED_0, CardLocation.Removed, 0);
        animation_count(gameField.LOCATION_DECK_1, CardLocation.Deck, 1);
        animation_count(gameField.LOCATION_EXTRA_1, CardLocation.Extra, 1);
        animation_count(gameField.LOCATION_GRAVE_1, CardLocation.Grave, 1);
        animation_count(gameField.LOCATION_REMOVED_1, CardLocation.Removed, 1);
        gameField.realize();
        Program.notGo(gameInfo.realize);
        Program.go(50,gameInfo.realize);
        Program.notGo(Program.I().book.realize);
        Program.go(50, Program.I().book.realize);
        Program.I().cardDescription.realizeMonitor();
    }

    private void animation_thunder(GameObject leftGameObject, GameObject rightGameObject)
    {
        thunder_locator thunder = null;
        for (int p = 0; p < gameField.thunders.Count; p++)
        {
            if (gameField.thunders[p].leftobj == leftGameObject)
            {
                if (gameField.thunders[p].rightobj == rightGameObject)
                {
                    thunder = gameField.thunders[p];
                }
            }
        }

        if (thunder == null)
        {
            thunder = create_s(Program.I().mod_ocgcore_decoration_thunder).GetComponent<thunder_locator>();
            thunder.set_objects(leftGameObject, rightGameObject);
            gameField.thunders.Add(thunder);
        }
        thunder.needDestroy = false;
    }

    enum cardRuleComdition
    {
        meUpAtk,
        meUpDef,
        meDownAtk,
        meDownDef,
        opUpAtk,
        opUpDef,
        opDownAtk,
        opDownDef,
    }

    Vector3 get_world_rotation(gameCard card)
    {
        cardRuleComdition r = cardRuleComdition.meUpAtk;
        if ((card.p.location & (UInt32)CardLocation.Deck) > 0)
        {
            if (card.get_data().Id > 0)
            {
                r = cardRuleComdition.meUpAtk;
            }
            else
            {
                r = cardRuleComdition.meDownAtk;
            }
        }
        if ((card.p.location & (UInt32)CardLocation.Grave) > 0)
        {
            r = cardRuleComdition.meUpAtk;
        }
        if ((card.p.location & (UInt32)CardLocation.Removed) > 0)
        {
            if ((card.p.position & (UInt32)CardPosition.FaceUp) > 0)
            {
                r = cardRuleComdition.meUpAtk;
            }
            else
            {
                r = cardRuleComdition.meDownAtk;
            }
        }
        if ((card.p.location & (UInt32)CardLocation.Extra) > 0)
        {
            if ((card.p.position & (UInt32)CardPosition.FaceUp) > 0)
            {
                r = cardRuleComdition.meUpAtk;
            }
            else
            {
                r = cardRuleComdition.meDownAtk;
            }
        }
        if ((card.p.location & (UInt32)CardLocation.MonsterZone) > 0)
        {
            if ((card.p.position & (UInt32)CardPosition.FaceDownDefence) > 0)
            {
                r = cardRuleComdition.meDownDef;
            }
            if ((card.p.position & (UInt32)CardPosition.FaceUpDefence) > 0)
            {
                r = cardRuleComdition.meUpDef;
            }
            if ((card.p.position & (UInt32)CardPosition.FaceDownAttack) > 0)
            {
                r = cardRuleComdition.meDownAtk;
            }
            if ((card.p.position & (UInt32)CardPosition.FaceUpAttack) > 0)
            {
                r = cardRuleComdition.meUpAtk;
            }
        }
        if ((card.p.location & (UInt32)CardLocation.SpellZone) > 0)
        {
            if ((card.p.position & (UInt32)CardPosition.FaceUp) > 0)
            {
                r = cardRuleComdition.meUpAtk;
            }
            else
            {
                r = cardRuleComdition.meDownAtk;
            }
        }
        if ((card.p.location & (UInt32)CardLocation.Overlay) > 0)
        {
            r = cardRuleComdition.meUpAtk;
        }
        if (card.p.controller == 1)
        {
            switch (r)  
            {
                case cardRuleComdition.meUpAtk:
                    r = cardRuleComdition.opUpAtk;
                    break;
                case cardRuleComdition.meUpDef:
                    r = cardRuleComdition.opUpDef;
                    break;
                case cardRuleComdition.meDownAtk:
                    r = cardRuleComdition.opDownAtk;
                    break;
                case cardRuleComdition.meDownDef:
                    r = cardRuleComdition.opDownDef;
                    break;
                default:
                    break;
            }
        }
        switch (r)  
        {
            case cardRuleComdition.meUpAtk:
                return new Vector3(0, 0, 0);
            case cardRuleComdition.meUpDef:
                return new Vector3(0, -90, 0);
            case cardRuleComdition.meDownAtk:
                return new Vector3(0, 0, 180);
            case cardRuleComdition.meDownDef:
                return new Vector3(0, -90, 180);


            case cardRuleComdition.opUpAtk:
                return new Vector3(0, 180, 0);
            case cardRuleComdition.opUpDef:
                return new Vector3(0, 90, 0);
            case cardRuleComdition.opDownAtk:
                return new Vector3(0, 180, 180);
            case cardRuleComdition.opDownDef:
                return new Vector3(0, 90, 180);

            default:
                return Vector3.zero;
        }
    }

    //private Vector3 get_real_rotation(int i)
    //{
    //    Vector3 r = get_point_worldrotation(cards[i].p);
    //    if ((cards[i].p.location & (UInt32)CardLocation.Deck) > 0)
    //    {
    //        if (cards[i].get_data().Id > 0)
    //        {
    //            r = new Vector3(90, 0, 0);
    //        }
    //        else
    //        {
    //            r = new Vector3(-90, 0, 0);
    //        }
    //    }
    //    if ((cards[i].p.location & (UInt32)CardLocation.MonsterZone) > 0)
    //    {
    //        if ((cards[i].p.position & (UInt32)CardPosition.FaceDown_DEFENSE) > 0)
    //        {
    //            r = new Vector3(-90, 0, 90);
    //        }
    //        if ((cards[i].p.position & (UInt32)CardPosition.FaceUp_DEFENSE) > 0)
    //        {
    //            r = new Vector3(90, 0, 90);
    //        }
    //        if ((cards[i].p.position & (UInt32)CardPosition.FaceDownAttack) > 0)
    //        {
    //            r = new Vector3(-90, 0, 0);
    //        }
    //        if ((cards[i].p.position & (UInt32)CardPosition.FaceUpAttack) > 0)
    //        {
    //            r = new Vector3(90, 0, 0);
    //        }
    //    }
    //    if ((cards[i].p.location & (UInt32)CardLocation.SpellZone) > 0)
    //    {
    //        if ((cards[i].p.position & (UInt32)CardPosition.FaceDown_DEFENSE) > 0)
    //        {
    //            r = new Vector3(-90, 0, 90);
    //        }
    //        if ((cards[i].p.position & (UInt32)CardPosition.FaceUp_DEFENSE) > 0)
    //        {
    //            r = new Vector3(90, 0, 90);
    //        }
    //        if ((cards[i].p.position & (UInt32)CardPosition.FaceDownAttack) > 0)
    //        {
    //            r = new Vector3(-90, 0, 0);
    //        }
    //        if ((cards[i].p.position & (UInt32)CardPosition.FaceUpAttack) > 0)
    //        {
    //            r = new Vector3(90, 0, 0);
    //        }
    //    }
    //    if ((cards[i].p.location & (UInt32)CardLocation.Grave) > 0)
    //    {
    //        r = new Vector3(90, 0, 0);
    //    }
    //    if ((cards[i].p.location & (UInt32)CardLocation.Removed) > 0)
    //    {
    //        if ((cards[i].p.position & (UInt32)CardPosition.FaceUp) > 0)
    //        {
    //            r = new Vector3(90, 0, 0);
    //        }
    //        else
    //        {
    //            r = new Vector3(-90, 0, 0);
    //        }
    //    }
    //    if ((cards[i].p.location & (UInt32)CardLocation.Extra) > 0)
    //    {
    //        if ((cards[i].p.position & (UInt32)CardPosition.FaceUp) > 0)
    //        {
    //            r = new Vector3(90, 0, 0);
    //        }
    //        else
    //        {
    //            r = new Vector3(-90, 0, 0);
    //        }
    //    }
    //    if ((cards[i].p.location & (UInt32)CardLocation.Overlay) > 0)
    //    {
    //        r = new Vector3(90, 0, 0);
    //    }
    //    if (cards[i].p.controller == 1)
    //    {
    //        r.z += 179f;
    //    }

    //    return r;
    //}

    private void animation_count(TMPro.TextMeshPro textmesh, CardLocation location, int player)
    {
        int count = 0;
        int countU = 0; 
        for (int i = 0; i < cards.Count; i++) if (cards[i].gameObject.activeInHierarchy)
            {
                if (cards[i].p.controller == player)
                {
                    if ((cards[i].p.location & (UInt32)location) > 0)
                    {
                        count++;
                        if ((cards[i].p.position & (UInt32)CardPosition.FaceUp) > 0)
                        {
                            countU++;
                        }
                    }
                }
            }
        if (count < 2)
        {
            textmesh.text = "";
        }
        else
        {
            if (location== CardLocation.Extra)    
            {
                textmesh.text = count.ToString()+"("+ countU .ToString()+ ")";
            }
            else
            {
                textmesh.text = count.ToString();
            }
        }
    }

    float camera_max = -17.5f;

    float camera_min = -17.5f;

    public void toNearest(bool fix=false)
    {
        if (fix)
        {
            if (Program.cameraPosition.z < camera_min)
            {
                Program.cameraPosition.z = camera_min;
                Program.cameraPosition.x = 0;
                Program.cameraPosition.y = 23;
            }
        }
        else
        {
            Program.cameraPosition.z = camera_min;
            Program.cameraPosition.x = 0;
            Program.cameraPosition.y = 23;
        }
        Program.cameraRotation = new Vector3(60, 0, 0);
    }

    public gameCard GCS_cardCreate(GPS p)
    {
        gameCard c = null;
        for (int i = 0; i < cards.Count; i++)
        {
            if (cards[i].md5 == md5Maker)
            {
                c = cards[i];
                c.p = p;
            }
        }
        if (c == null)
        {
            c = new gameCard();
            c.md5 = md5Maker;
            c.p = p;
            cards.Add(c);
        }
        c.show();
        c.p = p;
        c.controllerBased = p.controller;
        md5Maker++;
        return c;
    }

    public gameCard GCS_cardGet(GPS p, bool create)
    {
        gameCard c = null;
        if ((p.location & (UInt32)CardLocation.Overlay) > 0)
        {
            for (int i = 0; i < cards.Count; i++)
            {
                if (cards[i].p.location == p.location)
                {
                    if (cards[i].p.controller == p.controller)
                    {
                        if (cards[i].p.sequence == p.sequence)
                        {
                            if (cards[i].p.position == p.position)
                            {
                                if (cards[i].gameObject.activeInHierarchy)
                                {
                                    c = cards[i];
                                    break;
                                }
                            }
                        }
                    }
                }
            }
        }
        else
        {
            for (int i = 0; i < cards.Count; i++)
            {
                if (cards[i].p.location == p.location)
                {
                    if (cards[i].p.controller == p.controller)
                    {
                        if (cards[i].p.sequence == p.sequence)
                        {
                            if (cards[i].gameObject.activeInHierarchy)
                            {
                                c = cards[i];
                                break;
                            }
                        }
                    }
                }
            }
        }
        if (p.location == 0)
        {
            c = null;
        }
        if (create == true)
        {
            if (c == null)
            {
                c = GCS_cardCreate(p);
            }
        }
        return c;
    }

    public List<gameCard> GCS_cardGetOverlayElements(gameCard c)
    {
        List<gameCard> cas = new List<gameCard>();
        if (c != null)
        {
            if ((c.p.location & (UInt32)CardLocation.Overlay) == 0)
            {
                for (int i = 0; i < cards.Count; i++) if (cards[i].gameObject.activeInHierarchy)
                    {
                        if ((cards[i].p.location & (UInt32)CardLocation.Overlay) > 0)
                            if (cards[i].p.controller == c.p.controller)
                                if ((cards[i].p.location | (UInt32)CardLocation.Overlay) == (c.p.location | (UInt32)CardLocation.Overlay))
                                    if (cards[i].p.sequence == c.p.sequence)
                                        cas.Add(cards[i]);
                    }
            }
        }
        return cas;
    }

    List<int> keys = new List<int>();

    public gameCard GCS_cardMove(GPS p1, GPS p2, bool print = true, bool swap = false)
    {

        //from card
        gameCard card_from = GCS_cardGet(p1, true);

        try
        {
            if (reportShowAll)
            {
                if (print)
                {
                    if (swap)
                    {
                        //printDuelLog(UIHelper.getGPSstringLocation(p1) + InterString.Get("交换") + UIHelper.getGPSstringLocation(p2) + UIHelper.getGPSstringName(card_from));
                    }
                    else
                    {
                        //printDuelLog(UIHelper.getGPSstringLocation(p1) + InterString.Get("移到") + UIHelper.getGPSstringLocation(p2) + UIHelper.getGPSstringName(card_from));
                    }
                }
            }
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.Log(e);
        }


        //to card
        gameCard card_to = GCS_cardGet(p2, false);

        card_from.isShowed = false;
        card_from.ChainUNlock();

        if (swap == false)
        {
            if ((p1.location != p2.location) || ((p2.position & (int)CardPosition.FaceDown) > 0))
            {
                card_from.target.Clear();
                for (int i = 0; i < cards.Count; i++) if (cards[i].gameObject.activeInHierarchy)
                    {
                        cards[i].removeTarget(card_from);
                    }
                card_from.disabled = false;
                card_from.refreshData();
            }
        }

        if ((p2.location & (UInt32)CardLocation.Overlay) > 0)
        {
            card_from.p_beforeOverLayed = p1;
        }


        List<gameCard> overlayed_cards_of_cardFrom = GCS_cardGetOverlayElements(card_from);
        List<gameCard> overlayed_cards_of_cardTo = GCS_cardGetOverlayElements(card_to);

        //begin analyse
        if (swap)
        {
            if (card_from != null)
                card_from.p = p2;
            if (card_to != null)
                card_to.p = p1;
        }
        else
        {
            if (card_to == null)
            {
                if (card_from != null)
                    card_from.p = p2;
            }
            else
            {
                if (card_from == card_to)
                {
                    if (card_from != null)
                        card_from.p = p2;
                }
                else
                {
                    if ((card_to.p.location & (UInt32)CardLocation.Overlay) == 0)
                    {
                        if (((card_to.p.location & (UInt32)CardLocation.MonsterZone) > 0) || ((card_to.p.location & (UInt32)CardLocation.SpellZone) > 0))
                        {
                            if (card_from != null)
                                card_from.p = p2;
                            if (card_to != null)
                                card_to.p = p1;
                        }
                        else
                        {
                            if (card_from != null)
                            {
                                GCS_cardRelocate(card_from,p2);
                            }
                        }

                    }
                    else
                    {
                        if (card_from != null)
                        {
                            card_from.p = p2;
                            card_from.p.position += 500;
                        }
                    }
                }
            }
        }

        //overlay 
        if (card_from != null)
        {
            for (int i = 0; i < overlayed_cards_of_cardFrom.Count; i++)
            {
                overlayed_cards_of_cardFrom[i].p.controller = card_from.p.controller;
                overlayed_cards_of_cardFrom[i].p.location = card_from.p.location | (UInt32)CardLocation.Overlay;
                overlayed_cards_of_cardFrom[i].p.sequence = card_from.p.sequence;
                overlayed_cards_of_cardFrom[i].p.position += 1000;
            }
        }

        if (card_to != null)
        {
            for (int i = 0; i < overlayed_cards_of_cardTo.Count; i++)
            {
                overlayed_cards_of_cardTo[i].p.controller = card_to.p.controller;
                overlayed_cards_of_cardTo[i].p.location = card_to.p.location | (UInt32)CardLocation.Overlay;
                overlayed_cards_of_cardTo[i].p.sequence = card_to.p.sequence;
                overlayed_cards_of_cardTo[i].p.position += 1000;
            }
        }

        arrangeCards();
        return card_from;
    }

    void GCS_cardRelocate(gameCard card_from, GPS p2)
    {
        List<gameCard> cardsInLocation = MHS_getBundle((int)p2.controller, (int)p2.location);
        cardsInLocation.Remove(card_from);
        cardsInLocation.Sort((left, right) =>
        {
            int a = 0;
            if (left.p.sequence > right.p.sequence)
            {
                a = 1;
            }
            else if (left.p.sequence < right.p.sequence)
            {
                a = -1;
            }
            return a;
        });
        if ((int)p2.sequence < 0)
        {
            cardsInLocation.Insert(0, card_from);
        }
        else if ((int)p2.sequence > cardsInLocation.Count)
        {
            cardsInLocation.Insert(cardsInLocation.Count, card_from);
        }
        else
        {
            cardsInLocation.Insert((int)p2.sequence, card_from);
        }
        for (int i = 0; i < cardsInLocation.Count; i++) 
        {
            cardsInLocation[i].p.sequence = (uint)i;
        }
        card_from.p = p2;
    }

    private void arrangeCards()
    {
        //sort 
        cards.Sort((left, right) =>
        {
            int a = 1;
            if (left.p.controller > right.p.controller)
            {
                a = 1;
            }
            else if (left.p.controller < right.p.controller)
            {
                a = -1;
            }
            else
            {
                if (left.p.location == (UInt32)CardLocation.Hand && right.p.location != (UInt32)CardLocation.Hand)
                {
                    a = -1;
                }
                else if (left.p.location != (UInt32)CardLocation.Hand && right.p.location == (UInt32)CardLocation.Hand)
                {
                    a = 1;
                }
                else
                {
                    if ((left.p.location | (UInt32)CardLocation.Overlay) > (right.p.location | (UInt32)CardLocation.Overlay))
                    {
                        a = -1;
                    }
                    else if ((left.p.location | (UInt32)CardLocation.Overlay) < (right.p.location | (UInt32)CardLocation.Overlay))
                    {
                        a = 1;
                    }
                    else
                    {
                        if (left.p.sequence > right.p.sequence)
                        {
                            a = 1;
                        }
                        else if (left.p.sequence < right.p.sequence)
                        {
                            a = -1;
                        }
                        else
                        {
                            if ((left.p.location & (UInt32)CardLocation.Overlay) > (right.p.location & (UInt32)CardLocation.Overlay))
                            {
                                a = -1;
                            }
                            else if ((left.p.location & (UInt32)CardLocation.Overlay) < (right.p.location & (UInt32)CardLocation.Overlay))
                            {
                                a = 1;
                            }
                            else
                            {
                                if (left.p.position > right.p.position)
                                {
                                    a = 1;
                                }
                                else if (left.p.position < right.p.position)
                                {
                                    a = -1;
                                }
                            }
                        }
                    }
                }
            }
            return a;
        });

        /////rebuild
        UInt32 preController = 9999;
        UInt32 preLocation = 9999;
        UInt32 preSequence = 9999;

        UInt32 sequenceWriter = 0;
        int positionWriter = 0;

        for (int i = 0; i < cards.Count; i++) if (cards[i].gameObject.activeInHierarchy)
            {
                if (preController != cards[i].p.controller)
                {
                    sequenceWriter = 0;
                }
                if ((preLocation | (UInt32)CardLocation.Overlay) != (cards[i].p.location | (UInt32)CardLocation.Overlay))
                {
                    sequenceWriter = 0;
                }
                if (preSequence != cards[i].p.sequence)
                {
                    positionWriter = 0;
                }

                if ((cards[i].p.location & (UInt32)CardLocation.MonsterZone) == 0)
                {
                    if ((cards[i].p.location & (UInt32)CardLocation.SpellZone) == 0)
                    {
                        cards[i].p.sequence = sequenceWriter;
                    }
                }

                if ((cards[i].p.location & (UInt32)CardLocation.Overlay) > 0)
                {
                    cards[i].p.position = positionWriter;
                    positionWriter++;
                }
                else
                {
                    sequenceWriter++;
                }

                preController = cards[i].p.controller;
                preLocation = cards[i].p.location;
                preSequence = cards[i].p.sequence;
            }
    }

    int cookie_matchKill = 0;

    int md5Maker = 0;

    string ES_turnString = "";

    string ES_phaseString = "";

    string ES_selectUnselectHint = "";

    bool ES_selectCardFromFieldFirstFlag = false;

    void toDefaultHint()
    {
        gameField.setHint(ES_turnString + ES_phaseString);
    }

    void toDefaultHintLogical()
    {
        gameField.setHintLogical(ES_turnString + ES_phaseString);
    }

    void returnFromDeckEdit()
    {
        TcpHelper.CtosMessage_UpdateDeck(((DeckManager)Program.I().deckManager).getRealDeck());
        returnServant = Program.I().selectServer;
    }

    public GameField gameField;

    enum duelResult
    {
        disLink,win,lose,draw
    }

    duelResult result = duelResult.disLink;

    public override void show()
    {
        if (isShowed == true)
        {
            Menu.deleteShell();
        }
        base.show();
        Program.I().light.transform.eulerAngles = new Vector3(50, -50, 0);
        Program.cameraPosition = new Vector3(0, 23, -18.5f - 3.2f * (Program.fieldSize - 1f) / 0.21f);
        Program.camera_game_main.transform.position = Program.cameraPosition*1.5f;
        Program.cameraRotation = new Vector3(60, 0, 0);
        Program.camera_game_main.transform.eulerAngles = Program.cameraRotation;
        Program.reMoveCam(getScreenCenter());
        gameField = new GameField();    
        if (paused)
        {
            try
            {
                EventDelegate.Execute(UIHelper.getByName<UIButton>(toolBar, "go_").onClick);
            }
            catch (Exception e) 
            {
                paused = false;
            }
        }
        deckReserved = false;
        cantCheckGrave = false;
        surrended = false;
        Program.I().room.duelEnded = false;
        gameInfo.swaped = false;
        keys.Clear();
        currentMessageIndex = -1;
        result = duelResult.disLink;
        theWorldIndex = 0;
        gameInfo.setTimeStill(0);
        sideReference.Clear();
        confirmedCards.Clear();

    }

    public override void hide()
    {
        Program.I().cardDescription.shiftCardShower(true);
        InAI = false;
        MessageBeginTime = 0;
        currentMessage = GameMessage.Waiting;
        Packages_ALL.Clear();
        Packages.Clear();
        cardsForConfirm.Clear();
        logicalClearChain();
        deckReserved = false;
        cantCheckGrave = false;
        if (isShowed)
        {
            clearResponse();
            Program.I().book.clear();
            Program.I().book.hide();
        }
        for (int i = 0; i < cards.Count; i++)
        {
            cards[i].hide();
        }
        paused = false;
        condition = Condition.N;
        base.hide();
    }

    /// <summary>
    /// 「盖放怪兽前询问」开关：设置窗口那一列（x=-258）的追加行 askMset_，默认**开**。
    /// 开了之后点「前场放置」不会立刻把应答发给 ocgcore，先弹一句「是否确定盖放[卡名]？」。
    ///
    /// 与 DeckManager.TestHideAnim 同款：每次实时读 Config，设置窗口一改立刻生效 ——
    /// 不做缓存就没有失效问题（读一次是一次线性查找，盖放这种低频操作完全无所谓）。
    ///
    /// **键按当前模式取**（用户 2026-09-19 口径：RD 的这两个开关与 OCG 独立）：
    /// OCG 走历史键 `askMset_`，RD 走 `askMset_rd` —— 见 <see cref="GameModeManager.KeyAskMset"/>。
    /// </summary>
    static bool askMSetBeforeSet
    {
        get { return UIHelper.fromStringToBool(Config.Get(GameModeManager.KeyAskMset, "1")); }
    }

    /// <summary>
    /// 「召唤前询问」开关：设置窗口那一列（x=-258）的追加行 askSummon_，默认**开**。
    /// 开了之后点「通常召唤」不会立刻把应答发给 ocgcore，先弹一句「是否确定召唤[卡名]？」。
    ///
    /// 只拦**通常召唤**（superButtonType.summon / response 低位 0）—— 就是「会占掉本回合召唤权」
    /// 的那一下，与「盖放前询问」属于同一类误点代价高的动作。**特殊召唤**（spsummon，低位 1）
    /// 刻意不拦：它在效果流程里出现得更频繁，而且不占召唤权（这是跟用户确认过的口径）。
    /// 与 askMSetBeforeSet 同款：每次实时读 Config，设置窗口一改立刻生效；键按当前模式取。
    ///
    /// ⚠ **待实测**：RD 没有召唤次数上限，rd 服上「通常召唤」是不是仍是
    /// `superButtonType.summon` + response 低位 0 的那颗按钮，还没验过
    /// （见 _plan_rdmode.md「待实测口径」）。若不是，拦截条件要在 RD 下另立口径 ——
    /// 别把 OCG 的位判定当成跨模式真理。
    /// </summary>
    static bool askSummonBeforeSummon
    {
        get { return UIHelper.fromStringToBool(Config.Get(GameModeManager.KeyAskSummon, "1")); }
    }

    /// <summary>
    /// 「动手前先问一句」的公共实现（盖放 / 通常召唤共用一套，别再抄第二份）。
    ///
    /// 点「是」才把应答**原样**递出去（走 ES_RMS 的 "return" 分支，顺带保住 sendReturn 里的
    /// DuelUndo / DuelTimeline 记账 —— ⛔ 绝不能绕过 sendReturn 自己 writer.Write）；
    /// 点「否」一个字节都不发、停在 idle command 让玩家重来。core 侧完全无感：字节只晚了几拍。
    /// </summary>
    void askBeforeCommit(gameButton btn, string askFormat, string tag)
    {
        if (btn.cookieCard != null)
        {
            lastExcitedController = (int)btn.cookieCard.p.controller;
            lastExcitedLocation = (int)btn.cookieCard.p.location;
        }
        // ⛔ 用 btn.cookieCard.get_data().Name 取卡名，不要走 CardsManager.Get(code)：
        //    后者带「id−0..9」位宽回退，卡库外的码会返回不相干卡名。
        //    手上的牌在 SelectIdleCmd 解析时已经 set_code() 灌过真名，缺失时是「未知卡片」，不会 NRE。
        string cardName = btn.cookieCard != null ? btn.cookieCard.get_data().Name : "";
        string ask = InterString.Get(askFormat, cardName);
        // 应答值跟着对话框走（koishi 把盖放参数存在成员变量里、事后忘了清零，点「否」会留残值；
        // 我们不复刻这个隐患）。hash "return"（见 ES_RMS）会把 value 解析成 int 后 sendReturn；
        // 它对 "hide" 有专门的跳过分支，正好当「否」。
        RMSshow_yesOrNo("return",
            ask,
            new messageSystemValue { value = btn.response.ToString(), hint = "yes" },
            new messageSystemValue { value = "hide", hint = "no" });
        if (QuickTestTrace.Enabled)
        {
            // 探针与真判据同源：报的就是刚弹出去的那句话、那个应答值，
            // 以及对话框自己的是/否按钮坐标（脚本照它点，别猜坐标）。
            // 坐标由 TraceMSButtons 自己按「变了才写」连采约 1.5 秒 —— 框是 iTween 弹出来的，
            // 只采一两拍会拿到中途位置，脚本就会点空（真踩过，整段判据假红）。
            QuickTestTrace.Log(tag, "ask resp=" + btn.response
                + " name=" + cardName + " hint=" + ask);
            TraceMSButtons(tag);
        }
    }

    public void ES_gameButtonClicked(gameButton btn)
    {
        if (btn.cookieString == "see_overlay")
        {
            if (btn.cookieCard != null)
            {
                btn.cookieCard.ES_exit_excited(true);
                List<gameCard> cas = GCS_cardGetOverlayElements(btn.cookieCard);
                for (int i = 0; i < cas.Count; i++)
                {
                    cas[i].isShowed = !cas[i].isShowed;
                    cas[i].flash_line_off();
                    //if (cas[i].isShowed)
                    //{
                    //    cas[i].set_text(GameStringHelper.diefang);
                    //}
                    //else
                    //{
                    //    cas[i].set_text("");
                    //}
                }
                realize();
                toNearest();
            }
            return;
        }
        switch (currentMessage)
        {
            case GameMessage.SelectBattleCmd:
            case GameMessage.SelectIdleCmd:
                if (btn.hint == InterString.Get("发动效果@ui"))
                {
                    if (btn.cookieCard.effects.Count > 0)
                    {
                        if (btn.cookieCard.effects.Count == 1)
                        {
                            BinaryMaster binaryMaster = new BinaryMaster();
                            binaryMaster.writer.Write(btn.cookieCard.effects[0].ptr);
                            sendReturn(binaryMaster.get());
                        }
                        else
                        {
                            List<messageSystemValue> values = new List<messageSystemValue>();
                            for (int i = 0; i < btn.cookieCard.effects.Count; i++)
                            {
                                values.Add(new messageSystemValue { hint = btn.cookieCard.effects[i].desc, value = btn.cookieCard.effects[i].ptr.ToString() });
                            }
                            values.Add(new messageSystemValue { hint = InterString.Get("取消"), value = "hide" });
                            RMSshow_singleChoice("return", values);
                        }
                    }
                    return;
                }
                // 「盖放怪兽前询问」：与 KoishiPro 的 ask_mset 同口径 —— 点「前场放置」后
                // **先不发应答**，只弹一句确认；点「是」才把应答原样递出去，点「否」一个
                // 字节都不发、停在 idle command 让玩家重来。core 侧完全无感：字节只晚了几拍。
                // 具体弹框 / 探针在 askBeforeCommit()，与「召唤前询问」共用同一套。
                //
                // ⛔ 判据用 type + response 低 16 位，别拿文案比字符串：前场放置（MSET，
                //    response=(index<<16)+3）与后场放置（SSET，+4）同为 superButtonType.set，
                //    只有低位能区分；而两个条件叠起来才唯一 —— 哈希按钮「结束回合」也用
                //    response=3（见上游 addHashedButton("" ,3, ep)），只是不走这个分支。
                if (btn.type == superButtonType.set && (btn.response & 0xFFFF) == 3 && askMSetBeforeSet)
                {
                    askBeforeCommit(btn, "是否确定盖放[?]？", "mset");
                    return;
                }
                // 「召唤前询问」：与盖放同一套原理，只拦**通常召唤**（低位 0）。
                // 特殊召唤是低位 1，刻意不拦（效果流程里更频繁、且不占召唤权）。
                if (btn.type == superButtonType.summon && (btn.response & 0xFFFF) == 0 && askSummonBeforeSummon)
                {
                    askBeforeCommit(btn, "是否确定召唤[?]？", "summon");
                    return;
                }
                lastExcitedController = (int)btn.cookieCard.p.controller;
                lastExcitedLocation = (int)btn.cookieCard.p.location;
                BinaryMaster p = new BinaryMaster();
                p.writer.Write((int)btn.response);
                sendReturn(p.get());
                break;
            case GameMessage.SelectEffectYn:
                break;
            case GameMessage.SelectYesNo:
                break;
            case GameMessage.SelectOption:
                break;
            case GameMessage.SelectCard:
                break;
            case GameMessage.SelectUnselect:
                break;
            case GameMessage.SelectChain:
                break;
            case GameMessage.SelectPlace:
                break;
            case GameMessage.SelectPosition:
                break;
            case GameMessage.SelectTribute:
                break;
            case GameMessage.SortChain:
                break;
            case GameMessage.SelectCounter:
                break;
            case GameMessage.SelectSum:
                break;
            case GameMessage.SelectDisfield:
                break;
            case GameMessage.AnnounceRace:
                break;
            case GameMessage.AnnounceAttrib:
                break;
            case GameMessage.AnnounceCard:
                break;
            case GameMessage.AnnounceNumber:
                break;
        }
    }

    public void ES_gameUIbuttonClicked(gameUIbutton btn)
    {
        if (btn.hashString == "clearCounter")
        {
            for (int i = 0; i < allCardsInSelectMessage.Count; i++)
            {
                allCardsInSelectMessage[i].counterSELcount = 0;
                allCardsInSelectMessage[i].show_number(allCardsInSelectMessage[i].counterSELcount);
            }
            return;
        }
        if (btn.hashString == "sendSelected")
        {
            sendSelectedCards();
            return;
        }
        if (btn.hashString == "hide_all_card")
        {
            if (flagForTimeConfirm)
            {
                flagForTimeConfirm = false;
                MessageBeginTime = Program.TimePassed();
            }
            clearAllShowed();
            return;
        }
        if (btn.hashString == "deck_memo")
        {
            // ⛔ 绝不能在这里直接 toggleDeckMemo()！
            // 那个函数会在**按钮自己的点击派发还没退栈时**摘掉/新挂 NGUI 按钮
            // （`removeHashedButton` + `addHashedButton` 会销毁并新建 GameObject、
            //  在同一个 BoxCollider 上 AddComponent<UIEventTrigger>/<MonoListener>）。
            // 实测（2026-09-17，qt_1648.log）会诱发 UICamera 对本次点击的无限重入：
            // `UICamera.Update → ProcessRelease → Notify("OnClick") → listenerForClicked
            //  → ES_gameUIbuttonClicked → toggleDeckMemo → realize → syncDeckMemoButton`
            // 这条 16 帧的栈被反复执行约 550 次/秒，主线程从此回不到帧循环（[hb]/[sd]/[pos]
            // 全部停摆），只有 realize() 里的记牌日志还在刷。
            // 所以这里只**置一个待办**，真正的切换放到帧末（`deckMemoToggleTick`）——
            // 与 `clearAllShowedB` / `flagForTimeConfirm` 同一套路。
            deckMemoTogglePending = true;
            return;
        }
        if (btn.hashString == "swap")
        {
            GCS_swapALL();
            return;
        }
        if (btn.hashString == "cancelPlace")
        {
            cancelSelectPlace();
            return;
        }
        switch (currentMessage)
        {
            case GameMessage.SelectBattleCmd:
            case GameMessage.SelectIdleCmd:
                BinaryMaster p = new BinaryMaster();
                p.writer.Write((int)btn.response);
                sendReturn(p.get());
                break;
            case GameMessage.SelectEffectYn:
            case GameMessage.SelectYesNo:
            case GameMessage.SelectCard:
            case GameMessage.SelectUnselect:
            case GameMessage.SelectTribute:
            case GameMessage.SelectChain:
                clearAllShowedB = true;
                BinaryMaster binaryMaster = new BinaryMaster();
                binaryMaster.writer.Write(btn.response);
                sendReturn(binaryMaster.get());
                break;
            case GameMessage.SelectPlace:
                break;
            case GameMessage.SelectPosition:
                break;
            case GameMessage.SortChain:
                break;
            case GameMessage.SelectCounter:
                break;
            case GameMessage.SelectSum:
                break;
            case GameMessage.SelectDisfield:
                break;
            case GameMessage.AnnounceRace:
                break;
            case GameMessage.AnnounceAttrib:
                break;
            case GameMessage.AnnounceCard:
                clearResponse();
                realize();
                toNearest();
                RMSshow_input("AnnounceCard", InterString.Get("请输入关键字。"), "");
                break;
            case GameMessage.AnnounceNumber:
                break;
        }
    }

    private void GCS_swapALL(bool realized=true) 
    {
        isFirst = !isFirst;
        for (int i = 0; i < cards.Count; i++)
        {
            cards[i].p.controller = 1 - cards[i].p.controller;
            cards[i].p_beforeOverLayed.controller = 1 - cards[i].p_beforeOverLayed.controller;
            cards[i].isShowed = false;
            cards[i].controllerBased = 1 - cards[i].controllerBased;
        }
        gameInfo.swaped = !gameInfo.swaped;
        if (realized)
        {
            realize(true);
        }
    }

    private void cancelSelectPlace()
    {
        clearAllSelectPlace();
        BinaryMaster binaryMaster = new BinaryMaster();
        byte[] resp = new byte[3];
        resp[0] = (byte)localPlayer(0);
        resp[1] = 0;
        resp[2] = 0;
        binaryMaster.writer.Write(resp);
        sendReturn(binaryMaster.get());
    }

    private void clearAllShowed()
    {
        // 收摊只擦卡面、**不关记牌**。
        //
        // 擦是必须的：我们在展示中的卡组占位卡上写过真 code，不擦的话这些牌会
        // 「带着真卡面」回到卡组，下次再摊开就是泄底。
        // 但记牌态本身要留着 —— 用户口径（2026-09-17）：开记牌之后，哪怕点「确认完毕」
        // 把卡组收了，记牌也不许自动关，要一直开到玩家自己点「不再记牌」。
        // （之前的写法是 `endDeckMemo()`，把状态一起复位了，属于错误口径。）
        eraseDeckMemoFaces();
        for (int i = 0; i < cards.Count; i++) if (cards[i].gameObject.activeInHierarchy)
            {
                cards[i].isShowed = false;
            }
        realize();
        toNearest();
    }

    public delegate void responseHandler(byte[] buffer);
    public responseHandler handler = null;

    int theWorldIndex = 0;

    public bool inTheWorld() 
    {
        return currentMessageIndex < theWorldIndex;
    }

    /// <summary>
    /// 「这次应答不是玩家点的」标记。
    ///
    /// 撤回只认人工决策点，可客户端在「玩家根本没得选」的时候会替玩家自动应答
    /// （smartSelect 的强制代选 autoSendCards、只有一个选项的 SelectOption），
    /// 它们同样经过 sendReturn。不标记的话，玩家按撤回会退到这种「自己没动过手」的位置，
    /// 而那里又会被同一段代码立刻自动答掉 —— 看上去就是「按了没反应」。
    /// 口径对齐 duel-undo-mod 的 Origin::Manual / Automatic。
    /// </summary>
    bool autoResponding = false;

    /// <summary>自动代答出口：标记 + 转发到 sendReturn（见 <see cref="autoResponding"/>）。</summary>
    void sendReturnAuto(byte[] buffer)
    {
        autoResponding = true;
        try
        {
            sendReturn(buffer);
        }
        finally
        {
            autoResponding = false;
        }
    }

    public void sendReturn(byte[] buffer)
    {
        // 闸门一：正在按录制流本地重建模型 —— 那段重放不是真实对局，一个包都不许发出去。
        if (DuelUndo.rebuilding)
        {
            return;
        }
        // 闸门二：服务器正在重建（本地已回溯、正在追赶）。这期间的界面是「回溯后的旧状态」，
        // 玩家对着它点出来的应答在服务器那边指向的是别的东西 —— 一律丢掉。
        // 撤回器自己代打的那些应答走 SendReplayedReturn，不受这里拦截。
        if (DuelUndo.BlockInput && !DuelUndo.replayingResponse)
        {
            QuickTestTrace.Log("undo", "input dropped: session rebuilding, msg=" + currentMessage);
            return;
        }
        if (paused) 
        {
            EventDelegate.Execute(UIHelper.getByName<UIButton>(toolBar, "go_").onClick);
        }
        // 撤回用的时间线录制：必须赶在 clearResponse() 之前 —— 那之后 currentMessage
        // 就不再是触发这次应答的那条消息了。内部自带「只在人机对局录制」的开关。
        DuelTimeline.NoteResponse((int)currentMessage, buffer, !autoResponding);
        clearResponse();
        if (handler != null)
        {
            handler(buffer);
        }
    }

    /// <summary>撤回器代发录下来的应答：绕过输入闸门，其余与 <see cref="sendReturn"/> 完全一致。</summary>
    public void SendReplayedReturn(byte[] buffer)
    {
        DuelUndo.replayingResponse = true;
        try
        {
            sendReturn(buffer);
        }
        finally
        {
            DuelUndo.replayingResponse = false;
        }
    }

    List<sortResult> ES_sortCurrent = new List<sortResult>();

    public void ES_cardClicked(gameCard card)
    {
        if (card != null)
        {
            lastExcitedController = (int)card.p.controller;
            lastExcitedLocation = (int)card.p.location;
        }
        switch (currentMessage)
        {
            case GameMessage.SelectBattleCmd:
                break;
            case GameMessage.SelectIdleCmd:
                break;
            case GameMessage.SelectEffectYn:
                break;
            case GameMessage.SelectYesNo:
                break;
            case GameMessage.SelectOption:
                break;
            case GameMessage.SortChain:
            case GameMessage.SortCard:
                if (card.forSelect)
                {
                    for (int i = 0; i < cardsInSort.Count; i++)
                    {
                        cardsInSort[i].show_number(0);
                    }
                    List<int> avaliableSortOptions = new List<int>();
                    for (int i = 0; i < card.sortOptions.Count; i++)
                    {
                        avaliableSortOptions.Add(card.sortOptions[i]);
                    }
                    for (int i = 0; i < ES_sortResult.Count; i++)
                    {
                        avaliableSortOptions.Remove(ES_sortResult[i].option);
                    }
                    if (avaliableSortOptions.Count == 0)
                    {
                        List<sortResult> remove = new List<sortResult>();
                        for (int i = 0; i < ES_sortResult.Count; i++)
                        {
                            if (ES_sortResult[i].card == card)
                            {
                                remove.Add(ES_sortResult[i]);
                            }
                        }
                        for (int i = 0; i < remove.Count; i++)
                        {
                            ES_sortResult.Remove(remove[i]);
                        }
                        remove.Clear();
                    }
                    if (avaliableSortOptions.Count == 1)
                    {
                        ES_sortResult.Add(new sortResult
                        {
                            card = card,
                            option = avaliableSortOptions[0]
                        });
                    }
                    if (avaliableSortOptions.Count > 1)
                    {
                        ES_sortCurrent.Clear();
                        for (int i = 0; i < avaliableSortOptions.Count; i++)
                        {
                            ES_sortCurrent.Add(new sortResult
                            {
                                card = card,
                                option = avaliableSortOptions[i]
                            });
                        }
                        List<messageSystemValue> values = new List<messageSystemValue>();
                        values.Add(new messageSystemValue { hint = InterString.Get("顺发动顺序排序"), value = "shun" });
                        values.Add(new messageSystemValue { hint = InterString.Get("逆发动顺序排序"), value = "fan" });
                        values.Add(new messageSystemValue { hint = InterString.Get("确认其他场上的卡"), value = "hide" });
                        RMSshow_singleChoice("sort", values);
                    }
                    if (ES_sortResult.Count == ES_sortSum)
                    {
                        sendSorted();
                    }
                    else
                    {
                        for (int i = 0; i < ES_sortResult.Count; i++)
                        {
                            ES_sortResult[i].card.show_number(i + 1, true);
                        }
                    }
                }
                break;
            case GameMessage.SelectCard:
            case GameMessage.SelectTribute:
            case GameMessage.SelectSum:
                if (card.forSelect)
                {
                    bool selectable = false;

                    for (int i = 0; i < cardsSelectable.Count; i++)
                    {
                        if (card == cardsSelectable[i])
                        {
                            selectable = true;
                        }
                    }

                    if (selectable)
                    {
                        bool selected = false;
                        for (int i = 0; i < cardsSelected.Count; i++)
                        {
                            if (card == cardsSelected[i])
                            {
                                selected = true;
                            }
                        }
                        if (selected == false)
                        {
                            cardsSelected.Add(card);
                        }
                        else
                        {
                            cardsSelected.Remove(card);
                        }
                    }
                    else
                    {
                        cardsSelected.Remove(card);
                    }
                    realizeCardsForSelect();
                }
                break;
            case GameMessage.SelectUnselect:
                if (card.forSelect)
                {
                    cardsSelected.Add(card);
                    gameInfo.removeHashedButton("sendSelected");
                    sendSelectedCards();
                    realize();
                    toNearest();
                }
                break;
            case GameMessage.SelectChain:
                if (card.forSelect)
                {
                    if (card.effects.Count > 0)
                    {
                        if (card.effects.Count == 1)
                        {
                            BinaryMaster binaryMaster = new BinaryMaster();
                            binaryMaster.writer.Write(card.effects[0].ptr);
                            sendReturn(binaryMaster.get());
                        }
                        else
                        {
                            List<messageSystemValue> values = new List<messageSystemValue>();
                            for (int i = 0; i < card.effects.Count; i++)
                            {
                                if (card.effects[i].flag == 0)
                                {
                                    if (card.effects[i].desc.Length > 2)
                                    {
                                        values.Add(new messageSystemValue { hint = card.effects[i].desc, value = card.effects[i].ptr.ToString() });
                                    }
                                    else
                                    {
                                        values.Add(new messageSystemValue { hint = InterString.Get("发动效果@ui"), value = card.effects[i].ptr.ToString() });
                                    }
                                }
                                if (card.effects[i].flag == 1)
                                {
                                    values.Add(new messageSystemValue { hint = InterString.Get("适用「[?]」的效果", card.get_data().Name), value = card.effects[i].ptr.ToString() });
                                }
                                if (card.effects[i].flag == 2)
                                {
                                    values.Add(new messageSystemValue { hint = InterString.Get("重置「[?]」的控制权", card.get_data().Name), value = card.effects[i].ptr.ToString() });
                                }
                            }
                            values.Add(new messageSystemValue { hint = InterString.Get("取消"), value = "hide" });
                            RMSshow_singleChoice("return", values);
                        }
                    }
                }
                break;
            case GameMessage.SelectPlace:
                break;
            case GameMessage.SelectPosition:
                break;
            case GameMessage.SelectCounter:
                if (card.forSelect)
                {
                    if (card.counterSELcount < card.counterCANcount)
                    {
                        card.counterSELcount++;
                    }
                    int sum = 0;
                    for (int i = 0; i < allCardsInSelectMessage.Count; i++)
                    {
                        sum += allCardsInSelectMessage[i].counterSELcount;
                    }
                    if (sum == ES_min)
                    {
                        BinaryMaster binaryMaster = new BinaryMaster();
                        for (int i = 0; i < allCardsInSelectMessage.Count; i++)
                        {
                            binaryMaster.writer.Write((short)allCardsInSelectMessage[i].counterSELcount);
                        }
                        sendReturn(binaryMaster.get());
                    }
                    else
                    {
                        for (int i = 0; i < allCardsInSelectMessage.Count; i++)
                        {
                            allCardsInSelectMessage[i].show_number(allCardsInSelectMessage[i].counterSELcount);
                        }
                    }
                }
                break;
            case GameMessage.SelectDisfield:
                break;
            case GameMessage.AnnounceRace:
                break;
            case GameMessage.AnnounceAttrib:
                break;
            case GameMessage.AnnounceCard:
                if (card.forSelect)
                {
                    BinaryMaster binaryMaster = new BinaryMaster();
                    binaryMaster.writer.Write((UInt32)card.get_data().Id);
                    sendReturn(binaryMaster.get());
                }
                break;
            case GameMessage.AnnounceNumber:
                break;
        }
    }

    List<placeSelector> placeSelectors = new List<placeSelector>();

    public void ES_placeSelected(placeSelector data)
    {
        data.selected = !data.selected;
        switch (currentMessage) 
        {
            case GameMessage.SelectPlace:
            case GameMessage.SelectDisfield:
                int all = 0;
                BinaryMaster binaryMaster = new BinaryMaster();
                for (int i = 0; i < placeSelectors.Count; i++)
                {
                    if (placeSelectors[i].selected)
                    {
                        binaryMaster.writer.Write(placeSelectors[i].data);
                        all++;
                    }
                }
                if (all == ES_min)
                {
                    ES_min = -2;
                    sendReturn(binaryMaster.get());
                }
                if (ES_min == -2)
                {
                    clearAllSelectPlace();
                }
                break;
            default:
                clearResponse();
                break;
        }
    }

    public override void ES_RMS(string hashCode, List<messageSystemValue> result)
    {
        base.ES_RMS(hashCode, result);
        BinaryMaster binaryMaster;
        switch (hashCode)
        {
            case "return":
                if (result[0].value != "hide")
                {
                    try
                    {
                        binaryMaster = new BinaryMaster();
                        binaryMaster.writer.Write(Int32.Parse(result[0].value));
                        sendReturn(binaryMaster.get());
                    }
                    catch (System.Exception e)
                    {
                        UnityEngine.Debug.Log(e);
                    }
                }
                break;
            case "autoForceChainHandler":
                if (result[0].value != "hide")
                {
                    if (result[0].value == "yes")
                    {
                        autoForceChainHandler = autoForceChainHandlerType.autoHandleAll;
                        try
                        {
                            int answer = -1;
                            foreach (var card in chainCards)
                            {
                                foreach (var effect in card.effects)
                                {
                                    if (effect.forced)
                                    {
                                        answer = effect.ptr;
                                        break;
                                    }
                                }
                                if (answer >= 0) break;
                            }
                            binaryMaster = new BinaryMaster();
                            binaryMaster.writer.Write(answer >= 0 ? answer : 0);
                            sendReturn(binaryMaster.get());
                        }
                        catch (System.Exception e)
                        {
                            UnityEngine.Debug.Log(e);
                        }
                    }
                    if (result[0].value == "no")
                    {
                        autoForceChainHandler = autoForceChainHandlerType.afterClickManDo;
                    }
                }
                break;
            case "returnMultiple":
                binaryMaster = new BinaryMaster();
                UInt32 res = 0;
                foreach (var item in result)
                {
                    try
                    {
                        res |= UInt32.Parse(item.value);
                    }
                    catch (System.Exception e)
                    {
                        UnityEngine.Debug.Log(e);
                    }
                }
                binaryMaster.writer.Write(res);
                sendReturn(binaryMaster.get());
                break;
            case "AnnounceCard":
                List<YGOSharp.Card> datas = YGOSharp.CardsManager.search(result[0].value, ES_searchCode);
                int max = datas.Count;
                if (max > 49)
                {
                    max = 49;
                }
                for (int i = 0; i < max; i++)
                {
                    GPS p = new GPS
                    {
                        controller = 0,
                        location = (UInt32)CardLocation.Search,
                        sequence = (UInt32)i,
                        position = 0,
                    };
                    gameCard card = GCS_cardCreate(p);
                    card.set_data(datas[i]);
                    card.forSelect = true;
                    card.add_one_decoration(Program.I().mod_ocgcore_decoration_card_selecting, 2, Vector3.zero, "card_selecting");
                }
                realize();
                gameInfo.addHashedButton("clear", 0, superButtonType.no, InterString.Get("重新输入@ui"));
                toNearest();
                gameField.setHint(InterString.Get("请选择需要宣言的卡片。"));
                break;
            case "sort":
                if (result[0].value != "hide")
                {
                    for (int i = 0; i < cardsInSort.Count; i++)
                    {
                        cardsInSort[i].show_number(0);
                    }
                    if (result[0].value == "shun")
                    {
                        for (int i = 0; i < ES_sortCurrent.Count; i++)
                        {
                            ES_sortResult.Add(ES_sortCurrent[i]);
                        }
                    }
                    if (result[0].value == "fan")
                    {
                        for (int i = 0; i < ES_sortCurrent.Count; i++)
                        {
                            ES_sortResult.Add(ES_sortCurrent[ES_sortCurrent.Count - i - 1]);
                        }
                    }
                    if (ES_sortResult.Count == ES_sortSum)
                    {
                        sendSorted();
                    }
                    else
                    {
                        for (int i = 0; i < ES_sortResult.Count; i++)
                        {
                            ES_sortResult[i].card.show_number(i + 1, true);
                        }
                    }
                }
                break;
            case "RockPaperScissors":
                {
                    try
                    {
                        binaryMaster = new BinaryMaster();
                        binaryMaster.writer.Write(Int32.Parse(result[0].value));
                        sendReturn(binaryMaster.get());
                    }
                    catch (Exception e)
                    {
                        Debug.Log(e);
                    }
                }
                break;
        }
    }

    public override void ES_RMS_ForcedYesNo(messageSystemValue result)
    {
        base.ES_RMS_ForcedYesNo(result);
        if (result.value == "yes")
        {
            surrended = true;
            if (TcpHelper.tcpClient != null && TcpHelper.tcpClient.Connected)
            {
                if (paused) 
                {
                    EventDelegate.Execute(UIHelper.getByName<UIButton>(toolBar, "go_").onClick);
                }
                TcpHelper.CtosMessage_Surrender();
            }
            else
            {
                onExit();
            }
        }
    }

    public Dictionary<int, int> sideReference = new Dictionary<int, int>();

    // 排查用：qt_endduel.on 的一次性触发闩锁（见 preFrameFunction）
    bool endDuelTriggered = false;

    // 排查用：qt_endduel.on 达成触发条件的那一帧（Program.TimePassed 毫秒）；-1 = 未起算
    int endDuelArmMs = -1;

    // 排查用：qt_undoreplay.on（整局重放）的一次性闩锁与起算时刻
    bool replayAllTriggered = false;
    int replayAllArmMs = -1;

    /// <summary>
    /// log/qt_endduel.wait（秒，浮点）存在时，「对局自动收尾」会延后这么多秒再触发，
    /// 便于先让对局真的跑出一段入站消息再收尾。缺省/非法 = 0 秒（立即）。
    /// </summary>
    static int EndDuelDelayMs()
    {
        try
        {
            if (System.IO.File.Exists(QuickTestTrace.LogPath("qt_endduel.wait")))
            {
                float sec;
                if (float.TryParse(System.IO.File.ReadAllText(QuickTestTrace.LogPath("qt_endduel.wait")).Trim(),
                        System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out sec) && sec > 0f)
                {
                    return (int)(sec * 1000f);
                }
            }
        }
        catch (System.Exception)
        {
        }
        return 0;
    }

    public void onDuelResultConfirmed()
    {
        Program.I().room.joinWithReconnect = false;

        // ── 「测试直开局」收尾：打完一局直接回卡组编辑器 ────────────────────────
        //
        // AI 测试局是从卡组界面点「测试」发起的，收尾必须回到卡组界面。原实现走通用
        // 返回路径，有两个后果（用户实测反馈「结束一局会进空界面、不能直接回卡组编辑」）：
        //   1. 服务端在比赛制里会发 ChangeSide（Room.StocMessage_ChangeSide 把 needSide
        //      置位并把 returnServant 拨到 deckManager），客户端于是进 DeckManager 的
        //      changeSide（换备）界面 —— 那个界面的候选来自 ocgcore.sideReference
        //      「对手使用过的卡」，AI 模式没有这份数据，界面上必然是空的；
        //   2. DeckManager.hide() 离开编辑器时已经 destroyCard() 掉全部卡、并把 deck
        //      重置成空 Deck，而 returnTo() 只会 shiftToServant(deckManager)、不会重新
        //      loadDeckFromYDK —— 于是就算回到编辑器，看到的也是空卡组，没法接着改。
        // 这里在通用分支之前收口，口径对齐 selectDeck.KF_editDeck()。
        if (Program.I().room != null && Program.I().room.quickThisDuel)
        {
            Program.I().room.duelEnded = false;
            Program.I().room.needSide = false;
            Program.I().room.sideWaitingObserver = false;
            surrended = false;

            DeckManager dm = Program.I().deckManager;
            string quickDeck = GameModeManager.DeckInUse;
            string quickPath = GameModeManager.DeckPath(quickDeck);
            bool hasDeck = System.IO.File.Exists(quickPath);

            // show() 会按 condition 决定桌面碰撞盒尺寸，且会顺带重建工具条，
            // 所以 condition 必须在 shiftToServant 之前拨回来。
            if (dm != null)
            {
                dm.shiftCondition(DeckManager.Condition.editDeck);
            }
            returnServant = Program.I().deckManager;
            onExit();                       // 关 socket + kill AI.Server/WindBot + returnTo()

            if (dm != null && hasDeck && dm.isShowed)
            {
                dm.loadDeckFromYDK(quickPath);   // hide() 清空过 deck，这里重新载入
                ((CardDescription)Program.I().cardDescription).setTitle(quickDeck);
                dm.setGoodLooking();
            }
            // 返回键要能出去。这场对局是从卡组编辑器点「测试」发起的，收尾回到编辑器后
            // 必须补上「返回」动作 —— DeckManager.home() 是 `if (returnAction != null)`，
            // 空着的话工具条上的返回键点了没反应，人就卡在编辑器里出不去（用户实测反馈）。
            // 这里和从卡组列表进编辑器（selectDeck.KF_editDeck）装的是同一个动作；
            // 不放进上面那个 if 里：卡组文件缺失时照样得能退出去。
            if (dm != null)
            {
                Program.I().selectDeck.armDeckEditorReturn();
            }
            QuickTestTrace.Log("end", "quick duel end -> deckEditor deck=" + quickDeck
                + " hasDeck=" + hasDeck + " showed=" + (dm != null && dm.isShowed)
                + " returnAction=" + (dm != null && dm.returnAction != null));
            return;
        }

        if (Program.I().room.duelEnded == true || surrended || TcpHelper.tcpClient == null || TcpHelper.tcpClient.Connected == false)
        {
            // 打完这一局要回哪个界面，就看这一行：卡组测试局回卡组编辑器（上面那支），
            // 主菜单人机对战回人机界面（returnServant 由 AIRoom.launch 拨向 aiRoom）。
            QuickTestTrace.Log("end", "duel end -> onExit, returnServant="
                + DuelUndo.ServantName(returnServant)
                + " quick=" + Program.I().room.quickThisDuel);
            surrended = false;
            Program.I().room.duelEnded = false;
            Program.I().room.needSide = false;
            Program.I().room.sideWaitingObserver = false;
            onExit();
            return;
        }

        if (Program.I().room.needSide == true)
        {
            Program.I().room.needSide = false;
            RMSshow_none(InterString.Get("右侧为您准备了对手上一局使用的卡。"));
            ((DeckManager)Program.I().deckManager).shiftCondition(DeckManager.Condition.changeSide);
            returnTo();
            ((DeckManager)Program.I().deckManager).deck = TcpHelper.deck;
            ((DeckManager)Program.I().deckManager).FormCodedDeckToObjectDeck();
            ((CardDescription)Program.I().cardDescription).setTitle(GameModeManager.DeckInUse);
            ((DeckManager)Program.I().deckManager).setGoodLooking(true);
            ((DeckManager)Program.I().deckManager).returnAction = returnFromDeckEdit;
            return;
        }

        if (condition != Condition.duel)
        {
            hideCaculator();
            return;
        }

        RMSshow_yesOrNoForce(InterString.Get("你确定要投降吗？"), new messageSystemValue { value = "yes", hint = "yes" }, new messageSystemValue { value = "no", hint = "no" });
    }

    private void sendSorted()
    {
        BinaryMaster m = new BinaryMaster();
        byte[] bytes = new byte[ES_sortResult.Count];
        for (int i = 0; i < ES_sortResult.Count; i++)
        {
            bytes[ES_sortResult[i].option] = (byte)i;
        }
        for (int i = 0; i < ES_sortResult.Count; i++)
        {
            m.writer.Write(bytes);
        }
        sendReturn(m.get());
    }

    bool rightExcited = false;
    public override void ES_mouseDownRight()
    {
        if (gameInfo.queryHashedButton("sendSelected") == true)
        {
            return;
        }
        if (flagForCancleChain)
        {
            return;
        }
        if (gameInfo.queryHashedButton("hide_all_card") == true)
        {
            if (flagForTimeConfirm)
            {
                return;
            }
        }
        if (gameInfo.queryHashedButton("cancleSelected") == true)
        {
            return;
        }
        if (gameInfo.queryHashedButton("cancelPlace") == true)
        {
            return;
        }
        rightExcited = true;
        //gameInfo.ignoreChain_set(true);
        base.ES_mouseDownRight();
    }


    bool leftExcited = false;
    public override void ES_mouseDownEmpty()    
    {
        if (Program.I().setting.setting.spyer.value == false)
            if (gameInfo.queryHashedButton("hide_all_card") == false)
            {
                //gameInfo.keepChain_set(true);
                leftExcited = true;
            }
        base.ES_mouseDownEmpty();
    }

    public override void ES_mouseUpEmpty()
    {
        if (Program.I().setting.setting.spyer.value)
        {
            if (cantCheckGrave)
                RMSshow_none(InterString.Get("不能确认墓地里的卡，监控全局卡片功能暂停使用。"));
            else
                Program.I().cardDescription.shiftCardShower(false);
        }
        if (gameInfo.queryHashedButton("hide_all_card") == true)
        {
            if (flagForTimeConfirm)
            {
                flagForTimeConfirm = false;
                MessageBeginTime = Program.TimePassed();
            }
            clearAllShowed();
        }
        else
        {
            if (Program.I().setting.setting.spyer.value == false)
                if (leftExcited)
                {
                    if (Input.GetKey(KeyCode.A) == false)
                    {
                        leftExcited = false;
                        //gameInfo.keepChain_set(false);
                    }

                }
        }
        base.ES_mouseUpEmpty();
    }

    public override void ES_mouseUpGameObject(GameObject gameObject)
    {
        if (gameObject==gameInfo.instance_lab.gameObject)  
        {
            ES_mouseUpEmpty();
            return;
        }
        if (leftExcited)
        {
            if (Input.GetKey(KeyCode.A) == false)
            {
                leftExcited = false;
                //gameInfo.keepChain_set(false);
            }
        }
        base.ES_mouseUpGameObject(gameObject);
    }

    public override void ES_mouseUpRight()
    {
        base.ES_mouseUpRight();
        if (rightExcited)
        {
            if (Input.GetKey(KeyCode.S) == false)
            {
                rightExcited = false;
                //gameInfo.ignoreChain_set(false);
            }
        }
        if (gameInfo.queryHashedButton("sendSelected") == true)
        {
            sendSelectedCards();
            return;
        }
        if (flagForCancleChain)
        {
            flagForCancleChain = false;
            clearAllShowedB = true;
            BinaryMaster binaryMaster = new BinaryMaster();
            binaryMaster.writer.Write((Int32)(-1));
            sendReturn(binaryMaster.get());
            return;
        }
        if (gameInfo.queryHashedButton("hide_all_card") == true)
        {
            if (flagForTimeConfirm)
            {
                flagForTimeConfirm = false;
                MessageBeginTime = Program.TimePassed();
                clearAllShowed();
                return;
            }
        }
        if (gameInfo.queryHashedButton("cancleSelected") == true)
        {
            BinaryMaster binaryMaster = new BinaryMaster();
            binaryMaster.writer.Write(-1);
            sendReturn(binaryMaster.get());
            return;
        }
        if (gameInfo.queryHashedButton("cancelPlace") == true)
        {
            cancelSelectPlace();
            return;
        }
    }

    void animation_confirm(gameCard target)
    {
        Program.I().cardDescription.setData(target.get_data(), target.p.controller == 0 ? GameTextureManager.myBack : GameTextureManager.opBack, target.tails.managedString);
        target.animation_confirm_screenCenter(new Vector3(-30, 0, 0), 0.2f, 0.5f);
    }

    public void animation_show_card_code(long code)
    {
        code_for_show = code;
        AddUpdateAction_s(animation_show_card_code_handler);
        Sleep(30);
    }
    long code_for_show = 0;

    public bool InAI = false;

    void animation_show_card_code_handler()
    {
        Texture2D texture = GameTextureManager.get(code_for_show, GameTextureType.card_picture);
        if (texture != null)
        {
            RemoveUpdateAction_s(this.animation_show_card_code_handler);
            //Vector3 position = Program.camera_game_main.ScreenToWorldPoint(new Vector3(getScreenCenter(), Screen.height / 2f, 10));
            //GameObject obj = create_s(Program.I().mod_simple_quad);
            //obj.AddComponent<animation_screen_lock>().screen_point = new Vector3(getScreenCenter(), Screen.height / 2f, 6);
            //obj.transform.eulerAngles = new Vector3(60, 0, 0);
            //obj.GetComponent<Renderer>().material.mainTexture = texture;
            //obj.transform.localPosition = position;
            //obj.transform.localScale = new Vector3(3.2f, 4.6f, 1f);
            //destroy(obj, 1f);
            pro1CardShower shower = create(Program.I().Pro1_CardShower, Program.I().ocgcore.centre(), Vector3.zero, false, Program.ui_main_2d, true).GetComponent<pro1CardShower>();
            shower.card.mainTexture = texture;
            shower.mask.mainTexture = GameTextureManager.Mask;
            shower.disable.mainTexture = GameTextureManager.negated;
            shower.gameObject.transform.localScale = new Vector3(Screen.height / 650f, Screen.height / 650f, Screen.height / 650f);
            destroy(shower.gameObject, 0.5f);
        }
    }
}
