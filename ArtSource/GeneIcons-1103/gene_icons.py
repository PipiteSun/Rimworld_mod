# LichPhylactery gene icons, redrawn in the Alpha Genes / VRE style (1.104.0 hotfix, 2026-11-03).
# Usage: python gene_icons.py <out_root> [filter]
#   out_root = the mod's Textures folder (or a scratch folder for previews).
# Each entry key is the texture path used by <iconPath> (shared ability/xenotype
# icons are listed too, so they stay in the same style).
import os
import sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from motifs import *

ICONS = {}


def icon(path):
    def deco(f):
        ICONS[path] = f
        return f
    return deco


def up(c, **kw):
    arrow(c, True, **kw)


def down(c, **kw):
    arrow(c, False, **kw)


def demon_bust(c, col=CRIM, hcol=DARK, size=1.0, wide=1.0):
    with c.at(64 - 64 * size, 128 - 128 * size, size):
        torso(c, col, wide)
        horns_devil(c, hcol)
        head(c, col=col)
        eyes(c, 64, 52, 9, 3.2, YELLOW)


def sized_bust(c, col=W, k=1.0, wide=1.0, hfn=None):
    with c.at(64 - 64 * k, 128 - 128 * k, k):
        torso(c, col, wide)
        if hfn:
            hfn(c)
        else:
            head(c, col=col)


# ======================= Aarakocra =======================
@icon("Races124/Genes/AarakocraWings")
def _(c):
    feather_wings(c, TAN, 70, 0.95, 4)


@icon("Races124/Genes/AarakocraHollowBones")
def _(c):
    bone(c, BONE)
    c.detail(c.ell(64, 64, 10, 5, -35), (150, 140, 120))
    down(c)


@icon("Races124/Genes/AarakocraSkyborn")
def _(c):
    cloud(c, W, 84)
    for x, y, s in ((44, 30, 1.0), (80, 46, 0.7)):
        c.put(c.line([(x - 18 * s, y - 8 * s), (x - 6 * s, y), (x, y - 4 * s), (x + 6 * s, y), (x + 18 * s, y - 8 * s)], 6), DARK, shade=0)
    up(c, cx=106, cy=34, s=0.8)


# ======================= Abyss =======================
@icon("Abyss/Genes/AbyssCore")
def _(c):
    smoke(c, (90, 80, 100))
    demon_bust(c)


@icon("Abyss/Genes/AbyssUndying")
def _(c):
    zzz(c)
    badge(c, mug, 2, 62, 0.5)
    cross(c, 34, 96, 1.1)


@icon("Abyss/Genes/DretchStature")
def _(c):
    demon_bust(c, (150, 150, 126), DARK, 0.72, 1.3)
    down(c)


@icon("Abyss/Genes/DretchStench")
def _(c):
    wavy(c, LGREEN, 3, 34, 30, 10, 76)
    badge(c, head, 34, 66, 0.5, col=(150, 150, 126))


@icon("Abyss/Genes/DretchToil")
def _(c):
    pick(c)
    down(c)


@icon("Abyss/Genes/BarlguraStature")
def _(c):
    ape_head(c, (170, 104, 60))
    up(c)


@icon("Abyss/Genes/BarlguraMight")
def _(c):
    muscle_arm(c, (170, 104, 60))
    up(c, cx=30, cy=34)


@icon("Abyss/Abilities/Leap")
def _(c):
    curved_arrow(c, ORANGE)
    footprint(c, G, 100, 104, 0.32)


@icon("Abyss/Genes/BarlguraStill")
def _(c):
    sized_bust(c, DARK)
    eyes(c, 64, 52, 9, 3.2, ORANGE)
    badge(c, hourglass, 70, 66, 0.44)


@icon("Abyss/Genes/BarlguraBrute")
def _(c):
    fist(c, (170, 104, 60))
    badge(c, skull, 66, 64, 0.46)


@icon("Abyss/Genes/HezrouStature")
def _(c):
    toad_head(c)
    up(c)


@icon("Abyss/Genes/HezrouHide")
def _(c):
    shield(c, OLIVE)
    for x, y, r in ((44, 40, 7), (80, 38, 6), (60, 62, 8), (88, 66, 6), (40, 74, 5), (66, 92, 6)):
        c.detail(c.ell(x, y, r), (112, 108, 64))


@icon("Abyss/Genes/HezrouStench")
def _(c):
    wavy(c, LGREEN, 3, 34, 30, 10, 80)
    skull(c, BONE, 92, 98, 0.42)


@icon("Abyss/Genes/SuccubusNature")
def _(c):
    horns_devil(c, DARK, 40, 22, 30)
    heart(c, PINK, 64, 70, 0.9)


@icon("Abyss/Genes/SuccubusHorns")
def _(c):
    torso(c, (214, 120, 130))
    horns_ram(c, DARK, 34)
    head(c, col=(214, 120, 130))


@icon("Abyss/Genes/SuccubusWings")
def _(c):
    bat_wings(c, (96, 60, 110), 64, 0.95, 4)


@icon("Abyss/Genes/SuccubusTail")
def _(c):
    tail_spade(c, (170, 70, 100))


@icon("Abyss/Abilities/Guise")
def _(c):
    mask(c, W)
    sparkle(c, 104, 22, 18)
    sparkle(c, 22, 100, 13)


@icon("Abyss/Abilities/Kiss")
def _(c):
    lips = bez((14, 64), (36, 30), (56, 36), (64, 46)) + bez((64, 46), (72, 36), (92, 30), (114, 64)) + \
        bez((114, 64), (90, 104), (38, 104), (14, 64))
    c.put(c.poly(lips), (196, 70, 100))
    c.detail(c.line(bez((18, 64), (64, 74), (110, 64)), 3), INK)
    heart(c, PINK, 104, 22, 0.3)


@icon("Abyss/Genes/SuccubusDream")
def _(c):
    moon(c, (236, 222, 150))
    heart(c, (196, 70, 100), 92, 92, 0.42)


@icon("Abyss/Genes/CambionBlood")
def _(c):
    horns_devil(c, DARK, 44, 20, 32)
    drop(c, CRIM)


@icon("Abyss/Genes/CambionHorns")
def _(c):
    torso(c, W)
    for s in (-1, 1):
        horn(c, (64 + s * 12, 30), (64 + s * 30, 20), (64 + s * 46, 34), 12, DARK)
    head(c)
    eyes(c, 64, 52, 9, 3.6, ORANGE)


