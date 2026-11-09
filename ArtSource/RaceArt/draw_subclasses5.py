# 1.135.0 术士子职业补完：神圣之魂（圣金）、暗影魔法（暗紫）、时钟之魂（铜金）的 3 个徽记和 13 个能力图标（128×128）。
# 画法沿用 draw_subclasses.py / draw_subclasses2.py：粗黑描边、平涂 + 右下暗部、低饱和。
# 用法：python draw_subclasses5.py <模组根目录>
import math, sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from draw_class_art import (Icon, poly_mask, ellipse_mask, line_mask, arc_mask, rrect_mask, union, star_pts, shield_pts,
                            Image, CREAM, BONE, GOLD, GOLD_D, CREAM_D)
from draw_class_art2 import flame_mask, eye_mask, wing_mask, heart_mask, rays, wolf_mask, palm_mask, bolt_mask
from draw_legendary_174 import gear
from PIL import ImageDraw

ROOT = sys.argv[1] if len(sys.argv) > 1 else '.'
HOLY = (240, 214, 120); HOLY_D = (176, 148, 70)    # 圣金
SHADE = (92, 64, 130); SHADE_D = (54, 36, 82)      # 暗紫
COPPER = (206, 150, 84); COPPER_D = (140, 96, 50)  # 铜金
INKC = (30, 26, 34)
EMBLEMS = {'DivineSoul': HOLY, 'ShadowMagic': SHADE, 'ClockworkSoul': COPPER}

def moon(I, cx, cy, r, col):
    I.layer(ellipse_mask(cx, cy, r), col)
    I.layer(ellipse_mask(cx + r * 0.45, cy - r * 0.2, r * 0.85), INKC, outline=False, shade=False)

def icon(name):
    I = Icon()
    if name == 'DSCure':        # 治疗伤口：金色心 + 光芒
        rays(I, 64, 64, 30, 46, 10, HOLY, w=4); I.layer(heart_mask(64, 66, 0.75), (240, 150, 150))
    elif name == 'DSFavored':   # 神恩庇佑：手掌上一颗星
        I.layer(palm_mask(64, 74, 0.8), BONE); I.layer(poly_mask(star_pts(64, 40, 18, 7, 5)), HOLY)
    elif name == 'DSResonance': # 神恩共鸣：两道相扣的光环
        I.layer(arc_mask(50, 64, 24, 0, 360, 6), HOLY, shade=False); I.layer(arc_mask(78, 64, 24, 0, 360, 6), HOLY_D, shade=False)
    elif name == 'DSAngel':     # 天使形态：双翼 + 光环
        I.layer(wing_mask(64, 66, -1, 0.8), CREAM); I.layer(wing_mask(64, 66, 1, 0.8), CREAM); I.layer(arc_mask(64, 34, 16, 0, 360, 5), HOLY, shade=False)
    elif name == 'DSMiracle':   # 神迹：大光芒 + 白色圆核
        rays(I, 64, 64, 18, 52, 16, HOLY, w=5); I.layer(ellipse_mask(64, 64, 18), CREAM)
    elif name == 'SMEyes':      # 暗视之眼：黑暗中一只紫眼
        I.layer(ellipse_mask(64, 64, 50), SHADE_D); I.layer(eye_mask(64, 64, 64, 30), (200, 170, 240)); I.layer(ellipse_mask(64, 64, 6, 14), INKC, shade=False)
    elif name == 'SMHound':     # 影犬：紫色狼头
        I.layer(wolf_mask(64, 66, 0.9), SHADE); I.layer(ellipse_mask(54, 58, 4), (230, 200, 255), outline=False, shade=False)
    elif name == 'SMStep':      # 暗影步：两轮月牙之间的虚线
        moon(I, 34, 84, 18, SHADE); moon(I, 92, 40, 18, SHADE)
        for k in range(5): x = 46 + k * 9; y = 76 - k * 8; I.layer(ellipse_mask(x, y, 3), (200, 170, 240), outline=False, shade=False)
    elif name == 'SMForm':      # 影之形态：人形剪影被黑雾包裹
        I.layer(ellipse_mask(64, 70, 44, 50), SHADE_D); I.layer(ellipse_mask(64, 44, 12), INKC); I.layer(rrect_mask(50, 56, 78, 100, 10), INKC)
    elif name == 'CWBalance':   # 秩序之手：天平
        I.layer(line_mask([(64, 30), (64, 98)], 6), COPPER); I.layer(line_mask([(30, 46), (98, 46)], 6), COPPER)
        I.layer(arc_mask(38, 62, 14, 0, 180, 5), COPPER_D, shade=False); I.layer(arc_mask(90, 62, 14, 0, 180, 5), COPPER_D, shade=False)
    elif name == 'CWWard':      # 秩序护盾：铜色盾 + 齿轮
        I.layer(poly_mask(shield_pts(64, 64, 76, 90)), COPPER); gear(I, 64, 60, 20, COPPER_D)
    elif name == 'CWDomain':    # 秩序领域：大齿轮 + 光环
        gear(I, 64, 64, 34, COPPER); I.layer(arc_mask(64, 64, 48, 0, 360, 4), HOLY, shade=False)
    elif name == 'CWLegion':    # 秩序军团：三个小齿轮
        for (x, y) in ((40, 80), (88, 80), (64, 42)): gear(I, x, y, 18, COPPER)
    return I.done(128)

