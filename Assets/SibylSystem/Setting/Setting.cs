using UnityEngine;
using System;
public class Setting : WindowServant2D
{
    private EventDelegate onChange;

    public LAZYsetting setting;

    public override void initialize()
    {
        gameObject = createWindow(this, Program.I().new_ui_setting);
        setting = gameObject.GetComponentInChildren<LAZYsetting>();
        UIHelper.registEvent(gameObject, "exit_", onClickExit);
        UIHelper.registEvent(gameObject, "screen_", resizeScreen);
        UIHelper.registEvent(gameObject, "full_", resizeScreen);
        UIHelper.registEvent(gameObject, "resize_", resizeScreen);
        UIHelper.getByName<UIToggle>(gameObject, "full_").value = Screen.fullScreen;
        UIHelper.getByName<UIToggle>(gameObject, "ignoreWatcher_").value = UIHelper.fromStringToBool(Config.Get("ignoreWatcher_", "0"));
        UIHelper.getByName<UIToggle>(gameObject, "ignoreOP_").value = UIHelper.fromStringToBool(Config.Get("ignoreOP_", "0"));
        UIHelper.getByName<UIToggle>(gameObject, "smartSelect_").value = UIHelper.fromStringToBool(Config.Get("smartSelect_", "1"));
        UIHelper.getByName<UIToggle>(gameObject, "autoChain_").value = UIHelper.fromStringToBool(Config.Get("autoChain_", "1"));
        UIHelper.getByName<UIToggle>(gameObject, "handPosition_").value = UIHelper.fromStringToBool(Config.Get("handPosition_", "1"));
        UIHelper.getByName<UIToggle>(gameObject, "handmPosition_").value = UIHelper.fromStringToBool(Config.Get("handmPosition_", "1"));
        UIHelper.getByName<UIToggle>(gameObject, "spyer_").value = UIHelper.fromStringToBool(Config.Get("spyer_", "1"));
        UIHelper.getByName<UIToggle>(gameObject, "resize_").value = UIHelper.fromStringToBool(Config.Get("resize_", "0"));
        UIHelper.getByName<UIToggle>(gameObject, "longField_").value = UIHelper.fromStringToBool(Config.Get("longField_", "0"));
        if (QualitySettings.GetQualityLevel()<3)
        {
            UIHelper.getByName<UIToggle>(gameObject, "high_").value = false;
        }
        else
        {
            UIHelper.getByName<UIToggle>(gameObject, "high_").value = true;
        }
        UIHelper.registEvent(gameObject, "ignoreWatcher_", save);
        UIHelper.registEvent(gameObject, "ignoreOP_", save);
        UIHelper.registEvent(gameObject, "smartSelect_", save);
        UIHelper.registEvent(gameObject, "autoChain_", save);
        UIHelper.registEvent(gameObject, "handPosition_", save);
        UIHelper.registEvent(gameObject, "handmPosition_", save);
        UIHelper.registEvent(gameObject, "spyer_", save);
        UIHelper.registEvent(gameObject, "high_", save);
        UIHelper.registEvent(gameObject, "longField_", onChangeLongField);
        UIHelper.registEvent(gameObject, "size_", onChangeSize);
        //UIHelper.registEvent(gameObject, "alpha_", onChangeAlpha);
        UIHelper.registEvent(gameObject, "vSize_", onChangeVsize);
        sliderSize = UIHelper.getByName<UISlider>(gameObject, "size_");
        //sliderAlpha = UIHelper.getByName<UISlider>(gameObject, "alpha_");
        sliderVsize = UIHelper.getByName<UISlider>(gameObject, "vSize_");
        Program.go(2000,readVales);
        var collection = gameObject.GetComponentsInChildren<UIToggle>();
        for (int i = 0; i < collection.Length; i++)
        {
            if (collection[i].name.Length > 0 && collection[i].name[0] == '*')
            {
                if (collection[i].name== "*mouseParticle" || collection[i].name == "*showOff" || collection[i].name == "*Efield" || collection[i].name == "*Ewin") 
                {
                    collection[i].value = UIHelper.fromStringToBool(Config.Get(collection[i].name, "1"));
                }
                else
                {
                    collection[i].value = UIHelper.fromStringToBool(Config.Get(collection[i].name, "0"));
                }
            }
        }
        setting.showoffATK.value = Config.Get("showoffATK","1800");
        setting.showoffStar.value = Config.Get("showoffStar", "5");
        UIHelper.registEvent(setting.showoffATK.gameObject, onchangeClose);
        UIHelper.registEvent(setting.showoffStar.gameObject, onchangeClose);
        UIHelper.registEvent(setting.mouseEffect.gameObject, onchangeMouse);
        UIHelper.registEvent(setting.closeUp.gameObject, onchangeCloseUp);
        UIHelper.registEvent(setting.cloud.gameObject, onchangeCloud);  
        UIHelper.registEvent(setting.Vpedium.gameObject, onCP);
        UIHelper.registEvent(setting.Vfield.gameObject, onCP);
        UIHelper.registEvent(setting.Vlink.gameObject, onCP);
        onchangeMouse();
        onchangeCloud();
        setScreenSizeValue();
        // ⛔ 「进入测试战斗时播放动画」（testHideAnim_）这一行**已按用户 2026-10-03 口径删除**：
        //   功能本身（点「测试」先播 1.25 秒收起动画）连同这个开关一起去掉了，
        //   现在点「测试」恒等于「直接藏界面就开局」。
        //   ⚠ 它原本是**第一个**追加行 ⇒ 删掉之后下面几行（俯视角 / 视角钮 / 卡名翻译 /
        //   盖放询问 / 召唤询问 / 极大一体化）整体**上移一格**，可见行数 −1、窗口高度 −24px
        //   （`SyncExtraRowVisibility` 会自己重排，行位与高度都按可见行数重算，不用手改）。
        // 「俯视角」排在两行 RD 专项**之前**（用户 2026-09-23 第 1 条）：
        // 它是个全局键（不分 RD/OCG），不该压在「召唤怪兽前询问 / 盖放怪兽前询问」下面。
        // ⛔ 顺序就是视觉顺序（AddExtraToggleRow 按 extraToggleRows 往下排 24px/行），
        //   换顺序会把那两行的 slot 从 2/3 变成 3/4（行下移一行，属预期）。
        CreateTopDownToggle();
        // 「显示视角切换按钮」紧跟在「俯视角（平面图）」**下面**（用户 2026-09-30 定）。
        // ⚠ 顺序就是视觉顺序：追加行按 extraToggleRows 往下排 24px/行，插在俯视角之后
        //   会把它下面那几行（召唤/盖放询问、极大怪兽一体化）整体下移一行 —— 属预期。
        CreateViewToggleButtonToggle();
        // 「卡名翻译」排在「显示视角切换按钮」下面（用户 2026-10-02 要求做成设置项）。
        // ⚠ 它是**选择行**不是开关行，所以不占 <c>extraRowToggles</c>（save/refresh 都会跳过它）。
        CreateTranslationPackRow();
        CreateAskMSetToggle();
        CreateAskSummonToggle();
        // 「极大怪兽一体化」放在**最后**：它是 RD 独占行，排末尾才不会改动上面几行的 slot
        //（插中间会把 askMset_/askSummon_ 的行位整体下移，属无谓扰动）。
        CreateRdMaxIntegratedToggle();
        // 建行时每行各自同步过一次；这里再走一遍是把「模式已知」与「窗口已就位」两件事
        // 收口到同一个出口（幂等：显隐/行位/高度三者的算式都不依赖调用次数）。
        SyncExtraRowVisibility();
        SyncRdBadges();
        // 选择行的文案要在**所有**开关行都排完之后刷：它读的是 CardNameTranslation，
        // 与行位无关，但放在最后能让日志里的行位数字是最终态。
        RefreshTranslationPackRow();
    }

    /// <summary>
    /// 「俯视角（平面图）」开关：把相机从 60° 倾斜态抬到 90° 正俯视，牌桌变成平面图。
    ///
    /// **全局一个键（`topDown_`，不分 RD/OCG）** —— 这是「看牌桌的视角偏好」，与规则差异无关。
    /// 盘面本来就是平铺的（卡 prefab 的 card 子节点烘焙 X+90），倾斜感全部来自相机 pitch，
    /// 所以这个开关只干两件事：① 相机 pitch 60→90；② 把「面向镜头」的标签/名牌/装饰
    /// 角度一起跟到 90（都在 `Program.tableau*` 里，见那几个成员的注释）。
    ///
    /// 改动即生效：写进 `Program.topDown` 后让 ocgcore 重摆一次（沿用 `onCP` 那条先例），
    /// 相机那一步由 `fixALLcamerasPreFrame` 的每帧补间带出动画。
    /// ⚠ 对局途中切换时，**场地的位置标签要下一局才换角度**（它们是 `GameField` 构造时建一次的，
    ///   `realize` 不会重建整个 GameField）；卡与名牌会立刻重摆。
    ///
    /// <para>⛔ <b>2026-09-30：这一行在设置页里不再显示</b>（用户要求「玩家不再能通过设置界面
    /// 设置是否进入俯视角，而是得靠按钮」）。行**仍然照建**，只是登记进
    /// <see cref="extraRowNeverShown"/> 被 <see cref="ExtraRowVisible"/> 判成不可见 ——
    /// 理由与后果见那个字段的注释（一句话：不建会让 <c>save()</c> 少写一个键、
    /// <c>SyncTopDownRow</c> 变成死代码）。</para>
    ///
    /// <para>视角本身的真源仍然是全局键 <c>topDown_</c>：战斗界面那颗眼形钮
    /// （<see cref="ViewToggleButton.ApplyTopDown"/>）直接写它并落盘，
    /// 所以「设置页看不见这一行」**不等于**「俯视角只能开机时那一面」——
    /// 对局中随时能切。</para>
    /// </summary>
    private void CreateTopDownToggle()
    {
        // ⛔ 顺序：先登记隐藏、再建行 —— 建行末尾会调 `SyncExtraRowVisibility`，
        //   那时判据必须已经就位，否则这一行会先被排进 slot、留下一格错位。
        extraRowNeverShown.Add("topDown_");
        AddExtraToggleRow("topDown_", "俯视角（平面图）", () => "topDown_", "0");
        UIHelper.registEvent(gameObject, "topDown_", onChangeTopDown);
    }

    /// <summary>「俯视角」开关的改动回调：写全局 + 重摆场景（相机带补间过去）。</summary>
    private void onChangeTopDown()
    {
        UIToggle t;
        if (!extraRowToggles.TryGetValue("topDown_", out t) || t == null)
        {
            return;
        }
        Program.topDown = t.value;
        // 「拉镜头」的三个状态属于**上一个视角的取景**：换视角一律 `ResetPan()`。
        // 不复位的话，关掉再打开会带着上次的平移量 ⇒ 看起来像「画面歪了」，而玩家没滚过滚轮。
        // （收摊那一处已由 `TableauLayout.NormalizePan` 每帧夹取覆盖，见 v3 注释。）
        TableauLayout.ResetPan();
        // 重摆场景放在**落盘之前**（用户 2026-09-23 第 1 条：「切视角时手牌强制挪位置」）：
        // `[tdmid]` 实测这条路是**同帧**生效的（手牌一次到位）。
        // 放在 `save()` 前是为了「重摆」不依赖磁盘写成功 —— 写配置万一抛异常（磁盘满/文件被占），
        // 视角也该照样切、手牌也该照样摆。
        onCP();
        // ⛔⛔ **切视角不许碰背景**（v3 返工，2026-09-28 用户报「俯视角下背景丢失了」）。
        //   v3 上一版在这里调 `backGroundPic.ReapplyForView()` 去换「安静背景」——
        //   那张图暗到 (17,19,24)，铺满屏看着就是背景没了；而且它**违反需求①**
        //   （「普通视角和俯视角必须没有任何互相影响」）：背景是 OCG/RD 的属性。
        //   ⇒ 背景只由 `GameModeManager.Changed` 驱动，视角这一路一行都不留。
        //   （`desk_flat.jpg` 还在工程里，但**没有任何代码引用它** —— 别再顺手接上。）
        QuickTestTrace.Log("view", "topDown=" + (Program.topDown ? 1 : 0)
            + " mode=" + Program.view
            + " stamp=" + Program.viewStamp
            + " tilt=" + Program.tableauTilt.ToString("F0")
            + " cover=" + TableauLayout.CoverZ.ToString("F2")
            + " handRow=" + TableauLayout.HandRowAbs.ToString("F2")
            + " outer=" + TableauLayout.HandOuterAbs.ToString("F2")
            + " margin=" + TableauLayout.ScreenMargin.ToString("F2")
            + " handScale=" + Program.tableauHandScale(1).ToString("F3"));
        // ⛔ 自己落盘：AddExtraToggleRow 里挂的那份 save 已被本行的 registEvent 顶掉
        //   （UIHelper.registEvent 对 UIToggle 走的是 onClick.Clear()+Add —— 是替换不是追加），
        //   不补这一步就只剩 saveWhenQuit 一条路 ⇒ 游戏被强杀/崩溃时这一项会丢。
        save();
    }

