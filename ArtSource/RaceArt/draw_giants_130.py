# 1.30.0 巨人通用层 + 霜巨人贴图：沿用 draw_race_art.py / draw_races_124.py 的画法（粗黑轮廓、平涂、单侧暗面、低饱和），
# 能力图标照 1.27.4 职业图标的卡牌画法（draw_class_art.py：黑描边 → 平涂 → 右下暗部）。
# 用法：python -I ArtSource/RaceArt/draw_giants_130.py <模组根目录>
# 穿戴贴图（锻工外衣、冬狼皮大氅，五体型三向 256px）以熔铁重甲的穿戴贴图为轮廓蒙版（同一套原版身体画框），重新上色、加细节。
import math, os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import draw_race_art as base
from draw_race_art import Pic, poly, ell, tent, taper, arc, mir, INK, OW, WOOD, WOOD_D, STEEL, STEEL_D, GOLD, GOLD_D, BONE
from PIL import Image, ImageDraw, ImageFilter, ImageChops

ROOT = sys.argv[1] if len(sys.argv) > 1 else os.getcwd()
base.TEX = os.path.join(ROOT, "Textures")
TEX = base.TEX
HEAD_OW = 1.8

# ---------------- 调色（冰蓝 + 白，低饱和） ----------------
SKIN = (164, 186, 204); SKIN_D = (130, 152, 172)
ICE = (150, 190, 214); ICE_D = (108, 148, 178); ICE_L = (214, 234, 246); FROST = (238, 246, 252)
HAIR = (222, 236, 246); HAIR_D = (176, 196, 212)
FUR = (176, 194, 208); FUR_D = (132, 150, 168); FUR_L = (222, 232, 240)
IRON = (112, 120, 128); IRON_D = (82, 88, 96); COLDIRON = (128, 140, 156); COLDIRON_D = (94, 104, 120)
FLAME = (232, 132, 56); FLAME_L = (252, 210, 120)
SNOW = (232, 240, 246); SNOW_D = (196, 210, 222)
LEATHER = (118, 92, 70); LEATHER_D = (90, 68, 52)
CLOTH = (226, 226, 226); CLOTH_D = (186, 186, 186); CLOTH_DD = (150, 150, 150)  # 锻工外衣受材料着色，画成近白灰
GOLDC = (214, 184, 112)


def flame(p, cx, by, w, h):
    pts = [(cx - w * .5, by), (cx - w * .55, by - h * .35), (cx - w * .3, by - h * .55), (cx - w * .22, by - h * .82), (cx - w * .05, by - h * .62),
           (cx + w * .05, by - h), (cx + w * .25, by - h * .6), (cx + w * .4, by - h * .72), (cx + w * .55, by - h * .35), (cx + w * .5, by)]
    p.add(poly(pts), FLAME)
    p.add(poly([(cx - w * .28, by), (cx - w * .3, by - h * .3), (cx - w * .08, by - h * .5), (cx + w * .06, by - h * .7), (cx + w * .22, by - h * .4), (cx + w * .28, by)]), FLAME_L, 0)


def snowflake(p, cx, cy, r, color=FROST, w=3.0):
    for k in range(3):
        a = math.radians(90 + k * 60); dx, dy = math.cos(a) * r, math.sin(a) * r
        p.add(("line", [(cx - dx, cy - dy), (cx + dx, cy + dy)], w), color, 1.6)
    for k in range(6):
        a = math.radians(90 + k * 60); bx, by = cx + math.cos(a) * r * .6, cy + math.sin(a) * r * .6
        for s in (-35, 35):
            b = a + math.radians(180 + s)
            p.add(("line", [(bx, by), (bx + math.cos(b) * r * .3, by + math.sin(b) * r * .3)], w * .7), color, 1.2)


