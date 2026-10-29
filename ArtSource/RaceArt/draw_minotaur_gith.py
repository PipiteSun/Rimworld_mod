# 1.18.0 牛头人与吉斯洋基贴图：沿用 draw_race_art.py 的程序绘制流程（粗黑轮廓、平涂、单侧暗面）。
# 用法：python -I ArtSource/RaceArt/draw_minotaur_gith.py <模组根目录>
import math, os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import draw_race_art as base
from draw_race_art import Pic, poly, ell, tent, arc, ellpts, mir, brain, INK, BONE, PSY, PSY_L, FLAY, FLAY_D, EYE, STONE, STONE_D, WOOD, WOOD_D, GOLD, GOLD_D, GREY
from PIL import Image

ROOT = sys.argv[1] if len(sys.argv) > 1 else os.getcwd()
base.TEX = os.path.join(ROOT, "Textures")

FUR = (124, 84, 52); FUR_D = (96, 62, 38); FUR_L = (154, 110, 74)
HORN = (234, 224, 196); HORN_D = (196, 180, 146); HORN_TIP = (112, 98, 84)
GITH = (176, 176, 116); GITH_D = (142, 144, 90); GITH_HAIR = (58, 52, 46)
SILVER = (196, 200, 208); SILVER_D = (148, 152, 162); SILVER_L = (236, 240, 246)
LEATHER = (92, 64, 54); BRASS = (206, 170, 92); BRASS_D = (164, 128, 64)
HUMAN = (150, 154, 156); HUMAN_D = (124, 128, 130)
HEAD_OW = 1.8  # 头部附件用细一些的轮廓，128px 下约2像素，与原版 MiniHorns 一致


def star(cx, cy, r, inner=.3):
    return poly([(cx, cy - r), (cx + r * inner, cy - r * inner), (cx + r, cy), (cx + r * inner, cy + r * inner), (cx, cy + r), (cx - r * inner, cy + r * inner), (cx - r, cy), (cx - r * inner, cy - r * inner)])


def horn(p, pts, w0, w1=1.6, ow=HEAD_OW, color=HORN, dark=HORN_D, tip=HORN_TIP):
    """一根弯牛角：浅色角身、内侧暗面、深色角尖。"""
    p.add(tent(pts, w0, w1), color, ow)
    n = len(pts)
    p.add(tent(pts[: max(2, n - 1)], w0 * .45, w1 * .6), dark, 0)  # 内侧暗面，沿中线偏细
    p.add(tent(pts[-2:], w0 * .35 + w1 * .5, w1), tip, 0)


# ---------------- 头部附件（128px，与原版头部贴图同一画框） ----------------
# 原版 Male_Average_Normal 头部在 128px 画框里约为 x 41-87、y 39-89 像素，即 100 单位画布的 x 32-68、y 30-70。
HORN_L = [(37, 38), (29, 35), (21, 30), (16, 23), (16, 15)]


def horns_south():
    p = Pic()
    for s in (lambda q: q, mir):
        horn(p, s(HORN_L), 7.2, 1.8)
        p.add(tent(s([(37, 38), (33, 36.5)]), 7.6, 7.2), HORN_D, HEAD_OW)  # 角根的环纹
    return p


def horns_north():
    # 背面：画在头后面（layer 10），只露出伸出头外的部分
    p = Pic()
    for s in (lambda q: q, mir):
        horn(p, s(HORN_L), 7.2, 1.8)
    return p


def horns_east():
    p = Pic()
    horn(p, [(54, 35), (52, 28), (55, 22), (61, 18), (66, 18)], 5.6, 1.4, color=HORN_D, dark=(170, 154, 122))  # 远侧角
    horn(p, [(46, 38), (41, 31), (42, 23), (48, 16), (55, 13)], 7.2, 1.8)
    p.add(tent([(46, 38), (43.5, 34.5)], 7.6, 7.2), HORN_D, HEAD_OW)
    return p


def snout_south():
    # 原版猪鼻的锚点：x 43-57、y 55-66（眼睛约在 y 53，鼻子不能再往上）
    p = Pic()
    p.add(ell((40, 56.5, 60, 69.5)), (250, 250, 250), HEAD_OW)
    p.add(poly(ellpts(54, 64, 5.5, 4.5, 0)), (226, 226, 226), 0)
    p.add([ell((43, 60.5, 47.5, 64.5)), ell((52.5, 60.5, 57, 64.5))], (96, 96, 96), 0)
    p.line([(46.5, 67.5), (50, 66.8), (53.5, 67.5)], 1.1, (120, 120, 120))
    return p


