#!/usr/bin/env bash
set -euo pipefail
python3 - <<'PY'
import hashlib,json,pathlib
root=pathlib.Path('native/ai-server')
manifest=json.loads((root/'source-manifest.json').read_text())
for name, expected in manifest['files'].items():
    actual=hashlib.sha256((root/name).read_bytes()).hexdigest()
    if actual!=expected: raise SystemExit('Source mismatch: '+name)
print('Verified',len(manifest['files']),'source files')
PY
python3 native/patch_init_duel.py native/ai-server
set -euo pipefail
src=native/ai-server
for unit in data_manager deck_manager game gframe netserver replay serverapi single_duel tag_duel; do
xcrun --sdk iphoneos clang++ -target arm64-apple-ios13.0 \
  -isysroot "$(xcrun --sdk iphoneos --show-sdk-path)" \
  -std=c++17 -O2 -DYGOPRO_SERVER_MODE -DSERVER_ZIP_SUPPORT \
  -DSERVER_PRO2_SUPPORT -DSERVER_PRO3_SUPPORT \
  -DSERVER_TAG_SURRENDER_CONFIRM \
  -I "$src/ygoserver" -I "$src/ygoserver/spmemvfs" \
  -I "$src/ocgcore" -I "$src/event/include" -I "$src/event/android" \
  -I "$src/sqlite3" -I "$src/irrlicht/source/Irrlicht" \
  -I "$src/irrlicht/include" \
  -c "$src/ygoserver/$unit.cpp" -o "$unit.o"
done
python3 native/check_init_duel_binary.py netserver.o --objdump "$(xcrun --find llvm-objdump)"
set -euo pipefail
src=native/ai-server
sdk="$(xcrun --sdk iphoneos --show-sdk-path)"
cc=(xcrun --sdk iphoneos clang -target arm64-apple-ios13.0 -isysroot "$sdk" -O2)
mkdir -p ai-objects
mkdir -p ai-config/event2
python3 - <<'PY'
from pathlib import Path
src=Path('native/ai-server/event/android/event2/event-config.h').read_text()
for key in ('_EVENT_HAVE_EPOLL','_EVENT_HAVE_EPOLL_CTL','_EVENT_HAVE_EVENTFD',
            '_EVENT_HAVE_SYS_EPOLL_H','_EVENT_HAVE_SYS_EVENTFD_H',
            '_EVENT_HAVE_SENDFILE','_EVENT_HAVE_SYS_SENDFILE_H',
            '_EVENT_HAVE_NETINET_IN6_H'):
    src=src.replace('#define '+key+' 1', '/* #undef '+key+' */')
src=src.replace('/* #undef _EVENT_HAVE_FD_MASK */', '#define _EVENT_HAVE_FD_MASK 1')
Path('ai-config/event2/event-config.h').write_text(src)
PY
for unit in Alloc LzFind LzmaDec LzmaEnc LzmaLib; do
  "${cc[@]}" -c "$src/ygoserver/lzma/$unit.c" -o "ai-objects/lzma-$unit.o"
done
"${cc[@]}" -I "$src/sqlite3" -c "$src/ygoserver/spmemvfs/spmemvfs.c" -o ai-objects/spmemvfs.o
"${cc[@]}" -c "$src/sqlite3/sqlite3.c" -o ai-objects/sqlite3.o
for unit in event buffer bufferevent bufferevent_sock bufferevent_pair listener evmap log evutil strlcpy signal bufferevent_filter evthread evthread_pthread bufferevent_ratelim evutil_rand event_tagging http evdns evrpc poll select; do
  "${cc[@]}" -I ai-config -I "$src/event" -I "$src/event/include" -I "$src/event/compat" \
    -c "$src/event/$unit.c" -o "ai-objects/event-$unit.o"
done
set -euo pipefail
src=native/ai-server/irrlicht
sdk="$(xcrun --sdk iphoneos --show-sdk-path)"
common=(-target arm64-apple-ios13.0 -isysroot "$sdk" -O2 \
  -D_IRR_STATIC_LIB_ -DNO_IRR_COMPILE_WITH_ZIP_ENCRYPTION_ \
  -DNO_IRR_COMPILE_WITH_BZIP2_ \
  -DNO__IRR_COMPILE_WITH_MOUNT_ARCHIVE_LOADER_ \
  -DNO__IRR_COMPILE_WITH_PAK_ARCHIVE_LOADER_ \
  -DNO__IRR_COMPILE_WITH_NPK_ARCHIVE_LOADER_ \
  -DNO__IRR_COMPILE_WITH_TAR_ARCHIVE_LOADER_ \
  -DNO__IRR_COMPILE_WITH_WAD_ARCHIVE_LOADER_ \
  -I "$src/include" -I "$src/source/Irrlicht" -I "$src/source/Irrlicht/zlib")
for unit in adler32 crc32 inffast inflate inftrees zutil; do
  xcrun --sdk iphoneos clang "${common[@]}" -c "$src/source/Irrlicht/zlib/$unit.c" -o "ai-objects/irr-z-$unit.o"
done
for unit in os CAttributes CFileList CFileSystem CLimitReadFile CMemoryFile CReadFile CWriteFile CXMLReader CXMLWriter CZipReader; do
  xcrun --sdk iphoneos clang++ "${common[@]}" -std=gnu++17 -c "$src/source/Irrlicht/$unit.cpp" -o "ai-objects/irr-$unit.o"
done
bash native/build_core.sh
xcrun libtool -static -o native/out/libygoserver-ios.a ./*.o ai-objects/*.o
xcrun --sdk iphoneos clang++ -target arm64-apple-ios13.0   -isysroot "$(xcrun --sdk iphoneos --show-sdk-path)" -dynamiclib   ./*.o ai-objects/*.o native/out/libocgcore-ios.a -o native/out/ai-link-check.dylib
