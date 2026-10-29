# 1.55.0 异界契约贴图：沿用 draw_race_art.py / draw_races_124.py / draw_abyss_154.py 的画法（粗黑轮廓、平涂、单侧暗面、低饱和）。
#  - Textures/Pact/{Horns,Heads,Wings,Tails,Body}/*_{south,east,north}：12 个人形单位的头部 / 身体附件（128px，对位同 draw_races_124.py 的画框）。
#  - Textures/Pact/{Imp,Pixie}：九狱小魔鬼、小精灵四向 + 干尸（256px）。
#  - Textures/Pact/{Xenotypes,Genes,Abilities,Patrons,Factions}：图标。
#  - Textures/Pact/Items：契约书、地狱钢叉、缚绳、狂猎号角、天界羽披、妖精银弓；Pact/Apparel/CelestialMantle：羽披穿着图（五种体型 × 三向）。
#  - Textures/Pact/Buildings：契约讲台（2×1）、地狱铸炉（3×2），三向。
#  - ArtSource/Preview/Pact-1.55.0.png：所有单位三向（示意的身体 + 附件）、动物、物品、建筑、图标。
# 用法：python -I ArtSource/RaceArt/draw_pact_155.py <模组根目录>
import math, os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import draw_race_art as base
import draw_races_124 as r
import draw_abyss_154 as ab
from draw_race_art import Pic, poly, ell, tent, arc, ellpts, mir, INK
from draw_minotaur_gith import horn, HEAD_OW, ring_e
from PIL import Image, ImageDraw

ROOT = sys.argv[1] if len(sys.argv) > 1 else os.getcwd()
base.TEX = os.path.join(ROOT, "Textures")
OW = base.OW
S = r.S

# ---------------- 调色（低饱和） ----------------
HELL = (140, 44, 38); HELL_D = (98, 28, 26); HELL_L = (186, 86, 70); BRIMSTONE = (236, 140, 60)
IRON = ab.IRON; IRON_D = ab.IRON_D; STEEL = (150, 152, 156); STEEL_D = (110, 112, 118)
BONEW = (222, 214, 190); BONEW_D = (180, 170, 144)
GOLD = (214, 184, 118); GOLD_D = (172, 140, 80); GOLD_L = (244, 222, 160)
FEY = (118, 160, 98); FEY_D = (84, 120, 70); FEY_L = (164, 200, 132); MOON = (206, 214, 236); MOON_D = (160, 170, 200)
BARK = (122, 92, 62); BARK_D = (88, 64, 42); LEAF = (118, 168, 78); LEAF_D = (82, 126, 52)
FUR = (134, 98, 66); FUR_D = (98, 70, 46); HOOF = (60, 48, 44)
CAP = (164, 40, 40); CAP_D = (118, 26, 26)
FEATHER_B = (52, 46, 52); FEATHER_BD = (30, 26, 32); FEATHER_BL = (96, 84, 92)
FEATHER_W = (240, 232, 210); FEATHER_WD = (206, 190, 150); FEATHER_WL = (252, 248, 236)
FEATHER_S = (206, 212, 224); FEATHER_SD = (160, 168, 186); FEATHER_SL = (238, 242, 250)
PARCH = ab.PARCH; PARCH_D = ab.PARCH_D; LEATHERB = ab.LEATHERB; LEATHERB_D = ab.LEATHERB_D
PALE = (232, 230, 226); PALE_D = (204, 200, 196)
HUMAN = ab.HUMAN; HUMAN_D = ab.HUMAN_D
WOODL = (190, 178, 160); WOODL_D = (150, 138, 120)  # 讲台用浅色（按材料着色）
SOULGREEN = (120, 230, 150); SOULGREEN_D = (70, 170, 100)


# ================= 头部附件 =================
def bone_skull(view):
    """骨魔：骷髅一样的头——高眉骨、深陷的眼窝（红点）、露在外面的牙。"""
    p = Pic()
    if view == "south":
        p.add(poly([(33, 44), (41, 39), (50, 41), (59, 39), (67, 44), (62, 48), (50, 46), (38, 48)]), BONEW, HEAD_OW)
        for s in (S, mir):
            p.add(ell(r.sb(s, (38, 48, 48, 57))), (40, 30, 34), HEAD_OW)
            p.add(ell(r.sb(s, (42, 51, 45, 54))), (230, 70, 50), 0)
        p.add(poly([(40, 63), (60, 63), (58, 70), (42, 70)]), BONEW, HEAD_OW)
        for x in range(42, 59, 3):
            p.line([(x, 63.5), (x, 69.5)], .8, (120, 110, 90))
        p.add(poly([(48, 57), (52, 57), (50, 61)]), (40, 30, 34), 0)
    elif view == "east":
        p.add(poly([(48, 42), (60, 40), (69, 45), (64, 49), (52, 47)]), BONEW, HEAD_OW)
        p.add(ell((58, 48, 67, 57)), (40, 30, 34), HEAD_OW); p.add(ell((61.5, 51, 64.5, 54)), (230, 70, 50), 0)
        p.add(poly([(58, 63), (72, 62), (71, 69), (59, 70)]), BONEW, HEAD_OW)
        for x in range(60, 71, 3):
            p.line([(x, 63.5), (x, 69)], .8, (120, 110, 90))
    else:
        for s in (S, mir):
            p.add(tent(s([(38, 42), (34, 36)]), 4, 1.2), BONEW_D, HEAD_OW)
    return p


def erinyes_marks(view):
    """艾林魔：脸上红黑的纹样（细线）。"""
    p = Pic()
    if view == "south":
        for s in (S, mir):
            p.add(tent(s([(38, 47), (42, 50), (44, 56), (43, 62)]), 1.6, .6), (120, 26, 30), 0)
            p.add(tent(s([(40, 66), (44, 62)]), 1.2, .5), (40, 24, 28), 0)
        p.add(tent([(50, 33), (50, 41)], 1.8, .6), (120, 26, 30), 0)
        p.add(poly([(47.5, 41), (52.5, 41), (50, 45)]), (40, 24, 28), 0)
    elif view == "east":
        p.add(tent([(60, 46), (63, 50), (65, 56), (64, 62)], 1.6, .6), (120, 26, 30), 0)
        p.add(tent([(66, 34), (68, 41)], 1.6, .6), (120, 26, 30), 0)
    return p


def amnizu_head(view):
    """阿姆尼祖：干瘪的颧骨、小角、尖耳。"""
    p = Pic()
    if view == "south":
        for s in (S, mir):
            p.add(poly(s([(36, 54), (40, 50), (44, 58), (40, 66)])), (140, 112, 78), 0)
            p.add(poly(s([(31, 50), (22, 44), (33, 58)])), (176, 150, 112), HEAD_OW)
            horn(p, s([(42, 36), (39, 31), (39, 26)]), 3.6, 1.0, color=(70, 52, 44), dark=(48, 36, 30), tip=(150, 120, 100))
        p.add(poly([(44, 70), (56, 70), (54, 73), (46, 73)]), (90, 60, 56), HEAD_OW)
    elif view == "east":
        p.add(poly([(56, 54), (62, 51), (64, 60), (60, 66)]), (140, 112, 78), 0)
        p.add(poly([(48, 50), (40, 42), (48, 58)]), (176, 150, 112), HEAD_OW)
        horn(p, [(54, 36), (55, 30), (57, 25)], 3.6, 1.0, color=(70, 52, 44), dark=(48, 36, 30), tip=(150, 120, 100))
    else:
        for s in (S, mir):
            p.add(poly(s([(31, 50), (22, 44), (33, 58)])), (176, 150, 112), HEAD_OW)
            horn(p, s([(42, 36), (39, 31), (39, 26)]), 3.6, 1.0, color=(70, 52, 44), dark=(48, 36, 30), tip=(150, 120, 100))
    return p


def satyr_horns(view):
    return ab.curled_horns((150, 128, 96), (104, 86, 62), (210, 196, 166), view)


def horned_devil_horns(view):
    return r.fiend_horns((88, 40, 36), (56, 24, 22), (196, 150, 120), view)


def dryad_leaves(view):
    """林仙：头发上的一圈新叶。"""
    p = Pic()
    def leaf(cx, cy, ang, s=1.0):
        pts = ellpts(cx, cy, 4.2 * s, 2.0 * s, ang, 16)
        p.add(poly(pts), LEAF, HEAD_OW)
        a = math.radians(ang)
        p.line([(cx - math.cos(a) * 3.4 * s, cy - math.sin(a) * 3.4 * s), (cx + math.cos(a) * 3.4 * s, cy + math.sin(a) * 3.4 * s)], .6, LEAF_D)
    if view in ("south", "north"):
        for i, a in enumerate(range(200, 341, 20)):
            x, y = 50 + 21 * math.cos(math.radians(a)), 52 + 21 * math.sin(math.radians(a))
            leaf(x, y, a + 90 + (15 if i % 2 else -15), 1.1)
    else:
        for i, a in enumerate(range(200, 321, 24)):
            x, y = 50 + 19 * math.cos(math.radians(a)), 52 + 20 * math.sin(math.radians(a))
            leaf(x, y, a + 90, 1.1)
    return p