def snout_east():
    # 原版猪鼻侧面锚点：x 59-71、y 53-68
    p = Pic()
    p.add(poly([(59, 56), (70, 55), (76, 58.5), (77, 64), (72, 69), (60, 69)]), (250, 250, 250), HEAD_OW)
    p.add(poly([(64, 64), (76, 63), (72, 69), (62, 69)]), (226, 226, 226), 0)
    p.add(ell((70.5, 57.5, 74.5, 61.5)), (96, 96, 96), 0)
    p.line([(66, 67.3), (72, 66.8)], 1.1, (120, 120, 120))
    return p

# ---------------- 武器（128px，与原版长剑同规格：横放，剑柄在左） ----------------
def silver_sword():
    p = Pic()
    p.add(poly([(24, 47.5), (88, 46.5), (98, 50), (88, 53.5), (24, 52.5)]), SILVER, 1.8)
    p.add(poly([(24, 50), (88, 50), (98, 50), (88, 53.5), (24, 52.5)]), SILVER_D, 0)
    p.line([(28, 48.7), (86, 48.2)], .9, SILVER_L)
    for x in (34, 46, 58, 70, 80):
        p.add(star(x, 50, 2.2, .35), PSY, 0)
    p.line([(30, 50), (84, 50)], .5, PSY_L)
    p.add(poly([(20, 41), (24, 41), (25, 59), (21, 59)]), SILVER_D, 1.8)
    p.add(("rect", (8, 47.5, 20, 52.5)), LEATHER, 1.8)
    for x in (11, 14, 17):
        p.line([(x, 47.8), (x + 1.4, 52.2)], .6, (60, 40, 34))
    p.add(ell((2, 46, 9, 54)), SILVER, 1.8)
    p.add(ell((3.6, 47.6, 7.4, 52.4)), PSY, 0)
    return p


# ---------------- 异种型图标 ----------------
def minotaur_icon():
    p = Pic()
    for s in (lambda q: q, mir):
        horn(p, s([(34, 30), (22, 26), (12, 16), (10, 4)]), 9, 2.4, ow=base.OW)
        p.add(poly(s([(30, 38), (14, 36), (18, 46), (30, 47)])), FUR)  # 耳朵
    p.add(poly([(32, 22), (50, 18), (68, 22), (72, 40), (70, 62), (64, 74), (36, 74), (30, 62), (28, 40)]), FUR)
    p.add(poly([(62, 22), (68, 22), (72, 40), (70, 62), (64, 74), (58, 74), (64, 56), (66, 36)]), FUR_D, 0)
    p.add(poly([(36, 22), (50, 18), (64, 22), (58, 30), (50, 26), (42, 30)]), FUR_L, 0)  # 额头亮毛
    p.add(poly([(34, 64), (66, 64), (68, 80), (60, 90), (40, 90), (32, 80)]), (196, 160, 128))
    p.add([ell((39, 72, 46, 79)), ell((54, 72, 61, 79))], (70, 46, 36), 0)
    p.add(("line", arc(50, 84, 8, 20, 160), 2.6), BRASS, 1.4)
    for s in (lambda q: q, mir):
        p.line(s([(36, 42), (45, 46)]), 3.4)
    p.add([ell((38, 47, 43, 52)), ell((57, 47, 62, 52))], INK, 0)
    return p


def githyanki_icon():
    p = Pic()
    p.add(tent([(50, 14), (56, 6), (64, 4), (70, 10)], 7, 3), GITH_HAIR)  # 发髻
    ear = [(32, 44), (10, 30), (22, 54), (32, 60)]
    p.add([poly(ear), poly(mir(ear))], GITH)
    p.add(poly([(34, 22), (50, 14), (66, 22), (70, 44), (64, 70), (56, 84), (50, 88), (44, 84), (36, 70), (30, 44)]), GITH)
    p.add(poly([(60, 20), (66, 22), (70, 44), (64, 70), (56, 84), (52, 86), (60, 66), (64, 42)]), GITH_D, 0)
    p.add(poly([(34, 24), (50, 15), (66, 24), (62, 28), (50, 22), (38, 28)]), GITH_HAIR, 0)
    for s in (lambda q: q, mir):
        p.add(poly(s([(36, 46), (46, 44), (45, 50), (37, 50)])), (40, 34, 30), 1.4)
        p.line(s([(35, 41), (46, 39)]), 2.2)
        p.line(s([(40, 60), (44, 70)]), 1.2, GITH_D)  # 颧骨
    p.line([(50, 50), (52, 62), (48, 64)], 1.6)
    p.line([(44, 74), (50, 72), (56, 74)], 1.8)
    return p


