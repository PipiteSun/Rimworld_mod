# 1.111.0 器械师子职业（炼金师、装甲师、堡垒工程师）的美术：18 个能力图标、3 个徽记（128×128），
# 8 瓶药剂（沿用 draw_item_art 的药水瓶），符文地雷、奥术电容、要塞核心贴图 + 预览图。
# 画法沿用 draw_subclasses2.py：粗黑描边、平涂 + 右下暗部、低饱和。只写本批的新文件。
# 用法：python -I draw_subclasses4.py <模组根目录>
import math, sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from draw_class_art import (Icon, poly_mask, ellipse_mask, line_mask, arc_mask, rrect_mask, union, star_pts, shield_pts,
                            Image, CREAM, CREAM_D, KHAKI, KHAKI_D, SLATE, SLATE_D, RUST, RUST_D, GOLD, GOLD_D, BONE, BONE_D, STONE, STONE_D)
from draw_class_art2 import flame_mask, bolt_mask, heart_mask, fist_mask, rays
from draw_classes3 import MOSS, MOSS_D, INKC
from draw_legendary_174 import gear
from PIL import ImageDraw

ROOT = sys.argv[1] if len(sys.argv) > 1 else '.'
ORANGE = (232, 140, 56); ORANGE_D = (160, 90, 30)    # 炼金橙
STEELC = (150, 158, 172); STEELC_D = (96, 102, 116)  # 钢灰
FORT = (176, 146, 104); FORT_D = (120, 96, 64)       # 石褐
ARC = (128, 160, 236)                                  # 符文蓝
GLASS = (198, 214, 220); CORK = (150, 108, 70)
LIQ = {'Healing': (196, 48, 52), 'Swift': (90, 190, 220), 'Tough': (150, 120, 80), 'Light': (200, 230, 160), 'Lucid': (150, 120, 220), 'Courage': (236, 170, 40),
       'Restorative': (90, 180, 110), 'GreaterRestorative': (170, 30, 60)}

def flask(I, cx, cy, s=1.0, liq=ORANGE):
    I.layer(union(ellipse_mask(cx, cy + 10 * s, 26 * s), rrect_mask(cx - 8 * s, cy - 30 * s, cx + 8 * s, cy - 6 * s, 2)), GLASS)
    I.layer(union(ellipse_mask(cx, cy + 14 * s, 22 * s, 18 * s)), liq, outline=False)
    I.layer(rrect_mask(cx - 10 * s, cy - 38 * s, cx + 10 * s, cy - 28 * s, 2), CORK)
def chest(I, cx, cy, s=1.0, col=STEELC):     # 胸甲
    I.layer(poly_mask([(cx - 34 * s, cy - 30 * s), (cx - 14 * s, cy - 36 * s), (cx, cy - 26 * s), (cx + 14 * s, cy - 36 * s), (cx + 34 * s, cy - 30 * s),
                       (cx + 28 * s, cy + 6 * s), (cx + 22 * s, cy + 34 * s), (cx - 22 * s, cy + 34 * s), (cx - 28 * s, cy + 6 * s)]), col)
    I.layer(line_mask([(cx, cy - 22 * s), (cx, cy + 30 * s)], 3), STEELC_D, shade=False)
def wall(I, x0, y0, x1, y1, col=FORT):       # 砖墙
    I.layer(rrect_mask(x0, y0, x1, y1, 3), col)
    h = (y1 - y0) / 3.0
    for k in range(1, 3): I.layer(line_mask([(x0 + 2, y0 + k * h), (x1 - 2, y0 + k * h)], 3), FORT_D, shade=False)
    for k in range(3):
        off = 0 if k % 2 == 0 else (x1 - x0) / 4.0
        for x in (x0 + off + (x1 - x0) / 2.0,): I.layer(line_mask([(x, y0 + k * h + 2), (x, y0 + (k + 1) * h - 2)], 3), FORT_D, shade=False)
