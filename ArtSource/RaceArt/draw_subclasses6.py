# 1.140.0 热门子职业 15 个的 15 个徽记 + 69 个能力图标，6 个战斗专长图标，亡灵军团 4 个异种型图标（128×128）。
# 画法沿用 draw_subclasses5.py：粗黑描边、平涂 + 右下暗部、低饱和。用法：E:\Python\python.exe draw_subclasses6.py <模组根目录>
import math, sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from draw_class_art import (Icon, poly_mask, ellipse_mask, line_mask, arc_mask, rrect_mask, union, subtract, star_pts, shield_pts, sword_masks, bust_mask,
                            Image, CREAM, CREAM_D, BONE, BONE_D, GOLD, GOLD_D, RUST, RUST_D, SLATE, SLATE_D, VIOLET, VIOLET_D, STONE, STONE_D, KHAKI)
from draw_class_art2 import flame_mask, eye_mask, wing_mask, heart_mask, rays, wolf_mask, palm_mask, bolt_mask, bear_mask, tree_mask, cloud_mask, fist_mask, leaf_mask
from draw_legendary_174 import gear
from PIL import ImageDraw

ROOT = sys.argv[1] if len(sys.argv) > 1 else '.'
INKC = (30, 26, 34)
MOON = (190, 204, 242); MOON_D = (130, 142, 180)
CHAMP = (216, 178, 90); CHAMP_D = (150, 120, 56)
HOLY = (242, 234, 190); HOLY_D = (180, 170, 130)
BLOOD = (128, 52, 64); BLOOD_D = (84, 32, 42)
IRON = (190, 78, 64); IRON_D = (130, 50, 40)
LORE = (102, 140, 216); LORE_D = (64, 92, 150)
GRAVE = (96, 108, 92); GRAVE_D = (60, 70, 58)
HEX = (140, 116, 180); HEX_D = (92, 74, 124)
SUN = (242, 216, 140); SUN_D = (180, 156, 90)
ROT = (90, 128, 90); ROT_D = (54, 82, 54)
SIGHT = (192, 180, 242); SIGHT_D = (130, 120, 180)
WARD = (128, 178, 242); WARD_D = (80, 120, 170)
VEIL = (216, 140, 216); VEIL_D = (150, 90, 150)
FLAIR = (216, 116, 90); FLAIR_D = (150, 76, 56)
PSI = (154, 128, 230); PSI_D = (100, 80, 160)
EMBLEMS = {'CircleMoon': MOON, 'Champion': CHAMP, 'Devotion': HOLY, 'Vengeance': BLOOD, 'WarDomain': IRON, 'KnowledgeDomain': LORE, 'GraveDomain': GRAVE,
           'Hexblade': HEX, 'Celestial': SUN, 'Necromancy': ROT, 'Divination': SIGHT, 'Abjuration': WARD, 'Illusion': VEIL, 'Swashbuckler': FLAIR, 'Soulknife': PSI}

def moon(I, cx, cy, r, col):
    I.layer(ellipse_mask(cx, cy, r), col)
    I.layer(ellipse_mask(cx + r * 0.45, cy - r * 0.2, r * 0.85), INKC, outline=False, shade=False)
def sword(I, cx, cy, ang, L=56, blade=CREAM, hilt=RUST):
    for m, c in sword_masks(cx, cy, ang, L, blade, hilt): I.layer(m, c)
def skull(I, cx, cy, r, col):
    I.layer(ellipse_mask(cx, cy, r, r * 0.95), col); I.layer(rrect_mask(cx - r * 0.55, cy + r * 0.5, cx + r * 0.55, cy + r * 1.1, 4), col)
    I.layer(ellipse_mask(cx - r * 0.38, cy - r * 0.1, r * 0.22), INKC, outline=False, shade=False); I.layer(ellipse_mask(cx + r * 0.38, cy - r * 0.1, r * 0.22), INKC, outline=False, shade=False)
def book(I, cx, cy, col):
    I.layer(rrect_mask(cx - 30, cy - 22, cx + 30, cy + 22, 6), col); I.layer(line_mask([(cx, cy - 22), (cx, cy + 22)], 4), INKC, outline=False, shade=False)
