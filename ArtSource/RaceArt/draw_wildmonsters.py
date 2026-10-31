# DND 怪物第二批（锈蚀怪、凝胶怪、石化蜥、蝎尾狮、掘地兽、腐尸爬虫）：照 draw_dragons_1028.py 的画法
# （粗黑轮廓、平涂 + 柔和暗面、低饱和），整只动物一张图，原版动物贴图的朝向：east 头朝右，west 由游戏镜像。
#  - Textures/Things/Pawn/Animal/LP_WildMonsters/<名>/<名>_{south,east,north}.png（256px）和 Dessicated_<名>_*（褪色的干尸）
#  - Textures/Things/Projectile/LP_ManticoreSpike.png（蝎尾狮尾刺，竖直朝上，原版子弹的写法）
#  - ArtSource/Preview/WildMonsters.png：六只怪四个朝向，再按游戏里的成年体型和一个人比大小
# 用法：python -I ArtSource/RaceArt/draw_wildmonsters.py <模组根目录> [输出根目录]
import math, os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import draw_race_art as base
from draw_race_art import ell, tent, ellpts, INK
from draw_dragons_1028 import SPic, poly, mix, CREAM
from PIL import Image, ImageDraw, ImageFilter, ImageChops, ImageEnhance

ROOT = sys.argv[1] if len(sys.argv) > 1 else os.getcwd()
OUT = sys.argv[2] if len(sys.argv) > 2 else ROOT
TEX = os.path.join(OUT, "Textures")
REL = "Things/Pawn/Animal/LP_WildMonsters"
OW = 2.4
TOOTH = (240, 236, 224)


def thin(p, parts, color, ow=1.2):
    """细长的东西（触角杆、翼骨）不走 SPic 的圆角：圆角的模糊会把 2 个单位以下的细条磨没。"""
    base.Pic.add(p, parts, color, ow)


def legs_insect(p, c, roots, length=13, far=False, side=1):
    """细长的节肢：先向外上再折向下。roots 是腿根；side=1 向右伸，-1 向左。"""
    col = c["d"] if far else c["m"]
    for i, (x, y) in enumerate(roots):
        dx = (i - (len(roots) - 1) / 2) * 2.2
        knee = (x + dx + side * 2, y - 4)
        foot = (x + dx * 1.6 + side * 1, y + length)
        p.add(tent([(x, y), knee, foot], 3.2, 1.6), col, 1.6)


# ====================== 锈蚀怪 ======================
RUST = dict(m=(176, 106, 62), d=(130, 76, 48), b=(212, 178, 128), h=(218, 196, 150), a=(150, 170, 176))


def rust_antenna(p, c, base_pt, pts):
    thin(p, tent([base_pt] + pts, 2.0, 1.2), c["d"], 1.2)
    tip = pts[-1]
    # 羽状的触角尖
    for k in range(4):
        a = math.radians(-60 + k * 40)
        p.add(tent([tip, (tip[0] + 5 * math.cos(a), tip[1] + 5 * math.sin(a) - 2)], 2.2, .6), c["a"], 1.0, shade=False)
    p.add(ell((tip[0] - 2.2, tip[1] - 2.2, tip[0] + 2.2, tip[1] + 2.2)), c["a"], 1.2)


def rust_east(p):
    c = RUST
    legs_insect(p, c, [(34, 70), (46, 71), (58, 71)], 14, far=True, side=-1)
    # 螺旋桨似的尾巴
    p.add(tent([(30, 62), (18, 60), (10, 54)], 8, 3), c["m"], OW)
    p.add(poly([(10, 54), (3, 46), (6, 52), (2, 60), (9, 57)]), c["h"], 1.4)
    p.add(poly([(10, 54), (16, 46), (13, 53), (18, 60), (11, 57)]), c["h"], 1.4)
    body = ell((22, 42, 76, 76))
    p.add(body, c["m"], OW)
    for x in (32, 42, 52, 62):  # 背甲的环节
        p.add(tent([(x - 1, 44), (x + 1, 58), (x - 1, 74)], 2.4, 1.2), c["d"], 0, shade=False, clip=body)
    p.add(ell((26, 64, 74, 84)), c["b"], 0, clip=body)
    legs_insect(p, c, [(38, 72), (50, 73), (62, 72)], 14, side=-1)
    # 头
    head = poly([(70, 52), (82, 50), (90, 58), (88, 68), (78, 70), (70, 66)])
    p.add(head, c["m"], OW)
    p.add(poly([(84, 64), (92, 66), (90, 71), (82, 70)]), c["b"], 1.4)
    p.add(ell((80, 55, 85, 60)), INK, 0)
    p.add(ell((81.3, 55.8, 82.8, 57.3)), CREAM, 0, shade=False)
    rust_antenna(p, c, (82, 52), [(86, 40), (92, 30), (96, 24)])
    rust_antenna(p, c, (78, 52), [(78, 38), (82, 26), (88, 18)])


