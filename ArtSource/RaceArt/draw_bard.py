# 1.123.0 吟游诗人的美术：13 个职业能力 + 15 个学院能力图标（128×128）+ 2 个传奇图标 + 4 个学院徽记 + 职业背景（950×1515）+ 幻象分身 + 预览图。
# 画法沿用 draw_warlock.py：粗黑描边、平涂 + 右下暗部、低饱和；传奇外圈暗金环（被动断环）。
# 用法：python draw_bard.py <模组根目录>
import math, sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import draw_class_art2 as A2
from draw_class_art import (Icon, poly_mask, ellipse_mask, line_mask, arc_mask, rrect_mask, union, subtract, star_pts, bust_mask, shield_pts, sword_masks,
                            Image, CREAM, CREAM_D, KHAKI, KHAKI_D, SLATE, SLATE_D, RUST, RUST_D, VIOLET, VIOLET_D, GOLD, GOLD_D, BONE, BONE_D, STONE, STONE_D)
from draw_class_art2 import flame_mask, eye_mask, rays, heart_mask, bolt_mask, cloud_mask
from draw_classes3 import dagger, DUSK, DUSK_D, INKC
from draw_legendary_174 import ring
from PIL import ImageDraw, ImageFilter, ImageChops

ROOT = sys.argv[1] if len(sys.argv) > 1 else '.'
ROSE = (196, 104, 146); ROSE_D = (138, 64, 100)
SKY = (150, 176, 224); SKY_D = (96, 118, 166)
SILV = (196, 200, 210); SILV_D = (138, 142, 156)
WOOD = (170, 116, 64); WOOD_D = (112, 72, 38)

def lute(I, cx, cy, s=1.0, col=WOOD):
    I.layer(line_mask([(cx, cy - 6 * s), (cx + 22 * s, cy - 50 * s)], 8 * s), WOOD_D)
    I.layer(poly_mask([(cx + 18 * s, cy - 50 * s), (cx + 32 * s, cy - 58 * s), (cx + 30 * s, cy - 46 * s)]), RUST_D)
    I.layer(ellipse_mask(cx - 4 * s, cy + 10 * s, 22 * s, 25 * s), col)
    I.layer(ellipse_mask(cx - 2 * s, cy + 6 * s, 6 * s), INKC, outline=False, shade=False)
    I.layer(line_mask([(cx - 8 * s, cy + 26 * s), (cx + 26 * s, cy - 52 * s)], 2), CREAM, outline=False, shade=False)

def note(I, x, y, s=1.0, col=ROSE, double=False):
    I.layer(ellipse_mask(x, y, 7 * s, 5.5 * s), col)
    I.layer(line_mask([(x + 6 * s, y), (x + 6 * s, y - 26 * s)], 3.5 * s), col)
    if double:
        I.layer(ellipse_mask(x + 18 * s, y - 4 * s, 7 * s, 5.5 * s), col)
        I.layer(line_mask([(x + 24 * s, y - 4 * s), (x + 24 * s, y - 30 * s)], 3.5 * s), col)
        I.layer(line_mask([(x + 6 * s, y - 26 * s), (x + 24 * s, y - 30 * s)], 5 * s), col)
    else:
        I.layer(line_mask([(x + 6 * s, y - 26 * s), (x + 16 * s, y - 16 * s)], 4 * s), col)

def bubble(I, cx, cy, w, h, col=BONE, tail=-1):
    I.layer(union(ellipse_mask(cx, cy, w, h), poly_mask([(cx + tail * w * 0.3, cy + h * 0.6), (cx + tail * w * 0.9, cy + h * 1.35), (cx + tail * w * 0.1, cy + h * 0.85)])), col)

def book(I, cx, cy, s=1.0, col=VIOLET):
    I.layer(poly_mask([(cx - 48 * s, cy + 22 * s), (cx, cy + 12 * s), (cx + 48 * s, cy + 22 * s), (cx + 48 * s, cy + 34 * s), (cx, cy + 26 * s), (cx - 48 * s, cy + 34 * s)]), DUSK)
    I.layer(poly_mask([(cx - 44 * s, cy + 18 * s), (cx - 2 * s, cy + 8 * s), (cx - 2 * s, cy - 26 * s), (cx - 44 * s, cy - 14 * s)]), BONE)
    I.layer(poly_mask([(cx + 44 * s, cy + 18 * s), (cx + 2 * s, cy + 8 * s), (cx + 2 * s, cy - 26 * s), (cx + 44 * s, cy - 14 * s)]), BONE_D)

