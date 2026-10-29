# 1.45.0 魔法物品贴图：沿用 draw_race_art.py / draw_item_art.py 的程序绘制（粗黑轮廓、平涂 + 一层暗面、低饱和）。
# 物品图 128×128，放在 Textures/Items/Magic；石魔像图标放 Textures/Necromancy。
# 用法：python -I ArtSource/RaceArt/draw_magic_items_145.py <模组根目录>
import math, os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import draw_race_art as base
from draw_race_art import Pic, poly, ell, tent, arc, ellpts, mir, INK
import draw_item_art as items
from draw_item_art import shade, light, GOLD, GOLD_D, SILVER, SILVER_D, STEEL, STEEL_D, WOOD, WOOD_D, LEATHER, LEATHER_D, PARCH, PARCH_D, BONE, WHITE, GLASS

ROOT = sys.argv[1] if len(sys.argv) > 1 else os.getcwd()
base.TEX = os.path.join(ROOT, "Textures")

RUBY = (176, 58, 58); SAPPH = (70, 104, 168); EMER = (78, 140, 96); AMETH = (140, 96, 170); AMBER = (206, 150, 62)
JADE = (104, 160, 118); FIRE = (230, 120, 50); FIRE_L = (255, 206, 110); ICE = (170, 210, 228); ICE_D = (120, 166, 190)
MITH = (190, 204, 214); MITH_D = (142, 158, 170); BLACK = (70, 66, 72); BLOOD = (150, 40, 40); CLOTH = (120, 100, 140)
STONE = (140, 136, 128); STONE_D = (108, 104, 98)


def gem(p, cx, cy, r, c):
    p.add(poly([(cx - r, cy), (cx, cy - r), (cx + r, cy), (cx, cy + r)]), c, 2.2)
    p.add(poly([(cx, cy - r), (cx + r, cy), (cx, cy + r)]), shade(c, .75), 0)
    p.add(poly([(cx - r * .45, cy - r * .1), (cx, cy - r * .6), (cx + r * .1, cy - r * .2)]), light(c, .6), 0)


# ---------------- 剑 ----------------
def sword(blade, blade_d, guard, grip, extra=None, wide=1.0, length=1.0):
    p = Pic()
    tip = (82 - 4 * (1 - length), 18 + 4 * (1 - length))
    w = 7 * wide
    # 剑身（从左下到右上的斜剑）
    ang = math.atan2(tip[1] - 64, tip[0] - 40)
    nx, ny = -math.sin(ang) * w / 2, math.cos(ang) * w / 2
    b0 = (40, 64)
    bl = [(b0[0] + nx, b0[1] + ny), (tip[0] + nx * .6 - 3, tip[1] + ny * .6 + 2), tip, (tip[0] - nx * .6 + 2, tip[1] - ny * .6 - 3), (b0[0] - nx, b0[1] - ny)]
    p.add(poly(bl), blade, 2.8)
    p.add(poly([bl[0], bl[1], bl[2], (b0[0], b0[1])]), blade_d, 0)
    if extra:
        extra(p, b0, tip)
    # 护手、握柄、柄头
    p.add(tent([(28, 52), (52, 76)], 6, 6), guard, 2.6)
    p.add(tent([(38, 66), (20, 84)], 5.5, 5.5), grip, 2.4)
    p.add(ell((12, 80, 24, 92)), guard, 2.4)
    return p


def flame_tongue():
    def flames(p, b0, tip):
        for t, s in ((.3, 1), (.5, -1), (.7, 1), (.86, -1)):
            x = b0[0] + (tip[0] - b0[0]) * t; y = b0[1] + (tip[1] - b0[1]) * t
            p.add(poly([(x - 3, y + 3), (x + 4 * s, y - 9), (x + 6, y + 1)]), FIRE, 1.6)
            p.add(poly([(x - 1, y + 1), (x + 2 * s, y - 5), (x + 3, y + 1)]), FIRE_L, 0)
    return sword((214, 120, 80), (170, 84, 56), (120, 60, 40), (80, 52, 40), flames)


def frost_brand():
    def frost(p, b0, tip):
        for t in (.35, .6, .82):
            x = b0[0] + (tip[0] - b0[0]) * t; y = b0[1] + (tip[1] - b0[1]) * t
            for a in (0, 60, 120):
                r = math.radians(a)
                p.line([(x - 4 * math.cos(r), y - 4 * math.sin(r)), (x + 4 * math.cos(r), y + 4 * math.sin(r))], 1.3, (240, 250, 255))
    return sword(ICE, ICE_D, (110, 140, 170), (70, 80, 100), frost)


