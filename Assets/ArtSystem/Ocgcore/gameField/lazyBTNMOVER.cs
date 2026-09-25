﻿using UnityEngine;
using System.Collections;

/// <summary>
/// 阶段条那六个标签的摆位器。g1..g6 = prefab 里的 dp_ / sp_ / mp1_ / bp_ / mp2_ / ep_。
///
/// 三个档：
///   · <see cref="shift"/>(true)  —— OCG MR4/5：六格分两组靠两端，**中间空出额外怪兽区那一段**
///     （底板也不画，见 gameField.loadNewField）；文案用 M1 / M2。
///   · <see cref="shift"/>(false) —— OCG MR≤3：六格等距铺满，配原生六格底板；文案 MP1 / MP2。
///   · <see cref="shiftRD"/>      —— **RD：只有四个阶段**（抽卡 / 主要阶段1 / 战斗 / 结束）。
///     准备阶段与主要阶段2 在 RD 里不存在（core 不发那两个 NewPhase）⇒ 整块收起来
///     （SetActive(false)，不是变灰），四个标签按四格底板等距摆。
///
/// ⚠ 六格那两档**不许丢掉「把 g2/g5 打开」这一步**：盘面会跨局复用同一个 GameField
///   （撤回重开、模式切换），一旦有一档把它们关掉却没人打开，下一局就永远缺点东西。
/// </summary>
public class lazyBTNMOVER : MonoBehaviour {

    public UILabel g1;      // dp_
    public UILabel g2;      // sp_
    public UILabel g3;      // mp1_
    public UILabel g4;      // bp_
    public UILabel g5;      // mp2_
    public UILabel g6;      // ep_

    /// <summary>
    /// RD 档：四格等距（x = ∓237 / ∓79），SP 与 MP2 收起来。
    ///
    /// 那四个 x 不是拍出来的：`devtools/make_rd_phase.py` 把四格底板画在图上
    /// 格心 93 / 372 / 651 / 930（间距 279、关于图心对称），再按「贴图显示宽 580 / 图宽 1024」
    /// 换算 ⇒ ±237.0 / ±79.0。**贴图与这一组 x 是一对**，改一个必须改另一个
    /// （同 `get_point_worldposition_rd` 与 `newfield_rd.png` 的关系）。
    /// </summary>
    public void shiftRD()
    {
        g1.width = 84;
        g2.width = 84;
        g3.width = 84;
        g4.width = 84;
        g5.width = 84;
        g6.width = 84;
        g1.transform.localPosition = new Vector3(-237.0f, 1, 0);
        g3.transform.localPosition = new Vector3(-79.0f, 1, 0);
        g4.transform.localPosition = new Vector3(79.0f, 1, 0);
        g6.transform.localPosition = new Vector3(237.0f, 1, 0);
        g1.text = "DP";
        // RD 只有一个主要阶段 ⇒ 缩写不带 1（用户 2026-09-21：「主要阶段不要叫主要阶段1」；
        // 这里是阶段条上的缩写，hint 那行在 Ocgcore.ES_phaseString、大字贴图是 mp1_rd.png）。
        g3.text = "MP";
        g4.text = "BP";
        g6.text = "EP";
        // RD 没有准备阶段 / 主要阶段2 —— **整块收起来**（六格底板也换成了四格那张，
        // 见 gameField.applyPhaseBarLayout）。留灰字不算「删除」：条上还是一格东西。
        g2.gameObject.SetActive(false);
        g5.gameObject.SetActive(false);
    }

    public void shift(bool ifnew)
    {
        // 退回 OCG 两档时先把被 RD 收起来的两个放回来
        //（撤回重开 / 切模式都会复用同一个 GameField，不还就等于永久少两个阶段）。
        g2.gameObject.SetActive(true);
        g5.gameObject.SetActive(true);
        if (ifnew)
        {
            g1.width = 50;
            g2.width = 50;
            g3.width = 50;
            g4.width = 50;
            g5.width = 50;
            g6.width = 50;
            g1.transform.localPosition = new Vector3(-258f, 1, 0);
            g2.transform.localPosition = new Vector3(-202.2f, 1, 0);
            g3.transform.localPosition = new Vector3(-37.6f, 1, 0);
            g4.transform.localPosition = new Vector3(19.6f, 1, 0);
            g5.transform.localPosition = new Vector3(189.2f, 1, 0);
            g6.transform.localPosition = new Vector3(243f, 1, 0);
            g3.text = "M1";
            g5.text = "M2";
        }
        else
        {
            g1.width = 84;
            g2.width = 84;
            g3.width = 84;
            g4.width = 84;
            g5.width = 84;
            g6.width = 84;
            g1.transform.localPosition = new Vector3(-238, 1, 0);
            g2.transform.localPosition = new Vector3(-140.2f, 1, 0);
            g3.transform.localPosition = new Vector3(-47.5f, 1, 0);
            g4.transform.localPosition = new Vector3(47.5f, 1, 0);
            g5.transform.localPosition = new Vector3(142.5f, 1, 0);
            g6.transform.localPosition = new Vector3(237.8f, 1, 0);
            g3.text = "MP1";
            g5.text = "MP2";
        }
    }
}
