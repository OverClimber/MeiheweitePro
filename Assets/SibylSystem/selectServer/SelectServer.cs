using UnityEngine;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Text.RegularExpressions;

public class SelectServer : WindowServantSP
{
    UIPopupList list;
    UIPopupList serverList;

    UIInput inputIP;
    UIInput inputPort;
    UIInput inputPsw;
    UIInput inputVersion;

    UILabel serverLabel;

    public string name = "";

    #region 自定义服务器（KoishiPro2 的下拉逻辑 + 用户可定义、可保存、可管理）

    /// <summary>单个服务器定义。</summary>
    [Serializable]
    private sealed class ServerEntry
    {
        public string name;
        public string ip;
        public string port;
    }

    /// <summary>JsonUtility 需要的 [Serializable] 容器。</summary>
    [Serializable]
    private sealed class ServerList
    {
        public List<ServerEntry> items = new List<ServerEntry>();
    }

    /// <summary>存档键：全部服务器定义（含出厂预置；删掉预置后不会复现）。</summary>
    private const string ServersKey = "customServers";

    /// <summary>存档键：上次选中的服务器（沿用 KoishiPro2 的键名）。OCG 侧就是这个老键。</summary>
    private const string PickerKey = "serversPicker";

    /// <summary>
    /// 存档键：**RD 侧**上次选中的服务器。
    ///
    /// 与「召唤前询问 / 盖放前询问」同一口径（用户 2026-09-19）：两模式各自的 UI 选择互不覆盖。
    /// 共用一个键的话，在 RD 里选了超速决斗服，切回 OCG 就会把 OCG 的选择顶掉。
    /// RD 的服务器的 ip 与 OCG 完全不同（rd.moenext.com），串味了很难一眼看出。
    /// </summary>
    private const string PickerKeyRD = "serversPicker_rd";

    /// <summary>RD 出厂预置：超速决斗服。密码处填 <c>ai</c> 即对服务端 AI（见参考内容/RD/rd服务器.txt）。</summary>
    private const string RdServerName = "超速决斗（RD）";
    private const string RdServerHost = "rd.moenext.com";
    private const string RdServerPort = "961";

    private static List<ServerEntry> RdServerPresets()
    {
        List<ServerEntry> list = new List<ServerEntry>();
        list.Add(new ServerEntry
        {
            name = RdServerName,
            ip = RdServerHost,
            port = RdServerPort,
        });
        return list;
    }

    /// <summary>这个条目是不是 RD 预置（按 host 判，用户自己改了名字也认得出来）。</summary>
    private static bool IsRdPreset(ServerEntry s)
    {
        return s != null && s.ip != null && s.ip.Trim().ToLower() == RdServerHost;
    }

    /// <summary>当前模式该读写的「上次选择」键。</summary>
    private string PickerKeyNow
    {
        get { return GameModeManager.IsRD ? PickerKeyRD : PickerKey; }
    }

    /// <summary>
    /// 该模式**还没有存档时**的默认选中项。
    ///
    /// · OCG 沿用现状 —— <c>[自定义]</c>（老玩家的存档里本来就有值，只在全新安装时生效）。
    /// · RD 默认选预置的「超速决斗（RD）」：进 RD 就是冲着它去的，让用户每次自己在下拉里
    ///   挑一遍没有意义。**更要紧的是不选它的话 ip/port 输入框是空的**，
    ///   用户以为在 RD 服上，点「连接」去的还是上一次那个 OCG 地址（静默连错服务器）。
    /// </summary>
    private string DefaultPickerValue()
    {
        return GameModeManager.IsRD ? RdServerName : CustomItem;
    }

    /// <summary>模式切换钩子只挂一次。</summary>
    private bool modeHookInstalled = false;

    private const string CustomItem = "[自定义]";
    private const string ManageItem = "管理服务器…";
    private const string CancelValue = "__cancel__";
    private const string AddValue = "__add__";

    private List<ServerEntry> servers = new List<ServerEntry>();
    private string currentServer = "";
    private string managedServer = "";
    private bool suppressServerChange = false;

    // 「服务器」编辑窗口（名称 / 域名 / 端口）
    private GameObject serverEditor = null;
    private UIInput editorName = null;
    private UIInput editorIp = null;
    private UIInput editorPort = null;
    private string editingOriginal = "";
    /// <summary>上一次落盘的输入框内容，用来只在变化时记一条轨迹。</summary>
    private string lastEditorValues = "";
    private bool lastPopupOpen = false;

    #region 联机密码历史（按服务器分桶）与上次偏好

    /// <summary>一台服务器（ip:port）自己的密码历史，最近的在最前。</summary>
    [Serializable]
    private sealed class PswBucket
    {
        public string key;
        public List<string> psws = new List<string>();
    }

    /// <summary>JsonUtility 需要的 [Serializable] 容器。</summary>
    [Serializable]
    private sealed class PswStore
    {
        public List<PswBucket> buckets = new List<PswBucket>();
    }

    /// <summary>存档键：全部服务器的密码历史（分桶）。</summary>
    private const string HistoryKey = "hostHistoryV1";

    /// <summary>存档键：[自定义] 模式下上次手输的地址（下拉选中的服务器由 PickerKey 记）。</summary>
    private const string LastIpKey = "lastOnlineIp";
    private const string LastPortKey = "lastOnlinePort";

    /// <summary>每台服务器最多记几条密码。</summary>
    private const int HistoryCap = 8;

    /// <summary>挂在 psw_ 输入框上的「历史密码」候选弹出。</summary>
    private UIPopupList pswList = null;

    private List<PswBucket> pswBuckets = null;
    private bool suppressPswRefresh = false;
    private int pswSuggestIndex = -1;

    #endregion

    /// <summary>出厂预置 = KoishiPro2（hex unity2021 分支）的全部内置服务器。</summary>
    private static List<ServerEntry> DefaultServers()
    {
        List<ServerEntry> defaults = new List<ServerEntry>();
        defaults.Add(new ServerEntry { name = "Koishi服", ip = "koishi.momobako.com", port = "7210" });
        defaults.Add(new ServerEntry { name = "233正式1区", ip = "s1.ygo233.com", port = "233" });
        defaults.Add(new ServerEntry { name = "233正式2区", ip = "s2.ygo233.com", port = "233" });
        defaults.Add(new ServerEntry { name = "233约战区", ip = "s1.ygo233.com", port = "2333" });
        defaults.Add(new ServerEntry { name = "正式+超先行服", ip = "mygo.superpre.pro", port = "888" });
        defaults.Add(new ServerEntry { name = "正式+超先行2服", ip = "mygo2.superpre.pro", port = "888" });
        defaults.Add(new ServerEntry { name = "EXP娱乐服", ip = "e.ygo.pro", port = "23333" });
        defaults.Add(new ServerEntry { name = "决斗编年史", ip = "duels.link", port = "2333" });
        defaults.Add(new ServerEntry { name = "2Pick轮抽", ip = "2pick.moecube.com", port = "765" });
        return defaults;
    }

