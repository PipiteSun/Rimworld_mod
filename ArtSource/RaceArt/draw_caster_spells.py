# -*- coding: utf-8 -*-
# 1.131.0 施法职业法术图标（1.130.0 的 83 个 + 1.131.0 的 35 个）：RimWorld / VPE 画风（粗黑描边、平涂 + 右下暗部、低饱和），
# 沿用 draw_class_art / draw_class_art2 / draw_classes3 / draw_wizard_spells 的形状和配色。128×128。
# 用法（cwd = ArtSource/RaceArt）：python draw_caster_spells.py <模组根目录> [名字...]
#   → Textures/LP/Classes/CasterSpells/<名字>.png，并生成预览 ArtSource/CasterSpells-1.131.0-icons.png
import math, sys, os, re
from PIL import Image, ImageDraw, ImageFont
from draw_class_art import (Icon, poly_mask, ellipse_mask, line_mask, arc_mask, rrect_mask, union, subtract, star_pts, bust_mask, shield_pts,
                            CREAM, CREAM_D, KHAKI, KHAKI_D, SLATE, SLATE_D, RUST, RUST_D, VIOLET, VIOLET_D, GOLD, GOLD_D, BONE, BONE_D, STONE, STONE_D, INK)
from draw_class_art2 import flame_mask, cloud_mask, bolt_mask, fist_mask, palm_mask, paw_mask, wolf_mask, bear_mask, heart_mask, eye_mask, snow_mask, wing_mask, tree_mask, leaf_mask, rays, MOSS, MOSS_D, BARK, FUR, PLUM, PLUM_D, ICE, OCHRE, SKIN
from draw_classes3 import axe, arrow, dagger, hood, DUSK, DUSK_D, POISON, BLOOD
from draw_wizard_spells import skull, book, circle_rune, door, hourglass, tentacle, ray, almond, big_bust, gear, ARC, FIRE, EMBER, FROST, NECRO, NECRO_D, SUNC, FORCE, WATER

ROOT = sys.argv[1] if len(sys.argv) > 1 else os.path.join('..', '..')
OUT = os.path.join(ROOT, 'Textures', 'LP', 'Classes', 'CasterSpells')
HOLY = (226, 204, 132); HOLY_D = (176, 150, 84)
NAT = (122, 156, 92)
SHADOW = (70, 62, 92)

# ---------- 小部件 ----------
def bell(I, cx, cy, s=1.0, col=STONE):
    I.layer(union(poly_mask([(cx - 26 * s, cy + 22 * s), (cx - 18 * s, cy - 16 * s), (cx + 18 * s, cy - 16 * s), (cx + 26 * s, cy + 22 * s)]), ellipse_mask(cx, cy - 18 * s, 18 * s, 12 * s)), col)
    I.layer(ellipse_mask(cx, cy + 28 * s, 7 * s), BONE_D)
def bubble(I, cx, cy, w=40, h=30, col=CREAM):
    I.layer(union(ellipse_mask(cx, cy, w, h), poly_mask([(cx - w * .5, cy + h * .6), (cx - w * .9, cy + h * 1.3), (cx - w * .1, cy + h * .8)])), col)
def drop(I, cx, cy, s=1.0, col=WATER):
    I.layer(union(poly_mask([(cx, cy - 30 * s), (cx - 18 * s, cy + 2 * s), (cx + 18 * s, cy + 2 * s)]), ellipse_mask(cx, cy + 6 * s, 18 * s)), col)
def sword(I, x0=30, y0=102, x1=98, y1=26, col=CREAM, hilt=RUST):
    I.layer(line_mask([(x0, y0), (x1, y1)], 11), col)
    dx, dy = x1 - x0, y1 - y0; L = math.hypot(dx, dy); ux, uy = dx / L, dy / L; px, py = -uy, ux
    gx, gy = x0 + ux * 18, y0 + uy * 18
    I.layer(line_mask([(gx + px * 16, gy + py * 16), (gx - px * 16, gy - py * 16)], 8), hilt)
    I.layer(ellipse_mask(x0 - ux * 4, y0 - uy * 4, 6), GOLD)
def hammer(I, cx, cy, col=HOLY):
    I.layer(line_mask([(cx - 26, cy + 34), (cx + 14, cy - 10)], 9), BARK)
    I.layer(poly_mask([(cx + 2, cy - 34), (cx + 38, cy - 2), (cx + 24, cy + 12), (cx - 12, cy - 20)]), col, glow=(200, 180, 110))
def goblet(I, cx, cy, col=GOLD):
    I.layer(union(poly_mask([(cx - 22, cy - 26), (cx + 22, cy - 26), (cx + 12, cy), (cx - 12, cy)]), rrect_mask(cx - 4, cy, cx + 4, cy + 22, 2), rrect_mask(cx - 16, cy + 20, cx + 16, cy + 28, 3)), col)
