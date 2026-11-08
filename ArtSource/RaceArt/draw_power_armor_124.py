# 1.126.0 奥能动力甲贴图（2026-11-08 第四版：参考钢铁侠 × 战锤40K × 光环混合，原创造型）：6 件身甲 × 五体型 × 南/东/北（256px）+ 3 顶头盔 × 3 向 + _m 遮罩
# （红=甲色，绿=纹章色，黑=固定色）+ 图标；奥械锻炉（3×2，三向）、奥能充能座、奥能电池、4 个按钮图标（这些没变）。
# 预览：ArtSource/Preview/PowerArmor-1.126.0.png（全部）与 PowerArmor-1.126.0-views.png（上一版正面 / 新版正 / 侧 / 背）。
# 混合方式：40K = 大圆肩甲 + 宽辅色滚边、胯甲、纹章布帘、堡垒的塔盾肩甲；光环 = 整件深色内衬在关节和甲片缝里露出来、侍从盔的琥珀反光面罩；
#   钢铁侠 = 流线甲片、发光胸核、双色辅甲条、分节腹甲。头盔面甲、徽记都是原创（V 形目镜、展翼太阳、八芒旭日、齿轮、符文）。
# 无缝接缝：甲片用 soft()（只有体积明暗，不画黑线），片与片之间只露内衬或画同色加深的细缝（seam），黑描边只在整件外轮廓（finish）。
# 上一版（第三版，流线分节甲）备份在 ArtSource/backup-1.126.0-powerarmor-art3。
# 渲染器用 ArtRedraw-1030/apparel.py（原版画风：粗深描边、上亮下暗、左上高光、板甲纹理），身体框沿用 draw_apparel_145.py。
# 用法（要 numpy / scipy）：$env:PATH="E:\Python;E:\Python\Library\bin;"+$env:PATH; E:\Python\python.exe -I ArtSource/RaceArt/draw_power_armor_124.py <模组根目录>
import importlib.util, math, os, sys
from PIL import Image, ImageDraw, ImageFont

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = sys.argv[1] if len(sys.argv) > 1 else os.getcwd()
spec = importlib.util.spec_from_file_location("redraw1030", os.path.join(ROOT, "ArtSource", "ArtRedraw-1030", "apparel.py"))
R = importlib.util.module_from_spec(spec)
sys.argv = [sys.argv[0], ROOT]
spec.loader.exec_module(R)
A = R.ap

A.TEX = os.path.join(ROOT, "Textures")
R.TEX = A.TEX
PREVIEW = os.path.join(ROOT, "ArtSource", "Preview", "PowerArmor-1.126.0.png")
VIEWS_PNG = os.path.join(ROOT, "ArtSource", "Preview", "PowerArmor-1.126.0-views.png")
OLD = os.path.join(ROOT, "ArtSource", "backup-1.126.0-powerarmor-art3", "Textures")
Fit, Head, poly, ell, rect = A.Fit, A.Head, A.poly, A.ell, A.rect
FlatCanvas = A.Canvas
LT, MD, DK = A.LT, (198, 198, 196), (158, 158, 156)
STUFF, HERALD, FIXED = A.STUFF, A.HERALD, A.FIXED
S = A.S

BRASS = (176, 146, 88)
BRASS_DK = (128, 104, 62)
GOLD = (206, 172, 88)
GOLD_DK = (150, 122, 60)
GLOW = (126, 206, 236)
GLOW_LT = (214, 244, 252)
RUNE = (170, 150, 236)
DARKSTEEL = (84, 88, 96)
IRON = (62, 64, 70)
BLACK = (40, 38, 38)
BONE = (222, 212, 186)
BONE_DK = (176, 164, 136)
PARCH = (212, 194, 150)
WAX = (150, 50, 42)
ROBE = (92, 70, 50)
ROBE_DK = (68, 52, 38)
LEATHER = (104, 78, 52)


def Canvas(view="south"):
    return R.Canvas(view, None)   # 截图式干净平涂：不叠板甲纹理


def sc(col, k):
    return tuple(max(0, min(255, int(v * k))) for v in col)


# ---------------- 小工具 ----------------
def clip(pts, x0, y0, x1, y1):
    """Sutherland–Hodgman：多边形裁到矩形里。"""
    def cut(ps, inside, inter):
        out = []
        for i in range(len(ps)):
            a, b = ps[i - 1], ps[i]
            ia, ib = inside(a), inside(b)
            if ib:
                if not ia:
                    out.append(inter(a, b))
                out.append(b)
            elif ia:
                out.append(inter(a, b))
        return out

    def ix(xc):
        return lambda a, b: (xc, a[1] + (b[1] - a[1]) * (xc - a[0]) / ((b[0] - a[0]) or 1e-6))

    def iy(yc):
        return lambda a, b: (a[0] + (b[0] - a[0]) * (yc - a[1]) / ((b[1] - a[1]) or 1e-6), yc)

    for inside, inter in ((lambda p: p[0] >= x0, ix(x0)), (lambda p: p[0] <= x1, ix(x1)), (lambda p: p[1] >= y0, iy(y0)), (lambda p: p[1] <= y1, iy(y1))):
        pts = cut(pts, inside, inter)
        if not pts:
            return []
    return pts


def rivets(c, pts, r=.75, col=BRASS):
    for x, y in pts:
        c.add(ell((x - r, y - r, x + r, y + r)), col, FIXED, .35)


def rivet_row(c, x0, y, x1, n, r=.7, col=BRASS):
    rivets(c, [(x0 + (x1 - x0) * i / max(1, n - 1), y) for i in range(n)], r, col)


def scratches(c, f, seed, n=7, x_span=.4, t0=.05, t1=.9):
    import random
    rnd = random.Random(seed)
    for _ in range(n):
        x = f.cx + rnd.uniform(-x_span, x_span) * f.w
        y = f.y(rnd.uniform(t0, t1))
        dx, dy = rnd.uniform(-2.2, 2.2), rnd.uniform(-.6, 1.2)
        c.add(("line", [(x, y), (x + dx, y + dy)], .45), (70, 70, 72), STUFF, 0)
        if rnd.random() < .4:
            c.add(ell((x - .6, y + 1.2, x + .6, y + 2.2)), (90, 90, 94), STUFF, 0)  # 凹坑


def hazard(c, x0, y0, x1, y1, n=4):
    """警示斜纹板：纹章色底 + 黑色斜条。"""
    c.add(rect((x0, y0, x1, y1)), LT, HERALD, .8)
    step = (x1 - x0) / n
    for i in range(-2, n + 2):
        xa = x0 + i * step
        p = clip([(xa, y1), (xa + step * .5, y1), (xa + step * .5 + (y1 - y0) * .8, y0), (xa + (y1 - y0) * .8, y0)], x0, y0, x1, y1)
        if len(p) >= 3:
            c.add(poly(p), BLACK, FIXED, 0)


def core(c, x, y, r, ring=BRASS):
    c.add(ell((x - r * 1.4, y - r * 1.4, x + r * 1.4, y + r * 1.4)), ring, FIXED, 1.0)
    c.add(ell((x - r, y - r, x + r, y + r)), GLOW, FIXED, .6)
    c.add(ell((x - r * .45, y - r * .55, x + r * .1, y - r * .05)), GLOW_LT, FIXED, 0)


def winged_sun(c, x, y, r, east=False):
    if east:
        wing = [(x - r * .3, y), (x - r * 2.4, y - r * .9), (x - r * 2.0, y - r * .1), (x - r * 2.2, y + r * .4), (x - r * .4, y + r * .5)]
        c.add(poly(wing), GOLD, FIXED, .8)
    else:
        for s in (-1, 1):
            wing = [(x + s * r * .4, y - r * .2), (x + s * r * 3.0, y - r * 1.1), (x + s * r * 2.7, y - r * .3), (x + s * r * 2.9, y + r * .2), (x + s * r * 2.3, y + r * .5), (x + s * r * .5, y + r * .6)]
            c.add(poly(wing), GOLD, FIXED, .8)
            for k in (1, 2):
                c.add(("line", [(x + s * r * (.6 + k * .6), y - r * (.1 + k * .2)), (x + s * r * (.6 + k * .6), y + r * .45)], .4), GOLD_DK, FIXED, 0)
    A.sun(c, x, y, r * 1.1, GOLD)
    c.add(ell((x - r * .45, y - r * .45, x + r * .45, y + r * .45)), GOLD_DK, FIXED, .4)


def gear(c, x, y, r, col=BRASS):
    pts = []
    for i in range(16):
        rr = r if i % 2 == 0 else r * .78
        a = math.radians(i * 22.5)
        pts.append((x + rr * math.cos(a), y + rr * math.sin(a)))
    c.add([("poly", pts), ("-ell", (x - r * .32, y - r * .32, x + r * .32, y + r * .32))], col, FIXED, .6)


