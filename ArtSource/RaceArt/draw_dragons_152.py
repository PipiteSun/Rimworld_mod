# 1.52.0 五色真龙贴图：照 draw_race_art.py 的画法（粗黑轮廓、平涂、单侧暗面、低饱和）。
#  - Bridges/HAR/Textures/ChromaticDragons/<色>/Naked_Hulk_{south,east,north}.png：整条龙（含头），按颜色预先上色，256px；
#    Bridges/HAR/Textures/ChromaticDragons/Heads/<色>_*.png：透明（头画在身体贴图里）。
#  - Textures/ChromaticDragons/Items：龙鳞 5、真龙之心 5、龙之血 5、龙牙、龙巢情报；Faction（派系图标，白色给派系染色）、LairSite（龙巢地点）。
#  - ArtSource/Preview/ChromaticDragons-1.52.0.png：五色 × 三个年龄档（按游戏里的渲染缩放），旁边一个人做比例。
# 用法：python -I ArtSource/RaceArt/draw_dragons_152.py <模组根目录>
import math, os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import draw_race_art as base
from draw_race_art import Pic, poly, ell, tent, ellpts, mir, INK
from PIL import Image, ImageDraw

ROOT = sys.argv[1] if len(sys.argv) > 1 else os.getcwd()
HAR_TEX = os.path.join(ROOT, "Bridges", "HAR", "Textures")
MAIN_TEX = os.path.join(ROOT, "Textures")

# 低饱和调色：主色（鳞）、暗面、腹甲 / 翼膜、角
PAL = {
    "Red": dict(m=(152, 64, 54), d=(112, 46, 42), b=(200, 156, 108), w=(128, 72, 62), h=(222, 206, 172)),
    "Blue": dict(m=(72, 98, 142), d=(52, 72, 108), b=(176, 184, 172), w=(92, 112, 146), h=(214, 210, 190)),
    "Green": dict(m=(84, 120, 76), d=(62, 92, 58), b=(184, 178, 122), w=(96, 120, 84), h=(200, 190, 150)),
    "Black": dict(m=(64, 62, 70), d=(44, 42, 50), b=(118, 110, 100), w=(80, 74, 84), h=(214, 204, 178)),
    "White": dict(m=(214, 222, 228), d=(170, 182, 194), b=(240, 238, 228), w=(190, 204, 214), h=(150, 160, 172)),
}
EYE = {"Red": (240, 190, 60), "Blue": (170, 220, 250), "Green": (210, 230, 90), "Black": (230, 120, 60), "White": (120, 190, 230)}
OW = 2.2


def save(p, tex, rel, size):
    base.TEX = tex
    return p.save(rel, size)


# ---------------- 身体部件 ----------------
def wing_front(p, c, side=1, top=False):
    """正面 / 背面张开的翅膀：side=1 右边，-1 左边（画布坐标）。"""
    def X(x):
        return 50 + side * (x - 50)
    sh = (X(58), 44)
    tip = (X(94), 12)
    pts = [sh, (X(70), 30), tip, (X(96), 30), (X(92), 38), (X(86), 40), (X(84), 48), (X(76), 48), (X(72), 55), (X(62), 54)]
    p.add(poly(pts), c["w"], OW)
    # 翼骨
    for e in [(X(96), 30), (X(86), 40), (X(76), 48)]:
        p.line([sh, (X(70), 30), e], 1.0, c["d"])
    p.line([sh, (X(70), 30), tip], 1.8, c["m"])


