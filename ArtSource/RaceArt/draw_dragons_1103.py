# 1103 五色真龙身体重画：参考 Dragons Descent / Isekai Creatures 龙的画法（只看风格，不拿文件）——
# 伏地抬颈、头吻修长、侧面翅膀收在背上、正 / 背面翅膀半张向上；身上有鳞纹、腹甲横纹、翼膜带翼骨。
# 仍是 RimWorld 风格：粗黑轮廓、平涂 + 柔和暗面、低饱和；每色保留 1028 的角 / 背鳍 / 花纹特征。
#  - 只重画 Bridges/HAR/Textures/ChromaticDragons/<色>/Naked_Hulk_{south,east,north}.png（256px；east 朝右，west 由游戏镜像）。
#  - 头贴图（透明）、物品图标、定义都不动；龙仍在画布中央、贴近底部，游戏里的缩放 / 偏移不用改。三个年龄档共用贴图（只换体型）。
#  - 旧图备份 ArtSource/backup-dragon-art-1103；预览 ArtSource/Preview/ChromaticDragons-1103.png。
# 注意：模组里现在用的是 Peter 自己给的龙图（ArtSource/RaceArt/PeterDragons-1103，原样拷进 Naked_Hulk_*），
#       本脚本画的版本备份在 ArtSource/backup-dragon-art-1103b；别直接往模组根目录重跑，会覆盖 Peter 的图。
# 用法：python -I ArtSource/RaceArt/draw_dragons_1103.py <模组根目录> [输出根目录]
import math, os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import draw_race_art as base
import draw_dragons_1028 as d28
from draw_race_art import ell, tent, ellpts, INK
from draw_dragons_1028 import SPic, poly, mix, crest, spots, bands, X, PAL, EYE, OW, TOOTH, CREAM
from PIL import Image, ImageDraw, ImageFilter, ImageChops

ROOT = sys.argv[1] if len(sys.argv) > 1 else os.getcwd()
OUT = sys.argv[2] if len(sys.argv) > 2 else ROOT
HAR_TEX = os.path.join(OUT, "Bridges", "HAR", "Textures")
OLD_TEX = os.path.join(ROOT, "ArtSource", "backup-dragon-art-1103", "Bridges", "HAR", "Textures")


def parts_of(p):
    return [p] if isinstance(p[0], str) else p


def scales(p, part, col, step=3.2, a=.5, clip=None):
    """鳞纹：错位排列的小弧（开口朝上），收进形状里面一点，半透明叠色。"""
    k = p.k
    m = p.mask(parts_of(part)).filter(ImageFilter.MinFilter(int(k * 1.6) | 1))
    if clip is not None:
        m = ImageChops.multiply(m, p.mask(parts_of(clip)))
    box = m.getbbox()
    if not box:
        return
    pat = Image.new("L", m.size, 0)
    d = ImageDraw.Draw(pat)
    s = step * k
    w = max(2, int(k * .32))
    for j in range(int((box[3] - box[1]) / (s * .62)) + 2):
        y = box[1] + j * s * .62
        x0 = box[0] - s + (j % 2) * s / 2
        for i in range(int((box[2] - box[0]) / s) + 3):
            x = x0 + i * s
            d.arc([x - s / 2, y - s / 2, x + s / 2, y + s / 2], 25, 155, fill=255, width=w)
    pat = ImageChops.multiply(pat, m).point(lambda v: int(v * a))
    p.img.paste(Image.new("RGBA", p.img.size, col + (255,)), (0, 0), pat)


def plates(p, part, col, lines, w=.6, within=None):
    """腹甲：沿腹部的横纹（每条一段折线），剪进腹部形状（再收进 within 里面，不压轮廓）。"""
    m = p.mask([tent(ln, w, w * .8) for ln in lines])
    m = ImageChops.multiply(m, p.mask(parts_of(part)))
    if within is not None:
        m = ImageChops.multiply(m, p.mask(parts_of(within)).filter(ImageFilter.MinFilter(int(p.k * 1.2) | 1)))
    p.img.paste(Image.new("RGBA", p.img.size, col + (255,)), (0, 0), m)


def bez(a, c, b, n=14):
    """二次贝塞尔曲线上的点（含两端）。"""
    return [((1 - t) ** 2 * a[0] + 2 * (1 - t) * t * c[0] + t * t * b[0], (1 - t) ** 2 * a[1] + 2 * (1 - t) * t * c[1] + t * t * b[1]) for t in (i / n for i in range(n + 1))]


