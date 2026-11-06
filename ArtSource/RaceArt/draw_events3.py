# 1.117.0 第三批事件的贴图：宝箱怪、树人、塔拉斯克（三个朝向 + 干尸），宝箱和森林之心（建筑），七件新物品。
# 照 draw_wildmonsters.py 的画法（粗黑轮廓、平涂 + 柔和暗面、低饱和）；east 头朝右，west 由游戏镜像。
#  - Textures/Things/Pawn/Animal/LP_E3/<名>/<名>_{south,east,north}.png（256px）和 Dessicated_<名>_*
#  - Textures/Things/Building/LP_E3/{MimicChest,HeartTree}.png
#  - Textures/Things/Item/LP_E3/*.png（128px）
#  - ArtSource/Preview/Events3.png：预览
# 用法：python -I ArtSource/RaceArt/draw_events3.py <模组根目录>
import math, os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import draw_race_art as base
from draw_race_art import ell, tent, INK
from draw_dragons_1028 import SPic, poly, mix, CREAM
from draw_wildmonsters import dessicate
from PIL import Image, ImageDraw

ROOT = sys.argv[1] if len(sys.argv) > 1 else os.getcwd()
TEX = os.path.join(ROOT, "Textures")
REL = "Things/Pawn/Animal/LP_E3"
OW = 2.4
TOOTH = (240, 236, 224)
WOOD = dict(m=(150, 104, 62), d=(112, 76, 44), b=(196, 160, 72), g=(222, 192, 96), mouth=(110, 36, 44), tongue=(206, 104, 120))
BARK = dict(m=(112, 86, 60), d=(84, 64, 44), l=(104, 146, 82), ld=(76, 116, 62), eye=(214, 236, 140))
TARR = dict(m=(104, 116, 82), d=(78, 88, 62), s=(146, 132, 92), b=(186, 172, 128), h=(226, 218, 190), eye=(232, 140, 60), mouth=(112, 44, 40))


def teeth_row(p, x0, x1, y, n, down=True, size=3.2):
    step = (x1 - x0) / n
    for i in range(n):
        x = x0 + step * (i + .5)
        tip = y + size if down else y - size
        p.add(("poly", [(x - step * .42, y), (x + step * .42, y), (x, tip)]), TOOTH, 1.0, shade=False)


# ====================== 宝箱怪 ======================
def mimic_bands(p, c, box, xs):
    for x in xs:
        p.add(("poly", [(x - 1.6, box[1]), (x + 1.6, box[1]), (x + 1.6, box[3]), (x - 1.6, box[3])]), c["b"], 1.0, shade=False, clip=("poly", [(box[0], box[1]), (box[2], box[1]), (box[2], box[3]), (box[0], box[3])]))


def mimic_south(p):
    c = WOOD
    # 舌头垂到地上
    p.add(tent([(50, 56), (46, 70), (54, 82), (48, 94)], 7, 3), c["tongue"], OW)
    # 张开的箱盖（往后掀起）
    lid = poly([(20, 22), (80, 22), (84, 46), (16, 46)])
    p.add(lid, c["m"], OW)
    mimic_bands(p, c, (16, 22, 84, 46), (26, 50, 74))
    p.add(ell((30, 28, 38, 35)), (236, 214, 96), 1.2)
    p.add(ell((62, 28, 70, 35)), (236, 214, 96), 1.2)
    p.add(ell((33, 30, 36, 34)), INK, 0, shade=False)
    p.add(ell((65, 30, 68, 34)), INK, 0, shade=False)
    # 嘴里
    p.add(poly([(18, 46), (82, 46), (80, 58), (20, 58)]), c["mouth"], OW)
    teeth_row(p, 19, 81, 46, 9, True, 4.2)
    teeth_row(p, 20, 80, 58, 9, False, 3.6)
    # 箱身
    body = poly([(18, 58), (82, 58), (80, 86), (20, 86)])
    p.add(body, c["m"], OW)
    for y in (66, 76):
        p.add(tent([(20, y), (80, y)], 1.0, 1.0), c["d"], 0, shade=False, clip=body)
    mimic_bands(p, c, (18, 58, 82, 86), (26, 50, 74))
    p.add(poly([(46, 60), (54, 60), (54, 68), (46, 68)]), c["g"], 1.2)
    p.add(tent([(50, 56), (50, 72)], 5, 4), c["tongue"], 1.4, shade=False)


