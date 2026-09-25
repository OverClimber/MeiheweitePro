#!/usr/bin/env python
# -*- coding: utf-8 -*-
"""
RD（超速决斗）数据分发脚本 —— 把外部参考包展开成客户端运行目录下的 rd/ 布局。

⚠ 版本控制：本文件是**权威版**，随 `feature/rush-duel` 分支入库。
  早期副本在工作区根下的 `_unpack_rd.py`（那个目录不是 git 仓库）；今后改动以本文件为准。
  源包与 rd/ 数据本身**不入 git**（合计 1.6 GB+），清单与 MD5 见同目录 `rd_data_layout.md`。

背景：
  · 客户端装载层是「全量共池」的，RD 数据必须与 OCG 分池，
    所以产物里 RD 的东西一律放在 rd/ 子目录下，由 Program.cs 单独一条通道装载：
        rd/cdb/*.cdb        → CardsManager 的 RD 池
        rd/lflist.conf      → BanlistManager 的额外来源（RD 表）
        rd/picture/card/    → RD 卡图
        rd/picture/closeup/ → RD 立绘
  · **RD 人机（本地 AI 对局）** 的东西全部收在 rd/ai/ 一个工作目录里：
        rd/ai/AI.Server.exe      → 本客户端自带的那份 core（拷一份，见下「为什么要独立目录」）
        rd/ai/script/            → RD 规则库 + 3054 张 RD 卡脚本（+ 兼容引导，见下）
        rd/ai/cdb/cards.cdb      → AI.Server 与 WindBot 共读的卡表（三份 RD cdb 合并）
        rd/ai/config/lflist.conf → AI.Server 读的禁限表（= rd/lflist.conf，两侧口径必须一致）
        rd/ai/WindBot/           → RD 版机器人（WindBot.exe + dll + Decks + Dialogs）
        rd/bot.conf              → RD 机器人名单（客户端 AIRoom 读）
    为什么 AI.Server 需要独立工作目录：AI.Server.exe 的脚本/卡表路径是**相对 cwd** 的
    （实测二进制里就是 `./script/constant.lua`、`./script/c%d.lua`、`./cdb/%s` +
     "cards.cdb"、`config/lflist.conf` 这些字面量），而 RD 的 constant/utility/procedure
    是 RD 变体，与 OCG 那套不能共存 ⇒ 只能给它一个自己的目录，起进程时把 cwd 指过去。
      ・WindBot 也读 `../cdb/cards.cdb`（相对它自己所在目录）—— 把 WindBot 放在
        rd/ai/WindBot/ 正好让 `../cdb/` 落在 rd/ai/cdb/，两边共用同一份合并表。
    为什么卡脚本要从 ypk 里抽出来铺成文件：core 是按 `./script/c<code>.lua` 一个一个读的，
    不吃 ypk；而 RD 客户端脚本目录里只有 22 个规则库，3054 张卡脚本都在 ypk 的 script/ 成员里。
    ⚠ 规则库那条链上还有一层兼容引导，见 `devtools/rd_ai/bootstrap.lua`（写了取证与本 core 的差异）。
  · rd/ **不在** Assets/Editor/BuildHelper.RuntimeDataDirectories 里 —— 那是特意的：
    卡图/立绘合起来 ~770 MB，每次构建都 File.Copy 一遍纯属浪费。
    ⇒ **每次「重建 output/」之后要重跑一次本脚本**；日常改代码构建不受影响。

用法：
    python devtools/unpack_rd.py                  # 卡表 + 禁限表 + RD 人机包（默认，分钟级）
    python devtools/unpack_rd.py --all            # 再连卡图 / 立绘一起铺（约 +760 MB）
    python devtools/unpack_rd.py --no-ai          # 跳过 RD 人机包（只铺卡表/禁限表，秒级）
    python devtools/unpack_rd.py --force          # 已存在的目标文件也重写
    python devtools/unpack_rd.py --src D:/xx/RD   # 指定源包目录（默认 <工作区>/参考内容/RD）
    python devtools/unpack_rd.py --client D:/xx/电脑版RD客户端.zip   # RD 客户端包（zip 或已解包目录）
    python devtools/unpack_rd.py --dest D:/xxx    # 指定产物根目录（默认 <本仓库>/output/Windows）
    python devtools/unpack_rd.py --list           # 只列源包内容，不落盘

退出码：0 = 成功；1 = 源包缺失 / 结构不符。
"""

import argparse
import hashlib
import os
import re
import sqlite3
import sys
import zipfile

HERE = os.path.dirname(os.path.abspath(__file__))          # <仓库>/devtools
REPO = os.path.dirname(HERE)                               # <仓库> = YGOProUnity_V2
WORKSPACE = os.path.dirname(REPO)                          # 工作区 = 仓库的上一级目录
DEFAULT_SRC = os.path.join(WORKSPACE, "参考内容", "RD")
DEFAULT_DEST = os.path.join(REPO, "output", "Windows")

