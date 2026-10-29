# 1.38.0 流浆人贴图：沿用 draw_race_art.py / draw_races_124.py 的画法（粗黑轮廓、平涂、单侧暗面、低饱和）。
# 用法：python -I ArtSource/RaceArt/draw_oozefolk.py <模组根目录> [预览图输出路径]
# 头部附件对位同 1.24.0：原版头部画框 128px 下 x 41-87、y 39-89（100 单位画布约 x 32-68、y 30-70），头顶约 y 31。
# 流光受肤色着色（useSkinShader），所以画成近白灰：最亮的 PALE_L 就是“肤色本身”，暗面略深。
import math, os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import draw_race_art as base
from draw_race_art import Pic, poly, ell, tent, arc, ellpts, mir, INK, WOOD, WOOD_D, STEEL, STEEL_D, BONE, STONE, STONE_D
from draw_minotaur_gith import HEAD_OW
from draw_races_124 import PALE, PALE_D, PALE_L, flame, snowflake, eyes, ICE, ICE_D, ICE_L, FLAME, FLAME_D, FLAME_L

ROOT = sys.argv[1] if len(sys.argv) > 1 else os.getcwd()
base.TEX = os.path.join(ROOT, "Textures")
OW = base.OW

# ---------------- 调色（胆绿为主，另三种肤色只在预览里用） ----------------
OOZE = (118, 150, 104); OOZE_D = (90, 118, 80); OOZE_L = (164, 194, 148); GLOSS = (222, 236, 212)
ACID = (186, 208, 92); ACID_D = (146, 168, 64)
SKINS = {"胆绿": (118, 150, 104), "渊蓝": (96, 132, 156), "琥珀": (176, 140, 82), "雾紫": (138, 116, 152)}
CLOTH = (226, 220, 204); CLOTH_D = (190, 182, 162)


def blob_pts(cx, cy, rx, ry, wobble=0.0, seed=0, steps=40, flat=0.0):
    """略带起伏的胶团轮廓；flat>0 时底部压平成一滩。"""
    out = []
    for i in range(steps):
        t = 2 * math.pi * i / steps
        w = 1 + wobble * math.sin(3 * t + seed) * .5 + wobble * math.sin(5 * t + seed * 2) * .3
        x, y = cx + rx * math.cos(t) * w, cy + ry * math.sin(t) * w
        if flat and y > cy + ry * (1 - flat):
            y = cy + ry * (1 - flat)
        out.append((x, y))
    return out


def blob(p, cx, cy, rx, ry, color=OOZE, dark=OOZE_D, light=OOZE_L, gloss=GLOSS, ow=OW, wobble=.06, seed=0, flat=0.0):
    pts = blob_pts(cx, cy, rx, ry, wobble, seed, flat=flat)
    p.add(poly(pts), color, ow)
    # 右下暗面
    p.add(poly([(x, y) for (x, y) in blob_pts(cx + rx * .18, cy + ry * .14, rx * .86, ry * .86, wobble, seed, flat=flat)
                if x > cx + rx * .1 or y > cy + ry * .45] or pts), dark, 0)
    p.add(poly(blob_pts(cx - rx * .08, cy - ry * .06, rx * .8, ry * .78, wobble * .6, seed, flat=flat)), color, 0)
    # 左上高光
    p.add(poly(ellpts(cx - rx * .38, cy - ry * .42, rx * .22, ry * .14, -35)), light, 0)
    p.add(ell((cx - rx * .62, cy - ry * .2, cx - rx * .48, cy - ry * .06)), gloss, 0)


def drip(p, x, y0, y1, w, color=OOZE, light=OOZE_L, ow=OW):
    """从 (x,y0) 垂下的一道胶滴，末端是一颗水滴。"""
    p.add(tent([(x, y0), (x + .3, (y0 + y1) / 2), (x, y1 - w * .6)], w, w * .55), color, ow)
    p.add(poly([(x, y1 - w * 1.6)] + arc(x, y1, w * .75, -30, 210, 16)), color, ow)
    p.add(ell((x - w * .45, y1 - w * .35, x - w * .1, y1 + w * .05)), light, 0)


