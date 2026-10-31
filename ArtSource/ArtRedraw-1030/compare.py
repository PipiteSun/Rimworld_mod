# 前后对比图：每格左旧右新。用法：compare.py <json 列表(含 path/backup/label)> <输出 png> [每行几组]
import json, sys
from PIL import Image, ImageDraw, ImageFont

rows = json.load(open(sys.argv[1], encoding="utf8"))
out = sys.argv[2]
cols = int(sys.argv[3]) if len(sys.argv) > 3 else 6
font = ImageFont.truetype("C:/Windows/Fonts/msyh.ttc", 13)
cell = 112
gw = cell * 2 + 8
H = (len(rows) + cols - 1) // cols
im = Image.new("RGB", (cols * gw, H * (cell + 20)), (110, 104, 96))
d = ImageDraw.Draw(im)
for i, r in enumerate(rows):
    x, y = (i % cols) * gw, (i // cols) * (cell + 20)
    for k, p in enumerate((r["backup"], r["path"])):
        t = Image.open(p).convert("RGBA").resize((cell, cell), Image.LANCZOS)
        im.paste(t, (x + k * cell, y), t)
    d.text((x + 4, y + cell), r.get("label", "")[:12], fill=(255, 255, 255), font=font)
im.save(out)
