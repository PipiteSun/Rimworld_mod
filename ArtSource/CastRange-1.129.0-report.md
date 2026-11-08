# 射程外施放（1.129.0 热修）报告

版本号不变（About.xml 1.129.0）。改前备份：`ArtSource/backup-1.129.0-castrange/`。

## 效果
- 殖民者（玩家阵营）通过选目标界面给本模组的能力选**射程外或没有视线**的目标时不再被拒绝，光标显示“走近后施放”，目标照常高亮（范围法术画出半径）。
- 下令后排上新工作「走近以施放」（`LP_CastAbilityApproach`）：找离自己最近、在射程内、看得见目标、走得到的可站立格走过去；目标是人 / 物且在移动时每 30 刻重新找位置；到位的同一刻换成原版施放工作（`Ability.GetJob`），之后读条、命中、扣费和以前完全一样。
- 资源（法力、斗志、气、神恩、乐韵、契约法力…）、冷却、次数都只在真正施放时扣；走路途中被打断、目标死亡 / 消失 / 不再合法（例如变成友方）时干净结束，不扣任何东西。
- 按住 Shift 下令时照原版排到队尾（`TryTakeOrderedJob`）；排队次数把这条命令算进去（`job.verbToUse`）。
- 已经在射程内、看得见：和以前一模一样，直接原版施放，没有走路这一步。
- AI、敌人、NPC 职业、职业自动施放都不受影响：只有在选目标界面正用这个能力选目标时才放宽（`Find.Targeter.targetingSource`）。

## 不参与（保持原射程）
判定在启动时建好只读名单（`CastApproach.Allowed` / `ExcludedWhy`）：
- 位移，目标就是落点 / 会冲过去：传送术、迷踪步（法师 / 云巨人）、雾遁、奥术冲锋、念动跳跃、暗影步（武僧 / 神恩影遁）、攀云步、次元门、冲锋、狂暴冲撞、蛮牛冲撞、萨特冲撞、角盔冲撞、巴洛古拉跃击、坎比恩滑翔、风跃、翱翔、星界跳跃（含护符）、妖精步、龙族之翼、光辉之魂、援护换位、流浆人渗过。按效果名（`MoveEffects`）和 defName（`MoveDefs`）两层判定，原版 `CompProperties_AbilityTeleport` 也排除。
- 跳跃（`Verb_CastAbilityJump`）、接触（`Verb_CastAbilityTouch`，原版自己会走过去，如吞没）、自带施放工作（如 CastAbilityOnThingMelee）。
- 世界地图目标、射程 0 / 只能对自己、需要再点一次落点（WithDest Selected）的能力。
- 单独调整：在 AbilityDef 里加 `<modExtensions><li Class="LichPhylactery.CastRangeExt"><approach>false</approach></li></modExtensions>`（true = 强制参与）。

## 能力自己的距离检查
有些能力在自己的检查里另写了距离（武器射程类、魔能爆、龙息、水流、神罚、火花（含视线）、投掷战锤）。下命令时这些距离先跳过（`CastApproach.TooFar` / `IgnoreDistance`，线程局部计数），到位后照常再查；若能力自己的距离比原版射程短，走到后自动再靠近（射程 ×0.6，最多 4 次），还是放不了就取消。没改到的其他能力若自带距离检查，射程外目标照旧被拒绝（安全回退）。

## 文件
- 新增 `Source/CastApproach.cs`：名单、`ValidateTarget` / `OrderForceTarget` / `OnGUI` 前缀、`DrawHighlight` 后缀、`Targeter.CurrentTargetUnderMouse` 转译（射程过滤换成 `SourceCanHit` / `VerbCanHit`）、`JobDriver_CastApproach`。
  （类名用 `CastApproach`：`CastRange` 已是 1.64.0 `CastRangeFix.cs` 里的枪械射程修复，未改动。）
- 新增 `Defs/CastRange.xml`：JobDef `LP_CastAbilityApproach`。
- 新增 `Source/CastApproachChecks.cs`：`Test.ps1 -Only CastRange`。
- 小改（距离检查走 `CastApproach.TooFar`）：`ClassAbilities.cs`（武器射程）、`ClassWarlock.cs`（魔能爆）、`ForceDragonPowers.cs`（龙息）、`Races124.cs`（水流、神罚）、`Genasi.cs`（火花距离 + 视线）、`ItemsRaceGear.cs`（投掷战锤）。
- `使用说明.txt`：第一部分“职业能力自动施放”后加一行“射程外施放（1.129.0 热修）”。

## 测试
- `-Only CastRange`：7 条全过（名单与补丁 / 夹具 / 射程外咒术在选目标时接受、不在选目标时拒绝、雾遁射程外仍拒绝 / 下令排上走近工作且未扣契约法力与冷却 / 走到射程内换原版施放后才扣 1 点并进冷却 / 目标被摧毁时干净结束、魔能爆不进冷却 / 射程内直接原版施放）。
- `-Only Classes` 76 条、`-Only Spellbook` 28 条、`-Only AutoCast` 5 条全过。
- 构建通过（主程序集 + HAR / VPE / Mythic 桥）。