def dots(I, pts, r, col):
    for x, y in pts: I.layer(ellipse_mask(x, y, r), col, outline=False, shade=False)
def person(I, cx, cy, col, s=1.0): I.layer(bust_mask(cx, cy, s), col)
def chain(I, pts, col):
    for i, (x, y) in enumerate(pts): I.layer(ellipse_mask(x, y, 8, 5), col, shade=False)
def hand(I, cx, cy, col): I.layer(palm_mask(cx, cy, 0.9), col)

def icon(name):
    I = Icon()
    # ---- 月亮结社 ----
    if name == 'MOShape':      I.layer(bear_mask(64, 68, 0.95), MOON); moon(I, 96, 32, 14, CREAM)
    elif name == 'MOStrike':   I.layer(bear_mask(58, 70, 0.8), MOON_D); [I.layer(line_mask([(80 + k * 10, 40), (96 + k * 10, 92)], 5), CREAM) for k in range(3)]
    elif name == 'MOElemental':I.layer(flame_mask(64, 70, 1.1), (230, 140, 80)); moon(I, 96, 30, 12, CREAM)
    elif name == 'MOForms':    [moon(I, 40 + k * 24, 64, 14, MOON) for k in range(3)]
    # ---- 勇士 ----
    elif name == 'CPCrit':     sword(I, 64, 66, -60, 60, CREAM, RUST); I.layer(poly_mask(star_pts(92, 36, 18, 7, 6)), CHAMP, glow=CHAMP)
    elif name == 'CPAthlete':  person(I, 64, 46, CHAMP); I.layer(line_mask([(30, 104), (98, 104)], 6), CHAMP_D); I.layer(line_mask([(24, 70), (44, 60)], 6), CHAMP)
    elif name == 'CPStyle':    sword(I, 48, 66, -70, 52, CREAM, RUST); sword(I, 80, 66, -110, 52, CREAM, RUST)
    elif name == 'CPSurvivor': I.layer(heart_mask(64, 66, 0.95), IRON); I.layer(poly_mask(shield_pts(64, 64, 70, 84)), CHAMP, outline=True, shade=False); I.layer(heart_mask(64, 66, 0.5), IRON)
    # ---- 奉献之誓 ----
    elif name == 'DTWeapon':   sword(I, 64, 66, -90, 64, HOLY, GOLD_D); rays(I, 64, 36, 16, 30, 8, SUN, w=3)
    elif name == 'DTTurn':     skull(I, 64, 60, 24, BONE); rays(I, 64, 60, 36, 52, 10, SUN, w=4)
    elif name == 'DTAura':     I.layer(arc_mask(64, 64, 44, 0, 360, 6), HOLY, shade=False); person(I, 64, 50, HOLY)
    elif name == 'DTPurity':   hand(I, 60, 72, HOLY); rays(I, 92, 36, 8, 22, 8, SUN, w=3)
    elif name == 'DTNimbus':   rays(I, 64, 64, 24, 54, 16, SUN, w=5); I.layer(ellipse_mask(64, 64, 24), HOLY)
    # ---- 复仇之誓 ----
    elif name == 'VNVow':      I.layer(eye_mask(64, 60, 70, 34), BLOOD); I.layer(ellipse_mask(64, 60, 9), INKC, shade=False); sword(I, 64, 96, 0, 50, CREAM, RUST)
    elif name == 'VNAbjure':   person(I, 64, 46, BLOOD); rays(I, 64, 56, 40, 56, 6, BLOOD_D, w=4, a0=-150)
    elif name == 'VNRelentless': I.layer(wolf_mask(60, 66, 0.9), BLOOD); [I.layer(line_mask([(90, 50 + k * 14), (112, 50 + k * 14)], 4), CREAM, shade=False) for k in range(3)]
    elif name == 'VNSoul':     I.layer(flame_mask(64, 70, 1.1), BLOOD); I.layer(flame_mask(64, 78, 0.6), CREAM)
    elif name == 'VNAngel':    I.layer(wing_mask(64, 64, -1, 0.9), BLOOD); I.layer(wing_mask(64, 64, 1, 0.9), BLOOD); sword(I, 64, 70, -90, 60, CREAM, RUST)
    # ---- 战争领域 ----
    elif name == 'WDPriest':   I.layer(poly_mask(shield_pts(64, 64, 64, 80)), IRON); I.layer(line_mask([(64, 44), (64, 86)], 6), CREAM, shade=False); I.layer(line_mask([(50, 58), (78, 58)], 6), CREAM, shade=False)
    elif name == 'WDGuided':   sword(I, 64, 66, -90, 62, CREAM, RUST); I.layer(arc_mask(64, 40, 16, 0, 360, 4), IRON, shade=False); I.layer(ellipse_mask(64, 40, 5), IRON, shade=False)
    elif name == 'WDBlessing': hand(I, 54, 74, IRON); sword(I, 88, 52, -60, 44, CREAM, RUST)
    elif name == 'WDStrike':   I.layer(fist_mask(64, 66, 1.0), IRON); rays(I, 64, 66, 30, 44, 8, CREAM, w=3)
    elif name == 'WDAvatar':   I.layer(rrect_mask(40, 24, 88, 60, 10), IRON); I.layer(line_mask([(52, 44), (76, 44)], 5), INKC, shade=False); I.layer(rrect_mask(30, 62, 98, 108, 12), IRON_D)
    # ---- 知识领域 ----
    elif name == 'KDBless':    book(I, 64, 66, LORE); rays(I, 64, 32, 8, 20, 6, SUN, w=3)
    elif name == 'KDAges':     book(I, 60, 72, LORE_D); gear(I, 92, 40, 16, KHAKI)
    elif name == 'KDRead':     I.layer(eye_mask(64, 60, 70, 34), LORE); I.layer(ellipse_mask(64, 60, 10), INKC, shade=False); dots(I, [(40, 96), (64, 100), (88, 96)], 4, LORE)
    elif name == 'KDPotent':   book(I, 64, 70, LORE); I.layer(poly_mask(star_pts(64, 40, 14, 6, 5)), SUN, glow=SUN)
    elif name == 'KDVisions':  I.layer(eye_mask(64, 60, 76, 36), LORE); rays(I, 64, 60, 42, 56, 12, SIGHT, w=3); I.layer(ellipse_mask(64, 60, 10), INKC, shade=False)
    # ---- 坟墓领域 ----
    elif name == 'GDEyes':     I.layer(rrect_mask(44, 40, 84, 100, 8), GRAVE); I.layer(eye_mask(64, 66, 30, 14), BONE); I.layer(ellipse_mask(64, 66, 4), INKC, shade=False)
    elif name == 'GDPath':     I.layer(rrect_mask(44, 44, 84, 104, 8), GRAVE); I.layer(line_mask([(64, 20), (64, 56)], 6), BONE); I.layer(poly_mask([(50, 50), (78, 50), (64, 66)]), BONE)
    elif name == 'GDSentinel': I.layer(rrect_mask(46, 44, 82, 100, 8), GRAVE_D); person(I, 64, 36, GRAVE); I.layer(heart_mask(96, 90, 0.45), IRON)
    elif name == 'GDPotent':   I.layer(rrect_mask(44, 48, 84, 104, 8), GRAVE); I.layer(poly_mask(star_pts(64, 36, 14, 6, 5)), SUN, glow=SUN)
    elif name == 'GDKeeper':   I.layer(rrect_mask(44, 48, 84, 104, 8), GRAVE_D); I.layer(flame_mask(64, 40, 0.6), SIGHT); I.layer(flame_mask(36, 60, 0.4), SIGHT); I.layer(flame_mask(92, 60, 0.4), SIGHT)
    # ---- 咒剑宗主 ----
    elif name == 'HXCurse':    sword(I, 64, 66, -90, 64, HEX, HEX_D); I.layer(eye_mask(64, 54, 26, 12), CREAM); I.layer(ellipse_mask(64, 54, 4), INKC, shade=False)
    elif name == 'HXWarrior':  sword(I, 56, 68, -70, 56, HEX, HEX_D); I.layer(poly_mask(shield_pts(86, 72, 36, 44)), HEX_D)
    elif name == 'HXArmor':    I.layer(rrect_mask(34, 36, 94, 100, 12), HEX); I.layer(rrect_mask(50, 52, 78, 84, 6), HEX_D)
    elif name == 'HXSpecter':  I.layer(ellipse_mask(64, 60, 24, 30), HEX_D); I.layer(ellipse_mask(64, 100, 30, 10), HEX_D); sword(I, 96, 60, -90, 44, HEX, HEX_D)
    elif name == 'HXMaster':   sword(I, 64, 66, -90, 64, HEX, HEX_D); rays(I, 64, 40, 18, 30, 8, HEX, w=3); I.layer(poly_mask(star_pts(64, 40, 10, 4, 5)), CREAM)
    # ---- 天界宗主 ----
    elif name == 'CEHeal':     rays(I, 64, 64, 28, 44, 10, SUN, w=4); I.layer(heart_mask(64, 66, 0.7), (240, 150, 150))
    elif name == 'CESoul':     I.layer(wing_mask(64, 64, -1, 0.7), CREAM); I.layer(wing_mask(64, 64, 1, 0.7), CREAM); I.layer(ellipse_mask(64, 64, 14), SUN, glow=SUN)
    elif name == 'CEResilience': I.layer(poly_mask(shield_pts(64, 64, 70, 84)), SUN); I.layer(arc_mask(64, 36, 12, 0, 360, 4), CREAM, shade=False)
    elif name == 'CEBurst':    rays(I, 64, 64, 10, 52, 12, SUN, w=5); I.layer(ellipse_mask(64, 64, 14), CREAM)
    elif name == 'CEVengeance':person(I, 64, 48, SUN); rays(I, 64, 64, 40, 56, 12, (240, 140, 80), w=4)
    # ---- 死灵学派 ----
    elif name == 'NMHarvest':  skull(I, 60, 62, 22, BONE); I.layer(line_mask([(90, 30), (104, 100)], 5), ROT_D); I.layer(arc_mask(96, 40, 18, 200, 340, 6), CREAM, shade=False)
    elif name == 'NMThralls':  skull(I, 44, 60, 16, BONE); skull(I, 84, 60, 16, BONE); I.layer(rrect_mask(30, 84, 98, 100, 6), ROT)
    elif name == 'NMInured':   skull(I, 64, 60, 24, BONE); I.layer(poly_mask(shield_pts(64, 70, 76, 92)), ROT, shade=False, outline=True); skull(I, 64, 66, 18, BONE)
    elif name == 'NMCommand':  skull(I, 64, 52, 20, BONE); hand(I, 64, 100, ROT); dots(I, [(44, 80), (64, 76), (84, 80)], 4, ROT)
    # ---- 预言学派 ----
    elif name == 'DIPortent':  I.layer(ellipse_mask(64, 64, 34), SIGHT); I.layer(ellipse_mask(64, 64, 22), SIGHT_D, shade=False); I.layer(poly_mask(star_pts(64, 64, 12, 5, 4)), CREAM)
    elif name == 'DIExpert':   I.layer(ellipse_mask(64, 64, 32), SIGHT_D); I.layer(arc_mask(64, 64, 32, 0, 360, 5), SIGHT, shade=False); I.layer(line_mask([(64, 64), (64, 40)], 5), CREAM, shade=False); I.layer(line_mask([(64, 64), (82, 72)], 5), CREAM, shade=False)
    elif name == 'DIEye':      I.layer(eye_mask(64, 62, 72, 34), SIGHT); I.layer(ellipse_mask(64, 62, 11), INKC, shade=False); rays(I, 64, 62, 40, 52, 8, CREAM, w=3)
    elif name == 'DIGreater':  I.layer(ellipse_mask(64, 64, 36), SIGHT); [I.layer(poly_mask(star_pts(x, y, 9, 4, 4)), CREAM) for x, y in ((50, 56), (78, 56), (64, 78))]
    # ---- 防护学派 ----
    elif name == 'AJWard':     I.layer(ellipse_mask(64, 64, 40), WARD_D, shade=False); I.layer(ellipse_mask(64, 64, 28), WARD); person(I, 64, 54, CREAM, 0.6)
    elif name == 'AJProjected':I.layer(ellipse_mask(44, 70, 24), WARD); I.layer(ellipse_mask(92, 50, 20), WARD_D); I.layer(line_mask([(60, 62), (78, 56)], 4), CREAM, shade=False)
    elif name == 'AJImproved': I.layer(poly_mask(shield_pts(64, 64, 70, 84)), WARD); I.layer(poly_mask(shield_pts(64, 64, 44, 54)), WARD_D)
    elif name == 'AJResist':   I.layer(poly_mask(shield_pts(64, 64, 72, 86)), WARD); I.layer(poly_mask(star_pts(64, 62, 18, 8, 5)), CREAM)
    # ---- 幻术学派 ----
    elif name == 'IUSavant':   person(I, 54, 48, VEIL_D, 0.8); person(I, 74, 48, VEIL, 0.8)
    elif name == 'IUPhantasm': I.layer(cloud_mask(64, 64, 1.1), VEIL); I.layer(eye_mask(52, 60, 16, 8), INKC, shade=False); I.layer(eye_mask(78, 60, 16, 8), INKC, shade=False)
    elif name == 'IUSelf':     person(I, 44, 50, VEIL_D, 0.8); person(I, 84, 50, VEIL, 0.8); dots(I, [(60, 60), (66, 56), (72, 60)], 3, CREAM)
    elif name == 'IUReality':  [person(I, x, y, c, 0.6) for x, y, c in ((40, 52, VEIL_D), (64, 40, VEIL), (88, 52, VEIL_D))]; I.layer(poly_mask(star_pts(64, 96, 10, 4, 5)), CREAM)
    # ---- 风流剑客 ----
    elif name == 'SBFootwork': I.layer(line_mask([(36, 96), (58, 60), (80, 96)], 7), FLAIR); dots(I, [(50, 44), (64, 36), (78, 44)], 4, FLAIR_D)
    elif name == 'SBAudacity': sword(I, 64, 66, -70, 62, CREAM, FLAIR_D); I.layer(poly_mask(star_pts(96, 40, 12, 5, 5)), FLAIR, glow=FLAIR)
    elif name == 'SBPanache':  I.layer(ellipse_mask(64, 60, 34, 26), FLAIR); I.layer(poly_mask([(48, 80), (62, 80), (44, 100)]), FLAIR); dots(I, [(52, 60), (64, 60), (76, 60)], 4, CREAM)
    elif name == 'SBElegant':  sword(I, 64, 66, -80, 60, CREAM, FLAIR_D); I.layer(arc_mask(64, 66, 40, 200, 340, 5), FLAIR, shade=False)
    elif name == 'SBDuelist':  sword(I, 46, 66, -60, 56, CREAM, FLAIR_D); sword(I, 82, 66, -120, 56, CREAM, FLAIR_D); I.layer(poly_mask(star_pts(64, 96, 10, 4, 5)), FLAIR)
    # ---- 魂刃 ----
    elif name == 'SNBlades':   sword(I, 64, 66, -90, 64, PSI, PSI_D); rays(I, 64, 40, 16, 26, 8, PSI, w=3)
    elif name == 'SNPower':    I.layer(ellipse_mask(64, 50, 26), PSI); I.layer(arc_mask(64, 50, 36, 0, 360, 4), PSI_D, shade=False); I.layer(rrect_mask(48, 78, 80, 104, 8), PSI_D)
    elif name == 'SNThrow':    sword(I, 70, 60, -40, 54, PSI, PSI_D); [I.layer(line_mask([(24, 70 + k * 12), (44, 76 + k * 12)], 4), PSI, shade=False) for k in range(3)]
    elif name == 'SNVeil':     I.layer(cloud_mask(64, 70, 1.1), PSI_D); person(I, 64, 44, PSI, 0.7)
    elif name == 'SNRend':     I.layer(ellipse_mask(64, 56, 28), PSI); I.layer(line_mask([(44, 44), (84, 72)], 6), INKC, shade=False); I.layer(line_mask([(84, 44), (44, 72)], 6), INKC, shade=False)
    return I.done(128)

