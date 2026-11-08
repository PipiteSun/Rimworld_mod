# Minimal RimWorld-style painter: flat fill + offset shadow/highlight + thick ink outline.
import math
import numpy as np
from PIL import Image, ImageDraw, ImageFilter

SS = 4
REF = None
LAST = None
INK = (36, 30, 27)


def hexc(c):
    if isinstance(c, str):
        c = c.lstrip('#')
        return tuple(int(c[i:i + 2], 16) for i in (0, 2, 4))
    return tuple(c)


def mul(c, f):
    return tuple(max(0, min(255, int(v * f))) for v in hexc(c))


def mix(a, b, t):
    a, b = hexc(a), hexc(b)
    return tuple(int(a[i] * (1 - t) + b[i] * t) for i in range(3))


def catmull(pts, closed=False, n=14):
    pts = [tuple(map(float, p)) for p in pts]
    if len(pts) < 3:
        return pts
    P = pts + pts[:3] if closed else [pts[0]] + pts + [pts[-1]]
    out = []
    rng = range(len(pts)) if closed else range(len(pts) - 1)
    for i in rng:
        p0, p1, p2, p3 = P[i], P[i + 1], P[i + 2], P[i + 3]
        for k in range(n):
            t = k / n
            t2, t3 = t * t, t * t * t
            x = 0.5 * ((2 * p1[0]) + (-p0[0] + p2[0]) * t + (2 * p0[0] - 5 * p1[0] + 4 * p2[0] - p3[0]) * t2 + (-p0[0] + 3 * p1[0] - 3 * p2[0] + p3[0]) * t3)
            y = 0.5 * ((2 * p1[1]) + (-p0[1] + p2[1]) * t + (2 * p0[1] - 5 * p1[1] + 4 * p2[1] - p3[1]) * t2 + (-p0[1] + 3 * p1[1] - 3 * p2[1] + p3[1]) * t3)
            out.append((x, y))
    if not closed:
        out.append(pts[-1])
    return out


def shift(m, dx, dy):
    o = np.zeros_like(m)
    H, W = m.shape
    xs0, xs1 = max(0, dx), min(W, W + dx)
    ys0, ys1 = max(0, dy), min(H, H + dy)
    o[ys0:ys1, xs0:xs1] = m[ys0 - dy:ys1 - dy, xs0 - dx:xs1 - dx]
    return o