def claws(p, c, tips, size=1.0):
    for x, y, dx in tips:
        p.add(("poly", [(x, y - 1.2 * size), (x + dx * 2.8 * size, y + .6 * size), (x, y + 1.4 * size)]), c["h"], .9, shade=False)


def wing(p, c, root, wrist, tips, scal, back=False):
    """翅膀：翼臂（根→腕）+ 翼膜（腕→各指尖，后缘一段段内凹），翼骨从腕散开，腕上一只钩爪。"""
    m, d, w = c["m"], c["d"], c["w"]
    memb = w if not back else mix(w, d, .35)
    # 轮廓全用二次曲线：翼臂上沿略拱、前缘向外鼓、后缘每两指之间一段内凹的弧（不再是直线折角）
    cx = sum(x for x, _ in tips + [root]) / (len(tips) + 1)
    cy = sum(y for _, y in tips + [root]) / (len(tips) + 1)

    def bulge(a, b, f):
        mx, my = (a[0] + b[0]) / 2, (a[1] + b[1]) / 2
        nx, ny = -(b[1] - a[1]), b[0] - a[0]
        if nx * (mx - cx) + ny * (my - cy) < 0:
            nx, ny = -nx, -ny
        return (mx + nx * f, my + ny * f)
    pts = bez(root, bulge(root, wrist, .14), wrist) + bez(wrist, bulge(wrist, tips[0], .15), tips[0])[1:]
    for i, t in enumerate(tips):  # 后缘：每两指之间往腕的方向凹进去
        nxt = tips[i + 1] if i + 1 < len(tips) else root
        mx, my = (t[0] + nxt[0]) / 2, (t[1] + nxt[1]) / 2
        ctrl = (mx + (wrist[0] - mx) * scal * 2, my + (wrist[1] - my) * scal * 2)
        pts += bez(t, ctrl, nxt)[1:]
    part = ("poly", pts)
    p.add(part, memb, OW)
    ys = [y for _, y in pts]
    p.grad(part, c["w2"] if not back else mix(c["w2"], d, .45), min(ys) + (max(ys) - min(ys)) * .3, max(ys), .65)
    for i, t in enumerate(tips):  # 翼骨（略弯）
        mx, my = (wrist[0] + t[0]) / 2, (wrist[1] + t[1]) / 2
        dx, dy = t[0] - wrist[0], t[1] - wrist[1]
        mid = (mx + dy * .06, my - dx * .06)
        p.add(tent([wrist, mid, t], 1.5 if i == 0 else 1.1, .5), mix(memb, d, .55), 0, shade=False)
    arm = tent([root, ((root[0] + wrist[0]) / 2, (root[1] + wrist[1]) / 2 - 1), wrist], 6, 3.4)
    p.add(arm, m, OW * .8)
    scales(p, arm, mix(m, d, .7), 2.4, .45)
    dx = 1 if wrist[0] >= root[0] else -1
    p.add(("poly", [(wrist[0] - 1.2 * dx, wrist[1] + .6), (wrist[0] + .4 * dx, wrist[1] - 4.2), (wrist[0] + 1.6 * dx, wrist[1] + .4)]), c["h"], 1.0, shade=False)


def tail_tip(p, c, key, at, ang):
    """尾尖：红 = 铲形、蓝 = 刃鳍、绿 = 叶状鳍、黑 = 倒钩骨刺、白 = 羽簇；ang 为尾巴走向（度）。"""
    x, y = at
    ca, sa = math.cos(math.radians(ang)), math.sin(math.radians(ang))

    def T(u, v):  # 局部坐标：u 沿尾向，v 垂直
        return (x + u * ca - v * sa, y + u * sa + v * ca)
    if key == "Red":
        pts, col = [T(-1, -2), T(3, -4.5), T(7, 0), T(3, 4.5), T(-1, 2)], c["d"]
    elif key == "Blue":
        pts, col = [T(-2, -1.5), T(6, -3.5), T(4, 0), T(6, 2), T(-2, 1.5)], mix(c["b"], CREAM, .3)
    elif key == "Green":
        pts, col = [T(-3, 0), T(1, -4), T(6, -1), T(7, 0), T(1, 3)], c["w2"]
    elif key == "Black":
        pts, col = [T(-1, -1.5), T(6, -1), T(3.5, -3.5), T(8, 0), T(3.5, 3.5), T(6, 1), T(-1, 1.5)], c["h"]
    else:
        pts, col = [T(-2, -1), T(3, -4), T(4, -1.5), T(8, -2), T(6, 0), T(8, 2.5), T(4, 1.5), T(2, 3.5)], c["b"]
    p.add(poly(pts), col, 1.2)


