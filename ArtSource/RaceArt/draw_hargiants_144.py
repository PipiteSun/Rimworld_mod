# 1.44.0 双头巨人、独眼巨人贴图（Bridges/HAR/Textures/HARGiants）：照 draw_race_art.py 的画法（粗黑轮廓、平涂、单侧暗面、低饱和）。
#  - 第二个头（三款：男 A/B、女 F）与它的头发：近白灰底色，游戏里按肤色/发色通道染色；三向 128px，与原版头同一画框（头心约在 (50,50)/100）。
#  - 独眼巨人头型 4 款（同样近白灰，按肤色染色，脸上只有粗眉骨）；独眼是单独的身体附件（本色，北向空图）。
#  - 双盔：主头穿戴贴图 + 第二个头的附件（钢铁本色），横梁朝另一个头；物品图标、独眼巨锤、独眼透镜；基因图标 8 个、异种型 2 个（256px）。
# 用法：python -I ArtSource/RaceArt/draw_hargiants_144.py <模组根目录>
import math, os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import draw_race_art as base
from draw_race_art import Pic, poly, ell, tent, arc, ellpts, INK, OW, WOOD, WOOD_D, STEEL, STEEL_D, GOLD, GOLD_D, GLASS
from PIL import Image, ImageDraw

ROOT = sys.argv[1] if len(sys.argv) > 1 else os.getcwd()
base.TEX = os.path.join(ROOT, "Bridges", "HAR", "Textures")
OUT = "HARGiants"
HOW = 2.0  # 头部贴图的轮廓宽度（128px 下约 2.5px，与原版头差不多）

# 染色底色：近白灰（游戏里乘上肤色/发色），暗面浅灰
TINT = (238, 236, 232); TINT_D = (198, 194, 190); TINT_DD = (168, 164, 160)
HAIR = (232, 230, 226); HAIR_D = (186, 182, 178)
# 低饱和本色
SKIN_E = (150, 138, 122); SKIN_E_D = (118, 108, 96)
SKIN_C = (168, 104, 82); SKIN_C_D = (132, 80, 64)
IRON = (126, 128, 126); IRON_D = (94, 96, 96); IRON_L = (172, 174, 170)
SCLERA = (238, 232, 216); IRIS = (176, 128, 62); IRIS_D = (128, 90, 44)
WOOL = (226, 220, 204); WOOL_D = (190, 182, 164)
NIGHT = (70, 78, 104); NIGHT_D = (50, 56, 78)
ANGER = (196, 86, 70)


def save(p, rel, size):
    return p.save(OUT + "/" + rel, size)


# ================= 头的轮廓（100 单位画布）：照原版 1.7 头（128px 画框里约 x 32～68、y 30～70，头心 (50,50)） =================
def head_rx(style):
    return 16.5 if style in ("F", "CF", "CG") else 19 if style in ("B", "CB") else 17.5


def head_shape(style, d):
    """返回头的多边形部件（原版头没有画耳朵，这里也不画）；style: A 男常规 / B 男宽脸 / F 女 / CA CB 独眼男 / CF CG 独眼女。"""
    fem = style in ("F", "CF", "CG")
    wide = style in ("B", "CB")
    if d == "east":
        parts = [poly(ellpts(48, 48, 16, 18)), poly([(35, 52), (39, 64), (48, 70), (57, 69), (63, 63), (65, 57), (68, 54), (65, 50), (63, 40)])]
        if style.startswith("C"):
            parts.append(poly([(54, 39), (66, 42), (67, 47), (56, 47)]))  # 突出的眉骨
        return parts
    rx = head_rx(style)
    jw = rx - (4.5 if fem else 1 if wide else 3)
    jaw = [(50 - rx + 0.5, 50), (50 - jw, 62), (50 - jw * .55, 68.5), (50, 70.5 if not fem else 69.5), (50 + jw * .55, 68.5), (50 + jw, 62), (50 + rx - 0.5, 50)]
    return [poly(ellpts(50, 48, rx, 18)), poly(jaw)]


