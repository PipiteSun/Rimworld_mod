# Small props drawn upright in screen coordinates (x, y = centre unless noted).
import math
from rwart import *

WOOD = '#7a5a3e'
WOOD_D = '#5a4130'
STUFF = '#d8d4cc'      # neutral light tone, tinted by stuff colour in game
BRASS = '#b8925a'
GOLD = '#d2b265'
SILVER = '#c3c7cc'
IRON = '#6c6f73'
DARKIRON = '#45474b'
PAPER = '#e2d6b4'
PAPER_D = '#c9b98f'
LEATHER = '#6e3f32'
WAX = '#e7dfc8'
FLAME = '#f4b04a'
FLAME_C = '#fff0b0'
ARCANE = '#8a6fc4'
ARCANE_L = '#c7b3f0'
FROST = '#7fb2d6'
FROST_L = '#cfe8f4'
BLOOD = '#8e2f2a'
EMBER = '#e2672d'
HELL = '#d0452a'


def body_table(c, x0, y0, x1, y1, front=11, col=STUFF, stuff=True, planks=True, frame=5, horizontal=True,
               r=3):
    """Table top seen from above with a visible front face at the bottom."""
    face = c.rect(x0, y1 - front - r, x1, y1, r)
    c.paint(face, mul(col, 0.70), stuff=stuff, shade=0.18, hl=0.0, grad=0.0)
    top = c.rect(x0, y0, x1, y1 - front, r)
    tex = c.planks(horizontal=horizontal, spacing=22, line=0.72, grainamt=0.08) if planks else None
    c.paint(top, col, stuff=stuff, tex=tex, shade=0.16, hl=0.10, sd=6)
    if frame:
        inner = c.rect(x0 + frame, y0 + frame, x1 - frame, y1 - front - frame, 2)
        rim = top & ~inner
        c.darken(rim, 0.86)
        # bevel line
        edge = c.rect(x0 + frame, y0 + frame, x1 - frame, y1 - front - frame, 2) & ~c.rect(
            x0 + frame + 1.3, y0 + frame + 1.3, x1 - frame - 1.3, y1 - front - frame - 1.3, 2)
        c.darken(edge, 0.68)
    # nails/rivets on front face
    return top


def book_open(c, x, y, w=34, h=24, cover=LEATHER, tilt=0.0):
    hw, hh = w / 2, h / 2
    cv = c.poly([(x - hw - 2, y - hh + 1), (x, y - hh + 3), (x + hw + 2, y - hh + 1),
                 (x + hw + 2, y + hh + 2), (x, y + hh + 4), (x - hw - 2, y + hh + 2)])
    c.paint(cv, cover, ol=2.2, drop=0.35, shade=0.2, hl=0.0)
    for s in (-1, 1):
        pg = c.poly([(x, y - hh + 2), (x + s * hw, y - hh), (x + s * hw, y + hh), (x, y + hh + 2)])
        c.paint(pg, PAPER, ol=1.6, shade=0.18, hl=0.05, sd=3)
        for i in range(5):
            yy = y - hh + 5 + i * (h - 8) / 4.5
            c.stroke([(x + s * 4, yy + 1), (x + s * (hw - 4), yy)], '#8b7a5e', 1.0, 0.75)
    c.stroke([(x, y - hh + 2), (x, y + hh + 2)], mul(PAPER_D, 0.8), 1.2)


def book_closed(c, x, y, w=22, h=16, cover=LEATHER, pages=PAPER):
    pg = c.rect(x - w / 2 + 2, y - h / 2 + 2, x + w / 2 + 1, y + h / 2 + 3, 1.5)
    c.paint(pg, pages, ol=1.8, drop=0.3, shade=0.15, hl=0)
    cv = c.rect(x - w / 2, y - h / 2, x + w / 2, y + h / 2, 2)
    c.paint(cv, cover, ol=1.8, shade=0.25, hl=0.15)
    c.stroke([(x - w / 2 + 3, y - h / 2 + 1), (x - w / 2 + 3, y + h / 2 - 1)], mul(cover, 0.7), 1.5)
    c.paint(c.rect(x - 2, y - 3, x + 4, y + 3, 1), GOLD, ol=1.0, shade=0.2, hl=0.2)


