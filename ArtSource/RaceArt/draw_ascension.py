# 1.40.0 成神贴图（RimWorld 画风：粗墨线、平涂 + 右下暗部、低饱和；画法和调色沿用 draw_class_art.py / draw_faith.py）。
# 用法：python -I ArtSource/RaceArt/draw_ascension.py <模组根目录> [预览图输出路径]
# 输出到 Textures/Ascension：22 个神职能力图标、6 件物品（神性火花、篡夺的神性火花、圣徒圣印、永夜冠冕、天穹之矛、诸神之祸，武器图同时当手持贴图）、
#   4 个界面图标（成神之路、半神、众神的试炼、升入神域）、命匣神龛（在原命匣图上加金光和神纹）、觉醒泰坦的神辉（头部附件，南/北/东）、
#   觉醒泰坦异种型图标、觉醒神血基因图标。
import math, os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from PIL import Image, ImageDraw, ImageFilter
from draw_class_art import (Icon, poly_mask, ellipse_mask, line_mask, arc_mask, rrect_mask, union, subtract, star_pts,
                            bust_mask, shield_pts, sword_masks, darker, INK, CREAM, CREAM_D, KHAKI, KHAKI_D, SLATE, SLATE_D,
                            RUST, RUST_D, VIOLET, VIOLET_D, GOLD, GOLD_D, BONE, BONE_D, STONE, STONE_D)
from draw_faith import (rays, bolt, hand, scales, skull, flame_mask, sun, SILVER, SILVER_D, DAWN, DAWN_L, SKY, NIGHT, NIGHT_D,
                        BLOOD, EMBER, EMBER_L, GREEN, ASH)

ROOT = sys.argv[1] if len(sys.argv) > 1 else os.getcwd()
OUT = os.path.join(ROOT, 'Textures', 'Ascension')
SPARK = (246, 222, 140); SPARK_L = (252, 242, 200)
USURP = (176, 60, 50); USURP_L = (222, 120, 90)
WOLF = (128, 116, 104); WILD = (120, 92, 60)
MIND = (178, 130, 186); FOG = (150, 146, 170)
UNDEAD = (120, 200, 150)


def save(img, *path):
    p = os.path.join(OUT, *path)
    os.makedirs(os.path.dirname(p), exist_ok=True)
    img.save(p)
    return p


def spark_shape(I, cx, cy, r, col, core, glow):
    I.layer(poly_mask(star_pts(cx, cy, r, r * .42, 4)), col, glow=glow)
    I.layer(poly_mask(star_pts(cx, cy, r * .62, r * .26, 4, rot=-math.pi / 4)), darker(col, .9), shade=False)
    I.layer(ellipse_mask(cx, cy, r * .22), core, shade=False)


def wolf_head(I, cx, cy, sc, col):
    head = poly_mask([(cx - 26 * sc, cy - 6 * sc), (cx - 20 * sc, cy - 34 * sc), (cx - 8 * sc, cy - 16 * sc), (cx + 8 * sc, cy - 16 * sc), (cx + 20 * sc, cy - 34 * sc),
                      (cx + 26 * sc, cy - 6 * sc), (cx + 14 * sc, cy + 24 * sc), (cx, cy + 32 * sc), (cx - 14 * sc, cy + 24 * sc)])
    I.layer(head, col)
    I.layer(union(ellipse_mask(cx - 10 * sc, cy, 4 * sc), ellipse_mask(cx + 10 * sc, cy, 4 * sc)), (230, 200, 90), outline=False, shade=False)