def rapier(I, cx, cy, ang, L=86, blade=SILV, hilt=GOLD_D):
    for m, c in sword_masks(cx, cy, ang, L, blade, hilt):
        I.layer(m, c)

def mask_face(I, cx, cy, s=1.0, col=BONE, happy=True):
    I.layer(ellipse_mask(cx, cy, 28 * s, 32 * s), col)
    for sx in (-1, 1):
        I.layer(ellipse_mask(cx + sx * 11 * s, cy - 8 * s, 6 * s, 4 * s if happy else 6 * s), INKC, outline=False, shade=False)
    if happy:
        I.layer(arc_mask(cx, cy + 4 * s, 15 * s, 20, 160, 5 * s), INKC, outline=False, shade=False)
    else:
        I.layer(arc_mask(cx, cy + 22 * s, 13 * s, 200, 340, 5 * s), INKC, outline=False, shade=False)

def icon(name):
    I = Icon()
    # ============ 职业 ============
    if name == 'ViciousMockery':       # 恶毒嘲讽：对话气泡里一道带刺的闪电
        bubble(I, 60, 50, 44, 32)
        I.layer(bolt_mask(60, 52, 0.7), ROSE)
        I.layer(poly_mask(star_pts(104, 98, 12, 5, 4)), ROSE_D)
    elif name == 'Inspiration':        # 吟游激励：鲁特琴 + 飘出的音符 + 光
        rays(I, 54, 76, 30, 50, 8, GOLD, w=3)
        lute(I, 52, 76, 1.0)
        note(I, 92, 40, 1.0); note(I, 100, 86, 0.7, ROSE_D)
    elif name == 'HealingWord':        # 治疗真言：气泡里的爱心
        bubble(I, 64, 54, 46, 36, BONE, tail=1)
        I.layer(heart_mask(64, 54, 0.9), ROSE)
        I.layer(poly_mask(star_pts(22, 100, 10, 4, 4)), GOLD)
    elif name == 'HideousLaughter':    # 狂笑术：笑得变形的喜剧面具 + 泪花
        mask_face(I, 64, 62, 1.3, GOLD)
        for x, y in ((28, 40), (100, 40)):
            I.layer(ellipse_mask(x, y, 5, 8), SKY)
        I.layer(line_mask([(18, 100), (34, 92)], 4), INKC, outline=False); I.layer(line_mask([(94, 92), (110, 100)], 4), INKC, outline=False)
    elif name == 'SongOfRest':         # 安歇之歌：月牙 + 音符
        I.layer(subtract(ellipse_mask(56, 60, 40), ellipse_mask(76, 48, 36)), SKY)
        note(I, 86, 70, 0.9, BONE); note(I, 98, 32, 0.6, BONE_D)
        I.layer(poly_mask(star_pts(30, 22, 7, 3, 4)), CREAM)
    elif name == 'JackOfAllTrades':    # 万事通：一圈小工具 —— 锤、羽毛笔、书、剑围着星
        I.layer(poly_mask(star_pts(64, 64, 22, 10, 5)), GOLD)
        I.layer(line_mask([(24, 30), (44, 46)], 7), STONE); I.layer(rrect_mask(14, 20, 34, 32, 3), STONE_D)
        I.layer(line_mask([(104, 24), (84, 48)], 4), BONE); I.layer(poly_mask([(104, 24), (114, 14), (108, 30)]), BONE_D)
        I.layer(rrect_mask(18, 86, 46, 108, 3), VIOLET)
        for m, c in sword_masks(96, 96, -135, 40): I.layer(m, c)
    elif name == 'Countercharm':       # 反迷惑之歌：盾牌上的音符
        I.layer(poly_mask(shield_pts(64, 64, 80, 94)), SKY_D)
        note(I, 56, 80, 1.3, BONE)
    elif name == 'Confusion':          # 困惑术：脑袋上转圈的漩涡
        I.layer(bust_mask(64, 78, 1.0), STONE_D)
        for r, w in ((30, 5), (20, 5), (10, 4)):
            I.layer(arc_mask(64, 34, r, 200, 520, w), ROSE)
        I.layer(poly_mask(star_pts(100, 22, 8, 3, 4)), GOLD)
    elif name == 'Ballad':             # 传颂之歌：卷轴 + 羽毛笔 + 音符
        I.layer(rrect_mask(20, 30, 92, 104, 6), BONE)
        I.layer(ellipse_mask(56, 30, 38, 8), BONE_D); I.layer(ellipse_mask(56, 104, 38, 8), BONE_D)
        for y in (50, 64, 78): I.layer(line_mask([(32, y), (80, y)], 3), STONE, outline=False, shade=False)
        I.layer(line_mask([(112, 16), (78, 86)], 4), RUST_D); I.layer(poly_mask([(112, 16), (124, 8), (116, 30)]), ROSE)
        note(I, 100, 110, 0.6)
    elif name == 'MagicalSecrets':     # 魔法奥秘：打开的书 + 一颗星
        book(I, 64, 74)
        I.layer(poly_mask(star_pts(64, 30, 18, 7, 5)), ROSE)
    elif name == 'MagicalSecrets2':    # 魔法奥秘·二：两颗星
        book(I, 64, 74)
        I.layer(poly_mask(star_pts(44, 32, 14, 6, 5)), ROSE); I.layer(poly_mask(star_pts(84, 26, 14, 6, 5)), GOLD)
    elif name == 'Mislead':            # 惑心幻景：一个实身 + 一个淡色幻影
        I.layer(bust_mask(82, 66, 1.0), SKY)
        mask_face(I, 82, 44, 0.55, BONE)
        I.layer(bust_mask(44, 72, 0.9), DUSK_D)
        I.layer(line_mask([(20, 108), (60, 108)], 3), STONE, outline=False, shade=False)
    elif name == 'WordsOfCreation':    # 创世之言：口中吐出的金色星光
        rays(I, 64, 64, 34, 58, 12, GOLD, w=4)
        I.layer(poly_mask(star_pts(64, 64, 30, 12, 8)), CREAM)
        I.layer(ellipse_mask(64, 64, 10), GOLD)
    # ============ 学识 ============
    elif name == 'CuttingWords':       # 刻薄之语：气泡 + 匕首
        bubble(I, 58, 48, 42, 30)
        dagger(I, 62, 50, -30, 52)
    elif name == 'LoreMastery':        # 博闻强识：一摞书
        for i, (col, w) in enumerate(((VIOLET, 76), (RUST, 70), (SKY_D, 80), (KHAKI, 66))):
            y = 96 - i * 18
            I.layer(rrect_mask(64 - w / 2, y - 8, 64 + w / 2, y + 8, 3), col)
        I.layer(poly_mask(star_pts(96, 22, 10, 4, 5)), GOLD)
    elif name == 'ExtraSecrets':       # 额外魔法奥秘：书 + 三颗星
        book(I, 64, 80, 0.9)
        for x, y, c in ((34, 36, ROSE), (64, 22, GOLD), (94, 36, SKY)):
            I.layer(poly_mask(star_pts(x, y, 12, 5, 5)), c)
    elif name == 'PeerlessSkill':      # 无双技艺：金星 + 光环
        rays(I, 64, 64, 30, 52, 10, GOLD_D, w=3)
        I.layer(poly_mask(star_pts(64, 64, 36, 15, 5)), GOLD)
        I.layer(poly_mask(star_pts(64, 64, 14, 6, 5)), CREAM, outline=False)
    # ============ 勇气 ============
    elif name == 'CombatInspiration':  # 战斗激励：鲁特琴 + 剑
        rapier(I, 70, 56, -50, 96, SILV, RUST)
        lute(I, 46, 78, 0.85)
    elif name == 'ExtraAttack':        # 额外攻击：交叉的两把剑
        rapier(I, 64, 64, -45, 100); rapier(I, 64, 64, -135, 100)
    elif name == 'BattleMagic':        # 战斗魔法：剑上燃起魔焰
        I.layer(flame_mask(64, 66, 1.1), ROSE)
        rapier(I, 64, 66, -90, 104)
    elif name == 'ValorAnthem':        # 勇气战吼：号角 + 声波
        I.layer(poly_mask([(16, 72), (64, 50), (96, 30), (104, 46), (100, 62), (64, 80), (20, 84)]), GOLD)
        I.layer(ellipse_mask(100, 46, 10, 18), GOLD_D)
        for r in (14, 24):
            I.layer(arc_mask(104, 46, r + 10, -50, 50, 4), CREAM, outline=False, shade=False)
    # ============ 剑舞 ============
    elif name == 'BladeFlourish':      # 刀光剑舞：细剑 + 弧形剑光
        I.layer(arc_mask(64, 64, 46, 190, 340, 7), ROSE)
        rapier(I, 64, 64, -60, 104)
    elif name == 'BladeWhirl':         # 刃舞旋风：一圈剑光
        for a in range(0, 360, 90):
            I.layer(arc_mask(64, 64, 44, a, a + 60, 7), SILV)
        rapier(I, 64, 64, -30, 70, SILV, ROSE_D)
    elif name == 'MasterFlourish':     # 大师花招：细剑 + 星 + 双弧
        I.layer(arc_mask(64, 64, 48, 200, 330, 6), GOLD); I.layer(arc_mask(64, 64, 36, 30, 160, 6), ROSE)
        rapier(I, 64, 64, -70, 100)
        I.layer(poly_mask(star_pts(100, 24, 12, 5, 5)), GOLD)
    # ============ 雄辩 ============
    elif name == 'SilverTongue':       # 银舌：银色气泡 + 硬币
        bubble(I, 58, 50, 44, 32, SILV)
        I.layer(ellipse_mask(92, 92, 18), GOLD); I.layer(ellipse_mask(92, 92, 10), GOLD_D, outline=False)
    elif name == 'InfectiousInspiration':  # 感染性激励：三个连成一串的音符
        I.layer(line_mask([(28, 92), (64, 56), (100, 84)], 3), STONE, outline=False, shade=False)
        note(I, 22, 100, 0.8); note(I, 58, 64, 0.8, GOLD); note(I, 94, 92, 0.8, SKY)
    elif name == 'UniversalSpeech':    # 普世言谈：地球 + 气泡
        I.layer(ellipse_mask(56, 70, 36), SKY_D)
        for r in (12, 24): I.layer(arc_mask(56, 70, r, 90, 270, 3), SKY, outline=False, shade=False)
        I.layer(line_mask([(20, 70), (92, 70)], 3), SKY, outline=False, shade=False)
        bubble(I, 98, 30, 22, 16, BONE, tail=-1)
    elif name == 'Truce':              # 停战之言：白旗
        I.layer(line_mask([(36, 114), (36, 14)], 6), WOOD_D)
        I.layer(poly_mask([(38, 18), (104, 24), (96, 44), (106, 64), (38, 58)]), BONE)
    # ============ 传奇 ============
    elif name == 'UndyingLegend':      # 不朽传奇：月桂环里的鲁特琴
        for sx in (-1, 1):
            for k in range(5):
                a = math.radians(110 + k * 26) if sx < 0 else math.radians(70 - k * 26)
                x = 64 + math.cos(a) * 40; y = 70 - math.sin(a) * 40
                I.layer(ellipse_mask(x, y, 7, 4), (110, 140, 96))
        lute(I, 60, 72, 0.8, GOLD)
    elif name == 'FinalSong':          # 终焉之歌：火焰中的大音符
        I.layer(flame_mask(64, 70, 1.3), RUST)
        note(I, 54, 92, 1.5, GOLD, double=True)
    if name in LEGEND:
        ring(I, name in PASSIVE)
    return I.done(128)

