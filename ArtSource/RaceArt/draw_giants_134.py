# 1.34.0 风暴巨人 / 1.34.1 云巨人贴图：照 draw_giants_130.py 的画法（粗黑轮廓、平涂、单侧暗面、低饱和），
# 能力图标用 1.27.4 职业图标的卡牌画法，穿戴贴图以熔铁重甲的穿戴贴图为轮廓蒙版（五体型三向 256px）。
# 用法：python -I ArtSource/RaceArt/draw_giants_134.py <模组根目录> [storm|cloud|all]
import math, os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import draw_race_art as base
from draw_race_art import Pic, poly, ell, tent, taper, arc, mir, ellpts, INK, OW, WOOD, WOOD_D, STEEL, STEEL_D, GOLD, GOLD_D
import draw_giants_130 as g130
from draw_giants_130 import Worn, silhouette, bbox, shade_right, _m, _poly, _line, _arc, _taper, _layer, SS, S, BODIES, DIRS, K
from PIL import Image, ImageDraw, ImageFilter, ImageChops

ROOT = sys.argv[1] if len(sys.argv) > 1 else os.getcwd()
BATCH = sys.argv[2] if len(sys.argv) > 2 else "all"
base.TEX = g130.TEX = os.path.join(ROOT, "Textures")
TEX = base.TEX
HEAD_OW = 1.8

# ---------------- 风暴巨人调色（深蓝紫 + 电光黄，低饱和） ----------------
ST_SKIN = (156, 148, 178); ST_SKIN_D = (124, 116, 148)
ST_TEAL = (110, 160, 156)
NAVY = (58, 66, 104); NAVY_D = (40, 46, 78); NAVY_L = (98, 110, 156)
VIOLET = (112, 98, 150); VIOLET_D = (84, 72, 116)
CLOUD_G = (150, 156, 172); CLOUD_GD = (112, 118, 136)
BOLT = (238, 214, 108); BOLT_L = (252, 242, 186); BOLT_D = (196, 166, 70)
CORAL = (196, 112, 104); CORAL_D = (150, 80, 78); CORAL_L = (226, 158, 144)
SEA = (80, 132, 138); SEA_D = (56, 98, 106); SEA_L = (138, 182, 182)
FOAM = (226, 236, 238)
PLASTEEL = (166, 176, 186); PLASTEEL_D = (124, 134, 146)
MITHRAL = (206, 222, 232)
COLDIRON = (128, 140, 156); COLDIRON_D = (94, 104, 120)
JADE = (106, 156, 120)
SILVER_H = (214, 218, 228)


def bolt_shape(cx, top, bot, w):
    """一道折线闪电（竖直方向，上宽下尖）。"""
    h = bot - top
    return [(cx - w * .2, top), (cx + w * .5, top), (cx + w * .1, top + h * .38), (cx + w * .55, top + h * .38),
            (cx - w * .25, bot), (cx + w * .02, top + h * .55), (cx - w * .45, top + h * .55)]


def bolt(p, cx, top, bot, w, ow=OW):
    p.add(poly(bolt_shape(cx, top, bot, w)), BOLT, ow)
    h = bot - top
    p.add(poly([(cx - w * .1, top + h * .04), (cx + w * .28, top + h * .04), (cx + w * .02, top + h * .32)]), BOLT_L, 0)


def cloud(p, cx, cy, w, color=CLOUD_G, dark=CLOUD_GD, ow=OW):
    parts = [ell((cx - w * .5, cy - w * .12, cx - w * .1, cy + w * .22)), ell((cx - w * .3, cy - w * .32, cx + w * .12, cy + w * .14)),
             ell((cx - w * .02, cy - w * .26, cx + w * .38, cy + w * .16)), ell((cx + w * .16, cy - w * .1, cx + w * .5, cy + w * .22)),
             ("rect", (cx - w * .32, cy, cx + w * .34, cy + w * .22))]
    p.add(parts, color, ow)
    p.add([ell((cx + w * .04, cy - w * .04, cx + w * .44, cy + w * .22))], dark, 0)


def coral(p, x, y, h, color=CORAL, dark=CORAL_D, w=4.0, ow=OW):
    """一枝分叉的珊瑚（从 (x,y) 向上长 h）。"""
    trunk = [(x, y), (x + h * .04, y - h * .45), (x - h * .02, y - h)]
    p.add(tent(trunk, w, w * .55), color, ow)
    for (sx, sy, ex, ey) in ((x + h * .03, y - h * .4, x + h * .32, y - h * .72), (x, y - h * .6, x - h * .3, y - h * .9), (x + h * .02, y - h * .25, x - h * .26, y - h * .45)):
        p.add(tent([(sx, sy), ((sx + ex) / 2 + h * .03, (sy + ey) / 2), (ex, ey)], w * .7, w * .4), color, ow)
    p.add(tent([(x + h * .06, y - h * .1), (x + h * .07, y - h * .5)], w * .35, w * .2), dark, 0)