    /// <summary>
    /// 「卡名翻译」下拉框：**整局用哪一份译名**（需求 2026-10-02，第三轮改版）。
    ///
    /// 位置：**左侧那一列（分辨率 / 音量 / 字号）的最下沿再往下一格** —— 用户原话
    /// 「不放右边，改成在左边几个下拉框选项的下面」。
    ///
    /// 造法：克隆现成的 <c>screen_</c>（UIPopupList）—— 字体、图集、弹出层、
    /// 面板归属整套带过来。自己 new 的控件面板不认（「点一下就没了」那一族老坑）。
    ///
    /// ⛔ 它**不是**开关行：不登记进 extraToggleRowNames / extraRowToggles，
    ///   save() 与 config 落盘都不碰它 —— 真源是 translation/settings.txt 的 pack 键，
    ///   由 <see cref="YGOSharp.CardNameTranslation.SaveSettings"/> 自己写。
    /// ⛔ onSelectionChange 这版 NGUI 是 **LegacyEvent（直接赋值）**，不是 EventDelegate；
    ///   克隆体会把 screen_ 的旧回调带过来，必须整体覆盖成自己的。
    /// </summary>
    private void CreateTranslationPackRow()
    {
        UIPopupList src = UIHelper.getByName<UIPopupList>(gameObject, "screen_");
        if (src == null || UIHelper.getByName(gameObject, TranslationPackRow) != null)
        {
            return;
        }
        GameObject clone = UnityEngine.Object.Instantiate(src.gameObject);
        clone.name = TranslationPackRow;
        clone.transform.SetParent(src.transform.parent, false);
        clone.transform.localScale = src.transform.localScale;
        // ── 落点：**声明式锚点**，不算坐标 ────────────────────────────────────
        // 把本行的四条边锚到左列**最下面那一行**（star_：prefab 里 y=−7，六行里最低）：
        //   left/right 贴它的左右（同宽 240，与其余下拉框对齐）；
        //   top 落它下边再留 8px。
        // ⛔ 别再退回「算 localPosition」或「算屏幕矩形再写回 position」：那两版实测都是在
        //   **开窗补间**里量的（log 里同一行 `from` 的 y=1482、探针 2.5s 后量到 486，
        //   差 996 正好是整个窗口滑入的位移），量到的是移动靶 ⇒ 落点最后压在
        //   resize_/full_ 上。锚点是**每帧重算**的，补间与分辨率变化都不会让它跑偏。
        UIRect srcRect = src.GetComponent<UIRect>();
        // 基准＝**左列同 x 的最底一行**，动态扫出来，不写死名字。
        // ⛔ 写死过一次 `star_`，实测立错：左列真实行序（运行时 localPosition.y）
        //   vol_ 309.8 / size_ 285.8 / (alpha_ 缺) / screen_ 237.8 / atk_ 212.8 /
        //   star_ 187.8 / full_ ~157.8 / resize_ ~133.8 —— `star_` 下面还压着两行，
        //   锚在 star_ 上就正好落在 full_/resize_ 中间（探针 rect=(661,494)-(971,528)，
        //   overlapN=9）。行距也是 24 不是 prefab 看起来的 40。
        //   同名写死者还有 alpha_（它是失活的，UIHelper.getByName 根本查不到）。
        // ⇒ 只用「同一个父级 + x 相近」两件事做判据，谁能最低算谁。
        Transform refT = null;
        Transform srcT = src.transform;
        if (srcT.parent != null)
        {
            float colX = srcT.localPosition.x;
            float lowest = float.MaxValue;
            for (int i = 0; i < srcT.parent.childCount; i++)
            {
                Transform c = srcT.parent.GetChild(i);
                if (c == null || c.name == TranslationPackRow)
                {
                    continue;                       // 跳过本行自己（它此刻已挂在同一父级下）
                }
                if (Mathf.Abs(c.localPosition.x - colX) > 6f)
                {
                    continue;
                }
                if (c.localPosition.y < lowest)
                {
                    lowest = c.localPosition.y;
                    refT = c;
                }
            }
        }
        UIRect refRect = refT != null ? refT.GetComponent<UIRect>() : null;
        if (refRect == null)
        {
            refRect = srcRect;      // 兜底：扫不到就挂在 screen_ 自己下面一格
        }
        UIRect selfRect = clone.GetComponent<UIRect>();
        if (selfRect != null && refRect != null)
        {
            UIWidget selfW = selfRect as UIWidget;
            UIWidget refW = refRect as UIWidget;
            if (selfW == null)
            {
                selfW = clone.GetComponentInChildren<UIWidget>(true);
            }
            int h = selfW != null ? selfW.height : (refW != null ? refW.height : 28);
            const float gap = 8f;   // 行高 28 + 8 = 36 < 行距 40 ⇒ 不会压到更下面那一格
            selfRect.leftAnchor.target = refRect.transform;
            selfRect.leftAnchor.relative = 0f;
            selfRect.leftAnchor.absolute = 0;
            selfRect.rightAnchor.target = refRect.transform;
            selfRect.rightAnchor.relative = 1f;
            selfRect.rightAnchor.absolute = 0;
            selfRect.topAnchor.target = refRect.transform;
            selfRect.topAnchor.relative = 0f;               // 参照物的**下**边
            selfRect.topAnchor.absolute = -Mathf.RoundToInt(gap);
            selfRect.bottomAnchor.target = refRect.transform;
            selfRect.bottomAnchor.relative = 0f;            // 参照物的**下**边
            selfRect.bottomAnchor.absolute = -Mathf.RoundToInt(gap + h);
            selfRect.ResetAndUpdateAnchors();
            // ★ 落点定下之后，把 spyer_ 那一整列**整体下移**若干格，给本行腾出位置。
            //   为什么非移不可（2026-10-02 第五轮，实拍图 `_verify_viewbtn/10f_row_only2.png` 实证）：
            //   左列这 6 行（特效音量…特写星数）与中列（全屏游戏…召唤怪兽前询问）**是同一条纵向流
            //   交错排的** —— 屏幕上左列最后一行的下沿 536 与中列第一行（全屏游戏）的上沿 532 只差
            //   4px。本行有 34 高（下拉框比 24 的开关行高），不移的话一定压在中列第一行的勾选框和
            //   文字上（探针 overlapN=9）。用户要的是「在左边几个下拉框选项的下面」⇒ 就得真给它一格。
            PushMiddleColumnDown(clone.transform.localPosition.y - h * 0.5f,
                refT != null ? refT.localPosition.y : srcT.localPosition.y);
        }
        else
        {
            clone.transform.localPosition =
                src.transform.localPosition + new Vector3(0f, -40f, 0f);
        }

        UIPopupList popup = clone.GetComponent<UIPopupList>();
        if (popup == null)
        {
            // prefab 结构变了（screen_ 不是下拉框）：宁可没有也别摆一个坏控件。
            UnityEngine.Object.Destroy(clone);
            return;
        }
        translationPackPopup = popup;
        popup.onSelectionChange = onChangeTranslationPack;
        // 分辨率那串 "1280*720" 选项清掉，换成翻译表清单（按模式收窄，见 RebuildTranslationPackItems）。
        RebuildTranslationPackItems();
        // ★ 克隆体带过来的**行标题**要换成我们自己的。screen_ 的标题是「分辨率」——
        //   不改的话这一行会显示成「分辨率 / ygo原生翻译」两行字叠在一起，实拍图
        //   `_verify_viewbtn/10f_row_only2.png` 拍得清清楚楚。
        //   判据用「值标签不能动」：下拉框显示当前值的那一个标签，文本正好 == popup.value；
        //   其余非空标签一律改成行标题。⛔ 别按标签名写死（NGUI 的标签名随 prefab 版本变过）。
        string rawLabels = "";
        int titleChanged = 0;
        UILabel titleLabel = null;
        foreach (UILabel l in clone.GetComponentsInChildren<UILabel>(true))
        {
            if (l == null)
            {
                continue;
            }
            rawLabels += "[" + l.text + "]";
            if (string.IsNullOrEmpty(l.text) || l.text == popup.value)
            {
                continue;
            }
            l.text = PackRowTitle;
            titleChanged++;
            // RD 角标要贴**标题**那一格（值那一格的文案随当前表变、不能当锚）。
            // "哪一格是标题"只能按位置认：取最靠左的那个非值标签。
            if (titleLabel == null
                || l.transform.localPosition.x < titleLabel.transform.localPosition.x)
            {
                titleLabel = l;
            }
        }
        // ★ RD 角标（2026-10-02 用户要求「给 RD 做个特有化（标 rd 标）」）：
        //   与 askMset_/askSummon_ 共用同一套 CreateRowRdBadge + SyncRdBadges ⇒ 只在 RD 亮。
        //   语义差别要说清楚：那两行的角标是「本行读写的是 RD 那份存档」，本行没有分键存档，
        //   角标表示「本行在 RD 下是特化的（只剩原生一份、且不吃 OCG 那份选择）」。
        if (titleLabel != null)
        {
            // ⛔ 这一行走**锚定式**（第 4 个参数）：跟随式的落点会压在值文字上，见 CreateRowRdBadge。
            CreateRowRdBadge(TranslationPackRow, clone, titleLabel, selfRect);
        }

        hinter hint = clone.GetComponent<hinter>();
        if (hint == null)
        {
            hint = clone.AddComponent<hinter>();
        }
        hint.str = "整局用哪一份卡名翻译（悬停可看）";

        InstallModeHook();
        SyncExtraRowVisibility();
        RefreshTranslationPackRow();
        if (QuickTestTrace.Enabled)
        {
            QuickTestTrace.Log("setting", "packpopup items=" + popup.items.Count
                + " cur=" + YGOSharp.CardNameTranslation.CurrentKey
                + " rd=" + (GameModeManager.IsRD ? 1 : 0)
                + " badge=" + RdBadgeShown(TranslationPackRow)
                + " pos=" + clone.transform.localPosition.ToString("F1")
                + " title=" + titleChanged + "/" + rawLabels);
        }
    }

    /// <summary>这一行的 RD 角标此刻亮没亮（0 也包含"根本没建角标"）。</summary>
    private int RdBadgeShown(string rowName)
    {
        GameObject b;
        return extraRowRdBadges.TryGetValue(rowName, out b) && b != null && b.activeSelf ? 1 : 0;
    }

    /// <summary>
    /// 重建「卡名翻译」下拉框的**选项清单** —— 按当前模式收窄/放开。
    ///
    /// 判据源是 <see cref="YGOSharp.CardNameTranslation.AvailablePacks"/>（RD 只剩原生），
    /// 而**不是** Packs（磁盘上真装了什么，与模式无关）。
    /// 切模式时必须重来一遍：OCG 选的 nw 在切到 RD 后，下拉框里已经没有这一项了，
    /// 留着旧 items 就会出现「显示值是 nw、点开列表里没它」的自相矛盾画面。
    ///
    /// ⛔ <c>UIPopupList.value</c> 的 setter **无条件** <c>TriggerCallbacks()</c>
    ///   （实测：值没变也触发，见 UIPopupList.cs L240-249）⇒ 改 items/value 之前必须
    ///   先把 onSelectionChange 摘掉，否则开一次设置窗口就会白跑一遍全池重算。
    /// </summary>
    private void RebuildTranslationPackItems()
    {
        if (translationPackPopup == null)
        {
            return;
        }
        try
        {
            UIPopupList.LegacyEvent cb = translationPackPopup.onSelectionChange;
            translationPackPopup.onSelectionChange = null;
            translationPackPopup.Clear();
            foreach (YGOSharp.CardNameTranslation.Pack p in YGOSharp.CardNameTranslation.AvailablePacks)
            {
                if (p == null || string.IsNullOrEmpty(p.label))
                {
                    continue;
                }
                translationPackPopup.AddItem(p.label);
            }
            translationPackPopup.value = YGOSharp.CardNameTranslation.CurrentLabel;
            translationPackPopup.onSelectionChange = cb;
            if (QuickTestTrace.Enabled)
            {
                string list = "";
                for (int i = 0; i < translationPackPopup.items.Count; i++)
                {
                    list += (i > 0 ? "|" : "") + translationPackPopup.items[i];
                }
                QuickTestTrace.Log("setting", "packrowrd rd=" + (GameModeManager.IsRD ? 1 : 0)
                    + " items=" + translationPackPopup.items.Count
                    + " badge=" + RdBadgeShown(TranslationPackRow)
                    + " cur=" + YGOSharp.CardNameTranslation.CurrentLabel
                    + " stored=" + YGOSharp.CardNameTranslation.Packs.Count
                    + " list=[" + list + "]");
            }
        }
        catch (Exception e)
        {
            Program.DEBUGLOG(e);
        }
    }

    const string TranslationPackRow = "transPack_";
    private UIPopupList translationPackPopup;
    private UILabel translationPackLabel;
    /// <summary>旧版（开关行）留下的勾选框引用；下拉版没有它，恒为 null，探针在读。</summary>
    private UIWidget translationPackMark;
    /// <summary>还要继续摆几帧「卡名翻译」那一行（窗口刚开时锚点还没算好，多摆几次；落定即停）。</summary>
    private int packRowPlaceFrames = 0;
    /// <summary>下一次允许摆的最早帧号。
    /// ⛔ 必须隔几帧：NGUI 的 <c>worldCorners</c> 要等它自己那一轮更新才反映我们改过的位置
    ///   ⇒ 同一秒里连摆会把同一个位移**重复叠加**（实测 8 帧把这一行从屏下推到了窗口顶端）。</summary>
    private int packRowNextFrame = 0;
    /// <summary>左列那几行（下拉框/滑条）：新增行的落点基准就取它们里最靠下的一个。</summary>
    private static readonly string[] LeftColumnRows = { "vol_", "size_", "alpha_", "screen_", "atk_", "star_" };

