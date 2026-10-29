# 1.52.0 热修：巨灵在神灯里保持最佳状态

需求（Peter）：巨灵倒下回到神灯后玩家接触不到他，灯里的状态应一直维持在最佳，再擦出来是满状态。

## 原因
- 巨灵被杀时 `GenieDeath`（Pawn.Kill 前缀）→ `Genie.CatchDeath` 只调用 `Heal`（清伤口、失血、会致病/可免疫/致死的疾病，补缺失部位），然后 `ReturnToLamp` 把 pawn 放进 `WonderLamp.inner`（`ThingOwner<Pawn>`，Deep 存档，不是 WorldPawn）。
- 神灯是 ResourceBase 物品，不 tick，灯里的 pawn 完全冻结：饥饿、休息、娱乐等需求，负面记忆，精神状态，慢性病（如背伤）原样保留，擦出来还是倒下时的样子。

## 改动
- `Source/Genie.cs`
  - 新增 `Genie.Refresh(Pawn)`：`Heal` 后再删除所有 `isBad` 的 hediff（保留 `LP_` 开头的状态如许愿倦怠、保留成瘾、缺失部位已由 Heal 补回）；删除心情为负的记忆；所有需求 `CurLevel=MaxLevel`；`mentalStateHandler.Reset()`。
  - `ReturnToLamp`：放进灯后调用 `Refresh`。
  - `WonderLamp.Rub`：从灯里取出后、生成前调用 `Refresh`，生成后 `health.CheckForStateChange` 刷新倒地状态。旧存档里已经在灯里的巨灵擦出时直接生效。
  - 灯不 tick，所以不需要定时器：回灯时满一次（灯里检视看到的就是满状态），擦出前再满一次（兜底）。
- `Source/Races124Checks.cs`（`CheckGenie124` 死亡→回灯段）：杀前加背伤、把饥饿/休息/娱乐压到 0.05、加“没有桌子吃饭”负面记忆；新增 2 条检查：
  - a genie back in its lamp is kept at its best: needs full, no illness, no bad memories
  - a genie rubbed out of its lamp comes back at its best（中间再把需求压低一次，验证擦出前的兜底）

## 验证
- `Source\Build.ps1`：主模组与 HAR / VPE / Mythic 桥全部 PASS。
- `Source\Test.ps1 -Only Races124`：PASS，471 条（含存读档）。

## 其它
- 备份：`ArtSource/backup-1.52.0-genielamp`（基准为 backup-1.52.0-settings 之后的状态）。
- `使用说明.txt` 更新日志顶部加一行（精确锚点插入，对比备份仅新增 1 行）。About 版本保持 1.52.0。
- 未改：只被击倒（未被杀）的巨灵仍留在原地，不会自动回灯。
