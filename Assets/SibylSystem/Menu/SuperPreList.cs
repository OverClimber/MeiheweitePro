using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

public class SuperPreList : WindowServantSP
{
    private const string ListUrl =
        "https://cdntx2.moecube.com/ygopro-super-pre/data/test-release.json";
    private const int MinPageWidth = 820;
    private const int MaxPageWidth = 1420;
    private const int PageHeight = 680;
    private const int PageHorizontalMargin = 20;
    private const int ListHorizontalInset = 28;
    private const int ListHeight = 570;
    private const int RowHeight = 285;
    private const int CardImageHeight = 150;
    private const float CardImageAspect = 447f / 652f;
    private const int RowHorizontalPadding = 6;
    private const int ColumnGap = 10;
    private const int CellInnerPadding = 6;
    private const int TextColumnGap = 10;
    private const int ScrollBarWidth = 14;
    private const float HeaderRightInset = 20f;
    private const float HeaderButtonSpacing = 160f;
    private const float HeaderButtonLeftExtent = 145f;
    private const float HeaderElementGap = 18f;
    private const float HeaderY = 310f;
    private const float ListCenterY = -15f;

    [Serializable]
    private class SuperPreCard
    {
        public string name = string.Empty;
        public string desc = string.Empty;
        public string overallString = string.Empty;
        public string picUrl = string.Empty;
    }

    [Serializable]
    private class SuperPrePayload
    {
        public SuperPreCard[] cards = Array.Empty<SuperPreCard>();
    }

    private int _layer;
    private int _pageWidth;
    private int _listWidth;
    private int _cardImageWidth;
    private int _lastScreenWidth = -1;
    private int _lastScreenHeight = -1;
    private float _lastWindowScale = -1f;
    private UIAtlas _atlas;
    private string _spriteName;
    private UILabel _fontTemplate;
    private UILabel _statusLabel;
    private UILabel _titleLabel;
    private Transform _back;
    private UISprite _backSprite;
    private GameObject _exitItem;
    private GameObject _downloadItem;
    private UILabel _downloadBadge;
    private GameObject _listObject;
    private GameObject _scrollBarObject;
    private UIPanel _listPanel;
    private UISprite _barBackground;
    private UISprite _barForeground;
    private UIScrollView _touchScroll;
    private VirtualScrollView _scrollView;
    private SuperPreScrollWatcher _scrollWatcher;
    private GameObject _previewObject;
    private UISprite _previewBackground;
    private UISprite _previewFrame;
    private UITexture _previewTexture;
    private UILabel _previewHint;
    private SuperPreCard[] _cards;
    private bool _isLoading;
    private List<SuperPreCardImageLoader> _imageLoaders;

    public override void initialize()
    {
        _imageLoaders = new List<SuperPreCardImageLoader>();
        createWindow(Program.I().new_ui_menu);
        gameObject.name = "super_pre_list";
        _layer = gameObject.layer;

        Transform back = gameObject.transform.Find("back");
        Transform deck = gameObject.transform.Find("deck");
        Transform resources = gameObject.transform.Find("supreCards");
        if (back == null || deck == null || resources == null)
        {
            UnityEngine.Debug.LogError("[SuperPre] 无法从主菜单模板创建列表页面。");
            return;
        }

        _back = back;
        _backSprite = back.GetComponent<UISprite>();
        _atlas = _backSprite != null ? _backSprite.atlas : null;
        _spriteName = _backSprite != null ? _backSprite.spriteName : string.Empty;
        _fontTemplate = deck.GetComponentInChildren<UILabel>();
        RefreshResponsiveMetrics();

        var originalChildren = new List<GameObject>();
        for (int i = 0; i < gameObject.transform.childCount; i++)
        {
            originalChildren.Add(gameObject.transform.GetChild(i).gameObject);
        }

        _exitItem = CloneMenuItem(
            deck.gameObject,
            "superPreExit",
            "deck_",
            "exit_",
            "返回",
            new Vector3(GetHeaderExitX(), HeaderY, 0f)
        );
        UISprite exitIcon = UIHelper.getByName<UISprite>(_exitItem, "Texture");
        if (exitIcon != null)
        {
            exitIcon.spriteName = "exit";
        }
        _downloadItem = CloneMenuItem(
            resources.gameObject,
            "superPreDownload",
            "supreCards_",
            "download_",
            "下载",
            new Vector3(GetHeaderDownloadX(), HeaderY, 0f)
        );
        // 下载图标沿用主菜单「资源下载」的 file，而返回图标是原生的 exit：
        // 前者在 transAtlas 里是 #FFFFFF、后者是 #D6D6D6，同一排里会一明一暗，这里统一到原生档。
        UISprite downloadIcon = UIHelper.getByName<UISprite>(_downloadItem, "Texture");
        if (downloadIcon != null)
        {
            downloadIcon.color = UIHelper.NativeIconColor;
        }
        CreateDownloadBadge();

        for (int i = 0; i < originalChildren.Count; i++)
        {
            if (originalChildren[i] != back.gameObject)
            {
                originalChildren[i].SetActive(false);
            }
        }

        ConfigurePageBackground(back, _backSprite);
        // 标题/状态行原为移动版尺度（32 / 24），按 YGOPro2 的比例（整体小 1.5 倍）收到 21 / 16，
        // 与 V2 自家对话框标题（18）和本页卡片正文（15–19）协调。
        _titleLabel = CreateLabel(
            gameObject.transform,
            "title_",
            "超先行卡",
            new Vector3(GetHeaderTitleX(), HeaderY, 0f),
            GetHeaderTitleWidth(),
            30,
            21,
            UIWidget.Pivot.Center,
            NGUIText.Alignment.Center,
            20,
            new Color(0.82f, 0.94f, 1f, 1f)
        );
        _statusLabel = CreateLabel(
            gameObject.transform,
            "status_",
            "正在加载...",
            new Vector3(0f, ListCenterY, 0f),
            _listWidth - 80,
            40,
            16,
            UIWidget.Pivot.Center,
            NGUIText.Alignment.Center,
            20,
            Color.white
        );

        CreateListView();
        CreatePreview();
        _listObject.SetActive(false);
        _scrollBarObject.SetActive(false);
        _previewObject.SetActive(false);

        UIHelper.registEvent(gameObject, "exit_", OnExit);
        UIHelper.registEvent(gameObject, "download_", OnDownload);

        // ⚠ 必须像其它 servant 一样先把自己关掉。
        // createWindow 出来的窗口是「活的」，位置取自 config.conf 的 x_/y_trans_menu
        // （主菜单退出时写的是屏幕正中），所以不关掉就会在启动的一瞬间整块 820x680 面板
        // 连标题和「下载/返回」一起压在加载画面上——就是「打开游戏先弹出超先行卡界面」。
        SetActiveFalse();
    }

