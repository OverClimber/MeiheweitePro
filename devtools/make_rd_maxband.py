# -*- coding: utf-8 -*-
"""RD 极大怪兽「大框」贴图生成器（离线，可复跑）。

做的是用户 2026-09-22 要的那件事：
  「极大怪兽召唤时将前场的格子完全隐藏，并把三张极大怪兽无缝拼接，单独做一个大框，
    极大怪兽离场以后变回正常状态，要求这个切换必须是无延迟的」

## 为什么走「把大框画进场地贴图」这条路

场地的两条「怪兽排」格区是**烘焙在贴图里**的（`texture/duel/newfield_rd.png`），
运行时显示由 NGUI 的 `midT`（`UITexture`，9-slice `border=(0,500,0,230)`）承担
（见 `gameField.loadNewField`）。所以「把格子完全隐藏」最稳的做法不是新加 3D 物件，
而是**换掉 midT 这张图**：

  · 格子是被像素替换掉的 ⇒ 不存在「盖不住 / 露一条边」的问题；
  · 图层顺序与现在**完全一致**（大框就是场地的一部分，永远在卡下面）；
  · 切换 = 一次 `mainTexture` 赋值 ⇒ 真正无延迟（没有动画、没有实例化、没有加载）。

运行时按 `gameField.setMaximumBand(mask)` 在 `midBase` 与三张变体之间切（mask：
bit0=我方、bit1=对手，`--` 就是「基图」本身）。

## 本脚本产出

  变体图：`output/Windows/texture/duel/rd_maxband_me.png` / `_op.png`
    尺寸 = 「三格 ∪ 大框」那块（切片空间，见 `erase_rect()`），内容为
      · 半透明片 = **框那一块**（`frame_rect()`，与框逐 texel 同矩形）：片只许在框内、
        且必须把框填满。口径来自用户 2026-09-22 原话「那层半透明的片必须限制在框内
        并填满框，极限怪兽下去后和大框一起复原」；
      · **三格那块整体清空**（三条竖描边、两条横描边、格间那两道缝、以及**框外那两条
        36 texel 宽的带子**全部置透明）—— 格子要「完全隐藏」，而片不许渗出框外；
      · 大框描边：贴着框的内沿画 3 texel，颜色 = 本侧描边色
        （我方蓝 `(70,143,231)` / 对方红 `(217,83,85)`，与原生格线同宽）；
      · 其余像素透明。

  ⛔⛔ 片的矩形**不许**跟着「三格」走（2026-09-22 的被用户打回的那一版就是这么写的）：
    三格(327x116) 与框(257x121) **不是同一个矩形** —— 框横向窄 36 texel/边、纵向又朝
    场地中央多探出 CENTER_Y_OFF(14)±余量。片按三格铺 ⇒ 框的左右竖边之外各多出一条
    36 texel 的带子（实机屏幕 ≈37px，亮度比旁边正常格子低 ≈11），实机一句原话就是
    「半透明的那一层还是超出框外了」。⛔ 也不许反过来「把框内部额外再铺一层」（更早
    那版的错法）：那会让片的足迹变成「三格 ∪ 框」这个带凸台的矩形，凸台落在框的左右
    竖边处留下 13px 的方形台阶。两版的判据都是 **6w**（片必须 == 框那一块）。

  ⛔ 框**不是**「把三格那块框起来」：三格比三张卡宽得多（326 texel vs 244 texel），
    那样框会明显宽于卡、上下还各差一截（2026-09-22 第一版的实机截图就是这样）。
    框的口径一律是「贴住三张卡」，尺寸与位置都由 `TRIO_W/TRIO_H/CENTER_Y_OFF` 决定。

  预览（不带参数时）：`_mf2_me.png` / `_mf2_op.png` —— 把大框 + 三张卡（按运行时的
    对齐比与缩放）合到真场地上，用来**先看外观再改代码**。

## 量测依据（`texture/duel/newfield_rd.png` 999x800 空间）

  中区三列 x = 330..657（逐格 330/437、440/547、550/657，列距 110px）
  怪兽排 y：我方 481..596 、对手 204..319（各高 116）
  另两排（魔陷）我方 603..718 / 对手 82..197 —— 大框**不许碰到它们**

  描边实测：3px、我方 `(70,143,231,254)`、对手 `(217,83,85,254)`；格子底 `(255,255,255,51)`

  「卡画在贴图哪儿」的实测（`[max]` 探针的 `frect` + 截图网格线标定，2026-09-22 重测）：
  对手怪兽排上三张 1.45 倍的卡，屏幕矩形 x 908..1165 / y 210..284（257 x 74）。
  先量出**屏幕 → 贴图**的映射（拿截图上四条格线的像素位置做射影拟合，内插核验逐条 ≤2px、
  外推到另一侧也 ≤3px），再把卡的四条边反解回 999 空间 ⇒ 卡的**贴图足迹**：

    纵向 219.6..327.5（高 **108** texel），中心 **275.8**，而本排格心是 261.5
    ⇒ 卡足迹中心比格心**朝场地中央**偏 **+14.3 texel**（≈0.76 世界）；

  ⛔ 这个偏移**必须分侧**（第一版两侧都往下挪 ⇒ 我方侧低了整整 2×14 texel）：
  对手那排在场地上方 ⇒ +14（向下）、我方那排 ⇒ −14（向上）。
  ⛔ 高度也不能用「5.8 世界 × 19.14」那档：卡与场地**共面**（足迹反解出的高 108 与
  「5.8 世界 × 18.72」逐位吻合），纵向横向是同一个 texel/世界 比。

  切片空间：`UIHelper.sliceField` 会先把图缩到 1024x819 再按 x 69/320、247/320 切三片
  ⇒ 本脚本的变体按 1024x819 的坐标算，运行时直接 `SetPixels32(x,y,w,h,…)` 写进去。

用法：
  python devtools/make_rd_maxband.py              # 只出预览
  python devtools/make_rd_maxband.py --apply      # 写 texture/duel/rd_maxband_*.png
                                                  # （源树 + output/Windows 两处都写）
"""
import os
import sys

