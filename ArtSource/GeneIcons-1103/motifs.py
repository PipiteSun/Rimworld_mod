# Reusable motifs, each drawn in the 128 grid centred on (64,64).
# Place smaller copies with `with c.at(ox, oy, k): motif(c)`.
import math
from genelib import *


# ---------- people ----------
def torso(c, col=W, wide=1.0, y0=86):
    hw = 30 * wide
    pts = bez((64 - hw - 4, 126), (64 - hw - 4, y0 + 2), (64 - hw * 0.6, y0 - 2), (64, y0 - 2)) + \
        bez((64, y0 - 2), (64 + hw * 0.6, y0 - 2), (64 + hw + 4, y0 + 2), (64 + hw + 4, 126))
    return c.put(c.poly(pts), col)


def head(c, cx=64, cy=50, rx=21, ry=26, col=W):
    return c.put(c.ell(cx, cy, rx, ry), col)


def bust(c, col=W, hcol=None, wide=1.0):
    torso(c, col, wide)
    head(c, col=hcol or col)


def figure(c, col=W):
    """Small full-body standing person."""
    c.put(c.poly(bez((40, 124), (38, 70), (50, 62), (64, 62)) + bez((64, 62), (78, 62), (90, 70), (88, 124))), col)
    head(c, 64, 40, 15, 18, col=col)


def eyes(c, cx=64, cy=52, dx=9, r=3.2, col=INK):
    c.detail(c.ell(cx - dx, cy, r, r * 1.2) | c.ell(cx + dx, cy, r, r * 1.2), col)


# ---------- body parts ----------
def horn(c, base, ctrl, tip, w=12, col=BONE):
    return c.put(c.poly(taper(bez(base, ctrl, tip), w, 1.5)), col)


def horns_devil(c, col=DARK, y=34, spread=14, h=30):
    horn(c, (64 - spread, y), (64 - spread - 14, y - h * 0.5), (64 - spread - 8, y - h), 12, col)
    horn(c, (64 + spread, y), (64 + spread + 14, y - h * 0.5), (64 + spread + 8, y - h), 12, col)


def horns_ram(c, col=TAN, cy=40):
    for s in (-1, 1):
        pts =[(64 + s * (16 + (14 - i * 0.18) * math.cos(-2.2 + i * 0.13)),
                cy + (14 - i * 0.18) * math.sin(-2.2 + i * 0.13)) for i in range(48)]
        c.put(c.poly(taper(pts, 12, 4)), col)


def horns_bull(c, col=BONE, y=36):
    for s in (-1, 1):
        horn(c, (64 + s * 14, y + 4), (64 + s * 52, y + 4), (64 + s * 46, y - 26), 14, col)


def antlers(c, col=TAN, y=30):
    for s in (-1, 1):
        main = bez((64 + s * 10, y + 6), (64 + s * 26, y - 4), (64 + s * 42, y - 30))
        m = c.line(main, 6)
        m |= c.line([(64 + s * 22, y), (64 + s * 22, y - 22)], 5)
        m |= c.line([(64 + s * 32, y - 12), (64 + s * 46, y - 14)], 5)
        m |= c.line([(64 + s * 36, y - 20), (64 + s * 34, y - 38)], 5)
        c.put(m, col)


def bat_wing(c, s=1, col=DARK, cx=64, cy=64, size=1.0):
    """One bat wing whose shoulder sits at (cx,cy); s=1 extends right."""
    def p(x, y):
        return (cx + s * x * size, cy + y * size)
    tips = [(60, -44), (62, 0), (46, 24), (22, 30)]
    pts = [p(*q) for q in bez((0, 6), (6, -30), (30, -50), tips[0])]
    for a, b in zip(tips, tips[1:]):
        mx, my = (a[0] + b[0]) / 2, (a[1] + b[1]) / 2
        # scallop bulges toward the shoulder
        pts += [p(*q) for q in bez(a, (mx - (b[1] - a[1]) * 0.0 - 10, my - 2), b)]
    pts += [p(*q) for q in bez(tips[-1], (12, 22), (0, 18))]
    c.put(c.poly(pts), col)
    dk = tuple(int(v * 0.62) for v in col)
    for t in tips[1:]:
        c.detail(c.line([p(4, -4), p(*t)], 2.4), dk)


def bat_wings(c, col=DARK, cy=60, size=0.9, gap=10):
    bat_wing(c, -1, col, 64 - gap, cy, size)
    bat_wing(c, 1, col, 64 + gap, cy, size)


def feather_wing(c, s=1, col=W, cx=64, cy=64, size=1.0):
    """One feathered wing, shoulder at (cx,cy); trailing edge in rounded feather tips."""
    def p(x, y):
        return (cx + s * x * size, cy + y * size)
    tips = [(62, -46), (60, -18), (52, 6), (38, 24), (20, 34)]
    pts = [p(*q) for q in bez((0, 8), (4, -30), (30, -52), tips[0])]
    for a, b in zip(tips, tips[1:]):
        # convex feather bump between tips
        mx, my = (a[0] + b[0]) / 2 + 10, (a[1] + b[1]) / 2 + 6
        pts += [p(*q) for q in bez(a, (mx, my), b)]
    pts += [p(*q) for q in bez(tips[-1], (10, 30), (0, 20))]
    c.put(c.poly(pts), col)
    dk = tuple(int(v * 0.72) for v in col)
    c.detail(c.line([p(*q) for q in bez((8, -8), (24, -26), (50, -36))], 2.4), dk)
    for t in tips[1:-1]:
        c.detail(c.line([p(t[0] * 0.55, t[1] * 0.55 - 10), p(t[0] - 4, t[1] - 4)], 2.2), dk)

