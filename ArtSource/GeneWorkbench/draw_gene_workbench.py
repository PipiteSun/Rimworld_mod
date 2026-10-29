# 1.26.0 基因工作台（LP_GeneWorkbench）贴图：仿原版 Biotech 风格
# （俯视 3/4 视角、粗黑外轮廓、柔和渐变、灰白机壳 + 青绿玻璃 + 青色屏幕）。
# 用法：python -I ArtSource/GeneWorkbench/draw_gene_workbench.py <模组根目录> [原版贴图PNG目录(可选，用于预览对比)]
# 输出：Textures/GeneWorkbench/GeneWorkbench_{south,north,east}.png（640x640，128 像素/格，5x5）
#       Textures/GeneWorkbench/ExtractGene.png（128x128 按钮图标）
#       ArtSource/Preview/GeneWorkbench-1.26.0.png（预览对比图）
import math, os, sys, shutil
from PIL import Image, ImageDraw, ImageChops, ImageFilter, ImageFont

ROOT = sys.argv[1] if len(sys.argv) > 1 else os.getcwd()
VANILLA = sys.argv[2] if len(sys.argv) > 2 else None
TEX = os.path.join(ROOT, "Textures", "GeneWorkbench")
CELL = 128          # 原版 Biotech 建筑为 64 像素/格，这里取两倍保证清晰
N = 5 * CELL        # 5x5 建筑 → 640x640

# 调色（取自原版基因组装机/提取机/基因库贴图）
INK = (14, 14, 15)
BODY = (118, 118, 118); BODY_T = (128, 128, 128); BODY_B = (106, 105, 106)
FLOOR = (104, 103, 104); FLOOR_D = (92, 91, 92)
DARK = (83, 83, 83); DARK_B = (66, 66, 67)
PANEL_T = (204, 204, 204); PANEL_B = (172, 170, 172); PANEL_M = (150, 148, 150)
TEAL_L = (138, 168, 168); TEAL = (115, 147, 149); TEAL_D = (95, 117, 115); TEAL_DD = (66, 80, 76)
SLOT = (72, 76, 82); SLOT_B = (84, 87, 93)
SCREEN = (58, 61, 66); SCREEN_D = (40, 42, 46)
CYAN = (24, 172, 222); CYAN_L = (110, 214, 246)
AMBER = (246, 176, 64); GREEN = (118, 214, 112)


def cham(x0, y0, x1, y1, c):
    """倒角矩形 → 多边形点列。"""
    return [(x0 + c, y0), (x1 - c, y0), (x1, y0 + c), (x1, y1 - c), (x1 - c, y1), (x0 + c, y1), (x0, y1 - c), (x0, y0 + c)]


def lerp(a, b, t):
    return tuple(int(round(a[i] + (b[i] - a[i]) * t)) for i in range(3))


