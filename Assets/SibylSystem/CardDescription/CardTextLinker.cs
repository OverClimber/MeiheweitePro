using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using YGOSharp;
using YGOSharp.OCGWrapper.Enums;

/// <summary>
/// 卡牌描述富文本 —— 需求 1~4 的落地。
///
/// 干两件事：
///   ① **加链接**（<see cref="Linkify"/>）：把描述里能"点出东西来"的字包成 NGUI 的
///      <c>[url=...]</c>，并配一个可读的强调色；中文卡文里所有引用一律写作「卡名」，
///      所以识别口径就是「」里的内容 —— 实测本机 cdb 17353 处引用里 17065 处能对上卡名或字段。
///   ② **点得准**（<see cref="ResolveClick"/>）：点击命中直接交给 NGUI 的
///      <c>UILabel.GetUrlAtPosition</c>（它在未处理的原文 mText 上按 <c>LastIndexOf("[url=")</c> 反查）。
///      之所以能直接用，是因为 UILabel 处理文本时 <c>keepCharCount:true</c> —— 换行只替换行尾空格、
///      标签原样保留，processedText 与 mText **等长同下标**（读 NGUIText.WrapText 源码确认）。
///      另外再留一条自己扫 <c>[url=…]</c> 区间的兜底路径（<see cref="FindAt"/>），命中失败时用。
///
/// 链接种类（payload 一律避开 <c>]</c>，否则会咬断 NGUI 的标签）：
///   <c>c&lt;卡号&gt;</c>      卡名 → 打开这张卡的说明
///   <c>f&lt;文字&gt;</c>      字段 / 系列 → 检索该文字
///   <c>t&lt;主类&gt;:&lt;文字&gt;</c>  字段 + 种类 → 例如「英雄」怪兽 = 搜 英雄 且只留怪兽
///   <c>q&lt;文字&gt;</c>      兜底文字检索（含"卡名记述"短语）
///
/// 性能（需求 6）：<see cref="Linkify"/> 按卡号缓存，缓存只在
/// <see cref="CardsManager.NameStamp"/>（换翻译/换卡池）变化时整体作废；
/// 单次富化只扫一遍文本，不做跨卡池的正则匹配。点击时才走 <see cref="TryResolveClick"/>。
///
/// ⚠ 分成两个文件：本文件是**OCG 的「」口径**（引用一律写作「卡名」），
///   RD 那套**括号口径**（<c>怪兽（银河族）</c> / <c>传说卡</c>）在
///   <c>CardTextLinkerRd.cs</c>（同一个 partial 类，见 <c>LinkifyRd</c>）。
///   两边共用 <see cref="Link"/> / <see cref="Sanitize"/>，所以链接的标签写法只有一份。
/// </summary>
public static partial class CardTextLinker
{
    // ------------------------------------------------------------ 颜色 / 前缀

    const string ColorCard = "[FF6600]";     // 卡名
    const string ColorField = "[0070DD]";    // 字段/系列
    const string ColorPhrase = "[008080]";   // 整句（"有X的卡名记述"）

    public const char KindCard = 'c';
    public const char KindField = 'f';
    public const char KindTyped = 't';
    public const char KindQuery = 'q';

    /// <summary>
    /// <c>n&lt;掩码&gt;:&lt;卡名&gt;</c> —— 点的是**卡名**：只出「名字一模一样」的那些卡。
    ///
    /// <para><b>为什么不走 <see cref="KindQuery"/> 的全文检索</b>（用户 2026-10-03）：
    /// 全文检索是「卡名或卡文里**包含**这个串」，于是点「拉之翼神龙」会把
    /// 「拉之翼苏拉娜丝」「翼神龙…」「卡文里提到拉之翼神龙」的一堆卡全倒出来，
    /// 而用户要的是「就搜它自己」（同名异画的两张一起列全）。
    /// ⇒ 这一类必须**按 <c>Name</c> 精确相等**筛，而不是全文命中。</para>
    /// </summary>
    public const char KindName = 'n';

    /// <summary>
    /// <c>R&lt;掩码&gt;:&lt;卡名&gt;</c> —— 点的是「<c>X</c>」<b>的卡名记述</b>那半句：
    /// 只出「卡文里记述了 X」的那些卡。
    ///
    /// <para>同样不能走全文检索（用户 2026-10-03）：那会把「名字里带 X」的卡也算进来。
    /// 语义上这一类是**描述字段**上的包含，判据落在 <c>Card.Desc</c>。
    /// ⇒ 「本体没记述则本体都出不了」是这条语义的**自然结果**，不需要额外特判：
    /// 一张卡通常不会在自己的卡文里提到自己。</para>
    ///
    /// <para>⛔ 用大写 <c>R</c>：小写 <c>r</c> 已经 occupied 给 RD 的括号条件
    /// （<see cref="KindRd"/>，见 <c>CardTextLinkerRd</c>），撞了 switch 会编译不过。</para>
    /// </summary>
    public const char KindRecord = 'R';

    /// <summary>
    /// <c>u&lt;掩码&gt;:&lt;卡名&gt;</c> —— 点的是
    /// 「<c>X</c>」<b>或者有那个卡名记述的</b>「魔法·陷阱卡」这一整句（用户 2026-10-03：
    /// 「并集是形如『「头目连战」或者有那个卡名记述的魔法·陷阱卡』的情况」）。
    ///
    /// <para>这一句本身是<b>两者都要</b>：要么就是 X 这张卡，要么是「卡文里记述了 X」的
    /// 那种主类的卡。所以结果是<b>并集</b>（同名 ∪ 记述），不是交集 ——
    /// 交集在这里几乎恒为空（卡通常不会在自己的卡文里提到自己）。</para>
    ///
    /// <para>⛔ 掩码只作用在<b>记述那一支</b>：句子里「魔法·陷阱卡」修饰的是
    /// 「有那个卡名记述的」，不是「头目连战」那半截（后者本身是怪兽，被限成魔法·陷阱就
    /// 自己都出不来，那一眼就白链了）。</para>
    ///
    /// <para>⛔ 用 <c>u</c>：小写已 occupied 的有 c/f/t/q/r/n/R。</para>
    /// </summary>
    public const char KindUnion = 'u';

    /// <summary>
    /// <c>s&lt;系列hash&gt;:&lt;显示名&gt;</c> —— 简介末尾「系列：A|B」那行里的
    /// **单个系列名**（用户 2026-10-04：要能**分别**点）。
    ///
    /// <para><b>为什么不走全文检索（<see cref="KindQuery"/>）</b>：正文里的「系列名」是
    /// 引用，全文搜"提到它的卡"说得通；系列行是**这张卡自己的归属**，点它要的是
    /// 「同属这个系列的卡」—— 判据落在 <c>Card.Setcode</c> 的 16 位槽上，与
    /// <c>GameStringHelper.getSetName</c> 画这行用的判据**同一份**，显示什么就搜什么。</para>
    ///
    /// <para>⛔ 用 <c>s</c>：已 occupied 的有 c/f/t/q/n/R/u/r。</para>
    /// </summary>
    public const char KindSet = 's';

    /// <summary>
    /// 「字段」后面的种类词 —— 点它就等于"字段 + 这种类"。**长的排前面，先撞长词**。
    ///
    /// 第二列是**位掩码**（<see cref="CardType"/> 的十进制），检索时按
    /// <c>(card.Type &amp; mask) == mask</c> 逐位命中，所以"融合怪兽"能连"融合"这一位一起卡住：
    ///   怪兽 1 / 融合 0x41 / 同调 0x2001 / 超量 0x800001 / 连接 0x4000001 /
    ///   仪式 0x81 / 灵摆 0x1000001 / 魔法 2 / 陷阱 4
    /// 这样「英雄」融合怪兽点下去就**只**留融合英雄，而不是所有英雄怪兽。
    ///
    /// 组合种类（「太阳神」魔法·陷阱卡）由 <see cref="MatchTypeTail"/> 把多个词的掩码**按位或**起来
    /// （魔法 2 | 陷阱 4 = 6），所以那种情况不用在这里列条目。
    ///
    /// 末尾的「卡片 / 卡」掩码是 0 —— 它不带类型信息，只是**高亮范围内的一个词**
    /// （用户要求：把"卡"也一起高亮），掩码沿用前面已经累出来的那个。
    /// </summary>
    static readonly string[][] TypeSuffixes = new string[][]
    {
        new string[] { "融合怪兽", "65" },
        new string[] { "同调怪兽", "8193" },
        new string[] { "超量怪兽", "8388609" },
        new string[] { "连接怪兽", "67108865" },
        new string[] { "仪式怪兽", "129" },
        new string[] { "灵摆怪兽", "16777217" },
        new string[] { "怪兽", "1" },
        new string[] { "魔法卡", "2" },
        new string[] { "陷阱卡", "4" },
        new string[] { "魔法", "2" },
        new string[] { "陷阱", "4" },
        new string[] { "卡片", "0" },
        new string[] { "卡", "0" },
    };

