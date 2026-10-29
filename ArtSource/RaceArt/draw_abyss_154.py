# 1.54.0 深渊仪式贴图：沿用 draw_race_art.py / draw_races_124.py 的画法（粗黑轮廓、平涂、单侧暗面、低饱和）。
#  - Textures/Abyss/{Horns,Eyes,Heads,Taint,Wings,Tails}/*_{south,east,north}：恶魔与坎比恩的头部 / 身体附件（128px，对位同 draw_races_124.py 的画框）。
#  - Textures/Abyss/Quasit：夸塞魔三向 + 干尸（256px）。
#  - Textures/Abyss/{Xenotypes,Genes,Abilities}、Abyss/Faction：图标。
#  - Textures/Items/Abyss：灵魂币、恶魔血、恶魔学典籍（合 / 开）、真名卷、位面锚链、饱魂之牙。
#  - Textures/LP/Abyss：深渊法阵（普通 / 银边）、束缚法阵、驱魔结界。
#  - ArtSource/Preview/Abyss-1.54.0.png：所有单位三向（示意的身体 + 附件）和物品、建筑。
# 用法：python -I ArtSource/RaceArt/draw_abyss_154.py <模组根目录>
import math, os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import draw_race_art as base
import draw_races_124 as r
from draw_race_art import Pic, poly, ell, tent, arc, ellpts, mir, INK
from draw_minotaur_gith import horn, HEAD_OW
from PIL import Image, ImageDraw

ROOT = sys.argv[1] if len(sys.argv) > 1 else os.getcwd()
base.TEX = os.path.join(ROOT, "Textures")
OW = base.OW
S = r.S

# ---------------- 调色（低饱和） ----------------
ABYSS = (120, 36, 52); ABYSS_D = (82, 22, 36); ABYSS_L = (176, 72, 84); EMBERGLOW = (236, 108, 64)
VIOLET = (96, 62, 112); VIOLET_D = (64, 40, 78); VIOLET_L = (150, 112, 162)
DRETCH = (186, 182, 168); DRETCH_D = (150, 146, 132)
BARL = (168, 104, 62); BARL_D = (128, 76, 44); BARL_L = (200, 140, 92)
HEZ = (112, 128, 96); HEZ_D = (84, 98, 70); HEZ_L = (146, 160, 120); HEZ_W = (170, 160, 130)
SUCC = (196, 92, 104); SUCC_D = (150, 64, 78)
NAB = (84, 82, 86); NAB_D = (58, 56, 60); NAB_L = (118, 116, 120)
SHADOW = (40, 36, 50); SHADOW_D = (24, 22, 30)
HORN = (60, 44, 46); HORN_D = (38, 28, 30); HORN_TIP = (150, 110, 96)
TUSK = (232, 222, 194); TUSK_D = (196, 184, 152)
SILVER = (196, 198, 202); SILVER_D = (150, 152, 158); IRON = (92, 96, 100); IRON_D = (62, 66, 70)
GOLDEYE = (240, 176, 64); REDEYE = (220, 50, 40); PALEGLOW = (226, 214, 250)
PARCH = (226, 210, 168); PARCH_D = (190, 170, 124); LEATHERB = (92, 60, 50); LEATHERB_D = (64, 40, 34)
HUMAN = (226, 190, 160); HUMAN_D = (196, 160, 130)


def blank():
    return Pic()


# ================= 头部附件 =================
def small_horns(color, dark, tip, view, scale=1.0, curl=0.0):
    """短角：从发际线两侧长出，scale 控制长度，curl 让角尖往外弯。"""
    p = Pic()
    base_pts = [(41, 36), (38, 31), (36.5, 25), (37.5, 20)]
    pts = [(base_pts[0][0] + (x - base_pts[0][0]) * scale - curl * i * 1.2, base_pts[0][1] + (y - base_pts[0][1]) * scale) for i, (x, y) in enumerate(base_pts)]
    if view in ("south", "north"):
        for s in (S, mir):
            horn(p, s(pts), 5.4 * min(1.2, .6 + scale * .4), 1.3, color=color, dark=dark, tip=tip)
    else:
        horn(p, [(54, 35), (54.5, 31), (56, 26)], 4.0, 1.1, color=dark, dark=dark, tip=tip)
        horn(p, [(46, 37), (43.5, 32), (43, 26), (45, 21)][:2 + int(scale > .8) + int(scale > 1.1)], 5.2, 1.3, color=color, dark=dark, tip=tip)
    return p


def swept_horns(color, dark, tip, view):
    """坎比恩：向后上方掠的角（比提夫林的长、比炎魔的细）。"""
    p = Pic()
    if view in ("south", "north"):
        for s in (S, mir):
            horn(p, s([(40, 37), (35, 33), (31, 27), (29.5, 20), (31, 13)]), 6.0, 1.3, color=color, dark=dark, tip=tip)
    else:
        horn(p, [(53, 35), (56, 31), (60, 28), (64, 26)], 4.4, 1.1, color=dark, dark=dark, tip=tip)
        horn(p, [(45, 37), (48, 31), (53, 27), (59, 24), (66, 23)], 6.0, 1.3, color=color, dark=dark, tip=tip)
    return p


def curled_horns(color, dark, tip, view):
    """魅魔：弯成小半圈的角。"""
    p = Pic()
    if view in ("south", "north"):
        for s in (S, mir):
            horn(p, s([(41, 36), (37, 31), (36, 25), (39.5, 21), (43, 22.5)]), 4.8, 1.1, color=color, dark=dark, tip=tip)
    else:
        horn(p, [(53, 35), (55, 30), (58, 28), (60.5, 30)], 3.6, 1.0, color=dark, dark=dark, tip=tip)
        horn(p, [(46, 37), (44, 31), (45.5, 26), (49.5, 24.5), (52.5, 27)], 4.8, 1.1, color=color, dark=dark, tip=tip)
    return p


