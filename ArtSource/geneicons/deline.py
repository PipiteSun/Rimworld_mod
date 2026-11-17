"""Remove interior black linework so parts read as one piece; keep only the outer black contour.
Junctions between different colours: line removed (colours meet). Lines inside one colour: kept as a thin darker tone."""
from PIL import Image, ImageFilter, ImageChops
Z = 3
def _mx(m, r):
    left = int(r) * 2 + 1
    while left > 1:
        k = min(left, 9); k -= (k % 2 == 0)
        m = m.filter(ImageFilter.MaxFilter(k)); left -= (k - 1)
    return m
def _mn(m, r):
    return ImageChops.invert(_mx(ImageChops.invert(m), r))
def _bin(m, t=127):
    return m.point(lambda v: 255 if v > t else 0)
OFFS = [(1, 0), (-1, 0), (0, 1), (0, -1), (1, 1), (-1, -1), (1, -1), (-1, 1)]
def _spread(rgb, known, iters):
    for _ in range(iters):
        add_rgb = rgb.copy(); add_k = known.copy()
        for dx, dy in OFFS:
            sk = ImageChops.offset(known, dx, dy)
            new = ImageChops.subtract(sk, add_k)
            if new.getbbox() is None: continue
            add_rgb.paste(ImageChops.offset(rgb, dx, dy), (0, 0), new)
            add_k = ImageChops.lighter(add_k, new)
        rgb, known = add_rgb, add_k
    return rgb
def deline(im, band=4.0, line=3.2, seam=0.66, lum=70, same_tol=46):
    im = im.convert("RGBA")
    if im.size != (128, 128): im = im.resize((128, 128), Image.LANCZOS)
    big = im.resize((128 * Z, 128 * Z), Image.BICUBIC)
    r, g, b, a = big.split()
    solid = _bin(a, 110)
    mxc = ImageChops.lighter(ImageChops.lighter(r, g), b)
    dark = ImageChops.darker(mxc.point(lambda v: 255 if v < lum else 0), solid)
    outside = ImageChops.invert(solid)
    outer = ImageChops.darker(dark, _mx(outside, band * Z))
    inner = ImageChops.subtract(dark, outer)
    thick = _mx(_mn(inner, line * Z / 2), line * Z / 2 + Z)
    core = ImageChops.subtract(inner, thick)
    if core.getbbox() is None: return im
    lines = ImageChops.darker(_mx(core, 2), ImageChops.invert(_mx(outer, 1)))
    lines = ImageChops.darker(lines, solid)
    known = ImageChops.subtract(ImageChops.subtract(solid, _mx(dark, 2)), lines)
    rgb = Image.merge("RGB", (r, g, b))
    filled = _spread(rgb, known, int(line * Z) + 4)
    # same-colour test: local range of the filled image across the line
    k = int(line * Z) | 1
    hi = filled.filter(ImageFilter.MaxFilter(k + 4)); lo = filled.filter(ImageFilter.MinFilter(k + 4))
    d = ImageChops.subtract(hi, lo).split()
    rng = ImageChops.lighter(ImageChops.lighter(d[0], d[1]), d[2])
    same = rng.point(lambda v: 255 if v < same_tol else 0)
    out = big.copy()
    fa = filled.convert("RGBA"); fa.putalpha(a)
    out.paste(fa, (0, 0), lines)
    thin = ImageChops.darker(_mn(core, 1), same)
    sm = filled.point(lambda v: int(v * seam)).convert("RGBA"); sm.putalpha(a)
    out.paste(sm, (0, 0), thin.filter(ImageFilter.GaussianBlur(0.6)))
    return out.resize((128, 128), Image.LANCZOS)
