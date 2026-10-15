using System;
using System.IO;
using System.Linq;
using System.Windows.Forms;

public static class VersionControllerTests
{
    static int count;
    static void Check(bool value, string name) { if (!value) throw new Exception(name); count++; }
    static void Reject(Action work, string name) { try { work(); } catch { count++; return; } throw new Exception("Expected failure: " + name); }
    [STAThread] public static int Main(string[] args)
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        try
        {
            string root = Path.GetFullPath(args[0]); Directory.CreateDirectory(root);
            string bare = Path.Combine(root, "remote.git"), local = Path.Combine(root, "带空格 source"), work = Path.Combine(root, "controller work");
            Directory.CreateDirectory(local);
            VersionController client = new VersionController(bare, work, "git.exe");
            client.Run(root, null, "init", "--bare", "--initial-branch=main", bare);
            client.Run(local, null, "init", "--initial-branch=main");
            client.Run(local, null, "config", "user.name", "Test User");
            client.Run(local, null, "config", "user.email", "tests@example.invalid");
            File.WriteAllText(Path.Combine(local, "version.txt"), "2.2\n");
            File.WriteAllText(Path.Combine(local, "other.txt"), "keep\n");
            client.Run(local, null, "add", "."); client.Run(local, null, "commit", "-m", "baseline");
            client.Run(local, null, "remote", "add", "origin", bare); client.Run(local, null, "push", "origin", "main");
            File.WriteAllText(Path.Combine(local, "other.txt"), "dirty user content\n");
            File.WriteAllText(Path.Combine(local, "untracked.txt"), "user file\n");
            string state = client.Run(local, null, "status", "--porcelain");
            string head = client.Run(local, null, "rev-parse", "HEAD");
            Check(VersionController.Normalize(" v2.3 ") == "2.3", "v prefix");
            Check(VersionController.Normalize("2.2.1fix") == "2.2.1fix", "suffix");
            foreach (string bad in new string[]{"", "2", "2.3\n9.9", "2.3;cmd", "../2.3", "2.3 & whoami", "--all", new string('2', 60)})
                Reject(delegate { VersionController.Normalize(bad); }, "invalid version " + bad);
            Check(client.ReadRemote() == "2.2", "read remote");
            client.Publish("v2.3", "Test User", "tests@example.invalid", null);
            Check(client.ReadRemote() == "2.3", "publish remote");
            Check(client.Run(bare, null, "diff-tree", "--no-commit-id", "--name-only", "-r", "main") == "version.txt", "only version changed");
            Check(client.Run(bare, null, "show", "main:other.txt") == "keep", "user dirty content not pushed");
            Check(client.Run(local, null, "status", "--porcelain") == state, "local status unchanged");
            Check(client.Run(local, null, "rev-parse", "HEAD") == head, "local HEAD unchanged");
            string remoteHead = client.Run(bare, null, "rev-parse", "main");
            client.Publish("2.3", "Test User", "tests@example.invalid", null);
            Check(client.Run(bare, null, "rev-parse", "main") == remoteHead, "same version idempotent");
            client.Publish("2.2", "Test User", "tests@example.invalid", null);
            Check(client.ReadRemote() == "2.2", "operator can lower announcement version");
            client.Run(local, null, "fetch", "origin", "main");
            Reject(delegate { client.Publish("2.4", "Test User", "tests@example.invalid", delegate {
                string parent = client.Run(local, null, "rev-parse", "FETCH_HEAD");
                client.Run(local, null, "read-tree", parent);
                string blob = client.Run(local, "parallel change\n", "hash-object", "-w", "--stdin");
                client.Run(local, null, "update-index", "--cacheinfo", "100644", blob, "other.txt");
                string tree = client.Run(local, null, "write-tree");
                string commit = client.Run(local, "parallel\n", "commit-tree", tree, "-p", parent);
                client.Run(local, null, "push", "origin", commit+":refs/heads/main");
            }); }, "concurrent push rejected");
            Check(client.ReadRemote() == "2.2", "concurrent version not overwritten");
            Check(client.Run(bare, null, "show", "main:other.txt") == "parallel change", "concurrent other change preserved");
            Check(Directory.GetDirectories(work).Length == 0, "temporary repositories cleaned");
            VersionController missing = new VersionController(Path.Combine(root,"missing.git"), work, "git.exe");
            Reject(delegate { missing.ReadRemote(); }, "missing remote fails");
            Check(Directory.GetDirectories(work).Length == 0, "failed temporary repository cleaned");
            using (ControllerForm form = new ControllerForm())
            {
                Button push = form.Controls.OfType<Button>().First(x => x.Text == "一键推送版本");
                Check(!push.Enabled, "publish requires download readiness");
                form.Controls.OfType<CheckBox>().First().Checked = true;
                Check(push.Enabled, "publish enabled by operator");
            }
            Console.WriteLine("PASS VersionController assertions=" + count); return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }
}
