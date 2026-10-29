# 1.39.0 信仰贴图（RimWorld 画风：粗墨线、平涂 + 右下暗部、低饱和；画法和调色沿用 draw_class_art.py）。
# 用法：python -I ArtSource/RaceArt/draw_faith.py <模组根目录> [预览图输出路径]
# 输出到 Textures/Faith：圣坛（2×1，南/北 256×128、东 128×256）、圣徽、祝圣圣水、9 件遗物（128）、12 个神术图标（128）、
#   阿斯莫光环（头部附件 128，南/北/东）、11 位神的小图标（64）、2 个异种型图标与 11 个基因图标（128）、5 个种族能力图标（128）。
# 光环受发色着色（colorType Hair），所以画成近白：亮面就是“发色本身”。
import math, os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from PIL import Image, ImageDraw, ImageFilter, ImageChops
import draw_class_art as A
from draw_class_art import (Icon, poly_mask, ellipse_mask, line_mask, arc_mask, rrect_mask, union, subtract, star_pts,
                            bust_mask, shield_pts, sword_masks, darker, INK, CREAM, CREAM_D, KHAKI, KHAKI_D, SLATE, SLATE_D,
                            RUST, RUST_D, VIOLET, VIOLET_D, GOLD, GOLD_D, BONE, BONE_D, STONE, STONE_D, SS, S, OW)

ROOT = sys.argv[1] if len(sys.argv) > 1 else os.getcwd()
OUT = os.path.join(ROOT, 'Textures', 'Faith')
SILVER = (196, 200, 206); SILVER_D = (146, 150, 158)
DAWN = (226, 150, 110); DAWN_L = (240, 196, 130)
SKY = (132, 168, 214); NIGHT = (70, 56, 96); NIGHT_D = (44, 34, 64)
BLOOD = (150, 52, 44); EMBER = (214, 110, 50); EMBER_L = (238, 182, 86)
WATER = (150, 186, 206); WATER_D = (110, 142, 164)
GREEN = (118, 150, 96); ASH = (150, 150, 140)


def save(img, *path):
    p = os.path.join(OUT, *path)
    os.makedirs(os.path.dirname(p), exist_ok=True)
    img.save(p)
    return p


def rays(I, cx, cy, r0, r1, n, w, col, a0=0, a1=360):
    for i in range(n):
        a = math.radians(a0 + (a1 - a0) * (i + .5) / n)
        I.layer(line_mask([(cx + r0 * math.cos(a), cy + r0 * math.sin(a)), (cx + r1 * math.cos(a), cy + r1 * math.sin(a))], w), col)


def bolt(cx, cy, sc=1.0):
    pts = [(8, -56), (-18, 4), (2, 4), (-10, 56), (24, -10), (4, -10), (18, -56)]
    return poly_mask([(cx + x * sc, cy + y * sc) for x, y in pts])


def hand(cx, cy, sc=1.0):
    palm = rrect_mask(cx - 20 * sc, cy - 6 * sc, cx + 20 * sc, cy + 30 * sc, 9 * sc)
    fingers = union(*[rrect_mask(cx + (x - 4) * sc, cy - 34 * sc + abs(x) * .25 * sc, cx + (x + 4) * sc, cy, 4 * sc) for x in (-15, -5, 5, 15)])
    thumb = line_mask([(cx - 18 * sc, cy + 14 * sc), (cx - 32 * sc, cy - 2 * sc)], 9 * sc)
    return union(palm, fingers, thumb)


