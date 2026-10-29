# 1.17.1 常见种族与夺心魔贴图：程序绘制的 RimWorld 风格（粗黑轮廓、平涂、少量暗面）。
# 用法：python -I ArtSource/RaceArt/draw_race_art.py <模组根目录>
import math, os, sys
from PIL import Image, ImageDraw, ImageFilter

ROOT = sys.argv[1] if len(sys.argv) > 1 else os.getcwd()
TEX = os.path.join(ROOT, "Textures")
WORK = 1024
INK = (34, 30, 30)
OW = 2.6  # 轮廓宽度（画布 100 单位）

# 调色
BONE = (232, 222, 196); ORC = (124, 146, 90); ORC_D = (98, 118, 70)
ELF = (230, 204, 172); ELF_D = (204, 176, 146); GOLD = (214, 184, 118); GOLD_D = (178, 148, 88)
DWARF = (214, 158, 122); DWARF_D = (186, 128, 96); BEARD = (136, 88, 52); BEARD_D = (104, 66, 40)
FLAY = (152, 108, 162); FLAY_D = (116, 80, 128); FLAY_L = (186, 150, 196)
BRAIN = (206, 150, 182); BRAIN_D = (172, 114, 150); PSY = (188, 160, 228); PSY_L = (228, 214, 248)
STONE = (142, 136, 126); STONE_D = (112, 106, 98); WOOD = (140, 98, 60); WOOD_D = (108, 74, 44)
STEEL = (156, 158, 154); STEEL_D = (120, 122, 120); RED = (166, 62, 56); RED_D = (128, 44, 42)
GREY = (122, 128, 132); GLASS = (196, 206, 212); EYE = (240, 236, 244); LEAF = (112, 150, 88); LEAF_D = (86, 120, 66)


def taper(pts, w0, w1=None):
    """沿折线生成两端宽度不同的多边形（触须、尾巴、刀柄）。"""
    if w1 is None:
        w1 = w0
    # 先平滑：在折线上做 Catmull-Rom 插值
    sm = []
    p = [pts[0]] + list(pts) + [pts[-1]]
    for i in range(1, len(p) - 2):
        for t in [j / 8 for j in range(8)]:
            t2, t3 = t * t, t * t * t
            sm.append(tuple(0.5 * ((2 * p[i][k]) + (-p[i - 1][k] + p[i + 1][k]) * t + (2 * p[i - 1][k] - 5 * p[i][k] + 4 * p[i + 1][k] - p[i + 2][k]) * t2 + (-p[i - 1][k] + 3 * p[i][k] - 3 * p[i + 1][k] + p[i + 2][k]) * t3) for k in (0, 1)))
    sm.append(pts[-1])
    n = len(sm)
    left, right = [], []
    for i, (x, y) in enumerate(sm):
        a = sm[max(i - 1, 0)]; b = sm[min(i + 1, n - 1)]
        dx, dy = b[0] - a[0], b[1] - a[1]
        L = math.hypot(dx, dy) or 1
        nx, ny = -dy / L, dx / L
        w = (w0 + (w1 - w0) * i / (n - 1)) / 2
        left.append((x + nx * w, y + ny * w)); right.append((x - nx * w, y - ny * w))
    return left + right[::-1]


def arc(cx, cy, r, a0, a1, steps=24):
    return [(cx + r * math.cos(math.radians(a0 + (a1 - a0) * i / steps)), cy + r * math.sin(math.radians(a0 + (a1 - a0) * i / steps))) for i in range(steps + 1)]


def ellpts(cx, cy, rx, ry, ang=0, steps=48):
    c, s = math.cos(math.radians(ang)), math.sin(math.radians(ang))
    out = []
    for i in range(steps):
        t = 2 * math.pi * i / steps
        x, y = rx * math.cos(t), ry * math.sin(t)
        out.append((cx + x * c - y * s, cy + x * s + y * c))
    return out


def mir(pts):
    return [(100 - x, y) for x, y in pts]