@icon("Abyss/Abilities/Glide")
def _(c):
    bat_wings(c, CRIM, 54, 0.75, 4)
    curved_arrow_small = lambda cc: curved_arrow(cc, LBLUE)
    badge(c, curved_arrow, 62, 62, 0.5, col=LBLUE)


@icon("Abyss/Genes/CambionFire")
def _(c):
    flame(c, CRIM, ORANGE)
    badge(c, shield, 64, 64, 0.48, col=LG)


@icon("Abyss/Genes/CambionPresence")
def _(c):
    speech(c, W)
    horns_devil(c, CRIM, 30, 20, 26)
    exclaim_k = 0.6
    badge(c, exclaim, 26, 14, 0.58, col=CRIM)


@icon("Abyss/Genes/NabassuStature")
def _(c):
    bat_wings(c, DG, 70, 0.9, 6)
    sized_bust(c, G, 0.86)
    up(c)


@icon("Abyss/Genes/NabassuDevour")
def _(c):
    swirl(c, PURPLE, 64, 64, 56, 1.6, 9)
    skull(c, BONE, 64, 66, 0.7)


@icon("Abyss/Abilities/Gaze")
def _(c):
    eye(c, G)
    rock(c) if False else badge(c, rock, 66, 66, 0.46)


@icon("Abyss/Genes/ShadowDemonForm")
def _(c):
    ghost(c, DARK)
    c.detail(c.ell(50, 54, 6, 8) | c.ell(78, 54, 6, 8), LPURPLE)


# ======================= Ascension =======================
@icon("Ascension/Genes/DivineBlood")
def _(c):
    drop(c, GOLD)
    halo(c, YELLOW, 18, 38, 11)


# ======================= Giants =======================
@icon("Giants/Genes/CloudGiantCore")
def _(c):
    cloud(c, W, 96)
    crown(c, (210, 214, 224), 46, 44, LBLUE)


@icon("Giants/Genes/CloudNoble")
def _(c):
    crown(c, GOLD, 52, 50)
    heart(c, PINK, 100, 100, 0.34)


@icon("Giants/Genes/CloudGift")
def _(c):
    gift(c, BLUE, (226, 228, 236))


@icon("Giants/Genes/CloudGrace")
def _(c):
    speech(c, W)
    sparkle(c, 64, 54, 22, GOLD)
    sparkle(c, 98, 28, 12, GOLD)


@icon("Giants/Genes/FrostGiantCore")
def _(c):
    torso(c, (176, 206, 226), 1.1)
    for x in (40, 88):
        with c.at(x - 64 * 0.5, 54, 0.5):
            braid(c, (226, 210, 160))
    head(c, col=(176, 206, 226))
    badge(c, snowflake, 76, 4, 0.36)


@icon("Giants/Genes/FrostSnowWalker")
def _(c):
    snowflake(c, LBLUE)
    footprint(c, W, 48, 80, 0.42, -10)
    footprint(c, W, 86, 50, 0.42, 10)


@icon("Giants/Genes/FrostGiantFrailty")
def _(c):
    flame(c)
    with c.at(70, 4, 0.42):
        snowflake(c)
    up(c, col=RED)


@icon("Giants/Genes/FrostWolfLord")
def _(c):
    wolf_head(c, (210, 222, 232))
    badge(c, crown, 64, -2, 0.48, col=GOLD)


@icon("Giants/Genes/FrostLootHeart")
def _(c):
    coins(c)
    with c.at(4, 0, 0.6):
        sword(c, LG, -35)
    heart(c, RED, 100, 30, 0.34)


@icon("Giants/Genes/StormGiantCore")
def _(c):
    torso(c, (150, 186, 200), 1.1)
    head(c, col=(150, 186, 200))
    # coral crown: branching sticks
    m = None
    for x, h in ((40, 26), (54, 36), (64, 30), (74, 36), (88, 26)):
        mm = c.line([(x, 30), (x + (x - 64) * 0.3, 30 - h)], 6)
        mm |= c.line([(x + (x - 64) * 0.15, 30 - h * 0.5), (x + (x - 64) * 0.15 + 8, 30 - h * 0.7)], 5)
        m = mm if m is None else m | mm
    c.put(m | c.rect(36, 24, 92, 34, 4), (232, 130, 120), shade=0.1)
    badge(c, bolt, 78, 66, 0.44)


@icon("Giants/Genes/StormBody")
def _(c):
    bolt(c)
    badge(c, shield, 62, 64, 0.48, col=LG)


@icon("Giants/Genes/StormChild")
def _(c):
    cloud(c, DG, 44)
    rain(c, LBLUE, 80)
    badge(c, bolt, 44, 50, 0.4)
    up(c, cx=106, cy=30, s=0.8)


@icon("Giants/Genes/StormMelancholy")
def _(c):
    cloud(c, G, 54)
    drop(c) if False else badge(c, drop, 40, 64, 0.42, col=LBLUE)
    down(c, cx=26, cy=96, s=0.9)


@icon("Giants/Genes/StormOmen")
def _(c):
    eye(c, TEAL, cy=78, w=52, h=26)
    with c.at(38, -2, 0.42):
        bolt(c)


# ======================= Common races =======================
@icon("CommonRaces/Genes/Relentless")
def _(c):
    heart(c, RED)
    c.detail(c.line([(64, 30), (54, 58), (72, 70), (60, 100)], 4), INK)
    badge(c, shield, 66, 66, 0.44, col=LG)


@icon("CommonRaces/Genes/Savage")
def _(c):
    burst(c, RED, ORANGE)
    sword(c, LG, 40, wide=1.2)


@icon("CommonRaces/Genes/ElfTrance")
def _(c):
    torso(c)
    elf_head(c)
    c.detail(c.line(bez((48, 54), (54, 58), (60, 54)), 2.6) | c.line(bez((68, 54), (74, 58), (80, 54)), 2.6), INK)
    with c.at(78, 2, 0.36):
        moon(c)


@icon("CommonRaces/Genes/ElfBloodline")
def _(c):
    brain(c)
    badge(c, shield, 66, 66, 0.46, col=(160, 200, 150))


@icon("CommonRaces/Genes/ElfLongevity")
def _(c):
    hourglass(c)
    badge(c, leaf, 66, 62, 0.46)


@icon("CommonRaces/Genes/DwarfStature")
def _(c):
    with c.at(14, 30, 0.78):
        torso(c, W, 1.35)
        head(c)
        beard(c, BROWN, 62)
    down(c, cx=104, cy=30)


@icon("CommonRaces/Genes/DwarfLiver")
def _(c):
    mug(c)