def humanoid_blob(p, cx, top, h, color=OOZE, dark=OOZE_D, light=OOZE_L, ow=OW):
    """流浆人的剪影：圆头 + 往下化开成一滩的身体。"""
    r = h * .16
    hy = top + r
    body = [(cx - h * .2, hy + r * .9), (cx - h * .26, top + h * .55), (cx - h * .34, top + h * .82), (cx - h * .44, top + h * .96),
            (cx - h * .2, top + h), (cx + h * .2, top + h), (cx + h * .44, top + h * .96), (cx + h * .34, top + h * .82),
            (cx + h * .26, top + h * .55), (cx + h * .2, hy + r * .9)]
    p.add(poly(body), color, ow)
    p.add(poly([(cx + h * .08, hy + r * 1.1), (cx + h * .26, top + h * .55), (cx + h * .34, top + h * .82), (cx + h * .44, top + h * .96),
                (cx + h * .2, top + h), (cx + h * .1, top + h * .9), (cx + h * .16, top + h * .55)]), dark, 0)
    p.add(ell((cx - r, hy - r, cx + r, hy + r)), color, ow)
    p.add(poly([(cx + r * .3, hy - r * .9)] + arc(cx, hy, r, -55, 60, 10) + [(cx + r * .5, hy + r * .8)]), dark, 0)
    p.add(poly(ellpts(cx - r * .35, hy - r * .45, r * .32, r * .2, -30)), light, 0)
    p.add(poly(ellpts(cx - h * .12, top + h * .5, h * .04, h * .1, 10)), light, 0)


# ================= 头部附件：流光（128px，三向） =================
def sheen(view):
    p = Pic()
    if view in ("south", "north"):
        # 沿头顶的一道月牙形高光
        outer = arc(50, 50.5, 18.2, 205, 335, 24)
        inner = arc(50, 54.5, 15.5, 325, 215, 24)
        p.add(poly(outer + inner), PALE_L, HEAD_OW)
        p.add(poly(arc(50, 50.5, 18.2, 290, 335, 10) + arc(50, 54.5, 15.5, 325, 300, 8)), PALE_D, 0)
        if view == "south":
            drip(p, 34.8, 44, 55, 3.4, PALE, PALE_L, HEAD_OW)
            drip(p, 64.5, 41, 48, 2.6, PALE, PALE_L, HEAD_OW)
        else:
            drip(p, 37, 42, 56, 3.4, PALE, PALE_L, HEAD_OW)
            drip(p, 61.5, 43, 52, 3.0, PALE, PALE_L, HEAD_OW)
            drip(p, 50, 33, 40, 2.4, PALE, PALE_L, HEAD_OW)
    else:
        # 朝东：脸在右，高光偏后脑，胶滴挂在后脑勺
        outer = arc(47, 50.5, 18.0, 200, 320, 24)
        inner = arc(46, 54.5, 15.2, 310, 210, 24)
        p.add(poly(outer + inner), PALE_L, HEAD_OW)
        p.add(poly(arc(47, 50.5, 18.0, 280, 320, 8) + arc(46, 54.5, 15.2, 310, 288, 6)), PALE_D, 0)
        drip(p, 32.6, 47, 60, 3.4, PALE, PALE_L, HEAD_OW)
    return p


# ================= 异种型图标 =================
def xeno_icon():
    p = Pic()
    humanoid_blob(p, 50, 8, 84)
    eyes(p, 50, 25, 6, 2.6)
    drip(p, 37, 32, 46, 3.6)
    return p


# ================= 基因图标（256px） =================
def g_core():
    p = Pic()
    humanoid_blob(p, 50, 8, 86)
    return p


def g_protoplasm():
    p = Pic()
    p.add(poly([(50, 6)] + arc(50, 58, 34, -40, 220, 40)), OOZE, OW)
    p.add(poly(arc(50, 58, 34, -40, 60, 16) + [(62, 80), (70, 56)]), OOZE_D, 0)
    for (x, y, r) in ((40, 52, 7), (58, 66, 5), (46, 76, 4), (60, 44, 3.5)):
        p.add(ell((x - r, y - r, x + r, y + r)), OOZE_L, 1.4)
        p.add(ell((x - r * .55, y - r * .55, x - r * .1, y - r * .1)), GLOSS, 0)
    p.add(poly(ellpts(36, 36, 6, 3.2, -55)), GLOSS, 0)
    return p


