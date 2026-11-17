"""Batch 7: DND common races."""
import math
from geneart import *
from geneart2 import star, tri, bonepiece, heart, BONE
from geneart3 import almond, crescent, zig, drop
from geneart5 import SLATE, SLATE_L, waves, gear, spiral_line
from geneart6 import skull, skull_face, speech, GOLD, DRED

WHITE = (236, 236, 236)
STEEL = (160, 166, 178)
WOOD = (150, 104, 70)
GOB = (132, 158, 84)
KOB = (176, 96, 66)
ROCK = (140, 140, 146)
LEAF = (110, 160, 86)


def elf_head(cx=64, cy=66, rx=28, ry=33, ear=17):
    ears = U(poly([(cx - rx + 5, cy - 14), (cx - rx - ear, cy - 36), (cx - rx + 1, cy + 6)]), poly([(cx + rx - 5, cy - 14), (cx + rx + ear, cy - 36), (cx + rx - 1, cy + 6)]))
    return U(ell(cx, cy, rx, ry), ears)


def gob_head(cx=64, cy=66, rx=28, ry=28):
    ears = U(poly([(cx - rx + 6, cy - 12), (cx - rx - 30, cy - 18), (cx - rx + 4, cy + 10)]), poly([(cx + rx - 6, cy - 12), (cx + rx + 30, cy - 18), (cx + rx - 4, cy + 10)]))
    return U(ell(cx, cy, rx, ry), ears)


def kobold(cx, cy, s=1.0):
    pts = [(-34, -6), (-22, -24), (-2, -28), (14, -20), (40, -2), (42, 10), (22, 20), (-10, 24), (-30, 16)]
    head = poly([(cx + x * s, cy + y * s) for x, y in pts], sm=True)
    horn = poly([(cx - 14 * s, cy - 22 * s), (cx - 38 * s, cy - 42 * s), (cx - 26 * s, cy - 14 * s)])
    return head, horn


def kobold_draw(ic, cx, cy, s=1.0, col=KOB, shade=5):
    head, horn = kobold(cx, cy, s)
    ic.add(horn, BONE, shade=0)
    ic.add(head, col, shade=shade)
    ic.dots(ell(cx + 4 * s, cy - 8 * s, 4.2 * s), (236, 204, 96))
    ic.dots(ell(cx + 5 * s, cy - 8 * s, 1.6 * s, 3.4 * s), (40, 24, 20))


def bust(cx, base, s=1.0):
    return U(ell(cx, base - 62 * s, 17 * s, 19 * s), I(ell(cx, base, 32 * s, 46 * s), rect(0, 0, 128, base)))


def sword(x0, y0, x1, y1, w=11):
    dx, dy = x1 - x0, y1 - y0
    L = math.hypot(dx, dy); ux, uy = dx / L, dy / L; nx, ny = -uy, ux
    tipb = (x1 - ux * w * 1.2, y1 - uy * w * 1.2)
    blade = poly([(x0 + nx * w / 2, y0 + ny * w / 2), (tipb[0] + nx * w / 2, tipb[1] + ny * w / 2), (x1, y1), (tipb[0] - nx * w / 2, tipb[1] - ny * w / 2), (x0 - nx * w / 2, y0 - ny * w / 2)])
    guard = stroke([(x0 + nx * w * 1.5, y0 + ny * w * 1.5), (x0 - nx * w * 1.5, y0 - ny * w * 1.5)], 7)
    grip = stroke([(x0, y0), (x0 - ux * w * 1.9, y0 - uy * w * 1.9)], 7)
    return blade, guard, grip


def long_limbs():
    ic = Icon()
    fur = (150, 108, 70)
    arm = stroke(smooth([(22, 108), (50, 74), (84, 44)], closed=False) + [(84, 44)], 18)
    hand = U(ell(92, 36, 14, 15), *[stroke([(88 + i * 6, 30), (92 + i * 8, 12 + abs(i - 1) * 3)], 6) for i in range(3)])
    ic.add(arm, fur, shade=4)
    ic.add(hand, (196, 150, 110), shade=0)
    return ic


