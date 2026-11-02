# 1.100.0 魔力湖泊美术：派系图标、秘传结晶（白色，游戏里按职业颜色着色）、力场柱、48 个职业水晶能力图标。
# 用法（模组根目录）：$env:PATH="E:\Python;E:\Python\Library\bin;"+$env:PATH; E:\Python\python.exe ArtSource/ManaLake/draw_manalake.py
# 图标列表来自 gen_crystals.ps1 写出的 icons.json。
import json, math, os, re
from PIL import Image, ImageDraw, ImageFont, ImageFilter

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
TEX = os.path.join(ROOT, "Textures")
FONT = r"C:\Windows\Fonts\msyhbd.ttc"
S = 128

def save(img, rel):
    p = os.path.join(TEX, rel)
    os.makedirs(os.path.dirname(p), exist_ok=True)
    img.save(p)

def rgb(s):
    v = [float(x) for x in re.findall(r"[\d.]+", s)]
    return tuple(int(round(c * 255)) for c in v[:3])

def faction_icon():
    # 灰度：湖面（三道波纹）上浮着一枚结晶。游戏按派系颜色着色。
    big = S * 4
    im = Image.new("RGBA", (big, big), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    w = (235, 235, 235, 255)
    cx = big // 2
    # 结晶
    top, mid, bot = 40, 250, 330
    d.polygon([(cx, top), (cx + 95, mid - 70), (cx + 60, bot), (cx - 60, bot), (cx - 95, mid - 70)], fill=w)
    d.polygon([(cx, top), (cx + 18, mid - 60), (cx, bot), (cx - 18, mid - 60)], fill=(170, 170, 170, 255))
    # 波纹
    for i, y in enumerate((360, 410, 460)):
        pts = []
        for x in range(40, big - 40, 4):
            pts.append((x, y + 14 * math.sin((x / big) * math.pi * 5 + i)))
        d.line(pts, fill=w, width=26 - i * 4)
    save(im.resize((S, S), Image.LANCZOS), "Factions/ManaLake.png")

def crystal_item():
    big = S * 4
    im = Image.new("RGBA", (big, big), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    cx = big // 2
    outline = (60, 70, 90, 255)
    pts = [(cx, 50), (cx + 120, 190), (cx + 80, 450), (cx - 80, 450), (cx - 120, 190)]
    d.polygon(pts, fill=(235, 240, 250, 255), outline=outline)
    d.polygon([(cx, 50), (cx + 120, 190), (cx, 230)], fill=(255, 255, 255, 255))
    d.polygon([(cx, 50), (cx - 120, 190), (cx, 230)], fill=(205, 212, 228, 255))
    d.polygon([(cx - 120, 190), (cx, 230), (cx - 80, 450)], fill=(180, 188, 208, 255))
    d.polygon([(cx + 120, 190), (cx, 230), (cx + 80, 450)], fill=(225, 230, 242, 255))
    d.polygon([(cx, 230), (cx + 80, 450), (cx - 80, 450)], fill=(200, 206, 222, 255))
    d.line(pts + [pts[0]], fill=outline, width=10)
    # 高光
    d.ellipse((cx + 30, 120, cx + 60, 150), fill=(255, 255, 255, 255))
    save(im.resize((S, S), Image.LANCZOS), "LP/Items/ArcanumCrystal.png")

def pylon():
    big = S * 4
    im = Image.new("RGBA", (big, big), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    cx = big // 2
    d.polygon([(cx - 110, 470), (cx + 110, 470), (cx + 70, 400), (cx - 70, 400)], fill=(90, 95, 110, 255), outline=(40, 40, 50, 255))
    d.polygon([(cx - 45, 400), (cx + 45, 400), (cx + 25, 110), (cx - 25, 110)], fill=(140, 150, 170, 255), outline=(40, 40, 50, 255))
    d.ellipse((cx - 60, 40, cx + 60, 160), fill=(120, 200, 255, 255), outline=(30, 60, 110, 255), width=8)
    for y in (180, 250, 320):
        d.line([(cx - 20, y), (cx + 20, y)], fill=(120, 200, 255, 255), width=10)
    glow = im.filter(ImageFilter.GaussianBlur(6))
    out = Image.alpha_composite(glow, im)
    save(out.resize((S, S), Image.LANCZOS), "LP/Arcana/ForceFieldPylon.png")

def glyph(label):
    label = label.replace("超魔·", "")
    return label[0]

def ability_icon(entry, font):
    big = S * 4
    col = rgb(entry["color"])
    im = Image.new("RGBA", (big, big), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    # 底：深湖蓝圆角方块，带一点职业色的径向光
    d.rounded_rectangle((12, 12, big - 12, big - 12), radius=70, fill=(16, 28, 48, 255))
    glow = Image.new("RGBA", (big, big), (0, 0, 0, 0))
    gd = ImageDraw.Draw(glow)
    gd.ellipse((90, 90, big - 90, big - 90), fill=col + (120,))
    glow = glow.filter(ImageFilter.GaussianBlur(60))
    im = Image.alpha_composite(im, glow)
    d = ImageDraw.Draw(im)
    # 环：主动实线，被动虚线
    box = (54, 54, big - 54, big - 54)
    if entry["passive"]:
        for a in range(0, 360, 24):
            d.arc(box, a, a + 14, fill=col + (255,), width=20)
    else:
        d.ellipse(box, outline=col + (255,), width=20)
    # 环数：底部小点
    n = int(entry["ring"])
    for i in range(n):
        ang = math.radians(90 + (i - (n - 1) / 2) * 11)
        r = (big - 108) / 2
        x = big / 2 + r * math.cos(ang)
        y = big / 2 + r * math.sin(ang)
        d.ellipse((x - 11, y - 11, x + 11, y + 11), fill=(240, 245, 255, 255))
    ch = glyph(entry["label"])
    bb = d.textbbox((0, 0), ch, font=font)
    tw, th = bb[2] - bb[0], bb[3] - bb[1]
    tx, ty = (big - tw) / 2 - bb[0], (big - th) / 2 - bb[1] - 10
    d.text((tx + 6, ty + 8), ch, font=font, fill=(0, 0, 0, 160))
    d.text((tx, ty), ch, font=font, fill=(250, 250, 255, 255))
    save(im.resize((S, S), Image.LANCZOS), "LP/Arcana/%s.png" % entry["key"])

def main():
    faction_icon()
    crystal_item()
    pylon()
    font = ImageFont.truetype(FONT, 250)
    with open(os.path.join(os.path.dirname(os.path.abspath(__file__)), "icons.json"), encoding="utf-8-sig") as f:
        entries = json.load(f)
    for e in entries:
        ability_icon(e, font)
    print("icons:", len(entries))

if __name__ == "__main__":
    main()
