# 书架竖放贴图（CompProperties_Book.verticalGraphic）：拿原版 Textbook / Schematic 的 Vertical 三向贴图，
# 按每本书图标的封面颜色重新上色，生成 <输出>_Vertical_{south,east,north}.png（Graphic_Multi，west 由 east 镜像）。
# 用法：python -I draw_spines.py <LichPhylactery 根目录> <原版贴图导出目录 _VanillaTextures>
import sys, os, colorsys
from PIL import Image

MOD, VAN = sys.argv[1], sys.argv[2]
TEX = os.path.join(MOD, "Textures")
DIRS = ("south", "east", "north")

# (图标 texPath, 输出 texPath, 底版, 取色点(128 图上), 饰带颜色或 None)
BOOKS = [
    ("LP/Items/Tomes/Tome_STR", "LP/Items/Tomes/Vertical/Tome_STR", "book", (95, 60), (214, 172, 74)),
    ("LP/Items/Tomes/Tome_DEX", "LP/Items/Tomes/Vertical/Tome_DEX", "book", (95, 60), (214, 172, 74)),
    ("LP/Items/Tomes/Tome_CON", "LP/Items/Tomes/Vertical/Tome_CON", "book", (95, 60), (214, 172, 74)),
    ("LP/Items/Tomes/Tome_INT", "LP/Items/Tomes/Vertical/Tome_INT", "book", (95, 60), (214, 172, 74)),
    ("LP/Items/Tomes/Tome_WIS", "LP/Items/Tomes/Vertical/Tome_WIS", "book", (95, 60), (214, 172, 74)),
    ("LP/Items/Tomes/Tome_CHA", "LP/Items/Tomes/Vertical/Tome_CHA", "book", (95, 60), (214, 172, 74)),
    ("Items/Abyss/Demonomicon", "Items/Abyss/Vertical/Demonomicon", "book", (40, 60), (200, 40, 40)),
    # 卷轴：取色点给纸色，外壳（原版硬壳的位置）用卷轴木轴色
    ("Things/Item/LP_NetherilStudyScroll", "Things/Item/Vertical/LP_NetherilStudyScroll", "scroll", (64, 64), None),
    ("Things/Item/LP_NetherilArcaneScroll", "Things/Item/Vertical/LP_NetherilArcaneScroll", "scroll", (64, 64), (150, 60, 50)),
    ("Things/Item/LP_KarsusScroll", "Things/Item/Vertical/LP_KarsusScroll", "scroll", (64, 44), (140, 110, 220)),
]
BASE = {
    "book": os.path.join(VAN, "Core", "Things", "Item", "Book", "Textbook", "Textbook_Vertical_%s.png"),
    "scroll": os.path.join(VAN, "Core", "Things", "Item", "Book", "Schematic", "Schematic_Vertical_%s.png"),
}

def lum(c):
    return 0.299 * c[0] + 0.587 * c[1] + 0.114 * c[2]

def sample(icon, pt):
    im = Image.open(os.path.join(TEX, icon + ".png")).convert("RGBA").resize((128, 128))
    x0, y0 = pt
    px = [im.getpixel((x, y)) for x in range(x0 - 3, x0 + 4) for y in range(y0 - 3, y0 + 4)]
    px = [p for p in px if p[3] > 200]
    return tuple(sum(p[i] for p in px) // len(px) for i in range(3))

def is_cover(p, kind):
    h, l, s = colorsys.rgb_to_hls(p[0] / 255, p[1] / 255, p[2] / 255)
    if kind == "book":
        return l < 0.6        # 原版 Textbook：深棕封面，浅粉书页
    return s > 0.25 and l < 0.75  # Schematic：棕色硬壳，白纸

SCROLL_WOOD = {"LP_KarsusScroll": (214, 180, 82)}
SCROLL_PAGE = {"LP_KarsusScroll": (198, 182, 236)}

def recolor(base, color, kind, name="", south=False):
    page = (236, 222, 190)
    if kind == "scroll":
        page, color = SCROLL_PAGE.get(name, color), SCROLL_WOOD.get(name, (112, 78, 52))
    im = Image.open(base).convert("RGBA")
    cov = [im.getpixel((x, y)) for x in range(im.width) for y in range(im.height)]
    cov = [p for p in cov if p[3] > 0 and is_cover(p, kind)]
    ref = sum(lum(p) for p in cov) / max(1, len(cov))
    out = im.copy()
    for y in range(im.height):
        for x in range(im.width):
            p = im.getpixel((x, y))
            if p[3] == 0:
                continue
            if is_cover(p, kind):
                k = lum(p) / ref
                q = tuple(max(0, min(255, int(c * k))) for c in color)
            elif kind == "book" and south and y >= 31:  # 原版这里是浅色书脊下半，改成封面色，书架上才看得出颜色
                k = lum(p) / 162.0 * 1.15
                q = tuple(max(0, min(255, int(c * k))) for c in color)
            else:  # 书页 / 纸面
                k = lum(p) / 230.0
                q = tuple(max(0, min(255, int(c * k))) for c in page)
            out.putpixel((x, y), q + (p[3],))
    return out

def band(im, color, frac):
    """在封面像素上横贴一条饰带（书脊上的烫金条）。"""
    bb = im.getbbox()
    if not bb:
        return
    y = int(bb[1] + (bb[3] - bb[1]) * frac)
    for yy in (y, y + 1):
        for x in range(bb[0], bb[2]):
            p = im.getpixel((x, yy))
            if p[3] > 0 and lum(p) < 200:
                im.putpixel((x, yy), color + (p[3],))

for icon, outp, kind, pt, accent in BOOKS:
    color = sample(icon, pt)
    for d in DIRS:
        im = recolor(BASE[kind] % d, color, kind, icon.rsplit("/", 1)[1], d != "east")
        if accent:
            band(im, accent, 0.72)
            if kind == "book":
                band(im, accent, 0.86)
        path = os.path.join(TEX, outp + "_Vertical_%s.png" % d)
        os.makedirs(os.path.dirname(path), exist_ok=True)
        im.save(path)
    print(outp, color)
