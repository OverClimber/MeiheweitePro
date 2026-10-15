"""Real native gameplay smoke/replay case via the existing game file protocol.

Start the independent player through its normal launcher before running this tool.
No mouse automation, fabricated inputs, historical PASS or production writes.
"""
from pathlib import Path
import argparse
import datetime
import hashlib
import json
import re
import subprocess
import time

ROOT = Path(__file__).resolve().parents[3]
ARTIFACTS = ROOT / "_takeover_audit" / ("implementation_" + datetime.date.today().strftime("%Y%m%d"))

def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()

def remove_flag(path):
    # Unity briefly opens these files without delete sharing on Windows.
    until = time.monotonic() + 3
    while True:
        try:
            path.unlink(missing_ok=True)
            return
        except PermissionError:
            if time.monotonic() >= until: raise
            time.sleep(.05)

def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--mode", choices=("OCG", "RD"), required=True)
    parser.add_argument("--deck", required=True)
    parser.add_argument("--kind", choices=("test", "ai"), default="test")
    parser.add_argument("--timeout", type=int, default=180)
    parser.add_argument("--replay-count", type=int, default=1)
    parser.add_argument("--pid", type=int, help="select an explicit isolated player when multiple test instances exist")
    parser.add_argument("--artifacts", type=Path, default=ARTIFACTS, help="candidate DLL and result directory")
    parser.add_argument("--runtime", type=Path, help="independent test player directory")
    args = parser.parse_args()
    if not 1 <= args.replay_count <= 50: parser.error("replay count must be 1..50")
    if not re.fullmatch(r"[^\s/\\]+", args.deck): parser.error("deck must be a single local deck name")
    artifacts = args.artifacts.resolve()
    runtime = (args.runtime or artifacts / "runtime").resolve()
    if runtime == (ROOT / "YGOProUnity_V2/output/Windows").resolve():
        raise RuntimeError("Use an independent player, never the production directory")
    dll = runtime / "MeiheweitePro_Data/Managed/Assembly-CSharp.dll"
    if sha(dll) != sha(artifacts / "Assembly-CSharp.dll"):
        raise RuntimeError("Running player DLL differs from the current gated candidate")
    traces = sorted((runtime / "log").glob("qt_*.log"), key=lambda p: p.stat().st_mtime, reverse=True)
    traces = [path for path in traces if re.fullmatch(r"qt_\d+\.log", path.name)]
    if args.pid is not None: traces = [path for path in traces if path.stem == "qt_" + str(args.pid)]
    if not traces: raise RuntimeError("No live trace; enable qt_debug.on before launching the player")
    trace = traces[0]
    pid = int(trace.stem.split("_")[1])
    result = subprocess.run(["powershell", "-NoProfile", "-Command",
        "(Get-Process -Id %s -ErrorAction Stop).Path" % pid], capture_output=True)
    process_path = result.stdout.decode("utf-8", "replace").strip()
    if result.returncode or Path(process_path).resolve() != (runtime / "MeiheweitePro.exe").resolve():
        raise RuntimeError("Trace does not belong to the live independent player")
    deck = runtime / ("deck_rd" if args.mode == "RD" else "deck") / (args.deck + ".ydk")
    if not deck.is_file(): raise RuntimeError("Test deck missing: " + str(deck))
    start = len(trace.read_text(encoding="utf-8", errors="replace"))
    out = artifacts / "gameplay" / (args.mode.lower() + "-" + args.kind)
    out.mkdir(parents=True, exist_ok=True)
    manifest = {"status": "failed", "pid": pid, "dll_sha256": sha(dll), "mode": args.mode,
                "kind": args.kind, "deck": args.deck, "deck_sha256": sha(deck),
                "visual_validation": "not run", "coverage": "native start, recorded opening replay, synthetic end return",
                "requested_replays": args.replay_count, "replay_seconds": []}
    owned = []
    def body():
        return trace.read_text(encoding="utf-8", errors="replace")[start:]
    def wait_for(predicate, label, timeout=None):
        end = time.monotonic() + (timeout or args.timeout)
        while time.monotonic() < end:
            text = body()
            if "[err]" in text:
                raise RuntimeError("runtime exception while waiting for " + label)
            if "GIVE UP: DuelStart never arrived" in text or "[ai-fail]" in text:
                raise RuntimeError("Game reported failure while waiting for " + label)
            if "[undo] FAIL:" in text or "[error] ErrorMsg" in text:
                raise RuntimeError("Replay or server rejected the case while waiting for " + label)
            if predicate(text):
                print("PASS: " + label, flush=True)
                return text
            time.sleep(.5)
        raise RuntimeError("Timed out waiting for " + label)
    def flag(name, value="180"):
        path = runtime / "log" / name
        if path.exists(): raise RuntimeError("Refusing to overwrite an existing test flag: " + name)
        path.write_text(value, encoding="utf-8")
        owned.append(path)
        return path
    def command(text):
        path = runtime / "commamd.shell"
        if path.exists(): raise RuntimeError("A previous game command is still pending")
        path.write_text(text, encoding="utf-8")
        wait_for(lambda _: not path.exists(), "command consumed: " + text, 30)
    try:
        command("mode " + args.mode)
        wait_for(lambda text: "mode=" + args.mode in text, "current mode recorded", 20)
        flag("qt_autojoin.on")
        replay = flag("qt_undoreplay.on")
        command(args.kind + " " + args.deck)
        wait_for(lambda text: "DuelStart received" in text, "native duel started")
        text = wait_for(lambda text: re.search(r"\[undo\] finish forked=False replayed=(\d+)/(\d+) inbound=(\d+)", text), "recorded opening replay completed")
        matched = re.search(r"catchup done at inbound=(\d+)/(\d+)", text)
        if matched is None:
            # With zero decisions the last recorded packet itself completes replay;
            # no following packet is required to call EndCatchUp.
            finished = re.search(r"\[undo\] finish forked=False replayed=(\d+)/(\d+) inbound=(\d+)", text)
            started = re.search(r"begin\(replayall\).* inbound=(\d+) swallow=(\d+)", text)
            if not started or finished[1] != finished[2] or finished[3] != started[2]:
                raise RuntimeError("Replay completion boundary mismatch")
            matched = (None, finished[3], started[2])
        if int(matched[1]) <= 0 or matched[1] != matched[2]: raise RuntimeError("Replay count mismatch")
        if re.search(r"\[undo\].*(?:MISMATCH|desync|FAILED)", text, re.I): raise RuntimeError("Replay mismatch reported")
        remove_flag(replay)
        for cycle in range(1, args.replay_count):
            # Leave a real frame with the switch off to reset the in-game latch.
            time.sleep(1)
            offset = len(body())
            started_at = time.monotonic()
            replay = flag("qt_undoreplay.on")
            text = wait_for(lambda text: re.search(r"\[undo\] finish forked=False replayed=(\d+)/(\d+) inbound=(\d+)", text[offset:]),
                            "replay cycle " + str(cycle + 1) + " completed")
            cycle_text = text[offset:]
            finished = re.search(r"\[undo\] finish forked=False replayed=(\d+)/(\d+) inbound=(\d+)", cycle_text)
            started = re.search(r"begin\(replayall\).* inbound=(\d+) swallow=(\d+)", cycle_text)
            if not started or finished[1] != finished[2] or int(finished[3]) < int(started[2]) or re.search(r"\[undo\].*(?:MISMATCH|desync|FAILED|FORK at)", cycle_text, re.I):
                raise RuntimeError("Repeated replay mismatch at cycle " + str(cycle + 1))
            manifest["replay_seconds"].append(round(time.monotonic() - started_at, 3))
            remove_flag(replay)
        manifest["completed_replays"] = args.replay_count
        flag("qt_endduel.on")
        expected = "quick duel end -> deckEditor" if args.kind == "test" else "duel end -> onExit, returnServant=aiRoom"
        text = wait_for(lambda text: expected in text, "correct end return target")
        manifest["replayed_inbound"] = int(matched[1])
        manifest["status"] = "passed"
        return 0
    except Exception as error:
        manifest["error"] = str(error)
        print("FAIL: " + str(error), flush=True)
        return 1
    finally:
        for path in owned: remove_flag(path)
        (out / "trace.log").write_text(body(), encoding="utf-8")
        (out / "result.json").write_text(json.dumps(manifest, ensure_ascii=False, indent=2), encoding="utf-8")

if __name__ == "__main__": raise SystemExit(main())
