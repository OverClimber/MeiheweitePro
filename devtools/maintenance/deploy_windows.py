"""Promote a verified independent player, with a file whitelist and rollback.

Use --execute only following the user's explicit request to promote this build.
This records limited test coverage; it does not mark full release acceptance.
"""
from pathlib import Path
import argparse
import datetime
import hashlib
import json
import os
import re
import shutil
import subprocess
import sys
import zipfile

ROOT = Path(__file__).resolve().parents[3]
TOOLS = Path(__file__).parent
FORMAL = ROOT / "YGOProUnity_V2/output/Windows"
DLL = "MeiheweitePro_Data/Managed/Assembly-CSharp.dll"
ARCHIVE = "data/script.zip"

def sha(path):
    with path.open("rb") as stream: return hashlib.file_digest(stream, "sha256").hexdigest()

def read(path): return json.loads(path.read_text(encoding="utf-8"))

def write(path, value):
    temporary = path.with_suffix(path.suffix + ".tmp")
    temporary.write_text(json.dumps(value, ensure_ascii=False, indent=2), encoding="utf-8")
    os.replace(temporary, path)

def checked(base, relative):
    path = base / relative
    if path.resolve() != base.resolve() and base.resolve() not in path.resolve().parents:
        raise RuntimeError("Path escaped its root: " + str(path))
    for parent in (path, *path.parents):
        if parent == base.parent: break
        if parent.is_symlink() or parent.is_junction():
            raise RuntimeError("Links are not supported: " + str(parent))
    return path

def idle(candidate):
    command = "[Console]::OutputEncoding=[System.Text.Encoding]::UTF8; @(Get-Process | Where-Object { $_.ProcessName -match 'Meiheweite|AI.Server|WindBot' } | Select-Object Id,Path) | ConvertTo-Json -Compress"
    result = subprocess.run(["powershell", "-NoProfile", "-Command", command],
                            capture_output=True, encoding="utf-8", errors="strict", check=True)
    processes = json.loads(result.stdout) if result.stdout.strip() else []
    if isinstance(processes, dict): processes = [processes]
    for process in processes:
        if not process["Path"]: raise RuntimeError("Cannot identify an active game process")
        path = Path(process["Path"]).resolve()
        if any(root.resolve() in path.parents for root in (FORMAL, candidate)):
            raise RuntimeError("Close the player before deployment: " + str(process))

def inventory(excluded):
    # Hash personal data and packs. For large images/player files also verify
    # the complete path/size/mtime inventory; none are in the write whitelist.
    result = {}
    personal = {"config", "deck", "deck_rd", "replay", "expansions", "cdb", "translation"}
    for path in FORMAL.rglob("*"):
        if path.is_symlink() or path.is_junction(): raise RuntimeError("Linked player content: " + str(path))
        if not path.is_file(): continue
        name = path.relative_to(FORMAL).as_posix()
        if name in excluded: continue
        info = path.stat()
        result[name] = [info.st_size, info.st_mtime_ns,
                       sha(path) if name.split("/")[0] in personal else None]
    return result

def run_tool(name, arguments, output):
    result = subprocess.run([sys.executable, str(TOOLS / name), *arguments],
                            capture_output=True, encoding="utf-8", errors="replace", timeout=180)
    output.write_text(result.stdout + result.stderr, encoding="utf-8")
    if result.returncode: raise RuntimeError("Validation failed: " + name + "; see " + str(output))

def restore(batch, manifest):
    # Preflight all targets before restoring any. Refuse to overwrite later edits.
    for row in manifest["files"]:
        path = checked(FORMAL, row["path"])
        current = sha(path) if path.is_file() else None
        if current not in (row["before"], row["after"]):
            raise RuntimeError("Rollback would overwrite a later change: " + row["path"])
        if row["before"] and sha(checked(batch / "baseline/player", row["path"])) != row["before"]:
            raise RuntimeError("Rollback backup mismatch: " + row["path"])
    for row in reversed(manifest["files"]):
        path = checked(FORMAL, row["path"])
        if (sha(path) if path.is_file() else None) == row["before"]: continue
        if row["before"]:
            temporary = path.with_name(path.name + ".maintenance-rollback.tmp")
            if temporary.exists(): raise RuntimeError("Rollback temporary already exists")
            try:
                shutil.copy2(checked(batch / "baseline/player", row["path"]), temporary)
                os.replace(temporary, path)
            finally: temporary.unlink(missing_ok=True)
        else: path.unlink()
    if not manifest["script_directory_existed"] and (FORMAL / "script").is_dir():
        if not any((FORMAL / "script").iterdir()): (FORMAL / "script").rmdir()
    manifest["status"] = "rolled_back"
    write(batch / "deployment.json", manifest)