from PIL import Image, ImageDraw

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(HERE)                       # YGOProUnity_V2
RUN = os.path.join(ROOT, "output", "Windows")
FIELD = os.path.join(RUN, "texture", "duel", "newfield_rd.png")
OUTDIR = os.path.join(RUN, "texture", "duel")
CARDS = os.path.join(RUN, "rd", "picture", "card")

# ── 实测常量（999x800 原始空间）──────────────────────────────────────────────
# 中区三格的**外沿**（含各自 3px 描边）：这块整体**清空**（格线一条不留，片也不许铺
# 到这里 —— 片只铺框内）。口径见 make_art()。+1 texel 是舍入出血。
CELLS_X = (330, 657)
ROW = {"me": (481, 596), "op": (204, 319)}      # 怪兽排格区（含描边）
COLOR = {"me": (70, 143, 231), "op": (217, 83, 85)}   # 本侧描边色（实测）
ZONE_FILL = (255, 255, 255, 51)        # 格子底色（实测），大框内部照抄它
BORDER = 3                             # 描边粗细（texel）—— 与原生格线同宽

# 三张极大怪兽（贴紧 + 放大 1.45）换算成 **texel** 的尺寸。换算依据不是目测：
#   · 世界尺寸：卡面 Quad(1x1) × face.localScale(3,4) × 倍率 1.45 ⇒ 4.35 x 5.8 世界，
#     三张贴紧 ⇒ 13.05 世界宽（`Ocgcore.RdMaximumTrioScale`）；
#   · texel ↔ 世界（横向）：110 texel = 一列 = 5.876 世界（`rdColumnX` 实测间距 × real）
#     ⇒ 18.72 texel/世界。横向量测独立复核：卡在屏幕上 82px 宽，而 110 texel 的列距
#     对应 5.876 世界 × 18.85 px/世界 = 111px ⇒ texel 与屏幕像素 ≈ 1:1。
#   · texel ↔ 世界（纵向）：**不能**直接用 20.96（排距 122 texel / 5.82 世界）——
#     卡是**立着**的，它在屏幕上的"高"按格子的进深折算：实测卡 72px 高 / 本排格区
#     116 texel 在屏幕上 75px ⇒ 111 texel。用错这一档，框会上下各差 8~10px。
TRIO_W = 244.0                         # = 13.05 × 18.72（实测外接框 249，余量罩得住）
TRIO_H = 108.6                         # = 5.8 × 18.72 —— 与横向同一个 texel/世界 比
CENTER_X = 493.5                       # 中列格心（= 本体的那一列）
FIELD_CENTER_Y = 400.5                 # 场地中央（两排之间）—— 偏移方向的判据
CENTER_Y_OFF = 14.0                    # 卡足迹中心离本排格心的距离，**朝场地中央**
MARGIN_X = 6.0                         # 框到卡外沿的横向余量（≈6px）
MARGIN_Y = 6.0                         # 纵向余量（6 texel ≈ 4~7px，与横向同观感）

# 切片空间（`sliceField` 的缩放）
SX, SY = 1024.0 / 999.0, 819.0 / 800.0
SLICE_W, SLICE_H = 1024, 819
BLEED = 1                              # 出血：吃掉舍入误差，别让旧描边露一丝

