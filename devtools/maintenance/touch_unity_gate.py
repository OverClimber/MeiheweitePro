"""Exercise Android hit assistance and patched NGUI in isolated real Unity."""
from pathlib import Path
import argparse
import hashlib
import json
import os
import subprocess

ROOT = Path(__file__).resolve().parents[3]


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--out', type=Path, required=True)
    out = parser.parse_args().out.resolve()
    project = out / 'touch-unity'
    (project / 'Assets/Editor').mkdir(parents=True, exist_ok=True)
    (project / 'Packages').mkdir(exist_ok=True)
    (project / 'Packages/manifest.json').write_text('{"dependencies":{}}')
    source = (ROOT / 'YGOProUnity_V2/devtools/tests/TouchUnityTests.cs').read_text(encoding='utf-8')
    dll = out / 'android-compile/Assembly-CSharp.dll'
    status = out / 'touch-unity-status.txt'
    status.unlink(missing_ok=True)
    for key, value in {'MANAGED': out / 'android-stage/Library/ScriptAssemblies', 'DLL': dll, 'RESULT': status}.items():
        source = source.replace('@'+key+'@', str(value))
    (project / 'Assets/Editor/TouchUnityTests.cs').write_text(source, encoding='utf-8')
    result = {'status': 'failed', 'dll_sha256': hashlib.sha256(dll.read_bytes()).hexdigest(),
              'scope': 'real Unity Android candidate hit geometry and NGUI dispatch; no physical touchscreen'}
    try:
        editor = os.environ.get('MEI_TOUCH_EDITOR', 'D:/Unity/2021.3.45f1/Editor/Unity.exe')
        process = subprocess.Popen([editor, '-batchmode', '-nographics', '-projectPath', str(project),
            '-logFile', str(out/'touch-unity.log'), '-executeMethod', 'TouchUnityTests.Run'],
            stdin=subprocess.DEVNULL, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL,
            creationflags=subprocess.CREATE_NO_WINDOW)
        print('Touch Unity PID='+str(process.pid), flush=True)
        try:
            code = process.wait(timeout=180)
        except subprocess.TimeoutExpired:
            process.kill(); process.wait()
            raise RuntimeError('Unity touch validation timed out')
        result['exit_code'] = code
        if code or not status.exists() or not status.read_text().startswith('passed assertions='):
            raise RuntimeError('Unity touch validation failed; inspect touch-unity.log/status')
        result.update(status='passed', assertions=status.read_text())
        print(status.read_text())
        return 0
    except Exception as e:
        result['error'] = str(e); print(e); return 1
    finally:
        (out / 'touch-unity-result.json').write_text(json.dumps(result,indent=2),encoding='utf-8')


if __name__ == '__main__':
    raise SystemExit(main())
