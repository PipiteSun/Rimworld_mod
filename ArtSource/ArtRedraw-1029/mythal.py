import sys, math
from bench import *

MSTONE = '#8c9098'
MSTONE_D = '#5e626a'
MBLUE = '#6fa8d8'
MBLUE_L = '#d6ecfa'


def swirl(c, cx, cy, rx, ry, col_out, col_in, n=3, turns=1.6, w=3.0):
    for k in range(n):
        pts = []
        for i in range(60):
            t = i / 59
            a = 2 * math.pi * (turns * t + k / n)
            r = (1 - t) * 0.92 + 0.08
            pts.append((cx + rx * r * math.cos(a), cy + ry * r * math.sin(a)))
        c._put(c.line_mask(pts, w), mix(col_out, col_in, 0.5), 0.85)


def gate(c, sealed=False):
    W = 384
    # stone dais
    c.paint(c.rect(30, 300, 354, 366, 6), MSTONE_D, ol=3, drop=0.4, tex=c.blocks(40, 16, 0.75), shade=0.25, hl=0.1)
    c.paint(c.rect(46, 280, 338, 318, 5), MSTONE, ol=3, tex=c.blocks(48, 19, 0.78), shade=0.25, hl=0.15)
    # pillars + arch
    outer = c.rect(70, 60, 314, 300, 0) | c.ell(192, 110, 122, 92)
    inner = c.rect(116, 112, 268, 300, 0) | c.ell(192, 128, 76, 64)
    arch = outer & ~inner & c.rect(0, 0, W, 296)
    c.paint(arch, MSTONE, ol=3.2, drop=0.45, tex=c.blocks(30, 22, 0.75, 0.08), shade=0.3, hl=0.15)
    # keystone + capitals
    c.paint(c.poly([(174, 14), (210, 14), (204, 52), (180, 52)]), '#a6aab2', ol=2.6, shade=0.25, hl=0.2)
    for x in (70, 268):
        c.paint(c.rect(x - 6, 168, x + 52, 184, 3), '#a6aab2', ol=2.4, shade=0.25, hl=0.2)
        c.paint(c.rect(x - 6, 276, x + 52, 298, 3), '#a6aab2', ol=2.4, shade=0.25, hl=0.2)
    # glowing mythal runes on the arch
    for i in range(9):
        a = math.radians(200 + i * 17.5)
        x, y = 192 + 100 * math.cos(a), 118 + 78 * math.sin(a)
        g = c.line_mask([(x - 3, y - 3), (x, y + 3), (x + 3, y - 3)], 1.6)
        if not sealed:
            c.glow(g, MBLUE, 3, 0.6)
        c.fill(g, MBLUE_L if not sealed else '#4c5058', 1)
    for x in (93, 291):
        for j in range(4):
            g = c.line_mask([(x - 4, 200 + j * 18), (x + 4, 200 + j * 18), (x, 208 + j * 18)], 1.6)
            if not sealed:
                c.glow(g, MBLUE, 3, 0.5)
            c.fill(g, MBLUE_L if not sealed else '#4c5058', 1)
    if sealed:
        wall = inner & c.rect(0, 0, W, 296)
        c.paint(wall, '#6a6e76', ol=2.4, tex=c.blocks(26, 16, 0.65, 0.1), shade=0.25, hl=0.05)
        chain(c, [(118, 160), (192, 200), (266, 160)], '#4c4e52', 9)
        chain(c, [(118, 250), (192, 210), (266, 250)], '#4c4e52', 9)
        c.paint(c.ell(192, 205, 18, 20), '#5c4a3a', ol=2.6, shade=0.3, hl=0.2)
        c.paint(c.rect(184, 206, 200, 222, 3), '#3a2e26', ol=1.6, shade=0.2, hl=0.1)
        c.paint(c.ell(192, 200, 7), '#3a2e26', ol=1.6, shade=0.2, hl=0.1)
    else:
        portal = inner & c.rect(0, 0, W, 296)
        c.paint(portal, '#1c2a46', ol=2.4, shade=0.0, hl=0.0)
        swirl(c, 192, 196, 66, 84, MBLUE, MBLUE_L, 4, 1.8, 3.4)
        c.glow(c.ell(192, 196, 14, 18), MBLUE_L, 14, 0.6, only_on=True)
        c.glow(portal & ~c.dilate(portal & c.ell(192, 196, 64, 82), 0), MBLUE, 6, 0.3, only_on=True)


