using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Ionic.Zip;
using Mono.Data.Sqlite;

/// <summary>
/// RD（超速决斗）数据的 **ypk 更新通道** —— 预留给"不随版本发布、单独分发 RD 数据增量"的场景。
///
/// 用法（2026-09-20 扩）：
///   把 <c>*.ypk</c> 放进 <c>rd/update/</c>：下次启动自动生效；主菜单「资源更新」点击即热更新；
///   主菜单还会**轮询**这个目录（见 Menu.PollRdUpdatePacks），拖包进去会弹「现在应用吗」确认。
///   包就是普通 zip，接受两种条目路径口径：
///
///   ① **rd/ 相对路径**（白名单与 rd_data_layout.md 的布局一致）：
///     · cdb/…                → 客户端 RD 池卡表（CardsManager.initializeRD 那条通道）
///     · picture/…            → 卡图 / 立绘（GameModeManager.PictureRoot = rd/picture）
///     · ai/…                 → 人机整包（ai/cdb、ai/script、ai/config…，AI.Server 相对 cwd 读）
///     · lflist.conf          → RD 禁限表
///     · bot.conf             → 机器人名单
///
///   ② **RD 先行卡包的原生布局**（2026-09-20 起兼容，玩家拖进来的就是这种）：
///     · RD Patch.cdb（根部） → 覆盖 rd/cdb/rd_patch.cdb（补丁层是累积包，同名即换代）；
///       其他根部 *.cdb        → rd/cdb/&lt;原名&gt;
///     · pics/…               → rd/picture/card/…（RD 卡图按卡码取图，见 GameTextureManager）
///     · script/…             → rd/ai/script/…（AI 规则脚本，按卡码命名）
///     · server.ini（根部）   → 忽略（分发端元数据，客户端没有消费方）
///
///   **AI 侧同步**：包里带了根部 *.cdb 时，会把它 INSERT OR REPLACE 进
///   rd/ai/cdb/cards.cdb（AI.Server / WindBot 只认这一份合并表，见 unpack_rd.merge_cdbs；
///   客户端池与 AI 表是两份数据，铺文件必须两边都顾）。cards.cdb 不存在或被占用 ⇒
///   只记台账、不判包失败 —— 客户端数据已落地。
///
/// 失败处理（每一包都是独立的，绝不能挡启动）：
///   · 条目非法 / 解压中断 ⇒ 包改名 &lt;原名&gt;.failed（不是 *.ypk，下次不再碰），
///     原因写台账；已经解到 staging 的东西全部丢弃，**rd/ 一个字节都不动**。
///   · **zip 打不开且文件很新（&lt;120s）** ⇒ 视为「拖拽复制还没完成」，只记台账、原包保留，
///     下次扫描/下次启动再试 —— 复制到一半的包被改名 .failed 就冤死了（2026-09-20 用户实测）。
///   · 成功 ⇒ 删包、逐文件落到 rd/，写台账 log/rd_update.log（无条件落，判据与排障都靠它）。
///
/// 安全细节：
///   · 条目名先过「拒绝绝对路径 / 拒绝 .. / 拒绝白名单外」三道闸（闸作用在**映射后**的
///     rd/ 相对路径上），再解压 —— 否则一个恶意包可以把文件写到 rd/ 之外。
///   · 解压一律先进 rd/update/.staging，全部成功才整体搬运 —— 断电最坏也是 staging 留
///     半截垃圾，rd/ 里要么没有要么全新。
/// </summary>
public static class RdDataUpdater
{
    /// <summary>更新包的投放目录（相对游戏根）。不存在就建一个，方便用户找到投放点。</summary>
    public const string UpdateDir = "rd/update";

    /// <summary>解压暂存目录（在 UpdateDir 下，随包清空）。</summary>
    const string StagingDir = UpdateDir + "/.staging";

    /// <summary>台账（无条件落盘；[rdupdate] 探针只在 qt 开关下才有）。</summary>
    const string LedgerPath = "log/rd_update.log";

    /// <summary>允许的 rd/ 相对路径前缀。目录要带 /，单文件不带。</summary>
    static readonly string[] AllowedPrefixes =
    {
        "cdb/",
        "picture/",
        "ai/",
        "lflist.conf",
        "bot.conf",
    };

    /// <summary>复制中的宽容窗口：zip 打不开且文件这么新 ⇒ 等下一次扫描，不改名 .failed。</summary>
    const int BusyGraceSeconds = 120;