def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--date", default=datetime.date.today().strftime("%Y%m%d"))
    mode = parser.add_mutually_exclusive_group()
    mode.add_argument("--execute", action="store_true")
    mode.add_argument("--rollback", action="store_true")
    args = parser.parse_args()
    if not re.fullmatch(r"\d{8}", args.date): parser.error("date must be YYYYMMDD")
    build = ROOT / "_takeover_audit" / ("implementation_" + args.date)
    issues = ROOT / "_takeover_audit" / ("issues_" + args.date)
    batch = ROOT / "_takeover_audit" / ("windows_deployment_" + args.date)
    candidate = build / "runtime"
    idle(candidate)
    if args.rollback:
        restore(batch, read(batch / "deployment.json"))
        print("Rollback completed")
        return 0
    if (batch / "deployment.json").exists(): raise RuntimeError("Deployment journal already exists; inspect it first")
    manifest = read(build / "result.json")
    expected = manifest["dll_sha256"]
    if manifest["status"] != "passed" or sha(candidate / DLL) != expected or sha(build / "Assembly-CSharp.dll") != expected:
        raise RuntimeError("Candidate differs from the compiled build")
    for name, digest in read(build / "desktop-source-sha256.json").items():
        if sha(ROOT / "YGOProUnity_V2" / name) != digest: raise RuntimeError("Compiled source drift: " + name)
    matrix = read(issues / "desktop-gameplay-matrix.json")
    if matrix["status"] != "passed" or len(matrix["cases"]) != 4 or any(row["sha256"] != expected for row in matrix["cases"]):
        raise RuntimeError("Desktop gameplay evidence differs from candidate")
    dependencies = ["MeiheweitePro.exe", "UnityPlayer.dll", "AI.Server.exe",
                    "MeiheweitePro_Data/Plugins/x86_64/ocgcore.dll", "cdb/cards.cdb"]
    dependencies += [p.relative_to(candidate).as_posix() for p in (candidate / "MeiheweitePro_Data/Managed").glob("*.dll") if p.name != "Assembly-CSharp.dll"]
    dependencies += [p.relative_to(candidate).as_posix() for p in (candidate / "expansions").glob("*.ypk")]
    if {p.name for p in (FORMAL / "expansions").glob("*.ypk")} != {p.name for p in (candidate / "expansions").glob("*.ypk")}:
        raise RuntimeError("Expansion sets differ")
    for name in dependencies:
        if sha(checked(FORMAL, name)) != sha(checked(candidate, name)): raise RuntimeError("Player dependency differs: " + name)
    baseline_hash = sha(FORMAL / ARCHIVE)
    original = candidate / "updates/script-compatibility" / (baseline_hash + ".zip")
    if not original.is_file() or sha(original) != baseline_hash:
        raise RuntimeError("Formal script archive differs from tested compatibility baseline")
    suffix = (build / "type-compatibility.lua").read_bytes()
    with zipfile.ZipFile(original) as old, zipfile.ZipFile(candidate / ARCHIVE) as new:
        if old.namelist() != new.namelist(): raise RuntimeError("Script archive entries differ")
        for name in old.namelist():
            if new.read(name) != old.read(name) + (suffix if name == "script/utility.lua" else b""):
                raise RuntimeError("Unexpected archive change: " + name)
        archive_count = len(old.namelist())
    available = set()
    for folder in ("script", "expansions/script", "expansions"):
        available.update(p.name.lower() for p in (FORMAL / folder).glob("c*.lua"))
    for archive in [FORMAL / ARCHIVE, *(FORMAL / "expansions").glob("*.ypk")]:
        with zipfile.ZipFile(archive) as zip_file:
            available.update(name[7:].lower() for name in zip_file.namelist() if name.startswith("script/") and "/" not in name[7:])
    paths = [DLL, ARCHIVE]
    paths += ["script/" + p.name for p in sorted((candidate / "script").glob("c*.lua")) if p.name.lower() not in available]
    print("Verified %d matching dependencies, %d preserved ZIP entries; plan: %d replacements, %d missing scripts" % (len(dependencies), archive_count, 2, len(paths)-2), flush=True)
    if not args.execute:
        print("Preview only; use --execute after user authorization")
        return 0
    batch.mkdir(exist_ok=True)
    for name in ("card-activation-formal.json", "native-installed-script-smoke-formal.json", "native-all-loose-formal.json"):
        if (issues / name).exists():
            destination = batch / "baseline" / name
            destination.parent.mkdir(parents=True, exist_ok=True)
            if destination.exists(): raise RuntimeError("Evidence backup already exists")
            shutil.copy2(issues / name, destination)
    run_tool("card_validation_gate.py", [], batch / "candidate-card-gate.log")
    run_tool("native_script_smoke.py", ["--installed", "--all-loose"], batch / "candidate-all-scripts.log")
    idle(candidate)
    before = inventory(set(paths))
    write(batch / "protected-files-before.json", before)
    journal = {"status": "prepared", "started_utc": datetime.datetime.now(datetime.timezone.utc).isoformat(),
               "authorization": "User explicitly requested merging the tested build into output/Windows",
               "coverage": "User reported candidate works; native script initialization and 19 activation cases; hash-bound OCG/RD opening replay evidence. Full release acceptance remains incomplete.",
               "target": str(FORMAL), "client_sha256": expected, "dependencies_compared": len(dependencies),
               "archive_entries_compared": archive_count, "script_directory_existed": (FORMAL / "script").exists(), "files": []}
    stage = checked(batch, "staged")
    if stage.exists(): raise RuntimeError("Staging directory already exists; inspect before retry")
    for name in paths:
        source, target = checked(candidate, name), checked(FORMAL, name)
        staged = checked(stage, name)
        staged.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(source, staged)
        old = sha(target) if target.exists() else None
        if old:
            backup = checked(batch / "baseline/player", name)
            if backup.exists(): raise RuntimeError("Backup already exists; inspect before retry")
            backup.parent.mkdir(parents=True, exist_ok=True)
            shutil.copy2(target, backup)
            if sha(backup) != old: raise RuntimeError("Backup mismatch: " + name)
        if sha(staged) != sha(source): raise RuntimeError("Staging mismatch: " + name)
        journal["files"].append({"path": name, "before": old, "after": sha(source)})
    write(batch / "deployment.json", journal)
    try:
        for row in journal["files"]:
            target = checked(FORMAL, row["path"])
            if (sha(target) if target.exists() else None) != row["before"]: raise RuntimeError("Target changed during staging")
            target.parent.mkdir(parents=True, exist_ok=True)
            os.replace(checked(stage, row["path"]), target)
            if sha(target) != row["after"]: raise RuntimeError("Installed hash mismatch")
        run_tool("native_script_smoke.py", ["--installed", "--all-loose", "--profile", "formal"], batch / "formal-all-scripts.log")
        run_tool("native_script_activation.py", ["--profile", "formal"], batch / "formal-activation.log")
        after = inventory(set(paths))
        if after != before: raise RuntimeError("Unrelated player files changed; inspect protected inventory")
        journal.update({"status": "deployed", "protected_files_verified": len(before),
                        "finished_utc": datetime.datetime.now(datetime.timezone.utc).isoformat()})
        for name in ("native-all-loose-formal.json", "card-activation-formal.json"):
            shutil.copy2(issues / name, batch / name)
        write(batch / "deployment.json", journal)
        print("Deployed; all %d unrelated player files preserved; formal native checks passed" % len(before), flush=True)
    except BaseException as error:
        journal["error"] = str(error)
        write(batch / "deployment.json", journal)
        restore(batch, journal)
        raise
    finally:
        if stage.exists():
            checked(batch, "staged")
            shutil.rmtree(stage)
    return 0

if __name__ == "__main__": raise SystemExit(main())
