# 1.24.0 四个普通种族 + 四个传奇种族的贴图：沿用 draw_race_art.py / draw_minotaur_gith.py 的画法（粗黑轮廓、平涂、单侧暗面）。
# 用法：python -I ArtSource/RaceArt/draw_races_124.py <模组根目录>
# 对位：先在游戏里导出原版身体、头部、斗篷贴图量出画框（128px 画布）：
#   头部 Male_Average_Normal  x 41-87、y 39-89；猪鼻锚点 南 x55-73 y70-85、东 x75-91 y68-87
#   身体 Naked_Male 南/北 x38-90 y43-108、东 x42-86 y43-107；Naked_Hulk 南/北 x25-102 y32-127；Naked_Thin 南/北 x50-77 y43-109
# 下面的坐标都是 100 单位画布（×1.28 即 128px 像素）。导出代码用完已删除。
import math, os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import draw_race_art as base
import draw_minotaur_gith as mg
from draw_race_art import Pic, poly, ell, tent, arc, ellpts, mir, INK, BONE, STONE, STONE_D, WOOD, WOOD_D, GOLD, GOLD_D, STEEL, STEEL_D, RED, RED_D
from draw_minotaur_gith import star, horn, ring_e, HEAD_OW, SILVER, SILVER_D, SILVER_L, LEATHER, HUMAN, HUMAN_D

ROOT = sys.argv[1] if len(sys.argv) > 1 else os.getcwd()
base.TEX = os.path.join(ROOT, "Textures")
OW = base.OW

# ---------------- 调色 ----------------
EMBER = (176, 82, 52); EMBER_D = (138, 58, 36); FLAME = (250, 150, 52); FLAME_L = (255, 220, 110); FLAME_D = (214, 86, 34)
TIDE = (86, 146, 168); TIDE_D = (60, 112, 134); WATER = (120, 186, 228); WATER_L = (196, 232, 250); WATER_D = (70, 132, 186)
EARTH = (138, 122, 102); EARTH_D = (108, 94, 78); ROCK = (150, 140, 124); ROCK_D = (112, 104, 92); CRYSTAL = (150, 214, 196); CRYSTAL_D = (96, 168, 150)
SKY = (178, 196, 214); SKY_D = (140, 160, 182); WIND = (226, 238, 246); WIND_D = (176, 196, 214)
LOX = (150, 150, 155); LOX_D = (118, 118, 124); LOX_L = (176, 176, 182); IVORY = (240, 232, 208); IVORY_D = (206, 194, 162)
OGRE = (190, 160, 100); OGRE_D = (154, 126, 74); OGRE_L = (214, 188, 132)
FEATHER = (196, 160, 112); FEATHER_D = (150, 116, 76); FEATHER_L = (228, 204, 160); BEAK = (236, 186, 70); BEAK_D = (196, 142, 40)
ICE = (150, 196, 228); ICE_D = (106, 156, 196); ICE_L = (214, 238, 250); FROST = (236, 248, 255)
BALOR = (132, 38, 30); BALOR_D = (92, 24, 20); CHAR = (58, 42, 40); CHAR_D = (34, 26, 26); HELL = (255, 120, 40)
TITAN = (206, 164, 96); TITAN_D = (170, 128, 66); HALO = (250, 226, 140); HALO_D = (220, 182, 84); WHITE_HAIR = (238, 236, 230)
GENIE = (96, 150, 176); GENIE_D = (70, 116, 142); SMOKE = (186, 176, 214); SMOKE_D = (146, 134, 182)
BRASS = (212, 170, 80); BRASS_D = (166, 124, 48); BRASS_L = (244, 214, 132); GEM_R = (214, 56, 60); GEM_B = (70, 140, 220)
PALE = (238, 238, 238); PALE_D = (204, 204, 204); PALE_L = (252, 252, 252)  # 受肤色/材料着色的贴图用近白灰
S = lambda q: q


def sb(s, box):
    """按镜像函数 s 处理椭圆/矩形框（mir 时左右翻转）。"""
    x0, y0, x1, y1 = box
    return box if s is S else (100 - x1, y0, 100 - x0, y1)


def both(p, pts, color, ow=OW):
    p.add([poly(pts), poly(mir(pts))], color, ow)


def flame(p, cx, by, w, h, outer=FLAME, inner=FLAME_L, ow=OW):
    """一簇火苗：底部中心 (cx, by)，宽 w 高 h。"""
    pts = [(cx - w * .5, by), (cx - w * .55, by - h * .35), (cx - w * .3, by - h * .55), (cx - w * .22, by - h * .82), (cx - w * .05, by - h * .62),
           (cx + w * .05, by - h), (cx + w * .25, by - h * .6), (cx + w * .4, by - h * .72), (cx + w * .55, by - h * .35), (cx + w * .5, by)]
    p.add(poly(pts), outer, ow)
    p.add(poly([(cx - w * .28, by), (cx - w * .3, by - h * .3), (cx - w * .08, by - h * .5), (cx + w * .06, by - h * .7), (cx + w * .22, by - h * .4), (cx + w * .28, by)]), inner, 0)


def drop(p, cx, cy, r, color=WATER, light=WATER_L, ow=OW):
    p.add(poly([(cx, cy - r * 1.8)] + arc(cx, cy, r, -30, 210, 20)), color, ow)
    p.add(ell((cx - r * .55, cy - r * .5, cx - r * .15, cy + r * .1)), light, 0)


def snowflake(p, cx, cy, r, color=ICE_L, w=3.2):
    for k in range(3):
        a = math.radians(90 + k * 60)
        dx, dy = math.cos(a) * r, math.sin(a) * r
        p.add(("line", [(cx - dx, cy - dy), (cx + dx, cy + dy)], w), color, 1.6)
    for k in range(6):
        a = math.radians(90 + k * 60); bx, by = cx + math.cos(a) * r * .6, cy + math.sin(a) * r * .6
        for s in (-35, 35):
            b = a + math.radians(180 + s)
            p.add(("line", [(bx, by), (bx + math.cos(b) * r * .28, by + math.sin(b) * r * .28)], w * .7), color, 1.2)


def bolt(p, pts, color=HALO, ow=OW):
    p.add(poly(pts), color, ow)


def face(p, cx, cy, rx, ry, skin, dark, ow=OW):
    """通用的人形头：椭圆脸 + 右侧暗面。"""
    p.add(ell((cx - rx, cy - ry, cx + rx, cy + ry)), skin, ow)
    p.add(poly([(cx + rx * .35, cy - ry * .92)] + arc(cx, cy, rx, -60, 60, 12) + [(cx + rx * .5, cy + ry * .9), (cx + rx * .7, cy)]), dark, 0)


def eyes(p, cx, y, gap, r=2.6, color=INK):
    p.add([ell((cx - gap - r, y - r, cx - gap + r, y + r)), ell((cx + gap - r, y - r, cx + gap + r, y + r))], color, 0)


# ================= 头部附件（128px，同原版头部画框） =================
def trunk_south():
    # 从两眼之间垂下，越过下巴，末端向右微卷；受肤色着色，所以画成近白。
    p = Pic()
    pts = [(50, 52), (50.5, 62), (51, 72), (52.5, 81), (56, 87.5), (60, 88)]
    p.add(tent(pts, 12, 6.5), PALE, HEAD_OW)
    p.add(tent(pts[1:], 4.5, 2.4), PALE_D, 0)
    for y in (60, 66, 72, 78):
        p.line([(46.5 + (y - 60) * .08, y), (53.5 + (y - 60) * .12, y + .6)], .7, (150, 150, 150))
    p.add(ell((57, 85, 62.5, 90)), PALE_L, 1.2)
    return p


def trunk_east():
    p = Pic()
    pts = [(62, 52), (69, 57), (73, 66), (73.5, 76), (75.5, 85), (80, 89)]
    p.add(tent(pts, 11, 6), PALE, HEAD_OW)
    p.add(tent(pts[2:], 4, 2.2), PALE_D, 0)
    for y in (63, 69, 75, 81):
        p.line([(69.5 + (y - 63) * .05, y), (76.5 + (y - 63) * .05, y + .5)], .7, (150, 150, 150))
    p.add(ell((77, 86, 82.5, 91)), PALE_L, 1.2)
    return p


FIEND_HORN = [(39.5, 36), (34, 29), (30.5, 20), (31.5, 11), (36, 3.5)]


