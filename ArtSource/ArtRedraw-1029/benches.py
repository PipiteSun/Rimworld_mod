import sys, math
from bench import *


def rune_disc(c, x, y, r, glowc=ARCANE, lightc=ARCANE_L, metal=BRASS):
    c.paint(c.ell(x, y, r, r * 0.9), metal, ol=2.4, drop=0.3, shade=0.25, hl=0.2)
    c.paint(c.ell(x, y, r * 0.84, r * 0.75), '#2f2838', ol=1.4, shade=0.1, hl=0.0)
    inner = c.ell(x, y, r * 0.72, r * 0.64)
    c.paint(inner, mix('#2f2838', glowc, 0.55), ol=0, shade=0.15, hl=0.0)
    c.stroke([(x + r * 0.6 * math.cos(t / 20 * 2 * math.pi), y + r * 0.53 * math.sin(t / 20 * 2 * math.pi)) for t in range(21)],
             lightc, 1.2, 0.9)
    rune_marks(c, x, y, r * 0.0 + 1, 0, lightc)
    for i in range(8):
        a = math.radians(i * 45 + 22)
        c.stroke([(x + r * 0.62 * math.cos(a), y + r * 0.55 * math.sin(a)),
                  (x + r * 0.78 * math.cos(a), y + r * 0.69 * math.sin(a))], mul(metal, 0.7), 1.6)
    tri = [(x + r * 0.45 * math.cos(math.radians(a)), y + r * 0.4 * math.sin(math.radians(a))) for a in (-90, 30, 150, -90)]
    c.stroke(tri, lightc, 1.3, 0.9)
    tri2 = [(x + r * 0.45 * math.cos(math.radians(a)), y + r * 0.4 * math.sin(math.radians(a))) for a in (90, 210, 330, 90)]
    c.stroke(tri2, lightc, 1.3, 0.9)
    core = c.ell(x, y, r * 0.17, r * 0.15)
    c.glow(core, lightc, r * 0.25, 0.5)
    gem(c, x, y, r * 0.17, glowc, ol=1.4)


def enchanting(c, rot, w, h):
    m = 5
    front = 13
    body_table(c, m, m + 2, w - m, h - m, front=front, frame=7, horizontal=(rot != 'east'))
    k = Ctx(c, rot, (m + 22, m + 22, w - m - 22, h - m - front - 18), z=1.75)
    s = min(w, h)
    x, y = k.at(0.46, 0.5)
    rune_disc(c, x, y, s * 0.3)
    k.put(0.07, 0.16, ring_stand, 3, dy=4)
    k.put(0.09, 0.82, gem_box, 26, 17)
    k.put(0.87, 0.68, book_open, 34, 24)
    k.put(0.78, 0.06, candle, 13)
    k.put(0.97, 0.06, candle, 9, z=1.5)
    k.put(0.97, 0.3, potion, '#5f86c4', 5, dy=3, z=1.5)
    k.put(0.26, 0.92, scroll, 24, 9, seal='#6a4fa3', z=1.5)
    k.put(0.25, 0.08, potion, '#b54a4a', 4.5, z=1.4)