CLASS = ['ViciousMockery', 'Inspiration', 'HealingWord', 'HideousLaughter', 'SongOfRest', 'JackOfAllTrades', 'Countercharm', 'Confusion', 'Ballad',
         'MagicalSecrets', 'MagicalSecrets2', 'Mislead', 'WordsOfCreation',
         'CuttingWords', 'LoreMastery', 'ExtraSecrets', 'PeerlessSkill',
         'CombatInspiration', 'ExtraAttack', 'BattleMagic', 'ValorAnthem',
         'BladeFlourish', 'BladeWhirl', 'MasterFlourish',
         'SilverTongue', 'InfectiousInspiration', 'UniversalSpeech', 'Truce']
LEGEND = ['UndyingLegend', 'FinalSong']
PASSIVE = {'UndyingLegend'}
EMBLEMS = {'Lore': (SKY_D, 'book'), 'Valor': (RUST_D, 'sword'), 'Swords': (SLATE_D, 'rapiers'), 'Eloquence': (GOLD_D, 'bubble')}

def emblem(sub):
    I = Icon(); col, sym = EMBLEMS[sub]
    I.layer(poly_mask(shield_pts(64, 64, 100, 116)), GOLD_D)
    I.layer(poly_mask(shield_pts(64, 62, 86, 100)), col)
    if sym == 'book':
        book(I, 64, 66, 0.8); I.layer(poly_mask(star_pts(64, 34, 10, 4, 5)), CREAM)
    elif sym == 'sword':
        rapier(I, 64, 62, -90, 90, SILV, GOLD); lute(I, 50, 80, 0.6)
    elif sym == 'rapiers':
        rapier(I, 64, 62, -60, 86); rapier(I, 64, 62, -120, 86)
    else:
        bubble(I, 64, 54, 30, 22, BONE); I.layer(line_mask([(84, 90), (60, 60)], 3), RUST_D)
    return I.done(128)

