# 1.53.0 物理职业第一批：7 个战士新能力图标（铁锈主色，同 draw_class_art.py 的战士配色）+ 10 个战斗风格图标（同 draw_feats.py 画风）+ 预览图。
# 128×128，RimWorld / VPE 画风：粗黑描边、平涂 + 右下暗部、低饱和。沿用 draw_class_art.py 的工具函数与调色板。
# 用法：python draw_martial_153.py <模组根目录>
import math, sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from draw_class_art import (Icon, poly_mask, ellipse_mask, line_mask, arc_mask, rrect_mask, union, subtract, star_pts,
                            bust_mask, shield_pts, sword_masks, Image, SS,
                            CREAM, CREAM_D, KHAKI, KHAKI_D, SLATE, SLATE_D, RUST, RUST_D, VIOLET, VIOLET_D, GOLD, GOLD_D, BONE, BONE_D, STONE, STONE_D)
from PIL import ImageDraw

ROOT = sys.argv[1] if len(sys.argv) > 1 else '.'
SAGE = (120, 146, 112)
TEAL = (98, 140, 146)
INKC = (40, 38, 34)

def crosshair(I, cx, cy, r, col=CREAM, w=6):
    I.layer(arc_mask(cx, cy, r, 0, 360, w), col)
    for a, b in (((cx, cy - r - 10), (cx, cy - r + 10)), ((cx, cy + r - 10), (cx, cy + r + 10)), ((cx - r - 10, cy), (cx - r + 10, cy)), ((cx + r - 10, cy), (cx + r + 10, cy))):
        I.layer(line_mask([a, b], w), col)

def rifle(I, x0, y0, x1, y1, col=SLATE, stock=RUST):
    """一支朝 (x1,y1) 的步枪：枪托 + 枪身 + 枪管。"""
    dx, dy = x1 - x0, y1 - y0; L = math.hypot(dx, dy); ux, uy = dx / L, dy / L; px, py = -uy, ux
    s0 = (x0, y0); s1 = (x0 + ux * L * 0.32, y0 + uy * L * 0.32)
    I.layer(poly_mask([(s0[0] + px * 9, s0[1] + py * 9), (s1[0] + px * 6, s1[1] + py * 6), (s1[0] - px * 5, s1[1] - py * 5), (s0[0] - px * 4, s0[1] - py * 4)]), stock)
    b1 = (x0 + ux * L * 0.78, y0 + uy * L * 0.78)
    I.layer(poly_mask([(s1[0] + px * 6, s1[1] + py * 6), (b1[0] + px * 6, b1[1] + py * 6), (b1[0] - px * 5, b1[1] - py * 5), (s1[0] - px * 5, s1[1] - py * 5)]), col)
    I.layer(line_mask([b1, (x1, y1)], 5), SLATE_D)
    # 握把
    g = (x0 + ux * L * 0.42, y0 + uy * L * 0.42)
    I.layer(line_mask([g, (g[0] + px * 14 - ux * 4, g[1] + py * 14 - uy * 4)], 6), stock)

def bullet(I, cx, cy, ang, L=22, col=GOLD):
    a = math.radians(ang); dx, dy = math.cos(a), math.sin(a); px, py = -dy, dx
    tail = (cx - dx * L / 2, cy - dy * L / 2); tip = (cx + dx * L / 2, cy + dy * L / 2)
    I.layer(poly_mask([(tail[0] + px * 5, tail[1] + py * 5), (tip[0] - dx * 7 + px * 5, tip[1] - dy * 7 + py * 5), tip,
                       (tip[0] - dx * 7 - px * 5, tip[1] - dy * 7 - py * 5), (tail[0] - px * 5, tail[1] - py * 5)]), col)

def eye(I, cx, cy, w, h, iris=RUST):
    I.layer(poly_mask([(cx - w, cy), (cx - w * 0.5, cy - h), (cx + w * 0.5, cy - h), (cx + w, cy), (cx + w * 0.5, cy + h), (cx - w * 0.5, cy + h)]), BONE)
    I.layer(ellipse_mask(cx, cy, h * 0.75), iris)
    I.layer(ellipse_mask(cx, cy, h * 0.3), INKC, shade=False)