    private void loadServers()
    {
        servers = null;
        try
        {
            ServerList saved = JsonUtility.FromJson<ServerList>(Config.Get(ServersKey, ""));
            if (saved != null && saved.items != null && saved.items.Count > 0)
            {
                servers = saved.items;
            }
        }
        catch (ArgumentException)
        {
        }
        if (servers == null)
        {
            servers = DefaultServers();
        }
    }

    private void saveServers()
    {
        Config.Set(ServersKey, JsonUtility.ToJson(new ServerList { items = servers }));
    }

    private ServerEntry findServer(string serverName)
    {
        if (serverName == null)
        {
            return null;
        }
        foreach (ServerEntry s in servers)
        {
            if (s.name == serverName)
            {
                return s;
            }
        }
        return null;
    }

    #region 密码历史存取（按 ip:port 分桶）

    /// <summary>服务器身份 = ip:port（与下拉选的名字无关，[自定义] 手输也归同一个桶）。</summary>
    private static string HostKey(string ip, string port)
    {
        return (ip ?? "").Trim() + ":" + (port ?? "").Trim();
    }

    private string CurrentHostKey()
    {
        return HostKey(inputIP != null ? inputIP.value : "", inputPort != null ? inputPort.value : "");
    }

    private void loadPswStore()
    {
        pswBuckets = new List<PswBucket>();
        try
        {
            PswStore saved = JsonUtility.FromJson<PswStore>(Config.Get(HistoryKey, ""));
            if (saved != null && saved.buckets != null)
            {
                pswBuckets = saved.buckets;
            }
        }
        catch (ArgumentException)
        {
        }
        MigrateOldHostsFile();
    }

    /// <summary>
    /// 旧版 hosts.conf 是全局一锅炖（每行 "ip:port psw"，不分服务器）。
    /// 首次升级（分桶存档为空）时按 ip:port 拆进各自的桶；老文件保留不删。
    /// </summary>
    private void MigrateOldHostsFile()
    {
        if (pswBuckets.Count > 0 || File.Exists("config/hosts.conf") == false)
        {
            return;
        }
        try
        {
            string[] lines = File.ReadAllText("config/hosts.conf").Replace("\r", "").Split("\n");
            for (int i = 0; i < lines.Length; i++)
            {
                string line = Regex.Replace(lines[i], "^\\(.*\\)", "").Trim(); // remove old version
                if (line == "")
                {
                    continue;
                }
                string[] parts = line.Split(' ', 2);
                string addr = parts.Length > 0 ? parts[0].Trim() : "";
                string psw = parts.Length > 1 ? parts[1].Trim() : "";
                if (addr == "" || addr == ":")
                {
                    continue;
                }
                InsertPsw(addr, psw);
            }
        }
        catch (Exception)
        {
        }
        if (pswBuckets.Count > 0)
        {
            SavePswStore();
            QuickTestTrace.Log("server", "psw-history migrated buckets=" + pswBuckets.Count);
        }
    }

    private PswBucket BucketFor(string key, bool create)
    {
        if (pswBuckets == null)
        {
            loadPswStore();
        }
        foreach (PswBucket b in pswBuckets)
        {
            if (b.key == key)
            {
                return b;
            }
        }
        if (!create)
        {
            return null;
        }
        PswBucket made = new PswBucket();
        made.key = key;
        pswBuckets.Add(made);
        return made;
    }

    private void InsertPsw(string key, string psw)
    {
        if (psw == null || psw == "")
        {
            return;
        }
        PswBucket b = BucketFor(key, true);
        b.psws.Remove(psw);
        b.psws.Insert(0, psw);
        while (b.psws.Count > HistoryCap)
        {
            b.psws.RemoveAt(b.psws.Count - 1);
        }
    }

    private void SavePswStore()
    {
        Config.Set(HistoryKey, JsonUtility.ToJson(new PswStore { buckets = pswBuckets }));
    }

    private List<string> PswsFor(string key)
    {
        PswBucket b = BucketFor(key, false);
        return b != null ? b.psws : new List<string>();
    }

    private string LastPsw(string key)
    {
        List<string> l = PswsFor(key);
        return l.Count > 0 ? l[0] : "";
    }

    #endregion