class Pic:
    def __init__(self):
        self.img = Image.new("RGBA", (WORK, WORK), (0, 0, 0, 0))
        self.k = WORK / 100

    def P(self, pts):
        return [(x * self.k, y * self.k) for x, y in pts]

    def B(self, box):
        return [v * self.k for v in box]

    def mask(self, parts):
        m = Image.new("L", (WORK, WORK), 0)
        d = ImageDraw.Draw(m)
        for part in parts:
            kind, a = part[0], part[1]
            val = part[3] if len(part) > 3 else (0 if kind.startswith("-") else 255)
            kind = kind.lstrip("-")
            if kind == "poly":
                d.polygon(self.P(a), fill=val)
            elif kind == "ell":
                d.ellipse(self.B(a), fill=val)
            elif kind == "rect":
                d.rectangle(self.B(a), fill=val)
            elif kind == "line":
                w = part[2] * self.k
                d.line(self.P(a), fill=val, width=int(w), joint="curve")
                for x, y in (a[0], a[-1]):
                    d.ellipse([(x * self.k - w / 2), (y * self.k - w / 2), (x * self.k + w / 2), (y * self.k + w / 2)], fill=val)
        return m

    def add(self, parts, color, ow=OW):
        if parts and isinstance(parts[0], str):
            parts = [parts]
        m = self.mask(parts)
        if ow:
            o = m
            r = ow * self.k
            # 近似圆形膨胀：模糊后低阈值
            o = m.filter(ImageFilter.GaussianBlur(r / 2)).point(lambda v: 255 if v > 6 else 0)
            o = o.filter(ImageFilter.MaxFilter(5))
            self.img.paste(Image.new("RGBA", self.img.size, INK + (255,)), (0, 0), o)
        self.img.paste(Image.new("RGBA", self.img.size, color + (255,)), (0, 0), m)

    def line(self, pts, w, color=INK):
        self.add(("line", pts, w), color, 0)

    def save(self, rel, size):
        path = os.path.join(TEX, rel.replace("/", os.sep) + ".png")
        os.makedirs(os.path.dirname(path), exist_ok=True)
        self.img.resize((size, size), Image.LANCZOS).save(path)
        return path


def poly(p):
    return ("poly", p)


def ell(b):
    return ("ell", b)


def tent(pts, w0, w1):
    return ("poly", taper(pts, w0, w1))


def brain(pic, box, color=BRAIN, dark=BRAIN_D, ow=OW):
    x0, y0, x1, y1 = box
    w, h = x1 - x0, y1 - y0
    pic.add([ell((x0, y0 + h * .12, x0 + w * .56, y1)), ell((x0 + w * .44, y0 + h * .12, x1, y1)), ell((x0 + w * .12, y0, x1 - w * .12, y0 + h * .8))], color, ow)
    pic.add(poly([(x0 + w * .58, y0 + h * .1), (x1 - w * .1, y0 + h * .2), (x1, y0 + h * .55), (x1 - w * .08, y1 - h * .12), (x0 + w * .6, y1 - h * .02)]), dark, 0)
    lw = max(w / 26, 1.3)
    pic.line([(x0 + w * .5, y0 + h * .06), (x0 + w * .48, y0 + h * .4), (x0 + w * .52, y0 + h * .7), (x0 + w * .5, y1 - h * .04)], lw)
    for sx in (1, -1):
        cx = x0 + w * .5
        f = lambda u, v: (cx + sx * w * u, y0 + h * v)
        pic.line([f(.12, .2), f(.26, .26), f(.3, .4)], lw)
        pic.line([f(.1, .55), f(.24, .5), f(.38, .58)], lw)
        pic.line([f(.18, .76), f(.3, .7)], lw)


# ---------------- 异种型 ----------------
def halforc():
    p = Pic()
    ear = [(13, 34), (31, 44), (31, 60), (22, 58)]
    p.add([poly(ear), poly(mir(ear))], ORC)
    head = [(32, 22), (50, 16), (68, 22), (74, 40), (74, 62), (66, 80), (50, 87), (34, 80), (26, 62), (26, 40)]
    p.add(poly(head), ORC)
    p.add(poly([(64, 24), (68, 22), (74, 40), (74, 62), (66, 80), (56, 86), (66, 64), (69, 42)]), ORC_D, 0)
    p.add(poly([(30, 30), (33, 21), (50, 14), (67, 21), (70, 30), (50, 25)]), (52, 46, 40), 0)
    for s in (lambda q: q, mir):
        p.line(s([(35, 43), (46, 47)]), 3.4)
        p.line(s([(38, 51), (45, 52)]), 2.4)
        p.add(poly(s([(39, 71), (42, 61), (46, 71)])), BONE, 1.6)
    p.line([(46, 58), (50, 62), (54, 58)], 2)
    p.line([(37, 71), (63, 71)], 2.6)
    return p