def rust_south(p):
    c = RUST
    for s in (-1, 1):
        for k, (x, y) in enumerate([(36, 68), (32, 74), (36, 80)]):
            xx = 50 + s * (50 - x)
            p.add(tent([(xx, y), (xx + s * 12, y - 4), (xx + s * 17, y + 10)], 3.2, 1.6), c["d"] if k == 1 else c["m"], 1.6)
    p.add(ell((26, 44, 74, 86)), c["m"], OW)
    for y in (52, 60, 68):
        p.add(tent([(30, y), (50, y - 3), (70, y)], 2.2, 2.2), c["d"], 0, shade=False, clip=ell((26, 44, 74, 86)))
    head = poly([(38, 58), (62, 58), (64, 76), (50, 84), (36, 76)])
    p.add(head, c["m"], OW)
    p.add(poly([(43, 75), (57, 75), (54, 84), (46, 84)]), c["b"], 1.4)
    for x in (43, 57):
        p.add(ell((x - 2.6, 64, x + 2.6, 69)), INK, 0)
        p.add(ell((x - 1.6, 64.6, x - .2, 66)), CREAM, 0, shade=False)
    rust_antenna(p, c, (45, 60), [(40, 46), (34, 34), (28, 24)])
    rust_antenna(p, c, (55, 60), [(60, 46), (66, 34), (72, 24)])


def rust_north(p):
    c = RUST
    rust_antenna(p, c, (45, 44), [(40, 34), (34, 24), (28, 16)])
    rust_antenna(p, c, (55, 44), [(60, 34), (66, 24), (72, 16)])
    p.add(ell((38, 36, 62, 54)), c["m"], OW)
    for s in (-1, 1):
        for k, y in enumerate((56, 64, 72)):
            xx = 50 + s * 18
            p.add(tent([(xx, y), (xx + s * 12, y - 4), (xx + s * 16, y + 10)], 3.2, 1.6), c["m"], 1.6)
    body = ell((26, 44, 74, 84))
    p.add(body, c["m"], OW)
    for y in (52, 60, 68, 76):
        p.add(tent([(28, y + 2), (50, y - 2), (72, y + 2)], 2.4, 2.4), c["d"], 0, shade=False, clip=body)
    p.add(tent([(50, 80), (50, 90)], 7, 4), c["m"], OW)
    p.add(poly([(50, 90), (40, 94), (46, 91), (50, 99), (54, 91), (60, 94)]), c["h"], 1.4)


# ====================== 凝胶怪 ======================
GEL = (118, 196, 134)
GEL_D = (82, 156, 104)
GEL_L = (176, 228, 180)


class GelPic(SPic):
    def glass(self, parts, color, alpha, ow=OW):
        if parts and isinstance(parts[0], str):
            parts = [parts]
        m = self.mask(parts)
        if ow:
            o = m.filter(ImageFilter.GaussianBlur(ow * self.k / 2)).point(lambda v: 255 if v > 6 else 0)
            ring = ImageChops.subtract(o.filter(ImageFilter.MaxFilter(5)), m)
            self.img.paste(Image.new("RGBA", self.img.size, INK + (255,)), (0, 0), ring)
        layer = Image.new("RGBA", self.img.size, color + (0,))
        layer.putalpha(m.point(lambda v: int(v * alpha / 255)))
        self.img.alpha_composite(layer)


def junk(p, view):
    """吞进去的东西：骷髅、剑、硬币、铁盔，颜色压暗偏绿，像泡在胶里。"""
    t = lambda col: mix(col, GEL_D, .35)
    if view == "north":
        p.add(ell((40, 46, 52, 56)), t((214, 204, 176)), 1.2)
        p.add(tent([(56, 60), (70, 48)], 2.4, 1.6), t((176, 180, 184)), 1.2)
        return
    p.add(ell((30, 52, 44, 64)), t((220, 210, 180)), 1.4)  # 头骨
    p.add(ell((33, 56, 36.5, 59.5)), INK, 0)
    p.add(ell((38, 56, 41.5, 59.5)), INK, 0)
    p.add(poly([(34, 63), (40, 63), (39, 67), (35, 67)]), t((220, 210, 180)), 1.0)
    p.add(tent([(48, 76), (66, 54)], 2.6, 1.4), t((180, 184, 190)), 1.2)  # 剑
    p.add(tent([(47, 72), (53, 78)], 1.6, 1.6), t((150, 110, 70)), 1.0)
    for x, y in ((60, 78), (64, 80), (58, 82)):
        p.add(ell((x - 2, y - 1.4, x + 2, y + 1.4)), t((224, 190, 90)), .9)
    p.add(poly([(62, 36), (72, 36), (73, 44), (61, 44)]), t((150, 150, 156)), 1.2)  # 铁盔


