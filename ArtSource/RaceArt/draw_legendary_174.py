# 1.74.0 传奇职业能力：16 个图标（8 职业 × 主动 / 被动）+ 预览图。
# 128×128，RimWorld / VPE 画风：粗黑描边、平涂 + 右下暗部、低饱和；每个图标外面一圈暗金色“传奇”环（主动实线环，被动断开的环）。
# 沿用 draw_class_art.py / draw_martial_153.py 的工具函数与调色板。
# 用法：python draw_legendary_174.py <模组根目录>
import math, sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from draw_class_art import (Icon, poly_mask, ellipse_mask, line_mask, arc_mask, rrect_mask, star_pts,
                            bust_mask, shield_pts, sword_masks, Image,
                            CREAM, CREAM_D, KHAKI, KHAKI_D, SLATE, SLATE_D, RUST, RUST_D, VIOLET, VIOLET_D, GOLD, GOLD_D, BONE, BONE_D, STONE, STONE_D)
from PIL import ImageDraw

ROOT = sys.argv[1] if len(sys.argv) > 1 else '.'
SAGE = (120, 146, 112)
TEAL = (98, 140, 146)
INKC = (40, 38, 34)
EMBER = (176, 98, 62)

def ring(I, passive):
    if passive:
        for a in range(0, 360, 45):
            I.layer(arc_mask(64, 64, 57, a + 6, a + 39, 6), GOLD_D)
    else:
        I.layer(arc_mask(64, 64, 57, 0, 360, 6), GOLD_D)

def fist(I, cx, cy, s=1.0, col=BONE):
    I.layer(rrect_mask(cx - 26 * s, cy - 20 * s, cx + 26 * s, cy + 20 * s, 12 * s), col)
    for i in range(4):
        x = cx - 18 * s + i * 12 * s
        I.layer(line_mask([(x, cy - 18 * s), (x, cy - 4 * s)], 3), BONE_D, outline=False, shade=False)

def meteor(I, x, y, r, ang=-45):
    a = math.radians(ang); dx, dy = math.cos(a), math.sin(a)
    I.layer(line_mask([(x - dx * r * 2.6, y - dy * r * 2.6), (x, y)], int(r * 1.1)), EMBER)
    I.layer(ellipse_mask(x, y, r), RUST)

def bolt_shape(I, x, y, s=1.0, col=GOLD):
    I.layer(poly_mask([(x, y), (x + 14 * s, y + 26 * s), (x + 4 * s, y + 26 * s), (x + 16 * s, y + 52 * s), (x - 10 * s, y + 20 * s), (x + 0 * s, y + 20 * s), (x - 10 * s, y)]), col)

def gear(I, cx, cy, r, col=KHAKI):
    pts = []
    for i in range(24):
        a = math.radians(i * 15); rr = r if i % 2 == 0 else r * 0.78
        pts.append((cx + math.cos(a) * rr, cy + math.sin(a) * rr))
    I.layer(poly_mask(pts), col)
    I.layer(ellipse_mask(cx, cy, r * 0.32), SLATE_D)