# 源包 → (包内 cdb 名, 落地文件名)。三份表 id 域互不重叠（已实测：
# 正式卡 3324 张 120000000~120308006、先行卡 76 张、异画卡 84 张），
# 故装载顺序不影响身份，统一走 replace=true 让后加载的可覆盖。
CDBS = [
    ("RD正式卡.ypk", "RD Standard.cdb", "rd_standard.cdb"),
    ("RD先行卡 (15).ypk", "RD Patch.cdb", "rd_patch.cdb"),
    ("RD异画卡.ypk", "RD Alternate.cdb", "rd_alternate.cdb"),
]

LFLIST_ZIP = "2026.7 禁限表RD补丁+使用说明 (2).zip"
LFLIST_MEMBER = "2026.7 RD 禁卡表补丁+使用说明/lflist.conf"

CLOSEUP_ZIP = "【Pro2专用】RD立绘补丁.zip"

# ---------- RD 人机包：来源 = 电脑版 RD 客户端（KoishiPro 系，自带 RD 版 WindBot） ----------
CLIENT_ZIP = "电脑版RD客户端.zip"
# 这份包在工作区「参考内容/」下（不在 RD/ 子目录里），所以单独给一条默认路径。
DEFAULT_CLIENT = os.path.join(WORKSPACE, "参考内容", CLIENT_ZIP)
# 客户端包内的根目录名。zip 里是 "KoishiPro/..."，解包后的目录同理。
CLIENT_ROOT = "KoishiPro"
CLIENT_SCRIPT = "script"          # RD 规则库（22 个 lua）
CLIENT_WINBOT = "WindBot"         # RD 版机器人。**只有 2021 之后这份才认 RD 卡**，
                                  # 我们自带的 WindBot 是 OCG 的，两者不可互换
CLIENT_BOTCONF = "bot.conf"       # RD 机器人名单

# RD 规则库清单（顺序 = 原版 special.lua 的加载顺序，内联时必须保持）。
# 全部会被内联进产物 utility.lua；原始文件也一并铺到 rd/ai/script/ 留档便于比对。
RD_LIBS = [
    "RDBase.lua", "RDLegend.lua", "RDRule.lua", "RDMaximum.lua", "RDFunction.lua",
    "RDCondition.lua", "RDCost.lua", "RDTarget.lua", "RDValue.lua", "RDContinuous.lua",
    "RDAttach.lua", "RDLimit.lua", "RDAction.lua", "RDFusion.lua", "RDRitual.lua",
    "RDEquip.lua", "RDSummon.lua",
]
# 三个核心脚本也用 RD 变体（与 OCG 的 constant/procedure/utility 不是一份东西，
# 实测 MD5 全不同且 RD 那套多了 special.lua）。special.lua 单独处理 —— 见 rd_ai/special.lua。
RD_CORE_SCRIPTS = ["constant.lua", "procedure.lua", "utility.lua"]

# 卡脚本里的 `RD.AlternateCard(N)`（异画卡/网络复用另一张卡的实现）= 原版唯一一处
# 运行时动态加载（RushDuel.AlternateCard → Duel.LoadScript）。本 core 没有 LoadScript，
# 所以生成期内联成目标脚本源码 —— 语义等价：原版 LoadScript 也是在「当前脚本」的
# self_table/self_code 上下文里跑，内联后一模一样。
ALT_CARD_RE = re.compile(r"RD\.AlternateCard\s*\(\s*(\d+)\s*\)")
ALT_CARD_MAX_DEPTH = 4

# ---------- ⛔⛔ 这里曾经有一张「摘掉 EFFECT_FLAG_SINGLE_RANGE」的补丁表，已删除 ----------
# 2026-09-22 教训（**别再重新加回来**）：当时观察到「极大召唤后攻击力停在卡面原始值
# 1900」，就断定本 core 把带 EFFECT_FLAG_SINGLE_RANGE 的效果判成不可用，于是往
# RDMaximum.lua 的 103 那条抄了个「摘掉该 flag」的补丁，还写了一版 strip_single_range.py
# 打算批量摘 RD 侧 67 处、并推算出 OCG 侧 499 处「同样会失效」。
#
# 全部是误判。真凶是**造局探针少给了 enable 参数**：
#   libduel.cpp:872 `Duel.MoveToField(..., enable, ...)` 的第 6 参决定 move_to_field 之后
#   是否 `enable_field_effect(true)`。探针给 false ⇒ 卡的 STATUS_EFFECT_ENABLED 一直不置位
#   ⇒ effect.cpp:65 `is_flag(SINGLE_RANGE) && !phandler->get_status(STATUS_EFFECT_ENABLED)`
#   把 103 判成不可用。真实游戏里本体走 AddHandSpecialSummonProcedure → SpecialSummon
#   （召唤路径自带 enable），所以玩家自己召唤从来就是 3500。
# 铁证：同一张卡、同一位置、**同一份原生 lua（带 SINGLE_RANGE）**，只把探针第 6 参
# false→true，atk 立刻 1900→3500（_verify_rdai_game.py --max 的 6k1/6k2/6k3）。
#
# ⇒ SINGLE_RANGE 与 ocgcore.dll 都没有缺陷，任何「摘 flag」式的补丁都是纯粹的危害：
#   该 flag 在 core 里还兼着「作用范围限本卡 + 里侧/未启用时不生效」的语义，
#   摘掉会让效果在它本不该生效的场合生效。规则库保持与上游逐字一致。