def braid(p, pts, w, ow=OW, color=HAIR, dark=HAIR_D):
    """一条粗发辫：沿辫身交错排列向左、向右斜的辫节（三股辫的样子），末端寒铁环、一小撮发梢和一颗冰珠。"""
    from draw_race_art import ellpts
    sm = []
    for i in range(len(pts) - 1):
        (x0, y0), (x1, y1) = pts[i], pts[i + 1]
        n = max(2, int(math.hypot(x1 - x0, y1 - y0) / (w * .42)))
        for k in range(n):
            t = k / n; sm.append((x0 + (x1 - x0) * t, y0 + (y1 - y0) * t))
    sm.append(pts[-1])
    body = sm[:-1]
    # 外轮廓：所有辫节合在一起描一次粗边
    leaves = []
    for i, (x, y) in enumerate(body):
        ww = w * (1 - .25 * i / len(body))
        side = 1 if i % 2 else -1
        leaves.append(("poly", ellpts(x + side * ww * .16, y, ww * .36, ww * .27, ang=side * 38, steps=20)))
    p.add(leaves, color, ow)
    for i, (x, y) in enumerate(body):
        ww = w * (1 - .25 * i / len(body))
        side = 1 if i % 2 else -1
        p.add(("poly", ellpts(x + side * ww * .16, y, ww * .36, ww * .27, ang=side * 38, steps=20)), color, .7)
        if side > 0:
            p.add(("poly", ellpts(x + ww * .26, y + ww * .05, ww * .2, ww * .14, ang=38, steps=16)), dark, 0)
    ex, ey = sm[-1]
    p.add(tent([(ex, ey), (ex, ey + w * .7), (ex + w * .05, ey + w * 1.25)], w * .5, w * .1), color, ow)  # 发梢
    p.add(tent([(ex, ey - w * .12), (ex, ey + w * .22)], w * .58, w * .58), IRON, ow * .8)
    p.add(ell((ex + w * .2, ey + w * .05, ex + w * .5, ey + w * .35)), ICE_L, ow * .7)

# ================= 基因图标（256） =================
def core_icon():
    p = Pic()
    snowflake(p, 50, 44, 40, ICE_L, 4)
    # 头盔角
    for s in (lambda q: q, mir):
        p.add(tent(s([(30, 40), (20, 30), (16, 16), (20, 6)]), 8, 2), BONE)
    # 头与脸
    p.add(ell((28, 26, 72, 74)), SKIN)
    p.add(poly([(56, 28)] + arc(50, 50, 22, -70, 70, 12) + [(60, 72), (64, 50)]), SKIN_D, 0)
    p.add(poly([(28, 40), (50, 24), (72, 40), (72, 34), (50, 18), (28, 34)]), COLDIRON)  # 寒铁额箍
    p.add([ell((37, 44, 43, 50)), ell((57, 44, 63, 50))], ICE_L, 1.6)
    # 胡子与发辫
    p.add(poly([(30, 54), (38, 60), (50, 58), (62, 60), (70, 54), (68, 74), (58, 90), (50, 96), (42, 90), (32, 74)]), HAIR)
    p.add(poly([(52, 60), (62, 60), (68, 70), (58, 88), (52, 92)]), HAIR_D, 0)
    for s in (lambda q: q, mir):
        braid(p, s([(27, 50), (24, 66), (23, 82)]), 7)
    p.line([(44, 66), (50, 64), (56, 66)], 1.8, INK)
    return p


def snowwalker_icon():
    p = Pic()
    p.add(poly([(4, 70), (20, 60), (40, 64), (60, 56), (80, 62), (96, 56), (96, 96), (4, 96)]), SNOW)
    p.add(poly([(4, 84), (30, 78), (60, 82), (96, 76), (96, 96), (4, 96)]), SNOW_D, 0)
    # 一只大靴子踏在雪上
    p.add(poly([(34, 12), (56, 12), (58, 50), (78, 56), (84, 66), (82, 74), (30, 74), (28, 50)]), LEATHER)
    p.add(poly([(48, 14), (56, 14), (58, 52), (80, 58), (84, 66), (82, 74), (60, 74), (52, 54)]), LEATHER_D, 0)
    p.add(poly([(30, 70), (84, 70), (84, 76), (30, 76)]), IRON)
    p.add(poly([(30, 22), (58, 22), (58, 28), (30, 28)]), FUR)  # 毛皮靴口
    for x, y in ((16, 30), (84, 26), (72, 10)):
        snowflake(p, x, y, 7, ICE_L, 2.2)
    return p


def frailty_icon():
    p = Pic()
    flame(p, 50, 94, 70, 52)
    # 正在融化的冰块
    p.add(poly([(26, 20), (64, 12), (78, 40), (70, 62), (34, 66), (22, 44)]), ICE)
    p.add(poly([(64, 12), (78, 40), (70, 62), (52, 64), (58, 36)]), ICE_D, 0)
    p.add(poly([(30, 24), (50, 20), (40, 36)]), ICE_L, 0)
    for x, y, h in ((36, 66, 10), (52, 64, 16), (66, 62, 8)):
        p.add(poly([(x - 3, y), (x + 3, y), (x + 2, y + h), (x, y + h + 3), (x - 2, y + h)]), ICE_L, 1.8)
    return p


