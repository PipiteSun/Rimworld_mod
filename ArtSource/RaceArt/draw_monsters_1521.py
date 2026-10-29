# 1.52.1 传奇怪贴图：照 draw_race_art.py / draw_dragons_152.py 的画法（粗黑轮廓、平涂、单侧暗面、低饱和）。
#  - Bridges/HAR/Textures/LegendaryMonsters/<Behir|Beholder|Demilich>/Naked_Hulk_{south,east,north}.png：整只怪（含头），256px；Heads/*：透明。
#  - Textures/LegendaryMonsters/Items：贝希尔角、眼魔主眼、眼柄、反魔法护符、地狱火宝珠、灵魂宝石、半巫妖头骨、两份情报；Faction、SiteBeholder、SiteTomb。
#  - ArtSource/Preview/LegendaryMonsters-1.52.1.png：四只传奇怪（死亡骑士用示意的铠甲人形）按游戏里的渲染缩放，旁边一个人做比例。
# 用法：python -I ArtSource/RaceArt/draw_monsters_1521.py <模组根目录>
import math, os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import draw_race_art as base
from draw_race_art import Pic, poly, ell, tent, ellpts, INK
from PIL import Image, ImageDraw

ROOT = sys.argv[1] if len(sys.argv) > 1 else os.getcwd()
HAR_TEX = os.path.join(ROOT, "Bridges", "HAR", "Textures")
MAIN_TEX = os.path.join(ROOT, "Textures")
OW = 2.2

BEHIR = dict(m=(70, 96, 140), d=(50, 70, 106), b=(176, 182, 168), h=(214, 206, 176))
BEHOLD = dict(m=(122, 94, 128), d=(92, 68, 98), b=(200, 170, 150), e=(236, 226, 196), i=(150, 60, 56))
BONE = (226, 216, 190); BONE_D = (182, 170, 144); GEM = (110, 200, 170); GEM_D = (60, 140, 120)


def save(p, tex, rel, size):
    base.TEX = tex
    return p.save(rel, size)


def blank(rel):
    path = os.path.join(HAR_TEX, rel.replace("/", os.sep) + ".png")
    os.makedirs(os.path.dirname(path), exist_ok=True)
    Image.new("RGBA", (128, 128), (0, 0, 0, 0)).save(path)


# ---------------- 贝希尔 ----------------
def behir_east(p):
    c = BEHIR
    body = [(8, 60), (20, 52), (34, 56), (48, 50), (62, 54), (74, 46)]
    # 远处的足
    for x in (16, 30, 44, 58):
        p.add(tent([(x + 3, 60), (x + 1, 70), (x + 4, 78)], 4.5, 3.5), c["d"], OW)
    p.add(tent(body, 11, 15), c["m"], OW)
    p.add(tent([(x, y + 4) for x, y in body], 4, 6), c["b"], 0)
    for x, y in body[1:]:
        p.add(poly([(x - 2, y - 6), (x, y - 11), (x + 2, y - 6)]), c["h"], 1.0)
    p.add(tent([(10, 60), (4, 66), (6, 74)], 6, 2), c["m"], OW)  # 尾尖
    for x in (12, 26, 40, 54, 66):
        p.add(tent([(x + 2, 60), (x, 72), (x + 3, 82)], 5, 4), c["m"], OW)
        p.line([(x + 1, 82), (x + 4, 84)], 1.0, c["h"])
    # 头（朝右）
    p.add(tent([(70, 48), (78, 40)], 13, 11), c["m"], OW)
    p.add(ell((74, 28, 92, 44)), c["m"], OW)
    p.add(poly([(86, 32), (99, 37), (98, 42), (86, 44)]), c["m"], OW)
    p.add(poly([(84, 42), (97, 42), (95, 46), (85, 47)]), c["d"], OW)
    p.add(tent([(80, 30), (74, 18), (66, 12)], 5, 1.2), c["h"], OW)  # 后掠的角
    p.add(poly([(82, 33), (87, 33), (86, 36), (82, 36)]), (240, 220, 120), 1.0)
    p.add(ell((84, 33.4, 85.2, 35.8)), INK, 0)
    for x in (88, 91, 94):
        p.add(poly([(x, 42), (x + 1, 44.5), (x + 2, 42)]), (240, 236, 224), 0)
    # 闪电纹
    p.line([(36, 52), (40, 47), (44, 52), (48, 47)], 1.0, (200, 220, 250))