def scales(I, cx, cy, sc=1.0, col=SILVER, beam_col=GOLD):
    I.layer(line_mask([(cx, cy - 34 * sc), (cx, cy + 34 * sc)], 7 * sc), beam_col)
    I.layer(line_mask([(cx - 36 * sc, cy - 22 * sc), (cx + 36 * sc, cy - 22 * sc)], 7 * sc), beam_col)
    for sx in (-1, 1):
        x = cx + sx * 36 * sc
        I.layer(union(line_mask([(x, cy - 22 * sc), (x - 12 * sc, cy + 8 * sc)], 3 * sc), line_mask([(x, cy - 22 * sc), (x + 12 * sc, cy + 8 * sc)], 3 * sc)), col, shade=False)
        I.layer(poly_mask([(x - 18 * sc, cy + 8 * sc), (x + 18 * sc, cy + 8 * sc), (x + 10 * sc, cy + 18 * sc), (x - 10 * sc, cy + 18 * sc)]), col)
    I.layer(rrect_mask(cx - 18 * sc, cy + 32 * sc, cx + 18 * sc, cy + 40 * sc, 3 * sc), beam_col)


def skull(I, cx, cy, sc=1.0, col=BONE):
    head = union(ellipse_mask(cx, cy, 22 * sc, 20 * sc), rrect_mask(cx - 13 * sc, cy + 8 * sc, cx + 13 * sc, cy + 26 * sc, 4 * sc))
    holes = union(ellipse_mask(cx - 8 * sc, cy + 2 * sc, 6 * sc), ellipse_mask(cx + 8 * sc, cy + 2 * sc, 6 * sc), poly_mask([(cx, cy + 9 * sc), (cx - 3 * sc, cy + 15 * sc), (cx + 3 * sc, cy + 15 * sc)]))
    I.layer(subtract(head, holes), col)


def flame_mask(cx, cy, w, h):
    return poly_mask([(cx, cy - h), (cx + w * .55, cy - h * .35), (cx + w * .35, cy - h * .45), (cx + w * .6, cy + h * .1), (cx + w * .45, cy + h * .45),
                      (cx, cy + h * .55), (cx - w * .45, cy + h * .45), (cx - w * .6, cy + h * .1), (cx - w * .35, cy - h * .45), (cx - w * .55, cy - h * .35)])


def sun(I, cx, cy, r, col=DAWN_L, ray=DAWN, half=False):
    rays(I, cx, cy, r + 6, r + 22, 9 if half else 12, 7, ray, 180 if half else 0, 360)
    m = ellipse_mask(cx, cy, r)
    if half:
        m = subtract(m, rrect_mask(cx - r - 2, cy, cx + r + 2, cy + r + 2, 0))
    I.layer(m, col)


# ==================== 神的小图标（64） ====================
def deity(key):
    I = Icon()
    col = {'Lathander': DAWN, 'Tyr': GOLD, 'Mystra': SKY, 'Kelemvor': ASH, 'Malar': BLOOD, 'Talos': SLATE, 'Bane': NIGHT,
           'Shar': NIGHT_D, 'Surtr': EMBER, 'Ilsvi': VIOLET, 'Vlaakith': (120, 170, 176)}[key]
    I.layer(ellipse_mask(64, 64, 60), darker(col, .55), shade=False)
    I.layer(arc_mask(64, 64, 56, 0, 360, 7), col, shade=False)
    if key == 'Lathander':
        I.layer(rrect_mask(18, 78, 110, 90, 4), KHAKI_D); sun(I, 64, 78, 24, half=True)
    elif key == 'Tyr':
        scales(I, 64, 60, .95)
    elif key == 'Mystra':
        I.layer(poly_mask(star_pts(64, 64, 44, 18, 7)), (214, 222, 240))
        I.layer(ellipse_mask(64, 64, 11), SKY)
    elif key == 'Kelemvor':
        skull(I, 64, 46, .8); scales(I, 64, 84, .5, SILVER, ASH)
    elif key == 'Malar':
        for dx in (-22, 0, 22):
            I.layer(poly_mask([(64 + dx - 6, 16), (64 + dx + 8, 20), (64 + dx + 2, 112), (64 + dx - 8, 104)]), BONE)
    elif key == 'Talos':
        for dx, dy in ((-26, 6), (0, -6), (26, 6)):
            I.layer(bolt(64 + dx, 64 + dy, .62), (230, 222, 150))
    elif key == 'Bane':
        I.layer(hand(64, 70, 1.05), (40, 36, 46))
        rays(I, 64, 64, 46, 56, 10, 4, GREEN)
    elif key == 'Shar':
        I.layer(ellipse_mask(64, 64, 40), (24, 18, 32), shade=False)
        I.layer(arc_mask(64, 64, 40, 0, 360, 8), (150, 110, 190), shade=False)
    elif key == 'Surtr':
        I.layer(flame_mask(64, 60, 70, 46), EMBER)
        for m, c in sword_masks(64, 70, -90, L=96, blade_col=EMBER_L, hilt_col=RUST_D): I.layer(m, c)
    elif key == 'Ilsvi':
        I.layer(ellipse_mask(64, 50, 30, 24), (190, 150, 190))
        for dx in (-16, -6, 6, 16):
            I.layer(line_mask([(64 + dx, 66), (64 + dx * 1.4, 90), (64 + dx * .8, 108)], 7), (170, 120, 170))
    elif key == 'Vlaakith':
        skull(I, 64, 64, 1.0, (210, 214, 196))
        I.layer(poly_mask([(36, 44), (44, 22), (54, 36), (64, 16), (74, 36), (84, 22), (92, 44)]), (120, 200, 200))
    return I.done(64)