def cube(p, view):
    # 斜上方看的立方体：正面 + 顶面 + 侧面（north 时侧面在左，看起来是背面）
    side = 1 if view != "north" else -1
    fx0, fx1, fy0, fy1 = (18, 76, 30, 92) if side > 0 else (24, 82, 30, 92)
    dx, dy = 10 * side, -10
    front = poly([(fx0, fy0), (fx1, fy0), (fx1, fy1), (fx0, fy1)], 1)
    if side > 0:
        top = ("poly", [(fx0, fy0), (fx0 + dx, fy0 + dy), (fx1 + dx, fy0 + dy), (fx1, fy0)])
        sideface = ("poly", [(fx1, fy0), (fx1 + dx, fy0 + dy), (fx1 + dx, fy1 + dy), (fx1, fy1)])
    else:
        top = ("poly", [(fx0, fy0), (fx0 + dx, fy0 + dy), (fx1 + dx, fy0 + dy), (fx1, fy0)])
        sideface = ("poly", [(fx0, fy0), (fx0 + dx, fy0 + dy), (fx0 + dx, fy1 + dy), (fx0, fy1)])
    whole = [front, top, sideface]
    # 整体轮廓（一次画，免得面与面之间出现黑线）
    p.glass(whole, GEL, 0, OW)
    p.glass(sideface, GEL_D, 170, 0)
    p.glass(top, GEL_L, 170, 0)
    p.glass(front, GEL, 150, 0)
    junk(p, view)
    # 面与面的棱：浅色细线
    for a, b in ([(fx0, fy0), (fx1, fy0)], [(fx1 if side > 0 else fx0, fy0), (fx1 if side > 0 else fx0, fy1)]):
        p.line([a, b], .9, GEL_L)
    # 高光、气泡
    p.glass(tent([(fx0 + 5, fy0 + 8), (fx0 + 5, fy0 + 30)], 2.6, 1.4), (236, 250, 236), 200, 0)
    for x, y, r in ((fx0 + 12, fy0 + 40, 1.6), (fx0 + 40, fy0 + 14, 1.2), (fx0 + 30, fy0 + 52, 1.0), (fx1 - 8, fy0 + 30, 1.4)):
        p.glass(ell((x - r, y - r, x + r, y + r)), (230, 248, 230), 210, 0)


def gel_east(p):
    cube(p, "east")


def gel_south(p):
    cube(p, "south")


def gel_north(p):
    cube(p, "north")


# ====================== 石化蜥 ======================
BASI = dict(m=(112, 126, 86), d=(80, 94, 62), b=(190, 182, 132), h=(212, 198, 150), e=(236, 226, 96))


def basi_leg(p, c, x, y, far=False, s=1):
    col = c["d"] if far else c["m"]
    p.add(tent([(x, y), (x + s * 3, y + 7), (x + s * 1, y + 12)], 4.6, 3.4), col, 1.8)
    for k in (-1.4, 0, 1.4):
        p.add(poly([(x + s + k - .8, y + 11.5), (x + s + k + s * 1.4, y + 14), (x + s + k + .8, y + 11.5)]), c["h"], .8, shade=False)


def basi_east(p):
    c = BASI
    for x in (26, 38, 50, 62):
        basi_leg(p, c, x + 3, 66, far=True)
    p.add(tent([(26, 66), (14, 68), (4, 64)], 13, 2), c["m"], OW)  # 尾巴
    body = poly([(22, 60), (34, 52), (60, 51), (76, 56), (78, 70), (60, 76), (34, 77), (22, 72)])
    p.add(body, c["m"], OW)
    p.add(ell((26, 68, 76, 84)), c["b"], 0, clip=body)
    for x in range(26, 76, 6):  # 背上的棘
        p.add(poly([(x - 2, 55), (x + 1, 47), (x + 2.4, 55)], 1), c["h"], 1.0, shade=False)
    for x, y in ((32, 62), (44, 60), (56, 61), (66, 63), (40, 68), (60, 69)):
        p.add(ell((x - 2.2, y - 1.4, x + 2.2, y + 1.4)), c["d"], 0, shade=False, clip=body)
    for x in (24, 36, 50, 64):
        basi_leg(p, c, x, 70)
    # 头
    p.add(tent([(72, 62), (82, 58)], 12, 10), c["m"], OW)
    head = poly([(78, 52), (90, 51), (98, 57), (96, 64), (84, 66), (78, 62)])
    p.add(head, c["m"], OW)
    p.add(poly([(84, 62), (97, 60), (96, 64), (86, 66)]), c["b"], 1.2)
    for x in (88, 91, 94):
        p.add(poly([(x, 61), (x + .9, 63.5), (x + 1.8, 61)], 1), TOOTH, 0, shade=False)
    p.add(ell((84, 53.5, 89.5, 58)), c["e"], 1.2, shade=False)  # 发光的眼
    p.add(ell((86.3, 54, 87.3, 57.6)), INK, 0)
    p.add(poly([(80, 52), (86, 47), (85, 52)], 1), c["h"], 1.0, shade=False)


