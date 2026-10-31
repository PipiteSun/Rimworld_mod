# 2026-10-30 魔法盔甲衣服重画：沿用 draw_apparel_145.py 的身体/头部几何和各件造型函数，
# 换成 RimWorld 原版画风的渲染器：粗深描边、上亮下暗的渐变、左上高光 / 右下阴影带、细颗粒，
# 再按材质（板甲 / 锁子 / 布 / 皮）叠一层纹理；_m 遮罩通道与旧版一致（红=材料色，绿=纹章色）。
# 用法：E:\Python\python.exe ArtSource\ArtRedraw-1030\apparel.py <模组根目录>
import importlib.util, math, os, sys
import numpy as np
from PIL import Image, ImageDraw
from scipy import ndimage as ndi

ROOT = sys.argv[1] if len(sys.argv) > 1 else os.getcwd()
HERE = os.path.dirname(os.path.abspath(__file__))
spec = importlib.util.spec_from_file_location("ap145", os.path.join(ROOT, "ArtSource", "RaceArt", "draw_apparel_145.py"))
ap = importlib.util.module_from_spec(spec)
sys.argv = [sys.argv[0], ROOT]
spec.loader.exec_module(ap)

S = ap.S
N = 128 * S
INK = np.array((38, 32, 30), np.float32)
STUFF, HERALD, FIXED = ap.STUFF, ap.HERALD, ap.FIXED
LT, MD, DK = ap.LT, ap.MD, ap.DK
TEX = os.path.join(ROOT, "Textures")
RNG = np.random.default_rng(1030)


def _noise(sigma, seed):
    r = np.random.default_rng(seed).standard_normal((N, N)).astype(np.float32)
    r = ndi.gaussian_filter(r, sigma)
    return r / (r.std() + 1e-6)


GRAIN = _noise(0.8, 1)
MOTTLE = _noise(6, 2)
_yy, _xx = np.mgrid[0:N, 0:N].astype(np.float32)


def _chain(p=6.0):
    row = np.floor(_yy / p)
    xo = _xx + (row % 2) * p / 2
    dx = (xo % p) - p / 2
    dy = (_yy % p) - p / 2
    r = np.hypot(dx, dy * 1.25)
    ring = np.exp(-((r - p * 0.34) / (p * 0.12)) ** 2)
    hole = (r < p * 0.18).astype(np.float32)
    return 1 + 0.16 * ring - 0.30 * hole - 0.08 * (dy > p * 0.2)


def _scales(p=9.0):
    row = np.floor(_yy / (p * 0.7))
    xo = _xx + (row % 2) * p / 2
    dx = (xo % p) - p / 2
    dy = (_yy % (p * 0.7))
    edge = np.exp(-((np.hypot(dx, dy - p * 0.05) - p * 0.55) / (p * 0.09)) ** 2)
    return 1 - 0.22 * edge + 0.05 * (dy / (p * 0.7))


def _cloth():
    # 竖向衣褶：低频的竖条阴影 + 极细的织纹
    folds = ndi.gaussian_filter(np.random.default_rng(3).standard_normal((1, N)).astype(np.float32), (0, 9))
    folds = folds / (folds.std() + 1e-6)
    folds = np.repeat(folds, N, 0)
    weave = 0.025 * np.sin(_xx * 1.6) * np.sin(_yy * 1.6)
    return 1 + 0.07 * folds + weave


def _leather():
    return 1 + 0.06 * MOTTLE + 0.03 * _noise(1.5, 4)


def _plate():
    streak = ndi.gaussian_filter(np.random.default_rng(5).standard_normal((N, 1)).astype(np.float32), (14, 0))
    streak = streak / (streak.std() + 1e-6)
    return 1 + 0.035 * np.repeat(streak, N, 1)


MATS = {"chain": _chain(7.0),"scale": _scales(), "cloth": _cloth(), "leather": _leather(), "plate": _plate()}

