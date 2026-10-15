using System;
using System.Reflection;
using System.Threading;

// Tests the production host lifecycle with a deliberately slow worker.
// Does not execute a native server, robot AI, or Unity gameplay.
public static class MobileLifecycleTests
{
    static void Check(bool value, string message)
    {
        if (!value) throw new Exception(message);
        Console.WriteLine("PASS: " + message);
    }
    static void Worker(Type host, Func<bool> stop, Func<bool> start)
    {
        using (var release = new ManualResetEvent(false))
        {
            var thread = new Thread(() => release.WaitOne());
            thread.IsBackground = true;
            thread.Start();
            var flags = BindingFlags.Static | BindingFlags.NonPublic;
            host.GetField("_thread", flags).SetValue(null, thread);
            host.GetField("_running", flags).SetValue(null, true);
            try
            {
                Check(!stop(), host.Name + " reports a timed-out shutdown");
                Check(ReferenceEquals(host.GetField("_thread", flags).GetValue(null), thread),
                    host.Name + " retains the still-live worker");
                Check((bool)host.GetField("_running", flags).GetValue(null),
                    host.Name + " keeps reporting the timed-out worker as running");
                if (start != null) Check(!start(), host.Name + " refuses overlapping restart");
            }
            finally { release.Set(); thread.Join(1000); }
            Check(stop() && host.GetField("_thread", flags).GetValue(null) == null,
                host.Name + " releases the worker only after exit");
        }
    }
    public static int Main()
    {
        try
        {
            Worker(typeof(InProcessAiServer), () => InProcessAiServer.Stop(1),
                null); // Start calls Unity internal APIs; actual native restart is a device test.
            Worker(typeof(WindbotHost), () => WindbotHost.StopAndWait(1),
                () => WindbotHost.Start("unused", new string[0]));
            return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }
}
namespace WindBot
{
    public static class Program
    {
        public static string[] ExtraRoots;
        public static void Main(string[] args) { throw new Exception("Robot execution is outside this lifecycle test"); }
    }
    public static class Logger { public static Action<bool, string> Sink; }
}