    /// <summary>
    /// 在窗口里现场搭出"服务器"行与"保存"按钮（全部代码生成，不动 prefab）：
    /// 1. mainWindow 背景板加高 50（bg 是 Sliced，安全）；
    /// 2. 克隆 nameLine 一行作为 serverLine（label + line 框 + UIPopupList）；
    /// 3. 三行下移、连接按钮下移，腾出空间；
    /// 4. 克隆 join_ 按钮缩小为"保存"。
    /// </summary>
    private void setupServerPicker()
    {
        loadServers();

        UISprite mainWindow = UIHelper.getByName<UISprite>(gameObject, "mainWindow");
        Transform panT = UIHelper.getByName<Transform>(gameObject, "pan");
        Transform joinT = UIHelper.getByName<Transform>(gameObject, "join");
        Transform nameLineT = UIHelper.getByName<Transform>(gameObject, "nameLine");
        if (mainWindow == null || panT == null || joinT == null || nameLineT == null)
        {
            QuickTestTrace.Log("server", "picker ABORT mainWindow=" + (mainWindow != null)
                + " pan=" + (panT != null) + " join=" + (joinT != null)
                + " nameLine=" + (nameLineT != null));
            return;
        }

        // 1. 窗口加高
        mainWindow.height = 344; // 原 294

        // 2. 行区与连接按钮下移
        Vector3 pp = panT.localPosition;
        panT.localPosition = new Vector3(pp.x, pp.y - 25f, pp.z);
        Vector3 jp = joinT.localPosition;
        joinT.localPosition = new Vector3(jp.x, jp.y - 31f, jp.z); // -83.7 → -114.7

        // 3. 克隆 nameLine → serverLine，放在三行之上（pan 内相对 +45）
        GameObject serverLineGO = UnityEngine.Object.Instantiate(nameLineT.gameObject);
        serverLineGO.name = "serverLine";
        serverLineGO.transform.SetParent(nameLineT.parent, false);
        Vector3 sp = nameLineT.localPosition;
        serverLineGO.transform.localPosition = new Vector3(sp.x, sp.y + 45f, sp.z);

        UILabel lineLabel = UIHelper.getByName<UILabel>(serverLineGO, "!lable");
        if (lineLabel != null)
        {
            lineLabel.text = InterString.Get("服务器：");
        }

        // face_ 在原行里是头像选择按钮，图标 sprite='arrodown' 正好就是下拉箭头。
        // 不能直接删（克隆体与原生 face_ 同名，会让 onClickFace 绑到错误对象），
        // 改名保留为「服务器」行的下拉指示箭头，并去掉碰撞盒避免挡点击。
        Transform face = serverLineGO.transform.Find("face_");
        if (face != null)
        {
            face.name = "serverArrow_";
            BoxCollider arrowBox = face.GetComponent<BoxCollider>();
            if (arrowBox != null)
            {
                UnityEngine.Object.Destroy(arrowBox);
            }
        }

        // name_ 输入框 → 静态服务器名显示框 + 弹出列表
        Transform serverTextT = UIHelper.getByName<Transform>(serverLineGO, "name_");
        if (serverTextT != null)
        {
            Component inputComp = serverTextT.GetComponent<UIInput>();
            if (inputComp != null)
            {
                UnityEngine.Object.Destroy(inputComp);
            }
            serverTextT.name = "serverText_";
            serverLabel = UIHelper.getByName<UILabel>(serverTextT.gameObject, "!default");
            if (serverLabel != null)
            {
                serverLabel.text = CustomItem;
            }

            UIPopupList srcList = UIHelper.getByName<UIPopupList>(gameObject, "history_");
            if (srcList != null)
            {
                serverList = serverTextT.gameObject.AddComponent<UIPopupList>();
                // ⚠ 必须把「外观」字段全部照搬，漏一个就换一层皮：
                //   背景 Sprite 是 Wooden Atlas 的 Button —— 它本身是浅灰(219/186/240)，
                //   最终颜色 = 精灵 × backgroundColor。history_ 把它染成黑(0,0,0,1) → 黑框；
                //   而 NGUI 的字段默认值是 Color.white → 同一个精灵就渲染成白框。
                CopyPopupSkin(serverList, srcList);
                EventDelegate.Add(serverList.onChange, onServerPicked);

                // 密码框的「历史密码」候选弹出：挂在 psw_ 输入框本体（同一 GameObject、共用同一个碰撞盒）。
                // 点击输入框 = UIInput 拿焦点 + UIPopupList.OnClick 弹出候选；
                // 输入框保持选中 ⇒ 弹层不会因为「选中物变化」被 CloseIfUnselected 收走。
                if (inputPsw != null)
                {
                    pswList = inputPsw.gameObject.AddComponent<UIPopupList>();
                    CopyPopupSkin(pswList, srcList);
                    pswList.isAnimated = false; // 边打字边重建候选列表，动画会闪
                    EventDelegate.Add(pswList.onChange, onPswPicked);
                    // UIInput/弹层 Start 时会自动选中第一个条目并触发 onChange —— 那会把第一条历史
                    // 密码直接灌进输入框。先塞一个不在候选里的占位值把 mSelectedItem 占住。
                    pswList.value = "__psw_init__";
                }
            }
        }

        // 4. "保存"按钮：克隆 join_，缩到 0.7，放在连接按钮右侧
        Transform joinBtnT = UIHelper.getByName<Transform>(gameObject, "join_");
        if (joinBtnT != null)
        {
            GameObject saveBtn = UnityEngine.Object.Instantiate(joinBtnT.gameObject);
            saveBtn.name = "saveServer_";
            saveBtn.transform.SetParent(joinBtnT.parent.parent, false);
            saveBtn.transform.localPosition = new Vector3(150f, joinT.localPosition.y, joinBtnT.localPosition.z);
            saveBtn.transform.localScale = new Vector3(0.7f, 0.7f, 1f);

            // ⚠ Instantiate 会把 join_ 运行时才挂进 UIEventTrigger 的悬停委托一起复制，
            // 克隆体的 hinter.Start 又挂一遍 → 悬停造两个提示框、移开只消一个，另一个赖 5 秒。
            // 清掉后让 hinter.Start 重新挂唯一一份。
            UIEventTrigger saveTrigger = saveBtn.GetComponent<UIEventTrigger>();
            if (saveTrigger != null)
            {
                saveTrigger.onHoverOver.Clear();
                saveTrigger.onHoverOut.Clear();
                saveTrigger.onPress.Clear();
            }

            UIHelper.trySetLableText(saveBtn, "!lable", InterString.Get("保存"));
            Transform icon = saveBtn.transform.Find("Texture");
            if (icon != null)
            {
                UnityEngine.Object.Destroy(icon.gameObject);
            }
            hinter hint = saveBtn.GetComponent<hinter>();
            if (hint != null)
            {
                hint.str = InterString.Get("打开服务器编辑窗口（名称 / 域名 / 端口）");
            }
            UIHelper.registEvent(gameObject, "saveServer_", onSaveServerClicked);
        }

        // 5. 恢复上次选择（无存档时按模式取默认：[自定义] / RD 预置）
        refreshServerItems();
        applyServer(Config.Get(PickerKeyNow, DefaultPickerValue()), false);

        QuickTestTrace.Log("server", "picker built servers=" + servers.Count
            + " popup=" + (serverList != null ? "ok" : "null")
            + " row=" + (UIHelper.getByName<Transform>(gameObject, "serverLine") != null ? "ok" : "null")
            + " save=" + (UIHelper.getByName<Transform>(gameObject, "saveServer_") != null ? "ok" : "null"));
        if (serverList != null)
        {
            Color bg = serverList.backgroundColor;
            QuickTestTrace.Log("server", "popup skin sprite=" + serverList.backgroundSprite
                + " atlas=" + (serverList.atlas != null ? serverList.atlas.name : "null")
                + " bgColor=" + Color32Str(bg)
                + " textColor=" + Color32Str(serverList.textColor)
                + " separatePanel=" + serverList.separatePanel);
        }
    }

