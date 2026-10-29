# 1.48.1 冒险者专长图标：12 个 128×128，RimWorld / VPE 画风（粗黑描边、平涂 + 右下暗部、低饱和），沿用 draw_class_art.py 的工具函数与调色板。
# 用法：python draw_feats.py <输出目录 Textures/LP/Feats>
import math, sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from draw_class_art import (Icon, poly_mask, ellipse_mask, line_mask, arc_mask, rrect_mask, union, subtract, star_pts,
                            bust_mask, shield_pts, sword_masks, Image,
                            CREAM, CREAM_D, KHAKI, KHAKI_D, SLATE, SLATE_D, RUST, RUST_D, VIOLET, VIOLET_D, GOLD, GOLD_D, BONE, BONE_D, STONE, STONE_D)

OUT = sys.argv[1] if len(sys.argv) > 1 else 'out'
SAGE = (120, 146, 112)
TEAL = (98, 140, 146)

def eye_mask(cx, cy, w, h):
    return poly_mask([(cx - w, cy), (cx - w * 0.5, cy - h), (cx + w * 0.5, cy - h), (cx + w, cy), (cx + w * 0.5, cy + h), (cx - w * 0.5, cy + h)])

def icon(name):
    I = Icon()
    if name == 'Alert':            # 警觉：睁大的眼睛 + 上方三道警示线
        I.layer(eye_mask(64, 70, 50, 26), BONE)
        I.layer(ellipse_mask(64, 70, 17), SLATE)
        I.layer(ellipse_mask(64, 70, 7), (40, 38, 34), shade=False)
        for x0, x1 in ((34, 26), (64, 64), (94, 102)):
            I.layer(line_mask([(x0, 34), (x1, 12)], 7), GOLD)
    elif name == 'Tough':          # 坚韧：心形 + 绷带十字
        heart = union(ellipse_mask(46, 50, 24), ellipse_mask(82, 50, 24), poly_mask([(24, 58), (104, 58), (64, 112)]))
        I.layer(heart, RUST)
        I.layer(rrect_mask(54, 36, 74, 92, 4), BONE)
        I.layer(rrect_mask(36, 54, 92, 74, 4), BONE)
    elif name == 'Lucky':          # 幸运：二十面骰 + 小星
        hexp = [(64 + 46 * math.cos(math.radians(a)), 66 + 46 * math.sin(math.radians(a))) for a in range(-90, 270, 60)]
        I.layer(poly_mask(hexp), KHAKI)
        I.layer(poly_mask([(64, 30), (96, 84), (32, 84)]), CREAM)
        I.layer(poly_mask(star_pts(102, 24, 16, 6, 5)), GOLD)
    elif name == 'GreatWeaponMaster':  # 巨武器大师：双手大斧
        I.layer(line_mask([(30, 112), (96, 22)], 9), RUST_D)
        I.layer(poly_mask([(78, 16), (118, 30), (112, 66), (88, 48)]), SLATE)
        I.layer(poly_mask([(72, 40), (52, 20), (40, 50), (62, 58)]), SLATE)
    elif name == 'Sharpshooter':   # 神射手：准星 + 箭
        I.layer(arc_mask(64, 64, 44, 0, 360, 8), CREAM)
        for a, b in (((64, 8), (64, 32)), ((64, 96), (64, 120)), ((8, 64), (32, 64)), ((96, 64), (120, 64))):
            I.layer(line_mask([a, b], 7), CREAM)
        I.layer(line_mask([(18, 110), (70, 58)], 6), RUST)
        I.layer(poly_mask([(64, 52), (84, 44), (76, 64)]), SLATE)
    elif name == 'Sentinel':       # 哨兵：竖盾 + 斜矛
        I.layer(poly_mask(shield_pts(56, 66, 70, 88)), KHAKI)
        I.layer(line_mask([(28, 116), (104, 20)], 6), RUST_D)
        I.layer(poly_mask([(104, 8), (116, 30), (96, 26)]), SLATE)
        I.layer(poly_mask(star_pts(56, 62, 16, 7, 4)), CREAM)
    elif name == 'WarCaster':      # 战争施法者：法杖 + 环绕的法球
        I.layer(line_mask([(36, 118), (76, 30)], 8), RUST_D)
        I.layer(ellipse_mask(80, 26, 15), VIOLET, glow=VIOLET)
        I.layer(arc_mask(64, 66, 46, 200, 340, 7), VIOLET_D)
        for a, b in ((18, 54), (110, 54)):
            I.layer(poly_mask(star_pts(a, b, 11, 4, 4)), CREAM)
    elif name == 'Healer':         # 治疗者：药包 + 十字
        I.layer(rrect_mask(18, 38, 110, 108, 10), BONE)
        I.layer(rrect_mask(44, 24, 84, 42, 6), STONE)
        I.layer(rrect_mask(56, 50, 72, 98, 3), SAGE)
        I.layer(rrect_mask(40, 66, 88, 82, 3), SAGE)
    elif name == 'HeavyArmorMaster':  # 重甲大师：板甲胸甲
        I.layer(poly_mask([(24, 26), (50, 18), (64, 28), (78, 18), (104, 26), (110, 60), (96, 110), (32, 110), (18, 60)]), SLATE)
        I.layer(line_mask([(64, 30), (64, 104)], 5), SLATE_D, outline=False, shade=False)
        I.layer(poly_mask([(34, 62), (94, 62), (88, 74), (40, 74)]), STONE)
    elif name == 'Mobile':         # 灵活：长靴 + 速度线
        I.layer(poly_mask([(56, 16), (82, 16), (82, 78), (112, 92), (112, 110), (52, 110), (52, 80)]), RUST)
        I.layer(rrect_mask(50, 100, 114, 112, 3), RUST_D)
        for y, x0 in ((40, 12), (60, 6), (80, 14)):
            I.layer(line_mask([(x0, y), (x0 + 30, y)], 6), CREAM)
    elif name == 'InspiringLeader':  # 鼓舞领袖：旗帜 + 光环
        I.layer(arc_mask(64, 70, 50, 200, 340, 7), GOLD)
        I.layer(line_mask([(40, 118), (40, 16)], 7), RUST_D)
        I.layer(poly_mask([(44, 18), (108, 30), (92, 48), (108, 66), (44, 60)]), RUST)
        I.layer(poly_mask(star_pts(68, 40, 12, 5, 5)), GOLD)
    elif name == 'SpellSniper':    # 法术狙击：远射的魔法飞弹
        I.layer(line_mask([(14, 114), (60, 68)], 9), VIOLET_D)
        I.layer(poly_mask(star_pts(84, 44, 30, 11, 4)), VIOLET, glow=VIOLET)
        I.layer(ellipse_mask(84, 44, 8), CREAM)
        I.layer(arc_mask(84, 44, 40, 200, 250, 5), TEAL)
    return I.done(128)

NAMES = ['Alert', 'Tough', 'Lucky', 'GreatWeaponMaster', 'Sharpshooter', 'Sentinel', 'WarCaster', 'Healer',
         'HeavyArmorMaster', 'Mobile', 'InspiringLeader', 'SpellSniper']

if __name__ == '__main__':
    os.makedirs(OUT, exist_ok=True)
    for n in NAMES:
        icon(n).save(os.path.join(OUT, n + '.png'))
    print('feat icons written to', OUT)
