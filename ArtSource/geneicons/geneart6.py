"""Batch 6: DND Abyss demons + remaining giants."""
import math
from geneart import *
from geneart2 import star, tri, bonepiece, heart, BONE
from geneart3 import almond, crescent, zig, drop
from geneart5 import SLATE, SLATE_L, waves

DRED = (150, 58, 58)
OLIVE = (136, 140, 86)
GOLD = (208, 174, 86)
APE = (176, 108, 62)


def horns(cx, y, spread, h, w=9):
    ms = []
    for s in (-1, 1):
        ms.append(poly([(cx + s * (spread - w), y), (cx + s * (spread + w * 0.4), y - h * 0.5), (cx + s * (spread - w * 0.2), y - h), (cx + s * (spread + w), y - h * 0.3), (cx + s * (spread + w * 0.6), y + 4)], sm=True))
    return U(*ms)


def skull(cx, cy, s=1.0):
    return U(ell(cx, cy - 4 * s, 22 * s, 20 * s), rect(cx - 13 * s, cy + 6 * s, cx + 13 * s, cy + 24 * s, 4 * s))


def skull_face(ic, cx, cy, s=1.0, col=(50, 44, 50)):
    for sx in (-9, 9):
        ic.dots(ell(cx + sx * s, cy - 2 * s, 6 * s, 7 * s), col)
    ic.dots(tri(cx, cy + 6 * s, 6 * s, 7 * s), col)


def speech(cx=64, cy=56):
    return U(ell(cx, cy, 44, 34), poly([(cx - 26, cy + 22), (cx - 34, cy + 54), (cx - 2, cy + 30)]))


def abyss_undying():
    ic = Icon()
    eye = almond(64, 64, 100, 64)
    ic.add(eye, (226, 206, 150), shade=0)
    ic.add(ell(64, 64, 22), (176, 66, 58), shade=5)
    ic.dots(ell(64, 64, 6, 18), (40, 16, 18))
    return ic


def fist():
    ic = Icon()
    hand = U(rect(30, 44, 98, 100, 12), *[ell(40 + i * 16, 44, 9, 8) for i in range(4)])
    ic.add(hand, APE, shade=6)
    ic.add(rect(22, 62, 66, 84, 10), lighter(APE, 0.12), shade=0)
    for i in range(3):
        ic.line([(48 + i * 16, 40), (48 + i * 16, 58)], 2.6, darker(APE, 0.66), clip=hand)
    return ic


def flexed_arm():
    ic = Icon()
    arm = U(ell(54, 62, 30, 26), rect(24, 70, 104, 106, 14), stroke([(78, 62), (88, 34)], 22), ell(88, 28, 14, 12))
    ic.add(arm, APE, shade=6)
    return ic


def ape_face():
    ic = Icon()
    ic.add(U(ell(64, 62, 44, 42), ell(22, 58, 10, 12), ell(106, 58, 10, 12)), APE, shade=6)
    ic.add(U(ell(64, 74, 30, 26), ell(50, 54, 14, 12), ell(78, 54, 14, 12)), (220, 178, 128), shade=0)
    for sx in (-14, 14):
        ic.dots(ell(64 + sx, 56, 4.4), (50, 34, 26))
        ic.add(tri(64 + sx * 0.8, 82, 9, 16), (244, 240, 226), shade=0)
    return ic


def still_hidden():
    ic = Icon()
    fig = U(ell(64, 40, 20, 22), I(ell(64, 112, 42, 52), rect(0, 0, 128, 110)))
    stripes = U(rect(0, 0, 128, 74), *[rect(0, y, 128, y + 7) for y in (80, 92, 104)])
    ic.add(I(fig, stripes), (70, 60, 78), shade=5)
    eyes(ic, 64, 42, 8, (226, 150, 70))
    return ic


def cambion_blood():
    ic = Icon()
    ic.add(horns(64, 50, 22, 34), (90, 36, 44), shade=0)
    ic.add(drop(64, 20, 112, 32), DRED, shade=8)
    return ic


def presence():
    ic = Icon()
    ic.add(horns(64, 30, 26, 24), DRED, shade=0)
    ic.add(speech(64, 60), (234, 232, 228), shade=6)
    ic.add(U(poly([(57, 34), (71, 34), (67, 66), (61, 66)]), ell(64, 78, 5.5)), (176, 60, 58), shade=0)
    return ic


