# 1.57.0 普通种族贴图：13 个异种型头像（照 draw_race_art.py 的精灵/矮人头像画法）、28 个基因图标、黑暗术、发条玩具、狗头人刺坑、预览图。
# RimWorld 风格：粗黑轮廓、平涂 + 少量暗面、低饱和。复用 draw_race_art.py 的 Pic/poly/ell/mir/arc/taper。
# 用法：python -I ArtSource/RaceArt/draw_commonraces_157.py <模组根目录>
import math, os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import draw_race_art as R
from draw_race_art import Pic, poly, ell, mir, arc, taper, INK
from PIL import Image, ImageDraw, ImageFont

ROOT = sys.argv[1] if len(sys.argv) > 1 else os.getcwd()
R.TEX = os.path.join(ROOT, "Textures")

def dk(c, f=0.8): return tuple(int(v * f) for v in c)
def lt(c, f=1.18): return tuple(min(255, int(v * f)) for v in c)

# 调色
ELF = (230, 204, 172); WOOD_SKIN = (168, 122, 88); DROW = (92, 88, 110); WHITE_HAIR = (232, 232, 236)
GOLD = (214, 184, 118); AUBURN = (128, 66, 44); DBROWN = (96, 64, 42); SANDY = (196, 160, 104); RED_HAIR = (178, 84, 52)
DWARF = (214, 158, 122); COPPER_BEARD = (176, 98, 56); HALF = (226, 182, 148); GNOME = (228, 186, 152)
DEEPG = (138, 140, 146); KOBOLD = (156, 76, 58); KOB_BELLY = (206, 156, 104); GOBLIN = (128, 150, 82); HOBGOB = (192, 104, 64)
BUGFUR = (138, 96, 62); BUGFUR_D = (106, 70, 44); STEEL = (156, 158, 154); STONE = (142, 136, 126); WOOD = (140, 98, 60)
LEAF = (112, 150, 88); VIOLET = (126, 96, 150); PURPLE_D = (70, 52, 92); RED = (166, 62, 56); BONE = (232, 222, 196)
COPPER = (184, 116, 66); GREEN_EYE = (92, 128, 80); BLUE_EYE = (96, 120, 150); YELLOW = (220, 190, 90); SKY = (150, 176, 196)

def eyes(p, y=49, col=GREEN_EYE, dx=0, brow=True, r=2.0):
    if brow:
        for s in (lambda q: q, mir):
            p.line(s([(38 - dx, y), (46 - dx, y - 1)]), 2.2)
    p.add([ell((42.5 - r - dx, y + 1, 42.5 + r - dx, y + 1 + 2 * r)), ell((57.5 - r + dx, y + 1, 57.5 + r + dx, y + 1 + 2 * r))], col, 0)

def nose_mouth(p, ny=53, my=68, w=4, big=False):
    if big:
        p.add(ell((44, ny, 56, ny + 13)), dk(GNOME, .9), 1.8)
    else:
        p.line([(50, ny), (50, ny + 8), (48, ny + 9)], 1.6)
    p.line([(50 - w, my), (50 + w, my)], 1.8)

def elf_face(skin, hair, hair_d, eye=GREEN_EYE):
    p = Pic()
    p.add(poly([(28, 30), (50, 13), (72, 30), (78, 86), (63, 82), (50, 44), (37, 82), (22, 86)]), hair)
    ear = [(32, 45), (8, 26), (21, 50), (32, 61)]
    p.add([poly(ear), poly(mir(ear))], skin)
    face = [(34, 30), (50, 24), (66, 30), (68, 52), (60, 72), (50, 79), (40, 72), (32, 52)]
    p.add(poly(face), skin)
    p.add(poly([(60, 30), (66, 30), (68, 52), (60, 72), (52, 78), (60, 60), (63, 44)]), dk(skin, .88), 0)
    p.add(poly([(31, 34), (50, 19), (69, 34), (67, 44), (57, 32), (50, 39), (43, 32), (33, 45)]), hair)
    p.add(poly([(56, 22), (69, 34), (67, 44), (60, 34)]), hair_d, 0)
    for s in (lambda q: q, mir):
        p.line(s([(38, 49), (46, 48)]), 2.2)
    p.add([ell((40.5, 49, 44.5, 53)), ell((55.5, 49, 59.5, 53))], eye, 0)
    p.line([(50, 53), (50, 61), (48, 62)], 1.6)
    p.line([(46, 68), (54, 68)], 1.8)
    return p

def woodelf():
    p = elf_face(WOOD_SKIN, AUBURN, dk(AUBURN), GREEN_EYE)
    p.add(poly([(64, 20), (78, 10), (82, 20), (74, 28)]), LEAF)          # 发间橡叶
    p.line([(66, 22), (78, 14)], 1.2, dk(LEAF, .7))
    return p

def drow():
    p = elf_face(DROW, WHITE_HAIR, (186, 186, 196), (198, 64, 58))
    p.line([(44, 73), (56, 73)], 1.4, (60, 56, 76))
    return p

