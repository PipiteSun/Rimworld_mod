# 1.103.0 第二批子职业的美术：43 个能力图标（128×128）、7 个徽记、钢铁卫士贴图（南 / 北 / 东）+ 预览图。
# 画法沿用 draw_subclasses.py / draw_warlock.py：粗黑描边、平涂 + 右下暗部、低饱和。
# 用法：python draw_subclasses2.py <模组根目录>
import math, sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from draw_class_art import (Icon, poly_mask, ellipse_mask, line_mask, arc_mask, rrect_mask, union, star_pts, bust_mask, shield_pts, sword_masks,
                            Image, CREAM, CREAM_D, KHAKI, KHAKI_D, SLATE, SLATE_D, RUST, RUST_D, VIOLET, VIOLET_D, GOLD, GOLD_D, BONE, BONE_D, STONE, STONE_D)
from draw_class_art2 import flame_mask, eye_mask, leaf_mask, rays, palm_mask, bolt_mask, cloud_mask, wing_mask, heart_mask, fist_mask, snow_mask
from draw_classes3 import dagger, hood, MOSS, MOSS_D, DUSK, DUSK_D, BLOOD, INKC
from draw_legendary_174 import gear
from PIL import ImageDraw

ROOT = sys.argv[1] if len(sys.argv) > 1 else '.'
ARC = (128, 118, 220); ARC_D = (82, 74, 160)       # 奥法紫蓝
PSI = (100, 196, 206); PSI_D = (56, 128, 140)      # 灵能青
RED = (190, 46, 56); RED_D = (120, 26, 34)         # 血红
BRASS = (200, 160, 84); BRASS_D = (138, 104, 48)   # 黄铜
ABER = (86, 150, 108); ABER_D = (50, 96, 66)       # 墨绿
IRON = (92, 88, 96); IRON_D = (56, 52, 60)         # 铁黑
SILV = (226, 230, 236); SILV_D = (160, 168, 180)   # 银白
DRAG = (204, 112, 60); DRAG_D = (140, 66, 34)      # 龙魂橙红
FIEND = (184, 40, 46); FIEND_D = (110, 20, 26)     # 恶鬼红
EVO = (230, 120, 60)                                # 塑能橙
SCRIBE = (140, 176, 230); SCRIBE_D = (82, 110, 160) # 书士蓝
SPORE = (176, 160, 120); SPORE_D = (110, 98, 70)    # 孢子灰褐
STAR = (180, 200, 250); STAR_D = (100, 120, 180)    # 星光
HOLY = (240, 214, 120)

def sword(I, cx, cy, ang, L=90, blade=CREAM, hilt=RUST):
    for m, c in sword_masks(cx, cy, ang, L=L, blade_col=blade, hilt_col=hilt): I.layer(m, c)
def drop(I, cx, cy, s=1.0, col=RED):
    I.layer(union(ellipse_mask(cx, cy + 8 * s, 16 * s), poly_mask([(cx - 14 * s, cy + 2 * s), (cx, cy - 24 * s), (cx + 14 * s, cy + 2 * s)])), col)
def ripples(I, cx, cy, rs, col, w=4, a0=0, a1=360):
    for r in rs: I.layer(arc_mask(cx, cy, r, a0, a1, w), col, shade=False)
def tentacle(I, x0, y0, bend, col=ABER, w=9, L=60):
    I.layer(line_mask([(x0 + math.sin(k / 6.0 * math.pi) * bend, y0 - k * L / 6.0) for k in range(7)], w), col)
def bot(I, cx, cy, s=1.0, col=BRASS):   # 钢铁卫士的小剪影
    I.layer(rrect_mask(cx - 26 * s, cy - 18 * s, cx + 26 * s, cy + 20 * s, 8 * s), col)
    I.layer(rrect_mask(cx - 16 * s, cy - 38 * s, cx + 16 * s, cy - 16 * s, 6 * s), col)
    I.layer(ellipse_mask(cx, cy - 27 * s, 5 * s), PSI, outline=False, shade=False)
    for sx in (-1, 1): I.layer(rrect_mask(cx + sx * 30 * s - 7 * s, cy - 10 * s, cx + sx * 30 * s + 7 * s, cy + 22 * s, 4 * s), BRASS_D)
    for sx in (-1, 1): I.layer(rrect_mask(cx + sx * 14 * s - 7 * s, cy + 18 * s, cx + sx * 14 * s + 7 * s, cy + 34 * s, 3 * s), IRON)

