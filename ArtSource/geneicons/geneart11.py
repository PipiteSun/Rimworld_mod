"""Batch 11: last DND icons (troll, necromancy, marilith, ettin, cyclops, divine blood)."""
import math
from geneart import *
from geneart2 import star, tri, bonepiece, heart, BONE
from geneart3 import almond, crescent, zig, drop
from geneart5 import SLATE, SLATE_L, waves, gear, spiral_line
from geneart6 import skull, skull_face, speech, GOLD, DRED, horns
from geneart7 import WHITE, STEEL, WOOD, LEAF, ROCK, bust, sword, leaf, brain, gob_head
from geneart8 import HRED, halo, feather_wings, PURP

TEAL = (74, 140, 132)
TROLL = (120, 150, 86)
HEADC = (236, 236, 236)


def troll():
    ic = Icon()
    ears = U(poly([(40, 50), (8, 30), (38, 70)]), poly([(88, 50), (120, 30), (90, 70)]))
    ic.add(U(ears, ell(64, 62, 30, 36), I(ell(64, 124, 44, 30), rect(0, 0, 128, 112))), TROLL, shade=6)
    for sx in (-11, 11):
        ic.dots(ell(64 + sx, 54, 4.6, 3.6), (236, 208, 96))
        ic.add(tri(64 + sx * 1.1, 86, 9, -18), (240, 236, 222), shade=0)
    return ic


def regeneration():
    ic = Icon()
    ic.add(heart(64, 60, 40), (110, 150, 96), shade=7)
    ic.add(U(rect(56, 38, 72, 82, 2), rect(42, 52, 86, 68, 2)), (236, 240, 228), shade=0)
    return ic


def lich_core():
    ic = Icon()
    ic.add(rect(52, 12, 76, 28, 4), GOLD, shade=3)
    orb = ell(64, 70, 40, 42)
    ic.add(orb, (112, 84, 150), shade=7)
    ic.add(skull(64, 68, 0.9), (232, 226, 206), shade=0)
    skull_face(ic, 64, 68, 0.9, (70, 44, 100))
    return ic


def necrotic_body():
    ic = Icon()
    c = (214, 216, 196)
    ribs = I(ell(64, 100, 34, 34), rect(0, 0, 128, 112))
    ic.add(U(skull(64, 40, 0.9), rect(60, 58, 68, 110, 0), ribs), c, shade=5)
    skull_face(ic, 64, 40, 0.9)
    hole = (88, 96, 84)
    for y in (78, 90, 102):
        for s in (-1, 1):
            ic.dots(I(stroke([(64 + s * 8, y), (64 + s * 26, y + 3)], 5), ribs), hole)
    return ic


def necrotic_touch():
    ic = Icon()
    c = (150, 170, 140)
    palm = rect(40, 60, 88, 110, 14)
    fingers = U(*[rect(x, top, x + 10, 76, 5) for x, top in ((41, 34), (53, 24), (65, 22), (77, 30))])
    thumb = stroke([(42, 88), (26, 70)], 11)
    ic.add(U(palm, fingers, thumb), c, shade=5)
    d = ell(64, 86, 13)
    ic.add(d, (84, 58, 110), shade=0)
    spiral_line(ic, 64, 86, 11, (176, 150, 214), 2.6, d, 2)
    return ic


def blade_storm():
    ic = Icon()
    blades = U(*[poly([(64 + 12 * math.cos(math.radians(a + 90)), 64 + 12 * math.sin(math.radians(a + 90))), (64 + 52 * math.cos(math.radians(a - 14)), 64 + 52 * math.sin(math.radians(a - 14))), (64 + 12 * math.cos(math.radians(a - 60)), 64 + 12 * math.sin(math.radians(a - 60)))]) for a in range(0, 360, 60)])
    ic.add(blades, (196, 200, 210), shade=0)
    ic.add(ell(64, 64, 15), GOLD, shade=3)
    ic.add(ell(64, 64, 6), (96, 100, 112), shade=0)
    return ic


def blades():
    ic = Icon()
    bl, gd, gp = [], [], []
    for x1, y1 in ((24, 30), (64, 12), (104, 30)):
        x0, y0 = 64 + (x1 - 64) * 0.18, 84 - (84 - y1) * 0.12
        b, g, gr = sword(x0, y0, x1, y1, 11)
        bl.append(b); gd.append(g); gp.append(gr)
    ic.add(U(*gp, stroke([(64, 84), (64, 108)], 8)), WOOD, shade=0)
    ic.add(U(*bl), (206, 210, 218), shade=0)
    ic.add(U(*gd), GOLD, shade=0)
    return ic


def constrict():
    ic = Icon()
    coils = U(*[ell(60, y, 34 - i * 3, 11) for i, y in enumerate((98, 80, 62))])
    ic.add(coils, TEAL, shade=0)
    for y in (89, 71):
        ic.line([(30, y), (60, y + 5), (90, y)], 2.6, darker(TEAL, 0.62), clip=coils, sm=True)
    head = U(stroke([(72, 56), (86, 36)], 14), ell(94, 30, 15, 11))
    ic.add(head, lighter(TEAL, 0.08), shade=3)
    ic.dots(ell(98, 28, 3), (236, 208, 96))
    return ic


