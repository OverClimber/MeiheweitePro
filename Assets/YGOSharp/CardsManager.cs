using System.Collections.Generic;
using System.Data;
using Mono.Data.Sqlite;
using System;
using System.IO;
using System.Text.RegularExpressions;
using YGOSharp.OCGWrapper.Enums;

namespace YGOSharp
{
    internal static class CardsManager
    {
        /// <summary>OCG 池（历史那一本，装载链与以前逐字一致）。</summary>
        private static IDictionary<int, Card> _cards = new Dictionary<int, Card>();

        /// <summary>RD 池（只由 rd/cdb/*.cdb 那条通道填）。</summary>
        private static IDictionary<int, Card> _cards_rd = new Dictionary<int, Card>();

        /// <summary>
        /// 当前模式生效的那一池 —— **全部查询都从这里走**。
        ///
        /// 装载是「启动全装、各进各池」，所以切模式零加载；但**查询必须分池**：
        /// 一旦 RD 卡进到 OCG 池，编辑器搜索、卡表回退、卡组校验会全线串味。
        /// </summary>
        private static IDictionary<int, Card> ActiveCards
        {
            get { return GameModeManager.IsRD ? _cards_rd : _cards; }
        }

        /// <summary>某一池的卡数（排查/验收脚本用；绕过模式取原始池）。</summary>
        internal static int CountOf(bool rd)
        {
            return (rd ? _cards_rd : _cards).Count;
        }

        /// <summary>
        /// 遍历当前模式那一个池里的每一张卡（卡号 + 卡对象）。
        /// 给「整池自查」用 —— 例如「没改过名的卡是不是也报了 HasOverride」，
        /// 这种问题只有把 1 万多张全扫一遍才看得出来，看几张是看不出来的。
        /// </summary>
        internal static void ForEachActiveCard(Action<int, Card> visit)
        {
            if (visit == null)
            {
                return;
            }
            foreach (KeyValuePair<int, Card> item in ActiveCards)
            {
                visit(item.Key, item.Value);
            }
        }

        /// <summary>
        /// 当前模式那个池里，带**任一位**被 raceMask 命中的卡有几张（排查/验收用，同 CountOf 的性质）。
        ///
        /// 存在的意义：把「勾了某个种族到底能不能搜到」变成一个**离线可判**的数。
        /// 走的判据与 <see cref="searchAdvanced"/> 里那行逐字一致（都先转 uint）——
        /// RD 的「电子人」是 bit31，int 读出来是负数，`(int & uint)` 会被提升成 long
        /// 并**符号扩展**成 0xFFFFFFFF80000000，与 0x0000000080000000 与出来恒为 0，
        /// 于是「勾了电子人一张都搜不到」而没有任何报错。这个函数就是那条坑的哨兵。
        /// </summary>
        internal static int CountOfRace(uint raceMask)
        {
            int n = 0;
            foreach (KeyValuePair<int, Card> item in ActiveCards)
            {
                if (((uint)item.Value.Race & raceMask) != 0)
                {
                    n++;
                }
            }
            return n;
        }

        /// <summary>
        /// 当前模式那个池里，「主类是 mainMask **且**带 bit 位」的卡有几张（bit &lt; 0 = 不带位测试，
        /// 即只数主类）。性质同 <see cref="CountOfRace"/>：把「这一档到底搜不搜得到」
        /// 变成一个**离线可判的数**。
        ///
        /// 用途：卡组编辑器「种类」下拉要只列这个池里真有的档位 —— RD 没有同调/超量/灵摆/连接，
        /// 魔法里也没有速攻，共用 OCG 那份清单就会列出一堆死项（勾了 0 条）。
        /// 走的是与 <see cref="searchAdvanced"/> 里那行同族的位测试。
        ///
        /// ⚠ 先把 <c>Card.Type</c>（sqlite int32）转 uint 再与：RD 的位用到 bit31 时
        ///   `1 &lt;&lt; 31` 在 C# 里是 int.MinValue，与 uint 相与会提升成 long 并**符号扩展**，
        ///   bit31 恒不命中（同 CountOfRace 的坑，见那里的注释）。
        /// </summary>
        internal static int CountOfType(int bit, uint mainMask)
        {
            uint want = bit < 0 ? 0u : (1u << bit);
            int n = 0;
            foreach (KeyValuePair<int, Card> item in ActiveCards)
            {
                uint t = (uint)item.Value.Type;
                if ((t & mainMask) == 0)
                {
                    continue;
                }
                if (bit >= 0 && (t & want) == 0)
                {
                    continue;
                }
                n++;
            }
            return n;
        }

