# 前后对比图：每格左旧右新。用法：compare.py <新图根目录> <输出png> <cell> <相对路径通配...>
import glob, os, sys
from PIL import Image, ImageDraw

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(os.path.dirname(HERE))
BACKUP = os.path.join(ROOT, "ArtSource", "backup-art-1102")
new_root, out, cell = sys.argv[1], sys.argv[2], int(sys.argv[3])
rels = []
for g in sys.argv[4:]:
    rels += sorted(os.path.relpath(p, BACKUP) for p in glob.glob(os.path.join(BACKUP, g)))
cols = 6
rows = (len(rels) + cols - 1) // cols
sheet = Image.new("RGB", (cols * cell * 2, rows * (cell + 12)), (112, 106, 98))
d = ImageDraw.Draw(sheet)
for i, r in enumerate(rels):
    x, y = (i % cols) * cell * 2, (i // cols) * (cell + 12)
    for j, root in enumerate((BACKUP, new_root)):
        p = os.path.join(root, r)
        if not os.path.exists(p):
            continue
        im = Image.open(p).convert("RGBA")
        im = im.resize((cell, cell), Image.LANCZOS)
        sheet.paste(im, (x + j * cell, y), im)
    d.text((x + 2, y + cell), os.path.basename(r)[:30], fill=(235, 235, 235))
sheet.save(out)
print(len(rels))