def elf():
    p = Pic()
    p.add(poly([(28, 30), (50, 13), (72, 30), (78, 86), (63, 82), (50, 44), (37, 82), (22, 86)]), GOLD)
    ear = [(32, 45), (8, 26), (21, 50), (32, 61)]
    p.add([poly(ear), poly(mir(ear))], ELF)
    face = [(34, 30), (50, 24), (66, 30), (68, 52), (60, 72), (50, 79), (40, 72), (32, 52)]
    p.add(poly(face), ELF)
    p.add(poly([(60, 30), (66, 30), (68, 52), (60, 72), (52, 78), (60, 60), (63, 44)]), ELF_D, 0)
    p.add(poly([(31, 34), (50, 19), (69, 34), (67, 44), (57, 32), (50, 39), (43, 32), (33, 45)]), GOLD)
    p.add(poly([(56, 22), (69, 34), (67, 44), (60, 34)]), GOLD_D, 0)
    for s in (lambda q: q, mir):
        p.line(s([(38, 49), (46, 48)]), 2.2)
    p.add([ell((40.5, 49, 44.5, 53)), ell((55.5, 49, 59.5, 53))], (92, 128, 80), 0)
    p.line([(50, 53), (50, 61), (48, 62)], 1.6)
    p.line([(46, 68), (54, 68)], 1.8)
    return p


def dwarf():
    p = Pic()
    p.add([ell((23, 40, 33, 54)), ell((67, 40, 77, 54))], DWARF)
    p.add(ell((29, 22, 71, 66)), DWARF)
    p.add(poly([(28, 40), (30, 26), (50, 17), (70, 26), (72, 40), (63, 31), (37, 31)]), BEARD)
    p.add(poly([(27, 49), (38, 56), (50, 54), (62, 56), (73, 49), (75, 67), (67, 83), (56, 91), (50, 86), (44, 91), (33, 83), (25, 67)]), BEARD)
    p.add(poly([(62, 56), (73, 49), (75, 67), (67, 83), (56, 91), (62, 72)]), BEARD_D, 0)
    for x in (40, 47, 54, 61):
        p.line([(x, 70), (x + 1, 82)], 1.4, BEARD_D)
    p.add(poly([(37, 59), (50, 54), (63, 59), (58, 64), (50, 60), (42, 64)]), BEARD_D, 1.6)
    p.line([(35, 39), (46, 42)], 3.8)
    p.line([(54, 42), (65, 39)], 3.8)
    p.add([ell((40, 44, 44, 48)), ell((56, 44, 60, 48))], INK, 0)
    p.add(ell((44.5, 45, 55.5, 57)), (200, 128, 104), 1.8)
    return p


def mindflayer():
    p = Pic()
    for t in ([(41, 60), (36, 72), (39, 82), (34, 93)], [(47, 63), (47, 76), (44, 89)]):
        p.add([tent(t, 7.5, 2.4), tent(mir(t), 7.5, 2.4)], FLAY)
    p.add([ell((25, 9, 75, 58)), poly([(31, 42), (69, 42), (63, 66), (50, 71), (37, 66)])], FLAY)
    p.add(poly([(60, 11), (73, 24), (75, 38), (69, 46), (63, 66), (54, 70), (62, 48), (66, 30)]), FLAY_D, 0)
    for q in ([(37, 20), (42, 26), (39, 33)], [(50, 13), (49, 22), (52, 30)], [(62, 21), (58, 27), (61, 34)]):
        p.line(q, 1.7, FLAY_D)
    p.add([poly([(35, 46), (45, 47), (43, 52), (36, 51)]), poly(mir([(35, 46), (45, 47), (43, 52), (36, 51)]))], EYE, 1.8)
    return p


# ---------------- 基因 ----------------
def relentless():
    p = Pic()
    p.add([ell((22, 20, 52, 50)), ell((48, 20, 78, 50)), poly([(23.5, 40), (50, 82), (76.5, 40)])], RED)
    p.add(poly([(62, 22), (78, 32), (76, 46), (50, 82), (66, 46)]), RED_D, 0)
    p.add(ell((29, 27, 37, 35)), (206, 112, 100), 0)
    p.line([(51, 28), (45, 41), (55, 51), (47, 64)], 2.6)
    p.add(poly([(32, 58), (64, 34), (69, 41), (37, 65)]), BONE, 1.8)
    p.line([(46, 49), (50, 55)], 1.4)
    p.line([(53, 44), (57, 50)], 1.4)
    return p


def savage():
    p = Pic()
    for i, (a, b) in enumerate((((10, 26), (30, 52)), ((18, 18), (40, 46)), ((28, 12), (46, 36)))):
        p.add(tent([a, b], 2, 6), RED, 1.6)
    p.add(tent([(30, 90), (48, 60), (66, 26)], 6.5, 5), WOOD)
    p.add(poly([(56, 14), (78, 18), (88, 40), (80, 52), (70, 42), (60, 30)]), STEEL)
    p.add(poly([(78, 18), (88, 40), (80, 52), (82, 38)]), STEEL_D, 0)
    p.add(poly([(80, 52), (84, 47), (86, 58), (82, 60)]), RED, 1.4)
    return p