def icon(name):
    I = Icon()
    # ============ 奥法骑士 ============
    if name == 'WeaponBond':           # 武器羁绊：剑身缠着发光的符文链
        sword(I, 64, 64, -60, 104)
        I.layer(arc_mask(64, 64, 34, 200, 520, 5), ARC, shade=False)
        for a in (30, 150, 270): r = math.radians(a); I.layer(poly_mask(star_pts(64 + math.cos(r) * 34, 64 + math.sin(r) * 34, 8, 3, 4)), CREAM, shade=False)
    elif name == 'EKShield':           # 护盾术：剑后一面紫色光盾
        I.layer(poly_mask(shield_pts(64, 64, 80, 92)), ARC_D); I.layer(poly_mask(shield_pts(64, 60, 60, 70)), ARC)
        sword(I, 64, 64, -90, 96)
    elif name == 'ThunderBlade':       # 雷鸣斩：剑 + 闪电 + 声波
        sword(I, 52, 72, -45, 100)
        I.layer(bolt_mask(92, 42, 0.8), GOLD); ripples(I, 88, 40, (28, 40), ARC, 4, 120, 240)
    elif name == 'AbsorbElements':     # 吸收元素：火、冰、电汇入掌心
        I.layer(palm_mask(64, 84, 0.8), BONE)
        I.layer(flame_mask(28, 34, 0.45), RUST); I.layer(snow_mask(64, 22, 14), (170, 210, 240)); I.layer(bolt_mask(100, 34, 0.45), GOLD)
        for x, y in ((34, 46), (64, 38), (94, 46)): I.layer(line_mask([(x, y), (64, 70)], 3), ARC, shade=False)
    elif name == 'WarMagic':           # 战争魔法：剑与法杖交叉
        sword(I, 64, 64, -45, 104)
        I.layer(line_mask([(30, 104), (98, 26)], 7), KHAKI_D); I.layer(ellipse_mask(98, 26, 10), ARC)
    elif name == 'ArcaneCharge':       # 奥术冲锋：带残影的骑士
        for i, x in enumerate((30, 50)): I.layer(bust_mask(x, 70, 0.75), ARC_D if i == 0 else ARC)
        I.layer(bust_mask(82, 66, 0.9), SLATE); sword(I, 98, 54, -30, 64)
    elif name == 'EldritchStrike':     # 法术打击：剑尖刺出法术印记
        sword(I, 46, 82, -45, 96)
        I.layer(arc_mask(94, 36, 22, 0, 360, 5), ARC); I.layer(poly_mask(star_pts(94, 36, 14, 6, 5)), ARC_D)
    elif name == 'Bladestorm':         # 法刃风暴：环绕的剑 + 飞弹
        for a in range(0, 360, 90): sword(I, 64 + math.cos(math.radians(a)) * 26, 64 + math.sin(math.radians(a)) * 26, a + 90, 56)
        I.layer(ellipse_mask(64, 64, 12), ARC)
    # ============ 灵能战士 ============
    elif name == 'PsionicStrike':      # 灵能打击：拳头 + 青色光晕
        I.layer(ellipse_mask(64, 64, 46), PSI_D, glow=PSI); I.layer(fist_mask(64, 66, 1.0), BONE)
    elif name == 'ProtectiveField':    # 护体念力：人像外一圈念力壳
        I.layer(arc_mask(64, 64, 50, 0, 360, 8), PSI); I.layer(bust_mask(64, 70, 1.0), SLATE)
    elif name == 'TeleJump':           # 念动跳跃：弧线跳过墙
        I.layer(rrect_mask(54, 70, 74, 112, 3), STONE_D)
        I.layer(arc_mask(64, 90, 46, 200, 340, 5), PSI, shade=False); I.layer(bust_mask(104, 76, 0.55), SLATE); I.layer(bust_mask(24, 80, 0.45), PSI_D)
    elif name == 'PsiAdept':           # 念动适应：额头发光的冥想者
        I.layer(bust_mask(64, 72, 1.1), DUSK); rays(I, 64, 40, 18, 30, 10, PSI, w=3); I.layer(ellipse_mask(64, 42, 7), PSI)
    elif name == 'Shove':              # 念动推送：掌心推出的波纹撞飞人影
        I.layer(palm_mask(30, 66, 0.7), BONE); ripples(I, 40, 64, (24, 38, 52), PSI, 5, -40, 40); I.layer(bust_mask(100, 60, 0.6), STONE_D)
    elif name == 'GuardianField':      # 护卫念场：大罩子下三个人
        I.layer(arc_mask(64, 100, 56, 180, 360, 8), PSI)
        for x in (36, 64, 92): I.layer(bust_mask(x, 92, 0.55), SLATE)
    elif name == 'TeleStorm':          # 念动风暴：漩涡里浮起的石块
        for r in (18, 34, 50): I.layer(arc_mask(64, 64, r, r * 3, r * 3 + 260, 5), PSI if r != 34 else PSI_D, shade=False)
        for x, y in ((30, 30), (98, 36), (40, 100), (96, 94)): I.layer(poly_mask([(x - 9, y + 6), (x, y - 9), (x + 9, y + 4), (x + 2, y + 10)]), STONE)
    # ============ 血咒学派 ============
    elif name == 'BloodSpear':         # 血矛
        I.layer(poly_mask([(20, 108), (96, 32), (112, 16), (104, 40), (28, 116)]), RED); drop(I, 30, 92, 0.5, RED_D)
    elif name == 'BloodBind':          # 血缚：血色锁链缠住人影
        I.layer(bust_mask(64, 66, 1.0), STONE_D)
        for y in (58, 82): I.layer(line_mask([(18, y), (110, y - 10)], 7), RED)
    elif name == 'FleshShaping':       # 血肉塑形：针线缝合的伤口
        I.layer(ellipse_mask(64, 64, 46, 34), (214, 160, 150)); I.layer(line_mask([(30, 66), (98, 60)], 5), RED_D, shade=False)
        for x in (40, 54, 68, 82): I.layer(line_mask([(x, 54), (x + 6, 74)], 3), CREAM, shade=False)
    elif name == 'Siphon':             # 汲血：血滴流进杯子
        I.layer(poly_mask([(32, 64), (96, 64), (84, 100), (44, 100)]), SLATE); I.layer(rrect_mask(58, 100, 70, 116, 2), SLATE_D)
        drop(I, 64, 34, 0.7)
    elif name == 'BloodBoil':          # 血沸：冒泡的血池
        I.layer(ellipse_mask(64, 86, 50, 22), RED_D)
        for x, y, r in ((44, 70, 10), (70, 60, 14), (90, 74, 8)): I.layer(ellipse_mask(x, y, r), RED)
        rays(I, 64, 40, 8, 22, 6, RUST, w=3)
    elif name == 'FleshPrison':        # 血肉禁锢：被血肉缠成茧的人
        I.layer(ellipse_mask(64, 66, 34, 50), (170, 90, 90))
        for y in (36, 58, 80, 100): I.layer(arc_mask(64, y, 30, 200, 340, 5), RED_D, shade=False)
        I.layer(ellipse_mask(64, 30, 12), BONE)
    elif name == 'BloodRite':          # 血之秘仪：血色法阵中心的心脏
        I.layer(arc_mask(64, 64, 48, 0, 360, 6), RED_D); I.layer(poly_mask(star_pts(64, 64, 44, 18, 6)), RED)
        I.layer(heart_mask(64, 62, 0.55), (230, 120, 130))
    # ============ 战铸师 ============
    elif name == 'SteelDefender':      # 钢铁卫士
        bot(I, 64, 72, 1.1)
    elif name == 'BattleReady':        # 魔武训练：锤子和齿轮
        gear(I, 50, 74, 26, KHAKI); I.layer(line_mask([(70, 104), (100, 34)], 8), KHAKI_D); I.layer(rrect_mask(82, 20, 118, 42, 4), SLATE)
    elif name == 'ArcaneJolt':         # 奥术震击：卫士拳头上的电弧
        bot(I, 54, 78, 0.8); I.layer(bolt_mask(98, 40, 0.7), ARC)
    elif name == 'ImprovedDefender':   # 改良卫士：卫士加装甲板与扳手
        bot(I, 60, 74, 1.0, SLATE); I.layer(poly_mask(shield_pts(96, 40, 34, 40)), BRASS)
    elif name == 'MasterSmith':        # 战铸大师：两台卫士
        bot(I, 40, 80, 0.7); bot(I, 88, 76, 0.75, BRASS_D); I.layer(poly_mask(star_pts(64, 22, 12, 5, 5)), GOLD)
    # ============ 异变心灵 ============
    elif name == 'CalmMind':           # 安抚心灵：抚平的波纹
        I.layer(bust_mask(64, 70, 1.0), STONE); ripples(I, 64, 40, (26, 38), ABER, 4, 200, 340); I.layer(eye_mask(64, 46, 20, 6), ABER)
    elif name == 'PsychicSpike':       # 灵能尖刺：刺入脑袋的绿色尖刺
        I.layer(ellipse_mask(70, 74, 34, 28), (200, 150, 160)); I.layer(poly_mask([(16, 20), (64, 70), (56, 76)]), ABER)
    elif name == 'PsychicDefense':     # 灵能防护：触须围成的盾
        I.layer(poly_mask(shield_pts(64, 64, 64, 76)), ABER_D)
        tentacle(I, 30, 110, 10); tentacle(I, 98, 110, -10, ABER)
    elif name == 'FleshRevelation':    # 变形之身：人影背后伸出触手
        tentacle(I, 34, 110, 16); tentacle(I, 94, 110, -16, ABER_D); tentacle(I, 64, 110, 8, ABER, 8, 70)
        I.layer(bust_mask(64, 74, 0.85), STONE)
    elif name == 'MindDominion':       # 心灵支配：触须牵着的人头
        I.layer(bust_mask(64, 78, 1.0), STONE_D)
        for x in (44, 64, 84): I.layer(line_mask([(x, 14), (x, 54)], 4), ABER, shade=False)
        I.layer(eye_mask(64, 16, 46, 14), ABER_D)
    # ============ 征服之誓 ============
    elif name == 'ConqueringPresence': # 征服者之威：黑铁头盔 + 恐惧之眼
        I.layer(rrect_mask(30, 24, 98, 104, 20), IRON); I.layer(rrect_mask(40, 56, 88, 66, 3), INKC, shade=False)
        rays(I, 64, 64, 54, 62, 12, IRON_D, w=4)
    elif name == 'GuidedStrike':       # 必胜一击：准星中的剑
        I.layer(arc_mask(64, 64, 40, 0, 360, 5), HOLY); sword(I, 64, 64, -45, 100, SILV, IRON)
    elif name == 'AuraOfConquest':     # 征服灵光：黑色光环里跪下的人影
        I.layer(arc_mask(64, 64, 50, 0, 360, 8), IRON_D); I.layer(bust_mask(64, 78, 0.8), STONE)
    elif name == 'ScornfulRebuke':     # 威慑斩：斩出的黑色月牙
        I.layer(arc_mask(64, 64, 42, 200, 340, 12), IRON_D); sword(I, 64, 70, -90, 90, SILV, IRON)
    elif name == 'InvincibleConqueror':  # 不败征服者：王冠 + 黑铁盾
        I.layer(poly_mask(shield_pts(64, 72, 70, 82)), IRON)
        I.layer(poly_mask([(36, 34), (46, 14), (56, 30), (64, 10), (72, 30), (82, 14), (92, 34)]), GOLD)
    # ============ 救赎之誓 ============
    elif name == 'EmissaryOfPeace':    # 和平使者：橄榄枝
        I.layer(line_mask([(24, 104), (100, 28)], 5), MOSS_D)
        for i in range(5): t = 0.2 + i * 0.15; x, y = 24 + 76 * t, 104 - 76 * t; I.layer(leaf_mask(x, y, x + 16, y - 10, 7), MOSS)
        I.layer(ellipse_mask(100, 28, 7), SILV)
    elif name == 'RebukeViolent':      # 斥责暴力：盾上反弹的箭头
        I.layer(poly_mask(shield_pts(52, 66, 56, 66)), SILV); I.layer(line_mask([(104, 30), (78, 56), (104, 82)], 6), HOLY, shade=False)
    elif name == 'AuraOfGuardian':     # 守护者灵光：张开双臂护住身后
        I.layer(wing_mask(64, 60, -1, 1.0), SILV_D); I.layer(wing_mask(64, 60, 1, 1.0), SILV_D); I.layer(bust_mask(64, 70, 0.9), SILV)
    elif name == 'ProtectiveSpirit':   # 坚韧之魂：光中的心
        rays(I, 64, 64, 30, 52, 12, HOLY, w=4); I.layer(heart_mask(64, 64, 0.8), SILV)
    elif name == 'EmissaryOfRedemption':  # 救赎天使：光翼
        I.layer(wing_mask(64, 56, -1, 1.2), SILV); I.layer(wing_mask(64, 56, 1, 1.2), SILV); I.layer(arc_mask(64, 24, 16, 0, 360, 4), HOLY, shade=False)
        I.layer(bust_mask(64, 74, 0.7), CREAM)
    elif name == 'Reconcile':          # 和解：两只握在一起的手
        I.layer(palm_mask(44, 70, 0.7, 30), BONE); I.layer(palm_mask(84, 70, 0.7, -30), BONE_D); rays(I, 64, 52, 30, 44, 8, HOLY, w=3)
    # ============ 1.104.0 龙魂 ============
    elif name == 'DragonBlood':        # 龙血：带鳞片纹的血滴
        drop(I, 64, 62, 1.6, DRAG)
        for x, y in ((56, 70), (72, 70), (64, 84)): I.layer(arc_mask(x, y, 8, 200, 340, 3), DRAG_D, shade=False)
    elif name == 'DragonFlight':       # 龙翼：一对龙翼
        I.layer(wing_mask(64, 60, -1, 1.2), DRAG); I.layer(wing_mask(64, 60, 1, 1.2), DRAG_D); I.layer(bust_mask(64, 76, 0.6), SLATE)
    elif name == 'DragonPresence':     # 龙威：竖瞳龙眼 + 放射的威压
        rays(I, 64, 64, 40, 58, 14, DRAG_D, w=4); I.layer(eye_mask(64, 64, 70, 34), GOLD); I.layer(ellipse_mask(64, 64, 6, 24), INKC, shade=False)
    # ============ 1.104.0 恶鬼之血 ============
    elif name == 'FiendEyes':          # 恶鬼之眼
        I.layer(eye_mask(64, 64, 88, 40), FIEND); I.layer(ellipse_mask(64, 64, 14), GOLD); I.layer(ellipse_mask(64, 64, 4, 12), INKC, shade=False)
    elif name == 'FiendSkin':          # 恶鬼之皮：变红的手
        I.layer(palm_mask(64, 74, 1.0), FIEND)
        for x, y in ((50, 60), (76, 66), (62, 86)): I.layer(arc_mask(x, y, 6, 0, 360, 2), FIEND_D, shade=False)
    elif name == 'FiendTongue':        # 恶鬼之舌：分叉的舌头
        I.layer(ellipse_mask(64, 44, 40, 22), INKC); I.layer(poly_mask([(52, 46), (76, 46), (72, 96), (66, 84), (62, 84), (56, 96)]), FIEND)
    elif name == 'FiendAwaken':        # 血脉觉醒：铁笼里的两团红光
        I.layer(rrect_mask(24, 24, 104, 108, 6), INKC)
        I.layer(ellipse_mask(48, 66, 14), FIEND, glow=FIEND); I.layer(ellipse_mask(80, 66, 14), FIEND, glow=FIEND)
        for x in (36, 56, 76, 96): I.layer(line_mask([(x, 26), (x, 106)], 5), IRON)
    elif name == 'FiendBound':         # 血肉固化：被锁链钉住的红心
        I.layer(heart_mask(64, 64, 0.9), FIEND)
        for a in (45, 135): r = math.radians(a); I.layer(line_mask([(64 - math.cos(r) * 52, 64 - math.sin(r) * 52), (64 + math.cos(r) * 52, 64 + math.sin(r) * 52)], 6), IRON)
    # ============ 1.106.0 暗影宗 ============
    elif name == 'ShadowArts':         # 暗影技艺：掌心升起的黑雾
        for x, y, r in ((44, 50, 22), (80, 44, 24), (62, 30, 18)): I.layer(ellipse_mask(x, y, r), DUSK_D)
        I.layer(palm_mask(62, 92, 0.75), BONE)
    elif name == 'ShadowStep':         # 暗影步：两团暗影之间的人影
        I.layer(ellipse_mask(30, 96, 22, 10), INKC); I.layer(ellipse_mask(98, 96, 22, 10), INKC)
        I.layer(bust_mask(98, 70, 0.7), DUSK); I.layer(arc_mask(64, 96, 34, 200, 340, 4), DUSK_D, shade=False)
    elif name == 'Cloak':              # 幽影斗篷：淡去的兜帽人
        hood(I, 64, 64, DUSK_D); I.layer(ellipse_mask(64, 104, 40, 10), INKC)
    elif name == 'Opportunist':        # 灵机一斩：两把交错的拳与匕首
        I.layer(fist_mask(44, 70, 0.8), BONE); dagger(I, 84, 52, -45, 56)
    # ============ 1.106.0 散打宗 ============
    elif name == 'Technique':          # 散打技巧：张开的手掌与三道气流
        I.layer(palm_mask(64, 70, 1.0), BONE)
        for a in (-50, 0, 50): r = math.radians(a - 90); I.layer(line_mask([(64 + math.cos(r) * 40, 60 + math.sin(r) * 40), (64 + math.cos(r) * 56, 60 + math.sin(r) * 56)], 5), GOLD, shade=False)
    elif name == 'Wholeness':          # 混元体：冥想者胸口的金色光
        I.layer(bust_mask(64, 70, 1.1), KHAKI); rays(I, 64, 70, 16, 30, 10, GOLD, w=3); I.layer(ellipse_mask(64, 70, 10), GOLD)
    elif name == 'Tranquility':        # 宁神境：莲花
        for a in range(-60, 61, 30): I.layer(leaf_mask(64, 96, 64 + a * 0.7, 40 + abs(a) * 0.4, 14), (230, 200, 210) if a else (250, 230, 236))
        I.layer(ellipse_mask(64, 100, 34, 8), MOSS)
    elif name == 'Palm':               # 渗透劲：掌印 + 体内的波纹
        I.layer(bust_mask(64, 66, 1.1), STONE_D); ripples(I, 64, 70, (10, 20, 30), GOLD, 3); I.layer(palm_mask(64, 70, 0.45), BONE)
    elif name == 'Detonate':           # 引爆：从人影里炸开的金光
        I.layer(bust_mask(64, 70, 0.9), STONE_D); rays(I, 64, 64, 30, 58, 12, GOLD, w=5)
    # ============ 1.107.0 塑能学派 ============
    elif name == 'EvoSavant':          # 塑能学者：翻开的书 + 火苗
        I.layer(poly_mask([(16, 92), (64, 80), (112, 92), (112, 104), (64, 94), (16, 104)]), RUST_D)
        I.layer(poly_mask([(20, 88), (62, 76), (62, 44), (20, 56)]), BONE); I.layer(poly_mask([(108, 88), (66, 76), (66, 44), (108, 56)]), BONE_D)
        I.layer(flame_mask(64, 30, 0.45), EVO)
    elif name == 'EvoSculpt':          # 法术塑形：火圈里留出的安全人影
        I.layer(arc_mask(64, 64, 46, 0, 300, 12), EVO); I.layer(bust_mask(64, 70, 0.8), SILV)
    elif name == 'EvoPotent':          # 强力戏法：三枚加粗的飞弹
        for y in (36, 64, 92): I.layer(line_mask([(18, y), (90, y)], 9), ARC); I.layer(ellipse_mask(96, y, 11), EVO)
    elif name == 'EvoEmpowered':       # 强效塑能：带脑纹的爆炸
        rays(I, 64, 64, 24, 54, 12, EVO, w=6); I.layer(ellipse_mask(64, 64, 24, 20), (220, 160, 170)); I.layer(line_mask([(64, 46), (64, 82)], 3), (160, 100, 110), shade=False)
    elif name == 'EvoOverchannel':     # 超限导能：裂开的手掌喷出巨火
        I.layer(flame_mask(64, 44, 1.2), EVO); I.layer(palm_mask(64, 92, 0.7), BONE); I.layer(line_mask([(54, 84), (60, 98), (52, 108)], 3), RED_D, shade=False)
    # ============ 1.107.0 书士会 ============
    elif name == 'ScribeQuill':        # 法师笔：发光的羽毛笔
        I.layer(leaf_mask(36, 104, 100, 22, 22), SCRIBE); I.layer(line_mask([(28, 112), (58, 78)], 4), INKC); I.layer(ellipse_mask(26, 114, 5), SCRIBE_D)
    elif name == 'ScribeTome':         # 觉醒魔典：长了眼睛的书
        I.layer(rrect_mask(28, 22, 100, 108, 8), SCRIBE_D); I.layer(rrect_mask(36, 28, 96, 102, 6), BONE)
        I.layer(eye_mask(66, 64, 44, 22), SCRIBE); I.layer(ellipse_mask(66, 64, 7), INKC, shade=False)
    elif name == 'ScribeManifest':     # 思维具现：飘在半空的灵体书
        I.layer(poly_mask([(20, 64), (64, 50), (108, 64), (108, 80), (64, 68), (20, 80)]), SCRIBE, glow=SCRIBE)
        for x in (40, 64, 88): I.layer(ellipse_mask(x, 100, 5), SCRIBE_D, outline=False)
    elif name == 'ScribeMaster':       # 大书士：卷轴 + 星
        I.layer(rrect_mask(30, 28, 98, 100, 6), BONE); I.layer(ellipse_mask(30, 64, 10, 40), BONE_D); I.layer(ellipse_mask(98, 64, 10, 40), BONE_D)
        I.layer(poly_mask(star_pts(64, 64, 22, 9, 5)), SCRIBE)
    elif name == 'ScribeOneWord':      # 一字同心：书挡在人前
        I.layer(bust_mask(80, 76, 0.9), STONE_D); I.layer(rrect_mask(20, 36, 62, 100, 6), SCRIBE, glow=SCRIBE)
    # ============ 1.108.0 神龙宗 ============
    elif name == 'DragonDisciple':     # 龙之徒：拳头带火焰
        I.layer(flame_mask(70, 46, 0.8), DRAG); I.layer(fist_mask(58, 78, 0.9), BONE)
    elif name == 'DragonElement':      # 龙之打击·元素：五色珠
        for i, c in enumerate((DRAG, (170, 210, 240), GOLD, MOSS, ABER)): a = math.radians(-90 + i * 72); I.layer(ellipse_mask(64 + math.cos(a) * 32, 64 + math.sin(a) * 32, 13), c)
        I.layer(ellipse_mask(64, 64, 12), DRAG_D)
    elif name == 'DragonBreath':       # 龙之息：龙头喷出锥形火
        I.layer(poly_mask([(52, 64), (116, 24), (116, 104)]), DRAG, glow=DRAG); I.layer(ellipse_mask(34, 64, 24, 20), DRAG_D); I.layer(ellipse_mask(30, 56, 4), GOLD, shade=False)
    elif name == 'DragonWings':        # 龙翼翱翔：虚幻龙翼 + 风
        I.layer(wing_mask(64, 58, -1, 1.1), (230, 170, 120)); I.layer(wing_mask(64, 58, 1, 1.1), (230, 170, 120)); I.layer(bust_mask(64, 76, 0.65), KHAKI)
        ripples(I, 64, 110, (14, 26), CREAM_D, 3, 200, 340)
    elif name == 'DragonAspect':       # 古龙姿：人影背后的巨龙头影
        I.layer(bust_mask(64, 46, 1.5), DRAG_D); I.layer(ellipse_mask(50, 40, 6), GOLD, shade=False); I.layer(ellipse_mask(78, 40, 6), GOLD, shade=False)
        I.layer(arc_mask(64, 84, 40, 180, 360, 6), DRAG); I.layer(bust_mask(64, 86, 0.6), KHAKI)
    elif name == 'DragonAscendant':    # 神龙身：龙眼 + 光环
        I.layer(arc_mask(64, 64, 50, 0, 360, 6), GOLD); I.layer(eye_mask(64, 64, 64, 30), DRAG); I.layer(ellipse_mask(64, 64, 5, 20), INKC, shade=False)
    # ============ 1.109.0 大地结社 ============
    elif name == 'LandSpell':          # 大地法术：四季四色的圆
        for i, c in enumerate(((170, 210, 240), MOSS, (220, 180, 100), ABER)): a0 = i * 90; I.layer(arc_mask(64, 64, 34, a0, a0 + 90, 26), c)
        I.layer(ellipse_mask(64, 64, 12), BONE)
    elif name == 'NaturalRecovery':    # 自然恢复：树下的人
        I.layer(ellipse_mask(64, 42, 40, 30), MOSS); I.layer(rrect_mask(58, 60, 70, 108, 3), KHAKI_D); I.layer(bust_mask(36, 92, 0.55), STONE)
    elif name == 'Terraform':          # 大地塑形：土地裂开涌出水
        I.layer(poly_mask([(10, 70), (118, 70), (118, 112), (10, 112)]), KHAKI_D); I.layer(ellipse_mask(64, 84, 36, 14), (90, 150, 200)); I.layer(palm_mask(64, 40, 0.6), BONE)
    elif name == 'TerraMode':          # 地形切换：三块地砖
        for i, c in enumerate(((90, 150, 200), MOSS_D, KHAKI)): I.layer(rrect_mask(14 + i * 36, 40, 44 + i * 36, 88, 4), c)
    elif name == 'LandStride':         # 大地行者：踩过草丛的脚印
        for x, y in ((40, 96), (62, 70), (84, 44)): I.layer(ellipse_mask(x, y, 10, 14), KHAKI_D)
        for x in (24, 104): I.layer(leaf_mask(x, 110, x + 6, 70, 10), MOSS)
    elif name == 'NatureWard':         # 自然守护：叶子盾
        I.layer(poly_mask(shield_pts(64, 66, 64, 76)), MOSS_D); I.layer(leaf_mask(64, 96, 64, 36, 22), MOSS)
    elif name == 'Sanctuary':          # 自然庇护所：伏在人身边的狼
        I.layer(bust_mask(80, 70, 0.9), STONE); I.layer(ellipse_mask(40, 92, 26, 14), STONE_D); I.layer(ellipse_mask(24, 80, 10), STONE_D); I.layer(arc_mask(64, 64, 52, 200, 340, 5), MOSS, shade=False)
    # ============ 1.109.0 孢子结社 ============
    elif name == 'SporeHalo':          # 死亡光环：一圈孢子
        for a in range(0, 360, 30): r = math.radians(a); I.layer(ellipse_mask(64 + math.cos(r) * 40, 64 + math.sin(r) * 40, 6), SPORE)
        I.layer(ellipse_mask(64, 64, 16), SPORE_D)
    elif name == 'RotTouch':           # 腐烂之触：发霉的手
        I.layer(palm_mask(64, 74, 0.9), (150, 140, 110)); [I.layer(ellipse_mask(x, y, 5), MOSS_D) for x, y in ((54, 60), (72, 70), (60, 84))]
    elif name == 'Symbiotic':          # 共生实体：蘑菇护着的人
        I.layer(bust_mask(64, 80, 0.8), STONE); I.layer(ellipse_mask(64, 36, 40, 18), SPORE); I.layer(rrect_mask(58, 36, 70, 56, 3), BONE)
    elif name == 'Infestation':        # 真菌滋长：长着蘑菇的人影站起来
        I.layer(bust_mask(64, 74, 1.0), SPORE_D); I.layer(ellipse_mask(48, 40, 12, 7), SPORE); I.layer(ellipse_mask(78, 34, 14, 8), SPORE)
    elif name == 'FungalBody':         # 真菌之躯：菌褶纹的盾
        I.layer(ellipse_mask(64, 64, 48), SPORE); [I.layer(line_mask([(64, 64), (64 + math.cos(math.radians(a)) * 44, 64 + math.sin(math.radians(a)) * 44)], 3), SPORE_D, shade=False) for a in range(0, 360, 30)]
    # ============ 1.109.0 群星结社 ============
    elif name == 'StarBolt':           # 指引箭：带星的光箭
        I.layer(line_mask([(18, 110), (96, 32)], 8), STAR); I.layer(poly_mask(star_pts(100, 28, 18, 7, 5)), CREAM)
    elif name == 'StarryForm':         # 星象形态：星点连成的人形
        for x, y in ((64, 24), (44, 54), (84, 54), (64, 70), (48, 104), (80, 104)): I.layer(poly_mask(star_pts(x, y, 9, 4, 5)), STAR)
        for a, b in (((64, 24), (64, 70)), ((44, 54), (84, 54)), ((64, 70), (48, 104)), ((64, 70), (80, 104))): I.layer(line_mask([a, b], 2), STAR_D, shade=False, outline=False)
    elif name == 'FormMode':           # 星座切换：弓、杯、龙三颗星
        I.layer(arc_mask(30, 64, 18, 120, 240, 4), STAR); I.layer(poly_mask([(54, 50), (74, 50), (68, 76), (60, 76)]), GOLD); I.layer(eye_mask(98, 64, 30, 14), STAR_D)
    elif name == 'CosmicOmen':         # 宇宙预兆：一半太阳一半月亮
        I.layer(ellipse_mask(64, 64, 44), GOLD); I.layer(ellipse_mask(80, 56, 38), DUSK_D)
    elif name == 'FullOfStars':        # 全星辉：发光的半透明人形
        I.layer(bust_mask(64, 66, 1.2), STAR, glow=STAR); [I.layer(poly_mask(star_pts(x, y, 6, 2.5, 5)), CREAM, shade=False) for x, y in ((50, 50), (76, 60), (60, 86))]
    # ============ 1.110.0 生命领域 ============
    elif name == 'LifeDisciple':       # 生命门徒：捧着的心
        I.layer(heart_mask(64, 52, 0.8), (230, 120, 130)); I.layer(palm_mask(44, 96, 0.55, 20), BONE); I.layer(palm_mask(84, 96, 0.55, -20), BONE_D)
    elif name == 'LifePreserve':       # 保全生命：一圈小心
        for a in range(0, 360, 60): r = math.radians(a); I.layer(heart_mask(64 + math.cos(r) * 36, 64 + math.sin(r) * 36, 0.3), (230, 120, 130))
        rays(I, 64, 64, 8, 22, 8, HOLY, w=3)
    elif name == 'LifeBlessed':        # 受祝的治疗者：两颗心相连
        I.layer(heart_mask(44, 64, 0.5), (230, 120, 130)); I.layer(heart_mask(84, 64, 0.5), (240, 170, 170)); I.layer(line_mask([(56, 64), (72, 64)], 4), HOLY, shade=False)
    elif name == 'LifeStrike':         # 神圣打击·光耀：发光的钉锤
        I.layer(line_mask([(36, 104), (82, 46)], 7), KHAKI_D); I.layer(ellipse_mask(86, 42, 18), HOLY, glow=HOLY)
    elif name == 'LifeSupreme':        # 至高治疗：光芒中的十字心
        rays(I, 64, 64, 30, 56, 14, HOLY, w=4); I.layer(heart_mask(64, 64, 0.8), (240, 150, 150)); I.layer(rrect_mask(60, 46, 68, 80, 2), CREAM, shade=False)
    # ============ 1.110.0 光明领域 ============
    elif name == 'LightFlare':         # 守护之光：盾前的强光
        I.layer(poly_mask(shield_pts(54, 68, 54, 64)), SILV_D); rays(I, 88, 46, 8, 30, 10, HOLY, w=4); I.layer(ellipse_mask(88, 46, 8), CREAM)
    elif name == 'LightOrb':           # 光亮术：一团悬浮的光
        I.layer(ellipse_mask(64, 60, 24), HOLY, glow=HOLY); I.layer(ellipse_mask(56, 52, 7), CREAM, shade=False)
    elif name == 'LightDawn':          # 破晓之光：地平线上的太阳
        I.layer(ellipse_mask(64, 86, 34), GOLD); rays(I, 64, 86, 40, 60, 9, HOLY, w=4, a0=180); I.layer(rrect_mask(8, 86, 120, 120, 0), DUSK_D, shade=False)
    elif name == 'LightImproved':      # 光之守卫强化：三面光盾
        for x in (32, 64, 96): I.layer(poly_mask(shield_pts(x, 70, 28, 34)), HOLY)
        rays(I, 64, 30, 6, 18, 8, CREAM_D, w=3)
    elif name == 'LightPotent':        # 强力施法：烈焰般的光柱
        I.layer(poly_mask([(48, 112), (80, 112), (72, 16), (56, 16)]), HOLY, glow=HOLY)
    elif name == 'LightCorona':        # 晨光之冕：发光的王冠
        rays(I, 64, 64, 34, 56, 16, HOLY, w=4); I.layer(poly_mask([(30, 80), (38, 48), (52, 68), (64, 40), (76, 68), (90, 48), (98, 80)]), GOLD)
    # ============ 1.110.0 熔炉领域 ============
    elif name == 'ForgeBlessing':      # 熔炉祝福：铁砧上发光的剑
        I.layer(poly_mask([(24, 88), (104, 88), (92, 104), (36, 104)]), IRON); I.layer(rrect_mask(52, 104, 76, 118, 2), IRON_D); sword(I, 64, 62, -10, 60, SILV, RUST); I.layer(poly_mask(star_pts(96, 32, 10, 4, 4)), HOLY)
    elif name == 'ForgeArtisan':       # 工匠祝福：锤子砸出火花
        I.layer(line_mask([(28, 104), (76, 40)], 8), KHAKI_D); I.layer(rrect_mask(60, 22, 100, 46, 4), IRON); rays(I, 96, 86, 6, 20, 8, GOLD, w=3)
    elif name == 'ForgeSoul':          # 熔炉之魂：火中的重甲
        I.layer(flame_mask(64, 58, 1.2), RUST); I.layer(poly_mask(shield_pts(64, 70, 50, 60)), IRON)
    elif name == 'ForgeStrike':        # 神圣打击·熔火：烧红的锤
        I.layer(line_mask([(36, 104), (80, 48)], 7), KHAKI_D); I.layer(rrect_mask(66, 26, 106, 56, 5), RUST, glow=RUST)
    elif name == 'ForgeSaint':         # 熔炉圣徒：火焰光环中的铁盔
        I.layer(flame_mask(64, 54, 1.3), GOLD); I.layer(rrect_mask(38, 42, 90, 104, 14), IRON); I.layer(rrect_mask(46, 66, 82, 74, 2), INKC, shade=False)
    return I.done(128)