def front_view(p, c, key, north=False):
    m, d, b = c["m"], c["d"], c["b"]
    if not north:
        wing_front(p, c, 1); wing_front(p, c, -1)
    # 尾巴（正面：从身后绕到左下；背面：朝向观众伸下来）
    if north:
        p.add(tent([(50, 66), (56, 80), (48, 90), (40, 94)], 10, 2), m, OW)
    else:
        p.add(tent([(56, 70), (70, 82), (82, 86), (90, 80)], 9, 2), m, OW)
    # 后腿
    for x in (34, 66):
        p.add(ell((x - 7, 64, x + 7, 82)), d, OW)
    # 身体
    p.add(ell((31, 38, 69, 78)), m, OW)
    p.add(poly([(62, 42), (68, 54), (68, 66), (62, 76), (58, 70), (62, 56)]), d, 0)  # 右侧暗面
    if not north:
        p.add(ell((40, 48, 60, 77)), b, 1.2)
        for y in (54, 60, 66, 72):
            p.line([(42, y), (58, y)], .8, c["d"] if key != "Black" else (90, 84, 78))
    else:
        for y in range(40, 74, 6):  # 背脊棘
            p.add(poly([(48, y + 4), (50, y - 1), (52, y + 4)]), c["h"], 1.0)
        wing_front(p, c, 1, True); wing_front(p, c, -1, True)
    # 前腿
    for s in (-1, 1):
        x0 = 50 + s * 11
        p.add(tent([(x0, 64), (x0 + s * 2, 76), (x0 + s * 1, 86)], 9, 7), m if s < 0 else d, OW)
        p.add(ell((x0 + s * 1 - 5, 83, x0 + s * 1 + 5, 90)), d, OW)
        for k in (-3, 0, 3):
            p.line([(x0 + s + k, 88), (x0 + s + k, 91)], 1.2, c["h"])
    # 颈、头
    if north:
        p.add(tent([(50, 46), (50, 32)], 13, 11), m, OW)
        head_back(p, c, key)
    else:
        p.add(tent([(50, 54), (50, 38)], 13, 11), m, OW)
        p.add(ell((45, 40, 55, 54)), b, 0)
        head_front(p, c, key)


def head_front(p, c, key):
    m, d, h = c["m"], c["d"], c["h"]
    horns_front(p, c, key)
    p.add(ell((39, 18, 61, 38)), m, OW)
    p.add(poly([(43, 30), (57, 30), (56, 44), (50, 47), (44, 44)]), m, OW)  # 吻
    p.add(poly([(55, 22), (60, 28), (58, 36), (55, 42), (54, 32)]), d, 0)
    p.add(poly([(45, 42), (50, 45), (55, 42), (52, 47), (48, 47)]), d, 0)
    for x in (47, 53):
        p.add(ell((x - .8, 42, x + .8, 43.6)), INK, 0)  # 鼻孔
    e = EYE[key]
    for x in (44, 56):
        p.add(poly([(x - 3, 27), (x + 3, 26), (x + 2, 29.5), (x - 2, 29.5)]), e, 1.0)
        p.add(ell((x - .6, 26.6, x + .6, 29.2)), INK, 0)
    if key == "Black":  # 骷髅感：深陷的眼窝、露骨的吻
        p.add(poly([(45, 34), (55, 34), (54, 40), (46, 40)]), c["h"], 1.0)
        p.line([(47, 34), (47, 40)], .8, INK); p.line([(50, 34), (50, 40)], .8, INK); p.line([(53, 34), (53, 40)], .8, INK)
    if key == "Blue":
        p.add(poly([(48, 36), (50, 25), (52, 36)]), h, 1.2)  # 鼻上独角
    if key == "Green":
        p.add(poly([(46, 18), (50, 8), (54, 18)]), c["w"], 1.2)  # 头冠


def horns_front(p, c, key):
    h = c["h"]
    if key == "Red":  # 后掠大角
        for s in (-1, 1):
            p.add(tent([(50 + s * 8, 22), (50 + s * 16, 14), (50 + s * 22, 4)], 5, 1.2), h, OW)
    elif key == "Blue":
        for s in (-1, 1):
            p.add(tent([(50 + s * 9, 22), (50 + s * 15, 16), (50 + s * 17, 10)], 3.4, 1), h, OW)
            p.add(poly([(50 + s * 10, 30), (50 + s * 18, 26), (50 + s * 11, 35)]), c["w"], 1.2)  # 耳鳍
    elif key == "Green":
        for s in (-1, 1):
            for i, y in enumerate((20, 26, 32)):
                p.add(poly([(50 + s * 10, y), (50 + s * (17 - i), y - 4), (50 + s * 11, y + 3)]), c["w"], 1.0)
    elif key == "Black":  # 前伸弯角
        for s in (-1, 1):
            p.add(tent([(50 + s * 9, 22), (50 + s * 17, 18), (50 + s * 18, 28), (50 + s * 14, 34)], 4.5, 1.2), h, OW)
    elif key == "White":  # 圆头小角、鬃冠
        p.add(poly([(40, 22), (44, 12), (48, 19), (50, 10), (52, 19), (56, 12), (60, 22)]), c["d"], 1.2)
        for s in (-1, 1):
            p.add(tent([(50 + s * 8, 20), (50 + s * 12, 14)], 3, 1), h, 1.2)


