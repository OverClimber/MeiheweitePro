using UnityEngine;
using System;
using System.Collections.Generic;

public class gameUIbutton
{
    public string hashString;
    public GameObject gameObject;
    public int response;
    public bool dying;
    public bool dead;

    /// <summary>
    /// 排布优先级：越小越靠上（默认 100 = 常规紧凑段）。撤回组用 200+ 钉在列尾。
    /// 排布时按 (order, 插入序) **稳定**排序，所以同 order 的按钮相对顺序 = 挂载顺序，永不乱。
    ///
    /// 已占用档位（2026-09-26 定，数值刻意复刻历史稳定序，见 log 里 [opt] 的实测间距 60px）：
    ///   100 主要阶段 / 战斗阶段（默认值，未显式传）
    ///   101 结束回合    102 洗切手牌
    ///   110 取消选择 / 取消连锁 / 取消操作 / 重新选择 / 重新输入
    ///   111 完成选择
    ///   120 确认完毕    121 卡组记牌    122 转换视角
    ///   200 撤回（入口）  201 上一步  202 下一步  203 取消回溯  204 确认回溯
    /// 新增哈希按钮请续用空档；不传 order 的按钮彼此仍按挂载序排（不会插入到上述各组之间）。
    /// </summary>
    public int order = 100;

    /// <summary>
    /// 占位但不显示：仍吃掉一个档位（rank++），只是 SetActive(false)。
    /// 撤回组专用 —— 某键不可用时「组内留空档、其余键位置纹丝不动」，
    /// 若改成 removeHashedButton（退场销毁）再挂回，就会被追加到列尾、顺序乱掉（用户 2026-09-26 报的 bug）。
    /// </summary>
    public bool slotHold = false;

    /// <summary>gameInfo.removeAll() 时豁免：撤回组（入口 + 四键）用它躲开每次应答的清场。</summary>
    public bool keepOnClear = false;

    /// <summary>
    /// 灰键：**仍显示、仍占档位、仍能被鼠标悬停**，但点击被 listenerForClicked 拦下。
    /// 撤回预览的「上一步 / 下一步」退到头时用这条 —— 用户口径 2026-09-26 第 1 条：
    /// 不可用**不许消失**（消失会让其余键挪位、顺序看起来乱），要原地变灰，
    /// 再用「鼠标移上去在鼠标边上说一句」把不可用的原因讲清楚。
    /// </summary>
    public bool disabled = false;

    /// <summary>灰键悬停时跟随鼠标的小提示文案（已过 InterString.Get）。空 = 不提示。</summary>
    public string disabledHint = "";

    /// <summary>变灰前的原色快照（首次变灰时记下）。复原逐个还原 ——
    /// 反复乘灰度系数只会越乘越黑，所以必须存原值而不是就地改。</summary>
    List<UIWidget> grayWidgets = null;
    List<Color> enabledColors = null;

    /// <summary>
    /// 把按钮整体切成灰（图标 + 文字 + 可能的底图：遍历自身全部 UIWidget）/ 复原。
    /// 只动颜色 —— 可见性、占位、collider（悬停提示要用它）一律不碰。
    /// </summary>
    public void setGray(bool gray)
    {
        if (gameObject == null)
        {
            return;
        }
        if (gray)
        {
            if (grayWidgets != null)
            {
                return;                    // 已经是灰的：别重复快照，否则会把灰当成「原色」记下来
            }
            UIWidget[] ws = gameObject.GetComponentsInChildren<UIWidget>(true);
            grayWidgets = new List<UIWidget>();
            enabledColors = new List<Color>();
            for (int i = 0; i < ws.Length; i++)
            {
                grayWidgets.Add(ws[i]);
                enabledColors.Add(ws[i].color);
                Color c = ws[i].color;
                ws[i].color = new Color(0.45f, 0.45f, 0.47f, c.a);   // 统一灰，保留原透明度
            }
        }
        else
        {
            if (grayWidgets == null)
            {
                return;
            }
            for (int i = 0; i < grayWidgets.Count; i++)
            {
                if (grayWidgets[i] != null)
                {
                    grayWidgets[i].color = enabledColors[i];
                }
            }
            grayWidgets = null;
            enabledColors = null;
        }
    }
}

public class gameInfo : MonoBehaviour
{
    public UITexture instance_btnPan;
    public UITextList instance_lab;
    public UIToggle toggle_ignore;
    public UIToggle toggle_all;
    public UIToggle toggle_smart;
    public GameObject line;

    public void on_toggle_ignore()
    {
        toggle_all.value = false;
        toggle_smart.value = false;
    }

    public void on_toggle_all()
    {
        toggle_ignore.value = false;
        toggle_smart.value = false;
    }

    public void on_toggle_smart()
    {
        toggle_ignore.value = false;
        toggle_all.value = false;
    }

    //public UITextList hinter;

    barPngLoader me;

