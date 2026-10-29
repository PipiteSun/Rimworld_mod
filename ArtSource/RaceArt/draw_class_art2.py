# 1.36.0 德鲁伊、圣武士、术士、武僧的职业美术，沿用 draw_class_art.py 的画法（粗黑描边、平涂 + 右下暗部、VPE 配色）：
#   36 个能力图标 128×128，4 张路径背景 950×1515（塔罗牌式深色底 + 剪影）。
# 用法：python draw_class_art2.py <输出目录 Bridges/VPE/Textures/LP/Classes>
# 主色：德鲁伊苔绿、圣武士金白、术士紫红、武僧赭黄。
import math, sys, os, random
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from PIL import Image, ImageDraw, ImageFilter, ImageChops
from draw_class_art import (Icon, SS, S, INK, CREAM, CREAM_D, KHAKI, KHAKI_D, SLATE, SLATE_D, RUST, RUST_D, VIOLET, VIOLET_D,
                            GOLD, GOLD_D, BONE, BONE_D, STONE, STONE_D, P, poly_mask, ellipse_mask, line_mask, arc_mask, rrect_mask,
                            union, subtract, star_pts, bust_mask, shield_pts, sword_masks, darker, star_pts)

OUT = sys.argv[1] if len(sys.argv) > 1 else 'out'
MOSS = (110, 134, 74);  MOSS_D = (74, 92, 48)
BARK = (124, 90, 58)
FUR = (138, 104, 70)
PLUM = (156, 84, 140); PLUM_D = (108, 54, 96)
ICE = (176, 206, 220)
OCHRE = (198, 146, 68); OCHRE_D = (146, 102, 40)
SKIN = (206, 170, 128)

def leaf_mask(x0, y0, x1, y1, w):
    pts = []
    for i in range(25):
        t = i / 24; ax = x0 + t * (x1 - x0); ay = y0 + t * (y1 - y0)
        dx, dy = (x1 - x0), (y1 - y0); L = math.hypot(dx, dy); px, py = -dy / L, dx / L
        ww = w * math.sin(math.pi * t) ** 0.8; pts.append((ax + px * ww, ay + py * ww))
    for i in range(24, -1, -1):
        t = i / 24; ax = x0 + t * (x1 - x0); ay = y0 + t * (y1 - y0)
        dx, dy = (x1 - x0), (y1 - y0); L = math.hypot(dx, dy); px, py = -dy / L, dx / L
        ww = w * math.sin(math.pi * t) ** 0.8; pts.append((ax - px * ww, ay - py * ww))
    return poly_mask(pts)

def flame_mask(cx, cy, s=1.0):
    return poly_mask([(cx + x * s, cy + y * s) for x, y in ((0, -56), (20, -26), (14, -12), (34, 4), (24, 34), (0, 44), (-24, 34), (-34, 4), (-14, -12), (-20, -26))])

def cloud_mask(cx, cy, s=1.0):
    return union(ellipse_mask(cx - 22 * s, cy + 4 * s, 18 * s), ellipse_mask(cx, cy - 8 * s, 24 * s), ellipse_mask(cx + 24 * s, cy + 2 * s, 19 * s),
                 rrect_mask(cx - 38 * s, cy, cx + 42 * s, cy + 20 * s, 10 * s))

def bolt_mask(cx, cy, s=1.0):
    return poly_mask([(cx + x * s, cy + y * s) for x, y in ((6, -40), (-16, 4), (-2, 4), (-10, 40), (18, -6), (4, -6), (16, -40))])

def fist_mask(cx, cy, s=1.0):
    """正面的拳头：四指 + 拇指。"""
    ms = [rrect_mask(cx - 22 * s, cy - 6 * s, cx + 22 * s, cy + 26 * s, 8 * s)]
    for i in range(4):
        x = cx - 22 * s + i * 11 * s
        ms.append(rrect_mask(x, cy - 18 * s, x + 11 * s, cy + 4 * s, 5 * s))
    ms.append(rrect_mask(cx - 28 * s, cy + 2 * s, cx - 12 * s, cy + 20 * s, 6 * s))
    return union(*ms)