def berries(I, cx, cy, col=(178, 64, 92)):
    for dx, dy in ((-12, 6), (10, 8), (0, -8), (-2, 20), (16, -6)): I.layer(ellipse_mask(cx + dx, cy + dy, 11), col)
    I.layer(leaf_mask(cx + 4, cy - 18, cx + 30, cy - 40, 12), MOSS)
def crescent(I, cx, cy, r, col=FROST):
    I.layer(subtract(ellipse_mask(cx, cy, r), ellipse_mask(cx + r * .45, cy - r * .3, r * .85)), col)
def spikes(I, y, col=MOSS_D, n=6):
    for i in range(n):
        x = 14 + i * 100 / (n - 1)
        I.layer(poly_mask([(x - 9, y + 20), (x, y - 18 - (i % 2) * 10), (x + 9, y + 20)]), col)
def horns(I, cx, cy, col=BONE):
    I.layer(poly_mask([(cx - 18, cy), (cx - 32, cy - 34), (cx - 8, cy - 6)]), col); I.layer(poly_mask([(cx + 18, cy), (cx + 32, cy - 34), (cx + 8, cy - 6)]), col)
def spiral(I, cx, cy, col=PLUM, n=4):
    for i in range(n): I.layer(arc_mask(cx + (i % 2) * 4 - 2, cy, 12 + i * 11, i * 90, i * 90 + 270, 7), col)
def horse(I, cx, cy, col=CREAM):
    I.layer(poly_mask([(cx - 6, cy + 40), (cx - 24, cy + 6), (cx - 30, cy - 18), (cx - 14, cy - 40), (cx - 6, cy - 50), (cx + 2, cy - 38), (cx + 30, cy - 18), (cx + 34, cy - 4), (cx + 22, cy + 2), (cx + 8, cy - 6), (cx + 14, cy + 40)]), col)
    I.layer(ellipse_mask(cx + 4, cy - 26, 3.5), INK, shade=False, outline=False)
    I.layer(line_mask([(cx - 14, cy - 40), (cx - 30, cy - 4), (cx - 22, cy + 30)], 8), GOLD, shade=False)
def footprint(I, cx, cy, s, col):
    I.layer(ellipse_mask(cx, cy, 8 * s, 13 * s), col)
    for dx in (-7, -2, 3, 8): I.layer(ellipse_mask(cx + dx * s, cy - 17 * s, 2.6 * s), col)
def spider(I, cx, cy, col=DUSK):
    for a in (-60, -25, 25, 60):
        for side in (-1, 1):
            r = math.radians(a); x1 = cx + side * 40 * math.cos(r); y1 = cy + 34 * math.sin(r)
            I.layer(line_mask([(cx, cy), (cx + side * 26 * math.cos(r), cy + 24 * math.sin(r) - 12), (x1, y1)], 5), col)
    I.layer(union(ellipse_mask(cx, cy + 6, 18, 20), ellipse_mask(cx, cy - 20, 12)), col)
def note(I, cx, cy, col=GOLD):
    I.layer(union(ellipse_mask(cx - 8, cy + 22, 11, 8), rrect_mask(cx, cy - 26, cx + 5, cy + 22, 2), poly_mask([(cx + 3, cy - 26), (cx + 26, cy - 12), (cx + 26, cy - 2), (cx + 3, cy - 14)])), col)
def magnifier(I, cx, cy, col=STONE):
    I.layer(arc_mask(cx, cy, 24, 0, 360, 8), col); I.layer(line_mask([(cx + 18, cy + 18), (cx + 42, cy + 42)], 11), BARK)
def funnel(I, cx, cy, col=SLATE):
    for i in range(5): I.layer(ellipse_mask(cx + (i % 2) * 6 - 3, cy - 36 + i * 17, 40 - i * 7, 8), col if i % 2 == 0 else CREAM_D)
def sparkles(I, pts, col=GOLD):
    for x, y, r in pts: I.layer(poly_mask(star_pts(x, y, r, r * .38, 4)), col)

