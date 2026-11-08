# 1.127.0 DND 野兽第二批：伪龙、翼猫、巨型火甲虫、罗斯牛、鹰头马身兽、斧喙鸟、狮鹫、钩爪恐兽、相位蜘蛛、暗斗篷、血蝠蝇。
# 画法照 draw_wildmonsters.py（粗黑轮廓、平涂 + 柔和暗面、低饱和），整只动物一张图，east 头朝右，west 由游戏镜像。
#  - Textures/Things/Pawn/Animal/LP_BeastsDND2/<名>/<名>_{south,east,north}.png（256px）
#  - 尸体干尸用原版同体型动物的 Dessicated（见 Defs/Beasts_DND2.xml），这里不画
#  - ArtSource/Preview/BeastsDND2.png：十一只四个朝向，再按游戏里的成年体型和一个人比大小
# 用法：python -I ArtSource/RaceArt/draw_beasts_dnd2.py <模组根目录> [输出根目录]
import math, os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import draw_race_art as base
from draw_race_art import ell, tent, INK
from draw_dragons_1028 import SPic, poly, mix, CREAM
from PIL import Image, ImageDraw

ROOT = sys.argv[1] if len(sys.argv) > 1 else os.getcwd()
OUT = sys.argv[2] if len(sys.argv) > 2 else ROOT
TEX = os.path.join(OUT, "Textures")
REL = "Things/Pawn/Animal/LP_BeastsDND2"
OW = 2.4
BONE = (226, 216, 190)
GOLD = (214, 176, 92)


def thin(p, parts, color, ow=1.2):
    base.Pic.add(p, parts, color, ow)


def eye(p, x, y, r=1.7, col=INK, hl=True):
    p.add(ell((x - r, y - r, x + r, y + r)), col, 0, shade=False)
    if hl:
        p.add(ell((x - r * .55, y - r * .7, x - r * .55 + r * .7, y - r * .7 + r * .7)), CREAM, 0, shade=False)


def glow_eye(p, x, y, r, col):
    p.add(ell((x - r, y - r, x + r, y + r)), col, 1.0, shade=False)
    p.add(ell((x - r * .4, y - r * .4, x + r * .2, y + r * .2)), CREAM, 0, shade=False)


def leg(p, pts, w0, w1, col, ow=1.8):
    p.add(tent(pts, w0, w1), col, ow)


def feather_wing(p, root, tips, col, dark, ow=1.8):
    """羽翼：根部一点，按 tips 依次排出几片圆头羽毛，后面的颜色深一点。"""
    n = len(tips)
    for i, t in enumerate(tips):
        c = mix(col, dark, .5 * (n - 1 - i) / max(1, n - 1))
        p.add(tent([root, ((root[0] + t[0]) / 2, (root[1] + t[1]) / 2 - 1), t], 7, 3.4), c, ow)
    # 翼根的覆羽：压住各片羽毛的根部，用翅膀的暗色，不要亮斑
    p.add(poly([(root[0] - 5, root[1] + 2), (root[0], root[1] - 4), (root[0] + 5, root[1] + 1), (root[0] + 1, root[1] + 5)]), mix(col, dark, .25), ow * .7)


def bat_wing(p, root, fingers, col, mem, ow=1.8):
    """蝙蝠翼：膜面是根部和各指尖连成的多边形，指骨是细条。"""
    p.add(poly([root] + fingers + [(root[0] + (fingers[-1][0] - root[0]) * .3, root[1] + 4)]), mem, ow)
    for f in fingers:
        thin(p, tent([root, f], 1.8, .8), col, 1.0)


# ====================== 伪龙 ======================
PSEUDO = dict(m=(168, 92, 64), d=(124, 64, 50), b=(220, 180, 132), w=(196, 128, 96))


def pseudo_east(p):
    c = PSEUDO
    bat_wing(p, (44, 50), [(30, 24), (40, 18), (54, 22)], c["d"], mix(c["w"], c["d"], .35))
    # 尾巴和尾刺
    p.add(tent([(34, 62), (20, 64), (12, 56), (10, 46)], 6, 2.2), c["m"], OW)
    p.add(poly([(10, 46), (5, 40), (9, 38), (14, 44)]), c["d"], 1.4)
    leg(p, [(40, 64), (38, 72), (40, 79)], 4, 3, c["d"])
    leg(p, [(56, 64), (56, 72), (58, 79)], 4, 3, c["d"])
    body = ell((30, 50, 66, 72))
    p.add(body, c["m"], OW)
    p.add(ell((34, 62, 64, 76)), c["b"], 0, clip=body)
    leg(p, [(44, 66), (44, 73), (47, 80)], 4.4, 3.2, c["m"])
    leg(p, [(60, 64), (62, 72), (65, 79)], 4.4, 3.2, c["m"])
    # 脖子和头
    p.add(tent([(60, 58), (68, 50), (72, 44)], 9, 7), c["m"], OW)
    head = poly([(66, 38), (78, 36), (88, 42), (86, 48), (74, 50), (66, 46)])
    p.add(head, c["m"], OW)
    p.add(poly([(76, 46), (87, 45), (84, 49), (75, 50)]), c["b"], 1.2)
    p.add(tent([(70, 38), (66, 30), (62, 26)], 3, 1), c["d"], 1.2)
    eye(p, 76, 41, 1.8)
    bat_wing(p, (50, 52), [(40, 26), (54, 16), (68, 24)], c["d"], c["w"])


def pseudo_south(p):
    c = PSEUDO
    bat_wing(p, (42, 52), [(20, 34), (24, 22), (36, 26)], c["d"], c["w"])
    bat_wing(p, (58, 52), [(80, 34), (76, 22), (64, 26)], c["d"], c["w"])
    p.add(tent([(56, 74), (66, 80), (72, 72)], 5, 2), c["m"], 1.8)
    p.add(poly([(72, 72), (76, 66), (78, 70), (74, 75)]), c["d"], 1.2)
    leg(p, [(42, 70), (41, 80)], 4.6, 3.4, c["m"])
    leg(p, [(58, 70), (59, 80)], 4.6, 3.4, c["m"])
    body = ell((36, 50, 64, 78))
    p.add(body, c["m"], OW)
    p.add(ell((42, 56, 58, 80)), c["b"], 0, clip=body)
    head = poly([(40, 36), (60, 36), (62, 48), (50, 56), (38, 48)])
    p.add(head, c["m"], OW)
    p.add(poly([(44, 50), (56, 50), (50, 56)]), c["b"], 1.2)
    for s in (-1, 1):
        p.add(tent([(50 + s * 7, 38), (50 + s * 11, 30), (50 + s * 14, 26)], 3, 1), c["d"], 1.2)
        eye(p, 50 + s * 5.5, 44, 1.8)


