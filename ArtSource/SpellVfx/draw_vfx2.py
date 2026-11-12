# 1.144.0 法术特效第二批贴图：4 环以上法术的专属特效与持续区域特效用。
# 白色 + alpha（游戏里按元素着色），和 draw_vfx.py 同一画法。用法：python draw_vfx2.py <Mod 根目录>
import math, os, random, sys
from PIL import Image, ImageDraw, ImageFilter

ROOT = sys.argv[1] if len(sys.argv) > 1 else os.path.join(os.path.dirname(__file__), '..', '..')
OUT = os.path.join(ROOT, 'Textures', 'LP', 'Vfx')
os.makedirs(OUT, exist_ok=True)
SS = 4


def canvas(w, h=None):
    h = h or w
    im = Image.new('L', (w * SS, h * SS), 0)
    return im, ImageDraw.Draw(im)


def finish(name, im, blur=1.2, glow=0.0):
    w, h = im.size[0] // SS, im.size[1] // SS
    out = im.resize((w, h), Image.LANCZOS)
    if glow > 0:
        g = out.filter(ImageFilter.GaussianBlur(glow))
        out = Image.eval(Image.blend(out, g, .5), lambda v: min(255, int(v * 1.6)))
    if blur > 0:
        out = out.filter(ImageFilter.GaussianBlur(blur * .4))
    white = Image.new('L', out.size, 255)
    Image.merge('RGBA', (white, white, white, out)).save(os.path.join(OUT, name + '.png'))


def S(v):
    return v * SS


def tentacle():
    im, d = canvas(128, 256)
    rnd = random.Random(3)
    pts = []
    for i in range(41):
        t = i / 40
        x = 64 + math.sin(t * 5.2 + .4) * 22 * (1 - t * .3) + math.sin(t * 13) * 3
        y = 250 - t * 236
        pts.append((x, y, 20 * (1 - t) ** .8 + 1.5))
    for x, y, r in pts:
        d.ellipse([S(x - r), S(y - r), S(x + r), S(y + r)], fill=235)
    for i in range(2, 34, 4):
        x, y, r = pts[i]
        for side in (-1, 1):
            cx = x + side * r * .55
            rr = max(1.5, r * .22)
            d.ellipse([S(cx - rr), S(y - rr), S(cx + rr), S(y + rr)], fill=110)
    finish('Tentacle', im, glow=2)


def skull():
    im, d = canvas(128)
    d.ellipse([S(24), S(14), S(104), S(92)], fill=230)
    d.rounded_rectangle([S(40), S(72), S(88), S(112)], radius=S(10), fill=230)
    for ex in (46, 82):
        d.ellipse([S(ex - 13), S(44), S(ex + 13), S(70)], fill=0)
    d.polygon([(S(64), S(72)), (S(57), S(86)), (S(71), S(86))], fill=0)
    for tx in range(46, 86, 8):
        d.rectangle([S(tx), S(98), S(tx + 3), S(112)], fill=0)
    finish('Skull', im, glow=2)


def eye():
    im, d = canvas(256, 128)
    d.ellipse([S(10), S(34), S(246), S(94)], fill=200)
    d.ellipse([S(96), S(32), S(160), S(96)], fill=255)
    d.ellipse([S(116), S(48), S(140), S(80)], fill=0)
    d.ellipse([S(122), S(52), S(130), S(60)], fill=255)
    finish('Eye', im, glow=3)


def pillar():
    im = Image.new('L', (64 * SS, 256 * SS), 0)
    px = im.load()
    W, H = im.size
    for y in range(H):
        fy = y / H
        a = (fy ** .6) * (1 - max(0, fy - .92) / .08 if fy > .92 else 1)
        for x in range(W):
            fx = abs(x / W - .5) * 2
            v = math.exp(-(fx / .35) ** 2) + .6 * math.exp(-(fx / .08) ** 2)
            px[x, y] = min(255, int(255 * a * v))
    finish('Pillar', im, blur=0)


def crack():
    im, d = canvas(256)
    rnd = random.Random(7)

    def branch(x, y, ang, length, width, depth):
        pts = [(x, y)]
        for i in range(8):
            ang += rnd.uniform(-.5, .5)
            x += math.cos(ang) * length / 8
            y += math.sin(ang) * length / 8
            pts.append((x, y))
        d.line([(S(a), S(b)) for a, b in pts], fill=240, width=max(1, int(S(width))))
        if depth > 0:
            for _ in range(2):
                i = rnd.randint(2, 7)
                branch(pts[i][0], pts[i][1], ang + rnd.uniform(-1.2, 1.2), length * .5, width * .55, depth - 1)
    for k in range(6):
        branch(128, 128, k * math.pi / 3 + rnd.uniform(-.3, .3), 110, 7, 2)
    finish('Crack', im, glow=2)