    /// <summary>
    /// 左列的最下沿再往下一格（行距 40px）。
    /// ⛔ **不能写死列成员**：第一版列了 screen_/vol_/size_/vSize_/alpha_ 五个，
    ///   但左列 x=−128 上其实还有 atk_(y33) 和 star_(y−7) 两行 —— 漏了它们，
    ///   算出的「最低行」是 screen_(73)，落点 73−40=33 正好砸在 atk_ 上（用户报「重合了」）。
    ///   所以改成**动态扫同列**：取 screen_ 同一父级下 x 相近（±5px）的全部兄弟，
    ///   谁最低算谁，再往下退一格。
    /// </summary>
    private Vector3 LeftColumnSlotBelow(Vector3 fallback)
    {
        Transform anchor = UIHelper.getByName<Transform>(gameObject, "screen_");
        if (anchor == null)
        {
            return fallback + new Vector3(0f, -40f, 0f);
        }
        float colX = anchor.localPosition.x;
        float minY = anchor.localPosition.y;
        Transform parent = anchor.parent;
        if (parent != null)
        {
            for (int i = 0; i < parent.childCount; i++)
            {
                Transform c = parent.GetChild(i);
                if (c == anchor || c == null)
                {
                    continue;
                }
                if (Mathf.Abs(c.localPosition.x - colX) > 5f)
                {
                    continue;
                }
                if (c.localPosition.y < minY)
                {
                    minY = c.localPosition.y;
                }
            }
        }
        return new Vector3(fallback.x, minY - 40f, fallback.z);
    }

    /// <summary>「卡名翻译」下拉框那一行的**行标题**。</summary>
    const string PackRowTitle = "卡名翻译";
    /// <summary>「卡名翻译」那一行为它自己**腾出来的窗口高度**（折成的 24px 格数）。
    /// 它把 spyer_ 那一整列整体下移了若干格 ⇒ 窗口必须跟着长同样多，
    /// 否则最下面那几行会被窗口底边切掉（见 <see cref="GrowWindowForExtraRows"/>）。</summary>
    private int packRowReserveRows = 0;

    /// <summary>
    /// 把「spyer_ 那一列」（屏幕上是「全屏游戏 … 召唤怪兽前询问」那一大块）**整体下移**若干格，
    /// 给「卡名翻译」这一行腾出一个不与任何控件相交的位置。
    ///
    /// <para>为什么必须移（2026-10-02 第五轮）：设置窗里并没有「左列 / 中列」两个独立容器 ——
    /// <c>content</c> 下的所有行是**同一条纵向流**，只是横向分成三组 x（−131 的左列下拉、
    /// −258 的开关列、+12 的右侧特效列）。左列只占最上面 6 行，之后这条流就只剩开关列了：
    /// 屏幕上左列最后一行（特写星数）下沿 <b>536</b>、开关列第一行（全屏游戏）上沿 <b>532</b>，
    /// 只有 4px 缝。本行是下拉框、有 34 高（开关行只有 24）⇒ 落在那一格必然压住「全屏游戏」
    /// 的勾选框与文字（探针实测 <c>overlapN=9</c>）。</para>
    ///
    /// <para>移多少是**算出来的**，不写死：取开关列里最靠上的那一行的上边缘，
    /// 要求它下移到「本行下沿 − margin」以下，再向上取整到整格（24）。</para>
    ///
    /// <para>⛔ 追加行（<c>testHideAnim_</c>/<c>viewToggleBtn_</c>/<c>askMset_</c>…）的行位是
    /// <c>SyncExtraRowVisibility</c> 按「<c>extraRowSrcLocalPos</c> − 24×slot」每帧/每次同步**重算**的，
    /// 而 <c>extraRowSrcLocalPos</c> 是建第一行时从 spyer_ 记下来的一次性快照 ⇒
    /// 光移 spyer_ 不动这个快照，整块会被拉回原位（行位与窗口高度就对不上了）。两处都要移。</para>
    /// </summary>
    private void PushMiddleColumnDown(float ourLocalBottom, float insertBelowY)
    {
        try
        {
            Transform colT = UIHelper.getByName<Transform>(gameObject, "spyer_");
            if (colT == null || colT.parent == null)
            {
                return;
            }
            Transform par = colT.parent;
            float colX = colT.localPosition.x;
            const float halfRow = 12f;      // 开关行 24 高（屏幕实测：full_ 508..532 等）
            const float pitch = 24f;
            const float margin = 6f;        // 本行下沿与下一行上沿之间至少留这么多
            // 只算**插入点之下**的那些行（插入点上方没有别的行，但别把将来的改动算漏）。
            float topEdge = float.MinValue;
            for (int i = 0; i < par.childCount; i++)
            {
                Transform c = par.GetChild(i);
                if (c == null || c.name == TranslationPackRow)
                {
                    continue;
                }
                if (Mathf.Abs(c.localPosition.x - colX) > 6f)
                {
                    continue;
                }
                if (c.localPosition.y >= insertBelowY)
                {
                    continue;
                }
                float t = c.localPosition.y + halfRow;
                if (t > topEdge)
                {
                    topEdge = t;
                }
            }
            if (topEdge == float.MinValue)
            {
                return;
            }
            float need = topEdge + margin - ourLocalBottom;
            if (need <= 0f)
            {
                return;                     // 本来就有空位，别动
            }
            int grids = Mathf.CeilToInt(need / pitch);
            float dy = -pitch * grids;
            for (int i = 0; i < par.childCount; i++)
            {
                Transform c = par.GetChild(i);
                if (c == null || c.name == TranslationPackRow)
                {
                    continue;
                }
                if (Mathf.Abs(c.localPosition.x - colX) > 6f)
                {
                    continue;
                }
                Vector3 lp = c.localPosition;
                lp.y += dy;
                c.localPosition = lp;
            }
            if (extraRowSrcPosSaved)
            {
                extraRowSrcLocalPos.y += dy;
            }
            packRowReserveRows = grids;
            if (QuickTestTrace.Enabled)
            {
                QuickTestTrace.Log("setting", "packrow pushcol dy=" + dy.ToString("F0")
                    + " grids=" + grids
                    + " topEdge=" + topEdge.ToString("F1")
                    + " ourBottom=" + ourLocalBottom.ToString("F1")
                    + " base=" + (extraRowSrcPosSaved ? extraRowSrcLocalPos.y.ToString("F1") : "n/a"));
            }
        }
        catch (Exception e)
        {
            Program.DEBUGLOG(e);
        }
    }

    /// <summary>验收用：把「卡名翻译」这一行的真实状态摊到日志里（只在 qt_debug.on 下被调）。</summary>
    public void ProbeTranslationPackRow()
    {
        try
        {
            RefreshTranslationPackRow();
            GameObject go = FindExtraRow(TranslationPackRow);
            if (go == null)
            {
                // ⛔ 这一行是 screen_ 的克隆、**不登记进 extraRowObjects**（它是选择行不是开关行），
                //   所以 FindExtraRow 找不到它 —— 探针不能因此判"行不存在"。
                go = UIHelper.getByName(gameObject, TranslationPackRow);
            }
            UILabel lab = go != null ? go.GetComponentInChildren<UILabel>() : null;
            UIButton btn = go != null ? go.GetComponent<UIButton>() : null;
            UIToggle tog = go != null ? go.GetComponent<UIToggle>() : null;
            // 勾选框（activeSprite）应当是关掉的 —— 那是"这一行是个开关"的唯一视觉线索。
            // ⚠ 不能靠 go.GetComponent<UIToggle>() 去查：组件已经被 Destroy 了（要靠按钮行为），
            //   所以这里读建行时存下来的那个 widget 引用。
            bool markHidden = translationPackMark != null && !translationPackMark.enabled;
            // ── 屏幕矩形求交（判"有没有被别的控件压住"的唯一硬判据）──────────────
            // ⛔ 上一版只报本行的屏幕**中心点**（用户报「重合了」时，日志里那个点看着没有问题）。
            //   重合是**矩形**之间的关系，点对不上就没意义。所以这里把本行的矩形量出来，
            //   再和窗口里所有其它可见控件逐对求交，把压住它的名字与矩形一起打出来。
            float rx0 = 0f, ry0 = 0f, rx1 = 0f, ry1 = 0f;
            bool haveRect = false;
            if (go != null)
            {
                foreach (UIWidget w in go.GetComponentsInChildren<UIWidget>(true))
                {
                    if (w == null || !w.gameObject.activeInHierarchy)
                    {
                        continue;
                    }
                    float ax0, ay0, ax1, ay1;
                    if (!WidgetScreenRect(w, out ax0, out ay0, out ax1, out ay1))
                    {
                        continue;
                    }
                    if (!haveRect)
                    {
                        rx0 = ax0; ry0 = ay0; rx1 = ax1; ry1 = ay1;
                        haveRect = true;
                    }
                    else
                    {
                        rx0 = Mathf.Min(rx0, ax0); ry0 = Mathf.Min(ry0, ay0);
                        rx1 = Mathf.Max(rx1, ax1); ry1 = Mathf.Max(ry1, ay1);
                    }
                }
            }
            string sp = haveRect
                ? "(" + Mathf.RoundToInt(rx0) + "," + Mathf.RoundToInt(ry0)
                  + ")-(" + Mathf.RoundToInt(rx1) + "," + Mathf.RoundToInt(ry1) + ")"
                : "n/a";
            // 逐对求交：跳过本行自己的子控件；把矩形面积 > 半屏的（背景图）单独标出来，
            // 免得"整窗底图"天天报成重合。
            string hits = "";
            int hitN = 0;
            int bgN = 0;
            if (haveRect && go != null)
            {
                float screenArea = Mathf.Max(1f, Screen.width * Screen.height);
                foreach (UIWidget w in gameObject.GetComponentsInChildren<UIWidget>(true))
                {
                    if (w == null || !w.gameObject.activeInHierarchy)
                    {
                        continue;
                    }
                    if (w.transform.IsChildOf(go.transform))
                    {
                        continue;
                    }
                    float ax0, ay0, ax1, ay1;
                    if (!WidgetScreenRect(w, out ax0, out ay0, out ax1, out ay1))
                    {
                        continue;
                    }
                    float ix0 = Mathf.Max(rx0, ax0), iy0 = Mathf.Max(ry0, ay0);
                    float ix1 = Mathf.Min(rx1, ax1), iy1 = Mathf.Min(ry1, ay1);
                    if (ix1 - ix0 <= 0.5f || iy1 - iy0 <= 0.5f)
                    {
                        continue;
                    }
                    if ((ax1 - ax0) * (ay1 - ay0) > screenArea * 0.5f)
                    {
                        bgN++;
                        continue;
                    }
                    hitN++;
                    if (hitN <= 6)
                    {
                        hits += " [" + w.name + "=(" + Mathf.RoundToInt(ax0) + "," + Mathf.RoundToInt(ay0)
                            + ")-(" + Mathf.RoundToInt(ax1) + "," + Mathf.RoundToInt(ay1) + ")]";
                    }
                }
            }
            // ── 几何原始值：光有屏幕矩形判不出「为什么落在那里」────────────────────
            // 本行已改成**锚点驱动**（挂在 star_ 下面），所以要一眼能看出"锚对没对"：
            // 把左列六行的 localPosition/父级、本行自己的 localPosition/父级/顶部锚点目标/
            // 自己的 width×height 全摊出来。锚点为 none 或尺寸为 0 ⇒ 落点一定不对。
            // ⛔ 别再按名字列左列（写死过一次 vol_/size_/alpha_/screen_/atk_/star_，
            //   漏了 full_/resize_ 两行、还把失活的 alpha_ 当成"存在"）。直接把本行父级下
            //   **同 x 的全部兄弟**按原样摊开，一个不漏，肉眼即可核对锚点基准选对没。
            string geo = "";
            if (go != null && go.transform.parent != null)
            {
                Transform par = go.transform.parent;
                float selfX = go.transform.localPosition.x;
                for (int i = 0; i < par.childCount; i++)
                {
                    Transform c = par.GetChild(i);
                    if (c == null || Mathf.Abs(c.localPosition.x - selfX) > 6f)
                    {
                        continue;                     // 只看同一列
                    }
                    geo += " " + c.name + "=" + c.localPosition.y.ToString("F1")
                        + (c.gameObject.activeSelf ? "" : "(藏)");
                }
            }
            if (go != null)
            {
                UIRect selfR = go.GetComponent<UIRect>();
                UIWidget selfW = selfR as UIWidget;
                geo += " self=" + go.transform.localPosition.ToString("F1")
                    + "@" + (go.transform.parent != null ? go.transform.parent.name : "-")
                    + " refTop=" + (selfR != null && selfR.topAnchor.target != null
                        ? selfR.topAnchor.target.name + "(" + selfR.topAnchor.absolute + ")" : "none")
                    + " refBottom=" + (selfR != null && selfR.bottomAnchor.target != null
                        ? selfR.bottomAnchor.target.name + "(" + selfR.bottomAnchor.absolute + ")" : "none")
                    + " size=" + (selfW != null ? selfW.width + "x" + selfW.height : "?")
                    + " selfActive=" + go.activeInHierarchy;
            }
            // 行内每个 UILabel 的文案与 x：RD 角标是贴到**最靠左的那个非值标签**上的
            // （值那一格的文案随当前表变、不能当锚），"哪一格是标题"只能靠位置认。
            // 摊开一眼就能核对角标贴对没贴对。
            string labels = "";
            if (go != null)
            {
                foreach (UILabel l in go.GetComponentsInChildren<UILabel>(true))
                {
                    if (l == null)
                    {
                        continue;
                    }
                    // 连**屏幕矩形**一起报：角标该贴哪一格，取决于标签的左右边界，
                    // 只报 localPosition 看不出"标题右沿是不是正好贴着值那一格的左沿"
                    // （实测就是贴着 ⇒ 角标落进值文字里）。
                    float lx0, ly0, lx1, ly1;
                    labels += "[" + l.text + "@" + l.transform.localPosition.x.ToString("F0")
                        + (WidgetScreenRect(l, out lx0, out ly0, out lx1, out ly1)
                            ? ("[x" + Mathf.RoundToInt(lx0) + ".." + Mathf.RoundToInt(lx1) + "]")
                            : "[-]")
                        + (l.gameObject.activeInHierarchy ? "" : "(藏)") + "]";
                }
            }
            // ── 结构快照：本行**父级的全部子节点**＋几个关键行的父链 ────────────────
            // 动机（2026-10-02 第五轮）：锚点基准是「同父级 + localPosition.x 相近」，
            //   实测本行屏幕矩形 494..528 与 full_(508..532)/resize_(484..508) 相交（overlapN=9），
            //   而这两行的 localPosition.x 与本行差 >6px ⇒ 扫描根本没看见它们。
            //   ⇒ 判「屏幕上谁最低」不能只看 localPosition，必须把 localPosition 与屏幕矩形并排摊开。
            string dump = "";
            if (go != null && go.transform.parent != null)
            {
                Transform par = go.transform.parent;
                dump += " par=" + par.name + " n=" + par.childCount;
                for (int i = 0; i < par.childCount; i++)
                {
                    Transform c = par.GetChild(i);
                    if (c == null)
                    {
                        continue;
                    }
                    float bx0, by0, bx1, by1;
                    bool ok = RowScreenRect(c.gameObject, out bx0, out by0, out bx1, out by1);
                    // 末尾那个 `A` = 这一行自己挂没挂锚点。挂了的行**每帧会被锚点拉回去**，
                    // 那 PushMiddleColumnDown 写下的 localPosition 就会被覆盖掉 —— 必须能一眼看出来。
                    UIRect cr = c.GetComponent<UIRect>();
                    bool anch = cr != null && (cr.leftAnchor.target != null
                        || cr.rightAnchor.target != null || cr.topAnchor.target != null
                        || cr.bottomAnchor.target != null);
                    dump += " " + c.name + "@" + c.localPosition.x.ToString("F0") + ","
                        + c.localPosition.y.ToString("F0")
                        + (c.gameObject.activeSelf ? "" : "(藏)")
                        + (anch ? "A" : "")
                        + (ok ? ("[" + Mathf.RoundToInt(bx0) + ".." + Mathf.RoundToInt(bx1) + ","
                            + Mathf.RoundToInt(by0) + ".." + Mathf.RoundToInt(by1) + "]") : "[-]");
                }
            }
            foreach (string nm in new[] { "full_", "resize_", "star_", "screen_", "atk_" })
            {
                Transform t = null;
                foreach (Transform cand in gameObject.GetComponentsInChildren<Transform>(true))
                {
                    if (cand.name == nm)
                    {
                        t = cand;
                        break;
                    }
                }
                dump += " |" + nm + "=" + (t == null
                    ? "missing"
                    : ("@" + (t.parent != null ? t.parent.name : "-")
                       + " lp=" + t.localPosition.ToString("F0")
                       + " act=" + (t.gameObject.activeSelf ? 1 : 0)));
            }
            QuickTestTrace.Log("setting", "packrowdump" + dump);
            QuickTestTrace.Log("setting", "packrowprobe row=" + TranslationPackRow
                + " exists=" + (go != null)
                + " label=[" + (lab != null ? lab.text : "") + "]"
                + " btn=" + (btn != null) + " toggleLeft=" + (tog != null)
                + " markFound=" + (translationPackMark != null) + " markHidden=" + markHidden
                + " inPanel=" + (go != null && UIHelper.getByName(gameObject, TranslationPackRow) != null)
                + " rect=" + sp
                + " overlapN=" + hitN + " bgSkip=" + bgN + hits
                + " screenH=" + Screen.height + " screenW=" + Screen.width
                + " cur=" + YGOSharp.CardNameTranslation.CurrentKey
                + " rd=" + (GameModeManager.IsRD ? 1 : 0)
                + " items=" + (translationPackPopup != null ? translationPackPopup.items.Count : -1)
                + " badge=" + RdBadgeShown(TranslationPackRow)
                + " labels=" + labels
                + " geo=" + geo);
        }
        catch (Exception e)
        {
            Program.DEBUGLOG(e);
        }
    }

