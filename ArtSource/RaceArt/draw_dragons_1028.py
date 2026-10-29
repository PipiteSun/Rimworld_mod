# 1028 五色真龙身体重画：照 Peter 给的参考图——修长的龙伏在地上，长颈弯起，翅膀收拢成两片带条纹的长叶盖在背上，
# 尾巴绕到身前，身上有浅色斑点；粗黑轮廓、平涂 + 柔和暗面（每块右下暗、左上微亮）、低饱和。
#  - 只重画 Bridges/HAR/Textures/ChromaticDragons/<色>/Naked_Hulk_{south,east,north}.png（256px；west 由游戏镜像 east）。
#  - 头贴图（透明）、物品图标、定义都不动；画布大小不变，龙仍在画面中央、贴近底部，游戏里的缩放 / 偏移不用改。
#  - ArtSource/Preview/ChromaticDragons-1028.png：旧图 east 对照 + 新图 south / east / west / north，再加三个年龄档比例。
# 用法：python -I ArtSource/RaceArt/draw_dragons_1028.py <模组根目录> [输出根目录]
import math, os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import draw_race_art as base
from draw_race_art import Pic, poly, ell, tent, ellpts, INK
from PIL import Image, ImageDraw, ImageFilter, ImageChops

ROOT = sys.argv[1] if len(sys.argv) > 1 else os.getcwd()
OUT = sys.argv[2] if len(sys.argv) > 2 else ROOT
HAR_TEX = os.path.join(OUT, "Bridges", "HAR", "Textures")
OLD_TEX = os.path.join(ROOT, "ArtSource", "backup-dragon-art-1028", "Bridges", "HAR", "Textures")

# 低饱和调色：主色（鳞）、暗面、腹部、翼膜、角 / 爪、斑点
PAL = {
    "Red": dict(m=(146, 66, 58), d=(104, 46, 46), b=(196, 150, 112), w=(118, 64, 66), h=(224, 208, 176)),
    "Blue": dict(m=(80, 92, 148), d=(56, 62, 108), b=(150, 168, 196), w=(70, 104, 152), h=(214, 210, 190)),
    "Green": dict(m=(82, 124, 74), d=(56, 90, 56), b=(170, 184, 120), w=(104, 140, 84), h=(214, 204, 164)),
    "Black": dict(m=(52, 46, 62), d=(34, 30, 42), b=(92, 82, 98), w=(70, 56, 86), h=(206, 196, 176)),
    "White": dict(m=(208, 218, 226), d=(164, 178, 192), b=(236, 236, 228), w=(180, 198, 214), h=(146, 156, 172)),
}
# 每色的设计特征：翼膜渐变色（w2）、背鳍样式（crest）、身上的花纹（mark）
STYLE = {
    "Red": dict(w2=(190, 112, 72), crest="spike", mark="band"),
    "Blue": dict(w2=(112, 162, 182), crest="blade", mark="spot"),
    "Green": dict(w2=(160, 178, 94), crest="sail", mark="spot"),
    "Black": dict(w2=(124, 86, 146), crest="bone", mark="band"),
    "White": dict(w2=(228, 238, 246), crest="feather", mark="stripe"),
}
for _k, _v in STYLE.items():
    PAL[_k].update(_v)
EYE = {"Red": (240, 190, 60), "Blue": (170, 220, 250), "Green": (210, 230, 90), "Black": (230, 120, 60), "White": (120, 190, 230)}
OW = 2.4
TOOTH = (240, 236, 224)
CREAM = (255, 250, 232)