def scribe(c, rot, w, h):
    m = 5
    front = 12
    body_table(c, m, m + 2, w - m, h - m, front=front, frame=6, horizontal=(rot != 'east'))
    k = Ctx(c, rot, (m + 16, m + 14, w - m - 16, h - m - front - 12), z=1.7)
    x, y = k.at(0.5, 0.52)
    sw, sh = (70, 38) if k.wide else (40, 72)
    pg = c.rect(x - sw / 2, y - sh / 2, x + sw / 2, y + sh / 2, 1)
    c.paint(pg, PAPER, ol=2.0, drop=0.4, shade=0.12, hl=0.05)
    if k.wide:
        for s_ in (-1, 1):
            c.paint(c.rect(x + s_ * sw / 2 - 4, y - sh / 2 - 3, x + s_ * sw / 2 + 4, y + sh / 2 + 3, 4), PAPER_D, ol=1.8, shade=0.3, hl=0.2)
        for i in range(6):
            yy = y - sh / 2 + 6 + i * (sh - 11) / 5
            c.stroke([(x - sw / 2 + 9, yy), (x + sw / 2 - 10 - (i % 3) * 6, yy)], '#6e5c47', 1.1, 0.8)
    else:
        for s_ in (-1, 1):
            c.paint(c.rect(x - sw / 2 - 3, y + s_ * sh / 2 - 4, x + sw / 2 + 3, y + s_ * sh / 2 + 4, 4), PAPER_D, ol=1.8, shade=0.3, hl=0.2)
        for i in range(9):
            yy = y - sh / 2 + 8 + i * (sh - 15) / 8
            c.stroke([(x - sw / 2 + 6, yy), (x + sw / 2 - 6 - (i % 3) * 5, yy)], '#6e5c47', 1.1, 0.8)
    k.put(0.07, 0.4, book_stack, 3, 22, dy=6)
    k.put(0.9, 0.62, inkwell, dx=-4, dy=4)
    k.put(0.93, 0.08, candle, 11, z=1.3)
    k.put(0.12, 0.92, scroll, 18, 7, seal=BLOOD, z=1.3)

def anvil(c, x, y, s=1.0, metal='#6d7177', rune=None, base=None):
    if base is not None:
        b = c.rect(x - 15 * s, y + 3 * s, x + 15 * s, y + 22 * s, 3 * s)
        c.paint(b, base, ol=2.4, drop=0.35, shade=0.3, hl=0.12, stuff=True)
        c.darken(c.rect(x - 15 * s, y + 3 * s, x + 15 * s, y + 7 * s), 0.8)
        for i in range(3):
            c.stroke([(x - 12 * s, y + (9 + i * 4.5) * s), (x + 12 * s, y + (9 + i * 4.5) * s)], mul(base, 0.6), 1.0, 0.5)
    waist = c.poly([(x - 9 * s, y - 2 * s), (x + 9 * s, y - 2 * s), (x + 13 * s, y + 8 * s), (x - 13 * s, y + 8 * s)])
    c.paint(waist, mul(metal, 0.85), ol=2.2, drop=0.4, shade=0.3, hl=0.1)
    topm = c.poly([(x - 30 * s, y - 13 * s), (x - 18 * s, y - 15 * s), (x + 20 * s, y - 15 * s), (x + 22 * s, y - 13 * s),
                   (x + 22 * s, y - 3 * s), (x + 14 * s, y), (x - 14 * s, y), (x - 20 * s, y - 6 * s)])
    c.paint(topm, metal, ol=2.4, drop=0.4, shade=0.3, hl=0.25, sd=4 * s)
    face = c.poly([(x - 18 * s, y - 15 * s), (x + 20 * s, y - 15 * s), (x + 22 * s, y - 13 * s), (x - 24 * s, y - 12 * s)])
    c.fill(face, mul(metal, 1.25), 0.8)
    if rune:
        for i, gx in enumerate((-9, -1, 7)):
            g = c.line_mask([(x + gx * s, y - 9 * s), (x + (gx + 3) * s, y - 5 * s), (x + (gx + 6) * s, y - 9 * s)], 1.3 * s)
            c.glow(g, rune, 2.5 * s, 0.6)
            c.fill(g, mix(rune, '#ffffff', 0.5), 1.0)


