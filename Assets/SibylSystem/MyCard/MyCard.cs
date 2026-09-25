using UnityEngine;
using UnityEngine.Networking;
using System;
using System.Collections;
using System.IO;
using System.Threading;

public class MyCard : WindowServantSP
{
    const string ProfileAccountConfigKey = "mycard_profile_account";
    const string ProfileJsonConfigKey = "mycard_profile_json";
    const string AvatarCacheAccountConfigKey = "mycard_avatar_cache_account";
    const string AvatarCacheUrlConfigKey = "mycard_avatar_cache_url";
    const string AvatarCacheFileName = "mycard_avatar.cache";
    const float MatchRestartDebounceSeconds = 1f;

    public bool isMatching = false;
    public bool isRequesting = false;

    private Coroutine requestCoroutine = null;
    private Thread joinThread = null;
    MyCardHelper mycardHelper;
    UIInput inputUsername;
    UIInput inputPsw;
    UIButton loginButton;
    UIButton joinAthleticButton;
    UIButton joinEntertainButton;
    UILabel loginButtonLabel;
    UILabel joinAthleticButtonLabel;
    UILabel joinEntertainButtonLabel;
    UILabel accountStatusLabel;
    UILabel matchStatusLabel;
    GameObject nameLine;
    GameObject pswLine;
    GameObject accountSummary;
    UITexture accountAvatar;
    UILabel accountUsernameLabel;
    UILabel accountProfileLabel;
    Coroutine accountProfileCoroutine;
    string accountProfileText = string.Empty;
    string accountProfileAccount = string.Empty;
    string accountProfileJson = string.Empty;
    string accountProfileCheckedUsername = string.Empty;
    string loadedAvatarAccount = string.Empty;
    string loadedAvatarUrl = string.Empty;
    string matchingArena = string.Empty;
    float matchRestartAllowedAt = 0f;
    bool isLoggingIn = false;
    bool isEditingAccount = false;
    bool isCancellingMatch = false;

    public override void initialize()
    {
        createWindow(Program.I().new_ui_mycard);
        UIHelper.registEvent(gameObject, "exit_", onClickExit);
        UIHelper.registEvent(gameObject, "joinAthletic_", onClickJoinAthletic);
        UIHelper.registEvent(gameObject, "joinEntertain_", onClickJoinEntertain);
        UIHelper.registEvent(gameObject, "database_", onClickDatabase);
        UIHelper.registEvent(gameObject, "community_", onClickCommunity);
        inputUsername = UIHelper.getByName<UIInput>(gameObject, "name_");
        inputPsw = UIHelper.getByName<UIInput>(gameObject, "psw_");
        nameLine = FindChildIncludingInactive(gameObject, "nameLine")?.gameObject;
        pswLine = FindChildIncludingInactive(gameObject, "pswLine")?.gameObject;
        joinAthleticButton = UIHelper.getByName<UIButton>(gameObject, "joinAthletic_");
        joinEntertainButton = UIHelper.getByName<UIButton>(gameObject, "joinEntertain_");
        joinAthleticButtonLabel = joinAthleticButton == null
            ? null
            : joinAthleticButton.GetComponentInChildren<UILabel>(true);
        joinEntertainButtonLabel = joinEntertainButton == null
            ? null
            : joinEntertainButton.GetComponentInChildren<UILabel>(true);
        if (joinAthleticButtonLabel != null)
        {
            joinAthleticButtonLabel.overflowMethod = UILabel.Overflow.ShrinkContent;
        }
        if (joinEntertainButtonLabel != null)
        {
            joinEntertainButtonLabel.overflowMethod = UILabel.Overflow.ShrinkContent;
        }
        mycardHelper = new MyCardHelper();
        LoadUser();
        if (mycardHelper.RestoreSession(inputUsername.value))
        {
            inputPsw.value = string.Empty;
        }
#if UNITY_EDITOR
        Debug.Assert(
            CanMatchWithSession(true, "user", "user", "")
            && !CanMatchWithSession(true, "user", "other", "")
            && !CanMatchWithSession(true, "user", "user", "password")
            && IsHttpsUrl("https://example.com/avatar.jpg")
            && !IsHttpsUrl("file:///tmp/avatar.jpg")
            && IsSameAvatar(
                "user",
                "https://example.com/avatar.jpg",
                "user",
                "https://example.com/avatar.jpg")
            && !IsSameAvatar(
                "user",
                "https://example.com/avatar.jpg",
                "user",
                "https://example.com/avatar-new.jpg")
            && IsCancelMatchAction(true, false, false, "athletic", "athletic")
            && !IsCancelMatchAction(true, false, false, "athletic", "entertain")
            && !IsCancelMatchAction(true, true, false, "athletic", "athletic")
            && !IsCancelMatchAction(true, false, true, "athletic", "athletic")
            && IsMatchRestartCoolingDown(10f, 11f)
            && !IsMatchRestartCoolingDown(11f, 11f)
            && !HasProfileChanged("user", "{}", "user", "{}")
            && HasProfileChanged("user", "{}", "user", "{\"exp\":1}"),
            "MyCard login/match UI state check failed."
        );
#endif
        InitializeSeparatedLoginUi();
        if (mycardHelper.HasSession)
        {
            TryLoadCachedAccountProfile(mycardHelper.username);
        }
        RefreshSessionUi();
        SetActiveFalse();
    }

