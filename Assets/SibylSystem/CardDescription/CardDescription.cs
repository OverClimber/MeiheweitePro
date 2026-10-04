using UnityEngine;
using System;
using System.Collections.Generic;
using YGOSharp.OCGWrapper.Enums;

public class CardDescription : Servant
{
    cardPicLoader picLoader;
    UIDragResize resizer;
    UITexture underSprite;
    UITexture picSprite;
    UISprite lineSprite;
    public UITextList description;
    GameObject line;
    UIWidget cardShowerWidget;
    UIPanel monitor;
    UIDeckPanel deckPanel;

    /// <summary>说明面板「译」钮的图标名（放 texture/ui/，与 ↑ ↓ A+ A− 同一套外挂贴图加载方式）。</summary>
    public const string TranslateIcon = "translate_card_shower";

    cardPicLoader[] quickCards = new cardPicLoader[300];

    public override void initialize()
    {
        gameObject = create
            (
            Program.I().new_ui_cardDescription,
            Program.camera_main_2d.ScreenToWorldPoint(new Vector3(-256, Screen.height /2, 600)),
            new Vector3(0, 0, 0),
            true,
            Program.ui_back_ground_2d
            );
        picLoader = gameObject.AddComponent<cardPicLoader>();
        picLoader.code = 0;
        picLoader.uiTexture = UIHelper.getByName<UITexture>(gameObject, "pic_");
        picLoader.loaded_code = -1;
        resizer = UIHelper.getByName<UIDragResize>(gameObject, "resizer");
        underSprite = UIHelper.getByName<UITexture>(gameObject, "under_");
        description = UIHelper.getByName<UITextList>(gameObject, "description_");
        cardShowerWidget = UIHelper.getByName<UIWidget>(gameObject, "card_shower");
        monitor = UIHelper.getByName<UIPanel>(gameObject, "monitor");
        deckPanel = gameObject.GetComponentInChildren<UIDeckPanel>();
        line = UIHelper.getByName(gameObject, "line");
        UIHelper.registEvent(gameObject,"pre_", onPre);
        UIHelper.registEvent(gameObject, "next_", onNext); 
        UIHelper.registEvent(gameObject, "big_", onb);
        UIHelper.registEvent(gameObject, "small_", ons);
        picSprite = UIHelper.getByName<UITexture>(gameObject, "pic_");
        lineSprite = UIHelper.getByName<UISprite>(gameObject, "line");
        try
        {
            description.textLabel.fontSize = int.Parse(Config.Get("fontSize","24"));
        }
        catch (System.Exception e)
        {
        }
        installNameTranslationButton();
        if (QuickTestTrace.Enabled)
        {
            // 验收自检：等装载都落定，把"面板上真正渲染出来的文本"量一次（见 onAcceptanceProbe）。
            Program.go(2000, onAcceptanceProbe);
        }
        read();
        myGraveStr = InterString.Get("我方墓地：");
        myExtraStr = InterString.Get("我方额外：");
        myBanishedStr = InterString.Get("我方除外：");
        opGraveStr = InterString.Get("[8888FF]对方墓地：[-]");
        opExtraStr = InterString.Get("[8888FF]对方额外：[-]");
        opBanishedStr = InterString.Get("[8888FF]对方除外：[-]");
        for (int i = 0; i < quickCards.Length; i++)
        {
            quickCards[i] = deckPanel.createCard();
            quickCards[i].relayer(i);
        }
        monitor.gameObject.SetActive(false);
    }

    public float width = 0;
    public float cHeight = 0;

    void onb()
    {
        description.textLabel.fontSize += 1;
        description.scrollValue = 0;
        description.Rebuild();
        Config.Set("fontSize", description.textLabel.fontSize.ToString());
    }

    void ons()
    {
        description.textLabel.fontSize -= 1;
        description.scrollValue = 0;
        description.Rebuild();
        Config.Set("fontSize", description.textLabel.fontSize.ToString());
    }

    void onPre()
    {
        current--;
        loadData();
    }

    void onNext()
    {
        current++;
        loadData();
    }

    public override void applyHideArrangement()
    {
        if (gameObject != null)
        {
            underSprite.height = Screen.height + 4;
            iTween.MoveTo(gameObject, Program.camera_main_2d.ScreenToWorldPoint(new Vector3(-underSprite.width-20, Screen.height / 2, 0)), 1.2f);
            setTitle("");
            resizer.gameObject.GetComponent<BoxCollider>().enabled = false;
        }
    }

    public override void applyShowArrangement()
    {
        if (gameObject != null)
        {
            underSprite.height = Screen.height + 4;
            iTween.MoveTo(gameObject, Program.camera_main_2d.ScreenToWorldPoint(new Vector3(-2, Screen.height / 2, 0)), 1.2f);
            resizer.gameObject.GetComponent<BoxCollider>().enabled = true;
            ensureNameTranslationButton();
        }
    }

    public void read()
    {
        try
        {
            var ca = int.Parse(Config.Get("CA", "230"));
            var cb = int.Parse(Config.Get("CB", "270"));
            if (cb > Screen.height)
            {
                // some dumb ass repack the program and set the pic size so large that small screen users can't realize there is card description under it.
                cb = Screen.height / 2;
                ca = (int)(cb * 0.68) + 50;
            }
            underSprite.width = ca;
            picSprite.height =  cb; 
        }
        catch (System.Exception e)
        {
        }
    }

    public void save()
    {
        Config.Set("CA", underSprite.width.ToString());
        Config.Set("CB", picSprite.height.ToString());
    }

    class data
    {
        public YGOSharp.Card card;
        public Texture2D def;
        public string tail = "";
    }

    int current = 0;

    List<data> datas = new List<data>();

    void loadData()
    {
        if (current < 0)
        {
            current = 0;
        }
        if (current > datas.Count - 1)
        {
            current = datas.Count - 1;
        }
        if (datas.Count == 0)
        {
            return;
        }
        data d = datas[current];
        apply(d.card, d.def, d.tail);
    }

    YGOSharp.Card currentCard = null;

    /// <summary>
    /// 现在摆在说明面板上的那张卡（没有卡就是 null）。
    /// 给「卡名翻译」入口用（NameTranslationUI 要拿它当改名的对象）。
    /// 做成**属性**而不是新字段：本工程对已有类的字段布局有讲究，属性不占序列化字段。
    /// </summary>
    public YGOSharp.Card showingCard
    {
        get { return currentCard; }
    }

    public bool ifShowingThisCard(YGOSharp.Card card)   
    {
        return currentCard == card;
    }

    private void apply(YGOSharp.Card card, Texture2D def, string tail)
    {
        if (card == null)
        {
            return;
        }
        string smallstr = "";
        if (card.Id != 0)
        {
            smallstr = GameStringHelper.getName(card) + GameStringHelper.getSmall(card);
            // 系列行（getSmall 末尾「系列：A|B」）每个系列名**分别**可点
            // （用户 2026-10-04）—— 只动那一段，其余字节不动。
            smallstr = CardTextLinker.LinkifySeriesLine(smallstr, card.Setcode);
            smallstr += "\n";
        }
        // 需求 1~4：描述里引用的卡名 / 字段 / 种类做成可点链接（富化结果按卡号缓存，
        // 换翻译（CardsManager.NameStamp 变）时整体作废，见 CardTextLinker）。
        string rich = CardTextLinker.Linkify(card.Desc, card.Id);
        if (tail == "")
        {
            description.Clear();
            description.scrollValue = 0;
            description.Add(smallstr + rich);
        }
        else
        {
            description.Clear();
            description.scrollValue = 0;
            description.Add(smallstr + "[FFD700]" + tail + "[-]" + rich);
        }
        picLoader.code = card.Id;
        picLoader.defaults = def;
        picLoader.loaded_code = -1;
        currentCard = card;
        shiftCardShower(true);
        Program.go(50, () => { shiftCardShower(true); });
    }

    /// <summary>
    /// 译名换过之后把面板上的卡**重新取一遍**（换全局译名 / 改了某张卡的外号时由
    /// NameTranslationUI 调）。
    ///
    /// ⚠ 必须重取，不能就地改名字：面板里存的是 CardsManager.Get(id) 给的**克隆**，
    /// 而换翻译只重算卡池里的原件（见 CardsManager.ApplyPool），克隆不会跟着变 ——
    /// 不重取的话，玩家会看到「检索列表已经是新名字、左边说明面板还是旧名字」。
    /// </summary>
    public void refreshCardNames()
    {
        for (int i = 0; i < datas.Count; i++)
        {
            data d = datas[i];
            if (d == null || d.card == null || d.card.Id <= 0)
            {
                continue;
            }
            YGOSharp.Card fresh = YGOSharp.CardsManager.Get(d.card.Id);
            if (fresh != null && fresh.Id > 0)
            {
                d.card = fresh;
            }
        }
        if (datas.Count > 0)
        {
            loadData();
        }
    }

    /// <summary>同列四颗钮（← → A+ A−）在 prefab 里的名字，钮列顺序即数组顺序。</summary>
    static readonly string[] SiblingButtons = { "pre_", "next_", "small_", "big_" };

    /// <summary>
    /// 在说明面板左侧那一列小钮（← → A+ A−，即 prefab 里的 pre_/next_/small_/big_）**最下面**
    /// 补一颗「译」钮 = 卡名翻译入口（需求 5）。
    ///
    /// 这颗钮必须是同列四颗的**同款**，否则一眼就看出是外来的。实机验收时的教训：
    /// 第一版只做了「能点」，画法跟旁边不成套 —— 图标用了近白色 (237,239,251) 的粗描边字 +
    /// 淡紫辉光、画在 160x160 画布上、塞进 40x40 的 widget、depth 68。实测屏上峰值
    /// (220,222,235)、覆盖率 86%，而同列四颗是 (107,120,138)、约 27% —— 亮一倍、密三倍，
    /// 看上去像贴上去的一张贴纸。现在按同列的画法重做：
    ///   * 钮身 = UIWidget（尺寸/锚点/深度全抄 big_），点按配色、音效、hinter 也全抄；
    ///   * 图标 = **子物体**上的 UITexture，26x26、depth 10 —— 那四颗钮的箭头就是这么画的
    ///     （prefab 里四个 "Texture" 子物体都是 26x26 / depth 10 / keepAspectRatio=Free，
    ///      共用同一张 Assets/ArtSystem/cardDescription/arrow.png，靠旋转 0/90/180/270 变向）；
    ///   * 图标贴图 = texture/ui/translate_card_shower.png，与 arrow.png 同一套画法
    ///     （125x103 画布、核心色 #B5CDFF、峰值 alpha 238、细笔画 + 淡辉光），由
    ///     _gen_translate_icon.py 生成；
    ///   * 位置 = 抄 big_ 的锚点后，沿钮列方向平移**一个运行期实测的间距**
    ///     （不写死数字：面板宽度玩家可拖，prefab 里存的是编辑器当时的值）。
    ///
    /// 为什么补钮而不是改 prefab：prefab 是烘焙资源，手改风险大，而「运行时补一颗钮」
    /// 是本工程用惯的做法（见 DeckManager.createTestButton、ViewToggleButton）。
    ///
    /// 顺带把说明文字上那个 NGUI 示例脚本 OpenURLOnClick 换成自己的链接处理器 ——
    /// 描述里现在有可点链接了，而那个示例会把链接当网址甩给浏览器。
    /// </summary>
    void installNameTranslationButton()
    {
        try
        {
            if (description != null && description.textLabel != null)
            {
                GameObject label = description.textLabel.gameObject;
                OpenURLOnClick old = label.GetComponent<OpenURLOnClick>();
                if (old != null)
                {
                    UnityEngine.Object.Destroy(old);
                }
                if (label.GetComponent<CardLinkClick>() == null)
                {
                    label.AddComponent<CardLinkClick>();
                }
                // 悬停提亮（用户 2026-10-04）：可点的字移上去要有亮度变化。
                // 与 CardLinkClick 同一对象、同一套 [url=] 命中口径。
                if (label.GetComponent<CardLinkHover>() == null)
                {
                    label.AddComponent<CardLinkHover>();
                }
            }

            // 面板上其余旧式提示（pre_/next_/small_/big_ 若 prefab 给了 hinter）
            // 一并换成贴边跟随式。放在这颗钮自己的提示处理**之前**也无所谓——幂等。
            ConvertHintsToEdge();

            GameObject src = UIHelper.getByName(gameObject, "big_");
            if (src == null || UIHelper.getByName(gameObject, NameTranslationUI.ButtonName) != null)
            {
                return;
            }
            UIRect srcRect = src.GetComponent<UIRect>();
            UIWidget srcWidget = src.GetComponent<UIWidget>();

            // 钮距 = big_ 与它在钮列里前一颗钮的**运行期**实际间距。
            // 写死数字是不行的：面板宽度玩家可拖（resizer），prefab 里存的 localPosition
            // 是编辑器当时的值，运行期锚点会解到别的位置上去。
            Vector3 pitch = measureSiblingPitch(src);

            // ⚠ 造法必须是 **Instantiate 克隆同列那颗 big_**，不能自己 new 一个 GameObject 再挂
            //   UITexture/BoxCollider/UIButton —— 这是用户实测「点一下贴图就没了、以后再也不出现」
            //   的真凶（2026-10-02）。工程里所有运行期补的钮都是这么造的：
            //   DeckManager.createTestButton（439 行）、gameButton.show（33 行），
            //   全是 Instantiate 一颗现成能用的钮。克隆把 NGUI 认得的那一套整体带过来：
            //   碰撞体、面板归属（UIPanel 靠它把控件收进去 —— 没有面板 = 一个像素都不画）、
            //   UIButton 配色、点按音效、UIPlayAnimation。自己 new 出来的那些控件面板不认，
            //   于是显示一阵、点一下就没了，而且因为只在 initialize 里建一次，再也补不回来。
            GameObject btn = (GameObject)UnityEngine.Object.Instantiate(src.gameObject);
            btn.name = NameTranslationUI.ButtonName;
            btn.transform.SetParent(src.transform.parent, false);

            // 克隆体会把源钮运行时挂进 UIEventTrigger / hinter 的委托一并复制过来，
            // 于是悬停提示一次造两个、出悬停只消一个（createTestButton 445 行记的坑）。
            UIEventTrigger trig = btn.GetComponent<UIEventTrigger>();
            if (trig != null)
            {
                trig.onHoverOver.Clear();
                trig.onHoverOut.Clear();
                trig.onPress.Clear();
            }
            UIButton ub = btn.GetComponent<UIButton>();
            if (ub != null)
            {
                ub.onClick.Clear();
            }

            // 位置：抄 big_ 的锚点后往钮列末端挪一格（间距运行期量，见 measureSiblingPitch）。
            UIRect bodyRect = btn.GetComponent<UIRect>();
            UIWidget body = btn.GetComponent<UIWidget>();
            if (bodyRect != null && srcRect != null)
            {
                // 深度接在同列之后（实测同列是 60/62/64/66，译钮 68；图标 61/63/65/67，译钮 69）
                if (body != null && srcWidget != null)
                {
                    body.depth = srcWidget.depth + 2;
                }
                CopyAnchor(srcRect.leftAnchor, bodyRect.leftAnchor);
                CopyAnchor(srcRect.rightAnchor, bodyRect.rightAnchor);
                CopyAnchor(srcRect.bottomAnchor, bodyRect.bottomAnchor);
                CopyAnchor(srcRect.topAnchor, bodyRect.topAnchor);
                int dx = Mathf.RoundToInt(pitch.x);
                int dy = Mathf.RoundToInt(pitch.y);
                bodyRect.leftAnchor.absolute += dx;
                bodyRect.rightAnchor.absolute += dx;
                bodyRect.bottomAnchor.absolute += dy;
                bodyRect.topAnchor.absolute += dy;
                bodyRect.ResetAndUpdateAnchors();
            }
            else
            {
                btn.transform.localPosition = src.transform.localPosition + pitch;
            }

            UIHelper.registEvent(gameObject, NameTranslationUI.ButtonName, onNameTranslationClicked);

            // 提示改**贴边跟随式**（用户 2026-10-04：「卡牌简介里的提示也改成战斗
            // 俯视角那一套」）。旧式 hinter 的两宗罪见 hinterEdge 类注释：
            // 固定摆控件上方 45px（贴屏顶缘整条出屏）、归通用面板（会被别的窗压住）。
            hinter oldHint = btn.GetComponent<hinter>();
            if (oldHint != null)
            {
                UIEventTrigger t3 = btn.GetComponent<UIEventTrigger>();
                if (t3 != null)
                {
                    t3.onHoverOver.Clear();
                    t3.onHoverOut.Clear();
                    t3.onPress.Clear();
                }
                UnityEngine.Object.Destroy(oldHint);
            }
            hinterEdge hint = btn.GetComponent<hinterEdge>();
            if (hint == null)
            {
                hint = btn.AddComponent<hinterEdge>();
            }
            hint.str = "卡名翻译";
            hint.translate = true;

            UITexture srcTex = src.GetComponentInChildren<UITexture>(true);
            // 图标：**直接用克隆体自带的那个 "Texture" 子物体**（就是同列四颗的箭头本体），
            // 只把贴图换成「译」，并把箭头为指向上而转的 +90° 摆回 0。
            // 这样图标 widget 的尺寸/锚点/keepAspectRatio/depth 天然与同列四颗一模一样，
            // 不用抄任何数字，也不会因为抄错而跟邻居对不上。
            // 贴图走 texture/ui + UITexture.path（那四颗的 mOutPath 是
            // "arrow_in_card_shower"），客户端更新刷过 texture/ui 一样会自动生效。
            UITexture tex = btn.GetComponentInChildren<UITexture>(true);
            GameObject iconGo;
            if (tex != null)
            {
                iconGo = tex.gameObject;
                tex.transform.localRotation = Quaternion.identity;
                tex.transform.localPosition = Vector3.zero;
                tex.ResetAndUpdateAnchors();
            }
            else
            {
                // 源钮上没找到 UITexture（prefab 改过结构）：补一颗兜底。
                iconGo = new GameObject("Texture");
                iconGo.layer = btn.layer;
                iconGo.transform.SetParent(btn.transform, false);
                tex = iconGo.AddComponent<UITexture>();
                tex.pivot = UIWidget.Pivot.Center;
                tex.keepAspectRatio = UIWidget.AspectRatioSource.Free;
                tex.SetDimensions(40, 40);
                tex.depth = (srcWidget != null ? srcWidget.depth : 60) + 3;
                tex.leftAnchor.target = null;
                tex.leftAnchor.relative = 0f;
                tex.rightAnchor.target = null;
                tex.rightAnchor.relative = 1f;
                tex.bottomAnchor.target = null;
                tex.bottomAnchor.relative = 0f;
                tex.topAnchor.target = null;
                tex.topAnchor.relative = 1f;
                tex.ResetAndUpdateAnchors();
            }
            // 深度接在同列之后（实测同列是 60/62/64/66 与 61/63/65/67，译钮 68 / 69）
            if (srcTex != null)
            {
                tex.depth = srcTex.depth + 2;
            }
            tex.color = srcTex != null ? srcTex.color : Color.white;

            // 图标缺了（比如客户端更新把 texture/ui 刷掉了）就退回「描边译字」画法 ——
            // 两种情况下钮都在，绝不会变成一颗点得动但看不见的空方块。
            Texture2D icon = GameTextureManager.get(TranslateIcon);
            bool iconOk = icon != null;
            if (iconOk)
            {
                tex.path = TranslateIcon;
                tex.mainTexture = icon;
            }
            else
            {
                tex.path = "";
            }

            if (!iconOk)
            {
                // 兜底画法：借说明文字的字体，加描边 —— 底下是卡图，不加描边看不清。
                GameObject lab = new GameObject("lab");
                lab.layer = btn.layer;
                lab.transform.SetParent(iconGo.transform, false);
                lab.transform.localPosition = Vector3.zero;
                UILabel l = lab.AddComponent<UILabel>();
                UILabel tpl = description != null ? description.textLabel : null;
                if (tpl != null)
                {
                    l.bitmapFont = tpl.bitmapFont;
                    l.trueTypeFont = tpl.trueTypeFont;
                }
                l.text = "译";
                l.fontSize = 20;
                l.alignment = NGUIText.Alignment.Center;
                l.pivot = UIWidget.Pivot.Center;
                l.overflowMethod = UILabel.Overflow.ResizeFreely;
                l.effectStyle = UILabel.Effect.Outline;
                l.effectColor = new Color(0f, 0f, 0f, 0.85f);
                l.effectDistance = new Vector2(1f, 1f);
                l.color = Color.white;
                l.depth = tex.depth + 1;
            }

            QuickTestTrace.Log("nametrans", "button installed name=" + btn.name
                + " icon=" + (iconOk ? TranslateIcon : "(缺,退回译字)")
                + " body=" + body.width + "x" + body.height + " depth=" + body.depth
                + " iconWidget=" + tex.width + "x" + tex.height + " depth=" + tex.depth
                + " pitch=" + pitch.x + "," + pitch.y
                + " localPos=" + btn.transform.localPosition);
            probeButtonColumn();
        }
        catch (Exception e)
        {
            Program.DEBUGLOG(e);
        }
    }

