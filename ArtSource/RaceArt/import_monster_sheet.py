# 1030 Peter 给的怪物贴图总表（ArtSource/WildMonsters-sheet-1030.png）切成游戏贴图。
# 表的格式：每行 128px 高，第 0 列是名字 / 参考图，第 1～3 列依次是 south / east / north（west 由游戏镜像 east）。
# 背景是纯色 (120,112,95)：按颜色距离抠掉，边缘按距离渐变成半透明，免得留一圈棕边。
#  - 六只已有的怪 → Textures/Things/Pawn/Animal/LP_WildMonsters/<名>/<名>_{south,east,north}.png，干尸照 draw_wildmonsters.dessicate 重做
#  - 其余（软泥类、修格斯）→ ArtSource/MonsterSheet/<名>_*.png，先存着
#  - 最后一行掘地兽皮（64px）→ Textures/Things/Item/Resource/LP_BuletteHide.png
# 用法：python -I ArtSource/RaceArt/import_monster_sheet.py <模组根目录>
import os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from PIL import Image
import draw_wildmonsters as wm

ROOT = sys.argv[1] if len(sys.argv) > 1 else os.getcwd()
SHEET = os.path.join(ROOT, "ArtSource", "WildMonsters-sheet-1030.png")
BG = (120, 112, 95)
CELL = 128
ROWS = ["Warg", "Muffalo", "AcidBlob", "Basilisk", "BlackPudding", "BrownPudding", "Bulette", "CarrionCrawler", "GelatinousCube",
        "GrayOoze", "GreenSlime", "Manticore", "MoldJelly", "RustMonster", "Shoggoth", "ToughBlob", "BuletteHide"]
GAME = {"RustMonster", "GelatinousCube", "Basilisk", "Manticore", "Bulette", "CarrionCrawler"}


def cutout(img):
    img = img.convert("RGBA")
    px = img.load()
    for y in range(img.height):
        for x in range(img.width):
            r, g, b, a = px[x, y]
            d = max(abs(r - BG[0]), abs(g - BG[1]), abs(b - BG[2]))
            if d <= 10:
                px[x, y] = (0, 0, 0, 0)
            elif d <= 40:  # 抗锯齿的边：去掉混进来的背景色
                t = (d - 10) / 30
                un = lambda c, k: max(0, min(255, int((c - BG[k] * (1 - t)) / t)))
                px[x, y] = (un(r, 0), un(g, 1), un(b, 2), int(255 * t))
    return img


def cell(sheet, row, col, size=CELL):
    return sheet.crop((col * CELL, row * CELL, col * CELL + size, row * CELL + size))


def main():
    sheet = Image.open(SHEET).convert("RGBA")
    assert sheet.size == (4 * CELL, len(ROWS) * CELL), sheet.size
    for row, name in enumerate(ROWS):
        if name in ("Warg", "Muffalo"):
            continue
        if name == "BuletteHide":
            img = cutout(cell(sheet, row, 1))
            img = img.crop(img.getbbox())
            canvas = Image.new("RGBA", (64, 64), (0, 0, 0, 0))
            img.thumbnail((60, 60), Image.LANCZOS)
            canvas.alpha_composite(img, ((64 - img.width) // 2, (64 - img.height) // 2))
            path = os.path.join(ROOT, "Textures", "Things", "Item", "Resource", "LP_BuletteHide.png")
            os.makedirs(os.path.dirname(path), exist_ok=True)
            canvas.save(path)
            continue
        game = name in GAME
        folder = os.path.join(ROOT, "Textures", *wm.REL.split("/"), name) if game else os.path.join(ROOT, "ArtSource", "MonsterSheet")
        os.makedirs(folder, exist_ok=True)
        for col, rot in ((1, "south"), (2, "east"), (3, "north")):
            img = cutout(cell(sheet, row, col))
            img.save(os.path.join(folder, f"{name}_{rot}.png"))
            if game:
                wm.dessicate(img, name).save(os.path.join(folder, f"Dessicated_{name}_{rot}.png"))
    print("ok")


if __name__ == "__main__":
    main()
