"""Offline failure/ownership tests; never invokes a real Android device."""
import hashlib
import importlib.util
import json
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest
from unittest.mock import patch
import zipfile

TOOLS = Path(__file__).resolve().parents[1] / 'maintenance'

def load(name):
    spec = importlib.util.spec_from_file_location(name, TOOLS / (name + '.py'))
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module

class DeviceOwnershipTests(unittest.TestCase):
    def run_case(self, existing, installed='candidate'):
        module = load('device_gameplay_case')
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            (root / 'deck').mkdir()
            (root / 'deck/fixture.ydk').write_bytes(b'user deck')
            (root / 'apk-result.json').write_text(json.dumps({'sha256': 'candidate'}))
            module.RUNTIME = root
            remote = dict(existing)
            mutations = []
            def run(args, **kwargs):
                name = args[-1].removeprefix(module.REMOTE + '/')
                return subprocess.CompletedProcess(args, 0 if name in remote else 1)
            def adb(*args, **kwargs):
                if args[:3] == ('shell', 'pm', 'path'): return 'package:/test.apk'
                if args[:2] == ('shell', 'sha256sum'):
                    if args[2] == '/test.apk': return installed + ' /test.apk'
                    return hashlib.sha256(remote[args[2].removeprefix(module.REMOTE + '/')]).hexdigest()
                if args[0] == 'push':
                    name = args[2].removeprefix(module.REMOTE + '/')
                    mutations.append(('push', name))
                    remote[name] = Path(args[1]).read_bytes()
                if args[:3] == ('shell', 'rm', '-f'):
                    name = args[-1].removeprefix(module.REMOTE + '/')
                    mutations.append(('rm', name)); remote.pop(name, None)
                if args[:2] == ('shell', 'pidof'): return '1234'
                if args[:2] == ('shell', 'cat'):
                    return ('[rd] ui mode=OCG DuelStart received\n'
                            '[undo] begin(replayall) swallow=2\n'
                            '[undo] finish forked=False replayed=2/2 inbound=2\n'
                            'duel end -> onExit, returnServant=aiRoom')
                if args[:2] == ('shell', 'ls'):
                    remote.pop('commamd.shell', None)
                return ''
            with patch.object(sys, 'argv', ['gate', '--out', str(root), '--serial', 'fake', '--mode', 'OCG', '--deck', 'fixture']), \
                 patch.object(module, 'adb', adb), patch.object(module.subprocess, 'run', run), \
                 patch.object(module.time, 'sleep', lambda _: None):
                code = module.main()
            return code, remote, mutations

    def test_wrong_apk_never_cleans_existing_inputs(self):
        existing = {'log/qt_debug.on': b'owned by user'}
        code, remote, mutations = self.run_case(existing, installed='wrong')
        self.assertEqual(code, 1); self.assertEqual(remote, existing); self.assertEqual(mutations, [])

    def test_conflicting_flag_or_command_is_preserved(self):
        for name in ['log/qt_debug.on', 'log/qt_undoreplay.on', 'commamd.shell']:
            with self.subTest(name=name):
                code, remote, mutations = self.run_case({name: b'pending'})
                self.assertEqual(code, 1); self.assertEqual(remote, {name: b'pending'}); self.assertEqual(mutations, [])

    def test_different_deck_is_preserved(self):
        existing = {'deck/fixture.ydk': b'personal changes'}
        code, remote, mutations = self.run_case(existing)
        self.assertEqual(code, 1); self.assertEqual(remote, existing); self.assertEqual(mutations, [])

    def test_identical_deck_is_not_written_or_deleted(self):
        existing = {'deck/fixture.ydk': b'user deck'}
        code, remote, mutations = self.run_case(existing)
        self.assertEqual(code, 0); self.assertEqual(remote, existing)
        self.assertFalse(any(name == 'deck/fixture.ydk' for _, name in mutations))

    def test_new_fixture_is_cleaned_after_success(self):
        code, remote, mutations = self.run_case({})
        self.assertEqual(code, 0); self.assertEqual(remote, {})
        self.assertIn(('push', 'deck/fixture.ydk'), mutations)
        self.assertIn(('rm', 'deck/fixture.ydk'), mutations)

class ApkIdentityTests(unittest.TestCase):
    def test_wrong_hash_and_empty_inputs_cannot_retain_pass(self):
        module = load('verify_apk_native')
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp); apk = root / 'fixture.apk'
            with zipfile.ZipFile(apk, 'w') as package: package.writestr('lib/arm64-v8a/test.so', b'native')
            digest = hashlib.sha256(apk.read_bytes()).hexdigest()
            for claimed, native in [('wrong', {'arm64-v8a/test.so': 'unused'}), (digest, {})]:
                (root / 'apk-result.json').write_text(json.dumps({'apk': str(apk), 'sha256': claimed}))
                (root / 'build-input.json').write_text(json.dumps({'native': native}))
                result = root / 'apk-native-result.json'; result.write_text('{"status":"passed"}')
                with patch.object(sys, 'argv', ['gate', '--out', str(root)]), self.assertRaises(RuntimeError): module.main()
                self.assertEqual(json.loads(result.read_text())['status'], 'failed')

if __name__ == '__main__': unittest.main()