    public override void show()
    {
        base.show();
        RefreshDownloadBadge();
        if (_cards != null)
        {
            _statusLabel.gameObject.SetActive(false);
            _listObject.SetActive(true);
            _scrollBarObject.SetActive(true);
        }
        else if (!_isLoading)
        {
            Program.I().StartCoroutine(LoadCardsCoroutine());
        }

        // 这个面板的「下载 / 返回」在克隆时被改名成 download_ / exit_，
        // 不在 Menu.MenuItemOrder 里，[btn] 探针覆盖不到 —— 验收脚本没法用真实光标点它。
        // 面板是 tween 移入的，头几批坐标还没落定，所以和 Menu 一样连报 4 批，脚本取最后一批。
        Program.go(300, TraceButtons);
        Program.go(420, TraceButtons);
        Program.go(540, TraceButtons);
        Program.go(660, TraceButtons);
    }

    /// <summary>
    /// 把面板按钮的屏幕坐标落一条轨迹（Qt 排查用，只有 log/qt_debug.on 存在时才写）。
    /// 报的是**按钮节点**（download_ / exit_）而不是外层容器：容器偏左，
    /// 照容器坐标按下去会落在按钮外，那一下等于没点。
    /// </summary>
    private void TraceButtons()
    {
        TraceButton(_downloadItem, "download_", "superPreDownload");
        TraceButton(_exitItem, "exit_", "superPreExit");
    }

    private static void TraceButton(GameObject item, string nodeName, string tag)
    {
        if (item == null || !QuickTestTrace.Enabled)
        {
            return;
        }
        Transform node = UIHelper.getByName<Transform>(item, nodeName);
        if (node == null)
        {
            return;
        }
        Vector3 sp = Program.camera_main_2d.WorldToScreenPoint(node.position);
        QuickTestTrace.Log("btn", tag + " screen=("
            + Mathf.RoundToInt(sp.x) + "," + Mathf.RoundToInt(Screen.height - sp.y) + ")");
    }

    public override void preFrameFunction()
    {
        base.preFrameFunction();
        RefreshDownloadBadge();
    }

    public override void hide()
    {
        HidePreview();
        ReleaseImages();
        base.hide();
    }

    public override void fixScreenProblem()
    {
        base.fixScreenProblem();
        if (gameObject != null && _back != null && RefreshResponsiveMetrics())
        {
            ApplyResponsiveLayout();
        }
    }

    public override void ES_quit()
    {
        if (_previewObject != null && _previewObject.activeSelf)
        {
            HidePreview();
        }
        else
        {
            OnExit();
        }
    }

    private void OnExit()
    {
        HidePreview();
        Program.I().shiftToServant(Program.I().menu);
    }

    private void OnDownload()
    {
        Program.I().menu.StartSuperPreUpdateFromList();
    }

    private void ConfigurePageBackground(Transform back, UISprite backSprite)
    {
        back.localPosition = Vector3.zero;
        if (backSprite != null)
        {
            backSprite.SetDimensions(_pageWidth, PageHeight);
        }

        BoxCollider collider = back.GetComponent<BoxCollider>();
        if (collider != null)
        {
            collider.enabled = false;
        }
        UIDragObject drag = back.GetComponent<UIDragObject>();
        if (drag != null)
        {
            drag.enabled = false;
        }

        UIPanel panel = gameObject.GetComponent<UIPanel>();
        if (panel != null)
        {
            panel.baseClipRegion = new Vector4(0f, 0f, _pageWidth + 20f, PageHeight + 20f);
        }
    }

