# -*- coding: utf-8 -*-
"""RD 场地图生成器 —— 从 `texture/duel/newfield.png` 派生**一张** RD 盘面。

## 它产出什么

RD（超速决斗）一方只有 3 个怪兽区 + 3 个魔陷区，没有灵摆区、没有额外怪兽区、
没有第 1/5 列。原图（OCG 的 5 列盘面）把牌堆（卡组/墓地/场地魔法/额外卡组）
全摆在左右两侧的**外挂列**里，盘面被撑得很宽。

本脚本把牌堆**收进主区第 1/5 列**，与怪兽/魔陷排**严格对齐**，而且与主格**同一个缩放**
（落在这张图的中块，跟着整个场地的 fieldSize 一起放大）：

    texture/duel/newfield_rd.png
        我方半场（下半）          左列（原第1列）    中区 3 列（第2/3/4列）   右列（原第5列）
          近排（靠中线）            场地魔法 ⭐          怪兽区 ×3              墓地 ⚷
          远排（靠玩家）            额外卡组 🌀          魔陷区 ×3              卡组（素格）
        对手半场 = 上下镜像（对手的卡组落在我方视角的左侧）。

⛔ 曾经还有第二副摆法 `newfield_rd_small.png`（「附属格小一档」，配设置里那个
   rdPileSame_ 开关）—— **2026-09-21 已整体删除**：实测两档的牌堆格**中心逐点重合**、
   而坐标函数 `get_point_worldposition_rd` 本来也不读那个开关 ⇒ 两档卡的落点完全相同，
   差别只剩「卡相对格子的大小」（卡背 106px vs 格子 111/92px），玩家看不出区别，
   却要多背一张贴图 + 一个开关 + 一套跨档判据 ⇒ 砍掉。别再把「缩一档」引回来。

⛔ 牌堆格**别照抄原图外挂列自己的 y**（2026-09-21 的第一版就是这么做的：
   我方 场地/墓地 = 542-657、卡组/额外 = 673-788）。那套 y 属于**左右切片**（不缩放），
   而这张图的第 1/5 列画在**中块**里，中块整体乘 `fieldSize` ⇒ 同一个 y 在中块里会被
   再放大一次：屏幕上比卡低 30~70px。**卡是按世界坐标摆的，不跟着贴图这一刀走**，
   于是就成了「牌落在两个格子中间」。一句话：**中块里的格子，y 只能照中块的排来写**。
   同理，z 也别挪成 OCG 外挂格那一档（∓9 / ∓14.6）—— 见 `Ocgcore.get_point_worldposition_rd`。

## 消费方

`Assets/SibylSystem/Ocgcore/OCGobjects/gameField.cs`：
  · `NewFieldPathRD`（由 `RdFieldPath` 取；它缺了才回落 OCG 那张）；
  · `loadOldField()` / `loadNewField()` 在 RD 下都走同一张（RD 无论 MasterRule 3 还是 4+）。
坐标由 `Assets/SibylSystem/Ocgcore/Ocgcore.cs` 的 `get_point_worldposition_rd` 给，
**两边必须一起改**（贴图定「格子画在哪」、坐标定「卡往哪摆」）。

⛔ 别拿它替换 `newfield.png` / `field.png` 本身 —— OCG 模式还要用完整的那两张。

## 牌堆格必须整格搬，不能「把图标画进主区格」

第一版就是这么错的：原图上牌堆格比怪兽/魔陷格**窄** —— 92×116 vs 106×116，
而且图标是**满格**画的，92 正好等于一张卡的宽度（实测一局 OCG 里怪兽格上的卡 ≈88px、
原生牌堆格内净宽 86px）。把图标缩进 106 宽的格里 ⇒ 格子被「强行扩大」、图标反而显小。
现在改成**整格 1:1 搬运**（连边框带图标，一个像素都不缩放），只有落点变了。

## 格子几何从哪来（不是目测）

原图 999x800，用「红=对手框 / 蓝=我方框」的颜色阈值做逐行/逐列投影，聚类出边框线：

  · 主区 5 列 x：221-326 / 331-436 / 441-546 / 551-656 / 661-766
  · 四排 y（**注意：靠中线的那排是怪兽排**）：
      对手魔陷 82-197 ｜ 对手怪兽 204-319 ｜ 我方怪兽 481-596 ｜ 我方魔陷 603-718
  · 中区两个额外怪兽区空格：x 330-437 与 550-657，y 342-458
  · 左右外挂列 x：113-204 / 783-874，每列 5 格、每格 92x116，但**两列的格位不齐**
      （左列：对手 12-127/143-258/273-388，我方 542-657/673-788；
        右列：对手 12-127/143-258，我方 412-527/542-657/673-788）
      ⇒ 「哪一格是哪个区域」不是按序号猜的，是按 `get_point_worldposition`
      的世界坐标（x=∓15.2/14.65 那一列、z=14.6 卡组·额外 / 9 墓地·场地 / 3 除外）
      反算 y 对上去的。逐格抠出来核过图标：**卡组那两格是素格**（卡堆自己会显示数量），
      其余六格各带图标 —— 额外 🌀（螺旋）、场地 ⭐（圆圈里一颗四芒星）、墓地 ⚷（安卡），
      外加两个 **除外 ☒**（本图**不保留**，RD 的除外区不给格子）。

排靠中线那一侧的口径是这样定的：世界坐标里我方怪兽排 z=-5.68、魔陷排 z=-11.5，
把两条锚（怪兽排中心 y=538.5、魔陷排中心 y=660.5）与 |z| 线性拟合 ⇒ y = 419.5 + 20.96|z|，
代回外挂列的图标全部落在图上原有格子里 —— 反过来说，「482-595 那条是怪兽排」
是被这些图标钉死的，别照抄旧注释。

## 用法（在工程根执行）

    python devtools/make_rd_field.py             # 出对照预览图（源图 / RD），不写文件
    python devtools/make_rd_field.py --apply     # 写出 texture/duel/newfield_rd.png
    python devtools/make_rd_field.py --detect    # 重测边框线（换了场地图才需要）
    python devtools/make_rd_field.py --verify    # 对着磁盘上的产物逐条复核

依赖 Pillow。产物**不进 git**（`texture/` 在 .gitignore 里），
重建工作树后按上面的命令重放一次即可。
"""
import os
import sys

