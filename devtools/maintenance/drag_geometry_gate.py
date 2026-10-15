"""Run actual candidate drag geometry and list mutation in an isolated Unity editor."""
from pathlib import Path
import argparse
import hashlib
import json
import os
import subprocess

ROOT = Path(__file__).resolve().parents[3]
OUT = ROOT / '_takeover_audit/drag_20261012'


def main():
    global OUT
    parser = argparse.ArgumentParser()
    parser.add_argument('--out', type=Path, default=OUT)
    OUT = parser.parse_args().out.resolve()
    project = OUT / 'unity-validation'
    (project / 'Assets/Editor').mkdir(parents=True, exist_ok=True)
    (project / 'Packages').mkdir(exist_ok=True)
    (project / 'Packages/manifest.json').write_text('{"dependencies":{}}')
    source = (ROOT / 'YGOProUnity_V2/devtools/tests/DeckDragUnityTests.cs').read_text(encoding='utf-8')
    dll = OUT / 'Assembly-CSharp.dll'
    result = OUT / 'drag-status.txt'
    result.unlink(missing_ok=True)
    managed = ROOT / '_takeover_audit/implementation_20261011/runtime/MeiheweitePro_Data/Managed'
    for key, value in {'MANAGED': managed, 'DLL': dll, 'RESULT': result}.items():
        source = source.replace('@' + key + '@', str(value))
    (project / 'Assets/Editor/DeckDragUnityTests.cs').write_text(source, encoding='utf-8')
    editor = os.environ.get('MEI_DRAG_EDITOR', 'D:/Unity/2021.3.45f1/Editor/Unity.exe')
    manifest = {'status': 'failed', 'dll_sha256': hashlib.sha256(dll.read_bytes()).hexdigest(),
                'coverage': 'real Unity camera/math and production list mutation; not manual mouse/touch validation'}
    try:
        process = subprocess.Popen([editor, '-batchmode', '-nographics', '-projectPath', str(project),
            '-logFile', str(OUT / 'drag-unity.log'), '-executeMethod', 'DeckDragUnityTests.Run'],
            stdin=subprocess.DEVNULL, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL,
            creationflags=subprocess.CREATE_NO_WINDOW)
        print('Drag validation Unity PID=' + str(process.pid), flush=True)
        try:
            code = process.wait(timeout=300)
        except subprocess.TimeoutExpired:
            process.kill()
            process.wait()
            raise RuntimeError('Unity drag validation timed out')
        manifest['exit_code'] = code
        if code != 0 or not result.is_file() or not result.read_text().startswith('passed assertions='):
            raise RuntimeError('Unity drag validation failed; see drag-unity.log')
        manifest.update(status='passed', assertions=result.read_text())
        print(result.read_text())
        return 0
    except Exception as error:
        manifest['error'] = str(error)
        print(error)
        return 1
    finally:
        (OUT / 'drag-unity-result.json').write_text(json.dumps(manifest, indent=2), encoding='utf-8')


if __name__ == '__main__':
    raise SystemExit(main())