def rune_anvil(c, rot, w, h):
    m = 5
    front = 14
    face = c.rect(m, h - m - front - 3, w - m, h - m, 3)
    c.paint(face, mul(STUFF, 0.68), stuff=True, tex=c.blocks(34, 7, 0.8), shade=0.15, hl=0, grad=0)
    top = c.rect(m, m + 2, w - m, h - m - front, 4)
    c.paint(top, STUFF, stuff=True, tex=c.blocks(44, 30, 0.8, 0.05), shade=0.15, hl=0.1, sd=6)
    c.darken(top & ~c.rect(m + 8, m + 10, w - m - 8, h - m - front - 8, 3), 0.85)
    k = Ctx(c, rot, (m + 22, m + 20, w - m - 22, h - m - front - 18), z=2.2)
    x, y = k.at(0.13, 0.42)
    hw, hh = (34, 46) if k.wide else (46, 34)
    c.paint(c.rect(x - hw - 7, y - hh - 7, x + hw + 7, y + hh + 7, 6), '#8d8780', ol=2.6, drop=0.4, tex=c.blocks(12, 8, 0.7), shade=0.25)
    coals(c, x - hw, y - hh, x + hw, y + hh, EMBER, 30)
    # bellows
    k.put(0.13, 0.94, lambda c, x, y: (c.paint(c.poly([(x - 14, y - 5), (x + 8, y - 8), (x + 16, y), (x + 8, y + 8), (x - 14, y + 5)]), '#6e4a36', ol=1.8, drop=0.4, shade=0.3, hl=0.15),
                                       c.paint(c.rect(x + 14, y - 2, x + 24, y + 2, 1), DARKIRON, ol=1.2, shade=0.2, hl=0.2)), z=1.5)
    k.put(0.55, 0.44, anvil, 1.0, rune=FROST, base='#cfcac2', z=2.5 if k.wide else 2.0)
    k.put(0.9, 0.2, bowl, 9, '#6f5a44', '#4c6f86')
    k.put(0.88, 0.72, ingots, 3, '#a7c2cf')
    k.put(0.62, 0.95, hammer, -12, 22, dx=-14)
    k.put(0.36, 0.1, tongs, 5, 24, dx=-10, z=1.6)

def giant_anvil(c, rot, w, h):
    m = 6
    front = 16
    face = c.rect(m, h - m - front - 3, w - m, h - m, 4)
    c.paint(face, mul(STUFF, 0.68), stuff=True, tex=c.blocks(40, 8, 0.8), shade=0.15, hl=0, grad=0)
    top = c.rect(m, m + 2, w - m, h - m - front, 5)
    c.paint(top, STUFF, stuff=True, tex=c.blocks(56, 38, 0.8, 0.05), shade=0.15, hl=0.1, sd=7)
    c.darken(top & ~c.rect(m + 10, m + 12, w - m - 10, h - m - front - 10, 3), 0.85)
    k = Ctx(c, rot, (m + 34, m + 30, w - m - 34, h - m - front - 26), z=2.8)

    def stump(c, x, y):
        c.paint(c.ell(x, y + 10, 30, 20), '#7a5c40', ol=3, drop=0.45, shade=0.3, hl=0.1)
        c.paint(c.ell(x, y + 5, 28, 15), '#a17d55', ol=2, shade=0.1, hl=0.05)
        for rr in (0.75, 0.5, 0.25):
            c.stroke([(x + 28 * rr * math.cos(t / 24 * 6.283), y + 5 + 15 * rr * math.sin(t / 24 * 6.283)) for t in range(25)], '#7d5f40', 1.1, 0.8)
        anvil(c, x, y + 2, 1.0, metal='#5f6369')
    k.put(0.46, 0.46, stump, z=3.3 if k.wide else 2.7)
    k.put(0.9, 0.16, ingots, 4, '#9aa3a8', 20, 8, dy=6, z=2.3)

    def barrel(c, x, y):
        c.paint(c.ell(x, y, 20, 18), '#6e5440', ol=2.6, drop=0.4, shade=0.3, hl=0.15)
        c.paint(c.ring(x, y, 16, 20, 14.5, 18), '#4a4c50', ol=1.0, shade=0.2, hl=0.2)
        c.paint(c.ell(x, y - 1, 15, 13), '#3f6276', ol=1.2, shade=0.1, hl=0.0)
        c.fill(c.ell(x - 4, y - 5, 5, 2.5), '#9cc0d2', 0.6)
    k.put(0.88, 0.8, barrel, z=2.3)
    k.put(0.1, 0.22, hammer, -50, 30, dx=-10, dy=14)
    k.put(0.1, 0.84, tongs, 10, 30, dx=-24)
    x, y = k.at(0.68, 0.95)
    chain(c, [(x - 60, y), (x - 16, y + 8), (x + 30, y - 4)], '#55585c', 13)

