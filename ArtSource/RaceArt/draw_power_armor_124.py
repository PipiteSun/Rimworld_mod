# 1.126.0 奥能动力甲贴图（2026-11-08 第三版：按 Peter 的第二批三张参考图重绘成流线分节动力甲）：6 件身甲 × 五体型 × 南/东/北（256px）+ 3 顶头盔 × 3 向 + _m 遮罩
# （红=甲色，绿=纹章色，黑=固定色）+ 图标；奥械锻炉（3×2，三向）、奥能充能座、奥能电池、4 个按钮图标。
# 预览：ArtSource/Preview/PowerArmor-1.126.0.png（全部）与 PowerArmor-1.126.0-views.png（正 / 侧 / 背 + 旧版对比）。
# 造型：棱角分节肩甲（两三层叠片 + 辅色滚边）、左右两块棱角胸板、收窄的分节腹甲、腰侧辅色活塞关节、大腿外侧辅色条、护膝；胸口发光核心放在头盔下沿下面（CORE_T）。
#   奥械侍从甲（参考①）：做旧橄榄灰、旧围巾（纹章色）、呼吸软管通到左胸滤罐、帆布腰带弹药袋、背上天线。
#   秘铸骑士动力甲（参考③）：甲色（默认红）+ 银色辅甲、圆形发光核心、胸前两盏小灯、展翼太阳腰扣。
#   圣誓骑士动力甲（参考②）：黑甲金饰、六角核心外一圈八芒旭日、金色活塞关节。秘法炮手：银辅甲 + 符文线 + 背后大核心 + 肩上导能晶。
#   奇械动力甲：青铜甲、深铁辅甲、黄铜齿轮关节、双核心背包、工具袋。誓约堡垒重甲：同款放大加厚，三层尖刺大肩甲、三核心。
#   头盔：侍从盔 = 凸节盔顶 + V 形目缝 + 呼吸口罩软管；骑士盔 = 银色尖颌面甲 + V 形发光目镜 + 银脊 + 耳侧圆护；堡垒巨盔 = 宽方盔 + 窄目缝 + 护颊翼 + 金颈甲。
#   徽记只用 DND 风格（展翼太阳、八芒旭日、齿轮、符文）。上一版（40K 重甲风）在 ArtSource/backup-1.126.0-powerarmor-art2。
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
OLD = os.path.join(ROOT, "ArtSource", "backup-1.126.0-powerarmor-art2", "Textures")
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
    return R.Canvas(view, "plate")


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


# ---------------- 身体结构（2026-11-08 第三版：流线分节动力甲） ----------------
SILVER = (200, 204, 210)
SILVER_DK = (142, 148, 158)
KHAKI = (150, 136, 100)
KHAKI_DK = (112, 100, 72)
HOSE = (46, 46, 50)


def pauldron_geom(f, size=.26, off=.46, cap=57.0):
    r, o = f.w * size, f.w * off
    k = min(1.0, cap / (o + r))
    return r * k, o * k


def plate_pauldrons(c, f, size=.29, off=.5, accent=SILVER, layers=2, spikes=0, trim=True, glow_lines=False, rugged=False):
    """分节肩甲：棱角肩壳 + 下面两三层叠片，每层下沿一道辅色滚边。"""
    r, o = pauldron_geom(f, size, off)
    ty = f.y(-.02)
    sides = [(f.cx + f.w * .05, 1)] if f.east else [(f.cx + s * o, s) for s in (-1, 1)]
    if f.east:
        r *= 1.12
    for cx, s in sides:
        for k in range(layers, 0, -1):
            yk = ty + r * (.42 + .3 * k)
            wk = r * (.92 - .1 * k)
            dx = s * r * .06 * k
            pts = [(cx - wk + dx, yk - r * .3), (cx + wk + dx, yk - r * .3), (cx + wk * .9 + dx, yk + r * .14), (cx - wk * .9 + dx, yk + r * .14)]
            c.add(poly(pts), sc(LT, .93 if k % 2 else 1.0), STUFF, 1.1)
            if trim:
                c.add(("line", [(pts[3][0] + 1, pts[3][1] - .7), (pts[2][0] - 1, pts[2][1] - .7)], .8), accent, FIXED, 0)
        cap = []
        for i in range(13):
            a = math.radians(180 + i * 15)
            cap.append((cx + math.cos(a) * r, ty + math.sin(a) * r * .72))
        cap += [(cx + r * 1.04, ty + r * .5), (cx + r * .7, ty + r * .78), (cx - r * .7, ty + r * .78), (cx - r * 1.04, ty + r * .5)]
        if spikes:
            for k in range(spikes):
                u = (k + .5) / spikes
                a = math.radians(205 + u * 130)
                bx, by = cx + math.cos(a) * r * .75, ty + math.sin(a) * r * .55
                tx, ty2 = cx + math.cos(a) * r * 1.35, ty + math.sin(a) * r * 1.2
                c.add(poly([(bx - 1.6, by), (tx, ty2), (bx + 1.6, by)]), accent, FIXED, .9)
        c.add(poly(cap), LT, STUFF, 1.5)
        # 棱线：从内上角斜到外下角，外侧一块稍暗的斜面
        edge = [(cx - s * r * .2, ty - r * .66), (cx + s * r * .55, ty + r * .05), (cx + s * r * .45, ty + r * .78)]
        if f.east:
            edge = [(cx - r * .1, ty - r * .7), (cx + r * .5, ty + r * .05), (cx + r * .4, ty + r * .78)]
        c.add(("line", edge, .7), sc(LT, .78), STUFF, 0)
        if trim:
            c.add(("line", [(cx - r * 1.0, ty + r * .5), (cx - r * .68, ty + r * .74), (cx + r * .68, ty + r * .74), (cx + r * 1.0, ty + r * .5)], 1.0), accent, FIXED, 0)
        if glow_lines:
            c.add(("line", [(cx - r * .6, ty + r * .1), (cx - r * .15, ty - r * .3), (cx + r * .25, ty + r * .05), (cx + r * .6, ty - r * .2)], .6), RUNE, FIXED, 0)
        if rugged:
            for k in range(3):
                px = cx + (k - 1) * r * .45
                c.add(ell((px - .6, ty + r * .45, px + .6, ty + r * .45 + 1.2)), BRASS_DK, FIXED, .3)