def feather_wings(c, col=W, cy=62, size=0.9, gap=8):
    feather_wing(c, -1, col, 64 - gap, cy, size)
    feather_wing(c, 1, col, 64 + gap, cy, size)


def tail_spade(c, col=CRIM):
    c.put(c.poly(taper(bez((40, 120), (20, 70), (100, 80), (88, 34)), 9, 6)), col)
    c.put(c.poly([(88, 14), (104, 38), (88, 34), (72, 38)]), col)


def stinger(c, col=LBLUE, tip=None):
    """Scorpion-like segmented tail curling up to a hooked sting."""
    path = bez((24, 120), (6, 70), (40, 20), (84, 26), n=7)
    for i, (x, y) in enumerate(path):
        r = 15 - i * 1.2
        c.put(c.ell(x, y, r, r * 0.86), col, shade=0.15)
    c.put(c.poly(taper(bez((84, 22), (110, 22), (112, 50), (98, 66)), 18, 2)), tip or col)


def hand(c, col=SKIN):
    m = c.rect(40, 62, 92, 118, 14)
    for i, (x, top) in enumerate(((46, 24), (58, 16), (70, 18), (82, 28))):
        m |= c.rect(x - 5.5, top, x + 5.5, 74, 5.5)
    m |= c.poly(taper(bez((42, 96), (30, 84), (24, 66)), 12, 10))
    c.put(m, col)
    for x in (52, 64, 76):
        c.detail(c.line([(x, 60), (x, 72)], 1.6), tuple(int(v * 0.7) for v in col))


def fist(c, col=SKIN):
    m = c.rect(30, 40, 98, 104, 16)
    c.put(m, col)
    for x in (47, 64, 81):
        c.detail(c.line([(x, 42), (x, 62)], 2.2), tuple(int(v * 0.65) for v in col))
    c.put(c.rect(26, 62, 62, 82, 10), col)


def muscle_arm(c, col=G):
    pts = [(12, 124), (12, 98)] + bez((12, 98), (26, 54), (70, 50), (80, 76)) + \
        bez((80, 76), (78, 62), (74, 52), (72, 40)) + [(102, 34)] + \
        bez((102, 34), (114, 62), (116, 96), (100, 116)) + [(92, 124)]
    c.put(c.poly(pts), col)
    c.put(c.ell(86, 28, 20, 18, -10), col)
    dk = tuple(int(v * 0.7) for v in col)
    c.detail(c.line(bez((34, 78), (50, 68), (66, 80)), 2.6), dk)
    c.detail(c.line([(76, 24), (80, 38)], 2.4) | c.line([(88, 20), (92, 34)], 2.4), dk)

def eye(c, iris=GOLD, col=W, cx=64, cy=64, w=50, h=28):
    top = bez((cx - w, cy), (cx - w * 0.4, cy - h * 1.4), (cx + w * 0.4, cy - h * 1.4), (cx + w, cy))
    bot = bez((cx + w, cy), (cx + w * 0.4, cy + h * 1.4), (cx - w * 0.4, cy + h * 1.4), (cx - w, cy))
    c.put(c.poly(top + bot), col, shade=0)
    c.put(c.ell(cx, cy, h * 0.95), iris, ol=2.4)
    c.detail(c.ell(cx, cy, h * 0.42), INK)
    c.flat(c.ell(cx - h * 0.35, cy - h * 0.35, h * 0.16), W)


def fangs_mouth(c, col=W, gum=CRIM):
    c.put(c.poly(bez((18, 44), (40, 70), (88, 70), (110, 44)) + bez((110, 44), (100, 104), (28, 104), (18, 44))), gum)
    for x in (34, 50, 78, 94):
        l = 26 if x in (34, 94) else 14
        c.put(c.poly([(x - 7, 52), (x + 7, 52), (x, 52 + l)]), col, ol=2.4, shade=0)


def skull(c, col=BONE, cx=64, cy=60, s=1.0):
    with c.at(cx - 64 * s, cy - 60 * s, s):
        m = c.ell(64, 52, 34, 32) | c.rect(44, 66, 84, 96, 8)
        c.put(m, col)
        c.detail(c.ell(51, 58, 9, 10) | c.ell(77, 58, 9, 10), INK)
        c.detail(c.poly([(64, 68), (59, 78), (69, 78)]), INK)
        for x in (54, 64, 74):
            c.detail(c.line([(x, 86), (x, 96)], 2.2), INK)


def bone(c, col=BONE, ang=-35):
    m = c.poly(rot([(30, 58), (98, 58), (98, 70), (30, 70)], ang))
    for x in (24, 104):
        for dy in (-8, 8):
            p = rot([(x, 64 + dy)], ang)[0]
            m |= c.ell(p[0], p[1], 11)
    c.put(m, col)


def brain(c, col=(222, 150, 170)):
    m = c.ell(46, 60, 32, 30) | c.ell(82, 60, 32, 30) | c.ell(64, 44, 30, 24)
    c.put(m, col)
    dk = tuple(int(v * 0.7) for v in col)
    c.detail(c.line(bez((64, 26), (58, 50), (70, 70), (64, 90)), 2.6), dk)
    for pts in (bez((30, 50), (40, 44), (44, 60)), bez((92, 42), (100, 54), (86, 60)),
                bez((36, 72), (46, 80), (54, 70)), bez((78, 76), (88, 80), (96, 70))):
        c.detail(c.line(pts, 2.4), dk)