def wolflord_icon():
    p = Pic()
    # 冬狼头（正面）
    for s in (lambda q: q, mir):
        p.add(poly(s([(22, 40), (18, 8), (40, 26)])), FUR)
        p.add(poly(s([(23, 34), (22, 16), (34, 27)])), FUR_D, 0)
    p.add(poly([(20, 36), (50, 22), (80, 36), (86, 58), (70, 76), (58, 92), (42, 92), (30, 76), (14, 58)]), FUR)
    p.add(poly([(50, 22), (80, 36), (86, 58), (70, 76), (58, 92), (52, 92), (56, 60)]), FUR_D, 0)
    p.add(poly([(36, 64), (50, 58), (64, 64), (60, 84), (50, 92), (40, 84)]), FUR_L, 0)
    for s in (lambda q: q, mir):
        p.add(poly(s([(30, 50), (42, 46), (42, 54), (32, 56)])), ICE_L, 1.6)
    p.add([ell((36, 49, 40, 53)), ell((60, 49, 64, 53))], INK, 0)
    p.add(poly([(44, 80), (56, 80), (50, 88)]), INK, 1.2)
    # 主人的寒铁项圈与符印
    p.add(("line", arc(50, 70, 34, 30, 150, 20), 5), COLDIRON)
    p.add(poly([(50, 98), (56, 104), (50, 110), (44, 104)]), ICE_L, 1.6)
    return p


def lootheart_icon():
    p = Pic()
    # 一堆抢来的兵甲：圆盾、头盔、斧子、几枚银币，覆着霜
    p.add(ell((8, 46, 56, 94)), WOOD)
    p.add(ell((14, 52, 50, 88)), WOOD_D, 0)
    p.add(ell((26, 64, 38, 76)), IRON, 1.6)
    p.add(tent([(64, 14), (36, 70)], 6, 6), WOOD)
    p.add(poly([(56, 8), (82, 2), (90, 22), (72, 34), (60, 26)]), COLDIRON)
    p.add(poly([(60, 10), (82, 4), (86, 16), (66, 20)]), ICE_L, 0)
    p.add(poly([(52, 72), (54, 56), (68, 48), (84, 52), (92, 66), (92, 82), (52, 82)]), STEEL)
    p.add(poly([(70, 50), (84, 54), (92, 66), (92, 82), (74, 82)]), STEEL_D, 0)
    p.add(poly([(70, 60), (74, 60), (74, 80), (70, 80)]), INK, 0)
    for x, y in ((40, 92), (54, 92), (66, 90), (80, 92)):
        p.add(ell((x - 6, y - 4, x + 6, y + 4)), (196, 200, 208))
    p.add(poly([(8, 68), (14, 60), (22, 64), (20, 56), (30, 54)]), FROST, 0)
    snowflake(p, 86, 32, 8, ICE_L, 2.2)
    return p


# ================= 结霜发辫（128，与原版头部同一画框，照牛头人牛角的锚点） =================
# 原版头部在 100 单位画布约 x 32-68、y 30-70；发辫从太阳穴垂到下巴以下。
def braids_south():
    p = Pic()
    for s in (lambda q: q, mir):
        braid(p, s([(33, 46), (31, 60), (31, 74), (32, 80)]), 10, HEAD_OW)
    return p


def braids_north():
    p = Pic()
    for s in (lambda q: q, mir):
        braid(p, s([(41, 52), (40, 64), (41, 76), (42, 82)]), 10, HEAD_OW)
    return p


def braids_east():
    p = Pic()
    braid(p, [(42, 48), (39, 60), (38, 72), (39, 80)], 10, HEAD_OW)
    return p