def halo(c, x, y, r, spikes=12):
    pts = []
    for i in range(spikes * 2):
        rr = r * 1.24 if i % 2 == 0 else r * 1.02
        a = math.radians(i * 180 / spikes)
        pts.append((x + rr * math.cos(a), y + rr * math.sin(a)))
    c.add([("poly", pts), ("-ell", (x - r * .8, y - r * .8, x + r * .8, y + r * .8))], BRASS, FIXED, 1.0)
    c.add([("ell", (x - r * .94, y - r * .94, x + r * .94, y + r * .94)), ("-ell", (x - r * .84, y - r * .84, x + r * .84, y + r * .84))], BRASS_DK, FIXED, 0)


# ---------------- 身体结构（2026-11-08 第四版：钢铁侠 × 战锤40K × 光环 混合，原创造型） ----------------
# 40K：大圆肩甲 + 宽滚边、厚重轮廓；光环：深色内衬在关节处露出来、头盔大面罩；钢铁侠：流线甲片、发光胸核、双色辅甲。
# 接缝不画黑线：甲片之间只露深色内衬或画一道同色加深的细缝，黑描边只画在整件的外轮廓上（finish）。
import numpy as np
from scipy import ndimage as ndi

SILVER = (200, 204, 210)
SILVER_DK = (142, 148, 158)
KHAKI = (150, 136, 100)
KHAKI_DK = (112, 100, 72)
HOSE = (46, 46, 50)
UNDER = (62, 64, 68)
UNDER_DK = (46, 47, 51)
VISOR = (208, 150, 58)
VISOR_LT = (246, 212, 132)
CORE_T = .27


def soft(c, parts, color, chan=FIXED, shade=.2, ow=0.0, under=False):
    """截图式画法：干净的明暗（离边缘越近越暗、左上亮右下暗），没有颗粒和板甲纹理。
    ow>0 时先描一圈黑边；under=True 时只画在还空着的地方（压在已画部件的后面）。"""
    if parts and isinstance(parts[0], str):
        parts = [parts]
    m0 = c._maskarr(parts)
    if not m0.any():
        return
    free = (c.a <= 0) if under else None
    if ow:
        ring = (ndi.distance_transform_edt(~m0) <= ow * S * .95) & ~m0
        if under:
            ring &= free
        c.rgb[ring] = R.INK
        c.a[ring] = 1
        c.ch[ring] = FIXED
    m = m0 & free if under else m0
    if not m.any():
        return
    col = np.asarray(color, np.float32)
    ys, xs = np.nonzero(m0)
    size = min(np.ptp(xs) + 1, np.ptp(ys) + 1) / S
    y0, y1 = ys.min(), ys.max() + 1
    f = np.ones(m.shape, np.float32)
    if size > 3:
        d = ndi.distance_transform_edt(m0)
        dm = max(1.0, min(float(d.max()), 5.0 * S))
        f *= .8 + .2 * np.clip(d / dm, 0, 1) ** .6
        sd = max(1.2, min(5.0, size * .16)) * S
        band = m0 & ~R._shift(m0, -.35 * sd, -1.0 * sd)
        f *= 1 - shade * ndi.gaussian_filter(band.astype(np.float32), S * .9)
        hb = m0 & ~R._shift(m0, .35 * sd * .6, 1.0 * sd * .6)
        f *= 1 + .14 * ndi.gaussian_filter(hb.astype(np.float32), S * .7)
    f *= 1 + .08 * (.5 - (R._yy - y0) / max(1, y1 - y0))
    cc = col[None, None, :] * f[..., None]
    c.rgb[m] = np.clip(cc[m], 0, 255)
    c.a[m] = 1
    c.ch[m] = chan


def squircle(cx, cy, rx_top, rx_bot, ry_top, ry_bot, p=2.6, n=56):
    """圆角方形 / 胶囊：上下半宽可以不同（倒梯形躯干），四角是圆的。"""
    pts = []
    for i in range(n):
        a = 2 * math.pi * i / n
        ca, sa = math.cos(a), math.sin(a)
        rx = rx_top + (rx_bot - rx_top) * (1 + sa) / 2
        ry = ry_bot if sa > 0 else ry_top
        pts.append((cx + rx * math.copysign(abs(ca) ** (2 / p), ca), cy + ry * math.copysign(abs(sa) ** (2 / p), sa)))
    return pts


def leaf(x, y, ang, L, w, tip=.35, n=28):
    """叶片：从 (x,y) 朝 ang 方向伸出 L 长、w 宽的圆头叶片，尖端收窄。"""
    ca, sa = math.cos(ang), math.sin(ang)
    pts = []
    for i in range(n):
        t = 2 * math.pi * i / n
        u = L / 2 * math.cos(t)
        v = w / 2 * math.sin(t) * (1 - tip * math.cos(t))
        px, py = L / 2 + u, v
        pts.append((x + px * ca - py * sa, y + px * sa + py * ca))
    return pts


def seam(c, pts, col, chan=STUFF, w=.6):
    """甲片之间的细缝：同色加深的一道线，不是黑描边。"""
    c.add(("line", pts, w), col, chan, 0)


def finish(c, ow=1.5):
    """只在整件的外轮廓上描一圈深色边。"""
    m = c.a > 0
    ring = (ndi.distance_transform_edt(~m) <= ow * S * .95) & ~m
    c.rgb[ring] = R.INK
    c.a[ring] = 1
    c.ch[ring] = FIXED


def pauldron_geom(f, size=.32, off=.5, cap=57.0):
    ww = min(f.w, 66.0)   # 宽体型肩甲不跟着无限放大，各体型肩甲大小接近
    r, o = ww * size, max(f.w * .5, ww * off * 1.1)   # 肩甲往两侧撑开（按游戏内截图比例）
    k = min(1.0, cap / (o + r))
    return r * k, o * k


def big_pauldrons(c, f, size=.32, off=.5, accent=GOLD, rim=True, layers=1, spikes=0, glow_lines=False, square=False):
    """截图式叶片肩甲：每侧几片圆头叶片从肩后斜向外下方撑开、层层叠住，压在身体和手臂后面；
    侧面是一块盖在肩上的圆肩甲。rim=True 用辅色（金 / 银 / 铁）整片，False 用甲色。"""
    r, o = pauldron_geom(f, size, off)
    col, ch = (accent, FIXED) if rim else (LT, STUFF)
    if f.east:
        x, y = U(f, .02, .1)
        box = squircle(x, y, r * .95, r * 1.05, r * .62, r * .7, 2.4)
        soft(c, poly(box), col, ch, ow=1.2)
        for k in (.15, .45):
            seam(c, [(x - r * .8, y + r * k), (x, y + r * (k + .16)), (x + r * .85, y + r * k)], sc(col, .7), ch, .55)
        return
    n = 3 + (1 if layers >= 2 else 0) + (1 if square else 0)
    for s in (-1, 1):
        bx, by = U(f, s * .3, .1)
        for k in range(n - 1, -1, -1):
            ang = math.radians(12 + k * 15)
            if s < 0:
                ang = math.pi - ang
            L = min(r * (1.75 - .1 * k), 58 - abs(bx - 64) * .2 - r * .2)
            L = min(L, (60 - abs(bx - 64)) / max(.4, abs(math.cos(ang))))
            w = r * (.62 - .04 * k)
            pts = leaf(bx, by + k * r * .12, ang, L, w, .45 if spikes else .3)
            soft(c, poly(pts), sc(col, 1.0 - .05 * k), ch, ow=1.1, under=True)
            mx, my = bx + math.cos(ang) * L * .15, by + k * r * .12 + math.sin(ang) * L * .15
            ex, ey = bx + math.cos(ang) * L * .8, by + k * r * .12 + math.sin(ang) * L * .8
            soft(c, ("line", [(mx, my), (ex, ey)], .6), sc(col, .72), ch, 0, under=True)
            if glow_lines and k == 0:
                soft(c, ("line", [(bx + math.cos(ang) * L * .3, by + math.sin(ang) * L * .3 - 1.4), (bx + math.cos(ang) * L * .75, by + math.sin(ang) * L * .75 - 1.4)], .7), RUNE, FIXED, 0, under=True)


def U(f, u, t):
    """部件坐标：u 是相对甲框宽度的横向位置（乘以 f.k 体格系数），t 是纵向 0..1。"""
    return (f.cx + u * f.w * getattr(f, "k", 1.0), f.y(t))


def P(f, pts):
    return poly([U(f, u, t) for u, t in pts])


def M(pts, s):
    return [(u * s, t) for u, t in pts]


