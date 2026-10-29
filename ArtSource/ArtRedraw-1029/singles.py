import sys, math
from bench import *
from benches import rune_disc, anvil

STONE = '#a8a299'


# ---------------- facilities (1x1) ----------------
def alembic(c):
    w = h = 128
    # brick burner
    c.paint(c.rect(30, 70, 98, 112, 4), '#8a7d70', ol=2.6, drop=0.4, tex=c.blocks(14, 8, 0.7), shade=0.25, hl=0.1)
    c.paint(c.rect(44, 92, 84, 106, 2), '#241f1d', ol=1.6, shade=0, hl=0)
    coals(c, 46, 94, 82, 104, EMBER, 8)
    # copper pot
    c.paint(c.ell(56, 62, 26, 22), '#b5774a', ol=2.6, drop=0.35, shade=0.3, hl=0.3)
    c.paint(c.ring(56, 66, 20, 24, 7, 9) & c.rect(20, 66, 92, 80), '#8e5a37', ol=0, shade=0.1, hl=0)
    for i in range(5):
        c.paint(c.ell(38 + i * 9, 74, 1.6), '#e0b080', ol=0.8, shade=0, hl=0)
    c.paint(c.poly([(48, 42), (64, 42), (60, 24), (52, 24)]), '#a86e44', ol=2.2, shade=0.25, hl=0.25)
    c.paint(c.ell(56, 22, 9, 6), '#b5774a', ol=2.2, shade=0.25, hl=0.3)
    tube = c.line_mask([(62, 20), (80, 16), (96, 26), (102, 44), (100, 60)], 3.4)
    c.paint(tube, '#c48a5a', ol=1.6, shade=0.25, hl=0.25, grain=0)
    # coil
    for i in range(3):
        c.paint(c.ring(100, 50 + i * 7, 4, 7, 2, 4), '#b5774a', ol=1.2, shade=0.2, hl=0.3, grain=0)
    with c.zoom(104, 82, 1.9):
        potion(c, 104, 82, '#7aa86a', 6)
    c.fill(c.ell(48, 54, 5, 8), '#f4d3a8', 0.5)


def focus_stone(c):
    with c.zoom(64, 116, 1.08):
        _focus_stone(c)


def _focus_stone(c):
    # carved plinth
    c.paint(c.rect(22, 84, 106, 116, 5), '#7e7a73', ol=2.6, drop=0.4, tex=c.blocks(20, 12, 0.75), shade=0.25)
    c.paint(c.poly([(32, 88), (96, 88), (88, 64), (40, 64)]), '#9a958c', ol=2.4, shade=0.25, hl=0.15)
    c.paint(c.rect(36, 60, 92, 70, 3), '#aaa59b', ol=2.2, shade=0.2, hl=0.2)
    for i, x in enumerate((44, 56, 68, 80)):
        g = c.line_mask([(x - 3, 77), (x, 82), (x + 3, 77)], 1.4)
        c.glow(g, ARCANE, 2.5, 0.6)
        c.fill(g, ARCANE_L, 1)
    # crystal cluster
    cr = [((64, 8), (78, 34), (72, 64), (56, 64), (50, 34), 1.0),
          ((44, 26), (54, 44), (52, 64), (40, 64), (36, 44), 0.85),
          ((86, 22), (94, 42), (90, 64), (76, 64), (76, 42), 0.8)]
    c.glow(c.ell(64, 40, 26, 30), ARCANE, 10, 0.25)
    for *pts, f in cr[1:] + cr[:1]:
        m = c.poly(pts)
        c.paint(m, mul('#8d6cc4', f), ol=2.2, shade=0.3, hl=0.2, grain=0.01)
        top, rgt = pts[0], pts[1]
        c.fill(c.poly([pts[0], pts[4], pts[3], ((pts[0][0] + pts[3][0]) / 2, pts[3][1])]), '#c9b6ef', 0.55)
    for x, y in ((24, 30), (104, 14), (102, 50)):
        with c.zoom(x, y, 1.0):
            gem(c, x, y, 4, '#b79be6', ol=1.4)


