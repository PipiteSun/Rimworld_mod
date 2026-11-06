# 1.121.0 法术特效贴图：白色 + 同等 alpha（MoteGlow 叠加、Mote 混合都能用），游戏里按元素着色。
# 用法：python draw_vfx.py <Mod 根目录>   → 写入 Textures/LP/Vfx/*.png
import math, os, random, sys
from PIL import Image, ImageDraw, ImageFilter, ImageChops

ROOT = sys.argv[1] if len(sys.argv) > 1 else os.path.join(os.path.dirname(__file__), '..', '..')
OUT = os.path.join(ROOT, 'Textures', 'LP', 'Vfx')
os.makedirs(OUT, exist_ok=True)


def save(name, lum):
    """lum: L 图（亮度 = alpha）。"""
    w = Image.new('L', lum.size, 255)
    img = Image.merge('RGBA', (w, w, w, lum))
    img.save(os.path.join(OUT, name + '.png'))


def radial(size, inner, outer, power=2.0, peak=255):
    im = Image.new('L', (size, size), 0)
    px = im.load()
    c = (size - 1) / 2
    for y in range(size):
        for x in range(size):
            d = math.hypot(x - c, y - c) / c
            if d <= inner:
                v = 1
            elif d >= outer:
                v = 0
            else:
                v = (1 - (d - inner) / (outer - inner)) ** power
            px[x, y] = int(peak * v)
    return im


def glow():
    save('Glow', radial(128, 0.0, 1.0, 2.2))


def ring():
    s = 256
    im = Image.new('L', (s, s), 0)
    px = im.load()
    c = (s - 1) / 2
    for y in range(s):
        for x in range(s):
            d = math.hypot(x - c, y - c) / c
            v = math.exp(-((d - 0.86) / 0.06) ** 2) + 0.35 * math.exp(-((d - 0.7) / 0.18) ** 2) * (d < 0.86)
            px[x, y] = min(255, int(255 * v))
    save('Ring', im)


def circle():
    """施法法阵：双环 + 刻文 + 六芒星，类似 VPE 施法圈但自绘。"""
    s, ss = 256, 4
    S = s * ss
    im = Image.new('L', (S, S), 0)
    d = ImageDraw.Draw(im)
    c = S / 2

    def circ(r, w):
        d.ellipse((c - r, c - r, c + r, c + r), outline=255, width=int(w))

    circ(S * 0.47, 5 * ss)
    circ(S * 0.40, 3 * ss)
    circ(S * 0.24, 3 * ss)
    # 刻文：两环之间的短划与小圆
    rng = random.Random(7)
    n = 36
    for i in range(n):
        a = 2 * math.pi * i / n
        r0, r1 = S * 0.415, S * 0.455
        if i % 3 == 0:
            x, y = c + math.cos(a) * (r0 + r1) / 2, c + math.sin(a) * (r0 + r1) / 2
            rr = 3.2 * ss
            d.ellipse((x - rr, y - rr, x + rr, y + rr), fill=255)
        else:
            k = rng.choice([0.3, 0.6, 1.0])
            a2 = a + 0.05 * k
            d.line((c + math.cos(a) * r0, c + math.sin(a) * r0, c + math.cos(a2) * r1, c + math.sin(a2) * r1), fill=255, width=2 * ss)
    # 六芒星
    for off in (0, math.pi / 3):
        pts = [(c + math.cos(off + 2 * math.pi * k / 3 - math.pi / 2) * S * 0.40,
                c + math.sin(off + 2 * math.pi * k / 3 - math.pi / 2) * S * 0.40) for k in range(3)]
        d.polygon(pts, outline=255)
        for k in range(3):
            d.line(pts[k] + pts[(k + 1) % 3], fill=255, width=3 * ss)
    im = im.resize((s, s), Image.LANCZOS)
    bloom = im.filter(ImageFilter.GaussianBlur(5))
    im = ImageChops.add(im, bloom.point(lambda v: int(v * 0.8)))
    save('Circle', im)


def spark():
    s = 64
    im = Image.new('L', (s, s), 0)
    px = im.load()
    c = (s - 1) / 2
    for y in range(s):
        for x in range(s):
            dx, dy = abs(x - c) / c, abs(y - c) / c
            core = max(0, 1 - math.hypot(dx, dy) / 0.35) ** 2
            star = max(0, 1 - dx / 1.0) ** 3 * max(0, 1 - dy / 0.08) + max(0, 1 - dy / 1.0) ** 3 * max(0, 1 - dx / 0.08)
            px[x, y] = min(255, int(255 * (core + star)))
    save('Spark', im)