def g_digest():
    p = Pic()
    blob(p, 50, 56, 40, 34, seed=1)
    # 体内正在化开的骨头和果核
    p.add(tent([(34, 62), (56, 50)], 5, 5), BONE, 1.6)
    for (x, y) in ((32, 61), (35, 66), (55, 47), (58, 52)):
        p.add(ell((x - 3.4, y - 3.4, x + 3.4, y + 3.4)), BONE, 1.6)
    p.add(ell((56, 62, 70, 76)), (196, 120, 92), 1.6)
    p.add(tent([(63, 62), (64, 57)], 1.6, 1.2), WOOD_D, 0)
    for (x, y) in ((44, 74), (66, 46), (40, 44)):
        p.add(ell((x - 2.2, y - 2.2, x + 2.2, y + 2.2)), OOZE_L, 0)
    return p


def g_pseudopod():
    p = Pic()
    blob(p, 30, 76, 24, 16, flat=.3, seed=2)
    lash = [(34, 64), (40, 46), (54, 30), (72, 22), (88, 26)]
    p.add(tent(lash, 13, 5), OOZE, OW)
    p.add(tent(lash[1:], 4, 2), OOZE_D, 0)
    for (x, y, r) in ((84, 38, 3.4), (76, 46, 2.6), (90, 46, 2.2)):
        p.add(poly([(x, y - r * 1.8)] + arc(x, y, r, -30, 210, 14)), ACID, 1.6)
    p.add(("line", [(80, 16), (92, 12)], 2.2), ACID_D, 0)
    return p


def g_amorphous():
    p = Pic()
    # 两扇木板之间的窄缝，胶团从缝里挤过去
    for (x0, x1) in ((8, 44), (56, 92)):
        p.add(poly([(x0, 8), (x1, 8), (x1, 92), (x0, 92)]), WOOD, OW)
        p.add(poly([(x1 - 7, 8), (x1, 8), (x1, 92), (x1 - 7, 92)]), WOOD_D, 0)
        for y in (30, 54, 76):
            p.line([(x0 + 3, y), (x1 - 3, y)], 1.2, WOOD_D)
    blob(p, 24, 62, 18, 16, seed=3)
    p.add(tent([(30, 62), (50, 60), (70, 62)], 10, 10), OOZE, OW)
    blob(p, 76, 60, 15, 13, seed=4)
    p.add(tent([(36, 60), (64, 60)], 5, 5), OOZE_L, 0)
    return p


def g_split():
    p = Pic()
    p.add(tent([(36, 54), (64, 54)], 12, 12), OOZE, OW)
    blob(p, 30, 54, 24, 26, seed=5)
    blob(p, 72, 56, 18, 20, seed=6)
    p.add(tent([(42, 54), (58, 55)], 6, 6), OOZE, 0)
    eyes(p, 30, 50, 6, 2.2)
    eyes(p, 72, 52, 5, 1.8)
    return p


def g_chill():
    p = Pic()
    blob(p, 46, 60, 36, 30, seed=7)
    # 表面结了一层冰壳
    p.add(poly([(14, 52), (24, 38), (38, 32), (56, 31), (70, 36), (64, 42), (48, 40), (32, 44), (22, 54)]), ICE_L, 1.6)
    for (x, y) in ((26, 46), (44, 36), (60, 36)):
        p.add(poly([(x, y), (x + 3, y + 7), (x - 2, y + 8)]), ICE, 0)
    snowflake(p, 78, 22, 14)
    return p


def g_desiccate():
    p = Pic()
    flame(p, 50, 94, 60, 30, FLAME_D, FLAME, OW)
    blob(p, 50, 50, 34, 28, color=(150, 160, 112), dark=(118, 126, 86), light=(186, 192, 150), seed=8)
    for pts in ([(30, 40), (38, 48), (34, 58)], [(52, 30), (56, 42), (64, 46), (62, 56)], [(44, 62), (50, 68)]):
        p.line(pts, 1.4, INK)
    for (x, y) in ((70, 20), (80, 30)):
        p.add(("line", [(x, y + 8), (x + 2, y + 4), (x - 1, y)], 1.8), (200, 200, 200), 0)
    return p


