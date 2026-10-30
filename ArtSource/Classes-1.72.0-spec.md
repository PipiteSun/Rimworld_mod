# 1.72.0 职业体系脱离 VPE —— 第一版（框架 + 战士/武僧/圣武士）

Peter approved 2026-10-29 「按照你的方案构建职业体系」. Peter wants you to decide everything yourself; record decisions in report §决定.

Mod: E:\SteamLibrary\steamapps\common\RimWorld\Mods\LichPhylactery (current 1.71.0). Build `pwsh Build.ps1`, tests `pwsh Test.ps1 -Only <Group>` (targeted only, NEVER full suite; add -VPE for bridge groups). Game must be closed to compile. All in-game text Chinese. Never write d20/掷骰/检定/DC; use 「成功率 X%」.

## 0. Before coding
- Backup changed/affected files to ArtSource/backup-1.71.0 (Source, Defs, Bridges/VPE, About, 使用说明.txt, Assemblies).
- Read existing code: Bridges/VPE/Source (Classes*.cs, ClassRules, Paladin.cs, Monk.cs, Martial*, Artificer.cs), main-assembly AdventurerStyles.cs, Adventurer tab, LP_MartialPower/LP_MarksmanPower StatParts, 斗志/额外攻击 code.

## 1. Framework (main assembly, no VPE/VEF reference)
- Class data on the existing LP_Adventurer hediff (or a sibling comp): dict classDef → classLevel, plus unspent class points. Save/load via Scribe.
- `LP_ClassDef` (new Def): label, mainSkill, alternateSkill, order, resource type, martial flag, ability list with requiredClassLevel, capstone (class lvl 20 + mainSkill 18). Lich forbidden from 圣武士 (keep existing rule).
- Class points: every adventurer level gives 1 point; sum of class levels ≤ adventurer level (cap 20). Legendary 21–40 gives none (later phase).
- Unlock: first class needs main or alternate skill ≥6. 2nd/3rd class: adventurer lvl ≥3 and that class's skill ≥8. Max 3 classes.
- Abilities granted automatically at class level 1/3/5/9/13/17 (map current 9 abilities per path onto these + capstone 20; fighter has 2 branches → fit both, may give 2 at some levels; record mapping in report).
- Level-up: when a point is unspent, show a gizmo/letter that opens a Dialog (NOT FloatMenu — FloatMenu pickers fail in Peter's game) to choose the class. Also assignable from the tab.
- UI: new 「职业」 subpage inside the 冒险者 tab: class levels + resource bars + our own ability tree (4 per row, reuse existing path background/icons).

## 2. Abilities
- Use vanilla RimWorld AbilityDef + CompAbilityEffect (Royalty is hard dep). Port the 战士 (incl. 射击支), 武僧, 圣武士 abilities from Bridges/VPE into main assembly; keep effects/numbers. Gizmos show on the pawn like vanilla psycasts but no psylink needed and no psyfocus/entropy cost.
- Strength stats LP_MartialPower/LP_MarksmanPower/paladin+monk stats must work in main assembly (move StatParts if bridge-only). Replace the old 「+0.02×(VPE lvl−1)」 term with +0.02×(classLevel−1), same cap.
- Paladin/monk psychic factor: keep current behavior as a setting 「施法类能力受心灵敏感度影响」 default on (only flagged classes; 战士 never).

## 3. Resources
- 战士: 斗志 (existing). 武僧: 气 (max = 2 + classLevel/2 rounded, regen while meditating, refill on sleep). 圣武士: 神恩 (max = 2 + classLevel/2, regen praying/meditating or sleep refill; +25% regen if pawn follows a deity in the faith system).
- Each ported ability gets a resource cost (1 basic, 2 mid, 3 capstone) + keeps cooldowns. Decide numbers, record in report.
- Setting 「职业能力消耗心灵熵」 default off (if on and pawn has psylink, abilities also add entropy).

## 4. VPE coexistence (Bridges/VPE)
- With VPE: remove LP_Path_Fighter/Paladin/Monk from VPE's DND职业 tab (keep other 5 paths — they move in phase 2). Martial passives (斗志, 额外攻击, 战斗风格) read the new class levels.
- Without VPE: 战士/武僧/圣武士 fully playable.
- 冒险者手册: unlocks one extra class ignoring the skill gate.
- Mentors for these classes give adventurer XP instead of VPE XP.

## 5. Old-save migration (on load, once)
- Pawns with learned abilities of the 3 paths: classLevel = ceil(learned/2), capped by remaining adventurer-level points; keep every already-learned ability even if above the capped level (never take power away). Grant the matching new abilities; remove the old VPE abilities of those paths. Refund VPE points if easy, else note.
- Pawns without class data: unspent class points = adventurer level; letter tells player to pick classes.

## 6. Tests (targeted only)
New group `-Only Classes` (≤15 checks): point grant per level, unlock gates, multiclass gate, ability granted at level, resource spend/regen, migration from fake VPE data (with -VPE), no-VPE path, save/load round trip. Also rerun `-Only Martial` and `-Only Adventurer`. No full suite.

## 7. Deliverables
- About.xml version 1.72.0 + 使用说明.txt entry (exact anchor edits, Chinese).
- Report ArtSource/Classes-1.72.0-report.md: changes, ability→level mapping, resource numbers, 决定, Harmony hooks list, untested-in-game list.
- Don't delete VPE ability defs of the other 5 classes; don't touch other threads' files beyond need.
