"""Batch 9: DND Races124 (aarakocra, balor, genasi, genie, ice devil, loxodon, ogre, titan)."""
import math
from geneart import *
from geneart2 import star, tri, bonepiece, heart, BONE
from geneart3 import almond, crescent, zig, drop
from geneart5 import SLATE, SLATE_L, waves, gear, spiral_line
from geneart6 import skull, skull_face, speech, GOLD, DRED, horns
from geneart7 import WHITE, STEEL, WOOD, LEAF, ROCK, bust, sword, leaf, brain, rock_shape
from geneart8 import HRED, halo, feather_wings

ICE = (176, 206, 226)
WATER = (104, 146, 200)
EARTH = (150, 112, 84)
FIRE = (210, 122, 60)
AIR = (176, 206, 226)


def hollow_bones():
    ic = Icon()
    b = bonepiece(34, 96, 86, 40, 16, 11)
    ic.add(b, BONE, shade=5)
    ic.add(ell(94, 32, 9, 7), (120, 110, 96), shade=0)
    ic.line([(42, 88), (80, 46)], 3, (176, 168, 146), clip=b)
    return ic


def feather():
    ic = Icon()
    c = (214, 190, 150)
    f = poly([(30, 104), (34, 70), (50, 40), (78, 18), (98, 16), (100, 36), (88, 66), (64, 88), (42, 100)], sm=True)
    ic.add(stroke([(18, 116), (40, 92)], 5), (150, 124, 90), shade=0)
    ic.add(f, c, shade=5)
    ic.line([(34, 100), (94, 22)], 2.8, darker(c, 0.66), clip=f)
    for t in (0.3, 0.5, 0.7):
        x, y = 34 + 60 * t, 100 - 78 * t
        ic.line([(x, y), (x + 20, y + 6)], 2.2, darker(c, 0.72), clip=f)
    return ic


def deathburst():
    ic = Icon()
    ic.add(star(64, 64, 54, 32, 12), (196, 76, 56), shade=0)
    ic.add(star(64, 64, 36, 24, 12, -75), (226, 150, 70), shade=0)
    ic.add(skull(64, 62, 0.85), (236, 230, 210), shade=0)
    skull_face(ic, 64, 62, 0.85)
    return ic


def balor_flame():
    ic = Icon()
    ic.add(flame(64, 112, 76, 98, lean=3), (186, 62, 50), shade=6)
    ic.add(flame(64, 112, 40, 54, lean=2), (228, 132, 62), shade=0)
    return ic


def balor():
    ic = Icon()
    hn = U(*[stroke(smooth([(64 + s * 12, 34), (64 + s * 34, 30), (64 + s * 44, 10)], closed=False) + [(64 + s * 44, 10)], 9) for s in (-1, 1)])
    ic.add(hn, (70, 34, 40), shade=0)
    ic.add(bust(64, 112, 1.15), HRED, shade=6)
    eyes(ic, 64, 42, 8, EYE)
    return ic


def wind():
    ic = Icon()
    def gust(y, x1, r):
        pts = [(16, y), (x1, y)] + [(x1 + r * math.sin(math.radians(a)), y - r + r * math.cos(math.radians(a))) for a in range(20, 271, 25)]
        return stroke(pts, 8)
    ic.add(U(gust(50, 78, 13), gust(74, 92, 12), stroke([(28, 96), (70, 96)], 8)), AIR, shade=0)
    return ic


def earth():
    ic = Icon()
    r = rock_shape()
    ic.add(r, EARTH, shade=7)
    ic.line([(40, 30), (58, 58), (104, 46)], 2.8, darker(EARTH, 0.7), clip=r)
    ic.line([(58, 58), (50, 106)], 2.8, darker(EARTH, 0.7), clip=r)
    return ic


def fire():
    ic = Icon()
    ic.add(flame(64, 112, 76, 98, lean=3), FIRE, shade=6)
    ic.add(flame(64, 112, 40, 54, lean=2), (240, 200, 104), shade=0)
    return ic


def water():
    ic = Icon()
    d = drop(64, 12, 114, 34)
    ic.add(d, WATER, shade=8)
    ic.line([(40, 86), (50, 80), (60, 86), (70, 80), (80, 86), (90, 80)], 3.4, lighter(WATER, 0.4), clip=d, sm=True)
    return ic


