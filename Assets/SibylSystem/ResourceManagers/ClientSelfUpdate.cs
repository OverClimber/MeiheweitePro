using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

/// <summary>
/// **客户端本体**自更新检查（2026-09-25，v1.3 起）。
///
/// 边界（用户 2026-09-25 定稿）：
///   · 只管「客户端本体」（版本号）的**检查 + 提示 + 指路**；**不**自动下载安装。
///   · 项目原有的三套更新机制（<see cref="ClientDataUpdater"/> 卡牌数据、
///     <see cref="RdDataUpdater"/> RD ypk、超先行包下载）**一律不动**，
///     本类也不碰它们的文件与目录。
///   · 增量包机制**本轮只预留扩展点**（见 <see cref="IClientUpdateSink"/>），不写实现。
///
/// 参照 mdpro3（_mdpro3/Setting.cs:1925-1977）：拉一个纯文本版本文件，首行 = 版本串，
/// 与本地版本比较，不等即提示更新。这里做三处工程化改造：
///   ① 版本源是**候选列表**（jsDelivr 优先 → GitHub 直连 → gh-proxy），逐个尝试，
///      任一成功即用 —— 解决大陆玩家直连 raw.githubusercontent.com 不稳的问题。
///   ② 只提示 + 提供「打开下载页」（夸克网盘），**不**在客户端内置大文件下载。
///   ③ 已见版本记进 Config ini（<c>lastSeenClientVersion</c>），避免每次启动都弹。
/// </summary>
public static class ClientSelfUpdate
{
    /// <summary>本产品**独立**的单调版本号（与上游协议版本 Config.ClientVersion 解耦，别混）。</summary>
    public const string ClientVersionText = "1.3";

    /// <summary>版本文件在各源里的相对路径（仓库根 main 分支）。内容：纯文本，首行 = 版本串。</summary>
    const string VersionFileRel = "version.txt";

    /// <summary>本地版本文件的落地位置（相对游戏根）。仅作下载缓存，不参与判版本。</summary>
    const string LocalVersionCache = "config/client_version.txt";

    // Config ini 键
    const string KeyVersionUrls = "clientUpdateVersionUrls";
    const string KeyPageUrl = "clientUpdatePageUrl";
    const string KeyLastSeen = "lastSeenClientVersion";

    /// <summary>内置默认版本源（按 2026-09-25 实测稳定性排序：jsDelivr 最稳，gh-proxy 兜底）。
    /// ⛔ 实测 raw.gitmirror.com / raw.kkgithub.com 已失效（code=000），不要加回来。</summary>
    static readonly string[] DefaultVersionUrls =
    {
        "https://fastly.jsdelivr.net/gh/OverClimber/MeiheweitePro@main/" + VersionFileRel,
        "https://raw.githubusercontent.com/OverClimber/MeiheweitePro/main/" + VersionFileRel,
        "https://gh-proxy.com/https://raw.githubusercontent.com/OverClimber/MeiheweitePro/main/" + VersionFileRel,
    };

    /// <summary>默认下载页（夸克网盘分享页）。用户 2026-09-25 定稿 —— 以后换网盘只改 Config ini。</summary>
    public const string DefaultUpdatePageUrl = "https://pan.quark.cn/s/d2dd58b3d27e";

    /// <summary>检查结果。</summary>
    public class CheckResult
    {
        /// <summary>检查是否成功（网络/解析都 OK）。false = 全部源都拿不到，静默即可。</summary>
        public bool Ok;
        /// <summary>远端版本串（Ok 时有效）。</summary>
        public string RemoteVersion;
        /// <summary>本地版本串。</summary>
        public string LocalVersion = ClientVersionText;
        /// <summary>是否有新版（远端 != 本地）。</summary>
        public bool HasUpdate;
        /// <summary>实际命中的源 URL（诊断/记忆用）。</summary>
        public string SourceUrl;
        /// <summary>全部失败时的原因（Ok=false 时有效）。</summary>
        public string Error;
    }