@icon("CommonRaces/Genes/DwarfStoneEndurance")
def _(c):
    rock(c, G)
    with c.at(-6, 50, 0.6):
        pick(c, LG, 30)
    up(c)


@icon("CommonRaces/Genes/HighElfLineage")
def _(c):
    torso(c, W)
    elf_head(c)
    star(c, GOLD, 100, 22, 18)


@icon("CommonRaces/Genes/WoodElfLineage")
def _(c):
    torso(c, (150, 186, 110))
    elf_head(c, (150, 186, 110))
    badge(c, leaf, 76, 0, 0.36, ang=30)


@icon("CommonRaces/Genes/DrowSunlight")
def _(c):
    sun(c)
    down(c)


@icon("CommonRaces/Genes/DrowPoison")
def _(c):
    dagger(c, LG, 40)
    badge(c, drop, 0, 64, 0.46, col=LGREEN)


@icon("CommonRaces/Genes/DrowDarkness")
def _(c):
    c.put(c.ell(64, 64, 52), (52, 40, 70))
    swirl(c, PURPLE, 64, 64, 40, 1.8, 8)


@icon("CommonRaces/Genes/DwarfArmorTraining")
def _(c):
    shield(c, G)
    c.detail(c.rect(58, 26, 70, 110) | c.rect(26, 46, 102, 58), (110, 110, 110))
    up(c)


@icon("CommonRaces/Genes/DwarfToughness")
def _(c):
    heart(c, RED)
    plus(c, 102, 98, 1.0)


@icon("CommonRaces/Genes/DwarfGrudge")
def _(c):
    drop(c, CRIM)
    skull(c, BONE, 64, 82, 0.4)


@icon("CommonRaces/Genes/HalflingBlood")
def _(c):
    with c.at(4, 30, 0.75):
        figure(c)
    down(c, cx=104, cy=34)
    badge(c, drop, 72, 70, 0.4, col=TAN) if False else None
    c.put(c.ell(96, 100, 22, 16), (190, 80, 70))
    c.put(c.poly(taper([(96, 86), (100, 74)], 4, 3)), BROWN, shade=0)


@icon("CommonRaces/Genes/HalflingLuck")
def _(c):
    clover(c)


@icon("CommonRaces/Genes/HalflingHideBehind")
def _(c):
    with c.at(18, 0, 0.95):
        figure(c, G)
    with c.at(-6, 44, 0.62):
        figure(c, W)


@icon("CommonRaces/Genes/HalflingStout")
def _(c):
    sized_bust(c, W, 0.9, 1.45)
    plus(c, 104, 26, 0.9)


@icon("CommonRaces/Genes/GnomeBlood")
def _(c):
    with c.at(4, 22, 0.8):
        torso(c)
        head(c)
        c.put(c.poly([(36, 40), (64, -14), (92, 40)]), BLUE)
    badge(c, brain, 64, 64, 0.48)


@icon("CommonRaces/Genes/RockGnomeTinker")
def _(c):
    gear(c, G)
    with c.at(40, 40, 0.68):
        hammer(c, LG, -35)


@icon("CommonRaces/Genes/GnomeSmallBeasts")
def _(c):
    speech(c, W, 64, 46)
    footprint(c, BROWN, 70, 48, 0.5)
    c.put(c.ell(98, 104, 20, 14), (170, 120, 80))
    c.put(c.ell(84, 92, 8, 14, -20), (170, 120, 80))


@icon("CommonRaces/Genes/DeepGnomeCamouflage")
def _(c):
    rock(c, (130, 128, 134))
    c.detail(c.ell(50, 60, 6, 4) | c.ell(78, 60, 6, 4), YELLOW)
    c.detail(c.ell(50, 60, 2, 3) | c.ell(78, 60, 2, 3), INK)


@icon("CommonRaces/Genes/HalfElfBlood")
def _(c):
    torso(c)
    elf_head(c)
    half = c.rect(64, 0, 128, 128)
    m = (c.ell(64, 56, 21, 27) | c.poly([(80, 50), (110, 30), (82, 68)])) & half
    c.detail(m, SKIN)
    c.detail(c.line([(64, 30), (64, 82)], 2.4), INK)


@icon("CommonRaces/Genes/KoboldBlood")
def _(c):
    kobold_head(c)
    down(c, cx=24, cy=104, s=0.85)


@icon("CommonRaces/Genes/KoboldPackTactics")
def _(c):
    for x, y, k in ((-6, 30, 0.6), (56, 30, 0.6), (24, 50, 0.62)):
        with c.at(x, y, k):
            kobold_head(c)


@icon("CommonRaces/Genes/KoboldTrapsmith")
def _(c):
    spikes(c)
    badge(c, kobold_head, -6, -4, 0.5)


@icon("CommonRaces/Genes/KoboldDragonServant")
def _(c):
    bat_wings(c, RED, 58, 0.9, 0)
    with c.at(14, 30, 0.75):
        kobold_head(c)
    heart(c, PINK, 100, 104, 0.3)


@icon("CommonRaces/Genes/GoblinoidBlood")
def _(c):
    goblin_head(c)
    badge(c, snowflake, 68, 68, 0.42)


@icon("CommonRaces/Genes/GoblinFury")
def _(c):
    burst(c, RED, ORANGE, 64, 70, 58)
    goblin_head(c, angry=True)


@icon("CommonRaces/Genes/GoblinNimble")
def _(c):
    boot(c, BROWN)
    speed_lines(c, W, 0, (40, 62, 84), 24)
    up(c, cx=24, cy=104, s=0.8)


@icon("CommonRaces/Genes/HobgoblinNumbers")
def _(c):
    col = (196, 120, 76)
    for x, k in ((-4, 0.6), (56, 0.6)):
        with c.at(x, 20, k):
            torso(c, col)
            head(c, col=col)
    with c.at(22, 38, 0.7):
        torso(c, col)
        head(c, col=col)
    plus(c, 108, 22, 0.75)


@icon("CommonRaces/Genes/HobgoblinDiscipline")
def _(c):
    shield(c, CRIM)
    c.detail(c.line([(34, 50), (64, 70), (94, 50)], 8) | c.line([(34, 74), (64, 94), (94, 74)], 8), GOLD)


@icon("CommonRaces/Genes/BugbearSurprise")
def _(c):
    burst(c, ORANGE, YELLOW)
    exclaim(c, W)