def behir_front(p, north=False):
    c = BEHIR
    # 盘起的身体
    p.add(ell((14, 46, 86, 84)), c["m"], OW)
    p.add(ell((26, 56, 74, 80)), c["d"], OW)
    p.add(ell((36, 62, 64, 76)), c["m"], OW)
    for i, (x, y) in enumerate([(18, 76), (30, 84), (46, 86), (62, 86), (78, 80), (86, 70)]):
        p.add(tent([(x, y - 4), (x + (2 if x > 50 else -2), y + 4)], 5, 4), c["d"] if i % 2 else c["m"], OW)
    if north:
        for x in range(24, 78, 7):
            p.add(poly([(x, 50), (x + 2.5, 44), (x + 5, 50)]), c["h"], 1.0)
        p.add(tent([(50, 52), (50, 34)], 14, 11), c["m"], OW)
        p.add(ell((40, 16, 60, 36)), c["m"], OW)
        for s in (-1, 1):
            p.add(tent([(50 + s * 6, 22), (50 + s * 10, 10)], 4.5, 1.2), c["h"], OW)
        return
    p.add(tent([(50, 60), (50, 38)], 14, 11), c["m"], OW)
    p.add(tent([(50, 60), (50, 40)], 5, 4), c["b"], 0)
    for s in (-1, 1):
        p.add(tent([(50 + s * 7, 22), (50 + s * 13, 8)], 4.5, 1.2), c["h"], OW)
    p.add(ell((38, 16, 62, 38)), c["m"], OW)
    p.add(poly([(43, 30), (57, 30), (55, 44), (45, 44)]), c["m"], OW)
    p.add(poly([(55, 20), (60, 28), (57, 38), (55, 30)]), c["d"], 0)
    for x in (44, 56):
        p.add(poly([(x - 3, 26), (x + 3, 25), (x + 2, 28.5), (x - 2, 28.5)]), (240, 220, 120), 1.0)
        p.add(ell((x - .6, 25.6, x + .6, 28.2)), INK, 0)
    for x in (47, 53):
        p.add(ell((x - .8, 41, x + .8, 42.6)), INK, 0)


# ---------------- 眼魔 ----------------
def stalks(p, c, cx, cy, r, back=False):
    for i in range(10):
        a = math.radians(-160 + i * 14.5)
        sx, sy = cx + r * .7 * math.cos(a), cy + r * .7 * math.sin(a)
        ex, ey = cx + r * 1.35 * math.cos(a), cy + r * 1.25 * math.sin(a) - 6
        mx, my = (sx + ex) / 2 + 3 * math.cos(a + 1.4), (sy + ey) / 2 - 3
        p.add(tent([(sx, sy), (mx, my), (ex, ey)], 3.2, 2.2), c["d"] if i % 2 else c["m"], 1.6)
        p.add(ell((ex - 3, ey - 3, ex + 3, ey + 3)), c["e"] if not back else c["m"], 1.4)
        if not back:
            p.add(ell((ex - 1.2, ey - 1.2, ex + 1.2, ey + 1.2)), c["i"], 0)


