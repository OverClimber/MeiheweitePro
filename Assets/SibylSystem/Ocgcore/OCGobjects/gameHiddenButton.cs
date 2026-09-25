using System;
using UnityEngine;
using YGOSharp.OCGWrapper.Enums;

public class gameHiddenButton : OCGobject
{
    public CardLocation location;

    public int player;

    public TextMaster hintText;

    GPS ps;

    public gameHiddenButton(CardLocation l, int p)
    {
        ps = new GPS();
        ps.controller = (UInt32)p;
        ps.location = (UInt32)l;
        ps.position = 0;
        ps.sequence = 0;
        Program.I().ocgcore.AddUpdateAction_s(Update);
        player = p;
        location = l;
        gameObject = create(Program.I().mod_ocgcore_hidden_button);
    }

    public void dispose()
    {
        Program.I().ocgcore.RemoveUpdateAction_s(Update);
    }

    bool excited = false;

    /// <summary>上一次落盘「我方卡组区域屏幕坐标」的时刻（毫秒），用于每帧节流。</summary>
    int lastDeckPosDumpMs = -1;

    public void Update()
    {
        if (gameObject != null)
        {
            gameObject.transform.position = Program.I().ocgcore.get_point_worldposition(ps);
            // 排查用：八个「区域热区」的屏幕坐标，每个每 2 秒报一次。
            // 验收「悬停卡组 → 列出剩余」必须真把光标挪到这个区域上，坐标不能靠猜；
            // 三台相机各报一份，脚本取哪台对得上就用哪台（对局里的点用哪台相机换算有前车之鉴，
            // 见 Ocgcore.dumpUndoButtonPositions 的注释）。
            // 注意 Program.pointedGameObject 的射线用的是 Camera.main（Program.cs:1201），
            // 所以正常情况下 `main=` 那一份才是准的。
            if (QuickTestTrace.Enabled && Program.I().ocgcore.condition == Ocgcore.Condition.duel
                && Program.TimePassed() - lastDeckPosDumpMs > 2000)
            {
                lastDeckPosDumpMs = Program.TimePassed();
                Vector3 wp = gameObject.transform.position;
                Vector3 ca = Camera.main.WorldToScreenPoint(wp);
                Vector3 cb = Program.camera_back_ground_2d.WorldToScreenPoint(wp);
                Vector3 cc = Program.camera_main_2d.WorldToScreenPoint(wp);
                QuickTestTrace.Log("pos", location + "_" + player
                    + " main=(" + Mathf.RoundToInt(ca.x) + "," + Mathf.RoundToInt(Screen.height - ca.y) + ")"
                    + " bg=(" + Mathf.RoundToInt(cb.x) + "," + Mathf.RoundToInt(Screen.height - cb.y) + ")"
                    + " main2d=(" + Mathf.RoundToInt(cc.x) + "," + Mathf.RoundToInt(Screen.height - cc.y) + ")"
                    + " screenH=" + Screen.height
                    + " world=(" + wp.x.ToString("0.0") + "," + wp.y.ToString("0.0") + "," + wp.z.ToString("0.0") + ")");
            }
            if (Program.pointedGameObject == gameObject)
            {
                if (excited == false)
                {
                    QuickTestTrace.Log("hidden", "excite loc=" + location + " player=" + player);
                    excite();
                }
                if (Program.InputGetMouseButtonUp_0)
                {
                    showAll();
                }
            }
            else
            {
                if (excited == true)
                {
                    excited = false;
                    calm();
                }
            }
        }
    }