def pseudo_north(p):
    c = PSEUDO
    leg(p, [(42, 70), (41, 80)], 4.6, 3.4, c["d"])
    leg(p, [(58, 70), (59, 80)], 4.6, 3.4, c["d"])
    body = ell((36, 48, 64, 76))
    p.add(body, c["m"], OW)
    p.add(tent([(50, 74), (48, 84), (54, 90)], 6, 2.2), c["m"], OW)
    p.add(poly([(54, 90), (60, 94), (56, 96), (52, 92)]), c["d"], 1.2)
    p.add(poly([(40, 34), (60, 34), (62, 46), (50, 50), (38, 46)]), c["m"], OW)
    for s in (-1, 1):
        p.add(tent([(50 + s * 7, 36), (50 + s * 11, 28), (50 + s * 14, 24)], 3, 1), c["d"], 1.2)
    bat_wing(p, (42, 54), [(18, 40), (22, 26), (36, 30)], c["d"], c["w"])
    bat_wing(p, (58, 54), [(82, 40), (78, 26), (64, 30)], c["d"], c["w"])
    for y in (54, 62, 70):
        p.add(ell((48, y, 52, y + 3)), c["d"], 0, shade=False)


# ====================== 翼猫 ======================
TRESS = dict(m=(178, 150, 116), d=(130, 106, 80), b=(232, 218, 194), w=(228, 222, 206), wd=(176, 168, 150))


def cat_head_side(p, c, x, y):
    p.add(ell((x - 10, y - 9, x + 10, y + 8)), c["m"], OW)
    p.add(poly([(x - 6, y - 7), (x - 3, y - 15), (x + 1, y - 8)]), c["m"], 1.6)
    p.add(poly([(x + 2, y - 8), (x + 6, y - 15), (x + 8, y - 6)]), c["m"], 1.6)
    p.add(ell((x + 3, y - 1, x + 12, y + 7)), c["b"], 1.2)
    eye(p, x + 4, y - 2, 1.7, (90, 120, 70))
    p.add(ell((x + 10, y + 1, x + 12.5, y + 3)), (150, 90, 90), 0, shade=False)


def tress_east(p):
    c = TRESS
    feather_wing(p, (40, 54), [(18, 34), (22, 26), (30, 22), (40, 24)], c["wd"], c["d"])
    p.add(tent([(26, 62), (16, 56), (12, 44), (16, 36)], 5, 3.4), c["m"], OW)
    leg(p, [(32, 66), (32, 80)], 4.6, 3.6, c["d"])
    leg(p, [(56, 66), (57, 80)], 4.6, 3.6, c["d"])
    body = ell((24, 52, 66, 72))
    p.add(body, c["m"], OW)
    for x in (34, 42, 50):
        p.add(tent([(x, 52), (x - 2, 60)], 3, 1.4), c["d"], 0, shade=False, clip=body)
    p.add(ell((30, 64, 62, 78)), c["b"], 0, clip=body)
    leg(p, [(36, 68), (36, 81)], 4.8, 3.8, c["m"])
    leg(p, [(60, 68), (62, 81)], 4.8, 3.8, c["m"])
    cat_head_side(p, c, 70, 50)
    feather_wing(p, (44, 56), [(24, 36), (28, 26), (38, 20), (50, 22)], c["w"], c["wd"])


def tress_south(p):
    c = TRESS
    feather_wing(p, (40, 58), [(16, 44), (16, 34), (22, 26), (32, 26)], c["w"], c["wd"])
    feather_wing(p, (60, 58), [(84, 44), (84, 34), (78, 26), (68, 26)], c["w"], c["wd"])
    p.add(tent([(60, 74), (70, 72), (72, 62)], 4.4, 3.2), c["m"], 1.8)
    for s in (-1, 1):
        leg(p, [(50 + s * 7, 70), (50 + s * 8, 82)], 5, 4, c["m"])
    body = ell((36, 54, 64, 80))
    p.add(body, c["m"], OW)
    p.add(ell((42, 60, 58, 82)), c["b"], 0, clip=body)
    x, y = 50, 46
    p.add(ell((x - 12, y - 10, x + 12, y + 10)), c["m"], OW)
    for s in (-1, 1):
        p.add(poly([(x + s * 4, y - 8), (x + s * 9, y - 17), (x + s * 11, y - 5)]), c["m"], 1.6)
        eye(p, x + s * 5, y - 1, 1.8, (90, 120, 70))
    p.add(ell((x - 5, y + 2, x + 5, y + 9)), c["b"], 1.2)
    p.add(ell((x - 1.4, y + 2.4, x + 1.4, y + 4.4)), (150, 90, 90), 0, shade=False)


def tress_north(p):
    c = TRESS
    for s in (-1, 1):
        leg(p, [(50 + s * 7, 70), (50 + s * 8, 82)], 5, 4, c["d"])
    body = ell((36, 50, 64, 80))
    p.add(body, c["m"], OW)
    for y in (58, 66, 74):
        p.add(tent([(40, y), (50, y - 2), (60, y)], 2.2, 2.2), c["d"], 0, shade=False, clip=body)
    p.add(tent([(50, 78), (52, 88), (60, 92)], 5, 3.4), c["m"], OW)
    x, y = 50, 42
    p.add(ell((x - 11, y - 9, x + 11, y + 9)), c["m"], OW)
    for s in (-1, 1):
        p.add(poly([(x + s * 4, y - 7), (x + s * 9, y - 16), (x + s * 11, y - 4)]), c["m"], 1.6)
    feather_wing(p, (42, 56), [(18, 44), (18, 34), (24, 26), (34, 26)], c["w"], c["wd"])
    feather_wing(p, (58, 56), [(82, 44), (82, 34), (76, 26), (66, 26)], c["w"], c["wd"])


# ====================== 巨型火甲虫 ======================
BEETLE = dict(m=(98, 72, 58), d=(70, 52, 44), b=(140, 104, 80), g=(246, 168, 70), leg=(60, 46, 40))