    /// <summary>
    /// **增量包预留扩展点**（用户口径：增量包本轮只预留接口）。
    ///
    /// 默认实现 <see cref="DefaultSink"/> 只做「提示 + 打开下载页」。
    /// 将来若做客户端本体增量包，只需新增一个实现类（走 clientupdate/ 目录 + 一个
    /// 应用器），把它赋给 <see cref="Sink"/> 即可 —— 本类主流程（检查/比版本/记忆）
    /// 一行不用改。设计要求见 devtools/rd_data_layout.md 末尾「客户端本体增量包设计要求」。
    /// </summary>
    public interface IClientUpdateSink
    {
        /// <summary>检查完成后的处理。HasUpdate=true 时由实现决定提示/跳转/（将来的）自动更新。</summary>
        void OnChecked(CheckResult result);
    }

    /// <summary>当前挂着的扩展点实现；null ⇒ 用默认（提示 + 打开下载页）。</summary>
    public static IClientUpdateSink Sink;

    /// <summary>默认实现：只在「有新版」时弹一次提示，提供打开下载页。</summary>
    sealed class DefaultSink : IClientUpdateSink
    {
        public void OnChecked(CheckResult r)
        {
            if (!r.Ok) return;
            if (!r.HasUpdate) return;
            NotifyUpdate(r.RemoteVersion);
        }
    }

    static IClientUpdateSink EffectiveSink()
    {
        return Sink ?? new DefaultSink();
    }

    // ------------------------------------------------------------------
    // 配置读取
    // ------------------------------------------------------------------