def exit_pad(c):
    cx, cy = 192, 200
    c.paint(c.ell(cx, cy + 12, 168, 150), MSTONE_D, ol=3, drop=0.4, tex=c.blocks(36, 22, 0.75), shade=0.25, hl=0.1)
    c.paint(c.ell(cx, cy, 160, 142), MSTONE, ol=3, tex=c.blocks(44, 26, 0.78, 0.06), shade=0.25, hl=0.15)
    c.paint(c.ell(cx, cy, 118, 104), '#7a7e86', ol=2.6, shade=0.2, hl=0.1)
    pool = c.ell(cx, cy, 98, 86)
    c.paint(pool, '#1c2a46', ol=2.4, shade=0, hl=0)
    swirl(c, cx, cy, 92, 80, MBLUE, MBLUE_L, 4, 1.6, 3.6)
    c.glow(c.ell(cx, cy, 16, 14), MBLUE_L, 16, 0.6, only_on=True)
    rune_marks(c, cx, cy, 138, 22, MBLUE_L, 1.8, 4.5)
    for i in range(4):
        a = math.radians(45 + i * 90)
        x, y = cx + 150 * math.cos(a), cy + 132 * math.sin(a)
        c.paint(c.rect(x - 14, y - 34, x + 14, y + 10, 4), '#a6aab2', ol=2.6, drop=0.4, shade=0.3, hl=0.2)
        c.glow(c.poly([(x, y - 30), (x + 8, y - 14), (x, y), (x - 8, y - 14)]), MBLUE, 6, 0.5)
        c.paint(c.poly([(x, y - 30), (x + 8, y - 14), (x, y), (x - 8, y - 14)]), MBLUE_L, ol=1.8, shade=0.25, hl=0.2)


def lamp(c):
    c.paint(c.ell(64, 104, 34, 14), MSTONE_D, ol=2.4, drop=0.4, shade=0.25, hl=0.1)
    c.paint(c.rect(52, 62, 76, 104, 3), MSTONE, ol=2.4, tex=c.blocks(12, 10, 0.75), shade=0.3, hl=0.15)
    c.paint(c.rect(44, 56, 84, 66, 3), '#a6aab2', ol=2.2, shade=0.25, hl=0.2)
    c.glow(c.ell(64, 36, 20, 24), MBLUE, 16, 0.45)
    for pts, f in (([(50, 56), (46, 32), (54, 20), (60, 56)], 0.85), ([(78, 56), (84, 30), (76, 22), (68, 56)], 0.85),
                   ([(56, 58), (58, 22), (64, 6), (72, 22), (72, 58)], 1.0)):
        c.paint(c.poly(pts), mul('#9ccbec', f), ol=2.2, shade=0.25, hl=0.25, grain=0.01)
    c.fill(c.poly([(62, 12), (60, 40), (64, 48)]), '#ffffff', 0.6)
    g = c.line_mask([(58, 80), (64, 88), (70, 80)], 1.6)
    c.glow(g, MBLUE, 3, 0.6); c.fill(g, MBLUE_L, 1)


def fountain(c):
    cx, cy = 128, 132
    c.paint(c.ell(cx, cy + 10, 112, 100), MSTONE_D, ol=3, drop=0.4, tex=c.blocks(30, 18, 0.75), shade=0.25, hl=0.1)
    c.paint(c.ell(cx, cy, 108, 94), MSTONE, ol=3, tex=c.blocks(34, 20, 0.78), shade=0.25, hl=0.2)
    water = c.ell(cx, cy + 4, 88, 74)
    c.paint(water, '#3f7aa6', ol=2.6, shade=0.0, hl=0.0)
    c.darken(water & ~c.shift(water, 0, 8), 0.65)
    for r in (70, 52, 34):
        c._put(c.ring(cx, cy + 6, r - 1.2, r + 1.2, r * 0.84 - 1, r * 0.84 + 1), '#8fc3e6', 0.45)
    c.paint(c.ell(cx, cy + 18, 30, 16), MSTONE_D, ol=2.4, shade=0.25, hl=0.1)
    c.paint(c.rect(cx - 12, cy - 50, cx + 12, cy + 18, 4), MSTONE, ol=2.6, tex=c.blocks(12, 14, 0.78), shade=0.3, hl=0.2)
    c.paint(c.ell(cx, cy - 52, 34, 16), '#a6aab2', ol=2.6, shade=0.25, hl=0.2)
    c.paint(c.ell(cx, cy - 54, 26, 10), '#3f7aa6', ol=1.6, shade=0, hl=0)
    c.glow(c.ell(cx, cy - 72, 8, 10), MBLUE_L, 10, 0.5)
    c.paint(c.poly([(cx, cy - 92), (cx + 9, cy - 72), (cx, cy - 56), (cx - 9, cy - 72)]), '#9ccbec', ol=2.2, shade=0.25, hl=0.25)
    for k in (-1, 1):
        c._put(c.arc_mask(cx + k * 22, cy - 52, 22, 180 if k < 0 else 0, 270 if k < 0 else -90, 2.6, 40) & c.rect(0, 0, 256, cy + 20), '#bfe2f6', 0.75)