def fiend_horns(color, dark, tip, view):
    p = Pic()
    if view in ("south", "north"):
        for s in (S, mir):
            horn(p, s(FIEND_HORN), 7.4, 1.6, color=color, dark=dark, tip=tip)
            if view == "south":
                p.add(tent(s([(39.5, 36), (37, 33.5)]), 7.8, 7.4), dark, HEAD_OW)
    else:
        horn(p, [(55, 34), (55, 27), (58.5, 19), (63, 13.5)], 5.8, 1.4, color=dark, dark=dark, tip=tip)
        horn(p, [(46, 37), (41.5, 29), (40.5, 20), (43.5, 11), (49, 5)], 7.4, 1.6, color=color, dark=dark, tip=tip)
        p.add(tent([(46, 37), (43.5, 33.5)], 7.8, 7.4), dark, HEAD_OW)
    return p


def circlet(view):
    p = Pic()
    if view == "south":
        band = arc(50, 62, 24, 220, 320, 20)
        p.add(("line", band, 4.2), GOLD, HEAD_OW)
        p.add(("line", arc(50, 62, 24, 258, 282, 8), 1.4), (250, 228, 160), 0)
        p.add(poly([(50, 33.5), (54, 38), (50, 42.5), (46, 38)]), GEM_B, HEAD_OW)
        p.add(poly([(50, 34.8), (52.2, 38), (50, 38)]), (170, 210, 255), 0)
    elif view == "east":
        band = [(35.5, 45), (42, 40.5), (50, 38.2), (58, 37.6), (64.5, 38.6)]
        p.add(("line", band, 4.2), GOLD, HEAD_OW)
        p.add(poly([(63.5, 34.5), (67.3, 38.6), (63.5, 42.7), (60.6, 38.6)]), GEM_B, HEAD_OW)
    else:
        band = arc(50, 62, 24, 218, 322, 20)
        p.add(("line", band, 4.2), GOLD_D, HEAD_OW)
        p.add([ell((47, 33.5, 53, 39.5))], GOLD, HEAD_OW)
        p.add(tent([(49, 39), (46, 47)], 2.6, 1.6), GOLD_D, HEAD_OW)
        p.add(tent([(51, 39), (54, 47)], 2.6, 1.6), GOLD_D, HEAD_OW)
    return p


def halo(view):
    p = Pic()
    cx = 50 if view != "east" else 47
    cy = 25  # 头顶约在 y30，光环压在发顶上方一点，免得大体型的肖像把它裁掉
    p.add(ring_e(cx, cy, 19 if view != "east" else 15, 5.6, 3.4, 0), HALO, HEAD_OW)
    p.add(("line", arc(cx, cy, 17.2 if view != "east" else 13.2, 200, 250, 8), 1.0), (255, 248, 210), 0)
    return p


# ================= 身体附件（128px，同原版身体画框；节点用 1.5 网格，与身体贴图一致） =================
def bez(a, c, b, t):
    return ((1 - t) ** 2 * a[0] + 2 * (1 - t) * t * c[0] + t * t * b[0], (1 - t) ** 2 * a[1] + 2 * (1 - t) * t * c[1] + t * t * b[1])


def feather_wing(p, root, tip, bottom, color=FEATHER, dark=FEATHER_D, light=FEATHER_L, ow=HEAD_OW, n=7):
    """一只羽翼：根部 root、翼尖 tip、飞羽最低点 bottom。前缘是一条向上拱的弧线，后缘是一排圆头飞羽；
    后缘一带是深色的飞羽，前缘一带是浅色的覆羽。"""
    sgn = 1 if tip[0] > root[0] else -1
    span = abs(tip[0] - root[0])
    lc = (root[0] + sgn * span * .25, tip[1] - (root[1] - tip[1]) * .15)
    lead = [bez(root, lc, tip, k / 12) for k in range(13)]
    tc = (tip[0] - sgn * span * .02, bottom[1] + 4)
    trail = [bez(tip, tc, bottom, k / n) for k in range(n + 1)]
    back = (root[0] + sgn * 1.5, (root[1] + bottom[1]) / 2)
    scal = []
    for k in range(n):
        a, b = trail[k], trail[k + 1]
        mx, my = (a[0] + b[0]) / 2, (a[1] + b[1]) / 2
        dx, dy = b[0] - a[0], b[1] - a[1]; L = math.hypot(dx, dy) or 1
        nx, ny = dy / L * sgn, -dx / L * sgn
        if ny < 0:
            nx, ny = -nx, -ny
        scal += [a, (mx + nx * L * .45, my + ny * L * .45)]
    scal.append(trail[-1])
    sil = lead + scal + [back]
    p.add(poly(sil), color, ow)
    def lead_at(t):
        return bez(root, lc, tip, 1 - t)
    inner = [((trail[k][0] * .55 + lead_at(k / n * .85)[0] * .45), (trail[k][1] * .55 + lead_at(k / n * .85)[1] * .45)) for k in range(n + 1)]
    p.add(poly(scal + inner[::-1]), dark, 0)
    cov = [((trail[k][0] * .2 + lead_at(k / n * .85)[0] * .8), (trail[k][1] * .2 + lead_at(k / n * .85)[1] * .8)) for k in range(n + 1)]
    p.add(poly(lead[3:] + cov[::-1]), light, 0)
    for k in range(1, n):
        p.line([inner[k], trail[k]], .7, INK)
    p.add(("line", lead, 1.2), INK, 0)


def aarakocra_wing(p, root, top, tipx, bottom, color=FEATHER, dark=FEATHER_D, light=FEATHER_L, flip=False, ow=HEAD_OW):
    """兼容旧调用：翼尖 (tipx, top)，飞羽最低点在根部外侧 bottom 高度。"""
    sgn = 1 if flip else -1
    span = abs(tipx - root[0])
    feather_wing(p, root, (tipx, top), (root[0] + sgn * span * .3, bottom), color, dark, light, ow)

def wings_aarakocra(view):
    p = Pic()
    if view == "south":
        aarakocra_wing(p, (45, 38), 15, 6, 72)
        aarakocra_wing(p, (55, 38), 15, 94, 72, flip=True)
    elif view == "north":
        aarakocra_wing(p, (47, 37), 13, 6, 72)
        aarakocra_wing(p, (53, 37), 13, 94, 72, flip=True)
        p.add(ell((44, 33, 56, 43)), FEATHER_D, HEAD_OW)
    else:
        aarakocra_wing(p, (44, 38), 12, 8, 74)
    return p


def bat_wing(p, root, tips, flip=False):
    """炎魔蝠翼：根部 root，几根翼骨末端 tips（从上到下）；膜在相邻翼骨之间向内凹。"""
    sgn = 1 if flip else -1
    rx, ry = root
    elbow = (rx + sgn * 14, tips[0][1] + 4)
    mem = [(rx, ry), elbow, tips[0]]
    for a, b in zip(tips, tips[1:]):
        mx, my = (a[0] + b[0]) / 2, (a[1] + b[1]) / 2
        mem += [(mx - sgn * 4, my - 2), b]
    mem += [(rx + sgn * 6, ry + 18)]
    p.add(poly(mem), BALOR, HEAD_OW)
    p.add(poly([elbow] + tips[1:] + [(rx + sgn * 6, ry + 18), (rx + sgn * 2, ry + 6)]), BALOR_D, 0)
    p.add(("line", [(rx, ry), elbow, tips[0]], 2.6), CHAR, HEAD_OW)
    for t in tips[1:]:
        p.add(("line", [elbow, t], 1.6), CHAR, 1.0)
    p.add(tent([elbow, (elbow[0] + sgn * 2, elbow[1] - 6)], 2.4, .6), CHAR_D, 1.0)  # 翼爪
    for t in tips:
        p.add(ell((t[0] - 1.1, t[1] - 1.1, t[0] + 1.1, t[1] + 1.1)), HELL, 0)


def wings_balor(view):
    p = Pic()
    if view in ("south", "north"):
        for flip, sgn in ((False, -1), (True, 1)):
            r = (50 + sgn * (4 if view == "south" else 3), 36)
            bat_wing(p, r, [(50 + sgn * 44, 10), (50 + sgn * 47, 36), (50 + sgn * 40, 60), (50 + sgn * 26, 74)], flip=flip)
    else:
        bat_wing(p, (44, 36), [(8, 8), (4, 32), (10, 56), (24, 72)])
    return p


# ================= 风羽披风（穿着图：五种体型 × 三向，近白灰，由 colorGenerator 着成羽色） =================
BODY = {  # 原版 Naked_* 身体画框（128px 像素，导出后量得）
    "Male": {"south": (38, 43, 90, 108), "east": (42, 43, 86, 107), "north": (38, 43, 90, 108)},
    "Female": {"south": (40, 38, 88, 113), "east": (36, 39, 82, 113), "north": (40, 38, 88, 113)},
    "Thin": {"south": (50, 43, 77, 109), "east": (42, 44, 77, 108), "north": (50, 43, 77, 109)},
    "Hulk": {"south": (25, 32, 102, 127), "east": (27, 36, 95, 125), "north": (25, 32, 102, 127)},
    "Fat": {"south": (23, 38, 103, 113), "east": (22, 38, 100, 115), "north": (24, 38, 104, 113)},
}


