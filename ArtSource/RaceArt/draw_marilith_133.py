# 1.33.0 六臂蛇人贴图：沿用 draw_race_art.py 的画法（粗黑轮廓、平涂 + 一层暗面、低饱和）。
# 用法：python -I ArtSource/RaceArt/draw_marilith_133.py <模组根目录>
# 输出到 Bridges/HAR/Textures/Marilith（只在启用 HAR 时加载），预览 ArtSource/Marilith-1.33.0-preview.png。
#
# 身体：原版 Naked_Hulk / Naked_Child 的轮廓（ArtSource/RaceArt/marilith_ref，由 Test.ps1 -Only Marilith 从游戏里导出）重新描边上色，
#      近白灰底色 + 一层暗面（CutoutSkin 着色器按肤色相乘），下半身加几道鳞纹；衣服贴图直接用原版壮硕体型。
# 身体附件（HAR bodyAddons，画布与身体同一套坐标“身体单位”0～100，见 Canvas）：
#   额外手臂 ×4（中、下两对，每只一张，南/北/东/西四向；西向不是东向的镜像，而是另一侧手臂东向的镜像），手臂随肤色（_m 遮罩红色），弯刀不着色；
#   蛇尾 ×1（南/北/东，西向自动镜像），整条随肤色。儿童各有一套缩小版（BodyType Child/Baby）。
import math, os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import draw_race_art as base
from draw_race_art import Pic, poly, ell, tent, taper, arc, mir, INK, OW, STEEL, STEEL_D, GOLD, GOLD_D, WOOD, WOOD_D, LEAF, LEAF_D
from PIL import Image, ImageDraw, ImageFilter, ImageChops

ROOT = sys.argv[1] if len(sys.argv) > 1 else os.getcwd()
OUT = os.path.join(ROOT, "Bridges", "HAR", "Textures")
REF = os.path.join(ROOT, "ArtSource", "RaceArt", "marilith_ref")
base.TEX = OUT
WORK = 1024

# 着色部分画成近白灰（游戏里乘肤色），只用两层：亮面与暗面
SKIN_L = (238, 238, 238); SKIN_M = (206, 206, 206); SKIN_D = (170, 170, 170)
BLADE = (190, 194, 196); BLADE_D = (138, 144, 150); BLADE_E = (232, 234, 236)
GRIP = (96, 70, 52); GRIP_D = (70, 50, 38); GUARD = (176, 146, 86); GUARD_D = (140, 112, 62)
# 图标配色（低饱和的青绿、墨绿、赤金）
TEAL = (92, 138, 120); TEAL_D = (66, 104, 90); DARKG = (70, 98, 78); GOLDS = (178, 120, 74); GOLDS_D = (140, 92, 56)
FIRE = (214, 120, 64); FIRE_L = (240, 196, 120); ICE = (150, 190, 214); ICE_L = (214, 234, 246); MUD = (110, 92, 70); MUD_D = (86, 70, 52)
WATER = (104, 140, 160); WATER_L = (156, 186, 200); PURPLE = (120, 96, 140)


