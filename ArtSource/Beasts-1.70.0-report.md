# 1.70.0 野兽强化 — report

Backup: `ArtSource/backup-1.69.0` (1.7, About, Bridges, Defs, Languages, Patches, Source, 使用说明.txt, LoadFolders.xml). Not committed.

## Files
- **New** `Source/BeastLevels.cs` — `BeastLevels` (formula, level/elite/pack logic, growth, Isekai detection, death), `Hediff_BeastLevel` (indexed via `HediffIndex<>`), `Hediff_BeastElite` (display only), `BeastPack` + `MapComponent_BeastPacks`, Harmony hooks, settings fields.
- **New** `Source/BeastLevelChecks.cs` — check group `BeastLevels` (42 checks incl. save/reload).
- **New** `Defs/Beasts_Levels.xml` — `LP_BeastLevel`, `LP_BeastElite`, `LP_BeastKingHeart` (兽王之心), `LP_BeastKingAmulet` (兽王护符 + recipe at `LP_EnchantingTable`).
- `Source/LimbHealth.cs` — `PartHealth.WithoutReinforcement` also multiplies `BeastLevels.PartHealthFactor(pawn)` (returns 1 immediately for non-animals).
- `Source/Beasts.cs` — `ExposeBeasts` saves the 2 new settings; `BeastSettings.Draw` (existing 野兽 group) gets 「野兽野性等级」 and 「精英野兽概率」. No new settings group.
- `About/About.xml` 1.70.0; `使用说明.txt` title, part-1 line, part-2 【1.70.0 野兽强化】, changelog line.

## Design choices
- **Who gets a level:** `RaceProps.Animal`, excluding insects (infestations would otherwise get much harder), anomaly entities, `Legendary.Is`, `Dragons.Is`, necro constructs / conjured (`Adventurer.Excluded`).
- **When:** `Pawn.SpawnSetup` postfix (first, non-load spawn) → ambient rule; `AggressiveAnimalIncidentUtility.GenerateAnimals` postfix (both overloads; covers the vanilla manhunter incident, ambush, manhunter sites) → pack rule; `PawnGroupMakerUtility.GeneratePawns` postfix → hostile combat groups use points ÷ group size. Old saves: `MapComponent_BeastPacks.FinalizeInit` gives missing levels once (saved `initVersion`).
- **Level formula:** pack/group = `AdventurerEnemies.BaseLevel(per pawn)` ±2, cap = colony max adventurer level +3 (max 40). Ambient = 1..max(1, colony max −3). Colony max is cached for one in-game hour. Packs without a points value (list overload) use each animal's combat power.
- **Virtual STR/CON:** level 1→10, 20→20 linear, then +0.4/level (30→24, 40→28); `Adventurer.Curve` with interpolation between integer scores. Melee ×curve, part HP ×(1+1%/lvl)×curve, speed +1%/lvl max +30%. Level 1 = ×1 exactly (tested against a no-hediff animal).
- **Elites** live in the level hediff (`elite` field) so all numbers sit in one stage; `LP_BeastElite` is a coloured display row. 恐怖 part HP ×3 / melee ×1.5; 狂暴 `MeleeCooldownFactor` ×0.67, `neverFleeIndividual` (re-set on load) + `FleeUtility.ShouldAnimalFleeDanger` → false; 远古 level +5, sharp/blunt armor +0.40 offset; 深渊腐化 extra Burn = 30% of melee damage dealt (min 1). Chance setting 5% default, clamped 0–20%.
- **Leader:** highest combat power animal in the pack, always elite, level +3 after the cap, named 「首领XX」 if nameless; elite row label starts with 「首领 · 」. Pack id = leader's thingIDNumber, stored on each hediff and registered per map on spawn (`MapComponent_BeastPacks`, saved, pruned on save). Leader death (`Pawn.Kill` prefix/postfix): remaining members recover from aggro mental states, try PanicFlee, and get `exitMapAfterTick = now` (vanilla manhunter exit mechanism) — 狂暴 followers too.
- **Heart drop:** any elite (also tamed ones) drops one 兽王之心 at death position. 兽王护符: heart ×1 + silver 100 + gold 10, Crafting 6, belt layer; +0.10 `MeleeDamageFactor`; 驯兽 +2 via `SkillRecord.Aptitude` getter postfix (only runs work for the Animals skill).
- **Growth:** player animals: melee XP = damage dealt (`ApplyMeleeDamageToTarget` postfix), job XP on `Pawn_JobTracker.EndCurrentJob(Succeeded)`: HaulToCell / HaulToContainer / HaulToTransporter / UnloadYourInventory +5, Rescue +20. N→N+1 needs 100×N (the adventurer table is too steep for animals). Cap = master's adventurer level, else colony max; adventurer system off → 20; hard cap 20. Over-cap tamed animals keep their level and stop gaining. Message 「XX 的野性等级提升到 N」.
- **动物伙伴 (+25%)**: the mod has no ranger class, so ranger = 游侠向导 backstory (`LP_Bs_RangerGuide`) or wearing 游侠皮甲 / 林行者斗篷; druid = VPE path `LP_Path_Druid` read by reflection (main assembly does not reference VPE).
- **Isekai:** if the pawn carries Isekai's `MobRankComponent`, the hediff stays for display, applies no stat/part-HP multipliers, and the tooltip says 「已由异世界等级接管」. Elite behaviours (burn, no-flee, heart, leader scatter) still work.
- **Per-tick cost:** none added. Hooks are spawn, generation, melee hit, job end, death, flee decision, inspect string, aptitude getter, part max-HP (one race flag + dictionary lookup).
- **Skipped visuals:** 恐怖 draw size ×1.3 and 深渊腐化 dark-red tint are not done (would need render-node patches); the coloured hediff labels carry the information instead.
- **Test isolation:** `BeastLevels.TestSuppressed` is on in the lichtest process (like other systems); only the BeastLevels group turns it off, so other groups' animals are unchanged.

