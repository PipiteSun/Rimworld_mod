# 把 Peter 画的神像母版（1536×1536 品红底，单张正面）装进游戏。
# 用法：python -I install_statues.py <LichPhylactery 根目录> <母版目录> [只处理的名字...]
# 输出：透明底主图 + CutoutComplex 遮罩 _m（红=按材料染色的石头/描边/深色件，黑=不染色的土金和暗紫点缀）。
import sys, os
import numpy as np
from PIL import Image, ImageFilter
from scipy import ndimage

MOD, SRC = sys.argv[1], sys.argv[2]
ONLY = set(sys.argv[3:])
STATUE_DIR = "Bridges/Ideology/Textures/Things/Building/LP/Statues"
ITEMS = {n: (f"{STATUE_DIR}/{n}", 256) for n in
         ["Lathander", "Tyr", "Mystra", "Kelemvor", "Malar", "Talos", "Bane", "Shar", "Surtr", "Pantheon"]}
ITEMS["DeityStatue"] = ("Textures/LP/Workbenches/DeityStatue", 128)
# 画布里的目标框（最大宽、最大高、底边 y），按旧图的占位定的：2×2 旧图约 215×240，1×1 约 98×123。
BOX = {256: (236, 240, 252), 128: (116, 120, 125)}


def key(rgb):
    a = rgb.astype(int)
    r, g, b = a[..., 0], a[..., 1], a[..., 2]
    bg = (r - g > 110) & (b - g > 90)
    alpha = ~bg
    # 去掉孤立小块：品红底上的噪点，以及右下角可能残留的水印字
    lab, n = ndimage.label(alpha)
    sizes = ndimage.sum(alpha, lab, range(1, n + 1))
    h, w = alpha.shape
    keep = np.zeros(n + 1, bool)
    objs = ndimage.find_objects(lab)
    for i, (sl, s) in enumerate(zip(objs, sizes), 1):
        y0, x0 = sl[0].start, sl[1].start
        if s < 60:
            continue
        if s < 3000 and y0 > h * 0.85 and x0 > w * 0.7:  # 右下角水印区
            continue
        keep[i] = True
    alpha = keep[lab]
    # 补上统一体内的小洞，再吃掉 2px 品红描边
    alpha = ndimage.binary_erosion(alpha, iterations=2)
    return alpha


def fit(rgb, alpha, size):
    ys, xs = np.where(alpha)
    y0, y1, x0, x1 = ys.min(), ys.max() + 1, xs.min(), xs.max() + 1
    rgb, alpha = rgb[y0:y1, x0:x1], alpha[y0:y1, x0:x1]
    mw, mh, bottom = BOX[size]
    s = min(mw / rgb.shape[1], mh / rgb.shape[0])
    w, h = max(1, round(rgb.shape[1] * s)), max(1, round(rgb.shape[0] * s))
    a = alpha.astype(np.float32)
    pm = rgb.astype(np.float32) * a[..., None]  # 预乘 alpha，免得品红渗到边上
    pm_s = np.stack([np.asarray(Image.fromarray(pm[..., c]).resize((w, h), Image.LANCZOS)) for c in range(3)], -1)
    a_s = np.asarray(Image.fromarray(a).resize((w, h), Image.LANCZOS))
    a_s = np.clip(a_s, 0, 1)
    col = np.where(a_s[..., None] > 1e-3, pm_s / np.maximum(a_s[..., None], 1e-3), 0)
    out = np.zeros((size, size, 4), np.uint8)
    ox, oy = (size - w) // 2, bottom - h
    out[oy:oy + h, ox:ox + w, :3] = np.clip(col, 0, 255).astype(np.uint8)
    al = np.clip(a_s * 255, 0, 255).astype(np.uint8)
    al[al < 10] = 0
    out[oy:oy + h, ox:ox + w, 3] = al
    out[out[..., 3] == 0, :3] = 0
    return out


def mask(img):
    hsv = np.asarray(Image.fromarray(img[..., :3]).convert("HSV")).astype(int)
    hue, sat, val = hsv[..., 0] * 360 / 255, hsv[..., 1] / 255, hsv[..., 2] / 255
    gold = (hue >= 20) & (hue <= 60) & (sat > 0.33) & (val > 0.3)
    purple = (hue >= 250) & (hue <= 320) & (sat > 0.22) & (val > 0.15)
    keep = gold | purple
    keep = ndimage.binary_opening(keep, iterations=1) | (keep & (sat > 0.5))
    m = np.zeros(img.shape, np.uint8)
    m[..., 3] = 255
    m[..., 0] = np.where(keep, 0, 255)
    return m, keep


for name, (rel, size) in ITEMS.items():
    if ONLY and name not in ONLY:
        continue
    rgb = np.asarray(Image.open(os.path.join(SRC, name + ".png")).convert("RGB"))
    alpha = key(rgb)
    out = fit(rgb, alpha, size)
    m, keep = mask(out)
    dst = os.path.join(MOD, rel)
    Image.fromarray(out, "RGBA").save(dst + ".png")
    Image.fromarray(m, "RGBA").save(dst + "_m.png")
    opaque = out[..., 3] > 0
    print(f"{name}: {size}px bbox={Image.fromarray(out).getbbox()} gold%={100 * (keep & opaque).sum() / opaque.sum():.1f}")
