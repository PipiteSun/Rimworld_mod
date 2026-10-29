# 1.62.0 兽化症覆盖层：狼 / 熊 / 虎三套兽形，各有头部覆盖（兽面）和身披覆盖（兽皮披肩），南 / 东 / 北三向（西向由原版镜像）；
# 另画两个能力图标：心灵冲击（远域之触）、化身野兽（驯服野性）。
# 画法照 RimWorld 原版：粗黑描边、平涂 + 右下暗边 + 左上亮边、低饱和；只有眼睛的微光是亮色。
# 画布：头 / 身覆盖 128px（与原版头、身贴图同尺寸，脸大致在 (0.25..0.75, 0.22..0.80)）；图标 128px。
# 用法：python -I ArtSource/RaceArt/draw_dndtraits_162.py <模组根目录>
import math, os, sys
from PIL import Image, ImageDraw, ImageFilter, ImageChops

ROOT = sys.argv[1] if len(sys.argv) > 1 else os.getcwd()
OUT = os.path.join(ROOT, "Textures", "LP", "DndTraits")
PREVIEW = os.path.join(ROOT, "ArtSource", "Preview", "DndTraits-1.62.0.png")
SS = 3
INK = (34, 30, 34)

# 每种兽：主色 / 暗 / 亮、腹面浅色、鼻头、眼光、是否有条纹
BEASTS = {
    "Wolf": dict(fur=(118, 118, 116), dark=(86, 86, 88), light=(150, 150, 146), pale=(178, 174, 164), nose=(46, 42, 44), eye=(214, 196, 120), stripe=None, ear="tall"),
    "Bear": dict(fur=(108, 82, 60), dark=(80, 60, 44), light=(136, 106, 80), pale=(150, 122, 94), nose=(42, 34, 32), eye=(200, 150, 90), stripe=None, ear="round"),
    "Tiger": dict(fur=(176, 114, 58), dark=(138, 86, 44), light=(204, 144, 86), pale=(206, 192, 168), nose=(96, 62, 58), eye=(190, 206, 110), stripe=(52, 40, 34), ear="small"),
}


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
        return m

    def _fill(self, m, color):
        self.img.paste(Image.new("RGBA", self.img.size, tuple(color) + (255,)), (0, 0), m)

    def add(self, kind, a, color, ow=3.0, w=0, shade=None, light=None, sd=.035, clip=None):
        m = self.mask(kind, a, w)
        if clip is not None:
            m = ImageChops.multiply(m, clip)
        if ow:
            k = max(3, int(ow * SS) * 2 + 1)
            self._fill(m.filter(ImageFilter.MaxFilter(k)), INK)
        self._fill(m, color)
        bb = m.getbbox()
        if bb and (shade or light):
            size = max(bb[2] - bb[0], bb[3] - bb[1])
            off = min(max(2, int(size * sd * 2.4)), 4 * SS)
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


def tufts(cx, cy, r, n, start, end, length=.05):
    """一圈毛尖：沿 (cx,cy) 半径 r 的弧，从 start 到 end 度，n 个三角。"""
    pts = []
    for k in range(n * 2 + 1):
        t = start + (end - start) * k / (n * 2)
        a = math.radians(t)
        rr = r + (length if k % 2 else 0)
        pts.append((cx + math.cos(a) * rr, cy + math.sin(a) * rr))
    return pts


def mirror(pts):
    return [(1 - u, v) for u, v in pts]


