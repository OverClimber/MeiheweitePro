"""Run equipment fixtures read-only on the actual installed core and scripts."""
import argparse
import ctypes as C
import hashlib
import json
from pathlib import Path
import native_script_smoke as native


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--runtime', type=Path, required=True)
    parser.add_argument('--out', type=Path, required=True)
    parser.add_argument('--fixture', type=Path, required=True)
    parser.add_argument('--compatibility', type=Path)
    parser.add_argument('--process', action='store_true', help='run actual duel processors; fixture must emit EQUIP_PROBE DONE')
    parser.add_argument('--select-place', type=int, choices=range(5), help='answer only player 0 single monster-zone placement with this exact slot')
    parser.add_argument('--select-equip-place', type=int, choices=range(5), help='answer only player 0 single spell-zone placement with this exact slot')
    args = parser.parse_args()
    args.out.mkdir(parents=True, exist_ok=True)
    native.RUNTIME = args.runtime.resolve()
    core, dll, _, scripts, buffers, messages, requested = native.initialize(installed=True)
    # The standalone AI server prioritizes loose scripts over packed scripts.
    for path in (native.RUNTIME / 'script').glob('*.lua'):
        scripts[path.name] = path.read_bytes()
        buffers[path.name] = C.create_string_buffer(scripts[path.name])
    if args.compatibility:
        scripts['utility.lua'] += args.compatibility.read_bytes()
        buffers['utility.lua'] = C.create_string_buffer(scripts['utility.lua'])
    name = 'maintenance_equipment_fixture.lua'
    scripts[name] = args.fixture.read_bytes()
    if b'@COMPATIBILITY@' in scripts[name]:
        if not args.compatibility: raise RuntimeError('fixture requires --compatibility')
        scripts[name] = scripts[name].replace(b'@COMPATIBILITY@', args.compatibility.read_bytes())
    buffers[name] = C.create_string_buffer(scripts[name])
    core.preload_script.argtypes = [C.c_void_p, C.c_char_p]
    core.preload_script.restype = C.c_int
    duel = core.create_duel(12345)
    if not duel:
        raise RuntimeError('core failed to create duel')
    try:
        loaded = core.preload_script(duel, name.encode())
        if loaded and args.process:
            core.start_duel.argtypes = [C.c_void_p, C.c_int]
            core.process.argtypes = [C.c_void_p]
            core.process.restype = C.c_int
            core.get_message.argtypes = [C.c_void_p, C.c_void_p]
            core.start_duel(duel, 0)
            packets = bytearray()
            for _ in range(100):
                value = core.process(duel)
                packet = C.create_string_buffer(65536)
                length = core.get_message(duel, packet)
                if length > 0: packets.extend(packet.raw[:length])
                if any('EQUIP_PROBE DONE' in s or not s.startswith('EQUIP_PROBE ') for s in messages): break
                if value & 0x10000000:
                    tail = packet.raw[:length][-7:]
                    if (args.select_place is not None and len(tail)==7 and tail[:3]==bytes([18,0,1])
                            and not int.from_bytes(tail[3:], 'little') & (1 << args.select_place)):
                        core.set_responseb.argtypes = [C.c_void_p, C.c_void_p]
                        response = C.create_string_buffer(bytes([0,4,args.select_place]),64)
                        core.set_responseb(duel,response)
                        continue
                    if (args.select_equip_place is not None and len(tail)==7 and tail[:3]==bytes([18,0,1])
                            and not int.from_bytes(tail[3:], 'little') & (1 << (8+args.select_equip_place))):
                        core.set_responseb.argtypes = [C.c_void_p, C.c_void_p]
                        response = C.create_string_buffer(bytes([0,8,args.select_equip_place]),64)
                        core.set_responseb(duel,response)
                        continue
                    break
            (args.out / 'messages.bin').write_bytes(packets)
            if not any(s == 'EQUIP_PROBE DONE' for s in messages):
                messages.append('fixture did not complete its runtime assertions')
    finally:
        core.end_duel(duel)
    diagnostics = [s for s in messages if s.startswith('EQUIP_PROBE ')]
    errors = [s for s in messages if not s.startswith('EQUIP_PROBE ')]
    result = {'status': 'passed' if loaded and not errors else 'failed', 'runtime': str(native.RUNTIME),
              'core_sha256': hashlib.sha256(dll.read_bytes()).hexdigest(),
              'fixture_sha256': hashlib.sha256(scripts[name]).hexdigest(),
              'utility_sha256': hashlib.sha256(scripts['utility.lua']).hexdigest(),
              'diagnostics': diagnostics, 'errors': errors, 'loaded': loaded,
              'coverage': 'native Lua fixture; no GUI rendering assertion'}
    (args.out / 'result.json').write_text(json.dumps(result, ensure_ascii=False, indent=2), encoding='utf-8')
    print(json.dumps(result, ensure_ascii=False))
    return int(result['status'] != 'passed')


if __name__ == '__main__':
    raise SystemExit(main())
