# 1.47.0 DND工作台贴图：5 张工作台（Graphic_Multi，南/北/东，西由东翻转）+ 4 个附件，128 px/格。
# 画法沿用 draw_apparel_145.py / draw_faith.py 的 RimWorld 风格：粗黑轮廓、平涂 + 一层暗面（前脸/右下）、低饱和。
# 用材料建造的部分画成浅灰并在遮罩里涂红（CutoutComplex 按材料色相乘）；其余固定色，遮罩为黑。
# 遮罩命名按 1.7 的 Graphic_Multi / Graphic_Single：<路径>_southm.png …；单图 <路径>_m.png。
# 每件东西都在“长轴 u（0 左→1 右）× 纵深 v（0 后→1 前）”的平面坐标里画，南向直接映射，北向前后翻转，东向转成竖图。
# 用法：python -I ArtSource/RaceArt/draw_workbenches_147.py <模组根目录>
import math, os, sys
from PIL import Image, ImageDraw, ImageFilter

ROOT = sys.argv[1] if len(sys.argv) > 1 else os.getcwd()
OUT = os.path.join(ROOT, "Textures", "LP", "Workbenches")
PREVIEW = os.path.join(ROOT, "ArtSource", "Preview", "Workbenches-1.47.0.png")
CELL = 128
SS = 3                      # 超采样
INK = (34, 30, 30)
STUFF, FIXED = (255, 0, 0), (0, 0, 0)
GREY, GREY_D = (226, 224, 220), (178, 176, 172)       # 材料色区
WOOD, WOOD_D, WOOD_L = (128, 92, 62), (96, 68, 46), (156, 118, 82)
DARKWOOD, DARKWOOD_D = (84, 62, 50), (62, 46, 38)
IRON, IRON_D, IRON_L = (78, 78, 84), (54, 54, 60), (110, 110, 118)
COPPER, COPPER_D = (176, 112, 70), (130, 80, 52)
GOLD, GOLD_D = (208, 172, 92), (160, 128, 64)
PARCH, PARCH_D = (222, 206, 168), (186, 168, 128)
VIOLET, VIOLET_L, VIOLET_D = (140, 100, 186), (200, 170, 236), (98, 68, 136)
EMBER, EMBER_L = (226, 118, 52), (250, 196, 110)
RUNE = (130, 200, 230)
WATER, WATER_L = (90, 150, 196), (160, 210, 236)
NAVY, NAVY_D = (52, 62, 104), (36, 42, 74)
SILVERT = (210, 222, 236)
RED = (150, 52, 44)
GLASS, GLASS_D = (176, 210, 220), (128, 166, 180)
STONE, STONE_D = (150, 146, 138), (116, 112, 106)


def darker(c, f=.78):
    return tuple(int(v * f) for v in c[:3])