def tower(I, cx, cy, s=1.0, col=FORT):
    I.layer(rrect_mask(cx - 24 * s, cy - 20 * s, cx + 24 * s, cy + 40 * s, 3), col)
    for k in (-1, 0, 1): I.layer(rrect_mask(cx + k * 16 * s - 6 * s, cy - 34 * s, cx + k * 16 * s + 6 * s, cy - 18 * s, 2), col)
    I.layer(rrect_mask(cx - 7 * s, cy + 18 * s, cx + 7 * s, cy + 40 * s, 6 * s), INKC, shade=False)
def rune(I, cx, cy, r, col=ARC, w=4):
    I.layer(arc_mask(cx, cy, r, 0, 360, w), col, shade=False)
    I.layer(poly_mask(star_pts(cx, cy, r * 0.62, r * 0.25, 4)), col, shade=False)
def arrow_up(I, cx, cy, col=GOLD):
    I.layer(poly_mask([(cx, cy - 18), (cx + 14, cy), (cx + 6, cy), (cx + 6, cy + 16), (cx - 6, cy + 16), (cx - 6, cy), (cx - 14, cy)]), col)

def icon(name):
    I = Icon()
    # ============ 炼金师 ============
    if name == 'AlchExperimental':     # 实验药剂：冒泡的橙色药瓶 + 闪光
        flask(I, 60, 68, 1.2, ORANGE); I.layer(poly_mask(star_pts(100, 30, 12, 5, 4)), GOLD)
        for x, y, r in ((52, 80, 4), (66, 72, 3)): I.layer(ellipse_mask(x, y, r), CREAM, outline=False, shade=False)
    elif name == 'AlchSavant':         # 炼金学识：摊开的书上立着药瓶
        I.layer(poly_mask([(16, 92), (64, 100), (112, 92), (112, 112), (64, 120), (16, 112)]), BONE); I.layer(line_mask([(64, 100), (64, 120)], 3), BONE_D, shade=False)
        flask(I, 64, 56, 0.95, ORANGE)
    elif name == 'AlchRestorative':    # 复原试剂：绿色药瓶 + 白十字
        flask(I, 64, 64, 1.25, LIQ['Restorative'])
        I.layer(union(rrect_mask(58, 68, 70, 96, 2), rrect_mask(50, 76, 78, 88, 2)), CREAM, shade=False)
    elif name == 'AlchChemist':        # 化学大师：三瓶并排
        flask(I, 32, 74, 0.7, LIQ['Swift']); flask(I, 96, 74, 0.7, LIQ['Lucid']); flask(I, 64, 64, 0.95, ORANGE)
    elif name == 'AlchPick':           # 指定药剂：药瓶外的循环箭头
        I.layer(arc_mask(64, 64, 50, 200, 520, 6), GOLD, shade=False); I.layer(poly_mask([(102, 30), (118, 40), (100, 50)]), GOLD)
        flask(I, 64, 70, 0.9, ORANGE)
    elif name == 'AlchGreater':        # 大复原药剂：深红大瓶 + 心 + 金星
        flask(I, 60, 68, 1.3, LIQ['GreaterRestorative']); I.layer(heart_mask(60, 86, 0.35), (250, 210, 210), shade=False)
        I.layer(poly_mask(star_pts(102, 28, 13, 5, 5)), GOLD)
    # ============ 装甲师 ============
    elif name == 'ArmArcane':          # 奥术装甲：胸甲上发光的符文
        chest(I, 64, 64, 1.3); rune(I, 64, 64, 16)
    elif name == 'ArmMode':            # 切换装甲模式：一半钢盾一半蓝盾 + 两个箭头
        I.layer(poly_mask(shield_pts(64, 64, 76, 88)), STEELC); I.layer(poly_mask([(64, 20), (102, 26), (100, 64), (64, 108)]), ARC, outline=False)
        I.layer(poly_mask([(10, 40), (26, 30), (26, 50)]), GOLD); I.layer(poly_mask([(118, 88), (102, 78), (102, 98)]), GOLD)
    elif name == 'ArmField':           # 防御力场：胸甲外的蓝色穹顶
        I.layer(arc_mask(64, 84, 52, 180, 360, 6), ARC, shade=False); I.layer(arc_mask(64, 84, 40, 180, 360, 3), ARC, shade=False)
        chest(I, 64, 84, 0.8)
    elif name == 'ArmLauncher':        # 闪电发射器：拳套 + 闪电
        I.layer(fist_mask(44, 80, 0.8), STEELC); I.layer(bolt_mask(92, 40, 0.8), GOLD)
    elif name == 'ArmModify':          # 甲胄改装：胸甲 + 扳手
        chest(I, 54, 66, 1.0)
        I.layer(line_mask([(78, 104), (110, 64)], 9), KHAKI_D); I.layer(arc_mask(112, 58, 11, 300, 600, 7), STEELC_D)
    elif name == 'ArmUpgrade':         # 甲胄升级：胸甲 + 上箭头
        chest(I, 56, 70, 1.0); arrow_up(I, 100, 40)
    elif name == 'ArmPerfect':         # 完美装甲：密封头盔 + 金星
        I.layer(rrect_mask(36, 30, 92, 104, 22), STEELC); I.layer(rrect_mask(46, 56, 82, 70, 4), ARC, shade=False)
        I.layer(poly_mask(star_pts(104, 28, 13, 5, 5)), GOLD)
    # ============ 堡垒工程师 ============
    elif name == 'FortReinforce':      # 加固施工：砖墙 + 锤子
        wall(I, 16, 56, 92, 112); I.layer(line_mask([(70, 70), (110, 22)], 8), KHAKI_D); I.layer(rrect_mask(94, 10, 122, 34, 4), STEELC)
    elif name == 'FortMine':           # 符文地雷：地面上的符文圈
        I.layer(ellipse_mask(64, 84, 50, 22), FORT_D); rune(I, 64, 80, 24, ARC, 5); rays(I, 64, 46, 10, 22, 6, GOLD, w=3, a0=-90)
    elif name == 'FortCapacitor':      # 奥术电容：符文电容罐 + 闪电
        I.layer(rrect_mask(36, 24, 92, 110, 10), STEELC); I.layer(rrect_mask(36, 24, 92, 40, 6), STEELC_D)
        I.layer(bolt_mask(64, 74, 0.6), ARC)
    elif name == 'FortFortify':        # 战地加固：墙前一面盾
        wall(I, 12, 40, 116, 100); I.layer(poly_mask(shield_pts(64, 70, 50, 60)), ARC)
    elif name == 'FortCitadel':        # 要塞之心：塔楼 + 金色核心
        tower(I, 64, 64, 1.2); I.layer(ellipse_mask(64, 52, 9), GOLD, glow=GOLD)
    return I.done(128)