## Tests
Build: `pwsh Build.ps1` in Source → PASS (main + HAR/VPE/Mythic bridges).

- `pwsh Test.ps1 -Only BeastLevels` → **PASS: 42 checks (only BeastLevels); save/reload.**
- `pwsh Test.ps1 -Only BeastLevels,Beasts` → **PASS: 218 checks (only BeastLevels,Beasts); save/reload.** (Beasts group 176, unchanged)

During development one run timed out at game startup (log stopped during load, stale result file); the rerun passed with no code change. Earlier failures were fixed: wrong vanilla texture path (`HealthItemNatural` doesn't exist → `Things/Item/Health/HealthItem`), master assignment needs Obedience, test colonist already had an adventurer level.

Coverage: defs/classes/Chinese labels/textures/recipe; Score/Mult/Part/Speed values (1→×1, 20→×4, 30→×7); pack formula and cap; ambient range; level-1 vanilla equality (melee, torso HP, speed); level 20 numbers; toggle off; elite chance setting incl. clamp; all four variants; heart drop; pack leader choice/level/name, map registration, leader death → followers leave manhunter state and exit; growth (100×N, master cap, hard cap 20, no-master cap, melee XP, job XP values, ranger companion +25%, over-cap tamed animal, player-born level 1); amulet 驯兽 +2; Isekai takeover (simulated flag); old-save lazy init runs once; save/load of level/elite/leader/pack; no dice wording in new texts.

Not run: the full suite (per instructions). Limb checks live in the core run only; the `PartHealth` change returns ×1 for every non-animal before any lookup.

## Not verified in game
- A real manhunter pack from the incident (dev mode → 狂暴兽群): levels/elite/leader on real spawn, and that followers actually run off the map after the leader dies (test confirms state changes, not the walk).
- Ambient wild animals in a colony with high adventurer levels (spawn distribution).
- Taming an elite / high-level animal and growth during real hauling and fighting; level-up message.
- 狂暴 elites really not fleeing at low health in live AI.
- With Isekai loaded (only simulated): that `MobRankComponent` is found and our multipliers are off.
- With VPE: druid path detection by reflection.
- How the coloured labels and 兽王护符 / 兽王之心 tinted textures look.
