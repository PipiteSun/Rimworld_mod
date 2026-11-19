# 1.177.0 通用体型基因图标：同一个人形剪影，按档位越画越大（矮小 / 巨人 / 山岳 / 擎天），脚下一条地平线做参照。
# 用法：python geneart13.py <模组根目录>   （在 ArtSource/geneicons 里运行）
import sys, os
from geneart import *

SKIN = (198, 170, 140)
GROUND = (120, 124, 130)


def figure(h, cx=64, base=108):
    """高 h 像素的站立人形剪影（头 + 肩 + 躯干 + 两腿），脚踩在 base。"""
    hw = h * 0.20                      # 半肩宽
    head_r = h * 0.15
    top = base - h
    head = ell(cx, top + head_r, head_r)
    torso = poly([(cx - hw, top + head_r * 2.1), (cx + hw, top + head_r * 2.1), (cx + hw * 0.72, base - h * 0.42),
                  (cx - hw * 0.72, base - h * 0.42)], sm=False)
    arms = U(rect(cx - hw - h * 0.07, top + head_r * 2.1, cx - hw + 1, base - h * 0.40, 3),
             rect(cx + hw - 1, top + head_r * 2.1, cx + hw + h * 0.07, base - h * 0.40, 3))
    legs = U(rect(cx - hw * 0.72, base - h * 0.44, cx - 2, base, 2), rect(cx + 2, base - h * 0.44, cx + hw * 0.72, base, 2))
    return U(head, torso, arms, legs)


def tier(h):
    def make():
        ic = Icon()
        ic.add(rect(14, 106, 114, 113, 3), GROUND, shade=3)
        ic.add(figure(h), SKIN, shade=6)
        return ic
    return make


ICONS13 = {
    "Textures/BodySize/Small": ("矮小之躯", tier(40)),
    "Textures/BodySize/Large": ("巨人之躯", tier(66)),
    "Textures/BodySize/Huge": ("山岳之躯", tier(82)),
    "Textures/BodySize/Colossal": ("擎天之躯", tier(98)),
}

if __name__ == "__main__":
    root = sys.argv[1]
    for rel, (_, fn) in ICONS13.items():
        out = os.path.join(root, rel + ".png")
        os.makedirs(os.path.dirname(out), exist_ok=True)
        fn().render().save(out)
        print("wrote", out)
