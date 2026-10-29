# 1.29.0 器械师路径美术：9 个能力图标 128×128 + 路径背景 950×1515（齿轮与符文锤）。
#   直接复用 draw_class_art.py 的画法（Icon.layer：粗黑描边 → 平涂 → 右下暗部；VPE 配色；塔罗牌式背景框）。
# 用法：python draw_artificer_art.py <输出目录 Bridges/VPE/Textures/LP/Classes/Artificer>
import math, os, sys, random
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
OUT = sys.argv[1] if len(sys.argv) > 1 else 'out'
sys.argv = sys.argv[:1]  # draw_class_art 在导入时读取 argv[1]，这里清掉以免误用
from draw_class_art import (Icon, ImageDraw, ImageChops, ImageFilter, Image, union, subtract, poly_mask, ellipse_mask, line_mask,
                            arc_mask, rrect_mask, star_pts, bust_mask, CREAM, CREAM_D, KHAKI, KHAKI_D, SLATE, SLATE_D, RUST, RUST_D,
                            VIOLET, GOLD, GOLD_D, BONE, STONE, STONE_D, BW, BH)

def gear_pts(cx, cy, r_out, r_in, teeth, rot=0.0):
    pts = []
    for i in range(teeth * 4):
        a = rot + i * math.pi * 2 / (teeth * 4)
        r = r_out if (i % 4) in (1, 2) else r_in
        pts.append((cx + r * math.cos(a), cy + r * math.sin(a)))
    return pts

def gear(cx, cy, r, teeth=10, hole=0.38):
    g = poly_mask(gear_pts(cx, cy, r, r * 0.8, teeth))
    return subtract(g, ellipse_mask(cx, cy, r * hole))

def rune_mark(I, cx, cy, s, col):
    I.layer(union(line_mask([(cx, cy - s), (cx, cy + s)], 4), line_mask([(cx, cy - s * 0.3), (cx + s * 0.6, cy - s)], 4),
                  line_mask([(cx, cy + s * 0.1), (cx - s * 0.6, cy - s * 0.5)], 4)), col, shade=False, outline=False)

def hammer(I, cx, cy, ang, L=70, head=SLATE, handle=RUST):
    a = math.radians(ang); dx, dy = math.cos(a), math.sin(a); px, py = -dy, dx
    top = (cx + dx * L * 0.5, cy + dy * L * 0.5); bot = (cx - dx * L * 0.5, cy - dy * L * 0.5)
    I.layer(line_mask([bot, top], 9), handle)
    hw, hl = 13, 26
    I.layer(poly_mask([(top[0] + px * hl + dx * hw, top[1] + py * hl + dy * hw), (top[0] - px * hl + dx * hw, top[1] - py * hl + dy * hw),
                       (top[0] - px * hl - dx * hw, top[1] - py * hl - dy * hw), (top[0] + px * hl - dx * hw, top[1] + py * hl - dy * hw)]), head)
    return top