def chest(c, f, accent, collar=True):
    """左右两块棱角胸板 + 领口辅色护颈；东向是前凸的胸板；北向是两块肩胛板 + 脊柱节。"""
    if f.east:
        pts = [(f.cx - f.w * .02, f.y(.0)), (f.cx + f.w * .48, f.y(.03)), (f.cx + f.w * .62, f.y(.18)), (f.cx + f.w * .56, f.y(.36)), (f.cx + f.w * .06, f.y(.41))]
        c.add(poly(pts), LT, STUFF, 1.3)
        c.add(("line", [(f.cx + f.w * .1, f.y(.37)), (f.cx + f.w * .52, f.y(.335))], .8), accent, FIXED, 0)
        if collar:
            c.add(poly([(f.cx - f.w * .12, f.y(-.04)), (f.cx + f.w * .32, f.y(-.04)), (f.cx + f.w * .36, f.y(.06)), (f.cx - f.w * .1, f.y(.06))]), accent, FIXED, 1.0)
        return
    for s in (-1, 1):
        if f.view == "north":
            pts = [(f.cx + s * 1.2, f.y(.03)), (f.cx + s * f.w * .54, f.y(.02)), (f.cx + s * f.w * .56, f.y(.24)), (f.cx + s * f.w * .36, f.y(.4)), (f.cx + s * 1.2, f.y(.42))]
        else:
            pts = [(f.cx + s * 1.0, f.y(.06)), (f.cx + s * f.w * .56, f.y(.02)), (f.cx + s * f.w * .6, f.y(.2)), (f.cx + s * f.w * .44, f.y(.38)), (f.cx + s * 2.2, f.y(.42))]
        c.add(poly(pts), LT, STUFF, 1.3)
        c.add(("line", [(pts[4][0] + s * .6, pts[4][1] - .8), (pts[3][0] - s * .4, pts[3][1] - .6), (pts[2][0] - s * .8, pts[2][1])], .8), accent, FIXED, 0)
    if f.view == "north":
        for k in range(5):
            y = f.y(.06 + k * .085)
            c.add(rect((f.cx - 2.2, y, f.cx + 2.2, y + f.h * .055)), accent, FIXED, .7)
    elif collar:
        c.add(poly([(f.cx - f.w * .3, f.y(-.04)), (f.cx + f.w * .3, f.y(-.04)), (f.cx + f.w * .17, f.y(.09)), (f.cx - f.w * .17, f.y(.09))]), accent, FIXED, 1.0)


def abdomen(c, f, accent, joints=None, n=3):
    """收窄的分节腹甲；腰两侧可选辅色关节 / 活塞（joints=颜色）。"""
    for i in range(n):
        t = .44 + i * .05
        hwid = f.w * (.34 - i * .03)
        if f.east:
            x0, x1 = f.cx + f.w * .02, f.cx + f.w * (.5 - i * .02)
            c.add(rect((x0, f.y(t), x1, f.y(t + .042))), sc(LT, .96), STUFF, .9)
        else:
            c.add(poly([(f.cx - hwid, f.y(t)), (f.cx + hwid, f.y(t)), (f.cx + hwid * .94, f.y(t + .042)), (f.cx - hwid * .94, f.y(t + .042))]), sc(LT, .96), STUFF, .9)
    if joints and f.view == "south":
        for s in (-1, 1):
            x = f.cx + s * f.w * .44
            for k in range(3):
                y = f.y(.46 + k * .045)
                c.add(ell((x - 2, y - 1.6, x + 2, y + 1.6)), joints, FIXED, .6)
            c.add(("line", [(x, f.y(.44)), (x, f.y(.58))], .7), sc(joints, .7), FIXED, 0)
    elif joints and f.east:
        x = f.cx - f.w * .1
        for k in range(3):
            y = f.y(.46 + k * .045)
            c.add(ell((x - 2, y - 1.6, x + 2, y + 1.6)), joints, FIXED, .6)