def palm_mask(cx, cy, s=1.0, rot=0):
    """张开的手掌。"""
    ms = [rrect_mask(cx - 20 * s, cy - 8 * s, cx + 20 * s, cy + 30 * s, 10 * s)]
    for i, (dx, h) in enumerate(((-15, 30), (-5, 36), (5, 34), (15, 28))):
        ms.append(rrect_mask(cx + dx * s - 4.5 * s, cy - 8 * s - h * s, cx + dx * s + 4.5 * s, cy, 4.5 * s))
    ms.append(line_mask([(cx - 18 * s, cy + 14 * s), (cx - 34 * s, cy - 4 * s)], 9 * s))
    m = union(*ms)
    if rot: m = m.rotate(rot, resample=Image.BICUBIC, center=(cx * SS, cy * SS))
    return m

def paw_mask(cx, cy, s=1.0):
    return union(ellipse_mask(cx, cy + 8 * s, 18 * s, 15 * s), *[ellipse_mask(cx + dx * s, cy + dy * s, 7 * s, 8 * s) for dx, dy in ((-20, -10), (-7, -20), (7, -20), (20, -10))])

def wolf_mask(cx, cy, s=1.0):
    """侧面狼头（朝右）。"""
    return poly_mask([(cx + x * s, cy + y * s) for x, y in ((-30, 30), (-28, -6), (-20, -36), (-8, -14), (6, -34), (10, -10), (40, 2), (42, 12), (14, 18), (0, 34))])

def bear_mask(cx, cy, s=1.0):
    """正面熊头。"""
    return union(ellipse_mask(cx, cy, 34 * s, 30 * s), ellipse_mask(cx - 26 * s, cy - 24 * s, 11 * s), ellipse_mask(cx + 26 * s, cy - 24 * s, 11 * s))

def heart_mask(cx, cy, s=1.0):
    return union(ellipse_mask(cx - 12 * s, cy - 6 * s, 14 * s), ellipse_mask(cx + 12 * s, cy - 6 * s, 14 * s),
                 poly_mask([(cx - 25 * s, cy), (cx + 25 * s, cy), (cx, cy + 30 * s)]))

def eye_mask(cx, cy, w, h):
    pts = []
    for i in range(33):
        t = i / 32 * math.pi; pts.append((cx - w * math.cos(t), cy - h * math.sin(t)))
    for i in range(33):
        t = i / 32 * math.pi; pts.append((cx + w * math.cos(t), cy + h * math.sin(t)))
    return poly_mask(pts)

def snow_mask(cx, cy, r, w=5):
    ms = []
    for a in range(3):
        ang = math.radians(a * 60)
        ms.append(line_mask([(cx - r * math.cos(ang), cy - r * math.sin(ang)), (cx + r * math.cos(ang), cy + r * math.sin(ang))], w))
    return union(*ms)

def wing_mask(cx, cy, side, s=1.0):
    """张开的龙翼（side=-1 左，1 右）。"""
    pts = [(0, 0), (side * 20, -34), (side * 56, -40), (side * 50, -18), (side * 58, 4), (side * 44, 8), (side * 46, 26), (side * 30, 22), (side * 22, 34)]
    return poly_mask([(cx + x * s, cy + y * s) for x, y in pts])

def tree_mask(cx, cy, s=1.0):
    return union(rrect_mask(cx - 7 * s, cy, cx + 7 * s, cy + 46 * s, 4 * s), ellipse_mask(cx, cy - 10 * s, 30 * s, 26 * s),
                 ellipse_mask(cx - 22 * s, cy + 4 * s, 18 * s), ellipse_mask(cx + 22 * s, cy + 4 * s, 18 * s))

def rays(I, cx, cy, r0, r1, n, col, w=5, a0=0):
    for a in range(n):
        ang = math.radians(a0 + a * 360 / n)
        I.layer(line_mask([(cx + r0 * math.cos(ang), cy + r0 * math.sin(ang)), (cx + r1 * math.cos(ang), cy + r1 * math.sin(ang))], w), col)

