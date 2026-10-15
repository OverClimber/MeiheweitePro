"""Compare original Android decks/replays/configs without restoring or deleting user data."""
import argparse
import hashlib
import io
import json
from pathlib import Path
import subprocess
import tarfile

def main():
    p=argparse.ArgumentParser()
    p.add_argument('--adb',required=True); p.add_argument('--serial',required=True)
    p.add_argument('--baseline',type=Path,required=True); p.add_argument('--out',type=Path,required=True)
    a=p.parse_args(); a.out.mkdir(parents=True,exist_ok=True)
    result={'status':'passed','packages':{}}
    for name,old in json.loads(a.baseline.read_text(encoding='utf-8')).items():
        package=name.removesuffix('-user-data.tar')
        if package not in ('com.meiheweite.pro.android','com.meiheweite.pro.android.maintenance'):
            raise RuntimeError('unexpected baseline package')
        raw=subprocess.run([a.adb,'-s',a.serial,'exec-out','tar','-C','/sdcard/Android/data/'+package+'/files',
                            '-cf','-','config','deck','deck_rd','replay'],check=True,capture_output=True,timeout=120).stdout
        (a.out/name).write_bytes(raw)
        with tarfile.open(fileobj=io.BytesIO(raw)) as archive:
            current={m.name.removeprefix('./'):hashlib.sha256(archive.extractfile(m).read()).hexdigest()
                     for m in archive.getmembers() if m.isfile()}
        changed=[n for n,h in old['sha256'].items() if current.get(n)!=h]
        protected=[n for n in changed if package.endswith('.android') or not n.startswith('config/')]
        row={'original_files':len(old['sha256']),'original_changed_or_missing':changed,
             'protected_changed_or_missing':protected,'added':sorted(set(current)-set(old['sha256']))}
        if protected: result['status']='failed'
        result['packages'][package]=row
    (a.out/'result.json').write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf-8')
    print(json.dumps(result,ensure_ascii=False)); return int(result['status']!='passed')

if __name__=='__main__': raise SystemExit(main())