# ================= 物品（128） =================
def axe_item():
    p = Pic()
    p.add(tent([(6, 60), (88, 44)], 6.5, 6.5), WOOD)
    p.add(tent([(6, 61.5), (88, 45.5)], 2.6, 2.6), WOOD_D, 0)
    for x in (18, 26):
        p.add(tent([(x, 58.5), (x + 1, 62.5)], 7.4, 7.4), LEATHER, 1.6)  # 缠柄
    # 双刃斧头（右端）
    head = [(70, 46), (66, 22), (78, 10), (82, 30), (92, 30), (96, 10), (100, 26), (100, 62), (96, 80), (92, 62), (82, 62), (78, 82), (66, 72), (70, 52)]
    p.add(poly([(x - 4, y + 2) for x, y in head]), COLDIRON)
    p.add(poly([(80, 44), (94, 44), (94, 54), (80, 54)]), COLDIRON_D, 0)
    p.add(poly([(62, 24), (74, 12), (76, 30), (66, 40)]), ICE_L, 0)
    p.add(poly([(62, 70), (74, 80), (76, 62), (66, 56)]), ICE_L, 0)
    p.add(poly([(92, 12), (96, 28), (94, 64), (92, 78), (90, 62), (90, 30)]), FROST, 0)
    snowflake(p, 30, 32, 6, ICE_L, 1.8)
    return p


def cloak_item():
    p = Pic()
    body = [(30, 18), (70, 18), (86, 30), (90, 86), (70, 94), (50, 90), (30, 94), (10, 86), (14, 30)]
    p.add(poly(body), FUR)
    p.add(poly([(50, 22), (70, 18), (86, 30), (90, 86), (70, 94), (52, 90)]), FUR_D, 0)
    p.line([(50, 26), (50, 88)], 1.6, INK)
    for x in (20, 32, 66, 78):
        p.line([(x, 40), (x + 2, 52)], 1.2, FUR_D)
        p.line([(x + 4, 62), (x + 5, 74)], 1.2, FUR_D)
    # 毛领
    ruff = [(22, 24)] + [(22 + i * 5.6, 14 + (5 if i % 2 else 0)) for i in range(11)] + [(80, 24), (74, 34), (50, 30), (26, 34)]
    p.add(poly(ruff), FUR_L)
    p.add(ell((44, 28, 56, 38)), COLDIRON, 1.8)  # 寒铁扣
    p.add(ell((47, 31, 53, 35)), ICE_L, 0)
    return p


def horn_item():
    p = Pic()
    pts = [(14, 70), (30, 72), (50, 66), (68, 52), (80, 34), (84, 16)]
    p.add(tent(pts, 26, 8), BONE)
    p.add(tent(pts[1:], 11, 3), (196, 186, 156), 0)
    p.add(ell((4, 56, 22, 86)), (200, 190, 160))
    p.add(ell((8, 62, 18, 80)), INK, 0)
    for x, y in ((30, 72), (54, 64)):
        p.add(tent([(x - 1, y - 12), (x + 2, y + 12)], 5.5, 5.5), COLDIRON, 1.8)
        p.add(tent([(x - .5, y - 10), (x + 1, y + 2)], 1.6, 1.6), ICE_L, 0)
    p.add(("line", [(30, 82), (44, 92), (60, 88), (54, 76)], 2.8), LEATHER)
    snowflake(p, 80, 72, 9, ICE_L, 2.2)
    return p


def pelt(p, cx, cy, sc, ang=0):
    c, s = math.cos(math.radians(ang)), math.sin(math.radians(ang))
    def T(pts):
        return [(cx + (x * c - y * s) * sc, cy + (x * s + y * c) * sc) for x, y in pts]
    shape = [(-30, -8), (-22, -18), (-6, -14), (6, -20), (22, -16), (32, -6), (28, 6), (34, 16), (18, 20), (4, 14), (-10, 20), (-26, 16), (-34, 6)]
    p.add(poly(T(shape)), FUR)
    p.add(poly(T([(6, -20), (22, -16), (32, -6), (28, 6), (34, 16), (18, 20), (8, 4)])), FUR_D, 0)
    p.add(poly(T([(-20, -10), (-4, -8), (-12, 0)])), FUR_L, 0)


def leather(n):
    p = Pic()
    spots = [(50, 64, 1.1, -6), (46, 50, 1.0, 8), (54, 38, .95, -4)]
    for cx, cy, sc, a in spots[:n]:
        pelt(p, cx, cy, sc, a)
    return p


# ================= 能力图标（128，职业图标的卡牌画法） =================
SS = 4; S = 128 * SS; C_OW = 6 * SS


def _m():
    return Image.new("L", (S, S), 0)


def _poly(pts):
    m = _m(); ImageDraw.Draw(m).polygon([(x * SS, y * SS) for x, y in pts], fill=255); return m