def icon(name):
    I = Icon()
    # ================= 德鲁伊 =================
    if name == 'AnimalFriendship':   # 安抚野兽：爪印上的心
        I.layer(ellipse_mask(64, 64, 56), MOSS)
        I.layer(paw_mask(64, 68, 1.5), FUR)
        I.layer(heart_mask(64, 76, 0.6), CREAM)
    elif name == 'Entangle':         # 纠缠术：地面钻出的藤蔓
        I.layer(rrect_mask(6, 104, 122, 122, 6), BARK)
        for x, bend in ((30, 1), (64, -1), (98, 1)):
            I.layer(line_mask([(x, 110), (x + bend * 14, 84), (x - bend * 10, 58), (x + bend * 12, 32), (x, 14)], 8), MOSS)
        for x, y, d in ((44, 70, 1), (54, 40, -1), (86, 74, -1), (110, 46, 1), (20, 50, -1)):
            I.layer(leaf_mask(x, y, x + d * 18, y - 14, 7), MOSS_D)
    elif name == 'ConjureAnimals':   # 召唤野兽：灵光中的狼头
        I.layer(ellipse_mask(64, 64, 56), MOSS_D)
        I.layer(wolf_mask(60, 66, 1.3), SLATE, glow=(170, 220, 160))
        I.layer(ellipse_mask(78, 56, 4), CREAM, shade=False, outline=False)
        for x, y in ((22, 26), (106, 30), (102, 102)):
            I.layer(poly_mask(star_pts(x, y, 11, 4, 4)), CREAM)
    elif name == 'Barkskin':         # 树肤术：树皮纹的盾 + 叶
        I.layer(poly_mask(shield_pts(64, 64, 86, 102)), BARK)
        for x in (44, 64, 84):
            I.layer(line_mask([(x, 30), (x + 6, 60), (x - 4, 96)], 4), darker(BARK, 0.6), outline=False, shade=False)
        I.layer(leaf_mask(64, 52, 96, 18, 13), MOSS)
    elif name == 'WildShape':        # 荒野变形：熊头
        I.layer(bear_mask(64, 66, 1.4), FUR)
        I.layer(ellipse_mask(64, 80, 18, 14), CREAM_D)
        I.layer(ellipse_mask(64, 74, 7, 5), INK, shade=False, outline=False)
        for x in (46, 82): I.layer(ellipse_mask(x, 56, 5), INK, shade=False, outline=False)
    elif name == 'PlantGrowth':      # 植物滋长：嫩芽 + 上升箭头
        I.layer(rrect_mask(10, 100, 118, 120, 6), BARK)
        I.layer(line_mask([(64, 104), (64, 50)], 8), MOSS)
        I.layer(leaf_mask(64, 66, 22, 44, 14), MOSS)
        I.layer(leaf_mask(64, 52, 104, 26, 14), MOSS)
        for x in (24, 104):
            I.layer(poly_mask([(x, 60), (x + 12, 76), (x + 5, 76), (x + 5, 94), (x - 5, 94), (x - 5, 76), (x - 12, 76)]), CREAM)
    elif name == 'BeastCommand':     # 兽王号令：怒吼的狼 + 声波
        I.layer(wolf_mask(48, 66, 1.1), FUR)
        for i, r in enumerate((26, 40)):
            I.layer(arc_mask(78, 64, r, -40, 40, 7), CREAM if i == 0 else RUST)
        I.layer(poly_mask(star_pts(102, 24, 14, 5, 5)), GOLD)
    elif name == 'CallLightning':    # 召雷术：雷云 + 闪电
        I.layer(cloud_mask(64, 34, 1.2), SLATE_D)
        I.layer(bolt_mask(64, 86, 1.1), CREAM, glow=(220, 230, 150))
    elif name == 'ControlWeather':   # 掌控天气：日、云、雨三分圆
        I.layer(ellipse_mask(64, 64, 58), MOSS_D)
        rays(I, 42, 44, 18, 30, 8, GOLD, 5)
        I.layer(ellipse_mask(42, 44, 14), GOLD)
        I.layer(cloud_mask(76, 66, 0.8), SLATE)
        for x in (60, 76, 92):
            I.layer(line_mask([(x, 88), (x - 6, 104)], 5), ICE)
    # ================= 圣武士 =================
    elif name == 'DivineSmite':      # 至圣斩：光芒中下劈的剑
        I.layer(poly_mask(star_pts(64, 64, 60, 40, 12)), GOLD)
        for m, c in sword_masks(64, 64, 90, L=110, blade_col=CREAM, hilt_col=GOLD_D): I.layer(m, c)
    elif name == 'LayOnHands':       # 圣疗：手掌 + 光
        rays(I, 64, 60, 44, 58, 10, GOLD, 5)
        I.layer(palm_mask(64, 70, 1.25), SKIN)
        I.layer(union(rrect_mask(58, 50, 70, 86, 3), rrect_mask(46, 62, 82, 74, 3)), CREAM, shade=False)
    elif name == 'VowOfEnmity':      # 誓仇：剑穿过的靶心
        I.layer(ellipse_mask(64, 64, 52), RUST)
        I.layer(ellipse_mask(64, 64, 34), CREAM)
        I.layer(ellipse_mask(64, 64, 16), RUST)
        for m, c in sword_masks(64, 64, -45, L=118, blade_col=SLATE, hilt_col=GOLD_D): I.layer(m, c)
    elif name == 'AuraOfProtection':  # 守护灵光：光环里的盾
        I.layer(ellipse_mask(64, 64, 58), GOLD)
        I.layer(ellipse_mask(64, 64, 46), GOLD_D, shade=False)
        I.layer(poly_mask(shield_pts(64, 64, 58, 70)), CREAM)
        I.layer(union(rrect_mask(59, 40, 69, 86, 3), rrect_mask(46, 52, 82, 62, 3)), GOLD, shade=False)
    elif name == 'SacredWeapon':     # 神圣武器：发光的竖剑
        for m, c in sword_masks(64, 60, -90, L=110, blade_col=CREAM, hilt_col=GOLD): I.layer(m, c, glow=(250, 230, 150))
        I.layer(poly_mask(star_pts(64, 34, 16, 5, 4)), CREAM, shade=False)
    elif name == 'AuraOfCourage':    # 勇气灵光：狮心（心 + 光芒）
        rays(I, 64, 64, 40, 58, 12, GOLD, 6)
        I.layer(heart_mask(64, 62, 1.4), RUST)
        I.layer(poly_mask(star_pts(64, 60, 14, 5, 5)), GOLD)
    elif name == 'Intercept':        # 援护换位：两个人像 + 交换箭头
        I.layer(bust_mask(34, 50, 0.75), SLATE)
        I.layer(bust_mask(94, 50, 0.75), CREAM)
        I.layer(arc_mask(64, 50, 34, 200, 340, 7), GOLD)
        I.layer(poly_mask([(92, 22), (104, 34), (88, 38)]), GOLD)
        I.layer(poly_mask(shield_pts(64, 100, 34, 36)), GOLD)
    elif name == 'DispelEvil':       # 放逐邪恶：圣光驱散的角魔
        I.layer(poly_mask(star_pts(64, 64, 62, 46, 16)), GOLD)
        horns = union(ellipse_mask(64, 66, 26, 24), poly_mask([(42, 54), (30, 22), (52, 46)]), poly_mask([(86, 54), (98, 22), (76, 46)]))
        horns = subtract(horns, union(eye_mask(54, 64, 7, 3), eye_mask(74, 64, 7, 3)))
        I.layer(horns, PLUM_D)
        I.layer(line_mask([(24, 104), (104, 24)], 10), CREAM)
    elif name == 'HolyAvatar':       # 圣光化身：带光翼的人像
        I.layer(wing_mask(54, 58, -1, 0.95), CREAM)
        I.layer(wing_mask(74, 58, 1, 0.95), CREAM)
        I.layer(ellipse_mask(64, 24, 16, 6), GOLD, shade=False)
        I.layer(bust_mask(64, 44, 0.9), GOLD)
    # ================= 术士 =================
    elif name == 'ChaosBolt':        # 混沌箭：四色拖尾的光弹
        for i, col in enumerate((RUST, ICE, VIOLET, GOLD)):
            I.layer(line_mask([(10 + i * 6, 118 - i * 16), (80, 48)], 8), col)
        I.layer(poly_mask(star_pts(86, 42, 34, 14, 7)), PLUM)
        I.layer(ellipse_mask(86, 42, 11), CREAM, shade=False)
    elif name == 'BurningHands':     # 燃烧之手：张开的手喷出扇形火
        I.layer(poly_mask([(56, 70), (124, 14), (124, 90)]), RUST)
        I.layer(poly_mask([(60, 66), (112, 34), (112, 76)]), GOLD, shade=False)
        I.layer(palm_mask(40, 82, 0.95, rot=-70), SKIN)
    elif name == 'CharmPerson':      # 魅惑人类：粉色心眼
        I.layer(ellipse_mask(64, 64, 56), PLUM)
        I.layer(eye_mask(64, 64, 42, 22), CREAM)
        I.layer(heart_mask(64, 62, 0.7), RUST)
    elif name == 'TwinSpell':        # 超魔·孪生：两颗相同的法术星
        I.layer(arc_mask(64, 64, 48, 200, 340, 6), PLUM)
        I.layer(arc_mask(64, 64, 48, 20, 160, 6), PLUM)
        I.layer(poly_mask(star_pts(40, 64, 26, 10, 4)), VIOLET)
        I.layer(poly_mask(star_pts(88, 64, 26, 10, 4)), VIOLET)
    elif name == 'DragonWings':      # 龙族之翼：一对龙翼
        I.layer(wing_mask(58, 70, -1, 1.05), PLUM)
        I.layer(wing_mask(70, 70, 1, 1.05), PLUM)
        I.layer(ellipse_mask(64, 74, 9, 22), PLUM_D)
    elif name == 'IceStorm':         # 冰风暴：云下的冰雹与雪花
        I.layer(cloud_mask(64, 32, 1.15), SLATE_D)
        for x, y in ((34, 78), (64, 96), (94, 76)):
            I.layer(snow_mask(x, y, 13, 5), ICE)
        for x, y in ((48, 112), (82, 112), (110, 100), (18, 100)):
            I.layer(ellipse_mask(x, y, 5), ICE)
    elif name == 'MassSuggestion':   # 群体暗示：三人像头顶的螺旋
        for x, sc in ((30, 0.6), (98, 0.6), (64, 0.75)):
            I.layer(bust_mask(x, 74 if sc < 0.7 else 66, sc), SLATE)
        for x, y, r in ((30, 36, 12), (98, 36, 12), (64, 24, 15)):
            I.layer(arc_mask(x, y, r, 0, 300, 5), PLUM)
            I.layer(ellipse_mask(x, y, r * 0.35), PLUM)
    elif name == 'EmpoweredSpell':   # 超魔·强效：法术星 + 上升箭头
        I.layer(ellipse_mask(64, 64, 56), PLUM_D)
        I.layer(poly_mask(star_pts(64, 74, 36, 14, 5)), VIOLET)
        I.layer(poly_mask([(64, 6), (84, 32), (72, 32), (72, 48), (56, 48), (56, 32), (44, 32)]), CREAM)
    elif name == 'SorcerousSurge':   # 魔涌：爆发的魔力旋涡
        I.layer(poly_mask(star_pts(64, 64, 62, 36, 9)), PLUM)
        for a in (0, 120, 240):
            I.layer(arc_mask(64, 64, 30, a, a + 100, 9), CREAM)
        I.layer(ellipse_mask(64, 64, 12), GOLD)
    # ================= 武僧 =================
    elif name == 'MartialArts':      # 武艺：拳 + 圆
        I.layer(ellipse_mask(64, 64, 56), OCHRE)
        I.layer(fist_mask(64, 64, 1.5), SKIN)
    elif name == 'FlurryOfBlows':    # 疾风连击：三个拳 + 速度线
        for i, (x, y) in enumerate(((40, 34), (70, 64), (100, 94))):
            I.layer(line_mask([(x - 30, y - 6), (x - 16, y - 2)], 5), CREAM)
            I.layer(fist_mask(x, y, 0.7), SKIN)
    elif name == 'DeflectMissiles':  # 拨挡箭矢：手掌拨开折断的箭
        I.layer(line_mask([(8, 30), (56, 52)], 6), BARK)
        I.layer(poly_mask([(4, 22), (18, 26), (8, 38)]), SLATE)
        I.layer(line_mask([(70, 64), (110, 110)], 6), BARK)
        I.layer(poly_mask([(110, 118), (122, 104), (106, 100)]), SLATE)
        I.layer(palm_mask(76, 50, 1.0, rot=30), SKIN)
        I.layer(arc_mask(64, 60, 50, 150, 210, 5), CREAM)
    elif name == 'StunningStrike':   # 震慑拳：拳 + 星芒
        I.layer(poly_mask(star_pts(78, 50, 46, 22, 8)), GOLD)
        I.layer(fist_mask(54, 74, 1.2), SKIN)
        for x, y in ((104, 98), (24, 26)):
            I.layer(poly_mask(star_pts(x, y, 11, 4, 4)), CREAM)
    elif name == 'StepOfTheWind':    # 乘风步：腾跃的人像 + 风痕
        I.layer(ellipse_mask(64, 64, 56), SLATE_D)
        for i, (y, x1) in enumerate(((46, 44), (66, 36), (86, 46))):
            I.layer(line_mask([(14, y + 6), (x1, y), (x1 + 10, y - 6)], 6), CREAM)
        I.layer(bust_mask(78, 38, 0.95).rotate(-25, resample=Image.BICUBIC, center=(78 * SS, 64 * SS)), OCHRE)
    elif name == 'OpenHand':         # 开掌技：推出的手掌 + 冲击弧
        I.layer(palm_mask(46, 70, 1.15, rot=-90), SKIN)
        for i, r in enumerate((24, 38)):
            I.layer(arc_mask(84, 64, r, -50, 50, 7), CREAM if i == 0 else OCHRE)
    elif name == 'DiamondSoul':      # 金刚之躯：钻石中的人像
        I.layer(poly_mask([(64, 6), (118, 50), (64, 122), (10, 50)]), ICE)
        I.layer(poly_mask([(64, 18), (100, 50), (64, 104), (28, 50)]), darker(ICE, 0.85), shade=False)
        I.layer(bust_mask(64, 42, 0.75), OCHRE)
    elif name == 'QuiveringPalm':    # 颤心掌：手掌 + 心跳波纹
        I.layer(ellipse_mask(64, 64, 56), RUST_D)
        I.layer(palm_mask(64, 74, 1.1), SKIN)
        I.layer(line_mask([(10, 64), (34, 64), (42, 46), (52, 82), (60, 54), (66, 64), (118, 64)], 5), CREAM)
    elif name == 'EmptyBody':        # 空灵之身：半透明的冥想人像
        I.layer(ellipse_mask(64, 64, 56), OCHRE_D)
        ghost = bust_mask(64, 40, 0.9)
        I.im.paste(Image.new('RGBA', (S, S), CREAM + (255,)), (0, 0), ghost.filter(ImageFilter.GaussianBlur(3 * SS)).point(lambda v: int(v * 0.55)))
        I.layer(arc_mask(64, 96, 30, 200, 340, 6), CREAM)
        for x, y in ((28, 30), (100, 28), (104, 96), (24, 98)):
            I.layer(ellipse_mask(x, y, 4), CREAM)
    else:
        raise KeyError(name)
    return I.done()