def dwarf_face(skin, beard, helm=None):
    p = Pic()
    p.add([ell((23, 40, 33, 54)), ell((67, 40, 77, 54))], skin)
    p.add(ell((29, 22, 71, 66)), skin)
    if helm:
        p.add(poly([(27, 40), (29, 22), (50, 12), (71, 22), (73, 40), (63, 33), (37, 33)]), helm)
        p.add(poly([(50, 12), (54, 13), (52, 36), (48, 36), (46, 13)]), dk(helm), 0)
    else:
        p.add(poly([(28, 40), (30, 26), (50, 17), (70, 26), (72, 40), (63, 31), (37, 31)]), beard)
    p.add(poly([(27, 49), (38, 56), (50, 54), (62, 56), (73, 49), (75, 67), (67, 83), (56, 91), (50, 86), (44, 91), (33, 83), (25, 67)]), beard)
    p.add(poly([(62, 56), (73, 49), (75, 67), (67, 83), (56, 91), (62, 72)]), dk(beard), 0)
    for x in (40, 47, 54, 61):
        p.line([(x, 70), (x + 1, 82)], 1.4, dk(beard))
    p.add(poly([(37, 59), (50, 54), (63, 59), (58, 64), (50, 60), (42, 64)]), dk(beard), 1.6)
    p.line([(35, 39), (46, 42)], 3.8); p.line([(54, 42), (65, 39)], 3.8)
    p.add([ell((40, 44, 44, 48)), ell((56, 44, 60, 48))], INK, 0)
    p.add(ell((44.5, 45, 55.5, 57)), (200, 128, 104), 1.8)
    return p

def hilldwarf():
    p = dwarf_face((222, 170, 132), COPPER_BEARD, helm=STEEL)
    p.add([ell((40, 78, 46, 84)), ell((54, 78, 60, 84))], GOLD, 1.2)   # 胡须上的金扣
    return p

def round_face(skin, hair, ear_tip=0, curls=True, cheeks=True, wide=0):
    p = Pic()
    ear = [(27, 46), (17 - ear_tip, 38 - ear_tip), (21, 54), (29, 58)]
    p.add([poly(ear), poly(mir(ear))], skin)
    p.add(ell((26 - wide, 26, 74 + wide, 80)), skin)
    p.add(poly([(64, 30), (74 + wide, 52), (68, 74), (56, 80), (66, 56)]), dk(skin, .9), 0)
    if curls:
        for x, y, r in ((32, 30, 8), (42, 22, 9), (55, 21, 9), (67, 28, 8), (27, 40, 6), (73, 40, 6)):
            p.add(ell((x - r, y - r, x + r, y + r)), hair, 2)
    if cheeks:
        p.add([ell((32, 58, 40, 64)), ell((60, 58, 68, 64))], (224, 150, 130), 0)
    return p

def halfling_lightfoot():
    p = round_face(HALF, DBROWN, ear_tip=4)
    eyes(p, 46, BLUE_EYE); p.line([(50, 51), (50, 58), (48, 59)], 1.6)
    p.line(arc(50, 62, 8, 30, 150, 10), 2)
    return p

def halfling_stout():
    p = round_face((230, 190, 150), SANDY, ear_tip=2, wide=4)
    eyes(p, 46, GREEN_EYE); p.line([(50, 51), (50, 58), (48, 59)], 1.6)
    p.line(arc(50, 61, 9, 20, 160, 10), 2.2)
    p.add(poly([(26, 72), (74, 72), (70, 86), (30, 86)]), (110, 122, 84))   # 敦实领口
    return p

def gnome_face(skin, hair, extra=None, bald=False, eye=BLUE_EYE):
    p = Pic()
    ear = [(30, 44), (10, 30), (20, 52), (30, 60)]
    p.add([poly(ear), poly(mir(ear))], skin)
    p.add(ell((29, 28, 71, 80)), skin)
    p.add(poly([(62, 32), (71, 54), (64, 74), (54, 80), (64, 56)]), dk(skin, .88), 0)
    if not bald:
        for x, y, r in ((30, 32, 9), (40, 22, 10), (52, 18, 10), (64, 22, 10), (72, 32, 9)):
            p.add(ell((x - r, y - r, x + r, y + r)), hair, 2)
    eyes(p, 47, eye)
    p.add(ell((43, 52, 57, 67)), dk(skin, .92), 1.8)   # 大鼻子
    p.line([(45, 72), (55, 72)], 1.8)
    if extra: extra(p)
    return p

def rockgnome():
    def goggles(p):
        p.add(("line", [(28, 34), (72, 34)], 3.2), (90, 72, 52), 1.4)
        p.add([ell((34, 27, 46, 39)), ell((54, 27, 66, 39))], COPPER, 1.8)
        p.add([ell((37, 30, 43, 36)), ell((57, 30, 63, 36))], SKY, 0)
    return gnome_face(GNOME, RED_HAIR, goggles)

def forestgnome():
    def cap(p):
        p.add(poly([(32, 26), (50, 2), (68, 26), (50, 30)]), (118, 86, 54))
        p.add(poly([(28, 24), (72, 24), (70, 31), (30, 31)]), (88, 62, 38))
        p.add(poly([(62, 12), (74, 6), (76, 14), (66, 18)]), LEAF)
    return gnome_face(GNOME, DBROWN, cap, eye=GREEN_EYE)