def book_stack(c, x, y, n=3, w=24, cols=(LEATHER, '#3f5a6b', '#5c6a3e', '#6b3d55')):
    for i in range(n):
        book_closed(c, x + (i % 2) * 2 - 1, y - i * 5, w - i * 2, 9, cols[i % len(cols)])


def candle(c, x, y, h=12, w=6, col=WAX, lit=True, holder=BRASS):
    if holder:
        c.paint(c.ell(x, y + h / 2 + 1, w * 0.95, w * 0.5), holder, ol=1.6, drop=0.35, shade=0.25)
    body = c.rect(x - w / 2, y - h / 2, x + w / 2, y + h / 2, 1.5)
    c.paint(body, col, ol=1.6, shade=0.22, hl=0.18, sd=2)
    c.paint(c.ell(x, y - h / 2, w / 2, w * 0.22), mul(col, 0.92), ol=0, shade=0, hl=0)
    # wax drip
    c.paint(c.rect(x - w / 2 + 0.3, y - h / 2, x - w / 2 + 1.8, y - h / 2 + 4, 0.8), mul(col, 1.03), ol=0, shade=0, hl=0)
    if lit:
        f = c.poly([(x, y - h / 2 - 9), (x + 2.6, y - h / 2 - 3), (x, y - h / 2 + 0.3), (x - 2.6, y - h / 2 - 3)])
        c.glow(f, FLAME, 4, 0.35)
        c.paint(f, FLAME, ol=1.2, shade=0, hl=0, grain=0)
        c.paint(c.ell(x, y - h / 2 - 3.2, 1.0, 1.8), FLAME_C, ol=0, shade=0, hl=0, grain=0)


def gem(c, x, y, r=4, col='#7fb36b', ol=1.4):
    g = c.poly([(x, y - r), (x + r * 0.8, y), (x, y + r), (x - r * 0.8, y)])
    c.paint(g, col, ol=ol, shade=0.3, hl=0.0, grain=0)
    c.fill(c.poly([(x, y - r + 1), (x - r * 0.5, y), (x, y)]), mul(col, 1.35), 0.8)