def eyes(color, view, glow=None):
    p = Pic()
    if view == "north":
        return p
    if view == "south":
        for x in (43.5, 56.5):
            p.add(ell((x - 2.6, 51.5, x + 2.6, 55.2)), color, 1.2)
            p.add(ell((x - .9, 52.4, x + .5, 53.8)), glow or (255, 240, 200), 0)
    else:
        p.add(ell((60.5, 51.5, 65.3, 55.2)), color, 1.2)
        p.add(ell((62.6, 52.4, 64.0, 53.8)), glow or (255, 240, 200), 0)
    return p


def barlgura_face(view):
    p = Pic()
    if view == "south":
        p.add(poly([(33, 46), (40, 41), (50, 43), (60, 41), (67, 46), (64, 50), (50, 48), (36, 50)]), BARL_D, HEAD_OW)  # 眉骨
        p.add(ell((38, 55, 62, 72)), BARL_L, HEAD_OW)  # 猿口鼻
        p.add(ell((44.5, 57, 48, 60)), INK, 0); p.add(ell((52, 57, 55.5, 60)), INK, 0)
        for s in (S, mir):
            p.add(tent(s([(42, 67), (39.5, 61), (38.5, 56)]), 3.4, .8), TUSK, 1.2)
        p.line([(43, 68.5), (50, 70.5), (57, 68.5)], 1.4, INK)
        for x in (36, 64):
            p.add(ell((x - 3, 49, x + 3, 53)), (230, 170, 70), 1.0)
    elif view == "east":
        p.add(poly([(48, 42), (58, 40), (68, 44), (66, 49), (52, 48)]), BARL_D, HEAD_OW)
        p.add(poly([(56, 54), (70, 55), (73, 63), (69, 71), (57, 70)]), BARL_L, HEAD_OW)
        p.add(ell((68, 57, 71, 60)), INK, 0)
        p.add(tent([(64, 68), (67, 61), (68, 56)], 3.4, .8), TUSK, 1.2)
        p.add(ell((60, 48, 65, 52)), (230, 170, 70), 1.0)
    else:
        for i in range(5):
            x = 36 + i * 7
            p.add(tent([(x, 36), (x + 1.5, 48)], 4.5, 1), BARL_D, 1.0)
    return p


def hezrou_head(view):
    p = Pic()
    if view == "south":
        p.add(ell((26, 30, 74, 74)), HEZ, HEAD_OW)
        p.add(ell((26, 52, 74, 74)), HEZ_D, 0)
        p.add(poly([(30, 61), (40, 66), (50, 67), (60, 66), (70, 61), (66, 68), (50, 72), (34, 68)]), (96, 48, 52), HEAD_OW)  # 宽口
        for s in (S, mir):
            p.add(ell(r.sb(s, (32, 34, 44, 46))), HEZ_L, HEAD_OW)
            p.add(ell(r.sb(s, (35, 37, 41, 43))), (230, 200, 90), 0)
            p.add(ell(r.sb(s, (37, 37.6, 39, 42.4))), INK, 0)
        for x, y in [(50, 40), (44, 52), (57, 50), (36, 56), (64, 55)]:
            p.add(ell((x - 2, y - 2, x + 2, y + 2)), HEZ_W, .9)
    elif view == "east":
        p.add(ell((28, 32, 76, 74)), HEZ, HEAD_OW)
        p.add(poly([(48, 62), (66, 60), (77, 57), (74, 66), (56, 70)]), (96, 48, 52), HEAD_OW)
        p.add(ell((55, 34, 67, 46)), HEZ_L, HEAD_OW); p.add(ell((58, 37, 64, 43)), (230, 200, 90), 0); p.add(ell((60.5, 37.6, 62.5, 42.4)), INK, 0)
        for x, y in [(42, 44), (40, 56), (50, 52)]:
            p.add(ell((x - 2, y - 2, x + 2, y + 2)), HEZ_W, .9)
    else:
        p.add(ell((26, 30, 74, 74)), HEZ, HEAD_OW)
        p.add(ell((30, 50, 70, 72)), HEZ_D, 0)
        for x, y in [(40, 40), (58, 42), (50, 56), (36, 58), (64, 60)]:
            p.add(ell((x - 2.2, y - 2.2, x + 2.2, y + 2.2)), HEZ_W, .9)
    return p


def nabassu_fangs(view):
    p = Pic()
    if view == "south":
        p.add(poly([(34, 46), (42, 42), (50, 45), (58, 42), (66, 46), (60, 49), (50, 47.5), (40, 49)]), NAB_D, HEAD_OW)
        for s in (S, mir):
            p.add(tent(s([(45, 64), (44, 72), (45.5, 78)]), 3.0, .6), TUSK, 1.2)
            horn(p, s([(41, 36), (38, 32), (37, 28)]), 4.0, 1.0, color=NAB_L, dark=NAB_D, tip=(170, 170, 170))
        for x in (43.5, 56.5):
            p.add(ell((x - 2.4, 51.5, x + 2.4, 55)), (210, 64, 44), 1.0)
    elif view == "east":
        p.add(poly([(50, 43), (60, 41), (68, 45), (64, 49), (52, 48)]), NAB_D, HEAD_OW)
        p.add(tent([(64, 64), (64, 72), (66, 78)], 3.0, .6), TUSK, 1.2)
        horn(p, [(47, 37), (45, 32), (45.5, 27)], 4.0, 1.0, color=NAB_L, dark=NAB_D, tip=(170, 170, 170))
        p.add(ell((61, 51.5, 65.5, 55)), (210, 64, 44), 1.0)
    else:
        for s in (S, mir):
            horn(p, s([(41, 36), (38, 32), (37, 28)]), 4.0, 1.0, color=NAB_L, dark=NAB_D, tip=(170, 170, 170))
    return p


