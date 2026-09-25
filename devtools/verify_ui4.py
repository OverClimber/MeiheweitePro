# -*- coding: utf-8 -*-
"""验收「界面四项改造」（feature/rush-duel，2026-09-19）。

对应用户四条原文：
  ① 界面上有一行不该出现的字
     → 判据：`log/qt_uidump.on` 把整棵 UI 树的 UILabel 全量导出到
       `_probe_ui/uidump_*.txt`，**逐行读**找多余文案（源码/prefab/DLL 三处都搜不到，
       只能这样抓）。脚本本身只负责导出，判定由人/离线读文件做 —— 因为「该不该出现」
       是语义判断，脚本猜不了。
  ② OCG/RD 模式那几个字不与主菜单一起移动 + 配上主菜单一样的黑色底色
     → 判据 A1（[chipslide]）：每一行同时报三件东西 ——
         chip=  徽标按钮的屏幕坐标（钉住了就该恒等于 (467,113)）
         menu=  第一个可见菜单项的屏幕坐标（菜单动它就得动）
         root=  菜单根节点的世界位置 / scale=（分清「根在动」还是「只有子项在重排」）
       判据 A1a：菜单确实在动 —— 先看**帧窗内**（show() 后 60 帧逐帧落行）menu 的极差，
                 窗内全平就回退到整场会话的极差（OCG↔RD 切换的面板重排不走 show()）。
                 要 ≥40px。
       判据 A1b：徽标**全程**不动 —— 整场会话（含另一个模式的段）chip 极差 ≤2px。
       两条必须同时成立：只证明「徽标不动」分不清是钉住了还是整张菜单压根没动。
       ⚠ 实测（2026-09-19，242 行采样）：主菜单**不滑入而是跳变** —— show() 后 60 帧里
         root 恒 (0.00,0.00)/scale=0.003、menu 一动不动；唯一的位移是 OCG↔RD 切换时
         面板按项数（10/6）重新居中的那一下（menu 只有 (977,263)/(977,376) 两个取值，
         差 113px）。所以 A1a 的证据必然来自 `f=*` 的补记行，而它在 S4 之后才出现
         ⇒ A1 必须在全流程跑完时判（见 main 收尾处）。
     → 判据 A2（[rd] plate + [rd] platefit）：底板节点存在、精灵与 back 同源、depth 比标签低 1、
       且**字在底板正中** —— 四边余量都 > 0 且左右/上下各自相差 ≤2、字心与板心相差 ≤2
       （用户 2026-09-19 追加：「背景阴影与字重合度低，前面有一长部分阴影无字，
       后面又有字和阴影边缘很近」；原来底板按 180x50 的行按钮尺寸居中，而克隆来的标签
       为了让开行内图标是往右偏的 ⇒ 字贴右缘、左边空一大片）。
     → 判据 A3（[chipvis]）：徽标**全程**没有被 SoftClip 裁掉（vis/inClip 全 True）。
       A3b 是关键一条：脚本往 log/ 放一枚 qt_menushift.on 把主菜单整体推开 260 局部单位
       （≈366px），复现用户那句「**主菜单移动距离大**时还是会被隐藏」—— 推开期间徽标必须
       ①仍可见 ②屏幕落点漂移 ≤2px。
       （原来只有 FitMenuBack 照「落点常量」放宽的裁剪区 ⇒ 菜单一挪徽标就落到裁剪区外被切掉。）
  ③ RD 的「种类」筛选去掉它没有的、补上它有的
     → 判据 C（[mode] set ... typeItems=[…]）：切到 RD 那一行里，
       怪兽含「极大」「传说卡」、不含同调/超量/灵摆/连接；魔法不含速攻；
       切回 OCG 那一行里必须与改造前的**逐字一致**（老清单写死在脚本里）。
  ④ 卡组界面打开时自动选中上次那副，OCG/RD 各记各的
     → 判据 B（[mode] decklist … key=… deckInUse=[…]）：脚本先往 config.conf 里
       写两个**不同**的名字，再分别进两边的卡组列表：
       OCG 必须报 key=deckInUse      deckInUse=[<OCG 那枚>] dir=deck
       RD  必须报 key=deckInUse_rd   deckInUse=[<RD  那枚>] dir=deck_rd
       两边读到的是各自那枚 ⇒ 既证明「记住了」，也证明「没串」。

产物：_probe_ui/0N_*.png 截图 + _probe_ui/uidump_*.txt
用法：python _verify_ui4.py
"""
import ctypes
import ctypes.wintypes as wt
import os
import re
import shutil
import subprocess
import sys
import time

# ⛔ 本机绝对路径不要写死：本脚本随源码包一起分发，写死会暴露作者机器的目录结构
#   （2026-09-23 发布审计）。默认按「脚本所在仓库」推导，需要时用环境变量覆盖：
#     YGOPRO_RUN_DIR   运行目录（默认 <仓库>/output/Windows）
#     YGOPRO_PROBE_OUT 截图/导出的落点（默认 <仓库上一级>/_probe_ui）
_HERE = os.path.dirname(os.path.abspath(__file__))       # …/YGOProUnity_V2/devtools
_REPO = os.path.dirname(_HERE)                           # …/YGOProUnity_V2
RUN_DIR = os.environ.get("YGOPRO_RUN_DIR", os.path.join(_REPO, "output", "Windows"))
LOG_DIR = os.path.join(RUN_DIR, "log")
CONF = os.path.join(RUN_DIR, "config", "config.conf")
EXE = "MeiheweitePro.exe"
TITLE = "Meiheweite"
OUT = os.environ.get("YGOPRO_PROBE_OUT",
                     os.path.join(os.path.dirname(_REPO), "_probe_ui"))

WIN_WAIT = 90
WAIT = 25

WM_LBUTTONDOWN, WM_LBUTTONUP = 0x0002, 0x0004
SWP_NOMOVE, SWP_NOSIZE, SWP_SHOWWINDOW = 0x0002, 0x0001, 0x0040
SRCCOPY = 0x00CC0020
HWND_TOPMOST = wt.HWND(-1)

# config.conf 里注入的两枚「上次用的卡组」：必须是**两套目录里真实存在**的文件，
# 否则列表选不中，判据会退化成「读到了一个不存在的名字」。
OCG_DECK = "绝路的星神狱——白色幻境"
RD_DECK = "宇宙姬"

# 「主类」下拉里怪兽那一项的文字（conf 1312，两个模式都一样）。
RD_MAIN_MONSTER = "怪兽"

