"""Batch 2: mind flayer + Anomaly Humanized heavy-linework icons. Fused parts, outer contour only."""
import math
from geneart import *

BONE = (226, 220, 198)
MAW = (58, 28, 30)
LIP = (156, 80, 58)
FLESH = (196, 112, 112)


def star(cx, cy, ro, ri, n, rot=-90):
    pts = []
    for i in range(n * 2):
        r = ro if i % 2 == 0 else ri
        t = math.radians(rot + i * 180 / n)
        pts.append((cx + r * math.cos(t), cy + r * math.sin(t)))
    return poly(pts)


def tri(x, y, w, h):
    return poly([(x - w / 2, y), (x + w / 2, y), (x, y + h)])


def jaw(ic, lower=True, n=5):
    ic.add(ell(64, 64, 46, 34), LIP, shade=6)
    maw = ell(64, 64, 36, 24)
    ic.add(maw, MAW, shade=0)
    teeth = []
    for i in range(n):
        x = 64 + (i - (n - 1) / 2) * 12
        teeth.append(tri(x, 42, 10, 15))
        if lower:
            teeth.append(tri(x + 6 if i < n - 1 else x - 6 * (n - 1) - 6, 86, 10, -15))
    ic.add(I(U(*teeth), ell(64, 64, 38, 26)), BONE, shade=0)
    return ic


def great_jaw():
    return jaw(Icon())


def engulf():
    ic = jaw(Icon(), lower=False)
    ic.add(U(ell(64, 66, 6), I(ell(64, 86, 11, 12), rect(0, 0, 128, 84))), (150, 166, 176), shade=0)
    return ic


def bonepiece(x0, y0, x1, y1, w=6, k=4.6):
    m = stroke([(x0, y0), (x1, y1)], w)
    dx, dy = x1 - x0, y1 - y0
    L = math.hypot(dx, dy); nx, ny = -dy / L * k * 0.8, dx / L * k * 0.8
    for x, y in ((x0, y0), (x1, y1)):
        m = U(m, ell(x + nx, y + ny, k), ell(x - nx, y - ny, k))
    return m


def swallow_corpse():
    ic = jaw(Icon(), lower=False)
    ic.add(bonepiece(48, 76, 80, 66, 5, 4), BONE, shade=0)
    return ic


def heart(cx, cy, s):
    return U(ell(cx - s * 0.5, cy - s * 0.3, s * 0.56), ell(cx + s * 0.5, cy - s * 0.3, s * 0.56), poly([(cx - s * 1.02, cy - s * 0.1), (cx + s * 1.02, cy - s * 0.1), (cx, cy + s * 1.05)]))


def devour_heal():
    ic = jaw(Icon(), lower=False)
    ic.add(heart(64, 72, 10), (196, 84, 88), shade=0)
    return ic


def ingot(cx, cy, w=26, h=11):
    return poly([(cx - w / 2, cy + h / 2), (cx - w / 2 + 5, cy - h / 2), (cx + w / 2 - 5, cy - h / 2), (cx + w / 2, cy + h / 2)])


def living_iron():
    ic = jaw(Icon(), lower=False)
    ic.add(ingot(64, 74), (164, 170, 180), shade=0)
    return ic


def bioferrite_meta():
    ic = jaw(Icon(), lower=False)
    ic.add(ingot(64, 74), (170, 84, 76), shade=0)
    return ic


def undying_spine():
    ic = Icon()
    parts = [rect(60, 18, 68, 110, 3)]
    for i in range(6):
        y = 22 + i * 16.5
        w = 20 - abs(i - 2.2) * 1.6
        parts.append(ell(64, y, w, 6.5))
    ic.add(U(*parts), BONE, shade=5)
    return ic


def spikes(n):
    def f():
        ic = Icon()
        ic.add(rect(22, 82, 106, 102, 10), FLESH, shade=0)
        xs = {1: [64], 2: [48, 80], 3: [40, 64, 88]}[n]
        hs = {1: [56], 2: [50, 50], 3: [42, 54, 42]}[n]
        ic.add(U(*[poly([(x - 10, 90), (x + 10, 90), (x, 90 - h)]) for x, h in zip(xs, hs)]), BONE, shade=3)
        return ic
    return f


