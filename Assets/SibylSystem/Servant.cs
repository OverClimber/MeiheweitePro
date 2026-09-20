using UnityEngine;
using System;
using System.Collections.Generic;
using YGOSharp.OCGWrapper.Enums;

public class Servant
{
    public GameObject gameObject;

    public bool isShowed = false;

    List<GameObject> allGameObjects = new List<GameObject>();

    List<Action> updateActions = new List<Action>();

    List<Action> updateActions_s = new List<Action>();


    public Servant()
    {
        initialize();
        AddUpdateAction(preFrameFunction);
    }

    public virtual void initialize()
    {

    }

    public virtual void show()
    {
        if (isShowed == false)
        {
            isShowed = true;
            Program.notGo(fixScreenProblem);
            Program.go(50, fixScreenProblem);
        }
    }

    public virtual void hide()
    {
        RMSshow_clear();
        RMSshow_clearYNF();
        if (isShowed == true)
        {
            isShowed = false;
            Program.notGo(fixScreenProblem);
            Program.go(50, fixScreenProblem);
        }
        for (int i = 0; i < allGameObjects.Count; i++)
        {
            Program.I().destroy(allGameObjects[i], 0, false, true);
        }
        allGameObjects.Clear();
        updateActions_s.Clear();
        for (int i = 0; i < delayedTasks.Count; i++)
        {
            Program.notGo(delayedTasks[i].act);
        }
        delayedTasks.Clear();
    }

    public virtual void fixScreenProblem()
    {
        if (isShowed)
        {
            applyShowArrangement();
        }
        else
        {
            applyHideArrangement();
        }
    }

    public void safeObject(GameObject o)
    {
        allGameObjects.Add(o);
    }

    public virtual void preFrameFunction()
    {

    }

    public virtual void ES_mouseDownEmpty()
    {

    }

    public virtual void ES_mouseDownGameObject(GameObject gameObject)
    {

    }

    public virtual void ES_mouseUp()
    {

    }

    public virtual void ES_mouseDownRight()    
    {

    }

    public virtual void ES_mouseUpRight()
    {

    }

    public virtual void ES_mouseUpEmpty()
    {

    }

    public virtual void ES_mouseUpGameObject(GameObject gameObject)
    {

    }

    public virtual void ES_HoverOverGameObject(GameObject gameObject)
    {

    }

    public void showBarOnly()
    {
        if (toolBar != null)
        {
            Vector3 vectorOfShowedBar_Screen = new Vector3(Screen.width - RightToScreen, buttomToScreen, 0);
            iTween.MoveTo(toolBar, Program.camera_back_ground_2d.ScreenToWorldPoint(vectorOfShowedBar_Screen), 0.6f);
            toolBar.transform.localScale = new Vector3(((float)Screen.height) / 700f, ((float)Screen.height) / 700f, ((float)Screen.height) / 700f);
            var items = toolBar.GetComponentsInChildren<toolShift>();
            for (int i = 0; i < items.Length; i++)  
            {
                items[i].enabled = true;
            }
        }
    }

    public void hideBarOnly()
    {
        if (toolBar != null)
        {
            Vector3 vectorOfHidedBar_Screen = new Vector3(Screen.width - RightToScreen, -100, 0);
            iTween.MoveTo(toolBar, Program.camera_back_ground_2d.ScreenToWorldPoint(vectorOfHidedBar_Screen), 0.6f);
            toolBar.transform.localScale = new Vector3(((float)Screen.height) / 700f, ((float)Screen.height) / 700f, ((float)Screen.height) / 700f);
            var items = toolBar.GetComponentsInChildren<toolShift>();
            for (int i = 0; i < items.Length; i++)
            {
                items[i].enabled = false;
            }
        }
    }

    public virtual void applyShowArrangement()
    {
        showBarOnly();
    }

    public virtual void applyHideArrangement()
    {
        hideBarOnly();
    }

    public virtual void ES_quit()
    {

    }

    GameObject preHover = null;

    public void Update()
    {
        if (isShowed)
        {
            for (int i = 0; i < updateActions.Count; i++)
            {
                updateActions[i]();
            }
            for (int i = 0; i < updateActions_s.Count; i++)
            {
                updateActions_s[i]();
            }
            if (Program.InputGetMouseButtonDown_0)
            {
                if (Program.pointedGameObject == null)
                {
                    ES_mouseDownEmpty();
                }
                else
                {
                    ES_mouseDownGameObject(Program.pointedGameObject);
                }
            }
            if (Program.InputGetMouseButtonUp_0)
            {
                if (Program.pointedGameObject == null)
                {
                    ES_mouseUpEmpty();
                }
                else
                {
                    ES_mouseUpGameObject(Program.pointedGameObject);
                }
                ES_mouseUp();
            }
            if (Program.InputGetMouseButtonDown_1)
            {
                ES_mouseDownRight();
            }
            if (Program.InputGetMouseButtonUp_1)
            {
                ES_mouseUpRight();
            }
            if (preHover != Program.pointedGameObject)
            {
                preHover = Program.pointedGameObject;
                if (preHover!=null) 
                    ES_HoverOverGameObject(preHover);
            }
        }
    }

