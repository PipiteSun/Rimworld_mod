# 子职业（野蛮人 / 游荡者 / 游侠）的美术：45 个能力图标（128×128）+ 9 个子职业徽记 + 荆棘陷阱贴图 + 预览图。
# 画法沿用 draw_class_art.py / draw_class_art2.py / draw_classes3.py：粗黑描边、平涂 + 右下暗部、低饱和。徽记：盾形底 + 子职业符号 + 暗金边。
# 用法：python draw_subclasses.py <模组根目录>
import math, sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import draw_class_art2 as A2
from draw_class_art import (Icon, poly_mask, ellipse_mask, line_mask, arc_mask, rrect_mask, union, star_pts, bust_mask, shield_pts, sword_masks,
                            Image, CREAM, CREAM_D, KHAKI, KHAKI_D, SLATE, SLATE_D, RUST, RUST_D, VIOLET, VIOLET_D, GOLD, GOLD_D, BONE, BONE_D, STONE, STONE_D)
from draw_class_art2 import flame_mask, wolf_mask, bear_mask, eye_mask, leaf_mask, tree_mask, paw_mask, rays, snow_mask, fist_mask, bolt_mask, cloud_mask, wing_mask, heart_mask, palm_mask
from draw_classes3 import axe, arrow, dagger, hood, MOSS, MOSS_D, FUR, BARK, DUSK, DUSK_D, POISON, BLOOD, INKC
from PIL import ImageDraw

ROOT = sys.argv[1] if len(sys.argv) > 1 else '.'
ICE = (176, 206, 220); ICE_D = (120, 150, 170)
SEA = (84, 130, 168); SEA_D = (56, 90, 120)
SAND = (206, 168, 104)
ARC = (150, 120, 210); ARC_D = (100, 80, 150)

def eagle(I, cx, cy, s=1.0, col=FUR):
    I.layer(wing_mask(cx - 6 * s, cy, -1, s), col); I.layer(wing_mask(cx + 6 * s, cy, 1, s), col)
    I.layer(ellipse_mask(cx, cy - 4 * s, 10 * s, 16 * s), BONE)
    I.layer(poly_mask([(cx - 4 * s, cy - 10 * s), (cx + 4 * s, cy - 10 * s), (cx, cy - 2 * s)]), GOLD)

def coin(I, cx, cy, r=12):
    I.layer(ellipse_mask(cx, cy, r), GOLD); I.layer(arc_mask(cx, cy, r * 0.65, 0, 360, 2), GOLD_D, outline=False, shade=False)

def hand(I, cx, cy, s=1.0, col=ARC):
    I.layer(palm_mask(cx, cy, s), col)