def icon(n):
    I = Icon()
    # ================= 牧师 =================
    if n == 'Thaumaturgy': I.layer(palm_mask(64, 86, .85), SKIN); rays(I, 64, 36, 14, 30, 8, HOLY, 5); I.layer(ellipse_mask(64, 36, 12), SUNC)
    elif n == 'TollTheDead': bell(I, 56, 62, 1.1, STONE); [I.layer(arc_mask(56, 62, r, 300, 400, 6), NECRO) for r in (46, 58)]
    elif n == 'Resistance': I.layer(poly_mask(shield_pts(64, 64, 78, 92)), FORCE); I.layer(poly_mask(shield_pts(64, 64, 44, 54)), CREAM)
    elif n == 'HealingWord': bubble(I, 64, 56, 44, 32); I.layer(heart_mask(64, 54, .6), BLOOD)
    elif n == 'Command': I.layer(big_bust(50, 34), SLATE); I.layer(rrect_mask(90, 18, 104, 70, 4), GOLD); I.layer(ellipse_mask(97, 88, 8), GOLD)
    elif n == 'ShieldOfFaith': rays(I, 64, 64, 44, 58, 12, HOLY, 5); I.layer(poly_mask(shield_pts(64, 64, 70, 84)), GOLD); I.layer(poly_mask(star_pts(64, 60, 18, 7, 5)), CREAM)
    elif n == 'CreateWater': drop(I, 56, 62, 1.4); drop(I, 96, 40, .6); drop(I, 92, 86, .5)
    elif n == 'PurifyFood': I.layer(subtract(ellipse_mask(64, 74, 46, 30), rrect_mask(0, 0, 128, 70, 0)), BARK); I.layer(ellipse_mask(64, 70, 44, 8), OCHRE); sparkles(I, [(44, 40, 12), (78, 30, 15), (98, 52, 9)], HOLY)
    elif n == 'GuidingBolt': ray(I, 12, 116, 84, 44, HOLY, 12, glow=(210, 190, 120)); I.layer(poly_mask(star_pts(90, 38, 26, 10, 4)), SUNC)
    elif n == 'Aid': [I.layer(bust_mask(x, 52, .8), c) for x, c in ((30, SLATE), (98, SLATE), (64, CREAM_D))]; I.layer(heart_mask(64, 30, .55), BLOOD)
    elif n == 'SpiritualWeapon': hammer(I, 60, 66)
    elif n == 'PrayerOfHealing': I.layer(palm_mask(48, 78, .8, rot=15), SKIN); I.layer(palm_mask(80, 78, .8, rot=-15), SKIN); I.layer(heart_mask(64, 32, .6), BLOOD)
    elif n == 'Silence': bubble(I, 64, 58, 44, 32, CREAM_D); I.layer(arc_mask(64, 60, 46, 0, 360, 9), RUST); I.layer(line_mask([(32, 92), (96, 28)], 9), RUST)
    elif n == 'Revivify': I.layer(heart_mask(60, 66, 1.1), BLOOD); I.layer(bolt_mask(88, 40, .6), GOLD)
    elif n == 'SpiritGuardians': I.layer(big_bust(64, 40), SLATE); [I.layer(wing_mask(64 + dx, 50, side, .45), HOLY) for dx, side in ((-40, -1), (40, 1))]; I.layer(arc_mask(64, 64, 56, 200, 340, 6), HOLY)
    elif n == 'CreateFood': I.layer(ellipse_mask(64, 78, 46, 24), OCHRE); [I.layer(line_mask([(44 + i * 18, 70), (52 + i * 18, 62)], 4), KHAKI_D, shade=False) for i in range(3)]; [I.layer(line_mask([(50 + i * 14, 46), (46 + i * 14, 30), (52 + i * 14, 18)], 4), CREAM) for i in range(3)]
    elif n == 'MassHealingWord': [bubble(I, x, y, 24, 18) for x, y in ((40, 42), (90, 36), (66, 84))]; I.layer(heart_mask(66, 82, .35), BLOOD)
    elif n == 'Daylight': rays(I, 64, 64, 34, 60, 16, SUNC, 7); I.layer(ellipse_mask(64, 64, 30), SUNC); I.layer(ellipse_mask(56, 56, 10), CREAM, outline=False, shade=False)
    elif n == 'RemoveCurse': skull(I, 64, 52, .9, BONE); I.layer(line_mask([(20, 108), (108, 20)], 10), HOLY); rays(I, 64, 64, 50, 60, 10, HOLY, 4)
    elif n == 'FreedomOfMovement': I.layer(arc_mask(44, 70, 22, 200, 520, 9), STONE); I.layer(arc_mask(84, 58, 22, 20, 300, 9), STONE); I.layer(wing_mask(64, 50, 1, .7), CREAM)
    elif n == 'GuardianOfFaith': hood(I, 64, 54, HOLY_D); I.layer(ellipse_mask(64, 60, 10), SUNC, glow=(220, 200, 120)); I.layer(line_mask([(98, 110), (100, 20)], 7), GOLD)
    elif n == 'Divination': I.layer(almond(64, 70, 96, 50), BONE); I.layer(ellipse_mask(64, 70, 16), HOLY_D); sparkles(I, [(30, 26, 11), (64, 18, 13), (98, 26, 11)], HOLY)
    elif n == 'FlameStrike': I.layer(rrect_mask(48, 6, 80, 98, 8), (230, 196, 120), glow=(220, 150, 80)); I.layer(flame_mask(64, 92, 1.0), FIRE)
    elif n == 'GreaterRestoration': I.layer(heart_mask(64, 70, 1.0), BLOOD); I.layer(poly_mask(star_pts(64, 30, 26, 10, 8)), HOLY)
    elif n == 'RaiseDead': rays(I, 64, 56, 36, 58, 12, HOLY, 5); skull(I, 64, 52, 1.0)
    elif n == 'Hallow': circle_rune(I, 64, 70, 46, HOLY); I.layer(poly_mask(star_pts(64, 30, 18, 7, 4)), SUNC)
    elif n == 'BladeBarrier': [dagger(I, 64 + 36 * math.cos(a), 64 + 36 * math.sin(a), math.degrees(a) + 90, 40, CREAM) for a in [k * math.pi / 3 for k in range(6)]]
    elif n == 'HeroesFeast': goblet(I, 50, 60); I.layer(ellipse_mask(92, 84, 22, 14), RUST); I.layer(rrect_mask(104, 76, 120, 84, 3), BONE)
    elif n == 'Heal': I.layer(heart_mask(64, 64, 1.4), BLOOD); I.layer(rrect_mask(56, 36, 72, 84, 3), CREAM); I.layer(rrect_mask(40, 52, 88, 68, 3), CREAM)
    elif n == 'ConjureCelestial': I.layer(wing_mask(64, 56, -1, 1.0), CREAM); I.layer(wing_mask(64, 56, 1, 1.0), CREAM); I.layer(big_bust(64, 34), HOLY); I.layer(arc_mask(64, 20, 14, 180, 360, 5), SUNC)
    elif n == 'Resurrection': rays(I, 64, 100, 30, 64, 9, HOLY, 6, a0=math.pi); I.layer(big_bust(64, 34), CREAM_D)
    elif n == 'DivineWord': bubble(I, 64, 58, 46, 32, HOLY); I.layer(poly_mask(star_pts(64, 56, 20, 8, 8)), GOLD_D)
    elif n == 'HolyAura': I.layer(arc_mask(64, 30, 26, 0, 360, 7), SUNC, glow=(220, 200, 120)); I.layer(big_bust(64, 40), CREAM_D)
    elif n == 'DivineColumn': I.layer(rrect_mask(50, 0, 78, 96, 6), SUNC, glow=(220, 200, 120)); rays(I, 64, 100, 18, 40, 10, HOLY, 5)
    elif n == 'MassHeal9': [I.layer(heart_mask(x, y, .6), BLOOD) for x, y in ((40, 48), (88, 48), (64, 88))]; sparkles(I, [(64, 30, 14)], HOLY)
    elif n == 'TrueResurrection': I.layer(wing_mask(64, 60, -1, 1.1), EMBER); I.layer(wing_mask(64, 60, 1, 1.1), EMBER); I.layer(heart_mask(64, 66, .7), BLOOD); rays(I, 64, 66, 30, 40, 8, SUNC, 4)
    # ================= 德鲁伊 =================
    elif n == 'Druidcraft': I.layer(leaf_mask(30, 100, 80, 40, 26), NAT); I.layer(cloud_mask(86, 34, .55), CREAM_D)
    elif n == 'Shillelagh': I.layer(line_mask([(28, 104), (92, 28)], 14), BARK, glow=(110, 150, 90)); I.layer(ellipse_mask(92, 28, 14), BARK); I.layer(leaf_mask(60, 64, 84, 52, 10), MOSS)
    elif n == 'ProduceFlame': I.layer(palm_mask(60, 86, .9), SKIN); I.layer(flame_mask(64, 40, .8), FIRE)
    elif n == 'ThornWhip': pts = [(18 + i * 12, 100 - 70 * math.sin(i / 8 * math.pi * .9)) for i in range(9)]; I.layer(line_mask(pts, 8), MOSS_D); [I.layer(poly_mask([(x, y), (x + 6, y - 12), (x + 10, y + 2)]), MOSS) for x, y in pts[1:-1]]
    elif n == 'MoldEarth': I.layer(subtract(ellipse_mask(64, 96, 54, 40), rrect_mask(0, 96, 128, 128, 0)), BARK); I.layer(palm_mask(64, 44, .7, rot=180), SKIN)
    elif n == 'Goodberry': berries(I, 60, 70)
    elif n == 'Jump': I.layer(arc_mask(64, 100, 50, 190, 350, 6), CREAM); I.layer(bust_mask(96, 46, .8), SLATE); [I.layer(line_mask([(16 + i * 10, 104), (22 + i * 10, 96)], 4), CREAM_D) for i in range(3)]
    elif n == 'Moonbeam': crescent(I, 54, 36, 26, FROST); I.layer(poly_mask([(56, 54), (84, 54), (104, 116), (36, 116)]), (200, 214, 226), shade=False)
    elif n == 'SpikeGrowth': I.layer(rrect_mask(6, 96, 122, 116, 4), BARK); spikes(I, 82)
    elif n == 'FlamingSphere': I.layer(ellipse_mask(64, 70, 34), FIRE, glow=(220, 140, 70)); I.layer(flame_mask(64, 40, .6), EMBER)
    elif n == 'FindTraps': [I.layer(arc_mask(64, 78, 36, a0, a0 + 160, 7), STONE) for a0 in (190, 10)]; [I.layer(poly_mask([(34 + i * 15, 76), (40 + i * 15, 64), (46 + i * 15, 76)]), STONE) for i in range(4)]; I.layer(almond(64, 34, 56, 26), BONE); I.layer(ellipse_mask(64, 34, 8), NAT)
    elif n == 'AnimalMessenger': I.layer(wing_mask(56, 50, -1, .7), FUR); I.layer(wing_mask(56, 50, 1, .7), FUR); I.layer(ellipse_mask(56, 56, 12, 9), FUR); I.layer(rrect_mask(76, 76, 112, 100, 3), CREAM)
    elif n == 'WindWall': [I.layer(arc_mask(40 + i * 24, 64, 20, 200, 420, 6), CREAM if i % 2 else FROST) for i in range(3)]; I.layer(rrect_mask(10, 100, 118, 110, 3), STONE)
    elif n == 'StoneShape': I.layer(poly_mask([(30, 104), (36, 46), (64, 30), (94, 46), (98, 104)]), STONE); I.layer(palm_mask(92, 34, .55, rot=200), SKIN)
    elif n == 'ConjureWoodland': I.layer(tree_mask(46, 60, .9), MOSS); I.layer(bust_mask(92, 52, .8), NAT)
    elif n == 'DominateBeast': I.layer(wolf_mask(60, 66, 1.0), FUR); I.layer(almond(92, 30, 40, 18), PLUM); I.layer(ellipse_mask(92, 30, 6), INK, shade=False, outline=False)
    elif n == 'InsectPlague': I.layer(cloud_mask(64, 70, .9), (120, 112, 74)); [I.layer(union(ellipse_mask(18 + (i * 37) % 92, 18 + (i * 53) % 92, 9, 6), ellipse_mask(18 + (i * 37) % 92, 18 + (i * 53) % 92 - 6, 5, 4)), (82, 72, 46)) for i in range(11)]
    elif n == 'TreeStride': I.layer(tree_mask(34, 64, .7), MOSS); I.layer(tree_mask(96, 64, .7), MOSS); I.layer(line_mask([(46, 40), (86, 40)], 6), CREAM); I.layer(poly_mask([(86, 30), (100, 40), (86, 50)]), CREAM)
    elif n == 'Awaken': I.layer(paw_mask(60, 72, 1.1), FUR); I.layer(poly_mask(star_pts(96, 30, 18, 7, 4)), GOLD)
    elif n == 'Reincarnate': I.layer(arc_mask(64, 64, 42, 20, 300, 9), NAT); I.layer(poly_mask([(96, 26), (108, 50), (84, 46)]), NAT); I.layer(leaf_mask(50, 78, 76, 50, 16), MOSS)
    elif n == 'ControlEnvironment': rays(I, 44, 44, 22, 36, 10, SUNC, 5); I.layer(ellipse_mask(44, 44, 18), SUNC); I.layer(cloud_mask(80, 78, .8), CREAM_D)
    elif n == 'WindWalk': I.layer(cloud_mask(64, 74, 1.0), CREAM); I.layer(bust_mask(64, 28, .8), (190, 204, 214), shade=False)
    elif n == 'Whirlwind': funnel(I, 64, 64)
    elif n == 'Earthquake': I.layer(rrect_mask(6, 70, 122, 116, 4), BARK); I.layer(line_mask([(20, 70), (42, 92), (56, 78), (76, 104), (92, 84), (110, 112)], 6), INK, shade=False, outline=False); [I.layer(poly_mask([(x, 66), (x + 8, 50), (x + 16, 66)]), STONE) for x in (24, 70)]
    elif n == 'AnimalShapes': I.layer(bear_mask(46, 66, .8), FUR); I.layer(wolf_mask(88, 70, .8), SLATE)
    elif n == 'StormOfVengeance': I.layer(cloud_mask(64, 36, 1.1), DUSK); I.layer(bolt_mask(46, 84, .6), GOLD); I.layer(bolt_mask(84, 88, .5), GOLD)
    elif n == 'TreeOfLife': I.layer(tree_mask(64, 64, 1.2), MOSS, glow=(150, 200, 110)); sparkles(I, [(30, 30, 10), (100, 34, 9)], HOLY)
    # ================= 术士 =================
    elif n == 'MindSliver': I.layer(ellipse_mask(52, 70, 34, 30), PLUM); I.layer(poly_mask([(112, 18), (64, 66), (58, 60)]), FROST)
    elif n == 'MetaPierce': I.layer(poly_mask(star_pts(64, 64, 34, 14, 6)), ARC); arrow(I, 14, 112, 112, 16, CREAM, 6)
    elif n == 'DragonsBreath': I.layer(poly_mask([(20, 64), (112, 24), (112, 104)]), FIRE, glow=(220, 140, 70)); I.layer(ellipse_mask(22, 64, 14), RUST)
    elif n == 'MetaExtend': hourglass(I, 52, 64, .8); I.layer(rrect_mask(86, 52, 116, 64, 3), GOLD); I.layer(rrect_mask(95, 43, 107, 73, 3), GOLD)
    elif n == 'MetaQuicken': I.layer(arc_mask(64, 64, 44, 0, 360, 8), STONE); I.layer(bolt_mask(64, 64, .8), GOLD)
    elif n == 'BloodlineBreath': I.layer(poly_mask([(14, 64), (116, 12), (116, 116)]), EMBER, glow=(230, 160, 80)); I.layer(poly_mask([(30, 64), (106, 34), (106, 94)]), FIRE); [I.layer(poly_mask([(16 + i * 8, 54 + i * 4), (24 + i * 8, 58 + i * 4), (16 + i * 8, 62 + i * 4)]), RUST_D) for i in range(3)]
    elif n == 'PsychicBlast': I.layer(ellipse_mask(64, 64, 26, 24), PLUM); rays(I, 64, 64, 34, 58, 12, PLUM_D, 6)
    elif n == 'ElementalStorm': I.layer(arc_mask(64, 64, 48, 0, 360, 8), SLATE); I.layer(flame_mask(42, 70, .5), FIRE); I.layer(snow_mask(84, 46, 14, 4), ICE); I.layer(bolt_mask(78, 86, .4), GOLD)
    elif n == 'BloodlineAwakening': rays(I, 64, 64, 36, 60, 12, BLOOD, 6); I.layer(heart_mask(64, 66, 1.0), BLOOD); I.layer(poly_mask(star_pts(64, 62, 14, 6, 4)), GOLD)
    # ================= 邪术师 =================
    elif n == 'ChillTouch': I.layer(palm_mask(60, 70, 1.1), NECRO, glow=(110, 140, 100)); I.layer(snow_mask(96, 32, 16, 5), ICE)
    elif n == 'MinorIllusion': I.layer(big_bust(48, 32), (120, 120, 140), shade=False); I.layer(big_bust(80, 36), PLUM); sparkles(I, [(104, 22, 9)], CREAM)
    elif n == 'HellishRebuke': I.layer(poly_mask(shield_pts(52, 70, 58, 70)), SLATE); I.layer(flame_mask(90, 50, .8), FIRE)
    elif n == 'ArmsOfHadar': [tentacle(I, 64 + 40 * math.cos(a), 110, 18 * (1 if k % 2 else -1), DUSK_D, 9) for k, a in enumerate([math.pi * (0.5 + i) / 4 + math.pi for i in range(4)])]; I.layer(ellipse_mask(64, 60, 14), FROST)
    elif n == 'SpiderClimb': spider(I, 64, 64)
    elif n == 'HungerOfHadar': I.layer(ellipse_mask(64, 64, 52), (32, 28, 44)); [I.layer(poly_mask([(30 + i * 14, 54), (37 + i * 14, 70), (44 + i * 14, 54)]), BONE) for i in range(5)]; I.layer(ellipse_mask(64, 36, 8), PLUM)
    elif n == 'ContactOtherPlane': I.layer(poly_mask(star_pts(64, 64, 56, 24, 8)), DUSK); I.layer(almond(64, 64, 64, 30), PLUM); I.layer(ellipse_mask(64, 64, 9), GOLD)
    elif n == 'Dream': crescent(I, 54, 54, 34, PLUM); sparkles(I, [(96, 30, 10), (100, 70, 7)], CREAM)
    elif n == 'Eyebite': I.layer(almond(64, 64, 100, 54), DUSK); I.layer(ellipse_mask(64, 64, 18), PLUM, glow=(150, 110, 170)); I.layer(ellipse_mask(64, 64, 7), INK, shade=False, outline=False)
    elif n == 'SummonFiend': I.layer(big_bust(64, 36), BLOOD); horns(I, 64, 26); circle_rune(I, 64, 104, 22, FIRE, 5)
    elif n == 'PowerWordStun': bubble(I, 60, 56, 44, 32, CREAM); I.layer(bolt_mask(60, 56, .55), GOLD)
    # ================= 吟游诗人 =================
    elif n == 'Friends': I.layer(bust_mask(42, 46, 1.0), SLATE); I.layer(bust_mask(86, 46, 1.0), CREAM_D); I.layer(heart_mask(64, 26, .45), BLOOD)
    elif n == 'Heroism': I.layer(line_mask([(36, 114), (36, 14)], 7), BARK); I.layer(poly_mask([(40, 16), (104, 28), (84, 46), (104, 64), (40, 60)]), RUST); I.layer(poly_mask(star_pts(64, 40, 12, 5, 5)), GOLD)
    elif n == 'Tongues': book(I, 54, 76, .8); bubble(I, 92, 32, 22, 16, CREAM); bubble(I, 54, 26, 16, 12, GOLD)
    elif n == 'HypnoticPattern': spiral(I, 64, 64, PLUM, 4)
    elif n == 'Compulsion': I.layer(bust_mask(64, 50, .9), SLATE); [I.layer(line_mask([(64 + 30 * math.cos(a), 64 + 30 * math.sin(a)), (64 + 54 * math.cos(a), 64 + 54 * math.sin(a))], 6), GOLD) for a in (0.3, 2.8, 4.7)]
    elif n == 'GreaterInvisibility': I.layer(big_bust(64, 30), (124, 130, 146), shade=False); sparkles(I, [(34, 30, 12), (96, 44, 14), (78, 96, 10)], FORCE)
    elif n == 'MassSuggestion': [I.layer(bust_mask(x, 48, .7), c) for x, c in ((28, SLATE), (52, SLATE_D), (76, SLATE))]; I.layer(line_mask([(84, 100), (112, 100)], 7), GOLD); I.layer(poly_mask([(110, 88), (124, 100), (110, 112)]), GOLD)
    elif n == 'OttoDance': I.layer(bust_mask(50, 44, .9), SLATE); note(I, 94, 52)
    elif n == 'ProjectImage': I.layer(big_bust(42, 32), SLATE); I.layer(bust_mask(94, 40, 1.0), (130, 140, 160), shade=False); I.layer(line_mask([(56, 40), (82, 40)], 4), FORCE, shade=False)
    elif n == 'PowerWordHeal': bubble(I, 60, 56, 46, 34, HOLY); I.layer(heart_mask(60, 54, .7), BLOOD)
    # ================= 圣武士 =================
    elif n == 'SmiteBlaze': sword(I); I.layer(flame_mask(94, 34, .6), FIRE)
    elif n == 'SmiteThunder': sword(I); [I.layer(arc_mask(96, 30, r, 0, 360, 5), FORCE) for r in (16, 28)]
    elif n == 'SmiteSear': sword(I); [I.layer(ellipse_mask(x, y, 6), EMBER) for x, y in ((88, 20), (106, 40), (100, 22))]
    elif n == 'DetectEvil': I.layer(almond(64, 50, 96, 50), BONE); I.layer(ellipse_mask(64, 50, 16), HOLY_D); skull(I, 64, 94, .5, NECRO)
    elif n == 'MagicWeapon': sword(I, col=FORCE); sparkles(I, [(98, 24, 12), (110, 54, 8), (76, 18, 7)], ARC)
    elif n == 'FindSteed': horse(I, 64, 70, CREAM); I.layer(arc_mask(64, 16, 12, 180, 360, 4), SUNC)
    elif n == 'SmiteBrand': sword(I); I.layer(poly_mask(star_pts(100, 30, 18, 7, 6)), SUNC)
    elif n == 'CrusadersMantle': I.layer(poly_mask([(34, 24), (94, 24), (112, 112), (16, 112)]), RUST); I.layer(rrect_mask(56, 40, 72, 96, 3), CREAM); I.layer(rrect_mask(40, 56, 88, 70, 3), CREAM)
    elif n == 'ElementalWeapon': sword(I); I.layer(flame_mask(100, 26, .45), FIRE); I.layer(snow_mask(76, 20, 10, 3), ICE)
    elif n == 'SmiteBlind': sword(I); I.layer(almond(96, 30, 40, 20), BONE); I.layer(line_mask([(78, 44), (114, 16)], 5), RUST)
    elif n == 'SmiteStagger': sword(I); spiral(I, 98, 30, PLUM, 2)
    elif n == 'SmiteBanish': sword(I); I.layer(arc_mask(98, 32, 20, 0, 360, 7), ARC); I.layer(ellipse_mask(98, 32, 10), DUSK_D)
    # ================= 游侠 =================
    elif n == 'Ensnaring': arrow(I, 14, 112, 100, 26, BONE, 6); pts = [(30 + i * 10, 70 + 14 * math.sin(i)) for i in range(8)]; I.layer(line_mask(pts, 5), MOSS)
    elif n == 'PassWithoutTrace': [footprint(I, x, y, s, c) for x, y, s, c in ((34, 98, 1.9, KHAKI_D), (66, 70, 1.6, KHAKI), (94, 40, 1.3, (170, 168, 150)))]
    elif n == 'ConjureBarrage': [arrow(I, 24, 104, 24 + 90 * math.cos(a), 104 - 90 * math.sin(a), BONE, 5) for a in (0.35, 0.75, 1.15)]
    elif n == 'LightningArrow': arrow(I, 14, 112, 104, 22, BONE, 6); I.layer(bolt_mask(92, 38, .5), GOLD)
    elif n == 'SwiftQuiver': I.layer(poly_mask([(40, 44), (76, 30), (96, 104), (60, 116)]), BARK); [arrow(I, 54 + i * 10, 52 - i * 4, 44 + i * 12, 10, BONE, 4) for i in range(3)]
    elif n == 'ConjureVolley': [arrow(I, 20 + i * 22, 8, 26 + i * 22, 96, BONE, 4) for i in range(5)]; I.layer(rrect_mask(8, 104, 120, 114, 3), BARK)
    # ================= 器械师 =================
    elif n == 'AbsorbElements': I.layer(poly_mask(shield_pts(64, 66, 76, 90)), FORCE); I.layer(flame_mask(64, 58, .5), FIRE); I.layer(arc_mask(64, 66, 54, 210, 330, 6), CREAM)
    elif n == 'Identify': I.layer(poly_mask([(48, 64), (64, 44), (80, 64), (64, 88)]), ARC); magnifier(I, 58, 58)
    elif n == 'HeatMetal': I.layer(poly_mask([(20, 86), (36, 66), (96, 66), (112, 86)]), (196, 92, 60), glow=(220, 120, 70)); I.layer(rrect_mask(40, 86, 92, 100, 3), STONE); rays(I, 64, 56, 10, 30, 5, EMBER, 4, a0=math.pi)
    elif n == 'Levitate': I.layer(big_bust(64, 26), SLATE); [I.layer(arc_mask(64, 116, r, 220, 320, 5), FORCE) for r in (18, 30)]
    elif n == 'Creation': gear(I, 44, 70, 28); I.layer(rrect_mask(68, 64, 108, 100, 4), STONE); I.layer(poly_mask(star_pts(92, 30, 20, 7, 4)), GOLD)
    elif n == 'AnimateObjects': I.layer(rrect_mask(30, 30, 98, 100, 8), STONE); [I.layer(ellipse_mask(64 + dx, 58, 7), ARC, glow=(150, 130, 190)) for dx in (-14, 14)]; I.layer(rrect_mask(48, 78, 80, 84, 2), STONE_D)
    elif n == 'BigbysHand': I.layer(palm_mask(64, 66, 1.45), FORCE, glow=(130, 160, 200))
    else: raise KeyError(n)
    return I.done()