def _line(pts, w):
    m = _m(); d = ImageDraw.Draw(m); d.line([(x * SS, y * SS) for x, y in pts], fill=255, width=int(w * SS), joint="curve")
    for x, y in pts:
        d.ellipse([(x - w / 2) * SS, (y - w / 2) * SS, (x + w / 2) * SS, (y + w / 2) * SS], fill=255)
    return m


def _arc(cx, cy, r, a0, a1, w):
    m = _m(); ImageDraw.Draw(m).arc([(cx - r) * SS, (cy - r) * SS, (cx + r) * SS, (cy + r) * SS], a0, a1, fill=255, width=int(w * SS)); return m


def _taper(pts, w0, w1):
    return _poly(taper(pts, w0, w1))


def _layer(im, m, fill, shade=True):
    o = m.filter(ImageFilter.MaxFilter(C_OW + 1))
    im.paste(Image.new("RGBA", (S, S), INK + (255,)), (0, 0), o)
    im.paste(Image.new("RGBA", (S, S), fill + (255,)), (0, 0), m)
    if shade:
        d = 5 * SS; band = ImageChops.subtract(m, ImageChops.offset(m, -d, -d))
        im.paste(Image.new("RGBA", (S, S), tuple(int(v * .72) for v in fill) + (255,)), (0, 0), band)


def winter_horn_ability():
    im = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    # 号角（左下吹口 → 右上喇叭口）、寒铁箍、声波、雪花
    pts = [(16, 104), (30, 98), (48, 86), (62, 68), (70, 48)]
    _layer(im, _taper(pts, 30, 12), (220, 212, 184))
    m = _m(); ImageDraw.Draw(m).ellipse([56 * SS, 26 * SS, 90 * SS, 50 * SS], fill=255); _layer(im, m, (196, 186, 156))
    m = _m(); ImageDraw.Draw(m).ellipse([62 * SS, 30 * SS, 84 * SS, 46 * SS], fill=255); im.paste(Image.new("RGBA", (S, S), INK + (255,)), (0, 0), m)
    for cx, cy in ((34, 96), (52, 82)):
        _layer(im, _line([(cx - 8, cy - 10), (cx + 8, cy + 8)], 7), COLDIRON, False)
    for i, r in enumerate((22, 34, 46)):
        _layer(im, _arc(74, 36, r, 250, 350, 7), ICE_L if i % 2 == 0 else ICE, False)
    for k in range(3):
        a = math.radians(90 + k * 60); dx, dy = math.cos(a) * 12, math.sin(a) * 12
        _layer(im, _line([(104 - dx, 96 - dy), (104 + dx, 96 + dy)], 5), FROST, False)
    return im.resize((128, 128), Image.LANCZOS)


# ================= 穿戴贴图（256，五体型三向） =================
BODIES = ["Male", "Female", "Thin", "Fat", "Hulk"]
DIRS = ["south", "north", "east"]
K = 4  # 超采样


def silhouette(body, d):
    im = Image.open(os.path.join(TEX, "FireGiant", "Equipment", "Worn", "Armor_%s_%s.png" % (body, d))).convert("RGBA")
    a = im.split()[3].point(lambda v: 255 if v > 40 else 0)
    return a.resize((256 * K, 256 * K), Image.NEAREST).filter(ImageFilter.GaussianBlur(K)).point(lambda v: 255 if v > 127 else 0)


