"""Promote only the verified client DLL and migrated script archive, with rollback."""
import argparse
import json
import os
from pathlib import Path
import shutil
import subprocess
import sys
import zipfile
import deploy_windows as deployment

ROOT=Path(__file__).resolve().parents[3]
TOOLS=Path(__file__).parent
FILES=['MeiheweitePro_Data/Managed/Assembly-CSharp.dll','data/script.zip']
sha=deployment.sha
read=deployment.read

def main():
    p=argparse.ArgumentParser()
    p.add_argument('--runtime',type=Path,required=True)
    p.add_argument('--candidate',type=Path,required=True)
    p.add_argument('--evidence',type=Path,required=True)
    p.add_argument('--out',type=Path,required=True)
    p.add_argument('--execute',action='store_true')
    p.add_argument('--rollback',action='store_true')
    a=p.parse_args(); runtime=a.runtime.resolve(); candidate=a.candidate.resolve(); evidence=a.evidence.resolve(); out=a.out.resolve()
    if runtime != (ROOT/"YGOProUnity_V2/output/Windows").resolve():
        raise RuntimeError("Deployment is restricted to YGOProUnity_V2/output/Windows")
    deployment.FORMAL=runtime
    deployment.idle(candidate)
    def target(name): return deployment.checked(runtime,name)
    def journal(value): deployment.write(out/'deployment.json',value)
    if a.rollback:
        saved=read(out/'deployment.json')
        if saved['runtime']!=str(runtime): raise RuntimeError('rollback runtime mismatch')
        for row in saved['files']:
            if sha(target(row['path'])) not in (row['before'],row['after']): raise RuntimeError('later runtime edit; refusing rollback')
            if sha(out/'baseline'/row['path'])!=row['before']: raise RuntimeError('backup mismatch')
        for row in reversed(saved['files']):
            temp=target(row['path']).with_name(Path(row['path']).name+'.equipment-rollback.tmp')
            if temp.exists(): raise RuntimeError('existing rollback staging file')
            shutil.copy2(out/'baseline'/row['path'],temp); os.replace(temp,target(row['path']))
        saved['status']='rolled_back'; journal(saved); return 0
    if (out/'deployment.json').exists(): raise RuntimeError('existing deployment journal')
    desktop=evidence/'desktop'; expected=sha(desktop/'Assembly-CSharp.dll')
    for name in ['result.json','drag-unity-result.json','editor-runtime-result.json']:
        gate=read(desktop/name)
        if gate['status']!='passed' or gate['dll_sha256']!=expected: raise RuntimeError('candidate gate mismatch: '+name)
    puzzle=read(evidence/'desktop-puzzle-final/result.json')
    if puzzle['status']!='passed' or puzzle['dll_sha256']!=expected: raise RuntimeError('actual puzzle gate mismatch')
    if puzzle['fixture_sha256']!=sha(ROOT/'YGOProUnity_V2/devtools/tests/equipment_visual.lua'): raise RuntimeError('puzzle fixture drift')
    if sha(candidate/FILES[0])!=expected: raise RuntimeError('independent player differs from verified DLL')
    for name,digest in read(desktop/'desktop-source-sha256.json').items():
        if sha(ROOT/'YGOProUnity_V2'/name)!=digest: raise RuntimeError('source drift: '+name)
    render=read(evidence/'render-fixed/result.json')
    if render['status']!='passed': raise RuntimeError('equipment render gate failed')
    for name,digest in render['sources'].items():
        if sha(ROOT/'YGOProUnity_V2/Assets'/name)!=digest: raise RuntimeError('render source drift')
    for name in ['sync-result.json','isolated-native/result.json','native-full/card-activation-candidate.json','native-full/native-all-loose-candidate.json']:
        if read(evidence/name)['status']!='passed': raise RuntimeError('required gate failed: '+name)
    dependencies=['MeiheweitePro.exe','UnityPlayer.dll','AI.Server.exe','MeiheweitePro_Data/Plugins/x86_64/ocgcore.dll','cdb/cards.cdb']
    dependencies += [p.relative_to(candidate).as_posix() for p in (candidate/'MeiheweitePro_Data/Managed').glob('*.dll') if p.name!='Assembly-CSharp.dll']
    for name in dependencies:
        if sha(target(name))!=sha(candidate/name): raise RuntimeError('runtime dependency mismatch: '+name)
    suffix=(desktop/'type-compatibility.lua').read_bytes()
    with zipfile.ZipFile(target(FILES[1])) as old,zipfile.ZipFile(candidate/FILES[1]) as new:
        if old.namelist()!=new.namelist(): raise RuntimeError('script archive membership changed')
        for name in old.namelist():
            if new.read(name)!=old.read(name)+(suffix if name=='script/utility.lua' else b''):
                raise RuntimeError('unexpected archive modification: '+name)
    print('Verified DLL, dependencies, render/native gates and exact utility-only archive migration',flush=True)
    if not a.execute: return 0
    out.mkdir(parents=True,exist_ok=True)
    before=deployment.inventory(set(FILES))
    deployment.write(out/'protected-files-before.json',before)
    record={'status':'prepared','runtime':str(runtime),'candidate':str(candidate),'files':[],
            'scope':'client DLL and utility compatibility only; user decks/replays/pictures excluded'}
    staged=[]
    try:
        for name in FILES:
            path=target(name); backup=out/'baseline'/name; backup.parent.mkdir(parents=True,exist_ok=True)
            shutil.copy2(path,backup)
            temp=path.with_name(path.name+'.equipment-staged.tmp')
            if temp.exists(): raise RuntimeError('existing staging file')
            shutil.copy2(candidate/name,temp); staged.append(temp)
            row={'path':name,'before':sha(path),'after':sha(candidate/name)}
            if sha(backup)!=row['before'] or sha(temp)!=row['after']: raise RuntimeError('staging/backup hash mismatch')
            record['files'].append(row)
        journal(record)
        for row,temp in zip(record['files'],staged):
            if sha(target(row['path']))!=row['before']: raise RuntimeError('runtime changed during staging')
            os.replace(temp,target(row['path']))
        tests=[('native_equip_gate.py',['--runtime',str(runtime),'--out',str(out/'native'),
                '--fixture',str(ROOT/'YGOProUnity_V2/devtools/tests/equipment_regression.lua'),'--process','--select-place','4','--select-equip-place','0']),
               ('native_script_smoke.py',['--installed','--all-loose','--runtime',str(runtime),'--out',str(out/'native-full')]),
               ('native_script_activation.py',['--runtime',str(runtime),'--out',str(out/'native-full')])]
        for tool,arguments in tests:
            proc=subprocess.run([sys.executable,str(TOOLS/tool),*arguments],capture_output=True,timeout=180)
            (out/(tool+'.log')).write_bytes(proc.stdout+proc.stderr)
            if proc.returncode: raise RuntimeError('installed regression failed: '+tool)
        if deployment.inventory(set(FILES))!=before: raise RuntimeError('protected runtime inventory changed')
        for row in record['files']:
            if sha(target(row['path']))!=row['after']: raise RuntimeError('installed hash mismatch')
        record.update(status='deployed',protected_files_verified=len(before)); journal(record)
        print('PASS: installed regressions and protected file inventory',flush=True); return 0
    except BaseException as error:
        record['error']=str(error)
        for row in reversed(record['files']):
            path=target(row['path'])
            if sha(path)==row['after']:
                temp=path.with_name(path.name+'.equipment-restore.tmp')
                if temp.exists(): raise RuntimeError('existing restore staging file') from error
                shutil.copy2(out/'baseline'/row['path'],temp); os.replace(temp,path)
        record['status']='rolled_back'; journal(record); raise
    finally:
        for temp in staged: temp.unlink(missing_ok=True)

if __name__=='__main__': raise SystemExit(main())