@icon("CommonRaces/Genes/BugbearLongLimbs")
def _(c):
    c.put(c.poly(taper(bez((8, 120), (30, 70), (60, 50), (98, 30)), 26, 18)), (150, 112, 70))
    hand_k = 0.42
    with c.at(70, -2, 0.45):
        hand(c, (150, 112, 70))
    up(c, cx=104, cy=100)


# ======================= Necromancy =======================
@icon("Necromancy/Icons/LichCore")
def _(c):
    swirl(c, PURPLE, 64, 70, 58, 1.2, 9)
    skull(c, BONE, 64, 70, 0.82)
    c.detail(c.ell(51 * 0.82 + 64 - 64 * 0.82, 58 * 0.82 + 70 - 60 * 0.82, 4) |
             c.ell(77 * 0.82 + 64 - 64 * 0.82, 58 * 0.82 + 70 - 60 * 0.82, 4), (190, 120, 240))
    crown(c, GOLD, 18, 30, PURPLE)


@icon("Necromancy/Icons/NecroticTouch")
def _(c):
    swirl(c, PURPLE, 64, 64, 58, 1.4, 8)
    hand(c, (150, 170, 140))


@icon("Necromancy/Icons/NecroticBody")
def _(c):
    torso(c, (176, 186, 168))
    with c.at(34, 84, 0.46):
        ribs(c, (150, 160, 140))
    skull(c, (200, 206, 190), 64, 52, 0.62)


# ======================= Faith =======================
@icon("Faith/Genes/CelestialLineage")
def _(c):
    feather_wings(c, W, 70, 0.8, 6)
    sized_bust(c, (246, 230, 180), 0.8)
    halo(c, YELLOW, 22, 26, 8)


@icon("Faith/Genes/CelestialResistance")
def _(c):
    shield(c, W)
    sun_k = 0.5
    badge(c, sun, 33, 30, 0.5)


@icon("Faith/Genes/HealingHands")
def _(c):
    hand(c, SKIN)
    plus(c, 100, 100, 0.95)
    sparkle(c, 20, 24, 14)


@icon("Faith/Genes/RadiantSoul")
def _(c):
    feather_wings(c, (246, 222, 140), 70, 0.95, 2)
    sparkle(c, 64, 70, 22, W)


@icon("Faith/Genes/RadiantConsumption")
def _(c):
    sun(c, YELLOW)
    badge(c, flame, 64, 60, 0.48)


@icon("Faith/Genes/NecroticShroud")
def _(c):
    ghost(c, (110, 96, 130))
    c.detail(c.ell(50, 54, 5, 7) | c.ell(78, 54, 5, 7), LGREEN)


@icon("Faith/Genes/Halo")
def _(c):
    torso(c)
    head(c)
    halo(c, YELLOW, 18, 34, 11)


@icon("Faith/Genes/FiendishBlood")
def _(c):
    torso(c, (196, 90, 86))
    horns_devil(c, DARK)
    head(c, col=(196, 90, 86))
    eyes(c, 64, 52, 9, 3.4, YELLOW)
    tail = bez((96, 126), (120, 100), (112, 80), (118, 66))
    c.put(c.poly(taper(tail, 7, 5)) | c.poly([(118, 52), (126, 70), (110, 70)]), (196, 90, 86))


@icon("Faith/Genes/InfernalResistance")
def _(c):
    flame(c, CRIM, ORANGE)
    badge(c, shield, 64, 64, 0.48, col=DG)


@icon("Faith/Genes/HellishRebuke")
def _(c):
    shield(c, DG)
    with c.at(32, 28, 0.5):
        flame(c, CRIM, ORANGE)
    curved_arrow_k = 0
    with c.at(60, 52, 0.55):
        curved_arrow(c, ORANGE, True)


@icon("Faith/Genes/InfernalPact")
def _(c):
    scroll(c, TAN, CRIM)
    with c.at(66, -4, 0.4):
        flame(c, CRIM, ORANGE)


# ======================= Fiends (Races124) =======================
@icon("Races124/Genes/FiendCore")
def _(c):
    with c.at(0, 66, 0.5):
        flame(c, CRIM, ORANGE)
    with c.at(64, 66, 0.5):
        flame(c, CRIM, ORANGE)
    demon_bust(c, (190, 80, 70), DARK, 0.86)


@icon("Races124/Genes/IceDevilStature")
def _(c):
    demon_bust(c, (150, 190, 214), (232, 244, 252), 0.9)
    up(c)


@icon("Races124/Genes/IceDevilCold")
def _(c):
    snowflake(c, LBLUE)
    badge(c, flame, 68, 68, 0.42)
    c.put(c.line([(70, 68), (122, 124)], 6), RED, shade=0)


@icon("Races124/Genes/IceDevilStinger")
def _(c):
    stinger(c, LBLUE, (232, 244, 252))


@icon("Races124/Genes/IceDevilSight")
def _(c):
    eye(c, LBLUE)
    c.detail(c.line([(52, 52), (76, 76)], 2) | c.line([(76, 52), (52, 76)], 2), (60, 100, 140))


@icon("Races124/Genes/BalorStature")
def _(c):
    with c.at(0, 0, 1):
        torso(c, (160, 50, 44), 1.15)
        for s in (-1, 1):
            horn(c, (64 + s * 12, 34), (64 + s * 52, 30), (64 + s * 50, 2), 15, DARK)
        head(c, col=(160, 50, 44))
        eyes(c, 64, 52, 9, 3.4, YELLOW)
    up(c)


@icon("Races124/Genes/BalorFlame")
def _(c):
    flame(c, (196, 60, 44), ORANGE)
    with c.at(32, 48, 0.5):
        horns_devil(c, DARK, 50, 18, 30)


@icon("Races124/Genes/BalorWings")
def _(c):
    bat_wings(c, (110, 40, 40), 64, 0.95, 4)


@icon("Races124/Genes/BalorDeathburst")
def _(c):
    burst(c, (206, 70, 50), ORANGE)
    skull(c, BONE, 64, 64, 0.5)


@icon("FireGiant/FireGiantGene")
def _(c):
    with c.at(14, 0, 0.78):
        flame(c, ORANGE, YELLOW)
    torso(c, (176, 60, 50), 1.2)
    head(c, 64, 58, 21, 25, (176, 60, 50))
    beard(c, ORANGE, 66)
    eyes(c, 64, 56, 9, 3.2, YELLOW)


# ======================= Genasi / Genie =======================
@icon("Races124/Genes/GenasiCore")
def _(c):
    with c.at(0, 0, 0.5):
        flame(c)
    with c.at(64, 0, 0.5):
        drop(c)
    with c.at(0, 64, 0.5):
        rock(c, BROWN)
    with c.at(64, 64, 0.5):
        wind(c)