def tentacles(c, col=LPURPLE, n=3):
    xs = {3: (40, 64, 88), 4: (34, 54, 74, 94)}[n]
    for i, x in enumerate(xs):
        s = 1 if i % 2 else -1
        c.put(c.poly(taper(bez((x, 50), (x + s * 18, 80), (x - s * 14, 98), (x + s * 8, 122)), 16, 3)), col)


def hoof(c, col=BROWN):
    leg = bez((50, 10), (44, 40), (40, 60), (42, 80)) + bez((42, 80), (60, 82), (80, 82), (86, 80)) + \
        bez((86, 80), (84, 60), (80, 40), (76, 10))
    c.put(c.poly(leg), BROWN)
    c.put(c.poly([(36, 78), (92, 78), (98, 112), (30, 112)]), DARK)
    c.detail(c.line([(64, 82), (64, 110)], 3), INK)


def boot(c, col=DG):
    c.put(c.poly([(40, 14), (78, 14), (80, 76), (112, 90), (112, 114), (34, 114)]), col)
    c.detail(c.line([(36, 104), (112, 104)], 3), INK)
    c.put(c.rect(36, 12, 82, 24, 3), G)


def trunk_head(c, col=G):
    c.put(c.ell(22, 52, 18, 30) | c.ell(106, 52, 18, 30), tuple(int(v * 0.85) for v in col))
    c.put(c.ell(64, 46, 30, 30), col)
    c.put(c.poly(taper(bez((64, 58), (60, 96), (70, 110), (88, 104)), 22, 10)), col)
    eyes(c, 64, 42, 13, 3)
    c.put(c.poly(taper(bez((48, 68), (40, 80), (34, 82)), 7, 2)), W, ol=2.4)
    c.put(c.poly(taper(bez((80, 68), (88, 80), (94, 82)), 7, 2)), W, ol=2.4)


def bull_head(c, col=BROWN):
    horns_bull(c, BONE, 40)
    c.put(c.ell(64, 60, 30, 36), col)
    c.put(c.ell(64, 88, 22, 16), TAN)
    c.detail(c.ell(56, 88, 4, 5) | c.ell(72, 88, 4, 5), INK)
    eyes(c, 64, 56, 14, 3.4)


def ape_head(c, col=BROWN):
    c.put(c.ell(64, 58, 44, 40), col)
    c.put(c.ell(64, 72, 30, 24), TAN)
    c.detail(c.line(bez((36, 46), (50, 38), (60, 48)), 3) | c.line(bez((92, 46), (78, 38), (68, 48)), 3), INK)
    eyes(c, 64, 54, 13, 3.4)
    for x in (44, 84):
        c.put(c.poly([(x - 5, 80), (x + 5, 80), (x, 64)]), W, ol=2.2, shade=0)


def toad_head(c, col=OLIVE):
    c.put(c.ell(64, 70, 54, 36), col)
    c.put(c.ell(36, 40, 15) | c.ell(92, 40, 15), col)
    c.detail(c.ell(36, 40, 6) | c.ell(92, 40, 6), INK)
    c.detail(c.line(bez((22, 78), (64, 100), (106, 78)), 3), INK)
    for x, y in ((50, 60), (76, 62), (60, 84), (88, 82), (34, 72)):
        c.detail(c.ell(x, y, 4), tuple(int(v * 0.75) for v in col))


def wolf_head(c, col=LG):
    pts = [(24, 14), (46, 40), (82, 40), (104, 14), (104, 60), (86, 98), (64, 122), (42, 98), (24, 60)]
    c.put(c.poly(pts), col)
    c.put(c.poly([(46, 84), (82, 84), (64, 120)]), W)
    c.detail(c.ell(64, 108, 7, 5), INK)
    c.detail(c.poly([(40, 62), (54, 66), (42, 70)]) | c.poly([(88, 62), (74, 66), (86, 70)]), INK)


def sheep(c, col=W):
    m = c.ell(56, 66, 34, 24) | c.ell(40, 56, 16) | c.ell(64, 50, 18) | c.ell(80, 58, 16) | c.ell(42, 78, 14) | c.ell(70, 80, 16)
    for x in (42, 74):
        c.put(c.rect(x - 4, 80, x + 4, 112, 3), DG)
    c.put(m, col)
    c.put(c.ell(96, 58, 15, 12, 20), DG)
    c.detail(c.ell(100, 54, 2.5), W)


def ooze(c, col=LGREEN):
    pts = bez((14, 112), (6, 70), (24, 26), (64, 24)) + bez((64, 24), (104, 26), (122, 70), (114, 112))
    pts += bez((114, 112), (100, 104), (98, 122), (84, 116)) + bez((84, 116), (70, 108), (58, 124), (44, 114)) + \
        bez((44, 114), (32, 106), (26, 118), (14, 112))
    c.put(c.poly(pts), col)
    c.flat(c.ell(42, 46, 8, 12, -30), tuple(min(255, int(v * 1.25 + 20)) for v in col))


# ---------- elements ----------
def flame(c, col=ORANGE, inner=YELLOW):
    pts = bez((64, 124), (16, 124), (14, 80), (34, 54)) + bez((34, 54), (40, 72), (48, 74), (50, 70)) + \
        bez((50, 70), (40, 40), (56, 18), (70, 6)) + bez((70, 6), (68, 34), (96, 44), (100, 68)) + \
        bez((100, 68), (104, 62), (106, 56), (104, 48)) + bez((104, 48), (124, 76), (112, 124), (64, 124))
    c.put(c.poly(pts), col)
    ip = bez((64, 122), (38, 122), (38, 94), (56, 74)) + bez((56, 74), (60, 90), (70, 92), (72, 86)) + \
        bez((72, 86), (86, 96), (92, 122), (64, 122))
    c.put(c.poly(ip), inner, ol=0, shade=0)