# ---------------- 头（侧面，朝右） ----------------
def horns_side(p, c, key):
    h, w, d = c["h"], c["w"], c["d"]
    if key == "Red":  # 后掠大角 + 颊角
        p.add(tent([(79, 33), (71, 29), (63, 28), (56, 31)], 5.4, 1.1), h, OW)
        p.add(tent([(77, 40), (70, 39), (65, 42)], 3, 1), h, 1.4)
    elif key == "Blue":  # 竖角 + 耳鳍
        p.add(tent([(78, 32), (73, 26), (69, 20)], 4, 1), h, OW)
        p.add(poly([(76, 40), (63, 36), (65, 42), (74, 45)]), w, 1.2)
    elif key == "Green":  # 头冠 + 颊鳍
        p.add(("poly", [(76, 32), (79, 21), (85, 31)]), w, 1.2)
        for i, (x, y) in enumerate([(75, 34), (74, 39), (74, 43)]):
            p.add(("poly", [(x, y), (x - 9 + i, y - 3 + i * 2), (x + .5, y + 3.5)]), w, 1.0)
    elif key == "Black":  # 前弯大角
        p.add(tent([(78, 33), (71, 28), (71, 20), (79, 18), (85, 22)], 4.8, 1.1), h, OW)
    else:  # 白：鬃冠 + 小角
        p.add(("poly", [(74, 41), (68, 29), (74, 33), (75, 24), (80, 31), (82, 26), (84, 33)]), d, 1.2)
        p.add(tent([(81, 31), (83, 24)], 3, 1), h, 1.2)


def head_side(p, c, key):
    m, d, b, h = c["m"], c["d"], c["b"], c["h"]
    horns_side(p, c, key)
    # 下颚在后，头骨 + 吻一体描边（不让吻的轮廓切过脸）
    jaw = poly([(78, 41), (90, 42), (97.5, 42), (95, 46), (86, 48), (79, 46.5)])
    p.add(jaw, mix(m, d, .5), OW * .8)
    p.add(poly([(80, 44.8), (94, 45.2), (88, 47.2), (81, 46.8)]), b, 0, shade=False)  # 下颚腹鳞
    skull = ell((72, 29, 89, 45))
    snout = poly([(82, 31.5), (92, 34.5), (98.5, 39.5), (98, 42.6), (90, 43), (80, 43)])
    head = [skull, snout]
    p.add(head, m, OW)
    p.line([(82, 43.2), (97.4, 42.6)], .75)
    for x in (87, 90, 93):
        p.add(("poly", [(x, 42.9), (x + .8, 44.9), (x + 1.6, 42.8)]), TOOTH, 0, shade=False)
    scales(p, head, mix(m, d, .6), 2.2, .4)
    p.add(ell((95.2, 37.4, 96.6, 38.8)), INK, 0, shade=False)  # 鼻孔
    p.add(poly([(78, 33.2), (88.5, 33.2), (88, 35.4), (79, 36)]), d, 0, shade=False)  # 眉骨
    p.add(("poly", [(80.5, 35.8), (86.6, 35.4), (85.6, 38.2), (81.2, 38.2)]), EYE[key], .9, shade=False)
    p.add(ell((83.2, 35.6, 84.2, 38.1)), INK, 0, shade=False)
    if key == "Blue":
        p.add(("poly", [(90, 35.5), (93.5, 28.5), (95.5, 37)]), h, 1.2)  # 鼻上独角
    if key == "Black":  # 骷髅吻
        p.add(poly([(87, 35.5), (97, 38.6), (97.6, 42), (87, 42)]), h, 1.0)
        p.line([(90, 36.2), (90, 42)], .6); p.line([(93.5, 37.2), (93.5, 42)], .6)
    if key == "Green":
        for x, y in ((84, 47.5), (88, 47.8)):  # 下巴须鳍
            p.add(("poly", [(x - 1.5, y - .8), (x, y + 3.2), (x + 1.5, y - .8)]), c["w"], .9, shade=False)