def beetle_legs(p, c, roots, side=1, far=False):
    col = mix(c["leg"], (20, 18, 20), .3) if far else c["leg"]
    for i, (x, y) in enumerate(roots):
        dx = (i - 1) * 4
        leg(p, [(x, y), (x + dx + side * 3, y + 5), (x + dx * 1.4, y + 12)], 3, 1.8, col, 1.4)


def beetle_east(p):
    c = BEETLE
    beetle_legs(p, c, [(36, 66), (48, 67), (60, 66)], far=True)
    shell = ell((20, 42, 72, 74))
    p.add(shell, c["m"], OW)
    p.add(tent([(22, 58), (46, 50), (70, 54)], 1.4, 1.4), c["d"], 0, shade=False, clip=shell)
    p.add(ell((30, 46, 52, 54)), c["b"], 0, shade=False, clip=shell)
    glow_eye(p, 30, 66, 3.6, c["g"])
    beetle_legs(p, c, [(40, 68), (52, 69), (64, 68)])
    head = ell((66, 50, 84, 68))
    p.add(head, c["d"], OW)
    glow_eye(p, 74, 54, 3.2, c["g"])
    for a in (-1, 1):
        p.add(tent([(82, 62 + a * 2), (90, 62 + a * 4), (92, 60 + a * 6)], 2.6, 1.2), c["leg"], 1.2)
    thin(p, tent([(76, 52), (82, 40), (90, 34)], 1.6, .8), c["leg"])


def beetle_south(p):
    c = BEETLE
    for s in (-1, 1):
        for i, y in enumerate((56, 64, 72)):
            leg(p, [(50 + s * 14, y), (50 + s * 22, y + 2 + (i - 1) * 2), (50 + s * 25, y + 10)], 3, 1.8, c["leg"], 1.4)
    shell = ell((32, 42, 68, 80))
    p.add(shell, c["m"], OW)
    p.add(tent([(50, 44), (50, 80)], 1.4, 1.4), c["d"], 0, shade=False, clip=shell)
    head = ell((38, 60, 62, 82))
    p.add(head, c["d"], OW)
    for s in (-1, 1):
        glow_eye(p, 50 + s * 6, 66, 3.2, c["g"])
        p.add(tent([(50 + s * 4, 80), (50 + s * 6, 86), (50 + s * 2, 89)], 2.6, 1.2), c["leg"], 1.2)
        thin(p, tent([(50 + s * 5, 62), (50 + s * 12, 50), (50 + s * 16, 42)], 1.6, .8), c["leg"])


def beetle_north(p):
    c = BEETLE
    for s in (-1, 1):
        for i, y in enumerate((50, 58, 66)):
            leg(p, [(50 + s * 14, y), (50 + s * 22, y + 2 + (i - 1) * 2), (50 + s * 25, y + 10)], 3, 1.8, c["leg"], 1.4)
        thin(p, tent([(50 + s * 5, 36), (50 + s * 12, 26), (50 + s * 16, 20)], 1.6, .8), c["leg"])
    p.add(ell((40, 30, 60, 46)), c["d"], OW)
    shell = ell((30, 38, 70, 84))
    p.add(shell, c["m"], OW)
    p.add(tent([(50, 40), (50, 84)], 1.6, 1.6), c["d"], 0, shade=False, clip=shell)
    for s in (-1, 1):
        p.add(ell((50 + s * 8 - 5, 46, 50 + s * 8 + 5, 56)), c["b"], 0, shade=False, clip=shell)
        glow_eye(p, 50 + s * 9, 76, 3, c["g"])


# ====================== 罗斯牛 ======================
ROTHE = dict(m=(100, 84, 70), d=(72, 60, 52), b=(138, 116, 94), h=(218, 204, 172), snout=(150, 120, 110))


def shag(p, c, pts, clip):
    for x, y in pts:
        p.add(tent([(x, y), (x - 1.5, y + 6)], 2.4, .8), c["d"], 0, shade=False, clip=clip)


def rothe_east(p):
    c = ROTHE
    leg(p, [(30, 66), (29, 74), (30, 82)], 6, 5, c["d"], 1.8)
    leg(p, [(62, 66), (62, 74), (63, 82)], 6, 5, c["d"], 1.8)
    p.add(tent([(16, 52), (11, 58), (12, 66)], 3.4, 2.4), c["m"], 1.6)
    body = poly([(16, 50), (30, 38), (56, 36), (72, 42), (76, 58), (68, 70), (26, 70), (14, 62)])
    p.add(body, c["m"], OW)
    shag(p, c, [(24, 60), (32, 63), (40, 64), (48, 64), (56, 63), (64, 61), (36, 46), (48, 44), (60, 46)], body)
    leg(p, [(34, 66), (33, 75), (34, 84)], 6.4, 5.4, c["m"], 1.8)
    leg(p, [(66, 64), (68, 74), (69, 84)], 6.4, 5.4, c["m"], 1.8)
    for x, y in ((34, 84), (69, 84)):
        p.add(ell((x - 3.4, y - 2, x + 3.4, y + 2)), (50, 44, 40), 1.2)
    head = poly([(70, 44), (82, 42), (90, 52), (88, 62), (78, 64), (70, 56)])
    p.add(head, c["m"], OW)
    p.add(ell((80, 54, 92, 64)), c["snout"], 1.4)
    p.add(tent([(74, 44), (70, 36), (76, 30), (84, 32)], 4.4, 2), c["h"], 1.6)
    eye(p, 80, 48, 1.6)
    eye(p, 84, 46, 1.2, INK, False)  # 第三只眼
    eye(p, 76, 50, 1.2, INK, False)


def rothe_south(p):
    c = ROTHE
    for s in (-1, 1):
        leg(p, [(50 + s * 14, 68), (50 + s * 15, 84)], 7, 6, c["m"], 1.8)
        p.add(ell((50 + s * 15 - 3.6, 82, 50 + s * 15 + 3.6, 86)), (50, 44, 40), 1.2)
    body = ell((26, 40, 74, 78))
    p.add(body, c["m"], OW)
    shag(p, c, [(32, 62), (40, 66), (48, 68), (56, 66), (64, 62), (30, 50), (68, 50)], body)
    head = poly([(38, 44), (62, 44), (64, 62), (56, 72), (44, 72), (36, 62)])
    p.add(head, c["m"], OW)
    p.add(ell((42, 62, 58, 74)), c["snout"], 1.4)
    for s in (-1, 1):
        p.add(tent([(50 + s * 10, 46), (50 + s * 20, 40), (50 + s * 22, 30), (50 + s * 16, 26)], 4.4, 2), c["h"], 1.6)
        eye(p, 50 + s * 7, 54, 1.7)
        p.add(ell((50 + s * 4 - 1.2, 66, 50 + s * 4 + 1.2, 68.4)), INK, 0, shade=False)
    eye(p, 50, 49, 1.5)


