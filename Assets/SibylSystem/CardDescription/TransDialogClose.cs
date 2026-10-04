using System;
using UnityEngine;

/// <summary>
/// 翻译界面（「译」菜单开出来的那几个框）的**右键关闭**（用户 2026-10-04 第五条）。
/// 挂在那张对话框**窗口根**上：右键 = 关掉它 —— 与右上角那颗圆形 × 同一件事：
/// **什么都不提交**地退出来（对改名输入框来说就是"这次不改了"）。
///
/// <para>为什么不在 <c>Servant.ES_mouseDownRight</c> 里做：开这些框的是
/// <see cref="NameTranslationUI"/> 的<b>单例</b>，它既没有被注册进 <c>Program.servants</c>、
/// 也从不 <c>show()</c> ⇒ <c>Servant.Update</c> 那条派发路根本到不了它
/// （同一类坑见 notes/harness.md 与 ngui-button-conventions §1b）。
/// 这里跟 <c>CardLinkHover</c> / <c>hinterEdge</c> 一样走「挂在对象上的 MonoBehaviour」
/// 这一路：窗口在，本组件在；窗口一销毁，组件跟着走 —— 不可能误关别的框。</para>
/// </summary>
public class TransDialogClose : MonoBehaviour
{
    /// <summary>要干的事（＝把这张对话框收掉且不提交结果）。装配方（NameTranslationUI）负责给。</summary>
    public Action onClose;

    /// <summary>
    /// 这个框是**哪一帧**开出来的。
    /// ⛔⛔ 没有这道闸会真机翻车：右键简介里亮起的字段 ⇒ **开这一击本身就是右键**，而
    ///   <c>AddComponent</c> 之后本组件的 <c>Update</c> 有可能在**同一帧**就被跑到 ——
    ///   那样框会刚开就自己关掉（玩家看到"闪一下就没了"）。同帧一律不关。
    /// </summary>
    int armFrame = -1;

    /// <summary>框开出来的那一刻调用（<c>NameTranslationUI.DecorateMsWindow</c> 负责）。</summary>
    public void Arm()
    {
        armFrame = Time.frameCount;
    }

    void Update()
    {
        Tick(Program.InputGetMouseButtonDown_1);
    }

    /// <summary>
    /// <see cref="Update"/> 的实体，判据只有这一份（探针也调它，不另写一遍）。
    /// 返回"这一拍有没有真的关掉"，供验收断言"同帧被闸住 / 下一帧才关得掉"。
    /// </summary>
    public bool Tick(bool rightDown)
    {
        if (Time.frameCount <= armFrame)
        {
            return false;       // 开框那一击本身就是右键，同帧不关
        }
        if (!rightDown)
        {
            return false;
        }
        Action act = onClose;
        if (act == null)
        {
            return false;
        }
        // ⛔ 先把回调摘掉再执行：关框会把这个 GameObject 连带本组件一起销毁，
        //   同一帧里再进来一次就会对着已销毁的对象干活。
        onClose = null;
        try
        {
            act();
        }
        catch (Exception e)
        {
            Program.DEBUGLOG(e);
        }
        return true;
    }

    /// <summary>验收用：距"开框那一帧"过了几帧（&lt;=0 = 还在同帧那道闸里）。</summary>
    public int ProbeArmedAge()
    {
        return Time.frameCount - armFrame;
    }
}