from PIL import Image, ImageDraw

PROJ = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SRC = os.path.join(PROJ, "texture", "duel", "newfield.png")
OUT = os.path.join(PROJ, "texture", "duel", "newfield_rd.png")
PREVIEW = os.path.join(PROJ, "devtools", "newfield_rd_preview.png")

# ---- 主区格子网格（x0, y0, x1, y1），含边框线 ---------------------------------
COL = {
    1: (221, 326),
    2: (331, 436),
    3: (441, 546),
    4: (551, 656),
    5: (661, 766),
}
ROW_OP_FAR = (82, 197)     # 对手 魔陷排（离中线远）
ROW_OP_NEAR = (204, 319)   # 对手 怪兽排（靠中线）
ROW_ME_NEAR = (481, 596)   # 我方 怪兽排（靠中线）
ROW_ME_FAR = (603, 718)    # 我方 魔陷排（离中线远）

# ---- 牌堆格：原生 92x116 ------------------------------------------------------
# 源格从原图的左右外挂列整格抠；目标格 = 主区第 1/5 列那一槽里**居中**放，
# 92 宽在 106 宽的槽里两边各留 7px ⇒ 牌堆格比怪兽格窄一截，这就是原生观感。
NATIVE_W, NATIVE_H = 92, 116
NATIVE_WH = (NATIVE_W, NATIVE_H)
PILE_INSET = (COL[1][1] - COL[1][0] + 1 - NATIVE_W) // 2      # 221..326 里居中 = 227


def _pile_dst(col, row):
    x0 = COL[col][0] + PILE_INSET
    return (x0, row[0], x0 + NATIVE_W - 1, row[0] + NATIVE_H - 1)


def _tile_wh(dbox):
    return (dbox[2] - dbox[0] + 1, dbox[3] - dbox[1] + 1)