def arcane_loom(c, rot, w, h):
    m = 5
    if k_wide := rot in ('south', 'north'):
        # side beams
        for bx in (m + 6, w - m - 6):
            post = c.rect(bx - 7, m + 4, bx + 7, h - m - 4, 3)
            c.paint(post, STUFF, stuff=True, tex=c.planks(False, 12), shade=0.25, hl=0.15)
        # back & front rollers
        for by, d in ((m + 18, 0.85), (h - m - 26, 1.0)):
            roll = c.rect(m + 4, by - 8, w - m - 4, by + 8, 7)
            c.paint(roll, mul(STUFF, d), stuff=True, tex=c.planks(True, 30, 0.03, 0.85), shade=0.3, hl=0.25)
        # warp threads + woven cloth
        x0, x1 = m + 22, w - m - 22
        cloth = c.rect(x0, m + 27, x1, h - m - 35, 1)
        c.paint(cloth, '#2f3f6a', ol=1.6, shade=0.15, hl=0.05)
        rng = c.rng
        for i in range(26):
            sx, sy = rng.uniform(x0 + 4, x1 - 4), rng.uniform(m + 30, h - m - 38)
            c.fill(c.ell(sx, sy, 0.9), '#e8e2b8', 0.9)
        c.fill(c.rect(x0, h - m - 49, x1, h - m - 46), GOLD, 0.9)
        for i in range(int((x1 - x0) / 6)):
            xx = x0 + 3 + i * 6
            c.stroke([(xx, h - m - 45), (xx, h - m - 34)], '#d9d0b8', 0.9, 0.9)
        # shuttle
        sx = w * 0.62
        c.paint(c.poly([(sx - 18, h - m - 40), (sx - 12, h - m - 44), (sx + 12, h - m - 44), (sx + 18, h - m - 40), (sx + 12, h - m - 36), (sx - 12, h - m - 36)]),
                '#8a6a48', ol=1.6, drop=0.35, shade=0.25, hl=0.2)
        c.paint(c.ell(sx, h - m - 40, 5, 2), GOLD, ol=0.8, shade=0.1, hl=0.2)
        # spools on front roller
        for i, col in enumerate(['#7d5fb0', GOLD, '#b85a4a', SILVER]):
            px = (m + 40 + i * 22) if rot == 'south' else (w - m - 40 - i * 22)
            c.paint(c.rect(px - 6, h - m - 30, px + 6, h - m - 16, 2), col, ol=1.4, drop=0.35, shade=0.3, hl=0.25)
            c.stroke([(px - 6, h - m - 23), (px + 6, h - m - 23)], mul(col, 0.7), 1.0)
    else:
        for by in (m + 6, h - m - 6):
            post = c.rect(m + 4, by - 7, w - m - 4, by + 7, 3)
            c.paint(post, STUFF, stuff=True, tex=c.planks(True, 12), shade=0.25, hl=0.15)
        for bx, d in ((m + 22, 0.9), (w - m - 22, 1.0)):
            roll = c.rect(bx - 8, m + 4, bx + 8, h - m - 4, 7)
            c.paint(roll, mul(STUFF, d), stuff=True, tex=c.planks(False, 30, 0.03, 0.85), shade=0.3, hl=0.25)
        y0, y1 = m + 22, h - m - 22
        cloth = c.rect(m + 32, y0, w - m - 46, y1, 1)
        c.paint(cloth, '#2f3f6a', ol=1.6, shade=0.15, hl=0.05)
        rng = c.rng
        for i in range(26):
            sx, sy = rng.uniform(m + 35, w - m - 49), rng.uniform(y0 + 4, y1 - 4)
            c.fill(c.ell(sx, sy, 0.9), '#e8e2b8', 0.9)
        for i in range(int((y1 - y0) / 6)):
            yy = y0 + 3 + i * 6
            c.stroke([(w - m - 46, yy), (w - m - 32, yy)], '#d9d0b8', 0.9, 0.9)
        c.paint(c.poly([(w - m - 40, y0 + 40), (w - m - 44, y0 + 46), (w - m - 44, y0 + 70), (w - m - 40, y0 + 76), (w - m - 36, y0 + 70), (w - m - 36, y0 + 46)]),
                '#8a6a48', ol=1.6, drop=0.35, shade=0.25, hl=0.2)
        for i, col in enumerate(['#7d5fb0', GOLD, '#b85a4a', SILVER]):
            py = h - m - 40 - i * 22
            c.paint(c.rect(w - m - 29, py - 6, w - m - 15, py + 6, 2), col, ol=1.4, drop=0.35, shade=0.3, hl=0.25)


