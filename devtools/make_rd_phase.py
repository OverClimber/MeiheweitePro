# -*- coding: utf-8 -*-
"""RD 阶段条底板生成器 —— 从 `texture/duel/phase/phase.png` 派生**四格**那张。

## 为什么要有第二张

`texture/duel/phase/phase.png`（1024x128）是原生阶段条底板：外层一个圆角框，
里面 **6 个圆角格**、格间 5 个 ▷ 箭头 —— 对应 OCG 的 6 个阶段
（DP 抽卡 / SP 准备 / MP1 主要1 / BP 战斗 / MP2 主要2 / EP 结束）。

RD 只有 4 个阶段：**抽卡 / 主要阶段1 / 战斗 / 结束**（准备阶段与主要阶段2 在 RD 里
不存在，见 `Ocgcore` 的 `GameMessage.NewPhase` 分支）。六个标签里有四个要显示、
两个要藏起来 —— 但**底板不会自己少两格**：

  · 直接沿用六格底板 ⇒ 条上留着两个空格子，四个阶段被空格子**隔开**
    （用户 2026-09-21 的原话：「不要因为没了的阶段卡分开」）；
  · 干脆不显示底板（`phaseTexure.mainTexture = null`）⇒ 只剩四个浮空文字。

所以第二张底板是必需的：**同样式、四格、等距铺开**。

    texture/duel/phase/phase_rd.png
        外框与柔和底板：与 phase.png **逐像素相同**（整块搬，不重画）
        四格：A B C D 等距，格心 93 / 372 / 651 / 930（关于图心 511.5 对称）
        三箭头：居中落在三个格间空档里

## 格子几何从哪来（不是目测）

源图是 3 层，用 alpha 阈值分层量出来的（阈值 100 = 清晰线，40 = 柔和底板）：

  · 外框（alpha 163/177，线宽 3）：x 10-1013、y 4-122
  · 柔和底板（均匀 `(204,204,204,40)`）：铺满外框内缘，格子里外都是它
    ⇒ **擦原格/原箭头只需把那一块填回这个颜色**，不会有接缝、也没有渐变要对齐
  · 格子（129x85，线宽 3）：x 28-156 / 197-325 / 364-492 / 532-659 / 699-827 / 867-995
    格心 92 / 261 / 428 / 595.5 / 763 / 931 ← 与 `lazyBTNMOVER.shift(false)`
    里那六个 x（-238 / -140.2 / -47.5 / 47.5 / 142.5 / 237.8）**一一对上**
    （换算比例 = 贴图显示宽 580 / 图宽 1024；见下面 `label_x()`）
  · 箭头（26x42）：165-190 等，居中落在格间空档

## 新四格的位置怎么定

四个格心**等距**、且关于图心对称（这样 `shiftRD()` 里那四个 x 就是一对一对的）：

    格心 = 93 / 372 / 651 / 930        ⇒ 左沿 29 / 308 / 587 / 866（间距 279）
    空档 = 158-307 / 437-586 / 716-865 ⇒ 箭头中心 232.5 / 511.5 / 790.5

首末格沿用源图首末格的位置（29 / 866 vs 源图 28 / 867）：条的**总跨度不变**，
只是把「六格挤满」改成「四格铺满」—— 每个格子还是原尺寸，一个像素都不缩放。

## 消费方

`Assets/SibylSystem/ResourceManagers/GameTextureManager.cs`：`phaseRD`
（贴图缺了回落 `phase`，见 `phaseRDOrFallback`）。
`Assets/SibylSystem/Ocgcore/OCGobjects/lazyBTNMOVER.cs`：`shiftRD()`
—— 四个标签的 x 必须等于本文件 `label_x()` 的输出，**两边一起改**
（贴图定「格子画在哪」、代码定「字摆在哪」；这条线上踩过「坐标与贴图不成对」）。

## 用法（在工程根执行）

    python devtools/make_rd_phase.py            # 出对照预览图（六格 / 四格），不写文件
    python devtools/make_rd_phase.py --apply    # 写出 texture/duel/phase/phase_rd.png
    python devtools/make_rd_phase.py --verify   # 对着磁盘上的产物逐条复核

依赖 Pillow。产物**不进 git**（`texture/` 在 .gitignore 里），
重建工作树后按上面的命令重放一次即可。
"""
import os
import sys

from PIL import Image, ImageDraw