# ================= 身体附件 =================
def bat_wing(p, root, tips, mem, mem_d, bone, bone_d, glow=None, sgn=-1, ow=HEAD_OW):
    rx, ry = root
    elbow = (rx + sgn * 14 * (abs(tips[0][0] - rx) / 44), tips[0][1] + 4)
    pts = [(rx, ry), elbow, tips[0]]
    for a, b in zip(tips, tips[1:]):
        mx, my = (a[0] + b[0]) / 2, (a[1] + b[1]) / 2
        pts += [(mx - sgn * 4, my - 2), b]
    pts += [(rx + sgn * 6, ry + 18)]
    p.add(poly(pts), mem, ow)
    p.add(poly([elbow] + tips[1:] + [(rx + sgn * 6, ry + 18), (rx + sgn * 2, ry + 6)]), mem_d, 0)
    p.add(("line", [(rx, ry), elbow, tips[0]], 2.4), bone, ow)
    for t in tips[1:]:
        p.add(("line", [elbow, t], 1.4), bone, 1.0)
    p.add(tent([elbow, (elbow[0] + sgn * 2, elbow[1] - 5)], 2.2, .6), bone_d, 1.0)
    if glow:
        for t in tips:
            p.add(ell((t[0] - 1, t[1] - 1, t[0] + 1, t[1] + 1)), glow, 0)


def wings(view, span, mem, mem_d, bone, bone_d, glow=None):
    p = Pic()
    k = span / 44.0
    if view in ("south", "north"):
        for sgn in (-1, 1):
            rt = (50 + sgn * (4 if view == "south" else 3), 38)
            tips = [(50 + sgn * 44 * k, 38 - 26 * k), (50 + sgn * 47 * k, 38), (50 + sgn * 40 * k, 38 + 22 * k), (50 + sgn * 26 * k, 38 + 34 * k)]
            bat_wing(p, rt, tips, mem, mem_d, bone, bone_d, glow, sgn=sgn)
    else:
        tips = [(44 - 36 * k, 38 - 28 * k), (44 - 40 * k, 38 - 4 * k), (44 - 34 * k, 38 + 18 * k), (44 - 20 * k, 38 + 32 * k)]
        bat_wing(p, (44, 38), tips, mem, mem_d, bone, bone_d, glow, sgn=-1)
    return p


def succubus_tail(view):
    p = Pic()
    if view == "south":
        pts = [(58, 76), (66, 82), (72, 90), (76, 98)]
    elif view == "east":
        pts = [(38, 74), (30, 80), (24, 88), (18, 92)]
    else:
        pts = [(50, 76), (54, 86), (60, 94), (66, 100)]
    p.add(poly(base.taper(pts, 3.2, 1.4)), VIOLET_D, HEAD_OW)
    tx, ty = pts[-1]
    p.add(poly([(tx, ty - 3), (tx + 4, ty + 1), (tx, ty + 6), (tx - 4, ty + 1)]), VIOLET, HEAD_OW)
    return p


# ================= 夸塞魔（256，三向 + 干尸） =================
def quasit(view, dry=False):
    p = Pic()
    skin, skin_d = ((140, 128, 110), (110, 100, 86)) if dry else ((112, 72, 82), (82, 50, 60))
    wm, wmd = ((150, 140, 124), (120, 110, 96)) if dry else (VIOLET, VIOLET_D)
    if view in ("south", "north"):
        for sgn in (-1, 1):
            bat_wing(p, (50 + sgn * 4, 46), [(50 + sgn * 30, 26), (50 + sgn * 33, 44), (50 + sgn * 27, 60), (50 + sgn * 16, 68)], wm, wmd, skin_d, INK, sgn=sgn, ow=OW)
        p.add(poly(base.taper([(54, 70), (64, 78), (68, 88)], 3, 1.2)), skin_d, OW)
        p.add(ell((38, 48, 62, 74)), skin, OW)
        p.add(ell((38, 62, 62, 74)), skin_d, 0)
        for sgn in (-1, 1):
            p.add(tent([(50 + sgn * 7, 70), (50 + sgn * 9, 80)], 4, 3), skin_d, OW)
            p.add(tent([(50 + sgn * 11, 56), (50 + sgn * 16, 66)], 3.4, 2.4), skin, OW)
        p.add(ell((39, 30, 61, 52)), skin, OW)
        for sgn in (-1, 1):
            p.add(tent([(50 + sgn * 6, 33), (50 + sgn * 10, 24), (50 + sgn * 9, 19)], 3.6, .8), HORN, OW)
            p.add(poly([(50 + sgn * 10, 38), (50 + sgn * 18, 34), (50 + sgn * 12, 43)]), skin_d, OW)  # 尖耳
        if view == "south":
            for x in (45, 55):
                p.add(ell((x - 2.4, 39, x + 2.4, 43)), (240, 200, 70) if not dry else (60, 54, 48), 1.0)
            p.line([(45, 47), (50, 48.5), (55, 47)], 1.2, INK)
    else:
        bat_wing(p, (48, 46), [(22, 24), (16, 42), (22, 58), (34, 66)], wm, wmd, skin_d, INK, sgn=-1, ow=OW)
        p.add(poly(base.taper([(40, 68), (30, 76), (24, 86)], 3, 1.2)), skin_d, OW)
        p.add(ell((38, 50, 62, 74)), skin, OW)
        p.add(tent([(48, 70), (47, 80)], 4, 3), skin_d, OW); p.add(tent([(56, 70), (58, 80)], 4, 3), skin, OW)
        p.add(tent([(56, 56), (64, 64)], 3.4, 2.4), skin, OW)
        p.add(ell((46, 30, 68, 52)), skin, OW)
        p.add(tent([(55, 33), (57, 24), (54, 19)], 3.6, .8), HORN, OW)
        p.add(poly([(50, 38), (42, 34), (48, 43)]), skin_d, OW)
        p.add(ell((61, 38, 65, 42)), (240, 200, 70) if not dry else (60, 54, 48), 1.0)
    return p


# ================= 物品 =================
def soul_coin():
    p = Pic()
    for i, (cx, cy) in enumerate([(40, 62), (60, 58), (50, 44)]):
        p.add(ell((cx - 20, cy - 20, cx + 20, cy + 20)), IRON, OW)
        p.add(ell((cx - 15, cy - 15, cx + 15, cy + 15)), IRON_D, 0)
        p.add(ell((cx - 7, cy - 9, cx + 7, cy + 7)), (150, 150, 170), 1.4)  # 尖叫的脸
        p.add(ell((cx - 4, cy - 5, cx - 1.5, cy - 2)), INK, 0); p.add(ell((cx + 1.5, cy - 5, cx + 4, cy - 2)), INK, 0)
        p.add(ell((cx - 2.2, cy + 0, cx + 2.2, cy + 5)), INK, 0)
    return p


