# 1.28.0 事件贴图：照 draw_item_art.py 的手法（粗黑轮廓、平涂、单侧暗面）。
# 位面裂口建筑（2×2，紫黑旋涡）256×256；神迹残片图标 128×128；位面残渣按 Graphic_StackCount 画三张；
# 神明附体光晕：白色放射光晕 256×256（游戏里按神的颜色染色，配 MoteGlow 着色器）。
# 用法：python -I ArtSource/RaceArt/draw_events_128.py <模组根目录>
import math, os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import draw_race_art as base
from draw_race_art import Pic, poly, ell, ellpts, INK
from PIL import Image, ImageDraw, ImageFilter

ROOT = sys.argv[1] if len(sys.argv) > 1 else os.getcwd()
base.TEX = os.path.join(ROOT, "Textures")

VOID = (38, 18, 54); VOID_D = (22, 10, 34); PURPLE = (120, 56, 176); PURPLE_D = (86, 38, 132); PURPLE_L = (186, 128, 236)
GLOW = (236, 214, 255); STONE = (96, 88, 104); STONE_D = (72, 66, 80); GOLD = (236, 206, 112); GOLD_D = (196, 160, 72); GOLD_L = (255, 240, 186)


def spiral(cx, cy, r0, r1, turns, a0, steps=80):
    pts = []
    for i in range(steps + 1):
        t = i / steps
        a = a0 + turns * 2 * math.pi * t
        r = r0 + (r1 - r0) * t
        pts.append((cx + r * math.cos(a), cy + r * math.sin(a) * .82))
    return pts


def rift():
    p = Pic()
    # 地面上的碎石圈
    for a, rr, s in ((.2, 41, 6), (1.05, 37, 3.5), (1.6, 42, 5), (2.5, 39, 4), (3.3, 43, 6.5), (4.2, 38, 3.2), (4.75, 41, 4.5), (5.6, 40, 5.5)):
        x, y = 50 + rr * math.cos(a), 52 + rr * .82 * math.sin(a)
        p.add(poly([(x - s, y + s * .6), (x - s * .4, y - s), (x + s, y - s * .5), (x + s * .7, y + s * .8)]), STONE, 2.2)
        p.add(poly([(x + s * .1, y - s * .6), (x + s, y - s * .5), (x + s * .7, y + s * .8), (x, y + s * .6)]), STONE_D, 0)
    # 外层旋涡（紫）
    p.add(poly(ellpts(50, 50, 34, 28)), PURPLE, 2.8)
    p.add(poly([pt for pt in ellpts(50, 50, 34, 28) if pt[0] >= 50] + [(50, 22)]), PURPLE_D, 0)
    # 旋臂
    for k in range(4):
        arm = spiral(50, 50, 6, 32, .65, k * math.pi / 2)
        p.line(arm, 3.2, PURPLE_L if k % 2 == 0 else PURPLE_D)
    # 内层黑洞
    p.add(poly(ellpts(50, 50, 17, 14)), VOID, 2.4)
    p.add(poly(ellpts(50, 50, 9, 7)), VOID_D, 0)
    # 亮点
    for x, y, r in ((36, 36, 1.8), (64, 40, 1.4), (58, 66, 1.6), (40, 62, 1.2), (70, 56, 1.0)):
        p.add(ell((x - r, y - r, x + r, y + r)), GLOW, 0)
    return p


def shard():
    p = Pic()
    pts = [(50, 12), (66, 36), (60, 86), (40, 86), (34, 36)]
    p.add(poly(pts), GOLD, 2.8)
    p.add(poly([(50, 12), (66, 36), (60, 86), (50, 86)]), GOLD_D, 0)
    p.add(poly([(44, 34), (50, 18), (54, 34), (50, 60)]), GOLD_L, 0)
    for x, y, r in ((28, 28, 2.6), (74, 54, 2.2), (30, 70, 1.8), (70, 22, 1.6)):
        p.line([(x - r * 2, y), (x + r * 2, y)], 1.6, GOLD_L)
        p.line([(x, y - r * 2), (x, y + r * 2)], 1.6, GOLD_L)
    return p