ABIL = {'Druid': ['AnimalFriendship', 'Entangle', 'ConjureAnimals', 'Barkskin', 'WildShape', 'PlantGrowth', 'BeastCommand', 'CallLightning', 'ControlWeather'],
        'Paladin': ['DivineSmite', 'LayOnHands', 'VowOfEnmity', 'AuraOfProtection', 'SacredWeapon', 'AuraOfCourage', 'Intercept', 'DispelEvil', 'HolyAvatar'],
        'Sorcerer': ['ChaosBolt', 'BurningHands', 'CharmPerson', 'TwinSpell', 'DragonWings', 'IceStorm', 'MassSuggestion', 'EmpoweredSpell', 'SorcerousSurge'],
        'Monk': ['MartialArts', 'FlurryOfBlows', 'DeflectMissiles', 'StunningStrike', 'StepOfTheWind', 'OpenHand', 'DiamondSoul', 'QuiveringPalm', 'EmptyBody']}

# ---------------- 路径背景（与 draw_class_art.py 同款边框） ----------------
BW, BH = 950, 1515
COLORS = {'Druid': ((14, 26, 12), (72, 110, 56), (170, 210, 130)),
          'Paladin': ((36, 30, 14), (170, 146, 80), (250, 230, 160)),
          'Sorcerer': ((30, 10, 30), (130, 56, 120), (230, 140, 220)),
          'Monk': ((36, 24, 10), (150, 104, 44), (236, 190, 110))}