def chest(c, f, accent, e=.12):
    """胶囊形躯干：宽肩收腰的圆角倒梯形，一直包到腰下；胸肌缝、领口护颈、侧肋辅色条、腹部分节都画在里面。"""
    cy = f.y(.28)
    if f.east:
        x = f.cx + f.w * .03
        soft(c, poly(squircle(x, cy, f.w * .37, f.w * .33, f.y(.28) - f.y(-.04), f.y(.6) - cy, 2.4)), LT, STUFF, ow=1.3)
        seam(c, [U(f, .06, .34), U(f, .22, .32), U(f, .34, .22)], sc(LT, .72))
        soft(c, poly(squircle(*U(f, .04, .0), f.w * .2, f.w * .2, 2.2, 2.6, 2.2)), accent, FIXED, ow=.9)
        for i in range(3):
            seam(c, [U(f, -.18, .4 + i * .055), U(f, .32 - i * .02, .4 + i * .055)], sc(LT, .76))
        return
    soft(c, poly(squircle(f.cx, cy, f.w * .42 * f.k, f.w * .33 * f.k, cy - f.y(-.04), f.y(.6) - cy, 2.6)), LT, STUFF, ow=1.3)
    if f.view == "north":
        seam(c, [U(f, 0, .02), U(f, 0, .56)], sc(LT, .7))
        for s in (-1, 1):
            seam(c, [U(f, s * .06, .06), U(f, s * .2, .2), U(f, s * .32, .3)], sc(LT, .72))
        for k in range(4):
            x, y = U(f, 0, .08 + k * .08)
            soft(c, ell((x - 2.4, y - 1.6, x + 2.4, y + 1.6)), UNDER, FIXED)
        return
    seam(c, [U(f, 0, .12), U(f, 0, .37)], sc(LT, .7))
    for s in (-1, 1):
        seam(c, [U(f, s * .02, .36), U(f, s * .18, .34), U(f, s * .33, .22)], sc(LT, .7))
        soft(c, ("line", [U(f, s * .33, .24), U(f, s * .3, .36)], 1.6), accent, FIXED, 0)
    x, y = U(f, 0, .0)
    soft(c, poly(squircle(x, y, f.w * .24, f.w * .16, 2.4, 3.4, 2.2)), accent, FIXED, ow=.9)


def abdomen(c, f, joints=None, n=3):
    """腹部分节：胶囊躯干里几条圆头横片（不描黑边），腰两侧活塞关节。"""
    if f.east:
        return
    for i in range(n):
        t = .4 + i * .05
        hw_ = (.24 - i * .02) * f.k
        soft(c, ("line", [U(f, -hw_, t), U(f, hw_, t)], f.h * .036), sc(LT, .94), STUFF, 0)
        seam(c, [U(f, -hw_ - .02, t + .024), U(f, hw_ + .02, t + .024)], sc(LT, .66), STUFF, .5)
    if joints and f.view == "south":
        for s in (-1, 1):
            for k in range(3):
                x, y = U(f, s * .3, .4 + k * .045)
                soft(c, ell((x - 1.8, y - 1.4, x + 1.8, y + 1.4)), joints, FIXED, ow=.5)


def belt(c, f, col, buckle=None, e=.14):
    if f.east:
        soft(c, poly(squircle(*U(f, .03, .58), f.w * .35, f.w * .35, f.h * .028, f.h * .028, 3.0)), col, FIXED, ow=1.0)
    else:
        soft(c, poly(squircle(*U(f, 0, .58), f.w * .33 * f.k, f.w * .33 * f.k, f.h * .028, f.h * .028, 3.0)), col, FIXED, ow=1.0)
    if buckle and f.view != "north":
        x, y = U(f, .3 if f.east else 0, .58)
        soft(c, poly(squircle(x, y, 3.0, 3.0, 2.6, 2.6, 2.4)), buckle, FIXED, ow=.8)


def arms(c, f, accent):
    """手臂：圆头胶囊的上臂和粗前臂（贴着躯干、内侧压在躯干外框上），深色肘关节，前臂外侧辅色条，圆拳套带指节缝。"""
    W = f.w
    if f.east:
        soft(c, ("line", [U(f, .0, .08), U(f, .02, .26)], W * .17), sc(LT, .95), STUFF, ow=1.1)
        x, y = U(f, .03, .29)
        soft(c, ell((x - W * .08, y - f.h * .022, x + W * .08, y + f.h * .022)), UNDER, FIXED)
        soft(c, ("line", [U(f, .04, .32), U(f, .09, .46)], W * .2), LT, STUFF, ow=1.1)
        soft(c, ("line", [U(f, .12, .33), U(f, .16, .45)], 1.3), accent, FIXED, 0)
        x, y = U(f, .1, .52)
        soft(c, ell((x - W * .09, y - W * .08, x + W * .09, y + W * .08)), sc(LT, .88), STUFF, ow=1.0)
        for k in range(3):
            seam(c, [(x + W * (.0 + k * .035), y - 1.5), (x + W * (.02 + k * .035), y + 1.5)], sc(LT, .6), STUFF, .45)
        return
    for s in (-1, 1):
        soft(c, ("line", [U(f, s * .46, .1), U(f, s * .48, .26)], W * .16), sc(LT, .95), STUFF, ow=1.1)
        x, y = U(f, s * .48, .29)
        soft(c, ell((x - W * .08, y - f.h * .022, x + W * .08, y + f.h * .022)), UNDER, FIXED)
        soft(c, ("line", [U(f, s * .47, .32), U(f, s * .49, .45)], W * .2), LT, STUFF, ow=1.1)
        if f.view == "south":
            soft(c, ("line", [U(f, s * .55, .33), U(f, s * .56, .44)], 1.3), accent, FIXED, 0)
        else:
            seam(c, [U(f, s * .48, .33), U(f, s * .49, .44)], sc(LT, .72))
        x, y = U(f, s * .5, .52)
        soft(c, ell((x - W * .085, y - W * .08, x + W * .085, y + W * .08)), sc(LT, .88), STUFF, ow=1.0)
        if f.view == "south":
            for k in range(3):
                xx = x + s * W * (-.04 + k * .04)
                seam(c, [(xx, y - 1.0), (xx, y + 2.0)], sc(LT, .6), STUFF, .45)


def legs(c, f, accent, thick=.34, knee=None):
    """腿：圆头胶囊的大腿和小腿，深色膝关节 + 圆护膝，大腿外侧辅色条，圆底靴子；两腿之间留空。"""
    knee = knee or accent
    W = f.w
    q = 1.18 if f.body in ("Fat", "Hulk") else 1.0
    tw = W * (thick * .78) * q
    if f.east:
        for u0, tone in ((-.07, .8), (.06, 1.0)):
            soft(c, ("line", [U(f, u0, .66), U(f, u0 + .01, .79)], tw), sc(LT, tone), STUFF, ow=1.1)
            x, y = U(f, u0 + .01, .81)
            soft(c, ell((x - tw * .4, y - f.h * .02, x + tw * .4, y + f.h * .02)), UNDER, FIXED)
            soft(c, ("line", [U(f, u0 + .01, .84), U(f, u0, .93)], tw * .92), sc(LT, tone), STUFF, ow=1.1)
            soft(c, poly(squircle(*U(f, u0 + .06, .975), W * .2, W * .2, f.h * .045, f.h * .03, 2.6)), sc(LT, .8 * tone), STUFF, ow=1.1)
        x, y = U(f, .16, .81)
        soft(c, ell((x - 2.6, y - 3.0, x + 2.8, y + 3.0)), knee, FIXED, ow=.8)
        soft(c, ("line", [U(f, .0, .68), U(f, .01, .77)], 1.3), accent, FIXED, 0)
        return
    for s in (-1, 1):
        cxu = s * .19 * q
        soft(c, ("line", [U(f, cxu, .66), U(f, cxu * 1.03, .78)], tw), LT, STUFF, ow=1.1)
        soft(c, ("line", [U(f, cxu + s * .1, .67), U(f, cxu + s * .1, .77)], 1.3), accent, FIXED, 0)
        x, y = U(f, cxu * 1.03, .805)
        soft(c, ell((x - tw * .42, y - f.h * .02, x + tw * .42, y + f.h * .02)), UNDER, FIXED)
        soft(c, ("line", [U(f, cxu * 1.03, .84), U(f, cxu * 1.03, .925)], tw * .92), LT, STUFF, ow=1.1)
        seam(c, [U(f, cxu * 1.03, .86), U(f, cxu * 1.03, .92)], sc(LT, .72))
        if f.view == "south":
            soft(c, ell((x - tw * .3, y - tw * .26, x + tw * .3, y + tw * .26)), knee, FIXED, ow=.8)
        soft(c, poly(squircle(*U(f, cxu * 1.03, .975), tw * .58, tw * .62, f.h * .04, f.h * .028, 2.6)), sc(LT, .8), STUFF, ow=1.1)


