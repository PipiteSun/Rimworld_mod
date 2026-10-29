# 1.22.1 肉制婴儿食品 — 检查与测试报告

## 改动
- 备份：`Source/ItemChecks.cs` → `ArtSource/backup-1.22.0/Source/ItemChecks.cs`（哈希一致；`Source` 子目录是新建的）。
- `Source/ItemChecks.cs`：新增 `CheckBabyFoodMeat()`（E 组），挂在 `CheckItems()` 末尾，所以 Core 的 `-lichtest` 和 `-Isekai` 的 `-lichitemsonly` 都会跑到。
- XML 和说明文档没有改动（测试没有发现问题）。

## 新增检查（14 条）
对 `LP_Make_BabyFoodMeat`、`LP_Make_BabyFoodMeatBulk` 每张配方各 6 条：
1. 配方存在
2. `ElectricStove.AllRecipes` 里有这张配方
3. `FueledStove.AllRecipes` 里有这张配方
4. 产物只有一种，是 `BabyFood`，数量分别是 10 / 40
5. fixedIngredientFilter 允许 `Meat_Cow` 和 `RawPotatoes`，不允许 `EggChickenFertilized`
6. defaultIngredientFilter 允许 `Meat_Cow`，不允许 `Meat_Human` 和 `RawPotatoes`

另外 2 条：
- 原版 `Make_BabyFood` 仍然不允许 `Meat_Cow`（原版配方没被改动）
- 实际做一次：在电炉灶上用 `GenRecipe.MakeRecipeProducts` 和 `Meat_Cow` 做出婴儿食品，它的 `CompIngredients` 里有 `Meat_Cow`，所以不再算素食

## 测试结果
- `pwsh -File .\Build.ps1`：PASS（LichPhylactery 主程序集和 Mythic Bloodlines 桥接程序集都编译通过）。
- `pwsh -File .\Test.ps1`（Core）：**PASS: 2497 checks**，日志里没有错误。第一次就通过，没有重跑，也没有出现不稳定的检查。

## 问题
无。