    /// <summary>
    /// 当前模式下该显示的服务器条目（_plan_rdmode.md 清单 6）。
    ///
    /// · OCG：**现状** —— 用户自己的清单，但把 RD 预置剔掉（回 OCG 不该看到超速决斗服）。
    /// · RD：RD 预置排最前，后面**照旧接用户自己的清单** ——
    ///   私有/第三方 RD 服的 host 千奇百怪，靠 IsRdPreset 认不出来，
    ///   RD 下把用户清单藏起来等于把人家的服务器也藏了。
    /// </summary>
    private List<ServerEntry> VisibleServers()
    {
        List<ServerEntry> list = new List<ServerEntry>();
        if (GameModeManager.IsRD)
        {
            foreach (ServerEntry s in RdServerPresets())
            {
                list.Add(s);
            }
            foreach (ServerEntry s in servers)
            {
                list.Add(s);
            }
            return list;
        }
        foreach (ServerEntry s in servers)
        {
            if (IsRdPreset(s))
            {
                continue;
            }
            list.Add(s);
        }
        return list;
    }

    private void refreshServerItems()
    {
        if (serverList == null)
        {
            return;
        }
        suppressServerChange = true;
        serverList.Clear();
        foreach (ServerEntry s in VisibleServers())
        {
            serverList.items.Add(s.name);
        }
        serverList.items.Add(CustomItem);
        serverList.items.Add(ManageItem);
        string wanted = currentServer == "" ? CustomItem : currentServer;
        serverList.value = serverList.items.Contains(wanted) ? wanted : CustomItem;
        suppressServerChange = false;
        if (serverLabel != null)
        {
            serverLabel.text = serverList.value;
        }
        QuickTestTrace.Log("mode", "server items=" + (serverList.items.Count - 2)
            + " mode=" + GameModeManager.ModeLabel
            + " value=" + serverList.value
            // 清单内容也落盘（去掉尾部的 [自定义] / 管理项）：RD 线的判据是
            // 「超速决斗预置在 RD 清单里、在 OCG 清单里没有」——只报条数判不出来，
            // 用户自己的存档里也可能恰好有一台 rd.moenext.com。
            + " all=[" + string.Join("|",
                serverList.items.GetRange(0, serverList.items.Count - 2).ToArray()) + "]");
    }

    /// <summary>
    /// 切模式时重建服务器下拉：RD 进来看得到预置的超速决斗服（默认就选它），
    /// 回 OCG 则恢复 OCG 自己上次选的那台（两模式各记各的，见 PickerKeyRD）。
    /// </summary>
    private void OnGameModeChanged(GameModeManager.Mode mode)
    {
        if (serverList == null)
        {
            return;
        }
        currentServer = "";
        refreshServerItems();
        applyServer(Config.Get(PickerKeyNow, DefaultPickerValue()), false);
        QuickTestTrace.Log("mode", "server picker rebuilt mode=" + GameModeManager.ModeLabel
            + " ip=" + (inputIP != null ? inputIP.value : "") 
            + " port=" + (inputPort != null ? inputPort.value : "")
            // ⚠ 服务器名可能是用户自己起的、带空格，`picked=` 与禁限表探针同一口径放**行尾**。
            + " picked=" + serverList.value);
    }

    private void InstallModeHook()
    {
        if (modeHookInstalled)
        {
            return;
        }
        modeHookInstalled = true;
        GameModeManager.Changed += OnGameModeChanged;
    }

    private void onServerPicked()
    {
        if (suppressServerChange || serverList == null)
        {
            return;
        }
        applyServer(serverList.value, true);
    }

    /// <summary>选中一个服务器：填充 IP/端口；[自定义] 不动输入框；"管理服务器…" 进入管理流程。</summary>
    private void applyServer(string picked, bool remember)
    {
        if (picked == null || picked == "")
        {
            picked = CustomItem;
        }
        if (picked == ManageItem)
        {
            manageServers();
            suppressServerChange = true;
            serverList.value = currentServer == "" ? CustomItem : currentServer;
            suppressServerChange = false;
            if (serverLabel != null)
            {
                serverLabel.text = serverList.value;
            }
            return;
        }
        currentServer = picked;
        ServerEntry s = findServer(picked);
        if (s == null)
        {
            // RD 预置不在用户存档里（servers 只装存档内容），单独捞一下 ——
            // 否则选中「超速决斗（RD）」不会把 ip/port 填进输入框，点了连接还是上一次的地址。
            foreach (ServerEntry preset in RdServerPresets())
            {
                if (preset.name == picked)
                {
                    s = preset;
                    break;
                }
            }
        }
        if (s != null)
        {
            inputIP.value = s.ip;
            inputPort.value = s.port;
        }
        // [自定义]：保留输入框现有内容
        if (serverLabel != null)
        {
            serverLabel.text = picked;
        }
        // 下拉框自己那一格也要落到同一个值。
        // ⚠ applyServer 现在会被「进 RD 默认选超速决斗服」（DefaultPickerValue）直接调用，
        //   而 refreshServerItems 是按 currentServer 定位下拉的 —— 只改 serverLabel 的话
        //   标题写着「超速决斗（RD）」、下拉还停在 [自定义]，用户一拉清单就被顶回 [自定义]。
        if (serverList != null && serverList.items != null
            && serverList.items.Contains(picked) && serverList.value != picked)
        {
            suppressServerChange = true;
            serverList.value = picked;
            suppressServerChange = false;
        }
        if (remember)
        {
            Config.Set(PickerKeyNow, picked);
        }
        RefreshServerDependentUI();
    }

    #region 密码历史 UI（历史下拉按服务器过滤 + 密码框候选弹出）

    private static void CopyPopupSkin(UIPopupList dst, UIPopupList src)
    {
        dst.atlas = src.atlas;
        dst.bitmapFont = src.bitmapFont;
        dst.trueTypeFont = src.trueTypeFont;
        dst.fontSize = src.fontSize;
        dst.fontStyle = src.fontStyle;
        dst.backgroundSprite = src.backgroundSprite;
        dst.highlightSprite = src.highlightSprite;
        dst.textColor = src.textColor;
        dst.position = src.position;
        dst.alignment = src.alignment;
        dst.backgroundColor = src.backgroundColor;
        dst.highlightColor = src.highlightColor;
        dst.padding = src.padding;
        dst.isAnimated = src.isAnimated;
        dst.isLocalized = false;
        dst.separatePanel = src.separatePanel;
        dst.openOn = src.openOn;
    }

