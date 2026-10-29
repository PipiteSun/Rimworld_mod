# 1.58.1 专长第二批 + 种族专长图标（128×128），沿用 draw_feats.py / draw_class_art.py 的画风：粗黑描边、平涂 + 右下暗部、低饱和。
# 另画厨师的“点心”物品图。用法：python draw_feats2_158.py <Textures 根目录>
import math, sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from draw_class_art import (Icon, poly_mask, ellipse_mask, line_mask, arc_mask, rrect_mask, union, subtract, star_pts,
                            bust_mask, shield_pts, sword_masks,
                            CREAM, CREAM_D, KHAKI, KHAKI_D, SLATE, SLATE_D, RUST, RUST_D, VIOLET, VIOLET_D, GOLD, GOLD_D, BONE, BONE_D, STONE, STONE_D)

ROOT = sys.argv[1] if len(sys.argv) > 1 else 'out'
SAGE = (120, 146, 112); SAGE_D = (84, 104, 78)
TEAL = (98, 140, 146)
EMBER = (196, 104, 56)
ICE = (168, 196, 210)
ACID = (150, 170, 70)
DARK = (58, 52, 60)

def flame(I, cx, cy, s, col=EMBER):
    I.layer(poly_mask([(cx, cy - 34 * s), (cx + 18 * s, cy - 4 * s), (cx + 14 * s, cy + 18 * s), (cx, cy + 26 * s), (cx - 14 * s, cy + 18 * s), (cx - 18 * s, cy - 4 * s)]), col)
    I.layer(poly_mask([(cx, cy - 10 * s), (cx + 8 * s, cy + 8 * s), (cx, cy + 20 * s), (cx - 8 * s, cy + 8 * s)]), GOLD, outline=False)

def element(I, kind):
    if kind == 'Fire':
        flame(I, 64, 70, 1.6)
    elif kind == 'Cold':
        for a in range(0, 180, 60):
            r = math.radians(a)
            I.layer(line_mask([(64 - 46 * math.cos(r), 64 - 46 * math.sin(r)), (64 + 46 * math.cos(r), 64 + 46 * math.sin(r))], 9), ICE)
        I.layer(ellipse_mask(64, 64, 12), BONE)
    elif kind == 'Lightning':
        I.layer(poly_mask([(72, 10), (34, 70), (60, 70), (48, 118), (94, 52), (66, 52), (82, 10)]), GOLD)
    else:
        I.layer(poly_mask([(64, 14), (96, 70), (90, 96), (64, 112), (38, 96), (32, 70)]), ACID)
        I.layer(ellipse_mask(52, 80, 9), CREAM, outline=False)
        I.layer(ellipse_mask(100, 104, 8), ACID)