    private bool RefreshResponsiveMetrics()
    {
        float windowScale = gameObject != null ? Mathf.Abs(gameObject.transform.localScale.x) : 1f;
        if (windowScale < 0.01f)
        {
            windowScale = 1f;
        }

        float safeWidth = Screen.safeArea.width > 0f ? Screen.safeArea.width : Screen.width;
        int pageWidth = Mathf.Clamp(
            Mathf.RoundToInt(safeWidth / windowScale - PageHorizontalMargin),
            MinPageWidth,
            MaxPageWidth
        );
        int listWidth = pageWidth - ListHorizontalInset;
        int cardImageWidth = Mathf.RoundToInt(CardImageHeight * CardImageAspect);
        bool changed =
            pageWidth != _pageWidth
            || listWidth != _listWidth
            || cardImageWidth != _cardImageWidth
            || Screen.width != _lastScreenWidth
            || Screen.height != _lastScreenHeight
            || !Mathf.Approximately(windowScale, _lastWindowScale);

        _pageWidth = pageWidth;
        _listWidth = listWidth;
        _cardImageWidth = cardImageWidth;
        _lastScreenWidth = Screen.width;
        _lastScreenHeight = Screen.height;
        _lastWindowScale = windowScale;

        UnityEngine.Debug.Assert(
            GetCardCellWidth()
                > _cardImageWidth + CellInnerPadding * 2 + TextColumnGap + 180,
            "[SuperPre] 响应式布局没有为效果文本留下足够宽度。"
        );
        UnityEngine.Debug.Assert(
            ListHeight == RowHeight * 2,
            "[SuperPre] 列表必须恰好容纳两行四张卡片。"
        );
        return changed;
    }

    private float GetHeaderExitX()
    {
        return _pageWidth * 0.5f - HeaderRightInset;
    }

    private float GetHeaderDownloadX()
    {
        return GetHeaderExitX() - HeaderButtonSpacing;
    }

    private float GetHeaderTitleX()
    {
        return 0f;
    }

    private int GetHeaderTitleWidth()
    {
        float availableHalf =
            GetHeaderDownloadX() - HeaderButtonLeftExtent - HeaderElementGap;
        return Mathf.Clamp(
            Mathf.FloorToInt(availableHalf * 2f),
            140,
            360
        );
    }

    private float GetScrollBarX()
    {
        return _listWidth * 0.5f;
    }

    private int GetCardCellWidth()
    {
        return Mathf.FloorToInt(
            (_listWidth - RowHorizontalPadding * 2 - ColumnGap) * 0.5f
        );
    }

    private float GetCardCellLeft(int column)
    {
        return -_listWidth * 0.5f
            + RowHorizontalPadding
            + column * (GetCardCellWidth() + ColumnGap);
    }

    private void ApplyResponsiveLayout()
    {
        ConfigurePageBackground(_back, _backSprite);

        if (_exitItem != null)
        {
            _exitItem.transform.localPosition = new Vector3(GetHeaderExitX(), HeaderY, 0f);
        }
        if (_downloadItem != null)
        {
            _downloadItem.transform.localPosition = new Vector3(
                GetHeaderDownloadX(),
                HeaderY,
                0f
            );
        }
        if (_titleLabel != null)
        {
            _titleLabel.transform.localPosition = new Vector3(
                GetHeaderTitleX(),
                HeaderY,
                0f
            );
            _titleLabel.width = GetHeaderTitleWidth();
        }
        if (_statusLabel != null)
        {
            _statusLabel.width = _listWidth - 80;
        }
        if (_listPanel != null)
        {
            _listPanel.baseClipRegion = new Vector4(0f, 0f, _listWidth, ListHeight);
        }
        if (_listObject != null)
        {
            _listObject.transform.localPosition = new Vector3(0f, ListCenterY, 0f);
        }
        if (_scrollBarObject != null)
        {
            _scrollBarObject.transform.localPosition = new Vector3(
                GetScrollBarX(),
                ListCenterY,
                0f
            );
        }
        if (_barBackground != null)
        {
            _barBackground.SetDimensions(ScrollBarWidth, ListHeight);
        }
        if (_barForeground != null)
        {
            _barForeground.SetDimensions(ScrollBarWidth, ListHeight);
        }
        if (_previewBackground != null)
        {
            _previewBackground.SetDimensions(_pageWidth, PageHeight);
            BoxCollider collider = _previewBackground.GetComponent<BoxCollider>();
            if (collider != null)
            {
                collider.size = new Vector3(_pageWidth, PageHeight, 1f);
            }
        }
        if (_previewHint != null)
        {
            _previewHint.transform.localPosition = new Vector3(
                0f,
                -PageHeight * 0.5f + 25f,
                0f
            );
        }

        if (_cards != null && _scrollView != null)
        {
            HidePreview();
            ReleaseImages();
            ShowCards();
        }
    }

    private GameObject CloneMenuItem(
        GameObject source,
        string containerName,
        string sourceButtonName,
        string buttonName,
        string text,
        Vector3 position
    )
    {
        GameObject clone = UnityEngine.Object.Instantiate(source, gameObject.transform, false);
        clone.name = containerName;
        clone.transform.localPosition = position;

        Transform button = UIHelper.getByName<Transform>(clone, sourceButtonName);
        if (button != null)
        {
            button.name = buttonName;
            UILabel label = button.GetComponentInChildren<UILabel>();
            if (label != null)
            {
                label.text = InterString.Get(text);
                label.width = Mathf.Max(label.width, 140);
                label.overflowMethod = UILabel.Overflow.ShrinkContent;
            }
        }
        return clone;
    }

    private void CreateDownloadBadge()
    {
        Transform button = UIHelper.getByName<Transform>(_downloadItem, "download_");
        if (button == null)
        {
            return;
        }

        _downloadBadge = CreateLabel(
            button,
            "downloadNew_",
            "NEW",
            new Vector3(10f, 18f, 0f),
            50,
            20,
            16,
            UIWidget.Pivot.Center,
            NGUIText.Alignment.Center,
            30,
            new Color(1f, 0.2f, 0.2f, 1f),
            1
        );
        _downloadBadge.effectStyle = UILabel.Effect.Outline;
        _downloadBadge.effectColor = new Color(0.5f, 0f, 0f, 1f);
        _downloadBadge.effectDistance = new Vector2(1f, 1f);
        RefreshDownloadBadge();
    }