def rothe_north(p):
    c = ROTHE
    for s in (-1, 1):
        leg(p, [(50 + s * 14, 68), (50 + s * 15, 84)], 7, 6, c["d"], 1.8)
        p.add(tent([(50 + s * 10, 34), (50 + s * 20, 28), (50 + s * 22, 18), (50 + s * 16, 14)], 4.4, 2), c["h"], 1.6)
    p.add(ell((38, 28, 62, 46)), c["m"], OW)
    body = ell((26, 38, 74, 80))
    p.add(body, c["m"], OW)
    shag(p, c, [(34, 48), (42, 46), (50, 45), (58, 46), (66, 48), (36, 62), (50, 60), (64, 62), (42, 72), (58, 72)], body)
    p.add(tent([(50, 78), (50, 86)], 3.4, 2.4), c["m"], 1.6)


# ====================== 鹰头马身兽 ======================
HIPPO = dict(m=(158, 118, 82), d=(118, 86, 60), head=(228, 220, 198), headd=(186, 176, 156), w=(150, 112, 78), wd=(108, 80, 56), beak=GOLD)


def eagle_head_side(p, c, x, y, s=1.0):
    head = poly([(x - 8 * s, y + 4 * s), (x - 6 * s, y - 6 * s), (x + 3 * s, y - 8 * s), (x + 9 * s, y - 3 * s), (x + 8 * s, y + 6 * s), (x - 2 * s, y + 9 * s)])
    p.add(head, c["head"], OW)
    p.add(poly([(x + 7 * s, y - 3 * s), (x + 15 * s, y - 1 * s), (x + 16 * s, y + 4 * s), (x + 12 * s, y + 6 * s), (x + 8 * s, y + 3 * s)]), c["beak"], 1.6)
    eye(p, x + 3 * s, y - 2 * s, 1.8 * s, (60, 40, 30))


def hippo_east(p):
    c = HIPPO
    feather_wing(p, (44, 46), [(16, 30), (20, 22), (28, 16), (38, 14)], c["wd"], (70, 52, 40))
    leg(p, [(28, 64), (26, 74), (27, 84)], 5.4, 4, c["d"])
    leg(p, [(60, 64), (62, 74), (62, 84)], 5, 3.4, c["headd"])
    p.add(tent([(18, 52), (12, 62), (10, 72)], 5, 2.4), (90, 66, 46), 1.8)
    body = poly([(18, 50), (30, 42), (58, 42), (70, 48), (70, 62), (56, 68), (24, 68), (16, 60)])
    p.add(body, c["m"], OW)
    p.add(poly([(52, 40), (72, 44), (72, 64), (52, 68)]), c["head"], 0, clip=body)
    leg(p, [(32, 64), (30, 75), (31, 86)], 5.8, 4.2, c["m"])
    leg(p, [(64, 64), (66, 75), (66, 86)], 5.2, 3.6, c["head"])
    for x in (31, 27):
        p.add(ell((x - 3, 84, x + 3, 88)), (54, 44, 36), 1.2)
    p.add(tent([(66, 56), (72, 44), (74, 36)], 12, 9), c["head"], OW)
    eagle_head_side(p, c, 76, 32)
    feather_wing(p, (48, 48), [(20, 34), (22, 24), (30, 16), (42, 12), (54, 14)], c["w"], c["wd"])


def hippo_south(p):
    c = HIPPO
    feather_wing(p, (40, 50), [(14, 44), (12, 32), (18, 22), (28, 18)], c["w"], c["wd"])
    feather_wing(p, (60, 50), [(86, 44), (88, 32), (82, 22), (72, 18)], c["w"], c["wd"])
    for s in (-1, 1):
        leg(p, [(50 + s * 10, 66), (50 + s * 11, 86)], 6, 4.4, c["head"])
    body = ell((32, 42, 68, 80))
    p.add(body, c["head"], OW)
    p.add(tent([(44, 46), (50, 50), (56, 46)], 3, 3), c["headd"], 0, shade=False, clip=body)
    x, y = 50, 34
    p.add(ell((x - 11, y - 11, x + 11, y + 10)), c["head"], OW)
    p.add(poly([(x - 5, y + 2), (x + 5, y + 2), (x + 3, y + 10), (x, y + 14), (x - 3, y + 10)]), c["beak"], 1.6)
    for s in (-1, 1):
        eye(p, x + s * 6, y - 2, 1.9, (60, 40, 30))


def hippo_north(p):
    c = HIPPO
    for s in (-1, 1):
        leg(p, [(50 + s * 11, 66), (50 + s * 12, 86)], 6.4, 4.6, c["d"])
    body = ell((30, 38, 70, 82))
    p.add(body, c["m"], OW)
    p.add(tent([(50, 78), (48, 88), (52, 94)], 6, 2.4), (90, 66, 46), 1.8)
    p.add(ell((40, 22, 60, 42)), c["head"], OW)
    feather_wing(p, (42, 46), [(16, 42), (12, 30), (18, 20), (28, 16)], c["w"], c["wd"])
    feather_wing(p, (58, 46), [(84, 42), (88, 30), (82, 20), (72, 16)], c["w"], c["wd"])


# ====================== 斧喙鸟 ======================
AXE = dict(m=(178, 150, 108), d=(134, 110, 78), b=(214, 196, 160), beak=(176, 170, 156), beakd=(126, 120, 110), leg=(204, 162, 92))


def axe_east(p):
    c = AXE
    leg(p, [(42, 64), (44, 74), (40, 86)], 3.4, 2.4, mix(c["leg"], INK, .25), 1.4)
    p.add(poly([(40, 86), (34, 88), (46, 88)]), mix(c["leg"], INK, .25), 1.2)
    p.add(poly([(28, 52), (12, 46), (14, 56), (26, 62)]), c["d"], OW)
    body = ell((24, 44, 62, 70))
    p.add(body, c["m"], OW)
    p.add(ell((34, 54, 62, 74)), c["b"], 0, clip=body)
    p.add(poly([(30, 50), (48, 48), (54, 56), (40, 62), (28, 60)]), c["d"], 1.6)
    leg(p, [(48, 64), (52, 75), (50, 87)], 3.6, 2.6, c["leg"], 1.4)
    p.add(poly([(50, 87), (44, 89), (58, 89)]), c["leg"], 1.2)
    p.add(tent([(56, 52), (62, 40), (64, 28)], 7, 5), c["m"], OW)
    head = ell((58, 18, 72, 32))
    p.add(head, c["m"], OW)
    # 斧刃似的喙
    p.add(poly([(68, 20), (86, 18), (92, 26), (84, 34), (68, 30)]), c["beak"], 1.8)
    p.add(tent([(70, 26), (88, 26)], 1.2, 1.2), c["beakd"], 0, shade=False)
    eye(p, 66, 23, 1.7)