class Sheet:
    """一张图：彩色 + 遮罩；形状用 (u, v) 坐标，按朝向映射到像素。"""

    def __init__(self, cw, ch, view):
        self.view = view
        self.cw, self.ch = cw, ch                      # 南向的格数（宽 × 深）
        self.W, self.H = (cw * CELL, ch * CELL) if view != "east" else (ch * CELL, cw * CELL)
        self.col = Image.new("RGBA", (self.W * SS, self.H * SS), (0, 0, 0, 0))
        self.msk = Image.new("RGB", (self.W * SS, self.H * SS), (0, 0, 0))

    def P(self, u, v):
        if self.view == "south":
            x, y = u * self.W, v * self.H
        elif self.view == "north":
            x, y = u * self.W, (1 - v) * self.H
        else:  # east：长轴变成竖向，前（v=1）朝右
            x, y = v * self.W, (1 - u) * self.H
        return x * SS, y * SS

    def box(self, u0, v0, u1, v1):
        (xa, ya), (xb, yb) = self.P(u0, v0), self.P(u1, v1)
        return [min(xa, xb), min(ya, yb), max(xa, xb), max(ya, yb)]

    def _mask(self, kind, a, w=0):
        m = Image.new("L", self.col.size, 0)
        d = ImageDraw.Draw(m)
        if kind == "rect":
            d.rounded_rectangle(self.box(*a[:4]), radius=(a[4] if len(a) > 4 else 4) * SS, fill=255)
        elif kind == "ell":
            d.ellipse(self.box(*a), fill=255)
        elif kind == "poly":
            d.polygon([self.P(u, v) for u, v in a], fill=255)
        elif kind == "line":
            pts = [self.P(u, v) for u, v in a]
            d.line(pts, fill=255, width=max(1, int(w * SS)), joint="curve")
            for x, y in (pts[0], pts[-1]):
                d.ellipse([x - w * SS / 2, y - w * SS / 2, x + w * SS / 2, y + w * SS / 2], fill=255)
        return m

    def add(self, kind, a, color, chan=FIXED, ow=4.0, w=0):
        m = self._mask(kind, a, w)
        if ow:
            k = max(3, int(ow * SS) * 2 + 1)
            o = m.filter(ImageFilter.MaxFilter(k))
            self.col.paste(Image.new("RGBA", self.col.size, INK + (255,)), (0, 0), o)
            self.msk.paste(Image.new("RGB", self.col.size, FIXED), (0, 0), o)
        self.col.paste(Image.new("RGBA", self.col.size, tuple(color) + (255,)), (0, 0), m)
        self.msk.paste(Image.new("RGB", self.col.size, chan), (0, 0), m)

    # 带前脸暗面的块：顶面 + 前（南向下方 / 东向右方）一条暗面。
    def block(self, u0, v0, u1, v1, color, chan=FIXED, face=.22, r=6, ow=4.0):
        self.add("rect", (u0, v0, u1, v1, r), color, chan, ow)
        fv = v1 - (v1 - v0) * face
        if self.view == "north":
            fv = v0 + (v1 - v0) * face
            self.add("rect", (u0, v0, u1, fv, r), darker(color), chan, 0)
        else:
            self.add("rect", (u0, fv, u1, v1, r), darker(color), chan, 0)

    def glow(self, u, v, rad, color, alpha=90):
        x, y = self.P(u, v)
        g = Image.new("RGBA", self.col.size, (0, 0, 0, 0))
        ImageDraw.Draw(g).ellipse([x - rad * SS, y - rad * SS, x + rad * SS, y + rad * SS], fill=tuple(color) + (alpha,))
        g = g.filter(ImageFilter.GaussianBlur(rad * SS * .35))
        self.col.alpha_composite(g)

    def images(self):
        return (self.col.resize((self.W, self.H), Image.LANCZOS), self.msk.resize((self.W, self.H), Image.NEAREST))


# ==================== 工作台 ====================
def enchanting_table(s):
    s.block(.03, .12, .97, .94, GREY, STUFF, .2)                       # 台面（材料色）
    s.add("rect", (.07, .16, .93, .70, 5), DARKWOOD, FIXED, 3)          # 深色木面板
    s.add("ell", (.33, .18, .67, .70), COPPER, FIXED, 3.5)              # 铜框符文圆盘
    s.add("ell", (.37, .24, .63, .64), VIOLET_D, FIXED, 0)
    s.add("ell", (.41, .30, .59, .58), VIOLET, FIXED, 0)
    for i in range(8):
        a = math.radians(i * 45)
        s.add("line", [(.5 + .075 * math.cos(a), .44 + .11 * math.sin(a)), (.5 + .11 * math.cos(a), .44 + .17 * math.sin(a))], VIOLET_L, FIXED, 0, 3)
    s.add("ell", (.465, .39, .535, .49), VIOLET_L, FIXED, 0)
    # 戒指托架（左）
    s.add("rect", (.10, .24, .25, .52, 4), WOOD, FIXED, 3)
    for k, c in enumerate((GOLD, (196, 200, 206), GOLD)):
        s.add("ell", (.12 + k * .043, .30, .155 + k * .043, .40), c, FIXED, 2)
    # 宝石小盒（右后）
    s.add("rect", (.74, .20, .90, .44, 4), WOOD_L, FIXED, 3)
    for k, c in enumerate(((176, 58, 58), (70, 104, 168), (78, 140, 96))):
        s.add("poly", [(.765 + k * .045, .32), (.78 + k * .045, .26), (.795 + k * .045, .32), (.78 + k * .045, .38)], c, FIXED, 1.5)
    # 摊开的法术书（右前）
    s.add("poly", [(.70, .52), (.80, .48), (.90, .52), (.90, .72), (.80, .68), (.70, .72)], PARCH, FIXED, 3)
    s.add("line", [(.80, .49), (.80, .68)], PARCH_D, FIXED, 0, 2)
    for k in range(3):
        s.add("line", [(.72, .56 + k * .045), (.78, .545 + k * .045)], (120, 96, 90), FIXED, 0, 1.6)
        s.add("line", [(.82, .545 + k * .045), (.88, .56 + k * .045)], (120, 96, 90), FIXED, 0, 1.6)
    s.glow(.5, .44, 46, VIOLET_L, 70)