    /// <summary>
    /// 「译」钮自愈：每次说明面板要显示的时候都过一遍。
    ///
    /// 为什么需要（用户实测 2026-10-02）：**点一次这颗钮，它就再也不出现**。
    /// 它是运行期 new 出来的（只在本方法里建一次），所以只要有任何东西在点击之后
    /// 把贴图引用、面板归属或激活状态弄掉，就再也没有第二次机会补回来 ——
    /// 同列那四颗是 prefab 自带的，所以不会犯这个毛病。
    /// 这里逐项复查并就地修复：对象在不在、图标在不在、贴图还在不在、
    /// 有没有归到面板上、alpha 是不是 0、点击回调有没有被清掉。
    /// 幂等且很便宜（几个 null 判断），正常情况下什么都不改。
    /// </summary>
    void ensureNameTranslationButton()
    {
        try
        {
            GameObject btn = UIHelper.getByName(gameObject, NameTranslationUI.ButtonName);
            if (btn == null)
            {
                // 对象整个没了（被 destroy / removeAll 收走）：重建一颗。
                installNameTranslationButton();
                return;
            }
            if (!btn.activeSelf)
            {
                btn.SetActive(true);
            }
            UITexture tex = btn.GetComponentInChildren<UITexture>(true);
            if (tex == null)
            {
                // 图标子物体没了：整颗重建，别做半吊子修补。
                btn.transform.SetParent(null, false);
                UnityEngine.Object.Destroy(btn);
                installNameTranslationButton();
                return;
            }
            if (tex.mainTexture == null)
            {
                Texture2D again = GameTextureManager.get(TranslateIcon);
                if (again != null)
                {
                    tex.path = TranslateIcon;
                    tex.mainTexture = again;
                }
            }
            if (!tex.gameObject.activeSelf)
            {
                tex.gameObject.SetActive(true);
            }
            if (tex.panel == null)
            {
                // 真凶：控件没归到任何 UIPanel。NGUI 只把控件画进它 mPanel 指到的那个面板，
                // 没有面板 = 一个像素都不画 —— 看上去就是「按钮的贴图没了，其它还在」。
                // 同列那四颗是 prefab 自带的，天生就带着面板归属，所以不会犯这个毛病；
                // 这颗是运行期 new 的，得自己保证它被面板收进去。重建最省事也最可靠。
                btn.transform.SetParent(null, false);
                UnityEngine.Object.Destroy(btn);
                installNameTranslationButton();
                return;
            }
            Color c = tex.color;
            if (c.a <= 0f)
            {
                c.a = 1f;
                tex.color = c;
            }
            UIButton ub = btn.GetComponent<UIButton>();
            if (ub != null && ub.onClick != null && ub.onClick.Count == 0)
            {
                // 回调被谁 Clear 掉了，重新挂回去。
                UIHelper.registEvent(gameObject, NameTranslationUI.ButtonName, onNameTranslationClicked);
            }
        }
        catch (Exception e)
        {
            Program.DEBUGLOG(e);
        }
    }

    /// <summary>
    /// 把本面板上所有旧式 <see cref="hinter"/> 换成贴边跟随式 <see cref="hinterEdge"/>
    /// （用户 2026-10-04：「卡牌简介里的提示也改成战斗俯视角那一套」）。
    ///
    /// <para>文案原样带走，并挂 <c>translate</c> 标志 —— prefab 带来的提示是
    /// **待翻译的键**，<see cref="hinter"/> 原本靠自己的 Update 等 <c>InterString.loaded</c>
    /// 再翻；hinterEdge 的同款开关补齐这一步，不等翻译表加载完也不会把键当成品字钉死。</para>
    ///
    /// <para>⛔ 换组件前先把 UIEventTrigger 上**指向旧组件的委托清掉**：
    /// Destroy 是帧末才生效，留着的话同一次悬停会同时命中两套回调
    /// （检索窗克隆 back_ 时踩过的同一个坑）。</para>
    ///
    /// <para>幂等：已经是 hinterEdge 的不动、hinter 换完就没了 ⇒ 反复调用零副作用
    /// （本方法挂在会自我治愈、反复跑的 <see cref="installNameTranslationButton"/> 里）。</para>
    /// </summary>
    void ConvertHintsToEdge()
    {
        try
        {
            // ⛔ Servant 不是 MonoBehaviour（gameObject 是个普通字段），
            //   必须显式从 gameObject 上取，不能像 MonoBehaviour 那样直接调。
            hinter[] olds = gameObject.GetComponentsInChildren<hinter>(true);
            for (int i = 0; i < olds.Length; i++)
            {
                hinter h = olds[i];
                if (h == null)
                {
                    continue;
                }
                string s = h.str;
                GameObject go = h.gameObject;
                UIEventTrigger trig = go.GetComponent<UIEventTrigger>();
                if (trig != null)
                {
                    trig.onHoverOver.Clear();
                    trig.onHoverOut.Clear();
                    trig.onPress.Clear();
                }
                UnityEngine.Object.Destroy(h);
                if (go.GetComponent<hinterEdge>() == null)
                {
                    hinterEdge he = go.AddComponent<hinterEdge>();
                    he.str = s;
                    he.translate = true;
                }
            }
            if (olds.Length > 0)
            {
                QuickTestTrace.Log("nametrans", "edgehints n=" + olds.Length);
            }
        }
        catch (Exception e)
        {
            Program.DEBUGLOG(e);
        }
    }

    /// <summary>
    /// 右键落在简介上（用户 2026-10-04 第二条）：如果点在**亮起的字段**上
    /// （系列行里的系列名，或效果正文里「」框起来的字段），就弹出**改这个字段译名**的输入框；
    /// 点在卡名/普通文字上什么都不做。
    ///
    /// <para>为什么走 <see cref="Servant"/> 这条事件而不是自己轮询：
    /// <c>Servant.preFrameFunction</c> 每帧把 <c>Program.InputGetMouseButtonDown_1</c>
    /// 派发到每个 Servant 的 <c>ES_mouseDownRight</c> —— 这是本工程的右键正规入口
    /// （检索窗原来就在这条上"右键收窗"，本轮已按用户要求去掉）。</para>
    /// </summary>
    public override void ES_mouseDownRight()
    {
        string r = TryRenameFieldUnderMouse(UICamera.lastWorldPosition, false);
        // 落一行读数：这条路上任何一道检查没过（没悬在简介上 / 没点在链接上 / 点的不是字段），
        // 屏幕上都是"什么都没发生" —— 没有这行，"右键没反应"到底是没被调到、还是被哪道闸挡了，
        // 只能靠猜（2026-10-04 用户实测「右键没有弹出改字段」时踩到）。
        if (QuickTestTrace.Enabled)
        {
            QuickTestTrace.Log("nametrans", "rightclick " + r);
        }
    }

    /// <summary>
    /// <see cref="ES_mouseDownRight"/> 的实体。返回一行可判的读数（验收探针用；
    /// 屏幕上的正式路径忽略返回值）。
    ///
    /// <para><paramref name="forced"/> = 探针模式：跳过"鼠标是否在本 label 上"这道检查
    /// （进程内伪造不了 <c>UICamera.hoveredObject</c>，只能直喂坐标）。</para>
    /// </summary>
    public string TryRenameFieldUnderMouse(Vector3 worldPos, bool forced)
    {
        try
        {
            if (!isShowed)
            {
                return "desc=hidden";
            }
            UILabel lab = description != null ? description.textLabel : null;
            if (lab == null)
            {
                return "no label";
            }
            if (!forced && !IsMouseOverLabel(lab))
            {
                return "not-over-desc";
            }
            int ordinal;
            string payload = CardTextLinker.ResolveClick(lab, worldPos, out ordinal);
            if (string.IsNullOrEmpty(payload))
            {
                return "no-link";
            }
            char kind;
            string arg;
            if (!CardTextLinker.Parse(payload, out kind, out arg))
            {
                return "parse-fail";
            }
            if (kind != CardTextLinker.KindSet)
            {
                // 卡名（n）/全文（q）/并集（u）/记述（R）/RD 条件（r）都不是"系列"，
                // 没有可改的译名 —— 安静放过，别弹一个改不了东西的框。
                return "not-field kind=" + kind;
            }
            int mask;
            int code;
            string title;
            if (!CardTextLinker.ParseSet(arg, out mask, out code, out title))
            {
                return "set-parse-fail";
            }
            string native = CardTextLinker.FieldNativeOf(code);
            if (string.IsNullOrEmpty(native))
            {
                return "no-native code=0x" + code.ToString("x");
            }
            NameTranslationUI.OpenFieldRename(native);
            return "rename field=[" + native + "] title=[" + title + "]";
        }
        catch (Exception e)
        {
            Program.DEBUGLOG(e);
            return "failed " + e.GetType().Name;
        }
    }

    /// <summary>鼠标现在是不是在简介那个 label 上（与悬停提亮同一口径）。</summary>
    static bool IsMouseOverLabel(UILabel lab)
    {
        try
        {
            GameObject h = UICamera.hoveredObject;
            Transform t = h != null ? h.transform : null;
            while (t != null)
            {
                if (t.gameObject == lab.gameObject)
                {
                    return true;
                }
                t = t.parent;
            }
        }
        catch (Exception)
        {
        }
        return false;
    }

    /// <summary>
    /// 验收用：把简介面板上提示组件的实际状态摊成一行 —— 「译」钮上挂的是哪一套、
    /// 文案是什么、翻译开关开没开，以及**整块面板还剩几颗旧式 hinter**
    /// （用户 2026-10-04：简介的提示也要改成战斗俯视角那一套）。
    /// </summary>
    public string ProbeEdgeHint()
    {
        try
        {
            GameObject b = UIHelper.getByName(gameObject, NameTranslationUI.ButtonName);
            int oldAll = gameObject.GetComponentsInChildren<hinter>(true).Length;
            int edgeAll = gameObject.GetComponentsInChildren<hinterEdge>(true).Length;
            if (b == null)
            {
                return "btn=none oldAll=" + oldAll + " edgeAll=" + edgeAll;
            }
            hinterEdge he = b.GetComponent<hinterEdge>();
            return "btn=有 hinter=" + b.GetComponents<hinter>().Length
                + " edge=" + b.GetComponents<hinterEdge>().Length
                + " str=[" + (he != null ? he.str : "") + "]"
                + " translate=" + (he != null && he.translate ? 1 : 0)
                + " oldAll=" + oldAll + " edgeAll=" + edgeAll;
        }
        catch (Exception e)
        {
            return "probe failed " + e.GetType().Name + ": " + e.Message;
        }
    }

    /// <summary>
    /// 验收用：在简介那颗「译」钮上**真悬停一次**，让 <see cref="hinterEdge"/> 把提示面板
    /// 建出来（它是 Update 里按"悬停中"惰性建的，不悬停屏幕上永远没有 ——
    /// 只判"组件在不在"是判一根头发）。
    ///
    /// <para>先把**当前**全场最高面板 depth 打出来（必须在提示建出**之前**取，
    /// 否则会把提示自己算进去），脚本再拿 <c>[tipedge] show</c> 行的 depth 跟它比。</para>
    /// </summary>
    public void ProbeHoverEdgeHint()
    {
        try
        {
            GameObject b = UIHelper.getByName(gameObject, NameTranslationUI.ButtonName);
            if (b == null)
            {
                QuickTestTrace.Log("serieslink", "H2 no button");
                return;
            }
            hinterEdge he = b.GetComponent<hinterEdge>();
            if (he == null)
            {
                QuickTestTrace.Log("serieslink", "H2 no edge hint on 译钮");
                return;
            }
            int maxD = 0;
            for (int i = 0; i < UIPanel.list.Count; i++)
            {
                if (UIPanel.list[i] != null && UIPanel.list[i].depth > maxD)
                {
                    maxD = UIPanel.list[i].depth;
                }
            }
            QuickTestTrace.Log("serieslink", "H2 maxPanelDepth=" + maxD
                + " str=[" + he.str + "]");
            he.ProbeHover();
            Program.go(90, () => { he.ProbeUnhover(); });
        }
        catch (Exception e)
        {
            QuickTestTrace.Log("serieslink", "H2 failed " + e.GetType().Name + ": " + e.Message);
        }
    }

    static void CopyAnchor(UIRect.AnchorPoint from, UIRect.AnchorPoint to)
    {
        to.target = from.target;
        to.relative = from.relative;
        to.absolute = from.absolute;
    }

    /// <summary>
    /// 量「同列钮的间距」：`last` 与钮列里紧挨着它、在它前头的那颗之间的运行期位移。
    ///
    /// 为什么必须运行期量、不能写死：
    ///   * prefab 里 pre_/next_/small_/big_ 存的是编辑器当时的 localPosition
    ///     （-80 / -28 / 27 / 79），但它们的锚点是相对面板尺寸解的，而面板宽度玩家可拖
    ///     （resizer + Config CA/CB），运行期解出来的位置跟 prefab 里不是一回事；
    ///   * 钮列方向本身也不该写死 —— 在 prefab 的局部空间里是 x，量出来是什么就是什么。
    /// 拿不到（组件缺失之类）就退回 big_ 的钮宽，至少不会叠在 ↑ 上面。
    /// </summary>
    /// <summary>
    /// 把钮列上每颗钮（钮身 + 图标 widget + 贴图）的实际规格打进日志。
    /// 工程里的 new_cardDescription.prefab 与客户端实际加载的那份**并不一致**
    /// （实测 prefab 里 big_ 的 UIWidget 是 54x39/depth 0，运行期却是 40x40/depth 66），
    /// 所以「同款」只能以运行期量到的为准。
    /// </summary>
    void probeButtonColumn()
    {
        try
        {
            foreach (string n in SiblingButtons)
            {
                GameObject o = UIHelper.getByName(gameObject, n);
                if (o == null)
                {
                    continue;
                }
                UIWidget w = o.GetComponent<UIWidget>();
                string body = w != null ? (w.width + "x" + w.height + "@d" + w.depth) : "(none)";
                string icon = "(none)";
                UITexture t = o.GetComponentInChildren<UITexture>(true);
                if (t != null)
                {
                    Texture tx = t.mainTexture;
                    icon = t.width + "x" + t.height + "@d" + t.depth
                        + " tex=" + (tx != null ? tx.name : "null")
                        + (tx != null ? "(" + tx.width + "x" + tx.height + ")" : "")
                        + " rot=" + ((int)t.transform.localEulerAngles.z);
                }
                QuickTestTrace.Log("nametrans", "colprobe " + n
                    + " body=" + body + " icon=" + icon
                    + " pos=" + o.transform.localPosition);
            }
            GameObject me = UIHelper.getByName(gameObject, NameTranslationUI.ButtonName);
            if (me != null)
            {
                UITexture t = me.GetComponentInChildren<UITexture>(true);
                Texture tx = t != null ? t.mainTexture : null;
                QuickTestTrace.Log("nametrans", "colprobe translate_"
                    + " icon=" + (t != null ? t.width + "x" + t.height + "@d" + t.depth : "-")
                    + " tex=" + (tx != null ? tx.name + "(" + tx.width + "x" + tx.height + ")" : "null")
                    + " pos=" + me.transform.localPosition);
            }
        }
        catch (Exception e)
        {
            QuickTestTrace.Log("nametrans", "colprobe failed " + e.Message);
        }
    }

    Vector3 measureSiblingPitch(GameObject last)
    {
        try
        {
            Transform lastT = last.transform;
            Transform best = null;
            float bestDist = float.MaxValue;
            Vector3 lastPos = lastT.localPosition;
            foreach (string n in SiblingButtons)
            {
                if (n == last.name)
                {
                    continue;
                }
                GameObject o = UIHelper.getByName(gameObject, n);
                if (o == null || o.transform.parent != lastT.parent)
                {
                    continue;
                }
                float d = (o.transform.localPosition - lastPos).magnitude;
                if (d < bestDist)
                {
                    bestDist = d;
                    best = o.transform;
                }
            }
            if (best != null && bestDist > 0.5f)
            {
                return lastPos - best.localPosition;
            }
        }
        catch (Exception e)
        {
            Program.DEBUGLOG(e);
        }
        UIWidget w = last.GetComponent<UIWidget>();
        return new Vector3(w != null ? w.width : 54, 0f, 0f);
    }

