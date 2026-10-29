# 1.24.0 预览图：把游戏内截图（Test.ps1 的 Core 档在测试档目录里留下的 Races124-*.png、Races124Map-*.png、Races124Icons-*.png）拼成一张。
# 用法：python -I ArtSource/RaceArt/compose_preview_124.py <测试档目录> <输出.png>
import os, sys
from PIL import Image, ImageDraw, ImageFont

src, out = sys.argv[1], sys.argv[2]
W = 1372


def font(size):
    for f in (r"C:\Windows\Fonts\msyh.ttc", r"C:\Windows\Fonts\simhei.ttf"):
        if os.path.exists(f):
            return ImageFont.truetype(f, size)
    return ImageFont.load_default()


def load(name):
    return Image.open(os.path.join(src, name)).convert("RGB")


def fit(im, w):
    return im.resize((w, round(im.height * w / im.width)), Image.LANCZOS)


rows = []
title = Image.new("RGB", (W, 56), (34, 38, 40))
ImageDraw.Draw(title).text((16, 10), "DND边缘大陆 1.24.0：元素裔 · 象人 · 食人魔 · 鸟人 · 寒冰魔 · 炎魔 · 天穹神裔 · 巨灵（游戏内截图）", fill=(236, 236, 236), font=font(26))
rows.append(title)


def caption(text):
    im = Image.new("RGB", (W, 34), (46, 52, 55))
    ImageDraw.Draw(im).text((14, 5), text, fill=(220, 220, 220), font=font(20))
    return im


# 图鉴四页：窗口区域 1100×760 居中（1920×1080 截图）
rows.append(caption("图鉴四向（南 / 东 / 北 / 西；不戴帽子，鸟人穿风羽披风，巨灵为火 / 水两种血脉）"))
pages = [load("Races124-%d.png" % i).crop((275, 66, 1647, 1013)) for i in range(1, 5)]
half = [fit(p, W // 2) for p in pages]
grid = Image.new("RGB", (W, half[0].height * 2), (24, 26, 27))
for i, p in enumerate(half):
    grid.paste(p, ((i % 2) * (W // 2), (i // 2) * half[0].height))
rows.append(grid)

# 地图上的实际渲染：朝南、朝东、朝北
rows.append(caption("地图上的实际渲染（上：朝南；中：朝东；下：朝北）"))
for n in ("south", "east", "north"):
    rows.append(fit(load("Races124Map-%s.png" % n).crop((330, 250, 1590, 750)), W))

# 图标对照：TextureGallery 1160×760 居中
rows.append(caption("图标对照（每页上排为原版同规格参照，下排为本模组新图）"))
icons = [fit(load("Races124Icons-%d.png" % i).crop((380, 160, 1540, 920)), W // 3) for i in range(1, 4)]
strip = Image.new("RGB", (W, icons[0].height), (24, 26, 27))
for i, p in enumerate(icons):
    strip.paste(p, (i * (W // 3), 0))
rows.append(strip)

H = sum(r.height for r in rows)
sheet = Image.new("RGB", (W, H), (24, 26, 27))
y = 0
for r in rows:
    sheet.paste(r, (0, y)); y += r.height
os.makedirs(os.path.dirname(os.path.abspath(out)), exist_ok=True)
sheet.save(out)
print(out, sheet.size)