def netheril_altar(c):
    cx, cy = 192, 196
    oct8 = lambda r, ry: [(cx + r * math.cos(math.radians(22.5 + i * 45)), cy + ry * math.sin(math.radians(22.5 + i * 45))) for i in range(8)]
    c.paint(c.poly([(x, y + 14) for x, y in oct8(176, 166)]), '#4c4a56', ol=3, drop=0.4, shade=0.2, hl=0.0)
    c.paint(c.poly(oct8(176, 166)), '#7c7a8a', ol=3, tex=c.blocks(46, 30, 0.78, 0.06), shade=0.25, hl=0.15)
    c.paint(c.poly(oct8(130, 122)), '#9896a4', ol=2.8, tex=c.blocks(36, 24, 0.8), shade=0.25, hl=0.15)
    c._put(c.ring(cx, cy, 100, 106, 94, 100), GOLD, 0.9)
    rune_marks(c, cx, cy, 115, 18, '#e8d9a0', 1.8, 4.5)
    disc = c.ell(cx, cy, 92, 86)
    c.paint(disc, '#3c3450', ol=2.4, shade=0.1, hl=0)
    for i in range(6):
        a = math.radians(i * 60)
        c._put(c.line_mask([(cx, cy), (cx + 88 * math.cos(a), cy + 82 * math.sin(a))], 1.6), '#8a6fc4', 0.7)
    c._put(c.ring(cx, cy, 52, 55, 48, 51), '#8a6fc4', 0.8)
    # four braziers
    for i in range(4):
        a = math.radians(45 + i * 90)
        x, y = cx + 150 * math.cos(a), cy + 140 * math.sin(a)
        c.paint(c.ell(x, y + 6, 16, 9), '#3c3a40', ol=2.4, drop=0.4, shade=0.25, hl=0.2)
        c.paint(c.ell(x, y + 2, 13, 6), '#2a2622', ol=1.4, shade=0, hl=0)
        with c.zoom(x, y, 1.5):
            flame(c, x, y, 0.9, '#9a7ae0', '#e8dcff')
    # central pedestal with floating orb
    c.paint(c.rect(cx - 22, cy - 6, cx + 22, cy + 36, 4), '#a6a4b2', ol=2.8, drop=0.45, shade=0.3, hl=0.2)
    c.paint(c.rect(cx - 30, cy + 26, cx + 30, cy + 42, 4), '#8a8896', ol=2.6, shade=0.25, hl=0.15)
    c.paint(c.rect(cx - 28, cy - 14, cx + 28, cy - 2, 3), GOLD, ol=2.4, shade=0.25, hl=0.25)
    orb = c.ell(cx, cy - 50, 26)
    c.glow(orb, '#c4aaff', 22, 0.6)
    c.paint(orb, '#a184e6', ol=2.6, shade=0.3, hl=0.0, grain=0.01)
    c.fill(c.ell(cx - 8, cy - 60, 8, 6), '#e8dcff', 0.7)
    c.paint(c.ring(cx, cy - 50, 34, 38, 9, 12), GOLD, ol=1.8, shade=0.2, hl=0.3)


def planar_rift(c):
    cx, cy = 128, 132
    rng = c.rng
    c._put(c.ell(cx, cy + 6, 116, 100), '#2a2230', 0.45)
    for i in range(16):
        a = rng.uniform(0, 6.283); r = rng.uniform(86, 112)
        x, y = cx + r * math.cos(a), cy + r * 0.88 * math.sin(a)
        s = rng.uniform(7, 15)
        pts = [(x + s * math.cos(t) * rng.uniform(0.7, 1.1), y + s * 0.8 * math.sin(t) * rng.uniform(0.7, 1.1)) for t in [j * 1.047 for j in range(6)]]
        c.paint(c.poly(pts), mix('#6e6872', '#8f8a90', rng.uniform(0, 1)), ol=2.2, drop=0.4, shade=0.3, hl=0.2)
    hole = c.ell(cx, cy, 86, 74)
    c.paint(hole, '#2b1846', ol=3, shade=0, hl=0)
    swirl(c, cx, cy, 80, 68, '#8a4fd0', '#e0c8ff', 5, 1.9, 4.0)
    c.paint(c.ell(cx, cy, 30, 26), '#0c0614', ol=0, shade=0, hl=0)
    c.glow(c.ring(cx, cy, 80, 88, 68, 76), '#b47aff', 10, 0.35)
    for i in range(10):
        a = rng.uniform(0, 6.283); r = rng.uniform(30, 70)
        with c.zoom(cx + r * math.cos(a), cy + r * 0.86 * math.sin(a), 1):
            c.fill(c.ell(cx + r * math.cos(a), cy + r * 0.86 * math.sin(a), 1.6), '#f4eaff', 0.9)


