using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;
using YGOSharp.OCGWrapper.Enums;

public class selectDeck : WindowServantSP
{

    UIselectableList superScrollView = null;
    UIInput searchInput = null;
    UIDeckPanel deckPanel = null;

    string sort = "sortByTimeDeck";


    cardPicLoader[] quickCards = new cardPicLoader[200];

    public override void initialize()
    {
        createWindow(Program.I().remaster_deckManager);
        deckPanel = gameObject.GetComponentInChildren<UIDeckPanel>();
        UIHelper.registEvent(gameObject, "exit_", onClickExit);
        superScrollView = gameObject.GetComponentInChildren<UIselectableList>();
        superScrollView.selectedAction = onSelected;
        UIHelper.registEvent(gameObject, "sort_", onSort);
        setSortLable();
        UIHelper.registEvent(gameObject, "edit_", onEdit);
        UIHelper.registEvent(gameObject, "new_", onNew);
        UIHelper.registEvent(gameObject, "dispose_", onDispose);
        UIHelper.registEvent(gameObject, "copy_", onCopy);
        UIHelper.registEvent(gameObject, "rename_", onRename);
        UIHelper.registEvent(gameObject, "code_", onCode);
        searchInput = UIHelper.getByName<UIInput>(gameObject, "search_");
        superScrollView.install();
        for (int i = 0; i < quickCards.Length; i++)
        {
            quickCards[i] = deckPanel.createCard();
            quickCards[i].relayer(i);
        }
        SetActiveFalse();

    }

    void onSearch()
    {
        printFile();
        superScrollView.toTop();
    }

    void onEdit()
    {
        if (!superScrollView.Selected())
        {
            return;
        }
        if (!isShowed)
        {
            return;
        }
        KF_editDeck(superScrollView.selectedString);
    }

    void returnToSelect()
    {
        QuickTestTrace.Log("home", "returnToSelect -> selectDeck");
        Program.I().shiftToServant(Program.I().selectDeck);
    }

    string preString = "";

    /// <summary>上一次 dump 按钮坐标的时刻（节流用；Program.TimePassed 是毫秒）。</summary>
    private int lastBtnDumpMs = -1;

    /// <summary>
    /// 排查用：卡组列表窗口几个按钮的屏幕坐标。
    ///
    /// 存在的理由与编辑器那份完全一样（<c>DeckManager.dumpEditDeckButtonPositions</c>）：
    /// 窗口是 iTween 滑入的，show() 当刻读到的是半路位置，脚本照那坐标点必然点空；
    /// 这里由每帧节流调用，读到的永远是停稳之后的位置。相机同样必须用
    /// camera_back_ground_2d（这张窗口挂在它下面），换成 camera_main_2d 会算到屏幕外。
    /// </summary>
    private void dumpSelectDeckButtonPositions()
    {
        if (!QuickTestTrace.Enabled || Program.camera_back_ground_2d == null)
        {
            return;
        }
        string[] names = new string[] { "exit_", "edit_", "new_", "dispose_", "copy_", "rename_", "sort_" };
        for (int i = 0; i < names.Length; i++)
        {
            UIButton b = UIHelper.getByName<UIButton>(gameObject, names[i]);
            if (b == null)
            {
                QuickTestTrace.Log("btnpos", "selectdeck " + names[i] + " = null");
                continue;
            }
            Vector3 sp = Program.camera_back_ground_2d.WorldToScreenPoint(b.transform.position);
            QuickTestTrace.Log("btnpos", "selectdeck " + names[i]
                + " screen=(" + Mathf.RoundToInt(sp.x) + ","
                + Mathf.RoundToInt(Screen.height - sp.y) + ")"
                + " active=" + b.gameObject.activeInHierarchy
                + " enabled=" + b.isEnabled);
        }
    }