def trance():
    p = Pic()
    moon = [ell((38, 10, 66, 38)), ("-ell", (45, 6, 73, 34))]
    p.add(moon, (226, 220, 170))
    for cx, cy, r in ((24, 24, 5), (76, 32, 4)):
        p.add(poly([(cx, cy - r), (cx + r * .3, cy - r * .3), (cx + r, cy), (cx + r * .3, cy + r * .3), (cx, cy + r), (cx - r * .3, cy + r * .3), (cx - r, cy), (cx - r * .3, cy - r * .3)]), PSY_L, 1.4)
    p.add(("line", arc(50, 30, 34, 50, 130), 5.5), ELF, 2)
    p.add(("line", [(22, 50), (34, 46), (48, 45)], 3.5), GOLD_D, 1.6)
    p.add(("line", [(52, 45), (66, 46), (78, 50)], 3.5), GOLD_D, 1.6)
    lash = arc(50, 30, 34, 50, 130, 6)
    for (x, y) in lash[1:-1]:
        dx, dy = x - 50, y - 30
        L = math.hypot(dx, dy)
        p.line([(x, y), (x + dx / L * 9, y + dy / L * 9)], 2.2)
    return p


def bloodline():
    p = Pic()
    p.add(("line", arc(50, 50, 38, 200, 340), 4.5), PSY, 1.8)
    p.add(("line", arc(50, 50, 38, 20, 160), 4.5), PSY, 1.8)
    leaf = ellpts(50, 50, 15, 30, 30)
    p.add(poly(leaf), LEAF)
    p.add(poly(ellpts(55, 52, 8, 26, 30)), LEAF_D, 0)
    p.line([(38, 76), (50, 50), (63, 24)], 2)
    for a, b in (((47, 56), (40, 50)), ((53, 44), (61, 46)), ((44, 63), (37, 59))):
        p.line([a, b], 1.5)
    p.add(tent([(36, 80), (38, 76), (34, 90)], 3, 2), LEAF_D, 1.4)
    return p


def longevity():
    p = Pic()
    p.add(poly([(32, 21), (68, 21), (54, 50), (68, 79), (32, 79), (46, 50)]), GLASS)
    p.add(poly([(39, 30), (61, 30), (52, 46), (48, 46)]), GOLD, 0)
    p.add(poly([(35, 78), (65, 78), (56, 66), (50, 60), (44, 66)]), GOLD, 0)
    p.line([(50, 46), (50, 66)], 1.2, GOLD_D)
    p.line([(36, 26), (42, 36)], 1.6, (240, 244, 246))
    p.add([("line", [(29, 20), (29, 80)], 4), ("line", [(71, 20), (71, 80)], 4)], WOOD)
    p.add([("rect", (24, 13, 76, 21)), ("rect", (24, 79, 76, 87))], WOOD)
    p.add([("rect", (60, 13, 76, 21)), ("rect", (60, 79, 76, 87))], WOOD_D, 0)
    p.add(poly(ellpts(80, 10, 5, 10, 40)), LEAF, 1.6)
    return p


def stature():
    p = Pic()
    p.add([ell((60, 10, 76, 26)), poly([(57, 29), (79, 29), (82, 58), (77, 90), (59, 90), (54, 58)])], (150, 154, 156))
    p.add(("line", [(67, 32), (67, 88)], 1.2), (124, 128, 130), 0)
    p.add(poly([(13, 60), (49, 60), (52, 90), (10, 90)]), (88, 104, 122))
    p.add(poly([(34, 60), (49, 60), (52, 90), (36, 90)]), (70, 84, 100), 0)
    p.add(poly([(12, 74), (51, 74), (51.5, 79), (11.5, 79)]), WOOD_D, 0)
    p.add(ell((21, 37, 41, 57)), DWARF)
    p.add(poly([(19, 50), (43, 50), (41, 64), (31, 72), (21, 64)]), BEARD)
    p.add(poly([(20, 44), (22, 36), (31, 32), (40, 36), (42, 44), (31, 40)]), STEEL, 1.6)
    p.line([(28, 22), (28, 30)], 1.6, (120, 124, 126))
    p.line([(24, 26), (28, 22), (32, 26)], 1.6, (120, 124, 126))
    return p