def head_shade(style, d):
    """右侧（画面）暗面。"""
    if d == "east":
        return [poly([(32.5, 44), (36, 55), (41, 64), (48, 70), (43, 60), (39, 48), (37, 37)])]
    rx = head_rx(style)
    return [poly([(50 + rx * .55, 31.5)] + [(50 + rx * math.cos(math.radians(a)), 48 + 18 * math.sin(math.radians(a))) for a in range(-55, 50, 8)] + [(50 + rx * .6, 67), (50 + rx * .35, 58), (50 + rx * .45, 41)])]


def brow(style, d, p):
    """独眼巨人一道粗眉骨（独眼本身是身体附件）。"""
    if d == "south":
        p.add(poly([(37, 46), (43, 43), (50, 44), (57, 43), (63, 46), (57, 47.5), (50, 47), (43, 47.5)]), TINT_D, 0)
        p.line([(38, 46), (44, 43.5), (50, 44.5), (56, 43.5), (62, 46)], 1.4, TINT_DD)
    elif d == "east":
        p.line([(55, 41), (66, 43.5)], 1.6, TINT_DD)


def eyes(style, d, p):
    """第二个头的眼睛：照原版 1.7 头的画法（黑点 + 小高光，女性多一笔睫毛）。"""
    fem = style == "F"
    pts = [(43.5, 53), (56.5, 53)] if d == "south" else [(60.5, 52)] if d == "east" else []
    for x, y in pts:
        p.add(ell((x - 2.3, y - 2.3, x + 2.3, y + 2.3)), INK, 0)
        p.add(ell((x + .2, y - 1.4, x + 1.2, y - .4)), (250, 250, 250), 0)
        if fem:
            p.line([(x + 1.6, y - 1.8), (x + 3.2, y - 3.2)], .9, INK)


def draw_head(style, d, rel, rough=True):
    p = Pic()
    p.add(head_shape(style, d), TINT, HOW)
    p.add(head_shade(style, d), TINT_D, 0)
    if style.startswith("C"):
        brow(style, d, p)
    else:
        eyes(style, d, p)
    if rough and d == "south":
        # 粗犷：一道旧疤、一撇下巴的凹痕
        if style in ("A", "CA"):
            p.line([(59, 39), (62.5, 47)], 1.2, TINT_DD)
        if style in ("B", "CB"):
            p.line([(43, 64), (46.5, 67)], 1.2, TINT_DD)
    save(p, rel, 128)


# ================= 第二个头的头发（按发色染色） =================
def hair_parts(style, d):
    if d == "north":
        if style == "F":
            return [poly(ellpts(50, 45, 21, 22)), poly([(30, 50), (31, 78), (40, 84), (50, 80), (60, 84), (69, 78), (70, 50)])]
        if style == "B":
            return [poly(ellpts(50, 41, 20.5, 16))]
        return [poly(ellpts(50, 44, 21, 20)), poly([(30, 48), (34, 62), (40, 58), (44, 64), (50, 59), (56, 64), (60, 58), (66, 62), (70, 48)])]
    if d == "east":
        if style == "F":
            return [poly(ellpts(45, 40, 20, 15)), poly([(28, 40), (27, 74), (34, 82), (40, 70), (40, 52), (52, 34)])]
        if style == "B":
            return [poly([(32, 42), (36, 28), (44, 24), (48, 18), (54, 25), (62, 26), (66, 36), (58, 34), (40, 40)])]
        return [poly(ellpts(46, 38, 19, 14)), poly([(30, 40), (29, 56), (36, 60), (38, 50), (44, 44), (54, 40), (64, 41), (68, 36)])]
    # south
    if style == "F":
        return [poly(ellpts(50, 36, 21, 13)), poly([(29, 38), (27, 70), (33, 78), (36, 60), (38, 44)]), poly([(71, 38), (73, 70), (67, 78), (64, 60), (62, 44)])]
    if style == "B":
        return [poly([(32, 40), (36, 30), (42, 27), (46, 20), (50, 26), (54, 20), (58, 27), (64, 30), (68, 40), (60, 35), (50, 34), (40, 35)])]
    return [poly(ellpts(50, 34, 21, 12)), poly([(29, 36), (31, 46), (35, 41), (38, 47), (42, 41), (46, 45), (50, 40), (54, 45), (58, 41), (62, 47), (65, 41), (69, 46), (71, 36)])]