    private void RefreshDownloadBadge()
    {
        if (_downloadBadge == null || Program.I() == null || Program.I().menu == null)
        {
            return;
        }

        bool visible = Program.I().menu.HasSuperPreUpdate;
        if (_downloadBadge.gameObject.activeSelf != visible)
        {
            _downloadBadge.gameObject.SetActive(visible);
        }
    }

    private void CreateListView()
    {
        _listObject = CreateObject(
            "superPrePanel_",
            gameObject.transform,
            new Vector3(0f, ListCenterY, 0f)
        );
        UIPanel panel = _listObject.AddComponent<UIPanel>();
        _listPanel = panel;
        panel.clipping = UIDrawCall.Clipping.SoftClip;
        panel.baseClipRegion = new Vector4(0f, 0f, _listWidth, ListHeight);
        panel.depth = 2;
        panel.cullWhileDragging = true;
        panel.showInPanelTool = false;

        _scrollBarObject = CreateObject(
            "superPreBar_",
            gameObject.transform,
            new Vector3(GetScrollBarX(), ListCenterY, 0f)
        );
        GameObject backgroundObject = CreateObject(
            "Background",
            _scrollBarObject.transform,
            Vector3.zero
        );
        _barBackground = backgroundObject.AddComponent<UISprite>();
        ConfigureSprite(
            _barBackground,
            UIWidget.Pivot.Center,
            ScrollBarWidth,
            ListHeight,
            new Color(0.05f, 0.12f, 0.16f, 0.95f),
            12
        );
        _barBackground.spriteName = "white";

        GameObject foregroundObject = CreateObject(
            "Foreground",
            _scrollBarObject.transform,
            Vector3.zero
        );
        _barForeground = foregroundObject.AddComponent<UISprite>();
        ConfigureSprite(
            _barForeground,
            UIWidget.Pivot.Center,
            ScrollBarWidth,
            ListHeight,
            new Color(0.2f, 0.85f, 1f, 1f),
            13
        );
        _barForeground.spriteName = "white";

        NGUITools.AddWidgetCollider(backgroundObject);
        NGUITools.AddWidgetCollider(foregroundObject);

        UIScrollBar scrollBar = _scrollBarObject.AddComponent<UIScrollBar>();
        scrollBar.backgroundWidget = _barBackground;
        scrollBar.foregroundWidget = _barForeground;
        scrollBar.fillDirection = UIProgressBar.FillDirection.TopToBottom;

        _scrollView = new VirtualScrollView(panel, scrollBar, CreateCardRow, RowHeight, BindCardRow);
        _scrollView.itemOnListShower = ShowItemImage;
        _scrollView.itemOnListHider = HideItemImage;

        _touchScroll = _listObject.GetComponent<UIScrollView>();
        if (_touchScroll != null)
        {
            _touchScroll.can_be_draged = true;
            _touchScroll.restrictWithinPanel = false;
            _scrollWatcher = _listObject.AddComponent<SuperPreScrollWatcher>();
            _scrollWatcher.Configure(panel, _touchScroll);
        }
    }

    private void CreatePreview()
    {
        _previewObject = CreateObject(
            "superPrePreview_",
            gameObject.transform,
            Vector3.zero
        );
        UIPanel previewPanel = _previewObject.AddComponent<UIPanel>();
        previewPanel.depth = 20;
        previewPanel.clipping = UIDrawCall.Clipping.None;
        previewPanel.showInPanelTool = false;
        _previewBackground = CreateSprite(
            _previewObject.transform,
            "previewBackground_",
            Vector3.zero,
            _pageWidth,
            PageHeight,
            new Color(0f, 0f, 0f, 0.92f),
            40,
            UIWidget.Pivot.Center
        );
        BoxCollider collider = _previewBackground.gameObject.AddComponent<BoxCollider>();
        collider.isTrigger = true;
        collider.size = new Vector3(_pageWidth, PageHeight, 1f);

        UIEventTrigger trigger = _previewBackground.gameObject.AddComponent<UIEventTrigger>();
        MonoDelegate close = _previewBackground.gameObject.AddComponent<MonoDelegate>();
        close.actionInMono = HidePreview;
        trigger.onClick.Add(new EventDelegate(close, "function"));

        _previewFrame = CreateSprite(
            _previewObject.transform,
            "previewFrame_",
            new Vector3(0f, 10f, 0f),
            400,
            590,
            new Color(0.2f, 0.75f, 1f, 0.9f),
            41,
            UIWidget.Pivot.Center
        );
        _previewTexture = CreateObject(
            "previewPic_",
            _previewObject.transform,
            new Vector3(0f, 10f, 0f)
        ).AddComponent<UITexture>();
        _previewTexture.pivot = UIWidget.Pivot.Center;
        _previewTexture.keepAspectRatio = UIWidget.AspectRatioSource.Free;
        _previewTexture.fixedAspect = false;
        _previewTexture.drawRegion = new Vector4(0f, 0f, 1f, 1f);
        _previewTexture.depth = 42;
        _previewHint = CreateLabel(
            _previewObject.transform,
            "previewHint_",
            "点击任意处关闭",
            new Vector3(0f, -PageHeight * 0.5f + 25f, 0f),
            300,
            30,
            18,
            UIWidget.Pivot.Center,
            NGUIText.Alignment.Center,
            43,
            new Color(0.8f, 0.92f, 1f, 1f),
            1
        );
    }

