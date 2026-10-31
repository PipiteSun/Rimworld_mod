# 1.74.0 热修：「传奇」社交分页不停刷新

版本号不变（仍是 1.74.0）。修改前备份：`ArtSource/backup-1.74.0`。

## 现象
「传奇」（灰色·"传奇"·比亚萨达诺瓦，Human1629）的社交分页一直在刷新、闪动。

## 排查
1. **Player.log（01:22 读档 `传奇.rws`）**
   - 9 次 `Could not find class LichPhylactery.VPEBridge.Ability_MagicMissile`（还有 Ability_SecondWind、Ability_Heal、Ability_AnimalFriendship），接着 `Can't load abstract class VEF.Abilities.Ability`，然后 `SaveableFromNode exception`（在 VEF `CompAbilities.PostExposeData` 里）。
   - 结果是 9 个人读坏了：8 个世界上的人，加上地图上的囚犯 **Rilrae（收藏家，Human14541）**。他们没读到 PawnKindDef（为空），所以凡是取他们标签的地方都会报 NullReferenceException。
   - 每帧报一次 `Exception drawing ThingOverlay for SleepingSpot108963`（囚犯的睡眠点画主人名字时出错），一共 9921 次。
   - `Exception ticking 如龙` / `Exception ticking 魅力`（位置在「传奇」旁边）：殖民者聊天时会调 `MemoryThoughtHandler.TryGainMemory → Thought_Memory.GroupsWith → LabelCap`，碰到关于那个坏掉囚犯的记忆就报错。互动做到一半就中断了，冷却也没记上，所以下一个间隔马上又来一次。周围人（包括「传奇」）的社交记忆和好感一直在变，社交分页（每 20 帧重算一次）的数字和排序就一直跳。
2. **本模组的社交相关代码**（SocialCardUtility、OpinionOf、PlayLog、社交 ThoughtWorker、TryGainMemory 前缀等）都查过了，没有每帧改数据或者用随机数的。`ThoughtWorker_LegendRenown`（外人对传奇的好感 +10）是稳定值，但它会让外派系的囚犯出现在「传奇」的社交列表里，所以那个坏掉的囚犯正好在列表中。
3. **根本原因**：1.73.0 把旧 VPE 职业能力的类（共 57 个 `Ability_*`）从桥程序集删掉了。旧 hediff 之类的类名留了空壳，能力类却没有留。VEF 按类名读能力，找不到时会退回抽象类，整个人就读坏了。
   这不是其他模组的问题。VREAndroids、VEF 等出现在报错堆栈里，只是因为它们给同一个方法打了补丁。

## 修复（`Bridges/VPE/Source/Martial.cs`）
- 新增 `LegacyAbilityClassNames`：Harmony 前缀挂在 `BackCompatibility.GetBackCompatibleType` 上。基类能接受 `Ability_ClassLegacy`，并且类名是已经不存在的 `LichPhylactery.VPEBridge.Ability_*` 时，就读成 `Ability_ClassLegacy`（没有按钮、没有效果）。其他类名不受影响。
- `GameComponent_ClassMigration.Version` 从 2 改成 3：已经迁移过的存档再跑一遍迁移，把读回来的旧能力换成冒险者职业等级后删掉。对已经迁移过的人不做任何事。
- 只影响装了 VPE 的情况。没装 VPE 时桥程序集不加载，旧能力本来也读不出来，和以前一样。

## 测试
- `Build.ps1`：主程序集、HAR、VPE、神兽血裔桥全部通过。
- `Test.ps1 -VPE -Only Classes,Martial`：**通过 13 项**（原来 12 项，加 1 项：Ability_MagicMissile / Ability_SecondWind 读成 Ability_ClassLegacy，现有类名和其他基类不受影响；迁移版本号 3）。

## 存档情况
- `传奇.rws`（01:25 保存）是在读坏的状态下存的：那 9 个人已经从存档里消失，只剩空引用。原版会把空引用当作不存在，不会再报错，社交分页应该恢复正常。
- `Autosave-1`～`Autosave-5`（10/29 22:23～23:27）里这些人还在。装上这次热修以后读它们，可以完整读回来（包括囚犯 Rilrae）。

## 请 Peter 实测
1. 读 `传奇.rws`，打开「传奇」的社交分页，确认不再闪动。Player.log 里不应再出现 SleepingSpot / Exception ticking 的报错。
2. （可选）读 `Autosave-4.rws`：日志里不应再有 `Could not find class LichPhylactery.VPEBridge.Ability_*`，囚犯 Rilrae 正常，社交分页稳定，学过旧法术的人在冒险者分页里有对应的职业等级。