def icon(name):
    I = Icon()
    # ============ 野蛮人通用 ============
    if name == 'FeralInstinct':        # 野性直觉：竖瞳兽眼 + 竖起的毛
        for k in range(5):
            x = 30 + k * 17
            I.layer(poly_mask([(x - 7, 46), (x, 18 + (k % 2) * 8), (x + 7, 46)]), FUR)
        I.layer(eye_mask(64, 70, 44, 24), GOLD)
        I.layer(ellipse_mask(64, 70, 5, 20), INKC, shade=False)
    elif name == 'RecklessCharge':     # 狂暴冲撞：冲锋的牛角人形 + 速度线
        for y in (40, 62, 84):
            I.layer(line_mask([(10, y), (46, y)], 5), CREAM_D)
        I.layer(bust_mask(80, 54, 1.1), BLOOD)
        I.layer(poly_mask([(66, 40), (52, 18), (70, 34)]), BONE); I.layer(poly_mask([(94, 40), (110, 18), (90, 34)]), BONE)
    # ============ 狂战士之道 ============
    elif name == 'Frenzy':             # 狂乱：三把挥舞的斧影
        axe(I, 64, 64, -30, head=RUST_D); axe(I, 64, 64, -75); axe(I, 64, 64, -120, head=BLOOD)
    elif name == 'MindlessRage':       # 无脑狂怒：燃烧的头颅，眼里只有火
        I.layer(flame_mask(64, 60, 1.1), RUST)
        I.layer(ellipse_mask(64, 66, 26, 28), BLOOD)
        I.layer(ellipse_mask(54, 62, 6), GOLD, shade=False); I.layer(ellipse_mask(74, 62, 6), GOLD, shade=False)
        I.layer(rrect_mask(52, 80, 76, 86, 2), INKC, shade=False)
    elif name == 'IntimidatingPresence':  # 威吓凝视：怒视的眼 + 冲击波
        for r in (52, 40):
            I.layer(arc_mask(64, 64, r, 200, 340, 5), CREAM_D)
        I.layer(eye_mask(64, 70, 46, 22), BONE)
        I.layer(ellipse_mask(64, 70, 12), BLOOD); I.layer(ellipse_mask(64, 70, 5), INKC, shade=False)
        I.layer(poly_mask([(24, 50), (60, 58), (24, 58)]), INKC, shade=False); I.layer(poly_mask([(104, 50), (68, 58), (104, 58)]), INKC, shade=False)
    elif name == 'Retaliation':        # 报复：盾挡下一击 + 反手斧
        I.layer(poly_mask(shield_pts(48, 66, 44, 54)), SLATE)
        axe(I, 82, 62, -30, L=72)
        I.layer(poly_mask(star_pts(96, 32, 16, 6, 6)), GOLD)
    # ============ 图腾勇士之道 ============
    elif name == 'TotemSpirit':        # 图腾之灵：图腾柱（熊 / 鹰 / 狼三层）
        I.layer(rrect_mask(46, 14, 82, 118, 6), BARK)
        I.layer(bear_mask(64, 32, 0.5), FUR); I.layer(wolf_mask(64, 66, 0.5), SLATE)
        I.layer(poly_mask([(30, 92), (64, 82), (98, 92), (64, 102)]), GOLD)
    elif name == 'AspectOfBeast':      # 兽之面相：半张兽脸的人头
        I.layer(bust_mask(64, 50, 1.1), FUR)
        I.layer(poly_mask([(64, 26), (90, 36), (92, 62), (64, 70)]), BONE)
        I.layer(ellipse_mask(78, 48, 4), INKC, shade=False); I.layer(ellipse_mask(52, 48, 4), INKC, shade=False)
    elif name == 'SpiritWalker':       # 灵魂行者：半透明的灵兽
        I.layer(ellipse_mask(64, 100, 44, 10), MOSS_D)
        I.layer(wolf_mask(64, 56, 1.15), ICE)
        rays(I, 64, 56, 48, 58, 8, CREAM_D, w=3)
    elif name == 'TotemAttunement':    # 图腾调和：熊掌 + 鹰羽 + 狼牙环
        I.layer(arc_mask(64, 64, 46, 0, 360, 6), GOLD_D)
        I.layer(paw_mask(46, 70, 0.6), FUR)
        I.layer(leaf_mask(70, 30, 98, 76, 10), BONE)
        I.layer(poly_mask([(80, 84), (92, 104), (98, 82)]), BONE)
    # ============ 风暴先驱之道 ============
    elif name == 'StormAura':          # 风暴光环：人影周围的旋风
        for r, c in ((52, SEA_D), (40, SEA), (28, ICE)):
            I.layer(arc_mask(64, 64, r, 20, 300, 6), c)
        I.layer(bust_mask(64, 52, 0.7), BLOOD)
    elif name == 'StormSoul':          # 风暴之魂：火 / 雷 / 冰三分的心
        I.layer(heart_mask(64, 64, 1.1), SEA)
        I.layer(flame_mask(48, 62, 0.45), RUST); I.layer(bolt_mask(66, 56, 0.45), GOLD); I.layer(snow_mask(84, 66, 10, 3), ICE)
    elif name == 'ShieldingStorm':     # 庇护风暴：风暴环里的两个人影
        I.layer(arc_mask(64, 64, 50, 0, 360, 7), SEA)
        I.layer(bust_mask(48, 56, 0.7), STONE); I.layer(bust_mask(80, 56, 0.7), BLOOD)
    elif name == 'RagingStorm':        # 狂暴风暴：雷云 + 闪电 + 火与冰
        I.layer(cloud_mask(64, 40, 1.05), SLATE_D)
        I.layer(bolt_mask(64, 78, 0.9), GOLD)
        I.layer(flame_mask(30, 96, 0.4), RUST); I.layer(snow_mask(100, 94, 12, 3), ICE)
    # ============ 游荡者通用 ============
    elif name == 'RogueEvasion':       # 反射闪避：爆炸 + 闪开的人影
        I.layer(poly_mask(star_pts(46, 76, 34, 14, 9)), RUST)
        I.layer(bust_mask(88, 42, 0.8), DUSK)
        I.layer(arc_mask(64, 64, 52, 280, 350, 5), CREAM_D)
    elif name == 'SteadyTarget':       # 稳固目标：准星
        I.layer(arc_mask(64, 64, 42, 0, 360, 6), BONE)
        for a in (0, 90, 180, 270):
            r = math.radians(a)
            I.layer(line_mask([(64 + math.cos(r) * 26, 64 + math.sin(r) * 26), (64 + math.cos(r) * 54, 64 + math.sin(r) * 54)], 5), BONE)
        I.layer(ellipse_mask(64, 64, 8), BLOOD)
    # ============ 刺客 ============
    elif name == 'AssassinsOpening':   # 暗杀：熟睡的人头 + 背后匕首
        I.layer(bust_mask(48, 56, 1.0), STONE)
        I.layer(arc_mask(40, 50, 6, 20, 160, 2), INKC, outline=False, shade=False)
        dagger(I, 90, 54, -150, L=62)
        I.layer(poly_mask(star_pts(62, 40, 12, 5, 6)), GOLD)
    elif name == 'Impostor':           # 伪装身份：面具
        I.layer(ellipse_mask(64, 64, 40, 46), BONE)
        I.layer(eye_mask(48, 56, 16, 8), INKC, shade=False); I.layer(eye_mask(80, 56, 16, 8), INKC, shade=False)
        I.layer(arc_mask(64, 80, 16, 20, 160, 4), INKC, outline=False, shade=False)
        I.layer(line_mask([(20, 60), (6, 40)], 4), BLOOD)
    elif name == 'DeadlyStrike':       # 致命一击：倒地的人 + 刺下的匕首
        I.layer(rrect_mask(16, 86, 112, 104, 8), STONE)
        I.layer(ellipse_mask(26, 88, 12), STONE)
        dagger(I, 64, 56, 90, L=70, blade=BLOOD)
    elif name == 'DeathStrike':        # 死亡之击：骷髅 + 劈开的裂痕
        I.layer(ellipse_mask(64, 58, 34, 32), BONE)
        I.layer(rrect_mask(48, 80, 80, 102, 6), BONE)
        I.layer(ellipse_mask(52, 58, 8), INKC, shade=False); I.layer(ellipse_mask(76, 58, 8), INKC, shade=False)
        I.layer(line_mask([(96, 14), (66, 54), (80, 66), (40, 118)], 5), BLOOD)
    # ============ 盗贼 ============
    elif name == 'FastHands':          # 快手：伸出的手抓着钱袋
        I.layer(rrect_mask(58, 46, 100, 92, 16), KHAKI)
        I.layer(line_mask([(64, 46), (94, 46)], 6), BARK)
        hand(I, 40, 74, 0.8, CREAM)
        coin(I, 96, 30, 10)
    elif name == 'SecondStoryWork':    # 飞檐走壁：屋顶 + 飞跃的人影
        I.layer(poly_mask([(4, 120), (4, 86), (40, 64), (76, 86), (76, 120)]), STONE)
        I.layer(bust_mask(92, 40, 0.75), DUSK)
        I.layer(arc_mask(60, 70, 40, 230, 310, 4), CREAM_D)
    elif name == 'TreasureSense':      # 寻宝直觉：宝箱 + 光芒
        rays(I, 64, 58, 34, 54, 10, GOLD, w=4)
        I.layer(rrect_mask(30, 58, 98, 104, 6), BARK)
        I.layer(rrect_mask(30, 50, 98, 66, 6), RUST_D)
        I.layer(rrect_mask(58, 60, 70, 74, 2), GOLD)
    elif name == 'ThiefReflexes':      # 盗贼反射：两把交叉匕首 + 闪电
        dagger(I, 64, 64, -45, L=80); dagger(I, 64, 64, -135, L=80)
        I.layer(bolt_mask(64, 64, 0.5), GOLD)
    # ============ 诡术师 ============
    elif name == 'MageHand':           # 法师之手：发光的幽灵手
        rays(I, 64, 64, 40, 54, 8, ARC_D, w=3)
        hand(I, 64, 66, 1.1, ARC)
    elif name == 'ATMagicMissile':     # 魔能飞弹：三道紫光 + 匕首
        for k in range(3):
            y = 30 + k * 30
            I.layer(line_mask([(20, y + 10), (90, y)], 6), ARC)
            I.layer(poly_mask(star_pts(96, y, 12, 5, 4)), CREAM)
    elif name == 'ATInvisibility':     # 隐形术：虚线兜帽
        I.layer(arc_mask(64, 64, 50, 0, 360, 4), ARC_D)
        hood(I, 64, 64, col=VIOLET_D)
        I.layer(arc_mask(64, 64, 34, 200, 260, 4), CREAM_D)
    elif name == 'ATSleep':            # 睡眠术：新月 + Z
        I.layer(ellipse_mask(56, 66, 36), ARC)
        I.layer(arc_mask(56, 66, 30, 60, 300, 12), VIOLET_D)
        for x, y, s in ((88, 34, 12), (104, 18, 8)):
            I.layer(line_mask([(x - s, y - s), (x + s, y - s), (x - s, y + s), (x + s, y + s)], 4), CREAM)
    elif name == 'MagicalAmbush':      # 魔法伏击：阴影里伸出的法杖光
        hood(I, 50, 66, col=DUSK_D)
        I.layer(line_mask([(78, 100), (108, 30)], 6), BARK)
        I.layer(poly_mask(star_pts(108, 28, 16, 6, 5)), ARC)
    elif name == 'VersatileTrickster':  # 多才多艺的诡计：幽灵手扯住人影
        I.layer(bust_mask(80, 50, 0.95), STONE)
        hand(I, 40, 60, 0.8, ARC)
        I.layer(line_mask([(50, 60), (70, 62)], 3), ARC_D, outline=False)
    elif name == 'SpellThief':         # 偷取法术：手里抓着一颗法术星
        hand(I, 60, 76, 1.0, CREAM)
        I.layer(poly_mask(star_pts(62, 38, 22, 9, 5)), ARC)
        I.layer(arc_mask(64, 64, 52, 120, 200, 4), ARC_D)
    # ============ 游侠通用 ============
    elif name == 'FieldEye':           # 原野之眼：叶片里的眼 + 远处的骷髅
        for x0, y0, x1, y1 in ((10, 50, 54, 26), (74, 26, 118, 50)):
            I.layer(leaf_mask(x0, y0, x1, y1, 12), MOSS)
        I.layer(eye_mask(64, 70, 46, 24), BONE)
        I.layer(ellipse_mask(64, 70, 12), MOSS_D); I.layer(ellipse_mask(64, 70, 5), INKC, shade=False)
    elif name == 'ThornTrap':          # 荆棘陷阱：带刺的藤圈
        I.layer(arc_mask(64, 68, 38, 0, 360, 10), MOSS_D)
        for a in range(0, 360, 40):
            r = math.radians(a); x, y = 64 + math.cos(r) * 38, 68 + math.sin(r) * 38
            I.layer(poly_mask([(x - 5, y), (x + math.cos(r) * 16, y + math.sin(r) * 16), (x + 5, y)]), BONE)
        I.layer(leaf_mask(52, 30, 80, 20, 8), MOSS)
    # ============ 猎人 ============
    elif name == 'HuntersPrey':        # 猎手之选：巨像的影子 + 弓
        I.layer(bust_mask(78, 46, 1.4), STONE_D)
        I.layer(arc_mask(48, 66, 40, 110, 250, 6), BARK)
        I.layer(line_mask([(34, 30), (34, 102)], 2), CREAM, outline=False)
        arrow(I, 30, 66, 92, 66, w=3)
    elif name == 'DefensiveTactics':   # 防御战术：圆盾 + 擦过的箭
        I.layer(ellipse_mask(56, 66, 38), SLATE)
        I.layer(ellipse_mask(56, 66, 12), GOLD)
        arrow(I, 30, 22, 112, 40, w=3)
    elif name == 'MultiAttack':        # 多重攻击：扇形三箭 + 弧光
        I.layer(arc_mask(30, 64, 70, -40, 40, 5), CREAM_D)
        for a in (-30, 0, 30):
            r = math.radians(a)
            arrow(I, 20, 64, 20 + math.cos(r) * 84, 64 + math.sin(r) * 84, w=3)
    elif name == 'SuperiorDefense':    # 高级猎手防御：叠起的两块盾
        I.layer(poly_mask(shield_pts(52, 62, 44, 54)), SLATE_D)
        I.layer(poly_mask(shield_pts(74, 70, 44, 54)), SLATE)
        I.layer(poly_mask(star_pts(74, 66, 12, 5, 4)), GOLD)
    # ============ 兽王 ============
    elif name == 'RangerCompanion':    # 游侠伙伴：人与狼并肩
        I.layer(bust_mask(42, 46, 0.95), MOSS)
        I.layer(wolf_mask(84, 70, 0.8), SLATE)
    elif name == 'ExceptionalTraining':  # 非凡训练：跃起的狼 + 爪印
        I.layer(wolf_mask(60, 52, 1.0), FUR)
        for x, y in ((30, 104), (60, 110), (92, 104)):
            I.layer(paw_mask(x, y, 0.32), MOSS_D)
    elif name == 'BestialFury':        # 野兽之怒：两道爪痕 + 狼头
        I.layer(wolf_mask(56, 66, 0.95), SLATE_D)
        for k in range(3):
            I.layer(line_mask([(74 + k * 12, 18), (96 + k * 10, 86)], 5), BLOOD)
    elif name == 'ShareSpells':        # 分享法术：人和兽之间的光带
        I.layer(bust_mask(36, 48, 0.8), MOSS)
        I.layer(wolf_mask(92, 70, 0.7), FUR)
        I.layer(arc_mask(64, 90, 34, 200, 340, 6), GOLD)
        I.layer(poly_mask(star_pts(64, 52, 12, 5, 5)), GOLD)
    # ============ 幽域追踪者 ============
    elif name == 'DreadAmbusher':      # 恐惧伏击：黑暗里的一双眼 + 弓
        I.layer(ellipse_mask(64, 64, 52), DUSK_D)
        I.layer(eye_mask(46, 56, 18, 8), GOLD, shade=False); I.layer(eye_mask(82, 56, 18, 8), GOLD, shade=False)
        arrow(I, 24, 96, 104, 86, w=3)
    elif name == 'IronMind':           # 钢铁意志：铁盔里的脑 / 盾
        I.layer(rrect_mask(30, 34, 98, 104, 20), SLATE)
        I.layer(rrect_mask(40, 62, 88, 70, 2), INKC, shade=False)
        I.layer(line_mask([(64, 34), (64, 104)], 4), SLATE_D, outline=False)
    elif name == 'StalkersFlurry':     # 追猎者的乱舞：一箭落空、一箭追上
        arrow(I, 14, 30, 90, 20, col=STONE_D, w=3)
        arrow(I, 14, 90, 92, 60, w=4)
        I.layer(poly_mask(star_pts(100, 58, 14, 6, 6)), GOLD)
    elif name == 'ShadowyDodge':       # 暗影闪避：残影
        for x, c in ((40, DUSK_D), (64, DUSK), (88, STONE)):
            I.layer(bust_mask(x, 48, 0.8), c)
        I.layer(arc_mask(64, 64, 52, 200, 340, 4), CREAM_D)
    return I.done(128)