def redcap_cap(view):
    """红帽：血红的尖帽，帽尖垂到一边。"""
    p = Pic()
    if view == "south":
        p.add(poly([(30, 46), (34, 30), (46, 18), (62, 14), (74, 20), (68, 24), (62, 22), (66, 32), (70, 46), (50, 42)]), CAP, HEAD_OW)
        p.add(poly([(56, 18), (62, 14), (74, 20), (68, 24), (62, 22), (66, 32), (70, 46), (60, 44), (60, 30)]), CAP_D, 0)
        p.add(("line", [(30, 46), (50, 42.5), (70, 46)], 3.2), (96, 22, 22), HEAD_OW)
    elif view == "east":
        p.add(poly([(32, 46), (36, 30), (48, 18), (60, 12), (52, 24), (64, 30), (70, 46), (50, 42)]), CAP, HEAD_OW)
        p.add(poly([(48, 18), (60, 12), (52, 24), (44, 28)]), CAP_D, 0)
        p.add(("line", [(32, 46), (50, 42.5), (70, 46)], 3.2), (96, 22, 22), HEAD_OW)
    else:
        p.add(poly([(30, 50), (34, 30), (46, 18), (62, 14), (74, 20), (68, 26), (66, 34), (70, 50), (50, 54)]), CAP_D, HEAD_OW)
    return p


def changeling_face(view):
    """调换儿：没画完的脸——一块盖住五官的苍白皮肤，只剩两道细缝。"""
    p = Pic()
    if view == "south":
        p.add(ell((36, 44, 64, 72)), PALE, 0)
        for x in (43.5, 56.5):
            p.line([(x - 2.4, 53.2), (x + 2.4, 53.2)], .9, (150, 148, 150))
        p.line([(47, 66), (53, 66)], .7, (170, 166, 166))
    elif view == "east":
        p.add(ell((56, 44, 72, 72)), PALE, 0)
        p.line([(61, 53.2), (65.5, 53.2)], .9, (150, 148, 150))
    return p


def wildhunt_antlers(view):
    """狂猎：和头长在一起的鹿角盔。"""
    p = Pic()
    col, dark, tip = (176, 152, 116), (128, 106, 76), (226, 214, 186)
    def antler(s, main, tines):
        horn(p, s(main), 4.6, 1.6, color=col, dark=dark, tip=tip)
        for t in tines:
            horn(p, s(t), 2.8, 1.0, color=col, dark=dark, tip=tip)
    if view in ("south", "north"):
        for s in (S, mir):
            antler(s, [(41, 36), (35, 28), (28, 20), (24, 9)], [[(36, 29), (40, 21), (41, 15)], [(30, 22), (22, 18), (17, 12)], [(27, 15), (31, 8)]])
        if view == "south":
            p.add(("line", arc(50, 60, 22, 210, 330, 18), 4.2), (120, 100, 76), HEAD_OW)
    else:
        antler(S, [(52, 36), (50, 27), (46, 18), (44, 7)], [[(50, 28), (56, 20), (58, 14)], [(47, 20), (40, 15), (36, 10)]])
        antler(S, [(46, 37), (42, 28), (36, 19), (33, 9)], [[(41, 27), (46, 20)], [(37, 20), (30, 16)]])
    return p


def deva_halo(view):
    return r.halo(view)


# ================= 身体附件（同原版身体画框，男性） =================
BX = {"south": (29.7, 33.6, 70.3, 84.4), "east": (32.8, 33.6, 67.2, 83.6), "north": (29.7, 33.6, 70.3, 84.4)}


def chain_links(p, pts, color=STEEL, dark=STEEL_D, step=3.4):
    """沿折线画一串链环（交替横竖）。"""
    seg = []
    for a, b in zip(pts, pts[1:]):
        L = math.hypot(b[0] - a[0], b[1] - a[1]); n = max(1, int(L / step))
        for k in range(n):
            t = k / n
            seg.append((a[0] + (b[0] - a[0]) * t, a[1] + (b[1] - a[1]) * t, math.degrees(math.atan2(b[1] - a[1], b[0] - a[0]))))
    for i, (x, y, ang) in enumerate(seg):
        if i % 2 == 0:
            p.add(ring_e(x, y, 2.4, 1.4, .9, ang), color, 1.0)
        else:
            p.add(ring_e(x, y, 1.6, 1.1, .7, ang + 90), dark, 1.0)


def chain_body(view):
    p = Pic()
    x0, y0, x1, y1 = BX[view]
    if view == "south":
        chain_links(p, [(x0 + 4, y0 + 8), (50, y0 + 26), (x1 - 4, y0 + 42)])
        chain_links(p, [(x1 - 4, y0 + 8), (50, y0 + 26), (x0 + 6, y0 + 40)])
        p.add(ell((47, y0 + 23, 53, y0 + 29)), IRON, 1.2)
        chain_links(p, [(x0 + 3, y0 + 34), (x0 - 2, y0 + 46), (x0 + 1, y0 + 54)])
    elif view == "north":
        chain_links(p, [(x0 + 4, y0 + 8), (x1 - 4, y0 + 42)]); chain_links(p, [(x1 - 4, y0 + 8), (x0 + 4, y0 + 42)])
    else:
        chain_links(p, [(x0 + 10, y0 + 8), (x1 - 6, y0 + 30), (x0 + 8, y0 + 44)])
        chain_links(p, [(x1 - 4, y0 + 30), (x1 + 4, y0 + 42), (x1 + 2, y0 + 52)])
    return p


def bone_tail(view):
    """骨魔的蝎尾：分节，末端翘起一根毒刺。"""
    p = Pic()
    if view == "south":
        pts = [(60, 74), (70, 76), (78, 70), (82, 58), (80, 46)]
    elif view == "east":
        pts = [(38, 72), (28, 76), (20, 70), (16, 58), (19, 46)]
    else:
        pts = [(50, 74), (52, 84), (60, 88), (68, 82), (70, 70)]
    p.add(tent(pts, 6.2, 3.2), BONEW, HEAD_OW)
    for i in range(1, len(pts) - 1):
        x, y = pts[i]
        p.add(ell((x - 2.6, y - 2.6, x + 2.6, y + 2.6)), BONEW_D, 1.0)
    tx, ty = pts[-1]; px, py = pts[-2]
    ang = math.atan2(ty - py, tx - px)
    tipx, tipy = tx + math.cos(ang) * 7, ty + math.sin(ang) * 7
    p.add(poly([(tx + math.cos(ang + 1.6) * 2.6, ty + math.sin(ang + 1.6) * 2.6), (tipx, tipy), (tx + math.cos(ang - 1.6) * 2.6, ty + math.sin(ang - 1.6) * 2.6)]), (100, 30, 34), HEAD_OW)
    return p


def satyr_legs(view):
    """萨特：腰下的一圈毛和一对羊蹄。"""
    p = Pic()
    x0, y0, x1, y1 = BX[view]
    w = x1 - x0; hip = y0 + (y1 - y0) * .55
    if view in ("south", "north"):
        edge = [(x0 + w * .04, hip)]
        for i in range(9):
            edge.append((x0 + w * (.04 + .92 * (i + .5) / 9), hip + (2.6 if i % 2 == 0 else -.4)))
        edge.append((x1 - w * .04, hip))
        body = edge + [(x1 - w * .08, y1 - 3), (50 + 2, y1 - 3), (50, y1 - 8), (50 - 2, y1 - 3), (x0 + w * .08, y1 - 3)]
        p.add(poly(body), FUR, HEAD_OW)
        p.add(poly([(50, hip + 2), (x1 - w * .04, hip), (x1 - w * .08, y1 - 3), (52, y1 - 3)]), FUR_D, 0)
        for cx in (x0 + w * .25, x1 - w * .25):
            p.add(poly([(cx - 4.6, y1 - 4), (cx + 4.6, y1 - 4), (cx + 4, y1 + 1.5), (cx - 4, y1 + 1.5)]), HOOF, HEAD_OW)
            p.line([(cx, y1 - 3.6), (cx, y1 + 1.2)], .9, (30, 24, 22))
    else:
        body = [(x0 + w * .1, hip), (x1 - w * .1, hip + 1), (x1 - w * .16, y1 - 3), (x0 + w * .2, y1 - 3)]
        p.add(poly(body), FUR, HEAD_OW)
        p.add(poly([(x0 + w * .55, y1 - 4), (x1 - w * .02, y1 - 4), (x1 - w * .04, y1 + 1.5), (x0 + w * .55, y1 + 1.5)]), HOOF, HEAD_OW)
    return p


def dryad_bark(view):
    """林仙：皮肤上的树皮纹路（几道深色曲线）。"""
    p = Pic()
    x0, y0, x1, y1 = BX[view]
    w, h = x1 - x0, y1 - y0
    cols = 4 if view != "east" else 3
    for i in range(cols):
        x = x0 + w * (.2 + .6 * i / max(1, cols - 1))
        pts = [(x, y0 + h * .15), (x + 1.6, y0 + h * .35), (x - 1.2, y0 + h * .55), (x + 1.0, y0 + h * .78)]
        p.add(tent(pts, 1.4, .6), BARK_D, 0)
        p.add(tent([(x, y0 + h * .35), (x + 4, y0 + h * .42)], 1.0, .4), BARK_D, 0)
    return p


