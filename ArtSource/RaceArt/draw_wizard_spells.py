# 1.129.0 法师法术图标（1.128.0 的 38 个 + 1.129.0 的 51 个）：RimWorld / VPE 画风（粗黑描边、平涂 + 右下暗部、低饱和），
# 沿用 draw_class_art / draw_class_art2 / draw_classes3 的形状和配色。128×128。
# 用法：python draw_wizard_spells.py <模组根目录>   → Textures/LP/Classes/WizardSpells/<名字>.png
import math, sys, os
from PIL import Image, ImageDraw
from draw_class_art import (Icon, poly_mask, ellipse_mask, line_mask, arc_mask, rrect_mask, union, subtract, star_pts, bust_mask, shield_pts, sword_masks,
                            CREAM, CREAM_D, KHAKI, KHAKI_D, SLATE, SLATE_D, RUST, RUST_D, VIOLET, VIOLET_D, GOLD, GOLD_D, BONE, BONE_D, STONE, STONE_D, INK)
from draw_class_art2 import flame_mask, cloud_mask, bolt_mask, fist_mask, palm_mask, paw_mask, wolf_mask, bear_mask, heart_mask, eye_mask, snow_mask, wing_mask, tree_mask, rays, MOSS, MOSS_D, BARK, FUR, PLUM, PLUM_D, ICE, OCHRE, SKIN
from draw_classes3 import axe, arrow, dagger, hood, DUSK, DUSK_D, POISON, BLOOD

ROOT = sys.argv[1] if len(sys.argv) > 1 else '.'
OUT = os.path.join(ROOT, 'Textures', 'LP', 'Classes', 'WizardSpells')
ARC = (150, 132, 186)          # 奥术紫（低饱和）
FIRE = (206, 112, 58); EMBER = (226, 170, 80)
FROST = (168, 198, 214)
NECRO = (120, 150, 104); NECRO_D = (82, 104, 70)
SUNC = (232, 206, 120)
FORCE = (140, 178, 210)
WATER = (104, 140, 170)

def skull(I, cx, cy, s=1.0, col=BONE):
    I.layer(union(ellipse_mask(cx, cy, 20 * s, 18 * s), rrect_mask(cx - 12 * s, cy + 8 * s, cx + 12 * s, cy + 24 * s, 4 * s)), col)
    for dx in (-8, 8): I.layer(ellipse_mask(cx + dx * s, cy + 2 * s, 5 * s, 6 * s), INK, shade=False, outline=False)
    I.layer(poly_mask([(cx, cy + 8 * s), (cx - 3 * s, cy + 14 * s), (cx + 3 * s, cy + 14 * s)]), INK, shade=False, outline=False)
def rabbit(I, cx, cy, s=1.0, col=BONE):
    I.layer(union(ellipse_mask(cx, cy + 10 * s, 20 * s, 16 * s), ellipse_mask(cx + 14 * s, cy - 4 * s, 11 * s),
                  ellipse_mask(cx + 10 * s, cy - 24 * s, 4.5 * s, 14 * s), ellipse_mask(cx + 20 * s, cy - 22 * s, 4.5 * s, 13 * s)), col)
    I.layer(ellipse_mask(cx + 18 * s, cy - 6 * s, 2.5 * s), INK, shade=False, outline=False)
def book(I, cx, cy, s=1.0, col=RUST):
    I.layer(poly_mask([(cx - 34 * s, cy - 22 * s), (cx + 34 * s, cy - 28 * s), (cx + 36 * s, cy + 24 * s), (cx - 32 * s, cy + 30 * s)]), col)
    I.layer(poly_mask([(cx - 26 * s, cy - 16 * s), (cx + 28 * s, cy - 21 * s), (cx + 30 * s, cy + 18 * s), (cx - 24 * s, cy + 23 * s)]), BONE)
def circle_rune(I, cx, cy, r, col=ARC, w=7):
    I.layer(arc_mask(cx, cy, r, 0, 360, w), col)
    I.layer(poly_mask(star_pts(cx, cy, r * 0.8, r * 0.34, 5)), col)