    /// <summary>候选版本源（Config ini 覆盖 → 否则内置默认）。</summary>
    static string[] VersionUrls()
    {
        string raw = null;
        try { raw = Config.Get(KeyVersionUrls, ""); } catch { }
        if (!string.IsNullOrEmpty(raw))
        {
            string[] parts = raw.Split(new[] { ',', ';', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
            List<string> list = new List<string>();
            foreach (string p in parts)
            {
                string t = p.Trim();
                if (t.Length > 0) list.Add(t);
            }
            if (list.Count > 0) return list.ToArray();
        }
        return DefaultVersionUrls;
    }

    /// <summary>下载页 URL（Config ini 覆盖 → 否则默认夸克网盘）。</summary>
    public static string UpdatePageUrl()
    {
        string v = null;
        try { v = Config.Get(KeyPageUrl, DefaultUpdatePageUrl); } catch { }
        if (string.IsNullOrEmpty(v)) v = DefaultUpdatePageUrl;
        return v;
    }

    /// <summary>是否已对本版本提示过（避免每次启动都弹）。</summary>
    public static bool AlreadySeen(string version)
    {
        try { return Config.Get(KeyLastSeen, "") == version; }
        catch { return false; }
    }

    /// <summary>记住已提示过的版本。</summary>
    public static void MarkSeen(string version)
    {
        try { Config.Set(KeyLastSeen, version); }
        catch (Exception e) { Debug.Log(e); }
    }

    // ------------------------------------------------------------------
    // 检查流程
    // ------------------------------------------------------------------

    /// <summary>
    /// 起一次异步检查（协程宿主用 Program.I()，与工程其它下载协程同口径）。
    /// <paramref name="silentWhenNoUpdate"/> = true 时「无新版/全部源失败」不弹任何东西（启动静默检查用）。
    /// </summary>
    public static void CheckAsync(bool silentWhenNoUpdate)
    {
        try
        {
            Program.I().StartCoroutine(CheckCoroutine(silentWhenNoUpdate));
        }
        catch (Exception e)
        {
            Debug.Log(e);
        }
    }

    static IEnumerator CheckCoroutine(bool silentWhenNoUpdate)
    {
        QuickTestTrace.Log("clientupdate", "check start local=" + ClientVersionText);
        string[] urls = VersionUrls();
        string lastErr = null;
        CheckResult hit = null;

        for (int i = 0; i < urls.Length; i++)
        {
            string url = urls[i];
            string outErr = null;
            string remote = null;
            // 复用工程下载底座（只调用，不改）：拉到临时缓存文件后读首行。
            yield return UnityFileDownloader.DownloadFileAsync(
                url,
                LocalVersionCache,
                delegate (bool ok) { },
                null,
                delegate (string path)
                {
                    // 校验回调：能解析出非空首行即算合法。
                    string first = FirstNonEmptyLine(path);
                    if (string.IsNullOrEmpty(first)) return false;
                    remote = Normalize(first);
                    return true;
                },
                delegate (string err) { outErr = err; });

            if (!string.IsNullOrEmpty(remote))
            {
                hit = new CheckResult
                {
                    Ok = true,
                    RemoteVersion = remote,
                    SourceUrl = url,
                    HasUpdate = !VersionsEqual(remote, ClientVersionText),
                };
                break;
            }
            lastErr = outErr;
            QuickTestTrace.Log("clientupdate", "source failed(" + i + ") " + url + " err=" + (outErr ?? "?"));
        }

        if (hit == null)
        {
            QuickTestTrace.Log("clientupdate", "check all-failed err=" + (lastErr ?? "?"));
            if (!silentWhenNoUpdate)
            {
                Program.PrintToChat("客户端更新检查：暂时连不上版本服务器，稍后再试。");
            }
            yield break;
        }

        QuickTestTrace.Log("clientupdate",
            "check ok remote=" + hit.RemoteVersion + " hasUpdate=" + (hit.HasUpdate ? 1 : 0)
            + " src=" + hit.SourceUrl);

        EffectiveSink().OnChecked(hit);

        if (!hit.HasUpdate && !silentWhenNoUpdate)
        {
            Program.PrintToChat("客户端已是最新版本（v" + ClientVersionText + "）。");
        }
    }

    static string FirstNonEmptyLine(string path)
    {
        try
        {
            string text = File.ReadAllText(path, Encoding.UTF8).Replace("\r", "");
            foreach (string l in text.Split('\n'))
            {
                string t = l.Trim();
                if (t.Length > 0) return t;
            }
        }
        catch (Exception e)
        {
            Debug.Log(e);
        }
        return null;
    }

    /// <summary>规整版本串：去空白、去前缀 v/V（比较口径：v1.3 == 1.3）。</summary>
    static string Normalize(string s)
    {
        if (s == null) return null;
        string t = s.Trim();
        if (t.Length > 0 && (t[0] == 'v' || t[0] == 'V')) t = t.Substring(1);
        return t.Trim();
    }

    static bool VersionsEqual(string a, string b)
    {
        return string.Equals(Normalize(a), Normalize(b), StringComparison.OrdinalIgnoreCase);
    }

    // ------------------------------------------------------------------
    // 提示 + 打开下载页
    // ------------------------------------------------------------------

    /// <summary>有新版时的提示（默认实现调这里；将来换 Sink 也可复用）。</summary>
    static void NotifyUpdate(string remoteVersion)
    {
        if (AlreadySeen(remoteVersion))
        {
            return; // 同一版本提示过一次就不再打扰
        }
        MarkSeen(remoteVersion);
        Program.PrintToChat("发现新版本 v" + Normalize(remoteVersion)
            + "（当前 v" + ClientVersionText + "）—— 主菜单「资源更新」里可查看并前往下载。");
    }

    /// <summary>用系统默认浏览器打开下载页（夸克网盘），并把浏览器窗口拉到前台。
    /// 复用「支持作者」那套 <see cref="Menu"/> 的 OpenURL + 前台协程（口径一致）。</summary>
    public static void OpenDownloadPage()
    {
        string url = UpdatePageUrl();
        QuickTestTrace.Log("clientupdate", "open page -> " + url);
        Application.OpenURL(url);
        try
        {
            Program p = Program.I();
            if (p != null && p.menu != null)
            {
                p.menu.BringDownloadPageToFront();
            }
        }
        catch (Exception e)
        {
            Debug.Log(e);
        }
    }
}
