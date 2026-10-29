# 1.56.0 Ideology 桥接贴图（RimWorld 画风：粗墨线、平涂 + 右下暗部、低饱和；沿用 draw_faith.py / draw_class_art.py 的画法）。
# 用法：python -I ArtSource/RaceArt/draw_ideo.py [模组根目录]
# 输出到 Bridges/Ideology/Textures：
#   UI/Memes/LP/*.png（16 个模因图标 128，圆盘底）  UI/IdeoIcons/LP/*.png（同符号纯白剪影 256，供意识形态着色）
#   Things/Building/LP/Statues/*.png（10 座神像 256，2×2，单张南向；近白石色 + _m 遮罩，红 = 材料着色）
#   UI/Abilities/LP、UI/Issues/LP、UI/Roles/LP 若干 128 图标；预览拼图 ArtSource/Preview/Ideology-1.56.0.png。
import math, os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from PIL import Image, ImageDraw, ImageFilter, ImageChops, ImageFont
import draw_class_art as A
from draw_class_art import (poly_mask, ellipse_mask, line_mask, arc_mask, rrect_mask, union, subtract, star_pts, bust_mask,
                            darker, INK, CREAM, CREAM_D, KHAKI, KHAKI_D, SLATE, SLATE_D, RUST, RUST_D, VIOLET, VIOLET_D,
                            GOLD, GOLD_D, BONE, BONE_D, STONE, STONE_D, SS, S)
import draw_faith as F
from draw_faith import (SILVER, SILVER_D, DAWN, DAWN_L, SKY, NIGHT, NIGHT_D, BLOOD, EMBER, EMBER_L, GREEN, ASH,
                        rays, bolt, hand, scales, skull, flame_mask, sun)

ROOT = sys.argv[1] if len(sys.argv) > 1 else os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..'))
OUT = os.path.join(ROOT, 'Bridges', 'Ideology', 'Textures')
PREVIEW = os.path.join(ROOT, 'ArtSource', 'Preview', 'Ideology-1.56.0.png')
GREY, GREY_D, GREY_F = (228, 226, 222), (192, 190, 186), (160, 158, 154)   # 神像材料色区（近白，游戏按材料色相乘）
DARK = (60, 56, 60)


def save(img, *path):
    p = os.path.join(OUT, *path)
    os.makedirs(os.path.dirname(p), exist_ok=True)
    img.save(p)
    return p


def dilate(m, px):
    return m.filter(ImageFilter.MaxFilter(int(px) * 2 + 1))


def lens_pts(cx, cy, w, h, n=24):
    top = [(cx - w / 2 + w * i / n, cy - h / 2 * math.sin(math.pi * i / n)) for i in range(n + 1)]
    bot = [(cx + w / 2 - w * i / n, cy + h / 2 * math.sin(math.pi * i / n)) for i in range(1, n)]
    return top + bot


def oval_pts(cx, cy, rx, ry, rot, n=48):
    c, s = math.cos(rot), math.sin(rot)
    return [(cx + rx * math.cos(t) * c - ry * math.sin(t) * s, cy + rx * math.cos(t) * s + ry * math.sin(t) * c)
            for t in [2 * math.pi * i / n for i in range(n + 1)]]


def ring(cx, cy, rx, ry, w):
    return subtract(ellipse_mask(cx, cy, rx, ry), ellipse_mask(cx, cy, rx - w, ry - w))


def chevron(cx, cy, w, h, t):
    return poly_mask([(cx - w, cy), (cx, cy - h), (cx + w, cy), (cx + w, cy + t), (cx, cy - h + t), (cx - w, cy + t)])


FLAME = [(0, 0), (12, 22), (22, 12), (26, 34), (38, 26), (34, 54), (40, 68), (26, 84), (-26, 84), (-40, 68), (-34, 54),
         (-38, 26), (-26, 34), (-22, 12), (-12, 22)]


def tongues(cx, top, sc=1.0):  # 多焰舌火焰（比 flame_mask 更像火），宽 80×高 84 × sc
    return poly_mask([(cx + x * sc, top + y * sc) for x, y in FLAME])


def slash(x0, y0, x1, y1, w):
    mx, my = x0 + (x1 - x0) * .4, y0 + (y1 - y0) * .4
    L = math.hypot(x1 - x0, y1 - y0); nx, ny = -(y1 - y0) / L * w, (x1 - x0) / L * w
    return poly_mask([(x0, y0), (mx + nx, my + ny), (x1, y1), (mx - nx, my - ny)])