def mini_figure(c, cx, base_y, s, col, robe=True):
    # tiny robed figure for the shrine statue
    c.paint(c.poly([(cx - 13 * s, base_y), (cx - 7 * s, base_y - 34 * s), (cx + 7 * s, base_y - 34 * s), (cx + 13 * s, base_y)]), col, ol=2.2, shade=0.3, hl=0.15, stuff=True)
    for k in (-1, 1):
        c.stroke([(cx + k * 3 * s, base_y - 30 * s), (cx + k * 6 * s, base_y - 2 * s)], mul(col, 0.75), 1.2)
    c.paint(c.ell(cx, base_y - 40 * s, 7 * s, 7.5 * s), col, ol=2.2, shade=0.3, hl=0.2, stuff=True)
    c.paint(c.poly([(cx - 8 * s, base_y - 38 * s), (cx, base_y - 50 * s), (cx + 8 * s, base_y - 38 * s), (cx + 6 * s, base_y - 30 * s), (cx - 6 * s, base_y - 30 * s)]),
            mul(col, 0.92), ol=2.0, shade=0.3, hl=0.2, stuff=True)
    c.paint(c.ell(cx, base_y - 38 * s, 4.5 * s, 5 * s), mul(col, 0.55), ol=0, shade=0, hl=0, stuff=True)


def deity_statue(c):
    with c.zoom(64, 118, 1.08):
        _deity_statue(c)


def _deity_statue(c):
    c.paint(c.rect(26, 92, 102, 118, 4), STUFF, ol=2.6, drop=0.4, stuff=True, tex=c.blocks(18, 10, 0.75), shade=0.3, hl=0.15)
    c.paint(c.rect(34, 80, 94, 96, 3), STUFF, ol=2.4, stuff=True, shade=0.3, hl=0.2)
    mini_figure(c, 64, 84, 1.45, STUFF)
    # held glowing symbol
    c.glow(c.ell(64, 52, 8), '#ffe7a8', 7, 0.4)
    c.paint(c.ell(64, 52, 7), GOLD, ol=1.8, shade=0.25, hl=0.3)
    c.paint(c.ell(64, 52, 3), '#fff2c0', ol=0, shade=0, hl=0)
    for x in (30, 98):
        with c.zoom(x, 86, 1.3):
            candle(c, x, 86, 10)


# ---------------- god statues (2x2, CutoutComplex) ----------------
def pedestal(c, w=256):
    c.paint(c.rect(28, 206, 228, 246, 6), mul(STUFF, 0.92), ol=3, drop=0.4, stuff=True, tex=c.blocks(40, 20, 0.8), shade=0.3, hl=0.1)
    c.paint(c.rect(44, 188, 212, 214, 5), STUFF, ol=3, stuff=True, shade=0.3, hl=0.15, tex=c.blocks(56, 30, 0.85))
    c.stroke([(52, 201), (204, 201)], mul(STUFF, 0.7), 1.2, 0.6)


def robe_body(c, cx, top, bottom, wtop, wbot, col=STUFF, folds=5):
    m = c.poly([(cx - wbot, bottom), (cx - wtop, top), (cx + wtop, top), (cx + wbot, bottom)])
    c.paint(m, col, ol=3, stuff=True, shade=0.32, hl=0.15)
    for i in range(folds):
        t = (i + 1) / (folds + 1)
        xb = cx - wbot + 2 * wbot * t
        xt = cx - wtop + 2 * wtop * t
        c.stroke([(xt, top + 10), ((xt + xb) / 2 + 2, (top + bottom) / 2), (xb, bottom - 3)], mul(col, 0.72), 1.6, 0.8)
    c.paint(c.rect(cx - wbot - 2, bottom - 8, cx + wbot + 2, bottom, 3), mul(col, 0.95), ol=2.5, stuff=True, shade=0.3, hl=0.1)
    return m


