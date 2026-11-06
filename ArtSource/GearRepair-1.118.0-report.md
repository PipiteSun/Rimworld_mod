# 1.118.0 附魔台修复耐久（2026-11-04）

需求（Peter 经协调）：附魔台加上和 Isekai 精炼炉一样的“修复装备耐久”，不依赖 Isekai。

## 参照：Isekai 的做法
`ISEKAI RPG LEVELING/Source/IsekaiLeveling/Forge/ForgeUtility.cs`：`GetRepairEssenceCost` 每缺 20% 耐久 1 个魔力精华（向上取整、最少 1），`RepairItem` 从地图扣精华后当场修满；精炼炉窗口只在装备掉耐久时显示“修复”按钮。没有研究、工匠、失败。

## 本模组的实现
- 材料：白银（魔力精华是 Isekai 的物品，不能用）。每缺 20% 耐久 10 白银，向上取整，最多 50。
- `Source/GearEnhanceWindow.cs`
  - `GearEnhanceBench.RepairSilverPer / NeedsRepair / RepairCost / CanRepair / Repair / RepairAll`；`Consume` 拆出按 ThingDef 扣料的重载。
  - 窗口标题“附魔强化 · 修复”，高 520→580；右栏底部固定 112px 的“修复耐久”区块（满耐久时写“耐久是满的”）；左栏底部“全部修复：N 件，X 白银”（按列表顺序修到白银用完）；列表里掉耐久的装备副标题橙色显示百分比；中栏耐久行掉耐久时变橙。
  - 附魔台按钮不再因没研究“装备强化”而禁用（修复不需要研究；强化那边 `Blocked` 照旧提示先研究）。
- 自检 `GearEnhanceChecks.cs` 新增 1 条：费用 1 点缺失 10 / 剩 1 点 50 / 一半 30；白银不够不修；修好扣的白银正好等于费用；满耐久再修返回 false；全部修复；附魔台按钮未禁用。

## 文件
- 改：Source/GearEnhanceWindow.cs、Source/GearEnhanceChecks.cs、About/About.xml（1.118.0）、使用说明.txt（快速上手、【1.85.0 附魔强化】、更新日志）
- 备份：ArtSource/backup-1.117.0-gearrepair

## 测试
`Test.ps1 -Only GearEnhance`：PASS 17 checks; save/reload.
