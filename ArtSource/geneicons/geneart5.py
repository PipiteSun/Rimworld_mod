"""Batch 5: remaining Anomaly Humanized non-look icons."""
import math
from geneart import *
from geneart2 import star, tri, bonepiece, BONE, FLESH
from geneart3 import almond, crescent, zig

SLATE = (74, 74, 98)
SLATE_L = (112, 112, 140)
MAG = (176, 96, 170)
MOON = (226, 214, 150)


def gear(cx, cy, r, n=8, tooth=0.24):
    ms = [ell(cx, cy, r * (1 - tooth * 0.55))]
    for i in range(n):
        t = math.radians(i * 360 / n)
        c, s = math.cos(t), math.sin(t)
        w = r * 0.24
        p = [(cx + c * r * 0.6 - s * w, cy + s * r * 0.6 + c * w), (cx + c * r - s * w * 0.7, cy + s * r + c * w * 0.7),
             (cx + c * r + s * w * 0.7, cy + s * r - c * w * 0.7), (cx + c * r * 0.6 + s * w, cy + s * r * 0.6 - c * w)]
        ms.append(poly(p))
    return U(*ms)


def spiral_line(ic, cx, cy, r, col, w, clip, turns=2.6):
    pts = [(cx + r * (t / (360 * turns)) * math.cos(math.radians(t)), cy + r * (t / (360 * turns)) * math.sin(math.radians(t))) for t in range(40, int(360 * turns), 15)]
    ic.line(pts, w, col, clip=clip)


def waves(cx, cy, radii=(18, 32, 46), w=7, a=38):
    return U(*[arc(cx, cy, r, -a, a, w) for r in radii])


def wolf_head(cx, cy, s=1.0):
    pts = [(-30, -34), (-14, -18), (14, -18), (30, -34), (30, 2), (0, 40), (-30, 2)]
    return poly([(cx + x * s, cy + y * s) for x, y in pts])


def ambush():
    ic = Icon()
    grass = U(*[poly([(x - 9, 112), (x + 9, 112), (x + lean, 112 - h)]) for x, h, lean in ((22, 70, -4), (38, 88, 0), (90, 88, 0), (106, 70, 4))])
    ic.add(grass, (108, 138, 72), shade=0)
    ic.add(almond(64, 66, 78, 42), (214, 184, 110), shade=0)
    ic.add(ell(64, 66, 13), (150, 104, 50), shade=0)
    ic.dots(ell(64, 66, 4, 11), (40, 28, 20))
    return ic


def beast_kin():
    ic = Icon()
    c = (160, 112, 72)
    pad = U(ell(64, 82, 26, 22), ell(46, 92, 12), ell(82, 92, 12))
    toes = U(ell(30, 56, 11, 13), ell(52, 38, 11, 14), ell(76, 38, 11, 14), ell(98, 56, 11, 13))
    ic.add(U(pad, toes), c, shade=4)
    return ic


def chimerism():
    ic = Icon()
    head = ell(64, 70, 32, 36)
    ear = poly([(70, 46), (100, 12), (102, 60)])
    whole = U(head, ear)
    ic.add(whole, (206, 170, 132), shade=0)
    ic.add(I(whole, rect(64, 0, 128, 128)), (140, 98, 64), shade=0)
    for y in range(40, 104, 11):
        ic.line([(58, y), (70, y + 3)], 2.6, (96, 62, 46), clip=whole)
    ic.dots(ell(50, 72, 4.5), (60, 44, 36))
    ic.dots(ell(80, 70, 4.5), (236, 200, 96))
    return ic


def downed_shriek():
    ic = Icon()
    ic.add(waves(54, 64), (150, 122, 176), shade=0)
    head = ell(36, 64, 24, 28)
    ic.add(head, (150, 166, 176), shade=5)
    ic.dots(ell(42, 72, 7, 10), (44, 48, 60))
    return ic


def hypnotic_voice():
    ic = Icon()
    ic.add(waves(54, 64), MAG, shade=0)
    d = ell(36, 64, 24)
    ic.add(d, (84, 52, 100), shade=0)
    spiral_line(ic, 36, 64, 20, (214, 150, 210), 3.2, d)
    return ic


def keen_senses():
    ic = Icon()
    ic.add(waves(58, 62, (22, 36), 7, 40), (150, 190, 214), shade=0)
    ear = poly([(30, 30), (52, 22), (66, 40), (62, 66), (50, 84), (48, 104), (32, 106), (26, 84), (20, 56)], sm=True)
    ic.add(ear, (206, 170, 132), shade=5)
    ic.line([(34, 44), (48, 40), (52, 56), (42, 70), (38, 86)], 3.2, (156, 118, 92), clip=ear, sm=True)
    return ic