    /// <summary>
    /// 只在 log/qt_debug.on 存在时跑一次的验收探针。
    /// 要回答的问题只有一个：说明面板的 UILabel 最终拿到的文本里，还有没有 [url=…] 和高亮色 ——
    /// 卡文是先经 UITextList 包行、再塞给 UILabel 的，标签会不会在中途被吃掉，
    /// 光读源码不够稳妥，让游戏自己说一遍。
    /// </summary>
    void onAcceptanceProbe()
    {
        try
        {
            CardTextLinker.SelfTest();
            CardTextLinker.RdSelfTest();
            CardSearchWindow.RdQuerySelfTest();
            nameTranslationProbe();
            aliasRenameProbe();
            fieldRenameProbe();
            Program.go(600, buttonClickProbe);

            YGOSharp.Card probe = YGOSharp.CardsManager.Get(65518099);
            if (probe == null || probe.Id <= 0)
            {
                QuickTestTrace.Log("link", "panel probe card missing");
                return;
            }
            // 换一张**描述里真的带「X」卡 / 组合种类**的卡来显示，这样截图里能直接肉眼看到
            // 「卡」被一起高亮、以及组合种类的前后半截是同一个链接。选不到就退回原卡。
            YGOSharp.Card showCase = FindHighlightShowcase(probe);
            if (showCase != null)
            {
                probe = showCase;
            }
            setData(probe, GameTextureManager.myBack, "", true);
            // 面板现在真有卡了，才能照玩家那条路径验「打开改名框→什么都不改→确定」。
            NameTranslationUI.ProbeUnchangedSubmit();
            // 让面板真的滑出来 —— 验收时肉眼 + 截图都能看见高亮，而不是只量字符串。
            show();
            string shown = (description != null && description.textLabel != null)
                ? description.textLabel.text : null;
            // 需求 2/3/4 的检索口径自检：同一个关键字，加"只留怪兽"的掩码必须命中更少；
            // "的卡名记述"要能查到东西（说明整句检索真的落在描述上）。
            // 直接借检索窗的 RunQuery（与玩家点链接走的是同一条），只量条数、不弹窗。
            QuickTestTrace.Log("link", "searchprobe 机壳 mask0=" + CardSearchWindow.RunQuery("机壳", 0).Count
                + " mask1=" + CardSearchWindow.RunQuery("机壳", 1).Count
                + " 机壳工具 mask0=" + CardSearchWindow.RunQuery("机壳工具", 0).Count
                + " 记述 mask0=" + CardSearchWindow.RunQuery("的卡名记述", 0).Count
                + " nativeMask1=" + CardSearchWindow.RunQuery("黑魔术师", 1).Count
                + " heroMon=" + CardSearchWindow.RunQuery("英雄", 1).Count
                + " heroFusion=" + CardSearchWindow.RunQuery("英雄", 65).Count);
            // 「卡名跳转」两档的自检（用户 2026-10-03）：卡名**精确相等** / 卡文里**记述了**它。
            // 逐档报张数与名字，并自带硬判据（exactAllSameName / recordAllMentioned）——
            // 那两档的分别就在「算不算同名 / 记没记述」，人眼是看不出来的。
            CardSearchWindow.CardNameLinkSelfTest();
            // 「返回上一级」的自检：连着点两处不同的链接（第二次会记下第一次），
            // 再退一级 —— 必须退回第一次的条件，而不是把窗关掉。
            CardSearchWindow.ShowQuery("机壳", 1);
            CardSearchWindow.ShowQuery("英雄", 65);
            CardSearchWindow cs = Program.I().cardSearch;
            // ── 详情版排版（需求 2）的硬判据 ────────────────────────────────
            // ⛔ 必须**在窗口正开着**这一刻量：ShowQuery 刚跑完，detailApplied 已生效。
            //   后面那条 btnprobe 里也量过一次，但那时 goBack 已经把窗关了 ⇒ 四个读数全 "off"，
            //   等于什么都没验（上一轮就是这么漏掉的）。
            // 期望：input=0 inputLabel=0 search=0 detailed=0，back=1。
            if (cs != null)
            {
                GameObject dw = cs.ProbeWindowObject;
                // ⛔ 用 FindDeep（含失活）：详情版把那三颗**藏掉**了，UIHelper.getByName 查不到
                //   ⇒ 上一轮四条读数全是 "missing"，等于什么都没验。
                GameObject inG = CardSearchWindow.FindDeep(dw, "input_");
                GameObject inL = CardSearchWindow.FindDeep(dw, "LabelOfInput");
                GameObject seG = CardSearchWindow.FindDeep(dw, "search_");
                GameObject deG = CardSearchWindow.FindDeep(dw, "detailed_");
                GameObject bkG = CardSearchWindow.FindDeep(dw, CardSearchWindow.BackButtonName);
                string rct = "n/a";
                UITexture btex = bkG != null ? bkG.GetComponentInChildren<UITexture>(true) : null;
                if (btex != null && Program.camera_main_2d != null)
                {
                    Vector3[] wc = btex.worldCorners;
                    Vector3 c0 = Program.camera_main_2d.WorldToScreenPoint(wc[0]);
                    Vector3 c2 = Program.camera_main_2d.WorldToScreenPoint(wc[2]);
                    rct = "(" + Mathf.RoundToInt(Mathf.Min(c0.x, c2.x)) + ","
                        + Mathf.RoundToInt(Mathf.Min(c0.y, c2.y)) + ")-("
                        + Mathf.RoundToInt(Mathf.Max(c0.x, c2.x)) + ","
                        + Mathf.RoundToInt(Mathf.Max(c0.y, c2.y)) + ")";
                }
                QuickTestTrace.Log("btnprobe", "detailprobe win="
                    + (dw == null ? "NULL" : (dw.activeInHierarchy ? "on" : "off"))
                    + " input=" + (inG == null ? "missing" : (inG.activeSelf ? "1" : "0"))
                    + " inputLabel=" + (inL == null ? "missing" : (inL.activeSelf ? "1" : "0"))
                    + " search=" + (seG == null ? "missing" : (seG.activeSelf ? "1" : "0"))
                    + " detailed=" + (deG == null ? "missing" : (deG.activeSelf ? "1" : "0"))
                    + " back=" + (bkG == null ? "missing" : (bkG.activeSelf ? "1" : "0"))
                    + " backPanel=" + (btex != null && btex.panel != null ? btex.panel.name : "none")
                    + " backTex=" + (btex != null && btex.mainTexture != null ? btex.mainTexture.name : "NULL")
                    + " backRect=" + rct);
                QuickTestTrace.Log("btnprobe", cs.ProbeBackDeep);
            }
            int backDepthBefore = cs != null ? cs.ProbeHistoryDepth : -1;
            string backAtBefore = cs != null ? cs.ProbeLastQuery : "?";
            int backMaskBefore = cs != null ? cs.ProbeLastMask : -1;
            if (cs != null)
            {
                cs.goBack();
            }
            QuickTestTrace.Log("link", "backprobe depthBefore=" + backDepthBefore
                + " atBefore=" + backAtBefore + " maskBefore=" + backMaskBefore
                + " depthAfter=" + (cs != null ? cs.ProbeHistoryDepth : -1)
                + " atAfter=" + (cs != null ? cs.ProbeLastQuery : "?")
                + " maskAfter=" + (cs != null ? cs.ProbeLastMask : -1));
            // ── 返回钮**落定之后**的真实状态 ────────────────────────────────
            // 上面那条 btnprobe 量的是「刚 clone 完的同一帧」：那一刻克隆体的 UIWidget 还没跑
            // Start/CreatePanel，panel 必然是 none —— 分不出「瞬时未注册（下一帧会好）」和
            // 「永远注册不上（一个像素都不画）」。这里把窗重新叫出来、等 1.5s（NGUI 好几轮更新
            // 过去了），再报一次：active / panel / 贴图 / 屏幕矩形。矩形为空就是真的没画出来。
            CardSearchWindow.ShowQuery("机壳", 1);
            Program.go(1500, () =>
            {
                try
                {
                    CardSearchWindow w2 = Program.I() != null ? Program.I().cardSearch : null;
                    GameObject dw2 = w2 != null ? w2.ProbeWindowObject : null;
                    GameObject bk2 = CardSearchWindow.FindDeep(dw2, CardSearchWindow.BackButtonName);
                    UITexture tex2 = bk2 != null ? bk2.GetComponentInChildren<UITexture>(true) : null;
                    string rct2 = "n/a";
                    if (tex2 != null && Program.camera_main_2d != null)
                    {
                        Vector3[] wc = tex2.worldCorners;
                        Vector3 p0 = Program.camera_main_2d.WorldToScreenPoint(wc[0]);
                        Vector3 p2 = Program.camera_main_2d.WorldToScreenPoint(wc[2]);
                        rct2 = "(" + Mathf.RoundToInt(Mathf.Min(p0.x, p2.x)) + ","
                            + Mathf.RoundToInt(Mathf.Min(p0.y, p2.y)) + ")-("
                            + Mathf.RoundToInt(Mathf.Max(p0.x, p2.x)) + ","
                            + Mathf.RoundToInt(Mathf.Max(p0.y, p2.y)) + ")";
                    }
                    QuickTestTrace.Log("btnprobe", "backstate settled"
                        + " win=" + (dw2 == null ? "NULL" : (dw2.activeInHierarchy ? "on" : "off"))
                        + " back=" + (bk2 == null ? "missing" : (bk2.activeSelf ? "1" : "0"))
                        + " hier=" + (bk2 != null && bk2.activeInHierarchy ? "1" : "0")
                        + " panel=" + (tex2 != null && tex2.panel != null ? tex2.panel.name : "none")
                        + " tex=" + (tex2 != null && tex2.mainTexture != null ? tex2.mainTexture.name : "NULL")
                        + " alpha=" + (tex2 != null ? tex2.color.a.ToString("F2") : "-")
                        + " rect=" + rct2
                        + " input=" + ActOf(CardSearchWindow.FindDeep(dw2, "input_"))
                        + " search=" + ActOf(CardSearchWindow.FindDeep(dw2, "search_"))
                        + " detailed=" + ActOf(CardSearchWindow.FindDeep(dw2, "detailed_")));
                }
                catch (Exception e)
                {
                    QuickTestTrace.Log("btnprobe", "backstate failed " + e.Message);
                }
            });
            // 需求 3 的重头：「弹出的检索窗会重叠已有的」。
            // 把卡组编辑器真的叫起来（它右侧那张检索面板和本窗落点逐像素相同），再弹本窗 ——
            // 编辑器那张必须立刻让开、本窗关掉后再滑回来。末尾把本窗留在屏幕上给截图看。
            // ── 收尾：**非门控**那几支自己开出来的窗口，必须自己收掉 ────────────────
            // ⛔ 为什么非收不可：本方法最后一次 `CardSearchWindow.ShowQuery("机壳",1)`
            //   （backstate 那一档，见上）之后**没有任何人关它**。而检索窗一 `show()` 就会
            //   `BorrowRightSide()` 把**卡组编辑器的侧栏检索面板搬到 `Screen.width+600`（x=2520，
            //   屏外）**，而归还只挂在「检索窗 hide」这一条路上（`ReleaseRightSide`）
            //   ⇒ 窗口不收，面板**永远回不来**。
            //   2026-10-02 实测：`_verify_rdmode.py` 的 B1/B2a/B2b/B7 全红，症状
            //   「点了搜索但没等到 [mode] search」；根因是 `[btnpos] editor input_`
            //   全程停在借用位 `screen=(2407,346)`，脚本照它点 ⇒ `SetCursorPos(2407)`
            //   被屏幕宽度钳到 1918 ⇒ 点空。（同一条链还让 B7a/B7b 的卡图解析走不到。）
            // ⛔⛔ 上一版本里这个收尾是**碰巧**有的：`buttonClickProbe`（当时还没门控）的链尾
            //   会调 `csw.ProbeGoBackOnce()`，那时 history 已空 ⇒ 正好把窗关了。
            //   本轮给那一支加门控（`btnclick.probe`）时，等于把这条**偶然的**收尾一起关掉
            //   ⇒ 必须显式补回来。**别再把收尾留在门控分支里：门控一关，收尾就没了。**
            // ⚠ 只收检索窗与「译」菜单，**不收卡组编辑器**：编辑器是脚本自己开的
            //   （`commamd.shell` 的 `edit`，`_verify_rdmode.py` 的 B4/B1 全靠它），
            //   收了会把那一整套判据打断 ⇒ 所以不能直接调 `CloseAllProbeWindows()`
            //   （它里面那句 `dm.hide()` 会连编辑器一起关掉）。
            // ⚠ `linkdeck.probe` 在时必须让位：`deckBorrowProbe` 正靠这扇窗留在屏上量坐标/截图，
            //   它自己的链尾（`wininfo` + 5s）自带 `CloseAllProbeWindows()`。
            // ⚠ 时间点选 +4s（相对本方法开头）：backstate 那一档的读数是 +2.5s 取的，
            //   早于它会把那次的「落定后状态」量成"窗已关"，读数就废了。
            if (!ProbeSwitch("linkdeck.probe"))
            {
                Program.go(4000, () =>
                {
                    try
                    {
                        CardSearchWindow cs0 = Program.I() != null ? Program.I().cardSearch : null;
                        if (cs0 != null && cs0.isShowed)
                        {
                            cs0.hide();
                        }
                        NameTranslationUI.CloseMenuForProbe();
                        QuickTestTrace.Log("link", "autoclean probe windows (no linkdeck.probe)");
                        // 它那一记 `cs0.hide()` 会**收掉检索窗**，而它会因为主场景加载/切模式
                        // 被拖到很晚才跑 ⇒ 别的探针（见 linkToggleProbe）必须等它跑完再动手，
                        // 否则点击序列中间被它插一脚，读数整体错位一格。
                        probeAutocleanDone = true;
                    }
                    catch (Exception e)
                    {
                        ProbeEx("autoclean", e);
                    }
                });
            }
            boardPushProbe();
            deckBorrowProbe();
            deckPickWindowProbe();
            // 设置界面那行「卡名翻译」的截图探针：2026-10-02 第三轮**重新打开** ——
            // 用户报「翻译选项和其他下拉框重合了」，而重合是矩形之间的事，只能实机量。
            // （它会把设置窗口叫到最前，所以 30s 那张验收截图会被它占住；验证期就要这个。）
            Program.go(20000, settingsPackRowProbe);
            // 译名表「按需下载」的自检放在**最后**：它是真的联网下载，几秒才回，
            // 排在前面会把上面那些同步探针的时序搅乱（也别让它抢 30s 那张截图）。
            Program.go(12000, translationDownloadProbe);
            QuickTestTrace.Log("link", "panel id=" + probe.Id
                + " len=" + (shown != null ? shown.Length : -1)
                + " urlTags=" + CardTextLinker.CountOccur(shown, "[url=")
                + " hasCardColor=" + (shown != null && shown.IndexOf("[FF6600]", StringComparison.Ordinal) >= 0)
                + " hasFieldColor=" + (shown != null && shown.IndexOf("[0070DD]", StringComparison.Ordinal) >= 0));
            // 链接括号的截图探针（只在 log/linkunderline.probe 下干活）：OCG 这一档在这里拍，
            // RD 那一档由 Menu 的 `mode RD` 分支调同一个方法（池换成 RD 池之后再拍）。
            ProbeShowLinkCard();
            // 「连点同一处链接 = 关窗」的验收（只在 log/linktoggle.probe 下干活）；
            // RD 那一档由 Menu 的 `mode RD` 分支调同一个入口。
            ProbeLinkToggle();
            // 收尾那一帧安排在 deckBorrowProbe 的**链尾**（见那里的 finalframe），
            // 不能挂在这儿：deckBorrow 是异步的，它会在本方法返回之后好几秒才把卡组编辑器
            // 和检索窗重新叫起来，这儿先收等于白收，30s 那张截图照样被盖住。
        }
        catch (Exception e)
        {
            Program.DEBUGLOG(e);
        }
    }

    /// <summary>
    /// 译名表**下载/刷新通道**的端到端自检（只在 qt_debug.on 下跑，且排在探针链最后）。
    ///
    /// 要回答的是三个问题，光读源码都答不了：
    ///   ① 真从百鸽下的那份 zip，我们自己的扫描器能不能把三套译名摘出来
    ///      （JSON 有 13.7 MB、条目里还有 text/data 两层嵌套，必须能整块跳过）；
    ///   ② 写出来的 tsv 能不能被 <see cref="YGOSharp.CardNameTranslation"/> 那条
    ///      **现成的装载路径**认下来（与玩家手放的文件同一条路）；
    ///   ③ 换过去以后卡名是不是真的变了 —— 用青眼白龙（89631139）做对照：
    ///      nw 叫「青眼白龙」、cnocg 叫「蓝眼白龙」，两档不同才说明没串表。
    ///
    /// ⚠⛔⛔ 2026-10-03 口径翻转，这条自检的**收尾逻辑跟着改了**：
    ///   原来注释写的是「收尾**一定**把下载到的三个 tsv 删掉，绝不能躺在运行目录里
    ///   再被镜像进分发包」—— 那时表**不随包分发**，所以「运行目录里不该有表」是对的。
    ///   但 2026-10-03 起三张表**改为随包分发**，`_pkg_const.DELIVERY`（= 本运行目录）
    ///   **就是**成品包的镜像源，出厂数据本来就该在这儿。
    ///   ⇒ 无条件删的后果：跑一次自检就把出厂数据删了，下次打包 C2 判「多余文件」。
    ///   （2026-10-03 实打实踩到这一条。）
    ///   现在按 <see cref="YGOSharp.CardTranslationDownloader.LastWritten"/> 决定：
    ///   **这次真的写了才清**（保住 ①② 那条下载→写盘链路的验收），没写就一步不动。
    /// </summary>
    void translationDownloadProbe()
    {
        try
        {
            QuickTestTrace.Log("nametrans", "dlprobe begin packs=" + PackStateLine()
                + " cur=" + YGOSharp.CardNameTranslation.CurrentKey
                + " dir=" + YGOSharp.CardNameTranslation.Folder);
            YGOSharp.CardTranslationDownloader.Start(onDownloadProbeDone);
        }
        catch (Exception e)
        {
            QuickTestTrace.Log("nametrans", "dlprobe failed " + e.Message);
        }
    }

    void onDownloadProbeDone(bool ok, string summary)
    {
        try
        {
            QuickTestTrace.Log("nametrans", "dlprobe done ok=" + ok + " sum=" + summary);
            QuickTestTrace.Log("nametrans", "dlprobe packsAfter=" + PackStateLine());

            // 抽样对照：青眼白龙在原生 / nw / cnocg 三档下的名字。
            const int sample = 89631139;      // 青眼白龙：nw「青眼白龙」 vs cnocg「蓝眼白龙」
            string nameNative = SampleCardName(sample);
            string nameNw = SampleCardNameAfterPack(sample, "nw");
            string nameCnocg = SampleCardNameAfterPack(sample, "cnocg");
            string nameCn = SampleCardNameAfterPack(sample, "cn");
            string nameBack = SampleCardNameAfterPack(sample, YGOSharp.CardNameTranslation.NativeKey);
            QuickTestTrace.Log("nametrans", "dlprobe sample id=" + sample
                + " native=" + nameNative + " nw=" + nameNw + " cnocg=" + nameCnocg
                + " cn=" + nameCn + " back=" + nameBack
                + " allDiffer=" + (nameNw != nameCnocg));

            // 清场：**只删这次探针自己写下去的那几份**。
            // ⛔⛔ 2026-10-03：原来是无条件把 nw/cnocg/cn 三个 tsv 删掉，注释写的是
            //   「包里不能有别人家的译名数据，运行目录也不留」—— 那套口径下是对的，
            //   但 2026-10-03 起**这三张表改为随包分发**（`_gen_translation_pack.py`，
            //   `_pkg_const` 里也改成了「随包 + 进自动更新」）。
            //   ⇒ 无条件删就等于**探针跑一次，把交付树运行目录里的出厂数据删了**；
            //   而那个目录正是成品包的镜像源（`_pkg_const.DELIVERY`），
            //   于是下次打包变成「包里有、运行目录没有」，C2 判成多余文件。
            //   （2026-10-03 实打实踩到：C2 报 3 个 tsv 白名单外。）
            // 现在按 `LastWritten` 决定：写了才清（保住"下载→写盘"这条链路的验收），
            // 没写就一步都不动。
            int removed = 0;
            bool probeWrote = YGOSharp.CardTranslationDownloader.LastWritten > 0;
            if (probeWrote)
            {
                removed = RemoveDownloadedPacks();
            }
            else
            {
                QuickTestTrace.Log("nametrans", "dlprobe cleanup skipped（出厂数据已在，未改动）");
            }
            YGOSharp.CardNameTranslation.Reload();
            YGOSharp.CardsManager.ReapplyNameTranslation();
            QuickTestTrace.Log("nametrans", "dlprobe cleanup wrote=" + YGOSharp.CardTranslationDownloader.LastWritten
                + " removed=" + removed
                + " packsFinal=" + PackStateLine()
                + " cur=" + YGOSharp.CardNameTranslation.CurrentKey
                + " sampleNow=" + SampleCardName(sample)
                + " filesLeft=" + CountTranslationDataFiles());
        }
        catch (Exception e)
        {
            QuickTestTrace.Log("nametrans", "dlprobe done-failed " + e.Message);
        }
    }

    /// <summary>验收用：把所有翻译表「key:状态」摊成一行。</summary>
    static string PackStateLine()
    {
        List<YGOSharp.CardNameTranslation.Pack> packs = YGOSharp.CardNameTranslation.Packs;
        string s = "";
        for (int i = 0; i < packs.Count; i++)
        {
            YGOSharp.CardNameTranslation.Pack p = packs[i];
            if (s.Length > 0)
            {
                s += ",";
            }
            s += p.key + "=" + (p.missing ? "missing" : p.Count.ToString());
        }
        return s;
    }

    /// <summary>验收用：切到某一份表之后，这张卡显示成什么名（切不动就返回当前名）。</summary>
    static string SampleCardNameAfterPack(int id, string key)
    {
        YGOSharp.CardNameTranslation.SetCurrent(key);
        YGOSharp.CardsManager.ReapplyNameTranslation();
        return SampleCardName(id);
    }

    static string SampleCardName(int id)
    {
        YGOSharp.Card c = YGOSharp.CardsManager.Get(id);
        return c != null ? (c.Name ?? "") : "(池里没有这张卡)";
    }

    /// <summary>
    /// 把**这次自检下载到的那三份** tsv 删掉（只删这三个名字，不碰玩家自己放的文件）。
    /// 返回删掉的个数。
    /// ⛔⛔ 2026-10-03：**调用方必须先看**
    /// <see cref="YGOSharp.CardTranslationDownloader.LastWritten"/>——
    /// 三张表现在是**随包分发的出厂数据**，无条件调这里就是把出厂数据删了
    /// （而本运行目录正是成品包的镜像源）。详见
    /// <see cref="translationDownloadProbe"/> 的头注。
    /// </summary>
    static int RemoveDownloadedPacks()
    {
        string[] files = { "nw.tsv", "cnocg.tsv", "cn.tsv" };
        int n = 0;
        for (int i = 0; i < files.Length; i++)
        {
            string p = System.IO.Path.Combine(YGOSharp.CardNameTranslation.Folder, files[i]);
            try
            {
                if (System.IO.File.Exists(p))
                {
                    System.IO.File.Delete(p);
                    n++;
                }
            }
            catch (Exception e)
            {
                Program.DEBUGLOG(e);
            }
        }
        return n;
    }

    /// <summary>translation/ 下还剩几个数据文件（tsv/cdb）—— 只用来打日志。
    /// ⛔ 2026-10-03：原来的注释写「期望 0」。三张表**随包分发**之后这个期望**不成立**
    ///   ——出厂状态下这里就该是 3（nw/cnocg/cn）。改成只报数、不当判据；
    ///   「这次自检有没有留下东西」由 `LastWritten`/`removed` 那一对数回答。</summary>
    static int CountTranslationDataFiles()
    {
        try
        {
            if (!System.IO.Directory.Exists(YGOSharp.CardNameTranslation.Folder))
            {
                return 0;
            }
            return System.IO.Directory.GetFiles(YGOSharp.CardNameTranslation.Folder, "*.tsv").Length
                 + System.IO.Directory.GetFiles(YGOSharp.CardNameTranslation.Folder, "*.cdb").Length;
        }
        catch (Exception e)
        {
            Program.DEBUGLOG(e);
            return -1;
        }
    }

