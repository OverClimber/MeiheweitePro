"""Exercise official effect condition helpers using the actual desktop core."""
import ctypes as C
import hashlib
import json
from native_script_smoke import initialize, OUT

CASES = {
    "required_api": """
assert(type(Card.IsAllCardTypes)=='function','Card.IsAllCardTypes missing')
assert(type(Duel.GetSZoneCount)=='function','Duel.GetSZoneCount missing')
assert(type(Duel.GetMZoneCount)=='function','Duel.GetMZoneCount missing')
""",
    "senet_normal_filter": """
Debug.ReloadFieldBegin(0)
Debug.SetPlayerInfo(0,8000,0,0)
Debug.SetPlayerInfo(1,8000,0,0)
local normal=Debug.AddCard(89631139,0,0,LOCATION_HAND,0,POS_FACEUP_ATTACK)
local effect=Debug.AddCard(10007434,0,0,LOCATION_HAND,1,POS_FACEUP_ATTACK)
Debug.ReloadFieldEnd()
assert(c10007434.chkfilter(normal),'Senet must accept a normal monster')
assert(not c10007434.chkfilter(effect),'Senet must reject an effect monster')
""",
    "moon_cost_filter": """
Debug.ReloadFieldBegin(0)
Debug.SetPlayerInfo(0,8000,0,0)
Debug.SetPlayerInfo(1,8000,0,0)
local normal=Debug.AddCard(89631139,0,0,LOCATION_HAND,0,POS_FACEUP_ATTACK)
Debug.AddCard(75934716,0,0,LOCATION_HAND,1,POS_FACEUP_ATTACK)
Debug.ReloadFieldEnd()
assert(c75934716.costfilter(normal,0),'Moon must accept a usable hand cost with a free monster zone')
""",
}

def main():
    core, dll, sources, scripts, buffers, errors, requested = initialize()
    # Consume the exact production constant emitted by the C# fault gate.
    compatibility = (OUT.parent / "implementation_20261011/type-compatibility.lua").read_bytes()
    core.preload_script.argtypes = [C.c_void_p, C.c_char_p]
    core.preload_script.restype = C.c_int
    original_utility = scripts["utility.lua"]
    while original_utility.endswith(compatibility): original_utility = original_utility[:-len(compatibility)]
    scripts["utility.lua"] = original_utility
    buffers["utility.lua"] = C.create_string_buffer(original_utility)
    baseline_name = "maintenance_unpatched_senet.lua"
    scripts[baseline_name] = CASES["senet_normal_filter"].encode("utf-8")
    buffers[baseline_name] = C.create_string_buffer(scripts[baseline_name])
    before = len(errors)
    duel = core.create_duel(12345)
    try: baseline_loaded = core.preload_script(duel, baseline_name.encode("utf-8"))
    finally: core.end_duel(duel)
    baseline_errors = errors[before:]
    reproduced = not baseline_loaded and any("IsAllCardTypes" in error and "nil value" in error for error in baseline_errors)
    scripts["utility.lua"] = original_utility + compatibility
    buffers["utility.lua"] = C.create_string_buffer(scripts["utility.lua"])
    results = []
    CASES["existing_api_preserved"] = "local existing=function() return 'custom' end\nCard.IsAllCardTypes=existing\n" + compatibility.decode("utf-8") + "\nassert(Card.IsAllCardTypes==existing,'existing API was overwritten')"
    for name, body in CASES.items():
        filename = "maintenance_" + name + ".lua"
        scripts[filename] = body.encode("utf-8")
        buffers[filename] = C.create_string_buffer(scripts[filename])
        before = len(errors)
        duel = core.create_duel(12345)
        if not duel: raise RuntimeError("Core did not create duel")
        try: loaded = core.preload_script(duel, filename.encode("utf-8"))
        finally: core.end_duel(duel)
        results.append({"case": name, "loaded": loaded, "errors": errors[before:]})
    failed = not reproduced or any(not row["loaded"] or row["errors"] for row in results)
    result = {"status": "failed" if failed else "passed",
        "core_sha256": hashlib.sha256(dll.read_bytes()).hexdigest(),
        "coverage": "official Lua API availability and Senet/Moon cost filters only",
        "unpatched_failure_reproduced": reproduced, "unpatched_errors": baseline_errors, "cases": results}
    (OUT / "native-script-conditions.json").write_text(json.dumps(result, ensure_ascii=False, indent=2), encoding="utf-8")
    print(json.dumps(result, ensure_ascii=False))
    return int(failed)

if __name__ == "__main__": raise SystemExit(main())