class Worn:
    def __init__(self):
        self.img = Image.new("RGBA", (256 * K, 256 * K), (0, 0, 0, 0))

    def fill(self, m, color):
        self.img.paste(Image.new("RGBA", self.img.size, color + (255,)), (0, 0), m)

    def mask(self, draw_fn):
        m = Image.new("L", self.img.size, 0); draw_fn(ImageDraw.Draw(m)); return m

    def outline(self, m, w=3.2):
        r = int(w * K)
        o = m.filter(ImageFilter.MaxFilter(2 * (r // 2) + 1))
        inner = m.filter(ImageFilter.MinFilter(2 * (r // 2) + 1))
        self.fill(ImageChops.subtract(o, inner), INK)

    def save(self, rel):
        path = os.path.join(TEX, rel.replace("/", os.sep) + ".png")
        os.makedirs(os.path.dirname(path), exist_ok=True)
        self.img.resize((256, 256), Image.LANCZOS).save(path)
        return path


def bbox(m):
    x0, y0, x1, y1 = m.getbbox()
    return x0, y0, x1, y1


def shade_right(m, box, frac=.34):
    x0, y0, x1, y1 = box
    cut = Image.new("L", m.size, 0); ImageDraw.Draw(cut).rectangle([x1 - (x1 - x0) * frac, y0, x1, y1], fill=255)
    return ImageChops.multiply(m, cut)


def tunic(body, d):
    w = Worn(); m = silhouette(body, d); x0, y0, x1, y1 = bbox(m); W, H = x1 - x0, y1 - y0; cx = (x0 + x1) / 2
    w.fill(m, CLOTH)
    w.fill(shade_right(m, (x0, y0, x1, y1), .3 if d != "east" else .4), CLOTH_D)
    belt_y = y0 + H * .62
    belt = w.mask(lambda dr: dr.rectangle([x0, belt_y, x1, belt_y + H * .07], fill=255))
    w.fill(ImageChops.multiply(m, belt), LEATHER)
    if d == "south":
        apron = w.mask(lambda dr: dr.polygon([(cx - W * .2, y0 + H * .3), (cx + W * .2, y0 + H * .3), (cx + W * .26, y1), (cx - W * .26, y1)], fill=255))
        w.fill(ImageChops.multiply(m, apron), CLOTH_DD)
        w.fill(ImageChops.multiply(m, w.mask(lambda dr: dr.rectangle([cx - W * .2, y0 + H * .3, cx + W * .2, y0 + H * .34], fill=255))), LEATHER_D)
        w.fill(ImageChops.multiply(m, w.mask(lambda dr: dr.ellipse([cx - W * .06, belt_y - H * .005, cx + W * .06, belt_y + H * .075], fill=255))), GOLDC)
    elif d == "north":
        w.fill(ImageChops.multiply(m, w.mask(lambda dr: dr.line([(cx, y0 + H * .12), (cx, y1)], fill=255, width=int(2.2 * K)))), CLOTH_DD)
    else:
        w.fill(ImageChops.multiply(m, w.mask(lambda dr: dr.polygon([(x1 - W * .32, y0 + H * .32), (x1, y0 + H * .3), (x1, y1), (x1 - W * .36, y1)], fill=255))), CLOTH_DD)
    # 领口与下摆的缝线
    w.fill(ImageChops.multiply(m, w.mask(lambda dr: dr.line([(x0 + W * .08, y1 - H * .06), (x1 - W * .08, y1 - H * .06)], fill=255, width=int(1.6 * K)))), CLOTH_DD)
    w.outline(m)
    return w


def cloak(body, d):
    w = Worn(); base_m = silhouette(body, d); x0, y0, x1, y1 = bbox(base_m); W, H = x1 - x0, y1 - y0; cx = (x0 + x1) / 2
    # 大氅比身体略宽、垂得更低；背面整片罩住
    grow = int(W * .05) | 1
    m = base_m.filter(ImageFilter.MaxFilter(grow))
    hem = y1 + H * .06
    if d == "north":
        drape = w.mask(lambda dr: dr.polygon([(x0 + W * .02, y0 + H * .2), (x1 - W * .02, y0 + H * .2), (x1 + W * .02, hem), (x0 - W * .02, hem)], fill=255))
    elif d == "south":
        drape = w.mask(lambda dr: dr.polygon([(x0, y0 + H * .22), (x1, y0 + H * .22), (x1 + W * .02, hem), (x0 - W * .02, hem)], fill=255))
    else:
        drape = w.mask(lambda dr: dr.polygon([(x0 + W * .05, y0 + H * .2), (x1 - W * .25, y0 + H * .2), (x1 - W * .3, hem), (x0 - W * .04, hem)], fill=255))
    m = ImageChops.lighter(m, drape)
    # 下摆参差的毛边
    teeth = w.mask(lambda dr: [dr.polygon([(x0 - W * .1 + i * W * .08, hem), (x0 - W * .06 + i * W * .08, hem - H * .04), (x0 - W * .02 + i * W * .08, hem)], fill=255) for i in range(20)])
    m = ImageChops.subtract(m, teeth)
    w.fill(m, FUR)
    w.fill(shade_right(m, (x0, y0, x1, y1), .3 if d != "east" else .45), FUR_D)
    # 毛皮纹理：几道短弧
    tex = w.mask(lambda dr: [dr.line([(x0 + W * (.12 + .19 * (i % 5)), y0 + H * (.42 + .14 * (i // 5))), (x0 + W * (.15 + .19 * (i % 5)), y0 + H * (.5 + .14 * (i // 5)))], fill=255, width=int(1.4 * K)) for i in range(15)])
    w.fill(ImageChops.multiply(m, tex), FUR_D)
    # 肩上的毛领
    ruff_h = H * .2
    ruff = w.mask(lambda dr: dr.polygon([(x0 - W * .03, y0 + ruff_h)] + [(x0 + i * (W * 1.06) / 12 - W * .03, y0 + ruff_h * (1.25 if i % 2 else .95)) for i in range(13)] + [(x1 + W * .03, y0 + ruff_h), (x1 - W * .05, y0), (x0 + W * .05, y0)], fill=255))
    ruff = ImageChops.multiply(ruff, base_m.filter(ImageFilter.MaxFilter(grow * 3 | 1)))
    w.fill(ruff, FUR_L)
    w.fill(shade_right(ruff, (x0, y0, x1, y1), .25), FUR)
    if d == "south":  # 前襟：一道开缝和两侧的毛边
        w.fill(ImageChops.multiply(m, w.mask(lambda dr: dr.line([(cx, y0 + ruff_h), (cx + W * .02, hem)], fill=255, width=int(2.4 * K)))), INK)
        w.fill(ImageChops.multiply(m, w.mask(lambda dr: dr.line([(cx + W * .05, y0 + ruff_h), (cx + W * .07, hem)], fill=255, width=int(3.2 * K)))), FUR_L)
        clasp = w.mask(lambda dr: dr.ellipse([cx - W * .07, y0 + ruff_h * .9, cx + W * .07, y0 + ruff_h * .9 + W * .1], fill=255))
        w.fill(clasp, COLDIRON); w.outline(clasp, 2)
    w.outline(ImageChops.lighter(m, ruff))
    w.outline(ruff, 2.2)
    return w


# ================= 输出 =================
GENES = {"Giants/Genes/FrostGiantCore": core_icon, "Giants/Genes/FrostSnowWalker": snowwalker_icon, "Giants/Genes/FrostGiantFrailty": frailty_icon,
         "Giants/Genes/FrostWolfLord": wolflord_icon, "Giants/Genes/FrostLootHeart": lootheart_icon}
SMALL = {"Giants/FrostBraids/FrostBraids_south": braids_south, "Giants/FrostBraids/FrostBraids_north": braids_north, "Giants/FrostBraids/FrostBraids_east": braids_east,
         "Giants/Items/FrostGiantAxe": axe_item, "Giants/Items/WinterWolfCloak": cloak_item, "Giants/Items/FrostGiantHorn": horn_item,
         "Giants/Items/WinterWolfLeather/WinterWolfLeather_a": lambda: leather(1), "Giants/Items/WinterWolfLeather/WinterWolfLeather_b": lambda: leather(2),
         "Giants/Items/WinterWolfLeather/WinterWolfLeather_c": lambda: leather(3)}


def preview(paths):
    tiles = [Image.open(p).convert("RGBA") for p in paths]
    cell = 140; cols = 10; rows = (len(tiles) + cols - 1) // cols
    sheet = Image.new("RGBA", (cell * cols, cell * rows), (58, 62, 66, 255))
    for i, t in enumerate(tiles):
        t = t.copy(); t.thumbnail((128, 128), Image.LANCZOS)
        sheet.alpha_composite(t, ((i % cols) * cell + (cell - t.width) // 2, (i // cols) * cell + (cell - t.height) // 2))
    out = os.path.join(ROOT, "ArtSource", "Giants-1.30.0-preview.png")
    sheet.convert("RGB").save(out)
    return out


if __name__ == "__main__":
    out = []
    for rel, fn in GENES.items():
        out.append(fn().save(rel, 256))
    for rel, fn in SMALL.items():
        out.append(fn().save(rel, 128))
    path = os.path.join(TEX, "Giants", "Abilities", "WinterHorn.png"); os.makedirs(os.path.dirname(path), exist_ok=True)
    winter_horn_ability().save(path); out.append(path)
    for b in BODIES:
        for d in DIRS:
            out.append(tunic(b, d).save("FireGiant/Worn/Tunic_%s_%s" % (b, d)))
            out.append(cloak(b, d).save("Giants/Worn/WinterWolfCloak_%s_%s" % (b, d)))
    out.append(preview(out))
    print("\n".join(out))