def icon(name):
    I = Icon()
    passive = name in PASSIVE
    if name == 'Unyielding':            # 不屈战魂：裂开的盾牌里升起的火焰
        I.layer(poly_mask(shield_pts(64, 70, 66, 80)), SLATE)
        I.layer(line_mask([(52, 34), (70, 64), (58, 104)], 5), INKC, outline=False, shade=False)
        I.layer(poly_mask([(64, 20), (82, 54), (72, 50), (76, 76), (54, 46), (62, 48)]), RUST)
    elif name == 'ThousandArmies':      # 千军破：一扇形剑气 + 三把剑
        I.layer(arc_mask(30, 98, 74, 280, 350, 10), CREAM)
        I.layer(arc_mask(30, 98, 56, 280, 350, 7), RUST)
        for ang, x, y in ((-70, 46, 60), (-45, 64, 70), (-20, 80, 86)):
            for m, c in sword_masks(x, y, ang, L=60): I.layer(m, c)
    elif name == 'Formless':            # 无相之身：半透明的虚影人像重叠
        I.layer(bust_mask(48, 44, 1.0), STONE)
        I.layer(bust_mask(80, 44, 1.0), STONE_D)
        I.layer(bust_mask(64, 40, 1.1), CREAM)
        I.layer(arc_mask(64, 64, 40, 200, 340, 5), GOLD)
    elif name == 'HeavenPalm':          # 天震掌：张开的掌 + 震波
        for r in (46, 34):
            I.layer(arc_mask(64, 64, r, 0, 360, 5), CREAM_D)
        I.layer(rrect_mask(46, 40, 82, 92, 12), BONE)
        for i in range(4):
            I.layer(rrect_mask(46 + i * 9, 22, 54 + i * 9, 46, 4), BONE)
        I.layer(poly_mask(star_pts(64, 66, 12, 5, 6)), GOLD)
    elif name == 'DivineIncarnation':   # 神圣化身：发光人像 + 光环
        for a in range(0, 360, 30):
            ar = math.radians(a)
            I.layer(line_mask([(64 + math.cos(ar) * 34, 58 + math.sin(ar) * 34), (64 + math.cos(ar) * 50, 58 + math.sin(ar) * 50)], 5), GOLD)
        I.layer(bust_mask(64, 50, 1.05), CREAM)
        I.layer(arc_mask(64, 26, 16, 180, 360, 5), GOLD)
    elif name == 'UndyingOath':         # 誓约不灭：盾 + 环绕的锁链圈
        I.layer(poly_mask(shield_pts(64, 64, 58, 70)), KHAKI)
        I.layer(poly_mask(star_pts(64, 60, 18, 7, 4)), GOLD)
        for a in range(0, 360, 40):
            ar = math.radians(a)
            I.layer(ellipse_mask(64 + math.cos(ar) * 46, 64 + math.sin(ar) * 46, 6), SLATE)
    elif name == 'SpellMastery':        # 法术强效：法典 + 三颗星
        I.layer(rrect_mask(30, 52, 98, 106, 6), VIOLET)
        I.layer(line_mask([(64, 54), (64, 104)], 4), VIOLET_D, outline=False, shade=False)
        for x, y, r in ((40, 34, 12), (64, 26, 15), (88, 34, 12)):
            I.layer(poly_mask(star_pts(x, y, r, r * 0.42, 5)), GOLD)
    elif name == 'MeteorSwarm':         # 流星爆：四颗流星
        for x, y, r in ((44, 50, 11), (86, 40, 9), (70, 86, 13), (100, 92, 8)):
            meteor(I, x, y, r)
    elif name == 'DivineIntervention':  # 神迹干预：从天而降的光柱 + 张开的手
        I.layer(poly_mask([(48, 10), (80, 10), (92, 96), (36, 96)]), CREAM)
        I.layer(rrect_mask(38, 92, 90, 112, 8), BONE)
        I.layer(poly_mask(star_pts(64, 46, 16, 7, 8)), GOLD)
    elif name == 'DivineGuardian':      # 神圣守护：翅膀护着的心
        I.layer(poly_mask([(64, 64), (18, 30), (10, 54), (26, 80), (50, 86)]), CREAM)
        I.layer(poly_mask([(64, 64), (110, 30), (118, 54), (102, 80), (78, 86)]), CREAM_D)
        I.layer(poly_mask([(64, 104), (44, 80), (46, 64), (56, 58), (64, 66), (72, 58), (82, 64), (84, 80)]), RUST)
    elif name == 'TimelessDruid':       # 永恒德鲁伊：沙漏里长出的嫩芽
        I.layer(poly_mask([(36, 18), (92, 18), (68, 64), (92, 110), (36, 110), (60, 64)]), STONE)
        I.layer(poly_mask([(48, 96), (80, 96), (64, 74)]), KHAKI)
        I.layer(line_mask([(64, 64), (64, 36)], 5), SAGE)
        I.layer(poly_mask([(64, 40), (84, 28), (72, 48)]), SAGE)
        I.layer(poly_mask([(64, 46), (44, 34), (56, 54)]), SAGE)
    elif name == 'NatureWrath':         # 自然之怒：雷云 + 闪电 + 藤蔓
        for x, y, r in ((40, 36, 18), (64, 28, 22), (90, 38, 17)):
            I.layer(ellipse_mask(x, y, r), SLATE)
        bolt_shape(I, 62, 48, 1.0)
        I.layer(arc_mask(40, 112, 28, 200, 340, 7), SAGE)
        I.layer(arc_mask(92, 112, 26, 200, 340, 7), SAGE)
    elif name == 'BloodlineAwakening':  # 血脉觉醒：龙眼 + 火焰
        I.layer(poly_mask([(64, 14), (92, 60), (80, 56), (88, 92), (64, 70), (40, 92), (48, 56), (36, 60)]), RUST)
        I.layer(poly_mask([(26, 76), (64, 56), (102, 76), (64, 96)]), BONE)
        I.layer(poly_mask([(64, 60), (70, 76), (64, 92), (58, 76)]), INKC, shade=False)
    elif name == 'ArcaneSurge':         # 魔力奔流：三道螺旋的魔力流
        for r, col in ((46, VIOLET), (34, CREAM), (22, VIOLET_D)):
            I.layer(arc_mask(64, 64, r, 30, 300, 7), col)
        I.layer(poly_mask(star_pts(64, 64, 12, 5, 6)), GOLD)
    elif name == 'Soulforge':           # 魂铸：铁砧上燃着魂火的锤
        I.layer(poly_mask([(20, 82), (108, 82), (96, 96), (80, 96), (84, 112), (44, 112), (48, 96), (32, 96)]), SLATE)
        I.layer(line_mask([(46, 70), (88, 28)], 8), KHAKI_D)
        I.layer(rrect_mask(70, 16, 104, 38, 4), STONE)
        I.layer(poly_mask([(40, 78), (50, 54), (58, 66), (66, 48), (72, 78)]), TEAL)
    elif name == 'WarGolem':            # 战争傀儡：齿轮 + 符文炮管
        gear(I, 56, 72, 36)
        I.layer(rrect_mask(70, 36, 116, 52, 4), SLATE)
        I.layer(poly_mask(star_pts(56, 72, 9, 4, 4)), TEAL)
    ring(I, passive)
    return I.done(128)