def mimic_east(p):
    c = WOOD
    body = poly([(18, 56), (72, 56), (74, 86), (16, 86)])
    # 箱盖：铰链在左后方，向右上张开
    lid = poly([(18, 56), (26, 26), (70, 18), (74, 28), (34, 50)])
    p.add(lid, c["m"], OW)
    p.add(tent([(30, 40), (66, 26)], 2.6, 2.6), c["b"], 0, shade=False, clip=lid)
    p.add(ell((52, 26, 59, 33)), (236, 214, 96), 1.2)
    p.add(ell((55, 28, 58, 32)), INK, 0, shade=False)
    p.add(poly([(24, 52), (70, 30), (74, 56), (22, 56)]), c["mouth"], OW)
    for k in range(6):
        x = 30 + k * 7
        y = 50 - k * 3.6
        p.add(("poly", [(x - 2.6, y), (x + 2.6, y - 1.2), (x + 1, y + 4)]), TOOTH, 1.0, shade=False)
    teeth_row(p, 24, 72, 56, 7, False, 3.6)
    p.add(tent([(58, 54), (76, 48), (90, 58), (96, 70)], 6, 2.4), c["tongue"], OW)
    p.add(body, c["m"], OW)
    for y in (66, 76):
        p.add(tent([(18, y), (72, y)], 1.0, 1.0), c["d"], 0, shade=False, clip=body)
    mimic_bands(p, c, (16, 56, 74, 86), (24, 44, 66))


def mimic_north(p):
    c = WOOD
    lid = poly([(20, 22), (80, 22), (84, 52), (16, 52)])
    p.add(lid, c["d"], OW)
    mimic_bands(p, c, (16, 22, 84, 52), (26, 50, 74))
    body = poly([(18, 52), (82, 52), (80, 86), (20, 86)])
    p.add(body, c["m"], OW)
    for y in (62, 74):
        p.add(tent([(20, y), (80, y)], 1.0, 1.0), c["d"], 0, shade=False, clip=body)
    mimic_bands(p, c, (18, 52, 82, 86), (26, 50, 74))


def chest_closed(p):
    c = WOOD
    lid = poly([(14, 30), (86, 30), (88, 50), (12, 50)])
    p.add(lid, c["m"], OW)
    p.add(tent([(14, 34), (86, 34)], 1.0, 1.0), c["d"], 0, shade=False, clip=lid)
    mimic_bands(p, c, (12, 30, 88, 50), (24, 50, 76))
    body = poly([(14, 50), (86, 50), (84, 84), (16, 84)])
    p.add(body, c["m"], OW)
    for y in (60, 72):
        p.add(tent([(14, y), (86, y)], 1.0, 1.0), c["d"], 0, shade=False, clip=body)
    mimic_bands(p, c, (14, 50, 86, 84), (24, 50, 76))
    p.add(poly([(44, 46), (56, 46), (56, 58), (44, 58)]), c["g"], 1.4)
    p.add(ell((48.5, 50, 51.5, 54)), INK, 0, shade=False)


# ====================== 树人 ======================
def crown(p, c, cx, cy, r, dark=False):
    blobs = [(-.6, .1, .55), (.6, .1, .55), (0, -.45, .6), (-.35, -.3, .5), (.35, -.3, .5), (0, .2, .6)]
    for dx, dy, s in blobs:
        x, y, rr = cx + dx * r, cy + dy * r, s * r
        p.add(ell((x - rr, y - rr, x + rr, y + rr)), c["ld"] if dark else c["l"], OW)


def treant_south(p):
    c = BARK
    crown(p, c, 50, 22, 22, dark=True)
    for s in (-1, 1):  # 根脚
        p.add(tent([(50 + s * 8, 78), (50 + s * 14, 88), (50 + s * 20, 92)], 6, 3), c["d"], OW)
    trunk = poly([(38, 30), (62, 30), (66, 82), (34, 82)])
    p.add(trunk, c["m"], OW)
    for x in (42, 50, 58):
        p.add(tent([(x, 34), (x + 1, 56), (x - 1, 80)], 1.6, 1.0), c["d"], 0, shade=False, clip=trunk)
    for s in (-1, 1):  # 枝臂
        p.add(tent([(50 + s * 12, 38), (50 + s * 24, 50), (50 + s * 28, 66)], 6, 3.2), c["m"], OW)
        for k in (-1, 0, 1):
            p.add(tent([(50 + s * 28, 66), (50 + s * (28 + k * 3), 72)], 2, 1), c["d"], 1.0)
        p.add(ell((50 + s * 30 - 5, 44, 50 + s * 30 + 5, 54)), c["l"], 1.6)
    crown(p, c, 50, 18, 18)
    p.add(ell((42, 40, 47, 45)), c["eye"], 1.2)
    p.add(ell((53, 40, 58, 45)), c["eye"], 1.2)
    p.add(tent([(44, 54), (50, 56), (56, 54)], 1.6, 1.6), INK, 0, shade=False)