# 每个牌子格「原图在哪一格」→「本图摆在哪」
# ⚠ 外挂列的 y 不是按排推的：两列格位本来就不齐（见文件头），这里是**逐格量出来**的。
PILE_SLOT = {
    # 我方（蓝框）
    "me_field": ((113, 542, 204, 657), _pile_dst(1, ROW_ME_NEAR)),   # ⭐ 场地魔法
    "me_extra": ((113, 673, 204, 788), _pile_dst(1, ROW_ME_FAR)),    # 🌀 额外卡组
    "me_grave": ((783, 542, 874, 657), _pile_dst(5, ROW_ME_NEAR)),   # ⚷ 墓地
    "me_deck":  ((783, 673, 874, 788), _pile_dst(5, ROW_ME_FAR)),    # 卡组（素格）
    # 对手（红框）＝ 我方上下镜像
    "op_field": ((783, 143, 874, 258), _pile_dst(5, ROW_OP_NEAR)),
    "op_extra": ((783, 12, 874, 127), _pile_dst(5, ROW_OP_FAR)),
    "op_grave": ((113, 143, 204, 258), _pile_dst(1, ROW_OP_NEAR)),
    "op_deck":  ((113, 12, 204, 127), _pile_dst(1, ROW_OP_FAR)),
}

# 外挂列里两个「除外区 ☒」格：**不保留** —— RD 的除外区不给格子
# （卡还是落在那儿，只是没有框；点开看除外堆走不可见的 gameHiddenButton）。
# 它们随外挂列一起被 OUTER_L / OUTER_R 擦掉；这里只是把坐标记下来，别再贴回去。
REMOVED_CELLS = [(113, 273, 204, 388),      # 对手除外区（左外挂）
                 (783, 412, 874, 527)]      # 我方除外区（右外挂）


# ---- 擦除区 ------------------------------------------------------------------
# 擦除框各向外扩 2~3px：检测出来的是**边框线**的位置，线本身还有宽度，
# 差 1px 就会在产物上留一条悬空的残线（第一版就是这么漏的，--verify 的
# 「区外不透明像素」那条会把它抓出来）。
# 实测线宽 ~3px：col1 边界 220-222 / 325-327，col2 边界 330-332 ⇒ 列间只有 328-329
# 两像素是空的，所以擦 col1 用 219..329（含空隙、**不含** col2 的左沿）。
OUTER_L = (105, 0, 215, 800)     # 左外挂列整列
OUTER_R = (775, 0, 895, 800)     # 右外挂列整列
EMZ = [(327, 339, 440, 461), (547, 339, 660, 461)]   # 中区两个额外怪兽区空格

# 第 1/5 列的 4 个主区格（两列各 4 排）
SLOT_COL1 = (219, 0, 330, 800)
SLOT_COL5 = (658, 0, 770, 800)

# 新盘面里**保留**的主区格（3 列 × 4 排）：怪兽/魔陷各 3 格 × 2 家
KEPT_CELLS = [(COL[c][0], r[0], COL[c][1], r[1])
              for c in (2, 3, 4)
              for r in (ROW_OP_FAR, ROW_OP_NEAR, ROW_ME_NEAR, ROW_ME_FAR)]

# 牌堆收进主区第 1/5 列 ⇒ 外挂列（含两个 ☒）整列不要，原第 1/5 列的主区格也擦掉重画
ERASE = [OUTER_L, OUTER_R] + EMZ + [SLOT_COL1, SLOT_COL5]


def build():
    """出图。图标与格线一律从**源图**取（擦除只作用于输出图）。"""
    src_im = Image.open(SRC).convert("RGBA")
    out = src_im.copy()

    # 1) 擦（外挂列 / 第 1/5 列 / 额外怪兽区空格 / 不要的除外格）
    px = out.load()
    cleared = 0
    for (x0, y0, x1, y1) in ERASE:
        for y in range(y0, y1):
            for x in range(x0, x1):
                if px[x, y][3] > 0:
                    cleared += 1
                px[x, y] = (0, 0, 0, 0)
    print("  擦除 %d 个矩形 / %d 个像素" % (len(ERASE), cleared))

    # 2) 牌堆格：整格搬（1:1、零缩放，只有落点变了）
    for key, (sbox, dbox) in PILE_SLOT.items():
        assert _tile_wh(sbox) == NATIVE_WH, "%s 源格不是原生尺寸 %s" % (key, sbox)
        out.paste(src_im.crop((sbox[0], sbox[1], sbox[2] + 1, sbox[3] + 1)),
                  (dbox[0], dbox[1]))
    print("  贴牌堆格 %d 个（全部 1:1 %s）" % (len(PILE_SLOT), NATIVE_WH))
    return out


