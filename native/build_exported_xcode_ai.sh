#!/usr/bin/env bash
set -euo pipefail
: "${EXPORT_ZIP:?Set EXPORT_ZIP to reviewed Unity export archive}"
python3 - "$EXPORT_ZIP" <<'PY'
import pathlib,sys,zipfile
src=pathlib.Path(sys.argv[1]); dest=pathlib.Path('xcode-export')
if dest.exists(): raise SystemExit('Destination already exists')
with zipfile.ZipFile(src) as z:
    for info in z.infolist():
        path=pathlib.PurePosixPath(info.filename)
        if path.is_absolute() or '..' in path.parts: raise SystemExit('Unsafe archive path')
    z.extractall(dest)
if not (dest/'Unity-iPhone.xcodeproj').is_dir(): raise SystemExit('Xcode project missing')
PY
python3 native/apply_export_delta.py native/ai-urlfix.zip xcode-export
python3 native/apply_export_delta.py native/touch-20261024.zip xcode-export
python3 native/apply_export_delta.py native/ui-20261026.zip xcode-export
python3 native/apply_export_delta.py native/ui-20261028.zip xcode-export
python3 native/disable_legacy_ai_export.py xcode-export
python3 native/seed_ai_test_decks.py xcode-export/Data/Raw/gamedata.zip
xcrun --sdk iphoneos clang++ -target arm64-apple-ios13.0 -c native/legacy_ai_unavailable.cpp -o native/legacy_ai_unavailable.o
mkdir -p xcode-export/Libraries
xcrun libtool -static -o xcode-export/Libraries/libiPhone-lib-combined.a \
  xcode-export/Libraries/libiPhone-lib.a native/out/libocgcore-ios.a native/out/libygoserver-ios.a native/legacy_ai_unavailable.o
mv xcode-export/Libraries/libiPhone-lib-combined.a xcode-export/Libraries/libiPhone-lib.a
xcodebuild -project xcode-export/Unity-iPhone.xcodeproj -scheme Unity-iPhone \
  -configuration Release -sdk iphoneos -destination 'generic/platform=iOS' \
  -derivedDataPath "$PWD/DerivedData" ARCHS=arm64 ONLY_ACTIVE_ARCH=NO \
  CODE_SIGNING_ALLOWED=NO CODE_SIGNING_REQUIRED=NO build > xcodebuild.log 2>&1 || {
    tail -n 100 xcodebuild.log
    exit 1
  }
app=("$PWD"/DerivedData/Build/Products/Release-iphoneos/*.app)
[[ ${#app[@]} -eq 1 && -d ${app[0]} ]] || { echo 'Expected exactly one device app' >&2; exit 2; }
python3 - "${app[0]}" <<'PY'
import pathlib,sys,zipfile
archives=list(pathlib.Path(sys.argv[1]).rglob('gamedata.zip'))
if len(archives)!=1: raise SystemExit('Expected exactly one bundled game-data archive')
with zipfile.ZipFile(archives[0]) as z:
    required={'cdb/cards.cdb','data/script.zip','rd/ai/cdb/cards.cdb',
              'deck/iOS_AI_Test.ydk','deck_rd/iOS_RD_AI_Test.ydk'}
    if not required.issubset(z.namelist()) or z.testzip():
        raise SystemExit('Bundled AI data validation failed')
print('Verified bundled AI data and initial OCG/RD decks')
PY
python3 native/package_unsigned.py --app "${app[0]}" --output "$PWD/MeiheweitePro-pending-signing.ipa"
