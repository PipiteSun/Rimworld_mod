# 1.45.1 魔法盔甲衣服：穿着贴图（五体型 × 南/东/北，256px）+ _m 遮罩（红=材料色，绿=纹章色，黑=固定色）+ 图标（128px，带遮罩）。
# 画法沿用 draw_race_art 的 RimWorld 风格：粗黑轮廓、平涂、一层暗面；着色区域画成浅灰，由游戏按材料色 / 纹章色相乘。
# 身体框取自 draw_races_124.py 量好的原版 Naked_* 画框（128px 坐标）；1.7 的身体是梨形一团，衣服按同样的梨形外扩。
# 用法：python -I ArtSource/RaceArt/draw_apparel_145.py <模组根目录>
import math, os, sys
from PIL import Image, ImageDraw, ImageFilter

ROOT = sys.argv[1] if len(sys.argv) > 1 else os.getcwd()
TEX = os.path.join(ROOT, "Textures")
PREVIEW = os.path.join(ROOT, "ArtSource", "Preview", "Apparel-1.45.1.png")
S = 4            # 工作分辨率：128 坐标 × 4 = 512px，存成 256
INK = (34, 30, 30)
LT, MD, DK = (232, 232, 230), (198, 198, 196), (158, 158, 156)   # 着色区的三档灰
STUFF, HERALD, FIXED = (255, 0, 0), (0, 255, 0), (0, 0, 0)

BODY = {  # 原版 Naked_* 身体画框（128px）
    "Male": {"south": (38, 43, 90, 108), "east": (42, 43, 86, 107), "north": (38, 43, 90, 108)},
    "Female": {"south": (40, 38, 88, 113), "east": (36, 39, 82, 113), "north": (40, 38, 88, 113)},
    "Thin": {"south": (50, 43, 77, 109), "east": (42, 44, 77, 108), "north": (50, 43, 77, 109)},
    "Hulk": {"south": (25, 32, 102, 127), "east": (27, 36, 95, 125), "north": (25, 32, 102, 127)},
    "Fat": {"south": (23, 38, 103, 113), "east": (22, 38, 100, 115), "north": (24, 38, 104, 113)},
}
HEAD = {"south": (41, 39, 87, 89), "east": (43, 39, 88, 89), "north": (41, 39, 87, 89)}
BODIES = ["Male", "Female", "Thin", "Fat", "Hulk"]
VIEWS = ["south", "east", "north"]


