using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 撤回的「先演示、再确认」预览模式。
///
/// 为什么要有它
/// ────────────
/// 真撤回（<see cref="DuelUndo"/>）每次都要「同种子重开 AI.Server + 后台追赶」，按一次等一次；
/// 想多退几步就得连续撤回多次 = 连续重开多次服务器。本类把「选落点」从「执行撤回」里拆出来：
///
///   1. 按撤回（或 Ctrl+Z）先进入预览 —— 摘一份时间线快照，把盘面**纯客户端**地退到最后一个
///      人工决策点（<see cref="Ocgcore.previewReplayInPlace"/> 的原地重放，不碰服务器，瞬时）；
///   2. 右侧按钮组的「上一步 / 下一步」在快照的各个人工决策点之间移动，玩家先看清会退到哪；
///   3. 「确认回溯」才走真撤回 —— <see cref="DuelUndo.ConfirmFromPreview"/>：重开服务器 + 追赶，
///      但**跳过本地重建**（预览最后一步已经把画面画到目标点了）。多步撤回只重开一次服务器；
///   4. 「取消回溯」把模型原地重放回预览入口那一刻，积压的入站包恢复处理，对局无缝继续。
///
/// 按钮 UI 全在右侧 gameInfo 哈希按钮组（见 Ocgcore.undoButtonTick）：入口键「撤回」
/// 与 结束回合/战斗阶段 同款（且只在有人工决策可撤回时出现）；进预览后原地变
/// 取消回溯/上一步/下一步/确认回溯 四键。预览期间 结束回合/进入战斗 这类阶段操作
/// 按钮随 clearResponse 的 removeAll 收掉且不会回来（落位重放不含请求，practicalize
/// 不会挂它们）；确认完毕/卡组记牌 照常可用（用户口径 2026-09-26，见 realize /
/// syncDeckMemoButton——落位末尾的 realize 每步都按当前局面重新同步它们）。
///
/// 不变量（确认/取消的换算全靠它们）
/// ───────────────────────────────
///   ・预览期间 <see cref="Ocgcore.sibyl"/> 在循环开头直接 break：新到的入站包**只积压、
///     不处理、不进时间线** —— 于是 DuelTimeline 停在预览入口快照处，
///     decisions[k].inboundCount 的换算（<see cref="Land"/>）始终成立；
///   ・预览期间 <see cref="Ocgcore.sendReturn"/> 被闸门挡住：玩家对着「过去」点出的应答一律丢弃；
///   ・落点换算与真撤回同一口径：rebuildUpTo = decisions[keep].inboundCount - 2，
///     即「触发那次应答的请求消息**还没被回答**」的时刻 —— 预览看到的就是确认后的落点。
///
/// 与 ReadingSteiner（left_/right_ 世界线回看）的关系
/// ─────────────────────────────────────────────────
/// 机制同源（都是原地按消息流重放：inSkiping 压动画、只对最后一条 practicalize），
/// 但数据源不同：回看重放**实时** Packages_ALL，预览重放**快照**（snap.MakePackage，
/// 不碰实时包历史）。预览期间 left_/right_/rush_ 等直接拒绝，避免两套重放互相踩。
/// </summary>
public static class DuelUndoPreview
{
    /// <summary>预览会话是否进行中（Enter 到 Confirm/Cancel 之间）。</summary>
    public static bool active = false;

    /// <summary>预览入口摘下的时间线快照（独立于实时列表，确认时直接交给撤回器）。</summary>
    private static DuelTimeline.Snapshot snap = null;

    /// <summary>快照里全部人工决策点的下标（升序）。撤回的合法落点集合。</summary>
    private static readonly List<int> manualIdx = new List<int>();

    /// <summary>当前游标（指向 manualIdx）。入口 = 最后一个人工决策点（与旧撤回一步到位相同）。</summary>
    private static int cursor = -1;

    /// <summary>是否还能「上一步」（退到更早的人工决策点）。不可用时按钮组隐藏该键。</summary>
    public static bool CanStepBack
    {
        get { return active && cursor > 0; }
    }