def thrusters(c, f, length=.3, big=False):
    w = f.w * (.12 if big else .09)
    if f.view == "east":
        x = f.cx - f.w * .36
        soft(c, rect((x - w, f.y(-.02), x + w, f.y(length + .08))), DARKSTEEL, FIXED)
        soft(c, rect((x - w * 1.25, f.y(length + .04), x + w * 1.25, f.y(length + .13))), SILVER_DK, FIXED)
        return
    if f.view != "north":
        return
    for s in (-1, 1):
        x = f.cx + s * f.w * .2
        soft(c, rect((x - w, f.y(.02), x + w, f.y(length))), DARKSTEEL, FIXED)
        soft(c, rect((x - w * 1.3, f.y(length - .04), x + w * 1.3, f.y(length + .05))), SILVER_DK, FIXED)
        soft(c, ell((x - w * .8, f.y(length + .03), x + w * .8, f.y(length + .1))), GLOW, FIXED)


def hex_core(c, x, y, r, ring=GOLD):
    def hexa(rr, rot=90):
        return [(x + rr * math.cos(math.radians(rot + i * 60)), y + rr * math.sin(math.radians(rot + i * 60))) for i in range(6)]
    soft(c, poly(hexa(r * 1.55)), ring, FIXED)
    soft(c, poly(hexa(r)), GLOW, FIXED)
    soft(c, poly(hexa(r * .45)), GLOW_LT, FIXED)


def round_core(c, x, y, r, ring=SILVER):
    soft(c, ell((x - r * 1.45, y - r * 1.45, x + r * 1.45, y + r * 1.45)), ring, FIXED)
    soft(c, ell((x - r * 1.12, y - r * 1.12, x + r * 1.12, y + r * 1.12)), sc(ring, .62), FIXED)
    soft(c, ell((x - r, y - r, x + r, y + r)), GLOW, FIXED, .05)
    soft(c, ell((x - r * .5, y - r * .5, x + r * .5, y + r * .5)), GLOW_LT, FIXED, 0)


def sunburst(c, x, y, r, col=GOLD, n=8):
    for i in range(n):
        a = math.radians(-90 + i * 360 / n)
        b1, b2 = math.radians(-90 + i * 360 / n - 9), math.radians(-90 + i * 360 / n + 9)
        soft(c, poly([(x + r * .55 * math.cos(b1), y + r * .55 * math.sin(b1)), (x + r * math.cos(a), y + r * math.sin(a)), (x + r * .55 * math.cos(b2), y + r * .55 * math.sin(b2))]), col, FIXED)


def front_core(c, f, r_s, kind="round", ring=SILVER):
    if f.view == "north":
        return
    x, y = A.chest_xy(f, CORE_T)
    if f.east:
        x = f.cx + f.w * .36
        soft(c, ell((x - 1.6, y - r_s * .95, x + 1.8, y + r_s * .95)), ring, FIXED)
        soft(c, ell((x - .5, y - r_s * .62, x + 1.6, y + r_s * .62)), GLOW, FIXED, 0)
        return
    (hex_core if kind == "hex" else round_core)(c, x, y, r_s, ring)


def suit(c, f, accent, e=.12, flare=.03, joints=None, belt_col=UNDER_DK, buckle=None, knee=None, thick=.34, k=1.0):
    """由部件拼出的身体：先用深色内衬盖住原版裸体轮廓（不露皮肤），再画腿、腹、腰带、胯甲、胸甲、双臂；外轮廓跟着部件走。"""
    f.k = k
    fo = Fit(f.body, f.view)
    soft(c, poly(fo.shape(.02, 1.0, .04)), sc(LT, .62), STUFF, .24)   # 裸体轮廓垫底：用加深的甲色，缝里看着是凹下去的甲，不是黑泡
    legs(c, f, accent, thick, knee)
    if f.east:
        # 侧面：背板 + 后腰臀甲，盖住原版裸体轮廓的后半边
        soft(c, P(f, [(-.46, .0), (-.22, .0), (-.22, .5), (-.42, .52)]), sc(LT, .9), STUFF)
        seam(c, [U(f, -.4, .18), U(f, -.26, .18)], sc(LT, .66))
        seam(c, [U(f, -.41, .34), U(f, -.26, .34)], sc(LT, .66))
        soft(c, P(f, [(-.45, .5), (-.12, .5), (-.12, .64), (-.38, .72), (-.45, .66)]), sc(LT, .86), STUFF)
    abdomen(c, f, joints)
    belt(c, f, belt_col, buckle, e)
    tassets(c, f, accent, e)
    chest(c, f, accent, e)
    arms(c, f, accent)


def tassets(c, f, accent, e=.12):
    """胯甲：腰带下两片圆头垂片 + 中间护裆，下沿辅色。"""
    if f.east:
        x, y = U(f, .02, .63)
        soft(c, poly(squircle(x, y, f.w * .3, f.w * .26, f.h * .03, f.h * .045, 2.4)), sc(LT, .96), STUFF, ow=1.0)
        seam(c, [U(f, -.24, .655), U(f, .28, .655)], accent, FIXED, .9)
        return
    q = 1.18 if f.body in ("Fat", "Hulk") else 1.0
    for s in (-1, 1):
        x, y = U(f, s * .22 * q, .635)
        soft(c, poly(squircle(x, y, f.w * .15 * q, f.w * .13 * q, f.h * .03, f.h * .05, 2.4)), sc(LT, .96), STUFF, ow=1.0)
        seam(c, [(x - f.w * .1 * q, y + f.h * .03), (x + f.w * .1 * q, y + f.h * .03)], accent, FIXED, .9)
    x, y = U(f, 0, .635)
    soft(c, poly(squircle(x, y, f.w * .07, f.w * .05, f.h * .03, f.h * .05, 2.4)), sc(LT, .9), STUFF, ow=1.0)


def tabard(c, f, col_chan=HERALD, t0=.6, t1=.95, w=.26, emblem=None):
    """40K 式腰前布帘（纹章色），可带徽记。"""
    if f.view == "north":
        return
    x = f.cx if not f.east else f.cx + f.w * .4
    hw_ = f.w * (w if not f.east else w * .45)
    soft(c, poly([(x - hw_, f.y(t0)), (x + hw_, f.y(t0)), (x + hw_ * 1.05, f.y(t1)), (x, f.y(t1) - 2), (x - hw_ * 1.05, f.y(t1))]), LT, col_chan)
    seam(c, [(x - hw_ * .9, f.y(t0 + .04)), (x + hw_ * .9, f.y(t0 + .04))], sc(LT, .72), col_chan, .5)
    if emblem and not f.east:
        emblem(c, x, f.y((t0 + t1) / 2 + .02), hw_ * .55)


# ================= 身甲 =================
def scarf(c, f):
    if f.view == "north":
        soft(c, poly([(f.cx - f.w * .34, f.y(-.04)), (f.cx + f.w * .34, f.y(-.04)), (f.cx + f.w * .2, f.y(.1)), (f.cx - f.w * .2, f.y(.1))]), LT, HERALD)
        soft(c, poly([(f.cx - f.w * .26, f.y(.04)), (f.cx - f.w * .08, f.y(.06)), (f.cx - f.w * .2, f.y(.4)), (f.cx - f.w * .3, f.y(.36)), (f.cx - f.w * .36, f.y(.42))]), sc(LT, .9), HERALD)
        return
    if f.east:
        soft(c, poly([(f.cx - f.w * .2, f.y(-.05)), (f.cx + f.w * .3, f.y(-.04)), (f.cx + f.w * .34, f.y(.08)), (f.cx - f.w * .22, f.y(.1))]), LT, HERALD)
        soft(c, poly([(f.cx - f.w * .18, f.y(.04)), (f.cx - f.w * .02, f.y(.06)), (f.cx - f.w * .3, f.y(.36)), (f.cx - f.w * .42, f.y(.3))]), sc(LT, .9), HERALD)
        return
    soft(c, poly([(f.cx - f.w * .34, f.y(-.05)), (f.cx + f.w * .34, f.y(-.05)), (f.cx + f.w * .26, f.y(.08)), (f.cx, f.y(.13)), (f.cx - f.w * .26, f.y(.08))]), LT, HERALD)
    soft(c, poly([(f.cx + f.w * .14, f.y(.04)), (f.cx + f.w * .34, f.y(.02)), (f.cx + f.w * .4, f.y(.34)), (f.cx + f.w * .33, f.y(.3)), (f.cx + f.w * .27, f.y(.4)), (f.cx + f.w * .22, f.y(.3))]), sc(LT, .9), HERALD)


def webbing(c, f):
    """帆布战术腰带 + 弹药袋（挂在腰带上）。"""
    belt(c, f, KHAKI)
    us = [-.24, -.08, .08, .24] if f.view == "south" else ([.14] if f.east else [-.2, .2])
    for u in us:
        x0, y0 = U(f, u, .52)
        soft(c, rect((x0 - 2.6, y0, x0 + 2.6, y0 + f.h * .075)), KHAKI_DK, FIXED)
        seam(c, [(x0 - 2.4, y0 + f.h * .02), (x0 + 2.4, y0 + f.h * .02)], sc(KHAKI_DK, .75), FIXED, .5)