    barPngLoader opponent;

    public GameObject mod_healthBar;

    public float width = 0;

    int lastTickTime = 0;

    // Use this for initialization
    void Start()
    {
        ini();
    }

    public void ini()
    {
        Update();
    }

    public bool swaped = false;

    // ── 右缘让位（用户 2026-10-02 第二轮）─────────────────────────────────
    // 战斗里「点简介链接弹出的检索窗」占住右缘时，右缘这一列（血条×2 / 按钮列 / 消息列表）
    // 要整体**被顶开**、弹到检索窗左边。检索窗 show/hide 时由 CardSearchWindow 写目标值
    // （= 检索窗宽，0 = 没人占）；这里每帧朝目标缓动 —— Update 本来就每帧重写这些
    // localPosition，所以让位必须做进同一处，否则一帧就被写回原位。
    //
    // ⛔⛔两个字段都**必须 private**：本类已序列化进包（场景/prefab 里的 MonoBehaviour），
    //   包内数据按**旧类布局**写成；加一个 public 字段，运行期反序列化按**新布局**读就会
    //   越过数据末尾 ⇒ 「sharedassets0.assets is corrupted! [Position out of bounds!]」
    //   ⇒ 开机即崩（2026-10-02 实证三连崩，二分定位到本处）。私有字段不进序列化布局，
    //   随便加；对外用 SetRightShift()/GetRightShift() 这对访问器。
    /// <summary>右缘要让开的像素目标（= 占住右缘的检索窗宽；0 = 没人占）。</summary>
    float rightShiftTarget = 0f;

    /// <summary>当前已滑到的让位量（朝 rightShiftTarget 缓动，做出「被顶开」的动感）。</summary>
    float rightShiftCur = 0f;

    /// <summary>检索窗占住右缘时上报让位量（px）；收窗时传 0。幂等。</summary>
    public void SetRightShift(float px)
    {
        rightShiftTarget = px < 0f ? 0f : px;
    }

    /// <summary>验收用：读当前让位目标。</summary>
    public float GetRightShiftTarget()
    {
        return rightShiftTarget;
    }