    public void OnQuit()
    {
        ES_quit();
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
        var re = Program.I().create(mod, position, rotation, fade, father, allParamsInWorld, wantScale);
        return re;
    }

    public GameObject create_s(
        GameObject mod,
        Vector3 position = default(Vector3),
        Vector3 rotation = default(Vector3),
        bool fade = false,
        GameObject father = null,
        bool allParamsInWorld = true,
        Vector3 wantScale = default(Vector3)
        )
    {
        var re = Program.I().create(mod, position, rotation, fade, father, allParamsInWorld, wantScale);
        allGameObjects.Add(re);
        return re;
    }

    public void destroy(GameObject obj, float time = 0, bool fade = false, bool instantNull = false)
    {
        allGameObjects.Remove(obj);
        Program.I().destroy(obj, time, fade, instantNull);
    }

    public void AddUpdateAction(Action action)
    {
        updateActions.Add(action);
    }

    public void RemoveUpdateAction(Action action)
    {
        updateActions.Remove(action);
    }

    public void AddUpdateAction_s(Action action)
    {
        updateActions_s.Add(action);
    }

    public void RemoveUpdateAction_s(Action action)
    {
        updateActions_s.Remove(action);
    }

    public GameObject toolBar;

    float buttomToScreen;

    float RightToScreen;

    public void SetBar(GameObject mod,float buttomToScreen,float RightToScreen)
    {
        this.buttomToScreen = buttomToScreen;
        this.RightToScreen = RightToScreen;
        if (toolBar!=null)
        {
            MonoBehaviour.DestroyImmediate(toolBar);
        }
        toolBar = create
            (
            mod,
            Program.camera_main_2d.ScreenToWorldPoint(new Vector3(Screen.width - RightToScreen, -100, 0)),
            new Vector3(0, 0, 0),
            false,
            Program.ui_main_2d
            );
        UIHelper.InterGameObject(toolBar);
        fixScreenProblem();
    }

    public void reShowBar(float buttomToScreen, float RightToScreen)
    {
        this.buttomToScreen = buttomToScreen;
        this.RightToScreen = RightToScreen; 
        if (isShowed)   
        {
            showBarOnly();
        }
    }

    List<Program.delayedTask> delayedTasks = new List<Program.delayedTask>();
    public void safeGogo(int delay_, Action act_)
    {
        Program.go(delay_, act_);
        delayedTasks.Add(new Program.delayedTask
        {
            act = act_,
            timeToBeDone = delay_ + Program.TimePassed(),
        });
    }

    #region remasterMessageSystem

    public Vector3 centre(bool fix=false)
    {
        if (Program.I().ocgcore.isShowed || Program.I().deckManager.isShowed)
        {
            Vector3 screenP = Program.camera_game_main.WorldToScreenPoint(Vector3.zero);
            screenP.z = 0;
            if (fix)
            {
                if (screenP.y > Screen.height - 350f)
                {
                    screenP.y = Screen.height - 350f;
                }
                if (screenP.y < 350f)
                {
                    screenP.y = 350f;
                }
            }
            return Program.camera_main_2d.ScreenToWorldPoint(screenP);
        }
        else
        {
            return Program.camera_main_2d.ScreenToWorldPoint(new Vector3(Screen.width / 2, Screen.height / 2, 0));
        }
    }

    Vector3 MSentre()
    {
        if (Program.I().ocgcore.isShowed)
        {
            float real = (Program.fieldSize - 1) * 0.9f + 1f;
            Vector3 screenP = Program.camera_game_main.WorldToScreenPoint(new Vector3(0, 0, -5.65f * real));
            screenP.z = 0;
            return Program.camera_main_2d.ScreenToWorldPoint(screenP);
        }
        if (Program.I().deckManager.isShowed)
        {
            Vector3 screenP = Program.camera_game_main.WorldToScreenPoint(Vector3.zero);
            screenP.z = 0;
            return Program.camera_main_2d.ScreenToWorldPoint(screenP);
        }
        return Program.camera_main_2d.ScreenToWorldPoint(new Vector3(Screen.width / 2, Screen.height / 2, 0));
    }