def frame(cls):
    base, line, spark = COLORS[cls]
    rnd = random.Random(10 + list(COLORS).index(cls))
    bg = Image.new('RGBA', (BW, BH), base + (255,)); d = ImageDraw.Draw(bg)
    for _ in range(3000):
        x, y = rnd.randrange(BW), rnd.randrange(BH); v = rnd.randint(-6, 6)
        d.point((x, y), fill=tuple(max(0, min(255, c + v)) for c in base) + (255,))
    d.rounded_rectangle([54, 54, BW - 54, BH - 54], radius=26, fill=(10, 8, 6, 255))
    for inset, w in ((22, 4), (40, 2)):
        d.rounded_rectangle([inset, inset, BW - inset, BH - inset], radius=30, outline=line + (255,), width=w)
    for i in range(25):
        t = i / 24
        for (x, y) in ((31, 90 + t * (BH - 180)), (BW - 31, 90 + t * (BH - 180))):
            d.ellipse([x - 4, y - 4, x + 4, y + 4], fill=line + (255,))
    for i in range(15):
        t = i / 14
        for (x, y) in ((90 + t * (BW - 180), 31), (90 + t * (BW - 180), BH - 31)):
            d.ellipse([x - 4, y - 4, x + 4, y + 4], fill=line + (255,))
    for (cx, cy) in ((BW / 2, 31), (BW / 2, BH - 31), (31, BH / 2), (BW - 31, BH / 2)):
        d.ellipse([cx - 26, cy - 11, cx + 26, cy + 11], outline=line + (255,), width=3, fill=base + (255,))
        d.ellipse([cx - 7, cy - 7, cx + 7, cy + 7], outline=line + (255,), width=3)
    for _ in range(140):
        x, y = rnd.randrange(80, BW - 80), rnd.randrange(80, BH - 80); r = rnd.choice((1, 1, 1, 2, 2, 3))
        d.ellipse([x - r, y - r, x + r, y + r], fill=spark + (rnd.randint(90, 200),))
    for _ in range(10):
        x, y = rnd.randrange(120, BW - 120), rnd.randrange(120, BH - 120)
        d.polygon(star_pts(x, y, 14, 4, 4), fill=spark + (200,))
    return bg, base, line

