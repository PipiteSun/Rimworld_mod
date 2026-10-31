# 1.88.0 职业专属专长图标（128×128）：专长的圆底（职业色、粗黑描边、右下暗部）+ 缩小的职业能力图标 + 右下角金色档位标（一竖 = 3 级，两竖 = 7 级）。
# 用法：python draw_class_feats_188.py <Mod 根目录>
import os, sys
from PIL import Image, ImageDraw

ROOT = sys.argv[1] if len(sys.argv) > 1 else '.'
SRC = os.path.join(ROOT, 'Textures', 'LP', 'Classes')
OUT = os.path.join(ROOT, 'Textures', 'LP', 'Feats', 'Class')
OUTLINE = (34, 30, 36, 255)
GOLD = (214, 178, 92, 255); GOLD_D = (150, 116, 52, 255)

# 名字 → (职业文件夹, 借用的能力图标, 圆底颜色, 档位)
FEATS = {
    'FighterVeteran': ('Fighter', 'Indomitable', (150, 120, 92), 1),
    'FighterHundredBattles': ('Fighter', 'SurgeOfBattle', (150, 120, 92), 2),
    'MonkBreath': ('Monk', 'DiamondSoul', (120, 150, 160), 1),
    'MonkShadowless': ('Monk', 'StepOfTheWind', (120, 150, 160), 2),
    'PaladinOathkeeper': ('Paladin', 'AuraOfProtection', (196, 172, 104), 1),
    'PaladinZealot': ('Paladin', 'DivineSmite', (196, 172, 104), 2),
    'WizardScholar': ('Wizard', 'MageArmor', (96, 104, 160), 1),
    'WizardSculptor': ('Wizard', 'MagicMissile', (96, 104, 160), 2),
    'ClericDevout': ('Cleric', 'Bless', (200, 190, 150), 1),
    'ClericBlessedHands': ('Cleric', 'CureWounds', (200, 190, 150), 2),
    'DruidWildChild': ('Druid', 'PlantGrowth', (110, 140, 96), 1),
    'DruidWarden': ('Druid', 'Barkskin', (110, 140, 96), 2),
    'SorcererWellspring': ('Sorcerer', 'SorcerousSurge', (160, 90, 110), 1),
    'SorcererResonance': ('Sorcerer', 'EmpoweredSpell', (160, 90, 110), 2),
    'ArtificerTinker': ('Artificer', 'QuickRepair', (150, 130, 100), 1),
    'ArtificerMasterwork': ('Artificer', 'MasterCraft', (150, 130, 100), 2),
    'BarbarianSmolder': ('Barbarian', 'Rage', (160, 80, 64), 1),
    'BarbarianFeralBody': ('Barbarian', 'RelentlessRage', (160, 80, 64), 2),
    'RangerWildHunter': ('Ranger', 'HuntersMark', (100, 128, 90), 1),
    'RangerStalker': ('Ranger', 'FoeSlayer', (100, 128, 90), 2),
    'RogueShadowVeteran': ('Rogue', 'UncannyDodge', (84, 84, 100), 1),
    'RogueDeadlyPrecision': ('Rogue', 'SneakAttack', (84, 84, 100), 2),
    'WarlockDeepPact': ('Warlock', 'Invocations', (110, 80, 130), 1),
    'WarlockPatronFavor': ('Warlock', 'DarkBlessing', (110, 80, 130), 2),
}

def dark(c, k=.72):
    return tuple(int(v * k) for v in c[:3]) + (255,)

def make(name, folder, ability, color, tier):
    S = 4; W = 128 * S
    img = Image.new('RGBA', (W, W), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    r = 58 * S; c = W // 2
    d.ellipse([c - r, c - r, c + r, c + r], fill=OUTLINE)
    r2 = r - 5 * S
    d.ellipse([c - r2, c - r2, c + r2, c + r2], fill=color + (255,))
    # 右下暗部：同心偏移的暗色月牙
    shade = Image.new('L', (W, W), 0); sd = ImageDraw.Draw(shade)
    sd.ellipse([c - r2, c - r2, c + r2, c + r2], fill=255)
    sd.ellipse([c - r2 - 10 * S, c - r2 - 10 * S, c + r2 - 10 * S, c + r2 - 10 * S], fill=0)
    img.paste(Image.new('RGBA', (W, W), dark(color)), (0, 0), shade)
    icon = Image.open(os.path.join(SRC, folder, ability + '.png')).convert('RGBA')
    size = int(84 * S)
    icon = icon.resize((size, size), Image.LANCZOS)
    img.alpha_composite(icon, (c - size // 2, c - size // 2 - 2 * S))
    # 档位标
    bx, by, br = 100 * S, 100 * S, 19 * S
    d = ImageDraw.Draw(img)
    d.ellipse([bx - br, by - br, bx + br, by + br], fill=OUTLINE)
    d.ellipse([bx - br + 4 * S, by - br + 4 * S, bx + br - 4 * S, by + br - 4 * S], fill=GOLD)
    xs = [bx] if tier == 1 else [bx - 5 * S, bx + 5 * S]
    for x in xs:
        d.rectangle([x - 2 * S, by - 9 * S, x + 2 * S, by + 9 * S], fill=OUTLINE)
    img = img.resize((128, 128), Image.LANCZOS)
    img.save(os.path.join(OUT, name + '.png'))

os.makedirs(OUT, exist_ok=True)
for n, (f, a, col, t) in FEATS.items():
    make(n, f, a, col, t)
print(len(FEATS), 'icons ->', OUT)