def g_sheen():
    p = Pic()
    p.add(ell((22, 18, 78, 82)), OOZE, OW)
    p.add(poly([(64, 22)] + arc(50, 50, 28, -55, 60, 12) + [(66, 76), (72, 50)]), OOZE_D, 0)
    p.add(poly(arc(50, 50, 26, 205, 335, 20) + arc(50, 56, 22, 325, 215, 20)), OOZE_L, 1.6)
    eyes(p, 50, 52, 9, 2.8)
    drip(p, 28, 50, 70, 5)
    return p


# ================= 能力图标（128px） =================
def a_engulf():
    p = Pic()
    blob(p, 50, 58, 42, 34, seed=9)
    # 胶团里半化开的头骨
    p.add(ell((36, 40, 62, 64)), BONE, 1.8)
    p.add(poly([(40, 60), (58, 60), (56, 70), (42, 70)]), BONE, 1.8)
    eyes(p, 49, 52, 5.5, 3.2)
    p.add(poly([(49, 56), (51.5, 60), (46.5, 60)]), INK, 0)
    p.add(poly(blob_pts(50, 66, 30, 14, .08, 3)), OOZE, 0)
    for (x, y) in ((30, 50), (68, 46), (60, 74)):
        p.add(ell((x - 2.6, y - 2.6, x + 2.6, y + 2.6)), OOZE_L, 0)
    return p


def a_fission():
    p = Pic()
    humanoid_blob(p, 40, 12, 80)
    blob(p, 78, 78, 14, 12, seed=10, flat=.2)
    eyes(p, 78, 76, 4, 1.6)
    p.add(tent([(56, 80), (66, 79)], 4, 3), OOZE, 1.4)
    return p


def a_seep():
    p = Pic()
    p.add(poly([(18, 6), (82, 6), (82, 74), (18, 74)]), WOOD, OW)
    p.add(poly([(72, 6), (82, 6), (82, 74), (72, 74)]), WOOD_D, 0)
    for x in (34, 50, 66):
        p.line([(x, 10), (x, 70)], 1.2, WOOD_D)
    p.add(ell((64, 36, 72, 44)), STEEL, 1.6)
    # 门下的缝里渗出来的胶
    p.add(poly(blob_pts(50, 86, 40, 12, .1, 4)), OOZE, OW)
    p.add(poly([(20, 74), (80, 74), (80, 80), (20, 80)]), OOZE_D, 0)
    p.add(poly(ellpts(36, 84, 8, 2.4, 0)), OOZE_L, 0)
    return p


def a_reshape():
    p = Pic()
    # 正在长回来的手：手掌实色，手指由胶团捏出
    p.add(poly([(30, 92), (28, 62), (30, 50), (70, 50), (72, 62), (70, 92)]), OOZE, OW)
    p.add(poly([(58, 50), (70, 50), (72, 62), (70, 92), (60, 92)]), OOZE_D, 0)
    for (x, top, w) in ((34, 22, 9), (46, 14, 9), (58, 18, 9), (69, 30, 8)):
        p.add(tent([(x, 52), (x, top + 4)], w, w * .8), OOZE_L if x > 50 else OOZE, OW)
        p.add(ell((x - w * .4, top, x + w * .4, top + w * .8)), OOZE_L, 0)
    p.add(tent([(24, 70), (14, 56), (16, 44)], 8, 6), OOZE, OW)
    for (x, y, r) in ((84, 20, 6), (90, 40, 4), (80, 8, 3)):
        p.add(poly([(x, y - r), (x + r * .3, y - r * .3), (x + r, y), (x + r * .3, y + r * .3), (x, y + r), (x - r * .3, y + r * .3), (x - r, y), (x - r * .3, y - r * .3)]), GLOSS, 1.2)
    return p


def a_extract():
    p = Pic()
    # 一滴胶落进凝胶堆
    p.add(poly([(50, 6)] + arc(50, 34, 14, -30, 210, 20)), OOZE, OW)
    p.add(ell((42, 26, 48, 34)), GLOSS, 0)
    gel_pile(p, 50, 74, 40, 18)
    return p