def poly(pts, steps=6):
    """闭合的 Catmull-Rom 曲线：多边形的角都变成圆弧（三角形的小棘、爪保持原样，靠遮罩圆角磨钝）。"""
    if len(pts) <= 3:
        return ("poly", pts)
    n, out = len(pts), []
    for i in range(n):
        p0, p1, p2, p3 = pts[i - 1], pts[i], pts[(i + 1) % n], pts[(i + 2) % n]
        for j in range(steps):
            t = j / steps; t2, t3 = t * t, t * t * t
            out.append(tuple(.5 * (2 * p1[k] + (p2[k] - p0[k]) * t + (2 * p0[k] - 5 * p1[k] + 4 * p2[k] - p3[k]) * t2 + (3 * p1[k] - p0[k] - 3 * p2[k] + p3[k]) * t3) for k in (0, 1)))
    return ("poly", out)


def shift(m, dx, dy):
    out = Image.new("L", m.size, 0)
    out.paste(m, (int(dx), int(dy)))
    return out


def mix(c, t, f):
    return tuple(int(a + (b - a) * f) for a, b in zip(c, t))


class SPic(Pic):
    """在 Pic 的平涂上加柔和明暗：每块右下一条模糊的暗边，左上一条淡的亮边。"""

    def add(self, parts, color, ow=OW, shade=True, clip=None):
        if parts and isinstance(parts[0], str):
            parts = [parts]
        m = self.mask(parts)
        box = m.getbbox()
        if box:  # 圆角：模糊后取半，尖角磨圆、凹角填平
            r = min(1.3, min(box[2] - box[0], box[3] - box[1]) / self.k * .1) * self.k
            if r > 1:
                m = m.filter(ImageFilter.GaussianBlur(r)).point(lambda v: 255 if v > 120 else 0).filter(ImageFilter.GaussianBlur(1))
        if clip is not None:
            m = ImageChops.multiply(m, self.mask(clip if not isinstance(clip[0], str) else [clip]))
        if ow:
            o = m.filter(ImageFilter.GaussianBlur(ow * self.k / 2)).point(lambda v: 255 if v > 6 else 0)
            o = o.filter(ImageFilter.MaxFilter(5))
            self.img.paste(Image.new("RGBA", self.img.size, INK + (255,)), (0, 0), o)
        self.img.paste(Image.new("RGBA", self.img.size, color + (255,)), (0, 0), m)
        if not shade:
            return
        box = m.getbbox()
        if not box:
            return
        size = min(box[2] - box[0], box[3] - box[1]) / self.k
        s = min(4.5, size * .3) * self.k
        dark = ImageChops.subtract(m, shift(m, -s * .5, -s))
        dark = ImageChops.multiply(dark.filter(ImageFilter.GaussianBlur(s * .45)), m).point(lambda v: int(v * .85))
        self.img.paste(Image.new("RGBA", self.img.size, mix(color, (20, 18, 34), .3) + (255,)), (0, 0), dark)
        lite = ImageChops.subtract(m, shift(m, s * .4, s * .7))
        lite = ImageChops.multiply(lite.filter(ImageFilter.GaussianBlur(s * .5)), m).point(lambda v: int(v * .4))
        self.img.paste(Image.new("RGBA", self.img.size, mix(color, CREAM, .3) + (255,)), (0, 0), lite)

    def line(self, pts, w, color=INK):
        self.add(("line", pts, w), color, 0, shade=False)

    def grad(self, parts, color, y0, y1, a=.85):
        """竖直渐变叠色：y0 处透明，到 y1 处 a；收进形状里面一点，不压轮廓。"""
        m = self.mask(parts if not isinstance(parts[0], str) else [parts]).filter(ImageFilter.MinFilter(9))
        k = self.k
        g = Image.new("L", m.size, 0)
        h = max(1, int((y1 - y0) * k))
        g.paste(Image.linear_gradient("L").resize((m.width, h)), (0, int(y0 * k)))
        g.paste(255, (0, int(y0 * k) + h, m.width, m.height))
        g = ImageChops.multiply(g, m.filter(ImageFilter.GaussianBlur(3))).point(lambda v: int(v * a))
        self.img.paste(Image.new("RGBA", self.img.size, color + (255,)), (0, 0), g)


