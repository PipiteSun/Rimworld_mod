"""Batch 8: DND pact races (devils, fey, celestials)."""
import math
from geneart import *
from geneart2 import star, tri, bonepiece, heart, BONE
from geneart3 import almond, crescent, zig, drop
from geneart5 import SLATE, SLATE_L, waves, gear, spiral_line
from geneart6 import skull, skull_face, speech, GOLD, DRED, horns
from geneart7 import WHITE, STEEL, WOOD, LEAF, bust, sword, leaf, brain, boot_shape

PURP = (150, 122, 176)
HRED = (166, 62, 56)


def mirror(pts):
    return [(128 - x, y) for x, y in pts]


def feather_wings(cy=64, s=1.0):
    L = [(58, cy + 6), (44, cy - 30), (22, cy - 38), (8, cy - 24), (10, cy - 6), (16, cy + 4), (20, cy + 16), (28, cy + 22), (34, cy + 32), (44, cy + 30), (52, cy + 36)]
    return U(poly(L, sm=True), poly(mirror(L), sm=True))


def bat_wings(cy=60):
    L = [(54, cy + 4), (34, cy - 30), (8, cy - 36), (10, cy - 6), (20, cy - 8), (18, cy + 14), (30, cy + 8), (34, cy + 28), (46, cy + 18)]
    return U(poly(L), poly(mirror(L)))


def butterfly():
    L = [(60, 62), (46, 26), (22, 18), (10, 36), (18, 60), (36, 66), (20, 76), (22, 98), (42, 104), (58, 84)]
    return U(poly(L, sm=True), poly(mirror(L), sm=True))


def halo(cx, cy, rx=20, ry=7, w=5):
    return D(ell(cx, cy, rx, ry), ell(cx, cy, rx - w, ry - w * 0.55))


def amnizu_mind():
    ic = Icon()
    b = brain(64, 64, 2.0)
    ic.add(b, (220, 160, 170), shade=6)
    spiral_line(ic, 64, 64, 34, (134, 100, 166), 5, b, 2.6)
    return ic


def amnizu():
    ic = Icon()
    c = (186, 150, 140)
    ears = U(poly([(48, 44), (22, 30), (46, 58)]), poly([(80, 44), (106, 30), (82, 58)]))
    ic.add(U(bust(64, 112, 1.05), ears, poly([(58, 30), (64, 10), (70, 30)])), c, shade=6)
    eyes(ic, 64, 48, 7, (60, 40, 40))
    return ic


def dread():
    ic = Icon()
    ic.add(star(64, 64, 52, 34, 12), (96, 70, 124), shade=0)
    ic.add(skull(64, 62, 1.2), (232, 226, 206), shade=5)
    skull_face(ic, 64, 62, 1.2)
    return ic


def bone_devil():
    ic = Icon()
    body = I(ell(64, 118, 38, 44), rect(0, 0, 128, 112))
    ic.add(U(body, skull(64, 44, 0.95)), (226, 220, 198), shade=6)
    skull_face(ic, 64, 44, 0.95)
    return ic


def celestial_core():
    ic = Icon()
    ic.add(feather_wings(72), (238, 238, 240), shade=4)
    ic.add(halo(64, 26, 22, 8, 5.5), GOLD, shade=0)
    return ic


def chain(ic, pts, col, r=7):
    for i, (x, y) in enumerate(pts):
        ic.add(D(ell(x, y, r, r * 0.8), ell(x, y, r * 0.45, r * 0.3)), col, shade=0)


def chain_devil():
    ic = Icon()
    ic.add(bust(64, 112, 1.1), (150, 150, 158), shade=6)
    chain(ic, [(34 + i * 12, 100 - i * 7) for i in range(6)], (88, 90, 102), 8)
    return ic


def subvert():
    ic = Icon()
    chain(ic, [(22, 92), (34, 84), (46, 76)], (120, 124, 136), 9)
    ic.add(skull(72, 58, 1.3), (232, 226, 206), shade=5)
    for sx in (-12, 12):
        ic.dots(ell(72 + sx, 55, 8, 9), (50, 44, 50))
        ic.dots(ell(72 + sx, 55, 3.6), (206, 70, 66))
    ic.dots(tri(72, 66, 8, 9), (50, 44, 50))
    return ic


def mask():
    ic = Icon()
    m = poly([(28, 22), (64, 16), (100, 22), (102, 62), (90, 96), (64, 112), (38, 96), (26, 62)], sm=True)
    ic.add(m, (236, 234, 238), shade=0)
    ic.add(I(m, rect(64, 0, 128, 128)), PURP, shade=0)
    for sx in (-17, 17):
        ic.dots(almond(64 + sx, 56, 20, 10), (50, 44, 60))
    return ic