def redcap_boots(view):
    p = Pic()
    x0, y0, x1, y1 = BX[view]
    w = x1 - x0
    xs = (x0 + w * .25, x1 - w * .25) if view != "east" else (x0 + w * .58,)
    for cx in xs:
        p.add(poly([(cx - 5.4, y1 - 10), (cx + 5.4, y1 - 10), (cx + 6.2, y1 + 1.5), (cx - 6.2, y1 + 1.5)]), IRON, HEAD_OW)
        p.add(("line", [(cx - 5.6, y1 - 6), (cx + 5.6, y1 - 6)], 1.6), IRON_D, 0)
        for dx in (-3.5, 0, 3.5):
            p.add(ell((cx + dx - .9, y1 - 2.4, cx + dx + .9, y1 - .6)), (200, 200, 204), 0)
    return p


def feather_pair(view, color, dark, light, span=44, top=14, bottom=74):
    p = Pic()
    if view == "south":
        r.feather_wing(p, (45, 38), (50 - span, top), (50 - span * .6, bottom), color, dark, light)
        r.feather_wing(p, (55, 38), (50 + span, top), (50 + span * .6, bottom), color, dark, light)
    elif view == "north":
        r.feather_wing(p, (47, 37), (50 - span, top - 2), (50 - span * .6, bottom), color, dark, light)
        r.feather_wing(p, (53, 37), (50 + span, top - 2), (50 + span * .6, bottom), color, dark, light)
        p.add(ell((44, 33, 56, 43)), dark, HEAD_OW)
    else:
        r.feather_wing(p, (44, 38), (44 - span * .82, top - 2), (44 - span * .55, bottom + 2), color, dark, light)
    return p


def erinyes_wings(view):
    return feather_pair(view, FEATHER_B, FEATHER_BD, FEATHER_BL, 40)


def deva_wings(view):
    return feather_pair(view, FEATHER_W, FEATHER_WD, FEATHER_WL, 44)


def planetar_wings(view):
    return feather_pair(view, FEATHER_S, FEATHER_SD, FEATHER_SL, 46, 8, 80)


def horned_wings(view):
    return ab.wings(view, 44, HELL, HELL_D, (70, 30, 28), (44, 18, 18), BRIMSTONE)


# ================= 动物（256，四向 + 干尸） =================
def imp(view, dry=False):
    """九狱小魔鬼：红皮、小蝠翼、细尾、尖角——比夸塞魔更“人形”一些。"""
    p = Pic()
    skin, skin_d = ((150, 128, 110), (118, 100, 86)) if dry else ((176, 64, 52), (130, 40, 34))
    wm, wmd = ((150, 140, 124), (120, 110, 96)) if dry else ((122, 40, 36), (84, 24, 22))
    east = view in ("east", "west")
    if not east:
        for sgn in (-1, 1):
            ab.bat_wing(p, (50 + sgn * 4, 46), [(50 + sgn * 26, 28), (50 + sgn * 30, 44), (50 + sgn * 24, 58), (50 + sgn * 14, 64)], wm, wmd, skin_d, INK, sgn=sgn, ow=OW)
        p.add(poly(base.taper([(54, 70), (62, 80), (70, 84)], 2.4, 1.0)), skin_d, OW)
        p.add(poly([(70, 81), (76, 84), (70, 88)]), skin_d, OW)
        p.add(ell((40, 50, 60, 74)), skin, OW); p.add(ell((40, 64, 60, 74)), skin_d, 0)
        for sgn in (-1, 1):
            p.add(tent([(50 + sgn * 6, 70), (50 + sgn * 7, 82)], 3.6, 2.6), skin_d, OW)
            p.add(tent([(50 + sgn * 9, 56), (50 + sgn * 15, 66)], 3.0, 2.2), skin, OW)
        p.add(ell((39, 30, 61, 52)), skin, OW)
        for sgn in (-1, 1):
            p.add(tent([(50 + sgn * 6, 33), (50 + sgn * 9, 24), (50 + sgn * 12, 20)], 3.4, .8), (54, 30, 30), OW)
            p.add(poly([(50 + sgn * 10, 39), (50 + sgn * 19, 36), (50 + sgn * 12, 44)]), skin_d, OW)
        if view == "south":
            for x in (45, 55):
                p.add(ell((x - 2.4, 39, x + 2.4, 43)), (250, 210, 80) if not dry else (60, 54, 48), 1.0)
            p.line([(45, 47), (50, 49), (55, 47)], 1.2, INK)
    else:
        ab.bat_wing(p, (48, 46), [(26, 26), (20, 42), (26, 56), (36, 62)], wm, wmd, skin_d, INK, sgn=-1, ow=OW)
        p.add(poly(base.taper([(40, 68), (30, 76), (24, 82)], 2.4, 1.0)), skin_d, OW)
        p.add(poly([(24, 79), (18, 82), (24, 86)]), skin_d, OW)
        p.add(ell((40, 50, 62, 74)), skin, OW)
        p.add(tent([(48, 70), (47, 82)], 3.6, 2.6), skin_d, OW); p.add(tent([(56, 70), (58, 82)], 3.6, 2.6), skin, OW)
        p.add(tent([(56, 56), (64, 64)], 3.0, 2.2), skin, OW)
        p.add(ell((46, 30, 68, 52)), skin, OW)
        p.add(tent([(55, 33), (57, 24), (60, 19)], 3.4, .8), (54, 30, 30), OW)
        p.add(poly([(50, 39), (41, 36), (48, 44)]), skin_d, OW)
        p.add(ell((61, 38, 65, 42)), (250, 210, 80) if not dry else (60, 54, 48), 1.0)
    if view == "west":
        p.img = p.img.transpose(Image.FLIP_LEFT_RIGHT)
    return p


def pixie(view, dry=False):
    """小精灵：巴掌大的人形，背后一对彩色蝶翼。"""
    p = Pic()
    skin, skin_d = ((170, 160, 150), (140, 130, 120)) if dry else ((236, 214, 196), (206, 182, 164))
    wing1, wing2 = ((170, 160, 150), (140, 130, 120)) if dry else ((150, 196, 226), (226, 156, 200))
    hair = (120, 110, 100) if dry else (238, 196, 110)
    east = view in ("east", "west")
    def wing(cx, cy, sgn, big=True):
        rx, ry = (16, 12) if big else (11, 8)
        oy = -8 if big else 8
        pts = ellpts(cx + sgn * rx * .8, cy + oy, rx, ry, sgn * (-25 if big else 25), 24)
        p.add(poly(pts), wing1 if big else wing2, OW)
        p.add(poly(ellpts(cx + sgn * rx * .9, cy + oy, rx * .45, ry * .45, 0, 16)), wing2 if big else wing1, 0)
    if not east:
        for sgn in (-1, 1):
            wing(50, 54, sgn, True); wing(50, 54, sgn, False)
        p.add(poly([(44, 54), (56, 54), (60, 76), (40, 76)]), (120, 170, 110) if not dry else skin_d, OW)  # 叶子小裙
        for sgn in (-1, 1):
            p.add(tent([(50 + sgn * 4, 74), (50 + sgn * 5, 86)], 3.2, 2.4), skin, OW)
        p.add(ell((40, 30, 60, 52)), skin, OW)
        p.add(poly([(39, 40), (42, 30), (50, 27), (58, 30), (61, 40), (56, 34), (44, 34)]), hair, OW)
        for sgn in (-1, 1):
            p.add(poly([(50 + sgn * 9, 40), (50 + sgn * 17, 33), (50 + sgn * 11, 44)]), skin_d, OW)
        if view == "south":
            for x in (45.5, 54.5):
                p.add(ell((x - 1.8, 40, x + 1.8, 44)), (60, 90, 120) if not dry else (70, 64, 58), 0)
    else:
        wing(46, 54, -1, True); wing(46, 54, -1, False)
        p.add(poly([(44, 54), (56, 54), (58, 76), (42, 76)]), (120, 170, 110) if not dry else skin_d, OW)
        p.add(tent([(48, 74), (47, 86)], 3.2, 2.4), skin_d, OW); p.add(tent([(53, 74), (55, 86)], 3.2, 2.4), skin, OW)
        p.add(ell((44, 30, 64, 52)), skin, OW)
        p.add(poly([(43, 40), (46, 30), (54, 27), (62, 31), (60, 36), (50, 34)]), hair, OW)
        p.add(poly([(48, 40), (40, 34), (46, 44)]), skin_d, OW)
        p.add(ell((58, 40, 61.6, 44)), (60, 90, 120) if not dry else (70, 64, 58), 0)
    if view == "west":
        p.img = p.img.transpose(Image.FLIP_LEFT_RIGHT)
    return p


# ================= 物品 =================
def pact_scroll():
    p = Pic()
    p.add(poly([(24, 30), (76, 24), (78, 72), (26, 78)]), PARCH, OW)
    p.add(ell((18, 22, 32, 36)), PARCH_D, OW); p.add(ell((70, 66, 84, 80)), PARCH_D, OW)
    for i in range(4):
        p.line([(32, 38 + i * 8), (68, 35 + i * 8)], 1.3, (90, 60, 50))
    p.line([(40, 66), (62, 63)], 1.6, (130, 30, 30))  # 签名线
    p.add(ell((52, 58, 66, 72)), HELL, 1.6)  # 蜡印
    p.add(poly([(59, 61), (62, 65), (59, 69), (56, 65)]), HELL_L, 0)
    p.add(("line", [(48, 20), (50, 28), (46, 34)], 2.2), (60, 50, 48), 1.0)  # 一截锁链
    return p