def drop(c, col=BLUE):
    pts = bez((64, 8), (60, 40), (24, 64), (24, 86)) + bez((24, 86), (24, 110), (44, 124), (64, 124)) + \
        bez((64, 124), (84, 124), (104, 110), (104, 86)) + bez((104, 86), (104, 64), (68, 40), (64, 8))
    c.put(c.poly(pts), col)
    c.flat(c.ell(46, 92, 6, 12, 20), tuple(min(255, int(v * 1.3 + 30)) for v in col))


def snowflake(c, col=LBLUE):
    m = None
    for i in range(6):
        a = math.radians(i * 60 - 90)
        tip = (64 + 54 * math.cos(a), 64 + 54 * math.sin(a))
        mm = c.line([(64, 64), tip], 9)
        for f in (0.55, 0.8):
            b = (64 + 54 * f * math.cos(a), 64 + 54 * f * math.sin(a))
            for da in (-40, 40):
                aa = a + math.radians(da) + math.pi
                mm |= c.line([b, (b[0] - 16 * math.cos(aa - math.pi), b[1] - 16 * math.sin(aa - math.pi))], 7)
        m = mm if m is None else (m | mm)
    c.put(m, col, shade=0.1)


def bolt(c, col=YELLOW):
    c.put(c.poly([(74, 4), (28, 70), (58, 70), (44, 124), (100, 50), (68, 50), (88, 4)]), col)


def rock(c, col=G):
    c.put(c.poly([(18, 104), (14, 70), (34, 34), (64, 20), (96, 34), (114, 68), (110, 104), (64, 116)]), col)
    dk = tuple(int(v * 0.72) for v in col)
    c.detail(c.line([(34, 34), (50, 64), (14, 70)], 2.4) | c.line([(50, 64), (64, 116)], 2.4) |
             c.line([(50, 64), (96, 52), (114, 68)], 2.4), dk)


def wind(c, col=LBLUE):
    for y, x0, x1, r in ((34, 14, 84, 14), (64, 6, 100, 16), (94, 20, 74, 12)):
        pts = [(x0, y), (x1, y)] + [(x1 + r * math.sin(t / 10 * math.pi * 1.5), y - r + r * math.cos(t / 10 * math.pi * 1.5))
                                    for t in range(11)]
        c.put(c.line(pts, 9), col, shade=0)


def cloud(c, col=W, y=64):
    m = c.ell(40, y + 6, 24, 20) | c.ell(64, y - 8, 26, 24) | c.ell(90, y + 4, 24, 22) | c.rect(18, y + 4, 112, y + 26, 12)
    c.put(m, col)


def sun(c, col=YELLOW):
    pts = []
    for i in range(24):
        r = 58 if i % 2 == 0 else 40
        a = math.radians(i * 15)
        pts.append((64 + r * math.cos(a), 64 + r * math.sin(a)))
    c.put(c.poly(pts), col)
    c.put(c.ell(64, 64, 30), (246, 226, 140), shade=0.1)


def star(c, col=GOLD, cx=64, cy=64, r=56, ri=None, n=5):
    ri = ri or r * 0.45
    pts = []
    for i in range(n * 2):
        rr = r if i % 2 == 0 else ri
        a = math.radians(-90 + i * 180 / n)
        pts.append((cx + rr * math.cos(a), cy + rr * math.sin(a)))
    c.put(c.poly(pts), col)


def sparkle(c, cx, cy, r, col=YELLOW):
    star(c, col, cx, cy, r, r * 0.3, 4)


def moon(c, col=(236, 222, 150)):
    c.put(c.ell(64, 64, 52) & ~c.ell(88, 50, 44), col)


def leaf(c, col=GREEN, ang=0):
    pts = rot(bez((64, 120), (6, 90), (24, 30), (64, 8)) + bez((64, 8), (104, 30), (122, 90), (64, 120)), ang)
    c.put(c.poly(pts), col)
    c.detail(c.line(rot([(64, 112), (64, 20)], ang), 3), tuple(int(v * 0.65) for v in col))


def tree(c, col=GREEN):
    c.put(c.poly([(54, 124), (58, 70), (70, 70), (74, 124)]), BROWN)
    c.put(c.ell(64, 50, 46, 40) | c.ell(36, 66, 22) | c.ell(92, 66, 22), col)


def halo(c, col=YELLOW, cy=30, rx=40, ry=12):
    c.put(c.ring(64, cy, rx - 8, rx, ry - 5, ry), col, shade=0)


def thermometer(c, fill=RED):
    c.put(c.rect(50, 6, 78, 100, 14) | c.ell(64, 104, 20), W)
    c.flat(c.rect(58, 40, 70, 100, 6) | c.ell(64, 104, 13), fill)


# ---------- objects ----------
def shield(c, col=G, rim=None):
    pts = [(18, 18), (110, 18)] + bez((110, 18), (112, 80), (90, 106), (64, 124)) + bez((64, 124), (38, 106), (16, 80), (18, 18))
    c.put(c.poly(pts), col)


