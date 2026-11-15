# -*- coding: utf-8 -*-
# 叙事者立绘：品红底抠掉 → 按内容外框等比缩放进 580x620（底边贴齐）→ 另存 116x124 小图。
# 用法: python install_portrait.py <src.png> <TexturesDir> <Name>
import sys, os
from PIL import Image

src, tex, name = sys.argv[1], sys.argv[2], sys.argv[3]
im = Image.open(src).convert("RGBA")
px = im.load()
w, h = im.size
for y in range(h):
    for x in range(w):
        r, g, b, a = px[x, y]
        m = min(r, b) - g  # 品红程度
        if m > 110 and r > 150 and b > 150:
            px[x, y] = (0, 0, 0, 0)
        elif m > 40 and r > 90 and b > 90:
            # 边缘半透明：按品红程度降 alpha，并去掉品红溢色
            alpha = max(0, min(255, int(255 * (110 - m) / 70)))
            k = min(r, b) - max(g, 0)
            nr, nb = max(0, r - k), max(0, b - k)
            px[x, y] = (nr, g, nb, min(a, alpha))
bbox = im.getbbox()
im = im.crop(bbox)
W, H = 580, 620
s = min(W / im.width, H / im.height)
im2 = im.resize((max(1, round(im.width * s)), max(1, round(im.height * s))), Image.LANCZOS)
out = Image.new("RGBA", (W, H), (0, 0, 0, 0))
out.paste(im2, ((W - im2.width) // 2, H - im2.height), im2)
d = os.path.join(tex, "UI", "HeroArt", "Storytellers")
os.makedirs(d, exist_ok=True)
out.save(os.path.join(d, name + ".png"))
out.resize((116, 124), Image.LANCZOS).save(os.path.join(d, name + "Tiny.png"))
prev = Image.new("RGBA", (W, H), (40, 40, 44, 255))
prev.alpha_composite(out)
prev.save(os.path.join(os.path.dirname(os.path.abspath(__file__)), name + "-preview.png"))
print("ok", bbox, im2.size)
