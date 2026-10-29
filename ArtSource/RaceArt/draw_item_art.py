# 1.22.0 物品与炼金贴图：沿用 draw_race_art.py 的程序绘制流程（粗黑轮廓、平涂、单侧暗面）。
# 物品图 128×128；材料按原版 Graphic_StackCount 画三张（少量/中量/大量）；炼金桌按 Graphic_Multi 画北、南、东三向（西向由东向翻转）。
# 用法：python -I ArtSource/RaceArt/draw_item_art.py <模组根目录>
import math, os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import draw_race_art as base
from draw_race_art import Pic, poly, ell, tent, arc, ellpts, mir, INK
from PIL import Image

ROOT = sys.argv[1] if len(sys.argv) > 1 else os.getcwd()
base.TEX = os.path.join(ROOT, "Textures")

GLASS = (198, 214, 220); GLASS_D = (160, 178, 186); CORK = (150, 108, 70); CORK_D = (118, 82, 52)
WHITE = (244, 244, 240); GOLD = (222, 186, 92); GOLD_D = (178, 140, 62); SILVER = (206, 210, 214); SILVER_D = (156, 162, 168)
STEEL = (160, 164, 166); STEEL_D = (118, 122, 126); WOOD = (146, 102, 62); WOOD_D = (110, 74, 44)
LEATHER = (132, 92, 58); LEATHER_D = (98, 66, 40); PARCH = (234, 216, 170); PARCH_D = (204, 182, 130)
BONE = (230, 220, 194); BONE_D = (196, 184, 152)


def shade(c, f):
    return tuple(max(0, min(255, int(v * f))) for v in c)


def light(c, f=0.35):
    return tuple(int(v + (255 - v) * f) for v in c)


# ---------------- 药水 ----------------
def flask(liquid, shape="round", glow=None, extra=None):
    p = Pic()
    if shape == "round":
        body = ellpts(50, 64, 30, 28)
        p.add([poly(body), ("rect", (41, 18, 59, 42))], GLASS, 2.6)
        liq = [(x, y) for x, y in ellpts(50, 64, 28.5, 26.5) if y > 52] + [(78.5, 52), (21.5, 52)]
        liq = sorted([pt for pt in ellpts(50, 64, 28.5, 26.5) if pt[1] >= 52], key=lambda q: math.atan2(q[1] - 64, q[0] - 50))
        p.add(poly(liq), liquid, 0)
        p.add(poly([pt for pt in liq if pt[0] >= 50]), shade(liquid, .78), 0)
        p.line([(30, 50), (34, 44)], 2.2, WHITE)
    elif shape == "tall":
        p.add([("rect", (34, 30, 66, 90)), ("rect", (42, 14, 58, 32))], GLASS, 2.6)
        p.add(("rect", (35.5, 50, 64.5, 88.5)), liquid, 0)
        p.add(("rect", (51, 50, 64.5, 88.5)), shade(liquid, .78), 0)
        p.line([(39, 38), (39, 46)], 2, WHITE)
    elif shape == "square":
        p.add([poly([(26, 40), (74, 40), (78, 50), (78, 90), (22, 90), (22, 50)]), ("rect", (42, 18, 58, 42))], GLASS, 2.6)
        p.add(poly([(23.5, 56), (76.5, 56), (76.5, 88.5), (23.5, 88.5)]), liquid, 0)
        p.add(poly([(52, 56), (76.5, 56), (76.5, 88.5), (52, 88.5)]), shade(liquid, .78), 0)
        p.line([(28, 46), (28, 52)], 2.2, WHITE)
    elif shape == "cone":
        p.add([poly([(42, 34), (58, 34), (80, 88), (20, 88)]), ("rect", (42, 16, 58, 36))], GLASS, 2.6)
        p.add(poly([(33, 58), (67, 58), (78, 86.5), (22, 86.5)]), liquid, 0)
        p.add(poly([(50, 58), (67, 58), (78, 86.5), (50, 86.5)]), shade(liquid, .78), 0)
        p.line([(40, 44), (37, 52)], 2, WHITE)
    p.add(("rect", (39, 8, 61, 22)), CORK, 2.4)
    p.add(("rect", (51, 8, 61, 22)), CORK_D, 0)
    if glow:
        for x, y, r in ((36, 72, 3.4), (46, 80, 2.2), (60, 68, 2.6)):
            p.add(ell((x - r, y - r, x + r, y + r)), glow, 0)
    if extra:
        extra(p)
    return p


def p_healing():
    return flask((196, 48, 52), "round", (236, 112, 110))