# ================= 物品（128px） =================
def gel_pile(p, cx, cy, rx, ry):
    p.add(poly(blob_pts(cx, cy, rx, ry, .1, 2, flat=.25)), OOZE, OW)
    p.add(poly(blob_pts(cx + rx * .12, cy - ry * .2, rx * .62, ry * .7, .12, 5, flat=.2)), OOZE_L, 1.6)
    p.add(poly([(x, y) for (x, y) in blob_pts(cx, cy, rx, ry, .1, 2, flat=.25) if y > cy + ry * .1]), OOZE_D, 0)
    p.add(poly(ellpts(cx - rx * .2, cy - ry * .45, rx * .2, ry * .14, -15)), GLOSS, 0)
    for (dx, dy) in ((.45, -.05), (-.55, .1)):
        x, y = cx + rx * dx, cy + ry * dy
        p.add(ell((x - 2.2, y - 2.2, x + 2.2, y + 2.2)), GLOSS, 0)


def i_gel():
    p = Pic()
    gel_pile(p, 50, 60, 36, 26)
    return p


def i_dressing():
    p = Pic()
    # 卷起来的布条，一头散开，上面浸着凝胶
    p.add(poly([(20, 74), (62, 62), (88, 70), (48, 86)]), CLOTH, OW)
    p.add(ell((14, 36, 52, 80)), CLOTH, OW)
    p.add(ell((22, 44, 44, 72)), CLOTH_D, 1.6)
    p.add(ell((28, 51, 38, 65)), (160, 150, 130), 0)
    p.add(poly(blob_pts(64, 72, 14, 5, .2, 3)), OOZE, 1.6)
    p.add(poly(blob_pts(34, 44, 10, 5, .2, 4)), OOZE_L, 1.4)
    p.add(ell((30, 41, 34, 44)), GLOSS, 0)
    return p


def i_band():
    # 皮带（受皮革材料着色，画成近白灰），挂三袋凝胶和一个铜扣
    p = Pic()
    outer, inner = ellpts(50, 54, 40, 26), ellpts(50, 54, 33, 19)
    p.add([poly(outer), ("-poly", inner)], PALE, OW)
    p.add([poly(ellpts(50, 56, 40, 24)), ("-poly", ellpts(50, 54, 40, 26)), ("-poly", inner)], PALE_D, 0)
    p.add(poly([(42, 70), (58, 70), (58, 86), (42, 86)]), (196, 168, 104), OW)
    p.add(poly([(46, 74), (54, 74), (54, 82), (46, 82)]), PALE_D, 1.2)
    for (x, y) in ((22, 66), (78, 66), (50, 30)):
        p.add(poly(blob_pts(x, y, 9, 10, .08, int(x))), PALE_L, OW)
        p.add(ell((x - 5, y - 6, x - 1, y - 2)), (255, 255, 255), 0)
    return p


ICONS = {
    "Oozefolk/Xenotypes/Oozefolk": xeno_icon,
    "Oozefolk/Genes/OozeCore": g_core, "Oozefolk/Genes/Protoplasm": g_protoplasm, "Oozefolk/Genes/Digest": g_digest,
    "Oozefolk/Genes/Pseudopod": g_pseudopod, "Oozefolk/Genes/Amorphous": g_amorphous, "Oozefolk/Genes/Split": g_split,
    "Oozefolk/Genes/Chill": g_chill, "Oozefolk/Genes/Desiccate": g_desiccate, "Oozefolk/Genes/Sheen": g_sheen,
}
SMALL = {
    "Oozefolk/Abilities/Engulf": a_engulf, "Oozefolk/Abilities/Fission": a_fission, "Oozefolk/Abilities/Seep": a_seep,
    "Oozefolk/Abilities/Reshape": a_reshape, "Oozefolk/Abilities/ExtractGel": a_extract,
    "Oozefolk/Sheen/Sheen_south": lambda: sheen("south"), "Oozefolk/Sheen/Sheen_east": lambda: sheen("east"), "Oozefolk/Sheen/Sheen_north": lambda: sheen("north"),
    "Oozefolk/Items/ProtoplasmGel": i_gel, "Oozefolk/Items/GelDressing": i_dressing, "Oozefolk/Items/StabilizerBand": i_band,
}