def feather_rows(p, x0, x1, y0, y1, rows, color=PALE, dark=PALE_D, step=5.0):
    """在矩形里铺几排向下的圆头羽片（鳞片状，后排被前排压住），用于披风下摆。"""
    for r in range(rows):
        y = y0 + (y1 - y0) * r / max(1, rows - 1)
        n = max(2, int(round((x1 - x0) / step)))
        for i in range(n + (r % 2)):
            x = x0 + (x1 - x0) * (i + (.5 if r % 2 == 0 else 0)) / n
            if x < x0 - .1 or x > x1 + .1:
                continue
            sh = dark if (i + r) % 3 == 0 else color
            p.add(poly([(x - step * .5, y - step * .9)] + arc(x, y, step * .5, 0, 180, 10)[::-1][::-1] + [(x + step * .5, y - step * .9)]), sh, .9)
            p.line([(x, y - step * .7), (x, y + step * .3)], .5, (170, 170, 170))

def cloak_worn(body, view):
    p = Pic()
    x0, y0, x1, y1 = [v / 1.28 for v in BODY[body][view]]
    w, h = x1 - x0, y1 - y0
    sh = y0 + h * .08  # 肩线
    if view == "south":
        # 正面：肩上的羽毛披肩（不遮手臂），两侧露出背后垂下的披风边。
        for s in (-1, 1):
            cx = (x0 + x1) / 2
            side = [(cx + s * w * .5, sh + h * .04), (cx + s * w * .62, sh + h * .3), (cx + s * w * .6, y0 + h * .78), (cx + s * w * .5, y0 + h * .74)]
            p.add(poly(side), PALE_D, 1.6)
        mantle = [(x0 + w * .02, sh + h * .1), (x0 + w * .16, sh - h * .02), (x0 + w * .5, sh - h * .05), (x1 - w * .16, sh - h * .02), (x1 - w * .02, sh + h * .1), (x1 - w * .06, sh + h * .27), (x0 + w * .5, sh + h * .2), (x0 + w * .06, sh + h * .27)]
        p.add(poly(mantle), PALE, 1.6)
        feather_rows(p, x0 + w * .1, x1 - w * .1, sh + h * .15, sh + h * .22, 1, step=max(3.6, w / 9))
        p.add(ell(((x0 + x1) / 2 - 2.2, sh + h * .02, (x0 + x1) / 2 + 2.2, sh + h * .02 + 4.4)), (150, 150, 150), 1.2)  # 领扣
    elif view == "north":
        # 背面：整片披风盖住后背，垂到身体下部，下摆是一排羽片。
        cape = [(x0 + w * .06, sh), (x0 + w * .5, sh - h * .06), (x1 - w * .06, sh), (x1 + w * .06, y0 + h * .55), (x1 + w * .02, y0 + h * .8), (x0 - w * .02, y0 + h * .8), (x0 - w * .06, y0 + h * .55)]
        p.add(poly(cape), PALE, 1.6)
        p.add(poly([(x0 + w * .55, sh - h * .04), (x1 - w * .06, sh), (x1 + w * .06, y0 + h * .55), (x1 + w * .02, y0 + h * .8), (x0 + w * .62, y0 + h * .8)]), PALE_D, 0)
        feather_rows(p, x0, x1, y0 + h * .5, y0 + h * .8, 3, step=max(3.6, w / 8))
    else:
        # 侧面（朝右）：披风挂在背后（左侧），从肩头垂下。
        cape = [(x0 + w * .52, sh - h * .03), (x0 + w * .18, sh - h * .01), (x0 - w * .08, y0 + h * .5), (x0 - w * .1, y0 + h * .8), (x0 + w * .36, y0 + h * .8), (x0 + w * .44, y0 + h * .42)]
        p.add(poly(cape), PALE, 1.6)
        p.add(poly([(x0 + w * .18, sh - h * .01), (x0 - w * .08, y0 + h * .5), (x0 - w * .1, y0 + h * .8), (x0 + w * .08, y0 + h * .8), (x0 + w * .16, y0 + h * .4)]), PALE_D, 0)
        feather_rows(p, x0 - w * .06, x0 + w * .34, y0 + h * .6, y0 + h * .78, 2, step=max(3.4, w / 8))
        p.add(tent([(x0 + w * .55, sh - h * .02), (x0 + w * .72, sh + h * .06)], 4, 2.6), PALE_D, 1.4)  # 搭在肩前的系带
    return p


# ================= 物品（128px） =================
def focus(kind):
    p = Pic()
    p.add(("line", arc(50, 46, 26, 200, 340, 20), 2.2), SILVER_D, 1.4)  # 挂绳
    p.add(ell((30, 40, 70, 80)), SILVER, OW)
    p.add(ell((34, 44, 66, 76)), SILVER_D, 0)
    if kind == "Fire":
        p.add(poly([(50, 46), (60, 60), (50, 74), (40, 60)]), (226, 96, 44), OW)
        p.add(poly([(50, 50), (55, 60), (50, 60)]), FLAME_L, 0)
    elif kind == "Water":
        drop(p, 50, 64, 9)
    elif kind == "Earth":
        p.add(poly([(39, 66), (42, 52), (50, 46), (59, 51), (62, 64), (52, 72)]), (96, 92, 104), OW)
        p.add(poly([(50, 46), (59, 51), (62, 64), (54, 60)]), (70, 68, 80), 0)
        p.add(poly([(45, 58), (50, 52), (53, 60)]), CRYSTAL, 1.2)
    else:
        for r, a0 in ((12, 200), (8, 20)):
            p.add(("line", arc(50, 60, r, a0, a0 + 240, 16), 2.4), WIND, 1.4)
        for y, d in ((48, 1), (70, -1)):
            p.add(tent([(64, y), (80, y + d * 6), (90, y + d * 2)], 3.2, 1.2), (176, 206, 226), 1.2)
    p.add(ell((38, 47, 43, 52)), SILVER_L, 0)
    return p


def ivory_horn():
    p = Pic()
    pts = [(14, 70), (30, 66), (48, 58), (64, 46), (76, 32), (84, 18)]
    p.add(tent(pts, 6, 22), IVORY, OW)
    p.add(tent(pts[2:], 4, 12), IVORY_D, 0)
    p.add(ell((74, 6, 96, 30)), IVORY_D, OW)
    p.add(ell((78, 10, 92, 26)), (120, 104, 80), 0)
    for t in (.3, .55, .78):
        i = int(t * (len(pts) - 1)); x0, y0 = pts[i]; x1, y1 = pts[i + 1]; f = t * (len(pts) - 1) - i
        x, y = x0 + (x1 - x0) * f, y0 + (y1 - y0) * f; w = 6 + 16 * t
        ang = math.atan2(y1 - y0, x1 - x0) + math.pi / 2
        p.add(("line", [(x - math.cos(ang) * w / 2, y - math.sin(ang) * w / 2), (x + math.cos(ang) * w / 2, y + math.sin(ang) * w / 2)], 3.4), SILVER, 1.4)
    p.add(("line", [(10, 72), (14, 70)], 3), LEATHER, 1.2)
    return p


def ogre_club():
    # 受材料着色：近白灰
    p = Pic()
    p.add(tent([(6, 56), (40, 52), (70, 46), (92, 40)], 7, 22), PALE, OW)
    p.add(tent([(40, 55), (70, 50), (92, 45)], 3, 9), PALE_D, 0)
    for x, y in ((60, 47), (78, 43)):
        p.add(("line", [(x, y - 9), (x + 2, y + 9)], 3.6), (150, 150, 150), 1.4)
    for x, y in ((70, 38), (86, 36), (84, 52), (66, 56)):
        p.add(poly([(x - 2.5, y), (x, y - 6 if y < 46 else y + 6), (x + 2.5, y)]), (170, 170, 170), 1.2)
    p.add(("rect", (4, 52, 20, 60)), (180, 180, 180), 1.4)
    return p


def feather_cloak_icon():
    p = Pic()
    cape = [(30, 18), (50, 14), (70, 18), (82, 50), (86, 86), (14, 86), (18, 50)]
    p.add(poly(cape), PALE, OW)
    p.add(poly([(50, 14), (70, 18), (82, 50), (86, 86), (54, 86), (58, 50)]), PALE_D, 0)
    feather_rows(p, 16, 84, 60, 86, 3, step=8)
    p.add(poly([(28, 14), (50, 10), (72, 14), (66, 26), (50, 22), (34, 26)]), PALE_L, OW)
    p.add(ell((46, 18, 54, 26)), (160, 160, 160), 1.4)
    return p