    Transform FindChildIncludingInactive(GameObject root, string childName)
    {
        Transform[] children = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            if (children[i].name == childName)
            {
                return children[i];
            }
        }
        return null;
    }

    UILabel CreateSectionLabel(UILabel template, string labelName, Vector3 position)
    {
        if (template == null)
        {
            return null;
        }

        GameObject clone = UnityEngine.Object.Instantiate(template.gameObject);
        clone.name = labelName;
        clone.SetActive(false);
        clone.transform.SetParent(template.transform.parent, false);

        UILabel label = clone.GetComponent<UILabel>();
        label.SetAnchor((Transform)null);
        label.pivot = UIWidget.Pivot.Center;
        label.alignment = NGUIText.Alignment.Center;
        label.width = 540;
        label.height = 26;
        label.fontSize = 18;
        label.depth = 20;
        clone.transform.localPosition = position;
        clone.transform.localScale = Vector3.one;
        clone.SetActive(true);
        return label;
    }

    void InitializeSeparatedLoginUi()
    {
        // ponytail: reuse the dormant server selector instead of adding another prefab control.
        Transform loginTransform = FindChildIncludingInactive(gameObject, "server");
        if (loginTransform != null)
        {
            GameObject loginObject = loginTransform.gameObject;
            UIPopupList obsoletePopup = loginObject.GetComponent<UIPopupList>();
            if (obsoletePopup != null)
            {
                obsoletePopup.enabled = false;
                UnityEngine.Object.Destroy(obsoletePopup);
            }

            Transform symbol = FindChildIncludingInactive(loginObject, "Symbol");
            if (symbol != null)
            {
                symbol.gameObject.SetActive(false);
            }

            loginObject.name = "login_";
            loginObject.SetActive(true);
            loginButton = loginObject.GetComponent<UIButton>();
            loginButtonLabel = loginObject.GetComponentInChildren<UILabel>(true);
            if (loginButtonLabel != null)
            {
                loginButtonLabel.alignment = NGUIText.Alignment.Center;
                loginButtonLabel.fontSize = 22;
                loginButtonLabel.height = 42;
            }

            UISprite background = loginObject.GetComponent<UISprite>();
            if (background != null)
            {
                background.height = 42;
            }
            BoxCollider hitArea = loginObject.GetComponent<BoxCollider>();
            if (hitArea != null)
            {
                Vector3 size = hitArea.size;
                size.y = 42f;
                hitArea.size = size;
            }
            UIHelper.registEvent(loginButton, onClickLogin);
        }

        Transform mainWindow = FindChildIncludingInactive(gameObject, "mainWindow");
        UILabel titleLabel = null;
        UILabel[] labels = mainWindow == null
            ? Array.Empty<UILabel>()
            : mainWindow.GetComponentsInChildren<UILabel>(true);
        for (int i = 0; i < labels.Length; i++)
        {
            if (labels[i].transform.parent == mainWindow && labels[i].text == "MyCard")
            {
                titleLabel = labels[i];
                break;
            }
        }
#if UNITY_EDITOR
        Debug.Assert(titleLabel != null, "MyCard window title label was not found.");
#endif

        accountStatusLabel = CreateSectionLabel(
            titleLabel,
            "accountStatus_",
            new Vector3(0f, 160f, 0f)
        );
        matchStatusLabel = CreateSectionLabel(
            titleLabel,
            "matchStatus_",
            new Vector3(0f, -60f, 0f)
        );
        if (matchStatusLabel != null)
        {
            matchStatusLabel.fontSize = 16;
            matchStatusLabel.height = 20;
        }

        if (titleLabel != null)
        {
            accountSummary = NGUITools.AddChild(titleLabel.transform.parent.gameObject);
            accountSummary.name = "accountSummary_";

            GameObject avatarObject = NGUITools.AddChild(accountSummary);
            avatarObject.name = "accountAvatar_";
            avatarObject.transform.localPosition = new Vector3(-220f, 75f, 0f);
            accountAvatar = avatarObject.AddComponent<UITexture>();
            accountAvatar.width = 90;
            accountAvatar.height = 90;
            accountAvatar.depth = 20;

            accountUsernameLabel = CreateSectionLabel(
                titleLabel,
                "accountUsername_",
                Vector3.zero
            );
            if (accountUsernameLabel != null)
            {
                accountUsernameLabel.transform.SetParent(accountSummary.transform, false);
                accountUsernameLabel.pivot = UIWidget.Pivot.Left;
                accountUsernameLabel.alignment = NGUIText.Alignment.Left;
                accountUsernameLabel.width = 420;
                accountUsernameLabel.height = 34;
                accountUsernameLabel.fontSize = 24;
                accountUsernameLabel.supportEncoding = false;
                accountUsernameLabel.text = string.Empty;
                accountUsernameLabel.transform.localPosition = new Vector3(-155f, 128f, 0f);
            }

            accountProfileLabel = CreateSectionLabel(
                titleLabel,
                "accountProfile_",
                Vector3.zero
            );
            if (accountProfileLabel != null)
            {
                accountProfileLabel.transform.SetParent(accountSummary.transform, false);
                accountProfileLabel.pivot = UIWidget.Pivot.Left;
                accountProfileLabel.alignment = NGUIText.Alignment.Left;
                accountProfileLabel.width = 420;
                accountProfileLabel.height = 92;
                accountProfileLabel.fontSize = 18;
                accountProfileLabel.supportEncoding = false;
                accountProfileLabel.overflowMethod = UILabel.Overflow.ShrinkContent;
                accountProfileLabel.text = string.Empty;
                accountProfileLabel.transform.localPosition = new Vector3(-155f, 62f, 0f);
            }

            accountSummary.SetActive(false);
        }
    }

    static bool CanMatchWithSession(
        bool hasSession,
        string sessionUsername,
        string displayedUsername,
        string displayedPassword
    )
    {
        return hasSession
            && string.Equals(sessionUsername, displayedUsername, StringComparison.Ordinal)
            && string.IsNullOrEmpty(displayedPassword);
    }

    static bool IsCancelMatchAction(
        bool requesting,
        bool loggingIn,
        bool cancelling,
        string currentArena,
        string clickedArena)
    {
        return requesting
            && !loggingIn
            && !cancelling
            && string.Equals(currentArena, clickedArena, StringComparison.Ordinal);
    }

    static bool IsMatchRestartCoolingDown(float now, float restartAllowedAt)
    {
        return now < restartAllowedAt;
    }

    static bool IsCancelledRequest(MyCardRequestResult result)
    {
        return result != null && result.Failure == MyCardRequestFailure.Cancelled;
    }

    bool HasDisplayedSession()
    {
        string displayedUsername = (inputUsername.value ?? string.Empty).Trim();
        return CanMatchWithSession(
            mycardHelper.HasSession,
            mycardHelper.username,
            displayedUsername,
            inputPsw.value
        );
    }

    void RefreshSessionUi()
    {
        bool canMatch = !isEditingAccount && HasDisplayedSession();
        bool isJoining = joinThread != null && joinThread.IsAlive;
        bool isRestartCoolingDown = IsMatchRestartCoolingDown(
            Time.realtimeSinceStartup,
            matchRestartAllowedAt);
        if (nameLine != null)
        {
            nameLine.SetActive(!canMatch);
        }
        if (pswLine != null)
        {
            pswLine.SetActive(!canMatch);
        }
        if (accountSummary != null)
        {
            accountSummary.SetActive(canMatch);
        }
        if (canMatch
            && accountUsernameLabel != null
            && accountUsernameLabel.text != mycardHelper.username)
        {
            accountUsernameLabel.text = mycardHelper.username;
            if (accountAvatar != null
                && !IsSameAvatar(
                    loadedAvatarAccount,
                    loadedAvatarUrl,
                    mycardHelper.username,
                    mycardHelper.avatarUrl))
            {
                accountAvatar.mainTexture = UIHelper.getFace(mycardHelper.username);
            }
        }
        if (accountProfileLabel != null)
        {
            string profileText = canMatch
                ? accountProfileText
                : string.Empty;
            if (accountProfileLabel.text != profileText)
            {
                accountProfileLabel.text = profileText;
            }
        }

        if (loginButton != null)
        {
            loginButton.isEnabled = !isRequesting && !isJoining;
        }
        RefreshMatchButtons(canMatch, isJoining, isRestartCoolingDown);

        if (loginButtonLabel != null)
        {
            loginButtonLabel.text = canMatch
                ? InterString.Get("切换账号")
                : InterString.Get("登录 MyCard");
        }

        if (accountStatusLabel != null)
        {
            if (isLoggingIn)
            {
                accountStatusLabel.text = InterString.Get("账号登录")
                    + "  [7FE7FF]"
                    + InterString.Get("正在登录...")
                    + "[-]";
            }
            else if (canMatch)
            {
                accountStatusLabel.text = InterString.Get("账号登录")
                    + "  [62D995]"
                    + InterString.Get("已登录")
                    + "[-]";
            }
            else if (isEditingAccount && mycardHelper.HasSession)
            {
                accountStatusLabel.text = InterString.Get("账号登录")
                    + "  [FFD166]"
                    + InterString.Get("正在切换账号")
                    + "[-]";
            }
            else
            {
                accountStatusLabel.text = InterString.Get("账号登录")
                    + "  [A8A8A8]"
                    + InterString.Get("未登录")
                    + "[-]";
            }
        }

        if (matchStatusLabel != null)
        {
            if (isJoining)
            {
                matchStatusLabel.text = "[7FE7FF]"
                    + InterString.Get("正在连接房间...")
                    + "[-]";
            }
            else if (isCancellingMatch)
            {
                matchStatusLabel.text = "[FFD166]"
                    + InterString.Get("正在取消匹配...")
                    + "[-]";
            }
            else if (isRequesting && !isLoggingIn)
            {
                matchStatusLabel.text = "[7FE7FF]"
                    + InterString.Get(
                        string.Equals(matchingArena, "athletic", StringComparison.Ordinal)
                            ? "正在进行竞技匹配，可点击取消。"
                            : "正在进行娱乐匹配，可点击取消。")
                    + "[-]";
            }
            else if (isRestartCoolingDown)
            {
                matchStatusLabel.text = "[FFD166]"
                    + InterString.Get("匹配已取消，请稍候...")
                    + "[-]";
            }
            else
            {
                matchStatusLabel.text = canMatch
                    ? InterString.Get("选择匹配模式")
                    : string.Empty;
            }
        }
    }

    public override void preFrameFunction()
    {
        base.preFrameFunction();
        RefreshSessionUi();
    }

    public override void show()
    {
        bool refreshAccountProfile = !isShowed;
        if (mycardHelper != null && mycardHelper.HasSession)
        {
            inputUsername.value = mycardHelper.username;
            inputPsw.value = string.Empty;
            isEditingAccount = false;
            TryLoadCachedAccountProfile(mycardHelper.username);
        }
        else
        {
            isEditingAccount = true;
        }
        RefreshSessionUi();
        base.show();
        // 回到竞技场界面 = 本轮天梯会话收尾：清掉匹配态。
        // ⛔ 必须清 —— 本类只在匹配成功那一刻把 isMatching 置 true、从不复位，
        //    不清的话此后再从任何入口断线都会被 Ocgcore.setDefaultReturnServant 拨回竞技场界面。
        isMatching = false;
        if (refreshAccountProfile && mycardHelper != null && mycardHelper.HasSession)
        {
            StartAccountProfileRefresh();
        }
    }

    static bool IsHttpsUrl(string value)
    {
        Uri uri;
        return Uri.TryCreate(value, UriKind.Absolute, out uri)
            && string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase);
    }

    static bool IsSameAvatar(
        string cachedAccount,
        string cachedUrl,
        string accountName,
        string avatarUrl)
    {
        return string.Equals(cachedAccount, accountName, StringComparison.Ordinal)
            && string.Equals(cachedUrl, avatarUrl, StringComparison.Ordinal);
    }

    bool TryLoadCachedAvatar(string accountName, string avatarUrl)
    {
        if (accountAvatar == null || !IsHttpsUrl(avatarUrl))
        {
            return false;
        }
        if (IsSameAvatar(loadedAvatarAccount, loadedAvatarUrl, accountName, avatarUrl))
        {
            return true;
        }
        if (!IsSameAvatar(
            Config.Get(AvatarCacheAccountConfigKey, string.Empty),
            Config.Get(AvatarCacheUrlConfigKey, string.Empty),
            accountName,
            avatarUrl))
        {
            return false;
        }

        string cachePath = Path.Combine(Application.persistentDataPath, AvatarCacheFileName);
        if (!File.Exists(cachePath))
        {
            return false;
        }

        Texture2D texture = null;
        try
        {
            texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!texture.LoadImage(File.ReadAllBytes(cachePath), true))
            {
                UnityEngine.Object.Destroy(texture);
                File.Delete(cachePath);
                return false;
            }

            accountAvatar.mainTexture = texture;
            loadedAvatarAccount = accountName;
            loadedAvatarUrl = avatarUrl;
            return true;
        }
        catch (Exception exception)
        {
            if (texture != null)
            {
                UnityEngine.Object.Destroy(texture);
            }
            Debug.LogWarning("读取 MyCard 头像缓存失败: " + exception.Message);
            return false;
        }
    }

    void SaveAvatarCache(string accountName, string avatarUrl, byte[] data)
    {
        if (data == null || data.Length == 0)
        {
            return;
        }

        try
        {
            File.WriteAllBytes(
                Path.Combine(Application.persistentDataPath, AvatarCacheFileName),
                data
            );
            Config.Set(AvatarCacheAccountConfigKey, accountName);
            Config.Set(AvatarCacheUrlConfigKey, avatarUrl);
        }
        catch (Exception exception)
        {
            Debug.LogWarning("保存 MyCard 头像缓存失败: " + exception.Message);
        }
    }

    static string FormatArenaUserInfo(ArenaUserInfo userInfo)
    {
        string athleticRatio = string.IsNullOrEmpty(userInfo.athletic_wl_ratio)
            ? "0.00"
            : userInfo.athletic_wl_ratio;
        string entertainRatio = string.IsNullOrEmpty(userInfo.entertain_wl_ratio)
            ? "0.00"
            : userInfo.entertain_wl_ratio;
        return string.Format(
            "经验 {0}   积分 {1}\n"
                + "经验排名 #{2}   竞技排名 #{3}\n"
                + "竞技 {4}场   {5}胜 {6}负 {7}平   胜率 {8}%\n"
                + "娱乐 {9}场   {10}胜 {11}负 {12}平   胜率 {13}%",
            userInfo.exp,
            userInfo.pt,
            userInfo.exp_rank,
            userInfo.arena_rank,
            userInfo.athletic_all,
            userInfo.athletic_win,
            userInfo.athletic_lose,
            userInfo.athletic_draw,
            athleticRatio,
            userInfo.entertain_all,
            userInfo.entertain_win,
            userInfo.entertain_lose,
            userInfo.entertain_draw,
            entertainRatio
        );
    }

    bool TryLoadCachedAccountProfile(string accountName)
    {
        if (string.IsNullOrEmpty(accountName))
        {
            return false;
        }
        if (string.Equals(accountProfileAccount, accountName, StringComparison.Ordinal)
            && !string.IsNullOrEmpty(accountProfileJson))
        {
            return true;
        }

        accountProfileAccount = accountName;
        accountProfileJson = string.Empty;
        accountProfileText = string.Empty;
        if (!string.Equals(
            Config.Get(ProfileAccountConfigKey, string.Empty),
            accountName,
            StringComparison.Ordinal))
        {
            return false;
        }

        string cachedJson = Config.Get(ProfileJsonConfigKey, string.Empty);
        if (string.IsNullOrEmpty(cachedJson))
        {
            return false;
        }

        try
        {
            ArenaUserInfo userInfo = JsonUtility.FromJson<ArenaUserInfo>(cachedJson);
            if (userInfo == null)
            {
                return false;
            }

            accountProfileJson = cachedJson;
            accountProfileText = FormatArenaUserInfo(userInfo);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    bool UpdateCachedAccountProfile(string accountName, ArenaUserInfo userInfo)
    {
        string profileJson = JsonUtility.ToJson(userInfo);
        if (!HasProfileChanged(
            accountProfileAccount,
            accountProfileJson,
            accountName,
            profileJson))
        {
            return false;
        }

        accountProfileAccount = accountName;
        accountProfileJson = profileJson;
        accountProfileText = FormatArenaUserInfo(userInfo);
        Config.Set(ProfileAccountConfigKey, accountName);
        Config.Set(ProfileJsonConfigKey, profileJson);
        return true;
    }

    static bool HasProfileChanged(
        string cachedAccount,
        string cachedJson,
        string accountName,
        string profileJson)
    {
        return !string.Equals(cachedAccount, accountName, StringComparison.Ordinal)
            || !string.Equals(cachedJson, profileJson, StringComparison.Ordinal);
    }

    void ClearAccountProfileView()
    {
        accountProfileAccount = string.Empty;
        accountProfileJson = string.Empty;
        accountProfileText = string.Empty;
        accountProfileCheckedUsername = string.Empty;
    }

    void StopAccountProfileRefresh()
    {
        if (accountProfileCoroutine != null)
        {
            Program.I().StopCoroutine(accountProfileCoroutine);
            accountProfileCoroutine = null;
        }
    }

    void StartAccountProfileRefresh(bool force = false)
    {
        if (mycardHelper == null || !mycardHelper.HasSession)
        {
            return;
        }
        TryLoadCachedAvatar(mycardHelper.username, mycardHelper.avatarUrl);
        if (!force && string.Equals(
            accountProfileCheckedUsername,
            mycardHelper.username,
            StringComparison.Ordinal))
        {
            return;
        }

        StopAccountProfileRefresh();
        if (!TryLoadCachedAccountProfile(mycardHelper.username))
        {
            string loadingText = InterString.Get("正在加载用户资料...");
            if (accountProfileText != loadingText)
            {
                accountProfileText = loadingText;
                RefreshSessionUi();
            }
        }
        mycardHelper.ResetCancellation();
        accountProfileCoroutine = Program.I().StartCoroutine(
            RefreshAccountProfileCoroutine(
                mycardHelper.username,
                mycardHelper.avatarUrl
            )
        );
    }

    IEnumerator RefreshAccountProfileCoroutine(string accountName, string avatarUrl)
    {
        try
        {
            if (accountAvatar != null
                && IsHttpsUrl(avatarUrl)
                && !IsSameAvatar(
                    loadedAvatarAccount,
                    loadedAvatarUrl,
                    accountName,
                    avatarUrl))
            {
                using (UnityWebRequest request = UnityWebRequestTexture.GetTexture(avatarUrl, true))
                {
                    request.timeout = 15;
                    request.SetRequestHeader("User-Agent", "KoshiPro2iOS");
                    yield return request.SendWebRequest();

                    if (request.result == UnityWebRequest.Result.Success
                        && IsSameAvatar(
                            mycardHelper.username,
                            mycardHelper.avatarUrl,
                            accountName,
                            avatarUrl))
                    {
                        Texture2D texture = DownloadHandlerTexture.GetContent(request);
                        if (texture != null)
                        {
                            accountAvatar.mainTexture = texture;
                            loadedAvatarAccount = accountName;
                            loadedAvatarUrl = avatarUrl;
                            SaveAvatarCache(accountName, avatarUrl, request.downloadHandler.data);
                        }
                    }
                }
            }

            if (!string.Equals(mycardHelper.username, accountName, StringComparison.Ordinal))
            {
                yield break;
            }

            MyCardRequestResult requestResult = null;
            ArenaUserInfo userInfo = null;
            yield return mycardHelper.GetArenaUserInfo(accountName, (result, info) =>
            {
                requestResult = result;
                userInfo = info;
            });

            if (!string.Equals(mycardHelper.username, accountName, StringComparison.Ordinal))
            {
                yield break;
            }

            if (requestResult != null && requestResult.Success && userInfo != null)
            {
                accountProfileCheckedUsername = accountName;
                if (UpdateCachedAccountProfile(accountName, userInfo))
                {
                    RefreshSessionUi();
                }
            }
            else if (string.IsNullOrEmpty(accountProfileJson))
            {
                string unavailableText = InterString.Get("用户资料暂不可用");
                if (accountProfileText != unavailableText)
                {
                    accountProfileText = unavailableText;
                    RefreshSessionUi();
                }
            }
        }
        finally
        {
            accountProfileCoroutine = null;
        }
    }

    void SaveUser()
    {
        string account = (inputUsername.value ?? string.Empty).Trim();
        Config.Set("mycard_username", account);
        if (!string.IsNullOrEmpty(inputPsw.value)
            && !MyCardCredentialStore.SavePassword(account, inputPsw.value))
        {
            Program.PrintToChat(InterString.Get("无法记住 MyCard 密码。"));
        }
        Program.I().selectServer.name = account;
    }

    void LoadUser()
    {
        string account = Config.Get("mycard_username", string.Empty);
        if (string.Equals(account, "MyCard", StringComparison.Ordinal))
        {
            account = string.Empty;
            Config.Set("mycard_username", account);
        }
        inputUsername.value = account;
        LoadRememberedPassword();
    }

    void LoadRememberedPassword()
    {
        string password;
        inputPsw.value = MyCardCredentialStore.TryLoadPassword(
            (inputUsername.value ?? string.Empty).Trim(),
            out password
        )
            ? password
            : string.Empty;
    }

    public void TerminateRequest()
    {
        if (isRequesting && !isLoggingIn)
        {
            CancelMatchRequest();
            return;
        }

        bool wasRequesting = isRequesting;
        if (mycardHelper != null)
        {
            mycardHelper.CancelCurrentRequest();
        }

        if (requestCoroutine != null)
        {
            Program.I().StopCoroutine(requestCoroutine);
            requestCoroutine = null;
        }

        isRequesting = false;
        isLoggingIn = false;
        isCancellingMatch = false;
        matchingArena = string.Empty;
        RefreshSessionUi();
        if (wasRequesting)
        {
            Program.PrintToChat(InterString.Get("登录已中断。"));
        }
    }

    void CancelMatchRequest()
    {
        if (!isRequesting
            || isLoggingIn
            || isCancellingMatch
            || string.IsNullOrEmpty(matchingArena))
        {
            return;
        }

        isCancellingMatch = true;
        mycardHelper.CancelCurrentRequest();
        RefreshSessionUi();
    }

    void RefreshMatchButtons(
        bool canMatch,
        bool isJoining,
        bool isRestartCoolingDown)
    {
        bool cancellingAthletic = isCancellingMatch
            && string.Equals(matchingArena, "athletic", StringComparison.Ordinal);
        bool cancellingEntertain = isCancellingMatch
            && string.Equals(matchingArena, "entertain", StringComparison.Ordinal);
        bool cancelAthletic = IsCancelMatchAction(
            isRequesting,
            isLoggingIn,
            isCancellingMatch,
            matchingArena,
            "athletic");
        bool cancelEntertain = IsCancelMatchAction(
            isRequesting,
            isLoggingIn,
            isCancellingMatch,
            matchingArena,
            "entertain");
        bool canStart = !isRequesting
            && !isJoining
            && !isRestartCoolingDown
            && canMatch;

        if (joinAthleticButton != null)
        {
            joinAthleticButton.isEnabled = canStart || (!isJoining && cancelAthletic);
        }
        if (joinEntertainButton != null)
        {
            joinEntertainButton.isEnabled = canStart || (!isJoining && cancelEntertain);
        }

        string athleticText = InterString.Get(cancellingAthletic
            ? "正在取消匹配"
            : cancelAthletic ? "取消竞技匹配" : "竞技匹配");
        if (joinAthleticButtonLabel != null
            && joinAthleticButtonLabel.text != athleticText)
        {
            joinAthleticButtonLabel.text = athleticText;
        }

        string entertainText = InterString.Get(cancellingEntertain
            ? "正在取消匹配"
            : cancelEntertain ? "取消娱乐匹配" : "娱乐匹配");
        if (joinEntertainButtonLabel != null
            && joinEntertainButtonLabel.text != entertainText)
        {
            joinEntertainButtonLabel.text = entertainText;
        }
    }

    void onClickExit()
    {
        StopAccountProfileRefresh();
        // 主动离开竞技场 = 本轮匹配会话结束，清掉匹配态（否则之后从别的入口断线会被拨回这里）。
        isMatching = false;
        Program.I().shiftToServant(Program.I().menu);
        if (isRequesting)
        {
            if (isLoggingIn)
            {
                TerminateRequest();
            }
            else
            {
                CancelMatchRequest();
            }
        }
        if (TcpHelper.tcpClient != null)
        {
            TcpHelper.Disconnect(true);
        }
    }

    void onClickDatabase()
    {
        Application.OpenURL("https://mycard.moe/ygopro/arena/");
    }

    void onClickCommunity()
    {
        Application.OpenURL("https://ygobbs.com/");
    }

    bool TryStartJoinThread(MatchResultObject matchResultObject)
    {
        if (matchResultObject == null)
        {
            return false;
        }

        if (joinThread != null && joinThread.IsAlive)
        {
            return false;
        }

        string address = matchResultObject.address;
        string userName = mycardHelper.username;
        string port = matchResultObject.port.ToString();
        string roomPassword = matchResultObject.password;
        string version = "0x" + string.Format("{0:X}", Config.ClientVersion);

        joinThread = new Thread(() =>
        {
            TcpHelper.join(address, userName, port, roomPassword, version);
        });
        joinThread.Name = "MyCardJoinThread";
        joinThread.IsBackground = true;
        joinThread.Start();

        return true;
    }

    void ShowLoginRequired()
    {
        StopAccountProfileRefresh();
        ClearAccountProfileView();
        isEditingAccount = true;
        LoadRememberedPassword();
        RefreshSessionUi();
        RMSshow_onlyYes("", InterString.Get("MyCard 登录状态已失效，请重新输入密码。"), null);
    }

    IEnumerator LoginCoroutine(string username, string password)
    {
        isRequesting = true;
        isLoggingIn = true;
        RefreshSessionUi();

        try
        {
            MyCardRequestResult requestResult = null;
            Program.PrintToChat(InterString.Get("正在登录至 MyCard。"));
            yield return mycardHelper.Login(username, password, result =>
            {
                requestResult = result;
            });
            password = null;

            if (requestResult == null || !requestResult.Success)
            {
                string reason = requestResult == null ? "未知错误" : requestResult.Message;
                Program.PrintToChat(InterString.Get("MyCard 登录失败。原因: ") + reason);
                yield break;
            }

            inputUsername.value = mycardHelper.username;
            SaveUser();
            inputPsw.value = string.Empty;
            isEditingAccount = false;
            TryLoadCachedAccountProfile(mycardHelper.username);
            Program.PrintToChat(
                InterString.Get("MyCard 登录成功，用户名: ") + mycardHelper.username
            );
            StartAccountProfileRefresh(true);
        }
        finally
        {
            password = null;
            isLoggingIn = false;
            isRequesting = false;
            requestCoroutine = null;
            RefreshSessionUi();
        }
    }

    void onClickLogin()
    {
        if (isRequesting || (joinThread != null && joinThread.IsAlive))
        {
            return;
        }

        if (!isEditingAccount && HasDisplayedSession())
        {
            StopAccountProfileRefresh();
            ClearAccountProfileView();
            isEditingAccount = true;
            LoadRememberedPassword();
            RefreshSessionUi();
            return;
        }

        string username = (inputUsername.value ?? string.Empty).Trim();
        string password = inputPsw.value ?? string.Empty;
        if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
        {
            string hint = HasDisplayedSession()
                ? "当前账号已登录。若需重新登录或切换账号，请输入用户名和密码。"
                : "请先输入 MyCard 用户名和密码。";
            RMSshow_onlyYes("", InterString.Get(hint), null);
            return;
        }

        inputUsername.value = username;
        StopAccountProfileRefresh();
        mycardHelper.ResetCancellation();
        requestCoroutine = Program.I().StartCoroutine(LoginCoroutine(username, password));
    }

    IEnumerator MatchCoroutine(string matchType)
    {
        isRequesting = true;
        isLoggingIn = false;
        RefreshSessionUi();

        try
        {
            MyCardRequestResult requestResult = null;
            Program.PrintToChat(InterString.Get("正在验证 MyCard 登录状态。"));

            yield return mycardHelper.GetUserU16Secret(result =>
            {
                requestResult = result;
            });

            if (IsCancelledRequest(requestResult))
            {
                yield break;
            }
            if (requestResult == null || !requestResult.Success)
            {
                if (requestResult != null
                    && (requestResult.IsUnauthorized || requestResult.Failure == MyCardRequestFailure.MissingSession))
                {
                    ShowLoginRequired();
                }

                string reason = requestResult == null ? "未知错误" : requestResult.Message;
                Program.PrintToChat(InterString.Get("获取用户密钥失败。原因: ") + reason);
                yield break;
            }

            inputUsername.value = mycardHelper.username;
            SaveUser();
            Program.PrintToChat(InterString.Get("正在请求匹配。匹配类型: ") + matchType);
            MatchResultObject matchResultObject = null;
            requestResult = null;

            yield return mycardHelper.RequestMatch(matchType, (result, match) =>
            {
                requestResult = result;
                matchResultObject = match;
            });

            if (IsCancelledRequest(requestResult))
            {
                yield break;
            }
            if (requestResult != null && requestResult.IsUnauthorized)
            {
                Program.PrintToChat(InterString.Get("匹配凭据已过期，正在刷新。"));
                requestResult = null;

                yield return mycardHelper.GetUserU16Secret(result =>
                {
                    requestResult = result;
                });

                if (IsCancelledRequest(requestResult))
                {
                    yield break;
                }
                if (requestResult == null || !requestResult.Success)
                {
                    if (requestResult != null
                        && (requestResult.IsUnauthorized || requestResult.Failure == MyCardRequestFailure.MissingSession))
                    {
                        ShowLoginRequired();
                    }

                    string reason = requestResult == null ? "未知错误" : requestResult.Message;
                    Program.PrintToChat(InterString.Get("刷新匹配凭据失败。原因: ") + reason);
                    yield break;
                }

                matchResultObject = null;
                requestResult = null;
                yield return mycardHelper.RequestMatch(matchType, (result, match) =>
                {
                    requestResult = result;
                    matchResultObject = match;
                });

                if (IsCancelledRequest(requestResult))
                {
                    yield break;
                }
                if (requestResult != null && requestResult.IsUnauthorized)
                {
                    mycardHelper.ClearSession();
                    ShowLoginRequired();
                }
            }

            if (requestResult == null || !requestResult.Success || matchResultObject == null)
            {
                string reason = requestResult == null ? "未知错误" : requestResult.Message;
                Program.PrintToChat(InterString.Get("匹配请求失败。原因: ") + reason);
                yield break;
            }

            if (!TryStartJoinThread(matchResultObject))
            {
                Program.PrintToChat(InterString.Get("连接任务已存在，请稍后再试。"));
                yield break;
            }

            Program.PrintToChat(InterString.Get("匹配成功。正在进入房间。"));
            accountProfileCheckedUsername = string.Empty;
            isMatching = true;
        }
        finally
        {
            bool wasCancelled = isCancellingMatch;
            isRequesting = false;
            isCancellingMatch = false;
            matchingArena = string.Empty;
            requestCoroutine = null;
            if (wasCancelled)
            {
                matchRestartAllowedAt = Time.realtimeSinceStartup
                    + MatchRestartDebounceSeconds;
                Program.PrintToChat(InterString.Get("匹配已取消。"));
            }
            RefreshSessionUi();
        }
    }

    void StartMatch(string matchType)
    {
        if (IsCancelMatchAction(
            isRequesting,
            isLoggingIn,
            isCancellingMatch,
            matchingArena,
            matchType))
        {
            CancelMatchRequest();
            return;
        }

        if (isCancellingMatch
            || IsMatchRestartCoolingDown(
                Time.realtimeSinceStartup,
                matchRestartAllowedAt)
            || isRequesting
            || (joinThread != null && joinThread.IsAlive))
        {
            return;
        }

        if (isEditingAccount || !HasDisplayedSession())
        {
            RMSshow_onlyYes(
                "",
                InterString.Get("请先在上方完成 MyCard 账号登录，再选择匹配模式。"),
                null
            );
            return;
        }

        Program.PrintToChat(InterString.Get("已开始匹配。"));
        StopAccountProfileRefresh();
        mycardHelper.ResetCancellation();
        matchingArena = matchType;
        requestCoroutine = Program.I().StartCoroutine(MatchCoroutine(matchType));
    }

    void onClickJoinAthletic()
    {
        StartMatch("athletic");
    }

    void onClickJoinEntertain()
    {
        StartMatch("entertain");
    }
}