def deepgnome():
    p = gnome_face(DEEPG, None, bald=True, eye=(200, 200, 210))
    p.line([(36, 36), (44, 38)], 1.2, dk(DEEPG, .7)); p.line([(56, 38), (64, 36)], 1.2, dk(DEEPG, .7))
    return p

def halfelf():
    p = Pic()
    p.add(poly([(27, 32), (50, 15), (73, 32), (74, 62), (66, 50), (34, 50), (26, 62)]), DBROWN)
    ear = [(30, 46), (16, 34), (22, 52), (30, 60)]
    p.add([poly(ear), poly(mir(ear))], (226, 190, 160))
    face = [(32, 30), (50, 25), (68, 30), (69, 54), (62, 74), (50, 81), (38, 74), (31, 54)]
    p.add(poly(face), (226, 190, 160))
    p.add(poly([(60, 30), (68, 30), (69, 54), (62, 74), (52, 80), (62, 60), (64, 44)]), dk((226, 190, 160), .88), 0)
    p.add(poly([(30, 36), (50, 20), (70, 36), (68, 42), (52, 30), (34, 44)]), DBROWN)
    eyes(p, 48, (120, 96, 60)); p.line([(50, 53), (50, 61), (48, 62)], 1.6)
    p.line(arc(50, 64, 7, 40, 140, 8), 1.8)
    return p

def kobold():
    p = Pic()
    for s in (lambda q: q, mir):                                        # 小角
        p.add(poly(s([(36, 26), (28, 8), (42, 22)])), (220, 206, 176))
    p.add(poly([(28, 30), (50, 20), (72, 30), (76, 50), (88, 60), (86, 72), (64, 78), (40, 78), (24, 64), (24, 44)]), KOBOLD)
    p.add(poly([(52, 64), (86, 64), (86, 72), (64, 78), (50, 76)]), KOB_BELLY, 1.6)          # 下颌
    p.add(poly([(64, 30), (72, 30), (76, 50), (66, 46)]), dk(KOBOLD, .85), 0)
    p.add(ell((38, 38, 50, 50)), YELLOW, 1.8); p.add(ell((42, 39, 46, 49)), INK, 0)
    p.add([ell((78, 57, 81, 60))], INK, 0)                                                     # 鼻孔
    for x in (58, 66, 74):
        p.add(poly([(x, 64), (x + 3, 70), (x + 6, 64)]), BONE, 1)
    for x, y in ((30, 56), (36, 64), (44, 30), (58, 26)):
        p.add(ell((x, y, x + 5, y + 4)), dk(KOBOLD, .8), 0)                                 # 鳞片
    return p

def goblin():
    p = Pic()
    ear = [(30, 42), (2, 28), (14, 48), (30, 58)]
    p.add([poly(ear), poly(mir(ear))], GOBLIN)
    p.add(ell((28, 26, 72, 80)), GOBLIN)
    p.add(poly([(62, 30), (72, 52), (64, 74), (54, 80), (64, 56)]), dk(GOBLIN, .85), 0)
    for s in (lambda q: q, mir):
        p.line(s([(36, 42), (46, 47)]), 3)
    p.add([ell((39, 47, 46, 54)), ell((54, 47, 61, 54))], YELLOW, 1.4)
    p.add([ell((41.5, 48.5, 43.5, 52.5)), ell((56.5, 48.5, 58.5, 52.5))], INK, 0)
    p.add(poly([(46, 55), (54, 55), (56, 64), (50, 66), (44, 64)]), dk(GOBLIN, .9), 1.6)
    p.add(poly([(38, 70), (62, 70), (58, 74), (42, 74)]), (60, 40, 36), 1.6)
    for x in (42, 50, 56):
        p.add(poly([(x, 70), (x + 2, 74), (x + 4, 70)]), BONE, 0)
    return p

def hobgoblin():
    p = Pic()
    ear = [(30, 42), (14, 28), (20, 50), (30, 58)]
    p.add([poly(ear), poly(mir(ear))], HOBGOB)
    p.add(poly([(32, 26), (50, 20), (68, 26), (72, 50), (66, 74), (50, 84), (34, 74), (28, 50)]), HOBGOB)
    p.add(poly([(62, 28), (68, 26), (72, 50), (66, 74), (56, 82), (64, 58)]), dk(HOBGOB, .85), 0)
    p.add(poly([(30, 34), (50, 16), (70, 34), (62, 30), (50, 26), (38, 30)]), (40, 36, 34))
    p.add(ell((44, 4, 56, 18)), (40, 36, 34))                                                # 发髻
    for s in (lambda q: q, mir):
        p.line(s([(35, 44), (46, 47)]), 3.4)
    p.add([ell((40, 47, 45, 52)), ell((55, 47, 60, 52))], YELLOW, 0)
    p.line([(50, 54), (50, 63), (47, 64)], 1.8)
    p.line([(42, 72), (58, 72)], 2.4)
    for s in (lambda q: q, mir):
        p.add(poly(s([(43, 72), (45, 66), (47, 72)])), BONE, 1.2)
    return p