# 运行时侧的比例（`[max]` 探针实测）
CARD_W_WORLD, CARD_H_WORLD = 3.0, 4.0
SCALE = 1.45                           # 三件放大到 1.45 倍（再按「贴紧」重排）


def cell_rect(who):
    """三格并起来那块（999 空间 x0,y0,x1,y1），洗成一片底 —— 格线全没。"""
    return CELLS_X[0], ROW[who][0], CELLS_X[1], ROW[who][1]


def frame_rect(who):
    """大框（999 空间 x0,y0,x1,y1）—— 贴住三张卡 + 余量。

    位置 = 三张卡的**贴图足迹**（中列格心 ± 半个卡宽/卡高），纵向再朝**场地中央**
    挪 CENTER_Y_OFF。⛔ 方向必须分侧：对手那排在场地上方（值小）⇒ +；我方那排在
    下方 ⇒ −。第一版两侧都「往下挪」，我方侧就整整低了 2×14 texel，框底越过魔陷排
    顶线（603）把它吃掉、上沿又没罩住卡 —— 用户 2026-09-22 的「框太大、顶线被挤掉」
    正是这一条。分侧之后两侧到本排魔陷排的净空都 ≈18 texel。
    """
    cx = CENTER_X
    row_c = (ROW[who][0] + ROW[who][1]) / 2.0
    cy = row_c + (CENTER_Y_OFF if row_c < FIELD_CENTER_Y else -CENTER_Y_OFF)
    return (int(round(cx - TRIO_W / 2 - MARGIN_X)),
            int(round(cy - TRIO_H / 2 - MARGIN_Y)),
            int(round(cx + TRIO_W / 2 + MARGIN_X)),
            int(round(cy + TRIO_H / 2 + MARGIN_Y)))


def erase_rect(who):
    """变体图要覆盖的那块（999 空间）= 三格 ∪ 大框（含出血）—— 保证框整个落在图里。"""
    c = cell_rect(who)
    f = frame_rect(who)
    return (min(c[0], f[0]) - BLEED, min(c[1], f[1]) - BLEED,
            max(c[2], f[2]) + BLEED, max(c[3], f[3]) + BLEED)


def band_rect_slice(who):
    """该侧变体图在**切片空间**里的矩形 (x, y, w, h)，y 自下往上（Unity 口径）。

    Unity 的贴图 y 是底朝上的，PIL 是顶朝下的 ⇒ 这里换算一次，两边都由本函数统一管，
    免得 C# 侧再各自推一遍（推错一格就是「格子没盖住」）。
    """
    x0, y0, x1, y1 = erase_rect(who)
    sx0 = int(round(x0 * SX))
    sx1 = int(round(x1 * SX))
    sy_lo = SLICE_H - 1 - int(round(y1 * SY))     # 下方（PIL 的 y1 更靠下）
    sy_hi = SLICE_H - 1 - int(round(y0 * SY))     # 上方
    return sx0, sy_lo, sx1 - sx0 + 1, sy_hi - sy_lo + 1


def band_rect_pil(who):
    """同一块矩形换回 PIL（顶朝下）坐标，给预览/画图用。"""
    x, y, w, h = band_rect_slice(who)
    return x, SLICE_H - (y + h), x + w, SLICE_H - y


def _local_xy(who, x999, y999):
    """999 空间的一点 → 变体图内（PIL、顶朝下）的像素坐标。"""
    x, y, w, h = band_rect_slice(who)
    px = int(round(x999 * SX)) - x
    py = (SLICE_H - 1 - int(round(y999 * SY))) - y
    py = h - 1 - py                     # Unity 的 y 底朝上 → PIL 顶朝下
    return px, py