def antenna(c, f):
    if f.east:
        x = f.cx - f.w * .3
        c.add(("line", [(x, f.y(.2)), (x - 2, f.y(-.62))], .6), HOSE, FIXED, .3)
    elif f.view == "north":
        x = f.cx + f.w * .34
        soft(c, rect((x - 3.4, f.y(.06), x + 3.4, f.y(.3))), DARKSTEEL, FIXED)
        c.add(("line", [(x + 1.6, f.y(.08)), (x + 3.6, f.y(-.62))], .6), HOSE, FIXED, .3)


def squire(c, f):
    """奥械侍从甲：光环式战地甲——橄榄绿甲片、深色内衬、旧围巾、帆布腰带弹药袋、背上电台天线；40K 式圆肩甲但不带金边。"""
    suit(c, f, DARKSTEEL, e=.13, belt_col=KHAKI, knee=DARKSTEEL)
    webbing(c, f)
    front_core(c, f, f.w * (.05 if not f.east else .04), "round", BRASS)
    scarf(c, f)
    big_pauldrons(c, f, .3, .5, DARKSTEEL, rim=True, layers=1)
    antenna(c, f)
    thrusters(c, f, .28)


def knight(c, f):
    """秘铸骑士动力甲：钢铁侠式红金流线甲 + 40K 大圆肩甲金滚边 + 光环式深色内衬关节；圆形发光胸核，展翼太阳腰扣。"""
    suit(c, f, GOLD, joints=GOLD_DK, buckle=GOLD, knee=GOLD)
    front_core(c, f, f.w * (.075 if not f.east else .05), "round", GOLD)
    if f.view == "south":
        winged_sun(c, f.cx, f.y(.532), f.w * .035)
    big_pauldrons(c, f, .32, .5, GOLD, layers=1)
    thrusters(c, f, .3)


def oath(c, f):
    """圣誓骑士动力甲：40K 圣骑士味——黑甲金滚边大肩甲、六角胸核外一圈八芒旭日、腰前白色纹章布帘（带旭日）。"""
    suit(c, f, GOLD, joints=GOLD, buckle=GOLD, knee=GOLD)
    if f.view == "south":
        x, y = A.chest_xy(f, CORE_T)
        sunburst(c, x, y, f.w * .2, GOLD, 8)
    front_core(c, f, f.w * (.06 if not f.east else .05), "hex", GOLD)
    tabard(c, f, HERALD, .62, .96, .2, lambda c, x, y, r: sunburst(c, x, y, r, GOLD_DK, 8))
    if f.view == "north":
        sunburst(c, f.cx, f.y(.22), f.w * .16, GOLD_DK, 8)
    big_pauldrons(c, f, .33, .5, GOLD, layers=1)
    thrusters(c, f, .3)


def runes_ring(c, x, y, r, n=10):
    for i in range(n):
        a = math.radians(i * 360 / n)
        px, py = x + math.cos(a) * r, y + math.sin(a) * r
        soft(c, rect((px - .8, py - .8, px + .8, py + .8)), RUNE, FIXED, 0)


def arcanist(c, f):
    """秘法炮手动力甲：紫甲银辅甲、胸前与腿上符文线、背后大核心套符文环、肩上导能晶，没有推进器。"""
    suit(c, f, SILVER, joints=(150, 130, 200), buckle=(150, 130, 200), knee=SILVER)
    if f.view == "north":
        soft(c, ell((f.cx - f.w * .25, f.y(.04), f.cx + f.w * .25, f.y(.44))), DARKSTEEL, FIXED)
        round_core(c, f.cx, f.y(.24), f.w * .12, (150, 130, 200))
        runes_ring(c, f.cx, f.y(.24), f.w * .21)
    elif f.east:
        soft(c, ell((f.cx - f.w * .72, f.y(.02), f.cx - f.w * .28, f.y(.44))), DARKSTEEL, FIXED)
        round_core(c, f.cx - f.w * .5, f.y(.23), f.w * .085, (150, 130, 200))
    front_core(c, f, f.w * (.07 if not f.east else .05), "round", (150, 130, 200))
    if f.view == "south":
        x, y = A.chest_xy(f, CORE_T)
        for s in (-1, 1):
            c.add(("line", [(x + s * f.w * .1, y + 1), (x + s * f.w * .3, y - 2), (x + s * f.w * .42, y + 5)], .6), RUNE, FIXED, 0)
            xl = f.cx + s * f.w * .22
            c.add(("line", [(xl, f.y(.66)), (xl, f.y(.76))], .6), RUNE, FIXED, 0)
    big_pauldrons(c, f, .31, .5, SILVER, layers=1, glow_lines=True)
    tops = [(f.cx + f.w * .05, 1)] if f.east else [(f.cx + s * pauldron_geom(f, .31, .5)[1], s) for s in (-1, 1)]
    for x, s in tops:
        soft(c, rect((x - 1.4, f.y(-.3), x + 1.4, f.y(-.1))), BRASS_DK, FIXED)
        soft(c, poly([(x - 2.4, f.y(-.3)), (x, f.y(-.44)), (x + 2.4, f.y(-.3))]), RUNE, FIXED, .05)


def artificer(c, f):
    """奇械动力甲：青铜甲、深铁辅甲、黄铜齿轮关节、胸前齿轮核心、双核心背包、工具袋、带铆钉尖刺的大肩甲。"""
    suit(c, f, IRON, joints=BRASS, belt_col=LEATHER, buckle=BRASS, knee=BRASS)
    if f.view == "north":
        soft(c, rect((f.cx - f.w * .32, f.y(.02), f.cx + f.w * .32, f.y(.42))), DARKSTEEL, FIXED)
        for s in (-1, 1):
            round_core(c, f.cx + s * f.w * .15, f.y(.2), f.w * .07, BRASS)
        soft(c, rect((f.cx - f.w * .32, f.y(.4), f.cx + f.w * .32, f.y(.45))), BRASS, FIXED)
    elif f.east:
        soft(c, rect((f.cx - f.w * .68, f.y(.0), f.cx - f.w * .3, f.y(.44))), DARKSTEEL, FIXED)
        round_core(c, f.cx - f.w * .49, f.y(.18), f.w * .06, BRASS)
    if f.view == "south":
        x, y = A.chest_xy(f, CORE_T)
        gear(c, x, y, f.w * .13)
        round_core(c, x, y, f.w * .045, BRASS)
    else:
        front_core(c, f, f.w * .04, "round", BRASS)
    if f.view != "north":
        for k in ((-1, 1) if not f.east else (1,)):
            x = f.cx + k * f.w * (.22 if not f.east else .14)
            soft(c, rect((x - 2.8, f.y(.555), x + 2.8, f.y(.64))), LEATHER, FIXED)
            seam(c, [(x - 2.6, f.y(.58)), (x + 2.6, f.y(.58))], sc(LEATHER, .7), FIXED, .5)
    big_pauldrons(c, f, .3, .5, IRON, layers=1, spikes=3)


def bastion(c, f):
    """誓约堡垒重甲：最厚重的 40K 终结者味——方形塔盾大肩甲带尖刺金边、加宽躯干、并排三核心、粗腿、大推进器。"""
    suit(c, f, GOLD, e=.22, flare=.08, joints=GOLD_DK, buckle=GOLD, knee=GOLD, thick=.4, k=1.1)
    if f.view == "south":
        x, y = A.chest_xy(f, CORE_T)
        for k in (-1, 0, 1):
            round_core(c, x + k * f.w * .2, y + (0 if k == 0 else 1.5), f.w * .05, GOLD)
    elif f.east:
        front_core(c, f, f.w * .05, "round", GOLD)
    else:
        round_core(c, f.cx, f.y(.46), f.w * .06, GOLD)
    tabard(c, f, HERALD, .62, .97, .18, None)
    big_pauldrons(c, f, .36, .55, GOLD, layers=2, spikes=3, square=True)
    thrusters(c, f, .3, big=True)


class BigFit(Fit):
    """动力甲比人大一圈：身体画框按 Peter 给的游戏内截图比例：只加高 6%、不加宽（接近原版身体），宽度靠肩甲往两侧撑开；超出画布的体型按画布截住。"""
    def __init__(self, body, view):
        super().__init__(body, view)
        h = min(126 - self.y0, self.h * 1.06)
        self.y1 = self.y0 + h
        self.h = h
        # 同一件甲在不同体型上只差一点宽度（Peter 的三张游戏截图）：瘦 / 女体型撑到至少 58，宽体型不能比原版裸体窄（否则露皮肤）
        w = min(max(self.w, 58.0), 2 * (64 - 4) - 2)
        self.x0, self.x1 = self.cx - w / 2, self.cx + w / 2
        self.w = w


BODY_ITEMS = {"SquireArmor": squire, "KnightArmor": knight, "OathArmor": oath, "ArcanistArmor": arcanist, "ArtificerArmor": artificer, "BastionArmor": bastion}


