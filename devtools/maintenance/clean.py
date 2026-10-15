"""Delete only explicitly owned temporary artifacts; preview is the default."""
from pathlib import Path
import argparse
import datetime
import json
import shutil

ROOT = Path(__file__).resolve().parents[3]
OWNED_DIRECTORIES = ("runtime", "android-stage", "android-desired", "fault-fixture", "patch-fixture")

def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--execute", action="store_true")
    parser.add_argument("--verbose", action="store_true")
    parser.add_argument("--date", default=datetime.date.today().strftime("%Y%m%d"))
    parser.add_argument("--only", action="append", choices=OWNED_DIRECTORIES + ("compile-dependencies",),
                        help="limit cleanup to selected owned artifacts; repeat to select more")
    args = parser.parse_args()
    if len(args.date) != 8 or not args.date.isdigit():
        parser.error("date must be YYYYMMDD")
    base = (ROOT / "_takeover_audit" / ("implementation_" + args.date)).resolve()
    if base.parent != (ROOT / "_takeover_audit").resolve():
        raise RuntimeError("Cleanup root escaped audit directory")
    # The compile gate copied these dependencies from the existing player; keep candidate DLL.
    player = ROOT / "YGOProUnity_V2/output/Windows/MeiheweitePro_Data/Managed"
    selected = set(args.only or OWNED_DIRECTORIES + ("compile-dependencies",))
    paths = [base / name for name in OWNED_DIRECTORIES if name in selected]
    if "compile-dependencies" in selected:
        paths += [base / dll.name for dll in player.glob("*.dll") if dll.name.lower() != "assembly-csharp.dll"]
        paths += [base / "menu-session.patch"]
    removed = []
    for path in paths:
        if not path.exists(): continue
        if path.is_symlink() or path.is_junction():
            raise RuntimeError("Refusing link/junction cleanup: " + str(path))
        if path.resolve().parent != base:
            raise RuntimeError("Cleanup target escaped artifact directory: " + str(path))
        if args.verbose or path.is_dir():
            print(("DELETE " if args.execute else "PREVIEW ") + str(path), flush=True)
        if args.execute:
            if path.is_dir(): shutil.rmtree(path)
            else: path.unlink()
            removed.append(path.name)
    print("Cleanup deleted %s owned targets" % len(removed) if args.execute else "Preview only; nothing deleted", flush=True)
    if args.execute:
        (base / "cleanup.json").write_text(json.dumps({"deleted": removed, "retained":
            ["source baselines", "logs", "result.json", "desktop and Android candidate DLLs"]}, indent=2), encoding="utf-8")
    return 0

if __name__ == "__main__":
    raise SystemExit(main())