def alchemy(c, rot, w, h):
    m = 5
    front = 12
    body_table(c, m, m + 2, w - m, h - m, front=front, frame=6, horizontal=(rot != 'east'))
    k = Ctx(c, rot, (m + 16, m + 14, w - m - 16, h - m - front - 12), z=1.7)

    def still(c, x, y):
        c.paint(c.rect(x - 11, y + 2, x + 11, y + 14, 2), '#4d4e52', ol=2, drop=0.4, shade=0.25, hl=0.15)
        c.paint(c.rect(x - 8, y + 4, x + 8, y + 9, 1), '#2b2726', ol=1, shade=0, hl=0)
        c.glow(c.rect(x - 7, y + 5, x + 7, y + 8), EMBER, 3, 0.5)
        c.paint(c.ell(x, y - 4, 10, 9), '#b77a4c', ol=2, drop=0.3, shade=0.3, hl=0.3)
        c.paint(c.rect(x - 2.5, y - 20, x + 2.5, y - 10, 1), '#a86e44', ol=1.6, shade=0.25, hl=0.2)
        tube = c.line_mask([(x, y - 19), (x + 10, y - 23), (x + 22, y - 14), (x + 25, y - 6)], 2.4)
        c.paint(tube, '#c7d3d4', ol=1.2, shade=0.2, hl=0.2, grain=0)
        potion(c, x + 27, y + 2, '#7aa86a', 6)
    k.put(0.16, 0.5, still, z=1.25)
    k.put(0.5, 0.18, potion, '#b54a4a', 5, dy=3)
    k.put(0.62, 0.3, potion, '#8a5fb8', 4.5, dy=3, z=1.3)
    k.put(0.5, 0.72, potion, '#c9a547', 5, z=1.3)
    k.put(0.7, 0.75, flask_conical, '#5fa9a3', 1.0)
    k.put(0.9, 0.28, mortar, 7, dy=3)
    k.put(0.9, 0.82, herbs)
    k.put(0.33, 0.9, book_closed, 18, 12, '#4c5f3b', z=1.2)

