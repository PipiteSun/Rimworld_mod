"""Vanilla-style RimWorld gene icon renderer (supersampled vector art)."""
import math
from PIL import Image, ImageDraw, ImageFilter, ImageChops

K = 4            # supersample
N = 128 * K
OUTER = 4.0      # outer outline px (128-space)
INNER = 1.7      # inner part separation


def P(pts):
    return [(x * K, y * K) for x, y in pts]


def blank():
    return Image.new("L", (N, N), 0)


def smooth(pts, steps=10, closed=True):
    """Catmull-Rom through pts."""
    out = []
    n = len(pts)
    rng = range(n) if closed else range(n - 1)
    for i in rng:
        p0 = pts[(i - 1) % n] if closed or i > 0 else pts[i]
        p1 = pts[i]
        p2 = pts[(i + 1) % n]
        p3 = pts[(i + 2) % n] if closed or i + 2 < n else pts[(i + 1) % n]
        for s in range(steps):
            t = s / steps
            t2, t3 = t * t, t * t * t
            x = 0.5 * ((2 * p1[0]) + (-p0[0] + p2[0]) * t + (2 * p0[0] - 5 * p1[0] + 4 * p2[0] - p3[0]) * t2 + (-p0[0] + 3 * p1[0] - 3 * p2[0] + p3[0]) * t3)
            y = 0.5 * ((2 * p1[1]) + (-p0[1] + p2[1]) * t + (2 * p0[1] - 5 * p1[1] + 4 * p2[1] - p3[1]) * t2 + (-p0[1] + 3 * p1[1] - 3 * p2[1] + p3[1]) * t3)
            out.append((x, y))
    return out


def poly(pts, sm=False):
    m = blank()
    if sm:
        pts = smooth(pts)
    ImageDraw.Draw(m).polygon(P(pts), fill=255)
    return m


def ell(cx, cy, rx, ry=None):
    ry = rx if ry is None else ry
    m = blank()
    ImageDraw.Draw(m).ellipse([(cx - rx) * K, (cy - ry) * K, (cx + rx) * K, (cy + ry) * K], fill=255)
    return m


def rect(x0, y0, x1, y1, r=0):
    m = blank()
    ImageDraw.Draw(m).rounded_rectangle([x0 * K, y0 * K, x1 * K, y1 * K], radius=r * K, fill=255)
    return m


def U(*ms):
    out = ms[0]
    for m in ms[1:]:
        out = ImageChops.lighter(out, m)
    return out


def I(a, b):
    return ImageChops.darker(a, b)


def D(a, b):
    return ImageChops.subtract(a, b)


def shift(m, dx, dy):
    return ImageChops.offset(m, int(dx * K), int(dy * K))


def dilate(m, r):
    s = int(r * K) * 2 + 1
    # MaxFilter with big kernel is slow; do repeated passes of 9
    out = m
    left = s
    while left > 1:
        k = min(left, 9)
        if k % 2 == 0:
            k -= 1
        out = out.filter(ImageFilter.MaxFilter(k))
        left -= (k - 1)
    return out


def darker(c, f):
    return tuple(int(v * f) for v in c)


def lighter(c, f):
    return tuple(int(v + (255 - v) * f) for v in c)


class Icon:
    def __init__(self):
        self.parts = []   # (mask, color, shade)
        self.lines = []   # (mask, color)

    def add(self, mask, color, shade=6, hi=3, shadef=0.72):
        self.parts.append((mask, color, shade, hi, shadef))
        return mask

    def line(self, pts, w=2.2, color=(0, 0, 0), clip=None, sm=False):
        m = blank()
        if sm:
            pts = smooth(pts, closed=False)
        ImageDraw.Draw(m).line(P(pts), fill=255, width=int(w * K), joint="curve")
        if clip is not None:
            m = I(m, clip)
        self.lines.append((m, color))

    def dots(self, mask, color):
        self.lines.append((mask, color))

    def render(self):
        img = Image.new("RGBA", (N, N), (0, 0, 0, 0))
        union = U(*[p[0] for p in self.parts])
        black = Image.new("RGBA", (N, N), (0, 0, 0, 255))
        img.paste(black, (0, 0), dilate(union, OUTER))
        prev = None
        for mask, color, shade, hi, shadef in self.parts:
            seam = I(dilate(mask, INNER), prev) if prev is not None else None
            prev = mask if prev is None else U(prev, mask)
            img.paste(Image.new("RGBA", (N, N), color + (255,)), (0, 0), mask)
            if shade:
                inside = D(mask, dilate(D(blank().point(lambda v: 255), mask), 2.2))
                h = I(D(mask, shift(mask, shade, shade * 0.3)), inside)
                img.paste(Image.new("RGBA", (N, N), lighter(color, 0.30) + (255,)), (0, 0), h)
        for m, c in self.lines:
            img.paste(Image.new("RGBA", (N, N), c + (255,)), (0, 0), m)
        return img.resize((128, 128), Image.LANCZOS)


