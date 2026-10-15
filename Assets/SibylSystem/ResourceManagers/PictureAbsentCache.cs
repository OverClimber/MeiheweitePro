using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

/// <summary>确认不存在的卡图逐条计时；不沿用旧版混入网络错误的缓存。</summary>
public static class PictureAbsentCache
{
    public static bool ShouldRememberMissing(bool success, long responseCode)
    {
        return !success && (responseCode == 404 || responseCode == 410);
    }

    public static Dictionary<string, long> Read(string path, DateTime now)
    {
        var entries = new Dictionary<string, long>();
        if (!File.Exists(path)) return entries;
        foreach (string line in File.ReadAllLines(path))
        {
            string[] parts = line.Split(new[] { '|' });
            long code, ticks;
            if (parts.Length != 2
                || !long.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out code)
                || code <= 0
                || !long.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out ticks)
                || ticks <= 0 || ticks > now.Ticks
                || now.Ticks - ticks >= TimeSpan.FromDays(30).Ticks) continue;
            entries[parts[0]] = ticks;
        }
        return entries;
    }

    public static void Save(string path, IEnumerable<string> absent, DateTime now)
    {
        var entries = Read(path, now);
        foreach (string code in absent) entries[code] = now.Ticks;
        var codes = new List<string>(entries.Keys);
        codes.Sort(StringComparer.Ordinal);
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
        var lines = new List<string>();
        foreach (string code in codes)
            lines.Add(code + "|" + entries[code].ToString(CultureInfo.InvariantCulture));
        string temp = path + ".tmp";
        File.WriteAllLines(temp, lines.ToArray());
        if (File.Exists(path)) File.Replace(temp, path, null);
        else File.Move(temp, path);
    }
}