def head_back(p, c, key):
    m, d, h = c["m"], c["d"], c["h"]
    horns_front(p, c, key)
    p.add(ell((40, 14, 60, 34)), m, OW)
    p.add(poly([(55, 18), (60, 26), (57, 32), (54, 26)]), d, 0)
    for y in (22, 28):
        p.add(poly([(48, y + 3), (50, y - 2), (52, y + 3)]), h, 1.0)


def side_view(p, c, key):
    m, d, b, h = c["m"], c["d"], c["b"], c["h"]
    # 远处的翅膀（暗）
    p.add(poly([(42, 44), (34, 22), (44, 6), (54, 18), (60, 16), (58, 28), (54, 40)]), c["d"], OW)
    # 尾巴
    p.add(tent([(36, 60), (22, 66), (10, 64), (4, 54), (8, 46)], 11, 2), m, OW)
    p.add(poly([(6, 48), (2, 42), (10, 44)]), d, 1.2)  # 尾尖棘
    # 远处的腿
    p.add(tent([(64, 64), (66, 76), (64, 86)], 7, 6), d, OW)
    p.add(tent([(36, 62), (34, 76), (36, 86)], 8, 6), d, OW)
    # 身体
    p.add(ell((26, 44, 74, 72)), m, OW)
    p.add(poly([(30, 64), (40, 70), (60, 71), (70, 64), (66, 71), (50, 74), (34, 72)]), b, 0)
    for x in (38, 46, 54, 62):
        p.line([(x, 67), (x, 72)], .8, d)
    for x in range(30, 70, 6):  # 背脊棘
        p.add(poly([(x, 46), (x + 2.5, 41), (x + 5, 46)]), h, 1.0)
    # 近处的腿
    p.add(tent([(36, 60), (32, 74), (36, 86)], 12, 7), m, OW)
    p.add(ell((31, 83, 41, 89)), d, OW)
    p.add(tent([(62, 62), (64, 76), (66, 86)], 8, 6), m, OW)
    p.add(ell((61, 83, 71, 89)), d, OW)
    for x in (33, 36, 39, 64, 67, 70):
        p.line([(x, 88), (x + 1, 91)], 1.1, h)
    # 颈
    p.add(tent([(66, 54), (74, 42), (78, 32)], 13, 10), m, OW)
    p.add(tent([(70, 56), (78, 44), (81, 36)], 5, 4), b, 0)
    # 头（朝右）
    horns_side(p, c, key)
    p.add(ell((70, 20, 88, 36)), m, OW)
    p.add(poly([(82, 24), (97, 28), (98, 33), (84, 36)]), m, OW)  # 吻
    p.add(poly([(80, 34), (96, 34), (94, 38), (82, 39)]), d, OW)  # 下颚
    p.add(ell((94, 28.5, 95.6, 30)), INK, 0)
    e = EYE[key]
    p.add(poly([(80, 25), (85, 25), (84, 28), (80, 28)]), e, 1.0)
    p.add(ell((82, 25.3, 83.2, 27.8)), INK, 0)
    for x in (86, 89, 92):
        p.add(poly([(x, 34), (x + 1, 36.5), (x + 2, 34)]), (240, 236, 224), 0)  # 牙
    if key == "Blue":
        p.add(poly([(90, 27), (94, 18), (95, 28)]), h, 1.2)
    if key == "Black":
        p.add(poly([(86, 29), (96, 31), (95, 34), (86, 34)]), c["h"], 1.0)
    # 近处的翅膀（收拢、高高竖起）
    p.add(poly([(44, 48), (40, 26), (50, 2), (62, 10), (64, 22), (70, 24), (64, 36), (58, 46)]), c["w"], OW)
    for e2 in [(62, 10), (64, 22), (64, 36)]:
        p.line([(46, 46), (50, 4), e2], 1.0, c["d"])
    p.line([(46, 46), (50, 4)], 1.8, m)