def surprise():
    ic = Icon()
    ic.add(star(64, 64, 50, 28, 10), (214, 132, 60), shade=0)
    ic.add(U(poly([(55, 30), (73, 30), (69, 70), (59, 70)]), ell(64, 86, 7.5)), (244, 240, 226), shade=0)
    return ic


def rock_shape():
    return poly([(26, 96), (20, 62), (40, 30), (78, 22), (104, 46), (108, 84), (90, 104), (44, 106)])


def camouflage():
    ic = Icon()
    r = rock_shape()
    ic.add(r, ROCK, shade=7)
    ic.line([(40, 30), (56, 60), (44, 106)], 2.6, darker(ROCK, 0.72), clip=r)
    for x in (62, 86):
        ic.dots(ell(x, 62, 5.5, 4), (232, 204, 96))
        ic.dots(ell(x, 62, 2, 3.4), (40, 36, 30))
    return ic


def darkness():
    ic = Icon()
    d = ell(64, 64, 46)
    ic.add(d, (52, 40, 70), shade=0)
    spiral_line(ic, 64, 64, 42, (140, 108, 176), 5, d, 2.8)
    return ic


def poison_blade():
    ic = Icon()
    blade, guard, grip = sword(84, 44, 30, 98, 13)
    blade2, guard2, grip2 = sword(70, 58, 104, 18, 13)
    b, g, gr = sword(62, 62, 104, 16, 13)
    ic.add(gr, WOOD, shade=0)
    ic.add(b, STEEL, shade=3)
    ic.add(g, (96, 100, 112), shade=0)
    ic.add(drop(40, 62, 110, 15), (150, 190, 80), shade=4)
    return ic


def sunlight():
    ic = Icon()
    ic.add(star(64, 64, 50, 34, 12), (214, 170, 70), shade=0)
    ic.add(ell(64, 64, 28), (236, 208, 110), shade=6)
    return ic


def armor_training():
    ic = Icon()
    sh = shield()
    ic.add(sh, STEEL, shade=8)
    ic.add(U(rect(58, 32, 70, 92, 0), rect(42, 46, 86, 58, 0)), (110, 116, 130), shade=0)
    return ic


def grudge():
    ic = Icon()
    ic.add(drop(64, 12, 114, 34), DRED, shade=8)
    ic.add(skull(64, 80, 0.72), (232, 226, 206), shade=0)
    skull_face(ic, 64, 80, 0.72)
    return ic


def mug():
    ic = Icon()
    ic.add(D(ell(92, 72, 20, 22), ell(92, 72, 10, 12)), (196, 150, 70), shade=0)
    ic.add(rect(30, 44, 88, 110, 6), (206, 160, 70), shade=7)
    ic.add(U(rect(26, 30, 92, 52, 10), ell(40, 30, 12), ell(60, 26, 14), ell(80, 30, 12)), (240, 238, 228), shade=0)
    return ic


def dwarf():
    ic = Icon()
    body = I(ell(64, 118, 46, 40), rect(0, 0, 128, 112))
    ic.add(U(ell(64, 46, 22, 22), body), WHITE, shade=6)
    beard = poly([(42, 48), (86, 48), (88, 70), (78, 94), (64, 104), (50, 94), (40, 70)], sm=True)
    ic.add(beard, (140, 92, 60), shade=5)
    return ic


def mountain():
    ic = Icon()
    m = U(poly([(14, 106), (52, 26), (90, 106)]), poly([(56, 106), (86, 48), (116, 106)]))
    ic.add(m, ROCK, shade=7)
    ic.add(U(poly([(41, 50), (52, 26), (63, 50), (56, 58), (50, 50), (45, 58)]), poly([(78, 64), (86, 48), (94, 64), (88, 70), (84, 64)])), (236, 238, 242), shade=0)
    return ic


def brain(cx, cy, s=1.0):
    return U(*[ell(cx + x * s, cy + y * s, r * s) for x, y, r in ((-12, -4, 14), (12, -4, 14), (-14, 8, 11), (14, 8, 11), (0, -8, 14), (0, 8, 14))])