    /// <summary>
    /// 把「卡名翻译」这一行摆到**左列那几行**（vol_ / size_ / alpha_ / screen_ / atk_ / star_）
    /// 的最下面一行。
    ///
    /// ⛔ 为什么不按 localPosition 算：建行时那套算式实测给出 y=147.8，落进屏幕后正好压在
    ///   resize_/full_ 上（用户 2026-10-02 报「翻译选项和其他下拉框重合了」）。
    ///   运行时这些控件的 localPosition 与 prefab 里的值不是一回事 ⇒ **猜不得**。
    ///   （同一列在 prefab 里是 x≈−127.7、行距 40、最下是 star_ 的 y=−7，但运行时全变了。）
    ///
    /// ⇒ 改成**屏幕空间量矩形**：
    ///   ① 本行以它自己的**标签**中心为列基准（不是整块并集 —— 下拉框子件里有比标签宽的东西，
    ///      拿并集当列基准会把旁边那一列（追加开关行，中心差 ≈127px）也算成同列）；
    ///   ② 窗口里所有可见控件，中心落在列基准 ±45px、非背景（面积 > 1/5 屏）、非整窗件
    ///      （比本行还宽）⇒ 算同列候选；
    ///   ③ 取候选里**最靠下**的那个当基准，本行上边落到它下边再往下 16px（行距 40、行高 24）。
    ///
    /// 窗口滑入补间不影响结果：两个矩形是同一帧量的，位移差与动画无关。
    /// 返回 true = 这一帧量到了有效矩形（调用方据此递减重试计数）。
    /// </summary>
    private bool PlaceTranslationPackRow()
    {
        try
        {
            GameObject go = UIHelper.getByName(gameObject, TranslationPackRow);
            Camera cam = Program.camera_main_2d;
            if (go == null || cam == null || !go.activeInHierarchy)
            {
                return false;
            }
            float x0, y0, x1, y1;
            if (!RowScreenRect(go, out x0, out y0, out x1, out y1))
            {
                return false;
            }
            // 列基准＝本行标签的中心 x（退化时才用整块并集的中心）。
            UILabel lab = go.GetComponentInChildren<UILabel>(true);
            float colCx = (x0 + x1) * 0.5f;
            if (lab != null && lab.gameObject.activeInHierarchy)
            {
                Vector3[] lc = lab.worldCorners;
                Vector3 a = cam.WorldToScreenPoint(lc[0]);
                Vector3 b = cam.WorldToScreenPoint(lc[2]);
                colCx = (a.x + b.x) * 0.5f;
            }
            // ── 基准＝左列那几行里最靠下的一个 ─────────────────────────────
            // ⛔ 不能用「同列里最低的任意控件」：追加开关行（testHideAnim_/askSummon_ …）
            //   与左列**中心对齐**（cx≈857），会被算成同列，于是这一行被摆到整个列表最底下
            //   （实测 basis=askSummon_、basisBottom=1157 在屏外）。
            //   需求是「在左边几个下拉框选项的下面」⇒ 基准只认那六行。
            float bestBottom = float.MaxValue;
            string bestName = "";
            foreach (string nm in LeftColumnRows)
            {
                GameObject row = UIHelper.getByName(gameObject, nm);
                float ax0, ay0, ax1, ay1;
                if (row == null || !row.activeInHierarchy
                    || !RowScreenRect(row, out ax0, out ay0, out ax1, out ay1))
                {
                    continue;
                }
                if (ay0 < bestBottom)
                {
                    bestBottom = ay0;
                    bestName = nm;
                }
            }
            if (bestName == "")
            {
                // 兜底：prefab 换了、那六行一个都没找到 —— 退回几何法（同列中心对齐、
                // 非背景、且**不是追加开关行**）。
                float screenArea = Mathf.Max(1f, Screen.width * Screen.height);
                float selfW = x1 - x0;
                foreach (UIWidget cand in gameObject.GetComponentsInChildren<UIWidget>(true))
                {
                    if (cand == null || !cand.gameObject.activeInHierarchy)
                    {
                        continue;
                    }
                    if (cand.transform.IsChildOf(go.transform))
                    {
                        continue;
                    }
                    if (extraRowObjects.ContainsKey(cand.name))
                    {
                        continue;
                    }
                    float ax0, ay0, ax1, ay1;
                    if (!WidgetScreenRect(cand, out ax0, out ay0, out ax1, out ay1))
                    {
                        continue;
                    }
                    if ((ax1 - ax0) * (ay1 - ay0) > screenArea * 0.2f || (ax1 - ax0) > selfW * 1.2f)
                    {
                        continue;
                    }
                    if (Mathf.Abs((ax0 + ax1) * 0.5f - colCx) > 45f)
                    {
                        continue;
                    }
                    if (ay0 < bestBottom)
                    {
                        bestBottom = ay0;
                        bestName = cand.name + "(fallback)";
                    }
                }
            }
            if (bestName == "")
            {
                return false;
            }
            // 目标：本行**上边** = 基准下边 − 16。
            float dy = (bestBottom - 16f) - y1;
            if (Mathf.Abs(dy) < 1f)
            {
                return true;
            }
            Vector3 scr = cam.WorldToScreenPoint(go.transform.position);
            scr.y += dy;
            go.transform.position = cam.ScreenToWorldPoint(scr);
            if (QuickTestTrace.Enabled)
            {
                QuickTestTrace.Log("setting", "packrow placed basis=" + bestName
                    + " basisBottom=" + Mathf.RoundToInt(bestBottom)
                    + " dy=" + dy.ToString("F1")
                    + " from=(" + Mathf.RoundToInt(x0) + "," + Mathf.RoundToInt(y0)
                    + ")-(" + Mathf.RoundToInt(x1) + "," + Mathf.RoundToInt(y1) + ")"
                    + " colCx=" + Mathf.RoundToInt(colCx));
            }
            return true;
        }
        catch (Exception e)
        {
            Program.DEBUGLOG(e);
            return false;
        }
    }

    /// <summary>本行的屏幕矩形（自身所有可见子控件的并集）。</summary>
    private static bool RowScreenRect(GameObject go, out float x0, out float y0, out float x1, out float y1)
    {
        x0 = y0 = x1 = y1 = 0f;
        bool got = false;
        foreach (UIWidget w in go.GetComponentsInChildren<UIWidget>(true))
        {
            if (w == null || !w.gameObject.activeInHierarchy)
            {
                continue;
            }
            float ax0, ay0, ax1, ay1;
            if (!WidgetScreenRect(w, out ax0, out ay0, out ax1, out ay1))
            {
                continue;
            }
            if (!got)
            {
                x0 = ax0; y0 = ay0; x1 = ax1; y1 = ay1; got = true;
            }
            else
            {
                x0 = Mathf.Min(x0, ax0); y0 = Mathf.Min(y0, ay0);
                x1 = Mathf.Max(x1, ax1); y1 = Mathf.Max(y1, ay1);
            }
        }
        return got;
    }

    /// <summary>
    /// 控件的**屏幕矩形**（左、下、右、上）。
    /// ⛔ 不能走 <c>Renderer.bounds</c>：NGUI 的控件没有 Renderer，那儿恒为 null；
    ///   要用 <see cref="UIWidget.worldCorners"/>（按 width/height 算出来的四个角）。
    /// </summary>
    private static bool WidgetScreenRect(UIWidget w, out float x0, out float y0, out float x1, out float y1)
    {
        x0 = y0 = x1 = y1 = 0f;
        Camera cam = Program.camera_main_2d;
        if (w == null || cam == null)
        {
            return false;
        }
        Vector3[] c = w.worldCorners;
        Vector3 p = cam.WorldToScreenPoint(c[0]);
        x0 = x1 = p.x;
        y0 = y1 = p.y;
        for (int i = 1; i < c.Length; i++)
        {
            Vector3 q = cam.WorldToScreenPoint(c[i]);
            if (q.x < x0) { x0 = q.x; }
            if (q.x > x1) { x1 = q.x; }
            if (q.y < y0) { y0 = q.y; }
            if (q.y > y1) { y1 = q.y; }
        }
        return (x1 - x0) > 0.5f && (y1 - y0) > 0.5f;
    }

    /// <summary>
    /// 把下拉框的显示值刷成当前选中的表（在「译」菜单里换了表、设置页还开着时跟上）。
    /// ⛔ 相同值时不写回 —— UIPopupList 的 value setter 一改值就触发 onSelectionChange，
    ///   不挡住的话开一次设置窗口就会白跑一遍全池重算。
    /// </summary>
    private void RefreshTranslationPackRow()
    {
        // 落点自 2026-10-02 第四轮起改由 CreateTranslationPackRow 里的**锚点**决定，
        // 这套"开窗后连摆几帧"的命令式补救全部作废（置 0 = 永不启动）。
        // 留着方法本体是因为探针还要读几何原始值，且万一将来 prefab 换成锚点不可用的结构，
        // 这里改回 8 就能把旧的屏幕矩形摆法接回来。
        packRowPlaceFrames = 0;
        string cur = YGOSharp.CardNameTranslation.CurrentLabel;
        if (translationPackPopup == null)
        {
            GameObject go = FindExtraRow(TranslationPackRow);
            translationPackPopup = go != null ? go.GetComponent<UIPopupList>() : null;
        }
        if (translationPackPopup != null && translationPackPopup.value != cur)
        {
            translationPackPopup.value = cur;
        }
    }