# ================= 头盔 =================
def shell(c, h, v1=.98, e=.06, col=LT, chan=STUFF):
    """截图式头盔：比头略高的圆角胶囊，下巴圆收到脖子。"""
    pts = squircle(h.cx, h.Y(.52), h.w * (.5 + e), h.w * (.47 + e), h.h * .56, h.h * .55, 2.3)
    soft(c, poly(pts), col, chan, ow=1.4)
    return pts


def visor_glass(c, pts, base=VISOR, hi=VISOR_LT):
    """光环式反光面罩：琥珀色玻璃 + 一道斜高光，四周一圈深色内框（不是黑描边）。"""
    c.add(("line", pts + [pts[0]], 1.4), UNDER_DK, FIXED, 0)
    soft(c, poly(pts), base, FIXED, .3)
    xs = [p[0] for p in pts]
    ys = [p[1] for p in pts]
    x0, x1, y0, y1 = min(xs), max(xs), min(ys), max(ys)
    hl = clip([(x0 + (x1 - x0) * .18, y0), (x0 + (x1 - x0) * .32, y0), (x0 + (x1 - x0) * .2, y1), (x0 + (x1 - x0) * .06, y1)], x0, y0, x1, y1)
    if len(hl) >= 3:
        soft(c, poly(hl), hi, FIXED, 0)


def breather_helm(c, h):
    """奥械侍从盔：光环式圆滑全盔 + 大块琥珀反光面罩，下巴是 40K 式呼吸格栅，两侧短软管。"""
    shell(c, h, .98, .02)
    if h.view == "north":
        seam(c, [(h.cx, h.Y(.0)), (h.cx, h.Y(.9))], sc(LT, .74))
        soft(c, rect((h.X(.3), h.Y(.66), h.X(.7), h.Y(.95))), DARKSTEEL, FIXED)
        return
    if h.east:
        soft(c, poly([(h.X(.5), h.Y(.62)), (h.X(1.06), h.Y(.62)), (h.X(1.16), h.Y(.74)), (h.X(1.12), h.Y(1.02)), (h.X(.56), h.Y(1.04))]), DARKSTEEL, FIXED)
        for k in range(3):
            soft(c, rect((h.X(.92 + k * .07), h.Y(.76), h.X(.95 + k * .07), h.Y(.94))), BLACK, FIXED, 0)
        visor_glass(c, [(h.X(.6), h.Y(.3)), (h.X(1.1), h.Y(.34)), (h.X(1.12), h.Y(.58)), (h.X(.62), h.Y(.6))])
        c.add(("line", [(h.X(.6), h.Y(.98)), (h.X(.3), h.Y(1.08)), (h.X(.08), h.Y(.86))], 1.8), HOSE, FIXED, .4)
        return
    soft(c, poly([(h.X(.22), h.Y(.6)), (h.X(.78), h.Y(.6)), (h.X(.7), h.Y(1.04)), (h.X(.3), h.Y(1.04))]), DARKSTEEL, FIXED)
    for k in range(4):
        x = h.X(.38 + k * .08)
        soft(c, rect((x - .5, h.Y(.72), x + .5, h.Y(.96))), BLACK, FIXED, 0)
    visor_glass(c, [(h.X(.08), h.Y(.3)), (h.X(.92), h.Y(.3)), (h.X(.9), h.Y(.5)), (h.X(.66), h.Y(.62)), (h.X(.34), h.Y(.62)), (h.X(.1), h.Y(.5))])
    for s in (-1, 1):
        c.add(("line", [(h.cx + s * h.w * .22, h.Y(.98)), (h.cx + s * h.w * .44, h.Y(1.08)), (h.cx + s * h.w * .56, h.Y(.84))], 1.8), HOSE, FIXED, .4)


def knight_helm(c, h):
    """秘铸骑士盔：钢铁侠式流线盔 + 金色尖颌面甲，一道蓝色发光 V 形目镜（原创形状），盔顶 40K 式短金脊，耳侧圆护。"""
    shell(c, h, .98, .01)
    if h.view == "north":
        soft(c, poly([(h.cx - 1.6, h.Y(-.04)), (h.cx + 1.6, h.Y(-.04)), (h.cx + 1.2, h.Y(.9)), (h.cx - 1.2, h.Y(.9))]), GOLD, FIXED)
        soft(c, rect((h.X(-.04), h.Y(.86), h.X(1.04), h.Y(.98))), GOLD_DK, FIXED)
        return
    if h.east:
        soft(c, poly([(h.X(.5), h.Y(-.04)), (h.X(.95), h.Y(.18)), (h.X(1.06), h.Y(.34)), (h.X(.98), h.Y(.36)), (h.X(.86), h.Y(.22)), (h.X(.46), h.Y(.02))]), GOLD, FIXED)
        soft(c, poly([(h.X(.56), h.Y(.32)), (h.X(1.1), h.Y(.36)), (h.X(1.13), h.Y(.72)), (h.X(.92), h.Y(1.04)), (h.X(.58), h.Y(1.0))]), sc(LT, .8), STUFF)
        seam(c, [(h.X(.58), h.Y(.33)), (h.X(.6), h.Y(.99))], GOLD, FIXED, .9)
        soft(c, poly([(h.X(.74), h.Y(.45)), (h.X(1.11), h.Y(.49)), (h.X(1.11), h.Y(.56)), (h.X(.74), h.Y(.53))]), GLOW, FIXED, 0)
        for k in range(3):
            seam(c, [(h.X(.8 + k * .08), h.Y(.8)), (h.X(.8 + k * .08), h.Y(.92))], sc(LT, .6), STUFF, .5)
        soft(c, ell((h.X(.28), h.Y(.48), h.X(.54), h.Y(.72))), GOLD_DK, FIXED)
        soft(c, ell((h.X(.35), h.Y(.55), h.X(.47), h.Y(.65))), GLOW, FIXED, 0)
        return
    soft(c, poly([(h.cx - 1.8, h.Y(-.05)), (h.cx + 1.8, h.Y(-.05)), (h.cx + 1.4, h.Y(.34)), (h.cx - 1.4, h.Y(.34))]), GOLD, FIXED)
    face = [(h.X(.1), h.Y(.32)), (h.X(.9), h.Y(.32)), (h.X(.92), h.Y(.7)), (h.X(.66), h.Y(1.04)), (h.X(.34), h.Y(1.04)), (h.X(.08), h.Y(.7))]
    soft(c, poly(face), sc(LT, .8), STUFF)
    seam(c, face + [face[0]], GOLD, FIXED, .9)
    soft(c, poly([(h.X(.38), h.Y(.86)), (h.X(.62), h.Y(.86)), (h.X(.6), h.Y(1.03)), (h.X(.4), h.Y(1.03))]), GOLD, FIXED)
    soft(c, poly([(h.X(.14), h.Y(.42)), (h.cx, h.Y(.5)), (h.X(.86), h.Y(.42)), (h.X(.86), h.Y(.5)), (h.cx, h.Y(.58)), (h.X(.14), h.Y(.5))]), GLOW, FIXED, 0)
    soft(c, poly([(h.X(.3), h.Y(.44)), (h.cx, h.Y(.5)), (h.X(.36), h.Y(.47))]), GLOW_LT, FIXED, 0)
    for k in range(3):
        seam(c, [(h.X(.44 + k * .06), h.Y(.7)), (h.X(.44 + k * .06), h.Y(.82))], sc(LT, .6), STUFF, .5)
    for s in (-1, 1):
        x = h.X(-.04) if s < 0 else h.X(1.04)
        soft(c, ell((x - 3, h.Y(.5), x + 3, h.Y(.68))), GOLD_DK, FIXED)


