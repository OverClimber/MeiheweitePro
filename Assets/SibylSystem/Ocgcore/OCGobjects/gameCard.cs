using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using YGOSharp.OCGWrapper.Enums;

public enum gameCardCondition
{
    floating_clickable = 1,
    still_unclickable = 2,
    verticle_clickable = 3,
}

public struct GPS   
{
    public UInt32 controller;   
    public UInt32 location; 
    public UInt32 sequence; 
    public int position;
}

public class Effect
{
    public int ptr;
    public string desc;
    public int flag;
    public bool forced = false;
}

public class gameCard : OCGobject
{
    public GPS p;

    public uint controllerBased = 0;

    public int md5 = -233;

    public List<gameCard> target = new List<gameCard>();

    public void addTarget(gameCard card_)
    {
        bool exist = false;
        foreach (var item in target)
        {
            if (item == card_)
            {
                exist = true;
            }
        }
        if (exist == false)
        {
            target.Add(card_);
        }
    }

    public void removeTarget(gameCard card_)    
    {
        target.Remove(card_);
    }

    public bool isShowed = false;

    public bool isMinBlockMode = true;

    //public bool getIfInMinMode()
    //{
    //    return isMinBlockMode && (ES_excited_unsafe_should_not_be_changed_dont_touch_this==false);
    //}

    public bool cookie_cared = false;

    /// <summary>
    /// RD 极大怪兽「落稳」判据用：上一帧本卡的世界坐标，以及有没有填过。
    /// 出处与用法见 Ocgcore.rdMaximumLanded（**每帧每卡只能被写一次**，那边有说明）。
    /// </summary>
    public Vector3 rdMaxPrevPos;
    public bool rdMaxPrevSet = false;

    public bool forSelect = false;

    public int selectPtr = 0;

    public int levelForSelect_1 = 0;

    public int levelForSelect_2 = 0;

    public int counterCANcount = 0;

    public int counterSELcount = 0;

    public bool prefered = false;

    YGOSharp.Card data;

    GameObject gameObject_face;

    GameObject gameObject_back;

    GameObject gameObject_event_main;

    GameObject gameObject_event_card_bed;

    TMPro.TextMeshPro cardHint;

    public gameCardCondition condition = gameCardCondition.floating_clickable;

    GameObject game_object_verticle_drawing = null;

    TMPro.TextMeshPro verticle_number = null;

    GameObject game_object_verticle_Star = null;    

    GameObject game_object_monster_cloude = null;

    ParticleSystem game_object_monster_cloude_ParticleSystem = null;

    //public int ability = 2500;

    GameObject obj_number = null;

    BoxCollider VerticleCollider = null;

    int number_showing = 0;

    public List<Effect> effects = new List<Effect>();

    public List<int> sortOptions = new List<int>();

    public gameCard()
    {
        gameObject =Program.I().create(Program.I().mod_ocgcore_card);
        gameObject_face = gameObject.transform.Find("card").Find("face").gameObject;
        gameObject_back = gameObject.transform.Find("card").Find("back").gameObject;
        gameObject_event_main = gameObject.transform.Find("card").Find("event").gameObject;
        cardHint = gameObject.transform.Find("text").GetComponent<TMPro.TextMeshPro>();
        SpSummonFlash = insFlash("0099ff");
        ActiveFlash = insFlash("00ff66");
        SelectFlash = insFlash("ff8000");
        for (int i = 0; i < 2; i++)
        {
            SpSummonFlash[i].gameObject.SetActive(false);
            ActiveFlash[i].gameObject.SetActive(false);
            SelectFlash[i].gameObject.SetActive(false);
        }
        selectKuang = insKuang(Program.I().New_selectKuang);
        chainKuang = insKuang(Program.I().New_chainKuang);
        selectKuang.SetActive(false);
        chainKuang.SetActive(false);
        gameObject.SetActive(false);

    }

    public bool forceRefreshCondition = false;

    public void show()
    {
        clearCookie();
        gameObject.SetActive(true);
        Program.I().ocgcore.AddUpdateAction_s(Update);
        refreshFunctions.Clear();
        refreshFunctions.Add(RefreshFunction_ES);
        refreshFunctions.Add(RefreshFunction_decoration);
        refreshFunctions.Add(card_picture_handler);
        forceRefreshCondition = true;
        gameObject.transform.position = accurate_position;
        gameObject.transform.eulerAngles = accurate_rotation;
    }

    public void hide()
    {
        try
        {
            set_overlay_light(0);
            clearCookie();
            UIHelper.clearITWeen(gameObject);
            del_all_decoration();
            for (int i = 0; i < allObjects.Count; i++)
            {
                MonoBehaviour.Destroy(allObjects[i]);
            }
            allObjects.Clear();
            set_text("");
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.Log(e);
        }
        gameObject.SetActive(false);
    }

    void clearCookie()
    {
        Program.I().ocgcore.RemoveUpdateAction_s(Update);
        isShowed = false;
        prefered = false;
        erase_data();
        target.Clear();
        loaded_cardPictureCode = -1;
        loaded_cardCode = -1;
        loaded_back = -1;
        loaded_specialHint = -1;
        loaded_verticalDrawingCode = -1;
        loaded_verticalDrawingReal = Program.getVerticalTransparency() > 0.5f;
        loaded_verticalDrawingNumber = -1;
        loaded_verticalOverAttribute = -1;
        loaded_verticalatk = -1;
        loaded_verticaldef = -1;
        loaded_verticalpos = -1;
        loaded_verticalcon = -1;
        loaded_controller = -1;
        loaded_location = -1;
        p = new GPS
        {
            controller = 0,
            location = 0,
            position = 0,
            sequence = 0
        };
        CS_clear();
    }

    List<Action> refreshFunctions = new List<Action>();

    public void Update()
    {
        for (int i = 0; i < refreshFunctions.Count; i++)
        {
            try
            {
                refreshFunctions[i]();
            }
            catch (System.Exception e)
            {
                UnityEngine.Debug.Log(e);
            }
        }
    }

    GameObject nagaSign = null; 

    public bool disabled = false;
    public bool SemiNomiSummoned = false;

    public enum flashType
    {
        SpSummon,Active, Select, none
    }

    public enum kuangType
    {
        selected,chaining, none
    }

    public flashType currentFlash = flashType.none;

    public kuangType currentKuang = kuangType.none;

    flashType currentFlashPre = flashType.none;

    kuangType currentKuangPre = kuangType.none; 

    FlashingController[] SpSummonFlash, ActiveFlash, SelectFlash;

    GameObject selectKuang, chainKuang;

    FlashingController MouseFlash;

    public bool IsExtraCard()
    {
        return data.IsExtraCard();
    }

    /// <summary>
    /// 这张卡**当前**的卡面半长（世界单位）：基准 face localScale.y=4 ⇒ 半长 2；
    /// 手牌/摊开行带在俯视角整体放大 s（1.5）作用在父级上 ⇒ lossyScale.y=6 ⇒ 半长 3。
    /// 「卡面上方」一族偏移必须跟它走 —— 用户 2026-09-23 第 5 轮实机：俯视手牌放大后
    /// 闪光锚点没跟着长，2.4 &lt; 3 ⇒ 又掉回卡面里（「参考斜视角下的相对距离」= 半长的 h 倍）。
    /// </summary>
    private float faceHalfWorld()
    {
        Transform f = gameObject_face.transform;
        return f != null ? f.lossyScale.y * 0.5f : 2f;
    }

    void RefreshFunction_decoration()
    {
        for (int i = 0; i < cardDecorations.Count; i++)
        {
            if (cardDecorations[i].game_object != null)
            {
                Vector3 screenposition = Vector3.zero;
                // 选择类标志（可选卡的**三角标志** / 连锁选择标志 / 已选蓝光）单独一支：
                // 它们在 60° 下从卡心往相机方向冒，正俯视下照原样摆不是被压成侧棱就是
                // 被透视位移甩出卡外。俯视角改为：翻平/贴卡 + 锚到卡面这一侧。
                // 用户 2026-09-23 第 5 轮：「别的卡上相关的东西也要做好适配，如卡组选卡时的三角标志」；
                // 第 8 轮把 `selected` 补进名单 —— ⛔ Ocgcore.cs:6894/6968 添加时传的描述串是
                //    **"selected"**（不是 "card_selected"），旧名单里那个串根本没人传 ⇒
                //    蓝色已选标志俯视角既没翻平也没收距离（用户：「蓝色选择图标离卡的距离太远了」）。
                bool selectingMark = Program.topDown
                    && (cardDecorations[i].desctiption == "card_selecting"
                        || cardDecorations[i].desctiption == "chain_selecting"
                        || cardDecorations[i].desctiption == "selected");
                bool selectedMark = selectingMark && cardDecorations[i].desctiption == "selected";
                if (cardDecorations[i].up_of_card || selectingMark)
                {
                    // 「卡面上方 h」的偏移口径收在 `Program.cardUpOffset` 一处 ——
                    // ⛔ 原先写死的 `(0, 1.2, 1.2·1.732)` 是**卡对象自己的上方向**（卡面仰起 30°），
                    //   正俯视下卡面被压平、那个方向变成世界 +z，照搬就会让「可发动闪光 / 特召闪光」
                    //   从卡上沿之外掉进卡面中间（用户 2026-09-23 实机报的「光点变成在卡牌中间」）。
                    // ⛔ 偏移必须乘**这张卡当前的半长**（手牌/摊开放大 1.5 倍后固定 2.4 < 3 会掉回卡面里）。
                    // `selected` 例外：它是**粒子 billboard 光环**（card_selected prefab，蓝色 5×3 帧
                    //   动画、startSize 8），语义是「罩着这张卡」，锚**卡面中心**就好 —— 抬到卡上沿
                    //   反而把一圈光推离卡面。距离也收紧：相对位移在俯视角下随「离屏幕中心越远越放大」
                    //   （机高 31.7 时 3 世界 ≈ 屏缘 +10% ≈ 手牌行 +38px，用户报的「太远」就是它），
                    //   收到 0.8（刚好垫在卡面之上渲染，60° 视角不动 —— 这一支只在 topDown 分支里）。
                    Vector3 anchor = selectedMark
                        ? gameObject_face.transform.position
                        : gameObject_face.transform.position + Program.cardUpOffset(1.2f, faceHalfWorld());
                    screenposition = Program.camera_game_main.WorldToScreenPoint(anchor);
                }
                else
                {
                    screenposition = Program.camera_game_main.WorldToScreenPoint(gameObject_face.transform.position);
                }
                float pull = cardDecorations[i].relative_position;
                if (selectedMark && pull > 0.8f)
                {
                    pull = 0.8f;
                }
                Vector3 worldposition = Camera.main.ScreenToWorldPoint(new Vector3(screenposition.x, screenposition.y, screenposition.z - pull));
                // ⛔ 覆写角必须**算上各 prefab 的烘焙角**（`old/loader.prefab` 的字段接线是唯一真相，
                //    别看 prefab 文件名瞎猜 —— 上一轮就栽在这）：
                // ・card_selecting → mod_ocgcore_selecting：根 identity + **子 Quad 烘焙 +60° X**
                //   （专为 60° 相机烘焙）⇒ 根给 30° 时 Quad 世界角 = 30+60 = 90 正好「平铺朝天」。
                //   给根 90 的写法让 Quad 世界角到 150° —— 从正上方看仍斜着 60°，用户第 6 轮报过。
                // ・chain_selecting → **mod_ocgcore_chain_selector**（loader.prefab:69 按 guid 接线；
                //   select_chain_effect/mod_ocgcore_chain_selecting.prefab 那支根本没人引用）：
                //   与三角同款 **子 Quad 烘焙 +60°** ⇒ 根同样要 30°。第 7 轮前一直给 90°
                //   ⇒ 世界角 150°，连锁时「选择效果处理的图标还是斜的」（用户第 8 轮）就是它。
                // ・selected → mod_ocgcore_card_selected：单节点**粒子系统**，billboard 永远面向
                //   相机，根角度无观感差别，给 90° 保持与「平铺」口径一致。
                cardDecorations[i].game_object.transform.eulerAngles = selectingMark
                    ? (cardDecorations[i].desctiption == "selected"
                        ? new Vector3(90f, 0f, 0f)
                        : new Vector3(30f, 0f, 0f))
                    : cardDecorations[i].rotation;
                cardDecorations[i].game_object.transform.position = worldposition;
                if (cardDecorations[i].scale_change_ignored == false)
                    cardDecorations[i].game_object.transform.localScale += (new Vector3(1, 1, 1) - cardDecorations[i].game_object.transform.localScale) * 0.3f;
            }
        }
        // 偏移方向与理由见 `overlayLightOffset` 的注释 ——
        // 一句话：俯视角下必须换成世界 +z，否则光点糊在卡面正中（用户 2026-09-23 实测）。
        for (int i = 0; i < overlay_lights.Count; i++)
        {
            overlay_lights[i].transform.position = gameObject_face.transform.position + overlayLightOffset;
        }
        if (obj_number != null)
        {
            Vector3 screenposition = Program.camera_game_main.WorldToScreenPoint(
                gameObject_face.transform.position + Program.cardUpOffset(2.4f, faceHalfWorld()));
            Vector3 worldposition = Camera.main.ScreenToWorldPoint(new Vector3(screenposition.x, screenposition.y, screenposition.z - 5));
            obj_number.transform.position = worldposition;
        }
        if (disabled == true && (((p.location & (UInt32)CardLocation.MonsterZone) > 0) || ((p.location & (UInt32)CardLocation.SpellZone) > 0)))
        {
            if (nagaSign == null)
            {
                nagaSign = create(Program.I().mod_simple_quad);
                nagaSign.transform.localScale = Vector3.zero;
                nagaSign.GetComponent<Renderer>().material.mainTexture = GameTextureManager.negated;
            }
            if (game_object_verticle_drawing != null && Program.getVerticalTransparency() > 0.5f)
            {
                if (nagaSign.transform.parent!= game_object_verticle_drawing.transform)
                {
                    nagaSign.transform.SetParent(game_object_verticle_drawing.transform);
                    nagaSign.transform.localRotation = Quaternion.identity;
                    nagaSign.transform.localScale = Vector3.zero;
                    nagaSign.transform.localPosition = new Vector3(0,0,-0.25f);
                }
                try
                {
                    Vector3 devide = game_object_verticle_drawing.transform.localScale;
                    if (Vector3.Distance(Vector3.zero, devide) > 0.01f)
                        nagaSign.transform.localScale = (new Vector3(2.4f / devide.x, 2.4f / devide.y, 2.4f / devide.z));
                }
                catch (Exception)
                {
                }
            }
            else
            {
                if (nagaSign.transform.parent != gameObject_face.transform)
                {
                    nagaSign.transform.SetParent(gameObject_face.transform);
                    nagaSign.transform.localRotation = Quaternion.identity;
                    nagaSign.transform.localScale = Vector3.zero;
                    nagaSign.transform.localPosition = new Vector3(0, 0, -0.25f);
                }
                try
                {
                    Vector3 devide = gameObject_face.transform.localScale;
                    if (Vector3.Distance(Vector3.zero, devide) > 0.01f)
                        nagaSign.transform.localScale = (new Vector3(2.4f / devide.x, 2.4f / devide.y, 2.4f / devide.z));
                }
                catch (Exception)
                {
                }
            }
        }
        else
        {
            if (nagaSign != null)
            {
                destroy(nagaSign,0.6f,true,true);
            }
        }
        if (currentKuangPre!=currentKuang)  
        {
            currentKuangPre = currentKuang;
            switch (currentKuang)
            {
                case kuangType.selected:
                    selectKuang.SetActive(true);
                    chainKuang.SetActive(false);
                    break;
                case kuangType.chaining:
                    selectKuang.SetActive(false);
                    chainKuang.SetActive(true);
                    break;
                case kuangType.none:
                    selectKuang.SetActive(false);
                    chainKuang.SetActive(false);
                    break;
            }

        }
        if (currentFlashPre != currentFlash)
        {
            currentFlashPre = currentFlash;
            switch (currentFlash)
            {
                case flashType.SpSummon:
                    for (int i = 0; i < 2; i++)
                    {
                        ActiveFlash[i].gameObject.SetActive(false);
                        SelectFlash[i].gameObject.SetActive(false);
                    }
                    for (int i = 0; i < 2; i++)
                    {
                        SpSummonFlash[i].gameObject.SetActive(true);
                    }
                    break;
                case flashType.Active:
                    for (int i = 0; i < 2; i++)
                    {
                        SpSummonFlash[i].gameObject.SetActive(false);
                        SelectFlash[i].gameObject.SetActive(false);
                    }
                    for (int i = 0; i < 2; i++)
                    {
                        ActiveFlash[i].gameObject.SetActive(true);
                    }
                    break;
                case flashType.Select:
                    for (int i = 0; i < 2; i++)
                    {
                        SpSummonFlash[i].gameObject.SetActive(false);
                        ActiveFlash[i].gameObject.SetActive(false);
                    }
                    for (int i = 0; i < 2; i++)
                    {
                        SelectFlash[i].gameObject.SetActive(true);
                    }
                    break;
                case flashType.none:
                    for (int i = 0; i < 2; i++)
                    {
                        SpSummonFlash[i].gameObject.SetActive(false);
                        ActiveFlash[i].gameObject.SetActive(false);
                        SelectFlash[i].gameObject.SetActive(false);
                    }
                    break;
            }
        }
        handlerChain();
    }