GROUPS = {'野蛮人通用': ['FeralInstinct', 'RecklessCharge'],
          '狂战士': ['Frenzy', 'MindlessRage', 'IntimidatingPresence', 'Retaliation'],
          '图腾勇士': ['TotemSpirit', 'AspectOfBeast', 'SpiritWalker', 'TotemAttunement'],
          '风暴先驱': ['StormAura', 'StormSoul', 'ShieldingStorm', 'RagingStorm'],
          '游荡者通用': ['RogueEvasion', 'SteadyTarget'],
          '刺客': ['AssassinsOpening', 'Impostor', 'DeadlyStrike', 'DeathStrike'],
          '盗贼': ['FastHands', 'SecondStoryWork', 'TreasureSense', 'ThiefReflexes'],
          '诡术师': ['MageHand', 'ATMagicMissile', 'ATInvisibility', 'ATSleep', 'MagicalAmbush', 'VersatileTrickster', 'SpellThief'],
          '游侠通用': ['FieldEye', 'ThornTrap'],
          '猎人': ['HuntersPrey', 'DefensiveTactics', 'MultiAttack', 'SuperiorDefense'],
          '兽王': ['RangerCompanion', 'ExceptionalTraining', 'BestialFury', 'ShareSpells'],
          '幽域追踪者': ['DreadAmbusher', 'IronMind', 'StalkersFlurry', 'ShadowyDodge']}