def eyeless_sight():
    ic = Icon()
    rays = U(*[stroke([(64 + 38 * math.cos(math.radians(a)), 62 + 38 * math.sin(math.radians(a))), (64 + 50 * math.cos(math.radians(a)), 62 + 50 * math.sin(math.radians(a)))], 6) for a in (200, 235, 270, 305, 340)])
    ic.add(rays, (176, 196, 210), shade=0)
    h = ell(64, 72, 28, 34)
    ic.add(h, (150, 166, 176), shade=5)
    ic.add(I(h, rect(0, 64, 128, 78)), (92, 104, 116), shade=0)
    return ic


def fire_dread():
    ic = Icon()
    f = flame(64, 112, 74, 98, lean=3)
    ic.add(f, (206, 118, 62), shade=6)
    dk = (64, 30, 24)
    ic.dots(ell(52, 76, 6, 8), dk)
    ic.dots(ell(76, 76, 6, 8), dk)
    ic.line([(48, 96), (54, 92), (60, 97), (66, 92), (72, 97), (78, 92)], 3.2, dk, clip=f)
    return ic


def hypnotic_gaze():
    ic = Icon()
    d = ell(64, 64, 46)
    ic.add(d, (96, 60, 114), shade=0)
    spiral_line(ic, 64, 64, 44, (176, 110, 186), 4.5, d, 3.2)
    ic.add(ell(64, 64, 13), (226, 220, 232), shade=0)
    ic.dots(ell(64, 64, 5.5), (60, 30, 76))
    return ic


def limb_regrowth():
    ic = Icon()
    c = (206, 126, 122)
    palm = rect(40, 60, 88, 110, 14)
    fingers = U(*[rect(x, top, x + 10, 76, 5) for x, top in ((41, 34), (53, 24), (65, 22), (77, 30))])
    thumb = stroke([(42, 88), (26, 70)], 11)
    ic.add(U(palm, fingers, thumb), c, shade=5)
    ic.add(star(98, 28, 14, 5, 4), (150, 196, 110), shade=0)
    return ic


def living_alloy():
    ic = Icon()
    ic.add(gear(64, 64, 46), (150, 154, 164), shade=5)
    ic.add(ell(64, 64, 16), (176, 78, 72), shade=3)
    return ic


def mech_speed():
    ic = Icon()
    ic.add(gear(64, 64, 46), (150, 154, 164), shade=5)
    ic.add(bolt(0, 0, 0.56), (226, 192, 84), shade=0)
    return ic


def night_power():
    ic = Icon()
    ic.add(crescent(54, 66, 42), (190, 198, 226), shade=6)
    ic.add(U(star(92, 34, 12, 4.5, 4), star(104, 70, 8, 3, 4), star(78, 60, 7, 2.6, 4)), (238, 236, 200), shade=0)
    return ic


def pack_hunter():
    ic = Icon()
    ic.add(wolf_head(82, 56, 0.9), SLATE, shade=0)
    ic.add(wolf_head(50, 70, 1.05), SLATE_L, shade=5)
    for sx in (-12, 12):
        ic.dots(ell(50 + sx, 70, 3.6, 2.6), (236, 200, 96))
    return ic


def psychic_veil():
    ic = Icon()
    eye = almond(64, 66, 96, 56)
    ic.add(eye, (226, 220, 232), shade=0)
    ic.add(I(ell(64, 70, 17), eye), (136, 104, 176), shade=0)
    ic.dots(I(ell(64, 70, 7), eye), (40, 28, 56))
    ic.add(I(eye, rect(0, 0, 128, 62)), SLATE, shade=0)
    return ic


def revenant_vanish():
    ic = Icon()
    ghost = U(ell(64, 50, 28, 30), rect(36, 50, 92, 108, 4))
    stripes = U(rect(0, 0, 128, 70), *[rect(0, y, 128, y + 7) for y in (76, 89, 102)])
    ic.add(I(ghost, stripes), (214, 210, 190), shade=5)
    for x in (54, 74):
        ic.dots(ell(x, 50, 4.5, 6), (70, 68, 80))
    return ic