# 兼容层源码（入 git）。生成时把它的 @@RD_LIBS_HERE@@ 换成 RD 规则库源码，
# 再追加到 RD 版 utility.lua 末尾。
BOOTSTRAP = os.path.join(HERE, "rd_ai", "bootstrap.lua")
SPECIAL_STUB = os.path.join(HERE, "rd_ai", "special.lua")

# 合并卡表要带过去的表。口径与 CardsManager 同源（`SELECT datas.*, texts.* FROM datas,texts
# WHERE datas.id=texts.id`）—— 两张表按 id 各合各的，同 id 后加载者覆盖。
CDB_TABLES = ("datas", "texts")

# 产物侧 RD 人机布局（相对产物根）。⚠ 这几个路径与 C# 侧 GameModeManager 的同名常量
# **必须逐字一致**，改这里就得改那边。
AI_DIR = ("rd", "ai")
AI_SCRIPT_DIR = ("rd", "ai", "script")
AI_CDB_DIR = ("rd", "ai", "cdb")
AI_CONFIG_DIR = ("rd", "ai", "config")
AI_WINBOT_DIR = ("rd", "ai", "WindBot")
BOT_CONF_REL = ("rd", "bot.conf")
# AI.Server 那份 core 从产物根拷（本客户端自带的那一个，版本必须与客户端同源）。
AI_SERVER_SRC = "AI.Server.exe"
# 客户端脚本目录里的这两个文件由生成逻辑另写，不照抄（原因见下）。
CLIENT_SPECIAL = "special.lua"
CLIENT_UTILITY = "utility.lua"

# 源包指纹（2026-09-19 实测；与 rd_data_layout.md 同源）。只做核对告警，不阻断：
# 上游出包会变，但「换了包」这件事必须让人看见。
SRC_FINGERPRINT = {
    "RD正式卡.ypk": (678666934, "baa0cc89a9ad9824b212b071141941d4"),
    "RD先行卡 (15).ypk": (30899139, "a580c7fc294f5ffff44ff9c9a675476e"),
    "RD异画卡.ypk": (19068646, "ab95130d250bba405fc85ee463197469"),
    LFLIST_ZIP: (3702, "e6300e1e1c666d01f7e47e6f31ba2b30"),
    CLOSEUP_ZIP: (78648770, "400dfe50afcd9c9f1433bbf5dedc9721"),
    CLIENT_ZIP: (840169284, "5a5963f84731cb17be244e0374f25693"),
}


def human(n):
    for unit in ("B", "KB", "MB", "GB"):
        if n < 1024 or unit == "GB":
            return "%.1f%s" % (n, unit)
        n /= 1024.0


def md5_of(path):
    h = hashlib.md5()
    with open(path, "rb") as f:
        for b in iter(lambda: f.read(1 << 22), b""):
            h.update(b)
    return h.hexdigest()


def write_file(dest_path, data, force):
    """写文件；同尺寸跳过（幂等）。返回 (状态, 字节数)。"""
    if (not force) and os.path.exists(dest_path) and os.path.getsize(dest_path) == len(data):
        return ("skip", len(data))
    d = os.path.dirname(dest_path)
    if d and not os.path.isdir(d):
        os.makedirs(d)
    tmp = dest_path + ".tmp"
    with open(tmp, "wb") as f:
        f.write(data)
    os.replace(tmp, dest_path)
    return ("write", len(data))


def extract_member(dest_path, data, force):
    st, n = write_file(dest_path, data, force)
    print("    %-6s %-14s %s" % (st, human(n), os.path.relpath(dest_path, REPO)))
    return st


def check_cdb(path):
    """与 CardsManager.initialize 同源的完整性校验：同样的 SQL、能读出卡才算数。"""
    con = sqlite3.connect(path)
    try:
        cur = con.execute("SELECT datas.*, texts.* FROM datas,texts WHERE datas.id=texts.id;")
        cnt = sum(1 for _ in cur)
    finally:
        con.close()
    return cnt


def verify_sources(src_dir, names, deep):
    """清单核对：尺寸不符必报；--hash 时连 MD5 一起核（678 MB 那份要读一遍）。"""
    bad = 0
    for name in names:
        path = os.path.join(src_dir, name)
        if not os.path.isfile(path):
            continue
        exp = SRC_FINGERPRINT.get(name)
        if not exp:
            continue
        size, md5 = exp
        got_size = os.path.getsize(path)
        if got_size != size:
            print("    ⚠ 尺寸不符：%s 实测 %d，清单 %d" % (name, got_size, size))
            bad += 1
            continue
        if deep:
            got_md5 = md5_of(path)
            if got_md5 != md5:
                print("    ⚠ MD5 不符：%s 实测 %s，清单 %s" % (name, got_md5, md5))
                bad += 1
    if bad:
        print("    （源包与 rd_data_layout.md 的指纹不一致 —— 若非有意换包，先查来源）")
    return bad


# ==================== RD 人机包：生成 ====================

