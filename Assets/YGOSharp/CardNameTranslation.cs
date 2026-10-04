using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace YGOSharp
{
    /// <summary>
    /// 卡名翻译层 —— 「ygo原生翻译 / nw翻译 / cnocg翻译 / 简中翻译 / 自定义外号」。
    ///
    /// 设计要点（对应需求 5 / 6）：
    ///  * **真源不动**：<see cref="Card"/> 里新加的 <c>nativeName</c>/<c>nativeDesc</c> 保存 cdb 原文，
    ///    每张卡当前显示的 <c>Name</c>/<c>Desc</c> 都是**由原文 + 当前翻译表算出来的**。
    ///    所以换翻译不是"改一处显示"，而是"重算一遍"，不存在两处显示打架的可能。
    ///  * **描述跟着卡名走**：描述里出现的卡名（中文卡文一律写作「卡名」）在重算时一起替换，
    ///    于是把「卡通世界」改成「童话书」以后，所有"记述了卡通世界"的描述都会同步变成「童话书」。
    ///  * **单张覆盖优先于全局**：手改过译名的卡记在 <c>overrides</c> 里，换全局翻译时**不会**被带走。
    ///  * **数据是外挂的**：翻译表放在 <c>translation/</c> 目录（*.tsv 手写表，或任何 *.cdb 按 id 对照），
    ///    没放数据时只有"ygo原生翻译"一项 —— 界面按目录里真实存在的数据列项，不写死。
    ///  * **模式分家（RD）**：RD 侧**恒定原生**，也不做单卡换表 —— 只有「自定义外号」这一层保留，
    ///    所以 RD 的卡名 = 玩家自己起的别名，否则就是 cdb 原文。OCG 那份全局选择与单卡指定
    ///    **原样保住**（切回来即恢复），因为 <c>currentKey</c>/<c>cardPacks</c> 只被 OCG 改。
    ///    能选的清单走 <see cref="AvailablePacks"/>，与「磁盘上装了什么」(<see cref="Packs"/>) 分开。
    ///  * **性能**：翻译只在"装载完成 / 用户换表"时重算一次全池；平时的显示是查表，不参与每帧。
    /// </summary>
    public static class CardNameTranslation
    {
        public const string NativeKey = "native";
        public const string NativeLabel = "ygo原生翻译";

        /// <summary>外挂翻译数据所在目录（相对游戏根目录）。</summary>
        public const string Folder = "translation";

        static string SettingsPath { get { return Path.Combine(Folder, "settings.txt"); } }

        /// <summary>一份翻译表。key 稳定（=文件名），label 给人看。</summary>
        public class Pack
        {
            public string key = "";
            public string label = "";
            public bool isNative = false;
            public string source = "";
            /// <summary>true = 玩家点名要的那份表，但 translation/ 里还没有数据（界面要标注出来）。</summary>
            public bool missing = false;
            /// <summary>按卡号对照（cdb 导入、或 tsv 里写了数字 id 的行）。</summary>
            public Dictionary<int, string> byId = new Dictionary<int, string>();
            /// <summary>按"原生卡名"对照（tsv 里写 <c>=原生名</c> 的行，给不知道卡号的人用）。</summary>
            public Dictionary<string, string> byName = new Dictionary<string, string>();

            public int Count { get { return byId.Count + byName.Count; } }

            public bool TryGet(int id, string nativeName, out string name)
            {
                name = null;
                if (byId.Count > 0 && byId.TryGetValue(id, out name) && !string.IsNullOrEmpty(name))
                {
                    return true;
                }
                name = null;
                if (byName.Count > 0 && !string.IsNullOrEmpty(nativeName)
                    && byName.TryGetValue(nativeName, out name) && !string.IsNullOrEmpty(name))
                {
                    return true;
                }
                name = null;
                return false;
            }
        }

        /// <summary>
        /// 玩家点名要的那几份翻译表。translation/ 里没有对应文件时也列在菜单里（标"未装数据"），
        /// 这样界面上永远能看到 "ygo原生翻译 / nw翻译 / cnocg翻译 / 简中翻译" 这一组，
        /// 而不是黑箱地只剩一项。文件名的各种常见写法都算数。
        /// </summary>
        class KnownPack
        {
            public string key;
            public string label;
            public string[] aliases;
        }

        static readonly KnownPack[] KnownPacks = new KnownPack[]
        {
            new KnownPack { key = "nw",    label = "nw翻译",
                aliases = new string[] { "nw", "nwbbs", "newwise", "nw翻译" } },
            new KnownPack { key = "cnocg", label = "cnocg翻译",
                aliases = new string[] { "cnocg", "cnocg翻译" } },
            new KnownPack { key = "cn",    label = "简中翻译",
                aliases = new string[] { "cn", "sc", "zh-cn", "zhcn", "zh_cn", "simp", "简中", "简中翻译" } },
        };

        /// <summary>按文件名（不区分大小写、认别名）认领一份已知翻译表；不认识返回 null。</summary>
        static KnownPack FindKnown(string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                return null;
            }
            string k = key.Trim().ToLowerInvariant();
            for (int i = 0; i < KnownPacks.Length; i++)
            {
                string[] a = KnownPacks[i].aliases;
                for (int j = 0; j < a.Length; j++)
                {
                    if (a[j] == k)
                    {
                        return KnownPacks[i];
                    }
                }
            }
            return null;
        }

        static readonly List<Pack> packs = new List<Pack>();
        static readonly Dictionary<string, Pack> packByKey = new Dictionary<string, Pack>();
        /// <summary>异画组组主卡号 → 玩家给这一组指定的翻译表 key（空 = 跟随全局）。</summary>
        static readonly Dictionary<int, string> overrides = new Dictionary<int, string>();
        /// <summary>异画组组主卡号 → 玩家给这一组指定的翻译表 key（"global" 不落盘，见 SetCardPack）。</summary>
        static readonly Dictionary<int, string> cardPacks = new Dictionary<int, string>();
        static readonly Pack nativePack = new Pack { key = NativeKey, label = NativeLabel, isNative = true, source = "cdb" };

        static string currentKey = NativeKey;
        static bool inited = false;

        /// <summary>
        /// 翻译数据的版本号。任何"某张卡该显示成什么名字"会变的动作都把它 +1，
        /// 上游（卡池重算、富文本缓存）靠它判断要不要重来一遍。
        /// </summary>
        public static int Version { get; private set; }

        static void Bump()
        {
            Version++;
        }

        /// <summary>
        /// **模式切换时调**：让"每张卡现在该显示成什么名字"整体失效一次。
        ///
        /// 为什么不直接 <see cref="Reload"/>：RD 与 OCG 用的是**同一批文件**，差别只在「选哪一份」
        /// —— 重扫磁盘毫无意义，而且 Reload 会把 <c>currentKey</c>/<c>overrides</c>/<c>cardPacks</c>
        /// 从盘上重读，反而可能把内存里刚改还没落盘的东西冲掉。
        ///
        /// ⛔ 不 bump 的后果是实打实的：池里的 <c>Card.Name</c> 是 <c>ApplyPool</c> 那一刻按
        ///   「当时的 Current」算好的**快照**，而 <c>CardsManager.EnsureNameTranslation</c> 只在
        ///   <see cref="Version"/> 变了才重算 ⇒ OCG 下选了 nw 再切到 RD，界面上照样是 nw，
        ///   「RD 恒原生」就只是数据层的一厢情愿。上游（NameStamp / DisplayNameIndex）也靠它作废缓存。
        /// </summary>
        public static void InvalidateForMode()
        {
            Ensure();
            Bump();
        }

        /// <summary>已装好的翻译表（第 0 项永远是 native）。**与模式无关**：磁盘上装了什么就报什么。</summary>
        public static List<Pack> Packs
        {
            get { Ensure(); return packs; }
        }

        /// <summary>
        /// **当前模式下能选**的翻译表清单 —— UI（设置行下拉 / 「译」菜单里的「全局译名」）读它。
        ///
        /// RD 只有原生一份：nw / cnocg / 简中 三套表收的全是 OCG 卡，RD 卡的码域是
        /// 12xxxxxxxx，那三份表里一条都查不到 —— 摆进下拉只会让玩家以为「能换却没生效」。
        /// ⛔ 别把结果缓进 <see cref="packs"/>：那份的口径是「磁盘上真装了什么」，
        ///   与模式无关，改它会污染 OCG 侧的清单。
        /// </summary>
        public static List<Pack> AvailablePacks
        {
            get
            {
                Ensure();
                if (!GameModeManager.IsRD)
                {
                    return packs;
                }
                List<Pack> rd = new List<Pack>();
                rd.Add(nativePack);
                return rd;
            }
        }

        /// <summary>当前**生效**的那份翻译表的 key（RD 恒为 <see cref="NativeKey"/>）。</summary>
        public static string CurrentKey
        {
            get { Ensure(); return GameModeManager.IsRD ? NativeKey : currentKey; }
        }

        /// <summary>当前**生效**的那份翻译表（RD 恒为原生那份）。</summary>
        public static Pack Current
        {
            get
            {
                Ensure();
                if (GameModeManager.IsRD)
                {
                    return nativePack;
                }
                Pack p;
                if (packByKey.TryGetValue(currentKey, out p))
                {
                    return p;
                }
                return nativePack;
            }
        }

        public static string CurrentLabel
        {
            get
            {
                Pack p = Current;
                return p != null ? p.label : NativeLabel;
            }
        }

        /// <summary>
        /// 切全局翻译。返回 false = 没有这份表（界面不该走到这里，防一手）。
        /// ⚠ 只改"当前选择"，真正的重算由 <see cref="YGOSharp.CardsManager.ReapplyNameTranslation"/> 做。
        ///
        /// ⛔ <b>RD 下恒定原生、且一个字都不落盘</b>：这里写的是 <c>currentKey</c>，
        ///   而那份是 **OCG 那一侧**的选择 —— 在 RD 里写它，玩家切回 OCG 就会发现
        ///   自己原来选的 nw/cnocg 被冲成了原生。「RD 换一份全局译名」这件事本身不成立
        ///   （那几份表里没有 RD 卡），所以直接维持 native，并原样保住 OCG 的选择。
        /// </summary>
        public static bool SetCurrent(string key)
        {
            Ensure();
            if (string.IsNullOrEmpty(key))
            {
                key = NativeKey;
            }
            if (GameModeManager.IsRD)
            {
                if (QuickTestTrace.Enabled && key != NativeKey)
                {
                    QuickTestTrace.Log("nametrans", "rd pack request ignored key=" + key
                        + "（RD 恒原生；OCG 那份选择原样保留）");
                }
                return true;
            }
            if (!packByKey.ContainsKey(key))
            {
                return false;
            }
            if (currentKey == key)
            {
                return true;
            }
            currentKey = key;
            Bump();
            SaveSettings();
            return true;
        }

        /// <summary>
        /// 这张卡（连同它的异画组）现在有没有自定义外号。
        ///
        /// ⚠ 自定义名字是**按异画组**存的：给「电子界代码魔术师」取名，它的异画一起改
        ///   （分组判据见 <see cref="YGOSharp.CardsManager.AliasRootOf"/>）。但水波海豚 / 海洋海豚
        ///   这种只共用 alias、卡名不同的「真·不同的卡」自成一组，不会连坐。
        /// </summary>
        public static bool HasOverride(int id)
        {
            return !string.IsNullOrEmpty(GetOverride(id));
        }

        public static string GetOverride(int id)
        {
            Ensure();
            string v;
            if (overrides.TryGetValue(id, out v) && !string.IsNullOrEmpty(v))
            {
                return v;
            }
            int root = YGOSharp.CardsManager.AliasRootOf(id);
            if (root != id && overrides.TryGetValue(root, out v) && !string.IsNullOrEmpty(v))
            {
                return v;
            }
            return null;
        }

        /// <summary>
        /// 给一张卡**连同它的异画**设自定义外号；空串 = 取消，回到全局翻译。
        /// 落盘的键是异画组的组主卡号，所以组里任何一张改了名，全组一起改。
        /// </summary>
        public static void SetOverride(int id, string name)
        {
            Ensure();
            name = (name ?? "").Trim();
            int root = YGOSharp.CardsManager.AliasRootOf(id);
            if (root <= 0)
            {
                root = id;
            }
            // 组里先清干净：老设置文件可能残留着「按单张异画记」的行，留着会盖住组主的新名字。
            bool changed = overrides.Remove(id);
            if (root != id)
            {
                changed |= overrides.Remove(root);
            }
            if (name.Length == 0)
            {
                if (changed)
                {
                    Bump();
                    SaveSettings();
                }
                return;
            }
            string old;
            if (!changed && overrides.TryGetValue(root, out old) && old == name)
            {
                return;
            }
            overrides[root] = name;
            Bump();
            SaveSettings();
        }

        /// <summary>有几张卡被单独改过名（给界面/验收报数用）。</summary>
        public static int OverrideCount
        {
            get { Ensure(); return overrides.Count; }
        }

        /// <summary>
        /// **单卡专用翻译**（需求 2026-10-02）：让某一张卡用另一份翻译表，
        /// 与"全局用哪一份"（<see cref="SetCurrent"/>）互不干扰 ——
        /// 全局换成 nw 的时候，这些卡仍按自己指定的那份显示。
        /// 落盘的键同样是**异画组的组主卡号**（理由见 <see cref="SetOverride"/>）。
        /// 空串 / "global" = 跟随全局。
        /// </summary>
        public static bool HasCardPack(int id)
        {
            return !string.IsNullOrEmpty(GetCardPack(id));
        }

        public static string GetCardPack(int id)
        {
            Ensure();
            return GetCardPackOfGroup(id);
        }

        static string GetCardPackOfGroup(int id)
        {
            // RD 不做「单卡换表」（「译」菜单里那一项在 RD 下整个不出现，见 NameTranslationUI）：
            // 直接把单卡指定判成"没有" ⇒ 显示层自然落到全局那一份（RD 下＝原生）。
            // ⛔ 只是"这一刻不读"，**不清盘**：OCG 存的那些指定要原样留着，切回去照常生效。
            if (GameModeManager.IsRD)
            {
                return null;
            }
            string v;
            if (cardPacks.TryGetValue(id, out v) && !string.IsNullOrEmpty(v))
            {
                return v;
            }
            int root = YGOSharp.CardsManager.AliasRootOf(id);
            if (root != id && cardPacks.TryGetValue(root, out v) && !string.IsNullOrEmpty(v))
            {
                return v;
            }
            return null;
        }

        /// <summary>
        /// 给一张卡（连同它的异画）指定用哪一份翻译表。
        /// <paramref name="key"/> 为空 / "global" = 取消单卡指定（回到全局那一份）。
        /// 指定的那份表里**没有**这张卡时，显示层会自然退回全局那份
        /// （见 <see cref="DisplayName"/>），所以不必在这里拦。
        /// </summary>
        public static void SetCardPack(int id, string key)
        {
            Ensure();
            if (GameModeManager.IsRD)
            {
                return;         // RD 不做单卡换表（理由与 GetCardPackOfGroup 同）
            }
            int root = YGOSharp.CardsManager.AliasRootOf(id);
            if (root <= 0)
            {
                root = id;
            }
            key = (key ?? "").Trim();
            if (key == "global")
            {
                key = "";
            }
            // 组里先清干净，理由与 SetOverride 相同（老设置文件可能残留按单张异画记的行）。
            bool changed = cardPacks.Remove(id);
            if (root != id)
            {
                changed |= cardPacks.Remove(root);
            }
            if (key.Length == 0 || !packByKey.ContainsKey(key))
            {
                if (changed)
                {
                    Bump();
                    SaveSettings();
                }
                return;
            }
            string old;
            if (!changed && cardPacks.TryGetValue(root, out old) && old == key)
            {
                return;
            }
            cardPacks[root] = key;
            Bump();
            SaveSettings();
        }

        /// <summary>有几张卡指定了自己的翻译表（给界面/验收报数用）。</summary>
        public static int CardPackCount
        {
            get { Ensure(); return cardPacks.Count; }
        }

        // ------------------------------------------------------- 批量指定（检索窗的「译」钮）

        /// <summary>
        /// **一次给一整批卡**指定用哪一份翻译表 —— 检索窗里那颗「译」钮的落点
        /// （用户 2026-10-03：「该按钮可以用于批量决定该批关联卡采用跟随、原生、nw、
        /// cnocg、简中中哪一种翻译」）。
        ///
        /// <para><b>语义与 <see cref="SetCardPack"/> 完全一致</b>，只是把 N 次调用并成一次：
        /// 键落在**异画组组主**卡号上、<paramref name="key"/> 空 / "global" = 跟随全局、
        /// 组里先清干净（理由同 <see cref="SetCardPack"/>）。</para>
        ///
        /// <para>⛔ <b>为什么不直接 for 循环调 <see cref="SetCardPack"/></b>：那一条每张卡
        /// 都 <c>Bump()</c> 一次版本号、<c>SaveSettings()</c> 一次**全量重写 settings.txt**。
        /// 检索窗一次能出几千张（RD 的「怪兽·魔法·陷阱」是 3480 张），循环一遍就是几千次
        /// 同步磁盘写 —— 界面上表现为点一下「译」卡死好几秒。这里先把卡号去重成组、
        /// 全部改完再 <c>Bump()</c> + 落盘各**一次</c>。</para>
        ///
        /// <para>RD 与 <see cref="SetCardPack"/> 同口径：<b>不生效、一个字都不落盘</b>
        /// （RD 恒原生，见 <see cref="GetCardPackOfGroup"/>）。</para>
        ///
        /// <returns>真正改动了的**异画组**数（0 = 本来就都是这份，不用重算也不用落盘）。</returns>
        public static int SetCardPackBatch(List<int> ids, string key)
        {
            Ensure();
            if (GameModeManager.IsRD || ids == null || ids.Count == 0)
            {
                return 0;         // RD 不做单卡换表（理由与 SetCardPack 同）
            }
            key = (key ?? "").Trim();
            if (key == "global")
            {
                key = "";
            }
            bool known = key.Length > 0 && packByKey.ContainsKey(key);

            // 先按组去重：同一组的多张异画只落一条键（否则同一行会被反复写）。
            HashSet<int> groups = new HashSet<int>();
            for (int i = 0; i < ids.Count; i++)
            {
                int id = ids[i];
                if (id <= 0)
                {
                    continue;
                }
                int root = YGOSharp.CardsManager.AliasRootOf(id);
                groups.Add(root > 0 ? root : id);
            }

            int changed = 0;
            for (int i = 0; i < ids.Count; i++)
            {
                // 组里先清干净：老设置文件可能残留按单张异画记的行（与 SetCardPack 同理）。
                if (ids[i] > 0 && cardPacks.Remove(ids[i]))
                {
                    changed++;
                }
            }
            foreach (int root in groups)
            {
                if (root > 0 && cardPacks.Remove(root))
                {
                    changed++;
                }
            }
            if (key.Length == 0 || !known)
            {
                // 跟随全局 / 未知表：上面已经清干净了，changed>0 才需要落盘。
                if (changed > 0)
                {
                    Bump();
                    SaveSettings();
                }
                return changed;
            }
            bool anyNew = false;
            foreach (int root in groups)
            {
                string old;
                if (cardPacks.TryGetValue(root, out old) && old == key)
                {
                    continue;   // 本来就是这份，不算改动
                }
                cardPacks[root] = key;
                changed++;
                anyNew = true;
            }
            if (anyNew)
            {
                Bump();
                SaveSettings();
            }
            return changed;
        }

        /// <summary>
        /// 这一批卡此刻"跟着哪一份"的**一致口径**，给批量菜单标「●」用。
        /// 全都跟随全局 → <see cref="FollowGlobalKey"/>；全都是同一份 → 那个 key；
        /// 混着 / 有表没装 → 空串（菜单就不标任何一项）。
        /// </summary>
        public static string BatchPackKey(List<int> ids)
        {
            Ensure();
            if (GameModeManager.IsRD || ids == null || ids.Count == 0)
            {
                return "";
            }
            string common = null;
            HashSet<int> seen = new HashSet<int>();
            for (int i = 0; i < ids.Count; i++)
            {
                int id = ids[i];
                if (id <= 0)
                {
                    continue;
                }
                int root = YGOSharp.CardsManager.AliasRootOf(id);
                if (!seen.Add(root > 0 ? root : id))
                {
                    continue;   // 同一组只看一次（组内本来就一样）
                }
                string k = GetCardPackOfGroup(id) ?? "";
                if (common == null)
                {
                    common = k;
                }
                else if (common != k)
                {
                    return "";    // 混着 ⇒ 不标
                }
            }
            return common == null ? "" : common;
        }

        /// <summary>「跟随全局」在批量菜单里的哨兵值（不落盘，与 NameTranslationUI 同名常量一致）。</summary>
        public const string FollowGlobalKey = "global";

        // ------------------------------------------------------- 系列（字段）译名（需求 3)

        /// <summary>
        /// 系列（字段）译名 —— **按翻译表分家存** + **只做纯手改**（用户 2026-10-02 口径）。
        ///
        /// 「系列」= <c>datas.setcode</c> 那 4 个 16 位段在 <c>strings.conf !setname</c> 里的名字
        /// （<c>GameStringManager.xilies</c>）。它出现在两处：
        ///   ① **简介正文**里被「」引用的那串字（中文卡文把字段名也写成「元素英雄」）；
        ///   ② **简介系列行**（<c>GameStringHelper.getSmall</c> 末尾那行「系列：xxx|yyy」）。
        /// 两处都由下面这几个口子统一取显示名，改一处全跟着走。
        ///
        /// ⛔ **不做自动反推**（用户明确否掉「看看能不能自动配置 nw / cnocg / 简中的字段名」）：
        ///   那几份表只收卡名、没有字段列，硬反推只能猜。所以这里只有玩家亲手写的译名，
        ///   谁都没写就显示原生名。
        /// 分家存的含义：同一份 pack（nw / cnocg / 简中 / native）各存一套 —— 换全局表时
        /// 用**那一份**的字段译名，OCG 写的不会渗到 native、RD 写的也不会冲掉 OCG 的。
        /// </summary>
        static readonly Dictionary<string, Dictionary<string, string>> fieldMaps =
            new Dictionary<string, Dictionary<string, string>>();

        /// <summary>
        /// 字段译名**存/取**该落在哪一份 pack 下（与 <see cref="CurrentKey"/> 同口径）：
        /// RD 恒 native —— 分家存，OCG 写的那些原样保留，切回去照常生效。
        /// </summary>
        static string FieldPackKey
        {
            get { return GameModeManager.IsRD ? NativeKey : currentKey; }
        }

        static Dictionary<string, string> FieldMapOf(string packKey)
        {
            Dictionary<string, string> m;
            if (!fieldMaps.TryGetValue(packKey, out m) || m == null)
            {
                m = new Dictionary<string, string>();
                fieldMaps[packKey] = m;
            }
            return m;
        }

        /// <summary>当前生效 pack 下，这个原生系列名的显示名（没手改就原样返回）。</summary>
        public static string FieldNameDisplay(string nativeField)
        {
            if (string.IsNullOrEmpty(nativeField))
            {
                return nativeField;
            }
            Ensure();
            Dictionary<string, string> m;
            string v;
            if (fieldMaps.TryGetValue(FieldPackKey, out m) && m != null
                && m.TryGetValue(nativeField, out v) && !string.IsNullOrEmpty(v))
            {
                return v;
            }
            return nativeField;
        }

        /// <summary>这个原生系列名在**当前生效 pack** 下有没有手改译名。</summary>
        public static bool HasFieldOverride(string nativeField)
        {
            if (string.IsNullOrEmpty(nativeField))
            {
                return false;
            }
            Ensure();
            Dictionary<string, string> m;
            string v;
            return fieldMaps.TryGetValue(FieldPackKey, out m) && m != null
                && m.TryGetValue(nativeField, out v) && !string.IsNullOrEmpty(v);
        }

        /// <summary>这个原生系列名当前显示成什么（没手改返回 null，给菜单标题用）。</summary>
        public static string GetFieldOverride(string nativeField)
        {
            if (!HasFieldOverride(nativeField))
            {
                return null;
            }
            return fieldMaps[FieldPackKey][nativeField];
        }

        /// <summary>
        /// 给一个系列设译名（**当前生效 pack** 下）；空串 = 还原。
        /// 与 <see cref="SetOverride"/> 同款判据：值没变不落盘，避免"打开改名框→直接确定"
        /// 写进一条等于原名、却让界面从此挂着一串假设置的记录。
        /// </summary>
        public static void SetFieldOverride(string nativeField, string name)
        {
            if (string.IsNullOrEmpty(nativeField))
            {
                return;
            }
            Ensure();
            name = (name ?? "").Trim();
            string key = FieldPackKey;
            Dictionary<string, string> m = FieldMapOf(key);
            if (name.Length == 0)
            {
                if (m.Remove(nativeField))
                {
                    if (m.Count == 0)
                    {
                        fieldMaps.Remove(key);
                    }
                    Bump();
                    SaveSettings();
                }
                return;
            }
            if (name == nativeField)
            {
                // 写回原名 = 没改：不落盘（与 SetOverride 的"提交框里预填原文"同一个坑）。
                if (m.Remove(nativeField))
                {
                    if (m.Count == 0)
                    {
                        fieldMaps.Remove(key);
                    }
                    Bump();
                    SaveSettings();
                }
                return;
            }
            string old;
            if (m.TryGetValue(nativeField, out old) && old == name)
            {
                return;
            }
            m[nativeField] = name;
            Bump();
            SaveSettings();
        }

        /// <summary>当前生效 pack 下有几条字段译名（给界面/验收报数用）。</summary>
        public static int FieldOverrideCount
        {
            get
            {
                Ensure();
                Dictionary<string, string> m;
                return fieldMaps.TryGetValue(FieldPackKey, out m) && m != null ? m.Count : 0;
            }
        }

        /// <summary>
        /// **当前生效 pack** 的「原生系列名 → 译名」表（给 <c>CardsManager.ApplyPool</c>
        /// 并进 nameMap，让简介正文里「字段名」的引用跟着换）。空表返回 null。
        /// ⚠ 调用方只读，不许改这份字典。
        /// </summary>
        internal static Dictionary<string, string> FieldMapForCurrentPack()
        {
            Ensure();
            Dictionary<string, string> m;
            if (fieldMaps.TryGetValue(FieldPackKey, out m) && m != null && m.Count > 0)
            {
                return m;
            }
            return null;
        }

        /// <summary>
        /// 这张卡现在该显示成什么名字。优先级（自高而低）：
        ///   ① **自定义外号**（<see cref="GetOverride"/>）
        ///   ② **单卡指定的翻译表**（<see cref="GetCardPack"/>，仅当那份表里确实有这张卡）
        ///   ③ 全局翻译表（<see cref="Current"/>）
        ///   ④ cdb 原文
        /// 异画会跟着组主一起显示 —— ①② 两档都是按组存的。
        /// </summary>
        public static string DisplayName(int id, string nativeName)
        {
            Ensure();
            string over = GetOverride(id);
            if (!string.IsNullOrEmpty(over))
            {
                return over;
            }
            string key = GetCardPackOfGroup(id);
            if (!string.IsNullOrEmpty(key))
            {
                Pack mine;
                string fromMine;
                if (packByKey.TryGetValue(key, out mine) && mine != null && !mine.isNative
                    && mine.TryGet(id, nativeName, out fromMine))
                {
                    return fromMine;
                }
                // 这份表没收录这张卡 → 落回全局，不给玩家看一个空名字。
            }
            Pack p = Current;
            string translated;
            if (p != null && !p.isNative && p.TryGet(id, nativeName, out translated))
            {
                return translated;
            }
            return nativeName;
        }

        // ---------------------------------------------------------------- 装载

        public static void Ensure()
        {
            if (inited)
            {
                return;
            }
            inited = true;
            Reload();
        }

        /// <summary>重扫 <c>translation/</c> 并重读设置（用于外部改了数据文件后刷新）。</summary>
        public static void Reload()
        {
            packs.Clear();
            packByKey.Clear();
            packs.Add(nativePack);
            packByKey[NativeKey] = nativePack;

            try
            {
                Directory.CreateDirectory(Folder);
            }
            catch (Exception)
            {
            }

            List<string> files = new List<string>();
            try
            {
                files.AddRange(Directory.GetFiles(Folder, "*.tsv"));
                files.AddRange(Directory.GetFiles(Folder, "*.cdb"));
            }
            catch (Exception)
            {
            }
            files.Sort(StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < files.Count; i++)
            {
                Pack p = null;
                try
                {
                    string ext = Path.GetExtension(files[i]).ToLowerInvariant();
                    if (ext == ".tsv")
                    {
                        p = LoadTsv(files[i]);
                    }
                    else if (ext == ".cdb")
                    {
                        p = LoadCdb(files[i]);
                    }
                }
                catch (Exception e)
                {
                    Program.DEBUGLOG(e);
                    p = null;
                }
                if (p == null || p.Count == 0)
                {
                    continue;
                }
                // 文件名命中了已知翻译表 → 收成规范 key；文件里没写 #label 就用规范名。
                KnownPack known = FindKnown(p.key);
                if (known != null)
                {
                    if (p.label == p.key)
                    {
                        p.label = known.label;
                    }
                    p.key = known.key;
                }
                if (packByKey.ContainsKey(p.key))
                {
                    continue;
                }
                packs.Add(p);
                packByKey[p.key] = p;
            }

            // 目录里还没有数据的已知翻译表也列出来（标 missing），让玩家看得见"该放什么文件"。
            for (int i = 0; i < KnownPacks.Length; i++)
            {
                KnownPack k = KnownPacks[i];
                if (packByKey.ContainsKey(k.key))
                {
                    continue;
                }
                Pack ph = new Pack();
                ph.key = k.key;
                ph.label = k.label;
                ph.source = "";
                ph.missing = true;
                packs.Add(ph);
                packByKey[ph.key] = ph;
            }

            LoadSettings();
            if (!packByKey.ContainsKey(currentKey))
            {
                currentKey = NativeKey;
            }
            Bump();
        }

        static Pack LoadTsv(string path)
        {
            Pack p = new Pack();
            p.key = Path.GetFileNameWithoutExtension(path);
            p.label = p.key;
            p.source = path;
            string[] lines = File.ReadAllLines(path, Encoding.UTF8);
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];
                if (line == null)
                {
                    continue;
                }
                line = line.TrimEnd('\r');
                if (line.Length == 0)
                {
                    continue;
                }
                if (line[0] == '#')
                {
                    // `#label xxx` = 给这一项起个人看的名字
                    string rest = line.Substring(1).Trim();
                    if (rest.StartsWith("label"))
                    {
                        string v = rest.Substring(5).Trim();
                        if (v.Length > 0)
                        {
                            p.label = v;
                        }
                    }
                    continue;
                }
                int tab = line.IndexOf('\t');
                if (tab <= 0)
                {
                    continue;
                }
                string left = line.Substring(0, tab).Trim();
                string right = line.Substring(tab + 1).Trim();
                if (left.Length == 0 || right.Length == 0)
                {
                    continue;
                }
                if (left[0] == '=')
                {
                    p.byName[left.Substring(1).Trim()] = right;
                }
                else
                {
                    int id;
                    if (int.TryParse(left, out id) && id > 0)
                    {
                        p.byId[id] = right;
                    }
                }
            }
            return p;
        }

        /// <summary>
        /// 把另一份 cards.cdb 当翻译表：按卡号取它的 name。
        /// 社区流传的 nw / cnocg / 简中 译名通常就是整份 cdb，丢进 translation/ 即可。
        /// </summary>
        static Pack LoadCdb(string path)
        {
            Pack p = new Pack();
            p.key = Path.GetFileNameWithoutExtension(path);
            p.label = p.key;
            p.source = path;
            using (Mono.Data.Sqlite.SqliteConnection cn =
                new Mono.Data.Sqlite.SqliteConnection("Data Source=" + path))
            {
                cn.Open();
                using (System.Data.IDbCommand cmd = new Mono.Data.Sqlite.SqliteCommand(
                    "SELECT id,name FROM texts", cn))
                {
                    using (System.Data.IDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            int id = (int)reader.GetInt64(0);
                            string name = reader.GetString(1);
                            if (id > 0 && !string.IsNullOrEmpty(name))
                            {
                                p.byId[id] = name;
                            }
                        }
                    }
                }
            }
            return p;
        }

        static void LoadSettings()
        {
            currentKey = NativeKey;
            overrides.Clear();
            cardPacks.Clear();
            fieldMaps.Clear();
            if (!File.Exists(SettingsPath))
            {
                return;
            }
            try
            {
                string[] lines = File.ReadAllLines(SettingsPath, Encoding.UTF8);
                for (int i = 0; i < lines.Length; i++)
                {
                    string line = (lines[i] ?? "").TrimEnd('\r');
                    if (line.Length == 0 || line[0] == '#')
                    {
                        continue;
                    }
                    string[] mats = line.Split('\t');
                    if (mats.Length >= 2 && mats[0] == "pack")
                    {
                        currentKey = mats[1].Trim();
                    }
                    else if (mats.Length >= 3 && mats[0] == "override")
                    {
                        int id;
                        if (int.TryParse(mats[1], out id) && mats[2].Trim().Length > 0)
                        {
                            overrides[id] = mats[2].Trim();
                        }
                    }
                    else if (mats.Length >= 3 && mats[0] == "cardpack")
                    {
                        // 单卡专用翻译：<卡号>\t<翻译表 key>
                        int id;
                        string key = mats[2].Trim();
                        if (int.TryParse(mats[1], out id) && key.Length > 0 && key != "global")
                        {
                            cardPacks[id] = key;
                        }
                    }
                    else if (mats.Length >= 4 && mats[0] == "field")
                    {
                        // 系列译名（需求 3）：<pack>\t<原生系列名>\t<译名> —— 按翻译表分家存。
                        string pk = mats[1].Trim();
                        string nf = mats[2].Trim();
                        string nv = mats[3].Trim();
                        if (pk.Length > 0 && nf.Length > 0 && nv.Length > 0)
                        {
                            FieldMapOf(pk)[nf] = nv;
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Program.DEBUGLOG(e);
            }
        }

        static void SaveSettings()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("# 卡名翻译设置（改完存盘即可；游戏内改也会写这里）\r\n");
            sb.Append("# pack    = 全局用哪一份翻译表\r\n");
            sb.Append("# override= <卡号>\\t<自定义名字>（单卡自定义，按异画组记）\r\n");
            sb.Append("# cardpack= <卡号>\\t<翻译表 key>（这张卡单独用哪一份，按异画组记）\r\n");
            sb.Append("# field    = <翻译表 key>\\t<原生系列名>\\t<译名>（系列译名，按翻译表分家存）\r\n");
            sb.Append("pack\t").Append(currentKey).Append("\r\n");
            List<int> ids = new List<int>(overrides.Keys);
            ids.Sort();
            for (int i = 0; i < ids.Count; i++)
            {
                sb.Append("override\t").Append(ids[i]).Append('\t')
                  .Append(overrides[ids[i]].Replace("\t", " ").Replace("\r", " ").Replace("\n", " "))
                  .Append("\r\n");
            }
            List<int> cids = new List<int>(cardPacks.Keys);
            cids.Sort();
            for (int i = 0; i < cids.Count; i++)
            {
                sb.Append("cardpack\t").Append(cids[i]).Append('\t').Append(cardPacks[cids[i]]).Append("\r\n");
            }
            // 字段译名：先按 pack 排、pack 内按原名排 —— 落盘顺序稳定，重存 md5 不漂。
            List<string> fkeys = new List<string>(fieldMaps.Keys);
            fkeys.Sort(StringComparer.Ordinal);
            for (int i = 0; i < fkeys.Count; i++)
            {
                Dictionary<string, string> fm = fieldMaps[fkeys[i]];
                if (fm == null || fm.Count == 0)
                {
                    continue;
                }
                List<string> fnames = new List<string>(fm.Keys);
                fnames.Sort(StringComparer.Ordinal);
                for (int j = 0; j < fnames.Count; j++)
                {
                    sb.Append("field\t").Append(fkeys[i]).Append('\t').Append(fnames[j]).Append('\t')
                      .Append(fm[fnames[j]].Replace("\t", " ").Replace("\r", " ").Replace("\n", " "))
                      .Append("\r\n");
                }
            }
            try
            {
                File.WriteAllText(SettingsPath, sb.ToString(), new UTF8Encoding(false));
            }
            catch (Exception e)
            {
                Program.DEBUGLOG(e);
            }
        }

        // ------------------------------------------------------- 描述文本重写

        /// <summary>
        /// 把描述里「卡名」两端的名字换成当前译名。
        /// 只认中文卡文用的「」引用（实测 17353 处引用里 17065 处能对上卡名/字段），
        /// 不做无差别全文替换 —— 那样 "英雄" 会把 "元素英雄" 也啃掉。
        /// </summary>
        public static string RewriteDesc(string nativeDesc, Dictionary<string, string> nameMap)
        {
            if (string.IsNullOrEmpty(nativeDesc) || nameMap == null || nameMap.Count == 0)
            {
                return nativeDesc;
            }
            if (nativeDesc.IndexOf('「') < 0)
            {
                return nativeDesc;
            }
            StringBuilder sb = null;
            // ⛔⛔ copied ≠ scan，两个指针各管一件事（2026-10-04 用户报障「简介缺一大段」的真凶）：
            //   scan   ＝ 下一轮从哪儿找「（只管找引用，不关心写没写过）
            //   copied ＝ 已经**真正写进 sb** 的位置（sb 还没建起来时为 0）
            // 原先两处都拿 scan 当"已写入位置"，而 sb 是懒创建的：若开头若干处「」都没命中
            // nameMap（sb 仍为 null、一个字没写），第 N 处第一次命中时执行的是
            //     sb.Append(nativeDesc, scan, open + 1 - scan)   ← scan 已经越过前 N-1 处
            // ⇒ **开头那段正文连同前几处「」整段消失**。实测全池 15017 张卡文里 482 张中招
            //（例：把自己墓地的「H-火热之心」「E-紧急呼唤」… → 只剩「改名E-紧急呼唤」…）。
            // 修法：sb 还没建起来时**不要动 copied**，让 prefix 留给第一次建 sb 时整段带出。
            int scan = 0;
            int copied = 0;
            while (true)
            {
                int open = nativeDesc.IndexOf('「', scan);
                if (open < 0)
                {
                    break;
                }
                int close = nativeDesc.IndexOf('」', open + 1);
                if (close < 0)
                {
                    break;
                }
                string inner = nativeDesc.Substring(open + 1, close - open - 1);
                string translated;
                if (nameMap.TryGetValue(inner, out translated) && !string.IsNullOrEmpty(translated))
                {
                    if (sb == null)
                    {
                        sb = new StringBuilder(nativeDesc.Length + 32);
                    }
                    sb.Append(nativeDesc, copied, open + 1 - copied);
                    sb.Append(translated);
                    sb.Append('」');
                    copied = close + 1;
                }
                else if (sb != null)
                {
                    sb.Append(nativeDesc, copied, close + 1 - copied);
                    copied = close + 1;
                }
                // else：sb 还没建 ⇒ copied 保持不动，prefix 留给下一次建 sb 时整段带出。
                scan = close + 1;
            }
            if (sb == null)
            {
                return nativeDesc;
            }
            if (copied < nativeDesc.Length)
            {
                sb.Append(nativeDesc, copied, nativeDesc.Length - copied);
            }
            return sb.ToString();
        }
    }
}