# ================= 风暴巨人基因图标（256） =================
def st_core_icon():
    p = Pic()
    cloud(p, 50, 20, 70, NAVY_L, NAVY)
    bolt(p, 78, 26, 58, 14, 2.2)
    # 头与脸
    p.add(ell((28, 30, 72, 78)), ST_SKIN)
    p.add(poly([(56, 32)] + arc(50, 54, 22, -70, 70, 12) + [(60, 76), (64, 54)]), ST_SKIN_D, 0)
    # 珊瑚雷纹头冠
    p.add(poly([(26, 42), (50, 30), (74, 42), (74, 36), (50, 24), (26, 36)]), CORAL)
    for x, hh in ((32, 14), (42, 18), (50, 22), (58, 18), (68, 14)):
        coral(p, x, 36 - (6 if x == 50 else 2), hh, CORAL, CORAL_D, 3.2, 1.8)
    p.add(poly([(46, 32), (50, 28), (54, 32), (50, 36)]), BOLT, 1.4)
    p.add([ell((37, 48, 43, 54)), ell((57, 48, 63, 54))], BOLT_L, 1.6)
    # 银白长须
    p.add(poly([(30, 58), (38, 64), (50, 62), (62, 64), (70, 58), (68, 78), (58, 94), (50, 98), (42, 94), (32, 78)]), SILVER_H)
    p.add(poly([(52, 64), (62, 64), (68, 74), (58, 92), (52, 96)]), (176, 182, 196), 0)
    p.line([(44, 70), (50, 68), (56, 70)], 1.8, INK)
    return p


def st_body_icon():
    p = Pic()
    # 一具魁梧的躯干剪影，闪电劈下却被身上的雷纹导走
    p.add(poly([(20, 96), (24, 56), (34, 44), (50, 40), (66, 44), (76, 56), (80, 96)]), ST_SKIN)
    p.add(poly([(50, 40), (66, 44), (76, 56), (80, 96), (56, 96)]), ST_SKIN_D, 0)
    for pts in (((36, 60), (44, 70), (40, 80), (48, 92)), ((62, 58), (56, 68), (62, 78), (56, 90))):
        p.add(("line", list(pts), 3.2), BOLT, 1.2)
    bolt(p, 48, 0, 42, 22, 2.4)
    for x, y in ((18, 30), (82, 26), (86, 50)):
        p.add(("line", [(x - 5, y - 4), (x, y), (x - 3, y + 4), (x + 3, y + 8)], 2.4), BOLT_L, 1.2)
    return p


def st_child_icon():
    p = Pic()
    cloud(p, 50, 26, 84, NAVY_L, NAVY)
    for i, x in enumerate((22, 36, 50, 64, 78)):
        p.add(("line", [(x, 52 + (i % 2) * 6), (x - 5, 66 + (i % 2) * 6)], 2.6), SEA_L, 1.2)
    bolt(p, 52, 40, 92, 22, 2.4)
    # 音符：风暴在歌唱
    for x, y in ((18, 82), (82, 74)):
        p.add(ell((x - 6, y - 4, x + 4, y + 4)), FOAM, 1.6)
        p.add(tent([(x + 3, y), (x + 3, y - 18)], 2.4, 2.4), FOAM, 1.4)
        p.add(tent([(x + 3, y - 18), (x + 11, y - 12)], 2.4, 2.4), FOAM, 1.4)
    return p


def st_melancholy_icon():
    p = Pic()
    # 低垂的脸，一滴泪，头顶一小片雨云
    cloud(p, 50, 18, 56, CLOUD_G, CLOUD_GD)
    for x in (34, 46, 58):
        p.add(("line", [(x, 32), (x - 3, 40)], 2.2), SEA_L, 1.2)
    p.add(ell((26, 40, 74, 92)), ST_SKIN)
    p.add(poly([(56, 42)] + arc(50, 66, 24, -70, 70, 12) + [(60, 90), (64, 66)]), ST_SKIN_D, 0)
    for s in (lambda q: q, mir):
        p.add(("line", s([(34, 60), (40, 63), (44, 61)]), 2.2), INK, 0)
        p.add(("line", s([(32, 54), (42, 52)]), 2.0), INK, 0)
    p.add(("line", arc(50, 86, 8, 200, 340, 8), 2.2), INK, 0)
    p.add(poly([(40, 66), (43, 72), (40, 76), (37, 72)]), SEA_L, 1.4)
    return p


def st_omen_icon():
    p = Pic()
    # 一只睁开的眼睛，瞳孔里有闪电，周围一圈风暴云
    p.add(("line", arc(50, 50, 40, 0, 360, 40), 7), NAVY)
    for k in range(8):
        a = math.radians(k * 45 + 10)
        p.add(("line", [(50 + math.cos(a) * 34, 50 + math.sin(a) * 34), (50 + math.cos(a + .35) * 46, 50 + math.sin(a + .35) * 46)], 3), NAVY_L, 1.2)
    eye = [(14, 50)] + arc(50, 86, 52, 225, 315, 16) + [(86, 50)] + arc(50, 14, 52, 45, 135, 16)
    p.add(poly(eye), FOAM)
    p.add(ell((34, 34, 66, 66)), VIOLET)
    p.add(ell((40, 40, 60, 60)), NAVY_D, 0)
    bolt(p, 50, 38, 62, 12, 1.4)
    return p


