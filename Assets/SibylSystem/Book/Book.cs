using UnityEngine;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using YGOSharp.OCGWrapper.Enums;

public class Book : WindowServant2D
{
    string kacha = "";
    string changcha = "";
    string xuecha = "";
   
    lazyBookbtns texts;
    public override void initialize()
    {
        kacha = InterString.Get("卡差:");
        changcha = InterString.Get("场差:");
        xuecha = InterString.Get("血差:");
        gameObject = createWindow(this, Program.I().new_ui_book);
        texts = gameObject.GetComponentInChildren<lazyBookbtns>();
        texts.textlist.scrollValue = 1;
        texts.textlist.lockDrag = true;
        UIHelper.registEvent(gameObject, "exit_", hide);
        applyHideArrangement();
    }

    string formatS(string from, int c,bool k)
    {
        string returnValue = "";    
        if (k)  
        {
            return from + c.ToString();
        }
        if (c < 0)
        {
            returnValue = from + "[ff0000]" + c.ToString() + "[-]";
        }
        else
        {
            returnValue = from + "[00ff00]" + c.ToString() + "[-]";
        }
        return returnValue;
    }

    public override void preFrameFunction()
    {
        base.preFrameFunction();
        if (isShowed)
        {
            gameObject.transform.position = Program.camera_main_2d.ScreenToWorldPoint(new Vector3(Program.I().cardDescription.width / 2, (Screen.height-Program.I().cardDescription.cHeight) / 2, 0));
            texts.back.width = (int)Program.I().cardDescription.width;
            texts.back.height = Screen.height - (int)Program.I().cardDescription.cHeight;
        }
    }

    public override void applyShowArrangement()
    {
        if (gameObject != null)
        {
            gameObject.SetActive(true);
        }
    }

    public override void applyHideArrangement()
    {
        if (gameObject != null)
        {
            gameObject.SetActive(false);
        }
    }

    public override void hide()
    {
        base.hide();
        Program.notGo(fixScreenProblem);
        fixScreenProblem();
    }

    public override void show()
    {
        base.show();
        Program.I().cardDescription.shiftCardShower(true);
        Program.notGo(fixScreenProblem);
        fixScreenProblem();
        realize();
    }
    public UILabel lab = null;
    public UILabel labop = null;    

    public string deckString = "";
    public string opString = "";    

    /// <summary>最近一次 deckMemoByCode() 是否真的走了「按 code 精确减」那条路（false = 退回了上游牌名匹配）。</summary>
    public static bool lastUsedExactCode = false;

    /// <summary>最近一次 deckMemoByCode() 留在卡组里那些名额的下标（下标即 ydk 原始顺序）。仅供排查用。</summary>
    public static List<int> lastLeftIndexes = new List<int>();

    /// <summary>最近一次 deckMemoByCode() 剔掉的「卡库里按原码查不到」的 code（逗号分隔）。仅供排查用。</summary>
    public static string lastDroppedCodes = "";

    /// <summary>
    /// 「卡组还剩什么」的文本，**按 ydk 原始顺序**（同名合并成 名称*N，与上游格式一致）。
    /// 拿不到 ydk 原始序时返回 null，调用方退回上游那套牌名匹配。
    ///
    /// 与上游有两点差别：
    ///
    /// 一、**剔掉卡库里按原码查不到的条目**。这类条目两头都是错的：
    ///   ① 服务器加载卡组时会把它丢掉 —— 实测「垢轴」里的 101306061 不在 cdb 里，
    ///      服务器只装了 10 张（MSG_START 报 deck=10），于是卡组里算出 6 张、场上只有 5 张；
    ///   ② 客户端 CardsManager.Get() 有「id - 0..9」的**别名兜底**，会给它安上别的卡的**名字**，
    ///      于是浮标上会出现一张「名字是真的、牌却不存在」的卡。
    ///   判据必须用 GetCard（按原码精确查）而不是 Get（带兜底）—— 正是靠这个区别才认出它来。
    ///
    /// 二、减法按 **code 精确减**，不按牌名。上游是拿离场那张牌的**牌名**去 MultiStringMaster
    ///   里找，而 MultiStringMaster.remove() 用的是 `str.Replace(名字,"miaowu") != str` 这种
    ///   **子串**判断、命中还取最后一个匹配项 —— 卡名互为子串时就会减错条目。
    ///   例：卡组里同时有「贪欲之壶」和「强欲而贪欲之壶」，前者离场时可能把后者那份减掉。
    ///
    /// 起点直接用 TcpHelper.deck.Main —— 它就是上报时那份 ydk 主卡组 code 列表，顺序即 ydk 原始顺序：
    ///   ① 起手：整份主卡组，全部算「还在卡组里」（先剔掉卡库里没有的）；
    ///   ② 我方侧每张「已不在卡组/检视区/未知区、且已知是什么」的牌，各占掉一个同 code 名额
    ///      （按列表顺序取第一个还没被占的，所以同名牌被消耗的顺序不影响显示）；
    ///   ③ 没被占掉的名额，按原顺序输出。
    /// 额外卡组的牌不在 Main 里，code 自然对不上 → 不会被误减，这是白拿的。
    /// </summary>
    public static string deckMemoByCode()
    {
        lastUsedExactCode = false;
        lastLeftIndexes.Clear();
        lastDroppedCodes = "";
        if (TcpHelper.deck == null || TcpHelper.deck.Main == null)
        {
            return null;
        }
        IList<int> order = TcpHelper.deck.Main;
        if (order.Count == 0 || TcpHelper.deckStrings.Count != order.Count)
        {
            return null;
        }
        List<int> left = memoRemainingIndexes(order, out lastDroppedCodes);
        if (left == null)
        {
            return null;
        }
        MultiStringMaster master = new MultiStringMaster();
        for (int k = 0; k < left.Count; k++)
        {
            master.Add(TcpHelper.deckStrings[left[k]]);
            lastLeftIndexes.Add(left[k]);
        }
        lastUsedExactCode = true;
        return master.managedString.TrimEnd('\n');
    }