def demon_blood():
    p = Pic()
    p.add(poly([(42, 22), (58, 22), (58, 34), (70, 48), (70, 80), (30, 80), (30, 48), (42, 34)]), (190, 200, 206), OW)
    p.add(poly([(32, 54), (68, 54), (68, 78), (32, 78)]), (110, 22, 30), 0)
    p.add(poly([(32, 54), (68, 54), (68, 60), (32, 60)]), (150, 40, 44), 0)
    p.add(poly([(40, 14), (60, 14), (60, 23), (40, 23)]), LEATHERB, OW)
    for x, y in [(50, 48), (44, 42), (56, 38)]:
        p.add(ell((x - 2, y - 2, x + 2, y + 2)), (200, 190, 190), 0)
    return p


def demonomicon(open_=False):
    p = Pic()
    if not open_:
        p.add(poly([(24, 20), (74, 16), (78, 80), (28, 84)]), LEATHERB, OW)
        p.add(poly([(24, 20), (30, 19), (34, 83), (28, 84)]), LEATHERB_D, 0)
        p.add(ell((44, 36, 66, 60)), ABYSS, 2.0)
        pts = [(55 + 9 * math.cos(math.radians(-90 + i * 144)), 48 + 9 * math.sin(math.radians(-90 + i * 144))) for i in range(6)]
        p.add(("line", pts, 1.6), EMBERGLOW, 0)
        for y in (26, 74):
            p.add(poly([(36, y - 2), (70, y - 4), (70, y), (36, y + 2)]), (150, 120, 70), 1.0)
    else:
        p.add(poly([(10, 34), (50, 40), (50, 82), (10, 76)]), PARCH, OW)
        p.add(poly([(50, 40), (90, 34), (90, 76), (50, 82)]), PARCH_D, OW)
        for i in range(5):
            p.line([(16, 44 + i * 6), (44, 49 + i * 6)], 1.0, (120, 70, 60))
        p.add(ell((60, 46, 80, 66)), ABYSS, 1.6)
    return p


def true_name_scroll():
    p = Pic()
    p.add(poly([(26, 28), (74, 24), (76, 76), (28, 80)]), PARCH, OW)
    p.add(ell((20, 20, 34, 34)), PARCH_D, OW); p.add(ell((66, 70, 80, 84)), PARCH_D, OW)
    for i in range(4):
        p.line([(34, 38 + i * 9), (66, 35 + i * 9)], 1.4, (120, 40, 50))
    p.add(ell((42, 58, 58, 74)), ABYSS, 1.6)
    return p


def shackles():
    p = Pic()
    for cx, cy in [(36, 54), (64, 54)]:
        p.add(ell((cx - 15, cy - 15, cx + 15, cy + 15)), SILVER, OW)
        p.add(ell((cx - 9, cy - 9, cx + 9, cy + 9)), (0, 0, 0, 0) if False else (60, 60, 64), 0)
        p.add(ell((cx - 8, cy - 8, cx + 8, cy + 8)), (255, 255, 255), 0)
    img = p.img
    # 镂空：把内圈白色变透明
    px = img.load()
    for y in range(img.size[1]):
        for x in range(img.size[0]):
            c = px[x, y]
            if c[3] > 0 and c[0] > 250 and c[1] > 250 and c[2] > 250:
                px[x, y] = (0, 0, 0, 0)
    for i in range(4):
        x = 44 + i * 4
        p.add(ell((x - 2.6, 36 - (i % 2) * 2, x + 2.6, 42 - (i % 2) * 2)), IRON, 1.2)
    for cx in (36, 64):
        p.add(("line", arc(cx, 54, 12, 200, 260, 6), 1.2), (90, 160, 200), 0)
    return p


def gorged_fang():
    p = Pic()
    p.add(("line", arc(50, 30, 26, 20, 160, 16), 2.2), LEATHERB, 1.2)
    p.add(poly([(42, 46), (58, 46), (56, 58), (51, 84), (48, 84), (44, 58)]), TUSK, OW)
    p.add(poly([(51, 50), (56, 50), (54, 60), (50.5, 80)]), TUSK_D, 0)
    p.add(poly([(42, 42), (58, 42), (58, 48), (42, 48)]), IRON, OW)
    p.add(ell((47.5, 60, 52.5, 65)), (150, 80, 180), 0)
    return p


# ================= 建筑 =================
def rune_ring(p, cx, cy, r0, color, width, runes=12, rune_color=None):
    p.add(("line", arc(cx, cy, r0, 0, 360, 72), width), color, 1.2)
    rc = rune_color or color
    for i in range(runes):
        a = math.radians(i * 360 / runes)
        x, y = cx + (r0 - width * 1.6) * math.cos(a), cy + (r0 - width * 1.6) * math.sin(a)
        p.line([(x - 1.4, y - 1.6), (x + 1.4, y + 1.6)], 1.0, rc)
        p.line([(x + 1.4, y - 1.6), (x, y + 0.4)], 1.0, rc)


def abyss_circle(silver=False):
    p = Pic()
    edge = SILVER if silver else ABYSS_D
    p.add(("line", arc(50, 50, 46, 0, 360, 96), 3.2 if silver else 2.6), edge, 1.4)
    rune_ring(p, 50, 50, 40, ABYSS, 2.2, 18, ABYSS_L)
    pts = [(50 + 34 * math.cos(math.radians(-90 + i * 144)), 50 + 34 * math.sin(math.radians(-90 + i * 144))) for i in range(6)]
    p.add(("line", pts, 1.8), ABYSS, 1.0)
    p.add(("line", arc(50, 50, 14, 0, 360, 48), 1.6), ABYSS_L, 1.0)
    for pt in pts[:5]:
        p.add(ell((pt[0] - 2.4, pt[1] - 2.4, pt[0] + 2.4, pt[1] + 2.4)), EMBERGLOW, 1.0)
    if silver:
        p.add(("line", arc(50, 50, 48.2, 0, 360, 96), 1.0), (236, 238, 242), 0)
    return p