def beholder_front(p, north=False):
    c = BEHOLD
    stalks(p, c, 50, 52, 28, back=north)
    p.add(ell((22, 26, 78, 82)), c["m"], OW)
    p.add(poly([(64, 32), (76, 46), (76, 62), (66, 76), (60, 70), (66, 54)]), c["d"], 0)
    if north:
        for (x, y) in [(36, 44), (52, 38), (60, 56), (42, 64)]:
            p.add(ell((x - 3, y - 2, x + 3, y + 2)), c["d"], 0)
        return
    # 主眼
    p.add(ell((36, 36, 64, 58)), c["e"], 1.8)
    p.add(ell((44, 40, 56, 54)), c["i"], 1.2)
    p.add(ell((47.5, 42, 52.5, 52)), INK, 0)
    p.add(ell((48, 42.5, 50, 45)), (250, 250, 250), 0)
    # 巨口
    p.add(poly([(34, 64), (66, 64), (60, 74), (40, 74)]), (70, 40, 46), 1.8)
    for x in range(36, 64, 4):
        p.add(poly([(x, 64), (x + 2, 69), (x + 4, 64)]), (240, 236, 224), 0)
        p.add(poly([(x + 1, 74), (x + 3, 70), (x + 5, 74)]), (240, 236, 224), 0)


def beholder_east(p):
    c = BEHOLD
    stalks(p, c, 46, 52, 28)
    p.add(ell((20, 26, 76, 82)), c["m"], OW)
    p.add(poly([(24, 44), (30, 32), (36, 30), (30, 46), (30, 66), (36, 76), (28, 70)]), c["d"], 0)
    p.add(ell((56, 36, 76, 58)), c["e"], 1.8)
    p.add(ell((64, 40, 74, 54)), c["i"], 1.2)
    p.add(ell((68, 42, 72, 52)), INK, 0)
    p.add(poly([(52, 64), (76, 62), (72, 74), (56, 74)]), (70, 40, 46), 1.8)
    for x in range(54, 74, 4):
        p.add(poly([(x, 64), (x + 2, 68), (x + 4, 63)]), (240, 236, 224), 0)


# ---------------- 半巫妖 ----------------
def aura(p):
    for r, col in ((30, (150, 210, 190)), (24, (180, 226, 210))):
        p.add(ell((50 - r, 52 - r, 50 + r, 52 + r)), col, 0)


def demilich(p, rot):
    # 画布里只占中间一块（渲染 ×1.0，体型 0.8）：头骨约 40 单位宽，下面一圈淡淡的魂光。
    p.add(ell((26, 70, 74, 80)), (170, 220, 200), 0)  # 地上的魂光
    if rot == "east":
        p.add(poly([(32, 30), (44, 22), (60, 22), (70, 30), (72, 44), (66, 52), (64, 62), (52, 64), (44, 58), (34, 54), (30, 42)]), BONE, OW)
        p.add(poly([(32, 30), (40, 26), (38, 44), (34, 54), (30, 42)]), BONE_D, 0)
        p.add(ell((56, 34, 66, 44)), INK, 1.2)
        p.add(ell((58, 36, 64, 42)), GEM, 0)
        p.add(ell((60, 37, 62, 39)), (230, 250, 240), 0)
        p.add(poly([(62, 52), (70, 50), (70, 54), (64, 58)]), BONE_D, 1.2)
        for x in (60, 63, 66):
            p.line([(x, 56), (x, 60)], 1.0, INK)
        return
    p.add(poly([(30, 34), (36, 22), (50, 18), (64, 22), (70, 34), (70, 46), (64, 52), (62, 62), (38, 62), (36, 52), (30, 46)]), BONE, OW)
    p.add(poly([(62, 24), (70, 34), (70, 46), (64, 52), (62, 40)]), BONE_D, 0)
    if rot == "north":
        p.line([(50, 20), (48, 30), (52, 38)], 1.2, BONE_D)
        return
    for x in (41, 59):
        p.add(ell((x - 6, 32, x + 6, 44)), INK, 1.2)
        p.add(ell((x - 3.5, 34.5, x + 3.5, 41.5)), GEM, 0)
        p.add(ell((x - 1.5, 35.5, x, 37.5)), (230, 250, 240), 0)
    p.add(poly([(47, 46), (50, 51), (53, 46)]), INK, 0)
    p.add(poly([(39, 54), (61, 54), (60, 62), (40, 62)]), BONE_D, 1.2)
    for x in range(42, 60, 3):
        p.line([(x, 54), (x, 62)], 0.9, INK)
    p.line([(56, 22), (60, 30), (58, 36)], 1.0, BONE_D)  # 裂纹