MATERIAL = {
    "PaladinPlate": "plate", "KnightMantle": "cloth", "ChainShirt": "chain", "DwarvenRuneplate": "plate", "RangerLeathers": "leather",
    "WoodwalkerCloak": "cloth", "ShadowweaveCloak": "cloth", "ArchmageRobe": "cloth", "RedWizardRobe": "cloth", "MorninglordVestments": "cloth",
    "BattleClericBreastplate": "plate", "DeathknightPlate": "plate", "LichShroud": "cloth", "DragonknightArmor": "scale", "EmberCollarPlate": "plate",
    "LoxodonWardenArmor": "plate", "OgreTrophyHide": "leather", "SkyguardHarness": "leather", "CelestialPanoply": "plate", "ElementalWeave": "cloth",
    "AdventurerClothes": "cloth", "BardFinery": "cloth", "CourtAttire": "cloth", "HarperCloak": "cloth", "ElvenChain": "chain",
    "GithAstralArmor": "plate", "GreatHelmOath": "plate", "BeardedHelm": "plate", "WizardHat": "cloth", "HelmRestlessDead": "plate",
    "CrownOfBone": "plate", "DragonHeadHelm": "scale", "GithCrestedHelm": "plate", "EmberRivetHelm": "plate", "RogueMask": "leather",
    "BardBeret": "cloth", "MinotaurHornHelm": "plate",
}


def _shift(m, dx, dy):
    dx, dy = int(round(dx)), int(round(dy))
    out = np.zeros_like(m)
    H, W = m.shape
    out[max(0, dy):H + min(0, dy), max(0, dx):W + min(0, dx)] = m[max(0, -dy):H + min(0, -dy), max(0, -dx):W + min(0, -dx)]
    return out


