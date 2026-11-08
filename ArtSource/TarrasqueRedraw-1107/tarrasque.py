import sys, math
sys.path.insert(0, sys.path[0])
from paint import *

S = 512
BODY = '#6a5a45'
PLATE = '#887453'
HORN = '#8f5446'
RING = '#5c352e'
TIP = '#c9b38f'
MOUTH = '#5e2420'
GUM = '#8a3b33'
TOOTH = '#dccfb0'
TONGUE = '#3b2e33'
EYE = '#d8b24c'
CLAW = '#8a5040'


class Pal:
    def __init__(self, dead=False):
        self.dead = dead

    def __call__(self, c):
        if not self.dead:
            return c
        c = hexc(c)
        g = sum(c) / 3
        return mul(mix(c, (g, g * 0.98, g * 0.93), 0.72), 0.9)


def spike(c, pal, root, ang, length, w0, bend=-0.35, rings=4, dark=1.0):
    """Curved horn from root, direction ang (deg, 0=right, -90=up), bending by bend rad."""
    pts = [root]
    x, y = root
    a = math.radians(ang)
    seg = length / 3
    for i in range(3):
        x += math.cos(a) * seg
        y += math.sin(a) * seg
        pts.append((x, y))
        a += bend
    col, ring, tip = pal(mul(HORN, dark)), pal(mul(RING, dark)), pal(mul(TIP, dark))
    return c.horn(pts, w0, col=col, ring=ring, tip=tip, rings=rings)


def teeth_row(c, pal, p0, p1, n, size, down=True, clip=None):
    for i in range(n):
        t = (i + 0.5) / n
        x = p0[0] + (p1[0] - p0[0]) * t
        y = p0[1] + (p1[1] - p0[1]) * t
        s = size * (0.7 + 0.5 * math.sin(t * math.pi))
        d = 1 if down else -1
        tri = [(x - s * 0.38, y), (x + s * 0.38, y), (x + s * 0.08, y + d * s)]
        m = c.mask(tri)
        c.fill(m, pal(TOOTH))
        c.stroke(tri, 1.3)


def wrinkles(c, pal, clip, lines, col=None, w=1.6):
    for ln in lines:
        c.stroke(catmull(ln, n=6), w, col=pal(col or mul(BODY, 0.72)), closed=False, clip=clip)


def scute(cx, cy, rx, ry, rot):
    """Scale plate: rounded front (+x), pointed rear (-x), rotated."""
    pts = []
    for i in range(24):
        t = 2 * math.pi * i / 24
        x, y = math.cos(t), math.sin(t)
        if x < 0:
            x = x * 1.35
            y = y * (1 + x * 0.45)
        pts.append((x * rx, y * ry))
    a = math.radians(rot)
    return [(cx + x * math.cos(a) - y * math.sin(a), cy + x * math.sin(a) + y * math.cos(a)) for x, y in pts]


def plates(c, pal, clip, rows, col=PLATE):
    for cx, cy, rx, ry, rot in rows:
        pts = scute(cx, cy, rx, ry, rot)
        m = c.mask(pts) & clip
        c.shade(m, pal(col), 5, 2, shf=0.7)
        c.stroke(pts, 1.8, clip=clip)


def claws(c, pal, feet, size=1.0):
    for (x, y, dx) in feet:
        c.horn([(x, y - 6 * size), (x + dx * 10 * size, y - 2 * size), (x + dx * 18 * size, y + 6 * size)],
               12 * size, col=pal(CLAW), ring=pal(RING), tip=pal(TIP), rings=1, w=1.8, sh=2, hi=1)


# ------------------------------------------------------------------ EAST
def east(dead=False):
    c = Canvas(S, S, seed=5)
    pal = Pal(dead)
    far = lambda col: mul(pal(col), 0.78)
    with c.zoom(256, 470, 0.94, dx=-16, dy=2):
        _east(c, pal, far, dead)
    c.grain(0.035)
    return c.finish(outline=3.2)