def draw_bodies():
    for key, fn in (("Behir", None), ("Beholder", None), ("Demilich", None)):
        for rot in ("south", "east", "north"):
            p = Pic()
            if key == "Behir":
                behir_east(p) if rot == "east" else behir_front(p, rot == "north")
            elif key == "Beholder":
                beholder_east(p) if rot == "east" else beholder_front(p, rot == "north")
            else:
                demilich(p, rot)
            save(p, HAR_TEX, f"LegendaryMonsters/{key}/Naked_Hulk_{rot}", 256)
            blank(f"LegendaryMonsters/Heads/{key}_{rot}")


# ---------------- 物品与图标 ----------------
def icon(rel, draw, tex=MAIN_TEX):
    p = Pic(); draw(p); save(p, tex, rel, 128)


def horn(p):
    p.add(tent([(24, 84), (40, 60), (56, 38), (74, 16)], 20, 2), BEHIR["h"], OW)
    p.add(tent([(30, 82), (44, 60), (58, 40), (74, 16)], 6, 1), (176, 168, 140), 0)
    for t in (.3, .5, .7):
        x, y = 24 + 50 * t, 84 - 68 * t
        p.line([(x - 6, y - 4), (x + 6, y + 4)], 1.2, (160, 152, 126))
    p.line([(60, 30), (64, 24), (60, 22), (66, 14)], 1.4, (150, 200, 250))


def eye(p):
    p.add(ell((14, 18, 86, 82)), BEHOLD["e"], OW)
    p.add(ell((32, 30, 68, 70)), BEHOLD["i"], 1.6)
    p.add(ell((42, 36, 58, 64)), INK, 0)
    p.add(ell((44, 38, 50, 46)), (250, 250, 250), 0)
    p.line([(18, 56), (26, 52), (24, 62)], 1.2, (190, 110, 100))
    p.line([(84, 46), (76, 44), (78, 36)], 1.2, (190, 110, 100))


def stalk(p):
    p.add(tent([(20, 86), (36, 64), (50, 50), (66, 34)], 10, 6), BEHOLD["m"], OW)
    p.add(ell((58, 14, 86, 42)), BEHOLD["e"], OW)
    p.add(ell((66, 22, 78, 34)), BEHOLD["i"], 1.2)
    p.add(ell((70, 25, 74, 31)), INK, 0)


def amulet(p):
    p.line([(30, 10), (50, 40), (70, 10)], 2.0, (150, 120, 70))
    p.add(poly(ellpts(50, 60, 26, 26)), base.GOLD, OW)
    p.add(poly(ellpts(50, 60, 17, 17)), BEHOLD["e"], 1.4)
    p.add(poly(ellpts(50, 60, 9, 11)), BEHOLD["i"], 1.0)
    p.add(poly(ellpts(50, 60, 4, 7)), INK, 0)
    p.line([(50, 34), (50, 40)], 1.6, base.GOLD_D)


def hellorb(p):
    p.add(poly(ellpts(50, 54, 32, 32)), (120, 30, 26), OW)
    p.add(poly(ellpts(46, 50, 22, 22)), (200, 70, 40), 0)
    p.add(poly(ellpts(42, 46, 11, 11)), (250, 170, 80), 0)
    p.add(poly([(40, 30), (46, 18), (50, 28), (56, 14), (60, 30)]), (230, 110, 50), 1.4)
    p.line([(62, 66), (70, 72), (66, 78)], 1.4, (60, 20, 20))