# ---------------- 侧面（east，朝右） ----------------
def side_view(p, c, key):
    m, d, b, w, h = c["m"], c["d"], c["b"], c["w"], c["h"]
    far = mix(m, d, .55)
    # 远翅：收着，只露出翼腕和上沿
    wing(p, dict(c, m=far, w=mix(w, d, .5), w2=mix(c["w2"], d, .55)), (56, 60), (47, 35), [(12, 54), (24, 60), (36, 62)], .2)
    # 远侧前腿
    p.add(tent([(62, 78), (60, 86), (61, 91)], 7.5, 5.5), far, OW)
    p.add(ell((56, 88, 66, 93)), far, OW * .8)
    # 尾巴：从臀后往左拖，尾梢翘起
    tail = [(34, 80), (22, 85), (11, 86), (4.5, 81), (5, 73), (9, 67.5)]
    crest(p, c, [(26, 80.5), (16, 82), (8, 79), (6.5, 73)], 3.6, 1.1)
    tp = tent(tail, 14, 2.6)
    p.add(tp, m, OW)
    scales(p, tp, mix(m, d, .6), 2.8)
    p.add(tent([(30, 88), (18, 90), (10, 89)], 5, 2), b, 0, shade=False, clip=tp)
    bands(p, c, (16, 24), 80, 92, tp, 1)
    tail_tip(p, c, key, (9, 67.5), -70)
    # 身体：伏地，胸口抬起
    body = poly([(24, 70), (34, 63), (48, 60), (60, 61), (69, 68), (72, 79), (68, 89), (56, 93), (38, 93.5), (26, 91), (19, 82)])
    p.add(body, m, OW)
    scales(p, body, mix(m, d, .6))
    belly = ell((26, 82, 76, 106))
    p.add(belly, b, 0, clip=body)
    plates(p, ("poly", [(26, 84), (76, 84), (76, 100), (26, 100)]), mix(b, d, .28),
           [[(x, 83), (x - 1.5, 95)] for x in (36, 43, 50, 57, 64)], within=body)
    spots(p, c, [(34, 70, 1.4), (42, 75, 1.1), (54, 72, 1.2)], body)
    bands(p, c, (40, 48, 56), 60, 76, body)
    # 后腿：臀部鼓起，脚掌踩在前面
    haunch = poly(ellpts(32, 77, 13, 12.5, -12, 24))
    p.add(haunch, m, OW)
    scales(p, haunch, mix(m, d, .6), 3.0)
    spots(p, c, [(28, 72, 1.2), (35, 79, 1.0)], haunch)
    bands(p, c, (28, 35), 68, 82, haunch, 2)
    p.add(tent([(36, 85), (40, 89)], 7, 6), m, OW)
    p.add(ell((34, 87, 50, 93.5)), m, OW)
    claws(p, c, [(48.5, 89, 1), (49.5, 91.5, 1)])
    # 颈：S 形抬起；背鳍先画，基部被颈盖住
    crest(p, c, [(60, 64), (64.5, 56), (69, 47), (73.5, 39)], 4.0, 1.0)
    neck = tent([(62, 77), (68, 66), (72, 55), (77, 42)], 21, 11)
    p.add(neck, m, OW)
    scales(p, neck, mix(m, d, .6), 2.8)
    throat = tent([(70, 84), (75.5, 70), (78.5, 58), (82, 46)], 10, 5)
    p.add(throat, b, 0, clip=neck)
    plates(p, throat, mix(b, d, .28), [[(70 + i * 1.6, 79 - i * 5.6), (78 + i * 1.6, 81 - i * 5.6)] for i in range(7)], within=neck)
    # 近侧前腿：撑地
    p.add(tent([(67, 74), (71, 83), (72, 89)], 10, 7), m, OW)
    p.add(ell((66, 86.5, 81, 93.5)), m, OW)
    claws(p, c, [(79, 88.8, 1), (80.3, 91.4, 1)])
    head_side(p, c, key)
    # 近翅：收拢在背上，腕高过肩，翼指尖朝后下
    wing(p, c, (60, 64), (51, 37), [(10, 58), (22, 66), (34, 69), (46, 70)], .2)