def swirl():
    im, d = canvas(256)
    for arm in range(3):
        pts = []
        for i in range(120):
            t = i / 120
            r = 8 + t * 116
            a = arm * 2 * math.pi / 3 + t * 4.2
            pts.append((128 + math.cos(a) * r, 128 + math.sin(a) * r))
        for i in range(len(pts) - 1):
            w = 14 * (1 - i / len(pts)) + 2
            d.line([(S(pts[i][0]), S(pts[i][1])), (S(pts[i + 1][0]), S(pts[i + 1][1]))], fill=int(255 * (1 - i / 160)), width=int(S(w)))
    finish('Swirl', im, glow=3)


def hand():
    im, d = canvas(256)
    d.rounded_rectangle([S(70), S(110), S(186), S(230)], radius=S(30), fill=230)
    fingers = [(78, 40, 26), (106, 22, 26), (134, 22, 26), (162, 40, 24)]
    for x, top, w in fingers:
        d.rounded_rectangle([S(x - 2), S(top), S(x + w - 2), S(140)], radius=S(12), fill=230)
    d.rounded_rectangle([S(176), S(120), S(232), S(150)], radius=S(14), fill=230)
    finish('Hand', im, glow=3)


def arrow():
    im, d = canvas(64, 256)
    d.rectangle([S(29), S(40), S(35), S(250)], fill=230)
    d.polygon([(S(32), S(2)), (S(16), S(48)), (S(48), S(48))], fill=255)
    for y in (206, 222, 238):
        d.polygon([(S(29), S(y)), (S(12), S(y + 18)), (S(29), S(y + 10))], fill=180)
        d.polygon([(S(35), S(y)), (S(52), S(y + 18)), (S(35), S(y + 10))], fill=180)
    finish('Arrow', im, glow=2)


def leaf():
    im, d = canvas(128)
    d.chord([S(18), S(30), S(150), S(160)], 180, 270, fill=230)
    d.chord([S(-22), S(-32), S(110), S(98)], 0, 90, fill=230)
    d.line([(S(24), S(104)), (S(104), S(24))], fill=90, width=S(3))
    finish('Leaf', im, glow=1)


def feather():
    im, d = canvas(128, 256)
    d.line([(S(64), S(250)), (S(64), S(10))], fill=255, width=S(4))
    for i in range(36):
        y = 20 + i * 6
        w = 40 * math.sin(math.pi * (i + 2) / 40)
        d.line([(S(64), S(y)), (S(64 - w), S(y + 18))], fill=200, width=S(4))
        d.line([(S(64), S(y)), (S(64 + w), S(y + 18))], fill=200, width=S(4))
    finish('Feather', im, glow=2)


def clock():
    im, d = canvas(256)
    d.ellipse([S(12), S(12), S(244), S(244)], outline=255, width=S(10))
    d.ellipse([S(40), S(40), S(216), S(216)], outline=150, width=S(3))
    for k in range(12):
        a = k * math.pi / 6
        r1, r2 = (88, 112) if k % 3 else (72, 112)
        d.line([(S(128 + math.cos(a) * r1), S(128 + math.sin(a) * r1)), (S(128 + math.cos(a) * r2), S(128 + math.sin(a) * r2))], fill=255, width=S(6 if k % 3 == 0 else 3))
    d.line([(S(128), S(128)), (S(128), S(52))], fill=255, width=S(7))
    d.line([(S(128), S(128)), (S(182), S(150))], fill=255, width=S(5))
    d.ellipse([S(118), S(118), S(138), S(138)], fill=255)
    finish('Clock', im, glow=3)


def chain():
    im, d = canvas(256, 64)
    for i in range(6):
        x = 6 + i * 42
        if i % 2 == 0:
            d.ellipse([S(x), S(14), S(x + 60), S(50)], outline=240, width=S(7))
        else:
            d.rounded_rectangle([S(x + 8), S(26), S(x + 52), S(38)], radius=S(6), fill=240)
    finish('Chain', im, glow=1)


def hexcage():
    im, d = canvas(256)
    for r, w, v in ((120, 9, 255), (96, 4, 160)):
        pts = [(128 + math.cos(k * math.pi / 3 + math.pi / 6) * r, 128 + math.sin(k * math.pi / 3 + math.pi / 6) * r) for k in range(6)]
        d.line([(S(a), S(b)) for a, b in pts + [pts[0]]], fill=v, width=S(w))
    for k in range(6):
        a = k * math.pi / 3 + math.pi / 6
        d.line([(S(128), S(128)), (S(128 + math.cos(a) * 118), S(128 + math.sin(a) * 118))], fill=110, width=S(3))
    finish('Hex', im, glow=3)