def names_from_defs():
    out = []
    for f in ('CasterSpells.xml', 'CasterSpells2.xml'):
        txt = open(os.path.join(ROOT, 'Defs', f), encoding='utf-8').read()
        for m in re.finditer(r'<AbilityDef[^>]*>\s*<defName>LP_CS_(\w+)</defName>\s*<label>([^<]+)</label>', txt): out.append((m.group(1), m.group(2)))
    return out

def sheet(items):
    cols = 12; cell = 112; pad = 22; rows = (len(items) + cols - 1) // cols
    im = Image.new('RGBA', (cols * cell, rows * (cell + pad)), (44, 46, 52, 255)); d = ImageDraw.Draw(im)
    font = None
    for fp in (r'C:\Windows\Fonts\msyh.ttc', r'C:\Windows\Fonts\simhei.ttf'):
        if os.path.exists(fp): font = ImageFont.truetype(fp, 14); break
    for i, (n, label) in enumerate(items):
        x = (i % cols) * cell; y = (i // cols) * (cell + pad)
        ic = Image.open(os.path.join(OUT, n + '.png')).resize((96, 96), Image.LANCZOS); im.paste(ic, (x + 8, y + 4), ic)
        d.text((x + cell / 2, y + cell + 4), label, fill=(225, 225, 225), font=font, anchor='mt')
    return im

if __name__ == '__main__':
    os.makedirs(OUT, exist_ok=True)
    only = sys.argv[2:]; items = names_from_defs()
    for n, _ in items:
        if only and n not in only: continue
        icon(n).save(os.path.join(OUT, n + '.png'))
    sheet(items).save(os.path.join(ROOT, 'ArtSource', 'CasterSpells-1.131.0-icons.png'))
    print(len(items), 'icons ->', OUT)