    #region ES_system

    private bool ES_mouse_check()
    {
        bool re = false;
        if (gameObject_event_main != null)
        {
            if (Program.pointedGameObject == gameObject_event_main)
            {
                re = true;
            }
        }
        if (gameObject_event_card_bed != null)
        {
            if (Program.pointedGameObject == gameObject_event_card_bed)
            {
                re = true;
            }
        }
        if (game_object_verticle_drawing != null)
        {
            if (Program.pointedGameObject == game_object_verticle_drawing)
            {
                re = true;
            }
        }
        for (int i = 0; i < buttons.Count; i++)
        {
            if (buttons[i].gameObjectEvent != null)
            {
                if (Program.pointedGameObject == buttons[i].gameObjectEvent)
                {
                    re = true;
                }
            }
        }
        if (condition == gameCardCondition.still_unclickable)
        {
            re = false;
        }
        return re;
    }

    /// <summary>
    /// 光标是否正指在这张卡的事件碰撞盒上（`card/event`，与 handCardTick/ES_mouse_check
    /// 用的是同一个对象）—— **纯射线几何**，不看卡当前可不可点。
    /// 给俯视角「鼠标滑到对方手卡上 ⇒ 拉镜头看牌」用（用户 2026-09-23 第 8 轮）。
    /// ⛔ 别复用 <see cref="ES_mouse_check"/>：它把 still_unclickable 一票否掉，
    ///    而对方手牌平时恰恰不可点 —— 但「悬停看牌」要的只是几何指向，不是可点性。
    /// </summary>
    public bool ES_pointed_raw()
    {
        return gameObject_event_main != null && Program.pointedGameObject == gameObject_event_main;
    }

    public void ES_lock(float time)
    {
        ES_exit_excited(false);
        MonoBehaviour.Destroy(gameObject.AddComponent<card_locker>(), time);
    }

    private bool ES_check_locked()
    {
        bool return_value = false;
        if (gameObject.transform.GetComponent<card_locker>() != null)
        {
            return_value = true;
        }
        return return_value;
    }

    private bool ES_excited_unsafe_should_not_be_changed_dont_touch_this = false;

    /// <summary>
    /// 排查用：这张卡当前的 ES（悬停放大）状态。只读，不参与任何逻辑。
    ///
    /// 「服务端给了选项、卡上却一个按钮都没有」有两种完全不同的成因，光看按钮数为 0 分不出来：
    ///   idle    —— 压根没进 excited（鼠标没指着它，或被移动动画的 card_locker 锁住了）；
    ///   excited —— 进了 excited，那按钮就该由 ES_excited_handler_button_shower 建出来。
    /// 所以要把「进没进 excited」和「有没有被锁」一起报出来。
    /// </summary>
    public string ES_diag()
    {
        string s = ES_excited_unsafe_should_not_be_changed_dont_touch_this ? "excited" : "idle";
        if (gameObject != null && gameObject.transform.GetComponent<card_locker>() != null)
        {
            s += "+locked";
        }
        return s;
    }

    /// <summary>
    /// 这张卡是不是 RD 极大怪兽（RD 模式 + type bit15 = 0x8000；口径同 Ocgcore.isMaximumCard、
    /// GameStringHelper.typeName）。
    ///
    /// ⚠ 本体与 L/R 部件**都**带这个 bit —— 要区分部件再加「已成素材」那一半，见
    /// <see cref="isRdMaximumPiece"/>。判据里一律带 `GameModeManager.IsRD`：OCG 侧一个像素、
    /// 一个行为都不该变（0x8000 在 OCG 的 type 位里不是「极大」）。
    /// </summary>
    public bool isRdMaximumCard()
    {
        return GameModeManager.IsRD && data != null && (data.Type & 0x8000) != 0;
    }

    /// <summary>这张卡是不是「极大召唤的 L/R 部件」（RD 极大 + 已被收成本体的素材）。</summary>
    public bool isRdMaximumPiece()
    {
        return isRdMaximumCard() && (p.location & (UInt32)CardLocation.Overlay) != 0;
    }

    /// <summary>
    /// 排查用：这张卡当前挂着几件「场上表侧怪兽」专属的装饰 —— 竖立绘、怪兽云、
    /// 等级数字、星级图标。0 = 干净的卡面；非 0 = 被当场上怪兽渲染了。
    ///
    /// ⚠ 为什么要单独报这一个数：`UA_give_condition` 里只有 `verticle_clickable`
    /// 会 `refreshFunctions.Add(card_verticle_drawing_handler + monster_cloude_handler)`
    /// 并加载这几个对象，而它们画的正是用户 2026-09-22 说的「现在多出来的怪兽立绘、
    /// 等级」——`verticle_number` 的内容是 `data.Level`（见 card_verticle_drawing_handler），
    /// `game_object_verticle_Star` 是星级图标。
    /// 而 `cardHint`（探针里的 `txt` / `probe_hint_text`）画的是牌堆数量、攻防那类**文字**，
    /// **是另一个对象**：部件在进场上之前（手牌档）就已经被 `set_text("")` 清过一次，
    /// 之后 `verticle_clickable` 又不会写它 ⇒ 只看 `txt` 会恒为空，
    /// 把「带着满身装饰」误判成「干净」。判据必须咬这个属性。
    /// </summary>
    public int probe_verticle_deco()
    {
        int n = 0;
        if (verticle_number != null) n++;
        if (game_object_verticle_Star != null) n++;
        if (game_object_verticle_drawing != null) n++;
        if (game_object_monster_cloude != null) n++;
        return n;
    }

    /// <summary>
    /// 排查用：这张卡**画在屏幕上的那个矩形** `"x0,y0,x1,y1"`，客户区坐标、左上原点
    /// （与 `[max]` 里的 `win=`、以及 Program.cs 的 `[mouse] winPos=` 同一口径）。
    ///
    /// 用途：RD 大框要「三张无缝拼接 + 一个大框框住」，判据不能只看「中心距 == 卡宽」
    /// （那只能证明**彼此**贴紧，证明不了框也贴着它们）。有了这个矩形，验收脚本就能拿它当锚，
    /// 在截图上量「框的描边离卡的外沿几个像素」，量出来的才是真的贴合。
    ///
    /// 取角点用 `TransformPoint(±0.5, ±0.5)`：卡面是内置 Quad(1x1)，`face` 自己带
    /// `localScale(3,4)`，所以它的局部 ±0.5 就是卡面的四个角（缩放在 transform 里）。
    /// 以**屏幕包围盒**返回（不要求四个角是轴对齐的矩形——卡在场上是有倾角的）。
    /// </summary>
    public string probe_face_rect()
    {
        if (gameObject_face == null || Program.camera_game_main == null)
        {
            return "none";
        }
        Transform f = gameObject_face.transform;
        float x0 = float.MaxValue, x1 = float.MinValue, y0 = float.MaxValue, y1 = float.MinValue;
        for (int i = 0; i < 4; i++)
        {
            Vector3 local = new Vector3((i & 1) == 0 ? -0.5f : 0.5f,
                                        (i & 2) == 0 ? -0.5f : 0.5f, 0f);
            Vector3 sp = Program.camera_game_main.WorldToScreenPoint(f.TransformPoint(local));
            x0 = Math.Min(x0, sp.x); x1 = Math.Max(x1, sp.x);
            y0 = Math.Min(y0, sp.y); y1 = Math.Max(y1, sp.y);
        }
        // Unity 屏幕坐标原点在左下角 ⇒ 换成客户区（左上原点）
        return ((int)Math.Round(x0)) + "," + (Screen.height - (int)Math.Round(y1)) + ","
             + ((int)Math.Round(x1)) + "," + (Screen.height - (int)Math.Round(y0));
    }

    /// <summary>排查用：**选择类标志**（三角 card_selecting）装饰物的**世界欧拉角**，
    /// 没挂三角时返回 "none"。`RefreshFunction_decoration` 每帧写它的角度 ——
    /// 俯视角下三角的子 Quad 自带 +60° 烘焙，根节点必须给 30° 才能平铺（见那处注释）。
    /// 这条访问器让探针咬住「根的实际角度」，防止有人把 30 又改回 90。</summary>
    public string probe_selecting_mark_euler()
    {
        for (int i = 0; i < cardDecorations.Count; i++)
        {
            if (cardDecorations[i].game_object != null
                && cardDecorations[i].desctiption == "card_selecting")
            {
                Vector3 e = cardDecorations[i].game_object.transform.eulerAngles;
                return ((int)Math.Round(e.x)) + "," + ((int)Math.Round(e.y)) + ","
                     + ((int)Math.Round(e.z));
            }
        }
        return "none";
    }

    /// <summary>排查用：**连锁选择标志**（chain_selecting 装饰物）的**世界欧拉角**，没挂时返回 "none"。
    /// 第 8 轮起它的 prefab 是带烘焙 +60° 的 `mod_ocgcore_chain_selector` ⇒ 根 30° 才平铺
    /// （旧写法根 90° ⇒ 世界角 150°，用户报「连锁时选择效果处理的图标还是斜的」）。
    /// 这条访问器让探针咬住「根的实际角度」，防止有人改回 90。</summary>
    public string probe_chain_mark_euler()
    {
        for (int i = 0; i < cardDecorations.Count; i++)
        {
            if (cardDecorations[i].game_object != null
                && cardDecorations[i].desctiption == "chain_selecting")
            {
                Vector3 e = cardDecorations[i].game_object.transform.eulerAngles;
                return ((int)Math.Round(e.x)) + "," + ((int)Math.Round(e.y)) + ","
                     + ((int)Math.Round(e.z));
            }
        }
        return "none";
    }

    /// <summary>排查用：**已选蓝光**（selected 粒子装饰物）与**本卡卡面中心**的屏幕距离（px），
    /// 没挂蓝光时返回 "none"。第 8 轮把它的俯视角拉距收到 0.8（旧值 3 会被透视位移甩出
    /// 卡外，屏缘 ≈ +38px，用户报「蓝色选择图标离卡的距离太远了」）——
    /// 探针咬「图标还贴不贴着卡」这个外壳，不咬 0.8 这个实现常量。</summary>
    public string probe_selected_mark_gap()
    {
        for (int i = 0; i < cardDecorations.Count; i++)
        {
            if (cardDecorations[i].game_object != null
                && cardDecorations[i].desctiption == "selected")
            {
                Vector3 a = Program.camera_game_main.WorldToScreenPoint(
                    cardDecorations[i].game_object.transform.position);
                Vector3 b = Program.camera_game_main.WorldToScreenPoint(
                    gameObject_face.transform.position);
                float d = Vector2.Distance(new Vector2(a.x, a.y), new Vector2(b.x, b.y));
                return ((int)Math.Round(d)).ToString();
            }
        }
        return "none";
    }

    /// <summary>排查用：卡面的**世界尺寸** `"宽,高"`（Quad(1x1) × face 的 lossyScale）。</summary>
    public string probe_face_size()
    {
        if (gameObject_face == null)
        {
            return "none";
        }
        Vector3 s = gameObject_face.transform.lossyScale;
        return ((int)Math.Round(Math.Abs(s.x) * 100f)) + ","
             + ((int)Math.Round(Math.Abs(s.y) * 100f));
    }

    private void RefreshFunction_ES()
    {
        // ⛔ RD 极大怪兽的 L/R 部件**不接受点击**：它们是一体的部件在本体左右的常显，
        //   既不是可选对象、也不该被「确认」（用户 2026-09-22：别再让玩家确认这两张）。
        //   悬停看效果不受影响 —— 那条路走 ES_enter_excited → showMeLeft，不经过这里。
        //   （本处是 ES_cardClicked 在全局唯一的调用点，在这里拦下就再无入口。）
        if (Program.InputGetMouseButtonUp_0 && ES_mouse_check() && !isRdMaximumPiece())
        {
            Program.I().ocgcore.ES_cardClicked(this);
        }

        if (ES_excited_unsafe_should_not_be_changed_dont_touch_this)
        {
            //当前在excited态
            if (ES_mouse_check())
            {
                //刷新excited的数据
                ES_excited_handler();
            }
            else
            {
                //退出excited态
                ES_exit_excited(true);
            }
        }
        else
        {
            //当前不在excited态
            if (ES_mouse_check())
            {
                if (ES_check_locked() == false)
                {
                    //进入excited态
                    ES_enter_excited();
                }
                else
                {
                    //无作为
                }
            }
            else
            {
                //无作为
            }
        }
    }

    private void ES_excited_handler()
    {
        if (ES_excited_unsafe_should_not_be_changed_dont_touch_this)
        {
            ES_excited_handler_close_up_handler();
            ES_excited_handler_button_shower();
            ES_excited_handler_event_cookie_card_bed();
        }
    }