    /// <summary>
    /// 包条目 → rd/ 相对落地路径的映射。返回 null = 忽略（不落地也不算错）。
    /// 映射只认**包内路径**（先行卡包布局或 rd/ 布局），闸在 <see cref="RejectReason"/> 里做。
    /// </summary>
    static string MapEntry(string entryName)
    {
        string name = (entryName ?? "").Replace('\\', '/').TrimStart('/');
        int slash = name.IndexOf('/');
        if (slash < 0)
        {
            // 根部单文件
            string lower = name.ToLowerInvariant();
            if (lower.EndsWith(".cdb"))
            {
                // 「RD Patch.cdb」→ 覆盖补丁层 rd/cdb/rd_patch.cdb（归一化：去空格/下划线/连字符）。
                StringBuilder sb = new StringBuilder(lower);
                for (int i = sb.Length - 1; i >= 0; i--)
                {
                    char ch = sb[i];
                    if (ch == ' ' || ch == '_' || ch == '-')
                    {
                        sb.Remove(i, 1);
                    }
                }
                if (sb.ToString() == "rdpatch.cdb")
                {
                    return "cdb/rd_patch.cdb";
                }
                return "cdb/" + name;
            }
            if (lower == "server.ini")
            {
                return null; // 分发端元数据，客户端没有消费方
            }
            return name; // lflist.conf / bot.conf 等，走白名单判
        }
        string head = name.Substring(0, slash);
        string rest = name.Substring(slash + 1);
        if (string.Equals(head, "pics", StringComparison.OrdinalIgnoreCase))
        {
            return "picture/card/" + rest; // 先行卡包的卡图目录
        }
        if (string.Equals(head, "script", StringComparison.OrdinalIgnoreCase))
        {
            return "ai/script/" + rest; // AI 规则脚本
        }
        return name;
    }