# ---------------- 画布：在“身体单位”里画，按附件的 drawSize 与偏移换算到贴图 ----------------
class Canvas:
    """D：画布边长是身体画布的几倍（附件 drawSize）；oy：附件往下的偏移（身体单位，100 = 1.5 格）。
    身体单位里的点 (x, y) → 画布单位 50 + (x - 50) / D，50 + (y - 50 - oy) / D。child：把成人图形整体缩到儿童身体框里。"""
    def __init__(self, D=1.0, oy=0.0, child=False, mask=True):
        self.D, self.oy, self.child = D, oy, child
        self.col = Pic()
        self.msk = Pic() if mask else None

    def T(self, pts):
        out = []
        for x, y in pts:
            if self.child:  # 成人壮硕身体（x 20～80，y 26～99）→ 儿童身体（x 37～63，y 33～74）
                x, y = 50 + (x - 50) * .44, 54 + (y - 62) * .56
            out.append((50 + (x - 50) / self.D, 50 + (y - 50 - self.oy) / self.D))
        return out

    def conv(self, parts):
        if parts and isinstance(parts[0], str):
            parts = [parts]
        res = []
        for p in parts:
            kind = p[0]
            if kind.lstrip("-") == "line":
                res.append((kind, self.T(p[1]), p[2] / self.D * (.56 if self.child else 1)) + tuple(p[3:]))
            else:
                res.append((kind, self.T(p[1])) + tuple(p[2:]))
        return res

    def add(self, parts, color, tint=True, ow=OW):
        parts = self.conv(parts)
        k = (.6 if self.child else 1) / self.D
        self.col.add(parts, color, ow * k if ow else 0)
        if self.msk is not None:
            m = self.msk.mask(parts)
            if ow:
                o = m.filter(ImageFilter.GaussianBlur(ow * k * self.msk.k / 2)).point(lambda v: 255 if v > 6 else 0).filter(ImageFilter.MaxFilter(5))
                self.msk.img.paste(Image.new("RGBA", self.msk.img.size, (0, 0, 0, 255)), (0, 0), o)
            self.msk.img.paste(Image.new("RGBA", self.msk.img.size, ((255, 0, 0, 255) if tint else (0, 0, 0, 255))), (0, 0), m)

    def line(self, pts, w, color=INK, tint=False):
        self.add(("line", pts, w), color, tint, 0)

    def save(self, rel, size):
        path = os.path.join(OUT, rel.replace("/", os.sep) + ".png")
        os.makedirs(os.path.dirname(path), exist_ok=True)
        self.col.img.resize((size, size), Image.LANCZOS).save(path)
        out = [path]
        if self.msk is not None:
            mp = path[:-4] + "m.png"
            # 遮罩只要红/黑；透明处填黑
            mi = self.msk.img.resize((size, size), Image.LANCZOS)
            bg = Image.new("RGBA", mi.size, (0, 0, 0, 255)); bg.alpha_composite(mi)
            bg.convert("RGB").save(mp); out.append(mp)
        return out


def curve(p0, p1, p2, n=10):
    """二次贝塞尔折线。"""
    return [((1 - t) ** 2 * p0[0] + 2 * (1 - t) * t * p1[0] + t * t * p2[0], (1 - t) ** 2 * p0[1] + 2 * (1 - t) * t * p1[1] + t * t * p2[1]) for t in [i / n for i in range(n + 1)]]


def scimitar(c, hand, tipdir, length=30, bend=.32):
    """弯刀：从手（hand）向 tipdir 方向伸出，刀身向一侧弯。不着色。"""
    hx, hy = hand; dx, dy = tipdir; L = math.hypot(dx, dy); dx, dy = dx / L, dy / L
    nx, ny = -dy, dx
    tip = (hx + dx * length, hy + dy * length)
    mid = (hx + dx * length * .55 + nx * length * bend, hy + dy * length * .55 + ny * length * bend)
    spine = curve((hx + dx * 3, hy + dy * 3), mid, tip, 14)
    c.add(("poly", taper(spine, 6.2, .6)), BLADE, False, OW * .8)
    edge = curve((hx + dx * 4 + nx * 1.2, hy + dy * 4 + ny * 1.2), (mid[0] + nx * 1.4, mid[1] + ny * 1.4), tip, 12)
    c.add(("poly", taper(edge, 2.2, .3)), BLADE_E, False, 0)
    back = curve((hx + dx * 4 - nx * 1.4, hy + dy * 4 - ny * 1.4), (mid[0] - nx * 1.2, mid[1] - ny * 1.2), tip, 12)
    c.add(("poly", taper(back, 1.8, .2)), BLADE_D, False, 0)
    # 护手与握柄
    c.add(("poly", taper([(hx + nx * 5 + dx * 2.5, hy + ny * 5 + dy * 2.5), (hx - nx * 5 + dx * 2.5, hy - ny * 5 + dy * 2.5)], 2.6, 2.6)), GUARD, False, OW * .7)
    c.add(("poly", taper([(hx + dx * 2, hy + dy * 2), (hx - dx * 6, hy - dy * 6)], 2.8, 2.6)), GRIP, False, OW * .7)
    c.add(("poly", taper([(hx - dx * 6.5, hy - dy * 6.5), (hx - dx * 7.5, hy - dy * 7.5)], 3.6, 3.6)), GUARD_D, False, OW * .6)