    public override void preFrameFunction()
    {
        base.preFrameFunction();
        Menu.checkCommend();
        if (QuickTestTrace.Enabled && isShowed && Program.TimePassed() - lastBtnDumpMs > 1000)
        {
            lastBtnDumpMs = Program.TimePassed();
            dumpSelectDeckButtonPositions();
        }
        if (searchInput.value != preString)
        {
            preString = searchInput.value;
            onSearch();
        }
    }

    /// <summary>
    /// 装好卡组编辑器的「返回」动作：卡组有改动就先问要不要保存，否则回卡组列表。
    ///
    /// 两条进入编辑器的路必须装同一个动作 ——
    ///   ① 从卡组列表点「编辑」（<see cref="KF_editDeck"/>）；
    ///   ② 测试局打完/认输后由 Ocgcore.onDuelResultConfirmed 送回编辑器。
    /// 少了它 <c>DeckManager.home()</c> 就是空实现（那里是 <c>if (returnAction != null)</c>），
    /// 工具条上的返回键点了没反应，人就卡在编辑器里出不来（用户实测反馈）。
    /// </summary>
    public void armDeckEditorReturn()
    {
        ((DeckManager)Program.I().deckManager).returnAction =
            () =>
            {
                if (((DeckManager)Program.I().deckManager).deckDirty)
                {
                    RMSshow_yesOrNoOrCancle(
                          "deckManager_returnAction"
                        , InterString.Get("要保存卡组的变更吗？")
                        , new messageSystemValue { hint = "yes", value = "yes" }
                        , new messageSystemValue { hint = "no", value = "no" }
                        , new messageSystemValue { hint = "cancle", value = "cancle" }
                        );
                }
                else
                {
                    returnToSelect();
                }
            };
    }

    public void KF_editDeck(string deckName)
    {
        string path = GameModeManager.DeckPath(deckName);
        if (File.Exists(path))
        {
            GameModeManager.SetDeckInUse(deckName);
            ((DeckManager)Program.I().deckManager).shiftCondition(DeckManager.Condition.editDeck);
            Program.I().shiftToServant(Program.I().deckManager);
            ((DeckManager)Program.I().deckManager).loadDeckFromYDK(path);
            ((CardDescription)Program.I().cardDescription).setTitle(deckName);
            ((DeckManager)Program.I().deckManager).setGoodLooking();
            armDeckEditorReturn();
        }
    }

    public override void ES_RMS(string hashCode, List<messageSystemValue> result)
    {
        base.ES_RMS(hashCode, result);
        if (hashCode == "deckManager_returnAction")
        {
            if (result[0].value == "yes")
            {
                if (Program.I().deckManager.onSave())
                {
                    returnToSelect();
                }
            }
            if (result[0].value == "no")
            {
                returnToSelect();
            }
        }
        if (hashCode == "onNew")
        {
            try
            {
                File.Create(GameModeManager.DeckPath(result[0].value)).Close();
                RMSshow_none(InterString.Get("「[?]」创建完毕。", result[0].value));
                superScrollView.selectedString = result[0].value;
                printFile();
            }
            catch (Exception)
            {
                RMSshow_none(InterString.Get("创建卡组失败！请检查输入的文件名，以及文件夹权限。"));
            }
        }
        if (hashCode == "onDispose")
        {
            if (result[0].value == "yes")
            {
                try
                {
                    File.Delete(GameModeManager.DeckPath(superScrollView.selectedString));
                    RMSshow_none(InterString.Get("「[?]」删除完毕。", superScrollView.selectedString));
                    printFile();
                }
                catch (Exception)
                {
                    RMSshow_none(InterString.Get("删除卡组失败！请检查文件夹权限。"));
                }
            }
        }
        if (hashCode == "onCopy")
        {
            try
            {
                File.Copy(GameModeManager.DeckPath(superScrollView.selectedString), GameModeManager.DeckPath(result[0].value));
                RMSshow_none(InterString.Get("「[?]」复制完毕。", superScrollView.selectedString));
                superScrollView.selectedString = result[0].value;
                printFile();
            }
            catch (Exception)
            {
                RMSshow_none(InterString.Get("复制卡组失败！请检查输入的文件名，以及文件夹权限。"));
            }
        }
        if (hashCode == "onRename")
        {
            try
            {
                File.Move(GameModeManager.DeckPath(superScrollView.selectedString), GameModeManager.DeckPath(result[0].value));
                RMSshow_none(InterString.Get("「[?]」重命名完毕。", superScrollView.selectedString));
                superScrollView.selectedString = result[0].value;
                printFile();
            }
            catch (Exception)
            {
                RMSshow_none(InterString.Get("重命名卡组失败！请检查输入的文件名，以及文件夹权限。"));
            }
        }
    }

