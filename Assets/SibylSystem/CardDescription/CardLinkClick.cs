using System;
using UnityEngine;

/// <summary>
/// 挂在卡牌说明面板那个 UILabel 上的"点链接"处理（替掉了 NGUI 示例里的 OpenURLOnClick ——
/// 那玩意儿会把链接当网址甩给浏览器）。
///
/// 说明面板里的可点语义（见 <see cref="CardTextLinker"/>）：
///   * OCG（「」口径）：点任何一段带下划线的东西，都是把「文字 + 主类掩码」送进检索窗
///     （<see cref="CardSearchWindow"/>）列出结果 —— 卡名、系列名、种类词、组合种类
///     （「太阳神」魔法·陷阱卡）、整句（"有X的卡名记述"）全都一样。
///     卡名也不再"点了直接切左侧面板"（用户要求 2026-10-02）：
///     同名异画的两张卡会一起列出来，而且名字留在输入框里可以再改。
///   * RD（括号口径，用户要求 2026-10-02）：点 `怪兽（银河族）` / `怪兽（攻击力1000以下）`
///     这类条件，按**条件**把符合的真卡搜出来（没有关键字，全是筛选）；
///     点 `传说卡` 三个字则搜出全部传说卡。见 <see cref="CardSearchWindow.ShowRd"/>。
/// </summary>
public class CardLinkClick : MonoBehaviour
{
    void OnClick()
    {
        HandleClick();
    }

    /// <summary>
    /// <see cref="OnClick"/> 的实体。做成 public 是给**验收探针**用的：
    /// 合成鼠标事件送不进这个客户端（见 notes/harness.md），所以探针只能
    /// 自己摆好 <c>UICamera.lastWorldPosition</c> 再直接调这一下。
    /// </summary>
    public void HandleClick()
    {
        try
        {
            UILabel label = GetComponent<UILabel>();
            int ordinal;
            string payload = CardTextLinker.ResolveClick(label, UICamera.lastWorldPosition, out ordinal);
            if (string.IsNullOrEmpty(payload))
            {
                return;
            }
            // 「同一处链接再点一次 = 关掉弹出的检索窗」（用户 2026-10-03）。
            // ⛔ 只对**会弹检索窗**的那几类链接做 —— 卡名那种是切说明面板，没有窗可关；
            // ⛔ 顺序号拿不到（-1）时不做 —— 判不出点的是哪一处，宁可多开一次也别误关。
            if (ordinal >= 0 && OpensSearchWindow(payload[0])
                && CardSearchWindow.ToggleSameLink(LinkKey(ordinal)))
            {
                return;   // 这一击是关窗，别再往下派发
            }
            CardLinkRouter.Handle(payload);
        }
        catch (Exception e)
        {
            Program.DEBUGLOG(e);
        }
    }

    /// <summary>这一击是不是"会弹出检索窗"的那类链接（只有它们才有"关窗"可言）。</summary>
    static bool OpensSearchWindow(char kind)
    {
        return kind == CardTextLinker.KindQuery
            || kind == CardTextLinker.KindTyped
            || kind == CardTextLinker.KindField
            || kind == CardTextLinker.KindName      // 卡名精确检索（用户 2026-10-03）
            || kind == CardTextLinker.KindRecord    // 「X」的卡名记述（用户 2026-10-03）
            || kind == CardTextLinker.KindUnion     // 「X」或者有那个卡名记述的…（并集）
            || kind == CardTextLinker.KindSet       // 简介系列行·单个系列名（2026-10-04）
            || kind == CardTextLinker.KindRd;
    }

    /// <summary>
    /// 「同一处链接」的钥匙 = <c>卡号#第几个链接</c>。
    ///
    /// ⛔ 卡号必须参与：说明面板那个 UILabel 是**复用**的，换卡还是同一个对象，
    ///   所以不能用 label 的实例 id 当身份 —— 那样"两张不同卡上顺序号相同的链接"会被误判成同一处。
    /// ⛔ 顺序号来自 <see cref="CardTextLinker.ResolveClick(UILabel, Vector3, out int)"/>：
    ///   它是"文本里第几个 [url="，所以同一张卡上两处不同的链接天然就不是同一处。
    /// </summary>
    static string LinkKey(int ordinal)
    {
        int id = 0;
        try
        {
            YGOSharp.Card c = Program.I() != null && Program.I().cardDescription != null
                ? Program.I().cardDescription.showingCard
                : null;
            if (c != null)
            {
                id = c.Id;
            }
        }
        catch (Exception)
        {
        }
        return id + "#" + ordinal;
    }
}