def belt(c, f, col, buckle=None, e=.14):
    A.band(c, f, .6, .045, col, FIXED, e + .02)
    if buckle and f.view != "north":
        x, y = A.chest_xy(f, .6)
        if f.east:
            x = f.cx + f.w * .42
        c.add(rect((x - 2.6, y - 2.2, x + 2.6, y + 2.2)), buckle, FIXED, .7)


def sleek_legs(c, f, accent, thick=.15, knee=None, panel=True):
    """大腿甲 + 外侧辅色竖条 + 护膝 + 小腿甲 + 脚踝辅色。"""
    knee = knee or accent
    if f.east:
        x = f.cx + f.w * .06
        wl = f.w * (thick + .08)
        c.add(poly([(x - wl, f.y(.63)), (x + wl, f.y(.63)), (x + wl * .95, f.y(.8)), (x - wl * .9, f.y(.8))]), LT, STUFF, 1.2)
        if panel:
            c.add(poly([(x - wl * .3, f.y(.65)), (x + wl * .5, f.y(.65)), (x + wl * .4, f.y(.78)), (x - wl * .2, f.y(.78))]), accent, FIXED, .6)
        c.add(poly([(x - wl * .9, f.y(.81)), (x + wl * .95, f.y(.81)), (x + wl, f.y(1.02)), (x - wl, f.y(1.02))]), sc(LT, .95), STUFF, 1.2)
        kx = x + wl * .55
        c.add(poly([(kx - 3.2, f.y(.77)), (kx + 3.0, f.y(.77)), (kx + 3.6, f.y(.84)), (kx, f.y(.87)), (kx - 3.4, f.y(.84))]), knee, FIXED, .8)
        c.add(rect((x - wl, f.y(.97), x + wl, f.y(1.02))), accent, FIXED, .6)
        return
    for s in (-1, 1):
        x = f.cx + s * f.w * .22
        wl = f.w * thick * .5 + 1.6
        c.add(poly([(x - wl, f.y(.63)), (x + wl, f.y(.63)), (x + wl * .92, f.y(.8)), (x - wl * .92, f.y(.8))]), LT, STUFF, 1.2)
        if panel:
            xo = x + s * wl * .45
            c.add(poly([(xo - wl * .16, f.y(.65)), (xo + wl * .16, f.y(.65)), (xo + wl * .12, f.y(.79)), (xo - wl * .12, f.y(.79))]), accent, FIXED, .4)
        c.add(poly([(x - wl * .9, f.y(.81)), (x + wl * .9, f.y(.81)), (x + wl, f.y(1.02)), (x - wl, f.y(1.02))]), sc(LT, .95), STUFF, 1.2)
        if f.view == "south":
            c.add(poly([(x - wl * .7, f.y(.77)), (x + wl * .7, f.y(.77)), (x + wl * .8, f.y(.84)), (x, f.y(.87)), (x - wl * .8, f.y(.84))]), knee, FIXED, .8)
        c.add(rect((x - wl, f.y(.97), x + wl, f.y(1.02))), accent, FIXED, .6)


def bulk(c, f, e=.12, flare=.03):
    A.torso(c, f, .0, 1.0, STUFF, LT, e=e, flare=flare)


def thrusters(c, f, length=.3, big=False):
    w = f.w * (.12 if big else .09)
    if f.view == "east":
        x = f.cx - f.w * .5
        c.add(rect((x - w, f.y(-.02), x + w, f.y(length + .08))), DARKSTEEL, FIXED, 1.2)
        c.add(rect((x - w * 1.25, f.y(length + .04), x + w * 1.25, f.y(length + .13))), SILVER_DK, FIXED, 1.0)
        return
    if f.view != "north":
        return
    for s in (-1, 1):
        x = f.cx + s * f.w * .2
        c.add(rect((x - w, f.y(.02), x + w, f.y(length))), DARKSTEEL, FIXED, 1.2)
        c.add(rect((x - w * 1.3, f.y(length - .04), x + w * 1.3, f.y(length + .05))), SILVER_DK, FIXED, 1.0)
        c.add(ell((x - w * .8, f.y(length + .03), x + w * .8, f.y(length + .1))), GLOW, FIXED, .6)


def hex_core(c, x, y, r, ring=GOLD):
    def hexa(rr, rot=90):
        return [(x + rr * math.cos(math.radians(rot + i * 60)), y + rr * math.sin(math.radians(rot + i * 60))) for i in range(6)]
    c.add(poly(hexa(r * 1.55)), ring, FIXED, 1.0)
    c.add(poly(hexa(r)), GLOW, FIXED, .6)
    c.add(poly(hexa(r * .45)), GLOW_LT, FIXED, 0)


def sunburst(c, x, y, r, col=GOLD, n=8):
    for i in range(n):
        a = math.radians(-90 + i * 360 / n)
        b1, b2 = math.radians(-90 + i * 360 / n - 9), math.radians(-90 + i * 360 / n + 9)
        c.add(poly([(x + r * .55 * math.cos(b1), y + r * .55 * math.sin(b1)), (x + r * math.cos(a), y + r * math.sin(a)), (x + r * .55 * math.cos(b2), y + r * .55 * math.sin(b2))]), col, FIXED, .6)


CORE_T = .33


