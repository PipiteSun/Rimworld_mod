# 1.67.0 新专长图标（128×128），沿用 draw_feats2_158.py / draw_class_art.py 的画风：粗黑描边、平涂 + 右下暗部、低饱和。
# 用法：python draw_feats3_167.py <Textures 根目录>
import math, sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from draw_class_art import (Icon, poly_mask, ellipse_mask, line_mask, arc_mask, rrect_mask, union, star_pts,
                            bust_mask, shield_pts, sword_masks,
                            CREAM, CREAM_D, KHAKI, KHAKI_D, SLATE, SLATE_D, RUST, RUST_D, VIOLET, VIOLET_D, GOLD, GOLD_D, BONE, BONE_D, STONE, STONE_D)

ROOT = sys.argv[1] if len(sys.argv) > 1 else 'out'
SAGE = (120, 146, 112); SAGE_D = (84, 104, 78)
BLOOD = (150, 52, 46)
DARK = (58, 52, 60)
FEY = (150, 176, 120)

def icon(name):
    I = Icon()
    if name == 'Slasher':            # 斩击者：斜劈的弯刀 + 三道划痕
        for k in range(3):
            I.layer(line_mask([(70 + k * 12, 20), (112 + k * 4, 70 + k * 10)], 5), BLOOD)
        for m, c in sword_masks(52, 70, -45, 104, CREAM, RUST):
            I.layer(m, c)
    elif name == 'Piercer':          # 穿刺者：长矛刺穿靶心
        I.layer(ellipse_mask(78, 54, 38), BONE)
        I.layer(ellipse_mask(78, 54, 24), BLOOD)
        I.layer(ellipse_mask(78, 54, 10), BONE)
        I.layer(line_mask([(10, 118), (70, 62)], 8), RUST_D)
        I.layer(poly_mask([(64, 54), (96, 30), (78, 70)]), SLATE)
    elif name == 'Crusher':          # 粉碎者：战锤砸下 + 裂纹
        I.layer(poly_mask([(18, 112), (110, 112), (98, 92), (30, 92)]), STONE)
        I.layer(line_mask([(64, 92), (52, 104), (60, 112)], 3), STONE_D, outline=False, shade=False)
        I.layer(line_mask([(64, 92), (80, 106)], 3), STONE_D, outline=False, shade=False)
        I.layer(line_mask([(98, 14), (64, 70)], 9), RUST_D)
        I.layer(rrect_mask(34, 56, 90, 88, 6), SLATE)
    elif name == 'MageSlayer':       # 法师杀手：剑劈开法球
        I.layer(ellipse_mask(64, 64, 46), VIOLET)
        I.layer(poly_mask(star_pts(64, 64, 20, 8, 4)), VIOLET_D, outline=False)
        for m, c in sword_masks(64, 64, -60, 120, CREAM, RUST):
            I.layer(m, c)
    elif name == 'DefensiveDuelist': # 防御决斗者：细剑交叉格挡
        for m, c in sword_masks(60, 66, -125, 112, CREAM, GOLD_D):
            I.layer(m, c)
        for m, c in sword_masks(68, 66, -55, 112, SLATE, RUST):
            I.layer(m, c)
        I.layer(poly_mask(star_pts(64, 40, 12, 5, 4)), GOLD, outline=False)
    elif name == 'WeaponMaster':     # 武器大师：剑、斧、弓交叠
        I.layer(arc_mask(64, 74, 46, 200, 340, 6), RUST_D)
        I.layer(line_mask([(22, 58), (106, 58)], 2), BONE, outline=False, shade=False)
        I.layer(line_mask([(36, 112), (84, 30)], 7), RUST_D)
        I.layer(poly_mask([(76, 28), (102, 20), (98, 52), (84, 46)]), SLATE)
        for m, c in sword_masks(64, 76, -110, 100, CREAM, GOLD_D):
            I.layer(m, c)
    elif name == 'Durable':          # 耐久：缠着绷带的盾 + 心
        I.layer(poly_mask(shield_pts(64, 64, 84, 100)), KHAKI)
        I.layer(line_mask([(24, 50), (104, 80)], 10), BONE)
        I.layer(ellipse_mask(52, 62, 12), BLOOD, outline=False)
        I.layer(ellipse_mask(74, 62, 12), BLOOD, outline=False)
        I.layer(poly_mask([(41, 66), (85, 66), (63, 92)]), BLOOD, outline=False)
    elif name == 'FeyTouched':       # 妖精之触：蝴蝶翅膀 + 迷踪步的残影
        for x in (26, 46):
            I.layer(ellipse_mask(x, 92, 8, 4), SAGE_D, outline=False, shade=False)
        I.layer(ellipse_mask(48, 50, 22, 30), FEY)
        I.layer(ellipse_mask(80, 50, 22, 30), FEY)
        I.layer(ellipse_mask(52, 86, 14, 18), SAGE)
        I.layer(ellipse_mask(76, 86, 14, 18), SAGE)
        I.layer(rrect_mask(60, 36, 68, 104, 4), DARK)
        I.layer(poly_mask(star_pts(100, 22, 12, 5, 4)), GOLD, outline=False)
    return I.done(128)

FEATS = ['Slasher', 'Piercer', 'Crusher', 'MageSlayer', 'DefensiveDuelist', 'WeaponMaster', 'Durable', 'FeyTouched']

if __name__ == '__main__':
    fd = os.path.join(ROOT, 'LP', 'Feats'); os.makedirs(fd, exist_ok=True)
    for n in FEATS:
        icon(n).save(os.path.join(fd, n + '.png'))
    print('feat icons written:', len(FEATS))
