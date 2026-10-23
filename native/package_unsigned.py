"""Package an existing, unsigned iPhoneOS .app. Does not compile, sign or install."""
import argparse
import hashlib
import json
import plistlib
import shutil
import struct
import zipfile
from pathlib import Path


def package(app, output):
    app = app.resolve()
    output = output.resolve()
    if app.suffix != '.app' or not app.is_dir():
        raise ValueError('expected an existing .app directory')
    if output.is_relative_to(app):
        raise ValueError('output must be outside the source bundle')
    if output.suffix != '.ipa':
        raise ValueError('output must have an .ipa suffix')
    if output.exists() or output.with_suffix('.json').exists():
        raise ValueError('refusing to overwrite an existing IPA')
    info = plistlib.loads((app / 'Info.plist').read_bytes())
    if info.get('CFBundleSupportedPlatforms') != ['iPhoneOS']:
        raise ValueError('not an iPhoneOS device build (simulator bundles are rejected)')
    executable = info.get('CFBundleExecutable', '')
    if not executable or Path(executable).name != executable:
        raise ValueError('invalid bundle executable')
    # arm64 Mach-O executable, not an ELF/PE/archive or a simulator binary.
    binary = (app / executable).read_bytes()
    header = binary[:32]
    if len(header) != 32 or struct.unpack('<IIIIIIII', header)[:2] != (0xFEEDFACF, 0x0100000C):
        raise ValueError('expected a thin arm64 Mach-O executable')
    fields = struct.unpack('<IIIIIIII', header)
    if fields[3] != 2:
        raise ValueError('Mach-O is not MH_EXECUTE')
    offset, platform_seen = 32, False
    end = 32 + fields[5]
    if end > len(binary): raise ValueError('truncated Mach-O load commands')
    for _ in range(fields[4]):
        if offset + 8 > end: raise ValueError('truncated Mach-O command')
        command, size = struct.unpack_from('<II', binary, offset)
        if size < 8 or offset + size > end: raise ValueError('invalid Mach-O command size')
        if command == 0x32:  # LC_BUILD_VERSION
            if size < 24: raise ValueError('invalid LC_BUILD_VERSION')
            if struct.unpack_from('<I', binary, offset + 8)[0] != 2:
                raise ValueError('Mach-O platform is not iOS device')
            platform_seen = True
        if command == 0x25:  # LC_VERSION_MIN_IPHONEOS, legacy device toolchains
            if size < 16: raise ValueError('invalid LC_VERSION_MIN_IPHONEOS')
            platform_seen = True
        offset += size
    if not platform_seen or offset != end:
        raise ValueError('missing device platform or inconsistent load commands')
    paths = sorted(app.rglob('*'))
    for path in paths:
        if path.is_symlink() or path.is_junction():
            raise ValueError('linked bundle files need macOS-aware packaging: ' + str(path))
    output.parent.mkdir(parents=True, exist_ok=True)
    # Exclusive creation prevents accidental replacement, including concurrent jobs.
    with zipfile.ZipFile(output, 'x', zipfile.ZIP_DEFLATED) as archive:
        for path in paths:
            if path.is_file():
                arcname = 'Payload/' + app.name + '/' + path.relative_to(app).as_posix()
                entry = zipfile.ZipInfo(arcname)
                entry.create_system = 3
                entry.external_attr = ((0o100755 if path.name == executable or path.stat().st_mode & 0o111 else 0o100644) << 16)
                entry.compress_type = zipfile.ZIP_DEFLATED
                with archive.open(entry, 'w') as target, path.open('rb') as source:
                    shutil.copyfileobj(source, target)
    with zipfile.ZipFile(output) as archive:
        bad = archive.testzip()
        if bad: raise ValueError('IPA CRC failed: ' + bad)
    sha = hashlib.sha256()
    with output.open('rb') as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b''): sha.update(block)
    result = {'status': 'packaged_requires_signing', 'ipa': str(output),
              'sha256': sha.hexdigest(),
              'bundle_id': info.get('CFBundleIdentifier'), 'installed': False,
              'note': 'Packaging and CRC do not prove linkage, signatures or runtime compatibility.'}
    output.with_suffix('.json').write_text(json.dumps(result, indent=2), encoding='utf-8')
    return result


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--app', required=True, type=Path)
    parser.add_argument('--output', required=True, type=Path)
    args = parser.parse_args()
    try:
        print(json.dumps(package(args.app, args.output)))
    except (OSError, ValueError, plistlib.InvalidFileException) as error:
        parser.exit(2, str(error) + '\n')