def scythe_arms():
    ic = Icon()
    blade = poly([(34, 96), (40, 44), (60, 22), (108, 12), (72, 36), (58, 56), (56, 96)], sm=False)
    ic.add(blade, (176, 184, 200), shade=4)
    ic.add(rect(24, 88, 70, 110, 9), SLATE_L, shade=0)
    return ic


def shadow_claws():
    ic = Icon()
    cl = U(*[poly([(x, 108), (x + 14, 108), (x + 30, 20 + i * 4), (x + 18, 56)]) for i, x in enumerate((22, 48, 74))])
    ic.add(cl, SLATE, shade=3)
    return ic


def shadow_cloak():
    ic = Icon()
    cloak = poly([(64, 12), (88, 36), (104, 108), (24, 108), (40, 36)], sm=False)
    ic.add(cloak, SLATE, shade=5)
    ic.add(ell(64, 50, 13, 16), (36, 34, 50), shade=0)
    return ic


def shadow_leap():
    ic = Icon()
    ic.add(ell(34, 100, 24, 9), (48, 46, 64), shade=0)
    ic.add(arc(62, 100, 40, 190, 330, 7), SLATE_L, shade=0)
    ic.add(ell(98, 78, 15), SLATE, shade=4)
    return ic


def shadowwalk():
    ic = Icon()
    fig = U(ell(64, 46, 16, 18), I(ell(64, 100, 28, 40), rect(0, 0, 128, 92)))
    ic.add(fig, SLATE_L, shade=5)
    ic.add(ell(64, 94, 46, 14), (48, 46, 64), shade=0)
    return ic


def short_skip():
    ic = Icon()
    pur = (134, 108, 166)
    ic.add(arc(64, 92, 38, 205, 335, 7), (176, 150, 200), shade=0)
    for cx, cy in ((32, 86), (96, 86)):
        ic.add(ell(cx, cy, 20), pur, shade=3)
        ic.add(ell(cx, cy, 11), (62, 48, 88), shade=0)
    return ic


def spine_volley():
    ic = Icon()
    sp = U(*[poly([(30 + i * 4, 84 - i * 22), (36 + i * 4, 96 - i * 22), (108, 52 - i * 18 + 16)]) for i in range(3)])
    ic.add(sp, BONE, shade=0)
    ic.add(rect(16, 34, 40, 108, 10), FLESH, shade=4)
    return ic


def void_body():
    ic = Icon()
    torso = poly([(40, 22), (88, 22), (100, 40), (90, 108), (38, 108), (28, 40)], sm=False)
    ic.add(U(torso, ell(64, 20, 12, 10)), (70, 54, 100), shade=5)
    for x, y, r in ((52, 46, 3.5), (76, 58, 2.6), (60, 78, 4.2), (80, 88, 2.4), (48, 94, 2.4)):
        ic.dots(star(x, y, r * 2, r * 0.8, 4), (206, 190, 232))
    return ic


def voracious():
    ic = Icon()
    food = U(ell(50, 62, 18, 14), ell(76, 58, 18, 15), ell(64, 52, 16, 13))
    ic.add(bonepiece(74, 50, 96, 26, 6, 5), BONE, shade=0)
    ic.add(food, (196, 132, 84), shade=4)
    bowl = I(ell(64, 64, 46, 44), rect(0, 66, 128, 128))
    ic.add(bowl, (150, 110, 80), shade=6)
    return ic


AH = "AnomalyHumanized/Textures/AH/Genes/AH_"
ICONS5 = {AH + k: (k, v) for k, v in {
    "AmbushInstinct": ambush, "BeastKin": beast_kin, "ChimerismGene": chimerism, "DownedShriek": downed_shriek,
    "EyelessSight": eyeless_sight, "FireDread": fire_dread, "HypnoticGazeGene": hypnotic_gaze, "HypnoticVoice": hypnotic_voice,
    "KeenSenses": keen_senses, "LimbRegrowth": limb_regrowth, "LivingAlloy": living_alloy, "MechSpeedGene": mech_speed,
    "NightPower": night_power, "PackHunter": pack_hunter, "PsychicVeilGene": psychic_veil, "RevenantVanishGene": revenant_vanish,
    "ScytheArms": scythe_arms, "ShadowClaws": shadow_claws, "ShadowCloakGene": shadow_cloak, "ShadowLeapGene": shadow_leap,
    "Shadowwalk": shadowwalk, "ShortSkipGene": short_skip, "SpineVolleyGene": spine_volley, "VoidBody": void_body, "Voracious": voracious,
}.items()}