def pitchfork():
    p = Pic()
    p.add(tent([(22, 86), (64, 32)], 4.2, 3.6), (70, 48, 40), OW)  # 握柄
    p.add(tent([(26, 81), (34, 71)], 5, 5), (110, 40, 34), 0)
    head = [(58, 40), (60, 36), (72, 26), (76, 22)]
    p.add(poly([(56, 36), (70, 46), (76, 40), (62, 30)]), (54, 50, 54), OW)  # 叉横梁
    for dx, dy in ((-8, -6), (0, 0), (8, 6)):
        p.add(tent([(64 + dx, 36 + dy), (76 + dx, 22 + dy), (84 + dx, 12 + dy)], 3.2, .4), (64, 60, 66), OW)
    p.add(tent([(66, 34), (78, 20)], 1.0, .4), (120, 40, 32), 0)
    return p


def rope():
    p = Pic()
    for i in range(3):
        p.add(("line", arc(50, 52, 26 - i * 6, 0, 360, 48), 4.2), GOLD, 1.4)
        p.add(("line", arc(50, 52, 26 - i * 6, 20, 120, 16), 1.2), GOLD_L, 0)
    p.add(tent([(70, 66), (82, 80), (86, 88)], 4, 3), GOLD, 1.4)
    p.add(ell((80, 84, 90, 94)), GOLD_D, 1.4)
    return p


def hunt_horn():
    p = Pic()
    pts = [(22, 70), (36, 66), (52, 56), (64, 42), (70, 26)]
    p.add(tent(pts, 6, 22), (226, 214, 186), OW)
    p.add(tent(pts[2:], 6, 18), (196, 180, 146), 0)
    for t in (.25, .5, .75):
        x = 22 + (70 - 22) * t; y = 70 + (26 - 70) * t
        p.add(("line", [(x - 5, y - 4), (x + 5, y + 4)], 1.6), (90, 110, 120), 1.0)
    p.add(poly(ellpts(70, 26, 11, 4.5, -55, 24)), (120, 104, 80), OW)
    p.add(("line", [(28, 72), (36, 82), (48, 84), (60, 74)], 1.6), LEATHERB, 1.0)
    return p


def mantle_icon():
    p = Pic()
    p.add(poly([(30, 22), (50, 18), (70, 22), (84, 82), (50, 88), (16, 82)]), FEATHER_W, OW)
    p.add(poly([(50, 18), (70, 22), (84, 82), (56, 86), (60, 50)]), FEATHER_WD, 0)
    r.feather_rows(p, 20, 80, 70, 82, 2, FEATHER_W, FEATHER_WD, 6)
    p.add(ell((45, 20, 55, 30)), GOLD, 1.4)
    return p


def silver_bow():
    p = Pic()
    p.add(("line", arc(70, 50, 44, 125, 235, 30), 4.2), (196, 200, 208), OW)
    p.add(("line", arc(70, 50, 44, 150, 210, 18), 1.4), (240, 242, 248), 0)
    p.add(("line", [(44.8, 14), (44.8, 86)], .9), (220, 230, 250), 0)
    p.add(tent([(26, 48), (28, 52)], 6, 6), LEATHERB, 1.4)
    for y in (24, 76):
        p.add(poly(ellpts(46, y, 3, 5, 0, 12)), LEAF, 1.0)
    p.add(tent([(20, 50), (84, 50)], 1.4, 1.4), (150, 120, 80), 1.0)
    p.add(poly([(84, 47), (92, 50), (84, 53)]), (196, 200, 208), 1.2)
    return p


# ================= 建筑 =================
def lectern(view):
    """契约讲台（2×1）：书架讲台，读经架上摊着契约，锁链把它拴在架子上。浅色：按材料着色。"""
    p = Pic()
    if view in ("south", "north"):
        # 100 单位画布，用 y 25～75（裁成 2:1）
        p.add(poly([(6, 34), (94, 34), (94, 72), (6, 72)]), WOODL, OW)  # 底柜
        p.add(poly([(6, 60), (94, 60), (94, 72), (6, 72)]), WOODL_D, 0)
        for x in (18, 34, 66, 82):
            p.add(poly([(x - 6, 38), (x + 6, 38), (x + 6, 56), (x - 6, 56)]), (110, 80, 60), 1.2)  # 格子里的书
        if view == "south":
            p.add(poly([(36, 28), (64, 28), (68, 52), (32, 52)]), WOODL, OW)  # 斜面
            p.add(poly([(39, 31), (49.5, 33), (49.5, 50), (36, 48)]), PARCH, 1.2); p.add(poly([(50.5, 33), (61, 31), (64, 48), (50.5, 50)]), PARCH_D, 1.2)
            p.add(ell((53, 40, 59, 46)), HELL, 1.0)
        else:
            p.add(poly([(36, 28), (64, 28), (68, 52), (32, 52)]), WOODL_D, OW)
        chain_links(p, [(20, 36), (32, 44), (40, 48)], STEEL, STEEL_D, 3)
        chain_links(p, [(80, 36), (68, 44), (60, 48)], STEEL, STEEL_D, 3)
    else:
        p.add(poly([(34, 6), (66, 6), (66, 94), (34, 94)]), WOODL, OW)
        p.add(poly([(54, 6), (66, 6), (66, 94), (54, 94)]), WOODL_D, 0)
        for y in (20, 40, 60, 80):
            p.add(poly([(38, y - 6), (50, y - 6), (50, y + 6), (38, y + 6)]), (110, 80, 60), 1.2)
        p.add(poly([(42, 36), (58, 36), (60, 64), (40, 64)]), WOODL, OW)
        p.add(poly([(44, 39), (56, 39), (57, 61), (43, 61)]), PARCH, 1.0)
        chain_links(p, [(46, 20), (50, 36)], STEEL, STEEL_D, 3); chain_links(p, [(46, 80), (50, 64)], STEEL, STEEL_D, 3)
    return p


def forge(view):
    """地狱铸炉（3×2）：黑铁炉膛，炉口里烧着绿色的灵魂火。"""
    p = Pic()
    if view in ("south", "north"):
        # 用 y 16～84（裁成 3:2）
        p.add(poly([(6, 24), (94, 24), (94, 80), (6, 80)]), (58, 54, 58), OW)
        p.add(poly([(6, 66), (94, 66), (94, 80), (6, 80)]), (40, 36, 40), 0)
        for x in range(10, 92, 12):
            p.add(ell((x - 2, 27, x + 2, 31)), (110, 104, 110), 0)
        if view == "south":
            p.add(poly([(30, 36), (70, 36), (66, 64), (34, 64)]), (28, 22, 24), OW)  # 炉口
            r.flame(p, 50, 62, 26, 22, outer=SOULGREEN, inner=(210, 255, 220))
            p.add(poly([(74, 40), (90, 40), (90, 54), (74, 54)]), (90, 86, 92), OW)  # 铁砧
            p.add(poly([(74, 40), (90, 40), (94, 36), (70, 36)]), (120, 116, 122), OW)
        else:
            p.add(poly([(30, 40), (70, 40), (70, 60), (30, 60)]), (40, 36, 40), OW)
        for x in (12, 88):
            p.add(tent([(x, 24), (x, 10)], 6, 4), (48, 44, 48), OW)  # 烟囱角
            p.add(ell((x - 2.6, 8, x + 2.6, 13)), SOULGREEN_D, 0)
    else:
        p.add(poly([(24, 6), (80, 6), (80, 94), (24, 94)]), (58, 54, 58), OW)
        p.add(poly([(66, 6), (80, 6), (80, 94), (66, 94)]), (40, 36, 40), 0)
        p.add(poly([(34, 30), (62, 30), (62, 70), (34, 70)]), (28, 22, 24), OW)
        r.flame(p, 48, 66, 22, 28, outer=SOULGREEN, inner=(210, 255, 220))
    return p


# ================= 图标 =================
def icon(bg, dark):
    p = Pic()
    ab.icon_bg(p, bg, dark)
    return p


HELL_BG = ((118, 44, 40), (70, 24, 22)); FEY_BG = ((80, 112, 74), (46, 70, 42)); SKY_BG = ((176, 160, 112), (122, 104, 64)); PACT_BG = ((112, 86, 58), (66, 50, 34))


def bust(p, skin, dark=None):
    p.add(poly([(28, 92), (32, 70), (50, 64), (68, 70), (72, 92)]), dark or skin, OW)
    p.add(ell((34, 26, 66, 70)), skin, OW)


