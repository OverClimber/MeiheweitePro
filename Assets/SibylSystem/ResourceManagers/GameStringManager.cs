using System;
using System.Collections.Generic;
using UnityEngine;
public static class GameStringManager
{
    public class hashedString
    {
        public string region = "";
        public int hashCode = 0;
        public string content = "";
    }

    public static List<hashedString> hashedStrings = new List<hashedString>();

    public static List<hashedString> xilies = new List<hashedString>();

    public static int helper_stringToInt(string str)
    {
        int return_value = 0;
        try
        {
            if (str.Length > 2 && str.Substring(0, 2) == "0x")
            {
                return_value = Convert.ToInt32(str, 16);
            }
            else
            {
                return_value = Int32.Parse(str);
            }
        }
        catch (Exception)
        {
        }
        return return_value;
    }

    /// <summary>清空已载入的文本表，供数据更新后整体重载（见 Program.ReloadGameDatabases）。</summary>
    public static void Reset()
    {
        hashedStrings = new List<hashedString>();
        xilies = new List<hashedString>();
    }

    public static void initialize(string path)
    {
        string text = System.IO.File.ReadAllText(path);
        initializeContent(text);
    }

    /// <summary>
    /// 只做解析校验、不写入内存：确认这份 conf 能读出至少一条有效条目。
    /// 用于在线更新时校验刚下载到 .tmp 的 strings.conf。
    /// </summary>
    public static bool Validate(string path)
    {
        try
        {
            string text = System.IO.File.ReadAllText(path);
            return ParseContent(text, false) > 0;
        }
        catch (Exception)
        {
            return false;
        }
    }

    public static void initializeContent(string text)
    {
        ParseContent(text, true);
    }

    /// <summary>
    /// 文本表解析本体。apply=false 时只统计可解析条目数（校验用），不碰 hashedStrings/xilies。
    /// 返回解析出的条目数。
    /// </summary>
    static int ParseContent(string text, bool apply)
    {
        int parsed = 0;
        string st = text.Replace("\r", "");
        string[] lines = st.Split(new string[] { "\n" }, StringSplitOptions.RemoveEmptyEntries);
        foreach (string line in lines)
        {
            if (line.Length > 1 && line.Substring(0, 1) == "!")
            {
                string[] mats = line.Substring(1, line.Length - 1).Split(new string[] { " " }, StringSplitOptions.RemoveEmptyEntries);
                if (mats.Length > 2)
                {
                    hashedString a = new hashedString();
                    a.region = mats[0];
                    try
                    {
                        a.hashCode = helper_stringToInt(mats[1]);
                    }
                    catch (Exception e)
                    {
                        Program.DEBUGLOG(e);
                    }
                    a.content = "";
                    for (int i = 2; i < mats.Length; i++)
                    {
                        a.content += mats[i] + " ";
                    }
                    a.content = a.content.Substring(0, a.content.Length - 1);
                    parsed++;
                    if (!apply)
                    {
                        continue;
                    }
                    if (get(a.region, a.hashCode) == "")
                    {
                        hashedStrings.Add(a);
                        if (a.region == "setname")
                        {
                            xilies.Add(a);
                        }
                    }
                }
            }
        }
        return parsed;
    }

    public static string get(string region, int hashCode)
    {
        string re = "";
        foreach (hashedString s in hashedStrings)
        {
            if (s.region == region && s.hashCode == hashCode)
            {
                re = s.content;
                break;
            }
        }
        return re;
    }

    internal static string get_unsafe(int hashCode)
    {
        string re = "";
        foreach (hashedString s in hashedStrings)
        {
            if (s.region == "system" && s.hashCode == hashCode)
            {
                re = s.content;
                break;
            }
        }
        return re;
    }

    internal static string get(int description)
    {
        string a = "";
        if (description < 10000)
        {
            a = get("system", (int)description);
        }
        else
        {
            int code = description >> 4;
            int index = description & 0xf;
            try
            {
                a = YGOSharp.CardsManager.Get(code).Str[index];
            }
            catch (Exception e)
            {
                Program.DEBUGLOG(e);
            }
        }
        if (a == "")
            a = "???";
        return a;
    }

    internal static string formatLocation(uint location, uint sequence)
    {
        if (location == 0x8)
        {
            if (sequence < 5)
                return get(1003);
            else if (sequence == 5)
                return get(1008);
            else
                return get(1009);
        }
        uint filter = 1;
        int i = 1000;
        for (; filter != 0x100 && filter != location; filter <<= 1)
            ++i;
        if (filter == location)
            return get(i);
        else
            return "???";
    }
    internal static string formatLocation(GPS gps)
    {
        return formatLocation(gps.location, gps.sequence);
    }
}

