# 热修：圣坛献祭不加神眷（1.59.0，版本号不变）

## 现象
Peter 报：在圣坛献上祭品，神眷没有变化。三种献祭（LP_Offer_Silver / LP_Offer_Shard / LP_Offer_Liked）都受影响：祭品被吃掉，但没有神眷、没有飘字、神眷历史里没有记录。

## 原因
- 原版 `Toils_Recipe.FinishRecipeAndStartStoringProduct` 的顺序：`ConsumeIngredients`（每个原料 `RecipeWorker.ConsumeIngredient` → `Destroy()`）→ `bill.Notify_IterationCompleted` → `RecipeWorker.Notify_IterationCompleted`。（IL 核对 `E:\SteamLibrary\steamapps\common\RimWorld\Assembly-CSharp.dll`）
- 原版 `Thing.Destroy()` 会执行 `stackCount = 0`。
- `RecipeWorker_Offering.Notify_IterationCompleted` 用 `Offerings.FavorFor(recipe, ingredients)` 算神眷，三种算法都乘 `stackCount`，结果恒为 0，`f > 0` 不成立，什么都不做（也不调用 `IdeoBridge.OfferingDone()`）。
- 旧注释写的是“原料在完成那一刻还在”，这个前提是错的。旧自测（FaithChecks.cs）直接拿一块没销毁的残片调 `Notify_IterationCompleted`，绕过了销毁步骤，所以一直通过。

## 存档佐证（龙宫.rws，2026-10-28 02:44，只读）
- 守护神：凯兰沃；凯兰沃神眷 0（字典里没有记录），密斯特拉 −29.5。
- 神眷历史只有“殖民地改奉凯兰沃”和一次“每天回落”，没有献祭记录。
- “献上白银”工作单的 repeatCount 已到 0（至少完成过一次），神眷没有变化；`lpIdeoLastOffering` 停在 1371234（当前 tick 3846318）。
- Player.log / Player-prev.log 里没有和信仰相关的报错。

## 修改
- `Source/Faith.cs`
  - `Offerings.FavorFor(RecipeDef, Thing)`：按单件原料算神眷（残片 10×片数；白银 市值×数量÷100；喜爱的祭品 再×3）。保留列表重载 `FavorFor(RecipeDef, List<Thing>)`，内部累加。
  - `RecipeWorker_Offering`：新增 `ConsumeIngredient` 重写，在 `base`（销毁）之前把神眷累加到 `pending`；`Notify_IterationCompleted` 用 `pending` 结算后清零。倍率（意识形态 ×1.5、神眷获取倍率）和飘字、历史、`OfferingDone` 的逻辑不变。
- `Source/FaithChecks.cs`
  - 新辅助函数 `FOffer`：按原版顺序跑一次献祭（`ConsumeIngredient` 销毁 → `Notify_IterationCompleted`）。
  - 原来的“残片 +10”改用 `FOffer`；新增一条检查：白银 100 → +1、60 个土豆（拉森德喜爱）→ 市值÷100×3、历史里有 3 条“献上”、意识形态的献祭时刻被刷新。
- `使用说明.txt`：更新日志加一行“1.59.0 热修：圣坛献祭不加神眷……”（LF 编码，行数 1854 → 1855）。
- 不做补偿：以前被吃掉的祭品不补神眷。

## 验证
- `.\Source\Build.ps1`：主模组、HAR、VPE、神兽血裔联动全部 PASS。
- `.\Source\Test.ps1 -Only Faith`：**PASS: 18 checks (only Faith); save/reload.**（原来 17 条 + 新增 1 条）

## 备份
`ArtSource/backup-1.59.0-offering/`：`Source/Faith.cs`、`Source/FaithChecks.cs`、`使用说明.txt`（都是修改前的版本）。
