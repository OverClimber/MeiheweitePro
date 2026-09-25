using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>
/// 在线数据更新：卡片库（cards.cdb）/ 禁限表（lflist.conf）/ 卡牌文本（strings.conf）三件套。
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
    static bool _recoveryFailed;
    static bool _running;
    static bool _reloadPending;

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
            _recoveryFailed = true;
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
            _recoveryFailed = false;
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
            _recoveryFailed = false;
            return true;
        }

        if (!File.Exists(TransactionReadyMarker))
        {
            // 没有 ready 标记 = 上次已经提交或回滚过，剩下的只是清理残留。
            _transactionActive = false;
            _recoveryFailed = false;
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
            _recoveryFailed = false;
            return true;
        }

        try
        {
            Target[] targets = CreateTargets();
            string[] manifest = File.ReadAllLines(TransactionManifest);
            if (manifest.Length != targets.Length)
            {
                _recoveryFailed = true;
                return false;
            }

            // 先整体校验备份齐全，再动手恢复 —— 避免恢复到一半才发现备份缺了。
            for (int i = 0; i < targets.Length; i++)
            {
                string[] flags = manifest[i].Split('|');
                if (flags.Length != 2)
                {
                    _recoveryFailed = true;
                    return false;
                }
                if ((flags[0] != "0" && flags[0] != "1") || (flags[1] != "0" && flags[1] != "1"))
                {
                    _recoveryFailed = true;
                    return false;
                }
                if (flags[0] == "1" && !File.Exists(Path.Combine(TransactionRoot, i + ".data")))
                {
                    _recoveryFailed = true;
                    return false;
                }
                if (flags[1] == "1" && !File.Exists(Path.Combine(TransactionRoot, i + ".etag")))
                {
                    _recoveryFailed = true;
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
            _recoveryFailed = false;
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
            _recoveryFailed = true;
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
    /// force = true 时跳过本地 ETag 比对、无条件重下。必要性在于：ETag 记录的是「上次下到的版本」，
    /// 它看不出本地文件内容有没有被换掉（例如被外部工具覆盖成旧版），所以「检查更新」修不了坏数据。
    /// userInitiated = true 时无论结果都给出明确反馈 —— 手动点的，不能静默结束。
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
        Target[] targets = CreateTargets();
        bool completed = false;
        try
        {
            if (!RecoverTransaction())
            {
                _recoveryFailed = true;
                LastError = "上次更新未安全恢复";
                State = UpdateState.Failed;
                PrintToChat("上次数据更新未能安全恢复，请重启游戏后重试。");
                completed = true;
                yield break;
            }

            SetState(UpdateState.Checking, "", 0f);

            if (userInitiated)
            {
                PrintToChat(force
                    ? "开始强制重新下载卡牌数据（卡片库 / 禁限表 / 卡牌文本）..."
                    : "开始检查卡牌数据更新（卡片库 / 禁限表 / 卡牌文本）...");
            }

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
                            SetState(UpdateState.Downloading, target.name, (float)index / targets.Length);
                            PrintToChat("检测到【" + target.name + "】有更新，开始下载...");
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
                if (userInitiated)
                {
                    PrintToChat("卡牌数据已是最新，无需更新。");
                }
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
            _running = false;
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

    static void PrintToChat(string text)
    {
        Program.PrintToChat(text);
    }
}