        public static string nullName = "";

        public static string nullString = "";

        internal static void initialize(string databaseFullPath, bool replace = false)
        {
            initializeInto(_cards, databaseFullPath, replace);
        }

        /// <summary>
        /// RD 数据通道：装载进 **RD 池**。调用点只有 Program 的 rd/cdb/*.cdb 那一段
        /// （见 _plan_rdmode.md §2「数据分发」）。
        /// 默认 replace=true：三份表（正式卡 / 先行卡 / 异画卡）id 域实测互不重叠，
        /// 顺序不影响身份，但后加载的可覆盖更符合直觉。
        /// </summary>
        internal static void initializeRD(string databaseFullPath, bool replace = true)
        {
            initializeInto(_cards_rd, databaseFullPath, replace);
        }

        private static void initializeInto(IDictionary<int, Card> pool, string databaseFullPath, bool replace)
        {
            nullName = InterString.Get("未知卡片");
            nullString = "";
            nullString += "欢迎使用 YGOPro2";
            nullString += "\r\n\r\n";
            nullString += "官方网站：";
            nullString += "\r\n";
            nullString += "[url=https://ygopro2.lofter.com/][u]https://ygopro2.lofter.com/[/u][/url]";
            nullString += "\r\n\r\n";
            nullString += "公测玩家交流群：\r\n[url=https://jq.qq.com/?_wv=1027&k=O1xapcRQ][u]966380039[/u][/url]";
            using (SqliteConnection connection = new SqliteConnection("Data Source=" + databaseFullPath))
            {
                connection.Open();

                using (IDbCommand command = new SqliteCommand("SELECT datas.*, texts.* FROM datas,texts WHERE datas.id=texts.id;", connection))
                {
                    using (IDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            LoadCard(pool, reader, replace);
                        }
                    }
                }
            }
        }

        /// <summary>清空已载入的卡片，供数据更新后整体重载（见 Program.ReloadGameDatabases）。</summary>
        internal static void Reset()
        {
            _cards = new Dictionary<int, Card>();
            _cards_rd = new Dictionary<int, Card>();
            poolStamp++;
            InvalidateNameTranslation();
        }

        // ============================================================ 卡名翻译
        //
        // 需求 5 的落地：**显示用的 Name/Desc 全部由「cdb 原文 + 当前翻译表」重算**。
        // 三个好处：
        //   ① 换翻译后，卡表 / 检索结果 / 对局日志 / 左侧说明 —— 所有读 card.Name 的地方一起变；
        //   ② 描述里引用的卡名（中文卡文一律写作「卡名」）跟着一起换，
        //      所以把「卡通世界」改成「童话书」后，所有"记述了卡通世界"的描述同步变「童话书」；
        //   ③ 单张卡的自定义外号与全局翻译表是两套数据，换全局时不动单张（见 CardNameTranslation）。
        //
        // 性能：只在「池子换代」或「翻译版本变了」时重算一次全池，之后显示与检索都是查表；
        // NameStamp 让富文本缓存知道该作废了。

        /// <summary>某一池的两张索引：原生名→卡号（重写描述用）、显示名→卡号（点击反查用）。</summary>
        private class PoolIndex
        {
            public IDictionary<int, Card> pool;
            public int poolStamp = -1;
            public int version = -1;
            public Dictionary<string, int> nativeToId = new Dictionary<string, int>();
            public Dictionary<string, int> displayToId = new Dictionary<string, int>();
        }

        private static PoolIndex indexOcg = new PoolIndex();
        private static PoolIndex indexRd = new PoolIndex();

        /// <summary>
        /// 「异画」分组：卡号 → 它所在异画组的**组主**卡号。
        ///
        /// 判据（本机 14981 张实测：580 张带 alias 的卡里 562 张与本卡**同名** = 异画；
        /// 另外 18 张不同名 = 水波海豚 / 海洋海豚 这种**真·不同的卡**，绝不能并组）：
        ///   alias != 0 &amp;&amp; alias != id &amp;&amp; 池里 alias 那张卡的**原文卡名**与本卡逐字相同 → 同一组。
        /// 组主 = alias 指向的那张卡；找不到人、或者名字对不上，就自己当组主。
        ///
        /// 只在池子换代时重建一次（全池一趟），之后改名/查名都是字典查询 —— 需求 6 的「不进每帧」。
        /// </summary>
        private static Dictionary<int, int> aliasRootOcg = new Dictionary<int, int>();
        private static Dictionary<int, int> aliasRootRd = new Dictionary<int, int>();
        private static int aliasRootStamp = -1;

