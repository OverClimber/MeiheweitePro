using UnityEngine;
using System;

/// <summary>
/// 卡组编辑器里的一张卡。
///
/// 实现口径 = MDPro3 的「UI 幽灵卡」：**完全不使用物理**。
///   · 位置：由 DeckManager.RowSlotPose 的摆位公式算出槽位，用 iTween 纯变换驱动；
///   · 顺序：由 DeckManager 的列表（deck.IMain / IExtra / ISide）决定；拖拽落点只用来
///     换算「插到第几位」（网格吸附），不再靠位置排序反推顺序；
///   · BoxCollider 保留 —— Program.Update 的拾取射线是 Physics.Raycast，要它才有命中。
///
/// 为什么不再用物理：卡是带 Rigidbody 的实体、落位后放开重力自由落体，
/// 而同一行相邻卡的碰撞盒在 x 向本来就有约 0.222 的重叠（间距 25/9=2.778、卡宽 3），
/// PhysX 会把它们互相顶开。顶开多少取决于求解器版本 —— 参考成品是 Unity 5.6.7f1
/// (PhysX 3.4)、本工程是 2021.3.45f1 (PhysX 4.x)，同一份代码同一份 .ydk 在两边的
/// 落定结果不同，于是「卡会落到不该在的格子」。改成幽灵卡后，落格与物理求解器彻底解耦。
/// </summary>
public class MonoCardInDeckManager : MonoBehaviour
{
    int loadedPicCode = 0;
    YGOSharp.Banlist loaded_banlist = null;
    public bool dying = false;
    bool died = false;
    public YGOSharp.Card cardData = new YGOSharp.Card();

    /// <summary>
    /// 这张卡被摆位公式指定的槽位（世界坐标）。只是记账：位置本身就由它驱动，
    /// 不需要再用它去纠正物理漂移（已经没有物理漂移了）。
    /// </summary>
    public Vector3 slotTarget;