def residue_one(p, x, y, s):
    pts = [(x + dx * s, y + dy * s) for dx, dy in ((-16, 8), (-12, -8), (0, -16), (14, -10), (16, 6), (2, 14))]
    p.add(poly(pts), PURPLE, 2.6)
    p.add(poly([(x, y - 16 * s), (x + 14 * s, y - 10 * s), (x + 16 * s, y + 6 * s), (x + 2 * s, y + 14 * s), (x, y)]), PURPLE_D, 0)
    p.add(poly([(x - 8 * s, y - 6 * s), (x - 2 * s, y - 10 * s), (x, y - 2 * s), (x - 6 * s, y + 2 * s)]), PURPLE_L, 0)


def stack(n):
    p = Pic()
    spots = {1: [(50, 58, 1.0)], 2: [(38, 62, .9), (62, 56, .9)], 3: [(34, 66, .82), (62, 66, .82), (48, 44, .82)]}[n]
    for x, y, s in spots:
        residue_one(p, x, y, s)
    return p


def halo(size=256):
    # 白色放射光晕：中心透明一圈（不盖住人），往外渐亮再渐隐，加 12 道光芒。
    img = Image.new("RGBA", (size, size), (255, 255, 255, 0))
    px = img.load()
    c = (size - 1) / 2
    for yy in range(size):
        for xx in range(size):
            dx, dy = xx - c, yy - c
            r = math.hypot(dx, dy) / c
            a = math.atan2(dy, dx)
            ring = math.exp(-((r - .62) / .16) ** 2)
            rays = max(0.0, math.cos(a * 12)) ** 6 * max(0.0, 1 - r) * 1.4 * (r > .35)
            v = min(1.0, ring * .9 + rays * .55)
            if r > 1:
                v = 0
            px[xx, yy] = (255, 255, 255, int(255 * v))
    img = img.filter(ImageFilter.GaussianBlur(1.2))
    path = os.path.join(base.TEX, "Things", "Pawn", "LP_DivineHalo.png")
    os.makedirs(os.path.dirname(path), exist_ok=True)
    img.save(path)
    return path


# ---------- 1.31.0：法师塔残骸（3×3 建筑）、龙影（地图上掠过的大轮廓） ----------
TOWER = (128, 118, 132); TOWER_D = (92, 84, 98); TOWER_L = (168, 160, 172); ROOF = (96, 40, 54); ROOF_D = (66, 26, 38)
ARC = (150, 92, 230); ARC_L = (214, 186, 255)


def tower_wreck():
    p = Pic()
    # 塌掉的塔基：一圈断墙
    p.add(poly(ellpts(50, 56, 38, 30)), TOWER_D, 2.8)
    p.add(poly(ellpts(50, 54, 33, 25)), TOWER, 2.4)
    for a in range(0, 360, 40):
        x, y = 50 + 33 * math.cos(math.radians(a)), 54 + 25 * math.sin(math.radians(a))
        p.add(poly([(x - 4, y + 3), (x - 3, y - 4), (x + 3, y - 5), (x + 4, y + 2)]), TOWER_L, 2)
    # 倒下的塔身：斜着压在塔基上，尖顶摔在一边
    body = [(26, 66), (20, 58), (62, 26), (70, 34)]
    p.add(poly(body), TOWER, 2.8)
    p.add(poly([(26, 66), (62, 30), (70, 34), (30, 70)]), TOWER_D, 0)
    for t in (.25, .5, .75):
        x0, y0 = 23 + (62 - 20) * t, 62 - (58 - 26) * t
        p.line([(x0 - 3, y0 - 4), (x0 + 4, y0 + 4)], 1.8, TOWER_D)
    p.add(poly([(62, 26), (70, 34), (86, 14)]), ROOF, 2.6)
    p.add(poly([(66, 30), (70, 34), (86, 14)]), ROOF_D, 0)
    # 散落的石块
    for x, y, s in ((18, 34, 4), (80, 62, 5), (40, 84, 4.5), (72, 80, 3.5), (12, 52, 3)):
        p.add(poly([(x - s, y + s * .6), (x - s * .4, y - s), (x + s, y - s * .5), (x + s * .7, y + s * .8)]), TOWER_L, 2)
    # 野魔法：几块发光的奥术水晶
    for x, y, h in ((44, 48, 10), (58, 62, 8), (34, 58, 6)):
        p.add(poly([(x, y - h), (x + h * .45, y), (x, y + h * .5), (x - h * .45, y)]), ARC, 2.2)
        p.add(poly([(x, y - h), (x + h * .45, y), (x, y)]), ARC_L, 0)
    for x, y, r in ((50, 40, 1.6), (64, 52, 1.2), (30, 46, 1.4), (54, 72, 1.1)):
        p.add(ell((x - r, y - r, x + r, y + r)), GLOW, 0)
    return p


