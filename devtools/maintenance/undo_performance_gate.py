"""Compile current replay methods against a deterministic protocol/clock fixture.

No Unity/native gameplay claims: verifies long-prefix boundaries and room timing.
The extracted method hashes bind the tests to current production sources.
"""
from pathlib import Path
import hashlib
import importlib.util
import json
import sys

ROOT = Path(__file__).resolve().parents[3]
OUT = ROOT / '_takeover_audit/undo_performance_20261011'
SRC = ROOT / 'YGOProUnity_V2/Assets/SibylSystem'


def method(text, signature):
    start = text.index(signature)
    # End on the next class-level method closing brace (all selected methods use
    # the project's four-space indentation). Fail closed if a method moves.
    end = text.index('\n    }', start) + len('\n    }')
    return text[start:end]


def main():
    OUT.mkdir(parents=True, exist_ok=True)
    result = {'status': 'failed', 'scope': 'production replay/room methods; simulated protocol and clock'}
    try:
        fixture = (ROOT / 'YGOProUnity_V2/devtools/tests/UndoPerformanceTests.cs').read_text(encoding='utf-8')
        selected = {}
        groups = {
            'UNDO': ('Room/DuelUndo.cs', ['public static bool SwallowInbound(Package p)',
                     'private static void EndCatchUp()', 'public static DuelTimeline.Decision Tick()']),
            'OCG': ('Ocgcore/Ocgcore.cs', ['public void addPackage(Package p)', 'void undoReplayTick()']),
            'ROOM': ('Room/Room.cs', ['private System.Collections.IEnumerator quickStartFlow()']),
            'TCP': ('MonoHelpers/TcpHelper.cs', ['public static void preFrameFunction()']),
        }
        for marker, (name, signatures) in groups.items():
            source = (SRC / name).read_text(encoding='utf-8-sig')
            extracted = '\n'.join(method(source, signature) for signature in signatures)
            selected[name] = hashlib.sha256(extracted.encode()).hexdigest()
            fixture = fixture.replace('// INSERT_' + marker, extracted)
        generated = OUT / 'UndoPerformanceFixture.cs'
        generated.write_text(fixture, encoding='utf-8')
        spec = importlib.util.spec_from_file_location('gate', Path(__file__).with_name('run.py'))
        gate = importlib.util.module_from_spec(spec)
        spec.loader.exec_module(gate)
        build = gate.load('build', ROOT / '_csc_build.py')
        editor = Path(build.EDITOR_DATA)
        compiler = editor / 'DotNetSdkRoslyn/csc.dll'
        dotnet = editor / 'NetCoreRuntime/dotnet.exe'
        exe = OUT / 'UndoPerformanceTests.exe'
        refs = [Path(build.PLAYER_MANAGED) / name for name in ('mscorlib.dll', 'System.dll', 'System.Core.dll')]
        rsp = OUT / 'undo-tests.rsp'
        rsp.write_text('\n'.join(['-target:exe', '-nostdlib+', '-langversion:9.0', '-out:"%s"' % exe]
            + ['-r:"%s"' % p for p in refs] + ['"%s"' % generated]), encoding='utf-8')
        log = gate.run([dotnet, compiler, '@' + str(rsp)])
        log += gate.run([exe])
        (OUT / 'undo-tests.log').write_text(log, encoding='utf-8')
        result.update(status='passed', methods=selected)
        print(log)
    except Exception as error:
        result['error'] = str(error)
        print(error, file=sys.stderr)
        return 1
    finally:
        (OUT / 'undo-tests.json').write_text(json.dumps(result, ensure_ascii=False, indent=2), encoding='utf-8')
    return 0


if __name__ == '__main__':
    sys.exit(main())
