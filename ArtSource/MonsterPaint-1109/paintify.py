# 2026-11-09 怪物贴图「油画风」后处理：把平涂+粗黑描边的卡通贴图，
# 加工成饱和、有体积光影、高光和纹理的 BG/DND 插画感。纯后处理，不改形状。
# paintify(rgba PIL) -> rgba PIL（同尺寸；<=128px 的先放大到 2 倍）
import numpy as np
from PIL import Image, ImageFilter
from scipy import ndimage as nd

LIGHT = np.array([-0.55, -0.65, 0.52]); LIGHT /= np.linalg.norm(LIGHT)

def _noise(h, w, rng, octaves=(4, 8, 16, 32)):
    acc = np.zeros((h, w)); amp = 1.0; tot = 0
    for o in octaves:
        g = rng.random((max(2, h // o), max(2, w // o)))
        g = np.array(Image.fromarray((g * 255).astype(np.uint8)).resize((w, h), Image.BICUBIC)) / 255.0
        acc += g * amp; tot += amp; amp *= 0.55
    return acc / tot - 0.5

def paintify(img, sat=1.45, seed=7, strength=1.0, v=1, blend_joints=False):
    img = img.convert("RGBA")
    if max(img.size) <= 128:
        img = img.resize((img.width * 2, img.height * 2), Image.LANCZOS)
    a = np.asarray(img).astype(np.float32) / 255.0
    rgb, al = a[..., :3].copy(), a[..., 3]
    mask = al > 0.5
    if not mask.any():
        return img
    h, w = mask.shape
    rng = np.random.default_rng(seed)
    lum = rgb @ [0.299, 0.587, 0.114]
    # 1. 描边：黑线改成「周围颜色加深」的有色暗线，外轮廓保留一圈深色
    ink = mask & (lum < 0.22) & (rgb.max(-1) - rgb.min(-1) < 0.12)
    body = mask & ~ink
    idx = nd.distance_transform_edt(~body, return_distances=False, return_indices=True)
    near = rgb[idx[0], idx[1]]
    if blend_joints:
        # 关节/部件之间的内部黑线：整条去掉，用两侧颜色填平再局部模糊，让部件连成一体
        dout = nd.distance_transform_edt(mask)
        inner = ink & (dout > 3.0)
        inner = nd.binary_dilation(inner, iterations=1) & mask & ~(dout <= 2.0)
        fill = near.copy()
        for c in range(3):
            fill[..., c] = nd.gaussian_filter(fill[..., c], 2.0)
        soft = np.where(inner[..., None], fill, rgb)
        for c in range(3):  # 填补区再和周围揉一遍
            blur = nd.gaussian_filter(soft[..., c], 1.6)
            soft[..., c] = np.where(inner, blur, soft[..., c])
        rgb = soft
        ink = ink & ~inner
    rgb = np.where(ink[..., None], near * 0.42, rgb)
    # 2. 饱和度 + 对比
    g = rgb @ [0.299, 0.587, 0.114]
    rgb = g[..., None] + (rgb - g[..., None]) * sat
    rgb = np.clip(rgb, 0, 1)
    rgb = 0.5 + (rgb - 0.5) * 1.12
    rgb = np.clip(rgb, 0, 1)
    # 3. 体积感：按整只怪轮廓 + 内部线条求高度场 → 法线 → 漫反射/高光
    dist = nd.distance_transform_edt(mask)
    crease = nd.gaussian_filter(ink.astype(np.float32), 1.2)
    hgt = np.sqrt(np.clip(dist, 0, 40) / 40.0)
    hgt = nd.gaussian_filter(hgt, 2.5) - crease * 0.35
    gy, gx = np.gradient(hgt)
    n = np.stack([-gx * 18, -gy * 18, np.ones_like(hgt)], -1)
    n /= np.linalg.norm(n, axis=-1, keepdims=True)
    ndl = np.clip(n @ LIGHT, 0, 1)
    if v == 2:
        sat = sat * 1.12
        diff = 0.40 + 0.85 * ndl
        spec = np.clip(n @ LIGHT, 0, 1) ** 18 * 0.55
        # 背光侧的冷色轮廓光
        rimL = np.clip(n @ -LIGHT, 0, 1) ** 3 * np.exp(-dist / 4.0) * 0.35
    else:
        diff = 0.62 + 0.55 * ndl
        spec = np.clip(n @ LIGHT, 0, 1) ** 24 * 0.45
        rimL = 0
    # 4. 环境光遮蔽：靠近外轮廓和线条处变暗
    ao = 1 - 0.30 * np.exp(-dist / 5.0) - 0.35 * np.clip(crease * 2.2, 0, 1)
    # 5. 画布纹理
    tex = 1 + 0.10 * _noise(h, w, rng) * strength
    if v == 2:
        # 鳞片/毛发感：细密各向异性纹理 + 粗块笔触
        fine = _noise(h, w, rng, (2, 3, 5)); coarse = _noise(h, w, rng, (10, 20))
        tex = 1 + (0.16 * fine + 0.14 * coarse) * strength
        ao = 1 - 0.40 * np.exp(-dist / 6.0) - 0.45 * np.clip(crease * 2.2, 0, 1)
    shade = (diff * ao * tex)
    out = rgb * shade[..., None] * strength + rgb * (1 - strength)
    out = out + spec[..., None] * np.array([1.0, 0.97, 0.9]) * strength + np.asarray(rimL)[..., None] * np.array([0.6, 0.8, 1.0])
    # 外轮廓 1px 深色
    rim = mask & (dist <= 1.5)
    out = np.where(rim[..., None], out * 0.35, out)
    out = np.clip(out, 0, 1)
    res = np.dstack([out, al])
    return Image.fromarray((res * 255).astype(np.uint8), "RGBA")