def beam():
    w, h = 256, 32
    im = Image.new('L', (w, h), 0)
    px = im.load()
    for y in range(h):
        t = abs(y - (h - 1) / 2) / ((h - 1) / 2)
        v = math.exp(-(t / 0.22) ** 2) + 0.25 * math.exp(-(t / 0.6) ** 2)
        for x in range(w):
            e = min(x, w - 1 - x) / 24
            px[x, y] = min(255, int(255 * v * min(1, e)))
    save('Beam', im)


def bolt():
    """闪电：横向折线，用于雷电系连线。"""
    w, h, ss = 256, 64, 4
    im = Image.new('L', (w * ss, h * ss), 0)
    d = ImageDraw.Draw(im)
    rng = random.Random(11)
    pts = [(0, h * ss / 2)]
    x = 0
    while x < w * ss:
        x += rng.randint(14, 30) * ss
        pts.append((min(x, w * ss), h * ss / 2 + rng.uniform(-22, 22) * ss))
    pts[-1] = (w * ss, h * ss / 2)
    for i in range(len(pts) - 1):
        d.line(pts[i] + pts[i + 1], fill=255, width=4 * ss)
    im = im.resize((w, h), Image.LANCZOS)
    im = ImageChops.add(im, im.filter(ImageFilter.GaussianBlur(4)).point(lambda v: int(v * 1.2)))
    save('Bolt', im)


def slash():
    """近战弧光：月牙形。"""
    s, ss = 128, 4
    S = s * ss
    a = Image.new('L', (S, S), 0)
    ImageDraw.Draw(a).ellipse((S * 0.08, S * 0.08, S * 0.92, S * 0.92), fill=255)
    b = Image.new('L', (S, S), 0)
    ImageDraw.Draw(b).ellipse((S * 0.16, S * 0.0, S * 0.98, S * 0.80), fill=255)
    m = ImageChops.subtract(a, b)
    # 只留上半段弧
    mask = Image.new('L', (S, S), 0)
    ImageDraw.Draw(mask).rectangle((0, S * 0.45, S, S), fill=255)
    m = ImageChops.multiply(m, mask)
    m = m.resize((s, s), Image.LANCZOS).filter(ImageFilter.GaussianBlur(1))
    m = ImageChops.add(m, m.filter(ImageFilter.GaussianBlur(4)))
    save('Slash', m)


def rune():
    """上升的小符文（治疗/神圣/奥术的飘散粒子）。"""
    s, ss = 64, 4
    S = s * ss
    im = Image.new('L', (S, S), 0)
    d = ImageDraw.Draw(im)
    c = S / 2
    d.ellipse((c - S * 0.36, c - S * 0.36, c + S * 0.36, c + S * 0.36), outline=255, width=3 * ss)
    d.line((c, c - S * 0.26, c, c + S * 0.26), fill=255, width=4 * ss)
    d.line((c - S * 0.18, c - S * 0.05, c + S * 0.18, c - S * 0.05), fill=255, width=4 * ss)
    d.line((c - S * 0.12, c + S * 0.14, c + S * 0.12, c + S * 0.14), fill=255, width=3 * ss)
    im = im.resize((s, s), Image.LANCZOS)
    im = ImageChops.add(im, im.filter(ImageFilter.GaussianBlur(3)))
    save('Rune', im)


def flake():
    s, ss = 64, 4
    S = s * ss
    im = Image.new('L', (S, S), 0)
    d = ImageDraw.Draw(im)
    c = S / 2
    for k in range(6):
        a = math.pi * k / 3
        x, y = c + math.cos(a) * S * 0.44, c + math.sin(a) * S * 0.44
        d.line((c, c, x, y), fill=255, width=3 * ss)
        for t in (0.55, 0.75):
            bx, by = c + math.cos(a) * S * 0.44 * t, c + math.sin(a) * S * 0.44 * t
            for sgn in (-1, 1):
                a2 = a + sgn * 0.7
                d.line((bx, by, bx + math.cos(a2) * S * 0.12, by + math.sin(a2) * S * 0.12), fill=255, width=2 * ss)
    im = im.resize((s, s), Image.LANCZOS)
    im = ImageChops.add(im, im.filter(ImageFilter.GaussianBlur(2)))
    save('Flake', im)


for f in (glow, ring, circle, spark, beam, bolt, slash, rune, flake):
    f()
print('ok', OUT)