def front_core(c, f, r_s, kind="round", ring=SILVER):
    """胸口核心（南向正中、东向在胸板前沿）。"""
    if f.view == "north":
        return
    x, y = A.chest_xy(f, CORE_T)
    if f.east:
        x = f.cx + f.w * .54
        c.add(ell((x - 1.4, y - r_s * .9, x + 1.8, y + r_s * .9)), ring, FIXED, .8)
        c.add(ell((x - .4, y - r_s * .6, x + 1.6, y + r_s * .6)), GLOW, FIXED, 0)
        return
    if kind == "hex":
        hex_core(c, x, y, r_s, ring)
    else:
        core(c, x, y, r_s, ring)


def suit(c, f, accent, e=.12, flare=.03, joints=None, belt_col=IRON, buckle=None, knee=None, thick=.15):
    bulk(c, f, e, flare)
    sleek_legs(c, f, accent, thick, knee)
    abdomen(c, f, accent, joints)
    belt(c, f, belt_col, buckle, e)
    chest(c, f, accent)


# ================= 身甲 =================
def scarf(c, f):
    """参考①的破围巾（纹章色）：绕颈一圈，一角搭到左肩后垂下。"""
    if f.view == "north":
        c.add(poly([(f.cx - f.w * .34, f.y(-.04)), (f.cx + f.w * .34, f.y(-.04)), (f.cx + f.w * .2, f.y(.1)), (f.cx - f.w * .2, f.y(.1))]), LT, HERALD, 1.1)
        c.add(poly([(f.cx - f.w * .26, f.y(.04)), (f.cx - f.w * .08, f.y(.06)), (f.cx - f.w * .2, f.y(.4)), (f.cx - f.w * .3, f.y(.36)), (f.cx - f.w * .36, f.y(.42))]), sc(LT, .9), HERALD, 1.0)
        return
    if f.east:
        c.add(poly([(f.cx - f.w * .2, f.y(-.05)), (f.cx + f.w * .3, f.y(-.04)), (f.cx + f.w * .34, f.y(.08)), (f.cx - f.w * .22, f.y(.1))]), LT, HERALD, 1.1)
        c.add(poly([(f.cx - f.w * .18, f.y(.04)), (f.cx - f.w * .02, f.y(.06)), (f.cx - f.w * .3, f.y(.36)), (f.cx - f.w * .42, f.y(.3))]), sc(LT, .9), HERALD, 1.0)
        return
    c.add(poly([(f.cx - f.w * .34, f.y(-.05)), (f.cx + f.w * .34, f.y(-.05)), (f.cx + f.w * .26, f.y(.08)), (f.cx, f.y(.13)), (f.cx - f.w * .26, f.y(.08))]), LT, HERALD, 1.1)
    for k in range(3):
        c.add(("line", [(f.cx - f.w * (.24 - k * .12), f.y(-.02)), (f.cx - f.w * (.2 - k * .12), f.y(.08))], .45), sc(LT, .72), HERALD, 0)
    c.add(poly([(f.cx + f.w * .14, f.y(.04)), (f.cx + f.w * .34, f.y(.02)), (f.cx + f.w * .4, f.y(.34)), (f.cx + f.w * .33, f.y(.3)), (f.cx + f.w * .27, f.y(.4)), (f.cx + f.w * .22, f.y(.3))]), sc(LT, .9), HERALD, 1.0)


def hoses(c, f):
    """呼吸软管：从面罩下沿垂到左胸的滤罐（参考①）。"""
    if f.view == "north":
        return
    if f.east:
        c.add(("line", [(f.cx + f.w * .34, f.y(-.08)), (f.cx + f.w * .52, f.y(.12)), (f.cx + f.w * .36, f.y(.3))], 1.7), HOSE, FIXED, .4)
        c.add(rect((f.cx + f.w * .2, f.y(.26), f.cx + f.w * .4, f.y(.38))), DARKSTEEL, FIXED, .9)
        return
    x = f.cx - f.w * .3
    c.add(("line", [(f.cx - f.w * .1, f.y(-.08)), (f.cx - f.w * .22, f.y(.12)), (x, f.y(.27))], 1.7), HOSE, FIXED, .4)
    c.add(("line", [(f.cx + f.w * .1, f.y(-.08)), (f.cx + f.w * .02, f.y(.16)), (x + 3, f.y(.29))], 1.7), HOSE, FIXED, .4)
    c.add(rect((x - 3.4, f.y(.26), x + 3.4, f.y(.38))), DARKSTEEL, FIXED, .9)
    c.add(rect((x - 3.4, f.y(.26), x + 3.4, f.y(.28))), BRASS_DK, FIXED, 0)


def webbing(c, f):
    """参考①的帆布战术腰带 + 几个弹药袋。"""
    A.band(c, f, .6, .055, KHAKI, FIXED, .16)
    xs = ([f.cx + f.w * k for k in (-.36, -.12, .12, .36)] if not f.east else [f.cx + f.w * .3])
    if f.view == "north":
        xs = [f.cx + f.w * k for k in (-.3, .3)]
    for x in xs:
        c.add(rect((x - 2.6, f.y(.59), x + 2.6, f.y(.67))), KHAKI_DK, FIXED, .7)
        c.add(rect((x - 2.6, f.y(.59), x + 2.6, f.y(.61))), sc(KHAKI_DK, .8), FIXED, 0)