def faith_altar(c, rot, w, h):
    m = 5
    front = 14
    step = c.rect(m, m + 6, w - m, h - m, 4)
    c.paint(step, mul(STUFF, 0.9), stuff=True, tex=c.blocks(28, 14), shade=0.2, hl=0.1)
    face = c.rect(m + 8, h - m - front - 10, w - m - 8, h - m - 6, 3)
    c.paint(face, mul(STUFF, 0.72), stuff=True, tex=c.blocks(20, 10, 0.75), shade=0.15, hl=0, grad=0)
    top = c.rect(m + 8, m + 10, w - m - 8, h - m - front - 6, 3)
    c.paint(top, STUFF, stuff=True, shade=0.15, hl=0.12, sd=5)
    k = Ctx(c, rot, (m + 22, m + 20, w - m - 22, h - m - front - 14), z=1.55)
    if k.wide:
        cl = c.rect(w / 2 - 24, m + 10, w / 2 + 24, h - m - 4, 1)
        c.paint(cl, '#7a2f35', ol=1.8, shade=0.2, hl=0.1)
        c.fill(c.rect(w / 2 - 24, h - m - 12, w / 2 + 24, h - m - 9), GOLD, 0.9)
        for i in range(6):
            c.stroke([(w / 2 - 20 + i * 8, h - m - 4), (w / 2 - 20 + i * 8, h - m - 1)], GOLD, 1.4)
    else:
        cl = c.rect(m + 8, h / 2 - 24, w - m - 8, h / 2 + 24, 1)
        c.paint(cl, '#7a2f35', ol=1.8, shade=0.2, hl=0.1)
        c.fill(c.rect(m + 8, h / 2 - 24, w - m - 8, h / 2 - 21), GOLD, 0.9)
        c.fill(c.rect(m + 8, h / 2 + 21, w - m - 8, h / 2 + 24), GOLD, 0.9)

    def sun(c, x, y):
        c.paint(c.rect(x - 2.5, y, x + 2.5, y + 16, 1), BRASS, ol=1.6, drop=0.35, shade=0.25, hl=0.2)
        c.paint(c.ell(x, y + 16, 8, 3.5), BRASS, ol=1.6, shade=0.25, hl=0.2)
        rays = []
        for i in range(16):
            a = math.radians(i * 22.5)
            r = 13 if i % 2 == 0 else 9
            rays.append((x + r * math.cos(a), y - 4 + r * math.sin(a)))
        c.glow(c.ell(x, y - 4, 10), '#ffe7a8', 6, 0.3)
        c.paint(c.poly(rays), GOLD, ol=1.8, drop=0.3, shade=0.25, hl=0.25)
        c.paint(c.ell(x, y - 4, 5), '#f1dc94', ol=1.4, shade=0.2, hl=0.3)
    k.put(0.5, 0.3, sun, z=1.4)
    for u, v, hh in ((0.06, 0.3, 14), (0.2, 0.12, 10), (0.94, 0.3, 14), (0.8, 0.12, 10)):
        k.put(u, v, candle, hh, dy=4)
    k.put(0.18, 0.84, bowl, 8, BRASS, '#c9a547')

    def incense(c, x, y):
        bowl(c, x, y, 6, '#8d8780', '#9a9a9a')
        c.stroke([(x, y - 2), (x + 1, y - 9)], '#5a4a3a', 1.0)
        c.fill(c.ell(x + 1, y - 13, 3, 2), '#e4e4e4', 0.35)
        c.fill(c.ell(x - 1, y - 18, 4, 2.4), '#e4e4e4', 0.25)
    k.put(0.82, 0.84, incense)

