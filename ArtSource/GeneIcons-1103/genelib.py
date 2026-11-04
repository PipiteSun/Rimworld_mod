# Gene-icon painter in the Alpha Genes / Vanilla Races Expanded look:
# solid black outline (~3.4px at 128), flat muted fills, a hard lower-right
# shade band, white/grey silhouettes, no background disc.
# All coordinates are in a 128x128 logical grid; drawn at SSx and downsampled.
import math
import numpy as np
from PIL import Image, ImageDraw
from scipy import ndimage as ndi

S = 128
SS = 4
N = S * SS
OL = 3.4
INK = (18, 18, 18)

# palette (sampled by eye from AG / VRE icons)
W = (242, 242, 242)
LG = (200, 200, 200)
G = (150, 150, 150)
DG = (98, 98, 104)
RED = (176, 82, 80)
PINK = (196, 120, 118)
GREEN = (88, 140, 80)
LGREEN = (166, 184, 76)
OLIVE = (150, 144, 88)
BLUE = (104, 132, 186)
LBLUE = (168, 204, 232)
PURPLE = (134, 100, 166)
LPURPLE = (188, 160, 210)
ORANGE = (226, 138, 58)
YELLOW = (238, 206, 84)
GOLD = (214, 172, 72)
BROWN = (128, 92, 64)
TAN = (206, 176, 128)
DARK = (62, 56, 70)
BONE = (230, 222, 196)
TEAL = (82, 156, 150)
SKIN = (224, 186, 160)
CRIM = (150, 52, 60)


def bez(*p, n=28):
    """Quadratic or cubic bezier through control points -> list of points."""
    out = []
    for i in range(n + 1):
        t = i / n
        if len(p) == 3:
            a, b, c = p
            x = (1 - t) ** 2 * a[0] + 2 * (1 - t) * t * b[0] + t * t * c[0]
            y = (1 - t) ** 2 * a[1] + 2 * (1 - t) * t * b[1] + t * t * c[1]
        else:
            a, b, c, d = p
            x = (1 - t) ** 3 * a[0] + 3 * (1 - t) ** 2 * t * b[0] + 3 * (1 - t) * t * t * c[0] + t ** 3 * d[0]
            y = (1 - t) ** 3 * a[1] + 3 * (1 - t) ** 2 * t * b[1] + 3 * (1 - t) * t * t * c[1] + t ** 3 * d[1]
        out.append((x, y))
    return out


def mirror(pts, cx=64):
    return [(2 * cx - x, y) for x, y in pts]


def rot(pts, ang, cx=64, cy=64):
    a = math.radians(ang)
    ca, sa = math.cos(a), math.sin(a)
    return [(cx + (x - cx) * ca - (y - cy) * sa, cy + (x - cx) * sa + (y - cy) * ca) for x, y in pts]


def taper(curve, w0, w1, cap=True):
    """Polygon around a polyline whose width goes w0 -> w1."""
    L, R = [], []
    n = len(curve)
    for i, (x, y) in enumerate(curve):
        a = curve[max(0, i - 1)]
        b = curve[min(n - 1, i + 1)]
        dx, dy = b[0] - a[0], b[1] - a[1]
        d = math.hypot(dx, dy) or 1
        nx, ny = -dy / d, dx / d
        w = (w0 + (w1 - w0) * i / (n - 1)) / 2
        L.append((x + nx * w, y + ny * w))
        R.append((x - nx * w, y - ny * w))
    return L + R[::-1]