def layer(p, draw, scale, anchor, offset=(0, 0)):
    """在单独一层上画（头部），按 anchor 缩放、平移后叠回去：头小一点、离身体近一点。"""
    q = SPic()
    draw(q)
    k = p.k
    sz = int(q.img.width * scale)
    img = q.img.resize((sz, sz), Image.LANCZOS)
    ax, ay = anchor
    x = int(ax * k * (1 - scale) + offset[0] * k)
    y = int(ay * k * (1 - scale) + offset[1] * k)
    out = Image.new("RGBA", p.img.size, (0, 0, 0, 0))
    out.paste(img, (x, y))
    p.img.alpha_composite(out)


HEAD = .84  # 头部缩放：参考图里的龙头小、身体圆


def X(s, x):
    return 50 + s * (x - 50)


def spots(p, c, pts, clip):
    if c["mark"] != "spot":
        return
    col = mix(c["m"], c["b"], .45)
    for x, y, r in pts:
        p.add(ell((x - r, y - r * .8, x + r, y + r * .8)), col, 0, shade=False, clip=clip)


def bands(p, c, xs, y0, y1, clip, lean=3):
    """红龙 / 黑龙：背上横向的深色条带；白龙：淡蓝细纹。"""
    if c["mark"] == "band":
        col, w = mix(c["m"], c["d"], .75), 2.6
    elif c["mark"] == "stripe":
        col, w = mix(c["m"], (120, 160, 200), .35), 1.4
    else:
        return
    for x in xs:
        p.add(tent([(x - lean, y0), (x + lean * .3, (y0 + y1) / 2), (x, y1)], w, .4), col, 0, shade=False, clip=clip)


def resample(path, step):
    out = [path[0]]
    for a, b in zip(path, path[1:]):
        L = math.hypot(b[0] - a[0], b[1] - a[1])
        n = max(1, int(L / step))
        out += [(a[0] + (b[0] - a[0]) * i / n, a[1] + (b[1] - a[1]) * i / n) for i in range(1, n + 1)]
    return out


def crest(p, c, path, step=4.2, size=1.0):
    """背鳍 / 尾鳍：沿路径朝外（朝上）长出来，每色一种样式；先画，基部被后画的颈、尾盖住。"""
    pts = resample(path, step)
    kind = c["crest"]
    frames = []
    for i, (x, y) in enumerate(pts):
        a, b = pts[max(i - 1, 0)], pts[min(i + 1, len(pts) - 1)]
        tx, ty = b[0] - a[0], b[1] - a[1]
        L = math.hypot(tx, ty) or 1
        tx, ty = tx / L, ty / L
        nx, ny = -ty, tx
        if ny > 0:
            nx, ny = -nx, -ny
        frames.append((x, y, tx, ty, nx, ny))
    if kind == "sail":  # 绿龙：一条连续的波浪背帆，带鳍条
        top = [(x + nx * (5 if i % 2 == 0 else 3) * size, y + ny * (5 if i % 2 == 0 else 3) * size) for i, (x, y, tx, ty, nx, ny) in enumerate(frames)]
        p.add(("poly", [(x, y) for x, y, *_ in frames] + top[::-1]), c["w2"], 1.2)
        for (x, y, *_), (u, v) in zip(frames[::2], top[::2]):
            p.add(tent([(x, y), (u, v)], .9, .4), mix(c["w2"], c["d"], .5), 0, shade=False)
        return
    for i, (x, y, tx, ty, nx, ny) in enumerate(frames):
        if kind == "spike":  # 红龙：一排大三角棘
            hgt, half, col = 4.6, 2.0, c["h"]
        elif kind == "bone":  # 黑龙：稀疏、带钩的骨刺
            if i % 2:
                continue
            hgt, half, col = 5.8, 1.8, c["h"]
        elif kind == "blade":  # 蓝龙：短而密的刃鳍
            hgt, half, col = 4.2, 1.5, mix(c["b"], CREAM, .4)
        else:  # 白龙：羽毛状的簇
            hx, hy = x + nx * 2.6 * size, y + ny * 2.6 * size
            ang = math.degrees(math.atan2(ny, nx)) - 90 - 18
            p.add(("poly", ellpts(hx, hy, 1.5 * size, 3.4 * size, ang, 20)), c["b"], 1.0, shade=False)
            continue
        hgt, half = hgt * size, half * size
        hook = 1.6 if kind == "bone" else .6
        p.add(("poly", [(x - tx * half, y - ty * half), (x + nx * hgt - tx * hook, y + ny * hgt - ty * hook), (x + tx * half, y + ty * half)]), col, 1.0, shade=False)