def stinger(n):
    p = Pic()
    spots = [(50, 50, 0)] if n == 1 else [(40, 54, -20), (58, 46, 25), (48, 62, 70)][:n] if n <= 3 else [(30, 56, -25), (50, 42, 10), (68, 56, 40), (40, 68, 80), (60, 70, -60)]
    for cx, cy, a in spots:
        r = math.radians(a)
        def P(u, v):
            return (cx + u * math.cos(r) - v * math.sin(r), cy + u * math.sin(r) + v * math.cos(r))
        p.add(poly([P(-22, -4), P(10, -6), P(24, 0), P(10, 6), P(-22, 4)]), ICE, OW)
        p.add(poly([P(-20, 1), P(10, 1), P(24, 0), P(10, 6), P(-20, 4)]), ICE_D, 0)
        p.add(poly([P(-22, -4), P(-28, 0), P(-22, 4)]), (70, 110, 150), 1.4)
        p.line([P(-14, -2), P(12, -3)], .8, FROST)
    return p


def frost_spear():
    p = Pic()
    p.add(("rect", (4, 48.5, 72, 52.5)), (172, 186, 196), 1.8)
    p.add(("rect", (4, 50.5, 72, 52.5)), (136, 150, 162), 0)
    for x in (20, 34, 48):
        p.add(("rect", (x, 47.5, x + 3, 53.5)), (110, 130, 150), 1.2)
    p.add(poly([(70, 44), (84, 46), (98, 50), (84, 54), (70, 56), (74, 50)]), ICE, 1.8)
    p.add(poly([(74, 50), (98, 50), (84, 54), (70, 56)]), ICE_D, 0)
    p.line([(76, 48), (92, 49.5)], .8, FROST)
    for x, y in ((80, 43), (78, 57)):
        p.add(poly([(x - 2, 50), (x + 2, y), (x + 4, 50)]), ICE_L, 1.2)
    return p


def balor_heart():
    p = Pic()
    heart = [(50, 82)] + arc(38, 42, 14, 135, 330, 16) + arc(62, 42, 14, 210, 405, 16)
    p.add(poly(heart), BALOR, OW)
    p.add(poly([(50, 82)] + arc(62, 42, 14, 290, 405, 10)), BALOR_D, 0)
    for pts in ([(40, 40), (46, 52), (42, 62)], [(58, 36), (54, 50), (60, 60), (55, 70)], [(50, 46), (50, 58)]):
        p.add(("line", pts, 2.4), HELL, 0)
    p.add(tent([(44, 28), (42, 16)], 6, 4), CHAR, OW)
    p.add(tent([(56, 28), (60, 14)], 6, 4), CHAR, OW)
    flame(p, 50, 30, 14, 14, FLAME, FLAME_L, 1.6)
    return p


def balor_whip():
    p = Pic()
    p.add(("rect", (6, 47, 30, 53)), CHAR, 1.8)
    for x in (10, 16, 22):
        p.line([(x, 47.5), (x + 1.4, 52.5)], .7, (90, 70, 66))
    p.add(ell((2, 45, 9, 55)), (180, 60, 40), 1.8)
    for k, (dy, end) in enumerate(((-14, (96, 30)), (0, (98, 50)), (14, (94, 72)))):
        pts = [(30, 50), (50, 50 + dy * .3), (70, 50 + dy * .8 - 6), (86, (50 + end[1]) / 2 + 4), end]
        p.add(tent(pts, 4.2, 1.6), FLAME_D, 1.4)
        p.add(tent(pts[1:], 1.8, .8), FLAME_L, 0)
    flame(p, 95, 54, 8, 10, FLAME, FLAME_L, 1.2)
    return p


def balor_lash():
    p = Pic()
    p.add(tent([(20, 50), (50, 47), (80, 50)], 4, 10), FLAME_D, 0)
    p.add(tent([(30, 50), (55, 48.5), (80, 50)], 2, 6), FLAME, 0)
    p.add(ell((72, 44, 86, 56)), FLAME_L, 0)
    return p


def empyrean_blade():
    p = Pic()
    p.add(poly([(28, 45), (86, 44), (99, 50), (86, 56), (28, 55)]), (240, 238, 228), 1.8)
    p.add(poly([(28, 50), (99, 50), (86, 56), (28, 55)]), (206, 204, 196), 0)
    for x0 in (36, 58):
        p.add(poly([(x0, 46.5), (x0 + 8, 50), (x0 + 5, 50), (x0 + 13, 53.5), (x0 + 4, 50.5), (x0 + 7, 50.5)]), HALO, 0)
    p.add(poly([(22, 36), (28, 38), (28, 62), (22, 64), (25, 50)]), GOLD, 1.8)
    p.add(("rect", (8, 47, 22, 53)), (90, 60, 120), 1.8)
    for x in (11, 15, 19):
        p.line([(x, 47.4), (x + 1.4, 52.6)], .6, (60, 40, 80))
    p.add(ell((1, 45, 9, 55)), GOLD, 1.8)
    p.add(ell((3.5, 47.5, 6.5, 52.5)), (130, 210, 255), 0)
    return p


def wonder_lamp(state):
    p = Pic()
    metal, dark, light = (BRASS, BRASS_D, BRASS_L) if state != "Dim" else ((136, 122, 100), (104, 94, 78), (160, 148, 126))
    if state == "Bound":
        for i, (x, y, r) in enumerate(((70, 30, 7), (60, 18, 9), (72, 8, 6))):
            p.add(ell((x - r, y - r * .8, x + r, y + r * .8)), SMOKE, 1.4)
        p.add(ell((60, 28, 76, 40)), (255, 236, 160), 0)
    if state == "Asleep":
        p.add(tent([(72, 36), (76, 28), (73, 22)], 3, 1), SMOKE, 1.2)
    body = [(18, 62), (30, 52), (50, 50), (66, 54), (80, 44), (92, 40), (90, 46), (76, 60), (66, 70), (34, 72), (22, 70)]
    p.add(poly(body), metal, OW)
    p.add(poly([(34, 64), (66, 62), (76, 60), (66, 70), (34, 72), (22, 70)]), dark, 0)
    p.add(ell((36, 42, 54, 54)), metal, OW)  # 灯盖
    p.add(ell((42, 36, 48, 43)), light, 1.6)
    p.add(("line", arc(14, 60, 9, 100, 260, 12), 3), metal, 1.6)  # 把手
    p.add(("rect", (36, 72, 54, 78)), dark, 1.8)
    p.line([(30, 58), (60, 56)], 1.0, light)
    if state == "Dim":
        p.line([(40, 62), (46, 66), (52, 63)], .9, (80, 72, 60))
    return p


# ================= 异种型图标（256） =================
def genasi_fire_icon():
    p = Pic()
    for x, h in ((30, 30), (42, 40), (54, 44), (66, 36), (74, 26)):
        flame(p, x, 34, 18, h)
    face(p, 50, 52, 21, 26, EMBER, EMBER_D)
    for y in (46, 58):
        p.line([(32, y), (40, y + 2)], 1.4, (90, 30, 20))
    eyes(p, 50, 50, 8, 3, (255, 214, 120))
    p.line([(43, 66), (50, 68), (57, 66)], 1.8)
    return p


def genasi_water_icon():
    p = Pic()
    for s in (S, mir):
        p.add(tent(s([(32, 34), (22, 50), (24, 68), (16, 82)]), 9, 3), WATER, OW)
    p.add(poly([(28, 34)] + arc(50, 42, 22, 200, 340, 12) + [(72, 34), (64, 26), (50, 22), (36, 26)]), WATER_D, OW)
    face(p, 50, 54, 20, 25, TIDE, TIDE_D)
    eyes(p, 50, 52, 8, 3, (210, 245, 255))
    p.line([(44, 67), (50, 69), (56, 67)], 1.8)
    drop(p, 80, 72, 5)
    drop(p, 18, 30, 4)
    return p


def genasi_earth_icon():
    p = Pic()
    face(p, 50, 54, 22, 26, EARTH, EARTH_D)
    for pts, col in (([(30, 40), (34, 22), (42, 36)], CRYSTAL), ([(42, 32), (50, 12), (56, 30)], CRYSTAL), ([(58, 34), (66, 18), (70, 38)], CRYSTAL_D)):
        p.add(poly(pts), col, OW)
    for pts in ([(30, 60), (36, 64), (34, 70)], [(64, 46), (70, 50)]):
        p.line(pts, 1.4, (80, 68, 56))
    eyes(p, 50, 52, 8, 3, INK)
    p.line([(42, 68), (58, 68)], 2.2)
    return p