    //float deltaTimeCloseUp=0;
    //private void ES_excited_handler_close_up_handler()
    //{
    //    float faT = 0.25f;
    //    deltaTimeCloseUp += Time.deltaTime;
    //    if (deltaTimeCloseUp > faT)
    //    {
    //        deltaTimeCloseUp = faT;
    //    }
    //    Vector3 screenposition = Program.camera_game_main.WorldToScreenPoint(accurate_position);
    //    Vector3 worldposition = Camera.main.ScreenToWorldPoint(new Vector3(screenposition.x, screenposition.y, screenposition.z - 10));
    //    gameObject.transform.position = new Vector3
    //        (
    //        iTween.easeOutQuad(accurate_position.x, worldposition.x, deltaTimeCloseUp / faT),
    //        iTween.easeOutQuad(accurate_position.y, worldposition.y, deltaTimeCloseUp / faT),
    //        iTween.easeOutQuad(accurate_position.z, worldposition.z, deltaTimeCloseUp / faT)
    //        );
    //    if (game_object_verticle_drawing != null)
    //    {
    //        card_verticle_drawing_handler();
    //    }
    //}

    private void ES_excited_handler_close_up_handler()
    {
        Vector3 screenposition = Program.camera_game_main.WorldToScreenPoint(accurate_position);
        Vector3 worldposition = Camera.main.ScreenToWorldPoint(new Vector3(screenposition.x, screenposition.y, screenposition.z - 10));
        gameObject.transform.position += (worldposition - gameObject.transform.position) * 35f * Program.deltaTime;
        if (game_object_verticle_drawing != null)
        {
            card_verticle_drawing_handler();
        }
    }

    private void ES_excited_handler_button_shower()
    {
        if (opMonsterWithBackGroundCard)   
        {
            Vector3 vector_of_begin = Vector3.zero;
            if ((p.position & (UInt32)CardPosition.Attack) > 0)
            {
                vector_of_begin = gameObject_face.transform.position + new Vector3(0, 0, -2f);
            }
            else
            {
                vector_of_begin = gameObject_face.transform.position + new Vector3(0, 0, -1.5f);
            }
            vector_of_begin = Program.camera_game_main.WorldToScreenPoint(vector_of_begin);
            for (int i = 0; i < buttons.Count; i++)
            {
                buttons[i].show(vector_of_begin - i * (new Vector3(0, 65f * 0.7f * (float)Screen.height / 700f)) - (new Vector3(0, 20f * 0.7f * (float)Screen.height / 700f)));
            }
            return;
        }

        if (condition == gameCardCondition.floating_clickable)
        {
            // 按钮行的**锚点**也在「卡面上方」：同样收进 `Program.cardUpOffset`
            // （俯视角下换成世界 +z、长度 2h），否则整排按钮会压到卡面上。
            Vector3 vector_of_begin = gameObject_face.transform.position + Program.cardUpOffset(1f, faceHalfWorld());
            vector_of_begin = Program.camera_game_main.WorldToScreenPoint(vector_of_begin);
            for (int i = 0; i < buttons.Count; i++)
            {
                buttons[i].show(vector_of_begin + i * (new Vector3(0, 65f * 0.7f * (float)Screen.height / 700f)) + (new Vector3(0, 35f * 0.7f * (float)Screen.height / 700f)));
            }
            return;
        }

        if (condition== gameCardCondition.verticle_clickable)   
        {
            if (VerticleCollider == null)
            {
                Vector3 vector_of_begin;
                if ((p.position & (UInt32)CardPosition.Attack) > 0)
                {
                    vector_of_begin = gameObject_face.transform.position + new Vector3(0, 0, 2);
                }
                else
                {
                    vector_of_begin = gameObject_face.transform.position + new Vector3(0, 0, 1.5f);
                }
                vector_of_begin = Program.camera_game_main.WorldToScreenPoint(vector_of_begin);
                for (int i = 0; i < buttons.Count; i++)
                {
                    buttons[i].show(vector_of_begin + i * (new Vector3(0, 65f * 0.7f * (float)Screen.height / 700f)) + (new Vector3(0, 35f * 0.7f * (float)Screen.height / 700f)));
                }
            }
            else
            {
                float h = loaded_verticalDrawingK * 0.618f;
                Vector3 vector_of_begin = Vector3.zero;
                float l = (0.5f * game_object_verticle_drawing.transform.localScale.y * (h - 0.5f));
                // 展示图上方的按钮行锚点：偏移是「竖着立起 l」的世界长度 —— 收进 `cardUpOffsetLen`
                // （俯视角展示图被压平，世界 +y 分量屏上位移为 0，照搬会让按钮行压到图上）。
                vector_of_begin = game_object_verticle_drawing.transform.position + Program.cardUpOffsetLen(2f * l);
                vector_of_begin = Program.camera_game_main.WorldToScreenPoint(vector_of_begin);
                for (int i = 0; i < buttons.Count; i++)
                {
                    buttons[i].show(vector_of_begin + i * (new Vector3(0, 65f * 0.7f * (float)Screen.height / 700f)) + (new Vector3(0, 35f * 0.7f * (float)Screen.height / 700f)));
                }
            }
            return;
        }
    }

    private void ES_excited_handler_event_cookie_card_bed()
    {
        if (condition != gameCardCondition.verticle_clickable)
        {
            if (gameObject_event_card_bed == null)
            {
                gameObject_event_card_bed
                    = create(Program.I().mod_ocgcore_hidden_button, gameObject.transform.position); 
            }
        }
        else
        {
            if (gameObject_event_card_bed != null)
            {
                destroy(gameObject_event_card_bed);
            }
        }
    }

    private void ES_enter_excited()
    {
        //Program.I().audio.clip = Program.I().dididi;
        //Program.I().audio.Play();
        //deltaTimeCloseUp = 0;
        iTween[] iTweens = gameObject.GetComponents<iTween>();
        for (int i = 0; i < iTweens.Length; i++) MonoBehaviour.DestroyImmediate(iTweens[i]);
        if (condition == gameCardCondition.floating_clickable)
        {
            flash_line_on();
            // ⛔ RD 极大怪兽的 L/R 部件**不做**这个「抬起来朝向镜头」的旋转（用户 2026-09-23
            //   第 6 轮：「查看极大怪兽的lr部件时不像正常的查看怪兽那样放大，而是有点像查看
            //    盖放在魔陷区的卡一样会旋转一下，我希望不要这样」）。部件平躺在场上，悬停时
            //   只走下面的 close-up 拉近放大（正常怪兽 verticle_clickable 的查看方式）+ 左侧
            //   卡片说明，不再旋转。OCG 的超量素材照旧 —— 它们本来就要拉出来摆到 -30 斜看。
            //   （俯视角下 tableauAngle(-30)=0 本来就是无操作，这条只在 60° 视角起作用。）
            if (!isRdMaximumPiece())
            {
                // 「抬起来朝向镜头」—— 俯视角下镜头在天顶，「朝向镜头」就是**平铺**，
                // 所以这个角度走 Program.tableauAngle（关态原样 -30，一个像素不变）。
                iTween.RotateTo(gameObject, new Vector3(Program.tableauAngle(-30f), 0, 0), 0.3f);
            }
        }
        ES_excited_unsafe_should_not_be_changed_dont_touch_this = true;
        showMeLeft(true);

        // ⛔ RD 极大怪兽不做「把素材拉出来看」那一套（用户 2026-09-22 明确不要）：
        //   它的 L/R 是按**原格位**常显在本体左右的部件（见 Ocgcore.maximumPieceWorldPosition），
        //   本来就不「叠在父卡身上」，再拉一次就成了「两张部件从场上下来、摊在旁边」——
        //   玩家看到的是「像确认墓地/卡组、像检查超量素材」那种观感。
        //   OCG 的素材照旧（这里是 OCG 悬停查看素材的入口之一），一个像素不变。
        if (!isRdMaximumCard())
        {
            List<gameCard> overlayed_cards = Program.I().ocgcore.GCS_cardGetOverlayElements(this);
            Vector3 screen = Program.camera_game_main.WorldToScreenPoint(gameObject.transform.position);
            screen.z = 0;
            float k = ((float)Screen.height) / 700f;
            for (int x = 0; x < overlayed_cards.Count; x++)
            {
                if (overlayed_cards[x].isShowed == false)
                {
                    float pianyi = 130f;
                    if (Program.getVerticalTransparency() < 0.5f)
                    {
                        pianyi =90f;
                    }
                    Vector3 screen_vector_to_move = screen + new Vector3(pianyi * k + 60f * k * (overlayed_cards.Count - overlayed_cards[x].p.position - 1), 0, 12f + 2f * (overlayed_cards.Count - overlayed_cards[x].p.position - 1));
                    overlayed_cards[x].flash_line_on();
                    overlayed_cards[x].TweenTo(Camera.main.ScreenToWorldPoint(screen_vector_to_move), new Vector3(Program.tableauAngle(-30f), 0, 0),true);
                }
            }
        }
    }

    void showMeLeft(bool force=false)
    {
        Program.I().cardDescription.setData(data, p.controller == 0 ? GameTextureManager.myBack : GameTextureManager.opBack, tails.managedString, force);
    }

    public void ES_exit_excited(bool move_to_original_place)
    {
        iTween[] iTweens = gameObject.GetComponents<iTween>();
        for (int i = 0; i < iTweens.Length; i++) MonoBehaviour.DestroyImmediate(iTweens[i]);
        flash_line_off();
        ES_excited_unsafe_should_not_be_changed_dont_touch_this = false;
        for (int i = 0; i < buttons.Count; i++)
        {
            buttons[i].hide();
        }
        destroy(gameObject_event_card_bed);
        if (move_to_original_place)
        {
            ES_safe_card_move_to_original_place();
        }
        List<gameCard> overlayed_cards = Program.I().ocgcore.GCS_cardGetOverlayElements(this);
        for (int x = 0; x < overlayed_cards.Count; x++)
        {
            overlayed_cards[x].ES_safe_card_move_to_original_place();
            overlayed_cards[x].flash_line_off();
        }
        MonoBehaviour.Destroy(gameObject.AddComponent<card_locker>(), 0.3f);
    }

    public void ES_safe_card_move_to_original_place()
    {
        TweenTo(accurate_position,accurate_rotation);
    }

    private void ES_safe_card_move(Hashtable move_hash, Hashtable rotate_hash)
    {
        UIHelper.clearITWeen(gameObject);
        MonoBehaviour.DestroyImmediate(gameObject.GetComponent<screenFader>());
        if (Math.Abs((int)(gameObject.transform.eulerAngles.z)) == 180)
        {
            Vector3 p = gameObject.transform.eulerAngles;
            p.z = 179f;
            gameObject.transform.eulerAngles = p;
        }
        iTween.MoveTo(gameObject, move_hash);
        iTween.RotateTo(gameObject, rotate_hash);
    }

    #endregion

    #region UA_system

    //UA_system
    Vector3 gived_position = Vector3.zero;
    Vector3 gived_rotation = Vector3.zero;
    Vector3 accurate_position = Vector3.zero;
    Vector3 accurate_rotation = Vector3.zero;

    public void UA_give_position(Vector3 p)
    {
        gived_position = p;
    }

    public Vector3 UA_get_accurate_position()
    {
        return accurate_position;
    }

    public void UA_give_rotation(Vector3 r)
    {
        gived_rotation = r;
    }

    /// <summary>
    /// 手牌 / 展示行的**放大倍数**（俯视角专用，倍数由 `Program.tableauHandScale` 算）。
    ///
    /// 只碰「自己放大过的那张卡」（<see cref="handRowScaled"/>）—— 与 `rdMaxTrioScaled` 同一个
    /// 教训：卡根的 scale 有自己的创建动画，无脑每帧写就会把它踩掉。关态恒传 1
    /// ⇒ 只会「还原自己改过的」，对别的卡一次写入都不发生（逐字段等价于旧实现）。
    /// ⚠ 不走 `UA_flush_all_gived_witn_lock`：那里只 tween 位置/角度，scale 直接写更稳。
    /// </summary>
    public void UA_give_scale(float s)
    {
        if (s != 1f)
        {
            gameObject.transform.localScale = new Vector3(s, s, s);
            handRowScaled = true;
        }
        else if (handRowScaled)
        {
            gameObject.transform.localScale = Vector3.one;
            handRowScaled = false;
        }
    }

    /// <summary>卡根的 scale 是否被手牌排放大改过 —— 只有改过才由 `UA_give_scale(1)` 还原。</summary>
    public bool handRowScaled = false;

    public void UA_flush_all_gived_witn_lock(bool rush)
    {
        if (Vector3.Distance(gived_position, accurate_position) > 0.001f || Vector3.Distance(gived_rotation, accurate_rotation) > 0.001f)
        {
            if (QuickTestTrace.Enabled && isRdMaximumCard())
            {
                // 排查用：极大怪兽（本体/部件）每一次真实摆位都落一行。
                // 「先下去→弹回来→又下去」这类三段动画的第一现场就在这里：
                // 看相邻两行的 giv 交替就能定位是谁把卡摆了两次。
                QuickTestTrace.Log("maxmv", "flush id=" + data.Id
                    + " loc=0x" + p.location.ToString("X") + " seq=" + p.sequence
                    + " ctrl=" + p.controller + " rush=" + (rush ? "1" : "0")
                    + " acc=(" + accurate_position.x.ToString("F0") + "," + accurate_position.y.ToString("F0") + "," + accurate_position.z.ToString("F0") + ")"
                    + " giv=(" + gived_position.x.ToString("F0") + "," + gived_position.y.ToString("F0") + "," + gived_position.z.ToString("F0") + ")");
            }
            float time = 0.25f;
            time += Vector3.Distance(gived_position, gameObject.transform.position) * 0.05f / 20f;
            ES_lock(time+0.1f);
            UA_reloadCardHintPosition();
            if (rush)
            {
                UIHelper.clearITWeen(gameObject);
                gameObject.transform.position = gived_position;
                gameObject.transform.eulerAngles = gived_rotation;
            }
            else
            {
                TweenTo(gived_position, gived_rotation);
                if (
                    Program.I().ocgcore.currentMessage == GameMessage.Move
                     ||
                    Program.I().ocgcore.currentMessage == GameMessage.Swap
                     ||
                    Program.I().ocgcore.currentMessage == GameMessage.PosChange
                     ||
                    Program.I().ocgcore.currentMessage == GameMessage.FlipSummoning
                    )
                {
                    Program.I().ocgcore.Sleep((int)(30f * time));
                }
            }
            accurate_position = gived_position;
            accurate_rotation = gived_rotation;
        }
    }

