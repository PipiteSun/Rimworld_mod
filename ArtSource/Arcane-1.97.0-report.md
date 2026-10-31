# 1.97.0 奥术强度加强 — 报告

Peter：「将法师等级提升改成+5%，智力调整值改成10%」。只改 **奥术强度（LP_ArcanePower）**。

## 公式

奥术强度 = 0.5 + 智识等级/20
　+ 智力项：(天赋(1 + **10%** × 智力调整值′) − 1) × 属性效果强度（Isekai 接管时为 0）
　+ 熟练项：2% × (熟练加值 − 2)（不变）
　+ 职业等级项：**5%** × (用智识的职业等级合计 − 1)，最多 **+95%**（= 20 级）
　+ 职业专属专长（奥术学者等，不变）

其他强度（战技 / 射术 / 神恩 / 野性 / 生长 / 天赋 / 器械 / 契约 / 狂怒 / 猎术 / 诡术）仍是 2% / 最多 40% / 调整值 5%。

## 例子：法师 10 级、智识 15、智力 18（调整值 +4）、冒险者 10 级（熟练 +4）

| 项 | 1.96.0 | 1.97.0 |
|---|---|---|
| 0.5 + 15/20 | 1.25 | 1.25 |
| 智力 +4 | +0.20（4 × 5%） | **+0.40**（4 × 10%） |
| 熟练 +4 | +0.04 | +0.04 |
| 法师 10 级 | +0.18（9 × 2%） | **+0.45**（9 × 5%） |
| **奥术强度** | **×1.67** | **×2.14** |
| 下一级（法师 11） | ×1.69 | ×2.19 |

满级参考：法师 20、智识 20、智力 20（+5）、冒险者 20（熟练 +6）：旧 1.5 + 0.25 + 0.08 + 0.38 = ×2.21 → 新 1.5 + 0.50 + 0.08 + 0.95 = **×3.03**。

## 代码

- `StatPart_ClassPower`（Source/Classes.cs）新增可在 XML 设的字段 `levelPer`（默认 0.02）、`levelMax`（0.4）、`abilityPer`（0.05），以及 `Value / AbilityBonus / LevelBonus / LevelBonusFor / LevelPer / LevelMax`。格斗、射击仍走物理职业合计（Martial.Bonus），不受这些字段影响。
- Defs/Classes2.xml：LP_ArcanePower 的 part 写 `<levelPer>0.05</levelPer><levelMax>0.95</levelMax><abilityPer>0.10</abilityPer>`。
- `ClassSystem.LevelBonusFor(total,per,max)`、`ClassBonus(p,s,per,max)`；`Adventurer.ClassPowerBonus(p,ability,per)`、常量 `ArcaneAbilityPer = 0.1`。旧的 2 参数重载保留（VPE 桥还在用）。
- `ClassLevelInfo.PowerWith` / `ClassTip` 用 part 自己的比例（职业页 “现在 → 下一级”、悬停每级表、加点消息、升级信件）。
- 属性面板说明（ExplanationPart）写出真实比例：“主属性调整值 ×10%”“每比 1 高 1 级 +5%，最多 +95%”。
- 冒险者分页智力效果表、属性页（AttributePage）新增一行「奥术强度（智力）」。

## 文本

- 奥术强度说明改为具体比例；野性 / 生长 / 天赋 / 器械 / 契约强度说明也写明“调整值 × 0.05 与 (熟练 − 2) × 0.02、每级 +2%（最多 +40%）”。
- 使用说明：修正旧的“0.04 × 主属性调整值”；“强度”一段注明奥术例外；更新日志 1.97.0。

## 自检

- `-Only Classes`：72 PASS（新增 ClassUIChecks 第 5 条：法师 10、智识 15、智力 18 → ×2.14，下一级 +0.05，器械强度仍 5% / 2%，悬停与说明文本含 +5% / 最多 +95% / ×10%；ClassChecks 第 5 条改为法师 11 → +0.5）。
- `-Only Adventurer`：176 PASS（AdventurerChecks10 第 9 条改为奥术 +0.45、默认比例仍 +0.18、上限 0.95 / 0.4）。
- `-Only AttributePage`：4 PASS。
- Test.ps1 每次都因日志里一条已有的 `Config error in LP_GelCubeBody: no parts vulnerable to frostbite`（WildMonsters.xml，10-30 起未改）报 “Errors found in test log”，与本次无关。

备份：ArtSource/backup-1.96.0-arcane（ClassWarlock.xml 的备份是按编辑反推还原的）。未提交 git。
