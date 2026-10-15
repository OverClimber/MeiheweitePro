"""Verify candidate OCG/RD deck loading/slot layout in the existing isolated player.

Keeps the candidate in that test copy for manual drag testing. No production writes.
"""
from pathlib import Path
import argparse
import hashlib
import json
import re
import shutil
import subprocess
import time

ROOT = Path(__file__).resolve().parents[3]
OUT = ROOT / '_takeover_audit/drag_20261012'
RUNTIME = ROOT / '_takeover_audit/implementation_20261011/runtime'


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def main():
    global OUT, RUNTIME
    parser = argparse.ArgumentParser()
    parser.add_argument('--out', type=Path, default=OUT)
    parser.add_argument('--runtime', type=Path, default=RUNTIME)
    args = parser.parse_args()
    OUT, RUNTIME = args.out.resolve(), args.runtime.resolve()
    dll = OUT / 'Assembly-CSharp.dll'
    target = RUNTIME / 'MeiheweitePro_Data/Managed/Assembly-CSharp.dll'
    result = {'status': 'failed', 'dll_sha256': sha(dll), 'cases': {},
              'coverage': 'actual player loading and timed slot/order checks; no physical mouse/touch actions'}
    try:
        for name in ('result.json', 'drag-unity-result.json'):
            gate = json.loads((OUT / name).read_text())
            if gate['status'] != 'passed' or gate['dll_sha256'] != sha(dll):
                raise RuntimeError('Candidate gate failed/mismatch: ' + name)
        source_hashes = json.loads((OUT / 'desktop-source-sha256.json').read_text())
        for name, digest in source_hashes.items():
            if sha(ROOT / 'YGOProUnity_V2' / name) != digest:
                raise RuntimeError('Source changed after compilation: ' + name)
        check = subprocess.run(['powershell', '-NoProfile', '-Command',
            'if (Get-Process MeiheweitePro -ErrorAction SilentlyContinue) { exit 1 }'], capture_output=True)
        if check.returncode:
            raise RuntimeError('Close the existing player before replacing the isolated candidate')
        backup = OUT / 'baseline/isolated-player.dll'
        backup.parent.mkdir(parents=True, exist_ok=True)
        if not backup.exists():
            shutil.copy2(target, backup)
        shutil.copy2(dll, target)
        for mode, deck in [('OCG', '梅赫维特'), ('RD', 'RdAiE2E')]:
            flag = RUNTIME / 'log/qt_debug.on'
            command = RUNTIME / 'commamd.shell'
            if flag.exists() or command.exists():
                raise RuntimeError('Existing debug flag or pending command; refusing to overwrite')
            flag.write_text('180', encoding='utf-8')
            info = subprocess.STARTUPINFO()
            info.dwFlags |= subprocess.STARTF_USESHOWWINDOW
            info.wShowWindow = 0
            process = None
            trace = None
            try:
                process = subprocess.Popen([str(RUNTIME / 'MeiheweitePro.exe')], cwd=RUNTIME,
                    startupinfo=info, stdin=subprocess.DEVNULL, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
                trace = RUNTIME / 'log' / ('qt_%d.log' % process.pid)

                def wait(predicate, label):
                    end = time.monotonic() + 60
                    while time.monotonic() < end:
                        text = trace.read_text(encoding='utf-8', errors='replace') if trace.exists() else ''
                        if process.poll() is not None or '[err]' in text or re.search(r'\[slot\].* err ', text):
                            raise RuntimeError('Player failure: ' + label)
                        if predicate(text):
                            return text
                        time.sleep(.25)
                    raise RuntimeError('Timeout: ' + label)

                command.write_text('mode ' + mode, encoding='utf-8')
                wait(lambda text: not command.exists() and 'mode=' + mode in text, 'mode')
                command.write_text('edit ' + deck, encoding='utf-8')
                text = wait(lambda text: '[slotgeo] t8 S ' in text, 'deck slot settling')
                summaries = re.findall(r'\[slot\] (t[38]).*main=(\d+)/(\d+) extra=(\d+)/(\d+) side=(\d+)/(\d+) orderDiffVsYdk=(\d+)', text)
                if len(summaries) != 2 or any(a != b or c != d or e != f or diff != '0' for _, a, b, c, d, e, f, diff in summaries):
                    raise RuntimeError('Deck membership/order mismatch')
                offsets = re.findall(r'\[slotgeo\] t[38] [MES] count=\d+ maxDxz=([\d.]+)', text)
                if len(offsets) != 6 or any(float(x) > .001 for x in offsets):
                    raise RuntimeError('Slot landing drift')
                result['cases'][mode] = {'status': 'passed', 'deck': deck, 'samples': summaries, 'max_offsets': offsets}
                print('PASS ' + mode + ' actual editor slot/order checks at 3s and 8s', flush=True)
            finally:
                if process is not None and process.poll() is None:
                    process.terminate()
                    process.wait(timeout=15)
                if trace is not None and trace.exists():
                    shutil.copy2(trace, OUT / ('editor-' + mode.lower() + '-trace.log'))
                flag.unlink(missing_ok=True)
                command.unlink(missing_ok=True)
        result['status'] = 'passed'
        return 0
    except Exception as error:
        result['error'] = str(error)
        print(error)
        return 1
    finally:
        (OUT / 'editor-runtime-result.json').write_text(json.dumps(result, ensure_ascii=False, indent=2), encoding='utf-8')


if __name__ == '__main__':
    raise SystemExit(main())