    /// <summary>服务器（ip:port）变化后：恢复该服务器上一次的密码，并重建两个候选列表。</summary>
    private void RefreshServerDependentUI()
    {
        string key = CurrentHostKey();
        string last = LastPsw(key);
        if (inputPsw != null)
        {
            suppressPswRefresh = true;
            inputPsw.value = last;
            suppressPswRefresh = false;
        }
        pswSuggestIndex = -1;
        RefreshHistoryPopup();
        RefreshPswSuggestions();
        QuickTestTrace.Log("server", "psw-restore key=" + key + " last=[" + last + "]"
            + " total=" + PswsFor(key).Count);
    }

    /// <summary>「历史记录」下拉：只列当前服务器（ip:port）自己的记录，不再是全局一锅炖。</summary>
    private void RefreshHistoryPopup()
    {
        if (list == null)
        {
            return;
        }
        string key = CurrentHostKey();
        list.Clear();
        foreach (string p in PswsFor(key))
        {
            list.items.Add(key + " " + p);
        }
    }

    /// <summary>
    /// 按输入内容的前缀过滤当前服务器的历史密码，作为 psw_ 输入框的候选。
    /// 弹层已开时「关了重开」重建 —— 这样边打字边过滤是实时的
    /// （isAnimated=false，重建无闪烁；输入框焦点不受影响）。
    /// </summary>
    private void RefreshPswSuggestions()
    {
        if (pswList == null || inputPsw == null)
        {
            return;
        }
        string typed = (inputPsw.value ?? "").Trim();
        pswList.items.Clear();
        foreach (string p in PswsFor(CurrentHostKey()))
        {
            if (typed == "" || p.StartsWith(typed, StringComparison.Ordinal))
            {
                pswList.items.Add(p);
            }
        }
        pswSuggestIndex = -1;
        bool open = UIPopupList.current == pswList;
        if (open)
        {
            pswList.CloseSelf();
            if (pswList.items.Count > 0)
            {
                pswList.Show();
                QuickTestTrace.Log("server", "psw-suggest typed-len=" + typed.Length
                    + " matched=" + pswList.items.Count + " reopened");
            }
        }
    }

    private void onPswInputChanged()
    {
        if (suppressPswRefresh)
        {
            return;
        }
        RefreshPswSuggestions();
        QuickTestTrace.Log("server", "psw-value=[" + (inputPsw.value ?? "") + "]");
    }

    private void onHostFieldChanged()
    {
        // 手输的 ip/port 变了 = 换了一台服务器：密码也要换成那台的偏好
        RefreshServerDependentUI();
    }

    /// <summary>点选候选密码：填进输入框（占位/已不在候选里的值一律不落框）。</summary>
    private void onPswPicked()
    {
        if (pswList == null || inputPsw == null)
        {
            return;
        }
        string picked = pswList.value;
        if (picked == null || pswList.items.IndexOf(picked) < 0)
        {
            return;
        }
        suppressPswRefresh = true;
        inputPsw.value = picked;
        suppressPswRefresh = false;
        QuickTestTrace.Log("server", "psw picked len=" + picked.Length);
    }

    /// <summary>
    /// 方向键循环选择候选并**当场自动填入**，Enter/小键盘 Enter 确认收起。
    /// 本工程的 UICamera.useController=false，NGUI 自带的 OnNavigate（方向键高亮）不会触发，
    /// 所以这里自己管光标 —— 顺带正好是用户要的「方向键选择即填入」。
    /// </summary>
    private void PswSuggestKeyboard()
    {
        if (pswList == null || inputPsw == null || UIPopupList.current != pswList)
        {
            pswSuggestIndex = -1;
            return;
        }
        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
        {
            pswList.CloseSelf();
            return;
        }
        int count = pswList.items.Count;
        if (count == 0)
        {
            return;
        }
        bool down = Input.GetKeyDown(KeyCode.DownArrow);
        bool up = Input.GetKeyDown(KeyCode.UpArrow);
        if (!down && !up)
        {
            return;
        }
        if (pswSuggestIndex < 0 || pswSuggestIndex >= count)
        {
            pswSuggestIndex = down ? 0 : count - 1;
        }
        else
        {
            pswSuggestIndex = (pswSuggestIndex + (down ? 1 : -1) + count) % count;
        }
        suppressPswRefresh = true;   // 填入不要触发前缀重过滤，候选列表保持稳定
        inputPsw.value = pswList.items[pswSuggestIndex];
        suppressPswRefresh = false;
        QuickTestTrace.Log("server", "psw-key index=" + pswSuggestIndex + " of " + count
            + " value=[" + pswList.items[pswSuggestIndex] + "]");
    }

    #endregion

    private void onSaveServerClicked()
    {
        // 「保存」现在直接开编辑窗口：名称 / 域名 / 端口都能改，
        // 主窗口里已经填好的地址会带过去，不用先在外面填一半。
        ServerEntry existing = null;
        if (currentServer != "" && currentServer != CustomItem && currentServer != ManageItem)
        {
            existing = findServer(currentServer);
        }
        openServerEditor(existing);
    }

    private void manageServers()
    {
        if (servers.Count == 0)
        {
            // 一台都没存过时直接进编辑窗口，省掉「列表里只剩返回」的空转。
            openServerEditor(null);
            return;
        }
        List<messageSystemValue> options = new List<messageSystemValue>();
        foreach (ServerEntry s in servers)
        {
            options.Add(new messageSystemValue { value = s.name, hint = s.name });
        }
        options.Add(new messageSystemValue { value = AddValue, hint = InterString.Get("新增服务器…") });
        options.Add(new messageSystemValue { value = CancelValue, hint = InterString.Get("返回") });
        RMSshow_singleChoice("manage_pick", options);
    }

    #region 服务器编辑窗口

