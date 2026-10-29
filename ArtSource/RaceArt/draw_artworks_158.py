# 1.58.5 血肉艺术品与皮雕：小 / 大 / 巨型三档，各 2～3 个随机样式（Graphic_Random），外加两张未完成品。
# 画法照 RimWorld 原版雕塑：粗黑描边、平涂 + 右下暗边 + 左上亮边、低饱和。
# 血肉艺术品是固定色（生肉不是材料，不按材料染色）；皮雕是浅灰，游戏里按布料 / 皮革颜色相乘（原版雕塑同样做法）。
# 画布：小雕 1 格 128px；大雕 drawSize 3 → 384px，作品底座落在中心格下沿；巨雕占 2×2、drawSize 4 → 512px。
# 用法：python -I ArtSource/RaceArt/draw_artworks_158.py <模组根目录>
import math, os, sys
from PIL import Image, ImageDraw, ImageFilter, ImageChops

ROOT = sys.argv[1] if len(sys.argv) > 1 else os.getcwd()
OUT = os.path.join(ROOT, "Textures", "LP", "Art")
PREVIEW = os.path.join(ROOT, "ArtSource", "Preview", "Artworks-1.58.5.png")
SS = 3
INK = (38, 28, 28)

# 血肉色（低饱和）
FLESH, FLESH_D, FLESH_L = (170, 96, 88), (128, 66, 62), (200, 134, 120)
MUSCLE, MUSCLE_D = (148, 70, 66), (108, 48, 46)
SINEW = (204, 166, 144)
FAT, FAT_D = (222, 196, 156), (186, 160, 122)
BONE, BONE_D, BONE_L = (224, 212, 184), (178, 164, 136), (240, 232, 212)
BLOOD = (104, 34, 34)
EYE, EYE_D = (226, 222, 206), (64, 52, 48)
# 皮雕灰阶（乘材料色）
LG, LG_D, LG_L = (214, 212, 208), (160, 158, 154), (238, 236, 232)
LG_DD = (122, 120, 116)
STITCH = (96, 94, 90)