@icon("Races124/Genes/ElementFire")
def _(c):
    flame(c)


@icon("Races124/Genes/ElementWater")
def _(c):
    drop(c, BLUE)
    with c.at(66, 70, 0.38):
        wavy(c, LBLUE, 3, 20, 40, 20, 110, 14)


@icon("Races124/Genes/ElementEarth")
def _(c):
    rock(c, (150, 120, 88))


@icon("Races124/Genes/ElementAir")
def _(c):
    wind(c, LBLUE)


@icon("Races124/Genes/GenieCore")
def _(c):
    smoke(c, (150, 170, 226))
    with c.at(0, 8, 1):
        lamp(c)


@icon("Races124/Genes/GenieStature")
def _(c):
    c.put(c.poly(taper(bez((64, 128), (40, 110), (90, 100), (64, 84)), 10, 40)), (110, 140, 214))
    with c.at(4, -10, 0.92):
        torso(c, (110, 140, 214))
        head(c, col=(110, 140, 214))
    up(c)


@icon("Races124/Genes/GenieAgeless")
def _(c):
    hourglass(c)
    sparkle(c, 104, 24, 16, W)
    sparkle(c, 22, 100, 12, W)


@icon("Races124/Genes/GenieWish")
def _(c):
    with c.at(0, 30, 0.75):
        lamp(c)
    sparkle(c, 64, 30, 24, YELLOW)
    sparkle(c, 30, 18, 12, W)
    sparkle(c, 100, 22, 14, W)


# ======================= Githyanki / Minotaur =======================
@icon("MinotaurGith/Genes/SilverSword")
def _(c):
    swirl(c, PURPLE, 64, 64, 56, 1.3, 8)
    sword(c, (230, 234, 242), 40, DG, LG, 1.2)


@icon("MinotaurGith/Genes/AstralStep")
def _(c):
    portal(c)
    footprint(c, W, 100, 100, 0.34, 20)


@icon("MinotaurGith/Genes/LichDevotee")
def _(c):
    skull(c, BONE, 64, 62, 0.85)
    crown(c, GOLD, 16, 28, PURPLE)
    heart(c, PINK, 102, 100, 0.34)


@icon("MinotaurGith/Genes/Nemesis")
def _(c):
    with c.at(0, 0, 1):
        head(c, 64, 40, 26, 28, LPURPLE)
        tentacles(c, LPURPLE, 4)
    cross(c, 64, 64, 2.3)


@icon("MinotaurGith/Genes/Charge")
def _(c):
    with c.at(22, 10, 0.82):
        bull_head(c)
    speed_lines(c, W, 0, (50, 72, 94), 22)


@icon("MinotaurGith/Genes/Horns")
def _(c):
    torso(c)
    horns_bull(c, BONE, 38)
    head(c)


@icon("MinotaurGith/Genes/Snout")
def _(c):
    nose_ring(c)


@icon("MinotaurGith/Genes/Maze")
def _(c):
    maze(c)
    up(c)


@icon("MinotaurGith/Genes/Stature")
def _(c):
    torso(c, BROWN, 1.25)
    with c.at(14, -6, 0.78):
        bull_head(c)
    up(c)


# ======================= Ettin / Cyclops =======================
@icon("HARGiants/Ettin/Genes/Core")
def _(c):
    two_heads(c, (204, 176, 150))


@icon("HARGiants/Ettin/Genes/Watch")
def _(c):
    two_heads(c, (204, 176, 150), closed=0)
    c.detail(c.ell(77, 52, 3, 3.6) | c.ell(95, 52, 3, 3.6), INK)
    with c.at(0, -4, 0.3):
        zzz(c)


@icon("HARGiants/Ettin/Genes/TwinGrip")
def _(c):
    sword(c, LG, 40)
    sword(c, LG, -40)


@icon("HARGiants/Ettin/Genes/Quarrel")
def _(c):
    two_heads(c, (204, 176, 150))
    for cx, s in ((42, 1), (86, -1)):
        c.detail(c.line([(cx - 10, 44 + 3 * s), (cx - 2, 48 - 3 * s)], 2.6) | c.line([(cx + 2, 48 - 3 * s), (cx + 10, 44 + 3 * s)], 2.6), INK)
    badge(c, burst, 40, -6, 0.38, col=RED, inner=ORANGE)


@icon("HARGiants/Cyclops/Genes/Core")
def _(c):
    torso(c, (196, 168, 140), 1.2)
    head(c, 64, 50, 26, 30, (196, 168, 140))
    with c.at(32, 22, 0.5):
        eye(c, BLUE)


@icon("HARGiants/Cyclops/Genes/Craft")
def _(c):
    with c.at(0, 30, 0.85):
        anvil(c)
    with c.at(30, -10, 0.7):
        hammer(c, G, 35)


@icon("HARGiants/Cyclops/Genes/Shepherd")
def _(c):
    crook(c)
    with c.at(-6, 30, 0.82):
        sheep(c)


@icon("HARGiants/Cyclops/Genes/FlatSight")
def _(c):
    eye(c, BLUE, cy=56)
    target(c, RED, 100, 100, 18)
    down(c, cx=26, cy=100, s=0.85)


# ======================= Loxodon =======================
@icon("Races124/Genes/LoxodonStature")
def _(c):
    torso(c, G, 1.25)
    with c.at(14, -4, 0.78):
        trunk_head(c)
    up(c)


@icon("Races124/Genes/LoxodonTrunk")
def _(c):
    trunk_head(c)


@icon("Races124/Genes/LoxodonCalm")
def _(c):
    brain(c)
    with c.at(60, 56, 0.5):
        trunk_head(c)


@icon("Races124/Genes/LoxodonHide")
def _(c):
    shield(c, G)
    for y in (40, 58, 76):
        c.detail(c.line(bez((30, y), (50, y - 6), (78, y + 6), (98, y)), 2.6), (100, 100, 100))
    up(c)


# ======================= Marilith =======================
@icon("Marilith/Xenotype")
def _(c):
    skin = (196, 150, 170)
    tail = bez((64, 88), (30, 92), (24, 110), (60, 110)) + bez((60, 110), (96, 110), (112, 104), (118, 82))[1:]
    c.put(c.poly(taper(tail, 30, 7)), TEAL)
    k, oy = 0.62, 14
    for ang in (-50, -10, 30):
        for s in (-1, 1):
            a = math.radians(ang)
            p0 = (64 + s * 16, 80 + oy * 0.1 + (ang + 10) * 0.12)
            p1 = (p0[0] + s * 40 * math.cos(a), p0[1] + 40 * math.sin(a))
            c.put(c.poly(taper([p0, p1], 13, 9)) | c.ell(p1[0], p1[1], 6.5), skin)
    with c.at(64 - 64 * k, oy, k):
        torso(c, skin, 0.9)
        head(c, col=skin)
        eyes(c, 64, 52, 9, 3.4, YELLOW)