    void onNew()
    {
        RMSshow_input("onNew", InterString.Get("请输入要创建的卡组名"), UIHelper.getTimeString());
    }

    void onDispose()
    {
        if (!superScrollView.Selected())
        {
            return;
        }
        string path = GameModeManager.DeckPath(superScrollView.selectedString);
        if (File.Exists(path))
        {
            RMSshow_yesOrNo(
                          "onDispose"
                        , InterString.Get("确认删除「[?]」吗？", superScrollView.selectedString)
                        , new messageSystemValue { hint = "yes", value = "yes" }
                        , new messageSystemValue { hint = "no", value = "no" }
                        );
        }
    }

    void onCopy()
    {
        if (!superScrollView.Selected())
        {
            return;
        }
        string path = GameModeManager.DeckPath(superScrollView.selectedString);
        if (File.Exists(path))
        {
            string newname = InterString.Get("[?]的副本", superScrollView.selectedString);
            string newnamer = newname;
            int i = 1;
            while (File.Exists(GameModeManager.DeckPath(newnamer)))
            {
                newnamer = newname + i.ToString();
                i++;
            }
            RMSshow_input("onCopy", InterString.Get("请输入复制后的卡组名"), newnamer);
        }
    }

    void onRename()
    {
        if (!superScrollView.Selected())
        {
            return;
        }
        string path = GameModeManager.DeckPath(superScrollView.selectedString);
        if (File.Exists(path))
        {
            RMSshow_input("onRename", InterString.Get("新的卡组名"), superScrollView.selectedString);
        }
    }

    void onCode()
    {
        if (!superScrollView.Selected())
        {
            return;
        }
        string path = GameModeManager.DeckPath(superScrollView.selectedString);
        if (File.Exists(path))
        {
            #if UNITY_EDITOR || UNITY_STANDALONE_WIN //编译器、Windows
                System.Diagnostics.Process.Start("notepad.exe", path);
            #elif UNITY_STANDALONE_OSX //Mac OS X
                System.Diagnostics.Process.Start("open", "-e " + path);
            #elif UNITY_STANDALONE_LINUX //Linux
                System.Diagnostics.Process.Start("gedit", path);
            #endif
        }
    }

    private void setSortLable()
    {
        if (Config.Get(sort,"1") == "1")
        {
            UIHelper.trySetLableText(gameObject, "sort_", InterString.Get("时间排序"));
        }
        else
        {
            UIHelper.trySetLableText(gameObject, "sort_", InterString.Get("名称排序"));
        }
    }

    private void onSort()
    {
        if (Config.Get(sort,"1") == "1")
        {
            Config.Set(sort, "0");
        }
        else
        {
            Config.Set(sort, "1");
        }
        setSortLable();
        printFile();
    }


    string deckSelected = "";
    void onSelected()
    {
        if (deckSelected == superScrollView.selectedString)
        {
            onEdit();
        }
        deckSelected = superScrollView.selectedString;
        printSelected();
    }