    /// <summary>
    /// 下拉框选了哪份翻译表：按显示名反查 pack，交给 <see cref="NameTranslationUI.ApplyGlobalPackFromSetting"/>
    /// （与「译」菜单里选全局翻译**同一条路**：缺数据先问下载、换完重算全池）。
    /// </summary>
    private void onChangeTranslationPack(string label)
    {
        // 按显示名反查 pack：走 AvailablePacks（下拉里现在就只摆了这些，见 RebuildTranslationPackItems）。
        foreach (YGOSharp.CardNameTranslation.Pack p in YGOSharp.CardNameTranslation.AvailablePacks)
        {
            if (p != null && p.label == label)
            {
                string key = p.key;
                QuickTestTrace.Log("setting", "packpopup select key=" + key);
                NameTranslationUI.ApplyGlobalPackFromSetting(key);
                return;
            }
        }
        QuickTestTrace.Log("setting", "packpopup select UNKNOWN label=" + (label ?? ""));
    }

    /// <summary>
    /// 「显示视角切换按钮」开关：**默认开**（缺省 <c>"1"</c>，见
    /// <see cref="ViewToggleButton.SettingVisible"/>）。关掉之后战斗里那颗 56×56 眼形钮就不出现。
    ///
    /// <para>⚠ 2026-09-30：**「俯视角（平面图）」那一行已被隐藏**
    /// （见 <see cref="extraRowNeverShown"/>），所以这一行现在是**玩家唯一的偏好入口**。
    /// 后果要说清楚：<b>把这一项关掉之后，玩家就再也进不了俯视角了</b>
    /// ——设置页看不见俯视角开关、战斗里又没有钮，两个出口同时没了。
    /// 用户明确要求「默认开启」，此处照办；若要彻底堵死这个死角，
    /// 把这一行也登记进 <c>extraRowNeverShown</c> 即可（钮恒在、偏好项不再暴露）。</para>
    ///
    /// 键是 <see cref="ViewToggleButton.SettingKey"/>（<c>viewToggleBtn_</c>），
    /// 值走 <see cref="ViewToggleButton.SettingVisible"/>（static，不加实例字段）。
    /// OCG / RD 共用同一行 —— 它是「界面偏好」，与卡池无关（与 <c>topDown_</c> 同理）。
    /// </summary>
    private void CreateViewToggleButtonToggle()
    {
        AddExtraToggleRow(ViewToggleButton.SettingKey, "显示视角切换按钮",
            () => ViewToggleButton.SettingKey, "1");
        UIHelper.registEvent(gameObject, ViewToggleButton.SettingKey, onChangeViewToggleButton);
    }

    /// <summary>「显示视角切换按钮」的改动回调：写全局 + 自己落盘。</summary>
    private void onChangeViewToggleButton()
    {
        UIToggle t;
        if (!extraRowToggles.TryGetValue(ViewToggleButton.SettingKey, out t) || t == null)
        {
            return;
        }
        ViewToggleButton.SettingVisible = t.value;
        // ⛔ 自己落盘：AddExtraToggleRow 挂的那份 save 已被本行的 registEvent 顶掉
        //   （UIHelper.registEvent 对 UIToggle 走的是 onClick.Clear()+Add —— 替换不是追加），
        //   不补这一步就只剩 saveWhenQuit 一条路 ⇒ 被强杀/崩溃时这一项会丢（同 onChangeTopDown）。
        save();
        QuickTestTrace.Log("viewbtn", "setting visible=" + (t.value ? 1 : 0)
            + " key=" + ViewToggleButton.SettingKey);
    }

    /// <summary>
    /// 把「俯视角（平面图）」那一行的 toggle 刷成 <paramref name="value"/>。
    ///
    /// 给战斗界面那颗 56×56 视角钮用（<see cref="ViewToggleButton"/>）—— 它改的是**同一个**
    /// 全局键 <c>topDown_</c>，所以两边必须始终同状态：设置页开着的窗口要立刻跟上，
    /// 否则会出现「钮上是俯视、设置页里没勾」这种自相矛盾的画面。
    ///
    /// 只改控件的值、**不触发** <see cref="onChangeTopDown"/>：那边的副作用
    /// （写 view / ResetPan / 重摆 / 落盘）已经由 <c>ViewToggleButton.ApplyTopDown</c> 做过了，
    /// 再触发一遍就是重摆两次。
    /// ⛔ 纯新增方法，不加任何字段（发布 DLL 的字段布局须与已烘焙资源逐字段对齐）。
    /// </summary>
    public void SyncTopDownRow(bool value)
    {
        UIToggle t;
        if (!extraRowToggles.TryGetValue("topDown_", out t) || t == null)
        {
            return;         // 这一行还没建（设置窗口没开过）—— 存盘走 Config，真源不受影响
        }
        if (t.value != value)
        {
            t.value = value;
        }
    }

    /// <summary>
    /// 「盖放怪兽前询问」开关：点「前场放置」盖放怪兽前先弹一句「是否确定盖放[卡名]？」，
    /// 确认了才把应答发给 ocgcore。默认**开**（Config 缺省 "1"）。
    /// 出处是 KoishiPro 的同名功能（那边开关键叫 `ask_mset`，默认关；我们默认开）。
    /// 消费方：<see cref="Ocgcore.ES_gameButtonClicked"/>。
    ///
    /// **RD 与 OCG 是两份独立设置**（用户 2026-09-19 口径）：本行在不同模式下读写不同的键
    /// （见 <see cref="GameModeManager.KeyAskMset"/>），窗口里仍是同一行、切模式即时刷新值。
    /// ⚠ 节点名保持 `askMset_`（= OCG 的键名）不动 —— 验收脚本是按节点名点这个开关的。
    /// </summary>
    private void CreateAskMSetToggle()
    {
        AddExtraToggleRow("askMset_", "盖放怪兽前询问", () => GameModeManager.KeyAskMset, "1", true);
    }

    /// <summary>
    /// 「召唤前询问」开关：点「通常召唤」前先弹一句「是否确定召唤[卡名]？」，确认了才把应答
    /// 发给 ocgcore。默认**开**（Config 缺省 "1"）。
    /// 只拦通常召唤，不拦特殊召唤（口径见 <see cref="Ocgcore.askSummonBeforeSummon"/>）。
    /// RD / OCG 独立存储，同 CreateAskMSetToggle。
    /// </summary>
    private void CreateAskSummonToggle()
    {
        AddExtraToggleRow("askSummon_", "召唤怪兽前询问", () => GameModeManager.KeyAskSummon, "1", true);
    }

    /// <summary>RD 主色调（琥珀）。与主菜单 <c>Menu.ModeColorRD</c> **同值** —— 同一个「RD」标记跨界面必须一个颜色。</summary>
    private static readonly Color ModeColorRD = new Color(0.98f, 0.64f, 0.22f, 1f);

    /// <summary>追加行 → 该行的 RD 角标（只有 RD 专项行才建；建好即 SetActive(false)，等 SyncRdBadges 决定亮灭）。</summary>
    private readonly System.Collections.Generic.Dictionary<string, GameObject> extraRowRdBadges
        = new System.Collections.Generic.Dictionary<string, GameObject>();

    /// <summary>
    /// 在追加行尾部挂一枚 RD 角标。造法与主菜单 <c>Menu.CreateRdBadges</c> 同款：
    /// 运行时 new 一个 UILabel、借同一行的标签字体，不碰 prefab。
    ///
    /// 位置不在这里算死：UILabel 的宽度是 NGUI 在 Update 里排完版才知道的，而这里是
    /// initialize()（同一帧更早）——那一刻读到的是克隆来源（spyer_）的旧宽度。
    /// 所以位置交给 <see cref="RdBadgeFollower"/> 逐帧贴到锚点标签右边。
    /// </summary>
    /// <param name="pinTo">非 null = **锚定式**：把徽标钉在 <paramref name="pinTo"/> 的
    /// 右上角（不挂 <see cref="RdBadgeFollower"/>）。
    /// 为什么需要这一种（2026-10-02 第 6 轮，"卡名翻译"那一行）：跟随式用的是
    /// <c>anchor.localPosition + anchor.width</c>＝**标签的框**右沿，而那一行的行标题框
    /// （实测屏幕 661..761）右边紧贴着值那一格的框（771..971）⇒ 徽标落在值文字上
    /// （实拍 `_verify_viewbtn/20_rowrow_rd.png` 里「RD」压在「原生」两个字上）。
    /// 那一行真正空着的地方是 **870..955**（值文字结束到下拉箭头 957 之间），
    /// 所以钉在本行右沿、让开箭头即可 —— 它跟着行一起动（锚点每帧重算），与跟随式等价。</param>
    private void CreateRowRdBadge(string rowName, GameObject row, UILabel textLabel, UIRect pinTo = null)
    {
        if (textLabel == null)
        {
            return;
        }
        GameObject badge = new GameObject("rdBadge_" + rowName);
        badge.layer = row.layer;
        badge.transform.SetParent(row.transform, false);
        badge.transform.localPosition = Vector3.zero;
        badge.transform.localScale = Vector3.one;

        UILabel label = badge.AddComponent<UILabel>();
        label.text = "RD";
        label.color = ModeColorRD;
        label.fontSize = 16;
        if (textLabel.bitmapFont != null)
        {
            label.bitmapFont = textLabel.bitmapFont;
        }
        else
        {
            label.trueTypeFont = textLabel.trueTypeFont;
        }
        label.effectStyle = UILabel.Effect.Outline;
        label.effectColor = new Color(0.25f, 0.12f, 0f, 1f);
        label.effectDistance = new Vector2(1, 1);
        label.depth = textLabel.depth + 1;

        if (pinTo != null)
        {
            // 锚定式：盒子由锚点定死（所以不能再 ResizeFreely —— 那会每帧改自己的宽高、
            // 与锚点打架），文字在这个盒子里居中。
            label.overflowMethod = UILabel.Overflow.ClampContent;
            label.alignment = NGUIText.Alignment.Center;
            // 右沿留 24px 给下拉箭头（实测箭头在行右沿往左 8..14px 那一带）。
            const int w = 28, h = 22;
            label.leftAnchor.target = pinTo.transform;
            label.leftAnchor.relative = 1f;
            label.leftAnchor.absolute = -(24 + w);
            label.rightAnchor.target = pinTo.transform;
            label.rightAnchor.relative = 1f;
            label.rightAnchor.absolute = -24;
            label.topAnchor.target = pinTo.transform;
            label.topAnchor.relative = 1f;
            label.topAnchor.absolute = -6;
            label.bottomAnchor.target = pinTo.transform;
            label.bottomAnchor.relative = 1f;
            label.bottomAnchor.absolute = -(6 + h);
            label.ResetAndUpdateAnchors();
        }
        else
        {
            label.overflowMethod = UILabel.Overflow.ResizeFreely;
            RdBadgeFollower follower = badge.AddComponent<RdBadgeFollower>();
            follower.anchor = textLabel;
            follower.gap = 8f;
        }

        badge.SetActive(false);
        extraRowRdBadges[rowName] = badge;
    }

    /// <summary>
    /// 按当前模式刷 RD 角标的亮灭。角标语义：**「这一行现在读写的是 RD 那份存档」**，
    /// 所以只有按模式分键的行（askMset_ / askSummon_）有角标，且只在 RD 模式亮 ——
    /// OCG 下灭掉，是因为那时窗口里显示的是 OCG 的值，挂着 RD 标反而误导。
    ///
    /// 与主菜单一样，把「亮着的个数 + 名字」写进验收日志（判据是数个数，不是看字典大小）。
    /// </summary>
    private void SyncRdBadges()
    {
        bool rd = GameModeManager.IsRD;
        int shown = 0;
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        foreach (var kv in extraRowRdBadges)
        {
            if (kv.Value == null)
            {
                continue;
            }
            if (kv.Value.activeSelf != rd)
            {
                kv.Value.SetActive(rd);
            }
            if (rd)
            {
                shown++;
            }
            sb.Append(' ').Append(kv.Key).Append('=').Append(rd ? 1 : 0);
        }
        if (QuickTestTrace.Enabled)
        {
            QuickTestTrace.Log("setting", "rdBadges visible=" + shown + "/" + extraRowRdBadges.Count
                + " mode=" + GameModeManager.ModeLabel + sb);
        }
    }

    /// <summary>
    /// 追加行 → **行的对象本体**（创建时记下的直接引用）。
    ///
    /// 走这一张表而不是 <c>UIHelper.getByName</c>：后者是 `GetComponentsInChildren&lt;T&gt;()`
    /// 的深度搜索（更贵），而且**默认跳过未激活对象** —— 本项目真踩过这个坑：曾有一行
    /// RD 独占开关在 OCG 下被 `SetActive(false)`，getByName 就再也找不回它了。
    /// 留着直接引用，就不必依赖「这个对象此刻是否激活」这种隐式前提。
    /// </summary>
    private readonly System.Collections.Generic.Dictionary<string, GameObject> extraRowObjects
        = new System.Collections.Generic.Dictionary<string, GameObject>();

    /// <summary>追加行 → 行的 UIToggle（理由同 <see cref="extraRowObjects"/>：隐藏期间 getByName 找不到）。</summary>
    private readonly System.Collections.Generic.Dictionary<string, UIToggle> extraRowToggles
        = new System.Collections.Generic.Dictionary<string, UIToggle>();

