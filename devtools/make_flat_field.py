# -*- coding: utf-8 -*-
# ============================================================================
# ⛔ 本脚本【已作废】，跑它没有任何意义（2026-09-28 返工后无调用点）。
#
# 为什么留着它会【主动误导】人（2026-09-30 做 RD 俯视角调研时实测踩到）：
#   我读到本文件里的 SRC_RD / OUT_RD 两个常量，又看到一句
#   「本轮 GameField.NewFieldPath 只对 OCG 走 flat，接上去即可」
#   作为结论：RD 的俯视专用场地图【只差接一下】。
#   **这个结论是错的**：
#     - gameField.FlatFieldPath 早已标 ⛔⛔ 已停用，
#       NewFieldPath 【只看模式不看视角】；
#     - 停用原因：它把原图 52.2% 的【全透明】区填成近黑 MAT_BG=(26,28,34)，
#       场地变成「一团黑」，而用户口径是
#       「俯视的场地观感必须和正常视角一样」；
#     - RD 俯视的真实原因是【取景旋钮只有一个纵向】
#       （CoverZ），而 RD 盘面横向也小（BoardHalfX 9.12 vs OCG 15.2）
#       —— 见 _报告_RD俯视角适配调研_20260930.md。
#
# 要重做这件事的话：gameField.cs 174~188 已经把该走的路写清楚了 ——
#   【把原图合成到一张取自 60° 实测的浅色盘面底色上，
#    而不是填近黑。】
# ============================================================================
"""生成**俯视角专用**的不透明场地图 `texture/duel/newfield_flat.png`（v3 / 需求④）。

为什么需要它（2026-09-28 实测）
--------------------------------------------------------------------
`texture/duel/newfield.png` 是**线稿**：alpha 分布 52.2% 全透明 + 42.3% 半透明，
可见像素里「暗色块」只有 119/24716 ⇒ 它是一张**画在透明底上的线框图**，
底下的 `texture/common/desk.jpg`（7680×4320 角色插画）会**直接穿透盘面**。
60° 下透视 + 高光把格内冲淡成乳白（`_probe_topdown/off_center` 对比），问题不显；
正俯视正对盘面 ⇒ 看到的是贴图**原始 alpha** ⇒ 插画糊在格线上，
整块盘面读起来「小而远」—— 这就是需求④「不要产生卡牌和棋盘距离很远的感觉」的主因。

本脚本的做法（关键：**继承原图，不从零重画**）
--------------------------------------------------------------------
1. 铺一层**不透明**深色哑光底盘；
2. 给 5×4 的 20 个格子 + 左右外挂列 + 中区两个额外怪兽区，各铺一块**内缩**的浅色面板
   （这就是 G1「格子带缝」：面板内缩 ⇒ 面板与格线之间露出一圈底色 ⇒ 相邻格之间有缝，
   **不移动任何一条格线** ⇒ 点击热区与视觉**零错位**）；
   ⚠ 刻意**不**把格线本身画小/画窄：格子边界 = 点选热区，画小了就产生
   「我点在这个框里却没中」的错觉。EDOPro/YGOPro3 用「格子比卡大」制造缝，
   我们格子已经贴死卡宽（10 列 × 3.04 = 卡宽 3.0），所以缝只能做在**面板**这一层。
3. 我方半区（蓝）与我方以外的（红/中立）用**不同深浅**的底色 ⇒ 俯视下一眼分清两侧；
4. 把原图**所有非透明像素原样合成回来** ⇒ 35 / 14(0) / 星 / 圆 / 叉 等标记**逐像素不变**，
   标记错位的风险为零（这是整件事最重要的去风险点）。

坐标来源
--------------------------------------------------------------------
全部取自 `devtools/make_rd_field.py` 已实测的行/列投影（`detect()` 的产物），
**不是目测裁的**。⛔ 贴图与 `Ocgcore.get_point_worldposition()` 是同一份真相，
   改格位必须同改贴图（MEMORY 红线）。

用法
--------------------------------------------------------------------
    python devtools/make_flat_field.py            # 生成 + 校验
    python devtools/make_flat_field.py --verify   # 只校验（部署后核对）
    python devtools/make_flat_field.py --preview  # 额外出一张「叠在棋盘上」的效果预览
"""
import os
import sys

from PIL import Image, ImageDraw

PROJ = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SRC = os.path.join(PROJ, "texture", "duel", "newfield.png")
OUT = os.path.join(PROJ, "texture", "duel", "newfield_flat.png")
PREVIEW = os.path.join(PROJ, "devtools", "newfield_flat_preview.png")

# RD 的紧凑盘面（另出一张；格位更浅 ⇒ `TableauLayout.BoardHalfZ` 取 11.5）
SRC_RD = os.path.join(PROJ, "texture", "duel", "newfield_rd.png")
OUT_RD = os.path.join(PROJ, "texture", "duel", "newfield_rd_flat.png")
PREVIEW_RD = os.path.join(PROJ, "devtools", "newfield_rd_flat_preview.png")

# ── 底盘配色（俯视专用；60° 一行都没动）────────────────────────────────────
MAT_BG = (26, 28, 34, 255)        # 哑光深底：把插画彻底压掉
MAT_PANEL_ME = (40, 48, 68, 255)  # 我方半区（蓝侧）面板
MAT_PANEL_OP = (62, 40, 44, 255)  # 对方半区（红侧）面板
MAT_PANEL_EMZ = (56, 56, 64, 255)  # 中区两个额外怪兽区（中立）
MAT_PANEL_PILE = (34, 36, 44, 255)  # 左右外挂列（卡组/额外/墓地/除外）