    /// <summary>
    /// 从 <paramref name="at"/> 起吃掉一整个种类短语，掩码按位或累出来。
    /// 能吃下三种写法：
    ///   * 单个       「娱乐伙伴」怪兽
    ///   * 收尾带卡   「太阳神」魔法卡 / 「太阳神」卡
    ///   * **组合**   「太阳神」魔法·陷阱卡 / 魔法/陷阱 / 魔法、陷阱卡 / 魔法和陷阱
    /// 组合时掩码是 2|4=6，于是只留既是魔法又是陷阱的那种卡（双卡）。
    /// 返回 false = 这儿不是种类短语。
    /// </summary>
    static bool MatchTypeTail(string desc, int at, out string word, out int mask)
    {
        word = null;
        mask = 0;
        int len = MatchOneTypeWord(desc, at);
        if (len < 0)
        {
            return false;
        }
        int pos = at + len;
        mask = TypeMaskAt(desc, at, len);

        // 组合：<词> <连接符> <词> [<词> …]
        while (pos < desc.Length && IsTypeJoiner(desc[pos]))
        {
            int save = pos;
            pos++;
            int next = MatchOneTypeWord(desc, pos);
            if (next < 0)
            {
                pos = save;
                break;
            }
            mask |= TypeMaskAt(desc, pos, next);
            pos += next;
        }

        // 收尾的「卡片 / 卡」只算高亮范围，不改掩码。
        if (StartsWith(desc, pos, "卡片"))
        {
            pos += 2;
        }
        else if (pos < desc.Length && desc[pos] == '卡')
        {
            pos += 1;
        }

        if (pos <= at)
        {
            return false;
        }
        word = desc.Substring(at, pos - at);
        return true;
    }

    /// <summary>组合种类中间那些连接符：· ・ / ／ 、 ， 和 与 或 及。</summary>
    static bool IsTypeJoiner(char c)
    {
        return c == '·' || c == '・' || c == '/' || c == '／'
            || c == '、' || c == ',' || c == '，'
            || c == '和' || c == '与' || c == '或' || c == '及';
    }

    /// <summary>单个种类词的最长匹配长度，没有返回 -1。</summary>
    static int MatchOneTypeWord(string desc, int at)
    {
        for (int i = 0; i < TypeSuffixes.Length; i++)
        {
            if (StartsWith(desc, at, TypeSuffixes[i][0]))
            {
                return TypeSuffixes[i][0].Length;
            }
        }
        return -1;
    }

    static int TypeMaskAt(string desc, int at, int len)
    {
        for (int i = 0; i < TypeSuffixes.Length; i++)
        {
            if (TypeSuffixes[i][0].Length == len && StartsWith(desc, at, TypeSuffixes[i][0]))
            {
                int v;
                return int.TryParse(TypeSuffixes[i][1], out v) ? v : 0;
            }
        }
        return 0;
    }

    // ------------------------------------------------------------ 索引

    /// <summary>系列/字段表：来自 config/strings.conf 的 <c>!setname</c>（594 条）。</summary>
    static Dictionary<string, bool> fieldNames;

    /// <summary>
    /// 字段名 → <c>!setname</c> 的**表内码**（= <see cref="GameStringManager.hashedString.hashCode"/>，
    /// 如 融合 = 0x46 / 英雄 = 0x8）。
    ///
    /// <para>做「点字段 → 列出**真正属于这个系列**的卡」（用户 2026-10-04）要的就是它：
    /// 检索端拿这个码去问 <c>CardsManager.IfSetCard</c>（低 12 位=系列本体、高 4 位=子系列），
    /// 而不是拿名字去全文搜 —— 后者会把"卡文里只是提到它"的卡一起倒出来。</para>
    /// </summary>
    static Dictionary<string, int> fieldCodes;

    /// <summary>
    /// 表内码 → **原生系列名**（!setname 的第一个字段）。右键改系列译名时要用它：
    /// 落盘键就是原生名（见 <c>CardNameTranslation.SetFieldOverride</c>），
    /// 而链接 payload 里带的是码 —— 用码回查比"从显示名反推"稳（译名改过也不怕）。
    /// </summary>
    static Dictionary<int, string> fieldNatives;

    /// <summary>卡号 → 富化结果。换翻译（NameStamp 变）时整体丢弃。</summary>
    static readonly Dictionary<int, string> cache = new Dictionary<int, string>();
    static int cacheStamp = -1;

    static void EnsureFields()
    {
        if (fieldNames != null)
        {
            return;
        }
        fieldNames = new Dictionary<string, bool>();
        fieldCodes = new Dictionary<string, int>();
        fieldNatives = new Dictionary<int, string>();
        try
        {
            List<GameStringManager.hashedString> list = GameStringManager.xilies;
            for (int i = 0; i < list.Count; i++)
            {
                string content = list[i].content;
                if (string.IsNullOrEmpty(content))
                {
                    continue;
                }
                int tab = content.IndexOf('\t');
                string name = tab > 0 ? content.Substring(0, tab) : content;
                name = name.Trim();
                if (name.Length > 0 && !fieldNames.ContainsKey(name))
                {
                    fieldNames[name] = true;
                    // 同一个名字若在表里出现多次（主子系列重名之类），**留第一个**：
                    // IfSetCard 的低 12 位口径下，取更"本体"的那个更安全。
                    fieldCodes[name] = list[i].hashCode;
                    if (!fieldNatives.ContainsKey(list[i].hashCode))
                    {
                        fieldNatives[list[i].hashCode] = name;
                    }
                }
            }
        }
        catch (Exception e)
        {
            Program.DEBUGLOG(e);
        }
    }