def door(I, cx, cy, s=1.0, col=BARK):
    I.layer(union(rrect_mask(cx - 22 * s, cy - 18 * s, cx + 22 * s, cy + 40 * s, 3 * s), ellipse_mask(cx, cy - 18 * s, 22 * s)), col)
    I.layer(ellipse_mask(cx + 12 * s, cy + 12 * s, 3.5 * s), GOLD)
def hourglass(I, cx, cy, s=1.0):
    I.layer(rrect_mask(cx - 26 * s, cy - 44 * s, cx + 26 * s, cy - 36 * s, 2), BARK)
    I.layer(rrect_mask(cx - 26 * s, cy + 36 * s, cx + 26 * s, cy + 44 * s, 2), BARK)
    I.layer(poly_mask([(cx - 20 * s, cy - 36 * s), (cx + 20 * s, cy - 36 * s), (cx + 3 * s, cy), (cx + 20 * s, cy + 36 * s), (cx - 20 * s, cy + 36 * s), (cx - 3 * s, cy)]), ICE)
    I.layer(poly_mask([(cx - 12 * s, cy + 34 * s), (cx + 12 * s, cy + 34 * s), (cx, cy + 18 * s)]), OCHRE, outline=False)
def tentacle(I, x0, y0, bend, col=DUSK_D, w=10):
    pts = [(x0 + bend * math.sin(t * 2.6) * (t * 0.8), y0 - t * 70) for t in [i / 8 for i in range(9)]]
    I.layer(line_mask(pts, w), col)
def wall(I, y, col, n=4):
    for i in range(n): I.layer(rrect_mask(10 + i * 27, y, 34 + i * 27, y + 22, 3), col)
def ray(I, x0, y0, x1, y1, col, w=10, glow=None):
    I.layer(line_mask([(x0, y0), (x1, y1)], w), col, glow=glow)
def dog(I, cx, cy, s=1.0, col=FUR):
    I.layer(wolf_mask(cx, cy, s), col)
def almond(cx, cy, w, h):
    top = [(cx - w / 2 + w * i / 16, cy - h / 2 * math.sin(math.pi * i / 16)) for i in range(17)]
    bot = [(cx + w / 2 - w * i / 16, cy + h / 2 * math.sin(math.pi * i / 16)) for i in range(17)]
    return poly_mask(top + bot)
def big_bust(cx, cy, s=1.5):
    return bust_mask(cx, cy, s)
def gear(I, cx, cy, r, col=STONE):
    I.layer(poly_mask(star_pts(cx, cy, r, r * 0.78, 8)), col); I.layer(ellipse_mask(cx, cy, r * 0.35), INK, shade=False, outline=False)

