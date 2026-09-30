# -*- coding: utf-8 -*-
"""生成俯视角专用的安静背景 `texture/common/desk_flat.jpg`（v3 / 需求④）。

为什么需要
--------------------------------------------------------------------
`texture/common/desk.jpg` 是 7680×4320 的**角色插画**。60° 下透视裁掉大半、加上高光，
观感能接受；正俯视正对画面 ⇒ 整张插画**原样铺满屏幕**，与格线、卡面、手牌全部打架
（`_probe_topdown/on.png` 一眼可见）。这就是需求④「卡牌和棋盘距离很远的感觉」的一半来源
（另一半是场地贴图半透明，已由 `devtools/make_flat_field.py` 处理）。

⛔ **刻意程序化生成，不改也不覆盖 `desk.jpg`**：
  · 60° 逐字节不变（红线：关态一个像素都不能动）；
  · 不引入新的美术资产依赖、不会碰到授权/来源问题；
  · 俯视角需要的不是「另一张图」，而是「**一块不抢戏的底**」：深色、中心略亮、
    四角压暗（vignette），让盘面与手牌成为唯一的高对比区域。

EDOPro 的做法与此一致：背景是**屏幕空间 2D blit**，俯视模式换图是「可选的外观覆盖」，
默认 3D 模式的背景一行都不用改（§2.1）。
"""
import os
import sys

from PIL import Image, ImageDraw, ImageFilter

PROJ = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT = os.path.join(PROJ, "texture", "common", "desk_flat.jpg")
PREVIEW = os.path.join(PROJ, "devtools", "desk_flat_preview.png")

W, H = 1920, 1080          # 背景是「铺满屏幕」用的，尺寸不必等于实际屏幕
BASE = (17, 19, 24)        # 底色：比场地底盘略深，让盘面浮起来
CENTRE = (40, 46, 58)      # 中心略亮（聚光）


def build():
    img = Image.new("RGB", (W, H), BASE)
    px = img.load()
    cx, cy = W * 0.5, H * 0.5
    maxd = (cx * cx + cy * cy) ** 0.5
    for y in range(H):
        dy2 = (y - cy) ** 2
        for x in range(W):
            d = ((x - cx) ** 2 + dy2) ** 0.5 / maxd      # 0(中心) .. 1(角)
            t = max(0.0, 1.0 - d) ** 1.6                  # 中心亮、四周迅速暗下去
            px[x, y] = (
                int(BASE[0] + (CENTRE[0] - BASE[0]) * t),
                int(BASE[1] + (CENTRE[1] - BASE[1]) * t),
                int(BASE[2] + (CENTRE[2] - BASE[2]) * t),
            )
    img = img.filter(ImageFilter.GaussianBlur(6))
    img.save(OUT, quality=88)

    # 顺带出一张「叠上场地图」的预览，方便人眼一次看完两件美术 together
    field = Image.open(os.path.join(PROJ, "texture", "duel", "newfield_flat.png")).convert("RGBA")
    bg = Image.new("RGBA", (W, H), (0, 0, 0, 255))
    bg.paste(img, (0, 0))
    bg = bg.convert("RGBA")
    # 把场地图按盘面在屏幕上的大致位置贴上去（CoverZ 24.35 / 高度 1080 ⇒ 22.2 px/世界）
    k = H / 2.0 / 24.35
    fw = int(999 * k * 0.42)
    fh = int(800 * k * 0.42)
    f2 = field.resize((fw, fh), Image.LANCZOS)
    bg.alpha_composite(f2, (int(W * 0.5 - fw * 0.5), int(H * 0.5 - fh * 0.5)))
    bg.convert("RGB").save(PREVIEW)

    chk = Image.open(OUT)
    print("生成 %s  %dx%d  %d 字节" % (os.path.relpath(OUT, PROJ), chk.size[0], chk.size[1],
                                      os.path.getsize(OUT)))
    print("预览 %s" % os.path.relpath(PREVIEW, PROJ))
    return 0


def main():
    if "--verify" in sys.argv:
        if not os.path.exists(OUT):
            print("缺产物：%s" % OUT)
            return 1
        print("校验 %s 存在，%d 字节" % (os.path.relpath(OUT, PROJ), os.path.getsize(OUT)))
        return 0
    return build()


if __name__ == "__main__":
    sys.exit(main())