def tower_wreck(c):
    rng = c.rng
    cx, cy = 192, 210
    # scattered masonry footprint
    c._put(c.ell(cx, cy + 10, 176, 150), '#3a3632', 0.35)
    for i in range(46):
        a = rng.uniform(0, 6.283); r = rng.uniform(20, 160) ** 1.0
        x, y = cx + r * math.cos(a), cy + r * 0.85 * math.sin(a)
        bw, bh = rng.uniform(14, 34), rng.uniform(10, 20)
        ang = rng.uniform(-0.6, 0.6)
        ca, sa = math.cos(ang), math.sin(ang)
        pts = [(x + px * ca - py * sa, y + px * sa + py * ca) for px, py in ((-bw / 2, -bh / 2), (bw / 2, -bh / 2), (bw / 2, bh / 2), (-bw / 2, bh / 2))]
        c.paint(c.poly(pts), mix('#7a7480', '#a29ca6', rng.uniform(0, 1)), ol=2.2, drop=0.45, shade=0.32, hl=0.2)
    # broken round tower base
    base = c.ring(cx - 20, cy - 10, 54, 84, 46, 72)
    gap = c.poly([(cx - 20, cy - 10), (cx + 120, cy - 60), (cx + 120, cy + 70)])
    c.paint(base & ~gap, '#8a8490', ol=3, drop=0.45, tex=c.blocks(22, 16, 0.75, 0.08), shade=0.3, hl=0.15)
    c.paint(c.ell(cx - 20, cy - 10, 54, 46), '#4a4450', ol=0, shade=0.1, hl=0, tex=c.planks(True, 12, 0.08, 0.7))
    # snapped spire lying across
    sp = c.poly([(cx + 40, cy + 40), (cx + 150, cy - 110), (cx + 166, cy - 96), (cx + 62, cy + 54)])
    c.paint(sp, '#7a4a5a', ol=3, drop=0.45, tex=c.blocks(20, 14, 0.75), shade=0.3, hl=0.15)
    c.paint(c.poly([(cx + 150, cy - 110), (cx + 176, cy - 150), (cx + 166, cy - 96)]), '#8e3a3a', ol=2.6, shade=0.3, hl=0.2)
    # arcane crystals still glowing among the rubble
    for x, y, s in ((cx - 110, cy - 60, 1.3), (cx - 60, cy + 90, 1.0), (cx + 90, cy + 80, 1.1), (cx - 20, cy - 10, 1.6)):
        g = c.poly([(x, y - 22 * s), (x + 8 * s, y - 4 * s), (x + 4 * s, y + 6 * s), (x - 5 * s, y + 6 * s), (x - 8 * s, y - 6 * s)])
        c.glow(g, '#a88ae8', 10 * s, 0.4)
        c.paint(g, '#8d6cc4', ol=2.2, shade=0.3, hl=0.2, grain=0.01)
    # scattered pages
    for i in range(6):
        x, y = cx + rng.uniform(-150, 150), cy + rng.uniform(-120, 130)
        c.paint(c.rect(x - 7, y - 5, x + 7, y + 5, 1), PAPER, ol=1.6, drop=0.3, shade=0.15, hl=0)


if __name__ == '__main__':
    out = sys.argv[1]
    jobs = {
        'GateOpen': (384, 384, lambda c: gate(c, False)),
        'GateSealed': (384, 384, lambda c: gate(c, True)),
        'Exit': (384, 384, exit_pad),
        'Lamp': (128, 128, lamp),
        'Fountain': (256, 256, fountain),
        'LP_NetherilAltar': (384, 384, netheril_altar),
        'LP_PlanarRift': (256, 256, planar_rift),
        'LP_TowerWreck': (384, 384, tower_wreck),
    }
    for nm in (sys.argv[2:] or jobs):
        w, h, fn = jobs[nm]
        c = Canvas(w, h, seed=9)
        fn(c)
        c.save(f'{out}/{nm}.png')
