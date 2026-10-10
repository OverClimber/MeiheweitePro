using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Ionic.Zip;
using Mono.Data.Sqlite;
using UnityEngine;

/// <summary>
/// 在线数据更新：卡片库（cards.cdb）/ 禁限表（lflist.conf）/ 卡牌文本（strings.conf）三件套
/// + OCG 卡图补缺（pics/&lt;卡码&gt;.jpg，2026-09-26 起）。
///
/// 思路与 KoishiPro2(iOS) 的 UpdateClientCoroutine 一致，落地路径按本工程口径：
///   · UnityFileDownloader.DownloadFileWithHeadCheck 负责：HEAD 取 ETag → 与本地 "&lt;文件&gt;.etag"
///     比对 → 只有真的变了才下到 .tmp → 交给真解析器校验 → 原子替换 + 写回 .etag。
///     萌卡 CDN 的 ETag 实测就是文件 MD5，所以这一层等于「按内容判增量」。
///   · 替换前把旧文件快照进 updates/basic-data-transaction-v1/：ready.txt 存在 = 事务进行中，
///     下次启动发现即整体回滚（断电 / 崩溃自愈）。成功才删掉 ready 标记提交。
///   · 状态机 + 主菜单 version_ 标签实时显示，启动即跑，不需要点按钮。
///
/// 本模块是本工程唯一的数据更新入口。旧的「官方增量包 + 蓝奏云网盘」链路已整体移除。
/// 目标表 CreateTargets() 是唯一需要维护的地方：将来要加新的数据文件，加一行即可。
/// </summary>
public static class ClientDataUpdater
{
    public enum UpdateState
    {
        Idle,
        Checking,
        Downloading,
        Applying,
        Ready,
        Failed
    }

    /// <summary>一个可在线更新的数据文件。</summary>
    public class Target
    {
        public string name;              // 展示名，如「卡片库」
        public string url;               // 远端地址
        public string localPath;         // 相对游戏根目录的落地路径
        public Func<string, bool> validate; // 真解析器校验（不写入内存）

        // 单次流程的运行时状态
        public bool updated;             // 远端确实有变化、已开始下载
        public bool succeeded;           // 最终成功（含「本来最新」）
        public string failure;           // 失败原因

        public Target(string name, string url, string localPath, Func<string, bool> validate)
        {
            this.name = name;
            this.url = url;
            this.localPath = localPath;
            this.validate = validate;
        }
    }

    /// <summary>远端数据源。萌卡 CDN，与 KoishiPro2 客户端默认下载源相同。</summary>
    const string ContentRoot = "https://cdntx2.moecube.com/koishipro/content/";

    const string TransactionDirectory = "updates/basic-data-transaction-v1";
    const string TransactionManifest = TransactionDirectory + "/manifest.txt";
    const string TransactionReadyMarker = TransactionDirectory + "/ready.txt";

    /// <summary>测试沙箱：非空时所有落地路径都在这个目录下（不碰真实游戏数据）。</summary>
    public static string RootOverride;

    public static UpdateState State { get; private set; } = UpdateState.Idle;
    public static string CurrentFile { get; private set; } = "";
    public static float Progress { get; private set; }
    public static string LastError { get; private set; } = "";
    /// <summary>本进程内是否发生过真实更新（用于「更新后需要重载」判断）。</summary>
    public static bool AnyFileUpdated { get; private set; }

    static bool _transactionActive;
    static bool _running;
    static bool _reloadPending;

    // ── 单行汇总（2026-09-26 用户口径：一次「检查并更新」跑完只在聊天栏留一行，
    //    不再让「检测到…开始下载 / 补图完成 / 另有…」三四条进度语刷屏）。
    //    自动跑且什么都没变时完全静默（启动不该刷屏）。
    static readonly List<string> SummaryDataNames = new List<string>();
    static int SummaryPicsTotal;      // 真有图的张数（cdb 卡码里被覆盖集命中的个数 + 本次补到张数）
    static int SummaryPicsAdded;      // 真补到的张数
    static int SummaryPicsAbsent;     // CDN 确认暂无图、记入负缓存的张数
    static bool SummaryPicsRan;       // 卡图阶段真的扫过一遍
    static bool SummaryPicsNoCdb;     // 读不到卡片库 → 卡图阶段整体跳过
    static bool SummaryPicsFailed;    // 一张都没成 = 判定网络不通
    static bool SummaryDataFailed;    // 数据三件套阶段失败（状态已被 PicsPhase 冲掉，这里记着）

