# 新职业（野蛮人 / 游侠 / 游荡者）的美术：21 个能力图标 + 6 个传奇图标（128×128）+ 3 张职业背景（950×1515）+ 预览图。
# 画法沿用 draw_class_art.py / draw_class_art2.py / draw_legendary_174.py：粗黑描边、平涂 + 右下暗部、低饱和；传奇图标外圈暗金环（被动断环）。
# 用法：python draw_classes3.py <模组根目录>
import math, sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import draw_class_art2 as A2
from draw_class_art import (Icon, poly_mask, ellipse_mask, line_mask, arc_mask, rrect_mask, union, star_pts, bust_mask, shield_pts, sword_masks,
                            Image, CREAM, CREAM_D, KHAKI, KHAKI_D, SLATE, SLATE_D, RUST, RUST_D, VIOLET, VIOLET_D, GOLD, GOLD_D, BONE, BONE_D, STONE, STONE_D)
from draw_class_art2 import flame_mask, wolf_mask, eye_mask, leaf_mask, tree_mask, paw_mask, rays, snow_mask, fist_mask
from draw_legendary_174 import ring
from PIL import ImageDraw, ImageFilter, ImageChops

ROOT = sys.argv[1] if len(sys.argv) > 1 else '.'
MOSS = (110, 134, 74); MOSS_D = (74, 92, 48)
FUR = (138, 104, 70); BARK = (124, 90, 58)
DUSK = (92, 96, 120); DUSK_D = (62, 64, 84)
POISON = (118, 150, 70)
BLOOD = (150, 62, 50)
INKC = (40, 38, 34)

def axe(I, cx, cy, ang=-50, L=70, head=RUST):
    a = math.radians(ang); dx, dy = math.cos(a), math.sin(a); px, py = -dy, dx
    top = (cx + dx * L * 0.5, cy + dy * L * 0.5); bot = (cx - dx * L * 0.5, cy - dy * L * 0.5)
    I.layer(line_mask([bot, top], 6), BARK)
    hx, hy = cx + dx * L * 0.32, cy + dy * L * 0.32
    I.layer(poly_mask([(hx + px * 4, hy + py * 4), (hx + px * 26 + dx * 14, hy + py * 26 + dy * 14), (hx + px * 30, hy + py * 30),
                       (hx + px * 26 - dx * 14, hy + py * 26 - dy * 14), (hx - dx * 6 + px * 4, hy - dy * 6 + py * 4)]), head)

def arrow(I, x0, y0, x1, y1, col=BONE, w=4):
    I.layer(line_mask([(x0, y0), (x1, y1)], w), BARK)
    a = math.atan2(y1 - y0, x1 - x0); dx, dy = math.cos(a), math.sin(a); px, py = -dy, dx
    I.layer(poly_mask([(x1 + dx * 12, y1 + dy * 12), (x1 + px * 7, y1 + py * 7), (x1 - px * 7, y1 - py * 7)]), SLATE)
    I.layer(poly_mask([(x0, y0), (x0 - dx * 10 + px * 7, y0 - dy * 10 + py * 7), (x0 + dx * 6, y0 + dy * 6), (x0 - dx * 10 - px * 7, y0 - dy * 10 - py * 7)]), col)

def dagger(I, cx, cy, ang, L=46, blade=SLATE):
    for m, c in sword_masks(cx, cy, ang, L=L, blade_col=blade, hilt_col=DUSK_D): I.layer(m, c)

def hood(I, cx, cy, col=DUSK):
    I.layer(poly_mask([(cx, cy - 40), (cx + 34, cy - 4), (cx + 38, cy + 44), (cx - 38, cy + 44), (cx - 34, cy - 4)]), col)
    I.layer(ellipse_mask(cx, cy + 6, 18, 20), INKC, shade=False)
    I.layer(ellipse_mask(cx - 7, cy + 4, 3), GOLD, outline=False, shade=False)
    I.layer(ellipse_mask(cx + 7, cy + 4, 3), GOLD, outline=False, shade=False)

