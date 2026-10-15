"""Initialize downloaded card effects against the actual desktop core.

This checks Lua loading/initial_effect, not resolution of every card effect.
All inputs are read-only; output is a dated evidence file.
"""
from pathlib import Path
import ctypes as C
import hashlib
import json
import sqlite3
import zipfile
import argparse

ROOT = Path(__file__).resolve().parents[3]
OUT = ROOT / "_takeover_audit/issues_20261011"
RUNTIME = ROOT / "_takeover_audit/implementation_20261011/runtime"

class CardData(C.Structure):
    _fields_ = [("code", C.c_uint32), ("alias", C.c_uint32), ("sets", C.c_uint64 * 4)] + [
        (name, C.c_int32) for name in ("type", "level", "attribute", "race", "attack", "defense", "lscale", "rscale", "link", "rule")]

def initialize(installed=False):
    dll = RUNTIME / "MeiheweitePro_Data/Plugins/x86_64/ocgcore.dll"
    core = C.CDLL(str(dll))
    core.create_duel.argtypes = [C.c_uint32]
    core.create_duel.restype = C.c_void_p
    core.end_duel.argtypes = [C.c_void_p]
    core.new_card.argtypes = [C.c_void_p, C.c_uint32] + [C.c_uint8] * 5
    core.get_log_message.argtypes = [C.c_void_p, C.c_void_p]
    db = sqlite3.connect(RUNTIME / "cdb/cards.cdb")
    rows = {row[0]: row for row in db.execute("SELECT id,alias,setcode,type,level,attribute,race,atk,def FROM datas")}
    scripts = {}
    archives = [*list((RUNTIME / "expansions").glob("*.ypk")), RUNTIME / "data/script.zip"] if installed else [RUNTIME / "data/script.zip"]
    for path in archives:
        with zipfile.ZipFile(path) as archive:
            for name in archive.namelist():
                if name.startswith("script/") and name.endswith(".lua") and "/" not in name[7:]:
                    if name[7:] not in scripts: scripts[name[7:]] = archive.read(name)
    sources = sorted((OUT / "upstream").glob("c*.lua"))
    if installed:
        for path in (RUNTIME / "script").glob("*.lua"):
            if path.name not in scripts: scripts[path.name] = path.read_bytes()
    else:
        for path in sources: scripts[path.name] = path.read_bytes()
    buffers = {name: C.create_string_buffer(data) for name, data in scripts.items()}
    errors, requested = [], []
    @C.CFUNCTYPE(C.c_void_p, C.c_char_p, C.POINTER(C.c_int))
    def reader(name, length):
        key = name.decode("utf-8").replace("\\", "/").split("/")[-1]
        requested.append(key)
        if key not in buffers:
            length[0] = 0
            errors.append("missing script: " + key)
            return None
        length[0] = len(scripts[key])
        return C.addressof(buffers[key])
    @C.CFUNCTYPE(C.c_uint32, C.c_uint32, C.POINTER(CardData))
    def card_reader(code, target):
        C.memset(target, 0, C.sizeof(CardData))
        row = rows.get(code)
        if not row: return 0
        value = target.contents
        value.code, value.alias, value.sets[0], value.type = row[:4]
        level = row[4] & 0xffffffff
        value.level, value.lscale, value.rscale = level & 255, (level >> 24) & 255, (level >> 16) & 255
        value.attribute, value.race, value.attack, value.defense = row[5:]
        if value.type & 0x4000000: value.link, value.defense = value.defense, 0
        return code
    @C.CFUNCTYPE(C.c_uint32, C.c_void_p, C.c_uint32)
    def message(duel, kind):
        buffer = C.create_string_buffer(4096)
        core.get_log_message(duel, buffer)
        errors.append(buffer.value.decode("utf-8", "replace"))
        return 0
    core.set_script_reader(reader)
    core.set_card_reader(card_reader)
    core.set_message_handler(message)
    # Keep callbacks, buffers and the read-only database alive while native Lua runs.
    core._maintenance_refs = (reader, card_reader, message, buffers, db)
    return core, dll, sources, scripts, buffers, errors, requested

def main():
    global RUNTIME, OUT
    parser = argparse.ArgumentParser()
    parser.add_argument("--installed", action="store_true", help="use isolated player archive/loose scripts instead of overriding with upstream fixtures")
    parser.add_argument("--profile", choices=("candidate", "formal"), default="candidate")
    parser.add_argument("--all-loose", action="store_true", help="initialize every installed loose card script")
    parser.add_argument("--runtime", type=Path)
    parser.add_argument("--out", type=Path)
    args = parser.parse_args()
    if (args.all_loose or args.profile == "formal") and not args.installed:
        parser.error("--all-loose and --profile formal require --installed")
    if args.profile == "formal": RUNTIME = ROOT / "YGOProUnity_V2/output/Windows"
    if args.runtime: RUNTIME = args.runtime.resolve()
    if args.out: OUT = args.out.resolve()
    OUT.mkdir(parents=True, exist_ok=True)
    core, dll, sources, scripts, buffers, errors, requested = initialize(args.installed)
    if args.all_loose: sources = sorted((RUNTIME / "script").glob("c*.lua"))
    if not sources: raise RuntimeError("No card scripts to validate")
    results = []
    for path in sources:
        code = int(path.stem[1:])
        if path.name not in scripts: raise RuntimeError("Card script missing: " + path.name)
        if not core._maintenance_refs[-1].execute("SELECT 1 FROM datas WHERE id=?", (code,)).fetchone():
            raise RuntimeError("Card not present in actual database: " + str(code))
        before = len(errors)
        duel = core.create_duel(12345)
        if not duel: raise RuntimeError("Core did not create duel")
        try: core.new_card(duel, code, 0, 0, 2, 0, 1)
        finally: core.end_duel(duel)
        results.append({"code": code, "sha256": hashlib.sha256(scripts.get(path.name, b"")).hexdigest(),
                        "errors": errors[before:]})
    manifest = {"coverage": "actual desktop core card initial_effect only", "scripts": "installed" if args.installed else "upstream fixtures", "core_sha256": hashlib.sha256(dll.read_bytes()).hexdigest(),
                "results": results, "requested": sorted(set(requested)), "status": "failed" if errors else "passed"}
    manifest.update({"profile": args.profile, "runtime": str(RUNTIME),
        "client_sha256": hashlib.sha256((RUNTIME / "MeiheweitePro_Data/Managed/Assembly-CSharp.dll").read_bytes()).hexdigest(),
        "archive_sha256": hashlib.sha256((RUNTIME / "data/script.zip").read_bytes()).hexdigest(),
        "database_sha256": hashlib.sha256((RUNTIME / "cdb/cards.cdb").read_bytes()).hexdigest()})
    filename = "native-installed-script-smoke.json" if args.installed else "native-script-smoke.json"
    if args.all_loose: filename = "native-all-loose-" + args.profile + ".json"
    elif args.profile == "formal": filename = "native-installed-script-smoke-formal.json"
    (OUT / filename).write_text(json.dumps(manifest, ensure_ascii=False, indent=2), encoding="utf-8")
    print(json.dumps({"status": manifest["status"], "cards": len(results), "errors": errors}, ensure_ascii=False))
    return bool(errors)

if __name__ == "__main__": raise SystemExit(main())