# ==================== 带剪影记录的图标画布 ====================
class SymIcon(A.Icon):
    """在 draw_class_art.Icon 之上同时记录一张剪影（意识形态图标用）。
    sil=True 叠加（先挖一圈缝，让前后层分开）；'merge' 叠加不挖缝；'cut' 从剪影中挖掉；False 不计入。"""
    GAP = 1.5 * SS

    def __init__(self):
        super().__init__(); self.sil = Image.new('L', (S, S), 0)

    def layer(self, m, fill, shade=True, outline=True, glow=None, sil=True):
        super().layer(m, fill, shade, outline, glow)
        if sil == 'cut':
            self.sil = subtract(self.sil, m)
        elif sil:
            if sil is True:
                self.sil = subtract(self.sil, dilate(m, self.GAP))
            self.sil = union(self.sil, m)
        return self

    def silhouette(self, size=256, margin=12):
        box = self.sil.getbbox()
        m = self.sil.crop(box)
        k = (size - 2 * margin) / max(m.width, m.height)
        m = m.resize((max(1, round(m.width * k)), max(1, round(m.height * k))), Image.LANCZOS)
        out = Image.new('RGBA', (size, size), (255, 255, 255, 0))
        a = Image.new('L', (size, size), 0); a.paste(m, ((size - m.width) // 2, (size - m.height) // 2))
        out.putalpha(a)
        return out


def ink(I, pts, w):  # 内部墨线细节（剪影中挖空）
    I.layer(line_mask(pts, w), INK, outline=False, shade=False, sil='cut')


# ==================== 16 个符号（128 坐标，圆盘半径约 56 以内） ====================
MEMES = {  # 名称: (圆盘主色, 圆盘底色系数)
    'Lathander': ((204, 122, 118), .55), 'Tyr': ((112, 136, 188), .55), 'Mystra': ((128, 112, 198), .55),
    'Kelemvor': ((128, 148, 130), .55), 'Malar': ((160, 86, 64), .55), 'Talos': ((122, 134, 158), .55),
    'Bane': ((112, 150, 100), .62), 'Shar': ((122, 88, 166), .55), 'Surtr': ((214, 112, 52), .55),
    'Ilsvi': ((178, 92, 160), .55), 'Vlaakith': ((92, 162, 168), .55), 'Pantheon': ((184, 160, 118), .55),
    'Defiant': ((154, 154, 150), .55), 'Weave': ((104, 144, 214), .55), 'Undeath': ((132, 172, 92), .55),
    'Ordning': ((160, 128, 92), .55),
}


def symbol(I, key):
    if key == 'Lathander':
        I.layer(rrect_mask(18, 82, 110, 94, 5), GOLD_D)
        sun(I, 64, 82, 26, col=DAWN_L, ray=GOLD, half=True)
    elif key == 'Tyr':
        scales(I, 64, 66, 1.0, SILVER, SILVER_D)
        I.layer(rrect_mask(46, 22, 82, 32, 4), CREAM)                       # 蒙眼布
        I.layer(line_mask([(80, 28), (90, 42)], 5), CREAM)
        I.layer(line_mask([(80, 28), (94, 34)], 5), CREAM)
    elif key == 'Mystra':
        I.layer(arc_mask(64, 64, 38, 0, 360, 3), SKY, shade=False, outline=False, sil=False)
        for i in range(7):
            a = -math.pi / 2 + i * 2 * math.pi / 7
            I.layer(poly_mask(star_pts(64 + 38 * math.cos(a), 64 + 38 * math.sin(a), 12, 5, 5)), (226, 230, 246))
        I.layer(poly_mask(star_pts(64, 64, 18, 8, 7)), SKY)
    elif key == 'Kelemvor':
        skull(I, 64, 40, .85, BONE)
        scales(I, 64, 88, .55, SILVER, ASH)
    elif key == 'Malar':
        for dx in (-24, 0, 24):
            I.layer(slash(64 + dx + 16, 16, 64 + dx - 14, 112, 7), BONE)
    elif key == 'Talos':
        for dx, dy in ((-26, 6), (0, -6), (26, 6)):
            I.layer(bolt(64 + dx, 64 + dy, .62), (236, 224, 140))
    elif key == 'Bane':
        col = (44, 40, 50)
        I.layer(rrect_mask(46, 88, 86, 112, 6), (70, 64, 76))               # 护腕
        I.layer(rrect_mask(40, 56, 90, 94, 10), col)                        # 手背
        I.layer(rrect_mask(36, 34, 92, 66, 10), col)                        # 四指
        for x in (50, 64, 78): ink(I, [(x, 38), (x, 56)], 3)
        I.layer(line_mask([(42, 66), (66, 74)], 13), col)                   # 拇指
        I.layer(rrect_mask(42, 38, 58, 44, 3), (92, 86, 100), shade=False, outline=False, sil=False)
    elif key == 'Shar':
        I.layer(ellipse_mask(64, 64, 40), (24, 18, 32), shade=False)
        I.layer(arc_mask(64, 64, 40, 0, 360, 8), (156, 114, 196), shade=False)
    elif key == 'Surtr':
        I.layer(tongues(64, 10, 1.1), EMBER, glow=EMBER_L)
        I.layer(tongues(64, 42, .6), EMBER_L, shade=False, sil=False)
        I.layer(line_mask([(64, 64), (64, 112)], 9), RUST_D)
        I.layer(rrect_mask(38, 44, 90, 68, 4), (118, 114, 118))
    elif key == 'Ilsvi':
        for dx in (-20, -7, 7, 20):
            I.layer(line_mask([(64 + dx, 66), (64 + dx * 1.5, 86), (64 + dx * 1.1, 104), (64 + dx * 1.5, 112)], 8), (170, 112, 170))
        I.layer(ellipse_mask(64, 48, 36, 28), (210, 156, 200))
        ink(I, [(64, 24), (64, 72)], 3)
        for sx in (-1, 1):
            ink(I, [(64 + sx * 20, 30), (64 + sx * 12, 42), (64 + sx * 22, 52)], 3)
            ink(I, [(64 + sx * 34, 50), (64 + sx * 24, 62)], 3)
    elif key == 'Vlaakith':
        skull(I, 64, 70, 1.25, (214, 218, 204))
        I.layer(poly_mask([(34, 54), (38, 24), (51, 40), (64, 14), (77, 40), (90, 24), (94, 54)]), (120, 200, 200))
        for x, y in ((64, 40), (46, 46), (82, 46)):
            I.layer(ellipse_mask(x, y, 4), (60, 120, 140), shade=False, outline=False, sil='cut')
    elif key == 'Pantheon':
        I.layer(rrect_mask(14, 102, 114, 112, 3), CREAM_D)
        I.layer(rrect_mask(20, 92, 108, 104, 3), CREAM)
        for x in (32, 51, 77, 96): I.layer(rrect_mask(x - 6, 56, x + 6, 92, 2), CREAM)
        I.layer(rrect_mask(22, 46, 106, 56, 2), CREAM)
        I.layer(poly_mask([(18, 46), (64, 18), (110, 46)]), CREAM)
        I.layer(ellipse_mask(64, 37, 6), GOLD, sil='cut')
    elif key == 'Defiant':
        lc = (180, 182, 186)
        for x in (18, 34): I.layer(ring(x, 70, 10, 7, 4), lc, shade=False)
        for x in (94, 110): I.layer(ring(x, 70, 10, 7, 4), lc, shade=False)
        I.layer(subtract(ring(48, 70, 10, 7, 4), rrect_mask(50, 60, 60, 80, 0)), lc, shade=False)   # 断开的链环
        I.layer(subtract(ring(80, 70, 10, 7, 4), rrect_mask(68, 60, 78, 80, 0)), lc, shade=False)
        I.layer(rrect_mask(54, 70, 74, 114, 6), BONE_D)                    # 前臂
        I.layer(rrect_mask(42, 28, 86, 72, 10), BONE)                      # 拳
        for x in (53, 64, 75): ink(I, [(x, 32), (x, 48)], 3)
        I.layer(line_mask([(46, 54), (68, 60)], 11), BONE)
        for x, y in ((40, 50), (88, 50)): I.layer(poly_mask(star_pts(x, y, 7, 2.5, 4)), (240, 236, 200), shade=False, sil='merge')
    elif key == 'Weave':
        for k in range(3):
            I.layer(line_mask(oval_pts(64, 64, 46, 17, k * math.pi / 3), 5), (176, 204, 244), shade=False, glow=SKY if k == 0 else None, sil='merge')
        I.layer(poly_mask(star_pts(64, 64, 16, 6, 4)), (236, 242, 252))
    elif key == 'Undeath':
        I.layer(flame_mask(64, 28, 30, 18), (160, 210, 120), glow=(120, 180, 90))
        I.layer(rrect_mask(34, 50, 94, 108, 4), (146, 132, 96))
        I.layer(poly_mask([(30, 52), (98, 52), (90, 38), (38, 38)]), (120, 108, 78))
        skull(I, 64, 74, .8, BONE)
    elif key == 'Ordning':
        I.layer(bust_mask(64, 22, .8), (64, 56, 50))
        for cy in (72, 86, 100):
            I.layer(chevron(64, cy, 28, 14, 9), GOLD)


def meme(key):
    col, f = MEMES[key]
    I = SymIcon()
    I.layer(ellipse_mask(64, 64, 60), darker(col, f), shade=False, sil=False)
    I.layer(arc_mask(64, 64, 56, 0, 360, 7), col, shade=False, sil=False)
    symbol(I, key)
    return I


# ==================== 神像（2×2，256，南向；近白石 + 遮罩） ====================
STOW = 8  # 512 画布上的描边半径（成品约 4px）


class Statue(A.Icon):
    def __init__(self):
        super().__init__(); self.msk = Image.new('L', (S, S), 0)

    def layer(self, m, fill, shade=True, outline=True, glow=None, stuff=True):
        if glow:
            g = dilate(m, STOW * 2).filter(ImageFilter.GaussianBlur(STOW))
            self.im.paste(Image.new('RGBA', (S, S), glow + (255,)), (0, 0), g.point(lambda v: int(v * .6)))
        if outline:
            o = dilate(m, STOW)
            self.im.paste(Image.new('RGBA', (S, S), INK + (255,)), (0, 0), o)
            self.msk.paste(0, (0, 0), o)
        self.im.paste(Image.new('RGBA', (S, S), fill + (255,)), (0, 0), m)
        self.msk.paste(255 if stuff else 0, (0, 0), m)
        if shade:
            d = int(3.5 * SS)
            band = subtract(m, ImageChops.offset(m, -d, -d))
            self.im.paste(Image.new('RGBA', (S, S), darker(fill, .8) + (255,)), (0, 0), band)
        return self

    def fold(self, pts, w=2, col=GREY_F):
        self.layer(line_mask(pts, w), col, shade=False, outline=False)

    def dark(self, m):
        self.layer(m, DARK, shade=False, outline=False, stuff=False)

    def images(self):
        col = self.im.resize((256, 256), Image.LANCZOS)
        r = self.msk.resize((256, 256), Image.NEAREST)
        z = Image.new('L', (256, 256), 0)
        return col, Image.merge('RGBA', (r, z, z, Image.new('L', (256, 256), 255)))


def plinth(I):
    I.layer(rrect_mask(12, 104, 116, 124, 4), GREY_D)
    I.layer(rrect_mask(20, 96, 108, 108, 3), GREY)


def robe(I, cx, top, bot, wt, wb, col=GREY):
    I.layer(union(poly_mask([(cx - wt, top + 4), (cx + wt, top + 4), (cx + wb, bot), (cx - wb, bot)]),
                  rrect_mask(cx - wt - 4, top, cx + wt + 4, top + 14, 7)), col)


def statue(key):
    I = Statue(); plinth(I)
    if key == 'Lathander':
        for s in (-1, 1): I.layer(line_mask([(64 + s * 12, 56), (64 + s * 24, 40), (64 + s * 13, 24)], 7), GREY)
        robe(I, 64, 50, 100, 14, 28)
        I.fold([(58, 64), (54, 98)]); I.fold([(70, 64), (74, 98)])
        I.layer(ellipse_mask(64, 42, 8.5), GREY)
        rays(I, 64, 20, 14, 20, 12, 3.5, GREY)
        I.layer(ellipse_mask(64, 20, 11), GREY)
        I.fold([(64 + 6 * math.cos(t / 8 * 2 * math.pi), 20 + 6 * math.sin(t / 8 * 2 * math.pi)) for t in range(9)], 1.5)
    elif key == 'Tyr':
        robe(I, 64, 46, 100, 14, 26)
        I.fold([(60, 62), (56, 98)]); I.fold([(70, 62), (74, 98)])
        I.layer(line_mask([(50, 52), (46, 68), (60, 74)], 7), GREY)
        I.layer(line_mask([(78, 52), (90, 42), (96, 24)], 7), GREY)
        I.layer(poly_mask([(56, 40), (72, 40), (64, 56)]), GREY_D)          # 胡须
        I.layer(ellipse_mask(64, 36, 9), GREY)
        I.dark(rrect_mask(54, 32, 74, 38, 2))                                # 蒙眼布
        I.dark(line_mask([(72, 35), (80, 44)], 3))
        scales(I, 96, 38, .42, GREY, GREY_D)
    elif key == 'Mystra':
        I.layer(poly_mask([(54, 30), (74, 30), (80, 64), (48, 64)]), GREY_D)   # 长发
        for s in (-1, 1): I.layer(line_mask([(64 + s * 12, 52), (64 + s * 24, 66), (64 + s * 32, 72)], 6), GREY)
        robe(I, 64, 46, 100, 11, 30)
        I.fold([(60, 60), (50, 98)]); I.fold([(68, 60), (78, 98)])
        I.layer(arc_mask(64, 32, 22, 160, 380, 2.5), GREY_D, shade=False, outline=False)
        I.layer(ellipse_mask(64, 36, 8), GREY)
        for i in range(7):
            a = math.radians(160 + i * 220 / 6)
            I.layer(poly_mask(star_pts(64 + 22 * math.cos(a), 32 + 22 * math.sin(a), 6.5, 2.8, 5)), GREY, shade=False)
    elif key == 'Kelemvor':
        I.layer(poly_mask([(64, 14), (80, 26), (86, 60), (92, 100), (36, 100), (42, 60), (48, 26)]), GREY)
        I.fold([(56, 74), (50, 98)]); I.fold([(72, 74), (78, 98)])
        I.dark(ellipse_mask(64, 31, 7, 9))
        I.layer(union(line_mask([(46, 56), (56, 68)], 6), line_mask([(82, 56), (72, 68)], 6)), GREY)
        scales(I, 64, 76, .55, GREY, GREY_D)
        skull(I, 64, 52, .42, GREY)
    elif key == 'Malar':
        for s in (-1, 1):
            I.layer(line_mask([(64 + s * 22, 58), (64 + s * 38, 46), (64 + s * 42, 30)], 8), GREY)
            for dx, dy in ((-8, -12), (0, -14), (8, -10)):
                I.layer(line_mask([(64 + s * 42, 30), (64 + s * (42 + dx), 30 + dy)], 3), GREY_D, shade=False)
        I.layer(union(poly_mask([(40, 54), (88, 54), (82, 100), (46, 100)]), ellipse_mask(64, 56, 30, 12)), GREY)
        I.fold([(56, 70), (60, 98)]); I.fold([(72, 70), (68, 98)])
        for s in (-1, 1): I.layer(poly_mask([(64 + s * 8, 32), (64 + s * 14, 14), (64 + s * 16, 34)]), GREY)
        I.layer(ellipse_mask(64, 38, 12, 11), GREY)
        I.layer(poly_mask([(56, 42), (72, 42), (68, 56), (60, 56)]), GREY_D)
        for s in (-1, 1): I.dark(ellipse_mask(64 + s * 5, 36, 2))
    elif key == 'Talos':
        for s in (-1, 1): I.layer(line_mask([(64 + s * 14, 54), (64 + s * 30, 38), (64 + s * 36, 26)], 7), GREY)
        robe(I, 64, 48, 100, 16, 24)
        I.layer(rrect_mask(46, 70, 82, 76, 2), GREY_D, shade=False)
        I.layer(poly_mask([(56, 44), (72, 44), (64, 58)]), GREY_D)
        I.layer(ellipse_mask(64, 38, 9), GREY)
        I.dark(line_mask([(55, 32), (73, 42)], 2.5)); I.dark(ellipse_mask(60, 37, 3.5))
        for x in (28, 100): I.layer(bolt(x, 20, .27), GREY)
    elif key == 'Bane':
        I.layer(line_mask([(42, 58), (36, 82)], 8), GREY)
        I.layer(line_mask([(86, 58), (96, 46)], 8), GREY)
        I.layer(poly_mask([(46, 50), (82, 50), (88, 100), (40, 100)]), GREY)
        I.layer(rrect_mask(44, 74, 84, 80, 2), GREY_D)
        I.fold([(64, 82), (64, 100)])
        for x in (44, 84): I.layer(ellipse_mask(x, 54, 11, 9), GREY)
        I.layer(poly_mask([(54, 30), (56, 16), (60, 28), (64, 12), (68, 28), (72, 16), (74, 30)]), GREY_D)
        I.layer(rrect_mask(54, 26, 74, 48, 6), GREY)
        I.dark(line_mask([(57, 37), (71, 37)], 2.5))
        I.layer(hand(102, 36, .62), (52, 46, 58), stuff=False)                # 黑手套
    elif key == 'Shar':
        I.layer(poly_mask([(64, 14), (80, 24), (86, 56), (94, 100), (34, 100), (42, 56), (48, 24)]), GREY)
        I.fold([(52, 30), (46, 96)]); I.fold([(76, 30), (82, 96)]); I.fold([(64, 82), (64, 100)])
        I.dark(ellipse_mask(64, 33, 7, 9))
        I.layer(poly_mask([(56, 34), (72, 34), (70, 44), (58, 44)]), GREY_D, shade=False, outline=False)   # 面纱下摆
        for x in (49, 79): I.layer(ellipse_mask(x, 64, 4.5), GREY)
        I.layer(ellipse_mask(64, 64, 15), (34, 28, 42), shade=False, stuff=False)
        I.layer(arc_mask(64, 64, 15, 0, 360, 3), GREY_D, shade=False, outline=False)
    elif key == 'Surtr':
        for x in (36, 56): I.layer(rrect_mask(x, 74, x + 14, 100, 4), GREY)
        I.layer(line_mask([(30, 42), (24, 64), (36, 80)], 9), GREY)
        I.layer(union(poly_mask([(30, 36), (74, 36), (70, 78), (34, 78)]), ellipse_mask(52, 40, 26, 12)), GREY)
        I.layer(rrect_mask(34, 70, 70, 76, 2), GREY_D, shade=False)
        I.layer(line_mask([(72, 42), (88, 34), (98, 28)], 9), GREY)
        I.layer(line_mask([(98, 28), (102, 12)], 4), GREY_D)
        I.layer(rrect_mask(92, 4, 114, 16, 3), GREY)
        I.layer(flame_mask(52, 15, 24, 12), EMBER, stuff=False)
        I.layer(ellipse_mask(52, 24, 11), GREY)
        I.layer(poly_mask([(44, 28), (60, 28), (52, 44)]), GREY_D)
        I.layer(poly_mask([(80, 74), (118, 74), (112, 82), (106, 82), (106, 90), (112, 98), (84, 98), (92, 90), (92, 82), (86, 82)]), GREY_D)
        I.layer(rrect_mask(92, 68, 108, 74, 2), EMBER_L, shade=False, stuff=False, glow=EMBER)
    elif key == 'Pantheon':
        I.layer(rrect_mask(40, 86, 88, 100, 3), GREY_D)
        I.layer(rrect_mask(44, 22, 84, 90, 3), GREY)
        for x in (48, 80): I.fold([(x, 26), (x, 86)], 1.5)
        I.layer(rrect_mask(38, 14, 90, 26, 3), GREY_D)
        I.layer(rrect_mask(34, 6, 94, 16, 2), GREY)

        def face(cx, cy, r):
            I.layer(ellipse_mask(cx, cy, r, r * 1.1), GREY, shade=False, outline=False)
            I.layer(arc_mask(cx, cy, r, 0, 360, 1.5), GREY_F, shade=False, outline=False)
            for s in (-1, 1): I.fold([(cx + s * r * .45 - 2, cy - r * .2), (cx + s * r * .45 + 2, cy - r * .2)], 2, DARK if False else (110, 106, 100))
            I.fold([(cx, cy - r * .1), (cx, cy + r * .25)], 1.5, (130, 126, 120))
            I.fold([(cx - r * .35, cy + r * .5), (cx + r * .35, cy + r * .5)], 2, (110, 106, 100))
        for cx in (54, 74): face(cx, 36, 8.5)
        face(64, 58, 10)
        for cx in (54, 74): face(cx, 80, 8.5)
    return I


# ==================== 能力 / 议题 / 职位 图标（128） ====================
def ability(name):
    I = A.Icon()
    if name == 'DivinePreach':
        rays(I, 84, 66, 22, 46, 7, 5, GOLD, 215, 335)
        I.layer(bust_mask(34, 48, 1.15), SLATE)
        I.layer(poly_mask([(62, 64), (82, 70), (82, 96), (62, 88)]), CREAM)
        I.layer(poly_mask([(82, 70), (104, 62), (104, 88), (82, 96)]), CREAM_D)
        for y in (74, 80): I.layer(line_mask([(66, y), (78, y + 4)], 2), KHAKI_D, outline=False, shade=False)
    elif name == 'MassPrayer':
        rays(I, 64, 30, 26, 42, 7, 4, (236, 214, 150), 200, 340)
        I.layer(rrect_mask(38, 34, 90, 54, 4), STONE)
        I.layer(poly_mask(star_pts(64, 24, 14, 5, 4)), (246, 236, 180), glow=GOLD)
        for x, y, c in ((28, 78, SLATE), (100, 78, RUST), (64, 82, KHAKI)):
            I.layer(bust_mask(x, y, .72), c)
    elif name == 'Oracle':
        I.layer(tongues(64, 6, 1.15), VIOLET, glow=(110, 80, 150))
        I.layer(rrect_mask(28, 94, 100, 116, 4), STONE)
        I.layer(poly_mask(lens_pts(64, 56, 54, 28)), BONE)
        I.layer(ellipse_mask(64, 56, 10), SKY, shade=False)
        I.layer(ellipse_mask(64, 56, 4.5), INK, outline=False, shade=False)
    return I.done(128)


def issue(name):
    I = A.Icon()
    if name == 'Undead':
        skull(I, 64, 56, 2.0, BONE)
    elif name == 'Giants':
        c = (168, 140, 104)
        I.layer(union(ellipse_mask(66, 70, 26, 26), ellipse_mask(60, 100, 20, 16), rrect_mask(46, 70, 82, 100, 10)), c)
        for x, y, r in ((40, 34, 9), (56, 26, 8.5), (72, 24, 8), (86, 30, 7), (96, 42, 6)):
            I.layer(ellipse_mask(x, y, r), c)
    elif name == 'Arcane':
        I.layer(arc_mask(64, 64, 50, 0, 360, 5), SKY, shade=False)
        for k in range(3):
            a = -math.pi / 2 + k * 2 * math.pi / 3; b = a + 2 * math.pi / 3
            I.layer(line_mask([(64 + 46 * math.cos(a), 64 + 46 * math.sin(a)), (64 + 46 * math.cos(b), 64 + 46 * math.sin(b))], 3), SLATE, shade=False, outline=False)
        I.layer(poly_mask(star_pts(64, 64, 36, 10, 4)), (232, 238, 250), glow=SKY)
    elif name == 'Races':
        def person(cx, cy, sc, body, skin, extra=None):
            I.layer(rrect_mask(cx - 17 * sc, cy + 12 * sc, cx + 17 * sc, cy + 48 * sc, 12 * sc), body)
            if extra == 'elf':
                for s in (-1, 1): I.layer(poly_mask([(cx + s * 8 * sc, cy - 2 * sc), (cx + s * 20 * sc, cy - 14 * sc), (cx + s * 10 * sc, cy + 6 * sc)]), skin)
            I.layer(ellipse_mask(cx, cy, 11 * sc), skin)
            if extra == 'dwarf':
                I.layer(poly_mask([(cx - 11 * sc, cy + 2 * sc), (cx + 11 * sc, cy + 2 * sc), (cx, cy + 26 * sc)]), BONE_D)
        person(28, 52, .95, GREEN, (214, 196, 164), 'elf')
        person(100, 58, .95, RUST, (200, 170, 140), 'dwarf')
        person(64, 46, 1.1, SLATE, (222, 200, 168))
    elif name == 'Apotheosis':
        for x, y0 in ((46, 100), (64, 104), (82, 100)): I.layer(line_mask([(x, y0), (x, y0 + 16)], 4), GOLD, shade=False)
        for s in (-1, 1): I.layer(line_mask([(64 + s * 12, 66), (64 + s * 28, 48), (64 + s * 32, 32)], 7), CREAM)
        I.layer(bust_mask(64, 50, 1.0), CREAM)
        I.layer(ring(64, 28, 20, 7, 4), GOLD, glow=(240, 220, 150))
    elif name == 'Offerings':
        I.layer(line_mask([(56, 56), (50, 42), (58, 28), (52, 12)], 5), (206, 204, 198), shade=False)
        I.layer(line_mask([(74, 56), (80, 42), (72, 28), (78, 14)], 5), (206, 204, 198), shade=False)
        I.layer(ellipse_mask(64, 74, 40, 9), RUST_D, shade=False)
        for x, y, r, c in ((50, 70, 10, BLOOD), (78, 70, 9, GREEN), (64, 66, 11, GOLD)):
            I.layer(ellipse_mask(x, y, r), c)
        I.layer(rrect_mask(52, 100, 76, 112, 4), RUST_D)
        I.layer(subtract(ellipse_mask(64, 74, 40, 30), rrect_mask(20, 40, 108, 76, 0)), (176, 128, 80))
    return I.done(128)


def role_cleric():
    I = A.Icon()
    I.layer(arc_mask(64, 50, 36, 196, 344, 4), (150, 140, 120), shade=False)
    I.layer(ring(64, 44, 7, 7, 3), (220, 218, 210), shade=False)
    I.layer(ellipse_mask(64, 80, 32), (238, 236, 228))
    I.layer(poly_mask(star_pts(64, 80, 25, 13, 12)), (214, 210, 198), shade=False)
    I.layer(ellipse_mask(64, 80, 9), (248, 246, 240), shade=False)
    return I.done(128)


NAMES = list(MEMES)
STATUES = ['Lathander', 'Tyr', 'Mystra', 'Kelemvor', 'Malar', 'Talos', 'Bane', 'Shar', 'Surtr', 'Pantheon']
ABILITIES = ['DivinePreach', 'MassPrayer', 'Oracle']
ISSUES = ['Undead', 'Giants', 'Arcane', 'Races', 'Apotheosis', 'Offerings']
TINT = (190, 180, 165)


def tinted(col, msk):
    c = col.convert('RGBA'); r = msk.split()[0]
    t = ImageChops.multiply(c, Image.new('RGBA', c.size, TINT + (255,)))
    out = c.copy(); out.paste(t, (0, 0), r)
    out.putalpha(c.split()[3])
    return out


def preview(memes, statues):
    try:
        font = ImageFont.truetype('arial.ttf', 14)
    except Exception:
        font = ImageFont.load_default()
    W = 1400; mc, sc_ = 150, 276
    H = 20 + 2 * (mc + 20) + 20 + 2 * (sc_ + 20) + 10
    sheet = Image.new('RGBA', (W, H), (40, 38, 36, 255)); d = ImageDraw.Draw(sheet)
    for i, (n, im) in enumerate(memes):
        x = 20 + (i % 8) * (mc + 20); y = 20 + (i // 8) * (mc + 20)
        sheet.paste(im, (x + (mc - 128) // 2, y), im)
        d.text((x + mc // 2, y + 134), n, fill=(230, 226, 214), font=font, anchor='mt')
    y0 = 20 + 2 * (mc + 20) + 20
    for i, (n, im) in enumerate(statues):
        x = 20 + (i % 5) * sc_ ; y = y0 + (i // 5) * (sc_ + 20)
        sheet.paste(im, (x + (sc_ - 256) // 2, y), im)
        d.text((x + sc_ // 2, y + 258), n, fill=(230, 226, 214), font=font, anchor='mt')
    os.makedirs(os.path.dirname(PREVIEW), exist_ok=True)
    sheet.save(PREVIEW)
    return PREVIEW


if __name__ == '__main__':
    written, pm, ps = [], [], []
    for n in NAMES:
        I = meme(n)
        im = I.done(128); written.append(save(im, 'UI', 'Memes', 'LP', n + '.png')); pm.append((n, im))
        written.append(save(I.silhouette(256), 'UI', 'IdeoIcons', 'LP', n + '.png'))
    for n in STATUES:
        col, msk = statue(n).images()
        written.append(save(col, 'Things', 'Building', 'LP', 'Statues', n + '.png'))
        written.append(save(msk, 'Things', 'Building', 'LP', 'Statues', n + '_m.png'))
        ps.append((n, tinted(col, msk)))
    for n in ABILITIES: written.append(save(ability(n), 'UI', 'Abilities', 'LP', n + '.png'))
    for n in ISSUES: written.append(save(issue(n), 'UI', 'Issues', 'LP', n + '.png'))
    written.append(save(role_cleric(), 'UI', 'Roles', 'LP', 'Cleric.png'))
    print(len(written), 'textures written to', OUT)
    print('preview', preview(pm, ps))
    bad = []
    for p in written:
        im = Image.open(p)
        if im.mode != 'RGBA' or im.getchannel('A').getbbox() is None: bad.append(p)
        if p.endswith('_m.png') and im.getchannel('R').getbbox() is None: bad.append(p)
    print('bad:', bad)