# ================= 珊瑚雷纹头冠（128，与霜巨人发辫同一画框：原版头部约 x 32-68、y 30-70） =================
def crown_band(p, pts, th=4.2):
    p.add(("line", pts, th), CORAL_D, HEAD_OW)
    p.add(("line", pts, th * .55), CORAL, 0)


def crown_south():
    p = Pic()
    band = [(33, 40), (40, 37), (50, 36), (60, 37), (67, 40)]
    for x, hh, lean in ((35, 9, -1), (42, 12, 0), (50, 15, 0), (58, 12, 0), (65, 9, 1)):
        coral(p, x, 39 - (1 if x == 50 else 0), hh, CORAL, CORAL_D, 2.4, HEAD_OW)
    crown_band(p, band)
    p.add(poly([(48, 36), (50, 32), (52, 36), (50, 40)]), BOLT, 1.2)
    return p


def crown_north():
    p = Pic()
    band = [(33, 40), (40, 42), (50, 43), (60, 42), (67, 40)]
    for x, hh in ((36, 9), (44, 12), (50, 14), (56, 12), (64, 9)):
        coral(p, x, 41, hh, CORAL, CORAL_D, 2.4, HEAD_OW)
    crown_band(p, band)
    return p


def crown_east():
    p = Pic()
    band = [(36, 40), (46, 37), (56, 37), (63, 40)]
    for x, hh in ((40, 10), (48, 13), (56, 11)):
        coral(p, x, 38, hh, CORAL, CORAL_D, 2.4, HEAD_OW)
    crown_band(p, band)
    p.add(poly([(58, 36), (60, 32), (62, 36), (60, 40)]), BOLT, 1.2)
    return p


# ================= 风暴物品（128） =================
def trident_item():
    p = Pic()
    # 先横着画（戟尖朝右），最后整体转 18° 斜放
    p.add(tent([(6, 50), (66, 50)], 5.5, 5.5), PLASTEEL)
    p.add(tent([(6, 51.5), (66, 51.5)], 2.2, 2.2), PLASTEEL_D, 0)
    for x in (16, 24, 32):
        p.add(tent([(x, 47), (x, 53)], 6.4, 6.4), MITHRAL, 1.4)
    p.add(tent([(64, 34), (70, 42), (70, 58), (64, 66)], 5, 5), MITHRAL)  # 弯月形的叉托
    p.add(tent([(68, 50), (96, 50)], 6.5, 1.2), MITHRAL)  # 中齿
    for s in (-1, 1):
        p.add(tent([(66, 50 + s * 15), (80, 50 + s * 15.5), (92, 50 + s * 13)], 5, 1.2), MITHRAL)  # 两侧的齿
        p.add(poly([(84, 50 + s * 15), (79, 50 + s * 10), (81, 50 + s * 14)]), MITHRAL, 1.2)  # 倒钩
    p.add(tent([(70, 42), (70, 58)], 2, 2), PLASTEEL_D, 0)
    p.add(("line", [(46, 40), (52, 34), (50, 30), (58, 24)], 2.2), BOLT, 1.2)
    p.add(("line", [(48, 60), (56, 66), (54, 70), (62, 76)], 2.2), BOLT, 1.2)
    p.img = p.img.rotate(18, resample=Image.BICUBIC, center=(p.img.width / 2, p.img.height / 2))
    return p

def orb_item():
    p = Pic()
    p.add(ell((22, 64, 78, 92)), COLDIRON)  # 底座
    p.add(ell((28, 70, 72, 88)), COLDIRON_D, 0)
    p.add(ell((20, 12, 80, 72)), NAVY)
    p.add(ell((26, 18, 74, 66)), NAVY_L, 0)
    cloud(p, 50, 36, 40, CLOUD_G, CLOUD_GD, 1.6)
    bolt(p, 50, 40, 64, 12, 1.4)
    p.add(ell((30, 20, 42, 30)), FOAM, 0)
    return p


def coral_armor_item():
    p = Pic()
    body = [(26, 16), (74, 16), (88, 30), (84, 60), (72, 64), (70, 90), (30, 90), (28, 64), (16, 60), (12, 30)]
    p.add(poly(body), SEA)
    p.add(poly([(50, 18), (74, 16), (88, 30), (84, 60), (72, 64), (70, 90), (52, 90)]), SEA_D, 0)
    # 珊瑚片鳞
    for row in range(4):
        for col in range(4):
            x = 30 + col * 11 + (5 if row % 2 else 0); y = 30 + row * 14
            if x > 72:
                continue
            p.add(poly([(x - 5, y), (x + 5, y), (x + 4, y + 8), (x, y + 11), (x - 4, y + 8)]), CORAL if (row + col) % 2 else CORAL_L, 1.4)
    # 寒铁肩甲与玉扣
    for s in (lambda q: q, mir):
        p.add(poly(s([(12, 28), (28, 16), (36, 22), (22, 38)])), COLDIRON)
    p.add(ell((44, 18, 56, 28)), JADE, 1.6)
    return p