def fiend_core():
    ic = Icon()
    ic.add(horns(64, 46, 24, 30), (70, 34, 40), shade=0)
    ic.add(flame(64, 114, 72, 86, lean=0), HRED, shade=6)
    eyes(ic, 64, 78, 10, EYE)
    return ic


def genasi_core():
    ic = Icon()
    d = ell(64, 64, 46)
    ic.add(I(d, rect(0, 0, 64, 64)), FIRE, shade=0)
    ic.add(I(d, rect(64, 0, 128, 64)), WATER, shade=0)
    ic.add(I(d, rect(0, 64, 64, 128)), EARTH, shade=0)
    ic.add(I(d, rect(64, 64, 128, 128)), AIR, shade=0)
    return ic


def ageless():
    ic = Icon()
    glass = poly([(34, 26), (94, 26), (68, 64), (94, 102), (34, 102), (60, 64)])
    ic.add(glass, (226, 232, 240), shade=0)
    ic.add(I(glass, U(rect(0, 80, 128, 128), poly([(50, 38), (78, 38), (64, 60)]))), WATER, shade=0)
    ic.add(U(rect(26, 16, 102, 30, 4), rect(26, 98, 102, 112, 4)), GOLD, shade=4)
    return ic


def lamp_shape():
    body = U(ell(60, 86, 32, 15), poly([(86, 82), (116, 62), (112, 74), (90, 94)]), rect(46, 96, 74, 106, 3), ell(60, 68, 12, 7))
    handle = D(ell(26, 84, 13, 12), ell(26, 84, 6, 6))
    return U(body, handle)


def genie_core():
    ic = Icon()
    smoke = stroke(smooth([(112, 60), (100, 44), (108, 28), (96, 14)], closed=False) + [(96, 14)], 9)
    ic.add(smoke, (150, 170, 220), shade=0)
    ic.add(lamp_shape(), GOLD, shade=6)
    return ic


def genie():
    ic = Icon()
    c = (104, 136, 200)
    body = U(ell(64, 30, 17, 19), ell(64, 70, 30, 24), poly([(38, 78), (90, 78), (76, 100), (62, 110), (44, 116), (52, 104), (48, 92)], sm=True))
    ic.add(body, c, shade=6)
    return ic


def wish():
    ic = Icon()
    ic.add(U(star(58, 62, 44, 14, 4), star(98, 30, 16, 5.5, 4), star(28, 100, 13, 4.5, 4)), (236, 208, 110), shade=0)
    return ic


def ice_crystals():
    ic = Icon()
    sh = U(poly([(52, 108), (58, 34), (66, 14), (76, 36), (78, 108)]), poly([(24, 108), (22, 66), (34, 46), (46, 68), (52, 108)]), poly([(78, 108), (86, 70), (98, 56), (106, 76), (104, 108)]))
    ic.add(sh, ICE, shade=5)
    ic.line([(66, 14), (66, 108)], 2.4, darker(ICE, 0.78), clip=sh)
    ic.line([(34, 46), (36, 108)], 2.2, darker(ICE, 0.78), clip=sh)
    ic.line([(98, 56), (94, 108)], 2.2, darker(ICE, 0.78), clip=sh)
    return ic


def devil_sight():
    ic = Icon()
    ic.add(almond(64, 64, 100, 62), (232, 236, 240), shade=0)
    ic.add(ell(64, 64, 24), (120, 170, 206), shade=5)
    ic.dots(star(64, 64, 16, 4, 4), (30, 40, 60))
    return ic


def ice_devil():
    ic = Icon()
    ic.add(horns(64, 38, 12, 24, 6), (220, 232, 240), shade=0)
    ic.add(bust(64, 112, 1.05), (130, 176, 210), shade=6)
    eyes(ic, 64, 48, 7, (236, 208, 96))
    return ic


def zen_stones():
    ic = Icon()
    for y, rx, ry, c in ((96, 40, 15, (120, 122, 130)), (70, 30, 13, (150, 152, 160)), (48, 20, 10, (180, 182, 190))):
        ic.add(ell(64, y, rx, ry), c, shade=5)
    return ic


def thick_hide():
    ic = Icon()
    sh = shield()
    c = (150, 150, 156)
    ic.add(sh, c, shade=8)
    for y in (40, 58, 76):
        ic.line([(34, y), (52, y + 5), (76, y - 2), (96, y + 4)], 2.8, darker(c, 0.7), clip=sh, sm=True)
    return ic