    private void ShowPreview(SuperPreCardImageLoader loader)
    {
        if (loader == null || loader.Texture == null || _previewObject == null)
        {
            return;
        }

        Texture2D texture = loader.Texture;
        float aspect = texture.height > 0
            ? (float)texture.width / texture.height
            : CardImageAspect;
        int height = PageHeight - 100;
        int width = Mathf.RoundToInt(height * aspect);
        int maxWidth = _pageWidth - 140;
        if (width > maxWidth)
        {
            width = maxWidth;
            height = Mathf.RoundToInt(width / aspect);
        }

        _previewTexture.mainTexture = texture;
        _previewTexture.SetDimensions(width, height);
        _previewFrame.SetDimensions(width + 16, height + 16);
        _previewObject.SetActive(true);
        if (_touchScroll != null)
        {
            _touchScroll.currentMomentum = Vector3.zero;
            _touchScroll.enabled = false;
        }
    }

    private void HidePreview()
    {
        if (_previewTexture != null)
        {
            _previewTexture.mainTexture = null;
        }
        if (_previewObject != null)
        {
            _previewObject.SetActive(false);
        }
        if (_touchScroll != null)
        {
            _touchScroll.enabled = true;
        }
    }

    private IEnumerator LoadCardsCoroutine()
    {
        _isLoading = true;
        _statusLabel.text = "正在加载...";
        _statusLabel.gameObject.SetActive(true);
        _listObject.SetActive(false);
        _scrollBarObject.SetActive(false);

        string json = null;
        string error = null;
        for (int attempt = 0; attempt < 2 && string.IsNullOrEmpty(json); attempt++)
        {
            using (UnityWebRequest request = UnityWebRequest.Get(ListUrl))
            {
                request.timeout = 20;
                yield return request.SendWebRequest();
                if (request.result == UnityWebRequest.Result.Success)
                {
                    json = request.downloadHandler.text;
                }
                else
                {
                    error = request.error;
                }
            }

            if (string.IsNullOrEmpty(json) && attempt == 0)
            {
                yield return new WaitForSecondsRealtime(1f);
            }
        }

        try
        {
            if (string.IsNullOrEmpty(json))
            {
                throw new Exception(string.IsNullOrEmpty(error) ? "响应为空" : error);
            }

            SuperPrePayload payload = JsonUtility.FromJson<SuperPrePayload>(
                "{\"cards\":" + json + "}"
            );
            if (payload == null || payload.cards == null || payload.cards.Length == 0)
            {
                throw new Exception("列表数据为空");
            }

            _cards = payload.cards;
            ShowCards();
        }
        catch (Exception e)
        {
            _statusLabel.text = "加载失败，请稍后重新进入。";
            _statusLabel.gameObject.SetActive(true);
            UnityEngine.Debug.LogWarning("[SuperPre] 列表加载失败：" + e.Message);
        }
        finally
        {
            _isLoading = false;
        }
    }

    private void ShowCards()
    {
        var items = new List<string[]>((_cards.Length + 1) / 2);
        for (int i = 0; i < _cards.Length; i += 2)
        {
            var row = new string[8];
            for (int column = 0; column < 2; column++)
            {
                int cardIndex = i + column;
                if (cardIndex >= _cards.Length)
                {
                    continue;
                }

                SuperPreCard card = _cards[cardIndex];
                int offset = column * 4;
                row[offset] = card != null ? card.name ?? string.Empty : string.Empty;
                row[offset + 1] = card != null
                    ? card.overallString ?? string.Empty
                    : string.Empty;
                row[offset + 2] = card != null ? card.desc ?? string.Empty : string.Empty;
                row[offset + 3] = card != null ? card.picUrl ?? string.Empty : string.Empty;
            }
            items.Add(row);
        }

        bool visible = isShowed;
        _listObject.SetActive(false);
        _scrollBarObject.SetActive(false);
        _scrollView.print(items);
        if (_scrollWatcher != null)
        {
            _scrollWatcher.SetBounds(items.Count, RowHeight);
            _scrollWatcher.ScrollToTop();
        }
        else
        {
            _scrollView.toTop();
        }
        _statusLabel.gameObject.SetActive(false);
        _listObject.SetActive(visible);
        _scrollBarObject.SetActive(visible);
    }

    private GameObject CreateCardRow(string[] values)
    {
        GameObject row = CreateObject("superPreRow", null, Vector3.zero);
        CreateSprite(
            row.transform,
            "rowBackground_",
            Vector3.zero,
            _listWidth - 6,
            RowHeight - 6,
            new Color(0.015f, 0.035f, 0.055f, 0.45f),
            3,
            UIWidget.Pivot.Center
        );

        BoxCollider collider = row.AddComponent<BoxCollider>();
        collider.isTrigger = true;
        collider.center = Vector3.zero;
        collider.size = new Vector3(_listWidth - 6, RowHeight - 6, 1f);

        CreateCardCell(row, 0);
        CreateCardCell(row, 1);
        return row;
    }

