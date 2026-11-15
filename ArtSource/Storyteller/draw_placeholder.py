# 叙事者占位立绘（Peter 生成正式图之前用）。透明底，粗描边、低饱和平涂。
# 用法: python draw_placeholder.py dm <TexturesDir>   或   python draw_placeholder.py void <TexturesDir>
# 输出 UI/HeroArt/Storytellers/<Name>.png (580x620) 和 <Name>Tiny.png (116x124)
import sys, os, math
from PIL import Image, ImageDraw

W, H = 580, 620
OUT = (24, 20, 22, 255)

def poly(d, pts, fill, w=9):
    d.polygon(pts, fill=fill)
    d.line(pts + [pts[0]], fill=OUT, width=w, joint="curve")

def ell(d, box, fill, w=9):
    d.ellipse(box, fill=fill, outline=OUT, width=w)

def d20(d, cx, cy, r, fill, face):
    pts = [(cx + r * math.cos(math.radians(a)), cy + r * math.sin(math.radians(a))) for a in range(-90, 270, 60)]
    poly(d, pts, fill, 7)
    tri = [pts[0], pts[2], pts[4]]
    poly(d, tri, face, 5)
    for i in (1, 3, 5):
        d.line([pts[i], tri[(i // 2 + 1) % 3]], fill=OUT, width=4)
    d.text((cx - 7, cy - 4), "20", fill=OUT)

def dm(img):
    d = ImageDraw.Draw(img)
    # 斗篷身体
    poly(d, [(150, 600), (190, 330), (290, 270), (390, 330), (430, 600)], (92, 62, 82, 255))
    # 兜帽
    poly(d, [(200, 330), (215, 190), (290, 120), (365, 190), (380, 330), (290, 300)], (110, 74, 98, 255))
    ell(d, (232, 185, 348, 300), (206, 178, 150, 255))  # 脸
    d.ellipse((262, 228, 278, 244), fill=OUT); d.ellipse((302, 228, 318, 244), fill=OUT)
    d.arc((268, 250, 312, 280), 20, 160, fill=OUT, width=6)
    # 地下城主屏风
    poly(d, [(70, 470), (220, 440), (220, 610), (70, 610)], (150, 112, 70, 255))
    poly(d, [(220, 440), (360, 440), (360, 610), (220, 610)], (168, 128, 82, 255))
    poly(d, [(360, 440), (510, 470), (510, 610), (360, 610)], (150, 112, 70, 255))
    for x0, x1 in ((90, 200), (240, 340), (380, 490)):
        d.line([(x0, 520), (x1, 520)], fill=(110, 80, 50, 255), width=6)
        d.line([(x0, 555), (x1 - 20, 555)], fill=(110, 80, 50, 255), width=6)
    # 举起的手和 d20
    ell(d, (400, 300, 460, 360), (206, 178, 150, 255))
    d20(d, 430, 250, 58, (120, 150, 160, 255), (160, 190, 196, 255))

def void(img):
    d = ImageDraw.Draw(img)
    # 黑色巨石背景（虚空石碑）
    poly(d, [(200, 610), (230, 60), (350, 40), (380, 610)], (46, 44, 52, 255))
    for y in (140, 260, 380, 500):
        d.line([(250, y), (330, y + 20)], fill=(140, 40, 50, 255), width=5)
    # 长袍人形
    poly(d, [(120, 610), (170, 360), (290, 300), (410, 360), (460, 610)], (78, 70, 74, 255))
    ell(d, (225, 180, 355, 320), (176, 168, 160, 255))  # 脸
    # 竖排多只眼
    for i, (x, y) in enumerate(((268, 238), (312, 238), (290, 205), (290, 275))):
        d.ellipse((x - 13, y - 9, x + 13, y + 9), fill=(222, 214, 196, 255), outline=OUT, width=4)
        d.ellipse((x - 5, y - 5, x + 5, y + 5), fill=(150, 30, 40, 255))
    # 肩上的肉质触须
    for sx, dirx in ((180, -1), (400, 1)):
        pts = [(sx, 380), (sx + dirx * 70, 300), (sx + dirx * 110, 330), (sx + dirx * 40, 420)]
        poly(d, pts, (128, 60, 64, 255))
    # 手中的虚空光球
    ell(d, (250, 430, 330, 510), (70, 30, 40, 255))
    ell(d, (272, 452, 308, 488), (150, 40, 50, 255), 5)

def main():
    kind, tex = sys.argv[1], sys.argv[2]
    name = {"dm": "LP_DungeonMaster", "void": "AH_VoidWhisperer"}[kind]
    img = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    {"dm": dm, "void": void}[kind](img)
    out = os.path.join(tex, "UI", "HeroArt", "Storytellers")
    os.makedirs(out, exist_ok=True)
    img.save(os.path.join(out, name + ".png"))
    img.resize((116, 124), Image.LANCZOS).save(os.path.join(out, name + "Tiny.png"))
    print("wrote", out, name)

main()