def dretch():
    ic = Icon()
    c = (128, 132, 100)
    ic.add(horns(64, 50, 14, 16, 6), (86, 88, 70), shade=0)
    body = I(ell(64, 106, 46, 36), rect(0, 0, 128, 110))
    ic.add(U(ell(64, 62, 22, 20), body), c, shade=6)
    eyes(ic, 64, 64, 9, (226, 200, 96))
    return ic


def dretch_stench():
    ic = Icon()
    ic.add(U(*[stroke(zig(x, 16, 82, 7, 3), 7) for x in (36, 64, 92)]), OLIVE, shade=0)
    ic.add(ell(64, 100, 16, 11), (110, 112, 84), shade=3)
    return ic


def pickaxe():
    ic = Icon()
    ic.add(stroke([(40, 108), (86, 34)], 9), (150, 104, 70), shade=0)
    head = D(ell(78, 62, 54, 50), ell(74, 76, 56, 50))
    ic.add(I(head, rect(20, 0, 128, 70)), (150, 154, 164), shade=3)
    return ic


def hezrou_hide():
    ic = Icon()
    sh = shield()
    ic.add(sh, OLIVE, shade=8)
    for x, y, r in ((50, 40, 7), (78, 36, 5), (66, 60, 8), (44, 68, 5), (84, 66, 6), (62, 88, 6)):
        ic.dots(I(ell(x, y, r), sh), darker(OLIVE, 0.72))
    return ic


def frog():
    ic = Icon()
    g = (140, 150, 84)
    ic.add(U(ell(64, 76, 46, 32), ell(40, 44, 17), ell(88, 44, 17)), g, shade=6)
    for cx in (40, 88):
        ic.add(ell(cx, 44, 9), (232, 224, 170), shade=0)
        ic.dots(ell(cx, 44, 6, 3), (40, 44, 26))
    ic.line([(34, 86), (50, 94), (78, 94), (94, 86)], 3.2, darker(g, 0.6), sm=True)
    return ic


def toxic_cloud():
    ic = Icon()
    c = cloud(62, 66, 1.45)
    ic.add(c, (150, 178, 96), shade=6)
    dk = (70, 86, 50)
    ic.dots(ell(50, 68, 7, 9), dk)
    ic.dots(ell(76, 68, 7, 9), dk)
    return ic


def devour_soul():
    ic = Icon()
    ic.add(D(ell(64, 64, 48), ell(64, 64, 36)), (134, 100, 166), shade=0)
    ic.add(skull(64, 60, 1.15), (232, 226, 206), shade=5)
    skull_face(ic, 64, 60, 1.15)
    return ic


def nabassu():
    ic = Icon()
    wings = U(poly([(48, 60), (8, 26), (14, 60), (24, 58), (26, 78), (38, 72), (48, 90)]), poly([(80, 60), (120, 26), (114, 60), (104, 58), (102, 78), (90, 72), (80, 90)]))
    ic.add(wings, (100, 100, 110), shade=0)
    ic.add(U(ell(64, 46, 18, 20), I(ell(64, 114, 34, 44), rect(0, 0, 128, 110))), (156, 156, 164), shade=6)
    return ic


def shadow_form():
    ic = Icon()
    body = U(ell(64, 50, 34, 36), rect(30, 50, 98, 100, 0), *[ell(40 + i * 16, 100, 8, 10) for i in range(4)])
    ic.add(body, (64, 52, 78), shade=6)
    for sx in (-12, 12):
        ic.dots(ell(64 + sx, 52, 6, 8), (176, 150, 214))
    return ic


def dream():
    ic = Icon()
    ic.add(crescent(54, 60, 44), (226, 214, 150), shade=6)
    ic.add(heart(86, 84, 17), (206, 88, 104), shade=3)
    return ic


def succubus_nature():
    ic = Icon()
    ic.add(horns(64, 40, 28, 28), (90, 36, 44), shade=0)
    ic.add(heart(64, 64, 38), (206, 104, 112), shade=7)
    return ic


def gift():
    ic = Icon()
    blue = (96, 124, 186)
    ic.add(U(ell(52, 26, 12, 9), ell(76, 26, 12, 9)), (234, 234, 238), shade=0)
    ic.add(U(rect(26, 50, 102, 108, 4), rect(20, 36, 108, 56, 4)), blue, shade=6)
    ic.add(rect(57, 34, 71, 108, 0), (234, 234, 238), shade=0)
    return ic


def grace():
    ic = Icon()
    ic.add(speech(64, 56), (234, 232, 228), shade=6)
    ic.add(star(64, 56, 26, 9, 4), GOLD, shade=0)
    return ic