def horns_side(p, c, key):
    h = c["h"]
    if key == "Red":
        p.add(tent([(74, 24), (66, 16), (58, 12)], 5, 1.2), h, OW)
    elif key == "Blue":
        p.add(tent([(74, 24), (68, 18), (66, 12)], 3.4, 1), h, OW)
        p.add(poly([(74, 30), (64, 28), (72, 34)]), c["w"], 1.2)
    elif key == "Green":
        for i, x in enumerate((72, 66, 60)):
            p.add(poly([(x + 4, 22 + i * 4), (x - 4, 14 + i * 6), (x, 26 + i * 4)]), c["w"], 1.0)
        p.add(poly([(76, 20), (78, 8), (84, 20)]), c["w"], 1.2)
    elif key == "Black":
        p.add(tent([(74, 24), (70, 16), (78, 12), (86, 16)], 4.5, 1.2), h, OW)
    elif key == "White":
        p.add(poly([(70, 26), (66, 16), (72, 20), (74, 12), (78, 21)]), c["d"], 1.2)
        p.add(tent([(78, 21), (80, 15)], 3, 1), h, 1.2)


def draw_bodies():
    for key, c in PAL.items():
        for rot in ("south", "east", "north"):
            p = Pic()
            if rot == "east":
                side_view(p, c, key)
            else:
                front_view(p, c, key, north=(rot == "north"))
            save(p, HAR_TEX, f"ChromaticDragons/{key}/Naked_Hulk_{rot}", 256)
        for rot in ("south", "east", "north"):
            path = os.path.join(HAR_TEX, "ChromaticDragons", "Heads", f"{key}_{rot}.png")
            os.makedirs(os.path.dirname(path), exist_ok=True)
            Image.new("RGBA", (128, 128), (0, 0, 0, 0)).save(path)


# ---------------- 物品 ----------------
def scale_icon(key):
    c = PAL[key]; p = Pic()
    for (x, y, r) in [(34, 60, 18), (62, 58, 18), (48, 40, 19)]:
        pts = [(x, y - r), (x + r * .8, y - r * .2), (x + r * .55, y + r * .7), (x, y + r), (x - r * .55, y + r * .7), (x - r * .8, y - r * .2)]
        p.add(poly(pts), c["m"], OW)
        p.add(poly([(x, y - r), (x + r * .8, y - r * .2), (x + r * .55, y + r * .7), (x, y + r)]), c["d"], 0)
        p.line([(x, y - r + 3), (x, y + r - 3)], 1.0, c["b"])
    save(p, MAIN_TEX, f"ChromaticDragons/Items/Scale_{key}", 128)


def heart_icon(key):
    c = PAL[key]; p = Pic()
    p.add(poly([(50, 86), (22, 58), (18, 40), (26, 26), (40, 24), (50, 34), (60, 24), (74, 26), (82, 40), (78, 58)]), c["m"], OW)
    p.add(poly([(50, 86), (78, 58), (82, 40), (74, 26), (66, 30), (70, 50)]), c["d"], 0)
    p.add(tent([(44, 26), (42, 14), (36, 10)], 7, 5), c["d"], OW)
    p.add(tent([(56, 26), (60, 16)], 6, 5), c["d"], OW)
    p.line([(36, 44), (46, 54), (42, 66)], 1.2, c["b"])
    p.add(ell((30, 34, 40, 42)), tuple(min(255, v + 50) for v in c["m"]), 0)
    save(p, MAIN_TEX, f"ChromaticDragons/Items/Heart_{key}", 128)


def blood_icon(key):
    c = PAL[key]; p = Pic()
    glass = (196, 206, 212)
    p.add(poly([(42, 14), (58, 14), (58, 34), (74, 52), (74, 84), (26, 84), (26, 52), (42, 34)]), glass, OW)
    p.add(poly([(28, 58), (72, 58), (72, 82), (28, 82)]), c["m"], 0)
    p.add(poly([(60, 58), (72, 58), (72, 82), (62, 82)]), c["d"], 0)
    p.add(poly([(40, 6), (60, 6), (60, 16), (40, 16)]), base.WOOD, OW)
    p.add(poly([(32, 62), (36, 62), (36, 76), (32, 76)]), (238, 240, 242), 0)
    save(p, MAIN_TEX, f"ChromaticDragons/Items/Blood_{key}", 128)


def fang_icon():
    p = Pic()
    p.add(tent([(28, 20), (40, 50), (56, 74), (76, 86)], 22, 2), base.BONE, OW)
    p.add(tent([(36, 24), (48, 52), (62, 74), (76, 86)], 8, 1), (200, 188, 160), 0)
    p.add(ell((20, 12, 40, 30)), (170, 120, 100), OW)
    save(p, MAIN_TEX, "ChromaticDragons/Items/Fang", 128)