def bugbear():
    p = Pic()
    p.add([ell((14, 18, 32, 36)), ell((68, 18, 86, 36))], BUGFUR)
    p.add(ell((20, 20, 80, 86)), BUGFUR)
    p.add(poly([(64, 26), (80, 52), (72, 78), (58, 86), (70, 56)]), BUGFUR_D, 0)
    for x, y in ((22, 50), (24, 64), (76, 50), (74, 64), (34, 24), (60, 22)):
        p.line([(x, y), (x + (3 if x < 50 else -3), y + 6)], 1.4, BUGFUR_D)
    p.add(ell((34, 54, 66, 80)), (176, 140, 104), 1.8)                                       # 口鼻
    p.add(ell((44, 54, 56, 63)), INK, 0)
    p.line([(42, 72), (58, 72)], 2)
    p.add([ell((36, 40, 43, 47)), ell((57, 40, 64, 47))], YELLOW, 1.4)
    p.add([ell((38.5, 41, 40.5, 46)), ell((59.5, 41, 61.5, 46))], INK, 0)
    return p

# ---------------- 基因图标 ----------------
def star(cx, cy, r1, r2, n, rot=-90):
    return poly([(cx + (r1 if i % 2 == 0 else r2) * math.cos(math.radians(rot + i * 180 / n)), cy + (r1 if i % 2 == 0 else r2) * math.sin(math.radians(rot + i * 180 / n))) for i in range(2 * n)])

def figure(p, cx, cy, sc, col):
    p.add([ell((cx - 7 * sc, cy - 7 * sc, cx + 7 * sc, cy + 7 * sc))], col)
    p.add(poly([(cx - 11 * sc, cy + 9 * sc), (cx + 11 * sc, cy + 9 * sc), (cx + 13 * sc, cy + 34 * sc), (cx - 13 * sc, cy + 34 * sc)]), col)

def g_highelf():
    p = Pic()
    p.add(poly([(18, 70), (24, 34), (38, 54), (50, 24), (62, 54), (76, 34), (82, 70)]), GOLD)
    p.add(poly([(18, 70), (82, 70), (80, 80), (20, 80)]), dk(GOLD, .85))
    p.add(poly([(50, 44), (57, 54), (50, 64), (43, 54)]), (120, 170, 190), 1.8)
    for x in (24, 76):
        p.add(ell((x - 4, 30, x + 4, 38)), (190, 220, 230), 1.4)
    return p

def g_woodelf():
    p = Pic()
    p.add(poly(taper([(30, 82), (40, 60), (52, 42), (68, 22)], 2, 2)), WOOD, 1.6)
    p.add(poly([(68, 22), (60, 22), (72, 10), (76, 26)]), STEEL, 1.6)
    leaf = [(24, 40), (36, 26), (46, 22), (50, 36), (44, 52), (30, 58)]
    p.add(poly(leaf), LEAF); p.line([(26, 56), (46, 26)], 1.4, dk(LEAF, .7))
    leaf2 = [(54, 74), (64, 58), (78, 54), (82, 68), (72, 80), (58, 84)]
    p.add(poly(leaf2), dk(LEAF, 1.05)); p.line([(56, 82), (80, 58)], 1.4, dk(LEAF, .7))
    return p

def g_sunlight():
    p = Pic()
    p.add(star(50, 46, 40, 24, 12), (222, 190, 96))
    p.add(ell((26, 22, 74, 70)), (236, 206, 112))
    p.add(poly([(28, 48), (50, 36), (72, 48), (50, 58)]), BONE, 1.8)
    p.line([(30, 48), (70, 48)], 2.4)                                                        # 眯起的眼
    for x in (36, 44, 56, 64):
        p.line([(x, 50), (x - 2, 58)], 1.6)
    p.add(ell((38, 72, 62, 90)), DROW, 2)
    return p

def g_poison():
    p = Pic()
    p.add(poly([(48, 8), (58, 14), (40, 66), (34, 64)]), (196, 200, 206))
    p.add(poly([(28, 60), (50, 70), (46, 74), (24, 64)]), (80, 68, 60))
    p.add(poly(taper([(32, 70), (24, 86)], 6, 6)), (60, 48, 44))
    for x, y in ((50, 30), (46, 44)):
        p.add(ell((x - 3, y, x + 3, y + 8)), (122, 200, 96), 1.2)
    p.add(ell((60, 56, 84, 80)), PURPLE_D)                                                    # 蜘蛛
    for a in (200, 230, 310, 340, 160, 130, 50, 20):
        r = math.radians(a); p.line([(72, 68), (72 + 18 * math.cos(r), 68 + 18 * math.sin(r))], 2, PURPLE_D)
    p.add(ell((64, 60, 80, 76)), VIOLET, 1.6)
    return p

def g_darkness():
    p = Pic()
    for r, c in ((44, (78, 64, 104)), (34, (46, 38, 62)), (24, (24, 20, 32))):
        p.add(ell((50 - r, 50 - r, 50 + r, 50 + r)), c, 2 if r == 44 else 0)
    for a in (20, 140, 260):
        p.add(("line", arc(50, 50, 38, a, a + 70), 3), (150, 120, 190), 0)
    p.add([ell((40, 46, 46, 52)), ell((54, 46, 60, 52))], (200, 80, 72), 0)
    return p