def phantasm():
    I = Icon()
    I.layer(bust_mask(64, 70, 1.3), SKY)
    mask_face(I, 64, 40, 0.8, BONE)
    im = I.done(128)
    a = im.split()[3].point(lambda v: int(v * 0.6))
    im.putalpha(a)
    return im

A2.COLORS.update({'Bard': ((26, 12, 20), (150, 80, 112), (240, 190, 214))})

def background():
    BW, BH = A2.BW, A2.BH
    bg, base, line = A2.frame('Bard')
    parts = []
    def sil(): return Image.new('L', (BW, BH), 0)
    m = sil(); d = ImageDraw.Draw(m)        # 大鲁特琴
    d.ellipse([260, 640, 640, 1080], fill=255)
    d.polygon([(500, 680), (560, 700), (760, 260), (700, 240)], fill=255)
    d.polygon([(700, 240), (800, 180), (780, 280)], fill=255)
    parts.append(m)
    m = sil(); d = ImageDraw.Draw(m)        # 飘起的音符
    for x, y, r in ((240, 420, 40), (360, 300, 34), (620, 520, 30)):
        d.ellipse([x - r, y - r * 0.8, x + r, y + r * 0.8], fill=255)
        d.rectangle([x + r - 10, y - 170, x + r + 4, y], fill=255)
        d.polygon([(x + r - 10, y - 170), (x + r + 60, y - 120), (x + r + 4, y - 110)], fill=255)
    parts.append(m)
    m = sil(); d = ImageDraw.Draw(m)        # 喜剧 / 悲剧面具
    d.ellipse([300, 1180, 460, 1380], fill=255); d.ellipse([500, 1200, 660, 1400], fill=255)
    parts.append(m)
    allm = parts[0]
    for m in parts[1:]: allm = ImageChops.lighter(allm, m)
    glow = allm.filter(ImageFilter.MaxFilter(31)).filter(ImageFilter.GaussianBlur(22))
    bg.paste(Image.new('RGBA', (BW, BH), line + (255,)), (0, 0), glow.point(lambda v: int(v * 0.55)))
    for m in parts:
        bg.paste(Image.new('RGBA', (BW, BH), line + (255,)), (0, 0), m.filter(ImageFilter.MaxFilter(9)))
        bg.paste(Image.new('RGBA', (BW, BH), (8, 4, 6, 255)), (0, 0), m)
    d = ImageDraw.Draw(bg)                  # 琴上的音孔和面具的眼
    d.ellipse([410, 820, 490, 900], fill=(200, 140, 170, 255))
    for x, y in ((350, 1260), (410, 1260), (550, 1280), (610, 1280)):
        d.ellipse([x - 12, y - 8, x + 12, y + 8], fill=(240, 190, 214, 255))
    return bg