def antenna(c, f):
    if f.east:
        x = f.cx - f.w * .42
        c.add(("line", [(x, f.y(.2)), (x - 2, f.y(-.62))], .6), HOSE, FIXED, .3)
    elif f.view == "north":
        x = f.cx + f.w * .34
        c.add(rect((x - 3.4, f.y(.06), x + 3.4, f.y(.3))), DARKSTEEL, FIXED, 1.0)
        c.add(("line", [(x + 1.6, f.y(.08)), (x + 3.6, f.y(-.62))], .6), HOSE, FIXED, .3)
    else:
        x = f.cx - f.w * .34
        c.add(("line", [(x, f.y(.0)), (x - 2, f.y(-.62))], .6), HOSE, FIXED, .3)


def squire(c, f):
    """奥械侍从甲（参考①）：做旧橄榄灰分节甲、破围巾、呼吸软管、帆布腰带、背上天线。"""
    if f.view == "south":
        antenna(c, f)
    suit(c, f, DARKSTEEL, e=.13, belt_col=KHAKI, knee=DARKSTEEL, thick=.16)
    webbing(c, f)
    scratches(c, f, 11 + len(f.body), 10, .45, .02, .95)
    front_core(c, f, f.w * (.05 if not f.east else .04), "round", BRASS)
    if f.view != "north":
        x, y = A.chest_xy(f, CORE_T)
        if not f.east:
            c.add(("line", [(x - f.w * .14, y + 2), (x - f.w * .3, y + 4)], .7), BRASS_DK, FIXED, 0)
    scarf(c, f)
    hoses(c, f)
    plate_pauldrons(c, f, .28, .5, DARKSTEEL, layers=2, rugged=True)
    if f.view != "south":
        antenna(c, f)
    thrusters(c, f, .28)


def knight(c, f):
    """秘铸骑士动力甲（参考③）：流线分节甲 + 银色辅甲条、圆形发光胸核、肩前小灯，腰扣展翼太阳。"""
    suit(c, f, SILVER, joints=SILVER_DK, belt_col=IRON, buckle=GOLD, knee=SILVER)
    front_core(c, f, f.w * (.075 if not f.east else .05), "round", SILVER)
    if f.view == "south":
        for s in (-1, 1):
            x = f.cx + s * f.w * .4
            c.add(rect((x - 1.4, f.y(.3), x + 1.4, f.y(.34))), GLOW_LT, FIXED, .5)
        winged_sun(c, f.cx, f.y(.6), f.w * .035)
    elif f.east:
        winged_sun(c, f.cx + f.w * .42, f.y(.6), f.w * .03, True)
    plate_pauldrons(c, f, .29, .5, SILVER, layers=2)
    thrusters(c, f, .3)


def oath(c, f):
    """圣誓骑士动力甲（参考②）：黑甲金饰、六角胸核外一圈八芒旭日、腰侧金色活塞关节、金色腿甲条。"""
    suit(c, f, GOLD, joints=GOLD, belt_col=GOLD_DK, buckle=GOLD, knee=GOLD)
    if f.view == "south":
        x, y = A.chest_xy(f, CORE_T)
        sunburst(c, x, y, f.w * .2, GOLD, 8)
    front_core(c, f, f.w * (.06 if not f.east else .05), "hex", GOLD)
    if f.view == "north":
        sunburst(c, f.cx, f.y(.22), f.w * .16, GOLD_DK, 8)
    plate_pauldrons(c, f, .3, .5, GOLD, layers=2)
    thrusters(c, f, .3)


def runes_ring(c, x, y, r, n=10):
    for i in range(n):
        a = math.radians(i * 360 / n)
        px, py = x + math.cos(a) * r, y + math.sin(a) * r
        c.add(rect((px - .8, py - .8, px + .8, py + .8)), RUNE, FIXED, 0)


def arcanist(c, f):
    """秘法炮手动力甲：流线甲 + 银辅甲、胸前符文线、背后大核心套符文环、肩上导能晶，没有推进器。"""
    if f.view == "north":
        pass
    suit(c, f, SILVER, joints=(150, 130, 200), belt_col=IRON, buckle=(150, 130, 200), knee=SILVER)
    if f.view == "north":
        c.add(ell((f.cx - f.w * .25, f.y(.04), f.cx + f.w * .25, f.y(.44))), DARKSTEEL, FIXED, 1.3)
        core(c, f.cx, f.y(.24), f.w * .13, (150, 130, 200))
        runes_ring(c, f.cx, f.y(.24), f.w * .21)
    elif f.east:
        c.add(ell((f.cx - f.w * .72, f.y(.02), f.cx - f.w * .28, f.y(.44))), DARKSTEEL, FIXED, 1.3)
        core(c, f.cx - f.w * .5, f.y(.23), f.w * .09, (150, 130, 200))
    front_core(c, f, f.w * (.07 if not f.east else .05), "round", (150, 130, 200))
    if f.view == "south":
        x, y = A.chest_xy(f, CORE_T)
        for s in (-1, 1):
            c.add(("line", [(x + s * f.w * .1, y + 1), (x + s * f.w * .3, y - 2), (x + s * f.w * .42, y + 5)], .6), RUNE, FIXED, 0)
            xl = f.cx + s * f.w * .22
            c.add(("line", [(xl, f.y(.66)), (xl, f.y(.76))], .6), RUNE, FIXED, 0)
        c.add(("line", [(x, y + f.w * .1), (x, f.y(.56))], .6), RUNE, FIXED, 0)
    plate_pauldrons(c, f, .29, .5, SILVER, layers=2, glow_lines=True)
    tops = [(f.cx + f.w * .05, 1)] if f.east else [(f.cx + s * pauldron_geom(f, .29, .5)[1], s) for s in (-1, 1)]
    for x, s in tops:
        c.add(rect((x - 1.4, f.y(-.28), x + 1.4, f.y(-.08))), BRASS_DK, FIXED, .9)
        c.add(poly([(x - 2.4, f.y(-.28)), (x, f.y(-.42)), (x + 2.4, f.y(-.28))]), RUNE, FIXED, .9)