def head(c, cx, cy, r=15, col=STUFF, hood=False, helm=False, face=True):
    c.paint(c.ell(cx, cy, r, r * 1.08), col, ol=3, stuff=True, shade=0.32, hl=0.2)
    if hood:
        c.paint(c.poly([(cx - r - 5, cy + r * 0.6), (cx - r, cy - r * 0.6), (cx, cy - r - 9), (cx + r, cy - r * 0.6), (cx + r + 5, cy + r * 0.6),
                        (cx + r - 4, cy + r + 6), (cx - r + 4, cy + r + 6)]), mul(col, 0.95), ol=3, stuff=True, shade=0.3, hl=0.2)
        c.paint(c.ell(cx, cy + 2, r * 0.68, r * 0.8), mul(col, 0.45), ol=0, shade=0.1, hl=0, stuff=True)
        return
    if helm:
        c.paint(c.poly([(cx - r - 2, cy + 4), (cx - r + 1, cy - r * 0.7), (cx, cy - r - 4), (cx + r - 1, cy - r * 0.7), (cx + r + 2, cy + 4), (cx + r - 2, cy + r + 2), (cx - r + 2, cy + r + 2)]),
                mul(col, 0.97), ol=3, stuff=True, shade=0.3, hl=0.25)
        c.paint(c.rect(cx - r + 4, cy - 2, cx + r - 4, cy + 3, 1), mul(col, 0.4), ol=0, shade=0, hl=0, stuff=True)
        return
    if face:
        for k in (-1, 1):
            c.stroke([(cx + k * 4 - 2, cy - 1), (cx + k * 4 + 2, cy - 1)], mul(col, 0.55), 1.4)
        c.stroke([(cx, cy), (cx - 1, cy + 5), (cx + 1, cy + 6)], mul(col, 0.65), 1.2)
        c.stroke([(cx - 3, cy + 9), (cx + 3, cy + 9)], mul(col, 0.6), 1.2)


def arm(c, pts, col=STUFF, w=9):
    c.paint(c.line_mask(pts, w), col, ol=2.6, stuff=True, shade=0.3, hl=0.15)
    x, y = pts[-1]
    c.paint(c.ell(x, y, w * 0.62), col, ol=2.4, stuff=True, shade=0.3, hl=0.2)


def accent(c, m, col, glowc=None):
    if glowc:
        c.glow(m, glowc, 6, 0.3)
    c.paint(m, col, ol=2.2, shade=0.25, hl=0.25)


def statue(c, god):
    pedestal(c)
    with c.zoom(128, 192, 1.14):
        _statue(c, god)