    /// <summary>
    /// 需求 3 的端到端自检（只在 qt_debug.on 下跑）：卡组编辑器右侧那张检索面板与「点链接弹出的
    /// 检索窗」落点逐像素相同，同时出现就是一叠。这里把编辑器真的叫起来，先后量三次面板在屏幕上的 x：
    ///   onShow   编辑器刚起来 —— 面板应在屏内；
    ///   borrowed 本窗弹出 —— 面板必须立刻退到屏外（yieldSearchPanel，不做补间，免得半路还叠一下）；
    ///   restored 本窗关掉 —— 面板必须滑回屏内。
    /// 末尾再弹一次本窗，留给截图肉眼核对「屏幕上只有一块检索面板」。
    /// </summary>
    /// <summary>
    /// 两窗图标配色的**数值对照**（用户 2026-10-02 第二轮：返回钮/X 的颜色跟放大镜、
    /// 分类不一样）。只在 <c>iconclr.probe</c> 开关下跑，且必须在 markForScreenshot
    /// 染红之前调用（染了红读到的就是染料）。
    ///
    /// 报三组：
    ///   * editor —— 卡组编辑器检索面板（玩家对照的「正常搜索栏」）的 search_ / detailed_；
    ///   * popup  —— 弹窗本体的返回钮 / 自带（已失活）search_ / detailed_；
    ///   * 附带两窗根 UITexture 的颜色（面板底图不同会让同色图标**看起来**不同）。
    /// </summary>
    /// <summary>单颗图标钮的取色 + **屏幕矩形**（矩形是给「观感大小」对拍用的——
    /// widget 尺寸相同不等于渲染尺寸相同，两窗面板 scale 可能不同）。</summary>
    static string IconClrReport(GameObject root, string buttonName)
    {
        string s = CardSearchWindow.IconClrIn(root, buttonName);
        GameObject g = CardSearchWindow.FindDeep(root, buttonName);
        UITexture t = g != null ? g.GetComponentInChildren<UITexture>(true) : null;
        if (t != null)
        {
            s += " rect=" + CardSearchWindow.RectOf(t, Program.camera_main_2d);
            s += " depth=" + t.depth;
            GameObject under = CardSearchWindow.FindDeep(root, "under_");
            UITexture ut = under != null ? under.GetComponent<UITexture>() : null;
            if (ut != null)
            {
                s += " underDepth=" + ut.depth;
            }
            s += " shader=" + (t.material != null && t.material.shader != null ? t.material.shader.name : "null");
            s += " dc=" + (t.drawCall != null ? t.drawCall.renderQueue.ToString() : "none")
                + "/" + (t.drawCall != null && t.drawCall.dynamicMaterial != null
                    && t.drawCall.dynamicMaterial.shader != null
                    ? t.drawCall.dynamicMaterial.shader.name : "?");
            // panel 链 alpha：NGUI 最终渲染色 = widget.color × 沿途每个 UIPanel.alpha ——
            // 两窗挂不同链（ui_main_2d vs ui_back_ground_2d），任何一环 alpha<1 都会把
            // 图标整体压暗（「颜色数值一样却看着发暗」的头号嫌疑）。
            UIPanel p = t.panel;
            int guard = 0;
            while (p != null && guard++ < 8)
            {
                s += " p[" + p.name + " a=" + p.alpha.ToString("F2") + "]";
                Transform up = p.transform.parent;
                p = up != null ? up.GetComponentInParent<UIPanel>() : null;
            }
        }
        return s;
    }

