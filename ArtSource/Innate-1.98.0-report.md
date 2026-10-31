# 1.98.0 天赋强度加强（和 1.97.0 奥术强度一样）

需求（Peter）：术士「天赋强度」LP_InnatePower 的数值改成和法师「奥术强度」LP_ArcanePower 一样。

## 改动
- **Defs/Classes2.xml**（LP_InnatePower）：`StatPart_ClassPower` 覆盖 `levelPer 0.05`、`levelMax 0.95`、`abilityPer 0.10`，说明文字同步。其他强度属性没动（神恩 / 契约 / 野性 / 生长 / 器械仍 2% / 40% / 5%）。
- **Source/Adventurer.cs**：新增常量 `InnateAbilityPer = 0.1`；属性效果表（魅力）新增“天赋强度（魅力）”一行。
- **Source/AttributePage.cs**：属性面板魅力页新增“天赋强度（魅力）”一行（照 1.97.0 的“奥术强度（智力）”）。
- **Source/ClassUIChecks.cs**：-Only Classes 第 6 条：术士 10 级、社交 12、魅力 18、冒险者 10 级 → 天赋强度 ×1.99，下一级预算 +5%，职业页提示写“+5%”“最多 +95%”，说明写“×10%”；神恩强度仍 5% / 2%。
- **About/About.xml**：版本 1.97.0 → 1.98.0，描述末尾加更新说明。
- **使用说明.txt**：更新日志加 1.98.0 条目；“职业强度 =”公式与“强度：”段落补上天赋强度。

## 公式（1.98.0）
天赋强度 = 0.5 + 社交/20 + (Talented(1 + 0.10 × 魅力调整值′, 天赋) − 1) × 属性效果强度 + 0.02 × (熟练 − 2) + clamp(0.05 × (社交职业等级合计 − 1), 0, 0.95) + 职业专长 + 血脉 0.15 + 血脉觉醒 0.2。

职业等级合计 = 所有主 / 副技能是社交的职业（术士、牧师、邪术师、圣武士）等级之和，和奥术强度算法师 + 术士一样。

## 例子
术士 10 级、社交 12、魅力 18（+4）、熟练 +4，无天赋 / 种族亲和 / 专长 / 血脉，属性效果强度 100%：
- 旧：1.10 + 0.20 + 0.04 + 0.18 = ×1.52
- 新：1.10 + 0.40 + 0.04 + 0.45 = ×1.99（有血脉 ×2.14；Isekai 共存时魅力项为 0 → ×1.59）
- 混沌箭（心灵敏感度 100%）：12 × 1.99 ≈ 23.9（旧 18.2），强效后 ≈ 35.8

## 测试
- `pwsh Source\Build.ps1`：主程序集和三个桥都通过。
- `Test.ps1 -Only Classes`：73 条全过。第一次跑时日志里有一条 “ThingsListAt out of bounds”（1.88.0 报告里记过的冲锋落点不稳定），重跑一次干净通过。
- `Test.ps1 -Only AttributePage`：4 条全过。

## 备份
ArtSource/backup-1.97.0-innate（Classes2.xml、About.xml、使用说明.txt、Adventurer.cs、AttributePage.cs、ClassUIChecks.cs、LichPhylactery.dll）。没有 commit / push。