def mace():
    ic = Icon()
    ic.add(stroke([(30, 106), (72, 54)], 8), WOOD, shade=0)
    ic.add(star(82, 42, 30, 18, 8), GOLD, shade=0)
    ic.add(ell(82, 42, 15), lighter(GOLD, 0.2), shade=3)
    return ic


def deva():
    ic = Icon()
    ic.add(feather_wings(62), (232, 214, 150), shade=4)
    ic.add(bust(64, 112, 1.0), (226, 230, 240), shade=5)
    return ic


def dryad_bark():
    ic = Icon()
    sh = shield()
    c = (140, 98, 66)
    ic.add(sh, c, shade=8)
    for pts in ([(46, 22), (42, 60), (50, 96)], [(64, 18), (66, 60), (62, 108)], [(82, 22), (86, 56), (78, 96)]):
        ic.line(pts, 2.8, darker(c, 0.66), clip=sh, sm=True)
    ic.add(leaf(92, 30, 11, 18, 35), LEAF, shade=0)
    return ic


def resolve():
    ic = Icon()
    ic.add(shield(), (72, 62, 84), shade=8)
    b, g, gr = sword(64, 84, 64, 26, 12)
    ic.add(gr, (60, 44, 44), shade=0)
    ic.add(b, (206, 210, 218), shade=0)
    ic.add(g, HRED, shade=0)
    return ic


def erinyes():
    ic = Icon()
    c = (226, 190, 170)
    b = bust(64, 112, 1.1)
    ic.add(b, c, shade=6)
    for pts in ([(50, 86), (60, 96), (54, 108)], [(74, 82), (82, 94)], [(58, 36), (64, 46)]):
        ic.line(pts, 2.8, (176, 70, 66), clip=b, sm=False)
    return ic


def fey_core():
    ic = Icon()
    ic.add(butterfly(), (176, 150, 206), shade=5)
    ic.add(stroke([(64, 40), (64, 96)], 9), (110, 150, 96), shade=0)
    return ic


def pitchfork():
    ic = Icon()
    shaft = stroke([(64, 112), (64, 50)], 8)
    head = U(stroke([(40, 58), (88, 58)], 8), *[stroke([(x, 58), (x, 28)], 8) for x in (40, 64, 88)], *[poly([(x - 9, 30), (x + 9, 30), (x, 10)]) for x in (40, 64, 88)])
    ic.add(U(shaft, head), HRED, shade=3)
    return ic


def horned_devil():
    ic = Icon()
    ic.add(bat_wings(58), (120, 44, 46), shade=0)
    ic.add(horns(64, 36, 13, 22, 6), (70, 30, 34), shade=0)
    ic.add(bust(64, 112, 1.0), HRED, shade=5)
    eyes(ic, 64, 50, 7, EYE)
    return ic


def planetar():
    ic = Icon()
    ic.add(feather_wings(62), (238, 238, 240), shade=4)
    ic.add(bust(64, 112, 1.0), (150, 196, 150), shade=5)
    return ic


def great_sword():
    ic = Icon()
    b, g, gr = sword(42, 86, 100, 22, 16)
    ic.add(gr, WOOD, shade=0)
    ic.add(b, (220, 224, 232), shade=3)
    ic.add(g, GOLD, shade=0)
    ic.add(star(34, 34, 15, 5.5, 4), (238, 220, 130), shade=0)
    return ic


def bloodlust():
    ic = Icon()
    ic.add(U(drop(34, 66, 104, 11), drop(98, 54, 92, 9)), DRED, shade=0)
    ic.add(drop(66, 14, 110, 28), (176, 54, 58), shade=8)
    return ic


def redcap():
    ic = Icon()
    ic.add(ell(60, 84, 26, 26), (206, 170, 140), shade=6)
    cap = U(poly([(30, 68), (52, 22), (92, 30), (108, 54), (92, 46), (90, 68)], sm=True), rect(28, 62, 94, 74, 6))
    ic.add(cap, (176, 54, 58), shade=5)
    for sx in (-9, 9):
        ic.dots(ell(60 + sx, 88, 3.6), (60, 40, 36))
    return ic


def stomp():
    ic = Icon()
    ic.add(U(*[stroke([(x0, 110), (x1, 118)], 6) for x0, x1 in ((30, 16), (64, 64), (98, 112))]), (214, 170, 80), shade=0)
    b = I(boot_shape(), rect(0, 0, 128, 104))
    ic.add(b, (110, 112, 124), shade=5)
    ic.add(I(b, rect(0, 92, 128, 128)), (70, 72, 84), shade=0)
    return ic