class Canvas:
    def __init__(self):
        self.col = Image.new("RGBA", (128 * S, 128 * S), (0, 0, 0, 0))
        self.msk = Image.new("RGB", (128 * S, 128 * S), (0, 0, 0))

    def _mask(self, parts):
        m = Image.new("L", self.col.size, 0)
        d = ImageDraw.Draw(m)
        for part in parts:
            kind, a = part[0], part[1]
            neg = kind.startswith("-")
            kind = kind.lstrip("-")
            v = 0 if neg else 255
            if kind == "poly":
                d.polygon([(x * S, y * S) for x, y in a], fill=v)
            elif kind == "ell":
                d.ellipse([a[0] * S, a[1] * S, a[2] * S, a[3] * S], fill=v)
            elif kind == "rect":
                d.rectangle([a[0] * S, a[1] * S, a[2] * S, a[3] * S], fill=v)
            elif kind == "line":
                w = part[2] * S
                d.line([(x * S, y * S) for x, y in a], fill=v, width=max(1, int(w)), joint="curve")
                for x, y in (a[0], a[-1]):
                    d.ellipse([x * S - w / 2, y * S - w / 2, x * S + w / 2, y * S + w / 2], fill=v)
        return m

    def add(self, parts, color, chan=FIXED, ow=1.4):
        if parts and isinstance(parts[0], str):
            parts = [parts]
        m = self._mask(parts)
        if ow:
            k = max(3, int(ow * S) | 1)
            o = m.filter(ImageFilter.MaxFilter(k))
            self.col.paste(Image.new("RGBA", self.col.size, INK + (255,)), (0, 0), o)
            self.msk.paste(Image.new("RGB", self.col.size, FIXED), (0, 0), o)
        self.col.paste(Image.new("RGBA", self.col.size, color + (255,)), (0, 0), m)
        self.msk.paste(Image.new("RGB", self.col.size, chan), (0, 0), m)

    def line(self, pts, w, color=INK, chan=FIXED):
        self.add(("line", pts, w), color, chan, 0)

    def save(self, rel, size=256, mask=True):
        path = os.path.join(TEX, rel.replace("/", os.sep))
        os.makedirs(os.path.dirname(path), exist_ok=True)
        self.col.resize((size, size), Image.LANCZOS).save(path + ".png")
        if mask:
            self.msk.resize((size, size), Image.NEAREST).save(path + "_m.png")
        return path

    def icon(self, rel):
        box = self.col.split()[3].getbbox()
        if not box:
            return self.save(rel, 128)
        x0, y0, x1, y1 = box
        side = int(max(x1 - x0, y1 - y0) * 1.12)
        cx, cy = (x0 + x1) // 2, (y0 + y1) // 2
        crop = (cx - side // 2, cy - side // 2, cx - side // 2 + side, cy - side // 2 + side)
        path = os.path.join(TEX, rel.replace("/", os.sep))
        os.makedirs(os.path.dirname(path), exist_ok=True)
        c = Image.new("RGBA", (side, side), (0, 0, 0, 0)); c.paste(self.col.crop(crop), (0, 0))
        m = Image.new("RGB", (side, side), (0, 0, 0)); m.paste(self.msk.crop(crop), (0, 0))
        c.resize((128, 128), Image.LANCZOS).save(path + ".png")
        m.resize((128, 128), Image.NEAREST).save(path + "_m.png")
        return path


def poly(p):
    return ("poly", p)


def ell(b):
    return ("ell", b)


def rect(b):
    return ("rect", b)


# ---------------- 身体几何 ----------------
HW = [(0.0, .30), (.08, .40), (.22, .45), (.45, .48), (.62, .50), (.8, .48), (.93, .40), (1.0, .26)]


def hw(t):
    for (a, wa), (b, wb) in zip(HW, HW[1:]):
        if a <= t <= b:
            return wa + (wb - wa) * (t - a) / (b - a)
    return HW[-1][1]


class Fit:
    def __init__(self, body, view):
        self.body, self.view = body, view
        self.x0, self.y0, self.x1, self.y1 = BODY[body][view]
        self.w, self.h = self.x1 - self.x0, self.y1 - self.y0
        self.cx = (self.x0 + self.x1) / 2
        self.east = view == "east"
        self.female = body == "Female"

    def y(self, t):
        return self.y0 + self.h * t

    def x(self, t, side, e=.06):  # side -1 左 / +1 右
        return self.cx + side * hw(t) * self.w * (1 + e)

    def shape(self, t0, t1, e=.06, flare=0.0, steps=10, round_bottom=False):
        left, right = [], []
        for i in range(steps + 1):
            t = t0 + (t1 - t0) * i / steps
            extra = flare * max(0.0, (t - t0) / max(1e-6, t1 - t0)) ** 2
            left.append((self.cx - hw(t) * self.w * (1 + e + extra), self.y(t)))
            right.append((self.cx + hw(t) * self.w * (1 + e + extra), self.y(t)))
        if round_bottom and t1 >= .98:
            return left + [(self.cx - .12 * self.w, self.y(1.0) + 1.5), (self.cx + .12 * self.w, self.y(1.0) + 1.5)] + right[::-1]
        return left + right[::-1]

    def half(self, t0, t1, e=.06, flare=0.0, steps=10):
        """右半边（南/北）或后半边（东向的左边）用作暗面。"""
        pts = self.shape(t0, t1, e, flare, steps)
        n = len(pts) // 2
        left, right = pts[:n], pts[n:][::-1]
        if self.east:
            return left + [(self.cx - self.w * .05, self.y(t1)), (self.cx - self.w * .05, self.y(t0))]
        return [(self.cx + self.w * .1, self.y(t0))] + right + [(self.cx + self.w * .1, self.y(t1))]


def torso(c, f, t0, t1, chan=STUFF, col=LT, e=.06, flare=0.0, shade=True, ow=1.4):
    c.add(poly(f.shape(t0, t1, e, flare)), col, chan, ow)
    if shade:
        c.add(poly(f.half(t0, t1, e, flare)), DK if col == LT else tuple(int(v * .78) for v in col), chan, 0)


def band(c, f, t, thick, color, chan=FIXED, e=.07):
    pts = f.shape(t - thick / 2, t + thick / 2, e)
    c.add(poly(pts), color, chan, 1.0)


def pauldrons(c, f, size, chan=STUFF, col=MD, spikes=False, curl=False):
    r = f.w * size
    ty = f.y(.06)
    if f.east:
        cx = f.cx + f.w * .02
        c.add(ell((cx - r, ty - r * .7, cx + r, ty + r * .9)), col, chan, 1.4)
        c.add(ell((cx - r * .1, ty - r * .6, cx + r, ty + r * .8)), DK, chan, 0)
        if spikes:
            for k in (-1, 0, 1):
                bx = cx + k * r * .5
                c.add(poly([(bx - r * .22, ty - r * .4), (bx + r * .05, ty - r * 1.35), (bx + r * .22, ty - r * .4)]), MD, chan, 1.0)
        return
    for s in (-1, 1):
        cx = f.cx + s * f.w * .42
        c.add(ell((cx - r, ty - r * .7, cx + r, ty + r * .9)), col, chan, 1.4)
        if s == 1:
            c.add(ell((cx - r * .2, ty - r * .6, cx + r, ty + r * .8)), DK, chan, 0)
        if curl:
            c.line([(cx - r * .8, ty + r * .55), (cx + r * .8, ty + r * .55)], 1.0, (120, 120, 118), chan)
        if spikes:
            for k in (-1, 0, 1):
                bx = cx + k * r * .5
                c.add(poly([(bx - r * .22, ty - r * .4), (bx + s * r * .25, ty - r * 1.35), (bx + r * .22, ty - r * .4)]), MD, chan, 1.0)


def tabard(c, f, t0, t1, width, chan=HERALD, col=LT, notch=True):
    if f.view == "east":
        x = f.cx + f.w * .30
        pts = [(x - width * f.w * .25, f.y(t0)), (x + width * f.w * .25, f.y(t0)), (x + width * f.w * .35, f.y(t1)), (x - width * f.w * .2, f.y(t1))]
    else:
        hwid = width * f.w / 2
        pts = [(f.cx - hwid, f.y(t0)), (f.cx + hwid, f.y(t0)), (f.cx + hwid * 1.1, f.y(t1)), (f.cx, f.y(t1) - (2.5 if notch else 0)), (f.cx - hwid * 1.1, f.y(t1))]
    c.add(poly(pts), col, chan, 1.2)
    if f.view != "east":
        c.add(poly([(f.cx + width * f.w * .12, f.y(t0)), (f.cx + width * f.w / 2, f.y(t0)), (f.cx + width * f.w * .55, f.y(t1)), (f.cx + width * f.w * .12, f.y(t1) - 1)]), MD, chan, 0)


def sun(c, cx, cy, r, color=(222, 186, 92), chan=FIXED):
    pts = []
    for i in range(16):
        rr = r if i % 2 == 0 else r * .45
        a = math.radians(-90 + i * 22.5)
        pts.append((cx + rr * math.cos(a), cy + rr * math.sin(a)))
    c.add(poly(pts), color, chan, .9)


def chest_xy(f, t=.3):
    if f.east:
        return f.cx + f.w * .26, f.y(t)
    return f.cx, f.y(t)


def cape(c, f, t1=1.0, chan=HERALD, col=LT, lining=None, ragged=False, clasp=(222, 186, 92), lining_chan=FIXED):
    """披风：南向只露两侧边缘和肩扣，北向整面，东向挂在背后（左边）。"""
    if f.view == "south":
        for s in (-1, 1):
            x = f.cx + s * hw(.1) * f.w * 1.1
            side = [(x, f.y(.02)), (x + s * f.w * .13, f.y(.3)), (f.cx + s * hw(.7) * f.w * 1.18, f.y(t1 * .98)), (f.cx + s * hw(.7) * f.w * 1.02, f.y(t1 * .98)), (x - s * f.w * .02, f.y(.2))]
            c.add(poly(side), lining or DK, lining_chan if lining else chan, 1.2)
            c.add(ell((x - s * f.w * .06 - 2.2, f.y(.04) - 2.2, x - s * f.w * .06 + 2.2, f.y(.04) + 2.2)), clasp, FIXED, .9)
    elif f.view == "north":
        pts = [(f.cx - hw(.05) * f.w * 1.1, f.y(.0)), (f.cx + hw(.05) * f.w * 1.1, f.y(.0)), (f.cx + hw(.8) * f.w * 1.2, f.y(t1)), (f.cx - hw(.8) * f.w * 1.2, f.y(t1))]
        if ragged:
            bot = []
            n = 7
            for i in range(n + 1):
                x = f.cx + hw(.8) * f.w * 1.2 - i * (2 * hw(.8) * f.w * 1.2) / n
                bot.append((x, f.y(t1) - (0 if i % 2 == 0 else f.h * .1)))
            pts = pts[:2] + bot
        c.add(poly(pts), col, chan, 1.4)
        c.add(poly([(f.cx + f.w * .1, f.y(0)), pts[1], pts[2], (f.cx + f.w * .14, f.y(t1))]), MD, chan, 0)
    else:
        bx = f.cx - f.w * .18
        pts = [(f.cx + f.w * .05, f.y(.0)), (bx - f.w * .12, f.y(.05)), (f.x0 - f.w * .14, f.y(t1 * .97)), (f.cx - f.w * .02, f.y(t1 * .97)), (bx + f.w * .08, f.y(.4))]
        c.add(poly(pts), col, chan, 1.4)
        c.add(poly([(bx - f.w * .12, f.y(.05)), (f.x0 - f.w * .14, f.y(t1 * .97)), (f.x0 + f.w * .05, f.y(t1 * .97))]), MD, chan, 0)


# ================= 身体衣甲 =================
def paladin_plate(c, f):
    torso(c, f, .0, 1.0, STUFF, LT)
    cx, cy = chest_xy(f)
    if f.view != "north":
        c.line([(cx, f.y(.08)), (cx, f.y(.45))], 1.2, (150, 150, 148), STUFF)
        sun(c, cx, cy, f.w * .1)
        tabard(c, f, .48, .95, .42)
    else:
        tabard(c, f, .5, .97, .55)
        c.add(rect((f.cx - 3, f.y(.46) - 1.5, f.cx + 3, f.y(.46) + 1.5)), (222, 186, 92), FIXED, .8)
    band(c, f, .47, .05, (112, 82, 54))
    pauldrons(c, f, .23 if f.body != "Female" else .2, curl=True)


def knight_mantle(c, f):
    cape(c, f, 1.0, HERALD, LT, lining=(130, 40, 44))
    if f.view == "north":
        sun(c, f.cx, f.y(.35), f.w * .14, (222, 186, 92))


def chain_shirt(c, f):
    torso(c, f, .02, .82, STUFF, MD)
    for i in range(6):
        t = .1 + i * .11
        pts = f.shape(t, t + .001, .0)
        c.line([pts[0], pts[-1]], .5, (150, 150, 148), STUFF)
    band(c, f, .03, .04, (110, 80, 52))
    band(c, f, .62, .05, (90, 66, 44))


def dwarven_runeplate(c, f):
    torso(c, f, .0, 1.0, STUFF, LT, e=.1)
    for t in (.25, .5, .75):
        band(c, f, t, .03, (176, 132, 70), e=.11)
    cx, cy = chest_xy(f, .36)
    if f.view != "north":
        for k in (-1, 0, 1):
            x = cx + k * f.w * (.14 if not f.east else .07)
            c.add(poly([(x, cy - 3.2), (x + 2.2, cy), (x, cy + 3.2), (x - 2.2, cy)]), (170, 220, 240), FIXED, .8)
    # 方肩甲（垛口）
    r = f.w * .22
    for s in ((-1, 1) if not f.east else (0,)):
        x = f.cx + s * f.w * .42 if not f.east else f.cx
        c.add(poly([(x - r, f.y(.0)), (x + r, f.y(.0)), (x + r, f.y(.16)), (x - r, f.y(.16))]), MD, STUFF, 1.4)
        for k in (-1, 1):
            c.add(rect((x + k * r * .55 - r * .25, f.y(.0) - 3, x + k * r * .55 + r * .25, f.y(.0) + .5)), MD, STUFF, 1.0)
        c.add(rect((x - r, f.y(.13), x + r, f.y(.16))), (176, 132, 70), FIXED, .6)


def ranger_leathers(c, f):
    torso(c, f, .02, .78, STUFF, MD)
    if f.view != "north":
        x0, x1 = f.x(.1, -1), f.x(.1, 1)
        c.line([(x0 + 2, f.y(.08)), (x1 - 2, f.y(.6))], 1.8, (90, 64, 40))
        c.line([(x1 - 2, f.y(.08)), (x0 + 2, f.y(.6))], 1.8, (90, 64, 40))
    else:
        c.add(poly([(f.cx - 3, f.y(.05)), (f.cx + 4, f.y(.0)), (f.cx + 7, f.y(.5)), (f.cx, f.y(.52))]), (110, 80, 50), FIXED, 1.2)  # 箭袋
        for k in range(3):
            c.line([(f.cx + 1 + k * 1.6, f.y(.0)), (f.cx + 1 + k * 1.6, f.y(-.08))], .8, (200, 190, 160))
    band(c, f, .62, .06, (96, 120, 72))
    r = f.w * .2
    sx = f.cx - f.w * .42 if not f.east else f.cx
    c.add(ell((sx - r, f.y(.0) - r * .6, sx + r, f.y(.0) + r * .9)), MD, STUFF, 1.2)


def woodwalker_cloak(c, f):
    def leaves(pts, t1):
        return pts
    if f.view == "north":
        pts = [(f.cx - hw(.05) * f.w * 1.15, f.y(-.06)), (f.cx + hw(.05) * f.w * 1.15, f.y(-.06))]
        n = 8
        for i in range(n + 1):
            x = f.cx + hw(.8) * f.w * 1.22 - i * (2 * hw(.8) * f.w * 1.22) / n
            pts.append((x, f.y(.92) + (0 if i % 2 == 0 else f.h * .07)))
        c.add(poly(pts), LT, STUFF, 1.4)
        c.add(poly([(f.cx + f.w * .1, f.y(-.06)), pts[1], pts[2], (f.cx + f.w * .14, f.y(.92))]), MD, STUFF, 0)
        c.add(ell((f.cx - f.w * .26, f.y(-.2), f.cx + f.w * .26, f.y(.12))), MD, STUFF, 1.2)  # 背后的兜帽
    elif f.view == "south":
        for s in (-1, 1):
            x = f.cx + s * hw(.1) * f.w * 1.1
            side = [(x, f.y(.0)), (x + s * f.w * .14, f.y(.3)), (f.cx + s * hw(.7) * f.w * 1.2, f.y(.92)), (f.cx + s * hw(.7) * f.w * 1.04, f.y(.86)), (x - s * f.w * .03, f.y(.2))]
            c.add(poly(side), MD, STUFF, 1.2)
        c.add(poly([(f.cx - f.w * .28, f.y(.0)), (f.cx + f.w * .28, f.y(.0)), (f.cx + f.w * .2, f.y(.12)), (f.cx, f.y(.18)), (f.cx - f.w * .2, f.y(.12))]), LT, STUFF, 1.2)
        c.add(ell((f.cx - 1.8, f.y(.09) - 1.8, f.cx + 1.8, f.y(.09) + 1.8)), (170, 150, 90), FIXED, .8)
    else:
        cape(c, f, .92, STUFF, LT)
        c.add(ell((f.cx - f.w * .3, f.y(-.18), f.cx + f.w * .1, f.y(.12))), MD, STUFF, 1.2)


def shadowweave_cloak(c, f):
    torso(c, f, .0, .9, STUFF, MD, e=.1, flare=.08)
    if f.view != "north":
        cx, cy = chest_xy(f, .0)
        c.add(poly([(cx - f.w * .26, f.y(-.04)), (cx + f.w * .26, f.y(-.04)), (cx + f.w * .2, f.y(.12)), (cx - f.w * .2, f.y(.12))]), DK, STUFF, 1.2)  # 高立领
        c.add(poly([(f.x(.15, -1), f.y(.08)), (f.cx + f.w * .1, f.y(.3)), (f.x(.4, -1), f.y(.45))]), LT, STUFF, 1.0)  # 斜披肩
        c.line([(f.cx + f.w * .1, f.y(.55)), (f.cx + f.w * .3, f.y(.75))], 1.6, (80, 60, 44))
        c.line([(f.cx + f.w * .18, f.y(.52)), (f.cx + f.w * .38, f.y(.72))], 1.6, (80, 60, 44))
    if f.view == "east":
        c.add(poly([(f.x0 + f.w * .05, f.y(.5)), (f.x0 + f.w * .2, f.y(.5)), (f.x0 + f.w * .12, f.y(.88))]), (140, 34, 38), FIXED, .8)  # 走动露出的红里衬


def robe(c, f, chan=STUFF, col=LT, t1=1.0, flare=.18):
    torso(c, f, .0, t1, chan, col, e=.08, flare=flare)


def archmage_robe(c, f):
    robe(c, f)
    band(c, f, .97, .04, (220, 222, 230), e=.08 + .18)
    if f.view != "north":
        cx, cy = chest_xy(f, .0)
        c.add(poly([(cx - f.w * .3, f.y(-.03)), (cx + f.w * .3, f.y(-.03)), (cx + f.w * .16, f.y(.1)), (cx - f.w * .16, f.y(.1))]), MD, STUFF, 1.2)
        c.line([(cx, f.y(.1)), (cx, f.y(.9))], 1.6, (214, 216, 226))
        for k in range(3):
            y = f.y(.35 + k * .2)
            c.add(poly([(cx - 1.4, y), (cx, y - 1.8), (cx + 1.4, y), (cx, y + 1.8)]), (236, 238, 245), FIXED, .5)
    else:
        for k in range(4):
            x = f.cx - f.w * .3 + k * f.w * .2
            c.add(ell((x - 1.2, f.y(.9) - 1.2, x + 1.2, f.y(.9) + 1.2)), (236, 238, 245), FIXED, .4)


def red_robe(c, f):
    robe(c, f, STUFF, LT)
    band(c, f, .45, .06, (40, 34, 34), e=.12)
    band(c, f, .95, .05, (40, 34, 34), e=.08 + .18)
    for k in range(5):
        x = f.cx - f.w * .4 + k * f.w * .2
        c.add(poly([(x - 2, f.y(.97)), (x, f.y(.92)), (x + 2, f.y(.97))]), (210, 170, 70), FIXED, 0)
    # 尖角硬肩披肩（披风层）
    r = f.w * .3
    if f.east:
        c.add(poly([(f.cx - r, f.y(.02)), (f.cx + r * .6, f.y(.0)), (f.cx + r * .5, f.y(.2)), (f.cx - r * 1.3, f.y(.26)), (f.cx - r * 1.2, f.y(-.12))]), (52, 46, 48), FIXED, 1.4)
    else:
        pts = [(f.cx - f.w * .62, f.y(-.1)), (f.cx - f.w * .3, f.y(.02)), (f.cx, f.y(.06)), (f.cx + f.w * .3, f.y(.02)), (f.cx + f.w * .62, f.y(-.1)), (f.cx + f.w * .5, f.y(.22)), (f.cx, f.y(.26 if f.view == "north" else .18)), (f.cx - f.w * .5, f.y(.22))]
        c.add(poly(pts), (52, 46, 48), FIXED, 1.4)
        if f.view == "north":
            c.add(poly([(f.cx - f.w * .45, f.y(.2)), (f.cx + f.w * .45, f.y(.2)), (f.cx + f.w * .5, f.y(.95)), (f.cx - f.w * .5, f.y(.95))]), (46, 40, 42), FIXED, 1.2)


def morninglord(c, f):
    robe(c, f, STUFF, LT)
    gold = (222, 186, 92)
    if f.view == "south":
        for s in (-1, 1):
            x = f.cx + s * f.w * .16
            c.add(poly([(x - 2.2, f.y(.0)), (x + 2.2, f.y(.0)), (x + 2.6, f.y(.85)), (x - 2.6, f.y(.85))]), gold, FIXED, 1.0)
            sun(c, x, f.y(.82), 2.4, (250, 230, 160))
        sun(c, f.cx, f.y(.28), f.w * .12, gold)
        band(c, f, .5, .04, (190, 60, 50), e=.14)
    elif f.view == "north":
        for i in range(12):
            a = math.radians(-180 + i * 15)
            c.line([(f.cx, f.y(.4)), (f.cx + math.cos(a) * f.w * .45, f.y(.4) + math.sin(a) * f.w * .45)], 1.2, gold)
        sun(c, f.cx, f.y(.4), f.w * .1, gold)
    else:
        x = f.cx + f.w * .22
        c.add(poly([(x - 2, f.y(.0)), (x + 2, f.y(.0)), (x + 2.5, f.y(.85)), (x - 2.5, f.y(.85))]), gold, FIXED, 1.0)
        band(c, f, .5, .04, (190, 60, 50), e=.14)


def cleric_breastplate(c, f):
    torso(c, f, .02, .55, STUFF, LT)
    band(c, f, .03, .035, (222, 186, 92))
    band(c, f, .54, .035, (222, 186, 92))
    if f.view != "north":
        cx, cy = chest_xy(f, .28)
        sun(c, cx, cy, f.w * .12, (222, 186, 92))


def deathknight_plate(c, f):
    dark = (120, 120, 118)
    torso(c, f, .0, 1.0, STUFF, dark)
    if f.view != "north":
        cx, cy = chest_xy(f, .3)
        c.line([(cx - 1, f.y(.12)), (cx + 1.5, f.y(.25)), (cx - 1, f.y(.38)), (cx + 1, f.y(.5))], 1.4, (110, 230, 140))
        tabard(c, f, .5, .97, .38, FIXED, (60, 56, 60))
    pauldrons(c, f, .24, STUFF, (140, 140, 138), spikes=True)
    if f.view != "north":
        sx = f.cx - f.w * .42 if not f.east else f.cx
        c.add(ell((sx - 2.4, f.y(.12) - 2.4, sx + 2.4, f.y(.12) + 2.4)), (230, 222, 196), FIXED, .8)
    if f.view != "south":
        cape(c, f, 1.0, FIXED, (52, 48, 54), ragged=True)
    band(c, f, .48, .05, (60, 56, 60))


def lich_shroud(c, f):
    robe(c, f, STUFF, LT, flare=.24)
    for t in (.3, .5, .7):
        pts = f.shape(t, t + .001, .1)
        c.line([pts[0], (f.cx, pts[0][1] + 2), pts[-1]], .7, (160, 150, 170), STUFF)
    band(c, f, .02, .04, (190, 160, 90))
    if f.view != "north":
        cx, cy = chest_xy(f, .3)
        c.add(rect((cx - 2.4, cy - 3, cx + 2.4, cy + 3)), (90, 60, 120), FIXED, .9)
        c.line([(cx - 2, f.y(.02)), (cx, cy - 3), (cx + 2, f.y(.02))], .6, (190, 160, 90))
    # 破碎下摆
    for k in range(6):
        x = f.cx - f.w * .55 + k * f.w * .22
        c.add(poly([(x - 2.4, f.y(.97)), (x, f.y(1.04)), (x + 2.4, f.y(.97))]), MD, STUFF, .8)


def dragonknight_armor(c, f):
    torso(c, f, .0, 1.0, STUFF, LT)
    rows = 6
    for r in range(rows):
        t = .08 + r * .14
        pts = f.shape(t, t + .001, .02)
        x0, x1 = pts[0][0], pts[-1][0]
        n = max(3, int((x1 - x0) / 5))
        for i in range(n):
            x = x0 + (x1 - x0) * (i + .5 * (r % 2)) / n
            c.add(("line", [(x - 2, f.y(t)), (x, f.y(t) + 2.5), (x + 2, f.y(t))], .6), (140, 140, 170), STUFF, 0)
    # 龙翼肩甲
    if f.east:
        c.add(poly([(f.cx, f.y(.0)), (f.cx - f.w * .6, f.y(-.18)), (f.cx - f.w * .5, f.y(.0)), (f.cx - f.w * .3, f.y(.12))]), MD, STUFF, 1.2)
    else:
        for s in (-1, 1):
            x = f.cx + s * f.w * .36
            c.add(poly([(x, f.y(.0)), (x + s * f.w * .34, f.y(-.16)), (x + s * f.w * .3, f.y(.02)), (x + s * f.w * .22, f.y(.1)), (x - s * f.w * .05, f.y(.12))]), MD, STUFF, 1.2)
    band(c, f, .5, .04, (110, 112, 120))


def ember_collar(c, f):
    torso(c, f, .0, .45, STUFF, MD, e=.05)
    band(c, f, .02, .07, (60, 56, 56))
    if f.view != "north":
        cx, cy = chest_xy(f, .25)
        c.add(poly([(cx, cy - 4), (cx + 2.4, cy), (cx + 1, cy + 3), (cx - 1, cy + 3), (cx - 2.4, cy)]), (220, 110, 50), FIXED, .8)
    for t in (.15, .35):
        pts = f.shape(t, t + .001, .05)
        for x in (pts[0][0] + 2, pts[-1][0] - 2):
            c.add(ell((x - 1, f.y(t) - 1, x + 1, f.y(t) + 1)), (90, 86, 86), FIXED, 0)


def loxodon_armor(c, f):
    torso(c, f, .0, 1.0, STUFF, LT, e=.1)
    for t in (.3, .6):
        band(c, f, t, .03, (200, 170, 100), e=.11)
    if f.view != "north":
        cx, cy = chest_xy(f, .42)
        for r in (5, 3):
            c.add([ell((cx - r, cy - r, cx + r, cy + r)), ("-ell", (cx - r + 1.2, cy - r + 1.2, cx + r - 1.2, cy + r - 1.2))], (200, 170, 100), FIXED, 0)
    # 象耳肩甲
    r = f.w * .3
    for s in ((-1, 1) if not f.east else (0,)):
        x = f.cx + s * f.w * .4 if not f.east else f.cx - f.w * .05
        c.add(ell((x - r * .8, f.y(-.05), x + r * .8, f.y(.3))), MD, STUFF, 1.4)
        c.add(ell((x - r * .5, f.y(.0), x + r * .5, f.y(.25))), (200, 170, 100), FIXED, 0)


def ogre_hide(c, f):
    torso(c, f, .02, .8, STUFF, LT, e=.08)
    for k in range(4):
        pts = f.shape(.2 + k * .15, .2 + k * .15 + .001, .08)
        c.line([pts[0], pts[-1]], .7, (140, 120, 100), STUFF)
    sx = f.cx - f.w * .42 if not f.east else f.cx
    c.add(ell((sx - f.w * .18, f.y(-.06), sx + f.w * .18, f.y(.16))), (230, 222, 196), FIXED, 1.2)
    c.add(ell((sx - 3, f.y(.03) - 1.5, sx - 1, f.y(.03) + .5)), INK, FIXED, 0)
    c.add(ell((sx + 1, f.y(.03) - 1.5, sx + 3, f.y(.03) + .5)), INK, FIXED, 0)
    if f.view != "north":
        for k in range(5):
            x = f.cx - f.w * .3 + k * f.w * .15
            c.add(ell((x - 1.4, f.y(.66) - 1.4, x + 1.4, f.y(.66) + 1.4)), (230, 222, 196), FIXED, .6)


def skyguard_harness(c, f):
    brown = (150, 112, 70)
    if f.view == "north":
        c.line([(f.x(.1, -1), f.y(.05)), (f.x(.55, 1), f.y(.55))], 2.2, brown)
        c.line([(f.x(.1, 1), f.y(.05)), (f.x(.55, -1), f.y(.55))], 2.2, brown)
        band(c, f, .55, .05, brown)
        return
    torso(c, f, .05, .45, STUFF, LT, e=.0)
    c.line([(f.x(.1, -1), f.y(.05)), (f.x(.55, 1), f.y(.55))], 2.0, brown)
    c.line([(f.x(.1, 1), f.y(.05)), (f.x(.55, -1), f.y(.55))], 2.0, brown)
    cx, cy = chest_xy(f, .25)
    c.add(poly([(cx, cy - 2), (cx + 5, cy - 4), (cx + 2, cy + 1), (cx, cy + 3), (cx - 2, cy + 1), (cx - 5, cy - 4)]), (110, 160, 200), FIXED, .8)


def celestial_panoply(c, f):
    if f.view != "south":
        # 背后的日轮
        cx = f.cx - (f.w * .2 if f.east else 0)
        r = f.w * .55
        c.add([ell((cx - r, f.y(-.3), cx + r, f.y(-.3) + 2 * r)), ("-ell", (cx - r + 3, f.y(-.3) + 3, cx + r - 3, f.y(-.3) + 2 * r - 3))], (230, 196, 100), FIXED, 1.2)
    torso(c, f, .0, .6, STUFF, LT)
    if f.view != "north":
        cx, cy = chest_xy(f, .25)
        for s in (-1, 1):
            c.add(ell((cx + s * f.w * .12 - f.w * .12, cy - f.w * .1, cx + s * f.w * .12 + f.w * .12, cy + f.w * .1)), MD, STUFF, .8)
    n = 7
    pts = f.shape(.6, .95, .08)
    x0, x1 = f.x(.6, -1, .1), f.x(.6, 1, .1)
    for i in range(n):
        xa = x0 + (x1 - x0) * i / n
        xb = x0 + (x1 - x0) * (i + 1) / n - .6
        c.add(poly([(xa, f.y(.6)), (xb, f.y(.6)), (xb + .3, f.y(.92)), (xa - .3, f.y(.92))]), (190, 150, 90), FIXED, .8)
    band(c, f, .6, .04, (230, 196, 100))
    if f.view == "south":
        r = f.w * .55
        c.add([ell((f.cx - r, f.y(-.3), f.cx + r, f.y(-.3) + 2 * r)), ("-ell", (f.cx - r + 3, f.y(-.3) + 3, f.cx + r - 3, f.y(-.3) + 2 * r - 3)), ("-rect", (f.cx - r * .85, f.y(-.32), f.cx + r * .85, f.y(.8)))], (230, 196, 100), FIXED, 1.2)


def elemental_weave(c, f):
    robe(c, f, STUFF, LT)
    for k in range(5):
        t = .55 + k * .09
        pts = f.shape(t, t + .001, .1 + .18 * ((t - 0) ** 2))
        n = 6
        for i in range(n):
            x = pts[0][0] + (pts[-1][0] - pts[0][0]) * (i + .5 * (k % 2)) / n
            c.add(poly([(x - 2, f.y(t) + 1.5), (x, f.y(t) - 2.5), (x + 2, f.y(t) + 1.5)]), LT, HERALD, 0)
    band(c, f, .03, .05, LT, HERALD)


def adventurer(c, f):
    torso(c, f, .02, .78, STUFF, LT, e=.04)
    if f.view != "north":
        vx = .32 if not f.east else .18
        for s in ((-1, 1) if not f.east else (1,)):
            x = f.cx + s * f.w * vx
            c.add(poly([(f.x(.05, s, .05), f.y(.02)), (x, f.y(.05)), (x - s * f.w * .02, f.y(.7)), (f.x(.7, s, .05), f.y(.7))]), (120, 86, 56), FIXED, 1.0)
    else:
        torso(c, f, .02, .7, FIXED, (120, 86, 56), e=.05, shade=False)
    band(c, f, .62, .06, (90, 64, 40))
    for k, x in enumerate((f.cx - f.w * .25, f.cx + f.w * .2)):
        if f.view == "north" and k == 0:
            continue
        c.add(rect((x - 2.4, f.y(.66), x + 2.4, f.y(.76))), (150, 110, 70), FIXED, .8)


def bard_finery(c, f):
    torso(c, f, .02, .75, STUFF, LT, e=.06)
    for s in ((-1, 1) if not f.east else (0,)):
        x = f.cx + s * f.w * .44 if not f.east else f.cx
        c.add(ell((x - f.w * .18, f.y(-.02), x + f.w * .18, f.y(.22))), MD, STUFF, 1.2)
        for k in (-1, 1):
            c.line([(x + k * 2, f.y(.0)), (x + k * 2, f.y(.18))], 1.0, (230, 196, 100))
    if f.view != "north":
        c.line([(f.x(.05, -1), f.y(.05)), (f.x(.6, 1), f.y(.65))], 1.6, (110, 70, 40))
    else:
        c.add(ell((f.cx - 4, f.y(.15), f.cx + 6, f.y(.55))), (160, 110, 60), FIXED, 1.0)
        c.line([(f.cx + 1, f.y(.15)), (f.cx + 6, f.y(-.05))], 1.2, (110, 70, 40))
    band(c, f, .7, .05, (230, 196, 100))


def court_attire(c, f):
    if f.female:
        torso(c, f, .0, 1.0, STUFF, LT, e=.06, flare=.28)
        band(c, f, .4, .06, (230, 196, 100), e=.07)
        for t in (.6, .75, .9):
            pts = f.shape(t, t + .001, .06 + .28 * ((t) ** 2))
            c.line([pts[0], pts[-1]], .7, (150, 150, 148), STUFF)
        r = f.w * .32
        if not f.east:
            c.add(poly([(f.cx - f.w * .55, f.y(.0)), (f.cx + f.w * .55, f.y(.0)), (f.cx + f.w * .5, f.y(.18)), (f.cx - f.w * .5, f.y(.18))]), MD, STUFF, 1.2)
    else:
        torso(c, f, .0, .95, STUFF, LT, e=.07, flare=.06)
        if f.view != "north":
            for k in range(4):
                for s in (-1, 1):
                    x = f.cx + s * f.w * .12
                    c.add(ell((x - 1.2, f.y(.15 + k * .12) - 1.2, x + 1.2, f.y(.15 + k * .12) + 1.2)), (230, 196, 100), FIXED, .4)
            c.line([(f.cx, f.y(.05)), (f.cx, f.y(.95))], .8, (150, 150, 148), STUFF)
        for s in ((-1, 1) if not f.east else (0,)):
            x = f.cx + s * f.w * .42 if not f.east else f.cx
            c.add(rect((x - f.w * .12, f.y(-.01), x + f.w * .12, f.y(.05))), (230, 196, 100), FIXED, .8)
            for k in range(3):
                c.line([(x - f.w * .1 + k * f.w * .1, f.y(.05)), (x - f.w * .1 + k * f.w * .1, f.y(.12))], .8, (230, 196, 100))
    band(c, f, .02, .04, (230, 196, 100))


def harper_cloak(c, f):
    torso(c, f, .0, .92, STUFF, LT, e=.1, flare=.08)
    if f.view != "north":
        cx, cy = chest_xy(f, .22)
        x = cx - (f.w * .2 if not f.east else 0)
        c.add(ell((x - 3.4, cy - 3.4, x + 3.4, cy + 3.4)), (210, 214, 222), FIXED, 1.0)
        c.add(("line", [(x - 1.6, cy + 2), (x - 1.6, cy - 2), (x + 1.6, cy - 1), (x + 1.6, cy + 2)], .7), (120, 124, 134), FIXED, 0)
        c.add([ell((x + 1, cy - 3.6, x + 4.2, cy - .4)), ("-ell", (x + 2, cy - 3.6, x + 5, cy - .8))], (240, 240, 245), FIXED, 0)


# ---- 老装备换皮 ----
def elven_chain(c, f):
    torso(c, f, .0, .95, STUFF, LT)
    for i in range(7):
        t = .1 + i * .12
        pts = f.shape(t, t + .001, .0)
        c.line([pts[0], pts[-1]], .45, (170, 176, 182), STUFF)
    if f.view != "north":
        cx, cy = chest_xy(f, .3)
        c.add(poly([(cx, cy - 5), (cx + 3.4, cy), (cx, cy + 5), (cx - 3.4, cy)]), MD, STUFF, .9)
        c.line([(cx, cy - 4), (cx, cy + 4)], .5, (150, 156, 162), STUFF)
    for s in ((-1, 1) if not f.east else (0,)):
        x = f.cx + s * f.w * .42 if not f.east else f.cx
        for k in range(3):
            y = f.y(-.02 + k * .06)
            c.add(poly([(x - f.w * .16, y), (x, y - 2), (x + f.w * .16, y), (x, y + 3)]), LT if k % 2 else MD, STUFF, .8)


def astral_armor(c, f):
    torso(c, f, .02, .7, STUFF, LT)
    for s in ((-1, 1) if not f.east else (1,)):
        x = f.cx + s * f.w * .42 if not f.east else f.cx
        c.add(poly([(x - f.w * .18, f.y(.0)), (x + s * f.w * .32, f.y(-.14)), (x + f.w * .18, f.y(.1)), (x - f.w * .1, f.y(.14))]), MD, STUFF, 1.2)
    if f.view != "north":
        cx, cy = chest_xy(f, .3)
        c.line([(cx - f.w * .25, f.y(.15)), (cx, f.y(.4)), (cx + f.w * .25, f.y(.15))], 1.0, (230, 232, 240), STUFF)
        c.add(poly([(cx, cy + 2), (cx + 2, cy + 5), (cx, cy + 8), (cx - 2, cy + 5)]), (220, 225, 235), FIXED, .7)


BODY_ITEMS = {
    "PaladinPlate": paladin_plate, "KnightMantle": knight_mantle, "ChainShirt": chain_shirt, "DwarvenRuneplate": dwarven_runeplate,
    "RangerLeathers": ranger_leathers, "WoodwalkerCloak": woodwalker_cloak, "ShadowweaveCloak": shadowweave_cloak,
    "ArchmageRobe": archmage_robe, "RedWizardRobe": red_robe, "MorninglordVestments": morninglord, "BattleClericBreastplate": cleric_breastplate,
    "DeathknightPlate": deathknight_plate, "LichShroud": lich_shroud, "DragonknightArmor": dragonknight_armor, "EmberCollarPlate": ember_collar,
    "LoxodonWardenArmor": loxodon_armor, "OgreTrophyHide": ogre_hide, "SkyguardHarness": skyguard_harness, "CelestialPanoply": celestial_panoply,
    "ElementalWeave": elemental_weave, "AdventurerClothes": adventurer, "BardFinery": bard_finery, "CourtAttire": court_attire, "HarperCloak": harper_cloak,
}
RESKIN_BODY = {"ElvenChain": elven_chain, "GithAstralArmor": astral_armor}


# ================= 头部 =================
class Head:
    def __init__(self, view):
        self.view = view
        self.x0, self.y0, self.x1, self.y1 = HEAD[view]
        self.w, self.h = self.x1 - self.x0, self.y1 - self.y0
        self.cx = (self.x0 + self.x1) / 2
        self.east = view == "east"

    def X(self, u):
        return self.x0 + self.w * u

    def Y(self, v):
        return self.y0 + self.h * v


def dome(c, h, v1, chan=STUFF, col=LT, e=.06):
    """盖住头顶到 v1 的盔壳（不规则圆顶）。"""
    pts = []
    for i in range(25):
        a = math.radians(180 + i * 7.5)
        pts.append((h.cx + math.cos(a) * h.w * (.5 + e), h.Y(.5) + math.sin(a) * h.h * (.52 + e)))
    pts += [(h.X(1 + e), h.Y(v1)), (h.X(-e), h.Y(v1))]
    c.add(poly(pts), col, chan, 1.4)
    if h.east:
        c.add(poly(pts[:10] + [(h.X(.35), h.Y(v1)), (h.X(-e), h.Y(v1))]), DK, chan, 0)
    else:
        c.add(poly(pts[13:] + [(h.X(1 + e), h.Y(v1)), (h.X(.6), h.Y(v1))]), DK, chan, 0)


def full_helm(c, h, col=LT, chan=STUFF):
    dome(c, h, .98, chan, col)


def great_helm(c, h):
    full_helm(c, h)
    gold = (222, 186, 92)
    if h.view != "north":
        if h.east:
            c.add(rect((h.X(.55), h.Y(.5) - 1, h.X(1.04), h.Y(.5) + 1.4)), INK, FIXED, 0)
        else:
            c.add(rect((h.X(.18), h.Y(.48) - 1.2, h.X(.82), h.Y(.48) + 1.2)), INK, FIXED, 0)
            c.add(rect((h.cx - 1.2, h.Y(.48), h.cx + 1.2, h.Y(.8))), INK, FIXED, 0)
    c.add(("line", [(h.X(-.04), h.Y(.85)), (h.X(1.04), h.Y(.85))], 1.6), gold, FIXED, 0)
    # 羽饰（纹章色）
    if h.view == "north":
        c.add(poly([(h.cx - 3, h.Y(-.04)), (h.cx + 3, h.Y(-.04)), (h.cx + 4, h.Y(.9)), (h.cx - 4, h.Y(.9))]), LT, HERALD, 1.2)
    elif h.east:
        c.add(poly([(h.cx, h.Y(-.04)), (h.cx - h.w * .2, h.Y(-.22)), (h.X(-.25), h.Y(.2)), (h.X(-.2), h.Y(.7)), (h.X(.1), h.Y(.1))]), LT, HERALD, 1.2)
    else:
        c.add(poly([(h.cx - 2.5, h.Y(.0)), (h.cx - 4, h.Y(-.28)), (h.cx + 1, h.Y(-.34)), (h.cx + 4.5, h.Y(-.2)), (h.cx + 2.5, h.Y(.0))]), LT, HERALD, 1.2)


def bearded_helm(c, h):
    dome(c, h, .62)
    bronze = (190, 150, 80)
    if h.view != "north":
        if h.east:
            beard = [(h.X(.5), h.Y(.62)), (h.X(1.06), h.Y(.6)), (h.X(.98), h.Y(1.05)), (h.X(.8), h.Y(1.25)), (h.X(.58), h.Y(1.05))]
        else:
            beard = [(h.X(.08), h.Y(.6)), (h.X(.92), h.Y(.6)), (h.X(.82), h.Y(1.0)), (h.X(.62), h.Y(1.28)), (h.cx, h.Y(1.12)), (h.X(.38), h.Y(1.28)), (h.X(.18), h.Y(1.0))]
        c.add(poly(beard), bronze, FIXED, 1.4)
        for k in range(4):
            x = h.X(.25 + k * .17) if not h.east else h.X(.62 + k * .1)
            c.line([(x, h.Y(.66)), (x, h.Y(1.0))], .6, (140, 104, 54))
        for x in ((h.X(.38), h.X(.62)) if not h.east else (h.X(.8),)):
            c.add([ell((x - 2, h.Y(1.26) - 2, x + 2, h.Y(1.26) + 2)), ("-ell", (x - 1, h.Y(1.26) - 1, x + 1, h.Y(1.26) + 1))], (120, 120, 118), FIXED, .6)
        c.add(rect((h.X(.2), h.Y(.44), h.X(.8), h.Y(.5))) if not h.east else rect((h.X(.55), h.Y(.44), h.X(1.04), h.Y(.5))), INK, FIXED, 0)
    else:
        c.add(rect((h.X(.15), h.Y(.62), h.X(.85), h.Y(.8))), bronze, FIXED, 1.0)


def wizard_hat(c, h):
    brim = [(h.X(-.28), h.Y(.36)), (h.X(1.28), h.Y(.36)), (h.X(1.1), h.Y(.48)), (h.X(-.1), h.Y(.48))]
    tip_x = h.X(.95) if h.view != "north" else h.X(.15)
    if h.east:
        tip_x = h.X(-.1)
    cone = [(h.X(.12), h.Y(.4)), (h.X(.88), h.Y(.4)), (h.X(.62), h.Y(-.35)), (tip_x, h.Y(-.62)), (h.X(.45), h.Y(-.3))]
    c.add(poly(cone), LT, STUFF, 1.4)
    c.add(poly([(h.X(.5), h.Y(.4)), (h.X(.88), h.Y(.4)), (h.X(.62), h.Y(-.35))]), DK, STUFF, 0)
    c.add(poly(brim), MD, STUFF, 1.4)
    c.add(rect((h.X(.12), h.Y(.3), h.X(.88), h.Y(.38))), (150, 150, 160), STUFF, .8)
    if h.view != "north":
        sx = h.X(.55) if not h.east else h.X(.75)
        pts = []
        for i in range(10):
            r = 3 if i % 2 == 0 else 1.3
            a = math.radians(-90 + i * 36)
            pts.append((sx + r * math.cos(a), h.Y(.34) + r * math.sin(a)))
        c.add(poly(pts), (232, 234, 240), FIXED, .6)


def restless_helm(c, h):
    full_helm(c, h, (120, 120, 118))
    for s in ((-1, 1) if not h.east else (-1,)):
        bx = h.cx + s * h.w * .3 if not h.east else h.X(.3)
        c.add(poly([(bx - 2, h.Y(.12)), (bx + s * h.w * .4, h.Y(-.32)), (bx + s * h.w * .5, h.Y(-.4)), (bx + 2, h.Y(.0))]), (70, 66, 70), FIXED, 1.2)
    if h.view != "north":
        if h.east:
            for k in range(4):
                c.add(rect((h.X(.62 + k * .1), h.Y(.7), h.X(.66 + k * .1), h.Y(.86))), (220, 214, 196), FIXED, .5)
            c.add(ell((h.X(.78) - 1.5, h.Y(.45) - 1.5, h.X(.78) + 1.5, h.Y(.45) + 1.5)), (110, 240, 140), FIXED, 0)
        else:
            for k in range(6):
                c.add(rect((h.X(.22 + k * .1), h.Y(.7), h.X(.27 + k * .1), h.Y(.88))), (220, 214, 196), FIXED, .5)
            for x in (h.X(.33), h.X(.67)):
                c.add(ell((x - 1.8, h.Y(.45) - 1.4, x + 1.8, h.Y(.45) + 1.4)), (110, 240, 140), FIXED, 0)


def crown_of_bone(c, h):
    band_y0, band_y1 = h.Y(.22), h.Y(.32)
    c.add(rect((h.X(.06), band_y0, h.X(.94), band_y1)), LT, STUFF, 1.2)
    if h.view != "north":
        xs = [h.X(.2 + k * .15) for k in range(5)] if not h.east else [h.X(.55 + k * .1) for k in range(3)]
        for i, x in enumerate(xs):
            top = h.Y(-.08 if i == len(xs) // 2 else .0)
            c.add(poly([(x - 1.6, band_y0), (x - 1.1, top), (x + 1.1, top), (x + 1.6, band_y0)]), (230, 222, 196), FIXED, .9)
        gx = h.cx if not h.east else h.X(.75)
        c.add(poly([(gx, band_y0 - 2.5), (gx + 2.2, (band_y0 + band_y1) / 2), (gx, band_y1 + 1), (gx - 2.2, (band_y0 + band_y1) / 2)]), (140, 80, 180), FIXED, .7)


def dragon_helm(c, h):
    full_helm(c, h)
    if h.view != "north":
        if h.east:
            c.add(poly([(h.X(.6), h.Y(.35)), (h.X(1.3), h.Y(.5)), (h.X(1.28), h.Y(.62)), (h.X(.85), h.Y(.62))]), MD, STUFF, 1.2)
            c.add(poly([(h.X(.62), h.Y(.85)), (h.X(1.22), h.Y(.8)), (h.X(1.1), h.Y(.92)), (h.X(.65), h.Y(1.0))]), MD, STUFF, 1.2)
            c.add(rect((h.X(.6), h.Y(.62), h.X(.98), h.Y(.85))), (60, 54, 60), FIXED, 0)
        else:
            c.add(poly([(h.X(.1), h.Y(.5)), (h.X(.9), h.Y(.5)), (h.X(.82), h.Y(.85)), (h.X(.18), h.Y(.85))]), (60, 54, 60), FIXED, 1.0)
            for k in range(5):
                x = h.X(.22 + k * .14)
                c.add(poly([(x - 1.2, h.Y(.5)), (x, h.Y(.6)), (x + 1.2, h.Y(.5))]), (236, 232, 220), FIXED, 0)
            for x in (h.X(.34), h.X(.66)):
                c.add(ell((x - 1.6, h.Y(.38) - 1.2, x + 1.6, h.Y(.38) + 1.2)), (250, 200, 80), FIXED, 0)
    for s in ((-1, 1) if not h.east else (-1,)):
        bx = h.cx + s * h.w * .28 if not h.east else h.X(.3)
        c.add(poly([(bx - 2, h.Y(.1)), (bx + s * h.w * .35, h.Y(-.12)), (bx + s * h.w * .55, h.Y(-.05)), (bx + 2, h.Y(.15))]), MD, STUFF, 1.0)


def gith_helm(c, h):
    dome(c, h, .45)
    for s in ((-1, 1) if not h.east else (-1,)):
        bx = h.cx + s * h.w * .4 if not h.east else h.X(.35)
        c.add(poly([(bx, h.Y(.12)), (bx + s * h.w * .5, h.Y(-.42)), (bx + s * h.w * .38, h.Y(-.08)), (bx + s * h.w * .1, h.Y(.36))]), MD, STUFF, 1.2)
    if h.view != "north":
        gx = h.cx if not h.east else h.X(.85)
        c.add(poly([(gx, h.Y(.24)), (gx + 2.4, h.Y(.32)), (gx, h.Y(.42)), (gx - 2.4, h.Y(.32))]), (225, 230, 240), FIXED, .7)


def rivet_helm(c, h):
    dome(c, h, .42, STUFF, MD)
    c.add(rect((h.X(-.06), h.Y(.36), h.X(1.06), h.Y(.44))), (70, 66, 66), FIXED, .9)
    for k in range(6):
        x = h.X(.05 + k * .18)
        c.add(ell((x - 1, h.Y(.4) - 1, x + 1, h.Y(.4) + 1)), (150, 146, 140), FIXED, 0)
    if h.view != "north":
        x = h.cx if not h.east else h.X(.8)
        c.add(poly([(x, h.Y(.06)), (x + 2, h.Y(.18)), (x, h.Y(.3)), (x - 2, h.Y(.18))]), (220, 110, 50), FIXED, .6)


def rogue_mask(c, h):
    if h.view == "north":
        c.add(rect((h.X(.1), h.Y(.62), h.X(.9), h.Y(.68))), LT, STUFF, .9)
        c.add(poly([(h.cx - 2, h.Y(.66)), (h.cx + 2, h.Y(.66)), (h.cx + 3, h.Y(.86)), (h.cx - 3, h.Y(.86))]), MD, STUFF, .8)
        return
    if h.east:
        pts = [(h.X(.45), h.Y(.55)), (h.X(1.06), h.Y(.52)), (h.X(1.0), h.Y(.92)), (h.X(.6), h.Y(1.0)), (h.X(.45), h.Y(.85))]
    else:
        pts = [(h.X(.06), h.Y(.56)), (h.cx, h.Y(.5)), (h.X(.94), h.Y(.56)), (h.X(.86), h.Y(.92)), (h.cx, h.Y(1.0)), (h.X(.14), h.Y(.92))]
    c.add(poly(pts), LT, STUFF, 1.2)
    c.add(poly(pts[len(pts) // 2:] + [(h.cx, h.Y(.5))]), DK, STUFF, 0)


def bard_beret(c, h):
    pts = [(h.X(-.08), h.Y(.3)), (h.X(.2), h.Y(.02)), (h.X(.75), h.Y(-.04)), (h.X(1.12), h.Y(.16)), (h.X(1.04), h.Y(.32)), (h.X(.5), h.Y(.36))]
    if h.view == "north" or h.east:
        pts = [(x if h.east else 2 * h.cx - x, y) for x, y in pts]
    c.add(poly(pts), LT, STUFF, 1.3)
    c.add(poly(pts[2:5] + [(h.X(.6), h.Y(.3))]), DK, STUFF, 0)
    fx = h.X(.85) if not h.view == "north" else h.X(.15)
    c.add(("line", [(fx, h.Y(.1)), (fx + (6 if h.view != "north" else -6), h.Y(-.35)), (fx + (14 if h.view != "north" else -14), h.Y(-.45))], 2.2), (240, 240, 236), FIXED, .9)


def minotaur_horn_helm(c, h):
    dome(c, h, .5)
    for s in ((-1, 1) if not h.east else (-1,)):
        x = h.cx + s * h.w * .32 if not h.east else h.X(.4)
        c.add([ell((x - 3.6, h.Y(.15) - 3.6, x + 3.6, h.Y(.15) + 3.6)), ("-ell", (x - 2, h.Y(.15) - 2, x + 2, h.Y(.15) + 2))], (190, 130, 70), FIXED, .9)
    c.add(rect((h.X(-.06), h.Y(.42), h.X(1.06), h.Y(.5))), MD, STUFF, .9)


HEAD_ITEMS = {
    "GreatHelmOath": great_helm, "BeardedHelm": bearded_helm, "WizardHat": wizard_hat, "HelmRestlessDead": restless_helm, "CrownOfBone": crown_of_bone,
    "DragonHeadHelm": dragon_helm, "GithCrestedHelm": gith_helm, "EmberRivetHelm": rivet_helm, "RogueMask": rogue_mask, "BardBeret": bard_beret,
}
RESKIN_HEAD = {"MinotaurHornHelm": minotaur_horn_helm}

# 预览用的着色（材料色 / 纹章色）
PREVIEW_COLORS = {"default": ((180, 184, 190), (210, 180, 90))}
STUFFLESS = {"RedWizardRobe": (158, 31, 31), "DragonknightArmor": (158, 153, 209), "CelestialPanoply": (242, 230, 191), "HarperCloak": (122, 110, 92), "CrownOfBone": (77, 71, 82), "GithCrestedHelm": (77, 140, 140), "DragonHeadHelm": (158, 153, 209)}


def tint(col, msk, stuff, herald):
    from PIL import ImageChops
    rgb = col.convert("RGB")
    r, g, _ = msk.split()
    sv = ImageChops.multiply(rgb, Image.new("RGB", rgb.size, stuff))
    hv = ImageChops.multiply(rgb, Image.new("RGB", rgb.size, herald))
    out = Image.composite(sv, rgb, r)
    out = Image.composite(hv, out, g)
    out = out.convert("RGBA")
    out.putalpha(col.split()[3])
    return out

def body_blob(body, view, size=256):
    im = Image.new("RGBA", (128 * S, 128 * S), (0, 0, 0, 0))
    f = Fit(body, view)
    d = ImageDraw.Draw(im)
    pts = [(x * S, y * S) for x, y in f.shape(0, 1, -.02)]
    d.polygon(pts, fill=(222, 196, 170, 255))
    hx0, hy0, hx1, hy1 = HEAD[view]
    d.ellipse([hx0 * S, (hy0 - 30) * S, hx1 * S, (hy1 - 30) * S], fill=(222, 196, 170, 255))
    return im.resize((size, size), Image.LANCZOS)


if __name__ == "__main__":
    out = []
    previews = []
    for name, fn in list(BODY_ITEMS.items()) + list(RESKIN_BODY.items()):
        mask = name not in RESKIN_BODY
        row = []
        for body in BODIES:
            for view in VIEWS:
                c = Canvas()
                fn(c, Fit(body, view))
                out.append(c.save("LP/Apparel/%s/%s_%s_%s" % (name, name, body, view), 256, mask))
                if view == "south" or body == "Male":
                    row.append((body, view, c.col.copy(), c.msk.copy()))
                if body == "Male" and view == "south" and mask:
                    out.append(c.icon("LP/Apparel/%s/%s_Icon" % (name, name)))
        previews.append((name, row))
    for name, fn in list(HEAD_ITEMS.items()) + list(RESKIN_HEAD.items()):
        mask = name not in RESKIN_HEAD
        row = []
        for view in VIEWS:
            c = Canvas()
            fn(c, Head(view))
            out.append(c.save("LP/Apparel/%s/%s_%s" % (name, name, view), 256, mask))
            row.append(("Male", view, c.col.copy(), c.msk.copy()))
            if view == "south" and mask:
                out.append(c.icon("LP/Apparel/%s/%s_Icon" % (name, name)))
        previews.append((name, row))
    # 预览总图：每件一行，Male 南/东/北 + 其他体型南向；着色模拟游戏（材料色 + 纹章色）
    cell = 128
    cols = 7
    sheet = Image.new("RGBA", (cols * cell + 220, len(previews) * cell), (205, 200, 190, 255))
    try:
        from PIL import ImageFont
        font = ImageFont.truetype("C:/Windows/Fonts/msyh.ttc", 18)
    except Exception:
        font = None
    d = ImageDraw.Draw(sheet)
    for r, (name, row) in enumerate(previews):
        d.text((6, r * cell + 52), name, fill=(40, 36, 36), font=font)
        stuff = STUFFLESS.get(name, PREVIEW_COLORS["default"][0])
        herald = PREVIEW_COLORS["default"][1]
        for i, (body, view, col, msk) in enumerate(row[:cols]):
            base = body_blob(body, view, cell) if name not in HEAD_ITEMS and name not in RESKIN_HEAD else None
            tile = Image.new("RGBA", (cell, cell), (0, 0, 0, 0))
            if base is not None:
                tile.alpha_composite(base)
            else:
                hd = Image.new("RGBA", (128 * S, 128 * S), (0, 0, 0, 0))
                hx0, hy0, hx1, hy1 = HEAD[view]
                ImageDraw.Draw(hd).ellipse([hx0 * S, hy0 * S, hx1 * S, hy1 * S], fill=(222, 196, 170, 255))
                tile.alpha_composite(hd.resize((cell, cell), Image.LANCZOS))
            tile.alpha_composite(tint(col, msk, stuff, herald).resize((cell, cell), Image.LANCZOS))
            sheet.alpha_composite(tile, (220 + i * cell, r * cell))
    os.makedirs(os.path.dirname(PREVIEW), exist_ok=True)
    sheet.save(PREVIEW)
    print(len(out), "textures; preview", PREVIEW)
