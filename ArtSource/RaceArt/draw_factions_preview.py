# 1.19.0 派系预览拼图：派系图标按派系颜色染色 + 名称、关系、成员、商队。
# 用法：python -I ArtSource/RaceArt/draw_factions_preview.py <模组根目录>
import os, sys
from PIL import Image, ImageChops, ImageDraw, ImageFont

ROOT = sys.argv[1] if len(sys.argv) > 1 else os.getcwd()
FONT = "C:/Windows/Fonts/msyh.ttc"
BOLD = "C:/Windows/Fonts/msyhbd.ttc"
BG = (34, 36, 40); CARD = (48, 51, 56); TEXT = (226, 226, 220); DIM = (160, 164, 168)

FACTIONS = [
    ("FaerunColony", (0.20, 0.42, 0.92), "费伦拓荒同盟", "中世纪 · 友善（+40）",
     ["人类40% 矮人20% 精灵20% 半兽人15%", "圣武士、牧师约5%（人类）", "商队：矮人铁匠 / 精灵草药商 / 教会牧师 / 同盟商队", "好感≥60：竖琴手袭击预警", "讨厌巫妖、夺心魔、死灵造物；喜欢半兽人"]),
    ("FaerunHostile", (0.72, 0.06, 0.06), "塞伊远征军", "中世纪 · 敌对（-80，非永久）",
     ["红袍法师（长袍染红，各带2～3具骷髅）", "符印巨魔 · 兽人战士 · 牛头人冲锋兵", "吉斯洋基雇佣兵约3%（1/4带银剑）", "法师倒下：骷髅崩解、巨魔狂暴、兽人逃散", "招揽巫妖效忠 / 向无巫妖的殖民地索要贡银"]),
    ("EmberClan", (1.00, 0.46, 0.04), "余烬氏族", "中世纪 · 冷淡（-20）",
     ["火巨人领队每次1～2个（熔炉重锤、熔铁重甲）", "人类奴兵（板甲）、少量半兽人奴兵", "商队：火巨人铁匠，高价收购巨魔血", "火巨人倒下：奴兵逃命", "喜欢火巨人 +15，讨厌巨魔 -15；与塞伊世仇"]),
    ("IllithidColony", (0.52, 0.18, 0.74), "幽暗殖巢", "超凡科技 · 永久敌对",
     ["夺心魔领队1～3个", "被支配的人类、矮人、精灵、半兽人奴仆", "从地下钻出：在殖民地附近空地冒土登场", "夺心魔倒下：奴仆清醒倒地、脱离殖巢、可俘虏"]),
    ("BlackNetwork", (0.26, 0.30, 0.30), "黑网", "隐藏派系 · 只出商队",
     ["约每季一次黑网商队", "卖奴隶、毒品、走私货（偏贵）", "收器官、巨魔血、夺心魔蝌蚪、脑浆罐（压价）"]),
    ("Zealots", (0.96, 0.90, 0.64), "猎巫狂热分子", "隐藏 · 临时敌人",
     ["殖民地有巫妖或夺心魔时偶尔来袭", "圣武士与牧师，优先攻击巫妖和命匣", "杀死不影响同盟好感；放走俘虏同盟 +5"]),
]


def tinted(name, color, size):
    im = Image.open(os.path.join(ROOT, "Textures", "Factions", name + ".png")).convert("RGBA")
    t = ImageChops.multiply(im, Image.new("RGBA", im.size, tuple(int(c * 255) for c in color) + (255,)))
    return t.resize((size, size), Image.LANCZOS)


def main():
    cols, cw, ch, pad = 2, 700, 250, 20
    rows = (len(FACTIONS) + cols - 1) // cols
    W, H = cols * cw + (cols + 1) * pad, rows * ch + (rows + 1) * pad + 70
    img = Image.new("RGBA", (W, H), BG + (255,))
    d = ImageDraw.Draw(img)
    title = ImageFont.truetype(BOLD, 34); name_f = ImageFont.truetype(BOLD, 28); sub_f = ImageFont.truetype(FONT, 18); body_f = ImageFont.truetype(FONT, 19)
    d.text((pad, 18), "DND边缘大陆 1.19.0 · 新派系", font=title, fill=TEXT)
    for i, (key, color, name, sub, lines) in enumerate(FACTIONS):
        x = pad + (i % cols) * (cw + pad); y = 70 + pad + (i // cols) * (ch + pad)
        d.rounded_rectangle((x, y, x + cw, y + ch), 14, fill=CARD + (255,))
        bar = tuple(int(c * 255) for c in color)
        d.rounded_rectangle((x, y, x + 10, y + ch), 5, fill=bar + (255,))
        img.alpha_composite(tinted(key, color, 150), (x + 26, y + 50))
        tx = x + 200
        d.text((tx, y + 18), name, font=name_f, fill=TEXT)
        d.text((tx, y + 58), sub, font=sub_f, fill=tuple((c + 255) // 2 for c in bar))
        for j, line in enumerate(lines):
            d.text((tx, y + 92 + j * 29), "· " + line, font=body_f, fill=TEXT)
    out = os.path.join(ROOT, "ArtSource", "Preview", "Factions-1.19.0.png")
    os.makedirs(os.path.dirname(out), exist_ok=True)
    img.convert("RGB").save(out)
    print(out)


if __name__ == "__main__":
    main()