def _east(c, pal, far, dead):
    # far legs
    c.flesh([(195, 300), (245, 300), (250, 380), (242, 440), (250, 466), (196, 468), (200, 430), (190, 370)], far(BODY), sh=6, joints=[('y', 300, 348)])
    c.flesh([(345, 300), (398, 300), (405, 370), (398, 440), (410, 466), (350, 468), (356, 430), (342, 370)], far(BODY), sh=6, joints=[('y', 300, 348)])
    claws(c, lambda x: mul(pal(x), 0.8), [(232, 466, 1), (246, 466, 1), (390, 466, 1), (404, 466, 1)], 0.9)
    # far head horn (behind)
    c.horn([(402, 236), (392, 190), (366, 150), (338, 132), (318, 136)], 30, col=far(HORN), ring=far(RING), tip=far(TIP), rings=4)
    # tail
    tail = [(150, 318), (98, 344), (52, 330), (26, 285), (34, 236), (62, 206)]
    tc, L, R = c.taper(tail, 78, 26, ease=0.9)
    for i in range(1, 5):  # tail spikes along top edge
        k = int(len(R) * (0.15 + i * 0.17))
        x, y = R[min(k, len(R) - 1)]
        x2, y2 = tc[min(k, len(tc) - 1)]
        ang = math.degrees(math.atan2(y - y2, x - x2))
        spike(c, pal, (x2 + (x - x2) * 0.6, y2 + (y - y2) * 0.6), ang, 34 - i * 3, 15 - i, bend=-0.25, rings=2)
    tm = c.mask(L + R[::-1])
    c.flesh_acc |= tm
    c.shade(tm, pal(BODY), 8, 3, against=c.ref)
    for i in range(2, 9):  # segment ridges
        k = int(len(tc) * i / 9)
        c.stroke([L[k], tc[k], R[k]], 1.6, col=pal(mul(BODY, 0.7)), closed=False, clip=tm)
    c.stroke_fade(L + R[::-1], 2.6, [(150, 318, 30, 80)])
    # tail hook pincer
    c.horn([(66, 210), (88, 188), (112, 192), (120, 214)], 26, col=pal(HORN), ring=pal(RING), tip=pal(TIP), rings=2)
    c.horn([(56, 206), (52, 178), (66, 158), (86, 156)], 20, col=pal(HORN), ring=pal(RING), tip=pal(TIP), rings=2)
    # back spikes, rear row
    for root, ang, ln, w0 in [((150, 240), -150, 92, 30), ((195, 196), -135, 112, 34), ((248, 170), -120, 118, 36),
                              ((302, 160), -108, 112, 34), ((350, 176), -100, 92, 30), ((388, 210), -85, 66, 24)]:
        spike(c, pal, root, ang, ln, w0, bend=-0.22, rings=4)
    # body hump
    body = [(122, 340), (118, 280), (148, 214), (210, 172), (290, 156), (352, 172), (398, 214), (418, 276), (408, 340), (362, 384), (270, 398), (182, 392)]
    bm, _ = c.flesh(body, pal(BODY), sh=16, hi=5, w=0, ao=0.3)
    rows = []
    for f, n, sz in [(0.93, 7, 1.0), (0.78, 7, 0.95), (0.62, 6, 0.85)]:
        for i in range(n):
            th = math.radians(205 + 130 * (i + 0.5 * (f < 0.9 and f > 0.7)) / (n - 0.5))
            x, y = 268 + 150 * f * math.cos(th), 300 + 140 * f * math.sin(th)
            rot = math.degrees(math.atan2(140 * math.cos(th), -150 * math.sin(th)))
            rows.append((x, y, 26 * sz, 17 * sz, rot))
    rows.sort(key=lambda r: (r[1] > 0, r[0]))
    plates(c, pal, bm, rows)
    wrinkles(c, pal, bm, [[(150, 320), (180, 345), (220, 352)], [(260, 330), (300, 350), (340, 345)], [(200, 300), (240, 315)], [(300, 300), (340, 312)]])
    # front-row spikes sticking out of plates
    for root, ang, ln, w0 in [((205, 214), -130, 56, 20), ((262, 194), -112, 62, 22), ((320, 198), -96, 56, 20), ((250, 252), -120, 40, 15), ((312, 256), -100, 40, 15)]:
        spike(c, pal, root, ang, ln, w0, bend=-0.25, rings=2)
    # near hind leg
    hl, _ = c.flesh([(140, 296), (218, 290), (240, 345), (226, 405), (230, 446), (238, 470), (158, 472), (166, 440), (156, 398), (130, 348)], pal(BODY), sh=12, hi=4, joints=[('y', 296, 362)])
    wrinkles(c, pal, hl, [[(170, 330), (200, 350), (222, 345)], [(168, 410), (200, 418), (224, 410)]])
    claws(c, pal, [(196, 470, 1), (214, 470, 1), (230, 470, 1)], 1.0)
    # neck
    c.flesh([(360, 226), (420, 228), (444, 300), (412, 352), (360, 340), (344, 280)], pal(BODY), sh=10, hi=4, w=0, ao=0.25, joints=[(350, 276, 60, 125)])
    # near front leg (massive)
    fl, _ = c.flesh([(318, 286), (392, 276), (418, 334), (400, 392), (406, 440), (420, 470), (326, 474), (338, 440), (322, 392), (306, 336)], pal(BODY), sh=14, hi=5, joints=[('y', 286, 356)])
    wrinkles(c, pal, fl, [[(330, 330), (352, 316), (380, 314)], [(336, 360), (362, 352), (392, 356)]])
    wrinkles(c, pal, fl, [[(340, 400), (370, 410), (398, 404)], [(344, 430), (372, 438), (398, 432)]])
    for x in (360, 384, 404):
        c.horn([(x, 460), (x + 16, 458), (x + 32, 468), (x + 36, 482)], 18, col=pal(CLAW), ring=pal(RING), tip=pal(TIP), rings=2, w=2.0, sh=3)
    with c.zoom(440, 300, 1.15, dx=-6, dy=6):
        # mouth interior
        mm, _ = c.part([(404, 290), (470, 292), (506, 300), (502, 352), (470, 360), (408, 342)], pal(MOUTH), sh=0, hi=0, w=2.4, smooth=False)
        c.part([(420, 296), (500, 302), (498, 316), (430, 312)], pal(GUM), sh=0, hi=0, w=0, smooth=False)
        # lower jaw
        lj, ljp = c.part([(392, 322), (440, 334), (488, 344), (504, 350), (496, 372), (456, 384), (410, 372), (382, 348)], pal(BODY), sh=8, hi=3)
        teeth_row(c, pal, (418, 340), (496, 352), 6, 16, down=False)
        # tongue
        c.horn([(470, 336), (494, 350), (504, 374), (494, 396), (476, 400)], 16, col=pal(TONGUE), ring=pal(TONGUE), tip=pal(mul(TONGUE, 1.25)), rings=0, w=2.0, sh=3, hi=1, w1=6)
        # upper head / snout
        hd, _ = c.flesh([(380, 246), (428, 226), (478, 236), (506, 262), (510, 292), (470, 300), (420, 304), (384, 296)], pal(BODY), sh=10, hi=4, joints=[(382, 268, 22, 60)], ao=0.2)
        plates(c, pal, hd, [(420, 248, 26, 14, -10), (462, 254, 22, 12, 15)], col=mul(PLATE, 0.95))
        teeth_row(c, pal, (418, 300), (506, 292), 7, 20, down=True)
        # forward horn + swept horn (near side)
        c.horn([(458, 246), (486, 224), (506, 192), (504, 160)], 26, rings=4, col=pal(HORN), ring=pal(RING), tip=pal(TIP))
        c.horn([(412, 246), (398, 196), (372, 156), (340, 136), (316, 142)], 36, rings=5, col=pal(HORN), ring=pal(RING), tip=pal(TIP))
        # cheek spikes
        spike(c, pal, (430, 296), 120, 30, 14, bend=0.2, rings=1)
        # eyes cluster
        for (x, y, r) in [(446, 264, 4.2), (458, 270, 3.4), (438, 274, 3.0), (452, 280, 2.6)]:
            c.dot(x, y, r, pal('#2a2220') if dead else EYE, 1.2)



