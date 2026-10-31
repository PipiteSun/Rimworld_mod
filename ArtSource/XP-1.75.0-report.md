# DND边缘大陆 1.75.0 — 非战斗经验 + 实战磨砺实时累计

Backup: `ArtSource/backup-1.74.0-hotfix` (`backup-1.74.0` already existed from the social hotfix and was left untouched).

## Files changed
- NEW `Source/AdventurerExtraXp.cs` — the five sources, `ResearchShares` GameComponent, Harmony patches, settings fields + `Settings.DrawExtraXp`.
- NEW `Source/AdventurerChecks8.cs` — `CheckAdventurerExtraXp` (12 checks) + save/load fixture.
- `Source/Adventurer.cs` — Hediff_Adventurer fields `exToday[5] / exTotal[5] / exDay / exDownPower / exSurviveTick` (saved as `lpExXp*`, `lpExDownPower`, `lpExSurviveTick`); `ExposeExtraXp()` in `ExposeAdventurer`; `Settings.DrawExtraXp(l)` in the existing 冒险者 settings group (group count unchanged, still 19).
- `Source/AdventurerUI.cs` — XP-bar tooltip: one line per source (`AdventurerExtraXp.Tip`).
- `Source/AdventurerChecks.cs` — calls the new checks and save/load parts.
- `Source/Growth.cs`, `Source/GrowthChecks.cs`, `Source/HeroTraits.cs` — 实战磨砺 real-time (see below).
- `About/About.xml` → 1.75.0; `使用说明.txt` header, a 【1.75.0 非战斗经验】 section, and a changelog line.

## Sources (all via `Adventurer.GainXP`, so 冒险者经验倍率 / 凡人之志 / class factor / level letters apply)
Only player-faction pawns or colony slaves with LP_Adventurer (`AdventurerWork.Counts`). The 非战斗经验倍率 slider (0–3, default 1, 0 = all off) applies to all five. Daily caps count the **base** amount (before both multipliers), reset at local midnight (`Feats.LocalDay`), the same way 日常工作 works.

| Source | Harmony target (verified in 1.7 DLL) | Rule |
|---|---|---|
| 治疗与手术 | Postfix `Recipe_Surgery.CheckSurgeryFail(Pawn surgeon, Pawn patient, List<Thing>, BodyPartRecord, Bill)` (`__result==false` = success); Prefix/Postfix `TendUtility.DoTend(Pawn doctor, Pawn patient, Medicine)` | surgery +30; tend +15 if before the tend `TicksUntilDeathDueToBloodLoss < 6 h`; cap 150/day shared |
| 交易 | Prefix/Postfix `TradeDeal.TryExecute(out bool actuallyTraded)` | value computed in prefix: Σ abs(CountToTransfer) × GetPriceFor(ActionToDo), non-currency tradeables; paid only if `__result && actuallyTraded`; gift mode skipped; 1 per 100 silver; cap 100/day |
| 建筑完工 | Prefix/Postfix `Frame.CompleteConstruction(Pawn worker)` | `max(2, WorkToBuild/1000)`; **floors (TerrainDef frames) and zero-work skipped** (otherwise each floor tile = 2 XP); cap 100/day |
| 研究完成 | Prefix `ResearchManager.ResearchPerformed(float, Pawn)` records raw amount for `GetProject(null)`; Prefix/Postfix `ResearchManager.FinishProject(...)` (only when it actually transitions to finished) | `Cost × 5%` split by contribution share; non-colonist / dead shares stay in the denominator but aren't paid; shares saved in GameComponent `ResearchShares`, cleared on finish; no cap |
| 重伤倒地生还 | Postfix private `Pawn_HealthTracker.MakeDowned(DamageInfo?, Hediff)` and `MakeUndowned(Hediff)` | remembers instigator combatPower when the instigator is a Pawn, not self, and `HostileTo` the victim; on undown alive → `50 + 0.5 × power`; once per 180000 ticks |

Patch errors are caught and logged once (`Log.ErrorOnce`) for surgery/trade/research.

## 实战磨砺 real-time (Peter: 「然后给属性磨砺改成实时增加经验」)
Reading chosen: 磨砺 progress now grows the moment the activity happens.
- Found while testing: the old sampler diffed `SkillRecord.XpTotalEarned`, which in 1.7 only counts **completed levels** — so skill-based 磨砺 only moved on skill level-ups, then only at the next 2500-tick sample. Now progress = `XpTotalEarned + xpSinceLastLevel`.
- Hooks: `SkillRecord.Learn` prefix/postfix (delta of that progress; same Sources coefficients: melee ×1 / mining ×0.5 → 力量, shooting → 敏捷, intellectual → 智力, medicine/animals/plants ×0.5 → 感知, social → 魅力) and `Pawn_RecordsTracker.AddTo` postfix for `DamageTaken` ×100 → 体质. No per-tick scans; the 2500-tick `Growth.Sample` call was removed from HeroTraits' periodic loop and `Sample` deleted (`grindLast` is still saved but unused).
- If another prefix skips `SkillRecord.Learn` (知识亡灵 intercept), `__state` stays 0 and nothing is counted (guarded).
- Only free colonists (as before: the sampler looped free colonists). Tab bars/tooltip now read "实时累计".
- Not chosen: feeding 磨砺 into adventurer XP.

## Tests
- `pwsh Source\Test.ps1 -Only Adventurer` → **PASS 145** (was 132; +12 non-combat checks, +1 save/load).
- `pwsh Source\Test.ps1 -Only Growth` → **PASS 14** (was 13; grind checks rewritten for live accrual + new skill-XP check).
- New checks cover: all 8 targets patched; real surgery success +30 / forced failure 0; real `DoTend` light cut 0 / near-bleed-out +15; heal cap 140→+10→0 and day reset; trade 2500 → 25, cap; real steel-wall frame completion → minimum 2, 5000 work → 5, cap; research 300/100/100 split (colonists 60% / 20%, enemy unpaid, shares cleared); downed by hostile → 50+0.5cp, cooldown blocks, after 3 days pays; self/friendly/no-attacker downs 0; each toggle off → 0; multiplier ×2 / ×0; tooltip lines; save/load of counters, pending down record and research shares.

## Untested in game
- Real trade dialog execution (only the value/award path is tested; `DealValue` on a live TradeDeal is not exercised).
- Real `ResearchPerformed` during play and `FinishProject` via actual research (handlers tested directly).
- Real downing/undowning via combat (handlers tested directly; Harmony attachment verified).
- Downed by bleeding after being shot (`MakeDowned` with no dinfo) does not count — by design of the spec, but players may expect it.
- Settings UI layout of the 6 new rows; full test suite not run (per instructions).