        private static void EnsureAliasGroups()
        {
            if (aliasRootStamp == poolStamp)
            {
                return;
            }
            aliasRootOcg = BuildAliasRoot(_cards);
            aliasRootRd = BuildAliasRoot(_cards_rd);
            aliasRootStamp = poolStamp;
        }

        private static Dictionary<int, int> BuildAliasRoot(IDictionary<int, Card> pool)
        {
            Dictionary<int, int> root = new Dictionary<int, int>(pool.Count);
            foreach (KeyValuePair<int, Card> item in pool)
            {
                Card card = item.Value;
                int r = card.Id;
                if (card.Alias > 0 && card.Alias != card.Id)
                {
                    Card main;
                    if (pool.TryGetValue(card.Alias, out main)
                        && !string.IsNullOrEmpty(main.nativeName)
                        && main.nativeName == card.nativeName)
                    {
                        r = card.Alias;
                    }
                }
                root[card.Id] = r;
            }
            // alias 还可能再串一级（A ← B ← C）：把指向组主的节点一路收敛到最顶层。
            foreach (KeyValuePair<int, Card> item in pool)
            {
                int r = root[item.Key];
                for (int hop = 0; hop < 4; hop++)
                {
                    int up;
                    if (r == item.Key || !root.TryGetValue(r, out up) || up == r)
                    {
                        break;
                    }
                    r = up;
                }
                root[item.Key] = r;
            }
            return root;
        }

        /// <summary>当前模式下这张卡所属异画组的组主卡号（没有异画关系就是它自己）。</summary>
        internal static int AliasRootOf(int id)
        {
            EnsureAliasGroups();
            int root;
            Dictionary<int, int> map = GameModeManager.IsRD ? aliasRootRd : aliasRootOcg;
            if (map.TryGetValue(id, out root))
            {
                return root;
            }
            return id;
        }

        /// <summary>当前模式下这张卡的异画组里有几张卡（含自己；1 = 没有异画）。</summary>
        internal static int AliasGroupSize(int id)
        {
            EnsureAliasGroups();
            int root = AliasRootOf(id);
            Dictionary<int, int> map = GameModeManager.IsRD ? aliasRootRd : aliasRootOcg;
            int n = 0;
            foreach (KeyValuePair<int, int> item in map)
            {
                if (item.Value == root)
                {
                    n++;
                }
            }
            return n;
        }

        private static int nameStampCounter = 0;
        private static int translatedVersion = -1;

        /// <summary>索引/译名代次。任何一次重算都会 +1，富文本缓存拿它当失效判据。</summary>
        internal static int NameStamp
        {
            get { EnsureNameTranslation(); return nameStampCounter; }
        }

        /// <summary>当前模式那一池的「显示名 → 卡号」索引（点击文本里的卡名要反查它）。</summary>
        internal static Dictionary<string, int> DisplayNameIndex
        {
            get
            {
                EnsureNameTranslation();
                return (GameModeManager.IsRD ? indexRd : indexOcg).displayToId;
            }
        }

        /// <summary>当前模式那一池的「原生名 → 卡号」索引。</summary>
        internal static Dictionary<string, int> NativeNameIndex
        {
            get
            {
                EnsureNameTranslation();
                return (GameModeManager.IsRD ? indexRd : indexOcg).nativeToId;
            }
        }

        internal static void InvalidateNameTranslation()
        {
            indexOcg.poolStamp = -1;
            indexRd.poolStamp = -1;
            translatedVersion = -1;
        }

        /// <summary>用户换了全局翻译 / 改了某张卡的外号后调这里：立刻重算一遍全池。</summary>
        internal static void ReapplyNameTranslation()
        {
            CardNameTranslation.Ensure();
            InvalidateNameTranslation();
            EnsureNameTranslation();
        }

        private static void EnsureNameTranslation()
        {
            CardNameTranslation.Ensure();
            // 先建好异画分组：ApplyPool 里的 DisplayName 会按组取自定义名字，
            // 这一句保证它拿到的是「池子已经装完」之后的分组，而不是加载途中的半成品。
            EnsureAliasGroups();
            if (indexOcg.poolStamp == poolStamp && indexRd.poolStamp == poolStamp
                && translatedVersion == CardNameTranslation.Version)
            {
                return;
            }
            ApplyPool(indexOcg, _cards);
            ApplyPool(indexRd, _cards_rd);
            translatedVersion = CardNameTranslation.Version;
            nameStampCounter++;
        }