def pact_lectern(c, rot, w, h):
    m = 5
    front = 12
    body_table(c, m, m + 2, w - m, h - m, front=front, frame=6, col=STUFF, horizontal=(rot != 'east'))
    k = Ctx(c, rot, (m + 16, m + 14, w - m - 16, h - m - front - 12), z=1.65)
    x, y = k.at(0.5, 0.5)
    bw, bh = (74, 46) if k.wide else (46, 74)
    c.paint(c.rect(x - bw / 2, y - bh / 2, x + bw / 2, y + bh / 2, 3), '#3c2a2a', ol=2.4, drop=0.45, shade=0.3, hl=0.15)
    c.darken(c.rect(x - bw / 2, y + bh / 2 - 5, x + bw / 2, y + bh / 2), 0.7)
    pw, ph = bw - 16, bh - 10
    pg = c.rect(x - pw / 2, y - ph / 2 - 3, x + pw / 2, y + ph / 2 + 12, 1)
    c.paint(pg, '#d9c79c', ol=1.8, shade=0.15, hl=0.05)
    for i in range(7):
        yy = y - ph / 2 + 1 + i * (ph + 4) / 7
        c.stroke([(x - pw / 2 + 5, yy), (x + pw / 2 - 5 - (i % 3) * 5, yy)], '#5a2a24', 1.0, 0.85)
    c.stroke([(x - 10, y + ph / 2 + 6), (x - 3, y + ph / 2 + 2), (x + 5, y + ph / 2 + 7), (x + 13, y + ph / 2 + 3)], '#8e2f2a', 1.4)
    c.paint(c.ell(x + pw / 2 - 7, y + ph / 2 + 6, 4.5), BLOOD, ol=1.3, shade=0.2, hl=0.25)
    for u in (0.07, 0.93):
        x1, y1 = k.at(u, 0.15)
        x2, y2 = k.at(u, 0.9)
        chain(c, [(x1, y1), ((x1 + x2) / 2 + 5, (y1 + y2) / 2), (x2, y2)], '#4c4e52', 6)
    for u, v in ((0.16, 0.08), (0.84, 0.08)):
        k.put(u, v, candle, 12, 6, '#3a3336', True, '#5a4a3a', dy=4)
    k.put(0.84, 0.86, inkwell)
    k.put(0.16, 0.86, skull, 0.85)

