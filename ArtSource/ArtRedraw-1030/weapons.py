# 2026-10-30 DND 武器 / 投掷物重画（RimWorld 原版画风）。
# 每把武器沿旧贴图量出的主轴（axis.measure）重画：贴图尺寸、朝向、长度都与旧图一致，手持偏移不用改。
# 局部坐标：u 沿主轴（握把端 0 → 尖端 L），v 垂直（枪械 +v 朝下）。数值单位是 128 像素画布。
# 用法：E:\Python\python.exe ArtSource\ArtRedraw-1030\weapons.py <模组根目录> [defName ...]
import json, math, os, sys
import numpy as np
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
sys.path.insert(0, os.path.join(os.path.dirname(HERE), "ArtRedraw-1029"))
from rwart import Canvas, mul, mix, hexc, INK  # noqa: E402
from axis import measure  # noqa: E402

ROOT = sys.argv[1] if len(sys.argv) > 1 else os.getcwd()
ONLY = set(sys.argv[2:])

# ---- 调色（低饱和） ----
STEEL, STEEL_D, STEEL_L = '#a7abb0', '#6b6f74', '#d4d8dc'
IRON, DARK = '#5d6064', '#3a3b3f'
WOOD, WOOD_D, WOOD_L = '#7a5a3e', '#5a4130', '#9a7a58'
LEATHER, LEATHER_D = '#5e3b2c', '#402a20'
GOLD, GOLD_D = '#cfae62', '#9a7a3e'
SILVER = '#cfd4da'
BRONZE = '#b07c48'
BONE = '#ddd3bc'
RUNE_B, RUNE_O, RUNE_R, RUNE_G, RUNE_P = '#8fd0f0', '#f0a040', '#e0503a', '#8fe08a', '#c49af0'
ICE, ICE_L = '#9cc8e4', '#dff0fa'
FIRE, FIRE_C = '#e8782e', '#ffd27a'
STUFFG = '#d6d6d2'   # 吃材料色的部分用浅灰


class W:
    """把局部 (u, v) 映射到画布；ell 半径随缩放。"""

    def __init__(self, c, p0, p1, k, vs=1.0):
        self.c, self.k, self.vs = c, k, vs
        dx, dy = p1[0] - p0[0], p1[1] - p0[1]
        self.L = math.hypot(dx, dy)
        self.d = (dx / self.L, dy / self.L)
        self.n = (-self.d[1], self.d[0])
        if self.n[1] < 0 or (abs(self.n[1]) < 1e-6 and self.n[0] < 0):
            self.n = (-self.n[0], -self.n[1])
        self.p0 = p0
        c.tf = lambda x, y: (x * k, y * k)
        c.zs = k

    def xy(self, u, v):
        v = v * self.vs
        return (self.p0[0] + self.d[0] * u + self.n[0] * v, self.p0[1] + self.d[1] * u + self.n[1] * v)

    def poly(self, pts):
        return self.c.poly([self.xy(u, v) for u, v in pts])

    def seg(self, u0, u1, w0, w1=None):
        w1 = w0 if w1 is None else w1
        return self.poly([(u0, -w0 / 2), (u1, -w1 / 2), (u1, w1 / 2), (u0, w0 / 2)])

    def ell(self, u, v, r, r2=None):
        x, y = self.xy(u, v)
        f = self.vs ** 0.75
        return self.c.ell(x, y, r * f, None if r2 is None else r2 * f)

    def stroke(self, pts, col, w=1.0, alpha=1.0):
        return self.c.stroke([self.xy(u, v) for u, v in pts], col, w, alpha)

    def paint(self, m, col, **kw):
        return self.c.paint(m, col, **kw)

    def glow(self, m, col, r=5, s=0.5):
        self.c.glow(m, col, r, s)


# ================= 部件 =================
def haft(w, u0, u1, th, col=WOOD, bands=(), grain=True, ol=2.2):
    w.paint(w.seg(u0, u1, th), col, ol=ol, drop=0.4, shade=0.3, hl=0.15)
    if grain:
        for i, v in enumerate((-th * .2, th * .15)):
            a = u0 + (u1 - u0) * (.08 + .1 * i)
            w.stroke([(a, v), (a + (u1 - u0) * .35, v + .3)], mul(col, .72), .6, .8)
            w.stroke([(a + (u1 - u0) * .45, -v), (a + (u1 - u0) * .8, -v - .2)], mul(col, .72), .6, .8)
    for u, bw, bc in bands:
        w.paint(w.seg(u - bw / 2, u + bw / 2, th + 1.6), bc, ol=1.4, shade=0.25, hl=0.2)


def grip(w, u0, u1, th, col=LEATHER):
    w.paint(w.seg(u0, u1, th), col, ol=2.0, drop=0.4, shade=0.25, hl=0.1)
    u = u0 + 1.5
    while u < u1 - 1:
        w.stroke([(u, -th / 2 + .3), (u + th * .55, th / 2 - .3)], mul(col, .62), .7)
        u += 2.6


def pommel(w, u, r, col=GOLD, gem=None):
    m = w.ell(u, 0, r)
    w.paint(m, col, ol=1.8, drop=0.4, shade=0.3, hl=0.25)
    if gem:
        w.paint(w.ell(u, 0, r * .45), gem, ol=0.9, shade=0.2, hl=0.3, grain=0)


def guard(w, u, span, th, col=GOLD, curl=0.0, gem=None):
    pts = [(u - th / 2, -span), (u + th / 2 + curl, -span - curl * .4), (u + th / 2, 0), (u + th / 2 + curl, span + curl * .4), (u - th / 2, span),
           (u - th / 2 - 1, 0)]
    w.paint(w.poly(pts), col, ol=2.0, drop=0.4, shade=0.3, hl=0.25)
    for s in (-1, 1):
        w.paint(w.ell(u + curl * .6, s * (span + curl * .3), th * .55), col, ol=1.5, shade=0.25, hl=0.25)
    if gem:
        w.paint(w.ell(u, 0, th * .55), gem, ol=1.0, shade=0.2, hl=0.35, grain=0)


def blade(w, u0, u1, wd, col=STEEL, tip=None, fuller=True, wd_tip=None, edge=None, wavy=0, ol=2.2):
    tip = wd * 1.4 if tip is None else tip
    wt = wd if wd_tip is None else wd_tip
    n = 10
    top, bot = [], []
    for i in range(n + 1):
        u = u0 + (u1 - tip - u0) * i / n
        ww = wd + (wt - wd) * i / n
        off = wavy * math.sin(i * 1.7) if wavy else 0
        top.append((u, -ww / 2 + off))
        bot.append((u, ww / 2 + off))
    pts = top + [(u1, 0)] + bot[::-1]
    m = w.poly(pts)
    w.paint(m, col, ol=ol, drop=0.45, shade=0.22, hl=0.0, grain=0.02)
    # 刃口亮边（上侧）+ 下侧暗面
    w.paint(w.poly(top + [(u1, 0)] + [(u, v + wd * .28) for u, v in top[::-1]]), mix(col, '#ffffff', .35), ol=0, shade=0, hl=0, grain=0)
    w.paint(w.poly([(u, v - wd * .22) for u, v in bot] + [(u1, 0)][::-1] + bot[::-1]), mul(col, .78), ol=0, shade=0, hl=0, grain=0)
    if fuller:
        w.stroke([(u0 + 2, 0), (u1 - tip * 1.6, 0)], mul(col, .62), max(.8, wd * .16))
        w.stroke([(u0 + 2, -wd * .1), (u1 - tip * 1.6, -wd * .1)], mix(col, '#ffffff', .2), .5, .8)
    if edge:
        w.glow(m, edge, 4, 0.25)
    return m