def treant_east(p):
    c = BARK
    crown(p, c, 46, 22, 21, dark=True)
    p.add(tent([(40, 78), (34, 90), (28, 93)], 6, 3), c["d"], OW)
    p.add(tent([(54, 78), (60, 90), (66, 93)], 6, 3), c["d"], OW)
    trunk = poly([(36, 30), (58, 30), (62, 82), (32, 82)])
    p.add(tent([(42, 38), (34, 52), (32, 66)], 5, 3), c["d"], OW)
    p.add(trunk, c["m"], OW)
    for x in (40, 48, 55):
        p.add(tent([(x, 34), (x + 1, 56), (x - 1, 80)], 1.6, 1.0), c["d"], 0, shade=False, clip=trunk)
    p.add(tent([(54, 38), (68, 46), (80, 50)], 6, 3.2), c["m"], OW)
    for k in (-1, 0, 1):
        p.add(tent([(80, 50), (86, 50 + k * 4)], 2, 1), c["d"], 1.0)
    p.add(ell((70, 40, 80, 49)), c["l"], 1.6)
    crown(p, c, 46, 18, 17)
    p.add(ell((53, 40, 58, 45)), c["eye"], 1.2)
    p.add(tent([(52, 54), (58, 55)], 1.6, 1.6), INK, 0, shade=False)


def treant_north(p):
    c = BARK
    for s in (-1, 1):
        p.add(tent([(50 + s * 8, 78), (50 + s * 14, 88), (50 + s * 20, 92)], 6, 3), c["d"], OW)
        p.add(tent([(50 + s * 12, 38), (50 + s * 24, 50), (50 + s * 28, 66)], 6, 3.2), c["d"], OW)
    trunk = poly([(38, 30), (62, 30), (66, 82), (34, 82)])
    p.add(trunk, c["m"], OW)
    for x in (42, 50, 58):
        p.add(tent([(x, 34), (x + 1, 56), (x - 1, 80)], 1.6, 1.0), c["d"], 0, shade=False, clip=trunk)
    crown(p, c, 50, 22, 22, dark=True)
    crown(p, c, 50, 20, 17)


# ====================== 塔拉斯克 ======================
def spikes(p, c, pts, h=6):
    for x, y in pts:
        p.add(("poly", [(x - 3, y + 1), (x + 3, y + 1), (x, y - h)]), c["h"], 1.2, shade=False)


def tarr_south(p):
    c = TARR
    for s in (-1, 1):  # 后腿
        p.add(poly([(50 + s * 22, 60), (50 + s * 32, 62), (50 + s * 34, 88), (50 + s * 22, 88)]), c["d"], OW)
    body = ell((14, 26, 86, 84))
    p.add(body, c["m"], OW)
    for k, y in enumerate((34, 46, 58, 70)):
        p.add(tent([(20 + k, y), (50, y - 4), (80 - k, y)], 2.0, 2.0), c["s"], 0, shade=False, clip=body)
    spikes(p, c, [(22, 32), (32, 26), (68, 26), (78, 32)], 7)
    for s in (-1, 1):  # 前腿
        p.add(poly([(50 + s * 12, 60), (50 + s * 24, 60), (50 + s * 26, 92), (50 + s * 10, 92)]), c["m"], OW)
        for k in (-1, 0, 1):
            x = 50 + s * 18 + k * 4
            p.add(("poly", [(x - 1.6, 92), (x + 1.6, 92), (x, 96)]), c["h"], 1.0, shade=False)
    # 头和角
    for s in (-1, 1):
        p.add(tent([(50 + s * 10, 40), (50 + s * 22, 28), (50 + s * 24, 14)], 5, 1.6), c["h"], OW)
    head = poly([(36, 38), (64, 38), (66, 60), (56, 72), (44, 72), (34, 60)])
    p.add(head, c["m"], OW)
    p.add(ell((39, 44, 45, 50)), c["eye"], 1.2)
    p.add(ell((55, 44, 61, 50)), c["eye"], 1.2)
    p.add(poly([(40, 60), (60, 60), (56, 70), (44, 70)]), c["mouth"], 1.6)
    teeth_row(p, 40, 60, 60, 6, True, 3.4)
    teeth_row(p, 44, 56, 70, 4, False, 3.0)


