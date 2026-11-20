# 【已停用 2026-11-19】Peter 改要原版风格，书架贴图现由 draw_from_vanilla.py 生成；运行本脚本会覆盖掉原版风格的 north/south。
# 把 Peter 画的 3:1 三视图（品红底，左书脊 / 中封面 / 右俯视）装成书架竖放贴图。
# 注意：输出时书脊 → _north，俯视 → _south（原版朝南书架用 _north 画书脊，见下方 outs）。
# 用法：python -I install_sheets.py <LichPhylactery 根目录> <sheet 目录> [只处理的名字...]
# 每张 sheet 叫 <名字>-sheet.png；输出 64×64 透明底 <前缀>_Vertical_{south,east,north}.png。
# north：书类把俯视图转 90° 变成竖条（原版 north 也是竖条）；卷轴的俯视是圆头，放进书架格子不像样，改用 south 镜像。
import sys, os
from PIL import Image, ImageFilter, ImageChops

MOD, SHEETS = sys.argv[1], sys.argv[2]
ONLY = set(sys.argv[3:])
TEX = os.path.join(MOD, "Textures")
ITEMS = {
    "Tome_STR": "LP/Items/Tomes/Vertical/Tome_STR",
    "Tome_DEX": "LP/Items/Tomes/Vertical/Tome_DEX",
    "Tome_CON": "LP/Items/Tomes/Vertical/Tome_CON",
    "Tome_INT": "LP/Items/Tomes/Vertical/Tome_INT",
    "Tome_WIS": "LP/Items/Tomes/Vertical/Tome_WIS",
    "Tome_CHA": "LP/Items/Tomes/Vertical/Tome_CHA",
    "Demonomicon": "Items/Abyss/Vertical/Demonomicon",
    "LP_NetherilStudyScroll": "Things/Item/Vertical/LP_NetherilStudyScroll",
    "LP_NetherilArcaneScroll": "Things/Item/Vertical/LP_NetherilArcaneScroll",
    "LP_KarsusScroll": "Things/Item/Vertical/LP_KarsusScroll",
}
SCROLLS = {"LP_NetherilStudyScroll", "LP_NetherilArcaneScroll", "LP_KarsusScroll"}
# 目标框（64×64 画布里）：竖条最高 46、最宽 14，底边 y=55；封面最大 32×40，底边 y=52。和原版 Textbook/Tome 的竖放贴图一致。
SPINE = (14, 46, 55)
COVER = (32, 40, 52)

def key(im):
    """品红底 → 透明。底色在 (255,44,249) 附近浮动，也有偏粉的，用相对条件。"""
    rgb = im.convert("RGB")
    r, g, b = rgb.split()
    mask = Image.eval(ImageChops.subtract(r, g), lambda v: 255 if v > 110 else 0)
    mask = ImageChops.multiply(mask, Image.eval(ImageChops.subtract(b, g), lambda v: 255 if v > 90 else 0))
    alpha = ImageChops.invert(mask)
    alpha = alpha.filter(ImageFilter.MinFilter(5))   # 吃掉 2px 品红描边
    out = rgb.convert("RGBA")
    out.putalpha(alpha)
    return out

def split3(im):
    a = im.getchannel("A")
    w, h = im.size
    cols = [any(a.getpixel((x, y)) for y in range(0, h, 2)) for x in range(w)]
    runs, start, gap = [], None, 0
    for x, c in enumerate(cols + [False] * 40):
        if c:
            if start is None:
                start = x
            gap, end = 0, x
        elif start is not None:
            gap += 1
            if gap > 30:
                runs.append((start, end + 1)); start = None
    runs = [r for r in runs if r[1] - r[0] > 20]
    if len(runs) != 3:
        raise SystemExit("expected 3 views, got %s" % runs)
    views = []
    for x0, x1 in runs:
        v = im.crop((x0, 0, x1, h))
        views.append(v.crop(v.getbbox()))
    return views

def fit(v, box):
    mw, mh, bottom = box
    s = min(mw / v.width, mh / v.height)
    w, h = max(1, round(v.width * s)), max(1, round(v.height * s))
    # 预乘 alpha 缩放，避免透明处的颜色渗到边上
    r, g, b, a = v.split()
    pm = Image.merge("RGB", [ImageChops.multiply(c, a) for c in (r, g, b)])
    pm = pm.resize((w, h), Image.LANCZOS)
    a2 = a.resize((w, h), Image.LANCZOS)
    px = pm.load(); pa = a2.load()
    out = Image.new("RGBA", (w, h))
    po = out.load()
    for y in range(h):
        for x in range(w):
            al = pa[x, y]
            if al < 8:
                continue
            c = px[x, y]
            po[x, y] = tuple(min(255, c[i] * 255 // al) for i in range(3)) + (al,)
    canvas = Image.new("RGBA", (64, 64), (0, 0, 0, 0))
    canvas.alpha_composite(out, ((64 - w) // 2, bottom - h))
    return canvas

for name, prefix in ITEMS.items():
    if ONLY and name not in ONLY:
        continue
    sheet = Image.open(os.path.join(SHEETS, name + "-sheet.png"))
    south, east, north = split3(key(sheet))
    if name in SCROLLS:
        north = south.transpose(Image.FLIP_LEFT_RIGHT)
    else:
        north = north.rotate(90, expand=True)
    # 2026-11-19 修正：原版书架朝南（横放）时用 _north 画书，原版 north = 书脊、south = 书页切口。
    # sheet 里的变量名沿用"左 south"的叫法，但左边画的是书脊，所以书脊存成 north，俯视/镜像那张存成 south。
    # east 不再用手绘封面（朝东/西书架叠放会乱），改由 draw_east_from_vanilla.py 按原版形状生成。
    outs = {"north": fit(south, SPINE), "south": fit(north, SPINE)}
    for d, im in outs.items():
        p = os.path.join(TEX, prefix + "_Vertical_%s.png" % d)
        os.makedirs(os.path.dirname(p), exist_ok=True)
        im.save(p)
    print(name, south.size, east.size, north.size)
