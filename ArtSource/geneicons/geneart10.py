"""Batch 10: DND Faith races, Minotaur/Gith, Oozefolk."""
import math
from geneart import *
from geneart2 import star, tri, bonepiece, heart, BONE
from geneart3 import almond, crescent, zig, drop
from geneart5 import SLATE, SLATE_L, waves, gear, spiral_line
from geneart6 import skull, skull_face, speech, GOLD, DRED, horns
from geneart7 import WHITE, STEEL, WOOD, LEAF, ROCK, bust, sword, leaf, brain
from geneart8 import HRED, halo, feather_wings, PURP

OOZE = (150, 178, 76)
BULL = (140, 96, 66)
HORN = (232, 226, 206)


def blob(cx=64, base=104, w=84, h=72, bumps=4):
    top = base - h
    dome = U(ell(cx, top + w * 0.42, w / 2, w * 0.42), rect(cx - w / 2, top + w * 0.42, cx + w / 2, base - 6, 0))
    bw = w / bumps
    feet = U(*[ell(cx - w / 2 + bw * (i + 0.5), base - 6, bw / 2, 7) for i in range(bumps)])
    return U(dome, feet)


def shine(ic, x, y, r=7, col=(206, 226, 140)):
    ic.dots(ell(x, y, r * 0.7, r), col)


def bull_horns(cx, y, spread=22, w=8):
    return U(*[stroke(smooth([(cx + s * (spread - 6), y), (cx + s * (spread + 14), y - 2), (cx + s * (spread + 20), y - 22)], closed=False) + [(cx + s * (spread + 20), y - 22)], w) for s in (-1, 1)])


def lineage():
    ic = Icon()
    f = poly([(40, 112), (40, 82), (50, 56), (66, 38), (84, 34), (88, 50), (80, 76), (62, 98)], sm=True)
    ic.add(f, (238, 238, 240), shade=5)
    ic.line([(42, 110), (82, 40)], 2.6, (176, 178, 190), clip=f)
    ic.add(halo(64, 20, 24, 9, 6), GOLD, shade=0)
    return ic


def cel_resist():
    ic = Icon()
    ic.add(shield(), (232, 232, 236), shade=8)
    ic.add(star(64, 58, 28, 19, 10), GOLD, shade=0)
    ic.add(ell(64, 58, 13), (240, 222, 150), shade=0)
    return ic


def devil_tail():
    ic = Icon()
    t = stroke(smooth([(30, 108), (60, 104), (76, 82), (62, 60), (74, 40), (90, 34)], closed=False) + [(90, 34)], 10)
    tip = poly([(84, 14), (110, 26), (92, 48), (94, 34), (80, 32)])
    ic.add(U(t, tip), HRED, shade=3)
    return ic


def healing_hands():
    ic = Icon()
    c = (226, 190, 160)
    palm = rect(40, 60, 88, 110, 14)
    fingers = U(*[rect(x, top, x + 10, 76, 5) for x, top in ((41, 34), (53, 24), (65, 22), (77, 30))])
    thumb = stroke([(42, 88), (26, 70)], 11)
    ic.add(U(palm, fingers, thumb), c, shade=5)
    ic.add(star(64, 84, 18, 6.5, 4), (240, 214, 110), shade=0)
    return ic


def rebuke():
    ic = Icon()
    ic.add(star(64, 64, 54, 34, 10), (96, 50, 56), shade=0)
    ic.add(flame(64, 100, 52, 70, lean=2), (206, 100, 56), shade=4)
    ic.add(flame(64, 100, 26, 36, lean=1), (236, 180, 96), shade=0)
    return ic


def pact():
    ic = Icon()
    paper = (226, 206, 160)
    ic.add(rect(28, 30, 100, 98, 2), paper, shade=0)
    for y in (48, 60, 72):
        ic.line([(42, y), (86, y)], 2.6, darker(paper, 0.7))
    ic.add(U(rect(22, 20, 106, 34, 7), rect(22, 94, 106, 108, 7)), darker(paper, 0.82), shade=3)
    ic.add(ell(84, 86, 12), (176, 54, 58), shade=3)
    return ic


def inf_resist():
    ic = Icon()
    ic.add(shield(), (88, 62, 70), shade=8)
    ic.add(flame(64, 92, 46, 62, lean=2), (206, 100, 56), shade=0)
    ic.add(flame(64, 92, 22, 30, lean=1), (236, 180, 96), shade=0)
    return ic


