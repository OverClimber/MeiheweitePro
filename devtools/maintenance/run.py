"""Maintenance regression gate. Never deploys DLLs or edits production assets."""
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
PROJ = ROOT / "YGOProUnity_V2"
sys.dont_write_bytecode = True

def load(name, path):
    spec = importlib.util.spec_from_file_location(name, path)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module

def run(command, cwd=None):
    result = subprocess.run([str(x) for x in command], cwd=cwd, capture_output=True)
    output = (result.stdout + result.stderr).decode("utf-8", "replace")
    if result.returncode:
        raise RuntimeError("Command failed (%s): %s\n%s" % (result.returncode, command[0], output[-8000:]))
    return output

def patch_tests(out):
    delta = load("delta", ROOT / "YGOProAndroid/tools/_apply_layer_delta.py")
    fixture = out / "patch-fixture"
    fixture.mkdir(exist_ok=True)
    delta.WC = str(fixture)
    delta.PATCHES = str(fixture / "patches")
    Path(delta.PATCHES).mkdir(exist_ok=True)
    patch = Path(delta.PATCHES) / "change.patch"
    patch.write_text("diff --git a/sample.cs b/sample.cs\n--- a/sample.cs\n+++ b/sample.cs\n@@ -1 +1 @@\n-old();\n+new();\n", encoding="utf-8")
    sample = fixture / "sample.cs"
    sample.write_text("old();\nnew();\n", encoding="utf-8")
    assert not delta.added_lines_present(str(patch)), "partial patch incorrectly accepted"
    assert delta.apply_patches(True)[2], "inconsistent patch must fail closed"
    sample.write_text("new();\n", encoding="utf-8")
    assert delta.added_lines_present(str(patch)), "applied patch rejected"
    assert len(delta.apply_patches(True)[1]) == 1
    sample.write_text("old();\n", encoding="utf-8")
    assert len(delta.apply_patches(True)[0]) == 1
    assert sample.read_text() == "old();\n", "check mutated source"
    assert not delta.apply_patches(False)[2]
    assert sample.read_text() == "new();\n", "new patch did not apply"
    print("PASS: strict patch detection, rejection, dry run and application", flush=True)

def compile_desktop(out):
    build = load("build", ROOT / "_csc_build.py")
    build.PROJ = str(PROJ)
    build.PLAYER_MANAGED = str(PROJ / "output/Windows/MeiheweitePro_Data/Managed")
    build.EDITOR_DATA = os.environ.get("MEI_DESKTOP_EDITOR", build.EDITOR_DATA)
    sources, skipped = build.collect_sources()
    (out / "desktop-source-sha256.json").write_text(json.dumps({
        str(Path(source).relative_to(PROJ)): hashlib.sha256(Path(source).read_bytes()).hexdigest()
        for source in sorted(sources)}, ensure_ascii=False, indent=2), encoding="utf-8")
    refs = build.collect_refs()
    compiler = Path(build.EDITOR_DATA) / "DotNetSdkRoslyn/csc.dll"
    dotnet = Path(build.EDITOR_DATA) / "NetCoreRuntime/dotnet.exe"
    dll = out / "Assembly-CSharp.dll"
    options = ["-target:library", "-nostdlib+", "-noconfig", "-optimize+", "-unsafe", "-langversion:9.0",
               "-nowarn:1591,0436", "-define:" + ";".join(build.DEFINES), '-out:"%s"' % dll]
    rsp = out / "desktop.rsp"
    rsp.write_text("\n".join(options + ['-r:"%s"' % r for r in refs] + ['"%s"' % s for s in sources]), encoding="utf-8")
    log = run([dotnet, compiler, "@" + str(rsp)], cwd=PROJ)
    (out / "desktop-compile.log").write_text(log, encoding="utf-8")
    print("PASS: desktop compilation (%s sources)" % len(sources), flush=True)
    return build, compiler, dotnet, dll

