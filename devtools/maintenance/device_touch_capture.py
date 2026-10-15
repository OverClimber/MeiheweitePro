"""Record foreground-checked ADB touch actions and screenshots for acceptance."""
import argparse
import datetime
import json
from pathlib import Path
import subprocess
import time


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--adb', required=True)
    parser.add_argument('--serial', required=True)
    parser.add_argument('--out', type=Path, required=True)
    parser.add_argument('--name', required=True)
    parser.add_argument('action', choices=['capture', 'tap', 'swipe'])
    parser.add_argument('coordinates', type=int, nargs='*')
    args = parser.parse_args()
    if not args.name.replace('-', '').replace('_', '').isalnum():
        parser.error('name must be alphanumeric with optional hyphens/underscores')
    required = {'capture': 0, 'tap': 2, 'swipe': 5}[args.action]
    if len(args.coordinates) != required:
        parser.error('incorrect coordinate count; swipe requires x1 y1 x2 y2 duration_ms')
    args.out.mkdir(parents=True, exist_ok=True)
    target = args.out / args.name
    if target.with_suffix('.json').exists() or target.with_suffix('.png').exists():
        parser.error('evidence already exists; choose a new name')
    base = [args.adb, '-s', args.serial]
    def adb(*parts):
        return subprocess.run(base + list(parts), check=True, capture_output=True, timeout=90).stdout
    result = {'status': 'failed', 'serial': args.serial, 'action': args.action,
              'coordinates': args.coordinates, 'time': datetime.datetime.now().astimezone().isoformat(),
              'coverage': 'ADB injected pointer input and screenshot; not physical finger experience'}
    try:
        window = adb('shell', 'dumpsys', 'window').decode('utf-8', 'replace')
        focus = [line.strip() for line in window.splitlines() if 'mCurrentFocus=' in line]
        result['focus_before'] = focus
        package = 'com.meiheweite.pro.android.maintenance'
        if not any(package + '/' in line for line in focus):
            raise RuntimeError('maintenance app is not foreground; refusing touch/capture')
        if args.action != 'capture':
            adb('shell', 'input', args.action, *map(str, args.coordinates))
            time.sleep(1)
        png = adb('exec-out', 'screencap', '-p')
        if not png.startswith(b'\x89PNG\r\n\x1a\n'):
            raise RuntimeError('invalid screenshot')
        target.with_suffix('.png').write_bytes(png)
        result['status'] = 'captured'
        print(str(target.with_suffix('.png').resolve()))
        return 0
    except Exception as error:
        result['error'] = str(error)
        print('FAIL: ' + str(error))
        return 1
    finally:
        target.with_suffix('.json').write_text(json.dumps(result, ensure_ascii=False, indent=2), encoding='utf-8')


if __name__ == '__main__':
    raise SystemExit(main())
