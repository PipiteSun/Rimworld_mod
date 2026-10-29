# 1.29.0 DND 枪械与法杖贴图（RimWorld 风格）：13 张 64×64 武器图（D3 + E2 + F5 + G3）+ 预览拼图。
#   画法同 draw_class_art.py：4 倍超采样，粗深描边 → 低饱和平涂 → 右下暗部，再缩回 64。
#   朝向对齐原版：枪口朝右、握把在画面中下部（同原版手枪/步枪）；法杖从左下斜到右上（同原版长矛，装备角度 -45）。
# 用法：python draw_gun_art.py <Textures/LP/Weapons 目录> [预览图路径]
import math, sys, os
from PIL import Image, ImageDraw, ImageFilter, ImageChops

OUT = sys.argv[1] if len(sys.argv) > 1 else 'out'
PREVIEW = sys.argv[2] if len(sys.argv) > 2 else None
SS = 4
S = 64 * SS
OW = 3 * SS          # 描边约 3px（64 图）

INK = (26, 23, 21)
STEEL = (122, 124, 126); STEEL_D = (84, 86, 90)
DARK = (70, 70, 76)
WOOD = (128, 92, 60);    WOOD_D = (92, 64, 40)
SILVER = (196, 198, 204)
MITHRAL = (150, 176, 196)
BRASS = (176, 146, 84)
GOLD = (204, 170, 86)
LAVA = (216, 110, 48);   LAVA_HI = (240, 178, 92)
OBSID = (52, 44, 58)
RUNE = (120, 186, 220)
ADAM = (78, 92, 82)
BONE = (214, 208, 182)
VIOLET = (150, 112, 190)
ICE = (176, 214, 228);   ICE_D = (120, 162, 184)
EMBER = (196, 80, 52)

def mask():
    return Image.new('L', (S, S), 0)
def P(pts):
    return [(x * SS, y * SS) for x, y in pts]
def poly(*pts):
    m = mask(); ImageDraw.Draw(m).polygon(P(pts), fill=255); return m
def rect(x0, y0, x1, y1, r=0.8):
    m = mask(); ImageDraw.Draw(m).rounded_rectangle([x0 * SS, y0 * SS, x1 * SS, y1 * SS], radius=r * SS, fill=255); return m
def ell(cx, cy, rx, ry=None):
    ry = rx if ry is None else ry
    m = mask(); ImageDraw.Draw(m).ellipse([(cx - rx) * SS, (cy - ry) * SS, (cx + rx) * SS, (cy + ry) * SS], fill=255); return m
def line(pts, w):
    m = mask(); d = ImageDraw.Draw(m); d.line(P(pts), fill=255, width=int(w * SS), joint='curve')
    for x, y in (pts[0], pts[-1]):
        d.ellipse([(x - w / 2) * SS, (y - w / 2) * SS, (x + w / 2) * SS, (y + w / 2) * SS], fill=255)
    return m
def union(*ms):
    o = ms[0]
    for m in ms[1:]: o = ImageChops.lighter(o, m)
    return o
def darker(c, f=0.72):
    return tuple(int(v * f) for v in c)
def diamond(cx, cy, rx, ry):
    return poly((cx, cy - ry), (cx + rx, cy), (cx, cy + ry), (cx - rx, cy))

class Art:
    def __init__(self):
        self.im = Image.new('RGBA', (S, S), (0, 0, 0, 0))
    def layer(self, m, fill, shade=True, outline=True, glow=None):
        if glow:
            g = m.filter(ImageFilter.MaxFilter(OW * 2 + 1)).filter(ImageFilter.GaussianBlur(OW))
            self.im.paste(Image.new('RGBA', (S, S), glow + (255,)), (0, 0), g.point(lambda v: int(v * 0.55)))
        if outline:
            self.im.paste(Image.new('RGBA', (S, S), INK + (255,)), (0, 0), m.filter(ImageFilter.MaxFilter(OW + 1)))
        self.im.paste(Image.new('RGBA', (S, S), fill + (255,)), (0, 0), m)
        if shade:
            d = 2 * SS
            band = ImageChops.subtract(m, ImageChops.offset(m, -d, -d))
            self.im.paste(Image.new('RGBA', (S, S), darker(fill) + (255,)), (0, 0), band)
        return self
    def detail(self, m, fill):  # 不描边、不加暗部的细节（符文、高光）
        return self.layer(m, fill, shade=False, outline=False)
    def done(self):
        return self.im.resize((64, 64), Image.LANCZOS)