# ==================== 神术图标（128） ====================
def miracle(name):
    I = Icon()
    if name == 'Sanctuary':
        I.layer(ellipse_mask(64, 64, 58), (236, 214, 150), shade=False, outline=False)
        I.layer(poly_mask(shield_pts(64, 64, 76, 94)), SILVER)
        I.layer(union(rrect_mask(58, 34, 70, 94, 3), rrect_mask(40, 52, 88, 64, 3)), GOLD)
    elif name == 'Banish':
        skull(I, 70, 74, 1.3, BONE_D)
        I.layer(poly_mask(star_pts(46, 44, 40, 12, 8)), (246, 232, 170), glow=GOLD)
    elif name == 'Consecrate':
        I.layer(ellipse_mask(64, 92, 56, 22), KHAKI_D)
        I.layer(arc_mask(64, 92, 42, 0, 360, 6), GOLD, shade=False)
        for x, y, r in ((40, 40, 12), (78, 26, 16), (96, 58, 10), (60, 64, 8)):
            I.layer(poly_mask(star_pts(x, y, r, r * .35, 4)), (246, 236, 180))
    elif name == 'Dawn':
        I.layer(rrect_mask(6, 84, 122, 120, 6), KHAKI_D)
        sun(I, 64, 84, 32, half=True)
    elif name == 'Judgment':
        I.layer(line_mask([(28, 112), (78, 54)], 11), RUST)
        I.layer(poly_mask([(56, 26), (104, 72), (88, 88), (40, 42)]), SILVER)
        I.layer(poly_mask(star_pts(32, 30, 18, 6, 6)), GOLD)
    elif name == 'Weave':
        for a in range(0, 180, 30):
            r = math.radians(a)
            I.layer(line_mask([(64 - 54 * math.cos(r), 64 - 54 * math.sin(r)), (64 + 54 * math.cos(r), 64 + 54 * math.sin(r))], 4), SKY, shade=False)
        I.layer(poly_mask(star_pts(64, 64, 40, 16, 7)), (214, 222, 240), glow=SKY)
    elif name == 'Rest':
        I.layer(rrect_mask(14, 72, 114, 110, 10), STONE)
        skull(I, 64, 52, 1.0, BONE)
        I.layer(line_mask([(46, 54), (58, 54)], 4), INK, outline=False, shade=False)
        I.layer(line_mask([(70, 54), (82, 54)], 4), INK, outline=False, shade=False)
    elif name == 'Hunt':
        I.layer(poly_mask([(20, 98), (36, 30), (52, 60), (64, 18), (76, 60), (92, 30), (108, 98)]), BLOOD)
        for dx in (-26, 0, 26):
            I.layer(poly_mask([(64 + dx - 5, 70), (64 + dx + 5, 70), (64 + dx + 2, 112), (64 + dx - 3, 112)]), BONE)
    elif name == 'Thunder':
        I.layer(ellipse_mask(64, 26, 54, 20), SLATE_D)
        I.layer(bolt(64, 76, .9), (240, 232, 160), glow=SKY)
    elif name == 'Dread':
        I.layer(hand(64, 72, 1.5), (46, 40, 54))
        for i, r in enumerate((52, 60)):
            I.layer(arc_mask(64, 64, r, 200, 340, 5), GREEN, shade=False)
    elif name == 'Veil':
        I.layer(poly_mask([(64, 10), (104, 112), (24, 112)]), NIGHT)
        I.layer(ellipse_mask(64, 44, 16), NIGHT_D, shade=False)
        I.layer(union(ellipse_mask(58, 44, 3), ellipse_mask(70, 44, 3)), (190, 170, 230), outline=False, shade=False)
    elif name == 'ForgeWrath':
        I.layer(poly_mask([(12, 64), (120, 18), (120, 110)]), EMBER)
        I.layer(poly_mask([(30, 64), (112, 40), (112, 88)]), EMBER_L, shade=False)
        I.layer(rrect_mask(4, 54, 30, 74, 4), STONE_D)
    return I.done(128)