def tarr_east(p):
    c = TARR
    p.add(tent([(22, 62), (10, 66), (2, 74)], 12, 3), c["m"], OW)  # 尾巴
    spikes(p, c, [(10, 64), (16, 61)], 4)
    for x in (28, 58):  # 远侧腿
        p.add(poly([(x, 66), (x + 10, 66), (x + 11, 90), (x - 1, 90)]), c["d"], OW)
    body = ell((14, 30, 82, 82))
    p.add(body, c["m"], OW)
    for k, x in enumerate((26, 38, 50, 62, 72)):
        p.add(tent([(x - 6, 36 + abs(k - 2) * 2), (x, 56), (x - 4, 76)], 1.8, 1.8), c["s"], 0, shade=False, clip=body)
    spikes(p, c, [(24, 38), (34, 32), (46, 30), (58, 31), (70, 36)], 8)
    p.add(ell((22, 66, 72, 86)), c["b"], 0, clip=body)
    for x in (20, 52):  # 近侧腿
        p.add(poly([(x, 64), (x + 12, 64), (x + 13, 92), (x - 1, 92)]), c["m"], OW)
        for k in (0, 1, 2):
            xx = x + 2 + k * 4
            p.add(("poly", [(xx - 1.4, 92), (xx + 1.4, 92), (xx + .4, 96)]), c["h"], 1.0, shade=False)
    # 头
    p.add(tent([(76, 40), (70, 26), (60, 18)], 5, 1.6), c["h"], OW)
    head = poly([(70, 38), (88, 40), (98, 52), (96, 62), (78, 64), (70, 56)])
    p.add(head, c["m"], OW)
    p.add(poly([(80, 56), (97, 56), (95, 64), (80, 64)]), c["mouth"], 1.4)
    teeth_row(p, 81, 96, 56, 4, True, 3.0)
    p.add(ell((82, 44, 87, 49)), c["eye"], 1.2)


def tarr_north(p):
    c = TARR
    p.add(tent([(50, 70), (52, 86), (48, 98)], 12, 3), c["m"], OW)
    for s in (-1, 1):
        p.add(poly([(50 + s * 18, 60), (50 + s * 30, 62), (50 + s * 32, 90), (50 + s * 18, 90)]), c["d"], OW)
    body = ell((14, 22, 86, 80))
    p.add(body, c["m"], OW)
    for y in (32, 44, 56, 68):
        p.add(tent([(18, y + 4), (50, y), (82, y + 4)], 2.0, 2.0), c["s"], 0, shade=False, clip=body)
    spikes(p, c, [(50, 26), (50, 38), (50, 50), (50, 62), (36, 30), (64, 30)], 8)
    for s in (-1, 1):
        p.add(tent([(50 + s * 10, 26), (50 + s * 22, 14), (50 + s * 24, 4)], 5, 1.6), c["h"], OW)


# ====================== 建筑与物品 ======================
def heart_tree(p):
    c = BARK
    p.add(tent([(50, 92), (48, 70), (52, 50)], 9, 5), c["m"], OW)
    for s in (-1, 1):
        p.add(tent([(50, 66), (50 + s * 14, 54), (50 + s * 18, 44)], 4, 2), c["m"], OW)
    silver = dict(l=(196, 214, 210), ld=(150, 176, 172))
    crown(p, silver, 50, 30, 24, dark=True)
    crown(p, silver, 50, 26, 19)
    for x, y in ((40, 24), (58, 20), (50, 34), (64, 32), (36, 34)):
        p.add(ell((x - 2, y - 2, x + 2, y + 2)), (250, 246, 200), 0, shade=False)
    p.add(tent([(50, 86), (50, 56)], 2, 1), (238, 232, 170), 0, shade=False)


def scale(p, x, y, r, col):
    p.add(poly([(x - r, y - r * .2), (x, y - r * 1.1), (x + r, y - r * .2), (x + r * .7, y + r * .7), (x, y + r), (x - r * .7, y + r * .7)]), col, 2.0)
    p.add(tent([(x, y - r * .8), (x, y + r * .7)], 1.2, .6), mix(col, (60, 70, 90), .3), 0, shade=False)


def item_scale(p):
    scale(p, 38, 56, 20, (176, 192, 210))
    scale(p, 62, 50, 20, (206, 218, 232))
    scale(p, 50, 66, 18, (226, 234, 244))


def item_tooth(p):
    p.add(tent([(32, 80), (34, 50), (48, 30), (70, 22)], 14, 2), TOOTH, 2.6)
    p.add(tent([(30, 82), (40, 76)], 8, 6), (150, 104, 62), 2.0)