# 改造前 OCG 的三份种类清单（逐字抄自旧实现，见 DeckManager 的历史 getTypeFilter2）。
# 判据是「改造后 OCG 一个字都没变」—— 这张表是**故意写死的**，不从被测代码里读。
OCG_MONSTER = ["通常", "效果", "融合", "仪式", "同调", "超量", "调整", "二重",
               "同盟", "灵魂", "反转", "卡通", "灵摆", "特殊召唤", "连接"]
OCG_SPELL = ["通常", "速攻", "永续", "仪式", "装备", "场地"]
OCG_TRAP = ["通常", "永续", "反击"]
# RD 不许出现的（卡池里没有这些位）与必须出现的（RD 专有）
RD_MONSTER_BAN = ["同调", "超量", "灵摆", "连接"]
RD_MONSTER_MUST = ["极大", "传说卡"]
RD_SPELL_BAN = ["速攻"]

user32 = ctypes.windll.user32
gdi32 = ctypes.windll.gdi32
user32.SetProcessDPIAware()

os.makedirs(OUT, exist_ok=True)
T0 = time.time()
RESULTS = []


def log(m):
    print("[%6.1fs] %s" % (time.time() - T0, m), flush=True)


def need(ok, msg):
    RESULTS.append((bool(ok), msg))
    print("   %s %s" % ("OK  " if ok else "FAIL", msg), flush=True)
    return bool(ok)


# ─────────────────────────── 截图 / 窗口 ───────────────────────────

class BITMAPINFOHEADER(ctypes.Structure):
    _fields_ = [("biSize", wt.DWORD), ("biWidth", wt.LONG), ("biHeight", wt.LONG),
                ("biPlanes", wt.WORD), ("biBitCount", wt.WORD),
                ("biCompression", wt.DWORD), ("biSizeImage", wt.DWORD),
                ("biXPelsPerMeter", wt.LONG), ("biYPelsPerMeter", wt.LONG),
                ("biClrUsed", wt.DWORD), ("biClrImportant", wt.DWORD)]


class BITMAPINFO(ctypes.Structure):
    _fields_ = [("bmiHeader", BITMAPINFOHEADER), ("bmiColors", wt.DWORD * 3)]


def find_window(key):
    hits = []

    @ctypes.WINFUNCTYPE(wt.BOOL, wt.HWND, wt.LPARAM)
    def cb(hwnd, lparam):
        if not user32.IsWindowVisible(hwnd):
            return True
        n = user32.GetWindowTextLengthW(hwnd)
        if n <= 0:
            return True
        buf = ctypes.create_unicode_buffer(n + 1)
        user32.GetWindowTextW(hwnd, buf, n + 1)
        t = buf.value
        if "\\" in t or "/" in t or ":" in t:
            return True
        if key.lower() in t.lower():
            hits.append(hwnd)
        return True

    user32.EnumWindows(cb, 0)
    return hits


def client_box(hwnd):
    cr = wt.RECT()
    user32.GetClientRect(hwnd, ctypes.byref(cr))
    pt = wt.POINT(0, 0)
    user32.ClientToScreen(hwnd, ctypes.byref(pt))
    return (pt.x, pt.y, pt.x + cr.right, pt.y + cr.bottom)


def focus(hwnd):
    user32.ShowWindow(hwnd, 9)
    user32.SetForegroundWindow(hwnd)
    user32.SetWindowPos(hwnd, HWND_TOPMOST, 0, 0, 0, 0,
                        SWP_NOMOVE | SWP_NOSIZE | SWP_SHOWWINDOW)


def move_client(hwnd, cx, cy):
    pt = wt.POINT(int(cx), int(cy))
    user32.ClientToScreen(hwnd, ctypes.byref(pt))
    user32.SetCursorPos(int(pt.x), int(pt.y))


def click_client(hwnd, cx, cy, hover=0.4, settle=0.9):
    move_client(hwnd, cx, cy)
    focus(hwnd)
    time.sleep(hover)
    user32.mouse_event(WM_LBUTTONDOWN, 0, 0, 0, 0)
    time.sleep(0.08)
    user32.mouse_event(WM_LBUTTONUP, 0, 0, 0, 0)
    time.sleep(settle)


def grab_client(hwnd):
    x0, y0, x1, y1 = client_box(hwnd)
    w, h = x1 - x0, y1 - y0
    hdc = user32.GetDC(0)
    mdc = gdi32.CreateCompatibleDC(hdc)
    bmp = gdi32.CreateCompatibleBitmap(hdc, w, h)
    old = gdi32.SelectObject(mdc, bmp)
    gdi32.BitBlt(mdc, 0, 0, w, h, hdc, x0, y0, SRCCOPY)
    bmi = BITMAPINFO()
    bmi.bmiHeader.biSize = ctypes.sizeof(BITMAPINFOHEADER)
    bmi.bmiHeader.biWidth = w
    bmi.bmiHeader.biHeight = -h
    bmi.bmiHeader.biPlanes = 1
    bmi.bmiHeader.biBitCount = 32
    buf = ctypes.create_string_buffer(w * h * 4)
    gdi32.GetDIBits(mdc, bmp, 0, h, buf, ctypes.byref(bmi), 0)
    gdi32.SelectObject(mdc, old)
    gdi32.DeleteObject(bmp)
    gdi32.DeleteDC(mdc)
    user32.ReleaseDC(0, hdc)
    return buf.raw, w, h


def shot(hwnd, name):
    """截图（缺 PIL 也不许中断整场验收 —— 截图只是给人眼复核用的佐证，
    判定靠日志；早期版本就是在这里抛 ModuleNotFoundError 把后面三段全带崩了）。"""
    try:
        from PIL import Image
        data, w, h = grab_client(hwnd)
        img = Image.frombuffer("RGB", (w, h), data, "raw", "BGRX", 0, 1)
        p = os.path.join(OUT, name + ".png")
        img.save(p)
        log("   截图 %s" % os.path.basename(p))
        return p
    except BaseException as e:
        log("   !! 截图 %s 失败：%s" % (name, e))
        return None


# ─────────────────────────── 日志读取 ───────────────────────────

def read_log(p):
    try:
        with open(p, encoding="utf-8", errors="replace") as f:
            return f.read()
    except OSError:
        return ""


def lines_of(txt, tag, needle):
    pref = "[" + tag + "] "
    out = []
    for l in txt.splitlines():
        if pref in l and needle in l:
            out.append(l)
    return out