ICONS3 = ['ShadowArts', 'ShadowStep', 'Cloak', 'Opportunist', 'Technique', 'Wholeness', 'Tranquility', 'Palm', 'Detonate',
          'EvoSavant', 'EvoSculpt', 'EvoPotent', 'EvoEmpowered', 'EvoOverchannel', 'ScribeQuill', 'ScribeTome', 'ScribeManifest', 'ScribeMaster', 'ScribeOneWord',
          'DragonDisciple', 'DragonElement', 'DragonBreath', 'DragonWings', 'DragonAspect', 'DragonAscendant',
          'LandSpell', 'NaturalRecovery', 'Terraform', 'TerraMode', 'LandStride', 'NatureWard', 'Sanctuary',
          'SporeHalo', 'RotTouch', 'Symbiotic', 'Infestation', 'FungalBody',
          'StarBolt', 'StarryForm', 'FormMode', 'CosmicOmen', 'FullOfStars',
          'LifeDisciple', 'LifePreserve', 'LifeBlessed', 'LifeStrike', 'LifeSupreme', 'LightFlare', 'LightOrb', 'LightDawn', 'LightImproved', 'LightPotent', 'LightCorona',
          'ForgeBlessing', 'ForgeArtisan', 'ForgeSoul', 'ForgeStrike', 'ForgeSaint']