    private void CreateCardCell(GameObject row, int column)
    {
        GameObject cell = CreateObject("cell_" + column, row.transform, Vector3.zero);
        int cellWidth = GetCardCellWidth();
        float cellLeft = GetCardCellLeft(column);
        float contentTop = RowHeight * 0.5f - 10f;
        float imageLeft = cellLeft + CellInnerPadding;
        float textLeft = imageLeft + _cardImageWidth + TextColumnGap;
        int textWidth = Mathf.FloorToInt(
            cellLeft + cellWidth - CellInnerPadding - textLeft
        );

        CreateSprite(
            cell.transform,
            "cardBackground_" + column,
            new Vector3(cellLeft + cellWidth * 0.5f, 0f, 0f),
            cellWidth,
            RowHeight - 10,
            new Color(0.025f, 0.05f, 0.075f, 0.86f),
            4,
            UIWidget.Pivot.Center
        );
        UISprite imageBackground = CreateSprite(
            cell.transform,
            "imageBackground_" + column,
            new Vector3(imageLeft, contentTop, 0f),
            _cardImageWidth,
            CardImageHeight,
            new Color(0.12f, 0.17f, 0.21f, 1f),
            5,
            UIWidget.Pivot.TopLeft
        );
        GameObject imageObject = CreateObject(
            "pic_" + column,
            cell.transform,
            imageBackground.transform.localPosition
        );
        UITexture image = imageObject.AddComponent<UITexture>();
        image.pivot = UIWidget.Pivot.TopLeft;
        image.keepAspectRatio = UIWidget.AspectRatioSource.Free;
        image.SetDimensions(_cardImageWidth, CardImageHeight);
        image.fixedAspect = false;
        image.drawRegion = new Vector4(0f, 0f, 1f, 1f);
        image.depth = 6;
        image.transform.localPosition = imageBackground.transform.localPosition;

        CreateLabel(
            cell.transform,
            "name_" + column,
            string.Empty,
            new Vector3(textLeft, contentTop, 0f),
            textWidth,
            24,
            19,
            UIWidget.Pivot.TopLeft,
            NGUIText.Alignment.Left,
            7,
            new Color(0.78f, 0.94f, 1f, 1f),
            1
        );
        CreateLabel(
            cell.transform,
            "overall_" + column,
            string.Empty,
            new Vector3(textLeft, contentTop - 25f, 0f),
            textWidth,
            21,
            16,
            UIWidget.Pivot.TopLeft,
            NGUIText.Alignment.Left,
            7,
            new Color(0.7f, 0.82f, 0.88f, 1f),
            1
        );
        CreateLabel(
            cell.transform,
            "desc_" + column,
            string.Empty,
            new Vector3(textLeft, contentTop - 52f, 0f),
            textWidth,
            210,
            15,
            UIWidget.Pivot.TopLeft,
            NGUIText.Alignment.Left,
            7,
            new Color(0.94f, 0.96f, 0.98f, 1f)
        );

        SuperPreCardImageLoader loader = imageObject.AddComponent<SuperPreCardImageLoader>();
        loader.Configure(image, null, _cardImageWidth, CardImageHeight);
        _imageLoaders.Add(loader);

        BoxCollider imageCollider = imageObject.AddComponent<BoxCollider>();
        imageCollider.isTrigger = true;
        imageCollider.center = new Vector3(
            _cardImageWidth * 0.5f,
            -CardImageHeight * 0.5f,
            0f
        );
        imageCollider.size = new Vector3(_cardImageWidth, CardImageHeight, 1f);
        if (_touchScroll != null)
        {
            UIDragScrollView drag = imageObject.AddComponent<UIDragScrollView>();
            drag.scrollView = _touchScroll;
        }

        UIEventTrigger trigger = imageObject.AddComponent<UIEventTrigger>();
        MonoDelegate preview = imageObject.AddComponent<MonoDelegate>();
        preview.actionInMono = () => ShowPreview(loader);
        trigger.onClick.Add(new EventDelegate(preview, "function"));
    }

    private void BindCardRow(GameObject row, string[] values)
    {
        row.transform.Find("rowBackground_").GetComponent<UISprite>().SetDimensions(
            _listWidth - 6, RowHeight - 6);
        row.GetComponent<BoxCollider>().size = new Vector3(_listWidth - 6, RowHeight - 6, 1f);

        for (int column = 0; column < 2; column++)
        {
            int offset = column * 4;
            bool hasCard = values != null && values.Length >= offset + 4
                && (!string.IsNullOrEmpty(values[offset]) || !string.IsNullOrEmpty(values[offset + 3]));
            Transform cell = row.transform.Find("cell_" + column);
            int cellWidth = GetCardCellWidth();
            float cellLeft = GetCardCellLeft(column);
            float contentTop = RowHeight * 0.5f - 10f;
            float imageLeft = cellLeft + CellInnerPadding;
            float textLeft = imageLeft + _cardImageWidth + TextColumnGap;
            int textWidth = Mathf.FloorToInt(cellLeft + cellWidth - CellInnerPadding - textLeft);

            UISprite background = cell.Find("cardBackground_" + column).GetComponent<UISprite>();
            background.SetDimensions(cellWidth, RowHeight - 10);
            background.transform.localPosition = new Vector3(cellLeft + cellWidth * 0.5f, 0f, 0f);
            UISprite imageBackground = cell.Find("imageBackground_" + column).GetComponent<UISprite>();
            imageBackground.SetDimensions(_cardImageWidth, CardImageHeight);
            imageBackground.transform.localPosition = new Vector3(imageLeft, contentTop, 0f);

            UITexture image = cell.Find("pic_" + column).GetComponent<UITexture>();
            // 下载器会居中缩放图片，重绑前恢复基准位置，避免累计偏移。
            image.keepAspectRatio = UIWidget.AspectRatioSource.Free;
            image.SetDimensions(_cardImageWidth, CardImageHeight);
            image.fixedAspect = false;
            image.drawRegion = new Vector4(0f, 0f, 1f, 1f);
            image.transform.localPosition = imageBackground.transform.localPosition;
            SuperPreCardImageLoader loader = image.GetComponent<SuperPreCardImageLoader>();
            loader.Configure(image, hasCard ? values[offset + 3] : null, _cardImageWidth, CardImageHeight);
            image.mainTexture = null;

            BoxCollider imageCollider = image.GetComponent<BoxCollider>();
            imageCollider.center = new Vector3(_cardImageWidth * 0.5f, -CardImageHeight * 0.5f, 0f);
            imageCollider.size = new Vector3(_cardImageWidth, CardImageHeight, 1f);
            BindCardLabel(cell.Find("name_" + column).GetComponent<UILabel>(),
                hasCard ? values[offset] : null, textWidth, new Vector3(textLeft, contentTop, 0f));
            BindCardLabel(cell.Find("overall_" + column).GetComponent<UILabel>(),
                hasCard ? values[offset + 1] : null, textWidth, new Vector3(textLeft, contentTop - 25f, 0f));
            BindCardLabel(cell.Find("desc_" + column).GetComponent<UILabel>(),
                hasCard ? (values[offset + 2] ?? string.Empty).Replace("\r", string.Empty) : null,
                textWidth, new Vector3(textLeft, contentTop - 52f, 0f));
            cell.gameObject.SetActive(hasCard);
        }
    }