def trailing(line, key):
    """取 `key=` 之后**到行尾**的内容（含空格的值专用）。"""
    m = re.search(re.escape(key) + r"=(.*)$", line or "")
    return m.group(1).strip() if m else None


def field(line, key):
    """取 `key=token`（到空格或行尾为止）的值。"""
    m = re.search(re.escape(key) + r"=([^\s]*)", line or "")
    return m.group(1) if m else None


def screen_xy(line):
    m = re.search(r"screen=\((\d+),(\d+)\)", line or "")
    return (int(m.group(1)), int(m.group(2))) if m else None


def snapshot_logs():
    snap = {}
    for f in os.listdir(LOG_DIR):
        if f.startswith("qt_") and f.endswith(".log"):
            try:
                snap[f] = os.path.getsize(os.path.join(LOG_DIR, f))
            except OSError:
                snap[f] = -1
    return snap


def newest_log(before):
    new = [f for f in os.listdir(LOG_DIR)
           if f.startswith("qt_") and f.endswith(".log")
           and (f not in before
                or os.path.getsize(os.path.join(LOG_DIR, f)) != before.get(f))]
    if not new:
        return None
    new.sort(key=lambda f: os.path.getmtime(os.path.join(LOG_DIR, f)))
    return os.path.join(LOG_DIR, new[-1])


def safe_remove(p):
    if not os.path.exists(p):
        return True
    try:
        os.remove(p)
    except BaseException:
        pass
    if os.path.exists(p):
        try:
            os.replace(p, p + ".gone_%d" % int(time.time()))
        except BaseException:
            return False
    return not os.path.exists(p)


def write_switch(name, text=""):
    with open(os.path.join(LOG_DIR, name), "w", encoding="utf-8") as f:
        f.write(text)


def wait_line(logp, tag, needle, timeout=WAIT, hwnd=None):
    t = time.time()
    while time.time() - t < timeout:
        if lines_of(read_log(logp), tag, needle):
            return True
        if hwnd:
            focus(hwnd)
        time.sleep(0.4)
    return False


def btn_xy(logp, name, tag="btn"):
    """取 [tag] <name> … screen=(x,y) 的最后一次。"""
    last = None
    for l in lines_of(read_log(logp), tag, name):
        xy = screen_xy(l)
        if xy:
            last = xy
    return last


def wait_btn(logp, name, timeout=WAIT, hwnd=None, tag="btn"):
    t = time.time()
    while time.time() - t < timeout:
        xy = btn_xy(logp, name, tag)
        if xy:
            return xy
        if hwnd:
            focus(hwnd)
        time.sleep(0.4)
    return None


def extract_uidump(logp, out_name):
    """把 [uidump] 的整块内容（含续行）抽成一个文件 —— 续行不以时间戳开头。"""
    txt = read_log(logp)
    keep = []
    pref = re.compile(r"^\d\d:\d\d:\d\d\.\d\d\d ")
    inblock = False
    for l in txt.splitlines():
        if "[uidump]" in l:
            inblock = True
            keep.append(l)
            continue
        if inblock:
            if pref.match(l):
                inblock = False
            else:
                keep.append(l)
    p = os.path.join(OUT, out_name)
    with open(p, "w", encoding="utf-8") as f:
        f.write("\n".join(keep))
    log("   导出 %s（%d 行）" % (os.path.basename(p), len(keep)))
    return p


def uidump_items(logp, pref_needle=None):
    """取**最后一个** [uidump] 块里的 (text, (x,y), path) 列表。

    用途：NGUI 的下拉弹出来以后，每一项就是屏幕上一个普通 UILabel —— 靠全局文案探针
    直接读到它的**屏幕坐标**，脚本才能真去点它（下拉项目不报任何 [btnpos]，猜坐标必点空）。
    `pref_needle` 用来在**同名文字存在多处**时挑对那一族（例如「融合」既是种类项、
    也是效果勾选框的标签）：传节点名的片段（`second_` / `main_`），按 path 过滤。
    """
    txt = read_log(logp)
    # 从后往前找最后一个块的开头
    lines = txt.splitlines()
    start = None
    for i in range(len(lines) - 1, -1, -1):
        if "[uidump]" in lines[i]:
            start = i
            break
    if start is None:
        return []
    out = []
    for l in lines[start + 1:]:
        if TS.match(l):
            break
        # ⚠ path **可能带空格**（NGUI 的下拉项挂在 `Drop-down List` 下），
        #   所以不能用 `path=(\S+)` —— 那样整行都匹配不上，下拉项一个也读不到
        #   （第一版就是这么漏的：截图里明明列着，脚本却报「找不到怪兽」）。
        m = re.search(r"pos=\((\d+),(\d+)\) path=(.*) text=(.*)$", l)
        if not m:
            continue
        path = m.group(3)
        if pref_needle and pref_needle not in path:
            continue
        out.append((m.group(4), (int(m.group(1)), int(m.group(2))), path))
    return out


def click_popup_item(logp, hwnd, text, pref_needle, timeout=12):
    """在下拉里找到文字为 `text` 的那一项并点它。返回是否点到。

    ⚠ 先按 `pref_needle`（节点名片段）过滤，找不到再放开 —— NGUI 把下拉的项列表挂在哪个
      父节点下是实现细节，路径里不一定带得上 `main_`/`second_`；但**放开匹配有撞名的风险**
      （「融合」既是种类项、也是效果勾选框的标签），所以先精确后放宽。
    """
    t = time.time()
    relaxed = False
    while time.time() - t < timeout:
        if not relaxed and time.time() - t > timeout * 0.4:
            relaxed = True
            log("   （下拉项路径里没带 %s，放开匹配）" % pref_needle)
        for txt, xy, path in uidump_items(logp, None if relaxed else pref_needle):
            if txt.strip() == text:
                log("   下拉项「%s」在 %s → 点它" % (text, xy))
                click_client(hwnd, xy[0], xy[1], 0.35, 1.0)
                return True
        time.sleep(0.5)
    log("   !! 下拉里找不到「%s」（pref=%s）" % (text, pref_needle))
    return False


# ─────────────────────────── config.conf 注入 / 还原 ───────────────────────────

def conf_read_map():
    kv = {}
    with open(CONF, encoding="utf-8", errors="replace") as f:
        for raw in f:
            l = raw.rstrip("\r\n")
            if not l:
                continue
            i = l.find("->")
            if i <= 0:
                continue
            kv[l[:i]] = l[i + 2:]
    return kv


