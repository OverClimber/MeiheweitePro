"""Check registered official effects' real cost/target callbacks on the shipped core.

Positive and negative field fixtures exercise activation eligibility. They do not
claim a complete chain has resolved or that every card branch has been covered.
The formal profile checks output/Windows read-only. Before deployment it exposed
missing scripts; after promotion it must pass against the installed files.
"""
import argparse
import ctypes as C
import hashlib
import json
from pathlib import Path
import native_script_smoke as native
from native_script_smoke import ROOT, OUT, RUNTIME

def add(code, location="HAND", player=0, sequence=0):
    return f"Debug.AddCard({code},{player},{player},LOCATION_{location},{sequence},POS_FACEUP_ATTACK)"

def fixture(code, location, selector, cards, cost=True, target=True):
    # Observe the actual effects passed to native RegisterEffect. Native registration
    # still runs; IsActivatable evaluates cost and target in native effect context.
    return """
Debug.ReloadFieldBegin(0)
Debug.SetPlayerInfo(0,8000,0,0)
Debug.SetPlayerInfo(1,8000,0,0)
local effects={}
local register=Card.RegisterEffect
Card.RegisterEffect=function(c,e,...)
    local result=register(c,e,...)
    if c:GetCode()==HOST then effects[#effects+1]=e end
    return result
end
local host=HOST_ADD
EXTRA_CARDS
Card.RegisterEffect=register
Debug.ReloadFieldEnd()
local found
for _,e in ipairs(effects) do if SELECTOR then found=e break end end
assert(found,'Expected registered effect missing')
-- The core establishes reason_effect/reason_player before calling the real
-- callbacks. Calling a targeting callback directly lacks that native context.
local eligible=found:IsActivatable(0,false,false)
assert(not not eligible==(COST_OK and TARGET_OK),'Unexpected native activation eligibility')
""".replace("HOST_ADD", add(code, location)).replace("HOST", str(code)).replace(
    "EXTRA_CARDS", "\n".join(cards)).replace("SELECTOR", selector).replace(
    "COST_OK", str(cost).lower()).replace("TARGET_OK", str(target).lower())

IGNITION = "e:IsHasType(EFFECT_TYPE_IGNITION)"
TRIGGER = "e:GetCode()==EVENT_SUMMON_SUCCESS"
QUICK = "e:IsHasType(EFFECT_TYPE_QUICK_O)"
ACTIVATE = "e:IsHasType(EFFECT_TYPE_ACTIVATE)"

def cases():
    rows = []
    def case(name, code, location, selector, cards, cost=True, target=True):
        rows.append((name, code, fixture(code, location, selector, cards, cost, target)))
    case("amenhotep_summon_trigger_available",10007434,"MZONE",TRIGGER,[add(89631139),add(12555008,"DECK")])
    case("amenhotep_no_normal_monster",10007434,"MZONE",TRIGGER,[add(12555008,"DECK")],target=False)
    case("nefertari_summon_trigger_available",84512726,"MZONE",TRIGGER,[add(89631139),add(9762295,"DECK")])
    case("nefertari_no_senet_spell",84512726,"MZONE",TRIGGER,[add(89631139)],target=False)
    case("nekhbet_hand_recovery_available",10884847,"HAND",IGNITION,[add(10007434,"GRAVE")])
    case("nekhbet_no_grave_target",10884847,"HAND",IGNITION,[],target=False)
    case("sphinx_hand_search_available",47095439,"HAND",IGNITION,[add(84512726,"DECK")])
    case("sphinx_same_name_is_not_search_target",47095439,"HAND",IGNITION,[add(47095439,"DECK")],target=False)
    case("adjat_hand_send_available",73490148,"HAND",IGNITION,[add(10007434,"DECK")])
    case("adjat_no_deck_target",73490148,"HAND",IGNITION,[],target=False)
    case("senet_two_normals_and_fusion",9762295,"HAND",ACTIVATE,[add(89631139,sequence=1),add(89631139,sequence=2),add(45231177,"EXTRA")])
    case("senet_one_normal_insufficient",9762295,"HAND",ACTIVATE,[add(89631139,sequence=1),add(45231177,"EXTRA")],target=False)
    case("senet_three_normals_and_deck_target",9762295,"HAND",ACTIVATE,[add(89631139,sequence=1),add(89631139,sequence=2),add(89631139,sequence=3),add(84512726,"DECK")])
    case("moon_hand_summon_available",75934716,"HAND",IGNITION,[add(89631139,sequence=1)])
    case("moon_cannot_pay_with_itself",75934716,"HAND",IGNITION,[],cost=False)
    case("sphinx_field_effect_available",47095439,"MZONE",QUICK,[add(89631139,"MZONE",sequence=1),add(89631139,"MZONE",player=1)])
    case("sphinx_no_opponent_to_set",47095439,"MZONE",QUICK,[add(89631139,"MZONE",sequence=1)],target=False)
    case("adjat_field_effect_available",73490148,"MZONE",QUICK,[add(89631139,"MZONE",sequence=1),add(89631139,"MZONE",player=1)])
    case("adjat_no_normal_to_pay",73490148,"MZONE",QUICK,[add(89631139,"MZONE",player=1)],cost=False)
    return rows