class ClientSource(object):
    """RD 客户端包的统一读取口 —— zip 与「已经解包的目录」都能用。

    包内根目录固定是 `KoishiPro/`（zip 里成员名是 "KoishiPro/script/x.lua"，
    解包后是 "<dir>/KoishiPro/script/x.lua"）。这里把两边都归一成
    相对 KoishiPro 的名字（"script/x.lua"），生成逻辑只跟归一化名字打交道。

    为什么要支持解包目录：678 MB 那份 ypk 解出来能到 800 MB，反复开 zip 又慢又占内存；
    调试生成逻辑时先把客户端解一次、用 `--client <目录>` 反复跑，是最省时间的做法。
    """

    def __init__(self, path):
        self.path = path
        self.iszip = os.path.isfile(path)
        if self.iszip:
            self._zip = zipfile.ZipFile(path)
            self._root = None
        else:
            self._zip = None
            # 允许直接给到 KoishiPro 那一层，也允许给到它的父目录。
            root = path
            if os.path.isdir(os.path.join(path, CLIENT_ROOT)):
                root = os.path.join(path, CLIENT_ROOT)
            self._root = root
            if not os.path.isdir(root):
                raise ValueError("客户端目录里找不到 %s/：%s" % (CLIENT_ROOT, path))

    def _zip_name(self, rel):
        return CLIENT_ROOT + "/" + rel

    def has(self, rel):
        if self.iszip:
            return self._zip_name(rel) in self._zip.namelist()
        return os.path.isfile(os.path.join(self._root, rel))

    def read(self, rel):
        if self.iszip:
            return self._zip.read(self._zip_name(rel))
        with open(os.path.join(self._root, rel), "rb") as f:
            return f.read()

    def files_under(self, rel):
        """递归列出 rel 目录下的文件。

        ⚠ 返回的名字是**相对客户端根**的（如 "WindBot/WindBot.exe"），不是相对 rel 的 ——
        这样每个结果都能直接喂给 read()。要往下拼产物路径时，先切掉 rel 前缀。
        """
        pref = rel.rstrip("/") + "/"
        out = []
        if self.iszip:
            for n in self._zip.namelist():
                if n.endswith("/"):
                    continue
                full = self._zip_name(pref)
                if n.startswith(full):
                    out.append(n[len(CLIENT_ROOT) + 1:])
        else:
            base = os.path.join(self._root, *pref.split("/"))
            for dirpath, _, files in os.walk(base):
                for f in files:
                    full = os.path.join(dirpath, f)
                    out.append(os.path.relpath(full, self._root).replace("\\", "/"))
        return out


def collect_card_scripts(src_dir):
    """从三份 ypk 的 `script/c<code>.lua` 成员里取出全部 RD 卡脚本 → {code: 源码}。

    为什么卡脚本不从客户端包里拿：RD 客户端的 script/ 只有 22 个规则库文件，
    3054 张卡的脚本全在 ypk 里（这是实测，别按「客户端脚本目录更权威」的直觉改 ——
    两份脚本集是同一批，ypk 那份才是我们的卡表 id 域对应的那一份）。
    三份 ypk 的 id 域互不重叠（与 CDBS 的注释同源），所以后读到的不会覆盖掉有用的东西。
    """
    table = {}
    for src_name, _, _ in CDBS:
        with zipfile.ZipFile(os.path.join(src_dir, src_name)) as z:
            for n in z.namelist():
                if not n.lower().startswith("script/"):
                    continue
                base = os.path.basename(n)
                m = re.match(r"^c(\d+)\.lua$", base, re.I)
                if not m:
                    continue
                table[int(m.group(1))] = z.read(n).decode("utf-8", "replace")
    return table


def expand_alternates(code, source, table, depth=0, stack=(), counter=None):
    """把卡脚本里的 `RD.AlternateCard(N)` 就地展开成目标卡脚本的源码。

    原版：`RushDuel.AlternateCard(code)` = `Duel.LoadScript("c"..code..".lua")`，
    异画卡脚本整个文件就一行 `RD.AlternateCard(120244022)` —— 语义是「复用那张卡的实现，
    但 self_table/self_code 仍是我自己」（GetID 取的是当前脚本的表/码，所以复制过去就对了）。
    本 core 没有 Duel.LoadScript，生成期把它摊平，语义完全等价。

    包裹用 `do ... end`：原版被加载的 chunk 顶层 local 是自己的作用域，包一层块才等值。
    """
    if counter is None:
        counter = [0]
    if ALT_CARD_RE.search(source) is None:
        return source

    def repl(m):
        target = int(m.group(1))
        if target in stack:
            raise ValueError("异画卡循环引用：c%d -> %s"
                             % (target, " -> ".join("c%d" % c for c in stack + (target,))))
        if depth >= ALT_CARD_MAX_DEPTH:
            raise ValueError("异画卡嵌套超过 %d 层（c%d）" % (ALT_CARD_MAX_DEPTH, code))
        sub = table.get(target)
        if sub is None:
            raise ValueError("异画卡 c%d 指向的 c%d 不在脚本集里" % (code, target))
        body = expand_alternates(target, sub, table, depth + 1, stack + (code,), counter)
        counter[0] += 1
        return ("do -- >>> 内联异画卡 c%d（原脚本此处的 RD.AlternateCard(%d)）\n"
                "%s\n-- <<< 内联异画卡 c%d\nend" % (target, target, body, target))

    return ALT_CARD_RE.sub(repl, source)