def axe_south(p):
    c = AXE
    for s in (-1, 1):
        leg(p, [(50 + s * 7, 66), (50 + s * 8, 86)], 3.6, 2.6, c["leg"], 1.4)
        p.add(poly([(50 + s * 8, 86), (50 + s * 8 - 5, 89), (50 + s * 8 + 5, 89)]), c["leg"], 1.2)
    body = ell((32, 44, 68, 72))
    p.add(body, c["m"], OW)
    for s in (-1, 1):
        p.add(poly([(50 + s * 18, 50), (50 + s * 22, 58), (50 + s * 16, 66)]), c["d"], 1.6)
    p.add(ell((40, 52, 60, 74)), c["b"], 0, clip=body)
    p.add(tent([(50, 50), (50, 32)], 8, 6), c["m"], OW)
    p.add(ell((42, 16, 58, 32)), c["m"], OW)
    p.add(poly([(44, 26), (56, 26), (54, 40), (50, 44), (46, 40)]), c["beak"], 1.8)
    p.add(tent([(50, 28), (50, 42)], 1.2, 1.2), c["beakd"], 0, shade=False)
    for s in (-1, 1):
        eye(p, 50 + s * 5, 21, 1.6)


def axe_north(p):
    c = AXE
    for s in (-1, 1):
        leg(p, [(50 + s * 7, 66), (50 + s * 8, 86)], 3.6, 2.6, mix(c["leg"], INK, .2), 1.4)
    body = ell((32, 40, 68, 72))
    p.add(body, c["m"], OW)
    p.add(poly([(42, 66), (50, 82), (58, 66)]), c["d"], OW)
    for s in (-1, 1):
        p.add(poly([(50 + s * 4, 46), (50 + s * 18, 50), (50 + s * 14, 64)]), c["d"], 1.6)
    p.add(tent([(50, 46), (50, 30)], 8, 6), c["m"], OW)
    p.add(ell((42, 14, 58, 30)), c["m"], OW)


# ====================== 狮鹫 ======================
GRIF = dict(m=(196, 156, 98), d=(152, 116, 70), head=(234, 228, 208), headd=(190, 180, 158), w=(128, 98, 68), wd=(92, 70, 50), beak=GOLD)


def grif_east(p):
    c = GRIF
    feather_wing(p, (44, 44), [(12, 26), (16, 16), (26, 10), (38, 8)], c["wd"], (64, 48, 36))
    leg(p, [(28, 64), (26, 74), (28, 84)], 6.4, 5, c["d"])
    leg(p, [(60, 62), (62, 72), (62, 82)], 5, 3.4, mix(c["beak"], INK, .3))
    p.add(tent([(18, 54), (10, 64), (8, 76), (12, 80)], 4, 2.4), c["m"], 1.8)
    p.add(ell((8, 76, 16, 84)), c["d"], 1.4)
    body = poly([(18, 50), (30, 42), (58, 40), (70, 46), (70, 62), (56, 68), (24, 68), (16, 60)])
    p.add(body, c["m"], OW)
    p.add(poly([(54, 38), (72, 44), (72, 64), (52, 66)]), c["head"], 0, clip=body)
    leg(p, [(32, 64), (30, 75), (32, 86)], 6.6, 5.2, c["m"])
    p.add(ell((28, 83, 37, 88)), c["m"], 1.4)
    leg(p, [(64, 62), (66, 74), (66, 84)], 5, 3.4, c["beak"])
    for k in range(3):
        p.add(tent([(66, 84), (63 + k * 3, 89)], 1.8, .8), INK, 0, shade=False)
    p.add(tent([(66, 54), (72, 42), (74, 32)], 13, 10), c["head"], OW)
    eagle_head_side(p, c, 76, 28, 1.1)
    feather_wing(p, (48, 46), [(18, 30), (18, 18), (26, 10), (38, 4), (52, 6)], c["w"], c["wd"])


def grif_south(p):
    c = GRIF
    feather_wing(p, (40, 48), [(10, 42), (8, 28), (14, 16), (26, 10)], c["w"], c["wd"])
    feather_wing(p, (60, 48), [(90, 42), (92, 28), (86, 16), (74, 10)], c["w"], c["wd"])
    for s in (-1, 1):
        leg(p, [(50 + s * 11, 66), (50 + s * 12, 86)], 6, 4.4, c["beak"])
        for k in (-1, 0, 1):
            p.add(tent([(50 + s * 12, 85), (50 + s * 12 + k * 3, 89)], 1.8, .8), INK, 0, shade=False)
    body = ell((30, 40, 70, 80))
    p.add(body, c["head"], OW)
    x, y = 50, 30
    p.add(ell((x - 12, y - 12, x + 12, y + 11)), c["head"], OW)
    p.add(poly([(x - 6, y + 2), (x + 6, y + 2), (x + 3, y + 11), (x, y + 16), (x - 3, y + 11)]), c["beak"], 1.6)
    for s in (-1, 1):
        eye(p, x + s * 6.5, y - 2, 2.0, (60, 40, 30))
        p.add(tent([(x + s * 9, y - 9), (x + s * 13, y - 15)], 3, 1), c["headd"], 1.2)


def grif_north(p):
    c = GRIF
    for s in (-1, 1):
        leg(p, [(50 + s * 12, 66), (50 + s * 13, 86)], 7, 5.4, c["d"])
    body = ell((28, 36, 72, 82))
    p.add(body, c["m"], OW)
    p.add(tent([(50, 80), (46, 90), (52, 96)], 4, 2.4), c["m"], 1.8)
    p.add(ell((47, 92, 57, 99)), c["d"], 1.4)
    p.add(ell((39, 18, 61, 40)), c["head"], OW)
    for s in (-1, 1):
        p.add(tent([(50 + s * 8, 22), (50 + s * 12, 14)], 3, 1), c["headd"], 1.2)
    feather_wing(p, (42, 44), [(12, 40), (8, 26), (14, 14), (26, 8)], c["w"], c["wd"])
    feather_wing(p, (58, 44), [(88, 40), (92, 26), (86, 14), (74, 8)], c["w"], c["wd"])


