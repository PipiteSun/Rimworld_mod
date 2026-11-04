# 当前 DND 装备贴图总览（中文标注）：武器 / 衣甲穿戴（男性正面）/ 物品图标，各出一张。
# 用法：E:\Python\python.exe ArtSource\ArtRedraw-1102\gallery.py <输出目录>
import glob, json, os, re, sys
import xml.etree.ElementTree as ET
from PIL import Image, ImageDraw, ImageFont

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(os.path.dirname(HERE))
OUT = sys.argv[1] if len(sys.argv) > 1 else HERE
FONT = ImageFont.truetype(r"C:\Windows\Fonts\msyh.ttc", 15)
TITLE = ImageFont.truetype(r"C:\Windows\Fonts\msyhbd.ttc", 26)

labels = {}
for f in glob.glob(os.path.join(ROOT, "**", "Defs", "**", "*.xml"), recursive=True):
    if "\\ArtSource\\" in f:
        continue
    try:
        for n in ET.parse(f).getroot():
            dn, lb = n.findtext("defName"), n.findtext("label")
            if dn and lb:
                labels.setdefault(dn, lb)
    except Exception:
        pass

files = json.load(open(os.path.join(HERE, "equip.json"), encoding="utf8"))
skip = re.compile(r"\\Projectiles\\|\\Proj_", re.I)
groups = {"weapon": [], "worn": [], "icon": []}
seen_worn = set()
for p, (role, dn) in sorted(files.items(), key=lambda kv: labels.get(kv[1][1], kv[1][1])):
    if skip.search(p) or not os.path.exists(p):
        continue
    lb = labels.get(dn, dn)
    if role == "worn":
        # 每件衣甲只取一张：男性正面，头饰取正面
        if dn in seen_worn or not p.endswith("_south.png") or re.search(r"_(Female|Thin|Fat|Hulk)_south", p):
            continue
        seen_worn.add(dn)
    groups[role].append((p, lb))
# 补丁里改过穿戴图路径的三件（equip.json 只来自 Defs）
for name in ("ElvenChain", "GithAstralArmor", "MinotaurHornHelm"):
    d = os.path.join(ROOT, "Textures", "LP", "Apparel", name)
    for cand in (f"{name}_Male_south.png", f"{name}_south.png"):
        if os.path.exists(os.path.join(d, cand)) and "LP_" + name not in seen_worn:
            groups["worn"].append((os.path.join(d, cand), labels.get("LP_" + name, name)))
            break
groups["worn"].sort(key=lambda t: t[1])


def sheet(items, title, path, cell=128, cols=10):
    rows = (len(items) + cols - 1) // cols
    lh = 22
    W, H = cols * (cell + 8) + 8, 50 + rows * (cell + lh + 8)
    im = Image.new("RGB", (W, H), (104, 99, 92))
    d = ImageDraw.Draw(im)
    d.text((12, 10), f"{title}（{len(items)} 件）", font=TITLE, fill=(245, 240, 228))
    for i, (p, lb) in enumerate(items):
        x = 8 + (i % cols) * (cell + 8)
        y = 50 + (i // cols) * (cell + lh + 8)
        d.rectangle((x, y, x + cell - 1, y + cell - 1), fill=(116, 110, 102))
        t = Image.open(p).convert("RGBA").resize((cell, cell), Image.LANCZOS)
        im.paste(t, (x, y), t)
        s = lb
        while d.textlength(s, font=FONT) > cell + 4 and len(s) > 1:
            s = s[:-1]
        d.text((x + (cell - d.textlength(s, font=FONT)) / 2, y + cell + 2), s, font=FONT, fill=(240, 236, 226))
    im.save(path, optimize=True)
    print(path, len(items))


sheet(groups["weapon"], "DND 武器与投掷物", os.path.join(OUT, "DND装备贴图-武器.png"))
sheet(groups["worn"], "DND 衣甲·穿戴效果（男性正面）", os.path.join(OUT, "DND装备贴图-衣甲穿戴.png"))
sheet(groups["icon"], "DND 衣甲与饰品·物品图标", os.path.join(OUT, "DND装备贴图-物品图标.png"))