def bloodline():
    ic = Icon()
    ic.add(shield(), LEAF, shade=8)
    ic.add(brain(64, 58, 1.25), (220, 160, 170), shade=0)
    return ic


def leaf(cx, cy, w, h, rot=0):
    pts = []
    for t in range(0, 360, 20):
        a = math.radians(t)
        x = w * math.sin(a) * (0.5 + 0.5 * abs(math.cos(a / 2)))
        y = -h * math.cos(a)
        ca, sa = math.cos(math.radians(rot)), math.sin(math.radians(rot))
        pts.append((cx + x * ca - y * sa, cy + x * sa + y * ca))
    return poly(pts)


def longevity():
    ic = Icon()
    glass = U(poly([(34, 26), (94, 26), (68, 64), (94, 102), (34, 102), (60, 64)]))
    ic.add(glass, (232, 226, 206), shade=0)
    ic.add(I(glass, U(rect(0, 78, 128, 128), poly([(52, 40), (76, 40), (64, 60)]))), (214, 176, 96), shade=0)
    ic.add(U(rect(26, 16, 102, 30, 4), rect(26, 98, 102, 112, 4)), WOOD, shade=4)
    ic.add(leaf(92, 78, 13, 20, 30), LEAF, shade=0)
    return ic


def trance():
    ic = Icon()
    ic.add(elf_head(60, 70), WHITE, shade=6)
    for sx in (-11, 11):
        ic.line([(60 + sx - 6, 70), (60 + sx, 74), (60 + sx + 6, 70)], 2.6, (130, 130, 140), sm=True)
    ic.add(crescent(98, 28, 16), (226, 214, 150), shade=0)
    return ic


def gnome():
    ic = Icon()
    ic.add(ell(64, 82, 26, 26), WHITE, shade=6)
    ic.add(U(poly([(34, 66), (64, 10), (94, 66)]), rect(30, 60, 98, 72, 6)), (96, 124, 186), shade=5)
    for sx in (-9, 9):
        ic.dots(ell(64 + sx, 86, 3.6), (70, 70, 80))
    return ic


def small_beasts():
    ic = Icon()
    ic.add(speech(64, 56), (234, 232, 228), shade=6)
    c = (160, 112, 72)
    ic.add(U(ell(64, 64, 14, 11), ell(46, 50, 6, 7), ell(58, 40, 6, 7.5), ell(71, 40, 6, 7.5), ell(83, 50, 6, 7)), c, shade=0)
    return ic


def goblin_face(ic, cx=64, cy=66, angry=False):
    ic.add(gob_head(cx, cy), GOB, shade=5)
    for sx in (-11, 11):
        ic.dots(ell(cx + sx, cy - 2, 5, 4), (236, 208, 96))
        ic.dots(ell(cx + sx, cy - 2, 1.8, 3), (40, 40, 24))
    if angry:
        ic.line([(cx - 20, cy - 14), (cx - 5, cy - 7)], 3.4, darker(GOB, 0.5))
        ic.line([(cx + 20, cy - 14), (cx + 5, cy - 7)], 3.4, darker(GOB, 0.5))
        ic.line([(cx - 10, cy + 16), (cx, cy + 12), (cx + 10, cy + 16)], 3, darker(GOB, 0.5), sm=True)


def goblin_fury():
    ic = Icon()
    ic.add(star(64, 64, 54, 34, 12), (186, 72, 62), shade=0)
    goblin_face(ic, 64, 66, True)
    return ic


def goblinoid():
    ic = Icon()
    goblin_face(ic)
    return ic


def boot_shape():
    return poly([(44, 22), (74, 22), (76, 74), (104, 86), (108, 106), (40, 106)], sm=False)