# ---------------- 基因图标 ----------------
def charge():
    p = Pic()
    for y, x0 in ((40, 6), (54, 2), (68, 8)):
        p.add(tent([(x0, y), (x0 + 26, y)], 1.5, 5), (210, 196, 170), 1.4)
    p.add(star(84, 62, 12, .32), (250, 214, 120), 1.8)
    p.add(poly([(36, 34), (62, 30), (78, 44), (82, 58), (76, 70), (60, 72), (44, 66), (34, 52)]), FUR)
    p.add(poly([(62, 30), (78, 44), (82, 58), (76, 70), (66, 70), (72, 52)]), FUR_D, 0)
    horn(p, [(56, 36), (64, 26), (76, 22), (88, 26)], 7, 2, ow=2.2)
    horn(p, [(48, 36), (52, 24), (62, 16), (72, 16)], 6, 1.8, ow=2.2, color=HORN_D)
    p.add(ell((70, 56, 84, 70)), (196, 160, 128), 2)
    p.add(ell((78, 60, 81, 63)), (70, 46, 36), 0)
    p.add(ell((60, 44, 65, 49)), INK, 0)
    return p


def horns_icon():
    p = Pic()
    for s in (lambda q: q, mir):
        horn(p, s([(44, 74), (30, 70), (18, 58), (14, 42), (20, 22)]), 13, 3, ow=base.OW)
        p.add(tent(s([(44, 74), (38, 73)]), 13.6, 13), HORN_D, base.OW)
    p.add(poly([(40, 66), (60, 66), (62, 84), (50, 92), (38, 84)]), FUR)
    p.add(poly([(52, 66), (60, 66), (62, 84), (54, 90)]), FUR_D, 0)
    return p


def maze():
    p = Pic()
    p.add(("rect", (10, 10, 90, 90)), (214, 204, 182))
    walls = [[(18, 18), (82, 18), (82, 50)], [(18, 18), (18, 82), (60, 82)], [(30, 30), (70, 30), (70, 70), (42, 70)], [(30, 30), (30, 58)], [(42, 42), (58, 42), (58, 58)], [(82, 62), (82, 82), (72, 82)]]
    for w in walls:
        p.add(("line", w, 5), STONE, 1.4)
    p.add(("line", [(24, 90), (24, 24), (76, 24), (76, 76), (36, 76), (36, 36), (50, 36), (50, 50)], 2), (176, 70, 56), 0)
    horn(p, [(46, 52), (42, 48), (42, 44)], 3, 1, ow=1.2)
    horn(p, [(54, 52), (58, 48), (58, 44)], 3, 1, ow=1.2)
    p.add(ell((45, 50, 55, 58)), FUR, 1.4)
    return p


def stature_icon():
    p = Pic()
    p.add([ell((61, 37, 75, 51)), poly([(58, 54), (78, 54), (80, 76), (76, 92), (60, 92), (56, 76)])], HUMAN)
    p.add(("line", [(68, 56), (68, 90)], 1.2), HUMAN_D, 0)
    horn(p, [(24, 16), (16, 12), (10, 4)], 5, 1.6, ow=2)
    horn(p, [(40, 16), (48, 12), (54, 4)], 5, 1.6, ow=2)
    p.add([ell((20, 8, 44, 30)), poly([(10, 34), (54, 34), (58, 62), (50, 92), (14, 92), (6, 62)])], FUR)
    p.add(poly([(34, 34), (54, 34), (58, 62), (50, 92), (36, 92), (42, 60)]), FUR_D, 0)
    p.add(ell((26, 22, 38, 32)), (196, 160, 128), 1.4)
    p.add(("line", [(86, 8), (86, 92)], 1.6), (90, 96, 100), 0)
    for y in (8, 92):
        p.add(("line", [(82, y), (90, y)], 1.6), (90, 96, 100), 0)
    return p


