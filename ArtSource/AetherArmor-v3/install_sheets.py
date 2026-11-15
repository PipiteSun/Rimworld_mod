# Cut 3-view magenta sheets into LP AetherArmor textures.
# Each new view is stretched into the alpha bbox of the currently installed texture
# (keeps the per-body-type fit worked out for 1.150.0). Masks regenerated (grey metal -> red).
# usage: python install_sheets.py <ModRoot> <SheetDir>
import sys, os
import numpy as np
from PIL import Image
from scipy import ndimage

MOD, SRC = sys.argv[1], sys.argv[2]
TEX = os.path.join(MOD, 'Textures', 'LP', 'AetherArmor')
BODIES = ['Male', 'Female', 'Thin', 'Fat', 'Hulk']
# name, sheet, mirror east?, has mask, is body armor
ITEMS = [
    ('BastionArmor', 'Armors/BastionArmor-sheet-v3.png', True, True, True),
    ('ArtificerArmor', 'Armors/ArtificerArmor-sheet-v13.png', True, False, True),
    ('BastionHelm', 'Helmets/BastionHelmet-sheet-v3.png', False, True, False),
    ('AcolyteHelm', 'Helmets/AcolyteHelmet-sheet-v4.png', False, False, False),
]


def key(path):
    a = np.asarray(Image.open(path).convert('RGB')).astype(np.int16)
    r, g, b = a[..., 0], a[..., 1], a[..., 2]
    mn = np.minimum(r, b)
    pink = ((mn - g > 20) & (r - g > 50)) | ((r > 170) & (r - g > 60) & (r - b > 40))  # magenta or salmon bg
    fg = ~pink
    fg = ndimage.binary_opening(fg, iterations=2)
    lab, n = ndimage.label(fg)
    sizes = ndimage.sum(fg, lab, range(1, n + 1))
    keep = np.isin(lab, 1 + np.nonzero(sizes > 4000)[0])
    keep = ndimage.binary_fill_holes(keep) & fg | (keep)
    keep = ndimage.binary_erosion(keep, iterations=1)
    # decontaminate leftover pink tint on the fringe
    over = np.clip(mn - g - 8, 0, None)
    a2 = a.copy()
    a2[..., 0] -= over
    a2[..., 2] -= over
    a2 = np.clip(a2, 0, 255).astype(np.uint8)
    alpha = (keep * 255).astype(np.uint8)
    return np.dstack([a2, alpha])


def split3(rgba):
    m = rgba[..., 3] > 0
    cols = m.any(0)
    runs, x, w = [], 0, len(cols)
    while x < w:
        if cols[x]:
            s = x
            while x < w and cols[x]:
                x += 1
            runs.append((s, x))
        x += 1
    runs = sorted(runs, key=lambda r: r[1] - r[0], reverse=True)[:3]
    runs.sort()
    assert len(runs) == 3, runs
    out = []
    for s, e in runs:
        sub = rgba[:, s:e]
        rows = np.nonzero(sub[..., 3].any(1))[0]
        out.append(Image.fromarray(sub[rows[0]:rows[-1] + 1]))
    return out  # south, east, north


def fit(view, box, size):
    x0, y0, x1, y1 = box
    v = view.convert('RGBa').resize((x1 - x0, y1 - y0), Image.LANCZOS).convert('RGBA')
    canvas = Image.new('RGBA', (size, size), (0, 0, 0, 0))
    canvas.paste(v, (x0, y0), v)
    return canvas


def fit_icon(view, box, size):
    x0, y0, x1, y1 = box
    bw, bh = x1 - x0, y1 - y0
    s = min(bw / view.width, bh / view.height)
    w, h = round(view.width * s), round(view.height * s)
    v = view.convert('RGBa').resize((w, h), Image.LANCZOS).convert('RGBA')
    canvas = Image.new('RGBA', (size, size), (0, 0, 0, 0))
    canvas.paste(v, ((size - w) // 2, y0 + (bh - h) // 2), v)
    return canvas


def mask_of(img):
    a = np.asarray(img).astype(np.int16)
    r, g, b, al = a[..., 0], a[..., 1], a[..., 2], a[..., 3]
    mx, mn = np.maximum(np.maximum(r, g), b), np.minimum(np.minimum(r, g), b)
    sat = (mx - mn) / np.maximum(mx, 1)
    grey = (al > 40) & (sat < 0.16) & (mx > 55) & (mx < 215)
    m = np.zeros(a.shape[:2] + (3,), np.uint8)
    m[grey, 0] = 255
    return Image.fromarray(m, 'RGB')


def bbox(path):
    return Image.open(path).convert('RGBA').getchannel('A').getbbox()


def save(img, path, has_mask):
    img.save(path)
    if has_mask:
        mk = mask_of(img)
        base = path[:-4]
        mk.save(base + '_m.png')
        mk.save(base + 'm.png')


ONLY = sys.argv[3:]
for name, sheet, mirror, has_mask, body in ITEMS:
    if ONLY and name not in ONLY:
        continue
    s, e, n = split3(key(os.path.join(SRC, sheet)))
    if mirror:
        e = e.transpose(Image.FLIP_LEFT_RIGHT)
    views = {'south': s, 'east': e, 'north': n}
    d = os.path.join(TEX, name)
    for dirn, v in views.items():
        for b in (BODIES if body else [None]):
            fn = f'{name}_{b}_{dirn}.png' if b else f'{name}_{dirn}.png'
            p = os.path.join(d, fn)
            save(fit(v, bbox(p), 256), p, has_mask)
    ip = os.path.join(d, f'{name}_Icon.png')
    save(fit_icon(s, bbox(ip), 128), ip, has_mask)
    print(name, 'views', [v.size for v in (s, e, n)])