def basi_south(p):
    c = BASI
    for s in (-1, 1):
        for k, y in enumerate((58, 64, 70, 76)):
            x = 50 + s * 20
            col = c["d"] if k % 2 else c["m"]
            p.add(tent([(x, y), (x + s * 10, y + 2), (x + s * 13, y + 10)], 4.6, 3.4), col, 1.8)
    body = ell((28, 48, 72, 88))
    p.add(body, c["m"], OW)
    p.add(ell((38, 62, 62, 96)), c["b"], 0, clip=body)
    head = poly([(36, 50), (64, 50), (66, 66), (58, 76), (42, 76), (34, 66)])
    p.add(head, c["m"], OW)
    p.add(poly([(42, 70), (58, 70), (56, 77), (44, 77)]), c["b"], 1.2)
    for x in (45, 49, 53):
        p.add(poly([(x, 71), (x + 1, 74), (x + 2, 71)], 1), TOOTH, 0, shade=False)
    for x in (42, 58):
        p.add(ell((x - 4, 55, x + 4, 61.5)), c["e"], 1.3, shade=False)
        p.add(ell((x - .7, 55.4, x + .7, 61)), INK, 0)
    for x in (44, 50, 56):
        p.add(poly([(x - 2, 51), (x, 44), (x + 2, 51)], 1), c["h"], 1.0, shade=False)


def basi_north(p):
    c = BASI
    p.add(ell((38, 30, 62, 48)), c["m"], OW)
    p.add(poly([(48, 32), (50, 26), (52, 32)], 1), c["h"], 1.0, shade=False)
    for s in (-1, 1):
        for k, y in enumerate((50, 58, 66, 74)):
            x = 50 + s * 18
            p.add(tent([(x, y), (x + s * 10, y + 2), (x + s * 13, y + 10)], 4.6, 3.4), c["m"], 1.8)
    body = ell((28, 42, 72, 86))
    p.add(body, c["m"], OW)
    for x, y in ((38, 54), (60, 56), (42, 70), (58, 72), (50, 62)):
        p.add(ell((x - 2.6, y - 1.8, x + 2.6, y + 1.8)), c["d"], 0, shade=False, clip=body)
    for y in range(46, 84, 6):
        p.add(poly([(47.8, y + 4), (50, y - 2), (52.2, y + 4)], 1), c["h"], 1.0, shade=False)
    p.add(tent([(50, 84), (52, 92), (48, 99)], 10, 2), c["m"], OW)


# ====================== 蝎尾狮 ======================
MANT = dict(m=(192, 142, 86), d=(152, 106, 62), b=(222, 196, 150), mane=(112, 70, 46), mane_d=(84, 52, 36),
            w=(124, 70, 64), w2=(160, 100, 84), face=(214, 172, 132), h=(226, 214, 186))


def mant_wing(p, c, pts, ribs):
    p.add(poly(pts), c["w"], OW)
    for a, b in ribs:
        thin(p, tent([a, b], 1.2, .8), mix(c["w"], INK, .45), 0)


def mant_tail(p, c, pts):
    p.add(tent(pts, 5, 3), c["m"], OW)
    tx, ty = pts[-1]
    for k in range(7):
        a = math.radians(k * 51 - 90)
        thin(p, ("poly", [(tx + 3 * math.cos(a + .3), ty + 3 * math.sin(a + .3)), (tx + 11 * math.cos(a), ty + 11 * math.sin(a)), (tx + 3 * math.cos(a - .3), ty + 3 * math.sin(a - .3))]), mix(c["h"], c["d"], .25), 1.1)
    p.add(ell((tx - 5.5, ty - 5.5, tx + 5.5, ty + 5.5)), c["d"], OW)