def tailhook(c, pal, end, flip=1):
    x, y = end
    c.horn([(x, y), (x + 22 * flip, y - 22), (x + 46 * flip, y - 18), (x + 54 * flip, y + 4)], 24, col=pal(HORN), ring=pal(RING), tip=pal(TIP), rings=2)
    c.horn([(x - 8 * flip, y - 4), (x - 12 * flip, y - 32), (x + 2 * flip, y - 52), (x + 22 * flip, y - 54)], 19, col=pal(HORN), ring=pal(RING), tip=pal(TIP), rings=2)


def hump_rows(cx, cy, rx, ry, specs, a0, a1):
    rows = []
    for f, n, sz in specs:
        for i in range(n):
            th = math.radians(a0 + (a1 - a0) * (i + 0.5) / n)
            rows.append((cx + rx * f * math.cos(th), cy + ry * f * math.sin(th), 25 * sz, 17 * sz))
    return rows


# ------------------------------------------------------------------ SOUTH
def south(dead=False):
    c = Canvas(S, S, seed=7)
    pal = Pal(dead)
    far = lambda col: mul(pal(col), 0.78)
    with c.zoom(256, 470, 0.95, dy=2):
        # tail curling up behind, right side
        tail = [(330, 300), (398, 286), (446, 240), (452, 182), (430, 150)]
        tc, L, R = c.taper(tail, 62, 24, ease=0.9)
        tm = c.mask(L + R[::-1])
        c.flesh_acc |= tm
        c.shade(tm, far(BODY), 8, 3, against=c.ref)
        for i in range(2, 8):
            k = int(len(tc) * i / 8)
            c.stroke([L[k], tc[k], R[k]], 1.6, col=mul(far(BODY), 0.75), closed=False, clip=tm)
        c.stroke_fade(L + R[::-1], 2.6, [(330, 300, 30, 80)])
        tailhook(c, lambda x: mul(pal(x), 0.85), (428, 152), -1)
        # back spikes fan
        for root, ang, ln, w0, b in [((120, 230), -165, 96, 30, -0.25), ((150, 192), -138, 112, 34, -0.22),
                                     ((362, 192), -42, 112, 34, 0.22), ((392, 230), -15, 96, 30, 0.25),
                                     ((256, 160), -90, 96, 32, 0.0)]:
            spike(c, pal, root, ang, ln, w0, bend=b, rings=4)
        body = [(70, 330), (80, 250), (130, 186), (200, 152), (256, 144), (312, 152), (382, 186), (432, 250), (442, 330), (400, 390), (256, 410), (112, 390)]
        bm, _ = c.flesh(body, pal(BODY), sh=16, hi=5, w=0, ao=0.3)
        rows = hump_rows(256, 300, 182, 150, [(0.92, 9, 1.0), (0.76, 8, 0.95), (0.6, 6, 0.85)], 192, 348)
        rows.sort(key=lambda r: r[1])
        plates(c, pal, bm, [(x, y, rx, ry, 90 + (x - 256) * 0.25) for x, y, rx, ry in rows])
        for root, ang, ln, w0, b in [((150, 220), -140, 54, 20, -0.25),  ((362, 220), -40, 54, 20, 0.25), ((180, 250), -125, 40, 15, -0.2), ((332, 250), -55, 40, 15, 0.2)]:
            spike(c, pal, root, ang, ln, w0, bend=b, rings=2)
        # hind feet glimpsed between front legs
        for x in (200, 312):
            c.flesh([(x - 30, 420), (x + 30, 420), (x + 32, 466), (x - 32, 466)], far(BODY), sh=4, joints=[('y', 420, 446)])
        # front legs
        for sgn in (-1, 1):
            X = lambda x: 256 + sgn * (256 - x)
            lp = [(X(x), y) for x, y in [(78, 300), (166, 288), (190, 350), (172, 410), (180, 468), (88, 472), (98, 420), (76, 360)]]
            lm, _ = c.flesh(lp, pal(BODY), sh=13, hi=5, joints=[('y', 298, 366)])
            wrinkles(c, pal, lm, [[(X(x), y) for x, y in ln] for ln in [[(96, 400), (130, 410), (170, 402)], [(96, 432), (132, 440), (172, 434)], [(92, 330), (128, 318), (166, 322)]]])
            for x in (104, 132, 160):
                c.horn([(X(x), 452), (X(x) + sgn * 0, 470), (X(x) - sgn * 4, 484)], 18, col=pal(CLAW), ring=pal(RING), tip=pal(TIP), rings=1, w=2.0, sh=3)
        # shoulders / neck mass
        c.flesh(c.ellipse_pts(256, 300, 108, 66), pal(BODY), sh=10, hi=4, smooth=False, w=0, ao=0.2)
        with c.zoom(256, 330, 1.08):
            # lower jaw
            c.part([(172, 328), (340, 328), (332, 392), (256, 424), (180, 392)], pal(BODY), sh=10, hi=3)
            # maw
            c.part(c.ellipse_pts(256, 352, 72, 46), pal(MOUTH), sh=0, hi=0, smooth=False)
            c.part(c.ellipse_pts(256, 366, 46, 24), pal(mul(MOUTH, 0.7)), sh=0, hi=0, w=0, smooth=False)
            for i in range(9):
                t = math.radians(200 - 220 * (i + 0.5) / 9 + 0)
            # lower teeth (pointing up) along bottom arc
            for i in range(8):
                th = math.radians(25 + 130 * (i + 0.5) / 8)
                x, y = 256 + 66 * math.cos(th), 352 + 42 * math.sin(th)
                s_ = 15 * (0.75 + 0.4 * math.sin(th))
                tri = [(x - s_ * 0.36, y + 2), (x + s_ * 0.36, y + 2), (x, y - s_)]
                c.fill(c.mask(tri), pal(TOOTH)); c.stroke(tri, 1.3)
            # tongue hanging down
            c.horn([(262, 360), (270, 392), (262, 424), (246, 446), (236, 444)], 22, col=pal(TONGUE), ring=pal(TONGUE), tip=pal(mul(TONGUE, 1.25)), rings=0, w=2.0, sh=3, hi=1, w1=8)
            # upper head / brow
            hd, _ = c.flesh([(176, 262), (216, 236), (256, 228), (296, 236), (336, 262), (348, 300), (330, 328), (296, 334), (256, 330), (216, 334), (182, 328), (164, 300)], pal(BODY), sh=8, hi=4, joints=[(256, 222, 50, 110)], ao=0.25)
            plates(c, pal, hd, [(226, 252, 22, 13, 100), (286, 252, 22, 13, 80), (256, 244, 20, 12, 90)])
            for i in range(9):
                th = math.radians(205 + 130 * (i + 0.5) / 9)
                x = 256 + 70 * math.cos(th)
                y = 330 + 0 * math.sin(th) - 2 + abs(x - 256) * 0.05
                s_ = 18 * (1.15 - abs(x - 256) / 120)
                tri = [(x - s_ * 0.36, y - 2), (x + s_ * 0.36, y - 2), (x, y + s_)]
                c.fill(c.mask(tri), pal(TOOTH)); c.stroke(tri, 1.3)
            # eyes clusters
            for sgn in (-1, 1):
                for (dx, dy, r) in [(30, 284, 4.4), (44, 290, 3.4), (22, 296, 3.0), (38, 302, 2.6)]:
                    c.dot(256 + sgn * dx, dy, r, pal('#2a2220') if dead else EYE, 1.2)
            # horns
            for sgn in (-1, 1):
                X = lambda x: 256 + sgn * (x - 256)
                c.horn([(X(214), 250), (X(176), 206), (X(156), 158), (X(166), 112), (X(186), 96)], 36, rings=5, col=pal(HORN), ring=pal(RING), tip=pal(TIP))
                c.horn([(X(180), 302), (X(136), 300), (X(100), 280), (X(82), 248)], 26, rings=4, col=pal(HORN), ring=pal(RING), tip=pal(TIP))
                spike(c, pal, (X(240), 236), -90 - sgn * 15, 24, 12, bend=sgn * 0.2, rings=1)
    c.grain(0.035)
    return c.finish(outline=3.2)