def xeno_icon(kind):
    bg = HELL_BG if kind in ("ChainDevil", "BoneDevil", "HornedDevil", "Erinyes", "Amnizu") else SKY_BG if kind in ("Deva", "Planetar") else FEY_BG
    p = icon(*bg)
    if kind == "ChainDevil":
        bust(p, (120, 112, 112)); chain_links(p, [(30, 76), (50, 84), (70, 76)]); chain_links(p, [(36, 38), (50, 30), (64, 38)])
        for x in (44, 56):
            p.add(ell((x - 2.6, 46, x + 2.6, 51)), (230, 80, 50), 0)
    elif kind == "BoneDevil":
        p.add(tent([(70, 84), (82, 62), (80, 40), (72, 30)], 6, 3), BONEW, OW)
        p.add(poly([(72, 30), (66, 22), (76, 26)]), (100, 30, 34), OW)
        bust(p, BONEW, BONEW_D)
        for x in (43, 57):
            p.add(ell((x - 4, 42, x + 4, 52)), (40, 30, 34), 1.2); p.add(ell((x - 1.4, 45.6, x + 1.4, 48.4)), (230, 70, 50), 0)
        p.add(poly([(42, 60), (58, 60), (56, 66), (44, 66)]), BONEW, 1.2)
    elif kind == "HornedDevil":
        for sgn in (-1, 1):
            p.add(poly([(50 + sgn * 10, 50), (50 + sgn * 42, 22), (50 + sgn * 38, 56), (50 + sgn * 24, 66)]), HELL_D, OW)
        bust(p, (132, 52, 46))
        for s in (S, mir):
            horn(p, s([(42, 30), (34, 22), (26, 18), (20, 8)]), 6.4, 1.4, color=(88, 40, 36), dark=(56, 24, 22), tip=(196, 150, 120))
            p.add(ell(r.sb(s, (40, 44, 46, 49))), BRIMSTONE, 0)
    elif kind == "Erinyes":
        for sgn in (-1, 1):
            r.feather_wing(p, (50 + sgn * 6, 50), (50 + sgn * 42, 18), (50 + sgn * 26, 80), FEATHER_B, FEATHER_BD, FEATHER_BL, OW)
        bust(p, (176, 160, 160))
        p.add(poly([(34, 36), (50, 22), (66, 36), (66, 56), (60, 40), (40, 40), (34, 56)]), (30, 26, 30), OW)
        for s in (S, mir):
            p.add(tent(s([(40, 48), (44, 56), (43, 62)]), 1.8, .6), (120, 26, 30), 0)
    elif kind == "Amnizu":
        bust(p, (176, 150, 112))
        for s in (S, mir):
            p.add(poly(s([(36, 44), (24, 36), (36, 52)])), (176, 150, 112), OW)
            horn(p, s([(42, 30), (40, 22), (41, 16)]), 4.4, 1.2, color=(70, 52, 44), dark=(48, 36, 30), tip=(150, 120, 100))
            p.add(poly(s([(38, 52), (43, 50), (45, 60), (41, 64)])), (140, 112, 78), 0)
        p.add(("line", [(44, 62), (50, 63), (56, 62)], 1.4), INK, 0)
    elif kind == "Satyr":
        bust(p, HUMAN, FUR)
        for s in (S, mir):
            horn(p, s([(42, 32), (36, 26), (32, 18), (38, 12), (44, 16)]), 6, 1.4, color=(150, 128, 96), dark=(104, 86, 62), tip=(210, 196, 166))
        p.add(poly([(42, 60), (50, 70), (58, 60)]), FUR, 1.2)
    elif kind == "Dryad":
        bust(p, (150, 132, 96), (122, 104, 74))
        p.add(poly([(32, 46), (36, 26), (50, 20), (64, 26), (68, 46), (70, 70), (62, 56), (38, 56), (30, 70)]), LEAF, OW)
        for i, a in enumerate(range(200, 341, 28)):
            x, y = 50 + 22 * math.cos(math.radians(a)), 46 + 22 * math.sin(math.radians(a))
            p.add(poly(ellpts(x, y, 4.4, 2.2, a + 90, 12)), LEAF_D, 1.0)
    elif kind == "Redcap":
        bust(p, (140, 122, 104))
        p.add(poly([(30, 40), (36, 22), (52, 10), (72, 12), (64, 22), (68, 40), (50, 36)]), CAP, OW)
        p.add(poly([(42, 60), (58, 60), (54, 66), (46, 66)]), (230, 220, 190), 1.0)
    elif kind == "Changeling":
        bust(p, PALE, PALE_D)
        for x in (43, 57):
            p.line([(x - 3, 47), (x + 3, 47)], 1.2, (150, 148, 150))
        p.add(ell((14, 30, 30, 50)), HUMAN, 1.2); p.add(ell((70, 30, 86, 50)), (120, 112, 100), 1.2)
    elif kind == "WildHunter":
        bust(p, (204, 212, 216))
        for s in (S, mir):
            horn(p, s([(42, 30), (34, 22), (26, 14), (22, 4)]), 4.6, 1.4, color=(176, 152, 116), dark=(128, 106, 76), tip=(226, 214, 186))
            horn(p, s([(33, 21), (38, 13)]), 3, 1.0, color=(176, 152, 116), dark=(128, 106, 76), tip=(226, 214, 186))
    elif kind == "Deva":
        for sgn in (-1, 1):
            r.feather_wing(p, (50 + sgn * 6, 50), (50 + sgn * 42, 18), (50 + sgn * 26, 80), FEATHER_W, FEATHER_WD, FEATHER_WL, OW)
        bust(p, (236, 214, 168))
        p.add(ring_e(50, 20, 14, 4.4, 2.6, 0), (250, 226, 140), OW)
    elif kind == "Planetar":
        for sgn in (-1, 1):
            r.feather_wing(p, (50 + sgn * 6, 50), (50 + sgn * 44, 14), (50 + sgn * 28, 84), FEATHER_S, FEATHER_SD, FEATHER_SL, OW)
        bust(p, (200, 208, 220))
        p.add(tent([(74, 88), (80, 20)], 5, 2), (220, 226, 236), OW)
    return p


def g(draw, bg=PACT_BG):
    def f():
        p = icon(*bg)
        draw(p)
        return p
    return f


def g_core(color, symbol):
    def d(p):
        p.add(ell((26, 26, 74, 74)), color, OW)
        symbol(p)
    return d


def sym_trident(p):
    p.add(tent([(50, 80), (50, 36)], 3.4, 3.4), INK, 0)
    for dx in (-10, 0, 10):
        p.add(tent([(50 + dx, 40), (50 + dx, 26)], 3, .6), INK, 0)
    p.add(("line", [(40, 40), (60, 40)], 3), INK, 0)


def sym_leaf(p):
    p.add(poly(ellpts(50, 50, 18, 9, -45, 24)), LEAF_D, 1.4)
    p.line([(38, 62), (62, 38)], 1.4, INK)


def sym_sun(p):
    for k in range(8):
        a = math.radians(k * 45)
        p.add(tent([(50 + math.cos(a) * 12, 50 + math.sin(a) * 12), (50 + math.cos(a) * 22, 50 + math.sin(a) * 22)], 3, .6), (250, 226, 140), 1.0)
    p.add(ell((40, 40, 60, 60)), (250, 236, 180), 1.4)


def g_size(scale, color):
    def d(p):
        ab.g_size(p, scale)
    return d


def g_chain(p):
    chain_links(p, [(22, 30), (50, 50), (78, 72)]); chain_links(p, [(22, 72), (50, 50), (78, 30)])


def g_skull_subvert(p):
    p.add(ell((34, 30, 66, 62)), BONEW, OW); p.add(poly([(40, 58), (60, 58), (58, 70), (42, 70)]), BONEW, OW)
    for x in (43, 57):
        p.add(ell((x - 4, 40, x + 4, 50)), (90, 30, 30), 0)
    chain_links(p, [(20, 80), (50, 74), (80, 80)])


def g_tail(p):
    p.add(tent([(26, 80), (50, 70), (70, 52), (74, 32)], 7, 3), BONEW, OW)
    p.add(poly([(70, 34), (76, 18), (80, 34)]), (100, 30, 34), OW)


def g_dread(p):
    p.add(ell((30, 30, 70, 70)), (40, 30, 34), OW)
    for x in (42, 58):
        p.add(ell((x - 4, 42, x + 4, 50)), (230, 70, 50), 0)
    p.add(poly([(40, 60), (50, 54), (60, 60), (50, 66)]), (230, 70, 50), 0)


def g_resolve(p):
    p.add(poly([(50, 20), (74, 30), (70, 60), (50, 80), (30, 60), (26, 30)]), (120, 26, 30), OW)
    p.add(tent([(36, 64), (64, 36)], 4, 2), (220, 220, 230), 1.2)


def g_mind(p):
    p.add(ell((26, 30, 74, 70)), (176, 150, 112), OW)
    p.add(tent([(70, 22), (54, 46)], 3, 1), INK, 1.0)
    p.line([(38, 50), (46, 46), (54, 54), (62, 48)], 1.6, (90, 60, 120))


def g_hooves(p):
    for cx in (38, 62):
        p.add(poly([(cx - 9, 40), (cx + 9, 40), (cx + 8, 70), (cx - 8, 70)]), FUR, OW)
        p.add(poly([(cx - 9, 70), (cx + 9, 70), (cx + 8, 80), (cx - 8, 80)]), HOOF, OW)


def g_resist(p):
    p.add(poly([(50, 20), (74, 30), (70, 60), (50, 80), (30, 60), (26, 30)]), (150, 196, 226), OW)
    p.add(("line", arc(50, 50, 12, 0, 360, 24), 3), (226, 156, 200), 0)