def elephant():
    ic = Icon()
    g = (156, 156, 162)
    ears = U(ell(28, 52, 22, 30), ell(100, 52, 22, 30))
    ic.add(ears, darker(g, 0.82), shade=0)
    head = U(ell(64, 50, 28, 30), stroke(smooth([(64, 62), (64, 90), (58, 106), (46, 108)], closed=False) + [(46, 108)], 15))
    ic.add(head, g, shade=5)
    ic.add(U(poly([(44, 70), (52, 70), (40, 92)]), poly([(84, 70), (76, 70), (88, 92)])), (240, 236, 222), shade=0)
    for sx in (-12, 12):
        ic.dots(ell(64 + sx, 46, 3.4), (50, 50, 56))
    return ic


def dull():
    ic = Icon()
    b = D(brain(64, 64, 2.0), ell(98, 36, 20))
    ic.add(b, (190, 166, 170), shade=6)
    return ic


def ogre():
    ic = Icon()
    c = (166, 150, 104)
    body = I(ell(64, 120, 50, 44), rect(0, 0, 128, 112))
    ic.add(U(ell(64, 46, 24, 26), body), c, shade=6)
    for sx in (-10, 10):
        ic.dots(ell(64 + sx, 42, 3.6), (56, 46, 30))
        ic.add(tri(64 + sx * 1.1, 66, 9, -15), (240, 236, 222), shade=0)
    return ic


def divinity():
    ic = Icon()
    ic.add(star(64, 64, 52, 36, 12), GOLD, shade=0)
    ic.add(ell(64, 64, 30), (240, 222, 150), shade=6)
    ic.add(bolt(0, 0, 0.5), (150, 110, 40), shade=0)
    return ic


def titan():
    ic = Icon()
    ic.add(bust(64, 114, 1.05), GOLD, shade=6)
    ic.add(halo(64, 16, 22, 8, 5.5), (240, 222, 150), shade=0)
    return ic


def column():
    ic = Icon()
    c = (196, 196, 204)
    shaft = rect(44, 32, 84, 96, 0)
    ic.add(U(shaft, rect(30, 16, 98, 32, 4), rect(30, 96, 98, 112, 4)), c, shade=6)
    for x in (54, 64, 74):
        ic.line([(x, 36), (x, 92)], 2.6, darker(c, 0.72))
    return ic


def weather():
    ic = Icon()
    ic.add(star(80, 44, 34, 23, 12), (214, 170, 70), shade=0)
    ic.add(ell(80, 44, 19), (236, 208, 110), shade=0)
    ic.add(cloud(56, 78, 1.25), (232, 234, 238), shade=6)
    return ic


R = "LichPhylactery/Textures/Races124/Genes/"
ICONS9 = {R + k: v for k, v in {
    "AarakocraHollowBones": ("空心骨", hollow_bones), "AarakocraSkyborn": ("天生飞禽", feather), "BalorDeathburst": ("死亡爆炸", deathburst),
    "BalorFlame": ("深渊烈焰", balor_flame), "BalorStature": ("炎魔体型", balor), "ElementAir": ("疾风血脉", wind),
    "ElementEarth": ("岩石血脉", earth), "ElementFire": ("炽焰血脉", fire), "ElementWater": ("潮汐血脉", water),
    "FiendCore": ("九狱之躯", fiend_core), "GenasiCore": ("元素血脉", genasi_core), "GenieAgeless": ("巨灵不老", ageless),
    "GenieCore": ("巨灵本源", genie_core), "GenieStature": ("巨灵体型", genie), "GenieWish": ("许愿", wish),
    "IceDevilCold": ("寒霜之躯", ice_crystals), "IceDevilSight": ("魔鬼之眼", devil_sight), "IceDevilStature": ("寒冰魔体型", ice_devil),
    "LoxodonCalm": ("沉稳", zen_stones), "LoxodonHide": ("厚皮", thick_hide), "LoxodonStature": ("象人体型", elephant),
    "OgreDull": ("蠢笨", dull), "OgreStature": ("食人魔体型", ogre), "TitanDivinity": ("神威", divinity),
    "TitanStature": ("神裔之躯", titan), "TitanUnshakable": ("不可动摇", column), "TitanWeather": ("天候随心", weather),
}.items()}
LOOK9 = ["AarakocraWings", "BalorWings", "IceDevilStinger", "LoxodonTrunk"]
