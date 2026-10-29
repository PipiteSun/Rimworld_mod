# 1.60.0 迷锁地牢贴图：迷锁之门（开启 / 封闭）、迷锁回响（出口 / 回程门）、迷锁石墙连接图集、迷锁灯、迷锁泉、迷锁碎片。
# 画法照 RimWorld 原版：粗黑描边、平涂 + 右下暗边 + 左上亮边、低饱和；迷锁的淡蓝符文光是唯一的亮色。
# 画布：1 格 128px；门和回响 3×3 → 384px；泉 2×2 → 256px；墙图集 4×4 块、每块 128px（原版连接图集：第 i 块在第 i%4 列、自下往上第 i/4 行，
# 上=1 右=2 下=4 左=8；没连接的一边内缩 1/8 画描边，连上的一边画满到块边）。
# 用法：python -I ArtSource/RaceArt/draw_mythal_160.py <模组根目录>
import math, os, random, sys
from PIL import Image, ImageDraw, ImageFilter, ImageChops

ROOT = sys.argv[1] if len(sys.argv) > 1 else os.getcwd()
OUT = os.path.join(ROOT, "Textures", "LP", "Mythal")
PREVIEW = os.path.join(ROOT, "ArtSource", "Preview", "Mythal-1.60.0.png")
SS = 3
INK = (32, 32, 38)

STONE, STONE_D, STONE_L = (128, 128, 136), (94, 94, 104), (160, 160, 166)
SLATE, SLATE_D, SLATE_L = (82, 86, 98), (62, 64, 76), (104, 108, 120)
FACE = (118, 120, 132)
RUNE, RUNE_L, RUNE_DIM = (120, 170, 214), (190, 222, 240), (78, 96, 116)
WATER, WATER_D, WATER_L = (96, 142, 168), (70, 108, 132), (150, 192, 210)
VOID = (40, 52, 74)


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
        elif kind == "arc":  # a = (cx, cy, r_outer, r_inner, start_deg, end_deg)
            cx, cy, ro, ri, s, e = a
            (x0, y0), (x1, y1) = self.P(cx - ro, cy - ro), self.P(cx + ro, cy + ro)
            d.pieslice([x0, y0, x1, y1], s, e, fill=255)
            (x0, y0), (x1, y1) = self.P(cx - ri, cy - ri), self.P(cx + ri, cy + ri)
            d.pieslice([x0, y0, x1, y1], s - 1, e + 1, fill=0)
        return m

    def _fill(self, m, color):
        self.img.paste(Image.new("RGBA", self.img.size, tuple(color) + (255,)), (0, 0), m)

    def add(self, kind, a, color, ow=3.5, w=0, shade=None, light=None, sd=.035):
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


def runes(c, cx, cy, r, n, color, size=.012, start=0.0):
    for i in range(n):
        a = start + i * 2 * math.pi / n
        x, y = cx + math.cos(a) * r, cy + math.sin(a) * r
        c.add("line", [(x - size, y - size * 1.4), (x + size * .3, y), (x - size * .6, y + size * 1.4)], color, ow=0, w=1.6)


