# 1.23.2 力龙强胃 / 龙蛋剧本 验证报告

## 改动（已由协调方完成，本次未改代码）
- `Defs/ForceDragon.xml`：LP_ForceDragon 异种型加入原版 `StrongStomach`（强胃，食物中毒概率 0）。
- `Source/ForceDragon.cs`：`ForceDragon.StomachGene`、`Migrate` / `MigrateLoadedGame`；`Gene_ForceDragonHeart.stomachMigrated` 以 `dragonStomachMigrated` 存档。
- `Source/Core.cs`：`Registry.FinalizeInit` 调用 `ForceDragon.MigrateLoadedGame()`，旧存档的力龙加载时补上强胃内生基因。
- `Source/ForceDragonChecks.cs`：新增强胃检查（基因存在且激活、`FoodUtility.GetFoodPoisonChanceFactor(Pawn)` 反射结果为 0、旧龙无强胃时可中毒、加载迁移后获得强胃）。
- `Defs/Scenarios.xml` + `Source/ScenarioChecks.cs`：龙蛋剧本由 3 枚蛋改为 1 枚。
- 备份：`ArtSource/backup-1.23.1`。

## 构建
`pwsh -File .\Build.ps1`：通过（LichPhylactery 针对已安装的 RimWorld 1.7 + Harmony 编译成功；Mythic Bloodlines 桥接编译成功）。无编译错误。

## 测试
`pwsh -File .\Test.ps1`（Core）：一次通过，**2547 项检查全部 PASS**，无随机失败，无需重跑。
`GetFoodPoisonChanceFactor(Pawn)` 反射正常命中（检查通过，因子为 0）。

## 本次修复
无。未修改任何代码。