def bastion_helm(c, h):
    """堡垒巨盔：40K 终结者式宽盔缩在颈甲里——窄蓝目缝、呼吸格栅、两侧护颊翼、金色盔脊和颈甲。"""
    for s in ((-1, 1) if not h.east else (-1,)):
        x = h.X(-.03) if s < 0 else h.X(1.03)
        soft(c, poly([(x, h.Y(.42)), (x + s * h.w * .1, h.Y(.52)), (x + s * h.w * .09, h.Y(.98)), (x, h.Y(1.02))]), MD, STUFF)
        seam(c, [(x + s * h.w * .02, h.Y(.52)), (x + s * h.w * .07, h.Y(.58)), (x + s * h.w * .065, h.Y(.96))], GOLD, FIXED, .8)
    sh = squircle(h.cx, h.Y(.5), h.w * .56, h.w * .55, h.h * .56, h.h * .53, 3.2)
    soft(c, poly(sh), LT, STUFF, ow=1.4)
    soft(c, rect((h.X(-.05), h.Y(.9), h.X(1.05), h.Y(1.02))), GOLD, FIXED)
    rivet_row(c, h.X(.0), h.Y(.96), h.X(1.0), 7 if not h.east else 4, .6, GOLD_DK)
    if h.view == "north":
        soft(c, poly([(h.cx - 2, h.Y(-.08)), (h.cx + 2, h.Y(-.08)), (h.cx + 1.6, h.Y(.88)), (h.cx - 1.6, h.Y(.88))]), GOLD, FIXED)
        return
    if h.east:
        soft(c, poly([(h.X(.3), h.Y(-.09)), (h.X(.95), h.Y(-.06)), (h.X(1.05), h.Y(.3)), (h.X(1.0), h.Y(.32)), (h.X(.9), h.Y(-.01)), (h.X(.3), h.Y(-.03))]), GOLD, FIXED)
        soft(c, poly([(h.X(.6), h.Y(.3)), (h.X(1.06), h.Y(.34)), (h.X(1.06), h.Y(.88)), (h.X(.62), h.Y(.88))]), sc(LT, .9), STUFF)
        soft(c, rect((h.X(.72), h.Y(.45), h.X(1.05), h.Y(.53))), UNDER_DK, FIXED, 0)
        soft(c, rect((h.X(.78), h.Y(.475), h.X(1.04), h.Y(.505))), GLOW, FIXED, 0)
        for k in range(3):
            soft(c, rect((h.X(.9 + k * .08), h.Y(.64), h.X(.93 + k * .08), h.Y(.82))), UNDER_DK, FIXED, 0)
        return
    soft(c, poly([(h.cx - 2.2, h.Y(-.08)), (h.cx + 2.2, h.Y(-.08)), (h.cx + 1.8, h.Y(.3)), (h.cx - 1.8, h.Y(.3))]), GOLD, FIXED)
    face = [(h.X(.04), h.Y(.3)), (h.X(.96), h.Y(.3)), (h.X(.96), h.Y(.88)), (h.X(.04), h.Y(.88))]
    soft(c, poly(face), sc(LT, .9), STUFF)
    seam(c, face + [face[0]], sc(LT, .66), STUFF, .7)
    soft(c, rect((h.X(.12), h.Y(.44), h.X(.88), h.Y(.54))), UNDER_DK, FIXED, 0)
    soft(c, rect((h.X(.16), h.Y(.475), h.X(.84), h.Y(.505))), GLOW, FIXED, 0)
    soft(c, poly([(h.X(.32), h.Y(.62)), (h.X(.68), h.Y(.62)), (h.X(.62), h.Y(.84)), (h.X(.38), h.Y(.84))]), UNDER, FIXED)
    for k in range(4):
        x = h.X(.38 + k * .08)
        soft(c, rect((x - .5, h.Y(.65), x + .5, h.Y(.81))), UNDER_DK, FIXED, 0)


HEAD_ITEMS = {"SquireHelm": breather_helm, "KnightHelm": knight_helm, "BastionHelm": bastion_helm}
COLORS = {"SquireArmor": (104, 116, 88), "SquireHelm": (104, 116, 88), "KnightArmor": (148, 40, 38), "KnightHelm": (148, 40, 38), "OathArmor": (56, 56, 62),
          "ArcanistArmor": (78, 70, 136), "ArtificerArmor": (168, 133, 87), "BastionArmor": (102, 105, 115), "BastionHelm": (102, 105, 115)}
HERALD_PREVIEW = (214, 204, 180)
LABELS = {"SquireArmor": "奥械侍从甲", "KnightArmor": "秘铸骑士动力甲", "OathArmor": "圣誓骑士动力甲", "ArcanistArmor": "秘法炮手动力甲", "ArtificerArmor": "奇械动力甲",
          "BastionArmor": "誓约堡垒重甲", "SquireHelm": "奥械侍从盔", "KnightHelm": "秘铸骑士盔", "BastionHelm": "堡垒巨盔"}
HELM_FOR = {"SquireArmor": "SquireHelm", "ArtificerArmor": "SquireHelm", "BastionArmor": "BastionHelm"}


# ================= 建筑 / 物品 / 按钮（沿用初版） =================
def save_rgba(im, rel):
    path = os.path.join(A.TEX, rel.replace("/", os.sep))
    os.makedirs(os.path.dirname(path), exist_ok=True)
    im.save(path + ".png")
    return path


class Free:
    def __init__(self, w, h):
        self.c = FlatCanvas.__new__(FlatCanvas)
        self.c.col = Image.new("RGBA", (w * S, h * S), (0, 0, 0, 0))
        self.c.msk = Image.new("RGB", (w * S, h * S), (0, 0, 0))

    def out(self, rel, size):
        return save_rgba(self.c.col.resize(size, Image.LANCZOS), rel)


def runes(c, x, y, r, n=8):
    for i in range(n):
        a = math.radians(i * 360 / n)
        px, py = x + math.cos(a) * r, y + math.sin(a) * r
        c.add(rect((px - .7, py - .7, px + .7, py + .7)), GLOW, FIXED, 0)


def foundry(view):
    if view == "east":
        F = Free(128, 192)
        c = F.c
        c.add(rect((14, 10, 114, 182)), (120, 118, 116), FIXED, 1.6)
        c.add(rect((24, 20, 104, 92)), (86, 88, 96), FIXED, 1.4)
        c.add(ell((44, 36, 84, 76)), BRASS, FIXED, 1.2)
        c.add(ell((52, 44, 76, 68)), GLOW, FIXED, .8)
        runes(c, 64, 56, 26, 10)
        c.add(rect((30, 104, 98, 170)), (150, 146, 140), FIXED, 1.2)
        c.add(rect((40, 114, 88, 160)), (104, 104, 108), FIXED, .9)
        c.line([(104, 100), (118, 130), (100, 150)], 4, (96, 98, 104))
        c.add(ell((94, 144, 106, 156)), BRASS, FIXED, .8)
        return F.out("LP/PowerArmor/Foundry/ArcaneFoundry_east", (256, 384))
    F = Free(192, 128)
    c = F.c
    c.add(rect((8, 18, 184, 118)), (120, 118, 116), FIXED, 1.6)
    c.add(rect((16, 6, 82, 92)), (86, 88, 96), FIXED, 1.4)
    c.add(poly([(16, 6), (82, 6), (74, 2), (24, 2)]), (70, 72, 80), FIXED, 1.0)
    if view == "south":
        c.add(ell((29, 28, 69, 68)), BRASS, FIXED, 1.2)
        c.add(ell((37, 36, 61, 60)), GLOW, FIXED, .8)
        runes(c, 49, 48, 27, 10)
    else:
        for k in range(3):
            c.add(rect((24, 20 + k * 20, 74, 28 + k * 20)), (64, 66, 72), FIXED, .6)
    c.add(rect((96, 40, 176, 104)), (150, 146, 140), FIXED, 1.2)
    c.add(rect((106, 50, 166, 94)), (104, 104, 108), FIXED, .9)
    if view == "south":
        c.add(poly([(122, 56), (150, 56), (156, 70), (146, 88), (126, 88), (116, 70)]), (160, 168, 180), FIXED, 1.0)
        c.add(ell((131, 64, 141, 74)), GLOW, FIXED, .6)
    c.line([(176, 30), (160, 14), (136, 22)], 4, (96, 98, 104))
    c.add(ell((130, 16, 142, 28)), BRASS, FIXED, .8)
    c.add(rect((90, 20, 96, 108)), BRASS_DK, FIXED, .8)
    return F.out("LP/PowerArmor/Foundry/ArcaneFoundry_" + view, (384, 256))


def cradle():
    F = Free(104, 128)
    c = F.c
    c.add(poly([(22, 120), (82, 120), (74, 104), (30, 104)]), (110, 110, 112), FIXED, 1.4)
    c.add(rect((38, 34, 66, 106)), (92, 96, 104), FIXED, 1.4)
    c.add(rect((52, 34, 66, 106)), (72, 76, 84), FIXED, 0)
    for k in range(4):
        c.add(rect((44, 46 + k * 14, 60, 50 + k * 14)), GLOW, FIXED, 0)
    c.add(rect((32, 28, 72, 38)), BRASS, FIXED, 1.0)
    c.add(poly([(52, 2), (66, 18), (52, 34), (38, 18)]), GLOW, FIXED, 1.2)
    c.add(poly([(52, 6), (58, 18), (52, 22), (46, 14)]), GLOW_LT, FIXED, 0)
    return F.out("LP/PowerArmor/Cradle/ArcaneCradle", (208, 256))


def cell():
    F = Free(64, 64)
    c = F.c
    c.add(rect((18, 14, 46, 54)), BRASS, FIXED, 1.4)
    c.add(rect((34, 14, 46, 54)), BRASS_DK, FIXED, 0)
    c.add(rect((22, 20, 42, 48)), GLOW, FIXED, .8)
    c.add(rect((24, 22, 30, 38)), GLOW_LT, FIXED, 0)
    c.add(rect((26, 8, 38, 14)), DARKSTEEL, FIXED, 1.0)
    return F.out("LP/PowerArmor/Cell/ArcaneCell", (128, 128))


