# 1.61.0 制伏武器贴图：制伏短棍、缚网弩、安眠短弓、安眠法杖、安眠粉囊，以及束缚网、安眠箭、安眠魔弹、粉囊投射物。
# 画法照 RimWorld 原版：粗黑描边、平涂 + 右下暗边 + 左上亮边、低饱和；安眠符文与安眠粉的淡紫光是唯一的亮色。
# 画布：武器 128px（近战、法杖从左下指向右上；弓弩横放朝右，和原版一样靠 equippedAngleOffset 转）；投射物 64px，朝上。
# 用法：python -I ArtSource/RaceArt/draw_nonlethal.py <模组根目录>
import math, os, sys
from PIL import Image, ImageDraw, ImageFilter, ImageChops

ROOT = sys.argv[1] if len(sys.argv) > 1 else os.getcwd()
OUT = os.path.join(ROOT, "Textures", "LP", "Nonlethal")
PREVIEW = os.path.join(ROOT, "ArtSource", "Preview", "Nonlethal-1.61.0.png")
SS = 3
INK = (34, 30, 34)

WOOD, WOOD_D, WOOD_L = (128, 96, 66), (94, 70, 48), (158, 124, 90)
LEATHER, LEATHER_D, LEATHER_L = (108, 80, 60), (80, 58, 44), (136, 104, 78)
IRON, IRON_D, IRON_L = (120, 122, 128), (88, 90, 98), (158, 160, 166)
ROPE, ROPE_D, ROPE_L = (152, 148, 136), (114, 110, 100), (182, 178, 166)
GREEN, GREEN_D, GREEN_L = (80, 100, 72), (58, 74, 52), (104, 126, 94)
BONE, BONE_D, BONE_L = (190, 184, 170), (150, 144, 132), (214, 210, 198)
CLOTH, CLOTH_D, CLOTH_L = (112, 90, 140), (84, 66, 108), (140, 116, 166)
RUNE, RUNE_L, RUNE_D = (176, 146, 214), (222, 204, 244), (130, 104, 166)
FLETCH = (196, 178, 218)