    static string Root
    {
        get
        {
            if (!string.IsNullOrEmpty(RootOverride))
            {
                return Path.GetFullPath(RootOverride);
            }
            return Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        }
    }

    static string LocalPath(string relative)
    {
        // 一律钉在游戏根（= dataPath 的上级），别返回裸相对路径 —— 相对路径跟着**进程 CWD**走，
        // 任何一处把 CWD 改掉（例如原生文件对话框浏览过目录），下载就会落进奇怪的地方
        //（2026-09-20 实测：config/ 被写进了 rd/update/config/）。
        return Path.Combine(Root, relative.Replace('/', Path.DirectorySeparatorChar));
    }

    static string TransactionRoot
    {
        get { return Path.Combine(Root, TransactionDirectory.Replace('/', Path.DirectorySeparatorChar)); }
    }

    /// <summary>
    /// 目标表：本工程实际加载的三件套位置。
    /// 卡片库走 cdb/，禁限表与文本走 config/（与 Program 启动时的加载顺序一致）。
    /// </summary>
    public static Target[] CreateTargets()
    {
        return new Target[]
        {
            new Target(
                "卡片库",
                ContentRoot + "cards.cdb",
                "cdb/cards.cdb",
                YGOSharp.CardsManager.Validate),
            new Target(
                "禁限表",
                ContentRoot + "lflist.conf",
                "config/lflist.conf",
                YGOSharp.BanlistManager.Validate),
            new Target(
                "卡牌文本",
                ContentRoot + "strings.conf",
                "config/strings.conf",
                GameStringManager.Validate),
        };
    }

    public static bool IsBusy
    {
        get { return _running; }
    }

    /// <summary>主菜单 version_ 标签用的状态文案；无进展时返回 null（保持原版本号文本）。</summary>
    public static string StatusText()
    {
        switch (State)
        {
            case UpdateState.Checking:
                return "正在检查数据更新...";
            case UpdateState.Downloading:
                return "正在更新" + (string.IsNullOrEmpty(CurrentFile) ? "数据" : "【" + CurrentFile + "】")
                    + " " + Mathf.RoundToInt(Progress * 100f) + "%";
            case UpdateState.Applying:
                return "正在应用数据更新...";
            case UpdateState.Failed:
                return "数据更新失败：" + LastError;
            default:
                return null;
        }
    }

    static void SetState(UpdateState state, string currentFile, float progress)
    {
        State = state;
        CurrentFile = currentFile;
        Progress = Mathf.Clamp01(progress);
    }

    // ── 事务 ────────────────────────────────────────────────────────────────

    static void BeginTransaction(Target[] targets)
    {
        if (_transactionActive)
        {
            return;
        }
        if (!RecoverTransaction())
        {
            throw new IOException("无法恢复上一次未完成的数据更新。");
        }

        try
        {
            Directory.CreateDirectory(TransactionRoot);
            string[] manifest = new string[targets.Length];
            for (int i = 0; i < targets.Length; i++)
            {
                string path = LocalPath(targets[i].localPath);
                string dataBackup = Path.Combine(TransactionRoot, i + ".data");
                string etagBackup = Path.Combine(TransactionRoot, i + ".etag");
                bool dataExists = File.Exists(path);
                bool etagExists = File.Exists(path + ".etag");

                if (dataExists)
                {
                    File.Copy(path, dataBackup, true);
                }
                if (etagExists)
                {
                    File.Copy(path + ".etag", etagBackup, true);
                }
                manifest[i] = (dataExists ? "1" : "0") + "|" + (etagExists ? "1" : "0");
            }

            File.WriteAllLines(TransactionManifest, manifest);
            File.WriteAllText(TransactionReadyMarker, DateTime.UtcNow.ToString("O"));
            _transactionActive = true;
        }
        catch
        {
            try
            {
                if (Directory.Exists(TransactionRoot) && !File.Exists(TransactionReadyMarker))
                {
                    Directory.Delete(TransactionRoot, true);
                }
            }
            catch (Exception e)
            {
                Program.DEBUGLOG(e);
            }
            throw;
        }
    }