    /// <summary>
    /// 条目名三道闸（作用在**映射后**的 rd/ 相对路径上）。返回 null = 放行；
    /// 否则返回拒收原因（会原样进台账）。一律以 / 分隔来判断。
    /// </summary>
    static string RejectReason(string entryName)
    {
        string name = (entryName ?? "").Replace('\\', '/').TrimStart('/');
        if (name.Length == 0)
        {
            return "空条目名";
        }
        if (Path.IsPathRooted(entryName))
        {
            return "绝对路径";
        }
        foreach (string seg in name.Split('/'))
        {
            if (seg == ".." || seg == ".")
            {
                return "路径含 . / ..（目录穿越）";
            }
        }
        string target = MapEntry(name);
        if (target == null)
        {
            return null; // 明确忽略的条目（如 server.ini）
        }
        foreach (string allow in AllowedPrefixes)
        {
            if (target.StartsWith(allow, StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }
        }
        return "映射到 " + target + " 白名单外（只认 cdb/ picture/ ai/ lflist.conf bot.conf）";
    }

    /// <summary>当前待应用的包名列表（*.ypk，按文件名排序）。给主菜单的确认弹窗做签名与文案。</summary>
    public static List<string> PendingPacks()
    {
        List<string> packs = new List<string>();
        try
        {
            if (!Directory.Exists(UpdateDir))
            {
                return packs;
            }
            foreach (FileInfo file in new DirectoryInfo(UpdateDir).GetFiles("*.ypk"))
            {
                packs.Add(file.Name);
            }
            packs.Sort();
        }
        catch (Exception e)
        {
            UnityEngine.Debug.Log(e);
        }
        return packs;
    }

    /// <summary>
    /// 应用 rd/update/ 下所有待更新包。启动链与主菜单「资源更新」都会调；
    /// 任何异常都吞掉 —— 更新失败只影响 RD 数据新旧，不该影响游戏起来。
    /// 返回**实际应用**的包数（0 = 没有待更新的包；失败的包不算，见台账）。
    /// </summary>
    public static int ApplyPendingPacks()
    {
        try
        {
            if (!Directory.Exists(UpdateDir))
            {
                try
                {
                    Directory.CreateDirectory(UpdateDir);
                }
                catch (Exception e)
                {
                    UnityEngine.Debug.Log(e);
                    return 0;
                }
            }
            List<string> packs = CollectPacks();
            if (packs.Count == 0)
            {
                return 0;
            }
            QuickTestTrace.Log("rdupdate", "pending=" + packs.Count);
            // 按文件名排序逐个应用：同名冲突时谁是覆盖方要可预期（字典序靠后的赢）。
            packs.Sort();
            int applied = 0;
            foreach (string pack in packs)
            {
                if (ApplyOne(pack))
                {
                    applied++;
                }
            }
            return applied;
        }
        catch (Exception e)
        {
            UnityEngine.Debug.Log(e);
            QuickTestTrace.Log("rdupdate", "pending-scan error " + e.Message);
            return 0;
        }
    }

    static List<string> CollectPacks()
    {
        List<string> packs = new List<string>();
        foreach (FileInfo file in new DirectoryInfo(UpdateDir).GetFiles("*.ypk"))
        {
            packs.Add(file.FullName);
        }
        return packs;
    }

    /// <summary>
    /// 应用**外部指定**的包（文件选择器路径），2026-09-20 主菜单「选择安装包…」用。
    /// 口径与 ApplyPendingPacks 完全一致（同一 ApplyOne：校验→staging→映射→AI 合并→台账），
    /// 但**不消耗原包**：应用成功后玩家自选的文件原样保留（可重复安装），失败也不改名
    /// .failed、只记台账（2026-09-20 用户反馈：安装不该消耗玩家的文件）。
    /// 返回实际应用的包数。
    /// </summary>
    public static int ApplyPacks(List<string> fullPaths)
    {
        if (fullPaths == null || fullPaths.Count == 0)
        {
            return 0;
        }
        try
        {
            // 字典序（忽略大小写）逐个应用：同名冲突时谁是覆盖方要可预期。
            List<string> sorted = new List<string>(fullPaths);
            sorted.Sort(StringComparer.OrdinalIgnoreCase);
            int applied = 0;
            foreach (string pack in sorted)
            {
                // 选择器路径：不消耗原包，失败也不动玩家的文件（只记台账）。
                if (ApplyOne(pack, deleteOnSuccess: false, renameOnFail: false))
                {
                    applied++;
                }
            }
            return applied;
        }
        catch (Exception e)
        {
            UnityEngine.Debug.Log(e);
            QuickTestTrace.Log("rdupdate", "explicit error " + e.Message);
            return 0;
        }
    }

    /// <summary>
    /// 应用一个包：校验 → 解到 staging → 映射搬运 → AI 合并 → 收尾/台账。
    /// deleteOnSuccess=false = 应用后**保留原包**（文件选择器路径用：包是玩家自己的文件，
    /// 安装不该消耗它 —— 2026-09-20 用户反馈）；renameOnFail=false = 失败时**不改名 .failed**
    /// （那是 update 投放目录的 quarantine 语义，对玩家自选的文件不能动）。
    /// </summary>
    static bool ApplyOne(string packFullPath, bool deleteOnSuccess = true, bool renameOnFail = true)
    {
        string packName = Path.GetFileName(packFullPath);
        ZipFile zip = null;
        try
        {
            // staging 先清干净：上一包的残渣混进这一包就是"新一半旧一半"。
            WipeStaging();
            Directory.CreateDirectory(StagingDir);

            try
            {
                zip = new ZipFile(packFullPath);
            }
            catch (Exception e)
            {
                // 打不开：可能是拖拽复制还没完成（文件仍被 Explorer 写着）。
                // 很新的文件保留原包等下一轮；旧文件才是真坏包，改名 .failed 收场。
                if ((DateTime.UtcNow - File.GetLastWriteTimeUtc(packFullPath)).TotalSeconds < BusyGraceSeconds)
                {
                    Ledger(Stamp() + " | busy    | " + packName + " | " + e.Message + "（复制中？保留待重试）");
                    QuickTestTrace.Log("rdupdate", "busy " + packName);
                    return false;
                }
                throw;
            }

            // 第一遍只验名：任何一条不合法，整包拒收 —— 落地一半再发现就晚了。
            int fileEntries = 0;
            foreach (ZipEntry entry in zip)
            {
                if (entry.IsDirectory)
                {
                    continue;
                }
                string reason = RejectReason(entry.FileName);
                if (reason != null)
                {
                    throw new Exception("条目 " + entry.FileName + " " + reason);
                }
                fileEntries++;
            }
            if (fileEntries == 0)
            {
                throw new Exception("空包");
            }

            // 名字全合法才放行解压。
            zip.ExtractAll(StagingDir, ExtractExistingFileAction.OverwriteSilently);
            zip.Dispose();
            zip = null;

            // staging → rd/ 映射搬运。rd/ 在启动这一刻没有任何句柄握着
            //（卡包 zip 句柄只挂 expansions/data；rd 的 cdb 是读到内存就关）。
            long bytes = 0;
            int moved = 0, skipped = 0;
            List<string> stagedCdbs = new List<string>();
            foreach (string src in Directory.GetFiles(StagingDir, "*", SearchOption.AllDirectories))
            {
                string rel = src.Substring(StagingDir.Length).Replace('\\', '/').TrimStart('/');
                string target = MapEntry(rel);
                if (target == null)
                {
                    skipped++;
                    continue;
                }
                string dst = "rd/" + target;
                string dir = Path.GetDirectoryName(dst);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }
                File.Copy(src, dst, true);
                bytes += new FileInfo(src).Length;
                moved++;
                if (target.StartsWith("cdb/", StringComparison.OrdinalIgnoreCase))
                {
                    stagedCdbs.Add(src);
                }
            }

            // AI 侧同步：包里带了卡表就把内容并入 rd/ai/cdb/cards.cdb（失败不追溯包）。
            int merged = MergeIntoAiCdb(stagedCdbs);

            if (deleteOnSuccess)
            {
                File.Delete(packFullPath);
            }
            WipeStaging();

            string line = Stamp() + " | applied | " + packName + " | files=" + moved
                + " | bytes=" + bytes + " | ai_merge=" + merged
                + (skipped > 0 ? " | skipped=" + skipped : "")
                + (deleteOnSuccess ? "" : " | kept（选择器安装，原包保留）");
            Ledger(line);
            QuickTestTrace.Log("rdupdate", "applied " + packName + " files=" + moved + " bytes=" + bytes + " ai_merge=" + merged);
            return true;
        }
        catch (Exception e)
        {
            if (zip != null)
            {
                try { zip.Dispose(); } catch { }
            }
            try { WipeStaging(); } catch { }
            if (renameOnFail)
            {
                FailPack(packFullPath, packName, e.Message);
            }
            else
            {
                // 玩家自选的文件：原样留在原位置，只在台账留原因，绝不改他的文件名。
                Ledger(Stamp() + " | failed  | " + packName + " | " + e.Message + "（原包保留）");
                QuickTestTrace.Log("rdupdate", "failed " + packName + " reason=" + e.Message);
            }
            return false;
        }
    }

