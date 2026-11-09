# 1.137.0 技能分组切换按钮图标：三张错开的法术卡（蓝 / 金 / 红），上面一张有星芒。
# 用法：python draw_ability_groups.py  → Textures/LP/Classes/AbilityGroups.png
import os
from PIL import Image, ImageDraw

S = 4  # 超采样
W = 128
here = os.path.dirname(os.path.abspath(__file__))
out = os.path.join(here, "..", "Textures", "LP", "Classes", "AbilityGroups.png")

im = Image.new("RGBA", (W * S, W * S), (0, 0, 0, 0))
d = ImageDraw.Draw(im)

def card(x, y, w, h, fill, edge):
    r = 10 * S
    d.rounded_rectangle([x * S, y * S, (x + w) * S, (y + h) * S], r, fill=(20, 18, 24, 255))
    o = 5
    d.rounded_rectangle([(x + o) * S, (y + o) * S, (x + w - o) * S, (y + h - o) * S], r - 3 * S, fill=fill, outline=edge, width=3 * S)

card(10, 30, 62, 84, (70, 105, 170, 255), (150, 190, 240, 255))
card(34, 20, 62, 84, (185, 145, 60, 255), (245, 215, 130, 255))
card(58, 10, 62, 84, (160, 60, 60, 255), (235, 140, 120, 255))

# 上面那张的星芒
cx, cy = 89, 52
pts = []
import math
for i in range(16):
    a = math.pi / 8 * i - math.pi / 2
    rr = 22 if i % 2 == 0 else 7
    pts.append(((cx + rr * math.cos(a)) * S, (cy + rr * math.sin(a)) * S))
d.polygon(pts, fill=(255, 240, 200, 255), outline=(30, 20, 20, 255))

im = im.resize((W, W), Image.LANCZOS)
im.save(out)
print("wrote", out)
