using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Ionic.Zip;

/// <summary>只读 ZIP 的图像索引。保持条目枚举顺序；句柄关闭后索引可随之回收。</summary>
public static class ZipImageIndex
{
    static readonly ConditionalWeakTable<ZipFile, Dictionary<string, ZipEntry>> indexes
        = new ConditionalWeakTable<ZipFile, Dictionary<string, ZipEntry>>();
    static readonly Regex image = new Regex(@"(pics|closeup)/([0-9]+(?:_2)?)\.(jpg|png)$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    static Dictionary<string, ZipEntry> Build(ZipFile zip)
    {
        var entries = new Dictionary<string, ZipEntry>(System.StringComparer.OrdinalIgnoreCase);
        foreach (string name in zip.EntryFileNames)
        {
            Match match = image.Match(name);
            if (!match.Success) continue;
            string folder = match.Groups[1].Value.ToLowerInvariant();
            if (folder == "closeup" && match.Groups[3].Value.ToLowerInvariant() != "png") continue;
            string key = folder + "/" + match.Groups[2].Value;
            if (!entries.ContainsKey(key)) entries.Add(key, zip[name]);
        }
        return entries;
    }

    // 调用方串行化同一 ZIP 的读取与关闭；不在这里创建 Unity 对象。
    public static byte[] Read(ZipFile zip, string folder, string code)
    {
        ZipEntry entry;
        if (!indexes.GetValue(zip, Build).TryGetValue(folder + "/" + code, out entry)) return null;
        using (var stream = new MemoryStream())
        {
            entry.Extract(stream);
            return stream.ToArray();
        }
    }
}
