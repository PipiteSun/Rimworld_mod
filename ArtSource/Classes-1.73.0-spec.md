# 1.73.0 职业体系第二版：法师/牧师/术士/德鲁伊/器械师 脱离 VPE

GO for phase 2 as version 1.73.0, built on your 1.72.0. Same rules as before (targeted Test.ps1 -Only only, game must be closed to compile, Chinese text, no d20 wording, decide yourself, record 决定 in report). Don't commit or push. First back up affected files to ArtSource/backup-1.72.0 and save this spec to ArtSource/Classes-1.73.0-spec.md. Report back when done.

Port the remaining 5 VPE paths (LP_Path_Wizard, Cleric, Sorcerer, Druid, Artificer) into the 1.72.0 main-assembly class framework, same pattern as 战士/武僧/圣武士: LP_ClassDef each, abilities as vanilla AbilityDef with same defNames/numbers/cooldowns, unlock at class lvl 1/3/5/9/13/17/20 (capstone also main skill 18), existing skill gates and alternateSkill (德鲁伊 种植/驯兽, 术士 社交/智识), bloodline −2 sorcerer threshold, lich can't be 德鲁伊.

Resources:
- New 法力 for 法师/术士/德鲁伊: max = 2 + classLevel/2, refills on sleep only (+0.5/250 ticks), plus 法师 regains slowly while reading/studying at a research bench or scribe desk (+0.1/250 ticks). Costs 1/2/3.
- 牧师 uses 神恩 (shared rules with 圣武士; if a pawn has both classes, one shared 神恩 bar is fine — decide and record).
- 器械师: no resource, cooldowns only. Its passives keep gating DND weapon crafting: change Artificer.RequiredAbility gate to class level (奥术工坊 tier → 器械师 lvl ≥1, 符文铭刻 tier → lvl ≥5, 山岳锻造 → the ability that grants it); move the gate to main assembly so it works without VPE; keep setting 「DND武器需要器械师」.
- Psychic sensitivity factor applies to 法师/牧师/德鲁伊/术士 abilities when setting 「施法类能力受心灵敏感度影响」 is on.
- Strength stats LP_ArcanePower/LP_DivinePower/LP_PrimalPower/LP_VerdantPower/LP_InnatePower (+ sorcerer bloodline StatPart) must work in main assembly; add +0.02×(classLevel−1) max 40% like martial.

VPE coexistence: with VPE all 8 of our paths are hidden; the 「DND职业」 VPE tab should disappear entirely if empty. Old neurotrainers for our abilities: stop generating them if easy (else keep refusing use).
Old-save migration: same rule as 1.72.0 (ceil(learned/2), min 1, keep abilities, refund VPE points), for these 5 paths; must work when 1.72.0 migration already ran.
Mentors for these classes give adventurer XP like 1.72.0. Acolyte→paladin / hermit→monk XP bonuses: keep equivalent on adventurer XP if they existed.
Also fix the stale -VPE -Only Adventurer check (法术狙击 expects ×1.5, code ×1.75) by updating the expectation.

Tests: -Only Classes (extend ≤10 new checks), -VPE -Only Classes, -Only Adventurer, and -Only Guns or whatever group covers the artificer gate, -Only Faith (神恩强度 change from 1.72). No full suite.
Deliverables: About.xml 1.73.0, 使用说明.txt entry, report ArtSource/Classes-1.73.0-report.md (mapping, resource numbers, 决定, hooks, untested list).