# ==================== 物品（128） ====================
def holy_symbol():
    I = Icon()
    I.layer(arc_mask(64, 40, 30, 200, 340, 4), SILVER_D, shade=False)
    I.layer(ellipse_mask(64, 74, 34), SILVER)
    I.layer(poly_mask(star_pts(64, 74, 24, 9, 8)), GOLD)
    I.layer(ellipse_mask(64, 74, 7), CREAM, shade=False)
    return I.done(128)


def blessed_water():
    I = Icon()
    I.layer(rrect_mask(54, 14, 74, 38, 4), SILVER_D)
    I.layer(union(ellipse_mask(64, 82, 34, 36), rrect_mask(52, 30, 76, 60, 6)), WATER)
    I.layer(ellipse_mask(64, 86, 22, 22), (210, 228, 236), shade=False, outline=False)
    I.layer(poly_mask(star_pts(64, 84, 16, 6, 4)), GOLD)
    return I.done(128)


def relic(name):
    I = Icon()
    if name == 'MorningStar':
        I.layer(line_mask([(28, 112), (70, 58)], 10), RUST)
        I.layer(poly_mask(star_pts(80, 44, 34, 18, 10)), GOLD)
        I.layer(ellipse_mask(80, 44, 16), DAWN_L)
    elif name == 'StormSpear':
        I.layer(line_mask([(18, 116), (88, 40)], 8), RUST_D)
        I.layer(poly_mask([(84, 36), (114, 10), (100, 46)]), SILVER)
        I.layer(bolt(100, 64, .35), (240, 232, 160), glow=SKY)
    elif name in ('HandOfTyr', 'BeastClaw', 'BlackHand'):
        col = {'HandOfTyr': SILVER, 'BeastClaw': (150, 120, 90), 'BlackHand': (52, 46, 58)}[name]
        I.layer(hand(64, 70, 1.35), col)
        I.layer(rrect_mask(38, 96, 90, 118, 6), darker(col, .8))
        if name == 'BeastClaw':
            for dx in (-16, -5, 5, 16): I.layer(poly_mask([(64 + dx - 4, 26), (64 + dx + 4, 26), (64 + dx, 8)]), BONE)
        if name == 'HandOfTyr': I.layer(poly_mask(star_pts(64, 76, 12, 5, 4)), GOLD)
    elif name == 'SilverTear':
        I.layer(poly_mask([(64, 10), (92, 70), (64, 112), (36, 70)]), (214, 222, 240), glow=SKY)
        I.layer(poly_mask([(64, 34), (78, 70), (64, 92), (50, 70)]), SKY, shade=False)
    elif name == 'AshScales':
        scales(I, 64, 64, 1.1, ASH, SILVER_D)
    elif name == 'NightTear':
        I.layer(poly_mask([(64, 10), (92, 70), (64, 112), (36, 70)]), NIGHT_D, glow=(110, 80, 150))
        I.layer(ellipse_mask(58, 62, 8), (150, 120, 190), shade=False)
    elif name == 'ForgeHeart':
        I.layer(flame_mask(64, 60, 90, 50), EMBER)
        I.layer(poly_mask([(64, 112), (28, 60), (40, 40), (64, 52), (88, 40), (100, 60)]), RUST)
        I.layer(ellipse_mask(64, 66, 14), EMBER_L, shade=False)
    return I.done(128)