def conf_write_map(kv, order_hint=None):
    """整文件重写（键序沿用原文件，新键追加在末尾）。

    ⚠ 编码与换行必须与游戏写出来的一模一样：实测原文件是 **UTF-8（无 BOM）+ CRLF**，
      键值里带中文（卡组名）。改成 LF 或别的编码，游戏下次读配置就可能整份认不出来 ——
      那不是本次任务该引入的风险。
    """
    lines = []
    seen = set()
    hint = order_hint or []
    for k in hint:
        if k in kv and k not in seen:
            lines.append("%s->%s" % (k, kv[k]))
            seen.add(k)
    for k, v in kv.items():
        if k not in seen:
            lines.append("%s->%s" % (k, v))
            seen.add(k)
    with open(CONF, "w", encoding="utf-8", newline="") as f:
        f.write("\r\n".join(lines) + "\r\n")


def main():
    if not os.path.exists(CONF):
        log("XX 找不到 %s" % CONF)
        return 2

    for f in ("qt_undoreplay.on", "qt_autopass.on", "qt_autojoin.on",
              "qt_endduel.on", "qt_endduel.wait", "qt_undo.on"):
        safe_remove(os.path.join(LOG_DIR, f))
    write_switch("qt_debug.on")
    write_switch("qt_uidump.on")

    # 注入两枚**不同**的「上次用的卡组」。原来是 OCG=绝路的星神狱——白色幻境、没有 RD 那枚。
    orig = conf_read_map()
    orig_order = list(orig.keys())
    orig_had_rd_key = "deckInUse_rd" in orig
    injected = dict(orig)
    injected["deckInUse"] = OCG_DECK
    injected["deckInUse_rd"] = RD_DECK
    conf_write_map(injected, orig_order)
    log("已注入 config.conf：deckInUse=%s / deckInUse_rd=%s" % (OCG_DECK, RD_DECK))

    before = snapshot_logs()
    log("启动游戏…")
    proc = subprocess.Popen([os.path.join(RUN_DIR, EXE), "-screen-fullscreen", "0"],
                            cwd=RUN_DIR)
    hwnd = None
    try:
        t = time.time()
        while time.time() - t < WIN_WAIT:
            h = find_window(TITLE)
            if h:
                hwnd = h[0]
                break
            time.sleep(0.2)
        if hwnd is None:
            log("XX 等不到窗口")
            return 1
        focus(hwnd)
        time.sleep(4)

        logp = None
        t = time.time()
        while time.time() - t < 60:
            lp = newest_log(before)
            if lp:
                logp = lp
                break
            time.sleep(0.5)
        if logp is None:
            log("XX 没有 qt log")
            return 1
        log("轨迹 %s" % os.path.basename(logp))

        # ── S1 OCG 主菜单 ────────────────────────────────────────────
        if not wait_line(logp, "rd", "chip screen=", 40, hwnd):
            log("XX 等不到主菜单")
            return 1
        cbox = client_box(hwnd)
        click_client(hwnd, (cbox[0] + cbox[2]) / 2, cbox[1] + 40, 0.3, 0.6)  # 暖场
        time.sleep(1.0)

        # ── S1b 「主菜单移动距离大时徽标被隐藏」的复现（需求 ② 追加口径）────
        # 把主菜单整体推开 260 局部单位（≈366px）。为什么非要推开：菜单停在正位时
        # 裁剪区永远盖得住徽标（FitMenuBack 就是照落点常量放宽的），不推开就复现不出来。
        # 推开期间徽标必须①仍然可见（NGUI 自己判）②屏幕落点不变（它不该跟着菜单走）。
        chip_before = btn_xy(logp, "chip screen=", tag="rd")
        log("   徽标基线 %s；把主菜单推开 (260,60) 局部单位…" % (chip_before,))
        write_switch("qt_menushift.on", "260,60")
        time.sleep(2.5)
        shot(hwnd, "12_menu_shifted")
        check_chipvis_shift(logp, chip_before)
        safe_remove(os.path.join(LOG_DIR, "qt_menushift.on"))
        time.sleep(2.0)
        log("   菜单已归位，徽标落点 %s" % (btn_xy(logp, "chip screen=", tag="rd"),))

        check_plate(logp, "OCG")
        shot(hwnd, "01_menu_ocg")
        extract_uidump(logp, "uidump_ocg_menu.txt")
        # ⚠ check_chipslide("OCG") **不能**放这里：A1a 要的「菜单在动」在 OCG 半边
        #   唯一的正面证据是 OCG↔RD 切换时的面板跳变（113px），而那一刻要到 S4/S7 之后
        #   才落进日志。放在这里只会看到「启动窗 60 帧全平」⇒ 假红。留到收尾统一判。

        # ── S2 OCG 卡组列表（需求 4 的 OCG 半边）────────────────────
        xy = wait_btn(logp, "deck_")
        log("   点「编辑卡组」 %s" % (xy,))
        if xy:
            click_client(hwnd, xy[0], xy[1], 0.5, 2.5)
        time.sleep(1.5)
        check_decklist(logp, "OCG", "deck", "deckInUse", OCG_DECK)
        shot(hwnd, "02_decklist_ocg")
        extract_uidump(logp, "uidump_ocg_decklist.txt")

        # ── S3 OCG 编辑器 + 高级搜索 ─────────────────────────────────
        # ⚠ 卡组列表的「编辑」按钮报的是 `[btnpos] selectdeck edit_`（selectDeck 自己的探针），
        #   而 `editor xxx` 是编辑器工具条那一批 —— 两者不是同一个命名空间，别混用。
        ed = wait_btn(logp, "selectdeck edit_", tag="btnpos")
        log("   点「编辑」 %s" % (ed,))
        if ed:
            click_client(hwnd, ed[0], ed[1], 0.5, 2.5)
            time.sleep(2.0)
            if wait_line(logp, "btnpos", "editor detailed_", 20, hwnd):
                det = btn_xy(logp, "editor detailed_", tag="btnpos")
                log("   点「高级搜索」 %s" % (det,))
                if det:
                    click_client(hwnd, det[0], det[1], 0.5, 1.5)
                    shot(hwnd, "03_search_ocg")
            # 回卡组列表 → 回主菜单（点 home_，落点由 [btnpos] 报）
            hm = wait_btn(logp, "editor home_", tag="btnpos")
            log("   点「返回」 %s" % (hm,))
            if hm:
                click_client(hwnd, hm[0], hm[1], 0.5, 2.5)
        time.sleep(1.0)
        ex = btn_xy(logp, "selectdeck exit_", tag="btnpos")
        if ex:
            click_client(hwnd, ex[0], ex[1], 0.5, 2.5)
        time.sleep(1.0)

        # ── S4 切 RD（拿 typeItems 的第一份）────────────────────────
        # ⚠ 这里**不判** chipslide：切换模式走的是 RefreshRdUi（重排菜单项），
        #   不像 show() 那样把整张窗口从屏幕下方推上来，压根没有「滑入」这回事。
        #   RD 的滑入采样留到 S7 —— 从卡组列表退回主菜单时还在 RD，那一次才是真的滑入。
        chip = wait_btn(logp, "chip screen=", tag="rd")
        log("   点 RD 入口 %s" % (chip,))
        if chip:
            click_client(hwnd, chip[0], chip[1], 0.6, 2.0)
        if not wait_line(logp, "mode", "set -> RD", 20, hwnd):
            log("XX 没切到 RD")
        time.sleep(1.2)
        check_type_items(logp, "RD")
        shot(hwnd, "05_menu_rd")
        extract_uidump(logp, "uidump_rd_menu.txt")

        # ── S5 RD 卡组列表（需求 4 的 RD 半边）──────────────────────
        xy = wait_btn(logp, "deck_", timeout=15)
        log("   点「编辑卡组」 %s" % (xy,))
        if xy:
            click_client(hwnd, xy[0], xy[1], 0.5, 2.5)
        time.sleep(1.5)
        check_decklist(logp, "RD", "deck_rd", "deckInUse_rd", RD_DECK)
        shot(hwnd, "06_decklist_rd")
        extract_uidump(logp, "uidump_rd_decklist.txt")

        # ── S6 RD 编辑器：把「种类」下拉拍下来人眼复核 ───────────────
        ed = wait_btn(logp, "selectdeck edit_", tag="btnpos")
        if ed:
            click_client(hwnd, ed[0], ed[1], 0.5, 2.5)
            time.sleep(2.0)
            det = wait_btn(logp, "editor detailed_", tag="btnpos")
            if det:
                click_client(hwnd, det[0], det[1], 0.5, 1.5)
                shot(hwnd, "07_search_rd")
                m = btn_xy(logp, "editor main_", tag="btnpos")
                if m:
                    click_client(hwnd, m[0], m[1], 0.6, 1.5)
                    shot(hwnd, "08_search_rd_mainopen")
                    # 选「怪兽」—— 下拉项不报任何坐标，靠全局文案探针反查屏幕落点
                    # （见 uidump_items / click_popup_item）。
                    if click_popup_item(logp, hwnd, RD_MAIN_MONSTER, "Drop-down"):
                        time.sleep(1.2)
                        s = btn_xy(logp, "editor second_", tag="btnpos")
                        if s:
                            log("   点「种类」下拉 %s" % (s,))
                            click_client(hwnd, s[0], s[1], 0.6, 1.5)
                            shot(hwnd, "09_search_rd_second_open")
                            items = [t for t, _xy, _p in uidump_items(logp, "Drop-down")]
                            if not items:
                                items = [t for t, _xy, _p in uidump_items(logp)]
                            log("   种类下拉实际列出 %d 项：%s" % (len(items), items))
                            # 关掉下拉，免得吃掉后面那一下「返回」
                            click_client(hwnd, s[0], s[1], 0.4, 1.0)

        # ── S7 退回 RD 主菜单（RD 的滑入采样在这一段）→ 再切回 OCG ──
        hm = wait_btn(logp, "editor home_", tag="btnpos", timeout=15)
        if hm:
            click_client(hwnd, hm[0], hm[1], 0.5, 2.5)
        time.sleep(1.0)
        ex = btn_xy(logp, "selectdeck exit_", tag="btnpos")
        if ex:
            click_client(hwnd, ex[0], ex[1], 0.5, 2.5)
        time.sleep(1.8)
        # 这一刻主菜单是在 RD 下 show() 出来的 ⇒ 有 RD 的滑入采样与 chipdiag。
        check_chipslide(logp, "RD")
        check_plate(logp, "RD")

        chip = wait_btn(logp, "chip screen=", tag="rd", timeout=20)
        if chip:
            click_client(hwnd, chip[0], chip[1], 0.6, 2.0)
        if wait_line(logp, "mode", "set -> OCG", 20, hwnd):
            check_type_items(logp, "OCG")
        else:
            need(False, "C-OCG 没能切回 OCG（拿不到改造后的 OCG 种类清单）")
        # 此时 OCG/RD 两张菜单截图与三次 [bg] 探针行都已落齐，判背景双向切换。
        check_bg(logp)

        # ── S8a 超先行卡列表窗（需求 ① 取证）────────────────────────
        # 这是**唯一一个手搭出来的窗口**（SuperPreList 克隆主菜单项拼 header、状态行、
        # 下载/返回按钮），最容易漏出多余文案 —— 源码里搜不到就只能靠运行时全量导出。
        # 它自带 [btn] superPreExit 探针，所以进得去也出得来，放在收尾段不用怕卡住。
        sp = btn_xy(logp, "superPre_", tag="btn")
        if sp:
            log("   点「超先行卡」 %s" % (sp,))
            click_client(hwnd, sp[0], sp[1], 0.5, 2.5)
            if wait_line(logp, "vis", "super_pre_list -> true", 20, hwnd):
                time.sleep(2.0)
                shot(hwnd, "11_superpre")
                extract_uidump(logp, "uidump_superpre.txt")
                bx = wait_btn(logp, "superPreExit", tag="btn", timeout=15)
                log("   点「返回」 %s" % (bx,))
                if bx:
                    click_client(hwnd, bx[0], bx[1], 0.5, 2.0)
                    if not wait_line(logp, "vis", "super_pre_list -> false", 15, hwnd):
                        log("   !! 超先行卡窗没关上（后续 S8b 可能落空）")
            else:
                log("   !! 超先行卡窗没起来")
        time.sleep(0.8)

        # ── S8 系统设置窗（需求 ① 取证）：唯一还没覆盖的主菜单分支 ────────
        # 放在**最后**：进设置窗要去点它自己的 exit_ 才出得来，而那个坐标没有探针；
        # 反正全局文案探针每帧都在跑，窗口一开就会被导出，收尾直接结束进程即可。
        st = btn_xy(logp, "setting_", tag="btn")
        if st:
            log("   点「系统设置」 %s" % (st,))
            click_client(hwnd, st[0], st[1], 0.5, 2.5)
            shot(hwnd, "10_setting")
            time.sleep(1.0)

        # 需求 ① 的取证：uidump 是「变了才写」，日志里累积了本次会话**每一个界面**的
        # 全量文案块 —— 一次导出即覆盖菜单/卡组列表/编辑器/搜索面板/超先行卡/设置窗。
        extract_uidump(logp, "uidump_all.txt")

        # ── A1 统一在收尾判（两种模式用同一份完整证据）──────────────────
        # 为什么放最后：实测（2026-09-19，242 行采样）主菜单**根本不会滑入** ——
        # show() 之后的 60 帧里 root 恒为 (0.00,0.00)/scale=0.003、menu 一动不动；
        # 菜单真正的位移只有 OCG↔RD 切换那一下的**离散跳变**（menu 只有 (977,263) 与
        # (977,376) 两个取值，差 113px，正好对应两个模式的项数 10/6 重新居中）。
        # 那两条证据是 `f=*` 的补记行，在 S4/S7 之后才存在 ⇒ 必须等全流程跑完再判，
        # 否则 OCG 半边只看得到启动窗，A1a 必然假红。
        check_chipslide(logp, "OCG")
        # A3 也放收尾：chipvis 是**每帧对账**（变化才落行），全场累积下来的那份才作数 ——
        # 单独看某一段，判不出「有没有哪一帧被裁掉」。
        check_chipvis(logp, "全场")
        return 0
    finally:
        try:
            proc.kill()
        except BaseException:
            pass
        try:
            proc.wait(timeout=10)
        except BaseException:
            pass
        safe_remove(os.path.join(LOG_DIR, "qt_debug.on"))
        safe_remove(os.path.join(LOG_DIR, "qt_uidump.on"))
        safe_remove(os.path.join(LOG_DIR, "qt_menushift.on"))
        # 还原 config.conf：把 deckInUse 放回原值，删掉本次注入的 deckInUse_rd。
        # ⚠ 只动这两枚键，其余（窗口位置等）保留游戏自己写下的值 —— 整体回灌备份
        #   会把这一局里游戏合法更新的窗口坐标一起抹掉。
        try:
            cur = conf_read_map()
            if "deckInUse" in orig:
                cur["deckInUse"] = orig["deckInUse"]
            else:
                cur.pop("deckInUse", None)
            if not orig_had_rd_key:
                cur.pop("deckInUse_rd", None)
            else:
                cur["deckInUse_rd"] = orig["deckInUse_rd"]
            conf_write_map(cur, orig_order)
            log("已还原 config.conf（deckInUse 回到原值）")
        except BaseException as e:
            log("!! config.conf 还原失败：%s" % e)
        report()


