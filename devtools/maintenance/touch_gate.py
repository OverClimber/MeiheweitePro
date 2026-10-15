"""Compile and replay the production Android gesture policy, without Unity stubs."""
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
    out.mkdir(parents=True, exist_ok=True)
    editor = Path(os.environ.get('MEI_ANDROID_EDITOR', 'D:/Unity/2021.3.45f1/Editor/Data'))
    sources = [ROOT / 'YGOProAndroid/layer/Assets/Android/TouchGestureState.cs',
               ROOT / 'YGOProUnity_V2/devtools/tests/TouchGestureTests.cs']
    result = {'status': 'failed', 'sources': {str(p.relative_to(ROOT)): hashlib.sha256(p.read_bytes()).hexdigest() for p in sources}}
    log = ''
    try:
        refs = editor / 'UnityReferenceAssemblies/unity-4.8-api'
        exe = out / 'TouchGestureTests.exe'
        rsp = out / 'touch-tests.rsp'
        rsp.write_text('\n'.join(['-target:exe', '-nostdlib+', '-langversion:9.0', '-out:"%s"' % exe]
            + ['-r:"%s"' % (refs / name) for name in ['mscorlib.dll', 'System.dll']]
            + ['"%s"' % p for p in sources]), encoding='utf-8')
        for args in [[editor / 'NetCoreRuntime/dotnet.exe', editor / 'DotNetSdkRoslyn/csc.dll', '@'+str(rsp)], [exe]]:
            proc = subprocess.run(args, capture_output=True, text=True, encoding='utf-8', errors='replace')
            log += proc.stdout + proc.stderr
            if proc.returncode: raise RuntimeError('touch gate exit=' + str(proc.returncode))
        result['status'] = 'passed'
        print(log)
        return 0
    except Exception as error:
        result['error'] = str(error)
        print(log, str(error))
        return 1
    finally:
        (out / 'touch-tests.log').write_text(log, encoding='utf-8')
        (out / 'touch-result.json').write_text(json.dumps(result, indent=2), encoding='utf-8')


if __name__ == '__main__':
    raise SystemExit(main())
