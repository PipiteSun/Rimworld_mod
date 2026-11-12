# 1.142.0 银行贴图（热修：Peter 要像玻璃储物柜那样，柜里显示原版银 / 金物品图）。
# 一座带宽玻璃窗的保险金柜（Peter 2026-11-10 给的参考图改成 RimWorld 画风），2×2 = 256×256，不可旋转（Graphic_Multi，三个朝向同一张）：
#   BankVault_south/east/north.png  柜体 + 空柜窗（深色后壁）
#   BankVault_Front.png  同样的柜体，柜窗挖空只留反光，加立柱：游戏里印在物品图前面（Cutout）
#   BankVault_Icon.png   建造图标（柜里画简化的银币 / 金锭，只是图标用）
# 游戏里物品图由 Source/BankVault.cs 印在两层之间：柜窗中心 = 建筑中心往上 0.45 格，左半 / 右半各管一列。
# 用法：python -I ArtSource/RaceArt/draw_bank_1142.py <模组根目录>
import math, os, sys
from PIL import Image, ImageDraw, ImageFilter

ROOT = sys.argv[1] if len(sys.argv) > 1 else os.getcwd()
OUT = os.path.join(ROOT, "Textures", "LP", "Bank")
PREVIEW = os.path.join(ROOT, "ArtSource", "Preview", "Bank-1.142.0.png")
SS = 4
W = H = 256
INK = (34, 32, 36)
FRAME, FRAME_D, FRAME_L = (206, 211, 216), (160, 166, 174), (232, 236, 240)
BACK, BACK_D = (52, 60, 70), (40, 46, 54)
PANEL, PANEL_D = (78, 82, 90), (60, 64, 70)
BAR, BAR_L = (92, 96, 106), (124, 128, 138)
BRASS, BRASS_D = (200, 160, 86), (150, 114, 58)
GLASS = (176, 210, 226)
# 每座柜子（左 x0=0，右 x0=128）的部件，单位：像素
WIN = (26, 22, 230, 118)       # 柜窗（整座一扇宽窗：左半显示左边两格，右半显示右边两格）
OUTER = (8, 8, 248, 238)       # 柜体
VAULT = (18, 128, 238, 230)    # 下半截保险柜门
TEAL = (88, 168, 176)


def canvas():
    return Image.new("RGBA", (W * SS, H * SS), (0, 0, 0, 0))


def mask(img, kind, box, r=0, width=0):
    m = Image.new("L", img.size, 0)
    d = ImageDraw.Draw(m)
    b = [v * SS for v in box]
    if kind == "rect":
        d.rounded_rectangle(b, radius=r * SS, fill=255)
    elif kind == "ell":
        d.ellipse(b, fill=255)
    elif kind == "poly":
        d.polygon([(box[i] * SS, box[i + 1] * SS) for i in range(0, len(box), 2)], fill=255)
    elif kind == "line":
        d.line(b, fill=255, width=int(width * SS))
    return m


def put(img, kind, box, color, ow=0.0, r=0, width=0, alpha=255):
    m = mask(img, kind, box, r, width)
    if ow:
        k = max(3, int(ow * SS) * 2 + 1)
        img.paste(Image.new("RGBA", img.size, INK + (255,)), (0, 0), m.filter(ImageFilter.MaxFilter(k)))
    layer = Image.new("RGBA", img.size, tuple(color) + (alpha,))
    if alpha == 255:
        img.paste(layer, (0, 0), m)
    else:
        tmp = Image.new("RGBA", img.size, (0, 0, 0, 0))
        tmp.paste(layer, (0, 0), m)
        img.alpha_composite(tmp)


def shift(box, x0):
    return tuple(v + (x0 if i % 2 == 0 else 0) for i, v in enumerate(box))