def scribe_desk(s):
    s.block(.04, .10, .96, .92, WOOD, FIXED, .24)
    s.add("poly", [(.10, .16), (.66, .16), (.70, .66), (.08, .66)], WOOD_L, FIXED, 3)   # 斜面
    s.add("rect", (.16, .22, .58, .60, 3), PARCH, FIXED, 2.5)                         # 羊皮纸
    for k in range(5):
        s.add("line", [(.20, .29 + k * .06), (.53 - (k % 2) * .08, .29 + k * .06)], (130, 110, 96), FIXED, 0, 1.8)
    s.add("ell", (.70, .20, .80, .44), INK, FIXED, 2)                                 # 墨水瓶
    s.add("ell", (.72, .24, .78, .34), (70, 76, 110), FIXED, 0)
    s.add("line", [(.76, .30), (.66, .06)], (230, 226, 214), FIXED, 2.5, 4)           # 羽毛笔
    s.add("rect", (.80, .50, .94, .70, 8), PARCH_D, FIXED, 2.5)                       # 卷起的羊皮纸
    s.add("rect", (.82, .54, .92, .66, 6), PARCH, FIXED, 0)
    s.add("ell", (.60, .60, .70, .78), RED, FIXED, 2.5)                               # 蜡封印章
    s.add("ell", (.82, .14, .94, .40), COPPER, FIXED, 3)                              # 小油灯
    s.add("ell", (.855, .18, .905, .30), EMBER_L, FIXED, 0)
    s.glow(.88, .24, 26, EMBER_L, 90)


def rune_anvil(s):
    s.block(.03, .08, .52, .94, GREY, STUFF, .24)                       # 石砌炉膛
    for u in (.12, .27, .42):
        s.add("line", [(u, .14), (u, .70)], GREY_D, STUFF, 0, 2)
    s.add("line", [(.05, .40), (.50, .40)], GREY_D, STUFF, 0, 2)
    s.add("rect", (.14, .48, .41, .74, 10), INK, FIXED, 3)               # 炉口
    s.add("rect", (.17, .52, .38, .72, 8), EMBER, FIXED, 0)
    s.add("rect", (.21, .58, .34, .72, 6), EMBER_L, FIXED, 0)
    s.glow(.27, .64, 40, EMBER_L, 110)
    # 黑铁砧
    s.add("rect", (.64, .50, .82, .90, 4), IRON_D, FIXED, 3.5)           # 砧座
    s.add("poly", [(.56, .30), (.92, .30), (.97, .38), (.92, .48), (.58, .48), (.53, .39)], IRON, FIXED, 4)
    s.add("poly", [(.58, .42), (.92, .42), (.92, .48), (.58, .48)], IRON_D, FIXED, 0)
    for k in range(4):
        u = .62 + k * .075
        s.add("poly", [(u, .34), (u + .02, .32), (u + .04, .34), (u + .02, .38)], RUNE, FIXED, 0)
    # 锤钳挂在一旁
    s.add("line", [(.60, .10), (.66, .26)], WOOD, FIXED, 2.5, 6)
    s.add("rect", (.56, .06, .66, .14, 3), IRON_L, FIXED, 3)
    s.add("line", [(.78, .08), (.86, .26)], IRON_L, FIXED, 2.5, 4)
    s.add("line", [(.84, .08), (.80, .26)], IRON_L, FIXED, 2.5, 4)