def item_star(p):
    p.add(ell((22, 22, 78, 78)), (170, 196, 236), 0, shade=False)
    pts = []
    for k in range(10):
        a = math.radians(-90 + k * 36)
        r = 30 if k % 2 == 0 else 13
        pts.append((50 + r * math.cos(a), 50 + r * math.sin(a)))
    p.add(("poly", pts), (226, 236, 252), 2.4)
    p.add(ell((42, 42, 58, 58)), (255, 255, 236), 0, shade=False)


def item_log(p):
    p.add(poly([(22, 26), (74, 22), (80, 74), (28, 80)]), (124, 78, 46), 2.6)
    p.add(poly([(28, 30), (70, 27), (74, 70), (32, 74)]), (150, 100, 60), 0)
    p.add(tent([(52, 24), (55, 78)], 4, 4), (86, 54, 34), 1.4)
    p.add(ell((50, 44, 60, 54)), (214, 184, 118), 1.4)


def item_trophy(p):
    gold = (220, 186, 92)
    p.add(poly([(30, 22), (70, 22), (64, 48), (54, 56), (46, 56), (36, 48)]), gold, 2.6)
    for s in (-1, 1):
        p.add(tent([(50 + s * 18, 28), (50 + s * 28, 30), (50 + s * 24, 42), (50 + s * 14, 44)], 3, 3), gold, 2.0)
    p.add(poly([(46, 56), (54, 56), (56, 70), (44, 70)]), gold, 2.2)
    p.add(poly([(34, 70), (66, 70), (68, 80), (32, 80)]), (150, 104, 62), 2.4)
    p.add(ell((44, 30, 56, 42)), (166, 62, 56), 1.4)


def item_shell(p):
    c = TARR
    p.add(poly([(16, 64), (26, 36), (52, 26), (78, 34), (86, 62), (60, 76), (32, 76)]), c["m"], 2.6)
    for y in (44, 56):
        p.add(tent([(24, y + 4), (52, y - 2), (80, y + 4)], 2, 2), c["s"], 0, shade=False)
    spikes(p, c, [(36, 34), (52, 28), (68, 32)], 8)


def item_ttooth(p):
    p.add(tent([(24, 84), (30, 56), (50, 30), (76, 16)], 18, 2.4), TOOTH, 2.6)
    p.add(tent([(22, 86), (34, 76)], 12, 8), (104, 116, 82), 2.0)


MONSTERS = {
    "Mimic": (mimic_south, mimic_east, mimic_north),
    "Treant": (treant_south, treant_east, treant_north),
    "Tarrasque": (tarr_south, tarr_east, tarr_north),
}
ITEMS = {
    "SilverDragonScale": item_scale, "MimicTooth": item_tooth, "StarHeart": item_star, "AdventureLog": item_log,
    "TourneyTrophy": item_trophy, "TarrasqueShell": item_shell, "TarrasqueTooth": item_ttooth,
}


def draw_all():
    base.TEX = TEX
    paths = []
    for name, fns in MONSTERS.items():
        for rot, fn in zip(("south", "east", "north"), fns):
            p = SPic()
            fn(p)
            path = p.save(f"{REL}/{name}/{name}_{rot}", 256)
            dessicate(Image.open(path).convert("RGBA"), name).save(os.path.join(TEX, *REL.split("/"), name, f"Dessicated_{name}_{rot}.png"))
            paths.append(path)
    p = SPic(); chest_closed(p); paths.append(p.save("Things/Building/LP_E3/MimicChest", 128))
    p = SPic(); heart_tree(p); paths.append(p.save("Things/Building/LP_E3/HeartTree", 256))
    for name, fn in ITEMS.items():
        p = SPic(); fn(p); paths.append(p.save(f"Things/Item/LP_E3/{name}", 128))
    return paths


def preview(paths):
    cell = 150
    cols = 6
    rows = (len(paths) + cols - 1) // cols
    out = Image.new("RGBA", (20 + cols * (cell + 10), 20 + rows * (cell + 10)), (236, 230, 216, 255))
    for i, path in enumerate(paths):
        img = Image.open(path).convert("RGBA").resize((cell, cell), Image.LANCZOS)
        out.alpha_composite(img, (20 + (i % cols) * (cell + 10), 20 + (i // cols) * (cell + 10)))
    path = os.path.join(ROOT, "ArtSource", "Preview", "Events3.png")
    os.makedirs(os.path.dirname(path), exist_ok=True)
    out.convert("RGB").save(path)
    return path


if __name__ == "__main__":
    print(preview(draw_all()))