def cabinet(img, front):
    o = OUTER
    # 底座（深色踢脚）
    put(img, "rect", (o[0] + 2, o[3] - 8, o[2] - 2, o[3] + 12), PANEL_D, 4, 3)
    put(img, "line", (128, o[3] - 6, 128, o[3] + 10), INK, 0, width=2.5)
    # 柜体：上面两角切角
    c = 12
    put(img, "poly", (o[0], o[1] + c, o[0] + c, o[1], o[2] - c, o[1], o[2], o[1] + c, o[2], o[3], o[0], o[3]), FRAME, 5)
    put(img, "poly", (o[0] + 4, o[1] + c + 2, o[0] + c + 2, o[1] + 4, o[2] - c - 2, o[1] + 4, o[2] - 10, o[1] + 10, o[0] + 10, o[1] + 10), FRAME_L, 0)   # 顶沿亮面
    put(img, "rect", (o[2] - 10, o[1] + 14, o[2] - 4, o[3] - 4), FRAME_D, 0, 2)                                                                          # 右侧暗边
    w = WIN
    if front:
        # 前层：柜窗挖空（物品图从这里透出来），只留左上角两道斜反光（Cutout，不能半透明）
        img.paste(Image.new("RGBA", img.size, (0, 0, 0, 0)), (0, 0), mask(img, "rect", w, 6))
        GL = (214, 232, 242)
        for a, wd, ln in ((14, 8, 46), (30, 4, 36)):
            put(img, "poly", (w[0] + a, w[1] + 4, w[0] + a + wd, w[1] + 4, w[0] + a + wd - ln * .6, w[1] + 4 + ln, w[0] + a - ln * .6, w[1] + 4 + ln), GL, 0)
        edge = Image.new("L", img.size, 0)
        ImageDraw.Draw(edge).rounded_rectangle([v * SS for v in w], radius=6 * SS, outline=255, width=int(3.5 * SS))
        img.paste(Image.new("RGBA", img.size, INK + (255,)), (0, 0), edge)
    else:
        # 柜窗里面：后壁、两侧壁、浅色底板，看得出有纵深
        put(img, "rect", w, BACK, 3.5, 6)
        put(img, "poly", (w[0] + 3, w[1] + 3, w[0] + 14, w[1] + 12, w[0] + 14, w[3] - 14, w[0] + 3, w[3] - 3), BACK_D, 0)
        put(img, "poly", (w[2] - 3, w[1] + 3, w[2] - 14, w[1] + 12, w[2] - 14, w[3] - 14, w[2] - 3, w[3] - 3), BACK_D, 0)
        put(img, "poly", (w[0] + 3, w[3] - 3, w[0] + 14, w[3] - 14, w[2] - 14, w[3] - 14, w[2] - 3, w[3] - 3), (76, 86, 98), 0)
        put(img, "line", (w[0] + 14, w[1] + 12, w[2] - 14, w[1] + 12), (62, 70, 80), 0, width=1.5)
    # 窗下横梁
    put(img, "rect", (o[0] + 4, w[3] + 4, o[2] - 4, w[3] + 10), FRAME_D, 0, 1)
    v = VAULT
    # 两扇保险柜门
    for x0, x1 in ((v[0], 128), (128, v[2])):
        put(img, "rect", (x0, v[1], x1, v[3]), FRAME, 3.5, 4)
        put(img, "rect", (x0 + 5, v[1] + 5, x1 - 5, v[1] + 9), FRAME_L, 0, 2)
        put(img, "rect", (x0 + 5, v[3] - 9, x1 - 5, v[3] - 5), FRAME_D, 0, 2)
        for rx, ry in ((x0 + 8, v[1] + 8), (x1 - 8, v[1] + 8), (x0 + 8, v[3] - 8), (x1 - 8, v[3] - 8)):
            put(img, "ell", (rx - 2.5, ry - 2.5, rx + 2.5, ry + 2.5), FRAME_D, 1.2)
    # 铰链：左右各两个
    for hx in (v[0] - 4, v[2] - 14):
        for hy in (v[1] + 16, v[3] - 34):
            put(img, "rect", (hx, hy, hx + 18, hy + 16), BAR, 3, 3)
            put(img, "rect", (hx + (12 if hx < 128 else 2), hy - 4, hx + (16 if hx < 128 else 6), hy + 20), PANEL_D, 2.5, 2)
    # 中间的密码锁：深色方板 + 转盘 + 青色锁舌
    put(img, "rect", (98, 146, 158, 206), PANEL, 4, 6)
    put(img, "rect", (101, 149, 155, 153), BAR_L, 0, 2)
    put(img, "ell", (110, 158, 146, 194), BAR, 3.5)
    put(img, "ell", (116, 164, 140, 188), PANEL_D, 2.5)
    put(img, "ell", (122, 170, 134, 182), BAR_L, 2)
    for k in range(8):
        a = math.radians(k * 45)
        put(img, "ell", (128 + 15 * math.cos(a) - 1.4, 176 + 15 * math.sin(a) - 1.4, 128 + 15 * math.cos(a) + 1.4, 176 + 15 * math.sin(a) + 1.4), FRAME_L, 0)
    put(img, "rect", (86, 168, 100, 182), TEAL, 2.5, 2)
    # 右门通风格栅
    put(img, "rect", (184, 160, 206, 192), PANEL_D, 2.5, 3)
    for k in range(5):
        put(img, "rect", (187, 164 + k * 5.6, 203, 166.5 + k * 5.6), BAR_L, 0)