    /// <summary>启动时调用：上次没走到提交（ready 标记还在）就整体回滚。</summary>
    public static bool RecoverTransaction()
    {
        if (!Directory.Exists(TransactionRoot))
        {
            _transactionActive = false;
            return true;
        }

        if (!File.Exists(TransactionReadyMarker))
        {
            // 没有 ready 标记 = 上次已经提交或回滚过，剩下的只是清理残留。
            _transactionActive = false;
            try
            {
                Directory.Delete(TransactionRoot, true);
            }
            catch (Exception e)
            {
                Program.DEBUGLOG(e);
            }
            return true;
        }

        _transactionActive = true;
        return RollbackTransaction();
    }

    static bool RollbackTransaction()
    {
        if (!Directory.Exists(TransactionRoot))
        {
            _transactionActive = false;
            return true;
        }

        try
        {
            Target[] targets = CreateTargets();
            string[] manifest = File.ReadAllLines(TransactionManifest);
            if (manifest.Length != targets.Length)
            {
                return false;
            }

            // 先整体校验备份齐全，再动手恢复 —— 避免恢复到一半才发现备份缺了。
            for (int i = 0; i < targets.Length; i++)
            {
                string[] flags = manifest[i].Split('|');
                if (flags.Length != 2)
                {
                    return false;
                }
                if ((flags[0] != "0" && flags[0] != "1") || (flags[1] != "0" && flags[1] != "1"))
                {
                    return false;
                }
                if (flags[0] == "1" && !File.Exists(Path.Combine(TransactionRoot, i + ".data")))
                {
                    return false;
                }
                if (flags[1] == "1" && !File.Exists(Path.Combine(TransactionRoot, i + ".etag")))
                {
                    return false;
                }
            }

            for (int i = 0; i < targets.Length; i++)
            {
                string[] flags = manifest[i].Split('|');
                string path = LocalPath(targets[i].localPath);
                string etagPath = path + ".etag";
                string dataBackup = Path.Combine(TransactionRoot, i + ".data");
                string etagBackup = Path.Combine(TransactionRoot, i + ".etag");

                string dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                if (flags[0] == "1")
                {
                    File.Copy(dataBackup, path, true);
                }
                else if (File.Exists(path))
                {
                    File.Delete(path);
                }

                if (flags[1] == "1")
                {
                    File.Copy(etagBackup, etagPath, true);
                }
                else if (File.Exists(etagPath))
                {
                    File.Delete(etagPath);
                }
            }

            // 删掉 ready 标记 = 回滚完成点；剩下的目录清理失败也不影响正确性。
            File.Delete(TransactionReadyMarker);
            _transactionActive = false;
            try
            {
                Directory.Delete(TransactionRoot, true);
            }
            catch (Exception e)
            {
                Program.DEBUGLOG(e);
            }
            return true;
        }
        catch (Exception e)
        {
            Program.DEBUGLOG(e);
            return false;
        }
    }

    static bool CommitTransaction()
    {
        if (!_transactionActive || !Directory.Exists(TransactionRoot)
            || !File.Exists(TransactionReadyMarker))
        {
            return false;
        }

        try
        {
            // 删掉 ready 标记 = 提交点；之后残留快照只是清理问题。
            File.Delete(TransactionReadyMarker);
            _transactionActive = false;
            try
            {
                Directory.Delete(TransactionRoot, true);
            }
            catch (Exception e)
            {
                Program.DEBUGLOG(e);
            }
            return true;
        }
        catch (Exception e)
        {
            Program.DEBUGLOG(e);
            return false;
        }
    }

    // ── 主流程 ──────────────────────────────────────────────────────────────

