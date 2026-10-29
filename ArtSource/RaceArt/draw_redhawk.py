# 1.64.0 红鹰商业联盟派系图标：盾徽上一只展翅的鹰，嘴里叼着一枚金币。
# 沿用 draw_factions.py 的画法（粗黑轮廓、平涂、单侧暗面）：游戏里派系图标按派系颜色染色，所以贴图是白/灰剪影，
# 金币用最亮的白色和一圈深色描边与鹰的灰色区分。预览另外用红/金/羊皮纸的本色画一遍。
# 用法：python -I ArtSource/RaceArt/draw_redhawk.py <模组根目录>
import math, os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import draw_race_art as base
from draw_race_art import Pic, poly, ell, tent, mir, INK
from draw_factions import shield_pts, OW

ROOT = sys.argv[1] if len(sys.argv) > 1 else os.getcwd()
base.TEX = os.path.join(ROOT, "Textures")

# 染色用的灰阶（同 draw_factions.py）
GREY = dict(shield=(250, 250, 250), shield_l=(214, 214, 214), hawk=(168, 168, 168), hawk_d=(120, 120, 120),
            coin=(250, 250, 250), coin_d=(120, 120, 120), eye=INK)
# 预览本色：暗红鹰、金币、羊皮纸盾面（低饱和）
COLOR = dict(shield=(226, 212, 182), shield_l=(204, 188, 156), hawk=(158, 46, 36), hawk_d=(118, 32, 28),
             coin=(222, 182, 82), coin_d=(170, 128, 50), eye=(240, 220, 150))

LEFT_WING = [(45, 42), (34, 28), (22, 16), (12, 8), (8, 14), (11, 20), (5, 25), (11, 30), (5, 35), (12, 40), (7, 46), (16, 49), (13, 55), (24, 56), (23, 62), (34, 60), (43, 58)]


def redhawk(c):
    p = Pic()
    # 盾徽
    p.add(poly(shield_pts(14, 86, 6, 96)), c["shield"], OW)
    p.add(poly([(x, y) for x, y in shield_pts(14, 86, 6, 96) if x >= 50] + [(50, 96)]), c["shield_l"], 0)
    # 展开的双翼（左边亮、右边暗面）
    p.add([poly(LEFT_WING), poly(mir(LEFT_WING))], c["hawk"], 2.8)
    p.add(poly(mir(LEFT_WING)), c["hawk_d"], 0)
    # 翼上的羽缝
    for s in (1, -1):
        for (x0, y0), (x1, y1) in (((40, 46), (14, 22)), ((40, 51), (12, 37)), ((40, 56), (19, 50))):
            if s < 0:
                x0, x1 = 100 - x0, 100 - x1
            p.line([(x0, y0), (x1, y1)], 1.0, INK)
    # 尾羽
    tail = [(43, 70), (57, 70), (63, 88), (56, 85), (50, 91), (44, 85), (37, 88)]
    p.add(poly(tail), c["hawk"], 2.6)
    p.add(poly([(50, 70), (57, 70), (63, 88), (56, 85), (50, 91)]), c["hawk_d"], 0)
    # 爪
    for s in (1, -1):
        leg = [(50 - 5 * s, 66), (50 - 8 * s, 75)]
        p.add(tent(leg, 3.4, 2.4), c["hawk_d"], 1.6)
    # 身体
    p.add(ell((39, 38, 61, 74)), c["hawk"], 2.8)
    p.add(poly([(50, 38), (58, 42), (61, 56), (58, 68), (50, 74)]), c["hawk_d"], 0)
    for y in (50, 56, 62):
        p.line([(44, y), (50, y + 2.5), (56, y)], 1.0, INK)
    # 头（朝左）与钩喙
    p.add([ell((43, 21, 57, 37)), poly([(45, 25), (35, 25), (29, 29), (28, 35), (31, 33), (35, 31), (44, 33)])], c["hawk"], 2.8)
    p.add(poly([(50, 21), (55, 23), (57, 29), (55, 35), (50, 37)]), c["hawk_d"], 0)
    p.add(ell((44.5, 25.5, 48, 29)), c["eye"], 1.4)
    p.line([(42.5, 24.5), (49.5, 23)], 1.6, INK)
    # 叼着的金币（压在喙尖上）
    p.add(ell((14, 31, 30, 47)), c["coin"], 2.6)
    
    p.line([(18, 35), (26, 35), (26, 43), (18, 43), (18, 35)], 1.0, c["coin_d"])
    p.add(poly([(22, 36.5), (24.5, 39), (22, 41.5), (19.5, 39)]), c["coin_d"], 0)
    return p


def preview(root):
    from PIL import Image, ImageChops, ImageDraw, ImageFont
    FONT = "C:/Windows/Fonts/msyh.ttc"; BOLD = "C:/Windows/Fonts/msyhbd.ttc"
    BG = (34, 36, 40); CARD = (48, 51, 56); TEXT = (226, 226, 220)
    icon = Image.open(os.path.join(root, "Textures", "Factions", "RedHawkLeague.png")).convert("RGBA")
    W, H = 1100, 520
    img = Image.new("RGBA", (W, H), BG + (255,))
    d = ImageDraw.Draw(img)
    d.text((24, 16), "DND边缘大陆 1.64.0 · 红鹰商业联盟", font=ImageFont.truetype(BOLD, 34), fill=TEXT)
    d.rounded_rectangle((20, 76, W - 20, H - 20), 14, fill=CARD + (255,))
    x = 50
    for color in ((0.62, 0.12, 0.10), (0.80, 0.22, 0.14)):
        t = ImageChops.multiply(icon, Image.new("RGBA", icon.size, tuple(int(v * 255) for v in color) + (255,)))
        img.alpha_composite(t.resize((200, 200), Image.LANCZOS), (x, 110)); x += 220
    img.alpha_composite(icon.resize((200, 200), Image.LANCZOS), (x, 110)); x += 220
    concept = redhawk(COLOR).img.resize((200, 200), Image.LANCZOS)
    img.alpha_composite(concept, (x, 110))
    f = ImageFont.truetype(FONT, 20)
    for i, s in enumerate(["游戏内（暗红）", "游戏内（亮红）", "贴图原样（灰阶）", "本色示意"]):
        d.text((50 + i * 220, 320), s, font=f, fill=TEXT)
    lines = ["商队：奴隶（10～14 名、各不同种族）/ 魔法材料 / 契约卷轴（每张不同宗主）",
             "据点：红鹰商站 · 飞空商船：通讯台呼叫，敌对时不来",
             "中立 +10 · 中世纪 · 固定意识形态「金与契」"]
    for i, s in enumerate(lines):
        d.text((50, 370 + i * 34), "· " + s, font=f, fill=TEXT)
    for size, xx in ((64, 950), (32, 1030)):
        img.alpha_composite(ImageChops.multiply(icon, Image.new("RGBA", icon.size, (158, 31, 26, 255))).resize((size, size), Image.LANCZOS), (xx, 140))
    out = os.path.join(root, "ArtSource", "Preview", "RedHawk-1.64.0.png")
    os.makedirs(os.path.dirname(out), exist_ok=True)
    img.convert("RGB").save(out)
    return out


if __name__ == "__main__":
    print(redhawk(GREY).save("Factions/RedHawkLeague", 128))
    print(preview(ROOT))