def g_revelry(p):
    p.add(poly([(36, 30), (64, 30), (58, 56), (42, 56)]), (200, 120, 140), OW)
    p.add(tent([(50, 56), (50, 74)], 3, 3), (200, 200, 200), 1.2); p.add(ell((38, 72, 62, 80)), (200, 200, 200), 1.2)


def g_music(p):
    for i, x in enumerate((34, 44, 54, 64)):
        p.add(tent([(x, 30 + i * 2), (x, 74)], 6, 6), (190, 160, 110), 1.2)


def g_bark(p):
    p.add(poly([(30, 22), (70, 22), (74, 80), (26, 80)]), BARK, OW)
    for x in (38, 50, 62):
        p.add(tent([(x, 26), (x + 2, 50), (x - 2, 76)], 2, 1), BARK_D, 0)


def g_bloodlust(p):
    r.drop(p, 50, 56, 14, (180, 30, 30), (230, 120, 120))


def g_stomp(p):
    p.add(poly([(34, 30), (60, 30), (62, 62), (74, 66), (74, 76), (30, 76)]), IRON, OW)
    for dx in (36, 50, 64):
        p.add(ell((dx - 2, 70, dx + 2, 74)), (220, 220, 224), 0)


def g_fluid(p):
    for i, c in enumerate([(236, 214, 196), PALE, (176, 150, 112)]):
        p.add(ell((22 + i * 14, 30, 50 + i * 14, 70)), c, 1.6)


def g_antlers(p):
    for s in (S, mir):
        horn(p, s([(46, 74), (36, 56), (28, 36), (24, 18)]), 6, 2, color=(176, 152, 116), dark=(128, 106, 76), tip=(226, 214, 186))
        horn(p, s([(34, 52), (22, 46)]), 4, 1.4, color=(176, 152, 116), dark=(128, 106, 76), tip=(226, 214, 186))


def g_lord(p):
    g_antlers(p)
    p.add(poly([(36, 74), (40, 62), (50, 70), (60, 62), (64, 74)]), GOLD, OW)


def g_prey(p):
    p.add(("line", arc(50, 50, 24, 0, 360, 36), 3), (230, 70, 50), 1.2)
    p.add(("line", arc(50, 50, 12, 0, 360, 24), 3), (230, 70, 50), 1.2)
    p.add(tent([(18, 50), (82, 50)], 1.6, 1.6), INK, 0); p.add(tent([(50, 18), (50, 82)], 1.6, 1.6), INK, 0)


def g_spear(p):
    p.add(tent([(22, 82), (70, 30)], 3.6, 3.6), (150, 120, 80), OW)
    p.add(poly([(66, 34), (80, 18), (74, 38)]), (206, 214, 236), OW)


def g_mace(p):
    p.add(tent([(26, 80), (56, 44)], 4, 4), (150, 120, 80), OW)
    p.add(ell((48, 24, 76, 52)), GOLD, OW)
    r.flame(p, 62, 30, 10, 10, outer=(250, 240, 180), inner=(255, 255, 230))


def g_sword(p):
    p.add(tent([(24, 84), (40, 66)], 4, 4), (150, 120, 80), OW)
    p.add(tent([(32, 70), (46, 80)], 3, 3), GOLD, 1.2)
    p.add(tent([(40, 66), (80, 18)], 7, 2), (220, 226, 236), OW)


def ability(draw, bg=PACT_BG):
    return g(draw, bg)


def a_blast(p):
    for i, (y, c) in enumerate(((40, (190, 150, 240)), (50, (220, 190, 255)), (60, (170, 130, 230)))):
        p.add(tent([(20, y), (50, y + 2 * (i - 1)), (80, 50)], 4.4 - i, 1.2), c, 1.2)
    p.add(ell((70, 40, 88, 60)), (240, 220, 255), 1.4)


def a_summon(color, dark, extra=None):
    def d(p):
        r.flame(p, 50, 82, 46, 30, outer=(80, 40, 60), inner=(140, 70, 90))
        bust_small = [(38, 78), (40, 58), (50, 52), (60, 58), (62, 78)]
        p.add(poly(bust_small), dark, OW); p.add(ell((40, 30, 60, 54)), color, OW)
        if extra:
            extra(p)
    return d


def a_horns_extra(p):
    for s in (S, mir):
        horn(p, s([(44, 34), (36, 26), (32, 16)]), 4, 1, color=(88, 40, 36), dark=(56, 24, 22), tip=(196, 150, 120))


def a_ice_extra(p):
    for s in (S, mir):
        horn(p, s([(44, 34), (38, 26), (38, 16)]), 4, 1, color=r.ICE, dark=r.ICE_D, tip=r.ICE_L)


def a_redcap_extra(p):
    p.add(poly([(38, 38), (42, 24), (56, 18), (66, 22), (60, 28), (62, 38)]), CAP, OW)


def a_veil(p):
    for i in range(4):
        p.add(("line", arc(50, 54, 30 - i * 6, 200, 340, 18), 3), (206, 214, 236), 1.0)
    r.snowflake(p, 50, 44, 10, MOON)


def a_touch(p):
    p.add(poly([(32, 76), (34, 48), (40, 40), (46, 46), (48, 30), (54, 30), (56, 46), (62, 42), (66, 50), (64, 76)]), (236, 214, 196), OW)
    sym_leaf(p)


def a_heal(p):
    p.add(poly([(32, 76), (34, 48), (40, 40), (46, 46), (48, 30), (54, 30), (56, 46), (62, 42), (66, 50), (64, 76)]), (236, 214, 196), OW)
    p.add(poly([(46, 18), (54, 18), (54, 26), (62, 26), (62, 34), (54, 34), (54, 42), (46, 42), (46, 34), (38, 34), (38, 26), (46, 26)]), (250, 236, 180), 1.4)


def a_chain(p):
    chain_links(p, [(18, 70), (40, 56), (62, 44), (80, 30)])
    p.add(poly([(76, 24), (88, 22), (84, 34)]), STEEL, OW)


def a_hellfire(p):
    r.flame(p, 54, 80, 40, 56, outer=(236, 120, 50), inner=(255, 210, 120))
    p.add(ell((24, 62, 40, 78)), (236, 120, 50), 1.2)


def a_mark(p):
    g_prey(p)
    r.feather_wing(p, (70, 26), (88, 12), (84, 34), FEATHER_B, FEATHER_BD, FEATHER_BL, 1.2, 4)


def a_entangle(p):
    rope_p = rope()
    p.img.alpha_composite(rope_p.img)


def a_charge(p):
    for s in (S, mir):
        horn(p, s([(42, 56), (34, 48), (30, 36), (36, 28), (44, 32)]), 7, 2, color=(150, 128, 96), dark=(104, 86, 62), tip=(210, 196, 166))
    for i in range(3):
        p.line([(14, 50 + i * 8), (28, 50 + i * 8)], 2, (230, 220, 200))


def a_stride(p):
    for cx in (30, 70):
        p.add(tent([(cx, 84), (cx, 52)], 7, 5), BARK, OW)
        p.add(ell((cx - 16, 20, cx + 16, 56)), LEAF, OW)
    p.add(("line", arc(50, 60, 20, 200, 340, 16), 2.4), (230, 250, 210), 0)
    p.add(poly([(68, 52), (74, 58), (64, 60)]), (230, 250, 210), 0)


def a_bind(p):
    p.add(tent([(50, 86), (50, 50)], 9, 6), BARK, OW)
    p.add(ell((24, 16, 76, 58)), LEAF, OW)
    p.add(poly([(50, 70), (40, 60), (42, 52), (50, 56), (58, 52), (60, 60)]), (230, 120, 140), 1.2)


def a_faces(p):
    p.add(ell((14, 28, 50, 72)), HUMAN, OW)
    p.add(ell((50, 28, 86, 72)), PALE, OW)
    for x in (24, 38):
        p.add(ell((x - 2, 44, x + 2, 48)), INK, 0)
    for x in (60, 74):
        p.line([(x - 3, 46), (x + 3, 46)], 1, (150, 148, 150))


def a_wrath(p):
    sym_sun(p)
    for k in range(6):
        a = math.radians(k * 60 + 30)
        p.add(tent([(50 + math.cos(a) * 24, 50 + math.sin(a) * 24), (50 + math.cos(a) * 36, 50 + math.sin(a) * 36)], 3, .6), (250, 236, 180), 1.0)


def a_banish(p):
    p.add(("line", arc(50, 50, 28, 0, 360, 36), 3.4), (250, 236, 180), 1.2)
    p.add(poly([(40, 66), (44, 40), (50, 30), (56, 40), (60, 66), (50, 60)]), (60, 30, 40), OW)
    p.add(tent([(26, 26), (74, 74)], 4, 4), (250, 236, 180), 1.2)


def a_deva(p):
    for sgn in (-1, 1):
        r.feather_wing(p, (50 + sgn * 4, 54), (50 + sgn * 36, 24), (50 + sgn * 22, 80), FEATHER_W, FEATHER_WD, FEATHER_WL, 1.4, 5)
    p.add(ell((42, 34, 58, 50)), (236, 214, 168), OW)
    p.add(ring_e(50, 28, 10, 3.2, 2, 0), (250, 226, 140), 1.2)