def draw_hair(style, d, rel):
    p = Pic()
    p.add(hair_parts(style, d), HAIR, HOW)
    # 发丝暗面（右侧）
    if d != "east":
        p.add(poly([(56, 24), (68, 30), (71, 40), (62, 36)]), HAIR_D, 0)
    else:
        p.add(poly([(30, 42), (36, 30), (40, 40), (34, 52)]), HAIR_D, 0)
    for x in (42, 50, 58):
        if d == "south":
            p.line([(x, 28), (x - 2, 36)], 1.0, HAIR_D)
    save(p, rel, 128)


# ================= 双盔 =================
def helm_parts(d):
    if d == "east":
        return [poly([(30, 50)] + [(48 + 20 * math.cos(math.radians(a)), 46 + 22 * math.sin(math.radians(a))) for a in range(180, 361, 10)] + [(70, 50), (66, 53), (34, 53)])]
    return [poly([(28, 52)] + [(50 + 22 * math.cos(math.radians(a)), 47 + 23 * math.sin(math.radians(a))) for a in range(180, 361, 10)] + [(72, 52), (70, 55), (30, 55)])]


def draw_helm(d, rel, stub):
    """stub: 横梁朝哪边（'l'/'r'/None）。"""
    p = Pic()
    if stub:  # 横梁：只露出一小截（两顶盔本来就挨在一起）
        x0, x1 = (22, 30) if stub == "l" else (70, 78)
        p.add(("rect", (x0, 41, x1, 45)), IRON_D, HOW)
    p.add(helm_parts(d), IRON, HOW)
    if d == "east":
        p.add(poly([(28, 48), (48, 24), (40, 46)]), IRON_D, 0)
        p.add(("rect", (31, 46, 69, 51)), IRON_D, HOW)
        p.add(poly([(62, 49), (68, 49), (67, 62), (63, 62)]), IRON, HOW)  # 护鼻
    else:
        p.add(poly([(56, 26), (70, 38), (72, 50), (62, 50), (60, 36)]), IRON_D, 0)
        p.add(("rect", (28, 47, 72, 53)), IRON_D, HOW)
        if d == "south":
            p.add(poly([(47, 49), (53, 49), (52.5, 64), (47.5, 64)]), IRON, HOW)  # 护鼻
        p.add(poly([(49, 25), (51, 25), (51, 47), (49, 47)]), IRON_L, 0)  # 盔脊
    for x in ((32, 42, 58, 68) if d != "east" else (36, 48, 60)):
        p.add(ell((x - 1.2, 48.8, x + 1.2, 51.2)), IRON_L, 0)
    save(p, rel, 128)


def twin_helm_icon():
    p = Pic()
    p.add(("rect", (40, 44, 60, 50)), IRON_D, OW)
    for cx in (28, 72):
        p.add(poly([(cx - 20, 58)] + [(cx + 20 * math.cos(math.radians(a)), cx * 0 + 54 + 24 * math.sin(math.radians(a))) for a in range(180, 361, 10)] + [(cx + 20, 58), (cx + 18, 62), (cx - 18, 62)]), IRON, OW)
        p.add(poly([(cx + 6, 32), (cx + 18, 44), (cx + 20, 58), (cx + 10, 58), (cx + 8, 42)]), IRON_D, 0)
        p.add(("rect", (cx - 20, 54, cx + 20, 60)), IRON_D, OW)
        p.add(poly([(cx - 2.5, 56), (cx + 2.5, 56), (cx + 2, 72), (cx - 2, 72)]), IRON, OW)
        p.add(poly([(cx - 1, 31), (cx + 1, 31), (cx + 1, 54), (cx - 1, 54)]), IRON_L, 0)
    return p