/// <summary>把 <see cref="CardTextLinker"/> 产出的 payload 派发到具体动作。</summary>
public static class CardLinkRouter
{
    public static void Handle(string payload)
    {
        char kind;
        string arg;
        if (!CardTextLinker.Parse(payload, out kind, out arg))
        {
            return;
        }
        switch (kind)
        {
            case CardTextLinker.KindCard:
                {
                    // 旧版 payload（存档里的富化文本可能还带着），仍然按"看这张卡"处理。
                    int id;
                    if (int.TryParse(arg, out id))
                    {
                        ShowCard(id);
                    }
                }
                break;
            case CardTextLinker.KindTyped:
            case CardTextLinker.KindQuery:
                {
                    // payload = <掩码>:<文字>；掩码 0 = 不限主类。
                    int mask;
                    string text;
                    if (CardTextLinker.ParseTyped(arg, out mask, out text))
                    {
                        CardSearchWindow.ShowQuery(text, mask);
                    }
                    else
                    {
                        CardSearchWindow.ShowQuery(arg, 0);
                    }
                }
                break;
            case CardTextLinker.KindField:
                CardSearchWindow.ShowQuery(arg, 0);
                break;
            case CardTextLinker.KindName:
                {
                    // 卡名 → **只出同名的卡**（用户 2026-10-03；2026-10-04 起还要算上
                    // "规则同名"，如点「海」要带出传说之都 亚特兰蒂斯）。
                    // 掩码 0 = 不限主类（「X」怪兽那种带种类短语的走另一支，带掩码进来）。
                    // payload = <掩码>:<卡名>:<被点处原文>（第三段可选）——标题照抄第三段。
                    int mask;
                    string text;
                    string title;
                    if (CardTextLinker.ParseCardName(arg, out mask, out text, out title))
                    {
                        CardSearchWindow.ShowName(text, mask, title);
                    }
                    else
                    {
                        CardSearchWindow.ShowName(arg, 0, "");
                    }
                }
                break;
            case CardTextLinker.KindRecord:
                {
                    // 「X」的卡名记述 → **只出卡文里记述了 X 的卡**（用户 2026-10-03）。
                    // 掩码恒为 0（这半句不带种类限定），留着解析只是为了与上面同形。
                    int mask;
                    string text;
                    if (CardTextLinker.ParseTyped(arg, out mask, out text))
                    {
                        CardSearchWindow.ShowRecord(text, mask);
                    }
                    else
                    {
                        CardSearchWindow.ShowRecord(arg, 0);
                    }
                }
                break;
            case CardTextLinker.KindUnion:
                {
                    // 「X」或者有那个卡名记述的<种类词> → **并集**（用户 2026-10-03）。
                    // 掩码只作用在记述那一支（见 CardSearchWindow.RunUnionQuery）。
                    int mask;
                    string text;
                    if (CardTextLinker.ParseTyped(arg, out mask, out text))
                    {
                        CardSearchWindow.ShowUnion(text, mask);
                    }
                    else
                    {
                        CardSearchWindow.ShowUnion(arg, 0);
                    }
                }
                break;
            case CardTextLinker.KindRd:
                {
                    // RD 括号条件：payload = <主类掩码>:<括号里那串字>。
                    // 走独立入口 —— 它**没有关键字**，整条结果都是筛出来的
                    // （若走 ShowQuery 会被当成"搜这两个字"，一张都搜不到）。
                    int mask;
                    string text;
                    if (CardTextLinker.ParseTyped(arg, out mask, out text))
                    {
                        CardSearchWindow.ShowRd(text, mask);
                    }
                }
                break;
            case CardTextLinker.KindSet:
                {
                    // 系列/字段链接（用户 2026-10-04）：
                    // payload = <主类掩码>:<!setname 表内码>:<被点处原文>。
                    // 按**系列成员**检索（CardsManager.IfSetCard：低 12 位=系列本体、
                    // 高 4 位=子系列）+ 主类掩码；标题照抄第三段（用户 2026-10-04 第三条：
                    // 「融合」魔法卡 就写「融合」魔法卡 + 结果数，别加工）。
                    // 简介系列行、以及"字段+种类词"那几处都走这一支。
                    int mask;
                    int code;
                    string title;
                    if (CardTextLinker.ParseSet(arg, out mask, out code, out title))
                    {
                        CardSearchWindow.ShowSetcode(title, code, mask);
                    }
                }
                break;
        }
    }

    static void ShowCard(int id)
    {
        YGOSharp.Card card = YGOSharp.CardsManager.Get(id);
        if (card == null)
        {
            return;
        }
        Program.I().cardDescription.setData(card, GameTextureManager.myBack, "", true);
        QuickTestTrace.Log("link", "card id=" + id + " name=" + card.Name);
    }
}