    /// <summary>是否还能「下一步」（回到更晚的人工决策点）。不可用时按钮组隐藏该键。</summary>
    public static bool CanStepForward
    {
        get { return active && cursor < manualIdx.Count - 1; }
    }

    // ── 入口 ────────────────────────────────────────────────────────────────────

    /// <summary>
    /// 撤回按钮 / Ctrl+Z 的入口。前置校验全部沿用真撤回的口径（不满足时给同样的提示）；
    /// 已在预览中再按一次 = 再退一步（延续旧「连按撤回 = 多退一格」的手感）。
    /// </summary>
    public static void Enter()
    {
        if (active)
        {
            StepBack();
            return;
        }
        if (Program.I() == null || Program.I().ocgcore == null)
        {
            return;
        }
        if (!DuelUndo.IsUndoableDuel)
        {
            DuelUndo.Say("撤回只在人机对局里可用。");
            QuickTestTrace.Log("undo", "preview rejected: not ai duel");
            return;
        }
        if (DuelUndo.active)
        {
            DuelUndo.Say("正在回溯中…");
            QuickTestTrace.Log("undo", "preview rejected: undo session in progress");
            return;
        }
        int last = DuelTimeline.LastManualIndex();
        if (last < 0)
        {
            DuelUndo.Say("还没有可以撤回的选择。");
            QuickTestTrace.Log("undo", "preview rejected: no manual decision");
            return;
        }
        DuelTimeline.Snapshot s = DuelTimeline.Take();
        if (s.seed == 0)
        {
            DuelUndo.Say("这一局没有记下种子，无法重建。");
            QuickTestTrace.Log("undo", "preview rejected: seed=0");
            return;
        }
        snap = s;
        manualIdx.Clear();
        for (int i = 0; i < s.decisions.Count; i++)
        {
            if (s.decisions[i].manual)
            {
                manualIdx.Add(i);
            }
        }
        cursor = manualIdx.Count - 1;
        active = true;
        QuickTestTrace.Log("undo", "preview enter decisions=" + s.decisions.Count
            + " manual=" + manualIdx.Count + " inbound=" + s.inbound.Count);
        Land();
    }

    // ── 步进 ────────────────────────────────────────────────────────────────────

    /// <summary>上一步：退到更早的人工决策点（已在最早点则不动）。</summary>
    public static void StepBack()
    {
        if (!active)
        {
            return;
        }
        if (cursor <= 0)
        {
            QuickTestTrace.Log("undo", "preview step blocked: already at first");
            Say("已经在最早的撤回点了。");
            return;
        }
        cursor--;
        Land();
    }

    /// <summary>下一步：回到更晚的人工决策点（上限 = 预览入口那一刻，绝不越过「现在」）。</summary>
    public static void StepForward()
    {
        if (!active)
        {
            return;
        }
        if (cursor >= manualIdx.Count - 1)
        {
            QuickTestTrace.Log("undo", "preview step blocked: already at entry point");
            Say("这已经是当前进度了。");
            return;
        }
        cursor++;
        Land();
    }

    /// <summary>当前目标对应的真撤回参数（keep = decisions 下标，upTo = 本地重建的入站流边界）。</summary>
    private static void CurrentTarget(out int keep, out int upTo)
    {
        keep = manualIdx[cursor];
        int n = snap.decisions[keep].inboundCount;
        // 与 DuelUndo.BeginSession 同一换算：退到「触发那次应答的请求还没被回答」的时刻。
        upTo = Math.Max(-1, n - 2);
    }

    /// <summary>
    /// 把盘面落到当前目标点：原地重放（<see cref="Ocgcore.previewReplayInPlace"/>），
    /// 不销毁任何场景对象 —— 与世界线回看同一条性能路径，步进瞬时、无清场闪烁。
    /// 落位不含触发请求（upTo = inboundCount-2），阶段操作按钮随 clearResponse 收掉；
    /// 末尾 realize 会把 确认完毕/卡组记牌 按当前局面重新挂回（预览里照常可用）。
    /// </summary>
    private static void Land()
    {
        if (snap == null)
        {
            TearDown();
            return;
        }
        int keep;
        int upTo;
        CurrentTarget(out keep, out upTo);
        int ms = Program.TimePassed();
        try
        {
            Program.I().ocgcore.previewReplayInPlace(snap, upTo);
        }
        catch (Exception e)
        {
            QuickTestTrace.Log("undo", "preview land FAILED: " + e.Message);
        }
        QuickTestTrace.Log("undo", "preview step cursor=" + (cursor + 1) + "/" + manualIdx.Count
            + " keep=" + keep + " upTo=" + upTo
            + " took=" + (Program.TimePassed() - ms) + "ms");
    }