def p_greater():
    def heart(p):
        p.add(poly([(50, 82), (38, 70), (38, 63), (43, 59), (50, 64), (57, 59), (62, 63), (62, 70)]), (250, 214, 214), 1.6)
    return flask((140, 22, 40), "square", None, heart)


def p_giant():
    def flame(p):
        p.add(poly([(50, 62), (55, 70), (58, 66), (60, 76), (54, 84), (46, 84), (40, 76), (42, 66), (45, 70)]), (255, 214, 120), 1.4)
    return flask((232, 112, 34), "round", None, flame)


def p_fire():
    def snow(p):
        for a in range(0, 180, 60):
            r = math.radians(a)
            p.line([(50 - 9 * math.cos(r), 72 - 9 * math.sin(r)), (50 + 9 * math.cos(r), 72 + 9 * math.sin(r))], 2.4, (236, 246, 255))
    return flask((214, 156, 44), "tall", None, snow)


def p_invis():
    p = flask((214, 230, 236), "cone", (250, 252, 255))
    return p


def p_heroism():
    def star(p):
        pts = []
        for i in range(10):
            r = 11 if i % 2 == 0 else 4.6
            a = math.radians(-90 + i * 36)
            pts.append((50 + r * math.cos(a), 72 + r * math.sin(a)))
        p.add(poly(pts), (255, 246, 196), 1.4)
    return flask((236, 196, 52), "square", None, star)


def p_mind():
    def swirl(p):
        p.add(tent(arc(50, 72, 9, 0, 300, 18), 3, 1.2), (240, 226, 255), 0)
    return flask((152, 112, 204), "round", None, swirl)


def p_troll():
    return flask((78, 128, 62), "tall", (126, 170, 96))


# ---------------- 投掷物 ----------------
def t_alchemist_fire(small=False):
    p = Pic()
    p.add([ell((24, 34, 76, 90)), ("rect", (42, 18, 58, 40))], (196, 120, 64), 2.8)
    p.add(poly([(50, 36), (76, 50), (76, 76), (60, 90), (50, 90)]), (162, 94, 48), 0)
    p.add(("rect", (40, 8, 60, 22)), (120, 84, 60), 2.4)
    p.add(poly([(50, 0), (58, 8), (54, 14), (46, 14), (42, 8)]), (255, 170, 60), 1.6)
    p.add(poly([(50, 46), (60, 60), (58, 72), (50, 80), (42, 72), (40, 60)]), (255, 196, 80), 2)
    p.add(poly([(50, 58), (54, 66), (50, 74), (46, 66)]), (255, 240, 170), 0)
    return p


def t_acid():
    p = Pic()
    p.add([ell((30, 44, 70, 90)), ("rect", (43, 20, 57, 48))], GLASS, 2.6)
    p.add(poly([pt for pt in ellpts(50, 67, 18.5, 21.5) if pt[1] >= 60]), (138, 214, 52), 0)
    p.add(("rect", (41, 10, 59, 22)), STEEL, 2.4)
    for x, y, r in ((44, 74, 2.8), (55, 80, 2), (52, 68, 1.6)):
        p.add(ell((x - r, y - r, x + r, y + r)), (206, 248, 140), 0)
    p.line([(36, 56), (38, 50)], 2, WHITE)
    return p


def t_holy():
    p = Pic()
    p.add([poly([(30, 40), (70, 40), (78, 58), (70, 90), (30, 90), (22, 58)]), ("rect", (42, 18, 58, 42))], SILVER, 2.8)
    p.add(poly([(50, 40), (70, 40), (78, 58), (70, 90), (50, 90)]), SILVER_D, 0)
    p.add(("rect", (40, 8, 60, 22)), GOLD, 2.4)
    p.add([("rect", (46.5, 50, 53.5, 82)), ("rect", (37, 58, 63, 65))], GOLD, 1.8)
    return p


def t_thunder():
    p = Pic()
    p.add(poly([(30, 22), (66, 16), (84, 44), (76, 80), (40, 88), (16, 60)]), (120, 126, 148), 3)
    p.add(poly([(50, 18), (66, 16), (84, 44), (76, 80), (50, 86)]), (94, 100, 122), 0)
    p.add(poly([(54, 26), (38, 54), (50, 54), (42, 78), (64, 46), (52, 46), (60, 26)]), (255, 230, 90), 1.8)
    return p


