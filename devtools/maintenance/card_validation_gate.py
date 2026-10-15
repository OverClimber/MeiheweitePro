"""Require current-source and candidate identities before actual card-core checks."""
from pathlib import Path
import datetime
import hashlib
import json
import subprocess
import sys
from native_script_smoke import ROOT, OUT, RUNTIME

def sha(path):
    with path.open("rb") as stream: return hashlib.file_digest(stream,"sha256").hexdigest()

def main():
    build=OUT.parent/"implementation_20261011"
    artifact=OUT/"card-validation"
    artifact.mkdir(exist_ok=True)
    result={"status":"failed","started_utc":datetime.datetime.now(datetime.timezone.utc).isoformat(),
        "coverage":"script initialization, native activation eligibility and helper preservation; no complete chain or GUI coverage","checks":[]}
    try:
        manifest=json.loads((build/"result.json").read_text(encoding="utf-8"))
        current=sha(RUNTIME/"MeiheweitePro_Data/Managed/Assembly-CSharp.dll")
        if manifest["status"]!="passed" or current!=manifest["dll_sha256"] or current!=sha(build/"Assembly-CSharp.dll"):
            raise RuntimeError("Independent candidate differs from the gated desktop build")
        sources=json.loads((build/"desktop-source-sha256.json").read_text(encoding="utf-8"))
        drift=[name for name,digest in sources.items() if sha(ROOT/"YGOProUnity_V2"/name)!=digest]
        if drift: raise RuntimeError("Desktop source changed after compilation: "+", ".join(drift))
        result["client_sha256"]=current
        result["core_sha256"]=sha(RUNTIME/"MeiheweitePro_Data/Plugins/x86_64/ocgcore.dll")
        for name,args in (("native_script_smoke.py",["--installed"]),
                          ("native_script_activation.py",[]),
                          ("native_script_conditions.py",[]),
                          ("verify_script_compatibility.py",[])):
            command=[sys.executable,str(Path(__file__).parent/name),*args]
            completed=subprocess.run(command,capture_output=True,encoding="utf-8",errors="replace",timeout=120)
            (artifact/(name+".log")).write_text(completed.stdout+completed.stderr,encoding="utf-8")
            result["checks"].append({"tool":name,"exit_code":completed.returncode})
            if completed.returncode: raise RuntimeError("Card validation failed: "+name)
            print("PASS: "+name,flush=True)
        result["status"]="passed"
        return 0
    except Exception as error:
        result["error"]=str(error)
        print(str(error),file=sys.stderr)
        return 1
    finally:
        (OUT/"card-validation-result.json").write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding="utf-8")

if __name__=="__main__": raise SystemExit(main())