    void Update()
    {
        if (rightShiftCur != rightShiftTarget)
        {
            float step = (rightShiftTarget - rightShiftCur) * Mathf.Min(1f, Time.deltaTime * 7f);
            rightShiftCur += step;
            if (Mathf.Abs(rightShiftTarget - rightShiftCur) < 0.5f)
            {
                rightShiftCur = rightShiftTarget;
            }
        }
        if (me == null || opponent == null)
        {
            me = ((GameObject)MonoBehaviour.Instantiate(mod_healthBar, new Vector3(1000, 0, 0), Quaternion.identity)).GetComponent<barPngLoader>();
            me.transform.SetParent(gameObject.transform);
            opponent = ((GameObject)MonoBehaviour.Instantiate(mod_healthBar, new Vector3(1000, 0, 0), Quaternion.identity)).GetComponent<barPngLoader>();
            opponent.transform.SetParent(gameObject.transform);
            
            Transform[] Transforms = me.GetComponentsInChildren<Transform>();
            foreach (Transform child in Transforms)
            {
                child.gameObject.layer = gameObject.layer;
            }
            Transforms = opponent.GetComponentsInChildren<Transform>();
            foreach (Transform child in Transforms)
            {
                child.gameObject.layer = gameObject.layer;
            }
            Color c;
            ColorUtility.TryParseHtmlString(Config.Getui("gameChainCheckArea.color"), out c);
            UIHelper.getByName<UISprite>(UIHelper.getByName<UIToggle>(gameObject, "ignore_").gameObject, "Background").color = c;
            UIHelper.getByName<UISprite>(UIHelper.getByName<UIToggle>(gameObject, "watch_").gameObject, "Background").color = c;
            UIHelper.getByName<UISprite>(UIHelper.getByName<UIToggle>(gameObject, "use_").gameObject, "Background").color = c;
        }
        float k = ((float)(Screen.width - Program.I().cardDescription.width)) / 1200f;
        if (k > 1.2f)
        {
            k = 1.2f;
        }
        if (k <0.8f)
        {
            k = 0.8f;
        }
        Vector3 ks = new Vector3(k, k, k);
        float kb = ((float)(Screen.width - Program.I().cardDescription.width)) / 1200f;
        if (kb > 1.2f)
        {
            kb = 1.2f;
        }
        if (kb < 0.73f)
        {
            kb = 0.73f;
        }
        Vector3 ksb = new Vector3(kb, kb, kb);
        instance_btnPan.gameObject.transform.localScale = ksb;
        opponent.transform.localScale = ks;
        me.transform.localScale = ks;
        if (!swaped)
        {
            opponent.transform.localPosition = new Vector3(Screen.width / 2 - 14 - rightShiftCur, Screen.height / 2 - 14);
            me.transform.localPosition = new Vector3(Screen.width / 2 - 14 - rightShiftCur, Screen.height / 2 - 14 - k * (float)(opponent.under.height));
        }
        else
        {
            me.transform.localPosition = new Vector3(Screen.width / 2 - 14 - rightShiftCur, Screen.height / 2 - 14);
            opponent.transform.localPosition = new Vector3(Screen.width / 2 - 14 - rightShiftCur, Screen.height / 2 - 14 - k * (float)(opponent.under.height));
        }

        width = (150 * kb) + 15f;
        float localPositionPanX = (((float)Screen.width - 150 * kb) / 2) - 15f - rightShiftCur;
        instance_btnPan.transform.localPosition = new Vector3(localPositionPanX, 145, 0);
        instance_lab.transform.localPosition = new Vector3(Screen.width / 2 - 315 - rightShiftCur, -Screen.height / 2 + 90, 0);
        // ── 哈希按钮列排布（2026-09-26 重写）───────────────────────────────────
        // 旧实现按「列表出现序号 j」算 y，而 addHashedButton 永远追加到列表末尾 ⇒
        // 不可用键「摘掉再挂回」就被排到列尾，顺序必乱（用户报的撤回四键乱序）。
        // 新实现：① 在场项按 (order, 插入序) **稳定**排序 → 顺序发 rank；
        //        ② slotHold 键吃 rank 但不显示（组内留空档，其余键位置纹丝不动）；
        //        ③ 列高按 rank 算（撤回四格是唯一的固定空档来源 → 不会大空洞、不跳高度）。
        for (int i = 0; i < HashedButtons.Count; i++)
        {
            gameUIbutton b = HashedButtons[i];
            if (b.gameObject == null)
            {
                b.dead = true;
                continue;
            }
            if (b.dying)
            {
                // 退场：缩回列首（-120）后从列表摘除。
                b.gameObject.transform.localPosition += (new Vector3(0, -120, 0) - b.gameObject.transform.localPosition) * Program.deltaTime * 20f;
                if (Math.Abs(b.gameObject.transform.localPosition.y - -120) < 1)
                    b.dead = true;
            }
        }
        // 稳定插入排序（按钮就几颗，不值得上 LINQ）：order 升序，同 order 保持挂载顺序。
        List<gameUIbutton> alive = new List<gameUIbutton>();
        for (int i = 0; i < HashedButtons.Count; i++)
        {
            gameUIbutton b = HashedButtons[i];
            if (b.gameObject == null || b.dying)
            {
                continue;
            }
            int at = alive.Count;
            while (at > 0 && alive[at - 1].order > b.order)
            {
                at--;
            }
            alive.Insert(at, b);
        }
        int rank = 0;
        for (int i = 0; i < alive.Count; i++)
        {
            gameUIbutton b = alive[i];
            if (b.slotHold)
            {
                if (b.gameObject.activeSelf)
                {
                    b.gameObject.SetActive(false);
                }
                rank++;
                continue;
            }
            if (!b.gameObject.activeSelf)
            {
                b.gameObject.SetActive(true);
                // 留槽期间 iTween 的缩放入场可能被暂停（对象被停用 ⇒ 它挂在按钮上的 Update 也停了）
                // ⇒ scale 卡在 0 时补回常态尺寸。卡在中途就让它接着长完，观感反而是自然的入场。
                if (b.gameObject.transform.localScale.x < 0.01f)
                {
                    b.gameObject.transform.localScale = new Vector3(0.9f, 0.9f, 0.9f);
                }
            }
            b.gameObject.transform.localPosition += (new Vector3(0, -145 - rank * 50, 0) - b.gameObject.transform.localPosition) * Program.deltaTime * 10f;
            rank++;
        }
        for (int i = HashedButtons.Count - 1; i >= 0; i--)
        {
            if (HashedButtons[i].dead)
                HashedButtons.RemoveAt(i);
        }
        float height = 132 + 50 * rank;
        if (rank == 0)
        {
            height = 116;
        }
        instance_btnPan.height += (int)(((float)height - (float)instance_btnPan.height) * 0.2f);
        if (Program.TimePassed() - lastTickTime > 1000)
        {
            lastTickTime = Program.TimePassed();
            tick();
        }
        // 末尾：灰键悬停提示（没有灰键时只是一次列表扫描，见 updateGrayTip）
        updateGrayTip();
        // 「俯视角/正常视角」切换钮（方案C 56×56）：落位右缘钉在本面板右缘、中心高 80px。
        // 放这里是因为本类每帧都在跑，而且 `instance_btnPan` 的最终 scale 此刻才定 ——
        // 早一拍读会拿到未缩放的值，落位就偏。
        ViewToggleButton.Tick(instance_btnPan);
    }

    List<gameUIbutton> HashedButtons = new List<gameUIbutton>();