    private static void BindCardLabel(UILabel label, string text, int width, Vector3 position)
    {
        label.text = text ?? string.Empty;
        label.width = width;
        label.transform.localPosition = position;
    }

    private void ShowItemImage(GameObject item)
    {
        SuperPreCardImageLoader[] loaders = item.GetComponentsInChildren<SuperPreCardImageLoader>(
            true
        );
        for (int i = 0; i < loaders.Length; i++)
        {
            loaders[i].Show();
        }
    }

    private void HideItemImage(GameObject item)
    {
        SuperPreCardImageLoader[] loaders = item.GetComponentsInChildren<SuperPreCardImageLoader>(
            true
        );
        for (int i = 0; i < loaders.Length; i++)
        {
            loaders[i].Release();
        }
    }

    private void ReleaseImages()
    {
        for (int i = 0; i < _imageLoaders.Count; i++)
        {
            if (_imageLoaders[i] != null)
            {
                _imageLoaders[i].Release();
            }
        }
    }

    private GameObject CreateObject(string name, Transform parent, Vector3 position)
    {
        GameObject obj = new GameObject(name);
        obj.layer = _layer;
        if (parent != null)
        {
            obj.transform.SetParent(parent, false);
        }
        obj.transform.localPosition = position;
        obj.transform.localRotation = Quaternion.identity;
        obj.transform.localScale = Vector3.one;
        return obj;
    }

    private UISprite CreateSprite(
        Transform parent,
        string name,
        Vector3 position,
        int width,
        int height,
        Color color,
        int depth,
        UIWidget.Pivot pivot
    )
    {
        UISprite sprite = CreateObject(name, parent, position).AddComponent<UISprite>();
        ConfigureSprite(sprite, pivot, width, height, color, depth);
        // NGUI changes the Transform while switching away from the default center pivot.
        sprite.transform.localPosition = position;
        return sprite;
    }

    private void ConfigureSprite(
        UISprite sprite,
        UIWidget.Pivot pivot,
        int width,
        int height,
        Color color,
        int depth
    )
    {
        sprite.atlas = _atlas;
        sprite.spriteName = _spriteName;
        sprite.type = UIBasicSprite.Type.Sliced;
        sprite.pivot = pivot;
        sprite.color = color;
        sprite.depth = depth;
        sprite.SetDimensions(width, height);
    }

    private UILabel CreateLabel(
        Transform parent,
        string name,
        string text,
        Vector3 position,
        int width,
        int height,
        int fontSize,
        UIWidget.Pivot pivot,
        NGUIText.Alignment alignment,
        int depth,
        Color color,
        int maxLineCount = 0
    )
    {
        UILabel label = CreateObject(name, parent, position).AddComponent<UILabel>();
        if (_fontTemplate != null)
        {
            label.bitmapFont = _fontTemplate.bitmapFont;
            label.trueTypeFont = _fontTemplate.trueTypeFont;
            label.fontStyle = _fontTemplate.fontStyle;
            label.applyGradient = _fontTemplate.applyGradient;
            label.gradientTop = _fontTemplate.gradientTop;
            label.gradientBottom = _fontTemplate.gradientBottom;
            label.effectStyle = _fontTemplate.effectStyle;
            label.effectColor = _fontTemplate.effectColor;
            label.effectDistance = _fontTemplate.effectDistance;
        }
        label.text = text;
        label.fontSize = fontSize;
        label.pivot = pivot;
        label.alignment = alignment;
        label.color = color;
        label.width = width;
        label.height = height;
        label.depth = depth;
        label.maxLineCount = maxLineCount;
        label.supportEncoding = false;
        label.overflowMethod = UILabel.Overflow.ShrinkContent;
        // Keep every label on the requested row baseline after the pivot change above.
        label.transform.localPosition = position;
        return label;
    }
}

public sealed class SuperPreScrollWatcher : MonoBehaviour
{
    private UIPanel _panel;
    private UIScrollView _scrollView;
    private Vector2 _lastOffset;
    private float _topOffset;
    private float _bottomOffset;
    private bool _hasBounds;

    public void Configure(UIPanel panel, UIScrollView scrollView)
    {
        _panel = panel;
        _scrollView = scrollView;
        _lastOffset = panel.clipOffset;
    }