def genasi_air_icon():
    p = Pic()
    for r, a0, y in ((26, 190, 40), (20, 200, 34), (30, 210, 46)):
        p.add(("line", arc(50, y + 10, r, a0, a0 + 150, 16), 3.2), WIND, 1.6)
    face(p, 50, 56, 18, 24, SKY, SKY_D)
    p.add(tent([(36, 36), (50, 26), (66, 30), (80, 22)], 6, 2), WIND_D, OW)
    eyes(p, 50, 54, 7, 2.6, INK)
    p.line([(45, 68), (50, 69.5), (55, 68)], 1.6)
    for y in (78, 86):
        p.add(tent([(10, y), (34, y - 4)], 1.2, 4), WIND, 1.2)
    return p


def loxodon_icon():
    p = Pic()
    for s in (S, mir):
        p.add(poly(s([(32, 30), (8, 24), (4, 50), (12, 70), (30, 64)])), LOX_D, OW)
        p.add(poly(s([(28, 34), (12, 30), (9, 50), (15, 64), (28, 58)])), (206, 170, 170), 0)
    p.add(poly([(30, 22), (50, 16), (70, 22), (74, 44), (66, 62), (34, 62), (26, 44)]), LOX, OW)
    p.add(poly([(62, 22), (70, 22), (74, 44), (66, 62), (60, 62), (66, 44)]), LOX_D, 0)
    for s in (S, mir):
        p.add(tent(s([(40, 60), (34, 72), (36, 84)]), 5, 2), IVORY, OW)
    trunk = [(50, 50), (50, 66), (52, 80), (58, 90), (64, 88)]
    p.add(tent(trunk, 14, 7), LOX, OW)
    p.add(tent(trunk[1:], 5, 2.5), LOX_D, 0)
    for y in (60, 68, 76):
        p.line([(45, y), (55, y + .8)], 1, LOX_D)
    eyes(p, 50, 42, 11, 2.8)
    return p


def ogre_icon():
    p = Pic()
    p.add(poly([(24, 34), (36, 18), (64, 18), (76, 34), (80, 60), (70, 84), (30, 84), (20, 60)]), OGRE, OW)
    p.add(poly([(64, 18), (76, 34), (80, 60), (70, 84), (60, 84), (68, 58), (66, 34)]), OGRE_D, 0)
    p.add(tent([(26, 42), (50, 46), (74, 42)], 8, 8), OGRE_D, OW)  # 粗眉骨
    eyes(p, 50, 52, 10, 2.4)
    p.add(poly([(40, 56), (50, 54), (60, 56), (56, 64), (44, 64)]), OGRE_L, 1.8)
    p.add(poly([(30, 70), (70, 70), (66, 80), (34, 80)]), (120, 70, 60), OW)
    for s in (S, mir):
        p.add(poly(s([(36, 72), (40, 62), (44, 72)])), IVORY, 1.6)
    return p


def aarakocra_icon():
    p = Pic()
    p.add(tent([(48, 22), (40, 10), (30, 6)], 8, 3), FEATHER_D, OW)
    p.add(tent([(54, 22), (58, 8), (68, 2)], 8, 3), FEATHER_D, OW)
    p.add(ell((26, 18, 74, 70)), FEATHER, OW)
    p.add(poly([(60, 20), (72, 34), (74, 54), (66, 68), (58, 70), (64, 52), (64, 34)]), FEATHER_D, 0)
    p.add(poly([(34, 52), (46, 50), (50, 58), (40, 62)]), FEATHER_L, 0)
    for s in (S, mir):
        p.add(ell(sb(s, (34, 36, 44, 46))), (250, 240, 220), 1.8)
        p.add(ell(sb(s, (37, 39, 42, 44))), INK, 0)
    p.add(poly([(42, 50), (58, 50), (54, 66), (50, 82), (46, 66)]), BEAK, OW)
    p.add(poly([(50, 50), (58, 50), (54, 66), (50, 82)]), BEAK_D, 0)
    for y in (76, 86):
        p.add(tent([(18, y), (34, y - 6)], 3, 7), FEATHER_L, 1.4)
    return p


def ice_devil_icon():
    p = Pic()
    for s in (S, mir):
        horn(p, s([(36, 30), (28, 22), (24, 12), (26, 2)]), 8, 2, ow=OW, color=ICE_L, dark=ICE_D, tip=FROST)
    p.add(poly([(30, 30), (50, 20), (70, 30), (76, 52), (64, 74), (50, 80), (36, 74), (24, 52)]), ICE, OW)
    p.add(poly([(60, 26), (70, 30), (76, 52), (64, 74), (56, 76), (66, 52)]), ICE_D, 0)
    for s in (S, mir):
        p.add(ell(sb(s, (30, 40, 46, 52))), (40, 70, 110), OW)
        p.add(ell(sb(s, (34, 42, 40, 47))), (180, 230, 255), 0)
        p.add(tent(s([(42, 72), (38, 84), (44, 92)]), 4.4, 1.4), ICE_L, 1.6)  # 口器
    snowflake(p, 84, 80, 10)
    return p


def balor_icon():
    p = Pic()
    for x, h in ((20, 30), (32, 42), (68, 42), (80, 30)):
        flame(p, x, 80, 18, h)
    for s in (S, mir):
        horn(p, s([(36, 30), (22, 26), (12, 14), (14, 0)]), 10, 2.4, ow=OW, color=CHAR, dark=CHAR_D, tip=HELL)
    p.add(poly([(30, 26), (50, 20), (70, 26), (76, 50), (66, 76), (34, 76), (24, 50)]), BALOR, OW)
    p.add(poly([(62, 24), (70, 26), (76, 50), (66, 76), (58, 76), (66, 50)]), BALOR_D, 0)
    for s in (S, mir):
        p.add(poly(s([(32, 42), (46, 46), (44, 52), (34, 50)])), HELL, 1.8)
    p.add(poly([(36, 62), (64, 62), (58, 72), (42, 72)]), CHAR_D, OW)
    for x in (42, 50, 58):
        p.add(poly([(x - 2.5, 62), (x, 68), (x + 2.5, 62)]), IVORY, 0)
    return p


def titan_icon():
    p = Pic()
    p.add(ring_e(50, 14, 26, 7, 4, 0), HALO, OW)
    p.add(poly([(26, 40), (30, 22), (50, 16), (70, 22), (74, 40), (70, 56), (30, 56)]), WHITE_HAIR, OW)
    face(p, 50, 50, 20, 24, TITAN, TITAN_D)
    p.add(poly([(28, 56), (36, 64), (50, 66), (64, 64), (72, 56), (68, 84), (50, 94), (32, 84)]), WHITE_HAIR, OW)
    p.add(poly([(42, 64), (58, 64), (54, 70), (46, 70)]), TITAN_D, 0)
    eyes(p, 50, 46, 8, 2.6, (250, 220, 120))
    for s in (S, mir):
        p.line(s([(36, 40), (46, 41)]), 2.4, (200, 196, 186))
    return p


def genie_icon():
    p = Pic()
    p.add(tent([(50, 72), (46, 84), (56, 92), (50, 98)], 22, 6), SMOKE, OW)
    p.add(tent([(48, 74), (46, 86)], 10, 4), SMOKE_D, 0)
    p.add(tent([(50, 22), (50, 10), (58, 4)], 8, 3), (40, 40, 52), OW)  # 发髻
    face(p, 50, 46, 20, 24, GENIE, GENIE_D)
    p.add(("line", arc(50, 66, 24, 222, 318, 16), 4.4), GOLD, 1.8)
    p.add(poly([(50, 39), (54, 44), (50, 49), (46, 44)]), GEM_R, 1.8)
    eyes(p, 50, 50, 8, 2.6, INK)
    p.add(("line", arc(50, 58, 8, 30, 150, 10), 1.8), INK, 0)
    for s in (S, mir):
        p.add(ring_e(s([(29, 58)])[0][0], 58, 3.4, 4.4, 1.6, 0), GOLD)
    return p


# ================= 基因图标（256） =================
def genasi_core():
    p = Pic()
    for (pts, col) in (([(50, 50), (50, 10), (90, 50)], (230, 104, 52)), ([(50, 50), (90, 50), (50, 90)], (86, 146, 210)), ([(50, 50), (50, 90), (10, 50)], (140, 122, 96)), ([(50, 50), (10, 50), (50, 10)], (196, 216, 230))):
        p.add(poly(pts), col, OW)
    p.add(ell((42, 42, 58, 58)), (244, 236, 210), OW)
    return p


def element_fire():
    p = Pic()
    flame(p, 50, 88, 56, 80)
    flame(p, 30, 88, 20, 30, FLAME_D, FLAME, 1.8)
    flame(p, 72, 88, 18, 26, FLAME_D, FLAME, 1.8)
    return p