def patron_icon(kind):
    bgs = {"Asmodeus": HELL_BG, "Mephistopheles": ((70, 92, 120), (40, 54, 74)), "Dispater": ((86, 86, 92), (52, 52, 58)), "Glasya": ((128, 60, 92), (80, 34, 56)),
           "Titania": FEY_BG, "QueenOfAir": ((92, 104, 130), (56, 64, 84)), "Hags": ((84, 92, 60), (50, 56, 34))}
    p = icon(*bgs[kind])
    if kind in ("Asmodeus", "Mephistopheles", "Dispater", "Glasya"):
        skin = {"Asmodeus": (176, 64, 52), "Mephistopheles": (150, 186, 214), "Dispater": (140, 140, 146), "Glasya": (206, 140, 160)}[kind]
        bust(p, skin)
        for s in (S, mir):
            horn(p, s([(42, 30), (34, 22), (28, 14), (30, 6)]), 5.6, 1.4, color=(60, 40, 40), dark=(36, 24, 24), tip=(170, 130, 110))
        if kind == "Asmodeus":
            p.add(poly([(36, 30), (40, 20), (50, 26), (60, 20), (64, 30), (50, 32)]), GOLD, OW)
        elif kind == "Mephistopheles":
            r.snowflake(p, 50, 80, 8, (230, 240, 255))
        elif kind == "Dispater":
            p.add(poly([(32, 30), (36, 18), (44, 24), (50, 14), (56, 24), (64, 18), (68, 30)]), IRON, OW)
        else:
            p.add(tent([(66, 70), (80, 50)], 2.4, 1), GOLD, 1.0)
        for x in (44, 56):
            p.add(ell((x - 2.4, 44, x + 2.4, 49)), BRIMSTONE, 0)
    elif kind in ("Titania", "QueenOfAir"):
        bust(p, (236, 214, 196) if kind == "Titania" else (206, 214, 236))
        crown = (250, 220, 120) if kind == "Titania" else (220, 230, 250)
        p.add(poly([(32, 32), (36, 16), (44, 26), (50, 10), (56, 26), (64, 16), (68, 32), (50, 28)]), crown, OW)
        if kind == "Titania":
            sym_leaf(p)
        else:
            p.add(ell((36, 40, 64, 62)), (120, 130, 160), 0)  # 看不见的脸
    else:
        for i, x in enumerate((26, 50, 74)):
            p.add(ell((x - 13, 36 + (i % 2) * 6, x + 13, 70 + (i % 2) * 6)), (130, 150, 100), OW)
            p.add(poly([(x - 14, 44 + (i % 2) * 6), (x, 18 + (i % 2) * 6), (x + 14, 44 + (i % 2) * 6)]), (60, 56, 50), OW)
            p.add(ell((x - 2, 52 + (i % 2) * 6, x + 2, 56 + (i % 2) * 6)), (230, 220, 120), 0)
    return p


def faction_icon(kind):
    p = Pic()
    shape = [(50, 8), (88, 30), (80, 80), (50, 94), (20, 80), (12, 30)]
    if kind == "HellsLegion":
        p.add(poly(shape), HELL_D, OW); sym_trident(p)
        p.add(ell((40, 70, 60, 86)), BRIMSTONE, 1.2)
    elif kind == "FeyHunt":
        p.add(poly(shape), (60, 80, 70), OW); g_antlers(p)
    elif kind == "HellsBrokers":
        p.add(poly(shape), (90, 64, 40), OW)
        p.add(poly([(28, 30), (72, 26), (74, 70), (30, 74)]), PARCH, OW)
        for i in range(3):
            p.line([(34, 38 + i * 9), (66, 35 + i * 9)], 1.4, (90, 60, 50))
        p.add(ell((52, 56, 66, 70)), HELL, 1.4)
    elif kind == "FeyCourt":
        p.add(poly(shape), (90, 120, 86), OW)
        p.add(poly([(28, 60), (32, 32), (42, 46), (50, 24), (58, 46), (68, 32), (72, 60)]), (230, 230, 250), OW)
        p.add(ell((44, 64, 56, 76)), (150, 196, 226), 1.2)
    else:
        p.add(poly(shape), (110, 96, 80), OW)
        p.add(tent([(26, 26), (74, 74)], 5, 5), (200, 200, 204), 1.4); p.add(tent([(74, 26), (26, 74)], 5, 5), (200, 200, 204), 1.4)
        p.add(ell((40, 40, 60, 60)), (250, 236, 180), 1.4)
    return p