def emblem(sub):
    I = Icon(); col = EMBLEMS[sub]
    I.layer(poly_mask(shield_pts(64, 64, 100, 116)), GOLD_D)
    I.layer(poly_mask(shield_pts(64, 62, 86, 100)), col)
    if sub == 'CircleMoon': moon(I, 64, 60, 26, CREAM)
    elif sub == 'Champion': I.layer(poly_mask(star_pts(64, 60, 24, 10, 5)), CREAM)
    elif sub == 'Devotion': I.layer(line_mask([(64, 34), (64, 86)], 7), CREAM, shade=False); I.layer(line_mask([(46, 52), (82, 52)], 7), CREAM, shade=False)
    elif sub == 'Vengeance': sword(I, 64, 62, -90, 56, CREAM, RUST)
    elif sub == 'WarDomain': I.layer(fist_mask(64, 62, 0.9), CREAM)
    elif sub == 'KnowledgeDomain': book(I, 64, 62, CREAM)
    elif sub == 'GraveDomain': I.layer(rrect_mask(50, 40, 78, 86, 8), CREAM)
    elif sub == 'Hexblade': sword(I, 64, 62, -90, 56, CREAM, HEX_D); I.layer(eye_mask(64, 52, 20, 10), INKC, shade=False)
    elif sub == 'Celestial': rays(I, 64, 60, 12, 30, 8, CREAM, w=4); I.layer(ellipse_mask(64, 60, 12), CREAM)
    elif sub == 'Necromancy': skull(I, 64, 58, 20, BONE)
    elif sub == 'Divination': I.layer(eye_mask(64, 60, 52, 26), CREAM); I.layer(ellipse_mask(64, 60, 8), INKC, shade=False)
    elif sub == 'Abjuration': I.layer(poly_mask(shield_pts(64, 62, 44, 54)), CREAM)
    elif sub == 'Illusion': person(I, 54, 50, CREAM_D, 0.6); person(I, 72, 50, CREAM, 0.6)
    elif sub == 'Swashbuckler': sword(I, 54, 62, -60, 46, CREAM, RUST); sword(I, 74, 62, -120, 46, CREAM, RUST)
    elif sub == 'Soulknife': sword(I, 64, 62, -90, 56, PSI, PSI_D)
    return I.done(128)