def icon(name):
    I = Icon()
    if name == 'MagicInitiate':      # 魔法学徒：翻开的书 + 小星火
        I.layer(poly_mask([(14, 46), (62, 38), (62, 106), (14, 112)]), CREAM)
        I.layer(poly_mask([(66, 38), (114, 46), (114, 112), (66, 106)]), CREAM_D)
        I.layer(poly_mask(star_pts(64, 26, 18, 7, 4)), VIOLET, glow=VIOLET)
    elif name == 'TavernBrawler':    # 酒馆斗殴者：拳头 + 酒杯
        I.layer(rrect_mask(70, 46, 112, 104, 8), KHAKI)
        I.layer(rrect_mask(112, 58, 122, 88, 4), KHAKI_D)
        I.layer(rrect_mask(72, 40, 110, 52, 4), BONE)
        I.layer(rrect_mask(14, 40, 64, 92, 14), RUST)
        for x in (24, 36, 48):
            I.layer(line_mask([(x, 46), (x, 66)], 3), RUST_D, outline=False, shade=False)
    elif name == 'Crafter':          # 工匠：锤子 + 齿轮
        I.layer(ellipse_mask(80, 78, 30), STONE)
        I.layer(ellipse_mask(80, 78, 12), STONE_D)
        I.layer(line_mask([(24, 112), (64, 50)], 9), RUST_D)
        I.layer(rrect_mask(46, 22, 92, 46, 5), SLATE)
    elif name == 'Skilled':          # 技能专家：三颗星
        for cx, cy in ((34, 82), (64, 40), (94, 82)):
            I.layer(poly_mask(star_pts(cx, cy, 26, 11, 5)), GOLD)
    elif name == 'Musician':         # 音乐家：鲁特琴
        I.layer(ellipse_mask(52, 80, 30, 34), KHAKI)
        I.layer(ellipse_mask(52, 80, 9), KHAKI_D)
        I.layer(line_mask([(66, 60), (106, 18)], 9), RUST_D)
        I.layer(poly_mask(star_pts(104, 64, 10, 4, 4)), CREAM)
    elif name.startswith('ElementalAdept'):  # 元素使：法球里的元素
        I.layer(ellipse_mask(64, 64, 54), VIOLET_D)
        element(I, name.split('_')[1])
    elif name == 'Observant':        # 观察者：眼 + 放大镜
        I.layer(poly_mask([(10, 60), (40, 36), (88, 36), (118, 60), (88, 84), (40, 84)]), BONE)
        I.layer(ellipse_mask(64, 60, 18), TEAL)
        I.layer(ellipse_mask(64, 60, 7), (40, 38, 34), shade=False)
        I.layer(arc_mask(92, 92, 18, 0, 360, 6), SLATE)
        I.layer(line_mask([(104, 104), (120, 120)], 8), RUST_D)
    elif name == 'Actor':            # 演员：面具
        I.layer(poly_mask([(20, 30), (108, 30), (100, 82), (64, 110), (28, 82)]), BONE)
        I.layer(ellipse_mask(46, 56, 10, 6), DARK, shade=False)
        I.layer(ellipse_mask(82, 56, 10, 6), DARK, shade=False)
        I.layer(arc_mask(64, 70, 20, 20, 160, 5), RUST_D, shade=False)
    elif name == 'Telepathic':       # 心灵感应：两个头像之间的波纹
        I.layer(bust_mask(30, 70, 0.6), SLATE)
        I.layer(bust_mask(98, 70, 0.6), SLATE)
        for r in (12, 24, 36):
            I.layer(arc_mask(64, 50, r, 200, 340, 4), VIOLET)
    elif name == 'Resilient':        # 坚毅心智：盾牌里的头像
        I.layer(poly_mask(shield_pts(64, 64, 88, 104)), SLATE)
        I.layer(bust_mask(64, 66, 0.7), CREAM)
    elif name == 'Chef':             # 厨师：锅勺 + 蒸汽
        I.layer(rrect_mask(18, 62, 96, 104, 12), STONE)
        I.layer(line_mask([(96, 70), (122, 62)], 7), RUST_D)
        for x in (38, 58, 78):
            I.layer(arc_mask(x, 44, 10, 180, 360, 4), CREAM, outline=False)
    elif name == 'Poisoner':         # 毒师：滴毒的匕首
        for m, c in sword_masks(56, 60, -45, L=48, blade_col=SLATE, hilt_col=RUST):
            I.layer(m, c)
        I.layer(ellipse_mask(96, 98, 10, 14), ACID)
    elif name == 'RitualCaster':     # 仪式施法者：法阵
        I.layer(arc_mask(64, 64, 50, 0, 360, 6), VIOLET)
        I.layer(poly_mask(star_pts(64, 64, 44, 18, 5)), VIOLET_D)
        I.layer(ellipse_mask(64, 64, 10), CREAM, glow=VIOLET)
    elif name == 'DwarvenFortitude':  # 矮人坚毅：酒杯 + 铁砧
        I.layer(rrect_mask(20, 86, 108, 108, 4), STONE_D)
        I.layer(rrect_mask(34, 72, 94, 88, 4), STONE)
        I.layer(rrect_mask(44, 20, 84, 66, 6), KHAKI)
        I.layer(rrect_mask(42, 14, 86, 26, 4), BONE)
    elif name == 'DragonFear':       # 龙威：龙眼
        I.layer(poly_mask([(8, 64), (40, 30), (88, 30), (120, 64), (88, 98), (40, 98)]), GOLD)
        I.layer(poly_mask([(64, 32), (74, 64), (64, 96), (54, 64)]), DARK, shade=False)
    elif name == 'InfernalWrath':    # 炼狱之怒：犄角 + 火
        I.layer(poly_mask([(24, 110), (14, 40), (44, 86)]), RUST_D)
        I.layer(poly_mask([(104, 110), (114, 40), (84, 86)]), RUST_D)
        flame(I, 64, 72, 1.3)
    elif name == 'ElvenAccuracy':    # 精灵精准：叶形箭头 + 准环
        I.layer(arc_mask(64, 64, 46, 0, 360, 6), SAGE)
        I.layer(line_mask([(20, 108), (88, 40)], 6), RUST_D)
        I.layer(poly_mask([(84, 44), (116, 12), (104, 52)]), SAGE_D)
        I.layer(poly_mask([(84, 44), (116, 12), (76, 24)]), SAGE)
    elif name == 'MagmaGrip':        # 熔岩之握：发红的拳头
        I.layer(rrect_mask(28, 40, 100, 108, 18), RUST)
        for x in (44, 64, 84):
            I.layer(line_mask([(x, 48), (x, 74)], 4), EMBER, outline=False, shade=False)
        flame(I, 64, 26, 0.6)
    elif name == 'BloodRegen':       # 嗜血再生：血滴 + 爪痕
        I.layer(poly_mask([(64, 12), (96, 70), (90, 96), (64, 112), (38, 96), (32, 70)]), RUST)
        for x in (48, 64, 80):
            I.layer(line_mask([(x - 8, 56), (x + 8, 96)], 5), SAGE, outline=False)
    elif name == 'PhylacteryOath':   # 命匣之誓：命匣 + 锁链
        I.layer(rrect_mask(34, 30, 94, 104, 8), VIOLET_D)
        I.layer(poly_mask(star_pts(64, 66, 20, 8, 4)), SAGE, glow=SAGE)
        I.layer(arc_mask(64, 30, 20, 180, 360, 6), STONE)
    elif name == 'BountifulLuck':    # 好运连连：两片四叶草
        for cx, cy, r in ((48, 58, 16), (88, 84, 12)):
            for dx, dy in ((-1, 0), (1, 0), (0, -1), (0, 1)):
                I.layer(ellipse_mask(cx + dx * r * 0.8, cy + dy * r * 0.8, r * 0.7), SAGE)
        I.layer(line_mask([(48, 70), (40, 112)], 4), SAGE_D)
    elif name == 'GnomishCunning':   # 侏儒机巧：齿轮 + 尖帽
        I.layer(ellipse_mask(64, 82, 32), STONE)
        I.layer(ellipse_mask(64, 82, 12), STONE_D)
        I.layer(poly_mask([(40, 52), (64, 8), (88, 52)]), RUST)
    elif name == 'Snack':            # 点心物品：糕点
        I.layer(ellipse_mask(64, 80, 46, 24), KHAKI)
        I.layer(ellipse_mask(64, 66, 40, 20), CREAM)
        I.layer(ellipse_mask(64, 52, 10), RUST)
    return I.done(128)

FEATS = ['MagicInitiate', 'TavernBrawler', 'Crafter', 'Skilled', 'Musician', 'ElementalAdept_Fire', 'ElementalAdept_Cold', 'ElementalAdept_Lightning',
         'ElementalAdept_Acid', 'Observant', 'Actor', 'Telepathic', 'Resilient', 'Chef', 'Poisoner', 'RitualCaster',
         'DwarvenFortitude', 'DragonFear', 'InfernalWrath', 'ElvenAccuracy', 'MagmaGrip', 'BloodRegen', 'PhylacteryOath', 'BountifulLuck', 'GnomishCunning']

if __name__ == '__main__':
    fd = os.path.join(ROOT, 'LP', 'Feats'); os.makedirs(fd, exist_ok=True)
    for n in FEATS:
        icon(n).save(os.path.join(fd, n + '.png'))
    idir = os.path.join(ROOT, 'LP', 'Items'); os.makedirs(idir, exist_ok=True)
    icon('Snack').save(os.path.join(idir, 'FeatSnack.png'))
    print('feat icons written:', len(FEATS))