# ================= 独眼 =================
def draw_eye(d, rel):
    p = Pic()
    if d == "south":
        p.add(ell((42, 46, 58, 57)), SCLERA, HOW)
        p.add(ell((46.4, 47.6, 53.6, 55.4)), IRIS, 0)
        p.add(ell((48.6, 49.6, 51.4, 53.4)), INK, 0)
        p.add(ell((50.6, 48.8, 52.0, 50.2)), (250, 248, 240), 0)
        p.line([(41, 47.5), (45, 45), (50, 44.2), (55, 45), (59, 47.5)], 2.2, INK)
    elif d == "east":
        p.add(poly([(60, 46), (65.5, 48), (66, 53.5), (60.5, 55.5)]), SCLERA, HOW)
        p.add(poly([(63, 48), (65.6, 49.1), (65.8, 52.9), (63.2, 53.9)]), IRIS, 0)
        p.line([(59, 46), (66.5, 47.3)], 2.0, INK)
    save(p, rel, 128)


# ================= 物品 =================
def maul_icon():
    p = Pic()
    p.add(tent([(26, 86), (64, 34)], 7, 6.5), WOOD, OW)
    p.add(tent([(30, 82), (60, 40)], 2.2, 2.0), WOOD_D, 0)
    for t in (.25, .45):
        x, y = 26 + (64 - 26) * t, 86 + (34 - 86) * t
        p.add(tent([(x - 4, y - 3), (x + 4, y + 3)], 3, 3), (90, 70, 50), 1.6)
    # 锤头：斜放的大铁砧形
    c, s = math.cos(math.radians(-36)), math.sin(math.radians(-36))
    def R(pts, cx=66, cy=30):
        return [(cx + x * c - y * s, cy + x * s + y * c) for x, y in pts]
    p.add(poly(R([(-24, -11), (24, -11), (26, -8), (26, 8), (24, 11), (-24, 11), (-26, 8), (-26, -8)])), IRON, OW)
    p.add(poly(R([(-24, 2), (24, 2), (26, 8), (24, 11), (-24, 11), (-26, 8)])), IRON_D, 0)
    for x in (-14, 0, 14):
        p.add(poly(R([(x - 1.6, -11), (x + 1.6, -11), (x + 1.6, 11), (x - 1.6, 11)])), IRON_D, 0)
    return p


def lens_icon():
    p = Pic()
    p.add(ell((26, 26, 74, 74)), GOLD, OW)
    p.add(ell((33, 33, 67, 67)), GLASS, OW)
    p.add(poly([(40, 38), (52, 36), (40, 52)]), (232, 240, 244), 0)
    p.add(tent([(70, 40), (86, 30), (92, 16)], 4, 3), GOLD, OW)
    p.add(tent([(72, 64), (84, 76)], 4, 3), GOLD_D, OW)
    return p


# ================= 基因、异种型图标（256） =================
def bust(p, cx, skin, dark, w=18, top=30, eye=False):
    p.add([ell((cx - w, top, cx + w, top + w * 2.3))], skin, OW)
    p.add(poly([(cx + w * .3, top + 2)] + arc(cx, top + w * 1.15, w, -70, 70, 10) + [(cx + w * .4, top + w * 2.2), (cx + w * .6, top + w * 1.1)]), dark, 0)
    if eye:
        p.add(ell((cx - w * .45, top + w * .8, cx + w * .45, top + w * 1.3)), SCLERA, 2)
        p.add(ell((cx - w * .18, top + w * .85, cx + w * .18, top + w * 1.25)), IRIS, 0)
        p.add(ell((cx - w * .07, top + w * .95, cx + w * .07, top + w * 1.15)), INK, 0)


def shoulders(p, skin, dark):
    p.add(poly([(8, 100), (12, 80), (30, 70), (70, 70), (88, 80), (92, 100)]), skin, OW)
    p.add(poly([(50, 70), (70, 70), (88, 80), (92, 100), (54, 100)]), dark, 0)


