using System.Collections.Generic;
using System.IO;

namespace YGOSharp
{
    public static class BanlistManager
    {
        public static List<Banlist> Banlists { get; private set; }

        public static void initialize(string fileName)
        {
            Banlists = new List<Banlist>();
            ParseInto(Banlists, fileName);
            Banlist sentinel = new Banlist();
            sentinel.Name = "N/A";
            Banlists.Add(sentinel);
        }

        /// <summary>
        /// 往已装载的表清单里**追加**另一个来源（RD 的 rd/lflist.conf）。
        ///
        /// 为什么不在 initialize 之前合文件：RD 表要与 OCG 的几十张表并存，而 initialize 是清空重来。
        /// 末尾那个 "N/A" 兜底表必须**始终留在最后** —— GetByName / GetByHash 找不到时回落的是
        /// 「最后一个元素」，追加进来的 RD 表要是排到它后面，所有未命中的查询会静默落到 RD 表上。
        /// </summary>
        public static void Append(string fileName)
        {
            if (Banlists == null)
            {
                initialize(fileName);
                return;
            }
            Banlist sentinel = null;
            if (Banlists.Count > 0 && Banlists[Banlists.Count - 1].Name == "N/A")
            {
                sentinel = Banlists[Banlists.Count - 1];
                Banlists.RemoveAt(Banlists.Count - 1);
            }
            ParseInto(Banlists, fileName);
            if (sentinel != null)
            {
                Banlists.Add(sentinel);
            }
        }

        /// <summary>文件存在才追加。rd/lflist.conf 是随包分发的数据，缺了不该让启动失败。</summary>
        public static void AppendIfExists(string fileName)
        {
            if (File.Exists(fileName))
            {
                Append(fileName);
            }
        }

        /// <summary>把一份 lflist.conf 的表节解析进给定清单（initialize / Append 共用这一处）。</summary>
        private static void ParseInto(List<Banlist> into, string fileName)
        {
            Banlist current = null;
            StreamReader reader = new StreamReader(fileName);
            while (!reader.EndOfStream)
            {
                string line = reader.ReadLine();
                try
                {
                    if (line == null)
                        continue;
                    if (line.StartsWith("#"))
                        continue;
                    if (line.StartsWith("!"))
                    {
                        current = new Banlist();
                        current.Name = line.Substring(1, line.Length - 1);
                        into.Add(current);
                        continue;
                    }
                    if (!line.Contains(" "))
                        continue;
                    if (current == null)
                        continue;
                    string[] data = line.Split(new char[] {  ' '  }, System.StringSplitOptions.RemoveEmptyEntries);
                    int id = int.Parse(data[0]);
                    int count = int.Parse(data[1]);
                    current.Add(id, count);
                }
                catch (System.Exception e)  
                {
                    UnityEngine.Debug.Log(line);
                    UnityEngine.Debug.Log(e);
                }
            }
        }

        /// <summary>清空已载入的禁限表，供数据更新后整体重载（见 Program.ReloadGameDatabases）。</summary>
        public static void Reset()
        {
            Banlists = new List<Banlist>();
        }

        /// <summary>
        /// 只做解析校验、不写入内存：确认这份 lflist.conf 至少含一张以 ! 开头的表，
        /// 且每条限制行都是两个合法整数。用于校验刚下载到 .tmp 的禁限表。
        /// </summary>
        public static bool Validate(string fileName)
        {
            bool success = true;
            bool found = false;
            try
            {
                using (StreamReader reader = new StreamReader(fileName))
                {
                    while (!reader.EndOfStream)
                    {
                        string line = reader.ReadLine();
                        try
                        {
                            if (line == null)
                                continue;
                            if (line.StartsWith("#"))
                                continue;
                            if (line.StartsWith("!"))
                            {
                                found = true;
                                continue;
                            }
                            if (!line.Contains(" "))
                                continue;
                            if (!found)
                                continue;
                            string[] data = line.Split(new char[] { ' ' }, System.StringSplitOptions.RemoveEmptyEntries);
                            int.Parse(data[0]);
                            int.Parse(data[1]);
                        }
                        catch (System.Exception)
                        {
                            success = false;
                        }
                    }
                }
            }
            catch (System.Exception)
            {
                return false;
            }
            return success && found;
        }

        public static int GetIndex(uint hash)
        {
            for (int i = 0; i < Banlists.Count; i++)
                if (Banlists[i].Hash == hash)
                    return i;
            return 0;
        }

        public static string GetName(uint hash)    
        {
            for (int i = 0; i < Banlists.Count; i++)
                if (Banlists[i].Hash == hash)
                    return Banlists[i].Name;
            return InterString.Get("未知卡表");
        }

        public static List<string> getAllName()
        {
            List<string> returnValue = new List<string>();
            foreach (var item in Banlists)
            {
                returnValue.Add(item.Name);
            }
            return returnValue;
        }

        public static Banlist GetByName(string name)
        {
            Banlist returnValue = Banlists[Banlists.Count - 1];
            foreach (var item in Banlists)
            {
                if (item.Name == name)
                {
                    returnValue = item;
                }
            }
            return returnValue;
        }

        public static Banlist GetByHash(uint hash)
        {
            Banlist returnValue = Banlists[Banlists.Count - 1];
            foreach (var item in Banlists)
            {
                if (item.Hash == hash)
                {
                    returnValue = item;
                }
            }
            return returnValue;
        }

    }
}