def g_armor():
    p = Pic()
    p.add(poly([(22, 22), (40, 16), (50, 24), (60, 16), (78, 22), (82, 52), (70, 82), (30, 82), (18, 52)]), STEEL)
    p.add(poly([(50, 24), (60, 16), (78, 22), (82, 52), (70, 82), (54, 82)]), dk(STEEL, .82), 0)
    p.line([(50, 26), (50, 80)], 1.8)
    p.add(poly([(60, 60), (88, 60), (88, 52), (98, 66), (88, 80), (88, 72), (60, 72)]), (214, 184, 118), 1.8)   # 前进箭头
    return p

def g_tough():
    p = Pic()
    p.add(poly([(20, 18), (80, 18), (80, 50), (66, 76), (50, 88), (34, 76), (20, 50)]), STONE)
    p.add(poly([(50, 18), (80, 18), (80, 50), (66, 76), (50, 88)]), dk(STONE, .85), 0)
    p.add(poly([(50, 70), (32, 52), (34, 40), (42, 36), (50, 44), (58, 36), (66, 40), (68, 52)]), RED, 1.8)
    return p

def g_grudge():
    p = Pic()
    p.add(poly([(16, 26), (50, 20), (84, 26), (84, 82), (50, 76), (16, 82)]), (122, 84, 56))
    p.add(poly([(20, 28), (48, 23), (48, 72), (20, 78)]), BONE, 1.4)
    p.add(poly([(52, 23), (80, 28), (80, 78), (52, 72)]), dk(BONE, .92), 1.4)
    for y in (34, 42, 50, 58):
        p.line([(24, y), (44, y - 2)], 1.2, (120, 110, 96)); p.line([(56, y - 2), (76, y)], 1.2, (120, 110, 96))
    p.add(("line", [(56, 38), (76, 62)], 4), RED, 0); p.add(("line", [(76, 38), (56, 62)], 4), RED, 0)
    p.add(poly([(44, 76), (52, 76), (52, 94), (48, 88), (44, 94)]), RED, 1.4)
    return p

def g_halflingblood():
    p = Pic()
    p.add(ell((14, 46, 86, 78)), (200, 150, 92))
    p.add(ell((14, 40, 86, 70)), (226, 178, 112))
    p.add(poly([(50, 55), (86, 52), (84, 64)]), (150, 60, 70), 0)                              # 切开的馅
    for x, y in ((30, 50), (42, 46), (56, 47), (36, 58)):
        p.line([(x, y), (x + 6, y + 2)], 1.6, dk((226, 178, 112), .7))
    figure(p, 50, 18, .55, (96, 112, 70))
    return p

def g_luck():
    p = Pic()
    for cx, cy in ((50, 30), (70, 50), (50, 70), (30, 50)):
        p.add([ell((cx - 15, cy - 15, cx + 15, cy + 15))], LEAF)
    p.add(ell((40, 40, 60, 60)), dk(LEAF, .85), 0)
    p.add(poly(taper([(52, 62), (60, 80), (66, 92)], 4, 3)), dk(LEAF, .8), 1.4)
    p.add(star(76, 22, 10, 4, 4), (236, 214, 120))
    return p

def g_hide():
    p = Pic()
    figure(p, 60, 16, 1.6, (110, 116, 124))
    p.add(poly([(28, 52), (52, 52), (52, 90), (28, 90)]), (152, 140, 104))
    figure(p, 36, 44, .8, (186, 156, 120))
    p.add(("line", [(4, 30), (24, 40)], 2.6), (200, 80, 60), 0)
    p.add(poly([(24, 40), (16, 40), (22, 34)]), (200, 80, 60), 0)
    return p

def g_stout():
    p = Pic()
    p.add(poly([(24, 26), (68, 26), (66, 86), (26, 86)]), WOOD)
    p.add(poly([(54, 26), (68, 26), (66, 86), (56, 86)]), dk(WOOD, .8), 0)
    for y in (36, 74):
        p.add(("line", [(24, y), (68, y)], 4), STEEL, 1.2)
    p.add(poly([(20, 18), (72, 18), (70, 30), (22, 30)]), BONE)
    p.add(("line", arc(70, 56, 16, -80, 80), 6), WOOD, 1.8)
    return p

def g_gnomeblood():
    p = Pic()
    R.brain(p, (16, 22, 72, 74))
    p.add(star(72, 70, 20, 14, 8), COPPER); p.add(ell((65, 63, 79, 77)), dk(COPPER, .8), 1.4)
    return p

def gear(p, cx, cy, r, col):
    p.add(star(cx, cy, r, r * .78, 9), col)
    p.add(ell((cx - r * .4, cy - r * .4, cx + r * .4, cy + r * .4)), dk(col, .7), 1.4)

def g_tinker():
    p = Pic()
    gear(p, 40, 56, 28, COPPER); gear(p, 72, 30, 18, (176, 160, 110))
    p.add(poly(taper([(20, 86), (66, 40)], 7, 7)), STEEL, 1.8)
    p.add(poly([(62, 30), (78, 30), (80, 46), (72, 40), (66, 46)]), STEEL, 1.8)
    return p