    /// <summary>
    /// 弹出「服务器」编辑窗口，用来输入 名称 / 域名 / 端口 并保存。
    ///
    /// 直接复用本 servant 自己的 prefab：它自带窗口底板（mainWindow）、整屏遮罩（glass）、
    /// 名称行、地址行和两个按钮，样式与本页天然一致，不必手搭 UI。
    /// 只把用不上的「密码 / 版本 / 历史」那一行和头像按钮去掉，
    /// 再把「连接 / 关闭」改叫「保存 / 取消」。
    /// </summary>
    private void openServerEditor(ServerEntry existing)
    {
        closeServerEditor();

        GameObject mod = Program.I().new_ui_selectServer;
        if (mod == null)
        {
            return;
        }

        float scale = Mathf.Max(0.01f, Screen.height / 700f);
        Vector3 centre = Program.camera_main_2d.ScreenToWorldPoint(
            new Vector3(Screen.width * 0.5f, Screen.height * 0.5f, 0f)
        );
        serverEditor = create_s(
            mod,
            centre,
            Vector3.zero,
            true,
            Program.ui_main_2d,
            true,
            new Vector3(scale, scale, scale)
        );
        serverEditor.name = "serverEditor";
        UIHelper.InterGameObject(serverEditor);

        Transform nameRow = UIHelper.getByName<Transform>(serverEditor, "nameLine");
        Transform ipRow = UIHelper.getByName<Transform>(serverEditor, "ipLine");
        Transform pswRow = UIHelper.getByName<Transform>(serverEditor, "pswLine");

        // 这一行是密码 / 客户端版本 / 历史记录，编辑服务器用不上。
        if (pswRow != null)
        {
            pswRow.gameObject.SetActive(false);
        }
        // 昵称行里的头像选择按钮同理。
        if (nameRow != null)
        {
            Transform face = nameRow.Find("face_");
            if (face != null)
            {
                UnityEngine.Object.Destroy(face.gameObject);
            }
        }

        SetRowLabel(nameRow, "名称：");
        SetRowLabel(ipRow, "域名 / 端口：");
        SetButtonLabel(serverEditor, "join_", "保存");
        SetButtonLabel(serverEditor, "exit_", "取消");
        Transform mainWindow = UIHelper.getByName<Transform>(serverEditor, "mainWindow");
        if (mainWindow != null)
        {
            SetRowLabel(mainWindow, "服务器");
        }

        editorName = UIHelper.getByName<UIInput>(serverEditor, "name_");
        editorIp = UIHelper.getByName<UIInput>(serverEditor, "ip_");
        editorPort = UIHelper.getByName<UIInput>(serverEditor, "port_");

        editingOriginal = existing != null ? existing.name : string.Empty;
        editorName.value = editingOriginal;
        editorIp.value = existing != null
            ? existing.ip
            : (inputIP != null ? inputIP.value : string.Empty);
        editorPort.value = existing != null
            ? existing.port
            : (inputPort != null ? inputPort.value : string.Empty);
        lastEditorValues = "";

        UIHelper.registEvent(serverEditor, "join_", OnEditorSave);
        UIHelper.registEvent(serverEditor, "exit_", OnEditorCancel);
        // 等 0.3s 的弹出缩放走完再对焦/报坐标：缩放期间子节点的世界坐标是塌在一起的。
        Program.go(420, AfterEditorOpened);
    }

    /// <summary>
    /// 弹窗稳定后要做的事：聚焦名称框，并把三个输入框与两个按钮的屏幕坐标落盘。
    /// 验收脚本靠真光标操作，坐标不能猜（同 Ocgcore 里撤回按钮的做法）。
    /// </summary>
    private void AfterEditorOpened()
    {
        if (serverEditor == null)
        {
            return;
        }
        if (editorName != null)
        {
            editorName.isSelected = true;
        }
        LogAnchor("editor", "name_", editorName != null ? editorName.transform : null);
        LogAnchor("editor", "ip_", editorIp != null ? editorIp.transform : null);
        LogAnchor("editor", "port_", editorPort != null ? editorPort.transform : null);
        LogAnchor("editor", "save", UIHelper.getByName<Transform>(serverEditor, "join_"));
        LogAnchor("editor", "cancel", UIHelper.getByName<Transform>(serverEditor, "exit_"));
        QuickTestTrace.Log("editor", "opened original=" + editingOriginal);
    }

    /// <summary>颜色落盘成 0-255，方便脚本直接和期望值比。</summary>
    private static string Color32Str(Color c)
    {
        return "(" + Mathf.RoundToInt(c.r * 255f) + "," + Mathf.RoundToInt(c.g * 255f)
            + "," + Mathf.RoundToInt(c.b * 255f) + "," + Mathf.RoundToInt(c.a * 255f) + ")";
    }

    /// <summary>验收脚本用的坐标落盘（y 自下往上，脚本自己换算成客户区）。</summary>
    private static void LogAnchor(string tag, string name, Transform target)
    {
        if (target == null)
        {
            QuickTestTrace.Log(tag, name + " = null");
            return;
        }
        Vector3 sp = Program.camera_main_2d.WorldToScreenPoint(target.position);
        QuickTestTrace.Log(tag, name + " screen=(" + Mathf.RoundToInt(sp.x) + ","
            + Mathf.RoundToInt(Screen.height - sp.y) + ")");
    }

    private void closeServerEditor()
    {
        if (serverEditor != null)
        {
            destroy(serverEditor, 0f, false, true);
            serverEditor = null;
            QuickTestTrace.Log("editor", "closed");
        }
        editorName = null;
        editorIp = null;
        editorPort = null;
        editingOriginal = string.Empty;
        lastEditorValues = string.Empty;
    }

    /// <summary>只改行首那一枚标签，不用 getByName 递归找（行里同名标签不少）。</summary>
    private static void SetRowLabel(Transform row, string text)
    {
        if (row == null)
        {
            return;
        }
        Transform label = row.Find("!lable");
        if (label == null)
        {
            return;
        }
        UILabel ui = label.GetComponent<UILabel>();
        if (ui != null)
        {
            ui.text = InterString.Get(text);
        }
    }

    /// <summary>改按钮文字：按节点名取到按钮本身，再取它自己那一枚标签。</summary>
    private static void SetButtonLabel(GameObject root, string buttonName, string text)
    {
        Transform button = UIHelper.getByName<Transform>(root, buttonName);
        if (button == null)
        {
            return;
        }
        Transform label = button.Find("!lable");
        UILabel ui = label != null
            ? label.GetComponent<UILabel>()
            : button.GetComponentInChildren<UILabel>();
        if (ui != null)
        {
            ui.text = InterString.Get(text);
        }
    }

    /// <summary>校验没过：弹提示，同时把原因写进轨迹（验收脚本据此区分「字没进去」和「存盘失败」）。</summary>
    private void EditorBlocked(string reason, string text)
    {
        QuickTestTrace.Log("editor", "blocked " + reason);
        RMSshow_onlyYes("", InterString.Get(text), null);
    }