def ui_icon(name):
    F = Free(64, 64)
    c = F.c
    c.add(rect((2, 2, 62, 62)), (46, 54, 64), FIXED, 0)
    if name == "Leap":
        c.add(("line", [(10, 52), (24, 22), (40, 12), (54, 18)], 4), GLOW, FIXED, 1.2)
        c.add(poly([(48, 10), (58, 18), (46, 24)]), GLOW, FIXED, 1.0)
        c.add(poly([(6, 58), (14, 46), (18, 58)]), (220, 150, 80), FIXED, 1.0)
    elif name == "Field":
        pts = [(32 + 22 * math.cos(math.radians(30 + i * 60)), 32 + 22 * math.sin(math.radians(30 + i * 60))) for i in range(6)]
        c.add(poly(pts), (86, 150, 186), FIXED, 1.4)
        c.add([("poly", pts), ("-ell", (16, 16, 48, 48))], GLOW, FIXED, 0)
        c.add(ell((26, 26, 38, 38)), GLOW_LT, FIXED, 1.0)
    elif name == "Overload":
        pts = []
        for i in range(16):
            r = 26 if i % 2 == 0 else 12
            a = math.radians(i * 22.5)
            pts.append((32 + r * math.cos(a), 32 + r * math.sin(a)))
        c.add(poly(pts), GLOW, FIXED, 1.3)
        c.add(ell((24, 24, 40, 40)), GLOW_LT, FIXED, 1.0)
    elif name == "Charge":
        c.add(rect((20, 10, 44, 56)), BRASS, FIXED, 1.4)
        c.add(rect((26, 6, 38, 10)), DARKSTEEL, FIXED, 1.0)
        c.add(poly([(36, 14), (24, 36), (32, 36), (28, 52), (42, 28), (34, 28)]), GLOW_LT, FIXED, 1.0)
    return F.out("LP/PowerArmor/UI/" + name, (128, 128))


# ================= 预览 =================
def font(size):
    try:
        return ImageFont.truetype("C:/Windows/Fonts/msyh.ttc", size)
    except Exception:
        return None


def tinted(col, msk, name):
    return A.tint(col, msk, COLORS[name], HERALD_PREVIEW)


def dressed(name, body, view, col, msk, helm_imgs, size):
    """身体 + 身甲 + 头盔（头盔按原版的头部偏移上移 30px）。"""
    N = 128 * S
    tile = Image.new("RGBA", (N, N), (0, 0, 0, 0))
    tile.alpha_composite(A.body_blob(body, view, N))
    tile.alpha_composite(tinted(col, msk, name))
    hk = (HELM_FOR.get(name, "KnightHelm"), view)
    if hk in helm_imgs:
        hd = Image.new("RGBA", (N, N), (0, 0, 0, 0))
        hx0, hy0, hx1, hy1 = A.HEAD[view]
        ImageDraw.Draw(hd).ellipse([hx0 * S, (hy0 - 30) * S, hx1 * S, (hy1 - 30) * S], fill=(222, 196, 170, 255))
        tile.alpha_composite(hd)
        layer = Image.new("RGBA", (N, N), (0, 0, 0, 0))
        himg = A.tint(helm_imgs[hk][0], helm_imgs[hk][1], COLORS[name], HERALD_PREVIEW)
        layer.paste(himg, (0, -30 * S), himg)
        tile.alpha_composite(layer)
    return tile.resize((size, size), Image.LANCZOS)


def old_tile(name, view, size):
    """旧版（备份）同件同向，套同样的着色，作对比。"""
    p = os.path.join(OLD, name, "%s_Male_%s" % (name, view))
    if not os.path.exists(p + ".png"):
        return None
    col = Image.open(p + ".png").convert("RGBA").resize((128 * S, 128 * S), Image.LANCZOS)
    msk = Image.open(p + "_m.png").convert("RGB").resize((128 * S, 128 * S), Image.NEAREST)
    N = 128 * S
    tile = Image.new("RGBA", (N, N), (0, 0, 0, 0))
    tile.alpha_composite(A.body_blob("Male", view, N))
    tile.alpha_composite(tinted(col, msk, name))
    return tile.resize((size, size), Image.LANCZOS)


if __name__ == "__main__":
    out, previews = [], []
    for name, fn in BODY_ITEMS.items():
        row = []
        for body in A.BODIES:
            for view in A.VIEWS:
                c = Canvas(view)
                fn(c, BigFit(body, view))
                finish(c)
                out.append(c.save("LP/PowerArmor/%s/%s_%s_%s" % (name, name, body, view), 256, True))
                if view == "south" or body == "Male":
                    row.append((body, view, c.col, c.msk))
                if body == "Male" and view == "south":
                    out.append(c.icon("LP/PowerArmor/%s/%s_Icon" % (name, name)))
        previews.append((name, row))
    helm_imgs = {}
    for name, fn in HEAD_ITEMS.items():
        row = []
        for view in A.VIEWS:
            c = Canvas(view)
            fn(c, Head(view))
            finish(c)
            out.append(c.save("LP/PowerArmor/%s/%s_%s" % (name, name, view), 256, True))
            row.append(("Male", view, c.col, c.msk))
            helm_imgs[(name, view)] = (c.col, c.msk)
            if view == "south":
                out.append(c.icon("LP/PowerArmor/%s/%s_Icon" % (name, name)))
        previews.append((name, row))
    extras = [foundry("south"), foundry("east"), foundry("north"), cradle(), cell()] + [ui_icon(n) for n in ("Leap", "Field", "Overload", "Charge")]
    out += extras

    # ---- 总览：每件一行（Male 南/东/北 + 其他体型南向），最后两行建筑与图标 ----
    cp, cols = 128, 7
    sheet = Image.new("RGBA", (cols * cp + 220, (len(previews) + 2) * cp), (205, 200, 190, 255))
    d = ImageDraw.Draw(sheet)
    f18 = font(18)
    for r, (name, row) in enumerate(previews):
        d.text((6, r * cp + 52), LABELS[name], fill=(40, 36, 36), font=f18)
        for i, (body, view, col, msk) in enumerate(row[:cols]):
            if name in BODY_ITEMS:
                tile = dressed(name, body, view, col, msk, helm_imgs, cp)
            else:
                N = 128 * S
                t = Image.new("RGBA", (N, N), (0, 0, 0, 0))
                hx0, hy0, hx1, hy1 = A.HEAD[view]
                ImageDraw.Draw(t).ellipse([hx0 * S, hy0 * S, hx1 * S, hy1 * S], fill=(222, 196, 170, 255))
                t.alpha_composite(tinted(col, msk, name))
                tile = t.resize((cp, cp), Image.LANCZOS)
            sheet.alpha_composite(tile, (220 + i * cp, r * cp))
    y = len(previews) * cp
    d.text((6, y + 52), "奥械锻炉 / 充能座", fill=(40, 36, 36), font=f18)
    d.text((6, y + cp + 52), "电池 / 按钮", fill=(40, 36, 36), font=f18)
    x = 220
    for p in extras[:4]:
        im = Image.open(p + ".png").convert("RGBA")
        im.thumbnail((cp * 2 - 8, cp - 8))
        sheet.alpha_composite(im, (x, y + 4))
        x += im.width + 12
    x = 220
    for p in extras[4:]:
        im = Image.open(p + ".png").convert("RGBA")
        im.thumbnail((cp - 16, cp - 16))
        sheet.alpha_composite(im, (x, y + cp + 8))
        x += cp
    os.makedirs(os.path.dirname(PREVIEW), exist_ok=True)
    sheet.save(PREVIEW)

    # ---- 三方向对比：每件一行，旧版正面 | 新版 正 / 侧 / 背（Male，带头盔），256px ----
    vp = 256
    vs = Image.new("RGBA", (220 + 4 * vp, 40 + len(BODY_ITEMS) * vp), (205, 200, 190, 255))
    d = ImageDraw.Draw(vs)
    f20 = font(20)
    for i, t in enumerate(["旧版·正面", "新版·正面", "新版·侧面", "新版·背面"]):
        d.text((220 + i * vp + 70, 8), t, fill=(40, 36, 36), font=f20)
    for r, (name, row) in enumerate([p for p in previews if p[0] in BODY_ITEMS]):
        y = 40 + r * vp
        d.text((6, y + vp // 2 - 12), LABELS[name], fill=(40, 36, 36), font=f20)
        old = old_tile(name, "south", vp)
        if old is not None:
            vs.alpha_composite(old, (220, y))
        male = {v: (col, msk) for b, v, col, msk in row if b == "Male"}
        for i, view in enumerate(A.VIEWS):
            col, msk = male[view]
            vs.alpha_composite(dressed(name, "Male", view, col, msk, helm_imgs, vp), (220 + (i + 1) * vp, y))
    vs.save(VIEWS_PNG)
    print(len(out), "textures; preview", PREVIEW, VIEWS_PNG)