def icon(n):
    I = Icon()
    # ---------- 戏法 ----------
    if n == 'FireBolt': I.layer(flame_mask(80, 48, 0.9), FIRE); ray(I, 14, 112, 66, 64, EMBER, 12)
    elif n == 'RayOfFrost': ray(I, 12, 116, 86, 42, FROST, 12); I.layer(snow_mask(88, 40, 26, 6), ICE)
    elif n == 'ShockingGrasp': I.layer(palm_mask(56, 70, 1.0), SKIN); I.layer(bolt_mask(86, 40, 0.7), GOLD)
    elif n == 'PoisonSpray': I.layer(palm_mask(36, 80, 0.8), SKIN); [I.layer(ellipse_mask(70 + i * 12, 60 - i * 8 + (i % 2) * 8, 10 - i), POISON) for i in range(4)]
    elif n == 'Mending': gear(I, 54, 70, 32); I.layer(poly_mask(star_pts(92, 34, 20, 7, 4)), ARC)
    elif n == 'Prestidigitation': I.layer(palm_mask(64, 84, 0.9, rot=0), SKIN); [I.layer(poly_mask(star_pts(x, y, 11, 4, 4)), c) for x, y, c in ((38, 30, GOLD), (70, 18, ARC), (98, 36, CREAM))]
    elif n == 'Light': rays(I, 64, 60, 30, 52, 10, SUNC, 6); I.layer(ellipse_mask(64, 60, 24), SUNC); I.layer(ellipse_mask(58, 54, 8), CREAM, outline=False, shade=False)
    elif n == 'MageHand': I.layer(palm_mask(64, 66, 1.2), ARC, glow=(120, 100, 170))
    # ---------- 1 环 ----------
    elif n == 'FogCloud': I.layer(cloud_mask(50, 70, 1.0), SLATE); I.layer(cloud_mask(80, 52, 0.8), CREAM_D)
    elif n == 'Grease': I.layer(ellipse_mask(64, 92, 52, 16), (90, 80, 52)); I.layer(poly_mask([(64, 18), (82, 50), (64, 64), (46, 50)]), OCHRE)
    elif n == 'Thunderwave': [I.layer(arc_mask(64, 64, r, 0, 360, 9), c) for r, c in ((52, SLATE), (36, CREAM), (20, FORCE))]
    elif n == 'FalseLife': I.layer(heart_mask(64, 64, 1.1), NECRO); I.layer(arc_mask(64, 64, 52, 200, 340, 8), BONE)
    elif n == 'FindFamiliar': circle_rune(I, 64, 86, 34, ARC); I.layer(paw_mask(64, 46, 1.0), FUR)
    elif n == 'Alarm': I.layer(poly_mask([(40, 80), (44, 40), (64, 28), (84, 40), (88, 80)]), GOLD); I.layer(ellipse_mask(64, 90, 8), GOLD_D); [I.layer(arc_mask(64, 56, r, 300, 420, 6), CREAM) for r in (50,)]
    elif n == 'ComprehendLanguages': book(I, 64, 70); I.layer(ellipse_mask(92, 30, 18, 12), CREAM)
    elif n == 'DetectMagic': I.layer(almond(64, 64, 100, 54), BONE); I.layer(ellipse_mask(64, 64, 17), ARC); rays(I, 64, 64, 50, 60, 8, ARC, 4)
    # ---------- 2 环 ----------
    elif n == 'ScorchingRay': [ray(I, 14, 110, x, y, FIRE, 8) for x, y in ((70, 20), (100, 40), (112, 76))]; I.layer(ellipse_mask(16, 108, 12), EMBER)
    elif n == 'Shatter': [I.layer(poly_mask([(64, 64), (64 + 50 * math.cos(a), 64 + 50 * math.sin(a)), (64 + 34 * math.cos(a + .4), 64 + 34 * math.sin(a + .4))]), CREAM if i % 2 else SLATE) for i, a in enumerate([k * math.pi / 3 for k in range(6)])]
    elif n == 'Web': [I.layer(line_mask([(64, 64), (64 + 54 * math.cos(a), 64 + 54 * math.sin(a))], 4), BONE) for a in [k * math.pi / 4 for k in range(8)]]; [I.layer(arc_mask(64, 64, r, 0, 360, 4), BONE) for r in (16, 32, 48)]
    elif n == 'HoldPerson': I.layer(big_bust(64, 30), SLATE); [I.layer(line_mask([(16, y), (112, y + 6)], 8), GOLD) for y in (64, 88)]
    elif n == 'Invisibility': I.layer(big_bust(64, 30), (110, 116, 130), shade=False); I.layer(subtract(big_bust(64, 30), rrect_mask(0, 64, 128, 128, 0)), FORCE)
    elif n == 'Darkness': I.layer(ellipse_mask(64, 64, 50), DUSK_D); I.layer(ellipse_mask(80, 52, 40), (30, 28, 40), outline=False, shade=False)
    elif n == 'MindSpike': I.layer(ellipse_mask(56, 70, 34, 30), PLUM); I.layer(poly_mask([(108, 14), (66, 66), (56, 58)]), CREAM)
    elif n == 'EnhanceAbility': I.layer(fist_mask(56, 70, 1.0), SKIN); I.layer(poly_mask([(96, 16), (114, 40), (104, 40), (104, 58), (88, 58), (88, 40), (78, 40)]), GOLD)
    elif n == 'ArcaneLock': I.layer(arc_mask(64, 48, 22, 180, 360, 10), STONE); I.layer(rrect_mask(34, 48, 94, 104, 6), GOLD); I.layer(poly_mask(star_pts(64, 76, 14, 6, 5)), ARC)
    elif n == 'Knock': door(I, 54, 58); I.layer(fist_mask(96, 62, 0.6), SKIN); [I.layer(line_mask([(98, 30 + i * 10), (114, 26 + i * 12)], 4), CREAM) for i in range(2)]
    # ---------- 3 环 ----------
    elif n == 'DispelMagic': I.layer(poly_mask(star_pts(64, 64, 50, 24, 8)), ARC); I.layer(line_mask([(24, 104), (104, 24)], 12), CREAM)
    elif n == 'Fly': I.layer(wing_mask(64, 60, -1, 1.0), CREAM); I.layer(wing_mask(64, 60, 1, 1.0), CREAM); I.layer(ellipse_mask(64, 64, 10), ARC)
    elif n == 'Fear': hood(I, 64, 56, DUSK); [I.layer(ellipse_mask(64 + dx, 60, 5), (220, 80, 60), shade=False) for dx in (-10, 10)]
    elif n == 'Slow': hourglass(I, 64, 64); I.layer(arc_mask(64, 64, 54, 220, 320, 6), SLATE)
    elif n == 'StinkingCloud': I.layer(cloud_mask(64, 64, 1.1), POISON); [I.layer(line_mask([(40 + i * 24, 90), (36 + i * 24, 110)], 5), MOSS_D) for i in range(3)]
    elif n == 'VampiricTouch': I.layer(palm_mask(56, 70, 1.0), NECRO); I.layer(poly_mask([(96, 24), (106, 44), (96, 50), (86, 44)]), BLOOD)
    elif n == 'AnimateDead': skull(I, 64, 52); [I.layer(line_mask([(40 + i * 24, 108), (44 + i * 24, 86)], 7), BONE) for i in range(3)]
    elif n == 'Blink': I.layer(bust_mask(46, 34, 1.3), SLATE); I.layer(bust_mask(86, 34, 1.3), FORCE, shade=False)
    elif n == 'ProtectionFromEnergy': I.layer(poly_mask(shield_pts(64, 64, 82, 98)), FORCE); I.layer(flame_mask(52, 60, 0.45), FIRE); I.layer(snow_mask(78, 72, 13, 4), ICE)
    elif n == 'MagicCircle': circle_rune(I, 64, 64, 50, GOLD)
    elif n == 'SpeakWithDead': skull(I, 50, 64); I.layer(ellipse_mask(94, 34, 20, 14), CREAM)
    elif n == 'Sending': I.layer(poly_mask([(16, 60), (110, 22), (80, 108), (62, 74)]), CREAM); I.layer(poly_mask([(62, 74), (110, 22), (70, 88)]), CREAM_D)
    elif n == 'Counterspell': I.layer(poly_mask(star_pts(64, 64, 46, 22, 6)), ARC); I.layer(arc_mask(64, 64, 52, 0, 360, 9), RUST); I.layer(line_mask([(28, 100), (100, 28)], 10), RUST)
    # ---------- 4 环 ----------
    elif n == 'Polymorph': rabbit(I, 52, 72, 1.1); I.layer(poly_mask(star_pts(100, 28, 18, 7, 4)), ARC)
    elif n == 'Stoneskin': I.layer(big_bust(64, 30), STONE); [I.layer(line_mask(pts, 3), STONE_D, outline=False, shade=False) for pts in (((52, 70), (60, 84), (54, 98)), ((74, 64), (70, 80), (80, 94)))]
    elif n == 'WallOfFire': wall(I, 96, STONE); [I.layer(flame_mask(24 + i * 27, 66, 0.55), FIRE) for i in range(4)]
    elif n == 'BlackTentacles': [tentacle(I, x, 116, b) for x, b in ((30, 10), (58, -12), (86, 14), (108, -8))]
    elif n == 'Banishment': circle_rune(I, 64, 92, 30, ARC); I.layer(bust_mask(64, 30, 0.7), (110, 116, 130), shade=False); I.layer(poly_mask([(64, 2), (78, 20), (50, 20)]), CREAM)
    elif n == 'Confusion': [I.layer(arc_mask(64, 64, r, a, a + 250, 8), c) for r, a, c in ((48, 0, PLUM), (32, 120, CREAM), (16, 240, PLUM_D))]
    elif n == 'PhantasmalKiller': hood(I, 64, 52, DUSK_D); dagger(I, 96, 82, -60)
    elif n == 'ArcaneEye': I.layer(almond(64, 64, 104, 60), ARC, glow=(120, 100, 170)); I.layer(ellipse_mask(64, 64, 15), INK, outline=False, shade=False)
    elif n == 'Fabricate': gear(I, 44, 78, 28, STONE); gear(I, 86, 52, 22, KHAKI); I.layer(poly_mask(star_pts(96, 98, 14, 5, 4)), ARC)
    elif n == 'FaithfulHound': dog(I, 64, 70, 1.0, (150, 160, 180)); I.layer(arc_mask(64, 64, 54, 200, 340, 6), FORCE)
    elif n == 'Blight': I.layer(tree_mask(64, 60, 1.0), (110, 96, 70)); skull(I, 96, 96, 0.6)
    # ---------- 5 环 ----------
    elif n == 'ConeOfCold': I.layer(poly_mask([(14, 64), (116, 14), (116, 114)]), FROST); I.layer(snow_mask(88, 64, 20, 5), CREAM)
    elif n == 'DominatePerson': I.layer(bust_mask(64, 50, 1.3), SLATE); [I.layer(line_mask([(64, 36), (x, 6)], 4), PLUM) for x in (40, 64, 88)]; I.layer(ellipse_mask(64, 6, 7), PLUM)
    elif n == 'HoldMonster': I.layer(wolf_mask(64, 60, 1.0), FUR); [I.layer(line_mask([(14, y), (114, y - 8)], 7), GOLD) for y in (66, 90)]
    elif n == 'Telekinesis': I.layer(rrect_mask(46, 20, 82, 56, 4), BARK); [I.layer(arc_mask(64, 86, r, 200, 340, 6), ARC) for r in (20, 34)]; I.layer(palm_mask(64, 104, 0.7), SKIN)
    elif n == 'ConjureElemental': I.layer(flame_mask(42, 56, 0.6), FIRE); I.layer(cloud_mask(88, 44, 0.6), CREAM); I.layer(ellipse_mask(42, 96, 18, 12), WATER); I.layer(poly_mask([(76, 108), (90, 80), (104, 108)]), STONE)
    elif n == 'TeleportationCircle': circle_rune(I, 64, 80, 44, FORCE); I.layer(poly_mask([(64, 8), (84, 34), (72, 34), (72, 52), (56, 52), (56, 34), (44, 34)]), CREAM)
    elif n == 'ModifyMemory': I.layer(ellipse_mask(64, 60, 40, 34), PLUM); I.layer(line_mask([(36, 60), (52, 50), (70, 66), (92, 52)], 6), CREAM); I.layer(line_mask([(84, 100), (112, 72)], 8), RUST)
    elif n == 'Scrying': I.layer(ellipse_mask(64, 56, 36), (150, 180, 200)); I.layer(rrect_mask(36, 92, 92, 108, 4), BARK); I.layer(almond(64, 56, 46, 26), ARC, outline=False)
    elif n == 'LegendLore': book(I, 64, 72, 1.0, (120, 70, 60)); I.layer(poly_mask(star_pts(64, 70, 18, 8, 5)), GOLD)
    # ---------- 6 环 ----------
    elif n == 'ChainLightning': I.layer(bolt_mask(40, 50, 0.8), GOLD); I.layer(bolt_mask(88, 74, 0.7), GOLD); I.layer(line_mask([(46, 70), (84, 52)], 5), CREAM)
    elif n == 'WallOfForce': [I.layer(rrect_mask(12 + i * 36, 18, 40 + i * 36, 110, 4), FORCE, glow=(120, 160, 200)) for i in range(3)]
    elif n == 'TrueSeeing': I.layer(almond(64, 64, 108, 58), BONE); I.layer(ellipse_mask(64, 64, 18), GOLD); rays(I, 64, 64, 54, 62, 12, GOLD, 4)
    elif n == 'CircleOfDeath': I.layer(arc_mask(64, 64, 48, 0, 360, 12), NECRO_D); skull(I, 64, 58, 0.9)
    elif n == 'FleshToStone': I.layer(big_bust(64, 30), STONE); I.layer(subtract(big_bust(64, 30), rrect_mask(0, 0, 128, 66, 0)), SKIN, shade=False)
    elif n == 'MoveEarth': I.layer(poly_mask([(10, 110), (44, 40), (66, 72), (88, 30), (118, 110)]), STONE); I.layer(poly_mask([(84, 30), (100, 8), (110, 30)]), CREAM)
    elif n == 'Contingency': hourglass(I, 48, 64, 0.8); I.layer(poly_mask(shield_pts(94, 70, 40, 50)), FORCE)
    elif n == 'Sunbeam': ray(I, 64, 4, 64, 124, SUNC, 26, glow=(230, 200, 110)); rays(I, 64, 20, 18, 28, 8, CREAM, 4)
    elif n == 'CreateUndead': skull(I, 40, 50, 0.7); skull(I, 88, 50, 0.7); skull(I, 64, 90, 0.8)
    # ---------- 7 环 ----------
    elif n == 'DelayedBlastFireball': I.layer(ellipse_mask(64, 68, 34), FIRE, glow=(220, 130, 60)); I.layer(ellipse_mask(56, 60, 12), EMBER, outline=False); I.layer(line_mask([(86, 40), (104, 18)], 5), BONE)
    elif n == 'DimensionDoor': door(I, 40, 56, 0.9, ARC); door(I, 92, 56, 0.9, FORCE); I.layer(line_mask([(54, 108), (78, 108)], 6), CREAM)
    elif n == 'FingerOfDeath': I.layer(palm_mask(40, 80, 0.8), NECRO); ray(I, 56, 64, 112, 18, NECRO_D, 10); skull(I, 104, 26, 0.5)
    elif n == 'ReverseGravity': I.layer(bust_mask(64, 74, 0.8).rotate(180, center=(64 * 4, 84 * 4)), SLATE); [I.layer(poly_mask([(x, 12), (x + 10, 28), (x - 10, 28)]), CREAM) for x in (30, 64, 98)]
    elif n == 'Etherealness': I.layer(big_bust(64, 30), (170, 180, 210), shade=False, glow=(140, 150, 200))
    elif n == 'PlaneShift': I.layer(arc_mask(64, 64, 46, 0, 360, 10), ARC); I.layer(arc_mask(64, 64, 28, 90, 330, 8), FORCE); I.layer(poly_mask(star_pts(64, 64, 10, 4, 4)), CREAM)
    elif n == 'MagnificentMansion': I.layer(poly_mask([(20, 60), (64, 20), (108, 60)]), RUST); I.layer(rrect_mask(30, 58, 98, 108, 2), BONE); door(I, 64, 90, 0.4, BARK)
    # ---------- 8 环 ----------
    elif n == 'ForceCage': [I.layer(line_mask([(x, 20), (x, 108)], 7), FORCE) for x in (28, 50, 78, 100)]; [I.layer(line_mask([(20, y), (108, y)], 7), FORCE) for y in (20, 108)]; I.layer(bust_mask(64, 50, 0.6), SLATE)
    elif n == 'Maze': [I.layer(rrect_mask(64 - r, 64 - r, 64 + r, 64 + r, 3), c, shade=False) for r, c in ((52, STONE), (38, BONE), (24, STONE), (10, ARC))]
    elif n == 'DominateMonster': I.layer(bear_mask(64, 66, 0.9), FUR); [I.layer(line_mask([(64, 40), (x, 6)], 4), PLUM) for x in (36, 64, 92)]
    elif n == 'Feeblemind': I.layer(ellipse_mask(64, 60, 42, 36), PLUM); [I.layer(line_mask(pts, 5), INK, outline=False, shade=False) for pts in (((40, 40), (56, 64), (44, 84)), ((80, 36), (72, 62), (88, 86)))]
    elif n == 'MindBlank': I.layer(ellipse_mask(64, 64, 44, 38), PLUM_D); I.layer(arc_mask(64, 64, 54, 0, 360, 10), FORCE)
    elif n == 'Antipathy': circle_rune(I, 64, 64, 50, RUST); [I.layer(poly_mask([(64 + 56 * math.cos(a), 64 + 56 * math.sin(a)), (64 + 40 * math.cos(a + .25), 64 + 40 * math.sin(a + .25)), (64 + 40 * math.cos(a - .25), 64 + 40 * math.sin(a - .25))]), CREAM) for a in (0, math.pi / 2, math.pi, 3 * math.pi / 2)]
    elif n == 'Sunburst': rays(I, 64, 64, 24, 60, 16, SUNC, 7); I.layer(ellipse_mask(64, 64, 26), SUNC, glow=(230, 200, 110))
    elif n == 'Clone': I.layer(bust_mask(42, 36, 1.2), SLATE); I.layer(bust_mask(86, 36, 1.2), (130, 170, 140)); I.layer(rrect_mask(66, 18, 106, 112, 10), (170, 200, 190), shade=False)
    # ---------- 9 环 ----------
    elif n == 'TimeStop': hourglass(I, 64, 64); I.layer(line_mask([(16, 112), (112, 16)], 9), ARC)
    elif n == 'Wish': I.layer(poly_mask(star_pts(64, 60, 50, 20, 5)), GOLD, glow=(220, 190, 110)); [I.layer(poly_mask(star_pts(x, y, 10, 4, 4)), CREAM) for x, y in ((20, 104), (106, 100))]
    elif n == 'Gate': I.layer(union(rrect_mask(24, 40, 104, 116, 2), ellipse_mask(64, 40, 40)), STONE); I.layer(union(rrect_mask(38, 46, 90, 116, 2), ellipse_mask(64, 46, 26)), (150, 70, 60), glow=(200, 90, 60))
    elif n == 'PowerWordKill': I.layer(ellipse_mask(40, 52, 26, 22), CREAM); I.layer(poly_mask([(30, 72), (44, 72), (28, 90)]), CREAM); skull(I, 92, 76, 0.8)
    elif n == 'Foresight': I.layer(almond(64, 56, 104, 54), BONE); I.layer(ellipse_mask(64, 56, 16), ARC); I.layer(poly_mask([(64, 82), (80, 104), (48, 104)]), GOLD)
    elif n == 'MassPolymorph': rabbit(I, 36, 76, 0.75); rabbit(I, 84, 70, 0.75, CREAM_D); I.layer(poly_mask(star_pts(64, 22, 16, 6, 4)), ARC)
    else: raise KeyError(n)
    return I.done()