    private void OnEditorSave()
    {
        string newName = ((editorName != null ? editorName.value : "") ?? "").Trim();
        string ip = ((editorIp != null ? editorIp.value : "") ?? "").Trim();
        string port = ((editorPort != null ? editorPort.value : "") ?? "").Trim();

        if (newName == "")
        {
            EditorBlocked("name-empty", "请先给这台服务器起个名字。");
            return;
        }
        if (newName == CustomItem || newName == ManageItem
            || newName == AddValue || newName == CancelValue)
        {
            EditorBlocked("name-reserved", "这个名字和内置条目冲突，请换一个。");
            return;
        }
        if (ip == "" || port == "")
        {
            EditorBlocked("addr-empty", "域名和端口都不能为空。");
            return;
        }
        ServerEntry clash = findServer(newName);
        if (clash != null && clash.name != editingOriginal)
        {
            EditorBlocked("name-taken", "已经有同名的服务器了，请换一个名字。");
            return;
        }

        // 原名存在就改它（改名也算改这条），否则新增。
        ServerEntry entry = editingOriginal != "" ? findServer(editingOriginal) : null;
        if (entry == null)
        {
            entry = new ServerEntry();
            servers.Add(entry);
        }
        entry.name = newName;
        entry.ip = ip;
        entry.port = port;
        saveServers();

        currentServer = newName;
        Config.Set(PickerKeyNow, newName);
        refreshServerItems();
        if (serverLabel != null)
        {
            serverLabel.text = newName;
        }
        if (inputIP != null)
        {
            inputIP.value = ip;
        }
        if (inputPort != null)
        {
            inputPort.value = port;
        }

        QuickTestTrace.Log("editor", "saved name=" + newName + " ip=" + ip + " port=" + port
            + " total=" + servers.Count);
        closeServerEditor();
    }

    private void OnEditorCancel()
    {
        closeServerEditor();
    }

    public override void ES_quit()
    {
        closeServerEditor();
        base.ES_quit();
    }

    #endregion

    public override void ES_RMS(string hashCode, List<messageSystemValue> result)
    {
        base.ES_RMS(hashCode, result);
        if (result == null || result.Count == 0)
        {
            return;
        }
        string v = result[0].value;
        if (hashCode == "manage_pick")
        {
            if (v == CancelValue)
            {
                return;
            }
            if (v == AddValue)
            {
                openServerEditor(null);
                return;
            }
            managedServer = v;
            List<messageSystemValue> actions = new List<messageSystemValue>
            {
                new messageSystemValue { value = "__edit__", hint = InterString.Get("编辑名称 / 域名 / 端口") },
                new messageSystemValue { value = "__load__", hint = InterString.Get("直接使用这一台") },
                new messageSystemValue { value = "__delete__", hint = InterString.Get("删除") },
                new messageSystemValue { value = CancelValue, hint = InterString.Get("返回") },
            };
            RMSshow_singleChoice("manage_act", actions);
        }
        else if (hashCode == "manage_act")
        {
            if (v == "__edit__")
            {
                openServerEditor(findServer(managedServer));
            }
            else if (v == "__load__")
            {
                applyServer(managedServer, true);
            }
            else if (v == "__delete__")
            {
                RMSshow_yesOrNo("manage_delete", InterString.Get("确认删除服务器「[?]」？", managedServer),
                    new messageSystemValue { value = "yes" }, new messageSystemValue { value = "no" });
            }
        }
        else if (hashCode == "manage_delete" && v == "yes")
        {
            ServerEntry e = findServer(managedServer);
            if (e != null)
            {
                servers.Remove(e);
                saveServers();
            }
            if (currentServer == managedServer)
            {
                currentServer = CustomItem;
            }
            refreshServerItems();
        }
    }

    #endregion

    public override void initialize()
    {
        createWindow(Program.I().new_ui_selectServer);
        UIHelper.registEvent(gameObject, "exit_", onClickExit);
        UIHelper.registEvent(gameObject, "face_", onClickFace);
        UIHelper.registEvent(gameObject, "join_", onClickJoin);
        name = Config.Get("name", "一秒一喵机会");
        UIHelper.getByName<UIInput>(gameObject, "name_").value = name;
        list = UIHelper.getByName<UIPopupList>(gameObject, "history_");
        UIHelper.registEvent(gameObject,"history_", onSelected);
        inputIP = UIHelper.getByName<UIInput>(gameObject, "ip_");
        inputPort = UIHelper.getByName<UIInput>(gameObject, "port_");
        inputPsw = UIHelper.getByName<UIInput>(gameObject, "psw_");
        inputVersion = UIHelper.getByName<UIInput>(gameObject, "version_");
        inputVersion.value = "0x" + String.Format("{0:X}", Config.ClientVersion);
        // 密码候选跟着「输入的 ip:port」和「输入的内容」走
        EventDelegate.Add(inputPsw.onChange, onPswInputChanged);
        EventDelegate.Add(inputIP.onChange, onHostFieldChanged);
        EventDelegate.Add(inputPort.onChange, onHostFieldChanged);
        setupServerPicker();
        InstallModeHook();
        SetActiveFalse();
    }

    void onSelected()
    {
        if (list != null)
        {
            readString(list.value);
        }
    }

    private void readString(string str)
    {
        string remain = "";
        string ip = "", port = "", psw = "";
        string[] splited;
        splited = str.Split(":");
        try
        {
            ip = splited[0];
            remain = splited[1];
        }
        catch (Exception)
        {
        }
        splited = remain.Split(" ");
        try
        {
            port = splited[0];
            psw = splited[1];
        }
        catch (Exception)
        {
        }
        inputIP.value = ip;
        inputPort.value = port;
        inputPsw.value = psw;
    }

    public override void show()
    {
        base.show();
        Program.I().room.RMSshow_clear();
        // 恢复上次的服务器与该服务器的上一次密码。
        // ⛔ 不再用 hosts.conf 第一行覆盖输入框 —— 那正是「选好的服务器被历史顶掉」的根因。
        applyServer(Config.Get(PickerKeyNow, DefaultPickerValue()), false);
        if (currentServer == CustomItem)
        {
            // [自定义] 没有服务器条目可填，上次手输的地址也要原样还回来
            inputIP.value = Config.Get(LastIpKey, "");
            inputPort.value = Config.Get(LastPortKey, "");
            RefreshServerDependentUI();
        }
        Program.charge();
        Program.I().ocgcore.returnServant = Program.I().selectServer;
        // 窗口缩放（fixScreenProblem）在 +50ms 才生效，之后再报坐标给验收脚本。
        // ⚠ 单批 300ms 报的是 tween 途中的位置（见 skill §3.5a3）——连报 4 批，取最后一批。
        Program.go(300, LogPickerAnchors);
        Program.go(420, LogPickerAnchors);
        Program.go(540, LogPickerAnchors);
        Program.go(660, LogPickerAnchors);
    }