# ====================== 钩爪恐兽 ======================
HOOK = dict(m=(112, 116, 106), d=(82, 86, 80), b=(146, 148, 136), hook=BONE, beak=(70, 66, 60))


def hook_arm(p, c, sh, el, tip, far=False):
    col = c["d"] if far else c["m"]
    leg(p, [sh, el], 6, 5, col, 1.8)
    # 弯钩：从肘部向前下弯成一个钩
    p.add(tent([el, (tip[0], tip[1] - 8), tip, (tip[0] - 5, tip[1] - 2)], 5, 1.4), c["hook"], 1.8)


def hook_east(p):
    c = HOOK
    hook_arm(p, c, (54, 44), (64, 58), (78, 62), far=True)
    leg(p, [(40, 64), (36, 74), (38, 86)], 6.4, 5, c["d"])
    body = poly([(30, 40), (46, 30), (60, 34), (62, 52), (54, 68), (36, 68), (28, 56)])
    p.add(body, c["m"], OW)
    for y in (42, 50, 58):
        p.add(tent([(32, y + 2), (46, y - 2), (60, y + 1)], 2, 2), c["d"], 0, shade=False, clip=body)
    leg(p, [(46, 64), (46, 75), (48, 87)], 6.8, 5.4, c["m"])
    p.add(ell((43, 85, 54, 89)), c["d"], 1.2)
    # 秃鹫似的头和喙
    head = poly([(56, 26), (66, 22), (74, 28), (72, 36), (62, 38), (56, 34)])
    p.add(head, c["b"], OW)
    p.add(poly([(70, 28), (82, 32), (84, 38), (78, 40), (72, 36)]), c["beak"], 1.6)
    eye(p, 64, 28, 1.4, (180, 150, 90))
    hook_arm(p, c, (50, 42), (60, 60), (74, 66))


def hook_south(p):
    c = HOOK
    for s in (-1, 1):
        leg(p, [(50 + s * 9, 64), (50 + s * 10, 86)], 7, 5.6, c["m"])
    body = poly([(32, 36), (68, 36), (70, 56), (60, 70), (40, 70), (30, 56)])
    p.add(body, c["m"], OW)
    for y in (46, 54, 62):
        p.add(tent([(34, y), (50, y + 2), (66, y)], 2, 2), c["d"], 0, shade=False, clip=body)
    head = poly([(40, 22), (60, 22), (62, 34), (50, 40), (38, 34)])
    p.add(head, c["b"], OW)
    p.add(poly([(45, 32), (55, 32), (52, 44), (50, 47), (48, 44)]), c["beak"], 1.6)
    for s in (-1, 1):
        eye(p, 50 + s * 6, 28, 1.4, (180, 150, 90))
        hook_arm(p, c, (50 + s * 18, 40), (50 + s * 24, 56), (50 + s * 18, 72))


def hook_north(p):
    c = HOOK
    for s in (-1, 1):
        leg(p, [(50 + s * 9, 64), (50 + s * 10, 86)], 7, 5.6, c["d"])
        hook_arm(p, c, (50 + s * 18, 40), (50 + s * 25, 54), (50 + s * 22, 70), far=True)
    body = poly([(32, 34), (68, 34), (70, 56), (60, 72), (40, 72), (30, 56)])
    p.add(body, c["m"], OW)
    for y in (42, 50, 58, 66):
        p.add(tent([(34, y + 2), (50, y - 2), (66, y + 2)], 2.2, 2.2), c["d"], 0, shade=False, clip=body)
    p.add(poly([(40, 20), (60, 20), (62, 32), (50, 36), (38, 32)]), c["b"], OW)


# ====================== 相位蜘蛛 ======================
SPIDER = dict(m=(80, 98, 140), d=(56, 70, 106), mark=(172, 202, 240), leg=(66, 80, 118))


def spider_legs(p, c, cx, cy, spread, far=False, side_view=True):
    col = mix(c["leg"], (20, 20, 30), .35) if far else c["leg"]
    for i in range(4):
        a = -30 + i * 22
        kx, ky = cx + spread * math.cos(math.radians(a)) * .9, cy - 12 + i * 2
        fx, fy = cx + spread * math.cos(math.radians(a)) * 1.25, cy + 16
        leg(p, [(cx, cy), (kx, ky), (fx, fy)], 3, 1.6, col, 1.4)


def spider_east(p):
    c = SPIDER
    for i, (kx, fx) in enumerate(((30, 22), (42, 36), (56, 54), (68, 72))):
        leg(p, [(48, 58), (kx, 40 + abs(i - 1.5) * 2), (fx, 82)], 3, 1.6, mix(c["leg"], (20, 20, 30), .35), 1.4)
    abdomen = ell((12, 40, 46, 70))
    p.add(abdomen, c["m"], OW)
    for k in range(3):
        p.add(poly([(20 + k * 8, 48), (24 + k * 8, 44), (28 + k * 8, 48), (24 + k * 8, 52)]), c["mark"], 0, shade=False, clip=abdomen)
    p.add(ell((40, 46, 64, 66)), c["d"], OW)
    for i, (kx, fx) in enumerate(((34, 26), (46, 40), (60, 60), (72, 80))):
        leg(p, [(52, 60), (kx, 42 + abs(i - 1.5) * 2), (fx, 86)], 3.2, 1.8, c["leg"], 1.4)
    p.add(ell((60, 48, 76, 62)), c["m"], OW)
    for k, (x, y) in enumerate(((70, 52), (73, 55), (68, 55))):
        glow_eye(p, x, y, 1.3, c["mark"])
    p.add(tent([(74, 58), (80, 64), (78, 68)], 2.6, 1.2), c["d"], 1.2)