def _statue(c, god):
    cx = 128
    if god == 'Lathander':
        robe_body(c, cx, 92, 192, 26, 46)
        arm(c, [(cx - 22, 100), (cx - 30, 70), (cx - 18, 40)])
        arm(c, [(cx + 22, 100), (cx + 30, 70), (cx + 18, 40)])
        head(c, cx, 78)
        rays = [(cx + (30 if i % 2 == 0 else 20) * math.cos(math.radians(i * 22.5)), 30 + (30 if i % 2 == 0 else 20) * math.sin(math.radians(i * 22.5))) for i in range(16)]
        accent(c, c.poly(rays), GOLD, '#ffe7a8')
        c.paint(c.ell(cx, 30, 12), '#f1dc94', ol=2, shade=0.2, hl=0.3)
    elif god == 'Tyr':
        robe_body(c, cx, 96, 192, 30, 44, folds=4)
        c.paint(c.rect(cx - 30, 96, cx + 30, 140, 6), STUFF, ol=3, stuff=True, shade=0.3, hl=0.2)  # breastplate
        c.stroke([(cx, 100), (cx, 136)], mul(STUFF, 0.7), 1.6)
        for k in (-1, 1):
            c.paint(c.ell(cx + k * 32, 100, 12, 10), STUFF, ol=2.6, stuff=True, shade=0.3, hl=0.25)
        head(c, cx, 80, helm=False)
        c.paint(c.rect(cx - 16, 74, cx + 16, 82, 2), '#3c3a3a', ol=1.8, shade=0.2, hl=0.1)  # blindfold
        arm(c, [(cx + 30, 104), (cx + 52, 94), (cx + 60, 70)])
        sc = c.line_mask([(cx + 60, 70), (cx + 60, 40)], 3) | c.line_mask([(cx + 36, 46), (cx + 84, 46)], 3)
        accent(c, sc, GOLD)
        for sx in (cx + 38, cx + 82):
            c.stroke([(sx, 46), (sx - 6, 64)], GOLD, 1.2); c.stroke([(sx, 46), (sx + 6, 64)], GOLD, 1.2)
            accent(c, c.ell(sx, 66, 9, 4), GOLD)
        arm(c, [(cx - 30, 104), (cx - 44, 130), (cx - 40, 152)])
        with c.zoom(cx - 40, 172, 1.7):
            hammer(c, cx - 40, 172, -90, 26, head=SILVER, handle='#7a5a3e')
    elif god == 'Mystra':
        robe_body(c, cx, 94, 192, 24, 50, folds=6)
        arm(c, [(cx - 22, 104), (cx - 42, 120), (cx - 52, 108)])
        arm(c, [(cx + 22, 104), (cx + 42, 120), (cx + 52, 108)])
        head(c, cx, 80)
        c.paint(c.poly([(cx - 16, 74), (cx - 20, 98), (cx - 12, 92)]) | c.poly([(cx + 16, 74), (cx + 20, 98), (cx + 12, 92)]), STUFF, ol=2.2, stuff=True, shade=0.3, hl=0.1)
        for i in range(7):
            a = math.radians(-90 + (i - 3) * 26)
            x, y = cx + 42 * math.cos(a), 80 + 42 * math.sin(a)
            st = c.poly([(x + (7 if j % 2 == 0 else 3) * math.cos(math.radians(-90 + j * 36)), y + (7 if j % 2 == 0 else 3) * math.sin(math.radians(-90 + j * 36))) for j in range(10)])
            accent(c, st, '#dfe6f2', '#9ec0ff')
        c.glow(c.ell(cx - 52, 106, 4), '#9ec0ff', 6, 0.5)
        c.glow(c.ell(cx + 52, 106, 4), '#9ec0ff', 6, 0.5)
    elif god == 'Kelemvor':
        robe_body(c, cx, 92, 192, 30, 46, folds=4)
        head(c, cx, 80, hood=True)
        c.paint(c.ell(cx, 82, 7, 8), '#e2dccd', ol=1.6, shade=0.25, hl=0.1)
        for k in (-1, 1):
            c.paint(c.ell(cx + k * 3, 80, 2, 2.2), '#2a2420', ol=0, shade=0, hl=0)
        arm(c, [(cx - 26, 106), (cx - 10, 140), (cx, 146)])
        sc = c.line_mask([(cx, 150), (cx, 124)], 3) | c.line_mask([(cx - 28, 128), (cx + 28, 128)], 3)
        accent(c, sc, SILVER)
        for sx in (cx - 26, cx + 26):
            c.stroke([(sx, 128), (sx - 7, 148)], SILVER, 1.2); c.stroke([(sx, 128), (sx + 7, 148)], SILVER, 1.2)
            accent(c, c.ell(sx, 150, 10, 4), SILVER)
    elif god == 'Malar':
        robe_body(c, cx, 100, 192, 34, 44, folds=4)
        arm(c, [(cx - 30, 108), (cx - 58, 84), (cx - 54, 52)], w=12)
        arm(c, [(cx + 30, 108), (cx + 58, 84), (cx + 54, 52)], w=12)
        for k in (-1, 1):
            for j in range(3):
                accent(c, c.poly([(cx + k * (48 + j * 6), 46), (cx + k * (50 + j * 6), 30), (cx + k * (54 + j * 6), 46)]), '#e4dcc8')
        # beast head
        c.paint(c.poly([(cx - 22, 92), (cx - 24, 66), (cx - 10, 56), (cx + 10, 56), (cx + 24, 66), (cx + 22, 92), (cx, 104)]), STUFF, ol=3, stuff=True, shade=0.3, hl=0.2)
        for k in (-1, 1):
            c.paint(c.poly([(cx + k * 16, 60), (cx + k * 30, 36), (cx + k * 22, 64)]), STUFF, ol=2.6, stuff=True, shade=0.3, hl=0.2)
            accent(c, c.ell(cx + k * 9, 76, 3.5, 2.5), '#c24a3a', '#ff7a5a')
        c.paint(c.poly([(cx - 8, 92), (cx + 8, 92), (cx, 100)]), '#e4dcc8', ol=1.6, shade=0.2, hl=0.1)
    elif god == 'Talos':
        robe_body(c, cx, 96, 192, 30, 46, folds=4)
        arm(c, [(cx - 28, 104), (cx - 50, 70), (cx - 44, 40)])
        arm(c, [(cx + 28, 104), (cx + 50, 70), (cx + 44, 40)])
        head(c, cx, 80)
        c.paint(c.ell(cx + 5, 78, 5, 4), '#3c3a3a', ol=1.4, shade=0.1, hl=0)
        c.stroke([(cx - 14, 72), (cx + 14, 84)], '#3c3a3a', 1.6)
        for k in (-1, 1):
            bolt = c.poly([(cx + k * 44, 40), (cx + k * 36, 22), (cx + k * 46, 22), (cx + k * 38, 2), (cx + k * 58, 26), (cx + k * 48, 26), (cx + k * 54, 40)])
            accent(c, bolt, '#e8dc8a', '#bcd8ff')
    elif god == 'Bane':
        robe_body(c, cx, 100, 192, 32, 44, folds=3)
        c.paint(c.rect(cx - 32, 98, cx + 32, 146, 6), STUFF, ol=3, stuff=True, shade=0.3, hl=0.2)
        for k in (-1, 1):
            c.paint(c.poly([(cx + k * 22, 96), (cx + k * 46, 92), (cx + k * 48, 112), (cx + k * 30, 114)]), STUFF, ol=2.6, stuff=True, shade=0.3, hl=0.25)
        head(c, cx, 80, helm=True)
        for k in (-1, 1):
            c.paint(c.poly([(cx + k * 10, 66), (cx + k * 18, 50), (cx + k * 16, 68)]), STUFF, ol=2.2, stuff=True, shade=0.3, hl=0.2)
        arm(c, [(cx - 36, 108), (cx - 46, 140), (cx - 40, 160)])
        arm(c, [(cx + 36, 108), (cx + 58, 84), (cx + 62, 62)], w=11)
        gaunt = c.rect(cx + 46, 26, cx + 80, 66, 7) | c.rect(cx + 76, 44, cx + 88, 58, 4)
        for i in range(4):
            gaunt |= c.rect(cx + 47 + i * 8.5, 12, cx + 54 + i * 8.5, 34, 3.5)
        accent(c, gaunt, '#35333a', None)
        c.paint(c.ell(cx + 63, 48, 5), '#7a2f35', ol=1.2, shade=0.2, hl=0.3)
    elif god == 'Shar':
        robe_body(c, cx, 92, 192, 26, 50, folds=6)
        head(c, cx, 80, hood=True)
        arm(c, [(cx - 24, 104), (cx - 30, 132), (cx - 10, 136)])
        arm(c, [(cx + 24, 104), (cx + 30, 132), (cx + 10, 136)])
        disc = c.ell(cx, 136, 22)
        c.glow(disc, '#4a3a6a', 8, 0.35)
        accent(c, disc, '#5a4a7a')
        c.paint(c.ell(cx, 136, 16), '#14111c', ol=0, shade=0, hl=0)
        c.paint(c.ring(cx, 136, 16, 19), '#8a6fc4', ol=0, shade=0.1, hl=0.2)
    elif god == 'Surtr':
        robe_body(c, cx, 104, 192, 38, 46, folds=3)
        c.paint(c.rect(cx - 40, 100, cx + 40, 150, 8), STUFF, ol=3, stuff=True, shade=0.3, hl=0.2)
        c.stroke([(cx - 20, 116), (cx, 124), (cx + 20, 116)], mul(STUFF, 0.7), 1.6)
        head(c, cx, 84, r=17)
        c.paint(c.poly([(cx - 15, 92), (cx, 112), (cx + 15, 92), (cx + 10, 102), (cx, 106), (cx - 10, 102)]), STUFF, ol=2.4, stuff=True, shade=0.3, hl=0.1)
        fl = c.poly([(cx - 20, 76), (cx - 24, 50), (cx - 12, 60), (cx - 6, 36), (cx + 4, 56), (cx + 14, 40), (cx + 16, 60), (cx + 26, 52), (cx + 20, 76)])
        accent(c, fl, '#d8743a', '#ffb060')
        arm(c, [(cx + 38, 108), (cx + 62, 80), (cx + 60, 52)], w=13)
        with c.zoom(cx + 60, 100, 2.4):
            hammer(c, cx + 60, 100, -90, 28, head='#8a8d92', handle='#7a5a3e')
        arm(c, [(cx - 38, 108), (cx - 58, 140), (cx - 54, 160)], w=13)
        anvil(c, cx - 66, 176, 0.75, metal='#7a7d82')
    elif god == 'Pantheon':
        ob = c.poly([(cx - 34, 192), (cx - 26, 34), (cx, 12), (cx + 26, 34), (cx + 34, 192)])
        c.paint(ob, STUFF, ol=3, stuff=True, shade=0.32, hl=0.15, tex=c.blocks(80, 36, 0.85))
        syms = [(cx, 52, GOLD, 'sun'), (cx, 92, '#c3d4f0', 'star'), (cx, 132, SILVER, 'scale'), (cx, 168, '#5a4a7a', 'disc')]
        for x, y, col, kind in syms:
            c.paint(c.ell(x, y, 15), mul(STUFF, 0.8), ol=2, stuff=True, shade=0.25, hl=0.1)
            if kind == 'sun':
                accent(c, c.poly([(x + (11 if i % 2 == 0 else 7) * math.cos(math.radians(i * 30)), y + (11 if i % 2 == 0 else 7) * math.sin(math.radians(i * 30))) for i in range(12)]), col)
            elif kind == 'star':
                accent(c, c.poly([(x + (11 if j % 2 == 0 else 4.5) * math.cos(math.radians(-90 + j * 36)), y + (11 if j % 2 == 0 else 4.5) * math.sin(math.radians(-90 + j * 36))) for j in range(10)]), col, '#9ec0ff')
            elif kind == 'scale':
                accent(c, c.line_mask([(x - 9, y - 4), (x + 9, y - 4)], 2) | c.line_mask([(x, y - 8), (x, y + 8)], 2) | c.ell(x - 9, y + 4, 4, 2) | c.ell(x + 9, y + 4, 4, 2), col)
            else:
                accent(c, c.ell(x, y, 10), col)
                c.paint(c.ell(x, y, 6), '#14111c', ol=0, shade=0, hl=0)


