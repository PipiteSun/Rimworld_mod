"""Mechanical vanilla-fit pass: shrink subject to vanilla size, thicken outer outline, desaturate."""
from PIL import Image, ImageFilter, ImageEnhance
TARGET = 92   # max subject dimension (vanilla median ~87x92)
def restyle(im, sat=1.0, outline=1.6):
    im = im.convert("RGBA")
    if im.size != (128, 128): im = im.resize((128, 128), Image.LANCZOS)
    bb = im.getchannel("A").point(lambda v: 255 if v > 40 else 0).getbbox()
    if not bb: return im
    w, h = bb[2] - bb[0], bb[3] - bb[1]
    K = 4
    sub = im.crop(bb)
    f = min(1.0, TARGET / max(w, h))
    nw, nh = max(1, round(w * f * K)), max(1, round(h * f * K))
    sub = sub.resize((nw, nh), Image.LANCZOS)
    if sat != 1.0:
        a = sub.getchannel("A")
        sub = ImageEnhance.Color(sub.convert("RGB")).enhance(sat).convert("RGBA"); sub.putalpha(a)
    big = Image.new("RGBA", (128 * K, 128 * K), (0, 0, 0, 0))
    pos = ((128 * K - nw) // 2, (128 * K - nh) // 2)
    big.paste(sub, pos)
    solid = big.getchannel("A").point(lambda v: 255 if v > 110 else 0)
    r = int(outline * K) * 2 + 1
    grown = solid
    left = r
    while left > 1:
        k = min(left, 9); k -= (k % 2 == 0)
        grown = grown.filter(ImageFilter.MaxFilter(k)); left -= (k - 1)
    out = Image.new("RGBA", big.size, (0, 0, 0, 0))
    out.paste(Image.new("RGBA", big.size, (0, 0, 0, 255)), (0, 0), grown)
    out.alpha_composite(big)
    return out.resize((128, 128), Image.LANCZOS)
