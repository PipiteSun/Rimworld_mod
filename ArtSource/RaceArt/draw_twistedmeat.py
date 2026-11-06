# 1.119.0 扭曲肉资源化：物品、建筑、仪式、按钮图标。
# 照 draw_events3.py 的画法（粗黑轮廓、平涂 + 柔和暗面、低饱和），SPic 在 100×100 画布上画。
#  - Bridges/Anomaly/Textures/TwistedMeat/*.png（物品 128px，孵化槽 256px，按钮 / 仪式图标 128px）
#  - ArtSource/Preview/TwistedMeat.png：预览
# 用法：python -I ArtSource/RaceArt/draw_twistedmeat.py <模组根目录>
import math, os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import draw_race_art as base
from draw_race_art import ell, tent, INK
from draw_dragons_1028 import SPic, poly, mix, CREAM
from PIL import Image

ROOT = sys.argv[1] if len(sys.argv) > 1 else os.getcwd()
TEX = os.path.join(ROOT, "Bridges", "Anomaly", "Textures")
REL = "TwistedMeat"
FLESH = (176, 84, 92)
FLESH_D = (128, 56, 66)
FLESH_L = (214, 138, 138)
CLAY = (160, 112, 76)
CLAY_D = (120, 82, 56)
WAX = (150, 52, 48)
BONE = (222, 212, 186)
STEEL = (132, 136, 140)
STEEL_D = (98, 100, 106)
VOID = (70, 44, 96)
VOID_L = (150, 110, 200)
GLASS = (196, 214, 222)


def bumps(p, pts, r, col):
    for x, y in pts:
        p.add(ell((x - r, y - r * .8, x + r, y + r * .8)), col, 1.4)


def jar(p, top=30, bottom=84, w=26, col=CLAY):
    body = poly([(50 - w * .7, top), (50 + w * .7, top), (50 + w, top + 14), (50 + w * .9, bottom - 6), (50, bottom), (50 - w * .9, bottom - 6), (50 - w, top + 14)])
    p.add(body, col, 2.6)
    return body


def item_paste(p):
    body = jar(p)
    p.add(("poly", [(26, 50), (74, 50), (74, 56), (26, 56)]), CLAY_D, 0, shade=False, clip=body)
    # 冒出罐口的膏体
    p.add(poly([(32, 32), (40, 22), (50, 26), (60, 18), (70, 30), (64, 36), (36, 36)]), FLESH, 2.4)
    bumps(p, [(44, 27), (58, 24)], 3.2, FLESH_L)
    # 蜡封
    p.add(ell((42, 58, 58, 72)), WAX, 2.0)
    p.add(ell((46, 62, 54, 68)), mix(WAX, CREAM, .25), 0, shade=False)


def item_salve(p):
    p.add(ell((20, 58, 80, 84)), STEEL_D, 2.6)
    p.add(("poly", [(20, 52), (80, 52), (80, 71), (20, 71)]), STEEL, 2.6)
    p.add(ell((20, 42, 80, 62)), STEEL, 2.6)
    p.add(ell((26, 45, 74, 59)), FLESH, 1.6)
    bumps(p, [(42, 51), (56, 50)], 4, FLESH_L)
    p.add(tent([(54, 36), (64, 26), (76, 22)], 7, 2), (106, 140, 76), 2.0)
    p.add(tent([(60, 30), (70, 26)], 1.2, .8), (78, 104, 56), 0, shade=False)


def bottle(p, liquid, glow=None):
    neck = ("poly", [(44, 14), (56, 14), (56, 34), (44, 34)])
    p.add(neck, GLASS, 2.4)
    p.add(("poly", [(42, 10), (58, 10), (58, 16), (42, 16)]), CLAY_D, 2.0)
    body = ell((22, 30, 78, 88))
    p.add(body, GLASS, 2.6)
    p.add(("poly", [(18, 52), (82, 52), (82, 92), (18, 92)]), liquid, 0, shade=False, clip=ell((25, 33, 75, 85)))
    if glow:
        p.add(ell((34, 58, 50, 74)), glow, 0, shade=False)
    p.add(tent([(32, 42), (30, 54)], 3, 2), (240, 246, 250), 0, shade=False)


def item_morph(p):
    bottle(p, (112, 70, 96))
    bumps(p, [(46, 66), (58, 72), (52, 60)], 2.6, (156, 104, 132))
    p.add(tent([(60, 40), (68, 30), (64, 22)], 3, 1.4), FLESH, 1.6)