def g_beasts():
    p = Pic()
    p.add(ell((14, 40, 58, 84)), (164, 110, 70))                                                 # 松鼠
    p.add(poly([(24, 44), (20, 26), (32, 40)]), (164, 110, 70)); p.add(poly([(48, 44), (52, 26), (40, 40)]), (164, 110, 70))
    p.add(ell((26, 54, 32, 60)), INK, 0); p.add(ell((40, 54, 46, 60)), INK, 0)
    p.add(ell((33, 64, 39, 68)), (90, 56, 40), 0)
    p.add(poly([(54, 14), (90, 14), (90, 40), (70, 40), (60, 50), (62, 40), (54, 40)]), BONE)   # 对话气泡
    for x in (64, 72, 80):
        p.add(ell((x - 2.5, 25, x + 2.5, 30)), (120, 110, 96), 0)
    return p

def g_camo():
    p = Pic()
    p.add(poly([(12, 80), (20, 46), (40, 28), (66, 26), (84, 44), (90, 80)]), STONE)
    p.add(poly([(66, 26), (84, 44), (90, 80), (64, 80), (74, 54)]), dk(STONE, .82), 0)
    for x, y in ((28, 60), (60, 66)):
        p.line([(x, y), (x + 10, y - 4)], 1.4, dk(STONE, .7))
    p.add([ell((38, 46, 47, 54)), ell((53, 46, 62, 54))], (236, 236, 228), 1.4)
    p.add([ell((41, 48, 45, 53)), ell((56, 48, 60, 53))], INK, 0)
    return p

def g_halfelfblood():
    p = Pic()
    p.add(poly(taper([(8, 66), (30, 58), (50, 52)], 16, 14)), (226, 190, 160))
    p.add(poly(taper([(92, 66), (70, 58), (50, 52)], 16, 14)), (196, 160, 130))
    p.add(ell((40, 42, 60, 62)), (214, 176, 146), 1.8)
    p.add(poly([(16, 30), (4, 12), (24, 22)]), (226, 190, 160))      # 人类耳 / 尖耳
    p.add(poly([(84, 30), (98, 6), (76, 22)]), (196, 160, 130))
    p.add(star(50, 22, 10, 4, 4), GOLD)
    return p

def g_koboldblood():
    p = Pic()
    for i, x in enumerate((26, 50, 74)):
        p.add(poly(taper([(x, 30), (x + 4, 58), (x - 2, 84)], 12, 2)), (226, 214, 180))
    p.add(poly([(10, 14), (90, 14), (86, 40), (14, 40)]), KOBOLD)
    for x in (22, 40, 58, 76):
        p.add(poly([(x, 16), (x + 9, 26), (x, 36), (x - 9, 26)]), dk(KOBOLD, .82), 1.2)
    return p

def g_pack():
    p = Pic()
    for ang in (-150, -90, -30):
        a = math.radians(ang); c, s = math.cos(a), math.sin(a)
        x0, y0 = 50 + 46 * c, 66 + 46 * s; bx, by = 50 + 22 * c, 66 + 22 * s; tx, ty = 50 + 10 * c, 66 + 10 * s
        p.add(poly(taper([(x0, y0), (bx, by)], 5, 5)), WOOD, 1.6)
        p.add(poly([(tx, ty), (bx + 7 * s, by - 7 * c), (bx - 7 * s, by + 7 * c)]), STEEL, 1.4)
    p.add(ell((40, 58, 60, 78)), RED, 1.8)
    return p

def g_trap():
    p = Pic()
    p.add(ell((10, 50, 90, 90)), (86, 66, 48))
    p.add(ell((20, 58, 80, 84)), (52, 40, 30), 0)
    for x in (28, 40, 52, 64, 74):
        p.add(poly([(x - 5, 76), (x, 40 + (x % 3) * 4), (x + 5, 76)]), (196, 166, 110), 1.4)
    p.add(poly(taper([(66, 26), (88, 10)], 6, 6)), STEEL, 1.6)
    p.add(poly([(60, 22), (72, 34), (64, 38), (54, 28)]), WOOD, 1.6)
    return p

def g_servant():
    p = Pic()
    head = [(8, 40), (30, 22), (58, 18), (80, 28), (94, 46), (84, 56), (60, 54), (40, 64), (22, 62)]
    p.add(poly(head), (158, 52, 46))
    for s, e in (((40, 22), (30, 4)), ((56, 18), (54, 0))):
        p.add(poly([(s[0] - 5, s[1] + 2), e, (s[0] + 6, s[1])]), BONE, 1.6)
    p.add(ell((54, 30, 64, 38)), YELLOW, 1.2); p.add(ell((57, 30, 60, 38)), INK, 0)
    p.add(poly([(60, 54), (84, 56), (78, 62)]), BONE, 1)
    figure(p, 38, 70, .55, KOBOLD)
    p.add(star(60, 82, 8, 3, 4), GOLD)
    return p

def g_goblinoid():
    p = Pic()
    ear = [(70, 46), (96, 20), (86, 50), (70, 64)]
    p.add(poly(ear), GOBLIN)
    p.add(ell((14, 30, 74, 90)), GOBLIN)
    p.add(poly([(56, 34), (74, 56), (66, 84), (54, 90), (64, 60)]), dk(GOBLIN, .85), 0)
    p.add(star(28, 26, 18, 6, 6), (196, 226, 240))
    p.add(ell((36, 54, 44, 62)), YELLOW, 1.2)
    return p

