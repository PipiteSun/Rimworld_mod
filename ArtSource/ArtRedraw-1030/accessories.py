# 2026-10-30 DND 饰品图标重画（戒指 / 护符 / 念珠 / 护腕 / 手套 / 靴子 / 印记 / 宝石 / 号角 ...），RimWorld 原版画风。
# 只重画物品图标（graphicData.texPath，128 画布坐标），贴图尺寸与旧图一致。
# 用法：E:\Python\python.exe ArtSource\ArtRedraw-1030\accessories.py <模组根目录> [defName ...]
import json, math, os, sys, shutil

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
sys.path.insert(0, os.path.join(os.path.dirname(HERE), "ArtRedraw-1029"))
from rwart import Canvas, mul, mix  # noqa: E402
from PIL import Image  # noqa: E402
import numpy as np  # noqa: E402


def bbox(path):
    im = Image.open(path).convert('RGBA')
    b = im.split()[3].point(lambda v: 255 if v > 50 else 0).getbbox()
    s = 128 / im.width
    return tuple(v * s for v in b)

ROOT = sys.argv[1] if len(sys.argv) > 1 else os.getcwd()
ONLY = set(sys.argv[2:])

GOLD, GOLD_D, SILVER, IRON, DARK = '#cfae62', '#9a7a3e', '#c4c9cf', '#6a6d72', '#3a3b3f'
BRONZE, COPPER = '#b07c48', '#b8704a'
LEATHER, LEATHER_L, LEATHER_D = '#6e4632', '#8a5e44', '#4a2e22'
CLOTH_G, CLOTH_B, CLOTH_P = '#5f7a4e', '#4e6a8a', '#6a5088'
BONE = '#ddd3bc'
RUBY, SAPPH, EMER, AMETH, TOPAZ, PEARL, ONYX = '#c0453e', '#4f7cc0', '#4fa060', '#9a6ac8', '#e0a840', '#ece6dc', '#3a3440'
FIRE, FIRE_C, ICE = '#e8782e', '#ffd27a', '#9cc8e4'


# ================= 部件 =================
def gem(c, x, y, r, col, cut='diamond', glow=True, ol=1.8):
    if cut == 'round':
        m = c.ell(x, y, r)
    elif cut == 'oval':
        m = c.ell(x, y, r * .75, r)
    elif cut == 'drop':
        m = c.poly([(x, y - r * 1.3)] + [(x + r * math.cos(a), y + r * .2 + r * math.sin(a)) for a in [math.radians(t) for t in range(-20, 201, 20)]])
    else:
        m = c.poly([(x, y - r), (x + r * .85, y), (x, y + r), (x - r * .85, y)])
    if glow:
        c.glow(m, col, max(3, r * .9), 0.35)
    c.paint(m, col, ol=ol, shade=0.35, hl=0.0, grain=0)
    # 刻面 + 高光
    c.fill(m & c.poly([(x - r * 1.2, y - r * 1.4), (x + r * .1, y - r * 1.4), (x - r * .1, y), (x - r * 1.2, y + r * .2)]), mix(col, '#ffffff', .35), 0.7)
    c.fill(c.ell(x - r * .32, y - r * .38, r * .2, r * .26) & m, '#ffffff', 0.85)
    return m


def chain(c, pts, col=GOLD, link=4.2, w=1.4):
    segs = []
    for (ax, ay), (bx, by) in zip(pts, pts[1:]):
        L = math.hypot(bx - ax, by - ay)
        n = max(1, int(L / link))
        for i in range(n):
            t = (i + 0.5) / n
            segs.append((ax + (bx - ax) * t, ay + (by - ay) * t, math.atan2(by - ay, bx - ax), (len(segs)) % 2))
    for x, y, a, odd in segs:
        r1, r2 = link * 0.62, (link * 0.34 if not odd else link * 0.16)
        ca, sa = math.cos(a), math.sin(a)
        poly = [(x + r1 * math.cos(t) * ca - r2 * math.sin(t) * sa, y + r1 * math.cos(t) * sa + r2 * math.sin(t) * ca) for t in [i * math.pi / 8 for i in range(16)]]
        c.paint(c.poly(poly), col, ol=1.1, shade=0.25, hl=0.25, grain=0)


def curve(cx, cy, rx, ry, a0, a1, n=24):
    return [(cx + rx * math.cos(math.radians(a0 + (a1 - a0) * i / n)), cy + ry * math.sin(math.radians(a0 + (a1 - a0) * i / n))) for i in range(n + 1)]


def ring(c, band=GOLD, stone=RUBY, cut='diamond', setting=None, x=64, y=72, r=26, th=8, engraved=None, stone_r=11, prongs=True):
    # 戒圈：略带透视的椭圆环，内侧暗
    outer = c.ell(x, y, r, r * .9)
    inner = c.ell(x, y + 1.5, r - th, (r - th) * .82)
    m = outer & ~inner
    c.paint(m, band, ol=2.4, drop=0.45, shade=0.32, hl=0.3, grain=0.02)
    c.fill(c.ell(x, y + 1.5, r - th + 1.6, (r - th) * .82 + 1.6) & m, mul(band, .7), 0.9)
    c.stroke(curve(x, y, r - 2.2, r * .9 - 2.2, 200, 290), mix(band, '#ffffff', .5), 1.4, 0.9)
    if engraved:
        for i in range(5):
            a = math.radians(30 + i * 24)
            px, py = x + (r - th / 2) * math.cos(a), y + (r * .9 - th / 2) * math.sin(a)
            c.stroke([(px - 1.4, py - 1.4), (px + 1.4, py + 1.4)], engraved, 1.0)
            c.glow(c.ell(px, py, 1.4), engraved, 3, 0.4)
    # 戒托
    sy = y - r * .9 + 2
    setting = setting or band
    c.paint(c.poly([(x - stone_r * 1.05, sy + 4), (x + stone_r * 1.05, sy + 4), (x + stone_r * .7, sy - stone_r * .9), (x - stone_r * .7, sy - stone_r * .9)]),
            mul(setting, .9), ol=2.0, drop=0.4, shade=0.3, hl=0.2)
    if stone:
        gem(c, x, sy - stone_r * .55, stone_r, stone, cut)
        if prongs:
            for s in (-1, 1):
                c.paint(c.poly([(x + s * stone_r * .7, sy - stone_r * .2), (x + s * stone_r * .95, sy - stone_r * 1.1), (x + s * stone_r * .45, sy - stone_r * .4)]),
                        setting, ol=1.2, shade=0.2, hl=0.25)