def make_art(who):
    """该侧的变体贴图（RGBA，尺寸 = band_rect_slice 的 w×h）。

    内容两件：
      ① 半透明片 = **框那一块**（与 `frame_rect()` 逐 texel 同矩形）—— 片只许在框内、
         且必须把框填满。用户 2026-09-22 第二条口径原话：
         「那层半透明的片必须限制在框内并填满框，极限怪兽下去后和大框一起复原」。
      ② 大框描边：贴着框的内沿画 `BORDER` texel，颜色 = 本侧描边色。
    别处**一律透明**（含三格那块、以及框探出本排那一截之外的部分）—— 三格的格线与
    框外那两条带子的片都得清掉，格子才算「完全隐藏」。

    ⛔⛔ 片的矩形**不许**跟着「三格」走（2026-09-22 被用户打回的那一版就是这么写的）：
      三格(327x116) 与框(257x121) 不是同一个矩形 —— 框横向**窄** 36 texel/边、纵向又
      朝场地中央**多探出** CENTER_Y_OFF(14)±余量。片按三格铺 ⇒ 框的左右竖边之外各
      多出一条 36 texel 的带子（实机屏幕 ≈37px，亮度 102.7 vs 旁边正常格子 113.5），
      玩家看到的就是「半透明的那一层还是超出框外了」。
    ⛔ 也不许再「把框内部额外铺一层」（更早那版的错法）：那会让片的足迹变成
      「三格 ∪ 框」这个带凸台的矩形，凸台落在框的左右竖边处留下 13px 方形台阶。
    ⚠ 片是按框铺的 ⇒ 框**纵向探出本排**的那一截（对手侧向下 17、我方侧向上 17 texel）
      里也有片 —— 这正是「填满框」的要求：框是贴住卡画的，不该露出场景底图。
    """
    x, y, w, h = band_rect_slice(who)
    im = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    col = COLOR[who]
    f = frame_rect(who)

    # ① 片：铺满框那一块（不多一格、也不少一格）—— 三格那块不铺，它要整体清空
    d.rectangle([_local_xy(who, f[0], f[1]), _local_xy(who, f[2], f[3])], fill=ZONE_FILL)

    # ② 描边：贴着框的内侧画 BORDER 层，与原生格线同宽同色
    for i in range(BORDER):
        d.rectangle([_local_xy(who, f[0] + i, f[1] + i),
                     _local_xy(who, f[2] - i, f[3] - i)], outline=col + (255,))
    return im


def preview(cards=("120150001", "120150002", "120150003")):
    """出对照图：真场地里把该排三格换成大框，并把三张卡按运行时的排布贴上去。"""
    field = Image.open(FIELD).convert("RGBA")
    base = field.resize((SLICE_W, SLICE_H), Image.BICUBIC)

    for who in ("me", "op"):
        canvas = base.copy()
        canvas.paste(make_art(who), band_rect_pil(who)[:2])
        bg = Image.new("RGBA", canvas.size, (17, 21, 70, 255))
        comp = Image.alpha_composite(bg, canvas).convert("RGB")

        # 三张卡：按运行时口径摆（贴紧 + 放大 1.45，纵向再按**朝场地中央**的方向挪）
        cx_px = CENTER_X * SX
        row_c = (ROW[who][0] + ROW[who][1]) / 2.0
        cy_px = (row_c + (CENTER_Y_OFF if row_c < FIELD_CENTER_Y else -CENTER_Y_OFF)) * SY
        cw = TRIO_W / 3.0 * SX                     # 一张卡的宽（texel → 切片空间）
        ch = TRIO_H * SY                           # 一张卡的高
        step = cw                                  # 无缝拼接 = 中心距 = 卡宽
        for i, code in enumerate(cards):
            art = Image.open(os.path.join(CARDS, code + ".jpg")).convert("RGB")
            art = art.resize((max(1, int(round(cw))), max(1, int(round(ch)))), Image.LANCZOS)
            if who == "op":
                art = art.transpose(Image.ROTATE_180)   # 对手那侧卡是倒过来的
            px = int(round(cx_px + (i - 1) * step - cw / 2))
            py = int(round(cy_px - ch / 2))
            comp.paste(art, (px, py))

        y0, y1 = ROW[who]
        crop = (200, int(y0 * SY) - 40, 800, int(y1 * SY) + 60)
        out = comp.crop(crop).resize(((crop[2] - crop[0]) * 2, (crop[3] - crop[1]) * 2),
                                     Image.LANCZOS)
        p = os.path.join(ROOT, "..", "_mf2_%s.png" % who)
        out.save(os.path.abspath(p))
        print("预览", who, out.size, os.path.abspath(p),
              "cell=%s frame=%s" % (cell_rect(who), frame_rect(who)))


def apply():
    # 两个落点都要写：
    #   · RUN（output/Windows/texture/duel）—— 运行时直接读这里，改完立刻生效；
    #   · SRC（YGOProUnity_V2/texture/duel）—— 源树里的场地贴图（newfield_rd.png 就在这），
    #     整包重建会把源树合并铺入各输出目录 ⇒ 只写 RUN 的话下次重建图就没了。
    src = os.path.join(ROOT, "texture", "duel")
    for who in ("me", "op"):
        art = make_art(who)
        r = band_rect_slice(who)
        for d in (OUTDIR, src):
            if not os.path.isdir(d):
                print("跳过（目录不存在）%s" % d)
                continue
            p = os.path.join(d, "rd_maxband_%s.png" % who)
            art.save(p)
            print("写出 %s  rect(x=%d y=%d w=%d h=%d)" % (p, r[0], r[1], r[2], r[3]))


if __name__ == "__main__":
    preview()
    if "--apply" in sys.argv:
        apply()