def nimble():
    ic = Icon()
    ic.add(U(*[stroke([(12, y), (30 - (y % 3), y)], 6) for y in (48, 66, 84)]), (214, 218, 226), shade=0)
    b = boot_shape()
    ic.add(b, (150, 100, 66), shade=5)
    ic.add(I(b, rect(0, 98, 128, 128)), (96, 64, 46), shade=0)
    ic.add(I(b, rect(0, 0, 128, 32)), STEEL, shade=0)
    return ic


def half_elf():
    ic = Icon()
    h = elf_head(64, 66)
    ic.add(h, WHITE, shade=0)
    ic.add(I(h, rect(64, 0, 128, 128)), (224, 182, 150), shade=0)
    return ic


def apple():
    ic = Icon()
    ic.add(stroke([(64, 40), (68, 24)], 5), WOOD, shade=0)
    ic.add(leaf(82, 26, 8, 13, 60), LEAF, shade=0)
    ic.add(U(ell(48, 72, 26, 32), ell(80, 72, 26, 32)), (190, 70, 62), shade=7)
    return ic


def hide_behind():
    ic = Icon()
    ic.add(bust(74, 110, 1.2), (150, 150, 158), shade=5)
    ic.add(bust(44, 110, 0.7), WHITE, shade=3)
    return ic


def clover():
    ic = Icon()
    g = (96, 150, 86)
    ic.add(stroke(smooth([(66, 66), (74, 94), (88, 112)], closed=False) + [(88, 112)], 6), darker(g, 0.75), shade=0)
    ms = []
    for rot in (45, 135, 225, 315):
        a = math.radians(rot)
        ox, oy = math.sin(a), -math.cos(a)
        px, py = -oy, ox
        c0 = (64, 58)
        l1 = (c0[0] + ox * 24 + px * 9, c0[1] + oy * 24 + py * 9)
        l2 = (c0[0] + ox * 24 - px * 9, c0[1] + oy * 24 - py * 9)
        ms.append(U(ell(l1[0], l1[1], 11), ell(l2[0], l2[1], 11), poly([(l1[0] + px * 9, l1[1] + py * 9), (l2[0] - px * 9, l2[1] - py * 9), c0])))
    ic.add(U(*ms), g, shade=3)
    return ic


def barrel():
    ic = Icon()
    b = I(ell(64, 64, 42, 60), rect(0, 20, 128, 108))
    ic.add(b, (160, 112, 72), shade=7)
    for x in (48, 64, 80):
        ic.line([(x, 20), (x + (x - 64) * 0.25, 64), (x, 108)], 2.2, (120, 80, 52), clip=b, sm=True)
    ic.add(I(b, U(rect(0, 34, 128, 44), rect(0, 84, 128, 94))), STEEL, shade=0)
    return ic


def high_elf():
    ic = Icon()
    ic.add(elf_head(64, 72), WHITE, shade=6)
    ic.add(star(64, 46, 15, 6, 5), GOLD, shade=0)
    return ic


def wood_elf():
    ic = Icon()
    ic.add(elf_head(64, 72), (190, 206, 160), shade=6)
    ic.add(U(leaf(52, 40, 9, 16, -35), leaf(76, 40, 9, 16, 35)), LEAF, shade=0)
    return ic


def discipline():
    ic = Icon()
    sh = shield()
    ic.add(sh, (150, 54, 58), shade=8)
    for y in (44, 64):
        ic.line([(42, y), (64, y + 18), (86, y)], 7, GOLD, clip=sh)
    return ic


def numbers():
    ic = Icon()
    c = (196, 128, 84)
    ic.add(U(bust(34, 100, 0.72), bust(94, 100, 0.72)), darker(c, 0.8), shade=0)
    ic.add(bust(64, 112, 0.95), c, shade=5)
    return ic


def kobold_blood():
    ic = Icon()
    kobold_draw(ic, 60, 68, 1.2)
    return ic


def dragon_servant():
    ic = Icon()
    wings = U(poly([(52, 64), (6, 14), (10, 46), (22, 42), (24, 60), (36, 56), (38, 74)]), poly([(70, 60), (122, 14), (118, 46), (106, 42), (104, 60), (92, 56), (90, 74)]))
    ic.add(wings, (176, 76, 70), shade=0)
    kobold_draw(ic, 60, 76, 1.0)
    return ic


