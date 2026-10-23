"""Apply only exact hash-matched export changes inside a fresh Xcode workcopy."""
import hashlib,json,sys,zipfile
from pathlib import Path,PurePosixPath
archive=Path(sys.argv[1]); root=Path(sys.argv[2]).resolve()
def digest(data):return hashlib.sha256(data).hexdigest()
def target(name):
    p=PurePosixPath(name)
    if p.is_absolute() or '..' in p.parts or '\\' in name or ':' in name:
        raise ValueError('unsafe delta path')
    return root/name
with zipfile.ZipFile(archive) as z:
    manifest=json.loads(z.read('delta-manifest.json'))
    if manifest['removed']:raise ValueError('export removals require review')
    contents={}
    for name,hashes in manifest['files'].items():
        path=target(name)
        before=digest(path.read_bytes()) if path.exists() else None
        if before!=hashes['before']:raise ValueError('export baseline mismatch: '+name)
        data=z.read(name)
        if digest(data)!=hashes['after']:raise ValueError('export delta mismatch: '+name)
        contents[name]=data
    for name,data in contents.items():
        path=target(name)
        path.parent.mkdir(parents=True,exist_ok=True)
        path.write_bytes(data)
print('Applied exact export delta:',len(contents),'files')