# ---------------- floor circles (Transparent shader) ----------------
def chalk_ring(c, cx, cy, r, w, col, alpha=1.0):
    m = c.ring(cx, cy, r - w / 2, r + w / 2)
    c._put(m, col, alpha)
    return m


def floor_circle(c, size, style):
    cx = cy = size / 2
    R = size / 2 - 6
    if style == 'abyss':
        line, deep, glowc, accent_c = '#7e2a26', '#2a1416', '#c4402e', '#e2a07a'
    elif style == 'silver':
        line, deep, glowc, accent_c = '#9aa4ae', '#1c2026', '#cfe2f4', '#e8eef4'
    elif style == 'binding':
        line, deep, glowc, accent_c = '#7aa0b8', '#16202a', '#9cd0f0', '#d8eef8'
    else:
        line, deep, glowc, accent_c = '#b89a5a', '#2a2418', '#f0d68a', '#f4e8c0'
    # darkened stone disc with inset border
    disc = c.ell(cx, cy, R)
    c._put(disc, deep, 0.55)
    c.glow(chalk_ring(c, cx, cy, R - 3, 5, line), glowc, 6, 0.25)
    chalk_ring(c, cx, cy, R - 3, 2.2, accent_c, 0.9)
    chalk_ring(c, cx, cy, R * 0.86, 3, line)
    chalk_ring(c, cx, cy, R * 0.72, 2.2, line)
    # rune band
    nr = int(R * 0.33)
    rune_marks(c, cx, cy, R * 0.79, nr, accent_c, 1.6, R * 0.022 + 2.0)
    if style in ('abyss', 'silver'):
        pts = [(cx + R * 0.72 * math.cos(math.radians(-90 + i * 144)), cy + R * 0.72 * math.sin(math.radians(-90 + i * 144))) for i in range(6)]
        pm = c.line_mask(pts, 3.2)
        c.glow(pm, glowc, 5, 0.3)
        c._put(pm, line, 1.0)
        c._put(c.line_mask(pts, 1.2), accent_c, 0.9)
        chalk_ring(c, cx, cy, R * 0.28, 2.5, line)
        # candles at the points + skulls between
        for i in range(5):
            a = math.radians(-90 + i * 72)
            x, y = cx + R * 0.86 * math.cos(a), cy + R * 0.86 * math.sin(a)
            with c.zoom(x, y, size / 260):
                candle(c, x, y, 13, 7, '#3a3336' if style == 'abyss' else WAX, True, '#4a4040')
            if style == 'abyss':
                a2 = math.radians(-90 + i * 72 + 36)
                x2, y2 = cx + R * 0.55 * math.cos(a2), cy + R * 0.55 * math.sin(a2)
                c._put(c.ell(x2, y2, R * 0.06, R * 0.04), '#5a1414', 0.55)
        if style == 'abyss':
            rng = c.rng
            for i in range(14):
                a = rng.uniform(0, 6.283); rr = rng.uniform(0.1, 0.65) * R
                c._put(c.ell(cx + rr * math.cos(a), cy + rr * math.sin(a), rng.uniform(1.5, 5)), '#5a1414', 0.6)
            with c.zoom(cx, cy, size / 220):
                skull(c, cx, cy, 1.3)
        else:
            c.glow(c.ell(cx, cy, R * 0.12), glowc, R * 0.08, 0.5)
            with c.zoom(cx, cy, size / 200):
                gem(c, cx, cy, 7, '#cfe2f4', ol=1.6)
    elif style == 'binding':
        for i in range(8):
            a = math.radians(i * 45)
            x, y = cx + R * 0.72 * math.cos(a), cy + R * 0.72 * math.sin(a)
            chalk_ring(c, x, y, R * 0.1, 2.0, line)
            x2, y2 = cx + R * 0.3 * math.cos(a), cy + R * 0.3 * math.sin(a)
            c._put(c.line_mask([(x2, y2), (cx + R * 0.62 * math.cos(a), cy + R * 0.62 * math.sin(a))], 1.6), accent_c, 0.8)
        chalk_ring(c, cx, cy, R * 0.3, 2.5, line)
        # iron anchor pegs with chains to centre
        for i in range(4):
            a = math.radians(45 + i * 90)
            x, y = cx + R * 0.86 * math.cos(a), cy + R * 0.86 * math.sin(a)
            chain(c, [(x, y), (cx + R * 0.32 * math.cos(a), cy + R * 0.32 * math.sin(a))], '#6c7076', 7)
            c.paint(c.ell(x, y, 7), '#55585c', ol=2, drop=0.4, shade=0.3, hl=0.3)
            c.paint(c.ell(x, y, 3), '#2c2e31', ol=0, shade=0, hl=0)
        c.glow(c.ell(cx, cy, R * 0.15), glowc, R * 0.1, 0.4)
        rune_marks(c, cx, cy, R * 0.18, 6, accent_c, 1.4, 3)
    else:  # warding glyph (1x1)
        cross = c.line_mask([(cx, cy - R * 0.7), (cx, cy + R * 0.7)], 2.4) | c.line_mask([(cx - R * 0.7, cy), (cx + R * 0.7, cy)], 2.4)
        c.glow(cross, glowc, 3, 0.3)
        c._put(cross, line, 1)
        sq = [(cx, cy - R * 0.5), (cx + R * 0.5, cy), (cx, cy + R * 0.5), (cx - R * 0.5, cy), (cx, cy - R * 0.5)]
        c._put(c.line_mask(sq, 1.8), accent_c, 0.9)
        for i in range(4):
            a = math.radians(i * 90 + 45)
            c._put(c.ell(cx + R * 0.55 * math.cos(a), cy + R * 0.55 * math.sin(a), 3.2), accent_c, 0.9)
        c.glow(c.ell(cx, cy, 6), glowc, 5, 0.5)
        c.paint(c.ell(cx, cy, 6), '#e8d9a0', ol=1.6, shade=0.2, hl=0.3)


