# 1.58.3 持续成长：6 本属性典籍（同一书形换颜色和徽记）和 4 件传承武器，RimWorld 手绘风（粗描边、平涂 + 右下暗部、低饱和）。
# 用法：python draw_growth_158.py <Textures 根目录>
import math, sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from draw_class_art import (Icon, poly_mask, ellipse_mask, line_mask, arc_mask, rrect_mask, union, star_pts, sword_masks, shield_pts,
                            CREAM, CREAM_D, KHAKI, KHAKI_D, SLATE, SLATE_D, RUST, RUST_D, VIOLET, VIOLET_D, GOLD, GOLD_D, BONE, BONE_D, STONE, STONE_D)

ROOT = sys.argv[1] if len(sys.argv) > 1 else 'out'
SAGE = (120, 146, 112); TEAL = (98, 140, 146); ROSE = (168, 98, 110); OCHRE = (176, 132, 70)
TOME = {'STR': (RUST, 'fist'), 'DEX': (SAGE, 'feather'), 'CON': (OCHRE, 'shield'), 'INT': (SLATE_D, 'eye'), 'WIS': (TEAL, 'moon'), 'CHA': (ROSE, 'star')}

def tome(key):
    col, mark = TOME[key]
    I = Icon()
    I.layer(rrect_mask(26, 16, 106, 116, 6), BONE_D)              # 书页侧边
    I.layer(rrect_mask(20, 12, 100, 112, 8), col)                 # 封面
    I.layer(rrect_mask(20, 12, 34, 112, 4), tuple(int(c * .7) for c in col))  # 书脊
    I.layer(arc_mask(66, 60, 26, 0, 360, 4), GOLD, outline=False)
    if mark == 'fist':
        I.layer(rrect_mask(52, 46, 80, 74, 7), GOLD)
    elif mark == 'feather':
        I.layer(poly_mask([(54, 80), (78, 38), (82, 44), (60, 82)]), GOLD)
    elif mark == 'shield':
        I.layer(poly_mask(shield_pts(66, 60, 28, 34)), GOLD)
    elif mark == 'eye':
        I.layer(poly_mask([(44, 60), (56, 50), (76, 50), (88, 60), (76, 70), (56, 70)]), GOLD)
        I.layer(ellipse_mask(66, 60, 6), (40, 36, 32), shade=False)
    elif mark == 'moon':
        I.layer(ellipse_mask(66, 60, 16), GOLD)
        I.layer(ellipse_mask(74, 54, 13), col, outline=False, shade=False)
    else:
        I.layer(poly_mask(star_pts(66, 60, 18, 7, 5)), GOLD)
    I.layer(rrect_mask(40, 94, 92, 100, 2), GOLD_D, outline=False)
    return I.done(128)

def weapon(kind):
    I = Icon()
    if kind == 'Sword':        # 黎明之誓：金光长剑
        for m, c in sword_masks(64, 64, -45, L=100, blade_col=BONE, hilt_col=GOLD):
            I.layer(m, c)
        I.layer(poly_mask(star_pts(30, 98, 10, 4, 4)), GOLD, outline=False)
    elif kind == 'Hammer':     # 山之心：石头战锤
        I.layer(line_mask([(28, 112), (86, 34)], 9), RUST_D)
        I.layer(rrect_mask(62, 12, 118, 50, 6), STONE)
        I.layer(rrect_mask(80, 22, 100, 40, 3), OCHRE)
    elif kind == 'Bow':        # 风语：浅绿长弓
        I.layer(arc_mask(30, 64, 54, -70, 70, 7), SAGE)
        I.layer(line_mask([(48, 14), (48, 114)], 2), BONE, outline=False, shade=False)
        I.layer(line_mask([(46, 64), (116, 64)], 4), KHAKI_D)
        I.layer(poly_mask([(114, 64), (100, 56), (100, 72)]), SLATE)
    else:                      # 星辉：星石法杖
        I.layer(line_mask([(30, 116), (84, 36)], 8), KHAKI_D)
        I.layer(arc_mask(90, 28, 16, 0, 360, 4), GOLD)
        I.layer(poly_mask(star_pts(90, 28, 14, 6, 4)), VIOLET, glow=VIOLET)
    return I.done(128)

if __name__ == '__main__':
    bd = os.path.join(ROOT, 'LP', 'Items', 'Tomes'); os.makedirs(bd, exist_ok=True)
    for k in TOME:
        tome(k).save(os.path.join(bd, 'Tome_' + k + '.png'))
    wd = os.path.join(ROOT, 'LP', 'Weapons', 'Legacy'); os.makedirs(wd, exist_ok=True)
    for k in ('Sword', 'Hammer', 'Bow', 'Staff'):
        weapon(k).save(os.path.join(wd, k + '.png'))
    print('growth art written')