def sword(c, col=LG, ang=45, hilt=BROWN, guard=GOLD, wide=1.0):
    T = lambda pts: rot(pts, ang)
    hw = 7 * wide
    c.put(c.poly(T([(64 - hw, 12), (64, 0), (64 + hw, 12), (64 + hw, 90), (64 - hw, 90)])), col)
    c.put(c.poly(T([(59, 92), (69, 92), (69, 124), (59, 124)])), hilt)
    c.put(c.poly(T([(40, 86), (88, 86), (88, 97), (40, 97)])), guard)


def spear(c, col=LG, ang=40, shaft=BROWN):
    T = lambda pts: rot(pts, ang)
    c.put(c.poly(T([(60, 30), (68, 30), (68, 128), (60, 128)])), shaft)
    c.put(c.poly(T([(64, 0), (78, 26), (64, 40), (50, 26)])), col)


def mace(c, col=GOLD, ang=35, shaft=BROWN):
    T = lambda pts: rot(pts, ang)
    c.put(c.poly(T([(59, 40), (69, 40), (69, 126), (59, 126)])), shaft)
    m = c.poly(star_pts(64, 30, 30, 18, 8, ang))
    c.put(m | c.ell(*rot([(64, 30)], ang)[0], 20), col)


def star_pts(cx, cy, r, ri, n, ang=0):
    pts = []
    for i in range(n * 2):
        rr = r if i % 2 == 0 else ri
        a = math.radians(-90 + i * 180 / n)
        pts.append((cx + rr * math.cos(a), cy + rr * math.sin(a)))
    return rot(pts, ang)


def anvil(c, col=DG):
    c.put(c.poly([(10, 34), (98, 34), (118, 44), (98, 58), (84, 60), (78, 84), (96, 96), (96, 110),
                  (32, 110), (32, 96), (50, 84), (44, 60), (26, 58)]), col)


def hammer(c, col=G, ang=-30):
    T = lambda pts: rot(pts, ang)
    c.put(c.poly(T([(59, 40), (69, 40), (69, 126), (59, 126)])), BROWN)
    c.put(c.poly(T([(30, 14), (98, 14), (98, 44), (30, 44)])), col)


def gear(c, col=G, cx=64, cy=64, r=48):
    m = c.poly(star_pts(cx, cy, r, r * 0.8, 10)) | c.ell(cx, cy, r * 0.82)
    c.put(m & ~c.ell(cx, cy, r * 0.3), col)


def chain(c, col=G, ang=-40, n=4):
    for i in range(n):
        y = 18 + i * 28
        p = rot([(64, y)], ang)[0]
        if i % 2 == 0:
            m = c.ell(p[0], p[1], 14, 22, ang) & ~c.ell(p[0], p[1], 6, 13, ang)
        else:
            m = c.ell(p[0], p[1], 7, 22, ang)
        c.put(m, col, shade=0.12)


def rope(c, col=TAN):
    for r in (46, 32, 18):
        c.put(c.ring(64, 72, r - 9, r, (r - 9) * 0.8, r * 0.8), col, shade=0.12)
    c.put(c.poly(taper(bez((106, 74), (118, 40), (96, 20), (84, 10)), 9, 9)), col)


def spikes(c, col=G):
    c.put(c.rect(10, 96, 118, 116, 4), BROWN)
    for x in (24, 46, 68, 90, 108):
        c.put(c.poly([(x - 10, 98), (x + 10, 98), (x, 40 + (x % 3) * 8)]), col, shade=0.1)


def crown(c, col=GOLD, y=64, h=44, gem=RED):
    pts = [(18, y + h / 2), (14, y - h / 2), (38, y - 4), (64, y - h / 2 - 8), (90, y - 4), (114, y - h / 2), (110, y + h / 2)]
    c.put(c.poly(pts), col)
    c.put(c.ell(64, y + 6, 8), gem, ol=2.4, shade=0)


def heart(c, col=PINK, cx=64, cy=64, s=1.0):
    pts = bez((64, 118), (20, 86), (6, 60), (14, 38)) + bez((14, 38), (24, 14), (56, 14), (64, 36)) + \
        bez((64, 36), (72, 14), (104, 14), (114, 38)) + bez((114, 38), (122, 60), (108, 86), (64, 118))
    pts = [(cx + (x - 64) * s, cy + (y - 64) * s) for x, y in pts]
    c.put(c.poly(pts), col)


def hourglass(c, col=TAN, sand=GOLD):
    c.put(c.rect(26, 8, 102, 22, 4) | c.rect(26, 106, 102, 120, 4), BROWN)
    c.put(c.poly([(34, 22), (94, 22), (68, 64), (94, 106), (34, 106), (60, 64)]), (226, 230, 236), shade=0)
    c.flat(c.poly([(46, 38), (82, 38), (66, 60), (62, 60)]) | c.poly([(64, 80), (84, 104), (44, 104)]), sand)


def zzz(c, col=W):
    for x, y, s in ((30, 70, 30), (66, 44, 24), (96, 22, 18)):
        c.put(c.line([(x - s / 2, y - s / 2), (x + s / 2, y - s / 2), (x - s / 2, y + s / 2), (x + s / 2, y + s / 2)], 7), col, shade=0)


def lamp(c, col=GOLD):
    body = bez((20, 84), (24, 112), (100, 112), (104, 84))
    c.put(c.poly(body + [(104, 84), (20, 84)]), col)
    c.put(c.poly(taper(bez((100, 92), (118, 86), (124, 70)), 12, 6)), col)
    c.put(c.poly(taper(bez((24, 96), (2, 96), (10, 76)), 8, 8)), col)
    c.put(c.ell(62, 80, 26, 8), col)
    c.put(c.ell(62, 70, 7), col)


