using System;
using System.Collections.Generic;
using System.Diagnostics;

// Methods marked INSERT_* are copied verbatim from current production by the
// gate. Only transport, Unity presentation and the clock are test doubles.
class Clock { public bool IsRunning = true; public long ElapsedMilliseconds; public void Restart() { ElapsedMilliseconds = 0; } }
class Package { public int Fuction; public Data Data = new Data(); }
class Data { public byte[] get() { return new byte[0]; } }
namespace YGOSharp.OCGWrapper.Enums { enum GameMessage { Waiting } }
static class QuickTestTrace { public static void Log(string a, string b) {} }
static class GameModeManager { public static string DeckInUse = "fixture"; }
static class Program { public static int TimePassed() { return 1000000; } }
static class DuelTimeline
{
    public class Inbound { public string digest; }
    public class Decision { public int inboundCount, message; public bool manual; public byte[] response; public string digestAtResponse; }
    public class Snapshot { public List<Inbound> inbound = new List<Inbound>(); public List<Decision> decisions = new List<Decision>(); }
    public static List<Inbound> inbound = new List<Inbound>();
    public static List<Decision> decisions = new List<Decision>();
    public static string digest;
    public static void NoteGameMessage(int message, byte[] bytes) { digest = (inbound.Count + 1).ToString(); inbound.Add(new Inbound { digest = digest }); }
    public static void NoteResponse(int message, byte[] response, bool manual) { decisions.Add(new Decision { message=message, response=response, manual=manual, inboundCount=inbound.Count }); }
}
static class TcpHelper
{
    class ConnectionState {
        public bool Closing;
        public System.Collections.Concurrent.ConcurrentQueue<int> Incoming = new System.Collections.Concurrent.ConcurrentQueue<int>();
        public System.Threading.AutoResetEvent IncomingSignal = new System.Threading.AutoResetEvent(false);
    }
    static object stateLock=new object();
    static ConnectionState state=new ConnectionState();
    static int pendingRounds, processedRounds;
    static void ProcessIncomingFrame() {
        int packet;
        if(state.Incoming.TryDequeue(out packet)) {
            processedRounds++;
            if(--pendingRounds>0) state.Incoming.Enqueue(1);
        }
    }
    public static int Pump(int rounds, bool closing) {
        state=new ConnectionState { Closing=closing }; pendingRounds=rounds; processedRounds=0;
        state.Incoming.Enqueue(1); preFrameFunction(); return processedRounds;
    }
    // INSERT_TCP
    public static int sent, records, decks, ready, starts;
    public static void AddRecordLine(Package p) { records++; }
    public static void CtosMessage_Response(byte[] p) { sent++; }
    public static void CtosMessage_UpdateDeck(object deck) { decks++; }
    public static void CtosMessage_HsReady() { ready++; }
    public static void CtosMessage_HsStart() { starts++; }
}
static class DuelUndo
{
    public static bool active, catchingUp, silent, replayWholeDuel;
    public static int catchIndex, swallowCount, targetKeep, replayedDecisions, forks, failures, finishes;
    public static Clock progressClock = new Clock();
    public const int SessionTimeoutMs=25000;
    public static DuelTimeline.Snapshot saved;
    public static bool DropStalePreStart(int f) { return false; }
    static void OnFork(int i, Package p) { forks++; active=false; catchingUp=false; }
    static void Fail(string message) { failures++; active=false; }
    static void Finish(string message) { finishes++; active=false; catchingUp=false; }
    // INSERT_UNDO
}
class Ocg
{
    public List<Package> Packages = new List<Package>(), Packages_ALL = new List<Package>();
    public void Frame() { undoReplayTick(); }
    // INSERT_OCG
}
class Room
{
    public class Player { public bool prep; }
    public Player[] roomPlayers = new Player[4];
    public bool quickTypeKnown, quickFlowStarted, quickDuelStarted, is_host=true, undoRestartThisDuel=true;
    public int quickFlowGeneration, selftype;
    object DeckForAutoStart() { return new object(); }
    public System.Collections.IEnumerator Flow() { return quickStartFlow(); }
    // INSERT_ROOM
}
class UndoPerformanceTests
{
    static int checks;
    static void Check(bool value, string label) { if(!value) throw new Exception(label); checks++; }
    static Ocg Setup(int total, int target)
    {
        DuelTimeline.inbound.Clear(); DuelTimeline.decisions.Clear(); DuelTimeline.digest="";
        DuelUndo.saved=new DuelTimeline.Snapshot();
        for(int i=0;i<total;i++) {
            DuelUndo.saved.inbound.Add(new DuelTimeline.Inbound { digest=(i+1).ToString() });
            DuelUndo.saved.decisions.Add(new DuelTimeline.Decision { inboundCount=i+1, message=10+i%7,
                manual=i%3!=0, response=new byte[]{(byte)i}, digestAtResponse=(i+1).ToString() });
        }
        DuelUndo.active=true; DuelUndo.catchingUp=true; DuelUndo.replayWholeDuel=false;
        DuelUndo.targetKeep=target; DuelUndo.swallowCount=target; DuelUndo.catchIndex=0;
        DuelUndo.replayedDecisions=0; DuelUndo.forks=0; DuelUndo.failures=0; DuelUndo.finishes=0;
        DuelUndo.progressClock.Restart(); TcpHelper.sent=0; TcpHelper.records=0;
        return new Ocg();
    }
    static int Main()
    {
        try {
            foreach(int target in new[]{0,1,5000,18000}) {
                var o=Setup(20000,target); var timer=Stopwatch.StartNew();
                for(int i=0;i<target;i++) {
                    // Total virtual duration exceeds 25s; each validated message makes progress.
                    DuelUndo.progressClock.ElapsedMilliseconds=24000;
                    o.addPackage(new Package { Fuction=DuelUndo.saved.decisions[i].message });
                    Check(TcpHelper.sent==i+1,"one answer immediately per verified boundary");
                    Check(DuelTimeline.decisions[i].message==DuelUndo.saved.decisions[i].message,"original request type");
                    Check(DuelTimeline.decisions[i].manual==DuelUndo.saved.decisions[i].manual,"manual/automatic ownership");
                    o.Frame(); Check(TcpHelper.sent==i+1,"no duplicate answer at frame end");
                }
                o.addPackage(new Package { Fuction=DuelUndo.saved.decisions[target].message });
                DuelTimeline.NoteGameMessage(DuelUndo.saved.decisions[target].message, new byte[0]); o.Frame();
                Check(TcpHelper.sent==target,"target left unanswered");
                Check(o.Packages.Count==1,"target enters normal UI path");
                Check(DuelUndo.finishes==1 && DuelUndo.failures==0,"long replay completes");
                Check(TcpHelper.records==target+1,"record includes exact prefix and target");
                Console.WriteLine("PASS total=20000 target="+target+" withdrawn="+(20000-target)+" fixture_ms="+timer.ElapsedMilliseconds);
            }
            var fork=Setup(10,8); DuelUndo.saved.inbound[0].digest="wrong";
            fork.addPackage(new Package { Fuction=10 });
            Check(DuelUndo.forks==1 && TcpHelper.sent==0,"divergence never answers");
            var mismatch=Setup(10,8); DuelUndo.saved.decisions[0].digestAtResponse="wrong";
            mismatch.addPackage(new Package { Fuction=10 });
            Check(DuelUndo.failures==1 && TcpHelper.sent==0,"response digest mismatch stops");
            var idle=Setup(10,8); DuelUndo.progressClock.ElapsedMilliseconds=25001; idle.Frame();
            Check(DuelUndo.failures==1,"stalled replay times out");
            var all=Setup(10000,10000); DuelUndo.replayWholeDuel=true;
            for(int i=0;i<10000;i++) all.addPackage(new Package { Fuction=10+i%7 });
            all.Frame(); Check(DuelUndo.finishes==1 && TcpHelper.sent==10000,"whole replay exact completion");
            var room=new Room(); var flow=room.Flow();
            Check(flow.MoveNext() && TcpHelper.decks==0,"wait for type/player registration");
            room.quickTypeKnown=true; room.roomPlayers[0]=new Room.Player(); room.roomPlayers[1]=new Room.Player();
            Check(flow.MoveNext() && TcpHelper.decks==1 && TcpHelper.ready==1 && TcpHelper.starts==0,"prepare after registration, await ready");
            room.roomPlayers[0].prep=true; room.roomPlayers[1].prep=true;
            Check(flow.MoveNext() && TcpHelper.starts==1,"ready acknowledgements start without one-second delay");
            room.quickFlowGeneration++; Check(!flow.MoveNext(),"old coroutine invalidated by new room");
            Setup(10,8);
            TcpHelper.Pump(1000,false); // warm the fixture/JIT; the production budget includes cold work
            Check(TcpHelper.Pump(1000,false)>1,"multiple ready round trips in a frame");
            Check(TcpHelper.Pump(1000,false)<=65,"round cap prevents unbounded main-thread work");
            Check(TcpHelper.Pump(1000,true)==1,"closed connection is not pumped again");
            DuelUndo.active=false; Check(TcpHelper.Pump(1000,false)==1,"normal gameplay retains single pump");
            Console.WriteLine("PASS assertions="+checks+" (simulation only, no native/Unity performance claim)");
            return 0;
        } catch(Exception e) { Console.Error.WriteLine(e); return 1; }
    }
}
