# 1.19.0 派系图标与巨魔血：沿用 draw_race_art.py 的程序绘制流程（粗黑轮廓、平涂、单侧暗面）。
# 派系图标和原版一样是白色/浅灰剪影 + 黑色轮廓，游戏里按派系颜色染色；巨魔血是普通彩色物品图。
# 用法：python -I ArtSource/RaceArt/draw_factions.py <模组根目录>
import math, os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import draw_race_art as base
from draw_race_art import Pic, poly, ell, tent, arc, ellpts, mir, INK

ROOT = sys.argv[1] if len(sys.argv) > 1 else os.getcwd()
base.TEX = os.path.join(ROOT, "Textures")

W = (250, 250, 250)      # 主体（染成派系色）
L = (214, 214, 214)      # 亮面
D = (168, 168, 168)      # 暗面
DD = (120, 120, 120)     # 更深的细节
OW = 3.2                 # 图标在世界地图上很小，轮廓比基因图标粗


def shield_pts(x0=18, x1=82, y0=10, y1=92):
    cx = (x0 + x1) / 2
    pts = [(x0, y0), (x1, y0), (x1, y0 + (y1 - y0) * .45)]
    for i in range(1, 12):
        t = i / 12
        pts.append((x1 - (x1 - cx) * t ** .8, y0 + (y1 - y0) * (.45 + .55 * t ** 1.4)))
    pts.append((cx, y1))
    for i in range(11, 0, -1):
        t = i / 12
        pts.append((x0 + (cx - x0) * t ** .8, y0 + (y1 - y0) * (.45 + .55 * t ** 1.4)))
    pts.append((x0, y0 + (y1 - y0) * .45))
    return pts


def oak_leaf(cx, cy, length, ang, lobes=3, width=7):
    """橡叶：沿中脉左右各几个圆瓣。"""
    a = math.radians(ang)
    ux, uy = math.cos(a), math.sin(a)
    nx, ny = -uy, ux
    parts = []
    for i in range(lobes):
        t = (i + .7) / (lobes + .4)
        px, py = cx + ux * length * t, cy + uy * length * t
        r = width * (1 - .25 * t)
        for s in (1, -1):
            qx, qy = px + nx * r * .55 * s, py + ny * r * .55 * s
            parts.append(ell((qx - r * .62, qy - r * .62, qx + r * .62, qy + r * .62)))
    parts.append(tent([(cx, cy), (cx + ux * length, cy + uy * length)], width * .9, 2))
    return parts


def faerun_colony():
    p = Pic()
    p.add(poly(shield_pts()), W, OW)
    right = [(50, 10), (82, 10), (82, 47), (73, 70), (50, 92)]
    p.add(poly(right), L, 0)
    for s in (1, -1):
        leaves = oak_leaf(50 - 10 * s, 70, 30, -90 - 38 * s, 3, 8)
        p.add(leaves, D, 2)
        p.line([(50 - 10 * s, 70), (50 - 10 * s + 30 * math.cos(math.radians(-90 - 38 * s)) * .9, 70 + 30 * math.sin(math.radians(-90 - 38 * s)) * .9)], 1.1, INK)
    p.add(("rect", (46.5, 30, 53.5, 80)), D, 2.4)
    p.add(poly([(32, 18), (68, 18), (70, 34), (30, 34)]), W, 2.6)
    p.add(poly([(50, 18), (68, 18), (70, 34), (50, 34)]), L, 0)
    p.add(ell((46, 76, 54, 84)), D, 2)
    return p


def faerun_hostile():
    p = Pic()
    palm = [(30, 52), (70, 52), (72, 74), (62, 92), (38, 92), (28, 74)]
    fingers = [[(33, 52), (30, 30), (31, 20)], [(43, 52), (42, 22), (43, 10)], [(55, 52), (56, 20), (57, 9)], [(66, 53), (68, 28), (69, 19)]]
    thumb = [(30, 70), (19, 58), (12, 46)]
    p.add([poly(palm)] + [tent(f, 10, 8) for f in fingers] + [tent(thumb, 11, 8)], W, OW)
    p.add([poly([(56, 52), (70, 52), (72, 74), (62, 92), (52, 92), (60, 74)]), tent(fingers[3], 10, 8)], L, 0)
    # 掌心的骷髅符印
    p.add([ell((38, 58, 62, 80)), poly([(42, 74), (58, 74), (56, 86), (44, 86)])], DD, 2.2)
    p.add([ell((41.5, 65, 48.5, 72)), ell((51.5, 65, 58.5, 72))], W, 0)
    p.add(poly([(48.5, 75), (51.5, 75), (50, 78.5)]), W, 0)
    for x in (45.5, 50, 54.5):
        p.line([(x, 81), (x, 85.5)], 1.2, W)
    return p


