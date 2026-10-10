using UnityEngine;
using System.Collections;

public class cardPicLoader : MonoBehaviour
{

    public int loaded_code = -1;

    public int code = 0;

    public YGOSharp.Banlist loaded_banlist = null;

    public Texture2D defaults = null;

    public ban_icon ico;

    public void clear()
    {
        // 条目行被回收（面板关闭 / 换一批数据）⇒ 通知锁状态机一声。
        // ⛔ **检索列表里的锁不会因此被解掉**（用户 2026-10-10：卡滚出屏幕范围 / 不在右侧栏了
        //   也不许自动解锁）—— CardDescLock.NotifyRowRecycled 自己按宿主分别处置：
        //   检索列表只记「这一行壳空了」，决斗「墓地·除外」一览的缩略格、selectDeck 那种才放锁。
        //   这里只负责通知。
        CardDescLock.NotifyRowRecycled(CardDescLock.RowRootOf(this));
        loaded_code = 0;
        code = 0;
        ico.show(3);
        uiTexture.mainTexture = null;
    }

    public void reCode(int c)
    {
        // 同一个滚轮单元被填进**另一张卡**了 ⇒ 通知锁状态机「被锁那一行的壳被改派了」。
        // ⛔ 检索列表里这**不等于解锁**：被锁那张滚出视口再滚回来时，行池交给它的往往不是
        //   当初那个壳（见 CardDescLock.RowOn 的两条命中路径）。只有不在任何检索面板下的行
        //   （决斗一览缩略格 / selectDeck）才照旧放锁。
        // ⚠ 只在「这一条确实是被锁的那条」时才通知（NotifyRowRecycled 自己会判），
        //   否则滚动一次会把别人的锁弄掉。
        if (CardDescLock.IsLocked && CardDescLock.LockedId != c)
        {
            CardDescLock.NotifyRowRecycled(CardDescLock.RowRootOf(this));
        }
        loaded_code = 0;
        code = c;
    }

    public void relayer(int l)
    {
        uiTexture.depth = 50 + l * 2;
        UITexture t = ico.gameObject.GetComponent<UITexture>();
        t.depth = 51 + l * 2;
    }

    public Collider coli = null;

    // ── 「空格键锁定卡牌简介」的整条标记（需求 2026-10-09，第二版外观）──────────
    // 用户第二版口径：「选中不用现在这个框了，改成贴一层白色半透明的锁状图标（右侧的话就
    // 显示在行中间那里），边缘也围起来一圈白。」⇒ 检索列表这一侧**不再整条染色**，改成
    // 「四边围一圈白 + 行中间一枚白色半透明锁图标」。
    // 标记本体由 CardDescLock.NewRowMark 造（含贴图/摆位/量行尺寸的活），这里只按锁状态显隐。
    GameObject rowMark;

    /// <summary>
    /// 每帧轮询锁状态并显隐标记。轮询式（而不是让状态机来 push）的理由与 3D 卡一致：
    /// 这一行会被 VirtualScrollView **回收**去装别的卡，判据 <see cref="CardDescLock.RowOn"/>
    /// 里带着「这一行现在装的 code 必须还是被锁的那张」⇒ 复用后自己就灭了。
    /// ⚠ 标记挂在**行根节点**上（滚动回收时随行一起被处理），所以这里传的是 RowRootOf，
    /// 量尺寸用的矩形则是本组件所在的事件节点（带 BoxCollider 的那个背景控件）。
    /// </summary>
    void SyncRowMark()
    {
        bool on = CardDescLock.RowOn(CardDescLock.RowRootOf(this), code);
        if (on && rowMark == null)
        {
            rowMark = CardDescLock.NewRowMark(CardDescLock.RowRootOf(this),
                GetComponent<UIWidget>());
        }
        if (rowMark != null && rowMark.activeSelf != on)
        {
            rowMark.SetActive(on);
            if (on)
            {
                // 保险（**不是**根因 —— 第四轮把「右侧栏没显示」记成这里的锅，当晚实拍已推翻：
                // 真因是截图口径没跟上「游戏把分辨率从 1600x900 切成 1920x986」那一下。
                // 详见 CardDescLock.OnRowMarkShown 头注）。
                // 之所以留着：运行期 new 的 NGUI 控件何时进面板绘制列表没有保证，
                // 本工程有 ViewToggleButton.cs:254 那次确凿先例；且必须在**显示的这一拍**重建
                // （建的时候还是 inactive，重建会把禁用中的控件排除在外）。
                CardDescLock.OnRowMarkShown(rowMark);
            }
        }
    }

    void Update()
    {
        SyncRowMark();
        if (coli != null)
        {
            if (Program.InputGetMouseButtonDown_0)
            {
                if (Program.pointedCollider == coli)
                {
                    ((CardDescription)(Program.I().cardDescription)).setData(YGOSharp.CardsManager.Get(code), GameTextureManager.myBack,"",true);
                }
            }
        }
        if (Program.I().deckManager != null)
        {
            if (loaded_code != code)
            {
                Texture2D t = GameTextureManager.get(code, GameTextureType.card_picture, defaults);
                if (t != null)
                {
                    uiTexture.mainTexture = t;
                    uiTexture.aspectRatio = ((float)t.width) / ((float)t.height);
                    uiTexture.forceWidth((int)(uiTexture.height * uiTexture.aspectRatio));
                    loaded_code = code;
                    loaded_banlist = null;
                }
            }
            if (loaded_banlist != Program.I().deckManager.currentBanlist)
            {
                loaded_banlist = Program.I().deckManager.currentBanlist;
                if (ico != null)
                {
                    if (loaded_banlist == null)
                    {
                        ico.show(3);
                        return;
                    }
                    ico.show(loaded_banlist.GetQuantity(code));
                }
            }
        }
    }

    public UITexture uiTexture;

    public YGOSharp.Card data { get; set; }
}