def shroud():
    ic = Icon()
    body = U(ell(64, 50, 34, 36), rect(30, 50, 98, 100, 0), *[tri(38 + i * 17.3, 100, 17.4, 12) for i in range(4)])
    ic.add(body, (104, 84, 130), shade=6)
    ic.add(ell(64, 54, 20, 24), (48, 38, 60), shade=0)
    for sx in (-8, 8):
        ic.dots(ell(64 + sx, 52, 4, 5.5), (170, 214, 110))
    return ic


def consumption():
    ic = Icon()
    ic.add(star(64, 64, 54, 36, 12), (214, 170, 70), shade=0)
    ic.add(ell(64, 64, 32), (236, 208, 110), shade=0)
    ic.add(flame(64, 90, 40, 54, lean=2), (214, 116, 56), shade=0)
    return ic


def radiant_soul():
    ic = Icon()
    ic.add(feather_wings(64), (232, 208, 130), shade=4)
    ic.add(star(64, 64, 24, 8, 4), (250, 248, 236), shade=0)
    return ic


def astral_step():
    ic = Icon()
    d = ell(64, 64, 36, 48)
    ic.add(d, (112, 84, 150), shade=0)
    inner = ell(64, 64, 26, 38)
    ic.add(inner, (56, 42, 84), shade=0)
    spiral_line(ic, 64, 64, 30, (176, 150, 214), 4, inner, 2.4)
    return ic


def bull_face(ic, cx, cy, s=1.0):
    ic.add(bull_horns(cx, cy - 16 * s, 16 * s, 8 * s), HORN, shade=0)
    ic.add(U(ell(cx, cy, 26 * s, 26 * s), ell(cx - 26 * s, cy - 8 * s, 9 * s, 6 * s), ell(cx + 26 * s, cy - 8 * s, 9 * s, 6 * s)), BULL, shade=5)
    ic.add(ell(cx, cy + 14 * s, 16 * s, 11 * s), (206, 160, 130), shade=0)
    for sx in (-6, 6):
        ic.dots(ell(cx + sx * s, cy + 14 * s, 2.6 * s, 3.4 * s), (70, 44, 36))
    for sx in (-11, 11):
        ic.dots(ell(cx + sx * s, cy - 6 * s, 3.2 * s), (40, 28, 24))


def charge():
    ic = Icon()
    ic.add(U(*[stroke([(8, y), (28, y)], 6) for y in (52, 68, 84)]), (214, 218, 226), shade=0)
    bull_face(ic, 78, 68, 1.05)
    return ic


def minotaur():
    ic = Icon()
    body = I(ell(64, 122, 44, 42), rect(0, 0, 128, 112))
    ic.add(body, darker(BULL, 0.85), shade=5)
    bull_face(ic, 64, 54, 0.95)
    return ic


def devotee():
    ic = Icon()
    ic.add(skull(64, 74, 1.25), (232, 226, 206), shade=5)
    skull_face(ic, 64, 74, 1.25)
    ic.add(poly([(34, 52), (34, 20), (48, 34), (64, 14), (80, 34), (94, 20), (94, 52)]), GOLD, shade=4)
    ic.add(ell(64, 40, 6), PURP, shade=0)
    return ic


def maze():
    ic = Icon()
    c = (206, 176, 124)
    sq = rect(20, 20, 108, 108, 6)
    ic.add(sq, c, shade=6)
    pts = [(34, 34), (94, 34), (94, 94), (34, 94), (34, 52), (76, 52), (76, 76), (52, 76), (52, 64)]
    ic.line(pts, 5.5, darker(c, 0.6))
    return ic


def nemesis():
    ic = Icon()
    from geneart import tentacles
    ic.add(U(ell(64, 50, 26, 28), tentacles(64, 66, 4, 46, 40, 10)), PURP, shade=5)
    for sx in (-10, 10):
        ic.dots(ell(64 + sx, 50, 4), (240, 236, 244))
    ic.add(stroke([(26, 100), (102, 24)], 9), (190, 66, 62), shade=0)
    return ic


def silver_sword():
    ic = Icon()
    ic.add(arc(64, 70, 40, 20, 200, 7), PURP, shade=0)
    b, g, gr = sword(44, 84, 100, 22, 15)
    ic.add(gr, (70, 60, 90), shade=0)
    ic.add(b, (226, 230, 238), shade=3)
    ic.add(g, STEEL, shade=0)
    return ic