def t_tangle():
    p = Pic()
    sack = [(28, 36), (72, 36), (82, 66), (74, 88), (26, 88), (18, 66)]
    p.add(poly(sack), LEATHER, 2.8)
    p.add(poly([(50, 36), (72, 36), (82, 66), (74, 88), (50, 88)]), LEATHER_D, 0)
    p.add(poly([(34, 22), (66, 22), (62, 38), (38, 38)]), LEATHER, 2.4)
    p.line([(36, 38), (64, 38)], 3, (90, 60, 34))
    for x, y in ((36, 60), (60, 70), (48, 80)):
        p.add(tent([(x, y), (x + 2, y + 6), (x, y + 12)], 5, 2), (226, 214, 140), 1.2)
    return p


def t_dispel():
    p = Pic()
    p.add([poly([(30, 30), (70, 30), (80, 60), (70, 90), (30, 90), (20, 60)])], (214, 210, 232), 2.8)
    p.add(poly([(50, 30), (70, 30), (80, 60), (70, 90), (50, 90)]), (184, 180, 206), 0)
    p.add(poly([(34, 18), (66, 18), (62, 32), (38, 32)]), (180, 176, 204), 2.4)
    for i, (x, y) in enumerate(((40, 52), (58, 58), (46, 70), (62, 76), (34, 78), (54, 44))):
        r = 2.6 if i % 2 else 3.6
        p.add(poly([(x, y - r), (x + r, y), (x, y + r), (x - r, y)]), (250, 250, 255), 1)
    return p


# ---------------- 材料（三档堆叠） ----------------
def stack(draw_one, n):
    p = Pic()
    spots = {1: [(50, 58, 1.0)], 2: [(38, 62, .9), (62, 56, .9)], 3: [(34, 66, .82), (62, 66, .82), (48, 44, .82)]}[n]
    for x, y, s in spots:
        draw_one(p, x, y, s)
    return p


def lava_one(p, x, y, s):
    pts = [(x + dx * s, y + dy * s) for dx, dy in ((-14, 10), (-18, -4), (-6, -22), (10, -18), (18, -2), (12, 14))]
    p.add(poly(pts), (176, 48, 30), 2.6)
    p.add(poly([(x, y - 20 * s), (x + 10 * s, y - 18 * s), (x + 18 * s, y - 2 * s), (x + 12 * s, y + 14 * s), (x, y + 6 * s)]), (126, 30, 22), 0)
    p.add(poly([(x - 6 * s, y - 10 * s), (x + 2 * s, y - 14 * s), (x + 4 * s, y - 2 * s), (x - 4 * s, y + 2 * s)]), (255, 156, 60), 0)


def shed_one(p, x, y, s):
    pts = [(x + dx * s, y + dy * s) for dx, dy in ((-22, 8), (-16, -10), (0, -18), (18, -10), (22, 8), (0, 16))]
    p.add(poly(pts), (148, 186, 222), 2.6)
    for i in range(3):
        for j in range(3):
            cx, cy = x + (-12 + 12 * i) * s, y + (-8 + 8 * j) * s
            p.add(poly([(cx - 5 * s, cy), (cx, cy - 4 * s), (cx + 5 * s, cy), (cx, cy + 4 * s)]), (112, 150, 196), 0)


def hide_one(p, x, y, s):
    pts = [(x + dx * s, y + dy * s) for dx, dy in ((-24, -6), (-14, -16), (8, -14), (22, -18), (24, 0), (16, 14), (-2, 10), (-20, 14))]
    p.add(poly(pts), (106, 136, 78), 2.6)
    p.add(poly([(x + 4 * s, y - 14 * s), (x + 22 * s, y - 18 * s), (x + 24 * s, y), (x + 16 * s, y + 14 * s), (x, y + 8 * s)]), (82, 108, 58), 0)
    for dx, dy in ((-10, -4), (2, 2), (12, -6)):
        p.add(ell((x + (dx - 2) * s, y + (dy - 2) * s, x + (dx + 2) * s, y + (dy + 2) * s)), (70, 92, 50), 0)


def ingot_one(color, dark, shine):
    def f(p, x, y, s):
        pts = [(x - 20 * s, y + 8 * s), (x - 14 * s, y - 8 * s), (x + 14 * s, y - 8 * s), (x + 20 * s, y + 8 * s)]
        p.add(poly(pts), color, 2.6)
        p.add(poly([(x - 20 * s, y + 8 * s), (x + 20 * s, y + 8 * s), (x + 18 * s, y + 14 * s), (x - 18 * s, y + 14 * s)]), dark, 2)
        p.line([(x - 10 * s, y - 4 * s), (x + 6 * s, y - 4 * s)], 1.8 * s, shine)
    return f


