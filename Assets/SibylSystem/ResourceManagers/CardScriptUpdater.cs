using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Ionic.Zip;
using Mono.Data.Sqlite;

// 补齐官方卡表已收录、但旧基础包/扩展包尚未提供的效果脚本。
// 仅补缺：不替换用户脚本，不修改 RD 规则或覆盖基础辅助函数。
public static class CardScriptUpdater
{
    public static int Added { get; private set; }
    public static int Missing { get; private set; }
    public static string LastError { get; private set; }
    const string Source = "https://cdntx2.moecube.com/koishipro/content/script/";

    const string LegacyTypeCompatibility = "\n-- maintenance-card-type-alias-v1\n"
        + "if Card.IsAllCardTypes == nil and type(Card.IsAllTypes) == 'function' then\n"
        + "    Card.IsAllCardTypes = Card.IsAllTypes\nend\n";

    // Card type is independent of the current on-field type (e.g. an equipped
    // monster). Prefer the native API; old cores need the printed type plus
    // explicit card-type effects, ordered by effect ID, not EFFECT_CHANGE_TYPE.
    public const string TypeCompatibility = @"
-- maintenance-card-type-compat-v2
do
    local resolving = setmetatable({}, {__mode='k'})
    local function card_type(c)
        if Card.GetCardType then return c:GetCardType() end
        if resolving[c] ~= nil then return resolving[c] end
        local value = c:GetOriginalType()
        resolving[c] = value
        local effects = {}
        for _, code in pairs({EFFECT_ADD_CARD_TYPE, EFFECT_REMOVE_CARD_TYPE, EFFECT_CHANGE_CARD_TYPE}) do
            for _, effect in ipairs({c:IsHasEffect(code)}) do
                effects[#effects+1] = effect
            end
        end
        table.sort(effects, function(a,b) return a:GetFieldID() < b:GetFieldID() end)
        for _, effect in ipairs(effects) do
            local v = effect:GetValue()
            if type(v) == 'function' then v = v(effect,c) end
            local code = effect:GetCode()
            if code == EFFECT_ADD_CARD_TYPE then value = value | v
            elseif code == EFFECT_REMOVE_CARD_TYPE then value = value & ~v
            else value = v end
            resolving[c] = value
        end
        resolving[c] = nil
        return value
    end
    if Card.IsAllCardTypes == nil then
        function Card.IsAllCardTypes(c, mask) return (card_type(c) & mask) == mask end
    end
    if Card.IsCardType == nil then
        function Card.IsCardType(c, mask) return (card_type(c) & mask) ~= 0 end
    end
end
";

    // Run before ZIP readers open, so the client core and the separate AI server
    // see the same helper. The original archive is retained for rollback.
    public static bool EnsureTypeCompatibility(string root)
    {
        string path = Path.Combine(root, "data/script.zip");
        ManagedPackInstall.Recover(path);
        if (!File.Exists(path)) return false;
        string staged = path + ".type-compat.tmp";
        try
        {
            using (var zip = ZipFile.Read(path))
            {
                ZipEntry entry = zip["script/utility.lua"];
                if (entry == null) throw new InvalidDataException("基础脚本包缺少 utility.lua。");
                byte[] original;
                using (var stream = new MemoryStream())
                {
                    entry.Extract(stream);
                    original = stream.ToArray();
                }
                string text = System.Text.Encoding.UTF8.GetString(original);
                if (text.EndsWith(TypeCompatibility, StringComparison.Ordinal)) return false;
                // Only retire our exact v1 alias; retain native/custom methods.
                string migration = text.Contains(LegacyTypeCompatibility)
                    ? "\nif Card.IsAllCardTypes == Card.IsAllTypes then Card.IsAllCardTypes = nil end\n" : "";
                byte[] suffix = System.Text.Encoding.UTF8.GetBytes(migration + TypeCompatibility);
                byte[] updated = new byte[original.Length + suffix.Length];
                Buffer.BlockCopy(original, 0, updated, 0, original.Length);
                Buffer.BlockCopy(suffix, 0, updated, original.Length, suffix.Length);
                zip.UpdateEntry("script/utility.lua", updated);
                zip.Save(staged);
            }
            ManagedPackInstall.Validate(staged);
            string fingerprint;
            using (var sha = System.Security.Cryptography.SHA256.Create())
            using (var stream = File.OpenRead(path))
                fingerprint = BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
            string backup = Path.Combine(root, "updates/script-compatibility", fingerprint + ".zip");
            Directory.CreateDirectory(Path.GetDirectoryName(backup));
            if (!File.Exists(backup)) File.Copy(path, backup);
            else if (!ManagedPackInstall.SameContent(path, backup))
                throw new InvalidDataException("脚本兼容备份校验失败。");
            ManagedPackInstall.Begin(staged, path);
            ManagedPackInstall.Commit(path);
            return true;
        }
        finally
        {
            if (File.Exists(staged)) File.Delete(staged);
        }
    }

    public static bool Validate(string path)
    {
        try
        {
            var info = new FileInfo(path);
            if (!info.Exists || info.Length == 0 || info.Length > 512 * 1024) return false;
            string text = File.ReadAllText(path);
            return !text.TrimStart(' ', '\r', '\n', '\t').StartsWith("<", StringComparison.Ordinal)
                && text.IndexOf("initial_effect", StringComparison.Ordinal) >= 0;
        }
        catch { return false; }
    }

    public static HashSet<string> CollectAvailable(string root)
    {
        var available = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (string folder in new[] { "script", "expansions/script", "expansions" })
        {
            string path = Path.Combine(root, folder);
            if (!Directory.Exists(path)) continue;
            foreach (string file in Directory.GetFiles(path, "c*.lua"))
                if (new FileInfo(file).Length > 0) available.Add(Path.GetFileName(file));
        }
        var archives = new List<string>();
        string baseZip = Path.Combine(root, "data/script.zip");
        if (File.Exists(baseZip)) archives.Add(baseZip);
        string expansions = Path.Combine(root, "expansions");
        if (Directory.Exists(expansions)) archives.AddRange(Directory.GetFiles(expansions, "*.ypk"));
        foreach (string archive in archives)
            using (var zip = ZipFile.Read(archive))
                foreach (ZipEntry entry in zip)
                    if (!entry.IsDirectory && entry.UncompressedSize > 0
                        && entry.FileName.Replace('\\', '/').StartsWith("script/", StringComparison.OrdinalIgnoreCase)
                        && entry.FileName.Replace('\\', '/').IndexOf('/', 7) < 0)
                        available.Add(Path.GetFileName(entry.FileName));
        return available;
    }

    public static List<string> MissingFromCardTypes(IEnumerable<KeyValuePair<long, long>> cards, HashSet<string> available)
    {
        var missing = new List<string>();
        foreach (var card in cards)
        {
            long type = card.Value;
            if ((type & 16384) != 0 || ((type & 16) != 0 && (type & 16777216) == 0)) continue;
            string name = "c" + card.Key + ".lua";
            if (!available.Contains(name)) missing.Add(name);
        }
        return missing;
    }

    public static List<string> FindMissing(string root)
    {
        var cards = new List<KeyValuePair<long, long>>();
        using (var db = new SqliteConnection("Data Source=" + Path.Combine(root, "cdb/cards.cdb")))
        {
            db.Open();
            using (var command = db.CreateCommand())
            {
                // 普通怪兽/衍生物没有效果脚本；通常怪兽的灵摆效果仍需要脚本。
                command.CommandText = "SELECT id,type,alias FROM datas ORDER BY id";
                using (var reader = command.ExecuteReader())
                    while (reader.Read())
                    {
                        long code = reader.GetInt64(0), alias = reader.GetInt64(2);
                        cards.Add(new KeyValuePair<long, long>(ScriptCode(code, alias), reader.GetInt64(1)));
                    }
            }
        }
        return new List<string>(new HashSet<string>(MissingFromCardTypes(cards, CollectAvailable(root))));
    }

    public static long ScriptCode(long code, long alias)
    {
        // Match this project's ocgcore/card_data.h, including the BLS exception.
        return code != 5405695 && alias > 0 && Math.Abs(code - alias) < 20 ? alias : code;
    }

    sealed class Job
    {
        public string root;
        public List<string> files;
        public int next, done, failures, workers;
    }

    public static IEnumerator Repair(string root)
    {
        Added = 0;
        Missing = 0;
        LastError = "";
        List<string> files;
        try { files = FindMissing(root); }
        catch (Exception error)
        {
            LastError = "脚本检查失败：" + error.Message;
            yield break;
        }
        Missing = files.Count;
        if (Missing == 0) yield break;
        QuickTestTrace.Log("scripts", "missing=" + Missing);
        var job = new Job { root = root, files = files };
        int lanes = Math.Min(4, Missing);
        for (int i = 0; i < lanes; i++) Program.I().StartCoroutine(Lane(job));
        while (job.workers < lanes) yield return null;
        Missing -= Added;
        if (Missing > 0) LastError = "还有 " + Missing + " 个效果脚本未补齐，请稍后重试资源更新。";
        QuickTestTrace.Log("scripts", "added=" + Added + " missing=" + Missing);
    }

    static IEnumerator Lane(Job job)
    {
        try
        {
            while (job.next < job.files.Count && job.failures < 5)
            {
                // 对局期间不启动新的补缺请求。
                if (Program.I().menu == null || !Program.I().menu.isShowed)
                {
                    yield return null;
                    continue;
                }
                string name = job.files[job.next++];
                string path = Path.Combine(job.root, "script", name);
                bool success = false;
                long status = 0;
                yield return Program.I().StartCoroutine(UnityFileDownloader.DownloadFileAsync(
                    Source + name, path, ok => success = ok, null, Validate,
                    error => QuickTestTrace.Log("scripts", name + ": " + error),
                    code => status = code, () => File.Exists(path)));
                job.done++;
                if (success) { Added++; job.failures = 0; }
                else if (status != 404 && status != 410) job.failures++;
            }
        }
        finally { job.workers++; }
    }
}