def vorpal_sword():
    def edge(p, b0, tip):
        p.line([(b0[0] + 2, b0[1] - 2), (tip[0] - 1, tip[1] + 1)], 1.2, (250, 250, 255))
    return sword((206, 210, 216), (160, 166, 176), (60, 56, 70), (40, 38, 46), edge, wide=.75)


def dragon_slayer():
    def scales(p, b0, tip):
        for t in (.3, .5, .7):
            x = b0[0] + (tip[0] - b0[0]) * t; y = b0[1] + (tip[1] - b0[1]) * t
            p.add(ell((x - 2.6, y - 2.6, x + 2.6, y + 2.6)), (120, 150, 110), 1.2)
    p = sword((170, 170, 166), (128, 128, 124), (150, 112, 60), (90, 60, 40), scales, wide=1.4)
    # 龙首护手
    p.add(poly([(22, 46), (30, 40), (36, 48), (30, 54)]), (150, 112, 60), 2.2)
    p.add(ell((27, 44, 30, 47)), (220, 60, 40), 0)
    return p


def defender_sword():
    p = sword(MITH, MITH_D, (196, 176, 112), (70, 80, 120), None)
    p.add(poly([(24, 44), (44, 40), (56, 60), (34, 72), (22, 60)]), (196, 176, 112), 2.6)
    p.add(poly([(39, 50), (45, 52), (43, 60), (37, 58)]), (90, 110, 170), 1.4)
    return p


def sword_vengeance():
    def runes(p, b0, tip):
        for t in (.35, .55, .75):
            x = b0[0] + (tip[0] - b0[0]) * t; y = b0[1] + (tip[1] - b0[1]) * t
            p.add(ell((x - 1.8, y - 1.8, x + 1.8, y + 1.8)), (170, 40, 60), 0)
    return sword((120, 116, 124), (86, 82, 92), (50, 46, 52), (40, 30, 30), runes)


def javelin(flight=False):
    p = Pic()
    shaft = [(14, 88), (78, 22)]
    p.add(tent(shaft, 4, 4), WOOD, 2.4)
    p.add(poly([(74, 26), (90, 10), (84, 30)]), STEEL, 2.4)
    for t in (.25, .45, .65):
        x = 14 + 64 * t; y = 88 - 66 * t
        p.add(tent([(x - 3, y + 3), (x + 3, y - 3)], 5, 5), (196, 130, 60), 1.4)
    p.add(poly([(60, 20), (68, 30), (62, 32), (72, 44), (58, 34), (64, 32)]), (255, 236, 120), 1.6)
    if not flight:
        for dx in (-8, 8):
            p.add(tent([(14 + dx, 92), (70 + dx, 30)], 3, 3), WOOD_D, 2)
    return p


# ---------------- 手套 / 护腕 ----------------
def glove(color, dark, trim, studs=False):
    p = Pic()
    palm = [(30, 86), (30, 46), (36, 30), (44, 28), (46, 44), (50, 24), (58, 24), (60, 44), (64, 28), (72, 30), (72, 52), (80, 46), (86, 52), (72, 78), (70, 86)]
    p.add(poly(palm), color, 2.8)
    p.add(poly([(50, 50), (72, 52), (80, 46), (86, 52), (72, 78), (70, 86), (50, 86)]), dark, 0)
    p.add(("rect", (28, 80, 72, 94)), trim, 2.6)
    if studs:
        for x in (38, 50, 62):
            p.add(ell((x - 3, 48 - 3, x + 3, 48 + 3)), STEEL, 1.4)
    return p


def gauntlets_ogre():
    return glove((120, 96, 70), (96, 74, 52), STEEL, studs=True)


def gloves_snaring():
    p = glove((200, 196, 186), (166, 162, 150), SILVER)
    p.line([(36, 60), (66, 60)], 1.2, (140, 150, 170))
    p.line([(36, 68), (66, 68)], 1.2, (140, 150, 170))
    p.add(tent([(70, 18), (92, 6)], 2.4, 2.4), WOOD, 1.4)
    p.add(poly([(88, 4), (96, 2), (92, 10)]), STEEL, 1.2)
    return p


def bracer(color, dark, emblem):
    p = Pic()
    for x in (18, 54):
        p.add(poly([(x, 26), (x + 28, 22), (x + 30, 80), (x + 2, 84)]), color, 2.8)
        p.add(poly([(x + 15, 24), (x + 28, 22), (x + 30, 80), (x + 16, 82)]), dark, 0)
        emblem(p, x + 15, 52)
    return p