@icon("Marilith/Genes/Blades")
def _(c):
    for a in (-50, 0, 50):
        sword(c, LG, a)


@icon("Marilith/Genes/Glide")
def _(c):
    with c.at(0, 6, 1):
        wavy(c, (110, 150, 190), 1, 64, 0, 96, 126, 8) if False else None
    c.put(c.rect(4, 100, 124, 124, 8), (110, 130, 90))
    snake(c, TEAL)
    up(c, cx=104, cy=78, s=0.8)


@icon("Marilith/Genes/Constrict")
def _(c):
    with c.at(14, 0, 0.8):
        figure(c, W)
    for y in (64, 86, 108):
        c.put(c.ell(64, y, 40, 11) & ~c.ell(64, y - 4, 30, 6), TEAL, shade=0.1)
    c.put(c.ell(102, 50, 14, 10, -20), TEAL)
    c.detail(c.ell(106, 47, 2.6), INK)


@icon("Marilith/Genes/BladeStorm")
def _(c):
    for a in (0, 72, 144, 216, 288):
        with c.at(0, 0, 1):
            sword(c, LG, a)
    c.put(c.ell(64, 64, 12), DG)


@icon("Marilith/Genes/AbyssalHide")
def _(c):
    scales(c, TEAL)
    badge(c, flame, 64, 64, 0.48)


@icon("Marilith/Genes/ColdBlood")
def _(c):
    thermometer(c, BLUE)
    with c.at(54, 46, 0.6):
        snake(c, TEAL)
    down(c, cx=26, cy=100, s=0.8)


# ======================= Mind flayer =======================
@icon("MindFlayer/Genes/Cerebrum")
def _(c):
    brain(c)
    tentacles(c, LPURPLE, 3) if False else None
    c.put(c.rect(14, 104, 114, 120, 4), DG)
    c.flat(c.rect(18, 108, 80, 116), (222, 150, 170))


@icon("MindFlayer/Genes/Sterile")
def _(c):
    egg(c, LPURPLE)
    cross(c, 64, 64, 2.0)


@icon("MindFlayer/Genes/PsionicSense")
def _(c):
    for r in (58, 46):
        c.put(c.arc(64, 54, r, 200, 340, 6), LPURPLE, shade=0)
    head(c, 64, 60, 24, 26, (160, 120, 176))
    with c.at(14, 52, 0.62):
        tentacles(c, (160, 120, 176), 4)
    eyes(c, 64, 56, 10, 3.2, W)


@icon("MindFlayer/Genes/HiveLink")
def _(c):
    pts = [(32, 34), (96, 34), (64, 96)]
    m = c.line([pts[0], pts[1]], 6) | c.line([pts[1], pts[2]], 6) | c.line([pts[2], pts[0]], 6)
    c.put(m, LPURPLE, shade=0)
    for x, y in pts:
        with c.at(x - 28, y - 28, 0.44):
            brain(c)


@icon("MindFlayer/Genes/HalfIllithid")
def _(c):
    torso(c)
    with c.at(14, 40, 0.6):
        tentacles(c, (160, 120, 176), 3)
    head(c)
    m = c.ell(64, 50, 21, 26) & c.rect(64, 0, 128, 128)
    c.detail(m, (160, 120, 176))
    c.detail(c.line([(64, 24), (64, 76)], 2.4), INK)


# ======================= Lich (necro) done; Ogre =======================
@icon("Races124/Genes/OgreStature")
def _(c):
    col = (176, 160, 110)
    torso(c, col, 1.35)
    head(c, 64, 52, 26, 28, col)
    eyes(c, 64, 48, 11, 3)
    tusks(c, 64, 70, W, 14)
    up(c)


@icon("Races124/Genes/OgreDull")
def _(c):
    brain(c, (200, 170, 160))
    down(c)


# ======================= Oozefolk =======================
@icon("Oozefolk/Genes/OozeCore")
def _(c):
    ooze(c)
    c.detail(c.ell(52, 64, 5, 7) | c.ell(78, 64, 5, 7), INK)


@icon("Oozefolk/Genes/Protoplasm")
def _(c):
    drop(c, LGREEN)
    c.put(c.rect(14, 108, 114, 122, 4), DG)
    c.flat(c.rect(18, 112, 60, 118), LGREEN)


@icon("Oozefolk/Genes/Digest")
def _(c):
    ooze(c)
    with c.at(22, 30, 0.62):
        bone(c, BONE)
    c.detail(c.ell(64, 70, 30, 24), (166, 184, 76)) if False else None


@icon("Oozefolk/Genes/Pseudopod")
def _(c):
    with c.at(0, 30, 0.75):
        ooze(c)
    c.put(c.poly(taper(bez((60, 64), (70, 20), (100, 40), (118, 8)), 22, 6)), LGREEN)
    c.detail(c.ell(110, 20, 4), (220, 240, 120))


@icon("Oozefolk/Genes/Amorphous")
def _(c):
    c.put(c.rect(52, 6, 76, 124, 3), BROWN)
    c.detail(c.line([(64, 10), (64, 120)], 2.4), (90, 64, 44))
    with c.at(-6, 40, 0.6):
        ooze(c)
    with c.at(58, 40, 0.6):
        ooze(c)
    curved_arrow_small = None
    c.put(c.rect(4, 6, 124, 14, 3), DG)


@icon("Oozefolk/Genes/Split")
def _(c):
    with c.at(-4, 16, 0.75):
        ooze(c)
    with c.at(60, 50, 0.55):
        ooze(c)
    plus(c, 104, 24, 0.75)


@icon("Oozefolk/Genes/Chill")
def _(c):
    ooze(c, (150, 200, 140))
    badge(c, snowflake, 66, 0, 0.44)


@icon("Oozefolk/Genes/Desiccate")
def _(c):
    ooze(c)
    badge(c, flame, 66, 0, 0.44)
    up(c, col=RED)