    /// <summary>按行名取回追加行的对象本体。优先走直接引用，兜底才 getByName（未登记过的行）。</summary>
    private GameObject FindExtraRow(string rowName)
    {
        GameObject go;
        if (extraRowObjects.TryGetValue(rowName, out go) && go != null)
        {
            return go;
        }
        return UIHelper.getByName(gameObject, rowName);
    }

    /// <summary>本工程往设置窗口追加的自定义开关行数（决定新行的位置和窗口要往下长多少）。</summary>
    private int extraToggleRows = 0;

    /// <summary>追加过的自定义开关行名，save / saveWhenQuit 按这份名单落盘。</summary>
    private readonly System.Collections.Generic.List<string> extraToggleRowNames
        = new System.Collections.Generic.List<string>();

    /// <summary>
    /// 追加行 → 「这一行此刻该读写哪个 config 键」的解析器。
    /// 绝大多数行是固定键；「召唤前询问」「盖放前询问」两行按当前模式解析（RD/OCG 各自独立存储）。
    /// 之所以用解析器而不是固定键：模式可以在窗口活着的时候变，键必须跟着变。
    /// </summary>
    private readonly System.Collections.Generic.Dictionary<string, System.Func<string>> extraRowKeyOf
        = new System.Collections.Generic.Dictionary<string, System.Func<string>>();

    /// <summary>追加行 → 缺省值（切模式刷新时，某模式的键还不存在就按这个缺省显示）。</summary>
    private readonly System.Collections.Generic.Dictionary<string, string> extraRowDefault
        = new System.Collections.Generic.Dictionary<string, string>();

    /// <summary>取某个追加行**此刻**该读写的 config 键（按模式解析；查不到就退回节点名）。</summary>
    private string ExtraRowKey(string rowName)
    {
        System.Func<string> keyOf;
        if (extraRowKeyOf.TryGetValue(rowName, out keyOf))
        {
            return keyOf();
        }
        return rowName;
    }

    /// <summary>
    /// 追加行 → 这一行是不是**只该在 RD 模式出现**。
    ///
    /// 加这个机制之前没有它：<c>rdSpecific</c> 只决定「挂不挂 RD 角标」，行本身两种模式都常显
    /// （见 askMset_ / askSummon_）。而「极大怪兽一体化」只在 RD 里有意义（OCG 没有极大怪兽），
    /// 所以要真的把行藏起来。
    /// ⛔ 隐藏/点亮都必须走 <see cref="extraRowObjects"/> 的直接引用 —— `UIHelper.getByName`
    ///   会**跳过未激活对象**，本项目真踩过这个坑（见 extraRowObjects 的注释）。
    /// </summary>
    private readonly System.Collections.Generic.HashSet<string> extraRowRdOnly
        = new System.Collections.Generic.HashSet<string>();

    /// <summary>
    /// 克隆来源（prefab 原生的 spyer_）那一行的 localPosition —— 追加行的**基准 y**。
    /// 建第一行时记下来：藏掉某一行之后要**重排可见行**（把空档收掉），而这一步不该再去查
    /// spyer_（`getByName` 会跳过未激活对象，本项目在这上面栽过）；缓存一次最稳。
    /// </summary>
    private Vector3 extraRowSrcLocalPos = Vector3.zero;
    private bool extraRowSrcPosSaved = false;

    /// <summary>设置页里**永远不显示**的追加行（2026-09-30 用户要求：俯视角不再能从设置页切，
    /// 只能靠战斗界面那颗眼形钮 —— 见 <see cref="ViewToggleButton"/>）。
    ///
    /// <para>⛔ <b>static，不加实例字段</b>：发布 DLL 的字段布局必须与已烘焙资源逐字段对齐
    /// （MEMORY 红线）。与 <c>ViewToggleButton.SettingVisible</c> 同样的处理方式。</para>
    ///
    /// <para><b>为什么「建了行再藏」而不是干脆不建</b>：不建的话这一行就不进
    /// <c>save()</c> 的落盘循环（它遍历 <c>extraToggleRowNames</c>），
    /// 而 <c>topDown_</c> 的值有三处会读它（<c>AddExtraToggleRow</c> 建行时、
    /// <c>SyncTopDownRow</c> 按钮改视角后回刷、<c>save()</c> 落盘）。
    /// 建了藏起来 ⇒ toggle 仍持有当前值、<c>save()</c> 照常落盘、按钮回刷照常工作，
    /// 只是**玩家看不见也点不到**。不建则这三处里有两处变成死代码。</para>
    ///
    /// <para>藏了之后 <see cref="ExtraRowVisible"/> 把它算作不可见 ⇒
    /// <see cref="VisibleRowCount"/> 不计它、<c>SyncExtraRowVisibility</c> 会把后面的行
    /// **往上提一格** ⇒ 窗口高度自动收回，<b>不会留 24px 空档</b>。</para>
    /// </summary>
    private static readonly System.Collections.Generic.HashSet<string> extraRowNeverShown
        = new System.Collections.Generic.HashSet<string>();

    /// <summary>这一行此刻该不该显示（RD 独占行在 OCG 下不显示；
    /// <see cref="extraRowNeverShown"/> 里的**恒不显示**；其余显示）。</summary>
    private bool ExtraRowVisible(string rowName)
    {
        if (extraRowNeverShown.Contains(rowName))
        {
            return false;
        }
        if (extraRowRdOnly.Contains(rowName))
        {
            return GameModeManager.IsRD;
        }
        return true;
    }

    /// <summary>
    /// 当前**可见**的追加行数 —— 窗口高度只按它长（见 <see cref="GrowWindowForExtraRows"/>）。
    ///
    /// ⛔ 为什么不能用 <c>extraToggleRows</c>（＝建过的行数）：既有验收脚本把「行数」当判据
    ///   （`_verify_askconfirm.py` 的 `rows == 3` / `bg == 524 + 24×rows` / `winY == -12×rows`），
    ///   而 RD 独占行在 OCG 下是藏起来的、不该占高度。改成可见行数后，OCG 侧依旧是
    ///   `rows=3 / bg=596 / winY=-36` —— 与加这一行之前**逐字段相同**；只有 RD 才是 4。
    /// </summary>
    private int VisibleRowCount()
    {
        int n = 0;
        for (int i = 0; i < extraToggleRowNames.Count; i++)
        {
            if (ExtraRowVisible(extraToggleRowNames[i]))
            {
                n++;
            }
        }
        return n;
    }

    /// <summary>
    /// 按当前模式同步追加行的**显隐 + 行位**，再让窗口跟着长高/收回。
    ///
    /// 两件事必须一起做：
    ///   ① 显隐：RD 独占行只在 RD 出现；
    ///   ② **重排可见行**：行 y 是建行时按 slot 写死的（见 AddExtraToggleRow），藏掉一行会留一个
    ///      24px 的空档、窗口还会照旧偏高。这里按创建顺序只对**可见**行重新编号 ——
    ///      第 1 行 = spyer_ 的下一行（基准 y − 24），第 n 行 = 基准 y − 24×n。
    ///      于是 OCG 下的行位与加这一行之前完全一致，RD 下则是 4 行连续排下来。
    ///
    /// 调用点：每建完一行（AddExtraToggleRow 末尾）、initialize 末尾、模式切换
    /// （OnGameModeChanged）—— 三处都要求「先同步显隐、再算窗口高度」，否则会拿旧行数去长窗口。
    /// </summary>
    private void SyncExtraRowVisibility()
    {
        int slot = 0;
        for (int i = 0; i < extraToggleRowNames.Count; i++)
        {
            string name = extraToggleRowNames[i];
            GameObject go;
            if (!extraRowObjects.TryGetValue(name, out go) || go == null)
            {
                continue;
            }
            // ⚠ 显隐**只走 `ExtraRowVisible` 这一个出口**：原先这里是内联的
            //   `!extraRowRdOnly.Contains(name) || rd`，与 `ExtraRowVisible` 重复了一份 ——
            //   2026-09-30 加「永久隐藏」判据时就差点只改一处、结果行还照旧显示。
            bool show = ExtraRowVisible(name);
            if (go.activeSelf != show)
            {
                go.SetActive(show);
            }
            if (!show)
            {
                continue;
            }
            slot++;
            if (extraRowSrcPosSaved)
            {
                // 只改 y（x/z 沿用克隆时与 spyer_ 同列的值）。
                Vector3 lp = go.transform.localPosition;
                lp.y = extraRowSrcLocalPos.y - 24f * slot;
                go.transform.localPosition = lp;
            }
        }
        if (QuickTestTrace.Enabled)
        {
            // 排查用：可见行数 / 总行数 / 隐藏了几行。字段名刻意避开 `rdOnly `（带空格）——
            // 历史验收脚本用 `lines_of(..., "rdOnly ")` 断言「按模式藏行的机制已删干净」，
            // 那是**旧策略**的判据（旧行 rdPileSame_ 连同机制一起删过），本功能的验收会把它
            // 改成正面判据；这里不制造歧义。
            QuickTestTrace.Log("setting", "rdRows visible=" + VisibleRowCount() + "/"
                + extraToggleRowNames.Count + " mode=" + GameModeManager.ModeLabel
                + " hidden=" + (extraToggleRowNames.Count - VisibleRowCount())
                // ⚠ 永久隐藏（俯视角那行）**与**按模式隐藏必须能分开：
                //   `hidden=` 只是差值，看不出是哪一类藏的 ⇒ 验收脚本会把「按模式藏 3 行」
                //   和「永久藏 1 行 + 按模式藏 2 行」当成同一件事。逐个点名。
                + " neverShown=" + string.Join("|", extraRowNeverShown)
                // ⚠⚖ 再加逐行 `act=`：**不能**拿「日志里有没有 `[setting] toggle <row>` 行」
                //   来判断那一行可不可见 —— 那个日志在 `SyncExtraRowVisibility()` **之后**才打，
                //   而失活对象的 `transform.position` 照样读得出来 ⇒ 藏起来的行**也会**被报出来。
                //   （2026-09-30 写判据时在这上面栽过：把「隐藏」写成「日志里没有」，
                //     那条判据恒红 —— 它证明的是错的东西。）
                //   `act=` 直接报 `SetActive` 之后的真值，那才是玩家能不能看到/点到。
                + " act=" + ExtraRowActiveList());
        }
        GrowWindowForExtraRows();
    }

    /// <summary>逐行报 `<行名>=<activeSelf 0/1>`（创建顺序）。
    /// 供验收脚本判「这一行此刻到底显不显示」——只看行数或只看 `hidden=` 差值都不够。</summary>
    private string ExtraRowActiveList()
    {
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        for (int i = 0; i < extraToggleRowNames.Count; i++)
        {
            string name = extraToggleRowNames[i];
            GameObject go;
            bool act = extraRowObjects.TryGetValue(name, out go) && go != null
                && go.activeSelf;
            if (sb.Length > 0)
            {
                sb.Append('|');
            }
            sb.Append(name).Append('=').Append(act ? 1 : 0);
        }
        return sb.ToString();
    }

    /// <summary>
    /// 「极大怪兽一体化」开关（**只在 RD 模式出现**、默认开）。开启后：
    ///   ① 极大状态（三件齐）的极大怪兽被鼠标悬浮时，三件 **＋ 极大立绘视为一体**一起放大，
    ///      不再各自单独放大；**单独在场**的极大怪兽（未组成极大）不受影响；
    ///   ② 点 L/R 部件等效于点中间件 —— 卡面点击转发给本体，且悬浮 L/R 时把本体的
    ///      「发动效果 / 攻击宣言」按钮浮出来。发动/攻击依旧记在本体上（服务器天然保证
    ///      不能反复发动 / 反复攻击）；
    ///   ③ 左侧显示栏不变（仍是「你悬浮的那张部件」自己的资料）。
    ///
    /// 键是**全局固定键**（不分 RD/OCG）：这一行本来就只有 RD 能看见、能改，没必要再分键。
    /// 消费方（<c>Ocgcore.rdMaxIntegratedTick</c> / <c>Ocgcore.RdMaximumClickTarget</c>）
    /// **每帧实时读 Config**，不缓存、不挂 onChange —— 与 askMset_ 那条同一口径。
    /// </summary>
    private void CreateRdMaxIntegratedToggle()
    {
        AddExtraToggleRow("rdMaxIntegrated_", "极大怪兽一体化", () => "rdMaxIntegrated_", "1",
            rdSpecific: true, rdOnly: true);
    }

    /// <summary>模式切换钩子只挂一次（initialize 可能被反复调用）。</summary>
    private bool modeHookInstalled = false;

    /// <summary>
    /// 挂上模式切换钩子：切到 RD / 切回 OCG 时，把「按模式解析键」的那几行刷成新模式的存档值。
    /// 不刷新的话，在 OCG 下打开过设置窗口后切到 RD，看到的还是 OCG 那个值。
    /// </summary>
    private void InstallModeHook()
    {
        if (modeHookInstalled)
        {
            return;
        }
        modeHookInstalled = true;
        GameModeManager.Changed += OnGameModeChanged;
    }