    /// <summary>
    /// 记牌的**数据层核心**：在 <paramref name="order"/>（本局主卡组的有序 code 表）上，
    /// 划掉「我方已经不在卡组里」的那些名额，返回**没被划掉的那些名额在 order 里的下标**（升序）。
    ///
    /// 返回值是**下标**而不是 code：调用方要的是「哪些名额还在、而且按原顺序」，
    /// 拿下标既能还原 code（order[idx]）也能拿到卡名（deckStrings[idx]，同长同序）。
    ///
    /// 拿不到可用的顺序表（order 为空）时返回 null，调用方应当退回上游的牌名匹配。
    ///
    /// 算法两步（见 4.2，刻意**不维护增量**、每拍从现状重算）：
    ///
    /// ① **剔掉卡库里按原码查不到的条目**（用 `GetCard` 精确查，不用带别名兜底的 `Get`，见下）。
    /// ② 遍历场上所有牌，凡是「我方侧、已经不在卡组/检视区/未知区、且已知是什么」的，
    ///    各占掉一个同 code 的名额（按 order 顺序取第一个还没被占的，所以同名牌消耗顺序不影响结果）。
    ///
    /// ⛔ ① 为什么必须做：`CardsManager.Get(int)` 带「id−0..id−9」的**别名兜底**，而服务器加载卡组时
    /// 会把卡库里没有的码**直接丢掉**。不剔的话会多出一个「名字是真的、牌却不存在」的名额，
    /// 且张数与场上永远差 1（实测：`deck/垢轴.ydk` 里的 101306061）。
    /// 剔了之后，我们调用的 `Get(code)` 必然是精确命中，也不会污染缓存里的卡对象
    /// （别名兜底那条路会 `data.Id = code` 改写共享缓存对象的 Id，是另一个坑）。
    ///
    /// ⛔ `Search` 区必须跳过：那是「从卡组检视/选牌」的临时区，牌其实还在卡组里。
    ///
    /// ⛔ 判「是不是我方的牌」用 `controllerBased`（与上游一致）；转视角（`GCS_swapALL`）会把它
    /// 整体翻转，所以调用方必须在 `gameInfo.swaped == true` 时根本不调用本函数。
    /// </summary>
    public static List<int> memoRemainingIndexes(IList<int> order, out string dropped, List<string> consumed = null)
    {
        dropped = "";
        if (order == null || order.Count == 0)
        {
            return null;
        }
        List<int> live = new List<int>();
        for (int j = 0; j < order.Count; j++)
        {
            if (YGOSharp.CardsManager.GetCard(order[j]) == null)
            {
                dropped += (dropped.Length == 0 ? "" : ",") + order[j];
                continue;
            }
            live.Add(j);
        }
        bool[] gone = new bool[live.Count];
        foreach (var item in Program.I().ocgcore.cards)
        {
            if (item.p.location == (UInt32)CardLocation.Search)
            {
                continue;
            }
            if (item.p.location == (UInt32)CardLocation.Unknown)
            {
                continue;
            }
            if (item.p.location == (UInt32)CardLocation.Deck)
            {
                continue;
            }
            if (item.controllerBased != 0)
            {
                continue;
            }
            int id = item.get_data().Id;
            if (id <= 0)
            {
                continue;
            }
            // 排查用：把「实际被算作已离场」的牌逐张记下来（含它在哪个区、序号、code）。
            // 调用方把它落盘，验收脚本就能拿同一份证据复算「该剩哪些」——
            // 不要在脚本里另写一套扫描条件，两边一旦不一致就会变成互相扯皮。
            if (consumed != null)
            {
                consumed.Add(((CardLocation)item.p.location) + "/" + item.p.sequence + "/" + id);
            }
            for (int k = 0; k < live.Count; k++)
            {
                if (gone[k] == false && order[live[k]] == id)
                {
                    gone[k] = true;
                    break;
                }
            }
        }
        List<int> result = new List<int>();
        for (int k = 0; k < live.Count; k++)
        {
            if (gone[k] == false)
            {
                result.Add(live[k]);
            }
        }
        return result;
    }