# ==================== 圣坛（2×1） ====================
def altar(rot):
    w, h = (256, 128) if rot in ('south', 'north') else (128, 256)
    ss = 4; im = Image.new('RGBA', (w * ss, h * ss), (0, 0, 0, 0)); d = ImageDraw.Draw(im)
    def R(x0, y0, x1, y1, fill, r=6):
        d.rounded_rectangle([x0 * ss - 6 * ss, y0 * ss - 6 * ss, x1 * ss + 6 * ss, y1 * ss + 6 * ss], radius=(r + 4) * ss, fill=INK)
        d.rounded_rectangle([x0 * ss, y0 * ss, x1 * ss, y1 * ss], radius=r * ss, fill=fill)
        d.rounded_rectangle([x0 * ss + (x1 - x0) * ss * .62, y0 * ss, x1 * ss, y1 * ss], radius=r * ss, fill=darker(fill, .82))
        d.rounded_rectangle([x0 * ss, y0 * ss, x1 * ss - (x1 - x0) * ss * .38, y1 * ss], radius=r * ss, fill=fill)
    STONE_L = (172, 166, 150)
    if rot in ('south', 'north'):
        R(14, 70, 242, 118, STONE)          # 底座
        R(44, 40, 212, 80, STONE_L)         # 祭台
        R(98, 10, 158, 60, (186, 180, 160)) # 中央碑
        for x in (60, 196):                 # 两支蜡烛
            R(x - 7, 22, x + 7, 46, CREAM, 3)
            d.ellipse([(x - 6) * ss, 6 * ss, (x + 6) * ss, 22 * ss], fill=INK); d.ellipse([(x - 4) * ss, 8 * ss, (x + 4) * ss, 20 * ss], fill=EMBER_L)
        if rot == 'south':
            cx, cy = 128, 34
            for i in range(8):
                a = math.radians(i * 45); d.line([((cx + 8 * math.cos(a)) * ss, (cy + 8 * math.sin(a)) * ss), ((cx + 18 * math.cos(a)) * ss, (cy + 18 * math.sin(a)) * ss)], fill=GOLD_D, width=4 * ss)
            d.ellipse([(cx - 10) * ss, (cy - 10) * ss, (cx + 10) * ss, (cy + 10) * ss], fill=INK); d.ellipse([(cx - 7) * ss, (cy - 7) * ss, (cx + 7) * ss, (cy + 7) * ss], fill=GOLD)
            R(88, 96, 168, 112, RUST, 3)    # 前面的祭布
    else:
        R(14, 14, 114, 242, STONE)
        R(34, 44, 98, 212, STONE_L)
        R(48, 98, 92, 158, (186, 180, 160))
        for y in (60, 196):
            R(54, y - 7, 78, y + 7, CREAM, 3)
            d.ellipse([46 * ss, (y - 6) * ss, 58 * ss, (y + 6) * ss], fill=INK); d.ellipse([48 * ss, (y - 4) * ss, 56 * ss, (y + 4) * ss], fill=EMBER_L)
    return im.resize((w, h), Image.LANCZOS)


