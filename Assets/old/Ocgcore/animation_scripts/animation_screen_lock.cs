using UnityEngine;
using System.Collections;

public class animation_screen_lock : MonoBehaviour
{
    public Vector3 screen_point = Vector3.zero;
    // Use this for initialization
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {
        gameObject.transform.position = Camera.main.ScreenToWorldPoint(screen_point);
    }
}

public class animation_screen_lock2 : MonoBehaviour 
{
    // Use this for initialization
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {
        // ⚠⚠ 2026-09-30 定案：**俯视不再另放位置**，与正常视角同一个落点（盘面正中）。
        //
        // 这件事来回改过两轮，过程与教训留在 `_报告_RD俯视角适配调研_20260930.md`：
        //   ① 2026-09-28（v3 需求③O1）把它挪到「右侧空列」，理由 = 俯视下盘面正中会压住
        //      「阶段条 + 魔陷区」那一行；
        //   ② 2026-09-30 那次挪**用错了坐标空间** —— 把 2D UI 世界坐标当成盘面世界坐标
        //      （UIRoot 缩放让 2D 相机是 ≈493 像素/世界，3D 盘面相机只有 17.5~19.9）
        //      ⇒ 落点 screen.x≈19989、整条飞出屏外，两档都看不到。改成屏幕坐标后落在右侧空列，
        //      **但用户判它错**（在场地外）。
        // ⇒ 回到原点：俯视与正常视角**同一套**落点。
        //    代价：俯视下它会盖住盘面正中那一行 —— 那正是 ① 当初挪它的原因，
        //    记录在案，**需要时再议，但不再自作主张挪**。
        //
        // ⛔ 缩放仍由 `TableauLayout.PhaseTextScaleFlat`（0.40）在俯视下缩小
        //    （`gameField.animation_show_big_string`）—— 用户这条只说**位置**不对，
        //    尺寸没提，先不动；要改成与正常视角同尺寸就把那个 0.40 去掉。
        transform.position = Program.I().ocgcore.centre();
        PhaseTextAnchorReport.Log(transform);
    }
}

/// <summary>
/// 阶段大字（`MAIN PHASE 1` 那类 **3 秒瞬态**）的屏幕落点自述。
///
/// <para><b>为什么必须有这条</b>：它是过场文字，不盯着根本发现不了落点问题，
/// 而这个落点已经出过两次事（2026-09-28 挪去「右侧空列」、09-30 挪出屏外），
/// 第三次是 09-30 用户判「放在场地外不对」⇒ 位置一律由**游戏自己报**，
/// 判据在脚本侧做（`_verify_topdown_rd.py` G6/G8）。</para>
///
/// <para>⛔⛔ <b>口径警告（09-30 踩过）</b>：阶段大字是 <c>ui_main_2d</c> 下的
/// <b>UI 元素</b>，它的世界坐标活在 <b>2D UI 相机</b>那个空间；而盘面在
/// <b>3D 对局相机</b>空间（UIRoot 缩放让 2D 相机 ≈493 像素/世界，3D 盘面相机只有
/// 17.5~17.9）。<b>两者不是一回事</b>，把 3D 的世界坐标当 2D 的写，会落到
/// <c>screen.x≈19989</c>、整条飞出屏外。<b>本类只做「2D 元素 → 2D 相机」的往返</b>，
/// 不碰盘面坐标。</para>
///
/// <para>输出的 <c>screen=</c> / <c>box=</c> 与同一批 <c>[view] fbox</c>、
/// <c>[view] z-&gt;screenY</c> <b>同一套口径</b>：
/// <c>y = Screen.height − WorldToScreenPoint(...).y</c>（<b>屏幕左下原点</b>）
/// ⇒ 脚本侧可以直接和 <c>fbox</c> 比大小。</para>
///
/// <para>⛔ <b>1s 一条，不是每帧</b>：它是 3 秒瞬态，1s 足够捕获；
/// 每帧打会淹没 log / <c>qt_*.log</c>。</para>
/// </summary>
public class PhaseTextAnchorReport
{
    static float lastAt = -99f;

    public static void Log(Transform t)
    {
        Camera cam = Program.camera_main_2d;
        if (t == null || cam == null)
        {
            return;
        }
        float now = Time.time;
        if (now - lastAt < 1f)
        {
            return;
        }
        lastAt = now;

        Vector3 sp = cam.WorldToScreenPoint(t.position);
        System.Text.StringBuilder msg = new System.Text.StringBuilder();
        msg.Append("ptext topDown=").Append(Program.topDownLike ? 1 : 0)
           // ⛔ 键名写 `tiltDeg` 不是 `tilt`：验收脚本用 `last(text,"view","tilt=")` 抓主视角行，
           //    这里若也叫 `tilt=` 就会把 ptext 行当成主视角行（`duel=` 读不到 ⇒ 全档红）。
           .Append(" tiltDeg=").Append(Program.tableauTilt.ToString("F0"))
           .Append(" world=(").Append(t.position.x.ToString("F1")).Append(",")
           .Append(t.position.y.ToString("F1")).Append(",")
           .Append(t.position.z.ToString("F1")).Append(")")
           .Append(" screen=(").Append(Mathf.RoundToInt(sp.x)).Append(",")
           .Append(Mathf.RoundToInt(Screen.height - sp.y)).Append(")");

        // 整张字的屏幕包围盒：脚本要判「两档屏占比一不一样」（用户 2026-09-30：
        // 「阶段大字太小了，俯视角下也要有正常视角一样大的占比」），光给一个点不够。
        //
        // ⛔⛔ **不能用 `Renderer.bounds`**（两次踩坑）：
        //   ① `GetComponent<Renderer>()` 在根上恒为 null —— UITexture 挂在**子物体**上
        //      （`animation_show_big_string` 自己用的是 `GetComponentInChildren<UITexture>()`）；
        //   ② 改 `GetComponentInChildren<Renderer>()` 仍然 null —— `Program.create()` 只做
        //      `Instantiate` + 设 `localScale`，**不给它加 Renderer**；NGUI widget 走
        //      UIPanel 的 draw call，本来就不需要 Renderer。
        // ⇒ 用 NGUI widget 自己的 `worldCorners`（`viewToggleButton.cs` 量控件屏占
        //    用的就是这一套，同一个 `UIPanel` 口径）。
        UIWidget w = t.GetComponentInChildren<UIWidget>();
        if (w != null)
        {
            Vector3[] wc = w.worldCorners;
            float x0 = float.MaxValue, y0 = float.MaxValue;
            float x1 = float.MinValue, y1 = float.MinValue;
            for (int i = 0; i < wc.Length; i++)
            {
                Vector3 q = cam.WorldToScreenPoint(wc[i]);
                float qy = Screen.height - q.y;
                if (q.x < x0) { x0 = q.x; }
                if (q.x > x1) { x1 = q.x; }
                if (qy < y0) { y0 = qy; }
                if (qy > y1) { y1 = qy; }
            }
            msg.Append(" box=[").Append(Mathf.RoundToInt(x0)).Append(",")
               .Append(Mathf.RoundToInt(y0)).Append(",")
               .Append(Mathf.RoundToInt(x1)).Append(",")
               .Append(Mathf.RoundToInt(y1)).Append("]")
               .Append(" scale=").Append(t.transform.lossyScale.x.ToString("F3"));
        }
        else
        {
            msg.Append(" box=NULL");
        }
        QuickTestTrace.Log("view", msg.ToString());
    }
}