    /// <summary>
    /// 把包里的卡表内容并入 AI 侧合并表 rd/ai/cdb/cards.cdb（INSERT OR REPLACE，同 id 后者赢，
    /// 口径与 unpack_rd.merge_cdbs 一致）。返回合并条目里的总行数；-1 = 没合并（无 AI 表 / 无卡表 / 被占用）。
    /// </summary>
    static int MergeIntoAiCdb(List<string> stagedCdbs)
    {
        if (stagedCdbs.Count == 0 || !File.Exists("rd/ai/cdb/cards.cdb"))
        {
            return -1;
        }
        SqliteConnection con = null;
        try
        {
            con = new SqliteConnection("Data Source=rd/ai/cdb/cards.cdb");
            con.Open();
            int total = 0;
            for (int i = 0; i < stagedCdbs.Count; i++)
            {
                string alias = "pack" + i;
                using (SqliteCommand attach = con.CreateCommand())
                {
                    attach.CommandText = "ATTACH DATABASE @p AS " + alias;
                    attach.Parameters.AddWithValue("@p", stagedCdbs[i]);
                    attach.ExecuteNonQuery();
                }
                try
                {
                    using (SqliteCommand cmd = con.CreateCommand())
                    {
                        // INSERT OR REPLACE 按 id 覆盖 —— 与客户端池的 replace=true 同口径。
                        cmd.CommandText = "INSERT OR REPLACE INTO main.datas SELECT * FROM " + alias + ".datas;"
                            + "INSERT OR REPLACE INTO main.texts SELECT * FROM " + alias + ".texts;";
                        total += cmd.ExecuteNonQuery();
                    }
                }
                finally
                {
                    using (SqliteCommand detach = con.CreateCommand())
                    {
                        detach.CommandText = "DETACH DATABASE " + alias + ";";
                        detach.ExecuteNonQuery();
                    }
                }
            }
            return total;
        }
        catch (Exception e)
        {
            // AI 表被占用（AI 局进行中）/结构不符都只记台账 —— 客户端数据已落地，包不判失败。
            Ledger(Stamp() + " | ai-merge-fail | " + e.Message);
            return -1;
        }
        finally
        {
            if (con != null)
            {
                try { con.Dispose(); } catch { }
            }
        }
    }

    static string Stamp()
    {
        return DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
    }