    // ── 确认 / 取消 ─────────────────────────────────────────────────────────────

    /// <summary>
    /// 确认回溯：把目标交给真撤回器（重开服务器 + 追赶，跳过本地重建 —— 画面已经是目标态）。
    /// </summary>
    public static void Confirm()
    {
        if (!active || snap == null)
        {
            return;
        }
        int keep;
        int upTo;
        CurrentTarget(out keep, out upTo);
        DuelTimeline.Snapshot s = snap;
        TearDown();
        QuickTestTrace.Log("undo", "preview confirm keep=" + keep + " upTo=" + upTo);

        // 包历史铺成「撤回点之前的录制前缀」：世界线回看（on_left）在新线上仍能从开局重放；
        // 待处理队列里的旧服务器残留一律丢弃 —— 重开的服务器会把这段流原样重发（追赶期校验）。
        List<Package> prefix = new List<Package>();
        for (int i = 0; i <= upTo; i++)
        {
            prefix.Add(s.MakePackage(i));
        }
        Program.I().ocgcore.ResetPackagesForUndoConfirm(prefix);
        DuelUndo.ConfirmFromPreview(s, keep, upTo);
    }

    /// <summary>
    /// 取消回溯：把模型原地重放回「预览入口那一刻」，对局无缝继续。
    ///
    /// ⛔ 必须重放而不是「什么都不做」：预览把模型搬回了过去。重放最后一条是
    /// 入口时的请求消息，practicalize 会把选择按钮与右侧阶段按钮一并带回来；
    /// Packages/Packages_ALL 预览期间从未被动过，sibyl 恢复后照常处理积压包。
    /// </summary>
    public static void Cancel()
    {
        if (!active || snap == null)
        {
            return;
        }
        DuelTimeline.Snapshot s = snap;
        TearDown();
        QuickTestTrace.Log("undo", "preview cancel inbound=" + s.inbound.Count);
        try
        {
            // keepResponse = true：末条就是待应答的请求消息，practicalize 挂出的应答 UI
            // 就是玩家进预览前正在看的那个 —— 收尾绝不能 clearResponse（擦了=卡死，
            // 那条请求不在积压队列里，没有任何东西会把它挂回来）。
            Program.I().ocgcore.previewReplayInPlace(s, s.inbound.Count - 1, true);
        }
        catch (Exception e)
        {
            QuickTestTrace.Log("undo", "preview cancel FAILED: " + e.Message);
        }
        Say("已取消回溯，对局继续。");
    }

    /// <summary>
    /// 对局被整场清掉（收尾 / 换备 / 退出 / 判负）时，若预览还挂着就静默拆除 ——
    /// 只拆状态，不重建盘面（盘面马上也要被清掉/重建了）。
    /// 由 <see cref="Ocgcore.hide"/> 调用。
    /// </summary>
    public static void OnDuelHidden()
    {
        if (!active)
        {
            return;
        }
        TearDown();
        QuickTestTrace.Log("undo", "preview force-end (duel hidden)");
    }

    /// <summary>拆状态（按钮组由 Ocgcore.undoButtonTick 按状态自愈，无需在此处理）。</summary>
    private static void TearDown()
    {
        active = false;
        snap = null;
        manualIdx.Clear();
        cursor = -1;
    }

    private static void Say(string text)
    {
        try
        {
            if (Program.I().ocgcore != null)
            {
                Program.I().ocgcore.RMSshow_none(InterString.Get(text));
            }
        }
        catch (Exception)
        {
        }
    }
}