# ==================== 光环（头部附件，128，近白） ====================
def halo(rot):
    ss = 4; im = Image.new('RGBA', (128 * ss, 128 * ss), (0, 0, 0, 0)); d = ImageDraw.Draw(im)
    rx, ry, cy = (24, 6, 24) if rot != 'east' else (18, 6, 24)
    cx = 64 if rot != 'east' else 60
    def ring(rx_, ry_, w, fill):
        d.ellipse([(cx - rx_) * ss, (cy - ry_) * ss, (cx + rx_) * ss, (cy + ry_) * ss], outline=fill, width=int(w * ss))
    ring(rx + 2, ry + 2, 8, INK)
    ring(rx, ry, 4, (248, 246, 240))
    d.arc([(cx - rx) * ss, (cy - ry) * ss, (cx + rx) * ss, (cy + ry) * ss], 20, 160, fill=(206, 204, 198), width=4 * ss)
    return im.resize((128, 128), Image.LANCZOS)


# ==================== 异种型、基因、种族能力（128） ====================
def aasimar_bust(I, col=(236, 214, 168)):
    I.layer(bust_mask(64, 60, 1.25), col)
    I.layer(arc_mask(64, 26, 22, 0, 360, 5), GOLD, shade=False)


def xenotype(name):
    I = Icon()
    if name == 'Aasimar':
        I.layer(union(poly_mask([(64, 70), (14, 30), (24, 74), (10, 96)]), poly_mask([(64, 70), (114, 30), (104, 74), (118, 96)])), CREAM)
        aasimar_bust(I)
    else:
        I.layer(line_mask([(80, 112), (108, 96), (114, 72)], 6), (150, 52, 60))
        I.layer(poly_mask([(110, 62), (122, 74), (106, 78)]), (150, 52, 60))
        I.layer(bust_mask(60, 58, 1.25), (168, 70, 74))
        for sx in (-1, 1): I.layer(poly_mask([(60 + sx * 8, 46), (60 + sx * 22, 20), (60 + sx * 16, 48)]), BONE_D)
    return I.done(128)


def gene(name):
    I = Icon()
    if name == 'CelestialLineage':
        I.layer(poly_mask(star_pts(64, 64, 52, 22, 6)), (238, 222, 170)); skull(I, 64, 70, .7, BONE_D)
    elif name == 'CelestialResistance':
        I.layer(poly_mask(shield_pts(64, 64, 86, 104)), SILVER); I.layer(poly_mask(star_pts(64, 60, 24, 9, 6)), GOLD)
    elif name == 'HealingHands':
        I.layer(hand(64, 76, 1.4), (236, 214, 168)); I.layer(poly_mask(star_pts(64, 30, 20, 7, 4)), (246, 236, 180), glow=GOLD)
    elif name == 'RadiantSoul':
        I.layer(union(poly_mask([(64, 64), (8, 20), (20, 70), (6, 100)]), poly_mask([(64, 64), (120, 20), (108, 70), (122, 100)])), CREAM)
        I.layer(ellipse_mask(64, 66, 16), GOLD)
    elif name == 'RadiantConsumption':
        rays(I, 64, 64, 30, 58, 12, 7, (246, 220, 140)); I.layer(ellipse_mask(64, 64, 26), EMBER_L)
    elif name == 'NecroticShroud':
        I.layer(poly_mask([(64, 8), (108, 116), (20, 116)]), (70, 70, 74)); skull(I, 64, 52, .8, BONE_D)
    elif name == 'Halo':
        I.layer(arc_mask(64, 64, 40, 0, 360, 10), GOLD, shade=False); I.layer(bust_mask(64, 74, .7), SLATE)
    elif name == 'FiendishBlood':
        I.layer(poly_mask([(64, 12), (96, 70), (64, 116), (32, 70)]), BLOOD)
        for sx in (-1, 1): I.layer(poly_mask([(64 + sx * 6, 40), (64 + sx * 26, 14), (64 + sx * 18, 44)]), BONE_D)
    elif name == 'InfernalResistance':
        I.layer(poly_mask(shield_pts(64, 64, 86, 104)), (120, 60, 56)); I.layer(flame_mask(64, 64, 44, 30), EMBER)
    elif name == 'HellishRebuke':
        I.layer(hand(52, 74, 1.1), (168, 70, 74)); I.layer(flame_mask(92, 46, 40, 30), EMBER)
    elif name == 'InfernalPact':
        I.layer(poly_mask([(30, 20), (98, 14), (104, 108), (36, 114)]), BONE)
        for y in (40, 56, 72): I.layer(line_mask([(44, y), (88, y - 2)], 3), RUST_D, outline=False, shade=False)
        I.layer(ellipse_mask(80, 92, 12), BLOOD)
    return I.done(128)