# 徽记：盾形底色 + 符号，外圈暗金
EMBLEMS = {'Berserker': (BLOOD, 'axe2'), 'TotemWarrior': (BARK, 'totem'), 'StormHerald': (SEA_D, 'storm'),
           'Assassin': ((70, 40, 46), 'dagger'), 'Thief': ((88, 92, 70), 'coin'), 'ArcaneTrickster': (VIOLET_D, 'hand'),
           'Hunter': (MOSS_D, 'bow'), 'BeastMaster': ((96, 78, 52), 'paw'), 'GloomStalker': (DUSK_D, 'eye')}

def emblem(sub):
    I = Icon(); col, sym = EMBLEMS[sub]
    I.layer(poly_mask(shield_pts(64, 64, 100, 116)), GOLD_D)
    I.layer(poly_mask(shield_pts(64, 62, 86, 100)), col)
    if sym == 'axe2':
        axe(I, 64, 62, -60, L=66); axe(I, 64, 62, -120, L=66, head=RUST_D)
    elif sym == 'totem':
        I.layer(rrect_mask(52, 22, 76, 104, 5), BARK); I.layer(bear_mask(64, 40, 0.42), FUR); I.layer(wolf_mask(64, 74, 0.4), SLATE)
    elif sym == 'storm':
        I.layer(cloud_mask(64, 46, 0.8), SLATE); I.layer(bolt_mask(64, 78, 0.6), GOLD)
    elif sym == 'dagger':
        dagger(I, 64, 62, 90, L=78, blade=BONE); I.layer(ellipse_mask(64, 40, 8), BLOOD)
    elif sym == 'coin':
        coin(I, 64, 60, 22); I.layer(rrect_mask(48, 80, 80, 88, 3), BONE)
    elif sym == 'hand':
        hand(I, 64, 64, 0.95, ARC); I.layer(poly_mask(star_pts(64, 30, 10, 4, 5)), CREAM)
    elif sym == 'bow':
        I.layer(arc_mask(64, 62, 32, 110, 250, 6), BONE); arrow(I, 40, 62, 96, 62, w=3)
    elif sym == 'paw':
        I.layer(paw_mask(64, 64, 0.8), BONE)
    elif sym == 'eye':
        I.layer(eye_mask(64, 60, 50, 22), GOLD); I.layer(ellipse_mask(64, 60, 5, 16), INKC, shade=False)
    return I.done(128)