class Canvas:
    def __init__(self, w, h):
        self.W, self.H = w, h
        self.img = Image.new("RGBA", (w * SS, h * SS), (0, 0, 0, 0))

    def P(self, u, v):
        return u * self.W * SS, v * self.H * SS

    def mask(self, kind, a, w=0):
        m = Image.new("L", self.img.size, 0)
        d = ImageDraw.Draw(m)
        if kind == "ell":
            (x0, y0), (x1, y1) = self.P(a[0], a[1]), self.P(a[2], a[3])
            d.ellipse([x0, y0, x1, y1], fill=255)
        elif kind == "poly":
            d.polygon([self.P(u, v) for u, v in a], fill=255)
        elif kind == "line":
            pts = [self.P(u, v) for u, v in a]
            ww = max(1, int(w * SS))
            d.line(pts, fill=255, width=ww, joint="curve")
            for x, y in pts:
                d.ellipse([x - ww / 2, y - ww / 2, x + ww / 2, y + ww / 2], fill=255)
        elif kind == "arc":  # a = (cx, cy, r_outer, r_inner, start_deg, end_deg)
            cx, cy, ro, ri, s, e = a
            (x0, y0), (x1, y1) = self.P(cx - ro, cy - ro), self.P(cx + ro, cy + ro)
            d.pieslice([x0, y0, x1, y1], s, e, fill=255)
            (x0, y0), (x1, y1) = self.P(cx - ri, cy - ri), self.P(cx + ri, cy + ri)
            d.pieslice([x0, y0, x1, y1], s - 1, e + 1, fill=0)
        return m

    def _fill(self, m, color):
        self.img.paste(Image.new("RGBA", self.img.size, tuple(color) + (255,)), (0, 0), m)

    def add(self, kind, a, color, ow=3.0, w=0, shade=None, light=None, sd=.035):
        m = self.mask(kind, a, w)
        if ow:
            k = max(3, int(ow * SS) * 2 + 1)
            self._fill(m.filter(ImageFilter.MaxFilter(k)), INK)
        self._fill(m, color)
        bb = m.getbbox()
        if bb and (shade or light):
            size = max(bb[2] - bb[0], bb[3] - bb[1])
            off = max(2, int(size * sd * 2.4))
            off = min(off, 4 * SS)
            if shade:
                self._fill(ImageChops.subtract(m, ImageChops.offset(m, -off, -off)), shade)
            if light:
                self._fill(ImageChops.subtract(m, ImageChops.offset(m, off // 2 + 1, off // 2 + 1)), light)
        return m

    def glow(self, kind, a, color, radius=6, alpha=150, w=0):
        m = self.mask(kind, a, w).filter(ImageFilter.GaussianBlur(radius * SS))
        m = m.point(lambda v: min(255, int(v * alpha / 255)))
        layer = Image.new("RGBA", self.img.size, tuple(color) + (0,))
        layer.putalpha(m)
        self.img = Image.alpha_composite(self.img, layer)

    def save(self, name):
        os.makedirs(OUT, exist_ok=True)
        out = self.img.resize((self.W, self.H), Image.LANCZOS)
        out.save(os.path.join(OUT, name + ".png"))
        return out


def along(p0, p1, t, n=0.0):
    """点：p0→p1 上比例 t 处，再沿法线偏 n。"""
    dx, dy = p1[0] - p0[0], p1[1] - p0[1]
    L = math.hypot(dx, dy) or 1
    return (p0[0] + dx * t - dy / L * n, p0[1] + dy * t + dx / L * n)


def band(c, p0, p1, t, half, width, color, shade=None, light=None):
    a, b = along(p0, p1, t - width / 2, -half), along(p0, p1, t + width / 2, -half)
    cc, d = along(p0, p1, t + width / 2, half), along(p0, p1, t - width / 2, half)
    c.add("poly", [a, b, cc, d], color, ow=2.2, shade=shade, light=light)


def cudgel():
    c = Canvas(128, 128)
    p0, p1 = (.20, .82), (.80, .20)
    # 柄（皮革缠绕）与棍身（越往头越粗）
    c.add("poly", [along(p0, p1, 0, -.035), along(p0, p1, .45, -.04), along(p0, p1, 1, -.085), along(p0, p1, 1.02, 0), along(p0, p1, 1, .085), along(p0, p1, .45, .04), along(p0, p1, 0, .035)],
          WOOD, ow=3.2, shade=WOOD_D, light=WOOD_L)
    for k in range(5):
        band(c, p0, p1, .06 + k * .075, .045, .05, LEATHER, shade=LEATHER_D, light=LEATHER_L)
    px, py = along(p0, p1, -.01, 0)
    c.add("ell", (px - .045, py - .045, px + .045, py + .045), IRON_D, ow=2.5, light=IRON)
    # 铁箍 + 安眠符文
    for t in (.66, .86):
        band(c, p0, p1, t, .082 if t > .8 else .07, .07, IRON, shade=IRON_D, light=IRON_L)
    for t in (.66, .86):
        for n in (-.03, .0, .03):
            x, y = along(p0, p1, t, n)
            c.glow("ell", (x - .012, y - .012, x + .012, y + .012), RUNE, radius=2, alpha=150)
            c.add("ell", (x - .008, y - .008, x + .008, y + .008), RUNE_L, ow=0)
    return c.save("SubduingCudgel")


def net_crossbow():
    c = Canvas(128, 128)
    # 枪托（横放，朝右）
    c.add("poly", [(.06, .56), (.14, .50), (.70, .48), (.86, .50), (.86, .56), (.62, .58), (.30, .60), (.20, .70), (.08, .68)], WOOD, ow=3.2, shade=WOOD_D, light=WOOD_L)
    # 弩臂
    c.add("poly", [(.74, .18), (.80, .20), (.80, .80), (.74, .82), (.71, .50)], WOOD_D, ow=3, shade=(70, 52, 36), light=WOOD)
    c.add("line", [(.76, .20), (.60, .50), (.76, .80)], ROPE_L, ow=1.5, w=1.6)
    # 铁件
    c.add("poly", [(.70, .46), (.84, .46), (.84, .58), (.70, .58)], IRON, ow=2.4, shade=IRON_D, light=IRON_L)
    c.add("poly", [(.36, .58), (.42, .58), (.40, .68), (.35, .68)], IRON_D, ow=2)
    # 盘在枪托上的灰绳网
    c.add("ell", (.22, .30, .56, .52), ROPE, ow=3, shade=ROPE_D, light=ROPE_L)
    for i in range(5):
        x = .26 + i * .065
        c.add("line", [(x, .32), (x + .04, .50)], ROPE_D, ow=0, w=1.4)
    for y in (.37, .43, .48):
        c.add("line", [(.24, y), (.54, y)], ROPE_D, ow=0, w=1.4)
    # 前端的网弹
    c.add("ell", (.86, .46, .96, .58), ROPE, ow=2.4, shade=ROPE_D, light=ROPE_L)
    return c.save("NetCrossbow")


def sleep_bow():
    c = Canvas(128, 128)
    # 弓臂（竖弧，开口朝左）
    c.add("arc", (.30, .50, .44, .38, 300, 60), GREEN, ow=3, shade=GREEN_D, light=GREEN_L)
    for ang in (300, 60):
        x, y = .30 + math.cos(math.radians(ang)) * .41, .50 + math.sin(math.radians(ang)) * .41
        c.add("ell", (x - .025, y - .025, x + .025, y + .025), WOOD_D, ow=2)
    c.add("poly", [(.70, .42), (.76, .42), (.76, .58), (.70, .58)], LEATHER, ow=2.4, shade=LEATHER_D, light=LEATHER_L)
    x1, y1 = .30 + math.cos(math.radians(300)) * .41, .50 + math.sin(math.radians(300)) * .41
    x2, y2 = .30 + math.cos(math.radians(60)) * .41, .50 + math.sin(math.radians(60)) * .41
    c.add("line", [(x1, y1), (.42, .50), (x2, y2)], ROPE_L, ow=1.2, w=1.3)
    # 搭着的安眠箭：淡紫箭羽、发光紫粉箭头
    c.add("line", [(.36, .50), (.86, .50)], WOOD_L, ow=2, w=2.2)
    for dy in (-.05, .05):
        c.add("poly", [(.36, .50), (.46, .50), (.40, .50 + dy), (.32, .50 + dy)], FLETCH, ow=2, shade=RUNE_D)
    c.glow("ell", (.80, .43, .96, .57), RUNE, radius=4, alpha=200)
    c.add("poly", [(.84, .45), (.95, .50), (.84, .55)], RUNE_L, ow=2.4, shade=RUNE)
    return c.save("SleepBow")


def sleep_staff():
    c = Canvas(128, 128)
    p0, p1 = (.14, .90), (.70, .30)
    # 微弯的灰白木杖
    c.add("line", [p0, along(p0, p1, .5, .025), p1], BONE, ow=3, w=6.5, shade=BONE_D, light=BONE_L)
    for t in (.18, .30):
        band(c, p0, p1, t, .03, .04, LEATHER, shade=LEATHER_D)
    # 月牙托
    c.add("arc", (.74, .24, .17, .11, 110, 360), BONE, ow=3, shade=BONE_D, light=BONE_L)
    # 淡紫水晶
    c.glow("ell", (.62, .10, .88, .36), RUNE, radius=7, alpha=200)
    c.add("poly", [(.75, .12), (.83, .22), (.76, .34), (.68, .23)], RUNE, ow=2.6, shade=RUNE_D, light=RUNE_L)
    return c.save("SleepStaff")


def dust_pouch():
    c = Canvas(128, 128)
    # 冒出的紫粉
    c.glow("ell", (.30, .02, .78, .40), RUNE, radius=8, alpha=150)
    for x, y, r in ((.46, .20, .07), (.58, .12, .055), (.38, .10, .045), (.64, .26, .04)):
        c.add("ell", (x - r, y - r, x + r, y + r), RUNE_L, ow=0)
    # 布袋
    c.add("poly", [(.40, .40), (.60, .40), (.66, .48), (.80, .66), (.78, .86), (.62, .94), (.38, .94), (.22, .86), (.20, .66), (.34, .48)], CLOTH, ow=3.2, shade=CLOTH_D, light=CLOTH_L)
    c.add("poly", [(.36, .30), (.44, .40), (.56, .40), (.64, .30), (.58, .44), (.42, .44)], CLOTH_L, ow=2.5, shade=CLOTH)
    # 扎口绳
    c.add("line", [(.34, .44), (.66, .44)], ROPE, ow=2, w=3)
    c.add("line", [(.62, .45), (.72, .56), (.70, .62)], ROPE, ow=1.6, w=2)
    # 布上缝的安眠符文
    for x, y in ((.40, .72), (.52, .66), (.60, .78)):
        c.add("line", [(x - .02, y - .03), (x + .01, y), (x - .015, y + .03)], RUNE_L, ow=0, w=1.6)
    return c.save("SleepDustPouch")


def proj_net():
    c = Canvas(64, 64)
    c.add("ell", (.12, .12, .88, .88), ROPE, ow=2.5, shade=ROPE_D, light=ROPE_L)
    for i in range(4):
        t = .26 + i * .16
        c.add("line", [(t, .16), (t, .84)], ROPE_D, ow=0, w=1.2)
        c.add("line", [(.16, t), (.84, t)], ROPE_D, ow=0, w=1.2)
    for x, y in ((.12, .5), (.88, .5), (.5, .12), (.5, .88)):
        c.add("ell", (x - .07, y - .07, x + .07, y + .07), IRON, ow=1.8, shade=IRON_D)
    return c.save("Proj_Net")


def proj_arrow():
    c = Canvas(64, 64)
    c.add("line", [(.5, .90), (.5, .20)], WOOD_L, ow=1.6, w=1.8)
    for dx in (-.08, .08):
        c.add("poly", [(.5, .90), (.5, .74), (.5 + dx, .80), (.5 + dx, .94)], FLETCH, ow=1.6)
    c.glow("ell", (.38, .04, .62, .30), RUNE, radius=3, alpha=210)
    c.add("poly", [(.43, .22), (.5, .06), (.57, .22)], RUNE_L, ow=1.8, shade=RUNE)
    return c.save("Proj_SleepArrow")


def proj_bolt():
    c = Canvas(64, 64)
    c.glow("ell", (.22, .10, .78, .90), RUNE, radius=6, alpha=200)
    c.add("ell", (.36, .24, .64, .76), RUNE, ow=0)
    c.add("ell", (.42, .30, .58, .62), RUNE_L, ow=0)
    return c.save("Proj_SleepBolt")


def proj_dust():
    c = Canvas(64, 64)
    c.glow("ell", (.30, .00, .70, .36), RUNE, radius=4, alpha=170)
    c.add("ell", (.42, .08, .56, .22), RUNE_L, ow=0)
    c.add("poly", [(.40, .34), (.60, .34), (.74, .60), (.70, .84), (.50, .92), (.30, .84), (.26, .60)], CLOTH, ow=2.5, shade=CLOTH_D, light=CLOTH_L)
    c.add("line", [(.36, .36), (.64, .36)], ROPE, ow=1.6, w=2.2)
    return c.save("Proj_Dust")


def main():
    big = [cudgel(), net_crossbow(), sleep_bow(), sleep_staff(), dust_pouch()]
    small = [proj_net(), proj_arrow(), proj_bolt(), proj_dust()]
    W = 20 + sum(i.width * 2 + 20 for i in big)
    H = 20 + 256 + 20 + 128 + 20
    pv = Image.new("RGBA", (W, H), (150, 140, 120, 255))
    x = 20
    for i in big:
        pv.alpha_composite(i.resize((i.width * 2, i.height * 2), Image.NEAREST), (x, 20))
        x += i.width * 2 + 20
    x = 20
    for i in small:
        pv.alpha_composite(i.resize((i.width * 2, i.height * 2), Image.NEAREST), (x, 296))
        x += i.width * 2 + 20
    os.makedirs(os.path.dirname(PREVIEW), exist_ok=True)
    pv.save(PREVIEW)
    print("wrote", OUT, PREVIEW)


if __name__ == "__main__":
    main()