def icon(name):
    I = Icon()
    if name == 'ArcaneWorkshop':     # 奥术工坊：铁砧 + 竖着的法杖
        I.layer(line_mask([(84, 108), (84, 22)], 8), RUST)
        I.layer(poly_mask([(84, 6), (96, 22), (84, 36), (72, 22)]), VIOLET)
        I.layer(poly_mask([(14, 62), (96, 62), (90, 74), (66, 80), (70, 98), (88, 106), (88, 116), (22, 116), (22, 106), (40, 98), (44, 80), (24, 74)]), STONE)
        rune_mark(I, 56, 70, 6, GOLD)
    elif name == 'QuickRepair':      # 快速维修：扳手 + 齿轮
        I.layer(gear(76, 78, 34), SLATE)
        w = union(line_mask([(24, 104), (70, 52)], 13), ellipse_mask(80, 40, 20))
        w = subtract(w, poly_mask([(80, 18), (94, 32), (82, 44), (70, 32)]))
        I.layer(w, CREAM)
    elif name == 'RuneInscription':  # 符文铭刻：刻刀 + 发光符文板
        I.layer(rrect_mask(16, 22, 100, 106, 10), STONE)
        rune_mark(I, 46, 64, 22, GOLD)
        rune_mark(I, 74, 54, 14, GOLD)
        I.layer(line_mask([(70, 108), (116, 62)], 8), RUST)
        I.layer(poly_mask([(112, 58), (124, 46), (120, 66)]), CREAM)
    elif name == 'Overcharge':       # 过载射击：枪口 + 闪电
        I.layer(rrect_mask(8, 50, 74, 74, 6), SLATE)
        I.layer(poly_mask([(20, 70), (40, 70), (34, 104), (16, 104)]), RUST)
        I.layer(poly_mask([(88, 18), (68, 64), (86, 64), (74, 112), (118, 50), (98, 50), (110, 18)]), GOLD)
    elif name == 'ArcaneCharge':     # 魔力充能：人像 + 上升的蓝色能量
        I.layer(bust_mask(56, 50), KHAKI)
        for i, x in enumerate((92, 108)):
            I.layer(poly_mask(star_pts(x, 40 + i * 30, 14, 5, 4)), (140, 186, 220), glow=(120, 170, 220))
        I.layer(poly_mask([(96, 120), (96, 92), (86, 92), (104, 70), (122, 92), (112, 92), (112, 120)]), (140, 186, 220))
    elif name == 'MasterCraft':      # 大师工艺：星徽 + 锤子
        I.layer(poly_mask(star_pts(64, 64, 58, 24, 5)), GOLD)
        hammer(I, 64, 70, -60, L=64)
    elif name == 'MountainForge':    # 山岳锻造：山峰 + 火焰
        I.layer(poly_mask([(4, 118), (46, 34), (64, 60), (84, 26), (124, 118)]), STONE)
        I.layer(poly_mask([(84, 26), (96, 50), (84, 46), (74, 52)]), BONE, shade=False)
        I.layer(poly_mask([(64, 120), (46, 96), (54, 78), (64, 90), (70, 70), (84, 96)]), RUST)
        I.layer(poly_mask([(64, 120), (56, 104), (64, 96), (72, 106)]), GOLD, shade=False)
    elif name == 'RuneTurret':       # 符文炮台：三脚架 + 炮管 + 符文
        I.layer(union(line_mask([(64, 72), (28, 118)], 8), line_mask([(64, 72), (100, 118)], 8), line_mask([(64, 72), (64, 118)], 8)), RUST_D)
        I.layer(rrect_mask(30, 40, 110, 64, 8), SLATE)
        I.layer(rrect_mask(98, 44, 122, 60, 4), SLATE_D)
        I.layer(ellipse_mask(48, 52, 18), STONE)
        rune_mark(I, 48, 52, 9, (140, 186, 220))
    elif name == 'ArcaneResonance':  # 魔力共鸣：齿轮中的宝石 + 同心波
        for r in (58, 46):
            I.layer(arc_mask(64, 64, r, 0, 360, 5), (140, 186, 220), shade=False)
        I.layer(gear(64, 64, 36, 12, hole=0.0), GOLD)
        I.layer(poly_mask([(64, 40), (84, 64), (64, 88), (44, 64)]), VIOLET, glow=(150, 130, 200))
    return I.done()

NAMES = ['ArcaneWorkshop', 'QuickRepair', 'RuneInscription', 'Overcharge', 'ArcaneCharge', 'MasterCraft', 'MountainForge', 'RuneTurret', 'ArcaneResonance']