def amulet(c, pend='round', metal=GOLD, stone=SAPPH, cut='round', chain_col=None, x=64, y=82, r=27, symbol=None, sym_col=None, stone_r=None):
    chain(c, curve(64, 42, 26, 28, 175, 365, 18), chain_col or metal, 4.6)
    c.paint(c.ell(x, y - r - 3, 4, 4.6) & ~c.ell(x, y - r - 3, 1.8, 2.4), metal, ol=1.6, shade=0.25, hl=0.25)
    if pend == 'round':
        m = c.ell(x, y, r)
    elif pend == 'drop':
        m = c.poly([(x, y - r * 1.15)] + [(x + r * .9 * math.cos(a), y + r * .25 + r * .85 * math.sin(a)) for a in [math.radians(t) for t in range(-30, 211, 15)]])
    elif pend == 'shield':
        m = c.poly([(x - r, y - r * .9), (x + r, y - r * .9), (x + r * .95, y + r * .1), (x, y + r * 1.15), (x - r * .95, y + r * .1)])
    elif pend == 'diamond':
        m = c.poly([(x, y - r * 1.15), (x + r * .9, y), (x, y + r * 1.15), (x - r * .9, y)])
    elif pend == 'sun':
        pts = []
        for i in range(24):
            rr = r * (1.15 if i % 2 == 0 else .85)
            a = math.radians(i * 15 - 90)
            pts.append((x + rr * math.cos(a), y + rr * math.sin(a)))
        m = c.poly(pts)
    elif pend == 'hex':
        m = c.poly([(x + r * math.cos(math.radians(a)), y + r * math.sin(math.radians(a))) for a in range(-90, 270, 60)])
    else:
        m = c.ell(x, y, r * .8, r)
    c.paint(m, metal, ol=2.4, drop=0.45, shade=0.32, hl=0.25)
    inner = m & ~c.dilate(~m, 3.2)
    c.fill(m & ~inner, mul(metal, .82), 0.6)
    if symbol:
        symbol(c, x, y, r * .62, sym_col or mul(metal, .55))
    if stone:
        gem(c, x, y, stone_r or r * .5, stone, cut)


def beads(c, col='#8a6040', n=14, pend=None, pend_col=GOLD, small=None):
    pts = curve(64, 50, 34, 38, 150, 390, n)
    for i, (x, y) in enumerate(pts):
        bc = small[i % len(small)] if small else col
        c.paint(c.ell(x, y, 5.2), bc, ol=1.8, drop=0.35, shade=0.3, hl=0.35, grain=0.02)
    c.stroke([(64, 88), (64, 96)], mul(col, .6), 1.6)
    if pend:
        pend(c, 64, 106, pend_col)


def cross_pend(c, x, y, col):
    m = c.poly([(x - 2.5, y - 10), (x + 2.5, y - 10), (x + 2.5, y - 4), (x + 8, y - 4), (x + 8, y + 1), (x + 2.5, y + 1), (x + 2.5, y + 14), (x - 2.5, y + 14),
                (x - 2.5, y + 1), (x - 8, y + 1), (x - 8, y - 4), (x - 2.5, y - 4)])
    c.paint(m, col, ol=2.0, drop=0.4, shade=0.3, hl=0.25)


def sun_sym(c, x, y, r, col):
    pts = []
    for i in range(16):
        rr = r if i % 2 == 0 else r * .5
        a = math.radians(i * 22.5 - 90)
        pts.append((x + rr * math.cos(a), y + rr * math.sin(a)))
    c.paint(c.poly(pts), col, ol=1.4, shade=0.2, hl=0.25)


def eye_sym(c, x, y, r, col):
    m = c.poly(curve(x, y + r * .55, r * 1.2, r * .95, 200, 340, 12) + curve(x, y - r * .55, r * 1.2, r * .95, 20, 160, 12))
    c.paint(m, '#ece4d4', ol=1.6, shade=0.15, hl=0.1, grain=0)
    c.paint(c.ell(x, y, r * .42), col, ol=1.2, shade=0.25, hl=0.2, grain=0)
    c.paint(c.ell(x, y, r * .18), '#1e1a1c', ol=0, shade=0, hl=0, grain=0)


def bracer(c, x, y, w, h, col=LEATHER, metal=BRONZE, emblem=None, tilt=0):
    m = c.poly([(x - w / 2 + tilt, y - h / 2), (x + w / 2 + tilt, y - h / 2 + 3), (x + w / 2, y + h / 2), (x - w / 2, y + h / 2 - 3)])
    c.paint(m, col, ol=2.4, drop=0.45, shade=0.3, hl=0.2, grain=0.05)
    for k in (-1, 1):
        yy = y + k * (h / 2 - 4)
        c.paint(c.poly([(x - w / 2 + 1, yy - 2), (x + w / 2 - 1 + tilt * .2, yy - 1), (x + w / 2 - 1, yy + 2), (x - w / 2 + 1, yy + 1)]), metal, ol=1.4, shade=0.25,
                hl=0.25)
    for k in (-1, 0, 1):
        c.stroke([(x + w / 2 - 3, y + k * 6 - 2), (x + w / 2 + 2, y + k * 6)], LEATHER_D, 1.6)
    if emblem:
        emblem(c, x + tilt / 2, y, min(w, h) * .26)


def glove(c, x, y, s=1.0, col=LEATHER, cuff=None, knuckle=None, flip=False):
    f = -1 if flip else 1
    X = lambda dx: x + f * dx * s  # noqa: E731
    palm = c.poly([(X(-14), y + 4 * s), (X(-14), y - 12 * s), (X(14), y - 12 * s), (X(16), y + 6 * s), (X(10), y + 16 * s), (X(-10), y + 16 * s)])
    for i, (dx, ln) in enumerate([(-11, 14), (-4, 17), (3, 17), (10, 14)]):
        fg = c.rect(min(X(dx - 3.2), X(dx + 3.2)), y - 12 * s - ln * s, max(X(dx - 3.2), X(dx + 3.2)), y - 8 * s, 3 * s)
        c.paint(fg, col, ol=2.0, drop=0.4, shade=0.28, hl=0.2)
        c.stroke([(X(dx - 2.4), y - 12 * s - ln * s * .45), (X(dx + 2.4), y - 12 * s - ln * s * .45)], mul(col, .7), 0.9)
    th = c.poly([(X(-14), y), (X(-24), y - 10 * s), (X(-22), y - 15 * s), (X(-12), y - 8 * s)])
    c.paint(th, col, ol=2.0, drop=0.4, shade=0.28, hl=0.2)
    c.paint(palm, col, ol=2.2, drop=0.45, shade=0.3, hl=0.18, grain=0.04)
    cu = c.poly([(X(-15), y + 14 * s), (X(15), y + 14 * s), (X(18), y + 30 * s), (X(-18), y + 30 * s)])
    c.paint(cu, cuff or mul(col, .85), ol=2.2, shade=0.3, hl=0.2)
    if knuckle:
        for dx in (-11, -4, 3, 10):
            c.paint(c.ell(X(dx), y - 9 * s, 3.2 * s, 2.6 * s), knuckle, ol=1.3, shade=0.25, hl=0.3)