def mant_face(p, c, cx, cy, side=False):
    # 狮鬃里的人脸
    p.add(ell((cx - 13, cy - 13, cx + 13, cy + 13)), c["mane"], OW)
    for k in range(10):
        a = math.radians(k * 36 + 10)
        p.add(poly([(cx + 9 * math.cos(a - .3), cy + 9 * math.sin(a - .3)), (cx + 16 * math.cos(a), cy + 16 * math.sin(a)), (cx + 9 * math.cos(a + .3), cy + 9 * math.sin(a + .3))]), c["mane_d"], 1.4, shade=False)
    if side:
        p.add(poly([(cx - 2, cy - 8), (cx + 8, cy - 7), (cx + 11, cy + 1), (cx + 8, cy + 8), (cx - 1, cy + 8)]), c["face"], 1.6)
        p.add(ell((cx + 4, cy - 3, cx + 7, cy)), INK, 0)
        p.add(tent([(cx + 6, cy + 4), (cx + 10, cy + 4)], 1.2, 1.2), (110, 50, 50), 0, shade=False)
        return
    p.add(ell((cx - 8, cy - 9, cx + 8, cy + 9)), c["face"], 1.6)
    for x in (-3.4, 3.4):
        p.add(ell((cx + x - 1.6, cy - 3, cx + x + 1.6, cy)), INK, 0)
        p.add(tent([(cx + x - 2.4, cy - 5), (cx + x + 2.4, cy - 4.5)], 1.1, 1.1), c["mane_d"], 0, shade=False)
    p.add(tent([(cx - 3, cy + 4), (cx, cy + 5), (cx + 3, cy + 4)], 1.3, 1.3), (110, 50, 50), 0, shade=False)
    for x in (-1.6, 1.6):
        p.add(poly([(cx + x - .7, cy + 4.4), (cx + x, cy + 6.4), (cx + x + .7, cy + 4.4)]), TOOTH, 0, shade=False)


def mant_east(p):
    c = MANT
    mant_wing(p, c, [(46, 46), (40, 30), (30, 14), (22, 10), (24, 22), (18, 30), (26, 38), (22, 46), (34, 50)],
              [((44, 44), (22, 10)), ((42, 46), (18, 30)), ((40, 48), (22, 46))])
    for x in (30, 66):
        p.add(tent([(x + 3, 64), (x + 4, 80), (x + 6, 88)], 6, 5), c["d"], OW)
    mant_tail(p, c, [(22, 58), (12, 52), (8, 38), (14, 26)])
    body = poly([(20, 52), (40, 48), (66, 48), (76, 54), (74, 70), (52, 72), (24, 70), (18, 62)])
    p.add(body, c["m"], OW)
    p.add(ell((28, 64, 70, 80)), c["b"], 0, clip=body)
    for x in (24, 60):
        p.add(tent([(x + 2, 64), (x, 80), (x + 3, 89)], 7, 5.5), c["m"], OW)
        p.add(ell((x - 1, 86, x + 7, 91)), c["m"], 1.6)
    mant_wing(p, c, [(56, 48), (52, 32), (46, 16), (40, 8), (40, 22), (34, 30), (40, 38), (36, 46), (48, 52)],
              [((54, 46), (40, 8)), ((52, 48), (34, 30)), ((50, 50), (36, 46))])
    mant_face(p, c, 80, 50, side=True)


def mant_south(p):
    c = MANT
    for s in (-1, 1):
        mant_wing(p, c, [(50 + s * 10, 52), (50 + s * 22, 36), (50 + s * 40, 22), (50 + s * 48, 24), (50 + s * 44, 38), (50 + s * 46, 50), (50 + s * 34, 52), (50 + s * 30, 62)],
                  [((50 + s * 12, 52), (50 + s * 48, 24)), ((50 + s * 14, 54), (50 + s * 46, 50)), ((50 + s * 16, 58), (50 + s * 30, 62))])
    mant_tail(p, c, [(62, 66), (72, 56), (76, 40), (72, 26)])
    body = ell((30, 50, 70, 88))
    p.add(body, c["m"], OW)
    p.add(ell((40, 66, 60, 94)), c["b"], 0, clip=body)
    for x in (40, 60):
        p.add(tent([(x, 74), (x, 88)], 8, 7), c["m"], OW)
        p.add(ell((x - 5, 86, x + 5, 92)), c["m"], 1.6)
    mant_face(p, c, 50, 52)


def mant_north(p):
    c = MANT
    mant_face(p, c, 50, 40)
    p.add(ell((38, 28, 62, 50)), c["mane"], OW)
    for k in range(8):
        a = math.radians(k * 45 + 20)
        p.add(poly([(50 + 8 * math.cos(a - .3), 39 + 8 * math.sin(a - .3)), (50 + 14 * math.cos(a), 39 + 14 * math.sin(a)), (50 + 8 * math.cos(a + .3), 39 + 8 * math.sin(a + .3))]), c["mane_d"], 1.4, shade=False)
    body = ell((30, 44, 70, 90))
    p.add(body, c["m"], OW)
    p.add(tent([(50, 48), (50, 86)], 3, 3), c["d"], 0, shade=False, clip=body)
    for s in (-1, 1):
        mant_wing(p, c, [(50 + s * 6, 52), (50 + s * 20, 38), (50 + s * 40, 24), (50 + s * 48, 26), (50 + s * 42, 40), (50 + s * 44, 52), (50 + s * 30, 56), (50 + s * 24, 66)],
                  [((50 + s * 8, 52), (50 + s * 48, 26)), ((50 + s * 10, 56), (50 + s * 44, 52)), ((50 + s * 12, 60), (50 + s * 24, 66))])
    mant_tail(p, c, [(52, 86), (60, 92), (70, 86), (72, 74)])