def ettin_core():
    p = Pic()
    shoulders(p, SKIN_E, SKIN_E_D)
    bust(p, 32, SKIN_E, SKIN_E_D, 16, 24)
    bust(p, 68, SKIN_E, SKIN_E_D, 16, 24)
    for cx in (32, 68):
        p.line([(cx - 8, 40), (cx + 8, 40)], 2.4, INK)
    return p


def ettin_watch():
    p = Pic()
    p.add(ell((4, 4, 96, 96)), NIGHT, OW)
    p.add(poly(arc(70, 26, 12, 0, 360, 24)), (226, 214, 160), 0)
    p.add(poly(arc(76, 22, 11, 0, 360, 24)), NIGHT, 0)
    bust(p, 32, SKIN_E, SKIN_E_D, 15, 40)
    bust(p, 66, SKIN_E, SKIN_E_D, 15, 40)
    p.line([(24, 58), (30, 60), (36, 58)], 2.4, INK)  # 闭眼
    p.add(ell((60, 55, 72, 62)), SCLERA, 2)  # 睁眼
    p.add(ell((64, 56, 69, 61)), INK, 0)
    for i, (x, y) in enumerate(((14, 30), (20, 22), (26, 15))):
        s = 4 + i * 1.5
        p.line([(x, y), (x + s, y), (x, y + s), (x + s, y + s)], 1.8, (226, 230, 240))
    return p


def ettin_grip():
    p = Pic()
    for sgn in (-1, 1):
        a, b = (50 - sgn * 34, 88), (50 + sgn * 24, 18)
        p.add(tent([a, b], 7, 10), WOOD, OW)
        p.add(tent([(a[0] + sgn * 2, a[1] - 4), (b[0] - sgn * 1, b[1] + 8)], 2, 3), WOOD_D, 0)
    for x in (22, 78):
        p.add(ell((x - 9, 64, x + 9, 82)), SKIN_E, OW)
        p.add(poly([(x, 64), (x + 9, 70), (x + 6, 82), (x, 82)]), SKIN_E_D, 0)
    return p


def ettin_quarrel():
    p = Pic()
    bust(p, 28, SKIN_E, SKIN_E_D, 17, 34)
    bust(p, 72, SKIN_E, SKIN_E_D, 17, 34)
    p.line([(20, 46), (36, 50)], 2.6, INK)
    p.line([(64, 50), (80, 46)], 2.6, INK)
    p.line([(22, 66), (34, 64)], 2.4, INK)
    p.line([(66, 64), (78, 66)], 2.4, INK)
    # 中间的火花
    p.add(poly([(50, 18), (55, 34), (64, 30), (57, 44), (64, 52), (52, 50), (50, 62), (46, 50), (36, 52), (43, 44), (36, 30), (45, 34)]), ANGER, 2.2)
    return p


def ettin_xeno():
    p = Pic()
    shoulders(p, SKIN_E, SKIN_E_D)
    bust(p, 32, SKIN_E, SKIN_E_D, 16, 22)
    bust(p, 68, SKIN_E, SKIN_E_D, 16, 22)
    p.add(poly(ellpts(32, 26, 16, 7)), (98, 82, 66), OW)
    p.add(poly([(54, 24), (60, 14), (66, 20), (72, 12), (78, 20), (84, 24), (76, 26), (60, 26)]), (98, 82, 66), OW)
    return p


def cyclops_core():
    p = Pic()
    shoulders(p, SKIN_C, SKIN_C_D)
    bust(p, 50, SKIN_C, SKIN_C_D, 24, 14, eye=True)
    p.line([(36, 30), (50, 27), (64, 30)], 3, INK)
    return p