    void iconColorProbe()
    {
        try
        {
            DeckManager dm = Program.I() != null ? Program.I().deckManager : null;
            CardSearchWindow cs = Program.I() != null ? Program.I().cardSearch : null;
            QuickTestTrace.Log("iconclr", "editor "
                + (dm != null && dm.gameObjectSearch != null
                    ? IconClrReport(dm.gameObjectSearch, "search_") + " | "
                        + IconClrReport(dm.gameObjectSearch, "detailed_")
                    : "panel-missing"));
            QuickTestTrace.Log("iconclr", "popupBack "
                + (cs != null ? IconClrReport(cs.ProbeWindowObject, CardSearchWindow.BackButtonName) : "null"));
            // 同屏受控对照（与截图脚本配套，变体随窗销毁）：
            //   V2 = 白 color(1,1,1,1) 的 X —— 若它亮起来 ⇒ 差异在 color 路径；
            //   V3 = search.png 贴图的 X（color 不变）—— 若它亮起来 ⇒ 差异在贴图路径；
            //   两个都暗 ⇒ 弹窗里有个整层级的压暗因素（遮罩/overlay）。
            if (cs != null && cs.ProbeWindowObject != null)
            {
                GameObject v2 = CardSearchWindow.ProbeBackVariant(cs.ProbeWindowObject, 40f, Color.white, null);
                GameObject v3 = CardSearchWindow.ProbeBackVariant(cs.ProbeWindowObject, 80f, null, "search");
                QuickTestTrace.Log("iconclr", "variants v2=" + (v2 != null) + " v3=" + (v3 != null));
            }
            // 「分类弹窗挤开穿插」（用户 2026-10-02 第二轮需求 1）：检索窗开着时点一次
            // 「分类」—— preFrameFunction 应把本窗**平移**让位（不再整窗藏掉）。
            // 反射调私有 onClickDetail，等 1s 让 relayout 落盘 + 截图窗口正好拍到穿插态。
            if (ProbeSwitch("iconclr.probe"))
            {
                Program.go(1000, () =>
                {
                    try
                    {
                        DeckManager dm1 = Program.I() != null ? Program.I().deckManager : null;
                        if (dm1 != null)
                        {
                            System.Reflection.MethodInfo m = typeof(DeckManager).GetMethod("onClickDetail",
                                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                            if (m != null)
                            {
                                m.Invoke(dm1, null);
                                QuickTestTrace.Log("iconclr", "detailpop invoked -> "
                                    + (dm1.DetailPopupOpen ? "open" : "closed"));
                            }
                        }
                    }
                    catch (Exception e1)
                    {
                        ProbeEx("detailpop", e1);
                    }
                });
            }
            UITexture eb = dm != null && dm.gameObjectSearch != null
                ? UIHelper.getByName<UITexture>(dm.gameObjectSearch, "under_") : null;
            QuickTestTrace.Log("iconclr", "bgs editorUnder="
                + (eb != null ? eb.color.r.ToString("F2") + "," + eb.color.g.ToString("F2") + ","
                    + eb.color.b.ToString("F2") + "," + eb.color.a.ToString("F2") : "n/a")
                + " editorScale=" + (dm != null && dm.gameObjectSearch != null
                    ? dm.gameObjectSearch.transform.localScale.ToString("F2") : "n/a"));
        }
        catch (Exception e)
        {
            Program.DEBUGLOG(e);
        }
    }

    void deckBorrowProbe()
    {
        // ⛔⛔ 这一支有**全局副作用**：它把 `deck/` 里第一个 ydk（真实玩家卡组，40~60 张）
        //   装进卡组编辑器，并在链尾把编辑器 + 检索窗重新叫起来。所以它**不能**跟着
        //   `qt_debug.on` 默认跑 —— 2026-10-02 实测：`_verify_rdmode.py` 的 B4a/B4b
        //   （「编辑器读的是当前模式那个目录里的 fixture」）就是被它踩掉的：脚本刚把
        //   7 张的 `RdProbe` 装好，它 1.5s 后就把 48 张的 `3146095889+星遗物的再奏`
        //   灌进**同一个**编辑器 ⇒ `[slot] deck=RdProbe main=48/7 orderDiffVsYdk=7`；
        //   而它链尾重开的编辑器/检索窗又把后续「点搜索 / 读卡组列表 / 点主菜单」
        //   的落点全带偏（B1/B2/B7a/OCGB3 一连串红，看着像产品回归）。
        //   要跑它自己的验收（`_shot_searchdetail.py` / `_verify_uirev2.py`，
        //   两者都等它链尾的 `[link] wininfo`），先放 `log/linkdeck.probe`。
        // ⛔ `boardpush.probe` 与 `linkdeck.probe` **互斥**：两支都要「开卡组编辑器 + 装一个
        //   ydk + 把检索窗叫起来」，同时跑会互相抢落点（连读数都对不上）。
        //   这一支是 2026-10-03 那两条需求的专用验收，开它就只跑它。
        if (ProbeSwitch("boardpush.probe"))
        {
            QuickTestTrace.Log("link", "deckborrow skipped (boardpush.probe is on)");
            return;
        }
        if (!ProbeSwitch("linkdeck.probe"))
        {
            QuickTestTrace.Log("link", "deckborrow skipped (no linkdeck.probe)");
            return;
        }
        DeckManager dm = Program.I() != null ? Program.I().deckManager : null;
        CardSearchWindow cs = Program.I() != null ? Program.I().cardSearch : null;
        if (dm == null || cs == null)
        {
            QuickTestTrace.Log("link", "deckborrow skip (no deckManager/cardSearch)");
            return;
        }
        // 从干净状态起：先把上一段探针留在屏幕上的那颗窗收掉，否则量到的是它借走之后的位置。
        if (cs.isShowed)
        {
            cs.hide();
        }
        QuickTestTrace.Log("link", "deckborrow begin dir=" + GameModeManager.DeckDir);
        string deckPath = "";
        try
        {
            string[] files = System.IO.Directory.GetFiles(GameModeManager.DeckDir, "*.ydk");
            if (files.Length > 0)
            {
                deckPath = files[0];
            }
        }
        catch (Exception e0)
        {
            ProbeEx("listydk", e0);
        }
        if (deckPath.Length == 0)
        {
            QuickTestTrace.Log("link", "deckborrow skip (no ydk under " + GameModeManager.DeckDir + ")");
            return;
        }
        Program.go(1500, () =>
        {
            try
            {
                // 与 selectDeck.KF_editDeck 同一条路（少 armDeckEditorReturn 一步：那是给工具条返回键用的）。
                dm.shiftCondition(DeckManager.Condition.editDeck);
                Program.I().shiftToServant(dm);
                dm.loadDeckFromYDK(deckPath);
                setTitle(System.IO.Path.GetFileNameWithoutExtension(deckPath));
                dm.setGoodLooking();
            }
            catch (Exception e1)
            {
                ProbeEx("open", e1);
            }
            Program.go(2600, () =>
            {
                try
                {
                    int a = SearchPanelScreenX(dm);
                    CardSearchWindow.ShowQuery("机壳", 1);
                    Program.go(1700, () =>
                    {
                        try
                        {
                            int b = SearchPanelScreenX(dm);
                            CardSearchWindow cs2 = Program.I() != null ? Program.I().cardSearch : null;
                            if (cs2 != null)
                            {
                                cs2.hide();
                            }
                            Program.go(2000, () =>
                            {
                                try
                                {
                                    int c = SearchPanelScreenX(dm);
                                    QuickTestTrace.Log("link", "deckborrow onShow=" + a
                                        + " borrowed=" + b + " restored=" + c
                                        + " screenW=" + Screen.width);
                                    CardSearchWindow.ShowQuery("机壳", 1);
                                    Program.go(2200, () =>
                                    {
                                        try
                                        {
                                            CardSearchWindow csw = Program.I() != null ? Program.I().cardSearch : null;
                                            QuickTestTrace.Log("link", "wininfo " + (csw != null ? csw.ProbeWindowInfo : "null"));
                                            QuickTestTrace.Log("link", csw != null ? csw.ProbePanelWiring : "panelWiring null");
                                            // ⛔ 这才是**可信**的返回钮读数：上面那条 detailprobe 是紧跟着
                                            //   `deckborrow begin` 打的，那一刻整块检索面板正被卡组编辑器
                                            //   **搬到 x=2520**（onShow=1805 borrowed=2520）⇒ 量出来
                                            //   `backRect=(2782,612)-(2806,638)` 的"屏外"是**借走态**的产物，
                                            //   不是钮真的跑出去了（2026-10-02 第五轮踩过这个坑）。
                                            //   这里 `restored=1818` 已经复原、窗口重新 ShowQuery 过，
                                            //   量到的才是玩家看到的位置。
                                            QuickTestTrace.Log("link", "backclean "
                                                + (csw != null ? csw.ProbeBackDeep : "null"));
                                            // 第六轮：实拍证明「返回钮两个候选落点都是空白背景」⇒ 先判「窗画没画」。
                                            QuickTestTrace.Log("link", csw != null ? csw.ProbeDrawOrder : "draworder null");
                                            // 取色探针（用户 2026-10-02 第二轮「返回钮/X 的颜色跟放大镜、分类
                                            // 不一样」）：必须在 markForScreenshot **染红之前**同步读 —— 染了红
                                            // 读到的就是染料不是本色。独立开关放行，不踩别的验收。
                                            if (ProbeSwitch("iconclr.probe"))
                                            {
                                                iconColorProbe();
                                            }
                                            if (csw != null)
                                            {
                                                if (ProbeSwitch("iconclr.probe"))
                                                {
                                                    // 取色轮不染红：这张截图要拿去做「两窗图标配色」的观感对照。
                                                    QuickTestTrace.Log("iconclr", "skip red tint (iconclr.probe)");
                                                }
                                                else
                                                {
                                                    // 只染钮本身（红）。⛔ 别再顺手染容器：那会把整列底图糊成半透明，
                                                    //   观感判据跟着失真（markWindowForScreenshot 留着按需用）。
                                                    csw.markForScreenshot();
                                                }
                                            }
                                            // 收尾（2026-10-02）：整条 deckBorrow 链跑完之后，
                                            // 把所有窗口/对话框收干净、只留说明面板 + 展示卡。
                                            // 必须挂在**这里**（链尾），不能挂在 onAcceptanceProbe
                                            // 末尾 —— 那时候 deckBorrow 才刚开始，它会在几秒后
                                            // 重新把卡组编辑器和检索窗叫起来，截图照样被盖住。
                                            Program.go(5000, () =>
                                            {
                                                CloseAllProbeWindows();
                                                YGOSharp.Card sc = ShowcaseCard;
                                                if (sc == null)
                                                {
                                                    // RD 档没有「」+种类短语 ⇒ 上面那支挑不出来，
                                                    // 退回「只要描述里能链出东西就行」（RD 是（）括号）。
                                                    sc = FindLinkShowcase();
                                                }
                                                if (sc != null)
                                                {
                                                    setData(sc, GameTextureManager.myBack, "", true);
                                                }
                                                show();
                                                QuickTestTrace.Log("link", "finalframe showcase="
                                                    + (sc != null ? sc.Id.ToString() : "none")
                                                    + " urlTags=" + (sc != null
                                                        ? CardTextLinker.CountOccur(
                                                            CardTextLinker.Linkify(sc.Desc, sc.Id), "[url=")
                                                        : 0)
                                                    + " " + describeButton("translate_",
                                                        UIHelper.getByName(gameObject, NameTranslationUI.ButtonName)));
                                                // 描述文字的屏幕矩形：截图脚本靠它裁出「链接那几行」，
                                                // 不用去猜像素带（判据坐标带会随 UI 改动整体位移）。
                                                QuickTestTrace.Log("link", "descRect " + DescLabelScreenRect());
                                            });
                                        }
                                        catch (Exception e5)
                                        {
                                            ProbeEx("wininfo", e5);
                                        }
                                    });
                                }
                                catch (Exception e4)
                                {
                                    ProbeEx("restore", e4);
                                }
                            });
                        }
                        catch (Exception e3)
                        {
                            ProbeEx("borrow", e3);
                        }
                    });
                }
                catch (Exception e2)
                {
                    ProbeEx("measure", e2);
                }
            });
        });
    }

    /// <summary>
    /// 2026-10-03 两条需求的**专用验收探针**（只在 <c>log/boardpush.probe</c> 下跑）：
    ///   ① 卡组板子不再随滚轮运动；
    ///   ② 链接检索窗像「分类」一样顶开卡组板子；
    ///   ③ 简介 UI（宿主）消失 ⇒ 检索窗自动收掉。
    ///
    /// 为什么单独一支：它要**按固定时间轴**开卡组编辑器、开检索窗、关编辑器，而
    /// <see cref="deckBorrowProbe"/> 那条链是给「返回钮/画序」验收用的，时序不一样，
    /// 混在一条链里两边都量不准（两支互斥，见那里）。
    ///
    /// 时间轴（相对本方法被调用那一刻；脚本按 <c>[link] boardpush stage=</c> 抓拍）：
    ///   +1.5s 开编辑器 → +4.1s <b>closed</b>（板子满宽）
    ///   → ⚠ 之后有 **16 秒静默窗**给「滚轮不再让板子动」那条判据用（截图 A → 滚 → 截图 B），
    ///      别把它压短：上一版只留 2.5s，脚本还没拍完第二张窗就开了，两张图差 56% 像素。
    ///      也别把截图拍在**牌面贴图流完之前** —— 卡图是异步读盘的，早拍会拍到一半糊的牌，
    ///      两张"应该逐像素相同"的图能差 16%（第一版就是这么假红的）。
    ///   → +20.1s 开检索窗 → +22.3s <b>open</b>（窗在屏上，板子应被顶开）
    ///   → +24.8s 收窗 → +26.3s <b>reclosed</b>（板子应回到满宽）
    ///   → +27.8s 再开窗 → +29.3s <b>winup</b> → 立刻 <c>dm.hide()</c>
    ///   → +30.8s <b>afterhide</b>（宿主没了，窗应已自动收掉）
    /// </summary>
    /// <summary>
    /// 验收「选牌偏移」用的**开窗**探针（只在 <c>log/deckpickwin.probe</c> 下跑）。
    ///
    /// <para><b>为什么不复用 <see cref="boardPushProbe"/></b>：它会自己
    /// <c>loadDeckFromYDK(dir 里的第一个 ydk)</c> 打开卡组编辑器，而那个 ydk
    /// 不一定是脚本想验的那一副（本机就撞上过空卡组 ⇒ 板子上没有
    /// <c>MonoCardInDeckManager</c> ⇒ <c>[pick]</c> 探针一行都写不出来）。
    /// 本探针只做一件事：等脚本用 <c>commamd.shell</c> 把卡组编辑器开好之后，
    /// 在指定时刻把「简介里点链接弹出来的检索窗」叫起来，让脚本能在
    /// <b>push&gt;0</b> 那一档上再扫一遍板子（顶开逻辑不能被选牌修复抹掉）。</para>
    ///
    /// <para>用法：log/deckpickwin.probe 里写一个毫秒数（相对本函数被调用那一刻），
    /// 留空 = 12000。脚本按 <c>[link] pickwin open</c> 那一行对时。</para>
    /// </summary>
    void deckPickWindowProbe()
    {
        if (!ProbeSwitch("deckpickwin.probe"))
        {
            return;
        }
        int delayMs = 12000;
        try
        {
            string s = System.IO.File.ReadAllText(QuickTestTrace.LogPath("deckpickwin.probe")).Trim();
            int v;
            if (s.Length > 0 && int.TryParse(s, out v) && v > 0)
            {
                delayMs = v;
            }
        }
        catch (Exception)
        {
        }
        Program.go(delayMs, () =>
        {
            try
            {
                CardSearchWindow.ShowQuery("机壳", 1);
                QuickTestTrace.Log("link", "pickwin open t=" + delayMs
                    + " push=" + Mathf.RoundToInt(CardSearchWindow.BoardPushWidth())
                    + " camViewport=" + Program.CamViewportCenter.ToString("F1")
                    + " camRectX=" + Program.camera_game_main.rect.x.ToString("F4"));
            }
            catch (Exception e)
            {
                ProbeEx("pickwin open", e);
            }
        });
    }

    /// <summary>
    /// 目录里**第一副真有卡的** .ydk（找不到就退回第一副 .ydk，再没有就空串）。
    ///
    /// <para><b>为什么不能直接取 <c>files[0]</c></b>（2026-10-03 踩到）：
    /// 这支探针有一条判据是「截图上板子真的横移了」，靠的是**卡面纹理**互相关。
    /// 而 <c>files[0]</c> 取决于文件系统枚举顺序 —— 本机那天多了一副空卡组
    /// （<c>10-03「04：30：28」.ydk</c>，玩家自己建的），它恰好排在第一位，
    /// 于是板子上**一张牌都没有</c>：那条判据量到的样本区域里只剩背景，
    /// 量出「没横移」，整档假红（C3），而顶开功能其实好的。
    /// ⇒ 探针必须挑一副**有卡**的，才对得起它自己的判据。</para>
    ///
    /// <para>口径：读 .ydk 文本，统计「<c>#main</c> 段里的卡」的张数；&gt;0 即算有卡。
    /// 本工程写出来的是「一张卡一行、只有卡号」，上游 ygopro 是「卡号 张数」，两种都认
    /// （第一版只认后者 ⇒ 一副有卡的都判成 0 张 ⇒ 又退回 files[0]，也就是那个空卡组）。
    /// 纯读盘；读不动的当没卡，继续找下一副。</para>
    /// </summary>
    static string firstDeckWithCards(string dir)
    {
        string[] files = System.IO.Directory.GetFiles(dir, "*.ydk");
        System.Array.Sort(files);
        string firstAny = files.Length > 0 ? files[0] : "";
        for (int i = 0; i < files.Length; i++)
        {
            try
            {
                int main = 0;
                bool inMain = false;
                string[] ls = System.IO.File.ReadAllLines(files[i]);
                for (int k = 0; k < ls.Length; k++)
                {
                    string s = ls[k].Trim();
                    if (s.Length == 0 || s[0] == '#' || s[0] == '!')
                    {
                        if (s.StartsWith("#main"))
                        {
                            inMain = true;
                        }
                        else if (s.StartsWith("#"))
                        {
                            inMain = false;
                        }
                        continue;
                    }
                    if (!inMain)
                    {
                        continue;
                    }
                    // 本工程写出来的 .ydk 是**一张卡一行、只有卡号**；
                    // 上游 ygopro 是「卡号 张数」。两种都认。
                    string[] parts = s.Split(new char[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                    int code = 0;
                    int n = 0;
                    if (parts.Length >= 2 && int.TryParse(parts[0], out code) && int.TryParse(parts[1], out n))
                    {
                        main += n;
                    }
                    else if (parts.Length >= 1 && int.TryParse(parts[0], out code))
                    {
                        main += 1;
                    }
                }
                if (main > 0)
                {
                    return files[i];
                }
            }
            catch (Exception)
            {
                // 读不动就当它没卡，继续找下一副。
            }
        }
        return firstAny;
    }

    void boardPushProbe()
    {
        if (!ProbeSwitch("boardpush.probe"))
        {
            return;
        }
        DeckManager dm = Program.I() != null ? Program.I().deckManager : null;
        if (dm == null)
        {
            QuickTestTrace.Log("link", "boardpush skip (no deckManager)");
            return;
        }
        string deckPath = "";
        try
        {
            deckPath = firstDeckWithCards(GameModeManager.DeckDir);
        }
        catch (Exception e0)
        {
            ProbeEx("boardpush listydk", e0);
        }
        if (deckPath.Length == 0)
        {
            QuickTestTrace.Log("link", "boardpush skip (no ydk under " + GameModeManager.DeckDir + ")");
            return;
        }
        QuickTestTrace.Log("link", "boardpush begin deck=" + System.IO.Path.GetFileName(deckPath));

        void stage(string name, string extra)
        {
            CardSearchWindow w = Program.I() != null ? Program.I().cardSearch : null;
            QuickTestTrace.Log("link", "boardpush stage=" + name
                + " winShowed=" + (w != null && w.isShowed ? 1 : 0)
                + " push=" + Mathf.RoundToInt(CardSearchWindow.BoardPushWidth())
                + " camRectX=" + Program.camera_game_main.rect.x.ToString("F4")
                + " " + extra);
        }

        Program.go(1500, () =>
        {
            try
            {
                dm.shiftCondition(DeckManager.Condition.editDeck);
                Program.I().shiftToServant(dm);
                dm.loadDeckFromYDK(deckPath);
                setTitle(System.IO.Path.GetFileNameWithoutExtension(deckPath));
                dm.setGoodLooking();
            }
            catch (Exception e1)
            {
                ProbeEx("boardpush open", e1);
            }
            Program.go(2600, () =>
            {
                stage("closed", "editorShown=" + (dm.isShowed ? 1 : 0));
                // ⛔ 这一段**故意留 16 秒**：验收脚本要在这段时间里「拍一张 → 滚滚轮 → 再拍一张」，
                //   两张图必须都是"窗还没开"的状态，否则比出来的是"窗出现了"不是"板子动了"；
                //   而且第一张必须等卡图流完（异步读盘，早了会拍到一半糊的牌）。
                Program.go(16000, () =>
                {
                    try
                    {
                        CardSearchWindow.ShowQuery("机壳", 1);
                    }
                    catch (Exception e2)
                    {
                        ProbeEx("boardpush show", e2);
                    }
                    Program.go(2200, () =>
                    {
                        stage("open", "");
                        Program.go(2500, () =>
                        {
                            try
                            {
                                CardSearchWindow w = Program.I().cardSearch;
                                if (w != null)
                                {
                                    w.hide();
                                }
                            }
                            catch (Exception e3)
                            {
                                ProbeEx("boardpush hide", e3);
                            }
                            Program.go(1500, () =>
                            {
                                stage("reclosed", "");
                                Program.go(1500, () =>
                                {
                                    try
                                    {
                                        CardSearchWindow.ShowQuery("机壳", 1);
                                    }
                                    catch (Exception e4)
                                    {
                                        ProbeEx("boardpush show2", e4);
                                    }
                                    Program.go(1500, () =>
                                    {
                                        stage("winup", "");
                                        // 关键一刀：**藏掉卡组编辑器**（宿主简介 UI 随之消失）
                                        // ⇒ 检索窗应当自己收掉（不用任何人去点它）。
                                        try
                                        {
                                            dm.hide();
                                        }
                                        catch (Exception e5)
                                        {
                                            ProbeEx("boardpush dmhide", e5);
                                        }
                                        Program.go(1500, () =>
                                        {
                                            stage("afterhide", "");
                                            Program.go(1200, () =>
                                            {
                                                try
                                                {
                                                    show();
                                                }
                                                catch (Exception e6)
                                                {
                                                    ProbeEx("boardpush final", e6);
                                                }
                                            });
                                        });
                                    });
                                });
                            });
                        });
                    });
                });
            });
        });
    }

    /// <summary>探针专用异常出口：Program.DEBUGLOG 在正式版里是空实现，异常会被吞得无声无息。</summary>
    static void ProbeEx(string where, Exception e)
    {
        QuickTestTrace.Log("link", "deckborrow exc@" + where + " " + e.GetType().Name + ": " + e.Message);
    }

    /// <summary>
    /// 探针开关文件（`log/&lt;name&gt;`）在不在 —— 与 `log/qt_debug.on` 同一套「放文件即开」的约定。
    /// 给那些**有全局副作用**的探针用（例如"真的切模式"）：它们不能跟着 `qt_debug.on` 一起
    /// 默认打开，否则会把同时跑着的别的验收脚本搅乱。
    /// </summary>
    static bool ProbeSwitch(string name)
    {
        try
        {
            return System.IO.File.Exists(System.IO.Path.Combine("log", name));
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>侧栏检索面板此刻在屏幕上的 x（不在画面里就是被借走 / 收起了）。</summary>
    static int SearchPanelScreenX(DeckManager dm)
    {
        if (dm == null || dm.gameObjectSearch == null || Program.camera_back_ground_2d == null)
        {
            return -9999;
        }
        Vector3 wp = Program.camera_back_ground_2d.WorldToScreenPoint(dm.gameObjectSearch.transform.position);
        return Mathf.RoundToInt(wp.x);
    }

    /// <summary>
    /// 把设置界面叫出来、量一遍「卡名翻译」那一行，然后**留在屏幕上**给截图肉眼核对。
    /// 判据：行建出来了、文案是「卡名翻译：&lt;当前表名&gt;」、勾选框已藏、行挂在面板上。
    /// </summary>
    void settingsPackRowProbe()
    {
        // ⛔⛔ 这一支同样有**全局副作用**，必须由独立开关放行（`log/nametrans_pack.probe`）：
        //   它 `st.show()` 把**设置窗口**打开并留在屏上。而 `Setting.rayDumped` 是**实例
        //   字段、永不复位** ⇒ 只要它先开过一次窗，别的脚本再点「设置」就**永远等不到新的
        //   `[setting] ray`**（`_verify_rdmode.py::open_settings(first=True)` 正是靠这条 ray
        //   判定窗口开了）；更直接的是那扇窗**一直盖在屏上**，后面所有「点主菜单 / 点 RD
        //   入口 / 点搜索 / 读卡组列表」的落点全被它吃掉。
        //   2026-10-02 实测（不加闸那版）：`_verify_rdmode.py` 红 38 条，根因就一句
        //   「点 3 次 RD 入口都没切到 RD」—— RD 档整段连崩（A2*/B5*），`[mouse]` 的 hover
        //   全程停在 `trans_setting(Clone)/…/mainWindow`（＝光标压在设置窗上，根本没到主菜单）。
        //   ⚠ 早先那段注释写着「这一档是打开设置窗的唯一可靠通道、`_verify_rdmode.py` 吃它」
        //   ——那是**错的**：rdmode 不走探针开窗，它自己点「设置」并等**新的** ray。
        //   要跑本族的验收就放这个开关：`_verify_rdbadge.py` / `_verify_uirev2.py` /
        //   `_shot_setting_pack.py`（后两档「真切模式」另需 `nametrans_rd.probe`，见下）。
        if (!ProbeSwitch("nametrans_pack.probe"))
        {
            QuickTestTrace.Log("setting", "packrow probe skipped (no nametrans_pack.probe)");
            return;
        }
        try
        {
            Setting st = Program.I() != null ? Program.I().setting : null;
            if (st == null)
            {
                QuickTestTrace.Log("setting", "packrow probe skip (no setting)");
                return;
            }
            // 前面那些探针留下的「译」菜单 / 检索窗 / 卡组编辑器会**盖住**设置窗口里
            // 追加行所在的那一列，截图就看不出这一行了 —— 先全收掉，只留设置窗口。
            CloseAllProbeWindows();
            st.show();
            // ⚠ 必须等窗口滑进来再量：show() 是 1.2s 的补间，同帧量到的是**还没走到位**的
            //   屏幕坐标（实测 y=1193 而屏高只有 986 —— 整行在屏外下方，看起来就是"行不见了"）。
            //
            // ── 三段：OCG → RD → OCG（2026-10-02 第 6 轮，"给 RD 做特化"那三条需求）────
            // 判据是**同一行**在三档下 items / badge / 两个选择器清单的增减：
            //   OCG: items=4(全部) badge=0  menu=[pack|cardpack|alias]  packs=[native|nw|cnocg|cn]
            //   RD : items=1(只剩原生) badge=1  menu=[pack|alias]      packs=[native]
            //   回到 OCG 必须与第一档逐字段相同（证明 RD 那几下没把 OCG 的选择/清单改脏）。
            // ⚠ 直接写 GameModeManager（等价于点菜单那颗 RD 入口），不依赖鼠标注入 ——
            //   本机 mouse_event 的按下到不了 Input 层（见 Menu.cs 里 `mode` 指令那段注释）。
            // ── 第一档（OCG）：不切模式（要切模式另看 `nametrans_rd.probe`）─────────
            //   ⚠ 需要这一档开出来的窗口、以及它一次性 dump 的 `[setting] ray` 的是
            //   `_verify_rdbadge.py`（先等 ray、等不到才退回自己点）。所以跑 rdbadge
            //   必须放 `nametrans_pack.probe`。2026-10-02 实测：这一档不放行时，
            //   rdbadge 三次重试全拿不到 ray（`[setting] ray` 计数 0）。
            //   ⛔ 反过来 `_verify_rdmode.py` **不能**放这个开关 —— 它必须自己是第一个
            //   开窗的（ray 一次性、`rayDumped` 不复位，见函数头长注释）。
            Program.go(2500, () =>
            {
                st.ProbeTranslationPackRow();
                QuickTestTrace.Log("nametrans", NameTranslationUI.ProbeMenuContents());
            });
            // ── 后两档：**真的要切模式**，必须由独立开关文件放行 ──────────────
            // ⛔ 不能默认跑：切模式会搅乱同时跑的验收脚本（`_verify_rdmode.py` 正要点
            //   RD 入口、还要量 `grow rows=` / `rdRows` 台账），会把人家量到一半的判据
            //   搅成假红。只有本功能的脚本（_verify_uirev2.py / _shot_setting_pack.py）
            //   才放这个开关文件。
            if (!ProbeSwitch("nametrans_rd.probe"))
            {
                return;
            }
            // ⛔ 档期必须**显式排开**，不能"量完顺手切"：
            //   实拍脚本要在每一档里截一张图（`ImageGrab` 一次 ~2.7s，检测本身还要 ~0.4s），
            //   上一版把「量 OCG」与「切 RD」放在同一个 lambda 里 ⇒ OCG 档只活了 **2.6s**
            //   （show() 到那一刻），截图抓到的是切过去之后的 RD 画面 —— 两档拍成同一张。
            Program.go(6500, () =>
            {
                GameModeManager.Set(GameModeManager.Mode.RD);
            });
            Program.go(8500, () =>
            {
                st.ProbeTranslationPackRow();
                QuickTestTrace.Log("nametrans", NameTranslationUI.ProbeMenuContents());
            });
            Program.go(16000, () =>
            {
                GameModeManager.Set(GameModeManager.Mode.OCG);
            });
            Program.go(18000, () =>
            {
                st.ProbeTranslationPackRow();
                QuickTestTrace.Log("nametrans", NameTranslationUI.ProbeMenuContents());
            });
        }
        catch (Exception e)
        {
            Program.DEBUGLOG(e);
        }
    }

    /// <summary>把探针期间开出来的所有窗口收干净（给"要截图的那一帧"用）。</summary>
    void CloseAllProbeWindows()
    {
        try
        {
            if (Program.I() == null)
            {
                return;
            }
            CardSearchWindow cs = Program.I().cardSearch;
            if (cs != null && cs.isShowed)
            {
                cs.hide();
            }
            DeckManager dm = Program.I().deckManager;
            if (dm != null && dm.isShowed)
            {
                dm.hide();
            }
            // 「译」钮那个菜单是对话框（另一个 Servant 的窗口），用 RMSshow_clear 收掉。
            NameTranslationUI.CloseMenuForProbe();
            QuickTestTrace.Log("link", "probe windows closed");
        }
        catch (Exception e)
        {
            Program.DEBUGLOG(e);
        }
    }
    /// <summary>
    /// 需求 5 的端到端自检（只在 qt_debug.on 下跑）：给一张卡换名字 → **它自己描述里对它的「引用」
    /// 也要跟着换**；再取消改动 → 原样回去。走的正是玩家在手改译名时走的同一条路
    /// （CardNameTranslation.SetOverride → CardsManager.ReapplyNameTranslation），
    /// 改完自己清干净，不留痕迹。
    /// </summary>
    void nameTranslationProbe()
    {
        const int probeId = 65518099;
        const string tempName = "童话书测试";
        try
        {
            YGOSharp.Card before = YGOSharp.CardsManager.Get(probeId);
            if (before == null || before.Id <= 0)
            {
                QuickTestTrace.Log("nametrans", "probe card missing");
                return;
            }
            string beforeName = before.Name ?? "";
            string beforeDesc = before.Desc ?? "";
            bool beforeSelfRef = beforeDesc.IndexOf("「" + beforeName + "」", StringComparison.Ordinal) >= 0;

            YGOSharp.CardNameTranslation.SetOverride(probeId, tempName);
            YGOSharp.CardsManager.ReapplyNameTranslation();
            YGOSharp.Card after = YGOSharp.CardsManager.Get(probeId);
            string afterName = after != null ? (after.Name ?? "") : "";
            string afterDesc = after != null ? (after.Desc ?? "") : "";

            QuickTestTrace.Log("nametrans", "probe id=" + probeId
                + " nameBefore=" + beforeName
                + " nameAfter=" + afterName
                + " selfRefBefore=" + beforeSelfRef
                + " selfRefAfter=" + (afterDesc.IndexOf("「" + tempName + "」", StringComparison.Ordinal) >= 0)
                + " oldRefLeft=" + (beforeName.Length > 0 && afterDesc.IndexOf("「" + beforeName + "」", StringComparison.Ordinal) >= 0));

            YGOSharp.CardNameTranslation.SetOverride(probeId, "");
            YGOSharp.CardsManager.ReapplyNameTranslation();
            YGOSharp.Card back = YGOSharp.CardsManager.Get(probeId);
            QuickTestTrace.Log("nametrans", "probe restored name=" + (back != null ? back.Name : "")
                + " overrides=" + YGOSharp.CardNameTranslation.OverrideCount
                + " packCount=" + YGOSharp.CardNameTranslation.Packs.Count);
            sweepSpuriousRestore();
            cardPackProbe();
        }
        catch (Exception e)
        {
            Program.DEBUGLOG(e);
        }
    }

    /// <summary>
    /// 系列（字段）改名的端到端自检（需求 3，只在 qt_debug.on 下跑）。
    /// 走玩家那条路：<c>CardNameTranslation.SetFieldOverride</c> → <c>ReapplyNameTranslation</c>。
    /// 三个判据：
    ///   ① 简介正文里「系列名」的引用跟着换（RewriteDesc 的 nameMap 并进了字段译名）；
    ///   ② 简介系列行（<c>GameStringHelper.getSmall</c> 末尾「系列：…」那行）跟着换 ——
    ///     只截那一行来判，免得系列名恰好是别的字段/卡名的子串造成假红；
    ///   ③ 撤掉后原样回去（Desc 逐字复原、字段译名计数归零）。
    /// 改完自己清干净，不留痕迹。
    /// </summary>
    void fieldRenameProbe()
    {
        const string tempName = "字段测试译名";
        try
        {
            // ① 挑一张真挂着系列的卡，拿它的第一个原生系列名当样本。
            //    ⚠ 这张卡要留档（seriesHolderId）：系列行（getSmall 末尾那行）只挂在
            //    **属于该系列的卡**上 —— 简介正文引用它的往往是别的不带系列的卡
            //   （2026-10-02 实测：魔法卡「瞬间移动」被一张 Setcode==0 的卡引用）。
            string field = null;
            int seriesHolderId = 0;
            YGOSharp.CardsManager.ForEachActiveCard((id, c) =>
            {
                if (field != null || c == null || c.Setcode == 0)
                {
                    return;
                }
                List<string> names = GameStringHelper.getSetNames(c.Setcode);
                if (names != null && names.Count > 0)
                {
                    field = names[0];
                    seriesHolderId = id;
                }
            });
            if (string.IsNullOrEmpty(field))
            {
                QuickTestTrace.Log("nametrans", "fieldProbe skip: 池里没有带系列的卡");
                return;
            }
            // ② 找一张简介**原生文**里真的引用了这个系列名的卡（找不到就只验系列行）。
            int holderId = 0;
            YGOSharp.CardsManager.ForEachActiveCard((id, c) =>
            {
                if (holderId != 0 || c == null)
                {
                    return;
                }
                string d = !string.IsNullOrEmpty(c.nativeDesc) ? c.nativeDesc : (c.Desc ?? "");
                if (d.IndexOf("「" + field + "」", StringComparison.Ordinal) >= 0)
                {
                    holderId = id;
                }
            });
            string holderBefore = holderId > 0 ? (YGOSharp.CardsManager.Get(holderId).Desc ?? "") : "";
            bool refBefore = holderBefore.IndexOf("「" + field + "」", StringComparison.Ordinal) >= 0;

            YGOSharp.CardNameTranslation.SetFieldOverride(field, tempName);
            YGOSharp.CardsManager.ReapplyNameTranslation();
            YGOSharp.Card holderAfter = holderId > 0 ? YGOSharp.CardsManager.Get(holderId) : null;
            string descAfter = holderAfter != null ? (holderAfter.Desc ?? "") : "";
            // 系列行只挂在**属于该系列**的那张卡上（见上面 ① 的留档），不是正文引用者。
            YGOSharp.Card seriesHolder = seriesHolderId > 0 ? YGOSharp.CardsManager.Get(seriesHolderId) : null;
            // 只截「系列：」那一行 —— getSmall 前半还有种族/属性/别名卡名，
            // 系列名若是它们的子串，整串判会假红。
            string small = seriesHolder != null ? GameStringHelper.getSmall(seriesHolder) : "";
            int xi = GameStringHelper.xilie.Length > 0
                ? small.IndexOf(GameStringHelper.xilie, StringComparison.Ordinal) : -1;
            string seriesLine = xi >= 0 ? small.Substring(xi) : "";
            QuickTestTrace.Log("nametrans", "fieldProbe field=" + field
                + " holder=" + holderId
                + " seriesHolder=" + seriesHolderId
                + " cnt=" + YGOSharp.CardNameTranslation.FieldOverrideCount
                + " refBefore=" + refBefore
                + " refAfter=" + (descAfter.IndexOf("「" + tempName + "」", StringComparison.Ordinal) >= 0)
                + " oldRefLeft=" + (descAfter.IndexOf("「" + field + "」", StringComparison.Ordinal) >= 0)
                + " seriesLineAfter=" + (seriesLine.IndexOf(tempName, StringComparison.Ordinal) >= 0)
                + " seriesNativeLeft=" + (seriesLine.IndexOf(field, StringComparison.Ordinal) >= 0)
                + " seriesLine=" + seriesLine.Replace("\n", "\\n").Replace("\t", "\\t"));

            YGOSharp.CardNameTranslation.SetFieldOverride(field, "");
            YGOSharp.CardsManager.ReapplyNameTranslation();
            string descBack = holderId > 0 ? (YGOSharp.CardsManager.Get(holderId).Desc ?? "") : "";
            QuickTestTrace.Log("nametrans", "fieldProbe restored"
                + " cnt=" + YGOSharp.CardNameTranslation.FieldOverrideCount
                + " refBack=" + (descBack.IndexOf("「" + field + "」", StringComparison.Ordinal) >= 0)
                + " descSame=" + (descBack == holderBefore));
        }
        catch (Exception e)
        {
            QuickTestTrace.Log("nametrans", "fieldProbe failed " + e.Message);
        }
    }

    /// <summary>
    /// 「单卡用哪一份翻译」的自查（需求 2026-10-02 第 4 条）。
    /// 走的是玩家那条路：<c>SetCardPack</c> → <c>ReapplyNameTranslation</c> → 读回卡名，
    /// 然后自己清干净。判据是**三层优先级**：
    ///   自定义外号 &gt; 单卡指定的表 &gt; 全局表 &gt; 原文
    /// 所以只要 translation/ 里有第二份带数据��表，就能验出"单卡指定真的压过全局"。
    /// 没有第二份表时只报"不可验"，不算失败。
    /// </summary>
    void cardPackProbe()
    {
        const int probeId = 65518099;
        try
        {
            List<YGOSharp.CardNameTranslation.Pack> packs = YGOSharp.CardNameTranslation.Packs;
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            YGOSharp.CardNameTranslation.Pack second = null;
            for (int i = 0; i < packs.Count; i++)
            {
                YGOSharp.CardNameTranslation.Pack p = packs[i];
                if (sb.Length > 0) { sb.Append(','); }
                sb.Append(p.key).Append('/').Append(p.label)
                  .Append(p.missing ? "(未装)" : "(" + p.Count + ")");
                if (second == null && !p.isNative && !p.missing && p.Count > 0)
                {
                    second = p;
                }
            }
            QuickTestTrace.Log("nametrans", "packs cur=" + YGOSharp.CardNameTranslation.CurrentKey
                + " list=[" + sb + "] secondUsable=" + (second != null ? second.key : "(无)"));

            YGOSharp.Card c0 = YGOSharp.CardsManager.Get(probeId);
            string native = c0 != null && c0.nativeName != null ? c0.nativeName : "";
            // 要验"单卡指定压过全局"就至少得有两份**带数据**的表：一份给单卡指定、
            // 一份给全局切过去当对照。只有一份时下面只报"不可验"。
            YGOSharp.CardNameTranslation.Pack third = null;
            for (int i = 0; i < packs.Count; i++)
            {
                if (second != null && packs[i] != second && !packs[i].isNative
                    && !packs[i].missing && packs[i].Count > 0)
                {
                    third = packs[i];
                    break;
                }
            }
            if (second == null || third == null)
            {
                // 不足两份：验不了"压过全局"，但仍要验「设了 / 撤了不脏盘」。
                YGOSharp.CardNameTranslation.SetCardPack(probeId, "native");
                bool setOk = YGOSharp.CardNameTranslation.HasCardPack(probeId);
                YGOSharp.CardNameTranslation.SetCardPack(probeId, "");
                QuickTestTrace.Log("nametrans", "cardpack setOk=" + setOk
                    + " clearedCount=" + YGOSharp.CardNameTranslation.CardPackCount
                    + " (只有 " + (second != null ? 1 : 0) + " 份带数据的表，压过全局那项验不了)");
                return;
            }
            string globalKeyBefore = YGOSharp.CardNameTranslation.CurrentKey;
            string fromGlobal = YGOSharp.CardNameTranslation.DisplayName(probeId, native);

            YGOSharp.CardNameTranslation.SetCardPack(probeId, second.key);
            YGOSharp.CardsManager.ReapplyNameTranslation();
            string fromCard = YGOSharp.CardsManager.Get(probeId).Name ?? "";

            // 换全局：单卡指定的那份必须**不受影响**（这一档存在的意义就在这儿）。
            YGOSharp.CardNameTranslation.SetCurrent(third.key);
            YGOSharp.CardsManager.ReapplyNameTranslation();
            string afterSwitch = YGOSharp.CardsManager.Get(probeId).Name ?? "";

            // 先把全局切回去，再撤掉单卡指定 —— 顺序反了就该拿"全局那份"去比"全局那份"，
            // 变成恒真的假判据。
            YGOSharp.CardNameTranslation.SetCurrent(globalKeyBefore);
            YGOSharp.CardsManager.ReapplyNameTranslation();
            string withCardPackBackOnGlobal = YGOSharp.CardsManager.Get(probeId).Name ?? "";

            YGOSharp.CardNameTranslation.SetCardPack(probeId, "");
            YGOSharp.CardsManager.ReapplyNameTranslation();
            string afterClear = YGOSharp.CardsManager.Get(probeId).Name ?? "";

            QuickTestTrace.Log("nametrans", "cardpack id=" + probeId
                + " native=" + native
                + " fromGlobal(" + globalKeyBefore + ")=" + fromGlobal
                + " fromCardPack(" + second.key + ")=" + fromCard
                + " afterGlobalSwitch(" + third.key + ")=" + afterSwitch
                + " cardPackSurvivesGlobalSwitch=" + (afterSwitch == fromCard)
                + " withCardPackBackOnGlobal=" + withCardPackBackOnGlobal
                + " cardPackStillWins=" + (withCardPackBackOnGlobal == fromCard)
                + " afterClear=" + afterClear
                + " backToGlobal=" + (afterClear == fromGlobal)
                + " differsFromNative=" + (fromCard != native)
                + " count=" + YGOSharp.CardNameTranslation.CardPackCount);
        }
        catch (Exception e)
        {
            QuickTestTrace.Log("nametrans", "cardpack failed " + e.Message);
        }
    }

    /// <summary>
    /// 「没改过的卡名也提示恢复」的自查：清干净之后把**整池**扫一遍，看有没有哪张卡
    /// 在 overrides 为空时仍然 HasOverride == true —— 那就是菜单里会凭空冒出
    /// 「恢复本卡默认译名」的卡。只报数与样本，别把一万多张全打出来。
    /// </summary>
    void sweepSpuriousRestore()
    {
        try
        {
            int total = 0;
            int bad = 0;
            List<string> samples = new List<string>();
            YGOSharp.CardsManager.ForEachActiveCard((id, c) =>
            {
                if (c == null)
                {
                    return;
                }
                total++;
                if (!YGOSharp.CardNameTranslation.HasOverride(id))
                {
                    return;
                }
                bad++;
                if (samples.Count < 6)
                {
                    samples.Add(id + "(" + (c.nativeName ?? "") + ")->"
                        + YGOSharp.CardNameTranslation.GetOverride(id)
                        + " root=" + YGOSharp.CardsManager.AliasRootOf(id)
                        + " size=" + YGOSharp.CardsManager.AliasGroupSize(id));
                }
            });
            QuickTestTrace.Log("nametrans", "spuriousRestore overrides="
                + YGOSharp.CardNameTranslation.OverrideCount
                + " scanned=" + total + " flagged=" + bad
                + " samples=[" + string.Join(" | ", samples.ToArray()) + "]");
        }
        catch (Exception e)
        {
            QuickTestTrace.Log("nametrans", "spuriousRestore failed " + e.Message);
        }
    }

    /// <summary>
    /// 验收用的展示卡：描述里带「X」卡 / 组合种类的那一张（见 <see cref="FindHighlightShowcase"/>）。
    /// 收尾那一帧要把它重新摆到面板上，所以得跨方法存一份。
    /// </summary>
    static YGOSharp.Card ShowcaseCard;

    /// <summary>
    /// 找一张描述里带「X」卡 或「X」魔法·陷阱卡 这类**带种类短语的引用**的卡，
    /// 用来做"高亮范围"肉眼验收的展示卡。挑不出就返回 null（调用方退回原卡）。
    /// 判据直接照 <see cref="CardTextLinker"/> 的规则写：找 `」` 后面紧跟
    /// 「卡 / 卡片 / 魔法 / 陷阱 / 魔法·陷阱」的位置。
    /// </summary>
    static YGOSharp.Card FindHighlightShowcase(YGOSharp.Card fallback)
    {
        YGOSharp.Card mixed = null;
        YGOSharp.Card cardTail = null;
        try
        {
            YGOSharp.CardsManager.ForEachActiveCard((id, c) =>
            {
                if (c == null || string.IsNullOrEmpty(c.Desc))
                {
                    return;
                }
                if (mixed != null && cardTail != null)
                {
                    return;
                }
                string d = c.Desc;
                int at = d.IndexOf('」');
                while (at >= 0)
                {
                    int next = at + 1;
                    if (next < d.Length)
                    {
                        if (mixed == null && (d[next] == '魔' || d[next] == '陷'))
                        {
                            mixed = c;
                        }
                        else if (cardTail == null && d[next] == '卡')
                        {
                            cardTail = c;
                        }
                    }
                    at = d.IndexOf('」', next);
                    if (mixed != null && cardTail != null)
                    {
                        return;
                    }
                }
            });
        }
        catch (Exception e)
        {
            Program.DEBUGLOG(e);
        }
        YGOSharp.Card pick = mixed != null ? mixed : cardTail;
        if (pick != null)
        {
            ShowcaseCard = pick;
            QuickTestTrace.Log("link", "showcase id=" + pick.Id + " name=" + pick.Name
                + " kind=" + (mixed != null ? "组合种类" : "收尾「卡」")
                + " urlTags=" + CardTextLinker.CountOccur(CardTextLinker.Linkify(pick.Desc, pick.Id), "[url="));
        }
        return pick != null ? pick : fallback;
    }

    /// <summary>
    /// 展示卡的**兜底**挑法：只要描述里能链出东西就行（不挑「」+种类短语那类特例）。
    /// RD 档的链接是（）括号，上面那支 <see cref="FindHighlightShowcase"/> 认的是「」，
    /// 在 RD 池里一张也挑不出来 ⇒ 这一种才用得上。
    /// </summary>
    static YGOSharp.Card FindLinkShowcase()
    {
        YGOSharp.Card pick = null;
        try
        {
            YGOSharp.CardsManager.ForEachActiveCard((id, c) =>
            {
                if (pick != null || c == null || string.IsNullOrEmpty(c.Desc))
                {
                    return;
                }
                string rich = CardTextLinker.Linkify(c.Desc, c.Id);
                if (rich.IndexOf("[url=") >= 0)
                {
                    pick = c;
                }
            });
        }
        catch (Exception e)
        {
            Program.DEBUGLOG(e);
        }
        if (pick != null)
        {
            ShowcaseCard = pick;
        }
        return pick;
    }

    /// <summary>
    /// 截图探针：挑一张**当前池里描述能链出东西**的卡摆到说明面板上并 <c>show()</c>，
    /// 再把描述文字的屏幕矩形打出来供截图脚本裁图。
    ///
    /// ⛔ 为什么要单独挂一个：开机自检那条链（`onAcceptanceProbe`）跑在**冷启动的 OCG** 下，
    ///   切到 RD 之后不会重跑 ⇒ RD 的（）括号只能靠这条来拍。所以它挂在 Menu 的
    ///   `mode …` 分支里（切完模式、池已是 RD 池的那一刻）。
    /// ⛔ 只在 <c>log/linkunderline.probe</c> 下干活，正式包零开销。
    /// </summary>
    public static void ProbeShowLinkCard()
    {
        if (!ProbeSwitch("linkunderline.probe"))
        {
            return;
        }
        try
        {
            CardDescription cd = Program.I() != null ? Program.I().cardDescription : null;
            if (cd == null)
            {
                QuickTestTrace.Log("link", "showlink no cardDescription");
                return;
            }
            YGOSharp.Card sc = FindLinkShowcase();
            if (sc == null)
            {
                QuickTestTrace.Log("link", "showlink no card with url");
                return;
            }
            cd.setData(sc, GameTextureManager.myBack, "", true);
            cd.show();
            string rich = CardTextLinker.Linkify(sc.Desc, sc.Id);
            QuickTestTrace.Log("link", "showlink id=" + sc.Id + " name=" + sc.Name
                + " isRD=" + GameModeManager.IsRD
                + " urlTags=" + CardTextLinker.CountOccur(rich, "[url=")
                + " bracketInU=" + (rich.IndexOf("[u]（", StringComparison.Ordinal) >= 0
                    || rich.IndexOf("[u]「", StringComparison.Ordinal) >= 0
                    || rich.IndexOf("）[/u]", StringComparison.Ordinal) >= 0
                    || rich.IndexOf("」[/u]", StringComparison.Ordinal) >= 0));
            // ⛔ descRect 不能**同步**读：`show()` 只是把进场补间发出去，那一刻面板还在场外
            //   （实测 x=-370）⇒ 量到的是停泊位，不是玩家看到的位置。延后到补间跑完再量。
            Program.go(1600, () =>
            {
                QuickTestTrace.Log("link", "descRect " + cd.DescLabelScreenRect());
            });
            cd.underlineDiagProbe();
        }
        catch (Exception e)
        {
            ProbeEx("showlink", e);
        }
    }

    /// <summary>
    /// 下划线颜色诊断（<c>underline_diag.probe</c> 门控）：把描述 label 换成三行**受控标记串**，
    /// 一次截图分辨「落袋色对不对」vs「渲染层有没有问题」：
    ///   行1 <c>甲[色][u][-]乙丙[色]丁戊[-]己庚[/u]辛</c> —— [u] 处落袋（fc=色）；
    ///   乙丙/丁戊/己庚三段的线**应当同色**（紫）。全白=落袋拿到白；分段异色=[-]清了登记。
    ///   行2 <c>壬[FF0000]癸子[u]丑寅[/u]卯</c> —— 原生对照：[u] 在色段内，丑寅=红字红线。
    ///   行3 <c>辰[u]巳午[/u]未</c> —— [u] 前无色：线=白（预期基线）。
    /// </summary>
    void underlineDiagProbe()
    {
        if (!ProbeSwitch("underline_diag.probe"))
        {
            return;
        }
        NGUIText.UnderlineDiag = true;
        // ① 先把**真实链路**上两个串各自的标记摊到日志里：LabelBuilder 产出来的
        //   （Linkify 的结果）vs 真正被塞进 UILabel 的（description.textLabel.text）。
        //   ⛔ 描述不是直给 label 的：`description.Add()` 走 UITextList —— 它先把段落按宽度
        //   断成物理行（Rebuild 里 WrapText），UpdateVisibleText 再只把**可见行**拼回一个串。
        //   所以"同一份标记"在 label 里可能**不是**我们写进去的那个顺序/样子，
        //   这一步就是用来证伪（或坐实）这一层的。
        Program.go(400, () =>
        {
            try
            {
                if (currentCard == null)
                {
                    QuickTestTrace.Log("link", "uskell no currentCard");
                    return;
                }
                // ⛔ rich 必须与 <see cref="apply"/> **同一拼法**（系列行链接化 + 正文富化）：
                //   2026-10-04 起简介系列行也带 [url=] 链接，若 rich 只算正文，
                //   lab 里会多出系列行的 U/u 标记 ⇒ E2/E3 这类"骨架一致"判据全体假红。
                string smallstr = GameStringHelper.getName(currentCard)
                    + GameStringHelper.getSmall(currentCard);
                smallstr = CardTextLinker.LinkifySeriesLine(smallstr, currentCard.Setcode);
                string rich = smallstr
                    + CardTextLinker.Linkify(currentCard.Desc ?? "", currentCard.Id);
                UILabel lab = description != null ? description.textLabel : null;
                string shown = lab != null ? lab.text : "";
                QuickTestTrace.Log("link", "uskell rich " + TagSkeleton(rich));
                QuickTestTrace.Log("link", "uskell lab  " + TagSkeleton(shown));
                QuickTestTrace.Log("link", "uskell n rich U=" + CardTextLinker.CountOccur(rich, "[url=")
                    + " u=" + CardTextLinker.CountOccur(rich, "[u]")
                    + " #u-=" + CardTextLinker.CountOccur(rich, "[u][-]")
                    + " || lab U=" + CardTextLinker.CountOccur(shown, "[url=")
                    + " u=" + CardTextLinker.CountOccur(shown, "[u]")
                    + " #u-=" + CardTextLinker.CountOccur(shown, "[u][-]"));
            }
            catch (Exception e)
            {
                ProbeEx("uskell", e);
            }
        });
        // ⛔ 换文本**必须单独再开一把锁**（`udiag_synth.probe`）：只放 udiag 时它不许动屏上的字，
        //   否则"真卡文的线到底什么色"这个最要紧的像素证据会被合成串覆盖掉 ——
        //   上一轮正是无条件下手，拍出来的图全是合成串，等于白跑一趟。
        if (!ProbeSwitch("udiag_synth.probe"))
        {
            return;
        }
        Program.go(2400, () =>
        {
            try
            {
                UILabel lab = description != null ? description.textLabel : null;
                if (lab == null)
                {
                    QuickTestTrace.Log("link", "udiag no label");
                    return;
                }
                lab.text = "甲[9C4DCC][u][-]乙丙[9C4DCC]丁戊[-]己庚[/u]辛\n"
                    + "壬[FF0000]癸子[u]丑寅[/u]卯\n"
                    + "辰[u]巳午[/u]未";
                QuickTestTrace.Log("link", "udiag set text rect=" + DescLabelScreenRect());
            }
            catch (Exception e)
            {
                ProbeEx("udiag", e);
            }
        });
    }

    /// <summary>
    /// 把一串富文本里的**标记**压成一串可离线的符号，用来比对「写出去的」和「label 收到的」：
    /// <c>U</c>=<c>[url=…]</c>、<c>/U</c>=<c>[/url]</c>、<c>u</c>=<c>[u]</c>、<c>/u</c>=<c>[/u]</c>、
    /// <c>-</c>=<c>[-]</c>、<c>Crr</c>=颜色标记（rr 是头两位十六进制）、<c>|</c>=换行、
    /// <c>.</c>=普通字（连续多个缩成一个点，方便肉眼比）。
    /// </summary>
    static string TagSkeleton(string s)
    {
        if (string.IsNullOrEmpty(s))
        {
            return "(empty)";
        }
        System.Text.StringBuilder sb = new System.Text.StringBuilder(s.Length);
        bool pendingText = false;
        int i = 0;
        while (i < s.Length)
        {
            char c = s[i];
            if (c == '\n')
            {
                if (pendingText) { sb.Append('.'); pendingText = false; }
                sb.Append('|');
                i++;
                continue;
            }
            if (c != '[')
            {
                pendingText = true;
                i++;
                continue;
            }
            int e = s.IndexOf(']', i);
            if (e < 0)
            {
                pendingText = true;
                break;
            }
            if (pendingText) { sb.Append('.'); pendingText = false; }
            string tag = s.Substring(i + 1, e - i - 1);
            if (tag.Length >= 4 && tag[0] == 'u' && tag[1] == 'r' && tag[2] == 'l' && tag[3] == '=')
            {
                sb.Append('U');
            }
            else if (tag == "/url")
            {
                sb.Append("/U");
            }
            else if (tag == "u")
            {
                sb.Append('u');
            }
            else if (tag == "/u")
            {
                sb.Append("/u");
            }
            else if (tag == "-")
            {
                sb.Append('-');
            }
            else
            {
                sb.Append('C').Append(tag.Length >= 2 ? tag.Substring(0, 2) : tag);
            }
            i = e + 1;
        }
        if (pendingText)
        {
            sb.Append('.');
        }
        string all = sb.ToString();
        return all.Length > 900 ? all.Substring(0, 900) + "…(+" + (all.Length - 900) + ")" : all;
    }

    /// <summary>
    /// 找一张**同一 payload 出现两次**的卡（2026-10-03「连点同一处才关窗」的验收用）。
    ///
    /// ⛔ 为什么非要这种卡：需求里专门点名了两种"不算同一处"的情况 ——
    ///   ① 同一张卡的**两处不同**链接；② **两个不同的链接但搜出来一样**。
    ///   只有"同 payload 出现两次"的卡才能同时覆盖①②（payload 一样 ⇒ 搜出来必然一样，
    ///   但它是两处 ⇒ 点了不许关）。找不着就返回 null（探针会跳过）。
    /// </summary>
    static YGOSharp.Card FindDupPayloadShowcase()
    {
        YGOSharp.Card pick = null;
        try
        {
            YGOSharp.CardsManager.ForEachActiveCard((id, c) =>
            {
                if (pick != null || c == null || string.IsNullOrEmpty(c.Desc))
                {
                    return;
                }
                string rich = CardTextLinker.Linkify(c.Desc, c.Id);
                int from = 0;
                while (true)
                {
                    int s = rich.IndexOf("[url=", from, StringComparison.Ordinal);
                    if (s < 0)
                    {
                        break;
                    }
                    int ps = s + 5;
                    int pe = rich.IndexOf(']', ps);
                    if (pe < 0)
                    {
                        break;
                    }
                    string pl = rich.Substring(ps, pe - ps);
                    // 后面还能再找到一份一模一样的 payload ⇒ 就是它
                    if (rich.IndexOf("[url=" + pl + "]", pe, StringComparison.Ordinal) >= 0)
                    {
                        pick = c;
                        return;
                    }
                    from = pe + 1;
                }
            });
        }
        catch (Exception e)
        {
            Program.DEBUGLOG(e);
        }
        return pick;
    }

    /// <summary>开机自检那条收尾（<c>autoclean probe windows</c>）跑完了没有。见 <see cref="WaitProbeAutoclean"/>。</summary>
    static bool probeAutocleanDone = false;

    /// <summary>
    /// 验收用：等开机自检那整条链**跑完**再干 <paramref name="go"/>。
    ///
    /// <para>⛔ 为什么非等不可（2026-10-04 serieslink 探针实际踩到）：开机自检链会自己做
    /// 检索（`searchprobe`）、开关检索窗、往说明面板里换卡，而且全部**只挂在
    /// `qt_debug.on` 上、没有任何开关**。探针若与它交叠，读到的 window 状态/面板文本
    /// 就是它留下的（实测 `[serieslink] S2 filter=Text q=[机壳]`＝被它换掉了），
    /// 判据会假红/假绿。</para>
    ///
    /// <para>它是**一次性**的 ⇒ 等它落袋之后场子就干净了（同 <see cref="WaitProbeAutoclean"/>
    /// 的结论）。这里不带 run 号闸门：serieslink 探针自己只挂一次。</para>
    /// </summary>
    public void ProbeAfterAutoclean(System.Action go)
    {
        ProbeAfterAutoclean(go, 0);
    }

    void ProbeAfterAutoclean(System.Action go, int tries)
    {
        if (probeAutocleanDone || tries >= 100)
        {
            QuickTestTrace.Log("serieslink", "wait autoclean done=" + probeAutocleanDone
                + " tries=" + tries);
            go();
            return;
        }
        Program.go(200, () => ProbeAfterAutoclean(go, tries + 1));
    }

    /// <summary>
    /// 等到"开机收尾"跑完再干 <paramref name="go"/>。
    ///
    /// ⛔ 为什么非等不可：那条收尾会 `cs0.hide()` 收掉检索窗，而 `Program.go` 的延时任务
    ///   会被主场景加载 / 切模式**拖到很晚**才执行（2026-10-03 实证：RD 档它比预期晚了约
    ///   0.6s，正好插在点击序列第 1 击和第 2 击之间 ⇒ 第 2 击读到的 `isShowed` 已经被它
    ///   清成 false，"再点一次关窗"被判成没关，读数整体错位一格）。
    ///   它是**一次性**的，等它落袋之后场子就干净了。
    /// </summary>
    void WaitProbeAutoclean(int run, System.Action go)
    {
        WaitProbeAutoclean(run, go, 0);
    }

    void WaitProbeAutoclean(int run, System.Action go, int tries)
    {
        if (run != linkToggleRun)
        {
            return;   // 已被新一轮顶掉
        }
        if (tries == 0)
        {
            // 先给面板进场补间一点时间（show() 是 0.6s 动画，量早了坐标是停泊位）。
            Program.go(1500, () => WaitProbeAutoclean(run, go, 1));
            return;
        }
        if (probeAutocleanDone || tries >= 80)
        {
            QuickTestTrace.Log("linktoggle", "wait autoclean done=" + probeAutocleanDone
                + " tries=" + tries);
            go();
            return;
        }
        Program.go(500, () => WaitProbeAutoclean(run, go, tries + 1));
    }

    /// <summary>
    /// 第几轮 <see cref="linkToggleProbe"/>。每进一次就 +1 ⇒ **上一轮还在排队的延时回调自动作废**
    /// （2026-10-03 踩过：开机 OCG 那一轮的 go() 回调会被随后的切模式拖到很久之后才执行，
    ///   那时面板上早换成了 RD 的卡，两组点击读数交叠 ⇒ 验收脚本取到混行，T2/T3 全红。
    ///   光靠"只跑一次"的布尔闸门挡不住：闸门只挡入口，挡不住已经排进队列的回调）。
    /// </summary>
    static int linkToggleRun = 0;

    /// <summary>
    /// <see cref="linkToggleProbe"/> 的静态入口（挂 Menu <c>mode</c> 分支用，RD 档切完模式才跑得到）。
    /// </summary>
    public static void ProbeLinkToggle()
    {
        if (!ProbeSwitch("linktoggle.probe"))
        {
            return;
        }
        linkToggleRun++;
        CardDescription cd = Program.I() != null ? Program.I().cardDescription : null;
        if (cd != null)
        {
            cd.linkToggleProbe(linkToggleRun);
        }
    }

    /// <summary>
    /// 「连点同一处链接 = 关掉检索窗」的进程内验收（只在 <c>log/linktoggle.probe</c> 下跑）。
    ///
    /// ⛔ 合成鼠标事件送不进这个客户端（notes/harness.md），所以这里是
    ///   **摆好 <c>UICamera.lastWorldPosition</c> 再直接调 <c>CardLinkClick.HandleClick()</c>**。
    ///   为了让落点真的对应到"第 N 个链接"，先在标签上按网格扫一遍，
    ///   把每个顺序号第一次出现的世界坐标记下来（见 <see cref="ScanLinkPositions"/>）。
    ///
    /// 判据（每条都打 [linktoggle]）：
    ///   T1 找得到"同 payload 两处"的卡，且扫出 ≥2 个不同顺序号
    ///   T2 点 A ⇒ 窗开
    ///   T3 **再点 A ⇒ 窗关**（这就是需求）
    ///   T4 点 A 开 ⇒ 点 B（另一个顺序号、**同一 payload**）⇒ 窗**仍开着**
    ///   T5 点 A 开 ⇒ 点 C（又一个不同的 payload）⇒ 窗**仍开着**
    /// </summary>
    void linkToggleProbe(int run)
    {
        if (!ProbeSwitch("linktoggle.probe"))
        {
            return;
        }
        try
        {
            YGOSharp.Card sc = FindDupPayloadShowcase();
            if (sc == null)
            {
                QuickTestTrace.Log("linktoggle", "T1 XX 找不到「同 payload 出现两次」的卡");
                return;
            }
            setData(sc, GameTextureManager.myBack, "", true);
            show();
            string rich = CardTextLinker.Linkify(sc.Desc, sc.Id);
            QuickTestTrace.Log("linktoggle", "T1 card=" + sc.Id + " name=" + sc.Name
                + " urls=" + CardTextLinker.CountOccur(rich, "[url="));

            // 面板 show() 之后要等进场补间落定，量到的字符坐标才是对的。
            WaitProbeAutoclean(run, () =>
            {
                if (run != linkToggleRun)
                {
                    return;   // 已被新一轮顶掉
                }
                try
                {
                    UILabel lab = description != null ? description.textLabel : null;
                    CardLinkClick clk = lab != null ? lab.GetComponent<CardLinkClick>() : null;
                    CardSearchWindow csw = Program.I() != null ? Program.I().cardSearch : null;
                    if (lab == null || clk == null || csw == null)
                    {
                        QuickTestTrace.Log("linktoggle", "T1 XX 缺 label/click/searchwindow");
                        return;
                    }
                    List<int> ords = new List<int>();
                    List<Vector3> poss = new List<Vector3>();
                    List<string> pls = new List<string>();
                    ScanLinkPositions(lab, ords, poss, pls);
                    QuickTestTrace.Log("linktoggle", "T1 scanned ords=" + ords.Count);

                    // 挑同 payload 的两个不同顺序号（A / B），再挑一个 payload 不同的（C）
                    int ia = -1, ib = -1, ic = -1;
                    for (int i = 0; i < ords.Count && ia < 0; i++)
                    {
                        for (int j = i + 1; j < ords.Count; j++)
                        {
                            if (pls[i] == pls[j])
                            {
                                ia = i;
                                ib = j;
                                break;
                            }
                        }
                    }
                    for (int i = 0; i < ords.Count; i++)
                    {
                        if (ia >= 0 && pls[i] != pls[ia])
                        {
                            ic = i;
                            break;
                        }
                    }
                    if (ia < 0)
                    {
                        QuickTestTrace.Log("linktoggle", "T1 XX 扫不出「同 payload 的两处」");
                        return;
                    }
                    // ⛔ C（异 payload）不是每张卡都有 —— 挑中的卡若只有两处同 payload 链接就没有。
                    //   那种卡照样能验需求本体（T4b 就是「两处不同但搜出来一样 ⇒ 不许关」），
                    //   T5 只是补充 ⇒ 没有就跳过，别把整条探针判红。
                    if (ic < 0)
                    {
                        QuickTestTrace.Log("linktoggle", "T5 skip 本卡没有异 payload 的链接（只有同 payload 两处）");
                    }
                    QuickTestTrace.Log("linktoggle", "T1 A ord=" + ords[ia] + " payload=" + pls[ia]
                        + " | B ord=" + ords[ib] + " payload=" + pls[ib]
                        + " | C ord=" + (ic >= 0 ? ords[ic].ToString() : "none")
                        + " payload=" + (ic >= 0 ? pls[ic] : "none"));

                    // 每一步之间留 900ms：关窗/开窗都是 0.6s 补间，读早了量到的是过渡态。
                    List<int> seqL = new List<int>();
                    List<string> tagL = new List<string>();
                    seqL.Add(ia); tagL.Add("T2");    // 点 A ⇒ 开
                    seqL.Add(ia); tagL.Add("T3");    // **再点 A ⇒ 关**（需求本体）
                    seqL.Add(ia); tagL.Add("T4a");   // 再点 A ⇒ 开
                    seqL.Add(ib); tagL.Add("T4b");   // 点 B（同 payload 的另一处）⇒ 不许关
                    if (ic >= 0)
                    {
                        seqL.Add(ia); tagL.Add("T5a");
                        seqL.Add(ic); tagL.Add("T5b"); // 点 C（异 payload）⇒ 不许关
                    }
                    int[] seq = seqL.ToArray();
                    string[] tag = tagL.ToArray();
                    for (int k = 0; k < seq.Length; k++)
                    {
                        int idx = seq[k];
                        string tg = tag[k];
                        Program.go(900 * (k + 1), () =>
                        {
                            if (run != linkToggleRun)
                            {
                                return;   // 已被新一轮顶掉
                            }
                            try
                            {
                                UICamera.lastWorldPosition = poss[idx];
                                clk.HandleClick();
                                QuickTestTrace.Log("linktoggle", tg + " click ord=" + ords[idx]
                                    + " payload=" + pls[idx]
                                    + " showed=" + (Program.I().cardSearch.isShowed ? 1 : 0));
                            }
                            catch (Exception e2)
                            {
                                ProbeEx("click", e2);
                            }
                        });
                    }
                    Program.go(900 * (seq.Length + 2), () =>
                    {
                        if (run != linkToggleRun)
                        {
                            return;   // 已被新一轮顶掉
                        }
                        try
                        {
                            if (Program.I().cardSearch.isShowed)
                            {
                                Program.I().cardSearch.hide();
                            }
                        }
                        catch (Exception e3)
                        {
                            ProbeEx("cleanup", e3);
                        }
                    });
                }
                catch (Exception e1)
                {
                    ProbeEx("scan", e1);
                }
            });
        }
        catch (Exception e)
        {
            ProbeEx("linktoggle", e);
        }
    }

    /// <summary>
    /// 在标签上按网格扫一遍，把"每个顺序号第一次出现"的世界坐标记下来。
    /// 顺序号来自 <see cref="CardTextLinker.ResolveClick(UILabel, Vector3, out int)"/>，
    /// 也就是"文本里第几个 [url=" —— 这正是判重的依据。
    /// </summary>
    static void ScanLinkPositions(UILabel lab, List<int> ords, List<Vector3> poss, List<string> pls)
    {
        float w = lab.width;
        float h = lab.height;
        for (float y = -2f; y > -h; y -= 6f)
        {
            for (float x = 1f; x < w; x += 3f)
            {
                Vector3 world = lab.cachedTransform.TransformPoint(new Vector3(x, y, 0f));
                int ord;
                string pl = CardTextLinker.ResolveClick(lab, world, out ord);
                if (pl == null || ord < 0 || ords.Contains(ord))
                {
                    continue;
                }
                ords.Add(ord);
                poss.Add(world);
                pls.Add(pl);
            }
        }
    }

    /// <summary>
    /// 描述文字（<c>description_</c> 的 UILabel）的**屏幕矩形**，给截图脚本裁图用。
    /// ⛔ 别用 <c>Renderer.bounds</c> —— NGUI 的 UILabel 没有 Renderer，恒为 null；
    ///   走 <c>UIWidget.worldCorners</c>（2026-09-30 量阶段大字时踩过同一个坑）。
    /// ⛔ 输出两种 y：Unity 的 <c>WorldToScreenPoint</c> 是**下原点**，截图是**上原点**，
    ///   两个都打出来，免得脚本再猜一次该不该翻。
    /// </summary>
    public string DescLabelScreenRect()
    {
        try
        {
            UILabel lab = description != null ? description.textLabel : null;
            if (lab == null)
            {
                return "none";
            }
            Vector3[] c = lab.worldCorners;
            if (c == null || c.Length < 4 || Program.camera_main_2d == null)
            {
                return "badcorners";
            }
            float minX = float.MaxValue, maxX = float.MinValue;
            float minY = float.MaxValue, maxY = float.MinValue;
            for (int i = 0; i < 4; i++)
            {
                Vector3 s = Program.camera_main_2d.WorldToScreenPoint(c[i]);
                minX = Mathf.Min(minX, s.x); maxX = Mathf.Max(maxX, s.x);
                minY = Mathf.Min(minY, s.y); maxY = Mathf.Max(maxY, s.y);
            }
            return "x=" + Mathf.RoundToInt(minX) + " y=" + Mathf.RoundToInt(minY)
                + " w=" + Mathf.RoundToInt(maxX - minX) + " h=" + Mathf.RoundToInt(maxY - minY)
                + " top=" + Mathf.RoundToInt(Screen.height - maxY)
                + " screenH=" + Screen.height;
        }
        catch (Exception e)
        {
            return "err " + e.Message;
        }
    }

    /// <summary>探针用：控件的 activeSelf 读数（missing / 1 / 0）。</summary>
    static string ActOf(GameObject go)
    {
        return go == null ? "missing" : (go.activeSelf ? "1" : "0");
    }

    /// <summary>
    /// 「译钮 / 撤回钮点一下就没了」的自查：合成的鼠标事件送不进这个客户端，
    /// 所以改成**在进程内直接调点按回调**，前后各报一次钮的真实状态
    /// （activeSelf / activeInHierarchy / 图标 alpha / 三级缩放 / 屏幕坐标）。
    /// 事件能不能到玩家手上另说，先把「点了之后它到底是不是被隐藏了」变成可判的数。
    /// </summary>
    void buttonClickProbe()
    {
        // ⛔⛔ 这一支有**全局副作用**，必须由独立开关放行（`log/btnclick.probe`）：
        //   它 `onNameTranslationClicked()` 会在进程内**把「译」菜单真弹出来**——那是一扇
        //   `RMSshow_singleChoice` 的 `trans_ES_pan(Clone)` 对话框，居中摆在屏幕中央，而且
        //   **本探针从头到尾都不收它**（它只关心点按前后钮自己的 activeSelf）。
        //   2026-10-02 实测（不加闸那版）：`_verify_rdmode.py` 红 12 条，症状全是
        //   「主菜单『设置』按钮落点没确认 / 第二次开设置窗口确认不了 / 设置窗口关不掉」，
        //   而 `[mouse]` 的 hover 恒为 `trans_ES_pan(Clone)/pan/under/trans_ES_singleSelection`
        //   —— 光标压在那扇**从开机起就一直开着**的对话框上。
        //   ⚠ 为什么 OCG 档没露馅、只有 RD 档崩：那张对话框是 493×301 的居中矩形（实测边缘
        //     在客户区 y≈343..643），OCG 主菜单「设置」在 y=657（差 15px 擦边放过），
        //     而 RD 菜单少 3 项、同一颗钮上移到 y=573 ⇒ 正好落在对话框里被吃掉。
        //     同一份日志里 `[nametrans] menu card=43227 rd=0 pack=… items=[pack|cardpack|alias]`
        //     只出现 1 次、时间戳 16:32:16.620，紧跟 `[btnprobe] tree translate_` 之后，
        //     就是本探针干的（当时的鼠标 down 事件一个都没有 ⇒ 不是脚本点出来的）。
        if (!ProbeSwitch("btnclick.probe"))
        {
            QuickTestTrace.Log("btnprobe", "click translate skipped (no btnclick.probe)");
            return;
        }
        try
        {
            GameObject btn = UIHelper.getByName(gameObject, NameTranslationUI.ButtonName);
            if (btn == null)
            {
                QuickTestTrace.Log("btnprobe", "translate button not found");
                return;
            }
            // 层级快照：点按到底落在哪个 GameObject 上（NGUI 靠碰撞体命中，再往上找
            // UIEventTrigger/UIButton），只看根节点上的组件会漏掉子物体那一层。
            QuickTestTrace.Log("btnprobe", "tree translate_ " + dumpTree(btn));
            QuickTestTrace.Log("btnprobe", "tree big_(样板) " + dumpTree(UIHelper.getByName(gameObject, "big_")));
            string before = describeButton("translate_", btn);
            onNameTranslationClicked();
            string after = describeButton("translate_", btn);
            QuickTestTrace.Log("btnprobe", "click translate before[" + before + "] after[" + after + "]");

            // 同一个症状也出现在卡组编辑器检索窗那颗「返回上一级」上：一并量。
            Program.go(1500, () =>
            {
                try
                {
                    CardSearchWindow csw = Program.I() != null ? Program.I().cardSearch : null;
                    if (csw == null)
                    {
                        return;
                    }
                    GameObject backBtn = CardSearchWindow.FindDeep(csw.ProbeWindowObject, CardSearchWindow.BackButtonName);
                    if (backBtn == null)
                    {
                        QuickTestTrace.Log("btnprobe", "back_ not found");
                        return;
                    }
                    // ⛔ 贴图挂在**子节点**的图标上（钮的根只有 Transform + UIButton + BoxCollider）——
                    //   用 `backBtn.GetComponent<UITexture>()` 只会拿到 null，日志就会打出一行
                    //   `tex=NULL panel=none`，看着像产品坏了，其实是探针口径错了（2026-10-02 第六轮）。
                    UITexture bt = backBtn.GetComponentInChildren<UITexture>(true);
                    QuickTestTrace.Log("btnprobe", "back_icon "
                        + "tex=" + (bt != null && bt.mainTexture != null
                            ? "(无名)" + bt.mainTexture.width + "x" + bt.mainTexture.height : "NULL")
                        + " path=" + (bt != null ? bt.path : "-")
                        + " panel=" + (bt != null && bt.panel != null ? bt.panel.name : "none")
                        + " alpha=" + (bt != null ? bt.color.a.ToString("F2") : "-")
                        + " size=" + (bt != null ? bt.width + "x" + bt.height : "-"));
                    QuickTestTrace.Log("btnprobe", "tree back_ " + dumpTree(backBtn));
                    QuickTestTrace.Log("btnprobe", "tree search_(样板) "
                        + dumpTree(UIHelper.getByName(csw.ProbeWindowObject, "search_")));
                    // ── 详情版排版到底生效没有（需求 2）─────────────────────────
                    // 四件事全是 activeSelf 读数：输入框 / 占位文字 / 放大镜 / 分类钮 必须「不显示」，
                    // 返回钮必须「显示」。用户报「输入框放大镜分类都还没消失」时，日志里
                    // 一条读数都没有 ⇒ 只能靠肉眼；补上这四个数就变成可判的了。
                    GameObject dw = csw.ProbeWindowObject;
                    // ⛔ 这里必须用 FindDeep（含失活）：详情版恰恰把 input_/search_/detailed_ 置为失活，
                    //   `UIHelper.getByName` 跳过失活对象 ⇒ 只会打出 missing，分不出「已按需求藏掉」
                    //   和「压根没这个控件」（又是探针口径造出来的假红）。
                    GameObject inG = dw != null ? CardSearchWindow.FindDeep(dw, "input_") : null;
                    GameObject inL = dw != null ? CardSearchWindow.FindDeep(dw, "LabelOfInput") : null;
                    GameObject seG = dw != null ? CardSearchWindow.FindDeep(dw, "search_") : null;
                    GameObject deG = dw != null ? CardSearchWindow.FindDeep(dw, "detailed_") : null;
                    string act = "missing";
                    if (backBtn != null)
                    {
                        act = backBtn.activeSelf ? "1" : "0";
                    }
                    string rect = "n/a";
                    if (bt != null && Program.camera_main_2d != null)
                    {
                        Vector3[] wc = bt.worldCorners;
                        Vector3 c0 = Program.camera_main_2d.WorldToScreenPoint(wc[0]);
                        Vector3 c2 = Program.camera_main_2d.WorldToScreenPoint(wc[2]);
                        rect = "(" + Mathf.RoundToInt(Mathf.Min(c0.x, c2.x)) + ","
                             + Mathf.RoundToInt(Mathf.Min(c0.y, c2.y)) + ")-("
                             + Mathf.RoundToInt(Mathf.Max(c0.x, c2.x)) + ","
                             + Mathf.RoundToInt(Mathf.Max(c0.y, c2.y)) + ")";
                    }
                    QuickTestTrace.Log("btnprobe", "detailstate win="
                        + (dw == null ? "NULL" : (dw.activeInHierarchy ? "on" : "off"))
                        + " input=" + (inG == null ? "missing" : (inG.activeSelf ? "1" : "0"))
                        + " inputLabel=" + (inL == null ? "missing" : (inL.activeSelf ? "1" : "0"))
                        + " search=" + (seG == null ? "missing" : (seG.activeSelf ? "1" : "0"))
                        + " detailed=" + (deG == null ? "missing" : (deG.activeSelf ? "1" : "0"))
                        + " back=" + act + " backRect=" + rect
                        + " scr=" + Screen.width + "x" + Screen.height);
                    QuickTestTrace.Log("btnprobe", "back_ static self=" + backBtn.activeSelf
                        + " hier=" + backBtn.activeInHierarchy
                        + " lp=" + backBtn.transform.localPosition.ToString("F1")
                        + " parent=" + (backBtn.transform.parent != null ? backBtn.transform.parent.name : "-")
                        + " scale=" + backBtn.transform.localScale.ToString("F2"));
                    csw.ProbeGoBackOnce();
                    Program.go(300, () =>
                    {
                        UITexture bt2 = backBtn.GetComponentInChildren<UITexture>(true);
                        QuickTestTrace.Log("btnprobe", "back_ afterGoBack self=" + backBtn.activeSelf
                            + " hier=" + backBtn.activeInHierarchy
                            + " tex=" + (bt2 != null && bt2.mainTexture != null
                                ? "(无名)" + bt2.mainTexture.width + "x" + bt2.mainTexture.height : "NULL")
                            + " panel=" + (bt2 != null && bt2.panel != null ? bt2.panel.name : "none")
                            + " alpha=" + (bt2 != null ? bt2.color.a.ToString("F2") : "-")
                            + " scale=" + backBtn.transform.localScale.ToString("F2"));
                    });
                }
                catch (Exception e)
                {
                    QuickTestTrace.Log("btnprobe", "back_ failed " + e.Message);
                }
            });
        }
        catch (Exception e)
        {
            QuickTestTrace.Log("btnprobe", "translate failed " + e.Message);
        }
    }

    /// <summary>
    /// 递归打印一棵按钮子树：名字 / 层 / 关键组件 / 碰撞体尺寸 / 世界坐标。
    /// 用来判「点按落在哪个 GameObject 上」——NGUI 是靠碰撞体命中后**往上**找
    /// UIEventTrigger / UIButton，所以事件挂在根上、碰撞体在子物体上（或者反过来）
    /// 都会让点按静默失灵，只看根节点的组件是看不出来的。
    /// </summary>
    static string dumpTree(GameObject go)
    {
        if (go == null)
        {
            return "(null)";
        }
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        dumpTreeInto(go, sb, 0);
        return sb.ToString();
    }

    static void dumpTreeInto(GameObject go, System.Text.StringBuilder sb, int depth)
    {
        if (go == null || depth > 4)
        {
            return;
        }
        for (int i = 0; i < depth; i++)
        {
            sb.Append("  ");
        }
        sb.Append(go.name).Append("[L").Append(go.layer).Append(']');
        // 只列与"能不能被点到 / 能不能被画出来"有关的组件，别的（AudioSource 等）不占地方。
        if (go.GetComponent<UIWidget>() != null) { sb.Append(" +Widget"); }
        if (go.GetComponent<UIButton>() != null) { sb.Append(" +Button"); }
        if (go.GetComponent<UIEventTrigger>() != null) { sb.Append(" +Trigger"); }
        if (go.GetComponent<MonoListener>() != null) { sb.Append(" +Listener"); }
        if (go.GetComponent<MonoDelegate>() != null) { sb.Append(" +Delegate"); }
        if (go.GetComponent<UIPlaySound>() != null) { sb.Append(" +Sound"); }
        UITexture t = go.GetComponent<UITexture>();
        if (t != null)
        {
            sb.Append(" +Tex(").Append(t.width).Append('x').Append(t.height)
              .Append(",d").Append(t.depth)
              .Append(",panel=").Append(t.panel != null ? t.panel.name : "NULL")
              .Append(",a=").Append(t.color.a.ToString("F2"))
              .Append(",img=").Append(t.mainTexture != null ? t.mainTexture.name : "NULL")
              .Append(')');
        }
        BoxCollider bc = go.GetComponent<BoxCollider>();
        if (bc != null)
        {
            sb.Append(" +Box(").Append(bc.size.ToString("F0"))
              .Append(",trig=").Append(bc.isTrigger)
              .Append(",en=").Append(bc.enabled).Append(')');
        }
        Vector3 sp = Program.camera_main_2d != null
            ? Program.camera_main_2d.WorldToScreenPoint(go.transform.position)
            : Vector3.zero;
        sb.Append(" scr=(").Append(Mathf.RoundToInt(sp.x)).Append(',').Append(Mathf.RoundToInt(sp.y)).Append(')');
        sb.Append(" act=").Append(go.activeSelf ? 1 : 0);
        sb.Append('\n');
        for (int i = 0; i < go.transform.childCount; i++)
        {
            dumpTreeInto(go.transform.GetChild(i).gameObject, sb, depth + 1);
        }
    }

    /// <summary>按钮此刻的真实状态，一行字符串，方便前后对比。</summary>
    string describeButton(string tag, GameObject btn)
    {
        UITexture icon = btn.GetComponentInChildren<UITexture>(true);
        Vector3 sp = Program.camera_main_2d != null
            ? Program.camera_main_2d.WorldToScreenPoint(btn.transform.position)
            : Vector3.zero;
        return tag
            + " self=" + btn.activeSelf
            + " hier=" + btn.activeInHierarchy
            + " scale=" + btn.transform.localScale.ToString("F2")
            + " alpha=" + (icon != null ? icon.color.a.ToString("F2") : "-")
            + " iconDepth=" + (icon != null ? icon.depth.ToString() : "-")
            + " tex=" + (icon != null && icon.mainTexture != null ? icon.mainTexture.name : "NULL")
            + " panel=" + (icon != null && icon.panel != null ? icon.panel.name : "none")
            + " enabled=" + (icon != null && icon.enabled)
            + " inPanel=" + (icon != null && icon.transform.parent != null)
            + " screen=(" + Mathf.RoundToInt(sp.x) + "," + Mathf.RoundToInt(sp.y) + ")";
    }

    /// <summary>
    /// 需求 1 的自检（只在 qt_debug.on 下跑）：改名要**按异画组**生效 ——
    ///   * 火灵使 希塔 是 3 张一组的异画（759393 组主 + 759394 / 759395）；
    ///   * 「凭依装着-希塔」的描述里记述了「火灵使 希塔」，改完必须跟着变；
    ///   * 新空间侠·海洋海豚（78734254）虽然 alias 指向水波海豚，但**卡名不同**，
    ///     属于「真·不同的卡」，绝不能连坐。
    /// 改完自己清干净，不留痕迹。
    /// </summary>
    void aliasRenameProbe()
    {
        const int groupMain = 759393;     // 火灵使 希塔（组主）
        const int groupAltA = 759394;     // 同组异画
        const int groupAltB = 759395;     // 同组异画
        const int depends = 4376658;      // 凭依装着-希塔：描述里记述了「火灵使 希塔」
        const int dolphin = 78734254;     // 新空间侠·海洋海豚：alias 指水波海豚但卡名不同
        const string tempName = "火灵使测试";
        try
        {
            YGOSharp.Card m0 = YGOSharp.CardsManager.Get(groupMain);
            YGOSharp.Card a0 = YGOSharp.CardsManager.Get(groupAltA);
            YGOSharp.Card d0 = YGOSharp.CardsManager.Get(dolphin);
            if (m0 == null || m0.Id <= 0 || a0 == null || a0.Id <= 0)
            {
                QuickTestTrace.Log("nametrans", "alias probe card missing");
                return;
            }
            string dolphinName = d0 != null ? (d0.Name ?? "") : "";
            QuickTestTrace.Log("nametrans", "alias group main=" + groupMain
                + " size=" + YGOSharp.CardsManager.AliasGroupSize(groupMain)
                + " rootOfAltA=" + YGOSharp.CardsManager.AliasRootOf(groupAltA)
                + " rootOfAltB=" + YGOSharp.CardsManager.AliasRootOf(groupAltB)
                + " dolphinRoot=" + YGOSharp.CardsManager.AliasRootOf(dolphin)
                + " dolphinName=" + dolphinName);

            // 故意从**异画那一张**改（玩家在说明面板上看着哪张就改哪张）。
            YGOSharp.CardNameTranslation.SetOverride(groupAltA, tempName);
            YGOSharp.CardsManager.ReapplyNameTranslation();
            YGOSharp.Card m1 = YGOSharp.CardsManager.Get(groupMain);
            YGOSharp.Card a1 = YGOSharp.CardsManager.Get(groupAltA);
            YGOSharp.Card b1 = YGOSharp.CardsManager.Get(groupAltB);
            YGOSharp.Card dep1 = YGOSharp.CardsManager.Get(depends);
            YGOSharp.Card d1 = YGOSharp.CardsManager.Get(dolphin);
            string depDesc = dep1 != null ? (dep1.Desc ?? "") : "";
            QuickTestTrace.Log("nametrans", "alias rename main=" + (m1 != null ? m1.Name : "")
                + " altA=" + (a1 != null ? a1.Name : "")
                + " altB=" + (b1 != null ? b1.Name : "")
                + " allRenamed=" + (m1 != null && a1 != null && b1 != null
                    && m1.Name == tempName && a1.Name == tempName && b1.Name == tempName)
                + " dependDescFollows=" + (depDesc.IndexOf("「" + tempName + "」", StringComparison.Ordinal) >= 0
                    && depDesc.IndexOf("「火灵使 希塔」", StringComparison.Ordinal) < 0)
                + " dolphinUntouched=" + (d1 != null && (d1.Name ?? "") == dolphinName));

            YGOSharp.CardNameTranslation.SetOverride(groupAltA, "");
            YGOSharp.CardsManager.ReapplyNameTranslation();
            YGOSharp.Card m2 = YGOSharp.CardsManager.Get(groupMain);
            QuickTestTrace.Log("nametrans", "alias restored name=" + (m2 != null ? m2.Name : "")
                + " overrides=" + YGOSharp.CardNameTranslation.OverrideCount);
        }
        catch (Exception e)
        {
            Program.DEBUGLOG(e);
        }
    }

    /// <summary>说明面板那颗「译」钮点下来的口子（UIButton.onClick → MonoDelegate.function）。</summary>
    void onNameTranslationClicked()
    {
        NameTranslationUI.OpenMenu();
    }

    public void shiftCardShower(bool show)
    {
        if (show)
        {
            cardShowerWidget.alpha = 1f;
        }
        else
        {
            cardShowerWidget.alpha = 0f;
        }
        if (!show)
        {
            if (monitor.gameObject.activeInHierarchy == false)
            {
                monitor.gameObject.SetActive(true);
                realizeMonitor();
            }
        }
        else
        {
            monitor.gameObject.SetActive(false);
        }
    }

    string myGraveStr = "";
    string myExtraStr = "";
    string myBanishedStr = "";
    string opGraveStr = "";
    string opExtraStr = "";
    string opBanishedStr = "";


    public void realizeMonitor()
    {
        if (monitor.gameObject.activeInHierarchy)
        {
            List<gameCard> myGrave = new List<gameCard>();
            List<gameCard> myExtra = new List<gameCard>();
            List<gameCard> myBanished = new List<gameCard>();
            List<gameCard> opGrave = new List<gameCard>();
            List<gameCard> opExtra = new List<gameCard>();
            List<gameCard> opBanished = new List<gameCard>();
            for (int i = 0; i < Program.I().ocgcore.cards.Count; i++)
            {
                var curCard = Program.I().ocgcore.cards[i];
                int code = curCard.get_data().Id;
                var gps = curCard.p;
                if (code > 0)
                {
                    if (gps.controller == 0)
                    {
                        if ((gps.location & (UInt32)CardLocation.Grave) > 0)
                        {
                            myGrave.Add(curCard);
                        }
                        if ((gps.location & (UInt32)CardLocation.Removed) > 0)
                        {
                            myBanished.Add(curCard);
                        }
                        if ((gps.location & (UInt32)CardLocation.Extra) > 0)
                        {
                            myExtra.Add(curCard);
                        }
                    }
                    else
                    {
                        if ((gps.location & (UInt32)CardLocation.Grave) > 0)
                        {
                            opGrave.Add(curCard);
                        }
                        if ((gps.location & (UInt32)CardLocation.Removed) > 0)
                        {
                            opBanished.Add(curCard);
                        }
                        if ((gps.location & (UInt32)CardLocation.Extra) > 0)
                        {
                            opExtra.Add(curCard);
                        }
                    }
                }
            }
            currentHeight = 0;
            currentLabelIndex = 0;
            currentCardIndex = 0;
            handleMonitorArea(opGrave, opGraveStr);
            handleMonitorArea(opBanished, opBanishedStr);
            handleMonitorArea(opExtra, opExtraStr);
            handleMonitorArea(myGrave, myGraveStr);
            handleMonitorArea(myBanished, myBanishedStr);
            handleMonitorArea(myExtra, myExtraStr);
            while (currentLabelIndex < 6)
            {
                deckPanel.labs[currentLabelIndex].gameObject.SetActive(false);
                currentLabelIndex++;
            }
            while (currentCardIndex < 300)
            {
                quickCards[currentCardIndex].clear();
                currentCardIndex++;
            }
        }
    }

    float currentHeight = 0;
    int currentLabelIndex = 0;
    int currentCardIndex = 0;
    int eachLine = 0;
    float monitorHeight = 0;

    void handleMonitorArea(List<gameCard> list,string hint)
    {
        if (list.Count > 0)
        {
            deckPanel.labs[currentLabelIndex].gameObject.SetActive(true);
            deckPanel.labs[currentLabelIndex].text = hint;
            deckPanel.labs[currentLabelIndex].width = (int)(monitor.width - 12);
            deckPanel.labs[currentLabelIndex].transform.localPosition = new Vector3(monitor.width / 2, (monitor.height - 8) / 2 - 12 - currentHeight, 0);
            currentLabelIndex++;
            currentHeight += 24;
            float beginX = 6 + 22;
            float beginY = monitor.height / 2 - currentHeight - 36;
            eachLine = (int)((monitor.width - 12f) / 44f);
            for (int i = 0; i < list.Count; i++)
            {
                var gp = UIHelper.get_hang_lie(i, eachLine);
                quickCards[currentCardIndex].reCode(list[i].get_data().Id);
                quickCards[currentCardIndex].transform.localPosition = new Vector3(beginX + 44 * gp.y, beginY - 60 * gp.x, 0);
                currentCardIndex++;
            }
            int hangshu = list.Count / eachLine;
            int yushu = list.Count % eachLine;
            if (yushu > 0)
            {
                hangshu++;
            }
            currentHeight += 60 * hangshu;
        }
    }
    
    public void onResized()
    {
        if (monitor.gameObject.activeInHierarchy)
        {
            int newEach = (int)((monitor.width - 12f) / 44f);
            if (newEach != eachLine || monitorHeight != monitor.height)
            {
                monitorHeight = monitor.height;
                eachLine = newEach;
                realizeMonitor();
            }
        }
    }

    public void setData(YGOSharp.Card card, Texture2D def, string tail = "",bool force=false)
    {
        if (cardShowerWidget.alpha == 0&&force==false)
        {
            return;
        }
        if (card == null)
        {
            return;
        }
        // 验收探针：这次调用真把哪张卡摆到了左侧说明面板上（`[ms] show code=<id>`）。
        // 「鼠标移到极大怪兽的 L/R 上要能直接看到**它自己**的效果」这条需求，判据就得咬
        // 这一行的 id —— 光验「面板没崩」验不出显示的是本体还是部件。
        QuickTestTrace.Log("ms", "show code=" + card.Id);
        if (card.Id == 0)
        {
            apply(card,def,tail);
            return;
        }
        if (datas.Count > 0)
        {
            if (datas[datas.Count - 1].card.Id == card.Id)
            {
                datas[datas.Count - 1] = new data
                {
                    card = card,
                    def = def,
                    tail = tail
                };
                if (datas.Count > 300)
                {
                    datas.RemoveAt(0);
                }
                current = datas.Count - 1;
                loadData();
                return;
            }
        }
        datas.Add(new data
        {
            card = card,
            def = def,
            tail = tail
        });
        if (datas.Count>300)    
        {
            datas.RemoveAt(0);
        }
        current = datas.Count - 1;
        loadData();
    }

    public void setTitle(string title)
    {
        UIHelper.trySetLableText(gameObject,"title_",title);
    }

    List<string> Logs = new List<string>();

    public void mLog(string result)
    {
        // 提示条（Servant.RMSshow_none → 这里）此前**没有任何探针**：验收脚本只能验
        // 「状态被收干净了」，验不了「玩家真的看到了那句话」。而「断开连接不把我踢出来」
        // 这类投诉的核心恰恰是玩家看不见任何解释 —— 补一行，让文案本身可判。
        // 标签仍用 ms（同一族），内容加 none 前缀以便和 [ms] show（确认框）区分开。
        QuickTestTrace.Log("ms", "none " + (result ?? "").Replace("\n", " / "));
        Logs.Add(result);
        renderLogs();
        Program.go(8000, clearOneLog);
    }

    /// <summary>
    /// 把提示条**最后一行换掉**（译名表下载进度这类"同一句话反复刷"的场景用）。
    /// 与 <see cref="mLog"/> 的差别只有：不加新行 —— 进度刷一百次也只占一行，
    /// 不然等一个 2 MB 的下载能把整个提示区刷满。
    /// 一条都没有时退化成 <see cref="mLog"/>。
    /// </summary>
    public void mLogReplaceLast(string result)
    {
        if (Logs.Count == 0)
        {
            mLog(result);
            return;
        }
        Logs[Logs.Count - 1] = result ?? "";
        // 同样留一条可判的痕：验收要能证明「玩家真的看到了进度在动」，
        // 而不只是「最后那句结果对」。
        QuickTestTrace.Log("ms", "last " + (result ?? "").Replace("\n", " / "));
        renderLogs();
    }

    /// <summary>把 <see cref="Logs"/> 拼成提示区那一段文本（mLog / mLogReplaceLast 共用）。</summary>
    void renderLogs()
    {
        string all = "";
        for (int i = 0; i < Logs.Count; i++)
        {
            if (i == Logs.Count - 1)
            {
                all += Logs[i].Replace("\0", "");
            }
            else
            {
                all += Logs[i].Replace("\0", "") + "\n";
            }
        }
        UIHelper.trySetLableTextList(UIHelper.getByName(gameObject, "chat_"), all);
    }

    void clearOneLog()
    {
        if (Logs.Count>0)
        {
            Logs.RemoveAt(0);
            string all = "";
            foreach (var item in Logs)
            {
                all += item + "\n";
            }
            try
            {
                all = all.Substring(0, all.Length - 1);
            }
            catch (System.Exception e)
            {
            }
            UIHelper.trySetLableTextList(UIHelper.getByName(gameObject, "chat_"), all);
        }
        else
        {
            UIHelper.trySetLableTextList(UIHelper.getByName(gameObject, "chat_"), "");
        }

    }
    public void clearAllLog()
    {
        Program.notGo(clearOneLog);
        Logs.Clear();
        UIHelper.trySetLableTextList(UIHelper.getByName(gameObject, "chat_"), "");
    }
}