def leaf_wing(p, c, pts, root, ends, color=None):
    """收拢的翅膀：一片长叶，翼膜往下沿渐变成另一种颜色，翼骨从腕关节散开。"""
    w = color or c["w"]
    p.add(poly(pts), w, OW)
    ys = [y for _, y in pts]
    p.grad(poly(pts), c["w2"] if color is None else mix(c["w2"], c["d"], .5), min(ys) + (max(ys) - min(ys)) * .35, max(ys), .7)
    for i, e in enumerate(ends):  # 翼骨略弯
        mx, my = (root[0] + e[0]) / 2, (root[1] + e[1]) / 2
        dx, dy = e[0] - root[0], e[1] - root[1]
        bend = (.08 if i else .05)
        mid = (mx + dy * bend, my - dx * bend)
        col = mix(w, c["d"], .45) if i == 0 else mix(w, CREAM, .25)
        p.add(tent([root, mid, e], 1.4 if i == 0 else 1.0, .5), col, 0, shade=False)


# ---------------- 侧面（east，朝右） ----------------
def side_view(p, c, key):
    m, d, b, w, h = c["m"], c["d"], c["b"], c["w"], c["h"]
    # 远处的翅膀，只露出上沿
    p.add(poly([(60, 58), (52, 47), (38, 46), (24, 51), (12, 58), (30, 60), (48, 60)]), mix(w, d, .55), OW)
    # 身体（伏在地上，圆鼓鼓的）
    body = poly(ellpts(44, 74, 27, 16.5, -3))
    p.add(body, m, OW)
    p.add(ell((14, 80, 76, 104)), b, 0, clip=body)
    spots(p, c, [(30, 72, 1.4), (36, 77, 1.1), (24, 78, 1.0), (44, 80, 1.2)], body)
    bands(p, c, (44, 52, 60), 58, 74, body)
    # 后腿：臀部鼓起，脚收在身下
    haunch = ell((20, 66, 44, 89))
    p.add(haunch, m, OW)
    spots(p, c, [(28, 72, 1.2), (34, 79, 1.0)], haunch)
    bands(p, c, (27, 34), 67, 80, haunch, 2)
    crest(p, c, [(14, 75), (7, 78.5), (3.5, 83.5)], 3.8, 1.3)  # 尾巴外圈
    p.add(ell((30, 86, 44, 92)), m, OW)
    p.add(poly([(42.5, 88), (46, 90), (42.5, 91.5)]), h, 1.0, shade=False)
    # 尾巴：从臀后绕到身前，贴着地面
    tail = [(24, 79), (12, 79), (6, 84), (10, 90), (24, 92), (40, 92), (52, 91)]
    tail_part = tent(tail, 11, 3)
    p.add(tail_part, m, OW)
    bands(p, c, (26, 34, 42), 86, 97, tail_part, 1)
    if key == "White":  # 冰龙：羽状尾鳍
        p.add(poly([(49, 88.5), (55, 85), (54, 88), (60, 87), (57, 90), (61, 92.5), (55, 92.5), (52, 94)]), b, 1.2)
    else:
        p.add(poly([(50, 89.2), (58, 88.5), (52, 93.5)]), d, 1.2)  # 尾尖鳍
    # 颈：短而粗，从胸前弯起；背鳍先画，基部被颈盖住
    crest(p, c, [(52.5, 75), (58.5, 67), (63, 60), (67.5, 54)], 4.2, 1.0)
    neck = tent([(58, 80), (65, 72), (69, 64), (73, 58)], 19, 13)
    p.add(neck, m, OW)
    p.add(tent([(65, 85), (72, 75), (76, 66), (79, 60)], 9, 5), b, 0, clip=neck)
    spots(p, c, [(64, 70, 1.1)], neck)
    bands(p, c, (60, 65), 62, 72, neck, -2)
    # 前爪收在胸前
    p.add(tent([(64, 82), (70, 88)], 8, 7), m, OW)
    p.add(ell((66, 85, 78, 92)), m, OW)
    for x in (75.5, 78):
        p.add(poly([(x, 88), (x + 3, 90), (x, 91.5)]), h, .9, shade=False)
    # 头（朝右）：单独一层，缩小后贴在颈端
    layer(p, lambda q: head_side(q, c, key), HEAD, (77, 50), (-2.5, 7))
    # 背脊上的鳍从收拢的翅膀上沿露出来
    crest(p, c, [(18, 58.5), (28, 54.5), (40, 53), (52, 53.5), (60, 56)], 5.2, 1.9)
    # 近处的翅膀：收拢的蝙蝠翼，从肩头沿背盖向尾部，下沿是一个个翼指尖
    leaf_wing(p, c, [(66, 66), (62, 55), (52, 50), (38, 51), (24, 55), (6, 64), (18, 64), (24, 70), (32, 66), (38, 72), (46, 68), (52, 73), (58, 69), (63, 72)],
              (62, 56), [(6, 64), (24, 69), (38, 71), (52, 72)])
    p.add(poly([(60.5, 57), (62, 50.5), (64.5, 56)]), h, 1.0, shade=False)  # 腕爪


