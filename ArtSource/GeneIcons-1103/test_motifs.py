import sys, os
sys.path.insert(0, os.path.dirname(__file__))
import motifs as M
from genelib import Icon
from PIL import Image
out = sys.argv[1]
names = [n for n in dir(M) if callable(getattr(M, n)) and getattr(M, n).__module__ == 'motifs'
         and n not in ('star_pts', 'sparkle', 'horn', 'eyes', 'footprint')]
tiles = []
for n in names:
    c = Icon()
    try:
        getattr(M, n)(c)
    except Exception as e:
        print(n, 'ERR', e)
        continue
    p = os.path.join(os.path.dirname(out), '_t.png')
    c.save(p)
    tiles.append(Image.open(p).copy())
cols = 10
sheet = Image.new('RGBA', (cols * 136, ((len(tiles) + cols - 1) // cols) * 136), (52, 52, 52, 255))
for i, t in enumerate(tiles):
    sheet.alpha_composite(t, ((i % cols) * 136 + 4, (i // cols) * 136 + 4))
sheet.save(out)
print(names)