def boot(c, x, y, s=1.0, col=LEATHER, cuff=None, trim=None, wing=None):
    m = c.poly([(x - 9 * s, y - 26 * s), (x + 9 * s, y - 26 * s), (x + 9 * s, y + 4 * s), (x + 24 * s, y + 10 * s), (x + 25 * s, y + 18 * s), (x - 10 * s, y + 18 * s),
                (x - 11 * s, y)])
    c.paint(m, col, ol=2.4, drop=0.45, shade=0.3, hl=0.2, grain=0.05)
    c.paint(c.rect(x - 11 * s, y + 15 * s, x + 25.5 * s, y + 20 * s, 1.5), mul(col, .55), ol=1.8, shade=0.2, hl=0.1)
    c.paint(c.poly([(x - 11 * s, y - 30 * s), (x + 11 * s, y - 30 * s), (x + 10 * s, y - 20 * s), (x - 10 * s, y - 20 * s)]), cuff or mul(col, .85), ol=2.0,
            shade=0.25, hl=0.2)
    if trim:
        c.stroke([(x - 9 * s, y - 10 * s), (x + 9 * s, y - 10 * s)], trim, 1.6)
    if wing:
        w = c.poly([(x + 8 * s, y - 24 * s), (x + 22 * s, y - 34 * s), (x + 18 * s, y - 26 * s), (x + 24 * s, y - 26 * s), (x + 16 * s, y - 18 * s), (x + 8 * s, y - 16 * s)])
        c.paint(w, wing, ol=1.6, shade=0.2, hl=0.25)


def medallion(c, shape, col, x=64, y=66, r=34, rim=None):
    if shape == 'circle':
        m = c.ell(x, y, r)
    elif shape == 'pent':
        m = c.poly([(x + r * math.cos(math.radians(a)), y + r * math.sin(math.radians(a))) for a in range(-90, 270, 72)])
    elif shape == 'hex':
        m = c.poly([(x + r * math.cos(math.radians(a)), y + r * math.sin(math.radians(a))) for a in range(-90, 270, 60)])
    elif shape == 'shield':
        m = c.poly([(x - r, y - r * .85), (x + r, y - r * .85), (x + r * .9, y + r * .25), (x, y + r * 1.1), (x - r * .9, y + r * .25)])
    else:
        m = c.poly([(x, y - r), (x + r, y), (x, y + r), (x - r, y)])
    c.paint(m, col, ol=2.6, drop=0.45, shade=0.32, hl=0.25, grain=0.04)
    if rim:
        inner = c.dilate(~m, 4.0)
        c.fill(m & inner, rim, 0.95)
        c.paint(m & inner, rim, ol=0, shade=0.25, hl=0.3)
    return m


def horn(c, col=BONE, tip=None, bands=GOLD, mouth=GOLD, x0=22, y0=96, x1=104, y1=26, curl=24):
    n = 16
    pts_a, pts_b = [], []
    for i in range(n + 1):
        t = i / n
        x = x0 + (x1 - x0) * t
        y = y0 + (y1 - y0) * t - curl * math.sin(math.pi * t)
        th = 2 + 13 * (1 - t) ** 1.2
        pts_a.append((x - th * .6, y - th * .8))
        pts_b.append((x + th * .6, y + th * .8))
    m = c.poly(pts_a + pts_b[::-1])
    c.paint(m, col, ol=2.4, drop=0.45, shade=0.32, hl=0.25, grain=0.04)
    for k in range(1, 7):
        t = k / 8
        i = int(t * n)
        c.stroke([pts_a[i], pts_b[i]], mul(col, .78), 1.0)
    if bands:
        for t in (.12, .45):
            i = int(t * n)
            c.paint(c.line_mask([pts_a[i], pts_b[i]], 4.2), bands, ol=1.4, shade=0.25, hl=0.3)
    if mouth:
        c.paint(c.ell(x0, y0, 9, 10), mouth, ol=2.0, shade=0.25, hl=0.3)
        c.paint(c.ell(x0 + 1, y0, 5, 6.4), mul(mouth, .45), ol=0, shade=0, hl=0)
    if tip:
        c.paint(c.ell(x1, y1, 3.2), tip, ol=1.4, shade=0.2, hl=0.3)


def stone(c, col, pts, glow=None, rune=None):
    m = c.poly(pts)
    if glow:
        c.glow(m, glow, 8, 0.4)
    c.paint(m, col, ol=2.4, drop=0.45, shade=0.32, hl=0.0, grain=0.03)
    cx = sum(p[0] for p in pts) / len(pts)
    cy = sum(p[1] for p in pts) / len(pts)
    c.fill(m & c.poly([(cx - 40, cy - 40), (cx + 4, cy - 40), (cx - 6, cy + 2), (cx - 40, cy + 6)]), mix(col, '#ffffff', .3), 0.7)
    if rune:
        rr = c.stroke(rune, mix(col, '#ffffff', .7), 1.6)
        c.glow(rr, glow or col, 3, 0.5)
    return m


def flame(c, x, y, s=1.0, col=FIRE, core=FIRE_C):
    f = c.poly([(x, y - 22 * s), (x + 8 * s, y - 8 * s), (x + 9 * s, y + 4 * s), (x, y + 9 * s), (x - 9 * s, y + 4 * s), (x - 8 * s, y - 10 * s), (x - 3 * s, y - 13 * s)])
    c.glow(f, col, 9 * s, 0.4)
    c.paint(f, col, ol=1.6, shade=0.15, hl=0, grain=0)
    c.paint(c.poly([(x, y - 10 * s), (x + 4 * s, y), (x, y + 5 * s), (x - 4 * s, y)]), core, ol=0, shade=0, hl=0, grain=0)


# ================= 各物品 =================
def rune_sym(col):
    def f(c, x, y, r, _):
        m = c.stroke([(x - r * .5, y + r * .7), (x, y - r * .7), (x + r * .5, y + r * .7), (x - r * .6, y - r * .1), (x + r * .6, y - r * .1)], col, 2.0)
        c.glow(m, col, 3, 0.5)
    return f


