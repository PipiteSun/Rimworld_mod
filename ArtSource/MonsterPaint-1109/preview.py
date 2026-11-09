import sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from PIL import Image, ImageDraw
from paintify import paintify
ROOT = sys.argv[1]; OUT = sys.argv[2]
items = [("眼魔", "Textures/LegendaryMonsters/Beholder/Naked_Hulk"), ("树精", "Textures/Things/Pawn/Animal/LP_E3/Treant/Treant"),
 ("锈蚀怪", "Textures/Things/Pawn/Animal/LP_WildMonsters/RustMonster/RustMonster"), ("蛇蜥", "Textures/Things/Pawn/Animal/LP_WildMonsters/Basilisk/Basilisk"),
 ("蝎尾狮", "Textures/Things/Pawn/Animal/LP_WildMonsters/Manticore/Manticore"), ("掘地兽", "Textures/Things/Pawn/Animal/LP_WildMonsters/Bulette/Bulette"),
 ("拟态怪", "Textures/Things/Pawn/Animal/LP_E3/Mimic/Mimic"), ("狮鹫", "Textures/Things/Pawn/Animal/LP_BeastsDND2/Griffon/Griffon")]
C = 200
sheet = Image.new("RGBA", (4 * C + 20, len(items) * C + 30), (34, 34, 34, 255))
d = ImageDraw.Draw(sheet)
d.text((10, 8), "old | v2 | v3 joints blended | v3 east", fill=(220, 220, 220))
for i, (n, p) in enumerate(items):
    im = Image.open(os.path.join(ROOT, f"{p}_south.png")).convert("RGBA")
    ie = Image.open(os.path.join(ROOT, f"{p}_east.png")).convert("RGBA")
    for k, x in enumerate((im, paintify(im, v=2), paintify(im, v=2, blend_joints=True), paintify(ie, v=2, blend_joints=True))):
        x = x.resize((C, C), Image.LANCZOS)
        sheet.alpha_composite(x, (10 + k * C, 30 + i * C))
sheet.save(OUT); print("ok")