def silver_sword_icon():
    p = Pic()
    for r in (30, 40):
        p.add(("line", arc(50, 50, r, 200, 250), 3.2), PSY, 1.4)
        p.add(("line", arc(50, 50, r, 20, 70), 3.2), PSY, 1.4)
    p.add(poly([(45, 70), (46, 18), (50, 6), (54, 18), (55, 70)]), SILVER)
    p.add(poly([(50, 6), (54, 18), (55, 70), (50, 70)]), SILVER_D, 0)
    p.line([(48, 20), (48, 66)], 1.2, SILVER_L)
    for y in (26, 38, 50, 62):
        p.add(star(50, y, 3, .35), PSY, 0)
    p.add(poly([(32, 70), (68, 70), (66, 76), (34, 76)]), SILVER_D)
    p.add(("rect", (46, 76, 54, 90)), LEATHER)
    p.add(ell((44, 88, 56, 98)), SILVER)
    p.add(ell((47, 90.5, 53, 95.5)), PSY, 0)
    return p


def astral_step():
    p = Pic()
    for cx, cy, col in ((24, 62, PSY), (76, 38, SILVER)):
        p.add(ring_e(cx, cy, 15, 21, 5, -20), col)
        p.add(poly(ellpts(cx, cy, 10, 16, -20)), (70, 56, 96), 0)
    for i in range(1, 6):
        t = i / 6
        x, y = 24 + 52 * t, 62 - 24 * t - 26 * math.sin(math.pi * t)
        p.add(ell((x - 2, y - 2, x + 2, y + 2)), PSY_L, 1.2)
    for cx, cy, r in ((50, 14, 5), (14, 30, 3.5), (88, 76, 4)):
        p.add(star(cx, cy, r), PSY_L, 1.2)
    return p


def ring_e(cx, cy, rx, ry, th, ang):
    return [poly(ellpts(cx, cy, rx, ry, ang)), ("-poly", ellpts(cx, cy, rx - th, ry - th, ang))]


def lich_devotee():
    p = Pic()
    p.add(("line", arc(50, 56, 40, 200, 340), 3.5), PSY, 1.4)
    p.add([ell((28, 30, 72, 76)), poly([(36, 64), (64, 64), (62, 88), (38, 88)])], (226, 220, 204))
    p.add(poly([(56, 32), (72, 50), (70, 70), (62, 88), (56, 88), (64, 62)]), (196, 188, 170), 0)
    p.add([ell((36, 48, 47, 60)), ell((53, 48, 64, 60))], (50, 36, 64), 0)
    p.add([ell((39, 51, 44, 56)), ell((56, 51, 61, 56))], (206, 170, 250), 0)
    p.add(poly([(47, 66), (53, 66), (50, 71)]), (50, 36, 64), 0)
    for x in (42, 47, 53, 58):
        p.line([(x, 78), (x, 86)], 1.2)
    p.add(poly([(28, 34), (30, 14), (39, 26), (50, 8), (61, 26), (70, 14), (72, 34)]), GOLD)
    p.add(poly([(61, 26), (70, 14), (72, 34), (62, 34)]), GOLD_D, 0)
    p.add(ell((46.5, 24, 53.5, 31)), (150, 90, 200), 1.2)
    return p


def nemesis():
    p = Pic()
    for t in ([(38, 62), (34, 74), (37, 84), (32, 94)], [(46, 64), (46, 78), (43, 90)]):
        p.add([tent(t, 6.5, 2.2), tent(mir(t), 6.5, 2.2)], FLAY)
    p.add([ell((26, 14, 74, 60)), poly([(32, 44), (68, 44), (62, 66), (50, 70), (38, 66)])], FLAY)
    p.add(poly([(60, 16), (72, 28), (74, 42), (68, 48), (62, 66), (54, 70), (62, 48), (66, 32)]), FLAY_D, 0)
    p.add([poly([(35, 46), (45, 47), (43, 52), (36, 51)]), poly(mir([(35, 46), (45, 47), (43, 52), (36, 51)]))], EYE, 1.8)
    p.add(tent([(12, 90), (88, 10)], 7, 4), SILVER, 2.2)
    p.add(tent([(14, 88), (86, 12)], 2, 1.2), SILVER_L, 0)
    for x, y in ((30, 72), (50, 50), (70, 28)):
        p.add(star(x, y, 3, .35), PSY, 0)
    p.add(poly([(6, 82), (20, 96), (24, 92), (10, 78)]), SILVER_D, 1.8)
    return p