# ---------------- 正面（south） ----------------
def horns_front(p, c, key, y=0):
    h, w, d = c["h"], c["w"], c["d"]
    for s in (-1, 1):
        if key == "Red":
            p.add(tent([(X(s, 57), 24 + y), (X(s, 65), 17 + y), (X(s, 71), 7 + y)], 5.4, 1.1), h, OW)
        elif key == "Blue":
            p.add(tent([(X(s, 58), 24 + y), (X(s, 63), 17 + y), (X(s, 65), 9 + y)], 4, 1), h, OW)
            p.add(("poly", [(X(s, 59), 31 + y), (X(s, 69), 26 + y), (X(s, 60), 36 + y)]), w, 1.2)
        elif key == "Green":
            for i, yy in enumerate((23, 29, 35)):
                p.add(("poly", [(X(s, 59), yy + y), (X(s, 68 - i), yy - 4 + y), (X(s, 60), yy + 3 + y)]), w, 1.0)
        elif key == "Black":
            p.add(tent([(X(s, 58), 24 + y), (X(s, 67), 20 + y), (X(s, 69), 29 + y), (X(s, 63), 35 + y)], 4.8, 1.1), h, OW)
        else:
            p.add(tent([(X(s, 57), 22 + y), (X(s, 61), 15 + y)], 3, 1), h, 1.2)
    if key == "White":
        p.add(("poly", [(41, 25 + y), (44, 13 + y), (47.5, 21 + y), (50, 11 + y), (52.5, 21 + y), (56, 13 + y), (59, 25 + y)]), d, 1.2)
    if key == "Green":
        p.add(("poly", [(46, 21 + y), (50, 9 + y), (54, 21 + y)]), w, 1.2)


def head_front(p, c, key):
    m, d, b, h = c["m"], c["d"], c["b"], c["h"]
    horns_front(p, c, key)
    head = [ell((40, 19, 60, 38)), poly([(43, 30), (57, 30), (56.5, 44), (50, 48.5), (43.5, 44)])]
    p.add(head, m, OW)
    scales(p, head, mix(m, d, .6), 2.2, .4)
    p.add(poly([(45, 41), (55, 41), (54.5, 45), (50, 48.5), (45.5, 45)]), mix(m, d, .5), 0, shade=False)
    p.add(poly([(46.5, 44.5), (53.5, 44.5), (50, 47.6)]), b, 0, shade=False)
    for x in (47.6, 52.4):
        p.add(ell((x - .8, 41.6, x + .8, 43.2)), INK, 0, shade=False)
    for s in (-1, 1):
        x = 50 + s * 5.6
        p.add(("poly", [(x - s * 3.6, 25), (x + s * 3.6, 24), (x + s * 3.6, 26), (x - s * 3, 27)]), d, 0, shade=False)  # 眉骨
        p.add(("poly", [(x - 2.8, 27.3), (x + 2.8, 26.5), (x + 1.9, 29.7), (x - 1.9, 29.7)]), EYE[key], .9, shade=False)
        p.add(ell((x - .5, 26.9, x + .5, 29.5)), INK, 0, shade=False)
    for x in (46.8, 53.2):
        p.add(("poly", [(x - 1, 46), (x, 48.6), (x + 1, 46)]), TOOTH, 0, shade=False)
    if key == "Black":
        p.add(poly([(45.5, 33), (54.5, 33), (54, 39.5), (46, 39.5)]), h, 1.0)
    if key == "Blue":
        p.add(("poly", [(48.2, 36), (50, 23), (51.8, 36)]), h, 1.2)


def front_view(p, c, key):
    m, d, b, w, h = c["m"], c["d"], c["b"], c["w"], c["h"]
    # 翅膀半张、向上向外
    for s in (-1, 1):
        wing(p, c, (X(s, 60), 60), (X(s, 82), 32), [(X(s, 98), 57), (X(s, 91), 67), (X(s, 79), 73)], .17)
    # 后腿（两侧，身后）
    for s in (-1, 1):
        hp = ell((X(s, 64), 72, X(s, 78), 93)) if s > 0 else ell((22, 72, 36, 93))
        p.add(hp, m, OW)
        scales(p, hp, mix(m, d, .6), 3.0)
    # 尾巴绕到身前（右侧）
    tp = tent([(66, 84), (80, 88), (82, 93.5), (68, 96), (54, 95.5)], 11, 3)
    p.add(tp, m, OW)
    scales(p, tp, mix(m, d, .6), 2.8)
    tail_tip(p, c, key, (54, 95.5), 182)
    # 身体
    body = ell((27, 57, 73, 95))
    p.add(body, m, OW)
    scales(p, body, mix(m, d, .6))
    chest = ell((37, 60, 63, 100))
    p.add(chest, b, 0, clip=body)
    plates(p, ("poly", [(37, 60), (63, 60), (63, 95), (37, 95)]), mix(b, d, .28),
           [[(40, y), (50, y + 1.4), (60, y)] for y in (70, 76, 82, 88)], within=body)
    spots(p, c, [(32, 70, 1.3), (68, 72, 1.2), (34, 82, 1.0), (66, 84, 1.0)], body)
    # 颈、头
    neck = tent([(50, 72), (50, 42)], 20, 13)
    p.add(neck, m, OW)
    scales(p, neck, mix(m, d, .6), 2.6)
    th = ell((44, 44, 56, 82))
    p.add(th, b, 0, clip=neck)
    plates(p, th, mix(b, d, .28), [[(44, y), (50, y + 1), (56, y)] for y in (50, 56, 62, 68)], within=neck)
    head_front(p, c, key)
    # 前爪
    for s in (-1, 1):
        leg = [poly(ellpts(X(s, 60.5), 75, 6.2, 8, -s * 14, 24)), tent([(X(s, 61), 75), (X(s, 63.5), 82), (X(s, 62.5), 88)], 9, 6.5)]  # 肩肘鼓起、前臂收细
        p.add(leg, m, OW)
        scales(p, leg, mix(m, d, .6), 2.6)
        x0 = X(s, 62)
        p.add(ell((x0 - 5.5, 86.5, x0 + 5.5, 93)), m, OW)
        for k in (-3, 0, 3):
            p.add(("poly", [(x0 + k - 1.1, 90.5), (x0 + k, 93.8), (x0 + k + 1.1, 90.5)]), h, .9, shade=False)