if __name__ == '__main__':
    out = sys.argv[1]
    which = sys.argv[2:]
    jobs = {
        'Alembic': (128, 128, alembic, False),
        'ArcaneFocusStone': (128, 128, focus_stone, False),
        'DeityStatue': (128, 128, deity_statue, True),
    }
    for g in 'Lathander Tyr Mystra Kelemvor Malar Talos Bane Shar Surtr Pantheon'.split():
        jobs['Statue_' + g] = (256, 256, (lambda g: lambda c: statue(c, g))(g), True)
    jobs['AbyssCircle'] = (512, 512, lambda c: floor_circle(c, 512, 'abyss'), False)
    jobs['AbyssCircleSilver'] = (512, 512, lambda c: floor_circle(c, 512, 'silver'), False)
    jobs['BindingCircle'] = (512, 512, lambda c: floor_circle(c, 512, 'binding'), False)
    jobs['WardingGlyph'] = (128, 128, lambda c: floor_circle(c, 128, 'ward'), False)
    for nm in (which or jobs):
        w, h, fn, mk = jobs[nm]
        c = Canvas(w, h, seed=5)
        fn(c)
        c.save(f'{out}/{nm}.png', f'{out}/{nm}_m.png' if mk else None, mask_alpha=nm.startswith('Statue_'))