def runes(a, pts, col=RUNE):
    for x, y in pts:
        a.detail(union(line([(x - 1.2, y - 1.4), (x + 1.2, y + 1.4)], 0.9), line([(x - 1.2, y + 1.4), (x + 0.2, y)], 0.9)), col)

# ---------------- D 组 ----------------
def silver_pistol():
    a = Art()
    a.layer(poly((20, 31), (29, 31), (26, 46), (17, 46)), WOOD)                     # 握把
    a.layer(line([(25, 34), (28, 39), (32, 34)], 1.6), STEEL_D, shade=False)       # 扳机护圈
    a.layer(rect(19, 24, 52, 31), SILVER)                                           # 套筒
    a.layer(rect(48, 26, 57, 30), STEEL)                                            # 枪口
    a.detail(line([(23, 27), (46, 27)], 0.9), (236, 236, 240))
    a.detail(ell(36, 28.5, 1.6), GOLD)                                              # 圣徽
    return a.done()

def lava_piercer():
    a = Art()
    a.layer(poly((3, 29), (16, 28), (18, 37), (5, 41)), WOOD)                       # 枪托
    a.layer(rect(15, 26, 40, 35), OBSID)                                            # 机匣（黑曜石）
    a.layer(rect(38, 28, 62, 32), STEEL)                                            # 枪管
    a.layer(rect(55, 27, 60, 33), STEEL_D)
    a.layer(poly((22, 35), (28, 35), (26, 44), (20, 44)), WOOD_D)                   # 握把
    a.layer(diamond(29, 30.5, 5, 3.4), LAVA, glow=LAVA)                            # 熔岩结晶
    a.detail(diamond(28, 29.6, 2, 1.2), LAVA_HI)
    return a.done()

def thunder_gun():
    a = Art()
    a.layer(poly((5, 29), (17, 28), (18, 36), (7, 39)), WOOD)
    a.layer(rect(16, 26, 42, 35), STEEL)
    a.layer(poly((22, 35), (28, 35), (26, 44), (20, 44)), WOOD_D)
    a.layer(rect(40, 27, 53, 31), STEEL_D)
    for y in (25.5, 33.5):                                                           # 一对电极
        a.layer(line([(50, 29 if y < 29 else 30), (58, y)], 2), BRASS)
    for x in (27, 31, 35):                                                           # 线圈
        a.layer(rect(x - 1, 25, x + 1, 36, 0.4), BRASS)
    a.detail(line([(57, 26), (60, 29), (57, 30), (61, 33)], 1.1), RUNE)              # 电弧
    return a.done()

# ---------------- E 组 ----------------
def doom():
    a = Art()
    a.layer(poly((2, 30), (16, 29), (16, 37), (4, 42)), DARK)
    a.layer(rect(14, 27, 38, 34), DARK)
    a.layer(rect(36, 29, 63, 32), (58, 58, 64))
    a.layer(poly((22, 34), (28, 34), (25, 43), (19, 43)), DARK)
    a.layer(rect(20, 21, 36, 25, 1.5), (60, 60, 66))                                 # 瞄准镜
    a.layer(rect(25, 25, 29, 27), (60, 60, 66), shade=False)
    a.layer(ell(9, 34, 2.4), BONE)                                                   # 骨饰
    a.detail(line([(17, 30.5), (34, 30.5)], 0.8), (150, 150, 168))
    return a.done()