    /// <summary>
    /// 排查用：把当前挂着的哈希按钮暴露出来。
    ///
    /// 游戏里「玩家的可选操作」有两处来源，缺一不可：
    ///   ・挂在卡片上的选项按钮（通常召唤 / 发动效果 / 攻击宣言 …）—— 在 gameCard.buttons 里；
    ///   ・挂在 gameInfo 上的哈希按钮（战斗阶段 / 结束回合 / 洗切手牌 / 完成选择 …）—— 就是这里。
    /// 只枚举前者会漏掉「手牌没有可发动效果时的整个主要阶段」：那时盘面上能点的只有
    /// 结束回合 / 战斗阶段，而它们不在卡片上。验收脚本靠 [opt] 找点位，所以必须一起报。
    /// </summary>
    public List<gameUIbutton> allHashedButtons
    {
        get { return HashedButtons; }
    }

    /// <summary>
    /// 挂一颗右侧哈希按钮，返回它（撤回按钮要拿到 iconSetForButton 覆盖图标、
    /// 以及用 allHashedButtons 之外的途径定位这颗按钮 —— 返回值让调用方不必再翻列表找）。
    /// </summary>
    public gameUIbutton addHashedButton(string hashString_, int hashInt, superButtonType type, string hint)
    {
        return addHashedButton(hashString_, hashInt, type, hint, 100, false, false);
    }

    /// <summary>
    /// 带排布参数的版本（撤回组用）：<paramref name="order"/> 越小越靠上；
    /// <paramref name="slotHold"/> = 挂上即「留槽不显示」；<paramref name="keepOnClear"/> = 免被 removeAll 清掉。
    /// 原 4 参签名保留为转发，所有既有调用点零改动。
    /// </summary>
    public gameUIbutton addHashedButton(string hashString_, int hashInt, superButtonType type, string hint,
        int order, bool slotHold, bool keepOnClear)
    {
        gameUIbutton hashedButton = new gameUIbutton();
        string hashString = hashString_;
        if (hashString == "")
        {
            hashString = hashInt.ToString();
        }
        hashedButton.hashString = hashString;
        hashedButton.response = hashInt;
        hashedButton.order = order;
        hashedButton.slotHold = slotHold;
        hashedButton.keepOnClear = keepOnClear;
        hashedButton.disabled = false;
        hashedButton.disabledHint = "";
        hashedButton.gameObject = Program.I().create(Program.I().new_ui_superButtonTransparent);
        UIHelper.trySetLableText(hashedButton.gameObject, "hint_", hint);
        UIHelper.getRealEventGameObject(hashedButton.gameObject).name = hashString + "----" + hashInt.ToString();
        UIHelper.registUIEventTriggerForClick(hashedButton.gameObject, listenerForClicked);
        hashedButton.gameObject.GetComponent<iconSetForButton>().setTexture(type);
        hashedButton.gameObject.GetComponent<iconSetForButton>().setText(hint);
        Transform[] Transforms = hashedButton.gameObject.GetComponentsInChildren<Transform>();
        foreach (Transform child in Transforms)
        {
            child.gameObject.layer = instance_btnPan.gameObject.layer;
        }
        hashedButton.gameObject.transform.SetParent(instance_btnPan.transform,false);
        hashedButton.gameObject.transform.localScale = Vector3.zero;
        hashedButton.gameObject.transform.localPosition= new Vector3(0, -120, 0);
        hashedButton.gameObject.transform.localEulerAngles = Vector3.zero;
        iTween.ScaleTo(hashedButton.gameObject, new Vector3(0.9f, 0.9f, 0.9f), 0.3f);
        hashedButton.dying = false;
        if (slotHold)
        {
            // 留槽不显示：排布器会把它 SetActive(false)（但仍吃一个档位）。
            hashedButton.gameObject.SetActive(false);
        }
        HashedButtons.Add(hashedButton);
        refreshLine();
        return hashedButton;
    }

    void listenerForClicked(GameObject obj)
    {
        string[] mats = obj.name.Split("----");
        if (mats.Length == 2)
        {
            for (int i = 0; i < HashedButtons.Count; i++)
            {
                if (HashedButtons[i].hashString == mats[0])
                {
                    if (HashedButtons[i].response.ToString() == mats[1])
                    {
                        // 灰键（撤回预览里退到头的 上一步/下一步）：看得见、悬得停，但不派发。
                        // 「不可点」必须在这儿拦 —— collider 不能拆，它还得留着给悬停提示做命中。
                        if (HashedButtons[i].disabled)
                        {
                            QuickTestTrace.Log("btn", "click ignored (disabled) " + mats[0]);
                            return;
                        }
                        Program.I().ocgcore.ES_gameUIbuttonClicked(HashedButtons[i]);
                    }
                }
            }
        }
    }