def infernal_forge(c, rot, w, h):
    m = 5
    front = 16
    face = c.rect(m, h - m - front - 4, w - m, h - m, 4)
    c.paint(face, '#3a3433', shade=0.15, hl=0, grad=0, tex=c.blocks(34, 8, 0.7))
    top = c.rect(m, m + 2, w - m, h - m - front, 5)
    c.paint(top, '#57504d', tex=c.blocks(44, 30, 0.75, 0.06), shade=0.18, hl=0.1, sd=6)
    c.darken(top & ~c.rect(m + 8, m + 10, w - m - 8, h - m - front - 8, 3), 0.8)
    k = Ctx(c, rot, (m + 22, m + 20, w - m - 22, h - m - front - 18), z=1.8)
    x, y = k.at(0.34, 0.47)
    hw, hh = (68, 50) if k.wide else (50, 68)
    c.paint(c.rect(x - hw, y - hh, x + hw, y + hh, 6), '#2e2a2a', ol=2.8, drop=0.45, tex=c.blocks(14, 9, 0.7), shade=0.25, hl=0.1)
    coals(c, x - hw + 10, y - hh + 10, x + hw - 10, y + hh - 10, '#62c24e', 46)
    for dx, dy, s_ in ((-26, 6, 1.6), (4, -4, 2.0), (30, 8, 1.4), (-4, 20, 1.2)):
        flame(c, x + dx * (1 if k.wide else 0.6), y + dy * (1 if k.wide else 1.4), s_, '#6fd05a', '#e4ffb8')
    for sx in (-1, 1):
        hx, hy = x + sx * (hw + 2), y - hh + 4
        c.paint(c.poly([(hx, hy + 8), (hx + sx * 14, hy - 18), (hx + sx * 5, hy + 14)]), '#cbbfa8', ol=2, shade=0.3, hl=0.2)
    hx, hy = x, y - hh
    k.c.paint(c.rect(hx - 16, hy - 9, hx + 16, hy + 6, 3), '#3b3433', ol=2, shade=0.2, hl=0.15)
    with c.zoom(hx, hy - 2, 1.5):
        skull(c, hx, hy - 2, 1.0)

    def coins(c, x, y):
        c.paint(c.rect(x - 18, y - 12, x + 18, y + 12, 3), '#3b2a28', ol=2, drop=0.4, shade=0.25, hl=0.15)
        for i in range(6):
            cx, cy = x - 10 + (i % 3) * 10, y - 5 + (i // 3) * 10
            c.paint(c.ell(cx, cy, 4.2, 3.4), '#c9a547', ol=1.2, shade=0.25, hl=0.25)
            c.paint(c.ell(cx, cy, 1.6, 1.3), BLOOD, ol=0, shade=0, hl=0)
    k.put(0.86, 0.22, coins, z=1.6)
    k.put(0.85, 0.78, anvil, 1.0, metal='#4c4547', rune=HELL, z=1.5)
    k.put(0.62, 0.95, tongs, 0, 24, dx=-12, z=1.5)

def quench_trough(c, rot, w, h):
    m = 6
    front = 12
    face = c.rect(m, h - m - front - 4, w - m, h - m, 4)
    c.paint(face, mul(STUFF, 0.7), stuff=True, tex=c.blocks(18, 9, 0.7), shade=0.15, hl=0, grad=0)
    top = c.rect(m, m + 4, w - m, h - m - front, 5)
    c.paint(top, STUFF, stuff=True, tex=c.blocks(22, 14, 0.72), shade=0.18, hl=0.1)
    water = c.rect(m + 12, m + 14, w - m - 12, h - m - front - 10, 4)
    c.paint(water, '#2f5f7a', ol=2.2, shade=0.0, hl=0.0)
    c.darken(water & ~c.shift(water, 0, 4), 0.7)
    rng = c.rng
    ys, xs = (m + 18, h - m - front - 14), (m + 16, w - m - 16)
    for i in range(7):
        x, y = rng.uniform(*xs), rng.uniform(*ys)
        c.stroke([(x - 6, y), (x + 6, y)], '#8fc3dc', 1.0, 0.7)
    # glowing runes on the rim
    k = 9
    for i in range(k):
        if w > h:
            x, y = m + 18 + i * (w - 2 * m - 36) / (k - 1), m + 9
        else:
            x, y = m + 8, m + 20 + i * (h - 2 * m - front - 40) / (k - 1)
        g = c.line_mask([(x - 2, y - 2), (x, y + 2), (x + 2, y - 2)], 1.2)
        c.glow(g, FROST, 2, 0.5)
        c.fill(g, FROST_L, 1)
    # steam
    for i in range(3):
        x = w * (0.3 + i * 0.2) if w > h else w * 0.5
        y = h * 0.45 if w > h else h * (0.25 + i * 0.2)
        c.fill(c.ell(x, y, 7, 4), '#e8f0f4', 0.25)
        c.fill(c.ell(x + 4, y - 4, 5, 3), '#e8f0f4', 0.2)
    # tongs resting on rim
    tongs(c, w * 0.62 if w > h else w * 0.3, h * 0.25 if w > h else h * 0.7, 160 if w > h else 250, 30)


if __name__ == '__main__':
    out = sys.argv[1]
    which = sys.argv[2:] or ['enchanting']
    for nm in which:
        fn, wc, hc, name = {
            'enchanting': (enchanting, 3, 2, 'EnchantingTable'),
            'scribe': (scribe, 2, 1, 'ScribeDesk'),
            'rune': (rune_anvil, 3, 2, 'RuneAnvil'),
            'giant': (giant_anvil, 4, 3, 'GiantAnvil'),
            'loom': (arcane_loom, 3, 1, 'ArcaneLoom'),
            'alchemy': (alchemy, 2, 1, 'AlchemyTable'),
            'altar': (faith_altar, 2, 1, 'Altar'),
            'lectern': (pact_lectern, 2, 1, 'PactLectern'),
            'hellforge': (infernal_forge, 3, 2, 'InfernalForge'),
            'quench': (quench_trough, 2, 1, 'RuneQuenchTrough'),
        }[nm]
        render_multi(fn, wc, hc, out, name)
