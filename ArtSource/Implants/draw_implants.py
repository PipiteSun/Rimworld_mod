# 1.112.0 DND 植入物贴图（128×128）：器械师义肢 7 件、伊奥恩原石、11 颗伊奥恩颅石。画法沿用 RaceArt/draw_class_art（粗黑描边、平涂、右下暗部）。
# 用法：E:\Python\python.exe -I ArtSource\Implants\draw_implants.py .
import math, os, sys
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(os.path.dirname(HERE), 'RaceArt'))
from draw_class_art import Icon, poly_mask, ellipse_mask, line_mask, arc_mask, rrect_mask, union, star_pts, Image, GOLD, GOLD_D, CREAM, BONE, RUST, KHAKI_D
from draw_class_art2 import heart_mask, eye_mask, flame_mask, bolt_mask
from draw_classes3 import INKC
from PIL import ImageDraw

ROOT = sys.argv[1] if len(sys.argv) > 1 else '.'
STEEL = (160, 166, 176); STEEL_D = (110, 116, 128)
COLD = (120, 150, 176); MITH = (206, 222, 236); ADAM = (96, 84, 120); RUNE = (128, 170, 240); JADE = (110, 190, 140)

def arm(I, col, trim):
    I.layer(rrect_mask(46, 10, 76, 46, 10), col)                       # 肩
    I.layer(rrect_mask(50, 40, 72, 78, 8), col)                        # 上臂
    I.layer(ellipse_mask(61, 80, 9), trim)                             # 肘关节
    I.layer(rrect_mask(52, 82, 72, 108, 7), col)                       # 前臂
    I.layer(rrect_mask(48, 104, 78, 122, 6), trim)                     # 手
    I.layer(line_mask([(56, 18), (66, 18)], 3), RUNE, shade=False)
def leg(I, col, trim):
    I.layer(rrect_mask(46, 8, 78, 60, 10), col)
    I.layer(ellipse_mask(62, 62, 10), trim)
    I.layer(rrect_mask(50, 64, 74, 106, 8), col)
    I.layer(poly_mask([(46, 104), (78, 104), (96, 122), (46, 122)]), trim)
    I.layer(line_mask([(56, 24), (68, 24)], 3), RUNE, shade=False)
def stone(I, col, sym=None):
    I.layer(poly_mask([(64, 18), (100, 52), (82, 108), (46, 108), (28, 52)]), col)
    I.layer(poly_mask([(64, 18), (76, 52), (64, 108), (52, 52)]), tuple(min(255, int(c * 1.15)) for c in col), outline=False, shade=False)
    if sym: sym(I)
def txt(ch):
    def f(I):
        I.layer(ellipse_mask(64, 66, 14), CREAM, outline=True, shade=False)
    return f

ITEMS = {}
def item(name):
    def deco(fn): ITEMS[name] = fn; return fn
    return deco
@item('InfusedArm')
def _(I): arm(I, STEEL, STEEL_D)
@item('InfusedLeg')
def _(I): leg(I, STEEL, STEEL_D)
@item('ColdIronArm')
def _(I): arm(I, COLD, (80, 100, 124))
@item('MithralLeg')
def _(I): leg(I, MITH, (160, 176, 196))
@item('AdamantArm')
def _(I): arm(I, ADAM, (64, 54, 84))
@item('ClockEye')
def _(I):
    I.layer(ellipse_mask(64, 64, 44), STEEL_D)
    for a in range(0, 360, 45):
        r = math.radians(a); I.layer(rrect_mask(64 + math.cos(r) * 46 - 6, 64 + math.sin(r) * 46 - 6, 64 + math.cos(r) * 46 + 6, 64 + math.sin(r) * 46 + 6, 2), STEEL)
    I.layer(ellipse_mask(64, 64, 30), MITH); I.layer(ellipse_mask(64, 64, 16), JADE); I.layer(ellipse_mask(64, 64, 6), INKC, shade=False)
@item('MithralHeart')
def _(I):
    I.layer(heart_mask(64, 68, 1.25), MITH); I.layer(line_mask([(64, 40), (64, 98)], 4), ADAM, shade=False)
    I.layer(ellipse_mask(64, 64, 10), RUNE, glow=RUNE)