    public void TweenTo(Vector3 pos, Vector3 rot, bool exciting = false)
    {
        float time = 0.1f;
        time += Vector3.Distance(pos, gameObject.transform.position) * 0.2f / 30f;
        if (time < 0.1f)
        {
            time = 0.1f;
        }
        if (time > 0.3f)
        {
            time = 0.3f;
        }
        //time *= 20;
        iTween.EaseType e = iTween.EaseType.easeOutQuad;

        if (Vector3.Distance(Vector3.zero, pos) < Vector3.Distance(Vector3.zero, gameObject.transform.position))
        {
            e = iTween.EaseType.easeInQuad;
        }

        if (
            ((Math.Abs(gived_rotation.x) < 10 && Vector3.Distance(pos, gameObject.transform.position) > 1f))
            ||
            (accurate_position.x == pos.x && accurate_position.y < pos.y && accurate_position.z == pos.z)
        )
        {
            Vector3 from = gameObject.transform.position;
            Vector3 to = pos;
            Vector3[] path = new Vector3[30];
            for (int i = 0; i < 30; i++)
            {
                path[i] = from + (to - from) * (float)i / 29f + (new Vector3(0, 1.5f, 0)) * (float)Math.Sin(3.1415926 * (double)i / 29d);
            }
            if (exciting)   
            {
                ES_safe_card_move(
                       iTween.Hash(
                       "x", pos.x,
                       "y", pos.y,
                       "z", pos.z,
                       "path", path,
                       "time", time
                       ),
                       iTween.Hash
                       (
                       "x", rot.x,
                       "y", rot.y,
                       "z", rot.z,
                       "time", time
                       )
                       );
            }
            else
            {
                ES_safe_card_move(
                       iTween.Hash(
                       "x", pos.x,
                       "y", pos.y,
                       "z", pos.z,
                       "path", path,
                       "time", time,
                       "easetype", e
                       ),
                       iTween.Hash
                       (
                       "x", rot.x,
                       "y", rot.y,
                       "z", rot.z,
                       "time", time,
                       "easetype", e
                       )
                       );
            }

        }
        else
        {
            if (exciting)   
            {
                ES_safe_card_move(
                          iTween.Hash(
                          "x", pos.x,
                          "y", pos.y,
                          "z", pos.z,
                          "time", time
                          ),
                          iTween.Hash
                          (
                          "x", rot.x,
                          "y", rot.y,
                          "z", rot.z,
                           "time", time
                          )
                         );
            }
            else
            {
                ES_safe_card_move(
                          iTween.Hash(
                          "x", pos.x,
                          "y", pos.y,
                          "z", pos.z,
                          "time", time,
                          "easetype", e
                          ),
                          iTween.Hash
                          (
                          "x", rot.x,
                          "y", rot.y,
                          "z", rot.z,
                           "time", time,
                           "easetype", e
                          )
                         );
            }

        }
    }

    private void UA_reloadCardHintPosition()
    {
        if ((p.location & (UInt32)CardLocation.MonsterZone) > 0 && (p.location & (UInt32)CardLocation.Overlay) == 0)
        {
            if (p.controller == 0)
            {
                if ((p.position & (UInt32)CardPosition.Attack) > 0)
                {
                    cardHint.gameObject.transform.localPosition = new Vector3(0, 0, -2.5f);
                    cardHint.gameObject.transform.localEulerAngles = new Vector3(Program.tableauFrontX, 0, 0);
                }
                else
                {
                    cardHint.gameObject.transform.localPosition = new Vector3(-2.5f, 0, 0);
                    cardHint.gameObject.transform.localEulerAngles = new Vector3(Program.tableauFrontX, 90, 0);
                }
            }
            else
            {
                if ((p.position & (UInt32)CardPosition.Attack) > 0)
                {
                    cardHint.gameObject.transform.localPosition = new Vector3(0, 0, 2.5f);
                    cardHint.gameObject.transform.localEulerAngles = new Vector3(Program.tableauFrontX - 20f, 180, 0);
                }
                else
                {
                    cardHint.gameObject.transform.localPosition = new Vector3(2.5f, 0, 0);
                    cardHint.gameObject.transform.localEulerAngles = new Vector3(Program.tableauFrontX - 20f, -90, 0);
                }
            }
        }
        else
        {
            cardHint.gameObject.transform.localPosition = new Vector3(0, 0, -2.5f);
            cardHint.gameObject.transform.localEulerAngles = new Vector3(90, 0, 0);
        }
    }

    //private void bugOfUnity()
    //{
    //    this.gameObject.transform.eulerAngles = this.accurate_rotation;
    //}

    public void UA_give_condition(gameCardCondition c)
    {
        if (condition != c || forceRefreshCondition)
        {
            condition = c;
            forceRefreshCondition = false;
            if (condition == gameCardCondition.floating_clickable)
            {
                try
                {
                    gameObject_event_main.GetComponent<MeshCollider>().enabled = true;
                    gameObject.transform.Find("card").GetComponent<animation_floating_slow>().enabled = true;
                }
                catch (System.Exception e)
                {
                    UnityEngine.Debug.Log(e);
                }
                destroy(game_object_monster_cloude);
                destroy(game_object_verticle_drawing);
                if (verticle_number != null) destroy(verticle_number.gameObject);
                destroy(game_object_verticle_Star);
                refreshFunctions.Remove(this.card_verticle_drawing_handler);
                refreshFunctions.Remove(this.monster_cloude_handler);
                loaded_controller = -1;
                loaded_location = -1;
                refreshFunctions.Add(this.card_floating_text_handler);
                //caculateAbility();
            }
            if (condition == gameCardCondition.still_unclickable)
            {
                try
                {
                    gameObject_event_main.GetComponent<MeshCollider>().enabled = false;
                    gameObject.transform.Find("card").GetComponent<animation_floating_slow>().enabled = false;
                    destroy(gameObject_event_card_bed);
                }
                catch (System.Exception e)
                {
                    UnityEngine.Debug.Log(e);
                }
                destroy(game_object_monster_cloude);
                destroy(game_object_verticle_drawing);
                if (verticle_number!=null) destroy(verticle_number.gameObject); 
                destroy(game_object_verticle_Star);
                refreshFunctions.Remove(this.card_verticle_drawing_handler);
                refreshFunctions.Remove(this.monster_cloude_handler);
                refreshFunctions.Remove(this.card_floating_text_handler);
                gameObject.transform.Find("card").transform.localPosition = Vector3.zero;
                set_text("");
                //caculateAbility();
            }
            if (condition == gameCardCondition.verticle_clickable)
            {
                try
                {
                    gameObject_event_main.GetComponent<MeshCollider>().enabled = true;
                    gameObject.transform.Find("card").GetComponent<animation_floating_slow>().enabled = true;
                }
                catch (System.Exception e)
                {
                    UnityEngine.Debug.Log(e);
                }
                loaded_verticalDrawingCode = 0;
                loaded_verticalDrawingNumber = -1;
                loaded_verticalOverAttribute = -1;
                loaded_verticalatk = -1;
                loaded_verticaldef = -1;
                loaded_verticalpos = -1;
                loaded_verticalcon = -1;
                refreshFunctions.Add(this.card_verticle_drawing_handler);
                refreshFunctions.Add(this.monster_cloude_handler);
                refreshFunctions.Remove(this.card_floating_text_handler);
                //caculateAbility();
            }
        }
    }
    int loaded_controller = -1;
    int loaded_location = -1;
    private void card_floating_text_handler()
    {
        if (loaded_controller!= p.controller|| loaded_location!= p.location)    
        {
            loaded_controller = (int)p.controller;
            loaded_location = (int)p.location;
            string loc = "";
            if ((p.location & (UInt32)CardLocation.Deck) > 0)
            {
                loc = GameStringHelper.kazu;
            }
            if ((p.location & (UInt32)CardLocation.Extra) > 0)
            {
                loc = GameStringHelper.ewai;
            }
            if ((p.location & (UInt32)CardLocation.Grave) > 0)
            {
                loc = GameStringHelper.mudi;
            }
            if ((p.location & (UInt32)CardLocation.Removed) > 0)
            {
                loc = GameStringHelper.chuwai;
            }
            if (!SemiNomiSummoned && (data.Type & 0x68020C0) > 0 && (p.location & ((UInt32)CardLocation.Grave + (UInt32)CardLocation.Removed)) > 0)
            {
                loc = GameStringHelper.SemiNomi;
            }
            if (p.controller == 1 && loc != "")
            {
                loc = "<#ff8888>" + loc + "</color>";
            }
            set_text(loc);
        }
    }