    public void realize()
    {
        MultiStringMaster master;

        if (lab!=null)    
        {
            // 首选「按 code 精确减 + ydk 原始顺序」（见 deckMemoByCode 的注释）；
            // 拿不到那份 ydk code 列表时（观战者、未上报卡组等）退回上游的牌名匹配。
            deckString = deckMemoByCode();
            if (deckString == null)
            {
                deckString = "";
                master = new MultiStringMaster();
                foreach (var item in TcpHelper.deckStrings)
                {
                    master.Add(item);
                }
                foreach (var item in Program.I().ocgcore.cards)
                {
                    if (item.p.location == (UInt32)CardLocation.Search)
                    {
                        continue;
                    }
                    if (item.p.location == (UInt32)CardLocation.Unknown)
                    {
                        continue;
                    }
                    if (item.p.location == (UInt32)CardLocation.Deck)
                    {
                        continue;
                    }
                    if (item.get_data().Id <= 0)
                    {
                        continue;
                    }
                    if (item.controllerBased == 0)
                    {
                        master.remove(item.get_data().Name);
                    }
                }
                deckString += master.managedString.TrimEnd('\n');
            }
            lab.text = deckString;
        }
        if (labop != null)
        {
            opString = "";
            master = new MultiStringMaster();
            foreach (var item in Program.I().ocgcore.cards)
            {
                if (item.p.location == (UInt32)CardLocation.Search)
                {
                    continue;
                }
                if (item.get_data().Id <= 0)
                {
                    continue;
                }
                if (item.controllerBased == 1)
                {
                    master.Add(item.get_data().Name);
                }
            }
            opString += master.managedString.TrimEnd('\n');
            if (Program.I().ocgcore.cantCheckGrave)
            {
                labop.text = InterString.Get("不能查看对手使用过的卡");
            }
            else if (master.strings.Count > 0)
            {
                labop.text = InterString.Get("[ff5555]对手使用过：@n[?][-]", opString);
            }
            else
            {
                labop.text = InterString.Get("请等待对手出牌来获取情报");
            }
        }

        if (isShowed == false)
        {
            return;
        }



        int[] fieldCards = new int[2] { 0, 0 };
        int[] handCards = new int[2] { 0, 0 };
        int[] resourceCards = new int[2] { 0, 0 };
        bool died = false;
        foreach (var item in Program.I().ocgcore.cards) 
        {
            if (item.p.location == (UInt32)CardLocation.Search)
            {
                continue;
            }
            if (item.p.location == (UInt32)CardLocation.Unknown)
            {
                continue;
            }
            for (int i = 0; i < 2; i++) 
            {
                if (item.p.controller == i)
                {
                    if (item.p.location == (UInt32)CardLocation.MonsterZone || item.p.location == (UInt32)CardLocation.SpellZone)
                    {
                        fieldCards[i]++;
                    }
                    if (item.p.location == (UInt32)CardLocation.Hand)
                    {
                        handCards[i]++;
                    }
                    if (item.p.location == (UInt32)CardLocation.Grave || item.p.location == (UInt32)CardLocation.Removed)
                    {
                        resourceCards[i]++;
                    }
                }
            }
        }
        if (!died)
        {

            texts.lable.text =InterString.Get("消息记录")+"\n"+
    formatS(kacha, (fieldCards[0] + handCards[0]) - (fieldCards[1] + handCards[1]), false) + " " +
    formatS(changcha, (fieldCards[0]) - (fieldCards[1]), false) + " " +
    formatS(xuecha,

    Program.I().ocgcore.gameInfo.swaped
    ?
    Program.I().ocgcore.life_1 - Program.I().ocgcore.life_0
    :
    Program.I().ocgcore.life_0 - Program.I().ocgcore.life_1

    , false);
        }
        string all = "";
        foreach (var item in lines)
        {
            all += item + "\n";
        }
        try
        {
            all = all.Substring(0, all.Length - 1);
        }
        catch (System.Exception e)
        {
        }
        try
        {
            texts.textlist.Clear();
            texts.textlist.Add(all);
        }
        catch (Exception)
        {
            Program.DEBUGLOG("NO LableList");
        }
    }

    List<string> lines = new List<string>();

    public void add(string str)
    {
        lines.Add(str);
        realize();
    }

    public void clear()
    {
        lines.Clear();
        realize();
    }

}