def arm(c, shoulder, elbow, hand, w0=13, w1=9):
    """一条手臂：上臂 + 前臂（随肤色），手握成拳。"""
    c.add(("poly", taper([shoulder, elbow], w0, w1 + 1)), SKIN_L, True)
    c.add(("poly", taper([elbow, hand], w1 + 1, w1)), SKIN_L, True)
    # 暗面：沿下侧画一条窄带
    sx, sy = shoulder; ex, ey = elbow; hx, hy = hand
    c.add(("poly", taper([(sx, sy + w0 * .25), (ex, ey + w1 * .3), (hx, hy + w1 * .25)], w0 * .35, w1 * .3)), SKIN_M, True, 0)
    c.add(("poly", base.ellpts(hx, hy, w1 * .62, w1 * .62)), SKIN_L, True, OW * .9)
    c.add(("poly", base.ellpts(hx + w1 * .15, hy + w1 * .2, w1 * .38, w1 * .3)), SKIN_M, True, 0)


# 每只手臂在各朝向里的姿势：(肩, 肘, 手, 刀尖方向, 刀长)。南向：角色左侧在画面右边；北向：角色左侧在画面左边。
def pose(slot, facing):
    mid = slot in ("MidLeft", "MidRight")
    left = slot.endswith("Left")
    if facing in ("south", "north"):
        side = 1 if (left == (facing == "south")) else -1  # +1 = 画面右边
        if mid:
            p = ((50 + side * 16, 46), (50 + side * 33, 47), (50 + side * 42, 35), (side * .55, -1), 30)
        else:
            p = ((50 + side * 15, 64), (50 + side * 31, 72), (50 + side * 41, 82), (side * 1, .55), 28)
        return p
    # 东向（朝右）：角色右侧（近侧）手臂向前，左侧（远侧）手臂从背后露出
    near = not left
    if near:
        return ((54, 46), (66, 52), (80, 42), (.55, -1), 30) if mid else ((52, 63), (64, 74), (79, 76), (1, .4), 28)
    return ((44, 44), (32, 46), (20, 34), (-.4, -1), 28) if mid else ((44, 62), (34, 72), (20, 74), (-1, .35), 26)


def draw_arm(slot, facing, child=False):
    c = Canvas(D=1.4, child=child)
    sh, el, hd, tipd, L = pose(slot, facing)
    # 北向（看背后）和东向远侧：刀在手臂后面先画；其余刀在手臂前面
    behind = facing == "north" or (facing == "east" and slot.endswith("Left"))
    if behind:
        scimitar(c, hd, tipd, L)
    arm(c, sh, el, hd)
    if not behind:
        scimitar(c, hd, tipd, L)
    return c


# ---------------- 蛇尾 ----------------
TAIL_D, TAIL_OY = 2.0, 25.0  # 画布是身体的 2 倍、往下偏 25 身体单位（= 0.375 格），XML 里 drawSize (2,2)、offset (0,-0.375)
TAIL = {
    "south": [(50, 76), (51, 85), (58, 94), (70, 100), (80, 106), (75, 113), (57, 116), (38, 112), (26, 105), (29, 98), (40, 96)],
    "north": None,  # 北向 = 南向左右镜像
    "east": [(50, 76), (49, 86), (42, 95), (28, 100), (10, 103), (-8, 101), (-21, 96), (-29, 89), (-27, 83)],
}


