"""Exercise the real Android test app through its existing opt-in file protocol."""
from pathlib import Path
import argparse
import hashlib
import json
import subprocess
import time

ROOT = Path(__file__).resolve().parents[3]
OUT = ROOT / "_takeover_audit/device_20261011"
RUNTIME = ROOT / "_takeover_audit/implementation_20261011/runtime"
ADB = "E:/Unity/2021.3.45f1c2/Editor/Data/PlaybackEngines/AndroidPlayer/SDK/platform-tools/adb.exe"
PACKAGE = "com.meiheweite.pro.android.maintenance"
SERIAL = None
REMOTE = "/sdcard/Android/data/" + PACKAGE + "/files"

def adb(*args, required=True):
    result = subprocess.run([ADB, *(['-s', SERIAL] if SERIAL else []), *args], capture_output=True, timeout=120)
    if required and result.returncode: raise RuntimeError(result.stderr.decode("utf-8", "replace"))
    return result.stdout.decode("utf-8", "replace")

def main():
    global OUT, ADB, SERIAL
    parser = argparse.ArgumentParser()
    parser.add_argument('--out', type=Path, required=True)
    parser.add_argument('--adb', default=ADB)
    parser.add_argument('--serial', required=True)
    parser.add_argument("--mode", choices=["OCG", "RD"], required=True)
    parser.add_argument("--deck", required=True)
    parser.add_argument("--kind", choices=["ai", "test"], default="ai")
    parser.add_argument("--replay-count", type=int, default=1)
    parser.add_argument("--leave-duel", action="store_true", help="leave the test duel for manual UI validation")
    parser.add_argument("--start-only", action="store_true", help="start a fresh duel without replaying; leave it for manual validation")
    args = parser.parse_args()
    OUT, ADB, SERIAL = args.out.resolve(), args.adb, args.serial
    if args.start_only: args.leave_duel = True
    if not 1 <= args.replay_count <= 50: parser.error("replay count must be 1..50")
    if any(x in args.deck for x in " /\\\n\r"): parser.error("invalid deck name")
    folder = OUT / ("gameplay-" + args.mode.lower() + "-" + args.kind + ("-manual" if args.start_only else ""))
    folder.mkdir(parents=True, exist_ok=True)
    if (folder / "result.json").exists(): raise RuntimeError("evidence already exists; use a new output batch")
    info = json.loads((OUT / "apk-result.json").read_text())
    deckdir = "deck_rd" if args.mode == "RD" else "deck"
    deck = RUNTIME / deckdir / (args.deck + ".ydk")
    if not deck.is_file(): raise RuntimeError("fixture deck missing")
    manifest = {"status": "failed", "apk_sha256": info["sha256"], "mode": args.mode, "kind": args.kind,
                "deck_sha256": hashlib.sha256(deck.read_bytes()).hexdigest(), "requested_replays": args.replay_count,
                "coverage": "Android native start, recorded opening replay, synthetic end; no manual undo",
                "replay_seconds": [], "memory_cycles": []}
    switches = ["qt_debug.on", "qt_autojoin.on", "qt_undoreplay.on", "qt_endduel.on"]
    owned = set()
    def exists(name):
        result = subprocess.run([ADB, '-s', SERIAL, 'shell', 'test', '-e', REMOTE + '/' + name],
                                capture_output=True, timeout=30)
        if result.returncode not in (0, 1): raise RuntimeError('device path check failed')
        return result.returncode == 0
    def claim(name):
        if name not in owned:
            if exists(name): raise RuntimeError('existing device input: ' + name)
            owned.add(name)
    def push_text(name, text):
        local = folder / name
        local.parent.mkdir(parents=True, exist_ok=True)
        local.write_text(text, encoding="utf-8")
        claim(name)
        adb("push", str(local), REMOTE + "/" + name)
    trace = ""
    pid = ""
    def read_trace():
        return adb("shell", "cat", REMOTE + "/log/qt_" + pid + ".log", required=False)
    def wait(predicate, label, timeout=120):
        nonlocal trace
        until = time.monotonic() + timeout
        while time.monotonic() < until:
            trace = read_trace()
            if "[err]" in trace or "[ai-fail]" in trace:
                raise RuntimeError("runtime exception while waiting for " + label)
            if predicate(trace):
                print("PASS: " + label, flush=True)
                return trace
            if "[ai-fail]" in trace: raise RuntimeError("native launch failed")
            time.sleep(1)
        raise RuntimeError("timeout: " + label)
    try:
        package_path = adb("shell", "pm", "path", PACKAGE).strip().removeprefix("package:")
        installed_hash = adb("shell", "sha256sum", package_path).split()[0]
        if installed_hash != info["sha256"]: raise RuntimeError("installed APK does not match candidate")
        manifest["installed_apk_sha256"] = installed_hash
        for name in ["log/" + switch for switch in switches] + ["commamd.shell"]:
            if exists(name): raise RuntimeError("existing device input: " + name)
        remote_deck = deckdir + "/" + args.deck + ".ydk"
        deck_exists = exists(remote_deck)
        if deck_exists and adb("shell", "sha256sum", REMOTE + "/" + remote_deck).split()[0] != manifest["deck_sha256"]:
            raise RuntimeError("existing deck differs; refusing overwrite: " + remote_deck)
        adb("shell", "am", "force-stop", PACKAGE)
        adb("shell", "mkdir", "-p", REMOTE + "/log", REMOTE + "/" + deckdir)
        if not deck_exists:
            claim(remote_deck)
            adb("push", str(deck), REMOTE + "/" + remote_deck)
        push_text("log/qt_debug.on", "600")
        adb("shell", "am", "start", "-n", PACKAGE + "/com.unity3d.player.UnityPlayerActivity")
        time.sleep(3)
        pid = adb("shell", "pidof", PACKAGE).strip()
        if not pid.isdecimal(): raise RuntimeError("no test player PID")
        manifest["pid"] = pid
        wait(lambda text: "[rd] ui mode=" in text, "menu initialized")
        manifest["memory_before"] = adb("shell", "dumpsys", "meminfo", pid)
        def command(value):
            push_text("commamd.shell", value)
            wait(lambda _: not adb("shell", "ls", REMOTE + "/commamd.shell", required=False).strip(), "command consumed: " + value, 30)
        command("mode " + args.mode)
        wait(lambda text: "mode=" + args.mode in text, "mode switched")
        push_text("log/qt_autojoin.on", "600")
        if not args.start_only: push_text("log/qt_undoreplay.on", "600")
        command(args.kind + " " + args.deck)
        wait(lambda text: "DuelStart received" in text, "native duel started")
        import re
        for cycle in range(0 if args.start_only else args.replay_count):
            offset = 0 if cycle == 0 else len(read_trace())
            started_at = time.monotonic()
            if cycle: push_text("log/qt_undoreplay.on", "600")
            text = wait(lambda text: "[undo] finish forked=False" in text[offset:], "replay " + str(cycle + 1))
            segment = text[offset:]
            finished = re.search(r"finish forked=False replayed=(\d+)/(\d+) inbound=(\d+)", segment)
            begun = re.search(r"begin\(replayall\).* swallow=(\d+)", segment)
            if not finished or not begun or finished[1] != finished[2] or int(finished[3]) < int(begun[1]):
                raise RuntimeError("replay boundary mismatch")
            if re.search(r"\[undo\].*(?:MISMATCH|FAILED|FORK at)", segment): raise RuntimeError("replay mismatch")
            manifest["replay_seconds"].append(round(time.monotonic() - started_at, 3))
            manifest["memory_cycles"].append({"cycle": cycle + 1, "meminfo": adb("shell", "dumpsys", "meminfo", pid)})
            adb("shell", "rm", "-f", REMOTE + "/log/qt_undoreplay.on")
            time.sleep(1)
        if args.leave_duel:
            manifest["coverage"] = "Android native start and recorded opening replay; left for manual UI validation"
            if args.start_only: manifest["coverage"] = "Android native start only; left for manual UI validation"
        else:
            push_text("log/qt_endduel.on", "600")
            expected = "quick duel end -> deckEditor" if args.kind == "test" else "duel end -> onExit, returnServant=aiRoom"
            wait(lambda text: expected in text, "correct end return")
        manifest["memory_after"] = adb("shell", "dumpsys", "meminfo", pid)
        manifest["completed_replays"] = 0 if args.start_only else args.replay_count
        manifest["status"] = "passed"
        return 0
    except Exception as error:
        manifest["error"] = str(error)
        print("FAIL: " + str(error), flush=True)
        return 1
    finally:
        for name in sorted(owned):
            if name == "log/qt_debug.on" and args.leave_duel: continue
            adb("shell", "rm", "-f", REMOTE + "/" + name, required=False)
        (folder / "trace.log").write_text(trace, encoding="utf-8")
        (folder / "result.json").write_text(json.dumps(manifest, ensure_ascii=False, indent=2), encoding="utf-8")

if __name__ == "__main__": raise SystemExit(main())