def head_side(p, c, key):
    m, d, h = c["m"], c["d"], c["h"]
    horns_side(p, c, key)
    p.add(ell((71, 38, 87, 53)), m, OW)
    p.add(poly([(81, 41), (96, 45), (98, 49.5), (84, 53)]), m, OW)  # 吻
    p.add(poly([(83, 50.5), (97.5, 49), (96, 51.5), (85, 53.5)]), d, 0, shade=False)
    p.line([(84, 50.5), (97, 49)], .8)
    for x in (88, 91):
        p.add(poly([(x, 50.2), (x + .9, 52.4), (x + 1.8, 50)]), TOOTH, 0, shade=False)
    p.add(ell((94.4, 45.4, 95.8, 46.8)), INK, 0, shade=False)
    p.add(poly([(77.5, 42.5), (86.5, 41.5), (86, 43.5), (78.5, 44.5)]), d, 0, shade=False)  # 眉骨
    p.add(poly([(79.5, 44.3), (85, 43.8), (84.2, 46.6), (80, 46.6)]), EYE[key], 1.0, shade=False)
    p.add(ell((81.9, 44, 82.9, 46.4)), INK, 0, shade=False)
    if key == "Blue":
        p.add(poly([(90, 44), (93.5, 37), (95, 45.5)]), h, 1.2)
    if key == "Black":
        p.add(poly([(86, 44), (96.5, 46), (97, 48.8), (86, 49)]), h, 1.0)
        p.line([(89, 44.6), (89, 49)], .7); p.line([(92.5, 45.4), (92.5, 48.8)], .7)


