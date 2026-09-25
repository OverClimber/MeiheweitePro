# -*- coding: utf-8 -*-
"""RD「主要阶段」大字生成器 —— 从 `texture/duel/phase/mp1.png` 派生**不带 1** 的那张。

## 为什么要有第二张

用户 2026-09-21 口径：「rd 下的主要阶段不要叫主要阶段1了，就叫主要阶段」。
RD 只有一个主要阶段，玩家可见的「1」有三处：

  ① 盘面下方 hint 那行中文 —— `Ocgcore` 的 `ES_phaseString`（已改，RD →「主要阶段」）；
  ② 阶段条格子上的缩写 —— `lazyBTNMOVER.shiftRD()` 里的 `g3.text`（已改 MP1 → MP）；
  ③ 进主要阶段时全屏闪的**大字贴图** —— `mp1.png` 是美术字 **"MAIN PHASE 1"**，
     `GameMessage.NewPhase` 的 `DuelPhase.Main1` 分支原样弹它 ⇒ RD 里也带着「1」。

本脚本只管 ③：

    texture/duel/phase/mp1_rd.png
        = mp1.png 擦掉末尾的「1」字形 + 水平重新居中（其余逐像素不动）

## 「1」在哪（不是目测）

按列扫 alpha 得字形簇（中间由全透明列隔开）：

  · 最后一簇就是「1」（单词间空隙把它和 "PHASE" 隔开）；
  · 擦除范围 = 「1」左侧那条空隙的起点 → 图右缘；
  · 擦完对剩余内容求 bbox，水平平移 dx = (右边距 − 左边距) / 2 使其居中
    （`ImageChops.offset`，右缘已是透明，回卷不引入杂像素）。

## 消费方

`GameTextureManager.mp1RD`（可选资源，缺文件 = null）；
`Ocgcore.NewPhase`：`DuelPhase.Main1` 分支 `RD ? mp1RD : mp1`。
**只换贴图内容、不改尺寸（1024x600）** ⇒ `animation_show_big_string` 的坐标不动。

## 用法（在工程根执行）

    python devtools/make_rd_mp1.py            # 出对照预览图，不写文件
    python devtools/make_rd_mp1.py --apply    # 写出 texture/duel/phase/mp1_rd.png
    python devtools/make_rd_mp1.py --verify   # 对着磁盘上的产物逐条复核

⚠ texture/ 不入库：换机器 / 重 clone 后 `--apply` 重放这张图。
"""
import os
import sys

from PIL import Image, ImageChops

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(HERE)
SRC = os.path.join(ROOT, "texture", "duel", "phase", "mp1.png")
DST = os.path.join(ROOT, "texture", "duel", "phase", "mp1_rd.png")
PREVIEW = os.path.join(HERE, "mp1_rd_preview.png")

A_ON = 40          # alpha ≥ 此值算「有墨」（大字是金属渐变，实心区远高于它）


def clusters(im):
    """按列扫 alpha，返回 [(x0, x1), ...] 字形簇（x1 含）。"""
    w, h = im.size
    a = im.getchannel("A")
    px = a.load()
    ink = [any(px[x, y] >= A_ON for y in range(h)) for x in range(w)]
    out, s = [], None
    for x, v in enumerate(ink):
        if v and s is None:
            s = x
        elif not v and s is not None:
            out.append((s, x - 1))
            s = None
    if s is not None:
        out.append((s, w - 1))
    return out


def derive(src):
    """源图 → 擦掉最后一簇 + 重新居中。"""
    w, h = src.size
    cl = clusters(src)
    assert len(cl) >= 2, "源图应至少有 2 个字形簇（'MAIN PHASE 1' 连体+‘1’），实测 %d" % len(cl)
    last = cl[-1]
    # 「1」左侧空隙的起点：上一簇 x1 + 1
    gap0 = cl[-2][1] + 1
    # 合理性：末簇应明显窄于整行内容的 1/4（"1" 是窄字形，别把 "PHASE" 擦了）
    body_w = cl[-1][1] - cl[0][0]
    assert (last[1] - last[0]) * 4 < body_w, \
        "末簇 %s 不像「1」（内容宽 %d）—— 量错了别动手" % (last, body_w)
    im = src.copy()
    clear = (0, 0, 0, 0)
    for x in range(gap0, w):
        for y in range(h):
            im.putpixel((x, y), clear)
    # 重新居中（水平）
    bbox = im.getbbox()          # 非零（含 alpha）范围
    dx = ((w - 1 - bbox[2]) - bbox[0]) // 2
    im = ImageChops.offset(im, dx, 0)
    return im, last, gap0, dx


def build():
    src = Image.open(SRC).convert("RGBA")
    im, last, gap0, dx = derive(src)
    im.save(DST)
    print("derive: 末簇(“1”)=%s 擦除起点=%d 平移 dx=%+d" % (last, gap0, dx))
    print("wrote", DST)
    # 预览：上下拼
    pv = Image.new("RGBA", (src.width, src.height * 2 + 8), (24, 24, 24, 255))
    pv.paste(src, (0, 0))
    pv.paste(im, (0, src.height + 8))
    pv.save(PREVIEW)
    print("preview", PREVIEW)


def verify():
    src = Image.open(SRC).convert("RGBA")
    dst = Image.open(DST).convert("RGBA")
    ok = True

    def chk(name, cond, extra=""):
        nonlocal ok
        print(("PASS " if cond else "FAIL ") + name + (" " + extra if extra else ""))
        ok = ok and cond

    im, last, gap0, dx = derive(src)
    chk("rebuild-match", list(im.getdata()) == list(dst.getdata()))
    chk("size-unchanged", dst.size == src.size, "%s" % (dst.size,))
    # 「1」确实没了：平移 dx 后，擦除区 [gap0, w) 落在 [gap0+dx, w)（dx>0 时），
    # 另有左缘 dx 列回卷进来（也是透明）—— 这两段都不该有墨。
    w, h = dst.size
    a = dst.getchannel("A").load()
    right_clear = all(a[x, y] < A_ON
                      for x in (list(range(w - dx, w)) if dx > 0 else []) + list(range(gap0 + dx, w))
                      for y in range(h))
    chk("no-ink-right-of-gap", right_clear)
    # 居中
    b = dst.getbbox()
    lm, rm = b[0], w - 1 - b[2]
    chk("centered", abs(lm - rm) <= 1, "left=%d right=%d" % (lm, rm))
    # 垂直没动
    sb = src.getbbox()
    chk("v-unchanged", (b[1], b[3]) == (sb[1], sb[3]),
        "src y %s dst y %s" % ((sb[1], sb[3]), (b[1], b[3])))
    print("=>", "PASS" if ok else "FAIL")
    return 0 if ok else 1


if __name__ == "__main__":
    if "--verify" in sys.argv:
        sys.exit(verify())
    elif "--apply" in sys.argv:
        build()
        sys.exit(verify())
    else:
        build()
        print("（预览模式，产物已写 —— 正式落盘请 --apply；复核请 --verify）")