STONE_COL = {'Blank': (190, 190, 200), 'Str': (196, 70, 60), 'Dex': (90, 190, 120), 'Con': (210, 140, 60), 'Int': (90, 130, 220), 'Wis': (150, 200, 230), 'Cha': (220, 120, 190),
             'Sustain': (170, 210, 120), 'Regen': (220, 90, 110), 'Mastery': (230, 200, 90), 'Absorb': (130, 90, 190), 'Alert': (240, 240, 250)}
STONE_SYM = {'Str': lambda I: I.layer(rrect_mask(50, 56, 78, 76, 4), CREAM, shade=False),
             'Dex': lambda I: I.layer(poly_mask([(48, 74), (64, 52), (80, 74), (72, 74), (64, 64), (56, 74)]), CREAM, shade=False),
             'Con': lambda I: I.layer(heart_mask(64, 68, 0.35), CREAM, shade=False),
             'Int': lambda I: I.layer(arc_mask(64, 66, 10, 0, 360, 4), CREAM, shade=False),
             'Wis': lambda I: I.layer(eye_mask(64, 66, 26, 12), CREAM, shade=False),
             'Cha': lambda I: I.layer(poly_mask(star_pts(64, 66, 13, 5, 5)), CREAM, shade=False),
             'Sustain': lambda I: I.layer(ellipse_mask(64, 66, 10, 14), CREAM, shade=False),
             'Regen': lambda I: I.layer(union(rrect_mask(60, 54, 68, 80, 2), rrect_mask(52, 63, 76, 71, 2)), CREAM, shade=False),
             'Mastery': lambda I: I.layer(poly_mask([(50, 74), (54, 56), (64, 66), (74, 56), (78, 74)]), CREAM, shade=False),
             'Absorb': lambda I: I.layer(arc_mask(64, 66, 12, 30, 330, 4), CREAM, shade=False),
             'Alert': lambda I: I.layer(bolt_mask(64, 66, 0.3), INKC, shade=False)}
for k in STONE_COL:
    ITEMS['Ioun' + k] = (lambda k: lambda I: stone(I, STONE_COL[k], STONE_SYM.get(k)))(k)

# ---------- 1.113.0 巫妖血肉扩充、异怪嫁接 ----------
FLESH = (176, 96, 96); FLESH_D = (120, 60, 64); BONEC = (226, 216, 190); DRAG = {'Red': (200, 70, 50), 'White': (226, 236, 246), 'Blue': (80, 130, 220), 'Black': (60, 56, 66), 'Green': (90, 170, 90)}
@item('FleshHellHeart')
def _(I): I.layer(heart_mask(64, 70, 1.2), (90, 40, 60)); I.layer(flame_mask(64, 60, 0.6), (120, 220, 160), glow=(120, 220, 160))
@item('FleshBoneCage')
def _(I):
    I.layer(rrect_mask(56, 16, 72, 116, 6), BONEC)
    for k, y in enumerate((34, 52, 70, 88)):
        w = 40 - k * 4; I.layer(arc_mask(64, y + 30, w, 200, 340, 7), BONEC)
@item('FleshDeadEar')
def _(I): I.layer(ellipse_mask(64, 64, 26, 40), FLESH); I.layer(arc_mask(66, 60, 14, 120, 400, 6), FLESH_D, shade=False); I.layer(ellipse_mask(64, 64, 8), (120, 200, 170), glow=(120, 200, 170))
@item('FleshSoulJaw')
def _(I):
    I.layer(poly_mask([(20, 50), (108, 50), (96, 100), (32, 100)]), BONEC)
    for x in range(30, 100, 12): I.layer(poly_mask([(x, 50), (x + 10, 50), (x + 5, 68)]), CREAM)