def horns_side(p, c, key):
    h, w, d = c["h"], c["w"], c["d"]
    if key == "Red":  # 后掠大角
        p.add(tent([(78, 41), (70, 37), (62, 36), (55, 38)], 5.5, 1.2), h, OW)
        p.add(tent([(75, 46), (68, 45), (63, 48)], 3, 1), h, 1.4)
    elif key == "Blue":
        p.add(tent([(77, 40), (71, 35), (66, 30)], 3.8, 1), h, OW)
        p.add(poly([(74, 48), (62, 45), (64, 50), (73, 53)]), w, 1.2)  # 耳鳍
    elif key == "Green":
        p.add(poly([(75, 39.5), (79, 30), (83, 38.5)]), w, 1.2)  # 头冠
        for i, (x, y) in enumerate([(74, 41.5), (72, 46), (72, 50)]):
            p.add(poly([(x, y), (x - 9 + i, y - 3 + i * 2), (x + .5, y + 3.5)]), w, 1.0)
    elif key == "Black":  # 前伸弯角
        p.add(tent([(76, 41), (69, 36), (70, 29), (78, 27), (84, 31)], 4.8, 1.2), h, OW)
    elif key == "White":  # 鬃冠、小角
        p.add(poly([(72, 47), (67, 36), (73, 40), (74, 32), (79, 39), (81, 34), (82.5, 41)]), d, 1.2)
        p.add(tent([(80, 39), (82, 32)], 3, 1), h, 1.2)


# ---------------- 正面（south） ----------------
def head_front(p, c, key):
    m, d, h, w = c["m"], c["d"], c["h"], c["w"]
    horns_front(p, c, key)
    p.add(ell((40, 21, 60, 39)), m, OW)
    p.add(poly([(43, 32), (57, 32), (56.5, 46), (50, 49.5), (43.5, 46)]), m, OW)  # 吻
    p.add(poly([(44.5, 43), (55.5, 43), (55, 46.5), (50, 49.5), (45, 46.5)]), d, 0, shade=False)
    for x in (47.5, 52.5):
        p.add(ell((x - .8, 43.6, x + .8, 45.2)), INK, 0, shade=False)
    for s in (-1, 1):
        x = 50 + s * 5.5
        p.add(poly([(x - s * 3.5, 26.5), (x + s * 3.5, 25.5), (x + s * 3.5, 27.3), (x - s * 3, 28.3)]), d, 0, shade=False)  # 眉骨
        p.add(poly([(x - 2.8, 28.6), (x + 2.8, 27.8), (x + 1.9, 31), (x - 1.9, 31)]), EYE[key], 1.0, shade=False)
        p.add(ell((x - .5, 28.2, x + .5, 30.8)), INK, 0, shade=False)
    for x in (46.8, 53.2):
        p.add(poly([(x - 1, 48), (x, 50.5), (x + 1, 48)]), TOOTH, 0, shade=False)
    if key == "Black":  # 骷髅感：露骨的吻
        p.add(poly([(45.5, 35), (54.5, 35), (54, 41), (46, 41)]), h, 1.0)
    if key == "Blue":
        p.add(poly([(48.2, 37), (50, 25), (51.8, 37)]), h, 1.2)  # 鼻上独角
    if key == "Green":
        p.add(poly([(46, 22), (50, 11), (54, 22)]), w, 1.2)  # 头冠


def horns_front(p, c, key):
    h, w, d = c["h"], c["w"], c["d"]
    for s in (-1, 1):
        if key == "Red":
            p.add(tent([(X(s, 57), 25), (X(s, 65), 18), (X(s, 70), 8)], 5.5, 1.2), h, OW)
        elif key == "Blue":
            p.add(tent([(X(s, 58), 25), (X(s, 64), 19), (X(s, 66), 12)], 3.8, 1), h, OW)
            p.add(poly([(X(s, 59), 32), (X(s, 68), 28), (X(s, 60), 37)]), w, 1.2)
        elif key == "Green":
            for i, y in enumerate((24, 30, 36)):
                p.add(poly([(X(s, 59), y), (X(s, 67 - i), y - 4), (X(s, 60), y + 3)]), w, 1.0)
        elif key == "Black":
            p.add(tent([(X(s, 58), 25), (X(s, 66), 21), (X(s, 68), 30), (X(s, 63), 36)], 4.8, 1.2), h, OW)
        elif key == "White":
            p.add(tent([(X(s, 57), 23), (X(s, 61), 17)], 3, 1), h, 1.2)
    if key == "White":
        p.add(poly([(41, 26), (44, 15), (47.5, 22), (50, 13), (52.5, 22), (56, 15), (59, 26)]), d, 1.2)