def element_water():
    p = Pic()
    for y in (74, 86):
        pts = [(6, y)] + [(6 + i * 8, y + (-4 if i % 2 else 4)) for i in range(1, 12)]
        p.add(("line", pts, 3.6), WATER, 1.6)
    drop(p, 50, 50, 22)
    return p


def element_earth():
    p = Pic()
    p.add(poly([(10, 86), (20, 54), (40, 40), (64, 44), (84, 58), (92, 86)]), ROCK, OW)
    p.add(poly([(64, 44), (84, 58), (92, 86), (64, 86), (70, 62)]), ROCK_D, 0)
    for pts, col in (([(34, 56), (40, 18), (50, 52)], CRYSTAL), ([(48, 50), (58, 8), (66, 46)], CRYSTAL), ([(62, 50), (74, 24), (76, 56)], CRYSTAL_D)):
        p.add(poly(pts), col, OW)
    p.line([(26, 70), (40, 66), (48, 74)], 1.6, ROCK_D)
    return p


def element_air():
    p = Pic()
    for r, a0, cy in ((34, 180, 44), (24, 190, 50), (14, 200, 54)):
        p.add(("line", arc(50, cy, r, a0, a0 + 250, 24), 4.4), WIND, 1.8)
    for y in (78, 88):
        p.add(tent([(8, y), (60, y - 6), (92, y)], 2, 4), WIND_D, 1.4)
    return p


def figure(p, cx, h, color, dark):
    """站立的人形：脚底 y=92，高 h，中心 cx。返回头顶 y。"""
    top = 92 - h; r = h * .15; w = h * .32
    p.add([ell((cx - r, top, cx + r, top + 2 * r)), poly([(cx - w, top + 2 * r + 2), (cx + w, top + 2 * r + 2), (cx + w * 1.12, top + h * .62), (cx + w * .8, 92), (cx - w * .8, 92), (cx - w * 1.12, top + h * .62)])], color)
    if dark:
        p.add(poly([(cx + w * .1, top + 2 * r + 2), (cx + w, top + 2 * r + 2), (cx + w * 1.12, top + h * .62), (cx + w * .8, 92), (cx + w * .2, 92), (cx + w * .45, top + h * .6)]), dark, 0)
    return top


def stature_pair(color, dark, scale=1.3, extra=None):
    """大个子（左）与普通人（右）的对比，照牛头人体型图标。普通人随倍率缩小，大个子的头不出框。"""
    p = Pic()
    hh = min(56, 80 / scale); hb_h = hh * scale
    figure(p, 70, hh, HUMAN, HUMAN_D)
    hb = figure(p, 32, hb_h, color, dark)
    if extra:
        extra(p, hb)
    p.add(("line", [(90, hb), (90, 92)], 1.6), (90, 96, 100), 0)
    for y in (hb, 92):
        p.add(("line", [(86, y), (94, y)], 1.6), (90, 96, 100), 0)
    return p

def loxodon_stature():
    def extra(p, hb):
        p.add(tent([(33, hb + 10), (32, hb + 22), (36, hb + 30)], 5, 2.6), LOX, 1.6)
    return stature_pair(LOX, LOX_D, 1.3, extra)


def loxodon_trunk():
    p = Pic()
    trunk = [(30, 8), (34, 30), (40, 50), (52, 66), (66, 70), (76, 62)]
    p.add(tent(trunk, 20, 9), LOX, OW)
    p.add(tent(trunk[2:], 8, 4), LOX_D, 0)
    for t in range(1, 5):
        x, y = trunk[t]
        p.line([(x - 8, y - 2), (x + 6, y + 3)], 1.2, LOX_D)
    p.add(poly([(66, 60), (88, 52), (94, 74), (72, 82)]), WOOD, OW)  # 卷起的木箱
    p.add(poly([(80, 56), (88, 52), (94, 74), (84, 78)]), WOOD_D, 0)
    return p


def loxodon_calm():
    p = Pic()
    p.add(poly([(50, 8), (84, 20), (80, 56), (50, 92), (20, 56), (16, 20)]), (96, 120, 150), OW)
    p.add(poly([(50, 8), (84, 20), (80, 56), (50, 92)]), (74, 96, 126), 0)
    for s in (S, mir):
        p.add(poly(s([(36, 34), (24, 30), (22, 50), (34, 48)])), LOX_D, 1.8)
    p.add(poly([(36, 30), (50, 26), (64, 30), (66, 46), (60, 56), (40, 56), (34, 46)]), LOX, 1.8)
    p.add(tent([(50, 48), (50, 62), (54, 72)], 8, 4), LOX, 1.8)
    for s in (S, mir):
        p.add(("line", arc(s([(43, 0)])[0][0], 40, 3.4, 20, 160, 8), 1.8), INK, 0)  # 闭目
    return p


def loxodon_hide():
    p = Pic()
    p.add(poly([(12, 22), (50, 12), (88, 22), (86, 62), (50, 90), (14, 62)]), LOX, OW)
    p.add(poly([(50, 12), (88, 22), (86, 62), (50, 90)]), LOX_D, 0)
    for pts in ([(24, 34), (36, 40), (34, 52), (44, 60)], [(56, 28), (62, 42), (74, 46)], [(48, 46), (56, 58), (54, 72)], [(26, 62), (36, 66)]):
        p.line(pts, 1.8, (90, 90, 96))
    return p


def ogre_stature():
    def extra(p, hb):
        p.add(tent([(18, hb + 30), (10, hb + 46), (8, hb + 60)], 6, 10), PALE_D, 1.6)  # 大棒
    return stature_pair(OGRE, OGRE_D, 1.6, extra)


def ogre_dull():
    p = Pic()
    p.add(poly([(20, 40), (30, 20), (70, 20), (80, 40), (78, 70), (22, 70)]), OGRE, OW)
    p.add(poly([(64, 20), (70, 20), (80, 40), (78, 70), (66, 70)]), OGRE_D, 0)
    eyes(p, 50, 48, 10, 2.2)
    p.line([(40, 62), (52, 60), (60, 63)], 2)
    p.add(("line", arc(64, 16, 9, 200, 380, 14), 4.4), (236, 220, 120), 1.6)
    p.add(("line", [(64, 25), (64, 31)], 4.4), (236, 220, 120), 1.6)
    p.add(ell((61.6, 35, 66.4, 39.8)), (236, 220, 120), 1.6)
    p.add(poly([(10, 78), (46, 74), (50, 94), (12, 94)]), (150, 110, 80), OW)  # 书
    p.add(("line", [(8, 72), (52, 96)], 3), RED, 1.4)
    return p


def aarakocra_wings_icon():
    p = Pic()
    aarakocra_wing(p, (80, 70), 10, 6, 70)
    return p


def hollow_bones():
    p = Pic()
    for s in (S, mir):
        p.add([ell(sb(s, (10, 18, 30, 38))), ell(sb(s, (10, 30, 30, 50)))], BONE)
    p.add(("rect", (20, 28, 80, 40)), BONE, OW)
    p.add(ell((36, 60, 64, 88)), BONE, OW)
    p.add(ell((43, 67, 57, 81)), (90, 80, 70), 0)
    p.add(("line", [(50, 40), (50, 60)], 2.2), (150, 140, 120), 0)
    return p


def skyborn():
    p = Pic()
    p.add(ell((62, 6, 92, 36)), (250, 214, 120), OW)
    for x, y, r in ((20, 76, 14), (38, 70, 16), (58, 76, 13), (74, 80, 10)):
        p.add(ell((x - r, y - r * .7, x + r, y + r * .7)), (240, 244, 248), OW)
    p.add(poly([(14, 46), (30, 36), (40, 44), (52, 30), (66, 40), (54, 46), (40, 52), (28, 46)]), FEATHER_D, OW)
    return p


def fiend_core():
    p = Pic()
    p.add(ell((10, 10, 90, 90)), (70, 20, 20), OW)
    p.add(ell((18, 18, 82, 82)), (120, 30, 26), 0)
    pts = [(50 + 30 * math.cos(math.radians(-90 + i * 144)), 50 + 30 * math.sin(math.radians(-90 + i * 144))) for i in range(6)]
    p.add(("line", pts, 3.2), HELL, 0)
    for i in range(9):
        a = math.radians(i * 40)
        x, y = 50 + 36 * math.cos(a), 50 + 36 * math.sin(a)
        p.add(("line", [(x - 2, y - 2), (x + 2, y + 2)], 1.6), HELL, 0)
    return p


def ice_devil_stature():
    def extra(p, hb):
        for s in (-1, 1):
            horn(p, [(33 + s * 6, hb), (33 + s * 10, hb - 8), (33 + s * 9, hb - 14)], 3.6, 1, ow=1.6, color=ICE_L, dark=ICE_D, tip=FROST)
    return stature_pair(ICE, ICE_D, 1.5, extra)