def liver():
    p = Pic()
    p.add(("line", [(64, 40), (78, 42), (81, 56), (77, 68), (64, 72)], 6), STEEL)
    p.add(("rect", (24, 32, 66, 86)), WOOD)
    p.add(("rect", (52, 32, 66, 86)), WOOD_D, 0)
    for y in (40, 76):
        p.add(("rect", (23, y, 67, y + 5)), STEEL, 1.4)
    for x in (34, 44):
        p.line([(x, 47), (x, 74)], 1.2, WOOD_D)
    p.add([ell((19, 20, 37, 38)), ell((32, 15, 52, 35)), ell((47, 18, 66, 37)), ell((58, 25, 71, 38)), ("rect", (22, 30, 68, 38)), ell((25, 34, 33, 48))], (242, 236, 220))
    p.add([ell((52, 22, 64, 34))], (220, 212, 192), 0)
    return p


def stone():
    p = Pic()
    p.add(poly([(8, 86), (40, 24), (52, 40), (62, 30), (92, 86)]), STONE)
    p.add(poly([(62, 30), (92, 86), (66, 86), (60, 56)]), STONE_D, 0)
    p.add(poly([(40, 24), (47, 36), (42, 34), (38, 40), (34, 36)]), (232, 232, 228), 0)
    p.line([(26, 70), (34, 62)], 1.5, STONE_D)
    p.add(tent([(26, 94), (50, 62), (72, 34)], 5, 4.5), WOOD)
    p.add(tent([(54, 20), (70, 30), (78, 40), (88, 58)], 3, 2.5), STEEL)
    p.add(poly([(64, 26), (74, 32), (70, 40), (62, 34)]), STEEL_D, 1.4)
    return p


def cerebrum():
    p = Pic()
    brain(p, (20, 16, 80, 66))
    p.add([ell((40, 66, 60, 88)), poly([(41.5, 74), (50, 56), (58.5, 74)])], (130, 80, 160))
    p.add(ell((45, 72, 50, 78)), (176, 136, 200), 0)
    return p


def sterile():
    p = Pic()
    p.add(ell((28, 14, 72, 86)), (206, 196, 210))
    p.add(poly(ellpts(56, 52, 13, 33, 8)), (176, 164, 184), 0)
    p.line([(30, 46), (40, 50), (36, 56), (48, 60), (44, 66), (56, 70), (70, 60)], 2.6)
    p.add(tent([(80, 18), (66, 30), (62, 38)], 1, 2), FLAY, 0)
    p.add(("line", [(18, 86), (84, 14)], 5), RED, 1.6)
    return p


def psionic():
    p = Pic()
    for r in (30, 42):
        p.add(("line", arc(50, 54, r, 215, 325), 4), PSY, 1.6)
    p.add(poly([(14, 56), (32, 43), (50, 39), (68, 43), (86, 56), (68, 69), (50, 73), (32, 69)]), EYE)
    p.add(ell((39, 45, 61, 67)), (140, 90, 172), 1.6)
    p.add(ell((46, 52, 54, 60)), INK, 0)
    p.add(ell((50, 48, 55, 53)), (236, 228, 246), 0)
    return p


def hivelink():
    p = Pic()
    nodes = [(22, 26), (78, 26), (50, 86)]
    for n in nodes:
        p.add(("line", [(50, 52), n], 3.6), PSY, 1.4)
    for x, y in nodes:
        p.add([ell((x - 9, y - 9, x + 9, y + 7)), poly([(x - 4, y + 4), (x + 4, y + 4), (x + 3, y + 10), (x - 3, y + 10)])], FLAY)
        p.add([ell((x - 5, y - 2, x - 1, y + 2)), ell((x + 1, y - 2, x + 5, y + 2))], EYE, 0)
    brain(p, (34, 38, 66, 64), ow=2.2)
    return p


def halfillithid():
    p = Pic()
    for t in ([(58, 68), (59, 80), (56, 92)], [(66, 64), (70, 76), (68, 88)]):
        p.add(tent(t, 6, 2), FLAY)
    p.add(ell((28, 14, 72, 76)), ELF)
    p.add([ell((28, 14, 72, 76)), ("-rect", (0, 0, 50, 100))], FLAY, 0)
    p.add(poly([(62, 18), (72, 40), (70, 60), (60, 74), (66, 50)]), FLAY_D, 0)
    p.line([(50, 14), (48, 28), (52, 40), (48, 54), (52, 66), (50, 76)], 2.2)
    p.line([(35, 40), (45, 42)], 2.6)
    p.add(ell((38, 45, 43, 50)), INK, 0)
    p.add(poly([(55, 44), (64, 44), (63, 50), (56, 50)]), EYE, 1.6)
    p.line([(38, 64), (47, 64)], 1.8)
    return p


