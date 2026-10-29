# 1.27.4 DND 职业路径美术（RimWorld / VPE 画风）：
#   27 个能力图标 128×128（粗黑描边、平涂 + 右下暗部，配色参考 VPE 的卡其/青灰/铁锈/米白）；
#   3 张路径背景 950×1515（VPE 的塔罗牌式深色底 + 剪影）；冒险者手册物品贴图 64×64。
# 用法：python draw_class_art.py <输出目录 Bridges/VPE/Textures/LP/Classes>
# 1.27.0 的第一版是简单几何图形，Peter 2026-10-25 要求改成更像 RimWorld 的画风，故重写。
import math, sys, os
from PIL import Image, ImageDraw, ImageFilter, ImageChops

OUT = sys.argv[1] if len(sys.argv) > 1 else 'out'
SS = 4            # 超采样倍数
S = 128 * SS      # 工作画布
OW = 6 * SS       # 描边宽度（成品约 6px）

# VPE 风格调色板
INK = (22, 20, 18)
CREAM = (214, 208, 160); CREAM_D = (176, 170, 124)
KHAKI = (160, 152, 96);  KHAKI_D = (112, 106, 62)
SLATE = (152, 162, 184); SLATE_D = (104, 114, 136)
RUST = (152, 76, 46);    RUST_D = (100, 46, 28)
VIOLET = (142, 122, 172); VIOLET_D = (96, 80, 122)
GOLD = (206, 172, 84);   GOLD_D = (150, 120, 50)
BONE = (226, 222, 196);  BONE_D = (182, 176, 148)
STONE = (128, 124, 110); STONE_D = (86, 82, 72)

def mask():
    return Image.new('L', (S, S), 0)

def P(*pts):  # 以 128 坐标写，内部放大
    return [(x * SS, y * SS) for x, y in pts]

def poly_mask(pts):
    m = mask(); ImageDraw.Draw(m).polygon(P(*pts), fill=255); return m

def ellipse_mask(cx, cy, rx, ry=None):
    ry = rx if ry is None else ry
    m = mask(); ImageDraw.Draw(m).ellipse([(cx - rx) * SS, (cy - ry) * SS, (cx + rx) * SS, (cy + ry) * SS], fill=255); return m

def line_mask(pts, w):
    m = mask(); d = ImageDraw.Draw(m)
    d.line(P(*pts), fill=255, width=int(w * SS), joint='curve')
    for x, y in pts:  # 圆头
        d.ellipse([(x - w / 2) * SS, (y - w / 2) * SS, (x + w / 2) * SS, (y + w / 2) * SS], fill=255)
    return m

def arc_mask(cx, cy, r, a0, a1, w):
    m = mask(); d = ImageDraw.Draw(m)
    d.arc([(cx - r) * SS, (cy - r) * SS, (cx + r) * SS, (cy + r) * SS], a0, a1, fill=255, width=int(w * SS))
    return m

def rrect_mask(x0, y0, x1, y1, r):
    m = mask(); ImageDraw.Draw(m).rounded_rectangle([x0 * SS, y0 * SS, x1 * SS, y1 * SS], radius=r * SS, fill=255); return m

def union(*ms):
    out = ms[0]
    for m in ms[1:]: out = ImageChops.lighter(out, m)
    return out

def subtract(a, b):
    return ImageChops.subtract(a, b)

def star_pts(cx, cy, r1, r2, n, rot=-math.pi / 2):
    pts = []
    for i in range(2 * n):
        r = r1 if i % 2 == 0 else r2; a = rot + i * math.pi / n
        pts.append((cx + r * math.cos(a), cy + r * math.sin(a)))
    return pts

def bust_mask(cx, cy, sc=1.0):
    """VPE 式人像剪影：圆头 + 圆肩身体。cy 为头心。"""
    head = ellipse_mask(cx, cy, 11 * sc)
    body = rrect_mask(cx - 17 * sc, cy + 12 * sc, cx + 17 * sc, cy + 48 * sc, 12 * sc)
    return union(head, body)

