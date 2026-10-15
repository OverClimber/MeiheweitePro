"""Summarize current candidate smoke cases; never imply full release acceptance."""
from pathlib import Path
import argparse
import hashlib
import json
import re

ROOT = Path(__file__).resolve().parents[3]
OUT = ROOT / "_takeover_audit/issues_20261011"
DESKTOP = ROOT / "_takeover_audit/implementation_20261011"
ANDROID = ROOT / "_takeover_audit/device_20261011"

def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--platform", choices=("desktop", "android", "both"), default="both")
    args = parser.parse_args()
    desktop_hash = hashlib.sha256((DESKTOP / "Assembly-CSharp.dll").read_bytes()).hexdigest()
    android_hash = json.loads((ANDROID / "apk-result.json").read_text())["sha256"]
    rows = []
    for platform, base, expected, key in (("desktop", DESKTOP / "gameplay", desktop_hash, "dll_sha256"),
        ("android", ANDROID, android_hash, "apk_sha256")):
        if args.platform != "both" and args.platform != platform: continue
        for mode in ("ocg", "rd"):
            for kind in ("ai", "test"):
                case = mode + "-" + kind
                folder = base / (("gameplay-" if platform == "android" else "") + case)
                info = json.loads((folder / "result.json").read_text(encoding="utf-8"))
                text = (folder / "trace.log").read_text(encoding="utf-8")
                minimum = 10 if kind == "ai" else 1
                target = "quick duel end -> deckEditor" if kind == "test" else "duel end -> onExit, returnServant=aiRoom"
                if (info["status"] != "passed" or info[key] != expected
                    or info["completed_replays"] < minimum or "[err]" in text or target not in text):
                    raise RuntimeError("Current candidate case incomplete: " + platform + "/" + case)
                row = {"platform": platform, "case": case, "sha256": expected,
                    "completed_replays": info["completed_replays"], "seconds": info["replay_seconds"]}
                durations, start = [], None
                for match in re.finditer(r"(?m)^(\d\d):(\d\d):(\d\d\.\d+) \[undo\] (begin\(replayall\)|finish forked=False)", text):
                    stamp = int(match[1]) * 3600 + int(match[2]) * 60 + float(match[3])
                    if match[4].startswith("begin"): start = stamp
                    elif start is not None:
                        durations.append(round((stamp - start) % 86400, 3))
                        start = None
                if len(durations) != info["completed_replays"]:
                    raise RuntimeError("Missing in-game duration evidence: " + platform + "/" + case)
                row["in_game_rebuild_seconds"] = durations
                if platform == "android":
                    row["memory_kb"] = [{"cycle": x["cycle"],
                        "native_heap_pss": int(re.search(r"(?m)^\s*Native Heap\s+(\d+)", x["meminfo"])[1]),
                        "total_pss": int(re.search(r"TOTAL PSS:\s+(\d+)", x["meminfo"])[1])}
                        for x in info["memory_cycles"]]
                rows.append(row)
    result = {"status": "passed", "platform": args.platform, "coverage": "native opening replay and synthetic end return only",
        "full_release_acceptance": "not passed; manual long-game, online and resource fault coverage remain", "cases": rows}
    name = "gameplay-matrix.json" if args.platform == "both" else args.platform + "-gameplay-matrix.json"
    (OUT / name).write_text(json.dumps(result, ensure_ascii=False, indent=2), encoding="utf-8")
    print("PASS: %s current-candidate %s smoke cases; full release acceptance remains separate" % (len(rows), args.platform))

if __name__ == "__main__": main()