# ---------------- 能力 ----------------
def devour():
    p = Pic()
    brain(p, (24, 26, 76, 70))
    for t in ([(8, 94), (18, 72), (24, 54), (36, 40), (44, 38)], [(92, 94), (82, 72), (76, 54), (64, 42), (56, 40)], [(50, 98), (48, 84), (52, 72), (48, 62)]):
        p.add(tent(t, 9, 2.6), FLAY)
    p.add([ell((60, 16, 66, 24)), ell((64, 10, 68, 15))], RED, 1.2)
    return p


def mindblast():
    p = Pic()
    for r, w in ((24, 4.5), (36, 5), (48, 5.5)):
        p.add(("line", arc(32, 50, r, -32, 32), w), PSY, 1.8)
    p.add(tent([(20, 58), (18, 72), (12, 84)], 5, 2), FLAY)
    p.add(tent([(26, 58), (28, 72), (24, 86)], 5, 2), FLAY)
    p.add([ell((6, 24, 38, 60))], FLAY)
    p.add(poly([(24, 38), (32, 39), (31, 44), (25, 43)]), EYE, 1.4)
    return p


def readmind():
    p = Pic()
    p.add([poly([(16, 96), (26, 74), (74, 74), (84, 96)])], GREY)
    p.add(ell((26, 12, 74, 70)), GREY)
    p.add(poly(ellpts(58, 42, 14, 27, 0)), (100, 106, 112), 0)
    for r in (16, 22):
        p.add(("line", arc(50, 36, r, 200, 340), 2.6), PSY_L, 1.2)
    p.add(poly([(34, 38), (42, 32), (50, 30), (58, 32), (66, 38), (58, 44), (50, 46), (42, 44)]), EYE, 1.8)
    p.add(ell((45, 33, 55, 43)), (140, 90, 172), 0)
    p.add(ell((48, 36, 52, 40)), INK, 0)
    return p


def dominate():
    p = Pic()
    tips = [(33, 40), (45, 44), (56, 44), (67, 38)]
    fig = [(50, 58), (36, 78), (64, 78), (44, 94), (56, 94)]
    for a, b in zip(tips, [(36, 78), (50, 58), (50, 58), (64, 78)]):
        p.line([a, b], 0.9, (60, 50, 66))
    p.add([ell((42, 54, 58, 70)), poly([(41, 70), (59, 70), (62, 94), (38, 94)]), ("line", [(41, 72), (36, 80)], 4), ("line", [(59, 72), (64, 80)], 4)], (170, 172, 168))
    p.add(poly([(51, 70), (59, 70), (62, 94), (51, 94)]), (140, 142, 140), 0)
    for x0, (x1, y1) in zip((34, 45, 56, 66), tips):
        p.add(tent([(x0, 20), ((x0 + x1) / 2 + (x1 - 50) * .2, (20 + y1) / 2 - 2), (x1, y1)], 6.5, 4.5), FLAY, 2.2)
    p.add(tent([(26, 20), (18, 28), (16, 38)], 6.5, 4.5), FLAY, 2.2)
    p.add(poly([(28, 6), (72, 6), (74, 22), (64, 26), (34, 26), (26, 20)]), FLAY, 2.2)
    p.add(poly([(56, 6), (72, 6), (74, 22), (64, 26), (58, 26)]), FLAY_D, 0)
    p.line([(40, 12), (60, 12)], 1.2, FLAY_D)
    return p


def ring(cx, cy, rx, ry, th, ang):
    return [poly(ellpts(cx, cy, rx, ry, ang)), ("-poly", ellpts(cx, cy, rx - th, ry - th, ang))]


def bind():
    p = Pic()
    p.add(("line", arc(50, 50, 40, 0, 359, 48), 3), PSY, 1.4)
    p.add(ring(37, 38, 20, 12, 6.5, 45), STEEL)
    p.add(ring(63, 62, 20, 12, 6.5, 45), (176, 156, 206))
    p.add(poly(ellpts(46, 44, 4, 10, 45)), STEEL, 1.6)
    for cx, cy in ((78, 22), (22, 80)):
        r = 6
        p.add(poly([(cx, cy - r), (cx + 2, cy - 2), (cx + r, cy), (cx + 2, cy + 2), (cx, cy + r), (cx - 2, cy + 2), (cx - r, cy), (cx - 2, cy - 2)]), PSY_L, 1.2)
    return p


def throw():
    p = Pic()
    for y, x0 in ((34, 10), (50, 4), (66, 12)):
        p.add(tent([(x0, y), (x0 + 30, y)], 1.5, 5), PSY, 1.4)
    p.add(poly([(44, 30), (66, 18), (88, 34), (90, 60), (72, 80), (48, 72), (40, 52)]), STONE)
    p.add(poly([(66, 18), (88, 34), (90, 60), (72, 80), (74, 52)]), STONE_D, 0)
    p.line([(56, 40), (64, 50), (60, 62)], 1.6, STONE_D)
    p.add(("line", arc(66, 50, 38, 130, 230), 3), PSY_L, 1.2)
    return p