# ====================== 掘地兽 ======================
BUL = dict(m=(110, 122, 134), d=(80, 90, 104), b=(196, 186, 154), h=(208, 198, 168), crest=(150, 108, 76))


def bul_leg(p, c, x, y, far=False):
    col = c["d"] if far else c["m"]
    p.add(tent([(x, y), (x + 1, y + 10), (x - 1, y + 16)], 11, 9), col, OW)
    for k in (-3, 0, 3):
        p.add(poly([(x + k - 1.4, y + 17), (x + k + 1.6, y + 21), (x + k + 1.4, y + 17)]), c["h"], 1.0, shade=False)


def bul_east(p):
    c = BUL
    bul_leg(p, c, 30, 66, far=True)
    bul_leg(p, c, 64, 66, far=True)
    body = poly([(12, 62), (18, 44), (40, 34), (66, 34), (84, 42), (94, 56), (90, 70), (70, 78), (30, 78), (14, 72)])
    p.add(poly([(40, 36), (46, 16), (56, 12), (60, 22), (58, 36)]), c["crest"], OW)  # 背上的大甲鳍
    p.add(poly([(24, 44), (26, 32), (34, 30), (36, 40)]), c["crest"], OW)
    p.add(body, c["m"], OW)
    p.add(ell((22, 64, 88, 92)), c["b"], 0, clip=body)
    for x in (28, 42, 56, 70):  # 甲片接缝
        p.add(tent([(x, 38), (x + 3, 52), (x + 1, 68)], 1.8, 1.2), c["d"], 0, shade=False, clip=body)
    # 张开的大嘴
    p.add(poly([(74, 60), (95, 58), (98, 64), (78, 70)]), (120, 56, 60), OW)
    for x in (80, 84, 88, 92):
        p.add(poly([(x, 59), (x + 1.2, 62.6), (x + 2.4, 59)], 1), TOOTH, 0, shade=False)
        p.add(poly([(x - 1, 67.4), (x + .2, 64), (x + 1.4, 67)], 1), TOOTH, 0, shade=False)
    p.add(poly([(80, 44), (86, 44), (85, 48), (81, 48)]), (236, 200, 90), 1.2, shade=False)
    p.add(ell((82.6, 44.6, 84, 47.6)), INK, 0)
    bul_leg(p, c, 24, 68)
    bul_leg(p, c, 58, 68)


def bul_south(p):
    c = BUL
    for x in (22, 78):
        bul_leg(p, c, x, 66)
    p.add(poly([(42, 34), (46, 10), (54, 10), (58, 34)]), c["crest"], OW)
    body = ell((16, 30, 84, 88))
    p.add(body, c["m"], OW)
    p.add(ell((30, 70, 70, 100)), c["b"], 0, clip=body)
    for x in (32, 50, 68):
        p.add(tent([(x, 32), (x, 50)], 1.8, 1.2), c["d"], 0, shade=False, clip=body)
    p.add(poly([(32, 60), (68, 60), (62, 80), (38, 80)]), (120, 56, 60), OW)
    for x in range(35, 66, 5):
        p.add(poly([(x, 60.5), (x + 1.4, 65), (x + 2.8, 60.5)], 1), TOOTH, 0, shade=False)
    for x in range(38, 62, 5):
        p.add(poly([(x, 79.5), (x + 1.4, 75), (x + 2.8, 79.5)], 1), TOOTH, 0, shade=False)
    for x in (36, 64):
        p.add(poly([(x - 4, 46), (x + 4, 46), (x + 3, 51), (x - 3, 51)]), (236, 200, 90), 1.2, shade=False)
        p.add(ell((x - .8, 46.4, x + .8, 50.6)), INK, 0)
    for x in (32, 68):
        bul_leg(p, c, x, 72)


def bul_north(p):
    c = BUL
    for x in (22, 78, 32, 68):
        bul_leg(p, c, x, 66)
    body = ell((16, 28, 84, 90))
    p.add(body, c["m"], OW)
    for y in (40, 54, 68):
        p.add(tent([(20, y + 4), (50, y - 2), (80, y + 4)], 1.8, 1.8), c["d"], 0, shade=False, clip=body)
    p.add(poly([(46, 26), (50, 8), (54, 26), (54, 54), (46, 54)]), c["crest"], OW)
    p.add(poly([(47, 62), (50, 56), (53, 62), (53, 72), (47, 72)]), c["crest"], OW)