def cyclops_craft():
    p = Pic()
    # 铁砧
    p.add(poly([(14, 60), (78, 60), (88, 52), (92, 56), (84, 68), (66, 70), (66, 82), (78, 90), (22, 90), (34, 82), (34, 70), (18, 68)]), IRON, OW)
    p.add(poly([(50, 70), (66, 70), (66, 82), (78, 90), (54, 90)]), IRON_D, 0)
    # 锤子
    p.add(tent([(30, 46), (62, 18)], 5, 5), WOOD, OW)
    p.add(poly([(56, 6), (78, 26), (70, 34), (48, 14)]), IRON, OW)
    for x, y in ((40, 52), (46, 46), (52, 52)):
        p.add(ell((x - 2, y - 2, x + 2, y + 2)), (238, 200, 110), 1.2)
    return p


def sheep(p, cx, cy, s=1.0):
    p.add([ell((cx - 18 * s, cy - 10 * s, cx + 4 * s, cy + 10 * s)), ell((cx - 6 * s, cy - 12 * s, cx + 16 * s, cy + 8 * s)), ell((cx - 14 * s, cy - 2 * s, cx + 12 * s, cy + 14 * s))], WOOL, OW)
    p.add([ell((cx + 2 * s, cy - 2 * s, cx + 16 * s, cy + 10 * s))], WOOL_D, 0)
    p.add(ell((cx + 12 * s, cy - 10 * s, cx + 24 * s, cy + 2 * s)), (96, 88, 80), OW)
    for x in (-10, -2, 6):
        p.add(tent([(cx + x * s, cy + 12 * s), (cx + x * s, cy + 20 * s)], 2.6 * s, 2.2 * s), (96, 88, 80), 1.6)


def cyclops_shepherd():
    p = Pic()
    p.add(tent([(78, 92), (74, 30), (72, 16), (62, 10), (54, 16), (58, 22), (64, 18)], 4, 4), WOOD, OW)
    sheep(p, 36, 64, 1.2)
    return p


def cyclops_flat():
    p = Pic()
    p.add(ell((20, 34, 80, 66)), SCLERA, OW)
    p.add(ell((38, 38, 62, 62)), IRIS, 0)
    p.add(ell((45, 45, 55, 55)), INK, 0)
    p.line([(18, 36), (34, 26), (50, 22), (66, 26), (82, 36)], 3.4, INK)
    # 偏掉的准星
    for (a, b) in (((74, 6), (74, 22)), ((74, 30), (74, 46)), ((60, 26), (68, 26)), ((80, 26), (88, 26))):
        p.line([a, b], 2.6, ANGER)
    p.add(("line", [(74, 26), (74, 26.1)], 3), ANGER, 0)
    return p


def cyclops_xeno():
    p = Pic()
    shoulders(p, SKIN_C, SKIN_C_D)
    bust(p, 50, SKIN_C, SKIN_C_D, 22, 16, eye=True)
    p.line([(37, 32), (50, 29), (63, 32)], 3, INK)
    sheep(p, 18, 84, .55)
    return p


# ================= 生成 =================
def main():
    made = []
    for style, rel in (("A", "SecondHeadA"), ("B", "SecondHeadB"), ("F", "SecondHeadF")):
        for d in ("south", "east", "north"):
            draw_head(style, d, "Ettin/Addons/" + rel + "_" + d)
            draw_hair(style, d, "Ettin/Addons/" + rel.replace("Head", "Hair") + "_" + d)
    # 双盔：主头（穿戴贴图）横梁朝第二个头：南向在左、北向在右；第二个头的附件反过来。
    for d, stub in (("south", "l"), ("north", "r"), ("east", None)):
        draw_helm(d, "Worn/EttinTwinHelm_" + d, stub)
    for d, stub in (("south", "r"), ("north", "l"), ("east", None)):
        draw_helm(d, "Ettin/Addons/TwinHelmSecond_" + d, stub)
    for style, rel in (("CA", "Male_A"), ("CB", "Male_B"), ("CF", "Female_A"), ("CG", "Female_B")):
        for d in ("south", "east", "north"):
            draw_head(style, d, "Cyclops/Heads/" + rel + "_" + d, rough=style in ("CA", "CB"))
    for d in ("south", "east"):
        draw_eye(d, "Cyclops/Addons/Eye_" + d)
    Pic().save(OUT + "/Cyclops/Addons/Eye_north", 128)  # 北向看不到眼睛：空图
    save(twin_helm_icon(), "Items/EttinTwinHelm", 128)
    save(maul_icon(), "Items/CyclopsMaul", 128)
    save(lens_icon(), "Items/CyclopsLens", 64)
    for fn, rel in ((ettin_core, "Ettin/Genes/Core"), (ettin_watch, "Ettin/Genes/Watch"), (ettin_grip, "Ettin/Genes/TwinGrip"), (ettin_quarrel, "Ettin/Genes/Quarrel"), (ettin_xeno, "Ettin/Xenotype"),
                    (cyclops_core, "Cyclops/Genes/Core"), (cyclops_craft, "Cyclops/Genes/Craft"), (cyclops_shepherd, "Cyclops/Genes/Shepherd"), (cyclops_flat, "Cyclops/Genes/FlatSight"), (cyclops_xeno, "Cyclops/Xenotype")):
        made.append(save(fn(), rel, 256))
    preview()


