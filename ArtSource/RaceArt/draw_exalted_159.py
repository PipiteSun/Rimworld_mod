# 1.59.0 超凡属性特性徽记（128×128，12 张）：金框属性纹章；神话版外圈加紫焰边。沿用 draw_class_art.py 的画风（粗黑描边、平涂 + 右下暗部）。
# 用法：python draw_exalted_159.py <Textures 根目录>  → Textures/LP/Exalted/{Str,Dex,Con,Int,Wis,Cha}[_Mythic].png
import math, sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from draw_class_art import (Icon, poly_mask, ellipse_mask, line_mask, arc_mask, rrect_mask, union, subtract, star_pts,
                            CREAM, CREAM_D, KHAKI, KHAKI_D, SLATE, SLATE_D, RUST, RUST_D, VIOLET, VIOLET_D, GOLD, GOLD_D, BONE, BONE_D, STONE, STONE_D)

ROOT = sys.argv[1] if len(sys.argv) > 1 else 'out'
PURPLE = (150, 96, 200); PURPLE_L = (196, 150, 236)
FIELD = (70, 58, 50)
BLOOD = (170, 62, 56)
SKY = (120, 156, 184)

def frame(I, mythic):
    if mythic:  # 紫焰边：外圈一圈火舌
        for k in range(12):
            a = math.radians(k * 30 - 90)
            cx, cy = 64 + 52 * math.cos(a), 64 + 52 * math.sin(a)
            tx, ty = 64 + 63 * math.cos(a), 64 + 63 * math.sin(a)
            nx, ny = -math.sin(a) * 9, math.cos(a) * 9
            I.layer(poly_mask([(cx + nx, cy + ny), (tx, ty), (cx - nx, cy - ny)]), PURPLE, glow=PURPLE_L)
    I.layer(ellipse_mask(64, 64, 52), GOLD)
    I.layer(ellipse_mask(64, 64, 42), FIELD, shade=False)

def emblem(I, key):
    if key == 'Str':    # 巨人之力：握拳
        I.layer(rrect_mask(40, 44, 88, 86, 12), KHAKI)
        for x in (48, 60, 72):
            I.layer(line_mask([(x + 4, 46), (x + 4, 62)], 3), KHAKI_D, outline=False, shade=False)
        I.layer(rrect_mask(30, 54, 46, 80, 6), KHAKI)
        I.layer(rrect_mask(50, 86, 80, 100, 4), RUST)
    elif key == 'Dex':  # 疾影：羽毛 + 风痕
        I.layer(poly_mask([(84, 26), (98, 40), (52, 100), (42, 96)]), BONE)
        I.layer(line_mask([(90, 32), (40, 102)], 3), BONE_D, outline=False, shade=False)
        for y in (54, 70, 86):
            I.layer(line_mask([(28, y), (52, y - 6)], 4), SKY, shade=False)
    elif key == 'Con':  # 不屈之躯：心 + 盾
        I.layer(poly_mask([(64, 100), (32, 66), (34, 46), (50, 36), (64, 48), (78, 36), (94, 46), (96, 66)]), BLOOD)
        I.layer(ellipse_mask(52, 52, 6), CREAM, outline=False, shade=False)
    elif key == 'Int':  # 洞彻心智：翻开的书 + 星
        I.layer(poly_mask([(26, 58), (62, 52), (62, 98), (26, 102)]), CREAM)
        I.layer(poly_mask([(66, 52), (102, 58), (102, 102), (66, 98)]), CREAM_D)
        I.layer(poly_mask(star_pts(64, 38, 16, 7, 4)), SLATE, glow=SLATE)
    elif key == 'Wis':  # 明察：睁开的眼
        I.layer(poly_mask([(24, 64), (44, 46), (64, 40), (84, 46), (104, 64), (84, 82), (64, 88), (44, 82)]), BONE)
        I.layer(ellipse_mask(64, 64, 16), SLATE)
        I.layer(ellipse_mask(64, 64, 7), (30, 28, 34), outline=False, shade=False)
    else:               # 威仪：王冠
        I.layer(poly_mask([(30, 92), (30, 50), (46, 70), (64, 38), (82, 70), (98, 50), (98, 92)]), GOLD)
        I.layer(rrect_mask(30, 86, 98, 98, 3), GOLD_D)
        for x in (44, 64, 84):
            I.layer(ellipse_mask(x, 92, 4), BLOOD, outline=False, shade=False)

def badge(key, mythic):
    I = Icon()
    frame(I, mythic)
    emblem(I, key)
    return I.done(128)

if __name__ == '__main__':
    d = os.path.join(ROOT, 'LP', 'Exalted'); os.makedirs(d, exist_ok=True)
    n = 0
    for key in ('Str', 'Dex', 'Con', 'Int', 'Wis', 'Cha'):
        for mythic in (False, True):
            badge(key, mythic).save(os.path.join(d, key + ('_Mythic' if mythic else '') + '.png')); n += 1
    print('exalted badges written:', n)
