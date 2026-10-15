"""Rebuild pinned Android C# inputs in the audit directory, with strict patches.

--generate-maintenance-patch exports desktop fixes as one additive Android patch;
it does not change the pinned baseline, old patches, or the active workcopy.
"""
from pathlib import Path
import argparse
import datetime
import difflib
import json
import hashlib
import os
import shutil
import subprocess
import sys

ROOT = Path(__file__).resolve().parents[3]
DELIVERY = ROOT / "YGOProUnity_V2"
LAYER = ROOT / "YGOProAndroid"
OUT = ROOT / "_takeover_audit" / ("implementation_" + datetime.date.today().strftime("%Y%m%d"))
PATCH_NAME = "0019-maintenance-resource-reliability.patch"

def git(args, cwd=DELIVERY):
    result = subprocess.run(["git"] + args, cwd=cwd, capture_output=True)
    if result.returncode:
        raise RuntimeError(result.stderr.decode("utf-8", "replace"))
    return result.stdout

def export(stage, commit):
    names = git(["ls-tree", "-r", "--name-only", commit]).decode("utf-8").splitlines()
    names = [name for name in names if name.startswith("Assets/") and name.endswith((".cs", ".dll"))]
    request = "".join(commit + ":" + name + "\n" for name in names).encode("utf-8")
    result = subprocess.run(
        ["git", "cat-file", "--batch"], input=request, cwd=DELIVERY, capture_output=True)
    if result.returncode: raise RuntimeError(result.stderr.decode("utf-8", "replace"))
    stream = result.stdout
    position = 0
    for name in names:
        end = stream.index(b"\n", position)
        length = int(stream[position:end].split()[-1])
        data = stream[end + 1:end + 1 + length]
        target = stage / name
        target.parent.mkdir(parents=True, exist_ok=True)
        target.write_bytes(data)
        position = end + length + 2

def build_stage(stage, include_maintenance):
    if stage.exists():
        if stage.resolve().parent != OUT.resolve(): raise RuntimeError("Stage escaped audit directory")
        shutil.rmtree(stage)
    stage.mkdir(parents=True)
    lock = json.loads((LAYER / "LAYER.lock.json").read_text(encoding="utf-8"))
    export(stage, lock["base"]["commit"])
    for source in (LAYER / "layer/Assets").rglob("*"):
        if source.is_file() and source.suffix in (".cs", ".dll"):
            target = stage / "Assets" / source.relative_to(LAYER / "layer/Assets")
            target.parent.mkdir(parents=True, exist_ok=True)
            shutil.copy2(source, target)
    for patch in sorted((LAYER / "patches").glob("*.patch")):
        if patch.name == PATCH_NAME and not include_maintenance: continue
        # Only C# patches are relevant to this source compile stage.
        arguments = ["apply", "--include=*.cs", str(patch)]
        git(arguments[:1] + ["--check"] + arguments[1:], cwd=stage)
        git(arguments, cwd=stage)
    # firstpass was built by Unity; use the actual Android reference, never the desktop GDI build.
    firstpass = Path(os.environ.get("MEI_ANDROID_FIRSTPASS", "D:/Game/YGOProAndroid-build/Library/ScriptAssemblies/Assembly-CSharp-firstpass.dll"))
    target = stage / "Library/ScriptAssemblies"
    target.mkdir(parents=True, exist_ok=True)
    shutil.copy2(firstpass, target / firstpass.name)
    shutil.copy2(firstpass.parent / "StbImageSharp.dll", target / "StbImageSharp.dll")

