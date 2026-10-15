"""Build the owned independent Android test application, never a production update."""
from pathlib import Path
import datetime
import argparse
import hashlib
import json
import os
import subprocess

ROOT = Path(__file__).resolve().parents[3]
OUT = ROOT / "_takeover_audit" / ("device_" + datetime.date.today().strftime("%Y%m%d"))
TARGET = Path("D:/Game/MeiheweiteMaintenance-build")
UNITY = Path("E:/Unity/2021.3.45f1c2/Editor/Unity.exe")

def main():
    global OUT, TARGET, UNITY
    parser = argparse.ArgumentParser()
    parser.add_argument('--out', type=Path, default=OUT)
    parser.add_argument('--target', type=Path, default=TARGET)
    parser.add_argument('--unity', type=Path, default=UNITY)
    args = parser.parse_args()
    OUT, TARGET, UNITY = args.out.resolve(), args.target.resolve(), args.unity.resolve()
    owner = json.loads((TARGET / ".maintenance-owned.json").read_text(encoding="utf-8"))
    if owner["owner"] != str(ROOT): raise RuntimeError("Build project owner mismatch")
    OUT.mkdir(parents=True, exist_ok=True)
    env = os.environ.copy()
    for key, relative in {"GRADLE_USER_HOME": "gradle", "UPM_CACHE_PATH": "unity-cache/upm",
                          "UPM_NPM_CACHE_PATH": "unity-cache/npm", "TEMP": "buildtmp", "TMP": "buildtmp"}.items():
        path = Path("E:/Meiheweitemobile") / relative
        path.mkdir(parents=True, exist_ok=True)
        env[key] = str(path)
    log = OUT / "unity-android-build.log"
    command = [str(UNITY), "-batchmode", "-nographics", "-quit", "-projectPath", str(TARGET),
               "-logFile", str(log), "-buildTarget", "Android", "-executeMethod", "AndroidBuild.BuildApkMaintenance"]
    process = subprocess.Popen(command, env=env, stdin=subprocess.DEVNULL,
        stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL,
        creationflags=subprocess.CREATE_NO_WINDOW)
    (OUT / "build-process.json").write_text(json.dumps({"pid": process.pid, "command": command}), encoding="utf-8")
    print("Unity Android build PID=%s, log=%s" % (process.pid, log), flush=True)
    code = process.wait()
    text = log.read_text(encoding="utf-8", errors="replace")
    successes = [line.split("BUILD_OK ", 1)[1].strip() for line in text.splitlines() if "BUILD_OK " in line]
    if code or not successes:
        raise RuntimeError("Android build failed (%s); inspect %s" % (code, log))
    apk = Path(successes[-1])
    if not apk.is_file() or TARGET.resolve() not in apk.resolve().parents:
        raise RuntimeError("Invalid build output")
    with apk.open("rb") as stream:
        digest = hashlib.file_digest(stream, "sha256").hexdigest()
    (OUT / "apk-result.json").write_text(json.dumps({"status": "built", "apk": str(apk),
        "sha256": digest, "bytes": apk.stat().st_size, "device_validation": "not run",
        "package": "com.meiheweite.pro.android.maintenance"}, indent=2), encoding="utf-8")
    print("BUILT: " + str(apk), flush=True)

if __name__ == "__main__": main()