class Canvas:
    def __init__(self, w, h, seed=3, light=(-1, -1)):
        self.w, self.h = w, h
        self.W, self.H = w * SS, h * SS
        self.rgb = np.zeros((self.H, self.W, 3), np.float32)
        self.a = np.zeros((self.H, self.W), bool)
        self.rng = np.random.default_rng(seed)
        self.light = light
        self.mirror = False
        self.xf = None
        self.ref = REF
        self.flesh_acc = np.zeros((self.H, self.W), bool)
        global LAST
        LAST = self

    def zoom(self, ax, ay, z, dx=0, dy=0):
        c = self

        class _Z:
            def __enter__(s):
                s.old = c.xf
                prev = c.xf or (lambda x, y: (x, y))
                c.xf = lambda x, y: prev(ax + dx + (x - ax) * z, ay + dy + (y - ay) * z)

            def __exit__(s, *a):
                c.xf = s.old
        return _Z()

    def P(self, pts):
        if self.xf:
            pts = [self.xf(x, y) for x, y in pts]
        if self.mirror:
            return [((self.w - x) * SS, y * SS) for x, y in pts]
        return [(x * SS, y * SS) for x, y in pts]

    def mask(self, pts):
        im = Image.new('L', (self.W, self.H), 0)
        ImageDraw.Draw(im).polygon(self.P(pts), fill=255)
        return np.asarray(im) > 127

    def ellipse_pts(self, cx, cy, rx, ry, n=48, rot=0):
        out = []
        for i in range(n):
            t = 2 * math.pi * i / n
            x, y = rx * math.cos(t), ry * math.sin(t)
            c, s = math.cos(rot), math.sin(rot)
            out.append((cx + x * c - y * s, cy + x * s + y * c))
        return out

    def fill(self, m, col, clip=None):
        if clip is not None:
            m = m & clip
        self.rgb[m] = hexc(col)
        self.a |= m

    def shade(self, m, col, sh=10, hi=4, shf=0.74, hif=1.13, clip=None, against=None):
        """Flat fill with lower-right shadow band and upper-left highlight band.
        `against`: mask the bands are measured against (default: the part itself)."""
        lx, ly = self.light
        U = m if against is None else (m | against)
        self.fill(m, col, clip)
        s = int(sh * SS)
        if s:
            band = m & ~shift(U, int(lx * s), int(ly * s))
            self.fill(band, mul(col, shf), clip)
        h = int(hi * SS)
        if h:
            band = m & ~shift(U, int(-lx * h), int(-ly * h)) & ~(m & ~shift(U, int(lx * s * 0.5), int(ly * s * 0.5)))
            self.fill(band, mul(col, hif), clip)

    def ao(self, m, strength=0.38, radius=7, off=(3, 4)):
        """Soft contact shadow cast by mask m onto what is already painted behind it."""
        im = Image.fromarray((shift(m, int(off[0] * SS), int(off[1] * SS)) * 255).astype(np.uint8))
        g = np.asarray(im.filter(ImageFilter.GaussianBlur(radius * SS))).astype(np.float32) / 255
        f = 1 - strength * np.clip(g * 1.6, 0, 1)
        t = self.a & ~m
        self.rgb[t] *= f[t][:, None]

    def stroke_fade(self, poly, w, joints, col=INK):
        """Closed outline whose width fades to nothing near joint points (x, y, r0, r1)."""
        def f(p):
            k = 1.0
            for j in joints:
                if j[0] == 'y':
                    k = min(k, max(0.0, min(1.0, (p[1] - j[1]) / (j[2] - j[1]))))
                    continue
                jx, jy, r0, r1 = j
                d = math.hypot(p[0] - jx, p[1] - jy)
                k = min(k, max(0.0, min(1.0, (d - r0) / (r1 - r0))))
            return k * k * (3 - 2 * k)
        im = Image.new('L', (self.W, self.H), 0)
        d = ImageDraw.Draw(im)
        P = self.P(poly)
        n = len(poly)
        for i in range(n):
            a, b = poly[i], poly[(i + 1) % n]
            k = (f(a) + f(b)) / 2
            ww = w * k * SS
            if ww < 0.6 * SS:
                continue
            d.line([P[i], P[(i + 1) % n]], fill=255, width=int(ww))
            r = ww / 2
            for x, y in (P[i], P[(i + 1) % n]):
                d.ellipse((x - r, y - r, x + r, y + r), fill=255)
        self.fill(np.asarray(im) > 127, col)

    def joint_weight(self, joints, k0=0.25, k1=1.35):
        """0 at a joint centre, rising smoothly to 1 away from it (canvas pixels)."""
        W = np.ones((self.H, self.W), np.float32)
        if not joints:
            return W
        ys, xs = np.mgrid[0:self.H, 0:self.W].astype(np.float32)
        for j in joints:
            if j[0] == 'y':
                (_, a), (_, b) = self.P([(0, j[1]), (0, j[2])])
                t = np.clip((ys - a) / (b - a), 0, 1)
                W = np.minimum(W, t * t * (3 - 2 * t))
                continue
            jx, jy, r0, r1 = j
            (cx, cy), (ex, ey) = self.P([(jx, jy), (jx + r0, jy)])
            r = math.hypot(ex - cx, ey - cy)
            d = np.hypot(xs - cx, ys - cy)
            t = np.clip((d - r * k0) / (r * (k1 - k0)), 0, 1)
            W = np.minimum(W, t * t * (3 - 2 * t))
        return W

    def blend_joints(self, m, old, olda, joints):
        """Fade a freshly painted part into what was under it around its joints."""
        if not joints:
            return
        W = self.joint_weight(joints)
        t = m & olda & (W < 1)
        w = W[t][:, None]
        self.rgb[t] = old[t] * (1 - w) + self.rgb[t] * w

    def flesh(self, pts, col, sh=10, hi=4, joints=(), w=2.6, smooth=True, n=10, ao=0.38, **kw):
        """Skin part that merges into neighbouring skin: bands vs. whole-body silhouette,
        soft contact shadow instead of a seam, outline fading out at the joints."""
        poly = catmull(pts, closed=True, n=n) if smooth else list(pts)
        m = self.mask(poly)
        self.flesh_acc |= m
        if ao:
            self.ao(m, ao)
        old, olda = self.rgb.copy(), self.a.copy()
        self.shade(m, col, sh, hi, against=self.ref, **kw)
        self.blend_joints(m, old, olda, joints)
        if w:
            if joints:
                self.stroke_fade(poly, w, joints)
            else:
                self.stroke(poly, w)
        return m, poly

    def stroke(self, pts, w=2.6, col=INK, closed=True, clip=None):
        im = Image.new('L', (self.W, self.H), 0)
        d = ImageDraw.Draw(im)
        P = self.P(pts)
        if closed:
            P = P + [P[0], P[1]]
        d.line(P, fill=255, width=max(1, int(w * SS)), joint='curve')
        r = w * SS / 2
        for x, y in (P if not closed else []):
            pass
        if not closed:
            for x, y in (P[0], P[-1]):
                d.ellipse((x - r, y - r, x + r, y + r), fill=255)
        m = np.asarray(im) > 127
        self.fill(m, col, clip)

    def part(self, pts, col, w=2.6, sh=10, hi=4, smooth=True, n=10, **kw):
        poly = catmull(pts, closed=True, n=n) if smooth else pts
        m = self.mask(poly)
        self.shade(m, col, sh, hi, **kw)
        if w:
            self.stroke(poly, w)
        return m, poly

    def blob(self, cx, cy, rx, ry, col, w=2.6, sh=8, hi=3, rot=0, **kw):
        pts = self.ellipse_pts(cx, cy, rx, ry, rot=rot)
        m = self.mask(pts)
        self.shade(m, col, sh, hi, **kw)
        if w:
            self.stroke(pts, w)
        return m

    def dot(self, x, y, r, col, w=1.4):
        pts = self.ellipse_pts(x, y, r, r, n=20)
        m = self.mask(pts)
        self.fill(m, col)
        if w:
            self.stroke(pts, w)

    def taper(self, center, w0, w1=0.0, n=12, ease=1.0):
        c = catmull(center, n=n)
        L = len(c)
        left, right = [], []
        for i, (x, y) in enumerate(c):
            j0, j1 = max(0, i - 1), min(L - 1, i + 1)
            dx, dy = c[j1][0] - c[j0][0], c[j1][1] - c[j0][1]
            d = math.hypot(dx, dy) or 1
            nx, ny = -dy / d, dx / d
            t = i / (L - 1)
            w = (w0 + (w1 - w0) * (t ** ease)) / 2
            left.append((x + nx * w, y + ny * w))
            right.append((x - nx * w, y - ny * w))
        return c, left, right

    def horn(self, center, w0, col='#8f5446', ring='#5f3730', tip='#cbb592', rings=5, w=2.4, sh=5, hi=2, w1=0.0, ease=1.1):
        c, L, R = self.taper(center, w0, w1, ease=ease)
        poly = L + R[::-1]
        m = self.mask(poly)
        self.shade(m, col, sh, hi)
        N = len(c)
        # pale tip
        k0 = int(N * 0.72)
        tp = L[k0:] + R[k0:][::-1]
        self.fill(self.mask(tp), tip, clip=m)
        # rings (bands perpendicular to horn)
        for r in range(rings):
            t0 = 0.08 + r * (0.62 / max(1, rings))
            a, b = int(N * t0), int(N * (t0 + 0.045))
            if b - a < 1:
                b = a + 1
            seg = L[a:b + 1] + R[a:b + 1][::-1]
            self.fill(self.mask(seg), ring, clip=m)
        if w:
            self.stroke(poly, w)
        return m

    def claw(self, center, w0, col='#d4c4a2', w=2.0):
        return self.horn(center, w0, col=col, ring=mul(col, 0.8), tip=mix(col, '#ffffff', 0.15), rings=0, w=w, sh=3, hi=1)

    def grain(self, amt=0.05):
        n = self.rng.normal(0, 1, (self.H // SS + 1, self.W // SS + 1)).astype(np.float32)
        n = np.kron(n, np.ones((SS, SS), np.float32))[:self.H, :self.W]
        self.rgb *= (1 + amt * n)[..., None]

    def finish(self, outline=5.0, size=None):
        A = Image.fromarray((self.a * 255).astype(np.uint8))
        d = A
        r = int(outline * SS)
        step = 9
        while r > 0:
            k = min(step, 2 * r + 1)
            if k % 2 == 0:
                k -= 1
            d = d.filter(ImageFilter.MaxFilter(k))
            r -= (k - 1) // 2
        D = np.asarray(d) > 127
        ring = D & ~self.a
        self.rgb[ring] = INK
        a = D.astype(np.float32)
        rgb = np.clip(self.rgb, 0, 255) * a[..., None]
        out_size = size or (self.w, self.h)
        pm = Image.fromarray(rgb.astype(np.uint8)).resize(out_size, Image.LANCZOS)
        al = Image.fromarray((a * 255).astype(np.uint8)).resize(out_size, Image.LANCZOS)
        P = np.asarray(pm).astype(np.float32)
        Aa = np.asarray(al).astype(np.float32)
        safe = np.maximum(Aa, 1)[..., None] / 255.0
        C = np.clip(P / safe, 0, 255)
        C[Aa < 2] = 0
        return Image.fromarray(np.dstack([C, Aa]).astype(np.uint8), 'RGBA')