def generate(stage):
    desired = OUT / "android-desired"
    desired.mkdir(exist_ok=True)
    files = ["Menu/Menu.cs", "Program.cs", "ResourceManagers/ClientDataUpdater.cs",
             "ResourceManagers/UnityFileDownloader.cs", "ResourceManagers/GameTextureManager.cs",
             "ResourceManagers/GameZipManager.cs", "ResourceManagers/ManagedPackInstall.cs",
             "ResourceManagers/PictureAbsentCache.cs", "ResourceManagers/ZipImageIndex.cs",
             "ResourceManagers/CardScriptUpdater.cs"]
    diffs = []
    for name in files:
        rel = "Assets/SibylSystem/" + name
        target = desired / rel
        target.parent.mkdir(parents=True, exist_ok=True)
        if name == "Program.cs":
            # Keep all Android-specific startup, in-process AI and touch hooks.
            text = (stage / rel).read_text(encoding="utf-8-sig")
            point = "            ClientDataUpdater.RecoverTransaction();"
            if text.count(point) != 1: raise RuntimeError("Program recovery anchor drift")
            text = text.replace(point, point + '\n            ManagedPackInstall.Recover("expansions/ygopro-super-pre.ypk");')
            compatibility = "            QuickTestTrace.SweepStaleSwitches();"
            if text.count(compatibility) != 1: raise RuntimeError("Script compatibility startup anchor drift")
            text = text.replace(compatibility, compatibility + "\n            CardScriptUpdater.EnsureTypeCompatibility(Directory.GetCurrentDirectory());")
            zip_loop = "foreach (ZipFile zip in GameZipManager.Zips)"
            if text.count(zip_loop) != 3: raise RuntimeError("Program ZIP consumer anchor drift")
            text = text.replace(zip_loop, "lock (GameZipManager.SyncRoot)\n            " + zip_loop)
            startup = "    void Start()"
            desktop_text = (DELIVERY / rel).read_text(encoding="utf-8-sig")
            begin = desktop_text.index("    // 外部启动器可能继承自己的工作目录")
            initializer = desktop_text[begin:desktop_text.index(startup, begin)]
            if text.count(startup) != 1: raise RuntimeError("Program startup anchor drift")
            text = text.replace(startup, initializer + startup)
            start_body = "    void Start()\n    {"
            if text.count(start_body) != 1: raise RuntimeError("Program Start body drift")
            text = text.replace(start_body, start_body + "\n        InitializePlayerDirectory();\n        QuickTestTrace.ResetForCurrentDirectory();")
            target.write_text(text, encoding="utf-8")
        elif name == "Menu/Menu.cs":
            # Preserve Android layout/user edits, apply only this session's reviewed delta.
            before = (ROOT / "_takeover_audit/baseline_20261011/YGOProUnity_V2" / rel).read_text(encoding="utf-8-sig")
            after = (DELIVERY / rel).read_text(encoding="utf-8-sig")
            target.write_bytes((stage / rel).read_bytes())
            patch = OUT / "menu-session.patch"
            patch.write_text("".join(difflib.unified_diff(before.splitlines(True), after.splitlines(True),
                fromfile="a/" + rel, tofile="b/" + rel)), encoding="utf-8")
            git(["apply", "--check", str(patch)], cwd=desired)
            git(["apply", str(patch)], cwd=desired)
        else:
            shutil.copy2(DELIVERY / rel, target)
            if name == "ResourceManagers/GameTextureManager.cs":
                git(["apply", "--check", str(LAYER / "patches/0001-android-gametexturemanager.patch")], cwd=desired)
                git(["apply", str(LAYER / "patches/0001-android-gametexturemanager.patch")], cwd=desired)
            elif name == "ResourceManagers/ClientDataUpdater.cs":
                git(["apply", "--include=" + rel, "--check", str(LAYER / "patches/0002-android-boot-path.patch")], cwd=desired)
                git(["apply", "--include=" + rel, str(LAYER / "patches/0002-android-boot-path.patch")], cwd=desired)
        original = stage / rel
        before = original.read_text(encoding="utf-8") if original.exists() else ""
        after = target.read_text(encoding="utf-8")
        if before != after:
            diffs.append("diff --git a/%s b/%s\n" % (rel, rel))
            if not original.exists(): diffs.append("new file mode 100644\n")
            diffs.extend(difflib.unified_diff(before.splitlines(True), after.splitlines(True),
                fromfile="a/" + rel if original.exists() else "/dev/null", tofile="b/" + rel))
    (LAYER / "patches" / PATCH_NAME).write_text("".join(diffs), encoding="utf-8", newline="\n")

def main():
    global OUT
    parser = argparse.ArgumentParser()
    parser.add_argument("--out", type=Path, help="isolated audit output directory")
    parser.add_argument("--generate-maintenance-patch", action="store_true")
    parser.add_argument("--reuse-stage", action="store_true", help="strictly reverse the previous maintenance patch in a verified existing stage")
    args = parser.parse_args()
    if args.out:
        OUT = args.out.resolve()
    OUT.mkdir(parents=True, exist_ok=True)
    stage = OUT / "android-stage"
    if args.reuse_stage:
        if not args.generate_maintenance_patch or not stage.is_dir():
            raise RuntimeError("Reuse requires an existing stage and --generate-maintenance-patch")
        previous = LAYER / "patches" / PATCH_NAME
        git(["apply", "--check", "-R", str(previous)], cwd=stage)
        git(["apply", "-R", str(previous)], cwd=stage)
    else:
        build_stage(stage, include_maintenance=not args.generate_maintenance_patch)
    if args.generate_maintenance_patch:
        generate(stage)
        git(["apply", "--check", str(LAYER / "patches" / PATCH_NAME)], cwd=stage)
        git(["apply", str(LAYER / "patches" / PATCH_NAME)], cwd=stage)
    env = os.environ.copy()
    env["PYTHONIOENCODING"] = "utf-8"
    env["MEI_WORKCOPY"] = str(stage)
    env["MEI_GATE_OUT"] = str(OUT / "android-compile")
    result = subprocess.run([sys.executable, LAYER / "tools/_csc_gate_mobile.py"], env=env)
    manifest = {"status": "passed" if result.returncode == 0 else "failed",
                "gameplay_runtime": "not run", "apk_and_device": "not run",
                "lock": json.loads((LAYER / "LAYER.lock.json").read_text(encoding="utf-8")),
                "patches": {path.name: hashlib.sha256(path.read_bytes()).hexdigest()
                            for path in sorted((LAYER / "patches").glob("*.patch"))},
                "layer_sources": {str(path.relative_to(LAYER)): hashlib.sha256(path.read_bytes()).hexdigest()
                                  for path in sorted((LAYER / "layer").rglob("*.cs"))}}
    dll = OUT / "android-compile/Assembly-CSharp.dll"
    if result.returncode == 0:
        manifest["dll_sha256"] = hashlib.sha256(dll.read_bytes()).hexdigest()
    (OUT / "android-result.json").write_text(json.dumps(manifest, ensure_ascii=False, indent=2), encoding="utf-8")
    return result.returncode

if __name__ == "__main__":
    try: sys.exit(main())
    except Exception as error:
        print(error, file=sys.stderr)
        sys.exit(1)