    private void OnGameModeChanged(GameModeManager.Mode mode)
    {
        // 顺序：先按新模式同步**显隐与行位**（RD 独占行要出场 / 退场），再按新模式重读各行取值。
        // ⚠ 不能反：RefreshExtraToggleRows 末尾会刷 RD 角标，而角标语义是「这一行现在读哪一份
        //   存档」—— 「这一行此刻在不在窗口里」必须先定下来，否则会刷出一帧对不上的角标状态。
        SyncExtraRowVisibility();
        // ⚠ 先把 RD 角标刷到位，再重建译名行的下拉：`RebuildTranslationPackItems` 的台账里
        //   有一格 `badge=`，不先刷就会打出「rd=1 但 badge=0」—— 那个 0 只是同一拍里的
        //   先后顺序（角标在 RefreshExtraToggleRows 末尾才亮），但对着日志判据的人
        //   只会读成「RD 下来了、角标没亮」。（第一次实测就出现了这一条。）
        SyncRdBadges();
        // 「卡名翻译」下拉框的**选项清单**也随模式变（RD 只剩原生）⇒ 必须重建，
        // 否则切到 RD 后显示值已被压成原生、点开列表却还列着 nw/cnocg/简中。
        RebuildTranslationPackItems();
        RefreshTranslationPackRow();
        RefreshExtraToggleRows();
    }

    /// <summary>
    /// 按当前模式重读所有追加行的值。
    /// 注意 UIToggle.value 的 setter 会触发 onChange（= save），但这里写回的都是刚读出来的同一个值，
    /// 落盘结果不变，属于空转一次，可以接受 —— 换来的是「切模式立刻看到该模式的值」。
    /// </summary>
    private void RefreshExtraToggleRows()
    {
        for (int i = 0; i < extraToggleRowNames.Count; i++)
        {
            string name = extraToggleRowNames[i];
            // ⚠ 同上：必须走直接引用。隐藏中的行（OCG 下的 RD 独占行）用 getByName 会被
            //   整个跳过 —— 值不刷、方向对不上，等切回 RD 点亮时显示的还是上一次读到的旧值
            //   （UI 与 Config 打架，正是本项目最忌讳的那类错）。
            UIToggle t;
            if (!extraRowToggles.TryGetValue(name, out t) || t == null)
            {
                continue;
            }
            System.Func<string> keyOf;
            if (!extraRowKeyOf.TryGetValue(name, out keyOf))
            {
                continue;
            }
            string def = extraRowDefault.ContainsKey(name) ? extraRowDefault[name] : "0";
            bool now = UIHelper.fromStringToBool(Config.Get(keyOf(), def));
            if (t.value != now)
            {
                t.value = now;
            }
            QuickTestTrace.Log("setting", "refresh " + name
                + " key=" + keyOf() + " value=" + now + " mode=" + GameModeManager.ModeLabel);
        }
        // 值刷完再把 RD 角标刷一遍 —— 角标与「这一行现在读哪一份存档」必须同步，
        // 分开刷会出现「值已经是 RD 的、角标还灭着」的中间态被玩家看到。
        SyncRdBadges();
    }

    /// <summary>
    /// 往 spyer_ 那一列（x=-258、行距 24）下面追加一行开关。**不动 prefab**：
    /// 运行时克隆 spyer_ 本身，只改名字与文案，所以行距/字号/勾选框贴图天然一致。
    ///
    /// 克隆后必须清掉 UIEventTrigger 的悬停/按压委托 —— Instantiate 会把运行时挂上的
    /// 提示框委托一并复制，出过「悬停提示造两份、赖着不走」的事（同 tryCreateUndoButton）。
    /// 注意 UIToggle.value 的 setter 会触发 onChange，所以**先设初值再挂 save 事件**，
    /// 否则会空跑一次落盘。
    ///
    /// <paramref name="keyOf"/> 是「该行此刻读写哪个 config 键」的解析器（延迟求值）：
    /// 固定键的行传常量，按模式分键的行传 GameModeManager 的取键属性。
    ///
    /// <paramref name="rdSpecific"/> = 这一行是不是**RD 专项**（按模式分键、RD 有自己一份存档）。
    /// true 时行尾挂一枚 RD 角标，随模式亮灭（见 <see cref="SyncRdBadges"/>）；OCG 通用开关传 false。
    ///
    /// <paramref name="rdOnly"/> = 这一行是不是**只在 RD 模式出现**（OCG 下整行藏起来，
    /// 见 <see cref="SyncExtraRowVisibility"/>）。默认 false = 两种模式都常显。
    /// ⚠ 与 rdSpecific 是**两件事**：rdSpecific 只管角标、不管显隐；要「只在 RD 出现」必须用 rdOnly。
    /// </summary>
    private UIToggle AddExtraToggleRow(string rowName, string label, System.Func<string> keyOf, string configDefault,
        bool rdSpecific = false, bool rdOnly = false)
    {
        UIToggle src = UIHelper.getByName<UIToggle>(gameObject, "spyer_");
        if (src == null || UIHelper.getByName<UIToggle>(gameObject, rowName) != null)
        {
            return null;
        }
        if (!extraRowSrcPosSaved)
        {
            // 基准 y 只记一次：重排可见行（SyncExtraRowVisibility）要用它，之后不该再去查 spyer_。
            extraRowSrcLocalPos = src.transform.localPosition;
            extraRowSrcPosSaved = true;
        }
        int slot = extraToggleRows;
        GameObject clone = UnityEngine.Object.Instantiate(src.gameObject);
        clone.name = rowName;
        clone.transform.SetParent(src.transform.parent, false);
        clone.transform.localPosition = src.transform.localPosition + new Vector3(0f, -24f * (slot + 1), 0f);
        clone.transform.localScale = src.transform.localScale;
        UIEventTrigger trigger = clone.GetComponent<UIEventTrigger>();
        if (trigger != null)
        {
            trigger.onHoverOver.Clear();
            trigger.onHoverOut.Clear();
            trigger.onPress.Clear();
        }
        UILabel textLabel = clone.GetComponentInChildren<UILabel>();
        if (textLabel != null)
        {
            textLabel.text = label;
        }
        if (rdSpecific)
        {
            CreateRowRdBadge(rowName, clone, textLabel);
        }
        UIToggle toggle = clone.GetComponent<UIToggle>();
        toggle.value = UIHelper.fromStringToBool(Config.Get(keyOf(), configDefault));
        UIHelper.registEvent(gameObject, rowName, save);
        extraToggleRows = slot + 1;
        extraToggleRowNames.Add(rowName);
        extraRowKeyOf[rowName] = keyOf;
        extraRowDefault[rowName] = configDefault;
        if (rdOnly)
        {
            extraRowRdOnly.Add(rowName);
        }
        // 记下**直接引用**（行本体 + toggle）：刷值 / 报几何都走这两个表（详见 extraRowObjects）。
        extraRowObjects[rowName] = clone;
        extraRowToggles[rowName] = toggle;
        // 记完这一行：先按模式同步显隐与行位，再由它把窗口高度长到位。
        // ⛔ 顺序不能反 —— 高度是按**可见行数**算的（GrowWindowForExtraRows → VisibleRowCount），
        //   先长窗口就会拿「这一行还没参与显隐裁决」的旧行数去算。
        InstallModeHook();
        SyncExtraRowVisibility();
        if (QuickTestTrace.Enabled)
        {
            // 与 SelectServer.LogAnchor 同款：设置窗口挂在 camera_main_2d 下，
            // 用 camera_back_ground_2d 换算会把坐标映射到屏幕外（y 变负）。
            Vector3 sp = Program.camera_main_2d != null
                ? Program.camera_main_2d.WorldToScreenPoint(clone.transform.position)
                : Vector3.zero;
            QuickTestTrace.Log("setting", "toggle " + rowName
                + " key=" + keyOf()
                + " screen=(" + Mathf.RoundToInt(sp.x) + "," + Mathf.RoundToInt(Screen.height - sp.y) + ")"
                + " value=" + toggle.value);
        }
        return toggle;
    }

    /// <summary>
    /// 追加了 extraToggleRows 行之后，把窗口背景往下长高。
    ///
    /// prefab 里 mainWindow 挂着窗口背景（切片 bg sprite，650×524，**pivot = Center**），
    /// 本身挂在中间层「GameObject」节点下（不是根的直接子节点），
    /// 它的直接子节点是 content（装所有开关）/ exit_ / 标题 / 分隔线。
    ///
    /// ⚠ 不能简单地 `bg.height += 24 * n`：pivot 是 Center，加高会**上下各伸一半**，
    /// 顶边跟着往上跑，标题与顶边的留白越加越大（加一行时就已经多溢出 24px）。
    /// 这里改成「只往下长」：把 mainWindow 下移 delta/2 抵消顶边，再把它的直接子节点
    /// 整体上移同样的量抵消掉，净效果 = 底边下移 delta、顶边与所有内容原地不动。
    /// 增量式（按当前高度算 delta），所以可以被多次追加调用，每次长一行。
    /// </summary>
    private void GrowWindowForExtraRows()
    {
        Transform mainWindowT = gameObject.transform.Find("GameObject/mainWindow");
        if (mainWindowT == null)
        {
            mainWindowT = gameObject.transform.Find("mainWindow");
        }
        if (mainWindowT == null)
        {
            return;
        }
        UISprite bg = mainWindowT.GetComponent<UISprite>();
        if (bg == null)
        {
            return;
        }
        const int baseHeight = 524;      // prefab 原值
        // ⛔ 按**可见行数**长（不是 extraToggleRows = 建过的行数）：RD 独占行在 OCG 下是藏起来的、
        //   不该占高度。这样 OCG 侧恒为 rows=3 / bg=596 / winY=-36，与加这一行之前逐字段相同。
        // ⛔ 还要加 packRowReserveRows：「卡名翻译」那一行**不占** extraRowToggles（它不是开关行），
        //   但它把 spyer_ 那一列整体下移了若干格（见 PushMiddleColumnDown）⇒ 窗口必须长同样多，
        //   否则最下面那几行会被底边切掉。
        int visibleRows = VisibleRowCount() + packRowReserveRows;
        int target = baseHeight + 24 * visibleRows;
        int delta = target - bg.height;
        if (delta == 0)
        {
            return;                      // 已经到位了（重复调用时走这一条）
        }
        // 下移一半抵消顶边（bg 是 pivot=Center，加高两头伸），
        // 再把直接子节点整体上移同样的量抵消掉 —— 净效果只有底边在动。
        int shift = delta / 2;
        mainWindowT.localPosition += new Vector3(0f, -shift, 0f);
        foreach (Transform child in mainWindowT)
        {
            child.localPosition += new Vector3(0f, shift, 0f);
        }
        bg.height = target;
        BoxCollider box = mainWindowT.GetComponent<BoxCollider>();
        if (box != null)
        {
            box.size = new Vector3(box.size.x, target, box.size.z);
        }
        if (QuickTestTrace.Enabled)
        {
            // 几何落盘。⚠ 此刻窗口还停在屏幕外 1.5 倍高处（createWindow 的初始位），
            // 所以**不要**用屏幕坐标判「这一行有没有被切掉」—— 全部在 mainWindow 的
            // 局部坐标里算：bg 的 pivot 是 Center，在它自己节点的局部系里永远占
            // [-h/2, +h/2]，于是「行的 localY 到底边的距离」就是这一行离窗口下沿的留白，
            // 与窗口此刻摆在屏幕哪个位置无关。
            //   topRoot 是**顶边在窗口根坐标系里的位置**，它必须恒等于 prefab 原值
            //   （524/2 = 262）—— 这一条正是「只往下长、不向上溢」的判据。
            float bottomY = -bg.height * 0.5f;
            float topRoot = mainWindowT.localPosition.y + bg.height * 0.5f;
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            sb.Append("grow rows=").Append(visibleRows)
              .Append(" bg=").Append(bg.height)
              .Append(" box=").Append(box != null ? box.size.y.ToString("F0") : "null")
              .Append(" winY=").Append(mainWindowT.localPosition.y.ToString("F1"))
              .Append(" topRoot=").Append(topRoot.ToString("F1"))
              .Append(" bottomY=").Append(bottomY.ToString("F1"));
            for (int i = -1; i < extraToggleRowNames.Count; i++)
            {
                string n = i < 0 ? "spyer_" : extraToggleRowNames[i];
                if (i >= 0 && !ExtraRowVisible(n))
                {
                    // 藏起来的行（OCG 下的 RD 独占行）不报几何：它在窗口里不存在，
                    // 报了会顶掉「留白咬清单最后一行」那条判据（S6 咬的是可见行的末行）。
                    continue;
                }
                // 追加行走直接引用（理由见 extraRowObjects）；spyer_ 是 prefab 原生行、不在追加表里。
                GameObject go = i < 0 ? UIHelper.getByName(gameObject, n) : FindExtraRow(n);
                Transform t = go != null ? go.transform : null;
                if (t == null)
                {
                    sb.Append(' ').Append(n).Append("=null");
                    continue;
                }
                float ly = mainWindowT.InverseTransformPoint(t.position).y;
                sb.Append(' ').Append(n).Append("=(y").Append(ly.ToString("F1"))
                  .Append(",margin").Append((ly - bottomY).ToString("F1")).Append(')');
            }
            QuickTestTrace.Log("setting", sb.ToString());
        }
    }

    private void readVales()
    {
        try
        {
            setting.sliderVolum.forceValue(((float)(int.Parse(Config.Get("vol_", "750")))) / 1000f);
            setting.sliderSize.forceValue(((float)(int.Parse(Config.Get("size_", "500")))) / 1000f);
            setting.sliderSizeDrawing.forceValue(((float)(int.Parse(Config.Get("vSize_", "500")))) / 1000f);
            //setting.sliderAlpha.forceValue(((float)(int.Parse(Config.Get("alpha_", "666")))) / 1000f);
            onChangeAlpha();
            onChangeSize();
        }
        catch (Exception e)
        {
            Debug.Log(e);
        }
    }