def skyfire():
    a = Art()
    a.layer(poly((3, 30), (15, 29), (16, 36), (5, 39)), WOOD_D)
    a.layer(rect(14, 26, 34, 35), STEEL)
    a.layer(poly((22, 35), (28, 35), (26, 43), (20, 43)), WOOD_D)
    a.layer(poly((32, 27), (62, 29.5), (62, 31.5), (32, 34)), EMBER)                # 火纹长管
    for x in (40, 48, 56):
        a.detail(poly((x, 30.5), (x - 4, 28.4), (x - 2, 30.5), (x - 4, 32.6)), LAVA_HI)
    a.layer(diamond(24, 30.5, 4, 3), LAVA, glow=LAVA)
    return a.done()

# ---------------- F 组 ----------------
def runic_musket():
    a = Art()
    a.layer(poly((2, 29), (18, 28), (20, 37), (4, 42)), WOOD)
    a.layer(rect(17, 27, 36, 34), WOOD)
    a.layer(rect(30, 28, 62, 32), STEEL)
    a.layer(rect(58, 27, 62, 33), MITHRAL)
    a.layer(poly((22, 34), (27, 34), (25, 42), (20, 42)), WOOD_D)
    a.layer(rect(31, 27.5, 35, 32.5, 0.4), MITHRAL, shade=False)
    runes(a, [(40, 30), (46, 30), (52, 30)])
    return a.done()

def runic_pistol():
    a = Art()
    a.layer(poly((16, 31), (25, 31), (21, 47), (13, 46)), WOOD)
    a.layer(rect(15, 25, 44, 32), STEEL)
    a.layer(poly((42, 24), (52, 22), (52, 35), (42, 33)), MITHRAL)                 # 喇叭口
    a.layer(line([(21, 34), (24, 39), (28, 33)], 1.5), STEEL_D, shade=False)
    runes(a, [(24, 28.5), (31, 28.5), (38, 28.5)])
    return a.done()

def hammer_cannon():
    a = Art()
    a.layer(poly((4, 31), (14, 30), (15, 38), (6, 40)), WOOD)
    a.layer(rect(12, 26, 40, 38, 2), BRASS)                                          # 粗短炮身
    a.layer(poly((22, 37), (28, 37), (26, 46), (20, 46)), WOOD_D)
    a.layer(rect(38, 22, 50, 42, 1.5), STEEL)                                        # 锤头形炮口
    a.layer(rect(50, 25, 56, 39, 1), STEEL_D)
    a.detail(poly((44, 26), (41, 32), (44, 32), (42, 38), (47, 30), (44, 30)), RUNE)  # 雷纹
    return a.done()

def runic_repeater():
    a = Art()
    a.layer(poly((2, 30), (16, 29), (17, 38), (4, 42)), WOOD)
    a.layer(rect(14, 26, 34, 37), STEEL)
    a.layer(ell(30, 31.5, 6.5), STEEL_D)                                             # 转鼓
    for y in (26.5, 30.5, 34.5):                                                     # 三根枪管
        a.layer(rect(34, y - 1.4, 61, y + 1.4, 0.6), STEEL)
    a.layer(rect(56, 24.5, 59, 36.5, 0.6), MITHRAL)
    a.layer(poly((20, 37), (26, 37), (24, 45), (18, 45)), WOOD_D)
    runes(a, [(30, 31.5)])
    return a.done()

def mountain_wrath():
    a = Art()
    a.layer(poly((1, 28), (16, 27), (18, 38), (3, 44)), ADAM)
    a.layer(rect(14, 24, 40, 37, 1.5), ADAM)
    a.layer(rect(36, 27, 63, 33), (90, 100, 94))
    a.layer(rect(57, 25.5, 63, 34.5), GOLD)
    a.layer(poly((21, 37), (28, 37), (26, 46), (19, 46)), WOOD_D)
    a.layer(poly((22, 24), (26, 19), (32, 19), (36, 24)), ADAM)                     # 山形护板
    runes(a, [(20, 30.5), (27, 30.5), (34, 30.5)], GOLD)
    a.detail(line([(40, 30), (54, 30)], 0.9), GOLD)
    return a.done()