def runes(w, u0, u1, v, col, n=4, size=1.6, glow=True):
    shapes = [[(-1, -1), (0, 1), (1, -1)], [(-1, 1), (0, -1), (1, 1)], [(0, -1), (0, 1), (1, 0)], [(-1, 0), (1, 0), (0, -1), (0, 1)],
              [(-1, -1), (1, 1), (1, -1)]]
    ms = None
    for i in range(n):
        u = u0 + (u1 - u0) * (i + .5) / n
        m = w.stroke([(u + px * size, v + py * size) for px, py in shapes[i % len(shapes)]], col, .9)
        ms = m if ms is None else (ms | m)
    if glow and ms is not None:
        w.glow(ms, col, 3, 0.4)


def axe_head(w, u, size, col=STEEL, double=False, crude=False, beard=True):
    s = size
    edge = [(u + s * (.62 * math.cos(a)), -s * (.62 + .48 * math.sin(a) ** 0.5 * (1 if beard else .9)) + s * .1 * abs(math.cos(a)))
            for a in [math.radians(x) for x in range(180, -1, -15)]]
    pts = [(u - s * .28, -s * .15), (u - s * .45, -s * .5)] + edge + [(u + s * .4, -s * .45), (u + s * .28, -s * .15)]
    if crude:
        pts = [(a + (0.8 if i % 2 else -0.6), b) for i, (a, b) in enumerate(pts)]
    m = w.poly(pts)
    w.paint(m, col, ol=2.3, drop=0.45, shade=0.28, hl=0.18)
    # 刃口亮边
    w.stroke([(a, b + s * .07) for a, b in edge[1:-1]], mix(col, '#ffffff', .45), 1.3)
    if double:
        pts2 = [(a, -b) for a, b in pts]
        w.paint(w.poly(pts2), col, ol=2.3, drop=0.45, shade=0.28, hl=0.18)
        w.stroke([(a, -b - s * .07) for a, b in edge[1:-1]], mix(col, '#ffffff', .3), 1.3)
    w.paint(w.seg(u - s * .3, u + s * .3, s * .42), mul(col, .85), ol=2.0, shade=0.25, hl=0.15)
    return m


def hammer_head(w, u, length, depth, col=STEEL, spike=0, cap=None):
    m = w.poly([(u - depth / 2, -length / 2), (u + depth / 2, -length / 2), (u + depth / 2, length / 2), (u - depth / 2, length / 2)])
    w.paint(m, col, ol=2.4, drop=0.45, shade=0.3, hl=0.2)
    for s in (-1, 1):
        w.paint(w.poly([(u - depth / 2 - .5, s * length / 2), (u + depth / 2 + .5, s * length / 2), (u + depth / 2 + .5, s * (length / 2 - 3)),
                        (u - depth / 2 - .5, s * (length / 2 - 3))]), cap or mul(col, .8), ol=1.6, shade=0.2, hl=0.25)
    if spike:
        w.paint(w.poly([(u - depth * .3, -length / 2), (u, -length / 2 - spike), (u + depth * .3, -length / 2)]), col, ol=1.8, shade=0.25, hl=0.2)
    return m


def spike_ball(w, u, r, col=STEEL, n=8, spike=None, spike_col=None):
    spike = r * .55 if spike is None else spike
    for i in range(n):
        a = 2 * math.pi * i / n
        b1, b2 = a - .28, a + .28
        tip = (u + math.cos(a) * (r + spike), math.sin(a) * (r + spike))
        w.paint(w.poly([(u + math.cos(b1) * r * .85, math.sin(b1) * r * .85), tip, (u + math.cos(b2) * r * .85, math.sin(b2) * r * .85)]),
                spike_col or mul(col, .9), ol=1.6, drop=0.3, shade=0.2, hl=0.2)
    m = w.ell(u, 0, r)
    w.paint(m, col, ol=2.2, drop=0.4, shade=0.32, hl=0.25)
    return m


def spear_head(w, u, length, wd, col=STEEL, socket=None, barbs=False):
    pts = [(u, -wd * .25), (u + length * .35, -wd / 2), (u + length, 0), (u + length * .35, wd / 2), (u, wd * .25)]
    m = w.poly(pts)
    w.paint(m, col, ol=2.0, drop=0.45, shade=0.25, hl=0.0)
    w.paint(w.poly([(u, -wd * .25), (u + length * .35, -wd / 2), (u + length, 0), (u + length * .35, 0)]), mix(col, '#ffffff', .3), ol=0, shade=0, hl=0, grain=0)
    w.stroke([(u + 1, 0), (u + length * .8, 0)], mul(col, .7), .7)
    if barbs:
        for s in (-1, 1):
            w.paint(w.poly([(u + length * .12, s * wd * .3), (u - length * .12, s * wd * .75), (u + length * .02, s * wd * .25)]), col, ol=1.6, shade=0.2, hl=0.1)
    w.paint(w.seg(u - 4, u + 1, 3.6, 3.0), socket or mul(col, .75), ol=1.6, shade=0.25, hl=0.2)
    return m


def bow(w, L, col=WOOD, limb=3.4, bulge=None, grip_col=LEATHER, string='#e6dcc4', tips=None, recurve=True):
    bulge = L * .2 if bulge is None else bulge
    n = 16
    outer, inner = [], []
    for i in range(n + 1):
        t = i / n
        u = t * L
        b = bulge * math.sin(math.pi * t)
        if recurve:
            b -= bulge * .25 * (math.exp(-((t - 0.02) / .06) ** 2) + math.exp(-((t - .98) / .06) ** 2))
        th = limb * (1.25 - .55 * abs(t - .5) * 2)
        outer.append((u, b + th / 2))
        inner.append((u, b - th / 2))
    w.stroke([(L * .01, 0), (L * .99, 0)], string, .9)
    m = w.poly(outer + inner[::-1])
    w.paint(m, col, ol=2.0, drop=0.45, shade=0.25, hl=0.25)
    w.paint(w.poly([(L * .42, bulge - limb), (L * .58, bulge - limb), (L * .58, bulge + limb), (L * .42, bulge + limb)]), grip_col, ol=1.6,
            shade=0.25, hl=0.1)
    if tips:
        for t in (0.02, .98):
            w.paint(w.ell(t * L, 0, 1.8), tips, ol=1.2, shade=0.2, hl=0.3)
    return m


def arrow_rest(w, L, bulge):
    pass


def gem_at(w, u, v, r, col, glow=True):
    m = w.poly([(u, v - r), (u + r * .85, v), (u, v + r), (u - r * .85, v)])
    if glow:
        w.glow(m, col, 4, 0.45)
    w.paint(m, col, ol=1.3, shade=0.3, hl=0.0, grain=0)
    w.c.fill(w.poly([(u, v - r + .8), (u - r * .5, v), (u, v)]), mix(col, '#ffffff', .55), 0.85)
    return m


