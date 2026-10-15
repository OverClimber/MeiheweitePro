"""Load the real legacy equipment prefab and verify a rendered link in isolated Unity."""
import argparse
import hashlib
import json
from pathlib import Path
import shutil
import subprocess

ROOT=Path(__file__).resolve().parents[3]
ASSETS=ROOT/'YGOProUnity_V2/Assets'

def main():
    p=argparse.ArgumentParser()
    p.add_argument('--out',type=Path,required=True)
    p.add_argument('--unity',required=True)
    p.add_argument('--lightning-source',type=Path)
    a=p.parse_args(); out=a.out.resolve(); project=out/'unity'
    fixture=project/'Assets/Fixture'; fixture.mkdir(parents=True,exist_ok=True)
    (project/'Assets/Editor').mkdir(exist_ok=True)
    (project/'Packages').mkdir(exist_ok=True)
    (project/'Packages/manifest.json').write_text(json.dumps({'dependencies':{
        'com.unity.modules.particlesystem':'1.0.0','com.unity.modules.imageconversion':'1.0.0'}}))
    folder=ASSETS/'old/UiverseAssests/art_plugin/thunder'
    sources=[folder/'RAYOS/Sources/Artwork/Lightning Mat.mat',folder/'RAYOS/Sources/Artwork/Lightning.psd',folder/'ocgcore/mod_ocgcore_lighting.prefab',folder/'ocgcore/thunder_locator.cs',
             folder/'RAYOS/Sources/LightningBolt.cs',ASSETS/'Plugins/Perlin.cs']
    hashes={}
    for source in sources:
        actual=a.lightning_source if source.name=='LightningBolt.cs' and a.lightning_source else source
        shutil.copy2(actual,fixture/source.name)
        shutil.copy2(str(source)+'.meta',str(fixture/source.name)+'.meta')
        hashes[str(source.relative_to(ASSETS))]=hashlib.sha256(actual.read_bytes()).hexdigest()
    status=out/'status.txt'; status.unlink(missing_ok=True)
    test=(ROOT/'YGOProUnity_V2/devtools/tests/EquipmentUnityTests.cs').read_text(encoding='utf-8')
    test=test.replace('@RESULT@',str(status)).replace('@IMAGE@',str(out/'equipment-line.png'))
    (project/'Assets/Editor/EquipmentUnityTests.cs').write_text(test,encoding='utf-8')
    result={'status':'failed','sources':hashes,'coverage':'original prefab, particle endpoints, real camera render; no gameplay UI assertion'}
    try:
        process=subprocess.run([a.unity,'-batchmode','-projectPath',str(project),'-logFile',str(out/'unity.log'),
                '-executeMethod','EquipmentUnityTests.Run'],timeout=240,creationflags=subprocess.CREATE_NO_WINDOW)
        result['exit_code']=process.returncode
        result['assertions']=status.read_text(encoding='utf-8') if status.exists() else 'missing result'
        if process.returncode or not result['assertions'].startswith('passed'): raise RuntimeError(result['assertions'])
        result['status']='passed'; return 0
    except Exception as error: result['error']=str(error); return 1
    finally:
        (out/'result.json').write_text(json.dumps(result,indent=2),encoding='utf-8'); print(json.dumps(result))

if __name__=='__main__': raise SystemExit(main())