# ─────────────────────────── 判据 ───────────────────────────

TS = re.compile(r"^(\d\d):(\d\d):(\d\d)\.(\d\d\d) ")


def ts_of(line):
    """日志行首的时间戳 → 当日秒数（配对「同一时间窗」用）。"""
    m = TS.match(line or "")
    if not m:
        return None
    h, mi, s, ms = (int(x) for x in m.groups())
    return h * 3600 + mi * 60 + s + ms / 1000.0


def check_bg(logp):
    """D：RD 用独立背景图（texture/common/desk_rd.jpg），OCG 保持原背景。

    证据两路：
    · [bg] 探针行：init 时落一张（应为 OCG 原图），切 RD / 切回各落一张（path 要跟着换）。
    · 像素：01_menu_ocg.png 与 05_menu_rd.png 的全图平均差要显著大于「同背景」基线
      （2026-09-19 实测同背景基线 mean=4.91 ⇒ 阈值取 12，既排除 UI 重排噪声、
      又不至于让一张几乎全黑的图蒙混过关）。
    """
    ls = lines_of(read_log(logp), "bg", "tex=")
    if not ls:
        need(False, "D 一条 [bg] 探针都没有（背景模块没跑？）")
        return
    init = [l for l in ls if "why=init" in l]
    rd = [l for l in ls if "mode=RD" in l and "why=changed" in l]
    ocg = [l for l in ls if "mode=OCG" in l and "why=changed" in l]
    need(init and "path=texture/common/desk.jpg" in init[-1],
         "D1 启动时铺 OCG 原图（%s）" % (init[-1].split("tex=")[-1].split()[0] if init else "无",))
    need(bool(rd) and "path=texture/common/desk_rd.jpg" in rd[-1],
         "D2 切 RD 换上独立背景 desk_rd.jpg")
    need(bool(ocg) and "path=texture/common/desk.jpg" in ocg[-1],
         "D3 切回 OCG 换回原图（双向都换，不是单程）")

    pa = os.path.join(OUT, "01_menu_ocg.png")
    pb = os.path.join(OUT, "05_menu_rd.png")
    if not (os.path.exists(pa) and os.path.exists(pb)):
        need(False, "D4 缺截图，像素比对跳过")
        return
    try:
        from PIL import Image, ImageChops
        import numpy as np
        a = Image.open(pa).convert("RGB").resize((240, 135))
        b = Image.open(pb).convert("RGB").resize((240, 135))
        diff = np.asarray(ImageChops.difference(a, b), dtype=np.float32).mean()
        need(diff >= 12,
             "D4 🔑 肉眼级证据：OCG/RD 两张菜单截图全图平均差 %.1f（同背景基线 ~4.9，要 ≥12）"
             % diff)
    except ImportError:
        log("   （无 PIL/numpy，D4 像素比对跳过）")