def orb_at(w, u, v, r, col, glow=True):
    m = w.ell(u, v, r)
    if glow:
        w.glow(m, col, 6, 0.55)
    w.paint(m, col, ol=1.6, shade=0.3, hl=0.0, grain=0)
    w.c.fill(w.ell(u - r * .3, v - r * .3, r * .3), mix(col, '#ffffff', .7), 0.85)
    return m


def flame_lick(w, u0, u1, side, n=4, col=FIRE, core=FIRE_C, h=4.0):
    ms = None
    for i in range(n):
        a = u0 + (u1 - u0) * i / n
        b = u0 + (u1 - u0) * (i + 1) / n
        m = w.poly([(a, side * 1), ((a + b) / 2 + 1, side * (h + (i % 2) * 1.5)), (b, side * 1)])
        ms = m if ms is None else ms | m
    w.glow(ms, col, 5, 0.5)
    w.paint(ms, col, ol=1.2, shade=0.1, hl=0, grain=0)
    return ms


def lightning(w, pts, col=RUNE_B):
    m = w.stroke(pts, col, 1.3)
    w.glow(m, col, 4, 0.55)
    w.stroke(pts, '#f4fbff', .6)


# ================= 枪械 =================
def gun(w, L, wood=WOOD, metal=IRON, barrel_len=.55, barrel_w=3.4, stock=True, body_col=None, rune=None, bands=GOLD, flare=0, pistol=False,
        barrels=1, drum=False):
    ub0 = L * (1 - barrel_len)
    # 枪托 + 枪身
    if stock and not pistol:
        w.paint(w.poly([(0, -1.5), (L * .3, -2.5), (L * .45, -3.5), (L * .48, 2.5), (L * .3, 3.5), (L * .12, 7.5), (0, 8.0)]), wood, ol=2.2, drop=0.45,
                shade=0.3, hl=0.18)
        w.stroke([(L * .05, 2), (L * .3, .5)], mul(wood, .72), .7)
    if pistol:
        w.paint(w.poly([(L * .05, 0), (L * .32, -2.8), (L * .42, -2.8), (L * .4, 3), (L * .22, 10.5), (L * .05, 10.5), (L * .14, 4)]), wood, ol=2.2,
                drop=0.45, shade=0.3, hl=0.18)
        w.paint(w.ell(L * .1, 9.5, 2.2), body_col or metal, ol=1.4, shade=0.3, hl=0.3)
    # 机匣
    rc = body_col or metal
    w.paint(w.poly([(L * .36, -3.6), (ub0 + 2, -3.6), (ub0 + 2, 3.2), (L * .36, 3.2)]), rc, ol=2.2, drop=0.4, shade=0.3, hl=0.2)
    # 扳机护圈
    w.c.paint(w.c.line_mask([w.xy(L * .4, 3.2), w.xy(L * .42, 6.5), w.xy(L * .52, 6.5), w.xy(L * .54, 3.2)], 1.2), DARK, ol=0.8, shade=0, hl=0)
    w.stroke([(L * .47, 3.4), (L * .46, 5.5)], DARK, .9)
    # 枪管
    if barrels == 1:
        w.paint(w.seg(ub0, L - 1.5, barrel_w, barrel_w * (1 + flare)), metal, ol=2.0, drop=0.4, shade=0.3, hl=0.3)
    else:
        for b in range(barrels):
            off = (b - (barrels - 1) / 2) * barrel_w * .9
            w.paint(w.poly([(ub0, off - barrel_w * .45), (L - 1.5, off - barrel_w * .45), (L - 1.5, off + barrel_w * .45), (ub0, off + barrel_w * .45)]),
                    metal, ol=1.6, drop=0.35, shade=0.3, hl=0.3)
    w.paint(w.seg(L - 3.5, L, barrel_w * (1 + flare) + 1.8), mul(metal, .85), ol=1.8, shade=0.25, hl=0.25)
    # 护木
    if stock and not pistol:
        w.paint(w.poly([(ub0, -.5), (ub0 + (L - ub0) * .55, -.5), (ub0 + (L - ub0) * .55, 3.0), (ub0, 3.6)]), wood, ol=1.8, shade=0.25, hl=0.15)
    if bands:
        for t in (.25, .55):
            u = ub0 + (L - ub0) * t
            w.paint(w.seg(u - .9, u + .9, barrel_w + 2.2), bands, ol=1.2, shade=0.2, hl=0.25)
    # 击锤
    w.paint(w.poly([(L * .37, -3.6), (L * .33, -6.5), (L * .36, -7), (L * .41, -3.6)]), DARK, ol=1.4, shade=0.2, hl=0.2)
    if drum:
        w.paint(w.ell(L * .5, 0, 4.2), mul(metal, 1.1), ol=1.8, shade=0.3, hl=0.3)
        for k in range(4):
            a = k * math.pi / 2
            w.paint(w.ell(L * .5 + math.cos(a) * 2.2, math.sin(a) * 2.2, .9), DARK, ol=0, shade=0, hl=0)
    if rune:
        runes(w, L * .38, ub0 - 1, -.2, rune, n=2, size=1.2)
        runes(w, ub0 + 2, L - 6, -.2, rune, n=3, size=1.0)


# ================= 投掷物 / 瓶子（屏幕坐标，128 画布） =================
def flask(c, x, y, r, glass='#c9d6d8', liquid='#5f9fd0', neck=7, cork='#8a6a48', label=None, glow=None, shape='round'):
    if shape == 'round':
        b = c.ell(x, y, r, r)
    elif shape == 'tall':
        b = c.rect(x - r * .62, y - r * 1.1, x + r * .62, y + r, r * .35)
    else:  # conical
        b = c.poly([(x - r * .3, y - r * 1.1), (x + r * .3, y - r * 1.1), (x + r, y + r * .9), (x - r, y + r * .9)])
    top = y - (r if shape == 'round' else r * 1.1)
    nk = c.rect(x - r * .28, top - neck, x + r * .28, top + 2, 1.5)
    c.paint(nk, glass, ol=2.2, drop=0.4, shade=0.15, hl=0.1, grain=0)
    c.paint(c.rect(x - r * .38, top - neck - 1, x + r * .38, top - neck + 2.2, 1.2), mul(glass, .9), ol=1.6, shade=0.15, hl=0.2, grain=0)
    c.paint(b, glass, ol=2.4, drop=0.45, shade=0.15, hl=0.0, grain=0)
    liq = b & c.rect(x - r * 1.2, y - r * .2, x + r * 1.2, y + r * 1.2)
    if glow:
        c.glow(liq, liquid, 7, 0.35)
    c.paint(liq, liquid, ol=0, shade=0.35, hl=0.0, grain=0.02)
    c.fill(c.ell(x, y - r * .2, r * .85, r * .12) & b, mix(liquid, '#ffffff', .35), 0.8)
    c.paint(c.rect(x - r * .34, top - neck - 6, x + r * .34, top - neck + .5, 1.5), cork, ol=1.8, shade=0.25, hl=0.15)
    c.fill(c.ell(x - r * .42, y - r * .38, r * .14, r * .32), '#ffffff', 0.75)
    if label:
        lb = c.rect(x - r * .55, y + r * .05, x + r * .55, y + r * .55, 1)
        c.paint(lb, label, ol=1.2, shade=0.15, hl=0.1)
    return b