class Icon:
    def __init__(self):
        self.rgb = np.zeros((N, N, 3), np.float32)
        self.a = np.zeros((N, N), np.float32)
        self.tf = [(0.0, 0.0, 1.0)]

    # ---- transform: place a 128-grid motif at (ox,oy) scaled by k ----
    def at(self, ox, oy, k):
        ic = self

        class _T:
            def __enter__(s):
                px, py, pk = ic.tf[-1]
                ic.tf.append((px + ox * pk, py + oy * pk, pk * k))

            def __exit__(s, *a):
                ic.tf.pop()
        return _T()

    @property
    def k(self):
        return self.tf[-1][2]

    def P(self, x, y):
        ox, oy, k = self.tf[-1]
        return ((ox + x * k) * SS, (oy + y * k) * SS)

    # ---- masks ----
    def _blank(self):
        return Image.new('L', (N, N), 0)

    def _arr(self, im):
        return np.asarray(im) > 127

    def poly(self, pts):
        im = self._blank()
        ImageDraw.Draw(im).polygon([self.P(*p) for p in pts], fill=255)
        return self._arr(im)

    def ell(self, cx, cy, rx, ry=None, ang=0):
        ry = rx if ry is None else ry
        pts = [(cx + rx * math.cos(t * math.pi / 40), cy + ry * math.sin(t * math.pi / 40)) for t in range(80)]
        if ang:
            pts = rot(pts, ang, cx, cy)
        return self.poly(pts)

    def rect(self, x0, y0, x1, y1, r=0):
        (a, b), (c, d) = self.P(x0, y0), self.P(x1, y1)
        im = self._blank()
        ImageDraw.Draw(im).rounded_rectangle((min(a, c), min(b, d), max(a, c), max(b, d)),
                                             radius=r * SS * self.k, fill=255)
        return self._arr(im)

    def line(self, pts, w):
        im = self._blank()
        d = ImageDraw.Draw(im)
        P = [self.P(*p) for p in pts]
        ww = w * SS * self.k
        d.line(P, fill=255, width=max(1, int(ww)), joint='curve')
        for p in (P[0], P[-1]):
            d.ellipse((p[0] - ww / 2, p[1] - ww / 2, p[0] + ww / 2, p[1] + ww / 2), fill=255)
        return self._arr(im)

    def ring(self, cx, cy, r0, r1, ry0=None, ry1=None):
        return self.ell(cx, cy, r1, ry1) & ~self.ell(cx, cy, r0, ry0)

    def arc(self, cx, cy, r, a0, a1, w, ry=None):
        ry = r if ry is None else ry
        n = max(8, int(abs(a1 - a0) / 4))
        pts = [(cx + r * math.cos(math.radians(a0 + (a1 - a0) * i / n)),
                cy + ry * math.sin(math.radians(a0 + (a1 - a0) * i / n))) for i in range(n + 1)]
        return self.line(pts, w)

    # ---- painting ----
    def put(self, m, col, shade=0.16, ol=OL, olcol=INK):
        if not m.any():
            return m
        col = np.asarray(col, np.float32)
        if ol > 0:
            dil = ndi.distance_transform_edt(~m) <= ol * SS
            ring = dil & ~m
            self.rgb[ring] = olcol
            self.a[ring] = 1
        f = np.ones((N, N), np.float32)
        if shade > 0:
            ys, xs = np.nonzero(m)
            size = min(np.ptp(xs) + 1, np.ptp(ys) + 1)
            d = int(max(2 * SS, min(9 * SS, size * 0.12)))
            sh = np.zeros_like(m)
            sh[:-d, :-d] = m[d:, d:]
            band = m & ~sh
            f[band] = 1 - shade
        self.rgb[m] = col[None, :] * f[m][:, None]
        self.a[m] = 1
        return m

    def detail(self, m, col=INK, alpha=1.0):
        """Interior lines/marks: no outline, clipped to painted area."""
        m = m & (self.a > 0)
        col = np.asarray(col, np.float32)
        self.rgb[m] = self.rgb[m] * (1 - alpha) + col * alpha

    def flat(self, m, col):
        """Fill without outline or shade (e.g. highlights)."""
        self.rgb[m] = col
        self.a[m] = 1

    def save(self, path, size=S):
        a = np.clip(self.a, 0, 1)
        rgba = np.dstack([self.rgb * a[..., None], a * 255])
        im = Image.fromarray(np.clip(rgba, 0, 255).astype(np.uint8), 'RGBA')
        small = im.resize((size, size), Image.LANCZOS)
        arr = np.asarray(small).astype(np.float32)
        al = arr[..., 3:4] / 255.0
        rgb = np.where(al > 0.003, arr[..., :3] / np.maximum(al, 1e-3), 0)
        out = np.dstack([np.clip(rgb, 0, 255), arr[..., 3]]).astype(np.uint8)
        Image.fromarray(out, 'RGBA').save(path, optimize=True)