ICONS4 = ['AlchExperimental', 'AlchSavant', 'AlchRestorative', 'AlchChemist', 'AlchPick', 'AlchGreater',
          'ArmArcane', 'ArmMode', 'ArmField', 'ArmLauncher', 'ArmModify', 'ArmUpgrade', 'ArmPerfect',
          'FortReinforce', 'FortMine', 'FortCapacitor', 'FortFortify', 'FortCitadel']
EMBLEMS = {'Alchemist': ORANGE_D, 'Armorer': STEELC_D, 'FortEngineer': FORT_D}

def emblem(sub):
    I = Icon(); col = EMBLEMS[sub]
    I.layer(poly_mask(shield_pts(64, 64, 100, 116)), GOLD_D)
    I.layer(poly_mask(shield_pts(64, 62, 86, 100)), col)
    if sub == 'Alchemist': flask(I, 64, 66, 0.9, ORANGE)
    elif sub == 'Armorer': chest(I, 64, 62, 0.85, STEELC); rune(I, 64, 60, 10)
    elif sub == 'FortEngineer': tower(I, 64, 62, 0.75, FORT)
    return I.done(128)

# ---------- 建筑 ----------
def rune_mine():
    I = Icon()
    I.layer(ellipse_mask(64, 64, 40, 40), (120, 110, 96), shade=False)
    rune(I, 64, 64, 30, ARC, 5); I.layer(ellipse_mask(64, 64, 6), CREAM, outline=False, shade=False)
    return I.done(128)