def bracers_archery():
    def feather(p, x, y):
        p.add(poly([(x, y - 12), (x + 5, y), (x, y + 12), (x - 5, y)]), (226, 214, 180), 1.6)
        p.line([(x, y - 11), (x, y + 11)], 1, (120, 90, 60))
    return bracer(LEATHER, LEATHER_D, feather)


def bracers_defense():
    def shield(p, x, y):
        p.add(poly([(x - 8, y - 9), (x + 8, y - 9), (x + 7, y + 3), (x, y + 11), (x - 7, y + 3)]), (200, 170, 100), 1.6)
    return bracer((176, 120, 72), (140, 92, 52), shield)


# ---------------- 头顶 / 眼 ----------------
def circlet(band, band_d, stone):
    p = Pic()
    p.add([ell((14, 34, 86, 84)), ("-ell", (22, 42, 78, 76))], band, 2.8)
    p.add([ell((14, 34, 86, 84)), ("-ell", (22, 42, 78, 76)), ("-rect", (0, 0, 100, 59))], band_d, 0)
    p.add(poly([(40, 40), (50, 22), (60, 40)]), band, 2.4)
    gem(p, 50, 40, 9, stone)
    return p


def helm_telepathy():
    p = circlet(SILVER, SILVER_D, AMETH)
    for a in (200, 240, 300, 340):
        r = math.radians(a)
        p.line([(50 + 16 * math.cos(r), 40 + 16 * math.sin(r)), (50 + 24 * math.cos(r), 40 + 24 * math.sin(r))], 1.6, (210, 190, 240))
    return p


def eyes(lens, frame, frame_d, extra=None):
    p = Pic()
    p.add(tent([(18, 50), (82, 50)], 4, 4), frame, 2.2)
    for cx in (32, 68):
        p.add(ell((cx - 15, 36, cx + 15, 66)), frame, 2.8)
        p.add(ell((cx - 11, 40, cx + 11, 62)), lens, 0)
        p.add(poly([(cx - 6, 44), (cx - 2, 44), (cx - 8, 54)]), light(lens, .55), 0)
    if extra:
        extra(p)
    return p


def eyes_eagle():
    def feather(p):
        p.add(poly([(44, 30), (50, 16), (56, 30), (50, 36)]), (170, 130, 80), 1.8)
    return eyes((196, 214, 226), GOLD, GOLD_D, feather)


def eyes_minute():
    def loupe(p):
        p.add(ell((60, 64, 84, 88)), SILVER, 2.4)
        p.add(ell((64, 68, 80, 84)), (220, 236, 240), 0)
    return eyes((214, 226, 230), SILVER, SILVER_D, loupe)


def goggles_night():
    p = Pic()
    p.add(tent(arc(50, 60, 40, 200, 340, 20), 5, 5), LEATHER_D, 2.2)
    for cx in (32, 68):
        p.add(ell((cx - 16, 34, cx + 16, 66)), (90, 84, 76), 2.8)
        p.add(ell((cx - 11, 39, cx + 11, 61)), (40, 70, 60), 0)
        p.add(ell((cx - 7, 42, cx - 1, 48)), (120, 200, 160), 0)
    p.add(("rect", (44, 46, 56, 54)), (90, 84, 76), 2)
    return p


def halo(glow=False):
    p = Pic()
    if glow:
        for r, a in ((46, 0), (40, 0)):
            pass
        p.add([ell((6, 34, 94, 66)), ("-ell", (16, 40, 84, 60))], (255, 236, 160), 0)
        return p
    p.add([ell((12, 30, 88, 70)), ("-ell", (22, 38, 78, 62))], GOLD, 2.8)
    p.add([ell((12, 30, 88, 70)), ("-ell", (22, 38, 78, 62)), ("-rect", (0, 0, 100, 50))], GOLD_D, 0)
    for a in range(0, 360, 45):
        r = math.radians(a)
        p.line([(50 + 42 * math.cos(r), 50 + 24 * math.sin(r)), (50 + 48 * math.cos(r), 50 + 28 * math.sin(r))], 1.8, (255, 230, 150))
    return p