def preview(out):
    """预览图：四种肤色的头（原版头形近似 + 流光三向，按肤色正片叠底）、全部图标、物品。"""
    from PIL import Image, ImageDraw, ImageFont, ImageChops

    def font(size):
        for f in (r"C:\Windows\Fonts\msyh.ttc", r"C:\Windows\Fonts\simhei.ttf"):
            if os.path.exists(f):
                return ImageFont.truetype(f, size)
        return ImageFont.load_default()

    def tint(img, rgb):
        c = Image.new("RGBA", img.size, rgb + (255,))
        out = ImageChops.multiply(img.convert("RGBA"), c)
        out.putalpha(img.getchannel("A"))
        return out

    def head(view, rgb):
        # 近似原版 Male_Average_Normal 的头形（128px），肤色平涂 + 暗面，再叠上流光。
        h = Pic()
        if view == "east":
            h.add(poly(ellpts(52, 50, 17, 19.5)), PALE, HEAD_OW)
            h.add(poly([(66, 46), (71, 52), (66, 56)]), PALE, HEAD_OW)
        else:
            h.add(ell((32, 30.5, 68, 69.5)), PALE, HEAD_OW)
        h.add(poly([(58, 33)] + arc(50, 50, 18, -50, 60, 10) + [(58, 67), (62, 50)]), PALE_D, 0)
        if view == "south":
            eyes(h, 50, 54, 7, 1.8)
        elif view == "east":
            eyes(h, 63, 54, 0, 1.8)
        img = h.img.resize((128, 128), Image.LANCZOS)
        s = sheen(view).img.resize((128, 128), Image.LANCZOS)
        img.alpha_composite(s)
        return tint(img, rgb)

    W = 1200
    sheet = Image.new("RGB", (W, 900), (34, 38, 40))
    d = ImageDraw.Draw(sheet)
    d.text((16, 10), "DND边缘大陆 1.38.0：流浆人（贴图预览，程序生成）", fill=(236, 236, 236), font=font(26))
    d.text((16, 52), "四种肤色 × 流光三向（南 / 东 / 北，按肤色着色）", fill=(210, 210, 210), font=font(18))
    x = 16
    for name, rgb in SKINS.items():
        for v in ("south", "east", "north"):
            im = head(v, rgb).resize((88, 88), Image.LANCZOS)
            bg = Image.new("RGBA", (88, 88), (60, 66, 62, 255))
            bg.alpha_composite(im)
            sheet.paste(bg.convert("RGB"), (x, 80))
            x += 92
        d.text((x - 276 + 100, 172), name, fill=(220, 220, 220), font=font(18))
        x += 16
    d.text((16, 210), "异种型与基因图标（256px）", fill=(210, 210, 210), font=font(18))
    x, y = 16, 238
    for rel in ICONS:
        im = Image.open(os.path.join(base.TEX, rel + ".png")).convert("RGBA").resize((108, 108), Image.LANCZOS)
        bg = Image.new("RGBA", im.size, (48, 52, 55, 255)); bg.alpha_composite(im)
        sheet.paste(bg.convert("RGB"), (x, y)); x += 116
    d.text((16, 370), "能力图标（128px）与物品", fill=(210, 210, 210), font=font(18))
    x, y = 16, 398
    for rel in list(SMALL)[:5] + list(SMALL)[8:]:
        im = Image.open(os.path.join(base.TEX, rel + ".png")).convert("RGBA").resize((108, 108), Image.LANCZOS)
        if rel.endswith("StabilizerBand"):
            im = tint(im, (182, 136, 100))
        bg = Image.new("RGBA", im.size, (48, 52, 55, 255)); bg.alpha_composite(im)
        sheet.paste(bg.convert("RGB"), (x, y)); x += 116
    d.text((16, 530), "说明：游戏里流光由 useSkinShader 正片叠底肤色，所以高光不会比肤色更亮，靠轮廓和暗面区分；护带受皮革材料颜色着色（这里用普通皮革色示意）。", fill=(180, 180, 180), font=font(16))
    sheet = sheet.crop((0, 0, W, 570))
    os.makedirs(os.path.dirname(os.path.abspath(out)), exist_ok=True)
    sheet.save(out)
    return out


if __name__ == "__main__":
    out = []
    for rel, fn in ICONS.items():
        out.append(fn().save(rel, 256))
    for rel, fn in SMALL.items():
        out.append(fn().save(rel, 128))
    print(len(out), "textures written")
    if len(sys.argv) > 2:
        print(preview(sys.argv[2]))