    void monster_cloude_handler()
    {
        if (Program.MonsterCloud)
        {
            if (game_object_monster_cloude == null)
            {
                game_object_monster_cloude = create(Program.I().mod_ocgcore_card_cloude, gameObject.transform.position);
                game_object_monster_cloude_ParticleSystem = game_object_monster_cloude.GetComponent<ParticleSystem>();
            }
        }
        else
        {
            if (game_object_monster_cloude != null)
            {
                destroy(game_object_monster_cloude);
                game_object_monster_cloude = null;
                game_object_monster_cloude_ParticleSystem = null;
            }
        }
        if (game_object_monster_cloude != null)
        {
            if (game_object_monster_cloude_ParticleSystem != null)
            {
                Vector3 screenposition = Program.camera_game_main.WorldToScreenPoint(gameObject.transform.position);
                game_object_monster_cloude.transform.position = Camera.main.ScreenToWorldPoint(new Vector3(screenposition.x, screenposition.y, screenposition.z + 3));
                game_object_monster_cloude_ParticleSystem.startSize = UnityEngine.Random.Range(3f, 3f + (20f - 3f) * (float)(Mathf.Clamp(data.Attack,0,3000)) / 3000f);
                if (GameStringHelper.differ(data.Attribute, (long)CardAttribute.Earth))
                {
                    game_object_monster_cloude_ParticleSystem.startColor =
                        new Color(
                            200f / 255f + UnityEngine.Random.Range(-0.2f, 0.2f),
                            80f / 255f + UnityEngine.Random.Range(-0.2f, 0.2f),
                            0f / 255f + UnityEngine.Random.Range(-0.2f, 0.2f));
                }
                if (GameStringHelper.differ(data.Attribute, (long)CardAttribute.Water))
                {
                    game_object_monster_cloude_ParticleSystem.startColor =
                       new Color(
                           0f / 255f + UnityEngine.Random.Range(-0.2f, 0.2f),
                           0f / 255f + UnityEngine.Random.Range(-0.2f, 0.2f),
                           255f / 255f + UnityEngine.Random.Range(-0.2f, 0.2f));
                }
                if (GameStringHelper.differ(data.Attribute, (long)CardAttribute.Fire))
                {
                    game_object_monster_cloude_ParticleSystem.startColor =
                      new Color(
                          255f / 255f + UnityEngine.Random.Range(-0.2f, 0.2f),
                          0f / 255f + UnityEngine.Random.Range(-0.2f, 0.2f),
                          0f / 255f + UnityEngine.Random.Range(-0.2f, 0.2f));
                }
                if (GameStringHelper.differ(data.Attribute, (long)CardAttribute.Wind))
                {
                    game_object_monster_cloude_ParticleSystem.startColor =
                      new Color(
                          0f / 255f + UnityEngine.Random.Range(-0.2f, 0.2f),
                          140f / 255f + UnityEngine.Random.Range(-0.2f, 0.2f),
                          0f / 255f + UnityEngine.Random.Range(-0.2f, 0.2f));
                }
                if (GameStringHelper.differ(data.Attribute, (long)CardAttribute.Dark))
                {
                    game_object_monster_cloude_ParticleSystem.startColor =
                       new Color(
                           158f / 255f + UnityEngine.Random.Range(-0.2f, 0.2f),
                           0f / 255f + UnityEngine.Random.Range(-0.2f, 0.2f),
                           158f / 255f + UnityEngine.Random.Range(-0.2f, 0.2f));
                }
                if (GameStringHelper.differ(data.Attribute, (long)CardAttribute.Light))
                {
                    game_object_monster_cloude_ParticleSystem.startColor =
                        new Color(
                            255f / 255f + UnityEngine.Random.Range(-0.2f, 0.2f),
                            140f / 255f + UnityEngine.Random.Range(-0.2f, 0.2f),
                            0f / 255f + UnityEngine.Random.Range(-0.2f, 0.2f));
                }
                if (GameStringHelper.differ(data.Attribute, (long)CardAttribute.Divine))
                {
                    game_object_monster_cloude_ParticleSystem.startColor =
                        new Color(
                            255f / 255f + UnityEngine.Random.Range(-0.2f, 0.2f),
                            140f / 255f + UnityEngine.Random.Range(-0.2f, 0.2f),
                            0f / 255f + UnityEngine.Random.Range(-0.2f, 0.2f));
                }
            }
        }
    }
    int loaded_verticalDrawingCode = -1;
    bool loaded_verticalDrawingReal = false;
    bool loaded_verticalDrawingSingle = false;
    float loaded_verticalDrawingK = 1;
   // bool picLikeASquare = false;
    int loaded_verticalDrawingNumber = -1;
    int loaded_verticalatk = -1;
    int loaded_verticaldef = -1;
    int loaded_verticalpos = -1;
    int loaded_verticalcon = -1;
    /// <summary>上一帧的「极大模式」档（1 = 本体名下挂着 L/R 素材）。为什么要单独缓存：
    /// 素材收走/散伙不一定带动攻守或位置变化，不进脏检查的话「极大状态一变」就不会重写文字。
    /// 判据见 card_verticle_drawing_handler 里那段注释。</summary>
    int loaded_verticalmaxstate = -1;
    int loaded_verticalColor = -1;
    int loaded_verticalOverAttribute = -1;
    float k_verticle = 1;
    float VerticleTransparency = 1f;
    public bool opMonsterWithBackGroundCard=false;    
    void card_verticle_drawing_handler()
    {
        // 🔑 RD 极大怪兽**本体**的立绘两态（用户 2026-09-24 晚报「中间件单独召唤显示极大立绘」）：
        //   挂着 L/R 素材（极大状态）= 宽幅极大立绘（closeup/{code}.png，~970 宽）；
        //   单独在场（素材数 0）= 单体立绘（closeup/{code}_2.png，512 方图，资源 2026-09-19 已铺、
        //   此前代码从未读）。L/R 部件与普通卡不满足 isRdMaximumCard ⇒ 恒走常规档；
        //   OCG 侧 isRdMaximumCard() 恒 false，零影响。缺 _2 资源时纹理层回落 {code}.png。
        //   ⚠ 单体态必须进脏检查（素材收编/散伙不带动 data.Id 变化），同 loaded_verticalmaxstate 的教训。
        bool singleDrawing = isRdMaximumCard()
            && Program.I().ocgcore.GCS_cardGetOverlayCount(this) == 0;
        GameTextureType drawingType = singleDrawing
            ? GameTextureType.card_verticle_drawing_single
            : GameTextureType.card_verticle_drawing;
        if (game_object_verticle_drawing == null || loaded_verticalDrawingCode != data.Id || loaded_verticalDrawingReal != Program.getVerticalTransparency() > 0.5f
            || loaded_verticalDrawingSingle != singleDrawing)
        {
            if (Program.getVerticalTransparency() > 0.5f)
            {
                Texture2D texture = GameTextureManager.get(data.Id, drawingType);
                if (texture != null)
                {
                    loaded_verticalDrawingCode = data.Id;
                    loaded_verticalDrawingSingle = singleDrawing;
                    loaded_verticalDrawingK = GameTextureManager.getK(data.Id, drawingType);
                   // picLikeASquare = GameTextureManager.getB(data.Id, GameTextureType.card_verticle_drawing);
                    if (game_object_verticle_drawing == null)
                    {
                        game_object_verticle_drawing = create(Program.I().mod_simple_quad, gameObject.transform.position, new Vector3(Program.tableauFrontX, 0, 0));
                        VerticleTransparency = 1f;
                    }
                    if (loaded_verticalDrawingReal != Program.getVerticalTransparency() > 0.5f)
                    {
                        loaded_verticalDrawingReal = Program.getVerticalTransparency() > 0.5f;
                        game_object_verticle_drawing.transform.localScale = Vector3.zero;
                    }
                    game_object_verticle_drawing.GetComponent<Renderer>().material.mainTexture = texture;
                    k_verticle = (float)texture.width / (float)texture.height;
                }
            }
            else
            {
                Texture2D texture = GameTextureManager.N;
                loaded_verticalDrawingCode = data.Id;
                loaded_verticalDrawingK = 1;
               // picLikeASquare = true;
                if (game_object_verticle_drawing == null)
                {
                    game_object_verticle_drawing = create(Program.I().mod_simple_quad, gameObject.transform.position, new Vector3(Program.tableauFrontX, 0, 0));
                    VerticleTransparency = 1f;
                }
                if (loaded_verticalDrawingReal != Program.getVerticalTransparency() > 0.5f)
                {
                    loaded_verticalDrawingReal = Program.getVerticalTransparency() > 0.5f;
                    game_object_verticle_drawing.transform.localScale = Vector3.zero;
                }
                game_object_verticle_drawing.GetComponent<Renderer>().material.mainTexture = texture;
                k_verticle = (float)texture.width / (float)texture.height;
            }
        }
        else
        {
            float trans = 1f;
            //if (opMonsterWithBackGroundCard && loaded_verticalDrawingK < 0.9f && ability / loaded_verticalDrawingK > 2600f / 0.9f)  
            //{
            //    trans = 0.5f;
            //}
            //else
            //{
            //    trans = 1f;
            //}
            trans *= Program.getVerticalTransparency();
            if (trans < 0)
            {
                trans = 0;
            }
            if (trans > 1)
            {
                trans = 1;
            }
            if (trans!= VerticleTransparency)       
            {
                VerticleTransparency = trans;
                game_object_verticle_drawing.GetComponent<Renderer>().material.color = new Color(1, 1, 1, trans);
            }
            if (Program.getVerticalTransparency() <= 0.5f||opMonsterWithBackGroundCard) 
            {
                if (VerticleCollider != null)
                {
                    MonoBehaviour.DestroyImmediate(VerticleCollider);
                    VerticleCollider = null;
                }
            }
            else
            {
                if (VerticleCollider == null)
                {
                    VerticleCollider = game_object_verticle_drawing.AddComponent<BoxCollider>();
                }
            }

            Vector3 want_scale = Vector3.zero;
            float showscale = (isMinBlockMode ? 4.2f : Program.verticleScale) / loaded_verticalDrawingK;
            want_scale = new Vector3(showscale * k_verticle, showscale, 1);

            game_object_verticle_drawing.transform.position = get_verticle_drawing_vector(gameObject_face.transform.position);
            game_object_verticle_drawing.transform.localScale += (want_scale - game_object_verticle_drawing.transform.localScale) * Program.deltaTime * 10f;

            if (VerticleCollider != null)
            {
                float h = loaded_verticalDrawingK * 0.618f;
                VerticleCollider.size = new Vector3(4.3f / want_scale.x, h, 0.5f);
                VerticleCollider.center = new Vector3(0, -0.5f + 0.5f * h, 0);
            }


            int color = 0;

            if ((data.Type & (int)CardType.Tuner) > 0)
            {
                color = 1;
            }

            if ((data.Type & (int)CardType.Xyz) > 0)
            {
                color = 2;
            }
            if ((data.Type & (int)CardType.Link) > 0)
            {
                color = 3;
                data.Level = 0;
                for (int i = 0; i < 32; i++)
                {
                    if ((data.LinkMarker & 1 << i) > 0)
                    {
                        data.Level++;
                    }
                }
            }

            if (verticle_number == null || loaded_verticalDrawingNumber != (int)data.Level || loaded_verticalColor != color)
            {
                loaded_verticalDrawingNumber = (int)data.Level;
                loaded_verticalColor = color;
                if (verticle_number == null)
                {
                    verticle_number = create(Program.I().new_ui_textMesh, Vector3.zero, new Vector3(Program.tableauFrontX, 0, 0), true, null, true, new Vector3(3 * 1.8f * 0.04f, 3 * 1.8f * 0.04f, 3 * 1.8f * 0.04f)).GetComponent<TMPro.TextMeshPro>();
                }
                if (game_object_verticle_Star == null)
                {
                    game_object_verticle_Star = create(Program.I().mod_simple_quad, Vector3.zero, new Vector3(Program.tableauFrontX, 0, 0), true, null, true, new Vector3(3 * 1.8f * 0.17f, 3 * 1.8f * 0.17f, 3 * 1.8f * 0.17f));
                }
                if (color == 0)
                {
                    verticle_number.text = data.Level.ToString();
                    game_object_verticle_Star.GetComponent<Renderer>().material.mainTexture = GameTextureManager.L;
                }
                if (color == 1)
                {
                    verticle_number.text = "<#FFFF00>" + data.Level.ToString() + "</color>";
                    game_object_verticle_Star.GetComponent<Renderer>().material.mainTexture = GameTextureManager.L;
                }
                if (color == 2)
                {
                    verticle_number.text = "<#999999>" + data.Level.ToString() + "</color>";
                    game_object_verticle_Star.GetComponent<Renderer>().material.mainTexture = GameTextureManager.R;
                }
                if (color == 3)
                {
                    verticle_number.text = "<#E1FFFF>" + data.Level.ToString() + "</color>";
                    game_object_verticle_Star.GetComponent<Renderer>().material.mainTexture = GameTextureManager.LINK;
                }
            }

            if (Program.getVerticalTransparency() < 0.5f)
            {
                Vector3 screen_number_pos;
                screen_number_pos = 2 * gameObject_face.transform.position - cardHint.gameObject.transform.position;
                screen_number_pos = Program.camera_game_main.WorldToScreenPoint(screen_number_pos + new Vector3(-0.25f, 0, -0.7f));
                screen_number_pos.z -= 2f;
                verticle_number.transform.position = Program.camera_game_main.ScreenToWorldPoint(screen_number_pos);
                if (game_object_verticle_Star != null)
                {
                    screen_number_pos = 2 * gameObject_face.transform.position - cardHint.gameObject.transform.position;
                    screen_number_pos = Program.camera_game_main.WorldToScreenPoint(screen_number_pos + new Vector3(-1.5f, 0, -0.7f));
                    screen_number_pos.z -= 2f;
                    game_object_verticle_Star.transform.position = Program.camera_game_main.ScreenToWorldPoint(screen_number_pos);
                }
            }
            else
            {
                Vector3 screen_number_pos;
                // 「卡面上方」的精调偏移收进 `cardUpOffsetLen`（up 分量长度 1.3 = |(0, 0.65, 0.65·1.732)|）：
                // 俯视角照搬世界 +y 分量的屏上位移是 0，等级数字/星标会掉进展示图里。
                screen_number_pos = Program.camera_game_main.WorldToScreenPoint(cardHint.gameObject.transform.position
                    + new Vector3(-0.61f, 0f, 0f) + Program.cardUpOffsetLen(1.3f));
                screen_number_pos.z -= 2f;
                verticle_number.transform.position = Program.camera_game_main.ScreenToWorldPoint(screen_number_pos);
                if (game_object_verticle_Star != null)
                {
                    screen_number_pos = Program.camera_game_main.WorldToScreenPoint(cardHint.gameObject.transform.position
                        + new Vector3(-1.86f, 0f, 0f) + Program.cardUpOffsetLen(1.3f));
                    screen_number_pos.z -= 2f;
                    game_object_verticle_Star.transform.position = Program.camera_game_main.ScreenToWorldPoint(screen_number_pos);
                }
            }

            // 🔑 RD 极大怪兽「极大模式」下不显示守备力（用户 2026-09-22）。
            //    极大状态 = 本体名下挂着 L/R 素材（core 口径 RushDuel.MaximumMode =
            //    EFFECT_MAXIMUM_MODE && GetOverlayCount()>0；客户端能看到的对应物就是
            //    「本体是 RD 极大卡且素材数 > 0」）。此时卡面只写攻击力 —— 守备位在
            //    极大形态里恒 0、也没有意义。素材散伙/离场 ⇒ 计数归零、本档翻回，
            //    攻/守照旧显示。OCG 侧 isRdMaximumCard() 恒 false，一个字符都不变。
            //    ⚠ 极大状态必须进脏检查（素材数变化不一定带动攻守/位置变化），
            //      所以单独缓存 loaded_verticalmaxstate。
            int maxState = (isRdMaximumCard()
                            && Program.I().ocgcore.GCS_cardGetOverlayCount(this) > 0) ? 1 : 0;
            if (loaded_verticalatk != data.Attack || loaded_verticaldef != data.Defense  || loaded_verticalpos!=p.position|| loaded_verticalcon!=p.controller
                || loaded_verticalmaxstate != maxState)
            {
                loaded_verticalatk = data.Attack;
                loaded_verticaldef = data.Defense;
                loaded_verticalpos = p.position;
                loaded_verticalcon = (int)p.controller;
                loaded_verticalmaxstate = maxState;
                if ((data.Type&(uint)CardType.Link)>0)
                {
                    string raw = "";
                    YGOSharp.Card data_raw = YGOSharp.CardsManager.Get(data.Id);
                    if (data.Attack > data_raw.Attack)
                    {
                        raw += "<#7fff00>" + data.Attack.ToString() + "</color>";
                    }
                    if (data.Attack < data_raw.Attack)
                    {
                        raw += "<#dda0dd>" + data.Attack.ToString() + "</color>";
                    }
                    if (data.Attack == data_raw.Attack)
                    {
                        raw += data.Attack.ToString();
                    }
                    if (p.sequence==5||p.sequence==6)
                    {
                        raw += "(" + (p.controller == 0 ? GameStringHelper._wofang : GameStringHelper._duifang) + ")";
                    }
                    set_text(raw.Replace("-2", "?"));
                }
                else
                {
                    string raw = "";
                    YGOSharp.Card data_raw = YGOSharp.CardsManager.Get(data.Id);
                    if ((loaded_verticalpos & (int)CardPosition.Attack) > 0)
                    {
                        if (data.Attack > data_raw.Attack)
                        {
                            raw += "<#7fff00>" + data.Attack.ToString() + "</color>";
                        }
                        if (data.Attack < data_raw.Attack)
                        {
                            raw += "<#dda0dd>" + data.Attack.ToString() + "</color>";
                        }
                        if (data.Attack == data_raw.Attack)
                        {
                            raw += data.Attack.ToString();
                        }
                        if (maxState == 0)
                        {
                            raw += "/";
                            raw += "<#888888>" + data.Defense.ToString() + "</color>";
                        }
                        if (p.sequence == 5 || p.sequence == 6)
                        {
                            raw += "(" + (p.controller == 0 ? GameStringHelper._wofang : GameStringHelper._duifang) + ")";
                        }
                        set_text(raw.Replace("-2", "?"));
                    }
                    else
                    {
                        raw += "<#888888>" + data.Attack.ToString() + "</color>";
                        if (maxState == 0)
                        {
                            raw += "/";
                            if (data.Defense > data_raw.Defense)
                            {
                                raw += "<#7fff00>" + data.Defense.ToString() + "</color>";
                            }
                            if (data.Defense < data_raw.Defense)
                            {
                                raw += "<#dda0dd>" + data.Defense.ToString() + "</color>";
                            }
                            if (data.Defense == data_raw.Defense)
                            {
                                raw += data.Defense.ToString();
                            }
                        }
                        if (p.sequence == 5 || p.sequence == 6)
                        {
                            raw += "(" + (p.controller == 0 ? GameStringHelper._wofang : GameStringHelper._duifang) + ")";
                        }
                        set_text(raw.Replace("-2", "?"));
                    }
                }


            }
        }
    }

    //private float caculateBoxWidth()
    //{
    //    float colliderWidth = 1f;
    //    float showscale = 2f + (float)(ability - 1000) / 1000f;
    //    if (showscale > 4) showscale = 4;
    //    if (showscale < 2) showscale = 2;
    //    showscale *= 1.8f / loaded_verticalDrawingK;
    //    showscale *= k_verticle;
    //    colliderWidth = 4.3f / showscale;
    //    return colliderWidth;
    //}

    //public void caculateAbility()
    //{
    //    if (condition== gameCardCondition.verticle_clickable)
    //    {
    //        if ((p.position & (UInt32)CardPosition.Attack) > 0)
    //        {
    //            ability = data.Attack;
    //        }
    //        else
    //        {
    //            ability = data.Defense;
    //        }
    //    }
    //    else
    //    {
    //        ability = data.Attack;
    //    }
    //    if (ability > 3000)
    //    {
    //        ability = 3000;
    //    }
    //    if (ability < 0)
    //    {
    //        ability = 0;
    //    }
    //}

    #endregion

    #region data

    public void set_data(YGOSharp.Card d)
    {
        data = d;
        //caculateAbility();
        if (Program.I().cardDescription.ifShowingThisCard(data))
        {
            showMeLeft();
        }
    }

    /// <summary>
    /// 这张卡的**卡面编号是不是「卡组记牌」写上去的**。
    ///
    /// 为什么需要它：记牌要把「卡组还剩什么」铺到展示中的卡组占位卡上，做法是 `set_code`。
    /// 但抽卡/检索/回收等操作走 `GCS_cardMove`，**移动的是同一个 gameCard 对象**
    /// （Deck → Hand 就地搬），引擎紧接着会 `set_code(真码)`；如果记牌在下一拍还按
    /// 「这一拍不在展示集合里」去 `erase_data()`，就会把引擎刚认定的真码擦成 0 ——
    /// 手牌上那张牌从此变成未知卡、且因为 `data.Id==0` 点它发不出应答（"不能使用"）。
    ///
    /// 所以立一条铁律：**记牌只擦自己写过的东西**。任何非记牌路径给出真码
    /// （`set_code(code>0)`）都表示「引擎已经认定这张卡」，此处立即交还所有权。
    /// 记牌这边的写入走 `set_memo_code`、擦除走 `erase_memo_code`，两者都认这个标记。
    /// </summary>
    public bool memoFaceOwned = false;

    /// <summary>
    /// 正在走记牌自己的写入（`set_memo_code`）。
    ///
    /// 只给 `set_code` 里的排查探针用：没有它的话，我们**每拍重写自己铺的码**都会被记成
    /// 「引擎把这张卡收走了」——记牌页面刷屏不说，真正那次抽卡的证据还会被淹掉。
    /// </summary>
    bool memoWritingOwnCode = false;

