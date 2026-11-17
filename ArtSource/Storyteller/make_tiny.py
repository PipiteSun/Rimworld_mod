# -*- coding: utf-8 -*-
# 叙事者列表小头像：从原画（品红底）抠图 → 按脸部裁剪框（116:124）裁出 → 缩成 116x124，构图对齐原版（眉到下巴撑满）。
# 用法: python make_tiny.py <src.png> <out_Tiny.png> x0 y0 width   （高度按 124/116 自动算）
import sys
from PIL import Image


def key_magenta(im):
    px = im.load()
    w, h = im.size
    for y in range(h):
        for x in range(w):
            r, g, b, a = px[x, y]
            m = min(r, b) - g
            if m > 110 and r > 150 and b > 150:
                px[x, y] = (0, 0, 0, 0)
            elif m > 40 and r > 90 and b > 90:
                alpha = max(0, min(255, int(255 * (110 - m) / 70)))
                k = min(r, b) - max(g, 0)
                px[x, y] = (max(0, r - k), g, max(0, b - k), min(a, alpha))
    return im


def make(src, out, x0, y0, w):
    h = round(w * 124 / 116)
    im = Image.open(src).convert("RGBA").crop((x0, y0, x0 + w, y0 + h))
    im = key_magenta(im)
    im = im.resize((116, 124), Image.LANCZOS)
    im.save(out)
    return im


if __name__ == "__main__":
    make(sys.argv[1], sys.argv[2], int(sys.argv[3]), int(sys.argv[4]), int(sys.argv[5]))
    print("ok", sys.argv[2])