def gem_box(c, x, y, w=26, h=18, wood=WOOD, gems=('#b54a4a', '#4f78b5', '#5f9e5a', '#c9a547', '#8a5fb8', '#5fa9a3')):
    lid = c.rect(x - w / 2, y - h / 2 - 7, x + w / 2, y - h / 2 + 1, 2)
    c.paint(lid, mul(wood, 0.85), ol=1.8, drop=0.3, shade=0.2)
    c.paint(c.rect(x - w / 2 + 3, y - h / 2 - 5, x + w / 2 - 3, y - h / 2 - 1, 1), '#6d3b47', ol=1.0, shade=0.1, hl=0)
    box = c.rect(x - w / 2, y - h / 2, x + w / 2, y + h / 2, 2)
    c.paint(box, wood, ol=1.8, drop=0.35, shade=0.22)
    inner = c.rect(x - w / 2 + 3, y - h / 2 + 3, x + w / 2 - 3, y + h / 2 - 3, 1)
    c.paint(inner, '#5c2e38', ol=0.9, shade=0.0, hl=0.0)
    n = len(gems)
    cols = 3
    for i, gc in enumerate(gems):
        gx = x - w / 2 + 3 + (w - 6) * ((i % cols) + 0.5) / cols
        gy = y - h / 2 + 3 + (h - 6) * ((i // cols) + 0.5) / max(1, (n + cols - 1) // cols)
        gem(c, gx, gy, 2.8, gc, ol=1.0)
    c.paint(c.rect(x - 2, y + h / 2 - 2, x + 2, y + h / 2 + 2, 0.5), GOLD, ol=1.0, shade=0, hl=0.2)


def ring_stand(c, x, y, n=3):
    base = c.rect(x - 4 - n * 5, y + 2, x + 4 + n * 5, y + 8, 2)
    c.paint(base, WOOD_D, ol=1.8, drop=0.35, shade=0.2)
    for i in range(n):
        px = x - (n - 1) * 5 + i * 10
        post = c.rect(px - 1.6, y - 12, px + 1.6, y + 4, 1)
        c.paint(post, '#5a4a3a', ol=1.3, shade=0.25, hl=0.1)
        rc = [GOLD, SILVER, GOLD, '#c27f5c'][i % 4]
        c.paint(c.ring(px, y - 3 - (i % 2) * 4, 2.3, 4.3, 1.2, 2.4), rc, ol=1.2, shade=0.2, hl=0.25, grain=0)
        gem(c, px, y - 7 - (i % 2) * 4, 1.8, ['#b54a4a', '#4f78b5', '#5f9e5a'][i % 3], ol=0.8)


def scroll(c, x, y, w=30, h=12, col=PAPER, seal=BLOOD, angle=0):
    sh = c.rect(x - w / 2, y - h / 2, x + w / 2, y + h / 2, 1)
    c.paint(sh, col, ol=1.6, drop=0.3, shade=0.15, hl=0.05)
    for s in (-1, 1):
        c.paint(c.rect(x + s * w / 2 - 2.5, y - h / 2 - 1.5, x + s * w / 2 + 2.5, y + h / 2 + 1.5, 2.4), PAPER_D, ol=1.4, shade=0.3, hl=0.2)
    for i in range(3):
        yy = y - h / 2 + 3 + i * (h - 5) / 2.5
        c.stroke([(x - w / 2 + 5, yy), (x + w / 2 - 6 - (i == 2) * 6, yy)], '#7d6a52', 0.9, 0.8)
    if seal:
        c.paint(c.ell(x + w / 2 - 7, y + h / 2 - 1, 2.6), seal, ol=1.0, shade=0.2, hl=0.2, grain=0)


def inkwell(c, x, y, quill=True):
    c.paint(c.ell(x, y + 2, 5, 3.5), '#2e3238', ol=1.6, drop=0.4, shade=0.2)
    c.paint(c.ell(x, y - 1, 5, 3), '#3a4048', ol=1.4, shade=0.2, hl=0.25)
    c.paint(c.ell(x, y - 1.5, 2.6, 1.4), '#141618', ol=0, shade=0, hl=0, grain=0)
    if quill:
        q = c.poly([(x + 1, y - 2), (x + 16, y - 20), (x + 19, y - 21), (x + 18, y - 17), (x + 3, y - 1)])
        c.paint(q, '#ece6d6', ol=1.3, shade=0.25, hl=0.0, grain=0)
        c.stroke([(x + 1.5, y - 1.5), (x + 18, y - 19.5)], '#b8ab90', 0.8)


def potion(c, x, y, liquid='#b54a4a', r=5.5, neck=4, cork='#8a6a48', glass='#cfd9da', round_=True):
    if round_:
        b = c.ell(x, y, r, r)
    else:
        b = c.rect(x - r * 0.8, y - r, x + r * 0.8, y + r, 2)
    nk = c.rect(x - 1.8, y - r - neck, x + 1.8, y - r + 1.5, 0.8)
    c.paint(nk, glass, ol=1.3, drop=0.3, shade=0.15, hl=0.1, grain=0)
    c.paint(b, glass, ol=1.5, drop=0.35, shade=0.12, hl=0.0, grain=0)
    liq = b & c.rect(x - r, y - r * 0.15, x + r, y + r)
    c.paint(liq, liquid, ol=0, shade=0.3, hl=0.15, grain=0)
    c.paint(c.rect(x - 2.3, y - r - neck - 2.5, x + 2.3, y - r - neck + 0.5, 0.8), cork, ol=1.2, shade=0.2, hl=0.1)
    c.fill(c.ell(x - r * 0.4, y - r * 0.4, r * 0.22, r * 0.32), '#ffffff', 0.7)


def flask_conical(c, x, y, liquid='#6aa35b', s=1.0):
    b = c.poly([(x - 2 * s, y - 9 * s), (x + 2 * s, y - 9 * s), (x + 2 * s, y - 4 * s), (x + 7 * s, y + 5 * s),
                (x - 7 * s, y + 5 * s), (x - 2 * s, y - 4 * s)])
    c.paint(b, '#cfd9da', ol=1.5, drop=0.35, shade=0.12, hl=0, grain=0)
    liq = b & c.rect(x - 8 * s, y - 0.5 * s, x + 8 * s, y + 6 * s)
    c.paint(liq, liquid, ol=0, shade=0.3, hl=0.15, grain=0)
    c.fill(c.rect(x - 4 * s, y - 2 * s, x - 2.8 * s, y + 3 * s), '#ffffff', 0.6)


def mortar(c, x, y, r=7):
    c.paint(c.ell(x, y + 1, r, r * 0.75), '#9c958a', ol=1.6, drop=0.35, shade=0.3, hl=0.15)
    c.paint(c.ell(x, y - 1, r * 0.7, r * 0.42), '#5f5a52', ol=1.0, shade=0, hl=0)
    c.paint(c.ell(x, y - 0.5, r * 0.45, r * 0.25), '#7f9a5a', ol=0, shade=0.1, hl=0)
    p = c.poly([(x - 1, y - 1), (x + 9, y - 10), (x + 11, y - 8), (x + 1, y + 1)])
    c.paint(p, '#b3ab9e', ol=1.3, shade=0.25, hl=0.1)


def herbs(c, x, y):
    for i, col in enumerate(['#6d8a4a', '#7f9b52', '#5b7a42']):
        c.stroke([(x - 6 + i * 2, y + 4), (x - 2 + i * 4, y - 7)], mul(col, 0.8), 2.2)
        c.paint(c.ell(x - 2 + i * 4, y - 8, 2.4, 3), col, ol=1.0, shade=0.25, hl=0.1)
    c.paint(c.rect(x - 4, y - 0.5, x + 2, y + 2), '#a07c50', ol=1.0, shade=0.2, hl=0)


def bowl(c, x, y, r=7, col=BRASS, content=None):
    c.paint(c.ell(x, y + 1.5, r, r * 0.65), col, ol=1.6, drop=0.35, shade=0.3, hl=0.2)
    c.paint(c.ell(x, y - 0.2, r * 0.78, r * 0.45), mul(col, 0.62), ol=1.0, shade=0, hl=0)
    if content:
        c.paint(c.ell(x, y + 0.3, r * 0.6, r * 0.32), content, ol=0, shade=0.15, hl=0.2)


def hammer(c, x, y, ang=-35, L=26, head=IRON, handle=WOOD, s=1.0):
    a = math.radians(ang)
    dx, dy = math.cos(a), math.sin(a)
    hx, hy = x + dx * L * s, y + dy * L * s
    c.paint(c.line_mask([(x, y), (hx, hy)], 3.2 * s), handle, ol=1.4, drop=0.35, shade=0.25, hl=0.1)
    px, py = -dy, dx
    w, h = 6 * s, 4.5 * s
    pts = [(hx + px * w + dx * h, hy + py * w + dy * h), (hx - px * w + dx * h, hy - py * w + dy * h),
           (hx - px * w - dx * h, hy - py * w - dy * h), (hx + px * w - dx * h, hy + py * w - dy * h)]
    c.paint(c.poly(pts), head, ol=1.6, drop=0.35, shade=0.3, hl=0.2)


def tongs(c, x, y, ang=20, L=28, s=1.0):
    a = math.radians(ang)
    dx, dy = math.cos(a), math.sin(a)
    px, py = -dy, dx
    for k in (-1, 1):
        pts = [(x + px * k * 2.2 * s, y + py * k * 2.2 * s), (x + dx * L * 0.55 * s, y + dy * L * 0.55 * s),
               (x + dx * L * s + px * k * 3 * s, y + dy * L * s + py * k * 3 * s)]
        c.paint(c.line_mask(pts, 2.0 * s), DARKIRON, ol=1.2, drop=0.3, shade=0.2, hl=0.15)


def ingots(c, x, y, n=3, col='#9fb6c4', w=14, h=6):
    for i in range(n):
        ix, iy = x + (i % 2) * 6 - 3, y - i * 4
        b = c.poly([(ix - w / 2, iy + h / 2), (ix - w / 2 + 2, iy - h / 2), (ix + w / 2 - 2, iy - h / 2), (ix + w / 2, iy + h / 2)])
        c.paint(b, col, ol=1.4, drop=0.35, shade=0.3, hl=0.25)


def rune_marks(c, cx, cy, r, n, col, w=1.4, size=3.0, start=0, alpha=1.0):
    """Little angular rune glyphs placed around a circle."""
    shapes = [
        [(-1, -1), (0, 1), (1, -1)], [(-1, 1), (0, -1), (1, 1), (-1, 1)], [(0, -1), (0, 1), (1, 0)],
        [(-1, -1), (1, 1), (1, -1), (-1, 1)], [(-1, 0), (1, 0), (0, -1), (0, 1)], [(-1, -1), (-1, 1), (1, 0), (-1, -1)],
        [(0, -1), (1, 0), (0, 1), (-1, 0)], [(-1, 1), (-1, -1), (1, -1), (1, 1)],
    ]
    for i in range(n):
        a = math.radians(start + 360 * i / n)
        x, y = cx + r * math.cos(a), cy + r * math.sin(a)
        sh = shapes[i % len(shapes)]
        c.stroke([(x + px * size, y + py * size) for px, py in sh], col, w, alpha)


def flame(c, x, y, s=1.0, col=FLAME, core=FLAME_C, glow_col=None):
    f = c.poly([(x, y - 14 * s), (x + 5 * s, y - 6 * s), (x + 6 * s, y + 2 * s), (x, y + 5 * s), (x - 6 * s, y + 2 * s),
                (x - 5 * s, y - 6 * s), (x - 2 * s, y - 8 * s)])
    c.glow(f, glow_col or col, 7 * s, 0.35)
    c.paint(f, col, ol=1.4, shade=0.15, hl=0, grain=0)
    c.paint(c.poly([(x, y - 6 * s), (x + 3 * s, y), (x, y + 3.5 * s), (x - 3 * s, y)]), core, ol=0, shade=0, hl=0, grain=0)


def coals(c, x0, y0, x1, y1, hot=EMBER, n=14):
    pit = c.rect(x0, y0, x1, y1, 3)
    c.paint(pit, '#2a2523', ol=1.8, shade=0.0, hl=0.0)
    rng = c.rng
    for i in range(n):
        cx, cy = rng.uniform(x0 + 3, x1 - 3), rng.uniform(y0 + 3, y1 - 3)
        r = rng.uniform(2, 3.6)
        col = mix('#3a302b', hot, rng.uniform(0.3, 1.0))
        c.paint(c.ell(cx, cy, r, r * 0.8), col, ol=0.8, shade=0.25, hl=0.25, grain=0)
    c.glow(pit, hot, 5, 0.25, only_on=True)


def chain(c, pts, col=IRON, link=5, w=1.6):
    # simple chain: alternating ovals along a polyline
    segs = []
    for (ax, ay), (bx, by) in zip(pts, pts[1:]):
        L = math.hypot(bx - ax, by - ay)
        n = max(1, int(L / link))
        for i in range(n):
            t = (i + 0.5) / n
            segs.append((ax + (bx - ax) * t, ay + (by - ay) * t, math.atan2(by - ay, bx - ax), i % 2))
    for x, y, a, odd in segs:
        r1, r2 = link * 0.65, (link * 0.35 if not odd else link * 0.18)
        ca, sa = math.cos(a), math.sin(a)
        poly = [(x + r1 * math.cos(t) * ca - r2 * math.sin(t) * sa, y + r1 * math.cos(t) * sa + r2 * math.sin(t) * ca)
                for t in [i * math.pi / 8 for i in range(16)]]
        m = c.poly(poly)
        c.paint(m, col, ol=1.0, shade=0.25, hl=0.2, grain=0)


def skull(c, x, y, s=1.0, col='#ddd5c2'):
    c.paint(c.ell(x, y - 1 * s, 6 * s, 5.5 * s) | c.rect(x - 3.5 * s, y + 2 * s, x + 3.5 * s, y + 6 * s, 1.5 * s), col,
            ol=1.5, drop=0.35, shade=0.28, hl=0.1)
    for k in (-1, 1):
        c.paint(c.ell(x + k * 2.4 * s, y - 0.5 * s, 1.6 * s, 1.9 * s), '#2a2420', ol=0, shade=0, hl=0, grain=0)
    c.paint(c.poly([(x, y + 1.5 * s), (x + 1 * s, y + 3 * s), (x - 1 * s, y + 3 * s)]), '#2a2420', ol=0, shade=0, hl=0, grain=0)