def sack(c, x, y, r, col='#a08660', tie='#6b4a30', dust=None):
    body = c.poly([(x - r * .55, y - r * .6), (x + r * .55, y - r * .6), (x + r * 1.0, y + r * .2), (x + r * .8, y + r * .85), (x, y + r),
                   (x - r * .8, y + r * .85), (x - r * 1.0, y + r * .2)])
    c.paint(body, col, ol=2.4, drop=0.45, shade=0.3, hl=0.15, grain=0.05)
    for k in (-1, 0, 1):
        c.stroke([(x + k * r * .3, y - r * .5), (x + k * r * .45, y + r * .6)], mul(col, .78), .9, .8)
    top = c.poly([(x - r * .45, y - r * .62), (x - r * .7, y - r * 1.05), (x - r * .2, y - r * .9), (x, y - r * 1.15), (x + r * .25, y - r * .9),
                  (x + r * .7, y - r * 1.05), (x + r * .45, y - r * .62)])
    c.paint(top, mul(col, .95), ol=2.0, shade=0.25, hl=0.15)
    c.paint(c.rect(x - r * .55, y - r * .7, x + r * .55, y - r * .52, 1), tie, ol=1.5, shade=0.2, hl=0.15)
    if dust:
        for i in range(7):
            a = i * 0.9
            px, py = x + math.cos(a) * r * (0.5 + .1 * i), y - r * 1.1 - (i % 3) * 3
            m = c.ell(px, py, 1.4 + (i % 2))
            c.glow(m, dust, 3, 0.5)
            c.paint(m, mix(dust, '#ffffff', .4), ol=0, shade=0, hl=0, grain=0)


# ================= 各武器 =================
def sword_std(w, L, blade_col=STEEL, guard_col=GOLD, grip_col=LEATHER, pommel_col=None, gem=None, wd=None, guard_span=None, curl=0, fuller=True,
              tip=None, wd_tip=None):
    wd = wd or max(5.0, L * .06)
    gu = L * .22
    grip(w, L * .05, gu, wd * .62, grip_col)
    pommel(w, L * .04, wd * .62, pommel_col or guard_col, gem)
    m = blade(w, gu, L, wd, blade_col, tip=tip, fuller=fuller, wd_tip=wd_tip)
    guard(w, gu, guard_span or wd * 1.5, wd * .55, guard_col, curl=curl, gem=gem)
    return m


def staff_std(w, L, wood=WOOD, top=None, bands=GOLD):
    haft(w, 0, L * .8, max(3.2, L * .032), wood, bands=[(L * .08, 2.2, bands), (L * .5, 1.6, bands), (L * .76, 2.4, bands)])
    if top:
        top(w, L)


def W_SkySpear(w, L):
    haft(w, 0, L * .78, 3.6, '#e8e2d2', bands=[(L * .05, 2.5, GOLD), (L * .4, 2, GOLD)])
    for s in (-1, 1):
        w.paint(w.poly([(L * .74, s * 1.5), (L * .66, s * 7), (L * .78, s * 3), (L * .8, s * 1)]), GOLD, ol=1.6, shade=0.25, hl=0.25)
    m = spear_head(w, L * .79, L * .21, 6.4, SILVER, socket=GOLD)
    w.glow(m, '#fff3c0', 5, 0.3)
    gem_at(w, L * .77, 0, 2.0, '#9fd8ff')


def W_Godsbane(w, L):
    wd = L * .08
    grip(w, L * .06, L * .22, wd * .55, '#2e2426')
    pommel(w, L * .05, wd * .55, '#4a3b3e', '#c0392b')
    m = blade(w, L * .22, L, wd, '#4c4a52', tip=wd * 1.6, wd_tip=wd * .85)
    runes(w, L * .3, L * .8, 0, RUNE_R, n=5, size=1.5)
    for s in (-1, 1):
        w.paint(w.poly([(L * .2, s * 1), (L * .26, s * wd * 1.3), (L * .18, s * wd * .9)]), '#4a3b3e', ol=1.8, shade=0.25, hl=0.2)
    w.paint(w.seg(L * .19, L * .25, wd * 1.4), '#4a3b3e', ol=1.8, shade=0.25, hl=0.2)


def W_RunicMusket(w, L):
    gun(w, L, rune=RUNE_B, barrel_len=.58, barrel_w=3.0, flare=.25)


def W_SilverPistol(w, L):
    gun(w, L, wood='#6a4a36', metal='#c4c8ce', pistol=True, barrel_len=.5, barrel_w=3.4, bands=GOLD, rune='#f4f0d8')


def W_LavaPiercer(w, L):
    gun(w, L, wood='#4a3a32', metal='#4a4446', barrel_len=.55, barrel_w=3.6, bands='#c45a2a')
    u0 = L * .45
    m = w.poly([(u0, -.6), (L - 2, -.6), (L - 2, .6), (u0, .6)])
    w.glow(m, FIRE, 4, 0.6)
    w.stroke([(u0, 0), (L - 3, 0)], FIRE_C, .8)


def W_ThunderGun(w, L):
    gun(w, L, metal=BRONZE, barrel_len=.5, barrel_w=4, flare=.6, bands='#4f7fa0')
    lightning(w, [(L * .55, -4.5), (L * .62, -2.5), (L * .68, -5), (L * .76, -2.6), (L * .84, -5.2)], RUNE_B)


def W_Doom(w, L):
    gun(w, L, wood='#3a3236', metal='#3f4246', barrel_len=.6, barrel_w=2.6, bands='#5a8a5a', rune=RUNE_G)
    # 瞄准镜
    w.paint(w.poly([(L * .4, -5), (L * .6, -5), (L * .6, -8), (L * .4, -8)]), '#2e3034', ol=1.6, shade=0.2, hl=0.3)
    w.paint(w.ell(L * .61, -6.5, 1.8), RUNE_G, ol=1.0, shade=0, hl=0.3)


def W_Skyfire(w, L):
    gun(w, L, wood='#7a4a36', metal='#8c6a52', barrel_len=.55, barrel_w=3.4, flare=.3, bands=GOLD, rune=RUNE_R)
    orb_at(w, L * .44, -5.5, 2.2, '#ff8a4a')


def W_RunicPistol(w, L):
    gun(w, L, pistol=True, barrel_len=.5, barrel_w=3.4, rune=RUNE_B)


def W_HammerCannon(w, L):
    gun(w, L, metal='#6f6a64', barrel_len=.5, barrel_w=6.5, flare=.15, bands=GOLD, pistol=True)
    hammer_head(w, L * .93, 12, 6, '#7a756e')
    runes(w, L * .6, L * .85, 0, RUNE_B, n=2, size=1.4)


def W_RunicRepeater(w, L):
    gun(w, L, barrel_len=.55, barrel_w=2.4, barrels=3, rune=RUNE_B, drum=True)


def W_MountainWrath(w, L):
    gun(w, L, wood='#5a5048', metal='#5c6a5a', barrel_len=.55, barrel_w=5.0, flare=.2, bands='#a08850', rune=RUNE_G)


