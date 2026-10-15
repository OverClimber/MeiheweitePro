"""Check an isolated desktop player enters the versioned equipment puzzle."""
import argparse
import hashlib
import json
import re
from pathlib import Path
import shutil
import subprocess
import time

ROOT=Path(__file__).resolve().parents[3]

def main():
    p=argparse.ArgumentParser(); p.add_argument('--runtime',type=Path,required=True); p.add_argument('--out',type=Path,required=True)
    a=p.parse_args(); runtime=a.runtime.resolve(); out=a.out.resolve(); out.mkdir(parents=True,exist_ok=True)
    if (ROOT/'_takeover_audit').resolve() not in runtime.parents: raise RuntimeError('only isolated audit player allowed')
    source=Path(__file__).parents[1]/'tests/equipment_visual.lua'
    name='maintenance_equipment_20261014'; fixture=runtime/'puzzle'/(name+'.lua')
    flag=runtime/'log/qt_debug.on'; command=runtime/'commamd.shell'
    for path in (flag,fixture,command):
        if path.exists(): raise RuntimeError('existing test input: '+str(path))
    result={'status':'failed','dll_sha256':hashlib.sha256((runtime/'MeiheweitePro_Data/Managed/Assembly-CSharp.dll').read_bytes()).hexdigest(),
            'fixture_sha256':hashlib.sha256(source.read_bytes()).hexdigest(),
            'coverage':'actual desktop puzzle scene entry and runtime errors; no screenshot assertion'}
    process=None; trace=None
    try:
        fixture.parent.mkdir(exist_ok=True); shutil.copy2(source,fixture); flag.write_text('180')
        startup=subprocess.STARTUPINFO(); startup.dwFlags|=subprocess.STARTF_USESHOWWINDOW; startup.wShowWindow=0
        process=subprocess.Popen([str(runtime/'MeiheweitePro.exe'),'-s',name,'-logFile',str(out/'player.log')],cwd=runtime,
            startupinfo=startup,stdin=subprocess.DEVNULL,stdout=subprocess.DEVNULL,stderr=subprocess.DEVNULL)
        trace=runtime/'log'/('qt_'+str(process.pid)+'.log'); deadline=time.monotonic()+90
        while time.monotonic()<deadline:
            text=trace.read_text(encoding='utf-8',errors='replace') if trace.exists() else ''
            if process.poll() is not None or '[err]' in text: raise RuntimeError('puzzle startup failed')
            if re.search(r'\[sd\] cond=duel[^\n]*showed=True', text):
                time.sleep(3)
                if '[err]' in trace.read_text(encoding='utf-8',errors='replace'): raise RuntimeError('puzzle runtime error')
                player_log=(out/'player.log').read_text(encoding='utf-8',errors='replace')
                if re.search(r'(?:Exception:|\[err\])', player_log): raise RuntimeError('exception in player.log')
                result['status']='passed'; print('PASS: actual desktop equipment puzzle scene'); return 0
            time.sleep(.5)
        raise RuntimeError('puzzle scene timeout')
    except Exception as error: result['error']=str(error); print(error); return 1
    finally:
        if process is not None and process.poll() is None: process.terminate(); process.wait(timeout=15)
        if trace and trace.exists(): shutil.copy2(trace,out/'trace.log')
        for path in (flag,fixture,command): path.unlink(missing_ok=True)
        (out/'result.json').write_text(json.dumps(result,indent=2),encoding='utf-8')

if __name__=='__main__': raise SystemExit(main())