@icon("Oozefolk/Genes/Sheen")
def _(c):
    torso(c, LGREEN)
    head(c, col=LGREEN)
    c.flat(c.ell(54, 36, 6, 9, -20), (230, 246, 170))
    drop_k = 0.3
    with c.at(76, 64, 0.26):
        drop(c, LGREEN)


# ======================= Pact (fiend / fey / celestial patrons) =======================
@icon("Pact/Genes/HellsCore")
def _(c):
    with c.at(0, 56, 0.55):
        chain(c, G, 90, 4)
    demon_bust(c, (150, 44, 44), DARK, 0.9)


@icon("Pact/Genes/FeyCore")
def _(c):
    moth_wings(c, LPURPLE, 60)
    sized_bust(c, (150, 196, 130), 0.78)
    sparkle(c, 104, 20, 12, W)


@icon("Pact/Genes/CelestialCore")
def _(c):
    feather_wings(c, W, 70, 0.8, 6)
    sized_bust(c, (246, 230, 180), 0.8)
    halo(c, YELLOW, 24, 24, 7)
    sparkle(c, 108, 16, 10, W)


@icon("Pact/Genes/ChainDevilStature")
def _(c):
    sized_bust(c, G, 1.0, 1.1)
    c.detail(c.line([(30, 120), (98, 90)], 8), DG)
    for i in range(5):
        x, y = 36 + i * 14, 117 - i * 6.2
        c.detail(c.ell(x, y, 6, 4, -24), (190, 190, 190))
    up(c)


@icon("Pact/Abilities/ChainLash")
def _(c):
    chain(c, G, -40, 4)
    c.put(c.poly([(96, 4), (124, 14), (110, 36)]), LG)


@icon("Pact/Genes/ChainDevilSubvert")
def _(c):
    with c.at(0, 30, 0.7):
        chain(c, G, -60, 3)
    skull(c, BONE, 64, 58, 0.72)
    c.detail(c.ell(51 * 0.72 + 64 - 64 * 0.72, 58 * 0.72 + 58 - 60 * 0.72, 3.4) |
             c.ell(77 * 0.72 + 64 - 64 * 0.72, 58 * 0.72 + 58 - 60 * 0.72, 3.4), RED)


@icon("Pact/Genes/BoneDevilStature")
def _(c):
    torso(c, BONE, 0.9)
    skull(c, BONE, 64, 46, 0.6)
    up(c)


@icon("Pact/Genes/BoneDevilTail")
def _(c):
    stinger(c, BONE, (170, 196, 110))


@icon("Pact/Genes/BoneDevilDread")
def _(c):
    for r in (58, 48):
        c.put(c.arc(64, 64, r, 200, 340, 6), PURPLE, shade=0)
        c.put(c.arc(64, 64, r, 20, 160, 6), PURPLE, shade=0)
    skull(c, BONE, 64, 64, 0.62)


@icon("Pact/Genes/HornedDevilStature")
def _(c):
    bat_wings(c, (110, 40, 40), 70, 0.86, 8)
    with c.at(14, 26, 0.78):
        torso(c, (176, 60, 50), 1.15)
        for s in (-1, 1):
            horn(c, (64 + s * 12, 34), (64 + s * 46, 26), (64 + s * 40, -4), 14, DARK)
        head(c, col=(176, 60, 50))
        eyes(c, 64, 52, 9, 3.4, YELLOW)
    up(c)


@icon("Pact/Abilities/HellfireHurl")
def _(c):
    with c.at(20, -6, 0.86):
        c.tf[-1]
        flame(c, CRIM, ORANGE)
    speed_lines(c, ORANGE, 0, (70, 92, 114), 26)


@icon("Pact/Genes/ErinyesStature")
def _(c):
    torso(c, SKIN)
    head(c, col=SKIN)
    tat = (150, 40, 50)
    c.detail(c.line(bez((40, 96), (54, 110), (60, 100), (70, 120)), 3) | c.line(bez((88, 94), (76, 106), (84, 116)), 3) |
             c.line(bez((50, 40), (58, 46), (52, 52)), 2.6) | c.line(bez((78, 40), (70, 46), (76, 52)), 2.6), tat)


@icon("Pact/Genes/ErinyesWings")
def _(c):
    feather_wings(c, (70, 64, 76), 70, 0.95, 2)


@icon("Pact/Abilities/HuntersMark")
def _(c):
    target(c, RED)


@icon("Pact/Abilities/Entangle")
def _(c):
    rope(c)


@icon("Pact/Genes/ErinyesResolve")
def _(c):
    shield(c, DARK)
    sword(c, LG, 0, DG, CRIM)


@icon("Pact/Genes/AmnizuStature")
def _(c):
    torso(c, (180, 150, 140), 0.8)
    for s in (-1, 1):
        c.put(c.poly([(64 + s * 16, 44), (64 + s * 34, 30), (64 + s * 18, 58)]), (180, 150, 140))
        horn(c, (64 + s * 8, 26), (64 + s * 12, 16), (64 + s * 6, 8), 8, DARK)
    head(c, 64, 48, 18, 28, (180, 150, 140))
    up(c)


@icon("Pact/Genes/AmnizuMind")
def _(c):
    brain(c)
    swirl(c, PURPLE, 64, 60, 26, 1.6, 6)
    down(c)


@icon("Pact/Genes/SatyrHorns")
def _(c):
    torso(c)
    horns_ram(c, TAN, 36)
    head(c)


@icon("Pact/Genes/SatyrHooves")
def _(c):
    hoof(c)


@icon("Pact/Genes/SatyrMagicResist")
def _(c):
    shield(c, GREEN)
    sparkle(c, 64, 60, 26, W)
    sparkle(c, 40, 38, 10, W)


@icon("Pact/Abilities/SatyrCharge")
def _(c):
    with c.at(30, 18, 0.78):
        torso(c)
        horns_ram(c, TAN, 36)
        head(c)
    speed_lines(c, W, 0, (46, 68, 90), 24)


@icon("Pact/Genes/SatyrRevelry")
def _(c):
    mug(c)
    sparkle(c, 112, 20, 12, YELLOW)
    sparkle(c, 16, 26, 10, PINK)


@icon("Pact/Genes/SatyrMusic")
def _(c):
    # pan pipes
    for i, h in enumerate((84, 72, 62, 52, 44)):
        x = 30 + i * 15
        c.put(c.rect(x - 6, 30, x + 6, 30 + h, 4), TAN, shade=0.12)
    c.put(c.rect(20, 44, 108, 54, 3), BROWN)
    with c.at(70, 64, 0.45):
        note(c)


@icon("Pact/Abilities/BindTree")
def _(c):
    tree(c)
    heart(c, PINK, 64, 54, 0.36)