def staff_crystal(col, shape='crystal'):
    def top(w, L):
        u = L * .86
        for s in (-1, 1):
            w.paint(w.poly([(L * .76, s * 1.5), (L * .86, s * 5.5), (L * .9, s * 5), (L * .82, s * 1.2)]), GOLD_D, ol=1.4, shade=0.2, hl=0.25)
        if shape == 'crystal':
            m = w.poly([(L * .78, 0), (L * .86, -3.6), (L, 0), (L * .86, 3.6)])
            w.glow(m, col, 6, 0.6)
            w.paint(m, col, ol=1.8, shade=0.3, hl=0.0, grain=0)
            w.c.fill(w.poly([(L * .8, 0), (L * .86, -3), (L * .96, 0)]), mix(col, '#ffffff', .6), 0.8)
        elif shape == 'flame':
            orb_at(w, L * .88, 0, 3.6, col)
            flame_lick(w, L * .8, L, -1, 3, FIRE, h=5)
        else:
            orb_at(w, L * .88, 0, 3.8, col)
    return top


def W_MissileStaff(w, L):
    staff_std(w, L, top=staff_crystal('#b48ae8'))


def W_FireStaff(w, L):
    staff_std(w, L, '#6a3e2e', top=staff_crystal('#ff8a3a', 'flame'), bands=BRONZE)


def W_FrostStaff(w, L):
    staff_std(w, L, '#8a9aa8', top=staff_crystal(ICE, 'crystal'), bands=SILVER)


def W_Legacy_Staff(w, L):
    haft(w, 0, L * .82, 3.6, '#4e5a7a', bands=[(L * .1, 2.4, SILVER), (L * .55, 2, SILVER)])
    u = L * .88
    m = w.poly([(u + 7 * math.cos(a) * (1 if i % 2 == 0 else .45), 7 * math.sin(a) * (1 if i % 2 == 0 else .45))
                for i, a in enumerate([j * math.pi / 5 for j in range(10)])])
    w.glow(m, '#c9b8ff', 7, 0.55)
    w.paint(m, '#d8ccff', ol=1.8, shade=0.25, hl=0.0, grain=0)
    w.c.paint(w.ell(u, 0, 1.6), '#ffffff', ol=0, shade=0, hl=0, grain=0)


def W_SleepStaff(w, L):
    haft(w, 0, L * .82, 3.4, '#5a5070', bands=[(L * .1, 2.2, SILVER), (L * .6, 2, SILVER)])
    u = L * .9
    moon = w.ell(u, 0, 7) & ~w.ell(u + 3.5, -2.0, 6)
    w.glow(moon, '#c8b8f0', 6, 0.5)
    w.paint(moon, '#e2dcc4', ol=1.8, shade=0.25, hl=0.2, grain=0)
    gem_at(w, u - 2, 2.5, 1.8, '#a88ae0')


def W_Relic_MorningStar(w, L):
    haft(w, 0, L * .7, 4.0, WOOD, bands=[(L * .05, 3, GOLD), (L * .66, 3, GOLD)])
    grip(w, L * .02, L * .2, 4.6, LEATHER)
    m = spike_ball(w, L * .82, L * .1, GOLD, n=10, spike=L * .07, spike_col='#e8cf80')
    w.glow(m, '#fff0b0', 6, 0.35)
    w.paint(w.ell(L * .82, 0, L * .04), '#fff3c8', ol=1.0, shade=0.1, hl=0.2, grain=0)


def W_Relic_StormSpear(w, L):
    haft(w, 0, L * .76, 3.6, '#5a6a7a', bands=[(L * .1, 2, SILVER), (L * .45, 2, SILVER)])
    spear_head(w, L * .76, L * .24, 7.4, '#b9c6d2', socket=SILVER, barbs=True)
    lightning(w, [(L * .6, -5), (L * .66, -2.5), (L * .7, -5.5), (L * .76, -3)], RUNE_B)
    w.paint(w.poly([(L * .72, 1), (L * .62, 7), (L * .66, 9), (L * .74, 2)]), '#3f6f9f', ol=1.4, shade=0.2, hl=0.15)


def maul(w, L, head=DARK, wood=WOOD_D, crack=FIRE, size=1.0, bands=BRONZE):
    haft(w, 0, L * .82, 5.0 * size, wood, bands=[(L * .05, 3.2, bands), (L * .45, 2.6, bands)])
    grip(w, L * .06, L * .26, 5.6 * size, LEATHER_D)
    m = hammer_head(w, L * .84, L * .36, L * .2, head, cap=mul(head, .75))
    if crack:
        u = L * .84
        pts = [(u - L * .06, -L * .13), (u - L * .01, -L * .06), (u - L * .05, 0), (u + L * .02, L * .07), (u - L * .02, L * .14)]
        mm = w.stroke(pts, crack, 1.2)
        w.glow(mm, crack, 5, 0.55)
        w.stroke(pts, FIRE_C, .5)
    return m


def W_FireGiantMaul(w, L):
    maul(w, L, '#4a4442', '#4a3a30', FIRE)


def W_MoltenMaul(w, L):
    maul(w, L, '#3e3634', '#3a2e28', '#ff6a2a', bands='#c45a2a')
    orb_at(w, L * .84, 0, L * .045, '#ffb04a')


def W_CyclopsMaul(w, L):
    haft(w, 0, L * .7, 6.0, mul(STUFFG, .82), bands=[(L * .08, 3.5, mul(STUFFG, .7))])
    m = w.poly([(L * .62, -8), (L * .74, -12), (L * .95, -11), (L, -4), (L, 5), (L * .9, 12), (L * .68, 11), (L * .6, 6)])
    w.paint(m, STUFFG, ol=2.6, drop=0.45, shade=0.32, hl=0.18, grain=0.06)
    for k in range(3):
        w.stroke([(L * (.66 + k * .1), -10), (L * (.68 + k * .1), 10)], mul(STUFFG, .7), 1.2)


def W_SilverSword(w, L):
    sword_std(w, L, blade_col='#d8dde4', guard_col='#9aa2ac', grip_col='#3f4a5a', gem='#b48ae8', wd=L * .05, guard_span=L * .07, curl=2.0)


def W_Legacy_Sword(w, L):
    sword_std(w, L, guard_col=GOLD, gem='#ffd36a', curl=1.5)
    w.glow(w.seg(L * .25, L * .95, L * .02), '#fff0c0', 4, 0.2)


def W_Legacy_Hammer(w, L):
    haft(w, 0, L * .8, 4.4, WOOD, bands=[(L * .08, 3, BRONZE), (L * .5, 2.4, BRONZE)])
    grip(w, L * .04, L * .24, 5.0, LEATHER)
    hammer_head(w, L * .84, L * .34, L * .2, '#8a8a86', spike=4, cap=BRONZE)
    runes(w, L * .8, L * .88, 0, RUNE_O, n=1, size=2.6)


def W_Legacy_Bow(w, L):
    bow(w, L, '#6e8a5a', limb=3.6, grip_col=LEATHER, tips=GOLD)
    for k in range(3):
        u = L * (.62 + k * .05)
        w.paint(w.poly([(u, L * .2 + 1), (u + 3, L * .2 + 5), (u + 5, L * .2 + 1)]), '#b8d8a0', ol=1.0, shade=0.15, hl=0.1)