def artificer(c, f):
    """奇械动力甲（参考②的外露机械关节）：青铜甲、深铁辅甲、黄铜齿轮关节、胸前齿轮核心、双核心背包、工具袋。"""
    suit(c, f, IRON, joints=BRASS, belt_col=LEATHER, buckle=BRASS, knee=BRASS)
    if f.view == "north":
        c.add(rect((f.cx - f.w * .32, f.y(.02), f.cx + f.w * .32, f.y(.42))), DARKSTEEL, FIXED, 1.3)
        for s in (-1, 1):
            core(c, f.cx + s * f.w * .15, f.y(.2), f.w * .075)
        c.add(rect((f.cx - f.w * .32, f.y(.4), f.cx + f.w * .32, f.y(.45))), BRASS, FIXED, .8)
    elif f.east:
        c.add(rect((f.cx - f.w * .68, f.y(.0), f.cx - f.w * .3, f.y(.44))), DARKSTEEL, FIXED, 1.3)
        core(c, f.cx - f.w * .49, f.y(.18), f.w * .065)
    if f.view == "south":
        x, y = A.chest_xy(f, CORE_T)
        gear(c, x, y, f.w * .13)
        core(c, x, y, f.w * .045)
    else:
        front_core(c, f, f.w * .04, "round", BRASS)
    for k in ((-1, 1) if not f.east else (1,)):
        if f.view == "north":
            continue
        x = f.cx + k * f.w * (.36 if not f.east else .14)
        c.add(rect((x - 2.8, f.y(.6), x + 2.8, f.y(.71))), LEATHER, FIXED, .9)
        c.add(rect((x - 2.8, f.y(.6), x + 2.8, f.y(.63))), sc(LEATHER, .75), FIXED, 0)
    if f.view == "south":
        c.add(("line", [(f.cx + f.w * .5, f.y(.63)), (f.cx + f.w * .56, f.y(.74))], 1.1), DARKSTEEL, FIXED, .4)  # 扳手
    plate_pauldrons(c, f, .28, .5, IRON, layers=2, spikes=3)
    if f.view == "south":
        r, o = pauldron_geom(f, .28, .5)
        for s in (-1, 1):
            gear(c, f.cx + s * o, f.y(.02), f.w * .07)


def bastion(c, f):
    """誓约堡垒重甲：同一套流线分节甲放大加厚——三层大肩甲带尖刺和金滚边、并排三核心、粗腿、背后大推进器。"""
    suit(c, f, GOLD, e=.22, flare=.08, joints=GOLD_DK, belt_col=IRON, buckle=GOLD, knee=GOLD, thick=.21)
    if f.view == "south":
        x, y = A.chest_xy(f, CORE_T)
        for k in (-1, 0, 1):
            core(c, x + k * f.w * .2, y + (0 if k == 0 else 1.5), f.w * .05, GOLD)
    elif f.east:
        front_core(c, f, f.w * .05, "round", GOLD)
    else:
        core(c, f.cx, f.y(.46), f.w * .06, GOLD)
    plate_pauldrons(c, f, .36, .54, GOLD, layers=3, spikes=3)
    thrusters(c, f, .3, big=True)


BODY_ITEMS = {"SquireArmor": squire, "KnightArmor": knight, "OathArmor": oath, "ArcanistArmor": arcanist, "ArtificerArmor": artificer, "BastionArmor": bastion}