    public void SetBounds(int itemCount, float itemHeight)
    {
        if (itemCount <= 0 || itemHeight <= 0f)
        {
            _hasBounds = false;
            return;
        }

        float viewHeight = _panel.GetViewSize().y;
        float moveOffset =
            _panel.baseClipRegion.y + (viewHeight - itemHeight) * 0.5f - 10f;
        float scrollRange = -(itemHeight * itemCount - viewHeight + 20f);
        if (scrollRange >= 0f)
        {
            scrollRange = -0.001f;
        }

        _topOffset = -moveOffset;
        _bottomOffset = scrollRange - moveOffset;
        _hasBounds = true;
        UnityEngine.Debug.Assert(
            _bottomOffset <= _topOffset,
            "[SuperPre] 滚动边界计算错误。"
        );
        ClampOffset();
    }

    public void ScrollToTop()
    {
        if (!_hasBounds)
        {
            return;
        }
        ApplyOffset(_topOffset);
        NotifyScrolled();
    }

    private void LateUpdate()
    {
        if (_panel == null || _scrollView == null)
        {
            return;
        }

        bool clamped = ClampOffset();
        if (!clamped && _panel.clipOffset == _lastOffset)
        {
            return;
        }

        _lastOffset = _panel.clipOffset;
        NotifyScrolled();
    }

    private bool ClampOffset()
    {
        if (!_hasBounds)
        {
            return false;
        }

        float clamped = Mathf.Clamp(_panel.clipOffset.y, _bottomOffset, _topOffset);
        if (!Mathf.Approximately(clamped, _panel.clipOffset.y))
        {
            ApplyOffset(clamped);
            return true;
        }
        return false;
    }

    private void ApplyOffset(float y)
    {
        _panel.clipOffset = new Vector2(_panel.clipOffset.x, y);
        Transform content = _scrollView.transform;
        content.localPosition = new Vector3(content.localPosition.x, -y, content.localPosition.z);
        _scrollView.currentMomentum = Vector3.zero;
        _lastOffset = _panel.clipOffset;
    }

    private void NotifyScrolled()
    {
        if (_scrollView.onScrolled != null)
        {
            _scrollView.onScrolled();
        }
    }
}

public sealed class SuperPreCardImageLoader : MonoBehaviour
{
    private UITexture _target;
    private string _url;
    private int _width;
    private int _height;
    private Vector3 _origin;
    private UnityWebRequest _request;
    private Texture2D _texture;

    public Texture2D Texture
    {
        get { return _texture; }
    }

    public void Configure(UITexture target, string url, int width, int height)
    {
        Release();
        _target = target;
        _url = url;
        // 上游列表中的图片仍可能使用旧域名，下载时统一切换并保留路径及版本参数。
        Uri imageUri;
        if (Uri.TryCreate(url, UriKind.Absolute, out imageUri)
            && imageUri.Host.Equals("cdntx.moecube.com", StringComparison.OrdinalIgnoreCase))
        {
            _url = new UriBuilder(imageUri) { Host = "cdntx2.moecube.com" }.Uri.AbsoluteUri;
        }
        _width = width;
        _height = height;
        _origin = target != null ? target.transform.localPosition : Vector3.zero;
    }

    public void Show()
    {
        if (
            !isActiveAndEnabled
            || _target == null
            || string.IsNullOrEmpty(_url)
            || _texture != null
            || _request != null
        )
        {
            return;
        }
        StartCoroutine(LoadCoroutine());
    }

    public void Release()
    {
        StopAllCoroutines();
        if (_request != null)
        {
            _request.Abort();
            _request.Dispose();
            _request = null;
        }
        if (_texture != null)
        {
            if (_target != null && _target.mainTexture == _texture)
            {
                _target.mainTexture = null;
            }
            UnityEngine.Object.Destroy(_texture);
            _texture = null;
        }
    }

    private IEnumerator LoadCoroutine()
    {
        UnityWebRequest request = UnityWebRequestTexture.GetTexture(_url, true);
        _request = request;
        request.timeout = 20;
        yield return request.SendWebRequest();

        if (
            _request == request
            && request.result == UnityWebRequest.Result.Success
            && _target != null
            && gameObject.activeInHierarchy
        )
        {
            _texture = DownloadHandlerTexture.GetContent(request);
            _target.mainTexture = _texture;
            float textureAspect = _texture.height > 0
                ? (float)_texture.width / _texture.height
                : (float)_width / _height;
            float boxAspect = (float)_width / _height;
            int fittedWidth = _width;
            int fittedHeight = _height;
            if (textureAspect > boxAspect)
            {
                fittedHeight = Mathf.Max(1, Mathf.RoundToInt(_width / textureAspect));
            }
            else
            {
                fittedWidth = Mathf.Max(1, Mathf.RoundToInt(_height * textureAspect));
            }

            _target.keepAspectRatio = UIWidget.AspectRatioSource.Free;
            _target.SetDimensions(fittedWidth, fittedHeight);
            _target.fixedAspect = false;
            _target.drawRegion = new Vector4(0f, 0f, 1f, 1f);
            _target.transform.localPosition = _origin + new Vector3(
                (_width - fittedWidth) * 0.5f,
                -(_height - fittedHeight) * 0.5f,
                0f
            );
            UnityEngine.Debug.Assert(
                _target.width <= _width && _target.height <= _height,
                "[SuperPre] 卡图完整适配失败。"
            );
        }

        if (_request == request)
        {
            _request = null;
        }
        request.Dispose();
    }

    private void OnEnable()
    {
        Show();
    }

    private void OnDisable()
    {
        Release();
    }

    private void OnDestroy()
    {
        Release();
    }
}