def amorphous():
    ic = Icon()
    pud = poly([(12, 84), (24, 62), (44, 58), (56, 44), (76, 48), (88, 62), (108, 64), (118, 84), (104, 100), (72, 96), (56, 106), (30, 100)], sm=True)
    ic.add(pud, OOZE, shade=5)
    shine(ic, 50, 66, 6)
    return ic


def chill():
    ic = Icon()
    ic.add(blob(), (160, 200, 170), shade=6)
    snowflake(ic, 64, 68, 20, 3.6, (240, 248, 250))
    return ic


def desiccate():
    ic = Icon()
    c = (166, 156, 100)
    b = blob(64, 104, 84, 62)
    ic.add(b, c, shade=6)
    for pts in ([(64, 44), (58, 62), (68, 76), (60, 98)], [(58, 62), (40, 70)], [(68, 76), (88, 82)]):
        ic.line(pts, 3, darker(c, 0.6), clip=b)
    return ic


def digest():
    ic = Icon()
    ic.add(blob(), OOZE, shade=6)
    ic.add(bonepiece(48, 86, 80, 62, 6, 5), (236, 232, 214), shade=0)
    return ic


def ooze_core():
    ic = Icon()
    ic.add(blob(), OOZE, shade=6)
    for sx in (-12, 12):
        ic.dots(ell(64 + sx, 72, 5, 7), (50, 60, 30))
    return ic


def protoplasm():
    ic = Icon()
    ic.add(drop(64, 12, 114, 32), OOZE, shade=8)
    shine(ic, 52, 84, 8)
    return ic


def pseudopod():
    ic = Icon()
    arm = stroke(smooth([(72, 70), (82, 44), (98, 28), (110, 18)], closed=False) + [(110, 18)], 13)
    ic.add(U(blob(54, 106, 76, 60), arm), OOZE, shade=6)
    return ic


def sheen():
    ic = Icon()
    ic.add(blob(), OOZE, shade=6)
    ic.add(star(64, 66, 24, 8, 4), (244, 248, 226), shade=0)
    return ic


def split():
    ic = Icon()
    ic.add(U(blob(46, 104, 62, 66, 3), blob(92, 104, 44, 40, 2)), OOZE, shade=6)
    shine(ic, 36, 62, 6)
    return ic


F = "LichPhylactery/Textures/Faith/Genes/"
M = "LichPhylactery/Textures/MinotaurGith/Genes/"
O = "LichPhylactery/Textures/Oozefolk/Genes/"
ICONS10 = {
    F + "CelestialLineage": ("天界之裔", lineage), F + "CelestialResistance": ("天界抗性", cel_resist), F + "FiendishBlood": ("魔鬼血统", devil_tail),
    F + "HealingHands": ("治疗之手", healing_hands), F + "HellishRebuke": ("地狱叱喝", rebuke), F + "InfernalPact": ("魔鬼契约", pact),
    F + "InfernalResistance": ("地狱抗性", inf_resist), F + "NecroticShroud": ("亡者面纱", shroud), F + "RadiantConsumption": ("光耀焚身", consumption),
    F + "RadiantSoul": ("光辉之魂", radiant_soul),
    M + "AstralStep": ("星界跳跃", astral_step), M + "Charge": ("蛮牛冲撞", charge), M + "LichDevotee": ("巫妖女王的信徒", devotee),
    M + "Maze": ("迷宫感", maze), M + "Nemesis": ("宿敌", nemesis), M + "SilverSword": ("银剑", silver_sword), M + "Stature": ("牛头人体型", minotaur),
    O + "Amorphous": ("无定形", amorphous), O + "Chill": ("凝滞", chill), O + "Desiccate": ("怕干", desiccate), O + "Digest": ("万物消化", digest),
    O + "OozeCore": ("流浆之躯", ooze_core), O + "Protoplasm": ("原生质", protoplasm), O + "Pseudopod": ("伪足", pseudopod),
    O + "Sheen": ("流光", sheen), O + "Split": ("分裂繁殖", split),
}
LOOK10 = ["Faith/Genes/Halo", "MinotaurGith/Genes/Horns", "MinotaurGith/Genes/Snout"]