def merge_cdbs(dest_path, src_paths):
    """把多份卡表合成 AI.Server / WindBot 都要的那一份 `cdb/cards.cdb`。

    为什么必须合并：AI.Server 的卡表路径是写死的 `./cdb/%s` + 字面量 "cards.cdb"
    （实测二进制里就这一段），WindBot 同理读 `../cdb/cards.cdb` —— 两边都只认**一个**
    cards.cdb，不认我们给 CardsManager 用的 rd_standard/rd_patch/rd_alternate 三件套。
    口径与 CardsManager 一致：datas 与 texts 两张表各自按 id 合，同 id 后加载者覆盖；
    建表语句直接沿用源表的那一条，不自己重写（保住 id 的 primary key = rowid 别名）。
    """
    tmp = dest_path + ".tmp"
    if os.path.exists(tmp):
        os.remove(tmp)
    con = sqlite3.connect(tmp)
    try:
        for i, p in enumerate(src_paths):
            con.execute("ATTACH DATABASE ? AS s%d" % i, (p,))
        for t in CDB_TABLES:
            row = con.execute(
                "SELECT sql FROM s0.sqlite_master WHERE type='table' AND name=?", (t,)).fetchone()
            if row is None or not row[0]:
                raise ValueError("源卡表里没有 %s 表" % t)
            con.execute(row[0])
            for i in range(len(src_paths)):
                con.execute("INSERT OR REPLACE INTO main.%s SELECT * FROM s%d.%s" % (t, i, t))
        con.commit()
        cnt = {}
        for t in CDB_TABLES:
            cnt[t] = con.execute("SELECT COUNT(*) FROM main.%s" % t).fetchone()[0]
    finally:
        con.close()
    if os.path.exists(dest_path):
        os.remove(dest_path)
    os.replace(tmp, dest_path)
    return cnt