def front_view(p, c, key):
    m, d, b, w, h = c["m"], c["d"], c["b"], c["w"], c["h"]
    # 两片收拢的翅膀从背上向两侧后方伸出
    for s in (-1, 1):
        leaf_wing(p, c, [(X(s, 58), 66), (X(s, 66), 56), (X(s, 78), 52), (X(s, 92), 54), (X(s, 86), 59), (X(s, 82), 65), (X(s, 74), 68), (X(s, 66), 72)],
                  (X(s, 64), 60), [(X(s, 92), 54), (X(s, 84), 61), (X(s, 74), 67)])
    # 身体（伏地，从前面看是一团）
    body = ell((25, 58, 75, 93))
    p.add(body, m, OW)
    spots(p, c, [(31, 70, 1.3), (69, 72, 1.2), (34, 80, 1.0), (66, 82, 1.0)], body)
    p.add(ell((37, 66, 63, 100)), b, 0, clip=body)
    # 颈、头
    neck = tent([(50, 78), (50, 46)], 19, 14)
    p.add(neck, m, OW)
    p.add(ell((44.5, 48, 55.5, 84)), b, 0, clip=neck)
    for y in (56, 62, 68, 74):
        p.line([(46.5, y), (53.5, y)], .7, d)
    layer(p, lambda q: head_front(q, c, key), HEAD, (50, 46), (0, 5))
    # 尾巴绕到身前
    p.add(tent([(70, 81), (79, 86), (76, 92), (62, 93.5), (48, 93)], 10, 3), m, OW)
    p.add(poly([(50, 91), (42, 91.5), (49, 95.5)]), d, 1.2)
    # 前爪
    for s in (-1, 1):
        x0 = 50 + s * 11
        p.add(ell((x0 - 6, 84, x0 + 6, 91)), m, OW)
        for k in (-3, 0, 3):
            p.add(poly([(x0 + k - 1.1, 89.5), (x0 + k, 92.5), (x0 + k + 1.1, 89.5)]), h, .9, shade=False)


# ---------------- 背面（north） ----------------
def head_back(p, c, key):
    horns_front(p, c, key)
    p.add(ell((40, 20, 60, 40)), c["m"], OW)
    for y in (25, 31, 37):
        p.add(poly([(48, y + 3), (50, y - 2.5), (52, y + 3)]), c["h"], 1.0, shade=False)


def back_view(p, c, key):
    m, d, b, w, h = c["m"], c["d"], c["b"], c["w"], c["h"]
    # 头和颈在身体后面（更远）
    p.add(tent([(50, 64), (50, 42)], 17, 14), m, OW)
    for y in (49, 55):
        p.add(poly([(48, y + 3), (50, y - 2.5), (52, y + 3)]), h, 1.0, shade=False)
    layer(p, lambda q: head_back(q, c, key), HEAD, (50, 44), (0, 5))
    # 后脚、身体
    for s in (-1, 1):
        p.add(ell((24, 85, 37, 93) if s < 0 else (63, 85, 76, 93)), m, OW)
    body = ell((24, 58, 76, 93))
    p.add(body, m, OW)
    spots(p, c, [(29, 76, 1.2), (71, 78, 1.2), (32, 86, 1.0), (68, 86, 1.0)], body)
    # 两片翅膀在背上合拢
    for s in (-1, 1):
        leaf_wing(p, c, [(50, 62), (X(s, 60), 57), (X(s, 74), 55), (X(s, 88), 58), (X(s, 82), 63), (X(s, 74), 70), (X(s, 64), 76), (50, 79)],
                  (X(s, 58), 60), [(X(s, 88), 58), (X(s, 76), 67), (X(s, 64), 74), (X(s, 54), 78)])
    spine = {"spike": h, "bone": h, "blade": b, "sail": c["w2"], "feather": b}[c["crest"]]
    for y in range(62, 80, 6):  # 背脊棘，颜色跟侧面的背鳍一致
        p.add(poly([(48.3, y + 4), (50, y - 1.5), (51.7, y + 4)]), spine, 1.0, shade=False)
    # 尾巴从身后绕过来
    p.add(tent([(56, 87), (70, 92), (82, 90.5), (86, 85)], 10, 3), m, OW)
    p.add(poly([(84, 87), (88, 79), (90, 87)]), d, 1.2)


