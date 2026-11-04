# 2026-11-02 DND 装备贴图“原版描边”整理。
# 量了原版风格贴图（VE 系列的原版画师作品）的外轮廓粗细，按 128 像素画布折算：
#   穿戴衣甲/帽子 ≈ 3.1 像素（256 图约 6 像素），物品图标 ≈ 4.1 像素，武器 ≈ 1.6–2.5 像素。
# DND 的穿戴图只有一半粗（256 图约 3 像素），饰品/图标 2–3 像素，在游戏里缩小后轮廓发虚、显得“不是原版”。
# 这里对每张图量出现有外描边粗细，只补差额：沿外轮廓向外加一圈原版深色描边（抗锯齿），
# 半透明的发光晕不加描边；对应的 _m 遮罩在新描边处清零，材料色/纹章色不会染到描边上。
# 输入一律读备份（ArtSource\backup-art-1102）或高清重画结果，重复运行结果相同。
import numpy as np
from PIL import Image
from scipy import ndimage as ndi

INK = np.array([30, 26, 24], np.float32)
TARGET = {"worn": 3.1, "icon": 4.0, "weapon": 2.6}     # 按 128 画布
MAXADD = {"worn": 3.4, "icon": 2.6, "weapon": 1.6}
SS = 4


def thickness(a):
    """现有外描边粗细（像素）：从外轮廓往里一圈圈数“深色”占多数的层数。"""
    al = a[..., 3] / 255
    solid = al >= 0.5
    if solid.sum() < 30:
        return None
    lum = a[..., :3] @ np.array([.299, .587, .114], np.float32)
    dark = solid & (lum < 75)
    d = ndi.distance_transform_edt(solid)
    t = 0.0
    for k in range(1, 14):
        ring = solid & (d > k - 1) & (d <= k)
        if not ring.any():
            break
        f = (dark & ring).sum() / ring.sum()
        t += f
        if f < 0.5:
            break
    return t


def plan(a, role):
    W = a.shape[1]
    e = thickness(a)
    if e is None:
        return 0.0, e
    s = W / 128
    add = min(TARGET[role] * s - e, MAXADD[role] * s)
    return (add if add >= 0.4 * s else 0.0), e


def outline(a, r):
    """a: HxWx4 uint8 → 加 r 像素外描边后的数组，以及新描边覆盖度（给遮罩用）。"""
    a = a.astype(np.float32)
    al = a[..., 3] / 255
    H, W = al.shape
    up = np.asarray(Image.fromarray((al * 255).astype(np.uint8)).resize((W * SS, H * SS), Image.BILINEAR)) / 255
    solid = up >= 0.5
    d = ndi.distance_transform_edt(~solid) / SS                # 到实体的距离（最终像素）
    cov = np.clip(r + 0.5 - d, 0, 1)
    cov = np.asarray(Image.fromarray((cov * 255).astype(np.uint8)).resize((W, H), Image.BOX)).astype(np.float32) / 255
    # 发光晕：离实体超过 1.5 像素的半透明像素，不压黑
    dfin = ndi.distance_transform_edt(~(al >= 0.5))
    glow = (al > 0.03) & (dfin > 1.5)
    cov[glow] = 0
    # src over ink
    out_a = al + cov * (1 - al)
    rgb = a[..., :3]
    out_rgb = (rgb * al[..., None] + INK * (cov * (1 - al))[..., None]) / np.maximum(out_a, 1e-4)[..., None]
    out = np.dstack([np.clip(out_rgb, 0, 255), out_a * 255])
    out[out_a < 0.004] = 0
    return out.astype(np.uint8), cov * (1 - al)


def process(src, dst, role, mask_src=None, mask_dst=None):
    im = Image.open(src).convert("RGBA")
    a = np.asarray(im)
    r, e = plan(a, role)
    if r <= 0:
        im.save(dst, optimize=True)
        if mask_src:
            Image.open(mask_src).save(mask_dst, optimize=True)
        return e, 0.0
    out, newcov = outline(a, r)
    Image.fromarray(out, "RGBA").save(dst, optimize=True)
    if mask_src:
        m = Image.open(mask_src)
        mode = m.mode
        mm = np.asarray(m.convert("RGBA")).copy()
        if mm.shape[:2] == newcov.shape:
            kill = newcov > 0.02
            mm[kill, :3] = 0
        out_m = Image.fromarray(mm, "RGBA")
        (out_m if mode == "RGBA" else out_m.convert(mode)).save(mask_dst, optimize=True)
    return e, r