def soulgem(p):
    pts = [(50, 10), (78, 34), (70, 80), (30, 80), (22, 34)]
    p.add(poly(pts), GEM, OW)
    p.add(poly([(50, 10), (78, 34), (70, 80), (50, 60)]), GEM_D, 0)
    p.line([(22, 34), (50, 44), (78, 34)], 1.2, (200, 240, 230))
    p.line([(50, 44), (50, 80)], 1.0, (200, 240, 230))
    p.add(poly(ellpts(40, 50, 4, 6)), (240, 252, 248), 0)


def skull(p):
    p.add(poly([(22, 40), (30, 18), (50, 12), (70, 18), (78, 40), (76, 56), (68, 62), (66, 80), (34, 80), (32, 62), (24, 56)]), BONE, OW)
    p.add(poly([(68, 20), (78, 40), (76, 56), (68, 62), (68, 40)]), BONE_D, 0)
    for x in (38, 62):
        p.add(ell((x - 9, 38, x + 9, 56)), INK, 1.4)
    p.add(poly([(46, 60), (50, 68), (54, 60)]), INK, 0)
    p.line([(56, 14), (60, 26), (56, 34)], 1.3, BONE_D)
    p.line([(36, 72), (64, 72)], 1.0, INK)
    for x in range(38, 64, 4):
        p.line([(x, 68), (x, 78)], 0.9, INK)


def intel(color, mark):
    def d(p):
        hide = (196, 166, 120)
        p.add(poly([(14, 22), (86, 16), (90, 78), (16, 86)]), hide, OW)
        p.add(poly([(60, 18), (86, 16), (90, 78), (64, 80)]), (172, 142, 100), 0)
        p.line([(24, 70), (36, 56), (46, 60), (58, 44), (66, 40)], 1.5, (120, 60, 50))
        mark(p, color)
    return d


def mark_eye(p, c):
    p.add(poly(ellpts(70, 36, 10, 7)), c, 1.4)
    p.add(poly(ellpts(70, 36, 4, 5)), INK, 0)


def mark_skull(p, c):
    p.add(poly(ellpts(70, 36, 9, 10)), c, 1.4)
    p.add(poly(ellpts(67, 35, 2, 2.5)), INK, 0)
    p.add(poly(ellpts(73, 35, 2, 2.5)), INK, 0)


def faction(p):
    w = (240, 240, 240)
    p.add(poly(ellpts(50, 50, 34, 34)), w, 0)
    p.add(poly(ellpts(50, 50, 16, 12)), (40, 40, 40), 0)
    p.add(poly(ellpts(50, 50, 6, 10)), w, 0)
    for i in range(6):
        a = math.radians(-150 + i * 24)
        p.add(tent([(50 + 30 * math.cos(a), 50 + 30 * math.sin(a)), (50 + 44 * math.cos(a), 50 + 44 * math.sin(a))], 5, 3), w, 0)


def site_beholder(p):
    p.add(poly([(6, 86), (28, 34), (46, 50), (60, 24), (94, 86)]), (100, 96, 104), OW)
    p.add(poly([(38, 86), (44, 62), (56, 58), (64, 86)]), (34, 30, 34), OW)
    p.add(poly(ellpts(51, 72, 7, 5)), BEHOLD["e"], 1.2)
    p.add(poly(ellpts(51, 72, 2.5, 3.5)), BEHOLD["i"], 0)


def site_tomb(p):
    p.add(poly([(14, 86), (14, 40), (50, 18), (86, 40), (86, 86)]), (150, 146, 138), OW)
    p.add(poly([(50, 18), (86, 40), (86, 86), (60, 86)]), (124, 120, 112), 0)
    p.add(poly([(38, 86), (38, 58), (50, 50), (62, 58), (62, 86)]), (30, 28, 30), OW)
    p.add(poly(ellpts(50, 70, 6, 6)), BONE, 1.2)
    p.add(poly(ellpts(48, 69, 1.4, 1.6)), GEM, 0)
    p.add(poly(ellpts(52, 69, 1.4, 1.6)), GEM, 0)


