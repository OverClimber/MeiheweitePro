#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")"
mkdir -p out/lua out/core
sdk="$(xcrun --sdk iphoneos --show-sdk-path)"
flags=(-target arm64-apple-ios13.0 -isysroot "$sdk" -O2 -fvisibility=hidden)
for source in lua-5.4.8/src/*.c; do
  name="$(basename "$source" .c)"
  [[ "$name" == lua || "$name" == luac ]] && continue
  xcrun --sdk iphoneos clang++ "${flags[@]}" -x c++ -std=c++17 -DLUA_USE_POSIX -I lua-5.4.8/src -c "$source" -o "out/lua/$name.o"
done
for source in ocgcore/*.cpp; do
  name="$(basename "$source" .cpp)"
  xcrun --sdk iphoneos clang++ "${flags[@]}" -std=c++17 -DOCGCORE_EXPORT_FUNCTIONS -I lua-5.4.8/src -I ocgcore -c "$source" -o "out/core/$name.o"
done
xcrun libtool -static -o out/libocgcore-ios.a out/core/*.o out/lua/*.o
xcrun --sdk iphoneos clang++ "${flags[@]}" -dynamiclib out/core/*.o out/lua/*.o -o out/core-link-check.dylib
xcrun nm -gU out/libocgcore-ios.a > out/core-symbols.txt
for symbol in create_duel create_duel_v2 create_duel_undo set_card_reader set_script_reader set_message_handler start_duel end_duel process get_message set_responseb set_responsei query_card query_field_count query_field_card query_field_info preload_script new_card new_tag_card get_log_message set_player_info; do
  grep -Eq "[[:space:]]_${symbol}$" out/core-symbols.txt
done
shasum -a 256 out/libocgcore-ios.a > out/SHA256SUMS
