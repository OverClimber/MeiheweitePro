"""Build a fully independent player for UI/gameplay verification. No junctions."""
from pathlib import Path
import datetime
import hashlib
import json
import shutil
import sys

ROOT = Path(__file__).resolve().parents[3]
OUT = ROOT / "_takeover_audit" / ("implementation_" + datetime.date.today().strftime("%Y%m%d"))
SOURCE = ROOT / "YGOProUnity_V2/output/Windows"
TARGET = OUT / "runtime"
DIRECTORIES = ["MeiheweitePro_Data", "MonoBleedingEdge", "cdb", "config", "data", "deck",
               "deck_rd", "expansions", "pack", "picture", "puzzle", "rd", "sound", "texture", "translation", "WindBot"]

def main():
    dll = OUT / "Assembly-CSharp.dll"
    if not dll.is_file(): raise RuntimeError("Run maintenance gate first")
    if TARGET.exists() and any(TARGET.iterdir()):
        raise RuntimeError("Runtime already exists; preserve its evidence and clean the owned copy before rebuilding")
    TARGET.mkdir(parents=True, exist_ok=True)
    for source in SOURCE.iterdir():
        if source.is_file() and source.suffix.lower() in (".exe", ".dll"):
            shutil.copy2(source, TARGET / source.name)
    for name in DIRECTORIES:
        source = SOURCE / name
        if source.is_dir():
            print("Copying independent runtime: " + name, flush=True)
            shutil.copytree(source, TARGET / name, dirs_exist_ok=True,
                ignore=shutil.ignore_patterns("qt_*.on", "qt_*.txt", "*.log", ".picsync-absent*"))
    shutil.copy2(dll, TARGET / "MeiheweitePro_Data/Managed/Assembly-CSharp.dll")
    log = TARGET / "log"
    log.mkdir(exist_ok=True)
    (log / "qt_debug.on").write_text("180", encoding="utf-8")
    (OUT / "runtime-input.json").write_text(json.dumps({"player": str(TARGET / "MeiheweitePro.exe"),
        "dll_sha256": hashlib.sha256(dll.read_bytes()).hexdigest()}, indent=2), encoding="utf-8")
    print("Independent runtime prepared: " + str(TARGET))

if __name__ == "__main__": main()