    /// <summary>包收场：改名 &lt;原名&gt;.failed（同名的旧 .failed 直接覆盖），台账留原因。</summary>
    static void FailPack(string packFullPath, string packName, string reason)
    {
        try
        {
            string failed = packFullPath.Substring(0, packFullPath.Length - 4) + ".failed";
            if (File.Exists(failed))
            {
                File.Delete(failed);
            }
            File.Move(packFullPath, failed);
        }
        catch (Exception e)
        {
            UnityEngine.Debug.Log(e);
        }
        Ledger(Stamp() + " | failed  | " + packName + " | " + reason);
        QuickTestTrace.Log("rdupdate", "failed " + packName + " reason=" + reason);
    }

    static void WipeStaging()
    {
        if (!Directory.Exists(StagingDir))
        {
            return;
        }
        try
        {
            Directory.Delete(StagingDir, true);
        }
        catch (Exception e)
        {
            // 残渣只影响磁盘，不影响正确性（下一包开工前还会再清一次）。
            UnityEngine.Debug.Log(e);
        }
    }

    /// <summary>台账无条件追加一行。log/ 缺了就建（正常运行时它必然在）。</summary>
    static void Ledger(string line)
    {
        try
        {
            string dir = Path.GetDirectoryName(LedgerPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }
            File.AppendAllText(LedgerPath, line + Environment.NewLine, Encoding.UTF8);
        }
        catch (Exception e)
        {
            UnityEngine.Debug.Log(e);
        }
    }
}

/// <summary>
/// Windows 原生「打开文件」对话框（comdlg32.GetOpenFileNameW，P/Invoke 零依赖 ——
/// 播放器的 Managed 目录里没有 System.Windows.Forms，别去引它）。
///
/// 交互约束（为什么这么绕）：GetOpenFileName 是**线程模态**的，在 Unity 主线程上直接调
/// 会把整条主循环卡住（窗口白屏/「未响应」，游戏要在后台继续转就得另开线程）。
/// 所以这里做成「发起 + 轮询取结果」：
///   · BeginPick     —— 起一个 STA 后台线程弹对话框（STA 必须设，文件对话框的约定）；
///   · TryTakeResult —— 主线程每帧来问；返回 true 即已结束（picks=null = 取消/失败）。
/// 同时只允许一个在途选择（BeginPick 在途时直接忽略，别叠两个线程）。
/// </summary>
internal static class NativeFilePicker
{
    /// <summary>OFN_ALLOWMULTISELECT | OFN_EXPLORER | OFN_FILEMUSTEXIST | OFN_HIDEREADONLY | OFN_NOCHANGEDIR</summary>
    const int Flags = 0x200 | 0x80000 | 0x1000 | 0x4 | 0x8;

    /// <summary>lpstrFile 缓冲区长度（多选时是「目录+全部文件名」，给到 API 的推荐上限）。</summary>
    const int BufferChars = 32767;

    static System.Threading.Thread worker;
    static string[] result;
    static volatile bool done;

    /// <summary>发起一次多选。已在途时静默忽略（别叠线程）。initialDir=null 则用上次目录。</summary>
    public static void BeginPick(string title, string initialDir, string filter)
    {
        if (worker != null && worker.IsAlive)
        {
            return;
        }
        result = null;
        done = false;
        string titleCopy = title;
        string dirCopy = initialDir;
        string filterCopy = filter;
        worker = new System.Threading.Thread(delegate()
        {
            result = ShowDialog(titleCopy, dirCopy, filterCopy);
            done = true;
        });
        worker.SetApartmentState(System.Threading.ApartmentState.STA);
        worker.IsBackground = true;
        worker.Start();
    }

    /// <summary>
    /// 主线程轮询。返回 false = 还没完（下帧再来）；返回 true = 已结束，
    /// picks = 选中的绝对路径（可多个），null = 取消/失败。
    /// </summary>
    public static bool TryTakeResult(out string[] picks)
    {
        if (!done)
        {
            picks = null;
            return false;
        }
        picks = result;
        worker = null;
        return true;
    }