def magic_resist():
    ic = Icon()
    ic.add(shield(), (96, 150, 96), shade=8)
    ic.add(star(64, 58, 30, 10, 4), (240, 240, 232), shade=0)
    return ic


def pipes():
    ic = Icon()
    c = (206, 176, 124)
    tubes = U(*[rect(22 + i * 14, 28, 34 + i * 14, 104 - i * 9, 3) for i in range(6)])
    ic.add(tubes, c, shade=0)
    for i in range(1, 6):
        ic.line([(22 + i * 14, 30), (22 + i * 14, 104 - i * 9)], 2, darker(c, 0.66), clip=tubes)
    ic.add(rect(18, 42, 110, 54, 3), WOOD, shade=0)
    return ic


def chalice():
    ic = Icon()
    cup = I(ell(64, 34, 34, 44), rect(0, 34, 128, 128))
    ic.add(U(cup, rect(59, 74, 69, 100, 0), rect(40, 98, 88, 110, 5)), GOLD, shade=6)
    ic.add(ell(64, 34, 34, 9), (120, 50, 86), shade=0)
    return ic


def antler_crown():
    ic = Icon()
    ant = (206, 190, 150)
    a = []
    for s in (-1, 1):
        a.append(stroke([(64 + s * 26, 64), (64 + s * 34, 22)], 7))
        a.append(stroke([(64 + s * 30, 44), (64 + s * 48, 30)], 6))
        a.append(stroke([(64 + s * 32, 32), (64 + s * 20, 16)], 6))
    ic.add(U(*a), ant, shade=0)
    ic.add(poly([(24, 104), (24, 62), (44, 78), (64, 56), (84, 78), (104, 62), (104, 104)]), GOLD, shade=6)
    ic.add(ell(64, 86, 7), (110, 160, 96), shade=0)
    return ic


def crosshair():
    ic = Icon()
    c = (186, 72, 66)
    ring = D(ell(64, 64, 40), ell(64, 64, 28))
    bars = U(rect(58, 12, 70, 44, 0), rect(58, 84, 70, 116, 0), rect(12, 58, 44, 70, 0), rect(84, 58, 116, 70, 0))
    ic.add(U(ring, bars), c, shade=0)
    ic.add(ell(64, 64, 9), c, shade=0)
    return ic


def fey_spear():
    ic = Icon()
    ic.add(stroke([(26, 108), (80, 44)], 7), WOOD, shade=0)
    ic.add(leaf(92, 30, 13, 24, 40), (140, 200, 150), shade=3)
    return ic


Pc = "LichPhylactery/Textures/Pact/Genes/"
ICONS8 = {Pc + k: v for k, v in {
    "AmnizuMind": ("遗忘之触", amnizu_mind), "AmnizuStature": ("阿姆尼祖体型", amnizu), "BoneDevilDread": ("恐惧气场", dread),
    "BoneDevilStature": ("骨魔体型", bone_devil), "CelestialCore": ("天界之躯", celestial_core), "ChainDevilStature": ("链魔体型", chain_devil),
    "ChainDevilSubvert": ("策反骷髅", subvert), "ChangelingFluid": ("流动之貌", mask), "DevaMace": ("光辉之锤", mace),
    "DevaStature": ("狄瓦之躯", deva), "DryadBark": ("树皮之肤", dryad_bark), "ErinyesResolve": ("复仇者的意志", resolve),
    "ErinyesStature": ("艾林魔之躯", erinyes), "FeyCore": ("妖精之躯", fey_core), "HellsCore": ("九狱之躯", pitchfork),
    "HornedDevilStature": ("角魔体型", horned_devil), "PlanetarStature": ("行星使之躯", planetar), "PlanetarSword": ("天界巨剑", great_sword),
    "RedcapBloodlust": ("嗜血", bloodlust), "RedcapStature": ("红帽体型", redcap), "RedcapStomp": ("重踏", stomp),
    "SatyrMagicResist": ("妖精血的抗性", magic_resist), "SatyrMusic": ("林间乐手", pipes), "SatyrRevelry": ("狂欢", chalice),
    "WildHuntLord": ("猎主", antler_crown), "WildHunterPrey": ("猎物", crosshair), "WildHunterSpear": ("妖精长矛", fey_spear),
}.items()}
LOOK8 = ["BoneDevilTail", "ErinyesWings", "SatyrHooves", "SatyrHorns", "WildHunterAntlers"]