def check_chipslide(logp, tag):
    """A1：菜单在动、而徽标不动。

    探针（`Menu.TraceChipSlide`）每行同时报三件东西：
      chip=  徽标按钮的屏幕坐标
      menu=  第一个可见菜单项的屏幕坐标
      root=  菜单根节点的世界位置 / scale=
    参照物是 `menu=`、**不是**菜单根节点：实测（2026-09-19）进场时根节点的位置本来就在
    落点上（量出来是「菜单没动」），拿子项才量得到肉眼看到的那个位移。

    A1a 实质上是「全场」判据：实测主菜单**不滑入**（帧窗 60 行里 menu 一动不动），
       唯一的位移是 OCG↔RD 切换时面板按项数（10/6）重新居中的那一下 —— menu 只有
       (977,263)/(977,376) 两个取值、差 113px，落在 `f=*` 的补记行里。
       （用户原话就是「ocg模式和rd模式的字不和主菜单一起移动」，重排正是那个场景。）
       「帧窗」那一档仍然先算、并打进日志：万一将来改成真的滑入，它会直接量到轨迹。
    A1b 用整场会话（两个模式的段合起来）判：徽标本来就该在任何一次移动里都不动。
    """
    txt = read_log(logp)
    all_ls = lines_of(txt, "chipslide", "chip=")
    ls = [l for l in all_ls if ("mode=" + tag) in l]
    if not ls:
        need(False, "%s A1 没有本模式的滑入采样（找不到 [chipslide] mode=%s）" % (tag, tag))
        return

    def range_of(lines, key):
        vals = []
        for l in lines:
            m = re.search(key + r"=\((-?\d+),(-?\d+)\)", l)
            if m:
                vals.append((int(m.group(1)), int(m.group(2))))
        if not vals:
            return None, []
        return (max(v[0] for v in vals) - min(v[0] for v in vals),
                max(v[1] for v in vals) - min(v[1] for v in vals)), vals

    # 帧窗内 = 行里带 `f=<数字>`（窗外补记的行是 `f=*`）
    win = [l for l in ls if re.search(r" f=\d+ ", l)]
    need(len(win) >= 30,
         "%s A1c 帧窗采样 %d 行（要 ≥30：show() 后 60 帧逐帧落行，覆盖整段滑入）" % (tag, len(win)))

    wr, _ = range_of(win, "menu")
    src = "帧窗"
    mr = wr
    if mr is None or max(mr) < 40:
        gr, _ = range_of(all_ls, "menu")
        if gr is not None and (mr is None or max(gr) > max(mr)):
            src, mr = "全场（含 OCG\u2194RD 重排）", gr
    if mr is None:
        need(False, "%s A1a 采样里读不到 menu 坐标" % tag)
    else:
        need(max(mr) >= 40,
             "%s A1a 菜单确实在动：参照菜单项屏幕位移极差 (%d,%d)px（要 ≥40，来源=%s）"
             % (tag, mr[0], mr[1], src))

    cr, cvals = range_of(all_ls, "chip")
    if cr is None:
        need(False, "%s A1b 采样里读不到 chip 坐标" % tag)
    else:
        need(max(cr) <= 2,
             "%s A1b 🔑 徽标不与菜单一起移动：chip 极差 (%d,%d)px，要 ≤2 [%s→%s]"
             % (tag, cr[0], cr[1], cvals[0], cvals[-1]))
    log("       本模式 %d 行（帧窗 %d 行）；全场 %d 行；帧窗 menu 极差 %s / 全场 chip 极差 %s"
        % (len(ls), len(win), len(all_ls), wr, cr))