    public bool queryHashedButton(string hashString)
    {
        for (int i = 0; i < HashedButtons.Count; i++)
        {
            if (HashedButtons[i].hashString == hashString && !HashedButtons[i].dying)
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// 与 queryHashedButton 的差别：**正在退场（dying）的也算存在**。
    /// 撤回按钮组用「每拍自愈同步」维护 —— 如果用 !dying 的口径，刚 removeHashedButton
    /// 的那颗（还在 0.3 秒退场动画里）下一拍就会被当成「不在」再补一颗，同一格叠两颗。
    /// </summary>
    public bool queryHashedButtonAny(string hashString)
    {
        for (int i = 0; i < HashedButtons.Count; i++)
        {
            if (HashedButtons[i].hashString == hashString)
            {
                return true;
            }
        }
        return false;
    }

    public void removeHashedButton(string hashString)
    {
        gameUIbutton remove = null;
        for (int i = 0; i < HashedButtons.Count; i++)
        {
            if (HashedButtons[i].hashString == hashString)
            {
                remove = HashedButtons[i];
            }
        }
        if (remove != null)
        {
            if (remove.gameObject != null)
            {
                Program.I().destroy(remove.gameObject, 0.3f, true);
            }
            remove.dying = true;
        }
        refreshLine();
    }

    /// <summary>
    /// 把一颗哈希按钮切成「留槽但不显示」/ 恢复显示。
    /// 撤回组不可用键（退到头时的「上一步」/「下一步」）走这条 —— 组内留一个空档，
    /// 其余键的 y 完全不动（用户口径 2026-09-26：四键自上而下固定顺序，隐藏不许打乱排序）。
    /// </summary>
    public void setHashedButtonHold(string hashString, bool hold)
    {
        for (int i = 0; i < HashedButtons.Count; i++)
        {
            if (HashedButtons[i].hashString == hashString && !HashedButtons[i].dying)
            {
                HashedButtons[i].slotHold = hold;
            }
        }
        refreshLine();
    }

    /// <summary>
    /// 把一颗哈希按钮切成「灰键」/ 复原（撤回预览里退到头的 上一步 / 下一步 走这条）。
    /// 与 setHashedButtonHold 的区别：**可见性、档位一律不变** —— 灰键仍留在原地显示，
    /// 只是整体变灰、点击被 listenerForClicked 拦下、鼠标悬停时弹提示
    /// （用户口径 2026-09-26 第 1 条：不可用不许消失，位置也不许动）。
    /// </summary>
    public void setHashedButtonDisabled(string hashString, bool disabled, string hint)
    {
        for (int i = 0; i < HashedButtons.Count; i++)
        {
            if (HashedButtons[i].hashString == hashString && !HashedButtons[i].dying)
            {
                HashedButtons[i].disabled = disabled;
                HashedButtons[i].disabledHint = hint;
                HashedButtons[i].setGray(disabled);
            }
        }
    }

    public void removeAll()
    {
        if (HashedButtons.Count == 1)
        {
            if (HashedButtons[0].hashString == "swap")
            {
                return;
            }
        }
        for (int i = 0; i < HashedButtons.Count; i++)
        {
            if (HashedButtons[i].keepOnClear)
            {
                // 撤回组（入口 + 四键）免清：每次应答的 removeAll 都不该把它们销毁重建 ——
                // 那是「撤回按钮顺序乱 / 每步都缩放弹一次」的根因（用户 2026-09-26）。
                continue;
            }
            if (HashedButtons[i].gameObject != null)
            {
                Program.I().destroy(HashedButtons[i].gameObject, 0.3f, true);
            }
            HashedButtons[i].dying = true;
        }
        refreshLine();
    }

    void refreshLine()
    {
        int j = 0;
        for (int i = 0; i < HashedButtons.Count; i++)
        {
            // 留槽键不算「在场」：它不显示，只有它一个时不该把分隔线拉出来。
            if (!HashedButtons[i].dying && !HashedButtons[i].slotHold)
            {
                j++;
            }
        }
        line.SetActive(j > 0);
    }

    // ── 灰键悬停提示：跟随鼠标的小黑底白字 ──────────────────────────────────────
    // 撤回预览里「上一步 / 下一步」退到头时不再消失，而是变灰；「为什么点不动」
    // 由这枚跟随鼠标的小标签回答（用户口径 2026-09-26 第 1 条）。
    // 结构与 DuelUndo 的遮罩同一套路：自建独立 UIPanel（不继承 UIRoot 的 0.0019 缩放，
    // 于是「1 面板单位 = 1 像素」），深度取全场 UIPanel 最大值 + 余量，绝不写死。
    static GameObject grayTipRoot = null;
    static UILabel grayTipLabel = null;
    static UITexture grayTipBg = null;
    static string grayTipShown = null;
    static string grayTipSig = null;      // 诊断用：灰键集合 + hover 对象的签名（变化才报）

    /// <summary>每帧：鼠标悬在灰键上就弹提示（跟随鼠标），否则收起。</summary>
    void updateGrayTip()
    {
        bool any = false;
        for (int i = 0; i < HashedButtons.Count; i++)
        {
            if (HashedButtons[i].disabled && !HashedButtons[i].dying && HashedButtons[i].gameObject != null)
            {
                any = true;
                break;
            }
        }
        string txt = null;
        if (any)
        {
            GameObject hov = UICamera.Raycast(Input.mousePosition) ? UICamera.lastHit.collider.gameObject : null;
            // 诊断：有灰键在时，把「谁被标灰」+「光标此刻命中谁」按变化各报一次。
            // 「悬停没弹提示」只有两种可能：disabled 没设上、或 hover 没命中 —— 这一行分开它们。
            string dl = "";
            for (int i = 0; i < HashedButtons.Count; i++)
            {
                if (HashedButtons[i].disabled && !HashedButtons[i].dying)
                {
                    dl += HashedButtons[i].hashString + " ";
                }
            }
            string sig = dl + "|" + (hov == null ? "<null>" : hov.name);
            if (sig != grayTipSig)
            {
                grayTipSig = sig;
                QuickTestTrace.Log("btn", "gray probe disabled=[" + dl.Trim()
                    + "] hover=" + (hov == null ? "<null>" : hov.name));
            }
            if (hov != null)
            {
                for (int i = 0; i < HashedButtons.Count; i++)
                {
                    gameUIbutton b = HashedButtons[i];
                    if (!b.disabled || b.dying || b.gameObject == null || string.IsNullOrEmpty(b.disabledHint))
                    {
                        continue;
                    }
                    if (hov == UIHelper.getRealEventGameObject(b.gameObject))
                    {
                        txt = b.disabledHint;
                        break;
                    }
                }
            }
        }
        if (string.IsNullOrEmpty(txt))
        {
            if (grayTipRoot != null && grayTipRoot.activeSelf)
            {
                grayTipRoot.SetActive(false);
            }
            if (grayTipShown != null)
            {
                grayTipShown = null;
                QuickTestTrace.Log("btn", "gray tip=<none>");
            }
            return;
        }
        if (!ensureGrayTip(txt))
        {
            return;
        }
        Camera cam = Program.camera_main_2d;
        if (cam == null)
        {
            return;
        }
        // 跟随鼠标：世界 → 面板局部（面板 scale = unit，所以除以 unit）
        Vector3 bl = cam.ScreenToWorldPoint(new Vector3(0f, 0f, 0f));
        Vector3 tr = cam.ScreenToWorldPoint(new Vector3(Screen.width, Screen.height, 0f));
        float unit = Mathf.Abs(tr.y - bl.y) / Mathf.Max(1, Screen.height);
        if (unit <= 0f)
        {
            return;
        }
        Vector3 w = cam.ScreenToWorldPoint(new Vector3(Input.mousePosition.x + 22f, Input.mousePosition.y + 22f, 0f));
        grayTipRoot.transform.localPosition = new Vector3(w.x / unit, w.y / unit, 0f);
        grayTipRoot.SetActive(true);
        if (grayTipShown != txt)
        {
            grayTipShown = txt;
            QuickTestTrace.Log("btn", "gray tip=" + txt
                + " mouse=" + (int)Input.mousePosition.x + "," + (int)Input.mousePosition.y);
        }
    }

    /// <summary>懒创建提示小面板（尺寸跟着文案走）。相机/尺寸没就绪时返回 false。</summary>
    static bool ensureGrayTip(string txt)
    {
        if (grayTipRoot == null)
        {
            Camera cam = Program.camera_main_2d;
            if (cam == null || Program.ui_main_2d == null)
            {
                return false;
            }
            Vector3 bl = cam.ScreenToWorldPoint(new Vector3(0f, 0f, 0f));
            Vector3 tr = cam.ScreenToWorldPoint(new Vector3(Screen.width, Screen.height, 0f));
            float unit = Mathf.Abs(tr.y - bl.y) / Mathf.Max(1, Screen.height);
            if (unit <= 0f)
            {
                return false;
            }
            int depth = 900;
            for (int i = 0; i < UIPanel.list.Count; i++)
            {
                if (UIPanel.list[i] != null && UIPanel.list[i].depth + 20 > depth)
                {
                    depth = UIPanel.list[i].depth + 20;
                }
            }
            grayTipRoot = new GameObject("gray_tip");
            grayTipRoot.layer = Program.ui_main_2d.layer;
            grayTipRoot.transform.localScale = new Vector3(unit, unit, unit);
            UIPanel panel = grayTipRoot.AddComponent<UIPanel>();
            panel.depth = depth;
            panel.clipping = UIDrawCall.Clipping.None;

            GameObject bg = new GameObject("gray_tip_bg");
            bg.layer = grayTipRoot.layer;
            bg.transform.SetParent(grayTipRoot.transform, false);
            grayTipBg = bg.AddComponent<UITexture>();
            // ⛔ 与遮罩同一坑：Texture2D.whiteTexture 在本工程取不到（纯色块不渲染），
            //    必须用自建的那张 1×1 白纹理（见 DuelUndo.WhiteTex）。
            grayTipBg.mainTexture = DuelUndo.WhiteTex();
            // ⛔ 同一个坑：UITexture.OnStart 下一帧跑 `if (mOutPath != "")` 时，运行时 new 出来的
            //    组件 mOutPath 是 null（≠ ""）⇒ 会把 mainTexture 冲成 null ⇒ 底框不渲染。
            //    与遮罩那三块纯色矩形是同一个真凶（详见 DuelUndo.ShowMask 的说明）。
            grayTipBg.path = "";
            // 同上的材质坑：运行时新建的 UITexture 拿不到默认材质 ⇒ 抄现成的 shader
            Shader uiShader = DuelUndo.UiTexShader();
            if (uiShader != null)
            {
                grayTipBg.shader = uiShader;
            }
            grayTipBg.color = new Color(0.05f, 0.05f, 0.07f, 0.94f);
            grayTipBg.depth = 0;

            GameObject line = new GameObject("gray_tip_text");
            line.layer = grayTipRoot.layer;
            line.transform.SetParent(grayTipRoot.transform, false);
            grayTipLabel = line.AddComponent<UILabel>();
            UILabel tpl = DuelUndo.FindLabelTemplate();      // NGUI 无全局默认字体，必须抄一份真引用
            if (tpl != null)
            {
                grayTipLabel.bitmapFont = tpl.bitmapFont;
                grayTipLabel.trueTypeFont = tpl.trueTypeFont;
                grayTipLabel.fontStyle = tpl.fontStyle;
                grayTipLabel.applyGradient = tpl.applyGradient;
                grayTipLabel.gradientTop = tpl.gradientTop;
                grayTipLabel.gradientBottom = tpl.gradientBottom;
            }
            grayTipLabel.fontSize = 22;
            grayTipLabel.alignment = NGUIText.Alignment.Center;
            grayTipLabel.pivot = UIWidget.Pivot.Center;
            grayTipLabel.color = Color.white;
            grayTipLabel.depth = 1;
        }
        if (grayTipLabel.text != txt)
        {
            grayTipLabel.text = txt;
            int w = txt.Length * 24 + 30;
            int h = 40;
            // 框中心落在「鼠标 + 右下偏移」处 ⇒ 整个框显示在鼠标右下方，不挡指针
            grayTipBg.SetDimensions(w, h);
            grayTipBg.transform.localPosition = new Vector3(w / 2, -h / 2, 0f);
            grayTipLabel.SetDimensions(w - 12, h - 6);
            grayTipLabel.transform.localPosition = new Vector3(w / 2, -h / 2, 0f);
        }
        return true;
    }

    int[] time = new int[2];

    bool[] isTicking = new bool[2];

    public void setTime(int player, int t)
    {
        if (player < 2)
        {
            time[player] = t;
            setTimeAbsolutely(player, t);
            isTicking[player] = true;
            isTicking[1 - player] = false;

        }
    }

    public void setExcited(int player)
    {
        if (player == 0)
        {
            me.under.mainTexture = GameTextureManager.exBar;
            opponent.under.mainTexture = GameTextureManager.bar;
        }
        else
        {
            opponent.under.mainTexture = GameTextureManager.exBar;
            me.under.mainTexture = GameTextureManager.bar;
        }
    }


    public void setTimeStill(int player)    
    {
        time[0] = Program.I().ocgcore.timeLimit;
        time[1] = Program.I().ocgcore.timeLimit;
        setTimeAbsolutely(0, Program.I().ocgcore.timeLimit);
        setTimeAbsolutely(1, Program.I().ocgcore.timeLimit);
        isTicking[0] = false;
        isTicking[1] = false;
        if (player == 0)
        {
            me.under.mainTexture = GameTextureManager.exBar;
            opponent.under.mainTexture = GameTextureManager.bar;
        }
        else
        {
            opponent.under.mainTexture = GameTextureManager.exBar;
            me.under.mainTexture = GameTextureManager.bar;
        }
        if (Program.I().ocgcore.timeLimit == 0)
        {
            me.api_timeHint.text = "infinite";
            opponent.api_timeHint.text = "infinite";
        }
        else
        {
            me.api_timeHint.text = "paused";
            opponent.api_timeHint.text = "paused";
        }
    }

    public bool amIdanger()
    {
        return time[0] < Program.I().ocgcore.timeLimit / 3;
    }

    void setTimeAbsolutely(int player, int t)
    {
        if (Program.I().ocgcore.timeLimit == 0)
        {
            return;
        }
        if (player == 0)
        {
            me.api_timeHint.text = t.ToString() + "/" + Program.I().ocgcore.timeLimit.ToString();
            opponent.api_timeHint.text = "waiting";
            UIHelper.clearITWeen(me.api_healthBar.gameObject);
            iTween.MoveToLocal(me.api_timeBar.gameObject, new Vector3((float)(me.api_timeBar.width) - (float)t / (float)Program.I().ocgcore.timeLimit * (float)(me.api_timeBar.width), me.api_healthBar.gameObject.transform.localPosition.y, me.api_healthBar.gameObject.transform.localPosition.z), 1f);
        }
        if (player == 1)
        {
            opponent.api_timeHint.text = t.ToString() + "/" + Program.I().ocgcore.timeLimit.ToString();
            me.api_timeHint.text = "waiting";
            UIHelper.clearITWeen(opponent.api_healthBar.gameObject);
            iTween.MoveToLocal(opponent.api_timeBar.gameObject, new Vector3((float)(opponent.api_timeBar.width) - (float)t / (float)Program.I().ocgcore.timeLimit * (float)(opponent.api_timeBar.width), opponent.api_healthBar.gameObject.transform.localPosition.y, opponent.api_healthBar.gameObject.transform.localPosition.z), 1f);
        }
    }

    public void realize()
    {
        me.api_healthHint.text = ((float)Program.I().ocgcore.life_0 > 0 ? Program.I().ocgcore.life_0 : 0).ToString();
        opponent.api_healthHint.text = ((float)Program.I().ocgcore.life_1 > 0 ? Program.I().ocgcore.life_1 : 0).ToString();
        me.api_name.text = Program.I().ocgcore.name_0_c;
        opponent.api_name.text = Program.I().ocgcore.name_1_c;
        me.api_face.mainTexture = UIHelper.getFace(Program.I().ocgcore.name_0_c);
        opponent.api_face.mainTexture = UIHelper.getFace(Program.I().ocgcore.name_1_c);
        iTween.MoveToLocal(me.api_healthBar.gameObject, new Vector3(
            (float)(me.api_healthBar.width) - getRealLife(Program.I().ocgcore.life_0) / ((float)Program.I().ocgcore.lpLimit) * (float)(me.api_healthBar.width),
            me.api_healthBar.gameObject.transform.localPosition.y,
            me.api_healthBar.gameObject.transform.localPosition.z), 1f);
        iTween.MoveToLocal(opponent.api_healthBar.gameObject, new Vector3(
            (float)(opponent.api_healthBar.width) - getRealLife(Program.I().ocgcore.life_1) / ((float)Program.I().ocgcore.lpLimit) * (float)(opponent.api_healthBar.width),
            opponent.api_healthBar.gameObject.transform.localPosition.y,
            opponent.api_healthBar.gameObject.transform.localPosition.z), 1f);
        instance_lab.Clear();
        if (Program.I().ocgcore.confirmedCards.Count>0)
        {
            instance_lab.Add(GameStringHelper.yijingqueren);
        }
        foreach (var item in Program.I().ocgcore.confirmedCards)    
        {
            instance_lab.Add(item);
        }
    }

    static float getRealLife(float in_)
    {
        if (in_ < 0)
        {
            return 0;
        }
        if (in_ > Program.I().ocgcore.lpLimit)
        {
            return Program.I().ocgcore.lpLimit;
        }
        return in_;
    }

    void tick()
    {
        if (isTicking[0])
        {
            if (time[0] > 0)
            {
                time[0]--;
            }
            if (amIdanger())  
            {
                if (Program.I().ocgcore != null)
                {
                    Program.I().ocgcore.dangerTicking();
                }
            }
            setTimeAbsolutely(0, time[0]);
        }
        if (isTicking[1])
        {
            if (time[1] > 0)
            {
                time[1]--;
            }
            setTimeAbsolutely(1, time[1]);
        }
    }

    public enum chainCondition
    {
        standard,no,all,smart
    }

    public void set_condition(chainCondition c)
    {
        switch (c)  
        {
            case chainCondition.standard:
                toggle_all.value = false;
                toggle_smart.value = false;
                toggle_ignore.value = false;
                break;
            case chainCondition.no:
                toggle_all.value = false;
                toggle_smart.value = false;
                toggle_ignore.value = true;
                break;
            case chainCondition.all:
                toggle_all.value = true;
                toggle_smart.value = false;
                toggle_ignore.value = false;
                break;
            case chainCondition.smart:
                toggle_all.value = false;
                toggle_smart.value = true;
                toggle_ignore.value = false;
                break;
        }
    }

    public chainCondition get_condition()
    {
        chainCondition res = chainCondition.standard;
        if (toggle_ignore.value)
        {
            res = chainCondition.no;
        }
        if (toggle_smart.value)
        {
            res = chainCondition.smart;
        }
        if (toggle_all.value)
        {
            res = chainCondition.all;
        }
        return res;
    }
}