def ice_devil_cold():
    p = Pic()
    snowflake(p, 50, 50, 36, ICE_L, 5)
    for r in (40, 46):
        p.add(("line", arc(50, 50, r, 200, 250, 10), 2.4), ICE, 1.2)
        p.add(("line", arc(50, 50, r, 20, 70, 10), 2.4), ICE, 1.2)
    return p


def ice_devil_stinger():
    p = Pic()
    tail = [(8, 20), (26, 30), (40, 50), (52, 66), (70, 74)]
    p.add(tent(tail, 14, 8), ICE, OW)
    p.add(tent(tail[1:], 5, 3), ICE_D, 0)
    for i in range(1, 4):
        x, y = tail[i]
        p.add(poly([(x - 3, y - 4), (x - 8, y - 12), (x + 1, y - 6)]), ICE_L, 1.4)
    p.add(poly([(66, 66), (96, 82), (68, 82)]), FROST, OW)
    p.add(poly([(68, 76), (96, 82), (68, 82)]), ICE_D, 0)
    return p


def ice_devil_sight():
    p = Pic()
    p.add(ell((8, 26, 92, 74)), (40, 70, 110), OW)
    for i in range(-3, 4):
        for j in range(-1, 2):
            x, y = 50 + i * 10 + (j % 2) * 5, 50 + j * 9
            if ((x - 50) / 40) ** 2 + ((y - 50) / 22) ** 2 < 1:
                p.add(poly([(x + 4.5 * math.cos(math.radians(a)), y + 4.5 * math.sin(math.radians(a))) for a in range(30, 390, 60)]), (120, 200, 240), 1.0)
    p.add(("line", [(4, 92), (96, 8)], 2), (255, 255, 255), 0)
    return p


def balor_stature():
    def extra(p, hb):
        for s in (-1, 1):
            horn(p, [(33 + s * 6, hb), (33 + s * 14, hb - 6), (33 + s * 16, hb - 14)], 4, 1, ow=1.6, color=CHAR, dark=CHAR_D, tip=HELL)
    return stature_pair(BALOR, BALOR_D, 2.0, extra)


def balor_flame():
    p = Pic()
    for i in range(8):
        a = i * 45
        x, y = 50 + 34 * math.cos(math.radians(a)), 58 + 30 * math.sin(math.radians(a))
        flame(p, x, y + 8, 16, 22, FLAME_D, FLAME, 1.6)
    flame(p, 50, 80, 34, 56)
    return p


def balor_wings_icon():
    p = Pic()
    bat_wing(p, (88, 70), [(30, 8), (6, 38), (14, 66), (40, 86)])
    return p


def balor_deathburst():
    p = Pic()
    burst = []
    for i in range(16):
        r = 44 if i % 2 == 0 else 26
        a = math.radians(i * 22.5)
        burst.append((50 + r * math.cos(a), 50 + r * math.sin(a)))
    p.add(poly(burst), FLAME, OW)
    p.add(poly([(50 + (r * .55) * math.cos(math.radians(i * 22.5)), 50 + (r * .55) * math.sin(math.radians(i * 22.5))) for i, r in enumerate([44 if k % 2 == 0 else 26 for k in range(16)])]), FLAME_L, 0)
    p.add(ell((38, 38, 62, 62)), BALOR, OW)
    return p


def genie_core():
    p = Pic()
    lamp = wonder_lamp("Bound")
    p.img.alpha_composite(lamp.img)
    return p


def genie_stature():
    def extra(p, hb):
        p.add(tent([(32, 90), (28, 98)], 18, 4), SMOKE, 1.6)
    return stature_pair(GENIE, GENIE_D, 1.5, extra)


def genie_ageless():
    p = Pic()
    p.add(poly([(28, 12), (72, 12), (54, 50), (72, 88), (28, 88), (46, 50)]), (236, 222, 180), OW)
    p.add(poly([(34, 18), (66, 18), (50, 46)]), (240, 210, 120), 0)
    p.add(poly([(50, 56), (66, 82), (34, 82)]), (240, 210, 120), 0)
    for y in (8, 92):
        p.add(("rect", (22, y - 4, 78, y + 4)), GOLD, OW)
    p.add(("line", [(50, 46), (50, 56)], 1.6), (240, 210, 120), 0)
    return p


def genie_wish():
    p = Pic()
    p.add(star(50, 46, 30, .36), (255, 228, 120), OW)
    p.add(star(50, 46, 14, .36), (255, 248, 210), 0)
    for x, y, r in ((18, 20, 6), (84, 22, 5), (22, 80, 5), (80, 78, 7)):
        p.add(star(x, y, r, .3), (220, 210, 255), 1.4)
    return p


def titan_stature():
    def extra(p, hb):
        p.add(ring_e(33, hb - 8, 11, 3, 2, 0), HALO, 1.4)
    return stature_pair(TITAN, TITAN_D, 2.0, extra)


def titan_divinity():
    p = Pic()
    for i in range(12):
        a = math.radians(i * 30)
        p.add(tent([(50 + 26 * math.cos(a), 50 + 26 * math.sin(a)), (50 + 46 * math.cos(a), 50 + 46 * math.sin(a))], 6, 1.4), HALO_D, 1.4)
    p.add(ring_e(50, 50, 24, 24, 6, 0), HALO, OW)
    p.add(ell((38, 38, 62, 62)), (255, 246, 210), OW)
    return p


def titan_weather():
    p = Pic()
    p.add(ell((10, 8, 50, 48)), (250, 214, 120), OW)
    for x, y, r in ((52, 50, 18), (70, 46, 16), (84, 56, 12), (40, 58, 12)):
        p.add(ell((x - r, y - r * .8, x + r, y + r * .8)), (150, 156, 170), OW)
    bolt(p, [(60, 62), (52, 80), (60, 80), (54, 96), (72, 74), (63, 74), (68, 62)])
    return p


def titan_unshakable():
    p = Pic()
    p.add(poly([(6, 90), (36, 30), (50, 46), (64, 18), (94, 90)]), STONE, OW)
    p.add(poly([(64, 18), (94, 90), (62, 90), (58, 50)]), STONE_D, 0)
    p.add(poly([(56, 30), (64, 18), (72, 34), (64, 30)]), (240, 240, 244), 1.6)
    for y, x0 in ((40, 0), (56, 2)):
        p.add(("line", [(x0, y), (x0 + 16, y + 6)], 2.6), (180, 200, 220), 1.2)
    return p


# ================= 能力图标（256） =================
def spark_ab():
    p = Pic()
    p.add(poly([(18, 92), (14, 70), (28, 56), (40, 62), (36, 76), (30, 92)]), EMBER, OW)  # 手
    p.add(star(62, 36, 28, .28), (255, 210, 90), OW)
    p.add(star(62, 36, 12, .3), (255, 246, 210), 0)
    for x, y in ((84, 66), (40, 16), (88, 14)):
        p.add(star(x, y, 6, .3), FLAME_L, 1.2)
    return p


def tide_ab():
    p = Pic()
    wave = [(4, 80), (14, 54), (32, 34), (54, 26), (74, 32), (86, 46), (78, 52), (64, 44), (50, 46), (44, 60), (52, 70), (70, 74), (96, 80), (96, 94), (4, 94)]
    p.add(poly(wave), WATER, OW)
    p.add(poly([(50, 46), (44, 60), (52, 70), (70, 74), (96, 80), (96, 94), (40, 94)]), WATER_D, 0)
    for x, y in ((80, 20), (90, 32), (70, 12)):
        drop(p, x, y, 3.4)
    return p


def windleap_ab():
    p = Pic()
    for y, x0 in ((40, 4), (54, 0), (68, 6)):
        p.add(tent([(x0, y), (x0 + 30, y - 6)], 1.4, 4.6), WIND, 1.2)
    body = [ell((62, 14, 76, 28)), poly([(58, 32), (80, 32), (84, 52), (80, 54), (78, 44), (80, 76), (72, 76), (70, 58), (66, 76), (58, 76), (60, 44), (56, 56), (52, 54)])]
    p.add(body, SKY)
    p.add(("line", arc(70, 70, 22, 150, 230, 10), 3), WIND_D, 1.2)
    return p


def soar_ab():
    p = Pic()
    p.add(("rect", (10, 66, 46, 96)), STONE, OW)
    for y in (76, 86):
        p.line([(10, y), (46, y)], 1.4, STONE_D)
    p.line([(28, 66), (28, 76)], 1.4, STONE_D)
    p.add(("line", [(14, 60), (40, 30), (70, 22)], 2.4), (220, 210, 190), 0)
    aarakocra_wing(p, (68, 30), 6, 40, 46)
    aarakocra_wing(p, (72, 30), 6, 98, 46, flip=True)
    p.add(ell((64, 22, 78, 38)), FEATHER_D, OW)
    return p


