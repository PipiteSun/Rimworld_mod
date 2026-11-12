# 1.141.0 生产专长图标 8 个（128×128），沿用 draw_feats3_167.py / draw_class_art.py 的画风：粗黑描边、平涂 + 右下暗部、低饱和。
# 只写自己的 8 个文件。用法：E:\Python\python.exe draw_feats5_1141.py <Textures 根目录>
import math, sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from draw_class_art import (Icon, poly_mask, ellipse_mask, line_mask, arc_mask, rrect_mask, union, subtract, star_pts,
                            CREAM, CREAM_D, KHAKI, KHAKI_D, SLATE, SLATE_D, RUST, RUST_D, VIOLET, VIOLET_D, GOLD, GOLD_D, BONE, BONE_D, STONE, STONE_D)

ROOT = sys.argv[1] if len(sys.argv) > 1 else 'out'
SAGE = (120, 146, 112); SAGE_D = (84, 104, 78)
FEY = (150, 176, 120)
BLOOD = (150, 52, 46)
DARK = (58, 52, 60)
COPPER = (176, 118, 70)
SOIL = (96, 74, 54)

def hammer(cx, cy, ang, L=60, head_col=SLATE, handle_col=RUST_D):
    """锤子：ang 为锤头方向（屏幕坐标，-90 = 向上）。"""
    a = math.radians(ang); dx, dy = math.cos(a), math.sin(a); px, py = -dy, dx
    head = (cx + dx * L * 0.42, cy + dy * L * 0.42)
    handle = line_mask([(cx - dx * L * 0.5, cy - dy * L * 0.5), head], 7)
    hw, hh = 16, 9
    headm = poly_mask([(head[0] + px * hw + dx * hh, head[1] + py * hw + dy * hh), (head[0] + px * hw - dx * hh, head[1] + py * hw - dy * hh),
                       (head[0] - px * hw - dx * hh, head[1] - py * hw - dy * hh), (head[0] - px * hw + dx * hh, head[1] - py * hw + dy * hh)])
    return [(handle, handle_col), (headm, head_col)]