def knight(p):
    """预览用：死亡骑士（原版人类 + 死亡骑士铠甲）的示意剪影。"""
    steel = (70, 72, 78); steel_d = (50, 52, 58)
    p.add(poly([(36, 40), (64, 40), (68, 74), (32, 74)]), steel, OW)
    p.add(poly([(56, 40), (64, 40), (68, 74), (58, 74)]), steel_d, 0)
    p.add(poly(ellpts(50, 30, 12, 13)), steel, OW)
    p.add(poly([(42, 30), (58, 30), (58, 34), (42, 34)]), (200, 60, 50), 0)
    p.add(poly([(38, 74), (46, 74), (46, 92), (38, 92)]), steel_d, OW)
    p.add(poly([(54, 74), (62, 74), (62, 92), (54, 92)]), steel_d, OW)
    p.add(tent([(70, 44), (78, 20)], 3, 2), (180, 186, 190), OW)
    p.add(poly([(28, 40), (36, 40), (34, 30), (26, 34)]), (120, 30, 30), 1.2)


def preview():
    cell = 48
    shots = [("Behir", 2.4), ("Beholder", 2.0), ("Demilich", 1.0)]
    imgs = []
    for k, s in shots:
        im = Image.open(os.path.join(HAR_TEX, "LegendaryMonsters", k, "Naked_Hulk_east.png" if k != "Demilich" else "Naked_Hulk_south.png")).convert("RGBA")
        imgs.append(im.resize((int(1.5 * s * cell), int(1.5 * s * cell)), Image.LANCZOS))
    kp = Pic(); knight(kp); kimg = kp.img.resize((int(1.5 * cell), int(1.5 * cell)), Image.LANCZOS)
    imgs.insert(2, kimg)
    W = 120 + sum(i.width for i in imgs) + 30 * len(imgs); H = max(i.height for i in imgs) + 40
    out = Image.new("RGBA", (W, H), (236, 230, 216, 255)); d = ImageDraw.Draw(out)
    base_y = H - 20
    hx = 50
    d.ellipse((hx - 7, base_y - 40, hx + 7, base_y - 27), fill=(214, 168, 132), outline=INK, width=2)
    d.rectangle((hx - 8, base_y - 27, hx + 8, base_y - 7), fill=(110, 120, 90), outline=INK, width=2)
    d.line((hx - 5, base_y - 7, hx - 5, base_y), fill=INK, width=3); d.line((hx + 5, base_y - 7, hx + 5, base_y), fill=INK, width=3)
    x = 110
    for im in imgs:
        out.alpha_composite(im, (x, base_y - int(im.height * .95))); x += im.width + 30
    path = os.path.join(ROOT, "ArtSource", "Preview", "LegendaryMonsters-1.52.1.png")
    os.makedirs(os.path.dirname(path), exist_ok=True)
    out.convert("RGB").save(path)


draw_bodies()
icon("LegendaryMonsters/Items/BehirHorn", horn)
icon("LegendaryMonsters/Items/BeholderEye", eye)
icon("LegendaryMonsters/Items/BeholderStalk", stalk)
icon("LegendaryMonsters/Items/AntiMagicAmulet", amulet)
icon("LegendaryMonsters/Items/HellfireOrb", hellorb)
icon("LegendaryMonsters/Items/SoulGem", soulgem)
icon("LegendaryMonsters/Items/DemilichSkull", skull)
icon("LegendaryMonsters/Items/IntelBeholder", intel(BEHOLD["e"], mark_eye))
icon("LegendaryMonsters/Items/IntelTomb", intel(BONE, mark_skull))
icon("LegendaryMonsters/Faction", faction)
icon("LegendaryMonsters/SiteBeholder", site_beholder)
icon("LegendaryMonsters/SiteTomb", site_tomb)
preview()
print("ok")