# ------------------------------------------------------------------ NORTH
def north(dead=False):
    c = Canvas(S, S, seed=9)
    pal = Pal(dead)
    far = lambda col: mul(pal(col), 0.78)
    with c.zoom(256, 470, 0.95, dy=2):
        # head horns peeking over the front
        for sgn in (-1, 1):
            X = lambda x: 256 + sgn * (x - 256)
            c.horn([(X(212), 200), (X(178), 160), (X(166), 116), (X(180), 84)], 32, rings=4, col=far(HORN), ring=far(RING), tip=far(TIP))
        # front legs (far, darker)
        for sgn in (-1, 1):
            X = lambda x: 256 + sgn * (256 - x)
            c.flesh([(X(x), y) for x, y in [(84, 300), (160, 296), (170, 400), (166, 462), (90, 464), (88, 400)]], far(BODY), sh=6, joints=[('y', 300, 348)])
        body = [(68, 340), (74, 250), (124, 176), (200, 140), (256, 134), (312, 140), (388, 176), (438, 250), (444, 340), (404, 396), (256, 414), (108, 396)]
        bm, _ = c.flesh(body, pal(BODY), sh=16, hi=5, w=0, ao=0.3)
        rows = hump_rows(256, 320, 186, 180, [(0.95, 10, 1.0), (0.8, 9, 1.0), (0.64, 8, 0.95), (0.47, 6, 0.9), (0.3, 4, 0.85)], 185, 355)
        rows.sort(key=lambda r: -r[1])
        plates(c, pal, bm, [(x, y, rx, ry, -90 + (x - 256) * 0.3) for x, y, rx, ry in rows])
        wrinkles(c, pal, bm, [[(110, 360), (160, 385), (210, 392)], [(302, 392), (352, 385), (402, 360)]])
        # spikes: rows from far (top) to near
        sp = [((256, 150), -90, 120, 36, 0), ((196, 160), -112, 116, 34, -0.15), ((316, 160), -68, 116, 34, 0.15),
              ((140, 196), -135, 104, 32, -0.2), ((372, 196), -45, 104, 32, 0.2), ((100, 250), -160, 84, 28, -0.25), ((412, 250), -20, 84, 28, 0.25),
              ((226, 210), -100, 74, 26, -0.1), ((286, 210), -80, 74, 26, 0.1), ((170, 240), -125, 66, 24, -0.2), ((342, 240), -55, 66, 24, 0.2),
              ((256, 250), -90, 60, 22, 0), ((210, 280), -110, 46, 18, -0.15), ((302, 280), -70, 46, 18, 0.15)]
        for root, ang, ln, w0, b in sp:
            spike(c, pal, root, ang, ln, w0, bend=b, rings=4 if ln > 80 else 2)
        # hind legs near
        for sgn in (-1, 1):
            X = lambda x: 256 + sgn * (256 - x)
            hl, _ = c.flesh([(X(x), y) for x, y in [(96, 318), (186, 312), (204, 372), (188, 428), (196, 470), (102, 474), (110, 430), (90, 380)]], pal(BODY), sh=12, hi=4, joints=[('y', 318, 384)])
            wrinkles(c, pal, hl, [[(X(x), y) for x, y in ln] for ln in [[(112, 420), (148, 430), (186, 422)], [(104, 360), (140, 350), (180, 356)]]])
            for x in (116, 150):
                c.horn([(X(x), 462), (X(x) - sgn * 8, 472), (X(x) - sgn * 18, 478)], 12, col=pal(CLAW), ring=pal(RING), tip=pal(TIP), rings=0, w=1.8, sh=2)
        # tail toward viewer
        tail = [(256, 340), (264, 404), (254, 444), (220, 466), (184, 462)]
        tc, L, R = c.taper(tail, 70, 24, ease=0.9)
        tm = c.mask(L + R[::-1])
        c.ao(tm, 0.38)
        c.flesh_acc |= tm
        _o, _oa = c.rgb.copy(), c.a.copy()
        c.shade(tm, pal(BODY), 9, 3, against=c.ref)
        c.blend_joints(tm, _o, _oa, [(256, 346, 70, 140)])
        for i in range(2, 8):
            k = int(len(tc) * i / 8)
            c.stroke([L[k], tc[k], R[k]], 1.6, col=pal(mul(BODY, 0.7)), closed=False, clip=tm)
        c.stroke_fade(L + R[::-1], 2.6, [(256, 340, 45, 95)])
        tailhook(c, pal, (188, 462), -1)

    c.grain(0.035)
    return c.finish(outline=3.2)


