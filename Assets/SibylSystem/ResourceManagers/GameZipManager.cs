using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Ionic.Zip;
public static class GameZipManager
{
    public static List<ZipFile> Zips = new List<ZipFile>();

    /// <summary>
    /// 释放某个目录下卡包的 zip 句柄，并从 Zips 里摘掉，返回释放个数。
    /// Ionic.Zip.ZipFile 打开文件后会一直持有句柄，且共享模式不允许删除，
    /// 于是「清空 expansions」时 File.Delete 会撞 ERROR_SHARING_VIOLATION（共享冲突）
    /// —— 是游戏自己锁自己，表现为「清理旧文件失败」。
    /// 替换卡包前必须先调本方法，否则必然失败。
    /// </summary>
    public static int ReleaseZipsUnder(string folder)
    {
        string prefix = NormalizeFolderPrefix(folder);
        if (prefix == null)
        {
            return 0;
        }

        List<ZipFile> keep = new List<ZipFile>();
        List<ZipFile> release = new List<ZipFile>();
        foreach (ZipFile zip in Zips)
        {
            if (zip == null)
            {
                continue;
            }

            string name = GetZipName(zip);
            if (name != null &&
                name.Replace('\\', '/').StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                release.Add(zip);
            }
            else
            {
                keep.Add(zip);
            }
        }

        if (release.Count == 0)
        {
            return 0;
        }

        Zips.Clear();
        Zips.AddRange(keep);
        foreach (ZipFile zip in release)
        {
            try
            {
                zip.Dispose();
            }
            catch (Exception e)
            {
                Debug.LogError("[GameZipManager] 释放卡包句柄失败：" + e.Message);
            }
        }
        return release.Count;
    }

    /// <summary>
    /// 把某个目录下的 *.ypk 重新装进 Zips，返回打开个数。
    /// 插到列表最前面，保持与启动时相同的加载顺序（expansions 先于 data），
    /// 否则 Program.ReloadGameDatabases 重放 zip 内容时的覆盖次序会变。
    /// </summary>
    public static int OpenZipsUnder(string folder)
    {
        if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder))
        {
            return 0;
        }

        List<ZipFile> opened = new List<ZipFile>();
        foreach (FileInfo file in new DirectoryInfo(folder).GetFiles())
        {
            if (!file.Name.ToLower().EndsWith(".ypk"))
            {
                continue;
            }

            string path = folder.Replace('\\', '/') + "/" + file.Name;
            if (ContainsZip(path))
            {
                continue;
            }

            try
            {
                opened.Add(new ZipFile(path));
            }
            catch (Exception e)
            {
                Debug.LogError("[GameZipManager] 打开卡包失败 " + path + "：" + e.Message);
            }
        }

        if (opened.Count > 0)
        {
            Zips.InsertRange(0, opened);
        }
        return opened.Count;
    }

    private static bool ContainsZip(string path)
    {
        string want = path.Replace('\\', '/');
        foreach (ZipFile zip in Zips)
        {
            string name = GetZipName(zip);
            if (name != null &&
                string.Equals(name.Replace('\\', '/'), want, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
        return false;
    }

    private static string GetZipName(ZipFile zip)
    {
        if (zip == null)
        {
            return null;
        }
        try
        {
            return zip.Name;
        }
        catch
        {
            return null;
        }
    }

    private static string NormalizeFolderPrefix(string folder)
    {
        if (string.IsNullOrEmpty(folder))
        {
            return null;
        }
        string trimmed = folder.Replace('\\', '/').TrimEnd('/');
        return trimmed.Length == 0 ? null : trimmed + "/";
    }
}