def dragon_shadow(size=512):
    # 俯视的龙形剪影，头朝 +x（东）：游戏里按飞行方向转。纯黑，透明度在 Fleck 定义里调。
    k = size / 100
    img = Image.new("L", (size, size), 0)
    d = ImageDraw.Draw(img)
    P = lambda pts: [(x * k, y * k) for x, y in pts]
    # 头、颈、身体、尾巴连成一条
    d.polygon(P([(96, 50), (88, 47.5), (80, 48), (70, 46), (58, 44.5), (44, 45), (32, 47), (18, 48.5), (4, 49.6), (1, 50),
                 (4, 50.4), (18, 51.5), (32, 53), (44, 55), (58, 55.5), (70, 54), (80, 52), (88, 52.5)]), fill=255)
    for sgn in (1, -1):  # 两翼：前缘从肩膀斜伸到翼尖，后缘是一串扇形翼膜，收回到腰部
        y = lambda v: 50 - sgn * v
        d.polygon(P([(60, y(4)), (64, y(18)), (66, y(40)), (58, y(30)), (54, y(38)), (48, y(26)), (42, y(32)), (38, y(18)), (32, y(20)), (34, y(4))]), fill=255)
    # 后腿
    for sgn in (1, -1):
        d.polygon(P([(36, 50 - sgn * 4), (30, 50 - sgn * 10), (27, 50 - sgn * 10), (32, 50 - sgn * 3)]), fill=255)
    img = img.filter(ImageFilter.GaussianBlur(size / 160))
    out = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    out.putalpha(img)
    path = os.path.join(base.TEX, "Things", "Mote", "LP_DragonShadow.png")
    os.makedirs(os.path.dirname(path), exist_ok=True)
    out.save(path)
    return path


# ---------- 1.37.0：耐瑟瑞尔卷轴、耐瑟祭坛、奥术半神光环、拉洛克古墓/浮城残骸站点图标 ----------
PARCH = (222, 204, 160); PARCH_D = (188, 166, 120); ROD = (120, 86, 56); ROD_D = (90, 62, 40)
VIOLET = (150, 112, 210); VIOLET_L = (206, 184, 246); DARKP = (70, 54, 92); DARKP_D = (50, 38, 66)
SCHOOLS = {"Evocation": (204, 88, 56), "Abjuration": (92, 142, 204), "Enchantment": (198, 118, 178), "Illusion": (142, 108, 204),
           "Divination": (218, 192, 102), "Conjuration": (92, 168, 158), "Transmutation": (128, 172, 86), "Necromancy": (78, 78, 88)}


def scroll(paper=PARCH, paper_d=PARCH_D, rod=ROD, rod_d=ROD_D, runes=None, tag=None, cap=None):
    p = Pic()
    # 展开的纸面（略斜），右下暗面
    sheet = [(24, 30), (76, 26), (78, 72), (22, 76)]
    p.add(poly(sheet), paper, 2.6)
    p.add(poly([(50, 28), (76, 26), (78, 72), (50, 74)]), paper_d, 0)
    # 字行
    for i in range(5):
        y = 38 + i * 7
        p.line([(30, y + 0.4 * i * .1), (46 + (i % 2) * 6, y - .3)], 1.4, runes or (120, 100, 80))
        p.line([(54, y - .5), (70 - (i % 3) * 4, y - .9)], 1.4, runes or (120, 100, 80))
    # 上下卷轴杆
    for y0, y1 in ((22, 32), (70, 80)):
        p.add(poly([(16, y0 + 1), (84, y0 - 2), (85, y1 - 2), (17, y1 + 1)]), paper, 2.4)
        p.add(poly([(50, y0), (84, y0 - 2), (85, y1 - 2), (50, y1)]), paper_d, 0)
        for x in (12, 88):
            yy = (y0 + y1) / 2 - (1 if x > 50 else -0.5)
            p.add(ell((x - 4, yy - 4.5, x + 4, yy + 4.5)), cap or rod, 2.2)
            p.add(ell((x - 1, yy - 4, x + 4, yy + 4)), rod_d, 0)
    if tag:
        # 学派色签：垂在下卷轴右侧的布条
        p.add(poly([(62, 78), (70, 78), (70, 94), (66, 90), (62, 94)]), tag, 2.2)
        p.add(poly([(66, 78), (70, 78), (70, 94), (66, 90)]), tuple(int(c * .78) for c in tag), 0)
    return p


