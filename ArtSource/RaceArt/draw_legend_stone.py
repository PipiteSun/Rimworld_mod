# 1.50.0 传奇重铸之石图标：暗色多面晶石 + 金色裂纹与光晕（128x128，透明底）。
import math, os, sys
from PIL import Image, ImageDraw, ImageFilter

S = 128
out = sys.argv[1] if len(sys.argv) > 1 else os.path.join(os.path.dirname(__file__), '..', '..', 'Textures', 'LP', 'Items', 'LegendRespecStone.png')

img = Image.new('RGBA', (S, S), (0, 0, 0, 0))
# 光晕
glow = Image.new('RGBA', (S, S), (0, 0, 0, 0))
gd = ImageDraw.Draw(glow)
gd.ellipse((22, 26, 106, 110), fill=(255, 190, 70, 120))
glow = glow.filter(ImageFilter.GaussianBlur(12))
img.alpha_composite(glow)

d = ImageDraw.Draw(img)
cx, cy = 64, 68
outline = [(64, 18), (96, 40), (102, 78), (78, 108), (48, 108), (26, 80), (32, 40)]
d.polygon(outline, fill=(52, 44, 70, 255), outline=(20, 16, 28, 255))
# 刻面（中心向各顶点）
shades = [(78, 66, 102), (64, 54, 88), (44, 36, 60), (36, 30, 50), (46, 38, 64), (60, 50, 82), (86, 74, 112)]
center = (62, 62)
for i in range(len(outline)):
    a, b = outline[i], outline[(i + 1) % len(outline)]
    d.polygon([center, a, b], fill=shades[i] + (255,))
for p in outline:
    d.line([center, p], fill=(24, 20, 34, 255), width=1)
d.polygon(outline, outline=(18, 14, 26, 255))
# 金色裂纹
crack = [(62, 30), (58, 46), (66, 58), (60, 72), (70, 86), (64, 100)]
branch = [(66, 58), (82, 62), (90, 74)]
branch2 = [(60, 72), (44, 76), (38, 88)]
for line, w in ((crack, 4), (branch, 3), (branch2, 3)):
    d.line(line, fill=(255, 196, 80, 255), width=w, joint='curve')
for line in (crack, branch, branch2):
    d.line(line, fill=(255, 244, 200, 255), width=1)
# 高光
d.polygon([(64, 18), (52, 30), (60, 34)], fill=(160, 150, 190, 200))

os.makedirs(os.path.dirname(os.path.abspath(out)), exist_ok=True)
img.save(out)
print(out)