def icon(name):
    I = Icon()
    # ================= 野蛮人 =================
    if name == 'Rage':                 # 狂暴：怒吼的人头 + 火焰
        I.layer(flame_mask(64, 66, 1.05), RUST)
        I.layer(bust_mask(64, 52, 1.0), BLOOD)
        I.layer(poly_mask([(56, 58), (72, 58), (64, 70)]), INKC, shade=False)
    elif name == 'RecklessAttack':     # 鲁莽攻击：交叉双斧
        axe(I, 64, 64, -55); axe(I, 64, 64, -125, head=RUST_D)
    elif name == 'DangerSense':        # 危险感知：睁大的眼睛 + 擦过的箭
        I.layer(eye_mask(64, 64, 40, 22), BONE)
        I.layer(ellipse_mask(64, 64, 12), RUST)
        I.layer(ellipse_mask(64, 64, 5), INKC, shade=False)
        arrow(I, 18, 30, 104, 22, w=3)
    elif name == 'PrimalRoar':         # 野性怒吼：兽头 + 声波
        for r in (52, 42):
            I.layer(arc_mask(48, 64, r, -40, 40, 5), CREAM_D)
        I.layer(wolf_mask(46, 62, 0.95), FUR)
    elif name == 'BrutalCritical':     # 残暴重击：斧 + 迸裂的星
        I.layer(poly_mask(star_pts(76, 52, 30, 12, 7)), GOLD)
        axe(I, 54, 74, -45, L=76)
    elif name == 'RelentlessRage':     # 不灭之怒：碎裂的心 + 火焰
        I.layer(flame_mask(64, 64, 1.0), RUST)
        I.layer(A2.heart_mask(64, 62, 0.9), BLOOD)
        I.layer(line_mask([(58, 46), (70, 60), (60, 72), (68, 86)], 4), INKC, outline=False, shade=False)
    elif name == 'PrimalChampion':     # 原初斗士：握斧的魁梧人像 + 兽角
        I.layer(bust_mask(64, 44, 1.25), FUR)
        I.layer(poly_mask([(48, 36), (34, 14), (44, 32)]), BONE); I.layer(poly_mask([(80, 36), (94, 14), (84, 32)]), BONE)
        axe(I, 96, 82, -80, L=56)
    # ================= 游侠 =================
    elif name == 'FavoredFoe':         # 宿敌：靶心 + 箭
        for r, c in ((40, BONE), (28, BLOOD), (16, BONE), (6, BLOOD)):
            I.layer(ellipse_mask(60, 68, r), c)
        arrow(I, 112, 16, 66, 62)
    elif name == 'HuntersMark':        # 猎人印记：发光的印记符号
        I.layer(ellipse_mask(64, 64, 42), MOSS_D)
        I.layer(poly_mask(star_pts(64, 64, 32, 10, 4, rot=math.pi / 4)), GOLD)
        I.layer(ellipse_mask(64, 64, 8), RUST)
    elif name == 'Woodstride':         # 野外穿行：树 + 脚印
        I.layer(tree_mask(46, 54, 0.85), MOSS)
        for i, (x, y) in enumerate(((78, 100), (92, 82), (82, 62), (98, 44))):
            I.layer(ellipse_mask(x, y, 7, 10), BARK)
    elif name == 'Volley':             # 齐射：三支平行的箭
        for k in range(3):
            arrow(I, 16, 82 - k * 22 + 10, 96, 40 - k * 22 + 34)
    elif name == 'BeastBond':          # 兽伴：狼头 + 爪印
        I.layer(wolf_mask(56, 58, 1.05), SLATE)
        I.layer(paw_mask(94, 96, 0.55), MOSS)
    elif name == 'HideInPlain':        # 隐匿伏击：叶片后的眼睛
        I.layer(eye_mask(64, 70, 30, 14), BONE)
        I.layer(ellipse_mask(64, 70, 7), INKC, shade=False)
        for x0, y0, x1, y1 in ((20, 40, 70, 20), (58, 22, 110, 46), (16, 100, 62, 86), (66, 88, 112, 104)):
            I.layer(leaf_mask(x0, y0, x1, y1, 12), MOSS)
    elif name == 'FoeSlayer':          # 屠戮宿敌：被箭穿透的骷髅头
        I.layer(ellipse_mask(60, 60, 30, 28), BONE)
        I.layer(rrect_mask(46, 76, 74, 96, 6), BONE)
        I.layer(ellipse_mask(50, 60, 7), INKC, shade=False); I.layer(ellipse_mask(70, 60, 7), INKC, shade=False)
        arrow(I, 112, 14, 72, 52)
    # ================= 游荡者 =================
    elif name == 'SneakAttack':        # 偷袭：背后的匕首 + 剪影
        I.layer(bust_mask(50, 44, 1.05), STONE)
        dagger(I, 88, 70, -135, L=58)
    elif name == 'CunningAction':      # 灵巧动作：疾奔的速度线 + 靴子
        for k, y in enumerate((44, 64, 84)):
            I.layer(line_mask([(14 + k * 6, y), (52 + k * 6, y)], 5), CREAM_D)
        I.layer(poly_mask([(60, 28), (86, 28), (86, 78), (108, 86), (108, 100), (60, 100)]), DUSK)
    elif name == 'UncannyDodge':       # 诡诈闪避：侧身的剪影 + 擦过的箭
        I.layer(bust_mask(70, 44, 1.0), DUSK)
        arrow(I, 12, 62, 86, 66, w=3)
        I.layer(arc_mask(64, 64, 50, 200, 250, 5), CREAM_D)
    elif name == 'ShadowStep':         # 暗影潜行：兜帽 + 新月
        I.layer(arc_mask(64, 64, 52, 120, 300, 8), CREAM_D)
        hood(I, 64, 64)
    elif name == 'PoisonBlade':        # 淬毒刃：滴毒的匕首
        dagger(I, 60, 58, -60, L=70, blade=POISON)
        for x, y, r in ((84, 92, 6), (96, 110, 4)):
            I.layer(ellipse_mask(x, y, r, r * 1.3), POISON)
    elif name == 'Assassinate':        # 刺杀：匕首穿过的红点
        I.layer(ellipse_mask(64, 64, 30), BLOOD)
        dagger(I, 64, 64, -45, L=88)
    elif name == 'StrokeOfLuck':       # 幸运一击：金币 + 四叶
        I.layer(ellipse_mask(64, 64, 40), GOLD)
        I.layer(arc_mask(64, 64, 30, 0, 360, 4), GOLD_D)
        for a in range(4):
            ang = math.radians(45 + a * 90)
            I.layer(ellipse_mask(64 + math.cos(ang) * 11, 64 + math.sin(ang) * 11, 10), MOSS)
    # ================= 传奇 =================
    elif name == 'BerserkerSoul':      # 狂战之魂：燃烧的兽角头盔
        I.layer(flame_mask(64, 62, 1.0), RUST)
        I.layer(rrect_mask(38, 52, 90, 96, 14), SLATE)
        I.layer(poly_mask([(42, 58), (20, 22), (48, 50)]), BONE); I.layer(poly_mask([(86, 58), (108, 22), (80, 50)]), BONE)
        I.layer(rrect_mask(50, 70, 78, 76, 2), INKC, shade=False)
    elif name == 'EarthShatter':       # 大地崩裂：斧劈下的裂地
        I.layer(poly_mask([(10, 96), (118, 96), (118, 116), (10, 116)]), STONE)
        I.layer(line_mask([(64, 96), (54, 106), (68, 112), (60, 118)], 4), INKC, outline=False, shade=False)
        for x, y in ((30, 84), (98, 84), (44, 76), (84, 76)):
            I.layer(poly_mask([(x - 6, y + 8), (x, y - 6), (x + 6, y + 8)]), STONE_D)
        axe(I, 64, 52, -90, L=80)
    elif name == 'FoeBane':            # 宿敌克星：被三支箭钉住的骷髅
        I.layer(ellipse_mask(64, 66, 28, 26), BONE)
        I.layer(ellipse_mask(54, 66, 6), INKC, shade=False); I.layer(ellipse_mask(74, 66, 6), INKC, shade=False)
        for x0, y0, x1, y1 in ((20, 20, 52, 54), (64, 12, 64, 46), (108, 20, 76, 54)):
            arrow(I, x0, y0, x1, y1, w=3)
    elif name == 'ArrowStorm':         # 万箭齐发：落下的箭雨
        for k, (x, y) in enumerate(((30, 30), (54, 22), (78, 30), (102, 22), (42, 62), (66, 56), (90, 62))):
            arrow(I, x - 10, y - 18, x + 6, y + 18, w=3)
        I.layer(arc_mask(64, 140, 52, 220, 320, 6), MOSS)
    elif name == 'ShadowDance':        # 影舞：三个错开的兜帽剪影
        for x, c in ((40, DUSK_D), (88, DUSK_D), (64, DUSK)):
            I.layer(bust_mask(x, 46, 0.9), c)
        I.layer(arc_mask(64, 64, 46, 200, 340, 5), CREAM_D)
    elif name == 'DeathMark':          # 死亡印记：骷髅 + 匕首十字
        dagger(I, 64, 64, -45, L=90); dagger(I, 64, 64, -135, L=90)
        I.layer(ellipse_mask(64, 60, 20, 18), BONE)
        I.layer(ellipse_mask(57, 60, 5), INKC, shade=False); I.layer(ellipse_mask(71, 60, 5), INKC, shade=False)
    if name in LEGEND:
        ring(I, name in PASSIVE)
    return I.done(128)