def psychic_hood():
    p = Pic()
    hood = [(20, 90), (24, 40), (38, 18), (62, 18), (76, 40), (80, 90), (64, 86), (62, 52), (50, 44), (38, 52), (36, 86)]
    p.add(poly(hood), (70, 70, 100), 2.8)
    p.add(poly([(50, 18), (62, 18), (76, 40), (80, 90), (64, 86), (62, 52), (50, 44)]), (54, 54, 80), 0)
    for x, y in ((34, 34), (50, 26), (66, 34)):
        gem(p, x, y, 4.5, (150, 200, 230))
    return p


# ---------------- 颈部 ----------------
def chain(p, color=GOLD):
    p.add(tent(arc(50, 44, 24, 190, 350, 24), 2.6, 2.6), color, 1.8)
    p.add(tent([(26.4, 39.8), (44, 50)], 2.6, 2.6), color, 1.8)
    p.add(tent([(73.6, 39.8), (56, 50)], 2.6, 2.6), color, 1.8)


def periapt():
    p = Pic(); chain(p)
    p.add(poly([(50, 44), (66, 62), (50, 88), (34, 62)]), (176, 70, 64), 2.8)
    p.add(poly([(50, 44), (66, 62), (50, 88)]), (140, 50, 48), 0)
    p.add([("rect", (47, 56, 53, 76)), ("rect", (42, 63, 58, 69))], (246, 220, 200), 0)
    return p


def amulet_proof():
    p = Pic(); chain(p, SILVER)
    p.add(poly([(36, 48), (64, 48), (60, 86), (40, 86)]), (110, 112, 118), 2.8)
    p.add(poly([(50, 48), (64, 48), (60, 86), (50, 86)]), (84, 86, 92), 0)
    p.add(ell((44, 58, 56, 70)), (70, 72, 78), 1.4)
    return p


def necklace_adapt():
    p = Pic(); chain(p, SILVER)
    p.add(ell((30, 46, 70, 86)), (200, 226, 236), 2.8)
    p.add(ell((36, 52, 64, 80)), (230, 244, 250), 0)
    p.add(tent(arc(50, 66, 8, 200, 470, 18), 2, 1), (130, 180, 200), 0)
    return p


def necklace_fireballs():
    p = Pic()
    pts = arc(50, 30, 30, 160, 380, 9)
    p.add(tent(arc(50, 30, 30, 160, 380, 24), 2, 2), GOLD_D, 1.4)
    for i, (x, y) in enumerate(pts[1:-1]):
        p.add(ell((x - 6, y - 6, x + 6, y + 6)), (196, 70, 44), 2)
        p.add(ell((x - 3, y - 4, x + 1, y)), (250, 170, 110), 0)
    return p


def beads(color, dark, pendant):
    p = Pic()
    for i, (x, y) in enumerate(arc(50, 40, 30, 150, 390, 16)):
        p.add(ell((x - 4.5, y - 4.5, x + 4.5, y + 4.5)), color if i % 4 else dark, 1.6)
    pendant(p)
    return p


def prayer_beads():
    def p_(p):
        p.add([("rect", (47, 66, 53, 92)), ("rect", (40, 72, 60, 78))], (190, 160, 100), 2)
    return beads((140, 98, 62), (110, 74, 44), p_)


def martyrs_beads(broken=False):
    def p_(p):
        rose = (180, 60, 70) if not broken else (120, 80, 80)
        p.add(ell((38, 66, 62, 90)), rose, 2.4)
        p.add(ell((44, 72, 56, 84)), shade(rose, .75), 0)
        if broken:
            p.line([(40, 68), (50, 78), (46, 86)], 1.4, INK)
            p.line([(58, 66), (52, 76)], 1.2, INK)
    return beads((196, 70, 80) if not broken else (140, 110, 110), (150, 50, 60) if not broken else (110, 90, 90), p_)


def spirit_stone():
    p = Pic(); chain(p, SILVER)
    p.add(ell((32, 46, 68, 90)), (226, 230, 232), 2.8)
    p.add(ell((38, 52, 62, 84)), (206, 214, 222), 0)
    p.add(tent(arc(50, 68, 7, 0, 300, 12), 2, 1), (160, 190, 220), 0)
    return p


def amulet_planes():
    p = Pic(); chain(p, SILVER)
    p.add(ell((30, 46, 70, 86)), SILVER, 2.8)
    p.add([ell((36, 52, 64, 80)), ("-ell", (42, 58, 58, 74))], (90, 110, 170), 0)
    for a in (20, 140, 260):
        r = math.radians(a)
        p.add(ell((50 + 10 * math.cos(r) - 2, 66 + 10 * math.sin(r) - 2, 50 + 10 * math.cos(r) + 2, 66 + 10 * math.sin(r) + 2)), (230, 230, 255), 0)
    return p