I = {}


def item(name):
    def deco(fn):
        I[name] = fn
        return fn
    return deco


# ---- 戒指 ----
for nm, band, st, cut, eng in [
    ("RingResistanceFire", GOLD, RUBY, 'diamond', FIRE), ("RingResistanceCold", SILVER, ICE, 'diamond', '#cfe8f4'),
    ("RingResistanceAcid", GOLD, EMER, 'oval', '#a8e070'), ("RingResistanceMind", SILVER, AMETH, 'oval', '#d8c0ff'),
    ("RingSpellTurning", SILVER, SAPPH, 'diamond', '#a8d0ff'), ("RingFreeAction", GOLD, '#7fc8d0', 'round', None),
    ("RingRegeneration", GOLD, EMER, 'round', '#8fe08a'), ("RingProtection", SILVER, SAPPH, 'round', None),
    ("RingSpellStoring", SILVER, AMETH, 'diamond', '#c49af0'), ("RingAnimalInfluence", BRONZE, '#8a6a3a', 'oval', None),
    ("RingShootingStars", SILVER, '#3a3a6a', 'round', '#fff0a0'),
]:
    I[nm] = (lambda band, st, cut, eng: lambda c: ring(c, band, st, cut, engraved=eng))(band, st, cut, eng)


@item("RingWardingBond")
def _(c):
    ring(c, SILVER, None, x=50, y=74, r=22, th=7)
    ring(c, GOLD, SAPPH, 'round', x=78, y=70, r=22, th=7, stone_r=8)


@item("RingAnimalInfluence")
def _(c):
    ring(c, BRONZE, None)
    m = c.ell(64, 40, 12, 10)
    c.paint(m, '#8a6a3a', ol=2.0, shade=0.3, hl=0.25)
    for dx, dy in [(-10, -12), (-3, -16), (5, -16), (11, -11)]:
        c.paint(c.ell(64 + dx, 40 + dy, 3.6, 4.2), '#8a6a3a', ol=1.6, shade=0.25, hl=0.25)


# ---- 护符 / 项链 ----
I["AmuletHealth"] = lambda c: amulet(c, 'round', GOLD, None, symbol=lambda c, x, y, r, col: cross_pend(c, x, y + 1, '#e8e2d6'))
I["AmuletProofDetection"] = lambda c: amulet(c, 'hex', '#8a8e96', ONYX, 'round')
I["AmuletPlanes"] = lambda c: amulet(c, 'round', SILVER, '#5a6ad0', 'round', symbol=lambda c, x, y, r, col: [c.stroke(curve(x, y, r * 1.05, r * .45, 0, 360), '#e8eaf4', 1.2),
                                                                                              c.stroke(curve(x, y, r * .45, r * 1.05, 0, 360), '#e8eaf4', 1.2)])
I["PeriaptWoundClosure"] = lambda c: amulet(c, 'drop', GOLD, RUBY, 'drop', stone_r=11)
I["NecklaceAdaptation"] = lambda c: amulet(c, 'round', SILVER, '#a8d8e0', 'round', stone_r=12)
I["AntiMagicAmulet"] = lambda c: amulet(c, 'round', DARK, '#5a3a7a', 'round', symbol=lambda c, x, y, r, col: [c.stroke([(x - r, y - r), (x + r, y + r)], '#c8b8e0', 2.0)])


@item("NecklaceFireballs")
def _(c):
    chain(c, curve(64, 36, 34, 40, 170, 370, 18), GOLD, 4.6)
    for x, y in curve(64, 40, 30, 46, 30, 150, 4):
        c.paint(c.ell(x, y, 7.5), '#d8582a', ol=2.0, drop=0.4, shade=0.3, hl=0.0, grain=0)
        c.glow(c.ell(x, y, 5), FIRE, 5, 0.35)
        c.fill(c.ell(x - 2.4, y - 2.4, 2.2), FIRE_C, 0.85)


@item("PrayerBeads")
def _(c):
    beads(c, '#8a6040', 16, pend=lambda c, x, y, col: cross_pend(c, x, y, col))


@item("MartyrsBeads")
def _(c):
    beads(c, '#9a3a32', 16, pend=lambda c, x, y, col: gem(c, x, y, 7, RUBY, 'drop'), small=['#9a3a32', '#c8b8a0'])


@item("BroochShielding")
def _(c):
    m = medallion(c, 'shield', SILVER, 64, 62, 34, rim=GOLD)
    gem(c, 64, 60, 12, SAPPH, 'diamond')
    c.paint(c.rect(28, 92, 100, 98, 2), GOLD_D, ol=1.4, shade=0.2, hl=0.2)


@item("ScarabProtection")
def _(c):
    body = c.ell(64, 70, 26, 32)
    for s in (-1, 1):
        for k in range(3):
            c.stroke([(64 + s * 20, 58 + k * 12), (64 + s * 34, 52 + k * 16), (64 + s * 38, 60 + k * 18)], DARK, 2.2)
    c.paint(body, '#4e8a5a', ol=2.6, drop=0.45, shade=0.35, hl=0.3)
    c.stroke([(64, 44), (64, 100)], mul('#4e8a5a', .55), 1.8)
    c.paint(c.ell(64, 38, 14, 9), '#3e6a48', ol=2.2, shade=0.3, hl=0.25)
    c.paint(c.ell(64, 70, 8, 8), GOLD, ol=1.6, shade=0.3, hl=0.3)
    for s in (-1, 1):
        c.stroke([(64 + s * 6, 32), (64 + s * 12, 22)], DARK, 1.8)


# ---- 护腕 / 手套 / 靴 / 眼镜 ----
I["BracersArchery"] = lambda c: [bracer(c, 46, 66, 30, 50, LEATHER, BRONZE, None, 4), bracer(c, 82, 64, 30, 50, LEATHER_L, BRONZE, None, 4),
                                 c.stroke([(70, 50), (94, 82)], '#e0d6bc', 1.4), c.paint(c.poly([(92, 78), (98, 86), (90, 84)]), IRON, ol=1.2)]
I["BracersDefense"] = lambda c: [bracer(c, 46, 66, 32, 54, '#7a7e86', GOLD, lambda c, x, y, r: gem(c, x, y, r, SAPPH, 'diamond'), 4),
                                 bracer(c, 82, 64, 32, 54, '#8a8e96', GOLD, lambda c, x, y, r: gem(c, x, y, r, SAPPH, 'diamond'), 4)]