@item('GraftBeholderEye')
def _(I): I.layer(ellipse_mask(64, 64, 42), (190, 120, 150)); I.layer(eye_mask(64, 64, 64, 36), CREAM); I.layer(ellipse_mask(64, 64, 14), (120, 60, 160)); I.layer(ellipse_mask(64, 64, 5, 12), INKC, shade=False)
@item('GraftDemonArm')
def _(I): arm(I, (150, 50, 50), (90, 30, 34)); I.layer(flame_mask(86, 96, 0.4), RUST, glow=RUST)
@item('GraftTrollGland')
def _(I): I.layer(ellipse_mask(64, 66, 36, 30), (110, 150, 90)); I.layer(ellipse_mask(56, 60, 10), (150, 200, 120), outline=False, shade=False); I.layer(line_mask([(30, 92), (98, 92)], 4), (70, 100, 60), shade=False)
for c, col in DRAG.items():
    ITEMS['GraftScale' + c] = (lambda col: lambda I: [I.layer(poly_mask([(64, 14), (104, 46), (92, 106), (36, 106), (24, 46)]), col), I.layer(arc_mask(64, 70, 22, 200, 340, 4), tuple(int(v * .7) for v in col), shade=False), I.layer(arc_mask(64, 90, 22, 200, 340, 4), tuple(int(v * .7) for v in col), shade=False)])(col)
    ITEMS['GraftBreath' + c] = (lambda col: lambda I: [I.layer(ellipse_mask(64, 70, 34, 28), FLESH), I.layer(flame_mask(64, 50, 0.7), col, glow=col)])(col)

# ---------- 1.114.0 维克那神器、卡斯之剑 ----------
@item('VecnaHand')
def _(I):
    I.layer(rrect_mask(50, 70, 78, 120, 8), (70, 60, 56))                       # 腕
    I.layer(poly_mask([(36, 74), (92, 74), (88, 40), (40, 40)]), (84, 72, 66))   # 掌
    for k, x in enumerate((40, 54, 68, 82)): I.layer(rrect_mask(x - 5, 10 + (k % 2) * 6, x + 5, 46, 4), (84, 72, 66))
    I.layer(poly_mask([(30, 66), (40, 50), (46, 70)]), (84, 72, 66)); I.layer(ellipse_mask(64, 60, 7), (140, 220, 240), glow=(140, 220, 240))
@item('VecnaEye')
def _(I): I.layer(ellipse_mask(64, 64, 40, 34), (120, 20, 30)); I.layer(eye_mask(64, 64, 60, 30), (230, 180, 60)); I.layer(ellipse_mask(64, 64, 6, 15), INKC, shade=False); I.layer(ellipse_mask(52, 52, 6), (255, 220, 220), outline=False, shade=False)
@item('SwordOfKas')
def _(I):
    I.layer(poly_mask([(60, 104), (68, 104), (72, 20), (64, 6), (56, 20)]), (190, 200, 210)); I.layer(line_mask([(64, 14), (64, 98)], 3), (150, 20, 30), shade=False)
    I.layer(rrect_mask(40, 98, 88, 108, 3), (60, 40, 50)); I.layer(rrect_mask(59, 106, 69, 124, 3), (80, 50, 40)); I.layer(ellipse_mask(64, 103, 4), (200, 30, 40), outline=False, shade=False)

if __name__ == '__main__':
    out = os.path.join(ROOT, 'Textures', 'Items', 'Implants'); os.makedirs(out, exist_ok=True)
    pv = os.path.join(ROOT, 'ArtSource', 'Preview'); os.makedirs(pv, exist_ok=True)
    ims = []
    for n, fn in ITEMS.items():
        I = Icon(); fn(I); im = I.done(128); im.save(os.path.join(out, n + '.png')); ims.append((n, im))
    cols = 10; cell = 150; rows = (len(ims) + cols - 1) // cols
    sheet = Image.new('RGBA', (cols * cell + 20, rows * (cell + 24) + 30), (46, 48, 52, 255)); dr = ImageDraw.Draw(sheet)
    for i, (n, im) in enumerate(ims):
        x = 10 + (i % cols) * cell + 11; y = 20 + (i // cols) * (cell + 24); sheet.paste(im, (x, y), im); dr.text((x, y + 130), n, fill=(220, 220, 210, 255))
    sheet.save(os.path.join(pv, 'Implants.png')); print('implant art:', len(ims))