# ---------------- 头部覆盖（兽面盔：盖住头顶和额头，口鼻朝向面朝方向） ----------------
def head(name, facing):
    b = BEASTS[name]
    c = Canvas(128, 128)
    fur, dark, light, pale = b["fur"], b["dark"], b["light"], b["pale"]
    if facing == "south":
        # 耳朵
        if b["ear"] == "tall":
            ear = [(.27, .40), (.30, .12), (.43, .30)]
        elif b["ear"] == "round":
            ear = None
            c.add("ell", (.22, .16, .40, .34), fur, shade=dark, light=light)
            c.add("ell", (.60, .16, .78, .34), fur, shade=dark, light=light)
            c.add("ell", (.27, .21, .35, .29), pale, ow=0)
            c.add("ell", (.65, .21, .73, .29), pale, ow=0)
        else:
            ear = [(.26, .36), (.29, .17), (.41, .28)]
        if ear:
            c.add("poly", ear, fur, shade=dark, light=light)
            c.add("poly", mirror(ear), fur, shade=dark, light=light)
            inner = [(.30, .34), (.31, .21), (.39, .30)]
            c.add("poly", inner, pale, ow=0)
            c.add("poly", mirror(inner), pale, ow=0)
        # 头皮：盖住额头，两侧垂到腮
        cap = tufts(.5, .52, .25, 9, 160, 380, .035)
        cap = [(.5, .58)] + cap
        c.add("poly", [(.25, .58), (.24, .40), (.32, .26), (.50, .22), (.68, .26), (.76, .40), (.75, .58), (.66, .50), (.50, .47), (.34, .50)], fur, shade=dark, light=light)
        if b["stripe"]:
            for x0 in (.36, .50, .64):
                c.add("poly", [(x0 - .025, .24), (x0 + .025, .24), (x0 + .005, .36), (x0 - .005, .36)], b["stripe"], ow=0)
        # 腮毛
        c.add("poly", tufts(.27, .56, .05, 3, 90, 270, .04), fur, ow=2.4, shade=dark)
        c.add("poly", mirror(tufts(.27, .56, .05, 3, 90, 270, .04)), fur, ow=2.4, shade=dark)
        # 口鼻（盖在脸中下部）
        snout_len = .62 if name == "Wolf" else .60
        c.add("ell", (.38, .46, .62, snout_len + .06), pale, shade=dark, light=light)
        c.add("ell", (.45, .47, .55, .54), b["nose"], ow=2.0)
        # 眼光（额下两点）
        for x in (.38, .62):
            c.glow("ell", (x - .03, .40, x + .03, .46), b["eye"], radius=2, alpha=170)
            c.add("ell", (x - .016, .415, x + .016, .445), b["eye"], ow=1.4)
    elif facing == "east":
        ear = [(.40, .30), (.46, .10), (.56, .28)] if b["ear"] == "tall" else [(.42, .30), (.47, .16), (.56, .29)]
        if b["ear"] == "round":
            c.add("ell", (.38, .14, .56, .32), fur, shade=dark, light=light)
        else:
            c.add("poly", ear, fur, shade=dark, light=light)
        c.add("poly", [(.26, .56), (.24, .38), (.34, .24), (.54, .22), (.68, .30), (.72, .44), (.66, .52), (.48, .54), (.36, .62)], fur, shade=dark, light=light)
        if b["stripe"]:
            for x0 in (.36, .46, .56):
                c.add("poly", [(x0, .25), (x0 + .04, .25), (x0 + .02, .40)], b["stripe"], ow=0)
        # 口鼻朝右伸出
        reach = .90 if name == "Wolf" else .84 if name == "Tiger" else .86
        c.add("poly", [(.60, .40), (reach, .46), (reach, .54), (.70, .60), (.56, .58)], pale, shade=dark, light=light)
        c.add("ell", (reach - .06, .43, reach + .01, .50), b["nose"], ow=2.0)
        c.add("line", [(.70, .57), (reach - .04, .55)], dark, ow=0, w=1.6)
        c.glow("ell", (.58, .37, .64, .43), b["eye"], radius=2, alpha=170)
        c.add("ell", (.595, .385, .625, .415), b["eye"], ow=1.4)
        c.add("poly", tufts(.30, .58, .06, 3, 90, 250, .04), fur, ow=2.4, shade=dark)
    else:  # north：只看到后脑的毛和两只耳朵
        if b["ear"] == "round":
            c.add("ell", (.22, .16, .40, .34), fur, shade=dark, light=light)
            c.add("ell", (.60, .16, .78, .34), fur, shade=dark, light=light)
        else:
            ear = [(.27, .40), (.30, .12), (.43, .30)] if b["ear"] == "tall" else [(.26, .36), (.29, .17), (.41, .28)]
            c.add("poly", ear, dark, shade=None, light=fur)
            c.add("poly", mirror(ear), dark, shade=None, light=fur)
        c.add("poly", [(.24, .62), (.23, .40), (.32, .25), (.50, .21), (.68, .25), (.77, .40), (.76, .62), (.62, .72), (.50, .74), (.38, .72)], fur, shade=dark, light=light)
        c.add("poly", tufts(.5, .70, .12, 5, 20, 160, .045), fur, ow=2.4, shade=dark)
        if b["stripe"]:
            for x0 in (.34, .44, .56, .66):
                c.add("poly", [(x0 - .02, .30), (x0 + .02, .30), (x0 + .01, .56), (x0 - .01, .56)], b["stripe"], ow=0)
        else:
            c.add("line", [(.50, .26), (.50, .62)], dark, ow=0, w=3)
    return c.save("Beast_%s_Head_%s" % (name, facing))