# ---------- shared shapes ----------

def bust(head=(64, 50, 19, 22), shoulders=True):
    hx, hy, rx, ry = head
    h = ell(hx, hy, rx, ry)
    neck = rect(hx - 10, hy + 8, hx + 10, hy + 32)
    body = I(ell(64, 120, 42, 40), rect(0, 0, 128, 112))
    return h, U(neck, body)


def flame(cx, base, w, h, lean=0):
    pts = [
        (cx - w * 0.5, base - h * 0.30),
        (cx - w * 0.42, base - h * 0.62),
        (cx - w * 0.18, base - h * 0.80 + 2),
        (cx - w * 0.22 + lean * 0.3, base - h * 0.55),
        (cx + lean * 0.2, base - h * 0.80),
        (cx + lean, base - h),
        (cx + w * 0.30 + lean * 0.5, base - h * 0.70),
        (cx + w * 0.30, base - h * 0.52),
        (cx + w * 0.48, base - h * 0.62),
        (cx + w * 0.52, base - h * 0.30),
        (cx + w * 0.30, base - h * 0.03),
        (cx, base),
        (cx - w * 0.30, base - h * 0.03),
    ]
    return poly(pts, sm=True)


def bolt(dx=0, dy=0, s=1.0):
    pts = [(72, 12), (38, 68), (60, 68), (48, 116), (92, 52), (69, 52), (86, 12)]
    pts = [(64 + (x - 64) * s + dx, 64 + (y - 64) * s + dy) for x, y in pts]
    return poly(pts)


def cloud(cx, cy, s=1.0):
    cs = [(-22, 4, 14), (-6, -6, 18), (14, -2, 15), (26, 8, 11), (0, 8, 15)]
    m = U(*[ell(cx + x * s, cy + y * s, r * s) for x, y, r in cs])
    return U(m, rect(cx - 30 * s, cy + 2 * s, cx + 34 * s, cy + 19 * s, 9 * s))


def shield():
    pts = [(28, 22), (64, 14), (100, 22), (100, 60), (92, 86), (64, 114), (36, 86), (28, 60)]
    return poly(pts, sm=True)


def snowflake(ic, cx, cy, r, w=2.4, color=(255, 255, 255)):
    for a in range(0, 180, 60):
        t = math.radians(a + 90)
        ic.line([(cx - r * math.cos(t), cy - r * math.sin(t)), (cx + r * math.cos(t), cy + r * math.sin(t))], w, color)
    for a in range(0, 360, 60):
        t = math.radians(a + 90)
        bx, by = cx + r * 0.6 * math.cos(t), cy + r * 0.6 * math.sin(t)
        for s in (-1, 1):
            t2 = t + s * math.radians(40)
            ic.line([(bx, by), (bx + r * 0.32 * math.cos(t2), by + r * 0.32 * math.sin(t2))], w * 0.85, color)


EYE = (232, 200, 92)


def eyes(ic, cx, cy, gap=7, color=EYE):
    for s in (-1, 1):
        ic.dots(poly([(cx + s * gap - 4, cy - 1), (cx + s * gap + 4, cy - 1), (cx + s * gap + (2 if s < 0 else -2), cy + 2.5), (cx + s * gap + (-2 if s < 0 else 2), cy + 2.5)]), color)


# ---------- icons ----------

def stroke(pts, w):
    m = blank()
    d = ImageDraw.Draw(m)
    d.line(P(pts), fill=255, width=int(w * K), joint="curve")
    for x, y in (pts[0], pts[-1]):
        d.ellipse([(x - w / 2) * K, (y - w / 2) * K, (x + w / 2) * K, (y + w / 2) * K], fill=255)
    return m


def fire_giant():
    ic = Icon()
    ic.add(flame(64, 100, 70, 84, lean=3), (196, 112, 66), shade=6)
    ic.add(flame(64, 100, 36, 44, lean=1), (236, 170, 110), shade=0)
    rock = poly([(26, 112), (30, 98), (44, 92), (64, 96), (84, 92), (98, 98), (102, 112)], sm=False)
    ic.add(rock, (92, 84, 84), shade=4)
    return ic