def storm_bolt_item():
    p = Pic()
    glow = Image.new("RGBA", p.img.size, (0, 0, 0, 0))
    m = Image.new("L", p.img.size, 0)
    ImageDraw.Draw(m).polygon(p.P(bolt_shape(50, 4, 96, 34)), fill=255)
    m = m.filter(ImageFilter.GaussianBlur(40))
    glow.paste(Image.new("RGBA", p.img.size, BOLT_L + (255,)), (0, 0), m.point(lambda v: min(255, v * 2)))
    p.img = Image.alpha_composite(glow, p.img)
    p.add(poly(bolt_shape(50, 4, 96, 26)), BOLT, 0)
    p.add(poly(bolt_shape(50, 10, 90, 12)), BOLT_L, 0)
    return p


# ================= 能力图标（128） =================
def lightning_bolt_ability():
    im = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    # 一只巨人的拳头（左下）放出一道横贯画面的闪电
    _layer(im, _taper([(2, 126), (34, 92)], 34, 30), ST_SKIN)
    m = _m(); ImageDraw.Draw(m).ellipse([22 * SS, 60 * SS, 62 * SS, 98 * SS], fill=255); _layer(im, m, ST_SKIN)
    for y in (70, 78, 86):
        _layer(im, _line([(44, y), (58, y - 2)], 3), ST_SKIN_D, False)
    _layer(im, _line([(26, 70), (24, 60), (32, 56)], 3), CORAL, False)  # 腕上的珊瑚环
    pts = [(56, 64), (72, 54), (66, 46), (88, 34), (84, 26), (122, 6)]
    _layer(im, _line(pts, 11), BOLT, False)
    _layer(im, _line(pts, 4), BOLT_L, False)
    for (x, y) in ((76, 70), (100, 50), (106, 24)):
        _layer(im, _line([(x - 6, y + 6), (x, y), (x + 6, y + 2)], 4), BOLT_L, False)
    return im.resize((128, 128), Image.LANCZOS)

def summon_storm_ability():
    im = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    # 漩涡状的风暴云，中心劈下三道闪电
    for i, r in enumerate((52, 40, 28)):
        _layer(im, _arc(64, 50, r, 190 + i * 20, 350 + i * 20, 12), (NAVY_L, NAVY, VIOLET)[i], False)
    m = _m(); d = ImageDraw.Draw(m)
    for (x0, y0, x1, y1) in ((14, 30, 64, 70), (36, 14, 92, 60), (60, 22, 116, 66)):
        d.ellipse([x0 * SS, y0 * SS, x1 * SS, y1 * SS], fill=255)
    _layer(im, m, CLOUD_G)
    for pts in (((44, 66), (36, 84), (46, 86), (36, 112)), ((66, 68), (60, 88), (72, 90), (62, 122)), ((88, 64), (84, 80), (94, 82), (88, 104))):
        _layer(im, _line(list(pts), 8), BOLT, False)
        _layer(im, _line(list(pts), 3), BOLT_L, False)
    return im.resize((128, 128), Image.LANCZOS)


# ================= 穿戴：海渊珊瑚甲（256，五体型三向） =================
def coral_armor(body, d):
    w = Worn(); m = silhouette(body, d); x0, y0, x1, y1 = bbox(m); W, H = x1 - x0, y1 - y0; cx = (x0 + x1) / 2
    w.fill(m, SEA)
    w.fill(shade_right(m, (x0, y0, x1, y1), .3 if d != "east" else .42), SEA_D)
    # 一排排珊瑚鳞片
    rows = 6
    for r in range(rows):
        y = y0 + H * (.18 + r * .12)
        cols = 7
        for c in range(cols + 1):
            x = x0 + W * (c + (.5 if r % 2 else 0)) / cols
            sc = w.mask(lambda dr: dr.polygon([(x - W * .06, y), (x + W * .06, y), (x + W * .05, y + H * .07), (x, y + H * .1), (x - W * .05, y + H * .07)], fill=255))
            sc = ImageChops.multiply(sc, m)
            w.fill(sc, CORAL if (r + c) % 2 else CORAL_L)
            w.fill(ImageChops.multiply(sc, w.mask(lambda dr: dr.line([(x - W * .05, y + H * .065), (x, y + H * .095), (x + W * .05, y + H * .065)], fill=255, width=int(1.6 * K)))), CORAL_D)
    # 寒铁骨架：肩带与腰带
    belt_y = y0 + H * .7
    w.fill(ImageChops.multiply(m, w.mask(lambda dr: dr.rectangle([x0, belt_y, x1, belt_y + H * .06], fill=255))), COLDIRON)
    if d == "south":
        for s in (-1, 1):
            w.fill(ImageChops.multiply(m, w.mask(lambda dr: dr.line([(cx + s * W * .3, y0 + H * .04), (cx + s * W * .08, belt_y)], fill=255, width=int(5 * K)))), COLDIRON)
        clasp = w.mask(lambda dr: dr.ellipse([cx - W * .06, belt_y - H * .01, cx + W * .06, belt_y + H * .07], fill=255))
        w.fill(clasp, JADE); w.outline(clasp, 2)
    elif d == "north":
        w.fill(ImageChops.multiply(m, w.mask(lambda dr: dr.line([(cx, y0 + H * .05), (cx, belt_y)], fill=255, width=int(5 * K)))), COLDIRON)
    else:
        w.fill(ImageChops.multiply(m, w.mask(lambda dr: dr.line([(x0 + W * .45, y0 + H * .04), (x0 + W * .55, belt_y)], fill=255, width=int(5 * K)))), COLDIRON)
    w.fill(ImageChops.multiply(m, w.mask(lambda dr: dr.line([(x0 + W * .08, y1 - H * .05), (x1 - W * .08, y1 - H * .05)], fill=255, width=int(1.6 * K)))), SEA_L)
    w.outline(m)
    return w