def shield_pts(cx, cy, w=46, h=56):
    return [(cx - w / 2, cy - h / 2), (cx + w / 2, cy - h / 2), (cx + w / 2, cy - h / 2 + h * 0.45),
            (cx + w * 0.3, cy + h / 2 * 0.8), (cx, cy + h / 2), (cx - w * 0.3, cy + h / 2 * 0.8), (cx - w / 2, cy - h / 2 + h * 0.45)]

def sword_masks(cx, cy, ang_deg, L=52, blade_col=CREAM, hilt_col=RUST):
    """返回 [(mask, fill), ...]：剑刃 + 护手/剑柄/剑首。ang 为剑尖方向（屏幕坐标，-90 = 向上）。"""
    a = math.radians(ang_deg); dx, dy = math.cos(a), math.sin(a); px, py = -dy, dx
    tip = (cx + dx * L * 0.55, cy + dy * L * 0.55); base = (cx - dx * L * 0.15, cy - dy * L * 0.15)
    bw = 6
    blade = poly_mask([(base[0] + px * bw, base[1] + py * bw), (tip[0] - dx * 10 + px * bw, tip[1] - dy * 10 + py * bw),
                       tip, (tip[0] - dx * 10 - px * bw, tip[1] - dy * 10 - py * bw), (base[0] - px * bw, base[1] - py * bw)])
    guard = line_mask([(base[0] + px * 13, base[1] + py * 13), (base[0] - px * 13, base[1] - py * 13)], 6)
    grip = line_mask([base, (cx - dx * L * 0.42, cy - dy * L * 0.42)], 5)
    pommel = ellipse_mask(cx - dx * L * 0.45, cy - dy * L * 0.45, 4.5)
    return [(blade, blade_col), (union(guard, grip, pommel), hilt_col)]

def darker(c, f=0.72):
    return tuple(int(v * f) for v in c)

class Icon:
    def __init__(self):
        self.im = Image.new('RGBA', (S, S), (0, 0, 0, 0))
    def layer(self, m, fill, shade=True, outline=True, glow=None):
        """按 VPE 风格叠一层：黑描边 → 平涂 → 右下暗部。"""
        if glow:
            g = m.filter(ImageFilter.MaxFilter(OW * 2 + 1)).filter(ImageFilter.GaussianBlur(OW))
            self.im.paste(Image.new('RGBA', (S, S), glow + (255,)), (0, 0), g.point(lambda v: int(v * 0.6)))
        if outline:
            o = m.filter(ImageFilter.MaxFilter(OW + 1))
            self.im.paste(Image.new('RGBA', (S, S), INK + (255,)), (0, 0), o)
        self.im.paste(Image.new('RGBA', (S, S), fill + (255,)), (0, 0), m)
        if shade:
            d = 5 * SS
            shifted = ImageChops.offset(m, -d, -d)
            band = subtract(m, shifted)
            self.im.paste(Image.new('RGBA', (S, S), darker(fill) + (255,)), (0, 0), band)
        return self
    def done(self, size=128):
        return self.im.resize((size, size), Image.LANCZOS)