def binding_circle():
    p = Pic()
    p.add(("line", arc(50, 50, 44, 0, 360, 72), 2.6), SILVER_D, 1.4)
    rune_ring(p, 50, 50, 36, SILVER, 1.8, 12, (120, 160, 200))
    for i in range(4):
        a = math.radians(45 + i * 90)
        x, y = 50 + 44 * math.cos(a), 50 + 44 * math.sin(a)
        p.add(("line", [(x, y), (50 + 20 * math.cos(a), 50 + 20 * math.sin(a))], 2.0), IRON, 1.0)
        for k in range(3):
            t = .25 + k * .25
            cx, cy = x + (50 + 20 * math.cos(a) - x) * t, y + (50 + 20 * math.sin(a) - y) * t
            p.add(ell((cx - 2.4, cy - 2.4, cx + 2.4, cy + 2.4)), SILVER, 1.0)
    p.add(("line", arc(50, 50, 18, 0, 360, 48), 1.6), SILVER, 1.0)
    return p


def warding_glyph():
    p = Pic()
    p.add(("line", arc(50, 50, 40, 0, 360, 48), 3.6), SILVER, 1.4)
    p.add(("line", [(50, 16), (50, 84)], 3.4), SILVER, 1.4)
    p.add(("line", [(22, 40), (78, 40)], 3.4), SILVER, 1.4)
    p.add(ell((44, 34, 56, 46)), (160, 200, 240), 1.2)
    for i in range(6):
        a = math.radians(i * 60 + 30)
        p.add(ell((50 + 30 * math.cos(a) - 2, 50 + 30 * math.sin(a) - 2, 50 + 30 * math.cos(a) + 2, 50 + 30 * math.sin(a) + 2)), (200, 230, 250), 0)
    return p


# ================= 图标 =================
def icon_bg(p, color, dark):
    p.add(ell((8, 8, 92, 92)), dark, OW)
    p.add(ell((14, 14, 86, 86)), color, 0)


def head_icon(skin, extra, tusk=False):
    p = Pic()
    icon_bg(p, (70, 40, 50), (40, 22, 30))
    p.add(ell((30, 28, 70, 76)), skin, OW)
    extra(p)
    return p


def xeno_icon(kind):
    p = Pic()
    icon_bg(p, (90, 46, 58), (52, 24, 34))
    if kind == "Dretch":
        p.add(ell((30, 36, 70, 80)), DRETCH, OW); p.add(ell((36, 22, 64, 50)), DRETCH, OW)
        for s in (S, mir):
            p.add(tent(s([(42, 26), (40, 18)]), 4, 1), HORN, OW)
        p.add(ell((42, 32, 47, 37)), (200, 60, 40), 0); p.add(ell((53, 32, 58, 37)), (200, 60, 40), 0)
    elif kind == "Barlgura":
        p.add(ell((24, 22, 76, 82)), BARL, OW); p.add(poly([(28, 40), (50, 34), (72, 40), (70, 46), (30, 46)]), BARL_D, OW)
        p.add(ell((36, 54, 64, 76)), BARL_L, OW)
        for s in (S, mir):
            p.add(tent(s([(42, 72), (39, 62)]), 4, 1), TUSK, 1.2)
    elif kind == "Hezrou":
        p.add(ell((18, 26, 82, 82)), HEZ, OW); p.add(poly([(24, 62), (50, 70), (76, 62), (70, 72), (30, 72)]), (96, 48, 52), OW)
        for s in (S, mir):
            p.add(ell(r.sb(s, (28, 30, 44, 46))), HEZ_L, OW); p.add(ell(r.sb(s, (33, 35, 39, 41))), INK, 0)
    elif kind == "Succubus":
        for sgn in (-1, 1):
            p.add(poly([(50 + sgn * 10, 48), (50 + sgn * 40, 24), (50 + sgn * 38, 50), (50 + sgn * 28, 64)]), VIOLET, OW)
        p.add(ell((34, 26, 66, 70)), SUCC, OW)
        for s in (S, mir):
            horn(p, s([(42, 30), (38, 24), (40, 18), (44, 19)]), 4, 1, color=HORN, dark=HORN_D, tip=HORN_TIP)
        p.line([(44, 58), (50, 60), (56, 58)], 1.6, (120, 30, 50))
    elif kind == "Nabassu":
        for sgn in (-1, 1):
            p.add(poly([(50 + sgn * 10, 44), (50 + sgn * 42, 20), (50 + sgn * 38, 56), (50 + sgn * 24, 66)]), NAB_D, OW)
        p.add(ell((32, 26, 68, 74)), NAB, OW)
        for s in (S, mir):
            p.add(tent(s([(45, 60), (44, 72)]), 3.4, .8), TUSK, 1.2)
            p.add(ell(r.sb(s, (39, 40, 46, 46))), (210, 64, 44), 0)
    elif kind == "ShadowDemon":
        p.add(poly([(30, 84), (36, 40), (50, 22), (64, 40), (70, 84), (60, 76), (50, 84), (40, 76)]), SHADOW, OW)
        for x in (44, 56):
            p.add(ell((x - 3, 38, x + 3, 43)), PALEGLOW, 0)
    elif kind == "Cambion":
        for sgn in (-1, 1):
            p.add(poly([(50 + sgn * 10, 50), (50 + sgn * 30, 32), (50 + sgn * 30, 54), (50 + sgn * 22, 62)]), ABYSS_D, OW)
        p.add(ell((34, 26, 66, 72)), HUMAN, OW)
        for s in (S, mir):
            horn(p, s([(42, 30), (37, 26), (33, 18)]), 4.5, 1, color=HORN, dark=HORN_D, tip=HORN_TIP)
            p.add(ell(r.sb(s, (40, 44, 46, 49))), GOLDEYE, 0)
    return p