    /// <summary>
    /// 启动即调用；也可由「资源下载」菜单手动触发（同一时刻只会有一份在跑）。
    ///
    /// 两阶段：数据三件套（<see cref="DataPhase"/>）→ OCG 卡图补缺（<see cref="PicsPhase"/>，
    /// 2026-09-26 用户口径：资源下载要把卡图更新也管起来，否则新卡（如盈彩月夜）只有数据没有图）。
    /// 卡图阶段失败不判整体失败 —— 图是加法资产，缺了下次启动再补。
    /// </summary>
    public static IEnumerator UpdateCoroutine(bool force = false, bool userInitiated = false)
    {
        if (_running)
        {
            if (userInitiated)
            {
                PrintToChat("数据更新正在进行中，请稍候。");
            }
            yield break;
        }
        _running = true;
        try
        {
            ResetSummary();
            yield return Program.I().StartCoroutine(DataPhase(force, userInitiated));
            // 数据阶段失败也照样查卡图：两者是各自独立的缺件（cdb 坏了不代表图也坏），
            // 而且抓不到卡表时用户最想要的恰恰是先把图补上。
            // 唯一副作用：PicsPhase 收尾会把 State 打成 Ready，会冲掉右上角
            //「数据更新失败：…」的提示 ⇒ 跑完把失败状态还原回去。
            bool dataFailed = State == UpdateState.Failed;
            string dataError = LastError;
            yield return Program.I().StartCoroutine(PicsPhase(force, userInitiated));
            if (dataFailed)
            {
                State = UpdateState.Failed;
                CurrentFile = dataError;
            }
            SummaryDataFailed = dataFailed;
            PrintSummary(userInitiated);
        }
        finally
        {
            _running = false;
        }
    }

    /// <summary>数据三件套阶段（原 UpdateCoroutine 主体；运行闸在包装器上）。</summary>
    static IEnumerator DataPhase(bool force, bool userInitiated)
    {
        Target[] targets = CreateTargets();
        bool completed = false;
        try
        {
            if (!RecoverTransaction())
            {
                LastError = "上次更新未安全恢复";
                State = UpdateState.Failed;
                PrintToChat("上次数据更新未能安全恢复，请重启游戏后重试。");
                completed = true;
                yield break;
            }

            SetState(UpdateState.Checking, "", 0f);

            for (int i = 0; i < targets.Length; i++)
            {
                Target target = targets[i];
                int index = i;

                string dir = Path.GetDirectoryName(LocalPath(target.localPath));
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                SetState(UpdateState.Checking, target.name, (float)index / targets.Length);

                yield return Program.I().StartCoroutine(
                    UnityFileDownloader.DownloadFileWithHeadCheck(
                        target.url,
                        LocalPath(target.localPath),
                        delegate (bool success) { target.succeeded = success; },
                        delegate (float progress)
                        {
                            SetState(
                                UpdateState.Downloading,
                                target.name,
                                (index + progress) / targets.Length);
                        },
                        delegate
                        {
                            // 确认有更新、即将落盘 —— 此刻才开事务（未更新的文件不该被快照）。
                            BeginTransaction(targets);
                            target.updated = true;
                            AnyFileUpdated = true;
                            SummaryDataNames.Add(target.name);
                            SetState(UpdateState.Downloading, target.name, (float)index / targets.Length);
                        },
                        target.validate,
                        delegate (string reason) { target.failure = reason; },
                        forceDownload: force
                    ));
            }

            // 收集失败项
            List<string> failures = new List<string>();
            for (int i = 0; i < targets.Length; i++)
            {
                Target target = targets[i];
                if (target.succeeded)
                {
                    continue;
                }
                if (string.IsNullOrEmpty(target.failure))
                {
                    failures.Add(target.name + (target.updated ? "下载或校验失败" : "检查失败"));
                }
                else
                {
                    failures.Add(target.name + "：" + target.failure);
                }
            }

            if (failures.Count > 0)
            {
                LastError = string.Join("、", failures.ToArray());
                if (!RollbackTransaction())
                {
                    LastError += "（恢复旧数据失败）";
                }
                State = UpdateState.Failed;
                CurrentFile = LastError;
                PrintToChat("数据更新未完成：" + LastError + "。请稍后重试。");
                completed = true;
                yield break;
            }

            bool anyUpdated = false;
            for (int i = 0; i < targets.Length; i++)
            {
                anyUpdated = anyUpdated || targets[i].updated;
            }

            if (!anyUpdated)
            {
                State = UpdateState.Ready;
                CurrentFile = "";
                Progress = 1f;
                completed = true;
                yield break;
            }

            SetState(UpdateState.Applying, "应用更新", 1f);

            // 数据已经在盘上了，但内存里还是旧的。这里只挂起，真正的重载 + 事务提交交给
            // 帧循环（Menu.preFrameFunction / Program.Update）—— 本协程此刻 _running 还是
            // true，自己调 ApplyPendingReload 会被那道闸挡住，事务就再没有触发点了。
            _reloadPending = true;
            PrintToChat("数据更新完毕，正在自动生效（无需重启）...");
            completed = true;
        }
        finally
        {
            if (!completed)
            {
                bool rolledBack = RollbackTransaction();
                State = UpdateState.Failed;
                LastError = "更新流程异常中断";
                CurrentFile = LastError;
                PrintToChat(rolledBack
                    ? "数据更新异常中断，现有数据未改动，请重试。"
                    : "数据更新异常中断，且旧数据恢复失败，请重启游戏后重试。");
            }
        }
    }

