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
                // 根部 cdb 归一化（去空格/下划线/连字符后比对）：
                //   「RD Patch.cdb」    → cdb/rd_patch.cdb（补丁层，同名即换代）
                //   「RD Alternate.cdb」→ cdb/rd_alternate.cdb（异画层 —— 曾因未归一化
                //      与 rd_alternate.cdb 重名共存，被 Program.LoadRdDatabase 装两遍）
                // 其它名字保持原样落 cdb/<原名>。
                StringBuilder sb = new StringBuilder(lower);
                for (int i = sb.Length - 1; i >= 0; i--)
                {
                    char ch = sb[i];
                    if (ch == ' ' || ch == '_' || ch == '-')
                    {
                        sb.Remove(i, 1);
                    }
                }
                string norm = sb.ToString();
                if (norm == "rdpatch.cdb")
                {
                    return "cdb/rd_patch.cdb";
                }
                if (norm == "rdalternate.cdb")
                {
                    return "cdb/rd_alternate.cdb";
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

            // 内容闸（防复发核心）：包里**纯 RD.AlternateCard 指令**的脚本必须能内联，
            // 否则整包拒收（宁可装不上，也不留下「装完了某卡失灵」）。这正对 2026-09-20
            // 那次事故：异画包经本通道原样覆盖，93 张卡进局后效果不注册。
            // 判定所需信息：包内脚本集合 + 已落地 rd/ai/script/。
            HashSet<string> zipScripts = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (ZipEntry entry in zip)
            {
                if (entry.IsDirectory)
                {
                    continue;
                }
                string t = MapEntry(entry.FileName);
                if (t != null && t.StartsWith("ai/script/", StringComparison.OrdinalIgnoreCase)
                    && t.EndsWith(".lua", StringComparison.OrdinalIgnoreCase))
                {
                    zipScripts.Add(Path.GetFileName(t));
                }
            }
            foreach (ZipEntry entry in zip)
            {
                if (entry.IsDirectory)
                {
                    continue;
                }
                string t = MapEntry(entry.FileName);
                if (t == null || !t.StartsWith("ai/script/", StringComparison.OrdinalIgnoreCase)
                    || !t.EndsWith(".lua", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                string text;
                using (MemoryStream ms = new MemoryStream())
                {
                    entry.Extract(ms);
                    text = Encoding.UTF8.GetString(ms.ToArray());
                }
                if (!IsPureAlternateDirective(text))
                {
                    continue; // 含其它内容的脚本原样放行
                }
                int code = 0;
                System.Text.RegularExpressions.Match mm = AltCardRe.Match(text);
                int.TryParse(mm.Groups[1].Value, out code);
                // 原型解析：包内优先，已落地兜底。
                bool resolvable = zipScripts.Contains("c" + code + ".lua")
                    || File.Exists("rd/ai/script/c" + code + ".lua");
                if (!resolvable)
                {
                    throw new Exception("异画脚本 " + Path.GetFileName(t)
                        + " 找不到原型 c" + code + ".lua（包内与已落地都没有），无法内联");
                }
            }

            // 名字全合法才放行解压。
            zip.ExtractAll(StagingDir, ExtractExistingFileAction.OverwriteSilently);
            zip.Dispose();
            zip = null;

            // staging → rd/ 映射搬运。rd/ 在启动这一刻没有任何句柄握着
            //（卡包 zip 句柄只挂 expansions/data；rd 的 cdb 是读到内存就关）。
            //
            // ⚠ 分两趟搬：**先非脚本、再脚本**。异画脚本要内联，而内联要从 staging 里
            //   的同包兄弟文件取原型；先搬完普通文件（含被指向的原型脚本已在包内时，
            //   原型本就同属「脚本」趟，靠 resolve 同时看 staging 与已落盘 rd/）。
            //   真正的原因是：不能依赖 zip 条目遍历顺序 —— 把所有脚本放一趟、用
            //   「staging 优先、已落地兜底」的解析顺序，就与顺序无关了。
            long bytes = 0;
            int moved = 0, skipped = 0;
            int inlined = 0;            // 内联展开的处数（诊断用）
            List<string> stagedCdbs = new List<string>();
            List<string> allFiles = new List<string>(
                Directory.GetFiles(StagingDir, "*", SearchOption.AllDirectories));
            // 稳定排序，保证台账与诊断可复现。
            allFiles.Sort(StringComparer.OrdinalIgnoreCase);

            // 先建一份「staging 内全部脚本 → 源码」的索引，供异画内联解析（不看遍历顺序）。
            Dictionary<string, string> stagedScripts = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (string f in allFiles)
            {
                string r = f.Substring(StagingDir.Length).Replace('\\', '/').TrimStart('/');
                string t = MapEntry(r);
                if (t != null && t.StartsWith("ai/script/", StringComparison.OrdinalIgnoreCase)
                    && t.EndsWith(".lua", StringComparison.OrdinalIgnoreCase))
                {
                    string key = Path.GetFileName(t);
                    if (!stagedScripts.ContainsKey(key))
                    {
                        stagedScripts.Add(key, f);
                    }
                }
            }
            // 解析委托：先查本包 staging，再查已落地的 rd/ai/script/（异画原型通常在正式卡包里，已安装）。
            Func<int, string> resolveScript = delegate (int tcode)
            {
                string fn = "c" + tcode + ".lua";
                string p;
                if (stagedScripts.TryGetValue(fn, out p) && File.Exists(p))
                {
                    return File.ReadAllText(p, Encoding.UTF8);
                }
                string landed = "rd/ai/script/" + fn;
                return File.Exists(landed) ? File.ReadAllText(landed, Encoding.UTF8) : null;
            };

            for (int pass = 0; pass < 2; pass++)
            {
                foreach (string src in allFiles)
                {
                    string rel = src.Substring(StagingDir.Length).Replace('\\', '/').TrimStart('/');
                    string target = MapEntry(rel);
                    if (target == null)
                    {
                        if (pass == 0)
                        {
                            skipped++;
                        }
                        continue;
                    }
                    bool isScript = target.StartsWith("ai/script/", StringComparison.OrdinalIgnoreCase)
                        && target.EndsWith(".lua", StringComparison.OrdinalIgnoreCase);
                    // pass 0 = 非脚本；pass 1 = 脚本。跳过不含在本次趟次的条目。
                    if (isScript != (pass == 1))
                    {
                        continue;
                    }
                    string dst = "rd/" + target;
                    string dir = Path.GetDirectoryName(dst);
                    if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    {
                        Directory.CreateDirectory(dir);
                    }
                    if (isScript)
                    {
                        // 脚本：读文本，纯指令就地内联，其余原样写。
                        string text = File.ReadAllText(src, Encoding.UTF8);
                        if (IsPureAlternateDirective(text))
                        {
                            int code = 0;
                            System.Text.RegularExpressions.Match mm = AltCardRe.Match(text);
                            int.TryParse(mm.Groups[1].Value, out code);
                            int[] counter = new int[1];
                            string expanded = ExpandAlternate(code, text, resolveScript, 0, null, counter);
                            File.WriteAllText(dst, expanded, Encoding.UTF8);
                            inlined += counter[0];
                            bytes += Encoding.UTF8.GetByteCount(expanded);
                        }
                        else
                        {
                            File.Copy(src, dst, true);
                            bytes += new FileInfo(src).Length;
                        }
                    }
                    else
                    {
                        File.Copy(src, dst, true);
                        bytes += new FileInfo(src).Length;
                    }
                    moved++;
                    if (target.StartsWith("cdb/", StringComparison.OrdinalIgnoreCase))
                    {
                        stagedCdbs.Add(src);
                    }
                }
            }

            // lflist 双侧同源：客户端 rd/lflist.conf 落地后，同步同字节写 AI 侧
            // rd/ai/config/lflist.conf（AI.Server 读 config/lflist.conf），
            // 否则两侧禁限口径分家（与 unpack_rd.py 阶段 6 同口径）。
            bool lflistSynced = false;
            if (File.Exists("rd/lflist.conf"))
            {
                try
                {
                    string lfDir = "rd/ai/config";
                    if (!Directory.Exists(lfDir))
                    {
                        Directory.CreateDirectory(lfDir);
                    }
                    File.Copy("rd/lflist.conf", lfDir + "/lflist.conf", true);
                    lflistSynced = true;
                }
                catch (Exception e)
                {
                    QuickTestTrace.Log("rdupdate", "lflist-sync failed " + e.Message);
                }
            }

            // 尾部自检：本包落地的脚本里仍残留「纯指令」⇒ 说明有没被内联的（不该发生）。
            // 不删已落地文件，只暴露（台账 ⚠ + 探针），便于事后排查。
            int residual = 0;
            foreach (KeyValuePair<string, string> kv in stagedScripts)
            {
                string landed = "rd/ai/script/" + kv.Key;
                try
                {
                    if (File.Exists(landed) && IsPureAlternateDirective(File.ReadAllText(landed, Encoding.UTF8)))
                    {
                        residual++;
                    }
                }
                catch { }
            }
            if (residual > 0)
            {
                Ledger(Stamp() + " | ⚠ residual-alt | " + packName + " | " + residual + " 个脚本仍是纯 RD.AlternateCard 指令");
                QuickTestTrace.Log("rdupdate", "residual-alt " + packName + " count=" + residual);
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
                + (inlined > 0 ? " | inlined=" + inlined : "")
                + (lflistSynced ? " | lflist_sync=1" : "")
                + (residual > 0 ? " | residual=" + residual : "")
                + (skipped > 0 ? " | skipped=" + skipped : "")
                + (deleteOnSuccess ? "" : " | kept（选择器安装，原包保留）");
            Ledger(line);
            QuickTestTrace.Log("rdupdate", "applied " + packName + " files=" + moved + " bytes=" + bytes
                + " ai_merge=" + merged + " inlined=" + inlined + " residual=" + residual);
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

    // ---------- 异画脚本内联（与 devtools/unpack_rd.py:expand_alternates 同口径） ----------

    /// <summary>匹配一行的 <c>RD.AlternateCard(N)</c>。与 unpack_rd.py 的 ALT_CARD_RE 同义。</summary>
    static readonly System.Text.RegularExpressions.Regex AltCardRe =
        new System.Text.RegularExpressions.Regex(@"RD\.AlternateCard\s*\(\s*(\d+)\s*\)");

    /// <summary>内联最大嵌套深度（与 unpack_rd.py 的 ALT_CARD_MAX_DEPTH 一致）。</summary>
    const int AltCardMaxDepth = 4;

    /// <summary>
    /// 把脚本源码里的 <c>RD.AlternateCard(N)</c> 就地展开成目标卡 c&lt;N&gt;.lua 的源码。
    ///
    /// 原版：<c>RushDuel.AlternateCard(code)</c> = <c>Duel.LoadScript("c"..code..".lua")</c>，
    /// 异画卡脚本整个文件就一行 —— 语义是「复用那张卡的实现，但 self_table/self_code 仍是我自己」。
    /// 本 core 没有 Duel.LoadScript（二进制里 "LoadScript" 命中 0 次），故装包期必须摊平，
    /// 否则异画卡进局后效果不注册 ⇒ **功能失灵**（2026-09-20 事故：93 张异画经本通道装入）。
    ///
    /// 包裹用 <c>do ... end</c>：原版被加载的 chunk 顶层 local 是自己的作用域，包一层块才等值。
    /// </summary>
    /// <param name="code">当前脚本的卡码（仅用于报错）。</param>
    /// <param name="source">当前脚本源码。</param>
    /// <param name="resolve">取目标卡源码的委托（返回 null = 找不到）。</param>
    /// <param name="depth">当前嵌套深度。</param>
    /// <param name="stack">调用链（循环引用检测）。</param>
    /// <param name="counter">已展开处数累加器（可为 null）。</param>
    static string ExpandAlternate(int code, string source, Func<int, string> resolve,
        int depth = 0, long[] stack = null, int[] counter = null)
    {
        if (source == null || !AltCardRe.IsMatch(source))
        {
            return source;
        }
        if (stack == null)
        {
            stack = new long[0];
        }
        return AltCardRe.Replace(source, delegate (System.Text.RegularExpressions.Match m)
        {
            int target = int.Parse(m.Groups[1].Value);
            for (int i = 0; i < stack.Length; i++)
            {
                if (stack[i] == target)
                {
                    throw new Exception("异画卡循环引用：c" + code + " -> c" + target);
                }
            }
            if (depth >= AltCardMaxDepth)
            {
                throw new Exception("异画卡嵌套超过 " + AltCardMaxDepth + " 层（c" + code + "）");
            }
            string sub = resolve(target);
            if (sub == null)
            {
                throw new Exception("异画脚本 c" + code + ".lua 找不到原型 c" + target + ".lua，无法内联");
            }
            long[] next = new long[stack.Length + 1];
            Array.Copy(stack, next, stack.Length);
            next[stack.Length] = code;
            string body = ExpandAlternate(target, sub, resolve, depth + 1, next, counter);
            if (counter != null)
            {
                counter[0]++;
            }
            return "do -- >>> 内联异画卡 c" + target + "（原脚本此处的 RD.AlternateCard(" + target + ")）\n"
                + body + "\n-- <<< 内联异画卡 c" + target + "\nend";
        });
    }

    /// <summary>脚本是否**仅**含一行 RD.AlternateCard(N)（允许空白/换行/BOM，不含其它有效内容）。</summary>
    static bool IsPureAlternateDirective(string source)
    {
        if (string.IsNullOrEmpty(source))
        {
            return false;
        }
        if (!AltCardRe.IsMatch(source))
        {
            return false;
        }
        // 去掉所有指令出现后若还剩非空白 ⇒ 不是「纯指令」，原样放行（不要动玩家自己写的内容）。
        string stripped = AltCardRe.Replace(source, "");
        return stripped.Trim().Length == 0;
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