def build_ai_package(client, src_dir, dest, force):
    """生成 rd/ai/（core + 脚本集 + 卡表 + 禁限表 + RD WindBot）与 rd/bot.conf。

    返回 None = 成功；返回字符串 = 失败原因。
    """
    ai_dir = os.path.join(dest, *AI_DIR)
    script_dir = os.path.join(dest, *AI_SCRIPT_DIR)

    # ---------- 1) 规则库与核心脚本 ----------
    lib_text = {}
    for rel in client.files_under(CLIENT_SCRIPT):
        name = os.path.basename(rel)
        if not name.lower().endswith(".lua"):
            continue
        if re.match(r"^c\d+\.lua$", name, re.I):
            continue            # 客户端脚本目录里本来就没有卡脚本；有也不取（卡脚本走 ypk）
        lib_text[name] = client.read(rel).decode("utf-8", "replace")

    missing = [n for n in RD_LIBS if n not in lib_text]
    missing += [n for n in RD_CORE_SCRIPTS if n not in lib_text]
    missing += [n for n in (CLIENT_SPECIAL,) if n not in lib_text]
    if missing:
        return "客户端脚本目录里缺这些文件：%s" % "、".join(missing)

    # 规则库**不做任何改写**，逐字铺上游原文。历史教训见本文件顶部
    #「这里曾经有一张摘掉 EFFECT_FLAG_SINGLE_RANGE 的补丁表」那段注释。

    # 规则库原样铺一份留档（便于与内联结果比对），utility/special 另写。
    n_lib = 0
    for name, text in lib_text.items():
        if name in (CLIENT_UTILITY, CLIENT_SPECIAL):
            continue
        st, n = write_file(os.path.join(script_dir, name), text.encode("utf-8"), force)
        n_lib += 1
    print("    规则库 %d 个（含 RDSkill.lua —— 原版 special.lua 并不加载它，这里保持原样不加载）"
          % n_lib)

    # ---------- 2) utility.lua：基准 + 内联规则库 + 兼容引导 ----------
    boot = open(BOOTSTRAP, "r", encoding="utf-8").read()
    inlined = []
    for name in RD_LIBS:
        # 每段都补一个换行再拼：源文件末尾未必有换行，不然会和下一段的标记粘成一行。
        body = lib_text[name]
        if not body.endswith("\n"):
            body += "\n"
        inlined.append("-- ==== inlined %s ====\n%s" % (name, body))
    if "@@RD_LIBS_HERE@@" not in boot:
        return "bootstrap.lua 里找不到 @@RD_LIBS_HERE@@ 占位符"
    # ⚠ 必须**恰好一次**：注释里再提一遍这个词，就会内联出第二份规则库 ——
    # 文件白胖 200 KB，而且那份是在 do-block 之外、随 utility.lua 一同执行的，
    # 时机与设计不符。最坑的是它「看起来也能跑」，只有数尺寸/数标记才发现得了。
    placeholder = "-- @@RD_LIBS_HERE@@"
    n_ph = boot.count(placeholder)
    if n_ph != 1:
        return ("bootstrap.lua 里 %s 出现 %d 次（必须恰好 1 次）："
                "注释里别抄这一行" % (placeholder, n_ph))
    boot = boot.replace(placeholder, "".join(inlined))

    utility = lib_text[CLIENT_UTILITY]
    if not utility.endswith("\n"):
        utility += "\n"
    utility = (utility
               + "\n-- ===== 以上是 RD 版 utility.lua 原文，以下由 unpack_rd.py 追加 =====\n"
               + boot)
    write_file(os.path.join(script_dir, CLIENT_UTILITY), utility.encode("utf-8"), force)
    print("    utility.lua：RD 原文 + 内联 %d 个规则库 + 兼容引导 → %s"
          % (len(RD_LIBS), human(len(utility.encode("utf-8")))))

    # ---------- 3) special.lua：换成桩（原文另存 .txt 留档） ----------
    stub = open(SPECIAL_STUB, "r", encoding="utf-8").read()
    write_file(os.path.join(script_dir, CLIENT_SPECIAL), stub.encode("utf-8"), force)
    write_file(os.path.join(script_dir, CLIENT_SPECIAL + ".txt"),
               lib_text[CLIENT_SPECIAL].encode("utf-8"), force)

    # ---------- 4) 卡脚本：从 ypk 抽，异画卡就地摊平 ----------
    table = collect_card_scripts(src_dir)
    if not table:
        return "三份 ypk 里一张卡脚本都没抽到（源包结构变了？）"
    n_alt = 0
    counter = [0]
    n_card = 0
    for code in sorted(table):
        try:
            src = expand_alternates(code, table[code], table, 0, (code,), counter)
        except ValueError as e:
            return "异画卡内联失败：%s" % e
        if ALT_CARD_RE.search(table[code]):
            n_alt += 1
        st, n = write_file(os.path.join(script_dir, "c%d.lua" % code),
                           src.encode("utf-8"), force)
        n_card += 1
    print("    卡脚本 %d 张（其中 %d 张是异画卡，就地摊平了 %d 处 RD.AlternateCard）"
          % (n_card, n_alt, counter[0]))
    if counter[0] == 0:
        return "一张异画卡都没摊平（异画卡包没接上？或 RD.AlternateCard 写法变了）"

    # ---------- 5) 卡表：三份合并成 core/WindBot 共读的那一份 ----------
    src_cdbs = [os.path.join(dest, "rd", "cdb", out_name) for _, _, out_name in CDBS]
    for p in src_cdbs:
        if not os.path.isfile(p):
            return "缺少 %s（先跑本脚本的阶段 1 铺卡表）" % os.path.relpath(p, REPO)
    cdb_dest = os.path.join(dest, *AI_CDB_DIR, "cards.cdb")
    if not os.path.isdir(os.path.dirname(cdb_dest)):
        os.makedirs(os.path.dirname(cdb_dest))
    cnt = merge_cdbs(cdb_dest, src_cdbs)
    print("    cdb/cards.cdb 合并完成：datas=%d 行 texts=%d 行"
          % (cnt["datas"], cnt["texts"]))
    if check_cdb(cdb_dest) <= 0:
        return "合并出来的 cards.cdb 读不出卡，SQL 结构不符"
    print("           装载校验通过：%d 张卡" % check_cdb(cdb_dest))

    # ---------- 6) 禁限表：与 rd/lflist.conf **同一份** ----------
    rd_lf = os.path.join(dest, "rd", "lflist.conf")
    if not os.path.isfile(rd_lf):
        return "缺少 rd/lflist.conf（先跑本脚本的阶段 2）"
    with open(rd_lf, "rb") as f:
        lf_data = f.read()
    write_file(os.path.join(dest, *AI_CONFIG_DIR, "lflist.conf"), lf_data, force)
    print("    config/lflist.conf ← 与 rd/lflist.conf 同源（两侧口径必须一致）")

    # ---------- 7) RD 版 WindBot ----------
    wb_files = client.files_under(CLIENT_WINBOT)
    if not wb_files:
        return "客户端包里没有 %s/ 目录" % CLIENT_WINBOT
    # files_under 给的是「相对客户端根」的名字（WindBot/xxx），这里落到 rd/ai/WindBot/ 下，
    # 所以要把 "WindBot/" 这一层切掉再拼 —— 不切会变成 rd/ai/WindBot/WindBot/...。
    wb_pref = CLIENT_WINBOT.rstrip("/") + "/"
    for rel in wb_files:
        write_file(os.path.join(dest, *AI_WINBOT_DIR, *rel[len(wb_pref):].split("/")),
                   client.read(rel), force)
    n_deck = len([x for x in wb_files if "/Decks/" in x])
    n_dialog = len([x for x in wb_files if "/Dialogs/" in x])
    print("    WindBot %d 个文件（Decks %d、Dialogs %d）" % (len(wb_files), n_deck, n_dialog))

    # ---------- 8) rd/bot.conf ----------
    write_file(os.path.join(dest, *BOT_CONF_REL), client.read(CLIENT_BOTCONF), force)
    print("    rd/bot.conf ← 客户端包内那份（%s）" % CLIENT_BOTCONF)

    # ---------- 9) AI.Server.exe：从产物根拷一份 ----------
    server_src = os.path.join(dest, AI_SERVER_SRC)
    server_dest = os.path.join(ai_dir, AI_SERVER_SRC)
    if os.path.isfile(server_src):
        with open(server_src, "rb") as f:
            write_file(server_dest, f.read(), force)
        print("    AI.Server.exe ← 产物根那份（同一个 core，必须与客户端同版本）")
    else:
        print("    ⚠ 产物根没有 %s，没拷（先构建游戏，再重跑本脚本；"
              "缺它 RD 人机起不来）" % AI_SERVER_SRC)
    return None


