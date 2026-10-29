# 1.52.0 补丁：六臂蛇人副手拾取 / 卸货保留

About.xml 版本未改（仍为 1.52.0）。备份：`ArtSource/backup-1.52.0-marilith/`（改动前的 `Source/Marilith.cs`、`Source/MarilithChecks.cs`、`使用说明.txt`、`1.7/Assemblies/LichPhylactery.dll`）。

## 改动文件

- `Source/Marilith.cs`
  - `Marilith.OffhandWeapon(Thing)`：判断“可作副手的近战武器”（原 `Offhands()` 里的条件抽出来，行为不变）。
  - `Marilith.OffhandUser(Pawn)`：有生效的 `OffhandExtension` 基因（六臂连击 / 双头巨人左右开弓），且没启用双持类模组。
  - `Marilith.CarriedOffhands(Pawn)`、`Marilith.CannotTakeOffhand(Pawn)`：背包里副手武器数量、不能再拿的原因。
  - `Marilith.TakeOffhandOption(Pawn,Thing)` + `FloatMenuOptionProvider_TakeOffhand`（1.7 浮动菜单 API，同 `FloatMenuOptionProvider_OozeEngulf`）：
    右键地上的近战武器 →「拿作副手：{武器}」，原版 `JobDefOf.TakeInventory`（count 1）拿进背包。任何地图都可以，包括家园。
    - 不显示：非玩家阵营、没有副手基因、非近战武器、不在同一地图、被禁止、被别人预留（`CanReserve` 失败）。
    - 灰显：已带副手数 ≥ 上限 →「没有空闲的手」；上限 0 →「缺少能用的额外手臂」（六臂类基因）/「缺少能用的手臂」（双头巨人，两只手不全能用或不是双头身体）；另有「无法到达」「太重」。
  - `MarilithKeepOffhands`：Harmony Postfix 在 `Pawn_InventoryTracker.FirstUnloadableThing`（1.7 中是属性 getter，返回 `ThingCount`）。
    若原版选中的是 `Offhands()` 当前选中的副手，就把这些副手临时从 `innerContainer` 的内部列表里拿掉，再算一次，finally 放回原位置（背包顺序不变，有检查）。
    只剩副手时返回空 → 原版 `InventoryTrackerTick` 里 `unloadEverything = HasAnyUnloadableThing` 会清掉标志，`JobGiver_UnloadYourInventory` 不再派任务，不会死循环。超出上限的武器照常卸。
- `Source/MarilithChecks.cs`：Marilith 组新增 3 条检查（见下）。
- `使用说明.txt`：1.52.0 下加一行更新记录。

## 决定

- 双头巨人确实有副手机制（`OffhandExtension` max 1、needsTwinHeads），所以一并支持，上限沿用 `OffhandSlots()`（蛇人 ≤2，双头巨人 1）。
- 「当前副手数」按背包里的近战武器数算（不只是被选中的那几把），所以背包已满上限时就灰显。
- 原版 `JobGiver_DropUnusedInventory` 只丢食物/药物，不会把拿进背包的武器在家里丢掉，无需额外处理。
- 原版 1.7 的 `FloatMenuOptionProvider_PickUpItem` 没改，只是新增了独立的选项。

## 测试

- `pwsh Source/Build.ps1`：通过。
- `pwsh Source/Test.ps1 -Only Marilith`：PASS，22 条（含新增 3 条）+ 存读档。
  1. 家园地图右键近战武器：蛇人有可用的「拿作副手」（走原版 `FloatMenuMakerMap.GetOptions` 整条管线）；带六臂基因的两臂人类灰显，普通人类不显示。
  2. UnloadEverything：3 把武器只卸下多出的那把（斧头），2 把选中的副手留下，背包顺序不变，之后标志清掉。
  3. 手满时灰显「没有空闲的手」；四条额外手臂都没了灰显「缺少能用的额外手臂」。
- `pwsh Source/Test.ps1 -Only GiantKinHAR`：PASS，14 条 + 存读档（共用副手代码，顺带跑了）。
- 没跑全套。