# ---------------- 卷轴 ----------------
def scroll(seal, rune):
    p = Pic()
    p.add(poly([(24, 24), (76, 24), (76, 80), (24, 80)]), PARCH, 2.6)
    p.add(poly([(52, 24), (76, 24), (76, 80), (52, 80)]), PARCH_D, 0)
    for y in (24, 80):
        p.add(("rect", (16, y - 7, 84, y + 7)), PARCH, 2.6)
        p.add(ell((10, y - 7, 22, y + 7)), WOOD, 2.2)
        p.add(ell((78, y - 7, 90, y + 7)), WOOD, 2.2)
    for y in (38, 46, 54):
        p.line([(32, y), (56, y)], 1.4, (150, 126, 90))
    p.add(ell((52, 50, 76, 74)), seal, 2.4)
    p.add(ell((56, 54, 72, 70)), shade(seal, .8), 0)
    rune(p, 64, 62)
    return p


def r_teleport(p, x, y):
    p.add(tent(arc(x, y, 5, 0, 330, 12), 2, 1), (255, 240, 200), 0)


def r_revive(p, x, y):
    p.add([("rect", (x - 1.6, y - 6, x + 1.6, y + 6)), ("rect", (x - 6, y - 1.6, x + 6, y + 1.6))], (255, 246, 214), 0)


def r_ward(p, x, y):
    p.add(poly([(x - 6, y - 6), (x + 6, y - 6), (x + 6, y), (x, y + 7), (x - 6, y)]), (255, 246, 214), 0)


def r_fire(p, x, y):
    p.add(poly([(x, y - 7), (x + 5, y), (x + 3, y + 6), (x - 3, y + 6), (x - 5, y)]), (255, 214, 120), 0)


def r_eye(p, x, y):
    p.add(ell((x - 7, y - 4, x + 7, y + 4)), (255, 250, 230), 0)
    p.add(ell((x - 2.5, y - 2.5, x + 2.5, y + 2.5)), INK, 0)


def r_stone(p, x, y):
    p.add(poly([(x - 6, y + 5), (x - 4, y - 5), (x + 4, y - 6), (x + 6, y + 4)]), (200, 196, 186), 0)


def r_skull(p, x, y):
    p.add(ell((x - 6, y - 7, x + 6, y + 4)), BONE, 0)
    p.add([ell((x - 4, y - 3, x - 1, y)), ell((x + 1, y - 3, x + 4, y))], INK, 0)
    p.add(("rect", (x - 3, y + 3, x + 3, y + 6)), BONE, 0)


def r_burst(p, x, y):
    for a in range(0, 360, 45):
        r = math.radians(a)
        p.line([(x + 2 * math.cos(r), y + 2 * math.sin(r)), (x + 7 * math.cos(r), y + 7 * math.sin(r))], 1.6, (214, 255, 196))


# ---------------- 奇物 ----------------
def bag_of_holding():
    p = Pic()
    p.add(poly([(26, 40), (74, 40), (84, 70), (74, 90), (26, 90), (16, 70)]), (112, 62, 140), 2.8)
    p.add(poly([(50, 40), (74, 40), (84, 70), (74, 90), (50, 90)]), (86, 44, 110), 0)
    p.add(poly([(34, 26), (66, 26), (62, 42), (38, 42)]), (112, 62, 140), 2.4)
    p.line([(34, 42), (66, 42)], 3.4, GOLD)
    p.add(ell((40, 56, 60, 76)), (20, 14, 34), 2)
    for a in range(0, 360, 60):
        r = math.radians(a)
        p.add(ell((50 + 5 * math.cos(r) - 1.2, 66 + 5 * math.sin(r) - 1.2, 50 + 5 * math.cos(r) + 1.2, 66 + 5 * math.sin(r) + 1.2)), (214, 196, 255), 0)
    return p


def cloak(color, dark, clasp):
    p = Pic()
    body = [(36, 16), (64, 16), (72, 30), (86, 90), (60, 84), (50, 92), (40, 84), (14, 90), (28, 30)]
    p.add(poly(body), color, 2.8)
    p.add(poly([(50, 16), (64, 16), (72, 30), (86, 90), (60, 84), (50, 92)]), dark, 0)
    p.add(poly([(38, 16), (62, 16), (58, 26), (42, 26)]), shade(color, .7), 2)
    p.add(ell((44, 20, 56, 32)), clasp, 2)
    return p