def bone_backplate():
    ic = Icon()
    plate = poly([(30, 58), (98, 58), (104, 78), (94, 104), (64, 112), (34, 104), (24, 78)], sm=True)
    sp = U(*[poly([(x - 9, 64), (x + 9, 64), (x, 64 - h)]) for x, h in ((42, 34), (64, 46), (86, 34))])
    ic.add(sp, BONE, shade=3)
    ic.add(plate, (150, 70, 66), shade=6)
    return ic


def dread_aura():
    ic = Icon()
    ic.add(star(64, 64, 48, 30, 12), (150, 56, 54), shade=0)
    ic.add(ell(64, 64, 26), (190, 84, 74), shade=5)
    ic.add(ell(64, 64, 6, 17), (40, 16, 18), shade=0)
    return ic


def agony_psychic():
    ic = Icon()
    ic.add(star(64, 64, 48, 26, 10), (176, 70, 66), shade=0)
    ic.add(U(ell(64, 54, 9), I(ell(64, 84, 15, 18), rect(0, 0, 128, 82))), (70, 52, 84), shade=0)
    return ic


def bulb_burst():
    ic = Icon()
    ic.add(star(64, 64, 46, 28, 12), (110, 150, 74), shade=0)
    ic.add(ell(64, 64, 22), (156, 198, 96), shade=5)
    ic.dots(ell(57, 57, 4), (214, 236, 170))
    return ic


def cerebrum():
    ic = Icon()
    pink = (212, 150, 160)
    b = U(ell(46, 54, 22), ell(82, 54, 22), ell(40, 72, 18), ell(88, 72, 18), ell(64, 48, 22), ell(64, 76, 24, 18))
    ic.add(b, pink, shade=5)
    dk = darker(pink, 0.72)
    ic.line([(64, 30), (63, 50), (65, 70), (64, 92)], 2.6, dk, clip=b, sm=True)
    ic.line([(34, 62), (44, 56), (52, 64)], 2.4, dk, clip=b, sm=True)
    ic.line([(76, 64), (84, 56), (94, 62)], 2.4, dk, clip=b, sm=True)
    ic.line([(40, 82), (50, 78)], 2.4, dk, clip=b)
    ic.line([(78, 78), (88, 82)], 2.4, dk, clip=b)
    return ic


def hive_link():
    ic = Icon()
    nodes = [(64, 34), (34, 88), (94, 88)]
    link = U(*[stroke([nodes[i], nodes[(i + 1) % 3]], 7) for i in range(3)])
    ic.add(link, PURPLE, shade=0)
    ic.add(U(*[ell(x, y, 16) for x, y in nodes]), (212, 150, 160), shade=4)
    return ic


def sterile():
    ic = Icon()
    col = (176, 160, 196)
    egg = poly([(64, 18), (86, 40), (94, 72), (84, 100), (64, 110), (44, 100), (34, 72), (42, 40)], sm=True)
    ic.add(egg, col, shade=6)
    ic.line([(60, 20), (68, 40), (56, 56), (70, 74), (60, 90), (66, 108)], 3.2, darker(col, 0.5), clip=egg)
    return ic


AH = "AnomalyHumanized/Textures/AH/Genes/"
LP = "LichPhylactery/Textures/MindFlayer/Genes/"
ICONS2 = {
    LP + "Cerebrum": ("大脑代谢", cerebrum),
    LP + "HiveLink": ("群体链接", hive_link),
    LP + "Sterile": ("夺心魔不育", sterile),
    AH + "AH_UndyingSpine": ("不灭脊骨", undying_spine),
    AH + "AH_GreatJaw": ("巨颚", great_jaw),
    AH + "AH_EngulfGene": ("吞没", engulf),
    AH + "AH_SwallowCorpseGene": ("吞尸", swallow_corpse),
    AH + "AH_DevourHeal": ("消化回血", devour_heal),
    AH + "AH_LivingIronReservoir": ("活铁", living_iron),
    AH + "AH_BioferriteMetabolism": ("生铁代谢", bioferrite_meta),
    AH + "AH_BoneBackplate": ("骨刺背甲", bone_backplate),
    AH + "AH_BoneSpikeSingle": ("骨刺1", spikes(1)),
    AH + "AH_BoneSpikeDouble": ("骨刺2", spikes(2)),
    AH + "AH_BoneSpikeTriple": ("骨刺3", spikes(3)),
    AH + "AH_DreadAura": ("恐惧气场", dread_aura),
    AH + "AH_AgonyPsychic": ("苦痛心灵", agony_psychic),
    AH + "AH_BulbBurst": ("瘤囊爆裂", bulb_burst),
}