def cloud_giant():
    ic = Icon()
    ic.add(cloud(62, 66, 1.45), (224, 226, 232), shade=6)
    ic.line([(40, 78), (56, 80), (66, 72), (60, 64), (52, 68)], 3.0, (150, 154, 170), sm=True)
    return ic


def frost_giant():
    ic = Icon()
    arms = []
    for a in range(0, 360, 60):
        t = math.radians(a - 90)
        c, sn = math.cos(t), math.sin(t)
        arms.append(stroke([(64, 64), (64 + 40 * c, 64 + 40 * sn)], 9))
        bx, by = 64 + 25 * c, 64 + 25 * sn
        for sgn in (-1, 1):
            t2 = t + sgn * math.radians(50)
            arms.append(stroke([(bx, by), (bx + 13 * math.cos(t2), by + 13 * math.sin(t2))], 7))
    ic.add(U(*arms), (176, 206, 226), shade=4)
    return ic


def storm_giant():
    ic = Icon()
    ic.add(bolt(2, 30, 0.52), (222, 188, 86), shade=4)
    ic.add(cloud(62, 46, 1.3), (110, 116, 142), shade=6)
    return ic


def ettin():
    ic = Icon()
    col = (236, 236, 236)
    neck = poly([(50, 82), (78, 82), (82, 110), (46, 110)])
    ic.add(neck, col, shade=0)
    for cx in (44, 84):
        ic.add(ell(cx, 54, 19, 23), col, shade=5)
        eyes(ic, cx, 56, 7, (120, 120, 126))
    return ic


def cyclops():
    ic = Icon()
    eye = I(ell(64, 18, 64, 62), ell(64, 110, 64, 62))
    ic.add(eye, (226, 220, 206), shade=0)
    ic.add(ell(64, 64, 23), (168, 110, 78), shade=5)
    ic.dots(ell(64, 64, 10), (0, 0, 0))
    ic.dots(ell(57, 57, 3.6), (255, 255, 255))
    return ic


def abyssal_hide():
    ic = Icon()
    sh = shield()
    col = (74, 122, 116)
    ic.add(sh, col, shade=8)
    inner = D(sh, dilate(D(blank().point(lambda v: 255), sh), 3))
    for row in range(5):
        y = 32 + row * 15
        off = 0 if row % 2 == 0 else 9
        for i in range(-1, 6):
            x = 28 + off + i * 18
            pts = [(x + 9 * math.cos(math.radians(a)), y + 9 * math.sin(math.radians(a))) for a in range(0, 181, 15)]
            ic.line(pts, 2.0, (34, 62, 60), clip=inner)
    return ic


def storm_body():
    ic = Icon()
    ic.add(bolt(0, 0, 0.92), (226, 190, 82), shade=7)
    return ic


def cold_blood():
    ic = Icon()
    drop = U(ell(64, 80, 28), poly([(38, 70), (64, 14), (90, 70)]))
    drop = I(drop, poly([(30, 90), (64, 12), (98, 90), (98, 120), (30, 120)], sm=False))
    ic.add(drop, (118, 160, 186), shade=9)
    snowflake(ic, 64, 80, 12, 2.6, (236, 244, 250))
    return ic


def dwarf_toughness():
    ic = Icon()
    iron = (128, 132, 138)
    top = poly([(18, 44), (46, 40), (106, 40), (106, 58), (88, 62), (40, 62), (26, 54)], sm=False)
    waist = poly([(50, 62), (80, 62), (74, 82), (56, 82)])
    foot = poly([(36, 96), (46, 82), (84, 82), (94, 96)])
    ic.add(U(top, waist, foot), iron, shade=6)
    ic.line([(48, 49), (100, 49)], 1.4, (180, 184, 190))
    # hammer above
    ic.add(rect(58, 14, 62, 36, 1), (140, 100, 70), shade=0, hi=0)
    ic.add(rect(46, 14, 76, 26, 2), (150, 154, 160), shade=3)
    return ic


def abyss_fire():
    ic = Icon()
    ic.add(flame(64, 112, 72, 96, lean=4), (140, 64, 120), shade=8)
    ic.add(flame(64, 112, 38, 52, lean=2), (196, 102, 150), shade=0, hi=0)
    return ic