    void showAll()
    {
        if (location == CardLocation.Grave && Program.I().ocgcore.cantCheckGrave)
        {
            Program.I().cardDescription.RMSshow_none(InterString.Get("不能确认墓地里的卡"));
            return;
        }
        bool allShow = true;
        for (int i = 0; i < Program.I().ocgcore.cards.Count; i++) if (Program.I().ocgcore.cards[i].gameObject.activeInHierarchy)
            {
                if ((Program.I().ocgcore.cards[i].p.location & (UInt32)location) > 0)
                {
                    if (Program.I().ocgcore.cards[i].p.controller == player)
                    {
                        if (Program.I().ocgcore.cards[i].isShowed == false)
                        {
                            if (Program.I().ocgcore.cards[i].prefered == true)
                            {
                                allShow = false;
                            }
                        }
                    }
                }
            }
        for (int i = 0; i < Program.I().ocgcore.cards.Count; i++) if (Program.I().ocgcore.cards[i].gameObject.activeInHierarchy)
            {
                if ((Program.I().ocgcore.cards[i].p.location & (UInt32)location) > 0)
                {
                    if (Program.I().ocgcore.cards[i].p.controller == player)
                    {
                        if (allShow)
                        {
                            Program.I().ocgcore.cards[i].isShowed = true;
                        }
                        else
                        {
                            if (Program.I().ocgcore.cards[i].prefered == true)
                            {
                                Program.I().ocgcore.cards[i].isShowed = true;
                            }
                        }
                    }
                }
            }
        Program.I().ocgcore.realize();
        Program.I().ocgcore.toNearest();
        Program.I().audio.clip = Program.I().zhankai;
        Program.I().audio.Play();
    }

    void calm()
    {
        if (player == 0)
        {
            if (location == CardLocation.Deck)
            {
                // 只有真的摆过记牌浮标时才走这条早退路径。
                // 上游这里是无条件 return，靠 `!InAI` 那道门保证「AI 局从不摆浮标，所以一定要
                // 落到下面把扇形展开的卡收回去」。现在 AI 局也会摆浮标了，判据必须改成
                // 「浮标在不在」—— 没摆过（deckStrings 为空、转视角中等）就继续往下走。
                if (Program.I().book.lab != null)
                {
                    QuickTestTrace.Log("memo", "hover my deck: label destroyed");
                    destroy(Program.I().book.lab.gameObject);
                    Program.I().book.lab = null;
                    return;
                }
            }
        }
        if (player == 1)
        {
            if (location == CardLocation.Deck)
            {
                if (Program.I().book.labop != null)
                {
                    destroy(Program.I().book.labop.gameObject);
                    Program.I().book.labop = null;
                }
                return;
            }
        }
        for (int i = 0; i < Program.I().ocgcore.cards.Count; i++) if (Program.I().ocgcore.cards[i].gameObject.activeInHierarchy)
            {
            if ((Program.I().ocgcore.cards[i].p.location & (UInt32)location) > 0)
            {
                if (Program.I().ocgcore.cards[i].p.controller == player && Program.I().ocgcore.cards[i].isShowed == false)
                {
                    Program.I().ocgcore.cards[i].ES_safe_card_move_to_original_place();
                }
            }
        }
        if (hintText != null)
        {
            hintText.dispose();
            hintText = null;
        }
    }

    /// <summary>
    /// 「悬停我方卡组 → 鼠标旁列出卡组还剩什么」这套浮标的开关。
    ///
    /// 上游条件写的是 `condition==duel &amp;&amp; InAI==false &amp;&amp; room.mode!=2`。这里去掉 `!InAI`，
    /// 但要说清楚：**不是因为「AI 局本来被挡住」**——实测恰恰相反，AI.Server 局里
    /// `InAI` 恒为 false（探针实测 `InAI=False mode=0 cond=duel`；只有老的 `precy.cs`
    /// 内嵌 Percy AI 才置 true，见 precy.cs:102），所以上游那道门对 AI 局从来没关过，
    /// 「悬停看剩余」在 AI 局一直是可用的。去掉它只是把「AI 局也要开」写成显式意图，
    /// 免得以后有人把 InAI 误读成「这一局是不是 AI 局」。
    /// （⚠ 相关但不同的一件事：AI 局的 `TcpHelper.deckStrings` 确实是齐的 ——
    ///  AI 局走 Room.quickStartFlow，同样会发 CtosMessage_UpdateDeck。）
    ///
    /// 真正的判据是下面三条，排除两种情形：
    /// ・双打（room.mode==2）：我方/对方的归属与视角都会错位，不动；
    /// ・转换视角中（gameInfo.swaped）：controllerBased 被整体翻转（Ocgcore.cs:9300），
    ///   而 Book.realize() 是按 controllerBased==0 去减 deckStrings 的 —— 转视角后它会
    ///   拿对手的牌去减我的卡组，列出来的东西整个是错的。宁可不显示。
    /// </summary>
    static bool deckMemoAvailable()
    {
        if (Program.I().ocgcore.condition != Ocgcore.Condition.duel)
        {
            return false;
        }
        if (Program.I().room.mode == 2)
        {
            return false;
        }
        if (Program.I().ocgcore.gameInfo.swaped)
        {
            return false;
        }
        return true;
    }

