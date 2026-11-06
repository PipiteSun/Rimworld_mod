# 1.120.0 战士子职业「奥术射手」、圣武士子职业「破誓者」的美术：6 + 5 个能力图标 + 2 个徽记（128×128）。
# 画法沿用 draw_titan_subclass.py：粗黑描边、平涂 + 右下暗部、低饱和。只写这两个子职业的新文件
# （Textures/LP/Classes/ArcaneArcher、Textures/LP/Classes/Oathbreaker）。
# 用法：python -I draw_archer_oathbreaker.py <模组根目录>
import math, sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from draw_class_art import (Icon, poly_mask, ellipse_mask, line_mask, arc_mask, rrect_mask, union, subtract, star_pts, shield_pts, bust_mask, sword_masks,
                            Image, CREAM, CREAM_D, KHAKI, KHAKI_D, SLATE, SLATE_D, RUST, RUST_D, GOLD, GOLD_D, BONE, BONE_D, STONE, STONE_D, VIOLET, VIOLET_D)
from draw_class_art2 import eye_mask, flame_mask, rays
from PIL import ImageDraw

ROOT = sys.argv[1] if len(sys.argv) > 1 else '.'
ARC = (108, 170, 206); ARC_D = (70, 118, 150)        # 奥术蓝
WOOD = (150, 112, 70); WOOD_D = (102, 74, 44)        # 弓木
VINE = (112, 150, 88)                                # 荆棘绿
SHADOW = (76, 64, 98)                                # 暗影紫
BLOOD = (150, 40, 48); BLOOD_D = (98, 24, 32)        # 堕落暗红
IRON = (84, 84, 92); IRON_D = (56, 56, 62)           # 黑铁
GHOST = (160, 196, 168)                              # 亡者幽绿

def arrow(I, x0, y0, x1, y1, col=CREAM, head=ARC, w=4):
    a = math.atan2(y1 - y0, x1 - x0); dx, dy = math.cos(a), math.sin(a); px, py = -dy, dx
    I.layer(line_mask([(x0, y0), (x1 - dx * 8, y1 - dy * 8)], w), col, shade=False)
    I.layer(poly_mask([(x1, y1), (x1 - dx * 16 + px * 8, y1 - dy * 16 + py * 8), (x1 - dx * 16 - px * 8, y1 - dy * 16 - py * 8)]), head)
    for k in (0, 7):
        bx, by = x0 + dx * k, y0 + dy * k
        I.layer(poly_mask([(bx, by), (bx - dx * 8 + px * 7, by - dy * 8 + py * 7), (bx + dx * 2, by + dy * 2)]), RUST, shade=False)

def bow(I, cx, cy, s=1.0):
    I.layer(arc_mask(cx + 10 * s, cy, 44 * s, 120, 240, 8 * s), WOOD)
    I.layer(line_mask([(cx - 12 * s, cy - 38 * s), (cx - 12 * s, cy + 38 * s)], 2), BONE, shade=False, outline=False)

def archer_icon(name):
    I = Icon()
    if name == 'ArcaneLore':        # 奥术箭术：弓 + 搭着发光的奥术箭 + 符文星
        bow(I, 40, 64); arrow(I, 30, 64, 116, 64)
        I.layer(poly_mask(star_pts(96, 28, 16, 6, 4)), ARC, glow=ARC)
    elif name == 'GraspingArrow':   # 束缚箭：斜飞的箭 + 缠绕的荆棘藤
        arrow(I, 14, 112, 108, 18)
        for t in (0.3, 0.5, 0.7):
            x, y = 14 + 94 * t, 112 - 94 * t
            I.layer(arc_mask(x, y, 13, 200, 340, 4), VINE, shade=False)
        for x, y in ((40, 70), (64, 50), (86, 34)): I.layer(poly_mask([(x, y), (x + 9, y - 5), (x + 3, y + 6)]), VINE)
    elif name == 'BurstingArrow':   # 爆裂箭：箭头处炸开的奥术星爆
        I.layer(poly_mask(star_pts(84, 46, 40, 16, 8)), ARC, glow=ARC)
        I.layer(poly_mask(star_pts(84, 46, 20, 9, 8)), CREAM, shade=False)
        arrow(I, 12, 116, 72, 58)
    elif name == 'KeenEye':         # 鹰眼：睁大的眼睛 + 准星
        I.layer(eye_mask(64, 64, 52, 28), BONE)
        I.layer(ellipse_mask(64, 64, 20), ARC); I.layer(ellipse_mask(64, 64, 8), (28, 30, 36), shade=False)
        for a, b in (((64, 8), (64, 30)), ((64, 98), (64, 120)), ((8, 64), (24, 64)), ((104, 64), (120, 64))): I.layer(line_mask([a, b], 5), RUST, shade=False)
    elif name == 'ShadowArrow':     # 暗影箭：拖着暗影尾焰的箭 + 被遮住的眼
        I.layer(poly_mask([(10, 96), (60, 54), (70, 70), (24, 116)]), SHADOW, glow=SHADOW)
        arrow(I, 24, 104, 112, 24, BONE_D, SHADOW)
        I.layer(eye_mask(98, 96, 20, 10), BONE); I.layer(ellipse_mask(98, 96, 5), (28, 30, 36), shade=False, outline=False)
        I.layer(line_mask([(80, 104), (116, 88)], 6), SHADOW, shade=False)
    elif name == 'ArrowStorm':      # 箭雨：一片自上而下的奥术箭落在地面
        I.layer(poly_mask([(6, 108), (40, 104), (70, 110), (122, 104), (122, 122), (6, 122)]), STONE)
        for x, y in ((24, 14), (52, 30), (80, 8), (106, 26), (38, 56), (70, 52), (98, 64)):
            arrow(I, x - 10, y, x + 6, y + 40, CREAM, ARC, 3)
    return I.done(128)