def checker(size, step=16):
    im = Image.new("RGBA", size, (48, 52, 62, 255))
    d = ImageDraw.Draw(im)
    for y in range(0, size[1], step):
        for x in range(0, size[0], step):
            if ((x // step) + (y // step)) % 2 == 0:
                d.rectangle([x, y, x + step - 1, y + step - 1], fill=(64, 70, 82, 255))
    return im


def verify():
    """对着**磁盘产物**复核五件事：
      ① 擦除区（扣掉贴回去的牌堆格）全透明；
      ② 每个牌堆格与源图那一格**逐像素一致**（证明「整格搬、零缩放」）；
      ③ 除「擦除区 + 牌堆格 + 保留的主区格」外，其余像素与源图完全相同（没误伤）；
      ④ 产物里没有「已知区域之外的不透明像素」—— 抓「没擦干净」那一类漏
         （两个 ☒ 格没擦掉就会在这里冒出来）；
      ⑤ 八个牌堆格尺寸一致，且等于原生 92x116。"""
    name = os.path.basename(OUT)
    if not os.path.exists(OUT):
        print("!! 没有 %s —— 先跑 --apply" % OUT)
        return 1
    src = Image.open(SRC).convert("RGBA")
    rd = Image.open(OUT).convert("RGBA")
    if src.size != rd.size:
        print("!! %s 尺寸不一致 %s vs %s" % (name, src.size, rd.size))
        return 1
    sp, rp = src.load(), rd.load()
    W, H = src.size

    # ① 擦除区（扣掉贴回去的牌堆格）
    keep = set()
    for (_s, d) in PILE_SLOT.values():
        for y in range(d[1], d[3] + 1):
            for x in range(d[0], d[2] + 1):
                keep.add((x, y))
    dirty = 0
    for (x0, y0, x1, y1) in ERASE:
        for y in range(y0, y1):
            for x in range(x0, x1):
                if (x, y) in keep:
                    continue
                if rp[x, y][3] != 0:
                    dirty += 1

    # ② 牌堆格逐像素等于源图那一格
    pile_bad = 0
    for key, (sbox, dbox) in PILE_SLOT.items():
        tile = src.crop((sbox[0], sbox[1], sbox[2] + 1, sbox[3] + 1))
        tp = tile.load()
        tw, th = _tile_wh(dbox)
        for dy in range(th):
            for dx in range(tw):
                if tp[dx, dy] != rp[dbox[0] + dx, dbox[1] + dy]:
                    pile_bad += 1

    # ③ 保留格（主区 3 列）+ 牌堆格 + 擦除区 之外不得被动过
    zone = set()

    def _mark(rect):
        for y in range(max(0, rect[1] - 2), min(H, rect[3] + 2)):
            for x in range(max(0, rect[0] - 2), min(W, rect[2] + 2)):
                zone.add((x, y))

    for rect in KEPT_CELLS + [d for (_s, d) in PILE_SLOT.values()]:
        _mark(rect)
    for (x0, y0, x1, y1) in ERASE:
        for y in range(y0, y1):
            for x in range(x0, x1):
                zone.add((x, y))
    touched = 0
    for y in range(H):
        for x in range(W):
            if (x, y) in zone:
                continue
            if sp[x, y] != rp[x, y]:
                touched += 1

    # ④ 已知区域之外的不透明像素（格线本身有 2~4px 抗锯齿外沿 ⇒ 允许范围外扩 4px）
    PAD = 4
    allowed = set()

    def _add(rect):
        for y in range(max(0, rect[1] - PAD), min(H, rect[3] + PAD + 1)):
            for x in range(max(0, rect[0] - PAD), min(W, rect[2] + PAD + 1)):
                allowed.add((x, y))

    for rect in KEPT_CELLS + [d for (_s, d) in PILE_SLOT.values()]:
        _add(rect)
    stray = 0
    for y in range(H):
        for x in range(W):
            if rp[x, y][3] > 0 and (x, y) not in allowed:
                stray += 1

    # ⑤ 八个牌堆格尺寸一致，且等于原生 92x116
    sizes = set(_tile_wh(d) for (_s, d) in PILE_SLOT.values())
    wrong = 0 if sizes == {NATIVE_WH} else sum(
        1 for (_s, d) in PILE_SLOT.values() if _tile_wh(d) != NATIVE_WH)

    print("== %s ==" % name)
    print("   擦除区残留非透明像素        = %d（应 0）" % dirty)
    print("   牌堆格与源格不符的像素      = %d（应 0）" % pile_bad)
    print("   保留格/擦除区之外被改动     = %d（应 0）" % touched)
    print("   已知区域之外的不透明像素    = %d（应 0）" % stray)
    print("   牌堆格尺寸不是 %dx%d 的个数  = %d（应 0；实测 %s）"
          % (NATIVE_W, NATIVE_H, wrong, sorted(sizes)))
    ok = (dirty == 0 and pile_bad == 0 and touched == 0 and stray == 0 and wrong == 0)
    print("   结论：" + ("PASS" if ok else "FAIL"))
    print("总结论：" + ("PASS" if ok else "FAIL"))
    return 0 if ok else 1


def detect():
    """重测边框线（换了场地图才需要）。"""
    im = Image.open(SRC).convert("RGBA")
    px = im.load()
    W, H = im.size

    def is_red(c):
        r, g, b, a = c
        return a > 128 and r > 140 and g < 110 and b < 110

    def is_blue(c):
        r, g, b, a = c
        return a > 128 and b > 140 and r < 115 and g < 165

    def bands(cnt, thr):
        out, i, n = [], 0, len(cnt)
        while i < n:
            if cnt[i] >= thr:
                j = i
                while j < n and cnt[j] >= thr:
                    j += 1
                out.append((i, j - 1))
                i = j
            else:
                i += 1
        return out

    print("基准图 %s  %dx%d" % (os.path.basename(SRC), W, H))
    cr, cb = [0] * W, [0] * W
    rr, rb = [0] * H, [0] * H
    for y in range(H):
        for x in range(W):
            c = px[x, y]
            if is_red(c):
                cr[x] += 1
                rr[y] += 1
            elif is_blue(c):
                cb[x] += 1
                rb[y] += 1
    print("红 竖线(列投影) =", bands(cr, H * 0.04))
    print("蓝 竖线(列投影) =", bands(cb, H * 0.04))
    print("红 横线(行投影) =", bands(rr, W * 0.08))
    print("蓝 横线(行投影) =", bands(rb, W * 0.08))
    # 外挂列的格位（两列不齐，逐列单独投影）
    for name, (x0, x1) in (("左外挂 113-204", (113, 205)), ("右外挂 783-874", (783, 875))):
        cnt = [0] * H
        for y in range(H):
            c = 0
            for x in range(x0, x1):
                if px[x, y][3] > 0:
                    c += 1
            cnt[y] = c
        print("%s 行带 = %s" % (name, bands(cnt, 1)))
    print()
    print("对照上面代码里的常量，逐项核对；不一致就改常量，别改算法。")


def main():
    if "--detect" in sys.argv:
        detect()
        return 0
    if "--verify" in sys.argv:
        return verify()

    im = build()
    if "--apply" in sys.argv:
        im.save(OUT)
        print("写出 " + OUT)

    W, H = im.size
    panels = [("BEFORE  newfield.png (OCG 5 列 + 外挂列)",
               Image.open(SRC).convert("RGBA"), (255, 255, 255, 255))]
    right = checker((W, H))
    right.alpha_composite(im)
    d = ImageDraw.Draw(right)
    for (x0, y0, x1, y1) in ERASE:
        d.rectangle([x0, y0, x1, y1], outline=(255, 90, 90, 255), width=2)
    for (_s, dst) in PILE_SLOT.values():
        d.rectangle(list(dst), outline=(110, 230, 140, 255), width=2)
    panels.append(("AFTER  newfield_rd.png（牌堆收进第 1/5 列，与排对齐）",
                   right, (255, 200, 90, 255)))
    left = checker((W, H))
    left.alpha_composite(panels[0][1])
    panels[0] = (panels[0][0], left, panels[0][2])

    gap = 12
    canvas = Image.new("RGBA", (W * len(panels) + gap * (len(panels) - 1), H + 34),
                       (18, 20, 26, 255))
    dd = ImageDraw.Draw(canvas)
    for i, (cap, im_, col) in enumerate(panels):
        x = i * (W + gap)
        canvas.alpha_composite(im_, (x, 34))
        dd.text((x + 8, 10), cap, fill=col)
    scale = 0.62
    canvas = canvas.resize((int(canvas.width * scale), int(canvas.height * scale)),
                           Image.LANCZOS)
    canvas.convert("RGB").save(PREVIEW, quality=92)
    print("预览 " + PREVIEW)
    return 0


if __name__ == "__main__":
    sys.exit(main())