def feat(name):
    I = Icon()
    if name == 'DualWielder': sword(I, 46, 66, -65, 58, CREAM, RUST); sword(I, 82, 66, -115, 58, CREAM, RUST)
    elif name == 'ShieldMaster': I.layer(poly_mask(shield_pts(64, 64, 72, 88)), SLATE); I.layer(poly_mask(shield_pts(64, 64, 44, 56)), SLATE_D); I.layer(fist_mask(96, 90, 0.5), BONE)
    elif name == 'PolearmMaster': I.layer(line_mask([(32, 108), (96, 24)], 7), RUST); I.layer(poly_mask([(88, 34), (104, 16), (106, 40)]), CREAM); I.layer(ellipse_mask(32, 108, 7), STONE)
    elif name == 'CrossbowExpert': I.layer(line_mask([(28, 48), (100, 48)], 6), RUST); I.layer(arc_mask(64, 48, 36, 180, 360, 5), CREAM, shade=False); I.layer(line_mask([(64, 30), (64, 104)], 6), RUST_D); I.layer(poly_mask([(58, 30), (70, 30), (64, 16)]), CREAM)
    elif name == 'Charger': person(I, 72, 42, KHAKI); [I.layer(line_mask([(20, 60 + k * 14), (44, 60 + k * 14)], 4), CREAM, shade=False) for k in range(3)]
    elif name == 'Grappler': I.layer(fist_mask(50, 60, 0.9), BONE); I.layer(fist_mask(80, 72, 0.9), BONE_D); I.layer(arc_mask(64, 66, 40, 20, 160, 5), RUST, shade=False)
    return I.done(128)