def waaagh_totem():
    p = Pic()
    p.add(tent(arc(50, 30, 28, 170, 370, 20), 2.2, 2.2), LEATHER, 1.4)
    for i, (x, y) in enumerate(arc(50, 30, 28, 190, 350, 6)):
        p.add(poly([(x - 3, y), (x + 3, y), (x, y + 14)]), BONE, 1.6)
    p.add(poly([(36, 58), (64, 58), (60, 90), (40, 90)]), (124, 146, 90), 2.6)
    p.add(poly([(42, 66), (48, 72), (52, 68), (58, 74)]), INK, 0)
    p.line([(44, 80), (56, 80)], 2, INK)
    return p


# ---------------- 胸前 ----------------
def brooch_shielding():
    p = Pic()
    p.add(poly([(24, 24), (76, 24), (74, 56), (50, 84), (26, 56)]), SILVER, 2.8)
    p.add(poly([(50, 24), (76, 24), (74, 56), (50, 84)]), SILVER_D, 0)
    gem(p, 50, 46, 10, (120, 160, 210))
    return p


def scarab():
    p = Pic()
    p.add(ell((26, 20, 74, 86)), JADE, 2.8)
    p.add(ell((50, 20, 74, 86)), shade(JADE, .78), 0)
    p.line([(50, 34), (50, 84)], 1.6, INK)
    p.add(ell((38, 10, 62, 30)), shade(JADE, .85), 2.4)
    for s in (-1, 1):
        for y in (40, 56, 70):
            p.line([(50 + 22 * s, y), (50 + 34 * s, y + 6)], 2, INK)
    p.add(tent(arc(50, 60, 10, 200, 340, 8), 1.6, 1.6), GOLD, 0)
    return p