if __name__ == '__main__':
    out = os.path.join(ROOT, 'Textures', 'LP', 'Classes')
    pv = os.path.join(ROOT, 'ArtSource', 'Preview'); os.makedirs(pv, exist_ok=True)
    for sub in ('Bard', 'Legendary', 'Subclass'): os.makedirs(os.path.join(out, sub), exist_ok=True)
    items = [('Bard', n, icon(n)) for n in CLASS] + [('Legendary', n, icon(n)) for n in LEGEND] + [('Subclass', 'Emblem_' + s, emblem(s)) for s in EMBLEMS] + [('Bard', 'Phantasm', phantasm())]
    cols = 9; cell = 150; rows = (len(items) + cols - 1) // cols
    sheet = Image.new('RGBA', (cols * cell + 20, rows * (cell + 24) + 30), (46, 48, 52, 255)); dr = ImageDraw.Draw(sheet)
    for i, (sub, n, im) in enumerate(items):
        im.save(os.path.join(out, sub, n + '.png'))
        x = 10 + (i % cols) * cell + 11; y = 20 + (i // cols) * (cell + 24)
        sheet.paste(im, (x, y), im); dr.text((x, y + 130), n, fill=(220, 220, 210, 255))
    sheet.save(os.path.join(pv, 'Bard-icons.png'))
    b = background(); b.save(os.path.join(out, 'Bard', 'Background.png')); b.resize((190, 303)).save(os.path.join(pv, 'Bard-background.png'))
    print('bard art written:', len(CLASS), 'icons +', len(LEGEND), 'legendary +', len(EMBLEMS), 'emblems + background + phantasm')