def main():
    global OUT
    parser=argparse.ArgumentParser()
    parser.add_argument("--profile",choices=("candidate","formal"),default="candidate")
    parser.add_argument("--runtime",type=Path)
    parser.add_argument("--out",type=Path)
    args=parser.parse_args()
    runtime=RUNTIME if args.profile=="candidate" else ROOT/"YGOProUnity_V2/output/Windows"
    if args.runtime: runtime=args.runtime.resolve()
    if args.out: OUT=args.out.resolve()
    OUT.mkdir(parents=True,exist_ok=True)
    native.RUNTIME=runtime
    core,dll,sources,scripts,buffers,errors,requested=native.initialize(installed=True)
    core.preload_script.argtypes=[C.c_void_p,C.c_char_p]
    core.preload_script.restype=C.c_int
    results=[]
    for name,code,body in cases():
        print("CHECK: "+name,flush=True)
        filename="maintenance_activation_"+name+".lua"
        scripts[filename]=body.encode("utf-8")
        buffers[filename]=C.create_string_buffer(scripts[filename])
        before=len(errors)
        duel=core.create_duel(12345)
        if not duel: raise RuntimeError("Core did not create duel")
        try: loaded=core.preload_script(duel,filename.encode("utf-8"))
        finally: core.end_duel(duel)
        results.append({"case":name,"code":code,"loaded":loaded,"errors":errors[before:]})
    failed=any(not row["loaded"] or row["errors"] for row in results)
    codes=sorted(set(row["code"] for row in results))
    result={"status":"failed" if failed else "passed","profile":args.profile,"runtime":str(runtime),
        "coverage":"registered effects evaluated with native IsActivatable on positive and negative fields; no full chain resolution or GUI event timing",
        "core_sha256":hashlib.sha256(dll.read_bytes()).hexdigest(),
        "client_sha256":hashlib.sha256((runtime/"MeiheweitePro_Data/Managed/Assembly-CSharp.dll").read_bytes()).hexdigest(),
        "archive_sha256":hashlib.sha256((runtime/"data/script.zip").read_bytes()).hexdigest(),
        "database_sha256":hashlib.sha256((runtime/"cdb/cards.cdb").read_bytes()).hexdigest(),
        "scripts":{str(code):hashlib.sha256(scripts['c'+str(code)+'.lua']).hexdigest() if 'c'+str(code)+'.lua' in scripts else "missing" for code in codes},"cases":results}
    (OUT/('card-activation-'+args.profile+'.json')).write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding="utf-8")
    print(json.dumps({"status":result["status"],"profile":args.profile,"cases":len(results),"failed":[x for x in results if not x['loaded'] or x['errors']]},ensure_ascii=False))
    return int(failed)

if __name__=="__main__": raise SystemExit(main())