def karsus():
    p = scroll(paper=(196, 184, 214), paper_d=(160, 146, 182), rod=(206, 172, 92), rod_d=(164, 132, 64), runes=(112, 70, 170), cap=(236, 206, 112))
    for x, y, r in ((50, 50, 7), ):
        p.add(poly(ellpts(x, y, r, r)), VIOLET, 2.2)
        p.add(poly(ellpts(x, y, r * .5, r * .5)), VIOLET_L, 0)
    for x, y, r in ((30, 34, 1.4), (70, 62, 1.6), (64, 36, 1.2), (36, 66, 1.1)):
        p.add(ell((x - r, y - r, x + r, y + r)), VIOLET_L, 0)
    return p


def altar():
    p = Pic()
    # 八角石台
    octa = ellpts(50, 52, 44, 42, 22.5, 8)
    p.add(poly(octa), (132, 126, 140), 2.8)
    p.add(poly([pt for pt in octa if pt[0] + pt[1] >= 100] + [(50, 52)]), (100, 94, 108), 0)
    inner = ellpts(50, 52, 34, 32, 22.5, 8)
    p.add(poly(inner), (156, 150, 164), 2.2)
    # 金边
    p.line(inner + [inner[0]], 1.6, GOLD)
    # 魔法阵
    p.add(poly(ellpts(50, 52, 24, 23)), DARKP, 2.2)
    p.add(poly(ellpts(50, 52, 20, 19)), (96, 76, 128), 0)
    for k in range(8):
        a = math.radians(k * 45)
        p.line([(50 + 12 * math.cos(a), 52 + 12 * math.sin(a)), (50 + 19 * math.cos(a), 52 + 18 * math.sin(a))], 1.6, VIOLET_L)
    # 中央秘银祭柱
    p.add(poly([(42, 60), (44, 40), (56, 40), (58, 60)]), (184, 196, 210), 2.6)
    p.add(poly([(50, 40), (56, 40), (58, 60), (50, 60)]), (142, 154, 170), 0)
    p.add(poly(ellpts(50, 36, 9, 5)), GOLD, 2.4)
    p.add(poly(ellpts(50, 31, 5, 5)), VIOLET, 2.2)
    p.add(ell((48, 28, 52, 32)), VIOLET_L, 0)
    # 四角小石灯
    for x, y in ((18, 20), (82, 20), (18, 84), (82, 84)):
        p.add(poly([(x - 4, y + 3), (x - 3, y - 4), (x + 3, y - 4), (x + 4, y + 3)]), (120, 114, 128), 2)
        p.add(ell((x - 1.6, y - 3, x + 1.6, y)), VIOLET_L, 0)
    return p


def arcane_halo():
    # 头顶的金色奥术光环（侧视椭圆环 + 几颗符文星点），中间透明。
    p = Pic()
    ring_o = ellpts(50, 50, 34, 12)
    ring_i = ellpts(50, 50, 27, 7.5)
    p.add([poly(ring_o), ("-poly", ring_i)], GOLD, 2.4)
    p.add([("poly", [pt for pt in ring_o if pt[1] >= 50]), ("-poly", ring_i)], GOLD_D, 0)
    for k in range(6):
        a = math.radians(k * 60 + 15)
        x, y = 50 + 31 * math.cos(a), 50 + 10 * math.sin(a)
        p.add(ell((x - 2.2, y - 2.2, x + 2.2, y + 2.2)), VIOLET_L, 1.4)
    out = []
    for r in ("north", "south", "east"):
        out.append(p.save("Things/Pawn/LP_ArcaneHalo_%s" % r, 128))
    return out