def unseen():
    p = Pic()
    gaps = [("-rect", (0, y, 100, y + 3 + (y - 50) / 8)) for y in range(50, 96, 9)]
    body = [ell((39, 8, 61, 30)), poly([(32, 33), (68, 33), (72, 40), (80, 62), (73, 64), (66, 46), (64, 62), (66, 94), (55, 94), (50, 68), (45, 94), (34, 94), (36, 62), (34, 46), (27, 64), (20, 62), (28, 40)])]
    p.add(body + gaps, (196, 188, 216))
    p.add([poly([(52, 33), (68, 33), (72, 40), (80, 62), (73, 64), (66, 46), (64, 62), (66, 94), (55, 94), (52, 60)])] + gaps, (166, 156, 192), 0)
    for cx, cy in ((80, 20), (18, 30)):
        p.add(poly([(cx, cy - 6), (cx + 2, cy - 2), (cx + 6, cy), (cx + 2, cy + 2), (cx, cy + 6), (cx - 2, cy + 2), (cx - 6, cy), (cx - 2, cy - 2)]), PSY_L, 1.2)
    return p


def storm():
    p = Pic()
    for bolt in ([(30, 50), (24, 66), (32, 66), (22, 90), (40, 62), (32, 62), (38, 50)], [(58, 50), (50, 70), (60, 70), (54, 94), (72, 64), (62, 64), (68, 50)]):
        p.add(poly(bolt), PSY_L, 1.8)
    p.add([ell((10, 26, 40, 54)), ell((26, 12, 62, 46)), ell((52, 18, 88, 52)), ("rect", (22, 36, 78, 54))], (110, 80, 136))
    p.add([ell((58, 24, 84, 50)), ("rect", (54, 38, 78, 54))], (88, 62, 110), 0)
    brain(p, (36, 22, 60, 42), ow=1.6)
    return p


# ---------------- 物品与建筑 ----------------
def tadpole():
    p = Pic()
    p.add(tent([(54, 52), (42, 60), (34, 74), (22, 80), (12, 76)], 13, 2), FLAY)
    for t in ([(66, 34), (74, 24), (82, 22)], [(72, 40), (82, 36), (88, 40)], [(70, 46), (80, 50), (84, 58)]):
        p.add(tent(t, 4, 1.4), FLAY_D, 1.6)
    p.add(ell((44, 28, 74, 58)), FLAY)
    p.add(poly(ellpts(62, 46, 10, 7, -30)), FLAY_D, 0)
    p.add([ell((58, 34, 64, 40))], EYE, 1.2)
    p.add(("line", [(44, 66), (36, 76)], 1.2), FLAY_L, 0)
    return p


def brainjar():
    p = Pic()
    p.add([("rect", (24, 28, 76, 88)), ell((24, 80, 76, 94))], GLASS)
    p.add([("rect", (27, 44, 73, 88)), ell((27, 82, 73, 92))], (150, 112, 176), 0)
    brain(p, (31, 46, 69, 78), ow=1.8)
    p.line([(30, 34), (30, 80)], 2.4, (240, 244, 248))
    p.add(("rect", (20, 14, 80, 30)), STEEL)
    p.add(("rect", (60, 14, 80, 30)), STEEL_D, 0)
    p.add(("rect", (40, 6, 60, 14)), WOOD)
    return p


def suppressant():
    p = Pic()
    caps = [((16, 34), (44, 54)), ((54, 22), (84, 38)), ((30, 66), (64, 78))]
    for a, b in caps:
        mid = ((a[0] + b[0]) / 2, (a[1] + b[1]) / 2)
        p.add(("line", [a, b], 14), (184, 154, 222))
        p.add(("line", [mid, b], 14), (238, 234, 228), 0)
        p.line([(mid[0] - (b[1] - a[1]) * .25, mid[1] + (b[0] - a[0]) * .25), (mid[0] + (b[1] - a[1]) * .25, mid[1] - (b[0] - a[0]) * .25)], 1.4)
        p.line([(a[0] + (b[0] - a[0]) * .12, a[1] + (b[1] - a[1]) * .12 - 3), (a[0] + (b[0] - a[0]) * .35, a[1] + (b[1] - a[1]) * .35 - 3)], 1.8, (222, 206, 242))
    return p


