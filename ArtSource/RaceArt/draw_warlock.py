# 邪术师的美术：7 个职业能力 + 15 个宗主能力图标（128×128）+ 2 个传奇图标 + 3 个宗主徽记 + 职业背景（950×1515）+ 预览图。
# 画法沿用 draw_class_art.py / draw_class_art2.py / draw_classes3.py / draw_subclasses.py：粗黑描边、平涂 + 右下暗部、低饱和；传奇外圈暗金环（被动断环）。
# 用法：python draw_warlock.py <模组根目录>
import math, sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import draw_class_art2 as A2
from draw_class_art import (Icon, poly_mask, ellipse_mask, line_mask, arc_mask, rrect_mask, union, star_pts, bust_mask, shield_pts,
                            Image, CREAM, CREAM_D, KHAKI, KHAKI_D, SLATE, SLATE_D, RUST, RUST_D, VIOLET, VIOLET_D, GOLD, GOLD_D, BONE, BONE_D, STONE, STONE_D)
from draw_class_art2 import flame_mask, eye_mask, leaf_mask, rays, palm_mask, bolt_mask, cloud_mask, wing_mask, heart_mask
from draw_classes3 import dagger, hood, MOSS, MOSS_D, DUSK, DUSK_D, BLOOD, INKC
from draw_legendary_174 import ring
from PIL import ImageDraw, ImageFilter, ImageChops

ROOT = sys.argv[1] if len(sys.argv) > 1 else '.'
ELD = (150, 104, 200); ELD_D = (96, 64, 140)       # 魔能紫
VOID = (44, 32, 62)
FEY = (120, 190, 140); FEY_D = (70, 128, 90)
TEAL = (96, 158, 168); TEAL_D = (58, 104, 112)
HELL = (196, 92, 60)

def orb(I, cx, cy, r, col=ELD):
    I.layer(ellipse_mask(cx, cy, r), col)
    I.layer(ellipse_mask(cx - r * 0.3, cy - r * 0.3, r * 0.28), CREAM, outline=False, shade=False)