# ==================== 神职能力（128） ====================
def ability(name):
    I = Icon()
    if name == 'DawnLight':
        I.layer(rrect_mask(6, 86, 122, 120, 6), KHAKI_D); sun(I, 64, 86, 30, half=True)
        for x in (30, 98): I.layer(poly_mask(star_pts(x, 40, 12, 4, 4)), SPARK_L)
    elif name == 'UnfadingDawn':
        I.layer(poly_mask(shield_pts(64, 66, 80, 98)), DAWN_L)
        sun(I, 64, 64, 16)
    elif name == 'JudgmentHammer':
        I.layer(line_mask([(28, 112), (72, 58)], 11), RUST)
        I.layer(poly_mask([(52, 22), (108, 50), (94, 80), (38, 52)]), SILVER)
        I.layer(bolt(98, 98, .3), SPARK, glow=GOLD)
    elif name == 'JusticeShield':
        I.layer(poly_mask(shield_pts(64, 64, 90, 108)), SILVER)
        scales(I, 64, 62, .55, GOLD, GOLD_D)
    elif name == 'Dominate':
        I.layer(bust_mask(64, 72, 1.05), SLATE)
        I.layer(hand(64, 30, .85), (52, 44, 58))
        for r in (40, 50): I.layer(arc_mask(64, 64, r, 200, 340, 4), (150, 80, 90), shade=False)
    elif name == 'CrownOfDread':
        I.layer(poly_mask([(18, 92), (24, 40), (44, 66), (64, 26), (84, 66), (104, 40), (110, 92)]), (60, 52, 62))
        I.layer(rrect_mask(18, 88, 110, 104, 4), (90, 70, 80))
        for x in (24, 64, 104): I.layer(ellipse_mask(x, 36 if x != 64 else 24, 6), (150, 60, 70), shade=False)
    elif name == 'ThunderWrath':
        I.layer(ellipse_mask(64, 22, 58, 18), SLATE_D)
        for dx in (-30, 0, 30): I.layer(bolt(64 + dx, 72, .62), (240, 232, 160), glow=SKY)
    elif name == 'CallWind':
        for i, y in enumerate((34, 60, 86)):
            I.layer(line_mask([(14 + i * 6, y), (60, y - 6), (96, y + 4), (112 - i * 8, y - 8)], 9), (190, 206, 224))
        I.layer(arc_mask(104, 44, 14, 90, 360, 7), (190, 206, 224), shade=False)
    elif name == 'Meteor':
        I.layer(line_mask([(24, 18), (66, 60)], 16), EMBER_L, shade=False)
        I.layer(ellipse_mask(78, 74, 26), EMBER)
        I.layer(ellipse_mask(74, 70, 12), (250, 214, 130), shade=False)
    elif name == 'ForgeBlessing':
        I.layer(rrect_mask(18, 84, 110, 104, 6), STONE_D); I.layer(rrect_mask(30, 70, 98, 88, 6), STONE)
        I.layer(flame_mask(64, 42, 56, 34), EMBER)
        for m, c in sword_masks(64, 52, -90, L=70, blade_col=EMBER_L, hilt_col=RUST_D): I.layer(m, c)
    elif name == 'PackCall':
        wolf_head(I, 40, 76, .8, WOLF); wolf_head(I, 88, 76, .8, WOLF); wolf_head(I, 64, 54, 1.0, darker(WOLF, .9))
    elif name == 'PredatorForm':
        I.layer(poly_mask([(20, 98), (36, 30), (52, 60), (64, 18), (76, 60), (92, 30), (108, 98)]), WILD)
        for dx in (-22, 0, 22): I.layer(poly_mask([(64 + dx - 6, 64), (64 + dx + 6, 64), (64 + dx, 112)]), BONE)
    elif name == 'PsionicBlast':
        for r in (20, 36, 52): I.layer(arc_mask(64, 64, r, 0, 360, 6), MIND, shade=False)
        I.layer(ellipse_mask(64, 64, 12), (226, 196, 230))
    elif name == 'WillCrush':
        I.layer(ellipse_mask(64, 54, 34, 28), (210, 168, 200))
        I.layer(line_mask([(46, 40), (60, 58), (52, 70), (72, 86)], 5), INK, outline=False, shade=False)
        I.layer(hand(64, 98, .7), (64, 54, 70))
    elif name == 'SoulRest':
        I.layer(rrect_mask(14, 74, 114, 112, 10), STONE)
        I.layer(poly_mask([(64, 12), (82, 46), (64, 76), (46, 46)]), (214, 218, 226), glow=SKY)
    elif name == 'JudgmentGate':
        I.layer(union(rrect_mask(20, 18, 108, 116, 40), rrect_mask(20, 60, 108, 116, 0)), STONE)
        I.layer(union(rrect_mask(36, 34, 92, 116, 28), rrect_mask(36, 60, 92, 116, 0)), (34, 30, 40), shade=False)
        scales(I, 64, 78, .5, SILVER, ASH)
    elif name == 'WeaveMissiles':
        for i, (x, y) in enumerate(((26, 96), (44, 70), (64, 48), (84, 30), (104, 16))):
            I.layer(line_mask([(x - 14, y + 14), (x, y)], 5), SKY, shade=False)
            I.layer(poly_mask(star_pts(x, y, 11, 4, 4)), (214, 222, 240), glow=SKY)
    elif name == 'Antimagic':
        I.layer(ellipse_mask(64, 64, 48), (70, 80, 110))
        I.layer(poly_mask(star_pts(64, 64, 34, 14, 7)), (180, 196, 230))
        I.layer(line_mask([(24, 104), (104, 24)], 10), (170, 60, 56))
    elif name == 'OblivionFog':
        for x, y, r in ((40, 70, 28), (72, 58, 32), (92, 80, 24), (58, 88, 26)): I.layer(ellipse_mask(x, y, r), FOG)
        I.layer(union(ellipse_mask(56, 66, 4), ellipse_mask(76, 66, 4)), (230, 220, 250), outline=False, shade=False)
    elif name == 'ShadowStep':
        I.layer(poly_mask([(34, 112), (44, 46), (60, 22), (64, 50), (58, 112)]), NIGHT_D)
        I.layer(poly_mask([(70, 112), (80, 46), (96, 22), (100, 50), (94, 112)]), NIGHT)
        I.layer(line_mask([(30, 30), (54, 30)], 4), (150, 120, 190), shade=False)
    elif name == 'SoulHarvest':
        I.layer(arc_mask(64, 70, 46, 190, 330, 10), SILVER)
        I.layer(line_mask([(20, 116), (100, 32)], 8), RUST_D)
        for x, y in ((40, 46), (64, 36), (88, 52)): I.layer(ellipse_mask(x, y, 8, 12), UNDEAD, glow=UNDEAD)
    elif name == 'DeadArmy':
        I.layer(rrect_mask(8, 98, 120, 122, 6), STONE_D)
        for dx, sc in ((-34, .7), (34, .7), (0, .9)): skull(I, 64 + dx, 70 - (10 if dx == 0 else 0), sc, BONE)
    return I.done(128)