# ================= 天界羽披穿着图（五种体型 × 三向） =================
def tinted_cloak(body, view):
    p = r.cloak_worn(body, view)
    img = p.img
    px = img.load()
    tw = (252, 238, 196)
    for y in range(img.size[1]):
        for x in range(img.size[0]):
            c = px[x, y]
            if c[3] == 0:
                continue
            px[x, y] = (c[0] * tw[0] // 255, c[1] * tw[1] // 255, c[2] * tw[2] // 255, c[3])
    return p


# ================= 清单 =================
def views(rel, fn):
    return {rel + "_" + v: (lambda v=v: fn(v)) for v in ("south", "east", "north")}


SMALL = {}
SMALL.update(views("Pact/Body/ChainDevilChains", chain_body))
SMALL.update(views("Pact/Heads/BoneDevilSkull", bone_skull))
SMALL.update(views("Pact/Tails/BoneDevilTail", bone_tail))
SMALL.update(views("Pact/Horns/HornedDevilHorns", horned_devil_horns))
SMALL.update(views("Pact/Wings/HornedDevilWings", horned_wings))
SMALL.update(views("Pact/Heads/ErinyesMarks", erinyes_marks))
SMALL.update(views("Pact/Wings/ErinyesWings", erinyes_wings))
SMALL.update(views("Pact/Heads/AmnizuHead", amnizu_head))
SMALL.update(views("Pact/Horns/SatyrHorns", satyr_horns))
SMALL.update(views("Pact/Body/SatyrLegs", satyr_legs))
SMALL.update(views("Pact/Body/DryadBark", dryad_bark))
SMALL.update(views("Pact/Heads/DryadLeaves", dryad_leaves))
SMALL.update(views("Pact/Heads/RedcapCap", redcap_cap))
SMALL.update(views("Pact/Body/RedcapBoots", redcap_boots))
SMALL.update(views("Pact/Heads/ChangelingFace", changeling_face))
SMALL.update(views("Pact/Horns/WildHuntAntlers", wildhunt_antlers))
SMALL.update(views("Pact/Wings/DevaWings", deva_wings))
SMALL.update(views("Pact/Heads/DevaHalo", deva_halo))
SMALL.update(views("Pact/Wings/PlanetarWings", planetar_wings))
for body in ("Male", "Female", "Thin", "Hulk", "Fat"):
    SMALL.update({"Pact/Apparel/CelestialMantle/CelestialMantle_" + body + "_" + v: (lambda b=body, v=v: tinted_cloak(b, v)) for v in ("south", "east", "north")})
SMALL.update({"Pact/Items/PactScroll": pact_scroll, "Pact/Items/InfernalPitchfork": pitchfork, "Pact/Items/EntanglingRope": rope, "Pact/Items/WildHuntHorn": hunt_horn,
              "Pact/Items/CelestialMantle": mantle_icon, "Pact/Items/FeySilverBow": silver_bow})
SMALL.update({"Pact/Factions/" + k: (lambda k=k: faction_icon(k)) for k in ("HellsLegion", "FeyHunt", "HellsBrokers", "FeyCourt", "Truce")})
BIG = {}
for v in ("south", "east", "north", "west"):
    BIG["Pact/Imp/Imp_" + v] = (lambda v=v: imp(v)); BIG["Pact/Imp/Dessicated_Imp_" + v] = (lambda v=v: imp(v, True))
    BIG["Pact/Pixie/Pixie_" + v] = (lambda v=v: pixie(v)); BIG["Pact/Pixie/Dessicated_Pixie_" + v] = (lambda v=v: pixie(v, True))
BIG.update({"Pact/Xenotypes/" + k: (lambda k=k: xeno_icon(k)) for k in ("ChainDevil", "BoneDevil", "HornedDevil", "Erinyes", "Amnizu", "Satyr", "Dryad", "Redcap", "Changeling", "WildHunter", "Deva", "Planetar")})
BIG.update({"Pact/Patrons/" + k: (lambda k=k: patron_icon(k)) for k in ("Asmodeus", "Mephistopheles", "Dispater", "Glasya", "Titania", "QueenOfAir", "Hags")})
GENES = {
    "HellsCore": (g_core(HELL, sym_trident), HELL_BG), "FeyCore": (g_core(FEY_L, sym_leaf), FEY_BG), "CelestialCore": (g_core((236, 220, 170), sym_sun), SKY_BG),
    "ChainDevilStature": (lambda p: ab.g_size(p, 1.1), HELL_BG), "ChainDevilSubvert": (g_skull_subvert, HELL_BG), "BoneDevilStature": (lambda p: ab.g_size(p, 1.2), HELL_BG),
    "BoneDevilTail": (g_tail, HELL_BG), "BoneDevilDread": (g_dread, HELL_BG), "HornedDevilStature": (lambda p: ab.g_size(p, 1.3), HELL_BG),
    "ErinyesStature": (lambda p: ab.g_size(p, 1.0), HELL_BG), "ErinyesWings": (lambda p: [r.feather_wing(p, (50 + s * 4, 54), (50 + s * 38, 20), (50 + s * 22, 82), FEATHER_B, FEATHER_BD, FEATHER_BL, 1.4, 5) for s in (-1, 1)], HELL_BG),
    "ErinyesResolve": (g_resolve, HELL_BG), "AmnizuStature": (lambda p: ab.g_size(p, 1.15), HELL_BG), "AmnizuMind": (g_mind, HELL_BG),
    "SatyrHorns": (lambda p: [horn(p, s([(44, 70), (34, 56), (30, 40), (38, 30), (46, 36)]), 9, 2.4, color=(150, 128, 96), dark=(104, 86, 62), tip=(210, 196, 166)) for s in (S, mir)], FEY_BG),
    "SatyrHooves": (g_hooves, FEY_BG), "SatyrMagicResist": (g_resist, FEY_BG), "SatyrRevelry": (g_revelry, FEY_BG), "SatyrMusic": (g_music, FEY_BG),
    "DryadBark": (g_bark, FEY_BG), "RedcapStature": (lambda p: ab.g_size(p, .7), FEY_BG), "RedcapBloodlust": (g_bloodlust, FEY_BG), "RedcapStomp": (g_stomp, FEY_BG),
    "ChangelingFluid": (g_fluid, FEY_BG), "WildHunterAntlers": (g_antlers, FEY_BG), "WildHuntLord": (g_lord, FEY_BG), "WildHunterPrey": (g_prey, FEY_BG), "WildHunterSpear": (g_spear, FEY_BG),
    "DevaStature": (lambda p: ab.g_size(p, 1.15), SKY_BG), "DevaMace": (g_mace, SKY_BG), "PlanetarStature": (lambda p: ab.g_size(p, 1.4), SKY_BG), "PlanetarSword": (g_sword, SKY_BG),
}
for k, (f, bg) in GENES.items():
    BIG["Pact/Genes/" + k] = g(f, bg)
ABILITIES = {
    "EldritchBlast": (a_blast, PACT_BG), "SummonHornedDevil": (a_summon((132, 52, 46), HELL_D, a_horns_extra), HELL_BG), "SummonIceDevil": (a_summon(r.ICE, r.ICE_D, a_ice_extra), ((70, 92, 120), (40, 54, 74))),
    "SummonRedcaps": (a_summon((140, 122, 104), (100, 86, 72), a_redcap_extra), FEY_BG), "WinterVeil": (a_veil, ((92, 104, 130), (56, 64, 84))), "SummerTouch": (a_touch, FEY_BG),
    "ChainLash": (a_chain, HELL_BG), "HellfireHurl": (a_hellfire, HELL_BG), "HuntersMark": (a_mark, HELL_BG), "Entangle": (a_entangle, HELL_BG),
    "SatyrCharge": (a_charge, FEY_BG), "TreeStride": (a_stride, FEY_BG), "BindTree": (a_bind, FEY_BG), "Faces": (a_faces, FEY_BG),
    "HealingTouch": (a_heal, SKY_BG), "Wrath": (a_wrath, SKY_BG), "Banish": (a_banish, SKY_BG), "Deva": (a_deva, SKY_BG),
}
for k, (f, bg) in ABILITIES.items():
    BIG["Pact/Abilities/" + k] = ability(f, bg)


def save_rect(p, rel, w, h, box):
    """把 100 单位画布上 box 区域裁出来，存成 w×h。"""
    k = p.k
    img = p.img.crop((int(box[0] * k), int(box[1] * k), int(box[2] * k), int(box[3] * k))).resize((w, h), Image.LANCZOS)
    path = os.path.join(base.TEX, rel.replace("/", os.sep) + ".png")
    os.makedirs(os.path.dirname(path), exist_ok=True)
    img.save(path)
    return path


def buildings():
    out = []
    for v in ("south", "north"):
        out.append(save_rect(lectern(v), "Pact/Buildings/PactLectern_" + v, 256, 128, (0, 25, 100, 75)))
        out.append(save_rect(forge(v), "Pact/Buildings/InfernalForge_" + v, 384, 256, (0, 16.7, 100, 83.3)))
    out.append(save_rect(lectern("east"), "Pact/Buildings/PactLectern_east", 128, 256, (25, 0, 75, 100)))
    out.append(save_rect(forge("east"), "Pact/Buildings/InfernalForge_east", 256, 384, (16.7, 0, 83.3, 100)))
    return out


# ================= 预览 =================
def preview(path):
    rows = [
        ("链魔", dict(skin=(120, 112, 112), body="Hulk", size=1.1, attach_head=[], back_body=[chain_body])),
        ("骨魔", dict(skin=BONEW, body="Thin", size=1.3, attach_head=[bone_skull], back_body=[bone_tail])),
        ("角魔", dict(skin=(132, 52, 46), body="Hulk", size=1.4, attach_head=[horned_devil_horns], back_body=[horned_wings])),
        ("艾林魔", dict(skin=(176, 160, 160), body="Thin", attach_head=[erinyes_marks], back_body=[erinyes_wings])),
        ("阿姆尼祖", dict(skin=(176, 150, 112), body="Thin", size=1.2, attach_head=[amnizu_head])),
        ("萨特", dict(skin=HUMAN, body="Male", attach_head=[satyr_horns], back_body=[satyr_legs])),
        ("林仙", dict(skin=(150, 132, 96), body="Thin", attach_head=[dryad_leaves], back_body=[dryad_bark])),
        ("红帽", dict(skin=(140, 122, 104), body="Thin", size=.7, attach_head=[redcap_cap], back_body=[redcap_boots])),
        ("调换儿", dict(skin=PALE, body="Male", attach_head=[changeling_face])),
        ("狂猎骑手", dict(skin=(204, 212, 216), body="Male", attach_head=[wildhunt_antlers])),
        ("狄瓦", dict(skin=(236, 214, 168), body="Male", size=1.2, attach_head=[deva_halo], back_body=[deva_wings])),
        ("行星使", dict(skin=(200, 208, 220), body="Hulk", size=1.6, back_body=[planetar_wings])),
    ]
    cell = 180
    W = cell * 7
    H = cell * (len(rows) + 6)
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
            img = ab.figure(v, **kw)
            out.alpha_composite(img.resize((min(170, img.size[0]), min(170, img.size[1])), Image.LANCZOS), (cell * (j + 1) + 5, y + 5))
        if name in ("骨魔", "角魔"):
            pass
    y = len(rows) * cell
    d.text((10, y + 10), "九狱小魔鬼 / 小精灵", fill=(40, 30, 30), font=font)
    for j, v in enumerate(("south", "east", "north", "west")):
        out.alpha_composite(imp(v).img.resize((130, 130), Image.LANCZOS), (cell * (j + 1) - 20, y + 15))
    for j, v in enumerate(("south", "east", "north", "west")):
        out.alpha_composite(pixie(v).img.resize((110, 110), Image.LANCZOS), (cell * (j + 1) - 20 + 15, y + 100))
    y += cell + 40
    items = [pact_scroll(), pitchfork(), rope(), hunt_horn(), mantle_icon(), silver_bow()]
    for j, it in enumerate(items):
        out.alpha_composite(it.img.resize((140, 140), Image.LANCZOS), (10 + j * 160, y + 10))
    mant = tinted_cloak("Male", "north").img.resize((140, 140), Image.LANCZOS)
    out.alpha_composite(mant, (10 + 6 * 160, y + 10))
    y += cell
    lec = lectern("south").img.crop((0, 256, 1024, 768)).resize((300, 150), Image.LANCZOS)
    out.alpha_composite(lec, (10, y + 10))
    fo = forge("south").img.crop((0, 170, 1024, 854)).resize((300, 200), Image.LANCZOS)
    out.alpha_composite(fo, (330, y))
    for j, k in enumerate(("Asmodeus", "Mephistopheles", "Dispater", "Glasya", "Titania", "QueenOfAir", "Hags")):
        out.alpha_composite(patron_icon(k).img.resize((80, 80), Image.LANCZOS), (650 + (j % 4) * 90, y + (j // 4) * 90))
    y += cell + 30
    icons = [BIG["Pact/Abilities/" + k]() for k in ABILITIES] + [faction_icon(k) for k in ("HellsLegion", "FeyHunt", "HellsBrokers", "FeyCourt", "Truce")]
    for j, ic in enumerate(icons):
        out.alpha_composite(ic.img.resize((64, 64), Image.LANCZOS), (10 + (j % 18) * 68, y + (j // 18) * 70))
    os.makedirs(os.path.dirname(path), exist_ok=True)
    out.convert("RGB").save(path)
    return path


if __name__ == "__main__":
    out = []
    for rel, fn in SMALL.items():
        out.append(fn().save(rel, 128))
    for rel, fn in BIG.items():
        out.append(fn().save(rel, 256))
    out += buildings()
    out.append(preview(os.path.join(ROOT, "ArtSource", "Preview", "Pact-1.55.0.png")))
    print(len(out), "files written")
