"""Batch 3: Anomaly Humanized."""
import math
from geneart import *
from geneart2 import star, tri, bonepiece, heart, BONE, FLESH

RED = (176, 66, 62)


def zig(x, y0, y1, amp, n=4):
    pts = [(x + (amp if i % 2 else -amp), y0 + (y1 - y0) * i / n) for i in range(n + 1)]
    return pts


def almond(cx, cy, w, h):
    R = (w * w / 4 + h * h / 4) / h
    return I(ell(cx, cy - (R - h / 2), R), ell(cx, cy + (R - h / 2), R))


def crescent(cx, cy, r, dx=0.45, dy=-0.15):
    return D(ell(cx, cy, r), ell(cx + r * dx, cy + r * dy, r * 0.82))


def drop(cx, top, bottom, r):
    cy = bottom - r
    return U(ell(cx, cy, r), poly([(cx - r * 0.93, cy - r * 0.38), (cx, top), (cx + r * 0.93, cy - r * 0.38)]))


def agony_breath():
    ic = Icon()
    ic.add(U(*[stroke(zig(x, 22, 106, 8, 3), 8) for x in (32, 64, 96)]), RED, shade=0)
    return ic


def stench():
    ic = Icon()
    g = (124, 164, 80)
    ic.add(U(*[stroke(zig(x, 18, 84, 7, 3), 7) for x in (36, 64, 92)]), g, shade=0)
    ic.add(ell(64, 100, 28, 9), (160, 200, 100), shade=0)
    return ic


def agony_pulse():
    ic = Icon()
    ic.add(ell(64, 64, 46), RED, shade=0)
    ic.add(ell(64, 64, 37), darker(RED, 0.62), shade=0)
    ic.add(ell(64, 64, 29), RED, shade=0)
    ic.add(ell(64, 64, 20), (70, 50, 84), shade=4)
    return ic


def agony_reservoir():
    ic = Icon()
    ic.add(drop(64, 14, 112, 30), RED, shade=8)
    return ic


def bioferrite_bones():
    ic = Icon()
    b = bonepiece(36, 94, 92, 34, 12, 9)
    ic.add(b, (160, 166, 178), shade=4)
    ic.line([(46, 84), (82, 44)], 3.4, (176, 78, 74))
    return ic


def bottomless_stomach():
    ic = Icon()
    ic.add(ell(64, 64, 46, 38), (190, 118, 86), shade=6)
    hole = ell(64, 66, 28, 22)
    ic.add(hole, (70, 30, 32), shade=0)
    pts = [(64 + (4 + t * 0.045) * math.cos(math.radians(t)), 66 + (3 + t * 0.036) * math.sin(math.radians(t))) for t in range(0, 420, 20)]
    ic.line(pts, 2.6, (120, 54, 52), clip=hole)
    return ic


def brood():
    ic = Icon()
    ic.add(U(tri(54, 52, 12, -30), tri(74, 52, 12, -24)), BONE, shade=3)
    ic.add(ell(64, 74, 42, 36), (150, 58, 58), shade=6)
    ic.add(ell(64, 68, 18, 11), (214, 130, 130), shade=0)
    return ic


def dark_affinity():
    ic = Icon()
    ic.add(ell(64, 64, 46), (62, 64, 88), shade=6)
    ic.add(crescent(60, 62, 28), (226, 214, 150), shade=0)
    return ic


def nocturnal():
    ic = Icon()
    ic.add(crescent(58, 64, 44), (226, 214, 150), shade=6)
    ic.add(star(92, 40, 11, 4.5, 4), (240, 232, 190), shade=0)
    return ic


def emp_weakness():
    ic = Icon()
    ic.add(ell(64, 64, 46), (98, 146, 190), shade=6)
    ic.add(bolt(0, 0, 0.6), (226, 192, 84), shade=0)
    return ic


def ears_beast():
    ic = Icon()
    skin = (206, 170, 132)
    ear = poly([(70, 52), (100, 14), (104, 60)])
    ic.add(U(ell(58, 74, 34, 36), ear), skin, shade=6)
    ic.add(poly([(80, 50), (97, 26), (99, 56)]), (214, 150, 140), shade=0)
    return ic


def ears_fin():
    ic = Icon()
    fins = U(poly([(40, 56), (12, 34), (22, 66), (12, 94), (40, 78)]), poly([(88, 56), (116, 34), (106, 66), (116, 94), (88, 78)]))
    ic.add(fins, (170, 88, 56), shade=0)
    ic.add(ell(64, 66, 28, 34), (198, 128, 98), shade=6)
    return ic