    /// <summary>把「服务器」行与保存/连接按钮的屏幕坐标落盘，供真光标脚本定位。</summary>
    private void LogPickerAnchors()
    {
        if (gameObject == null || !isShowed)
        {
            return;
        }
        LogAnchor("server", "row", UIHelper.getByName<Transform>(gameObject, "serverLine"));
        // ⚠ row 是「行容器」，它的中点在「服务器：」标签与显示框之间的空白上，那里没有
        //   碰撞盒 —— 拿 row 的坐标去点是点不开下拉框的（实测点了个空）。
        //   要开下拉框必须点 serverText_ 本体。
        LogAnchor("server", "popup", UIHelper.getByName<Transform>(gameObject, "serverText_"));
        LogAnchor("server", "save", UIHelper.getByName<Transform>(gameObject, "saveServer_"));
        LogAnchor("server", "join", UIHelper.getByName<Transform>(gameObject, "join_"));
        LogAnchor("server", "exit", UIHelper.getByName<Transform>(gameObject, "exit_"));
        // 密码历史验收用的输入框锚点（报**碰撞盒中心** —— transform 的锚点不在正中，
        // 照着点会落到框外，见 skill §3.9.1）
        LogAnchorBox("server", "ip", inputIP != null ? inputIP.gameObject : null);
        LogAnchorBox("server", "port", inputPort != null ? inputPort.gameObject : null);
        LogAnchorBox("server", "psw", inputPsw != null ? inputPsw.gameObject : null);
        Transform nameT = UIHelper.getByName<Transform>(gameObject, "name_");
        LogAnchorBox("server", "name", nameT != null ? nameT.gameObject : null);
    }

    /// <summary>验收脚本用的坐标落盘（碰撞盒中心口径；y 自下往上，脚本自己换算成客户区）。</summary>
    private static void LogAnchorBox(string tag, string name, GameObject go)
    {
        if (go == null)
        {
            QuickTestTrace.Log(tag, name + " = null");
            return;
        }
        Vector3 sp = Program.camera_main_2d.WorldToScreenPoint(Ocgcore.ButtonWorldCenter(go));
        QuickTestTrace.Log(tag, name + " screen=(" + Mathf.RoundToInt(sp.x) + ","
            + Mathf.RoundToInt(Screen.height - sp.y) + ")");
    }

    public override void preFrameFunction()
    {
        base.preFrameFunction();
        Menu.checkCommend();
        TraceEditorValues();
        TracePopupState();
        PswSuggestKeyboard();
    }

    /// <summary>下拉框开/合的翻转时刻（UIPopupList.isOpen 是静态可读的）。</summary>
    private void TracePopupState()
    {
        if (!isShowed)
        {
            return;
        }
        bool now = UIPopupList.isOpen;
        if (now != lastPopupOpen)
        {
            lastPopupOpen = now;
            QuickTestTrace.Log("server", "popup isOpen=" + now);
        }
    }

    /// <summary>
    /// 编辑窗口开着时，把三个输入框的内容在变化的那一刻记一条轨迹。
    ///
    /// 排查用：验收脚本是「注入键盘 → 点保存」，一旦文字没进 Unity（比如系统输入法
    /// 把字母当成组字吃掉），OnEditorSave 只会弹「请先起个名字」而看不出原因。
    /// 有这条轨迹就能立刻区分「字没进去」和「存盘失败」。
    /// </summary>
    private void TraceEditorValues()
    {
        if (serverEditor == null || !QuickTestTrace.Enabled)
        {
            return;
        }
        string v = "name=[" + (editorName != null ? editorName.value : "?")
            + "] ip=[" + (editorIp != null ? editorIp.value : "?")
            + "] port=[" + (editorPort != null ? editorPort.value : "?") + "]";
        if (v == lastEditorValues)
        {
            return;
        }
        lastEditorValues = v;
        QuickTestTrace.Log("editor", "values " + v);
    }

    void onClickExit()
    {
        if (Program.exitOnReturn)
            Program.I().menu.onClickExit();
        else
            Program.I().shiftToServant(Program.I().menu);
        if (TcpHelper.tcpClient != null)
        {
            if (TcpHelper.tcpClient.Connected)
            {
                TcpHelper.tcpClient.Close();
            }
        }
    }

    void onClickJoin()
    {
        if (!isShowed)
        {
            return;
        }
        string Name = UIHelper.getByName<UIInput>(gameObject, "name_").value;
        string ipString = UIHelper.getByName<UIInput>(gameObject, "ip_").value;
        string portString = UIHelper.getByName<UIInput>(gameObject, "port_").value;
        string pswString = UIHelper.getByName<UIInput>(gameObject, "psw_").value;
        string versionString = UIHelper.getByName<UIInput>(gameObject, "version_").value;
        KF_onlineGame(Name, ipString, portString, versionString, pswString);
    }

    public void KF_onlineGame(string Name,string ipString, string portString, string versionString, string pswString="")
    {
        name = Name;
        Config.Set("name", name);
        if (ipString == "" || portString == "")
        {
            RMSshow_onlyYes("", InterString.Get("非法输入！请检查输入的主机名。"), null);
        }
        else
        {
            if (name != "")
            {
                string key = HostKey(ipString, portString);
                InsertPsw(key, pswString);
                SavePswStore();
                // 就算从来没动过服务器下拉框（[自定义] 手输地址），这次选择也要记住
                Config.Set(PickerKeyNow, currentServer == "" ? CustomItem : currentServer);
                Config.Set(LastIpKey, ipString);
                Config.Set(LastPortKey, portString);
                RefreshHistoryPopup();
                RefreshPswSuggestions();
                (new Thread(() => { TcpHelper.join(ipString, name, portString, pswString,versionString); })).Start();
            }
            else
            {
                RMSshow_onlyYes("", InterString.Get("昵称不能为空。"), null);
            }
        }
    }

    GameObject faceShow = null;

    void onClickFace()
    {
        name = UIHelper.getByName<UIInput>(gameObject, "name_").value;
        RMSshow_face("showFace", name);
        Config.Set("name", name);
    }

}
