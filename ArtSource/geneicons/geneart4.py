"""Batch 4: Anomaly Humanized Look_* icons (grey figure + one feature)."""
import math
from geneart import *
from geneart2 import star, tri, BONE
from geneart3 import almond

G = (198, 200, 210)
GD = (128, 130, 144)
EYEC = (56, 58, 70)
HEAD = (64, 72, 30, 36)


def face(ic, behind=None, bcol=None, bshade=3):
    if behind is not None:
        ic.add(behind, bcol, shade=bshade)
    h = ell(*HEAD)
    ic.add(h, G, shade=6)
    for x in (52, 76):
        ic.dots(ell(x, 78, 5.2), EYEC)
    return h


def front(ic, behind=None, bcol=None, bshade=3):
    if behind is not None:
        ic.add(behind, bcol, shade=bshade)
    torso = rect(44, 48, 84, 114, 15)
    ic.add(U(ell(64, 34, 17, 19), torso), G, shade=6)
    for x in (58, 70):
        ic.dots(ell(x, 37, 2.8), EYEC)
    return torso


def side(ic, behind=None, bcol=None, bshade=3):
    if behind is not None:
        ic.add(behind, bcol, shade=bshade)
    body = rect(50, 46, 86, 114, 15)
    ic.add(U(ell(72, 34, 18, 20), body), G, shade=6)
    ic.dots(ell(82, 37, 3), EYEC)
    return body


def spikes_top(xs, hs, base=48, w=13):
    return U(*[poly([(x - w / 2, base + 8), (x + w / 2, base + 8), (x, base - h)]) for x, h in zip(xs, hs)])


def diamond(cx, cy, r):
    return poly([(cx, cy - r), (cx + r * 0.75, cy), (cx, cy + r), (cx - r * 0.75, cy)])


def none():
    ic = Icon(); face(ic); return ic


def agony_cracks():
    ic = Icon(); h = face(ic)
    for pts in ([(50, 46), (58, 56), (52, 64)], [(70, 44), (66, 54), (74, 62)]):
        ic.line(pts, 3.4, (190, 70, 64), clip=h)
    return ic


def agony_shards():
    ic = Icon()
    sh = U(*[diamond(x, y, 10) for x, y in ((30, 56), (46, 34), (64, 26), (82, 34), (98, 56))])
    face(ic, sh, (190, 70, 64), 0)
    return ic


def back_blades():
    ic = Icon()
    bl = U(poly([(46, 62), (30, 22), (40, 70)]), poly([(82, 62), (98, 22), (88, 70)]))
    front(ic, bl, (226, 228, 234), 0)
    return ic


def back_tendrils():
    ic = Icon()
    t = U(stroke(smooth([(54, 60), (38, 44), (34, 22), (40, 10)], closed=False), 7), stroke(smooth([(54, 76), (30, 66), (20, 44)], closed=False), 7))
    side(ic, t, GD, 0)
    return ic


def beast_stripes():
    ic = Icon(); t = front(ic)
    st = U(*[U(poly([(40, y), (60, y + 5), (40, y + 9)]), poly([(88, y), (68, y + 5), (88, y + 9)])) for y in (58, 72, 86, 100)])
    ic.add(I(st, t), (130, 92, 64), shade=0)
    return ic


def bone_crown():
    ic = Icon(); face(ic, spikes_top([42, 53, 64, 75, 86], [18, 24, 28, 24, 18], 42, 11), BONE); return ic


def spike_crown():
    ic = Icon(); face(ic, spikes_top([44, 57, 71, 84], [28, 34, 34, 28], 42, 14), BONE); return ic


def noctol_crest():
    ic = Icon(); face(ic, spikes_top([48, 64, 80], [26, 38, 26], 42, 15), (88, 86, 110)); return ic


def single_horn():
    ic = Icon(); face(ic, spikes_top([64], [36], 42, 15), BONE); return ic


def bone_wing_stubs():
    ic = Icon()
    st = U(poly([(48, 58), (18, 30), (30, 52), (20, 62), (46, 70)]), poly([(80, 58), (110, 30), (98, 52), (108, 62), (82, 70)]))
    front(ic, st, BONE)
    return ic


def chest_plates():
    ic = Icon(); front(ic)
    ic.add(U(*[poly([(64 - w, y), (64 + w, y), (64 + w - 3, y + 9), (64 - w + 3, y + 9)]) for w, y in ((13, 60), (11, 72), (9, 84))]), BONE, shade=0)
    return ic


def chest_ribs():
    ic = Icon(); t = front(ic)
    for y in (62, 72, 82, 92):
        ic.line([(46, y), (64, y + 5), (82, y)], 2.6, GD, clip=t)
    return ic


def dorsal_fin():
    ic = Icon()
    side(ic, poly([(56, 54), (26, 62), (34, 80), (28, 100), (56, 100)]), (150, 160, 180), 0)
    return ic


def extra_eyes():
    ic = Icon(); face(ic)
    for x in (50, 64, 78):
        ic.add(ell(x, 54, 5.5, 4.5), (226, 200, 96), shade=0)
        ic.dots(ell(x, 54, 1.8, 3), EYEC)
    return ic