def W_ElvenLongbow(w, L):
    bow(w, L, '#c8b080', limb=3.0, bulge=L * .15, grip_col='#5a7a4a', tips=SILVER)
    for t in (.3, .7):
        w.paint(w.poly([(L * t - 3, L * .13 * math.sin(math.pi * t) / .15 * .15 + 2), (L * t, L * .15 * math.sin(math.pi * t) + 6), (L * t + 3, L * .15 * math.sin(math.pi * t) + 2)]),
                '#7fa86a', ol=1.0, shade=0.15, hl=0.1)


def W_FeySilverBow(w, L):
    bow(w, L, '#c8ced6', limb=3.2, grip_col='#4f7a5a', tips='#a8e0b0')
    for t in (.2, .8):
        b = L * .2 * math.sin(math.pi * t)
        m = w.poly([(L * t - 3, b + 2), (L * t, b + 7), (L * t + 3, b + 2)])
        w.paint(m, '#8fcf8a', ol=1.0, shade=0.15, hl=0.1)
        w.glow(m, '#c8ffd0', 3, 0.3)


def W_SleepBow(w, L):
    bow(w, L, mul(STUFFG, .9), limb=3.4, grip_col=mul(STUFFG, .6), tips=mul(STUFFG, .7))


def W_FrostGiantAxe(w, L):
    haft(w, 0, L * .8, 7.0, mul(STUFFG, .78), bands=[(L * .08, 3, mul(STUFFG, .62)), (L * .5, 2.6, mul(STUFFG, .62))])
    axe_head(w, L * .72, L * .3, STUFFG, double=True)
    w.paint(w.poly([(L * .78, -2), (L, 0), (L * .78, 2)]), STUFFG, ol=1.8, shade=0.25, hl=0.2)


def W_OrcishGreataxe(w, L):
    haft(w, 0, L * .86, 6.6, mul(STUFFG, .78), bands=[(L * .1, 3, mul(STUFFG, .55))])
    axe_head(w, L * .74, L * .38, STUFFG, crude=True)
    for k in range(2):
        w.paint(w.ell(L * .78 - 2 + k * 4, -2, 1.0), mul(STUFFG, .5), ol=0, shade=0, hl=0)


def W_DwarvenThrowingHammer(w, L):
    haft(w, 0, L * .72, 4.4, mul(STUFFG, .78), bands=[(L * .1, 3, mul(STUFFG, .6))])
    grip(w, L * .04, L * .3, 5.0, mul(STUFFG, .6))
    hammer_head(w, L * .8, L * .42, L * .26, STUFFG, cap=mul(STUFFG, .82))
    runes(w, L * .76, L * .84, 0, mul(STUFFG, .55), n=1, size=2.4, glow=False)


def W_OgreClub(w, L):
    m = w.poly([(0, -3), (L * .3, -4), (L * .7, -7), (L * .95, -8), (L, -3), (L, 4), (L * .9, 8), (L * .6, 6.5), (L * .3, 4), (0, 3)])
    w.paint(m, mul(STUFFG, .85), ol=2.4, drop=0.45, shade=0.3, hl=0.15, grain=0.06)
    for k in range(5):
        u = L * (.6 + k * .08)
        s = -1 if k % 2 else 1
        w.paint(w.poly([(u - 1.5, s * 5), (u, s * 10), (u + 1.5, s * 5)]), mul(STUFFG, .6), ol=1.4, shade=0.2, hl=0.2)
    grip(w, L * .02, L * .2, 6.4, mul(STUFFG, .55))


def W_SubduingCudgel(w, L):
    m = w.seg(L * .2, L, 5.0, 7.0)
    w.paint(m, mul(STUFFG, .85), ol=2.2, drop=0.45, shade=0.3, hl=0.18)
    grip(w, 0, L * .24, 4.6, mul(STUFFG, .55))
    for u in (L * .5, L * .7, L * .9):
        w.paint(w.seg(u - 1.5, u + 1.5, 8.0), mul(STUFFG, .6), ol=1.4, shade=0.2, hl=0.2)


def W_FrostSpear(w, L):
    haft(w, 0, L * .74, 3.2, '#7a8a98', bands=[(L * .1, 2, SILVER), (L * .5, 2, SILVER)])
    m = w.poly([(L * .72, -2), (L * .8, -5.5), (L, 0), (L * .8, 5.5), (L * .72, 2)])
    w.glow(m, ICE_L, 5, 0.4)
    w.paint(m, ICE, ol=2.0, shade=0.25, hl=0.0, grain=0)
    w.c.fill(w.poly([(L * .74, -1.5), (L * .8, -4.5), (L * .98, 0), (L * .8, 0)]), ICE_L, 0.8)
    for k in range(3):
        u = L * (.6 + k * .05)
        w.paint(w.poly([(u, -1.6), (u + 3, -4.5), (u + 4, -1.6)]), ICE_L, ol=1.0, shade=0.15, hl=0.1)


def W_BalorWhip(w, L):
    grip(w, 0, L * .2, 5.0, '#3a2a26')
    pommel(w, L * .01, 3.2, '#4a3a36')
    pts = [(L * .2 + (L * .8) * t / 20, 6 * math.sin(t * .55) * (t / 20)) for t in range(21)]
    m = w.c.line_mask([w.xy(*p) for p in pts], 3.0)
    w.glow(m, FIRE, 6, 0.6)
    w.paint(m, '#5a2a22', ol=1.8, shade=0.2, hl=0.15)
    w.stroke(pts, FIRE, 1.0)
    for k in range(5):
        u, v = pts[4 + k * 3]
        f = w.poly([(u - 2, v), (u, v - 5 - (k % 2) * 2), (u + 2, v)])
        w.paint(f, FIRE, ol=1.0, shade=0.1, hl=0, grain=0)


def W_EmpyreanBlade(w, L):
    m = sword_std(w, L, blade_col='#e6e2d4', guard_col=GOLD, grip_col='#e0d6b8', gem='#ffe08a', wd=L * .07, guard_span=L * .1, curl=2.5)
    w.glow(m, '#fff3c0', 6, 0.35)
    for s in (-1, 1):
        w.paint(w.poly([(L * .2, s * 3), (L * .14, s * 11), (L * .24, s * 5)]), '#f4efe0', ol=1.4, shade=0.2, hl=0.2)


def W_FlameTongue(w, L):
    m = sword_std(w, L, blade_col='#c8b0a0', guard_col='#8a4a2a', grip_col='#3a2a26', gem='#ff7a3a')
    flame_lick(w, L * .3, L * .92, -1, 5, FIRE, h=5.5)
    flame_lick(w, L * .35, L * .85, 1, 4, FIRE, h=4)


def W_FrostBrand(w, L):
    m = sword_std(w, L, blade_col='#c8e0f0', guard_col='#8aa8c0', grip_col='#3a4a5a', gem=ICE)
    w.glow(m, ICE_L, 5, 0.35)
    for k in range(4):
        u = L * (.35 + k * .14)
        w.paint(w.poly([(u, -2.4), (u + 2, -5.5), (u + 3.5, -2.4)]), ICE_L, ol=1.0, shade=0.15, hl=0.1)


