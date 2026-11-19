# 1.173.0 古巨兽后裔：异种型图标（巨兽脚印）和「无限生长」基因图标（一排越来越高的兽角）。
# 用法：python geneart12.py <模组根目录>   （在 ArtSource/geneicons 里运行）
import sys, os, math
from geneart import *

HIDE = (150, 132, 104)     # 兽皮土色
HORN = (222, 208, 176)     # 角质


def footprint():
    ic = Icon()
    pad = poly([(40, 96), (34, 78), (44, 62), (64, 56), (84, 62), (94, 78), (88, 96), (64, 104)], sm=True)
    toes = [ell(34, 44, 9, 13), ell(54, 30, 10, 15), ell(76, 30, 10, 15), ell(96, 44, 9, 13)]
    ic.add(U(pad, *toes), HIDE, shade=6)
    return ic


def horn(cx, base, w, h, lean):
    return poly([(cx - w / 2, base), (cx - w * 0.30 + lean * 0.35, base - h * 0.55), (cx + lean, base - h),
                 (cx + w * 0.42 + lean * 0.30, base - h * 0.50), (cx + w / 2, base)], sm=True)


def endless_growth():
    ic = Icon()
    horns = [horn(30, 102, 20, 34, 3), horn(56, 102, 24, 58, 5), horn(88, 102, 30, 86, 8)]
    ic.add(U(*horns, rect(18, 98, 106, 106, 3)), HORN, shade=6)
    return ic


ICONS12 = {
    "Textures/Behemoth/Xenotype": ("古巨兽后裔", footprint),
    "Textures/Behemoth/Genes/EndlessGrowth": ("无限生长", endless_growth),
}

if __name__ == "__main__":
    root = sys.argv[1]
    for rel, (_, fn) in ICONS12.items():
        out = os.path.join(root, rel + ".png")
        os.makedirs(os.path.dirname(out), exist_ok=True)
        fn().render().save(out)
        print("wrote", out)