def capacitor():
    I = Icon()
    I.layer(rrect_mask(30, 40, 98, 116, 10), STEELC_D); I.layer(rrect_mask(34, 18, 94, 104, 12), STEELC)
    for y in (40, 60, 80): I.layer(line_mask([(36, y), (92, y)], 3), STEELC_D, shade=False)
    I.layer(ellipse_mask(64, 18, 30, 9), STEELC_D); rune(I, 64, 60, 15, ARC, 4)
    return I.done(128)
def fortress_core():
    I = Icon()
    I.layer(rrect_mask(8, 20, 120, 122, 8), FORT_D)
    for x0, x1 in ((8, 32), (52, 76), (96, 120)): I.layer(rrect_mask(x0, 6, x1, 26, 3), FORT)
    I.layer(rrect_mask(16, 28, 112, 114, 6), FORT)
    gear(I, 64, 70, 30, STEELC); I.layer(ellipse_mask(64, 70, 13), GOLD, glow=GOLD); rune(I, 64, 70, 9, CREAM, 3)
    return I.done(384)

if __name__ == '__main__':
    out = os.path.join(ROOT, 'Textures', 'LP', 'Classes'); pv = os.path.join(ROOT, 'ArtSource', 'Preview'); os.makedirs(pv, exist_ok=True)
    os.makedirs(os.path.join(out, 'Subclass4'), exist_ok=True)
    items = [('Subclass4', n, icon(n)) for n in ICONS4] + [('Subclass', 'Emblem_' + s, emblem(s)) for s in EMBLEMS]
    bdir = os.path.join(ROOT, 'Textures', 'Things', 'Building'); os.makedirs(bdir, exist_ok=True)
    for n, fn in (('LP_RuneMine', rune_mine), ('LP_ArcaneCapacitor', capacitor), ('LP_FortressCore', fortress_core)):
        im = fn(); im.save(os.path.join(bdir, n + '.png')); items.append(('-', n, im.resize((128, 128), Image.LANCZOS) if im.size[0] != 128 else im))
    import draw_race_art as base, draw_item_art as ia
    base.TEX = os.path.join(ROOT, 'Textures')
    def deco(n):
        def f(p):
            if n == 'Restorative': p.add([('rect', (46, 64, 54, 84)), ('rect', (40, 70, 60, 78))], (244, 244, 240), 1.2)
            elif n == 'GreaterRestorative': p.add(ia.poly([(50, 82), (38, 70), (38, 63), (43, 59), (50, 64), (57, 59), (62, 63), (62, 70)]), (250, 214, 214), 1.6)
            elif n == 'Swift': p.add(ia.poly([(36, 70), (58, 62), (52, 70), (66, 70), (44, 82), (50, 74)]), (236, 246, 255), 1.2)
        return f
    shapes = {'Healing': 'round', 'Swift': 'tall', 'Tough': 'square', 'Light': 'cone', 'Lucid': 'round', 'Courage': 'square', 'Restorative': 'tall', 'GreaterRestorative': 'square'}
    for n, liq in LIQ.items():
        pic = ia.flask(liq, shapes[n], ia.light(liq, .45) if n in ('Healing', 'Lucid', 'Light') else None, deco(n))
        pic.save('Items/Alchemist/' + n, 128)
        items.append(('-', 'P_' + n, Image.open(os.path.join(ROOT, 'Textures', 'Items', 'Alchemist', n + '.png'))))
    cols = 10; cell = 150; rows = (len(items) + cols - 1) // cols
    sheet = Image.new('RGBA', (cols * cell + 20, rows * (cell + 24) + 30), (46, 48, 52, 255)); dr = ImageDraw.Draw(sheet)
    for i, (sub, n, im) in enumerate(items):
        if sub != '-': im.save(os.path.join(out, sub, n + '.png'))
        x = 10 + (i % cols) * cell + 11; y = 20 + (i // cols) * (cell + 24)
        sheet.paste(im, (x, y), im.convert('RGBA')); dr.text((x, y + 130), n, fill=(220, 220, 210, 255))
    sheet.save(os.path.join(pv, 'Subclasses4-icons.png'))
    print('subclass4 art written:', len(ICONS4), 'icons +', len(EMBLEMS), 'emblems + 3 buildings + 8 potions')