def snout_icon():
    p = Pic()
    p.add(poly([(18, 30), (82, 30), (90, 58), (80, 84), (20, 84), (10, 58)]), (196, 160, 128))
    p.add(poly([(64, 30), (82, 30), (90, 58), (80, 84), (66, 84), (74, 58)]), (170, 136, 106), 0)
    p.add(poly([(18, 30), (82, 30), (78, 40), (22, 40)]), FUR, 0)
    p.add([ell((24, 50, 42, 66)), ell((58, 50, 76, 66))], (70, 46, 36), 0)
    p.add(("line", arc(50, 72, 14, 10, 170), 4.5), BRASS, 1.8)
    p.add(("line", arc(50, 72, 14, 100, 160), 4.5), BRASS_D, 0)
    return p


# ---------------- 能力图标 ----------------
def bull_rush():
    p = Pic()
    for x, y, r in ((18, 78, 10), (30, 86, 8), (8, 88, 7), (22, 66, 6)):
        p.add(ell((x - r, y - r * .7, x + r, y + r * .7)), (196, 178, 150))
    for y, x0 in ((30, 4), (44, 0), (58, 6)):
        p.add(tent([(x0, y), (x0 + 22, y)], 1.4, 4.6), (224, 210, 186), 1.2)
    p.add(star(78, 46, 18, .3), (250, 214, 120), 2)
    p.add(star(78, 46, 9, .3), (255, 244, 200), 0)
    horn(p, [(40, 50), (52, 42), (66, 40), (78, 44)], 9, 2.4, ow=2.4)
    horn(p, [(36, 44), (44, 32), (56, 26), (68, 26)], 7, 2, ow=2.2, color=HORN_D)
    p.add(poly([(26, 46), (42, 40), (50, 54), (46, 70), (32, 74), (24, 62)]), FUR)
    p.add(poly([(42, 40), (50, 54), (46, 70), (40, 72), (44, 56)]), FUR_D, 0)
    return p


def astral_step_ability():
    p = Pic()
    p.add(ring_e(66, 50, 22, 34, 6, 0), PSY)
    p.add(poly(ellpts(66, 50, 16, 28, 0)), (54, 42, 80), 0)
    for cx, cy, r in ((66, 24, 3), (72, 66, 2.5), (60, 46, 2)):
        p.add(star(cx, cy, r), PSY_L, 0)
    body = [ell((26, 14, 40, 28)), poly([(22, 31), (44, 31), (48, 54), (44, 56), (42, 44), (42, 92), (35, 92), (33, 66), (31, 92), (24, 92), (24, 44), (22, 56), (18, 54)])]
    p.add(body, GITH)
    p.add([poly([(34, 31), (44, 31), (48, 54), (44, 56), (42, 44), (42, 92), (35, 92), (34, 60)])], GITH_D, 0)
    for i, x in enumerate((50, 56)):
        p.add(("line", [(x, 30 + i * 8), (x, 90 - i * 8)], 2.4 - i * .8), PSY_L, 0)
    return p


ART = {
    "MinotaurGith/Xenotypes/Minotaur": minotaur_icon, "MinotaurGith/Xenotypes/Githyanki": githyanki_icon,
    "MinotaurGith/Genes/Charge": charge, "MinotaurGith/Genes/Horns": horns_icon, "MinotaurGith/Genes/Maze": maze,
    "MinotaurGith/Genes/Stature": stature_icon, "MinotaurGith/Genes/Snout": snout_icon,
    "MinotaurGith/Genes/SilverSword": silver_sword_icon, "MinotaurGith/Genes/AstralStep": astral_step,
    "MinotaurGith/Genes/LichDevotee": lich_devotee, "MinotaurGith/Genes/Nemesis": nemesis,
    "MinotaurGith/Abilities/BullRush": bull_rush, "MinotaurGith/Abilities/AstralStep": astral_step_ability,
}
SMALL = {
    "MinotaurGith/Horns/BullHorns_south": horns_south, "MinotaurGith/Horns/BullHorns_east": horns_east, "MinotaurGith/Horns/BullHorns_north": horns_north,
    "MinotaurGith/Snout/BullSnout_south": snout_south, "MinotaurGith/Snout/BullSnout_east": snout_east,
    "MinotaurGith/Weapons/SilverSword": silver_sword,
}

if __name__ == "__main__":
    out = []
    for rel, fn in ART.items():
        out.append(fn().save(rel, 256))
    for rel, fn in SMALL.items():
        out.append(fn().save(rel, 128))
    print("\n".join(out))
