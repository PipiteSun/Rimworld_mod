# 2026-11-19 心灵术士、枪手的英文 DefInjected（标签；职业、子职业、专长、强度属性另有说明）。用法：python -I gen_english.py <模组根目录>
import sys, os, re
from xml.sax.saxutils import escape as X
ROOT = sys.argv[1] if len(sys.argv) > 1 else '.'
L = {
 # psion
 'LP_Class_Psion': 'psion', 'LP_PsionicPower': 'psionic power', 'LP_CSL_Psion': 'psion spell list',
 'LP_PS_Propel': 'telekinetic propel', 'LP_PS_Telepathy': 'telepathic connection', 'LP_PS_MindHand': 'subtle telekinesis', 'LP_PS_Awakening': 'psionic awakening',
 'LP_PS_Restoration': 'psionic restoration', 'LP_PS_Surge': 'psionic surge', 'LP_PS_Resonance': 'psychic resonance', 'LP_PS_Reserves': 'psionic reserves', 'LP_PS_Enkindle': 'enkindled life force',
 'LP_PS_Awareness': 'expanded awareness', 'LP_PS_Guards': 'psionic guards', 'LP_PS_Sharpened': 'sharpened mind',
 'LP_PS_MutableForm': 'mutable form', 'LP_PS_OrganicWeapons': 'organic weapons', 'LP_PS_BileLauncher': 'viscera launcher', 'LP_PS_FleshWeave': 'flesh weaver', 'LP_PS_EvolvedForm': 'improved mutable form', 'LP_PS_LifeBending': 'life-bending weapons',
 'LP_PS_StrongTK': 'stronger telekinesis', 'LP_PS_TKTechniques': 'telekinetic techniques', 'LP_PS_DestructiveTrance': 'destructive trance', 'LP_PS_ReboundField': 'rebounding field', 'LP_PS_EnhancedCrush': 'enhanced telekinetic crush', 'LP_PS_HeightenedTK': 'heightened telekinesis',
 'LP_PS_Infiltrate': 'mind infiltrator', 'LP_PS_TeleDistract': 'telepathic distraction', 'LP_PS_BulwarkMind': 'bulwark mind', 'LP_PS_PotentThoughts': 'potent thoughts', 'LP_PS_TeleBolster': 'telepathic bolstering', 'LP_PS_ScrambleMinds': 'scramble minds',
 'LP_PS_Teleport': 'psionic teleport', 'LP_PS_WarpPropel': 'warp propel', 'LP_PS_Displace': 'duplicitous target', 'LP_PS_RescueWarp': 'rescue warp', 'LP_PS_WarpBeacon': 'warp beacon', 'LP_PS_MassWarp': 'mass teleportation',
 'LP_LegPsychicCatastrophe': 'psychic catastrophe', 'LP_LegTranscendentMind': 'transcendent mind',
 'LP_PS_TelekineticFling': 'telekinetic fling', 'LP_PS_LifeSiphon': 'life siphon', 'LP_PS_EgoWhip': 'ego whip', 'LP_PS_TelekineticCrush': 'telekinetic crush', 'LP_PS_IntellectFortress': 'intellect fortress',
 'LP_PS_PsychicLance': 'psychic lance', 'LP_PS_SynapticStatic': 'synaptic static', 'LP_PS_MentalPrison': 'mental prison', 'LP_PS_PsionicBlast': 'psionic blast', 'LP_PS_PsychicScream': 'psychic scream',
 'LP_AC_ThousandMinds': 'eye of a thousand minds', 'LP_AC_MindSeal': 'mind seal', 'LP_AC_PsychicStorm': 'psychic storm', 'LP_AC_MindDominion': 'mind dominion',
 'LP_Crystal_ThousandMinds': 'class crystal: eye of a thousand minds', 'LP_Crystal_MindSeal': 'class crystal: mind seal', 'LP_Crystal_PsychicStorm': 'class crystal: psychic storm', 'LP_Crystal_MindDominion': 'class crystal: mind dominion',
 'LP_Sub_Metamorph': 'metamorph', 'LP_Sub_Psykinetic': 'psykinetic', 'LP_Sub_Telepath': 'telepath', 'LP_Sub_PsiWarper': 'psi warper',
 'LP_Feat_Class_PsionTempered': 'tempered mind', 'LP_Feat_Class_PsionSilent': 'silent mind',
 'LP_PS_Pool': 'psionics', 'LP_PS_DevilTongue': "devilish tongue", 'LP_PS_InerrantAim': 'inerrant aim', 'LP_PS_Kinetic': 'kinetic force', 'LP_PS_Observant': 'observant mind', 'LP_PS_Precog': 'bolstering precognition',
 'LP_PS_IdInsinuation': 'id insinuation', 'LP_PS_SurgeOn': 'psionic surge', 'LP_PS_Enkindled': 'enkindled', 'LP_PS_Sensed': 'psychically sensed', 'LP_PS_AwarenessBuff': 'expanded awareness', 'LP_PS_GuardsBuff': 'psionic guards',
 'LP_PS_SharpenedBuff': 'sharpened mind', 'LP_PS_Mutable': 'mutable form', 'LP_PS_FleshWeaveBuff': 'flesh weave', 'LP_PS_EvolvedStone': 'stony epidermis', 'LP_PS_EvolvedStride': 'superior stride', 'LP_PS_EvolvedFlex': 'unnatural flexibility',
 'LP_PS_FormBlade': 'bone blade', 'LP_PS_FormMaul': 'flesh maul', 'LP_PS_FormLauncher': 'viscera launcher', 'LP_PS_TranceBuff': 'destructive trance', 'LP_PS_Slowed': 'telekinetic crush', 'LP_PS_BulwarkBuff': 'bulwark mind',
 'LP_PS_Linked': 'psychic network', 'LP_PS_Probed': 'mind read', 'LP_PS_Fortress': 'intellect fortress', 'LP_PS_EgoWhipped': 'ego whipped', 'LP_PS_Static': 'synaptic static', 'LP_LegTranscendentBuff': 'transcendent mind',
 'LP_LegPsychicCatastropheBuff': 'psychic catastrophe', 'LP_PS_ArcEyesBuff': 'eye of a thousand minds', 'LP_PS_MindSealed': 'mind sealed', 'LP_PS_StormAura': 'psychic storm',
 # gunslinger
 'LP_Class_Gunslinger': 'gunslinger', 'LP_GunPower': 'gunslinging power',
 'LP_GS_BondGun': 'bonded gun', 'LP_GS_Dodge': "gunslinger's dodge", 'LP_GS_ViolentShot': 'violent shot', 'LP_GS_QuickClear': 'quick clear', 'LP_GS_Deadeye': 'deadeye', 'LP_GS_QuickDraw': 'gunslinger initiative',
 'LP_GS_PistolWhip': 'pistol-whip', 'LP_GS_UtilityShot': 'utility shot', 'LP_GS_GunTraining': 'gun training', 'LP_GS_Gunsmith': 'gunsmith', 'LP_GS_DeadShot': 'dead shot', 'LP_GS_Targeting': 'targeting',
 'LP_GS_AimMode': 'switch target location', 'LP_GS_StartlingShot': 'startling shot', 'LP_GS_BleedingWound': 'bleeding wound', 'LP_GS_LightningReload': 'lightning reload', 'LP_GS_Evasive': 'evasive',
 'LP_GS_MenacingShot': 'menacing shot', 'LP_GS_StunningShot': 'stunning shot', 'LP_GS_SlingerLuck': "slinger's luck", 'LP_GS_DeathShot': "death's shot", 'LP_GS_CheatDeath': 'cheat death',
 'LP_GS_BladeAndGun': 'blade and gun', 'LP_GS_Skirmish': 'skirmish step', 'LP_GS_CloseQuarters': 'close quarters', 'LP_GS_DriftersWake': "drifter's wake",
 'LP_GS_TwinPistols': 'twin pistols', 'LP_GS_Fanning': 'fanning', 'LP_GS_Retort': "pistolero's retort", 'LP_GS_HighNoon': 'high noon',
 'LP_GS_Hidden': 'concealed position', 'LP_GS_PiercingShot': 'piercing shot', 'LP_GS_Spotter': 'spotter', 'LP_GS_ThousandYard': 'thousand-yard stare',
 'LP_GS_HeavyStance': 'heavy stance', 'LP_GS_PointBlank': 'point-blank blast', 'LP_GS_CoveringFire': 'covering fire', 'LP_GS_Breach': 'breach', 'LP_GS_SteelStorm': 'steel storm',
 'LP_LegBulletStorm': 'bullet storm', 'LP_LegTrueGrit': 'true grit',
 'LP_AC_RuneBullets': 'rune bullets', 'LP_AC_BulletRain': 'bullet rain', 'LP_AC_StoppedTime': 'stopped-time shot', 'LP_AC_FateBullet': 'bullet of fate',
 'LP_Crystal_RuneBullets': 'class crystal: rune bullets', 'LP_Crystal_BulletRain': 'class crystal: bullet rain', 'LP_Crystal_StoppedTime': 'class crystal: stopped-time shot', 'LP_Crystal_FateBullet': 'class crystal: bullet of fate',
 'LP_Sub_Drifter': 'way of the drifter', 'LP_Sub_Pistolero': 'way of the pistolero', 'LP_Sub_Sniper': 'way of the sniper', 'LP_Sub_Vanguard': 'way of the vanguard',
 'LP_Feat_Class_GunExpert': 'firearm expert', 'LP_Feat_Class_GunRenown': "sharpshooter's renown",
 'LP_GS_Bonded': 'bonded gun', 'LP_GS_GritEdge': 'grit', 'LP_GS_Jammed': 'jammed', 'LP_GS_Startled': 'startled', 'LP_GS_BleedReady': 'bleeding wound', 'LP_GS_ReloadBuff': 'lightning reload',
 'LP_GS_DrifterBlade': 'blade and gun: blade', 'LP_GS_DrifterShot': 'blade and gun: gun', 'LP_GS_SkirmishBuff': 'skirmish step', 'LP_GS_Dueled': 'high noon', 'LP_GS_Concealed': 'concealed position',
 'LP_GS_Spotted': 'spotted', 'LP_GS_YardBuff': 'thousand-yard stare', 'LP_GS_Covered': 'covering fire', 'LP_GS_StormBuff': 'steel storm', 'LP_LegBulletStormBuff': 'bullet storm', 'LP_GS_RunesBuff': 'rune bullets',
}
D = {
 'LP_Class_Psion': 'The D&D psion casts with pure mental power: spells need no words or gestures (about 80 prepared spells), and a pool of psionic points fuels telekinetic propel, telepathy and six psionic disciplines (chosen from level 3). Gains a psylink from level 1 (setting), and can pay neural heat when out of points. Subclass at level 3: metamorph, psykinetic, telepath or psi warper. Capstone: enkindled life force. Scales with psionic power (Intellectual, Intelligence); damage and healing (setting) scale with psychic sensitivity. Resources: mana (2 + level, regenerates faster while meditating) and psionic points (shown on the health tab). The psychically deaf cannot become psions.',
 'LP_Class_Gunslinger': 'The D&D gunslinger lives on grit and a good gun: bonded gun, gunslinger\'s dodge, violent shot (may jam), targeting, dead shot, menacing shot; capstone death\'s shot. Firearms only (industrial ranged weapons plus this mod\'s magic guns and dwarven firearms; no bows, crossbows, staves or grenades). Way at level 3: drifter (short gun + blade), pistolero (short guns), sniper (long guns) or vanguard (heavy weapons). Also a gunsmith: crafts faster with a chance of +1 quality on firearms. Scales with gunslinging power (Shooting, Dexterity). Resource: grit (gained by killing or downing enemies with firearms, and while sleeping). Martial class: extra attack, fighting style.',
 'LP_PsionicPower': 'Strength multiplier for psion abilities and spells: 0.5 + Intellectual/20, plus Intelligence modifier × 0.05 and (proficiency − 2) × 0.02, and +2% per combined psion (and related) class level above 1 (max +40%). Psionic rolls are also multiplied by it.',
 'LP_GunPower': 'Strength multiplier for gunslinger abilities: 0.5 + Shooting/20, plus Dexterity modifier × 0.05 and (proficiency − 2) × 0.02; +2% per combined martial class level above 1 (max +40%).',
 'LP_Sub_Metamorph': 'Treats flesh like clay: mutable form, organic weapons (bone blade, flesh maul, viscera launcher), flesh weaving, and finally weapons that both kill and heal. Always-prepared spells by level.',
 'LP_Sub_Psykinetic': 'Molds telekinetic force: hauling, pushing, crushing, rebounding, and finally lifting and hurling anything.',
 'LP_Sub_Telepath': 'A tactician of the mind: reads minds, interrogates prisoners, distracts enemies, links allies, and finally scrambles a whole group\'s minds.',
 'LP_Sub_PsiWarper': 'Folds space: teleports, swaps places, rescues allies, places beacons, and finally moves a whole group at once.',
 'LP_Sub_Drifter': 'A short gun and a blade: alternating cuts and shots, repositioning after every takedown, ending in a blade-and-two-shots combo.',
 'LP_Sub_Pistolero': 'Two short guns: twin shots, fanning, return fire, and a high-noon duel.',
 'LP_Sub_Sniper': 'Waits where enemies can\'t see: concealment, piercing shots, spotting for the squad, and a thousand-yard stare.',
 'LP_Sub_Vanguard': 'Heavy weapons at the front: point-blank blasts, covering fire, breaching, and a steel storm.',
 'LP_Feat_Class_PsionTempered': 'Psionic power +15%; psionic points +2; faster psyfocus gain while meditating. Psion only. Grants Intelligence +1.',
 'LP_Feat_Class_PsionSilent': 'Psionic power +10%; mind resistance +30%, psychic sensitivity +10%, neural heat recovery +25%; psion ability cooldowns ×0.85. Psion only. Grants Intelligence or Wisdom +1.',
 'LP_Feat_Class_GunExpert': 'Gunslinging power +10%; violent shot never jams; faster general labor; gunslinger ability cooldowns ×0.9. Gunslinger only. Grants Dexterity +1.',
 'LP_Feat_Class_GunRenown': 'Gunslinging power +15%; grit +2; shooting accuracy +5%. Gunslinger only. Grants Dexterity or Wisdom +1.',
}
FOLDER = {'AbilityDef': 'AbilityDef', 'HediffDef': 'HediffDef', 'StatDef': 'StatDef', 'ThingDef': 'ThingDef', 'LichPhylactery.LP_ClassDef': 'LichPhylactery.LP_ClassDef',
          'LichPhylactery.LP_SubclassDef': 'LichPhylactery.LP_SubclassDef', 'LichPhylactery.LP_FeatDef': 'LichPhylactery.LP_FeatDef', 'LichPhylactery.CasterListDef': 'LichPhylactery.CasterListDef'}