NAMES = ['Unyielding', 'ThousandArmies', 'Formless', 'HeavenPalm', 'DivineIncarnation', 'UndyingOath', 'SpellMastery', 'MeteorSwarm',
         'DivineIntervention', 'DivineGuardian', 'TimelessDruid', 'NatureWrath', 'BloodlineAwakening', 'ArcaneSurge', 'Soulforge', 'WarGolem']
PASSIVE = {'Unyielding', 'Formless', 'UndyingOath', 'SpellMastery', 'DivineGuardian', 'TimelessDruid', 'BloodlineAwakening', 'Soulforge'}

if __name__ == '__main__':
    od = os.path.join(ROOT, 'Textures', 'LP', 'Classes', 'Legendary')
    pv = os.path.join(ROOT, 'ArtSource', 'Preview')
    for d in (od, pv): os.makedirs(d, exist_ok=True)
    cols = 8; cell = 150
    sheet = Image.new('RGBA', (cols * cell + 20, 2 * (cell + 24) + 30), (46, 48, 52, 255))
    dr = ImageDraw.Draw(sheet)
    for i, n in enumerate(NAMES):
        im = icon(n); im.save(os.path.join(od, n + '.png'))
        x = 10 + (i % cols) * cell + 11; y = 20 + (i // cols) * (cell + 24)
        sheet.paste(im, (x, y), im); dr.text((x, y + 130), n, fill=(220, 220, 210, 255))
    sheet.save(os.path.join(pv, 'LegendaryClasses-1.74.0.png'))
    print('legendary icons written:', len(NAMES))