# ====================== 腐尸爬虫 ======================
CRAW = dict(m=(156, 164, 108), d=(116, 124, 80), b=(204, 200, 150), t=(206, 150, 150), t_d=(170, 116, 120))


def craw_tentacles(p, c, cx, cy, spread=1.0, n=6):
    for k in range(n):
        f = (k - (n - 1) / 2) / ((n - 1) / 2)
        x1 = cx + f * 8 * spread
        p.add(tent([(cx + f * 2, cy), (x1, cy + 8), (x1 + f * 3 + 1.5, cy + 15), (x1 + f * 2, cy + 20)], 2.2, .9), c["t"] if k % 2 else c["t_d"], 1.1)


def craw_east(p):
    c = CRAW
    segs = [(10, 72, 7), (20, 70, 8), (31, 68, 9), (43, 66, 9.5), (55, 62, 10), (66, 55, 10), (75, 46, 9)]
    for x, y, r in segs:  # 远侧的腿
        p.add(tent([(x, y), (x - 3, y + 6), (x - 1, y + 13)], 2.2, 1.2), c["d"], 1.2)
    for i, (x, y, r) in enumerate(segs):
        seg = ell((x - r, y - r * .9, x + r, y + r * .9))
        p.add(seg, c["m"] if i % 2 else mix(c["m"], c["d"], .3), OW)
        p.add(ell((x - r * .8, y + r * .1, x + r * .8, y + r * 1.2)), c["b"], 0, clip=seg)
    for x, y, r in segs[:-1]:
        p.add(tent([(x + 1, y + 4), (x + 4, y + 9), (x + 2, y + 15)], 2.4, 1.2), c["m"], 1.3)
    # 头
    head = poly([(74, 34), (86, 32), (92, 40), (88, 48), (76, 48)])
    p.add(head, c["m"], OW)
    p.add(ell((82, 35, 86, 39)), (40, 30, 30), 0)
    p.add(ell((86, 37, 89, 40)), (40, 30, 30), 0)
    craw_tentacles(p, c, 86, 46, .6, 5)


def craw_south(p):
    c = CRAW
    for i, (y, r) in enumerate([(30, 10), (40, 11), (50, 12)]):
        for s in (-1, 1):
            p.add(tent([(50 + s * r * .8, y + 2), (50 + s * (r + 8), y - 1), (50 + s * (r + 11), y + 8)], 2.4, 1.2), c["d"], 1.2)
        p.add(ell((50 - r, y - r * .8, 50 + r, y + r * .8)), mix(c["m"], c["d"], .3) if i % 2 else c["m"], OW)
    for s in (-1, 1):
        for y in (58, 66):
            p.add(tent([(50 + s * 10, y), (50 + s * 22, y - 2), (50 + s * 26, y + 10)], 2.6, 1.3), c["m"], 1.3)
    p.add(ell((36, 50, 64, 74)), c["m"], OW)
    head = poly([(38, 56), (62, 56), (64, 68), (50, 76), (36, 68)])
    p.add(head, c["m"], OW)
    for x in (43, 57):
        p.add(ell((x - 2.4, 60, x + 2.4, 64)), (40, 30, 30), 0)
    craw_tentacles(p, c, 50, 72, 1.3, 8)


def craw_north(p):
    c = CRAW
    craw_tentacles(p, c, 50, 22, 1.0, 6)
    p.add(poly([(40, 18), (60, 18), (62, 28), (50, 32), (38, 28)]), c["m"], OW)
    segs = [(30, 10), (40, 11), (51, 12), (62, 12), (72, 11), (81, 9), (89, 7)]
    for y, r in segs:
        for s in (-1, 1):
            p.add(tent([(50 + s * r * .8, y), (50 + s * (r + 7), y - 2), (50 + s * (r + 10), y + 7)], 2.4, 1.2), c["m"], 1.2)
    for i, (y, r) in enumerate(segs[::-1]):
        p.add(ell((50 - r, y - r * .8, 50 + r, y + r * .8)), mix(c["m"], c["d"], .3) if i % 2 else c["m"], OW)
        p.add(tent([(50 - r * .5, y - r * .3), (50, y - r * .55), (50 + r * .5, y - r * .3)], 1.4, 1.4), c["d"], 0, shade=False)
    for y, r in segs[::-1]:
        pass