    private enum messageSystemType
    {
        none,
        onlyYes,
        yesOrNo,
        yesOrNoOrCancle,
        yesOrNoOrSee,
        singleChoice,
        multipleChoice,
        input,
        position,
        tp,
    }

    private messageSystemType currentMStype = messageSystemType.none;

    public string currentMShash;

    private GameObject currentMSwindow = null;

    public class messageSystemValue
    {
        public string value = "";
        public string hint = "";
    }

    public virtual void ES_RMS(string hashCode, List<messageSystemValue> result)
    {
        RMSshow_clear();
    }

    /// <summary>这一轮采样用的探针 tag（弹框的调用方给）。</summary>
    private string msTraceTag = "";

    /// <summary>上一拍报出去的坐标串 —— 「变了才写」靠它比较。</summary>
    private string msTraceLast = "";

    /// <summary>还剩几拍采样；0 表示不再排下一拍。</summary>
    private int msTraceTicksLeft = 0;

    private const int MS_TRACE_INTERVAL = 150;   // 采样间隔（毫秒）
    private const int MS_TRACE_TICKS = 10;       // 10 拍 × 150ms ⇒ 覆盖弹框后约 1.5 秒

    /// <summary>
    /// 排查用：把当前 RMS 对话框里「是/否」按钮的屏幕坐标落到轨迹里。
    /// 验收脚本要在真光标下点这两个按钮，而对话框位置随分辨率/当前界面（决斗场 / 卡组 /
    /// 主菜单的 <c>MSentre()</c> 落点各不相同）变，脚本没法靠猜坐标点中 —— 口径与
    /// <c>SelectServer.LogAnchor</c>、<c>Ocgcore.dumpUndoButtonPositions</c> 一致：
    /// 这些窗口挂在 <c>camera_main_2d</c> 下，必须用这台相机换算，用背景相机会映射到屏幕外。
    /// </summary>
    public void TraceMSButtons(string tag)
    {
        if (!QuickTestTrace.Enabled)
        {
            return;
        }
        if (currentMSwindow == null)
        {
            QuickTestTrace.Log(tag, "MS window = null");
            return;
        }
        // ⚠ 框是 iTween 弹出来的，**位置在飞**。曾经的实现只 dump 两拍（立即 +250ms），
        //   撞上机器卡顿就被坑惨：第二拍采样时框还没落定（实测 (1042,459) → (978,497)，
        //   终位 (901,541) **从没落盘**），脚本拿着中途坐标点了三次全空、整段判据假红。
        //   现在改成**变了才写**（与 [opt] 探针同一套口径）：一段窗口内多拍采样，坐标与
        //   上一拍相同就不落行 ⇒ 最后一行必然是落定值，脚本按「这行多久没变」判落定即可。
        msTraceTag = tag;
        msTraceLast = "";
        msTraceTicksLeft = MS_TRACE_TICKS;
        TraceMSSample();
    }

    /// <summary>采样一拍：坐标与上一拍不同才落盘，然后排下一拍（框关掉了就自然停）。</summary>
    private void TraceMSSample()
    {
        if (!QuickTestTrace.Enabled || currentMSwindow == null || msTraceTicksLeft <= 0)
        {
            return;
        }
        msTraceTicksLeft--;
        string now = MSButtonCoord("yes_") + "|" + MSButtonCoord("no_");
        if (now != msTraceLast)
        {
            msTraceLast = now;
            TraceOneMSButton(msTraceTag, "yes_");
            TraceOneMSButton(msTraceTag, "no_");
        }
        if (msTraceTicksLeft > 0)
        {
            Program.go(MS_TRACE_INTERVAL, () => TraceMSSample());
        }
    }

    /// <summary>某个按钮的客户区屏幕坐标串。只用于「变没变」的比较，不落盘。</summary>
    private string MSButtonCoord(string name)
    {
        Transform t = UIHelper.getByName<Transform>(currentMSwindow, name);
        if (t == null)
        {
            return name + ":null";
        }
        Vector3 sp = Program.camera_main_2d.WorldToScreenPoint(t.position);
        return name + ":" + Mathf.RoundToInt(sp.x) + "," + Mathf.RoundToInt(Screen.height - sp.y);
    }