NAMES = ['FireBolt', 'RayOfFrost', 'ShockingGrasp', 'PoisonSpray', 'Mending', 'Prestidigitation', 'Light', 'MageHand',
 'FogCloud', 'Grease', 'Thunderwave', 'FalseLife', 'FindFamiliar', 'Alarm', 'ComprehendLanguages', 'DetectMagic',
 'ScorchingRay', 'Shatter', 'Web', 'HoldPerson', 'Invisibility', 'Darkness', 'MindSpike', 'EnhanceAbility', 'ArcaneLock', 'Knock',
 'DispelMagic', 'Fly', 'Fear', 'Slow', 'StinkingCloud', 'VampiricTouch', 'AnimateDead', 'Blink', 'ProtectionFromEnergy', 'MagicCircle', 'SpeakWithDead', 'Sending', 'Counterspell',
 'Polymorph', 'Stoneskin', 'WallOfFire', 'BlackTentacles', 'Banishment', 'Confusion', 'PhantasmalKiller', 'ArcaneEye', 'Fabricate', 'FaithfulHound', 'Blight',
 'ConeOfCold', 'DominatePerson', 'HoldMonster', 'Telekinesis', 'ConjureElemental', 'TeleportationCircle', 'ModifyMemory', 'Scrying', 'LegendLore',
 'ChainLightning', 'WallOfForce', 'TrueSeeing', 'CircleOfDeath', 'FleshToStone', 'MoveEarth', 'Contingency', 'Sunbeam', 'CreateUndead',
 'DelayedBlastFireball', 'DimensionDoor', 'FingerOfDeath', 'ReverseGravity', 'Etherealness', 'PlaneShift', 'MagnificentMansion',
 'ForceCage', 'Maze', 'DominateMonster', 'Feeblemind', 'MindBlank', 'Antipathy', 'Sunburst', 'Clone',
 'TimeStop', 'Wish', 'Gate', 'PowerWordKill', 'Foresight', 'MassPolymorph']

if __name__ == '__main__':
    os.makedirs(OUT, exist_ok=True)
    only = sys.argv[2:]
    for n in NAMES:
        if only and n not in only: continue
        icon(n).save(os.path.join(OUT, n + '.png'))
    print(len(NAMES), 'icons ->', OUT)