def giant_anvil(s):
    # 几乎一整块山岩：不规则多边形 + 前脸
    rock = [(.04, .30), (.12, .12), (.40, .06), (.70, .08), (.92, .18), (.97, .50), (.94, .90), (.06, .92), (.02, .62)]
    s.add("poly", rock, GREY, STUFF, 5)
    s.add("poly", [(.03, .70), (.97, .70), (.94, .90), (.06, .92)], GREY_D, STUFF, 0)
    for a, b in (((.20, .20), (.30, .40)), ((.62, .16), (.70, .34)), ((.82, .40), (.88, .58)), ((.12, .50), (.22, .62))):
        s.add("line", [a, b], GREY_D, STUFF, 0, 2.5)
    # 砧面与砧角（冷色铁）
    s.add("poly", [(.18, .32), (.78, .32), (.92, .42), (.78, .54), (.18, .54)], IRON, FIXED, 4.5)
    s.add("poly", [(.18, .48), (.78, .48), (.78, .54), (.18, .54)], IRON_D, FIXED, 0)
    s.add("line", [(.22, .36), (.74, .36)], (150, 166, 186), FIXED, 0, 2.5)
    # 巨大铁钳
    s.add("rect", (.08, .40, .16, .66, 3), IRON_D, FIXED, 3.5)
    s.add("rect", (.82, .58, .90, .82, 3), IRON_D, FIXED, 3.5)
    # 链条
    for k in range(6):
        u = .14 + k * .055
        s.add("ell", (u, .70, u + .06, .80), (126, 136, 150), FIXED, 2.5)
    s.add("line", [(.48, .62), (.70, .80)], WOOD_D, FIXED, 3, 9)             # 巨锤柄
    s.add("rect", (.40, .56, .56, .68, 4), IRON_L, FIXED, 3.5)


def arcane_loom(s):
    s.add("rect", (.03, .20, .97, .90, 4), WOOD_D, FIXED, 4)                # 底座
    s.add("rect", (.03, .76, .97, .90, 4), darker(WOOD_D), FIXED, 0)
    for u in (.06, .90):
        s.add("rect", (u, .06, u + .05, .86, 3), WOOD, FIXED, 3.5)            # 立柱
    s.add("rect", (.06, .08, .95, .20, 3), WOOD_L, FIXED, 3.5)              # 上梁
    for k in range(22):
        u = .14 + k * .034
        s.add("line", [(u, .20), (u, .60)], SILVERT if k % 3 == 0 else (150, 140, 120), FIXED, 0, 1.6)
    s.add("rect", (.12, .40, .86, .64, 3), NAVY, FIXED, 3)                  # 深蓝星纹布
    for (u, v) in ((.2, .46), (.33, .56), (.47, .47), (.60, .58), (.74, .48), (.81, .58)):
        s.add("poly", [(u, v - .04), (u + .012, v), (u, v + .04), (u - .012, v)], SILVERT, FIXED, 0)
    s.add("rect", (.12, .64, .86, .74, 6), NAVY_D, FIXED, 3)                 # 卷布轴
    s.glow(.5, .36, 40, (150, 190, 255), 70)


# ==================== 附件 ====================
def alembic(s):
    s.add("rect", (.22, .52, .78, .92, 6), IRON_D, FIXED, 4)                # 小火炉
    s.add("rect", (.34, .66, .66, .86, 6), EMBER, FIXED, 3)
    s.add("rect", (.42, .72, .58, .86, 4), EMBER_L, FIXED, 0)
    s.add("ell", (.24, .22, .66, .64), GLASS, FIXED, 4)                     # 曲颈瓶
    s.add("ell", (.30, .40, .60, .62), (140, 186, 120), FIXED, 0)
    s.add("ell", (.34, .28, .46, .38), (236, 246, 250), FIXED, 0)
    s.add("line", [(.48, .24), (.62, .10), (.84, .22), (.86, .40)], GLASS_D, FIXED, 3, 6)
    s.glow(.5, .76, 22, EMBER_L, 110)


def focus_stone(s):
    s.add("rect", (.20, .66, .80, .92, 5), STONE, FIXED, 4)                 # 石座
    s.add("rect", (.20, .82, .80, .92, 5), STONE_D, FIXED, 0)
    for (u0, u1, top, c) in ((.26, .44, .30, VIOLET_D), (.56, .74, .26, VIOLET_D), (.38, .62, .08, VIOLET)):
        mid = (u0 + u1) / 2
        s.add("poly", [(u0, .70), (u0 + .02, top + .12), (mid, top), (u1 - .02, top + .12), (u1, .70)], c, FIXED, 3.5)
        s.add("poly", [(mid, top), (u1 - .02, top + .12), (u1, .70), (mid, .70)], darker(c, .8), FIXED, 0)
    s.add("line", [(.46, .20), (.46, .54)], VIOLET_L, FIXED, 0, 3)
    s.glow(.5, .40, 34, VIOLET_L, 90)