    private void TraceOneMSButton(string tag, string name)
    {
        Transform t = UIHelper.getByName<Transform>(currentMSwindow, name);
        if (t == null)
        {
            QuickTestTrace.Log(tag, name + " = null");
            return;
        }
        Vector3 sp = Program.camera_main_2d.WorldToScreenPoint(t.position);
        QuickTestTrace.Log(tag, name + " screen=(" + Mathf.RoundToInt(sp.x) + ","
            + Mathf.RoundToInt(Screen.height - sp.y) + ")");
    }

    void ES_RMSpremono(GameObject gameObjectClicked, messageSystemValue value)
    {
        List<messageSystemValue> re;
        switch (currentMStype)  
        {
            case messageSystemType.onlyYes:
            case messageSystemType.yesOrNo:
            case messageSystemType.yesOrNoOrCancle:
            case messageSystemType.yesOrNoOrSee:
            case messageSystemType.singleChoice:
            case messageSystemType.input:
            case messageSystemType.position:
            case messageSystemType.tp:
                re = new List<messageSystemValue>();
                re.Add(value);
                ES_RMS(currentMShash, re);
                break;
            case messageSystemType.multipleChoice:
                bool exist = false;
                for (int i = 0; i < RMSshow_multipleChoice_selected.Count; i++)
                {
                    if (RMSshow_multipleChoice_selected[i] == value)
                    {
                        exist = true;
                    }
                }
                UILabel lab = gameObjectClicked.GetComponentInChildren<UILabel>();
                if (exist)
                {
                    RMSshow_multipleChoice_selected.Remove(value);
                    if (lab != null)
                    {
                        Color c = lab.color;
                        c.a = 1f;
                        lab.color = c;
                    }
                }
                else
                {
                    RMSshow_multipleChoice_selected.Add(value);
                    if (lab != null)
                    {
                        Color c = lab.color;
                        c.a = 0.3f;
                        lab.color = c;
                    }
                }
                if (RMSshow_multipleChoice_selected.Count == RMSshow_multipleChoice_count)
                {
                    ES_RMS(currentMShash, RMSshow_multipleChoice_selected);
                }
                break;
        }
    }

    public void RMSshow_clear()
    {
        if (QuickTestTrace.Enabled && currentMSwindow != null)
        {
            // 对话框生命周期探针（只在真有框要关时落一条）。
            // ⚠ 这条是「点否生效」的唯一正面证据：点「否」的语义就是**什么都不发**，
            //   于是「点否成功」和「点空了」在字节/应答层面完全一样 —— 没有这条，
            //   脚本那两条否定判据都会假通过。配套的正面证据在 RMSshow_yesOrNo 里。
            QuickTestTrace.Log("ms", "clear hash=" + currentMShash + " type=" + currentMStype);
        }
        currentMStype = messageSystemType.none;
        currentMShash = "NULL";
        if (currentMSwindow != null)
        {
            destroy(currentMSwindow, 0.1f, false, true);
            currentMSwindow = null;
        }
    }

    public void RMSshow_clearYNF()
    {
        if (yesOrNoForce != null)
        {
            destroy(yesOrNoForce, 0.1f, false, true);
            yesOrNoForce = null;
        }
    }

    public bool IfNoMessage()
    {
        return currentMShash == "NULL";
    }

    public void RMSshow_onlyYes(string hashCode, string hint, messageSystemValue yes)
    {
        RMSshow_clear();
        currentMShash = hashCode;
        currentMStype = messageSystemType.onlyYes;
        currentMSwindow = create
            (
            Program.I().ES_1,
            MSentre(),
            Vector3.zero,
            true,
            Program.ui_main_2d,
            true,
            new Vector3(((float)Screen.height) / 700f, ((float)Screen.height) / 700f, ((float)Screen.height) / 700f)
            );
        UIHelper.InterGameObject(currentMSwindow);
        UIHelper.trySetLableText(currentMSwindow, "hint_", hint);
        UIHelper.registEvent(currentMSwindow, "yes_", ES_RMSpremono, yes);
    }