class Cv:
    """超采样画布：所有坐标用最终像素，内部乘以 ss。"""

    def __init__(self, n, ss):
        self.n, self.ss, self.W = n, ss, n * ss
        self.img = Image.new("RGBA", (self.W, self.W), (0, 0, 0, 0))

    # ---------- 形状 ----------
    @staticmethod
    def bbox(sh):
        if sh[0] == "pg":
            xs = [p[0] for p in sh[1]]; ys = [p[1] for p in sh[1]]
            return min(xs), min(ys), max(xs), max(ys)
        return sh[1:5]

    def draw_shape(self, d, sh, ox, oy, fill=None, outline=None, width=0):
        s = self.ss
        if sh[0] == "pg":
            pts = [(x * s - ox, y * s - oy) for x, y in sh[1]]
            if fill is not None:
                d.polygon(pts, fill=fill)
            if width:
                d.line(pts + [pts[0], pts[1]], fill=outline, width=int(width), joint="curve")
        else:
            box = [sh[1] * s - ox, sh[2] * s - oy, sh[3] * s - ox, sh[4] * s - oy]
            if sh[0] == "rr":
                r = min(sh[5] * s, (box[2] - box[0]) / 2 - 1, (box[3] - box[1]) / 2 - 1)
                d.rounded_rectangle(box, radius=max(r, 0), fill=fill, outline=outline, width=int(width))
            elif sh[0] == "el":
                d.ellipse(box, fill=fill, outline=outline, width=int(width))

    def fill_img(self, fill, w, h):
        if isinstance(fill[0], int):
            return Image.new("RGB", (w, h), fill)
        kind = fill[0]
        if kind in ("v", "h"):
            g = Image.linear_gradient("L").resize((w, h) if kind == "v" else (h, w), Image.BILINEAR)
            if kind == "h":
                g = g.rotate(90, expand=True).transpose(Image.FLIP_LEFT_RIGHT)
            if len(fill) > 3:   # 三段渐变
                g1 = g.point(lambda v: min(255, v * 2))
                g2 = g.point(lambda v: max(0, v * 2 - 255))
                a = Image.composite(Image.new("RGB", (w, h), fill[2]), Image.new("RGB", (w, h), fill[1]), g1)
                return Image.composite(Image.new("RGB", (w, h), fill[3]), a, g2)
            return Image.composite(Image.new("RGB", (w, h), fill[2]), Image.new("RGB", (w, h), fill[1]), g)
        if kind == "rad":  # ("rad", 中心色, 边缘色, 中心x比例, 中心y比例, 半径比例)
            _, c0, c1, fx, fy, sc = fill
            R = int(max(w, h) * sc)
            rg = Image.radial_gradient("L").resize((2 * R, 2 * R), Image.BILINEAR)
            g = Image.new("L", (w, h), 255)
            g.paste(rg, (int(fx * w - R), int(fy * h - R)))
            return Image.composite(Image.new("RGB", (w, h), c1), Image.new("RGB", (w, h), c0), g)
        raise ValueError(fill)

    def part(self, sh, fill, ow=2.5, oc=INK, bevel=(0.22, 0.22), bw=2.5, shadow=0, sdx=3, sdy=6, alpha=255):
        """填充 + 上沿高光/下沿暗边 + 墨线；shadow>0 时先在已有像素上投一层柔和阴影。"""
        s = self.ss
        if sh[0] == "ch":
            sh = ("pg", cham(*sh[1:6]))
        x0, y0, x1, y1 = self.bbox(sh)
        pad = int((max(ow, bw, 2) + (sdy + 12 if shadow else 0) + 4) * s)
        bx0 = max(0, int(x0 * s) - pad); by0 = max(0, int(y0 * s) - pad)
        bx1 = min(self.W, int(x1 * s) + pad); by1 = min(self.W, int(y1 * s) + pad)
        w, h = bx1 - bx0, by1 - by0
        if w <= 0 or h <= 0:
            return
        m = Image.new("L", (w, h), 0)
        self.draw_shape(ImageDraw.Draw(m), sh, bx0, by0, fill=255)
        m0 = m
        if alpha < 255:
            m = m.point(lambda v: v * alpha // 255)
        crop = self.img.crop((bx0, by0, bx1, by1))
        if shadow:
            sm = ImageChops.offset(m0, int(sdx * s), int(sdy * s)).filter(ImageFilter.GaussianBlur(5 * s))
            sm = ImageChops.multiply(sm, crop.getchannel("A")).point(lambda v: int(v * shadow))
            sl = Image.new("RGBA", (w, h), (10, 10, 14, 0)); sl.putalpha(sm)
            crop.alpha_composite(sl)
        fw, fh = max(1, int((x1 - x0) * s)), max(1, int((y1 - y0) * s))
        fi = Image.new("RGB", (w, h), (0, 0, 0))
        fi.paste(self.fill_img(fill, fw, fh), (int(x0 * s) - bx0, int(y0 * s) - by0))
        layer = fi.convert("RGBA"); layer.putalpha(m)
        if bevel:
            b = int(bw * s)
            for amt, dy in ((bevel[0], b), (bevel[1], -b)):
                if not amt:
                    continue
                band = ImageChops.subtract(m, ImageChops.offset(m, 0, dy)).filter(ImageFilter.GaussianBlur(b * 0.35))
                band = ImageChops.multiply(band, m).point(lambda v, a=abs(amt): int(v * a))
                col = (255, 255, 255) if (amt > 0) == (dy > 0) else (0, 0, 0)
                bl = Image.new("RGBA", (w, h), col + (0,)); bl.putalpha(band)
                layer.alpha_composite(bl)
        crop.alpha_composite(layer)
        if ow:
            ol = Image.new("RGBA", (w, h), (0, 0, 0, 0))
            self.draw_shape(ImageDraw.Draw(ol), sh, bx0, by0, outline=oc + (255,), width=ow * s)
            ol.putalpha(ImageChops.multiply(ol.getchannel("A"), m.point(lambda v: 255 if v else 0)))
            crop.alpha_composite(ol)
        self.img.paste(crop, (bx0, by0))

    def line(self, pts, w, col, ow=2.5, oc=INK, hl=True):
        """软管/线缆：墨线外框 + 本色 + 一条细高光。"""
        s = self.ss
        d = ImageDraw.Draw(self.img)
        P = [(x * s, y * s) for x, y in pts]
        for width, c in ((w + 2 * ow, oc), (w, col)):
            d.line(P, fill=c + (255,), width=int(width * s), joint="curve")
            r = width * s / 2
            for x, y in (P[0], P[-1]):
                d.ellipse([x - r, y - r, x + r, y + r], fill=c + (255,))
        if hl:
            hcol = lerp(col, (255, 255, 255), 0.35)
            Q = [(x, y - w * s * 0.22) for x, y in P]
            d.line(Q, fill=hcol + (255,), width=max(1, int(w * s * 0.22)), joint="curve")

    def rect(self, box, col, r=0):
        self.part(("rr",) + tuple(box) + (r,), col, ow=0, bevel=None)

    def light(self, cx, cy, r, col):
        """指示灯：外发光 + 墨圈 + 亮点。"""
        s = self.ss
        gl = Image.new("RGBA", (self.W, self.W), col + (0,))
        R = int(r * 3.2 * s)
        m = Image.new("L", (2 * R, 2 * R), 0)
        ImageDraw.Draw(m).ellipse([R * 0.45, R * 0.45, R * 1.55, R * 1.55], fill=110)
        m = m.filter(ImageFilter.GaussianBlur(R * 0.22))
        a = Image.new("L", (self.W, self.W), 0); a.paste(m, (int(cx * s) - R, int(cy * s) - R))
        a = ImageChops.multiply(a, self.img.getchannel("A"))
        gl.putalpha(a); self.img.alpha_composite(gl)
        self.part(("el", cx - r, cy - r, cx + r, cy + r), ("rad", lerp(col, (255, 255, 255), 0.6), col, 0.38, 0.35, 0.9), ow=1.5, bevel=None)

    def finish(self, outline=5.0):
        """外轮廓：对整体 alpha 做膨胀，垫一层黑边；再缩小到最终尺寸。"""
        s = self.ss
        a = self.img.getchannel("A").point(lambda v: 255 if v > 8 else 0)
        R = int(outline * s * 1.25)
        g = a.filter(ImageFilter.BoxBlur(R)).point(lambda v: 255 if v > 24 else 0)
        g = g.filter(ImageFilter.GaussianBlur(s * 0.6))
        base = Image.new("RGBA", (self.W, self.W), INK + (0,)); base.putalpha(g)
        base.alpha_composite(self.img)
        return base.resize((self.n, self.n), Image.LANCZOS)


# ---------------- 部件 ----------------

def block(cv, x0, y0, x1, y1, h, top, face=None, c=10, shadow=0.35, ow=2.5):
    """3/4 视角的抬高方块：先画正面（下方），再画顶面。"""
    face = face or ("v", DARK, DARK_B)
    if shadow:
        cv.part(("ch", x0, y0, x1, y1 + h, c), (0, 0, 0), ow=0, bevel=None, shadow=shadow, alpha=0)
    # 正面 = 顶面倒角轮廓向下挤出 h
    cv.part(("pg", [(x0, y1 - c), (x1, y1 - c), (x1, y1 + h - c), (x1 - c, y1 + h), (x0 + c, y1 + h), (x0, y1 + h - c)]), face, ow=ow, bevel=(0.10, 0.0))
    cv.part(("ch", x0, y0, x1, y1, c), top, ow=ow, bevel=(0.30, 0.18))


def helix_icon(cv, x0, y0, x1, y1, col=CYAN, vertical=False, w=2.2):
    """屏幕上的 DNA 双螺旋小图标。"""
    pts1, pts2, rungs = [], [], []
    n = 28
    for i in range(n + 1):
        t = i / n
        if not vertical:
            x = x0 + (x1 - x0) * t; ph = math.sin(t * math.pi * 2)
            cy = (y0 + y1) / 2; a = (y1 - y0) / 2
            pts1.append((x, cy + a * ph)); pts2.append((x, cy - a * ph))
            if i % 4 == 2:
                rungs.append(((x, cy + a * ph * 0.8), (x, cy - a * ph * 0.8)))
        else:
            y = y0 + (y1 - y0) * t; ph = math.sin(t * math.pi * 2)
            cx = (x0 + x1) / 2; a = (x1 - x0) / 2
            pts1.append((cx + a * ph, y)); pts2.append((cx - a * ph, y))
            if i % 4 == 2:
                rungs.append(((cx + a * ph * 0.8, y), (cx - a * ph * 0.8, y)))
    d = ImageDraw.Draw(cv.img); s = cv.ss
    for a, b in rungs:
        d.line([(a[0] * s, a[1] * s), (b[0] * s, b[1] * s)], fill=lerp(col, SCREEN, 0.45) + (255,), width=int(w * 0.7 * s))
    for P in (pts1, pts2):
        d.line([(x * s, y * s) for x, y in P], fill=col + (255,), width=int(w * s), joint="curve")


def screen(cv, x0, y0, x1, y1, kind="helix", vertical=False):
    cv.part(("rr", x0, y0, x1, y1, 5), ("v", SCREEN, SCREEN_D), ow=2.5, bevel=(-0.25, 0.12), bw=3)
    # 屏幕玻璃反光
    cv.part(("pg", [(x0 + 4, y0 + 4), (x0 + (x1 - x0) * 0.42, y0 + 4), (x0 + (x1 - x0) * 0.22, y1 - 4), (x0 + 4, y1 - 4)]), (255, 255, 255), ow=0, bevel=None, alpha=14)
    pw, ph = x1 - x0, y1 - y0
    if kind == "helix":
        if not vertical:
            helix_icon(cv, x0 + pw * 0.12, y0 + ph * 0.22, x0 + pw * 0.48, y1 - ph * 0.22)
            for i, f in enumerate((0.75, 0.55, 0.35)):
                yy = y0 + ph * (0.3 + 0.2 * i)
                cv.rect((x0 + pw * 0.56, yy - 1.6, x0 + pw * (0.56 + 0.34 * f), yy + 1.6), CYAN)
        else:
            helix_icon(cv, x0 + pw * 0.22, y0 + ph * 0.1, x1 - pw * 0.22, y0 + ph * 0.5, vertical=True)
            for i, f in enumerate((0.75, 0.55, 0.35)):
                yy = y0 + ph * (0.62 + 0.11 * i)
                cv.rect((x0 + pw * 0.2, yy - 1.6, x0 + pw * (0.2 + 0.6 * f), yy + 1.6), CYAN)
    elif kind == "text":
        rows = max(2, int(ph / 12))
        for i in range(rows):
            yy = y0 + ph * (i + 0.8) / (rows + 0.6)
            f = (0.8, 0.5, 0.65, 0.35, 0.7, 0.45)[i % 6]
            cv.rect((x0 + pw * 0.14, yy - 1.5, x0 + pw * (0.14 + 0.72 * f), yy + 1.5), CYAN if i else CYAN_L)
    elif kind == "bars":
        n = 5
        for i in range(n):
            bh = ph * (0.25 + 0.5 * ((i * 37) % 7) / 6)
            xx = x0 + pw * (0.15 + 0.7 * i / (n - 1))
            cv.rect((xx - 3, y1 - ph * 0.15 - bh, xx + 3, y1 - ph * 0.15), CYAN)


def base(cv):
    # 底座：正面（前沿）+ 顶面 + 下沉内底
    cv.part(("ch", 8, 560, 632, 630, 20), ("v", DARK, DARK_B), ow=0, bevel=(0.1, 0))
    cv.part(("ch", 8, 10, 632, 606, 20), ("v", BODY_T, BODY_B), ow=0, bevel=(0.28, 0.2), bw=3)
    cv.part(("ch", 26, 26, 614, 590, 14), ("v", FLOOR, FLOOR_D), ow=2.2, bevel=(-0.35, 0.12), bw=4)
    # 地面检修格栅线
    for i in range(1, 5):
        x = 26 + (614 - 26) * i / 5
        cv.rect((x - 1, 32, x + 1, 584), (96, 95, 96))


def genebank(cv, x0, y0, x1, y1, h, filled=(1, 1, 0, 1, 1, 1, 0, 1)):
    vertical = (y1 - y0) > (x1 - x0)
    block(cv, x0, y0, x1, y1, h, ("v", PANEL_T, PANEL_B), c=14)
    # 两侧青绿把手条（仿原版基因库）
    if not vertical:
        for yy in (y0 + 4, y1 - 12):
            cv.part(("rr", x0 + 60, yy, x1 - 60, yy + 9, 4.5), ("v", TEAL_L, TEAL_D), ow=2, bevel=None)
        ix0, iy0, ix1, iy1 = x0 + 26, y0 + 20, x1 - 26, y1 - 20
    else:
        for xx in (x0 + 4, x1 - 13):
            cv.part(("rr", xx, y0 + 60, xx + 9, y1 - 60, 4.5), ("h", TEAL_L, TEAL_D), ow=2, bevel=None)
        ix0, iy0, ix1, iy1 = x0 + 20, y0 + 26, x1 - 20, y1 - 26
    cv.part(("rr", ix0, iy0, ix1, iy1, 6), ("v", SLOT, SLOT_B), ow=2.5, bevel=(-0.4, 0.1), bw=4)
    cols, rows = (4, 2) if not vertical else (2, 4)
    gw, gh = (ix1 - ix0 - 16) / cols, (iy1 - iy0 - 16) / rows
    for k in range(cols * rows):
        cx, ry = (k % cols, k // cols)
        sx0 = ix0 + 8 + cx * gw + 4; sy0 = iy0 + 8 + ry * gh + 4
        sx1 = sx0 + gw - 8; sy1 = sy0 + gh - 8
        if filled[k]:
            cv.part(("rr", sx0, sy0, sx1, sy1, 5), ("v", (214, 214, 214), (170, 168, 170)), ow=2, bevel=(0.35, 0.2))
            # 基因包上的青色小窗
            if not vertical:
                cv.rect((sx0 + 8, (sy0 + sy1) / 2 - 2, sx0 + 8 + (sx1 - sx0) * 0.35, (sy0 + sy1) / 2 + 2), TEAL, r=2)
            else:
                cv.rect(((sx0 + sx1) / 2 - 2, sy0 + 8, (sx0 + sx1) / 2 + 2, sy0 + 8 + (sy1 - sy0) * 0.35), TEAL, r=2)
        else:
            cv.part(("rr", sx0, sy0, sx1, sy1, 5), ("v", (52, 55, 60), (64, 67, 72)), ow=1.5, bevel=(-0.3, 0.1))
    # 端头状态灯
    if not vertical:
        cv.light(ix0 - 12, (iy0 + iy1) / 2, 4, GREEN)
        cv.light(ix1 + 12, (iy0 + iy1) / 2, 4, CYAN_L)
    else:
        cv.light((ix0 + ix1) / 2, iy0 - 12, 4, GREEN)
        cv.light((ix0 + ix1) / 2, iy1 + 12, 4, CYAN_L)


def pod(cv, x0, y0, x1, y1, h, head="top"):
    """中央躺式提取舱：青绿玻璃舱壳 + 浅色软垫，中心区域保持朴素（小人会躺在上面）。"""
    horizontal = head in ("left", "right")
    block(cv, x0, y0, x1, y1, h, ("v", TEAL_L, TEAL_D), face=("v", TEAL_D, TEAL_DD), c=26)
    # 舱壳内沿暗线
    cv.part(("ch", x0 + 10, y0 + 10, x1 - 10, y1 - 10, 20), ("v", TEAL_D, (84, 106, 104)), ow=2, bevel=(-0.35, 0.15), bw=4)
    # 玻璃高光
    if not horizontal:
        cv.part(("rr", x0 + 7, y0 + 40, x0 + 13, y1 - 60, 3), (220, 236, 236), ow=0, bevel=None, alpha=120)
    else:
        cv.part(("rr", x0 + 40, y0 + 6, x1 - 60, y0 + 11, 3), (220, 236, 236), ow=0, bevel=None, alpha=120)
    L = (y1 - y0) if not horizontal else (x1 - x0)
    Wd = (x1 - x0) if not horizontal else (y1 - y0)

    def seg(a, b, inset):
        # 沿长轴 a..b（0~1），短轴内缩 inset 像素
        if not horizontal:
            if head == "top":
                return (x0 + inset, y0 + L * a, x1 - inset, y0 + L * b)
            return (x0 + inset, y1 - L * b, x1 - inset, y1 - L * a)
        if head == "left":
            return (x0 + L * a, y0 + inset, x0 + L * b, y1 - inset)
        return (x1 - L * b, y0 + inset, x1 - L * a, y1 - inset)

    pad = ("rad", (214, 214, 214), (176, 174, 176), 0.42, 0.38, 0.95)
    cv.part(("rr",) + seg(0.07, 0.24, 30) + (14,), pad, ow=2.2, bevel=(0.35, 0.22), bw=3)    # 头枕
    cv.part(("rr",) + seg(0.26, 0.76, 22) + (12,), pad, ow=2.2, bevel=(0.35, 0.22), bw=3)    # 主垫
    cv.part(("rr",) + seg(0.78, 0.94, 36) + (12,), pad, ow=2.2, bevel=(0.35, 0.22), bw=3)    # 脚垫
    # 主垫缝线（很淡，保持中心朴素）
    for f in (0.43, 0.59):
        bx = seg(f, f, 34)
        if not horizontal:
            cv.rect((bx[0], bx[1] - 1, bx[2], bx[1] + 1), (150, 148, 150))
        else:
            cv.rect((bx[0] - 1, bx[1], bx[0] + 1, bx[3]), (150, 148, 150))
    # 头端扫描环（青色细弧）
    hb = seg(0.0, 0.07, 40)
    cv.part(("rr",) + hb + (4,), ("v", SCREEN, SCREEN_D), ow=1.8, bevel=None)
    if not horizontal:
        cv.rect((hb[0] + 8, (hb[1] + hb[3]) / 2 - 1.5, hb[2] - 8, (hb[1] + hb[3]) / 2 + 1.5), CYAN, r=1)
    else:
        cv.rect(((hb[0] + hb[2]) / 2 - 1.5, hb[1] + 8, (hb[0] + hb[2]) / 2 + 1.5, hb[3] - 8), CYAN, r=1)


def dome(cv, x0, y0, x1, y1, h):
    """组装机式全息球罩：灰座 + 青绿玻璃碗 + 浅灰球顶 + 两侧散热栅。"""
    block(cv, x0, y0, x1, y1, h, ("v", BODY_T, BODY_B), c=16)
    cx, cy = (x0 + x1) / 2, (y0 + y1) / 2
    r = min(x1 - x0, y1 - y0) * 0.43
    # 散热栅（白色横条，仿原版）
    vertical = (y1 - y0) >= (x1 - x0)
    if vertical:
        for gy in (y0 + 18, y1 - 58):
            cv.part(("rr", cx - r * 0.75, gy, cx + r * 0.75, gy + 40, 3), ("v", DARK, DARK_B), ow=2, bevel=None)
            for i in range(5):
                yy = gy + 6 + i * 7
                cv.rect((cx - r * 0.65, yy, cx + r * 0.65, yy + 3.5), (206, 206, 206))
    else:
        for gx in (x0 + 18, x1 - 58):
            cv.part(("rr", gx, cy - r * 0.75, gx + 40, cy + r * 0.75, 3), ("v", DARK, DARK_B), ow=2, bevel=None)
            for i in range(5):
                xx = gx + 6 + i * 7
                cv.rect((xx, cy - r * 0.65, xx + 3.5, cy + r * 0.65), (206, 206, 206))
    # 玻璃碗（球下方露出的一圈青绿）
    cv.part(("el", cx - r * 1.08, cy - r * 0.78, cx + r * 1.08, cy + r * 1.22), ("v", TEAL, TEAL_DD), ow=2.5, bevel=None, shadow=0.4)
    cv.part(("el", cx - r * 0.86, cy + r * 0.62, cx - r * 0.68, cy + r * 0.80), (200, 226, 226), ow=0, bevel=None, alpha=150)
    # 球顶
    cv.part(("el", cx - r, cy - r * 1.05, cx + r, cy + r * 0.95), ("rad", (222, 222, 222), (150, 148, 150), 0.38, 0.32, 1.05), ow=2.5, bevel=None, shadow=0.35)
    # 球顶上的青色全息环
    d = ImageDraw.Draw(cv.img); s = cv.ss
    d.ellipse([(cx - r * 0.42) * s, (cy - r * 0.50) * s, (cx + r * 0.42) * s, (cy + r * 0.30) * s], outline=CYAN + (255,), width=int(2.4 * s))
    cv.part(("el", cx - r * 0.18, cy - r * 0.30, cx + r * 0.18, cy + r * 0.08), ("rad", (190, 190, 190), (120, 120, 122), 0.4, 0.35, 1.0), ow=1.8, bevel=None)


def console(cv, x0, y0, x1, y1, h, back=False):
    """侧边控制塔：正面有屏幕和指示灯；背面为散热口。"""
    block(cv, x0, y0, x1, y1, h, ("v", BODY_T, BODY_B), c=16)
    vertical = (y1 - y0) >= (x1 - x0)
    if not back:
        cv.part(("ch", x0 + 14, y0 + 14, x1 - 14, y1 - 14, 10), ("v", PANEL_T, PANEL_B), ow=2.2, bevel=(0.3, 0.2))
        if vertical:
            screen(cv, x0 + 26, y0 + 28, x1 - 26, y0 + (y1 - y0) * 0.52, "helix", vertical=True)
            screen(cv, x0 + 26, y0 + (y1 - y0) * 0.57, x1 - 26, y1 - 62, "bars")
            for i, c in enumerate((AMBER, GREEN, GREEN, CYAN_L)):
                cv.light(x0 + 40 + i * ((x1 - x0 - 80) / 3), y1 - 38, 5, c)
        else:
            screen(cv, x0 + 28, y0 + 26, x0 + (x1 - x0) * 0.52, y1 - 26, "helix")
            screen(cv, x0 + (x1 - x0) * 0.57, y0 + 26, x1 - 62, y1 - 26, "bars")
            for i, c in enumerate((AMBER, GREEN, GREEN, CYAN_L)):
                cv.light(x1 - 38, y0 + 40 + i * ((y1 - y0 - 80) / 3), 5, c)
    else:
        cv.part(("ch", x0 + 14, y0 + 14, x1 - 14, y1 - 14, 10), ("v", (124, 124, 124), (108, 108, 108)), ow=2.2, bevel=(0.25, 0.15))
        if vertical:
            for k in range(6):
                yy = y0 + 36 + k * (y1 - y0 - 80) / 5.6
                cv.part(("rr", x0 + 30, yy, x1 - 30, yy + 14, 6), ("v", DARK_B, DARK), ow=2, bevel=None)
        else:
            for k in range(6):
                xx = x0 + 36 + k * (x1 - x0 - 80) / 5.6
                cv.part(("rr", xx, y0 + 30, xx + 14, y1 - 30, 6), ("v", DARK_B, DARK), ow=2, bevel=None)
        cv.light((x0 + x1) / 2, y1 - 26 if vertical else (y0 + y1) / 2, 4, AMBER)


def bench(cv, x0, y0, x1, y1, h, side="front"):
    """前方长操作台：屏幕 + 键盘 + 指示灯；背面为平板与格栅。"""
    block(cv, x0, y0, x1, y1, h, ("v", BODY_T, BODY_B), c=14)
    vertical = (y1 - y0) > (x1 - x0)
    cv.part(("ch", x0 + 10, y0 + 10, x1 - 10, y1 - 10, 8), ("v", PANEL_T, PANEL_B), ow=2.2, bevel=(0.3, 0.22))
    if side == "back":
        if not vertical:
            for gx0 in (x0 + 40, x1 - 200):
                cv.part(("rr", gx0, y0 + 22, gx0 + 160, y1 - 22, 4), ("v", DARK, DARK_B), ow=2, bevel=None)
                for i in range(int(160 / 9)):
                    xx = gx0 + 8 + i * 9
                    cv.rect((xx, y0 + 27, xx + 4, y1 - 27), (190, 190, 190))
            cv.part(("rr", (x0 + x1) / 2 - 60, y0 + 24, (x0 + x1) / 2 + 60, y1 - 24, 6), ("v", TEAL_L, TEAL_D), ow=2, bevel=(0.3, 0.2))
        return
    if not vertical:
        L = x1 - x0
        screen(cv, x0 + 24, y0 + 18, x0 + L * 0.30, y1 - 18, "helix")
        # 键盘区
        kx0, kx1 = x0 + L * 0.34, x0 + L * 0.66
        cv.part(("rr", kx0, y0 + 18, kx1, y1 - 18, 5), ("v", DARK, DARK_B), ow=2.2, bevel=(-0.3, 0.1))
        rows, cols = 3, 12
        kw, kh = (kx1 - kx0 - 12) / cols, (y1 - y0 - 48) / rows
        for r in range(rows):
            for c in range(cols):
                xx = kx0 + 6 + c * kw; yy = y0 + 24 + r * kh
                col = (196, 196, 196)
                if (r, c) in ((0, 0), (0, 1)):
                    col = AMBER
                elif (r, c) in ((0, 10), (0, 11)):
                    col = GREEN
                cv.part(("rr", xx + 1, yy + 1, xx + kw - 1.5, yy + kh - 1.5, 1.5), ("v", lerp(col, (255, 255, 255), 0.2), lerp(col, (0, 0, 0), 0.15)), ow=0, bevel=None)
        screen(cv, x0 + L * 0.70, y0 + 18, x1 - 24, y1 - 18, "text")
    else:
        L = y1 - y0
        screen(cv, x0 + 16, y0 + 24, x1 - 16, y0 + L * 0.30, "helix", vertical=True)
        kx0, kx1 = x0 + 16, x1 - 16
        ky0, ky1 = y0 + L * 0.34, y0 + L * 0.66
        cv.part(("rr", kx0, ky0, kx1, ky1, 5), ("v", DARK, DARK_B), ow=2.2, bevel=(-0.3, 0.1))
        rows, cols = 12, 3
        kw, kh = (kx1 - kx0 - 12) / cols, (ky1 - ky0 - 12) / rows
        for r in range(rows):
            for c in range(cols):
                xx = kx0 + 6 + c * kw; yy = ky0 + 6 + r * kh
                col = (196, 196, 196)
                if (r, c) in ((0, 0), (1, 0)):
                    col = AMBER
                elif (r, c) in ((10, 0), (11, 0)):
                    col = GREEN
                cv.part(("rr", xx + 1, yy + 1, xx + kw - 1.5, yy + kh - 1.5, 1.5), ("v", lerp(col, (255, 255, 255), 0.2), lerp(col, (0, 0, 0), 0.15)), ow=0, bevel=None)
        screen(cv, x0 + 16, y0 + L * 0.70, x1 - 16, y1 - 24, "text")


# ---------------- 布局（以朝南为基准，北/东通过平面旋转得到） ----------------
CX, CY = 320, 308   # 顶面中心（底部有正面厚度，因此略偏上）
SOUTH = {
    "bank": (40, 28, 600, 136),
    "dome": (36, 166, 190, 470),
    "cons": (450, 166, 604, 470),
    "pod": (204, 160, 436, 480),
    "bench": (36, 500, 604, 562),
}


def xf(rect, rot):
    x0, y0, x1, y1 = rect
    if rot == "south":
        return rect
    if rot == "north":
        return (2 * CX - x1, 2 * CY - y1, 2 * CX - x0, 2 * CY - y0)
    # east：朝南时的前方 → 右侧，左侧 → 下方
    return (CX + (y0 - CY), CY - (x1 - CX), CX + (y1 - CY), CY - (x0 - CX))


def pipes(cv, rot):
    """连接各模块的青绿软管（先画，压在模块下面）。"""
    def P(x, y):
        if rot == "south":
            return (x, y)
        if rot == "north":
            return (2 * CX - x, 2 * CY - y)
        return (CX + (y - CY), CY - (x - CX))
    routes = []
    for y in (250, 400):
        routes += [[(176, y), (224, y)], [(416, y), (464, y)]]
    for r in routes:
        cv.line([P(x, y) for x, y in r], 11, TEAL)


def draw_building(rot):
    cv = Cv(N, 4)
    base(cv)
    pipes(cv, rot)
    L = {k: xf(v, rot) for k, v in SOUTH.items()}
    # 绘制顺序：从屏幕上方到下方（越靠下越“近”，后画）
    items = []
    head = {"south": "top", "north": "bottom", "east": "left"}[rot]
    items.append((L["bank"], lambda r: genebank(cv, *r, 16)))
    items.append((L["dome"], lambda r: dome(cv, *r, 16)))
    items.append((L["cons"], lambda r: console(cv, *r, 16, back=(rot == "north"))))
    items.append((L["pod"], lambda r: pod(cv, *r, 12, head=head)))
    items.append((L["bench"], lambda r: bench(cv, *r, 18, side=("back" if rot == "north" else "front"))))
    items.sort(key=lambda it: (it[0][3], it[0][1]))
    for r, fn in items:
        fn(r)
    return cv.finish(5.0)


# ---------------- 按钮图标：指定提取基因 ----------------

def draw_gizmo():
    cv = Cv(128, 8)
    # 左侧：双螺旋片段（青色链 + 灰色碱基对），中间缺一对
    n = 40
    s1, s2 = [], []
    for i in range(n + 1):
        t = i / n
        y = 10 + 108 * t
        ph = math.sin(t * math.pi * 2 + 0.6)
        s1.append((34 + 20 * ph, y)); s2.append((34 - 20 * ph, y))
    for k in range(7):
        t = (k + 0.5) / 7
        y = 10 + 108 * t
        ph = math.sin(t * math.pi * 2 + 0.6) * 0.82
        if k == 3:
            continue
        cv.part(("rr", 34 - abs(20 * ph), y - 3.5, 34 + abs(20 * ph), y + 3.5, 2), ("v", (150, 166, 166), (106, 122, 122)), ow=2.2, bevel=None)
    cv.line(s2, 7, (18, 150, 200), ow=2.8)
    cv.line(s1, 7, CYAN, ow=2.8)
    # 被抽出的那一段基因（亮色胶囊）
    cv.part(("rr", 44, 56, 74, 72, 7), ("v", (250, 230, 150), (226, 168, 60)), ow=3, bevel=(0.35, 0.2), bw=2, shadow=0.0)
    # 弧形箭头：从基因指向右下的基因包
    arc = [(76, 52 - 0), (92, 40), (108, 44), (112, 58)]
    cv.line(arc, 8, (224, 224, 224), ow=3)
    cv.part(("pg", [(100, 58), (124, 58), (112, 74)]), ("v", (236, 236, 236), (196, 196, 196)), ow=3, bevel=None)
    # 右下：基因包（灰色卡匣 + 青绿窗 + 青色螺旋标）
    cv.part(("rr", 76, 78, 124, 122, 8), ("v", (206, 206, 206), (148, 146, 148)), ow=3.2, bevel=(0.35, 0.25), bw=2.5, shadow=0.0)
    cv.part(("rr", 84, 88, 116, 112, 5), ("v", TEAL_L, TEAL_DD), ow=2.4, bevel=(-0.3, 0.15), bw=2)
    helix_icon(cv, 89, 94, 111, 106, col=CYAN_L, w=2.2)
    return cv.finish(2.2)


# ---------------- 预览对比图 ----------------

def floor_panel(cells_w, cells_h, cell, pad=24):
    W, H = cells_w * cell + 2 * pad, cells_h * cell + 2 * pad
    im = Image.new("RGBA", (W, H), (96, 92, 84, 255))
    d = ImageDraw.Draw(im)
    for i in range(cells_w):
        for j in range(cells_h):
            x, y = pad + i * cell, pad + j * cell
            tone = 112 if (i + j) % 2 == 0 else 108
            d.rectangle([x, y, x + cell - 1, y + cell - 1], fill=(tone, tone - 3, tone - 9, 255), outline=(86, 83, 77, 255), width=2)
    return im, pad


def font(sz):
    for f in (r"C:\Windows\Fonts\msyh.ttc", r"C:\Windows\Fonts\simhei.ttf"):
        if os.path.exists(f):
            return ImageFont.truetype(f, sz)
    return ImageFont.load_default()


def preview(views, gizmo, out):
    F = font(26); Fs = font(20)
    sheet = Image.new("RGBA", (2200, 1520), (54, 52, 48, 255))
    d = ImageDraw.Draw(sheet)
    d.text((24, 14), "基因工作台 LP_GeneWorkbench（5x5，128 像素/格）—— 朝南 / 朝北 / 朝东（朝西由游戏镜像）", font=F, fill=(230, 226, 214))
    x = 24
    for name, im in views:
        pnl, pad = floor_panel(5, 5, CELL)
        pnl.alpha_composite(im, (pad, pad))
        sheet.alpha_composite(pnl, (x, 60))
        d.text((x + 6, 60 + pnl.height + 6), name, font=Fs, fill=(220, 216, 204))
        x += pnl.width + 24
    y2 = 60 + 688 + 50
    x = 24
    # 图标
    pnl, pad = floor_panel(2, 2, 128)
    pnl.alpha_composite(gizmo.resize((256, 256), Image.LANCZOS), (pad, pad))
    sheet.alpha_composite(pnl, (x, y2))
    d.text((x + 6, y2 + pnl.height + 6), "按钮：指定提取基因（放大2倍）", font=Fs, fill=(220, 216, 204))
    gx = x + pnl.width + 20
    for i, bg in enumerate(((40, 40, 40), (90, 90, 90))):
        d.rectangle([gx, y2 + i * 150, gx + 135, y2 + i * 150 + 135], fill=bg + (255,))
        sheet.alpha_composite(gizmo, (gx + 4, y2 + i * 150 + 4))
    d.text((gx, y2 + 300), "原尺寸", font=Fs, fill=(220, 216, 204))
    x = gx + 160
    # 游戏缩放检查：25% 缩小
    small = views[0][1].resize((160, 160), Image.LANCZOS)
    pnl, pad = floor_panel(5, 5, 32, pad=12)
    pnl.alpha_composite(small, (pad, pad))
    sheet.alpha_composite(pnl, (x, y2))
    d.text((x, y2 + pnl.height + 6), "25% 缩小", font=Fs, fill=(220, 216, 204))
    x += pnl.width + 24
    # 原版对比（统一到 128 像素/格）
    if VANILLA and os.path.isdir(VANILLA):
        for fn, cw, ch, label in (("GeneAssembler_south.png", 3, 2, "原版 基因组装机"), ("GeneExtractor_south.png", 2, 2, "原版 基因提取机"), ("Genebank_south.png", 2, 1, "原版 基因库")):
            p = os.path.join(VANILLA, fn)
            if not os.path.exists(p):
                continue
            v = Image.open(p).convert("RGBA").resize((cw * CELL, ch * CELL), Image.BICUBIC)
            pnl, pad = floor_panel(cw, ch, CELL)
            pnl.alpha_composite(v, (pad, pad))
            if x + pnl.width > sheet.width:
                break
            sheet.alpha_composite(pnl, (x, y2))
            d.text((x + 6, y2 + pnl.height + 6), label, font=Fs, fill=(220, 216, 204))
            x += pnl.width + 24
    sheet = sheet.crop((0, 0, max(2200, x), y2 + 420))
    os.makedirs(os.path.dirname(out), exist_ok=True)
    sheet.save(out)


def main():
    os.makedirs(TEX, exist_ok=True)
    views = []
    for rot, label in (("south", "朝南 south"), ("north", "朝北 north"), ("east", "朝东 east")):
        im = draw_building(rot)
        im.save(os.path.join(TEX, "GeneWorkbench_%s.png" % rot))
        views.append((label, im))
    gz = draw_gizmo()
    gz.save(os.path.join(TEX, "ExtractGene.png"))
    out = os.path.join(ROOT, "ArtSource", "Preview", "GeneWorkbench-1.26.0.png")
    preview(views, gz, out)
    desk = os.path.join(os.path.expanduser("~"), "Desktop")
    if os.path.isdir(desk):
        shutil.copy(out, os.path.join(desk, "GeneWorkbench-1.26.0.png"))
    print("done:", out)


if __name__ == "__main__":
    main()