ICONS = ['WeaponBond', 'EKShield', 'ThunderBlade', 'AbsorbElements', 'WarMagic', 'ArcaneCharge', 'EldritchStrike', 'Bladestorm',
         'PsionicStrike', 'ProtectiveField', 'TeleJump', 'PsiAdept', 'Shove', 'GuardianField', 'TeleStorm',
         'BloodSpear', 'BloodBind', 'FleshShaping', 'Siphon', 'BloodBoil', 'FleshPrison', 'BloodRite',
         'SteelDefender', 'BattleReady', 'ArcaneJolt', 'ImprovedDefender', 'MasterSmith',
         'CalmMind', 'PsychicSpike', 'PsychicDefense', 'FleshRevelation', 'MindDominion',
         'ConqueringPresence', 'GuidedStrike', 'AuraOfConquest', 'ScornfulRebuke', 'InvincibleConqueror',
         'EmissaryOfPeace', 'RebukeViolent', 'AuraOfGuardian', 'ProtectiveSpirit', 'EmissaryOfRedemption', 'Reconcile',
         'DragonBlood', 'DragonFlight', 'DragonPresence', 'FiendEyes', 'FiendSkin', 'FiendTongue', 'FiendAwaken', 'FiendBound']
EMBLEMS = {'EldritchKnight': ARC_D, 'PsiWarrior': PSI_D, 'Hemomancy': RED_D, 'BattleSmith': BRASS_D, 'AberrantMind': ABER_D, 'Conquest': IRON_D, 'Redemption': SILV_D, 'DraconicSoul': DRAG_D, 'FiendBlood': FIEND_D, 'ShadowWay': DUSK_D, 'OpenHandWay': KHAKI_D, 'Evocation': RUST_D, 'Scribes': SCRIBE_D, 'DragonWay': DRAG_D, 'CircleLand': MOSS_D, 'CircleSpores': SPORE_D, 'CircleStars': STAR_D, 'LifeDomain': GOLD_D, 'LightDomain': (190, 160, 60), 'ForgeDomain': RUST_D}