def portal():
    im = Image.new('L', (256 * SS, 256 * SS), 0)
    px = im.load()
    N = im.size[0]
    c = (N - 1) / 2
    for y in range(N):
        for x in range(N):
            dx, dy = (x - c) / c, (y - c) / c * 1.25
            r = math.hypot(dx, dy)
            if r > 1:
                continue
            a = math.atan2(dy, dx)
            ring = math.exp(-((r - .82) / .08) ** 2)
            spiral = .5 + .5 * math.sin(a * 3 + r * 14)
            inner = (1 - r) ** 1.5 * .55
            px[x, y] = min(255, int(255 * (ring + spiral * .35 * r + inner)))
    finish('Portal', im, blur=1)


def shard():
    im, d = canvas(96, 256)
    d.polygon([(S(48), S(4)), (S(80), S(90)), (S(62), S(252)), (S(34), S(252)), (S(16), S(90))], fill=210)
    d.polygon([(S(48), S(4)), (S(56), S(100)), (S(48), S(250))], fill=255)
    finish('Shard', im, glow=2)


def note():
    im, d = canvas(128)
    d.ellipse([S(14), S(78), S(54), S(110)], fill=240)
    d.ellipse([S(64), S(66), S(104), S(98)], fill=240)
    d.rectangle([S(46), S(22), S(53), S(96)], fill=240)
    d.rectangle([S(96), S(10), S(103), S(84)], fill=240)
    d.polygon([(S(46), S(22)), (S(103), S(10)), (S(103), S(26)), (S(46), S(38))], fill=240)
    finish('Note', im, glow=2)


def blade():
    im, d = canvas(256)
    for k in range(4):
        a = k * math.pi / 2
        tip = (128 + math.cos(a) * 124, 128 + math.sin(a) * 124)
        l = (128 + math.cos(a + .35) * 40, 128 + math.sin(a + .35) * 40)
        r = (128 + math.cos(a - .12) * 30, 128 + math.sin(a - .12) * 30)
        d.polygon([(S(128), S(128)), (S(l[0]), S(l[1])), (S(tip[0]), S(tip[1])), (S(r[0]), S(r[1]))], fill=235)
    d.ellipse([S(112), S(112), S(144), S(144)], fill=255)
    finish('Blade', im, glow=2)


def sun():
    im = Image.new('L', (256 * SS, 256 * SS), 0)
    px = im.load()
    N = im.size[0]
    c = (N - 1) / 2
    for y in range(N):
        for x in range(N):
            dx, dy = (x - c) / c, (y - c) / c
            r = math.hypot(dx, dy)
            a = math.atan2(dy, dx)
            rays = max(0, math.cos(a * 8)) ** 8 * max(0, 1 - r) ** .8 + max(0, math.cos(a * 8 + math.pi)) ** 16 * max(0, 1 - r * 1.3) * .6
            core = math.exp(-(r / .28) ** 2)
            px[x, y] = min(255, int(255 * (core + rays)))
    finish('Sun', im, blur=1)


def mask():
    im, d = canvas(128)
    d.ellipse([S(14), S(16), S(114), S(118)], fill=220)
    for ex in (44, 84):
        d.polygon([(S(ex - 16), S(52)), (S(ex + 16), S(48)), (S(ex + 10), S(64)), (S(ex - 12), S(66))], fill=0)
    d.arc([S(36), S(64), S(92), S(104)], 20, 160, fill=0, width=S(6))
    finish('Mask', im, glow=2)


def dome():
    im = Image.new('L', (256 * SS, 256 * SS), 0)
    px = im.load()
    N = im.size[0]
    c = (N - 1) / 2
    for y in range(N):
        for x in range(N):
            r = math.hypot(x - c, y - c) / c
            if r > 1:
                continue
            v = .18 + .82 * math.exp(-((r - .93) / .05) ** 2) + .25 * r ** 6
            px[x, y] = min(255, int(255 * v))
    finish('Dome', im, blur=1)


def insect():
    im, d = canvas(64)
    d.ellipse([S(24), S(18), S(40), S(48)], fill=240)
    d.ellipse([S(6), S(10), S(30), S(30)], fill=140)
    d.ellipse([S(34), S(10), S(58), S(30)], fill=140)
    finish('Insect', im, glow=1)


if __name__ == '__main__':
    for f in (tentacle, skull, eye, pillar, crack, swirl, hand, arrow, leaf, feather, clock, chain, hexcage, portal, shard, note, blade, sun, mask, dome, insect):
        f()
    print('ok', OUT)