I["GauntletsOgrePower"] = lambda c: glove(c, 64, 64, 1.45, '#6a5a4a', cuff=IRON, knuckle='#8a8e94')
I["GlovesMissileSnaring"] = lambda c: [glove(c, 64, 64, 1.35, '#c8b48c', cuff=LEATHER), c.stroke([(84, 22), (110, 8)], '#8a6040', 1.6),
                                       c.paint(c.poly([(108, 4), (116, 8), (108, 12)]), IRON, ol=1.2)]
I["BootsElvenkind"] = lambda c: [boot(c, 44, 70, 1.25, CLOTH_G, trim=GOLD), c.paint(c.ell(48, 50, 4, 6), '#8fbf6a', ol=1.4)]
I["BootsSpeed"] = lambda c: boot(c, 44, 72, 1.25, '#8a3a32', cuff='#6a2a24', trim=GOLD, wing='#ece6d6')


@item("GogglesNight")
def _(c):
    c.paint(c.rect(18, 58, 110, 70, 5), LEATHER_D, ol=2.0, drop=0.4, shade=0.25, hl=0.15)
    for x in (44, 84):
        c.paint(c.ell(x, 64, 19), BRONZE, ol=2.4, drop=0.4, shade=0.3, hl=0.3)
        m = c.ell(x, 64, 13)
        c.glow(m, '#6ad06a', 6, 0.35)
        c.paint(m, '#2e5a3a', ol=1.6, shade=0.2, hl=0, grain=0)
        c.fill(c.ell(x - 4, 60, 3.6, 2.6), '#c8f0c8', 0.8)
    c.paint(c.rect(60, 60, 68, 68, 2), BRONZE, ol=1.4, shade=0.2, hl=0.2)


@item("EyesEagle")
def _(c):
    for x in (42, 86):
        m = c.ell(x, 64, 20, 15)
        c.paint(m, '#d8c890', ol=2.2, drop=0.4, shade=0.2, hl=0.3, grain=0)
        c.paint(c.ell(x, 64, 8), '#c8962a', ol=1.4, shade=0.3, hl=0.2, grain=0)
        c.paint(c.ell(x, 64, 3.4), '#1e1a1c', ol=0, shade=0, hl=0, grain=0)
        c.fill(c.ell(x - 7, 58, 4, 2.4), '#ffffff', 0.7)


@item("EyesMinuteSeeing")
def _(c):
    for x in (42, 86):
        m = c.ell(x, 64, 20, 16)
        c.paint(m, '#b8d0d8', ol=2.2, drop=0.4, shade=0.15, hl=0.3, grain=0)
        c.stroke(curve(x, 64, 12, 9, 0, 360), '#8ab0c0', 1.0)
        c.stroke(curve(x, 64, 6, 4.5, 0, 360), '#8ab0c0', 1.0)
        c.fill(c.ell(x - 7, 58, 4, 2.4), '#ffffff', 0.75)


# ---- 头部 ----
@item("HelmTelepathy")
def _(c):
    m = c.poly(curve(64, 76, 38, 42, 180, 360, 20) + [(102, 82), (26, 82)])
    c.paint(m, '#9aa0aa', ol=2.6, drop=0.45, shade=0.3, hl=0.3)
    c.paint(c.rect(24, 76, 104, 88, 3), GOLD, ol=2.0, shade=0.25, hl=0.3)
    eye_sym(c, 64, 52, 12, AMETH)
    for s in (-1, 1):
        c.paint(c.poly([(64 + s * 30, 50), (64 + s * 46, 30), (64 + s * 40, 56)]), GOLD, ol=1.8, shade=0.25, hl=0.25)


@item("IntellectFortress")
def _(c):
    band = c.ell(64, 70, 42, 26) & ~c.ell(64, 74, 34, 20)
    c.paint(band, SILVER, ol=2.4, drop=0.4, shade=0.3, hl=0.3)
    c.paint(c.poly([(64, 34), (74, 46), (64, 52), (54, 46)]), SILVER, ol=2.0, shade=0.25, hl=0.25)
    gem(c, 64, 44, 8, AMETH)


@item("PsychicHood")
def _(c):
    m = c.poly([(64, 12), (96, 36), (104, 76), (96, 112), (32, 112), (24, 76), (32, 36)])
    c.paint(m, CLOTH_P, ol=2.6, drop=0.45, shade=0.32, hl=0.2, grain=0.04)
    c.paint(c.poly([(64, 34), (86, 52), (88, 92), (40, 92), (42, 52)]), '#241c2e', ol=2.0, shade=0.15, hl=0)
    for x in (54, 74):
        c.glow(c.ell(x, 66, 3.2, 2.2), '#c8a8ff', 4, 0.6)
        c.paint(c.ell(x, 66, 2.6, 1.6), '#e8d8ff', ol=0, shade=0, hl=0, grain=0)
    c.stroke([(36, 100), (92, 100)], GOLD, 1.8)


@item("HaloOfLight")
def _(c):
    m = c.ell(64, 64, 46, 20) & ~c.ell(64, 64, 36, 13)
    c.glow(m, '#fff0b0', 10, 0.5)
    c.paint(m, GOLD, ol=2.4, shade=0.3, hl=0.35)
    for i in range(8):
        a = math.radians(i * 45)
        c.paint(c.ell(64 + 41 * math.cos(a), 64 + 16.5 * math.sin(a), 2.4), '#fff6d0', ol=0, shade=0, hl=0, grain=0)


@item("EternalNightCrown")
def _(c):
    m = c.poly([(22, 92), (22, 52), (36, 66), (46, 36), (56, 62), (64, 24), (72, 62), (82, 36), (92, 66), (106, 52), (106, 92)])
    c.paint(m, '#4a4458', ol=2.6, drop=0.45, shade=0.3, hl=0.3)
    c.paint(c.rect(20, 82, 108, 96, 3), '#5a5468', ol=2.0, shade=0.25, hl=0.3)
    for x, col in [(42, AMETH), (64, '#3a3a6a'), (86, AMETH)]:
        gem(c, x, 89, 5, col, 'round')
    gem(c, 64, 40, 6, '#c8d0ff', 'diamond')


# ---- 印记 / 徽章 / 圣物 ----
def mark(c, shape, col, sym, sym_col, rim=None):
    medallion(c, shape, col, rim=rim)
    m = c.stroke(sym, sym_col, 3.4)
    c.glow(m, sym_col, 4, 0.35)


I["MarkBlood"] = lambda c: mark(c, 'pent', '#8a2a26', [(48, 48), (80, 84), (64, 66), (80, 48), (48, 84), (64, 66), (64, 40), (64, 92)], '#e8c8b0', rim=DARK)
I["MarkChange"] = lambda c: mark(c, 'pent', '#3a5a9a', curve(64, 66, 18, 18, 0, 540, 30)[::-1][:24], '#a8d0ff', rim=DARK)