def icon(name):
    I = Icon()
    if name == 'PracticedHand':       # 学以致用：摊开的书 + 工具（小锤）搁在书上 + 星
        I.layer(poly_mask([(16, 44), (62, 52), (62, 110), (16, 102)]), BONE)
        I.layer(poly_mask([(112, 44), (66, 52), (66, 110), (112, 102)]), CREAM)
        for k in range(3):
            y = 62 + k * 12
            I.layer(line_mask([(24, y + 1), (54, y + 4)], 2), BONE_D, outline=False, shade=False)
            I.layer(line_mask([(74, y + 4), (104, y + 1)], 2), CREAM_D, outline=False, shade=False)
        for m, c in hammer(78, 60, -35, 56):
            I.layer(m, c)
        I.layer(poly_mask(star_pts(30, 26, 11, 5, 4)), GOLD, outline=False)
    elif name == 'MasterArtisan':     # 名匠：铁砧 + 顶上一颗金星
        I.layer(poly_mask([(14, 60), (114, 60), (104, 74), (84, 78), (84, 100), (44, 100), (44, 78), (28, 74)]), SLATE)
        I.layer(rrect_mask(34, 100, 94, 112, 4), STONE)
        I.layer(poly_mask([(14, 60), (114, 60), (108, 52), (20, 52)]), SLATE_D, outline=False)
        I.layer(poly_mask(star_pts(64, 30, 18, 8, 5)), GOLD)
    elif name == 'RuneSmith':         # 符文锻师：锤子敲在石板上，石板上有发光符文
        I.layer(rrect_mask(20, 66, 108, 112, 8), STONE)
        rune = union(line_mask([(50, 78), (64, 100)], 4), line_mask([(64, 100), (78, 78)], 4), line_mask([(50, 78), (78, 78)], 4), line_mask([(64, 78), (64, 100)], 4))
        I.layer(rune, VIOLET, outline=False, shade=False, glow=VIOLET)
        for m, c in hammer(64, 44, -50, 64, SLATE, RUST_D):
            I.layer(m, c)
        for k, (x, y) in enumerate([(100, 30), (108, 46), (92, 20)]):
            I.layer(poly_mask(star_pts(x, y, 5 + k, 2 + k // 2, 4)), GOLD, outline=False)
    elif name == 'MasterAlchemist':   # 炼金大师：圆底烧瓶 + 紫色药液 + 气泡
        flask = union(ellipse_mask(64, 82, 34, 30), rrect_mask(54, 18, 74, 70, 4))
        I.layer(flask, BONE)
        liquid = subtract(ellipse_mask(64, 82, 34, 30), rrect_mask(0, 0, 128, 76, 0))
        I.layer(liquid, VIOLET, outline=False)
        I.layer(rrect_mask(50, 14, 78, 24, 4), RUST)
        for (x, y, r) in [(54, 92, 4), (70, 98, 3), (64, 84, 2)]:
            I.layer(ellipse_mask(x, y, r), BONE, outline=False, shade=False)
        I.layer(poly_mask(star_pts(100, 28, 9, 4, 4)), GOLD, outline=False)
    elif name == 'MasterBuilder':     # 筑城大师：砖墙 + 泥铲
        for row in range(3):
            y = 62 + row * 18
            off = 0 if row % 2 == 0 else 14
            for col in range(4):
                x = 14 + col * 28 + off
                if x > 108: continue
                I.layer(rrect_mask(x, y, min(x + 24, 114), y + 14, 2), RUST if (row + col) % 2 else RUST_D, shade=False)
        I.layer(line_mask([(84, 20), (104, 44)], 6), RUST_D)
        I.layer(poly_mask([(60, 50), (102, 40), (104, 60), (72, 70)]), SLATE)
    elif name == 'GreenThumb':        # 丰收之手：土里长出的嫩芽，两片叶 + 一颗金色麦穗星
        I.layer(poly_mask([(16, 112), (112, 112), (104, 92), (24, 92)]), SOIL)
        I.layer(line_mask([(64, 96), (64, 48)], 6), SAGE_D)
        I.layer(poly_mask([(64, 72), (30, 52), (26, 72), (46, 86)]), FEY)
        I.layer(poly_mask([(64, 60), (98, 40), (102, 60), (82, 74)]), SAGE)
        I.layer(ellipse_mask(64, 40, 10, 13), FEY)
        I.layer(poly_mask(star_pts(100, 24, 9, 4, 4)), GOLD, outline=False)
    elif name == 'Surgeon':           # 外科圣手：手术刀 + 红十字
        I.layer(union(rrect_mask(74, 30, 110, 46, 3), rrect_mask(84, 20, 100, 56, 3)), BLOOD, shade=False)
        I.layer(line_mask([(22, 106), (62, 66)], 7), DARK)
        I.layer(poly_mask([(56, 72), (86, 38), (96, 42), (70, 82)]), BONE)
        I.layer(line_mask([(28, 100), (36, 92)], 2), BONE_D, outline=False, shade=False)
    elif name == 'Fabricate':         # 造物之手：齿轮 + 金色光芒
        teeth = [ellipse_mask(64 + 36 * math.cos(math.radians(a)), 64 + 36 * math.sin(math.radians(a)), 8) for a in range(0, 360, 45)]
        I.layer(union(ellipse_mask(64, 64, 32), *teeth), GOLD_D)
        I.layer(ellipse_mask(64, 64, 20), GOLD)
        I.layer(ellipse_mask(64, 64, 8), DARK, shade=False)
        I.layer(poly_mask(star_pts(64, 64, 18, 6, 4)), CREAM, outline=False, shade=False, glow=GOLD)
        I.layer(poly_mask(star_pts(104, 24, 10, 4, 4)), GOLD, outline=False)
        I.layer(poly_mask(star_pts(24, 104, 7, 3, 4)), GOLD, outline=False)
    return I.done(128)

FEATS = ['PracticedHand', 'MasterArtisan', 'RuneSmith', 'MasterAlchemist', 'MasterBuilder', 'GreenThumb', 'Surgeon', 'Fabricate']

if __name__ == '__main__':
    fd = os.path.join(ROOT, 'LP', 'Feats'); os.makedirs(fd, exist_ok=True)
    for n in FEATS:
        icon(n).save(os.path.join(fd, n + '.png'))
    print('feat icons written:', len(FEATS))