    // ── 卡图阶段 ────────────────────────────────────────────────────────────

    /// <summary>负缓存文件：CDN 上确认没有图的卡码（一行一个）。放 picture/card/ 里，
    /// GameTextureManager 只按精确路径取图，不会列举目录，点开头文件对它不可见。</summary>
    const string AbsentPicsFile = "picture/card/.picsync-absent.txt";

    /// <summary>负缓存有效期（天）：CDN 后来补上的新图不能被一次「暂无」永久遮蔽。
    /// 超期即视为空集重探，重探后 SaveAbsentPics 会刷新文件 mtime。</summary>
    const int AbsentCacheTtlDays = 30;

    /// <summary>卡图连续失败到此数且一张未成功 ⇒ 判定网络不通，提前退出（不空打上千张 HEAD）。</summary>
    const int PicsNetworkGiveUpStreak = 5;

    /// <summary>测试开关文件名：存在（且 qt_debug.on 开）时其内容替换卡图下载根 ——
    /// 本地无外网环境用本地 HTTP 服务器验证整条卡图链路。正式环境永远读不到它。</summary>
    const string PicsTestRootSwitch = "qt_picsroot.txt";

    /// <summary>测试根是否激活（qt_debug.on 开 + qt_picsroot.txt 有内容）。</summary>
    static bool PicsTestMode()
    {
        if (!QuickTestTrace.Enabled)
        {
            return false;
        }
        try
        {
            string p = QuickTestTrace.LogPath(PicsTestRootSwitch);
            return File.Exists(p) && File.ReadAllText(p).Trim().Length > 0;
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>卡图下载根（正式 = ContentRoot；测试 = qt_picsroot.txt 内容）。</summary>
    static string PicsRoot()
    {
        if (!QuickTestTrace.Enabled)
        {
            return ContentRoot;
        }
        try
        {
            string p = QuickTestTrace.LogPath(PicsTestRootSwitch);
            if (File.Exists(p))
            {
                string t = File.ReadAllText(p).Trim();
                if (t.Length > 0)
                {
                    return t;
                }
            }
        }
        catch (Exception)
        {
        }
        return ContentRoot;
    }

    /// <summary>
    /// OCG 卡图补缺（KoishiPro2 同款源：ContentRoot + pics/&lt;卡码&gt;.jpg，2026-09-26 实测
    /// 盈彩月夜 9362643 在此可达）。流程：
    ///   ① 卡码集合 = 盘上 cdb/cards.cdb 的 datas 表（直接读文件 —— 不依赖内存重载时序，
    ///     本轮刚更新的新卡也在内）；
    ///   ② 覆盖集 = picture/card/ + picture/cardIn8thEdition/ + zip 内 pics/（GameTextureManager
    ///     的查找顺序，见 GameTextureManager.getPicture）；
    ///   ③ 缺的逐张 DownloadFileWithHeadCheck（HEAD+ETag 判重，落 picture/card/&lt;码&gt;.jpg）；
    ///   ④ CDN 上确认没有的记负缓存，之后启动不再探测；卡片库刚更新过 / 强制更新时清缓存重探
    ///     （CDN 可能补了新图）。
    /// 单张失败不判整体失败（图是加法资产）。全失败视为网络问题、不写负缓存。
    /// </summary>
    static IEnumerator PicsPhase(bool force, bool userInitiated)
    {
        string root = PicsRoot();
        string cdbPath = LocalPath("cdb/cards.cdb");
        List<string> ids = QueryCardIds(cdbPath);
        if (ids == null || ids.Count == 0)
        {
            QuickTestTrace.Log("pics", "query FAILED path=" + cdbPath + "（读不到卡码，跳过）");
            SummaryPicsNoCdb = true;
            yield break;
        }

        HashSet<string> covered = CollectCoveredPicIds();
        HashSet<string> absent = (AnyFileUpdated || force) ? new HashSet<string>() : LoadAbsentPics();
        List<string> missing = new List<string>();
        for (int i = 0; i < ids.Count; i++)
        {
            if (!covered.Contains(ids[i]) && !absent.Contains(ids[i]))
            {
                missing.Add(ids[i]);
            }
        }
        // 「真有图的张数」= cdb 卡码里被覆盖集命中的个数（用户口径 2026-09-26）。
        // ⛔ 不能用 ids.Count（卡码总数）：负缓存里的卡**本地其实没图**，拿总数去报
        //    「N 张卡都有图」会把数字报大（实测差几张，用户在对局外的汇总行里能看出来）。
        int haveCount = 0;
        for (int i = 0; i < ids.Count; i++)
        {
            if (covered.Contains(ids[i]))
            {
                haveCount++;
            }
        }
        QuickTestTrace.Log("pics", "scan ids=" + ids.Count + " covered=" + covered.Count
            + " have=" + haveCount + " absentCache=" + absent.Count + " missing=" + missing.Count
            + " root=" + root + " anyDataUpdated=" + AnyFileUpdated + " force=" + force);

        if (missing.Count == 0)
        {
            SetState(UpdateState.Ready, "", 1f);
            SummaryPicsRan = true;
            SummaryPicsTotal = haveCount;
            yield break;
        }

        int okCount = 0;
        int consecutiveFail = 0;
        List<string> newlyAbsent = new List<string>();
        for (int i = 0; i < missing.Count; i++)
        {
            string code = missing[i];
            SetState(UpdateState.Downloading, "卡图", (float)i / missing.Count);
            bool success = false;
            yield return Program.I().StartCoroutine(UnityFileDownloader.DownloadFileWithHeadCheck(
                root + "pics/" + code + ".jpg",
                LocalPath("picture/card/" + code + ".jpg"),
                delegate (bool s) { success = s; },
                null,
                null,
                null,
                null,
                forceDownload: true));
            if (success)
            {
                okCount++;
                consecutiveFail = 0;
            }
            else
            {
                newlyAbsent.Add(code);
                consecutiveFail++;
                // 一张都没成、还连着失败 N 张 = 判定网络不通。继续对着上千张卡空打 HEAD
                // 只会白等（新装机 + 外网挂的典型场景），提前收工、且不写负缓存。
                if (okCount == 0 && consecutiveFail >= PicsNetworkGiveUpStreak)
                {
                    QuickTestTrace.Log("pics", "giveup streak=" + consecutiveFail
                        + " ok=0（判定网络不通，提前退出，不写负缓存）");
                    break;
                }
            }
        }

        SetState(UpdateState.Ready, "", 1f);
        SummaryPicsRan = true;
        SummaryPicsTotal = haveCount + okCount;   // 扫完 + 补完之后的「真有图张数」
        SummaryPicsAdded = okCount;
        SummaryPicsAbsent = newlyAbsent.Count;
        SummaryPicsFailed = (okCount == 0 && newlyAbsent.Count > 0);
        QuickTestTrace.Log("pics", "done ok=" + okCount + " absentNew=" + newlyAbsent.Count
            + (newlyAbsent.Count > 0 ? " absentCodes=" + string.Join(",", newlyAbsent.GetRange(0, Math.Min(5, newlyAbsent.Count)).ToArray())
                + (newlyAbsent.Count > 5 ? "…" : "") : ""));
        // 全失败 = 大概率网络不通，负缓存会把「其实有图只是没网」的卡错记成没有 ⇒ 不写。
        if (newlyAbsent.Count > 0 && okCount > 0)
        {
            SaveAbsentPics(newlyAbsent);
        }
    }

    /// <summary>读盘上 cdb 的全部卡码（datas.id，字符串化）。失败返回 null。</summary>
    static List<string> QueryCardIds(string cdbPath)
    {
        if (!File.Exists(cdbPath))
        {
            return null;
        }
        List<string> ids = new List<string>();
        try
        {
            using (SqliteConnection con = new SqliteConnection("Data Source=" + cdbPath))
            {
                con.Open();
                using (SqliteCommand cmd = con.CreateCommand())
                {
                    cmd.CommandText = "SELECT id FROM datas";
                    using (SqliteDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            ids.Add(Convert.ToInt64(reader.GetValue(0)).ToString());
                        }
                    }
                }
            }
        }
        catch (Exception e)
        {
            Program.DEBUGLOG(e);
            return null;
        }
        return ids;
    }

    /// <summary>
    /// 已有卡图的覆盖集：picture/card/ 与 picture/cardIn8thEdition/ 的数字名文件
    /// + 各 zip 内 pics/&lt;码&gt;.(jpg|png)（与 GameTextureManager.getPicture 的查找顺序对齐，
    /// zip 里有的不算缺）。
    /// </summary>
    static HashSet<string> CollectCoveredPicIds()
    {
        HashSet<string> set = new HashSet<string>();
        CollectDir(LocalPath("picture/card"), set);
        CollectDir(LocalPath("picture/cardIn8thEdition"), set);
        try
        {
            foreach (ZipFile zip in GameZipManager.Zips)
            {
                if (zip == null)
                {
                    continue;
                }
                foreach (string entry in zip.EntryFileNames)
                {
                    if (entry == null)
                    {
                        continue;
                    }
                    string lower = entry.ToLowerInvariant();
                    if (!lower.StartsWith("pics/"))
                    {
                        continue;
                    }
                    string stem = Path.GetFileNameWithoutExtension(lower);
                    string ext = Path.GetExtension(lower);
                    long v;
                    if ((ext == ".jpg" || ext == ".png") && long.TryParse(stem, out v))
                    {
                        set.Add(stem);
                    }
                }
            }
        }
        catch (Exception e)
        {
            Program.DEBUGLOG(e);
        }
        return set;
    }

    static void CollectDir(string dir, HashSet<string> set)
    {
        try
        {
            if (!Directory.Exists(dir))
            {
                return;
            }
            foreach (string f in Directory.GetFiles(dir))
            {
                string stem = Path.GetFileNameWithoutExtension(f);
                long v;
                if (stem.Length > 0 && long.TryParse(stem, out v))
                {
                    set.Add(stem);
                }
            }
        }
        catch (Exception e)
        {
            Program.DEBUGLOG(e);
        }
    }

    static HashSet<string> LoadAbsentPics()
    {
        HashSet<string> set = new HashSet<string>();
        try
        {
            string path = LocalPath(AbsentPicsFile);
            if (File.Exists(path))
            {
                // TTL：超期就把「暂无图」的结论作废、整份重探（CDN 可能已经补上了图）。
                if ((DateTime.UtcNow - File.GetLastWriteTimeUtc(path)).TotalDays > AbsentCacheTtlDays)
                {
                    QuickTestTrace.Log("pics", "absentCache expired（超 " + AbsentCacheTtlDays + " 天，忽略重探）");
                    return set;
                }
                string[] lines = File.ReadAllLines(path);
                for (int i = 0; i < lines.Length; i++)
                {
                    string s = lines[i].Trim();
                    if (s.Length > 0)
                    {
                        set.Add(s);
                    }
                }
            }
        }
        catch (Exception e)
        {
            Program.DEBUGLOG(e);
        }
        return set;
    }

    static void SaveAbsentPics(List<string> newlyAbsent)
    {
        try
        {
            HashSet<string> merged = LoadAbsentPics();
            for (int i = 0; i < newlyAbsent.Count; i++)
            {
                merged.Add(newlyAbsent[i]);
            }
            List<string> all = new List<string>(merged);
            all.Sort();
            string path = LocalPath(AbsentPicsFile);
            string dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
            }
            File.WriteAllLines(path, all.ToArray());
        }
        catch (Exception e)
        {
            Program.DEBUGLOG(e);
        }
    }