class Canvas:
    def __init__(self, w, h, scale=1.0, base=1.0):
        self.W, self.H = w, h
        self.scale, self.base = scale, base      # 以 (0.5, base) 为中心整体放大，大 / 巨型作品用
        self.img = Image.new("RGBA", (w * SS, h * SS), (0, 0, 0, 0))

    def P(self, u, v):
        u = .5 + (u - .5) * self.scale
        v = self.base + (v - self.base) * self.scale
        return u * self.W * SS, v * self.H * SS

    def mask(self, kind, a, w=0):
        m = Image.new("L", self.img.size, 0)
        d = ImageDraw.Draw(m)
        if kind == "ell":
            (x0, y0), (x1, y1) = self.P(a[0], a[1]), self.P(a[2], a[3])
            d.ellipse([x0, y0, x1, y1], fill=255)
        elif kind == "rect":
            (x0, y0), (x1, y1) = self.P(a[0], a[1]), self.P(a[2], a[3])
            d.rounded_rectangle([x0, y0, x1, y1], radius=(a[4] if len(a) > 4 else 3) * SS, fill=255)
        elif kind == "poly":
            d.polygon([self.P(u, v) for u, v in a], fill=255)
        elif kind == "line":
            pts = [self.P(u, v) for u, v in a]
            ww = max(1, int(w * SS))
            d.line(pts, fill=255, width=ww, joint="curve")
            for x, y in pts:
                d.ellipse([x - ww / 2, y - ww / 2, x + ww / 2, y + ww / 2], fill=255)
        return m

    def _fill(self, m, color):
        self.img.paste(Image.new("RGBA", self.img.size, tuple(color) + (255,)), (0, 0), m)

    def add(self, kind, a, color, ow=3.5, w=0, shade=None, light=None, sd=.035):
        """形状 + 描边；shade/light 给出时，在右下画暗边、左上画亮边（形状自己错位相减）。"""
        m = self.mask(kind, a, w)
        if ow:
            k = max(3, int(ow * SS) * 2 + 1)
            self._fill(m.filter(ImageFilter.MaxFilter(k)), INK)
        self._fill(m, color)
        bb = m.getbbox()
        if bb and (shade or light):
            size = max(bb[2] - bb[0], bb[3] - bb[1])
            off = max(2, int(size * sd * 2.4))
            if shade:
                moved = ImageChops.offset(m, -off, -off)
                self._fill(ImageChops.subtract(m, moved), shade)
            if light:
                moved = ImageChops.offset(m, off // 2 + 1, off // 2 + 1)
                self._fill(ImageChops.subtract(m, moved), light)
        return m

    def line(self, pts, color, w, ow=2.5):
        return self.add("line", pts, color, ow, w)

    def shadow(self, u0, v0, u1, v1, alpha=70):
        """地面投影：一块模糊的半透明暗椭圆。"""
        g = Image.new("RGBA", self.img.size, (0, 0, 0, 0))
        (x0, y0), (x1, y1) = self.P(u0, v0), self.P(u1, v1)
        ImageDraw.Draw(g).ellipse([x0, y0, x1, y1], fill=(20, 14, 14, alpha))
        g = g.filter(ImageFilter.GaussianBlur((x1 - x0) * .08))
        self.img.alpha_composite(g)

    def final(self):
        return self.img.resize((self.W, self.H), Image.LANCZOS)


def wave(u0, v0, u1, v1, n=8, amp=.012, phase=0.0):
    """两点之间带起伏的折线（肌肉纤维 / 皮革缝线用）。"""
    pts = []
    for i in range(n + 1):
        t = i / n
        u, v = u0 + (u1 - u0) * t, v0 + (v1 - v0) * t
        dx, dy = v1 - v0, -(u1 - u0)
        L = math.hypot(dx, dy) or 1
        s = math.sin(t * math.pi * 3 + phase) * amp
        pts.append((u + dx / L * s, v + dy / L * s))
    return pts


def blob(cu, cv, ru, rv, n=14, j=.18, seed=0):
    """不规则的肉团轮廓。"""
    pts = []
    for i in range(n):
        a = i / n * math.tau
        r = 1 + j * math.sin(a * 3 + seed) * math.cos(a * 2 + seed * 1.7)
        pts.append((cu + math.cos(a) * ru * r, cv + math.sin(a) * rv * r))
    return pts


def stitches(c, pts, color=STITCH, every=1, w=1.6, ln=.012):
    """沿折线画短横缝线。"""
    for i in range(0, len(pts) - 1, every):
        (u0, v0), (u1, v1) = pts[i], pts[i + 1]
        mu, mv = (u0 + u1) / 2, (v0 + v1) / 2
        dx, dy = v1 - v0, -(u1 - u0)
        L = math.hypot(dx, dy) or 1
        c.add("line", [(mu - dx / L * ln, mv - dy / L * ln), (mu + dx / L * ln, mv + dy / L * ln)], color, 0, w)


# ==================== 共用部件 ====================
def bone_plinth(c, u0, u1, v0, v1):
    c.add("rect", (u0, v0, u1, v1, 4), BONE, 3.5, shade=BONE_D, light=BONE_L, sd=.02)
    for i in range(1, 4):
        u = u0 + (u1 - u0) * i / 4
        c.add("line", [(u, v0 + .01), (u, v1 - .01)], BONE_D, 0, 1.6)


def leather_plinth(c, u0, u1, v0, v1):
    c.add("rect", (u0, v0, u1, v1, 4), LG_D, 3.5, shade=LG_DD, light=LG, sd=.02)
    stitches(c, [(u0 + .02 + (u1 - u0 - .04) * i / 10, v0 + (v1 - v0) * .35) for i in range(11)], STITCH)


def tendril(c, pts, color=MUSCLE, w=7, ow=3):
    c.add("line", pts, color, ow, w)
    c.add("line", pts, FLESH_L, 0, max(1, w * .3))


def fibers(c, u0, v0, u1, v1, k=4, color=MUSCLE_D):
    for i in range(k):
        t = (i + .5) / k
        c.add("line", wave(u0 + (u1 - u0) * t * .2, v0, u0 + (u1 - u0) * (.3 + t * .7), v1, 6, .006, i), color, 0, 1.4)


# ==================== 血肉艺术品 ====================
def flesh_small_knot(c):
    """骨座上一团拧成心形的肉结，几条筋络垂下。"""
    c.shadow(.18, .80, .82, .94)
    bone_plinth(c, .26, .74, .72, .88)
    c.add("poly", blob(.5, .46, .26, .25, 16, .14, 1.0), FLESH, 4, shade=FLESH_D, light=FLESH_L)
    c.add("poly", blob(.42, .40, .12, .11, 12, .2, 2.0), MUSCLE, 2.5, shade=MUSCLE_D)
    c.add("poly", blob(.60, .52, .10, .09, 12, .2, 3.0), FAT, 2.5, shade=FAT_D)
    for u in (.36, .5, .64):
        tendril(c, [(u, .62), (u + .02, .68), (u - .01, .74)], MUSCLE, 5, 2.5)
    c.add("line", wave(.30, .40, .70, .50, 10, .02), SINEW, 0, 2)
    c.add("ell", (.47, .22, .53, .27), BLOOD, 2)


def flesh_small_face(c):
    """一张由肉团塑成的脸，两只空洞的眼窝。"""
    c.shadow(.2, .82, .8, .94)
    bone_plinth(c, .30, .70, .76, .88)
    c.add("poly", blob(.5, .47, .27, .31, 16, .08, .4), FLESH, 4, shade=FLESH_D, light=FLESH_L)
    c.add("ell", (.33, .36, .45, .46), MUSCLE_D, 2.5)
    c.add("ell", (.55, .36, .67, .46), MUSCLE_D, 2.5)
    c.add("ell", (.37, .39, .42, .44), EYE, 0)
    c.add("poly", [(.5, .44), (.45, .58), (.55, .58)], FLESH_D, 2.5)
    c.add("line", wave(.36, .66, .64, .66, 8, .015), BLOOD, 2, 4)
    fibers(c, .26, .20, .74, .30, 4, FLESH_D)


def flesh_small_totem(c):
    """三团肉叠成图腾，中间插着肋骨。"""
    c.shadow(.2, .84, .8, .95)
    bone_plinth(c, .28, .72, .78, .90)
    for i, (cv, r) in enumerate(((.66, .17), (.47, .14), (.30, .11))):
        c.add("poly", blob(.5, cv, r * 1.1, r * .85, 14, .15, i * 1.9), (FLESH, MUSCLE, FLESH)[i], 3.5,
              shade=(FLESH_D, MUSCLE_D, FLESH_D)[i], light=FLESH_L)
    for s in (-1, 1):
        c.add("line", [(.5 + s * .12, .50), (.5 + s * .26, .40), (.5 + s * .30, .28)], BONE, 3, 5)
    c.add("ell", (.44, .24, .56, .33), EYE, 2.5)
    c.add("ell", (.48, .26, .53, .31), EYE_D, 0)


def flesh_large_figure(c):
    """站立的人形：肌束缠成躯体，骨骼外露，立在骨座上。"""
    c.shadow(.30, .66, .70, .76)
    bone_plinth(c, .36, .64, .62, .70)
    # 腿
    for s in (-1, 1):
        c.add("poly", [(.5 + s * .02, .44), (.5 + s * .08, .44), (.5 + s * .07, .62), (.5 + s * .03, .62)], MUSCLE, 3, shade=MUSCLE_D)
    # 躯干
    c.add("poly", [(.42, .24), (.58, .24), (.60, .34), (.56, .46), (.44, .46), (.40, .34)], FLESH, 4, shade=FLESH_D, light=FLESH_L)
    fibers(c, .42, .26, .58, .44, 5, MUSCLE_D)
    for i in range(3):
        c.add("line", [(.45, .29 + i * .035), (.5, .30 + i * .035), (.55, .29 + i * .035)], BONE, 2, 2.4)
    # 手臂向上托举
    for s in (-1, 1):
        tendril(c, [(.5 + s * .08, .27), (.5 + s * .14, .22), (.5 + s * .13, .12)], MUSCLE, 8, 3)
    c.add("poly", blob(.5, .10, .09, .05, 12, .2, 2), FAT, 3, shade=FAT_D)     # 托举的肉团
    # 头
    c.add("ell", (.455, .155, .545, .235), FLESH, 3.5, shade=FLESH_D, light=FLESH_L)
    c.add("ell", (.475, .185, .495, .205), BLOOD, 0)
    c.add("ell", (.505, .185, .525, .205), BLOOD, 0)


def flesh_large_heart(c):
    """一颗巨大的心脏挂在骨架上，动脉向上盘绕。"""
    c.shadow(.28, .66, .72, .76)
    bone_plinth(c, .34, .66, .62, .70)
    for s in (-1, 1):
        c.add("line", [(.5 + s * .13, .62), (.5 + s * .15, .40), (.5 + s * .11, .14)], BONE, 3.5, 7)
    c.add("line", [(.39, .15), (.61, .15)], BONE, 3.5, 6)
    for s in (-1, 1):
        tendril(c, [(.5 + s * .03, .26), (.5 + s * .05, .19), (.5 + s * .10, .16)], MUSCLE, 7, 3)
    c.add("poly", [(.5, .58), (.36, .42), (.37, .30), (.45, .26), (.5, .31), (.56, .26), (.64, .30), (.65, .42)],
          FLESH, 4.5, shade=FLESH_D, light=FLESH_L)
    c.add("line", wave(.44, .30, .52, .52, 8, .01), MUSCLE_D, 0, 3)
    c.add("line", wave(.58, .32, .56, .46, 6, .01), FAT, 0, 3)
    for u, v in ((.42, .38), (.60, .40)):
        c.add("ell", (u - .012, v - .012, u + .012, v + .012), FLESH_L, 0)


def flesh_grand_arch(c):
    """一座肋骨拱门，肉幕垂挂，正中一只睁开的眼。"""
    c.shadow(.20, .70, .80, .84)
    bone_plinth(c, .22, .78, .70, .78)
    for i in range(6):
        t = i / 5
        u = .26 + t * .48
        top = .16 + abs(t - .5) * .14
        c.add("line", [(u, .70), (u + (t - .5) * -.06, .42), (.5 + (u - .5) * .4, top)], BONE, 3.5, 9)
    c.add("poly", [(.30, .30), (.70, .30), (.72, .56), (.62, .62), (.5, .58), (.38, .62), (.28, .56)],
          FLESH, 4.5, shade=FLESH_D, light=FLESH_L)
    fibers(c, .30, .32, .70, .56, 7, MUSCLE_D)
    c.add("ell", (.42, .38, .58, .50), EYE, 4)
    c.add("ell", (.47, .40, .53, .48), (120, 96, 70), 0)
    c.add("ell", (.485, .425, .515, .455), EYE_D, 0)
    for u in (.34, .44, .56, .66):
        tendril(c, [(u, .58), (u + .01, .64), (u - .01, .69)], MUSCLE, 7, 3)


def flesh_grand_embrace(c):
    """两具肉身相拥，额头相抵，下半身长在同一团血肉里。"""
    c.shadow(.16, .70, .84, .84)
    for s in (-1, 1):                                          # 躯干向内倾
        c.add("poly", [(.5 + s * .22, .62), (.5 + s * .04, .62), (.5 + s * .02, .40), (.5 + s * .07, .28), (.5 + s * .17, .30), (.5 + s * .22, .42)],
              FLESH, 4.5, shade=FLESH_D, light=FLESH_L)
        fibers(c, .5 + min(s * .20, s * .05), .34, .5 + max(s * .20, s * .05), .58, 4, MUSCLE_D)
        c.add("ell", (.5 + s * .095 - .065, .15, .5 + s * .095 + .065, .29), FLESH, 4, shade=FLESH_D, light=FLESH_L)
        c.add("line", [(.5 + s * .07, .21), (.5 + s * .11, .21)], BLOOD, 0, 3)
    for s in (-1, 1):                                          # 手臂互相环抱
        tendril(c, [(.5 + s * .19, .36), (.5 + s * .06, .42), (.5 - s * .10, .38), (.5 - s * .16, .33)], MUSCLE, 10, 3.5)
    c.add("poly", blob(.5, .66, .30, .09, 18, .10, 4), FLESH_D, 4.5, shade=MUSCLE_D, light=FLESH)
    c.add("line", wave(.26, .66, .74, .66, 12, .012), SINEW, 0, 3)
    for u in (.24, .76):
        c.add("line", [(u, .70), (u + (.5 - u) * .15, .58), (u - (.5 - u) * .05, .50)], BONE, 3, 7)

# ==================== 皮雕（灰阶，乘材料色） ====================
def leather_small_hide(c):
    """绷在框上的一张皮，上面压出太阳纹。"""
    c.shadow(.2, .84, .8, .95)
    leather_plinth(c, .26, .74, .80, .90)
    for u in (.24, .76):
        c.add("line", [(u, .82), (u, .14)], LG_D, 3, 5)
    c.add("line", [(.20, .16), (.80, .16)], LG_D, 3, 5)
    c.add("poly", [(.28, .18), (.72, .18), (.70, .44), (.73, .70), (.5, .76), (.27, .70), (.30, .44)], LG, 4, shade=LG_D, light=LG_L)
    for i in range(12):
        a = i / 12 * math.tau
        c.add("line", [(.5 + math.cos(a) * .07, .46 + math.sin(a) * .07), (.5 + math.cos(a) * .14, .46 + math.sin(a) * .14)], LG_D, 0, 2.2)
    c.add("ell", (.43, .39, .57, .53), LG_D, 2.5)
    c.add("ell", (.46, .42, .54, .50), LG_L, 0)
    for u0, u1 in ((.24, .28), (.72, .76)):
        for v in (.22, .40, .58):
            c.add("line", [(u0, v), (u1, v + .03)], STITCH, 0, 1.8)


def leather_small_figurine(c):
    """缝制的小兽：鹿形，接缝一圈缝线。"""
    c.shadow(.2, .82, .8, .94)
    leather_plinth(c, .28, .72, .78, .90)
    for u in (.36, .44, .58, .66):
        c.add("line", [(u, .56), (u - .01, .78)], LG_D, 3, 6)
    body = [(.30, .46), (.38, .38), (.62, .38), (.70, .46), (.68, .58), (.32, .58)]
    c.add("poly", body, LG, 4, shade=LG_D, light=LG_L)
    stitches(c, wave(.33, .48, .67, .48, 10, .004), STITCH)
    c.add("poly", [(.62, .40), (.70, .24), (.80, .26), (.78, .32), (.70, .44)], LG, 3.5, shade=LG_D, light=LG_L)
    for s in (0, 1):
        c.add("line", [(.72 + s * .04, .25), (.70 + s * .06, .12), (.66 + s * .08, .08)], LG_DD, 2.5, 3)
    c.add("ell", (.745, .27, .765, .29), INK, 0)


def leather_small_mask(c):
    """挂在支架上的一张皮面具，边缘压花。"""
    c.shadow(.24, .84, .76, .95)
    leather_plinth(c, .32, .68, .80, .90)
    c.add("line", [(.5, .80), (.5, .58)], LG_D, 3, 6)
    c.add("poly", blob(.5, .40, .22, .26, 18, .04, 0), LG, 4, shade=LG_D, light=LG_L)
    ring = [(.5 + math.cos(a / 24 * math.tau) * .19, .40 + math.sin(a / 24 * math.tau) * .23) for a in range(25)]
    stitches(c, ring, STITCH)
    for s in (-1, 1):
        c.add("poly", [(.5 + s * .04, .34), (.5 + s * .13, .31), (.5 + s * .12, .38), (.5 + s * .05, .39)], LG_DD, 2)
    c.add("poly", [(.5, .37), (.46, .48), (.54, .48)], LG_D, 2)
    c.add("line", [(.42, .54), (.5, .56), (.58, .54)], LG_DD, 2, 3)


def leather_large_figure(c):
    """高大的缝合人形：拼接的皮片和粗缝线，手持长杖。"""
    c.shadow(.30, .66, .70, .76)
    leather_plinth(c, .36, .64, .62, .70)
    for s in (-1, 1):
        c.add("poly", [(.5 + s * .015, .44), (.5 + s * .075, .44), (.5 + s * .065, .62), (.5 + s * .025, .62)], LG_D, 3, shade=LG_DD)
    c.add("poly", [(.41, .24), (.59, .24), (.61, .36), (.57, .47), (.43, .47), (.39, .36)], LG, 4, shade=LG_D, light=LG_L)
    seam = wave(.5, .25, .5, .46, 8, .006)
    c.add("line", seam, LG_D, 0, 1.6)
    stitches(c, seam, STITCH)
    seam2 = wave(.41, .34, .59, .34, 8, .004)
    stitches(c, seam2, STITCH)
    c.add("line", [(.62, .10), (.64, .62)], LG_DD, 3, 5)
    c.add("line", [(.41, .27), (.36, .38), (.38, .46)], LG, 3, 8)
    c.add("line", [(.59, .27), (.63, .33)], LG, 3, 8)
    c.add("ell", (.45, .14, .55, .245), LG, 3.5, shade=LG_D, light=LG_L)
    stitches(c, wave(.46, .19, .54, .19, 6, .003), STITCH)
    c.add("poly", [(.60, .08), (.66, .06), (.65, .13)], LG_L, 2.5)


def leather_large_banner(c):
    """皮革大旗：两根旗杆间绷着压花兽皮，上缀流苏。"""
    c.shadow(.28, .66, .72, .76)
    leather_plinth(c, .32, .68, .62, .70)
    for u in (.34, .66):
        c.add("line", [(u, .64), (u, .10)], LG_DD, 3, 6)
        c.add("ell", (u - .02, .07, u + .02, .11), LG_L, 2.5)
    c.add("line", [(.32, .13), (.68, .13)], LG_DD, 3, 5)
    c.add("poly", [(.36, .15), (.64, .15), (.63, .40), (.60, .50), (.5, .46), (.40, .50), (.37, .40)], LG, 4, shade=LG_D, light=LG_L)
    c.add("poly", [(.5, .20), (.56, .30), (.5, .40), (.44, .30)], LG_D, 2.5)
    c.add("ell", (.475, .27, .525, .33), LG_L, 0)
    for u in (.40, .45, .55, .60):
        c.add("line", [(u, .47), (u + .005, .54)], LG_D, 2, 2.5)
    stitches(c, [(.37 + .26 * i / 12, .17) for i in range(13)], STITCH)


def leather_grand_dragon(c):
    """拼缝的巨龙：盘着身子仰头，翼膜是绷开的薄皮。"""
    c.shadow(.16, .70, .84, .84)
    leather_plinth(c, .22, .78, .70, .78)
    for s in (-1, 1):
        c.add("poly", [(.5 + s * .06, .40), (.5 + s * .34, .16), (.5 + s * .30, .30), (.5 + s * .26, .44), (.5 + s * .10, .52)],
              LG_L, 3.5, shade=LG)
        for k in range(3):
            c.add("line", [(.5 + s * .07, .42), (.5 + s * (.30 - k * .02), .18 + k * .10)], LG_DD, 0, 2.4)
    c.add("poly", blob(.5, .58, .22, .11, 18, .08, 1), LG, 4.5, shade=LG_D, light=LG_L)
    c.add("line", [(.70, .62), (.80, .66), (.82, .58)], LG, 3.5, 8)
    c.add("poly", [(.44, .56), (.56, .56), (.56, .36), (.52, .24), (.46, .26), (.44, .38)], LG, 4, shade=LG_D, light=LG_L)
    c.add("poly", [(.45, .20), (.55, .22), (.60, .28), (.52, .31), (.45, .29)], LG, 3.5, shade=LG_D, light=LG_L)
    for s in (0, 1):
        c.add("line", [(.47 + s * .05, .21), (.45 + s * .06, .14)], LG_DD, 2.5, 3)
    c.add("ell", (.535, .235, .55, .25), INK, 0)
    stitches(c, wave(.30, .58, .70, .58, 14, .008), STITCH)
    stitches(c, wave(.50, .54, .50, .30, 8, .004), STITCH)


def leather_grand_pavilion(c):
    """皮帐神龛：拼缝的大兽皮搭成帐篷形，门帘上压着星纹。"""
    c.shadow(.16, .70, .84, .84)
    leather_plinth(c, .20, .80, .70, .78)
    c.add("poly", [(.5, .10), (.78, .70), (.22, .70)], LG, 4.5, shade=LG_D, light=LG_L)
    for t in (.33, .66):
        seam = [(.5 + (.28 * t) * k / 8 - .14 * t, .10 + (.60 * t) + 0 * k) for k in range(9)]
        stitches(c, seam, STITCH)
    for s in (-1, 1):
        seam = [(.5 + s * .28 * k / 10, .10 + .60 * k / 10) for k in range(11)]
        c.add("line", seam, LG_D, 0, 1.6)
    c.add("poly", [(.5, .36), (.60, .70), (.40, .70)], LG_DD, 3)
    for i in range(5):
        a = -math.pi / 2 + i / 5 * math.tau
        b = a + math.tau / 10
        c.add("poly", [(.5, .55), (.5 + math.cos(a) * .045, .55 + math.sin(a) * .045), (.5 + math.cos(b) * .018, .55 + math.sin(b) * .018)], LG_L, 0)
    c.add("line", [(.5, .10), (.5, .04)], LG_DD, 2.5, 4)
    c.add("poly", [(.5, .04), (.56, .06), (.5, .08)], LG_L, 2)


# ==================== 未完成品 ====================
def unfinished_flesh(c):
    c.shadow(.18, .72, .82, .9)
    c.add("poly", blob(.5, .56, .30, .20, 14, .2, .7), FLESH, 4, shade=FLESH_D, light=FLESH_L)
    c.add("poly", blob(.40, .50, .10, .08, 10, .2, 1.5), FAT, 2.5, shade=FAT_D)
    c.add("line", [(.58, .40), (.70, .26)], BONE, 3, 5)
    c.add("line", wave(.30, .62, .70, .62, 8, .015), MUSCLE_D, 0, 2)


def unfinished_leather(c):
    c.shadow(.18, .72, .82, .9)
    c.add("poly", [(.22, .40), (.70, .32), (.80, .62), (.30, .72)], LG, 4, shade=LG_D, light=LG_L)
    c.add("poly", [(.30, .48), (.60, .44), (.66, .60), (.36, .64)], LG_D, 2.5)
    stitches(c, [(.30 + .30 * i / 8, .48 - .04 * i / 8) for i in range(9)], STITCH)
    c.add("line", [(.62, .30), (.82, .18)], LG_DD, 2.5, 3)


SETS = {
    "FleshArtSmall": (128, 128, 1.0, [flesh_small_knot, flesh_small_face, flesh_small_totem]),
    "FleshArtLarge": (384, 384, 1.07, [flesh_large_figure, flesh_large_heart]),
    "FleshArtGrand": (512, 512, 1.04, [flesh_grand_arch, flesh_grand_embrace]),
    "LeatherCarvingSmall": (128, 128, 1.0, [leather_small_hide, leather_small_figurine, leather_small_mask]),
    "LeatherCarvingLarge": (384, 384, 1.07, [leather_large_figure, leather_large_banner]),
    "LeatherCarvingGrand": (512, 512, 1.04, [leather_grand_pavilion, leather_grand_dragon]),
}
SINGLES = {"UnfinishedFleshArt": unfinished_flesh, "UnfinishedLeatherCarving": unfinished_leather}

# 预览里给皮雕乘一个皮革色，看染色效果。
TINT = (176, 128, 92)


def tinted(im, col):
    r, g, b, a = im.split()
    r = r.point(lambda x: x * col[0] // 255); g = g.point(lambda x: x * col[1] // 255); b = b.point(lambda x: x * col[2] // 255)
    return Image.merge("RGBA", (r, g, b, a))


def main():
    os.makedirs(OUT, exist_ok=True)
    tiles = []
    for name, (w, h, sc, fns) in SETS.items():
        d = os.path.join(OUT, name)
        os.makedirs(d, exist_ok=True)
        for i, fn in enumerate(fns):
            c = Canvas(w, h, sc, .70 if w == 384 else .78)
            fn(c)
            im = c.final()
            im.save(os.path.join(d, "%s_%s.png" % (name, "ABC"[i])))
            tiles.append((name, im))
    for name, fn in SINGLES.items():
        c = Canvas(128, 128)
        fn(c)
        im = c.final()
        im.save(os.path.join(OUT, name + ".png"))
        tiles.append((name, im))
    # 预览：每张缩到 192 格，皮雕另附一份乘色。
    cell = 192
    row = []
    for name, im in tiles:
        t = im.resize((cell, cell), Image.LANCZOS)
        row.append(t)
        if "Leather" in name:
            row.append(tinted(t, TINT))
    cols = 6
    rows = (len(row) + cols - 1) // cols
    sheet = Image.new("RGBA", (cols * cell, rows * cell), (92, 88, 80, 255))
    for i, t in enumerate(row):
        sheet.alpha_composite(t, ((i % cols) * cell, (i // cols) * cell))
    os.makedirs(os.path.dirname(PREVIEW), exist_ok=True)
    sheet.save(PREVIEW)
    print("wrote", len(tiles), "textures and", PREVIEW)


if __name__ == "__main__":
    main()