    public void set_code(int code)
    {
        if (code>0)
        {
            if (memoFaceOwned && memoWritingOwnCode == false && QuickTestTrace.Enabled)
            {
                // 排查用：记牌铺上去的临时码正被**引擎**的真码取代。
                // 拿这条就能看出「记牌铺过的牌被抽走/检索走/公开了」——
                // 抽卡那类 bug 的第一现场就是这个转移（记牌此后再也不许碰这张卡）。
                // `loc` 是关键：`loc!=1`（离开卡组区）才是真的被搬走，`loc=1` 只可能是别的路径改写。
                QuickTestTrace.Log("memo", "deck_memo reclaim id=" + code
                    + " loc=" + p.location + " ctrl=" + p.controller);
            }
            // ⛔ 这一句必须放在 `data.Id != code` 判断**外面**：
            // 引擎给出的真码恰好等于我们铺上去的临时码时，里面那句会跳过，
            // 我们就永远不知道该交还所有权了（正是「抽到的正好是同一张」的偶发场景）。
            memoFaceOwned = false;
            if (data.Id != code)
            {
                set_data(YGOSharp.CardsManager.Get(code));
                data.Id = code;
                if (p.controller == 1)
                {
                    if (Program.I().ocgcore.condition== Ocgcore.Condition.duel) 
                    {
                        if (!Program.I().ocgcore.sideReference.ContainsKey(code))   
                        {
                            Program.I().ocgcore.sideReference.Add(code, code);
                        }
                    }
                }
            }
        }
    }

    /// <summary>
    /// 「卡组记牌」专用：把卡面码铺上去，并声明**这张卡的面归记牌管**。
    ///
    /// ⚠ 必须走这个入口，不能直接 `set_code`：`set_code` 会把 `memoFaceOwned` 清掉
    /// （那是给引擎路径用的），所以要先让它清、再自己置 true。
    /// </summary>
    public void set_memo_code(int code)
    {
        memoWritingOwnCode = true;
        try
        {
            set_code(code);
        }
        finally
        {
            memoWritingOwnCode = false;
        }
        if (code > 0)
        {
            memoFaceOwned = true;
        }
    }

    /// <summary>
    /// 「卡组记牌」专用：把卡面擦回未知。**幂等且只擦记牌自己写的**。
    ///
    /// 引擎已经认定过（`memoFaceOwned == false`）就原样返回 —— 这是防止
    /// 「抽上来的牌被记牌擦成未知」的最后一道闸。
    /// </summary>
    public void erase_memo_code()
    {
        if (memoFaceOwned == false)
        {
            return;
        }
        memoFaceOwned = false;
        if (data.Id != 0)
        {
            erase_data();
        }
    }

    public void refreshData()
    {
        YGOSharp.CardsManager.Get(data.Id).cloneTo(data);
        set_data(data);
        clear_all_tail();
    }

    public void erase_data()    
    {
        // 擦回未知 = 这张卡重新「没人认领」，记牌的归属标记也要一并交还
        // （gameCard 对象是复用的，留着上一次的 true 会让记牌误擦后来的真码）。
        memoFaceOwned = false;
        set_data(YGOSharp.CardsManager.Get(0));
        disabled = false;
        clear_all_tail();
    }

    public YGOSharp.Card get_data()
    {
        return data;
    }

    int loaded_cardPictureCode = -1;
    int loaded_cardCode = -1;   
    int loaded_back = -1;
    int loaded_specialHint = -1;
    bool cardCodeChangedButNowLoadedPic = false;

    /// <summary>
    /// 排查用（只读）：这张卡的**卡面**当前实际贴上的是哪个 code 的图（-1 = 还没贴过）。
    /// 与 `get_data().Id` 的区别正是「记牌」要的那一步 —— 数据改了不等于贴图换了，
    /// 验收要证的就是「贴图真的跟着换了」，光看 data.Id 证明不了。
    /// </summary>
    public int debugFacePictureCode
    {
        get { return loaded_cardPictureCode; }
    }

    void card_picture_handler()
    {
        if (loaded_cardCode != data.Id)
        {
            loaded_cardCode = data.Id;
            cardCodeChangedButNowLoadedPic = true;
        }
        if (loaded_cardPictureCode != data.Id)
        {
            Texture2D texture = GameTextureManager.get(data.Id, GameTextureType.card_picture, p.controller == 0 ? GameTextureManager.myBack : GameTextureManager.opBack);
            if (texture != null)
            {
                loaded_cardPictureCode = data.Id;
                gameObject_face.GetComponent<Renderer>().material.mainTexture = texture;
            }
            else
            {
                if (cardCodeChangedButNowLoadedPic) 
                {
                    gameObject_face.GetComponent<Renderer>().material.mainTexture = GameTextureManager.unknown;
                    cardCodeChangedButNowLoadedPic = false;
                }
            }
        }
        if (p.controller != loaded_back)
        {
            try
            {
                loaded_back = (int)p.controller;
                UIHelper.getByName(gameObject, "back").GetComponent<Renderer>().material.mainTexture = loaded_back == 0 ? GameTextureManager.myBack : GameTextureManager.opBack;
                if (data.Id == 0)
                {
                    UIHelper.getByName(gameObject, "face").GetComponent<Renderer>().material.mainTexture = loaded_back == 0 ? GameTextureManager.myBack : GameTextureManager.opBack;
                }
                del_one_tail(GameStringHelper.opHint);
                if (loaded_back != controllerBased)
                {
                    add_string_tail(GameStringHelper.opHint);
                }
            }
            catch (System.Exception e)
            {
                UnityEngine.Debug.Log(e);
            }
        }
        int special_hint = 0;
        if ((p.position & (int)CardPosition.FaceDown) > 0)
        {
            if ((p.location & (int)CardLocation.Removed) > 0)
            {
                special_hint = 1;
            }
        }
        if ((p.position & (int)CardPosition.FaceUp) > 0)
        {
            if ((p.location & (int)CardLocation.Extra) > 0)
            {
                special_hint = 2;
            }
        }
        if (loaded_specialHint!= special_hint)
        {
            loaded_specialHint = special_hint;
            if (loaded_specialHint==0)  
            {
                del_one_tail(GameStringHelper.licechuwai);
                del_one_tail(GameStringHelper.biaoceewai);
            }
            if (loaded_specialHint == 1)
            {
                add_string_tail(GameStringHelper.licechuwai);
            }
            if (loaded_specialHint == 2)
            {
                add_string_tail(GameStringHelper.biaoceewai);
            }
        }
    }

    public MultiStringMaster tails = new MultiStringMaster();

    public void add_string_tail(string str)
    {
        tails.Add(str);
        if (Program.I().cardDescription.ifShowingThisCard(data))    
        {
            showMeLeft();
        }
    }

    public void clear_all_tail()
    {
        tails.clear();
        if (Program.I().cardDescription.ifShowingThisCard(data))
        {
            showMeLeft();
        }
    }

    public void del_one_tail(string str)
    {
        tails.remove(str);
        if (Program.I().cardDescription.ifShowingThisCard(data))
        {
            showMeLeft();
        }
    }

    #endregion

    #region tools


    public bool isHided()
    {
        if ((p.location & (int)CardLocation.Deck) > 0)
        {
            return true;
        }
        if ((p.location & (int)CardLocation.Extra) > 0)
        {
            return true;
        }
        if ((p.location & (int)CardLocation.Removed) > 0)
        {
            return true;
        }
        if ((p.location & (int)CardLocation.Grave) > 0)
        {
            return true;
        }
        return false;
    }

    public void set_text(string s)
    {
        cardHint.gameObject.SetActive(s != "");
        cardHint.text = s;
    }

    /// <summary>
    /// 卡面上那串「ATK/DEF」文本原文（含颜色标记），只给 `_verify_rdai_game.py` 的
    /// `[max]` 探针读 —— 判断「显示不对」是没推过来还是推过来没画上去，只能看这串。
    /// </summary>
    public string probe_hint_text
    {
        get { return cardHint == null ? "" : cardHint.text; }
    }

    private int get_color_num_int()
    {
        int re = 0;
        //
        if (GameStringHelper.differ(data.Attribute, (long)CardAttribute.Earth))
        {
            re = 0;
        }
        if (GameStringHelper.differ(data.Attribute, (long)CardAttribute.Water))
        {
            re = 3;
        }
        if (GameStringHelper.differ(data.Attribute, (long)CardAttribute.Fire))
        {
            re = 5;
        }
        if (GameStringHelper.differ(data.Attribute, (long)CardAttribute.Wind))
        {
            re = 2;
        }
        if (GameStringHelper.differ(data.Attribute, (long)CardAttribute.Dark))
        {
            re = 4;
        }
        if (GameStringHelper.differ(data.Attribute, (long)CardAttribute.Light))
        {
            re = 1;
        }
        if (GameStringHelper.differ(data.Attribute, (long)CardAttribute.Divine))
        {
            re = 1;
        }
        //
        return re;
    }

    Vector3 get_verticle_drawing_vector(Vector3 facevector)
    {
        Vector3 want_position = Vector3.zero;
        if (isMinBlockMode) 
        {
            want_position = facevector;
            want_position.y += (4.2f / loaded_verticalDrawingK) / 2f * 0.5f;
            want_position.z += ((4.2f / loaded_verticalDrawingK) / 2f * 1.732f * 0.5f)-1.85f;
        }
        else
        {
            float showscale = Program.verticleScale;
            want_position = facevector;
            want_position.y += (showscale / loaded_verticalDrawingK) / 2f * 0.5f;
            want_position.z += ((showscale / loaded_verticalDrawingK) / 2f * 1.732f * 0.5f) - (showscale * 1.3f / 3.6f - 0.8f);
        }
        return want_position;
    }


    #endregion

    #region publicTools

    public void show_number(int number,bool add=false)  
    {
        if (add)
        {
            show_number(number_showing * 10 + number);
            return;
        }
        if (number == 0)
        {
            if (obj_number != null)
            {
                iTween.ScaleTo(obj_number, Vector3.zero, 0.3f);
                destroy(obj_number, 0.6f);
            }
        }
        else
        {
            if (obj_number == null)
            {
                obj_number = create(Program.I().mod_ocgcore_card_number_shower);
                obj_number.transform.GetComponent<TMPro.TextMeshPro>().text = number.ToString();
                obj_number.transform.localScale = Vector3.zero;
                iTween.ScaleTo(obj_number, new Vector3(1, 1, 1), 0.3f);
                iTween.RotateTo(obj_number, new Vector3(Program.tableauFrontX, 0, 0), 0.3f);
            }
            else if (number_showing != number)
            {
                iTween.ScaleTo(obj_number, Vector3.zero, 0.6f);
                destroy(obj_number, 0.6f);
                obj_number = create(Program.I().mod_ocgcore_card_number_shower);
                obj_number.transform.GetComponent<TMPro.TextMeshPro>().text = number.ToString();
                obj_number.transform.localScale = Vector3.zero;
                iTween.ScaleTo(obj_number, new Vector3(1, 1, 1), 0.3f);
                iTween.RotateTo(obj_number, new Vector3(Program.tableauFrontX, 0, 0), 0.3f);
            }
        }
        number_showing = number;
    }

    #endregion

    #region button

    List<gameButton> buttons = new List<gameButton>();

    /// <summary>
    /// 排查用：看这张卡当前挂了哪些选项按钮。
    /// 验收脚本靠它拿到「可点选项」的屏幕坐标（见 Ocgcore.optionDumpTick）——
    /// 按钮是挂在卡片上的，位置随盘面与窗口尺寸变，脚本没法靠猜坐标点中。
    /// 只读，不参与任何逻辑。
    /// </summary>
    public List<gameButton> allButtons
    {
        get { return buttons; }
    }

    public void add_one_button(gameButton b)    
    {
        b.cookieCard = this;
        buttons.Add(b);
    }

    public bool query_hint_button(string hint)
    {
        for (int i = 0; i < buttons.Count; i++)
        {
            if (buttons[i].hint == hint)
            {
                return true;
            }
        }
        return false;
    }

    public void remove_all_cookie_button()
    {   
        List<gameButton> buttons_to_remove = new List<gameButton>();
        for (int i = 0; i < buttons.Count; i++)
        {
            if (buttons[i].notCookie == false)
            {
                buttons[i].hide();
                buttons_to_remove.Add(buttons[i]);
            }
        }
        for (int i = 0; i < buttons_to_remove.Count; i++)
        {
            buttons.Remove(buttons_to_remove[i]);
        }
        buttons_to_remove.Clear();
    }

    public void remove_all_unCookie_button()    
    {
        List<gameButton> buttons_to_remove = new List<gameButton>();
        for (int i = 0; i < buttons.Count; i++)
        {
            if (buttons[i].notCookie == true)
            {
                buttons[i].hide();
                buttons_to_remove.Add(buttons[i]);
            }
        }
        for (int i = 0; i < buttons_to_remove.Count; i++)
        {
            buttons.Remove(buttons_to_remove[i]);
        }
        buttons_to_remove.Clear();
    }

    #endregion

    #region decoration

    public class cardDecoration
    {
        public GameObject game_object;
        public float relative_position;
        public Vector3 rotation;
        public string desctiption;
        public bool scale_change_ignored = false;
        public bool cookie = true;
        public bool up_of_card = false;
    }

    List<cardDecoration> cardDecorations = new List<cardDecoration>();

    public cardDecoration add_one_decoration(GameObject mod, float relative_position, Vector3 rotation, string desctiption, bool cookie = true, bool up = false)
    {
        cardDecoration c = new cardDecoration();
        c.desctiption = desctiption;
        c.up_of_card = up;
        c.cookie = cookie;
        c.relative_position = relative_position;
        c.rotation = rotation;
        c.game_object = create(mod, gameObject_face.transform.position);
        c.game_object.transform.eulerAngles = rotation;
        c.game_object.transform.localScale = Vector3.zero;
        cardDecorations.Add(c);
        return c;
    }

    public void fast_decoration(GameObject mod)
    {
        destroy(add_one_decoration(mod, -0.5f, Vector3.zero, "",false).game_object, 5);
    }

    public void animationEffect(GameObject mod)
    {
        MonoBehaviour.Destroy((GameObject)MonoBehaviour.Instantiate(mod, UA_get_accurate_position(), Quaternion.identity), 5f);
    }

    public void positionEffect(GameObject mod)
    {
        MonoBehaviour.Destroy((GameObject)MonoBehaviour.Instantiate(mod, Program.I().ocgcore.get_point_worldposition(p), Quaternion.identity), 5f);
    }

    public void positionShot(GameObject mod)
    {
        MonoBehaviour.Destroy((GameObject)MonoBehaviour.Instantiate(mod, Program.I().ocgcore.get_point_worldposition(p), Quaternion.identity), 1f);
    }

    public void del_all_decoration_by_string(string desctiption)
    {
        List<cardDecoration> to_remove = new List<cardDecoration>();
        for (int i = 0; i < cardDecorations.Count; i++)
        {
            if (cardDecorations[i].desctiption == desctiption)
            {
                to_remove.Add(cardDecorations[i]);
                destroy(cardDecorations[i].game_object);
            }
        }
        for (int i = 0; i < to_remove.Count; i++)
        {
            cardDecorations.Remove(to_remove[i]);
        }
    }

    public void del_all_decoration()
    {
        List<cardDecoration> to_remove = new List<cardDecoration>();
        for (int i = 0; i < cardDecorations.Count; i++)
        {
            if (cardDecorations[i].game_object != null && cardDecorations[i].cookie)
            {
                to_remove.Add(cardDecorations[i]);
                destroy(cardDecorations[i].game_object);
            }
        }
        for (int i = 0; i < to_remove.Count; i++)
        {
            cardDecorations.Remove(to_remove[i]);
        }
    }