    public void RMSshow_yesOrNo(string hashCode, string hint, messageSystemValue yes, messageSystemValue no)
    {
        RMSshow_clear();
        currentMShash = hashCode;
        currentMStype = messageSystemType.yesOrNo;
        currentMSwindow = create
            (
            Program.I().ES_2,
            MSentre(),
            Vector3.zero,
            true,
            Program.ui_main_2d,
            true,
            new Vector3(((float)Screen.height) / 700f, ((float)Screen.height) / 700f, ((float)Screen.height) / 700f)
            );
        UIHelper.InterGameObject(currentMSwindow);
        UIHelper.trySetLableText(currentMSwindow, "hint_", hint);
        UIHelper.registEvent(currentMSwindow, "yes_", ES_RMSpremono, yes);
        UIHelper.registEvent(currentMSwindow, "no_", ES_RMSpremono, no);
        if (QuickTestTrace.Enabled)
        {
            QuickTestTrace.Log("ms", "show yesOrNo hash=" + hashCode + " hint=" + hint);
            // 验收脚本要用真实光标点「yes」那一个，但只看 [ms] show 拿不到落点。
            // yesOrNo 这类弹窗的按钮是预制体上固定命名的 yes_ / no_（不像 singleChoice 那样
            // create 出一串），所以按名字查；字段与换算沿用 [msopt]，脚本用同一个解析器。
            GameObject win = currentMSwindow;
            Program.go(60, () => TraceMsNamedOptions(hashCode, win,
                new[] { "yes_", "no_" }, new[] { yes, no }));
            Program.go(200, () => TraceMsNamedOptions(hashCode, win,
                new[] { "yes_", "no_" }, new[] { yes, no }));
            Program.go(400, () => TraceMsNamedOptions(hashCode, win,
                new[] { "yes_", "no_" }, new[] { yes, no }));
            Program.go(700, () => TraceMsNamedOptions(hashCode, win,
                new[] { "yes_", "no_" }, new[] { yes, no }));
        }
    }

    /// <summary>
    /// 两按钮弹窗（yesOrNo 系）的选项落定坐标，供验收脚本按真光标点击。
    /// 被 Program.go 延迟若干批调用，理由同 TraceMsOptions：create 返回这一刻按钮还没落定。
    /// </summary>
    static void TraceMsNamedOptions(
        string hashCode, GameObject window, string[] names, messageSystemValue[] values)
    {
        if (!QuickTestTrace.Enabled || window == null)
        {
            return;
        }
        for (int i = 0; i < names.Length && i < values.Length; i++)
        {
            GameObject btn = UIHelper.getByName(window, names[i]);
            if (btn == null || !btn.activeInHierarchy)
            {
                return;                 // 按钮缺失或弹窗已经关掉，别再报
            }
        }
        for (int i = 0; i < names.Length && i < values.Length; i++)
        {
            GameObject btn = UIHelper.getByName(window, names[i]);
            Vector3 cp = Program.camera_main_2d.WorldToScreenPoint(
                Ocgcore.ButtonWorldCenter(btn));
            Vector3 lp = btn.transform.localPosition;
            string box = btn.GetComponent<BoxCollider>() != null ? "self"
                : (btn.GetComponentInChildren<BoxCollider>() != null ? "child" : "none");
            QuickTestTrace.Log("msopt", hashCode + " i=" + i
                + " value=" + values[i].value
                + " hint=" + values[i].hint
                + " local=(" + Mathf.RoundToInt(lp.x) + "," + Mathf.RoundToInt(lp.y) + ")"
                + " box=" + box
                + " screen=(" + Mathf.RoundToInt(cp.x) + "," + Mathf.RoundToInt(Screen.height - cp.y) + ")");
        }
    }

    private GameObject yesOrNoForce;

    public void RMSshow_yesOrNoForce(string hint, messageSystemValue yes, messageSystemValue no)
    {
        RMSshow_clearYNF();
        yesOrNoForce = create
            (
            Program.I().ES_2Force,
            MSentre(),
            Vector3.zero,
            true,
            Program.ui_main_2d,
            true,
            new Vector3(((float)Screen.height) / 700f, ((float)Screen.height) / 700f, ((float)Screen.height) / 700f)
            );
        UIHelper.InterGameObject(yesOrNoForce);
        UIHelper.trySetLableText(yesOrNoForce, "hint_", hint);
        UIHelper.registEvent(yesOrNoForce, "yes_", ES_RMSpremonoForceYesNo, yes);
        UIHelper.registEvent(yesOrNoForce, "no_", ES_RMSpremonoForceYesNo, no);
    }

    void ES_RMSpremonoForceYesNo(GameObject gameObjectClicked, messageSystemValue value)
    {
        ES_RMS_ForcedYesNo(value);
    }

    public virtual void ES_RMS_ForcedYesNo(messageSystemValue result)
    {
        destroy(yesOrNoForce, 0.6f, true, true);
    }

    public void RMSshow_FS(string hashCode, messageSystemValue first, messageSystemValue second)
    {
        RMSshow_clear();
        currentMShash = hashCode;
        currentMStype = messageSystemType.yesOrNo;
        currentMSwindow = create
            (
            Program.I().ES_FS,
            MSentre(),
            Vector3.zero,
            true,
            Program.ui_main_2d,
            true,
            new Vector3(((float)Screen.height) / 700f, ((float)Screen.height) / 700f, ((float)Screen.height) / 700f)
            );
        UIHelper.InterGameObject(currentMSwindow);
        UIHelper.registEvent(currentMSwindow, "yes_", ES_RMSpremono, first);
        UIHelper.registEvent(currentMSwindow, "no_", ES_RMSpremono, second);
    }

