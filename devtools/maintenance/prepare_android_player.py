"""Prepare a fresh ASCII Android test project; cached projects are read-only."""
from pathlib import Path
import argparse
import datetime
import hashlib
import importlib.util
import json
import os
import shutil
import subprocess
import sys

ROOT = Path(__file__).resolve().parents[3]
LAYER = ROOT / "YGOProAndroid"
OUT = ROOT / "_takeover_audit" / ("device_" + datetime.date.today().strftime("%Y%m%d"))
sys.dont_write_bytecode = True

def run(args, cwd=None):
    result = subprocess.run([str(x) for x in args], cwd=cwd, capture_output=True)
    if result.returncode:
        raise RuntimeError((result.stdout + result.stderr).decode("utf-8", "replace"))
    return result.stdout

def main():
    global OUT
    parser = argparse.ArgumentParser()
    parser.add_argument("--out", type=Path, default=OUT)
    parser.add_argument("--target", type=Path, default=Path("D:/Game/MeiheweiteMaintenance-build"))
    parser.add_argument("--cache", type=Path, default=Path("E:/Meiheweitemobile/build"))
    parser.add_argument("--resume", action="store_true", help="resume only this tool's owned project after a failed preparation")
    args = parser.parse_args()
    OUT = args.out.resolve()
    target, cache = args.target.resolve(), args.cache.resolve()
    str(target).encode("ascii")
    if target.exists():
        marker = target / ".maintenance-owned.json"
        if not args.resume or not marker.is_file() or json.loads(marker.read_text())["owner"] != str(ROOT):
            raise RuntimeError("Target already exists; preserve its evidence before cleanup")
    if target == cache or target == ROOT or not (cache / "Assets/main.unity").is_file():
        raise RuntimeError("Invalid independent build target/cache")
    OUT.mkdir(parents=True, exist_ok=True)
    target.mkdir(parents=True, exist_ok=args.resume)
    (target / ".maintenance-owned.json").write_text(json.dumps({"owner": str(ROOT), "cache": str(cache)}), encoding="utf-8")
    for folder in ("Assets", "Packages", "ProjectSettings", "Library"):
        if args.resume and (target / folder).is_dir(): continue
        print("Copying independent Android project: " + folder, flush=True)
        shutil.copytree(cache / folder, target / folder,
            ignore=shutil.ignore_patterns("Bee", "TempArtifacts", "Search", "*.lock"))
    # Imported scenes/metas/resources stay as cached; all executable C# is rebuilt
    # from the pinned baseline and strict layer chain, not trusted from that cache.
    for path in (target / "Assets").rglob("*.cs"):
        path.unlink()
    spec = importlib.util.spec_from_file_location("stage", Path(__file__).with_name("stage_android.py"))
    stage = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(stage)
    lock = json.loads((LAYER / "LAYER.lock.json").read_text(encoding="utf-8"))
    stage.export(target, lock["base"]["commit"])
    for source in (LAYER / "layer/Assets").rglob("*"):
        if source.is_file():
            dest = target / "Assets" / source.relative_to(LAYER / "layer/Assets")
            dest.parent.mkdir(parents=True, exist_ok=True)
            shutil.copy2(source, dest)
    for patch in sorted((LAYER / "patches").glob("*.patch")):
        run(["git", "apply", "--check", "--include=*.cs", patch], target)
        run(["git", "apply", "--include=*.cs", patch], target)
    for name in ("ManagedPackInstall", "PictureAbsentCache", "ZipImageIndex", "CardScriptUpdater"):
        meta = ROOT / ("YGOProUnity_V2/Assets/SibylSystem/ResourceManagers/" + name + ".cs.meta")
        shutil.copy2(meta, target / "Assets/SibylSystem/ResourceManagers" / meta.name)
    # Cached imported plugins must not silently override rebuilt layer libraries.
    native_hashes = {}
    for abi in ("arm64-v8a", "armeabi-v7a"):
        for name in ("libygoserver.so", "libocgcore.so", "libsqlite3.so", "libc++_shared.so"):
            source = LAYER / "layer/native" / abi / name
            dest = target / "Assets/Plugins/Android" / abi / name
            if not source.is_file() or not dest.with_suffix(".so.meta").is_file():
                raise RuntimeError("Missing native library or importer: " + str(source))
            shutil.copy2(source, dest)
            digest = hashlib.sha256(source.read_bytes()).hexdigest()
            if hashlib.sha256(dest.read_bytes()).hexdigest() != digest:
                raise RuntimeError("Native plugin copy mismatch: " + str(dest))
            native_hashes[abi + "/" + name] = digest
    # Restore settings to a known baseline before applying the Android layout.
    settings = target / "ProjectSettings/ProjectSettings.asset"
    settings.write_bytes(run(["git", "show", lock["base"]["commit"] + ":ProjectSettings/ProjectSettings.asset"], ROOT / "YGOProUnity_V2"))
    env = os.environ.copy()
    env["PYTHONIOENCODING"] = "utf-8"
    subprocess.run([sys.executable, LAYER / "tools/_patch_projectsettings_android.py", "--wc", target], env=env, check=True)
    product = LAYER / "patches/0011-android-product-splash.patch"
    run(["git", "apply", "--check", product], target)
    run(["git", "apply", product], target)
    sources = {str(path.relative_to(target)): hashlib.sha256(path.read_bytes()).hexdigest()
               for path in sorted((target / "Assets").rglob("*.cs"))}
    manifest = {"target": str(target), "base": lock["base"]["commit"],
                "test_package": "com.meiheweite.pro.android.maintenance", "sources": sources,
                "native": native_hashes,
                "patches": {p.name: hashlib.sha256(p.read_bytes()).hexdigest()
                            for p in sorted((LAYER / "patches").glob("*.patch"))}}
    (OUT / "build-input.json").write_text(json.dumps(manifest, ensure_ascii=False, indent=2), encoding="utf-8")
    print("READY: " + str(target), flush=True)

if __name__ == "__main__":
    main()