def emblem(sub):
    I = Icon(); col = EMBLEMS[sub]
    I.layer(poly_mask(shield_pts(64, 64, 100, 116)), GOLD_D)
    I.layer(poly_mask(shield_pts(64, 62, 86, 100)), col)
    if sub == 'EldritchKnight': sword(I, 64, 62, -90, 84); I.layer(arc_mask(64, 50, 22, 0, 360, 4), ARC, shade=False)
    elif sub == 'PsiWarrior': I.layer(fist_mask(64, 66, 0.8), BONE); ripples(I, 64, 60, (30,), PSI, 4)
    elif sub == 'Hemomancy': drop(I, 64, 62, 1.1, RED)
    elif sub == 'BattleSmith': bot(I, 64, 68, 0.75)
    elif sub == 'AberrantMind': tentacle(I, 48, 100, 8); tentacle(I, 80, 100, -8, ABER); I.layer(eye_mask(64, 50, 40, 18), BONE); I.layer(ellipse_mask(64, 50, 5, 12), INKC, shade=False)
    elif sub == 'Conquest': I.layer(rrect_mask(40, 34, 88, 92, 14), IRON); I.layer(rrect_mask(48, 56, 80, 63, 2), INKC, shade=False)
    elif sub == 'DraconicSoul': I.layer(wing_mask(64, 60, -1, 0.75), DRAG); I.layer(wing_mask(64, 60, 1, 0.75), DRAG); I.layer(eye_mask(64, 60, 36, 16), GOLD); I.layer(ellipse_mask(64, 60, 3, 10), INKC, shade=False)
    elif sub == 'FiendBlood':
        drop(I, 64, 62, 1.0, FIEND)
        for x in (44, 64, 84): I.layer(line_mask([(x, 32), (x, 98)], 4), IRON)
    elif sub == 'ShadowWay': hood(I, 64, 60, DUSK)
    elif sub == 'OpenHandWay': I.layer(palm_mask(64, 66, 0.85), BONE)
    elif sub == 'Evocation': I.layer(flame_mask(64, 60, 0.85), EVO)
    elif sub == 'Scribes': I.layer(leaf_mask(40, 92, 92, 30, 18), BONE); I.layer(line_mask([(34, 98), (54, 76)], 4), INKC)
    elif sub == 'DragonWay': I.layer(fist_mask(64, 70, 0.7), BONE); I.layer(wing_mask(64, 56, -1, 0.6), DRAG); I.layer(wing_mask(64, 56, 1, 0.6), DRAG)
    elif sub == 'CircleLand': I.layer(leaf_mask(64, 96, 64, 30, 26), MOSS)
    elif sub == 'CircleSpores': I.layer(ellipse_mask(64, 52, 30, 16), SPORE); I.layer(rrect_mask(58, 52, 70, 92, 3), BONE)
    elif sub == 'CircleStars': I.layer(poly_mask(star_pts(64, 60, 28, 11, 5)), STAR)
    elif sub == 'LifeDomain': I.layer(heart_mask(64, 62, 0.7), (240, 150, 150))
    elif sub == 'LightDomain': rays(I, 64, 60, 18, 34, 10, HOLY, w=4); I.layer(ellipse_mask(64, 60, 14), CREAM)
    elif sub == 'ForgeDomain': I.layer(poly_mask([(30, 70), (98, 70), (88, 84), (40, 84)]), IRON); I.layer(flame_mask(64, 48, 0.6), RUST)
    elif sub == 'Redemption': I.layer(wing_mask(64, 58, -1, 0.75), SILV); I.layer(wing_mask(64, 58, 1, 0.75), SILV); I.layer(heart_mask(64, 64, 0.4), HOLY)
    return I.done(128)