def preview(paths, name):
    tiles = [Image.open(p).convert("RGBA") for p in paths]
    cell = 140; cols = 10; rows = (len(tiles) + cols - 1) // cols
    sheet = Image.new("RGBA", (cell * cols, cell * rows), (58, 62, 66, 255))
    for i, t in enumerate(tiles):
        t = t.copy(); t.thumbnail((128, 128), Image.LANCZOS)
        sheet.alpha_composite(t, ((i % cols) * cell + (cell - t.width) // 2, (i // cols) * cell + (cell - t.height) // 2))
    out = os.path.join(ROOT, "ArtSource", name)
    sheet.convert("RGB").save(out)
    return out


def save_im(im, rel):
    path = os.path.join(TEX, rel.replace("/", os.sep) + ".png"); os.makedirs(os.path.dirname(path), exist_ok=True)
    im.save(path); return path


STORM_GENES = {"Giants/Genes/StormGiantCore": st_core_icon, "Giants/Genes/StormBody": st_body_icon, "Giants/Genes/StormChild": st_child_icon,
               "Giants/Genes/StormMelancholy": st_melancholy_icon, "Giants/Genes/StormOmen": st_omen_icon}
STORM_SMALL = {"Giants/StormCrown/StormCrown_south": crown_south, "Giants/StormCrown/StormCrown_north": crown_north, "Giants/StormCrown/StormCrown_east": crown_east,
               "Giants/Items/StormTrident": trident_item, "Giants/Items/StormOrb": orb_item, "Giants/Items/AbyssCoralArmor": coral_armor_item, "Giants/Items/StormBolt": storm_bolt_item}


def storm_batch():
    out = []
    for rel, fn in STORM_GENES.items():
        out.append(fn().save(rel, 256))
    for rel, fn in STORM_SMALL.items():
        out.append(fn().save(rel, 128))
    out.append(save_im(lightning_bolt_ability(), "Giants/Abilities/LightningBolt"))
    out.append(save_im(summon_storm_ability(), "Giants/Abilities/SummonStorm"))
    for b in BODIES:
        for d in DIRS:
            out.append(coral_armor(b, d).save("Giants/Worn/AbyssCoralArmor_%s_%s" % (b, d)))
    out.append(preview(out, "Giants-1.34.0-preview.png"))
    return out


# ================= 1.34.1 云巨人（银灰 + 淡金） =================
CL_SKIN = (206, 220, 232); CL_SKIN_D = (170, 186, 202)
SILV = (196, 202, 212); SILV_D = (150, 158, 172); SILV_L = (232, 236, 242)
PGOLD = (226, 202, 134); PGOLD_D = (186, 160, 96)
MIST = (228, 234, 242); MIST_D = (188, 198, 214)
SKY = (150, 182, 218); SKY_D = (112, 142, 182)
AMBER = (232, 168, 70); AMBER_L = (250, 214, 140); AMBER_D = (184, 120, 44)
ROBE = (230, 230, 232); ROBE_D = (190, 190, 194); ROBE_DD = (156, 156, 160)  # 云纹长袍受布料着色，画成近白灰
STEEL_C = (150, 154, 160); STEEL_CD = (112, 116, 122)


def swirl(p, cx, cy, r, color=PGOLD, w=2.6, ow=1.4):
    """一个卷云纹（螺旋）。"""
    pts = [(cx + math.cos(math.radians(a)) * r * (1 - a / 900), cy + math.sin(math.radians(a)) * r * (1 - a / 900)) for a in range(0, 540, 30)]
    p.add(("line", pts, w), color, ow)


def cl_core_icon():
    p = Pic()
    cloud(p, 50, 80, 96, MIST, MIST_D)
    p.add(ell((28, 18, 72, 66)), CL_SKIN)
    p.add(poly([(56, 20)] + arc(50, 42, 22, -70, 70, 12) + [(60, 64), (64, 42)]), CL_SKIN_D, 0)
    # 银发与云纹头冠
    p.add(poly([(26, 34), (30, 16), (50, 8), (70, 16), (74, 34), (68, 24), (50, 18), (32, 24)]), SILV_L)
    p.add(poly([(28, 28), (50, 18), (72, 28), (72, 22), (50, 12), (28, 22)]), SILV)
    for x in (34, 50, 66):
        swirl(p, x, 18 - (4 if x == 50 else 0), 5, PGOLD, 2.2, 1.2)
    p.add(ell((47, 8, 53, 14)), AMBER, 1.2)
    p.add([ell((37, 38, 43, 44)), ell((57, 38, 63, 44))], SKY, 1.6)
    p.line([(44, 54), (50, 56), (56, 54)], 1.8, INK)
    return p


def cl_noble_icon():
    p = Pic()
    # 一张华盖大床
    p.add(poly([(14, 40), (86, 40), (86, 46), (14, 46)]), PGOLD)
    for x in (16, 82):
        p.add(tent([(x, 44), (x, 92)], 4, 4), PGOLD_D)
    p.add(poly([(14, 40), (24, 18), (50, 10), (76, 18), (86, 40)]), SKY)
    p.add(poly([(50, 10), (76, 18), (86, 40), (60, 40)]), SKY_D, 0)
    p.add(poly([(18, 66), (82, 66), (84, 86), (16, 86)]), ROBE)
    p.add(poly([(18, 60), (40, 58), (40, 68), (18, 68)]), SILV_L)
    p.add(poly([(40, 62), (82, 60), (84, 72), (40, 74)]), SKY, 1.6)
    swirl(p, 62, 28, 7, PGOLD, 2.4, 1.2)
    return p


def cl_gift_icon():
    p = Pic()
    p.add(poly([(20, 44), (80, 44), (76, 92), (24, 92)]), SKY)
    p.add(poly([(50, 44), (80, 44), (76, 92), (50, 92)]), SKY_D, 0)
    p.add(poly([(16, 34), (84, 34), (84, 46), (16, 46)]), SKY)
    p.add(poly([(44, 34), (56, 34), (56, 92), (44, 92)]), PGOLD, 1.6)
    for s in (lambda q: q, mir):
        p.add(tent(s([(50, 34), (36, 22), (28, 26), (36, 34)]), 7, 4), PGOLD)
    for x, y in ((14, 18), (86, 20), (80, 66)):
        cloud(p, x, y, 18, MIST, MIST_D, 1.6)
    return p


def cl_grace_icon():
    p = Pic()
    # 一只优雅抬起的手和两道流动的云纹（谈吐）
    p.add(tent([(24, 96), (34, 66), (48, 48)], 18, 14), CL_SKIN)
    for (x0, y0, x1, y1) in ((44, 50, 52, 26), (48, 52, 60, 30), (52, 56, 66, 38), (54, 60, 68, 50)):
        p.add(tent([(x0, y0), (x1, y1)], 6, 5), CL_SKIN)
    p.add(tent([(20, 70), (36, 66)], 22, 22), SKY, 2)
    for i, (x, y, r) in enumerate(((74, 30, 10), (84, 56, 7), (66, 78, 6))):
        swirl(p, x, y, r, PGOLD if i % 2 == 0 else SILV_L, 2.6, 1.4)
    return p


def cl_crown(p, band, gems):
    p.add(("line", band, 4.0), SILV_D, HEAD_OW)
    p.add(("line", band, 2.2), SILV_L, 0)
    for x, y, r in gems:
        p.add(("line", [(x - r, y + r * .6), (x - r * .4, y - r * .8), (x + r * .4, y - r), (x + r, y + r * .6)], 2.2), SILV, HEAD_OW)


def cloud_crown_south():
    p = Pic()
    cl_crown(p, [(33, 40), (40, 37), (50, 36), (60, 37), (67, 40)], [(38, 35, 3.4), (62, 35, 3.4)])
    swirl(p, 44, 33, 3, PGOLD, 1.6, 1.0); swirl(p, 56, 33, 3, PGOLD, 1.6, 1.0)
    p.add(ell((47.5, 30, 52.5, 35)), AMBER, 1.2)
    return p


def cloud_crown_north():
    p = Pic()
    cl_crown(p, [(33, 40), (40, 42), (50, 43), (60, 42), (67, 40)], [(40, 39, 3.2), (60, 39, 3.2), (50, 40, 3.6)])
    return p


def cloud_crown_east():
    p = Pic()
    cl_crown(p, [(36, 40), (46, 37), (56, 37), (63, 40)], [(44, 34, 3.2)])
    swirl(p, 52, 33, 3, PGOLD, 1.6, 1.0)
    p.add(ell((57.5, 31, 62.5, 36)), AMBER, 1.2)
    return p


def morningstar_item():
    p = Pic()
    p.add(tent([(6, 50), (66, 50)], 6, 6), WOOD)
    p.add(tent([(6, 51.5), (66, 51.5)], 2.4, 2.4), WOOD_D, 0)
    for x in (14, 22):
        p.add(tent([(x, 46.5), (x, 53.5)], 7, 7), PGOLD, 1.4)
    p.add(tent([(62, 44), (62, 56)], 8, 8), PGOLD, 1.6)  # 金箍
    spikes = []
    for k in range(10):
        a = math.radians(k * 36)
        spikes.append(poly([(80 + math.cos(a - .22) * 13, 50 + math.sin(a - .22) * 13), (80 + math.cos(a) * 21, 50 + math.sin(a) * 21), (80 + math.cos(a + .22) * 13, 50 + math.sin(a + .22) * 13)]))
    p.add(spikes, STEEL_C)
    p.add(ell((66, 36, 94, 64)), STEEL_C)
    p.add(ell((76, 44, 94, 64)), STEEL_CD, 0)
    swirl(p, 78, 48, 6, PGOLD, 2.2, 1.0)
    p.img = p.img.rotate(18, resample=Image.BICUBIC, center=(p.img.width / 2, p.img.height / 2))
    return p


def robe_item():
    p = Pic()
    body = [(34, 12), (66, 12), (84, 26), (90, 50), (80, 54), (74, 40), (78, 94), (22, 94), (26, 40), (20, 54), (10, 50), (16, 26)]
    p.add(poly(body), ROBE)
    p.add(poly([(50, 14), (66, 12), (84, 26), (90, 50), (80, 54), (74, 40), (78, 94), (52, 94)]), ROBE_D, 0)
    p.add(poly([(40, 12), (50, 28), (60, 12)]), ROBE_DD, 1.6)
    p.line([(50, 28), (50, 92)], 1.6, ROBE_DD)
    for (x, y) in ((34, 54), (64, 62), (40, 80), (60, 84)):
        swirl(p, x, y, 6, ROBE_DD, 2.0, 0)
    p.add(poly([(24, 88), (76, 88), (78, 94), (22, 94)]), ROBE_DD, 1.4)
    return p


def fog_flask_item():
    p = Pic()
    p.add(ell((22, 38, 78, 94)), SKY)
    p.add(ell((44, 44, 78, 94)), SKY_D, 0)
    p.add(poly([(40, 20), (60, 20), (62, 42), (38, 42)]), SKY)
    p.add(poly([(36, 12), (64, 12), (64, 22), (36, 22)]), WOOD)  # 木塞
    cloud(p, 50, 66, 40, MIST, MIST_D, 1.4)
    for x, y in ((70, 14), (80, 4)):
        cloud(p, x, y, 14, MIST, MIST_D, 1.2)
    return p


def sky_amber_item():
    p = Pic()
    gem = [(50, 10), (78, 30), (82, 62), (60, 90), (36, 88), (18, 62), (22, 30)]
    p.add(poly(gem), AMBER)
    p.add(poly([(50, 10), (78, 30), (82, 62), (60, 90), (52, 56)]), AMBER_D, 0)
    p.add(poly([(28, 32), (50, 16), (46, 40)]), AMBER_L, 0)
    cloud(p, 48, 58, 30, MIST, MIST_D, 1.2)
    return p


def misty_step_ability():
    im = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    # 左边一团散开的银雾，右边一个出现的身影剪影，中间一道弧线
    for (x, y, r) in ((24, 84, 16), (38, 98, 14), (18, 104, 12), (40, 74, 10)):
        m = _m(); ImageDraw.Draw(m).ellipse([(x - r) * SS, (y - r) * SS, (x + r) * SS, (y + r) * SS], fill=255); _layer(im, m, MIST)
    _layer(im, _arc(64, 90, 40, 200, 330, 6), SILV_L, False)
    _layer(im, _poly([(84, 116), (88, 66), (100, 54), (112, 66), (116, 116)]), SKY)
    m = _m(); ImageDraw.Draw(m).ellipse([88 * SS, 28 * SS, 112 * SS, 52 * SS], fill=255); _layer(im, m, CL_SKIN)
    for (x, y) in ((76, 40), (72, 60), (120, 40)):
        _layer(im, _line([(x - 4, y), (x + 4, y)], 3), SILV_L, False)
    return im.resize((128, 128), Image.LANCZOS)


def cloud_fog_ability():
    im = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    m = _m(); d = ImageDraw.Draw(m)
    for (x0, y0, x1, y1) in ((8, 50, 60, 100), (34, 30, 96, 90), (66, 48, 122, 104), (20, 74, 112, 120)):
        d.ellipse([x0 * SS, y0 * SS, x1 * SS, y1 * SS], fill=255)
    _layer(im, m, MIST)
    for y in (64, 84, 102):
        _layer(im, _line([(22, y), (52, y - 4), (80, y + 2), (108, y - 2)], 4), MIST_D, False)
    _layer(im, _arc(64, 24, 14, 0, 360, 5), PGOLD, False)  # 被遮住一半的太阳
    return im.resize((128, 128), Image.LANCZOS)


def call_wind_ability():
    im = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    # 三道卷风把一朵云吹散，右上露出太阳
    m = _m(); ImageDraw.Draw(m).ellipse([82 * SS, 8 * SS, 120 * SS, 46 * SS], fill=255); _layer(im, m, PGOLD)
    for i, y in enumerate((52, 74, 96)):
        pts = [(8, y), (40, y - 6), (70, y + 2), (92, y - 6)]
        _layer(im, _line(pts, 7), SILV_L if i % 2 == 0 else SKY, False)
        _layer(im, _arc(98, y - 14, 10, 90, 300, 6), SILV_L if i % 2 == 0 else SKY, False)
    m = _m(); d = ImageDraw.Draw(m)
    for (x0, y0, x1, y1) in ((14, 14, 46, 40), (30, 6, 66, 38), (52, 16, 78, 40)):
        d.ellipse([x0 * SS, y0 * SS, x1 * SS, y1 * SS], fill=255)
    _layer(im, m, MIST)
    return im.resize((128, 128), Image.LANCZOS)


def cloud_robe_worn(body, d):
    w = Worn(); base_m = silhouette(body, d); x0, y0, x1, y1 = bbox(base_m); W, H = x1 - x0, y1 - y0; cx = (x0 + x1) / 2
    # 长袍：身体轮廓再往下垂到脚踝、下摆展开
    hem = min(y1 + H * .1, 256 * K * .9)
    if d == "east":
        skirt = w.mask(lambda dr: dr.polygon([(x0 + W * .08, y0 + H * .55), (x1 - W * .12, y0 + H * .55), (x1 + W * .02, hem), (x0 - W * .02, hem)], fill=255))
    else:
        skirt = w.mask(lambda dr: dr.polygon([(x0 + W * .06, y0 + H * .55), (x1 - W * .06, y0 + H * .55), (x1 + W * .06, hem), (x0 - W * .06, hem)], fill=255))
    m = ImageChops.lighter(base_m, skirt)
    w.fill(m, ROBE)
    w.fill(shade_right(m, (x0, y0, x1, hem), .3 if d != "east" else .42), ROBE_D)
    # 腰带与衣襟
    belt_y = y0 + H * .6
    w.fill(ImageChops.multiply(m, w.mask(lambda dr: dr.rectangle([x0, belt_y, x1, belt_y + H * .05], fill=255))), ROBE_DD)
    if d == "south":
        w.fill(ImageChops.multiply(m, w.mask(lambda dr: dr.line([(cx, y0 + H * .06), (cx, hem)], fill=255, width=int(2.2 * K)))), ROBE_DD)
        w.fill(ImageChops.multiply(m, w.mask(lambda dr: dr.polygon([(cx - W * .14, y0), (cx, y0 + H * .2), (cx + W * .14, y0)], fill=255))), ROBE_D)
    elif d == "north":
        w.fill(ImageChops.multiply(m, w.mask(lambda dr: dr.line([(cx, y0 + H * .1), (cx, hem)], fill=255, width=int(1.6 * K)))), ROBE_D)
    # 卷云纹刺绣
    for i in range(6):
        x = x0 + W * (.2 + .3 * (i % 3)); y = y0 + H * (.72 + .22 * (i // 3))
        if d == "east" and i % 3 == 2:
            continue
        r = W * .06
        pts = [(x + math.cos(math.radians(a)) * r * (1 - a / 900), y + math.sin(math.radians(a)) * r * (1 - a / 900)) for a in range(0, 540, 30)]
        w.fill(ImageChops.multiply(m, w.mask(lambda dr: dr.line(pts, fill=255, width=int(1.6 * K)))), ROBE_DD)
    w.fill(ImageChops.multiply(m, w.mask(lambda dr: dr.line([(x0 - W * .04, hem - H * .04), (x1 + W * .04, hem - H * .04)], fill=255, width=int(2 * K)))), ROBE_DD)
    w.outline(m)
    return w


CLOUD_GENES = {"Giants/Genes/CloudGiantCore": cl_core_icon, "Giants/Genes/CloudNoble": cl_noble_icon, "Giants/Genes/CloudGift": cl_gift_icon, "Giants/Genes/CloudGrace": cl_grace_icon}
CLOUD_SMALL = {"Giants/CloudCrown/CloudCrown_south": cloud_crown_south, "Giants/CloudCrown/CloudCrown_north": cloud_crown_north, "Giants/CloudCrown/CloudCrown_east": cloud_crown_east,
               "Giants/Items/CloudMorningstar": morningstar_item, "Giants/Items/CloudRobe": robe_item, "Giants/Items/FogFlask": fog_flask_item, "Giants/Items/SkyAmber": sky_amber_item}


def cloud_batch():
    out = []
    for rel, fn in CLOUD_GENES.items():
        out.append(fn().save(rel, 256))
    for rel, fn in CLOUD_SMALL.items():
        out.append(fn().save(rel, 128))
    out.append(save_im(misty_step_ability(), "Giants/Abilities/MistyStep"))
    out.append(save_im(cloud_fog_ability(), "Giants/Abilities/CloudFog"))
    out.append(save_im(call_wind_ability(), "Giants/Abilities/CallWind"))
    for b in BODIES:
        for d in DIRS:
            out.append(cloud_robe_worn(b, d).save("Giants/Worn/CloudRobe_%s_%s" % (b, d)))
    out.append(preview(out, "Giants-1.34.1-preview.png"))
    return out


if __name__ == "__main__":
    out = []
    if BATCH in ("storm", "all"):
        out += storm_batch()
    if BATCH in ("cloud", "all") and "cloud_batch" in globals():
        out += globals()["cloud_batch"]()
    print("\n".join(out))
