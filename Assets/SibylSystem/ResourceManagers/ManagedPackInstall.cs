using System;
using System.IO;
using System.Security.Cryptography;
using Ionic.Zip;

/// <summary>只替换指定官方包；ready 存在时，下次启动恢复旧包。</summary>
public static class ManagedPackInstall
{
    public static bool SameContent(string source, string target)
    {
        if (!File.Exists(source) || !File.Exists(target)) return false;
        if (new FileInfo(source).Length != new FileInfo(target).Length) return false;
        using (var sha = SHA256.Create())
        using (var left = File.OpenRead(source))
        using (var right = File.OpenRead(target))
            return BitConverter.ToString(sha.ComputeHash(left)) == BitConverter.ToString(sha.ComputeHash(right));
    }
    public static void Validate(string source)
    {
        using (ZipFile zip = ZipFile.Read(source))
        {
            if (zip.Count == 0) throw new InvalidDataException("卡包为空。");
            foreach (ZipEntry entry in zip)
                if (!entry.IsDirectory) entry.Extract(Stream.Null);
        }
    }

    public static void Begin(string source, string target)
    {
        Recover(target);
        Validate(source);
        string work = target + ".install";
        Directory.CreateDirectory(work);
        bool existed = File.Exists(target);
        File.Copy(source, Path.Combine(work, "new"), true);
        if (existed) File.Copy(target, Path.Combine(work, "old"), true);
        // 完整新包与旧包备份写完之后才登记恢复点；之后所有失败可重试恢复。
        File.WriteAllText(Path.Combine(work, "ready"), existed ? "1" : "0");
        File.Copy(Path.Combine(work, "new"), target, true);
    }

    public static void Recover(string target)
    {
        string work = target + ".install";
        string ready = Path.Combine(work, "ready");
        if (!File.Exists(ready))
        {
            if (Directory.Exists(work)) Directory.Delete(work, true);
            return;
        }
        string state = File.ReadAllText(ready);
        if (state == "1") File.Copy(Path.Combine(work, "old"), target, true);
        else if (state == "0")
        {
            if (File.Exists(target)) File.Delete(target);
        }
        else throw new InvalidDataException("卡包恢复记录损坏：" + ready);
        Commit(target);
    }

    public static bool Commit(string target)
    {
        string work = target + ".install";
        File.Delete(Path.Combine(work, "ready"));
        // ready 删除即提交点。清理失败保留的目录不会触发回滚。
        try { if (Directory.Exists(work)) Directory.Delete(work, true); }
        catch (IOException) { return false; }
        catch (UnauthorizedAccessException) { return false; }
        return true;
    }
}