    #endregion

    #region overlay

    List<GameObject> overlay_lights = new List<GameObject>();

    /// <summary>
    /// 超量素材光点相对卡面的**世界偏移**。⛔⛔ 必须跟视角走，原来写死的是「世界正上方
    /// <c>(0, 1.8, 0)</c>」：
    /// ・60° 斜视角下世界 +y 在**屏幕上也是向上** ⇒ 光点浮在卡面之上（正常，就是用户说的
    ///   「像斜视角一样正常处于卡牌之上」）；
    /// ・正俯视（tilt 90）下世界 +y 是**朝相机的方向**、屏幕位移恰好为 0 ⇒ 光点与卡面完全重叠
    ///   —— 用户 2026-09-23 实测：「俯视角下卡牌上的光点没有像斜视角一样正常处于卡牌之上，
    ///   变成在卡牌中间了」。
    /// 定稿：俯视角下改用**垂直于视线**的那个方向（= 屏幕上方）= 世界 **+z**。
    /// ⛔ 方向不靠猜：`[view] proj` 实测投影标尺（`log/qt_*.log`）量得很清楚 ——
    ///   地面 z 越大、屏幕 y（自上而下计）越小，且 20.3 px/世界 ⇒ **+z 就是屏幕上方**。
    /// 长度仍是 1.8 ⇒ 屏上约 36.5 px，与斜视角那 0.9 世界（≈18px）同一量级观感。
    /// ⚠ 关态逐字节不变：仍然是 <c>(0, 1.8, 0)</c>。
    /// 提成静态量是为了让探针**只咬一处**（`[view] oloff` 直接投影它，判据不在脚本里重抄公式）。
    /// </summary>
    public static Vector3 overlayLightOffset
    {
        get { return Program.topDown ? new Vector3(0f, 0f, 1.8f) : new Vector3(0f, 1.8f, 0f); }
    }

    /// <summary>
    /// 探针用：把「卡面上方 h」的偏移与「卡片自己的半高」各投到屏幕上，回两个**像素长度之比**。
    ///
    /// 这个比值**与视角无关**（两者沿同一个世界方向、同一个投影缩放，见 `Program.cardUpOffset` 的注释），
    /// 所以「关态比值 == 开态比值」就是「卡上闪光在两个视角里处在卡面的同一位置」的等价说法 ——
    /// 探针 `[cardup]` 咬它（用户 2026-09-23：「没有像斜视角一样正常处于卡牌之上，变成在卡牌中间」）。
    /// </summary>
    public bool upOffsetPixelRatio(float h, out float ratio, out string diag)
    {
        ratio = -1f;
        diag = "";
        try
        {
            Transform f = gameObject_face.transform;
            Renderer rd = gameObject_face.GetComponent<Renderer>();
            if (rd == null || f == null)
            {
                return false;
            }
            Vector3 up = f.up;
            Bounds b = rd.bounds;
            // 卡片沿「自己的上方向」的半长（世界 AABB 在该方向上的支撑半径）
            float half = Mathf.Abs(up.x) * b.extents.x + Mathf.Abs(up.y) * b.extents.y + Mathf.Abs(up.z) * b.extents.z;
            Camera cam = Program.camera_game_main;
            Vector3 p0 = cam.WorldToScreenPoint(f.position);
            Vector3 p1 = cam.WorldToScreenPoint(f.position + up * half);
            Vector3 p2 = cam.WorldToScreenPoint(f.position + Program.cardUpOffset(h, half));
            float halfPx = new Vector2(p1.x - p0.x, p1.y - p0.y).magnitude;
            float upPx = new Vector2(p2.x - p0.x, p2.y - p0.y).magnitude;
            if (halfPx < 0.5f)
            {
                return false;
            }
            ratio = upPx / halfPx;
            diag = " up=(" + up.x.ToString("F2") + "," + up.y.ToString("F2") + "," + up.z.ToString("F2") + ")"
                + " half=" + half.ToString("F2")
                + " upPx=" + Mathf.RoundToInt(upPx) + " halfPx=" + Mathf.RoundToInt(halfPx);
            return true;
        }
        catch (System.Exception)
        {
            return false;
        }
    }

    public void add_one_overlay_light()
    {
        GameObject mod = Program.I().mod_ocgcore_ol_light;
        if (GameStringHelper.differ(data.Attribute, (long)CardAttribute.Earth))
        {
            mod = Program.I().mod_ocgcore_ol_earth;
        }
        if (GameStringHelper.differ(data.Attribute, (long)CardAttribute.Water))
        {
            mod = Program.I().mod_ocgcore_ol_water;
        }
        if (GameStringHelper.differ(data.Attribute, (long)CardAttribute.Fire))
        {
            mod = Program.I().mod_ocgcore_ol_fire;
        }
        if (GameStringHelper.differ(data.Attribute, (long)CardAttribute.Wind))
        {
            mod = Program.I().mod_ocgcore_ol_wind;
        }
        if (GameStringHelper.differ(data.Attribute, (long)CardAttribute.Dark))
        {
            mod = Program.I().mod_ocgcore_ol_dark;
        }
        if (GameStringHelper.differ(data.Attribute, (long)CardAttribute.Light))
        {
            mod = Program.I().mod_ocgcore_ol_light;
        }
        GameObject obj = create(mod, gameObject_face.transform.position);
        overlay_lights.Add(obj);
    }

    public void del_one_overlay_light()
    {
        if (overlay_lights.Count > 0)
        {
            destroy(overlay_lights[0]);
            overlay_lights.RemoveAt(0);
        }
    }


 
    public void set_overlay_light(int number)
    {
        if (number != 0)
        {
            if (loaded_verticalOverAttribute != data.Attribute)
            {
                loaded_verticalOverAttribute = data.Attribute;
                while (overlay_lights.Count > 0)
                {
                    del_one_overlay_light();
                }
            }
        }
        while (overlay_lights.Count != number)
        {
            if (number > overlay_lights.Count)
            {
                add_one_overlay_light();
            }
            if (number < overlay_lights.Count)
            {
                del_one_overlay_light();
            }
        }
    }

    public void set_overlay_see_button(bool on)
    {
        gameButton re = null;
        for (int i = 0; i < buttons.Count; i++)
        {
            if (buttons[i].cookieString == "see_overlay")
            {
                re = buttons[i];
            }
        }
        if (on)
        {
            if (re == null)
            {
                gameButton button = new gameButton(0, InterString.Get("查看素材"), superButtonType.see);
                button.cookieString = "see_overlay";
                button.notCookie = true;
                button.cookieCard = this;
                add_one_button(button);
            }
        }
        else
        {
            if (re != null)
            {
                remove_all_unCookie_button();
            }
        }
    }

    #endregion

    #region lines

    FlashingController[] insFlash(string color)
    {
        FlashingController[] ret = new FlashingController[2];
        ret[0] = insFlashONE(color);
        ret[1] = insFlashONE(color);
        ret[1].transform.localEulerAngles = new Vector3(180, 0, 0);
        return ret;
    }

    GameObject insKuang(GameObject mod)
    {
        GameObject ret = null;
        ret = Program.I().create(mod);
        ret.transform.SetParent(gameObject_face.transform, false);
        ret.transform.localScale = new Vector3(0.1f/3f,0.1f, 0.1f / 4f);
        ret.transform.localEulerAngles = new Vector3(90,0,0);
        return ret;
    }

    FlashingController insFlashONE(string color)
    {
        FlashingController flash = null;
        Program.camera_game_main.GetComponent<HighlightingEffect>().enabled = true;
        flash = Program.I().create(Program.I().mod_ocgcore_card_figure_line).GetComponent<FlashingController>();
        flash.transform.SetParent(gameObject_face.transform, false);
        flash.transform.localPosition = Vector3.zero;
        Color tcl = Color.yellow;
        ColorUtility.TryParseHtmlString(color, out tcl);
        flash.flashingStartColor = tcl;
        ColorUtility.TryParseHtmlString("000000", out tcl);
        flash.flashingEndColor = tcl;
        return flash;
    }

    public void flash_line_on()
    {
        Program.camera_game_main.GetComponent<HighlightingEffect>().enabled = true;
        if (MouseFlash==null)   
        {
            MouseFlash = create(Program.I().mod_ocgcore_card_figure_line).GetComponent<FlashingController>();
            MouseFlash.transform.SetParent(gameObject_face.transform, false);
            MouseFlash.transform.localPosition = Vector3.zero;
            Color tcl = Color.yellow;
            ColorUtility.TryParseHtmlString("ff8000", out tcl);
            MouseFlash.flashingStartColor = tcl;
            ColorUtility.TryParseHtmlString("ffffff", out tcl);
            MouseFlash.flashingEndColor = tcl;
        }
        MouseFlash.gameObject.SetActive(true);
    }

    public void flash_line_off()
    {
        if (MouseFlash != null)
        {
            MouseFlash.gameObject.SetActive(false);
        }
    }

    GameObject p_line = null;

    public void p_line_on()
    {
        Program.camera_game_main.GetComponent<HighlightingEffect>().enabled = true;
        if (p_line != null) destroy(p_line);
        p_line = create(Program.I().mod_ocgcore_card_figure_line);
        p_line.transform.SetParent(gameObject_face.transform, false);
        p_line.transform.localPosition = Vector3.zero;
        p_line.GetComponent<FlashingController>().flashingStartColor = Color.blue;
        p_line.GetComponent<FlashingController>().flashingEndColor = Color.gray;
        p_line.GetComponent<FlashingController>().flashingFrequency = 0.5f;
    }

    public void p_line_off()
    {
        if (p_line != null)
        {
            destroy(p_line);
            p_line = null;
        }
    }

    #endregion

    #region animation

    public void animation_confirm(Vector3 position, Vector3 rotation, float time_move, float time_still)
    {
        ES_lock(time_move + time_move + time_still);
        confirm_step_time_still = time_still;
        confirm_step_time_move = time_move;
        confirm_step_r = rotation;
        iTween[] iTweens = gameObject.GetComponents<iTween>();
        for (int i = 0; i < iTweens.Length; i++) MonoBehaviour.Destroy(iTweens[i]);
        iTween.MoveTo(gameObject, iTween.Hash(
                            "x", position.x,
                            "y", position.y,
                            "z", position.z,
                            "onupdate", (Action)RefreshFunction_decoration,
                            "oncomplete", (Action)confirm_step_2,
                            "time", confirm_step_time_move
                            ));
        iTween.RotateTo(gameObject, iTween.Hash(
                            "x", confirm_step_r.x,
                            "y", confirm_step_r.y,
                            "z", confirm_step_r.z,
                            "time", confirm_step_time_move
                            ));
    }

    public void animation_confirm_screenCenter(Vector3 rotation, float time_move, float time_still)   
    {
        ES_lock(time_move + time_move + time_still);
        confirm_step_time_still = time_still;
        confirm_step_time_move = time_move;
        confirm_step_r = rotation;
        iTween[] iTweens = gameObject.GetComponents<iTween>();
        for (int i = 0; i < iTweens.Length; i++) MonoBehaviour.DestroyImmediate(iTweens[i]);
        iTween.RotateTo(gameObject, iTween.Hash(
                            "x", confirm_step_r.x,
                            "y", confirm_step_r.y,
                            "z", confirm_step_r.z,
                             "easetype", iTween.EaseType.spring,
                            "onupdate", (Action)RefreshFunction_decoration, 
                            "oncomplete", (Action)confirm_step_2,
                            "time", confirm_step_time_move
                            ));
        var ttt = gameObject.AddComponent<screenFader>();
        ttt.from = gameObject.transform.position;
        ttt.time = time_move;
        ttt.deltaTimeCloseUp = 0;
        MonoBehaviour.Destroy(ttt, time_move + time_still);
    }

    Vector3 confirm_step_r = Vector3.zero;

    float confirm_step_time_still = 0;

    float confirm_step_time_move = 0;

    void confirm_step_2()
    {
        iTween.RotateTo(gameObject, iTween.Hash(
                            "x", confirm_step_r.x,
                            "y", confirm_step_r.y,
                            "z", confirm_step_r.z,
                            "onupdate", (Action)RefreshFunction_decoration,
                            "oncomplete", (Action)confirm_step_3,
                            "time", confirm_step_time_still
                            ));
    }

    void confirm_step_3()
    {
        iTween.RotateTo(gameObject, iTween.Hash(
                            "x", accurate_rotation.x,
                            "y", accurate_rotation.y,
                            "z", accurate_rotation.z,
                            "time", confirm_step_time_move
                            ));
        iTween.MoveTo(gameObject, iTween.Hash(
                            "x", accurate_position.x,
                            "y", accurate_position.y,
                            "z", accurate_position.z,
                            "onupdate", (Action)RefreshFunction_decoration,
                            "time", confirm_step_time_move,
                             "easetype", iTween.EaseType.easeInQuad
                            ));
    }

    public void animation_shake_to(float time)
    {
        ES_lock(time);
        gameObject.transform.position = accurate_position;
        gameObject.transform.eulerAngles = accurate_rotation;
        iTween[] iTweens = gameObject.GetComponents<iTween>();
        for (int i = 0; i < iTweens.Length; i++) MonoBehaviour.Destroy(iTweens[i]);
        iTween.ShakePosition(gameObject, iTween.Hash(
                            "x", 1,
                            "y", 1,
                            "z", 1,
                            "time", time,
                            "oncomplete", (Action)ES_safe_card_move_to_original_place
                            ));
    }


    public void animation_rush_to(Vector3 position, Vector3 rotation)
    {
        ES_lock(0.4f);
        iTween[] iTweens = gameObject.GetComponents<iTween>();
        for (int i = 0; i < iTweens.Length; i++)
            MonoBehaviour.Destroy(iTweens[i]);
        MonoBehaviour.DestroyImmediate(gameObject.GetComponent<screenFader>());
        iTween.MoveTo(gameObject, iTween.Hash(
                            "x", position.x,
                            "y", position.y,
                            "z", position.z,
                            "time", 0.2f
                            ));
        iTween.RotateTo(gameObject, iTween.Hash(
                            "x", rotation.x,
                            "y", rotation.y,
                            "z", rotation.z,
                             "onupdate", (Action)RefreshFunction_decoration,
                            "oncomplete", (Action)ES_safe_card_move_to_original_place,
                            "time", 0.21f
                            ));
    }

    public void animation_show_off(bool summon, bool disabled = false)   
    {
        if (Ocgcore.inSkiping) 
        {
            return;
        }

        show_off_disabled = disabled;
        show_off_begin_time = Program.TimePassed();
        show_off_shokewave = summon;

        if (show_off_disabled)
        {
            refreshFunctions.Add(SOH_dis);
            Program.I().ocgcore.Sleep(42);
        }
        else if (show_off_shokewave)
        {
            if (Program.I().setting.setting.showoff.value == false || File.Exists("picture/closeup/" + data.Id.ToString() + ".png") == false || (data.Attack < Program.I().setting.atk && data.Level < Program.I().setting.star))
            {
                refreshFunctions.Add(SOH_nSum);
                Program.I().ocgcore.Sleep(30);
            }
            else
            {
                refreshFunctions.Add(SOH_sum);
                Program.I().ocgcore.Sleep(72);
            }
        }
        else
        {
            if (Program.I().setting.setting.showoffWhenActived.value == false || File.Exists("picture/closeup/" + data.Id.ToString() + ".png") == false)
            {
                refreshFunctions.Add(SOH_nAct);
                Program.I().ocgcore.Sleep(42);
            }
            else
            {
                refreshFunctions.Add(SOH_act);
                Program.I().ocgcore.Sleep(42);
            }
        }
    }