    void Awake()
    {
        // 兜底：万一 prefab 上挂了 Rigidbody，把它冻成永不移动的静态体。
        // 不用 Destroy —— Destroy 要到帧末才生效，中间那一帧重力会先把它拽一下。
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.useGravity = false;
            rb.isKinematic = true;
            rb.constraints = RigidbodyConstraints.FreezeAll;
            rb.Sleep();
        }
        // 生成/飞行途中不参与拾取，落位完成（onLand）再打开。
        SetPickable(false);
    }

    void SetPickable(bool on)
    {
        BoxCollider box = GetComponent<BoxCollider>();
        if (box != null)
        {
            box.enabled = on;
        }
    }

    void Update()
    {
        if (loadedPicCode != cardData.Id)
        {
            Texture2D pic = GameTextureManager.get(cardData.Id, GameTextureType.card_picture);
            if (pic != null)
            {
                loadedPicCode = cardData.Id;
                gameObject.transform.Find("face").GetComponent<Renderer>().material.mainTexture = pic;
            }
        }
        if (Program.I().deckManager.currentBanlist != loaded_banlist)
        {
            ban_icon ico = GetComponentInChildren<ban_icon>();
            loaded_banlist = Program.I().deckManager.currentBanlist;
            if (loaded_banlist != null)
            {
                ico.show(loaded_banlist.GetQuantity(cardData.Id));
            }
            else
            {
                ico.show(3);
            }
        }
        if (isDraging)
        {
            // 幽灵卡跟手：鼠标射线与 y=4 水平面的交点做指数趋近。
            // 纯数学，没有刚体、没有力、没有碰撞。
            gameObject.transform.position += (getGoodPosition(4) - gameObject.transform.position) * 0.3f;
        }
    }

    /// <summary>把这张卡从卡组里拿掉（丢弃）。纯逻辑移除，不再用 AddForce 甩出去。</summary>
    public void killIt()
    {
        if (Program.I().deckManager.condition == DeckManager.Condition.changeSide)
        {
            // 换备界面不允许删卡：退回它自己的槽位，交给 DeckManager 重排。
            gameObject.transform.position = slotTarget;
            endDrag();
            if (Program.I().deckManager.cardInDragging == this)
            {
                Program.I().deckManager.cardInDragging = null;
            }
        }
        else
        {
            dying = true;
            died = true;
            gameObject.SetActive(false);
        }
    }

    /// <summary>
    /// 鼠标射线打在「高度 height 的水平面」上的世界坐标。
    /// 拖拽跟手与落点判定都用它 —— 纯几何，与物理无关。
    /// </summary>
    public Vector3 getGoodPosition(float height)
    {
        float x = Input.mousePosition.x;
        float y = Input.mousePosition.y;
        Vector3 to_ltemp = Program.camera_game_main.ScreenToWorldPoint(new Vector3(x, y, 1));
        Vector3 dv = to_ltemp - Program.camera_game_main.transform.position;
        if (dv.y == 0) dv.y = 0.01f;
        to_ltemp.x = ((height - Program.camera_game_main.transform.position.y)
            * (dv.x) / dv.y + Program.camera_game_main.transform.position.x);
        to_ltemp.y = ((height - Program.camera_game_main.transform.position.y)
            * (dv.y) / dv.y + Program.camera_game_main.transform.position.y);
        to_ltemp.z = ((height - Program.camera_game_main.transform.position.y)
            * (dv.z) / dv.y + Program.camera_game_main.transform.position.z);
        return to_ltemp;
    }

    bool isDraging = false;

    public void beginDrag()
    {
        isDraging = true;
        SetPickable(true);
        Program.go(1, () => { iTween.RotateTo(gameObject, new Vector3(90, 0, 0), 0.6f); });
    }

    public void endDrag()
    {
        isDraging = false;
    }

    /// <summary>
    /// 纯变换落位：把卡 tween 到槽位并摆正姿态。
    ///
    /// 与原实现的区别只有一处 —— 不再靠重力沉降（原来 tween 到一个较高的 y，
    /// 再放开重力让它自己掉到桌面）。现在直接 tween 到最终高度（含扇形排布的微抬高），
    /// 所以落点只由摆位公式决定，任何机器上结果都一样。
    /// </summary>
    public void tweenToVectorAndFall(Vector3 position, Vector3 rotation, float delay = 0)
    {
        slotTarget = position;
        SetPickable(false);
        iTween.MoveTo(gameObject, iTween.Hash(
                            "delay", delay,
                            "x", position.x,
                            "y", position.y,
                            "z", position.z,
                            "time", 0.25f,
                            "easeType", "easeOutQuad",
                            "oncomplete", (Action)onLand
                            ));
        iTween.RotateTo(gameObject, iTween.Hash(
                          "delay", delay,
                          "x", rotation.x,
                          "y", rotation.y,
                          "z", rotation.z,
                          "time", 0.2f
                          ));
    }

    void onLand()
    {
        SetPickable(true);
    }

    public bool getIfAlive()
    {
        bool ret = true;
        if (died == true)
        {
            ret = false;
        }
        if (gameObject.transform.position.y < -0.5f)
        {
            ret = false;
        }
        Vector3 to_ltemp = refLectPosition(gameObject.transform.position);
        if (to_ltemp.x < -15.2f) ret = false;
        if (to_ltemp.x > 15.2f) ret = false;

        if (Program.I().deckManager.condition == DeckManager.Condition.changeSide)
        {
            ret = true;
        }
        return ret;
    }

    /// <summary>把世界坐标投影到桌面平面（y=0），用于判断卡片是否被拖到桌面之外。</summary>
    public static Vector3 refLectPosition(Vector3 pos)
    {
        Vector3 to_ltemp = pos;
        Vector3 dv = to_ltemp - Program.camera_game_main.transform.position;
        if (dv.y == 0) dv.y = 0.01f;
        to_ltemp.x = ((0 - Program.camera_game_main.transform.position.y)
            * (dv.x) / dv.y + Program.camera_game_main.transform.position.x);
        to_ltemp.y = ((0 - Program.camera_game_main.transform.position.y)
            * (dv.y) / dv.y + Program.camera_game_main.transform.position.y);
        to_ltemp.z = ((0 - Program.camera_game_main.transform.position.y)
            * (dv.z) / dv.y + Program.camera_game_main.transform.position.z);
        return to_ltemp;
    }
}