# ================= 头盔 =================
def breather_helm(c, h):
    """奥械侍从盔（参考①）：棱角盔、盔顶一排凸节、深色斜面罩带一条淡蓝目缝、前凸呼吸口罩 + 两根软管。"""
    A.dome(c, h, .98, STUFF, LT, e=.07)
    if h.view == "north":
        for k in range(4):
            c.add(ell((h.cx - 2, h.Y(.0 + k * .16), h.cx + 2, h.Y(.1 + k * .16))), DARKSTEEL, FIXED, .6)
        c.add(rect((h.X(.3), h.Y(.62), h.X(.7), h.Y(.95))), DARKSTEEL, FIXED, .9)
        for s in (-1, 1):
            c.add(("line", [(h.cx + s * h.w * .2, h.Y(.92)), (h.cx + s * h.w * .5, h.Y(.78))], 1.6), HOSE, FIXED, .4)
        return
    if h.east:
        for k in range(4):
            x = h.X(.25 + k * .17)
            c.add(ell((x - 2, h.Y(-.06 + abs(k - 1.5) * .03), x + 2, h.Y(.05 + abs(k - 1.5) * .03))), DARKSTEEL, FIXED, .6)
        c.add(poly([(h.X(.55), h.Y(.32)), (h.X(1.1), h.Y(.38)), (h.X(1.08), h.Y(.62)), (h.X(.56), h.Y(.6))]), DARKSTEEL, FIXED, 1.1)
        c.add(rect((h.X(.75), h.Y(.44), h.X(1.06), h.Y(.49))), GLOW, FIXED, 0)
        c.add(poly([(h.X(.62), h.Y(.64)), (h.X(1.2), h.Y(.7)), (h.X(1.16), h.Y(1.0)), (h.X(.64), h.Y(1.02))]), DARKSTEEL, FIXED, 1.2)
        for k in range(3):
            c.add(rect((h.X(.95 + k * .07), h.Y(.76), h.X(.98 + k * .07), h.Y(.94))), BLACK, FIXED, 0)
        c.add(("line", [(h.X(.7), h.Y(.98)), (h.X(.3), h.Y(1.1)), (h.X(.05), h.Y(.86))], 1.8), HOSE, FIXED, .4)
    else:
        for k in range(4):
            c.add(ell((h.cx - 2, h.Y(-.08 + k * .1), h.cx + 2, h.Y(.02 + k * .1))), DARKSTEEL, FIXED, .6)
        c.add(poly([(h.X(.08), h.Y(.34)), (h.X(.92), h.Y(.34)), (h.X(.88), h.Y(.6)), (h.cx, h.Y(.66)), (h.X(.12), h.Y(.6))]), DARKSTEEL, FIXED, 1.1)
        c.add(poly([(h.X(.16), h.Y(.44)), (h.cx, h.Y(.5)), (h.X(.84), h.Y(.44)), (h.X(.84), h.Y(.48)), (h.cx, h.Y(.54)), (h.X(.16), h.Y(.48))]), GLOW, FIXED, 0)
        c.add(poly([(h.X(.3), h.Y(.64)), (h.X(.7), h.Y(.64)), (h.X(.64), h.Y(1.04)), (h.X(.36), h.Y(1.04))]), DARKSTEEL, FIXED, 1.2)
        for k in range(3):
            x = h.X(.41 + k * .09)
            c.add(rect((x - .5, h.Y(.74), x + .5, h.Y(.96))), BLACK, FIXED, 0)
        for s in (-1, 1):
            c.add(("line", [(h.cx + s * h.w * .16, h.Y(.98)), (h.cx + s * h.w * .42, h.Y(1.08)), (h.cx + s * h.w * .56, h.Y(.84))], 1.8), HOSE, FIXED, .4)
    rivet_row(c, h.X(.0), h.Y(.26), h.X(1.0), 6 if not h.east else 3, .55, BRASS_DK)


def knight_helm(c, h):
    """秘铸骑士盔（参考③的流线全盔，但面甲是自己的：银色尖颌面甲、一道 V 形发光目镜、盔顶银脊、耳侧圆护）。"""
    A.dome(c, h, .98, STUFF, LT, e=.05)
    if h.view == "north":
        c.add(("line", [(h.cx, h.Y(-.02)), (h.cx, h.Y(.9))], 2.2), SILVER, FIXED, .5)
        c.add(rect((h.X(-.04), h.Y(.86), h.X(1.04), h.Y(.98))), SILVER_DK, FIXED, .7)
        return
    if h.east:
        c.add(("line", [(h.X(.6), h.Y(-.02)), (h.X(.95), h.Y(.2)), (h.X(1.06), h.Y(.36))], 2.0), SILVER, FIXED, .5)
        face = [(h.X(.56), h.Y(.32)), (h.X(1.1), h.Y(.36)), (h.X(1.13), h.Y(.72)), (h.X(.92), h.Y(1.04)), (h.X(.58), h.Y(1.0))]
        c.add(poly(face), SILVER, FIXED, 1.2)
        c.add(poly([(h.X(.74), h.Y(.45)), (h.X(1.11), h.Y(.49)), (h.X(1.11), h.Y(.56)), (h.X(.74), h.Y(.53))]), GLOW, FIXED, .5)
        for k in range(3):
            c.add(("line", [(h.X(.8 + k * .08), h.Y(.8)), (h.X(.8 + k * .08), h.Y(.92))], .5), SILVER_DK, FIXED, 0)
        c.add(ell((h.X(.3), h.Y(.48), h.X(.56), h.Y(.72))), SILVER, FIXED, .9)
        c.add(ell((h.X(.37), h.Y(.55), h.X(.49), h.Y(.65))), SILVER_DK, FIXED, 0)
        return
    face = [(h.X(.1), h.Y(.32)), (h.X(.9), h.Y(.32)), (h.X(.92), h.Y(.7)), (h.X(.66), h.Y(1.04)), (h.X(.34), h.Y(1.04)), (h.X(.08), h.Y(.7))]
    c.add(poly(face), SILVER, FIXED, 1.2)
    c.add(("line", [(h.cx, h.Y(-.04)), (h.cx, h.Y(.34))], 2.2), SILVER, FIXED, .5)
    c.add(poly([(h.X(.14), h.Y(.42)), (h.cx, h.Y(.5)), (h.X(.86), h.Y(.42)), (h.X(.86), h.Y(.5)), (h.cx, h.Y(.58)), (h.X(.14), h.Y(.5))]), GLOW, FIXED, .6)
    c.add(poly([(h.X(.3), h.Y(.44)), (h.cx, h.Y(.5)), (h.X(.36), h.Y(.47))]), GLOW_LT, FIXED, 0)
    for k in range(3):
        x = h.X(.42 + k * .08)
        c.add(("line", [(x, h.Y(.78)), (x, h.Y(.92))], .5), SILVER_DK, FIXED, 0)
    for s in (-1, 1):
        x = h.X(-.04) if s < 0 else h.X(1.04)
        c.add(ell((x - 3, h.Y(.5), x + 3, h.Y(.68))), SILVER, FIXED, .9)