# ====================== 输出 ======================
MONSTERS = {
    "RustMonster": (rust_south, rust_east, rust_north, SPic),
    "GelatinousCube": (gel_south, gel_east, gel_north, GelPic),
    "Basilisk": (basi_south, basi_east, basi_north, SPic),
    "Manticore": (mant_south, mant_east, mant_north, SPic),
    "Bulette": (bul_south, bul_east, bul_north, SPic),
    "CarrionCrawler": (craw_south, craw_east, craw_north, SPic),
}
# 游戏里成年体的 drawSize（和 Defs/WildMonsters.xml 一致），用来画比例图
DRAW = {"RustMonster": 1.9, "GelatinousCube": 2.3, "Basilisk": 2.3, "Manticore": 2.7, "Bulette": 3.3, "CarrionCrawler": 2.4}


def dessicate(img, name):
    """干尸：褪色发灰、偏骨色；凝胶怪只剩一滩干掉的胶。"""
    if name == "GelatinousCube":
        out = Image.new("RGBA", img.size, (0, 0, 0, 0))
        q = GelPic()
        q.glass(poly([(18, 78), (40, 72), (72, 74), (86, 82), (70, 90), (30, 90)]), (150, 160, 120), 160, OW)
        q.add(ell((32, 76, 44, 86)), (200, 194, 170), 1.4)
        q.add(tent([(52, 86), (68, 78)], 2.4, 1.4), (150, 150, 140), 1.2)
        return q.img.resize(img.size, Image.LANCZOS)
    a = img.getchannel("A")
    g = ImageEnhance.Color(img.convert("RGB")).enhance(.15)
    g = Image.blend(g, Image.new("RGB", img.size, (196, 186, 164)), .35)
    g = ImageEnhance.Brightness(g).enhance(.92)
    out = g.convert("RGBA")
    out.putalpha(a)
    return out


def draw_all():
    paths = []
    for name, (south, east, north, cls) in MONSTERS.items():
        for rot, fn in (("south", south), ("east", east), ("north", north)):
            p = cls()
            fn(p)
            base.TEX = TEX
            path = p.save(f"{REL}/{name}/{name}_{rot}", 256)
            img = Image.open(path).convert("RGBA")
            dessicate(img, name).save(os.path.join(TEX, *REL.split("/"), name, f"Dessicated_{name}_{rot}.png"))
            paths.append(path)
    # 尾刺
    p = SPic()
    p.add(poly([(50, 8), (56, 60), (53, 92), (47, 92), (44, 60)]), (226, 214, 186), 4)
    p.add(tent([(50, 20), (50, 80)], 2, 1), (180, 160, 130), 0, shade=False)
    base.TEX = TEX
    paths.append(p.save("Things/Projectile/LP_ManticoreSpike", 64))
    return paths


def preview():
    cell = 150
    bg = (236, 230, 216, 255)
    names = list(MONSTERS)
    cols = ["south", "east", "west", "north", "dess"]
    W = 20 + len(cols) * (cell + 10) + 10
    scale_h = 260
    out = Image.new("RGBA", (W, 20 + len(names) * (cell + 10) + scale_h), bg)
    d = ImageDraw.Draw(out)
    y = 20
    folder = os.path.join(TEX, *REL.split("/"))
    for name in names:
        x = 20
        for col in cols:
            rot = "east" if col in ("west", "dess") else col
            fn = f"Dessicated_{name}_{rot}.png" if col == "dess" else f"{name}_{rot}.png"
            img = Image.open(os.path.join(folder, name, fn)).convert("RGBA")
            if col == "west":
                img = img.transpose(Image.FLIP_LEFT_RIGHT)
            out.alpha_composite(img.resize((cell, cell), Image.LANCZOS), (x, y))
            x += cell + 10
        y += cell + 10
    # 比例：游戏里 1 格 = 40px，一个人约 1 格高
    unit = 40
    base_y = y + scale_h - 30
    hx = 36
    d.ellipse((hx - 8, base_y - 44, hx + 8, base_y - 30), fill=(214, 168, 132), outline=INK, width=2)
    d.rectangle((hx - 9, base_y - 30, hx + 9, base_y - 8), fill=(110, 120, 90), outline=INK, width=2)
    d.line((hx - 5, base_y - 8, hx - 5, base_y), fill=INK, width=3)
    d.line((hx + 5, base_y - 8, hx + 5, base_y), fill=INK, width=3)
    x = 60
    for name in names:
        img = Image.open(os.path.join(folder, name, f"{name}_east.png")).convert("RGBA")
        sz = int(DRAW[name] * unit)
        out.alpha_composite(img.resize((sz, sz), Image.LANCZOS), (x, base_y - int(sz * .92)))
        x += int(sz * .8) + 6
    path = os.path.join(OUT, "ArtSource", "Preview", "WildMonsters.png")
    os.makedirs(os.path.dirname(path), exist_ok=True)
    out.convert("RGB").save(path)
    return path


if __name__ == "__main__":
    draw_all()
    print(preview())