def main():
    ap = argparse.ArgumentParser(description="RD 数据分发（ypk → 产物 rd/ 布局）")
    ap.add_argument("--src", default=DEFAULT_SRC, help="源包目录（默认 %s）" % DEFAULT_SRC)
    ap.add_argument("--dest", default=DEFAULT_DEST, help="产物根目录（默认 %s）" % DEFAULT_DEST)
    ap.add_argument("--all", action="store_true", help="连卡图 / 立绘一起铺（约 760 MB）")
    ap.add_argument("--force", action="store_true", help="已存在的目标文件也重写")
    ap.add_argument("--list", action="store_true", help="只列源包内容，不落盘")
    ap.add_argument("--hash", action="store_true", help="顺带核源包 MD5（要读完 800 MB）")
    ap.add_argument("--no-ai", action="store_true", help="跳过 RD 人机包（rd/ai/ 与 rd/bot.conf）")
    ap.add_argument("--client", default=DEFAULT_CLIENT,
                    help="RD 客户端包（zip 或已解包目录，默认 %s）" % DEFAULT_CLIENT)
    args = ap.parse_args()

    src_dir = os.path.abspath(args.src)
    if not os.path.isdir(src_dir):
        print("!! 源目录不存在：%s" % src_dir)
        print("   （这些包是外部资料，不入 git；换了机器要先把它们拷回来，")
        print("     可核对 devtools/rd_data_layout.md 里的清单与 MD5，或用 --src 指定别处）")
        return 1

    print("源目录：%s" % src_dir)
    print("目标：  %s" % args.dest)
    print()

    # ---------- 0. 源包清点 ----------
    need = [n for n, _, _ in CDBS] + [LFLIST_ZIP] + ([CLOSEUP_ZIP] if args.all else [])
    missing = [n for n in need if not os.path.isfile(os.path.join(src_dir, n))]
    if missing:
        print("!! 缺少源包：")
        for m in missing:
            print("   - %s" % m)
        return 1
    steps = 3 if args.no_ai else 4
    print("[0/%d] 源包指纹核对" % steps)
    verify_sources(src_dir, need, args.hash)

    client = None
    if not args.no_ai:
        client_path = os.path.abspath(args.client)
        if not os.path.exists(client_path):
            print("!! RD 客户端包不存在：%s" % client_path)
            print("   （它不在 参考内容/RD/ 里，在工作区 参考内容/ 下；或用 --client 指定，")
            print("     或者用 --no-ai 明确跳过 RD 人机包）")
            return 1
        try:
            client = ClientSource(client_path)
        except (ValueError, zipfile.BadZipFile) as e:
            print("!! 读不了 RD 客户端包：%s" % e)
            return 1
        if client.iszip:
            exp = SRC_FINGERPRINT.get(CLIENT_ZIP)
            if exp:
                got = os.path.getsize(client_path)
                if got != exp[0]:
                    print("    ⚠ %s 尺寸不符：实测 %d，清单 %d" % (CLIENT_ZIP, got, exp[0]))
        print("客户端：%s" % client_path)

    if args.list:
        for src_name, member, _ in CDBS:
            with zipfile.ZipFile(os.path.join(src_dir, src_name)) as z:
                names = z.namelist()
                pics = [n for n in names if n.lower().startswith("pics/")]
                scripts = [n for n in names if n.lower().startswith("script/")]
            print("%-22s cdb=%-18s pics=%-5d script=%-5d" %
                  (src_name, member, len(pics), len(scripts)))
        with zipfile.ZipFile(os.path.join(src_dir, LFLIST_ZIP)) as z:
            print("%-22s members=%s" % (LFLIST_ZIP, z.namelist()))
        with zipfile.ZipFile(os.path.join(src_dir, CLOSEUP_ZIP)) as z:
            print("%-22s entries=%d" % (CLOSEUP_ZIP, len(z.namelist())))
        if client is not None:
            print("%-22s script=%-5d WindBot=%-5d bot.conf=%s" %
                  (CLIENT_ZIP, len(client.files_under(CLIENT_SCRIPT)),
                   len(client.files_under(CLIENT_WINBOT)), client.has(CLIENT_BOTCONF)))
        return 0

    # ---------- 1. 卡表 → rd/cdb ----------
    print("[1/%d] 卡表 → rd/cdb/" % steps)
    total = 0
    for src_name, member, out_name in CDBS:
        with zipfile.ZipFile(os.path.join(src_dir, src_name)) as z:
            data = z.read(member)
        dest_cdb = os.path.join(args.dest, "rd", "cdb", out_name)
        extract_member(dest_cdb, data, args.force)
        cnt = check_cdb(dest_cdb)
        if cnt <= 0:
            print("     !! %s 读不出卡，SQL 结构不符" % out_name)
            return 1
        print("           %s 装载校验通过：%d 张卡" % (out_name, cnt))
        total += cnt
    print("      合计 %d 张 RD 卡" % total)

    # ---------- 2. 禁限表 → rd/lflist.conf ----------
    print("[2/%d] 禁限表 → rd/lflist.conf" % steps)
    with zipfile.ZipFile(os.path.join(src_dir, LFLIST_ZIP)) as z:
        lflist = z.read(LFLIST_MEMBER)
    dest_lf = os.path.join(args.dest, "rd", "lflist.conf")
    extract_member(dest_lf, lflist, args.force)
    # 与 BanlistManager 同源的口径：! 开头的行 = 表名，后面 "id count" 成对
    sections = []
    for raw in lflist.decode("utf-8", "replace").splitlines():
        line = raw.strip()
        if line.startswith("!"):
            sections.append(line[1:])
    if not sections:
        print("     !! 禁限表里没有任何 ! 开头的表")
        return 1
    print("           表名：%s" % "、".join(sections))

    # ---------- 3. RD 人机包 ----------
    ai_note = []
    if args.no_ai:
        print("[3/%d] RD 人机包：跳过（--no-ai；卡表/禁限表已铺，人机对局用不了）" % steps)
    else:
        print("[3/%d] RD 人机包 → rd/ai/ + rd/bot.conf" % steps)
        err = build_ai_package(client, src_dir, args.dest, args.force)
        if err:
            print("     !! %s" % err)
            return 1
        ai_note.append("  %s -> rd/ai/script/ + rd/ai/WindBot/ + rd/ai/cdb/cards.cdb + rd/bot.conf\n"
                       % CLIENT_ZIP)

    # ---------- 4. 卡图 / 立绘（可选） ----------
    if args.all:
        print("[4/%d] 卡图 / 立绘 → rd/picture/（约 760 MB，慢慢等）" % steps)
        card_dir = os.path.join(args.dest, "rd", "picture", "card")
        if not os.path.isdir(card_dir):
            os.makedirs(card_dir)
        n_pic = 0
        for src_name, _, _ in CDBS:
            with zipfile.ZipFile(os.path.join(src_dir, src_name)) as z:
                for name in z.namelist():
                    if not name.lower().startswith("pics/"):
                        continue
                    base = os.path.basename(name)
                    if not base:
                        continue
                    target = os.path.join(card_dir, base)
                    if (not args.force) and os.path.exists(target):
                        continue
                    with open(target + ".tmp", "wb") as f:
                        f.write(z.read(name))
                    os.replace(target + ".tmp", target)
                    n_pic += 1
        print("      卡图 %d 张 → rd/picture/card/" % n_pic)
        ai_note.append("  %s -> rd/picture/closeup/\n" % CLOSEUP_ZIP)

        close_dir = os.path.join(args.dest, "rd", "picture", "closeup")
        if not os.path.isdir(close_dir):
            os.makedirs(close_dir)
        n_close = 0
        with zipfile.ZipFile(os.path.join(src_dir, CLOSEUP_ZIP)) as z:
            for name in z.namelist():
                if name.endswith("/"):
                    continue
                base = os.path.basename(name)
                if not base:
                    continue
                target = os.path.join(close_dir, base)
                if (not args.force) and os.path.exists(target):
                    continue
                with open(target + ".tmp", "wb") as f:
                    f.write(z.read(name))
                os.replace(target + ".tmp", target)
                n_close += 1
        print("      立绘 %d 张 → rd/picture/closeup/" % n_close)
    else:
        print("[4/%d] 卡图 / 立绘：跳过（要铺加 --all）" % steps)

    # ---------- 5. 来源自述（排查用） ----------
    with open(os.path.join(args.dest, "rd", "rd_source.txt"), "w", encoding="utf-8") as f:
        f.write("RD 数据分发自述（由 devtools/unpack_rd.py 生成）\n")
        # ⛔ 只写目录名，**不许写 src_dir 绝对路径**：这份自述会随成品包一起分发，
        #    绝对路径会暴露作者机器的目录结构（2026-09-23 发布审计抓到的实漏）。
        f.write("源目录: %s\n" % os.path.basename(os.path.normpath(src_dir)))
        for src_name, member, out_name in CDBS:
            f.write("  %s ! %s -> rd/cdb/%s\n" % (src_name, member, out_name))
        f.write("  %s ! %s -> rd/lflist.conf\n" % (LFLIST_ZIP, LFLIST_MEMBER))
        for line in ai_note:
            f.write(line)

    print()
    print("完成。rd/ 不在构建铺设名单里 ⇒ 重建 output/ 之后请重跑本脚本。")
    return 0


if __name__ == "__main__":
    sys.exit(main())