    /// <summary>
    /// 排查用：把刚算出来的「剩余卡组」摘要落盘，供验收脚本对账
    /// （distinct/total 是浮标上真正写出来的东西，deckNow 是场上我方卡组区域里实际还有几张）。
    /// </summary>
    void traceDeckMemo()
    {
        if (!QuickTestTrace.Enabled)
        {
            return;
        }
        string s = Program.I().book.deckString;
        string[] ls = s.Split('\n');
        int distinct = 0;
        int total = 0;
        for (int i = 0; i < ls.Length; i++)
        {
            if (ls[i].Length == 0)
            {
                continue;
            }
            distinct++;
            int star = ls[i].LastIndexOf('*');
            int n = 1;
            if (star > 0 && int.TryParse(ls[i].Substring(star + 1), out n) == false)
            {
                n = 1;
            }
            total += n;
        }
        int deckNow = 0;
        int deckKnown = 0;
        for (int i = 0; i < Program.I().ocgcore.cards.Count; i++)
        {
            if ((Program.I().ocgcore.cards[i].p.location & (UInt32)CardLocation.Deck) > 0
                && Program.I().ocgcore.cards[i].p.controller == 0)
            {
                deckNow++;
                if (Program.I().ocgcore.cards[i].get_data().Id > 0)
                {
                    deckKnown++;
                }
            }
        }
        bool exact = false;
        if (TcpHelper.deck != null && TcpHelper.deck.Main != null)
        {
            exact = (TcpHelper.deck.Main.Count == TcpHelper.deckStrings.Count);
        }
        QuickTestTrace.Log("memo", "hover my deck: deckStrings=" + TcpHelper.deckStrings.Count
            + " distinct=" + distinct + " total=" + total
            + " deckNow=" + deckNow + " deckKnown=" + deckKnown
            + " exactByCode=" + exact + " usedExact=" + Book.lastUsedExactCode
            + " InAI=" + Program.I().ocgcore.InAI
            + " mode=" + Program.I().room.mode
            + " swaped=" + Program.I().ocgcore.gameInfo.swaped
            + " cond=" + Program.I().ocgcore.condition
            + " head=" + (ls.Length > 0 && ls[0].Length > 0 ? ls[0] : "(empty)"));

        // 顺序审计（只在 log/qt_memo_order.on 存在时落盘，默认不写）：
        //   ordercodes = TcpHelper.deck.Main，即上报时那份 ydk 主卡组 code 列表，顺序就是 ydk 原始顺序；
        //   leftidx    = 还留在卡组里的名额下标（升序）。
        // 验收脚本拿 ordercodes 跟 deck/<卡组>.ydk 的 main 段逐项比对，就能把
        // 「呈现顺序 = ydk 原始顺序」钉死，不必靠肉眼看浮标。
        if (QuickTestTrace.SwitchOn("qt_memo_order.on") && exact)
        {
            string codes = "";
            for (int i = 0; i < TcpHelper.deck.Main.Count; i++)
            {
                codes += (i == 0 ? "" : ",") + TcpHelper.deck.Main[i];
            }
            string idx = "";
            for (int i = 0; i < Book.lastLeftIndexes.Count; i++)
            {
                idx += (i == 0 ? "" : ",") + Book.lastLeftIndexes[i];
            }
            QuickTestTrace.Log("memo", "ordercodes " + codes);
            QuickTestTrace.Log("memo", "leftidx " + idx);
            string shown = "";
            for (int i = 0; i < ls.Length; i++)
            {
                shown += (i == 0 ? "" : "|") + ls[i];
            }
            QuickTestTrace.Log("memo", "lines " + shown);
            QuickTestTrace.Log("memo", "dropped " + (Book.lastDroppedCodes.Length > 0 ? Book.lastDroppedCodes : "(none)"));

            // 明细：我方每张牌在哪个区（用来追「对不上」的那点差额到底差在哪）
            int hand = 0, grave = 0, removed = 0, extra = 0, field = 0, unknown = 0, search = 0, deck = 0;
            string det = "";
            for (int i = 0; i < Program.I().ocgcore.cards.Count; i++)
            {
                var it = Program.I().ocgcore.cards[i];
                if (it.controllerBased != 0)
                {
                    continue;
                }
                uint L = it.p.location;
                string zone;
                if ((L & (UInt32)CardLocation.Deck) > 0) { deck++; zone = "Deck"; }
                else if (L == (UInt32)CardLocation.Hand) { hand++; zone = "Hand"; }
                else if (L == (UInt32)CardLocation.Grave) { grave++; zone = "Grave"; }
                else if (L == (UInt32)CardLocation.Removed) { removed++; zone = "Removed"; }
                else if (L == (UInt32)CardLocation.Extra) { extra++; zone = "Extra"; }
                else if (L == (UInt32)CardLocation.Unknown) { unknown++; zone = "Unknown"; }
                else if (L == (UInt32)CardLocation.Search) { search++; zone = "Search"; }
                else { field++; zone = "Field"; }
                if (zone != "Deck")
                {
                    det += " " + zone + "/" + it.p.sequence + "/" + it.get_data().Id;
                }
            }
            QuickTestTrace.Log("memo", "mycards deck=" + deck + " hand=" + hand + " grave=" + grave
                + " removed=" + removed + " extra=" + extra + " field=" + field
                + " unknown=" + unknown + " search=" + search + det);
        }
    }