def face_folds():
    ic = Icon(); h = face(ic)
    for x in (54, 64, 74):
        ic.line([(x, 46), (x, 64)], 2.6, GD, clip=h)
    return ic


def gills():
    ic = Icon(); h = face(ic)
    for s in (-1, 1):
        for y in (86, 92, 98):
            ic.line([(64 + s * 30, y - 2), (64 + s * 18, y)], 2.4, GD, clip=h)
    return ic


def fin_tail():
    ic = Icon()
    tail = U(stroke(smooth([(62, 104), (44, 108), (30, 100)], closed=False), 12), poly([(34, 100), (12, 82), (20, 102), (12, 120)]))
    side(ic, tail, (150, 160, 180), 0)
    return ic


def flesh_tumors():
    ic = Icon(); face(ic)
    ic.add(U(ell(46, 46, 11, 9), ell(66, 36, 9, 7), ell(84, 46, 10, 8)), (214, 160, 170), shade=3)
    return ic


def head_bulbs():
    ic = Icon(); face(ic)
    ic.add(U(ell(46, 46, 9), ell(66, 34, 11), ell(84, 46, 8)), (150, 200, 90), shade=3)
    return ic


def membrane_wings():
    ic = Icon()
    w = U(poly([(48, 54), (8, 40), (14, 74), (24, 70), (28, 90), (38, 80), (48, 96)]), poly([(80, 54), (120, 40), (114, 74), (104, 70), (100, 90), (90, 80), (80, 96)]))
    front(ic, w, GD, 0)
    return ic


def ram_horns():
    ic = Icon()
    tan = (206, 186, 140)
    face(ic)
    for cx in (34, 94):
        ic.add(ell(cx, 46, 15), tan, shade=3)
        ic.add(ell(cx, 46, 8), darker(tan, 0.72), shade=0)
        ic.add(ell(cx, 46, 3.5), tan, shade=0)
    return ic


def skull_plate():
    ic = Icon(); h = face(ic)
    cap = I(U(h, dilate(h, 1)), poly([(0, 0), (128, 0), (128, 56), (64, 62), (0, 56)]))
    ic.add(U(cap, poly([(40, 44), (64, 24), (88, 44)])), (176, 188, 206), shade=0)
    return ic


def spore_tassels():
    ic = Icon(); t = front(ic)
    for x in (52, 64, 76):
        ic.line([(x, 56), (x, 84)], 2.4, GD, clip=t)
        ic.add(ell(x, 88, 5), (150, 200, 90), shade=0)
    return ic


def swept_horns():
    ic = Icon()
    hn = U(stroke(smooth([(48, 52), (34, 40), (16, 38), (6, 46)], closed=False) + [(6, 46)], 9), stroke(smooth([(80, 52), (94, 40), (112, 38), (122, 46)], closed=False) + [(122, 46)], 9))
    face(ic, hn, (120, 120, 130), 0)
    return ic


def vertebra_tail():
    ic = Icon()
    pts = [(54, 104), (44, 108), (34, 106), (25, 100), (18, 92)]
    side(ic, U(*[ell(x, y, 7.5 - i * 0.7) for i, (x, y) in enumerate(pts)]), BONE, 0)
    return ic


def vestigial_arm():
    ic = Icon()
    arm = U(stroke(smooth([(80, 76), (96, 80), (104, 92)], closed=False) + [(104, 92)], 8), ell(105, 95, 7))
    front(ic, arm, GD, 0)
    return ic


def whip_tail():
    ic = Icon()
    tail = U(stroke(smooth([(56, 106), (36, 112), (22, 98), (24, 74)], closed=False) + [(24, 74)], 6), poly([(14, 78), (24, 56), (34, 78)]))
    side(ic, tail, GD, 0)
    return ic


AH = "AnomalyHumanized/Textures/AH/Genes/AH_Look_"
ICONS4 = {AH + k: (k, v) for k, v in {
    "AgonyFaceCracks": agony_cracks, "AgonyShards": agony_shards, "BackBlades": back_blades, "BackTendrils": back_tendrils,
    "BeastStripes": beast_stripes, "BoneCrown": bone_crown, "BoneWingStubs": bone_wing_stubs, "ChestPlates": chest_plates,
    "ChestRibs": chest_ribs, "DorsalFin": dorsal_fin, "ExtraEyes": extra_eyes, "FaceFolds": face_folds, "FinTail": fin_tail,
    "FleshTumors": flesh_tumors, "Gills": gills, "HeadBulbs": head_bulbs, "MembraneWings": membrane_wings,
    "NoctolCrest": noctol_crest, "None": none, "RamHorns": ram_horns, "SingleHorn": single_horn, "SkullPlate": skull_plate,
    "SpikeCrown": spike_crown, "SporeTassels": spore_tassels, "SweptHorns": swept_horns, "VertebraTail": vertebra_tail,
    "VestigialArm": vestigial_arm, "WhipTail": whip_tail,
}.items()}