def spider_south(p):
    c = SPIDER
    for s in (-1, 1):
        for i in range(4):
            kx = 50 + s * (16 + i * 4)
            leg(p, [(50 + s * 6, 52 + i * 2), (kx, 36 + i * 6), (50 + s * (26 + i * 5), 70 + i * 4)], 3, 1.6, c["leg"], 1.4)
    p.add(ell((34, 28, 66, 56)), c["m"], OW)
    p.add(ell((38, 46, 62, 72)), c["d"], OW)
    p.add(ell((40, 58, 60, 78)), c["m"], OW)
    for x, y in ((46, 64), (54, 64), (43, 68), (57, 68), (48, 61), (52, 61)):
        glow_eye(p, x, y, 1.4, c["mark"])
    for s in (-1, 1):
        p.add(tent([(50 + s * 3, 76), (50 + s * 5, 82), (50 + s * 2, 85)], 2.6, 1.2), c["d"], 1.2)


def spider_north(p):
    c = SPIDER
    for s in (-1, 1):
        for i in range(4):
            kx = 50 + s * (16 + i * 4)
            leg(p, [(50 + s * 6, 40 + i * 2), (kx, 24 + i * 6), (50 + s * (26 + i * 5), 58 + i * 4)], 3, 1.6, c["leg"], 1.4)
    p.add(ell((40, 26, 60, 44)), c["m"], OW)
    p.add(ell((38, 36, 62, 56)), c["d"], OW)
    abdomen = ell((30, 48, 70, 90))
    p.add(abdomen, c["m"], OW)
    for k in range(3):
        y = 56 + k * 10
        p.add(poly([(50, y - 4), (55, y), (50, y + 4), (45, y)]), c["mark"], 0, shade=False, clip=abdomen)


# ====================== 暗斗篷 ======================
DARK = dict(m=(66, 62, 72), d=(46, 42, 52), inner=(122, 82, 92), t=(86, 80, 92))


def dark_east(p):
    c = DARK
    for k in range(5):
        y = 62 + k * 2
        p.add(tent([(58, y - 4), (66 + k, y + 4), (72 + k * 2, y + 12), (68 + k * 3, y + 16)], 3, 1.2), c["t"], 1.4)
    # 斗篷状的膜：一大片向后飘
    mantle = poly([(66, 40), (52, 34), (30, 42), (16, 56), (22, 66), (34, 62), (44, 70), (56, 64), (70, 58)])
    p.add(mantle, c["m"], OW)
    for x in (26, 38, 50):
        p.add(tent([(x + 14, 42), (x, 62)], 1.4, 1.4), c["d"], 0, shade=False, clip=mantle)
    head = ell((58, 38, 78, 60))
    p.add(head, c["m"], OW)
    eye(p, 70, 46, 1.6, (200, 190, 120))


def dark_south(p):
    c = DARK
    mantle = poly([(50, 30), (70, 36), (84, 52), (78, 66), (64, 60), (50, 68), (36, 60), (22, 66), (16, 52), (30, 36)])
    p.add(mantle, c["m"], OW)
    for s in (-1, 1):
        p.add(tent([(50, 40), (50 + s * 26, 60)], 1.4, 1.4), c["d"], 0, shade=False, clip=mantle)
    for k in range(-3, 4):
        p.add(tent([(50 + k * 3, 60), (50 + k * 4, 72), (50 + k * 5.5, 82)], 3, 1.2), c["t"], 1.4)
    p.add(ell((40, 42, 60, 64)), c["m"], OW)
    for s in (-1, 1):
        eye(p, 50 + s * 5, 50, 1.6, (200, 190, 120))
    p.add(ell((46, 56, 54, 62)), c["inner"], 1.0, shade=False)


def dark_north(p):
    c = DARK
    for k in range(-3, 4):
        p.add(tent([(50 + k * 3, 66), (50 + k * 4, 76), (50 + k * 5.5, 86)], 3, 1.2), c["t"], 1.4)
    mantle = poly([(50, 28), (70, 34), (84, 50), (78, 66), (64, 62), (50, 72), (36, 62), (22, 66), (16, 50), (30, 34)])
    p.add(mantle, c["m"], OW)
    for s in (-1, 1):
        for k in (0, 1):
            p.add(tent([(50, 34), (50 + s * (18 + k * 10), 62 - k * 6)], 1.4, 1.4), c["d"], 0, shade=False, clip=mantle)


# ====================== 血蝠蝇 ======================
STIRGE = dict(m=(124, 76, 66), d=(90, 56, 50), w=(110, 74, 70), wd=(78, 52, 50), p=(206, 184, 160))


def stirge_east(p):
    c = STIRGE
    bat_wing(p, (44, 44), [(28, 16), (44, 10), (58, 18)], c["wd"], mix(c["w"], c["wd"], .4))
    for i, x in enumerate((36, 44, 52)):
        leg(p, [(x, 58), (x - 4 + i * 2, 66), (x - 6 + i * 3, 74)], 2.4, 1.4, c["d"], 1.2)
    p.add(ell((20, 42, 46, 62)), c["m"], OW)
    for x in (28, 34, 40):
        p.add(tent([(x, 43), (x - 1, 61)], 1.4, 1.4), c["d"], 0, shade=False, clip=ell((20, 42, 46, 62)))
    p.add(ell((42, 40, 62, 58)), c["m"], OW)
    p.add(tent([(60, 50), (76, 54), (90, 58)], 3, 1), c["p"], 1.4)
    eye(p, 54, 46, 2.0, (220, 200, 90))
    bat_wing(p, (48, 44), [(36, 12), (54, 4), (70, 14)], c["wd"], c["w"])


def stirge_south(p):
    c = STIRGE
    bat_wing(p, (42, 46), [(12, 34), (14, 20), (28, 18)], c["wd"], c["w"])
    bat_wing(p, (58, 46), [(88, 34), (86, 20), (72, 18)], c["wd"], c["w"])
    for s in (-1, 1):
        for i in range(3):
            leg(p, [(50 + s * 6, 56 + i * 3), (50 + s * 14, 62 + i * 3), (50 + s * 16, 70 + i * 3)], 2.4, 1.4, c["d"], 1.2)
    p.add(ell((38, 40, 62, 66)), c["m"], OW)
    p.add(ell((40, 30, 60, 50)), c["m"], OW)
    p.add(tent([(50, 46), (50, 60), (50, 72)], 3, 1), c["p"], 1.4)
    for s in (-1, 1):
        eye(p, 50 + s * 5, 38, 2.0, (220, 200, 90))