    /// <summary>数据已落盘，重载内存里的三件套并提交事务。菜单每帧调用来兑现挂起的重载。</summary>
    public static bool ApplyPendingReload()
    {
        // 更新协程还在跑时绝不能重载：此刻事务正开着，就地提交会把「下载到一半」
        // 的状态当成结果定死。菜单是每帧调本方法的，所以这道闸必须有。
        if (_running)
        {
            return false;
        }
        if (!_reloadPending && !_transactionActive)
        {
            return false;
        }
        // 只允许在主菜单重载：对局中 Reset 掉 CardsManager 会让正在进行的对局读到空卡。
        // 不方便重载就先放着 —— 帧循环下一帧还会来问，事务也就一直保持未提交（可回滚）。
        if (Program.I().menu == null || !Program.I().menu.isShowed)
        {
            return false;
        }

        bool ok = Program.I().ReloadGameDatabases();
        if (!ok)
        {
            // 内存里还是旧数据 —— 事务不能提交，回滚磁盘让它与内存一致。
            RollbackTransaction();
            State = UpdateState.Failed;
            LastError = "应用更新失败";
            CurrentFile = LastError;
            _reloadPending = false;
            return false;
        }

        _reloadPending = false;
        if (_transactionActive && !CommitTransaction())
        {
            // 重载已经成功、数据是新的；只是事务没提交掉，下次启动会自动回滚。
            PrintToChat("数据已生效，但更新事务提交失败，下次启动会回滚到本次更新前的数据。");
            State = UpdateState.Failed;
            LastError = "提交更新失败";
            CurrentFile = LastError;
            return false;
        }

        State = UpdateState.Ready;
        CurrentFile = "";
        return true;
    }