def ember_clan():
    p = Pic()
    flame = [(50, 4), (58, 16), (66, 10), (70, 24), (78, 20), (76, 38), (68, 50), (32, 50), (24, 38), (22, 20), (30, 24), (34, 10), (42, 16)]
    p.add(poly(flame), L, OW)
    p.add(poly([(50, 18), (56, 28), (62, 26), (62, 40), (56, 50), (44, 50), (38, 40), (38, 26), (44, 28)]), W, 0)
    anvil = [(10, 46), (72, 46), (90, 50), (78, 58), (66, 60), (62, 72), (72, 86), (72, 94), (28, 94), (28, 86), (38, 72), (34, 60), (16, 56)]
    p.add(poly(anvil), W, OW)
    p.add(poly([(50, 46), (72, 46), (90, 50), (78, 58), (66, 60), (62, 72), (72, 86), (72, 94), (50, 94)]), L, 0)
    p.add(poly([(10, 46), (90, 50), (78, 58), (16, 56)]), D, 0)
    p.line([(10, 46), (72, 46), (90, 50)], 1.4, INK)
    return p


def illithid_colony():
    p = Pic()
    for t in ([(36, 62), (30, 76), (36, 86), (28, 96)], [(45, 64), (44, 80), (48, 90), (44, 98)]):
        p.add([tent(t, 8, 2.6), tent(mir(t), 8, 2.6)], L, OW)
    base.brain(p, (14, 8, 86, 68), W, D, OW)
    return p


def black_network():
    p = Pic()
    # 首尾相衔的蛇环
    pts = arc(50, 52, 33, 120, 420, 48)
    p.add(tent(pts, 13, 6), W, OW)
    p.add(tent(arc(50, 52, 30, 180, 330, 24), 4, 3), L, 0)
    for i in range(0, 40, 4):
        a = math.radians(140 + i * 7)
        x, y = 50 + 33 * math.cos(a), 52 + 33 * math.sin(a)
        p.add(ell((x - 1.6, y - 1.6, x + 1.6, y + 1.6)), D, 0)
    hx, hy = 50 + 33 * math.cos(math.radians(120)), 52 + 33 * math.sin(math.radians(120))
    head = [(hx - 12, hy - 4), (hx - 2, hy - 13), (hx + 12, hy - 9), (hx + 16, hy + 2), (hx + 6, hy + 10), (hx - 8, hy + 8)]
    p.add(poly(head), W, OW)
    p.add(poly([(hx + 2, hy - 2), (hx + 12, hy - 9), (hx + 16, hy + 2), (hx + 6, hy + 10)]), L, 0)
    p.add(ell((hx + 1, hy - 7, hx + 6, hy - 2)), INK, 0)
    p.add(tent([(hx + 16, hy + 2), (hx + 23, hy + 4), (hx + 27, hy + 1)], 1.6, 1), DD, 0)
    return p


def zealots():
    p = Pic()
    p.add(poly(shield_pts()), W, OW)
    p.add(poly([(50, 10), (82, 10), (82, 47), (73, 70), (50, 92)]), L, 0)
    flame = [(50, 18), (58, 32), (64, 28), (66, 46), (60, 62), (50, 70), (40, 62), (34, 46), (36, 28), (42, 32)]
    p.add(poly(flame), D, 2.6)
    p.add(poly([(50, 36), (55, 46), (56, 56), (50, 64), (44, 56), (45, 46)]), W, 0)
    p.add(("rect", (46, 70, 54, 86)), DD, 2.2)
    return p


def troll_blood():
    p = Pic()
    GLASS = (196, 214, 210); GREEN = (78, 128, 62); GREEN_D = (52, 92, 42); GREEN_L = (126, 170, 96); CORK = (150, 108, 70)
    body = [(30, 40), (70, 40), (78, 58), (78, 86), (70, 94), (30, 94), (22, 86), (22, 58)]
    p.add(poly(body), GLASS, 2.4)
    p.add(poly([(23, 62), (77, 62), (78, 86), (70, 93), (30, 93), (22, 86)]), GREEN, 0)
    p.add(poly([(52, 62), (77, 62), (78, 86), (70, 93), (54, 93), (60, 78)]), GREEN_D, 0)
    for x, y, r in ((36, 72, 3.2), (44, 82, 2.2), (32, 84, 1.8)):
        p.add(ell((x - r, y - r, x + r, y + r)), GREEN_L, 0)
    p.add(("rect", (40, 22, 60, 42)), GLASS, 2.4)
    p.add(("rect", (38, 12, 62, 26)), CORK, 2.4)
    p.line([(28, 48), (28, 58)], 1.6, (236, 244, 242))
    return p


ICONS = {
    "Factions/FaerunColony": faerun_colony, "Factions/FaerunHostile": faerun_hostile, "Factions/EmberClan": ember_clan,
    "Factions/IllithidColony": illithid_colony, "Factions/BlackNetwork": black_network, "Factions/Zealots": zealots,
}
ITEMS = {"Factions/Items/TrollBlood": troll_blood}

if __name__ == "__main__":
    out = []
    for rel, fn in ICONS.items():
        out.append(fn().save(rel, 128))
    for rel, fn in ITEMS.items():
        out.append(fn().save(rel, 128))
    print("\n".join(out))