# ==================== 物品（128；武器同时是手持贴图） ====================
def item(name):
    I = Icon()
    if name == 'DivineSpark':
        spark_shape(I, 64, 64, 50, SPARK, SPARK_L, GOLD)
    elif name == 'UsurpedSpark':
        spark_shape(I, 64, 64, 50, USURP, USURP_L, (120, 30, 30))
    elif name == 'SaintSeal':
        I.layer(arc_mask(64, 70, 30, 0, 360, 12), GOLD)
        I.layer(ellipse_mask(64, 40, 20, 16), SILVER)
        I.layer(poly_mask(star_pts(64, 40, 12, 5, 6)), GOLD_D, shade=False)
    elif name == 'EternalNightCrown':
        I.layer(poly_mask([(14, 92), (20, 36), (40, 62), (52, 24), (64, 56), (76, 24), (88, 62), (108, 36), (114, 92)]), BONE_D)
        I.layer(rrect_mask(14, 84, 114, 104, 5), (60, 52, 66))
        for x in (34, 64, 94): I.layer(ellipse_mask(x, 94, 6), UNDEAD, shade=False, glow=UNDEAD)
    elif name == 'SkySpear':
        I.layer(line_mask([(14, 118), (90, 42)], 8), (200, 170, 110))
        I.layer(poly_mask([(84, 36), (118, 6), (106, 50)]), (232, 226, 210))
        I.layer(rrect_mask(80, 40, 96, 56, 3), GOLD)
        I.layer(poly_mask(star_pts(104, 26, 10, 3, 4)), SPARK_L, glow=GOLD)
    elif name == 'Godsbane':
        for m, c in sword_masks(64, 64, -45, L=118, blade_col=(184, 178, 186), hilt_col=(70, 36, 34)): I.layer(m, c)
        I.layer(line_mask([(54, 74), (98, 30)], 3), USURP_L, outline=False, shade=False)
    return I.done(128)