def draw_bodies(tex=HAR_TEX):
    for key, c in PAL.items():
        for rot in ("south", "east", "north"):
            p = SPic()
            {"south": front_view, "east": side_view, "north": back_view}[rot](p, c, key)
            base.TEX = tex
            p.save(f"ChromaticDragons/{key}/Naked_Hulk_{rot}", 256)


def preview():
    cell = 160
    bg = (236, 230, 216, 255)
    keys = list(PAL)
    have_old = os.path.isdir(OLD_TEX)
    cols = (["old"] if have_old else []) + ["south", "east", "west", "north"]
    W = 20 + len(cols) * (cell + 10) + 20
    rows_h = len(keys) * (cell + 10)
    ages = [2.0, 2.8, 3.8]
    age_cell = 40
    age_h = int(1.5 * ages[-1] * age_cell) + 20
    out = Image.new("RGBA", (max(W, 760), 20 + rows_h + age_h + 20), bg)
    d = ImageDraw.Draw(out)
    y = 20
    for key in keys:
        x = 20
        for col in cols:
            if col == "old":
                img = Image.open(os.path.join(OLD_TEX, "ChromaticDragons", key, "Naked_Hulk_east.png")).convert("RGBA")
                d.rectangle((x - 4, y - 4, x + cell + 4, y + cell + 4), fill=(214, 206, 190))
            else:
                rot = "east" if col == "west" else col
                img = Image.open(os.path.join(HAR_TEX, "ChromaticDragons", key, f"Naked_Hulk_{rot}.png")).convert("RGBA")
                if col == "west":
                    img = img.transpose(Image.FLIP_LEFT_RIGHT)
            out.alpha_composite(img.resize((cell, cell), Image.LANCZOS), (x, y))
            x += cell + 10
        y += cell + 10
    # 年龄档比例（按游戏渲染缩放 ×2.0 / 2.8 / 3.8），旁边一个人
    base_y = y + age_h - 10
    hx = 40
    d.ellipse((hx - 7, base_y - 40, hx + 7, base_y - 27), fill=(214, 168, 132), outline=INK, width=2)
    d.rectangle((hx - 8, base_y - 27, hx + 8, base_y - 7), fill=(110, 120, 90), outline=INK, width=2)
    d.line((hx - 5, base_y - 7, hx - 5, base_y), fill=INK, width=3); d.line((hx + 5, base_y - 7, hx + 5, base_y), fill=INK, width=3)
    x = 80
    for i, s in enumerate(ages):
        key = keys[i % len(keys)]
        img = Image.open(os.path.join(HAR_TEX, "ChromaticDragons", key, "Naked_Hulk_east.png")).convert("RGBA")
        sz = int(1.5 * s * age_cell)
        out.alpha_composite(img.resize((sz, sz), Image.LANCZOS), (x, base_y - int(sz * .95)))
        x += sz + 10
    path = os.path.join(OUT, "ArtSource", "Preview", "ChromaticDragons-1028.png")
    os.makedirs(os.path.dirname(path), exist_ok=True)
    out.convert("RGB").save(path)
    return path


if __name__ == "__main__":
    draw_bodies()
    print(preview())