def gate(open_):
    c = Canvas(384, 384)
    # 台基
    c.add("rect", (.08, .70, .92, .93, 6), STONE_D, shade=SLATE, light=STONE)
    c.add("rect", (.14, .64, .86, .78, 5), STONE, shade=STONE_D, light=STONE_L)
    # 门洞（开启：漩涡；封闭：石板）
    hole = [(.30, .74), (.30, .40), (.36, .27), (.50, .21), (.64, .27), (.70, .40), (.70, .74)]
    if open_:
        c.glow("poly", hole, RUNE, radius=9, alpha=170)
        c.add("poly", hole, VOID, ow=2.5)
        for k in range(4):
            r0 = .17 - k * .04
            c.add("arc", (.50, .50, r0, r0 - .018, 200 + k * 50, 340 + k * 50), RUNE if k % 2 == 0 else RUNE_L, ow=0)
        c.glow("ell", (.44, .44, .56, .56), RUNE_L, radius=5, alpha=200)
    else:
        c.add("poly", hole, SLATE_D, ow=2.5)
        for y in (.36, .48, .60):
            c.add("line", [(.32, y), (.68, y)], SLATE, ow=0, w=2)
        c.add("line", [(.50, .48), (.50, .60)], SLATE, ow=0, w=2)
    # 两根柱子与拱
    for x0 in (.18, .70):
        c.add("rect", (x0, .30, x0 + .12, .76, 3), STONE, shade=STONE_D, light=STONE_L)
        c.add("rect", (x0 - .02, .72, x0 + .14, .79, 2), STONE_D, ow=3)
        for y in (.42, .55, .66):
            c.add("line", [(x0 + .02, y), (x0 + .10, y)], STONE_D, ow=0, w=1.5)
    c.add("arc", (.50, .40, .32, .20, 180, 360), STONE, shade=STONE_D, light=STONE_L)
    c.add("rect", (.45, .06, .55, .16, 2), STONE_L, shade=STONE_D)
    rc = RUNE if open_ else RUNE_DIM
    if open_:
        c.glow("arc", (.50, .40, .29, .23, 185, 355), RUNE, radius=4, alpha=120)
    runes(c, .50, .40, .26, 9, rc, start=math.pi * 1.06)
    for x0 in (.24, .76):
        for y in (.40, .52, .62):
            c.add("ell", (x0 - .012, y - .012, x0 + .012, y + .012), rc, ow=0)
    return c.save("GateOpen" if open_ else "GateSealed")


def exit_portal():
    c = Canvas(384, 384)
    c.add("ell", (.10, .16, .90, .90), SLATE_D, shade=INK, light=SLATE)
    c.add("ell", (.18, .24, .82, .82), SLATE, shade=SLATE_D, light=SLATE_L)
    c.glow("ell", (.26, .30, .74, .76), RUNE, radius=10, alpha=170)
    c.add("ell", (.30, .34, .70, .72), VOID, ow=2.5)
    for k in range(3):
        r0 = .17 - k * .05
        c.add("arc", (.50, .53, r0, r0 - .02, 30 + k * 70, 220 + k * 70), RUNE_L if k == 1 else RUNE, ow=0)
    runes(c, .50, .53, .29, 12, RUNE)
    for x in (.14, .86):
        c.add("rect", (x - .04, .40, x + .04, .66, 2), STONE, shade=STONE_D, light=STONE_L)
        c.glow("ell", (x - .03, .38, x + .03, .44), RUNE_L, radius=4, alpha=200)
        c.add("ell", (x - .02, .39, x + .02, .43), RUNE_L, ow=1.5)
    return c.save("Exit")


def lamp():
    c = Canvas(128, 128)
    c.add("ell", (.18, .66, .82, .92), STONE_D, shade=SLATE, light=STONE)
    c.glow("ell", (.20, .10, .80, .78), RUNE, radius=8, alpha=150)
    for poly, col in (
        ([(.38, .76), (.32, .40), (.42, .14), (.52, .40), (.50, .76)], RUNE),
        ([(.50, .78), (.52, .30), (.62, .18), (.70, .44), (.64, .78)], RUNE_L),
        ([(.28, .78), (.22, .54), (.30, .44), (.38, .60), (.38, .78)], RUNE),
    ):
        c.add("poly", poly, col, ow=2.5, shade=(96, 136, 176), light=(214, 236, 246))
    return c.save("Lamp")


def fountain():
    c = Canvas(256, 256)
    c.add("ell", (.06, .14, .94, .94), STONE_D, shade=SLATE, light=STONE)
    c.add("ell", (.14, .22, .86, .86), WATER, ow=2.5, shade=WATER_D, light=WATER_L)
    for r in (.14, .22, .30):
        c.add("arc", (.50, .54, r, r - .012, 200, 330), WATER_L, ow=0)
    c.add("rect", (.42, .30, .58, .60, 3), STONE, shade=STONE_D, light=STONE_L)
    c.glow("ell", (.38, .14, .62, .38), RUNE, radius=6, alpha=180)
    c.add("ell", (.42, .18, .58, .34), RUNE_L, ow=2.5, shade=RUNE)
    runes(c, .50, .54, .40, 10, RUNE)
    return c.save("Fountain")