def thorn_trap():
    I = Icon()
    for a in range(0, 360, 30):
        r = math.radians(a); x, y = 64 + math.cos(r) * 34, 64 + math.sin(r) * 30
        I.layer(poly_mask([(x - 5, y), (x + math.cos(r) * 18, y + math.sin(r) * 18), (x + 5, y)]), BONE_D)
    I.layer(arc_mask(64, 64, 32, 0, 360, 12), MOSS_D)
    I.layer(arc_mask(64, 64, 18, 30, 330, 8), MOSS)
    for x0, y0, x1, y1 in ((40, 40, 60, 30), (70, 92, 92, 84)):
        I.layer(leaf_mask(x0, y0, x1, y1, 8), MOSS)
    return I.done(128)

if __name__ == '__main__':
    out = os.path.join(ROOT, 'Textures', 'LP', 'Classes', 'Subclass')
    pv = os.path.join(ROOT, 'ArtSource', 'Preview'); os.makedirs(pv, exist_ok=True); os.makedirs(out, exist_ok=True)
    names = [n for g in GROUPS.values() for n in g]
    items = [(n, icon(n)) for n in names] + [('Emblem_' + s, emblem(s)) for s in EMBLEMS] + [('ThornTrapThing', thorn_trap())]
    cols = 10; cell = 150
    rows = (len(items) + cols - 1) // cols
    sheet = Image.new('RGBA', (cols * cell + 20, rows * (cell + 24) + 30), (46, 48, 52, 255)); dr = ImageDraw.Draw(sheet)
    for i, (n, im) in enumerate(items):
        im.save(os.path.join(out, n + '.png'))
        x = 10 + (i % cols) * cell + 11; y = 20 + (i // cols) * (cell + 24)
        sheet.paste(im, (x, y), im); dr.text((x, y + 130), n, fill=(220, 220, 210, 255))
    sheet.save(os.path.join(pv, 'Subclasses-icons.png'))
    print('subclass art written:', len(names), 'icons +', len(EMBLEMS), 'emblems + trap')