def crypt_icon():
    p = Pic()
    p.add(poly([(14, 80), (14, 40), (50, 18), (86, 40), (86, 80)]), (128, 122, 132), 3)
    p.add(poly([(50, 18), (86, 40), (86, 80), (50, 80)]), (98, 92, 104), 0)
    p.add(poly([(40, 80), (40, 56), (50, 48), (60, 56), (60, 80)]), (40, 34, 48), 2.4)
    # 骷髅
    p.add(ell((40, 26, 60, 44)), base.BONE, 2.4)
    p.add(ell((44, 32, 49, 37)), INK, 0)
    p.add(ell((51, 32, 56, 37)), INK, 0)
    p.add(poly([(45, 42), (55, 42), (54, 47), (46, 47)]), base.BONE, 2)
    p.add(poly(ellpts(50, 62, 4, 4)), VIOLET_L, 0)
    return p


def wreck_icon():
    p = Pic()
    # 倒扣的浮城碎块：上面是断墙，下面是尖锥状岩体
    p.add(poly([(12, 46), (88, 46), (70, 70), (54, 92), (40, 74), (24, 64)]), (116, 98, 84), 3)
    p.add(poly([(50, 46), (88, 46), (70, 70), (54, 92), (50, 80)]), (88, 72, 62), 0)
    p.add(poly([(12, 46), (88, 46), (84, 40), (16, 40)]), (186, 182, 170), 2.4)
    for x, h in ((22, 14), (34, 22), (48, 10), (62, 26), (76, 16)):
        p.add(poly([(x - 4, 42), (x - 4, 42 - h), (x + 1, 40 - h - 3), (x + 4, 42 - h), (x + 4, 42)]), (206, 202, 190), 2.2)
        p.add(poly([(x, 42), (x, 41 - h), (x + 4, 42 - h), (x + 4, 42)]), (166, 162, 150), 0)
    p.add(poly(ellpts(50, 60, 6, 5)), VIOLET, 2)
    return p


def netheril_137():
    out = []
    out.append(scroll().save("Things/Item/LP_NetherilStudyScroll", 128))
    out.append(scroll(paper=(214, 200, 176), paper_d=(180, 164, 136), tag=(170, 160, 150)).save("Things/Item/LP_NetherilArcaneScroll", 128))
    for name, col in SCHOOLS.items():
        out.append(scroll(paper=(214, 200, 176), paper_d=(180, 164, 136), tag=col).save("Things/Item/LP_NetherilArcaneScroll_%s" % name, 128))
    out.append(karsus().save("Things/Item/LP_KarsusScroll", 128))
    out.append(altar().save("Things/Building/LP_NetherilAltar", 384))
    out.extend(arcane_halo())
    out.append(crypt_icon().save("World/LP_LarlochCrypt", 128))
    out.append(wreck_icon().save("World/LP_NetherilWreck", 128))
    return out

if __name__ == "__main__":
    out = []
    # 只画 1.31.0 新增的两张（不重画已有贴图）：python -I draw_events_128.py <模组根目录> 1.31.0
    # 只画 1.37.0 新增的耐瑟瑞尔贴图：python -I draw_events_128.py <模组根目录> 1.37.0
    if len(sys.argv) > 2 and sys.argv[2] == "1.37.0":
        for o in netheril_137():
            print(o)
        sys.exit(0)
    if len(sys.argv) > 2 and sys.argv[2] == "1.31.0":
        out.append(tower_wreck().save("Things/Building/LP_TowerWreck", 384))
        out.append(dragon_shadow())
        for o in out:
            print(o)
        sys.exit(0)
    out.append(rift().save("Things/Building/LP_PlanarRift", 256))
    out.append(shard().save("Items/Materials/MiracleShard", 128))
    for i, s in enumerate("abc"):
        out.append(stack(i + 1).save("Items/Materials/PlanarResidue/PlanarResidue_%s" % s, 128))
    out.append(halo())
    for o in out:
        print(o)