    public void RMSshow_yesOrNoOrCancle(string hashCode, string hint, messageSystemValue yes, messageSystemValue no, messageSystemValue cancle)
    {
        RMSshow_clear();
        currentMShash = hashCode;
        currentMStype = messageSystemType.yesOrNoOrCancle;
        currentMSwindow = create
            (
            Program.I().ES_3cancle,
            MSentre(),
            Vector3.zero,
            true,
            Program.ui_main_2d,
            true,
            new Vector3(((float)Screen.height) / 700f, ((float)Screen.height) / 700f, ((float)Screen.height) / 700f)
            );
        UIHelper.InterGameObject(currentMSwindow);
        UIHelper.trySetLableText(currentMSwindow, "hint_", hint);
        UIHelper.registEvent(currentMSwindow, "yes_", ES_RMSpremono, yes);
        UIHelper.registEvent(currentMSwindow, "no_", ES_RMSpremono, no);
        UIHelper.registEvent(currentMSwindow, "cancle_", ES_RMSpremono, cancle);
    }

    public void RMSshow_singleChoice(string hashCode, List<messageSystemValue> options)
    {
        RMSshow_clear();
        currentMShash = hashCode;
        currentMStype = messageSystemType.singleChoice;
        currentMSwindow = create
            (
            Program.I().ES_Single_multiple_window,
            MSentre(),
            Vector3.zero,
            true,
            Program.ui_main_2d,
            true,
            new Vector3(((float)Screen.height) / 700f, ((float)Screen.height) / 700f, ((float)Screen.height) / 700f)
            );
        UISprite sp = UIHelper.getByName<UISprite>(currentMSwindow, "under");
        sp.height = 70 + options.Count * 48;
        List<GameObject> optionButtons = QuickTestTrace.Enabled ? new List<GameObject>() : null;
        for (int i = 0; i < options.Count; i++)
        {
            GameObject btn = create
           (
           Program.I().ES_Single_option,
           new Vector3(-2, sp.height / 2 - 59 - 48 * i, 0),
           Vector3.zero,
           false,
           sp.gameObject,
           false
           );
            UIHelper.trySetLableText(btn, "[u]"+options[i].hint);
            UIHelper.registEvent(btn, btn.name, ES_RMSpremono, options[i]);
            if (optionButtons != null)
            {
                optionButtons.Add(btn);
            }
        }
        UIHelper.InterGameObject(currentMSwindow);

        // 验收脚本要用真实光标把这些选项按下去，而弹窗落点随「当前在主菜单 / 对局 / 卡组编辑器」
        // 各不相同，猜不出来。所以把每个选项的落定坐标报出来。
        //
        // ⚠ 两个坑，都踩过：
        //   1) 必须报**碰撞盒中心**（Ocgcore.ButtonWorldCenter），不能报文字标签的位置 ——
        //      标签锚点不在碰撞盒正中，按它去点会落到背景板 under 上（实测 hover=pan/under），
        //      等于点空。这套换算与 [opt] 探针同源。
        //   2) `create` 返回这一刻，选项**还没拿到自己的位置**：四个选项会报出**同一个坐标**
        //      （实测全是 (960,493)）。所以必须延迟到布局应用之后再报，并且连报几批，
        //      脚本取最后一批。
        if (optionButtons != null)
        {
            Program.go(60, () => TraceMsOptions(hashCode, options, optionButtons));
            Program.go(200, () => TraceMsOptions(hashCode, options, optionButtons));
            Program.go(400, () => TraceMsOptions(hashCode, options, optionButtons));
            Program.go(700, () => TraceMsOptions(hashCode, options, optionButtons));
        }
    }