# ---------------- 7 个战士新能力 ----------------
def ability(name):
    I = Icon()
    if name == 'Charge':            # 冲锋：前冲的人像 + 速度线 + 前伸的剑
        for y, x0 in ((44, 8), (64, 4), (84, 10)):
            I.layer(line_mask([(x0, y), (x0 + 34, y)], 7), RUST)
        I.layer(bust_mask(58, 40, 1.15), SLATE)
        for m, c in sword_masks(94, 66, -25, L=74): I.layer(m, c)
    elif name == 'DisarmingStrike':  # 缴械击：飞出去的剑 + 打击星
        I.layer(poly_mask(star_pts(46, 82, 34, 14, 7)), RUST)
        for m, c in sword_masks(84, 40, -135, L=72): I.layer(m, c)
        I.layer(arc_mask(70, 60, 44, 250, 330, 6), CREAM)
        I.layer(line_mask([(18, 112), (40, 88)], 9), SLATE_D)
    elif name == 'SteadyAim':        # 稳固瞄准：准星 + 中心红点
        crosshair(I, 64, 64, 40)
        I.layer(ellipse_mask(64, 64, 9), RUST)
        I.layer(arc_mask(64, 64, 22, 200, 340, 5), SLATE)
    elif name == 'SuppressingFire':  # 压制射击：一排子弹 + 弹着点
        for i, (x, y) in enumerate(((30, 30), (52, 46), (34, 62), (58, 78))):
            bullet(I, x, y, 20, 26, GOLD if i % 2 == 0 else CREAM)
        for x, y in ((98, 52), (90, 82), (108, 98)):
            I.layer(poly_mask(star_pts(x, y, 15, 6, 6)), RUST)
    elif name == 'PrecisionShot':    # 精准射击：瞄准镜里的单发子弹
        I.layer(ellipse_mask(64, 64, 46), SLATE_D)
        I.layer(ellipse_mask(64, 64, 36), STONE)
        for a, b in (((64, 32), (64, 54)), ((64, 74), (64, 96)), ((32, 64), (54, 64)), ((74, 64), (96, 64))):
            I.layer(line_mask([a, b], 4), INKC, outline=False, shade=False)
        bullet(I, 64, 64, -45, 34, GOLD)
    elif name == 'RapidFire':        # 连射：并排三发 + 枪口火
        I.layer(poly_mask(star_pts(96, 64, 30, 12, 8)), RUST)
        for y in (38, 64, 90):
            bullet(I, 52, y, 0, 40, GOLD if y != 64 else CREAM)
    elif name == 'DeadEye':          # 死神之眼：骷髅般的眼 + 准星
        crosshair(I, 64, 64, 46, RUST_D, 7)
        eye(I, 64, 64, 36, 20, RUST)
    return I.done(128)

