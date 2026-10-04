using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace YGOSharp
{
    /// <summary>
    /// 译名表的**按需下载 / 数据刷新**（需求 2026-10-02）。
    ///
    /// ── 口径变更（2026-10-03，两次都留档）──────────────────────
    /// 初版口径：「nw / cnocg / 简中 这三份译名是别人的数据，**不进分发包**」——
    /// 包里只带能下载的代码，玩家自己点一次才落到 <c>translation/</c>。
    /// <para>⛔⛔ 当天量了真实体积就改口径了：三张表加起来**只有 845~1013 KB**
    /// （gzip 后 336 KB），和一张卡面贴图同量级，远不到「值得让玩家等一次下载」
    /// 的程度 ⇒ 改为**随完整包分发 + 进自动更新**（生成器 `_gen_translation_pack.py`），
    /// 本类退居「数据刷新 / 补缺」通道（玩家自己删了表、或想更新到新数据时用）。</para>
    /// 所以出厂状态下这三份**不是** <see cref="CardNameTranslation.Pack.missing"/>；
    /// 而 <see cref="WriteTsvs"/> 的「已有同 key 数据就不覆盖」正好让本通道
    /// 在出厂状态下**无脑刷新也不动玩家的表**（<see cref="LastWritten"/> 会是 0）。
    ///
    /// ── 数据源 ──────────────────────────────────────────────────────────────
    /// 百鸽（ygocdb.com）的 <c>/api/v0/cards.zip</c>（2.4 MB，里面一张 cards.json
    /// 约 13.7 MB，1.4 万条）。一条记录里同时有 <c>nwbbs_n</c>（nw 译名）、
    /// <c>cnocg_n</c>（cnocg 译名）、<c>sc_name</c>（官方简中名）三个字段，
    /// ⚠ 所以**下一份 zip 就能把三张表一次补齐** —— 不是"选哪份下哪份"。
    /// 同端点还提供 <c>cards.zip.md5</c>（cards.json 的 md5），留作核对用。
    ///
    /// ── 落盘 ────────────────────────────────────────────────────────────────
    /// 写成 <c>translation/&lt;key&gt;.tsv</c>（每行「卡号&lt;TAB&gt;译名」，
    /// 首行 <c>#label 中文名</c>），之后由 <see cref="CardNameTranslation.Reload"/>
    /// 现成的装载路径接管 —— 与玩家自己丢一个 tsv/cdb 进去**完全同一条路**，
    /// 不存在"下载的"和"手放的"两套数据格式。
    /// 已经有一份同 key 的数据（出厂自带的、或玩家自己放过的）**不覆盖** ——
    /// 这条在「随包分发」的新口径下正好保证：本通道在出厂状态下是**纯 no-op**。
    ///
    /// ── 性能 ────────────────────────────────────────────────────────────────
    /// 下载走 <see cref="UnityFileDownloader"/>（异步，不卡主线程）；
    /// 解压用工程里现成的 Ionic.Zip；JSON 是**一遍流式扫描**（只取 4 个字段，
    /// 不建对象树）—— 13.7 MB 文本扫完在几十毫秒量级，整段同步跑在一帧里。
    /// </summary>
    public static class CardTranslationDownloader
    {
        /// <summary>数据源名字（给玩家看的署名）。</summary>
        public const string SourceLabel = "百鸽 YGOCDb";

        public const string ZipUrl = "https://ygocdb.com/api/v0/cards.zip";
        public const string Md5Url = "https://ygocdb.com/api/v0/cards.zip.md5";

        /// <summary>zip 里那一个条目的名字。</summary>
        const string EntryName = "cards.json";

        /// <summary>下载中转目录（translation/ 下的子目录；Reload 只扫根目录，不会误当数据表）。</summary>
        const string IncomingDir = "_incoming";

        /// <summary>可下载的三份表：key（= translation/ 里的文件名，与 KnownPacks 对齐）。</summary>
        static readonly string[] Keys = { "nw", "cnocg", "cn" };

        /// <summary>与 <see cref="Keys"/> 一一对应：cards.json 里的字段名。</summary>
        static readonly string[] Fields = { "nwbbs_n", "cnocg_n", "sc_name" };

        /// <summary>与 <see cref="Keys"/> 一一对应：写进 tsv 首行的 <c>#label</c>。</summary>
        static readonly string[] Labels = { "nw翻译", "cnocg翻译", "简中翻译" };

        /// <summary>一次下载能补齐的表（给界面判断"这份能不能靠下载装上"）。</summary>
        public static bool IsRemotePack(string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                return false;
            }
            for (int i = 0; i < Keys.Length; i++)
            {
                if (Keys[i] == key)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>有没有正在跑的下载。</summary>
        public static bool IsBusy { get; private set; }

        /// <summary>0~1。下载占前 90%，剩下 10% 留给解压与写盘。</summary>
        public static float Progress { get; private set; }

        /// <summary>上一次失败的原因（成功则空）。</summary>
        public static string LastError { get; private set; }

        /// <summary>上一次的结果摘要（"nw翻译 14258 条"这种，给提示条用）。</summary>
        public static string LastSummary { get; private set; }

        /// <summary>上一次**真正写进 translation/ 的表数**（已有同 key 数据时为 0）。
        /// ⛔ 2026-10-03 新增。存在的理由：验收探针原来「下载完就删掉那三份 tsv」，
        ///   而 2026-10-03 起**这三张表改为随包分发**（`_gen_translation_pack.py`）。
        ///   于是探针跑一次就把**交付树运行目录**里的出厂数据删了 ——
        ///   而那个目录正是成品包的镜像源（`_pkg_const.DELIVERY`），
        ///   下次打包就会「包里有、运行目录没有」，C2 当成多余文件判红。
        ///   有了这个计数，探针可以**只删自己写的那几份**（written&gt;0 才清场），
        ///   出厂数据一律不碰。</summary>
        public static int LastWritten { get; private set; }

        /// <summary>给提示条用的状态文案。</summary>
        public static string StatusText()
        {
            if (!IsBusy)
            {
                return null;
            }
            return "正在从" + SourceLabel + "下载译名表 " + Mathf.RoundToInt(Progress * 100f) + "%";
        }

        /// <summary>
        /// 开始下载。<paramref name="onDone"/> 的第二个参数是给人看的结果摘要。
        /// ⚠ 正在下载时重复调用会被忽略（界面那边也不该给出第二次入口）。
        /// </summary>
        public static void Start(Action<bool, string> onDone)
        {
            if (IsBusy)
            {
                return;
            }
            LastWritten = 0;      // ⛔ 每次开始前清零：上一轮写过不算这一轮的（见 LastWritten）
            if (Program.I() == null)
            {
                if (onDone != null)
                {
                    onDone(false, "游戏还没准备好。");
                }
                return;
            }
            IsBusy = true;
            Progress = 0f;
            LastError = null;
            LastSummary = null;
            Program.I().StartCoroutine(Co(onDone));
        }

        static IEnumerator Co(Action<bool, string> onDone)
        {
            string dir = Path.Combine(CardNameTranslation.Folder, IncomingDir);
            string zipPath = Path.Combine(dir, "cards.zip");

            bool ok = false;
            string err = null;
            Progress = 0.02f;

            yield return Program.I().StartCoroutine(
                UnityFileDownloader.DownloadFileAsync(
                    ZipUrl,
                    zipPath,
                    delegate (bool r) { ok = r; },
                    delegate (float p) { Progress = 0.02f + p * 0.88f; },
                    null,
                    delegate (string e) { err = e; ok = false; }));

            if (!ok)
            {
                Finish(false, "下载失败：" + OneLine(err ?? "网络不通？"), onDone);
                Cleanup(dir);
                yield break;
            }

            Progress = 0.92f;
            Dictionary<int, string>[] maps = new Dictionary<int, string>[Keys.Length];
            for (int i = 0; i < Keys.Length; i++)
            {
                maps[i] = new Dictionary<int, string>();
            }

            string why = null;
            try
            {
                Parse(zipPath, maps);
            }
            catch (Exception e)
            {
                why = OneLine(e.Message);
            }

            Cleanup(dir);

            if (why != null)
            {
                Finish(false, "下载到了，但读不出来：" + why, onDone);
                yield break;
            }

            Progress = 0.96f;
            int written = 0;
            StringBuilder sum = new StringBuilder();
            try
            {
                written = WriteTsvs(maps, sum);
                LastWritten = written;      // ⛔ 见 LastWritten：探针据此判断该不该清场
            }
            catch (Exception e)
            {
                Finish(false, "写不进 translation/：" + OneLine(e.Message), onDone);
                yield break;
            }

            if (written == 0)
            {
                // 三份表都已经有数据了（玩家自己放过），不算失败 —— 只是无事可做。
                Finish(true, "三份译名表都已经有数据了，没覆盖你自己的。", onDone);
                yield break;
            }

            Progress = 1f;
            // 交回给装载层：Reload 之后 packs 里那份就不再是 missing 了，
            // 由调用方决定要不要顺手切过去。
            CardNameTranslation.Reload();
            Finish(true, "已装好 " + written + " 份译名表（" + sum + "），来自" + SourceLabel + "。", onDone);
        }

        static void Finish(bool ok, string summary, Action<bool, string> onDone)
        {
            IsBusy = false;
            Progress = ok ? 1f : Progress;
            LastSummary = summary;
            if (!ok)
            {
                LastError = summary;
            }
            QuickTestTrace.Log("nametrans", "download done ok=" + ok + " " + summary);
            if (onDone != null)
            {
                onDone(ok, summary);
            }
        }

        static void Cleanup(string dir)
        {
            try
            {
                if (Directory.Exists(dir))
                {
                    Directory.Delete(dir, true);
                }
            }
            catch (Exception e)
            {
                Program.DEBUGLOG(e);
            }
        }

        // ---------------------------------------------------------------- 解析

        /// <summary>把 zip 里的 cards.json 扫一遍，按 <see cref="Fields"/> 填进 maps。</summary>
        static void Parse(string zipPath, Dictionary<int, string>[] maps)
        {
            byte[] data;
            using (Ionic.Zip.ZipFile zip = Ionic.Zip.ZipFile.Read(zipPath))
            {
                Ionic.Zip.ZipEntry e = zip[EntryName];
                if (e == null)
                {
                    throw new Exception("zip 里没有 " + EntryName);
                }
                using (MemoryStream ms = new MemoryStream())
                {
                    e.Extract(ms);
                    data = ms.ToArray();
                }
            }
            string text = Encoding.UTF8.GetString(data);
            Scanner sc = new Scanner(text, maps, Fields);
            sc.Run();
        }

        /// <summary>
        /// 极简 JSON 扫描器 —— **只取需要的字段，不建对象树**。
        ///
        /// cards.json 顶层是 <c>{ "4007": {...}, "4008": {...} }</c>，键是 cid（不是卡号），
        /// 卡号在条目的 <c>id</c> 字段里；条目里还有 text/data 两层嵌套对象与数组
        /// （占 90% 以上的体积），所以必须能**整块跳过**任意值，否则白扫十几 MB。
        /// </summary>
        class Scanner
        {
            readonly string s;
            readonly int len;
            int i;
            readonly Dictionary<int, string>[] maps;
            readonly string[] fields;

            public Scanner(string text, Dictionary<int, string>[] maps, string[] fields)
            {
                s = text;
                len = s != null ? s.Length : 0;
                this.maps = maps;
                this.fields = fields;
            }

            public void Run()
            {
                SkipWs();
                if (Peek() != '{')
                {
                    return;
                }
                i++;
                while (i < len)
                {
                    SkipWs();
                    char c = Peek();
                    if (c == '}')
                    {
                        i++;
                        break;
                    }
                    if (c == ',')
                    {
                        i++;
                        continue;
                    }
                    // 顶层键是 cid —— 用不上，直接丢。
                    if (c == '"')
                    {
                        ReadString();
                    }
                    else
                    {
                        return;
                    }
                    SkipWs();
                    if (Peek() == ':')
                    {
                        i++;
                    }
                    SkipWs();
                    if (Peek() == '{')
                    {
                        Entry();
                    }
                    else
                    {
                        SkipValue();
                    }
                }
            }

            void Entry()
            {
                i++;                                  // '{'
                long id = 0;
                string[] got = new string[fields.Length];
                while (i < len)
                {
                    SkipWs();
                    char c = Peek();
                    if (c == '}')
                    {
                        i++;
                        break;
                    }
                    if (c == ',')
                    {
                        i++;
                        continue;
                    }
                    if (c != '"')
                    {
                        SkipValue();
                        continue;
                    }
                    string key = ReadString();
                    SkipWs();
                    if (Peek() == ':')
                    {
                        i++;
                    }
                    SkipWs();
                    if (key == "id")
                    {
                        id = ReadNumber();
                        continue;
                    }
                    int slot = -1;
                    for (int k = 0; k < fields.Length; k++)
                    {
                        if (fields[k] == key)
                        {
                            slot = k;
                            break;
                        }
                    }
                    if (slot >= 0)
                    {
                        got[slot] = ReadString();
                    }
                    else
                    {
                        SkipValue();
                    }
                }
                if (id > 0 && id <= 0xFFFFFFFFL)
                {
                    int key = (int)id;
                    for (int k = 0; k < fields.Length; k++)
                    {
                        if (!string.IsNullOrEmpty(got[k]))
                        {
                            maps[k][key] = got[k];
                        }
                    }
                }
            }

            // ------------------------------------------------ 字符级小工具

            char Peek()
            {
                return i < len ? s[i] : '\0';
            }

            void SkipWs()
            {
                while (i < len && (s[i] == ' ' || s[i] == '\t' || s[i] == '\r' || s[i] == '\n'))
                {
                    i++;
                }
            }

            /// <summary>读一个字符串值（处理转义，含 \uXXXX）。</summary>
            string ReadString()
            {
                if (i >= len || s[i] != '"')
                {
                    return null;
                }
                i++;
                StringBuilder sb = new StringBuilder();
                while (i < len)
                {
                    char ch = s[i++];
                    if (ch == '"')
                    {
                        break;
                    }
                    if (ch != '\\')
                    {
                        sb.Append(ch);
                        continue;
                    }
                    if (i >= len)
                    {
                        break;
                    }
                    char e = s[i++];
                    switch (e)
                    {
                        case 'n': sb.Append('\n'); break;
                        case 't': sb.Append('\t'); break;
                        case 'r': sb.Append('\r'); break;
                        case 'b': sb.Append('\b'); break;
                        case 'f': sb.Append('\f'); break;
                        case 'u':
                            if (i + 4 <= len)
                            {
                                int code;
                                if (int.TryParse(s.Substring(i, 4), NumberStyles.HexNumber,
                                    CultureInfo.InvariantCulture, out code))
                                {
                                    sb.Append((char)code);
                                }
                                i += 4;
                            }
                            break;
                        default: sb.Append(e); break;
                    }
                }
                return sb.ToString();
            }

            long ReadNumber()
            {
                SkipWs();
                int start = i;
                while (i < len && (char.IsDigit(s[i]) || s[i] == '-' || s[i] == '+' || s[i] == '.'))
                {
                    i++;
                }
                long v;
                long.TryParse(s.Substring(start, i - start), out v);
                return v;
            }

            /// <summary>整块跳过一个值（对象/数组/字符串/数字/字面量）。</summary>
            void SkipValue()
            {
                SkipWs();
                char c = Peek();
                if (c == '"')
                {
                    ReadString();
                    return;
                }
                if (c == '{' || c == '[')
                {
                    int depth = 0;
                    while (i < len)
                    {
                        char ch = s[i++];
                        if (ch == '"')
                        {
                            i--;
                            ReadString();
                            continue;
                        }
                        if (ch == '{' || ch == '[')
                        {
                            depth++;
                        }
                        else if (ch == '}' || ch == ']')
                        {
                            depth--;
                            if (depth <= 0)
                            {
                                break;
                            }
                        }
                    }
                    return;
                }
                while (i < len && s[i] != ',' && s[i] != '}' && s[i] != ']')
                {
                    i++;
                }
            }
        }

        // ---------------------------------------------------------------- 落盘

        /// <summary>
        /// 把扫出来的三份表写成 <c>translation/&lt;key&gt;.tsv</c>。
        /// ⚠ 已经有一份同 key 的数据时**跳过**：那说明玩家自己放过一个（cdb 或 tsv），
        ///   覆盖掉等于把人家挑的那份换了 —— 缺哪份补哪份就行。
        /// </summary>
        static int WriteTsvs(Dictionary<int, string>[] maps, StringBuilder summary)
        {
            Directory.CreateDirectory(CardNameTranslation.Folder);
            int written = 0;
            for (int k = 0; k < Keys.Length; k++)
            {
                if (maps[k].Count == 0)
                {
                    continue;
                }
                if (PackHasData(Keys[k]))
                {
                    continue;
                }
                StringBuilder sb = new StringBuilder();
                sb.Append("# ").Append(Labels[k]).Append("　来源：").Append(SourceLabel)
                  .Append("（").Append(ZipUrl).Append("）\r\n");
                sb.Append("#label ").Append(Labels[k]).Append("\r\n");
                List<int> ids = new List<int>(maps[k].Keys);
                ids.Sort();
                for (int i = 0; i < ids.Count; i++)
                {
                    sb.Append(ids[i]).Append('\t').Append(Flatten(maps[k][ids[i]])).Append("\r\n");
                }
                File.WriteAllText(
                    Path.Combine(CardNameTranslation.Folder, Keys[k] + ".tsv"),
                    sb.ToString(), new UTF8Encoding(false));
                written++;
                if (summary.Length > 0)
                {
                    summary.Append("、");
                }
                summary.Append(Labels[k]).Append(" ").Append(ids.Count).Append(" 条");
            }
            return written;
        }

        /// <summary>这份 key 现在是不是已经有真数据（不是 missing 占位）。</summary>
        static bool PackHasData(string key)
        {
            List<CardNameTranslation.Pack> packs = CardNameTranslation.Packs;
            for (int i = 0; i < packs.Count; i++)
            {
                if (packs[i].key == key && !packs[i].missing && packs[i].Count > 0)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>tsv 是一行一条，名字里不能有换行/制表符。</summary>
        static string Flatten(string s)
        {
            if (string.IsNullOrEmpty(s))
            {
                return "";
            }
            return s.Replace("\r", " ").Replace("\n", " ").Replace("\t", " ").Trim();
        }

        static string OneLine(string s)
        {
            if (string.IsNullOrEmpty(s))
            {
                return "";
            }
            return s.Replace("\r", " ").Replace("\n", " ").Trim();
        }
    }
}