def draw_tail(facing, child=False):
    c = Canvas(D=TAIL_D, oy=TAIL_OY, child=child, mask=False)
    pts = TAIL["south"] if facing in ("south", "north") else TAIL["east"]
    if facing == "north":
        pts = mir(pts)
    w0 = 32 if facing != "east" else 29
    body = taper(pts, w0, 6)
    c.add(("poly", body), SKIN_L, True)
    # 暗面：沿外侧（右下）一条
    shade = taper([(x + 2.2, y + 2.6) for x, y in pts], w0 * .45, 2.5)
    c.add(("poly", shade), SKIN_M, True, 0)
    # 腹鳞：沿中线的横纹
    n = len(pts)
    sm = taper(pts, 0, 0)[:len(taper(pts, 0, 0)) // 2]
    for i in range(3, len(sm) - 4, 4):
        (x0, y0), (x1, y1) = sm[i - 1], sm[i + 1]
        dx, dy = x1 - x0, y1 - y0; L = math.hypot(dx, dy) or 1; nx, ny = -dy / L, dx / L
        t = i / len(sm); w = (w0 + (6 - w0) * t) * .33
        x, y = sm[i]
        c.line([(x - nx * w, y - ny * w), (x + dx / L * 1.2, y + dy / L * 1.2), (x + nx * w, y + ny * w)], 1.1 + (1 - t) * .6, SKIN_D)
    # 腰部和身体接缝：一圈鳞边
    x, y = pts[0]
    c.line([(x - w0 * .48, y - 1), (x - w0 * .2, y + 2.5), (x, y + 3), (x + w0 * .2, y + 2.5), (x + w0 * .48, y - 1)], 1.6, SKIN_D)
    return c


# ---------------- 身体（原版轮廓重描） ----------------
def body(btype, facing):
    ref = Image.open(os.path.join(REF, "MarilithRef-Naked_%s_%s.png" % (btype, facing))).convert("RGBA")
    a = ref.split()[3].resize((WORK, WORK), Image.BICUBIC).filter(ImageFilter.GaussianBlur(3)).point(lambda v: 255 if v > 128 else 0)
    # 原版轮廓本身带描边：往里收一圈当填充，原轮廓当描边
    inner = a.filter(ImageFilter.MinFilter(25 if btype == "Hulk" else 15))
    img = Image.new("RGBA", (WORK, WORK), (0, 0, 0, 0))
    img.paste(Image.new("RGBA", img.size, INK + (255,)), (0, 0), a)
    img.paste(Image.new("RGBA", img.size, SKIN_L + (255,)), (0, 0), inner)
    # 暗面：填充区和“往左上平移后的填充区”之差（右下一层）
    k = WORK / 100
    shift = ImageChops.offset(inner, int(-5 * k), int(-3 * k)) if facing != "north" else ImageChops.offset(inner, int(5 * k), int(-3 * k))
    shade = ImageChops.subtract(inner, shift)
    img.paste(Image.new("RGBA", img.size, SKIN_M + (255,)), (0, 0), shade)
    bb = inner.getbbox()
    d = ImageDraw.Draw(img)
    x0, y0, x1, y1 = bb; w = x1 - x0; h = y1 - y0
    lw = max(3, int(k * (1.1 if btype == "Hulk" else .7)))
    # 腰下几道鳞纹（会被蛇尾的接缝盖住一部分）
    for j, yy in enumerate((.80, .88)):
        cy = y0 + h * yy
        for i in range(-3, 4):
            cx = x0 + w * (.5 + i * .11) + (w * .055 if j % 2 else 0)
            r = w * .055
            if cx - r < x0 + w * .12 or cx + r > x1 - w * .12:
                continue
            d.arc([cx - r, cy - r, cx + r, cy + r], 20, 160, fill=SKIN_D + (255,), width=lw)
    if facing == "south":
        # 锁骨与胸口的简单线条
        d.line([(x0 + w * .33, y0 + h * .2), (x0 + w * .46, y0 + h * .25)], fill=SKIN_D + (255,), width=lw)
        d.line([(x1 - w * .33, y0 + h * .2), (x1 - w * .46, y0 + h * .25)], fill=SKIN_D + (255,), width=lw)
    img = Image.composite(img, Image.new("RGBA", img.size, (0, 0, 0, 0)), a)
    path = os.path.join(OUT, "Marilith", "Bodies", "Naked_%s_%s.png" % (btype, facing))
    os.makedirs(os.path.dirname(path), exist_ok=True)
    img.resize((256, 256), Image.LANCZOS).save(path)
    return path


# ---------------- 图标（256） ----------------
def icon_six_arms(p, cx=50, cy=50, skin=TEAL, dark=TEAL_D, s=1.0):
    def S(pts): return [(cx + (x - 50) * s, cy + (y - 50) * s) for x, y in pts]
    for side in (1, -1):
        for (sh, el, hd) in (((50 + side * 10, 40), (50 + side * 26, 38), (50 + side * 34, 24)),
                             ((50 + side * 10, 50), (50 + side * 28, 54), (50 + side * 38, 46)),
                             ((50 + side * 10, 60), (50 + side * 24, 70), (50 + side * 34, 78))):
            p.add(("poly", taper(S([sh, el, hd]), 7 * s, 5 * s)), skin, OW * s)
    p.add(("poly", S(base.ellpts(50, 48, 12, 18))), skin, OW * s)
    p.add(("poly", S(base.ellpts(54, 52, 7, 14))), dark, 0)
    p.add(("poly", S(base.ellpts(50, 24, 9, 10))), skin, OW * s)


def blade_icon(p, hand, tipd, L, s=1.0):
    hx, hy = hand; dx, dy = tipd; n = math.hypot(dx, dy); dx, dy = dx / n, dy / n; nx, ny = -dy, dx
    tip = (hx + dx * L, hy + dy * L); mid = (hx + dx * L * .55 + nx * L * .3, hy + dy * L * .55 + ny * L * .3)
    p.add(("poly", taper(curve(hand, mid, tip, 14), 6 * s, .6)), BLADE, OW * s * .8)
    p.add(("poly", taper(curve((hx + nx, hy + ny), (mid[0] + nx * 1.4, mid[1] + ny * 1.4), tip, 12), 2 * s, .3)), BLADE_E, 0)
    p.add(("poly", taper([(hx + nx * 5 * s, hy + ny * 5 * s), (hx - nx * 5 * s, hy - ny * 5 * s)], 2.6 * s, 2.6 * s)), GUARD, OW * s * .7)


def xenotype_icon():
    p = Pic()
    # 盘起的蛇尾 + 六臂上身
    p.add(("poly", taper([(50, 66), (64, 78), (78, 84), (70, 94), (46, 95), (26, 90), (24, 80), (36, 78)], 18, 5)), TEAL)
    p.add(("poly", taper([(52, 70), (64, 81), (76, 86)], 6, 2)), TEAL_D, 0)
    icon_six_arms(p, 50, 44, TEAL, TEAL_D, .9)
    return p


def blades_icon():
    p = Pic()
    for k, (hand, d) in enumerate((((30, 70), (-.5, -1)), ((70, 70), (.5, -1)), ((22, 56), (-1, -.4)), ((78, 56), (1, -.4)), ((36, 82), (-.8, -.6)), ((64, 82), (.8, -.6)))):
        blade_icon(p, hand, d, 30 if k < 2 else 24)
    p.add(("poly", base.ellpts(50, 78, 14, 14)), TEAL)
    p.add(("poly", base.ellpts(55, 82, 8, 9)), TEAL_D, 0)
    return p


def glide_icon():
    p = Pic()
    p.add(("poly", [(4, 62), (30, 56), (60, 60), (96, 54), (96, 96), (4, 96)]), MUD)
    p.add(("poly", [(4, 80), (40, 74), (96, 78), (96, 96), (4, 96)]), MUD_D, 0)
    p.add(("poly", [(10, 66), (36, 62), (60, 66), (40, 70)]), WATER_L, 1.6)
    p.add(("poly", taper([(8, 40), (26, 30), (44, 42), (62, 54), (80, 46), (92, 34)], 14, 4)), TEAL)
    p.add(("poly", taper([(10, 44), (26, 35), (44, 47), (62, 58), (80, 50)], 5, 2)), TEAL_D, 0)
    for x in (30, 52, 74):
        p.line([(x, 72), (x + 10, 70)], 2.2, WATER_L)
    return p


def constrict_icon():
    p = Pic()
    # 一个人形被蛇尾缠住
    p.add(("poly", base.ellpts(50, 22, 10, 11)), (196, 180, 160))
    p.add(("poly", [(36, 34), (64, 34), (68, 90), (32, 90)]), (150, 130, 112))
    for y in (44, 60, 76):
        p.add(("poly", taper([(18, y + 6), (50, y - 4), (84, y + 4)], 13, 10)), TEAL)
        p.add(("poly", taper([(22, y + 9), (50, y), (80, y + 7)], 4, 3)), TEAL_D, 0)
    p.add(("poly", taper([(84, 80), (92, 70), (88, 58)], 8, 2)), TEAL)
    return p


def bladestorm_icon():
    p = Pic()
    for k in range(6):
        a = math.radians(k * 60 + 15)
        hx, hy = 50 + math.cos(a) * 14, 50 + math.sin(a) * 14
        blade_icon(p, (hx, hy), (math.cos(a + .9), math.sin(a + .9)), 30)
    p.add(("poly", base.ellpts(50, 50, 13, 13)), TEAL)
    p.add(("poly", base.ellpts(54, 54, 7, 7)), TEAL_D, 0)
    for r in (36, 44):
        p.add(("line", base.arc(50, 50, r, 200, 300, 12), 2.2), (226, 214, 190), 0)
        p.add(("line", base.arc(50, 50, r, 20, 120, 12), 2.2), (226, 214, 190), 0)
    return p


def hide_icon():
    p = Pic()
    p.add(("poly", [(50, 8), (86, 22), (82, 62), (50, 92), (18, 62), (14, 22)]), DARKG)
    p.add(("poly", [(50, 8), (86, 22), (82, 62), (50, 92)]), (56, 80, 64), 0)
    for row in range(4):
        for col in range(-2, 3):
            x, y = 50 + col * 13 + (6 if row % 2 else 0), 26 + row * 14
            if abs(x - 50) > 30 - row * 3:
                continue
            p.add(("line", base.arc(x, y, 6, 20, 160, 8), 1.8), (110, 140, 118), 0)
    p.add(("poly", taper([(16, 92), (24, 74), (20, 60)], 9, 2)), FIRE, 1.8)
    p.add(("poly", taper([(84, 92), (78, 74), (84, 60)], 9, 2)), ICE, 1.8)
    return p


def coldblood_icon():
    p = Pic()
    # 温度计 + 雪花，蛇头蜷着
    p.add(("poly", taper([(30, 14), (30, 70)], 12, 12)), (226, 226, 226))
    p.add(("poly", base.ellpts(30, 78, 11, 11)), (120, 160, 196))
    p.add(("poly", taper([(30, 50), (30, 72)], 5, 5)), (120, 160, 196), 0)
    for i, y in enumerate((22, 32, 42)):
        p.line([(36, y), (40, y)], 1.6)
    p.add(("poly", taper([(52, 86), (70, 84), (84, 74), (82, 60), (66, 58), (60, 68)], 14, 6)), TEAL)
    p.add(("poly", base.ellpts(64, 66, 9, 7)), TEAL, 1.6)
    for k in range(3):
        a = math.radians(90 + k * 60); dx, dy = math.cos(a) * 12, math.sin(a) * 12
        p.add(("line", [(72 - dx, 26 - dy), (72 + dx, 26 + dy)], 3), ICE_L, 1.4)
    return p


def ability_constrict():
    return constrict_icon()


def ability_bladestorm():
    return bladestorm_icon()


def preview(paths):
    tiles = [Image.open(p).convert("RGBA") for p in paths if not p.endswith(("southm.png", "eastm.png", "northm.png", "westm.png"))]
    cell = 140; cols = 10; rows = (len(tiles) + cols - 1) // cols
    sheet = Image.new("RGBA", (cell * cols, cell * rows), (58, 62, 66, 255))
    for i, t in enumerate(tiles):
        t = t.copy(); t.thumbnail((128, 128), Image.LANCZOS)
        sheet.alpha_composite(t, ((i % cols) * cell + (cell - t.width) // 2, (i // cols) * cell + (cell - t.height) // 2))
    return sheet


def composite(btype, facing, skin=(92, 138, 120)):
    """预览用：把身体、手臂、蛇尾按游戏里的层级与缩放拼起来（身体画布 256 = 1.5 格）。"""
    S = 256
    W = int(S * TAIL_D)
    sheet = Image.new("RGBA", (W, W), (0, 0, 0, 0))
    def tint(img, mask=None):
        img = img.convert("RGBA"); r, g, b, a = img.split()
        mul = Image.merge("RGBA", (r.point(lambda v: v * skin[0] // 255), g.point(lambda v: v * skin[1] // 255), b.point(lambda v: v * skin[2] // 255), a))
        if mask is None:
            return mul
        m = mask.convert("L").point(lambda v: 255 if v > 128 else 0) if mask.mode != "RGB" else mask.split()[0].point(lambda v: 255 if v > 128 else 0)
        return Image.composite(mul, img, m.resize(img.size))
    def place(img, D, oy):
        size = int(S * D); img = img.resize((size, size), Image.LANCZOS)
        cx = W // 2; cy = W // 2 - int(TAIL_OY / 100 * S) + int(oy / 100 * S)
        sheet.alpha_composite(img, (cx - size // 2, cy - size // 2))
    sfx = "" if btype == "Hulk" else "Child"
    fac = "east" if facing == "west" else facing
    def addon(name):
        p = os.path.join(OUT, "Marilith", "Addons", name + sfx + "_" + facing + ".png")
        if not os.path.exists(p):
            p = os.path.join(OUT, "Marilith", "Addons", name + sfx + "_" + fac + ".png")
        img = Image.open(p); m = p[:-4] + "m.png"
        out = tint(img, Image.open(m) if os.path.exists(m) else None)
        return out.transpose(Image.FLIP_LEFT_RIGHT) if facing == "west" and not os.path.exists(os.path.join(OUT, "Marilith", "Addons", name + sfx + "_west.png")) else out
    for n in ("ArmMidLeft", "ArmMidRight", "ArmLowLeft", "ArmLowRight"):
        place(addon(n), 1.4, 0)
    bodyimg = tint(Image.open(os.path.join(OUT, "Marilith", "Bodies", "Naked_%s_%s.png" % (btype, fac))))
    if facing == "west":
        bodyimg = bodyimg.transpose(Image.FLIP_LEFT_RIGHT)
    place(bodyimg, 1.0, 0)
    place(addon("Tail"), TAIL_D, TAIL_OY)
    return sheet


if __name__ == "__main__":
    out = []
    for b in ("Hulk", "Child"):
        for f in ("south", "east", "north"):
            out.append(body(b, f))
    for child in (False, True):
        sfx = "Child" if child else ""
        for slot in ("MidLeft", "MidRight", "LowLeft", "LowRight"):
            for f in ("south", "east", "north"):
                out += draw_arm(slot, f, child).save("Marilith/Addons/Arm%s%s_%s" % (slot, sfx, f), 384)
            # 西向：另一侧手臂的东向左右镜像（近侧/远侧互换）
            other = slot.replace("Left", "#").replace("Right", "Left").replace("#", "Right")
            c = draw_arm(other, "east", child)
            for img, suf in ((c.col.img, ""), (c.msk.img, "m")):
                im = img.resize((384, 384), Image.LANCZOS).transpose(Image.FLIP_LEFT_RIGHT)
                path = os.path.join(OUT, "Marilith", "Addons", "Arm%s%s_west%s.png" % (slot, sfx, suf))
                if suf:
                    bg = Image.new("RGBA", im.size, (0, 0, 0, 255)); bg.alpha_composite(im); bg.convert("RGB").save(path)
                else:
                    im.save(path)
                out.append(path)
        for f in ("south", "east", "north"):
            out += draw_tail(f, child).save("Marilith/Addons/Tail%s_%s" % (sfx, f), 512)
    icons = {"Marilith/Xenotype": xenotype_icon, "Marilith/Genes/Blades": blades_icon, "Marilith/Genes/Glide": glide_icon, "Marilith/Genes/Constrict": constrict_icon,
             "Marilith/Genes/BladeStorm": bladestorm_icon, "Marilith/Genes/AbyssalHide": hide_icon, "Marilith/Genes/ColdBlood": coldblood_icon,
             "Marilith/Abilities/Constrict": ability_constrict, "Marilith/Abilities/BladeStorm": ability_bladestorm}
    for rel, fn in icons.items():
        out.append(fn().save(rel, 256))
    # 预览：上排图标，下排三种鳞色的四向合成（成人），再一排儿童
    sheet = preview([p for p in out if "Genes" in p or "Abilities" in p or "Xenotype" in p])
    rows = [sheet]
    for btype in ("Hulk", "Child"):
        row = Image.new("RGBA", (140 * 10, 300), (58, 62, 66, 255))
        for i, (skin, f) in enumerate([((92, 138, 120), "south"), ((92, 138, 120), "east"), ((70, 98, 78), "north"), ((178, 120, 74), "west")]):
            comp = composite(btype, f, skin); comp.thumbnail((300, 300), Image.LANCZOS)
            row.alpha_composite(comp, (i * 340 + 20, 0))
        rows.append(row)
    full = Image.new("RGBA", (1400, sum(r.height for r in rows)), (58, 62, 66, 255)); y = 0
    for r in rows:
        full.alpha_composite(r, (0, y)); y += r.height
    pv = os.path.join(ROOT, "ArtSource", "Marilith-1.33.0-preview.png")
    full.convert("RGB").save(pv)
    out.append(pv)
    print(len(out), "files;", pv)