def buffet_ab():
    p = Pic()
    bat_wing(p, (24, 60), [(62, 8), (86, 30), (82, 56), (64, 76)], flip=True)
    for r in (20, 30, 40):
        p.add(("line", arc(28, 62, r + 30, -40, 40, 12), 2.6), (230, 200, 170), 1.2)
    return p


def wish_ab():
    p = Pic()
    lamp = wonder_lamp("Bound")
    p.img.alpha_composite(lamp.img)
    p.add(star(74, 16, 12, .32), (255, 228, 120), 1.8)
    return p


def smite_ab():
    p = Pic()
    for x, y, r in ((30, 16, 16), (54, 12, 18), (74, 18, 14)):
        p.add(ell((x - r, y - r * .7, x + r, y + r * .7)), (120, 126, 146), OW)
    bolt(p, [(52, 24), (36, 56), (50, 56), (38, 92), (70, 46), (56, 46), (66, 24)])
    p.add(ell((26, 86, 54, 96)), (250, 200, 110), 1.6)
    return p


def breath_ab():
    p = Pic()
    for r, w in ((40, 3.2), (30, 3.2), (20, 3.2)):
        p.add(("line", arc(18, 50, r, -40, 40, 12), w), HALO, 1.4)
    p.add([("rect", (56, 40, 84, 60)), ("rect", (60, 30, 80, 70))], (255, 255, 255), OW)
    p.add([("rect", (60, 42, 80, 58)), ("rect", (64, 32, 76, 68))], (110, 200, 120), 0)
    return p


def horn_ab():
    p = Pic()
    pts = [(10, 82), (30, 72), (46, 58), (56, 42), (60, 26)]
    p.add(tent(pts, 6, 18), IVORY, OW)
    p.add(tent(pts[2:], 4, 10), IVORY_D, 0)
    p.add(ell((50, 14, 70, 32)), IVORY_D, OW)
    for r in (16, 26, 36):
        p.add(("line", arc(60, 22, r, -70, 20, 10), 2.6), (240, 220, 160), 1.2)
    return p


# ================= 清单 =================
ICONS = {
    "Races124/Xenotypes/GenasiFire": genasi_fire_icon, "Races124/Xenotypes/GenasiWater": genasi_water_icon,
    "Races124/Xenotypes/GenasiEarth": genasi_earth_icon, "Races124/Xenotypes/GenasiAir": genasi_air_icon,
    "Races124/Xenotypes/Loxodon": loxodon_icon, "Races124/Xenotypes/Ogre": ogre_icon, "Races124/Xenotypes/Aarakocra": aarakocra_icon,
    "Races124/Xenotypes/IceDevil": ice_devil_icon, "Races124/Xenotypes/Balor": balor_icon, "Races124/Xenotypes/Titan": titan_icon, "Races124/Xenotypes/Genie": genie_icon,
    "Races124/Genes/GenasiCore": genasi_core, "Races124/Genes/ElementFire": element_fire, "Races124/Genes/ElementWater": element_water,
    "Races124/Genes/ElementEarth": element_earth, "Races124/Genes/ElementAir": element_air,
    "Races124/Genes/LoxodonStature": loxodon_stature, "Races124/Genes/LoxodonTrunk": loxodon_trunk, "Races124/Genes/LoxodonCalm": loxodon_calm, "Races124/Genes/LoxodonHide": loxodon_hide,
    "Races124/Genes/OgreStature": ogre_stature, "Races124/Genes/OgreDull": ogre_dull,
    "Races124/Genes/AarakocraWings": aarakocra_wings_icon, "Races124/Genes/AarakocraHollowBones": hollow_bones, "Races124/Genes/AarakocraSkyborn": skyborn,
    "Races124/Genes/FiendCore": fiend_core, "Races124/Genes/IceDevilStature": ice_devil_stature, "Races124/Genes/IceDevilCold": ice_devil_cold,
    "Races124/Genes/IceDevilStinger": ice_devil_stinger, "Races124/Genes/IceDevilSight": ice_devil_sight,
    "Races124/Genes/BalorStature": balor_stature, "Races124/Genes/BalorFlame": balor_flame, "Races124/Genes/BalorWings": balor_wings_icon, "Races124/Genes/BalorDeathburst": balor_deathburst,
    "Races124/Genes/GenieCore": genie_core, "Races124/Genes/GenieStature": genie_stature, "Races124/Genes/GenieAgeless": genie_ageless, "Races124/Genes/GenieWish": genie_wish,
    "Races124/Genes/TitanStature": titan_stature, "Races124/Genes/TitanDivinity": titan_divinity, "Races124/Genes/TitanWeather": titan_weather, "Races124/Genes/TitanUnshakable": titan_unshakable,
    "Races124/Abilities/Spark": spark_ab, "Races124/Abilities/Tide": tide_ab, "Races124/Abilities/WindLeap": windleap_ab, "Races124/Abilities/Soar": soar_ab,
    "Races124/Abilities/Buffet": buffet_ab, "Races124/Abilities/Wish": wish_ab, "Races124/Abilities/Smite": smite_ab, "Races124/Abilities/DivineBreath": breath_ab, "Races124/Abilities/Horn": horn_ab,
}
SMALL = {
    "Races124/Trunk/Trunk_south": trunk_south, "Races124/Trunk/Trunk_east": trunk_east,
    "Races124/Horns/IceHorns_south": lambda: fiend_horns(ICE_L, ICE_D, FROST, "south"), "Races124/Horns/IceHorns_east": lambda: fiend_horns(ICE_L, ICE_D, FROST, "east"), "Races124/Horns/IceHorns_north": lambda: fiend_horns(ICE_L, ICE_D, FROST, "north"),
    "Races124/Horns/BalorHorns_south": lambda: fiend_horns(CHAR, CHAR_D, HELL, "south"), "Races124/Horns/BalorHorns_east": lambda: fiend_horns(CHAR, CHAR_D, HELL, "east"), "Races124/Horns/BalorHorns_north": lambda: fiend_horns(CHAR, CHAR_D, HELL, "north"),
    "Races124/Circlet/GenieCirclet_south": lambda: circlet("south"), "Races124/Circlet/GenieCirclet_east": lambda: circlet("east"), "Races124/Circlet/GenieCirclet_north": lambda: circlet("north"),
    "Races124/Halo/TitanHalo_south": lambda: halo("south"), "Races124/Halo/TitanHalo_east": lambda: halo("east"), "Races124/Halo/TitanHalo_north": lambda: halo("north"),
    "Races124/Wings/AarakocraWings_south": lambda: wings_aarakocra("south"), "Races124/Wings/AarakocraWings_east": lambda: wings_aarakocra("east"), "Races124/Wings/AarakocraWings_north": lambda: wings_aarakocra("north"),
    "Races124/Wings/BalorWings_south": lambda: wings_balor("south"), "Races124/Wings/BalorWings_east": lambda: wings_balor("east"), "Races124/Wings/BalorWings_north": lambda: wings_balor("north"),
    "Races124/Items/FocusFire": lambda: focus("Fire"), "Races124/Items/FocusWater": lambda: focus("Water"), "Races124/Items/FocusEarth": lambda: focus("Earth"), "Races124/Items/FocusAir": lambda: focus("Air"),
    "Races124/Items/IvoryHorn": ivory_horn, "Races124/Items/OgreClub": ogre_club, "Races124/Items/FeatherCloak": feather_cloak_icon,
    "Races124/Items/IceDevilStinger/IceDevilStinger_a": lambda: stinger(1), "Races124/Items/IceDevilStinger/IceDevilStinger_b": lambda: stinger(3), "Races124/Items/IceDevilStinger/IceDevilStinger_c": lambda: stinger(5),
    "Races124/Items/FrostSpear": frost_spear, "Races124/Items/BalorHeart": balor_heart, "Races124/Items/BalorWhip": balor_whip, "Races124/Items/BalorLash": balor_lash,
    "Races124/Items/EmpyreanBlade": empyrean_blade,
    "Races124/Items/WonderLamp_Asleep": lambda: wonder_lamp("Asleep"), "Races124/Items/WonderLamp_Bound": lambda: wonder_lamp("Bound"), "Races124/Items/WonderLamp_Dim": lambda: wonder_lamp("Dim"),
}
for _b in BODY:
    for _v in ("south", "east", "north"):
        SMALL["Races124/Items/Worn/FeatherCloak_" + _b + "_" + _v] = (lambda b=_b, v=_v: cloak_worn(b, v))

if __name__ == "__main__":
    out = []
    for rel, fn in ICONS.items():
        out.append(fn().save(rel, 256))
    for rel, fn in SMALL.items():
        out.append(fn().save(rel, 128))
    print(len(out), "textures written")