ABIL = {'Barbarian': ['Rage', 'RecklessAttack', 'DangerSense', 'PrimalRoar', 'BrutalCritical', 'RelentlessRage', 'PrimalChampion'],
        'Ranger': ['FavoredFoe', 'HuntersMark', 'Woodstride', 'Volley', 'BeastBond', 'HideInPlain', 'FoeSlayer'],
        'Rogue': ['SneakAttack', 'CunningAction', 'UncannyDodge', 'ShadowStep', 'PoisonBlade', 'Assassinate', 'StrokeOfLuck']}
LEGEND = ['BerserkerSoul', 'EarthShatter', 'FoeBane', 'ArrowStorm', 'ShadowDance', 'DeathMark']
PASSIVE = {'BerserkerSoul', 'FoeBane', 'ShadowDance'}

A2.COLORS.update({'Barbarian': ((34, 14, 10), (150, 70, 52), (236, 150, 110)),
                  'Ranger': ((14, 24, 14), (90, 120, 70), (190, 220, 150)),
                  'Rogue': ((14, 14, 22), (98, 98, 128), (190, 190, 230))})

def background(cls):
    BW, BH = A2.BW, A2.BH
    bg, base, line = A2.frame(cls)
    parts = []
    def sil(): return Image.new('L', (BW, BH), 0)
    if cls == 'Barbarian':   # 双刃巨斧 + 兽角头骨
        m = sil(); d = ImageDraw.Draw(m)
        d.line([(475, 380), (475, 1330)], fill=255, width=44)
        for sx in (-1, 1):
            d.polygon([(475, 520), (475 + sx * 280, 400), (475 + sx * 330, 620), (475 + sx * 280, 840), (475, 720)], fill=255)
        parts.append(m)
        m = sil(); d = ImageDraw.Draw(m)
        d.ellipse([385, 1000, 565, 1170], fill=255)
        for sx in (-1, 1):
            d.polygon([(475 + sx * 70, 1040), (475 + sx * 230, 930), (475 + sx * 120, 1080)], fill=255)
        parts.append(m)
    elif cls == 'Ranger':    # 长弓 + 箭 + 下方狼头
        m = sil(); d = ImageDraw.Draw(m)
        d.arc([215, 280, 735, 1040], 100, 260, fill=255, width=40)
        d.line([(373, 300), (373, 1020)], fill=255, width=8)
        d.line([(373, 660), (800, 660)], fill=255, width=18)
        d.polygon([(800, 620), (870, 660), (800, 700)], fill=255)
        parts.append(m)
        m = sil(); d = ImageDraw.Draw(m)
        d.polygon([(360, 1300), (380, 1150), (420, 1060), (460, 1140), (500, 1060), (540, 1150), (640, 1190), (650, 1230), (560, 1260), (520, 1320)], fill=255)
        parts.append(m)
    else:                    # 游荡者：兜帽 + 两把交叉匕首 + 弯月
        m = sil(); d = ImageDraw.Draw(m)
        d.polygon([(475, 360), (690, 620), (720, 1000), (230, 1000), (260, 620)], fill=255)
        parts.append(m)
        m = sil(); d = ImageDraw.Draw(m)
        for sx in (-1, 1):
            d.line([(475 - sx * 260, 1350), (475 + sx * 220, 1050)], fill=255, width=34)
            d.line([(475 - sx * 290, 1300), (475 - sx * 200, 1360)], fill=255, width=28)
        parts.append(m)
        m = sil(); d = ImageDraw.Draw(m)
        d.ellipse([380, 190, 570, 380], fill=255); d.ellipse([420, 170, 610, 360], fill=0)
        parts.append(m)
    allm = parts[0]
    for m in parts[1:]: allm = ImageChops.lighter(allm, m)
    glow = allm.filter(ImageFilter.MaxFilter(31)).filter(ImageFilter.GaussianBlur(22))
    bg.paste(Image.new('RGBA', (BW, BH), line + (255,)), (0, 0), glow.point(lambda v: int(v * 0.55)))
    for m in parts:
        bg.paste(Image.new('RGBA', (BW, BH), line + (255,)), (0, 0), m.filter(ImageFilter.MaxFilter(9)))
        bg.paste(Image.new('RGBA', (BW, BH), (6, 4, 4, 255)), (0, 0), m)
    if cls == 'Rogue':       # 兜帽里的一双眼
        d = ImageDraw.Draw(bg)
        for x in (430, 520):
            d.ellipse([x - 16, 700, x + 16, 722], fill=(220, 210, 150, 255))
    return bg