def body(front):
    img = canvas()
    cabinet(img, front)
    return img.resize((W, H), Image.LANCZOS)


def coin_pile(img, cx, cy, s):
    """图标用：简化的银币堆。"""
    put(img, "poly", (cx - s, cy + s * .35, cx - s * .5, cy - s * .45, cx + s * .5, cy - s * .45, cx + s, cy + s * .35), (150, 160, 172), 2.5)
    for dx, dy in ((-.45, .1), (.1, .2), (.5, .05), (-.1, -.25), (.3, -.25)):
        put(img, "ell", (cx + dx * s - s * .28, cy + dy * s - s * .17, cx + dx * s + s * .28, cy + dy * s + s * .17), (212, 220, 230), 1.6)


def ingots(img, cx, cy, s):
    """图标用：简化的金锭堆。"""
    for row, n in enumerate((3, 2, 1)):
        for k in range(n):
            x = cx - (n - 1) * s * .42 + k * s * .84
            y = cy - row * s * .42
            put(img, "poly", (x - s * .4, y, x + s * .4, y, x + s * .3, y - s * .26, x - s * .3, y - s * .26), (240, 204, 110), 2)
            put(img, "rect", (x - s * .4, y, x + s * .4, y + s * .18), (176, 134, 52), 2)


def icon():
    img = canvas()
    cabinet(img, False)
    coin_pile(img, 78, 104, 40)
    ingots(img, 178, 106, 30)
    front = canvas()
    cabinet(front, True)
    img.alpha_composite(front)
    return img.resize((128, 128), Image.LANCZOS)


def main():
    os.makedirs(OUT, exist_ok=True)
    for f in os.listdir(OUT):                       # 旧的币堆叠层与多朝向本体不再用
        if f.startswith(("Silver_", "Gold_", "SilverHalfCell_", "GoldHalfCell_")):
            os.remove(os.path.join(OUT, f))
    b, f, ic = body(False), body(True), icon()
    for old in ("BankVault.png",):
        if os.path.exists(os.path.join(OUT, old)):
            os.remove(os.path.join(OUT, old))
    # 不可旋转，但用 Graphic_Multi：这个运行时的 Graphic_Single 建筑印出来上下颠倒；三个朝向放同一张。
    for v in ("south", "east", "north"):
        b.save(os.path.join(OUT, "BankVault_" + v + ".png"))
    f.save(os.path.join(OUT, "BankVault_Front.png")); ic.save(os.path.join(OUT, "BankVault_Icon.png"))
    prev = Image.new("RGBA", (256 * 3 + 80, 300), (92, 88, 80, 255))
    prev.alpha_composite(b, (20, 20)); prev.alpha_composite(f, (296, 20))
    both = b.copy(); both.alpha_composite(f); prev.alpha_composite(both, (572, 20))
    prev.convert("RGB").save(PREVIEW)
    print("wrote 3 textures; preview", PREVIEW)


if __name__ == "__main__":
    main()