    private void printSelected()
    {
        GameTextureManager.clearUnloaded();
        YGOSharp.Deck deck;
        DeckManager.FromYDKtoCodedDeck(GameModeManager.DeckPath(deckSelected), out deck);
        int mainAll = 0;
        int mainMonster = 0;
        int mainSpell = 0;
        int mainTrap = 0;
        int sideAll = 0;
        int sideMonster = 0;
        int sideSpell = 0;
        int sideTrap = 0;
        int extraAll = 0;
        int extraFusion = 0;
        int extraLink = 0;
        int extraSync = 0;
        int extraXyz = 0;
        int currentIndex = 0;

        int[] hangshu = UIHelper.get_decklieshuArray(deck.Main.Count);
        foreach (var item in deck.Main)
        {
            mainAll++;
            YGOSharp.Card c = YGOSharp.CardsManager.Get(item);
            if ((c.Type & (UInt32)CardType.Monster) > 0)
            {
                mainMonster++;
            }
            if ((c.Type & (UInt32)CardType.Spell) > 0)
            {
                mainSpell++;
            }
            if ((c.Type & (UInt32)CardType.Trap) > 0)
            {
                mainTrap++;
            }
            quickCards[currentIndex].reCode(item);
            Vector2 v = UIHelper.get_hang_lieArry(mainAll - 1, hangshu);
            quickCards[currentIndex].transform.localPosition = new Vector3
                (
                -176.3f + UIHelper.get_left_right_indexZuo(0, 352f, (int)v.y, hangshu[(int)v.x],10)
                ,
                161.6f - v.x * 60f
                ,
                0
                );
            if (currentIndex <= 198)
            {
                currentIndex++;
            }
        }
        foreach (var item in deck.Side)
        {
            sideAll++;
            YGOSharp.Card c = YGOSharp.CardsManager.Get(item);
            if ((c.Type & (UInt32)CardType.Monster) > 0)
            {
                sideMonster++;
            }
            if ((c.Type & (UInt32)CardType.Spell) > 0)
            {
                sideSpell++;
            }
            if ((c.Type & (UInt32)CardType.Trap) > 0)
            {
                sideTrap++;
            }
            quickCards[currentIndex].reCode(item);
            quickCards[currentIndex].transform.localPosition = new Vector3
                (
                -176.3f + UIHelper.get_left_right_indexZuo(0, 352f, sideAll - 1, deck.Side.Count,10)
                ,
                -181.1f
                ,
                0
                );
            if (currentIndex <= 198)
            {
                currentIndex++;
            }
        }
        foreach (var item in deck.Extra)
        {
            extraAll++;
            YGOSharp.Card c = YGOSharp.CardsManager.Get(item);
            if ((c.Type & (UInt32)CardType.Fusion) > 0)
            {
                extraFusion++;
            }
            if ((c.Type & (UInt32)CardType.Synchro) > 0)
            {
                extraSync++;
            }
            if ((c.Type & (UInt32)CardType.Xyz) > 0)
            {
                extraXyz++;
            }
            if ((c.Type & (UInt32)CardType.Link) > 0)
            {
                extraLink++;
            }
            quickCards[currentIndex].reCode(item);
            quickCards[currentIndex].transform.localPosition = new Vector3
                (
                -176.3f + UIHelper.get_left_right_indexZuo(0, 352f, extraAll - 1, deck.Extra.Count, 10)
                ,
                -99.199f
                ,
                0
                );
            if (currentIndex <= 198)
            {
                currentIndex++;
            }
        }
        while (true)
        {
            quickCards[currentIndex].clear();
            if (currentIndex <= 198)
            {
                currentIndex++;
            }
            else
            {
                break;
            }
        }
        deckPanel.leftMain.text = GameStringHelper._zhukazu + mainAll;
        deckPanel.leftExtra.text = GameStringHelper._ewaikazu + extraAll;
        deckPanel.leftSide.text = GameStringHelper._fukazu + sideAll;
        deckPanel.rightMain.text = GameStringHelper._guaishou + mainMonster + " "+ GameStringHelper._mofa + mainSpell + " " + GameStringHelper._xianjing + mainTrap;
        deckPanel.rightExtra.text = GameStringHelper._ronghe + extraFusion + " " + GameStringHelper._tongtiao + extraSync + " " + GameStringHelper._chaoliang + extraXyz + " " + GameStringHelper._lianjie + extraLink;
        deckPanel.rightSide.text = GameStringHelper._guaishou + sideMonster + " " + GameStringHelper._mofa + sideSpell + " " + GameStringHelper._xianjing + sideTrap;
    }