def simple_icon(draw):
    p = Pic()
    icon_bg(p, (84, 44, 60), (48, 24, 36))
    draw(p)
    return p


def g_core(p):
    pts = [(50 + 26 * math.cos(math.radians(-90 + i * 144)), 50 + 26 * math.sin(math.radians(-90 + i * 144))) for i in range(6)]
    p.add(("line", pts, 3.0), EMBERGLOW, 0); p.add(("line", arc(50, 50, 32, 0, 360, 48), 2.4), ABYSS_L, 0)


def g_undying(p):
    p.add(ell((32, 34, 68, 70)), (200, 190, 180), OW); p.line([(30, 30), (70, 74)], 4, (190, 50, 50))


def g_size(p, s):
    p.add(ell((50 - 14 * s, 76 - 40 * s, 50 + 14 * s, 76)), (200, 190, 180), OW); p.add(ell((50 - 8 * s, 76 - 52 * s, 50 + 8 * s, 76 - 38 * s)), (200, 190, 180), OW)


def g_stench(p, c):
    for i in range(3):
        p.add(("line", [(36 + i * 12, 74), (32 + i * 12, 60), (40 + i * 12, 48), (36 + i * 12, 30)], 3.4), c, 1.0)


def g_toil(p):
    p.add(tent([(30, 72), (62, 34)], 5, 4), (150, 110, 70), OW); p.add(poly([(56, 26), (74, 38), (66, 46), (52, 34)]), (150, 150, 150), OW)


def g_might(p):
    p.add(ell((28, 30, 72, 74)), BARL, OW); p.add(poly([(36, 40), (64, 40), (66, 64), (34, 64)]), BARL_L, OW)


def g_still(p):
    p.add(poly([(36, 80), (40, 40), (50, 26), (60, 40), (64, 80)]), (120, 120, 140), 0)
    p.add(("line", arc(50, 54, 30, 200, 340, 16), 2.2), (220, 220, 240), 0)


def g_brute(p):
    p.add(tent([(30, 70), (70, 30)], 6, 4), (160, 160, 165), OW); p.add(ell((60, 60, 72, 72)), (180, 30, 30), OW)


def g_hide(p):
    p.add(poly([(30, 30), (70, 30), (68, 64), (50, 78), (32, 64)]), HEZ, OW)
    for x, y in [(42, 42), (56, 44), (48, 58)]:
        p.add(ell((x - 3, y - 3, x + 3, y + 3)), HEZ_W, 1.0)


def g_nature(p):
    p.add(poly([(50, 76), (28, 52), (32, 36), (44, 34), (50, 42), (56, 34), (68, 36), (72, 52)]), SUCC, OW)


def g_dream(p):
    p.add(poly(arc(50, 50, 24, 60, 300, 24) + arc(58, 46, 18, 300, 60, 16)[::-1]), (230, 220, 160), OW)
    for x, y in [(66, 30), (72, 44)]:
        p.add(ell((x - 2, y - 2, x + 2, y + 2)), (250, 240, 200), 0)


def g_blood(p):
    p.add(poly([(50, 22)] + arc(50, 56, 18, -30, 210, 20)), (150, 30, 40), OW)


def g_fire(p):
    r.flame(p, 50, 78, 34, 52)


def g_presence(p):
    p.add(ell((30, 30, 70, 70)), (200, 160, 80), OW); p.add(poly([(50, 24), (56, 44), (76, 44), (60, 56), (66, 76), (50, 64), (34, 76), (40, 56), (24, 44), (44, 44)]), GOLDEYE, 1.2)


def g_devour(p):
    p.add(ell((30, 28, 70, 72)), (160, 140, 200), OW); p.add(poly([(34, 50), (66, 50), (60, 60), (40, 60)]), INK, 0)
    for s in (S, mir):
        p.add(tent(s([(42, 50), (43, 60)]), 3, .8), TUSK, 1.0)


def g_form(p):
    p.add(poly([(32, 80), (38, 42), (50, 26), (62, 42), (68, 80), (58, 72), (50, 80), (42, 72)]), SHADOW, OW)
    r.flame(p, 74, 34, 14, 18, outer=(250, 230, 160), inner=(255, 250, 220))


def a_leap(p):
    p.add(("line", [(24, 74), (40, 40), (60, 30), (76, 50)], 3.0), (230, 220, 200), 0)
    p.add(poly([(70, 46), (82, 54), (72, 58)]), (230, 220, 200), 0)
    p.add(ell((60, 64, 84, 76)), (150, 110, 70), OW)


def a_kiss(p):
    p.add(poly(arc(50, 52, 20, 180, 360, 16) + [(70, 52), (60, 62), (50, 58), (40, 62), (30, 52)]), (200, 60, 90), OW)


def a_guise(p):
    p.add(ell((26, 30, 62, 66)), HUMAN, OW); p.add(ell((40, 34, 76, 70)), SUCC, OW)
    p.add(poly([(50, 20), (54, 30), (46, 30)]), HORN, 1.0)


def a_gaze(p):
    p.add(poly(arc(50, 50, 30, 200, 340, 16) + arc(50, 30, 30, 20, 160, 16)), (220, 210, 190), OW)
    p.add(ell((40, 38, 60, 58)), (210, 64, 44), 1.2); p.add(ell((46, 44, 54, 52)), INK, 0)


def a_glide(p):
    for sgn in (-1, 1):
        p.add(poly([(50, 46), (50 + sgn * 32, 30), (50 + sgn * 26, 52), (50 + sgn * 14, 58)]), ABYSS_D, OW)
    p.add(("line", [(30, 74), (70, 66)], 2.0), (230, 220, 200), 0)


def a_ritual(p):
    p.add(("line", arc(50, 50, 30, 0, 360, 48), 2.4), ABYSS_L, 0)
    pts = [(50 + 24 * math.cos(math.radians(-90 + i * 144)), 50 + 24 * math.sin(math.radians(-90 + i * 144))) for i in range(6)]
    p.add(("line", pts, 2.0), EMBERGLOW, 0)