# ---------------- 背面（north） ----------------
def back_view(p, c, key):
    m, d, b, w, h = c["m"], c["d"], c["b"], c["w"], c["h"]
    spine = {"spike": h, "bone": h, "blade": mix(b, CREAM, .4), "sail": c["w2"], "feather": b}[c["crest"]]
    # 头、颈在最远处
    neck = tent([(50, 66), (50, 38)], 18, 13)
    p.add(neck, m, OW)
    scales(p, neck, mix(m, d, .6), 2.6)
    horns_front(p, c, key, 1)
    skull = ell((40, 20, 60, 40))
    p.add(skull, m, OW)
    scales(p, skull, mix(m, d, .6), 2.2, .4)
    for y in (44, 50, 56):
        p.add(("poly", [(48.2, y + 3.2), (50, y - 2.6), (51.8, y + 3.2)]), spine, 1.0, shade=False)
    # 翅膀（背面，颜色暗一些）
    for s in (-1, 1):
        wing(p, c, (X(s, 58), 62), (X(s, 82), 33), [(X(s, 98), 58), (X(s, 91), 68), (X(s, 79), 74)], .17, back=True)
    # 后脚、身体
    for s in (-1, 1):
        p.add(ell((22, 84, 36, 93) if s < 0 else (64, 84, 78, 93)), m, OW)
    body = ell((25, 57, 75, 94))
    p.add(body, m, OW)
    scales(p, body, mix(m, d, .6))
    bands(p, c, (38, 50, 62), 62, 80, body, 0)
    spots(p, c, [(32, 72, 1.2), (68, 74, 1.2), (36, 84, 1.0), (64, 84, 1.0)], body)
    for y in range(61, 88, 6):  # 背脊棘
        p.add(("poly", [(48, y + 4.2), (50, y - 2), (52, y + 4.2)]), spine, 1.0, shade=False)
    # 尾巴朝向观者，从身后拖出来绕到右下
    tp = tent([(52, 88), (60, 94), (74, 95), (84, 91), (87, 84)], 12, 3)
    p.add(tp, m, OW)
    scales(p, tp, mix(m, d, .6), 2.8)
    tail_tip(p, c, key, (87, 84), -80)


def draw_bodies(tex=HAR_TEX):
    for key, c in PAL.items():
        for rot in ("south", "east", "north"):
            p = SPic()
            {"south": front_view, "east": side_view, "north": back_view}[rot](p, c, key)
            base.TEX = tex
            p.save(f"ChromaticDragons/{key}/Naked_Hulk_{rot}", 256)


def preview():
    """借 1028 的预览排版（旧图 / south / east / west / north + 年龄档比例），不覆盖 1028 的预览图。"""
    d28.OLD_TEX, d28.HAR_TEX, d28.OUT = OLD_TEX, HAR_TEX, OUT
    old = os.path.join(OUT, "ArtSource", "Preview", "ChromaticDragons-1028.png")
    keep = open(old, "rb").read() if os.path.exists(old) else None
    path = d28.preview()
    new = path.replace("ChromaticDragons-1028.png", "ChromaticDragons-1103.png")
    os.replace(path, new)
    if keep is not None:
        open(old, "wb").write(keep)
    return new


if __name__ == "__main__":
    draw_bodies()
    print(preview())