# ---------------- G 组：法杖（左下 → 右上） ----------------
def staff(head_col, head_hi, prongs_col, extra=None):
    a = Art()
    a.layer(line([(8, 58), (46, 18)], 3.4), WOOD)                                   # 杖身
    a.layer(line([(14, 51.7), (17, 48.5)], 3.8), prongs_col, shade=False)          # 握缠
    for ang in (-35, 55):                                                            # 杖头爪
        r = math.radians(ang)
        a.layer(line([(46, 18), (46 + 9 * math.cos(r), 18 - 9 * math.sin(r))], 2), prongs_col)
    if extra: extra(a)
    a.layer(diamond(51, 13, 5.5, 6.5), head_col, glow=head_col)                     # 宝石
    a.detail(diamond(50, 11.5, 2, 2.6), head_hi)
    return a.done()

def missile_staff():
    return staff(VIOLET, (214, 196, 236), SILVER)

def fire_staff():
    def flames(a):
        for (x, y) in ((56, 20), (44, 9)):
            a.layer(poly((x, y - 4), (x + 2.6, y + 1.5), (x, y + 3), (x - 2.6, y + 1.5)), EMBER, shade=False)
    return staff(LAVA, LAVA_HI, BRASS, flames)

def frost_staff():
    def shards(a):
        a.layer(line([(8, 58), (46, 18)], 3.4), (140, 150, 160))                    # 寒铁杖身
        for (x, y, l) in ((30, 34, 5), (22, 43, 4)):
            a.layer(poly((x, y), (x + l, y - l - 1), (x + 1.5, y - 1)), ICE, shade=False)
    return staff(ICE, (236, 248, 252), ICE_D, shards)

ART = [('D', 'SilverPistol', silver_pistol), ('D', 'LavaPiercer', lava_piercer), ('D', 'ThunderGun', thunder_gun),
       ('E', 'Doom', doom), ('E', 'Skyfire', skyfire),
       ('F', 'RunicMusket', runic_musket), ('F', 'RunicPistol', runic_pistol), ('F', 'HammerCannon', hammer_cannon),
       ('F', 'RunicRepeater', runic_repeater), ('F', 'MountainWrath', mountain_wrath),
       ('G', 'MissileStaff', missile_staff), ('G', 'FireStaff', fire_staff), ('G', 'FrostStaff', frost_staff)]

def preview(images):
    cell, pad, cols = 192, 16, 5
    rows = (len(images) + cols - 1) // cols
    sheet = Image.new('RGBA', (cols * cell + (cols + 1) * pad, rows * (cell + 28) + (rows + 1) * pad), (54, 52, 48, 255))
    d = ImageDraw.Draw(sheet)
    for i, (grp, name, im) in enumerate(images):
        x = pad + (i % cols) * (cell + pad); y = pad + (i // cols) * (cell + 28 + pad)
        d.rounded_rectangle([x, y, x + cell, y + cell], radius=8, fill=(84, 80, 72, 255))
        sheet.paste(im.resize((cell, cell), Image.NEAREST), (x, y), im.resize((cell, cell), Image.NEAREST))
        d.text((x + 6, y + cell + 6), grp + '  ' + name, fill=(230, 226, 210, 255))
    return sheet

if __name__ == '__main__':
    os.makedirs(OUT, exist_ok=True)
    made = []
    for grp, name, fn in ART:
        im = fn(); im.save(os.path.join(OUT, name + '.png')); made.append((grp, name, im))
    if PREVIEW:
        preview(made).save(PREVIEW)
    print('weapons written to', OUT)