# ==================== 界面图标（128） ====================
def ui(name):
    I = Icon()
    if name == 'PathOfAscension':
        I.layer(poly_mask([(20, 116), (52, 116), (64, 30), (76, 116), (108, 116), (70, 14), (58, 14)]), STONE)
        spark_shape(I, 64, 22, 18, SPARK, SPARK_L, GOLD)
    elif name == 'Demigod':
        I.layer(bust_mask(64, 74, 1.1), (214, 190, 150))
        I.layer(arc_mask(64, 30, 26, 0, 360, 7), GOLD, shade=False)
        spark_shape(I, 64, 70, 12, SPARK, SPARK_L, GOLD)
    elif name == 'Council':
        for x, c in ((26, DAWN), (64, SKY), (102, BLOOD)):
            I.layer(poly_mask(star_pts(x, 40, 18, 7, 6)), c)
        I.layer(rrect_mask(14, 72, 114, 112, 8), STONE)
        I.layer(poly_mask(star_pts(64, 92, 14, 5, 4)), SPARK_L)
    elif name == 'Realm':
        I.layer(rrect_mask(46, 8, 82, 124, 10), SPARK, glow=GOLD)
        I.layer(rrect_mask(56, 8, 72, 124, 6), SPARK_L, shade=False, outline=False)
        I.layer(ellipse_mask(64, 112, 50, 12), KHAKI_D)
    return I.done(128)


def xenotype():
    I = Icon()
    I.layer(bust_mask(64, 62, 1.3), (206, 164, 96))
    I.layer(arc_mask(64, 24, 24, 0, 360, 6), GOLD, shade=False)
    I.layer(arc_mask(64, 24, 34, 200, 340, 4), SPARK_L, shade=False)
    return I.done(128)


def gene():
    I = Icon()
    I.layer(poly_mask([(64, 10), (96, 72), (64, 118), (32, 72)]), (196, 150, 90))
    spark_shape(I, 64, 72, 22, SPARK, SPARK_L, GOLD)
    return I.done(128)


# ==================== 神辉（头部附件，128，金色；比泰坦原有光环大一圈） ====================
def halo(rot):
    ss = 4; im = Image.new('RGBA', (128 * ss, 128 * ss), (0, 0, 0, 0)); d = ImageDraw.Draw(im)
    rx, ry, cy = (32, 9, 20) if rot != 'east' else (24, 9, 20)
    cx = 64 if rot != 'east' else 60
    def ring(rx_, ry_, w, fill):
        d.ellipse([(cx - rx_) * ss, (cy - ry_) * ss, (cx + rx_) * ss, (cy + ry_) * ss], outline=fill, width=int(w * ss))
    glow = Image.new('RGBA', im.size, (0, 0, 0, 0)); gd = ImageDraw.Draw(glow)
    gd.ellipse([(cx - rx - 6) * ss, (cy - ry - 6) * ss, (cx + rx + 6) * ss, (cy + ry + 6) * ss], outline=(250, 226, 150, 110), width=10 * ss)
    im.alpha_composite(glow.filter(ImageFilter.GaussianBlur(6 * ss)))
    ring(rx + 2, ry + 2, 7, INK)
    ring(rx, ry, 3, SPARK)
    d.arc([(cx - rx) * ss, (cy - ry) * ss, (cx + rx) * ss, (cy + ry) * ss], 20, 160, fill=GOLD_D, width=3 * ss)
    for a in range(0, 360, 60):
        r = math.radians(a); x = cx + rx * math.cos(r); y = cy + ry * math.sin(r)
        d.ellipse([(x - 3) * ss, (y - 3) * ss, (x + 3) * ss, (y + 3) * ss], fill=INK); d.ellipse([(x - 2) * ss, (y - 2) * ss, (x + 2) * ss, (y + 2) * ss], fill=SPARK_L)
    return im.resize((128, 128), Image.LANCZOS)