missing = []
for src in ['ClassPsion.xml', 'ClassGunslinger.xml']:
    t = open(os.path.join(ROOT, 'Defs', src), encoding='utf-8').read()
    by = {}
    for m in re.finditer(r'<(AbilityDef|HediffDef|StatDef|ThingDef|LichPhylactery\.[A-Za-z_]+)(?: [^>]*)?>\s*(?:<hediffClass>[^<]*</hediffClass>\s*)?<defName>([^<]+)</defName>(.*?)</\1>', t, re.S):
        kind, dn, body = m.group(1), m.group(2), m.group(3)
        zl = re.search(r'<label>([^<]*)</label>', body)
        if dn not in L: missing.append(dn); continue
        lines = [f'  <!-- {zl.group(1) if zl else ""} -->', f'  <{dn}.label>{X(L[dn])}</{dn}.label>']
        if dn in D: lines.append(f'  <{dn}.description>{X(D[dn])}</{dn}.description>')
        by.setdefault(kind, []).extend(lines)
    for kind, lines in by.items():
        d = os.path.join(ROOT, 'Languages', 'English', 'DefInjected', FOLDER[kind]); os.makedirs(d, exist_ok=True)
        with open(os.path.join(d, src), 'w', encoding='utf-8', newline='\n') as f:
            f.write('<?xml version="1.0" encoding="utf-8"?>\n<LanguageData>\n' + '\n'.join(lines) + '\n</LanguageData>\n')
print('missing', missing)
