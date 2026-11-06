# 1.115.0 战士子职业「泰坦战士」的美术：6 个能力图标 + 1 个徽记（128×128）。
# 画法沿用 draw_subclasses2.py / draw_subclasses4.py：粗黑描边、平涂 + 右下暗部、低饱和。只写本子职业的新文件（Textures/LP/Classes/Titan）。
# 用法：python -I draw_titan_subclass.py <模组根目录>
import math, sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from draw_class_art import (Icon, poly_mask, ellipse_mask, line_mask, arc_mask, rrect_mask, union, star_pts, shield_pts, bust_mask,
                            Image, CREAM, CREAM_D, KHAKI, KHAKI_D, SLATE, SLATE_D, RUST, RUST_D, GOLD, GOLD_D, BONE, BONE_D, STONE, STONE_D)
from draw_class_art2 import fist_mask, bolt_mask, rays
from PIL import ImageDraw

ROOT = sys.argv[1] if len(sys.argv) > 1 else '.'
SKIN = (196, 150, 112); SKIN_D = (140, 100, 72)     # 巨人肤色
EARTH = (150, 118, 84); EARTH_D = (100, 78, 54)     # 土褐
ROCK = (140, 140, 132); ROCK_D = (92, 92, 88)       # 山岩

def giant(I, cx, cy, s=1.0, col=SKIN):   # 宽肩巨人剪影（cy 为头心）
    I.layer(union(ellipse_mask(cx, cy, 12 * s), rrect_mask(cx - 30 * s, cy + 12 * s, cx + 30 * s, cy + 58 * s, 14 * s)), col)
def ground(I, y, col=EARTH):
    I.layer(poly_mask([(6, y), (40, y - 4), (64, y + 2), (92, y - 3), (122, y), (122, y + 18), (6, y + 18)]), col)
def cracks(I, cx, y):
    for pts in (((cx, y), (cx - 14, y + 8), (cx - 26, y + 6)), ((cx, y), (cx + 12, y + 10), (cx + 28, y + 7)), ((cx, y), (cx + 2, y + 14))):
        I.layer(line_mask(list(pts), 3), EARTH_D, shade=False, outline=False)

def icon(name):
    I = Icon()
    if name == 'TitanBody':       # 泰坦之躯：小人身后站着大一圈的巨人 + 向上箭头
        giant(I, 72, 30, 1.25, SKIN); giant(I, 30, 70, 0.55, BONE)
        I.layer(poly_mask([(110, 18), (122, 34), (115, 34), (115, 52), (105, 52), (105, 34), (98, 34)]), GOLD)
    elif name == 'MightyGrip':    # 巨力之握：握紧的大拳 + 被捏住的小石块碎屑
        I.layer(fist_mask(58, 70, 1.7), SKIN)
        for x, y, r in ((108, 30, 8), (116, 52, 6), (98, 14, 5)): I.layer(ellipse_mask(x, y, r), ROCK)
    elif name == 'Stomp':         # 撼地践踏：大脚踩下、地面裂开、两侧冲击波
        ground(I, 96); cracks(I, 64, 100)
        I.layer(union(rrect_mask(50, 10, 78, 70, 8), rrect_mask(38, 62, 90, 92, 10)), RUST_D)
        I.layer(rrect_mask(36, 84, 92, 96, 4), STONE_D)
        for side in (-1, 1):
            for k, (y, l) in enumerate(((70, 18), (82, 26))):
                x0 = 64 + side * 34; I.layer(line_mask([(x0 + side * 4, y), (x0 + side * l, y - 8 + k * 4)], 4), CREAM, shade=False)
    elif name == 'Immovable':     # 不动如山：山岩 + 前方的盾
        I.layer(poly_mask([(6, 116), (44, 30), (64, 52), (86, 18), (122, 116)]), ROCK)
        I.layer(poly_mask([(78, 32), (86, 18), (94, 34)]), CREAM, shade=False)
        I.layer(poly_mask(shield_pts(64, 82, 44, 52)), RUST)
    elif name == 'Titanize':      # 泰坦化：巨人剪影 + 身后放射光芒
        rays(I, 64, 56, 38, 60, 12, GOLD, 5)
        giant(I, 64, 34, 1.35, SKIN); I.layer(ellipse_mask(64, 34, 5), GOLD, outline=False, shade=False)
    elif name == 'AncientTitan':  # 远古泰坦：戴石冠的巨人头像 + 雷纹 + 裂地
        ground(I, 100); cracks(I, 64, 104)
        I.layer(union(ellipse_mask(64, 56, 30, 34), rrect_mask(34, 70, 94, 100, 12)), ROCK)
        for x in (44, 64, 84): I.layer(poly_mask([(x - 9, 30), (x, 8), (x + 9, 30)]), GOLD)
        for x in (52, 76): I.layer(ellipse_mask(x, 54, 5, 4), CREAM, shade=False)
        I.layer(bolt_mask(108, 40, 0.55), GOLD)
    return I.done(128)

ICONS = ['TitanBody', 'MightyGrip', 'Stomp', 'Immovable', 'Titanize', 'AncientTitan']

def emblem():
    I = Icon()
    I.layer(poly_mask(shield_pts(64, 64, 100, 116)), GOLD_D)
    I.layer(poly_mask(shield_pts(64, 62, 86, 100)), EARTH_D)
    giant(I, 64, 38, 0.95, SKIN); ground(I, 92, EARTH)
    return I.done(128)

if __name__ == '__main__':
    out = os.path.join(ROOT, 'Textures', 'LP', 'Classes', 'Titan'); pv = os.path.join(ROOT, 'ArtSource', 'Preview')
    os.makedirs(out, exist_ok=True); os.makedirs(pv, exist_ok=True)
    items = [(n, icon(n)) for n in ICONS] + [('Emblem_Titan', emblem())]
    cell = 150; sheet = Image.new('RGBA', (len(items) * cell + 20, cell + 50), (46, 48, 52, 255)); dr = ImageDraw.Draw(sheet)
    for i, (n, im) in enumerate(items):
        im.save(os.path.join(out, n + '.png'))
        x = 10 + i * cell + 11; sheet.paste(im, (x, 20), im); dr.text((x, 152), n, fill=(220, 220, 210, 255))
    sheet.save(os.path.join(pv, 'Titan-icons.png'))
    print('titan art written:', len(ICONS), 'icons + emblem')