# ---------------- 27 个能力图标 ----------------
def icon(name):
    I = Icon()
    if name == 'SecondWind':     # 回气：人像 + 两侧上升的风 + 向上箭头
        I.layer(bust_mask(64, 56), SLATE)
        for sx in (-1, 1):
            I.layer(line_mask([(64 + sx * 26, 100), (64 + sx * 40, 80), (64 + sx * 30, 62), (64 + sx * 44, 42)], 7), CREAM)
        I.layer(poly_mask([(64, 8), (80, 28), (71, 28), (71, 40), (57, 40), (57, 28), (48, 28)]), CREAM)
    elif name == 'BattleCry':    # 战吼：人像 + 声波
        I.layer(bust_mask(40, 54), SLATE)
        for i, r in enumerate((30, 46, 62)):
            I.layer(arc_mask(44, 58, r, -42, 42, 8), CREAM if i % 2 == 0 else RUST)
    elif name == 'Trip':         # 绊摔：扫腿 + 倒下的人像 + 方向箭头
        I.layer(line_mask([(12, 104), (116, 96)], 10), RUST)
        tilted = bust_mask(64, 40).rotate(-60, resample=Image.BICUBIC, center=(64 * SS, 72 * SS))
        I.layer(tilted, SLATE)
        I.layer(poly_mask([(96, 28), (118, 44), (96, 60), (96, 51), (76, 51), (76, 37), (96, 37)]), CREAM)
    elif name == 'ActionSurge':  # 动作如潮：爆发 + 交叉双剑
        I.layer(poly_mask(star_pts(64, 64, 62, 38, 10)), RUST)
        for a in (-45, -135):
            for m, c in sword_masks(64, 64, a, L=96): I.layer(m, c)
    elif name == 'WeaponMastery':  # 武器专精：星徽 + 竖剑
        I.layer(poly_mask(star_pts(64, 78, 42, 19, 5)), KHAKI)
        for m, c in sword_masks(64, 60, -90, L=104): I.layer(m, c)
    elif name == 'Indomitable':  # 不屈意志：盾 + 挺立人像
        I.layer(poly_mask(shield_pts(64, 64, 86, 102)), KHAKI)
        I.layer(bust_mask(64, 44, 0.9), CREAM)
    elif name == 'Whirlwind':    # 旋风斩：三片弧刃 + 中心
        for a in (0, 120, 240):
            I.layer(arc_mask(64, 64, 42, a, a + 95, 14), CREAM)
        I.layer(ellipse_mask(64, 64, 17), RUST)
    elif name == 'GuardStance':  # 守护姿态：双盾 + 被护的人
        I.layer(poly_mask(shield_pts(46, 72, 62, 76)), SLATE)
        I.layer(poly_mask(shield_pts(80, 60, 62, 76)), KHAKI)
        I.layer(bust_mask(80, 42, 0.6), CREAM)
    elif name == 'SurgeOfBattle':  # 战意再临：燃烧的剑
        flame = poly_mask([(64, 4), (86, 30), (78, 44), (100, 58), (86, 82), (64, 74), (42, 82), (28, 58), (50, 44), (42, 30)])
        I.layer(flame, RUST)
        I.layer(poly_mask([(64, 26), (76, 44), (72, 54), (64, 62), (56, 54), (52, 44)]), GOLD, shade=False)
        for m, c in sword_masks(64, 86, -90, L=100): I.layer(m, c)
    elif name == 'MagicMissile':  # 魔法飞弹：三支带尾迹的光矢
        for (x0, y0) in ((14, 112), (8, 84), (36, 120)):
            x1, y1 = x0 + 80, y0 - 72
            I.layer(line_mask([(x0, y0), (x1 - 14, y1 + 13)], 7), VIOLET)
            I.layer(poly_mask(star_pts(x1, y1, 14, 5, 4)), CREAM)
    elif name == 'MageArmor':    # 法师护甲：光环中的人像
        I.layer(ellipse_mask(64, 64, 56), VIOLET)
        I.layer(ellipse_mask(64, 64, 42), SLATE_D, shade=False)
        I.layer(bust_mask(64, 50, 0.85), SLATE)
    elif name == 'Fireball':     # 火球术：拖尾的火球
        I.layer(poly_mask([(96, 32), (34, 56), (10, 106), (56, 86), (86, 98)]), RUST)
        I.layer(ellipse_mask(86, 42, 32), GOLD)
        I.layer(ellipse_mask(92, 36, 14), CREAM, shade=False, outline=False)
    elif name == 'Sleep':        # 睡眠术：躺倒的人 + Z
        I.layer(rrect_mask(8, 86, 120, 106, 8), KHAKI_D)
        I.layer(bust_mask(64, 50).rotate(90, resample=Image.BICUBIC, center=(64 * SS, 74 * SS)), SLATE)
        for (x, y, sc) in ((66, 12, 1.0), (92, 30, 0.75), (106, 8, 0.55)):
            w = 16 * sc; h = 18 * sc
            z = union(line_mask([(x, y), (x + w, y)], 5 * sc), line_mask([(x + w, y), (x, y + h)], 5 * sc), line_mask([(x, y + h), (x + w, y + h)], 5 * sc))
            I.layer(z, CREAM)
    elif name == 'LightningBolt':  # 闪电束（带紫色辉光）
        I.layer(poly_mask([(72, 4), (32, 66), (58, 66), (42, 124), (98, 50), (70, 50), (92, 4)]), CREAM, glow=VIOLET)
    elif name == 'Haste':        # 急速术：前倾人像 + 速度线
        for i, y in enumerate((46, 64, 82)):
            I.layer(line_mask([(8, y), (34 + (i % 2) * 10, y)], 6), CREAM)
        I.layer(bust_mask(82, 44).rotate(-15, resample=Image.BICUBIC, center=(82 * SS, 70 * SS)), SLATE)
    elif name == 'WallOfStone':  # 石墙术：拔地而起的砖墙 + 法术星
        I.layer(poly_mask([(6, 110), (122, 100), (122, 122), (6, 122)]), KHAKI_D)
        for row in range(3):
            y = 92 - row * 24; off = (row % 2) * 16
            for col in range(5):
                x0 = 8 + off + col * 32 - 24
                if x0 + 30 < 8 or x0 > 120: continue
                I.layer(rrect_mask(max(6, x0), y - 18, min(122, x0 + 30), y + 2, 3), STONE)
        I.layer(poly_mask(star_pts(100, 22, 13, 4.5, 4)), VIOLET)
    elif name == 'Teleport':     # 传送术：两个传送环 + 人像
        I.layer(ellipse_mask(36, 94, 30, 12), VIOLET)
        I.layer(ellipse_mask(36, 94, 16, 6), VIOLET_D, shade=False)
        I.layer(line_mask([(48, 78), (70, 60)], 7), CREAM)
        I.layer(poly_mask([(68, 70), (86, 44), (60, 52)]), CREAM)
        I.layer(ellipse_mask(92, 56, 30, 12), VIOLET)
        I.layer(bust_mask(92, 22, 0.7), SLATE)
    elif name == 'Disintegrate':  # 解离术：射线 + 崩解的人
        I.layer(line_mask([(8, 110), (84, 46)], 12), VIOLET)
        I.layer(line_mask([(8, 110), (84, 46)], 4), CREAM, outline=False, shade=False)
        body = bust_mask(94, 34, 0.8)
        holes = union(*[ellipse_mask(x, y, r) for x, y, r in ((104, 74, 7), (90, 68, 5), (110, 58, 4), (84, 56, 3))])
        I.layer(subtract(body, holes), SLATE)
        for x, y, r in ((116, 92, 4), (122, 80, 3), (108, 100, 3), (120, 108, 2.5)):
            I.layer(ellipse_mask(x, y, r), SLATE)
    elif name == 'CureWounds':   # 治疗伤口：圆盘上的十字
        I.layer(ellipse_mask(64, 64, 56), KHAKI)
        I.layer(union(rrect_mask(50, 22, 78, 106, 5), rrect_mask(22, 50, 106, 78, 5)), CREAM)
    elif name == 'Bless':        # 祝福：星光笼罩的人像
        for a in range(8):
            ang = math.radians(a * 45 + 22.5)
            I.layer(line_mask([(64 + 30 * math.cos(ang), 34 + 30 * math.sin(ang)), (64 + 50 * math.cos(ang), 34 + 50 * math.sin(ang))], 5), GOLD)
        I.layer(bust_mask(64, 62, 0.9), SLATE)
        I.layer(poly_mask(star_pts(64, 32, 24, 9, 4)), CREAM)
    elif name == 'LesserRestoration':  # 次级复原术：圆盘 + 叶
        I.layer(ellipse_mask(64, 64, 56), KHAKI)
        leaf = []
        for i in range(0, 25):
            t = i / 24; ax = 36 + t * 56; ay = 94 - t * 56; w = 30 * math.sin(math.pi * t) ** 0.8
            leaf.append((ax + w * 0.707, ay + w * 0.707))
        for i in range(24, -1, -1):
            t = i / 24; ax = 36 + t * 56; ay = 94 - t * 56; w = 30 * math.sin(math.pi * t) ** 0.8
            leaf.append((ax - w * 0.707, ay - w * 0.707))
        I.layer(poly_mask(leaf), CREAM)
        I.layer(line_mask([(40, 90), (88, 42)], 4), KHAKI_D, outline=False, shade=False)
        for t in (0.35, 0.55, 0.75):
            ax = 36 + t * 56; ay = 94 - t * 56
            I.layer(line_mask([(ax, ay), (ax + 10, ay + 2)], 3), KHAKI_D, outline=False, shade=False)
            I.layer(line_mask([(ax, ay), (ax - 2, ay - 10)], 3), KHAKI_D, outline=False, shade=False)
    elif name == 'Guidance':     # 神导术：指路星 + 人像
        I.layer(bust_mask(64, 68, 0.9), SLATE)
        I.layer(poly_mask(star_pts(64, 34, 32, 10, 4)), GOLD)
        I.layer(poly_mask(star_pts(64, 34, 15, 5, 4, rot=-math.pi / 4)), CREAM, shade=False)
    elif name == 'SacredFlame':  # 神圣打击：从天而降的圣火
        I.layer(poly_mask([(64, 4), (92, 40), (82, 54), (106, 72), (88, 112), (64, 124), (40, 112), (22, 72), (46, 54), (36, 40)]), GOLD)
        I.layer(poly_mask([(64, 50), (78, 72), (74, 90), (64, 102), (54, 90), (50, 72)]), CREAM, shade=False)
    elif name == 'TurnUndead':   # 驱散亡灵：日芒 + 骷髅 + 禁止斜杠
        I.layer(poly_mask(star_pts(64, 64, 62, 46, 12)), GOLD)
        skull = union(ellipse_mask(64, 56, 27, 25), rrect_mask(48, 72, 80, 94, 6))
        skull = subtract(skull, union(ellipse_mask(54, 56, 7), ellipse_mask(74, 56, 7), poly_mask([(64, 66), (69, 76), (59, 76)])))
        I.layer(skull, BONE)
        I.layer(line_mask([(24, 102), (104, 26)], 10), RUST)
    elif name == 'MassHeal':     # 群体治疗：三个十字
        I.layer(ellipse_mask(64, 64, 58), KHAKI)
        for (x, y, s) in ((42, 46, 14), (86, 46, 14), (64, 88, 14)):
            I.layer(union(rrect_mask(x - 5, y - s, x + 5, y + s, 3), rrect_mask(x - s, y - 5, x + s, y + 5, 3)), CREAM)
    elif name == 'DeathWard':    # 死亡守卫：盾中骷髅 + 金环
        I.layer(poly_mask(shield_pts(64, 64, 86, 102)), KHAKI_D)
        I.layer(ellipse_mask(64, 60, 31), GOLD, shade=False)
        skull = union(ellipse_mask(64, 56, 20, 19), rrect_mask(52, 66, 76, 82, 4))
        skull = subtract(skull, union(ellipse_mask(57, 56, 5), ellipse_mask(71, 56, 5)))
        I.layer(skull, BONE)
    elif name == 'Raise':        # 复生术：墓碑前升起的人像
        for a in range(5):
            ang = math.radians(200 + a * 35)
            I.layer(line_mask([(64 + 32 * math.cos(ang), 48 + 32 * math.sin(ang)), (64 + 52 * math.cos(ang), 48 + 52 * math.sin(ang))], 5), GOLD)
        I.layer(union(rrect_mask(28, 50, 100, 118, 4), ellipse_mask(64, 52, 36, 26)), STONE)
        I.layer(bust_mask(64, 52, 0.8), CREAM)
        I.layer(rrect_mask(16, 108, 112, 124, 4), STONE_D)
    return I.done()