def faction_icon():
    p = Pic()
    p.add(poly([(50, 8), (88, 30), (80, 80), (50, 94), (20, 80), (12, 30)]), ABYSS_D, OW)
    for s in (S, mir):
        horn(p, s([(40, 42), (32, 32), (28, 20), (32, 12)]), 7, 1.6, color=HORN, dark=HORN_D, tip=HORN_TIP)
    p.add(ell((34, 38, 66, 74)), ABYSS, OW)
    for s in (S, mir):
        p.add(ell(r.sb(s, (39, 50, 46, 56))), EMBERGLOW, 0)
    return p


# ================= 清单 =================
def views(rel, fn):
    return {rel + "_" + v: (lambda v=v: fn(v)) for v in ("south", "east", "north")}


SMALL = {}
SMALL.update(views("Abyss/Horns/DretchHorns", lambda v: small_horns(HORN, HORN_D, HORN_TIP, v, .8)))
SMALL.update(views("Abyss/Horns/SuccubusHorns", lambda v: curled_horns(VIOLET_D, (40, 26, 48), VIOLET_L, v)))
SMALL.update(views("Abyss/Horns/CambionHorns", lambda v: swept_horns((96, 34, 36), (62, 20, 24), (190, 120, 90), v)))
SMALL.update(views("Abyss/Eyes/CambionEyes", lambda v: eyes(GOLDEYE, v, (255, 120, 60))))
SMALL.update(views("Abyss/Eyes/ShadowEyes", lambda v: eyes(PALEGLOW, v, (255, 255, 255))))
SMALL.update(views("Abyss/Heads/BarlguraFace", barlgura_face))
SMALL.update(views("Abyss/Heads/HezrouHead", hezrou_head))
SMALL.update(views("Abyss/Heads/NabassuFangs", nabassu_fangs))
SMALL.update(views("Abyss/Taint/TaintHorns", lambda v: small_horns((70, 50, 54), (44, 30, 34), (170, 90, 90), v, .7)))
SMALL.update(views("Abyss/Taint/TaintEyes", lambda v: eyes(REDEYE, v, (255, 170, 140))))
SMALL.update(views("Abyss/Wings/SuccubusWings", lambda v: wings(v, 34, VIOLET, VIOLET_D, (52, 34, 60), (34, 22, 40))))
SMALL.update(views("Abyss/Wings/CambionWings", lambda v: wings(v, 28, ABYSS, ABYSS_D, (60, 30, 34), (40, 20, 22))))
SMALL.update(views("Abyss/Wings/NabassuWings", lambda v: wings(v, 42, NAB, NAB_D, (44, 42, 46), (28, 26, 30), (210, 64, 44))))
SMALL.update(views("Abyss/Tails/SuccubusTail", succubus_tail))
SMALL.update({"Items/Abyss/SoulCoin": soul_coin, "Items/Abyss/DemonBlood": demon_blood, "Items/Abyss/Demonomicon": lambda: demonomicon(False),
              "Items/Abyss/DemonomiconOpen": lambda: demonomicon(True), "Items/Abyss/TrueNameScroll": true_name_scroll,
              "Items/Abyss/DimensionalShackles": shackles, "Items/Abyss/GorgedFang": gorged_fang,
              "LP/Abyss/WardingGlyph": warding_glyph, "Abyss/Faction": faction_icon})
BIG = {}
for v in ("south", "east", "north"):
    BIG["Abyss/Quasit/Quasit_" + v] = (lambda v=v: quasit(v))
    BIG["Abyss/Quasit/Dessicated_Quasit_" + v] = (lambda v=v: quasit(v, True))
BIG.update({"Abyss/Xenotypes/" + k: (lambda k=k: xeno_icon(k)) for k in ("Dretch", "Barlgura", "Hezrou", "Succubus", "Nabassu", "ShadowDemon", "Cambion")})
GENES = {"AbyssCore": g_core, "AbyssUndying": g_undying, "DretchStature": lambda p: g_size(p, .7), "DretchStench": lambda p: g_stench(p, (150, 160, 110)), "DretchToil": g_toil,
         "BarlguraStature": lambda p: g_size(p, 1.2), "BarlguraMight": g_might, "BarlguraStill": g_still, "BarlguraBrute": g_brute,
         "HezrouStature": lambda p: g_size(p, 1.3), "HezrouHide": g_hide, "HezrouStench": lambda p: g_stench(p, (130, 170, 90)),
         "SuccubusNature": g_nature, "SuccubusHorns": lambda p: [horn(p, s([(44, 64), (36, 50), (38, 36), (48, 32)]), 8, 2, color=VIOLET_D, dark=(40, 26, 48), tip=VIOLET_L) for s in (S, mir)],
         "SuccubusWings": lambda p: [p.add(poly([(50, 50), (50 + g * 36, 24), (50 + g * 32, 54), (50 + g * 20, 66)]), VIOLET, OW) for g in (-1, 1)],
         "SuccubusTail": lambda p: (p.add(poly(base.taper([(30, 74), (50, 60), (60, 40), (66, 28)], 5, 2)), VIOLET_D, OW), p.add(poly([(66, 20), (72, 28), (66, 36), (60, 28)]), VIOLET, OW)),
         "SuccubusDream": g_dream, "CambionBlood": g_blood, "CambionHorns": lambda p: [horn(p, s([(44, 66), (36, 52), (30, 36), (32, 22)]), 8, 2, color=(96, 34, 36), dark=(62, 20, 24), tip=(190, 120, 90)) for s in (S, mir)],
         "CambionFire": g_fire, "CambionPresence": g_presence, "NabassuStature": lambda p: g_size(p, 1.2), "NabassuDevour": g_devour, "ShadowDemonForm": g_form}
for k, f in GENES.items():
    BIG["Abyss/Genes/" + k] = (lambda f=f: simple_icon(f))