# ==================== 命匣神龛（原命匣图 + 金光 + 神纹） ====================
def shrine():
    src = Image.open(os.path.join(ROOT, 'Textures', 'Necromancy', 'Phylactery.png')).convert('RGBA').resize((512, 512), Image.LANCZOS)
    alpha = src.split()[3]
    # 金色外光：把轮廓模糊后染金，垫在原图下面。
    glow = Image.new('RGBA', src.size, (250, 214, 120, 0)); glow.putalpha(alpha.filter(ImageFilter.MaxFilter(21)).filter(ImageFilter.GaussianBlur(14)).point(lambda v: int(v * .8)))
    out = Image.new('RGBA', src.size, (0, 0, 0, 0)); out.alpha_composite(glow); out.alpha_composite(src)
    # 整体偏一点金色（只在原图不透明处）。
    tint = Image.new('RGBA', src.size, (240, 200, 110, 0)); tint.putalpha(alpha.point(lambda v: int(v * .22))); out.alpha_composite(tint)
    # 神纹：一圈金色小符号。
    d = ImageDraw.Draw(out)
    bbox = alpha.getbbox() or (0, 0, 512, 512); cx = (bbox[0] + bbox[2]) / 2; cy = (bbox[1] + bbox[3]) / 2; r = min(bbox[2] - bbox[0], bbox[3] - bbox[1]) * .36
    for i in range(8):
        a = math.radians(i * 45 + 22.5); x = cx + r * math.cos(a); y = cy + r * .8 * math.sin(a)
        pts = [(x + 9 * math.cos(math.radians(k * 90)), y + 9 * math.sin(math.radians(k * 90))) for k in range(4)]
        d.polygon(pts, fill=INK); d.polygon([(x + 6 * math.cos(math.radians(k * 90)), y + 6 * math.sin(math.radians(k * 90))) for k in range(4)], fill=(250, 224, 140))
    return out


def preview(paths, dest):
    ims = [Image.open(p).convert('RGBA').resize((96, 96), Image.LANCZOS) for p in paths]
    cols = 10; rows = (len(ims) + cols - 1) // cols
    sheet = Image.new('RGBA', (cols * 104 + 8, rows * 104 + 8), (60, 60, 58, 255))
    for i, im in enumerate(ims): sheet.alpha_composite(im, (8 + (i % cols) * 104, 8 + (i // cols) * 104))
    sheet.save(dest)


if __name__ == '__main__':
    made = []
    for n in ['DawnLight', 'UnfadingDawn', 'JudgmentHammer', 'JusticeShield', 'Dominate', 'CrownOfDread', 'ThunderWrath', 'CallWind', 'Meteor', 'ForgeBlessing', 'PackCall',
              'PredatorForm', 'PsionicBlast', 'WillCrush', 'SoulRest', 'JudgmentGate', 'WeaveMissiles', 'Antimagic', 'OblivionFog', 'ShadowStep', 'SoulHarvest', 'DeadArmy']:
        made.append(save(ability(n), 'Abilities', n + '.png'))
    for n in ['DivineSpark', 'UsurpedSpark', 'SaintSeal', 'EternalNightCrown']:
        made.append(save(item(n), 'Items', n + '.png'))
    for n in ['SkySpear', 'Godsbane']:
        made.append(save(item(n), 'Weapons', n + '.png'))
    for n in ['PathOfAscension', 'Demigod', 'Council', 'Realm']:
        made.append(save(ui(n), 'UI', n + '.png'))
    made.append(save(xenotype(), 'Xenotypes', 'AwakenedTitan.png'))
    made.append(save(gene(), 'Genes', 'DivineBlood.png'))
    for rot in ('south', 'north', 'east'):
        made.append(save(halo(rot), 'Halo', 'DivineHalo_' + rot + '.png'))
    made.append(save(shrine(), 'PhylacteryShrine.png'))
    print('wrote', len(made), 'textures to', OUT)
    if len(sys.argv) > 2: preview(made, sys.argv[2]); print('preview', sys.argv[2])
