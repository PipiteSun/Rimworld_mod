# 1.101.0 秘金深钻贴图：原版深层钻机的画法（俯视略带正面、平涂、深色粗描边），矮人符文钻架 + 秘银/精金光。
# 用法（模组根目录）：$env:PATH="E:\Python;E:\Python\Library\bin;"+$env:PATH; E:\Python\python.exe ArtSource/MythicDrill/draw_drill.py
import os
from PIL import Image, ImageDraw, ImageFilter

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
OUT = os.path.join(ROOT, "Textures", "Things", "Building", "LP_MythicDrill.png")
S = 256          # 游戏贴图（drawSize 2x2）
K = 4            # 超采样
B = S * K

OL = (34, 30, 36, 255)          # 描边
STEEL_D = (86, 90, 98, 255)
STEEL = (122, 127, 136, 255)
STEEL_L = (160, 166, 175, 255)
BRASS = (170, 136, 70, 255)
BRASS_L = (204, 172, 98, 255)
MITH = (214, 232, 244, 255)
ADAM = (128, 86, 170, 255)
HOLE = (28, 24, 30, 255)

def P(*xy):
    return [(x * K, y * K) for x, y in zip(xy[0::2], xy[1::2])]

def poly(d, pts, fill, w=3):
    d.polygon(pts, fill=fill)
    d.line(pts + [pts[0]], fill=OL, width=w * K, joint="curve")

def rect(d, x0, y0, x1, y1, fill, w=3):
    poly(d, P(x0, y0, x1, y0, x1, y1, x0, y1), fill, w)

def main():
    im = Image.new("RGBA", (B, B), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    cx = 128

    # 钻孔与地面碎石（底部中间）
    d.ellipse([(cx - 34) * K, (196) * K, (cx + 34) * K, (226) * K], fill=HOLE, outline=OL, width=3 * K)
    for (x, y, c) in [(cx - 40, 214, MITH), (cx + 38, 206, ADAM), (cx + 30, 224, MITH), (cx - 28, 226, ADAM)]:
        poly(d, P(x - 4, y, x, y - 5, x + 5, y, x, y + 4), c, 2)

    # 底座：八角钢平台
    base = P(cx - 70, 150, cx - 56, 136, cx + 56, 136, cx + 70, 150, cx + 70, 196, cx + 56, 208, cx - 56, 208, cx - 70, 196)
    poly(d, base, STEEL_D, 4)
    top = P(cx - 62, 150, cx - 52, 142, cx + 52, 142, cx + 62, 150, cx + 62, 174, cx - 62, 174)
    d.polygon(top, fill=STEEL)
    # 铆钉
    for x in (cx - 58, cx - 30, cx + 30, cx + 58):
        for y in (156, 196):
            d.ellipse([(x - 3) * K, (y - 3) * K, (x + 3) * K, (y + 3) * K], fill=STEEL_L, outline=OL, width=K)
    # 黄铜护板与矮人符文
    rect(d, cx - 30, 178, cx + 30, 202, BRASS, 3)
    d.line(P(cx - 30, 184, cx + 30, 184), fill=BRASS_L, width=2 * K)
    for i, x in enumerate((cx - 18, cx, cx + 18)):
        rune = [P(x - 5, 198, x, 188, x + 5, 198), P(x - 5, 188, x + 5, 198), P(x, 187, x, 199)][i]
        d.line(rune, fill=OL, width=2 * K)

    # 井架：两条斜腿 + 横撑
    legL = P(cx - 58, 152, cx - 48, 152, cx - 10, 36, cx - 18, 36)
    legR = P(cx + 58, 152, cx + 48, 152, cx + 10, 36, cx + 18, 36)
    poly(d, legL, STEEL, 3)
    poly(d, legR, STEEL, 3)
    for y, half in ((118, 40), (84, 29)):
        rect(d, cx - half, y, cx + half, y + 7, STEEL_D, 3)
    d.line(P(cx - 44, 125, cx + 22, 91), fill=OL, width=3 * K)
    d.line(P(cx + 44, 125, cx - 22, 91), fill=OL, width=3 * K)

    # 顶部滑轮
    rect(d, cx - 22, 26, cx + 22, 40, STEEL_D, 3)
    d.ellipse([(cx - 12) * K, (14) * K, (cx + 12) * K, (38) * K], fill=BRASS, outline=OL, width=3 * K)
    d.ellipse([(cx - 4) * K, (22) * K, (cx + 4) * K, (30) * K], fill=OL)

    # 钻杆（中间竖直），带符文光圈
    rect(d, cx - 8, 38, cx + 8, 206, STEEL_L, 3)
    d.line(P(cx - 3, 42, cx - 3, 200), fill=(196, 202, 210, 255), width=2 * K)
    for y, c in ((70, MITH), (108, ADAM), (146, MITH)):
        rect(d, cx - 12, y, cx + 12, y + 8, c, 2)

    # 侧面电机箱（右）
    rect(d, cx + 74, 154, cx + 104, 192, STEEL_D, 3)
    rect(d, cx + 80, 160, cx + 98, 172, STEEL, 2)
    d.line(P(cx + 70, 170, cx + 74, 170), fill=OL, width=4 * K)
    d.ellipse([(cx + 85) * K, (176) * K, (cx + 93) * K, (184) * K], fill=MITH, outline=OL, width=K)

    # 白银探针（左）
    rect(d, cx - 98, 160, cx - 78, 196, STEEL_D, 3)
    poly(d, P(cx - 92, 160, cx - 84, 160, cx - 88, 120), (200, 204, 210, 255), 2)

    # 符文光晕
    glow = Image.new("RGBA", (B, B), (0, 0, 0, 0))
    g = ImageDraw.Draw(glow)
    for y, c in ((74, MITH), (112, ADAM), (150, MITH)):
        g.ellipse([(cx - 22) * K, (y - 10) * K, (cx + 22) * K, (y + 14) * K], fill=c[:3] + (90,))
    glow = glow.filter(ImageFilter.GaussianBlur(6 * K))
    out = Image.alpha_composite(glow, im)

    out = out.resize((S, S), Image.LANCZOS)
    os.makedirs(os.path.dirname(OUT), exist_ok=True)
    out.save(OUT)
    print("wrote", OUT)

if __name__ == "__main__":
    main()