class Canvas:
    """与 draw_apparel_145.Canvas 同接口（add/line/save/icon），渲染换成带体积感的平涂。"""

    def __init__(self, view="south", material=None):
        self.rgb = np.zeros((N, N, 3), np.float32)
        self.a = np.zeros((N, N), np.float32)
        self.ch = np.zeros((N, N, 3), np.uint8)
        self.view = view
        self.kind = material
        self.mat = MATS.get(material)

    def _maskarr(self, parts):
        m = Image.new("L", (N, N), 0)
        d = ImageDraw.Draw(m)
        for part in parts:
            kind, a = part[0], part[1]
            neg = kind.startswith("-")
            kind = kind.lstrip("-")
            v = 0 if neg else 255
            if kind == "poly":
                d.polygon([(x * S, y * S) for x, y in a], fill=v)
            elif kind == "ell":
                d.ellipse([a[0] * S, a[1] * S, a[2] * S, a[3] * S], fill=v)
            elif kind == "rect":
                d.rectangle([a[0] * S, a[1] * S, a[2] * S, a[3] * S], fill=v)
            elif kind == "line":
                w = part[2] * S
                d.line([(x * S, y * S) for x, y in a], fill=v, width=max(1, int(w)), joint="curve")
                for x, y in (a[0], a[-1]):
                    d.ellipse([x * S - w / 2, y * S - w / 2, x * S + w / 2, y * S + w / 2], fill=v)
        return np.asarray(m) > 127

    def add(self, parts, color, chan=FIXED, ow=1.4, shade=None):
        if parts and isinstance(parts[0], str):
            parts = [parts]
        m = self._maskarr(parts)
        if not m.any():
            return
        col = np.asarray(color, np.float32)
        ys, xs = np.nonzero(m)
        size = min(np.ptp(xs) + 1, np.ptp(ys) + 1) / S
        big = ow and ow >= 0.9 and size > 3
        if ow:
            dil = ndi.distance_transform_edt(~m) <= ow * S * 0.95
            o = dil & ~m
            # 投影：描边外侧往右下压暗一点已画内容
            if big:
                sh = _shift(dil, 0.8 * S, 1.2 * S) & ~dil & (self.a > 0)
                sh = ndi.gaussian_filter(sh.astype(np.float32), S * 0.8)
                self.rgb *= (1 - 0.35 * sh)[..., None]
            self.rgb[o] = INK
            self.a[o] = 1
            self.ch[o] = FIXED
        f = np.ones((N, N), np.float32)
        y0, y1 = ys.min(), ys.max() + 1
        x0, x1 = xs.min(), xs.max() + 1
        f *= 1 + 0.10 * (0.5 - (_yy - y0) / max(1, y1 - y0))
        if big:
            sd = max(1.2, min(5.0, size * 0.16)) * S
            lx, ly = (-0.35, -1.0)
            band = m & ~_shift(m, lx * sd, ly * sd)
            band = ndi.gaussian_filter(band.astype(np.float32), S * 0.7)
            f *= 1 - (0.26 if shade is None else shade) * band
            hb = m & ~_shift(m, -lx * sd * 0.6, -ly * sd * 0.6)
            hb = ndi.gaussian_filter(hb.astype(np.float32), S * 0.6)
            f *= 1 + 0.12 * hb
            # 侧面体积：南/北向右侧暗，东向后背（左）暗
            xx = (_xx - x0) / max(1, x1 - x0)
            if self.view == "east":
                f *= 1 - 0.10 * np.clip(0.6 - xx, 0, 1)
            else:
                f *= 1 - 0.14 * np.clip(xx - 0.45, 0, 1) * 1.8
        f *= 1 + 0.03 * GRAIN
        if self.mat is not None and chan != FIXED:
            f *= self.mat
        elif chan == FIXED and big:
            f *= 1 + 0.025 * MOTTLE
        c = col[None, None, :] * f[..., None]
        self.rgb[m] = np.clip(c[m], 0, 255)
        self.a[m] = 1
        self.ch[m] = chan

    def line(self, pts, w, color=ap.INK, chan=FIXED):
        self.add(("line", pts, w), color, chan, 0)

    # ---- 输出 ----
    def _rgba(self):
        rgba = np.dstack([self.rgb, self.a * 255]).astype(np.uint8)
        return Image.fromarray(rgba, "RGBA")

    @property
    def col(self):
        return self._rgba()

    @property
    def msk(self):
        return Image.fromarray(self.ch, "RGB")

    def _down(self, im, size):
        arr = np.asarray(im).astype(np.float32)
        al = arr[..., 3:4] / 255.0
        pm = Image.fromarray(np.dstack([arr[..., :3] * al, arr[..., 3:]]).astype(np.uint8), "RGBA")
        small = np.asarray(pm.resize((size, size), Image.LANCZOS)).astype(np.float32)
        a = small[..., 3:4] / 255.0
        rgb = np.where(a > 0.004, small[..., :3] / np.maximum(a, 1e-3), 0)
        return Image.fromarray(np.dstack([np.clip(rgb, 0, 255), small[..., 3]]).astype(np.uint8), "RGBA")

    def save(self, rel, size=256, mask=True):
        path = os.path.join(TEX, rel.replace("/", os.sep))
        os.makedirs(os.path.dirname(path), exist_ok=True)
        self._down(self.col, size).save(path + ".png", optimize=True)
        if mask:
            self.msk.resize((size, size), Image.NEAREST).save(path + "_m.png", optimize=True)
        return path

    def icon(self, rel):
        col, msk = self.col, self.msk
        box = col.split()[3].getbbox()
        x0, y0, x1, y1 = box
        side = int(max(x1 - x0, y1 - y0) * 1.12)
        cx, cy = (x0 + x1) // 2, (y0 + y1) // 2
        crop = (cx - side // 2, cy - side // 2, cx - side // 2 + side, cy - side // 2 + side)
        path = os.path.join(TEX, rel.replace("/", os.sep))
        c = Image.new("RGBA", (side, side), (0, 0, 0, 0)); c.paste(col.crop(crop), (0, 0))
        m = Image.new("RGB", (side, side), (0, 0, 0)); m.paste(msk.crop(crop), (0, 0))
        self._down(c, 128).save(path + ".png", optimize=True)
        m.resize((128, 128), Image.NEAREST).save(path + "_m.png", optimize=True)
        return path


# ---- 覆盖旧版里画“硬切半边暗面”的函数：暗面改由渲染器的体积阴影完成 ----
def _row(f, t, e, flare, t0, t1, inset=1.2):
    pts = f.shape(t, t + .001, e + flare * max(0.0, (t - t0) / max(1e-6, t1 - t0)) ** 2, 0)
    return (pts[0][0] + inset, f.y(t)), (pts[-1][0] - inset, f.y(t))


def _sc(col, k):
    return tuple(max(0, min(255, int(v * k))) for v in col)


def torso(c, f, t0, t1, chan=STUFF, col=LT, e=.06, flare=0.0, shade=True, ow=1.4):
    c.add(ap.poly(f.shape(t0, t1, e, flare)), col, chan, ow)
    if not shade:
        return
    kind = getattr(c, "kind", None)
    span = t1 - t0
    if kind == "plate":
        # 腰下分段甲片：每片上缘一道暗线、下面一道亮线，两端铆钉
        for i in range(3):
            t = t0 + span * (.56 + i * .14)
            if t > t1 - .05:
                break
            a, b = _row(f, t, e, flare, t0, t1)
            c.add(("line", [a, (f.cx, f.y(t) + 1.0), b], .55), _sc(col, .66), chan, 0)
            a2, b2 = _row(f, t + .025, e, flare, t0, t1, 1.8)
            c.add(("line", [a2, (f.cx, f.y(t + .025) + 1.0), b2], .4), _sc(col, 1.08), chan, 0)
            for x, y in (a, b):
                xx = x + (1.4 if x < f.cx else -1.4)
                c.add(ap.ell((xx - .7, y + .6, xx + .7, y + 2.0)), (92, 88, 84), FIXED, 0)
        # 胸甲中脊（南/北）或侧面弧线（东）
        if f.view != "east":
            c.add(("line", [(f.cx, f.y(t0 + .04)), (f.cx, f.y(t0 + span * .5))], .5), _sc(col, 1.1), chan, 0)
            c.add(("line", [(f.cx + .8, f.y(t0 + .04)), (f.cx + .8, f.y(t0 + span * .5))], .45), _sc(col, .78), chan, 0)
        else:
            c.add(("line", [(f.cx + f.w * .32, f.y(t0 + .05)), (f.cx + f.w * .40, f.y(t0 + span * .3)), (f.cx + f.w * .32, f.y(t0 + span * .52))], .5), _sc(col, 1.1), chan, 0)
    elif kind == "cloth":
        # 衣褶：从腰部垂到下摆的几道弯线，下摆一道深色缝边
        n = 4 if f.w > 40 else 3
        for i in range(n):
            u = (i + .5) / n - .5
            xa = f.cx + u * f.w * .55
            xb = f.cx + u * f.w * (.75 + flare)
            ta = t0 + span * (.45 + .1 * (i % 2))
            c.add(("line", [(xa, f.y(ta)), (xa + (xb - xa) * .4 + 1, f.y(ta + (t1 - ta) * .5)), (xb, f.y(t1 - .03))], .5), _sc(col, .8), chan, 0)
        a, b = _row(f, t1 - .035, e, flare, t0, t1, .8)
        c.add(("line", [a, b], .55), _sc(col, .74), chan, 0)
        if f.view != "east":
            c.add(("line", [(f.cx, f.y(t0 + .04)), (f.cx, f.y(t0 + span * .4))], .45), _sc(col, .82), chan, 0)
    elif kind == "leather":
        # 缝线：两侧竖向虚线 + 下缘虚线
        for s in (-1, 1):
            if f.view == "east" and s < 0:
                continue
            x = f.cx + s * f.w * .3
            ys = np.linspace(f.y(t0 + .08), f.y(t1 - .08), 9)
            for y0_, y1_ in zip(ys[::2], ys[1::2]):
                c.add(("line", [(x, y0_), (x, y1_)], .35), _sc(col, .7), chan, 0)
        a, b = _row(f, t1 - .05, e, flare, t0, t1, 2.0)
        xs = np.linspace(a[0], b[0], 13)
        for x0_, x1_ in zip(xs[::2], xs[1::2]):
            c.add(("line", [(x0_, a[1]), (x1_, a[1])], .35), _sc(col, .7), chan, 0)
    elif kind in ("chain", "scale"):
        a, b = _row(f, t1 - .03, e, flare, t0, t1, .5)
        c.add(("line", [a, b], .8), _sc(col, .8), chan, 0)
        if f.view != "east":
            c.add(("line", [(f.cx - f.w * .18, f.y(t0 + .01)), (f.cx, f.y(t0 + .1)), (f.cx + f.w * .18, f.y(t0 + .01))], .7), _sc(col, .78), chan, 0)


_pauldrons = ap.pauldrons


def pauldrons(c, f, size, chan=STUFF, col=MD, spikes=False, curl=False):
    _pauldrons(c, f, size, chan, col, spikes, curl)
    # 肩甲叠片：两道弧线
    r = f.w * size
    ty = f.y(.06)
    centers = [f.cx + f.w * .02] if f.east else [f.cx + s * f.w * .42 for s in (-1, 1)]
    for cx in centers:
        for k in (.15, .45):
            c.add(("line", [(cx - r * .85, ty + r * k), (cx, ty + r * (k + .22)), (cx + r * .85, ty + r * k)], .45), _sc(col, .72), chan, 0)


def dome(c, h, v1, chan=STUFF, col=LT, e=.06):
    pts = []
    for i in range(25):
        a = math.radians(180 + i * 7.5)
        pts.append((h.cx + math.cos(a) * h.w * (.5 + e), h.Y(.5) + math.sin(a) * h.h * (.52 + e)))
    pts += [(h.X(1 + e), h.Y(v1)), (h.X(-e), h.Y(v1))]
    c.add(ap.poly(pts), col, chan, 1.4)
    # 盔顶脊线
    if not h.east:
        c.add(("line", [(h.cx, h.Y(-.02)), (h.cx, h.Y(min(v1, .5)))], .9), tuple(int(v * 1.04) if v < 245 else v for v in col), chan, 0)


ap.torso = torso
ap.dome = dome
ap.pauldrons = pauldrons

# 头盔里旧版用 DK 画的大块暗面也弱化成 MD
ap.DK = MD


def main():
    count = 0
    previews = []
    for name, fn in list(ap.BODY_ITEMS.items()) + list(ap.RESKIN_BODY.items()):
        mask = name not in ap.RESKIN_BODY
        row = []
        for body in ap.BODIES:
            for view in ap.VIEWS:
                c = Canvas(view, MATERIAL.get(name))
                fn(c, ap.Fit(body, view))
                c.save("LP/Apparel/%s/%s_%s_%s" % (name, name, body, view), 256, mask)
                count += 1
                if view == "south" or body == "Male":
                    row.append((body, view, c.col, c.msk))
                if body == "Male" and view == "south" and mask:
                    c.icon("LP/Apparel/%s/%s_Icon" % (name, name))
        previews.append((name, row))
    for name, fn in list(ap.HEAD_ITEMS.items()) + list(ap.RESKIN_HEAD.items()):
        mask = name not in ap.RESKIN_HEAD
        row = []
        for view in ap.VIEWS:
            c = Canvas(view, MATERIAL.get(name))
            fn(c, ap.Head(view))
            c.save("LP/Apparel/%s/%s_%s" % (name, name, view), 256, mask)
            count += 1
            row.append(("Male", view, c.col, c.msk))
            if view == "south" and mask:
                c.icon("LP/Apparel/%s/%s_Icon" % (name, name))
        previews.append((name, row))
    # 预览
    cell = 128
    cols = 7
    from PIL import ImageFont
    font = ImageFont.truetype("C:/Windows/Fonts/msyh.ttc", 18)
    sheet = Image.new("RGBA", (cols * cell + 220, len(previews) * cell), (120, 114, 104, 255))
    d = ImageDraw.Draw(sheet)
    for r, (name, row) in enumerate(previews):
        d.text((6, r * cell + 52), name, fill=(255, 255, 255), font=font)
        stuff = ap.STUFFLESS.get(name, ap.PREVIEW_COLORS["default"][0])
        herald = ap.PREVIEW_COLORS["default"][1]
        for i, (body, view, col, msk) in enumerate(row[:cols]):
            tile = Image.new("RGBA", (cell, cell), (0, 0, 0, 0))
            if name in ap.HEAD_ITEMS or name in ap.RESKIN_HEAD:
                hd = Image.new("RGBA", (N, N), (0, 0, 0, 0))
                hx0, hy0, hx1, hy1 = ap.HEAD[view]
                ImageDraw.Draw(hd).ellipse([hx0 * S, hy0 * S, hx1 * S, hy1 * S], fill=(222, 196, 170, 255))
                tile.alpha_composite(hd.resize((cell, cell), Image.LANCZOS))
            else:
                tile.alpha_composite(ap.body_blob(body, view, cell))
            tile.alpha_composite(ap.tint(col, msk, stuff, herald).resize((cell, cell), Image.LANCZOS))
            sheet.alpha_composite(tile, (220 + i * cell, r * cell))
    out = os.path.join(HERE, "preview_apparel.png")
    sheet.save(out)
    print(count, "worn textures; preview", out)


if __name__ == "__main__":
    main()