        private static void ApplyPool(PoolIndex index, IDictionary<int, Card> pool)
        {
            index.pool = pool;
            index.poolStamp = poolStamp;
            index.version = CardNameTranslation.Version;
            index.nativeToId.Clear();
            index.displayToId.Clear();

            // 第一遍：用原文建「原生名 → 卡号」，并算出每张卡该显示成什么名字。
            int count = pool.Count;
            List<string> natives = new List<string>(count);
            List<string> displays = new List<string>(count);
            foreach (KeyValuePair<int, Card> item in pool)
            {
                Card card = item.Value;
                string native = card.nativeName;
                if (string.IsNullOrEmpty(native))
                {
                    native = card.Name;
                    card.nativeName = native;
                }
                if (string.IsNullOrEmpty(card.nativeDesc))
                {
                    card.nativeDesc = card.Desc;
                }
                natives.Add(native);
                string display = CardNameTranslation.DisplayName(item.Key, native);
                displays.Add(display);
                if (!index.nativeToId.ContainsKey(native))
                {
                    index.nativeToId[native] = item.Key;
                }
                if (!index.displayToId.ContainsKey(display))
                {
                    index.displayToId[display] = item.Key;
                }
            }

            // 第二遍：整张表算齐了再重写描述 —— 边算边用会让「先出现的卡」看不到「后出现的卡」的译名。
            Dictionary<string, string> nameMap = new Dictionary<string, string>();
            for (int i = 0; i < count; i++)
            {
                if (!string.IsNullOrEmpty(natives[i]) && displays[i] != natives[i]
                    && !nameMap.ContainsKey(natives[i]))
                {
                    nameMap[natives[i]] = displays[i];
                }
            }
            // 系列（字段）译名（需求 3）也并进同一张 map：中文卡文把字段名也写成「」引用
            //（RewriteDesc 的注释里实测 17065 处能对上"卡名/字段"），所以「改了系列译名
            // 也要作用在简介上」这一条，靠的就是这里 —— **卡名优先**（先卡名后字段，
            // 同名时卡名的那条已占位，不会被字段顶掉）。
            Dictionary<string, string> fieldMap = CardNameTranslation.FieldMapForCurrentPack();
            if (fieldMap != null)
            {
                foreach (KeyValuePair<string, string> kv in fieldMap)
                {
                    if (!string.IsNullOrEmpty(kv.Key) && !string.IsNullOrEmpty(kv.Value)
                        && !nameMap.ContainsKey(kv.Key))
                    {
                        nameMap[kv.Key] = kv.Value;
                    }
                }
            }
            int k = 0;
            foreach (KeyValuePair<int, Card> item in pool)
            {
                Card card = item.Value;
                card.Name = displays[k];
                card.Desc = nameMap.Count > 0
                    ? CardNameTranslation.RewriteDesc(card.nativeDesc, nameMap)
                    : card.nativeDesc;
                k++;
            }
        }