def shard():
    c = Canvas(128, 128)
    c.glow("ell", (.18, .18, .82, .86), RUNE, radius=6, alpha=110)
    for poly, col in (
        ([(.30, .80), (.24, .46), (.40, .18), (.50, .50), (.44, .82)], RUNE),
        ([(.48, .84), (.52, .40), (.68, .24), (.76, .56), (.62, .86)], RUNE_L),
        ([(.38, .86), (.42, .62), (.54, .58), (.56, .86)], (100, 150, 196)),
    ):
        c.add("poly", poly, col, ow=3, shade=(88, 126, 168), light=(220, 240, 248))
    return c.save("Shard")


def wall_atlas():
    T = 128
    m = T // 8
    P = 24
    atlas = Image.new("RGBA", (T * 4, T * 4), (0, 0, 0, 0))
    rnd = random.Random(1600)
    for i in range(16):
        up, right, down, left = bool(i & 1), bool(i & 2), bool(i & 4), bool(i & 8)
        size = (T + 2 * P) * SS
        mk = Image.new("L", (size, size), 0)
        d = ImageDraw.Draw(mk)
        o = P * SS
        s = lambda v: o + v * SS
        rad = 2 * SS
        d.rounded_rectangle([s(m), s(m), s(T - m), s(T - m)], radius=rad, fill=255)
        if up: d.rectangle([s(m), 0, s(T - m), s(T // 2)], fill=255)
        if down: d.rectangle([s(m), s(T // 2), s(T - m), size], fill=255)
        if left: d.rectangle([0, s(m), s(T // 2), s(T - m)], fill=255)
        if right: d.rectangle([s(T // 2), s(m), size, s(T - m)], fill=255)
        tile = Image.new("RGBA", (size, size), (0, 0, 0, 0))
        k = int(4.5 * SS) * 2 + 1
        tile.paste(Image.new("RGBA", (size, size), INK + (255,)), (0, 0), mk.filter(ImageFilter.MaxFilter(k)))
        tile.paste(Image.new("RGBA", (size, size), SLATE + (255,)), (0, 0), mk)
        # 南侧立面（没连下面时露出来）、右下暗边、左上亮边
        face = ImageChops.subtract(mk, ImageChops.offset(mk, 0, -int(26 * SS)))
        tile.paste(Image.new("RGBA", (size, size), FACE + (255,)), (0, 0), face)
        top_edge = ImageChops.subtract(ImageChops.subtract(mk, ImageChops.offset(mk, 0, int(4 * SS))), face)
        tile.paste(Image.new("RGBA", (size, size), SLATE_L + (255,)), (0, 0), top_edge)
        dark = ImageChops.subtract(ImageChops.subtract(mk, ImageChops.offset(mk, -int(4 * SS), 0)), face)
        tile.paste(Image.new("RGBA", (size, size), SLATE_D + (255,)), (0, 0), dark)
        seam = ImageChops.subtract(face, ImageChops.offset(face, 0, int(3 * SS)))
        tile.paste(Image.new("RGBA", (size, size), INK + (255,)), (0, 0), ImageChops.multiply(seam, mk))
        # 几粒暗淡的符文光点
        dd = ImageDraw.Draw(tile)
        for _ in range(3):
            x, y = rnd.uniform(m + 14, T - m - 14), rnd.uniform(m + 14, T - m - 34)
            r = rnd.uniform(1.5, 2.5) * SS
            dd.ellipse([s(x) - r, s(y) - r, s(x) + r, s(y) + r], fill=RUNE_DIM + (255,))
        tile = tile.crop((o, o, o + T * SS, o + T * SS)).resize((T, T), Image.LANCZOS)
        col, row = i % 4, 3 - i // 4
        atlas.paste(tile, (col * T, row * T))
    os.makedirs(OUT, exist_ok=True)
    atlas.save(os.path.join(OUT, "Wall_Atlas.png"))
    return atlas


def main():
    imgs = [gate(True), gate(False), exit_portal(), fountain(), lamp(), shard(), wall_atlas()]
    W = sum(i.width for i in imgs) + 20 * (len(imgs) + 1)
    H = max(i.height for i in imgs) + 40
    pv = Image.new("RGBA", (W, H), (150, 140, 120, 255))
    x = 20
    for i in imgs:
        pv.alpha_composite(i, (x, 20))
        x += i.width + 20
    os.makedirs(os.path.dirname(PREVIEW), exist_ok=True)
    pv.save(PREVIEW)
    print("wrote", OUT, PREVIEW)


if __name__ == "__main__":
    main()