    static string[] ShowDialog(string title, string initialDir, string filter)
    {
        // ⚠ CWD 是**进程级**的：Explorer 口径的对话框在用户浏览目录时会一路改它，
        // OFN_NOCHANGEDIR 实测不可靠（2026-09-20：CWD 被留在浏览目录里，ClientDataUpdater
        // 的 CWD 相对路径把 config/ 下载进了 rd/update/config/）。进来先存、走前必还。
        string cwdBefore = Environment.CurrentDirectory;
        IntPtr filterPtr = IntPtr.Zero, filePtr = IntPtr.Zero, titlePtr = IntPtr.Zero, dirPtr = IntPtr.Zero;
        try
        {
            // 过滤串是「描述\0模式\0描述\0模式\0\0」的双 NUL 结尾，只能按字符数组平铺。
            char[] filterChars = (filter + "\0").ToCharArray();
            filterPtr = Marshal.AllocHGlobal(filterChars.Length * 2);
            Marshal.Copy(filterChars, 0, filterPtr, filterChars.Length);

            filePtr = Marshal.AllocHGlobal(BufferChars * 2);
            Marshal.Copy(new char[BufferChars], 0, filePtr, BufferChars); // 首字符 \0 = 空初始名

            if (!string.IsNullOrEmpty(title))
            {
                char[] titleChars = (title + "\0").ToCharArray();
                titlePtr = Marshal.AllocHGlobal(titleChars.Length * 2);
                Marshal.Copy(titleChars, 0, titlePtr, titleChars.Length);
            }
            if (!string.IsNullOrEmpty(initialDir))
            {
                char[] dirChars = (initialDir + "\0").ToCharArray();
                dirPtr = Marshal.AllocHGlobal(dirChars.Length * 2);
                Marshal.Copy(dirChars, 0, dirPtr, dirChars.Length);
            }

            OpenFileName ofn = new OpenFileName();
            ofn.lStructSize = Marshal.SizeOf(typeof(OpenFileName));
            ofn.hwndOwner = IntPtr.Zero; // 后台线程上拿不到可靠的属主窗口，无主对话框即可
            ofn.lpstrFilter = filterPtr;
            ofn.lpstrFile = filePtr;
            ofn.nMaxFile = BufferChars;
            ofn.lpstrInitialDir = dirPtr;
            ofn.lpstrTitle = titlePtr;
            ofn.Flags = Flags;

            if (!GetOpenFileNameW(ref ofn))
            {
                return null; // 取消 / 失败
            }

            char[] buf = new char[BufferChars];
            Marshal.Copy(filePtr, buf, 0, BufferChars);
            List<string> parts = new List<string>();
            foreach (string piece in new string(buf).Split('\0'))
            {
                if (piece.Length > 0)
                {
                    parts.Add(piece);
                }
            }
            if (parts.Count == 0)
            {
                return null;
            }
            if (parts.Count == 1)
            {
                return new string[] { parts[0] }; // 单选：整条就是完整路径
            }
            // 多选（EXPLORER 口径）：第 0 条是目录，其余是文件名，拼回完整路径。
            string dir = parts[0];
            if (!dir.EndsWith("\\")) dir += "\\";
            List<string> picks = new List<string>();
            for (int i = 1; i < parts.Count; i++)
            {
                picks.Add(dir + parts[i]);
            }
            return picks.ToArray();
        }
        catch (Exception e)
        {
            UnityEngine.Debug.Log(e);
            return null;
        }
        finally
        {
            try { Environment.CurrentDirectory = cwdBefore; } catch { }
            if (filterPtr != IntPtr.Zero) Marshal.FreeHGlobal(filterPtr);
            if (filePtr != IntPtr.Zero) Marshal.FreeHGlobal(filePtr);
            if (titlePtr != IntPtr.Zero) Marshal.FreeHGlobal(titlePtr);
            if (dirPtr != IntPtr.Zero) Marshal.FreeHGlobal(dirPtr);
        }
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    struct OpenFileName
    {
        public int lStructSize;
        public IntPtr hwndOwner;
        public IntPtr hInstance;
        public IntPtr lpstrFilter;
        public IntPtr lpstrCustomFilter;
        public int nMaxCustFilter;
        public int nFilterIndex;
        public IntPtr lpstrFile;
        public int nMaxFile;
        public IntPtr lpstrFileTitle;
        public int nMaxFileTitle;
        public IntPtr lpstrInitialDir;
        public IntPtr lpstrTitle;
        public int Flags;
        public short nFileOffset;
        public short nFileExtension;
        public IntPtr lpstrDefExt;
        public IntPtr lCustData;
        public IntPtr lpfnHook;
        public IntPtr lpTemplateName;
        public IntPtr pvReserved;
        public int dwReserved;
        public int FlagsEx;
    }

    [DllImport("comdlg32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    static extern bool GetOpenFileNameW(ref OpenFileName ofn);
}