        /// <summary>
        /// 只做解析校验、不写入内存：确认这份 cdb 能被真正读出来（表结构、列数、行数都对）。
        /// 用于在线更新时校验刚下载到 .tmp 的文件，避免把坏数据替换进游戏目录。
        /// 与 initialize 同源：同样的 SQL、同样走 Card 构造，只是不落 _cards。
        /// </summary>
        internal static bool Validate(string databaseFullPath)
        {
            int count = 0;
            try
            {
                using (SqliteConnection connection = new SqliteConnection("Data Source=" + databaseFullPath))
                {
                    connection.Open();

                    using (IDbCommand command = new SqliteCommand("SELECT datas.*, texts.* FROM datas,texts WHERE datas.id=texts.id;", connection))
                    {
                        using (IDataReader reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                new Card(reader);
                                count++;
                            }
                        }
                    }
                }
            }
            catch (Exception)
            {
                return false;
            }
            return count > 0;
        }

        internal static Card GetCard(int id)
        {
            IDictionary<int, Card> pool = ActiveCards;
            if (pool.ContainsKey(id))
                return pool[id].clone();
            return null;
        }

        internal static Card GetCardRaw(int id)
        {
            IDictionary<int, Card> pool = ActiveCards;
            if (pool.ContainsKey(id))
                return pool[id];
            return null;
        }

        /// <summary>
        /// 绕过模式、指名取某一池的原始卡。
        ///
        /// 目前只有 <c>PacksManager.initialize</c> 用它 —— 那边的数据源固定是 OCG 的
        /// <c>pack/*.db</c>，而装载动作可能发生在 RD 模式下（菜单「资源下载」更新完成后
        /// 会 ReloadGameDatabases）；走 ActiveCards 的话在 RD 下会一条都对不上，
        /// 卡包列表会整块变空。**别拿它当通用查询口。**
        /// </summary>
        internal static Card GetCardRawInPool(bool rd, int id)
        {
            IDictionary<int, Card> pool = rd ? _cards_rd : _cards;
            if (pool.ContainsKey(id))
                return pool[id];
            return null;
        }

        internal static Card Get(int id)
        {
            Card returnValue = new Card();
            if (id > 0)
            {
                // ⚠ 位宽回退（id-0..id-9）**只允许在当前池内做**：
                // 跨池回退会拿到另一条线的不相干卡名（_plan_rdmode.md §2 清单 1 的坑）。
                for (int i = 0; i < 10; i++)
                {
                    returnValue = GetCard(id - i);
                    if (returnValue != null)
                    {
                        break;
                    }
                }
                if (returnValue == null)
                {
                    returnValue = new Card();
                }
            }
            return returnValue;
        }

        /// <summary>
        /// 卡池换代的戳。装载 / 清空都会 +1 —— 卡名索引与描述重写都靠它判断"要不要重算"。
        /// </summary>
        private static int poolStamp = 0;

        private static void LoadCard(IDictionary<int, Card> pool, IDataRecord reader, bool replace)
        {
            Card card = new Card(reader);
            if (!pool.ContainsKey(card.Id))
            {
                pool.Add(card.Id, card);
            }
            else if (replace)
            {
                pool.Remove(card.Id);
                pool.Add(card.Id, card);
            }
            poolStamp++;
        }

        internal static void updateSetNames()
        {
            // 两池都要刷：系列名来自 GameStringManager（与模式无关），漏掉谁谁就整池显示空系列。
            updateSetNames(_cards);
            updateSetNames(_cards_rd);
        }

        private static void updateSetNames(IDictionary<int, Card> pool)
        {
            foreach (var item in pool)
            {
                Card card = item.Value;
                card.strSetName = GameStringHelper.getSetName(card.Setcode);
            }
        }

        internal static List<Card> searchAdvanced(
            string getName,
            int getLevel,
            int getAttack,
            int getDefence,
            int getP,
            int getYear,
            int getLevel_UP,
            int getAttack_UP,
            int getDefence_UP,
            int getP_UP,
            int getYear_UP,
            int getOT,
            string getPack,
            int getBAN,
            Banlist banlist,
            uint getTypeFilter,
            uint getTypeFilter2,
            uint getRaceFilter,
            uint getAttributeFilter,
            uint getCatagoryFilter
            )
        {
            EnsureNameTranslation();
            List<Card> returnValue = new List<Card>();
            IDictionary<int, Card> pool = ActiveCards;
            foreach (var item in pool)
            {
                Card card = item.Value;
                if ((card.Type & (uint)CardType.Token) == 0)
                {
                    if (getName == ""
                        || ContainsLiteral(card.Name, getName)
                        || ContainsLiteral(card.Desc, getName)
                        || ContainsLiteral(card.strSetName, getName)
                        || card.Id.ToString() == getName
                        )
                    {
                        if (((card.Type & getTypeFilter) == getTypeFilter || getTypeFilter == 0)
                            && ((card.Type == getTypeFilter2
                                || getTypeFilter == (UInt32)CardType.Monster) && (card.Type & getTypeFilter2) == getTypeFilter2
                                || getTypeFilter2 == 0))
                        {
                            // ⚠ 必须先转成 uint 再比。card.Race 是 sqlite 的 int32，RD 的「电子人」
                            //   用的就是 0x80000000（读出来是负数）；int 与 uint 相与会提升成 long，
                            //   于是 int 那侧被**符号扩展**成 0xFFFFFFFF80000000，
                            //   与 0x0000000080000000 与出来恒为 0 —— 勾了「电子人」一张都搜不到。
                            //   两侧都是 uint 则语义不变（OCG 只用 bit0..25，永远碰不到符号位）。
                            if (((uint)card.Race & getRaceFilter) > 0 || getRaceFilter == 0)
                            {
                                if ((card.Attribute & getAttributeFilter) > 0 || getAttributeFilter == 0)
                                {
                                    if (((card.Category & getCatagoryFilter))== getCatagoryFilter || getCatagoryFilter == 0)
                                    {
                                        if (judgeint(getAttack, getAttack_UP, card.Attack))
                                        {
                                            if (judgeint(getDefence, getDefence_UP, card.Defense))
                                            {
                                                if (judgeint(getLevel, getLevel_UP, card.Level))
                                                {
                                                    if (judgeint(getP, getP_UP, card.LScale))
                                                    {
                                                        if (judgeint(getYear, getYear_UP, card.year))
                                                        {
                                                            if (getBAN == -233 || banlist == null || banlist.GetQuantity(card.Id) == getBAN)
                                                            {
                                                                if (getOT == -233 || (getOT & card.Ot) == getOT)
                                                                {
                                                                    if (getPack == "" || card.packFullName == getPack)
                                                                    {
                                                                        returnValue.Add(card);
                                                                    }
                                                                }
                                                            }
                                                        }
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
            nameInSearch = getName;
            returnValue.Sort(comparisonOfCard());
            nameInSearch = "";
            return returnValue;
        }

        static string nameInSearch = "";

        /// <summary>
        /// 关键字命中判定 —— **纯文本包含，不走正则**。
        ///
        /// 老实现把关键字当正则塞进 Regex.Replace 再看结果变没变，两个毛病：
        ///   ① 卡名带正则元字符就会炸（"BFS(炖牛肉)装甲" 的圆括号、"No.39 …" 的点），
        ///      而需求 1~4 会让**卡名本身**成为检索词，点一下就是一次崩溃；
        ///   ② 每次比较都要跑一遍正则，卡池上万张卡 × 3 个字段 = 一次检索几万次正则 —— 会卡。
        /// 这里换成 OrdinalIgnoreCase 子串查找：语义正是搜的人想要的"包含"。
        /// </summary>
        static bool ContainsLiteral(string haystack, string needle)
        {
            if (string.IsNullOrEmpty(needle))
            {
                return true;
            }
            if (string.IsNullOrEmpty(haystack))
            {
                return false;
            }
            return haystack.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        static bool judgeint(int min, int max, int raw)
        {
            bool re = true;
            if (min == -233 && max == -233)
            {
                re = true;
            }
            if (min == -233 && max != -233)
            {
                re = max == raw;
            }
            if (min != -233 && max == -233)
            {
                re = min == raw;
            }
            if (min != -233 && max != -233)
            {
                re = min <= raw && raw <= max;
            }
            return re;
        }

        internal static List<Card> search(
          string getName,
            List<int> getsearchCode
            )
        {
            EnsureNameTranslation();
            List<Card> returnValue = new List<Card>();
            IDictionary<int, Card> pool = ActiveCards;
            foreach (var item in pool)
            {
                Card card = item.Value;
                if (getName == ""
                        || ContainsLiteral(card.Name, getName)
                        //|| ContainsLiteral(card.Desc, getName)
                        || ContainsLiteral(card.strSetName, getName)
                        || card.Id.ToString() == getName
                        )
                {
                    if (getsearchCode.Count == 0|| is_declarable(card, getsearchCode))
                    {
                        returnValue.Add(card);
                    }
                }
            }
            nameInSearch = getName;
            returnValue.Sort(comparisonOfCard());
            nameInSearch = "";
            return returnValue;
        }

        private static bool is_declarable(Card card, List<int> getsearchCode)
        {
            Stack<int> stack = new Stack<int>();
            for (int i = 0; i < getsearchCode.Count; i++)
            {
                switch (getsearchCode[i])
                {
                    case (int)searchCode.OPCODE_ADD:
                        if (stack.Count >= 2)
                        {
                            int rhs = stack.Pop();
                            int lhs = stack.Pop();
                            stack.Push(lhs + rhs);
                        }
                        break;
                    case (int)searchCode.OPCODE_SUB:
                        if (stack.Count >= 2)
                        {
                            int rhs = stack.Pop();
                            int lhs = stack.Pop();
                            stack.Push(lhs - rhs);
                        }
                        break;
                    case (int)searchCode.OPCODE_MUL:
                        if (stack.Count >= 2)
                        {
                            int rhs = stack.Pop();
                            int lhs = stack.Pop();
                            stack.Push(lhs * rhs);
                        }
                        break;
                    case (int)searchCode.OPCODE_DIV:
                        if (stack.Count >= 2)
                        {
                            int rhs = stack.Pop();
                            int lhs = stack.Pop();
                            stack.Push(lhs / rhs);
                        }
                        break;
                    case (int)searchCode.OPCODE_AND:
                        if (stack.Count >= 2)
                        {
                            int rhs = stack.Pop();
                            int lhs = stack.Pop();
                            bool b0 = rhs != 0;
                            bool b1 = lhs != 0;
                            if (b0 && b1)
                            {
                                stack.Push(1);
                            }
                            else
                            {
                                stack.Push(0);
                            }
                        }
                        break;
                    case (int)searchCode.OPCODE_OR:
                        if (stack.Count >= 2)
                        {
                            int rhs = stack.Pop();
                            int lhs = stack.Pop();
                            bool b0 = rhs != 0;
                            bool b1 = lhs != 0;
                            if (b0 || b1)
                            {
                                stack.Push(1);
                            }
                            else
                            {
                                stack.Push(0);
                            }
                        }
                        break;
                    case (int)searchCode.OPCODE_NEG:
                        if (stack.Count >= 1)
                        {
                            int rhs = stack.Pop();
                            stack.Push(-rhs);
                        }
                        break;
                    case (int)searchCode.OPCODE_NOT:
                        if (stack.Count >= 1)
                        {
                            int rhs = stack.Pop();
                            bool b0 = rhs != 0;
                            if (b0)
                            {
                                stack.Push(0);
                            }
                            else
                            {
                                stack.Push(1);
                            }
                        }
                        break;
                    case (int)searchCode.OPCODE_ISCODE:
                        if (stack.Count >= 1)
                        {
                            int code = stack.Pop();
                            bool b0 = code == card.Id;
                            if (b0)
                            {
                                stack.Push(1);
                            }
                            else
                            {
                                stack.Push(0);
                            }
                        }
                        break;
                    case (int)searchCode.OPCODE_ISSETCARD:
                        if (stack.Count >= 1)
                        {
                            if (IfSetCard(stack.Pop(), card.Setcode))
                            {
                                stack.Push(1);
                            }
                            else
                            {
                                stack.Push(0);
                            }
                        }
                        break;
                    case (int)searchCode.OPCODE_ISTYPE:
                        if (stack.Count >= 1)
                        {
                            if ((stack.Pop() & card.Type) > 0)
                            {
                                stack.Push(1);
                            }
                            else
                            {
                                stack.Push(0);
                            }
                        }
                        break;
                    case (int)searchCode.OPCODE_ISRACE:
                        if (stack.Count >= 1)
                        {
                            // ⚠ 与 searchAdvanced 同一类坑：先转 uint 再比。
                            // 卡表里 Race 是 int32，RD 的「电子人」= 0x80000000（负数），
                            // `& ` 出来仍是负数，`> 0` 会判假 ⇒ 声明「电子人族」的卡永远搜不到。
                            // 位掩码的语义本来就是「非零」，用 != 0 才对（OCG 位全 ≤ 25，两式等价，
                            // 所以这行不会改动 OCG 的既有行为）。
                            if (((uint)stack.Pop() & (uint)card.Race) != 0)
                            {
                                stack.Push(1);
                            }
                            else
                            {
                                stack.Push(0);
                            }
                        }
                        break;
                    case (int)searchCode.OPCODE_ISATTRIBUTE:
                        if (stack.Count >= 1)
                        {
                            if ((stack.Pop() & card.Attribute) > 0)
                            {
                                stack.Push(1);
                            }
                            else
                            {
                                stack.Push(0);
                            }
                        }
                        break;
                    default:
                        stack.Push(getsearchCode[i]);
                        break;
                }
            }
            if (stack.Count != 1 || stack.Pop() == 0)
                return false;
            return
                card.Id == (int)TwoNameCards.CARD_MARINE_DOLPHIN
                ||
                card.Id == (int)TwoNameCards.CARD_TWINKLE_MOSS
         ||
         (!(card.Alias != 0)
         && ((card.Type & ((int)CardType.Monster + (int)CardType.Token)))
         != ((int)CardType.Monster + (int)CardType.Token));
        }

        public static bool IfSetCard(int setCodeToAnalyse, ulong setCodeFromCard)
        {
            bool res = false;
            uint settype = (uint)setCodeToAnalyse & 0xfff;
            uint setsubtype = (uint)setCodeToAnalyse & 0xf000;
            ulong sc = setCodeFromCard;
            while (sc != 0)
            {
                if ((sc & 0xfff) == settype && (sc & 0xf000 & setsubtype) == setsubtype)
                    res = true;
                sc = sc >> 16;
            }

            return res;
        }

        internal static Comparison<Card> comparisonOfCard()
        {
            return (left, right) =>
            {
                int a = 1;
                if (left.Name == nameInSearch && right.Name != nameInSearch)
                {
                    a = -1;
                }
                else if (right.Name == nameInSearch && left.Name != nameInSearch)
                {
                    a = 1;
                }
                else
                {
                    if ((left.Type & 7) < (right.Type & 7))
                    {
                        a = -1;
                    }
                    else if ((left.Type & 7) > (right.Type & 7))
                    {
                        a = 1;
                    }
                    else
                    {
                        if ((left.Type >> 3) > (right.Type >> 3))
                        {
                            a = 1;
                        }
                        else if ((left.Type >> 3) < (right.Type >> 3))
                        {
                            a = -1;
                        }
                        else
                        {
                            if (left.Level > right.Level)
                            {
                                a = -1;
                            }
                            else if (left.Level < right.Level)
                            {
                                a = 1;
                            }
                            else
                            {
                                if (left.Attack > right.Attack)
                                {
                                    a = -1;
                                }
                                else if (left.Attack < right.Attack)
                                {
                                    a = 1;
                                }
                                else
                                {
                                    if (left.Attribute > right.Attribute)
                                    {
                                        a = 1;
                                    }
                                    else if (left.Attribute < right.Attribute)
                                    {
                                        a = -1;
                                    }
                                    else
                                    {
                                        if (left.Race > right.Race)
                                        {
                                            a = 1;
                                        }
                                        else if (left.Race < right.Race)
                                        {
                                            a = -1;
                                        }
                                        else
                                        {
                                            if (left.Category > right.Category)
                                            {
                                                a = 1;
                                            }
                                            else if (left.Category < right.Category)
                                            {
                                                a = -1;
                                            }
                                            else
                                            {
                                                if (left.Id > right.Id)
                                                {
                                                    a = 1;
                                                }
                                                else if (left.Id < right.Id)
                                                {
                                                    a = -1;
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
                return a;
            };
        }

    }

    internal static class PacksManager
    {
        public class packName
        {
           public string fullName;
            public string shortName;
            public int year;
            public int month;
            public int day;
        }

        public static List<packName> packs = new List<packName>();

        static Dictionary<string, string> pacDic = new Dictionary<string, string>();

        internal static void initialize(string databaseFullPath)
        {
            using (SqliteConnection connection = new SqliteConnection("Data Source=" + databaseFullPath))
            {
                connection.Open();
                using (IDbCommand command = new SqliteCommand("SELECT pack.* FROM pack;", connection))
                {
                    using (IDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            try
                            {
                                int Id = (int)reader.GetInt64(0);
                                // 指名 OCG 池：pack/*.db 是 OCG 的卡包数据，与当前模式无关。
                                Card c = CardsManager.GetCardRawInPool(false, Id);
                                if (c != null)
                                {
                                    string temp = reader.GetString(1);
                                    c.packFullName = reader.GetString(2);
                                    string[] mats = temp.Split("-");
                                    if (mats.Length > 1)
                                        c.packShortNam = mats[0];
                                    else
                                        c.packShortNam = c.packFullName.Length > 10 ? c.packFullName.Substring(0, 10) + "..." : c.packFullName;
                                    c.reality = reader.GetString(3);
                                    temp = reader.GetString(4);
                                    mats = temp.Split("/");
                                    if (mats.Length == 3)
                                    {
                                        c.month = int.Parse(mats[0]);
                                        c.day = int.Parse(mats[1]);
                                        c.year = int.Parse(mats[2]);
                                    }
                                    mats = temp.Split("-");
                                    if (mats.Length == 3)
                                    {
                                        c.year = int.Parse(mats[0]);
                                        c.month = int.Parse(mats[1]);
                                        c.day = int.Parse(mats[2]);
                                    }
                                    c.packFullName = c.year + "-" + c.month.ToString().PadLeft(2, '0') + "-" + c.day.ToString().PadLeft(2, '0') + " " + c.packShortNam;
                                    if (!pacDic.ContainsKey(c.packFullName))    
                                    {
                                        pacDic.Add(c.packFullName, c.packShortNam);
                                        packName p = new packName();
                                        p.day = c.day;
                                        p.year = c.year;
                                        p.month = c.month;
                                        p.fullName = c.packFullName;
                                        p.shortName = c.packShortNam;
                                        packs.Add(p);
                                    }
                                }
                            }
                            catch (Exception)
                            {

                            }
                        }
                    }
                }
            }
        }

        internal static void initializeSec()
        {
            packs.Sort((left, right) => {
                if (left.year > right.year)
                {
                    return -1;
                }
                if (left.year < right.year)
                {
                    return 1;
                }
                if (left.month > right.month)
                {
                    return -1;
                }
                if (left.month < right.month)
                {
                    return 1;
                }
                if (left.day > right.day)
                {
                    return -1;
                }
                if (left.day < right.day)
                {
                    return 1;
                }
                return 1;
            });
        }

    }
}