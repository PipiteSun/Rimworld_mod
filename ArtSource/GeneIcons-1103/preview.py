# Before/after comparison sheets: preview.py <old_root> <new_root> <out_prefix> [per_sheet]
import json, os, sys
from PIL import Image, ImageDraw
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from gene_icons import ICONS

old_root, new_root, out = sys.argv[1], sys.argv[2], sys.argv[3]
per = int(sys.argv[4]) if len(sys.argv) > 4 else 40
paths = list(ICONS)
cell, cols = 84, 8
for si in range(0, len(paths), per):
    chunk = paths[si:si + per]
    rows = (len(chunk) + cols - 1) // cols
    sheet = Image.new('RGBA', (cols * cell * 2 + cols * 8, rows * cell), (52, 52, 52, 255))
    for i, p in enumerate(chunk):
        x = (i % cols) * (cell * 2 + 8)
        y = (i // cols) * cell
        for j, root in enumerate((old_root, new_root)):
            f = os.path.join(root, p.replace('/', os.sep) + '.png')
            if os.path.exists(f):
                im = Image.open(f).convert('RGBA').resize((cell - 6, cell - 6), Image.LANCZOS)
                sheet.alpha_composite(im, (x + j * cell + 3, y + 3))
        ImageDraw.Draw(sheet).line([(x + cell * 2 + 3, y), (x + cell * 2 + 3, y + cell)], fill=(90, 90, 90), width=2)
    sheet.save(f"{out}{si // per + 1}.png")
    print(f"{out}{si // per + 1}.png", len(chunk))