def smoke(c, col=(200, 196, 220)):
    c.put(c.poly(taper(bez((100, 70), (120, 30), (60, 50), (76, 10), n=36), 18, 6)), col)


def coins(c, col=GOLD):
    for i, y in enumerate((104, 90, 76, 62)):
        c.put(c.ell(64, y, 38, 12), col, shade=0.2)


def note(c, col=DARK):
    c.put(c.ell(40, 100, 16, 12, -20) | c.ell(92, 88, 16, 12, -20), col)
    c.put(c.line([(54, 98), (54, 24), (106, 12), (106, 86)], 7), col, shade=0)


def mug(c, col=TAN, foam=W):
    c.put(c.ring(92, 74, 12, 22), BROWN)
    c.put(c.rect(26, 40, 94, 120, 8), BROWN)
    c.flat(c.rect(32, 48, 88, 112, 4), GOLD)
    c.put(c.ell(40, 40, 16, 12) | c.ell(62, 36, 16, 14) | c.ell(84, 40, 14, 12), foam, shade=0)


def mask(c, col=W):
    pts = bez((20, 30), (64, 14), (64, 14), (108, 30)) + bez((108, 30), (112, 90), (84, 120), (64, 120)) + \
        bez((64, 120), (44, 120), (16, 90), (20, 30))
    c.put(c.poly(pts), col)
    c.detail(c.ell(46, 56, 10, 7, -10) | c.ell(82, 56, 10, 7, 10), INK)
    c.detail(c.line(bez((48, 92), (64, 100), (80, 92)), 3), INK)


def maze(c, col=TAN):
    c.put(c.rect(10, 10, 118, 118, 6), col)
    dk = tuple(int(v * 0.5) for v in col)
    for pts in ([(24, 24), (104, 24), (104, 104), (40, 104)], [(24, 24), (24, 104)],
                [(40, 40), (88, 40), (88, 88), (56, 88), (56, 56), (72, 56)]):
        c.detail(c.line(pts, 6), dk)


def scales(c, col=TEAL, rows=4):
    m = c.rect(14, 14, 114, 114, 14)
    c.put(m, col)
    dk = tuple(int(v * 0.65) for v in col)
    for r in range(rows):
        y = 26 + r * 24
        off = 0 if r % 2 == 0 else 12
        for x in range(14 + off, 120, 24):
            c.detail(c.arc(x + 12, y, 12, 10, 170, 2.6), dk)


def arrow(c, up=True, cx=104, cy=100, s=1.0, col=None):
    col = col or (GREEN if up else RED)
    pts = [(0, -22), (20, 0), (8, 0), (8, 22), (-8, 22), (-8, 0), (-20, 0)]
    if not up:
        pts = [(x, -y) for x, y in pts]
    c.put(c.poly([(cx + x * s, cy + y * s) for x, y in pts]), col, shade=0.1)


def plus(c, cx=104, cy=100, s=1.0, col=GREEN):
    c.put(c.rect(cx - 6 * s, cy - 20 * s, cx + 6 * s, cy + 20 * s) | c.rect(cx - 20 * s, cy - 6 * s, cx + 20 * s, cy + 6 * s), col, shade=0)


def cross(c, cx=104, cy=100, s=1.0, col=RED):
    m = c.line([(cx - 16 * s, cy - 16 * s), (cx + 16 * s, cy + 16 * s)], 11 * s) | \
        c.line([(cx + 16 * s, cy - 16 * s), (cx - 16 * s, cy + 16 * s)], 11 * s)
    c.put(m, col, shade=0)


def swirl(c, col=PURPLE, cx=64, cy=64, r=50, turns=2.2, w=9):
    pts = []
    n = 80
    for i in range(n):
        t = i / (n - 1)
        a = t * turns * 2 * math.pi
        rr = r * (1 - t * 0.85)
        pts.append((cx + rr * math.cos(a), cy + rr * math.sin(a)))
    c.put(c.line(pts, w), col, shade=0)


def burst(c, col=ORANGE, inner=YELLOW, cx=64, cy=64, r=58):
    c.put(c.poly(star_pts(cx, cy, r, r * 0.55, 12)), col)
    c.put(c.poly(star_pts(cx, cy, r * 0.55, r * 0.32, 8, 10)), inner, ol=0, shade=0)


def cap(c, col=RED):
    c.put(c.poly(bez((14, 100), (30, 60), (60, 10), (108, 8)) + bez((108, 8), (96, 30), (100, 70), (114, 100))), col)
    c.put(c.rect(10, 92, 118, 116, 8), tuple(int(v * 0.8) for v in col))


def bed(c):
    c.put(c.rect(14, 60, 114, 104, 6), BROWN)
    c.put(c.rect(20, 50, 108, 82, 6), BLUE)
    c.put(c.rect(20, 46, 50, 66, 8), W)
    c.put(c.rect(10, 40, 20, 116, 3) | c.rect(108, 70, 118, 116, 3), BROWN)


def footprint(c, col=G, cx=64, cy=64, s=1.0, ang=0):
    with c.at(cx - 64 * s, cy - 64 * s, s):
        m = c.ell(64, 76, 22, 34, ang)
        for i, (x, y, r) in enumerate(((44, 32, 7), (56, 26, 8), (70, 26, 8), (82, 32, 7))):
            m |= c.ell(x, y, r)
        c.put(m, col)