def beam(I, x0, y0, x1, y1, w=8, col=ELD):
    I.layer(line_mask([(x0, y0), (x1, y1)], w), col)
    I.layer(line_mask([(x0, y0), (x1, y1)], max(2, w // 3)), CREAM, outline=False, shade=False)

def tentacle(I, x0, y0, bend, col=TEAL, w=9, L=60):
    pts = [(x0 + math.sin(k / 6.0 * math.pi) * bend, y0 - k * L / 6.0) for k in range(7)]
    I.layer(line_mask(pts, w), col)

def horns(I, cx, cy, s=1.0, col=BONE):
    I.layer(poly_mask([(cx - 14 * s, cy), (cx - 34 * s, cy - 36 * s), (cx - 6 * s, cy - 8 * s)]), col)
    I.layer(poly_mask([(cx + 14 * s, cy), (cx + 34 * s, cy - 36 * s), (cx + 6 * s, cy - 8 * s)]), col)

def butterfly(I, cx, cy, s=1.0, col=FEY):
    for sx in (-1, 1):
        I.layer(ellipse_mask(cx + sx * 16 * s, cy - 8 * s, 15 * s, 18 * s), col)
        I.layer(ellipse_mask(cx + sx * 12 * s, cy + 14 * s, 10 * s, 12 * s), FEY_D)
    I.layer(rrect_mask(cx - 3 * s, cy - 20 * s, cx + 3 * s, cy + 24 * s, 3), VOID)

def icon(name):
    I = Icon()
    # ============ 通用 ============
    if name == 'EldritchBlast':        # 魔能爆：三道紫色光束从掌心射出
        I.layer(palm_mask(34, 84, 0.7), BONE)
        for y1 in (24, 46, 68):
            beam(I, 44, 74, 112, y1, 7)
        orb(I, 46, 74, 10)
    elif name == 'Darkness':           # 黑暗术：黑雾吞没的眼睛
        I.layer(ellipse_mask(64, 66, 52, 44), VOID)
        for x, y, r in ((34, 44, 18), (90, 40, 20), (60, 96, 22)):
            I.layer(ellipse_mask(x, y, r), DUSK_D)
        I.layer(eye_mask(64, 66, 40, 18), STONE_D); I.layer(line_mask([(28, 84), (100, 48)], 6), INKC, shade=False)
    elif name == 'Invocations':        # 魔能祈唤：翻开的魔典 + 三颗符文
        I.layer(poly_mask([(16, 90), (64, 78), (112, 90), (112, 104), (64, 94), (16, 104)]), VOID)
        I.layer(poly_mask([(20, 86), (62, 74), (62, 40), (20, 52)]), BONE); I.layer(poly_mask([(108, 86), (66, 74), (66, 40), (108, 52)]), BONE_D)
        for x, y in ((40, 26), (64, 16), (88, 26)):
            I.layer(poly_mask(star_pts(x, y, 9, 4, 4)), ELD)
    elif name == 'Hex':                # 咒术：带钉的诅咒标记
        I.layer(arc_mask(64, 64, 44, 0, 360, 7), ELD_D)
        I.layer(poly_mask(star_pts(64, 64, 38, 15, 5)), ELD)
        I.layer(ellipse_mask(64, 64, 10), INKC, shade=False)
        I.layer(line_mask([(64, 8), (64, 34)], 5), BONE)
    elif name == 'Dominate':           # 支配人类：被丝线牵着的人头
        I.layer(bust_mask(64, 66, 1.1), STONE)
        for x in (44, 64, 84):
            I.layer(line_mask([(x, 10), (x, 46)], 3), ELD, shade=False)
        I.layer(rrect_mask(28, 6, 100, 16, 4), ELD_D)
        I.layer(ellipse_mask(56, 58, 4), ELD, shade=False); I.layer(ellipse_mask(72, 58, 4), ELD, shade=False)
    elif name == 'ForceCage':          # 力场监牢：发光的栅栏立方体
        I.layer(rrect_mask(26, 22, 102, 108, 4), VOID)
        for x in (34, 52, 70, 88):
            I.layer(line_mask([(x + 4, 24), (x + 4, 106)], 5), ELD)
        I.layer(line_mask([(26, 26), (102, 26)], 6), ELD_D); I.layer(line_mask([(26, 104), (102, 104)], 6), ELD_D)
        I.layer(bust_mask(64, 70, 0.6), STONE_D)
    elif name == 'EldritchMaster':     # 秘法大师：冥想的人 + 背后巨大的紫色光环
        I.layer(arc_mask(64, 58, 48, 0, 360, 8), ELD)
        rays(I, 64, 58, 52, 62, 12, ELD_D, w=3)
        I.layer(bust_mask(64, 66, 1.0), DUSK)
        I.layer(ellipse_mask(64, 100, 34, 10), DUSK_D)
    # ============ 邪魔 ============
    elif name == 'DarkBlessing':       # 黑暗祝福：头骨上燃起治愈的暗火
        I.layer(flame_mask(64, 50, 0.9), ELD)
        I.layer(ellipse_mask(64, 78, 24, 22), BONE)
        I.layer(ellipse_mask(55, 76, 5), INKC, shade=False); I.layer(ellipse_mask(73, 76, 5), INKC, shade=False)
        I.layer(heart_mask(96, 32, 0.5), BLOOD)
    elif name == 'HellishRebuke':      # 炼狱叱喝：指向前方的手 + 地狱火
        I.layer(flame_mask(80, 60, 1.1), HELL)
        I.layer(flame_mask(80, 70, 0.6), GOLD)
        I.layer(palm_mask(36, 76, 0.7), BONE)
    elif name == 'DarkLuck':           # 黑暗之运：带角的骰子形宝石（不画数字）
        I.layer(poly_mask([(64, 18), (104, 44), (104, 88), (64, 112), (24, 88), (24, 44)]), RUST_D)
        I.layer(poly_mask([(64, 18), (104, 44), (64, 64), (24, 44)]), RUST)
        horns(I, 64, 30, 0.8)
    elif name == 'FiendResilience':    # 邪魔韧性：火焰中的盾
        I.layer(flame_mask(64, 60, 1.25), HELL)
        I.layer(poly_mask(shield_pts(64, 66, 54, 66)), SLATE)
        horns(I, 64, 52, 0.6, RUST_D)
    elif name == 'HurlHell':           # 掷入地狱：坠入火坑的人影
        I.layer(ellipse_mask(64, 96, 50, 20), INKC)
        I.layer(flame_mask(64, 92, 0.9), HELL)
        I.layer(bust_mask(64, 40, 0.8), STONE)
        for x in (36, 92):
            I.layer(line_mask([(x, 30), (x + (64 - x) * 0.3, 62)], 4), GOLD, shade=False)
    # ============ 妖精 ============
    elif name == 'FeyPresence':        # 妖精风采：蝴蝶翅膀的光环
        I.layer(arc_mask(64, 64, 50, 0, 360, 5), FEY_D)
        butterfly(I, 64, 62, 1.5)
        rays(I, 64, 62, 46, 56, 8, CREAM_D, w=3)
    elif name == 'MistyEscape':        # 雾遁：雾气中淡去的人影
        for x, y, r in ((40, 84, 22), (80, 88, 26), (62, 70, 20)):
            I.layer(ellipse_mask(x, y, r), STONE)
        I.layer(bust_mask(56, 50, 0.85), FEY)
        I.layer(bust_mask(88, 44, 0.6), FEY_D)
    elif name == 'BeguilingDefense':   # 魅惑防御：花瓣围成的盾
        for a in range(0, 360, 45):
            r = math.radians(a); I.layer(ellipse_mask(64 + math.cos(r) * 32, 64 + math.sin(r) * 32, 14, 14), FEY)
        I.layer(poly_mask(shield_pts(64, 64, 44, 52)), FEY_D)
        I.layer(heart_mask(64, 62, 0.45), (200, 120, 150))
    elif name == 'Beguile':            # 迷魂之舞：螺旋 + 三只小蝴蝶
        I.layer(arc_mask(64, 64, 40, 0, 300, 6), FEY_D); I.layer(arc_mask(64, 64, 24, 60, 360, 6), FEY)
        for x, y in ((30, 30), (98, 34), (64, 100)):
            butterfly(I, x, y, 0.55)
    elif name == 'DarkDelirium':       # 黑暗谵妄：裂开的面具 + 藤蔓
        I.layer(ellipse_mask(64, 62, 36, 44), BONE)
        I.layer(line_mask([(64, 18), (58, 50), (70, 72), (62, 106)], 4), INKC, shade=False)
        I.layer(ellipse_mask(50, 56, 7), VOID, shade=False); I.layer(ellipse_mask(78, 56, 7), VOID, shade=False)
        I.layer(leaf_mask(20, 100, 46, 80, 8), MOSS); I.layer(leaf_mask(108, 100, 82, 80, 8), MOSS)
    # ============ 旧日支配者 ============
    elif name == 'AwakenedMind':       # 觉醒心灵：额头的第三只眼 + 波纹
        I.layer(bust_mask(64, 66, 1.1), STONE)
        for r in (30, 44):
            I.layer(arc_mask(64, 40, r, 200, 340, 3), TEAL)
        I.layer(eye_mask(64, 46, 24, 12), TEAL); I.layer(ellipse_mask(64, 46, 4), INKC, shade=False)
    elif name == 'DissonantWhispers':  # 不谐低语：耳边的触须与音波
        I.layer(ellipse_mask(40, 64, 16, 28), STONE)
        tentacle(I, 70, 106, 12); tentacle(I, 92, 106, -10, TEAL_D)
        for r in (20, 32):
            I.layer(arc_mask(40, 64, r + 14, -50, 50, 4), TEAL, shade=False)
    elif name == 'EntropicWard':       # 熵能护盾：扭曲的子弹被一圈涡旋偏开
        I.layer(arc_mask(64, 64, 42, 0, 360, 10), TEAL_D)
        I.layer(arc_mask(64, 64, 26, 40, 320, 6), TEAL)
        I.layer(line_mask([(10, 30), (40, 44), (24, 80)], 5), SLATE)
    elif name == 'ThoughtShield':      # 思维屏障：被半球罩住的大脑
        I.layer(arc_mask(64, 74, 48, 180, 360, 8), TEAL)
        I.layer(ellipse_mask(64, 78, 30, 22), (200, 150, 160))
        I.layer(line_mask([(64, 58), (64, 98)], 3), (150, 100, 110), shade=False)
    elif name == 'CreateThrall':       # 创造奴仆：触须缠住跪下的人影
        I.layer(bust_mask(60, 70, 0.9), STONE_D)
        tentacle(I, 30, 110, 14); tentacle(I, 98, 110, -14, TEAL_D)
        I.layer(eye_mask(64, 24, 34, 14), TEAL); I.layer(ellipse_mask(64, 24, 4, 10), INKC, shade=False)
    # ============ 传奇 ============
    elif name == 'PatronAvatar':       # 宗主化身：背后巨大的宗主剪影
        I.layer(bust_mask(64, 50, 1.6), VOID)
        horns(I, 64, 26, 1.0, ELD_D)
        I.layer(ellipse_mask(52, 44, 5), ELD, shade=False); I.layer(ellipse_mask(76, 44, 5), ELD, shade=False)
        I.layer(bust_mask(64, 84, 0.7), DUSK)
    elif name == 'EldritchSoul':       # 魔能之魂：胸口的紫色魂火
        I.layer(bust_mask(64, 60, 1.2), DUSK_D)
        orb(I, 64, 76, 16)
        rays(I, 64, 76, 22, 30, 8, ELD, w=3)
    if name in LEGEND:
        ring(I, name in PASSIVE)
    return I.done(128)

CLASS = ['EldritchBlast', 'Darkness', 'Invocations', 'Hex', 'Dominate', 'ForceCage', 'EldritchMaster',
         'DarkBlessing', 'HellishRebuke', 'DarkLuck', 'FiendResilience', 'HurlHell',
         'FeyPresence', 'MistyEscape', 'BeguilingDefense', 'Beguile', 'DarkDelirium',
         'AwakenedMind', 'DissonantWhispers', 'EntropicWard', 'ThoughtShield', 'CreateThrall']
LEGEND = ['PatronAvatar', 'EldritchSoul']
PASSIVE = {'EldritchSoul'}
EMBLEMS = {'Fiend': (BLOOD, 'horns'), 'Archfey': (FEY_D, 'fey'), 'GreatOldOne': (TEAL_D, 'eye')}

def emblem(sub):
    I = Icon(); col, sym = EMBLEMS[sub]
    I.layer(poly_mask(shield_pts(64, 64, 100, 116)), GOLD_D)
    I.layer(poly_mask(shield_pts(64, 62, 86, 100)), col)
    if sym == 'horns':
        I.layer(flame_mask(64, 70, 0.8), HELL); horns(I, 64, 44, 0.9)
    elif sym == 'fey':
        butterfly(I, 64, 60, 1.2); I.layer(poly_mask(star_pts(64, 26, 8, 3, 5)), CREAM)
    else:
        tentacle(I, 44, 104, 8); tentacle(I, 84, 104, -8, TEAL)
        I.layer(eye_mask(64, 50, 46, 22), BONE); I.layer(ellipse_mask(64, 50, 6, 14), INKC, shade=False)
    return I.done(128)

A2.COLORS.update({'Warlock': ((18, 12, 26), (110, 78, 150), (210, 180, 240))})

def background():
    BW, BH = A2.BW, A2.BH
    bg, base, line = A2.frame('Warlock')
    parts = []
    def sil(): return Image.new('L', (BW, BH), 0)
    m = sil(); d = ImageDraw.Draw(m)        # 带角的宗主头影
    d.ellipse([300, 300, 650, 700], fill=255)
    for sx in (-1, 1):
        d.polygon([(475 + sx * 120, 360), (475 + sx * 330, 140), (475 + sx * 180, 420)], fill=255)
    parts.append(m)
    m = sil(); d = ImageDraw.Draw(m)        # 立约的手 + 契约卷轴
    d.rectangle([250, 980, 700, 1080], fill=255)
    d.ellipse([220, 960, 290, 1100], fill=255); d.ellipse([660, 960, 730, 1100], fill=255)
    d.polygon([(400, 980), (440, 820), (510, 820), (550, 980)], fill=255)
    parts.append(m)
    m = sil(); d = ImageDraw.Draw(m)        # 三道魔能光束
    for x in (300, 475, 650):
        d.line([(475, 1180), (x, 1400)], fill=255, width=22)
    parts.append(m)
    allm = parts[0]
    for m in parts[1:]: allm = ImageChops.lighter(allm, m)
    glow = allm.filter(ImageFilter.MaxFilter(31)).filter(ImageFilter.GaussianBlur(22))
    bg.paste(Image.new('RGBA', (BW, BH), line + (255,)), (0, 0), glow.point(lambda v: int(v * 0.55)))
    for m in parts:
        bg.paste(Image.new('RGBA', (BW, BH), line + (255,)), (0, 0), m.filter(ImageFilter.MaxFilter(9)))
        bg.paste(Image.new('RGBA', (BW, BH), (6, 4, 8, 255)), (0, 0), m)
    d = ImageDraw.Draw(bg)                  # 宗主的一双眼
    for x in (420, 530):
        d.ellipse([x - 18, 520, x + 18, 546], fill=(200, 160, 250, 255))
    return bg

if __name__ == '__main__':
    out = os.path.join(ROOT, 'Textures', 'LP', 'Classes')
    pv = os.path.join(ROOT, 'ArtSource', 'Preview'); os.makedirs(pv, exist_ok=True)
    for sub in ('Warlock', 'Legendary', 'Subclass'): os.makedirs(os.path.join(out, sub), exist_ok=True)
    items = [('Warlock', n, icon(n)) for n in CLASS] + [('Legendary', n, icon(n)) for n in LEGEND] + [('Subclass', 'Emblem_' + s, emblem(s)) for s in EMBLEMS]
    cols = 9; cell = 150; rows = (len(items) + cols - 1) // cols
    sheet = Image.new('RGBA', (cols * cell + 20, rows * (cell + 24) + 30), (46, 48, 52, 255)); dr = ImageDraw.Draw(sheet)
    for i, (sub, n, im) in enumerate(items):
        im.save(os.path.join(out, sub, n + '.png'))
        x = 10 + (i % cols) * cell + 11; y = 20 + (i // cols) * (cell + 24)
        sheet.paste(im, (x, y), im); dr.text((x, y + 130), n, fill=(220, 220, 210, 255))
    sheet.save(os.path.join(pv, 'Warlock-icons.png'))
    b = background(); b.save(os.path.join(out, 'Warlock', 'Background.png')); b.resize((190, 303)).save(os.path.join(pv, 'Warlock-background.png'))
    print('warlock art written:', len(CLASS), 'icons +', len(LEGEND), 'legendary +', len(EMBLEMS), 'emblems + background')