if __name__ == '__main__':
    out = os.path.join(ROOT, 'Textures', 'LP', 'Classes')
    pv = os.path.join(ROOT, 'ArtSource', 'Preview'); os.makedirs(pv, exist_ok=True)
    names = [(c, n) for c, ns in ABIL.items() for n in ns] + [('Legendary', n) for n in LEGEND]
    cols = 9; cell = 150
    sheet = Image.new('RGBA', (cols * cell + 20, 3 * (cell + 24) + 30), (46, 48, 52, 255)); dr = ImageDraw.Draw(sheet)
    for i, (c, n) in enumerate(names):
        os.makedirs(os.path.join(out, c), exist_ok=True)
        im = icon(n); im.save(os.path.join(out, c, n + '.png'))
        x = 10 + (i % cols) * cell + 11; y = 20 + (i // cols) * (cell + 24)
        sheet.paste(im, (x, y), im); dr.text((x, y + 130), n, fill=(220, 220, 210, 255))
    sheet.save(os.path.join(pv, 'Classes3-icons.png'))
    bgs = []
    for c in ABIL:
        b = background(c); b.save(os.path.join(out, c, 'Background.png')); bgs.append(b.resize((190, 303)))
    strip = Image.new('RGBA', (3 * 200 + 10, 323), (46, 48, 52, 255))
    for i, b in enumerate(bgs): strip.paste(b, (10 + i * 200, 10))
    strip.save(os.path.join(pv, 'Classes3-backgrounds.png'))
    print('classes3 art written:', len(names), 'icons + 3 backgrounds')
