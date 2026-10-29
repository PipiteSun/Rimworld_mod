# RimWorld-style building texture painter (hand-drawn look: thick dark outline,
# flat fill + soft lower shadow + top highlight, light grain, low saturation).
# Coordinates are in final pixels; everything is drawn at SSx and downsampled.
import math
import numpy as np
from PIL import Image, ImageDraw
from scipy import ndimage as ndi

SS = 4
INK = (34, 29, 26)


def hexc(c):
    if isinstance(c, str):
        c = c.lstrip('#')
        return tuple(int(c[i:i + 2], 16) for i in (0, 2, 4))
    return tuple(c)


def mul(c, f):
    c = hexc(c)
    return tuple(max(0, min(255, int(round(v * f)))) for v in c)


def mix(a, b, t):
    a, b = hexc(a), hexc(b)
    return tuple(int(round(a[i] * (1 - t) + b[i] * t)) for i in range(3))


class Canvas:
    def __init__(self, w, h, seed=7):
        self.w, self.h = w, h
        self.W, self.H = w * SS, h * SS
        self.rgb = np.zeros((self.H, self.W, 3), np.float32)
        self.a = np.zeros((self.H, self.W), np.float32)
        self.m = np.zeros((self.H, self.W), np.float32)
        self.rng = np.random.default_rng(seed)
        # rotation / plan transform (identity by default)
        self.tf = lambda x, y: (x, y)
        self.zs = 1.0

    def zoom(self, ax, ay, z):
        c = self

        class _Z:
            def __enter__(s):
                s.old = (c.tf, c.zs)
                prev = c.tf
                c.tf = lambda x, y: prev(ax + (x - ax) * z, ay + (y - ay) * z)
                c.zs = s.old[1] * z

            def __exit__(s, *a):
                c.tf, c.zs = s.old
        return _Z()

    # ---------- shape masks ----------
    def _img(self):
        return Image.new('L', (self.W, self.H), 0)

    def _done(self, im):
        return np.asarray(im) > 127

    def P(self, x, y):
        x, y = self.tf(x, y)
        return (x * SS, y * SS)

    def poly(self, pts):
        im = self._img()
        ImageDraw.Draw(im).polygon([self.P(*p) for p in pts], fill=255)
        return self._done(im)

    def rect(self, x0, y0, x1, y1, r=0):
        (ax, ay), (bx, by) = self.P(x0, y0), self.P(x1, y1)
        x0s, x1s = sorted((ax, bx))
        y0s, y1s = sorted((ay, by))
        im = self._img()
        d = ImageDraw.Draw(im)
        if r > 0:
            d.rounded_rectangle((x0s, y0s, x1s, y1s), radius=r * SS * self.zs, fill=255)
        else:
            d.rectangle((x0s, y0s, x1s, y1s), fill=255)
        return self._done(im)

    def ell(self, cx, cy, rx, ry=None):
        ry = rx if ry is None else ry
        cx, cy = self.P(cx, cy)
        rx, ry = rx * self.zs, ry * self.zs
        im = self._img()
        ImageDraw.Draw(im).ellipse((cx - rx * SS, cy - ry * SS, cx + rx * SS, cy + ry * SS), fill=255)
        return self._done(im)

    def ring(self, cx, cy, r0, r1, ry0=None, ry1=None):
        return self.ell(cx, cy, r1, ry1) & ~self.ell(cx, cy, r0, ry0)

    def line_mask(self, pts, w):
        im = self._img()
        d = ImageDraw.Draw(im)
        P = [self.P(*p) for p in pts]
        w = w * self.zs
        d.line(P, fill=255, width=max(1, int(w * SS)), joint='curve')
        for p in (P[0], P[-1]):
            d.ellipse((p[0] - w * SS / 2, p[1] - w * SS / 2, p[0] + w * SS / 2, p[1] + w * SS / 2), fill=255)
        return self._done(im)

    def arc_mask(self, cx, cy, r, a0, a1, w, ry=None):
        ry = r if ry is None else ry
        n = max(8, int(abs(a1 - a0) / 4))
        pts = [(cx + r * math.cos(math.radians(a0 + (a1 - a0) * i / n)),
                cy + ry * math.sin(math.radians(a0 + (a1 - a0) * i / n))) for i in range(n + 1)]
        return self.line_mask(pts, w)

    # ---------- helpers ----------
    def shift(self, m, dx, dy):
        dx, dy = int(round(dx * SS)), int(round(dy * SS))
        out = np.zeros_like(m)
        H, W = m.shape
        ys = slice(max(0, dy), H + min(0, dy))
        yd = slice(max(0, -dy), H + min(0, -dy))
        xs = slice(max(0, dx), W + min(0, dx))
        xd = slice(max(0, -dx), W + min(0, -dx))
        out[ys, xs] = m[yd, xd]
        return out

    def dilate(self, m, r):
        if r <= 0:
            return m
        return ndi.distance_transform_edt(~m) <= r * SS

    def noise(self, scale=1.0, octave=1):
        # grain at final-pixel resolution, upsampled
        h, w = int(self.h / scale) + 2, int(self.w / scale) + 2
        n = self.rng.standard_normal((h, w)).astype(np.float32)
        if octave > 1:
            n = ndi.gaussian_filter(n, octave * 0.6)
            n /= (n.std() + 1e-6)
        n = np.kron(n, np.ones((int(SS * scale), int(SS * scale)), np.float32))
        return n[:self.H, :self.W]

    def _put(self, m, col, alpha=1.0):
        col = np.asarray(hexc(col), np.float32) if not isinstance(col, np.ndarray) else col
        if col.ndim == 1:
            self.rgb[m] = self.rgb[m] * (1 - alpha) + col * alpha
        else:
            self.rgb[m] = self.rgb[m] * (1 - alpha) + col[m] * alpha
        self.a[m] = np.maximum(self.a[m], alpha)

    # ---------- painting ----------
    def paint(self, m, col, ol=3.0, shade=0.28, hl=0.12, sd=None, grain=0.035,
              stuff=False, drop=0.0, tex=None, olcol=INK, light=(-0.35, -1.0), grad=0.10):
        """Fill mask m with col: outline, lower-right shadow band, top-left highlight."""
        if not m.any():
            return m
        col = np.asarray(hexc(col), np.float32)
        ys, xs = np.nonzero(m)
        size = min(np.ptp(xs) + 1, np.ptp(ys) + 1) / SS
        if sd is None:
            sd = max(1.5, min(9.0, size * 0.16))
        ol = ol * (self.zs ** 0.5)
        dil = self.dilate(m, ol) if ol > 0 else m
        if drop > 0:
            sh = self.shift(dil, 2.5 * self.zs, 3.5 * self.zs) & ~dil & (self.a > 0)
            sh = ndi.gaussian_filter(sh.astype(np.float32), SS * 1.2)
            self.rgb *= (1 - sh * drop)[..., None]
        if ol > 0:
            self._put(dil & ~m, olcol)
        # base with vertical gradient
        f = np.ones((self.H, self.W), np.float32)
        y0, y1 = ys.min(), ys.max() + 1
        yy = (np.arange(self.H, dtype=np.float32)[:, None] - y0) / max(1, y1 - y0)
        f = f * (1 + grad * (0.5 - yy))
        lx, ly = light
        if shade > 0:
            band = m & ~self.shift(m, lx * sd, ly * sd)  # far from light (lower-right)
            band = ndi.gaussian_filter(band.astype(np.float32), SS * 0.6)
            f = f * (1 - shade * band)
        if hl > 0:
            hb = m & ~self.shift(m, -lx * sd * 0.6, -ly * sd * 0.6)
            hb = ndi.gaussian_filter(hb.astype(np.float32), SS * 0.6)
            f = f * (1 + hl * hb)
        if grain > 0:
            f = f * (1 + grain * self.noise(1.0))
        if tex is not None:
            f = f * tex
        c = col[None, None, :] * f[..., None]
        np.clip(c, 0, 255, out=c)
        self.rgb[m] = c[m]
        self.a[m] = 1
        if stuff is not None:
            self.m[dil] = 1.0 if stuff else 0.0
        return m

    def stroke(self, pts, col, w=2.0, alpha=1.0):
        mm = self.line_mask(pts, w)
        self._put(mm & (self.a > 0) if alpha < 1 else mm, col, alpha)
        return mm

    def fill(self, m, col, alpha=1.0, only_on=True):
        if only_on:
            m = m & (self.a > 0)
        self._put(m, col, alpha)

    def glow(self, m, col, r=6, strength=0.8, only_on=False):
        g = ndi.gaussian_filter(m.astype(np.float32), r * SS / 2.0) * strength
        g = np.clip(g * 2.2, 0, 1)
        if only_on:
            g = g * (self.a > 0)
        col = np.asarray(hexc(col), np.float32)
        self.rgb = self.rgb * (1 - g[..., None]) + col * g[..., None]
        self.a = np.maximum(self.a, g)

    def darken(self, m, f=0.7, blur=0.0):
        mm = m.astype(np.float32)
        if blur:
            mm = ndi.gaussian_filter(mm, blur * SS)
        mm = mm * (self.a > 0)
        self.rgb *= (1 - mm * (1 - f))[..., None]

    # ---------- texture helpers (return multiplier arrays) ----------
    def planks(self, horizontal=True, spacing=16, jitter=0.05, line=0.55, grainamt=0.06):
        H, W = self.H, self.W
        f = np.ones((H, W), np.float32)
        sp = spacing * SS
        coord = np.arange(H if horizontal else W)
        n = len(coord) // sp + 2
        tone = 1 + self.rng.uniform(-jitter, jitter, n)
        idx = (coord // sp).astype(int)
        t = tone[idx]
        lines = (coord % sp) < SS * 1.0
        prof = np.where(lines, line, 1.0) * t
        if horizontal:
            f *= prof[:, None]
            g = ndi.gaussian_filter(self.rng.standard_normal((H // SS + 1, 4)).astype(np.float32), (0.3, 1))
            stretch = np.kron(self.rng.standard_normal((H // SS + 1, W // (SS * 10) + 2)).astype(np.float32),
                              np.ones((SS, SS * 10)))[:H, :W]
            f *= 1 + grainamt * ndi.gaussian_filter(stretch, (SS * 0.4, SS * 4))
        else:
            f *= prof[None, :]
            stretch = np.kron(self.rng.standard_normal((H // (SS * 10) + 2, W // SS + 1)).astype(np.float32),
                              np.ones((SS * 10, SS)))[:H, :W]
            f *= 1 + grainamt * ndi.gaussian_filter(stretch, (SS * 4, SS * 0.4))
        return f

    def blocks(self, bw=24, bh=14, line=0.62, jitter=0.07, offset=True):
        H, W = self.H, self.W
        f = np.ones((H, W), np.float32)
        y = np.arange(H)[:, None]
        x = np.arange(W)[None, :]
        row = y // (bh * SS)
        xo = x + (row % 2) * (bw * SS // 2) * (1 if offset else 0)
        col = xo // (bw * SS)
        tone = 1 + self.rng.uniform(-jitter, jitter, (H // (bh * SS) + 2, W // (bw * SS) + 3))
        f *= tone[row, col]
        ln = ((y % (bh * SS)) < SS) | ((xo % (bw * SS)) < SS)
        f = np.where(ln, f * line, f)
        return f

    # ---------- output ----------
    def save(self, path, mask_path=None, mask_alpha=False):
        a = np.clip(self.a, 0, 1)
        rgba = np.dstack([self.rgb * a[..., None], a * 255]).astype(np.float32)
        im = Image.fromarray(np.clip(rgba, 0, 255).astype(np.uint8), 'RGBA')
        # premultiplied downsample
        small = im.resize((self.w, self.h), Image.LANCZOS)
        arr = np.asarray(small).astype(np.float32)
        al = arr[..., 3:4] / 255.0
        rgb = np.where(al > 0.003, arr[..., :3] / np.maximum(al, 1e-3), 0)
        out = np.dstack([np.clip(rgb, 0, 255), arr[..., 3]]).astype(np.uint8)
        Image.fromarray(out, 'RGBA').save(path, optimize=True)
        if mask_path:
            mm = Image.fromarray((np.clip(self.m, 0, 1) * 255).astype(np.uint8), 'L').resize((self.w, self.h), Image.BOX)
            r = np.asarray(mm)
            r = np.where(r > 110, 255, 0).astype(np.uint8)
            z = np.zeros_like(r)
            if mask_alpha:
                Image.fromarray(np.dstack([r, z, z, np.full_like(r, 255)]), 'RGBA').save(mask_path, optimize=True)
            else:
                Image.fromarray(np.dstack([r, z, z]), 'RGB').save(mask_path, optimize=True)
        return Image.open(path)


def rotated(c, rot, pw, ph):
    """Set c.tf so drawing in south-plan coords (pw x ph) lands correctly for rot."""
    if rot == 'south':
        c.tf = lambda x, y: (x, y)
    elif rot == 'north':
        c.tf = lambda x, y: (pw - x, ph - y)
    elif rot == 'east':
        # plan rotated 90deg: plan x -> screen y (top..bottom), plan y -> screen x (right->left)
        c.tf = lambda x, y: (ph - y, x)
    elif rot == 'west':
        c.tf = lambda x, y: (y, pw - x)