    /// <summary>
    /// 系列名 → <c>!setname</c> 表内码；认不出来返回 -1（调用方退回全文检索）。
    ///
    /// <para>两层查法：① 直接查表（卡文里写的是原生系列名，绝大多数走这一层）；
    /// ② 查不到时把名字当**译名**反查回原生名再查 —— 玩家手改过系列译名时，
    /// 卡文里的引用会跟着换成显示名（见 CardNameTranslation 的 desc 重写）。</para>
    /// </summary>
    public static int FieldCodeOf(string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return -1;
        }
        EnsureFields();
        int code;
        if (fieldCodes != null && fieldCodes.TryGetValue(name, out code))
        {
            return code;
        }
        try
        {
            Dictionary<string, string> m = YGOSharp.CardNameTranslation.FieldMapForCurrentPack();
            if (m != null)
            {
                foreach (KeyValuePair<string, string> kv in m)
                {
                    if (kv.Value == name && fieldCodes != null
                        && fieldCodes.TryGetValue(kv.Key, out code))
                    {
                        return code;
                    }
                }
            }
        }
        catch (Exception)
        {
        }
        return -1;
    }

    /// <summary>表内码 → 原生系列名（认不出返回 null）。右键改系列译名用。</summary>
    public static string FieldNativeOf(int code)
    {
        EnsureFields();
        string name;
        if (fieldNatives != null && fieldNatives.TryGetValue(code, out name))
        {
            return name;
        }
        return null;
    }

    /// <summary>strings.conf 换了（数据更新）之后要把字段表丢掉重读。</summary>
    public static void InvalidateFields()
    {
        fieldNames = null;
        fieldCodes = null;
        fieldNatives = null;
    }

    // ------------------------------------------------------------ 富化

    /// <summary>
    /// 把一段卡牌描述富化成可点击的 NGUI 富文本。<paramref name="cacheKey"/> 用卡号，
    /// 同一张卡重复显示时直接命中缓存。
    /// </summary>
    public static string Linkify(string desc, int cacheKey)
    {
        if (string.IsNullOrEmpty(desc))
        {
            return desc;
        }
        EnsureFields();
        int stamp = CardsManager.NameStamp;
        if (stamp != cacheStamp)
        {
            cache.Clear();
            cacheStamp = stamp;
        }
        string cached;
        if (cacheKey != 0 && cache.TryGetValue(cacheKey, out cached))
        {
            return cached;
        }
        string result = Build(desc);
        // RD 再过一遍**括号口径**（「」那套跑完才做）——见 CardTextLinkerRd.cs。
        // 两趟的识别记号不重叠（「」 vs （）/传说卡），所以可以各扫各的、不用做上下文协商；
        // LinkifyRd 内部会把已经生成的 [url=…] 区间整段跳过，不会互相咬。
        // OCG 下 LinkifyRd 原样返回（它在第一行就按 GameModeManager.IsRD 短路）。
        result = LinkifyRd(result);
        if (cacheKey != 0)
        {
            if (cache.Count > 1024)
            {
                cache.Clear();
            }
            cache[cacheKey] = result;
        }
        return result;
    }

    static string Build(string desc)
    {
        Dictionary<string, int> names = null;
        try
        {
            names = CardsManager.DisplayNameIndex;
        }
        catch (Exception e)
        {
            Program.DEBUGLOG(e);
        }

        StringBuilder sb = new StringBuilder(desc.Length + 96);
        int scan = 0;
        while (true)
        {
            int open = desc.IndexOf('「', scan);
            if (open < 0)
            {
                break;
            }
            int close = desc.IndexOf('」', open + 1);
            if (close < 0)
            {
                break;
            }
            string inner = desc.Substring(open + 1, close - open - 1);

            // 「X」前面是"有"、后面跟"的卡名记述 / 卡的卡名记述"？那整句都做成链接（需求 4）。
            int phraseStart = (open > scan && desc[open - 1] == '有') ? open - 1 : -1;
            string phraseTail = null;
            if (phraseStart >= 0)
            {
                phraseTail = MatchRecordTail(desc, close + 1);
            }

            sb.Append(desc, scan, phraseStart >= 0 ? phraseStart - scan : open - scan);
            if (phraseStart >= 0)
            {
                sb.Append('有');
            }

            bool linked = false;
            int cardId = 0;
            bool isCardName = names != null && names.TryGetValue(inner, out cardId);
            bool isField = IsLinkableField(inner);

            // 🔑 **既是卡名又是字段**（如「融合」：卡 24094653 叫这个名字，
            //   `!setname 0x46` 也是这个系列的字段）——用户 2026-10-04 口径：
            //   在「使用它当字段」的场合（后面跟种类词，如「融合」魔法卡）**两段要分开**：
            //   点内圈「融合」= 当**卡名**（那张卡），点外圈「魔法卡」= 当**字段**（这个系列）。
            //   其余情况（字段+种类词、卡名+种类词）整段是**一个**选中。
            bool ambiguous = isCardName && isField;

            // 先看后面紧跟着的是不是种类短语（需求 4）：「娱乐伙伴」怪兽 要当成**一个整体**，
            // 点前半的「娱乐伙伴」和点后半的「怪兽」都只给"娱乐伙伴怪兽"，而不是点前半
            // 把整个系列（魔法/陷阱/…）都倒出来。所以掩码要先拿到手，再决定前半怎么链。
            // 短语可以是组合的：「太阳神」魔法·陷阱卡 → 掩码 2|4=6（双卡）。
            string tailWord = null;
            int tailMask = 0;
            bool hasTail = false;
            if (isCardName || isField)
            {
                hasTail = MatchTypeTail(desc, close + 1, out tailWord, out tailMask);
            }

            // 🔑 并集形态（用户 2026-10-03）：「X」**或者/以及**有那个卡名记述的<种类词>
            //   例：「炎之剑士」或者有那个卡名记述的怪兽
            // 这一整句点一下 = X 这张卡 **或** 卡文里记述了 X 的<种类词>卡（并集）。
            // ⛔ 与上面 hasTail 互斥：那种句式里 close+1 处是「或者…」，MatchTypeTail
            //   不会命中，所以 hasTail 必为 false，两者不会同时发链接。
            int orRecTail = isCardName ? MatchOrRecordConnector(desc, close + 1) : -1;

            // 🔑 「整段当一个选中」的条件（用户 2026-10-04 第二条）：
            //   有种类尾词，且**不属于**必须分两段的三种 ——
            //     · <see cref="ambiguous"/>：「X」既是卡名又是字段（内圈当卡名、外圈当字段）；
            //     · 并集句（「X」或者有那个卡名记述的…）—— 尾词那截是另一个链接；
            //     · 记述句（有「X」的卡名记述）—— 同上。
            //   满足时「X」+ 种类词裹进**同一个** [url=] 区间 ⇒ 悬停/下划线连成一片。
            bool mergeTail = hasTail && !ambiguous && orRecTail < 0 && phraseTail == null;
            bool tailConsumed = false;

            // payload 统一是 <掩码>:<文字>（字段那一支多一段表内码，见下），
            // 掩码 0 = 不限主类（就是这个系列/这个名字的全部卡）。
            //
            // ⛔ 历史教训（2026-10-03 用户报障）：前半与后半**档位写岔**过 ——
            //   字段（「黑魔导」那种本池没有同名卡的系列名）被当成"卡名精确相等"，
            //   于是点「黑魔导」是系列检索、点括号外的「怪兽」却是找卡名。
            //   2026-10-04 起形状改成：**合并**时两半本来就是一个 payload（天然一致）；
            //   **分开**时（ambiguous）后半固定走字段口径，见下。
            // 🔑 字段那一支的 payload（用户 2026-10-04 第二条）：**能查到表内码就用系列成员判据**
            //   （`s<掩码>:<表内码>:<名>` → 检索端问 CardsManager.IfSetCard），
            //   而不是拿名字全文搜 —— 后者会把"卡文里只是提到它"的卡一起倒出来
            //   （用户原话：「融合魔法卡会搜出来简介带融合」）。查不到就退回全文检索。
            int fieldCode = isField ? FieldCodeOf(inner) : -1;
            char fieldKind = fieldCode >= 0 ? KindSet : KindQuery;
            string fieldMask = (hasTail ? tailMask : 0).ToString();
            // 🔑 **被点处的原文**（用户 2026-10-04 第三条）：检索窗标题要照抄它 + 结果数，
            //   不要"加工一下"（原来是「融合（魔法）」这种重新拼的写法）。
            //   括号与尾词都照卡面上显示的样子带进来，两半（分段时）取同一个短语。
            string segTitle = "「" + inner + "」" + (hasTail ? tailWord : "");
            string fieldPl = fieldCode >= 0
                ? (fieldMask + ":" + fieldCode + ":" + segTitle)
                : (fieldMask + ":" + inner);
            if (isCardName)
            {
                // 需求（2026-10-02）：**卡名也不再"点了直接切面板"**。
                // 和种类词一个待遇：把名字送进检索栏，由玩家在结果列表里挑。
                // 好处一：同名异画的两张卡会一起列出来（直接切面板只能随便挑一张）。
                // 好处二：名字留在输入框里，可以顺手改宽/加限定再搜。
                // 「」两头的括号现在**进链接**（一起画下划线）但**不吃卡名色**（不再高亮）。
                // ⛔ 2026-10-03：改走 <see cref="KindName"/>（**按卡名精确相等**筛），
                //   不再走全文检索 —— 后者会把「名字里包含它」的一堆卡一起倒出来。
                // ⛔ ambiguous 时内圈**不带主类掩码**：尾词限的是"这个字段的这张类卡"，
                //   而"当卡名"要的是那张卡本身 —— 带了掩码，遇到"卡名那张卡的主类与
                //   尾词不符"（如「融合」魔法卡 里 融合 确实是魔法，但换成
                //   「XYZ」怪兽 而 XYZ 是魔法卡）就会一张都搜不出来。
                string pl = (ambiguous ? "0:" : tailMask + ":") + inner + ":" + segTitle;
                sb.Append(mergeTail
                    ? LinkBracketed(KindName, pl, ColorCard, "「", inner, "」", "", tailWord)
                    : LinkBracketed(KindName, pl, ColorCard, "「", inner, "」"));
                if (mergeTail)
                {
                    tailConsumed = true;
                }
                linked = true;
            }
            else if (isField)
            {
                // 系列/字段：不带种类限定 = 这个系列的**全部卡**（「娱乐伙伴」→ 所有娱乐伙伴）。
                // ⛔ 字段**不能**走「按卡名精确相等」（会把整个系列筛没）——
                //   能查到表内码时走系列成员判据（KindSet），查不到才退全文检索（KindQuery）。
                string pl = fieldPl;
                sb.Append(mergeTail
                    ? LinkBracketed(fieldKind, pl, ColorField, "「", inner, "」", "", tailWord)
                    : LinkBracketed(fieldKind, pl, ColorField, "「", inner, "」"));
                if (mergeTail)
                {
                    tailConsumed = true;
                }
                linked = true;
            }
            else
            {
                sb.Append('「').Append(inner).Append('」');
            }

            int next = close + 1;

            if (orRecTail >= 0)
            {
                // 连「连接词 + 记述短语 + 种类词」整段做成**一个**链接（点哪都一样），
                // 掩码取自种类词；没有种类词就退化成不限主类的并集。
                int orRecMask;
                string orRecWord;
                if (MatchTypeTail(desc, orRecTail, out orRecWord, out orRecMask))
                {
                    sb.Append(Link(KindUnion, orRecMask + ":" + inner, ColorPhrase,
                        desc.Substring(close + 1, orRecTail - (close + 1) + orRecWord.Length)));
                    next = orRecTail + orRecWord.Length;
                }
                else
                {
                    int end = orRecTail;
                    sb.Append(Link(KindUnion, "0:" + inner, ColorPhrase,
                        desc.Substring(close + 1, end - (close + 1))));
                    next = end;
                }
            }
            else if (phraseTail != null)
            {
                // 整句可点：**只出「卡文里记述了 X」的那些卡**（用户 2026-10-03）。
                // ⛔ 原来这里把尾巴文字也拼进检索词（`X」的卡名记述`）去全文搜 ——
                //   那串字面量在卡文里根本不存在，搜出来的是一堆无关卡。
                // 现在 payload 只带卡名 X，判据由 <see cref="KindRecord"/> 落到 Card.Desc 上。
                sb.Append(Link(KindRecord, "0:" + inner, ColorPhrase, phraseTail));
                next = close + 1 + phraseTail.Length;
            }
            else if (hasTail)
            {
                if (!tailConsumed)
                {
                    // 分两段的场合（用户 2026-10-04 第一条）：外圈的种类词按**字段**口径 ——
                    // 「融合」魔法卡 里点「魔法卡」要的是"**属于**融合系列、且是魔法"的卡，
                    // 而不是"按卡名精确等于 融合"（那是内圈那一击的活）、也不是"卡文提到融合"。
                    sb.Append(Link(fieldKind, fieldPl, ColorField, tailWord));
                }
                next += tailWord.Length;
            }

            scan = next;
        }
        if (scan < desc.Length)
        {
            sb.Append(desc, scan, desc.Length - scan);
        }
        return sb.ToString();
    }

    /// <summary>
    /// 把简介的**系列行**（<c>GameStringHelper.getSmall</c> 末尾「系列：A|B」）里的
    /// 每个系列名做成**单独可点**的链接（用户 2026-10-04：「卡的简介里的系列也可以
    /// 分别点击来触发关联卡」）。
    ///
    /// <para>做法：只动「系列：」**之后**到行尾那一小段（那是 getSmall 的最后一段，
    /// 后面只剩收色的 <c>[-]</c>），按 <c>getSetNamePairs</c> 的成对读数重拼 ——
    /// 显示字照旧（含译名），payload 换成表内 hash，点下去按系列精确检索
    /// （<see cref="KindSet"/>）。其余部分（卡名/种族/属性/攻守）**一个字节不动**。</para>
    ///
    /// <para>不做缓存：只在 <c>CardDescription.apply</c> 里对带系列的卡各跑一次，
    /// 开销就是几百次整数比对；换来的是译名一换、下一帧显示的就是新字。</para>
    /// </summary>
    public static string LinkifySeriesLine(string small, ulong setcode)
    {
        if (string.IsNullOrEmpty(small) || setcode == 0)
        {
            return small;
        }
        List<GameStringHelper.SetNamePair> pairs;
        try
        {
            pairs = GameStringHelper.getSetNamePairs(setcode);
        }
        catch (Exception e)
        {
            Program.DEBUGLOG(e);
            return small;
        }
        if (pairs == null || pairs.Count == 0)
        {
            return small;
        }
        string head = GameStringHelper.xilie;
        int xi = head.Length > 0 ? small.IndexOf(head, StringComparison.Ordinal) : -1;
        if (xi < 0)
        {
            return small;
        }
        int namesStart = xi + head.Length;
        // 行尾若是收色的 [-]（getSmall 末尾统一补的），摘出来放到链接后面。
        string tail = "";
        int namesEnd = small.Length;
        if (namesEnd >= 3 && small.EndsWith("[-]", StringComparison.Ordinal))
        {
            namesEnd -= 3;
            tail = "[-]";
        }
        if (namesStart > namesEnd)
        {
            return small;
        }
        StringBuilder sb = new StringBuilder(small.Length + pairs.Count * 40);
        sb.Append(small, 0, namesStart);
        for (int i = 0; i < pairs.Count; i++)
        {
            if (i > 0)
            {
                sb.Append('|');
            }
            string disp = !string.IsNullOrEmpty(pairs[i].display)
                ? pairs[i].display : pairs[i].native;
            if (string.IsNullOrEmpty(disp))
            {
                continue;
            }
            sb.Append(Link(KindSet, "0:" + pairs[i].hash + ":" + disp, ColorField, disp));
        }
        sb.Append(tail);
        return sb.ToString();
    }

    /// <summary>「」之后的「或者有那个卡名记述的」那整段（并集形态的连接词）。</summary>
    const string OrRecordClause = "有那个卡名记述的";

    /// <summary>
    /// 认「<c>」之后紧跟『或者有那个卡名记述的』/『以及有那个卡名记述的』」这种<b>并集</b>形态
    /// （用户 2026-10-03，例：「炎之剑士」或者有那个卡名记述的怪兽 /
    /// 「古代的机械巨人」或者1张有那个卡名记述的魔法·陷阱卡 / 「星尘龙」以及有那个卡名记述的同调怪兽）。
    ///
    /// <para><b>返回</b>：种类词应当开始的位置（= 连接词 + 那段记述短语之后）；不匹配返回 -1。
    /// 判据刻意收得紧：①「」之后 8 字之内必须出现 <see cref="OrRecordClause"/>；
    /// ② 这 8 字里必须含「或者」或「以及」。
    /// 少了 ② 就会把「只有那个卡名记述的」之类也当成并集。
    /// ⚠ ② 查的是**这段里含不含**，不是「紧贴短语前那两个字」——
    ///   真卡文里有「或者<b>1张</b>有那个卡名记述的」这种带数量字的（连接词与短语不相邻），
    ///   按「紧贴前两字」判会漏掉它（第一版就漏了，`nameshape` 那档自检当场报 XX）。</para>
    /// </summary>
    static int MatchOrRecordConnector(string desc, int at)
    {
        if (at < 0 || at >= desc.Length)
        {
            return -1;
        }
        for (int k = 1; k <= 8 && at + k < desc.Length; k++)
        {
            if (!StartsWith(desc, at + k, OrRecordClause))
            {
                continue;
            }
            string con = desc.Substring(at, k);
            if (con.IndexOf("或者", StringComparison.Ordinal) < 0
                && con.IndexOf("以及", StringComparison.Ordinal) < 0)
            {
                continue;
            }
            return at + k + OrRecordClause.Length;
        }
        return -1;
    }

    /// <summary>"的卡名记述" / "卡的卡名记述" —— 返回尾巴原文（用于拼检索词），没有返回 null。</summary>
    static string MatchRecordTail(string desc, int at)
    {
        const string a = "的卡名记述";
        const string b = "卡的卡名记述";
        if (StartsWith(desc, at, b))
        {
            return b;
        }
        if (StartsWith(desc, at, a))
        {
            return a;
        }
        return null;
    }

    static bool StartsWith(string text, int at, string word)
    {
        if (at < 0 || at + word.Length > text.Length)
        {
            return false;
        }
        for (int i = 0; i < word.Length; i++)
        {
            if (text[at + i] != word[i])
            {
                return false;
            }
        }
        return true;
    }

    /// <summary>
    /// 「」里不是卡名时还算不算"字段"：认得出来的系列名照收；
    /// 认不出来的短词（≤8 字、不含句读）也收 —— 本机实测这类 288 处里，
    /// 绝大多数是没进 setname 的系列（水晶机巧 / 文具电子人 / 红莲魔…）与"光/地/水/炎/风"这种宣言词，
    /// 而整句效果文（长、带标点）会被长度与标点两道闸挡在外面。
    /// </summary>
    /// <summary>验收/探针用：这个字串是不是 !setname 表里的字段名（<see cref="IsLinkableField"/> 的公开壳）。</summary>
    public static bool IsFieldName(string inner)
    {
        return IsLinkableField(inner);
    }

    static bool IsLinkableField(string inner)
    {
        if (string.IsNullOrEmpty(inner))
        {
            return false;
        }
        if (fieldNames != null && fieldNames.ContainsKey(inner))
        {
            return true;
        }
        if (inner.Length > 8)
        {
            return false;
        }
        for (int i = 0; i < inner.Length; i++)
        {
            char ch = inner[i];
            if (ch == '，' || ch == '。' || ch == '、' || ch == '；' || ch == '：'
                || ch == '！' || ch == '？' || ch == '\n' || ch == ' ' || ch == '·')
            {
                return false;
            }
        }
        return true;
    }

    static string Link(char kind, string payload, string color, string text)
    {
        StringBuilder sb = new StringBuilder(text.Length + payload.Length + 32);
        sb.Append("[url=").Append(kind).Append(Sanitize(payload)).Append(']');
        sb.Append(color).Append("[u]").Append(text).Append("[/u][-]");
        sb.Append("[/url]");
        return sb.ToString();
    }

    /// <summary>
    /// 带括号的链接（用户 2026-10-02 第三轮口径）：
    /// **括号跟着一起画下划线，但不吃高亮色**，好跟"正文里本来就有的一对括号"区分开。
    ///
    /// 生成的是 <c>[url=…][色][u][-]括号[色]内容[-]括号[/u][/url]</c> ——
    /// <c>[u]</c> 罩住整段（含两头的括号），颜色只罩住"头 + 内容"两截，括号留在正文色上。
    /// 但**下划线整条是链接色**（`[色]` 先上、`[u]` 记色、`[-]` 才退色），见下面的实现注释。
    ///
    /// ⛔ 为什么能这么套：NGUI 里 <c>[u]</c> 是**独立的开关**（<c>NGUIText.ParseSymbol</c> 里
    ///    <c>underline = true/false</c>），<c>[-]</c> **只动颜色栈**（<c>colors.RemoveAt</c>），
    ///    两者互不干扰；下划线是逐字画的（<c>Print</c> 里按 <c>_</c> 字形拉一条），
    ///    所以整段连成一条线、颜色各段自理。
    /// </summary>
    static string LinkBracketed(char kind, string payload, string color,
        string open, string inner, string close)
    {
        return LinkBracketed(kind, payload, color, open, inner, close, "");
    }

    /// <summary>
    /// 同上，多一个 <paramref name="head"/>：括号**前面**那截也要高亮的部分
    /// （RD 的 <c>怪兽（银河族）</c> ——「怪兽」是主类词，跟着一起高亮；两头的括号不跟）。
    /// </summary>
    static string LinkBracketed(char kind, string payload, string color,
        string open, string inner, string close, string head)
    {
        return LinkBracketed(kind, payload, color, open, inner, close, head, "");
    }

    /// <summary>
    /// 同上，多一个 <paramref name="head"/>（括号**前**那截也高亮）与
    /// <paramref name="after"/>（括号**后**那截种类词也进同一段）。
    ///
    /// <para><paramref name="after"/> 是用户 2026-10-04 第二条要的「整段当一个选中」：
    /// 「英雄」怪兽 这种（字段+种类词）要把括号后的「怪兽」也裹进同一个
    /// <c>[url=…]</c> 区间 —— 悬停高亮/下划线连成一片，点哪都同一个动作。
    /// 必须分两段的三种情况（既是卡名又是字段 / 并集句 / 记述句）不走这条路。</para>
    /// </summary>
    static string LinkBracketed(char kind, string payload, string color,
        string open, string inner, string close, string head, string after)
    {
        StringBuilder sb = new StringBuilder(inner.Length + payload.Length + 96);
        sb.Append("[url=").Append(kind).Append(Sanitize(payload)).Append(']');
        // ⛔ 顺序是刻意排的：**先上色 → 再开 [u] → 立刻 [-] 退色**。
        //   `[u]` 会把"这一刻的字色"记成**下划线的颜色**（见 NGUIText 里 mUnderlineColor），
        //   紧接着 `[-]` 又把字色退回正文色 ⇒ 括号**字**不上色、脚下的**线**仍是链接色，
        //   正好对上用户 2026-10-03「下划线的颜色要是一样的，括号处别掉色」。
        sb.Append(color).Append("[u][-]");
        if (!string.IsNullOrEmpty(head))
        {
            sb.Append(color).Append(head).Append("[-]");
        }
        sb.Append(open);
        sb.Append(color).Append(inner).Append("[-]");
        sb.Append(close);
        if (!string.IsNullOrEmpty(after))
        {
            // 尾词属于这一段的**正文**（不再是括号外的独立链接）⇒ 跟着吃链接色 + 下划线。
            sb.Append(color).Append(after).Append("[-]");
        }
        sb.Append("[/u]");
        sb.Append("[/url]");
        return sb.ToString();
    }

    /// <summary>payload 里不能出现 <c>]</c>，否则会咬断 NGUI 的 url 标签。</summary>
    static string Sanitize(string payload)
    {
        if (payload == null)
        {
            return "";
        }
        if (payload.IndexOf(']') < 0)
        {
            return payload;
        }
        return payload.Replace("]", "");
    }

    // ------------------------------------------------------------ 点击

    // ------------------------------------------------------------ 验收自检

    /// <summary>
    /// 「系列 + 种类」当作**一个整体**的自检（需求 4）。
    ///
    /// 拿真卡文里现成的「娱乐伙伴」怪兽 举例：
    ///   * 前半的「娱乐伙伴」与后半的「怪兽」必须落到**同一个** payload（t + 怪兽掩码 + 关键字），
    ///     —— 点哪一半都只给"娱乐伙伴怪兽"，不是点前半就把整个系列都倒出来；
    ///   * 不带种类词时（「娱乐伙伴」…）前半仍是 t+无掩码 的字段检索 = 这个系列的**全部**卡；
    ///   * 真·卡名（「青眼白龙」怪兽）点前半仍然是"看这张卡"，不被种类词改道。
    /// 判据全是可以离线核的数：同一段富化文本里 t1: 这个 payload 出现两次。
    /// </summary>
    static void WholeUnitSelfTest()
    {
        const string probeDesc =
            "以「娱乐伙伴」怪兽的攻击力为基准。「娱乐伙伴」以外的卡不受影响。"
            + "「太阳神」魔法·陷阱卡的其中一张。「星杯」卡片也能用。「青眼白龙」很强。";
        try
        {
            string rich = Linkify(probeDesc, 0);
            QuickTestTrace.Log("link", "wholeunit rich=" + rich);
            // ⚠ 2026-10-04 起口径变了两次：① 有种类尾词的整段当一个链接（不再发两颗）；
            //   ② 字段那一支的 payload 从 `q<掩码>:<名>` 换成 `s<掩码>:<表内码>:<名>`
            //      （按系列成员检索，不再全文搜）。所以下面按"名字+掩码"数，不数整串 payload。
            QuickTestTrace.Log("link", "wholeunit typedMonster=" + CountFieldTag(rich, "娱乐伙伴", 1)
                + " (期望 1 = 「娱乐伙伴」怪兽 整段一个链接，掩码 1)"
                + " plainField=" + CountFieldTag(rich, "娱乐伙伴", 0)
                + " (期望 1 = 第二处不带种类限定的那次，掩码 0)"
                + " mixedSpellTrap=" + CountFieldTag(rich, "太阳神", 6)
                + " (期望 1 = 整段一个链接，掩码 6)"
                + " cardTail=" + CountFieldTag(rich, "星杯", 0)
                + " (期望 1 = 「星杯」卡片 整段一个链接)"
                // 卡名也走检索栏（不再是直接切面板）；⛔ 2026-10-03 起走 KindName(`n`)
                + " cardNameQuery=" + CountOccur(rich, "[url=n0:青眼白龙]")
                + " (期望 1 = 卡名进检索栏，掩码 0)"
                + " fieldSet=" + CountOccur(rich, "[url=" + KindSet)
                + " (期望 3 = 三处字段链接全走 KindSet)");
        }
        catch (Exception e)
        {
            QuickTestTrace.Log("link", "wholeunit failed " + e.Message);
        }
    }

    /// <summary>
    /// 数「名 = <paramref name="field"/>、掩码 = <paramref name="mask"/>」的**字段**链接。
    ///
    /// <para>⛔ 别去比对 payload 的字面形状：2026-10-04 起第三段是**被点处原文**
    /// （<c>「融合」魔法卡</c>），不再是裸名字。正确口径是"按 payload 解析 ⇒ 表内码回查原生名"，
    /// 与检索端同一套（<see cref="ParseSet"/> + <see cref="FieldNativeOf"/>）。</para>
    /// </summary>
    static int CountFieldTag(string rich, string field, int mask)
    {
        int n = 0;
        int at = 0;
        string head = "[url=" + KindSet;
        while (true)
        {
            int i = rich.IndexOf(head, at, StringComparison.Ordinal);
            if (i < 0)
            {
                break;
            }
            int e = rich.IndexOf(']', i);
            if (e > i)
            {
                string pl = rich.Substring(i + 5, e - i - 5);   // 含种类字符
                int m;
                int c;
                string ttl;
                if (ParseSet(pl.Substring(1), out m, out c, out ttl)
                    && m == mask && FieldNativeOf(c) == field)
                {
                    n++;
                }
            }
            at = i + 1;
        }
        return n;
    }

    /// <summary>
    /// 「既是卡名又是字段」那一条口径的自检（用户 2026-10-04 第一条）。
    ///
    /// <para>样本用**真数据**里現成的：「融合」既是卡名（24094653 那几张）又是
    /// <c>!setname 0x46</c> 的字段；卡文里就写着「融合」魔法卡（如 同调融合者 15839054）。
    /// 期望：
    /// <list type="bullet">
    /// <item><b>分开</b>（mixed）：内圈 <c>[url=n0:融合]</c>（当卡名，**不带掩码**）+
    ///   外圈 <c>[url=q2:融合]</c>（当字段，掩码=魔法 2）—— 两个 payload 不同。</item>
    /// <item><b>合并</b>（merge）：「英雄」怪兽（英雄只是字段、不是卡名）整段只有
    ///   **一个** <c>[url=…]</c>，且尾词「怪兽」在 <c>[/url]</c> **里面**。</item>
    /// </list></para>
    /// </summary>
    static void SegmentSelfTest()
    {
        try
        {
            const string mixedDesc = "①：以自己墓地1张「融合」魔法卡为对象才能发动。";
            string mr = Linkify(mixedDesc, 0);
            // ⚠ 内圈 payload 2026-10-04 起是 <掩码>:<卡名>:<被点处原文> ⇒ 只会出现
            //   `[url=n0:融合:` 这个**前缀**（后面还有标题），不能再按整串比。
            int innerN = CountOccur(mr, "[url=" + KindName + "0:融合:");
            int tailQ = CountFieldTag(mr, "融合", 2);
            int tailCode = FieldCodeOf("融合");
            bool split = innerN == 1 && tailQ == 1;
            QuickTestTrace.Log("link", "segsplit mixed=[融合] innerCard=" + innerN
                + " tailField=" + tailQ
                + " innerKind=" + KindName + " tailKind=" + (tailCode >= 0 ? KindSet : KindQuery)
                + " code=0x" + tailCode.ToString("x")
                + " isCardName=" + CardsManager.DisplayNameIndex.ContainsKey("融合")
                + " isField=" + IsFieldName("融合")
                + " ok=" + (split ? 1 : 0));
            if (!split)
            {
                QuickTestTrace.Log("link", "segsplit rich=" + mr);
            }

            const string mergeDesc = "「英雄」怪兽2只。";
            string gr = Linkify(mergeDesc, 0);
            int urls = CountOccur(gr, "[url=");
            bool tailInside = gr.IndexOf("怪兽[-][/u][/url]", StringComparison.Ordinal) >= 0;
            int heroCode = FieldCodeOf("英雄");
            bool merge = urls == 1 && tailInside && heroCode >= 0
                && CountFieldTag(gr, "英雄", 1) == 1;
            QuickTestTrace.Log("link", "segmerge field=[英雄] urls=" + urls
                + " tailInside=" + (tailInside ? 1 : 0)
                + " code=0x" + heroCode.ToString("x")
                + " setTag=" + CountFieldTag(gr, "英雄", 1)
                + " isField=" + IsFieldName("英雄")
                + " isCardName=" + CardsManager.DisplayNameIndex.ContainsKey("英雄")
                + " ok=" + (merge ? 1 : 0));
            if (!merge)
            {
                QuickTestTrace.Log("link", "segmerge rich=" + gr);
            }
        }
        catch (Exception e)
        {
            QuickTestTrace.Log("link", "segsplit failed " + e.Message);
        }
    }

    // ---------------------------------------------------------------- 全池「不许丢字」自检

    /// <summary>
    /// 剥掉 NGUI 标记、只留**可见文字**（自检用）。卡文本身不含任何 <c>[…]</c>，
    /// 所以「富化输出剥完」必须与原文逐字相等 —— 这是"简介有没有缺字"最直接的判据。
    /// </summary>
    static string VisibleText(string s)
    {
        if (string.IsNullOrEmpty(s))
        {
            return "";
        }
        StringBuilder sb = new StringBuilder(s.Length);
        int i = 0;
        while (i < s.Length)
        {
            if (s[i] == '[')
            {
                int e = s.IndexOf(']', i + 1);
                if (e > i)
                {
                    i = e + 1;      // 整个 [xxx] 丢掉
                    continue;
                }
            }
            sb.Append(s[i]);
            i++;
        }
        return sb.ToString();
    }

    /// <summary>两串第一处不同的下标；完全相同返回 -1。</summary>
    static int FirstDiff(string a, string b)
    {
        int n = Math.Min(a.Length, b.Length);
        for (int i = 0; i < n; i++)
        {
            if (a[i] != b[i])
            {
                return i;
            }
        }
        return a.Length == b.Length ? -1 : n;
    }

    /// <summary>自检日志里带一小段上下文（换行打平，免得把日志切碎）。</summary>
    static string Context(string s, int at)
    {
        if (string.IsNullOrEmpty(s))
        {
            return "";
        }
        int from = Math.Max(0, (at < 0 ? 0 : at) - 12);
        int len = Math.Min(40, s.Length - from);
        if (len <= 0)
        {
            return "";
        }
        return s.Substring(from, len).Replace("\r", "").Replace("\n", "\\n");
    }

    /// <summary>串尾 <paramref name="n"/> 个字符（换行打平）。看"少了一段"时比看开头有用。</summary>
    static string Tail(string s, int n)
    {
        if (string.IsNullOrEmpty(s))
        {
            return "";
        }
        int from = Math.Max(0, s.Length - n);
        return s.Substring(from).Replace("\r", "").Replace("\n", "\\n");
    }

    /// <summary>
    /// 【2026-10-04 第六轮】全池自检：**简介不许缺段**（用户报「很多卡的简介直接缺了一大段」）。
    ///
    /// <para>一次把全池点一遍，四条判据：</para>
    /// <list type="number">
    /// <item>卡文富化剥掉标记后必须与卡文**逐字相等**（丢字/多字都抓）；</item>
    /// <item>系列行重拼后剥完必须与重拼前剥完相等（<c>LinkifySeriesLine</c> 是**按区间替换**的）；</item>
    /// <item>结构性：<c>getName+getSmall</c> 必须**以 <c>[-]</c> 收尾**、有系列的必须含「系列：」、
    ///   必须有名字块 —— ⛔ 这两个函数各自把异常**吞掉**（catch 是空的），中途抛了就静默返回
    ///   **半截串**，屏幕上正是"缺了一段"，而日志里一个字都没有；</item>
    /// <item>卡文不许为空，并报总长度（与 cdb 对拍，能看出数据层有没有被截）。</item>
    /// </list>
    ///
    /// <para>末尾再跑一次**运行期改名**实验（只在 <c>log/nodrop_rename.probe</c> 存在时）：
    /// 改名前 / 改名后 / 再改一个 / 还原 —— 四次读数一对比，就能看出 <c>ApplyPool</c>
    /// 重建描述（<c>RewriteDesc</c>）那一趟有没有把简介改坏。跑完自动把改名撤回。</para>
    /// </summary>
    public static void PoolNoDropSelfTest()
    {
        QuickTestTrace.Log("nodrop", "pass1 " + NoDropPass(true));
        if (!QuickTestTrace.SwitchOn("nodrop_rename.probe"))
        {
            return;
        }
        try
        {
            // ⛔ 别再"跑 4 遍全池"：每遍 14846 张 × 剥标签，实测一遍要几十秒，
            //   4 遍直接超时（本轮第一次跑就没等到 pass2）。改成**定点**验证：
            //   就在那三张已知中招的卡（离线复现出来的样本）上比对 nativeDesc 与 c.Desc。
            QuickTestTrace.Log("nodrop", "rename " + ProbeRewriteOnSamples());
            // 顺带证明"改名会触发 ApplyPool 重建全池描述"这条链是通的（不然样本恒等、看不出事）。
            YGOSharp.CardNameTranslation.SetOverride(89631139, "改名测试甲");
            QuickTestTrace.Log("nodrop", "afterSet renamedLen="
                + ProbeDescLen(89631139));
            YGOSharp.CardNameTranslation.SetOverride(89631139, "");
            QuickTestTrace.Log("nodrop", "afterRestore len="
                + ProbeDescLen(89631139));
        }
        catch (Exception e)
        {
            QuickTestTrace.Log("nodrop", "rename probe failed " + e.GetType().Name);
        }
    }

    /// <summary>
    /// 定点验证 RewriteDesc：拿离线复现出来的三张"已知中招"样本，在**真实卡池**里
    /// 比对 cdb 原文（<c>nativeDesc</c>）与改写后的 <c>c.Desc</c>。
    ///
    /// <para>为什么是"长度"判据而不是逐字相等：<c>c.Desc</c> 与原文的差异里**混着玩家
    /// 改的名**（那是功能）。但丢字必然让长度**掉一整段**（实测样本掉 12~64 字），
    /// 而改名替换是等位的。所以判据＝"不该比原文短过 16 字"。</para>
    /// </summary>
    static string ProbeRewriteOnSamples()
    {
        // 三张都是离线用真实 nameMap 复现出"旧版会变短"的卡（id 见 notes/2026-10-04）。
        int[] ids = new int[] { 2511, 176392, 191749 };
        StringBuilder sb = new StringBuilder();
        int bad = 0;
        for (int i = 0; i < ids.Length; i++)
        {
            YGOSharp.Card c = FindCardById(ids[i]);
            if (c == null)
            {
                sb.Append(" id").Append(ids[i]).Append("=absent");
                continue;
            }
            int natLen = c.nativeDesc != null ? c.nativeDesc.Length : 0;
            int descLen = c.Desc != null ? c.Desc.Length : 0;
            // 差 16 以内算改名（译名长度不同），差得更多就是丢段。
            int lost = natLen - descLen;
            bool drop = lost > 16;
            if (drop)
            {
                bad++;
            }
            sb.Append(" id").Append(ids[i])
              .Append(" nat=").Append(natLen)
              .Append(" desc=").Append(descLen)
              .Append(" lost=").Append(lost)
              .Append(drop ? " DROP" : " ok");
        }
        sb.Append(" badDrop=").Append(bad);
        return sb.ToString();
    }

    /// <summary>按卡号在池里找一张卡（探针用；找不到返回 null）。</summary>
    static YGOSharp.Card FindCardById(int id)
    {
        try
        {
            List<YGOSharp.Card> pool = CardSearchWindow.ProbeWholePool();
            for (int i = 0; i < pool.Count; i++)
            {
                if (pool[i] != null && pool[i].Id == id)
                {
                    return pool[i];
                }
            }
        }
        catch (Exception)
        {
        }
        return null;
    }

    /// <summary>某张卡当前 <c>c.Desc</c> 的长度（-1 = 池里没有这张卡）。</summary>
    static int ProbeDescLen(int id)
    {
        YGOSharp.Card c = FindCardById(id);
        return c == null || c.Desc == null ? -1 : c.Desc.Length;
    }

    /// <summary>跑一遍全池判据，返回汇总串；<paramref name="verbose"/> 时把前 8 条明细也落盘。</summary>
    static string NoDropPass(bool verbose)
    {
        try
        {
            List<YGOSharp.Card> pool = CardSearchWindow.ProbeWholePool();
            int checkedN = 0;
            int badDesc = 0;
            int badSmall = 0;
            int smallNoEnd = 0;         // getSmall 末尾没有收色的 [-]
            int smallNoSeries = 0;      // 有系列（setcode != 0）却没有「系列：」那一段
            int smallNoName = 0;        // 连名字块都没有（[b]…[/b]）
            int emptyDesc = 0;          // 卡文是空的
            int dropped = 0;            // 🔴 相对 cdb 原文**变短**（丢段，2026-10-04 的真症状）
            long sumDesc = 0;           // 卡文总长度（与 cdb 对拍用）
            int shown = 0;
            for (int i = 0; i < pool.Count; i++)
            {
                YGOSharp.Card c = pool[i];
                if (c == null)
                {
                    continue;
                }
                string raw = c.Desc;
                if (!string.IsNullOrEmpty(raw))
                {
                    checkedN++;
                    sumDesc += raw.Length;
                    // 🔴 判据的参照必须是 **nativeDesc（cdb 原文）**，绝不能用 c.Desc。
                    //   c.Desc 正是 CardNameTranslation.RewriteDesc **就地改写过的那一份** ——
                    //   丢字（2026-10-04 用户报障「很多卡简介缺一大段」）就发生在改写里，
                    //   拿它当参照＝拿"被改坏的结果"证明"没改坏"⇒ 永远测不出来。
                    //   实证：那一版自检报 badDesc=0，而离线拿 cdb 原文一对，482/15017 张变短。
                    //   代价：c.Desc 与原文的差异里**混着玩家改的名**（那是功能，不是 bug），
                    //   所以只判"**长度**不该变短"（丢字必然变短；改名可能变长也可能变短，
                    //   但改名替换是等位的，不会少掉整段正文）——真丢字会一次丢掉几十上百字。
                    string nat = c.nativeDesc;
                    if (!string.IsNullOrEmpty(nat) && nat.Length > raw.Length + 16)
                    {
                        dropped++;
                        if (verbose && shown < 8)
                        {
                            shown++;
                            QuickTestTrace.Log("nodrop", "XX dropped id=" + c.Id
                                + " name=" + c.Name
                                + " nativeLen=" + nat.Length + " descLen=" + raw.Length
                                + " lost=" + (nat.Length - raw.Length)
                                + " nat=[" + Context(nat, 0) + "]"
                                + " desc=[" + Context(raw, 0) + "]");
                        }
                    }
                    // ⛔ 极少数老卡文的方括号是**卡面文字本身**（不是标记）⇒ 两边都剥一遍再比，
                    //   并标 rawBracket=1，免得把"原文自带方括号"误报成丢字。
                    bool rawBracket = raw.IndexOf('[') >= 0;
                    string refText = rawBracket ? VisibleText(raw) : raw;
                    string vis = VisibleText(Linkify(raw, 0));
                    if (vis != refText)
                    {
                        badDesc++;
                        if (verbose && shown < 8)
                        {
                            shown++;
                            int at = FirstDiff(refText, vis);
                            QuickTestTrace.Log("nodrop", "XX desc id=" + c.Id
                                + " name=" + c.Name
                                + " rawLen=" + refText.Length + " visLen=" + vis.Length
                                + " at=" + at + " rawBracket=" + (rawBracket ? 1 : 0)
                                + " raw=[" + Context(refText, at) + "]"
                                + " vis=[" + Context(vis, at) + "]");
                        }
                    }
                }
                else
                {
                    emptyDesc++;
                }
                // 系列行：只在有系列（setcode != 0）的卡上跑，判据同上。
                if (c.Setcode != 0)
                {
                    string small = null;
                    try
                    {
                        small = GameStringHelper.getName(c) + GameStringHelper.getSmall(c);
                    }
                    catch (Exception)
                    {
                    }
                    if (!string.IsNullOrEmpty(small))
                    {
                        // ⛔ 结构性判据（"缺一段"的真正嫌疑）：getName / getSmall 各自**把异常吞掉**
                        //   （两个 catch 都是空的），一旦中途抛了，返回的就是**半截串** ——
                        //   名字块 / 系列行 / 收色标记会**静默少掉一段**，而且日志里一个字都没有。
                        //   这里按"本该有什么"去点它：末尾必有 [-]、有系列的必有「系列：」、必有名字块。
                        if (!small.EndsWith("[-]", StringComparison.Ordinal))
                        {
                            smallNoEnd++;
                            if (verbose && shown < 8)
                            {
                                shown++;
                                QuickTestTrace.Log("nodrop", "XX smallNoEnd id=" + c.Id
                                    + " name=" + c.Name + " setcode=" + c.Setcode
                                    + " tail=[" + Tail(small, 60) + "]");
                            }
                        }
                        if (c.Setcode != 0 && small.IndexOf(GameStringHelper.xilie,
                                StringComparison.Ordinal) < 0)
                        {
                            smallNoSeries++;
                            if (verbose && shown < 8)
                            {
                                shown++;
                                QuickTestTrace.Log("nodrop", "XX smallNoSeries id=" + c.Id
                                    + " name=" + c.Name + " setcode=0x" + c.Setcode.ToString("x")
                                    + " small=[" + Tail(small, 90) + "]");
                            }
                        }
                        if (small.IndexOf("[b]", StringComparison.Ordinal) < 0)
                        {
                            smallNoName++;
                            if (verbose && shown < 8)
                            {
                                shown++;
                                QuickTestTrace.Log("nodrop", "XX smallNoName id=" + c.Id
                                    + " name=" + c.Name
                                    + " small=[" + Tail(small, 90) + "]");
                            }
                        }
                        // ⛔ 参照要**也剥一遍标记**：getName+getSmall 本身就带 [F4A460][b]…[sup]…
                        //   这些标记，拿带标记的原串去比剥完的串必然全体假红（第一版就这么错的）。
                        string ref2 = VisibleText(small);
                        string lined = LinkifySeriesLine(small, c.Setcode);
                        string vis2 = VisibleText(lined);
                        if (vis2 != ref2)
                        {
                            badSmall++;
                            if (verbose && shown < 8)
                            {
                                shown++;
                                int at2 = FirstDiff(ref2, vis2);
                                QuickTestTrace.Log("nodrop", "XX small id=" + c.Id
                                    + " name=" + c.Name
                                    + " rawLen=" + ref2.Length + " visLen=" + vis2.Length
                                    + " at=" + at2
                                    + " raw=[" + Context(ref2, at2) + "]"
                                    + " vis=[" + Context(vis2, at2) + "]"
                                    + " rawLine=[" + (ref2.Length > 220 ? ref2.Substring(0, 220)
                                        : ref2).Replace("\r", "").Replace("\n", "\\n") + "]");
                            }
                        }
                    }
                }
            }
            return "total=" + pool.Count + " desc=" + checkedN
                + " badDesc=" + badDesc + " badSmall=" + badSmall
                + " smallNoEnd=" + smallNoEnd + " smallNoSeries=" + smallNoSeries
                + " smallNoName=" + smallNoName
                + " emptyDesc=" + emptyDesc + " sumDesc=" + sumDesc
                + " dropped=" + dropped;
        }
        catch (Exception e)
        {
            return "failed " + e.GetType().Name + ": " + e.Message;
        }
    }

    /// <summary>
    /// 只在 log/qt_debug.on 存在时写轨迹的一次性自检（正式包零开销）：
    /// 把"字段表读没读到""富化出来的文本长什么样"摊到日志里，供验收脚本判读。
    /// 不碰任何游戏状态。
    /// </summary>
    public static void SelfTest()
    {
        if (!QuickTestTrace.Enabled)
        {
            return;
        }
        try
        {
            EnsureFields();
            int fields = fieldNames != null ? fieldNames.Count : 0;
            int display = 0;
            try
            {
                Dictionary<string, int> d = CardsManager.DisplayNameIndex;
                display = d != null ? d.Count : 0;
            }
            catch (Exception)
            {
            }
            QuickTestTrace.Log("link", "selftest fields=" + fields + " displayNames=" + display
                + " typeSuffixes=" + TypeSuffixes.Length);

            int[] probes = new int[] { 65518099, 8963110, 46986414 };
            for (int i = 0; i < probes.Length; i++)
            {
                Card c = CardsManager.Get(probes[i]);
                if (c == null || c.Id <= 0)
                {
                    QuickTestTrace.Log("link", "probe " + probes[i] + " = (not in pool)");
                    continue;
                }
                string desc = c.Desc ?? "";
                string rich = Linkify(desc, c.Id);
                QuickTestTrace.Log("link", "probe id=" + c.Id
                    + " name=" + c.Name
                    + " refs=" + CountChar(desc, '「')
                    + " outRefs=" + CountChar(rich, '「')
                    + " urlTags=" + CountOccur(rich, "[url=")
                    + " cardColor=" + CountOccur(rich, ColorCard)
                    + " fieldColor=" + CountOccur(rich, ColorField));
            }
            WholeUnitSelfTest();
            SegmentSelfTest();
            // 【2026-10-04 第六轮】全池「富化不许丢字」——用户报"很多卡的简介直接缺了一大段"，
            // 这条一次把所有卡都过一遍（原文 vs 富化后剥标记的可见文字），直接点名有问题的卡。
            PoolNoDropSelfTest();
        }
        catch (Exception e)
        {
            Program.DEBUGLOG(e);
        }
    }

    static int CountChar(string s, char c)
    {
        if (string.IsNullOrEmpty(s))
        {
            return 0;
        }
        int n = 0;
        for (int i = 0; i < s.Length; i++)
        {
            if (s[i] == c)
            {
                n++;
            }
        }
        return n;
    }

    public static int CountOccur(string s, string sub)
    {
        if (string.IsNullOrEmpty(s) || string.IsNullOrEmpty(sub))
        {
            return 0;
        }
        int n = 0;
        int at = 0;
        while (true)
        {
            int i = s.IndexOf(sub, at, StringComparison.Ordinal);
            if (i < 0)
            {
                break;
            }
            n++;
            at = i + sub.Length;
        }
        return n;
    }

    /// <summary>
    /// 把一次点击翻译成"该干什么"。返回 null = 没点在链接上。
    /// </summary>
    public static string ResolveClick(UILabel label, Vector3 worldPos)
    {
        int ordinal;
        return ResolveClick(label, worldPos, out ordinal);
    }

    /// <summary>
    /// 把一次点击翻译成"该干什么"。返回 null = 没点在链接上。
    /// <paramref name="ordinal"/> = 点中的是**文本里第几个链接**（0 起算；拿不到 = -1）。
    /// </summary>
    public static string ResolveClick(UILabel label, Vector3 worldPos, out int ordinal)
    {
        ordinal = -1;
        if (label == null)
        {
            return null;
        }
        string raw = label.text;
        if (string.IsNullOrEmpty(raw) || raw.IndexOf("[url=", StringComparison.Ordinal) < 0)
        {
            return null;
        }

        // 首选 NGUI 自带的命中：它在 mText（= 未处理的原文，含 [url=…]）上按
        // LastIndexOf("[url=") 反查，正是我们要的"原文下标"。
        string native = null;
        try
        {
            native = label.GetUrlAtPosition(worldPos);
        }
        catch (Exception)
        {
        }

        // 自己再算一遍，主要为了拿"第几个链接"（ordinal）：NGUI 只给 payload、不给顺序号，
        // 而「连点同一处才关窗」要的就是这份区分度（见 FindAt 的注释）。
        // 关键前提（读 NGUI 源码确认）：UILabel.ProcessText() 走的是
        //   NGUIText.WrapText(mText, out mProcessedText, keepCharCount:true, wrapLineColors:false, …)
        // keepCharCount=true 时换行只把行尾空格改成 '\n'（或什么都不加）——**字符总数不变，
        // 标签也不会被吃掉**（WrapText 只在测宽时跳过标签，拼接时仍按原文区间整段带走）。
        // 所以 processedText 的下标与 mText（label.text）的下标**一一对应**，可以直接拿来用。
        int index = -1;
        try
        {
            Vector2 local = label.cachedTransform.InverseTransformPoint(worldPos);
            index = label.GetCharacterIndexAtPosition(local, true);
        }
        catch (Exception)
        {
            index = -1;
        }

        string payload = FindAt(raw, index, out ordinal);
        if (payload == null)
        {
            // 对齐偶尔会差几个字：在原下标附近再找一圈，谁先中就用谁。
            for (int d = 1; d <= 8; d++)
            {
                payload = FindAt(raw, index + d, out ordinal);
                if (payload == null)
                {
                    payload = FindAt(raw, index - d, out ordinal);
                }
                if (payload != null)
                {
                    break;
                }
            }
        }

        if (payload == null)
        {
            // 自己扫不出来 ⇒ 退回 NGUI 的命中；代价是拿不到顺序号 ⇒
            // 那一击按"判不出是哪一处"处理，不会误触发关窗。
            ordinal = -1;
            return string.IsNullOrEmpty(native) ? null : native;
        }
        if (!string.IsNullOrEmpty(native) && native != payload)
        {
            // 两路读数不一致 = 落点落在两链接交界附近 ⇒ 顺序号也不敢认。
            ordinal = -1;
        }
        // payload 仍以 NGUI 的读数为准（它更准），顺序号用自己的。
        return string.IsNullOrEmpty(native) ? payload : native;
    }

    /// <summary>原始文本里，落点在第 index 个字符上的链接 payload。</summary>
    static string FindAt(string raw, int index)
    {
        int ordinal;
        return FindAt(raw, index, out ordinal);
    }

    /// <summary>
    /// 同上，外加"这是文本里的第几个 <c>[url=</c>"（0 起算；没落在链接上 = -1）。
    ///
    /// ⛔ 为什么非得要这个顺序号：同一张卡上的多处链接可能 payload **完全一样**
    ///   （同一个卡名在文里出现两次就是），光看 payload 分不出点的是哪一处。
    ///   用户 2026-10-03 要的「连点**同一处**才关窗」正需要这份区分度 ——
    ///   「同一种链接」「两个不同的链接但搜出来一样」都不算同一处。
    /// </summary>
    static string FindAt(string raw, int index, out int ordinal)
    {
        ordinal = -1;
        if (raw == null || index < 0 || index >= raw.Length)
        {
            return null;
        }
        int from = 0;
        int nth = 0;
        while (true)
        {
            int start = raw.IndexOf("[url=", from, StringComparison.Ordinal);
            if (start < 0)
            {
                return null;
            }
            int payloadStart = start + 5;
            int tagEnd = raw.IndexOf(']', payloadStart);
            if (tagEnd < 0)
            {
                return null;
            }
            int close = raw.IndexOf("[/url]", tagEnd, StringComparison.Ordinal);
            if (close < 0)
            {
                return null;
            }
            if (index > tagEnd && index < close)
            {
                ordinal = nth;
                return raw.Substring(payloadStart, tagEnd - payloadStart);
            }
            from = tagEnd + 1;
            nth++;
        }
    }

    /// <summary>把 payload 拆成 (kind, 参数)。</summary>
    public static bool Parse(string payload, out char kind, out string arg)
    {
        kind = '\0';
        arg = "";
        if (string.IsNullOrEmpty(payload))
        {
            return false;
        }
        kind = payload[0];
        arg = payload.Substring(1);
        return true;
    }

    /// <summary>
    /// payload 的拆法：<c>&lt;种类&gt;&lt;主类掩码&gt;:&lt;文字&gt;</c>。
    /// 掩码 0 = 不限主类。没有冒号 / 掩码不是数字时返回 false（调用方按掩码 0 兜底）。
    /// </summary>
    public static bool ParseTyped(string arg, out int typeMask, out string text)
    {
        typeMask = 0;
        text = arg;
        int colon = arg.IndexOf(':');
        if (colon <= 0)
        {
            return false;
        }
        int mask;
        if (!int.TryParse(arg.Substring(0, colon), out mask))
        {
            return false;
        }
        typeMask = mask;
        text = arg.Substring(colon + 1);
        return true;
    }

    /// <summary>
    /// <see cref="KindName"/> 那支的 payload 拆法：<c>&lt;主类掩码&gt;:&lt;卡名&gt;:&lt;被点处原文&gt;</c>
    /// （第三段可选；老写法只有前两段，<paramref name="title"/> 会是空串）。
    ///
    /// <para>为什么要带"被点处原文"（用户 2026-10-04 第三条）：检索窗标题要照抄玩家点的
    /// 那句话（如 <c>「融合」魔法卡</c>）+ 结果数，不要重新拼一个 ——
    /// 而卡名那一支的 payload 只有名字，拼不回括号与尾词。</para>
    /// </summary>
    public static bool ParseCardName(string arg, out int typeMask, out string name, out string title)
    {
        typeMask = 0;
        name = arg ?? "";
        title = "";
        if (string.IsNullOrEmpty(arg))
        {
            return false;
        }
        int c1 = arg.IndexOf(':');
        if (c1 <= 0 || !int.TryParse(arg.Substring(0, c1), out typeMask))
        {
            return false;
        }
        int c2 = arg.IndexOf(':', c1 + 1);
        if (c2 < 0)
        {
            name = arg.Substring(c1 + 1);      // 老写法：只有掩码 + 名字
            return true;
        }
        name = arg.Substring(c1 + 1, c2 - c1 - 1);
        title = arg.Substring(c2 + 1);
        return true;
    }

    /// <summary>
    /// <see cref="KindSet"/> 那支的 payload 拆法：<c>&lt;主类掩码&gt;:&lt;表内码&gt;:&lt;被点处原文&gt;</c>。
    ///
    /// <para>比 <see cref="ParseTyped"/> 多一段"表内码"（<c>!setname</c> 的 hashCode）：
    /// 第三段只进标题，**判据是码** —— 这样系列名被改译名也不影响检索，
    /// 而且检索端能用 <c>CardsManager.IfSetCard</c> 的"低 12 位=系列本体、高 4 位=子系列"
    /// 口径把子系列（如 E-HERO 之于 英雄）一起收进来。</para>
    /// </summary>
    public static bool ParseSet(string arg, out int typeMask, out int code, out string title)
    {
        typeMask = 0;
        code = -1;
        title = "";
        if (string.IsNullOrEmpty(arg))
        {
            return false;
        }
        int c1 = arg.IndexOf(':');
        if (c1 <= 0)
        {
            return false;
        }
        int c2 = arg.IndexOf(':', c1 + 1);
        if (c2 < 0)
        {
            return false;
        }
        if (!int.TryParse(arg.Substring(0, c1), out typeMask))
        {
            return false;
        }
        if (!int.TryParse(arg.Substring(c1 + 1, c2 - c1 - 1), out code))
        {
            return false;
        }
        // ⚠ 标题**可能含 ':'**（卡文里的字段名不保证没有），所以第三段取到**行尾**。
        title = arg.Substring(c2 + 1);
        return true;
    }
}