def check_plate(logp, tag):
    """A2：底板存在、与 back 同源、层序正确，且**与文字同心**。

    ⚠ 尺寸判据 2026-09-19 改过：原来判 `wh=180x50`（= 行按钮的尺寸），而用户当场追加了
      「背景阴影与字重合度低，前面有一长部分阴影无字，后面又有字和阴影边缘很近」——
      问题的根就在这个 180x50：底板按按钮居中，而克隆来的标签为了让开行内图标是**往右偏**的
      （实测字贴底板右缘、左边空 ≈60px）。现在底板 = 文字 + 四边等宽留白，所以判据从
      「等于某个固定尺寸」改成「文字在底板正中」（A2e/A2f，读 [rd] platefit）。
    """
    txt = read_log(logp)
    ls = lines_of(txt, "rd", "plate node=")
    if not ls:
        need(False, "%s A2 没有底板（[rd] plate node= 不存在）" % tag)
        return
    l = ls[-1]
    sprite = field(l, "sprite")
    depth = field(l, "depth")
    lb_depth = field(l, "labelDepth")
    need(sprite == "darkTransparented",
         "%s A2a 底板与主菜单 back 同一精灵（sprite=%s）" % (tag, sprite))
    try:
        need(int(lb_depth) == int(depth) + 1,
             "%s A2c 标签压在底板之上（labelDepth=%s, depth=%s）" % (tag, lb_depth, depth))
    except (TypeError, ValueError):
        need(False, "%s A2c depth 读不出来（%s / %s）" % (tag, lb_depth, depth))
    need("visible=True" in l, "%s A2d 底板可见（%s）" % (tag, field(l, "visible")))

    fl = lines_of(txt, "rd", "platefit ")
    if not fl:
        need(False, "%s A2e 没有 platefit 行（拿不到「底板 vs 文字」的余量）" % tag)
        return
    f = fl[-1]
    m = re.search(r"margin=\(左(-?\d+),右(-?\d+),下(-?\d+),上(-?\d+)\)", f)
    o = re.search(r"offset=\(字心-板心(-?\d+),(-?\d+)\)", f)
    if not m:
        need(False, "%s A2e platefit 解析失败：%s" % (tag, f[:160]))
        return
    left, right, bottom, top = (int(x) for x in m.groups())
    need(min(left, right, bottom, top) > 0,
         "%s A2b 底板盖住文字：四边余量 (左%d,右%d,下%d,上%d) 必须都 > 0"
         % (tag, left, right, bottom, top))
    need(abs(left - right) <= 2 and abs(bottom - top) <= 2,
         "%s A2e 🔑 字在底板正中：左右余量 %d/%d、上下余量 %d/%d（各差 ≤2）"
         % (tag, left, right, bottom, top))
    if o:
        need(abs(int(o.group(1))) <= 2 and abs(int(o.group(2))) <= 2,
             "%s A2f 字心与板心重合：offset=(%s,%s)（各 ≤2）"
             % (tag, o.group(1), o.group(2)))
        log("       余量=(左%d,右%d,下%d,上%d) 字心-板心=(%s,%s)"
            % (left, right, bottom, top, o.group(1), o.group(2)))
    else:
        log("       !! platefit 里没有 offset 字段：%s" % f[:140])


def check_chipvis(logp, tag):
    """A3：徽标**全程**没有被 SoftClip 裁掉。

    数据源 [chipvis]：`vis=` 是 NGUI 自己的判决（UIPanel.IsVisible(widget)，裁剪就是它说了算），
    `inClip=` 是按几何独立算的同一个结论 —— 两者必须一致且都为 True。
    ⚠ 只看裁剪这一路：面板整体淡出（panelKIller 把 panel.alpha 拉到 0）不算「被裁」，
      它以 `[vis] trans_menu -> false` 出现，属于窗口正常关闭。
    """
    ls = lines_of(read_log(logp), "chipvis", "vis=")
    if not ls:
        need(False, "%s A3 没有 chipvis 行（读不到徽标的可见性）" % tag)
        return
    bad = [l for l in ls if "vis=False" in l or "inClip=False" in l]
    need(not bad,
         "%s A3 🔑 徽标全程可见（%d 行里有 %d 行被裁）\n      %s"
         % (tag, len(ls), len(bad), (bad[0].strip()[:150] if bad else "")))
    log("       chipvis %d 行，全部 vis=True/inClip=True" % len(ls))