def boots(color, dark, trim, wing=False):
    p = Pic()
    for dx in (0, 30):
        b = [(18 + dx, 20), (40 + dx, 20), (42 + dx, 62), (58 + dx, 74), (58 + dx, 86), (18 + dx, 86)]
        p.add(poly(b), color if dx else shade(color, .9), 2.6)
        p.add(("rect", (16 + dx, 18, 42 + dx, 28)), trim, 2.2)
        if wing:
            p.add(poly([(40 + dx, 32), (52 + dx, 22), (50 + dx, 34), (56 + dx, 30), (48 + dx, 42)]), WHITE, 1.6)
    return p


def headband():
    p = Pic()
    p.add(tent(arc(50, 60, 32, 200, 340, 24), 9, 9), SILVER, 2.6)
    p.add(tent(arc(50, 60, 32, 270, 340, 12), 9, 9), SILVER_D, 0)
    p.add(poly([(50, 18), (60, 30), (50, 42), (40, 30)]), (120, 196, 236), 2.2)
    p.add(poly([(50, 22), (55, 30), (50, 30)]), (226, 246, 255), 0)
    return p


def ring(gem, band=GOLD, band_d=GOLD_D):
    p = Pic()
    p.add([ell((22, 36, 78, 92)), ("-ell", (32, 46, 68, 82))], band, 3)
    p.add([ell((22, 36, 78, 92)), ("-ell", (32, 46, 68, 82)), ("-rect", (0, 0, 50, 100))], band_d, 0)
    p.add(poly([(36, 30), (50, 14), (64, 30), (50, 44)]), gem, 2.6)
    p.add(poly([(50, 14), (64, 30), (50, 44)]), shade(gem, .75), 0)
    p.add(poly([(44, 26), (50, 19), (53, 25)]), light(gem, .6), 0)
    return p


def amulet():
    p = Pic()
    p.add(tent(arc(50, 30, 26, 180, 360, 24), 3, 3), GOLD, 1.8)
    p.add(ell((32, 50, 68, 86)), GOLD, 2.8)
    p.add(ell((38, 56, 62, 80)), (210, 60, 70), 0)
    p.add([("rect", (47.5, 60, 52.5, 76)), ("rect", (42, 65.5, 58, 70.5))], WHITE, 0)
    return p


def ioun():
    p = Pic()
    pts = [(50, 14), (72, 40), (60, 86), (40, 86), (28, 40)]
    p.add(poly(pts), (186, 120, 236), 2.8)
    p.add(poly([(50, 14), (72, 40), (60, 86), (50, 86)]), (148, 90, 204), 0)
    p.add(poly([(42, 34), (50, 22), (54, 34), (48, 52)]), (236, 214, 255), 0)
    return p


def ioun_glow():
    p = Pic()
    for r, c in ((44, (186, 140, 255)), (30, (214, 190, 255)), (16, (250, 244, 255))):
        p.add(ell((50 - r, 50 - r, 50 + r, 50 + r)), c, 0)
    return p


def wand():
    p = Pic()
    p.add(tent([(22, 86), (50, 50), (72, 26)], 7, 5), WOOD, 2.6)
    p.add(tent([(48, 54), (72, 26)], 3, 2), WOOD_D, 0)
    p.add(poly([(72, 12), (84, 26), (72, 40), (60, 26)]), (120, 220, 200), 2.4)
    p.add(poly([(72, 12), (84, 26), (72, 26)]), (200, 255, 240), 0)
    for x, y in ((88, 14), (58, 10), (90, 36)):
        p.add(poly([(x, y - 3), (x + 2, y), (x, y + 3), (x - 2, y)]), (220, 255, 245), 0)
    return p


# ---------------- 种族装备 ----------------
def throwing_hammer():
    p = Pic()
    p.add(tent([(30, 90), (56, 40)], 7, 7), WOOD, 2.6)
    head = [(40, 18), (80, 38), (72, 54), (32, 34)]
    p.add(poly(head), STEEL, 3)
    p.add(poly([(56, 26), (80, 38), (72, 54), (56, 46)]), STEEL_D, 0)
    for t in (.3, .6):
        x, y = 40 + 40 * t, 18 + 20 * t
        p.line([(x - 2, y + 4), (x - 6, y + 12)], 1.6, (96, 196, 236))
    p.add(ell((26, 84, 36, 94)), STEEL, 2)
    return p


