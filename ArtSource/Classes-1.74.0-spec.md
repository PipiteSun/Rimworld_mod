# 1.74.0 传奇职业能力

GO phase 4 as 1.74.0 from 1.73.0. Same rules (targeted -Only tests only, game closed to compile, Chinese text, no d20 wording, decide details yourself, record 决定). Back up to ArtSource/backup-1.73.0 first, save this spec as ArtSource/Classes-1.74.0-spec.md, don't commit.

1. Class points: legendary levels 21–40 give NO class levels (class levels stay capped at 20 total). Instead at legendary adventurer levels 25/30/35/40 the pawn gets 1 「传奇职业点」 (4 max). Old saves already above those levels get the points retroactively once.
2. New abilities, not upgrades: each of the 8 classes gets 2 legendary abilities (1 active + 1 passive) = 16. Spend a 传奇职业点 to learn one, via the same Dialog/gizmo/letter flow and a 「传奇」 row on the 职业 page. Requirement: that class level ≥9 for the first legendary of that class, ≥17 for the second. Each pick also gives +2 max to that class's resource.
3. Suggested set (rename/tune freely, keep power in line with legendary scale where colonists have STR 24+; actives cost 3 + long cooldown 1–3 days):
- 战士: 不屈战魂 (passive: once per day, instead of going down, heal 50% and full 斗志) / 千军破 (active: cone melee sweep or ranged volley using weapon damage ×3 on all enemies in range).
- 武僧: 无相之身 (passive: +25% dodge, immune to stun) / 天震掌 (active: one touch, huge damage + 10s stun, 成功率 based save vs legendary).
- 圣武士: 神圣化身 (active: 30s aura — allies immune to fear/mental break, heal per second, holy damage to enemies) / 誓约不灭 (passive: allies within 8 cells 豁免成功率 +10%).
- 法师: 法术强效 (passive: arcane spells ×1.3 and cost −1, min 1) / 流星爆 (active: 4 meteor impacts in an area).
- 牧师: 神迹干预 (active: once per 30 days, full heal + revive recently dead colonists on map, uses faith deity flavor) / 神圣守护 (passive: 复生术 costs 2, divine power +20%).
- 德鲁伊: 永恒德鲁伊 (passive: stops aging + wild shape costs 0) / 自然之怒 (active: storm + entangling vines on an area).
- 术士: 血脉觉醒 (passive by bloodline: matching damage resistance +50% and innate power +20%) / 魔力奔流 (active: next 3 spells free and no cooldown).
- 器械师: 魂铸 (passive: crafted DND weapons +1 quality, and 10% chance legendary) / 战争傀儡 (active: summon a rune construct/turret for 2 days; reuse existing rune turret/construct art).
4. Icons: draw 16 new icons in the existing RimWorld-style class art script (thick outline, flat fill + shade, low saturation).
5. Tests: extend -Only Classes (≤10 new checks: point grant at 25/30/35/40, retroactive grant, requirement gates, resource +2, one active + one passive each behaves, save/load). Also -Only Adventurer. No full suite.
6. Deliverables: About.xml 1.74.0, 使用说明.txt entry, report ArtSource/Classes-1.74.0-report.md (ability table with numbers, 决定, hooks, untested list).