def elderbrain():
    p = Pic()
    p.add(ell((4, 6, 96, 96)), STONE)
    p.add([ell((4, 6, 96, 96)), ("-ell", (4, 0, 92, 90))], STONE_D, 0)
    for a in range(0, 360, 30):
        x, y = 50 + 43 * math.cos(math.radians(a)), 51 + 43 * math.sin(math.radians(a))
        x2, y2 = 50 + 37 * math.cos(math.radians(a)), 51 + 37 * math.sin(math.radians(a))
        p.line([(x, y), (x2, y2)], 1, STONE_D)
    p.add(ell((13, 14, 87, 88)), (86, 58, 108))
    p.add(ell((16, 17, 84, 85)), (110, 76, 136), 0)
    for t in ([(20, 70), (14, 80), (20, 86)], [(80, 70), (88, 76), (82, 86)], [(26, 26), (18, 18)], [(74, 24), (82, 16)]):
        p.add(tent(t, 5, 1.6), FLAY, 1.6)
    brain(p, (22, 20, 78, 80))
    for x, y in ((18, 50), (84, 46), (50, 86), (36, 84), (66, 84)):
        p.add(ell((x - 2, y - 2, x + 2, y + 2)), PSY_L, 0)
    return p


def tentacles(facing):
    p = Pic()
    skin, dark = (236, 236, 236), (196, 196, 196)
    if facing == "south":
        roots = [((42, 61), (39, 70), (40, 77), (37, 83)), ((47.5, 63), (47, 72), (46, 81)), ((52.5, 63), (53, 72), (54, 81)), ((58, 61), (61, 70), (60, 77), (63, 83))]
        for r in roots:
            p.add(tent(list(r), 4.4, 1.6), skin, 1.6)
        p.add(tent([(47.5, 64), (47, 72), (46, 80)], 1.6, .6), dark, 0)
        p.add(tent([(58.5, 63), (61, 70), (60, 77)], 1.6, .6), dark, 0)
    else:
        roots = [((61, 58), (63, 67), (61, 74), (64, 81)), ((64, 59), (67, 67), (66, 74), (70, 80))]
        for r in roots:
            p.add(tent(list(r), 4.4, 1.6), skin, 1.6)
        p.add(tent([(64.5, 61), (67, 67), (66, 73)], 1.6, .6), dark, 0)
    return p


ICONS = {
    "CommonRaces/Xenotypes/HalfOrc": halforc, "CommonRaces/Xenotypes/Elf": elf, "CommonRaces/Xenotypes/Dwarf": dwarf,
    "CommonRaces/Genes/Relentless": relentless, "CommonRaces/Genes/Savage": savage,
    "CommonRaces/Genes/ElfTrance": trance, "CommonRaces/Genes/ElfBloodline": bloodline, "CommonRaces/Genes/ElfLongevity": longevity,
    "CommonRaces/Genes/DwarfStature": stature, "CommonRaces/Genes/DwarfLiver": liver, "CommonRaces/Genes/DwarfStoneEndurance": stone,
    "MindFlayer/Xenotype": mindflayer,
    "MindFlayer/Genes/Cerebrum": cerebrum, "MindFlayer/Genes/Sterile": sterile, "MindFlayer/Genes/PsionicSense": psionic,
    "MindFlayer/Genes/HiveLink": hivelink, "MindFlayer/Genes/HalfIllithid": halfillithid,
    "MindFlayer/Abilities/DevourBrain": devour, "MindFlayer/Abilities/MindBlast": mindblast, "MindFlayer/Abilities/ReadMind": readmind,
    "MindFlayer/Abilities/Dominate": dominate, "MindFlayer/Abilities/PsychicBind": bind, "MindFlayer/Abilities/TelekineticThrow": throw,
    "MindFlayer/Abilities/Unseen": unseen, "MindFlayer/Abilities/PsychicStorm": storm,
    "MindFlayer/Items/Tadpole": tadpole, "MindFlayer/Items/BrainJar": brainjar, "MindFlayer/Items/PsionicSuppressant": suppressant,
}

if __name__ == "__main__":
    out = []
    for rel, fn in ICONS.items():
        out.append(fn().save(rel, 256))
    out.append(elderbrain().save("MindFlayer/ElderBrain", 512))
    for f in ("south", "east"):
        out.append(tentacles(f).save("MindFlayer/Tentacles/FaceTentacles_" + f, 256))
    # 北向看不到触须，给一张空图满足 Graphic_Multi
    Image.new("RGBA", (256, 256), (0, 0, 0, 0)).save(os.path.join(TEX, "MindFlayer", "Tentacles", "FaceTentacles_north.png"))
    print("\n".join(out))
