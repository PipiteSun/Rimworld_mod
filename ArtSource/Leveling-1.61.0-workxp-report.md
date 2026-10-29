# 1.61.0 热修：日常工作经验（冒险者等级）

版本号不变（1.61.0）。备份：`ArtSource/backup-1.61.0-workxp`。

## 问题
冒险者经验只来自击杀、袭击、任务、里程碑等冒险活动，科研、手工、种植等非战斗殖民者一直停在 1 级。

## 规则（第二版：50:1、每天 80 全额、之后 ×0.5）
- 每次非 direct 的技能学习（`Pawn_SkillTracker.Learn(sDef, xp, direct=false, ignoreLearnRate)`），按**原始**技能经验（在 Adventurer 属性学习速度前缀之前取值，热情等原版倍率也不计入）折算：`冒险者经验 = 技能经验 / 50 × 日常工作经验倍率`。
- 每人每个本地日（`Feats.LocalDay`，按地图经度，午夜换日）前 80 点全额，之后 ×0.5，不封顶。跨过 80 时按比例拆分（70 + 20 → 10 + 5）。
- 不满 1 点的部分攒在余数里，满 1 点才调用 `Adventurer.GainXP`（再乘冒险者经验倍率和凡人之志，升级、信件照常）。
- 只算殖民地的人（玩家阵营或奴隶）、活着、有 LP_Adventurer；技能被完全禁用的不算；传奇等级（21～40）一样算。
- direct=true（技能训练、神经增强等直接加经验）不算；冒险者总开关关闭或倍率为 0 时不算。
- 第一版是 100:1、每天 40 全额、之后 ×0.25，按那个速度纯工人 10 级要约 9 游戏年，太慢，改成现在的数值。

## 改动
- 新文件 `Source/AdventurerWork.cs`：`AdventurerWork`（常量 `SkillPerXp=50`、`DailyFull=80`、`OverFactor=0.5`；`Mult` / `Counts` / `Today` / `Diminish` / `Learned`）、Harmony 前缀 `AdventurerWorkLearn`（`Priority.First`）、设置字段 `adventurerWorkXp`（默认 1.0，0～3，存档键 `adventurerWorkXp`）。
- `Source/Adventurer.cs`：`Hediff_Adventurer` 新字段 `workXpToday / workXpDay / workXpRem / workXpTotal`（存档键 `lpWorkXpToday / lpWorkXpDay / lpWorkXpRem / lpWorkXpTotal`）；`ExposeAdventurer` 调用 `ExposeWork()`；设置“冒险者”分组在“冒险者经验倍率”下面加滑条“日常工作经验倍率”（步长 0.1，悬停说明规则）；总开关说明加一句“日常工作也有少量”。
- `Source/AdventurerUI.cs`：经验条悬停的来源列表加一行“· 日常工作：每 50 技能经验 +X（今日 x / 80 全额，之后 ×0.5；累计 N）”，倍率 0 时“· 日常工作：已关闭”。X 和累计都已乘当前经验倍率（与其他行一致）。原来没有按来源累计的统计，所以只有日常工作自带累计值。
- `Source/AdventurerChecks7.cs`（新）+ `AdventurerChecks.cs`：`-Only Adventurer` 新增 8 条（1000 → +20、满 80 后 1025 → +10.25、跨 80 拆分 → 15、换日重置、倍率 0 关闭 / 倍率 2 → +40、direct 和敌人不算、悬停说明）+ 存读档 1 条（今日量、日、余数、累计）。
- `使用说明.txt`：更新日志加一行；第二部分冒险者“经验”一节标题改写、加“日常工作”一条；悬停说明一节加上日常工作。

## 预期进度（默认倍率 1.0、经验倍率 100%）
一个整天干活、每天约 4000 技能经验的人 ≈ 每天 80 冒险者经验（刚好在全额额度内）。RimWorld 一年 60 天。

| 等级 | 累计经验 | 约需 |
|---|---|---|
| 2 | 100 | 1.25 天 |
| 3 | 300 | 3.75 天 |
| 4 | 900 | 11 天 |
| 5 | 2200 | 27.5 天 |
| 8 | 11300 | 141 天（约 2.4 游戏年） |
| 9 | 16000 | 200 天（约 3.3 游戏年） |
| 10 | 21300 | 266 天（约 4.4 游戏年） |

注：协调说明里“8 级 (16000) ≈ 200 天”的 16000 是 9 级的下限；8 级是 11300（约 141 天）。冒险（一次袭击、任务就是几十到几百）仍然快得多。

## 自检
2026-10-28：RimWorld 未运行，Build.ps1 四个程序集全部编译通过；`.\Source\Test.ps1 -Only Adventurer` → **PASS: 131 checks (only Adventurer); save/reload**（0 失败，含新增的 8 条日常工作检查和 1 条存读档检查）。