def ears_noctol():
    ic = Icon()
    col = (76, 74, 98)
    ears = U(poly([(46, 56), (10, 22), (36, 76)]), poly([(82, 56), (118, 22), (92, 76)]))
    ic.add(U(ears, ell(64, 72, 28, 30)), col, shade=6)
    eyes(ic, 64, 70, 10, EYE)
    return ic


def glow_eye(col):
    def f():
        ic = Icon()
        ic.add(almond(64, 64, 100, 58), (58, 62, 84), shade=0)
        ic.add(ell(64, 64, 20), col, shade=5)
        ic.dots(ell(57, 57, 4.4), lighter(col, 0.7))
        return ic
    return f


def fertile_spores():
    ic = Icon()
    g = (110, 140, 70)
    stem = stroke([(64, 112), (62, 84), (64, 56)], 7)
    leaves = U(ell(46, 76, 16, 8), ell(82, 68, 16, 8))
    ic.add(U(stem, leaves), g, shade=3)
    ic.add(U(ell(40, 38, 9), ell(64, 26, 9), ell(88, 36, 9)), (166, 204, 96), shade=3)
    return ic


def hybrid_hide():
    ic = Icon()
    hide = poly([(26, 24), (102, 24), (104, 86), (90, 106), (64, 112), (38, 106), (24, 86)])
    ic.add(hide, (204, 164, 128), shade=0)
    ic.add(I(hide, rect(0, 0, 64, 128)), (160, 114, 90), shade=0)
    for y in range(34, 104, 12):
        ic.line([(57, y), (71, y + 3)], 2.6, (110, 70, 58), clip=hide)
    return ic


def split_survival():
    ic = Icon()
    c = (156, 58, 58)
    ic.add(ell(42, 64, 20, 34), c, shade=5)
    ic.add(ell(86, 64, 20, 34), c, shade=5)
    return ic


def wounded_rage():
    ic = Icon()
    c = (166, 60, 54)
    h = heart(64, 58, 38)
    ic.add(h, c, shade=7)
    ic.line([(66, 28), (56, 48), (72, 62), (58, 80), (64, 98)], 4.2, darker(c, 0.45), clip=h)
    return ic


def void_kinship():
    ic = Icon()
    ic.add(ell(64, 64, 46), (78, 60, 108), shade=6)
    ic.add(almond(64, 64, 64, 34), (226, 220, 232), shade=0)
    ic.add(ell(64, 64, 11), (136, 104, 176), shade=0)
    ic.dots(ell(64, 64, 4.5), (40, 28, 56))
    return ic


def void_resonance():
    ic = Icon()
    ic.add(ell(64, 64, 46), (134, 108, 166), shade=6)
    ic.add(ell(64, 64, 35), (62, 48, 88), shade=0)
    ic.add(star(64, 64, 30, 13, 5), (134, 108, 166), shade=0)
    return ic


AH = "AnomalyHumanized/Textures/AH/Genes/AH_"
ICONS3 = {AH + k: v for k, v in {
    "AgonyBreath": ("苦痛吐息", agony_breath),
    "AgonyPulseGene": ("苦痛脉冲", agony_pulse),
    "AgonyReservoir": ("苦痛储池", agony_reservoir),
    "BioferriteBones": ("生铁骨", bioferrite_bones),
    "BottomlessStomach": ("无底胃", bottomless_stomach),
    "BroodGene": ("育囊", brood),
    "DarkAffinity": ("暗夜亲和", dark_affinity),
    "Nocturnal": ("夜行", nocturnal),
    "EMPWeakness": ("EMP弱点", emp_weakness),
    "Ears_Beast": ("兽耳", ears_beast),
    "Ears_Fin": ("鳍耳", ears_fin),
    "Ears_NoctolLong": ("夜魔长耳", ears_noctol),
    "Eyes_GlowBlue": ("蓝", glow_eye((90, 150, 214))),
    "Eyes_GlowCyan": ("青", glow_eye((90, 196, 200))),
    "Eyes_GlowGreen": ("绿", glow_eye((120, 190, 90))),
    "Eyes_GlowMagenta": ("品红", glow_eye((196, 96, 180))),
    "Eyes_GlowRed": ("红", glow_eye((204, 80, 72))),
    "Eyes_GlowYellow": ("黄", glow_eye((222, 196, 84))),
    "FertileSpores": ("孢子", fertile_spores),
    "HybridHide": ("混种皮", hybrid_hide),
    "Stench": ("恶臭", stench),
    "SplitSurvival": ("分裂求生", split_survival),
    "WoundedRage": ("伤怒", wounded_rage),
    "VoidKinship": ("虚空亲缘", void_kinship),
    "VoidResonance": ("虚空共鸣", void_resonance),
}.items()}