def item_frenzy(p):
    body = jar(p, 36, 86, 24, CLAY)
    # 布包
    p.add(poly([(28, 46), (72, 46), (76, 58), (24, 58)]), (196, 176, 140), 2.0)
    p.add(tent([(66, 52), (80, 66), (84, 80)], 5, 2), (196, 176, 140), 2.0)
    # 瓶口冒出的红雾
    p.add(poly([(38, 30), (44, 16), (52, 22), (60, 10), (66, 26), (58, 36), (42, 36)]), (200, 70, 80), 2.0)
    p.add(ell((46, 66, 56, 76)), (226, 96, 96), 0, shade=False)


def item_eye(p):
    # 干枯的骨框
    p.add(poly([(16, 50), (30, 28), (50, 20), (70, 28), (84, 50), (70, 72), (50, 80), (30, 72)]), BONE, 2.8)
    p.add(tent([(22, 46), (14, 36), (18, 26)], 5, 2), BONE, 2.0)
    p.add(tent([(78, 54), (86, 64), (82, 74)], 5, 2), BONE, 2.0)
    p.add(ell((30, 32, 70, 68)), VOID, 2.4)
    p.add(ell((38, 40, 62, 60)), VOID_L, 0, shade=False)
    p.add(ell((45, 45, 55, 55)), (20, 14, 30), 0, shade=False)
    pts = []
    for k in range(8):
        a = math.radians(-90 + k * 45)
        r = 7 if k % 2 == 0 else 2.4
        pts.append((60 + r * math.cos(a), 40 + r * math.sin(a)))
    p.add(("poly", pts), (248, 240, 255), 0, shade=False)


def item_armor(p):
    # 浅色画（游戏里按颜色生成器染成血肉色）
    light, mid = (232, 222, 216), (206, 192, 186)
    body = poly([(30, 16), (42, 22), (58, 22), (70, 16), (84, 26), (78, 46), (74, 86), (26, 86), (22, 46), (16, 26)])
    p.add(body, light, 2.8)
    p.add(poly([(42, 22), (50, 34), (58, 22)]), mid, 2.0)
    for x, y in [(38, 46), (60, 44), (46, 62), (62, 68), (34, 72)]:
        p.add(ell((x - 6, y - 5, x + 6, y + 5)), mid, 1.6)
    p.add(("line", [(50, 36), (50, 84)], 1.4), INK, 0, shade=False)


def incubator(p):
    # 俯视的肉槽：钢框 + 一池血肉
    p.add(("poly", [(6, 10), (94, 10), (94, 92), (6, 92)]), STEEL, 3.0)
    for x, y in [(10, 14), (90, 14), (10, 88), (90, 88)]:
        p.add(ell((x - 3, y - 3, x + 3, y + 3)), STEEL_D, 1.2)
    pool = poly([(16, 22), (50, 16), (84, 22), (88, 54), (82, 82), (50, 86), (18, 82), (12, 52)])
    p.add(pool, FLESH_D, 2.4)
    p.add(poly([(22, 28), (50, 24), (78, 30), (80, 56), (74, 76), (48, 80), (24, 74), (20, 50)]), FLESH, 0)
    bumps(p, [(34, 40), (62, 36), (48, 56), (30, 64), (68, 62), (54, 72)], 6, FLESH_L)
    # 一个正在成形的小东西
    p.add(ell((40, 42, 60, 58)), (196, 108, 112), 2.0)
    p.add(ell((46, 46, 50, 50)), (40, 20, 24), 0, shade=False)
    p.add(ell((52, 46, 56, 50)), (40, 20, 24), 0, shade=False)
    p.add(tent([(14, 92), (14, 98)], 3, 3), STEEL_D, 1.6)
    p.add(tent([(86, 92), (86, 98)], 3, 3), STEEL_D, 1.6)


def wall_icon(p):
    p.add(("poly", [(14, 20), (86, 20), (86, 84), (14, 84)]), FLESH_D, 3.0)
    bumps(p, [(28, 34), (54, 30), (76, 38), (36, 56), (64, 58), (26, 74), (50, 76), (74, 74)], 9, FLESH)
    for x, y in [(30, 33), (56, 29), (38, 55), (66, 57)]:
        p.add(ell((x - 3, y - 2, x + 3, y + 2)), FLESH_L, 0, shade=False)


def ritual_icon(p):
    p.add(poly([(14, 86), (24, 64), (40, 70), (50, 56), (62, 68), (78, 60), (88, 86)]), FLESH, 2.6)
    bumps(p, [(30, 74), (52, 66), (72, 72)], 5, FLESH_L)
    p.add(ell((30, 10, 70, 46)), VOID, 2.6)
    p.add(ell((40, 18, 60, 38)), VOID_L, 0, shade=False)
    p.add(ell((46, 24, 54, 32)), (16, 10, 24), 0, shade=False)
    for a in (-60, -30, 0, 30, 60):
        x = 50 + 26 * math.sin(math.radians(a))
        p.add(("line", [(50 + 18 * math.sin(math.radians(a)), 46), (x, 54)], 1.6), VOID_L, 0, shade=False)


