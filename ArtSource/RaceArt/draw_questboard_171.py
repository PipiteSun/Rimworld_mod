# 1.71.0 探索公告板贴图：Graphic_Multi（南 / 北 / 东，西由东翻转），2×2 格画布（256 px），占地 2×1，板子往上伸。
# 画法沿用 draw_workbenches_147.py：粗黑轮廓、平涂 + 一层暗面、低饱和。固定色，不需要遮罩。
# 用法：python -I ArtSource/RaceArt/draw_questboard_171.py <模组根目录>
import os, sys
from PIL import Image, ImageDraw

ROOT = sys.argv[1] if len(sys.argv) > 1 else os.getcwd()
OUT = os.path.join(ROOT, "Textures", "LP", "QuestBoard")
PREVIEW = os.path.join(ROOT, "ArtSource", "Preview", "QuestBoard-1.71.0.png")
SIZE, SS = 256, 4
INK = (34, 30, 30)
WOOD, WOOD_D, WOOD_L = (128, 92, 62), (96, 68, 46), (156, 118, 82)
DARK, DARK_D = (84, 62, 50), (62, 46, 38)
CORK, CORK_D = (170, 128, 84), (140, 104, 66)
PARCH, PARCH_D = (226, 212, 176), (190, 172, 132)
RED, GOLD, IRON = (150, 52, 44), (208, 172, 92), (78, 78, 84)


def S(v):
    return int(v * SS)


class Pic:
    def __init__(self):
        self.im = Image.new("RGBA", (SIZE * SS, SIZE * SS), (0, 0, 0, 0))
        self.d = ImageDraw.Draw(self.im)

    def rect(self, x0, y0, x1, y1, fill, ow=3, r=3):
        self.d.rounded_rectangle([S(x0), S(y0), S(x1), S(y1)], radius=S(r), fill=fill, outline=INK if ow else None, width=S(ow) if ow else 0)

    def poly(self, pts, fill, ow=3):
        self.d.polygon([(S(x), S(y)) for x, y in pts], fill=fill, outline=INK)
        if ow:
            self.d.line([(S(x), S(y)) for x, y in pts + [pts[0]]], fill=INK, width=S(ow), joint="curve")

    def line(self, pts, fill, w=2):
        self.d.line([(S(x), S(y)) for x, y in pts], fill=fill, width=S(w))

    def dot(self, x, y, r, fill):
        self.d.ellipse([S(x - r), S(y - r), S(x + r), S(y + r)], fill=fill, outline=INK, width=S(1.5))

    def save(self, path):
        self.im.resize((SIZE, SIZE), Image.LANCZOS).save(path)
        return Image.open(path)


def posts(p, x_left=40, x_right=216, top=58, bottom=226):
    for x in (x_left, x_right - 16):
        p.rect(x, top, x + 16, bottom, WOOD, 3, 2)
        p.rect(x + 10, top + 4, x + 16, bottom - 2, WOOD_D, 0, 1)


def roof(p, y=40):
    p.poly([(26, y + 20), (128, y - 6), (230, y + 20), (230, y + 30), (26, y + 30)], DARK)
    p.poly([(128, y - 6), (230, y + 20), (230, y + 30), (128, y + 4)], DARK_D, 0)
    for x in range(44, 220, 22):
        p.line([(x, y + 22 - (6 if 60 < x < 196 else 2)), (x, y + 29)], DARK_D, 1.5)


def south():
    p = Pic()
    posts(p)
    roof(p)
    p.rect(32, 72, 224, 178, WOOD, 4, 4)            # 外框
    p.rect(42, 80, 214, 170, CORK, 3, 2)            # 软木板
    p.rect(42, 158, 214, 170, CORK_D, 0, 0)
    notes = [(50, 86, 92, 132, 0), (98, 84, 146, 124, 1), (152, 88, 206, 136, 2), (60, 136, 110, 166, 3), (118, 128, 160, 166, 4), (166, 140, 206, 166, 5)]
    for x0, y0, x1, y1, i in notes:
        p.rect(x0, y0, x1, y1, PARCH, 2, 1)
        p.rect(x0 + 2, y1 - 6, x1 - 2, y1 - 1, PARCH_D, 0, 0)
        for k in range(3):
            yy = y0 + 10 + k * 8
            if yy < y1 - 8:
                p.line([(x0 + 6, yy), (x1 - 6 - (k * 5) % 12, yy)], (120, 104, 84), 1.5)
        p.dot((x0 + x1) / 2, y0 + 3, 2.6, RED if i % 3 == 0 else GOLD if i % 3 == 1 else IRON)
    p.dot(176, 106, 6, RED)                         # 公会蜡封
    p.line([(32, 186), (224, 186)], INK, 3)
    p.rect(30, 182, 226, 192, WOOD_D, 3, 2)          # 底横梁
    return p


def north():
    p = Pic()
    posts(p)
    roof(p)
    p.rect(32, 72, 224, 178, WOOD_D, 4, 4)
    for x in range(42, 200, 24):
        p.rect(x, 78, x + 22, 172, WOOD if (x // 24) % 2 else WOOD_L, 2, 1)
    p.rect(34, 112, 222, 124, DARK, 3, 2)
    p.rect(34, 146, 222, 158, DARK, 3, 2)
    p.rect(30, 182, 226, 192, WOOD_D, 3, 2)
    return p


def east():
    p = Pic()
    # 侧面：一根立柱 + 板子的厚度 + 屋檐，靠画布中线
    p.poly([(108, 46), (148, 46), (156, 72), (100, 72)], DARK)
    p.rect(118, 58, 136, 226, WOOD, 3, 2)
    p.rect(130, 62, 136, 224, WOOD_D, 0, 1)
    p.rect(112, 72, 142, 178, WOOD_D, 3, 2)
    p.rect(138, 84, 146, 132, PARCH, 2, 1)          # 侧面翘起的告示一角
    p.rect(110, 182, 146, 192, WOOD_D, 3, 2)
    return p


def main():
    os.makedirs(OUT, exist_ok=True)
    os.makedirs(os.path.dirname(PREVIEW), exist_ok=True)
    ims = []
    for name, fn in (("south", south), ("north", north), ("east", east)):
        ims.append(fn().save(os.path.join(OUT, "QuestBoard_" + name + ".png")))
    sheet = Image.new("RGBA", (SIZE * 3 + 40, SIZE + 20), (120, 116, 104, 255))
    for i, im in enumerate(ims):
        sheet.alpha_composite(im.convert("RGBA"), (10 + i * (SIZE + 10), 10))
    sheet.save(PREVIEW)
    print("wrote", OUT, PREVIEW)


if __name__ == "__main__":
    main()