def lairmap_icon():
    p = Pic()
    hide = (196, 166, 120)
    p.add(poly([(14, 22), (86, 16), (90, 78), (16, 86)]), hide, OW)
    p.add(poly([(60, 18), (86, 16), (90, 78), (64, 80)]), (172, 142, 100), 0)
    p.line([(24, 70), (36, 56), (46, 60), (58, 44), (70, 36)], 1.5, (120, 60, 50))
    p.add(poly([(64, 30), (76, 30), (76, 42), (64, 42)]), (152, 64, 54), 1.5)
    p.line([(64, 30), (76, 42)], 1.4, INK); p.line([(76, 30), (64, 42)], 1.4, INK)
    for x, y in [(28, 34), (40, 28), (50, 36)]:
        p.add(poly([(x - 5, y + 4), (x, y - 4), (x + 5, y + 4)]), (140, 130, 116), 1.0)
    save(p, MAIN_TEX, "ChromaticDragons/Items/LairMap", 128)


def faction_icon():
    p = Pic()
    white = (240, 240, 240)
    p.add(poly([(50, 90), (26, 64), (20, 36), (34, 14), (44, 30), (50, 8), (56, 30), (66, 14), (80, 36), (74, 64)]), white, 0)
    save(p, MAIN_TEX, "ChromaticDragons/Faction", 128)


def lairsite_icon():
    p = Pic()
    p.add(poly([(6, 86), (30, 30), (44, 46), (58, 18), (94, 86)]), (118, 110, 100), OW)
    p.add(poly([(58, 18), (94, 86), (70, 86)]), (92, 86, 80), 0)
    p.add(poly([(40, 86), (44, 64), (52, 58), (60, 64), (62, 86)]), (34, 30, 30), OW)
    p.add(poly([(46, 84), (50, 76), (56, 80), (58, 86)]), (214, 184, 118), 0)
    p.add(tent([(70, 40), (60, 30), (52, 32)], 4, 1), (152, 64, 54), 1.2)
    save(p, MAIN_TEX, "ChromaticDragons/LairSite", 128)


def preview():
    scales = [2.0, 2.8, 3.8]
    cell = 40  # 每格像素
    cols = []
    for key in PAL:
        img = Image.open(os.path.join(HAR_TEX, "ChromaticDragons", key, "Naked_Hulk_east.png")).convert("RGBA")
        cols.append([img.resize((int(1.5 * s * cell), int(1.5 * s * cell)), Image.LANCZOS) for s in scales])
    rows = [int(1.5 * s * cell) for s in scales]
    W = sum(max(i.width for i in col) for col in cols) + 100 + 30 * 5
    H = sum(rows) + 20 * 4
    out = Image.new("RGBA", (W, H), (236, 230, 216, 255))
    d = ImageDraw.Draw(out)
    y = 20
    for r, rh in enumerate(rows):
        base_y = y + int(rh * .92)  # 龙脚大约在贴图 90% 高处
        # 人（约 1 格高，原版人形贴图 1.5 格里身体约占 1 格）
        hx, hy = 50, base_y
        d.ellipse((hx - 7, hy - 40, hx + 7, hy - 27), fill=(214, 168, 132), outline=INK, width=2)
        d.rectangle((hx - 8, hy - 27, hx + 8, hy - 7), fill=(110, 120, 90), outline=INK, width=2)
        d.line((hx - 5, hy - 7, hx - 5, hy), fill=INK, width=3); d.line((hx + 5, hy - 7, hx + 5, hy), fill=INK, width=3)
        x = 100
        for col in cols:
            cw = max(i.width for i in col); img = col[r]
            out.alpha_composite(img, (x + (cw - img.width) // 2, y + rh - img.height)); x += cw + 30
        y += rh + 20
    path = os.path.join(ROOT, "ArtSource", "Preview", "ChromaticDragons-1.52.0.png")
    os.makedirs(os.path.dirname(path), exist_ok=True)
    out.convert("RGB").save(path)


draw_bodies()
for k in PAL:
    scale_icon(k); heart_icon(k); blood_icon(k)
fang_icon(); lairmap_icon(); faction_icon(); lairsite_icon()
preview()
print("ok")