ABIL = {'Fighter': ['SecondWind', 'BattleCry', 'Trip', 'ActionSurge', 'WeaponMastery', 'Indomitable', 'Whirlwind', 'GuardStance', 'SurgeOfBattle'],
        'Wizard': ['MagicMissile', 'MageArmor', 'Fireball', 'Sleep', 'LightningBolt', 'Haste', 'WallOfStone', 'Teleport', 'Disintegrate'],
        'Cleric': ['CureWounds', 'Bless', 'LesserRestoration', 'Guidance', 'SacredFlame', 'TurnUndead', 'MassHeal', 'DeathWard', 'Raise']}

# ---------------- 路径背景（VPE 塔罗牌式 950×1515） ----------------
BW, BH = 950, 1515
def background(cls):
    import random
    base, line, spark = {'Fighter': ((38, 14, 10), (120, 50, 34), (210, 110, 70)),
                         'Wizard': ((12, 12, 34), (56, 54, 120), (150, 140, 230)),
                         'Cleric': ((34, 28, 10), (120, 100, 44), (230, 200, 110))}[cls]
    rnd = random.Random({'Fighter': 1, 'Wizard': 2, 'Cleric': 3}[cls])
    bg = Image.new('RGBA', (BW, BH), base + (255,))
    d = ImageDraw.Draw(bg)
    for _ in range(3000):  # 细微颗粒
        x, y = rnd.randrange(BW), rnd.randrange(BH); v = rnd.randint(-6, 6)
        d.point((x, y), fill=tuple(max(0, min(255, c + v)) for c in base) + (255,))
    d.rounded_rectangle([54, 54, BW - 54, BH - 54], radius=26, fill=(10, 8, 6, 255))  # 内框
    for inset, w in ((22, 4), (40, 2)):  # 双线边框
        d.rounded_rectangle([inset, inset, BW - inset, BH - inset], radius=30, outline=line + (255,), width=w)
    for i in range(25):
        t = i / 24
        for (x, y) in ((31, 90 + t * (BH - 180)), (BW - 31, 90 + t * (BH - 180))):
            d.ellipse([x - 4, y - 4, x + 4, y + 4], fill=line + (255,))
    for i in range(15):
        t = i / 14
        for (x, y) in ((90 + t * (BW - 180), 31), (90 + t * (BW - 180), BH - 31)):
            d.ellipse([x - 4, y - 4, x + 4, y + 4], fill=line + (255,))
    for (cx, cy) in ((BW / 2, 31), (BW / 2, BH - 31), (31, BH / 2), (BW - 31, BH / 2)):  # 眼形纹
        d.ellipse([cx - 26, cy - 11, cx + 26, cy + 11], outline=line + (255,), width=3, fill=base + (255,))
        d.ellipse([cx - 7, cy - 7, cx + 7, cy + 7], outline=line + (255,), width=3)
    for _ in range(140):  # 星点
        x, y = rnd.randrange(80, BW - 80), rnd.randrange(80, BH - 80); r = rnd.choice((1, 1, 1, 2, 2, 3))
        d.ellipse([x - r, y - r, x + r, y + r], fill=spark + (rnd.randint(90, 200),))
    for _ in range(10):
        x, y = rnd.randrange(120, BW - 120), rnd.randrange(120, BH - 120)
        d.polygon(star_pts(x, y, 14, 4, 4), fill=spark + (200,))
    # 中央剪影：按层绘制，每层各自描边，整体加辉光
    def sil():
        return Image.new('L', (BW, BH), 0)
    parts = []
    if cls == 'Fighter':   # 大盾 + 盾后交叉双剑 + 顶部星
        sw = sil(); sd = ImageDraw.Draw(sw)
        for ang in (-50, -130):
            a = math.radians(ang); dx, dy = math.cos(a), math.sin(a); px, py = -dy, dx
            cx, cy, L = 475, 740, 1150
            tip = (cx + dx * L * .55, cy + dy * L * .55); b = (cx - dx * L * .15, cy - dy * L * .15)
            sd.polygon([(b[0] + px * 40, b[1] + py * 40), (tip[0] - dx * 60 + px * 40, tip[1] - dy * 60 + py * 40), tip,
                        (tip[0] - dx * 60 - px * 40, tip[1] - dy * 60 - py * 40), (b[0] - px * 40, b[1] - py * 40)], fill=255)
            sd.line([(b[0] + px * 110, b[1] + py * 110), (b[0] - px * 110, b[1] - py * 110)], fill=255, width=44)
            sd.line([b, (cx - dx * L * .42, cy - dy * L * .42)], fill=255, width=38)
            e = (cx - dx * L * .45, cy - dy * L * .45); sd.ellipse([e[0] - 36, e[1] - 36, e[0] + 36, e[1] + 36], fill=255)
        parts.append(sw)
        sh = sil(); ImageDraw.Draw(sh).polygon([(475, 400), (715, 460), (715, 790), (640, 990), (475, 1140), (310, 990), (235, 790), (235, 460)], fill=255)
        parts.append(sh)
        st = sil(); ImageDraw.Draw(st).polygon(star_pts(475, 200, 90, 40, 5), fill=255); parts.append(st)
    elif cls == 'Wizard':  # 法杖 + 宝珠 + 星月
        m = sil(); sd = ImageDraw.Draw(m)
        sd.line([(475, 1300), (475, 520)], fill=255, width=46)
        sd.polygon([(475, 540), (560, 440), (475, 300), (390, 440)], fill=255)
        sd.ellipse([475 - 95, 300 - 95, 475 + 95, 300 + 95], fill=255)
        parts.append(m)
        m = sil(); sd = ImageDraw.Draw(m)
        for i, (x, y, r) in enumerate(((250, 420, 60), (700, 420, 60), (190, 760, 44), (760, 760, 44), (300, 1080, 50), (650, 1080, 50))):
            sd.polygon(star_pts(x, y, r, r * 0.4, 4 if i % 2 else 5), fill=255)
        sd.ellipse([150, 1160, 350, 1360], fill=255); sd.ellipse([200, 1140, 390, 1330], fill=0)
        parts.append(m)
    else:                  # 圣徽：日轮 + 圣杯
        m = sil(); sd = ImageDraw.Draw(m)
        sd.ellipse([475 - 230, 520 - 230, 475 + 230, 520 + 230], fill=255)
        for a in range(16):
            ang = math.radians(a * 22.5); R = 330 if a % 2 == 0 else 290
            sd.line([(475 + 250 * math.cos(ang), 520 + 250 * math.sin(ang)), (475 + R * math.cos(ang), 520 + R * math.sin(ang))], fill=255, width=26)
        parts.append(m)
        m = sil(); ImageDraw.Draw(m).polygon([(475, 880), (600, 880), (600, 960), (560, 1000), (560, 1180), (640, 1240), (640, 1280), (310, 1280), (310, 1240), (390, 1180), (390, 1000), (350, 960), (350, 880)], fill=255)
        parts.append(m)
    allm = parts[0]
    for m in parts[1:]: allm = ImageChops.lighter(allm, m)
    glow = allm.filter(ImageFilter.MaxFilter(31)).filter(ImageFilter.GaussianBlur(22))
    bg.paste(Image.new('RGBA', (BW, BH), line + (255,)), (0, 0), glow.point(lambda v: int(v * 0.55)))
    for m in parts:
        bg.paste(Image.new('RGBA', (BW, BH), line + (255,)), (0, 0), m.filter(ImageFilter.MaxFilter(9)))
        bg.paste(Image.new('RGBA', (BW, BH), (6, 4, 4, 255)), (0, 0), m)
    if cls == 'Cleric':    # 日轮中的十字镂空
        cd = ImageDraw.Draw(bg)
        cd.rectangle([445, 350, 505, 690], fill=base + (255,)); cd.rectangle([305, 490, 645, 550], fill=base + (255,))
    return bg

# ---------------- 冒险者手册 64×64 ----------------
def manual():
    I = Icon()
    I.layer(poly_mask([(26, 18), (104, 10), (110, 100), (32, 110)]), RUST)
    I.layer(poly_mask([(36, 26), (100, 20), (104, 92), (40, 100)]), BONE)
    for y0, y1, x1 in ((40, 38, 86), (54, 52, 86), (68, 66, 74)):
        I.layer(line_mask([(50, y0), (x1, y1)], 4), RUST_D, outline=False, shade=False)
    I.layer(poly_mask(star_pts(84, 80, 11, 4.5, 5)), GOLD)
    I.layer(rrect_mask(18, 16, 30, 112, 4), RUST_D)
    return I.done(64)

if __name__ == '__main__':
    for cls, names in ABIL.items():
        os.makedirs(os.path.join(OUT, cls), exist_ok=True)
        for n in names:
            icon(n).save(os.path.join(OUT, cls, n + '.png'))
        background(cls).save(os.path.join(OUT, 'Background_' + cls + '.png'))
    manual().save(os.path.join(OUT, 'Manual.png'))
    print('icons written to', OUT)