def g_fury():
    p = Pic()
    figure(p, 70, 10, 2.0, (100, 104, 112))
    p.add(ell((14, 44, 50, 80)), GOBLIN)
    for s, e in (((20, 54), (32, 60)), ((44, 54), (32, 60))):
        p.line([s, e], 3)
    p.add(("line", [(30, 70), (36, 70)], 2.4), INK, 0)
    p.add(poly([(34, 20), (48, 36), (40, 36), (40, 44), (28, 44), (28, 36), (20, 36)]), RED, 1.8)
    return p

def g_nimble():
    p = Pic()
    for y, x0 in ((30, 4), (50, 0), (70, 6)):
        p.add(("line", [(x0, y), (x0 + 26, y)], 4), (214, 208, 160), 0)
    p.add(ell((52, 12, 68, 28)), GOBLIN)
    p.add(poly([(46, 30), (66, 30), (70, 56), (50, 58)]), (110, 92, 60))
    p.add(poly(taper([(52, 56), (38, 70), (30, 88)], 7, 6)), GOBLIN, 1.8)
    p.add(poly(taper([(64, 56), (76, 72), (90, 76)], 7, 6)), GOBLIN, 1.8)
    p.add(poly(taper([(50, 34), (36, 46)], 6, 5)), GOBLIN, 1.6)
    return p

def g_numbers():
    p = Pic()
    for i, x in enumerate((22, 50, 78)):
        h = 70 if i != 1 else 82
        p.add(poly(taper([(x, 92), (x, 92 - h)], 3.4, 3.4)), WOOD, 1.4)
        p.add(poly([(x - 14, 100 - h), (x + 14, 100 - h), (x + 14, 124 - h), (x, 118 - h), (x - 14, 124 - h)]), RED if i == 1 else (166, 96, 60))
    p.add(star(50, 30, 7, 3, 4), GOLD)
    return p

def g_discipline():
    p = Pic()
    p.add(poly(taper([(50, 94), (50, 8)], 4, 4)), WOOD, 1.6)
    p.add(poly([(26, 16), (74, 16), (74, 58), (50, 50), (26, 58)]), RED)
    p.add(poly([(50, 16), (74, 16), (74, 58), (50, 50)]), dk(RED, .82), 0)
    p.add(poly([(38, 26), (62, 26), (58, 40), (42, 40)]), INK, 0)
    p.add(ell((44, 4, 56, 14)), GOLD, 1.4)
    return p

def g_surprise():
    p = Pic()
    p.add(ell((4, 10, 70, 76)), (42, 40, 48), 0)
    for i in range(3):
        x = 36 + i * 14
        p.add(poly(taper([(x, 18), (x + 18, 60), (x + 26, 92)], 2, 7)), (214, 208, 196), 1.6)
    p.add(poly([(74, 10), (88, 10), (84, 50), (78, 50)]), YELLOW, 1.6)
    p.add(ell((76, 56, 86, 66)), YELLOW, 1.6)
    p.add([ell((20, 36, 28, 42)), ell((34, 36, 42, 42))], YELLOW, 0)
    return p

def g_limbs():
    p = Pic()
    p.add(poly(taper([(12, 84), (34, 60), (64, 40), (80, 28)], 14, 9)), BUGFUR, 1.8)
    for d in (-8, 0, 8):
        p.add(poly(taper([(80 + d * .4, 28 + d * .3), (92, 16 + d)], 4, 2)), (226, 214, 180), 1.2)
    p.add(("line", [(18, 30), (64, 12)], 3), (214, 208, 160), 0)
    p.add(poly([(64, 12), (54, 8), (58, 20)]), (214, 208, 160), 0)
    return p

# ---------------- 物品 ----------------
def toy():
    p = Pic()
    p.add(poly([(14, 78), (86, 78), (80, 92), (20, 92)]), WOOD)
    p.add(poly([(50, 78), (86, 78), (80, 92), (50, 92)]), dk(WOOD, .8), 0)
    p.add(ell((24, 34, 76, 80)), COPPER)
    p.add(poly([(50, 36), (76, 52), (74, 74), (50, 80)]), dk(COPPER, .82), 0)
    p.line([(50, 36), (50, 80)], 1.8)
    p.add(ell((38, 20, 62, 40)), (150, 96, 56))
    p.add([ell((41, 26, 47, 32)), ell((53, 26, 59, 32))], (150, 210, 220), 1.2)
    gear(p, 76, 30, 10, (204, 184, 120))
    p.add(poly(taper([(76, 30), (90, 14)], 3, 3)), STEEL, 1.2)
    for a in (210, 240, 300, 330):
        r = math.radians(a); p.line([(50 + 26 * math.cos(r), 66 + 10 * math.sin(r) + 8), (50 + 34 * math.cos(r), 66 + 16 * math.sin(r) + 10)], 2)
    return p

def spikepit():
    p = Pic()
    p.add(ell((6, 18, 94, 92)), (92, 74, 50))
    p.add(ell((16, 28, 84, 84)), (44, 34, 26), 0)
    for x, y in ((30, 50), (44, 40), (58, 46), (70, 58), (38, 66), (54, 66), (64, 74), (28, 62)):
        p.add(poly([(x - 4, y + 10), (x, y - 8), (x + 4, y + 10)]), (204, 172, 116), 1.2)
    for x0, y0, x1, y1 in ((10, 30, 40, 22), (60, 86, 92, 76), (8, 70, 22, 84)):
        p.add(poly(taper([(x0, y0), (x1, y1)], 4, 3)), (112, 150, 88), 1.2)
    return p