def noble_heart():
    ic = Icon()
    crown = poly([(22, 96), (22, 38), (44, 60), (64, 26), (84, 60), (106, 38), (106, 96)])
    ic.add(crown, GOLD, shade=6)
    ic.add(heart(64, 74, 13), (190, 74, 80), shade=0)
    return ic


def fire_frailty():
    ic = Icon()
    ic.add(flame(64, 112, 76, 98, lean=3), (206, 118, 62), shade=6)
    snowflake(ic, 64, 78, 17, 3.6, (214, 236, 248))
    return ic


def loot():
    ic = Icon()
    coins = U(*[U(ell(64, y, 36, 12), rect(28, y, 100, y + 10, 0), ell(64, y + 10, 36, 12)) for y in (84, 62, 40)])
    ic.add(coins, GOLD, shade=6)
    for y in (40, 62, 84):
        ic.line([(28 + 36 - 36 * math.cos(math.radians(a)), y + 10 + 12 * math.sin(math.radians(a))) for a in range(0, 181, 12)], 2.4, darker(GOLD, 0.68), clip=coins)
    ic.add(ell(64, 39, 22, 6), lighter(GOLD, 0.3), shade=0)
    return ic


def snow_walker():
    ic = Icon()
    ice = (190, 214, 232)
    foot = U(ell(60, 54, 24, 30), ell(62, 96, 17, 16), rect(48, 60, 78, 96, 0))
    toes = U(*[ell(x, y, r) for x, y, r in ((38, 22, 7), (52, 14, 7.5), (67, 13, 7.5), (81, 19, 7), (92, 30, 6))])
    ic.add(U(foot, toes), ice, shade=5)
    return ic


def storm_child():
    ic = Icon()
    ic.add(U(*[stroke([(x, 82), (x - 8, 108)], 7) for x in (44, 66, 88)]), (120, 164, 206), shade=0)
    ic.add(cloud(62, 48, 1.35), (150, 154, 170), shade=6)
    return ic


def melancholy():
    ic = Icon()
    ic.add(drop(64, 70, 116, 15), (120, 164, 206), shade=4)
    ic.add(cloud(62, 46, 1.35), (150, 154, 170), shade=6)
    return ic


def omen():
    ic = Icon()
    ic.add(almond(64, 64, 100, 62), (232, 230, 222), shade=0)
    ic.add(ell(64, 64, 24), (74, 136, 140), shade=5)
    ic.add(bolt(0, 0, 0.36), (232, 200, 92), shade=0)
    return ic


A = "LichPhylactery/Textures/Abyss/Genes/"
Gi = "LichPhylactery/Textures/Giants/Genes/"
ICONS6 = {
    A + "AbyssUndying": ("不吃不睡", abyss_undying), A + "BarlguraBrute": ("嗜杀", fist), A + "BarlguraMight": ("猿力", flexed_arm),
    A + "BarlguraStature": ("巴路古拉体型", ape_face), A + "BarlguraStill": ("静立隐形", still_hidden), A + "CambionBlood": ("深渊血统", cambion_blood),
    A + "CambionPresence": ("深渊气魄", presence), A + "DretchStature": ("劣魔体型", dretch), A + "DretchStench": ("劣魔恶臭", dretch_stench),
    A + "DretchToil": ("苦力", pickaxe), A + "HezrouHide": ("疣皮", hezrou_hide), A + "HezrouStature": ("哈兹魔体型", frog),
    A + "HezrouStench": ("哈兹魔恶臭", toxic_cloud), A + "NabassuDevour": ("噬魂", devour_soul), A + "NabassuStature": ("纳巴苏体型", nabassu),
    A + "ShadowDemonForm": ("无形之影", shadow_form), A + "SuccubusDream": ("梦境吸取", dream), A + "SuccubusNature": ("魅魔之性", succubus_nature),
    Gi + "CloudGift": ("赠礼之风", gift), Gi + "CloudGrace": ("优雅者", grace), Gi + "CloudNoble": ("贵胄之心", noble_heart),
    Gi + "FrostGiantFrailty": ("畏火", fire_frailty), Gi + "FrostLootHeart": ("战利品之心", loot), Gi + "FrostSnowWalker": ("雪原行者", snow_walker),
    Gi + "StormChild": ("风暴之子", storm_child), Gi + "StormMelancholy": ("忧郁先知", melancholy), Gi + "StormOmen": ("风暴预兆", omen),
}