    public override void show()
    {
        base.show();
        printFile();
        superScrollView.toTop();
        superScrollView.selectedString = GameModeManager.DeckInUse;
        printSelected();
        Program.charge();
    }

    public override void hide()
    {
        if (isShowed)
        {
            if (superScrollView.Selected())
            {
                // 只是「选中过」也要记住（不要求点进编辑器）——用户口径 2026-09-19：
                // 「打开卡组界面时自动选择到之前选过的卡组」。键按模式分家（见
                // GameModeManager.KeyDeckInUse），OCG 与 RD 各记各的。
                GameModeManager.SetDeckInUse(superScrollView.selectedString);
            }
        }
        base.hide();    
    }

    void printFile()
    {
        string deckInUse = GameModeManager.DeckInUse;
        superScrollView.clear();
        FileInfo[] fileInfos = (new DirectoryInfo(GameModeManager.DeckDir)).GetFiles();
        // 「卡组列表读的是哪个目录」在界面上看不出来（两个目录里都可能有同名卡组），
        // 所以把目录名与条数落盘 —— RD 线的判据靠这条，别去数界面上的卡组。
        QuickTestTrace.Log("mode", "decklist dir=" + GameModeManager.DeckDir
            + " mode=" + GameModeManager.ModeLabel + " n=" + fileInfos.Length
            // 判据（用户口径 2026-09-19「卡组界面要记住上次选的那副，OCG/RD 各记各的」）：
            // key 必须是本模式那一个，deckInUse 必须是本模式上次记下的名字。
            + " key=" + GameModeManager.KeyDeckInUse
            + " deckInUse=[" + deckInUse + "]");
        if (Config.Get(sort,"1") == "1")
        {
            Array.Sort(fileInfos, UIHelper.CompareTime);
        }
        else
        {
            Array.Sort(fileInfos, UIHelper.CompareName);
        }
        for (int i = 0; i < fileInfos.Length; i++)
        {
            if (fileInfos[i].Name.Length > 4)
            {
                if (fileInfos[i].Name.Substring(fileInfos[i].Name.Length - 4, 4) == ".ydk")
                {
                    if (fileInfos[i].Name.Substring(0, fileInfos[i].Name.Length - 4) == deckInUse)
                    {
                        if (searchInput.value == "" || Regex.Replace(fileInfos[i].Name, searchInput.value, "miaowu", RegexOptions.IgnoreCase) != fileInfos[i].Name)
                        {
                            superScrollView.add(fileInfos[i].Name.Substring(0, fileInfos[i].Name.Length - 4));
                        }
                    }
                }
            }
        }
        for (int i = 0; i < fileInfos.Length; i++)
        {
            if (fileInfos[i].Name.Length > 4)
            {
                if (fileInfos[i].Name.Substring(fileInfos[i].Name.Length - 4, 4) == ".ydk")
                {
                    if (fileInfos[i].Name.Substring(0, fileInfos[i].Name.Length - 4) != deckInUse)
                    {
                        if (searchInput.value == "" || Regex.Replace(fileInfos[i].Name, searchInput.value, "miaowu", RegexOptions.IgnoreCase) != fileInfos[i].Name)
                        {
                            superScrollView.add(fileInfos[i].Name.Substring(0, fileInfos[i].Name.Length - 4));
                        }
                    }
                }
            }
        }
        if (superScrollView.Selected() == false)
        {
            superScrollView.selectTop();
        }
    }

    void onClickExit()
    {
        if (Program.exitOnReturn)
            Program.I().menu.onClickExit();
        else
            Program.I().shiftToServant(Program.I().menu);
    }

}