# ---------------- 身披覆盖（兽皮披肩：盖住肩和上背，前面在胸口打结） ----------------
def body(name, facing):
    b = BEASTS[name]
    c = Canvas(128, 128)
    fur, dark, light, pale = b["fur"], b["dark"], b["light"], b["pale"]
    if facing == "south":
        pelt = [(.20, .50), (.24, .36), (.36, .30), (.50, .32), (.64, .30), (.76, .36), (.80, .50), (.74, .60), (.62, .52), (.56, .58), (.50, .66), (.44, .58), (.38, .52), (.26, .60)]
        c.add("poly", pelt, fur, shade=dark, light=light)
        c.add("poly", tufts(.23, .58, .04, 3, 60, 200, .035), fur, ow=2.2, shade=dark)
        c.add("poly", mirror(tufts(.23, .58, .04, 3, 60, 200, .035)), fur, ow=2.2, shade=dark)
        # 胸前垂下的两只兽爪 + 系绳
        for x in (.43, .57):
            c.add("poly", [(x - .04, .55), (x + .04, .55), (x + .03, .70), (x - .03, .70)], fur, ow=2.4, shade=dark)
            for k in (-.02, 0, .02):
                c.add("line", [(x + k, .70), (x + k, .735)], pale, ow=1.2, w=1.6)
        c.add("ell", (.46, .53, .54, .60), (108, 84, 62), ow=2.2)
    elif facing == "east":
        pelt = [(.30, .54), (.32, .36), (.46, .30), (.62, .34), (.70, .46), (.64, .58), (.52, .52), (.40, .60)]
        c.add("poly", pelt, fur, shade=dark, light=light)
        c.add("poly", [(.56, .52), (.64, .53), (.62, .68), (.56, .68)], fur, ow=2.4, shade=dark)
        for k in (-.02, 0, .02):
            c.add("line", [(.59 + k, .68), (.59 + k, .715)], pale, ow=1.2, w=1.6)
        c.add("poly", tufts(.34, .58, .05, 3, 80, 220, .04), fur, ow=2.2, shade=dark)
    else:  # north：整张兽皮盖住后背，尾巴垂下
        pelt = [(.20, .48), (.25, .34), (.40, .29), (.60, .29), (.75, .34), (.80, .48), (.74, .66), (.62, .74), (.50, .76), (.38, .74), (.26, .66)]
        c.add("poly", pelt, fur, shade=dark, light=light)
        c.add("poly", tufts(.5, .72, .14, 5, 20, 160, .045), fur, ow=2.4, shade=dark)
        tail = [(.46, .74), (.54, .74), (.56, .88), (.50, .93), (.44, .88)] if name != "Bear" else [(.46, .74), (.54, .74), (.52, .80), (.48, .80)]
        c.add("poly", tail, fur, ow=2.6, shade=dark, light=light)
        if name == "Wolf":
            c.add("poly", [(.47, .87), (.53, .87), (.50, .93)], pale, ow=0)
    if b["stripe"]:
        clip = c.img.split()[3]
        for k in range(6):
            x = .26 + k * .095
            c.add("poly", [(x, .30), (x + .035, .30), (x + .015, .56), (x - .005, .56)], b["stripe"], ow=0, clip=clip)
    return c.save("Beast_%s_Body_%s" % (name, facing))