    /// <summary>弹窗选项的落定坐标（验收脚本按真光标点击用）。被 Program.go 延迟调用若干批。</summary>
    static void TraceMsOptions(string hashCode, List<messageSystemValue> options, List<GameObject> buttons)
    {
        if (!QuickTestTrace.Enabled || buttons == null)
        {
            return;
        }
        for (int i = 0; i < buttons.Count; i++)
        {
            if (buttons[i] == null || !buttons[i].activeInHierarchy)
            {
                return;                 // 弹窗已经关掉了，别再报
            }
        }
        for (int i = 0; i < buttons.Count && i < options.Count; i++)
        {
            Vector3 cp = Program.camera_main_2d.WorldToScreenPoint(
                Ocgcore.ButtonWorldCenter(buttons[i]));
            Vector3 lp = buttons[i].transform.localPosition;
            string box = buttons[i].GetComponent<BoxCollider>() != null ? "self"
                : (buttons[i].GetComponentInChildren<BoxCollider>() != null ? "child" : "none");
            QuickTestTrace.Log("msopt", hashCode + " i=" + i
                + " value=" + options[i].value
                + " hint=" + options[i].hint
                + " local=(" + Mathf.RoundToInt(lp.x) + "," + Mathf.RoundToInt(lp.y) + ")"
                + " box=" + box
                + " screen=(" + Mathf.RoundToInt(cp.x) + "," + Mathf.RoundToInt(Screen.height - cp.y) + ")");
        }
    }

    int RMSshow_multipleChoice_count = 0;

    List<messageSystemValue> RMSshow_multipleChoice_selected = new List<messageSystemValue>();

    public void RMSshow_multipleChoice(string hashCode, int selectCount, List<messageSystemValue> options)
    {
        RMSshow_multipleChoice_count = selectCount;
        RMSshow_multipleChoice_selected.Clear();
        RMSshow_clear();
        currentMShash = hashCode;
        currentMStype = messageSystemType.multipleChoice;
        currentMSwindow = create
            (
            Program.I().ES_Single_multiple_window,
            MSentre(),
            Vector3.zero,
            true,
            Program.ui_main_2d,
            true,
            new Vector3(((float)Screen.height) / 700f, ((float)Screen.height) / 700f, ((float)Screen.height) / 700f)
            );
        UISprite sp = UIHelper.getByName<UISprite>(currentMSwindow, "under");
        sp.height = 70 + UIHelper.get_zonghangshu(options.Count, 5) * 40;
        sp.width = 470;
        for (int i = 0; i < options.Count; i++)
        {
            Vector2 v = UIHelper.get_hang_lie(i, 5);
            float hang = v.x;
            float lie = v.y;
            GameObject btn = create
           (
           Program.I().ES_multiple_option,
           new Vector3(-162 + lie * 80, sp.height / 2 - 55 - 40 * hang, 0),
           Vector3.zero,
           false,
           sp.gameObject,
           false
           );
            UIHelper.trySetLableText(btn, "[u]" + options[i].hint);
            UIHelper.registEvent(btn, btn.name, ES_RMSpremono, options[i]);
        }
        UIHelper.InterGameObject(currentMSwindow);
    }

    public void RMSshow_position(string hashCode, int code, messageSystemValue atk, messageSystemValue def)
    {
        RMSshow_clear();
        currentMShash = hashCode;
        currentMStype = messageSystemType.position;
        currentMSwindow = create
            (
            Program.I().ES_position,
            MSentre(),
            Vector3.zero,
            true,
            Program.ui_main_2d,
            true,
            new Vector3(((float)Screen.height) / 700f, ((float)Screen.height) / 700f, ((float)Screen.height) / 700f)
            );
        UIHelper.InterGameObject(currentMSwindow);
        UIHelper.registEvent(currentMSwindow, "atk_", ES_RMSpremono, atk);
        UIHelper.registEvent(currentMSwindow, "def_", ES_RMSpremono, def);

        UITexture atkpic = UIHelper.getByName<UITexture>(currentMSwindow, "atkPic_");
        UIButton defbutton = UIHelper.getByName<UIButton>(currentMSwindow, "def_");
        if (Int32.Parse(atk.value) == (int)CardPosition.FaceUpDefence)
        {
            atkpic.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            defbutton.transform.localPosition = new Vector3(72.8f, 2f, 0f);
        }
        else
        {
            atkpic.transform.localRotation = Quaternion.Euler(0f, 0f, 0f);
            defbutton.transform.localPosition = new Vector3(62.8f, 0f, 0f);
        }

        cardPicLoader cardPicLoader_ = currentMSwindow.AddComponent<cardPicLoader>();
        cardPicLoader_.code = code;
        cardPicLoader_.uiTexture = atkpic;
        cardPicLoader_ = currentMSwindow.AddComponent<cardPicLoader>();
        cardPicLoader_.code = (Int32.Parse(def.value) == (int)CardPosition.FaceDownDefence) ? 0 : code;
        cardPicLoader_.uiTexture = UIHelper.getByName<UITexture>(currentMSwindow, "defPic_");
    }
    public void RMSshow_position3(string hashCode, int code)
    {
        RMSshow_clear();
        currentMShash = hashCode;
        currentMStype = messageSystemType.position;
        currentMSwindow = create
            (
            Program.I().ES_position3,
            MSentre(),
            Vector3.zero,
            true,
            Program.ui_main_2d,
            true,
            new Vector3(((float)Screen.height) / 700f, ((float)Screen.height) / 700f, ((float)Screen.height) / 700f)
            );
        UIHelper.InterGameObject(currentMSwindow);
        UIHelper.registEvent(currentMSwindow, "upAtk_", ES_RMSpremono, new messageSystemValue { value = "1", hint = "Face-Up Attack" });
        UIHelper.registEvent(currentMSwindow, "upDef_", ES_RMSpremono, new messageSystemValue { value = "4", hint = "Face-Up Defense" });
        UIHelper.registEvent(currentMSwindow, "downDef_", ES_RMSpremono, new messageSystemValue { value = "8", hint = "Face-Down Defense" });

        UITexture upatkpic = UIHelper.getByName<UITexture>(currentMSwindow, "upAtkPic_");
        UITexture updefpic = UIHelper.getByName<UITexture>(currentMSwindow, "upDefPic_");
        UITexture downdefpic = UIHelper.getByName<UITexture>(currentMSwindow, "downDefPic_");

        cardPicLoader cardPicLoader_ = currentMSwindow.AddComponent<cardPicLoader>();
        cardPicLoader_.code = code;
        cardPicLoader_.uiTexture = upatkpic;
        cardPicLoader_ = currentMSwindow.AddComponent<cardPicLoader>();
        cardPicLoader_.code = code;
        cardPicLoader_.uiTexture = updefpic;
        cardPicLoader_ = currentMSwindow.AddComponent<cardPicLoader>();
        cardPicLoader_.code = 0;
        cardPicLoader_.uiTexture = downdefpic;
    }

