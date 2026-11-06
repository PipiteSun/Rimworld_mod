# 1.122.0 系统宿主：「系统面板」按钮图标、改命符物品贴图（128×128）。
# 画法沿用 draw_class_art.py：粗黑描边、平涂 + 右下暗部、低饱和。只写 Textures/LP/SystemHost。
# 用法：python -I draw_systemhost.py <模组根目录>
import sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from draw_class_art import (Icon, poly_mask, ellipse_mask, line_mask, arc_mask, rrect_mask, union, subtract, star_pts,
                            CREAM, CREAM_D, SLATE, SLATE_D, RUST, RUST_D, BONE, GOLD)

ROOT = sys.argv[1] if len(sys.argv) > 1 else '.'
OUT = os.path.join(ROOT, 'Textures', 'LP', 'SystemHost')
os.makedirs(OUT, exist_ok=True)
HUD = (84, 112, 138); HUD_D = (58, 80, 102)          # 面板灰蓝
CYAN = (132, 190, 200)                               # 低饱和青光
PAPER = (206, 184, 128)                              # 符纸黄
CINNABAR = (170, 62, 48)                             # 朱砂

def panel():
    I = Icon()
    I.layer(rrect_mask(10, 16, 118, 112, 12), HUD)                         # 面板
    I.layer(rrect_mask(20, 26, 108, 40, 4), HUD_D, shade=False)            # 标题栏
    for x in (26, 36, 46): I.layer(ellipse_mask(x, 33, 3), CYAN, shade=False, outline=False)
    I.layer(poly_mask([(64, 46), (92, 74), (64, 102), (36, 74)]), CYAN, glow=CYAN)   # 任务菱形
    I.layer(rrect_mask(60, 56, 68, 82, 3), (30, 40, 52), shade=False, outline=False) # 感叹号
    I.layer(ellipse_mask(64, 91, 4), (30, 40, 52), shade=False, outline=False)
    for y in (52, 64, 76, 88):                                             # 两侧刻度
        I.layer(line_mask([(18, y), (28, y)], 3), SLATE, shade=False, outline=False)
        I.layer(line_mask([(100, y), (110, y)], 3), SLATE, shade=False, outline=False)
    return I.done(128)

def charm():
    I = Icon()
    I.layer(poly_mask([(40, 8), (88, 8), (92, 120), (36, 120)]), PAPER)    # 符纸
    I.layer(line_mask([(46, 18), (82, 18)], 4), CINNABAR, shade=False, outline=False)
    I.layer(arc_mask(64, 52, 18, 0, 360, 5), CINNABAR, shade=False, outline=False, glow=CYAN)  # 符印
    I.layer(poly_mask(star_pts(64, 52, 9, 4, 4)), CINNABAR, shade=False, outline=False)
    I.layer(line_mask([(64, 72), (60, 84), (70, 92), (58, 104), (66, 112)], 5), CINNABAR, shade=False, outline=False)  # 笔画
    I.layer(line_mask([(50, 82), (78, 82)], 4), CINNABAR, shade=False, outline=False)
    return I.done(128)

panel().save(os.path.join(OUT, 'SystemPanel.png'))
charm().save(os.path.join(OUT, 'FateCharm.png'))
print('wrote', OUT)