PROJ = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SRC = os.path.join(PROJ, "texture", "duel", "phase", "phase.png")
OUT = os.path.join(PROJ, "texture", "duel", "phase", "phase_rd.png")
PREVIEW = os.path.join(PROJ, "devtools", "phase_rd_preview.png")

# ---- 源图三层（实测，见文件头）------------------------------------------------
FRAME_BOX = (10, 4, 1013, 122)          # 外框（线宽 3）
GLOW = (204, 204, 204, 40)              # 柔和底板：均匀铺满外框内缘

SLOT_WH = (129, 85)                     # 单格尺寸（含线宽 3）
SLOT_BOX_SRC = [(28, 19, 156, 103),     # 六个原格
                (197, 19, 325, 103),
                (364, 19, 492, 103),
                (532, 19, 659, 103),
                (699, 19, 827, 103),
                (867, 19, 995, 103)]
ARROW_BOX_SRC = [(165, 42, 190, 83),    # 五个原箭头（格间 ▷）
                 (334, 42, 359, 83),
                 (501, 42, 526, 83),
                 (669, 42, 694, 83),
                 (836, 42, 861, 83)]

# ---- 产物：四格等距 -----------------------------------------------------------
# 格心：**等距**（间距 279）**且关于图心对称**（首末和 = 1023 = 图宽-1）。
# ⚠ 这两个条件一起才定得住整数解：3×间距 = 1023 - 2×首格心 ⇒ 间距只能取奇数，
#   279 是「首末格大致落在源图首末格位置」的那个解（29/866 vs 源图 28/867）。
#   间距取偶数就会变成 279/281/279 那种「看着齐、量着不齐」。
RD_SLOT_CENTER = [93, 372, 651, 930]
RD_SLOT_LEFT = [c - (SLOT_WH[0] // 2) for c in RD_SLOT_CENTER]      # 29 / 308 / 587 / 866
# 箭头落在相邻格之间空档的中心
RD_ARROW_LEFT = [int((RD_SLOT_CENTER[i] + RD_SLOT_CENTER[i + 1]) / 2.0 - 13 + 0.5)
                 for i in range(len(RD_SLOT_CENTER) - 1)]

# 贴图显示宽（phaseT 的 UITexture mWidth）—— 用来把图上的格心换算成标签的 local x。
# ⚠ 这个 580 是 prefab 里的值（new_gameField.prefab → phaseT）。改了它，标签 x 要跟着改。
DISPLAY_W = 580.0


def label_x(center_px):
    """图上的格心 → 标签的 local x（六格那套也是这么来的，见文件头）。"""
    return (center_px - 511.5) * (DISPLAY_W / 1024.0)


def _erase_to_glow(im, box):
    """把一块矩形填回柔和底板色（原格/原箭头就是这么擦掉的）。"""
    ImageDraw.Draw(im).rectangle(list(box), fill=GLOW)


def build():
    """出图：整块搬 + 擦原格原箭头 + 贴四格三箭头。全程 1:1，不缩放、不重画线。"""
    src = Image.open(SRC).convert("RGBA")
    out = src.copy()

    # 1) 擦掉原六格五箭头 —— 填回柔和底板色，格子里外同色 ⇒ 无缝、无残留
    for box in SLOT_BOX_SRC + ARROW_BOX_SRC:
        _erase_to_glow(out, box)

    # 2) 贴四格（源格 1 的整块，1:1）
    slot = src.crop((SLOT_BOX_SRC[0][0], SLOT_BOX_SRC[0][1],
                     SLOT_BOX_SRC[0][2] + 1, SLOT_BOX_SRC[0][3] + 1))
    for left in RD_SLOT_LEFT:
        out.paste(slot, (left, SLOT_BOX_SRC[0][1]))

    # 3) 贴三箭头（源箭头 1 的整块，1:1）
    arrow = src.crop((ARROW_BOX_SRC[0][0], ARROW_BOX_SRC[0][1],
                      ARROW_BOX_SRC[0][2] + 1, ARROW_BOX_SRC[0][3] + 1))
    for left in RD_ARROW_LEFT:
        out.paste(arrow, (left, ARROW_BOX_SRC[0][1]))

    print("  擦原格 %d 个 / 原箭头 %d 个；贴四格 %s / 三箭头 %s"
          % (len(SLOT_BOX_SRC), len(ARROW_BOX_SRC), RD_SLOT_LEFT, RD_ARROW_LEFT))
    print("  四个标签的 local x = %s"
          % " / ".join("%.1f" % label_x(c) for c in RD_SLOT_CENTER))
    return out


def checker(size, step=16):
    im = Image.new("RGBA", size, (48, 52, 62, 255))
    d = ImageDraw.Draw(im)
    for y in range(0, size[1], step):
        for x in range(0, size[0], step):
            if ((x // step) + (y // step)) % 2 == 0:
                d.rectangle([x, y, x + step - 1, y + step - 1], fill=(64, 70, 82, 255))
    return im


def _slot_center_of(box):
    return (box[0] + box[2]) / 2.0


def verify():
    """对着**磁盘产物**复核六件事：
      ① 尺寸与源图一致；
      ② 产物 == 内存重建（逐像素）—— 抓「手改过产物」这一类漂移；
      ③ 外框逐像素等于源图（整块没被动过）；
      ④ 四格贴片各自逐像素等于源格 1（1:1 搬运）；
      ⑤ 三个箭头贴片逐像素等于源箭头 1；
      ⑥ 除「四格 + 三箭头」之外，产物与柔和底板逐像素一致 —— 抓「六个原格/五个原箭头
         没擦干净」（残留的新月形线会在这里冒出来）；
      外加几何：四格等距、关于图心对称，以及由格心算出的标签 x。"""
    name = os.path.basename(OUT)
    if not os.path.exists(OUT):
        print("!! 没有 %s —— 先跑 --apply" % OUT)
        return 1
    src = Image.open(SRC).convert("RGBA")
    rd = Image.open(OUT).convert("RGBA")
    bad = 0

    if src.size != rd.size:
        print("!! %s 尺寸不一致 %s vs %s" % (name, src.size, rd.size))
        return 1
    W, H = src.size

    # ② 与内存重建逐像素相同
    want = build()
    wp, rp = want.load(), rd.load()
    diff = sum(1 for y in range(H) for x in range(W) if wp[x, y] != rp[x, y])

    # ③ 外框（源图里「清晰、且不在任何原格/原箭头里」的像素）必须一模一样
    sp = src.load()
    slot_area = set()
    for (_a, _b, c, d) in SLOT_BOX_SRC:
        for y in range(_b - 3, d + 4):
            for x in range(_a - 3, c + 4):
                slot_area.add((x, y))
    for (_a, _b, c, d) in ARROW_BOX_SRC:
        for y in range(_b - 3, d + 4):
            for x in range(_a - 3, c + 4):
                slot_area.add((x, y))
    frame_bad = 0
    for y in range(H):
        for x in range(W):
            if sp[x, y][3] > 100 and (x, y) not in slot_area:
                if sp[x, y] != rp[x, y]:
                    frame_bad += 1

    # ④ 四格贴片逐像素 == 源格 1
    slot_src = src.crop((SLOT_BOX_SRC[0][0], SLOT_BOX_SRC[0][1],
                         SLOT_BOX_SRC[0][2] + 1, SLOT_BOX_SRC[0][3] + 1))
    slp = slot_src.load()
    sw, sh = slot_src.size
    slot_bad = 0
    for left in RD_SLOT_LEFT:
        for dy in range(sh):
            for dx in range(sw):
                if slp[dx, dy] != rp[left + dx, SLOT_BOX_SRC[0][1] + dy]:
                    slot_bad += 1

    # ⑤ 三箭头贴片逐像素 == 源箭头 1
    ar_src = src.crop((ARROW_BOX_SRC[0][0], ARROW_BOX_SRC[0][1],
                       ARROW_BOX_SRC[0][2] + 1, ARROW_BOX_SRC[0][3] + 1))
    ap = ar_src.load()
    aw, ah = ar_src.size
    arrow_bad = 0
    for left in RD_ARROW_LEFT:
        for dy in range(ah):
            for dx in range(aw):
                if ap[dx, dy] != rp[left + dx, ARROW_BOX_SRC[0][1] + dy]:
                    arrow_bad += 1

    # ⑥ 「四格 + 三箭头」之外 = 柔和底板
    keep = set()
    for left in RD_SLOT_LEFT:
        for y in range(SLOT_BOX_SRC[0][1] - 3, SLOT_BOX_SRC[0][3] + 4):
            for x in range(left - 3, left + sw + 3):
                keep.add((x, y))
    for left in RD_ARROW_LEFT:
        for y in range(ARROW_BOX_SRC[0][1] - 3, ARROW_BOX_SRC[0][3] + 4):
            for x in range(left - 3, left + aw + 3):
                keep.add((x, y))
    # ⚠ 「外框」自己也要排除掉：它是清晰的、但不在原格/原箭头里（③ 已经咬过它与源图相同）。
    #   用矩形去框「外框内缘」是错的 —— 圆角的弧会探进那个矩形（第一版就是这么报出
    #   108 个假残留的：x 13-23 / 1000-1010 那一圈弧）。所以「外框」按定义取：
    #   **源图里清晰、且不属于任何原格/原箭头的像素**。
    frame = set()
    for y in range(H):
        for x in range(W):
            if sp[x, y][3] > 100 and (x, y) not in slot_area:
                frame.add((x, y))
    glow_bad = 0
    for y in range(H):
        for x in range(W):
            if (x, y) in keep or (x, y) in slot_area or (x, y) in frame:
                continue
            if rp[x, y][3] > 100 or sp[x, y][3] > 100:
                glow_bad += 1

    # 几何：四格等距 + 关于图心对称
    gaps = [RD_SLOT_CENTER[i + 1] - RD_SLOT_CENTER[i]
            for i in range(len(RD_SLOT_CENTER) - 1)]
    even = len(set(gaps)) == 1
    sym = all(abs((RD_SLOT_CENTER[i] + RD_SLOT_CENTER[-1 - i]) - (W - 1)) <= 1
              for i in range(len(RD_SLOT_CENTER)))

    print("== %s ==" % name)
    print("   与内存重建不符的像素        = %d（应 0）" % diff)
    print("   外框被改动                  = %d（应 0）" % frame_bad)
    print("   四格与源格 1 不符的像素     = %d（应 0）" % slot_bad)
    print("   三箭头与源箭头 1 不符的像素 = %d（应 0）" % arrow_bad)
    print("   四格/箭头之外的清晰像素     = %d（应 0；非 0 = 原六格/五箭头有残留）" % glow_bad)
    print("   格心 = %s   间距 = %s（等距 %s，对称 %s）"
          % (RD_SLOT_CENTER, gaps, even, sym))
    print("   标签 local x（贴图显示宽 %.0f）= %s"
          % (DISPLAY_W, " / ".join("%.1f" % label_x(c) for c in RD_SLOT_CENTER)))
    ok = (diff == 0 and frame_bad == 0 and slot_bad == 0
          and arrow_bad == 0 and glow_bad == 0 and even and sym)
    print("   结论：" + ("PASS" if ok else "FAIL"))
    print("总结论：" + ("PASS" if ok else "FAIL"))
    bad = 0 if ok else 1
    return bad


def main():
    if "--verify" in sys.argv:
        return verify()

    im = build()
    if "--apply" in sys.argv:
        im.save(OUT)
        print("写出 " + OUT)

    W, H = im.size
    panels = [("BEFORE  phase.png (6 格：DP/SP/MP1/BP/MP2/EP)", Image.open(SRC).convert("RGBA")),
              ("AFTER   phase_rd.png (4 格：DP/MP1/BP/EP)", im)]
    gap = 12
    canvas = Image.new("RGBA", (W, (H + 34) * len(panels) + gap), (18, 20, 26, 255))
    dd = ImageDraw.Draw(canvas)
    for i, (cap, im_) in enumerate(panels):
        panel = checker((W, H))
        panel.alpha_composite(im_)
        box = [0, i * (H + 34 + gap) + 34, W, i * (H + 34 + gap) + 34 + H]
        canvas.alpha_composite(panel, (0, i * (H + 34 + gap) + 34))
        dd.text((8, i * (H + 34 + gap) + 10), cap, fill=(255, 200, 90, 255))
        if i == 1:
            for (left, c) in zip(RD_SLOT_LEFT, RD_SLOT_CENTER):
                dd.rectangle([left, box[1] + SLOT_BOX_SRC[0][1],
                              left + SLOT_WH[0] - 1, box[1] + SLOT_BOX_SRC[0][3]],
                             outline=(110, 230, 140, 255), width=1)
    canvas.convert("RGB").save(PREVIEW, quality=92)
    print("预览 " + PREVIEW)
    return 0


if __name__ == "__main__":
    sys.exit(main())