    public void RMSshow_tp(string hashCode, messageSystemValue jiandao, messageSystemValue shitou, messageSystemValue bu)
    {
        RMSshow_clear();
        currentMShash = hashCode;
        currentMStype = messageSystemType.tp;
        currentMSwindow = create
            (
            Program.I().ES_Tp,
            MSentre(),
            Vector3.zero,
            true,
            Program.ui_main_2d,
            true,
            new Vector3(((float)Screen.height) / 700f, ((float)Screen.height) / 700f, ((float)Screen.height) / 700f)
            );
        UIHelper.InterGameObject(currentMSwindow);
        UIHelper.registEvent(currentMSwindow, "jiandao_", ES_RMSpremono, jiandao);
        UIHelper.registEvent(currentMSwindow, "shitou_", ES_RMSpremono, shitou);
        UIHelper.registEvent(currentMSwindow, "bu_", ES_RMSpremono, bu);
    }

    public void RMSshow_input(string hashCode, string hint,string default_) 
    {
        RMSshow_clear();
        currentMShash = hashCode;
        currentMStype = messageSystemType.input;
        currentMSwindow = create
            (
            Program.I().ES_input,
            MSentre(),
            Vector3.zero,
            true,
            Program.ui_main_2d,
            true,
            new Vector3(((float)Screen.height) / 700f, ((float)Screen.height) / 700f, ((float)Screen.height) / 700f)
            );
        UIHelper.InterGameObject(currentMSwindow);
        UIHelper.trySetLableText(currentMSwindow, "hint_", hint);
        UIHelper.registEvent(currentMSwindow, "input_", ES_RMSpremono, null, "yes_");
        UIHelper.getByName<UIInput>(currentMSwindow, "input_").value = default_;
        Program.go(100, () => { UIHelper.getByName<UIInput>(currentMSwindow, "input_").isSelected = true; });
    }

    public void RMSshow_none(string hint)
    {
        Program.I().cardDescription.mLog(hint);
    }

    public void RMSshow_face(string hashCode, string name)  
    {
        RMSshow_clear();
        currentMShash = hashCode;
        currentMStype = messageSystemType.onlyYes;
        currentMSwindow = create
            (
            Program.I().ES_Face,
            MSentre(),
            Vector3.zero,
            true,
            Program.ui_main_2d,
            true,
            new Vector3(((float)Screen.height) / 700f, ((float)Screen.height) / 700f, ((float)Screen.height) / 700f)
            );
        UIHelper.InterGameObject(currentMSwindow);
        UIHelper.getByName<UITexture>(currentMSwindow, "face_").mainTexture = UIHelper.getFace(name);
        UIHelper.registEvent(currentMSwindow, "yes_", ES_RMSpremono, new messageSystemValue());
    }

    #endregion
}