def items(out):
    pal = Pal(False)
    # shell fragment
    c = Canvas(128, 128, seed=2)
    pts = scute(62, 68, 44, 34, -78)
    m = c.mask(pts)
    c.shade(m, PLATE, 9, 3, shf=0.68)
    inner = scute(62, 72, 30, 22, -78)
    c.shade(c.mask(inner), mul(PLATE, 1.08), 5, 2, shf=0.75)
    c.stroke(inner, 1.4, col=mul(PLATE, 0.6))
    for ln in [[(30, 70), (40, 86), (54, 98)], [(94, 70), (86, 88), (72, 100)], [(46, 52), (62, 46), (78, 52)]]:
        c.stroke(catmull(ln, n=6), 1.2, col=mul(PLATE, 0.62), closed=False, clip=m)
    c.stroke([(48, 60), (58, 54)], 2.2, col='#d9d4c4', closed=False, clip=m)
    c.stroke(pts, 2.4)
    spike(c, pal, (50, 56), -120, 34, 13, bend=-0.2, rings=2)
    spike(c, pal, (76, 56), -70, 28, 11, bend=0.2, rings=2)
    c.grain(0.03)
    c.finish(outline=1.6).save(f'{out}/TarrasqueShell.png')
    # tooth
    c = Canvas(128, 128, seed=4)
    c.part([(30, 96), (58, 86), (66, 104), (40, 116), (24, 110)], GUM, sh=4, hi=1.5, w=2.2)
    c.horn([(46, 100), (54, 70), (72, 42), (96, 24), (110, 22)], 32, col=TOOTH, ring=mul(TOOTH, 0.82), tip='#ece4d0', rings=3, w=2.4, sh=7, hi=2)
    c.stroke(catmull([(56, 86), (66, 60), (84, 40)], n=6), 1.2, col=mul(TOOTH, 0.7), closed=False)
    c.grain(0.03)
    c.finish(outline=1.6).save(f'{out}/TarrasqueTooth.png')


if __name__ == '__main__':
    out = sys.argv[1]
    items(out)
    for name, fn in (('east', east), ('south', south), ('north', north)):
        if len(sys.argv) > 2 and name not in sys.argv[2:]:
            continue
        for dead, pre in ((False, ''), (True, 'Dessicated_')):
            import paint
            paint.REF = None
            fn(dead)
            paint.REF = paint.LAST.flesh_acc.copy()
            fn(dead).save(f'{out}/{pre}Tarrasque_{name}.png')
            paint.REF = None