    public void onchangeCloud()
    {
        Program.MonsterCloud = setting.cloud.value;
    }

    public void onchangeMouse()
    {
        Program.I().mouseParticle.SetActive(setting.mouseEffect.value);
    }

    //private int dontResizeTwice = 2;

    public void setScreenSizeValue()
    {
        //dontResizeTwice = 3;
        UIHelper.getByName<UIPopupList>(gameObject, "screen_").value = Screen.width.ToString() + "*" + Screen.height.ToString();
    }

    void onCP()
    {
        try
        {
            Program.I().ocgcore.realize(true);
        }
        catch (Exception e) 
        {
        }
    }


    public void onchangeCloseUp()   
    {
        if (setting.closeUp.value == false)
        {
            setting.sliderAlpha.forceValue(0);
            setting.sliderSize.forceValue(0);
        }
        else
        {
            setting.sliderAlpha.forceValue(0.6666f);
            setting.sliderSize.forceValue(1f);
        }
        onChangeSize();
        onChangeAlpha();
    }

    public int atk = 1800;
    public int star = 5;

    void onchangeClose()
    {
        atk = 1800;
        star = 5;
        try
        {
            atk = int.Parse(setting.showoffATK.value);
        }
        catch (Exception)
        {

        }
        try
        {
            star = int.Parse(setting.showoffStar.value);
        }
        catch (Exception)
        {

        }
    }


    UISlider sliderAlpha;
    void onChangeAlpha()
    {
        if (sliderAlpha != null)
        {
            Program.transparency = 1.5f * sliderAlpha.value;
        }
        Program.transparency = 1f;
    }

    void onChangeLongField()
    {
        Program.longField = UIHelper.getByName<UIToggle>(gameObject, "longField_").value;
        onCP();
    }

    UISlider sliderVsize;
    void onChangeVsize()
    {
        if (sliderVsize != null)
        {
            Program.verticleScale = 4f + 2f * sliderVsize.value;
        }
    }

    UISlider sliderSize;
    void onChangeSize()  
    {
        if (sliderSize != null)
        {
            Program.fieldSize = 1f + sliderSize.value * 0.21f;
        }
    }

    public float vol() 
    {
        return UIHelper.getByName<UISlider>(gameObject, "vol_").value;
    }

    private int rayDumped = 0;
    private int shownAtFrame = -1;

    public override void preFrameFunction()
    {
        base.preFrameFunction();
        // 「卡名翻译」下拉框的落点：**窗口真的显示出来之后**再量再摆。
        // ⛔ 建行时按 localPosition 猜是错的（实测算出 y=147.8，正好压在 resize_/full_ 上）：
        //   运行时左列那几个控件的 localPosition 与 prefab 里的值不是一回事。
        //   这里改成屏幕空间量矩形（见 PlaceTranslationPackRow），窗口刚开的几帧多摆几次，
        //   落定后（位移 < 1px）就停手。
        if (isShowed && packRowPlaceFrames > 0 && Time.frameCount >= packRowNextFrame)
        {
            if (PlaceTranslationPackRow())
            {
                packRowPlaceFrames--;
                // 隔 3 帧再量：worldCorners 要等 NGUI 那一轮更新才反映我们改过的位置。
                packRowNextFrame = Time.frameCount + 3;
            }
        }
        // 排查用（仅 log/qt_debug.on 生效，只打一次）：窗口亮起 1 秒后（iTween 滑入
        // 0.6 秒已落定），对每个追加开关的中心做一次全量射线，列出压在这个像素上的
        // 所有碰撞盒与距离 —— 实测「点了开关没反应」就是被别的碰撞盒截胡，这份清单
        // 能直接指认。注意不能在 initialize 时打：那时窗口还停在屏幕外 1.5 倍高处。
        // 命中清单里必须有自己（名字 = 开关名）；只剩背景 mainWindow 一个命中就说明
        // 这一行被窗口底边截掉了 —— 加行后窗口没跟着长高时的典型症状。
        if (QuickTestTrace.Enabled && rayDumped == 0 && isShowed)
        {
            if (shownAtFrame < 0)
            {
                shownAtFrame = Time.frameCount;
            }
            if (Time.frameCount - shownAtFrame > 60)
            {
                rayDumped = 1;
                for (int i = 0; i < extraToggleRowNames.Count; i++)
                {
                    UIToggle t = UIHelper.getByName<UIToggle>(gameObject, extraToggleRowNames[i]);
                    if (t == null || Program.camera_main_2d == null)
                    {
                        continue;
                    }
                    Vector3 sp = Program.camera_main_2d.WorldToScreenPoint(t.transform.position);
                    Ray ray = Program.camera_main_2d.ScreenPointToRay(sp);
                    RaycastHit[] hits = Physics.RaycastAll(ray);
                    System.Text.StringBuilder sb = new System.Text.StringBuilder();
                    for (int h = 0; h < hits.Length; h++)
                    {
                        sb.Append(hits[h].collider.gameObject.name);
                        sb.Append("@d=").Append(hits[h].distance.ToString("F2"));
                        sb.Append(" z=").Append(hits[h].collider.transform.position.z.ToString("F2"));
                        sb.Append("; ");
                    }
                    QuickTestTrace.Log("setting", "ray " + extraToggleRowNames[i]
                        + " screen=(" + Mathf.RoundToInt(sp.x)
                        + "," + Mathf.RoundToInt(Screen.height - sp.y) + ") camZ="
                        + Program.camera_main_2d.transform.position.z.ToString("F1")
                        + " hits[" + hits.Length + "]: " + sb);
                }

                // 关闭按钮（exit_）也报一次落定坐标。验收脚本的流程是「点开关 → 关窗口 →
                // 切模式 → 再开窗口」，没有这个坐标就只能猜关闭键的位置；而 RD 入口按钮
                // 正好被设置窗口盖住，**关不掉窗口就切不了模式**。同样等 60 帧（滑入落定）后才报。
                Transform exitT = UIHelper.getByName<Transform>(gameObject, "exit_");
                if (exitT != null && Program.camera_main_2d != null)
                {
                    Vector3 ep = Program.camera_main_2d.WorldToScreenPoint(exitT.position);
                    QuickTestTrace.Log("setting", "exit screen=(" + Mathf.RoundToInt(ep.x)
                        + "," + Mathf.RoundToInt(Screen.height - ep.y) + ")");
                }
                else
                {
                    QuickTestTrace.Log("setting", "exit = null");
                }
            }
        }
    }

    void onClickExit()
    {
        hide();
    }

    void resizeScreen()
    {
        //if (dontResizeTwice > 0)
        //{
        //    dontResizeTwice--;
        //    return;
        //}
        //dontResizeTwice = 2;
        if (UIHelper.isMaximized())
            UIHelper.RestoreWindow();
        string[] mats = UIHelper.getByName<UIPopupList>(gameObject, "screen_").value.Split(new string[] { "*" }, StringSplitOptions.RemoveEmptyEntries);
        if (mats.Length == 2)
        {
            Screen.SetResolution(int.Parse(mats[0]), int.Parse(mats[1]), UIHelper.getByName<UIToggle>(gameObject, "full_").value);
        }
        Program.go(100, () => { Program.I().fixScreenProblems(); });
    }

    public void saveWhenQuit()
    {
        Config.Set("vol_", ((int)(UIHelper.getByName<UISlider>(gameObject, "vol_").value * 1000)).ToString());
        Config.Set("size_", ((int)(UIHelper.getByName<UISlider>(gameObject, "size_").value * 1000)).ToString());
        Config.Set("vSize_", ((int)(UIHelper.getByName<UISlider>(gameObject, "vSize_").value * 1000)).ToString());
        //Config.Set("alpha_", ((int)(UIHelper.getByName<UISlider>(gameObject, "alpha_").value * 1000)).ToString());
        Config.Set("longField_", UIHelper.fromBoolToString(UIHelper.getByName<UIToggle>(gameObject, "longField_").value));
        var collection = gameObject.GetComponentsInChildren<UIToggle>();
        for (int i = 0; i < collection.Length; i++) 
        {
            if (collection[i].name.Length > 0 && collection[i].name[0] == '*')
            {
                Config.Set(collection[i].name, UIHelper.fromBoolToString(collection[i].value));
            }
        }
        Config.Set("showoffATK", setting.showoffATK.value.ToString());
        Config.Set("showoffStar", setting.showoffStar.value.ToString());
        Config.Set("resize_", UIHelper.fromBoolToString(UIHelper.getByName<UIToggle>(gameObject, "resize_").value));
        Config.Set("maximize_", UIHelper.fromBoolToString(UIHelper.isMaximized()));
        for (int i = 0; i < extraToggleRowNames.Count; i++)
        {
            // ⚠ 必须走**直接引用**（extraRowToggles）：隐藏中的行（OCG 下的 RD 独占行）用
            //   getByName 会被整个跳过 ⇒ 这一项再也存不进盘（理由见 extraRowObjects 的注释）。
            UIToggle t;
            if (!extraRowToggles.TryGetValue(extraToggleRowNames[i], out t) || t == null)
            {
                continue;
            }
            Config.Set(ExtraRowKey(extraToggleRowNames[i]), UIHelper.fromBoolToString(t.value));
        }
    }

    public void save()
    {
        System.Text.StringBuilder trace = QuickTestTrace.Enabled ? new System.Text.StringBuilder() : null;
        if (trace != null)
        {
            UIToggle spyer = UIHelper.getByName<UIToggle>(gameObject, "spyer_");
            trace.Append("save spyer_=").Append(spyer != null ? spyer.value.ToString() : "null");
        }
        Config.Set("ignoreWatcher_",UIHelper.fromBoolToString(UIHelper.getByName<UIToggle>(gameObject, "ignoreWatcher_").value));
        Config.Set("ignoreOP_", UIHelper.fromBoolToString(UIHelper.getByName<UIToggle>(gameObject, "ignoreOP_").value));
        Config.Set("smartSelect_", UIHelper.fromBoolToString(UIHelper.getByName<UIToggle>(gameObject, "smartSelect_").value));
        Config.Set("autoChain_", UIHelper.fromBoolToString(UIHelper.getByName<UIToggle>(gameObject, "autoChain_").value));
        Config.Set("handPosition_", UIHelper.fromBoolToString(UIHelper.getByName<UIToggle>(gameObject, "handPosition_").value));
        Config.Set("handmPosition_", UIHelper.fromBoolToString(UIHelper.getByName<UIToggle>(gameObject, "handmPosition_").value));
        Config.Set("spyer_", UIHelper.fromBoolToString(UIHelper.getByName<UIToggle>(gameObject, "spyer_").value));
        // 追加行都在「改动即存」这一档：消费方（卡组界面点测试、决斗里点盖放）
        // 是实时读 Config 的，只靠 saveWhenQuit 的话运行中永远读不到。
        for (int i = 0; i < extraToggleRowNames.Count; i++)
        {
            // ⚠ 同上：走**直接引用**，隐藏中的行不能被 getByName 跳过
            //   （否则「改一下即存」这一档对它失效，只能等退出时才落盘）。
            UIToggle t;
            if (!extraRowToggles.TryGetValue(extraToggleRowNames[i], out t) || t == null)
            {
                continue;
            }
            // 落盘的键按模式解析：RD 的那两行写自己的键，OCG 的两行走历史键。
            Config.Set(ExtraRowKey(extraToggleRowNames[i]), UIHelper.fromBoolToString(t.value));
            if (trace != null)
            {
                trace.Append(' ').Append(extraToggleRowNames[i])
                    .Append("@").Append(ExtraRowKey(extraToggleRowNames[i]))
                    .Append('=').Append(t.value);
            }
        }
        if (trace != null)
        {
            QuickTestTrace.Log("setting", trace.ToString());
        }
        if (UIHelper.getByName<UIToggle>(gameObject, "high_").value)
        {
            QualitySettings.SetQualityLevel(5);
        }
        else
        {
            QualitySettings.SetQualityLevel(0);
        }
    }

    public float soundValue()
    {
        return UIHelper.getByName<UISlider>(gameObject, "vol_").value;
    }
}

/// <summary>
/// 让一枚 RD 角标贴在锚点标签的右边（贴着文字走，不与文案重叠）。
///
/// 为什么是逐帧跟随而不是「造的时候算一次」：UILabel 的宽度要等 NGUI 排完版才知道，
/// 而角标是在 <c>Setting.initialize()</c>（同一帧更早）里造的，那一刻读到的宽度还是
/// 克隆来源（spyer_）的旧值。挂在角标自己身上就地解决，也不需要在窗口里加一个 Update。
///
/// 开销可以忽略：角标在 OCG 下是 <c>SetActive(false)</c>，停用的 GameObject 不跑 Update。
/// </summary>
public class RdBadgeFollower : MonoBehaviour
{
    /// <summary>对齐用的标签（角标与它同一个父节点，所以直接借用它的局部坐标）。</summary>
    public UILabel anchor;

    /// <summary>与文字右边缘的间距。</summary>
    public float gap = 8f;

    /// <summary>相对文字基线上抬多少（角标式，略微上飘）。</summary>
    public float lift = 6f;

    void LateUpdate()
    {
        if (anchor == null)
        {
            return;
        }
        // 锚点是 UILabel（不是 Transform）：位置得从它的 transform 取 ——
        // UILabel 上没有 localPosition 这个成员（写 anchor.localPosition 编译不过）。
        Vector3 ap = anchor.transform.localPosition;
        transform.localPosition = new Vector3(
            ap.x + anchor.width + gap,
            ap.y + lift,
            0f);
    }
}