def fault_tests(out, build, compiler, dotnet, dll):
    player = Path(build.PLAYER_MANAGED)
    for dependency in player.glob("*.dll"):
        if dependency.name.lower() != "assembly-csharp.dll":
            shutil.copy2(dependency, out / dependency.name)
    test = PROJ / "devtools/tests/MaintenanceTests.cs"
    boot = ROOT / "YGOProAndroid/layer/Assets/Android/BootDataInstall.cs"
    bootstrap = ROOT / "YGOProAndroid/layer/Assets/Android/AndroidBoot.cs"
    references = [dll] + [Path(path) for path in build.collect_refs()]
    exe = out / "MaintenanceTests.exe"
    rsp = out / "tests.rsp"
    rsp.write_text("\n".join(["-target:exe", "-nostdlib+", "-langversion:9.0", "-define:UNITY_EDITOR", '-out:"%s"' % exe]
        + ['-r:"%s"' % r for r in references] + ['"%s"' % test, '"%s"' % boot, '"%s"' % bootstrap]), encoding="utf-8")
    log = run([dotnet, compiler, "@" + str(rsp)])
    fixture = out / "fault-fixture"
    if fixture.exists():
        if fixture.resolve().parent != out.resolve():
            raise RuntimeError("Fixture cleanup escaped artifact directory")
        shutil.rmtree(fixture)
    fixture.mkdir(exist_ok=True)
    log += run([exe, fixture])
    mobile = ROOT / "YGOProAndroid/layer/Assets/MobileAi"
    lifecycle = out / "MobileLifecycleTests.exe"
    lifecycle_rsp = out / "mobile-lifecycle.rsp"
    lifecycle_rsp.write_text("\n".join(["-target:exe", "-nostdlib+", "-langversion:9.0", "-define:UNITY_ANDROID",
        "-main:MobileLifecycleTests", '-out:"%s"' % lifecycle]
        + ['-r:"%s"' % r for r in references]
        + ['"%s"' % (PROJ / "devtools/tests/MobileLifecycleTests.cs")]
        + ['"%s"' % (mobile / name) for name in ("InProcessAiServer.cs", "WindbotHost.cs")]), encoding="utf-8")
    log += run([dotnet, compiler, "@" + str(lifecycle_rsp)])
    log += run([lifecycle])
    (out / "fault-tests.log").write_text(log, encoding="utf-8")
    shutil.copy2(fixture / "type-compatibility.lua", out / "type-compatibility.lua")
    print(log, flush=True)

def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--runtime", action="store_true", help="also require actual gameplay/UI regression scripts; missing scripts fail")
    parser.add_argument("--out", type=Path)
    args = parser.parse_args()
    out = (args.out or ROOT / "_takeover_audit" / ("implementation_" + datetime.date.today().strftime("%Y%m%d"))).resolve()
    out.mkdir(parents=True, exist_ok=True)
    manifest = {"started_utc": datetime.datetime.now(datetime.timezone.utc).isoformat(), "status": "failed", "gameplay_runtime": "not run"}
    try:
        for repo in (PROJ, ROOT / "YGOProAndroid"):
            manifest[repo.name] = {"head": run(["git", "-C", repo, "rev-parse", "HEAD"]).strip(),
                                   "status": run(["git", "-C", repo, "status", "--short"])}
        names = ["_verify_shellfile.py", "_verify_deckmemo.py", "_verify_aiundo.py", "_verify_testundo.py"]
        if args.runtime:
            missing = [name for name in names if not (ROOT / name).is_file()]
            if missing:
                raise RuntimeError("Gameplay regression sources missing (historical PASS is insufficient): " + ", ".join(missing))
        patch_tests(out)
        build, compiler, dotnet, dll = compile_desktop(out)
        manifest["dll_sha256"] = hashlib.sha256(dll.read_bytes()).hexdigest()
        fault_tests(out, build, compiler, dotnet, dll)
        if args.runtime:
            for name in names:
                (out / (name + ".log")).write_text(run([sys.executable, ROOT / name], cwd=ROOT), encoding="utf-8")
        manifest["status"] = "passed"
        manifest["gameplay_runtime"] = "passed" if args.runtime else "not run"
    except Exception as error:
        manifest["error"] = str(error)
        print(str(error), file=sys.stderr)
        return 1
    finally:
        (out / "result.json").write_text(json.dumps(manifest, ensure_ascii=False, indent=2), encoding="utf-8")
    print("Maintenance gate passed; gameplay runtime: " + manifest["gameplay_runtime"])
    return 0

if __name__ == "__main__":
    sys.exit(main())