PANEL_INSET = 4                    # G1：面板相对格子内缩几 px（= 格线与面板之间的「缝」）

# ── 格位表（来自 make_rd_field.py 的 detect() 实测）────────────────────────
NATIVE_W, NATIVE_H = 106, 116
COLS = [(113, 218), (221, 326), (329, 434), (437, 542), (545, 650)]
ROWS_ME = [(481, 596), (603, 718)]      # 我方（画面下方）
ROWS_OP = [(82, 197), (204, 319)]       # 对方（画面上方）
OUTER_L = (105, 0, 215, 800)            # 左外挂列整列
OUTER_R = (775, 0, 895, 800)            # 右外挂列整列
EMZ = [(327, 339, 440, 461), (547, 339, 660, 461)]


def _panel(img, box, colour):
    d = ImageDraw.Draw(img)
    x0, y0, x1, y1 = box
    d.rectangle([x0 + PANEL_INSET, y0 + PANEL_INSET, x1 - PANEL_INSET, y1 - PANEL_INSET],
                fill=colour)


def build():
    src = Image.open(SRC).convert("RGBA")
    W, H = src.size

    # 1) 不透明底盘（整张，含原本全透明的区域）
    dst = Image.new("RGBA", (W, H), MAT_BG)

    # 2) 面板：对方两行 / 我方两行 / 外挂列 / 中区 EMZ
    for c in COLS:
        for (r0, r1) in ROWS_OP:
            _panel(dst, (c[0], r0, c[1], r1), MAT_PANEL_OP)
        for (r0, r1) in ROWS_ME:
            _panel(dst, (c[0], r0, c[1], r1), MAT_PANEL_ME)
    for b in (OUTER_L, OUTER_R):
        _panel(dst, b, MAT_PANEL_PILE)
    for b in EMZ:
        _panel(dst, b, MAT_PANEL_EMZ)

    # 3) 原图线稿**原样**合成回来（标记/格线逐像素不变）
    dst = Image.alpha_composite(dst, src)
    dst.save(OUT)

    # ── 自检 ─────────────────────────────────────────────────────────────
    chk = Image.open(OUT).convert("RGBA")
    a = chk.split()[-1]
    hist = a.histogram()
    tot = W * H
    opaque = hist[255] * 100.0 / tot
    # 标记保真：**只**要求「原图里完全不透明的那些像素」在产物里逐字节不变。
    # ⚠ 半透明像素**本来就该变** —— 它们正是「让插画穿透盘面」的那些像素，
    #   alpha_composite 会把它们与新底盘混合，这正是本脚本要修的东西。
    #   拿全部可见像素去比会得到 8 万+ 个假失败（2026-09-28 首跑就踩了）。
    sp, dp = src.load(), chk.load()
    mismatch = 0
    solid = 0
    for y in range(0, H, 2):
        for x in range(0, W, 2):
            if sp[x, y][3] == 255:
                solid += 1
                if sp[x, y] != dp[x, y]:
                    mismatch += 1
    print("生成 %s  %dx%d" % (os.path.relpath(OUT, PROJ), W, H))
    print("  不透明覆盖率 = %.1f%%   (验收下限 98%%)" % opaque)
    print("  标记保真：原图**完全不透明**像素 %d 个，其中被改动的 = %d  (必须 0)"
          % (solid, mismatch))
    ok = opaque >= 98.0 and mismatch == 0
    print("  结论：%s" % ("通过" if ok else "**不通过**"))
    return ok


def preview():
    """把产物叠到一张纯色底上，看它单独长什么样（判「盘面自己是否读得清」）。"""
    im = Image.open(OUT).convert("RGBA")
    bg = Image.new("RGBA", im.size, (120, 130, 150, 255))
    Image.alpha_composite(bg, im).convert("RGB").save(PREVIEW)
    print("预览 %s" % os.path.relpath(PREVIEW, PROJ))


def verify():
    if not os.path.exists(OUT):
        print("缺产物：%s" % OUT)
        return False
    chk = Image.open(OUT).convert("RGBA")
    a = chk.split()[-1].histogram()
    opaque = a[255] * 100.0 / float(chk.size[0] * chk.size[1])
    src = Image.open(SRC).convert("RGBA")
    sp, dp = src.load(), chk.load()
    mismatch = 0
    for y in range(0, chk.size[1], 2):
        for x in range(0, chk.size[0], 2):
            # ⛔ 只咬**完全不透明**的像素（格线/数字/图标都靠它们）。
            #   半透明像素本来就该被新底盘吃掉 —— 那正是要修的病。
            if sp[x, y][3] == 255 and sp[x, y] != dp[x, y]:
                mismatch += 1
    print("校验 %s：不透明 %.1f%%，标记保真违例 %d" % (
        os.path.relpath(OUT, PROJ), opaque, mismatch))
    return opaque >= 98.0 and mismatch == 0


def main():
    args = sys.argv[1:]
    if "--verify" in args:
        return 0 if verify() else 1
    ok = build()
    if "--preview" in args:
        preview()
    return 0 if ok else 1


if __name__ == "__main__":
    sys.exit(main())