ABILITY_FROM_GENE = {'HealingHands': 'HealingHands', 'RadiantSoul': 'RadiantSoul', 'NecroticShroud': 'NecroticShroud', 'InfernalPact': 'InfernalPact', 'RadiantConsumption': 'RadiantConsumption'}
DEITIES = ['Lathander', 'Tyr', 'Mystra', 'Kelemvor', 'Malar', 'Talos', 'Bane', 'Shar', 'Surtr', 'Ilsvi', 'Vlaakith']
MIRACLES = ['Sanctuary', 'Banish', 'Consecrate', 'Dawn', 'Judgment', 'Weave', 'Rest', 'Hunt', 'Thunder', 'Dread', 'Veil', 'ForgeWrath']
RELICS = ['MorningStar', 'StormSpear', 'HandOfTyr', 'SilverTear', 'AshScales', 'BeastClaw', 'BlackHand', 'NightTear', 'ForgeHeart']
GENES = ['CelestialLineage', 'CelestialResistance', 'HealingHands', 'RadiantSoul', 'RadiantConsumption', 'NecroticShroud', 'Halo', 'FiendishBlood', 'InfernalResistance', 'HellishRebuke', 'InfernalPact']

if __name__ == '__main__':
    written = []
    for rot in ('south', 'north', 'east'): written.append(save(altar(rot), 'Altar_' + rot + '.png'))
    for rot in ('south', 'north', 'east'): written.append(save(halo(rot), 'Halo', 'Halo_' + rot + '.png'))
    written.append(save(holy_symbol(), 'HolySymbol.png'))
    written.append(save(blessed_water(), 'BlessedWater.png'))
    for n in RELICS: written.append(save(relic(n), 'Relics', n + '.png'))
    for n in MIRACLES: written.append(save(miracle(n), 'Miracles', n + '.png'))
    for n in DEITIES: written.append(save(deity(n), 'Deities', n + '.png'))
    for n in ('Aasimar', 'Tiefling'): written.append(save(xenotype(n), 'Xenotypes', n + '.png'))
    for n in GENES: written.append(save(gene(n), 'Genes', n + '.png'))
    for g, a in ABILITY_FROM_GENE.items(): written.append(save(gene(g), 'Abilities', a + '.png'))
    print(len(written), 'textures written to', OUT)
    if len(sys.argv) > 2:  # 预览拼图
        tiles = [Image.open(p).convert('RGBA') for p in written]
        cols = 10; cell = 140; rows = (len(tiles) + cols - 1) // cols
        sheet = Image.new('RGBA', (cols * cell, rows * cell), (60, 58, 54, 255))
        for i, t in enumerate(tiles):
            t.thumbnail((128, 128)); sheet.paste(t, ((i % cols) * cell + (cell - t.width) // 2, (i // cols) * cell + (cell - t.height) // 2), t)
        sheet.save(sys.argv[2]); print('preview', sys.argv[2])