def rune_shield():
    p = Pic()
    p.add(ell((12, 12, 88, 88)), WOOD, 3)
    p.add(ell((20, 20, 80, 80)), STEEL, 2.4)
    p.add(poly([(50, 20), (80, 50), (50, 80)]), STEEL_D, 0)
    p.add(ell((40, 40, 60, 60)), STEEL, 2.4)
    for a in range(0, 360, 90):
        r = math.radians(a + 45)
        x, y = 50 + 20 * math.cos(r), 50 + 20 * math.sin(r)
        p.add(poly([(x, y - 4), (x + 3, y), (x, y + 4), (x - 3, y)]), (96, 196, 236), 1.2)
    return p


def longbow():
    p = Pic()
    p.add(tent(arc(110, 50, 70, 140, 220, 30), 5, 5), (214, 196, 150), 2.6)
    p.line([(56.4, 5), (56.4, 95)], 1.4, (240, 236, 220))
    for y in (24, 76):
        p.add(poly([(42, y - 4), (50, y - 6), (52, y + 4), (44, y + 6)]), (120, 160, 86), 1.6)
    p.add(("rect", (44, 44, 52, 56)), (110, 74, 44), 2)
    return p


def elven_chain():
    p = Pic()
    shirt = [(30, 14), (70, 14), (90, 30), (82, 46), (72, 40), (72, 90), (28, 90), (28, 40), (18, 46), (10, 30)]
    p.add(poly(shirt), (214, 226, 236), 2.8)
    p.add(poly([(50, 14), (70, 14), (90, 30), (82, 46), (72, 40), (72, 90), (50, 90)]), (180, 196, 210), 0)
    for y in range(24, 88, 6):
        for x in range(30 + (y // 6 % 2) * 3, 72, 6):
            p.add(ell((x - 1.3, y - 1.3, x + 1.3, y + 1.3)), (150, 168, 186), 0)
    p.add(poly([(42, 14), (58, 14), (50, 26)]), INK, 0)
    p.add(tent([(40, 50), (50, 60), (60, 50)], 2, 2), (120, 160, 86), 0)
    return p


def greataxe():
    p = Pic()
    p.add(tent([(24, 92), (66, 18)], 7, 7), WOOD_D, 2.6)
    blade = [(54, 10), (86, 18), (90, 52), (68, 46), (58, 30)]
    p.add(poly(blade), (132, 128, 120), 3)
    p.add(poly([(70, 14), (86, 18), (90, 52), (70, 46)]), (98, 94, 88), 0)
    p.add(poly([(56, 30), (36, 22), (32, 44), (52, 40)]), (132, 128, 120), 2.6)
    for x, y in ((80, 24), (82, 34)):
        p.line([(x - 4, y), (x + 4, y + 2)], 1.6, (170, 40, 36))
    return p


def horn_helm():
    p = Pic()
    for s in (1, -1):
        horn = [(50 - 18 * s, 44), (50 - 34 * s, 30), (50 - 40 * s, 12), (50 - 30 * s, 26), (50 - 14 * s, 34)]
        if s == -1:
            horn = mir([(50 - 18, 44), (50 - 34, 30), (50 - 40, 12), (50 - 30, 26), (50 - 14, 34)])
        p.add(poly(horn), BONE, 2.6)
    p.add(poly([(24, 82), (24, 46), (36, 30), (64, 30), (76, 46), (76, 82), (60, 86), (60, 62), (40, 62), (40, 86)]), STEEL, 3)
    p.add(poly([(50, 30), (64, 30), (76, 46), (76, 82), (60, 86), (60, 62), (50, 62)]), STEEL_D, 0)
    p.line([(50, 34), (50, 60)], 2, STEEL_D)
    return p


def astral_armor():
    p = Pic()
    body = [(30, 14), (70, 14), (90, 30), (80, 50), (70, 44), (70, 90), (30, 90), (30, 44), (20, 50), (10, 30)]
    p.add(poly(body), (176, 156, 96), 2.8)
    p.add(poly([(50, 14), (70, 14), (90, 30), (80, 50), (70, 44), (70, 90), (50, 90)]), (140, 122, 70), 0)
    p.add(poly([(38, 30), (62, 30), (66, 70), (50, 84), (34, 70)]), (120, 186, 196), 2.2)
    p.add(poly([(50, 36), (58, 56), (50, 76), (42, 56)]), (220, 246, 250), 0)
    return p


def shed_armor():
    p = Pic()
    body = [(30, 14), (70, 14), (90, 30), (80, 50), (70, 44), (70, 90), (30, 90), (30, 44), (20, 50), (10, 30)]
    p.add(poly(body), (148, 186, 222), 2.8)
    p.add(poly([(50, 14), (70, 14), (90, 30), (80, 50), (70, 44), (70, 90), (50, 90)]), (116, 154, 196), 0)
    for row in range(5):
        for col in range(4):
            x, y = 36 + col * 9 + (row % 2) * 4, 30 + row * 11
            p.add(poly([(x - 4, y), (x, y - 4), (x + 4, y), (x, y + 5)]), (96, 132, 178), 0)
    return p


def tendril_cloak():
    p = cloak((106, 70, 120), (80, 50, 96), (200, 150, 214))
    for x in (30, 42, 58, 70):
        p.add(tent([(x, 84), (x - 3, 92), (x + 2, 98)], 4, 1.4), (150, 108, 162), 1.4)
    return p


def bone_talisman():
    p = Pic()
    p.add(tent(arc(50, 26, 22, 180, 360, 20), 2.6, 2.6), LEATHER, 1.6)
    p.add(ell((30, 40, 70, 80)), BONE, 3)
    p.add([ell((38, 52, 48, 62)), ell((52, 52, 62, 62))], INK, 0)
    p.add(poly([(48, 64), (52, 64), (50, 68)]), INK, 0)
    for x in (42, 50, 58):
        p.line([(x, 72), (x, 78)], 1.6, INK)
    p.add(ell((44, 82, 56, 94)), (150, 214, 160), 2)
    return p


# ---------------- 其他 ----------------
def statue():
    p = Pic()
    p.add(("rect", (26, 82, 74, 94)), (128, 124, 116), 2.6)
    p.add([ell((38, 12, 62, 36)), poly([(34, 40), (66, 40), (72, 82), (28, 82)])], (178, 174, 164), 3)
    p.add(poly([(50, 40), (66, 40), (72, 82), (50, 82)]), (150, 146, 138), 0)
    for a, b in (((40, 54), (46, 60)), ((58, 66), (62, 72)), ((44, 22), (48, 28))):
        p.line([a, b], 1.4, (110, 106, 100))
    return p


def ab_hammer():
    p = throwing_hammer()
    p.add(tent(arc(50, 50, 40, 200, 320, 16), 3, 1), (240, 240, 255), 0)
    return p


def ab_charge():
    p = horn_helm()
    for y in (50, 62, 74):
        p.line([(4, y), (18, y)], 3, (240, 200, 120))
    return p


def ab_speed():
    p = boots((178, 52, 46), (138, 36, 34), GOLD, True)
    for y in (40, 56, 72):
        p.line([(2, y), (12, y)], 3, (255, 230, 150))
    return p


# ---------------- 炼金桌（非方形） ----------------
def save_rect(p, rel, w, h, box):
    path = os.path.join(base.TEX, rel.replace("/", os.sep) + ".png")
    os.makedirs(os.path.dirname(path), exist_ok=True)
    k = base.WORK / 100
    p.img.crop(tuple(int(v * k) for v in box)).resize((w, h), Image.LANCZOS).save(path)
    return path


def table_long():
    """北/南向：2×1，画在 100×50 的区域里（y 25..75）。"""
    p = Pic()
    p.add(("rect", (4, 34, 96, 70)), WOOD, 2.6)
    p.add(("rect", (4, 56, 96, 70)), WOOD_D, 0)
    for x, col in ((18, (196, 48, 52)), (34, (120, 196, 90)), (76, (152, 112, 204))):
        p.add([ell((x - 7, 34, x + 7, 50)), ("rect", (x - 2.5, 26, x + 2.5, 38))], GLASS, 2)
        p.add(ell((x - 5.5, 40, x + 5.5, 48.5)), col, 0)
    p.add([ell((48, 36, 62, 50))], (130, 124, 114), 2)
    p.add(ell((51, 38, 59, 46)), (90, 84, 76), 0)
    p.line([(60, 36), (70, 28)], 2.4, WOOD_D)
    p.add(("rect", (86, 30, 94, 48)), (214, 196, 150), 2)
    return p


def table_east():
    """东向：1×2，画在 50×100 的区域里（x 25..75）。"""
    p = Pic()
    p.add(("rect", (30, 4, 66, 96)), WOOD, 2.6)
    p.add(("rect", (52, 4, 66, 96)), WOOD_D, 0)
    for y, col in ((18, (196, 48, 52)), (36, (120, 196, 90)), (78, (152, 112, 204))):
        p.add([ell((32, y - 7, 46, y + 7))], GLASS, 2)
        p.add(ell((34, y - 4, 44, y + 5)), col, 0)
    p.add(ell((34, 50, 48, 64)), (130, 124, 114), 2)
    p.add(ell((37, 53, 45, 61)), (90, 84, 76), 0)
    return p


POTIONS = {"Healing": p_healing, "GreaterHealing": p_greater, "GiantStrength": p_giant, "FireResistance": p_fire,
           "Invisibility": p_invis, "Heroism": p_heroism, "MindShield": p_mind, "TrollRegen": p_troll}
THROWABLES = {"AlchemistFire": t_alchemist_fire, "AcidVial": t_acid, "HolyWater": t_holy, "Thunderstone": t_thunder,
              "TanglefootBag": t_tangle, "DispelDust": t_dispel}
MATERIALS = {"LavaCrystal": lava_one, "DragonShed": shed_one, "TrollHide": hide_one,
             "Mithral": ingot_one((214, 230, 240), (170, 190, 206), (255, 255, 255)),
             "Adamantine": ingot_one((96, 70, 122), (66, 46, 88), (176, 150, 210)),
             "ColdIron": ingot_one((92, 104, 118), (64, 74, 88), (170, 196, 220))}
SCROLLS = {"Teleport": ((90, 120, 210), r_teleport), "Revivify": ((214, 180, 60), r_revive), "ProtectionEvil": ((230, 222, 200), r_ward),
           "Fireball": ((206, 80, 40), r_fire), "TrueSeeing": ((70, 160, 190), r_eye), "FleshToStone": ((120, 116, 108), r_stone),
           "FearGaze": ((70, 50, 90), r_skull), "NecroticBlast": ((60, 110, 60), r_burst)}
WONDROUS = {"BagOfHolding": bag_of_holding, "CloakElvenkind": lambda: cloak((96, 140, 84), (72, 110, 62), GOLD),
            "BootsElvenkind": lambda: boots((110, 140, 80), (84, 110, 60), (190, 170, 110)),
            "BootsSpeed": lambda: boots((178, 52, 46), (138, 36, 34), GOLD, True), "IntellectFortress": headband,
            "RingFire": lambda: ring((226, 80, 40)), "RingCold": lambda: ring((120, 196, 240)), "RingAcid": lambda: ring((140, 214, 60)),
            "RingMind": lambda: ring((176, 120, 230), SILVER, SILVER_D), "AmuletHealth": amulet, "IounStone": ioun, "IounGlow": ioun_glow,
            "WandDetection": wand}
RACEGEAR = {"DwarvenThrowingHammer": throwing_hammer, "DwarvenRuneShield": rune_shield, "ElvenLongbow": longbow, "ElvenChain": elven_chain,
            "OrcishGreataxe": greataxe, "MinotaurHornHelm": horn_helm, "GithAstralArmor": astral_armor, "DragonShedArmor": shed_armor,
            "IllithidTendrilCloak": tendril_cloak, "BoneTalisman": bone_talisman}
ABILITIES = {"HammerThrow": ab_hammer, "HelmCharge": ab_charge, "SpeedBurst": ab_speed}

if __name__ == "__main__":
    out = []
    for n, fn in POTIONS.items():
        out.append(fn().save("Items/Potions/" + n, 128))
    for n, fn in THROWABLES.items():
        out.append(fn().save("Items/Throwables/" + n, 128))
        out.append(fn().save("Items/Projectiles/" + n, 64))
    for n, fn in MATERIALS.items():
        for i, s in enumerate("abc"):
            out.append(stack(fn, i + 1).save("Items/Materials/%s/%s_%s" % (n, n, s), 128))
    for n, (seal, rune) in SCROLLS.items():
        out.append(scroll(seal, rune).save("Items/Scrolls/" + n, 128))
    for n, fn in WONDROUS.items():
        out.append(fn().save("Items/Wondrous/" + n, 128))
    for n, fn in RACEGEAR.items():
        out.append(fn().save("Items/RaceGear/" + n, 128))
    for n, fn in ABILITIES.items():
        out.append(fn().save("Items/Abilities/" + n, 128))
    out.append(statue().save("Items/PetrifiedStatue", 128))
    t = table_long()
    out.append(save_rect(t, "Items/AlchemyTable_north", 256, 128, (0, 25, 100, 75)))
    out.append(save_rect(t, "Items/AlchemyTable_south", 256, 128, (0, 25, 100, 75)))
    out.append(save_rect(table_east(), "Items/AlchemyTable_east", 128, 256, (25, 0, 75, 100)))
    print("\n".join(out))
    print(len(out), "textures")