@item("MarkDecay")
def _(c):
    mark(c, 'pent', '#5a7a3a', curve(64, 66, 7, 7, 0, 360), '#d8e8a0', rim=DARK)
    for a in (-90, 30, 150):
        c.paint(c.ell(64 + 14 * math.cos(math.radians(a)), 66 + 14 * math.sin(math.radians(a)), 6), '#d8e8a0', ol=1.4, shade=0.2, hl=0.2)
I["MarkRapture"] = lambda c: mark(c, 'pent', '#8a4a9a', [(50, 86), (50, 46), (78, 86), (78, 46)], '#f0c8f8', rim=DARK)


@item("InquisitorSeal")
def _(c):
    m = medallion(c, 'circle', '#9a2a2a', rim=GOLD_D)
    c.paint(c.ell(64, 66, 18), GOLD, ol=2.0, shade=0.3, hl=0.3)
    c.paint(c.poly([(56, 56), (72, 56), (72, 62), (66, 62), (66, 80), (62, 80), (62, 62), (56, 62)]), '#9a2a2a', ol=0, shade=0, hl=0)
    for s in (-1, 1):
        c.paint(c.poly([(64 + s * 24, 96), (64 + s * 34, 120), (64 + s * 18, 110)]), '#9a2a2a', ol=1.8, shade=0.25, hl=0.15)


@item("SaintSeal")
def _(c):
    medallion(c, 'circle', GOLD, rim=GOLD_D)
    sun_sym(c, 64, 66, 20, '#fff0c0')


@item("HolySymbol")
def _(c):
    chain(c, curve(64, 30, 26, 22, 180, 360, 12), SILVER, 4.4)
    medallion(c, 'circle', SILVER, 64, 70, 30, rim='#9aa0aa')
    sun_sym(c, 64, 70, 17, GOLD)


@item("WaaaghTotem")
def _(c):
    c.paint(c.rect(58, 50, 70, 118, 2), '#7a5a3e', ol=2.2, drop=0.45, shade=0.3, hl=0.15)
    sk = c.ell(64, 40, 22, 20) | c.rect(52, 48, 76, 62, 4)
    c.paint(sk, BONE, ol=2.6, drop=0.45, shade=0.3, hl=0.15)
    for x in (55, 73):
        c.paint(c.ell(x, 40, 5, 5.6), '#2a2420', ol=0, shade=0, hl=0, grain=0)
    for s in (-1, 1):
        c.paint(c.poly([(64 + s * 12, 56), (64 + s * 20, 72), (64 + s * 16, 56)]), '#f0ead8', ol=1.4, shade=0.15, hl=0.1)
        c.paint(c.poly([(64 + s * 18, 28), (64 + s * 38, 8), (64 + s * 28, 30)]), '#c8b890', ol=1.6, shade=0.25, hl=0.2)
    c.stroke([(52, 78), (76, 72), (52, 90), (76, 86)], '#a83a2a', 2.4)