def braid(c, col=TAN, x=64):
    for i in range(6):
        y = 14 + i * 18
        c.put(c.ell(x + (5 if i % 2 else -5), y, 14, 11, 25 if i % 2 else -25), col, shade=0.15)
    c.put(c.ell(x, 124 - 6, 9), LBLUE, ol=2.6)


# ---------- added motifs ----------
def curved_arrow(c, col=LBLUE, flip=False):
    pts = bez((14, 112), (14, 40), (70, 26), (96, 46), n=30)
    if flip:
        pts = mirror(pts)
    c.put(c.poly(taper(pts, 8, 16)), col, shade=0.1)
    hx, hy = pts[-1]
    s = -1 if flip else 1
    c.put(c.poly([(hx + s * 22, hy + 22), (hx - s * 12, hy + 26), (hx + s * 8, hy - 12)]), col, shade=0.1)


def wavy(c, col=LGREEN, n=3, x0=34, dx=30, y0=16, y1=112, w=11):
    for i in range(n):
        x = x0 + i * dx
        pts = [(x + 9 * math.sin((y - y0) / (y1 - y0) * 3 * math.pi), y) for y in range(int(y0), int(y1) + 1, 4)]
        c.put(c.line(pts, w), col, shade=0)


def gift(c, col=RED, ribbon=GOLD):
    c.put(c.rect(16, 52, 112, 120, 4), col)
    c.put(c.rect(10, 36, 118, 58, 4), col)
    c.flat(c.rect(56, 37, 72, 119), ribbon)
    for s in (-1, 1):
        c.put(c.poly(bez((64, 36), (64 + s * 8, 6), (64 + s * 40, 10), (64, 36))), ribbon, shade=0)


def speech(c, col=W, cx=64, cy=56):
    m = c.ell(cx, cy, 52, 38) | c.poly([(cx - 30, cy + 20), (cx - 40, cy + 62), (cx - 2, cy + 30)])
    c.put(m, col)


def beard(c, col=BROWN, cy=62):
    pts = bez((40, cy - 6), (36, cy + 34), (54, cy + 52), (64, cy + 58)) + bez((64, cy + 58), (74, cy + 52), (92, cy + 34), (88, cy - 6))
    pts += bez((88, cy - 6), (76, cy + 8), (52, cy + 8), (40, cy - 6))
    c.put(c.poly(pts), col)


def elf_head(c, col=W, cx=64, cy=56):
    for s in (-1, 1):
        c.put(c.poly([(cx + s * 16, cy - 6), (cx + s * 46, cy - 26), (cx + s * 18, cy + 12)]), col)
    head(c, cx, cy, 21, 27, col)


def clover(c, col=GREEN):
    c.put(c.poly(taper(bez((64, 64), (70, 96), (84, 110), (96, 122)), 8, 6)), col)
    for a in (0, 90, 180, 270):
        p = rot([(64, 36)], a + 45, 64, 60)[0]
        heart(c, col, p[0], p[1], 0.38)


def kobold_head(c, col=(176, 96, 70), cx=64, cy=60):
    pts = bez((cx - 30, cy + 20), (cx - 40, cy - 30), (cx + 10, cy - 40), (cx + 30, cy - 12)) + \
        bez((cx + 30, cy - 12), (cx + 50, cy - 4), (cx + 58, cy + 12), (cx + 54, cy + 22)) + \
        bez((cx + 54, cy + 22), (cx + 20, cy + 36), (cx - 10, cy + 40), (cx - 30, cy + 20))
    for s, dx in ((1, -22), (1, -8)):
        horn(c, (cx + dx, cy - 26), (cx + dx - 14, cy - 40), (cx + dx - 26, cy - 44), 9, BONE)
    c.put(c.poly(pts), col)
    c.detail(c.ell(cx + 4, cy - 8, 6, 5), YELLOW)
    c.detail(c.ell(cx + 5, cy - 8, 2, 4), INK)
    c.detail(c.line(bez((cx + 54, cy + 18), (cx + 30, cy + 22), (cx + 14, cy + 18)), 2.4), INK)


def goblin_head(c, col=(138, 160, 80), cx=64, cy=60, angry=False):
    for s in (-1, 1):
        c.put(c.poly([(cx + s * 18, cy - 10), (cx + s * 60, cy - 22), (cx + s * 22, cy + 14)]), col)
    c.put(c.ell(cx, cy, 28, 32), col)
    c.detail(c.ell(cx - 11, cy - 2, 5, 4) | c.ell(cx + 11, cy - 2, 5, 4), YELLOW)
    c.detail(c.ell(cx - 11, cy - 2, 2) | c.ell(cx + 11, cy - 2, 2), INK)
    if angry:
        c.detail(c.line([(cx - 20, cy - 16), (cx - 5, cy - 9)], 3) | c.line([(cx + 20, cy - 16), (cx + 5, cy - 9)], 3), INK)
    c.detail(c.line(bez((cx - 12, cy + 16), (cx, cy + 20), (cx + 12, cy + 16)), 2.6), INK)


def ghost(c, col=LPURPLE):
    pts = bez((22, 120), (16, 60), (30, 12), (64, 12)) + bez((64, 12), (98, 12), (112, 60), (106, 120))
    pts += [(92, 104), (78, 120), (64, 104), (50, 120), (36, 104)]
    c.put(c.poly(pts), col)
    c.detail(c.ell(50, 54, 8, 11) | c.ell(78, 54, 8, 11) | c.ell(64, 80, 7, 9), INK)


