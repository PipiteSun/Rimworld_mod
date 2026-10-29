# 1.52.0 热修：传奇种族不会食物中毒

About 版本保持 1.52.0（1.52.1 留给传奇怪物）。备份：`ArtSource/backup-1.52.0-foodpoison/`（使用说明.txt（改动前的损坏版本，见下）、LegendaryChecks.cs、ForceDragonChecks.cs、LichPhylactery.dll，均为改动前）。

## 范围（哪些算“传奇种族”）
`LegendaryFoodPoison.Immune(pawn)` 为真时免疫：
- 种族亲和表里标了传奇层（×1.75）的种族：`Affinity.LegendaryRace(p)`。目前是巫妖、火 / 霜 / 风暴 / 云巨人、双头巨人（含 HAR 种族）、独眼巨人（含 HAR 种族）、泰坦、觉醒泰坦、力能龙裔、夺心魔、巨灵、阿斯莫、六臂蛇人（含 HAR 种族）、寒冰魔、炎魔；以后在种族 / 异种 `LP_AdventurerAffinityExt` 上写 `legendary` 的也自动算。巫妖本来就不吃东西，算进去无害。半夺心魔是普通层，不算。
- 传奇单位（`LP_LegendaryExtension`，五色真龙等，1.52.1 的传奇怪也会自动算）：`Legendary.Is(p)`。
- 半神（成神之路的 `Hediff_Demigod`）：`Demigods.Is(p)`。奥术半神不需进食，没单独处理。

## 实现
新文件 `Source/LegendaryFoodPoison.cs`：
- `FoodUtility.GetFoodPoisonChanceFactor(Pawn)` 后缀：免疫者返回 0（原版吃东西、`CompFoodPoisonable`、`CompRottable` 都乘这个因子）。
- `FoodUtility.AddFoodPoisoningHediff(Pawn,Thing,FoodPoisonCause)` 前缀：免疫者直接跳过，兜底特性覆盖概率（`TryGetFoodPoisoningChanceOverrideFromTraits`）等直接加中毒的路径。
- 每次吃东西时现查，不加基因、不存档，旧存档直接生效，不需要迁移（力能龙裔 1.23.2 的强胃基因迁移保留不动）。

## 检查
- `LegendaryChecks.cs` 第 11 条（-Only Legendary 共 12 条）：人类、精灵中毒因子 > 0 且会中毒；火巨人异种、测试传奇单位因子 0 且加中毒被拦；亲和表里每个传奇层异种都免疫。
- `ForceDragonChecks.cs`：原来“旧存档力龙没有强胃时会中毒”的前提不再成立，改为“没有强胃也不会中毒（传奇种族）”；迁移后补上强胃的检查不变。

## 构建与测试
- `pwsh -File .\Source\Build.ps1`：通过（主程序集、HAR / VPE / Mythic Bloodlines 桥接）。
- `pwsh -File .\Source\Test.ps1 -Only Legendary`：12 条 PASS，存读档通过。
- `pwsh -File .\Source\Test.ps1 -Only Legendary,ForceDragon`：143 条 PASS，存读档通过。

## 顺带修复：使用说明.txt 损坏
改动前的 `使用说明.txt` 已经损坏：上一个热修（耐瑟娱乐）往更新日志加那一行时，把它插到了全文每一个字符“1”的后面（2458 份，文件从约 13.7 万字涨到 54 万字，标题变成“使用说明（1”）。本次把每个“1 + 换行 + 那一行”还原成“1”，还原结果与 `backup-1.52.0-netheril2` 逐行比较，只差耐瑟热修本来要改的 5 行说明，证明还原完整；然后把耐瑟那行和本次这行正确地加到更新日志最上面，Legendary 自检条数改为 12。损坏版本留在本次备份里。