@item("CogOfMakers")
def _(c):
    pts = []
    for i in range(32):
        a = math.radians(i * 11.25)
        r = 42 if (i // 2) % 2 == 0 else 34
        pts.append((64 + r * math.cos(a), 64 + r * math.sin(a)))
    m = c.poly(pts) & ~c.ell(64, 64, 12)
    c.paint(m, BRONZE, ol=2.6, drop=0.45, shade=0.32, hl=0.25)
    c.fill(c.ell(64, 64, 26) & ~c.ell(64, 64, 22), mul(BRONZE, .75), 1)
    for i in range(6):
        a = math.radians(i * 60 + 30)
        c.paint(c.ell(64 + 18 * math.cos(a), 64 + 18 * math.sin(a), 2.4), GOLD, ol=1.0, shade=0.2, hl=0.3)
    m2 = c.ell(64, 64, 9)
    c.glow(m2, '#ff8a3a', 6, 0.5)
    c.paint(m2, '#e8702a', ol=1.6, shade=0.2, hl=0.3, grain=0)


@item("RevivalCore")
def _(c):
    m = c.poly([(64 + 38 * math.cos(math.radians(a)), 64 + 38 * math.sin(math.radians(a))) for a in range(-90, 270, 60)])
    c.paint(m, '#4a5058', ol=2.6, drop=0.45, shade=0.32, hl=0.25)
    c.fill(m & ~c.dilate(~m, 5), '#6a7078', 0.8)
    orb = c.ell(64, 64, 18)
    c.glow(orb, '#6ae0a0', 10, 0.55)
    c.paint(orb, '#3aa070', ol=2.0, shade=0.25, hl=0, grain=0)
    c.paint(c.ell(64, 64, 9), '#c8ffe0', ol=0, shade=0, hl=0, grain=0)
    for a in range(-90, 270, 60):
        c.paint(c.ell(64 + 32 * math.cos(math.radians(a)), 64 + 32 * math.sin(math.radians(a)), 2.4), GOLD, ol=1.0, shade=0.2, hl=0.3)


@item("SpiritStone")
def _(c):
    chain(c, curve(64, 32, 20, 16, 180, 360, 10), SILVER, 4)
    stone(c, '#a8c0e0', [(64, 26), (84, 46), (86, 84), (64, 108), (42, 84), (44, 46)], glow='#d8e8ff')
    c.paint(c.ell(64, 66, 7, 10), '#eef4ff', ol=0, shade=0, hl=0, grain=0)


@item("IounStone")
def _(c):
    for r, a in ((34, .25), (26, .45)):
        c.stroke(curve(64, 70, r + 12, r * .45, 0, 360), '#c8b8f0', 1.0, a)
    stone(c, '#9a6ac8', [(64, 28), (88, 60), (64, 100), (40, 60)], glow='#d8c0ff')


@item("BoneTalisman")
def _(c):
    c.stroke(curve(64, 30, 26, 22, 180, 360, 12), '#7a5a3e', 2.2)
    m = c.poly([(56, 44), (72, 44), (78, 60), (74, 100), (64, 112), (54, 100), (50, 60)])
    c.paint(m, BONE, ol=2.4, drop=0.45, shade=0.3, hl=0.2, grain=0.05)
    for y in (58, 74, 90):
        c.stroke([(56, y), (72, y - 3)], '#8a3a2a', 1.6)
    for x, y in ((40, 56), (88, 56)):
        c.paint(c.poly([(x - 3, y - 6), (x + 3, y - 6), (x, y + 10)]), '#e8dfc8', ol=1.4, shade=0.2, hl=0.15)


@item("GorgedFang")
def _(c):
    c.stroke(curve(64, 30, 28, 22, 180, 360, 12), '#5a3a2e', 2.2)
    m = c.poly([(50, 36), (80, 36), (82, 50), (70, 94), (62, 116), (58, 92), (48, 52)])
    c.paint(m, '#e8dfc8', ol=2.6, drop=0.45, shade=0.32, hl=0.25, grain=0.04)
    c.paint(c.rect(46, 34, 84, 46, 3), '#7a2a2a', ol=2.0, shade=0.25, hl=0.2)
    c.glow(c.ell(66, 70, 6, 14), '#c0392b', 6, 0.35)


@item("ElementalFocus_Fire")
def _(c):
    stone(c, '#c0502a', [(64, 22), (92, 52), (86, 96), (42, 96), (36, 52)], glow='#ff9a4a')
    flame(c, 64, 74, 1.4)


@item("ElementalFocus_Water")
def _(c):
    stone(c, '#3a7ab0', [(64, 22), (92, 52), (86, 96), (42, 96), (36, 52)], glow='#8ad0ff')
    m = c.poly([(64, 46)] + [(64 + 13 * math.cos(math.radians(a)), 76 + 13 * math.sin(math.radians(a))) for a in range(-30, 211, 15)])
    c.paint(m, '#a8e0ff', ol=1.6, shade=0.2, hl=0.3, grain=0)


@item("ElementalFocus_Earth")
def _(c):
    stone(c, '#7a6a4a', [(64, 22), (92, 52), (86, 96), (42, 96), (36, 52)], glow='#c8a870')
    c.paint(c.poly([(48, 90), (58, 62), (66, 74), (74, 54), (84, 90)]), '#a89060', ol=1.8, shade=0.25, hl=0.25)


@item("ElementalFocus_Air")
def _(c):
    stone(c, '#8aa8b8', [(64, 22), (92, 52), (86, 96), (42, 96), (36, 52)], glow='#e0f4ff')
    for k, r in enumerate((16, 10)):
        c.stroke(curve(64 + k * 3, 70, r, r * .8, -60, 230), '#f0f8ff', 2.2)


@item("FrostGiantHorn")
def _(c):
    horn(c, '#cfdfe8', tip='#ffffff', bands='#8aa8c0', mouth='#8aa8c0')


@item("IvoryHorn")
def _(c):
    horn(c, '#ece2c8', bands=GOLD, mouth=GOLD)


@item("HornValhalla")
def _(c):
    horn(c, '#d8c8a0', bands=SILVER, mouth=SILVER, curl=30)
    for x, y in ((80, 66), (94, 76)):
        c.glow(c.ell(x, y, 3), '#a8d0ff', 4, 0.5)


@item("EverburningTorch")
def _(c):
    m = c.poly([(56, 54), (72, 54), (68, 118), (60, 118)])
    c.paint(m, '#7a5a3e', ol=2.4, drop=0.45, shade=0.3, hl=0.15)
    for y in (64, 96):
        c.paint(c.rect(55, y, 73, y + 6, 1.5), IRON, ol=1.6, shade=0.25, hl=0.25)
    c.paint(c.poly([(50, 42), (78, 42), (74, 58), (54, 58)]), DARK, ol=2.2, shade=0.25, hl=0.2)
    flame(c, 64, 30, 1.6)


@item("BagOfHolding")
def _(c):
    body = c.poly([(36, 46), (92, 46), (104, 84), (96, 112), (32, 112), (24, 84)])
    c.paint(body, '#5a3a6a', ol=2.6, drop=0.45, shade=0.32, hl=0.18, grain=0.05)
    c.paint(c.poly([(32, 46), (96, 46), (90, 30), (38, 30)]), '#6a4a7a', ol=2.2, shade=0.25, hl=0.2)
    c.paint(c.rect(30, 42, 98, 50, 2), GOLD, ol=1.6, shade=0.2, hl=0.25)
    m = c.ell(64, 80, 12)
    c.glow(m, '#b48ae8', 7, 0.5)
    c.paint(m, '#1e1428', ol=1.6, shade=0, hl=0, grain=0)
    for a in range(0, 360, 60):
        c.fill(c.ell(64 + 7 * math.cos(math.radians(a)), 80 + 7 * math.sin(math.radians(a)), 1.2), '#e8d8ff', 0.9)


@item("CloakElvenkind")
def _(c):
    m = c.poly([(48, 18), (80, 18), (100, 100), (90, 112), (78, 104), (64, 114), (50, 104), (38, 112), (28, 100)])
    c.paint(m, CLOTH_G, ol=2.6, drop=0.45, shade=0.32, hl=0.18, grain=0.04)
    for x0, x1 in ((58, 46), (70, 82)):
        c.stroke([(x0, 30), (x1, 104)], mul(CLOTH_G, .72), 1.4)
    c.paint(c.poly([(44, 18), (84, 18), (78, 32), (50, 32)]), mul(CLOTH_G, .85), ol=2.0, shade=0.25, hl=0.2)
    c.paint(c.ell(64, 30, 6), SILVER, ol=1.6, shade=0.25, hl=0.3)
    c.paint(c.poly([(60, 30), (64, 22), (68, 30), (64, 38)]), '#8fbf6a', ol=1.0)


@item("Relic_HandOfTyr")
def _(c):
    glove(c, 64, 62, 1.4, SILVER, cuff=GOLD, knuckle=GOLD)
    sun_sym(c, 64, 72, 8, GOLD)


@item("Relic_BlackHand")
def _(c):
    glove(c, 64, 62, 1.4, '#3a383e', cuff='#5a2a2a', knuckle='#6a6870')
    c.glow(c.ell(64, 70, 6), '#c0392b', 5, 0.5)


@item("Relic_BeastClaw")
def _(c):
    for i, (x, a) in enumerate(((40, -20), (64, 0), (88, 20))):
        m = c.poly([(x - 8, 92), (x + 8, 92), (x + 6 + a * .3, 50), (x + a * .9, 18), (x - 4 + a * .2, 52)])
        c.paint(m, '#e0d4b8', ol=2.4, drop=0.45, shade=0.32, hl=0.25)
    c.paint(c.rect(26, 88, 102, 108, 6), '#6a4a32', ol=2.4, shade=0.3, hl=0.2, grain=0.06)
    c.stroke([(30, 98), (98, 98)], '#4a3020', 1.4)


@item("Relic_AshScales")
def _(c):
    c.paint(c.rect(61, 28, 67, 104, 2), '#5a5458', ol=2.2, drop=0.4, shade=0.3, hl=0.2)
    c.paint(c.rect(40, 102, 88, 112, 3), '#5a5458', ol=2.2, shade=0.3, hl=0.2)
    c.paint(c.rect(20, 34, 108, 40, 2), '#7a7478', ol=2.0, shade=0.3, hl=0.25)
    for x in (30, 98):
        c.stroke([(x, 40), (x - 10, 74)], '#4a4448', 1.0)
        c.stroke([(x, 40), (x + 10, 74)], '#4a4448', 1.0)
        pan = c.poly(curve(x, 74, 15, 9, 0, 180, 10))
        c.paint(pan, '#8a8488', ol=2.0, shade=0.3, hl=0.25)
        c.fill(c.ell(x, 72, 9, 3), '#b8b0a8', 0.9)
    c.paint(c.ell(64, 28, 6), '#8a8488', ol=1.6, shade=0.25, hl=0.3)


@item("Relic_ForgeHeart")
def _(c):
    m = c.poly([(64, 112), (24, 66), (26, 40), (42, 26), (64, 40), (86, 26), (102, 40), (104, 66)])
    c.glow(m, '#ff7a2a', 9, 0.4)
    c.paint(m, '#5a4a44', ol=2.6, drop=0.45, shade=0.32, hl=0.2, grain=0.05)
    cr = c.stroke([(64, 46), (56, 62), (70, 74), (60, 92)], FIRE, 2.6)
    c.glow(cr, FIRE, 5, 0.6)
    c.stroke([(64, 46), (56, 62), (70, 74), (60, 92)], FIRE_C, 1.0)


def tear(c, col, glowc):
    m = c.poly([(64, 18)] + [(64 + 26 * math.cos(math.radians(a)), 80 + 26 * math.sin(math.radians(a))) for a in range(-30, 211, 12)])
    c.glow(m, glowc, 9, 0.45)
    c.paint(m, col, ol=2.6, drop=0.4, shade=0.32, hl=0.0, grain=0)
    c.fill(c.ell(54, 72, 6, 10) & m, '#ffffff', 0.6)


I["Relic_SilverTear"] = lambda c: tear(c, '#c8d0dc', '#ffffff')
I["Relic_NightTear"] = lambda c: tear(c, '#3a3a5a', '#8a7ac8')


@item("StabilizerBand")
def _(c):
    m = c.ell(64, 66, 44, 30) & ~c.ell(64, 70, 34, 21)
    c.paint(m, '#d6d6d2', ol=2.6, drop=0.45, shade=0.32, hl=0.3)
    for a in range(200, 341, 35):
        x, y = 64 + 39 * math.cos(math.radians(a)), 66 + 25.5 * math.sin(math.radians(a))
        c.paint(c.ell(x, y, 3.4), '#9a9a96', ol=1.2, shade=0.2, hl=0.3)


@item("EntanglingRope")
def _(c):
    for r, w in ((40, 7), (30, 7), (20, 7)):
        m = c.ell(64, 66, r, r * .8) & ~c.ell(64, 66, r - w, (r - w) * .8)
        c.paint(m, '#c8a868', ol=2.0, drop=0.35, shade=0.3, hl=0.2)
        for i in range(0, 360, 20):
            a = math.radians(i)
            c.stroke([(64 + (r - w) * math.cos(a), 66 + (r - w) * .8 * math.sin(a)), (64 + r * math.cos(a + .2), 66 + r * .8 * math.sin(a + .2))], '#8a6a3a', .9)
    c.paint(c.poly([(96, 74), (114, 98), (106, 102), (92, 80)]), '#c8a868', ol=2.0, shade=0.25, hl=0.2)
    c.glow(c.ell(64, 66, 6), '#a8e0a0', 5, 0.35)


@item("CyclopsLens")
def _(c):
    c.paint(c.ell(64, 64, 40), BRONZE, ol=2.6, drop=0.45, shade=0.32, hl=0.3)
    m = c.ell(64, 64, 30)
    c.paint(m, '#a8c8d0', ol=2.0, shade=0.2, hl=0.0, grain=0)
    c.fill(c.ell(52, 52, 10, 7) & m, '#ffffff', 0.7)
    for a in range(0, 360, 45):
        c.paint(c.ell(64 + 35 * math.cos(math.radians(a)), 64 + 35 * math.sin(math.radians(a)), 2.4), GOLD, ol=1.0, shade=0.2, hl=0.3)
    c.paint(c.rect(98, 58, 124, 70, 3), BRONZE, ol=2.0, shade=0.25, hl=0.25)


SKIP_WORN = True


def main():
    rows = json.load(open(os.path.join(HERE, "acc_list.json"), encoding="utf8"))
    done = set()
    for r in rows:
        name = r["def_"][3:]
        if ONLY and r["def_"] not in ONLY:
            continue
        fn = I.get(name)
        if fn is None or r["path"] in done:
            continue
        done.add(r["path"])
        size = r["W"]
        k = size / 128
        # 先按 1:1 画一遍量外框，再缩放到与旧图同样的占格大小（中心对齐旧图）
        ob = bbox(r["backup"])
        c = Canvas(size, size, seed=(hash(name) & 0xffff))
        c.tf = lambda x, y: (x * k, y * k)
        c.zs = k
        fn(c)
        a = c.a
        ys, xs = np.nonzero(a > 0.2)
        nb = (xs.min() / c.W * 128, ys.min() / c.H * 128, (xs.max() + 1) / c.W * 128, (ys.max() + 1) / c.H * 128)
        z = max(ob[2] - ob[0], ob[3] - ob[1]) / max(nb[2] - nb[0], nb[3] - nb[1])
        z = min(max(z, 0.8), 1.9)
        ncx, ncy = (nb[0] + nb[2]) / 2, (nb[1] + nb[3]) / 2
        ocx, ocy = (ob[0] + ob[2]) / 2, (ob[1] + ob[3]) / 2
        half = max(nb[2] - nb[0], nb[3] - nb[1]) * z / 2
        ocx = min(max(ocx, 2 + (nb[2] - nb[0]) * z / 2), 126 - (nb[2] - nb[0]) * z / 2)
        ocy = min(max(ocy, 2 + (nb[3] - nb[1]) * z / 2), 126 - (nb[3] - nb[1]) * z / 2)
        c = Canvas(size, size, seed=(hash(name) & 0xffff))
        c.tf = lambda x, y: (((x - ncx) * z + ocx) * k, ((y - ncy) * z + ocy) * k)
        c.zs = k * z
        fn(c)
        c.save(r["path"])
        print("ok", r["def_"], size)


if __name__ == "__main__":
    main()