def preview():
    """预览：第二个头/头发（按灰褐皮、黑发染色）三款三向、双盔、独眼巨人头 + 眼睛四款三向、物品和图标。"""
    T = os.path.join(base.TEX, OUT)
    def load(rel):
        return Image.open(os.path.join(T, rel + ".png")).convert("RGBA")
    def tint(im, col):
        r, g, b, a = im.split()
        mul = Image.new("RGBA", im.size, col + (255,))
        from PIL import ImageChops
        rgb = ImageChops.multiply(im.convert("RGB"), mul.convert("RGB"))
        return Image.merge("RGBA", (*rgb.split(), a))
    W, cell = 1280, 128
    img = Image.new("RGBA", (W, 900), (58, 62, 58, 255))
    d = ImageDraw.Draw(img)
    x = 10
    for key in ("A", "B", "F"):
        for rot in ("south", "east", "north"):
            im = tint(load("Ettin/Addons/SecondHead" + key + "_" + rot), SKIN_E)
            im.alpha_composite(tint(load("Ettin/Addons/SecondHair" + key + "_" + rot), (70, 56, 44)))
            img.alpha_composite(im, (x, 10)); x += cell - 20
    x = 10
    for rot in ("south", "east", "north"):
        im = tint(load("Ettin/Addons/SecondHeadA_" + rot), SKIN_E)
        im.alpha_composite(load("Ettin/Addons/TwinHelmSecond_" + rot))
        img.alpha_composite(im, (x, 150))
        img.alpha_composite(load("Worn/EttinTwinHelm_" + rot), (x + 120, 150)); x += 250
    x = 10
    for key in ("Male_A", "Male_B", "Female_A", "Female_B"):
        for rot in ("south", "east", "north"):
            im = tint(load("Cyclops/Heads/" + key + "_" + rot), SKIN_C)
            im.alpha_composite(load("Cyclops/Addons/Eye_" + rot))
            img.alpha_composite(im, (x, 290)); x += 100
    x = 10
    for rel in ("Items/EttinTwinHelm", "Items/CyclopsMaul", "Items/CyclopsLens"):
        img.alpha_composite(load(rel).resize((128, 128)), (x, 430)); x += 140
    x = 10
    for rel in ("Ettin/Xenotype", "Ettin/Genes/Core", "Ettin/Genes/Watch", "Ettin/Genes/TwinGrip", "Ettin/Genes/Quarrel", "Cyclops/Xenotype", "Cyclops/Genes/Core", "Cyclops/Genes/Craft", "Cyclops/Genes/Shepherd", "Cyclops/Genes/FlatSight"):
        img.alpha_composite(load(rel).resize((120, 120)), (x, 580)); x += 126
    d.text((10, 870), "1.44.0 HARGiants preview: second heads + hair (tinted), twin helm, cyclops heads + eye, items, gene/xenotype icons", fill=(230, 230, 230, 255))
    out = os.path.join(ROOT, "ArtSource", "HARGiants-1.44.0-preview.png")
    img.save(out)
    print("preview", out)


if __name__ == "__main__":
    main()