def abyss_core():
    ic = Icon()
    skin = (150, 64, 60)
    h, b = bust((64, 52, 18, 21))
    horn = (206, 192, 162)
    for s in (-1, 1):
        pts = [(64 + s * 12, 40), (64 + s * 18, 30), (64 + s * 22, 20), (64 + s * 30, 12), (64 + s * 28, 26), (64 + s * 24, 38), (64 + s * 18, 46)]
        ic.add(poly(pts, sm=True), horn, shade=3)
    ic.add(b, skin)
    ic.add(h, skin, shade=6)
    eyes(ic, 64, 53, 7, EYE)
    return ic


def arc(cx, cy, r, a0, a1, w):
    pts = [(cx + r * math.cos(math.radians(t)), cy + r * math.sin(math.radians(t))) for t in range(a0, a1 + 1, 6)]
    return stroke(pts, w)


def tentacles(cx, top, n=4, spread=30, length=40, w=9):
    ms = []
    for i in range(n):
        f = (i / (n - 1) - 0.5) * 2
        x0 = cx + f * spread * 0.45
        pts = [(x0, top), (x0 + f * 6, top + length * 0.4), (x0 + f * 14 - 3 * (1 if f >= 0 else -1), top + length * 0.75), (x0 + f * 12, top + length)]
        ms.append(stroke(smooth(pts, closed=False) + [pts[-1]], w))
    return U(*ms)


PURPLE = (150, 122, 176)


def half_illithid():
    ic = Icon()
    head = ell(64, 44, 21, 25)
    body = I(ell(64, 124, 44, 44), rect(0, 0, 128, 110))
    ic.add(U(head, body, rect(54, 60, 74, 90)), (236, 236, 236), shade=6)
    ic.add(U(I(head, rect(64, 0, 128, 128)), I(tentacles(70, 58, 3, 26, 40, 8), rect(64, 0, 128, 128)), tentacles(70, 62, 3, 26, 38, 8)), PURPLE, shade=0)
    return ic


def psionic_sense():
    ic = Icon()
    ic.add(arc(64, 62, 44, 215, 325, 8), (186, 164, 206), shade=0)
    ic.add(U(ell(64, 62, 22, 23), tentacles(64, 74, 4, 44, 36, 9)), PURPLE, shade=5)
    for sx in (-8, 8):
        ic.dots(ell(64 + sx, 62, 3.6), (240, 236, 244))
    return ic


def frost_wolf_lord():
    ic = Icon()
    fur = (196, 212, 226)
    head = poly([(28, 30), (48, 50), (80, 50), (100, 30), (98, 72), (64, 112), (30, 72)])
    ic.add(head, fur, shade=6)
    ic.add(poly([(48, 78), (80, 78), (64, 108)]), (240, 242, 244), shade=0)
    ic.dots(poly([(58, 98), (70, 98), (64, 107)]), (40, 44, 52))
    for s_ in (-1, 1):
        ic.dots(poly([(64 + s_ * 26, 62), (64 + s_ * 10, 68), (64 + s_ * 24, 70)]), (70, 88, 110))
    ic.add(poly([(48, 50), (48, 28), (56, 38), (64, 22), (72, 38), (80, 28), (80, 50)]), (206, 172, 84), shade=3)
    ic.dots(ell(64, 42, 3.4), (170, 70, 66))
    return ic


ICONS = {
    "FireGiant/FireGiantGene": ("熔火巨人本源", fire_giant),
    "Giants/Genes/CloudGiantCore": ("云巨人本源", cloud_giant),
    "Giants/Genes/FrostGiantCore": ("霜巨人本源", frost_giant),
    "Giants/Genes/StormGiantCore": ("风暴巨人本源", storm_giant),
    "HARGiants/Ettin/Genes/Core": ("双头巨人本源", ettin),
    "HARGiants/Cyclops/Genes/Core": ("独眼巨人本源", cyclops),
    "Marilith/Genes/AbyssalHide": ("深渊之躯", abyssal_hide),
    "Giants/Genes/StormBody": ("雷电之躯", storm_body),
    "Marilith/Genes/ColdBlood": ("冷血", cold_blood),
    "CommonRaces/Genes/DwarfToughness": ("矮人坚韧", dwarf_toughness),
    "Abyss/Genes/CambionFire": ("深渊之火", abyss_fire),
    "Abyss/Genes/AbyssCore": ("深渊之核", abyss_core),
    "MindFlayer/Genes/HalfIllithid": ("半夺心魔", half_illithid),
    "MindFlayer/Genes/PsionicSense": ("灵能感知", psionic_sense),
    "Giants/Genes/FrostWolfLord": ("冬狼之主", frost_wolf_lord),
}