def g_catalysis(p):
    # 沙漏 + 往下滴的血肉
    p.add(("poly", [(22, 12), (78, 12), (78, 20), (22, 20)]), CLAY_D, 2.4)
    p.add(("poly", [(22, 80), (78, 80), (78, 88), (22, 88)]), CLAY_D, 2.4)
    p.add(("poly", [(28, 20), (72, 20), (54, 50), (72, 80), (28, 80), (46, 50)]), GLASS, 2.4)
    p.add(("poly", [(34, 22), (66, 22), (52, 44), (48, 44)]), FLESH, 0, shade=False)
    p.add(("poly", [(36, 78), (64, 78), (58, 66), (42, 66)]), FLESH, 0, shade=False)
    p.add(("line", [(50, 44), (50, 66)], 2.2), FLESH_D, 0, shade=False)


def g_regen(p):
    p.add(poly([(50, 10), (82, 22), (78, 58), (50, 90), (22, 58), (18, 22)]), (150, 156, 164), 2.8)
    p.add(poly([(50, 22), (70, 30), (68, 56), (50, 76), (32, 56), (30, 30)]), FLESH, 2.0)
    p.add(("poly", [(46, 34), (54, 34), (54, 46), (64, 46), (64, 54), (54, 54), (54, 66), (46, 66), (46, 54), (36, 54), (36, 46), (46, 46)]), FLESH_L, 1.4)


def g_nourish(p):
    p.add(poly([(28, 20), (72, 20), (80, 44), (70, 64), (30, 64), (20, 44)]), BONE, 2.6)
    p.add(ell((32, 34, 46, 46)), INK, 0, shade=False)
    p.add(ell((54, 34, 68, 46)), INK, 0, shade=False)
    p.add(ell((36, 37, 42, 43)), (160, 220, 140), 0, shade=False)
    p.add(ell((58, 37, 64, 43)), (160, 220, 140), 0, shade=False)
    p.add(poly([(24, 78), (40, 66), (60, 70), (78, 64), (82, 86), (22, 88)]), FLESH, 2.4)
    bumps(p, [(40, 76), (62, 76)], 4, FLESH_L)


def g_devour(p):
    p.add(poly([(16, 30), (50, 16), (84, 30), (78, 50), (22, 50)]), BONE, 2.6)
    p.add(poly([(22, 58), (78, 58), (72, 84), (50, 90), (28, 84)]), BONE, 2.6)
    for x in range(26, 78, 9):
        p.add(("poly", [(x, 50), (x + 6, 50), (x + 3, 58)]), (244, 238, 224), 1.0, shade=False)
    p.add(poly([(36, 50), (64, 50), (60, 60), (40, 60)]), FLESH, 1.8)


ITEMS = {"AberrantPaste": item_paste, "AberrantSalve": item_salve, "MorphDraught": item_morph, "FrenzyFlask": item_frenzy,
         "EyeOfHadar": item_eye, "AberrantHideArmor": item_armor, "FleshWall_MenuIcon": wall_icon, "RitualFarFeast": ritual_icon,
         "CatalysisGizmo": g_catalysis, "FleshRegenGizmo": g_regen, "NourishGizmo": g_nourish, "DevourGizmo": g_devour}


def draw_all():
    base.TEX = TEX
    paths = []
    p = SPic(); incubator(p); paths.append(p.save(f"{REL}/FleshIncubator", 256))
    for name, fn in ITEMS.items():
        p = SPic(); fn(p); paths.append(p.save(f"{REL}/{name}", 128))
    return paths


def preview(paths):
    cell = 150
    cols = 5
    rows = (len(paths) + cols - 1) // cols
    out = Image.new("RGBA", (20 + cols * (cell + 10), 20 + rows * (cell + 10)), (236, 230, 216, 255))
    for i, path in enumerate(paths):
        img = Image.open(path).convert("RGBA").resize((cell, cell), Image.LANCZOS)
        out.alpha_composite(img, (20 + (i % cols) * (cell + 10), 20 + (i // cols) * (cell + 10)))
    path = os.path.join(ROOT, "ArtSource", "Preview", "TwistedMeat.png")
    os.makedirs(os.path.dirname(path), exist_ok=True)
    out.convert("RGB").save(path)
    return path


if __name__ == "__main__":
    print(preview(draw_all()))