def icon_shock():
    c = Canvas(128, 128)
    c.glow("poly", [(.18, .50), (.88, .18), (.88, .82)], (150, 110, 210), radius=9, alpha=150)
    c.add("poly", [(.20, .50), (.86, .22), (.80, .50), (.86, .78)], (96, 74, 132), shade=(70, 54, 100), light=(126, 100, 166))
    for r in (.18, .30, .42):
        pts = [(.20 + math.cos(math.radians(a)) * r, .50 + math.sin(math.radians(a)) * r) for a in range(-26, 27, 4)]
        c.add("line", pts, (196, 176, 230), ow=1.6, w=2.4)
    c.add("ell", (.10, .40, .30, .60), (60, 48, 80), ow=3, light=(90, 74, 116))
    c.glow("ell", (.15, .45, .25, .55), (210, 190, 250), radius=3, alpha=200)
    c.add("ell", (.17, .47, .23, .53), (224, 210, 250), ow=0)
    return c.save("PsychicShock")


def icon_shape():
    c = Canvas(128, 128)
    b = BEASTS["Wolf"]
    c.add("ell", (.14, .14, .86, .86), (90, 70, 56), shade=(66, 50, 40), light=(118, 94, 74))
    c.add("poly", [(.30, .46), (.33, .20), (.46, .36), (.54, .36), (.67, .20), (.70, .46), (.66, .66), (.50, .80), (.34, .66)], b["fur"], shade=b["dark"], light=b["light"])
    c.add("ell", (.40, .56, .60, .78), b["pale"], ow=2.4, shade=b["dark"])
    c.add("ell", (.46, .58, .54, .64), b["nose"], ow=1.8)
    for x in (.40, .60):
        c.glow("ell", (x - .035, .44, x + .035, .52), b["eye"], radius=2, alpha=180)
        c.add("ell", (x - .02, .46, x + .02, .50), b["eye"], ow=1.4)
    # 下方一弯月牙：驯服的野性
    c.add("poly", [(.30, .86), (.50, .80), (.70, .86), (.50, .84)], (214, 206, 176), ow=2.0)
    return c.save("Beastshape")


def main():
    rows = []
    for name in BEASTS:
        row = []
        for f in ("south", "east", "north"):
            row.append((body(name, f), head(name, f)))
        rows.append(row)
    icons = [icon_shock(), icon_shape()]
    cell = 128 * 2
    W = 20 + 3 * (cell + 20)
    H = 20 + 3 * (cell + 20) + cell // 2 + 20
    pv = Image.new("RGBA", (W, H), (150, 140, 120, 255))
    skin = (222, 190, 160, 255)
    for r, row in enumerate(rows):
        for k, (bo, he) in enumerate(row):
            x, y = 20 + k * (cell + 20), 20 + r * (cell + 20)
            # 粗略的人形底：身体椭圆 + 头圆，方便看覆盖层的位置
            base = Image.new("RGBA", (128, 128), (0, 0, 0, 0))
            d = ImageDraw.Draw(base)
            d.ellipse([34, 44, 94, 120], fill=(120, 110, 96, 255), outline=(34, 30, 34, 255), width=3)
            comp = Image.alpha_composite(base, bo)
            hb = Image.new("RGBA", (128, 128), (0, 0, 0, 0))
            ImageDraw.Draw(hb).ellipse([36, 30, 92, 96], fill=skin, outline=(34, 30, 34, 255), width=3)
            hb = Image.alpha_composite(hb, he)
            comp.alpha_composite(hb.resize((96, 96), Image.LANCZOS), (16, -18))
            pv.alpha_composite(comp.resize((cell, cell), Image.NEAREST), (x, y))
    y = 20 + 3 * (cell + 20)
    for k, i in enumerate(icons):
        pv.alpha_composite(i, (20 + k * 148, y))
    os.makedirs(os.path.dirname(PREVIEW), exist_ok=True)
    pv.save(PREVIEW)
    print("wrote", OUT, PREVIEW)


if __name__ == "__main__":
    main()
