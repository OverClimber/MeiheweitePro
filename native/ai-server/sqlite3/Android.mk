# Sqlite3 —— 层库改写版（不是 MDPro3 上游原件）
#
# 上游原件 sha256: 5b80a1c674608dc34f6aa3aa04ea59111f4563598b8b00c33b9c127d5aee4f26
# 覆盖时机：tools/native-recipe/build_ygoserver.sh 铺完上游源码后，先校验上游指纹，再用本文件覆盖。
#           指纹不符 ⇒ 中止并要求人工比对（见脚本 1.6 节）。
#
# ── 唯一改动：给 sqlite3 这一个编译单元关掉 fortify ──────────────────────────
# NDK 的 build/core/default-build-commands.mk:134 全局加了 `-D_FORTIFY_SOURCE=2`。
# 于是 Android 的 <sys/stat.h> 末尾会 include <bits/fortify/stat.h>，
# 而那个头用一个 `__overloadable` 的 umask 去「重载」真的 umask
# （头里原话：Abuse enable_if to make this an overload of umask）。
# ⇒ sqlite3.c:23320 的 `(sqlite3_syscall_ptr)umask` 取函数地址就变成
#   "address of overloaded function 'umask' is ambiguous"，硬错误、编不过。
#   （`__overloadable` 让 clang 在 C 模式下也按重载处理，所以诊断是 C++ 措辞。）
#
# ⛔ 旗标只加在本模块：全局关 fortify 会削弱其他模块的加固，不做。
# ⛔ -U 与 -D 两句都要：只写 -D 的话，万一 NDK 那句 -D_FORTIFY_SOURCE=2 排在我们后面，
#    就会反压回来。LOCAL_CFLAGS 在编译命令行里排在 GLOBAL/TARGET_CFLAGS 之后，
#    两句一起写才稳（已实测：带旗标可编过，1.07 MB .o）。
# ⛔ 不改 sqlite3.c 本身，也不改 sqlite3 版本 —— 只改这一个模块的编译旗标。
#
# 其余与上游逐字一致（含 -O2 -Wall）。
LOCAL_PATH := $(call my-dir)

include $(CLEAR_VARS)

LOCAL_MODULE    := sqlite3
LOCAL_MODULE_FILENAME := libsqlite3
LOCAL_SRC_FILES := sqlite3.c

LOCAL_CFLAGS    := -O2 -Wall -U_FORTIFY_SOURCE -D_FORTIFY_SOURCE=0
include $(BUILD_STATIC_LIBRARY)