def scroll(c, col=TAN, seal=CRIM):
    c.put(c.rect(24, 16, 104, 112, 4), col)
    c.put(c.ell(24, 64, 10, 50) | c.ell(104, 64, 10, 50), tuple(int(v * 0.85) for v in col))
    for y in (36, 50, 64):
        c.detail(c.line([(40, y), (88, y)], 3), tuple(int(v * 0.6) for v in col))
    c.put(c.ell(76, 90, 14), seal, shade=0.1)


def target(c, col=RED, cx=64, cy=64, r=52):
    c.put(c.ring(cx, cy, r * 0.62, r), col, shade=0)
    c.put(c.ell(cx, cy, r * 0.25), col, shade=0)
    m = c.rect(cx - 4, cy - r - 10, cx + 4, cy - r * 0.45) | c.rect(cx - 4, cy + r * 0.45, cx + 4, cy + r + 10) | \
        c.rect(cx - r - 10, cy - 4, cx - r * 0.45, cy + 4) | c.rect(cx + r * 0.45, cy - 4, cx + r + 10, cy + 4)
    c.put(m, col, shade=0)


def egg(c, col=BONE):
    c.put(c.poly(bez((64, 8), (20, 8), (16, 120), (64, 120)) + bez((64, 120), (112, 120), (108, 8), (64, 8))), col)


def moth_wings(c, col=LPURPLE, cy=56):
    for s in (-1, 1):
        c.put(c.ell(64 + s * 30, cy - 10, 28, 24, s * -25) | c.ell(64 + s * 24, cy + 24, 18, 16, s * 20), col)
        c.detail(c.ell(64 + s * 34, cy - 12, 7), tuple(int(v * 0.7) for v in col))


def rain(c, col=LBLUE, y0=78):
    for x in (30, 52, 74, 96):
        c.put(c.line([(x + 6, y0 + (x % 3) * 6), (x - 4, y0 + 30 + (x % 3) * 6)], 6), col, shade=0)


def crook(c, col=BROWN):
    c.put(c.line([(96, 124), (96, 30)] + [(96 - 14 + 14 * math.cos(t / 10 * math.pi), 30 - 14 * math.sin(t / 10 * math.pi)) for t in range(11)] + [(68, 42)], 7), col, shade=0)


def tusks(c, cx=64, cy=78, col=W, h=18):
    for s in (-1, 1):
        c.put(c.poly([(cx + s * 16 - 5, cy), (cx + s * 16 + 5, cy), (cx + s * 18, cy - h)]), col, ol=2.4, shade=0)


def exclaim(c, col=YELLOW, cx=64):
    c.put(c.poly([(cx - 12, 10), (cx + 12, 10), (cx + 7, 82), (cx - 7, 82)]), col)
    c.put(c.ell(cx, 104, 11), col)


def speed_lines(c, col=W, x0=8, ys=(40, 64, 88), L=30):
    for i, y in enumerate(ys):
        c.put(c.line([(x0 + (i % 2) * 8, y), (x0 + (i % 2) * 8 + L, y)], 7), col, shade=0)


def snake(c, col=TEAL):
    pts = bez((14, 110), (60, 130), (80, 80), (50, 64), n=30) + bez((50, 64), (20, 48), (40, 10), (90, 22), n=30)[1:]
    c.put(c.poly(taper(pts, 10, 22)), col)
    c.put(c.ell(96, 24, 16, 12, -10), col)
    c.detail(c.ell(100, 20, 3), INK)


def pick(c, col=G, ang=-30):
    T = lambda pts: rot(pts, ang)
    c.put(c.poly(T([(59, 30), (69, 30), (69, 126), (59, 126)])), BROWN)
    c.put(c.poly(T(bez((8, 44), (40, 10), (88, 10), (120, 44)) + bez((120, 44), (88, 26), (40, 26), (8, 44)))), col)


def dagger(c, col=LG, ang=40):
    T = lambda pts: rot(pts, ang)
    c.put(c.poly(T([(56, 34), (64, 6), (72, 34), (72, 84), (56, 84)])), col)
    c.put(c.poly(T([(59, 92), (69, 92), (69, 120), (59, 120)])), BROWN)
    c.put(c.poly(T([(44, 84), (84, 84), (84, 94), (44, 94)])), DG)


def ribs(c, col=BONE):
    c.put(c.rect(58, 20, 70, 116, 4), col)
    for y in (34, 54, 74, 94):
        for s in (-1, 1):
            c.put(c.poly(taper(bez((64, y), (64 + s * 34, y - 6), (64 + s * 36, y + 16)), 9, 6)), col, shade=0.1)


def nose_ring(c, col=TAN):
    c.put(c.ell(64, 60, 46, 36), col)
    c.detail(c.ell(46, 62, 9, 12) | c.ell(82, 62, 9, 12), INK)
    c.put(c.arc(64, 84, 20, 10, 170, 6), GOLD, shade=0, ol=2.4)


def two_heads(c, col=W, closed=None):
    torso(c, col, 1.35)
    for cx, i in ((42, 0), (86, 1)):
        head(c, cx, 52, 18, 23, col)
        if closed == i:
            c.detail(c.line([(cx - 10, 52), (cx - 3, 54)], 2.6) | c.line([(cx + 3, 54), (cx + 10, 52)], 2.6), INK)


def portal(c, col=PURPLE):
    c.put(c.ell(64, 64, 40, 54), col)
    c.put(c.ell(64, 64, 28, 40), (60, 40, 90), ol=0, shade=0)
    swirl(c, LPURPLE, 64, 64, 26, 1.6, 5)


def badge(c, fn, x=64, y=64, k=0.47, **kw):
    with c.at(x, y, k):
        fn(c, **kw)