    void excite()
    {
        excited = true;
        if (location == CardLocation.Grave && Program.I().ocgcore.cantCheckGrave)
        {
            return;
        }
        YGOSharp.Card data = null;
        string tailString = "";
        uint con = 0;
        for (int i = 0; i < Program.I().ocgcore.cards.Count; i++) if (Program.I().ocgcore.cards[i].gameObject.activeInHierarchy)
            {
                if ((Program.I().ocgcore.cards[i].p.location & (UInt32)location) > 0)
                {
                    if (Program.I().ocgcore.cards[i].p.controller == player)
                    {
                        if (Program.I().ocgcore.cards[i].isShowed == false)
                        {
                            data = Program.I().ocgcore.cards[i].get_data();
                            tailString = Program.I().ocgcore.cards[i].tails.managedString;
                            con = Program.I().ocgcore.cards[i].p.controller;
                        }
                    }
                }
            }
        Program.I().cardDescription.setData(data, con == 0 ? GameTextureManager.myBack : GameTextureManager.opBack, tailString, data != null);
        if (deckMemoAvailable())
        {
            if (player == 0)
            {
                if (location == CardLocation.Deck)
                {
                    if (Program.I().book.lab != null)
                    {
                        destroy(Program.I().book.lab.gameObject);
                        Program.I().book.lab = null;
                    }

                    // 拿不到本局卡组（观战者、没上报过卡组）就不摆一个空框，
                    // 继续往下走 → 退回上游的「显示张数 + 扇形展开」表现。
                    if (TcpHelper.deckStrings.Count <= 0)
                    {
                        QuickTestTrace.Log("memo", "hover my deck: no deck list -> fallback fan");
                    }
                    else
                    {
                        Program.I().book.lab = create(Program.I().New_decker, Vector3.zero, Vector3.zero, false, Program.ui_main_2d, true).GetComponent<UILabel>();
                        Program.I().book.realize();


                        Vector3 screenPosition = Input.mousePosition;
                        screenPosition.x -= 90;
                        screenPosition.y += Program.I().book.lab.height/4;
                        screenPosition.z = 0;
                        Vector3 worldPositin = Program.camera_main_2d.ScreenToWorldPoint(screenPosition);
                        Program.I().book.lab.transform.position = worldPositin;

                        traceDeckMemo();
                        return;
                    }
                }
            }
        }


        if (player == 1)
        {
            if (location == CardLocation.Deck)
            {
                if (Program.I().book.labop != null)
                {
                    destroy(Program.I().book.labop.gameObject);
                    Program.I().book.labop = null;
                }


                Program.I().book.labop = create(Program.I().New_decker, Vector3.zero, Vector3.zero, false, Program.ui_main_2d, true).GetComponent<UILabel>();
                Program.I().book.realize();


                Vector3 screenPosition = Input.mousePosition;
                screenPosition.x -= 90;
                screenPosition.y -= Program.I().book.labop.height / 4;
                screenPosition.z = 0;
                Vector3 worldPositin = Program.camera_main_2d.ScreenToWorldPoint(screenPosition);
                Program.I().book.labop.transform.position = worldPositin;

                return;
            }
        }

        int count = 0;
        for (int i = 0; i < Program.I().ocgcore.cards.Count; i++)if(Program.I().ocgcore.cards[i].gameObject.activeInHierarchy)
        {
            if ((Program.I().ocgcore.cards[i].p.location & (UInt32)location) > 0)
            {
                if (Program.I().ocgcore.cards[i].p.controller == player)
                {
                    count++;
                }
            }
        }
        int count_show = 0;
        for (int i = 0; i < Program.I().ocgcore.cards.Count; i++) if (Program.I().ocgcore.cards[i].gameObject.activeInHierarchy)
            {
            if ((Program.I().ocgcore.cards[i].p.location & (UInt32)location) > 0)
            {
                if (Program.I().ocgcore.cards[i].p.controller == player && Program.I().ocgcore.cards[i].isShowed == false)
                {
                    count_show++;
                }
            }
        }
        if (hintText != null)
        {
            hintText.dispose();
            hintText = null;
        }
        if (count > 0)
        {
            hintText = new TextMaster(count.ToString(), Input.mousePosition, false);
        }
        Vector3 qidian = new Vector3(Input.mousePosition.x, Input.mousePosition.y, 12f);
        Vector3 zhongdian = new Vector3(2f * Program.I().ocgcore.getScreenCenter() - Input.mousePosition.x, Input.mousePosition.y, 19f);
        int i_real = 0;
        for (int i = 0; i < Program.I().ocgcore.cards.Count; i++) if (Program.I().ocgcore.cards[i].gameObject.activeInHierarchy)
            {
                if ((Program.I().ocgcore.cards[i].p.location & (UInt32)location) > 0)
                {
                    if (Program.I().ocgcore.cards[i].p.controller == player)
                    {
                        if (Program.I().ocgcore.cards[i].isShowed == false)
                        {
                            Vector3 screen_vector_to_move = Vector3.zero;
                            int gezi = 8;
                            if (count_show > 8)
                            {
                                gezi = count_show;
                            }
                            int index = count_show - 1 - i_real;
                            i_real++;
                            screen_vector_to_move =
                                                (new Vector3(0, 50f * (float)Math.Sin(((float)index / (float)count)  * 3.1415926f), 0))
                                                +
                                                qidian
                                                +
                                                ((float)index / (float)(gezi - 1)) * (zhongdian - qidian);
                            //iTween.MoveTo(Program.I().ocgcore.cards[i].gameObject, Camera.main.ScreenToWorldPoint(screen_vector_to_move), 0.5f);
                            //iTween.RotateTo(Program.I().ocgcore.cards[i].gameObject, new Vector3(-30, 0, 0), 0.1f);
                            Program.I().ocgcore.cards[i].TweenTo(Camera.main.ScreenToWorldPoint(screen_vector_to_move), new Vector3(Program.tableauAngle(-30f), 0, 0),true);
                        }
                    }
                }
            }
        if (count_show > 0)
        {
            Program.I().audio.clip = Program.I().zhankai;
            Program.I().audio.Play();
        }
    }
}