# ---------------- 10 个战斗风格 ----------------
def style(name):
    I = Icon()
    if name == 'Archery':          # 箭术：弓 + 搭箭
        I.layer(arc_mask(44, 64, 48, -70, 70, 9), RUST)
        I.layer(line_mask([(60, 20), (60, 108)], 3), CREAM, shade=False)
        I.layer(line_mask([(28, 64), (106, 64)], 6), KHAKI)
        I.layer(poly_mask([(118, 64), (100, 54), (100, 74)]), SLATE)
        I.layer(poly_mask([(30, 64), (16, 54), (22, 64), (16, 74)]), CREAM)
    elif name == 'Defense':        # 防御：胸甲 + 盾徽
        I.layer(poly_mask([(24, 26), (50, 18), (64, 28), (78, 18), (104, 26), (110, 60), (96, 110), (32, 110), (18, 60)]), SLATE)
        I.layer(poly_mask(shield_pts(64, 70, 40, 48)), KHAKI)
    elif name == 'Dueling':        # 决斗：单手细剑 + 背在身后的手
        for m, c in sword_masks(60, 62, -60, L=104): I.layer(m, c)
        I.layer(ellipse_mask(96, 96, 13), BONE)
        I.layer(arc_mask(64, 64, 52, 120, 170, 6), GOLD)
    elif name == 'GreatWeapon':    # 巨武器：双手巨剑
        for m, c in sword_masks(64, 66, -90, L=118, blade_col=SLATE, hilt_col=RUST_D): I.layer(m, c)
        I.layer(poly_mask(star_pts(64, 98, 12, 5, 4)), GOLD)
    elif name == 'Protection':     # 守护：大盾挡在小人像前
        I.layer(bust_mask(92, 30, 0.85), CREAM)
        I.layer(poly_mask(shield_pts(50, 70, 70, 90)), KHAKI)
        I.layer(line_mask([(50, 38), (50, 102)], 5), KHAKI_D, outline=False, shade=False)
    elif name == 'Interception':   # 拦截：剑挡下飞来的箭
        I.layer(line_mask([(10, 30), (62, 58)], 6), STONE)
        I.layer(poly_mask([(70, 62), (54, 60), (60, 48)]), SLATE)
        for m, c in sword_masks(78, 74, -120, L=96): I.layer(m, c)
        I.layer(poly_mask(star_pts(64, 56, 14, 5, 6)), GOLD)
    elif name == 'TwoWeapon':      # 双武器：一长一短交叉
        for m, c in sword_masks(56, 66, -55, L=100): I.layer(m, c)
        for m, c in sword_masks(74, 70, -125, L=72, blade_col=SLATE): I.layer(m, c)
    elif name == 'BlindFighting':  # 盲斗：蒙眼布 + 烟雾
        for x, y, r in ((30, 92, 20), (60, 100, 22), (92, 94, 20)):
            I.layer(ellipse_mask(x, y, r), STONE)
        I.layer(bust_mask(64, 42, 1.05), SLATE)
        I.layer(rrect_mask(40, 34, 92, 46, 4), RUST)
        I.layer(line_mask([(92, 40), (110, 54)], 6), RUST)
    elif name == 'Thrown':         # 投掷：飞出的手雷 + 弧线
        I.layer(arc_mask(70, 96, 58, 200, 300, 5), CREAM)
        I.layer(ellipse_mask(92, 46, 22), SAGE)
        I.layer(rrect_mask(84, 18, 100, 28, 2), SLATE)
        I.layer(line_mask([(100, 22), (112, 14)], 4), GOLD)
    elif name == 'Unarmed':        # 徒手：握紧的拳头
        I.layer(rrect_mask(30, 40, 98, 96, 18), BONE)
        for x in (38, 54, 70, 86):
            I.layer(line_mask([(x, 42), (x, 62)], 3), BONE_D, outline=False, shade=False)
        I.layer(rrect_mask(20, 58, 46, 84, 10), BONE)
        I.layer(rrect_mask(44, 94, 86, 118, 6), RUST)
    return I.done(128)

ABIL = ['Charge', 'DisarmingStrike', 'SteadyAim', 'SuppressingFire', 'PrecisionShot', 'RapidFire', 'DeadEye']
STYLES = ['Archery', 'Defense', 'Dueling', 'GreatWeapon', 'Protection', 'Interception', 'TwoWeapon', 'BlindFighting', 'Thrown', 'Unarmed']

if __name__ == '__main__':
    ad = os.path.join(ROOT, 'Bridges', 'VPE', 'Textures', 'LP', 'Classes', 'Fighter')
    sd = os.path.join(ROOT, 'Textures', 'LP', 'Adventurer', 'Styles')
    pv = os.path.join(ROOT, 'ArtSource', 'Preview')
    for d in (ad, sd, pv): os.makedirs(d, exist_ok=True)
    tiles = []
    for n in ABIL:
        im = ability(n); im.save(os.path.join(ad, n + '.png')); tiles.append(('能力 ' + n, im))
    for n in STYLES:
        im = style(n); im.save(os.path.join(sd, n + '.png')); tiles.append(('风格 ' + n, im))
    # 预览：两行（能力 7 个 / 风格 10 个），深灰底 + 名字
    cols = 10; cell = 150
    sheet = Image.new('RGBA', (cols * cell + 20, 2 * (cell + 24) + 30), (46, 48, 52, 255))
    dr = ImageDraw.Draw(sheet)
    for i, (label, im) in enumerate(tiles):
        row = 0 if i < len(ABIL) else 1; col = i if row == 0 else i - len(ABIL)
        x = 10 + col * cell + 11; y = 20 + row * (cell + 24)
        sheet.paste(im, (x, y), im)
        dr.text((x, y + 130), label.split(' ')[1], fill=(220, 220, 210, 255))
    sheet.save(os.path.join(pv, 'MartialClasses-1.53.0.png'))
    print('martial icons written:', len(ABIL), 'abilities,', len(STYLES), 'styles; preview', os.path.join(pv, 'MartialClasses-1.53.0.png'))