def emblem(sub):
    I = Icon(); col = EMBLEMS[sub]
    I.layer(poly_mask(shield_pts(64, 64, 100, 116)), GOLD_D)
    I.layer(poly_mask(shield_pts(64, 62, 86, 100)), col)
    if sub == 'DivineSoul': I.layer(wing_mask(64, 62, -1, 0.6), CREAM); I.layer(wing_mask(64, 62, 1, 0.6), CREAM); I.layer(arc_mask(64, 38, 12, 0, 360, 4), HOLY, shade=False)
    elif sub == 'ShadowMagic': moon(I, 64, 60, 26, (200, 170, 240))
    elif sub == 'ClockworkSoul': gear(I, 64, 62, 26, (240, 210, 150))
    return I.done(128)

ICONS = ['DSCure', 'DSFavored', 'DSResonance', 'DSAngel', 'DSMiracle', 'SMEyes', 'SMHound', 'SMStep', 'SMForm', 'CWBalance', 'CWWard', 'CWDomain', 'CWLegion']
if __name__ == '__main__':
    out = os.path.join(ROOT, 'Textures', 'LP', 'Classes'); pv = os.path.join(ROOT, 'ArtSource', 'Preview'); os.makedirs(pv, exist_ok=True)
    os.makedirs(os.path.join(out, 'Subclass5'), exist_ok=True); os.makedirs(os.path.join(out, 'Subclass'), exist_ok=True)
    items = [('Subclass5', n, icon(n)) for n in ICONS] + [('Subclass', 'Emblem_' + s, emblem(s)) for s in EMBLEMS]
    cols = 8; cell = 150; rows = (len(items) + cols - 1) // cols
    sheet = Image.new('RGBA', (cols * cell + 20, rows * (cell + 24) + 30), (46, 48, 52, 255)); dr = ImageDraw.Draw(sheet)
    for i, (sub, n, im) in enumerate(items):
        im.save(os.path.join(out, sub, n + '.png'))
        x = 10 + (i % cols) * cell + 11; y = 20 + (i // cols) * (cell + 24)
        sheet.paste(im, (x, y), im); dr.text((x, y + 130), n, fill=(220, 220, 210, 255))
    sheet.save(os.path.join(pv, 'Subclasses5-icons.png'))
    print('subclass5 art written:', len(ICONS), 'icons +', len(EMBLEMS), 'emblems')