def background():
    base, line, spark = (30, 24, 14), (128, 104, 56), (230, 190, 110)
    rnd = random.Random(4)
    bg = Image.new('RGBA', (BW, BH), base + (255,)); d = ImageDraw.Draw(bg)
    for _ in range(3000):
        x, y = rnd.randrange(BW), rnd.randrange(BH); v = rnd.randint(-6, 6)
        d.point((x, y), fill=tuple(max(0, min(255, c + v)) for c in base) + (255,))
    d.rounded_rectangle([54, 54, BW - 54, BH - 54], radius=26, fill=(10, 8, 6, 255))
    for inset, w in ((22, 4), (40, 2)):
        d.rounded_rectangle([inset, inset, BW - inset, BH - inset], radius=30, outline=line + (255,), width=w)
    for i in range(25):
        t = i / 24
        for (x, y) in ((31, 90 + t * (BH - 180)), (BW - 31, 90 + t * (BH - 180))):
            d.ellipse([x - 4, y - 4, x + 4, y + 4], fill=line + (255,))
    for i in range(15):
        t = i / 14
        for (x, y) in ((90 + t * (BW - 180), 31), (90 + t * (BW - 180), BH - 31)):
            d.ellipse([x - 4, y - 4, x + 4, y + 4], fill=line + (255,))
    for (cx, cy) in ((BW / 2, 31), (BW / 2, BH - 31), (31, BH / 2), (BW - 31, BH / 2)):
        d.ellipse([cx - 26, cy - 11, cx + 26, cy + 11], outline=line + (255,), width=3, fill=base + (255,))
        d.ellipse([cx - 7, cy - 7, cx + 7, cy + 7], outline=line + (255,), width=3)
    for _ in range(140):
        x, y = rnd.randrange(80, BW - 80), rnd.randrange(80, BH - 80); r = rnd.choice((1, 1, 1, 2, 2, 3))
        d.ellipse([x - r, y - r, x + r, y + r], fill=spark + (rnd.randint(90, 200),))
    def sil():
        return Image.new('L', (BW, BH), 0)
    parts = []
    # 大齿轮（中空）+ 小齿轮
    g = sil(); gd = ImageDraw.Draw(g)
    gd.polygon(gear_pts(475, 560, 300, 245, 14), fill=255); gd.ellipse([475 - 120, 560 - 120, 475 + 120, 560 + 120], fill=0)
    gd.polygon(gear_pts(250, 1000, 130, 104, 10, 0.2), fill=255); gd.ellipse([250 - 48, 1000 - 48, 250 + 48, 1000 + 48], fill=0)
    gd.polygon(gear_pts(720, 1050, 100, 80, 9, 0.1), fill=255); gd.ellipse([720 - 36, 1050 - 36, 720 + 36, 1050 + 36], fill=0)
    parts.append(g)
    # 符文锤：斜放压在齿轮前
    h = sil(); hd = ImageDraw.Draw(h)
    a = math.radians(-60); dx, dy = math.cos(a), math.sin(a); px, py = -dy, dx
    cx, cy, L = 475, 860, 980
    top = (cx + dx * L * 0.42, cy + dy * L * 0.42); bot = (cx - dx * L * 0.5, cy - dy * L * 0.5)
    hd.line([bot, top], fill=255, width=56)
    hw, hl = 95, 190
    hd.polygon([(top[0] + px * hl + dx * hw, top[1] + py * hl + dy * hw), (top[0] - px * hl + dx * hw, top[1] - py * hl + dy * hw),
                (top[0] - px * hl - dx * hw, top[1] - py * hl - dy * hw), (top[0] + px * hl - dx * hw, top[1] + py * hl - dy * hw)], fill=255)
    parts.append(h)
    allm = parts[0]
    for m in parts[1:]: allm = ImageChops.lighter(allm, m)
    glow = allm.filter(ImageFilter.MaxFilter(31)).filter(ImageFilter.GaussianBlur(22))
    bg.paste(Image.new('RGBA', (BW, BH), line + (255,)), (0, 0), glow.point(lambda v: int(v * 0.55)))
    for m in parts:
        bg.paste(Image.new('RGBA', (BW, BH), line + (255,)), (0, 0), m.filter(ImageFilter.MaxFilter(9)))
        bg.paste(Image.new('RGBA', (BW, BH), (6, 4, 4, 255)), (0, 0), m)
    # 锤头上的符文（镂空发光）
    rd = ImageDraw.Draw(bg)
    for off in (-70, 70):
        x, y = top[0] + px * off, top[1] + py * off
        rd.line([(x, y - 50), (x, y + 50)], fill=spark + (255,), width=12)
        rd.line([(x, y - 15), (x + 34, y - 50)], fill=spark + (255,), width=12)
        rd.line([(x, y + 5), (x - 34, y - 30)], fill=spark + (255,), width=12)
    return bg

if __name__ == '__main__':
    os.makedirs(OUT, exist_ok=True)
    for n in NAMES:
        icon(n).save(os.path.join(OUT, n + '.png'))
    background().save(os.path.join(OUT, 'Background.png'))
    print('artificer art written to', OUT)