@icon("Pact/Abilities/TreeStride")
def _(c):
    with c.at(-8, 8, 0.6):
        tree(c)
    with c.at(58, 46, 0.6):
        tree(c)
    with c.at(18, -14, 0.65):
        curved_arrow(c, W)


@icon("Pact/Genes/DryadBark")
def _(c):
    shield(c, BROWN)
    for x in (40, 58, 76, 92):
        c.detail(c.line(bez((x, 24), (x - 6, 60), (x + 6, 80), (x - 2, 110 - abs(x - 64) * 0.6)), 3), (90, 62, 40))
    badge(c, leaf, 64, 0, 0.42)


@icon("Pact/Genes/RedcapStature")
def _(c):
    with c.at(10, 36, 0.72):
        torso(c, (170, 140, 120))
        head(c, col=(170, 140, 120))
        c.put(c.poly(bez((34, 40), (40, 0), (70, -16), (106, -10)) + bez((106, -10), (84, 4), (94, 26), (94, 40))), RED)
    down(c, cx=104, cy=34)


@icon("Pact/Genes/RedcapBloodlust")
def _(c):
    drop(c, CRIM)
    up(c)


@icon("Pact/Genes/RedcapStomp")
def _(c):
    burst(c, ORANGE, YELLOW, 64, 104, 30)
    boot(c, DG)


@icon("Pact/Abilities/Faces")
def _(c):
    with c.at(-4, 0, 0.72):
        mask(c, W)
    with c.at(38, 36, 0.72):
        mask(c, (190, 160, 200))


@icon("Pact/Genes/ChangelingFluid")
def _(c):
    mask(c, (220, 220, 230))
    m = c.poly(bez((64, 10), (40, 50), (88, 80), (64, 124)) + [(128, 124), (128, 10)])
    c.detail(m & c.poly(bez((20, 30), (64, 14), (64, 14), (108, 30)) + bez((108, 30), (112, 90), (84, 120), (64, 120)) +
                         bez((64, 120), (44, 120), (16, 90), (20, 30))), (176, 150, 196))
    c.detail(c.ell(82, 56, 10, 7, 10), INK)


@icon("Pact/Genes/WildHunterAntlers")
def _(c):
    torso(c, DG)
    antlers(c, TAN, 40)
    head(c, col=DG)
    eyes(c, 64, 52, 9, 3.2, LGREEN)


@icon("Pact/Genes/WildHuntLord")
def _(c):
    antlers(c, TAN, 76)
    crown(c, GOLD, 84, 40, GREEN)


@icon("Pact/Genes/WildHunterPrey")
def _(c):
    target(c, RED, 64, 64, 50)
    footprint(c, BROWN, 64, 64, 0.3)
    up(c, cx=104, cy=104, s=0.8)


@icon("Pact/Genes/WildHunterSpear")
def _(c):
    spear(c, (200, 230, 190), 40)
    sparkle(c, 26, 26, 14, LGREEN)


@icon("Pact/Genes/DevaStature")
def _(c):
    feather_wings(c, (240, 226, 170), 70, 0.86, 6)
    sized_bust(c, (216, 220, 228), 0.82)
    up(c)


@icon("Pact/Genes/DevaMace")
def _(c):
    mace(c, GOLD, 35)
    sparkle(c, 30, 24, 14, W)


@icon("Pact/Abilities/HealingTouch")
def _(c):
    hand(c, (246, 230, 180))
    sparkle(c, 24, 22, 14, YELLOW)
    plus(c, 102, 100, 0.9)


@icon("Pact/Genes/PlanetarStature")
def _(c):
    feather_wings(c, (210, 216, 228), 70, 0.95, 6)
    sized_bust(c, (150, 190, 140), 0.84)
    up(c)
    up(c, cx=104, cy=56, s=0.7)


@icon("Pact/Genes/PlanetarSword")
def _(c):
    sparkle(c, 30, 30, 22, YELLOW)
    sword(c, (240, 240, 250), 40, GOLD, GOLD, 1.4)


@icon("Pact/Abilities/Wrath")
def _(c):
    burst(c, YELLOW, W)
    with c.at(32, 32, 0.5):
        bolt(c, W)


@icon("Pact/Abilities/Banish")
def _(c):
    portal(c)
    with c.at(56, 54, 0.55):
        curved_arrow(c, W)


# ======================= Storm/Titan =======================
@icon("Races124/Genes/TitanStature")
def _(c):
    torso(c, GOLD, 1.25)
    head(c, col=GOLD)
    halo(c, YELLOW, 18, 30, 9)
    up(c)
    up(c, cx=104, cy=56, s=0.7)


@icon("Races124/Genes/TitanDivinity")
def _(c):
    sun(c, YELLOW)
    bolt(c, W) if False else badge(c, bolt, 34, 34, 0.5, col=W)


@icon("Races124/Genes/TitanWeather")
def _(c):
    with c.at(16, -6, 0.75):
        sun(c)
    cloud(c, W, 80)
    badge(c, bolt, 6, 70, 0.4)


@icon("Races124/Genes/TitanUnshakable")
def _(c):
    rock(c, G)
    badge(c, shield, 66, 66, 0.46, col=GOLD)


# ======================= Troll =======================
@icon("Troll/TrollAppearance")
def _(c):
    col = (118, 150, 92)
    torso(c, col, 1.25)
    head(c, 64, 52, 24, 28, col)
    for s in (-1, 1):
        c.put(c.poly([(64 + s * 20, 46), (64 + s * 44, 36), (64 + s * 22, 60)]), col)
    eyes(c, 64, 48, 10, 3.2, YELLOW)
    tusks(c, 64, 72, W, 16)
    up(c)


@icon("Troll/TrollRegeneration")
def _(c):
    heart(c, (118, 150, 92))
    for a0 in (200, 20):
        c.put(c.arc(64, 64, 58, a0, a0 + 110, 7), GREEN, shade=0)
    plus(c, 64, 64, 0.8, W)


def render(out_root, flt=None):
    done = []
    for path, fn in ICONS.items():
        if flt and flt not in path:
            continue
        c = Icon()
        fn(c)
        dst = os.path.join(out_root, path.replace('/', os.sep) + '.png')
        os.makedirs(os.path.dirname(dst), exist_ok=True)
        c.save(dst)
        done.append(path)
    return done


if __name__ == '__main__':
    done = render(sys.argv[1], sys.argv[2] if len(sys.argv) > 2 else None)
    print(len(done), 'icons')