    /// <summary>
    /// 是否有「数据已落盘、但内存还没重载」的活儿等帧循环来兑现。
    /// 注意把「事务还开着」也算进来：更新成功但没能立刻重载时，事务要靠这个标志才会被提交。
    /// </summary>
    public static bool ReloadPending
    {
        get { return (_reloadPending || _transactionActive) && !_running; }
    }

    static void ResetSummary()
    {
        SummaryDataNames.Clear();
        SummaryPicsTotal = 0;
        SummaryPicsAdded = 0;
        SummaryPicsAbsent = 0;
        SummaryPicsRan = false;
        SummaryPicsNoCdb = false;
        SummaryPicsFailed = false;
        SummaryDataFailed = false;
    }

    /// <summary>
    /// 一轮「检查并更新」跑完的单行汇总（2026-09-26 用户口径：一次跑完只留一行）。
    /// 自动跑且一切照旧 → 完全静默（启动不该刷屏）；手动跑至少给一行回执
    ///（否则点了菜单像没反应 —— 这正是「看不到卡图更新」的一半原因）。
    /// </summary>
    static void PrintSummary(bool userInitiated)
    {
        bool dataChanged = SummaryDataNames.Count > 0;
        bool anything = SummaryDataFailed || dataChanged || SummaryPicsAdded > 0
            || SummaryPicsFailed || SummaryPicsNoCdb;
        if (!anything && !userInitiated)
        {
            return;
        }

        List<string> parts = new List<string>();
        if (SummaryDataFailed)
        {
            parts.Add("数据更新失败（" + LastError + "）");
        }
        else if (dataChanged)
        {
            parts.Add("数据已更新[" + string.Join("、", SummaryDataNames.ToArray()) + "]");
        }
        else
        {
            parts.Add("数据已是最新");
        }

        if (SummaryPicsNoCdb)
        {
            parts.Add("卡图检查跳过（读不到卡片库）");
        }
        else if (SummaryPicsFailed)
        {
            parts.Add("卡图更新失败（网络问题？，下次启动重试）");
        }
        else if (SummaryPicsRan)
        {
            if (SummaryPicsAdded > 0)
            {
                string s = "卡图补 " + SummaryPicsAdded + " 张";
                if (SummaryPicsAbsent > 0)
                {
                    s += "，另有 " + SummaryPicsAbsent + " 张 CDN 暂无图已跳过";
                }
                parts.Add(s);
            }
            else
            {
                parts.Add("卡图已是最新（" + SummaryPicsTotal + " 张卡都有图）");
            }
        }

        PrintToChat("资源更新：" + string.Join("，", parts.ToArray()) + "。");
    }

    static void PrintToChat(string text)
    {
        Program.PrintToChat(text);
    }
}