def glide():
    ic = Icon()
    body = stroke(smooth([(26, 104), (60, 108), (84, 90), (62, 66), (44, 46), (62, 26), (88, 24)], closed=False) + [(88, 24)], 15)
    ic.add(U(body, ell(94, 24, 15, 11)), TEAL, shade=4)
    ic.dots(ell(98, 22, 3), (236, 208, 96))
    return ic


def two_heads(ic, cy=62):
    ic.add(poly([(50, cy + 28), (78, cy + 28), (82, cy + 54), (46, cy + 54)]), HEADC, shade=0)
    for cx in (44, 84):
        ic.add(ell(cx, cy, 19, 23), HEADC, shade=5)


def quarrel():
    ic = Icon()
    two_heads(ic, 66)
    dk = (110, 110, 118)
    ic.line([(32, 60), (44, 66)], 3.2, dk)
    ic.line([(96, 60), (84, 66)], 3.2, dk)
    ic.dots(ell(40, 70, 3.2), (60, 60, 66)); ic.dots(ell(88, 70, 3.2), (60, 60, 66))
    ic.add(star(64, 26, 20, 9, 8), (206, 84, 62), shade=0)
    return ic


def watch():
    ic = Icon()
    two_heads(ic, 62)
    ic.add(ell(44, 62, 8, 7), (120, 160, 200), shade=0)
    ic.dots(ell(44, 62, 3.4), (30, 40, 60))
    ic.line([(74, 62), (84, 66), (94, 62)], 3.2, (110, 110, 118), sm=True)
    return ic


def twin_grip():
    ic = Icon()
    b1, g1, r1 = sword(40, 86, 96, 20, 13)
    b2, g2, r2 = sword(88, 86, 32, 20, 13)
    ic.add(U(r1, r2), WOOD, shade=0)
    ic.add(b2, (176, 180, 190), shade=0)
    ic.add(b1, (212, 216, 224), shade=0)
    ic.add(U(g1, g2), GOLD, shade=0)
    return ic


def craft():
    ic = Icon()
    ic.add(stroke([(34, 108), (76, 52)], 10), WOOD, shade=0)
    head = stroke([(52, 26), (104, 62)], 30)
    ic.add(head, (150, 154, 164), shade=5)
    ic.add(ell(78, 44, 10, 8), (236, 232, 222), shade=0)
    ic.dots(ell(78, 44, 4), (70, 100, 150))
    return ic


def flat_sight():
    ic = Icon()
    eye = I(almond(64, 60, 100, 68), rect(0, 60, 128, 128))
    ic.add(U(eye, rect(14, 52, 114, 62, 4)), (232, 230, 222), shade=0)
    ic.add(I(ell(64, 62, 24), rect(0, 62, 128, 128)), (104, 136, 190), shade=0)
    ic.dots(I(ell(64, 62, 10), rect(0, 62, 128, 128)), (30, 40, 60))
    ic.add(rect(14, 50, 114, 62, 4), (176, 128, 106), shade=0)
    return ic


def sheep():
    ic = Icon()
    ic.add(U(rect(40, 84, 50, 108, 3), rect(76, 84, 86, 108, 3)), (90, 86, 92), shade=0)
    ic.add(U(*[ell(x, y, r) for x, y, r in ((44, 62, 18), (64, 52, 20), (84, 62, 18), (52, 78, 17), (76, 78, 17))]), (238, 238, 236), shade=5)
    ic.add(U(ell(98, 58, 13, 15), ell(86, 48, 7, 5)), (90, 86, 92), shade=3)
    ic.dots(ell(100, 56, 2.8), (236, 232, 222))
    return ic


def divine_blood():
    ic = Icon()
    ic.add(drop(64, 30, 116, 30), (214, 176, 80), shade=8)
    ic.add(halo(64, 18, 26, 9, 6), (240, 222, 150), shade=0)
    return ic


T = "LichPhylactery/Textures/"
ICONS11 = {T + k: v for k, v in {
    "Troll/TrollAppearance": ("巨魔巨躯与獠牙", troll), "Troll/TrollRegeneration": ("巨魔再生", regeneration),
    "Necromancy/Icons/LichCore": ("巫妖魂核", lich_core), "Necromancy/Icons/NecroticBody": ("死灵之躯", necrotic_body),
    "Necromancy/Icons/NecroticTouch": ("死灵之触", necrotic_touch),
    "Marilith/Genes/BladeStorm": ("剑刃风暴", blade_storm), "Marilith/Genes/Blades": ("六臂连击", blades),
    "Marilith/Genes/Constrict": ("缠绕", constrict), "Marilith/Genes/Glide": ("蛇行", glide),
    "HARGiants/Ettin/Genes/Quarrel": ("两头不和", quarrel), "HARGiants/Ettin/Genes/TwinGrip": ("左右开弓", twin_grip),
    "HARGiants/Ettin/Genes/Watch": ("轮流值守", watch),
    "HARGiants/Cyclops/Genes/Craft": ("独眼匠心", craft), "HARGiants/Cyclops/Genes/FlatSight": ("单眼视界", flat_sight),
    "HARGiants/Cyclops/Genes/Shepherd": ("牧者", sheep), "Ascension/Genes/DivineBlood": ("觉醒神血", divine_blood),
}.items()}
