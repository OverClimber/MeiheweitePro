using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Ionic.Zip;

public static class MaintenanceTests
{
    static int assertions;
    static void Check(bool condition, string name)
    {
        if (!condition) throw new Exception("FAIL: " + name);
        assertions++;
        Console.WriteLine("PASS: " + name);
    }
    static void Write(string path, string text)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        File.WriteAllText(path, text);
    }
    static void Pack(string path, string value)
    {
        using (var zip = new ZipFile()) { zip.AddEntry("pics/1.jpg", Encoding.UTF8.GetBytes(value)); zip.Save(path); }
    }
    static void Cache(string root)
    {
        string path = Path.Combine(root, "absent.txt");
        DateTime start = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        PictureAbsentCache.Save(path, new[] { "1" }, start);
        PictureAbsentCache.Save(path, new[] { "2" }, start.AddDays(20));
        var rows = PictureAbsentCache.Read(path, start.AddDays(30));
        Check(!rows.ContainsKey("1") && rows.ContainsKey("2"), "per-card expiration is not extended by new entries");
        File.AppendAllText(path, "\n3\n4|junk\n5|" + start.AddDays(50).Ticks);
        Check(PictureAbsentCache.Read(path, start.AddDays(30)).Count == 1, "legacy, malformed and future entries ignored");
        Check(PictureAbsentCache.ShouldRememberMissing(false, 404)
            && PictureAbsentCache.ShouldRememberMissing(false, 410), "only confirmed missing responses enter negative cache");
        Check(!PictureAbsentCache.ShouldRememberMissing(false, 0)
            && !PictureAbsentCache.ShouldRememberMissing(false, 503)
            && !PictureAbsentCache.ShouldRememberMissing(false, 200)
            && !PictureAbsentCache.ShouldRememberMissing(true, 404), "network, server, installation failures and success do not enter negative cache");
    }
    static void Packs(string root)
    {
        string folder = Path.Combine(root, "expansions");
        string target = Path.Combine(folder, "official.ypk");
        string custom = Path.Combine(folder, "custom.ypk");
        string source = Path.Combine(root, "new.ypk");
        Directory.CreateDirectory(folder);
        Pack(target, "old"); Pack(custom, "user"); Pack(source, "new");
        byte[] old = File.ReadAllBytes(target), user = File.ReadAllBytes(custom);
        ManagedPackInstall.Begin(source, target);
        Check(File.ReadAllBytes(target).SequenceEqual(File.ReadAllBytes(source)), "new pack installed");
        ManagedPackInstall.Recover(target);
        Check(File.ReadAllBytes(target).SequenceEqual(old), "interrupted installation restores old pack");
        Check(File.ReadAllBytes(custom).SequenceEqual(user), "custom pack preserved");
        ManagedPackInstall.Begin(source, target); ManagedPackInstall.Commit(target); ManagedPackInstall.Recover(target);
        Check(File.ReadAllBytes(target).SequenceEqual(File.ReadAllBytes(source)), "committed pack survives restart");
        Write(source, "broken zip");
        bool failed = false;
        try { ManagedPackInstall.Begin(source, target); } catch { failed = true; }
        Check(failed && File.ReadAllBytes(target).Length > 20, "invalid zip cannot destroy installed pack");
        Pack(source, "fresh"); string first = Path.Combine(folder, "first.ypk");
        ManagedPackInstall.Begin(source, first); ManagedPackInstall.Recover(first);
        Check(!File.Exists(first), "failed first installation removes only its new pack");
    }
    static void Boot(string root)
    {
        string data = Path.Combine(root, "boot"), stage = Path.Combine(data, ".boot-install/stage");
        string picture = Path.Combine(data, "picture/card/1.jpg");
        Write(picture, "user HD"); Write(Path.Combine(stage, "picture/card/1.jpg"), "bundled low resolution");
        Write(Path.Combine(stage, "cdb/cards.cdb"), "new db");
        BootDataInstall.Commit(data, stage, "3");
        Check(File.ReadAllText(picture) == "user HD", "bootstrap preserves existing pictures");
        Check(File.Exists(Path.Combine(data, "cdb/cards.cdb")), "bootstrap installs missing data");
        Check(File.ReadAllText(Path.Combine(data, "gamedata.version")) == "3", "bootstrap marks successful commit");
        bool rejected = false;
        try { BootDataInstall.SafePath(data, "../escape"); } catch (InvalidDataException) { rejected = true; }
        Check(rejected, "bootstrap rejects traversal");
        // Simulate a crash after a payload file and marker were written, before ready was removed.
        string created = Path.Combine(data, "new.txt"); Write(created, "payload");
        string hash;
        using (var sha = SHA256.Create()) hash = BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(created))).Replace("-", "");
        Write(Path.Combine(data, ".boot-install/ready"), hash + "|new.txt");
        Write(Path.Combine(data, ".boot-install/old-version"), "2");
        BootDataInstall.Recover(data);
        Check(!File.Exists(created) && File.ReadAllText(Path.Combine(data, "gamedata.version")) == "2", "bootstrap interruption restores marker and removes created payload");
        Write(created, "user changed"); Write(Path.Combine(data, ".boot-install/ready"), hash + "|new.txt");
        BootDataInstall.Recover(data);
        Check(File.ReadAllText(created) == "user changed", "recovery preserves files changed by user");
    }
    static void ZipLookup(string root)
    {
        string path = Path.Combine(root, "images.zip");
        using (var zip = new ZipFile())
        {
            zip.AddEntry("nested/PICS/42.JPG", new byte[] { 1 });
            zip.AddEntry("pics/42.png", new byte[] { 2 });
            zip.AddEntry("closeup/42.jpg", new byte[] { 3 });
            zip.AddEntry("nested/closeup/42_2.png", new byte[] { 4 });
            zip.Save(path);
        }
        using (var zip = ZipFile.Read(path))
        {
            Check(ZipImageIndex.Read(zip, "pics", "42")[0] == 1, "ZIP precedence and case-insensitive nested path preserved");
            Check(ZipImageIndex.Read(zip, "closeup", "42") == null, "closeup remains PNG-only");
            Check(ZipImageIndex.Read(zip, "closeup", "42_2")[0] == 4, "maximum monster single variant supported");
            Check(ZipImageIndex.Read(zip, "pics", "missing") == null, "missing indexed picture falls back");
        }
    }
    static void Generation()
    {
        // Exercise the production queue without creating textures or starting the native game.
        Type manager = typeof(GameTextureManager);
        Type picture = manager.GetNestedType("PictureResource", BindingFlags.NonPublic);
        var constructor = picture.GetConstructors()[0];
        var generation = manager.GetField("cacheGeneration", BindingFlags.Static | BindingFlags.NonPublic);
        var publish = manager.GetMethod("Publish", BindingFlags.Static | BindingFlags.NonPublic);
        var drain = manager.GetMethod("DrainCompleted", BindingFlags.Static | BindingFlags.NonPublic);
        var loaded = manager.GetField("loadedList", BindingFlags.Static | BindingFlags.NonPublic);
        GameTextureManager.clearAll();
        object old = constructor.Invoke(new object[] { GameTextureType.card_picture, 1L, null });
        picture.GetField("generation").SetValue(old, generation.GetValue(null));
        GameTextureManager.clearAll();
        publish.Invoke(null, new[] { old }); drain.Invoke(null, null);
        Check(((System.Collections.IDictionary)loaded.GetValue(null)).Count == 0, "stale worker result rejected after clear");
        object fresh = constructor.Invoke(new object[] { GameTextureType.card_picture, 2L, null });
        picture.GetField("generation").SetValue(fresh, generation.GetValue(null));
        publish.Invoke(null, new[] { fresh }); drain.Invoke(null, null);
        Check(((System.Collections.IDictionary)loaded.GetValue(null)).Count == 1, "current worker result published");
        GameTextureManager.clearUnloaded();
        Check(((System.Collections.IDictionary)loaded.GetValue(null)).Count == 1, "cancel pending retains loaded pictures");
        GameTextureManager.clearAll();
    }
    public static int Main(string[] args)
    {
        string root = args[0];
        try { ReloadFieldBoundary(); ClassicChain(); Cache(root); Packs(root); Boot(root); ZipLookup(root); Generation(); ScriptCoverage(root); ScriptCompatibility(root); ConnectionCloseRace(); DrainCleanup(root);
            int ready = 0;
            Check(!AndroidBoot.Boot(() => ready++).MoveNext() && ready == 1,
                "editor bootstrap iterator completes and invokes ready exactly once");
            QuickTestTrace.ResetForCurrentDirectory();
            Check((int)typeof(QuickTestTrace).GetField("enabled", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null) == -1,
                "trace state is invalidated after resource directory changes");
            Console.WriteLine("Assertions passed: " + assertions); return 0; }
        catch (Exception e) { Console.Error.WriteLine(e); return 1; }
    }

    static void ReloadFieldBoundary()
    {
        var method = typeof(Percy.smallYgopro).GetMethod("CopyReloadField", BindingFlags.NonPublic | BindingFlags.Static);
        foreach (int chains in new[] { 0, 2 })
        {
            var packet = new MemoryStream();
            var write = new BinaryWriter(packet);
            write.Write((byte)4);
            for (int player = 0; player < 2; player++)
            {
                write.Write(8000);
                for (int slot = 0; slot < 7; slot++)
                {
                    write.Write((byte)(slot == 2 ? 1 : 0));
                    if (slot == 2) { write.Write((byte)1); write.Write((byte)0); }
                }
                for (int slot = 0; slot < 8; slot++)
                {
                    write.Write((byte)(slot == 0 ? 1 : 0));
                    if (slot == 0) write.Write((byte)1);
                }
                write.Write(new byte[] { 10, 0, 0, 0, 0, 0 });
            }
            write.Write((byte)chains);
            for (int i = 0; i < chains * 15; i++) write.Write((byte)(i + 1));
            byte[] body = packet.ToArray();
            write.Write((byte)93); // Next message: Equip. Must remain unread.
            packet.Position = 0;
            var output = new MemoryStream();
            method.Invoke(null, new object[] { new BinaryReader(packet), new BinaryWriter(output) });
            Check(output.ToArray().SequenceEqual(body) && packet.ReadByte() == 93,
                "ReloadField preserves both players, chain list and next Equip: " + chains);
            bool rejected = false;
            try { method.Invoke(null, new object[] { new BinaryReader(new MemoryStream(body.Take(body.Length - 1).ToArray())), new BinaryWriter(new MemoryStream()) }); }
            catch (TargetInvocationException error) { rejected = error.InnerException is EndOfStreamException; }
            Check(rejected, "ReloadField rejects truncated packet: " + chains);
        }
    }

    static void ClassicChain()
    {
        for (int count = 0; count <= 3; count++)
        {
            byte[] packet = new byte[12 + count * 13];
            for (int i = 0; i < packet.Length; i++) packet[i] = (byte)i;
            packet[1] = (byte)count; packet[3] = 1;
            byte[] result = TcpHelper.NormalizeClassicSelectChain(packet);
            Check(result.Length == 11 + count * 14 && result.Take(3).SequenceEqual(packet.Take(3))
                && result.Skip(3).Take(8).SequenceEqual(packet.Skip(4).Take(8)), "classic chain header preserved count=" + count);
            for (int i = 0; i < count; i++)
                Check(result[11 + i * 14] == packet[12 + i * 13] && result[12 + i * 14] == 1
                    && result.Skip(13 + i * 14).Take(12).SequenceEqual(packet.Skip(13 + i * 13).Take(12)),
                    "classic chain entry and global forced flag preserved count=" + count + " entry=" + i);
        }
        bool rejected = false;
        try { TcpHelper.NormalizeClassicSelectChain(new byte[11]); } catch (InvalidDataException) { rejected = true; }
        Check(rejected, "truncated classic chain packet rejected");
    }

    static void ScriptCoverage(string root)
    {
        string folder = Path.Combine(root, "script-coverage");
        var cards = new System.Collections.Generic.Dictionary<long, long> {
            {1,33}, {2,17}, {3,16777233}, {4,33}, {5,33}, {6,16385} };
        Write(Path.Combine(folder, "script/c1.lua"), "function initial_effect(c) end");
        Directory.CreateDirectory(Path.Combine(folder, "data"));
        using (var zip = new ZipFile())
        {
            zip.AddEntry("script/c4.lua", "function initial_effect(c) end");
            zip.Save(Path.Combine(folder, "data/script.zip"));
        }
        Check(CardScriptUpdater.MissingFromCardTypes(cards, CardScriptUpdater.CollectAvailable(folder))
            .SequenceEqual(new[] { "c3.lua", "c5.lua" }),
            "script scan preserves loose/archive scripts, excludes normal monsters and includes pendulum effects");
        string script = Path.Combine(folder, "download.lua");
        Write(script, "<html>initial_effect</html>");
        Check(!CardScriptUpdater.Validate(script), "HTML error response rejected as a card script");
        Write(script, "function c5.initial_effect(c) end");
        Check(CardScriptUpdater.Validate(script), "effect script accepted");
        Check(CardScriptUpdater.ScriptCode(10000001, 10000000) == 10000000
            && CardScriptUpdater.ScriptCode(10000020, 10000000) == 10000020
            && CardScriptUpdater.ScriptCode(5405695, 5405694) == 5405695,
            "script aliases match the native artwork offset and BLS exception");
    }

    static void ScriptCompatibility(string root)
    {
        string folder = Path.Combine(root, "script-compatibility");
        Directory.CreateDirectory(Path.Combine(folder, "data"));
        string archive = Path.Combine(folder, "data/script.zip");
        byte[] helper = System.Text.Encoding.UTF8.GetBytes("-- user helper retained\ncustom_helper = 123\n"
            + "\n-- maintenance-card-type-alias-v1\n"
            + "if Card.IsAllCardTypes == nil and type(Card.IsAllTypes) == 'function' then\n"
            + "    Card.IsAllCardTypes = Card.IsAllTypes\nend\n");
        byte[] card = new byte[] { 0, 1, 2, 255 };
        using (var zip = new ZipFile())
        {
            zip.AddEntry("script/utility.lua", helper);
            zip.AddEntry("script/c123.lua", card);
            zip.Save(archive);
        }
        byte[] original = File.ReadAllBytes(archive);
        Check(CardScriptUpdater.EnsureTypeCompatibility(folder), "v1 helper migrates to card-type compatibility");
        using (var zip = ZipFile.Read(archive))
        using (var stream = new MemoryStream())
        {
            zip["script/c123.lua"].Extract(stream);
            Check(stream.ToArray().SequenceEqual(card), "compatibility preserves unrelated card bytes");
            stream.SetLength(0);
            zip["script/utility.lua"].Extract(stream);
            Check(stream.ToArray().Take(helper.Length).SequenceEqual(helper), "compatibility preserves original helper bytes");
            File.WriteAllBytes(Path.Combine(root, "type-compatibility.lua"), stream.ToArray().Skip(helper.Length).ToArray());
        }
        byte[] once = File.ReadAllBytes(archive);
        Check(!CardScriptUpdater.EnsureTypeCompatibility(folder) && File.ReadAllBytes(archive).SequenceEqual(once),
            "compatibility is byte-idempotent on the next startup");
        string backup = Directory.GetFiles(Path.Combine(folder, "updates/script-compatibility"), "*.zip").Single();
        Check(File.ReadAllBytes(backup).SequenceEqual(original), "compatibility retains the complete original archive");
    }

    static void ConnectionCloseRace()
    {
        Type state = typeof(TcpHelper).GetNestedType("ConnectionState", BindingFlags.NonPublic);
        object closed = System.Runtime.Serialization.FormatterServices.GetUninitializedObject(state);
        var cts = new System.Threading.CancellationTokenSource();
        state.GetField("Cts").SetValue(closed, cts);
        cts.Dispose();
        typeof(TcpHelper).GetMethod("SenderLoop", BindingFlags.Static | BindingFlags.NonPublic)
            .Invoke(null, new[] { closed });
        Check(true, "sender started after cancellation source disposal cannot escape with an unhandled exception");
    }

    static void DrainCleanup(string root)
    {
        Type drain = typeof(AIRoom).GetNestedType("StdoutDrain", BindingFlags.NonPublic);
        object value = Activator.CreateInstance(drain, true);
        string path = Path.Combine(root, "drain.log");
        var writer = new StreamWriter(new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None));
        drain.GetField("writer", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(value, writer);
        var completed = drain.GetMethod("PumpCompleted", BindingFlags.Instance | BindingFlags.NonPublic);
        completed.Invoke(value, null);
        Check(drain.GetField("writer", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(value) != null,
            "shared process log survives until both output pumps finish");
        completed.Invoke(value, null);
        using (var reopened = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            Check(reopened != null, "process log handle released after both output pumps finish");
    }
}