    bool show_off_shokewave = false;

    bool show_off_disabled = false;

    int show_off_begin_time = 0;

    private void SOH_act()
    {
        Texture2D tex = GameTextureManager.get(data.Id, GameTextureType.card_picture);
        Texture2D texc = GameTextureManager.get(data.Id, GameTextureType.card_feature);
        if (tex != null)
        {
            if (texc != null)
            {
                float k = GameTextureManager.getK(data.Id, GameTextureType.card_feature);
                refreshFunctions.Remove(SOH_act);
                YGO1superShower shower = create(Program.I().Pro1_superCardShowerA, Program.I().ocgcore.centre(true), Vector3.zero, false, Program.ui_main_2d, true).GetComponent<YGO1superShower>();
                shower.card.mainTexture = tex;
                shower.closeup.mainTexture = texc;
                shower.closeup.height = (int)(500f / k);
                shower.closeup.width = (int)((500f / k) * ((float)texc.width) / ((float)texc.height));
                Ocgcore.LRCgo = shower.gameObject;
                destroy(shower.gameObject, 0.7f, false, true);
            }
        }
    }

    private void SOH_nAct()
    {
        Texture2D tex = GameTextureManager.get(data.Id, GameTextureType.card_picture);
        if (tex != null)
        {
            refreshFunctions.Remove(SOH_nAct);
            pro1CardShower shower = create(Program.I().Pro1_CardShower, Program.I().ocgcore.centre(), Vector3.zero, false, Program.ui_main_2d, true).GetComponent<pro1CardShower>();
            shower.card.mainTexture = tex;
            shower.mask.mainTexture = GameTextureManager.Mask;
            shower.disable.mainTexture = GameTextureManager.negated;
            shower.transform.localScale = Vector3.zero;
            shower.gameObject.transform.localScale = new Vector3(Screen.height / 650f, Screen.height / 650f, Screen.height / 650f);
            shower.run();
            Ocgcore.LRCgo = shower.gameObject;
            destroy(shower.gameObject, 0.7f, false, true);
        }
    }

    private void SOH_sum()
    {
        Texture2D tex = GameTextureManager.get(data.Id, GameTextureType.card_picture);
        Texture2D texc = GameTextureManager.get(data.Id, GameTextureType.card_feature);
        if (tex != null)
        {
            if (texc != null)
            {
                float k = GameTextureManager.getK(data.Id, GameTextureType.card_feature);
                refreshFunctions.Remove(SOH_sum);
                YGO1superShower shower = create(Program.I().Pro1_superCardShower, Program.I().ocgcore.centre(true), Vector3.zero, false, Program.ui_main_2d, true).GetComponent<YGO1superShower>();
                shower.card.mainTexture = tex;
                shower.closeup.mainTexture = texc;
                shower.closeup.height = (int)(500f / k);
                shower.closeup.width = (int)((500f / k) * ((float)texc.width) / ((float)texc.height));
                Ocgcore.LRCgo = shower.gameObject;
                destroy(shower.gameObject, 2f, false, true);
            }
        }
    }

    private void SOH_nSum()
    {
        Texture2D tex = GameTextureManager.get(data.Id, GameTextureType.card_picture);
        if (tex != null)
        {
            refreshFunctions.Remove(SOH_nSum);
            pro1CardShower shower = create(Program.I().Pro1_CardShower, Program.I().ocgcore.centre(), Vector3.zero, false, Program.ui_main_2d, true).GetComponent<pro1CardShower>();
            shower.card.mainTexture = tex;
            shower.mask.mainTexture = GameTextureManager.Mask;
            shower.disable.mainTexture = GameTextureManager.negated;
            shower.transform.localScale = Vector3.zero;
            iTween.ScaleTo(shower.gameObject, iTween.Hash(
               "scale",
               new Vector3(Screen.height / 650f, Screen.height / 650f, Screen.height / 650f),
               "time",
               0.5f
               ));
            Ocgcore.LRCgo = shower.gameObject;
            destroy(shower.gameObject, 0.5f, false, true);
        }
    }

    private void SOH_dis()  
    {
        Texture2D tex = GameTextureManager.get(data.Id, GameTextureType.card_picture);
        if (tex != null)
        {
            refreshFunctions.Remove(SOH_dis);
            pro1CardShower shower = create(Program.I().Pro1_CardShower, Program.I().ocgcore.centre(), Vector3.zero, false, Program.ui_main_2d, true).GetComponent<pro1CardShower>();
            shower.card.mainTexture = tex;
            shower.mask.mainTexture = GameTextureManager.Mask;
            shower.disable.mainTexture = GameTextureManager.negated;
            shower.transform.localScale = Vector3.zero;
            shower.gameObject.transform.localScale = new Vector3(Screen.height / 650f, Screen.height / 650f, Screen.height / 650f);
            shower.Dis();
            Ocgcore.LRCgo = shower.gameObject;
            destroy(shower.gameObject, 0.7f, false, true);
        }
    }

    public void sortButtons()
    {
        buttons.Sort((left, right) =>
        {
            return getButtonGravity(right) - getButtonGravity(left);
        });
    }

    int getButtonGravity(gameButton left)
    {
        gameButton button = left;
        int gravity = 0;
        switch (button.type)
        {
            case superButtonType.act:
                gravity = 1;
                break;
            case superButtonType.attack:
                gravity = 7;
                break;
            case superButtonType.change:
                gravity = 6;
                break;
            case superButtonType.see:
                gravity = 5;
                break;
            case superButtonType.set:
                gravity = 4;
                break;
            case superButtonType.spsummon:
                gravity = 2;
                break;
            case superButtonType.summon:
                gravity = 3;
                break;
        }

        return gravity;
    }

    #endregion

    #region cs

    public void ChainUNlock()
    {
        for (int i = 0; i < chains.Count; i++)  
        {
            if (chains[i].G != null)
            {
                Program.I().ocgcore.allChainPanelFixedContainer.Remove(chains[i].G.gameObject);
                chains[i].G.transform.SetParent(Program.I().transform,true);
            }
        }
    }

    void handlerChain()
    {
        for (int i = 0; i < chains.Count; i++)  
        {
            if (chains[i].G == null)
            {
                chains[i].G = create(Program.I().new_ocgcore_chainCircle).GetComponent<chainMono>();
                Program.I().ocgcore.allChainPanelFixedContainer.Add(chains[i].G.gameObject);
                chains[i].G.text.text = chains[i].i.ToString();
                chains[i].G.text.color = GameTextureManager.chainColor;
                chains[i].G.text.enableVertexGradient = false;
                chains[i].G.circle.material.mainTexture = GameTextureManager.Chain;
                chains[i].G.gameObject.transform.localScale = Vector3.zero;
                chains[i].G.flashing = false;
            }
            chainMono decorationChain = chains[i].G;
            if (game_object_verticle_drawing != null && Program.getVerticalTransparency() > 0.5f)
            {
                if (decorationChain.transform.parent != Program.I().transform)
                {
                    if (decorationChain.transform.parent != game_object_verticle_drawing.transform)
                    {
                        decorationChain.transform.SetParent(game_object_verticle_drawing.transform);
                        decorationChain.transform.localRotation = Quaternion.identity;
                        decorationChain.transform.localScale = Vector3.zero;
                        decorationChain.transform.localPosition = Vector3.zero;
                    }
                    try
                    {
                        Vector3 devide = game_object_verticle_drawing.transform.localScale;
                        if (Vector3.Distance(Vector3.zero, devide) > 0.01f)
                        {
                            decorationChain.transform.localScale = (new Vector3(5f / devide.x, 5f / devide.y, 5f / devide.z));
                        }
                    }
                    catch (Exception)
                    {
                    }
                }
                else
                {
                    decorationChain.transform.localScale = (new Vector3(5, 5, 5));
                }
            }
            else
            {
                if (decorationChain.transform.parent != Program.I().transform)
                {
                    if (decorationChain.transform.parent != gameObject_face.transform)
                    {
                        decorationChain.transform.SetParent(gameObject_face.transform);
                        decorationChain.transform.localRotation = Quaternion.identity;
                        decorationChain.transform.localScale = Vector3.zero;
                        decorationChain.transform.localPosition = Vector3.zero;
                    }
                    try
                    {
                        Vector3 devide = gameObject_face.transform.localScale;
                        if (Vector3.Distance(Vector3.zero, devide) > 0.01f)
                        {
                            decorationChain.transform.localScale = (new Vector3(5f / devide.x, 5f / devide.y, 5f / devide.z));
                        }
                    }
                    catch (Exception)
                    {
                    }
                }
                else
                {
                    decorationChain.transform.localScale = (new Vector3(5, 5, 5));
                }
            }
        }
        if (CS_ballIsShowed)    
        {
            if (Program.I().setting.setting.Vchain.value == true)
            {
                if (ballChain == null)
                {
                    ballChain = add_one_decoration(Program.I().mod_ocgcore_cs_chaining, 3, Vector3.zero, "chaining", false).game_object;
                    ballChain.GetComponent<slowFade>().yse = (condition != gameCardCondition.verticle_clickable || Program.getVerticalTransparency() < 0.5f);
                }
            }
        }
        else
        {
            if (ballChain!=null)    
            {
                del_all_decoration_by_string("chaining");
                Vector3 pos = UIHelper.get_close(gameObject.transform.position, Program.camera_game_main, 5);
                if (Program.I().setting.setting.Vchain.value == true)
                {
                    MonoBehaviour.Destroy((GameObject)MonoBehaviour.Instantiate(Program.I().mod_ocgcore_cs_end, pos, Quaternion.identity), 5f);
                }
                if (ballChain != null)  
                {
                    destroy(ballChain);
                }
                ballChain = null;
            }
        }
    }

    GameObject ballChain;

    public bool CS_ballIsShowed = false;

    public void CS_showBall()
    {
        CS_ballIsShowed = true;
        currentKuang = kuangType.chaining;
    }

    public void CS_hideBall()   
    {
        CS_ballIsShowed = false;
    }

    public void CS_ballToNumber()   
    {
        if (CS_ballIsShowed)
        {
            currentKuang = kuangType.chaining;
            CS_hideBall();
            CS_addChainNumber(1);
        }
    }

    List<chainMonoW> chains = new List<chainMonoW>();

    class chainMonoW
    {
        public chainMono G;
        public int i;
        //public Vector3 bornPosition = default(Vector3);
        //public Vector3 bornAngle = default(Vector3);
    }

    public void CS_addChainNumber(int i)
    {
        currentKuang = kuangType.chaining;
        chainMonoW w = new chainMonoW();
        w.i = i;
        w.G = null;
        chains.Add(w);
    }

    public void CS_removeOneChainNumber()   
    {
        if (chains.Count > 0)
        {
            chainMono decorationChain = chains[chains.Count - 1].G;
            if (decorationChain!=null)  
            {
                if (Program.I().ocgcore.inTheWorld())
                {
                    destroy(decorationChain.gameObject);
                }
                else
                {
                    decorationChain.flashing = true;
                    destroy(decorationChain.gameObject, 0.7f);
                }
            }
            chains.RemoveAt(chains.Count - 1);
            currentKuang = kuangType.none;
        }
    }

    public void CS_removeAllChainNumber()   
    {
        while (chains.Count>0)  
        {
            CS_removeOneChainNumber();
        }
    }

    public void CS_clear()
    {
        CS_hideBall();
        CS_removeAllChainNumber();
        currentKuang = kuangType.none;
    }

    #endregion

    public GPS p_beforeOverLayed;
    public int overFatherCount;

    /// <summary>
    /// 这张卡现在是不是被我按「极大怪兽大框」的倍率放大着（见
    /// <c>Ocgcore.realize</c> 里的 <c>RdMaximumTrioScale</c> 那一段）。
    ///
    /// 为什么要记一个标记、而不是每帧无脑写 scale：卡根的 scale 有自己的动画
    /// （创建时置零、随后由别人收到 1）。无脑写就会把那套动画踩掉 ⇒ **只碰我自己改过的卡**，
    /// 一旦它不再是三件之一（本体被打掉 / 撤回重开 / 送回卡组），立刻还原成 1。
    /// </summary>
    public bool rdMaxTrioScaled = false;

    /// <summary>
    /// 这张卡正被「RD 极限召唤三件同帧入场」的暂存按住（见 <c>Ocgcore.rdMaxApplyStageHold</c>）。
    ///
    /// 为什么要按：极限召唤的三张卡是**三条独立 MOVE** 送来的，每条都走 `TweenTo` 补间
    /// ⇒ 一前一后飞进场，看着像三次独立召唤。实测（log/qt_6128.log，我方那次）：
    ///   18.805 L(120150001) hand→MZ seq1 ｜ 18.946 R(120150003) hand→MZ seq3
    ///   19.125 L → Overlay ｜ 19.126 本体(120150002) hand→MZ seq2 ｜ 19.128 R → Overlay
    /// 现在把先到的部件按在原处，等本体那一条 MOVE 进来的**同一帧**一起放行 ⇒ 三张卡
    /// 从同一处、同一时长飞出去，同时落位（大框也是那一帧亮）。
    /// </summary>
    public bool rdMaxHeld = false;

    /// <summary>
    /// 按住：位置钉死在 <paramref name="keepAt"/>（snap，不起补间），并缩到不可见。
    ///
    /// ⚠ 缩到不可见而不是「留在原地不动」：此刻它已经不在手牌那一摞里了（`p.location`
    ///   是怪兽区），留在原地就是两张卡悬在手牌行上不动 —— 正是用户 2026-09-22 截图里
    ///   骂过的「悬浮在场地中间的两张卡」。缩到 0.001 视觉上等于没有，放行时 `UA_rdMaxRelease`
    ///   还原成 1（三件齐就立刻被 `applyRdMaximumTrioScale` 改成 1.45）。
    /// ⚠ 位置必须**每帧重钉**：卡根的 scale 有自己的创建动画，位置也可能被别的补间盯上。
    /// </summary>
    public void UA_rdMaxHold(Vector3 keepAt)
    {
        UA_give_position(keepAt);
        UA_flush_all_gived_witn_lock(true);     // rush ⇒ clearITWeen + 直接赋值，不飞
        rdMaxHeld = true;
        gameObject.transform.localScale = new Vector3(0.001f, 0.001f, 0.001f);
    }

    /// <summary>放行：清标记 + 还原成 1。位置由紧随其后的 `realize` 摆位循环给（于是三张同帧起飞）。</summary>
    public void UA_rdMaxRelease()
    {
        if (!rdMaxHeld)
        {
            return;
        }
        rdMaxHeld = false;
        gameObject.transform.localScale = Vector3.one;
    }
}