def quench_trough(s):
    s.block(.04, .12, .96, .92, GREY, STUFF, .26)
    s.add("rect", (.12, .22, .88, .62, 6), WATER, FIXED, 3)
    s.add("rect", (.12, .22, .88, .34, 6), WATER_L, FIXED, 0)
    for k in range(5):
        u = .16 + k * .17
        s.add("poly", [(u, .74), (u + .03, .70), (u + .06, .74), (u + .03, .80)], RUNE, FIXED, 0)
    s.glow(.5, .42, 44, WATER_L, 80)


def deity_statue(s):
    s.add("rect", (.18, .76, .82, .94, 4), GREY_D, STUFF, 4)                 # 底座
    s.add("poly", [(.30, .80), (.24, .40), (.36, .18), (.50, .10), (.64, .18), (.76, .40), (.70, .80)], GREY, STUFF, 4.5)   # 兜帽长袍
    s.add("poly", [(.50, .10), (.64, .18), (.76, .40), (.70, .80), (.56, .80)], GREY_D, STUFF, 0)
    s.add("ell", (.40, .20, .60, .38), (60, 56, 56), FIXED, 0)               # 兜帽阴影
    s.add("ell", (.38, .44, .62, .62), GREY, STUFF, 3)                       # 双手
    s.add("ell", (.43, .42, .57, .58), GOLD, FIXED, 3)                       # 圣徽
    s.add("ell", (.47, .46, .53, .54), GOLD_D, FIXED, 0)


BENCHES = [  # (文件名, 宽, 深, 函数, 是否材料着色)
    ("EnchantingTable", 3, 2, enchanting_table, True),
    ("ScribeDesk", 2, 1, scribe_desk, False),
    ("RuneAnvil", 3, 2, rune_anvil, True),
    ("GiantAnvil", 4, 3, giant_anvil, True),
    ("ArcaneLoom", 3, 1, arcane_loom, False),
    ("RuneQuenchTrough", 2, 1, quench_trough, True),
]
SINGLES = [("Alembic", alembic, False), ("ArcaneFocusStone", focus_stone, False), ("DeityStatue", deity_statue, True)]


def main():
    os.makedirs(OUT, exist_ok=True)
    written, tiles = [], []
    for name, w, h, fn, stuff in BENCHES:
        for view in ("south", "north", "east"):
            s = Sheet(w, h, view); fn(s)
            col, msk = s.images()
            p = os.path.join(OUT, name + "_" + view + ".png"); col.save(p); written.append(p)
            if stuff:
                pm = os.path.join(OUT, name + "_" + view + "m.png"); msk.save(pm); written.append(pm)
            tiles.append((name + " " + view, col))
    for name, fn, stuff in SINGLES:
        s = Sheet(1, 1, "south"); fn(s)
        col, msk = s.images()
        p = os.path.join(OUT, name + ".png"); col.save(p); written.append(p)
        if stuff:
            pm = os.path.join(OUT, name + "_m.png"); msk.save(pm); written.append(pm)
        tiles.append((name, col))
    # 总预览：材料色区按花岗岩色相乘，放在格子底上。
    pad = 16; rowh = 4 * CELL + 2 * pad
    widths = [t[1].width for t in tiles]
    W = 1700; x, y, rows = pad, pad, [[]]
    for t in tiles:
        if x + t[1].width + pad > W:
            rows.append([]); x = pad
        rows[-1].append(t); x += t[1].width + pad
    H = sum(max(t[1].height for t in r) + 2 * pad + 18 for r in rows) + pad
    prev = Image.new("RGBA", (W, H), (92, 88, 80, 255))
    d = ImageDraw.Draw(prev)
    for gx in range(0, W, CELL // 2):
        d.line([(gx, 0), (gx, H)], fill=(84, 80, 72, 255))
    for gy in range(0, H, CELL // 2):
        d.line([(0, gy), (W, gy)], fill=(84, 80, 72, 255))
    y = pad
    for r in rows:
        x = pad; rh = max(t[1].height for t in r)
        for label, img in r:
            prev.alpha_composite(img, (x, y + 18)); d.text((x, y), label, fill=(240, 236, 226, 255)); x += img.width + pad
        y += rh + 2 * pad + 18
    os.makedirs(os.path.dirname(PREVIEW), exist_ok=True)
    prev.convert("RGB").save(PREVIEW)
    print("wrote", len(written), "textures; preview", PREVIEW)


if __name__ == "__main__":
    main()
