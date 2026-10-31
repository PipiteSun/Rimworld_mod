# 量旧武器贴图的主轴：返回 (尺寸, 起点, 终点)，坐标按 128 归一。终点取“偏右上”的一端。
import math, os
import numpy as np
from PIL import Image


def measure(path):
    im = Image.open(path).convert("RGBA")
    W = im.width
    a = np.asarray(im)[..., 3] > 40
    ys, xs = np.nonzero(a)
    k = 128.0 / W
    xs, ys = xs * k, ys * k
    cx, cy = xs.mean(), ys.mean()
    cov = np.cov(np.vstack([xs - cx, ys - cy]))
    w, v = np.linalg.eigh(cov)
    d = v[:, np.argmax(w)]
    if d[0] - d[1] < 0:          # 指向右上
        d = -d
    t = (xs - cx) * d[0] + (ys - cy) * d[1]
    t0, t1 = np.percentile(t, 0.5), np.percentile(t, 99.5)
    p0 = (cx + d[0] * t0, cy + d[1] * t0)
    p1 = (cx + d[0] * t1, cy + d[1] * t1)
    elong = math.sqrt(max(w) / max(1e-6, min(w)))
    return W, p0, p1, elong
