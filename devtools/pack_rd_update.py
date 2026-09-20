# -*- coding: utf-8 -*-
"""把一个目录打成 RD 数据更新包（ypk）—— RdDataUpdater 的对端工具。

用法：
    python pack_rd_update.py <源目录> <输出.ypk>

源目录的布局 = rd/ 的布局（只允许白名单内的顶层）：
    cdb/*.cdb            客户端 RD 池卡表
    picture/card/*.jpg   卡图、picture/closeup/*.jpg 立绘
    ai/…                 人机整包（ai/cdb、ai/script、ai/config…）
    lflist.conf          RD 禁限表
    bot.conf             机器人名单

另外也兼容 **RD 先行卡包的原生布局**（2026-09-20 起，游戏侧做映射）：
    RD Patch.cdb（根部） → 覆盖 cdb/rd_patch.cdb；其他根部 *.cdb → cdb/<原名>
    pics/…               → picture/card/…
    script/…             → ai/script/…
    server.ini           → 忽略（不要打进包里）

产物是普通 zip（游戏侧用 Ionic.Zip 读），条目路径保留相对路径、一律 / 分隔。
顶层白名单与 Assets/SibylSystem/ResourceManagers/RdDataUpdater.cs 的
AllowedPrefixes 必须一致 —— 改一处要同时改两处。
"""

import os
import re
import sys
import zipfile

ALLOWED = ("cdb/", "picture/", "ai/", "lflist.conf", "bot.conf")
# 先行卡包原生布局的别名（与 RdDataUpdater.MapEntry 同口径；改一处要同时改两处）
NATIVE_ROOT_CDB = re.compile(r"^rd[\s_-]?patch\.cdb$", re.IGNORECASE)


def map_entry(rel):
    """包内相对路径 → rd/ 相对落地路径。返回 None = 忽略。"""
    rel = rel.replace("\\", "/").lstrip("/")
    if "/" not in rel:
        low = rel.lower()
        if low.endswith(".cdb"):
            if NATIVE_ROOT_CDB.match(low):
                return "cdb/rd_patch.cdb"
            return "cdb/" + rel
        if low == "server.ini":
            return None
        return rel
    head, rest = rel.split("/", 1)
    if head.lower() == "pics":
        return "picture/card/" + rest
    if head.lower() == "script":
        return "ai/script/" + rest
    return rel


def human(n):
    for unit in ("B", "KB", "MB", "GB"):
        if n < 1024 or unit == "GB":
            return "%.1f%s" % (n, unit) if unit != "B" else "%dB" % n
        n /= 1024.0


def main():
    if len(sys.argv) != 3:
        print(__doc__)
        return 2
    src, out = sys.argv[1], sys.argv[2]
    if not os.path.isdir(src):
        print("XX 源目录不存在：%s" % src)
        return 2

    src = os.path.abspath(src)
    bad = []
    files = []
    for root, dirs, names in os.walk(src):
        dirs[:] = [d for d in dirs if d != "__pycache__"]
        for name in names:
            full = os.path.join(root, name)
            rel = os.path.relpath(full, src).replace("\\", "/")
            if map_entry(rel) is None:
                continue  # server.ini 等忽略项不打包
            target = map_entry(rel)
            ok = any(target == p or target.startswith(p) for p in ALLOWED)
            if not ok:
                bad.append(rel)
            files.append((full, rel))

    if bad:
        print("XX 白名单外的条目（游戏会整包拒收）：")
        for rel in bad[:20]:
            print("     %s" % rel)
        if len(bad) > 20:
            print("     …共 %d 条" % len(bad))
        return 1
    if not files:
        print("XX 源目录里一个文件都没有")
        return 1

    with zipfile.ZipFile(out, "w", zipfile.ZIP_DEFLATED) as z:
        for full, rel in sorted(files, key=lambda t: t[1]):
            z.write(full, rel)

    total = sum(os.path.getsize(f) for f, _ in files)
    print("OK %s：%d 个文件，%s，条目 %d 条"
          % (out, len(files), human(total), len(files)))
    return 0


if __name__ == "__main__":
    sys.exit(main())
