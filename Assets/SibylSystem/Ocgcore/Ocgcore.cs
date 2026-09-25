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

    /// <summary>
    /// 场上的世界坐标 —— **格位的唯一来源**（卡、高亮、禁用标记、牌堆数量文字全走它）。
    ///
    /// 只有一个自变量：**模式**：
    ///   · OCG → get_point_worldposition_ocg(p, c, MasterRule)
    ///   · RD  → get_point_worldposition_rd（格位只认模式，不认规则号 —— 见那个函数）
    ///
    /// ⚠ 曾经这里挂过一条「RD 牌堆按设置换一套 z」的支路 —— 2026-09-21 已随那个开关
    ///   一起**废掉**。教训留在 get_point_worldposition_rd 的注释里：那套 z（照抄 OCG
    ///   外挂格的 ∓9 / ∓14.6）配的是**画在中块里**的格子，中块整体乘 fieldSize
    ///   ⇒ 贴图那一刀被放大过、卡没被放大 ⇒ 牌落在两格中间。
    /// </summary>
    public Vector3 get_point_worldposition(GPS p, gameCard c = null)
    {
        if (GameModeManager.IsRD)
        {
            return get_point_worldposition_rd(p, c);
        }
        return get_point_worldposition_ocg(p, c, MasterRule);
    }

    /// <summary>
    /// OCG 的格位。**只服务 OCG** —— RD 有自己的函数（`get_point_worldposition_rd`），
    /// 别再把 RD 往这里引：RD 的贴图是从 `newfield.png` 派生的另一张图，
    /// 外挂格位对不上（历史坑：RD 曾借这函数传 rule=4，牌堆就挂到了最外面的空列）。
    ///
    /// <paramref name="rule"/>：按哪一档规则算（OCG 传本局的 MasterRule；MASTER RULE 3/4+
    /// 两套格位不同，别混用）。
    /// ⛔ 除了这个参数，本函数一个字节都别顺手改 —— OCG 全回归咬它。
    /// </summary>
    private Vector3 get_point_worldposition_ocg(GPS p, gameCard c, int rule)
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
            if (rule >= 4)
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
            if (rule >= 4)
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
            if (p.sequence < 5 || ((p.sequence == 6 || p.sequence == 7) && rule >= 4))
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
                if (rule >= 4)
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
            if (rule <= 3)
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

    /// <summary>
    /// RD 盘面坐标 —— 与 <c>texture/duel/newfield_rd.png</c> 上的格子一一对应
    /// （生成器 <c>devtools/make_rd_field.py</c>；**贴图改了这里必须一起改**）。
    ///
    /// 布局（我方视角）：中区 3 列 × 2 排 —— 怪兽排靠中线、魔陷排靠玩家；
    /// 左右各借一列（就是 OCG 的第 1/5 列，RD 里被 RDRule.lua 的
    /// `EFFECT_DISABLE_FIELD 0x11711171` 禁掉的那两列）放牌堆：
    ///   左列：远排＝额外卡组、近排＝场地魔法；右列：远排＝卡组、近排＝墓地。
    ///   对手左右镜像（对手的卡组落在我方视角的左侧）。
    ///
    /// ⚠ 贴图上这四个格子用的是**牌堆格的原生画法**（92×116，比怪兽/魔陷格
    ///   106×116 窄，92 正好≈一张卡的宽度）—— 别按怪兽格的大小去画，
    ///   那是两种观感（见 `devtools/make_rd_field.py` 的说明）。
    /// **除外区不画格子**：参考盘面里没有它的位置，所以场地图上不给它格子；
    /// 落脚点仍在盘面外沿（xRemoved、与墓地同排），真有卡被除外才看得见卡。
    ///
    /// ⚠ 这里**不看 MasterRule**：联机 RD 由服务器给 3、本机 AI 局由命令行第 5 格给 3
    ///   （见 `GameModeManager.DuelRule`），但**规则号本来就不该决定盘面** —— 它一变
    ///   （服务器换版本、老录像里存的 5），格位就会跟着跳。所以 RD 的格位只认模式、
    ///   不认规则号，无论 3 还是 5 都落在同一张图的同一批格子上。
    ///   RD 没有额外怪兽区、没有灵摆区，
    ///   怪兽区 seq 0/4（被禁的最左最右）与魔陷区 seq 0/4、6/7 一律摆到场外，
    ///   免得叠到卡组/额外的格子上。
    ///
    /// ⚠ 附属格（卡组/墓地/场地魔法/额外卡组）的格位就在下面这一组常量里，而
    ///   `gameField.NewFieldPathRD` 那张贴图也是照这一套画的 —— **贴图与坐标是一对**，
    ///   改一头必须同时改另一头（生成器 `devtools/make_rd_field.py` 咬的正是贴图那一侧）。
    ///   别把这里的 z 挪成 OCG 外挂格那一档（∓9 / ∓14.6）：那套点画在中块里会被
    ///   fieldSize 再放大一次，而卡不会 ⇒ 牌会落在两个格子中间（2026-09-21 就这么错过一次）。
    /// </summary>
    public Vector3 get_point_worldposition_rd(GPS p, gameCard c = null)
    {
        Vector3 return_value = Vector3.zero;
        float real = (Program.fieldSize - 1) * 0.9f + 1f;
        bool op = p.controller != 0;

        float xExtra = (op ? 9.62f : -10.1f) * real;    // 额外卡组 / 场地魔法那一列
        float xDeck = (op ? -10.1f : 9.62f) * real;     // 卡组 / 墓地那一列
        float xRemoved = (op ? -14.65f : 14.65f) * real;// 除外区：卡组列再往外一档（贴图上不给格子）
        float zNear = (op ? 5.65f : -5.68f) * real;     // 近排（怪兽排，靠中线）
        float zFar = (op ? 11.5f : -11.5f) * real;      // 远排（魔陷排，靠玩家）
        float zOff = (op ? 16.5f : -16.5f) * real;      // 场外（RD 用不到的格位）

        if ((p.location & (UInt32)CardLocation.Deck) > 0)
        {
            return_value = new Vector3(xDeck, 0, zFar);
            return_value.y += p.sequence * 0.03f;
        }
        if ((p.location & (UInt32)CardLocation.Extra) > 0)
        {
            return_value = new Vector3(xExtra, 0, zFar);
            return_value.y += p.sequence * 0.03f;
        }
        if ((p.location & (UInt32)CardLocation.Grave) > 0)
        {
            return_value = new Vector3(xDeck, 0, zNear);
            return_value.y += p.sequence * 0.03f;
        }
        if ((p.location & (UInt32)CardLocation.Removed) > 0)
        {
            return_value = new Vector3(xRemoved, 0, zNear);
            return_value.y += p.sequence * 0.03f;
        }
        if ((p.location & (UInt32)CardLocation.MonsterZone) > 0)
        {
            if (p.sequence < 1 || p.sequence > 3)
            {
                return_value = new Vector3(0, 0, zOff);   // 被禁的外列 / 额外怪兽区：RD 没有
            }
            else
            {
                return_value.y = 0;
                return_value.z = zNear;
                return_value.x = rdColumnX(p.sequence, op) * real;
            }
        }
        if ((p.location & (UInt32)CardLocation.SpellZone) > 0)
        {
            if (p.sequence == 5)
            {
                return_value = new Vector3(xExtra, 0, zNear);   // 场地魔法
            }
            else if (p.sequence >= 1 && p.sequence <= 3)
            {
                return_value.y = 0;
                return_value.z = zFar;
                return_value.x = rdColumnX(p.sequence, op) * real;
            }
            else
            {
                return_value = new Vector3(0, 0, zOff);         // 被禁的外列 / 灵摆区：RD 没有
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

    // ═════════════════ RD 极大怪兽：L/R 部件摆在本体左右 ═════════════════
    //
    // 机制（core 侧，全文见 output/Windows/rd/ai/script/RDMaximum.lua 的
    // RushDuel.MaximumSummonOperation）：
    //   极限召唤 = 主卡 C 进场占中区一格，随后
    //     `Duel.MoveToField(left,  …, 0x2)`   ← L 先落到中区**左**那一格
    //     `Duel.MoveToField(right, …, 0x8)`   ← R 先落到中区**右**那一格
    //     `Duel.Overlay(c, mg)`               ← 再把 L/R 收成主卡的**素材**
    //   所以三张卡在规则上本来就是**一只怪兽**：L/R 没有独立的怪兽行为（不能被选作攻击/
    //   表示形式对象），它们的 `EFFECT_TYPE_XMATERIAL` 效果正是靠「是主卡的素材」才生效。
    //   这里只动「画在哪儿」—— 一个字节的规则都不碰。
    //
    // 左右从哪来：core 已经把答案给了 —— **L/R 成为素材之前所在的那一格**就是它的左右位。
    //   实测（`_probe_rdmax.py`，超魔机神 大霸道王）：
    //     L code=…001 loc=128(=Overlay) seq=0 ploc=4(MZone) pseq=1  ← 左
    //     R code=…003 loc=128           seq=1           pseq=3  ← 右
    //   ⇒ 依据是客户端记录的 `p_beforeOverLayed`（`GCS_cardMove` 在「移进 Overlay」时存下来），
    //     不去猜卡码、也不去比素材序号 —— 卡码那套在 rd_standard.cdb 里**并不总成立**
    //     （120293061 那组的 L/R 码就不是本体 ±1）。
    //
    // ⚠ 为什么不能直接让它们走 `get_point_worldposition_rd`：素材卡在客户端会被
    //   `GCS_cardMove` 改写成「**父卡位置** | Overlay」并把 `position` 加 1000
    //   （那套是给「素材叠在父卡身上」的显示用的）⇒ 直接算出来是「父卡格心 + 素材偏移」，
    //   三张卡会挤在本体那一格。这里把部件单独引回它自己的旧格位。

    /// <summary>这张卡是不是 RD 极大怪兽（type bit15 = 0x8000，口径见 GameStringHelper.typeName）。</summary>
    private static bool isMaximumCard(gameCard c)
    {
        if (c == null)
        {
            return false;
        }
        YGOSharp.Card d = c.get_data();
        return d != null && (d.Type & 0x8000) != 0;
    }

    /// <summary>卡表里的原始卡（`probe` 用来看 data.Attack 是不是被 core 改过的实时值）。</summary>
    private static YGOSharp.Card rawCardOf(gameCard c)
    {
        return c == null ? null : YGOSharp.CardsManager.Get(c.get_data().Id);
    }

    /// <summary>
    /// 这张卡是不是「极大召唤进行中、正要成为 L/R 部件」—— 已经落到场上中区左右列、
    /// 但还没被 `Duel.Overlay` 收成本体素材的那一瞬。
    ///
    /// 为什么需要它（用户 2026-09-22：「极大召唤成功时，被作为部件的两卡会像是给召唤者
    /// 确认一样，能不能跳过这个显示步骤」）：
    ///   `RushDuel.MaximumSummonOperation` 是「MoveToField(L/右) → … → Overlay(c, mg)」两步，
    ///   中间隔着两三条消息（实测逐帧：16.521s 本体到、16.653s L 到、16.811s R 到、
    ///   16.952s 才收成素材 —— 见 log/qt_*.log 的 `[max]` 行）。
    ///   这一段里 L/R 的 `p.location` 只有 MonsterZone、**没有 Overlay 位** ⇒
    ///   `isRdMaximumPiece()` 判不出来、落到 `get_point_worldcondition` 的
    ///   「场上表侧怪兽」档（`verticle_clickable`）⇒ 两张卡带着满身怪兽装备
    ///   （竖立绘 / 怪兽云 / 等级数字 / 星级）立在场上，卡面原值 800 / 500 也照写 ——
    ///   看上去就是两只独立怪兽先「亮相」、再被收走，正是用户不要的那一下。
    ///   判据一命中，这一段就与稳态同档（floating_clickable），两张卡从飞出来的第一帧起
    ///   就是干净的部件卡面，中间没有「先当怪兽、再变部件」的跳变。
    ///
    /// 判据为什么这么取：
    ///   · 只认 `sequence ∈ {1,3}` —— 极大召唤里本体固定落中列 seq=2（`MaximumSummonValue`
    ///     返回 zone 位 0x4），而本体在场时 `EFFECT_MAX_MZONE`=1（占满 RD 中场三格）
    ///     ⇒ 同一方**至多**只有这么一只极大怪兽，边上那两张只可能是它的部件；
    ///   · 再要求「同一控制者、中列 seq=2 上另有一只极大怪兽」—— 「本体被效果挪到边上」
    ///     这类布局就不会被误判（宁可漏、不可错：误判会让真本体丢掉卡面装饰）。
    ///
    /// ⚠ 位置不需要特判：中间态与稳态算出的格位**逐字段相同**（都命中
    ///   `get_point_worldposition_rd` 的 MonsterZone 分支，不带 Overlay 那截偏移），
    ///   验收里 `got == want` 依旧成立。
    /// ⚠ 只在 `GameModeManager.IsRD` 下成立 ⇒ OCG 侧一个像素不变。
    /// </summary>
    private bool isRdMaximumPiecePending(gameCard c)
    {
        if (!GameModeManager.IsRD)
        {
            return false;
        }
        if (c == null || !isMaximumCard(c))
        {
            return false;
        }
        if ((c.p.location & (UInt32)CardLocation.Overlay) != 0)
        {
            return false;      // 已经是素材了，走 isRdMaximumPiece 那一支
        }
        if ((c.p.location & (UInt32)CardLocation.MonsterZone) == 0)
        {
            return false;
        }
        if (c.p.sequence != 1 && c.p.sequence != 3)
        {
            return false;
        }
        for (int i = 0; i < cards.Count; i++)
        {
            gameCard o = cards[i];
            if (o == null || o == c || !o.gameObject.activeInHierarchy)
            {
                continue;
            }
            if (!isMaximumCard(o) || o.p.controller != c.p.controller)
            {
                continue;
            }
            if ((o.p.location & (UInt32)CardLocation.MonsterZone) == 0)
            {
                continue;
            }
            if (o.p.sequence == 2)
            {
                // 🔑 光有「本体站在中列」不够：本体**单体召唤**之后常驻场上，此后再单独
                //   召唤的部件同样满足上面全部条件 ⇒ 被误判成「召唤中途」⇒ 拿到
                //   floating 档（这个档故意 destroy 竖立绘）⇒ 用户 2026-09-24 晚报的
                //   「单个部件没有立绘」就是它。
                //   真正的极大召唤中，SP_SUMMONING 宣告的正是这只本体
                //   （rdMaxNoteSummonDeclared，SP_SUMMONED 才清）；宣告码对不上 =
                //   本体只是恰好在场 ⇒ 不是极大召唤，部件照正常怪兽渲染（有立绘）。
                if (o.get_data().Id != rdMaxSummonDeclared[(int)c.p.controller])
                {
                    continue;
                }
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// 这张卡若是「极大怪兽的 L/R 部件」，返回它该待的世界坐标（本体左右那一格）；
    /// 不是部件（含 OCG 的普通素材）返回 null —— 调用方照旧走格位函数，OCG 侧一个像素不变。
    /// </summary>
    private Vector3? maximumPieceWorldPosition(gameCard c)
    {
        if (!GameModeManager.IsRD)
        {
            return null;
        }
        if (c == null || (c.p.location & (UInt32)CardLocation.Overlay) == 0)
        {
            return null;
        }
        if (!isMaximumCard(c))
        {
            return null;
        }
        // ── 该摆哪一列（q）────────────────────────────────────────────────
        // 优先用它自己的「旧格位」= 极限召唤把它放下的那一列（before.sequence）。
        // ⛔⛔ 拿不到旧格位时**绝不许**再 return null（2026-09-25 定案，「左部件跑到第三格
        //   右部件上方、遮挡阶段 UI」的根因）：调用方 `rdMaximumWantPosition` 的兜底是
        //   `get_point_worldposition(c.p, c)`，而此刻 `c.p.location` 是**素材态**
        //   （含 Overlay 位；可能是 `0x82 = Overlay|Hand`，或还没被 normalizeRdMaximumPiece
        //   归一过）—— `get_point_worldposition_rd` 对 `0x82` 这种**没有任何分支命中**的区
        //   会一路落到函数末尾 `return Vector3.zero`（⚠ 是**垃圾向量、不是 null**，
        //   调用方的 `??` 兜底永远不会触发）。于是两步连锁：
        //     · 卡落到世界原点 (0, 0, 0)；
        //     · 紧接着「三件齐」的 `rdMaximumFlushPosition` 拿这个 nativePos 算方向 ——
        //       x = 0 落在中列的**右侧** ⇒ dir = −1 ⇒ x_target = 0 + (−1)×(0.27 − 4.35)
        //       = **+4.08**，正是**第三格（右部件）**的贴紧位置；而 z 还留在 0
        //       （比怪兽排 zNear 更靠屏幕上方）⇒ 视觉上就是「左部件跑到右部件**上方**」，
        //       并且因为偏上而**遮挡阶段 UI**。
        //   ⇒ 没有旧格位时退到**本体所在列**（真·素材本来就叠在父卡那一格上），
        //     再退到中列 2 —— 落点**永远是中区一个合法格位**。
        // ⚠ 「没有怪兽区位」与「sequence 越界」在这里**合并成同一条**，理由同上：
        //   旧实现把两者都写成 return null，也就都掉进了那个垃圾向量。
        GPS before = c.p_beforeOverLayed;
        uint q = 2;
        bool qFromBefore = false;
        if ((before.location & (UInt32)CardLocation.MonsterZone) != 0
            && before.sequence >= 1 && before.sequence <= 3)
        {
            q = before.sequence;
            qFromBefore = true;
        }
        else
        {
            gameCard qf = rdMaximumBody(c);
            if (qf != null && qf.p.sequence >= 1 && qf.p.sequence <= 3)
            {
                q = qf.p.sequence;
            }
        }

        // ⚠⚠ 只借 before 的**格位（sequence）**，controller 必须用本卡自己的 `p.controller`。
        //   两套值的口径不同：客户端 GPS 的 controller 是**经 localPlayer() 映射过**的
        //   （谁算「我方」随本机视角而定），而 `p_beforeOverLayed` 存的是**没映射的原始值**
        //   —— 实测同一张卡 `p.controller=1` 而 `before.controller=0`（2026-09-22）。
        //   拿 before.controller 去算，算出来的是**对面半场**：三张卡一半在自家、一半在对面，
        //   而每一张单看都「位置正确」（第一版就是这么错的）。
        //
        // ⛔⛔ 区号**不许**用「把 Overlay 位抠掉」那种写法（2026-09-24 定案，物证在
        //   log/qt_19412.log 22:32:53.266）：
        //   core 发的「收成素材」那条 MOVE，`to.location` 实测是 **0x82 = Overlay|Hand**
        //   （`seq=0`、`pos=0`；探针打出来就是 `p=0/130/0/0 before=0/4/1/1`）。
        //   `0x82 & ~0x80 = 0x02`（Hand），而 `get_point_worldposition_rd` **没有 Hand 分支**
        //   ⇒ 一路落到函数末尾 `return Vector3.zero` —— 注意它返回的是**垃圾向量而不是 null**，
        //   调用方那句 `?? get_point_worldposition(...)` 的兜底**永远不会触发**（探针里
        //   就是 `want=(0,0,0)`），部件随即被展示行几何拖到手卡那一排 ⇒ 用户看到的
        //   「LR 部件卡回手卡」。
        //   ⇒ 部件的合法落点只有一个：**它自己原来的那一格**。直接**赋**怪兽区，残余位
        //     一个都不带（Overlay 那截偏移靠「不传 c」天然避开，见上面那条 ⚠）。
        // ⛔⛔ 已经**不在场上**（`p.location` 里没有怪兽区位）：它该画在**本体去的那个区**，
        //   不是场上那一列。core 在本体离场那条路上会把部件的 `to.location` 写成
        //   「本体的新区号 | Overlay」（实测 `0x90 = 墓地|Overlay`，见 log/qt_3480.log
        //   08:45:23.329 的 `[maxmv] flush id=120150001 loc=0x90`），客户端照单收下；
        //   少了这一闸，只要带 Overlay 位就被钉在场的原格位 ⇒ 本体一走、两张部件留在
        //   场上不走 —— 用户 2026-09-24 报的「极大怪兽离场以后场上会多出两个被叠放的
        //   素材状态的 L/R 部件」正是它。
        //   ⇒ 判据是「它现在到底在不在场上」，**不是**「它有没有 Overlay 位」。
        //   ⚠ 残余位必须**确实是某个有格位分支的区**（墓地/卡组/除外/额外）才自己算：
        //     `0x82 = Overlay|Hand` 那种召唤中间态没有 Hand 分支，硬算会得到 `Vector3.zero`
        //     （垃圾向量，见 normalizeRdMaximumPiece 的 ⚠）⇒ 那种一律返回 null 交回兜底。
        // ✅ 2026-09-24 阴性对照已跑过（`_verify_maxleave_neg3.log`）：把这一段注释掉、其余不动
        //   重编后，`_verify_rdai_game.py --max` 的 **7x7b 稳定变红**
        //   （`offpiece id=120150001 loc=0x90 over=2 stuck=1 want=(561,0,672)` ——
        //   已离场的部件被指向怪兽区原列，正是用户报的形态；判据不是假绿）。
        //   这一段就是本 bug 的修复本体。
        if ((c.p.location & (UInt32)CardLocation.MonsterZone) == 0)
        {
            GPS gone = c.p;
            gone.location = c.p.location & ~(UInt32)CardLocation.Overlay;
            gone.position = (int)CardPosition.FaceUpAttack;
            if ((gone.location & (UInt32)(CardLocation.Grave | CardLocation.Deck
                | CardLocation.Removed | CardLocation.Extra)) == 0)
            {
                // ⛔ 残余区**不是**有格位分支的区 ⇒ 它是**召唤中间态**（`0x82 = Overlay|Hand`，
                //   还没被 normalizeRdMaximumPiece 归一），这张卡其实**还在场上** ——
                //   绝不能 return null（那会掉进调用方的垃圾向量兜底，见上面「该摆哪一列」那段）。
                //   ⇒ 按 q 列摆。留痕 `piece-fallback`：这条一旦出现，说明 core 又发了
                //   「Overlay|残余区」的组合而归一没盖住（验收可咬）。
                rdMaxNotePieceFallback(c, q, qFromBefore, "mid");
                GPS mid = c.p;
                mid.location = (UInt32)CardLocation.MonsterZone;
                mid.sequence = q;
                mid.position = (int)CardPosition.FaceUpAttack;
                return get_point_worldposition_rd(mid, null);
            }
            return get_point_worldposition_rd(gone, null);
        }

        // 走到这里 = 还在场上的素材态部件。
        // ⚠ 只在**真的走了兜底列**（拿不到旧格位）时留痕：正常极限召唤恒 qFromBefore=1，
        //   无条件调用会变成「每张部件每次 realize 都落一行」的刷屏 —— 2026-09-25 实测一局
        //   3 条全是 `fromBefore=1` 的正常路径（`before=0/4/1/1`），把「正常局 0 条」这条
        //   判据从 0 打成 3。反过来这也是一份**正向证据**：修好的实现没动正常路径。
        if (!qFromBefore)
        {
            rdMaxNotePieceFallback(c, q, false, "nobefore");
        }
        GPS g = c.p;
        g.location = (UInt32)CardLocation.MonsterZone;
        g.sequence = q;
        g.position = (int)CardPosition.FaceUpAttack;
        return get_point_worldposition_rd(g, null);
    }

    /// <summary>
    /// 诊断（只在 `log/qt_debug.on` 下写，内容不变只写一次）：**部件摆位走了兜底列**的留痕。
    ///
    /// 为什么要它：`maximumPieceWorldPosition` 现在对「拿不到旧格位」一律退到本体列/中列
    /// （见那里的长注释），这**修好了**旧实现的「垃圾向量 → 被三件齐的贴紧推到第三格」
    /// 那条连锁，但那条路径本身就是个**异常**（正常极限召唤的部件 before 恒有怪兽区位、
    /// sequence 恒为 1/3）。⇒ 留一行物证，让「真机再看到 L 部件跑到右边」时能一眼判定
    /// 是不是这个问题、以及当时的 p / before 到底是什么。
    /// 去重口径与 `logRdMaximumPieceState` 同一套：按卡号，内容没变不重复写。
    /// </summary>
    private readonly System.Collections.Generic.Dictionary<int, string> maxPieceFallbackLastById =
        new System.Collections.Generic.Dictionary<int, string>();

    private void rdMaxNotePieceFallback(gameCard c, uint q, bool qFromBefore, string why)
    {
        if (!QuickTestTrace.Enabled || !GameModeManager.IsRD || c == null)
        {
            return;
        }
        int id = c.get_data().Id;
        string line = "piece-fallback id=" + id + " why=" + why
            + " q=" + q + " fromBefore=" + (qFromBefore ? "1" : "0")
            + " p=" + c.p.controller + "/" + c.p.location + "/" + c.p.sequence + "/" + c.p.position
            + " before=" + c.p_beforeOverLayed.controller + "/" + c.p_beforeOverLayed.location
            + "/" + c.p_beforeOverLayed.sequence + "/" + c.p_beforeOverLayed.position
            + " over=" + c.overFatherCount;
        string last;
        if (maxPieceFallbackLastById.TryGetValue(id, out last) && last == line)
        {
            return;
        }
        maxPieceFallbackLastById[id] = line;
        QuickTestTrace.Log("max", line);
    }

    /// <summary>
    /// 极大部件的**本体**：同一控制者、站在中区中列（seq 2）的极大怪兽。找不到返回 null。
    /// 判据与 <see cref="isRdMaximumPiecePending"/> 里那段同源（`MaximumSummonValue` 固定
    /// 把本体放 zone 0x4 = 中列，且本体在场时 `EFFECT_MAX_MZONE`=1 独占中场三格
    /// ⇒ 同侧中列上只可能是本体）。
    /// </summary>
    private gameCard rdMaximumFather(gameCard piece)
    {
        if (piece == null)
        {
            return null;
        }
        for (int i = 0; i < cards.Count; i++)
        {
            gameCard o = cards[i];
            if (o == null || o == piece || !o.gameObject.activeInHierarchy)
            {
                continue;
            }
            if (!isMaximumCard(o) || o.p.controller != piece.p.controller)
            {
                continue;
            }
            if ((o.p.location & (UInt32)CardLocation.MonsterZone) == 0)
            {
                continue;
            }
            if (o.p.sequence == 2)
            {
                return o;
            }
        }
        return null;
    }

    /// <summary>
    /// 极大**本体**在哪 —— 判据比 <see cref="rdMaximumFather"/> **多一道硬闸**：本体**自己不是素材**
    /// （不带 `Overlay` 位）。找不到返回 null。**只给「本体还在吗」这类判断用**。
    ///
    /// ⛔⛔ 为什么不能直接用 `rdMaximumFather`（2026-09-24 定案，物证 `log/qt_13724.log`
    ///   11:35:55.845）：
    ///   `rdMaximumFather` 只要求「同控制者 + 带怪兽区位 + `sequence == 2`」。可**两张 L/R 部件
    ///   在客户端的 `p.sequence` 也是 2** —— 那是规矩本来就这么写的：`GCS_cardMove` 的素材重规整
    ///   把素材统一改成 `父卡.p.sequence`，而 `normalizeRdMaximumPiece` 也把部件对齐到本体那一列
    ///   （两处都是**改过的、验过的**行为，别动）。于是「带怪兽区位 + seq==2 的极大卡」在客户端
    ///   里**一次就有三张**（本体 + 两张部件）⇒ `rdMaximumFather(部件)` 顺手就返回了**另一张部件**。
    ///   后果：本体离场后兜底仍以为「本体还在」，永远不动手 —— 用户看到的「部件永久卡在场上」
    ///   就是这个（探针里字符特征极好认：`father=1` 而本体早已是 `0x90`）。
    ///   ⇒ 判「本体在不在」必须要求**它不是素材**，光看 seq 不够。
    ///   ⚠ 这里**不**要求 `sequence == 2`：本判据的错法必须是「保守」（漏判⇒不归位，
    ///     由别的闸兜；误判⇒把本体还在的部件挪走 = 画面炸掉），所以条件取最窄的那一条。
    ///
    /// ⛔⛔ **2026-09-25 晚三：再加一道「成组」校验（用户第 3 条）。**
    ///   上面那套「同侧 + 带怪兽区位 + 不带 Overlay」**没有任何配对语义** —— 它找的是
    ///   「**本侧随便哪一个本体**」，不是「**这张部件的**本体」。于是：
    ///     旧极大怪下去、它的 L/R 部件成了孤儿 → 玩家接着召唤**新**极大怪 →
    ///     `rdMaximumBody(旧部件)` 立刻找到了**新本体** ⇒ 返回非 null ⇒
    ///     `rdMaxParkOrphans` 判「本体还在」⇒ **孤儿永不归位**（第 1095 行那一句）。
    ///     旧部件就永久停在场上，位置跟着新本体的格位走 ⇒ 用户看到的
    ///     「**旧左部件停在下一个极大怪的格子上、遮盖住它**」。
    ///   ⇒ 必须校验「这张候选本体**确实拥有**该部件」。
    ///   ⛔ 用 **`GCS_cardGetOverlayElements` 的口径**（引擎的素材归属：同控制者、
    ///     同 location、同 sequence）—— 这是客户端里唯一的权威归属依据；
    ///     **不要**用「卡号相邻」那种猜法（三件卡号确实连号，但那是发行规律、不是引擎契约，
    ///     异画/特殊卡的别名会把相邻关系打乱）。
    ///   ⚠ 位置口径**必须**跟着 `maximumPieceWorldPosition` / `normalizeRdMaximumPiece`
    ///     走：部件被归一之后 `p.sequence` 就是本体那一列（2）。所以这一道校验在
    ///     「部件已被归一」时**恒成立**（那就是健康的三件），只在「部件挂着召唤中间态、
    ///     还没被归一」时才真正起作用 —— 正是我们要拦的那一族。
    /// </summary>
    private gameCard rdMaximumBody(gameCard piece)
    {
        if (piece == null)
        {
            return null;
        }
        for (int i = 0; i < cards.Count; i++)
        {
            gameCard o = cards[i];
            if (o == null || o == piece || !o.gameObject.activeInHierarchy)
            {
                continue;
            }
            if (!isMaximumCard(o) || o.p.controller != piece.p.controller)
            {
                continue;
            }
            if ((o.p.location & (UInt32)CardLocation.MonsterZone) == 0)
            {
                continue;
            }
            if ((o.p.location & (UInt32)CardLocation.Overlay) != 0)
            {
                continue;      // ⛔ 素材（L/R 部件）**不是**本体
            }
            // ── 成组校验：这张本体**确实拥有**这块部件吗（口径同 GCS_cardGetOverlayElements）──
            // ⛔⛔ 别退回「只要同侧就算」。少了这一道，旧极大怪的孤儿部件会被**新本体**
            //   认领 ⇒ `rdMaxParkOrphans` 永不归位 ⇒ 孤儿永久停在场上盖住新极大怪
            //   （用户 2026-09-25 第 3 条）。
            // ⚠⚠ 但**不能无条件要求配对** —— 有一族**健康**的中间态会被它误杀：
            //   极限召唤的「L/R 已被收成素材、本体那条 MOVE 还在路上」那一段，部件的
            //   `p.sequence` 还**没被归一**（实测 `p=0/130/0/0`，seq 0），而本体已在
            //   seq 2 ⇒ 配对校验**不成立**（真机 `qt_5636.log` 22:06:55.725~.893，
            //   这段窗口实测 168ms）。若那种态被误判成孤儿并超过宽限期（1.0s），
            //   健康的部件会被送进墓地 —— 比原 bug 更糟。
            //   ⇒ 否定配对**只在「本侧已经有完整的另一组三件」时**才生效
            //     （<see cref="rdMaxSideHasOtherTrio"/>）：那才是「新极大怪已成型、
            //     这张卡纯属上一组的残留」这个**唯一确定的场景**；其余时刻（含
            //     「本侧只有一个本体、部件还在路上」）退回旧的宽松口径，不误杀。
            if (rdMaxSideHasOtherTrio(piece, o) && !rdMaxBodyOwnsPiece(o, piece))
            {
                continue;
            }
            return o;
        }
        return null;
    }

    /// <summary>
    /// 这块「极大部件」此刻**还在不在场上** —— 也就是它还算不算三件里的一员。
    ///
    /// ⛔⛔ 为什么必须有它（用户 2026-09-25 第 4 次报告，物证 `log/qt_17992.log`
    ///   18:52:34.685 → 18:52:35.544）：
    ///   所有「三件齐不齐」的判据原来都写的是 **`(location & Overlay) != 0`** ——
    ///   只问「它是不是素材」，**没问「它还在不在场上」**。可部件被 `rdMaxParkOrphans`
    ///   归位之后 `p.location` 是 **`0x90 = 墓地|Overlay`**：Overlay 位**照样在**。
    ///   ⇒ 一句话连锁出的三个症状（用户截图里的全部内容）：
    ///     · `rdMaximumTrioState` 仍报「齐」⇒ `rdMaxTrioMe=1` ⇒ **大框不掉**、
    ///       `gameField.setMaximumBand()` 不收；
    ///     · `rdMaxResolveTrio` 仍把这两张当部件 ⇒ `RdMaxIntegratedOwnsPosition` 返回 true
    ///       ⇒ **一体化继续占用它们的屏幕位置**（悬停时把它们画回场上那两格）；
    ///     · `applyRdMaximumTrioScale` 仍给 1.45 倍 ⇒ 卡还撑在大框口径上。
    ///   物证里两者**同时**成立：`piece … p=1/144/0/0 … got=(403,0,672)`（画在场上）
    ///   而 `want=(-1201,0,672)`（真实目标=墓地）⇒ 屏幕上是「部件**从墓地回场上**的补间」，
    ///   同时它的 info 面板按 `p.location & Grave` 打出 **「墓地」** 两个字
    ///   （`UIHelper.getGPSstringLocation`）。
    ///
    /// ⛔⛔⛔ **判据：带 `Overlay` 位，且“残余区”不是真正的终态离场区**（墓地/卡组/除外/额外）。
    ///
    /// 为什么**不能**写成「必须带 MonsterZone 位」（2026-09-25 晚八实锤：那么写就是回归，
    /// 用户第 5 次报告里「三张各自分散、像根本没进极大状态」就是它）：
    ///   core 把部件收成素材时 `to.location` 实测是 **`0x82 = Overlay|Hand`**
    ///   （见 `maximumPieceWorldPosition` 里那条 ⛔⛔，物证 `log/qt_19412.log` 22:32:53.266，
    ///   探针里就是 `p=0/130/0/0`）—— **它不带怪兽区位**。
    ///   而 `isRdMaximumPiecePending` **第一条**就是 `(location & Overlay) != 0 ⇒ false`
    ///   ⇒ `0x82` **也**不是 pending。于是「要求 MonsterZone」的写法把 `0x82` 逼进
    ///   **两边都不认的真空**：既不算三件、也不算召唤中间态 ⇒ 贴紧/放大/大框全不生效，
    ///   玩家看到的就是「三张卡各自散着、完全不像极大状态」。
    ///   ⇒ 而 `0x82` 在语义上**就是「已经当上素材了」**（`maximumPieceWorldPosition` 专门
    ///     为它写了 `mid` 分支、按本体列摆位），**必须算三件**。
    ///
    /// 所以闸门只排除**真正的终态**：`0x90(墓地|素材) / 0x50(卡组|素材) / 0x20(除外|素材) /
    /// 0x40(额外|素材)` —— 那才是不该再占着场上位置、不该再撑 1.45 倍的那一族。
    /// ⚠ 与 <see cref="rdMaxPieceGoneOffField"/> 的分工：那个要求「**在**终态离场区」，
    ///   这个要求「**不在**终态离场区」，两者互补且都带 `Overlay` 位 ⇒ 合起来正好切完
    ///   所有素材态。中间态 `0x82` 归**这个**（算三件）。
    /// </summary>
    private static bool rdMaxPieceOnField(gameCard c)
    {
        return c != null
            && (c.p.location & (UInt32)CardLocation.Overlay) != 0
            && (c.p.location & (UInt32)(CardLocation.Grave | CardLocation.Deck
                | CardLocation.Removed | CardLocation.Extra)) == 0;
    }

    /// <summary>
    /// 这块极大卡是「**刚刚离场的素材态部件/本体**」—— 带着 `Overlay` 位、却已经不在怪兽区。
    ///
    /// 用途只有一个：让这类卡的摆位走 **`rush=true` 瞬移**，而不是 `TweenTo` 的 0.1~0.3s 缓动
    /// （调用点 `realize` 摆位循环末的 `UA_flush_all_gived_witn_lock`）。
    ///
    /// ⛔⛔ 为什么必须单列（用户 2026-09-25 第 5 次报告，物证 `log/qt_20760.log`
    ///   20:23:33.052 → .234）：
    ///   本体被送墓（`0x4/2/1 → 0x10/0/5`）之后，core 在**同一帧**把三张卡全指向墓地：
    ///     `[maxmv] flush id=…001 loc=0x90 seq=0 ctrl=1 rush=0 acc=( 4,0,7) giv=(-12,0,7)`
    ///     `[maxmv] flush id=…003 loc=0x90 seq=0 ctrl=1 rush=0 acc=(-5,0,7) giv=(-12,0,7)`
    ///     `[maxmv] flush id=…002 loc=0x10 seq=0 ctrl=1 rush=0 acc=( 0,0,7) giv=(-12,0,7)`
    ///   `rush=0` ⇒ 三张各走一段 TweenTo。于是这 170ms 里玩家看到的是：
    ///     · 本体已经没了（`count=0`）、三件状态已解散 ⇒ **贴紧/一体化停止**，
    ///       L/R 各自停在**自己的原格位**（x=+4 / −5，中间空一格）—— 用户说的「**散开**」；
    ///     · 随后两张部件才慢吞吞地飞向墓地列（x=−12）—— 用户说的「部件**滞留**在场上的
    ///       一段动画」；
    ///     · 而它们的 `p.location` 已经是 `0x90` ⇒ info 面板打出「**墓地**」两个字。
    ///   ⇒ 同一个 `rush` 开关上还挂着一条**同族**的老账（见 `realize` 里
    ///     `isRdMaximumPiecePending` 那段注释：召唤中「手卡→场」的补间会横穿半张屏幕）。
    ///     一次把两类都瞬移掉，观感才干净：**召唤时瞬间出现、离场时瞬间消失**。
    ///
    /// ⚠ 判据与 <see cref="rdMaxPieceOnField"/> 严格互补（同一个 `Overlay` 位，一取一否
    ///   怪兽区位），但**不能**直接写 `!rdMaxPieceOnField(c) && Overlay位` ——
    ///   那样把 `0x82 = Overlay|Hand`（召唤中间态）也算了进去，而那一族该走
    ///   `isRdMaximumPiecePending` 那条已有路径（两者都进 rush 也无害，但语义要分开：
    ///   一个是「还没到场」，一个是「已经走了」）。
    /// ⚠ 只认「真的在某个有格位分支的区里」（墓地/卡组/除外/额外）—— `0x82` 那种
    ///   残余区没有格位分支，交给 pending 那条管。
    /// </summary>
    private static bool rdMaxPieceGoneOffField(gameCard c)
    {
        if (c == null || !isMaximumCard(c))
        {
            return false;
        }
        if ((c.p.location & (UInt32)CardLocation.Overlay) == 0)
        {
            return false;      // 不是素材态，与三件无关
        }
        if ((c.p.location & (UInt32)CardLocation.MonsterZone) != 0)
        {
            return false;      // 还在场上：正常三件，照旧走 tweens
        }
        return (c.p.location & (UInt32)(CardLocation.Grave | CardLocation.Deck
            | CardLocation.Removed | CardLocation.Extra)) != 0;
    }

    /// <summary>
    /// 本侧是不是**已经有完整的另一组三件**了（本体 <paramref name="candBody"/> 那一组之外）。
    ///
    /// 给 <see cref="rdMaximumBody"/> 的成组校验当**前提闸**：只有「本侧的新一组三件
    /// 已经成型」时，才允许用配对校验否定一块旧部件（那才是用户第 3 条那个确定场景）。
    /// 其余时刻一律不做配对否定，避免误杀「本体已在场、部件 seq 还没归一」的健康中间态
    /// （那个窗口真机实测 168ms，但慢局/掉帧会拉长，不许赌）。
    ///
    /// 判据：把 <paramref name="candBody"/> 当成本体，数一下**配得上它**的部件有几块
    /// （口径 = <see cref="rdMaxBodyOwnsPiece"/>）。≥2 就是「另一组三件已成型」。
    /// ⛔ 不调 `rdMaxResolveTrio`（它还会排 `rdMaxHeld` / pending / 旧格位，口径更窄，
    ///   这里要的只是「这一组是不是已经在位」）。
    /// </summary>
    private bool rdMaxSideHasOtherTrio(gameCard piece, gameCard candBody)
    {
        if (candBody == null)
        {
            return false;
        }
        int n = 0;
        for (int i = 0; i < cards.Count; i++)
        {
            gameCard o = cards[i];
            if (o == null || o == piece || o == candBody || !o.gameObject.activeInHierarchy)
            {
                continue;
            }
            if ((o.p.location & (UInt32)CardLocation.Overlay) == 0)
            {
                continue;      // 只数部件
            }
            if (rdMaxBodyOwnsPiece(candBody, o))
            {
                n++;
            }
        }
        return n >= 2;
    }

    /// <summary>
    /// <paramref name="body"/>（一个候选本体）是不是**真的拥有** <paramref name="piece"/>
    /// 这块素材。口径逐条对齐 <see cref="GCS_cardGetOverlayElements"/>（引擎的素材归属）：
    ///
    ///   · 同控制者（`p.controller` 相等）；
    ///   · 「抠掉 Overlay 位之后」的区号相同；
    ///   · `p.sequence` 相同。
    ///
    /// ⛔ 为什么用这三条而不是卡号：三件卡号确实连号（`120283150/151/152`），但那是**发行规律**，
    ///   不是引擎契约 —— 异画/别名卡会把相邻关系打乱（本项目 2026-09-24 已实测：
    ///   `get_code()` 会把 alias 当成本码）。位置归属是引擎自己给的，才是权威。
    /// ⚠ 这是**严格版**：三条全中才算「拥有」。健康的三件里部件被归一后 `p.sequence`
    ///   就是本体那一列（2）⇒ 恒成立，不影响正常路径。
    /// </summary>
    private bool rdMaxBodyOwnsPiece(gameCard body, gameCard piece)
    {
        if (body == null || piece == null)
        {
            return false;
        }
        if (body.p.controller != piece.p.controller)
        {
            return false;
        }
        if ((piece.p.location | (UInt32)CardLocation.Overlay)
            != (body.p.location | (UInt32)CardLocation.Overlay))
        {
            return false;
        }
        return piece.p.sequence == body.p.sequence;
    }

    // ═════════ 部件「本体早就不在了」的兜底（用户 2026-09-24 第二次报告）═════════
    //
    // 现象：「上级召唤把极大怪兽送墓地以后，场上还是会残留两个部件」。
    //
    // 为什么前面两条离场修复（`maximumPieceWorldPosition` 的「不在场上 ⇒ 跟着本体走」+
    // `normalizeRdMaximumPiece` 的起点闸）盖不住它：那两条的前提都是 **core 会给部件发
    // MOVE**。core 确实有这条（`operations.cpp:4389` 本体离场时
    // `send_to(overlays, 0, REASON_RULE + REASON_LOST_OVERLAY, …, LOCATION_GRAVE, …)`
    // ⇒ 实测两条路径都发得出，见 `_verify_maxleave_fix3.log` / `_verify_maxleave_rel_a.log`）。
    // 但客户端**不能假定**它一定发：用户现场的部件是**永久**留在场上的（不是补录 36 那种
    // 130~270ms 瞬态），说明那一次 core 根本没提这两张卡。
    //
    // ⇒ 判据挪到「客户端自己的不变量」上，不再依赖消息形状：
    //   **「带 Overlay 位的极大卡」+「自己那一侧中列没有本体」= 一个任何健康对局都不该出现的
    //   状态**（部件是素材，素材必有本体；极限召唤唯一的那段窗口是「先收素材、本体后到」，
    //   实测只有 1ms~0.2s ⇒ 用 <see cref="RdMaxOrphanGrace"/> 秒的宽限期完全避开）。
    //   一旦持续超过宽限期 ⇒ 就地归到**本体去的那个区**。core 对 LOST_OVERLAY 素材的处置是
    //   **一律送墓地**（`operations.cpp:4389` 那句常量就是 `LOCATION_GRAVE`），所以落点取墓地
    //   —— 与 core 真发消息时的终态逐字段一致（`0x90 = 墓地|Overlay`）。归一之后
    //   `maximumPieceWorldPosition` 那段「不在场上 ⇒ 跟着本体走」自然接管摆位。
    //
    // ⛔ 为什么必须改 `p.location` 而不仅是摆位：只要它还是 `0x84`（怪兽区|Overlay），
    //   `isRdMaximumPiece()` / `GCS_cardGetOverlayElements(新本体)` / `rdMaximumTrioState`
    //   都会继续把这张幽灵卡当成「场上的部件」——
    //     · 大框/贴紧会把它算进三件（本体不在就凑不齐，但一旦**同侧再召唤一只极大**，
    //       序列号一对上就会被那次的素材重规整循环**领养**回场上，变成第 4 张卡）；
    //     · 探针的 `count=` 也永远掉不下去（验收 7x2/7x5 咬的就是它）。
    //   改成 `墓地|Overlay` 之后：既不再被领养（`0x90|0x80 ≠ 0x04|0x80`），
    //   也不再计入 `count=`，摆位走「不在场上」那条支 ⇒ 用户看到的就是「部件跟着本体走了」。
    //
    // ⚠ 只碰 RD 极大卡；OCG 侧 `isMaximumCard` 恒 false ⇒ 一个字节不变。
    // ⚠ 宽限期内的「本体不在」不计入（召唤中间态）；本体一出现就清零。

    /// <summary>各张「没有本体」的部件从什么时候开始等（按卡号；本体一出现/它离场就清）。</summary>
    private readonly System.Collections.Generic.Dictionary<int, float> rdMaxOrphanSince =
        new System.Collections.Generic.Dictionary<int, float>();
    private int rdMaxOrphanFrame = -1;

    /// <summary>已经报过 `orphan-suspect` 的卡号（每张只报一次，别每帧刷屏）。</summary>
    private readonly System.Collections.Generic.HashSet<int> rdMaxSuspectLogged =
        new System.Collections.Generic.HashSet<int>();

    /// <summary>
    /// 宽限期：极限召唤的中间态（L/R 已收成素材、本体那条 MOVE 还没进来）实测 1ms~0.2s，
    /// 但「等本体」的暂存上限是 0.7s（<see cref="RdMaxStageHold"/>）⇒ 宽限必须**大于**它，
    /// 否则慢召唤会被这个兜底当幽灵把部件提前收走。取 1.0s 留一倍余量；
    /// 真·残留由根修 A/B（seq 对齐 + 离场确定性跟随）在消息当帧处理，不靠这里抢时间。
    /// </summary>
    private const float RdMaxOrphanGrace = 1.0f;

    /// <summary>
    /// 兜底的**每帧入口**（`Ocgcore.preFrameFunction` 每帧调一次）。
    ///
    /// ⛔⛔ 为什么必须有它（用户 2026-09-24 **第三次**报告后定案 —— 这是前两轮都白修的根因）：
    ///   兜底与探针原来**都只挂在 `realize`** 里，而 `realize` 是「收到消息才跑」的。
    ///   可宽限期要「≥2 次调用、相隔 0.5s」才可能计满 ⇒ 部件变幽灵之后如果**没有新消息**
    ///   （玩家就那样停在场上看着），`realize` 不再被调 ⇒ 那一秒的计时永远停在第一帧
    ///   ⇒ **归位、探针、判据三者一起静默**：用户明明看得见残留，日志里却一个字都没有。
    ///   这正是「三条离场复现全绿、用户却照旧复现」的原因 —— 我的验收脚本里消息一直在流，
    ///   所以它绿；玩家真实操作里消息流一停，兜底就瞎了。
    ///   ⇒ 判据必须跑在**帧**上，不能跑在**消息**上。
    /// </summary>
    public void rdMaxOrphanTick()
    {
        rdMaxAlignPieceSequences();
        rdMaxParkOrphans();
        rdMaxLogPieceStates();
    }

    /// <summary>seq 对齐的每帧一次闩（与 <see cref="rdMaxOrphanFrame"/> 分开：
    /// 两个函数各自独立判帧，共用一个会把对方短路）。</summary>
    private int rdMaxAlignFrame = -1;

    /// <summary>
    /// **RD 极大部件的 seq 对齐本体**（2026-09-24 第四次报告的根修，真机局
    /// log/qt_24572.log 12:31:20.739 一手物证）：
    ///
    /// `normalizeRdMaximumPiece` 只在「收成素材」那条 MOVE 进来的**那一刻**对齐 seq，
    /// 而真机极限召唤的顺序是 **L/R 先收成素材、本体那条 MOVE 晚 27ms 才到**
    /// （07.553/07.571 收素材，07.580 本体才落中列）⇒ 归一时 `rdMaximumBody` 找不到本体
    /// ⇒ 部件的 seq 留在了**自己原来的列号**（1/3），而不是本体的 2。
    ///
    /// 这一个字段差错在**本体离场**那一刻爆炸：`GCS_cardMove` 的素材重规整循环
    /// （`overlayed_cards_of_cardFrom`）按 OCG 口径「素材.seq == 父卡.seq」匹配，
    /// 1/3 ≠ 2 ⇒ **两张部件没跟着本体走**，以素材态滞留在场上（用户看到的残留）；
    /// 0.6s 后我方再召唤下一只极大，新 L 落在 seq1，同一条口径反过来把**旧部件误认成
    /// 新 L 的素材**（21.597 劫成 0x82、21.626 领养成 0x84/2/x）⇒ 旧 L/R 图像从此
    /// 钉死在场上，正是用户报的形态。
    ///
    /// ⇒ 每帧把「带 Overlay 位 + 带怪兽区位」的部件 seq 对齐到本体（同 <see cref="rdMaximumBody"/>
    ///   的严格判据）。这正是 OCG 素材不变量「素材与父卡同 seq」的 RD 版——重规整循环只在
    ///   父卡**动**的时候维护它，父卡站着不动时由这里维护。对齐之后本体一离场，
    ///   现成的重规整循环就会像对面那只（13.487，全程正常）一样把两张部件带走。
    ///
    /// ⚠ 摆位不受影响：部件画哪一列读的是 `p_beforeOverLayed.sequence`
    ///   （见 maximumPieceWorldPosition / rdMaximumTrioState），不读这里的 seq。
    /// ⚠ 只对齐「本体在场」的部件；本体缺席（幽灵/召唤中间态）不动，交给兜底。
    /// </summary>
    private void rdMaxAlignPieceSequences()
    {
        if (!GameModeManager.IsRD || rdMaxAlignFrame == Time.frameCount || cards.Count == 0)
        {
            return;
        }
        rdMaxAlignFrame = Time.frameCount;
        for (int i = 0; i < cards.Count; i++)
        {
            gameCard c = cards[i];
            if (c == null || !c.gameObject.activeInHierarchy || !isMaximumCard(c))
            {
                continue;
            }
            if ((c.p.location & (UInt32)CardLocation.Overlay) == 0
                || (c.p.location & (UInt32)CardLocation.MonsterZone) == 0)
            {
                continue;      // 不是「场上素材态」的部件：本体不在场的交给兜底，不在场上的不归这管
            }
            gameCard f = rdMaximumBody(c);
            if (f == null || f.p.sequence == c.p.sequence)
            {
                continue;
            }
            uint seqOld = c.p.sequence;
            c.p.sequence = f.p.sequence;
            QuickTestTrace.Log("max", "piece-align id=" + c.get_data().Id
                + " seq=" + seqOld + "->" + c.p.sequence
                + " father=" + f.get_data().Id);
        }
    }

    /// <summary>
    /// 每帧把**每一张场上的 / 素材态的**极大卡的完整状态报一遍（内容不变不重复写）。
    ///
    /// ⛔ 为什么不直接把 `logMaximumProbe()` 挂到帧上：那一支还会写 `count=` / `offpiece` 行，
    ///   而验收判据咬的是「**最后一条** `count=`」这种**消息序**语义（7x2 等）—— 挂到帧上
    ///   会把判据的语义整个改掉，等于自己拆自己的验收。
    ///   这里只写 `[max] piece …`（现有判据一条都不解析它）⇒ 纯增益、零干扰。
    /// </summary>
    private void rdMaxLogPieceStates()
    {
        if (!QuickTestTrace.Enabled || !GameModeManager.IsRD || cards.Count == 0)
        {
            return;
        }
        for (int i = 0; i < cards.Count; i++)
        {
            gameCard c = cards[i];
            if (c == null || !c.gameObject.activeInHierarchy || !isMaximumCard(c))
            {
                continue;
            }
            logRdMaximumPieceState(c);
        }
        logRdMaxOffFieldTrio();
        logRdMaxTrioStateChange();
    }

    /// <summary>
    /// 三件状态 `rdMaxTrioMe/Op` **每次变化**落一行，并附上「本侧三张极大卡的 p 值」。
    ///
    /// ⛔⛔ 为什么必须有它（2026-09-25 晚八，一次真实回归的教训）：
    ///   `rdMaxPieceOnField` 第一版写成「带 Overlay 位 **且** 带 MonsterZone 位」——
    ///   看起来严丝合缝，实际把 **`0x82 = Overlay|Hand`**（core 收素材时真发的区号，
    ///   见 `maximumPieceWorldPosition` 里那条 ⛔⛔）逼进了**两边都不认的真空**：
    ///   它不是「在场上」（无怪兽区位）、也不是 `isRdMaximumPiecePending`（第一条就按
    ///   Overlay 位 return false）⇒ 三件恒不成立 ⇒ **贴紧/放大/大框全不生效**，
    ///   玩家看到的是「三张卡各自散着、完全不像极大状态」。
    ///   ⇒ 这个回归**自动化抓不到**：造局探针走的是 `0x84`，`0x82` 只出现在真机路径上。
    ///     所以补一条**不依赖构造**的探针：只要三件状态翻过一次边就落一行，
    ///     附带每张的 `p=` —— 真机一眼就能看出「三张明明在场、trio 却是 0」。
    ///
    /// ⚠ 读**缓存** `rdMaxTrioMe/Op`（不去调 `rdMaximumTrioComplete`：那会写 `rdMaxPrevPos`）。
    ///   它与 `logRdMaxOffFieldTrio` 同帧、同源，只是判据口径相反（那条只在「有离场部件」时报）。
    /// </summary>
    private string rdMaxTrioStateLast = "";
    private void logRdMaxTrioStateChange()
    {
        string line = "trio me=" + (rdMaxTrioMe ? "1" : "0") + " op=" + (rdMaxTrioOp ? "1" : "0");
        // 附上本侧三张极大卡的 p（同侧、按 seq 排），三件为什么不成一眼可见。
        for (int side = 0; side < 2; side++)
        {
            line += " | s" + side + ":";
            int n = 0;
            for (int i = 0; i < cards.Count && n < 4; i++)
            {
                gameCard c = cards[i];
                if (c == null || !c.gameObject.activeInHierarchy || !isMaximumCard(c))
                {
                    continue;
                }
                if ((int)c.p.controller != side)
                {
                    continue;
                }
                line += "[" + c.get_data().Id + " p=" + c.p.location + "/" + c.p.sequence
                    + " b=" + c.p_beforeOverLayed.location + "/" + c.p_beforeOverLayed.sequence + "]";
                n++;
            }
            if (n == 0)
            {
                line += "none";
            }
        }
        if (line != rdMaxTrioStateLast)
        {
            rdMaxTrioStateLast = line;
            QuickTestTrace.Log("max", line);
        }
    }

    /// <summary>
    /// 「已经离场的极大部件」侧的三件状态探针（内容不变只写一次），供
    /// <c>_verify_rdai_game.py</c> 的 **7x8** 咬「离场后三件必须不再成立」。
    ///
    /// 为什么必须单独报这一个数（用户 2026-09-25 第 4 次报告）：
    ///   三件状态 `rdMaxTrioMe/Op` 决定**大框不掉、一体化继续占用屏幕位置、三张仍放大 1.45**。
    ///   旧口径里「部件带 Overlay 位」就够，而归位后的部件是 `0x90 = 墓地|Overlay`
    ///   ⇒ 三件恒成立 ⇒ 卡片被画回场上而 `p.location` 说墓地（截图里那一幕）。
    ///   `bodyflash` 那条探针**报不了**这个 —— 它挂在**本体**上，本体走了就一行都没有。
    ///   ⇒ 只要「本侧有离场素材态部件、而 `rdMaxTrioMe/Op` 还是 1」就一定是本 bug 复发，
    ///     与帧率、消息序、动画全无关，可以稳定地咬（阴性对照 = 注释掉 rdMaxPieceOnField
    ///     那两闸，本行必出现 `trio=1`）。
    /// </summary>
    private string rdMaxOffFieldTrioLast = "";
    private void logRdMaxOffFieldTrio()
    {
        for (int side = 0; side < 2; side++)
        {
            bool hasOffPiece = false;
            for (int i = 0; i < cards.Count; i++)
            {
                gameCard c = cards[i];
                if (c == null || !c.gameObject.activeInHierarchy)
                {
                    continue;
                }
                if (!(c.isRdMaximumCard() && (int)c.p.controller == side))
                {
                    continue;
                }
                if ((c.p.location & (UInt32)CardLocation.Overlay) == 0)
                {
                    continue;
                }
                if ((c.p.location & (UInt32)CardLocation.MonsterZone) != 0)
                {
                    continue;      // 还在场上：正常三件，不归这条管
                }
                hasOffPiece = true;
                break;
            }
            if (!hasOffPiece)
            {
                continue;          // 没有离场部件：不落行，免得刷屏
            }
            // ⚠ 读**缓存**字段（`rdMaxOrphanTick` 跑在 `rdMaxIntegratedTick` 之前，这里是上一帧的值）。
            //   对「部件都离场了、三件却还报齐」这个**持续态**判据来说，差一帧不影响结论；
            //   而调 `rdMaximumTrioComplete(side)` 会顺带写 `rdMaxPrevPos`（每帧每卡只能一次，
            //   见 rdMaxRefreshTrioState）⇒ 宁可用缓存也不去踩那条已知副作用。
            bool trio = (side == 0 ? rdMaxTrioMe : rdMaxTrioOp);
            string line = "offtrio ctrl=" + side + " trio=" + (trio ? "1" : "0");
            if (line != rdMaxOffFieldTrioLast)
            {
                rdMaxOffFieldTrioLast = line;
                QuickTestTrace.Log("max", line);
            }
        }
    }

    /// <summary>
    /// 每帧一次：把「本体早就不在了」却还挂着素材态的极大卡归到本体去的区（墓地|Overlay）。
    /// 判据与理由见上面那段。调用点：`preFrameFunction`（每帧）+ `realize` 的摆位循环之前（同帧更早生效）。
    ///
    /// ⚠ 计时**按卡**记、判「本侧有没有本体」用 <see cref="rdMaximumBody"/>（**严格版**：
    ///   要求那张极大卡**自己不是素材**），**不要**用 <see cref="rdMaximumFather"/> ——
    ///   后者只要求「seq == 2」，而两张 L/R 部件的 `p.sequence` 也是 2（客户端重规整的约定）
    ///   ⇒ 两张部件会互相认成本体。也**不要**按 controller 0/1 分侧循环：素材在 core 里的
    ///   controller 是 `PLAYER_NONE(2)`，客户端在某些路径上可能原样收下，分侧循环会漏掉它们。
    ///   ⛔ 2026-09-24：这条曾经就是「兜底明明在跑、却一次也不归位」的原因。物证
    ///   `log/qt_13724.log` 11:35:55.845 —— 本体已被标成 `0x90`，两张部件仍报 `father=1`。
    /// </summary>
    private void rdMaxParkOrphans()
    {
        if (!GameModeManager.IsRD || rdMaxOrphanFrame == Time.frameCount)
        {
            return;
        }
        rdMaxOrphanFrame = Time.frameCount;
        if (cards.Count == 0)
        {
            return;
        }
        for (int i = 0; i < cards.Count; i++)
        {
            gameCard c = cards[i];
            if (c == null || !c.gameObject.activeInHierarchy)
            {
                continue;
            }
            if (!isMaximumCard(c))
            {
                continue;
            }
            int id = c.get_data().Id;
            // 不是幽灵（本体还在 / 已经走了 / 根本还没收成素材）⇒ 清计时。
            // 形态判据全部收在 rdMaxLooksOrphaned 里（含「只报不修」的那一族）。
            // ⛔⛔ 「本体还在吗」必须用 rdMaximumBody（**严格版**），不能用 rdMaximumFather ——
            //   后者会把两张 L/R 部件互相认成本体（两张的 p.sequence 都是 2），
            //   于是本体走了这里也判成「还在」⇒ 兜底永远不动手。物证与推演见 rdMaximumBody。
            if (!rdMaxLooksOrphaned(c) || rdMaximumBody(c) != null)
            {
                // 留痕：计时被清掉的**每一次**（含原因）。qt_24572 那次事故里兜底明明
                // 在跑却没动手，事后无法复盘计时是被重置还是没计满 —— 这行就是为下次准备的。
                if (rdMaxOrphanSince.ContainsKey(id))
                {
                    rdMaxOrphanSince.Remove(id);
                    QuickTestTrace.Log("max", "orphan-reset id=" + id
                        + " loc=0x" + c.p.location.ToString("X")
                        + " father=" + (rdMaximumBody(c) != null ? "1" : "0")
                        + " looksOrphaned=" + (rdMaxLooksOrphaned(c) ? "1" : "0"));
                }
                continue;
            }
            float since;
            if (!rdMaxOrphanSince.TryGetValue(id, out since))
            {
                rdMaxOrphanSince[id] = Time.time;
                QuickTestTrace.Log("max", "orphan-wait id=" + id
                    + " loc=0x" + c.p.location.ToString("X") + " 开始计时（本体缺席）");
                continue;      // 这一帧才开始「没有本体」，至少等满宽限期
            }
            if (Time.time - since < RdMaxOrphanGrace)
            {
                continue;
            }
            rdMaxParkOrphan(c);
            rdMaxOrphanSince.Remove(id);
        }
    }

    /// <summary>
    /// 这张极大卡**此刻**是不是「本该跟着本体走、却还挂着素材态」的样子。
    ///
    ///   ① 带 `Overlay` 位（= 在 core 眼里它还是**素材**，客户端也按素材画）**且**不在
    ///      「已经走了」的那几个区（墓地/卡组/除外/额外）里 —— 后者是 core 给的**正常终态**，
    ///      由 `maximumPieceWorldPosition` 的「跟着本体走」那条支接管，不许再动
    ///      （否则重复归位，`orphan-park` 会变成噪音）。
    ///      ⛔ 旧口径在这里**多要了一条「带怪兽区位」**，于是把 `0x82 = Overlay|Hand`
    ///        （补录 36 的根因，缺的正是怪兽区位）那一族整个漏在外面 —— 它们恰恰是
    ///        「还挂着素材态、却没在场上」的形态，用户第三次报的就是它。
    ///   ② 另报一类**可疑**形态（**只留痕、不归位**）：`p_beforeOverLayed` 证明它曾经是
    ///      场上某一列的素材，现在却掉成了「场上表侧、没有 Overlay 位」的普通卡。
    ///      ⛔ 不直接归位：这种状态与「被单独通常召唤出来的极大怪」在字段上**无法区分**，
    ///        盲改会把一只正常怪兽送墓。先让它出现在日志里，拿到物证再定。
    /// </summary>
    private bool rdMaxLooksOrphaned(gameCard c)
    {
        if ((c.p.location & (UInt32)CardLocation.Overlay) != 0)
        {
            return (c.p.location & (UInt32)(CardLocation.Grave | CardLocation.Deck
                | CardLocation.Removed | CardLocation.Extra)) == 0;
        }
        // ② 可疑一族：曾经是场上素材，现在成了场上普通卡
        if ((c.p_beforeOverLayed.location & (UInt32)CardLocation.MonsterZone) == 0
            || (c.p.location & (UInt32)CardLocation.MonsterZone) == 0
            || (c.p.location & (UInt32)(CardLocation.Grave | CardLocation.Deck
                | CardLocation.Removed | CardLocation.Extra)) != 0)
        {
            return false;
        }
        int id = c.get_data().Id;
        if (rdMaxSuspectLogged.Add(id))
        {
            QuickTestTrace.Log("max", "orphan-suspect id=" + id
                + " p=" + c.p.controller + "/" + c.p.location + "/" + c.p.sequence + "/" + c.p.position
                + " before=" + c.p_beforeOverLayed.controller + "/" + c.p_beforeOverLayed.location
                + "/" + c.p_beforeOverLayed.sequence + "/" + c.p_beforeOverLayed.position
                + " father=0 素材态掉了 Overlay 位（或整块被擦成未知卡）—— 只报不修");
        }
        return false;
    }

    /// <summary>把一张「本体早就不在」的幽灵部件归位（留痕 + 改值）。理由见上面那段。</summary>
    private void rdMaxParkOrphan(gameCard c)
    {
        uint old = c.p.location;
        uint seqOld = c.p.sequence;
        c.p.location = (UInt32)(CardLocation.Grave | CardLocation.Overlay);
        // ⛔ 序列号必须清掉：不清的话，同侧以后**再召唤一只极大**时，那次素材重规整循环
        //   要求的 `素材.p.sequence == 本体.p.sequence`（本体恒在 seq 2）就会对上这张幽灵卡
        //   ⇒ 它被领养回场上变成第 4 张卡（见上面那段 ⛔）。
        c.p.sequence = 0;
        c.p.position = (int)CardPosition.FaceUpAttack;
        QuickTestTrace.Log("max", "orphan-park id=" + c.get_data().Id
            + " loc=0x" + old.ToString("X") + "->0x" + c.p.location.ToString("X")
            + " seq=" + seqOld + "->0 father=none");
    }

    /// <summary>
    /// **RD 极大部件的「位置归一」** —— 把「收成素材」MOVE 里带进来的残余区号修成
    /// `怪兽区 | Overlay`（本体那一格），在**唯一入口** <see cref="GCS_cardMove"/> 里调一次。
    ///
    /// 为什么非做不可（2026-09-24，用户三个症状的共同根因）：
    ///   core 的 `Duel.Overlay`（`RDMaximum.lua` 里 `MaximumSummonOperation` 的最后一步）
    ///   发给客户端的 `to.location` 是 **0x82 = Overlay|Hand**（`seq=0`），而极大部件在客户端
    ///   **不是手牌** —— 它常显在本体左右（见 <see cref="maximumPieceWorldPosition"/>）。
    ///   这一个多余的 `0x02` 会同时打穿三处，全是「按 location 分堆」的老代码：
    ///     · L9521 的 `Hand+controller==0 ⇒ isShowed=true` ⇒ 部件排进**展示行**（手卡那排），
    ///       随即被 L9560 的 `showcase-LEAK` 哨兵当帧抓到（补录 32 的清理被它抵消）；
    ///     · `GameMessage.ShuffleHand` 的擦除循环按 `& Hand` 命中它 ⇒ `erase_data()` ⇒
    ///       **卡面变未知卡**，`isMaximumCard()` 随之恒 false ⇒ 三件不齐 ⇒ 大框掉档「变形」；
    ///     · <see cref="maximumPieceWorldPosition"/> 少了怪兽区 ⇒ `Vector3.zero` 垃圾落点。
    ///   与其在每个消费者各打一块补丁（还会漏掉 `get_point_worldcondition` /
    ///   `MHS_*` / `lines` 分组等一串），不如**在入口把值改成对的**：后面每一处读到的
    ///   就都是正常素材口径。
    ///
    /// 归一后的值：`location = MonsterZone|Overlay`（0x84，与客户端自己对普通素材的改写
    ///   `父卡位置 | Overlay` 同一口径，见本函数下面那个 `overlayed_cards_of_cardFrom` 循环）；
    ///   `sequence` 优先取本体那格，拿不到本体时退回 `p_beforeOverLayed.sequence`
    ///   （就是这张部件原来自己站的那一列，反正部件摆位只认 `before`，不认这里）。
    ///
    /// ⚠ 只在命中「IsRD + 极大卡 + 带 Overlay 位 + **没有**怪兽区位 + **这条 MOVE 的起点
    ///   是场上表侧怪兽**」时才有写入：
    ///   · 正常态（已被治好的 0x84）一个字段都不动；
    ///   · OCG 侧 `isMaximumCard` 恒 false（`GameModeManager.IsRD` 门）⇒ 一个字节不变；
    ///   · 普通超量素材本来就不带 Hand 位，也不会进这个分支；
    ///   · **本体离场**那条路（起点是 0x84 的「素材跟着走」MOVE）也不动 —— 见下面那段 ⛔⛔。
    /// </summary>
    private void normalizeRdMaximumPiece(gameCard c, GPS from)
    {
        if (!GameModeManager.IsRD || c == null || !c.gameObject.activeInHierarchy)
        {
            return;
        }
        if ((c.p.location & (UInt32)CardLocation.Overlay) == 0)
        {
            return;      // 没被收成素材，不归这条管
        }
        if (!isMaximumCard(c))
        {
            return;
        }
        if ((c.p.location & (UInt32)CardLocation.MonsterZone) != 0)
        {
            return;      // 已经是「怪兽区|Overlay」，正常态
        }
        // ⛔⛔ 必须看这条 MOVE 的**起点**（2026-09-24 补录 43）：只有「本来是场上表侧怪兽、
        //   现在被收成素材」那一步（起点 = 怪兽区、不带 Overlay）才该改写成「怪兽区|Overlay」。
        //   旧口径只看终点（带 Overlay 位 + 没有怪兽区位），于是把**本体离场**那条路也一起
        //   改写成了「还在场上」—— core 在本体离场时会先给每张部件发一条
        //   `to.location = 本体的新区号 | Overlay`（实测 `0x90 = 墓地|Overlay`）的 MOVE，
        //   起点是 0x84（= 已经是素材）。后果两步：
        //     · 画面：本体走了、两张部件留在场上（被 maximumPieceWorldPosition 按原格位摆住）；
        //     · 更致命：core 紧接着发的「从素材摘到墓地」那条 MOVE 的 `from` 是 `0x90/0/0`，
        //       而客户端手里那份已经被改成 `0x84` ⇒ `GCS_cardGet` 对不上
        //       ⇒ **给同一张卡另建一个对象**塞进墓地，原来那个对象永远留在场上
        //       （用户报的「离场以后场上多出两个被叠放的素材状态的 L/R 部件」）。
        // ✅ 2026-09-24 阴性对照已跑过（`_verify_maxleave_neg3.log`）：与上面那段一起注释掉后
        //   7x7b 变红、还原后转绿 —— 两道闸各自都咬得住。
        if ((from.location & (UInt32)CardLocation.MonsterZone) == 0
            || (from.location & (UInt32)CardLocation.Overlay) != 0)
        {
            return;
        }

        uint seq = (c.p_beforeOverLayed.location & (UInt32)CardLocation.MonsterZone) != 0
            ? c.p_beforeOverLayed.sequence
            : c.p.sequence;
        gameCard father = rdMaximumFather(c);
        if (father != null)
        {
            seq = father.p.sequence;
        }
        uint oldLoc = c.p.location;
        c.p.location = (UInt32)CardLocation.MonsterZone | (UInt32)CardLocation.Overlay;
        c.p.sequence = seq;
        // 留痕：这条一旦出现，说明 core 又发了「Overlay|残余区」的组合（验收可咬）。
        QuickTestTrace.Log("max", "overlay-fix code=" + c.get_data().Id
            + " loc=0x" + oldLoc.ToString("X") + "->0x" + c.p.location.ToString("X")
            + " seq=" + seq + " father=" + (father != null ? father.get_data().Id.ToString() : "none"));
    }

    /// <summary>
    /// 诊断（只在 `log/qt_debug.on` 下写）：RD 极大卡每一条 MOVE 的「起点 / 终点 / 收下之后
    /// 的状态」，外加**同一张卡在客户端有几个对象**（`obj=`）。
    ///
    /// 为什么要 `obj=`：core 那条「把素材摘到墓地」的 MOVE 的 `from` 一旦和客户端手里那份
    /// 对不上，`GCS_cardGet` 会**给同一张卡另建一个对象**（客户端里同 id 出现两份），
    /// 原来那个不动 ⇒ 本体走了、两张部件留在场上 —— 用户 2026-09-24 报的正是这个
    /// （全文见 <see cref="normalizeRdMaximumPiece"/> 与 <see cref="maximumPieceWorldPosition"/>）。
    /// `obj=2` 就是那个 bug 的当场证据；正常态恒为 1。
    /// </summary>
    private void logRdMaximumMove(gameCard c, GPS p1, GPS p2)
    {
        if (!QuickTestTrace.Enabled || !GameModeManager.IsRD || c == null || !isMaximumCard(c))
        {
            return;
        }
        int id = c.get_data().Id;
        int obj = 0;
        for (int i = 0; i < cards.Count; i++)
        {
            if (cards[i] != null && cards[i].gameObject.activeInHierarchy
                && cards[i].get_data().Id == id)
            {
                obj++;
            }
        }
        QuickTestTrace.Log("max", "piece-mv id=" + id
            + " from=0x" + p1.location.ToString("X") + "/" + p1.sequence + "/" + p1.position
            + " to=0x" + p2.location.ToString("X") + "/" + p2.sequence + "/" + p2.position
            + " now=0x" + c.p.location.ToString("X") + "/" + c.p.sequence + "/" + c.p.position
            + " obj=" + obj);
    }

    private static string maxProbeLast = null;

    /// <summary>部件/幽灵卡的**全状态**探针的去重表（按卡号），见 <see cref="logRdMaximumPieceState"/>。</summary>
    private readonly System.Collections.Generic.Dictionary<int, string> maxPieceLastById =
        new System.Collections.Generic.Dictionary<int, string>();

    /// <summary>
    /// 诊断（只在 `log/qt_debug.on` 下写，内容不变只写一次）：**每一张带 Overlay 位的极大卡**
    /// 的完整客户端状态 —— 不复用 `count=` / `offpiece` 的那两套判据（它们各自只看一种形态），
    /// 因为用户 2026-09-24 第二次报的形态还没被复现出来，需要一行能覆盖**任意**形态的物证：
    ///   · `p=` 它自认在哪（`0x84` = 场上素材态 / `0x90` = 墓地|Overlay / 别的）；
    ///   · `before=` 收成素材前站的那一列（摆位靠它）；
    ///   · `father=` 自己那一侧「**本体还在吗**」（严格判据 `rdMaximumBody`：不算素材；
    ///     **0 = 幽灵卡**，任何健康对局都不该出现）；
    ///   · `fLoose=` 老口径（`rdMaximumFather`）的结果 —— 留着是因为这两个字段**不一致**
    ///     本身就是物证：2026-09-24 那个「兜底一次也不归位」的现场就是 `father=0 fLoose=1`
    ///     （两张部件互相认成对方是本体的那个 bug；全文见 `rdMaximumBody`）。
    ///   · `got=` / `want=` 此刻画在哪、被指向哪 —— `want` 指向怪兽区原列就是用户看到的「残留」。
    /// </summary>
    private void logRdMaximumPieceState(gameCard c)
    {
        // ⛔ 旧口径「没带 Overlay 位就直接 return」把**一整族**挡在日志外面：素材态一旦被
        //   别的代码擦掉（少了 Overlay 位），这张卡就再也不出现在任何一条探针里 ——
        //   用户看得见它卡在场上，日志里却一个字都没有（正是第三次报告时的处境）。
        //   ⇒ 改成「**素材**（带 Overlay 位）**或** 场上的极大卡」都报；内容不变只写一次，
        //     所以最多就是三件 + 本体的量级，不会刷屏。
        bool material = (c.p.location & (UInt32)CardLocation.Overlay) != 0;
        bool onField = (c.p.location & (UInt32)CardLocation.MonsterZone) != 0;
        if (!material && !onField)
        {
            return;
        }
        GPS b = c.p_beforeOverLayed;
        Vector3? want = maximumPieceWorldPosition(c);
        string line = "piece id=" + c.get_data().Id
            + " mat=" + (material ? "1" : "0")
            + " p=" + c.p.controller + "/" + c.p.location + "/" + c.p.sequence + "/" + c.p.position
            + " before=" + b.controller + "/" + b.location + "/" + b.sequence + "/" + b.position
            + " over=" + c.overFatherCount
            + " father=" + (rdMaximumBody(c) != null ? "1" : "0")
            + " fLoose=" + (rdMaximumFather(c) != null ? "1" : "0")
            + " cond=" + c.condition
            + " held=" + (c.rdMaxHeld ? "1" : "0")
            + " got=" + probe3(c.gameObject.transform.position)
            + " want=" + (want.HasValue ? probe3(want.Value) : "none");
        int id = c.get_data().Id;
        string last;
        if (maxPieceLastById.TryGetValue(id, out last) && last == line)
        {
            return;
        }
        maxPieceLastById[id] = line;
        QuickTestTrace.Log("max", line);
    }

    // ═════════════ RD 极大怪兽「大框」（用户 2026-09-22）═════════════════════
    //
    // 要求：「极大怪兽召唤时将前场的格子完全隐藏，并把三张极大怪兽无缝拼接，单独做一个大框，
    //      极大怪兽离场以后变回正常状态，要求这个切换必须是无延迟的」。
    //
    // 两件事各归各位：
    //   · **格子隐藏 + 大框** = 场地贴图的事，走 `gameField.setMaximumBand()`（那里有完整说明）；
    //   · **三张贴紧 + 放大** = 摆位的事，就在下面这两个函数里（`realize` 每帧给一次目标）。
    //
    // 为什么「贴紧」和「三件齐」都要放在 `realize` 里算、而不是挂事件：
    // 位置是**每帧重给**的（`UA_give_position` → `UA_flush_all_gived_witn_lock(rush)`），
    // 大框也必须在**同一处**翻档 —— 否则会出现「框开了、卡还没贴上去」或者反过来的错帧。
    // RD 走 rush ⇒ 位置是直接赋值、没有 tween，所以这里天然是「当帧到位」。

    /// <summary>
    /// 极大怪兽三件凑齐后卡片的放大倍率（大框口径）。
    ///
    /// 为什么要放大：实测（`[max]` 探针）卡宽 3.0 世界单位、列距 ≈ 5.87
    /// ⇒ 原尺寸只占格宽的 **51%**，三张之间明晃晃两道空隙 —— 用户说的「有空隙」就是它。
    /// 1.45 倍时：卡宽 4.35、三张合计 13.05（前场三格总宽 ≈ 17.6 的 74%），
    /// 卡高 5.8 / 排距 6.91（84%）⇒ 撑满大框又不压到上下两排。
    ///
    /// ⚠ 它只决定「多大」，不决定「贴不贴得上」：贴合位置由 `rdMaximumFlushPosition`
    ///   按「各自的实际列距收到一张卡宽」算出，改这个值时贴合自动跟着走
    ///   （但会改变留白比例）。
    /// </summary>
    private const float RdMaximumTrioScale = 1.45f;

    /// <summary>「这张卡落稳了」的兜底容差（世界单位，x/y/z 各比一次）—— 出处见 rdMaximumLanded。</summary>
    private const float BandSettleTol = 0.5f;

    /// <summary>
    /// 「这张卡**就是**在目标点上」的容差（世界单位）。取 0.06 有出处：与验收 6q 咬「实到间距
    /// 4.35±0.06」同口径 ⇒ 大框亮的那一帧，三张卡一定是真的贴着，而不是「快贴上了」。
    /// </summary>
    private const float BandSettleExact = 0.06f;

    /// <summary>「这张卡这一帧没动」的阈值（与上一帧的位置差，世界单位）—— 出处见 rdMaximumLanded。</summary>
    private const float BandSettleMoveTol = 0.05f;

    /// <summary>
    /// 三件里某一张在「大框口径」下的目标位置 = 它自己的格位（部件走
    /// <see cref="maximumPieceWorldPosition"/>，本体走原生 `get_point_worldposition`）
    /// 再套一层「贴紧」（<see cref="rdMaximumFlushPosition"/>）。
    ///
    /// ⛔ 运行时摆位（realize 里那段）、落稳判据（rdMaximumLanded）、探针报的 `want=`
    ///   **必须**都走这一个入口。三处只要有一处算式不同，验收判据就会变成
    ///   「got 与 want 不符」那种假报警（6q1/6n 咬的就是它们）。
    /// </summary>
    private Vector3 rdMaximumWantPosition(gameCard c, bool trioMe, bool trioOp)
    {
        Vector3 nativePos = maximumPieceWorldPosition(c) ?? get_point_worldposition(c.p, c);
        return rdMaximumFlushPosition(c, nativePos, trioMe, trioOp);
    }

    /// <summary>
    /// 某一侧场上是不是「极大怪兽三件齐了」—— 中区三列（seq 1/2/3）都站着同一侧的极大怪兽。
    /// 两个阶段都算齐（这正是要的：召唤中途就该是**贴紧 + 放大**的样子，不能等收成素材才变）：
    ///   · 召唤中：本体占中列、L/R 还在自己那两格（location 只有 MonsterZone）；
    ///   · 收成素材后：本体仍在怪兽区，L/R 变成它的素材（location 含 Overlay，格位要读
    ///     `p_beforeOverLayed.sequence`，理由见 maximumPieceWorldPosition）。
    ///
    /// ⛔ 这个「齐」**只许**驱动放大与贴紧（applyRdMaximumTrioScale / rdMaximumFlushPosition），
    ///   **不许**拿去换图：大框要用 `rdMaxSettledMe/Op` 那一档，它多一条「落稳」。
    ///
    /// 2026-09-22 两轮实测踩出来的教训（都在这条线上）：
    ///   ① 直接拿「齐」换图 ⇒ 卡从**场外飞入场**的那 2~3 秒里会亮**错侧**：客户端手里的
    ///      `c.p.controller` 还是**原始值**（没经 localPlayer 映射），造局日志写着
    ///      54.019 三件已「齐」（controller 全是 0）、54.186 才翻成 1 ⇒ 对手极大召唤时
    ///      我方半场先亮 170ms 蓝框。
    ///   ② 给「齐」补一道**看位置**的闸（本体离格位 <1.5）也拦不住，反而更糟：本体飞入时
    ///      **恰好落在 z=-6.75**，而那正是 `get_point_worldposition(controller=0)` 算出来的
    ///      格子 ⇒ 拿同一个错值去算目标，本体当然「已在位」，闸门形同不存在；同时它把放大
    ///      一起打掉了（scale 145→100→145，三张卡当着玩家的面缩一下），贴紧也被推迟。
    ///   结论：「哪一侧」**不需要**分 —— 就用 `c.p.controller`（卡画在哪一侧，框就亮哪一侧）；
    ///     飞行期那 170ms 的「错侧」是**与卡一致的**（卡此刻真画在那一侧），不是错帧。
    ///     ⛔ 别再用「三张落稳」当换图门（第二轮的过度修正，被用户 2026-09-22 第 2 条否掉）。
    /// </summary>
    public bool rdMaximumTrioComplete(int controller)
    {
        rdMaxRefreshTrioState();
        return controller == 0 ? rdMaxTrioMe : rdMaxTrioOp;
    }

    /// <summary>
    /// **极大状态下的本体**这一帧该不该亮「悬停白框」（用户 2026-09-25 第 1 条）。
    /// 由 `gameCard.rdMaxBodyFlashSync` 每帧、以及本体进入 excited 那一帧调用。
    ///
    /// 为什么需要它：极大状态下场上是**三张**卡，而三张的档位不同 ——
    ///   · L/R 部件是 `floating_clickable`（这是刻意的，见 realize 摆位循环那段长注释：
    ///     部件不能带「场上表侧怪兽」的竖立绘/怪兽云/等级），那一档的原生行为就是
    ///     `ES_enter_excited` 里 `flash_line_on()` ⇒ **有白框**；
    ///   · 本体是 `verticle_clickable`（真·场上表侧怪兽），**原生没有白框**。
    /// ⇒ 用户看到的「只有左右两个部件有白框、中间那个没有」。三件既然是一体的，
    ///   框就该一致。
    ///
    /// 条件（两道，缺一不可）：
    ///   ① **本侧三件齐** = 真的处于极大状态 ⇒ 单独在场的极大怪兽**不受影响**
    ///      （那时本体就是普通场上怪兽，原生无框，不许凭空点亮）；
    ///   ② **本体自己被悬停**（<paramref name="pointed"/>，调用方用 ES_mouse_check 给）。
    ///
    /// ⛔⛔ **2026-09-25 晚三修正：`zoom`（一体化放大中）这一半已被用户否掉。**
    ///   原判据是 `trio && (pointed || zoom)`：本意是「悬浮 L/R 时三件一体、中间也该有框」，
    ///   但三张卡各自挂一个框、各贴自己卡面（`MouseFlash` 是**每张卡一份**，见
    ///   gameCard.flash_line_on），三张并排 ⇒ 三条独立边线**视觉上连成一大条**，
    ///   而且「选别的部件时中间也亮」正是用户报的那个现象。
    ///   ⇒ 回归「**光标压着哪张，就只亮哪张**」：只有 `pointed` 才算。
    ///   `zoom` 仍留在诊断行里（判据要看「不是 pointed 就不会亮」这件事，见
    ///   `_verify_rdai_game.py` 7z5/7z7 与 `_verify_maxpiece_col.py` P3b 的**反向**口径）。
    /// ⛔ 只读、无副作用：`rdMaximumTrioComplete` 有帧闩（`rdMaxStateFrame`），
    ///   同帧多调几次不会踩 `rdMaxPrevPos`。
    /// ⛔ 不读设置项 `rdMaxIntegrated_`：这是**极大状态的观感**，不是那个开关的附庸。
    /// </summary>
    public bool RdMaxBodyFrameFlash(int controller, bool pointed)
    {
        bool inRange = GameModeManager.IsRD && controller >= 0 && controller <= 1;
        bool trio = inRange && rdMaximumTrioComplete(controller);
        bool zoom = inRange && rdMaxZoomOwn(controller) && rdMaxZoomK(controller) > 0.01f;
        bool on = trio && pointed;
        // 诊断（仅 qt_debug.on）：本侧判定结果**变化时**落一行 —— 验收咬它，别拿帧数当判据。
        // ⛔ 去重键**不含 k**（k 每帧都在动，带上它就成了逐帧刷屏）；k 只作为当时的参考值打出来。
        if (QuickTestTrace.Enabled && inRange)
        {
            // ⛔⛔ 去重键**不含 k**：k 每帧都在动（12/s 朝目标收敛），把它拼进去就等于逐帧落行
            //   —— 2026-09-25 实测一局刷出 159 条，全是同一个判定态、只有 k 在变。
            //   判定态（on/trio/pointed/zoom）才是判据关心的东西；k 只作为**落行那一刻**的
            //   参考值打出来（7z5/7z6/7z7 三条判据都不咬 k，见 _verify_rdai_game.py）。
            string key = "on=" + (on ? "1" : "0") + " trio=" + (trio ? "1" : "0")
                + " pointed=" + (pointed ? "1" : "0") + " zoom=" + (zoom ? "1" : "0");
            string line = "bodyflash ctrl=" + controller + " " + key
                + " k=" + rdMaxZoomK(controller).ToString("0.00");
            if (controller == 0)
            {
                if (key != rdMaxBodyFlashLast0) { rdMaxBodyFlashLast0 = key; QuickTestTrace.Log("max", line); }
            }
            else
            {
                if (key != rdMaxBodyFlashLast1) { rdMaxBodyFlashLast1 = key; QuickTestTrace.Log("max", line); }
            }
        }
        return on;
    }

    /// <summary>`[max] bodyflash` 的每侧去重（键里不含 k，理由见 RdMaxBodyFrameFlash）。</summary>
    private string rdMaxBodyFlashLast0 = "", rdMaxBodyFlashLast1 = "";
    /// <summary>
    /// 本帧「极大怪兽三件」的两个判据，每侧各一位 —— **谁要都读这几个字段**：
    ///   · <c>齐</c>（Trio）＝ 中区三列（seq 1/2/3）站着同一侧的三件。**不看位置**。
    ///     两个阶段都算齐（这正是要的：召唤中途就该是**贴紧 + 放大**的样子，不能等收成素材才变）：
    ///       召唤中：本体占中列、L/R 还在自己那两格（location 只有 MonsterZone）；
    ///       收成素材后：本体仍在怪兽区，L/R 变成它的素材（location 含 Overlay，格位要读
    ///       `p_beforeOverLayed.sequence`，理由见 maximumPieceWorldPosition）。
    ///     它**只许**驱动放大与贴紧（applyRdMaximumTrioScale / rdMaximumFlushPosition）。
    ///   · <c>落稳</c>（Settled）＝ 三张都到位、落平、**而且这一帧不再移动**。换图只认它。
    ///
    /// ⛔ **每帧只能算一次**：`落稳` 里那条「这一帧没动」要跟**上一帧**的位置比（见
    ///   rdMaximumLanded），同一帧算两遍会把「上一帧位置」踩成「这一帧位置」⇒ 永远判成在动、
    ///   大框永远不亮。所以统一走 <see cref="rdMaxRefreshTrioState"/>，同帧重复调用直接吃缓存。
    ///
    /// 2026-09-22 两轮实测踩出来的教训（为什么换图不能只看「齐」）：
    ///   ① 直接拿「齐」换图 ⇒ 卡从**场外飞入场**的那 2~3 秒里会亮**错侧**：客户端手里的
    ///      `c.p.controller` 还是**原始值**（没经 localPlayer 映射 —— 造局那局本机视角是
    ///      player 1，所以对手那三件的原始值是 0），造局日志写着 54.019 三件已「齐」
    ///      （controller 全是 0）、54.186 才翻成 1 ⇒ 对手极大召唤时我方半场先亮 170ms 蓝框。
    ///   ② 给「齐」补一道**看位置**的闸（本体离它格位 <1.5 世界）也拦不住，反而更糟：本体
    ///      飞入时**恰好落在 z=-6.75**，而那正是 `get_point_worldposition(controller=0)` 算
    ///      出来的格子 ⇒ 拿同一个错值去算目标，本体当然「已在位」，闸门形同不存在；同时它把
    ///      放大一起打掉了（scale 145→100→145，三张卡当着玩家的面缩一下）、贴紧也被推迟。
    ///   结论：飞行期「本体位置」与错侧是**自洽**的，分不出真假；能分出来的是「另两张还在
    ///   半空」（54.174 两件部件都已收成素材时，第三张 `got=(-370,182,1033)` 还在飞）。
    ///   这就是 `落稳` 的三条判据。
    /// </summary>
    private bool rdMaxTrioMe, rdMaxTrioOp;
    private bool rdMaxSettledMe, rdMaxSettledOp;
    private int rdMaxStateFrame = -1;

    // ═════════ RD 极限召唤「三件同帧入场」的暂存（用户 2026-09-24 第 2 条）═════════════
    //
    // 要求：「让三个部件看起来是同时进入场地的」。
    //
    // 现实：极限召唤的三张卡是**三条独立 MOVE** 送来的（实测 log/qt_6128.log，我方那次）：
    //     18.805 L(120150001) hand→MZ seq1（TweenTo 飞过去）
    //     18.946 R(120150003) hand→MZ seq3
    //     19.125 L → Overlay
    //     19.126 本体(120150002) hand→MZ seq2
    //     19.128 R → Overlay
    //   每条 MOVE 都走 `UA_flush_all_gived_witn_lock(false)` ⇒ `TweenTo` ⇒ 三张卡一前一后
    //   飞进场（各 ~0.19s），看起来是三次独立召唤，而不是「极大召唤」这一个动作。
    //
    // 做法：**把先到的部件按住**（钉在原处、缩到不可见），等本体那一条 MOVE 进来的那一帧
    //   一起放行 —— 三张卡从同一处、同一时长飞出去，同时落位；大框也在同一帧亮。
    //
    // 为什么「同一帧」成立：`sibyl()` 一条一条跨帧喂消息，而 `practicalizeMessage` 的
    //   `case GameMessage.Move: realize();` 是**每条 MOVE 当帧 realize**（见 6726 行）。
    //   本函数就在 `realize` 的摆位循环之前跑 ⇒ 本体到场的那一帧，放行与摆位落在同一次 realize 里。
    //
// ⚠ 起手判据必须**精确**（不能只看「有部件没本体」）：被效果从墓地复活的极大怪兽
//   也满足「极大卡 + 中区左右列 + 没有本体」，误判会让它凭空消失一下。
//   ⇒ 只认**极限召唤的签名**：`GCS_cardMove` 里 p1 来自**手牌**、p2 落到**怪兽区中区左右列**
//      （`MaximumSummonOperation` 的 `MoveToField(left,0x2)` / `(right,0x8)`），
//      且这张卡**不是**本侧最近被宣告召唤的那张（rdMaxSummonDeclared —— 单独召唤/
//      特招部件时宣告码就是它自己，闸门放行；全文见 GCS_cardMove 签名那段）。
    //
    // ⚠ 兜底：本体真要是不来（召唤被无效 / 中途出事），`RdMaxStageHold` 秒后自动放行，
    //   不会把卡永久按在手里。
    private int rdMaxStageMask = 0;             // bit0 = 我方，bit1 = 对手
    private float rdMaxStageDeadline = 0f;
    private const float RdMaxStageHold = 0.7f;

    // 🔑 「本侧正在被召唤/特招宣告的那张卡」（存卡码，0 = 无宣告）。
    //   OCG/RD core 的召唤流程恒为「先发 SUMMONING / SP_SUMMONING（带**被召唤卡自己**
    //   的码），再发那条 MOVE」。极大召唤是唯一的例外：`AddHandSpecialSummonProcedure`
    //   特招的是**本体**（宣告码 = 本体码），L/R 是 operation 里 `Duel.MoveToField`
    //   直接拉下去的，**没有自己的宣告**。于是「这条 MOVE 的卡 ≠ 本侧最近宣告的卡」
    //   就是「它是被极大召唤顺带放下的部件」的强签名 —— 单独召唤/特招部件时宣告码
    //   就是部件自己，闸门自然放行（用户 2026-09-24 晚报的「非极大召唤时进场也会
    //   消失」「单个部件没有立绘」，两处的共同钥匙）。
    //   SUMMONED / SP_SUMMONED 到达即全清：消息不带参（清两侧），且极大召唤的所有
    //   部件 MOVE 恒在 SP_SUMMONED **之前**（实测 qt_6128：18.805 L → 19.128 R→Overlay
    //   → SP_SUMMONED），清早了会漏。只在 IsRD 下写：OCG 侧连数组都恒 0。
    private readonly int[] rdMaxSummonDeclared = new int[2];

    /// <summary>SUMMONING/SP_SUMMONING 解析到卡 ⇒ 记下「本侧正在宣告召唤的卡码」（由消息循环调）。</summary>
    private void rdMaxNoteSummonDeclared(int code, gameCard card)
    {
        if (QuickTestTrace.Enabled)
        {
            // 诊断（仅 qt_debug.on）：宣告码锚的每次写入与**每次早退**（写不上也要留痕 ——
            // 2026-09-24 晚 SINGLE 局 decl=0 无法只从日志断定「宣告没来」还是「来了没记上」）。
            string why = "ok";
            if (!GameModeManager.IsRD) why = "nord";
            else if (card == null) why = "null";
            else if (card.p.controller > 1) why = "ctrl" + card.p.controller;
            QuickTestTrace.Log("max", "decl-note code=" + code + " ctrl=" + (card == null ? "?" : card.p.controller.ToString()) + " why=" + why);
        }
        if (!GameModeManager.IsRD || card == null || card.p.controller > 1)
        {
            return;
        }
        rdMaxSummonDeclared[card.p.controller] = code;
        // 🔑 RD core 的 SpecialSummon 消息序与 OCG **相反**：先 MOVE 后宣告（qt_13944 实测：
        //   49.686 MOVE → 49.808 SP_SUMMONING，间隔 122ms）⇒ 单独特招 L/R 时，签名闸先用
        //   decl=0 命中、把卡按进暂存，宣告**迟到** 122ms。这里补一手：宣告到达时若该侧
        //   暂存掩码已置位、且宣告的正是「在场 seq1/3、无 Overlay 位」的极大卡自己
        //   （= 被误咬的那个部件），立刻清该侧掩码放行 —— 下一帧 rdMaxApplyStageHold
        //   就会把卡放掉（卡被按住的总时长 ≈ 消息间隔，肉眼不可见）。
        //   极大召唤不受影响：SP_SUMMONING 宣告的是**本体**、且发生在 L/R 的 MOVE **之前**
        //   （那一刻掩码还没置位）；宣告码也永远对不上在场 seq1/3 部件的码。
        int rdCtrl = (int)card.p.controller;
        if ((rdMaxStageMask & (1 << rdCtrl)) != 0
            && (card.p.location & (UInt32)CardLocation.MonsterZone) != 0
            && (card.p.location & (UInt32)CardLocation.Overlay) == 0
            && (card.p.sequence == 1 || card.p.sequence == 3))
        {
            rdMaxStageMask &= ~(1 << rdCtrl);
            if (QuickTestTrace.Enabled)
            {
                QuickTestTrace.Log("max", "decl-release id=" + code + " ctrl=" + rdCtrl);
            }
        }
    }

    /// <summary>SUMMONED/SP_SUMMONED 到达 ⇒ 清掉两侧宣告（消息不带参；部件 MOVE 恒在它之前）。</summary>
    private void rdMaxClearSummonDeclared()
    {
        rdMaxSummonDeclared[0] = 0;
        rdMaxSummonDeclared[1] = 0;
    }

    /// <summary>极限召唤签名命中 ⇒ 这一侧进入「等本体」暂存（由 GCS_cardMove 调）。</summary>
    private void rdMaxStageBegin(UInt32 controller)
    {
        if (controller > 1)
        {
            return;
        }
        rdMaxStageMask |= 1 << (int)controller;
        rdMaxStageDeadline = Time.time + RdMaxStageHold;
    }

    /// <summary>本帧「本体已经站在中列（seq 2）」的侧别掩码 —— 这些侧本帧就该放行。</summary>
    private int rdMaxFatherPresentMask()
    {
        int mask = 0;
        for (int i = 0; i < cards.Count; i++)
        {
            gameCard c = cards[i];
            if (c == null || !c.gameObject.activeInHierarchy || !isMaximumCard(c))
            {
                continue;
            }
            if ((c.p.location & (UInt32)CardLocation.MonsterZone) == 0
                || (c.p.location & (UInt32)CardLocation.Overlay) != 0)
            {
                continue;
            }
            if (c.p.sequence == 2 && c.p.controller <= 1)
            {
                mask |= 1 << (int)c.p.controller;
            }
        }
        return mask;
    }

    /// <summary>这张卡现在该不该被按进暂存位（部件还没收成素材、或刚收成素材的那一段）。</summary>
    private bool rdMaxShouldHold(gameCard c, int mask)
    {
        if (c == null || !c.gameObject.activeInHierarchy || !isMaximumCard(c))
        {
            return false;
        }
        if (c.p.controller > 1 || (mask & (1 << (int)c.p.controller)) == 0)
        {
            return false;
        }
        if ((c.p.location & (UInt32)CardLocation.Overlay) != 0)
        {
            // 🔑 只按「还在场上」的素材：极大召唤 operation 开头 `SendtoGrave(fg)` 会把本侧
            //   场上怪（含**旧合体的素材**）先送墓，那些卡带着 Overlay 位落到墓地
            //   （`0x90 = Grave|Overlay`）—— 它们不在进场路上，按住只会让墓地堆里那张
            //   凭空缩没 0.7s（2026-09-24 晚核对签名时顺手加固）。
            return (c.p.location & (UInt32)CardLocation.MonsterZone) != 0;      // 已收成素材的部件（场上）
        }
        return (c.p.location & (UInt32)CardLocation.MonsterZone) != 0
            && (c.p.sequence == 1 || c.p.sequence == 3);
    }

    /// <summary>
    /// 每帧（`realize` 摆位之前）跑一遍：先把该放行的放开，再把该按的按在原处。
    /// 放行只清标记 + 还原 scale —— 位置由紧随其后的摆位循环给，于是三张卡同帧起飞。
    /// </summary>
    private void rdMaxApplyStageHold()
    {
        if (rdMaxStageMask != 0)
        {
            rdMaxStageMask &= ~rdMaxFatherPresentMask();       // 本体到场的侧：本帧放行
            if (Time.time > rdMaxStageDeadline)
            {
                rdMaxStageMask = 0;                            // 兜底：本体没来，别把卡按住不放
            }
        }
        for (int i = 0; i < cards.Count; i++)
        {
            gameCard c = cards[i];
            if (c == null || !c.rdMaxHeld)
            {
                continue;
            }
            if (rdMaxShouldHold(c, rdMaxStageMask))
            {
                continue;
            }
            c.UA_rdMaxRelease();
        }
        if (rdMaxStageMask == 0)
        {
            return;
        }
        for (int i = 0; i < cards.Count; i++)
        {
            gameCard c = cards[i];
            if (!rdMaxShouldHold(c, rdMaxStageMask))
            {
                continue;
            }
            QuickTestTrace.Log("max", "stage-hold id=" + c.get_data().Id
                + " seq=" + c.p.sequence + " decl=" + rdMaxSummonDeclared[(int)c.p.controller]);
            c.UA_rdMaxHold(c.gameObject.transform.position);
        }
    }

    /// <summary>
    /// RD 极大部件是不是挂在 <paramref name="father"/>（本体）名下的素材。
    ///
    /// 为什么非要有这个判据：`GCS_cardGetOverlayCount` 认「谁是素材」用的是 OCG 口径
    /// `素材.sequence == 父卡.sequence` —— OCG 里素材确实与父卡同格。
    /// **RD 极大部件不是**：极限召唤把 L/R 放在中区左右两列（seq 1/3）、本体落中列（seq 2），
    /// 而客户端对素材的 sequence 对齐（`overlayed_cards_of_cardX[i].p.sequence = cardX.p.sequence`）
    /// 对 RD 部件**不生效** —— 它上游 `GCS_cardGetOverlayElements` 用同一条 sequence 判据筛，
    /// 列表恒空。⇒ `GCS_cardGetOverlayCount(本体)` **恒为 0**（实测 log/qt_6128.log 的探针
    /// `over=0`、部件 `p=0/132/1/0` 对本体的 `p=0/4/2/1`）⇒ 后果就是 gameCard 那条
    /// 「极大状态不显示守备力」的闸门（`loaded_verticalmaxstate`）从来没成立过：
    /// 极限状态照样画着 `800/0`（用户 2026-09-24 第 1 条）。
    ///
    /// ⛔ 只改 `GCS_cardGetOverlayCount`，**不动** `GCS_cardGetOverlayElements`：后者喂给
    ///   `GCS_cardMove` 的素材重规整循环（`overlayed_cards_of_cardFrom/To[i].p.location = …`），
    ///   把 RD 部件放进那个列表会顺带改写它们的 location / sequence / position
    ///   （本体离场那条路上还会先被写成 `墓地|Overlay` 一帧）—— 而它对表现层没有任何收益
    ///   （`set_overlay_light` / `set_overlay_see_button` 对极大本体本来就恒关，见 10263 行那段）。
    ///   计数版的**唯一**调用点就是那条守备力闸门 ⇒ blast radius 最小。
    ///
    /// 判据改用部件自己的「旧格位」`p_beforeOverLayed`（= 极限召唤里 `MoveToField` 把它放下的
    /// 那一列），与 <see cref="rdMaximumFather"/> / <see cref="isRdMaximumPiecePending"/> /
    /// <see cref="rdMaximumTrioState"/> 同一口径：
    ///   ① 双方都是极大卡；② 同控制者；③ 部件带 Overlay 位；
    ///   ④ 本体的位置是「怪兽区、不带 Overlay」；⑤ 部件的旧格位来自中区三列。
    /// ⑤ 同时把「手牌/墓地直接当素材贴上去」的卡排除掉（那种卡没有这一段旧格位）—— 与
    /// <see cref="maximumPieceWorldPosition"/> 的守卫同源 ⇒ 只认极限召唤那一套。
    /// ⚠ 只在 `GameModeManager.IsRD` 下成立 ⇒ OCG 侧一个字节不变。
    /// </summary>
    private bool isRdMaximumMaterialOf(gameCard material, gameCard father)
    {
        if (!GameModeManager.IsRD || material == null || father == null || material == father)
        {
            return false;
        }
        if (!isMaximumCard(material) || !isMaximumCard(father))
        {
            return false;
        }
        if ((material.p.location & (UInt32)CardLocation.Overlay) == 0)
        {
            return false;
        }
        if (material.p.controller != father.p.controller)
        {
            return false;
        }
        if ((father.p.location & (UInt32)CardLocation.MonsterZone) == 0
            || (father.p.location & (UInt32)CardLocation.Overlay) != 0)
        {
            return false;
        }
        if ((material.p_beforeOverLayed.location & (UInt32)CardLocation.MonsterZone) == 0)
        {
            return false;
        }
        uint q = material.p_beforeOverLayed.sequence;
        return q >= 1 && q <= 3;
    }

    /// <summary>
    /// 算一遍三件状态。**`realize` 必须传 `force: true`**，只有探针（同一调用里跟在后面）吃缓存。
    ///
    /// ⚠ 为什么按 `realize` 调用失效、而不是按帧：一帧里可能进来**两条**核心消息、`realize` 被调
    ///   两次，而三件状态正好在这两条之间翻边（实测 2026-09-22：11:30.077 与 11:30.088 同帧，
    ///   第一条时 controller 还是 0、第二条已经翻成 1）。按帧缓存会把**翻边前**的状态喂给第二条
    ///   ⇒ 放大当帧掉回 100（`145→100→145`，一次 17ms 的缩小闪）、贴紧也晚一帧。
    /// ⚠ 代价：同帧第二次调用时 `rdMaxPrevPos` 会被写成当前值（`rdMaximumLanded` 里那条「这一帧
    ///   没动」退化成「没动」）。不碍事 —— 还有「到位 + 落平」两条拦着，飞行途中一定不成立。
    /// </summary>
    private void rdMaxRefreshTrioState(bool force = false)
    {
        if (!force && rdMaxStateFrame == Time.frameCount)
        {
            return;
        }
        rdMaxStateFrame = Time.frameCount;
        rdMaxTrioMe = rdMaximumTrioState(0, out rdMaxSettledMe);
        rdMaxTrioOp = rdMaximumTrioState(1, out rdMaxSettledOp);
    }

    /// <summary>返回值 = 齐；<paramref name="allSettled"/> = 三张都落稳了（判据见 rdMaximumLanded）。</summary>
    private bool rdMaximumTrioState(int controller, out bool allSettled)
    {
        allSettled = false;
        if (!GameModeManager.IsRD)
        {
            return false;
        }
        bool s1 = false, s2 = false, s3 = false;
        bool anyFar = false;
        for (int i = 0; i < cards.Count; i++)
        {
            gameCard c = cards[i];
            if (c == null || !c.gameObject.activeInHierarchy)
            {
                continue;
            }
            if (!isMaximumCard(c) || (int)c.p.controller != controller)
            {
                continue;
            }
            uint q;
            if ((c.p.location & (UInt32)CardLocation.Overlay) != 0)
            {
                // ⛔⛔ 同 rdMaxResolveTrio 那一闸：`0x90 = 墓地|Overlay` 也带 Overlay 位，
                //   不加「在不在场上」这一条，本体离场后三件仍报「齐」⇒ 大框不掉、
                //   三张仍按大框口径放大贴紧（用户 2026-09-25 第 4 次报告，见 rdMaxPieceOnField）。
                if (!rdMaxPieceOnField(c))
                {
                    continue;
                }
                q = c.p_beforeOverLayed.sequence;
                if ((c.p_beforeOverLayed.location & (UInt32)CardLocation.MonsterZone) == 0)
                {
                    continue;      // 被别的效果直接当素材贴上去的：没有「旧格位」，不算一列
                }
            }
            else if ((c.p.location & (UInt32)CardLocation.MonsterZone) != 0)
            {
                q = c.p.sequence;
            }
            else
            {
                continue;          // 手牌/墓地/卡组/额外里躺着的极大怪兽不算
            }
            if (q == 1) s1 = true;
            else if (q == 2) s2 = true;
            else if (q == 3) s3 = true;
            if (!rdMaximumLanded(c, controller))
            {
                anyFar = true;
            }
        }
        bool trio = s1 && s2 && s3;
        allSettled = trio && !anyFar;
        return trio;
    }

    /// <summary>
    /// 这一张是不是**已经落稳在大框口径的目标位置**上。
    ///
    /// 先要弄清引擎有**两条**摆位路径（`gameCard.UA_flush_all_gived_witn_lock`）：
    ///   · `rush=true`（核心消息驱动的那几条：realize(true)）⇒ 位置**瞬间赋值**，当帧到位；
    ///   · `rush=false`（gameHiddenButton / DuelUndo 那条）⇒ `TweenTo`，位置**缓动**过去。
    /// 所以判据写成「快判 + 兜底」两段：
    ///
    ///   · **快判 `就是目标点`**：|Δx|、|Δz|、|Δy| ≤ <see cref="BandSettleExact"/>（0.06，与验收
    ///     6q 同口径）⇒ 直接算落稳。瞬时赋值那条路一进来就命中 ⇒ **零延迟**，而且大框亮的那一帧
    ///     三张卡一定真的贴着（6q 量的是实到间距 4.35±0.06）。
    ///   · **兜底 `到位 + 落平 + 不再动`**：缓动那条路收敛后可能停在离目标几个像素的地方，
    ///     上面那条永远不成立 ⇒ 退到：|Δx|、|Δz|、|Δy| ≤ <see cref="BandSettleTol"/>（0.5）
    ///     **且**这一帧与上一帧的位置差 ≤ <see cref="BandSettleMoveTol"/>。
    ///     飞行途中这三条都不成立：离目标还有 1.4 以上（实测 1.48 / 1.58）、y 抬到 1~2.4
    ///     （实测 1.38 / 1.82 / 2.37）而且一直在动。**「落平」不能省**：从手牌飞向对面那排时
    ///     会从目标正上方穿过，那一瞬的水平重合会把判据骗过去。
    ///
    /// ⚠ 目标点必须与 `realize` 真给出去的那个是**同一个表达式** —— 走
    ///   <see cref="rdMaximumWantPosition"/>，否则判据会变成「got 与 want 不符」的假报警。
    /// ⚠ 有副作用（写 <c>rdMaxPrevPos</c>）⇒ **每帧每张卡只能调一次**，见 rdMaxRefreshTrioState。
    /// </summary>
    private bool rdMaximumLanded(gameCard c, int controller)
    {
        bool me = controller == 0;
        Vector3 want = rdMaximumWantPosition(c, me, !me);
        Vector3 got = c.gameObject.transform.position;
        float dx = Math.Abs(got.x - want.x);
        float dz = Math.Abs(got.z - want.z);
        float dy = Math.Abs(got.y - want.y);
        bool moved = c.rdMaxPrevSet
            && (got - c.rdMaxPrevPos).sqrMagnitude > BandSettleMoveTol * BandSettleMoveTol;
        c.rdMaxPrevPos = got;
        c.rdMaxPrevSet = true;
        if (dx <= BandSettleExact && dz <= BandSettleExact && dy <= BandSettleExact)
        {
            return true;                       // 快判：已经是目标点了
        }
        if (moved)
        {
            return false;                      // 还在飞/还在缓动
        }
        return dx <= BandSettleTol && dz <= BandSettleTol && dy <= BandSettleTol;
    }

    /// <summary>
    /// 把极大怪兽三件里的一张摆到「贴紧」后的位置：**把它与中列之间那段距离收到「正好一张卡宽」**。
    ///
    ///   x_target = x_native + dir × (|x_native - x_center| - 卡宽)
    ///
    /// ⇒ 相邻两张的中心距 = 卡宽 ⇒ 边缘严丝合缝（这就是「无缝拼接」）。本体在中列，距离为 0，天然不动。
    ///
    /// ⚠ 不能写成「偏移 × 某个比例」：中区三列的**间距本来就不等**
    ///   （`rdColumnX` = -5.17 / -0.27 / 4.72 ⇒ 两段分别是 4.90 与 4.99 世界单位），
    ///   按比例缩出来一边贴 4.35、另一边贴 4.27，缝还是看得见（2026-09-22 实测 6q 抓到）。
    ///   按「各自的实际间距」收才两边都严丝合缝。
    ///
    /// ⚠ 也不能写成「±卡宽」那种固定值：我方与对手的列序是**反的**
    ///   （`rdColumnX` 里 op 走 `4-seq`），写死必然有一侧贴反、一半在对面半场
    ///   （这条线上踩过一次同类坑，见 maximumPieceWorldPosition 里那条 ⚠⚠）。
    ///
    /// ⚠ 必须先筛「在场上」：同侧三件齐了的时候，那侧墓地/卡组里可能还躺着别的极大怪兽，
    ///   它们的 x 是牌堆那一列（±10 上下），套这个变换会被硬拽到中场上来。
    /// </summary>
    private Vector3 rdMaximumFlushPosition(gameCard c, Vector3 nativePos, bool trioMe, bool trioOp)
    {
        if (!GameModeManager.IsRD || c == null || !isMaximumCard(c))
        {
            return nativePos;
        }
        bool op = c.p.controller != 0;
        if (op ? !trioOp : !trioMe)
        {
            return nativePos;
        }
        bool isPiece = rdMaxPieceOnField(c);
        bool inZone = (c.p.location & (UInt32)CardLocation.MonsterZone) != 0;
        if (!isPiece && !(inZone && c.p.sequence >= 1 && c.p.sequence <= 3))
        {
            return nativePos;
        }
        float real = (Program.fieldSize - 1) * 0.9f + 1f;
        float centerX = rdColumnX(2, op) * real;
        float cardW = 3f * RdMaximumTrioScale;      // 卡面 Quad(1x1) × face localScale.x(3) × 倍率
        float gap = Math.Abs(nativePos.x - centerX);
        float dir = centerX > nativePos.x ? 1f : (centerX < nativePos.x ? -1f : 0f);
        return new Vector3(nativePos.x + dir * (gap - cardW), nativePos.y, nativePos.z);
    }

    /// <summary>
    /// 三件齐 ⇒ 把这三张卡放大到 <see cref="RdMaximumTrioScale"/>；凑不齐/离场 ⇒ 还原成 1。
    /// 只碰「自己放大过的那几张」（<see cref="gameCard.rdMaxTrioScaled"/>）—— 卡根的 scale
    /// 有自己的创建动画，无脑每帧写就会把它踩掉。
    /// </summary>
    private void applyRdMaximumTrioScale(gameCard c, bool trioMe, bool trioOp)
    {
        bool want = false;
        if (GameModeManager.IsRD && c != null && isMaximumCard(c))
        {
            bool op = c.p.controller != 0;
            // ⛔ 部件那一支必须用 rdMaxPieceOnField（「还挂着素材态」**且**「还在场上」）：
            //   只问 Overlay 位的话，归位到 0x90 的部件仍会被撑成 1.45 倍并长期不还原
            //   （用户 2026-09-25 第 4 次报告，见 rdMaxPieceOnField）。
            bool isPiece = rdMaxPieceOnField(c);
            bool inZone = (c.p.location & (UInt32)CardLocation.MonsterZone) != 0;
            want = (op ? trioOp : trioMe)
                && (isPiece || (inZone && c.p.sequence >= 1 && c.p.sequence <= 3));
        }
        if (want)
        {
            c.gameObject.transform.localScale =
                new Vector3(RdMaximumTrioScale, RdMaximumTrioScale, RdMaximumTrioScale);
            c.rdMaxTrioScaled = true;
        }
        else if (c.rdMaxTrioScaled)
        {
            c.gameObject.transform.localScale = Vector3.one;
            c.rdMaxTrioScaled = false;
        }
    }

    private static string probe3(Vector3 v)
    {
        return "(" + ((int)Math.Round(v.x * 100.0)) + "," + ((int)Math.Round(v.y * 100.0))
            + "," + ((int)Math.Round(v.z * 100.0)) + ")";
    }

    // ═════════════ 极大怪兽一体化（设置项 rdMaxIntegrated_，仅 RD、默认开）═════════════
    //
    // 需求（用户 2026-09-25）：**三件齐**的极大怪兽被鼠标悬浮时，三个部件**不再各自单独放大**，
    //   而是「三件 + 极大立绘」**视为一体**一起放大；**单独在场**的极大怪兽不受影响；
    //   点 L/R 部件等效点中间件（卡面点击转发 → RdMaximumClickTarget；悬浮 L/R 时浮出本体按钮）；
    //   左侧说明栏保持「显示你悬浮的那张部件」自己的资料。
    //
    // 为什么这么实现（都从既有代码里核实过，见各自的注释）：
    //   · 三件是**三个独立 gameCard**，卡根没有共同父（create 不带 father）⇒ 统一放大只能走
    //     **屏幕空间以本体屏点为轴的等比缩放**，不能挂父节点、更不能改 scale。
    //   · 「悬浮放大」本来就不是写 scale，而是 close-up：把被悬浮那张卡沿相机视线**拉近 10**。
    //     本功能就是把这套「拉近」改成「三张共用一个放大倍率 m、以本体屏点 C 为轴」——
    //     m 从本体算，三张共用 ⇒ 彼此仍严丝合缝（间距与卡宽同时 ×m）。
    //   · 立绘每帧由 `card_verticle_drawing_handler` 挂在**卡面位置**上、世界尺寸恒定
    //     （`Program.verticleScale`，与世界深度无关）⇒ 卡面朝镜头前移时立绘自动同步放大，
    //     **立绘一句代码都不用改**。
    //   · ⛔ `localScale` 已被三方占用（本体 1.45 / 手牌行 / 暂存 0.001）⇒ 全程**只写 position**。
    //   · ⛔ 不调 `rdMaxRefreshTrioState`（有副作用：写 `rdMaxPrevPos`）、也不看 `rdMaxSettled*`
    //     （落稳判据拿「这一帧没动」与上一帧比，卡被我们挪动后会永久判成在动 ⇒ 用它会自毁）
    //     —— 这里**自己扫 cards** 出一份无副作用的「三件」快照。
    //   · ⛔ 判本体必须用严格口径（同侧 + 带怪兽区位 + **不带 Overlay**）；绝不用
    //     `rdMaximumFather`（只要求 seq==2，而两张部件的 `p.sequence` 也是 2 ⇒ 会互认）。

    /// <summary>一体放大把整组「朝镜头拉近」多少世界单位 —— 与 gameCard 现有 close-up 的 10
    /// 同口径 ⇒ 观感与「单卡查看」一致（m = d / (d - 10)；d ≈ 33 时 m ≈ 1.45，即既有 1.45 档）。</summary>
    private const float RdMaxIntegratedPull = 10f;

    /// <summary>收敛速度（1/s）：k 每帧朝目标插值，约 0.5s 到位的观感（与既有 close-up 同量级）。</summary>
    private const float RdMaxIntegratedGain = 12f;

    /// <summary>收敛系数（每侧一份）：0 = 完全在原位，1 = 放大到位。</summary>
    private float rdMaxZoomKMe, rdMaxZoomKOp;

    /// <summary>本帧查到的三件（[0]=本体、[1]=L、[2]=R）；[0]==null 表示这一侧没有成立的三件。</summary>
    private gameCard[] rdMaxZoomCardsMe = new gameCard[3];
    private gameCard[] rdMaxZoomCardsOp = new gameCard[3];

    /// <summary>本帧这一侧是否**正在占用**这三张卡的位置（= 我们写过位置，k&gt;0）。
    /// gameCard 的 close-up / exit 用它早退，避免与本 tick 抢位置。</summary>
    private bool rdMaxZoomOwnMe, rdMaxZoomOwnOp;

    /// <summary>本帧共用的放大倍率（从本体算）—— 探针用。</summary>
    private float rdMaxZoomMMe = 1f, rdMaxZoomMOp = 1f;

    /// <summary>`[maxzoom]` 探针去重（每侧一份）：内容没变就不落行。</summary>
    private string rdMaxZoomProbeLast0 = "", rdMaxZoomProbeLast1 = "";

    private gameCard[] rdMaxZoomArr(int side) { return side == 0 ? rdMaxZoomCardsMe : rdMaxZoomCardsOp; }
    private float rdMaxZoomK(int side) { return side == 0 ? rdMaxZoomKMe : rdMaxZoomKOp; }
    private void rdMaxZoomSetK(int side, float k) { if (side == 0) rdMaxZoomKMe = k; else rdMaxZoomKOp = k; }
    private bool rdMaxZoomOwn(int side) { return side == 0 ? rdMaxZoomOwnMe : rdMaxZoomOwnOp; }
    private void rdMaxZoomSetOwn(int side, bool v) { if (side == 0) rdMaxZoomOwnMe = v; else rdMaxZoomOwnOp = v; }

    /// <summary>设置项（键 `rdMaxIntegrated_`，仅 RD 可见、**默认开**）。每帧实时读 Config，
    /// 不缓存、不订阅 —— 与 `askMset_` 那条同一口径（改完立刻生效，不必重开窗口）。</summary>
    private bool rdMaxIntegratedOn()
    {
        return GameModeManager.IsRD
            && UIHelper.fromStringToBool(Config.Get("rdMaxIntegrated_", "1"));
    }

    /// <summary>
    /// 「极大怪兽一体化」的**每帧入口**（挂在 <see cref="preFrameFunction"/> 里，见那里的调用点）。
    ///
    /// 时序：`Program.Update` 先把本帧的 `pointedGameObject` 算好，再跑 `preFrameFunction`，
    /// 最后才逐个 `gameCard.Update`（close-up / button shower 都在那里）⇒ 本 tick 处在
    /// 「最新鼠标命中之后、所有卡表现之前」，所以它写的位置与置的托管标记当帧就会被读到。
    /// 只在 RD 生效、`cards.Count == 0`（菜单 / 加载中）或非决斗态直接早退 ⇒ OCG 一个像素不变。
    /// </summary>
    public void rdMaxIntegratedTick()
    {
        if (!GameModeManager.IsRD || condition == Condition.N || cards.Count == 0)
        {
            rdMaxIntegratedReset();
            return;
        }
        bool on = rdMaxIntegratedOn();
        rdMaxIntegratedSide(0, on);
        rdMaxIntegratedSide(1, on);
        if (QuickTestTrace.Enabled)
        {
            logRdMaxZoomProbe(0);
            logRdMaxZoomProbe(1);
        }
    }

    /// <summary>清理两侧状态（对局收尾 / 切模式 / <see cref="hide"/>）。被我们挪动过的三张各回位一次。</summary>
    public void rdMaxIntegratedReset()
    {
        for (int side = 0; side < 2; side++)
        {
            if (rdMaxZoomArr(side)[0] != null)
            {
                rdMaxZoomRelease(side, rdMaxZoomOwn(side));
            }
            rdMaxZoomSetK(side, 0f);
            rdMaxZoomSetOwn(side, false);
            if (side == 0) rdMaxZoomMMe = 1f; else rdMaxZoomMOp = 1f;
        }
        rdMaxZoomProbeLast0 = "";
        rdMaxZoomProbeLast1 = "";
    }

    /// <summary>
    /// 把这一侧**上一帧记的三张**收干净并清空名单。两件事各自独立：
    ///   · <paramref name="releasePositions"/> = 把三张放回各自的原位（<c>TweenTo(accurate_position)</c>）。
    ///     ⚠ 只有「确实挪动过位置」（k&gt;0）才需要 —— k==0 时位置本来就是准的，无脑 TweenTo
    ///     反而会在 realize 的补间（飞入 / 撤回 / 重排）上插一脚。
    ///   · 按钮托管则**总是**要解除（否则旧本体永远不肯 hide，按钮会一直挂在那儿）。
    /// </summary>
    private void rdMaxZoomRelease(int side, bool releasePositions)
    {
        gameCard[] a = rdMaxZoomArr(side);
        gameCard body = a[0];
        for (int i = 0; i < 3; i++)
        {
            gameCard c = a[i];
            // 只回收「还在场上的」：已经离场的卡由 realize / 归位兜底管，这里别去 tween 它们。
            if (releasePositions && c != null && c.gameObject != null
                && c.gameObject.activeInHierarchy)
            {
                c.ES_safe_card_move_to_original_place();
            }
            a[i] = null;
        }
        rdMaxZoomReleaseButtons(body);
    }

    /// <summary>解除某个本体上的「按钮托管」：夺回权 + （它自己没在 excited 时）把按钮收掉。
    /// 它自己还在 excited 的话，按钮归它自己的 ES 管，别去抢。</summary>
    private void rdMaxZoomReleaseButtons(gameCard body)
    {
        if (body != null && body.rdMaxIntegratedButtonOwned)
        {
            body.rdMaxIntegratedButtonOwned = false;
            if (!body.ES_isExcited)
            {
                body.ES_hideButtonsForRdMaxIntegrated();
            }
        }
    }

    /// <summary>
    /// 一侧的一帧：解析三件 → 判「有没有被指着」→ 收敛 k → 按屏幕空间等比缩放写三张的位置
    /// （+ 托管本体按钮）。**只有 k&gt;0 时才写 position** —— k 归 0 后一次也不写，
    /// 否则会和 realize 的 `TweenTo`（飞入 / 撤回 / 重排）抢位置。
    /// </summary>
    private void rdMaxIntegratedSide(int side, bool on)
    {
        // ⚠ 必须先给 out 形参一个初值：`on` 为 false 时 `&&` 短路、rdMaxResolveTrio 根本不会被调用，
        //   而 C# 要求「可能未赋值」的变量不能读 ⇒ 显式初始化（值仍是「这一侧没有三件」的 null）。
        gameCard body = null, pl = null, pr = null;
        bool valid = on && rdMaxResolveTrio(side, out body, out pl, out pr);
        gameCard[] a = rdMaxZoomArr(side);

        bool sameGroup = valid && a[0] == body && a[1] == pl && a[2] == pr;
        if (!sameGroup)
        {
            // 换组（含「上一组散伙 / 本体被换掉」）：把上一组收干净，再换上新组（或清空）。
            // ⚠ 位置只在「确实挪动过」（k&gt;0）时才回收（理由见 rdMaxZoomRelease）；
            //   按钮托管则**无条件**解除 —— 少了这一句，「上一次 hover 让 owned=true、
            //   但 k 还没起来组就散了」这种情形下，旧本体会永远不肯 hide（按钮残留）。
            if (a[0] != null)
            {
                rdMaxZoomRelease(side, rdMaxZoomOwn(side));
            }
            rdMaxZoomSetK(side, 0f);
            rdMaxZoomSetOwn(side, false);
        }
        if (!valid)
        {
            return;
        }
        a[0] = body; a[1] = pl; a[2] = pr;

        bool hovered = body.ES_hoveredByPointer()
            || pl.ES_hoveredByPointer() || pr.ES_hoveredByPointer();

        float k = rdMaxZoomK(side);
        k += ((hovered ? 1f : 0f) - k) * Math.Min(1f, Program.deltaTime * RdMaxIntegratedGain);
        if (k < 0.002f)
        {
            k = 0f;                       // 收尾阈值：避免永远差一点点
        }
        rdMaxZoomSetK(side, k);

        bool own = k > 0f;
        if (own && Program.camera_game_main != null)
        {
            Vector3 baseBody = body.UA_get_accurate_position();
            Vector3 spBody = Program.camera_game_main.WorldToScreenPoint(baseBody);
            // 共同 m：从**本体**算（本体在中列 ⇒ 它的屏点就是整组的视觉中心 C）。
            // z 太近（贴脸）时退化成 1，免得 m 爆掉。
            float m = spBody.z > RdMaxIntegratedPull + 0.5f
                ? spBody.z / (spBody.z - RdMaxIntegratedPull)
                : 1f;
            if (side == 0) rdMaxZoomMMe = m; else rdMaxZoomMOp = m;
            for (int i = 0; i < 3; i++)
            {
                gameCard c = a[i];
                if (c == null || c.gameObject == null)
                {
                    continue;
                }
                // 底座永远取 accurate_position（realize 摆到哪儿）：缩放期间它不变，
                // 于是「谁在什么时候动过」都不会与本 tick 打架。
                Vector3 b = c.UA_get_accurate_position();
                Vector3 sp = Program.camera_game_main.WorldToScreenPoint(b);
                Vector2 tgt = new Vector2(spBody.x + (sp.x - spBody.x) * m,
                                          spBody.y + (sp.y - spBody.y) * m);
                float z = sp.z / m;       // 深度收到 1/m ⇒ 屏幕尺寸 ×m
                Vector3 want = Program.camera_game_main.ScreenToWorldPoint(
                    new Vector3(tgt.x, tgt.y, z));
                // ⛔ 不 lerp「当前 → want」（阻尼目标会漂）；一律 Lerp(底座, 目标, k)。
                c.gameObject.transform.position = Vector3.Lerp(b, want, k);
            }
        }
        else if (rdMaxZoomOwn(side))
        {
            // 刚从「占用中」退出：把三张精确写回底座一次，之后一帧都不再写。
            for (int i = 0; i < 3; i++)
            {
                gameCard c = a[i];
                if (c != null && c.gameObject != null)
                {
                    c.gameObject.transform.position = c.UA_get_accurate_position();
                }
            }
        }
        rdMaxZoomSetOwn(side, own);

        // ── D 按钮托管：三件里任一张被指着 ⇒ 把**本体**的选项按钮浮出来 ──
        //    tick 早于 gameCard.Update ⇒ 本帧先置 owned 再 show；夺回权时**先**置 false 再 hide，
        //    本体同帧看到的已是最新值 ⇒ 不会出现「hide 完下一帧再 show」的闪烁。
        //    关掉选项 / 三件散伙时，owned 由上面的 release 分支解除。
        if (hovered)
        {
            body.rdMaxIntegratedButtonOwned = true;
            body.ES_showButtonsForRdMaxIntegrated();
        }
        else if (body.rdMaxIntegratedButtonOwned)
        {
            body.rdMaxIntegratedButtonOwned = false;
            if (!body.ES_isExcited)
            {
                body.ES_hideButtonsForRdMaxIntegrated();
            }
        }
    }

    /// <summary>
    /// **无副作用**地扫出某一侧的三件（本体 + 两张带 Overlay 的部件）。三个都齐才算成立
    /// ⇒「单独在场的极大怪兽」（单体召唤的本体 / 还没收成素材的部件）一律不受影响。
    ///
    /// 判据：
    ///   · 同侧（`p.controller == side`）+ 极大卡 + `activeInHierarchy`；
    ///   · 排除 `rdMaxHeld`（三件同帧入场的暂存，缩到 0.001）与 `isRdMaximumPiecePending`
    ///     （极大召唤中间态：已经落到场上、还没被 `Duel.Overlay` 收成素材的那一瞬）；
    ///   · **本体** = 带怪兽区位且**不带 Overlay**（严格口径）；
    ///   · **部件** = 带 Overlay，且「成为素材前的格位」落在中区左右两列（1 / 3）
    ///     —— 效果直接贴上来的素材没有这段旧格位，不算。
    /// ⛔ 不调 `rdMaxRefreshTrioState`（有副作用）、不看 `rdMaxSettled*`。
    /// </summary>
    private bool rdMaxResolveTrio(int side, out gameCard body, out gameCard l, out gameCard r)
    {
        body = null; l = null; r = null;
        for (int i = 0; i < cards.Count; i++)
        {
            gameCard c = cards[i];
            if (c == null || !c.gameObject.activeInHierarchy)
            {
                continue;
            }
            if (!isMaximumCard(c) || (int)c.p.controller != side)
            {
                continue;
            }
            if (c.rdMaxHeld || isRdMaximumPiecePending(c))
            {
                continue;
            }
            if ((c.p.location & (UInt32)CardLocation.Overlay) != 0)
            {
                // ⛔⛔ 「带 Overlay 位」**不等于**「还是三件里的那块部件」：归位后的部件
                //   是 `0x90 = 墓地|Overlay`，Overlay 位照样在（见 rdMaxPieceOnField）。
                //   少了这一闸，本体一走、大框与一体化都因为「部件还在」而不收，
                //   屏幕上的表现正是用户报的「部件从墓地回场上 + 墓地字样」。
                if (!rdMaxPieceOnField(c))
                {
                    continue;      // 已经不在场上（墓地/卡组/除外/额外）：不是这一组三件了
                }
                GPS before = c.p_beforeOverLayed;
                if ((before.location & (UInt32)CardLocation.MonsterZone) == 0)
                {
                    continue;      // 效果直接贴上去的素材：没有「旧格位」⇒ 不算极大召唤的部件
                }
                if (before.sequence == 1) l = c;
                else if (before.sequence == 3) r = c;
                continue;
            }
            if ((c.p.location & (UInt32)CardLocation.MonsterZone) == 0)
            {
                continue;          // 手牌 / 墓地 / 卡组 / 额外里躺着的极大卡
            }
            body = c;
        }
        return body != null && l != null && r != null;
    }

    /// <summary>
    /// 这张卡的位置此刻是不是正被「极大怪兽一体化」占用（本帧三件之一且 k&gt;0）。
    /// gameCard 的 close-up 早退与 exit 跳过回位都问它。
    /// </summary>
    public bool RdMaxIntegratedOwnsPosition(gameCard c)
    {
        if (c == null || !GameModeManager.IsRD)
        {
            return false;
        }
        int s = (int)c.p.controller;
        if (s != 0 && s != 1)
        {
            return false;
        }
        if (!rdMaxZoomOwn(s))
        {
            return false;
        }
        gameCard[] a = rdMaxZoomArr(s);
        return a[0] == c || a[1] == c || a[2] == c;
    }

    /// <summary>
    /// 点 L/R 部件 / 召唤中间态的部件时，**该被当成哪张卡来点**（入口在 gameCard.RefreshFunction_ES）。
    ///   · 非部件（含普通卡、本体自己）⇒ 原样返回（**旧行为逐字节不变**）；
    ///   · 部件 / 召唤中间态 ⇒ 返回它的**本体**（严格版口径，见 rdMaximumBody）；
    ///     本体找不到（孤儿部件）⇒ 返回 null ⇒ 调用方**不发包**；
    ///   · 选项关掉 ⇒ 部件一律返回 null（退回今天「点部件没反应」）。
    /// 发动 / 攻击仍由**本体**发出 ⇒ 服务器天然保证不能反复发动 / 反复攻击。
    /// </summary>
    public gameCard RdMaximumClickTarget(gameCard c)
    {
        if (c == null)
        {
            return null;
        }
        if (!(c.isRdMaximumPiece() || isRdMaximumPiecePending(c)))
        {
            return c;
        }
        bool on = rdMaxIntegratedOn();
        gameCard target = on ? rdMaximumBody(c) : null;
        if (QuickTestTrace.Enabled)
        {
            // `[maxclick]` 探针：转发**只在点部件时**才落行（普通卡点击不落，免得刷屏）。
            // 验收咬 `from != target`（点了 L/R，真正被点的是本体）与 `target` 不带 Overlay。
            QuickTestTrace.Log("maxclick",
                "from=" + (c.isRdMaximumPiece() ? "piece" : "pending")
                + " code=" + c.get_data().Id
                + " on=" + (on ? 1 : 0)
                + " target=" + (target == null ? "none" : ("body code=" + target.get_data().Id)));
        }
        return target;
    }

    /// <summary>
    /// `[maxzoom]` 探针（每侧一行，内容变了才落）：一体放大的 k / m / 是否占用位置，以及
    /// 三张卡各自的「逻辑矩形 base（按 accurate_position 算）vs 实际矩形 cur」与「谁被指着 hit」，
    /// 外加本体的立绘矩形（cur / base）。
    ///
    /// 为什么同时给 base 与 cur：验收要量的是**比例**（三张 cur 宽 / base 宽 ≈ m、立绘同理），
    /// base 用 `probe_face_rect_at(accurate_position)` 算 —— 与本 tick 写的目标同源，
    /// 所以「放大倍率对不对」是自洽地量出来的，不靠猜。只在 log/qt_debug.on 下有开销。
    /// </summary>
    private void logRdMaxZoomProbe(int side)
    {
        gameCard[] a = rdMaxZoomArr(side);
        if (a[0] == null)
        {
            return;
        }
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        sb.Append("side=").Append(side)
          .Append(" k=").Append(rdMaxZoomK(side).ToString("F3"))
          .Append(" m=").Append((side == 0 ? rdMaxZoomMMe : rdMaxZoomMOp).ToString("F3"))
          .Append(" own=").Append(rdMaxZoomOwn(side) ? 1 : 0);
        // cx/cy = 这一帧用的**轴心 C**（本体的 base 屏点，客户区左上原点）。验收要按
        // 「三张的屏幕中心以 C 为轴、按倍率 f=1+k(m-1) 等比移动」量刚体性 —— 没有 C 就只能
        // 拿「本体 base 矩形中心」近似，会有 1~2px 的系统偏差。这里直接把它报出来。
        if (Program.camera_game_main != null)
        {
            Vector3 spBody = Program.camera_game_main.WorldToScreenPoint(
                a[0].UA_get_accurate_position());
            sb.Append(" cx=").Append((int)Math.Round(spBody.x))
              .Append(" cy=").Append(Screen.height - (int)Math.Round(spBody.y));
        }
        for (int i = 0; i < 3; i++)
        {
            gameCard c = a[i];
            if (c == null || c.gameObject == null)
            {
                continue;
            }
            sb.Append(" role=").Append(i == 0 ? "body" : (i == 1 ? "L" : "R"))
              .Append(" code=").Append(c.get_data().Id)
              .Append(" base=").Append(c.probe_face_rect_at(c.UA_get_accurate_position()))
              .Append(" cur=").Append(c.probe_face_rect())
              .Append(" hit=").Append(c.ES_hoveredByPointer() ? 1 : 0);
        }
        // 立绘只看本体：它挂在**本体的卡面**上（部件没有立绘，见 UA_give_condition）。
        sb.Append(" vert=").Append(a[0].probe_verticle_rect())
          .Append(" vertbase=").Append(a[0].probe_verticle_rect_base());
        string s = sb.ToString();
        if (side == 0)
        {
            if (s == rdMaxZoomProbeLast0) return;
            rdMaxZoomProbeLast0 = s;
        }
        else
        {
            if (s == rdMaxZoomProbeLast1) return;
            rdMaxZoomProbeLast1 = s;
        }
        QuickTestTrace.Log("maxzoom", s);
    }

    /// <summary>
    /// 极大怪兽的显示探针：把场上每张「极大怪兽相关卡」（本体 + L/R 部件）的客户端 GPS、
    /// 「成为素材前」的格位、实到世界坐标与缩放拼成一行 `[max] …`，**内容变了才写**。
    ///
    /// 为什么非要让游戏自己报：部件的最终 `p` 是「父卡位置 | Overlay + position 1000」，
    /// 「它到底被摆到哪儿」从 core 的数据推不出来（见上面 GCS_cardMove 那条）。
    /// 验收 `_verify_rdai_game.py` 咬的就是这行；只在 log/qt_debug.on 存在时才有开销。
    /// </summary>
    private void logMaximumProbe()
    {
        if (!QuickTestTrace.Enabled || !GameModeManager.IsRD)
        {
            return;
        }
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        int n = 0;
        // 大框口径的目标位置：报的必须是**真的给出去的那个目标**（贴紧之后），
        // 否则判据会变成「got 与 want 不符」这种假报警。
        bool trioMe = rdMaximumTrioComplete(0);
        bool trioOp = rdMaximumTrioComplete(1);
        for (int i = 0; i < cards.Count; i++)
        {
            gameCard c = cards[i];
            if (c == null || !c.gameObject.activeInHierarchy)
            {
                continue;
            }
            if (!isMaximumCard(c))
            {
                continue;
            }
            // 🔑 只要带 Overlay 位就先报一行**全状态**（用户 2026-09-24 第二次报告后加）：
            //   `count=` / `offpiece` 各自只覆盖一种形态，而这次用户报的形态在两条复现路径里
            //   都没出现 ⇒ 需要一行能覆盖任意形态的物证。见 logRdMaximumPieceState。
            logRdMaximumPieceState(c);
            // ⛔ 已经**不在场上**（`p.location` 没有怪兽区位）的极大卡不并进 `count=`，但必须留一条
            //   专门的痕 —— 它是用户报的「离场后场上多出两张素材状态的部件」的唯一物证。
            //   ⚠ 这一支**必须排在下面的 `count` 分支之前**：判据要的是「它此刻被指向哪儿」，
            //     而它恰恰是因为不在场上才出问题。见 logRdMaximumOffField 的说明。
            if ((c.p.location & (UInt32)CardLocation.MonsterZone) == 0)
            {
                logRdMaximumOffField(c);
                continue;
            }
            n++;
            GPS b = c.p_beforeOverLayed;
            // 探针只给**部件**报 want（本体报 `none`）：验收 6q1/6n 咬的是「两张部件真的到了
            // 贴紧位置」，本体不参与；把本体也算进来，飞行途中的行会被判红。
            // 值本身与运行时同一个入口（rdMaximumWantPosition），不另写算式。
            Vector3? want = maximumPieceWorldPosition(c) == null
                ? (Vector3?)null
                : rdMaximumWantPosition(c, trioMe, trioOp);
            // 🔑 「极大怪兽一体化」放大期间：本 tick 会把这三张卡**视觉上**朝镜头拉近，
            //   但那是表现层位移、不是它的逻辑位置 ⇒ 探针必须报**逻辑位置**
            //   （accurate_position = realize 摆到哪儿 / want），否则
            //   `_verify_rdai_game.py` 的 6j9/6q/6q1（got==want）会红；
            //   frect 同理按逻辑位置算（base 口径）—— 6t 的 texel↔屏幕交叉验算
            //   量与「框贴住卡」的换算基准，拿放大后的矩形去算会把余量放大 m 倍。
            //   ⚠ 未托管时这两处与原实现逐字节相同（OCG / 非放大期零影响）。
            bool zoomOwned = RdMaxIntegratedOwnsPosition(c);
            Vector3 pos = zoomOwned ? c.UA_get_accurate_position()
                                    : c.gameObject.transform.position;
            Vector3 sp = Program.camera_game_main.WorldToScreenPoint(pos);
            YGOSharp.Card raw = rawCardOf(c);
            sb.Append(" | code=").Append(c.get_data().Id)
              .Append(" p=").Append(c.p.controller).Append("/").Append(c.p.location)
              .Append("/").Append(c.p.sequence).Append("/").Append(c.p.position)
              .Append(" before=").Append(b.controller).Append("/").Append(b.location)
              .Append("/").Append(b.sequence).Append("/").Append(b.position)
              .Append(" over=").Append(c.overFatherCount)
              .Append(" got=").Append(probe3(pos))
              .Append(" want=").Append(want.HasValue ? probe3(want.Value) : "none")
              // scr = 屏幕坐标（Unity 口径，原点在**左下角**；对截图时 y 要翻过来）。
              // win = 同一位置换算成**客户区坐标（左上原点）** —— 与 Program.cs 的
              //   `[mouse] winPos=` 同一口径，验收脚本可以直接喂给 SetCursorPos，不用自己猜
              //   Screen.height（窗口模式客户区 ≠ 窗口高，猜错就点空）。
              .Append(" scr=").Append((int)sp.x).Append(",").Append((int)sp.y)
              .Append(" win=").Append((int)sp.x).Append(",").Append(Screen.height - (int)sp.y)
              .Append(" scale=").Append(probe3(c.gameObject.transform.localScale))
              // 卡面数字到底显示了什么：`atk` 是客户端手里的 data.Attack（core 推过来的实时值），
              // `raw` 是卡表原始 ATK，`txt` 是真正画在卡上的那串文本。三者一比就知道
              // 「显示不对」是 core 没把新值推过来、还是推过来了但没画上去。
              .Append(" atk=").Append(c.get_data().Attack).Append("/").Append(c.get_data().Defense)
              .Append(" raw=").Append(raw == null ? -999 : raw.Attack).Append("/")
              .Append(raw == null ? -999 : raw.Defense)
              // vdeco = 这张卡身上「场上表侧怪兽」专属装饰的件数（竖立绘/怪兽云/等级数字/
              //   星级图标，见 gameCard.probe_verticle_deco）。用户 2026-09-22 要求 RD 极大
              //   怪兽的 L/R 部件**不要**这些（原话：「没有怪兽立绘、等级、攻击力防御力之类
              //   现在多出来的」）⇒ 部件这一项必须是 0，本体照旧可以有。
              // ⚠ 别拿 `txt` 代替它：那是另一个对象（卡面文字），部件上恒为空，看不出装饰还在。
              //   放在 txt 之前是因为 txt 用 `[^|]*` 吃到记录尾，塞它后面会被一起吃掉。
              .Append(" vdeco=").Append(c.probe_verticle_deco())
              // frect = 这张卡**画在屏幕上的矩形**（客户区、左上原点，同 `win=` 口径），
              //   fsize = 卡面的世界尺寸 ×100。验收脚本拿 frect 当锚，在截图上量
              //   「RD 大框的描边离卡的外沿几个像素」——「三张无缝拼接 + 一个大框框住」
              //   才有一个不靠猜的判据（见 gameCard.probe_face_rect）。
              // ⚠ 放在 txt 之前：txt 的 `[^|]*` 会吃到记录尾。
              .Append(" frect=").Append(zoomOwned
                  ? c.probe_face_rect_at(c.UA_get_accurate_position())
                  : c.probe_face_rect())
              .Append(" fsize=").Append(c.probe_face_size())
              .Append(" txt=").Append(c.probe_hint_text.Replace("|", "/"));
        }
        if (n == 0)
        {
            // 「场上有极大卡」→「一张都没有」这一步也要留一条：探针只在内容变化时写行，
            // 而 count=0 过去是直接 return ⇒ 「部件终于离场了」在日志里**完全静默**，
            // 于是「一直卡在场上」和「正常离场」看起来一模一样（都是没有新行）。
            // 验收 `_verify_rdai_game.py` 的 7x 咬这一行：本体离场后必须等到 count=0。
            if (maxProbeLast != null && maxProbeLast != "count=0")
            {
                maxProbeLast = "count=0";
                QuickTestTrace.Log("max", "count=0");
            }
            return;
        }
        sb.Insert(0, "count=" + n);
        string line = sb.ToString();
        if (line == maxProbeLast)
        {
            return;
        }
        maxProbeLast = line;
        QuickTestTrace.Log("max", line);
    }

    /// <summary>
    /// 诊断（只在 `log/qt_debug.on` 下落行，内容不变时只写一次）：**已经不在场上**
    /// （`p.location` 没有怪兽区位、却仍带 `Overlay` 位）的极大卡，此刻被
    /// <see cref="maximumPieceWorldPosition"/> 指向了哪儿。
    ///
    /// 为什么需要它（用户 2026-09-24 报的 bug，物证 `log/qt_3480.log` 08:45:23.332）：
    ///   第二次极限召唤第一步 `Duel.SendtoGrave(fg)` 把本体送走后，两张部件在客户端被
    ///   （`GCS_cardMove` 的素材重规整循环）改写成 `0x90 = 墓地|Overlay`，可旧口径的
    ///   `maximumPieceWorldPosition` 只看「有没有 Overlay 位」⇒ 返回**它原来那一格**
    ///   ⇒ 那一帧探针实测 `code=120150001 p=0/144/0/0 … got=(-467,0,-675)`
    ///   —— 画在场上原列上、还是一副素材相 ⇒ 用户看到「场上多出两个被叠放的素材状态的
    ///   L/R 部件」。那一瞬只持续 130~270ms（130ms 后 core 才把部件摘到墓地），
    ///   **靠帧率去撞是撞不稳的**，所以这里判的是「算出来的目标点」而不是动画途中的位置：
    ///     · 指向**它原来那一格** ⇒ `stuck=1`（= bug 在演）；
    ///     · 指向它**真正去的那个区**（墓地/除外/卡组/额外）⇒ `stuck=0`。
    ///   与帧率、动画、掉帧全无关 ⇒ 验收可以稳定地咬。
    /// </summary>
    private static string maxOffLast = null;

    private void logRdMaximumOffField(gameCard c)
    {
        if (!QuickTestTrace.Enabled)
        {
            return;
        }
        // 只报**「曾作为素材」那种离场**（带 Overlay 位，实测 `0x90 = 墓地|Overlay`）——
        // 手牌/卡组里的极大卡也「不在场上」，但那不是这条判据要盯的形态，
        // 全报会把真正的物证淹掉（首版一次跑刷了 38 条 `loc=0x2` 的手牌噪声）。
        if ((c.p.location & (UInt32)CardLocation.Overlay) == 0)
        {
            return;
        }
        GPS before = c.p_beforeOverLayed;
        Vector3? want = maximumPieceWorldPosition(c);
        bool stuck = false;
        if (want.HasValue && (before.location & (UInt32)CardLocation.MonsterZone) != 0)
        {
            // 「原来那一格」= 部件成为素材之前站的那一列（`p_beforeOverLayed.sequence`），
            // 但**其余字段必须拿本卡自己的 `c.p`**（尤其是 controller）：
            // ⛔ 这里踩过一次 —— 直接用 `before` 当 GPS 会带上它那份**未映射**的 controller
            //   （实测同一张卡 `p.controller=1` 而 `before.controller=0`，见
            //   `maximumPieceWorldPosition` 里那条 ⚠⚠），算出来是**对面半场那一列**，
            //   与函数真正返回的列差着半个场地 ⇒ `stuck` 恒 0，阴性对照假绿。
            //   算式与 `maximumPieceWorldPosition` 里那一支逐字段对齐（同一入口，不另写）。
            GPS bp = c.p;
            bp.location = (UInt32)CardLocation.MonsterZone;
            bp.sequence = before.sequence;
            bp.position = (int)CardPosition.FaceUpAttack;
            stuck = Vector3.Distance(want.Value, get_point_worldposition_rd(bp, null)) < 1.0f;
        }
        string line = "offpiece id=" + c.get_data().Id
            + " loc=0x" + c.p.location.ToString("X")
            + " over=" + c.overFatherCount
            + " stuck=" + (stuck ? 1 : 0)
            + " want=" + (want.HasValue ? probe3(want.Value) : "none");
        if (line == maxOffLast)
        {
            return;
        }
        maxOffLast = line;
        QuickTestTrace.Log("max", line);
    }

    /// <summary>
    /// RD 中区三列的世界 x（raw，未乘 real）。对手左右对调 —— 口径与原 OCG 分支的
    /// `realIndex = 4 - sequence` 一致（取的是同一批列值，两边都落在贴图的中区格子上）。
    /// </summary>
    private static float rdColumnX(uint sequence, bool op)
    {
        uint i = op ? (4 - sequence) : sequence;
        switch (i)
        {
            case 1: return -5.17f;
            case 2: return -0.27f;
            case 3: return 4.72f;
        }
        return -0.27f;
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

    /// <summary>
    /// 天梯「回环」：匹配态下把返回目标钉在竞技场界面（对齐 hex 版 ygopro2 的同名方法）。
    ///
    /// 为什么需要：本客户端原来只有「进入某界面时记下 returnServant」这一半，
    /// 打完一局天梯 / 匹配中掉线时没有任何地方把它指回 mycard ⇒ 玩家被丢回服务器列表，
    /// 而竞技场那边还停在「匹配中」。补上这一半就闭合了。
    ///
    /// ⛔ 只在 isMatching 为真时改 returnServant，非匹配态一律不动：
    ///    ours 的 returnServant 另有多处语义 —— Room.StocMessage_ChangeSide 指向 deckManager
    ///    （换副卡组后回卡组界面）、AIRoom.launch 指向 aiRoom（人机局）、precy 指向 puzzleMode、
    ///    SelectServer.show 指向 selectServer。无条件覆盖会把它们全部带偏，
    ///    尤其人机局（AIRoom）也复用 TcpHelper 的断线路径，一旦被改就回不到人机界面。
    /// </summary>
    public void setDefaultReturnServant()
    {
        if (Program.I().mycard != null && Program.I().mycard.isMatching)
        {
            returnServant = Program.I().mycard;
        }
    }

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
        // 天梯回环（打完一局离开决斗的那条路）：先把返回目标钉住再关连接。
        // ⛔ 必须排在关 tcpClient 之前 —— 关掉之后 TcpHelper 的断线分支会再走一遍界面切换。
        setDefaultReturnServant();
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


    // ── 俯视角「拉镜头」的每帧跟随状态（第 7 轮）────────────────────────────
    // 上一拍的摊开态：摊开↔收摊切换时把 `Program.topDownPanUserTook` 复位，
    // 让自动值重新接管（旧写法在闲态分支里每帧复位，第 7 轮起闲态允许用户上滑后停留，
    // 复位时机收窄到「状态切换」这一拍）。
    bool topDownPanSpreadLast = false;

    // 上一拍「查看对方手牌」态（第 8 轮起 = **悬停锁存**）。翻转时复位 UserTook
    // 并落一条 `[ophand]` 探针日志（验收/排查用，变了才写）。
    bool topDownOpHandShownLast = false;

    // 「查看对方手牌」的**锁存**态：鼠标悬停对方手卡 ⇒ 置位；光标退到对方手牌行下方
    // （释放线，见 `topDownOpHandReleaseLine`）才清零。悬停丢失本身**不**清零 —— 见方法注释。
    bool topDownOpHandHoverLatch = false;

    /// <summary>
    /// 锁存释放线的 |z|：对方手牌行 0 的行心再往**牌桌内侧**退一个卡长。
    /// 行心 = `(cover − 0.5) − 2s`（与 <see cref="Program.topDownPanMaxZ"/> 消费的同一行带模型），
    /// 半长 = 2s；s = 1.5 时行心 20.8、半长 3 ⇒ 释放线 = 20.8 − 6 = **14.8**。
    /// 光标的世界 z 低于它 = 用户明确把鼠标从对方手牌那侧收回来了 —— 这是锁存唯一的 OFF 条件。
    /// </summary>
    float topDownOpHandReleaseLine()
    {
        float s = Program.topDownHandMaxScale;
        float row0 = (Program.topDownCoverZ - 0.5f) - 2f * s;
        return row0 - 4f * s;
    }

    /// <summary>
    /// 「查看对方手牌」态检测（第 8 轮口径：**鼠标悬停对方手卡**，整体替换第 7 轮的
    /// 「对方手牌行有公开牌面（Id&gt;0）」—— 用户澄清「查看」指的是把鼠标滑上去，不是效果公开）。
    ///
    /// ⛔⛔ **必须锁存**，否则永动震荡：悬停 ⇒ 拉镜头到 maxZ ⇒ 对方手牌行在屏上滑走
    ///    ⇒ 光标不再压着那张卡 ⇒ 悬停丢失 ⇒ 回 rest ⇒ 行又滑回光标底下 ⇒ 再悬停……
    ///    所以 ON 之后只认「光标退到释放线以下」这一种 OFF；悬停丢失不清锁存。
    /// 触发用 <see cref="gameCard.ES_pointed_raw"/>（纯射线几何）：对方手牌平时
    /// still_unclickable，走 ES_mouse_check 会被一票否掉。
    /// 光标的世界 z：正俯视相机沿 −Y 看 ⇒ ScreenToWorldPoint 给相机高度即得 y=0 桌面落点。
    /// </summary>
    bool topDownOpHandHover()
    {
        bool hover = false;
        for (int i = 0; i < cards.Count; i++)
        {
            if (cards[i].gameObject.activeInHierarchy
                && cards[i].p.controller == 1
                && (cards[i].p.location & (UInt32)CardLocation.Hand) > 0
                && cards[i].ES_pointed_raw())
            {
                hover = true;
                break;
            }
        }
        if (hover)
        {
            topDownOpHandHoverLatch = true;
        }
        else if (topDownOpHandHoverLatch)
        {
            float camH = Program.camera_game_main.transform.position.y;
            Vector3 wp = Program.camera_game_main.ScreenToWorldPoint(new Vector3(
                Input.mousePosition.x, Input.mousePosition.y, camH));
            if (wp.z < topDownOpHandReleaseLine())
            {
                topDownOpHandHoverLatch = false;
            }
        }
        return topDownOpHandHoverLatch;
    }

    #region 排查用：对局途中切视角（qt_topdownmid.on）
    // 用户 2026-09-23 第 1 条：「对局途中切视角时，手牌要**立刻**重摆，
    // 别等到下一次抽牌/用牌」—— 切视角本身要强制重摆。
    //
    // 这段替探针走一遍设置窗口那条路的**实质**（`Program.topDown = v` + `realize(true)`
    // 就是 `Setting.onChangeTopDown` 去掉「存盘」与「从 UI 取开关值」两步），
    // 并在切换**前 / 后同帧 / 1.5 秒后**各打一行「相机 + 手牌排每张卡的落点·目标·缩放」。
    // 判据（见 `_probe_topdown.py --mid`）咬的是：切换后同帧手牌就已经到新落点
    // （`t≈acc≈` 新行、`sc≈` 新倍数），1.5 秒后没有被谁弹回去。
    //
    // ⛔ 只在 log/qt_debug.on 存在时生效（`QuickTestTrace.Enabled`），正式包零影响。
    bool tdMidFlipDone = false;
    int tdMidArmMs = -1;
    int tdMidLateAt = -1;

    /// <summary>排查用：一行装下「相机位置 + 手牌排每张卡的 transform / 已落定目标 / 缩放」。</summary>
    string tdMidDump()
    {
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        if (Program.camera_game_main != null)
        {
            Vector3 c = Program.camera_game_main.transform.position;
            sb.Append(" cam=(").Append(c.x.ToString("F1")).Append(",")
              .Append(c.y.ToString("F1")).Append(",").Append(c.z.ToString("F1")).Append(")");
        }
        for (int i = 0; i < cards.Count; i++)
        {
            if ((cards[i].p.location & (UInt32)CardLocation.Hand) == 0)
            {
                continue;
            }
            if (cards[i].gameObject == null || cards[i].gameObject.activeInHierarchy == false)
            {
                continue;
            }
            Vector3 t = cards[i].gameObject.transform.position;
            Vector3 a = cards[i].UA_get_accurate_position();
            sb.Append(" [c").Append(cards[i].p.controller)
              .Append("s").Append(cards[i].p.sequence)
              .Append(" t=(").Append(t.x.ToString("F1")).Append(",").Append(t.y.ToString("F1"))
              .Append(",").Append(t.z.ToString("F2")).Append(")")
              .Append(" acc=").Append(a.z.ToString("F2"))
              .Append(" sc=").Append(cards[i].gameObject.transform.localScale.x.ToString("F3"))
              .Append("]");
        }
        return sb.ToString();
    }

    /// <summary>排查用：qt_topdownmid.on 的每帧处理（见上面这段的说明）。</summary>
    void topDownMidProbeTick()
    {
        if (!QuickTestTrace.SwitchOn("qt_topdownmid.on"))
        {
            tdMidFlipDone = false;
            tdMidArmMs = -1;
            tdMidLateAt = -1;
            return;
        }
        if (!tdMidFlipDone)
        {
            if (!isShowed || condition != Condition.duel)
            {
                return;
            }
            if (tdMidArmMs < 0)
            {
                tdMidArmMs = Program.TimePassed();
                return;
            }
            // 让对局先跑够时间：开局那几拍 realize 还会自己把牌挪来挪去，太早切看不准。
            if (Program.TimePassed() - tdMidArmMs < 4000)
            {
                return;
            }
            tdMidFlipDone = true;
            QuickTestTrace.Log("tdmid", "pre topDown=" + (Program.topDown ? 1 : 0) + tdMidDump());
            Program.topDown = !Program.topDown;
            realize(true);                 // = Setting.onChangeTopDown 里的 onCP()
            QuickTestTrace.Log("tdmid", "post topDown=" + (Program.topDown ? 1 : 0) + tdMidDump());
            tdMidLateAt = Program.TimePassed();
            return;
        }
        if (tdMidLateAt > 0 && Program.TimePassed() - tdMidLateAt >= 1500)
        {
            tdMidLateAt = -1;
            QuickTestTrace.Log("tdmid", "late topDown=" + (Program.topDown ? 1 : 0) + tdMidDump());
        }
    }
    #endregion

    private bool rdMaxGhostDone;

    /// <summary>
    /// 排查用（开关 `log/qt_maxghost.on`）：**人为造出幽灵部件**，用来正面验证兜底真的会动手。
    ///
    /// 为什么需要它（2026-09-24）：三条离场复现（`RDMAX_LEAVE=1/2/3`）**验不到兜底** ——
    /// 那三条路上 core 都老老实实发了「素材跟着走」的 MOVE，靠 `maximumPieceWorldPosition`
    /// 的「不在场上 ⇒ 跟着本体走」就收干净了，`orphan-park` 永远 0 条。缺的那一半正是用户
    /// 的现场：**那条 MOVE 没到客户端**（人类玩家局里 `sibyl` 会清掉未消费的包）。
    ///   做法：把本侧极大**本体**在客户端标记里改成「已经不在场上」（`墓地|Overlay`、`seq=0`）
    ///   —— **只动客户端字段**，core 那边一无所知 ⇒ 那条 MOVE 根本不存在，两张部件就停在
    ///   `0x84`、`father=0`，与用户现场同构。此后每帧的兜底应在 0.5s 内把它们归位
    ///   ⇒ 日志里必须出现 `orphan-park`。
    /// ⛔ 关得很死：RD + 开关在 + 只做一次 + 只从「本侧已经有本体（= 三件齐全）」的场面里挑。
    ///   正式包（没有 `qt_maxghost.on`）零影响。
    /// </summary>
    private void rdMaxGhostInject()
    {
        if (rdMaxGhostDone || !GameModeManager.IsRD
            || !QuickTestTrace.SwitchOn("qt_maxghost.on") || cards.Count == 0)
        {
            return;
        }
        for (int i = 0; i < cards.Count; i++)
        {
            gameCard c = cards[i];
            if (c == null || !c.gameObject.activeInHierarchy || !isMaximumCard(c))
            {
                continue;
            }
            if (rdMaximumFather(c) == null)
            {
                continue;      // 只从「零件已经齐全」的场面里挑
            }
            // 挑**本体**：没有 Overlay 位（不是部件）、在场上的中列 seq 2
            if ((c.p.location & (UInt32)CardLocation.Overlay) != 0
                || (c.p.location & (UInt32)CardLocation.MonsterZone) == 0
                || c.p.sequence != 2)
            {
                continue;
            }
            rdMaxGhostDone = true;
            c.p.location = (UInt32)(CardLocation.Grave | CardLocation.Overlay);
            c.p.sequence = 0;
            QuickTestTrace.Log("max", "max-ghost-inject id=" + c.get_data().Id
                + " 本体在客户端被标成已离场（core 不知道）⇒ 两张部件应成幽灵，由兜底归位");
            return;
        }
    }

    public override void preFrameFunction()
    {
        base.preFrameFunction();

        // 排查用（`log/qt_maxghost.on`）：造幽灵部件，供兜底的**正面验收**用。
        // ⚠ 必须排在兜底之前、且**本身不受兜底开关影响** —— 否则「阴性对照版」连幽灵都造不出来，
        //   对照就变成「没注入 vs 注入」，失去意义。见 rdMaxGhostInject 的注释。
        rdMaxGhostInject();

        // 🔑 RD 极大部件「本体早就不在了」的兜底 —— **每帧**跑一次，不靠消息驱动。
        // ⛔⛔ 这里必须每帧跑：兜底原来只挂在 `realize`（= 收到消息才跑）里，而宽限期
        //   要「相隔 0.5s 的两次调用」才计得满 ⇒ 玩家停手不再产生消息时，归位与探针
        //   一起静默（用户 2026-09-24 第三次报告，全文见 rdMaxOrphanTick 的注释）。
        //   内部自带 `IsRD` 门 + 每帧一次闩 + `cards.Count==0` 早退 ⇒ OCG/菜单零开销。
        // ⛔⛔ 阴性对照（2026-09-24）：把下面这一行注释掉、其余不动重编，`RDMAX_GHOST=1`
        //   的 **7y2 必须变红**（注入发生了、但此后没有消息 ⇒ 宽限期永远计不满 ⇒ 不归位）。
        //   这一行就是「用户第三次报的那个 bug」的修复本体。
        rdMaxOrphanTick();

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

        // 排查用：对局途中翻一次「俯视角」（qt_topdownmid.on），验「切视角时手牌立刻重摆」。
        if (QuickTestTrace.Enabled)
        {
            topDownMidProbeTick();
        }

        Program.reMoveCam(getScreenCenter());
        if (Program.topDown)
        {
            // 俯视角下同一根滚轮 = **拉镜头（纯平移）**：正俯视没有透视，沿桌面纵深挪相机
            // 不会改变任何一张卡的大小（口径与理由见 `Program.topDownPanZ`）——
            // 用户 2026-09-23 第 2 条要的就是「像斜位视角一样能拉镜头，而不是靠改变卡的大小」。
            //
            // ⛔⛔ **每帧跟随**（第 5 轮起）：实机里有的摊开路径不走 toNearest、或调用时机早于
            //   `someCardIsShowed` 翻转 / `topDownRowOuter` 重算 ⇒ 只弹一次会「弹了个空」。
            //   所以自动值**每一帧**都贴；用户一滚轮（`Program.topDownPanUserTook`）就把控制权
            //   交给玩家，直到**状态切换**（摊开↔收摊、对方手牌公开↔结束）才收回 ——
            //   摊开/收摊/切视角/新对局的显式复位点不变（clearAllShowed / 11278 / Setting）。
            //
            // **自动值取哪个**（第 8 轮起三档）：
            //   ① 摊开态（someCardIsShowed）⇒ `topDownPanNeed()`（最外行底缘 + 空位，「一口气弹到底」）；
            //   ② 闲态 + **鼠标悬停对方手卡**（=「查看对方手牌」，第 8 轮口径：悬停锁存
            //      `topDownOpHandHover` —— 悬停 ⇒ maxZ，光标退到对方手牌行下方才解除，
            //      防止「镜头滑走 → 悬停丢失 → 镜头回来」的永动震荡）⇒ `topDownPanMaxZ`
            //      （对手手牌上缘离屏幕边的空位 = 我方手牌默认的底缘空位，与 rest 对称）；
            //   ③ 其余闲态 ⇒ `topDownPanRestZ`（默认机位，最底下）。
            //   手动滚轮的量程 = [自动值, `topDownPanMaxZ`] —— 闲态也允许上滑一格看一眼对面
            //   手牌行（第 7 轮：「允许上滑一点」），一格（5 世界）大于量程宽（4 世界）⇒
            //   一滚就到 maxZ，恰好是「查看对方手牌时默认滑动到那个距离」的那个距离。
            // ⚠ 滚轮本身仍受 `isShowed` 一道闸（排除菜单/卡组界面：`DeckManager` 自己也在用
            //   滚轮转相机、卡描述滚页）；关态不受影响：走下面那条原路（`cameraPosition.z` + 夹取），
            //   逐字节不变。
            bool spreadNow = someCardIsShowed;
            if (spreadNow != topDownPanSpreadLast)
            {
                // 摊开↔收摊的切换点：上一段里用户滚出来的位置不再延续（收摊回 rest / 摊开弹到底），
                // 自动值重新接管。收摊回 rest 由 clearAllShowed 的显式复位兜同帧，这里是兜底。
                // 悬停锁存一并清零：摊开时卡面都在挪，旧锁存不再代表「用户正看着对面」。
                topDownPanSpreadLast = spreadNow;
                topDownOpHandHoverLatch = false;
                topDownOpHandShownLast = false;
                Program.topDownPanUserTook = false;
            }
            bool opShown = topDownOpHandHover();
            if (opShown != topDownOpHandShownLast)
            {
                // 「查看对方手牌」开始/结束：同上，自动值重新接管（结束 = 回默认机位）。
                topDownOpHandShownLast = opShown;
                Program.topDownPanUserTook = false;
                if (QuickTestTrace.Enabled)
                {
                    QuickTestTrace.Log("ophand", "shown=" + (opShown ? 1 : 0));
                }
            }
            if (spreadNow)
            {
                if (!Program.topDownPanUserTook)
                {
                    Program.topDownPanZ = Program.topDownPanNeed();
                }
            }
            else
            {
                if (!Program.topDownPanUserTook)
                {
                    Program.topDownPanZ = opShown ? Program.topDownPanMaxZ : Program.topDownPanRestZ;
                }
            }
            if (isShowed)
            {
                Program.topDownPanBy(Program.wheelValue);
            }
        }
        else
        {
            Program.cameraPosition.z += Program.wheelValue;
            if (Program.cameraPosition.z < camera_min)
            {
                Program.cameraPosition.z = camera_min;
            }
            if (Program.cameraPosition.z > camera_max)
            {
                Program.cameraPosition.z = camera_max;
            }
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
        // 🔑 「极大怪兽一体化」（设置项 rdMaxIntegrated_，仅 RD、默认开）：读**本帧最新**的
        //   鼠标命中（`Program.pointedGameObject` 在本帧更早处已更新），把三件齐的极大怪兽
        //   按「共同 m、以本体屏点为轴」一体放大；同时托管本体的选项按钮。
        //   ⛔ 只写 position（scale 被本体 1.45 / 手牌行 / 暂存 0.001 三方占用）。
        //   ⛔ 本 tick 必须排在 gameCard.Update（close-up / button shower）**之前** ——
        //     preFrameFunction 就是那个位置，所以它置的标记当帧生效、不用等下一帧。
        //   非 RD / 菜单 / 非决斗态内部直接 reset 早退 ⇒ OCG 侧一个像素不变。
        rdMaxIntegratedTick();
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
        // 对方那一排也要量：**它才是俯视角取景的上边界**。对方手牌排的原生 z 是 `19×fieldSize`
        // （= 22.99），比我方（`-(3+15×fieldSize)` = -21.15）还外 1.84 ⇒ 拉近镜头时先被切掉的是它。
        // 上一版探针只报我方，所以「拉近到 0.98 把对方手牌切在屏幕顶」这件事一直被判绿 —— 见 `[ohc]`。
        string so = "";
        for (int i = 0; i < cards.Count; i++)
        {
            gameCard hc = cards[i];
            if (hc == null || hc.p.location != (UInt32)CardLocation.Hand || hc.p.controller > 1)
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
            if (hc.p.sequence == 0)
            {
                // 放大量的**来源**：手牌卡的世界坐标 + 碰撞盒（≈卡面）的实际世界尺寸。
                // 独立 tag/独立一行，不碰上面 `[sN "名" ...]` 的结构（那一段有验收脚本按名解析）。
                // 俯视角「能不能再放大」完全由这张卡的 z 跨度决定，所以这里要的是硬数，不是估的。
                // `ctrl=` 必带：两侧的 z 公式不同（我方 -(3+15·fs)、对方 19·fs），要分别对得上。
                QuickTestTrace.Log("hcgeom", "ctrl=" + hc.p.controller + " seq=" + hc.p.sequence
                    + " world=(" + aim.x.ToString("F2") + "," + aim.y.ToString("F2")
                    + "," + aim.z.ToString("F2") + ")"
                    + " size=" + (mc != null ? mc.bounds.size.x.ToString("F3") + "x"
                        + mc.bounds.size.y.ToString("F3") + "x"
                        + mc.bounds.size.z.ToString("F3") : "无碰撞盒")
                    + " lossy=" + hc.gameObject.transform.lossyScale.x.ToString("F3"));
            }
            Vector3 sp = Program.camera_game_main.WorldToScreenPoint(aim);
            string nm = "";
            YGOSharp.Card d = hc.get_data();
            if (d != null)
            {
                nm = d.Name;
            }
            string item = "[s" + hc.p.sequence + " \"" + nm + "\" screen=(" + Mathf.RoundToInt(sp.x)
                + "," + Mathf.RoundToInt(Screen.height - sp.y) + ")"
                // 卡自己的世界欧拉角。**平铺 = x 0**（手牌排本来就是平铺在台面上的，
                // 见 `_probe_topdown.py` 的判据：关态实测 y 与「z=-21.1 平铺」只差 0.5px）。
                // x 用 DeltaAngle 折到 [-180,180]，免得 -30 报成 330 看不出来。
                + " rot=(" + Mathf.RoundToInt(Mathf.DeltaAngle(0f, hc.gameObject.transform.eulerAngles.x))
                + "," + Mathf.RoundToInt(hc.gameObject.transform.eulerAngles.y)
                + "," + Mathf.RoundToInt(Mathf.DeltaAngle(0f, hc.gameObject.transform.eulerAngles.z)) + ")"
                + " cond=" + hc.condition
                + (mc == null ? " 无碰撞盒" : (mc.enabled ? " 可点" : " 碰撞盒禁用"))
                + " es=" + hc.ES_diag()
                + " btn=" + (hc.allButtons == null ? -1 : hc.allButtons.Count)
                + "]";
            if (hc.p.controller == 0)
            {
                s += item;
            }
            else
            {
                so += item;
            }
        }
        // 变化检测用「两排拼起来」当键，省一个静态字段（`lastHandDump` 只在本函数里用）。
        string key = s + "||" + so;
        if ((s.Length == 0 && so.Length == 0) || key == lastHandDump)
        {
            return;
        }
        lastHandDump = key;
        if (s.Length > 0)
        {
            QuickTestTrace.Log("hc", "我方手牌落点 " + s);
        }
        if (so.Length > 0)
        {
            QuickTestTrace.Log("ohc", "对方手牌落点 " + so);
        }
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
                    QuickTestTrace.Log("rule", "sibyl_name 带数据 -> MasterRule=" + MasterRule);
                }
                else
                {
                    MasterRule = 3;
                    QuickTestTrace.Log("rule", "sibyl_name 无数据 -> MasterRule=3（兜底）");
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
                QuickTestTrace.Log("rule", "Start duel_rule -> MasterRule=" + MasterRule);
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
                    // RD 只有一个主要阶段（没有 MP2）⇒ 别叫「主要阶段1」（用户 2026-09-21 口径：
                    // 「rd 下的主要阶段不要叫主要阶段1了，就叫主要阶段」）。
                    // 这一处是**盘面下方那行中文提示**（ES_hint = ES_turnString + ES_phaseString
                    // ⇒「我方的 主要阶段1」），不是阶段条上的 MP1 缩写、也不是大字贴图。
                    ES_phaseString = GameModeManager.IsRD
                        ? InterString.Get("主要阶段")
                        : InterString.Get("主要阶段1");
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
                    // RD 没有主要阶段2 —— 但 core **确实**还会发这一次（实测日志 8 → 256 → 512，
                    // 见 qt_19624.log 的 21:26:52.819 那条 RD-skip）。既然它不是 RD 的一个阶段，
                    // 名字就不要跟着翻成「主要阶段2」：hint 停在上一阶段（战斗阶段），
                    // 与阶段条上根本没有这一格、也不报大字保持一致。
                    if (!GameModeManager.IsRD)
                    {
                        ES_phaseString = InterString.Get("主要阶段2");
                    }
                    gameField.currentPhase = GameField.ph.mp2;
                }
                if (ph == 0x200)
                {
                    ES_phaseString = InterString.Get("结束阶段");
                    gameField.currentPhase = GameField.ph.ep;
                }
                //printDuelLog(InterString.Get("进入[?]", ES_turnString + ES_phaseString));
                ES_hint = ES_turnString + ES_phaseString;
                if (QuickTestTrace.Enabled)
                {
                    // 盘面下方那行中文提示的**实际文本** —— 阶段名改口径（RD 去掉「1」）之后，
                    // 只有把真串记下来才能在第 6h 段验收里咬住它，光看代码不知道屏上是什么。
                    QuickTestTrace.Log("phase", "hint=" + ES_hint
                        + " mode=" + GameModeManager.ModeLabel);
                }
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
                        // ⛔ 带 Overlay 位的卡**不是手牌**（2026-09-24）：core 收成素材那条 MOVE 的
                        //   location 可能是 Overlay|Hand（0x82），极大部件一旦被这行擦成未知卡
                        //   （`erase_data()` ⇒ `data.Id = 0`），`isMaximumCard()` 就恒 false
                        //   ⇒ 三件不齐 ⇒ 大框掉档「变形」（用户症状三）。
                        //   排除 Overlay 位对 OCG 零影响：手牌里的卡不可能带 Overlay 位。
                        if ((cards[i].p.location & (UInt32)CardLocation.Hand) > 0
                            && (cards[i].p.location & (UInt32)CardLocation.Overlay) == 0)
                        {
                            if (cards[i].p.controller == player)
                            {
                                cards[i].erase_data();
                            }
                        }
                        else if ((cards[i].p.location & (UInt32)CardLocation.Hand) > 0
                                 && (cards[i].p.location & (UInt32)CardLocation.Overlay) > 0
                                 && isMaximumCard(cards[i]))
                        {
                            // 探针缺口补录（2026-09-24）：这条路径以前**完全静默** —— 部件被擦成
                            // 未知卡只在卡面上看得出来，日志里一个字都没有。归一修好之后
                            // 正常局应当 **0 条**；一旦出现就说明 core 又发了「Overlay|Hand」
                            // 并且入口那次归一没赶上（可据此判「是第二道闸在兜底」）。
                            QuickTestTrace.Log("max", "shuffle-skip-overlay code="
                                + cards[i].get_data().Id
                                + " p=" + cards[i].p.controller + "/" + cards[i].p.location
                                + "/" + cards[i].p.sequence + " player=" + player);
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
                // RD 的战斗阶段是**单行道**（主阶段 → 战斗 → 结束，RD 没有主阶段2），
                // 但 core 仍会把 mp 发成 1（RD core 照 OCG 的 SelectBattleCmd 填字节）⇒
                // 盘面上不能给这个「退回主要阶段」的出口。
                //
                // ⚠ 必须是「不建」而不是「建完再藏」：addHashedButton 里接着就
                // iTween.ScaleTo 把它弹到右侧面板上（gameInfo.instance_btnPan），事后隐藏
                // 挡不住弹出那一帧；而且它还会顺手把阶段条上的 colliderMp2 热区打开
                // （phaser 里那个热区直接 sendReturn(retOfMp=2)，等于绕过按钮也能退回去）。
                // 用户 2026-09-21：「rd 下战斗阶段时右边还会弹主要阶段按钮，直接隐藏」。
                bool allowReturnMp = (mp == 1) && !GameModeManager.IsRD;
                if (QuickTestTrace.Enabled)
                {
                    QuickTestTrace.Log("battle", "SelectBattleCmd core mp=" + mp + " ep=" + ep
                        + " mode=" + GameModeManager.ModeLabel
                        + " returnMp=" + (allowReturnMp ? 1 : 0));
                }
                if (allowReturnMp)
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
                        // 🔑 RD 极大怪兽的这次「特殊召唤」**就是**极大召唤（手牌特殊召唤手续 =
                        //   `RushDuel.MaximumSummonOperation`），按钮文案跟着改（用户 2026-09-24 第 3 条）。
                        //   ⚠ 只换文案：`response` 与 `superButtonType.spsummon` 一律不动
                        //     （`hint` 只喂 `iconSetForButton.setText`，见 gameButton.show）。
                        //   ⚠ 判据用 isRdMaximumCard（IsRD 门 + type&0x8000）—— 与渲染层同一口径；
                        //     OCG 侧恒 false ⇒ 一个字符都不变。
                        //   ⚠ `InterString.Get` 首次遇到新键会自动往 config/translation.conf 落一行
                        //     `极大召唤@ui->极大召唤@ui`（与既有的 `特殊召唤@ui` 同一机制）。
                        string spSummonLabel = card.isRdMaximumCard()
                            ? InterString.Get("极大召唤@ui")
                            : InterString.Get("特殊召唤@ui");
                        if (card.query_hint_button(spSummonLabel) == false)
                        {
                            btn = new gameButton(((i << 16) + 1), spSummonLabel, superButtonType.spsummon);
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
                    // 🔑 RD 极大怪兽的 L/R 部件（含本体）不做「确认展示」（用户 2026-09-22 原话：
                    //    「阻止左右部件作为中间件素材的动画 —— 不是说阻止其成为极限怪兽，
                    //    是只不播放这个动画」）。
                    //
                    //    病根（两条叠一起，用户截图里「悬浮在场地中间的两张卡」就是它）：
                    //    ① 极限召唤会把那两张手卡作为素材公开 —— ConfirmCards 走 confirm() ⇒
                    //       animation_confirm_screenCenter（screenFader）把整张卡拉到屏幕中央、
                    //       停 0.5s，这就是「像给召唤者确认一样」的展示步骤；
                    //    ② 紧接着的 Move/Overlay 消息在展示期间到达，realize/TweenTo 会清掉
                    //       iTween 链（UIHelper.clearITWeen），confirm 的回位补间
                    //       （confirm_step_3）随之被杀 ⇒ 卡停在半路，要**手动点一下**才回原位
                    //       —— 正是用户第 4 条的原话「需要手动点一下让他们回归」。
                    //
                    //    ⇒ 极大卡一律不进这条展示管线：信息并不丢失（部件紧接着就会以部件
                    //      形态立在本体两侧、本体走 SpSummoning 的出场演出），流程还因此少等
                    //      t2 的 50 帧。OCG 与非极大卡一个字节不变。
                    bool rdMaxSkipShow = GameModeManager.IsRD && card != null && card.isRdMaximumCard();
                    if (rdMaxSkipShow)
                    {
                        QuickTestTrace.Log("max", "confirm skip code=" + code
                            + " p=" + (card.p.controller) + "/" + (card.p.location)
                            + "/" + (card.p.sequence));
                    }
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
                    if (showC && !rdMaxSkipShow)
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
                // RD 只有四个阶段（抽卡 / 主要阶段1 / 战斗 / 结束）：准备阶段与主要阶段2
                // **既不显示在阶段条上，到了也不报**（阶段条那一半见
                // gameField.applyPhaseBarLayout → lazyBTNMOVER.shiftRD）。
                //
                // 为什么这里也要拦一道：阶段条是「静态的格位」，这条是「动态的大字」。
                // 只说「条上看不见」不够 —— core 哪一版真把这两个阶段发出来（例如某个
                // 自制卡效果触发了 Standby），屏幕上照样会蹦出 SP / MP2 的大字。
                // 用户口径是「被删掉的阶段既不显示在阶段条里，同时也不会显示说到了这个阶段」。
                bool rdNoSpMp2 = GameModeManager.IsRD;
                if (QuickTestTrace.Enabled)
                {
                    // big= 记下大字选了哪张贴图 —— MP1 在 RD 里应选 mp1RD（不带 1 的那张），
                    // 验收只看这行就够，不必等肉眼对截图。
                    string big = "?";
                    if (GameStringHelper.differ(phrase, (long)DuelPhase.BattleStart)) big = "bp";
                    else if (GameStringHelper.differ(phrase, (long)DuelPhase.Draw)) big = "dp";
                    else if (GameStringHelper.differ(phrase, (long)DuelPhase.End)) big = "ep";
                    else if (GameStringHelper.differ(phrase, (long)DuelPhase.Main1))
                        big = (rdNoSpMp2 && GameTextureManager.mp1RD != null) ? "mp1RD" : "mp1";
                    else if (!rdNoSpMp2 && GameStringHelper.differ(phrase, (long)DuelPhase.Main2)) big = "mp2";
                    else if (!rdNoSpMp2 && GameStringHelper.differ(phrase, (long)DuelPhase.Standby)) big = "sp";
                    QuickTestTrace.Log("phase", "new ph=" + phrase
                        + " mode=" + GameModeManager.ModeLabel
                        + " big=" + big
                        + ((rdNoSpMp2 && (GameStringHelper.differ(phrase, (long)DuelPhase.Standby)
                                          || GameStringHelper.differ(phrase, (long)DuelPhase.Main2)))
                           ? " RD-skip" : ""));
                }
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
                    // RD 只有一个主要阶段 ⇒ 大字也别带「1」（mp1_rd.png = mp1.png 擦掉
                    // 末尾的「1」并重新居中；贴图缺了回落 mp1 —— 只少个“去 1”，不炸）。
                    gameField.animation_show_big_string(GameModeManager.IsRD && GameTextureManager.mp1RD != null
                        ? GameTextureManager.mp1RD
                        : GameTextureManager.mp1);
                }
                if (!rdNoSpMp2 && GameStringHelper.differ(phrase, (long)DuelPhase.Main2))
                {
                    gameField.animation_show_big_string(GameTextureManager.mp2);
                }
                if (!rdNoSpMp2 && GameStringHelper.differ(phrase, (long)DuelPhase.Standby))
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
                    rdMaxNoteSummonDeclared(code, card);   // 🔑 RD 极大召唤签名的锚（见字段说明）
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
                rdMaxClearSummonDeclared();      // 🔑 宣告结束（OCG 下数组恒 0，无副作用）
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
                    rdMaxNoteSummonDeclared(code, card);   // 🔑 RD 极大召唤签名的锚（见字段说明）
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
                rdMaxClearSummonDeclared();      // 🔑 宣告结束
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
                    // 直接攻击（打玩家）：箭头指向该侧手牌的**外沿**。
                    // ⛔ 俯视角下原生那两个落点（−23.15 / +24.99）都在取景范围之外 ⇒ 箭头飞出去就
                    //    看不见了；换算交给 `tableauAttackTargetAbsZ`（关态原样返回 ⇒ 逐字段等价）。
                    bool attacker_bool_me = (p1.controller == 0);
                    if (!attacker_bool_me)
                    {
                        VectorAttackTarget = new Vector3(0, 3,
                            -Program.tableauAttackTargetAbsZ(5f + 15f * Program.fieldSize));
                    }
                    else
                    {
                        if (gameField.isLong)
                        {
                            VectorAttackTarget = new Vector3(0, 3,
                                Program.tableauAttackTargetAbsZ(2f + (19f + gameField.delat) * Program.fieldSize));
                        }
                        else
                        {
                            VectorAttackTarget = new Vector3(0, 3,
                                Program.tableauAttackTargetAbsZ(2f + (19f) * Program.fieldSize));
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
                        var number = game_card.add_one_decoration(Program.I().mod_ocgcore_number, 3, new Vector3(Program.tableauFrontX, 0, 0), "number", false);
                        number.game_object.GetComponent<number_loader>().set_number((int)value, 3);
                        number.scale_change_ignored = true;
                        number.game_object.transform.localScale = new Vector3(1, 1, 1);
                        number.game_object.transform.eulerAngles = new Vector3(Program.tableauFrontX, 0, 0);
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
        // 俯视角下洗完之后要落回**新的**手牌排位置与倍数：旧口径把落点写死成 21.15 / 22.99，
        // 而俯视角的取景只到 cover ⇒ 点一次「洗切手牌」整排就飞到屏幕外（用户 2026-09-23 反馈）。
        // `tableauHandRowAbs` / `UA_give_scale` 关态原样返回 ⇒ 逐字段等价于旧实现。
        float handScale = Program.tableauHandScale(1);
        for (int i = 0; i < cards.Count; i++) if (cards[i].gameObject.activeInHierarchy)
            {
                if ((cards[i].p.location & (UInt32)CardLocation.Hand) > 0)
                {
                    if (cards[i].p.controller == player)
                    {
                        Vector3 position;
                        if (cards[i].p.controller == 0)
                        {
                            position = new Vector3(0, 0, -Program.tableauHandRowAbs(
                                3f + 15f * Program.fieldSize, 1, 0, handScale));
                        }
                        else
                        {
                            if (gameField.isLong)
                            {
                                position = new Vector3(0, 0, Program.tableauHandRowAbs(
                                    (19f + gameField.delat) * Program.fieldSize, 1, 0, handScale));
                            }
                            else
                            {
                                position = new Vector3(0, 0, Program.tableauHandRowAbs(
                                    (19f) * Program.fieldSize, 1, 0, handScale));
                            }
                        }
                        cards[i].UA_give_scale(handScale);
                        cards[i].animation_rush_to(position, new Vector3(Program.tableauAngle(-30f), 0, 180));
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

    /// <summary>
    /// 「本帧画面上**真的有一批卡摊开、等着点『确认完毕』**」—— 由实时消息循环逐条累计
    /// （见 `someCardIsShowed = true` 那处），也就是游戏自己用来**挂/摘「确认完毕」按钮**的那个标志。
    /// ⚠ 从 private 放成 public 是为了让 `Program.topDownPanAuto()` 能问它一句
    ///   「现在要不要把取景自动弹到摊开那一行」（用户 2026-09-23 第 3 轮口径：
    ///   「查看卡牌时…自动计算可以扩充的距离并**弹过去**」）。语义一个字没改，只是加了可见性。
    /// </summary>
    public bool someCardIsShowed = false;

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
                // 手牌排的放大（俯视角）在这里**统一还原**：一手牌打出去 / 进墓地 / 回牌组之后，
                // 这次 realize 就不再被 `lines` 选中，必须缩回 1，否则会带着 1.5 倍留在场上。
                // `UA_give_scale(1)` 只碰「自己放大过的卡」（handRowScaled 闸），
                // 不会踩掉卡根 scale 的创建动画（同 rdMaxTrioScaled 的教训）。关态下恒为无写入。
                cards[i].UA_give_scale(1f);
                cards[i].opMonsterWithBackGroundCard = false;
                cards[i].isMinBlockMode = false;
                cards[i].overFatherCount = 0;
                // 🔑 RD 极大部件不许进「展示行」（用户 2026-09-22 实测截图：极大召唤完成后
                //    屏幕底部 2×2 网格，上面两张是手卡、下面两张就是 L/R）。
                //    病根：isShowed 的复位只在「非 Overlay 且在怪兽/魔陷区」时发生（本函数
                //    下面那段），而部件在选素材阶段恒带 isShowed=true（自家手牌那条
                //    「Hand+controller==0 ⇒ isShowed=true」+ SelectCard 的 cardsSelectable
                //    分支）；收成素材后位置是 MZONE|Overlay ⇒ 复位分支被 Overlay 位挡死，
                //    若召唤窗口里没有恰好落在 MZONE 中间态上的 realize，这个 true 就永远
                //    没人清 ⇒ 每次 realize 都把它们排进 isShowed 的 lines（正是手卡所在的
                //    底部网格，L/R 的 controller/location 与手卡不同 ⇒ 单独成行 = 第二行）。
                //    ⇒ 在这里（无条件循环，先于一切 cookie/条件分支）按部件口径清死：
                //      · isRdMaximumPiece       = 已收成本体素材（MZONE|Overlay）；
                //      · isRdMaximumPiecePending= 召唤操作中短暂落在本侧怪兽区的中间态
                //        （0.3~0.4s；若这个窗口没有 realize 也照样拦得住）。
                //    信息不丢：部件紧接着就按部件形态常显在本体两侧（6o/6q 那套）。
                //    OCG 侧 isRdMaximumCard() 恒 false（IsRD 门），一个字节不变。
                if (cards[i].isRdMaximumCard()
                    && (cards[i].isRdMaximumPiece() || isRdMaximumPiecePending(cards[i])))
                {
                    if (cards[i].isShowed)
                    {
                        QuickTestTrace.Log("max", "showcase-unshow code=" + cards[i].get_data().Id
                            + " p=" + cards[i].p.controller + "/" + cards[i].p.location
                            + "/" + cards[i].p.sequence
                            + " （极大部件不进展示行，2026-09-22 用户实测遗留展示）");
                        cards[i].isShowed = false;
                    }
                }
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
                    // ⛔ 「手牌」这一档必须排除带 Overlay 位的卡（2026-09-24 定案）：
                    //   core 那条「收成素材」的 MOVE 给的 location 可能是 Overlay|Hand（0x82），
                    //   而极大部件**不是手牌** —— 它常显在本体左右。旧写法会把上面那段
                    //   `showcase-unshow` 的清理**当帧原样抵消**（log/qt_19412.log 22:32:53.411
                    //   里 `showcase-unshow` 紧跟着两条 `showcase-LEAK` 就是这个顺序），
                    //   部件随即被排进底部展示行 —— 用户看到的「LR 卡回手卡」。
                    //   排除 Overlay 位对 OCG 是零影响：真素材的 location 是 `父卡位置|Overlay`
                    //   （不带 Hand 位），本来就落不进这个条件。
                    //   `normalizeRdMaximumPiece` 已在入口修过一次，这里是第二道闸
                    //   （本体被效果挪走、入口那次找不到本体时仍然拦得住）。
                    if ((((cards[i].p.location & (UInt32)CardLocation.Hand) > 0)
                         && (cards[i].p.controller == 0)
                         && (cards[i].p.location & (UInt32)CardLocation.Overlay) == 0)
                        || ((cards[i].p.location & (UInt32)CardLocation.Unknown) > 0))
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
                        // 🔑 哨兵：极大部件（MZONE|Overlay）进了展示行 = 上面的 showcase-unshow
                        //    修复失效（验收 _probe_maxui.py 咬「全日志 0 条 LEAK」）。
                        if (cards[i].isRdMaximumPiece())
                        {
                            QuickTestTrace.Log("max", "showcase-LEAK code=" + cards[i].get_data().Id
                                + " p=" + cards[i].p.controller + "/" + cards[i].p.location
                                + "/" + cards[i].p.sequence
                                + " （极大部件漏进展示行 = 修复失效！）");
                        }
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

        // 俯视角下这一排要**整体放大**：正俯视没有透视，不放大手牌就只能和牌桌上的牌一样小
        // （用户 2026-09-23「手牌不易读」的根因，口径 = `Program.topDownHandMaxScale`）。
        // 行数与倍数**一起算**：n 行都得塞进行带，行多了倍数自动变小（`Program.tableauHandScale`）
        // —— 这条同时修掉「展示行第二行在俯视角下整行出屏」。
        int handLines = lines.Count;
        float handScale = Program.tableauHandScale(handLines);
        // 排查用：每行的**落定目标** z。判据要的是目标值，不是补间途中的即时 transform
        // （`UA_flush_all_gived_witn_lock(rush)` 在非 rush 时是补间，当场读 transform 会读到半路）。
        float[] tdRowZ = new float[handLines];
        for (int line_index = 0; line_index < handLines; line_index++)
        {
            for (int index = 0; index < lines[line_index].Count; index++)
            {
                Vector3 want_position = Vector3.zero;
                want_position.y = 0;
                // 行从屏幕外沿往里堆（口径 = `Program.tableauHandRowAbs`）；关态原样返回
                // ⇒ 关掉开关逐字段等价于旧实现。
                // ⚠ 原生 |z|（`3 + 15×fieldSize`）由调用点算好传进去，不在这里重抄排布公式。
                float handRow0 = 3f + 15f * Program.fieldSize;
                want_position.z = -Program.tableauHandRowAbs(
                    handRow0 + line_index * 5f, handLines, line_index, handScale);
                tdRowZ[line_index] = want_position.z;
                // ⛔ 行距必须**跟 s 一起放大**：`get_left_right_indexEnhanced` 的落点是按 left/right
                //    半宽线性分配的 ⇒ 把半宽乘 s，整排的**节距**就同步乘 s，卡的相对间距（缝宽）
                //    与关态完全一致。不乘就出事：卡宽 3s = 4.5 会超过原行距 —— line0 在
                //    **恰 6 张**那一档行距只有 `20/(6-1) = 4.0`（≤5 张才是 5.0）
                //    ⇒ 相邻手牌互相压住 11%。
                //    这也是「回到 60° 观感」的另一半：60° 的手牌是 95.6px 卡宽 + 159.2px 节距，
                //    正俯视放大后是 91.1px + 152.2px —— 卡宽与节距两样都追到 95%。
                //    `handScale` 关态恒 1 ⇒ 半宽原样、逐字段等价旧实现。
                if (line_index == 0)
                {
                    want_position.x = UIHelper.get_left_right_indexEnhanced(-10 * handScale, 10 * handScale, index, lines[line_index].Count, 5);
                }
                else
                {
                    want_position.x = UIHelper.get_left_right_indexEnhanced(-15 * handScale, 15 * handScale, index, lines[line_index].Count, 7);
                }
                lines[line_index][index].cookie_cared = true;
                lines[line_index][index].UA_give_condition(gameCardCondition.floating_clickable);
                lines[line_index][index].UA_give_scale(handScale);
                lines[line_index][index].UA_give_position(want_position);
                lines[line_index][index].UA_give_rotation(new Vector3(Program.tableauAngle(-30f), 0, 0));
                lines[line_index][index].UA_flush_all_gived_witn_lock(rush);
            }
        }

        // 「拉镜头」的行程要够得着**最外那一行**：行带现在是往外堆的（`Program.tableauHandRowAbs`），
        // 一副 40 张的卡组摊开能排到 |z| ≈ 50 —— 行程不跟着长就是「看得见但拉不到」。
        // ⛔ 存的是**带符号**的 z（不是绝对值）：摊开我方卡堆时那一行在 −z 侧、摊开对方的在 +z 侧，
        //    「自动弹过去」必须往**那一侧**弹（见 `Program.topDownPanAuto`）——
        //    丢了符号就成了「往反方向弹」，越弹越看不见。
        Program.topDownRowOuter = 0f;
        for (int k = 0; k < handLines; k++)
        {
            float az = tdRowZ[k] < 0f ? -tdRowZ[k] : tdRowZ[k];
            float aout = Program.topDownRowOuter < 0f ? -Program.topDownRowOuter : Program.topDownRowOuter;
            if (az > aout)
            {
                Program.topDownRowOuter = tdRowZ[k];
            }
        }

        if (QuickTestTrace.Enabled && handLines > 0)
        {
            // 行带结构（俯视角取景的硬指标）：行数 n、每行共用的缩放 s、每行的 c/loc/卡数/落定 z。
            // 用户 2026-09-23「摊开那批确认卡被缩小、而不是拉镜头」直接对得上这里的 `s` 与各行 z。
            var sb = new System.Text.StringBuilder();
            sb.Append("topDown=").Append(Program.topDown ? 1 : 0)
              .Append(" n=").Append(handLines)
              .Append(" s=").Append(handScale.ToString("F3"));
            for (int k = 0; k < handLines; k++)
            {
                sb.Append(" | L").Append(k)
                  .Append(" c=").Append(lines[k][0].p.controller)
                  .Append(" loc=").Append(lines[k][0].p.location)
                  .Append(" n=").Append(lines[k].Count)
                  .Append(" z=").Append(tdRowZ[k].ToString("F2"));
            }
            QuickTestTrace.Log("rowband", sb.ToString());
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
        // 对方手牌排：与我方**同一口径**（整体放大 + 行贴屏幕外沿）。对方不分行，恒 1 行。
        float opHandScale = Program.tableauHandScale(1);
        for (int index = 0; index < line.Count; index++)
        {
            Vector3 want_position = Vector3.zero;
            want_position.y = 0;
            // 对方原生是 `19×fieldSize = 22.99`，比我方（21.15）还外 1.84 ⇒ 它才是上边那条瓶颈
            // （实机症状：被切在屏幕顶的从来就是这一排）。这里与我方共用同一条式子 ⇒ 两侧对齐。
            // ⚠ `index * 0.015f` 那点叠牌微差原样保留（0.075 世界，屏上 ~1.5px，不会出带）。
            if (gameField.isLong)
            {
                float opRow0 = (19f + gameField.delat) * Program.fieldSize;
                want_position.z = Program.tableauHandRowAbs(opRow0, 1, 0, opHandScale) + index * 0.015f;
            }
            else
            {
                float opRow0 = 19f * Program.fieldSize;
                want_position.z = Program.tableauHandRowAbs(opRow0, 1, 0, opHandScale) + index * 0.015f;
            }
            // 行距同样要跟 `opHandScale` 一起放大（与我方同一个理由，见上面 `lines` 那一处）：
            // 只放大卡不放大行距 ⇒ 对方手牌会互相压住。关态 `opHandScale` 恒 1 ⇒ 原样。
            want_position.x = UIHelper.get_left_right_indexEnhanced(10 * opHandScale, -10 * opHandScale, index, line.Count, 5);
            line[index].cookie_cared = true;
            line[index].UA_give_scale(opHandScale);
            line[index].UA_give_position(want_position);
            if (line[index].get_data().Id > 0)
            {
                line[index].UA_give_rotation(new Vector3(Program.tableauAngle(-30f), 0, 0));
            }
            else
            {
                line[index].UA_give_rotation(new Vector3(Program.tableauAngle(-30f), 0, 180));
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
                // ⛔ RD 极大怪兽本体**不是超量怪兽**：它的 L/R 是按原格位常显在本体左右的部件
                //   （见 maximumPieceWorldPosition），所以既不给「素材光点」、也不给
                //   「查看素材」按钮 —— 那两样在 OCG 是「叠在父卡身上、点开查看」的入口，
                //   放到极大怪兽上就是用户不要的「像检查超量素材那样把部件拉出来」。
                //   `overFatherCount` 照旧赋值（L/R 确实是一体的素材，探针/表现层都在用）。
                bool rdMax = GameModeManager.IsRD && isMaximumCard(cards[i]);
                cards[i].set_overlay_light(rdMax ? 0 : overC);
                cards[i].set_overlay_see_button(!rdMax && overlayed_cards.Count > 0);
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
                    my_p_cards[0].UA_give_rotation(new Vector3(Program.tableauAngle(-60f), -45, 0));
                    my_p_cards[0].UA_give_condition(gameCardCondition.floating_clickable);
                    my_p_cards[0].UA_flush_all_gived_witn_lock(rush);
                    my_p_cards[1].cookie_cared = true;
                    my_p_cards[1].UA_give_position(new Vector3(9.62f * real - 1, 5, -11.5f * real - 1));
                    my_p_cards[1].UA_give_rotation(new Vector3(Program.tableauAngle(-60f), 45, 0));
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
                    op_p_cards[0].UA_give_rotation(new Vector3(Program.tableauAngle(-90f), 45, 0));
                    op_p_cards[0].UA_give_condition(gameCardCondition.floating_clickable);
                    op_p_cards[0].UA_flush_all_gived_witn_lock(rush);
                    op_p_cards[1].cookie_cared = true;
                    op_p_cards[1].UA_give_position(new Vector3(-10.1f * real + 1, 5, 11.5f * real - 1));
                    op_p_cards[1].UA_give_rotation(new Vector3(Program.tableauAngle(-90f), -45, 0));
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
                    my_p_cards[0].UA_give_rotation(new Vector3(Program.tableauAngle(-90f), -45, 0));
                    my_p_cards[0].UA_give_condition(gameCardCondition.floating_clickable);
                    my_p_cards[0].UA_flush_all_gived_witn_lock(rush);
                    my_p_cards[1].cookie_cared = true;
                    my_p_cards[1].UA_give_position(new Vector3(14.65f * real, 5, -10f));
                    my_p_cards[1].UA_give_rotation(new Vector3(Program.tableauAngle(-90f), 45, 0));
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
                    op_p_cards[0].UA_give_rotation(new Vector3(Program.tableauAngle(-90f), 45, 0));
                    op_p_cards[0].UA_give_condition(gameCardCondition.floating_clickable);
                    op_p_cards[0].UA_flush_all_gived_witn_lock(rush);
                    op_p_cards[1].cookie_cared = true;
                    op_p_cards[1].UA_give_position(new Vector3(-15.2f * real, 5, 8f));
                    op_p_cards[1].UA_give_rotation(new Vector3(Program.tableauAngle(-90f), -45, 0));
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

        // ── RD 极大怪兽「大框」：本帧先算一次「哪一侧三件齐了 / 落稳了」──
        // 放在这里而不是各自的入口：位置是每帧重给的（下一段循环），换图必须与它**同一帧**，
        // 否则会出现「框开了但卡还没贴上去」的错帧（用户要的是「切换无延迟」）。
        //
        // ⚠ 「齐」与「落稳」两个状态都留着，但**换图只看「齐」**：
        //   · 齐（不看位置）⇒ 放大 + 贴紧 + **换图**。理由：框是画在场地贴图上的，而卡是
        //     客户端自己画的 —— 两者要看成一回事，唯一的条件是「框的侧别 == 卡的侧别」，
        //     而卡的侧别就是 `c.p.controller`（摆位 `get_point_worldposition` 用的也是它）。
        //     所以：齐在哪一侧就亮哪一侧，任何额外的位置闸都只会让框**晚亮**。
        //     ⛔ 上一版拿「落稳」换图，就是用户 2026-09-22 第 2 条的病根：
        //        「极大召唤刚完成时没有正确切换为极大模式的框，还保留着小框」——
        //        真机的 L/R 是 tween 过去的，等「三张都到位且不再动」要 1~2 秒，
        //        那段时间格子系统还照原样画着（小框）。
        //     ⚠ 出场飞行期确实会先亮「错侧」170ms，但**那一侧此刻真的画着这三张卡**
        //        （造局日志 54.019 本体落在 z=-675；探针 `want=` 也是按同一个 controller
        //        算出来的）⇒ 框跟着卡走才是对的，那不是错帧。判据见验收 6u。
        //   · 落稳（三张都到位、落平、且不再移动）⇒ **只写进日志当诊断**（`pres=`）：
        //     `pres=0 mask=3` 就是「三件齐了、但三张还没到位」的痕迹，见 rdMaximumLanded。
        // ⛔ 这两个状态每帧只能算一次（落稳要跟上一帧的位置比）⇒ 统一走 rdMaxRefreshTrioState，
        //   下面和探针都读 rdMaxTrioMe/Op、rdMaxSettledMe/Op 这几个字段。
        // ⛔ **必须 force**：一帧里可能调两次 realize，而三件状态正好在两条消息之间翻边
        //   （实测同帧两次调用之间 controller 从 0 翻成 1）—— 吃缓存会把翻边前的状态喂给后一次，
        //   放大当帧掉回 100。见 rdMaxRefreshTrioState 的注释。
        rdMaxRefreshTrioState(true);
        if (gameField != null)
        {
            gameField.setMaximumBand((rdMaxTrioMe ? 1 : 0) | (rdMaxTrioOp ? 2 : 0),
                                     (rdMaxSettledMe ? 1 : 0) | (rdMaxSettledOp ? 2 : 0));
        }

        // 🔑 RD 极限召唤「三件同帧入场」（用户 2026-09-24 第 2 条）：先到的 L/R 先按进暂存，
        //   等本体那一条 MOVE 进来的**同一帧**一起放行 ⇒ 三张卡从同一处、同一时长飞出去。
        //   必须放在这一帧的摆位循环**之前**：放行只清标记，位置由紧随其后的循环给。
        //   时序依据：`practicalizeMessage` 的 `case GameMessage.Move: realize();` 是每条 MOVE
        //   当帧 realize（见 6726 行），本体到场的那一帧就落在这里。
        rdMaxApplyStageHold();

        // 🔑 部件「本体早就不在了」的兜底（用户 2026-09-24 第二次报告）：必须在摆位循环**之前**
        //   跑 —— 它改的是 `p.location`，而摆位循环（`rdMaximumWantPosition`）与探针都读它，
        //   晚一步改就要多错一帧。判据、宽限期与理由见 rdMaxParkOrphans。
        rdMaxParkOrphans();

        for (int i = 0; i < cards.Count; i++) if (cards[i].gameObject.activeInHierarchy)
                if (cards[i].cookie_cared == false)
                {
                    // 被暂存按住的部件：本帧不给档位/位置/缩放（放行那一帧标记已清，走下面的正常路径）。
                    if (cards[i].rdMaxHeld)
                    {
                        continue;
                    }
                    // ⛔ RD 极大怪兽的 L/R 部件是个**例外**：它们不是「叠在父卡身上、点开查看的
                    //   素材」，而是自己在场上左右各占一格常显的部件（见 maximumPieceWorldPosition）。
                    //   给它们的档位要同时满足三条（用户 2026-09-22 的原话：
                    //   「L 和 R 两个部件从属的，但是看起来独立，鼠标划上去也能显示信息，
                    //     但没有怪兽立绘、等级、攻击力防御力之类现在多出来的」）：
                    //
                    //   ① already 要能悬停（鼠标划上去看得到它**自己**的效果）。
                    //      `still_unclickable`（Overlay 的原生档）会关 MeshCollider 且
                    //      `ES_mouse_check()` 对它直接 return false ⇒ 永远指不上。
                    //   ② 不能带「场上表侧怪兽」那套装饰。`verticle_clickable`
                    //      （= get_point_worldcondition 对 MonsterZone+FaceUp 给的档，本体的档）
                    //      会 `refreshFunctions.Add(card_verticle_drawing_handler +
                    //      monster_cloude_handler)` 并加载 verticle_number / verticle_Star
                    //      ⇒ 竖立绘 + 怪兽云 + 星级 + ATK/DEF —— 那正是**多出来的**那几样。
                    //   ③ 保持「从属」观感：不要竖立。
                    //
                    //   `floating_clickable` 三条全中（见 gameCard.UA_give_condition 的头一个分支）：
                    //   开 MeshCollider、`destroy(monster_cloude / verticle_drawing /
                    //   verticle_Star / verticle_number)` 且只挂 `card_floating_text_handler`
                    //   （对场上卡 loc 恒为 "" ⇒ `set_text("")`，卡面上一个数字都不留）。
                    //   ⚠ 曾经给成 `verticle_clickable`（想「与本体同口径」）—— 那是错的：
                    //     本体是**真正的场上怪兽**、要那套展示；L/R 是**部件**、不要。
                    //   点击/确认仍然关着：gameCard.RefreshFunction_ES 里对部件早退，不发点击。
                    //
                    //   ⛔ 还要多认一段**同源**的：正在被极大召唤、**还没**收成素材的那两三帧
                    //      （`isRdMaximumPiecePending`）。`MoveToField` 与 `Overlay` 之间隔着
                    //      0.3~0.4 秒，那一段 L/R 的 location 只有 MonsterZone ⇒ 只认
                    //      `isRdMaximumPiece()` 的话它们会按「场上表侧怪兽」渲染一次：
                    //      两张卡带着竖立绘/怪兽云/等级/星级、卡面写着 800·500 亮相，随后才
                    //      变成部件 —— 用户 2026-09-22 的原话「被作为部件的两卡会像是给
                    //      召唤者确认一样」正是这一下。判据与理由见该方法。
                    cards[i].UA_give_condition(
                        cards[i].isRdMaximumPiece() || isRdMaximumPiecePending(cards[i])
                        ? gameCardCondition.floating_clickable
                        : get_point_worldcondition(cards[i].p));
                    // 极大怪兽的 L/R 部件走自己的格位（本体左右），其余照旧 —— 判定与理由
                    // 见 maximumPieceWorldPosition。三件齐了之后再套一层「贴紧 + 放大」
                    // （大框口径；目标点的唯一出口是 rdMaximumWantPosition，
                    // 探针报的 want= 与落稳判据都走它）。
                    cards[i].UA_give_position(
                        rdMaximumWantPosition(cards[i], rdMaxTrioMe, rdMaxTrioOp));
                    applyRdMaximumTrioScale(cards[i], rdMaxTrioMe, rdMaxTrioOp);
                    cards[i].UA_give_rotation(get_world_rotation(cards[i]));
                    // 🔑 正在极限召唤、还没收成素材的部件（pending）**直接落位、不飞**：
                    //    普通 Move 走 rush=false 的 TweenTo，手卡→场的补间会横穿半张屏幕
                    //    —— 两张部件并排飞就是用户 2026-09-22 截图里「悬浮在场地中间的
                    //    两张卡」的另一半来源；「直接成为极大怪兽、不播放部件展示」就要
                    //    这一帧直接出现在本体两侧。素材收完（Overlay 之后）走原 tweens。
                    //    snap 同时 clearITWeen ⇒ 顺手把任何残存的 confirm 补间也清干净
                    //    （见 ConfirmCards 里 rdMaxSkipShow 那段注释的「卡停半路」病根）。
                    // 🔑 2026-09-25 第 5 次报告：**同一条 `rush` 还要罩住「刚离场的部件」** ——
                    //    本体一进墓，core 同帧把三张全指向墓地列，`rush=0` 时三张各飞一段
                    //    ⇒ 那 170ms 里 L/R 停在自己的格位、中间空一格 = 用户说的「散开」，
                    //    随后才慢吞吞飞向墓地 = 「部件滞留 + 从场上飞走」。判据与物证见
                    //    rdMaxPieceGoneOffField。
                    cards[i].UA_flush_all_gived_witn_lock(
                        rush || isRdMaximumPiecePending(cards[i])
                             || rdMaxPieceGoneOffField(cards[i]));
                }

        logMaximumProbe();

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
        // 俯视角下「回到最近的取景」的等价动作：把「拉镜头」的平移量设回**自动值** ——
        // 有卡摊开等确认（`someCardIsShowed`）⇒ **一口气弹到底**（最外一行底缘 + 空位）；
        // 平时 ⇒ 默认机位（`topDownPanRestZ`，最底下）。这就是用户 2026-09-23 第 3 轮说的
        // 「查看卡牌时会像斜视角一样…自动计算可以扩充的距离并**弹过去**」——
        // 与上面那句 `cameraPosition.z = camera_min`（60°：把相机贴到最前那一张卡）是**同一个动作**，
        // 只是俯视角没有「相机站位」这一维，等价物成了「取景中心沿桌面深耕滑过去」。
        // ⚠ 第 5 轮起另有 `preFrameFunction` 的**每帧跟随**兜底 —— 实机里有的摊开路径不走
        //   toNearest / 调用时机早于标志翻转 ⇒ 只在这里弹一次会「没生效」，那条兜住它。
        Program.topDownPanAuto();
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

    /// <summary>
    /// 这张卡名下挂着几件素材 —— <see cref="GCS_cardGetOverlayElements"/> 的**不分配**版：
    /// 只数数、不 new List。给 `card_verticle_drawing_handler` 的每帧脏检查用
    /// （极大状态判据见 gameCard.loaded_verticalmaxstate；那边有完整说明）。
    /// 口径与上面逐字相同：非素材卡、同控制者、同 location(除 Overlay 位)、同 sequence。
    /// </summary>
    public int GCS_cardGetOverlayCount(gameCard c)
    {
        int n = 0;
        if (c != null)
        {
            if ((c.p.location & (UInt32)CardLocation.Overlay) == 0)
            {
                for (int i = 0; i < cards.Count; i++)
                {
                    if (!cards[i].gameObject.activeInHierarchy)
                    {
                        continue;
                    }
                    if ((cards[i].p.location & (UInt32)CardLocation.Overlay) == 0)
                    {
                        continue;
                    }
                    if (cards[i].p.controller != c.p.controller)
                    {
                        continue;
                    }
                    if ((cards[i].p.location | (UInt32)CardLocation.Overlay) != (c.p.location | (UInt32)CardLocation.Overlay))
                    {
                        continue;
                    }
                    // ⚠ RD 极大部件**不**遵守 OCG 的「素材与父卡同 sequence」口径
                    //   （部件站在本体的左右两列，见 isRdMaximumMaterialOf）。
                    //   少了这一支，这条计数对极大本体**恒为 0** ⇒ gameCard 那条
                    //   「极大状态不显示守备力」的闸门永远不成立（用户 2026-09-24 第 1 条）。
                    if (cards[i].p.sequence == c.p.sequence || isRdMaximumMaterialOf(cards[i], c))
                    {
                        n++;
                    }
                }
            }
        }
        return n;
    }

    List<int> keys = new List<int>();

    public gameCard GCS_cardMove(GPS p1, GPS p2, bool print = true, bool swap = false)
    {

        //from card
        gameCard card_from = GCS_cardGet(p1, true);

        // 🔑 RD 极大「本体离场 → 部件确定性跟随」的准备（2026-09-24 根修 B，物证
        //   log/qt_24572.log 12:31:20.739）。必须在 `card_from.p` 被改写**之前**收资：
        //   此刻它还站在怪兽区、部件还挂在它名下。匹配口径与 OCG 重规整循环同源
        //   （同控制者 + 素材态 + 带怪兽区位），但**不要求 seq 相等**——seq 错位正是
        //   那次事故里重规整匹配不上的原因，这里就是给它兜底的（平时由
        //   rdMaxAlignPieceSequences 每帧对齐，正常走不进这条）。
        List<gameCard> rdMaxFollowPieces = null;
        if (GameModeManager.IsRD && card_from != null
            && (card_from.p.location & (UInt32)CardLocation.MonsterZone) != 0
            && (card_from.p.location & (UInt32)CardLocation.Overlay) == 0
            && isMaximumCard(card_from))
        {
            List<gameCard> follow = new List<gameCard>();
            for (int i = 0; i < cards.Count; i++)
            {
                gameCard m = cards[i];
                if (m == null || m == card_from || !m.gameObject.activeInHierarchy)
                {
                    continue;
                }
                if (!isMaximumCard(m)
                    || (m.p.location & (UInt32)CardLocation.Overlay) == 0
                    || (m.p.location & (UInt32)CardLocation.MonsterZone) == 0
                    || m.p.controller != card_from.p.controller)
                {
                    continue;
                }
                if ((m.p_beforeOverLayed.location & (UInt32)CardLocation.MonsterZone) == 0
                    || m.p_beforeOverLayed.sequence < 1 || m.p_beforeOverLayed.sequence > 3)
                {
                    continue;      // 只认极限召唤放下的那两列部件，别把 OCG 素材卷进来
                }
                follow.Add(m);
            }
            if (follow.Count > 0)
            {
                rdMaxFollowPieces = follow;
            }
        }

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

        // 🔑 RD 极大「本体离场 → 部件确定性跟随」（2026-09-24 根修 B）：
        //   本体从怪兽区落到怪兽区以外（墓地/手牌/卡组/除外都算），把它名下的场上部件
        //   逐字段照 OCG 重规整循环的写法跟过去（新 location|Overlay / 新 seq / pos+=1000）
        //   —— 与 core 的素材不变量一致（core 对 LOST_OVERLAY 素材的处置恒为
        //   「父亲新区号|Overlay」，随后才摘去墓地，见 log/qt_24572.log 13.487→13.827）。
        //   收下之后同一帧的 realize 摆位循环读到「不在场上」那一支 ⇒ 部件当场飞向本体去的区；
        //   core 紧跟着发的「素材摘到墓地」MOVE（from=0x90/0/0）也还能对得上，不会另建对象。
        //   ⛔ 重规整已经跟过的（seq 恰好对上的那一族）跳过，避免 pos 被加两次。
        //   ⛔ 本体在怪兽区内挪动不触发（那种走 MZ→MZ 的换位，另有路径管）。
        //   ⛔⛔ 2026-09-25 晚九（用户第 6 次报告，物证 log/qt_8516.log）：再加一道
        //     「**终点不带 Overlay 位**」闸。点燃判据原来只看「新位置无怪兽区位」，可
        //     「收集」类 MOVE 的终点本来就带 Overlay 位 —— 极限召唤「先收素材、本体
        //     还在手牌」时，core 把场上的部件/单张收进手里那套三件，终点实测就是
        //     **0x82 = Overlay|Hand**（21:01:01.061 `piece-mv … to=0x82/0/1`）。这种
        //     消息的 card_from（被收的那张极大卡）在收之前恰好站在怪兽区、不带
        //     Overlay ⇒ fill 一定会收罗同侧**别的**在场素材 ⇒ 点燃把它们也劫去 0x82
        //     ⇒ 客户端与 core 从此错位：core 后续的 `from=0x90/1/0` 对不上 → 凭空造
        //     第二份对象 → 真身滞留 0x82 被下一只本体的对齐循环领养 → 与真 L 叠位，
        //     封闸计数被撑到 2 ⇒ **真 R 件被孤儿兜底误停墓地**（21:01:02.089
        //     `orphan-park id=120283152`）⇒ 三件散伙 + 缩回 1.0 倍 + 墓地字样。
        //     本局实测 3 次点燃**全部** father->0x82（21:00:56.155 / 21:01:01.060 /
        //     21:01:42.784），无一合法。判据：**本体离场的合法终点（墓地 0x10 /
        //     手牌 0x02 / 卡组 0x01 / 除外 0x20 / 额外 0x40）全都不带 Overlay 位；
        //     终点带 Overlay 位只有一种语义 —— 这张卡自己被收成了素材**，它的去向
        //     由它自己的 MOVE + overlay-fix 归一管，跟「跟随」无关。
        if (rdMaxFollowPieces != null
            && (card_from.p.location & (UInt32)CardLocation.MonsterZone) == 0
            && (card_from.p.location & (UInt32)CardLocation.Overlay) == 0)
        {
            for (int i = 0; i < rdMaxFollowPieces.Count; i++)
            {
                gameCard m = rdMaxFollowPieces[i];
                if (overlayed_cards_of_cardFrom.Contains(m))
                {
                    continue;
                }
                uint locOld = m.p.location;
                uint seqOld = m.p.sequence;
                m.p.controller = card_from.p.controller;
                m.p.location = card_from.p.location | (UInt32)CardLocation.Overlay;
                m.p.sequence = card_from.p.sequence;
                m.p.position += 1000;
                QuickTestTrace.Log("max", "maxleft-follow id=" + m.get_data().Id
                    + " loc=0x" + locOld.ToString("X") + "->0x" + m.p.location.ToString("X")
                    + " seq=" + seqOld + "->" + m.p.sequence
                    + " father->0x" + card_from.p.location.ToString("X"));
            }
        }

        // 🔑 RD 极限召唤的**签名**：极大卡从**手牌**直接落到怪兽区中区左右列
        //   （`RDMaximum.lua` 的 `MoveToField(left, 0x2)` / `MoveToField(right, 0x8)`）。
        //   命中就记下「这一侧在等本体」—— 先到的部件会被 rdMaxApplyStageHold 按住，
        //   等本体那条 MOVE 进来的**同一帧**三张一起放行（见 rdMaxStageMask 那段说明）。
        //   ⚠ 判据必须带「来自手牌」：被效果从墓地/卡组复活的极大怪也满足
        //     「极大卡 + 中区左右列 + 没有本体」，那种情况**不许**暂存（按住了它会凭空消失一下）。
        //   🔑 再带「这张卡**不是**本侧最近被宣告召唤的那张」（rdMaxSummonDeclared）：
        //     core 的召唤流程恒为「先宣告、后移动」，宣告码 = 被召唤卡自己的码；
        //     只有极大召唤例外 —— SP_SUMMONING 宣告的是**本体**，L/R 是 operation 里
        //     MoveToField 拉下去的、没有自己的宣告。于是「宣告码 ≠ 自己」⇔「极大召唤
        //     顺带放下的部件」；单独召唤/特招部件时宣告码就是部件自己 ⇒ 闸门放行，
        //     不再把用户 2026-09-24 晚报的「非极大召唤进场也消失」那张卡按住 0.7s。
        //   🔑 再带「表侧」（FaceUp）：极大召唤与通常召唤都是 FaceUpAttack，盖放不走这条。
        if (GameModeManager.IsRD && card_from != null && isMaximumCard(card_from)
            && (p1.location & (UInt32)CardLocation.Hand) != 0
            && (p2.location & (UInt32)CardLocation.MonsterZone) != 0
            && (p2.sequence == 1 || p2.sequence == 3)
            && (p2.position & (UInt32)CardPosition.FaceUp) != 0)
        {
            int decl = rdMaxSummonDeclared[(int)card_from.p.controller];
            if (QuickTestTrace.Enabled)
            {
                // 诊断（仅 qt_debug.on）：签名闸的每一次「几何/侧别」命中与最终放行与否 ——
                // decl= 才是裁决关键（2026-09-24 晚 SINGLE 局：hit=1 且 decl=0 ⇒ 锚没记上，
                // 而不是闸的几何判据误咬）。
                QuickTestTrace.Log("max", "gate id=" + card_from.get_data().Id
                    + " seq=" + p2.sequence + " decl=" + decl
                    + " hit=" + (card_from.get_data().Id != decl ? 1 : 0));
            }
            if (card_from.get_data().Id != decl)
            {
                rdMaxStageBegin(card_from.p.controller);
            }
        }

        // 🔑 RD 极大部件的「位置归一」——**唯一入口**，就在 card_from.p 定下来之后、
        //   `arrangeCards()` 之前（`arrangeCards` 里就有一处按 location 分堆的展示行排布，
        //   晚一步改就赶不上这一帧）。理由与三个症状的对应见 normalizeRdMaximumPiece。
        //   ⚠ 这一段对上面所有分支都有效：`card_from.p = p2` / `GCS_cardRelocate` /
        //     `p2` 带 Overlay 位的 `position += 500` 那支，收尾都落到这一行。
        //   ⚠ 必须把**这条 MOVE 的起点 `p1`** 一起带进去：判据里要用它区分「把场上那只
        //     收成素材」（起点 = 怪兽区）和「素材跟着本体离场」（起点带 Overlay）——
        //     只看终点会把本体离场那条路也改成「还在场上」，就是用户报的那个 bug。
        normalizeRdMaximumPiece(card_from, p1);

        // 诊断（仅 qt_debug.on）：极大卡每条 MOVE 的 from/to/收下后的状态，外加同一张卡在
        // 客户端**有几个对象**（`obj=2` = GCS_cardGet 没对上、另建了一个 ⇒ 另一个留在原地）。
        logRdMaximumMove(card_from, p1, p2);

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
        // 「拉镜头」的平移量设回**默认机位**：进一局就是一次新取景（默认机位 = 最底下，
        // 见 `Program.topDownPanRestZ`）。不复位的话，上一局结束时滚到的位置会带到
        // 这一局的第一个画面里。
        Program.topDownPanZ = Program.topDownPanRestZ;
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
        // 🔑 「极大怪兽一体化」：对局收尾 / 换备 / 撤回重开都走这里 ⇒ 先把三件的表现层位移
        //   收干净（回各自 accurate_position 一次），否则残留的放大位移会带进下一局。
        //   非 RD / 没在放大时内部是空操作。
        rdMaxIntegratedReset();
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
        // 「确认完毕」= 这一次确认结束 ⇒ 把「拉镜头」的平移量设回**默认机位**（最底下），
        // 回到默认取景。
        // ⛔ 必须排在 `toNearest()` **之后**：`toNearest` 里那句 `Program.topDownPanAuto()` 读的是
        //    `topDownRowOuter`，而它由实时消息循环累计、点下按钮的**本帧**还没收摊
        //    ⇒ toNearest 有可能把取景又弹回摊开位置。这一行是「确认结束」这个**事实**的落点，
        //    不依赖那个字段的更新时机。
        // ⛔ 为什么必须复位：滚轮量程随即缩回一个点（默认态自动值 == 默认机位），
        //    收摊之后玩家**没有任何别的手段**把这笔平移挪回来 —— 留着就是一个挪不回去的取景。
        //    （`Ocgcore.preFrameFunction` 的每帧跟随下一帧也会兜到，这里同帧先落定。）
        Program.topDownPanZ = Program.topDownPanRestZ;
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
        if (QuickTestTrace.Enabled && target.isRdMaximumCard())
        {
            // 排查用：极大怪兽被「拉到屏中央展示」的第一现场（弹回动画的头号嫌疑）。
            QuickTestTrace.Log("maxmv", "confirm-pull id=" + target.get_data().Id
                + " loc=0x" + target.p.location.ToString("X") + " seq=" + target.p.sequence
                + " ctrl=" + target.p.controller);
        }
        Program.I().cardDescription.setData(target.get_data(), target.p.controller == 0 ? GameTextureManager.myBack : GameTextureManager.opBack, target.tails.managedString);
        target.animation_confirm_screenCenter(new Vector3(Program.tableauAngle(-30f), 0, 0), 0.2f, 0.5f);
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