def W_VorpalSword(w, L):
    m = sword_std(w, L, blade_col='#dce0e6', guard_col='#5a5e66', grip_col='#2e2a36', wd=L * .05, guard_span=L * .08, tip=L * .12)
    star = w.poly([(L * .8, -5), (L * .81, -1.2), (L * .85, -.4), (L * .81, .4), (L * .8, 4), (L * .79, .4), (L * .75, -.4), (L * .79, -1.2)])
    w.c.fill(star, '#ffffff', 0.9)
    w.glow(star, '#ffffff', 4, 0.5)


def W_DragonSlayer(w, L):
    wd = L * .085
    grip(w, L * .05, L * .22, wd * .55, '#4a2a22')
    pommel(w, L * .04, wd * .55, '#8a3a2a', '#d84a2a')
    m = blade(w, L * .22, L, wd, STEEL, tip=wd * 1.5)
    for s in (-1, 1):
        w.paint(w.poly([(L * .25, s * 1), (L * .2, s * wd * 1.4), (L * .16, s * wd * 1.1), (L * .19, s * wd * .5)]), '#8a3a2a', ol=1.8, shade=0.25,
                hl=0.2)
    w.paint(w.seg(L * .19, L * .25, wd * 1.1), '#8a3a2a', ol=1.8, shade=0.25, hl=0.2)
    runes(w, L * .3, L * .75, 0, RUNE_O, n=4, size=1.4)


def W_DefenderSword(w, L):
    sword_std(w, L, guard_col=SILVER, gem='#5a8ad0')
    u = L * .22
    w.paint(w.poly([(u - 3, -6), (u + 4, -6), (u + 4, 6), (u - 3, 6), (u - 6, 0)]), '#5a7aa8', ol=1.8, shade=0.25, hl=0.2)
    w.paint(w.ell(u, 0, 2), SILVER, ol=1.0, shade=0.2, hl=0.3)


def W_JavelinLightning(w, L):
    haft(w, 0, L * .78, 3.0, '#6a7a8a', bands=[(L * .3, 2, SILVER), (L * .6, 2, SILVER)])
    spear_head(w, L * .76, L * .24, 5.2, '#c8d4de', socket=SILVER)
    lightning(w, [(L * .3, -4), (L * .4, -1.5), (L * .48, -4.5), (L * .58, -1.8), (L * .66, -4.4), (L * .76, -2)], RUNE_B)


def W_SwordVengeance(w, L):
    m = sword_std(w, L, blade_col='#6a6066', guard_col='#3a3036', grip_col='#2a2024', gem='#c0392b', curl=2)
    w.glow(m, '#c0392b', 4, 0.25)
    runes(w, L * .32, L * .8, 0, RUNE_R, n=4, size=1.3)


def W_StormTrident(w, L):
    haft(w, 0, L * .7, 2.2, '#4a5a68', bands=[(L * .1, 1.4, BRONZE), (L * .5, 1.4, BRONZE)])
    u = L * .7
    w.paint(w.poly([(u - 2, -8), (u + 3, -8), (u + 3, 8), (u - 2, 8)]), BRONZE, ol=2.0, shade=0.25, hl=0.2)
    for v in (-6.5, 0, 6.5):
        tip = L if v == 0 else L - 5
        m = w.poly([(u + 2, v - 1.0), (tip - 8, v - .8), (tip - 9, v - 2.0), (tip, v), (tip - 9, v + 2.0), (tip - 8, v + .8), (u + 2, v + 1.0)])
        w.paint(m, BRONZE, ol=1.8, drop=0.4, shade=0.25, hl=0.2)
        w.stroke([(tip - 5, v - .8), (tip - 1, v)], mix(BRONZE, '#ffffff', .4), .6)
    lightning(w, [(L * .55, 4), (L * .62, 1.5), (L * .66, 5), (L * .72, 2.5)], RUNE_B)


def W_CloudMorningstar(w, L):
    haft(w, 0, L * .7, 4.4, '#8a8a90', bands=[(L * .08, 3, SILVER), (L * .66, 3, SILVER)])
    grip(w, L * .02, L * .22, 5.0, '#4a5262')
    spike_ball(w, L * .84, L * .1, '#d8dce4', n=9, spike=L * .06)
    w.paint(w.ell(L * .84, 0, L * .04), '#9fc8e8', ol=1.0, shade=0.2, hl=0.3, grain=0)


def W_InfernalPitchfork(w, L):
    haft(w, 0, L * .7, 3.6, '#3a3030', bands=[(L * .1, 2.4, '#7a2a22'), (L * .5, 2, '#7a2a22')])
    u = L * .7
    w.paint(w.poly([(u - 2, -8), (u + 3, -8), (u + 3, 8), (u - 2, 8)]), IRON, ol=2.0, shade=0.25, hl=0.2)
    for v in (-6.5, 0, 6.5):
        m = w.poly([(u + 2, v - 1.4), (L - 1, v - .3), (L, v), (L - 1, v + .3), (u + 2, v + 1.4)])
        w.paint(m, IRON, ol=1.8, shade=0.25, hl=0.2)
        w.glow(w.ell(L - 2, v, 1.2), RUNE_R, 3, 0.5)


def W_NetCrossbow(w, L):
    w.paint(w.poly([(0, -2), (L * .7, -2.5), (L * .7, 2.5), (L * .2, 3.5), (L * .05, 7), (0, 6.5)]), WOOD, ol=2.2, drop=0.45, shade=0.3, hl=0.18)
    w.paint(w.seg(L * .3, L, 3.4), WOOD_D, ol=2.0, shade=0.3, hl=0.2)
    u = L * .85
    pts_o = [(u - 6 * math.sin(t / 10 * math.pi) * 0 - 3 * abs(math.sin(t / 10 * math.pi)), -L * .32 + L * .64 * t / 10) for t in range(11)]
    m = w.c.line_mask([w.xy(*p) for p in pts_o], 3.2)
    w.paint(m, IRON, ol=1.8, shade=0.25, hl=0.25)
    w.stroke([pts_o[0], (L * .52, 0), pts_o[-1]], '#e6dcc4', .8)
    # 网包
    nb = w.ell(L * .95, 0, 5.2, 4.6)
    w.paint(nb, '#c8b890', ol=2.0, shade=0.3, hl=0.15)
    for k in (-3, 0, 3):
        w.stroke([(L * .95 + k, -4.5), (L * .95 - k, 4.5)], '#8a7a5a', .7)


# ---- 投掷物、瓶子（屏幕坐标） ----
def T(c, size, z=1.22):
    k = size / 128
    c.tf = lambda x, y: (((x - 64) * z + 64) * k, ((y - 68) * z + 66) * k)
    c.zs = k * z


def P_BlessedWater(c):
    flask(c, 64, 80, 30, liquid='#8ac8f0', glow='#c8ecff', neck=12)
    c.paint(c.poly([(60, 64), (68, 64), (68, 72), (76, 72), (76, 80), (68, 80), (68, 96), (60, 96), (60, 80), (52, 80), (52, 72), (60, 72)]), GOLD,
            ol=2.0, shade=0.2, hl=0.25)