def pack_tactics():
    ic = Icon()
    kobold_draw(ic, 74, 50, 0.85, darker(KOB, 0.82), 0)
    kobold_draw(ic, 54, 82, 0.95)
    return ic


def trap():
    ic = Icon()
    ic.add(U(*[poly([(x - 11, 92), (x + 11, 92), (x, 92 - h)]) for x, h in ((34, 44), (54, 62), (74, 62), (94, 44))]), STEEL, shade=3)
    ic.add(rect(16, 88, 112, 108, 5), WOOD, shade=0)
    return ic


def relentless():
    ic = Icon()
    h = heart(64, 60, 40)
    ic.add(h, (186, 70, 66), shade=7)
    ic.add(I(h, poly([(64, 0), (128, 0), (128, 128), (64, 128), (72, 96), (58, 76), (72, 56), (58, 36)])), STEEL, shade=0)
    return ic


def tinker():
    ic = Icon()
    ic.add(gear(58, 66, 44), (150, 154, 164), shade=5)
    ic.add(ell(58, 66, 13), (96, 100, 112), shade=0)
    ic.add(stroke([(60, 100), (92, 44)], 8), WOOD, shade=0)
    ic.add(stroke([(78, 30), (108, 48)], 18), (196, 200, 208), shade=0)
    return ic


def savage():
    ic = Icon()
    ic.add(star(64, 64, 52, 30, 10), (186, 72, 62), shade=0)
    b, g, gr = sword(46, 84, 98, 24, 14)
    ic.add(gr, WOOD, shade=0)
    ic.add(b, (206, 210, 218), shade=0)
    ic.add(g, GOLD, shade=0)
    return ic


C = "LichPhylactery/Textures/CommonRaces/Genes/"
ICONS7 = {C + k: v for k, v in {
    "BugbearLongLimbs": ("长臂", long_limbs), "BugbearSurprise": ("突袭", surprise), "DeepGnomeCamouflage": ("石肤伪装", camouflage),
    "DrowDarkness": ("黑暗术", darkness), "DrowPoison": ("蛛后之毒", poison_blade), "DrowSunlight": ("阳光敏感", sunlight),
    "DwarfArmorTraining": ("矮人护甲训练", armor_training), "DwarfGrudge": ("矮人记仇", grudge), "DwarfLiver": ("酒鬼之肝", mug),
    "DwarfStature": ("矮人体型", dwarf), "DwarfStoneEndurance": ("石之耐力", mountain), "ElfBloodline": ("精灵血统", bloodline),
    "ElfLongevity": ("精灵长寿", longevity), "ElfTrance": ("冥想之息", trance), "GnomeBlood": ("侏儒狡黠", gnome),
    "GnomeSmallBeasts": ("与小动物交谈", small_beasts), "GoblinFury": ("小个子的怒火", goblin_fury), "GoblinNimble": ("灵巧逃脱", nimble),
    "GoblinoidBlood": ("地精族血统", goblinoid), "HalfElfBlood": ("半精灵血统", half_elf), "HalflingBlood": ("半身人血统", apple),
    "HalflingHideBehind": ("躲到大个子身后", hide_behind), "HalflingLuck": ("半身人的幸运", clover), "HalflingStout": ("敦实身板", barrel),
    "HighElfLineage": ("高等精灵血脉", high_elf), "HobgoblinDiscipline": ("铁律", discipline), "HobgoblinNumbers": ("群体之助", numbers),
    "KoboldBlood": ("狗头人血统", kobold_blood), "KoboldDragonServant": ("龙之仆从", dragon_servant), "KoboldPackTactics": ("群体战术", pack_tactics),
    "KoboldTrapsmith": ("陷阱匠", trap), "Relentless": ("不屈坚韧", relentless), "RockGnomeTinker": ("修补匠", tinker),
    "Savage": ("野蛮攻击", savage), "WoodElfLineage": ("木精灵血脉", wood_elf),
}.items()}