def oath_icon(name):
    I = Icon()
    if name == 'DreadfulAspect':    # 恐惧面容：头盔下发红光的双眼 + 恐惧波纹
        for r in (54, 44): I.layer(arc_mask(64, 66, r, 200, 340, 4), BLOOD_D, shade=False)
        I.layer(union(ellipse_mask(64, 68, 30, 34), rrect_mask(34, 70, 94, 104, 10)), IRON)
        I.layer(rrect_mask(40, 62, 88, 72, 3), (24, 22, 26), shade=False)
        for x in (52, 76): I.layer(ellipse_mask(x, 67, 5, 3), (230, 60, 60), shade=False, outline=False, glow=(200, 40, 40))
    elif name == 'ControlUndead':   # 操控亡者：张开的手 + 吊在丝线下的骷髅
        I.layer(rrect_mask(30, 8, 98, 30, 10), IRON)
        for x in (40, 56, 72, 88): I.layer(line_mask([(x, 30), (x, 54)], 2), GHOST, shade=False, outline=False)
        I.layer(union(ellipse_mask(64, 70, 22, 20), rrect_mask(52, 82, 76, 100, 4)), BONE)
        for x in (56, 72): I.layer(ellipse_mask(x, 70, 6), (40, 70, 52), shade=False, outline=False, glow=GHOST)
        I.layer(line_mask([(56, 100), (56, 114)], 4), BONE_D, shade=False); I.layer(line_mask([(72, 100), (72, 114)], 4), BONE_D, shade=False)
    elif name == 'AuraOfHate':      # 憎恨灵光：暗红火环 + 中间倒置的剑
        I.layer(arc_mask(64, 64, 50, 0, 360, 10), BLOOD, glow=BLOOD_D)
        for m, c in sword_masks(64, 64, 90, 92, IRON, BLOOD_D): I.layer(m, c)
    elif name == 'SupernaturalResistance':  # 超凡抗性：裂开的黑盾，箭和剑断在上面
        I.layer(poly_mask(shield_pts(64, 66, 76, 92)), IRON)
        I.layer(line_mask([(64, 26), (58, 54), (70, 74), (62, 104)], 4), BLOOD, shade=False, outline=False)
        I.layer(line_mask([(10, 30), (36, 48)], 5), CREAM_D, shade=False); I.layer(line_mask([(118, 34), (94, 50)], 5), CREAM_D, shade=False)
    elif name == 'DreadLord':       # 恐惧之王：黑冠骑士头像 + 身后暗影光芒
        rays(I, 64, 60, 34, 60, 12, BLOOD_D, 5)
        I.layer(union(ellipse_mask(64, 62, 26, 30), rrect_mask(30, 76, 98, 110, 12)), IRON)
        for x in (48, 64, 80): I.layer(poly_mask([(x - 8, 38), (x, 14), (x + 8, 38)]), BLOOD)
        for x in (54, 74): I.layer(ellipse_mask(x, 62, 5, 3), (230, 60, 60), shade=False, outline=False, glow=(200, 40, 40))
    return I.done(128)

ARCHER = ['ArcaneLore', 'GraspingArrow', 'BurstingArrow', 'KeenEye', 'ShadowArrow', 'ArrowStorm']
OATH = ['DreadfulAspect', 'ControlUndead', 'AuraOfHate', 'SupernaturalResistance', 'DreadLord']

def archer_emblem():
    I = Icon()
    I.layer(poly_mask(shield_pts(64, 64, 100, 116)), GOLD_D)
    I.layer(poly_mask(shield_pts(64, 62, 86, 100)), ARC_D)
    bow(I, 50, 60, 0.8); arrow(I, 40, 60, 104, 60, CREAM, GOLD)
    return I.done(128)

def oath_emblem():
    I = Icon()
    I.layer(poly_mask(shield_pts(64, 64, 100, 116)), IRON_D)
    I.layer(poly_mask(shield_pts(64, 62, 86, 100)), BLOOD_D)
    for m, c in sword_masks(64, 62, 90, 84, BONE_D, IRON): I.layer(m, c)
    I.layer(line_mask([(46, 40), (82, 84)], 5), (24, 22, 26), shade=False, outline=False)   # 断誓的裂痕
    return I.done(128)

def write(folder, items, sheet_name):
    out = os.path.join(ROOT, 'Textures', 'LP', 'Classes', folder); pv = os.path.join(ROOT, 'ArtSource', 'Preview')
    os.makedirs(out, exist_ok=True); os.makedirs(pv, exist_ok=True)
    cell = 150; sheet = Image.new('RGBA', (len(items) * cell + 20, cell + 50), (46, 48, 52, 255)); dr = ImageDraw.Draw(sheet)
    for i, (n, im) in enumerate(items):
        im.save(os.path.join(out, n + '.png'))
        x = 10 + i * cell + 11; sheet.paste(im, (x, 20), im); dr.text((x, 152), n, fill=(220, 220, 210, 255))
    sheet.save(os.path.join(pv, sheet_name))

if __name__ == '__main__':
    write('ArcaneArcher', [(n, archer_icon(n)) for n in ARCHER] + [('Emblem_ArcaneArcher', archer_emblem())], 'ArcaneArcher-icons.png')
    write('Oathbreaker', [(n, oath_icon(n)) for n in OATH] + [('Emblem_Oathbreaker', oath_emblem())], 'Oathbreaker-icons.png')
    print('archer / oathbreaker art written:', len(ARCHER) + len(OATH), 'icons + 2 emblems')