def stirge_north(p):
    c = STIRGE
    for s in (-1, 1):
        for i in range(3):
            leg(p, [(50 + s * 6, 50 + i * 3), (50 + s * 14, 56 + i * 3), (50 + s * 16, 64 + i * 3)], 2.4, 1.4, c["d"], 1.2)
    p.add(ell((40, 26, 60, 44)), c["m"], OW)
    body = ell((36, 38, 64, 78))
    p.add(body, c["m"], OW)
    for y in (50, 58, 66):
        p.add(tent([(38, y), (50, y - 2), (62, y)], 1.6, 1.6), c["d"], 0, shade=False, clip=body)
    bat_wing(p, (42, 44), [(12, 30), (16, 16), (30, 16)], c["wd"], c["w"])
    bat_wing(p, (58, 44), [(88, 30), (84, 16), (70, 16)], c["wd"], c["w"])


# ====================== 输出 ======================
BEASTS = {
    "Pseudodrake": (pseudo_south, pseudo_east, pseudo_north),
    "Tressym": (tress_south, tress_east, tress_north),
    "FireBeetle": (beetle_south, beetle_east, beetle_north),
    "Rothe": (rothe_south, rothe_east, rothe_north),
    "Hippogriff": (hippo_south, hippo_east, hippo_north),
    "AxeBeak": (axe_south, axe_east, axe_north),
    "Griffon": (grif_south, grif_east, grif_north),
    "HookHorror": (hook_south, hook_east, hook_north),
    "PhaseSpider": (spider_south, spider_east, spider_north),
    "Darkmantle": (dark_south, dark_east, dark_north),
    "Stirge": (stirge_south, stirge_east, stirge_north),
}
# 游戏里成年体的 drawSize（和 Defs/Beasts_DND2.xml 一致），用来画比例图
DRAW = {"Pseudodrake": 1.1, "Tressym": 1.05, "FireBeetle": 1.25, "Rothe": 2.2, "Hippogriff": 2.4, "AxeBeak": 1.9, "Griffon": 2.7,
        "HookHorror": 2.4, "PhaseSpider": 2.0, "Darkmantle": 1.3, "Stirge": 0.85}


def draw_all():
    paths = []
    base.TEX = TEX
    for name, (south, east, north) in BEASTS.items():
        for rot, fn in (("south", south), ("east", east), ("north", north)):
            p = SPic()
            fn(p)
            paths.append(p.save(f"{REL}/{name}/{name}_{rot}", 256))
    return paths


# 驮兽的“驮货”贴图 <名>Pack_*：在原图上加一对鞍袋（原版驮兽有驮货时换这张图）。
BAG = (132, 96, 62)
PACK = {
    "Hippogriff": {"east": [(26, 48, 44, 64)], "south": [(24, 52, 34, 68), (66, 52, 76, 68)], "north": [(24, 50, 34, 66), (66, 50, 76, 66)]},
    "Griffon": {"east": [(26, 46, 44, 62)], "south": [(22, 50, 32, 66), (68, 50, 78, 66)], "north": [(22, 48, 32, 64), (68, 48, 78, 64)]},
    "AxeBeak": {"east": [(32, 50, 46, 64)], "south": [(26, 50, 34, 64), (66, 50, 74, 64)], "north": [(26, 48, 34, 62), (66, 48, 74, 62)]},
}


def draw_packs():
    paths = []
    base.TEX = TEX
    for name, rots in PACK.items():
        for rot, bags in rots.items():
            src = os.path.join(TEX, *REL.split("/"), name, f"{name}_{rot}.png")
            p = SPic()
            p.img.alpha_composite(Image.open(src).convert("RGBA").resize(p.img.size, Image.LANCZOS))
            if rot != "east":  # 横跨背上的绑带
                x0, x1 = bags[0][2], bags[1][0]
                y = (bags[0][1] + bags[0][3]) / 2 - 4
                p.add(tent([(x0 - 2, y), (50, y - 4), (x1 + 2, y)], 2.4, 2.4), mix(BAG, INK, .3), 1.0)
            for b in bags:
                p.add(poly([(b[0], b[1] + 2), (b[2], b[1]), (b[2] + 1, b[3] - 2), (b[0] - 1, b[3])]), BAG, 1.8)
                p.add(tent([((b[0] + b[2]) / 2, b[1] + 1), ((b[0] + b[2]) / 2, b[3] - 1)], 1.6, 1.6), mix(BAG, INK, .35), 0, shade=False)
            paths.append(p.save(f"{REL}/{name}/{name}Pack_{rot}", 256))
    return paths

def preview():
    cell = 130
    bg = (236, 230, 216, 255)
    names = list(BEASTS)
    cols = ["south", "east", "west", "north"]
    per_row = 2
    rows = (len(names) + per_row - 1) // per_row
    block = len(cols) * (cell + 6) + 30
    W = 20 + per_row * block
    scale_h = 220
    out = Image.new("RGBA", (W, 20 + rows * (cell + 8) + scale_h), bg)
    d = ImageDraw.Draw(out)
    folder = os.path.join(TEX, *REL.split("/"))
    for i, name in enumerate(names):
        x = 20 + (i % per_row) * block
        y = 20 + (i // per_row) * (cell + 8)
        for col in cols:
            rot = "east" if col == "west" else col
            img = Image.open(os.path.join(folder, name, f"{name}_{rot}.png")).convert("RGBA")
            if col == "west":
                img = img.transpose(Image.FLIP_LEFT_RIGHT)
            out.alpha_composite(img.resize((cell, cell), Image.LANCZOS), (x, y))
            x += cell + 6
    unit = 40
    base_y = out.height - 24
    hx = 30
    d.ellipse((hx - 8, base_y - 44, hx + 8, base_y - 30), fill=(214, 168, 132), outline=INK, width=2)
    d.rectangle((hx - 9, base_y - 30, hx + 9, base_y - 8), fill=(110, 120, 90), outline=INK, width=2)
    d.line((hx - 5, base_y - 8, hx - 5, base_y), fill=INK, width=3)
    d.line((hx + 5, base_y - 8, hx + 5, base_y), fill=INK, width=3)
    x = 50
    for name in names:
        img = Image.open(os.path.join(folder, name, f"{name}_east.png")).convert("RGBA")
        sz = int(DRAW[name] * unit)
        out.alpha_composite(img.resize((sz, sz), Image.LANCZOS), (x, base_y - int(sz * .92)))
        x += int(sz * .8) + 4
    path = os.path.join(OUT, "ArtSource", "Preview", "BeastsDND2.png")
    os.makedirs(os.path.dirname(path), exist_ok=True)
    out.convert("RGB").save(path)
    return path


if __name__ == "__main__":
    if "--pack-only" not in sys.argv:
        draw_all()
    draw_packs()
    print(preview())