GENES = {"HighElfLineage": g_highelf, "WoodElfLineage": g_woodelf, "DrowSunlight": g_sunlight, "DrowPoison": g_poison, "DrowDarkness": g_darkness,
         "DwarfArmorTraining": g_armor, "DwarfToughness": g_tough, "DwarfGrudge": g_grudge,
         "HalflingBlood": g_halflingblood, "HalflingLuck": g_luck, "HalflingHideBehind": g_hide, "HalflingStout": g_stout,
         "GnomeBlood": g_gnomeblood, "RockGnomeTinker": g_tinker, "GnomeSmallBeasts": g_beasts, "DeepGnomeCamouflage": g_camo,
         "HalfElfBlood": g_halfelfblood, "KoboldBlood": g_koboldblood, "KoboldPackTactics": g_pack, "KoboldTrapsmith": g_trap, "KoboldDragonServant": g_servant,
         "GoblinoidBlood": g_goblinoid, "GoblinFury": g_fury, "GoblinNimble": g_nimble, "HobgoblinNumbers": g_numbers, "HobgoblinDiscipline": g_discipline,
         "BugbearSurprise": g_surprise, "BugbearLongLimbs": g_limbs}
XENOS = {"WoodElf": woodelf, "Drow": drow, "HillDwarf": hilldwarf, "HalflingLightfoot": halfling_lightfoot, "HalflingStout": halfling_stout,
         "RockGnome": rockgnome, "ForestGnome": forestgnome, "DeepGnome": deepgnome, "HalfElf": halfelf, "Kobold": kobold,
         "Goblin": goblin, "Hobgoblin": hobgoblin, "Bugbear": bugbear}
LABELS = {"WoodElf": "木精灵", "Drow": "卓尔精灵", "HillDwarf": "丘陵矮人", "HalflingLightfoot": "轻足半身人", "HalflingStout": "敦实半身人",
          "RockGnome": "岩侏儒", "ForestGnome": "森林侏儒", "DeepGnome": "地侏", "HalfElf": "半精灵", "Kobold": "狗头人", "Goblin": "地精",
          "Hobgoblin": "大地精", "Bugbear": "熊地精"}

def font(sz):
    for f in (r"C:\Windows\Fonts\msyh.ttc", r"C:\Windows\Fonts\simhei.ttf"):
        if os.path.exists(f): return ImageFont.truetype(f, sz)
    return ImageFont.load_default()

if __name__ == "__main__":
    tiles = []
    for n, fn in XENOS.items():
        pic = fn(); pic.save("CommonRaces/Xenotypes/" + n, 256); tiles.append((LABELS[n], pic.img.resize((128, 128), Image.LANCZOS)))
    gtiles = []
    for n, fn in GENES.items():
        pic = fn(); pic.save("CommonRaces/Genes/" + n, 256); gtiles.append((n, pic.img.resize((96, 96), Image.LANCZOS)))
    g_darkness().save("CommonRaces/Abilities/DrowDarkness", 256)
    t = toy(); t.save("CommonRaces/Things/ClockworkToy", 128); s = spikepit(); s.save("CommonRaces/Things/KoboldSpikePit", 128)
    # 预览：头像一行 13 个 + 基因两行 14 个 + 物品
    W = 14 * 130 + 30; H = 40 + 156 + 2 * 126 + 150
    sheet = Image.new("RGBA", (W, H), (46, 48, 52, 255)); d = ImageDraw.Draw(sheet); f = font(18); fs = font(13)
    d.text((10, 8), "DND边缘大陆 1.57.0 普通种族：13 个新异种型 · 28 个基因 · 黑暗术 · 发条玩具 · 狗头人刺坑", fill=(230, 226, 210, 255), font=f)
    for i, (lab, im) in enumerate(tiles):
        x = 10 + i * 136; sheet.paste(im, (x + 4, 40), im); d.text((x + 10, 172), lab, fill=(220, 220, 210, 255), font=f)
    for i, (lab, im) in enumerate(gtiles):
        r, c = divmod(i, 14); x = 10 + c * 130; y = 204 + r * 126
        sheet.paste(im, (x + 14, y), im); d.text((x + 4, y + 98), lab, fill=(190, 190, 180, 255), font=fs)
    y = 204 + 2 * 126 + 6
    for j, (lab, im) in enumerate((("黑暗术", g_darkness().img.resize((110, 110), Image.LANCZOS)), ("发条玩具", t.img.resize((110, 110), Image.LANCZOS)), ("狗头人刺坑", s.img.resize((110, 110), Image.LANCZOS)))):
        x = 10 + j * 150; sheet.paste(im, (x, y), im); d.text((x + 20, y + 112), lab, fill=(220, 220, 210, 255), font=f)
    out = os.path.join(ROOT, "ArtSource", "CommonRaces-1.57.0-preview.png"); sheet.save(out)
    print("wrote", len(XENOS), "xenotype icons,", len(GENES), "gene icons, ability, 2 things; preview", out)