def check_chipvis_shift(logp, chip_before):
    """A3b 🔑「主菜单移动距离大时徽标被隐藏」的复现判据（需求 ② 的核心口径）。

    做法：验收脚本往 log/ 里放一枚 qt_menushift.on（内容 `dx,dy`），游戏把主菜单整体推开
    这么远 —— 这正是用户报的场景（菜单挪开，徽标应当**留在原地**）。
    两条要求同时成立：
      · 推开期间徽标**仍然可见**（[chipvis] vis/inClip 全 True）—— 被 SoftClip 切掉就是这条红；
      · 推开期间徽标的**屏幕落点不变**（与推开前那一次相比 ≤2px）—— 它不该跟着菜单走。
    """
    ls = lines_of(read_log(logp), "chipvis", "shift=(")
    if not ls:
        need(False, "A3b 没有 chipvis 行（读不到推距）")
        return
    shifted = [l for l in ls if not l.rstrip().endswith("shift=(0,0)")]
    if not shifted:
        need(False, "A3b 没采到「主菜单被推开」之后的 chipvis 行（推开没生效？）")
        return
    bad = [l for l in shifted if "vis=False" in l or "inClip=False" in l]
    need(not bad,
         "A3b-1 🔑 主菜单被推开后徽标仍可见（%d 行里 %d 行被裁）\n      %s"
         % (len(shifted), len(bad), (bad[0].strip()[:150] if bad else "")))
    pts = []
    for l in shifted:
        m = re.search(r"chip=\((\d+),(\d+)\)", l)
        if m:
            pts.append((int(m.group(1)), int(m.group(2))))
    if not pts:
        need(False, "A3b-2 推开期间读不到徽标屏幕坐标")
        return
    if chip_before is None:
        log("       !! 没有推开前的落点可比（%d 个点）" % len(pts))
        return
    dx = max(abs(p[0] - chip_before[0]) for p in pts)
    dy = max(abs(p[1] - chip_before[1]) for p in pts)
    need(dx <= 2 and dy <= 2,
         "A3b-2 🔑 菜单被推开、徽标**不跟着走**：落点漂移 (%d,%d)px（要 ≤2）\n"
         "      推开前 %s；推开后采样 %s" % (dx, dy, chip_before, pts[:4]))
    log("       菜单推开期间采 %d 行；徽标落点 %s（漂移 %d,%dpx）"
        % (len(shifted), pts[0], dx, dy))


def check_type_items(logp, tag):
    """C：种类清单按卡池收敛。"""
    ls = [l for l in lines_of(read_log(logp), "mode", "typeItems=[")
          if ("set -> " + tag) in l]
    if not ls:
        need(False, "%s C 没有 typeItems 行" % tag)
        return
    l = ls[-1]
    # ⚠ 必须**贪婪到行尾**：同一行里 raceNames=[…]、rdRaceHits=… 也带方括号，
    #   非贪婪会在「怪兽[…]」那个右括号就停，只拿到三份清单里的第一份。
    #   typeItems=[…] 是这行的最后一个字段（见 GameModeManager.RdRaceProbe 的拼法）。
    m = re.search(r"typeItems=\[(.*)\]", l)
    if not m:
        need(False, "%s C typeItems 解析失败：%s" % (tag, l[:160]))
        return
    raw = m.group(1)
    groups = {}
    for part in re.finditer(r"(怪兽|魔法|陷阱)\[([^\]]*)\]", raw):
        groups[part.group(1)] = [x for x in part.group(2).split("|") if x]
    log("       %s typeItems=%s" % (tag, raw))
    if tag == "RD":
        mon = groups.get("怪兽", [])
        sp = groups.get("魔法", [])
        for bad in RD_MONSTER_BAN:
            need(bad not in mon, "RD C1 怪兽不含「%s」" % bad)
        for must in RD_MONSTER_MUST:
            need(must in mon, "RD C2 🔑 怪兽含「%s」（RD 专有）" % must)
        for bad in RD_SPELL_BAN:
            need(bad not in sp, "RD C3 魔法不含「%s」" % bad)
        need(len(mon) >= 5, "RD C4 怪兽档还剩 %d 项（不是被削空）" % len(mon))
    else:
        need(groups.get("怪兽") == OCG_MONSTER,
             "OCG C5 🔑 怪兽清单与改造前逐字一致\n      现在=%s\n      应为=%s"
             % (groups.get("怪兽"), OCG_MONSTER))
        need(groups.get("魔法") == OCG_SPELL,
             "OCG C6 🔑 魔法清单与改造前逐字一致（现在=%s）" % (groups.get("魔法"),))
        need(groups.get("陷阱") == OCG_TRAP,
             "OCG C7 🔑 陷阱清单与改造前逐字一致（现在=%s）" % (groups.get("陷阱"),))


def check_decklist(logp, tag, expect_dir, expect_key, expect_name):
    """B：卡组列表读到的是本模式那枚键、且值 = 脚本注入的名字。"""
    ls = [l for l in lines_of(read_log(logp), "mode", "decklist dir=")
          if ("mode=" + tag) in l]
    if not ls:
        need(False, "%s B 没有 decklist 行" % tag)
        return
    l = ls[-1]
    log("       %s %s" % (tag, l.strip()))
    need(field(l, "dir") == expect_dir,
         "%s B1 读的是 %s（dir=%s）" % (tag, expect_dir, field(l, "dir")))
    need(field(l, "key") == expect_key,
         "%s B2 🔑 用的是本模式那枚键（key=%s，应为 %s）"
         % (tag, field(l, "key"), expect_key))
    got = trailing(l, "deckInUse")
    need(got == "[" + expect_name + "]",
         "%s B3 🔑 打开时自动选中上次那副（deckInUse=%s，应为 [%s]）"
         % (tag, got, expect_name))


def report():
    bad = [m for ok, m in RESULTS if not ok]
    print("\n================ 汇总 ================", flush=True)
    print("共 %d 条，通过 %d，失败 %d" % (len(RESULTS), len(RESULTS) - len(bad), len(bad)))
    for m in bad:
        print("  FAIL %s" % m)
    print("产物目录：%s" % OUT, flush=True)


if __name__ == "__main__":
    sys.exit(main())