def xeno(name):
    I = Icon(); col = (236, 236, 236)
    if name == 'Ghoul': skull(I, 64, 56, 24, col); I.layer(line_mask([(48, 92), (58, 112)], 5), col); I.layer(line_mask([(80, 92), (70, 112)], 5), col)
    elif name == 'Wight': skull(I, 64, 54, 22, col); sword(I, 98, 76, -90, 44, col, (180, 180, 180))
    elif name == 'Wraith': I.layer(cloud_mask(64, 70, 1.1), col); I.layer(ellipse_mask(64, 50, 18), col); I.layer(ellipse_mask(58, 48, 4), INKC, shade=False); I.layer(ellipse_mask(70, 48, 4), INKC, shade=False)
    elif name == 'Vampire': I.layer(wing_mask(64, 64, -1, 0.8), col); I.layer(wing_mask(64, 64, 1, 0.8), col); I.layer(ellipse_mask(64, 60, 14), col); I.layer(poly_mask([(58, 70), (62, 70), (60, 80)]), col); I.layer(poly_mask([(66, 70), (70, 70), (68, 80)]), col)
    return I.done(128)

ICONS = ['MOShape', 'MOStrike', 'MOElemental', 'MOForms', 'CPCrit', 'CPAthlete', 'CPStyle', 'CPSurvivor', 'DTWeapon', 'DTTurn', 'DTAura', 'DTPurity', 'DTNimbus',
         'VNVow', 'VNAbjure', 'VNRelentless', 'VNSoul', 'VNAngel', 'WDPriest', 'WDGuided', 'WDBlessing', 'WDStrike', 'WDAvatar', 'KDBless', 'KDAges', 'KDRead', 'KDPotent', 'KDVisions',
         'GDEyes', 'GDPath', 'GDSentinel', 'GDPotent', 'GDKeeper', 'HXCurse', 'HXWarrior', 'HXArmor', 'HXSpecter', 'HXMaster', 'CEHeal', 'CESoul', 'CEResilience', 'CEBurst', 'CEVengeance',
         'NMHarvest', 'NMThralls', 'NMInured', 'NMCommand', 'DIPortent', 'DIExpert', 'DIEye', 'DIGreater', 'AJWard', 'AJProjected', 'AJImproved', 'AJResist',
         'IUSavant', 'IUPhantasm', 'IUSelf', 'IUReality', 'SBFootwork', 'SBAudacity', 'SBPanache', 'SBElegant', 'SBDuelist', 'SNBlades', 'SNPower', 'SNThrow', 'SNVeil', 'SNRend']
