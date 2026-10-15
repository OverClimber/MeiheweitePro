"""Verify every bundled layer native library against this build's input manifest."""
from pathlib import Path
import hashlib
import json
import zipfile
import argparse

ROOT = Path(__file__).resolve().parents[3]
OUT = ROOT / "_takeover_audit/device_20261011"

def main():
    global OUT
    parser = argparse.ArgumentParser()
    parser.add_argument('--out', type=Path, required=True)
    OUT = parser.parse_args().out.resolve()
    info = json.loads((OUT / "apk-result.json").read_text())
    inputs = json.loads((OUT / "build-input.json").read_text())
    result_path = OUT / "apk-native-result.json"
    result_path.write_text(json.dumps({"status": "failed", "reason": "verification not completed"}))
    with Path(info["apk"]).open("rb") as stream:
        actual_hash = hashlib.file_digest(stream, "sha256").hexdigest()
    if actual_hash != info["sha256"]: raise RuntimeError("APK file hash differs from build record")
    if not inputs.get("native"): raise RuntimeError("No native inputs to verify")
    checked = {}
    with zipfile.ZipFile(info["apk"]) as apk:
        for relative, expected in inputs["native"].items():
            digest = hashlib.sha256(apk.read("lib/" + relative)).hexdigest()
            if digest != expected:
                raise RuntimeError("APK native input mismatch: " + relative)
            checked[relative] = digest
    (OUT / "apk-native-result.json").write_text(json.dumps({"status": "passed",
        "apk_sha256": info["sha256"], "native": checked}, indent=2))
    print("PASS: all %s APK native libraries match build inputs" % len(checked))

if __name__ == "__main__": main()