def sil():
    return Image.new('L', (BW, BH), 0)

def background(cls):
    bg, base, line = frame(cls)
    parts = []
    if cls == 'Druid':      # 大树 + 下方新月与鹿角
        m = sil(); d = ImageDraw.Draw(m)
        d.polygon([(445, 1250), (505, 1250), (520, 760), (430, 760)], fill=255)
        for (x, y, r) in ((475, 480, 230), (300, 620, 160), (650, 620, 160), (380, 360, 140), (570, 360, 140), (475, 700, 170)):
            d.ellipse([x - r, y - r, x + r, y + r], fill=255)
        for sx in (-1, 1):
            d.line([(475, 1150), (475 + sx * 170, 1250)], fill=255, width=40); d.line([(475, 1100), (475 + sx * 140, 1190)], fill=255, width=34)
        parts.append(m)
        m = sil(); d = ImageDraw.Draw(m)
        d.ellipse([360, 1270, 590, 1500 - 60], fill=255); d.ellipse([400, 1250, 630, 1420], fill=0)
        parts.append(m)
    elif cls == 'Paladin':  # 竖剑 + 盾 + 日轮
        m = sil(); d = ImageDraw.Draw(m)
        d.ellipse([475 - 170, 330 - 170, 475 + 170, 330 + 170], fill=255)
        for a in range(16):
            ang = math.radians(a * 22.5); R = 260 if a % 2 == 0 else 225
            d.line([(475 + 190 * math.cos(ang), 330 + 190 * math.sin(ang)), (475 + R * math.cos(ang), 330 + R * math.sin(ang))], fill=255, width=22)
        parts.append(m)
        m = sil(); ImageDraw.Draw(m).polygon([(475, 600), (700, 650), (700, 930), (630, 1100), (475, 1230), (320, 1100), (250, 930), (250, 650)], fill=255)
        parts.append(m)
        m = sil(); d = ImageDraw.Draw(m)
        d.polygon([(450, 1320), (500, 1320), (500, 560), (475, 500), (450, 560)], fill=255)
        d.line([(340, 1250), (610, 1250)], fill=255, width=44); d.ellipse([445, 1370, 505, 1430], fill=255); d.line([(475, 1270), (475, 1380)], fill=255, width=34)
        parts.append(m)
    elif cls == 'Sorcerer':  # 龙首剪影 + 环绕的法术星
        m = sil(); d = ImageDraw.Draw(m)
        d.polygon([(300, 1150), (330, 800), (400, 640), (350, 470), (450, 590), (520, 470), (540, 620), (700, 720), (720, 790), (560, 800), (520, 1150)], fill=255)
        parts.append(m)
        m = sil(); d = ImageDraw.Draw(m)
        for i, (x, y, r) in enumerate(((220, 380, 56), (740, 420, 56), (180, 900, 44), (780, 950, 48), (300, 1290, 50), (650, 1290, 50), (475, 250, 70))):
            d.polygon(star_pts(x, y, r, r * 0.4, 4 if i % 2 else 5), fill=255)
        parts.append(m)
    else:                    # 武僧：拳 + 外圈太极式双弧
        m = sil(); d = ImageDraw.Draw(m)
        d.arc([175, 460, 775, 1060], 200, 340, fill=255, width=46); d.arc([175, 460, 775, 1060], 20, 160, fill=255, width=46)
        parts.append(m)
        m = sil(); d = ImageDraw.Draw(m)
        d.rounded_rectangle([355, 700, 595, 900], radius=50, fill=255)
        for i in range(4):
            x = 355 + i * 60; d.rounded_rectangle([x + 4, 620, x + 56, 740], radius=26, fill=255)
        d.rounded_rectangle([315, 730, 400, 840], radius=30, fill=255)
        d.rounded_rectangle([395, 890, 555, 1220], radius=30, fill=255)
        parts.append(m)
        m = sil(); ImageDraw.Draw(m).polygon(star_pts(475, 300, 90, 40, 8), fill=255); parts.append(m)
    allm = parts[0]
    for m in parts[1:]: allm = ImageChops.lighter(allm, m)
    glow = allm.filter(ImageFilter.MaxFilter(31)).filter(ImageFilter.GaussianBlur(22))
    bg.paste(Image.new('RGBA', (BW, BH), line + (255,)), (0, 0), glow.point(lambda v: int(v * 0.55)))
    for m in parts:
        bg.paste(Image.new('RGBA', (BW, BH), line + (255,)), (0, 0), m.filter(ImageFilter.MaxFilter(9)))
        bg.paste(Image.new('RGBA', (BW, BH), (6, 4, 4, 255)), (0, 0), m)
    return bg

if __name__ == '__main__':
    for cls, names in ABIL.items():
        os.makedirs(os.path.join(OUT, cls), exist_ok=True)
        for n in names:
            icon(n).save(os.path.join(OUT, cls, n + '.png'))
        background(cls).save(os.path.join(OUT, cls, 'Background.png'))
    print('icons written to', OUT)