FEATS = ['DualWielder', 'ShieldMaster', 'PolearmMaster', 'CrossbowExpert', 'Charger', 'Grappler']
XENOS = ['Ghoul', 'Wight', 'Wraith', 'Vampire']
if __name__ == '__main__':
    out = os.path.join(ROOT, 'Textures', 'LP'); pv = os.path.join(ROOT, 'ArtSource', 'Preview'); os.makedirs(pv, exist_ok=True)
    for d in ('Classes/Subclass6', 'Classes/Subclass', 'Feats', 'Undead'): os.makedirs(os.path.join(out, d), exist_ok=True)
    items = [('Classes/Subclass6', n, icon(n)) for n in ICONS] + [('Classes/Subclass', 'Emblem_' + s, emblem(s)) for s in EMBLEMS] + [('Feats', n, feat(n)) for n in FEATS] + [('Undead', 'Xeno_' + n, xeno(n)) for n in XENOS]
    cols = 10; cell = 150; rows = (len(items) + cols - 1) // cols
    sheet = Image.new('RGBA', (cols * cell + 20, rows * (cell + 24) + 30), (46, 48, 52, 255)); dr = ImageDraw.Draw(sheet)
    for i, (sub, n, im) in enumerate(items):
        im.save(os.path.join(out, sub, n + '.png'))
        x = 10 + (i % cols) * cell + 11; y = 20 + (i // cols) * (cell + 24)
        sheet.paste(im, (x, y), im); dr.text((x, y + 130), n, fill=(220, 220, 210, 255))
    sheet.save(os.path.join(pv, 'Subclasses6-icons.png'))
    print('subclass6 art written:', len(ICONS), 'icons +', len(EMBLEMS), 'emblems +', len(FEATS), 'feats +', len(XENOS), 'xenos')