def defender(facing):
    I = Icon()
    if facing == 'south':
        bot(I, 64, 70, 1.25)
    elif facing == 'north':
        I.layer(rrect_mask(31, 47, 97, 95, 10), BRASS_D); I.layer(rrect_mask(44, 22, 84, 50, 8), BRASS_D)
        for sx in (-1, 1): I.layer(rrect_mask(64 + sx * 38 - 9, 57, 64 + sx * 38 + 9, 98, 5), BRASS)
        for sx in (-1, 1): I.layer(rrect_mask(64 + sx * 18 - 9, 92, 64 + sx * 18 + 9, 112, 4), IRON)
    else:
        I.layer(rrect_mask(38, 47, 92, 95, 10), BRASS); I.layer(rrect_mask(58, 22, 94, 50, 8), BRASS)
        I.layer(ellipse_mask(86, 36, 6), PSI, outline=False, shade=False)
        I.layer(rrect_mask(70, 57, 90, 98, 5), BRASS_D)
        for x in (50, 78): I.layer(rrect_mask(x - 9, 92, x + 9, 112, 4), IRON)
    return I.done(128)

if __name__ == '__main__':
    out = os.path.join(ROOT, 'Textures', 'LP', 'Classes'); pv = os.path.join(ROOT, 'ArtSource', 'Preview'); os.makedirs(pv, exist_ok=True)
    os.makedirs(os.path.join(out, 'Subclass2'), exist_ok=True); os.makedirs(os.path.join(out, 'Subclass'), exist_ok=True)
    dd = os.path.join(ROOT, 'Textures', 'LP', 'SteelDefender'); os.makedirs(dd, exist_ok=True)
    os.makedirs(os.path.join(out, 'Subclass3'), exist_ok=True)
    items = [('Subclass2', n, icon(n)) for n in ICONS] + [('Subclass3', n, icon(n)) for n in ICONS3] + [('Subclass', 'Emblem_' + s, emblem(s)) for s in EMBLEMS]
    for f in ('south', 'north', 'east'):
        im = defender(f); im.save(os.path.join(dd, 'SteelDefender_' + f + '.png')); items.append(('-', 'Defender_' + f, im))
    cols = 10; cell = 150; rows = (len(items) + cols - 1) // cols
    sheet = Image.new('RGBA', (cols * cell + 20, rows * (cell + 24) + 30), (46, 48, 52, 255)); dr = ImageDraw.Draw(sheet)
    for i, (sub, n, im) in enumerate(items):
        if sub != '-': im.save(os.path.join(out, sub, n + '.png'))
        x = 10 + (i % cols) * cell + 11; y = 20 + (i // cols) * (cell + 24)
        sheet.paste(im, (x, y), im); dr.text((x, y + 130), n, fill=(220, 220, 210, 255))
    sheet.save(os.path.join(pv, 'Subclasses2-icons.png'))
    print('subclass2 art written:', len(ICONS), 'icons +', len(EMBLEMS), 'emblems + defender')
