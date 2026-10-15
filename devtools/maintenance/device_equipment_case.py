"""Run the versioned equipment puzzle on the hash-verified maintenance APK."""
import argparse
import hashlib
import json
import re
from pathlib import Path
import subprocess
import sys
import time
import device_gameplay_case as device

def main():
    p=argparse.ArgumentParser()
    p.add_argument('--out',type=Path,required=True)
    p.add_argument('--serial',required=True)
    p.add_argument('--name',default='equipment-device')
    args=p.parse_args()
    if not args.name.replace('-','').isalnum(): p.error('invalid evidence name')
    out=args.out.resolve(); folder=out/args.name; folder.mkdir(exist_ok=True)
    if (folder/'result.json').exists(): raise RuntimeError('evidence exists; use a new --name')
    device.SERIAL=args.serial
    adb=device.adb; remote=device.REMOTE
    info=json.loads((out/'apk-result.json').read_text())
    script=Path(__file__).parents[1]/'tests/equipment_visual.lua'
    name='maintenance_equipment_20261014'
    flag=remote+'/log/qt_debug.on'; puzzle=remote+'/puzzle/'+name+'.lua'
    result={'status':'failed','apk_sha256':info['sha256'],'fixture_sha256':hashlib.sha256(script.read_bytes()).hexdigest(),
            'coverage':'scripted actual equipment puzzle and screenshot; visual assertion requires review'}
    owned=[]; pid=''
    def push_text(path,value):
        local=folder/Path(path).name; local.write_text(value,encoding='utf-8'); adb('push',str(local),path)
    def trace(): return adb('shell','cat',remote+'/log/qt_'+pid+'.log',required=False)
    def exists(path):
        check=subprocess.run([device.ADB,'-s',args.serial,'shell','test','-e',path],capture_output=True,timeout=30)
        if check.returncode not in (0,1): raise RuntimeError('device path check failed')
        return check.returncode==0
    def wait(predicate,label):
        until=time.monotonic()+90
        while time.monotonic()<until:
            content=trace()
            if '[err]' in content: raise RuntimeError('runtime exception: '+label)
            if predicate(content): return
            time.sleep(1)
        raise RuntimeError('timeout: '+label)
    try:
        apk=adb('shell','pm','path',device.PACKAGE).strip().removeprefix('package:')
        if adb('shell','sha256sum',apk).split()[0]!=info['sha256']: raise RuntimeError('installed APK mismatch')
        for path in [flag,puzzle,remote+'/commamd.shell']:
            if exists(path): raise RuntimeError('existing fixture/flag/command: '+path)
        adb('shell','am','force-stop',device.PACKAGE)
        adb('shell','mkdir','-p',remote+'/puzzle',remote+'/log')
        adb('push',str(script),puzzle); owned.append(puzzle)
        push_text(flag,'600'); owned.append(flag)
        adb('shell','monkey','-p',device.PACKAGE,'1')
        time.sleep(3); pid=adb('shell','pidof',device.PACKAGE).strip()
        if not pid.isdecimal(): raise RuntimeError('maintenance process not running')
        wait(lambda s:'[rd] ui mode=' in s,'menu')
        command=remote+'/commamd.shell'
        push_text(command,'mode OCG'); owned.append(command)
        wait(lambda s:'mode=OCG' in s and not exists(command),'OCG mode')
        push_text(command,'puzzle '+name)
        wait(lambda s:not exists(command),'puzzle command')
        wait(lambda s:re.search(r'\[sd\] cond=duel[^\n]*showed=True', s),'actual puzzle scene')
        time.sleep(12)
        runtime_log=adb('logcat','-d','--pid='+pid,'-v','threadtime')
        (folder/'unity-logcat.txt').write_text(runtime_log,encoding='utf-8')
        if re.search(r'(?:Exception:|FATAL EXCEPTION|Fatal signal)',runtime_log): raise RuntimeError('exception in maintenance process logcat')
        subprocess.run([sys.executable,str(Path(__file__).with_name('device_touch_capture.py')),
            '--adb',device.ADB,'--serial',args.serial,'--out',str(folder),'--name','equipment-scene','capture'],check=True)
        adb('pull',remote+'/data/script.zip',str(folder/'device-script.zip'))
        result.update(status='captured',pid=pid)
        return 0
    except Exception as error: result['error']=str(error); print(error); return 1
    finally:
        if pid: (folder/'trace.log').write_text(trace(),encoding='utf-8')
        for path in owned: adb('shell','rm','-f',path,required=False)
        (folder/'result.json').write_text(json.dumps(result,indent=2),encoding='utf-8')

if __name__=='__main__': raise SystemExit(main())