def P_HolyWater(c):
    flask(c, 64, 80, 28, shape='tall', liquid='#9ad0f0', glow='#d0f0ff', neck=10)
    c.paint(c.poly([(61, 66), (67, 66), (67, 74), (74, 74), (74, 80), (67, 80), (67, 96), (61, 96), (61, 80), (54, 80), (54, 74), (61, 74)]), GOLD,
            ol=1.8, shade=0.2, hl=0.25)


def P_AcidVial(c):
    flask(c, 64, 82, 26, shape='conical', liquid='#8fd04a', glow='#c8ff8a', neck=14)
    for i, (x, y) in enumerate([(56, 40), (70, 34), (62, 26)]):
        m = c.ell(x, y, 3 - i * .5)
        c.paint(m, '#a8e070', ol=1.2, shade=0.2, hl=0.3, grain=0)


def P_AlchemistFire(c):
    flask(c, 64, 82, 28, liquid='#e8702a', glow='#ffb060', neck=10, cork='#c8b89a')
    rag = c.poly([(58, 30), (70, 30), (74, 18), (66, 22), (62, 12), (58, 22)])
    c.paint(rag, '#d8cbb0', ol=1.6, shade=0.2, hl=0.1)
    f = c.poly([(64, 2), (71, 12), (68, 20), (60, 20), (57, 12)])
    c.glow(f, FIRE, 6, 0.5)
    c.paint(f, FIRE, ol=1.4, shade=0.1, hl=0, grain=0)
    c.paint(c.ell(64, 14, 2.6, 4), FIRE_C, ol=0, shade=0, hl=0, grain=0)


def P_FogFlask(c):
    flask(c, 64, 82, 30, glass='#d4dcdf', liquid='#c8d0d8', neck=10, cork='#6a5a48')
    for i, (x, y, r) in enumerate([(52, 76, 9), (66, 72, 10), (76, 82, 8), (60, 88, 9)]):
        c.fill(c.ell(x, y, r, r * .7), '#f4f6f8', 0.55)
    for i, (x, y, r) in enumerate([(58, 22, 6), (68, 16, 7), (76, 22, 5)]):
        m = c.ell(x, y, r, r * .75)
        c.paint(m, '#e8ecf0', ol=1.4, shade=0.15, hl=0.1, grain=0)


def P_EversmokingBottle(c):
    flask(c, 64, 84, 26, shape='tall', glass='#8a9a8e', liquid='#5a6a62', neck=14, cork='#5a4a3a')
    for i, (x, y, r) in enumerate([(62, 32, 7), (70, 22, 8), (60, 12, 7), (74, 8, 5)]):
        m = c.ell(x, y, r, r * .8)
        c.paint(m, mix('#c0c4c8', '#8a8e92', i / 4), ol=1.4, shade=0.2, hl=0.15, grain=0)


def P_Thunderstone(c):
    m = c.poly([(40, 60), (54, 38), (80, 36), (94, 56), (90, 86), (64, 100), (40, 88)])
    c.paint(m, '#7a7a80', ol=2.6, drop=0.45, shade=0.35, hl=0.2, grain=0.06)
    c.stroke([(50, 52), (60, 48)], '#9a9aa0', 1.2)
    pts = [(70, 46), (60, 66), (70, 66), (58, 90)]
    mm = c.stroke(pts, RUNE_B, 3.0)
    c.glow(mm, RUNE_B, 6, 0.6)
    c.stroke(pts, '#f0faff', 1.2)


def P_TanglefootBag(c):
    sack(c, 64, 74, 32, col='#9a8458')
    for x, y in [(46, 92), (58, 104), (78, 100), (86, 88)]:
        m = c.ell(x, y, 4, 3)
        c.paint(m, '#c8b860', ol=1.4, shade=0.2, hl=0.35, grain=0)
    c.paint(c.poly([(54, 104), (58, 116), (62, 104)]), '#c8b860', ol=1.2, shade=0.2, hl=0.3, grain=0)


def P_DispelDust(c):
    sack(c, 64, 78, 28, col='#7a6a9a', tie=GOLD, dust='#d8c8ff')


def P_SleepDustPouch(c):
    sack(c, 64, 80, 28, col='#5a5080', tie=SILVER, dust='#b8a8f0')
    moon = c.ell(64, 84, 7) & ~c.ell(68, 81, 6)
    c.paint(moon, '#e2dcc4', ol=1.2, shade=0.15, hl=0.2, grain=0)


def P_HellfireOrb(c):
    cage = None
    m = c.ell(64, 66, 34)
    c.glow(m, '#ff5a2a', 10, 0.45)
    c.paint(m, '#c8402a', ol=2.6, drop=0.4, shade=0.35, hl=0.0, grain=0)
    c.paint(c.ell(60, 60, 20), '#f07a3a', ol=0, shade=0.2, hl=0, grain=0)
    c.paint(c.ell(56, 54, 9), '#ffd27a', ol=0, shade=0, hl=0, grain=0)
    for a in (-60, 0, 60):
        c.stroke([(64 + 34 * math.sin(math.radians(a)) * math.cos(t / 10 * math.pi - math.pi / 2), 32 + t * 6.8) for t in range(11)], DARK, 2.4)
    c.paint(c.ell(64, 32, 6, 4), DARK, ol=1.6, shade=0.2, hl=0.25)
    for x, y in [(36, 98), (64, 104), (92, 98)]:
        c.paint(c.poly([(x - 5, y - 4), (x, y + 8), (x + 5, y - 4)]), DARK, ol=1.4, shade=0.2, hl=0.2)


AXIAL = {k[2:]: v for k, v in list(globals().items()) if k.startswith('W_')}
POINT = {k[2:]: v for k, v in list(globals().items()) if k.startswith('P_')}
# 垂直方向加粗倍数（旧图是粗短的卡通图标，原版武器也偏粗壮）
VS = {"FrostGiantAxe": 1.2, "OrcishGreataxe": 1.2, "StormTrident": 2.2, "InfernalPitchfork": 2.0, "Legacy_Bow": 1.15, "ElvenLongbow": 1.15, "FeySilverBow": 1.15, "SleepBow": 1.15, "BalorWhip": 1.3, "NetCrossbow": 1.5}
for _g in ("SilverPistol", "LavaPiercer", "ThunderGun", "Doom", "Skyfire", "RunicMusket", "RunicPistol", "HammerCannon", "RunicRepeater", "MountainWrath"):
    VS[_g] = 2.0
SHARED = {"LP_RuneTurretGun": "LP_RunicMusket"}


def main():
    rows = json.load(open(os.path.join(HERE, "weapons_axes.json"), encoding="utf8"))
    done = set()
    for r in rows:
        name = r["def_"][3:]
        if ONLY and r["def_"] not in ONLY:
            continue
        if r["def_"] in SHARED or r["path"] in done:
            continue
        done.add(r["path"])
        size = r["W"]
        c = Canvas(size, size, seed=hash(name) & 0xffff)
        if name in AXIAL:
            w = W(c, tuple(r["p0"]), tuple(r["p1"]), size / 128, VS.get(name, 1.75))
            AXIAL[name](w, w.L)
        elif name in POINT:
            T(c, size)
            POINT[name](c)
        else:
            print("no painter for", name)
            continue
        c.save(r["path"])
        print("ok", r["def_"], size)


if __name__ == "__main__":
    main()