def cog():
    p = Pic()
    teeth = []
    for i in range(24):
        a = math.radians(i * 15)
        r = 40 if (i // 1) % 2 == 0 else 32
        teeth.append((50 + r * math.cos(a), 50 + r * math.sin(a)))
    p.add([poly(teeth), ("-ell", (38, 38, 62, 62))], (176, 120, 72), 2.8)
    p.add([poly(teeth), ("-ell", (38, 38, 62, 62)), ("-rect", (0, 0, 52, 100))], (140, 92, 52), 0)
    p.add(ell((44, 44, 56, 56)), (200, 60, 50), 2)
    return p


def inquisitor_seal():
    p = Pic()
    for a in range(0, 360, 72):
        r = math.radians(a - 90)
        x, y = 50 + 20 * math.cos(r), 48 + 20 * math.sin(r)
        p.add(ell((x - 13, y - 13, x + 13, y + 13)), (170, 50, 60), 2.4)
    p.add(ell((34, 32, 66, 64)), GOLD, 2.6)
    p.add(("rect", (46, 36, 54, 60)), (80, 60, 40), 0)
    p.add(tent([(50, 66), (50, 92)], 4, 4), GOLD_D, 1.8)
    return p


def revival_core():
    p = Pic()
    p.add(poly([(50, 10), (80, 30), (80, 70), (50, 90), (20, 70), (20, 30)]), (60, 64, 60), 2.8)
    p.add(poly([(50, 10), (80, 30), (80, 70), (50, 90)]), (44, 48, 44), 0)
    p.add(ell((34, 34, 66, 66)), (60, 170, 110), 2.4)
    p.add(ell((42, 42, 58, 58)), (160, 240, 190), 0)
    for a in (30, 150, 270):
        r = math.radians(a)
        p.line([(50 + 18 * math.cos(r), 50 + 18 * math.sin(r)), (50 + 30 * math.cos(r), 50 + 30 * math.sin(r))], 2, (120, 220, 160))
    return p


def mark(color, dark, sigil):
    p = Pic()
    p.add(poly([(50, 10), (84, 34), (76, 82), (24, 82), (16, 34)]), color, 2.8)
    p.add(poly([(50, 10), (84, 34), (76, 82), (50, 82)]), dark, 0)
    sigil(p)
    return p


def mark_blood():
    def s(p):
        for a in range(0, 360, 45):
            r = math.radians(a)
            p.line([(50, 50), (50 + 16 * math.cos(r), 50 + 16 * math.sin(r))], 2.2, (240, 200, 190))
            p.add(poly([(50 + 16 * math.cos(r) - 2, 50 + 16 * math.sin(r) - 2), (50 + 21 * math.cos(r), 50 + 21 * math.sin(r)), (50 + 16 * math.cos(r) + 2, 50 + 16 * math.sin(r) + 2)]), (240, 200, 190), 0)
    return mark(BLOOD, shade(BLOOD, .75), s)


def mark_decay():
    def s(p):
        for x, y in ((50, 40), (40, 58), (60, 58)):
            p.add([ell((x - 9, y - 9, x + 9, y + 9)), ("-ell", (x - 5, y - 5, x + 5, y + 5))], (210, 220, 150), 0)
    return mark((96, 118, 70), (76, 94, 54), s)


def mark_change():
    def s(p):
        p.add(tent(arc(50, 48, 14, 0, 300, 18), 3, 1), (230, 220, 255), 0)
        p.add(tent(arc(50, 48, 6, 180, 420, 12), 2, 1), (180, 230, 250), 0)
    return mark((70, 100, 160), (54, 78, 130), s)


def mark_rapture():
    def s(p):
        p.add(tent([(36, 64), (46, 36), (56, 60), (64, 34)], 4, 2), (250, 220, 240), 0)
    return mark((168, 104, 160), (134, 80, 128), s)


# ---------------- 戒指 ----------------
def ring(gemc, band=GOLD, band_d=GOLD_D, extra=None):
    p = items.ring(gemc, band, band_d)
    if extra:
        extra(p)
    return p


def ring_spell_turning():
    return ring((200, 214, 226), SILVER, SILVER_D, lambda p: p.line([(44, 28), (56, 28)], 1.4, (90, 110, 140)))


def ring_free_action():
    def broken(p):
        p.line([(22, 64), (14, 58)], 2, STEEL_D)
        p.line([(78, 64), (86, 58)], 2, STEEL_D)
    return ring((150, 170, 186), SILVER, SILVER_D, broken)


def ring_regen():
    return ring((96, 150, 90), GOLD, GOLD_D)


def ring_protection():
    return ring((210, 210, 220), GOLD, GOLD_D)


def ring_warding():
    def link(p):
        p.add([ell((60, 60, 92, 92)), ("-ell", (66, 66, 86, 86))], SILVER, 2.4)
    return ring((150, 170, 220), GOLD, GOLD_D, link)


def warding_case():
    p = Pic()
    p.add(("rect", (14, 40, 86, 86)), (120, 70, 80), 2.8)
    p.add(("rect", (50, 40, 86, 86)), (96, 54, 64), 0)
    p.add(poly([(14, 40), (24, 22), (76, 22), (86, 40)]), (140, 84, 94), 2.6)
    for cx in (36, 64):
        p.add([ell((cx - 9, 52, cx + 9, 70)), ("-ell", (cx - 5, 56, cx + 5, 66))], GOLD, 1.8)
        p.add(ell((cx - 3, 48, cx + 3, 54)), (150, 170, 220), 1.2)
    return p


def ring_spell_storing():
    return ring((90, 130, 200), SILVER, SILVER_D, lambda p: p.add(ell((47, 25, 53, 31)), (230, 240, 255), 0))


def ring_animal():
    def fang(p):
        p.add(poly([(70, 60), (82, 52), (78, 74)]), BONE, 1.6)
    return ring((140, 110, 80), GOLD, GOLD_D, fang)


def ring_stars():
    def stars(p):
        for x, y in ((48, 26), (54, 32), (45, 33)):
            p.add(ell((x - 1.2, y - 1.2, x + 1.2, y + 1.2)), (255, 255, 230), 0)
    return ring((40, 50, 90), SILVER, SILVER_D, stars)


# ---------------- 圣物 / 器物 ----------------
def purity_seal():
    p = Pic()
    p.add(poly([(30, 30), (70, 30), (66, 92), (58, 84), (50, 92), (42, 84), (34, 92)]), PARCH, 2.6)
    p.add(poly([(50, 30), (70, 30), (66, 92), (58, 84), (50, 92)]), PARCH_D, 0)
    for y in (64, 72, 80):
        p.line([(38, y), (60, y)], 1.2, (150, 126, 90))
    p.add(ell((30, 12, 70, 52)), (170, 40, 44), 2.6)
    p.add(ell((38, 20, 62, 44)), (140, 30, 34), 0)
    p.add([("rect", (47.5, 22, 52.5, 42)), ("rect", (41, 29, 59, 34))], (240, 210, 190), 0)
    return p


def immovable_rod(active=False):
    p = Pic()
    if active:
        for r, c in ((30, (190, 200, 230)), (22, (220, 228, 250))):
            p.add(ell((50 - r, 50 - r * .5, 50 + r, 50 + r * .5)), c, 0)
    p.add(tent([(18, 82), (82, 18)], 9, 9), (110, 108, 112), 2.8)
    p.add(tent([(18, 82), (82, 18)], 4, 4), (86, 84, 90), 0)
    p.add(ell((74, 10, 90, 26)), (176, 60, 54), 2.4)
    return p


def decanter():
    p = Pic()
    p.add([ell((24, 36, 76, 92)), ("rect", (40, 16, 60, 42))], (90, 130, 176), 2.8)
    p.add(poly([(50, 36), (76, 50), (76, 80), (60, 92), (50, 92)]), (70, 106, 150), 0)
    for x in (34, 46, 58):
        p.line([(x, 56), (x + 6, 70)], 1.6, (220, 236, 245))
    p.add(("rect", (38, 10, 62, 20)), (90, 130, 176), 2.4)
    for x, y in ((30, 8), (50, 4), (70, 8)):
        p.add(ell((x - 3, y - 3, x + 3, y + 3)), (180, 220, 240), 1)
    return p


def eversmoking():
    p = Pic()
    p.add([ell((28, 44, 72, 92)), ("rect", (42, 22, 58, 50))], (176, 140, 80), 2.8)
    p.add(poly([(50, 44), (72, 60), (72, 80), (50, 92)]), (140, 108, 58), 0)
    p.add(("rect", (40, 16, 60, 26)), (120, 84, 60), 2.2)
    for x, y, r in ((44, 10, 8), (58, 6, 7), (66, 14, 6)):
        p.add(ell((x - r, y - r, x + r, y + r)), (196, 196, 200), 1.4)
    return p


def torch():
    p = Pic()
    p.add(tent([(38, 92), (56, 40)], 7, 9), WOOD, 2.6)
    p.add(("rect", (48, 36, 64, 46)), GOLD, 2.2)
    p.add(poly([(56, 8), (70, 26), (66, 38), (48, 38), (44, 26)]), (150, 200, 240), 2)
    p.add(poly([(57, 18), (64, 28), (60, 36), (52, 36), (50, 28)]), (230, 246, 255), 0)
    return p


def horn_valhalla():
    p = Pic()
    horn = [(16, 76), (30, 50), (54, 30), (82, 22), (86, 34), (60, 44), (40, 62), (28, 86)]
    p.add(poly(horn), (220, 206, 176), 2.8)
    p.add(poly([(54, 30), (82, 22), (86, 34), (60, 44)]), (190, 174, 142), 0)
    for t in ((34, 54, 44, 66), (52, 38, 60, 48)):
        p.line([(t[0], t[1]), (t[2], t[3])], 3, SILVER)
    p.add(ell((10, 72, 32, 94)), SILVER, 2.4)
    return p


def bag_tricks():
    p = Pic()
    p.add(poly([(24, 40), (76, 40), (86, 86), (14, 86)]), (130, 128, 122), 2.8)
    p.add(poly([(50, 40), (76, 40), (86, 86), (50, 86)]), (104, 102, 98), 0)
    p.add(tent([(26, 40), (74, 40)], 6, 6), (100, 90, 80), 2)
    p.add(ell((40, 20, 60, 42)), (170, 150, 120), 2.2)
    for x in (44, 56):
        p.add(poly([(x - 3, 26), (x, 14), (x + 3, 26)]), (170, 150, 120), 1.4)
    p.add(ell((44, 28, 47, 31)), INK, 0); p.add(ell((53, 28, 56, 31)), INK, 0)
    return p


def crystal_ball():
    p = Pic()
    p.add(poly([(28, 92), (36, 72), (64, 72), (72, 92)]), (60, 46, 40), 2.6)
    p.add(ell((20, 10, 80, 70)), (170, 190, 220), 2.8)
    p.add(ell((26, 16, 74, 64)), (196, 210, 232), 0)
    p.add(tent(arc(50, 40, 14, 20, 300, 18), 3, 1), (240, 244, 255), 0)
    p.add(ell((32, 20, 42, 30)), (250, 252, 255), 0)
    return p


def manual_golems():
    p = Pic()
    p.add(poly([(18, 24), (78, 16), (82, 80), (22, 88)]), STONE, 2.8)
    p.add(poly([(50, 20), (78, 16), (82, 80), (52, 84)]), STONE_D, 0)
    p.add(poly([(22, 88), (82, 80), (84, 86), (24, 94)]), PARCH_D, 2)
    p.add(("rect", (40, 36, 60, 64)), (120, 116, 108), 2)
    p.add(ell((45, 42, 49, 46)), (200, 60, 50), 0); p.add(ell((51, 42, 55, 46)), (200, 60, 50), 0)
    return p


def golem_icon():
    p = Pic()
    p.add(poly([(30, 30), (70, 30), (80, 64), (66, 66), (64, 92), (54, 92), (50, 70), (46, 92), (36, 92), (34, 66), (20, 64)]), STONE, 3)
    p.add(poly([(50, 30), (70, 30), (80, 64), (66, 66), (64, 92), (54, 92), (50, 70)]), STONE_D, 0)
    p.add(("rect", (38, 10, 62, 32)), STONE, 2.8)
    p.add(("rect", (50, 10, 62, 32)), STONE_D, 0)
    p.add(ell((42, 18, 47, 23)), (220, 70, 60), 0); p.add(ell((53, 18, 58, 23)), (220, 70, 60), 0)
    for x, y in ((40, 44), (58, 50), (46, 60)):
        p.line([(x, y), (x + 6, y + 4)], 1.4, INK)
    return p


def scroll_remove_curse():
    def r_break(p, x, y):
        p.add([ell((x - 6, y - 6, x + 6, y + 6)), ("-ell", (x - 3, y - 3, x + 3, y + 3))], (255, 246, 214), 0)
        p.line([(x - 7, y + 7), (x + 7, y - 7)], 1.6, (255, 246, 214))
    return items.scroll((90, 150, 210), r_break)


ICONS = {
    "FlameTongue": flame_tongue, "FrostBrand": frost_brand, "VorpalSword": vorpal_sword, "DragonSlayer": dragon_slayer, "DefenderSword": defender_sword,
    "JavelinLightning": javelin, "JavelinLightningFlight": lambda: javelin(True), "SwordVengeance": sword_vengeance,
    "GauntletsOgrePower": gauntlets_ogre, "GlovesMissileSnaring": gloves_snaring, "BracersArchery": bracers_archery, "BracersDefense": bracers_defense,
    "HelmTelepathy": helm_telepathy, "EyesEagle": eyes_eagle, "EyesMinuteSeeing": eyes_minute, "PeriaptWoundClosure": periapt, "AmuletProofDetection": amulet_proof,
    "RingSpellTurning": ring_spell_turning, "RingFreeAction": ring_free_action, "RingRegeneration": ring_regen,
    "RingProtection": ring_protection, "RingWardingBond": ring_warding, "WardingBondCase": warding_case, "RingSpellStoring": ring_spell_storing,
    "RingAnimalInfluence": ring_animal, "RingShootingStars": ring_stars, "NecklaceAdaptation": necklace_adapt, "NecklaceFireballs": necklace_fireballs,
    "PrayerBeads": prayer_beads, "BroochShielding": brooch_shielding, "ScarabProtection": scarab, "AmuletPlanes": amulet_planes, "GogglesNight": goggles_night,
    "HaloOfLight": halo, "HaloGlow": lambda: halo(True), "MartyrsBeads": martyrs_beads, "MartyrsBeadsBroken": lambda: martyrs_beads(True), "PuritySeal": purity_seal,
    "SpiritStone": spirit_stone, "PsychicHood": psychic_hood, "CogOfMakers": cog, "InquisitorSeal": inquisitor_seal, "WaaaghTotem": waaagh_totem, "RevivalCore": revival_core,
    "MarkBlood": mark_blood, "MarkDecay": mark_decay, "MarkChange": mark_change, "MarkRapture": mark_rapture,
    "ImmovableRod": immovable_rod, "ImmovableRodActive": lambda: immovable_rod(True), "DecanterEndlessWater": decanter, "EversmokingBottle": eversmoking,
    "EverburningTorch": torch, "HornValhalla": horn_valhalla, "BagOfTricks": bag_tricks, "CrystalBall": crystal_ball, "ManualOfGolems": manual_golems,
}

if __name__ == "__main__":
    out = []
    for n, fn in ICONS.items():
        out.append(fn().save("Items/Magic/" + n, 128))
    out.append(scroll_remove_curse().save("Items/Scrolls/RemoveCurse", 128))
    g = golem_icon()
    out.append(g.save("Necromancy/StoneGolem", 256))
    out.append(g.save("Necromancy/StoneGolem_south", 256))
    print(len(out), "textures")