for k, f in {"Leap": a_leap, "Kiss": a_kiss, "Guise": a_guise, "Gaze": a_gaze, "Glide": a_glide, "Ritual": a_ritual}.items():
    BIG["Abyss/Abilities/" + k] = (lambda f=f: simple_icon(f))
HUGE = {"LP/Abyss/AbyssCircle": lambda: abyss_circle(False), "LP/Abyss/AbyssCircleSilver": lambda: abyss_circle(True), "LP/Abyss/BindingCircle": binding_circle}


# ================= 预览 =================
def figure(view, skin, body="Male", size=1.0, attach_head=(), attach_body=(), back_body=()):
    """示意的人形：先画背后的附件，再画身体和头，最后画头部附件。返回 128px 图。"""
    W = 128
    img = Image.new("RGBA", (W, W), (0, 0, 0, 0))
    for fn in back_body:
        img.alpha_composite(fn(view).img.resize((W, W), Image.LANCZOS))
    p = Pic()
    bw = {"Male": 14, "Thin": 10, "Hulk": 22, "Fat": 22}[body]
    p.add(poly([(50 - bw, 96), (50 - bw * 1.1, 62), (50 - bw * .7, 46), (50 + bw * .7, 46), (50 + bw * 1.1, 62), (50 + bw, 96)]), skin, OW)
    p.add(ell((34, 26, 66, 66)) if view != "east" else ell((36, 26, 68, 66)), skin, OW)
    img.alpha_composite(p.img.resize((W, W), Image.LANCZOS))
    for fn in attach_head:
        h = fn(view).img.resize((W, W), Image.LANCZOS)
        img.alpha_composite(h, (0, -10))
    if size != 1.0:
        n = int(W * size)
        big = img.resize((n, n), Image.LANCZOS)
        img = Image.new("RGBA", (max(W, n), max(W, n)), (0, 0, 0, 0))
        img.alpha_composite(big, ((img.size[0] - n) // 2, img.size[1] - n))
    return img


def preview(path):
    rows = [
        ("劣魔", dict(skin=DRETCH, body="Fat", size=.8, attach_head=[lambda v: small_horns(HORN, HORN_D, HORN_TIP, v, .8)])),
        ("巴路古拉", dict(skin=BARL, body="Hulk", size=1.3, attach_head=[barlgura_face])),
        ("哈兹魔", dict(skin=HEZ, body="Hulk", size=1.4, attach_head=[hezrou_head])),
        ("魅魔", dict(skin=SUCC, body="Thin", attach_head=[lambda v: curled_horns(VIOLET_D, (40, 26, 48), VIOLET_L, v)], back_body=[lambda v: wings(v, 34, VIOLET, VIOLET_D, (52, 34, 60), (34, 22, 40)), succubus_tail])),
        ("坎比恩", dict(skin=HUMAN, body="Male", attach_head=[lambda v: swept_horns((96, 34, 36), (62, 20, 24), (190, 120, 90), v), lambda v: eyes(GOLDEYE, v)], back_body=[lambda v: wings(v, 28, ABYSS, ABYSS_D, (60, 30, 34), (40, 20, 22))])),
        ("纳巴苏", dict(skin=NAB, body="Hulk", size=1.3, attach_head=[nabassu_fangs], back_body=[lambda v: wings(v, 42, NAB, NAB_D, (44, 42, 46), (28, 26, 30), (210, 64, 44))])),
        ("影魔", dict(skin=SHADOW, body="Thin", attach_head=[lambda v: eyes(PALEGLOW, v)])),
        ("腐化·恶魔之印", dict(skin=HUMAN, body="Male", attach_head=[lambda v: small_horns((70, 50, 54), (44, 30, 34), (170, 90, 90), v, .7), lambda v: eyes(REDEYE, v)])),
    ]
    cell = 180
    W = cell * 6
    H = cell * (len(rows) + 3)
    out = Image.new("RGBA", (W, H), (226, 220, 206, 255))
    d = ImageDraw.Draw(out)
    try:
        from PIL import ImageFont
        font = ImageFont.truetype("C:/Windows/Fonts/msyh.ttc", 22)
    except Exception:
        font = None
    for i, (name, kw) in enumerate(rows):
        y = i * cell
        d.text((10, y + 10), name, fill=(40, 30, 30), font=font)
        for j, v in enumerate(("south", "east", "north")):
            img = figure(v, **kw)
            out.alpha_composite(img.resize((min(170, img.size[0]), min(170, img.size[1])), Image.LANCZOS), (cell * (j + 1) + 5, y + 5))
    y = len(rows) * cell
    d.text((10, y + 10), "夸塞魔", fill=(40, 30, 30), font=font)
    for j, v in enumerate(("south", "east", "north")):
        out.alpha_composite(quasit(v).img.resize((150, 150), Image.LANCZOS), (cell * (j + 1) + 15, y + 15))
    out.alpha_composite(quasit("east", True).img.resize((150, 150), Image.LANCZOS), (cell * 4 + 15, y + 15))
    y += cell
    items = [soul_coin(), demon_blood(), demonomicon(), demonomicon(True), true_name_scroll(), shackles(), gorged_fang()]
    for j, it in enumerate(items):
        out.alpha_composite(it.img.resize((140, 140), Image.LANCZOS), (10 + j * 150, y + 20))
    y += cell
    for j, b in enumerate([abyss_circle(), abyss_circle(True), binding_circle(), warding_glyph(), faction_icon()]):
        out.alpha_composite(b.img.resize((160, 160), Image.LANCZOS), (10 + j * 190, y + 10))
    os.makedirs(os.path.dirname(path), exist_ok=True)
    out.convert("RGB").save(path)
    return path


if __name__ == "__main__":
    out = []
    for rel, fn in SMALL.items():
        out.append(fn().save(rel, 128))
    for rel, fn in BIG.items():
        out.append(fn().save(rel, 256))
    for rel, fn in HUGE.items():
        out.append(fn().save(rel, 512))
    out.append(preview(os.path.join(ROOT, "ArtSource", "Preview", "Abyss-1.54.0.png")))
    print(len(out), "files written")
