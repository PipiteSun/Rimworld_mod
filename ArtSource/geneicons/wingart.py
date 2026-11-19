# 2026-11-17 翅膀贴图重画样张（Peter：DND 的翅膀只是几个几何图形拼的，细节比 Alpha Genes 少）。
# 256×256，蝠翼：弯曲的臂骨、三根指骨、扇贝形的膜缘、腕爪、每片翼膜深浅两个色调、粗黑外轮廓。
# 用法：python wingart.py <输出目录>   → SuccubusWings_south/north/east.png + 对比图 compare.png（需要 --compare 旧图和参考图路径）
import sys, os, math
from PIL import Image, ImageDraw

K = 4
N = 256 * K


def q(p0, c, p1, n=18):
    return [((1 - t) ** 2 * p0[0] + 2 * (1 - t) * t * c[0] + t * t * p1[0],
             (1 - t) ** 2 * p0[1] + 2 * (1 - t) * t * c[1] + t * t * p1[1]) for t in [i / n for i in range(n + 1)]]


def toward(a, b, f):
    return (a[0] + (b[0] - a[0]) * f, a[1] + (b[1] - a[1]) * f)


def S(pts):
    return [(x * K, y * K) for x, y in pts]


def wing(draw, tf, membrane=(74, 44, 102), dark=(56, 30, 80), bone=(150, 118, 172), hi=(182, 150, 204), ol=5.0):
    """tf: 把翼的局部坐标（右翼，肩在原点附近）变到画布坐标。"""
    Sh = (0, 0); El = (24, -46); W = (58, -72)           # 肩、肘、腕
    T = [(114, -50), (110, 26), (70, 80)]                 # 三根指骨的尖
    B = (6, 38)                                           # 翼膜在身侧的下端
    arm = q(Sh, (4, -34), El, 10) + q(El, (36, -68), W, 10)[1:]
    f1 = q(W, (90, -82), T[0]); f2 = q(W, (98, -30), T[1]); f3 = q(W, (78, 6), T[2])
    def scallop(a, b, hub, f):
        return q(a, toward(toward(a, b, .5), hub, f), b)
    sc1 = scallop(T[0], T[1], W, .58); sc2 = scallop(T[1], T[2], W, .55); sc3 = scallop(T[2], B, El, .60)
    inner = q(B, (-2, 20), Sh)
    outline = arm + f1[1:] + sc1[1:] + sc2[1:] + sc3[1:] + inner[1:]
    P = lambda pts: S([tf(p) for p in pts])
    draw.polygon(P(outline), fill=membrane)
    # 每片翼膜沿后缘一条深色带，做出膜的起伏
    for sc, hub in ((sc1, W), (sc2, W), (sc3, El)):
        band = [toward(p, hub, .30) for p in sc]
        draw.polygon(P(sc + list(reversed(band))), fill=dark)    # 后缘内侧一条细的亮边
    for sc in (sc1, sc2, sc3):
        draw.line(P([toward(p, W, .06) for p in sc]), fill=hi, width=int(1.6 * K))
    # 骨头：臂骨粗，指骨由粗到细
    draw.line(P(arm), fill=bone, width=int(9 * K), joint="curve")
    for f, w in ((f1, 5.5), (f2, 5), (f3, 4.5)):
        draw.line(P(f), fill=bone, width=int(w * K), joint="curve")
    draw.line(P([toward(p, (p[0], p[1] - 3), 1) for p in arm[2:-2]]), fill=hi, width=int(2 * K))
    # 腕爪和指尖的小钩
    claw = [(52, -70), (52, -88), (60, -104), (68, -86), (66, -70)]
    draw.polygon(P(claw), fill=hi)
    draw.line(P(claw + [W]), fill=(0, 0, 0), width=int(ol * .8 * K), joint="curve")
    # 外轮廓
    draw.line(P(outline + [outline[0]]), fill=(0, 0, 0), width=int(ol * K), joint="curve")
    for t in T:
        x, y = tf(t)
        r = ol * .5 * K
        draw.ellipse((x * K - r, y * K - r, x * K + r, y * K + r), fill=(0, 0, 0))


def render(view):
    im = Image.new("RGBA", (N, N), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    if view in ("south", "north"):
        wing(d, lambda p: (142 + p[0], 126 + p[1]))
        wing(d, lambda p: (114 - p[0], 126 + p[1]))
    else:  # east：人朝右，翅膀收在身后（画面左侧），横向压窄
        wing(d, lambda p: (118 - p[0] * .62, 124 + p[1] * 1.02))
    return im.resize((256, 256), Image.LANCZOS)


if __name__ == "__main__":
    out = sys.argv[1]
    os.makedirs(out, exist_ok=True)
    for v in ("south", "north", "east"):
        render(v).save(os.path.join(out, "SuccubusWings_%s.png" % v))
    if len(sys.argv) > 3:
        old, ref = sys.argv[2], sys.argv[3]
        sheet = Image.new("RGBA", (3 * 264 + 8, 2 * 264 + 8), (58, 62, 68, 255))
        for r, v in enumerate(("south", "east")):
            a = Image.open(os.path.join(old, "SuccubusWings_%s.png" % v)).convert("RGBA").resize((256, 256), Image.NEAREST)
            b = render(v)
            c = Image.open(os.path.join(ref, "AG_DemonWings_%s.png" % v)).convert("RGBA").resize((256, 256))
            for col, img in enumerate((a, b, c)):
                sheet.alpha_composite(img, (8 + col * 264, 8 + r * 264))
        sheet.save(os.path.join(out, "compare.png"))