def bastion_helm(c, h):
    """堡垒巨盔：方肩宽盔、一条窄蓝目缝、两侧护颊翼、金色盔脊和颈甲（与 Def 描述一致）。"""
    for s in ((-1, 1) if not h.east else (-1,)):
        x = h.X(-.12) if s < 0 else h.X(1.12)
        c.add(poly([(x, h.Y(.4)), (x + s * h.w * .2, h.Y(.52)), (x + s * h.w * .18, h.Y(1.0)), (x, h.Y(1.06))]), MD, STUFF, 1.2)
        c.add(("line", [(x + s * h.w * .04, h.Y(.5)), (x + s * h.w * .14, h.Y(.58)), (x + s * h.w * .13, h.Y(.98))], .8), GOLD, FIXED, 0)
    shell = [(h.X(-.13), h.Y(1.02)), (h.X(-.15), h.Y(.3)), (h.X(-.06), h.Y(.08)), (h.X(.14), h.Y(-.06)), (h.X(.86), h.Y(-.06)), (h.X(1.06), h.Y(.08)), (h.X(1.15), h.Y(.3)), (h.X(1.13), h.Y(1.02))]
    c.add(poly(shell), LT, STUFF, 1.5)
    c.add(poly([(h.X(1.15), h.Y(.32)), (h.X(.95), h.Y(-.06)), (h.X(.82), h.Y(-.06)), (h.X(1.0), h.Y(.34)), (h.X(.98), h.Y(1.02)), (h.X(1.13), h.Y(1.02))]), sc(LT, .86), STUFF, 0)
    c.add(rect((h.X(-.14), h.Y(.9), h.X(1.14), h.Y(1.04))), GOLD, FIXED, .9)
    rivet_row(c, h.X(-.06), h.Y(.97), h.X(1.06), 7 if not h.east else 4, .6, GOLD_DK)
    if h.view == "north":
        c.add(("line", [(h.cx, h.Y(-.08)), (h.cx, h.Y(.88))], 2.4), GOLD, FIXED, .5)
        return
    if h.east:
        c.add(("line", [(h.X(.3), h.Y(-.08)), (h.X(.95), h.Y(-.04)), (h.X(1.14), h.Y(.3))], 2.2), GOLD, FIXED, .5)
        c.add(poly([(h.X(.6), h.Y(.3)), (h.X(1.16), h.Y(.34)), (h.X(1.16), h.Y(.88)), (h.X(.62), h.Y(.88))]), sc(LT, .9), STUFF, 1.1)
        c.add(rect((h.X(.72), h.Y(.46), h.X(1.15), h.Y(.52))), BLACK, FIXED, 0)
        c.add(rect((h.X(.78), h.Y(.475), h.X(1.13), h.Y(.505))), GLOW, FIXED, 0)
        return
    c.add(("line", [(h.cx, h.Y(-.08)), (h.cx, h.Y(.3))], 2.4), GOLD, FIXED, .5)
    c.add(poly([(h.X(.04), h.Y(.3)), (h.X(.96), h.Y(.3)), (h.X(.96), h.Y(.88)), (h.X(.04), h.Y(.88))]), sc(LT, .9), STUFF, 1.1)
    c.add(rect((h.X(.12), h.Y(.45), h.X(.88), h.Y(.53))), BLACK, FIXED, 0)
    c.add(rect((h.X(.16), h.Y(.475), h.X(.84), h.Y(.505))), GLOW, FIXED, 0)
    for k in range(4):
        x = h.X(.32 + k * .12)
        c.add(rect((x - .5, h.Y(.64), x + .5, h.Y(.82))), BLACK, FIXED, 0)


HEAD_ITEMS = {"SquireHelm": breather_helm, "KnightHelm": knight_helm, "BastionHelm": bastion_helm}
COLORS = {"SquireArmor": (118, 124, 108), "SquireHelm": (118, 124, 108), "KnightArmor": (148, 40, 38), "KnightHelm": (148, 40, 38), "OathArmor": (56, 56, 62),
          "ArcanistArmor": (78, 70, 136), "ArtificerArmor": (168, 133, 87), "BastionArmor": (102, 105, 115), "BastionHelm": (102, 105, 115)}
HERALD_PREVIEW = (150, 112, 74)
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
                fn(c, Fit(body, view))
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
