"""Verify every original script archive entry survives the compatibility install."""
from pathlib import Path
import hashlib
import json
import zipfile

ROOT = Path(__file__).resolve().parents[3]
OUT = ROOT / "_takeover_audit/issues_20261011"
RUN = ROOT / "_takeover_audit/implementation_20261011/runtime"

def sha(path):
    with path.open("rb") as stream: return hashlib.file_digest(stream, "sha256").hexdigest()

def main():
    current = RUN / "data/script.zip"
    suffix = (OUT.parent / "implementation_20261011/type-compatibility.lua").read_bytes()
    backups = list((RUN / "updates/script-compatibility").glob("*.zip"))
    if len(backups) != 1: raise RuntimeError("Expected one baseline archive for this isolated candidate")
    baseline = backups[0]
    if sha(baseline) != baseline.stem: raise RuntimeError("Original backup hash mismatch")
    with zipfile.ZipFile(baseline) as old, zipfile.ZipFile(current) as new:
        if old.namelist() != new.namelist(): raise RuntimeError("Script archive entries changed")
        for name in old.namelist():
            expected = old.read(name) + (suffix if name == "script/utility.lua" else b"")
            if new.read(name) != expected: raise RuntimeError("Unexpected script content change: " + name)
        count = len(old.namelist())
    result = {"status": "passed", "entries_compared": count,
        "changed_entries": ["script/utility.lua"], "baseline_sha256": sha(baseline),
        "current_sha256": sha(current), "compatibility_sha256": hashlib.sha256(suffix).hexdigest()}
    (OUT / "script-compatibility-result.json").write_text(json.dumps(result, indent=2), encoding="utf-8")
    print(json.dumps(result))

if __name__ == "__main__": main()
