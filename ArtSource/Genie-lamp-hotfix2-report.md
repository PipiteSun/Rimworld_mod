# 1.58.3 热修：神灯巨灵（击倒回灯、灯内隔绝、1000 银唤醒）

需求（Peter）：巨灵倒下后回到灯里，可以花 1000 银快速复活；在灯里不受周围环境影响。原先神灯放在外面时，灯里的巨灵会冻伤、营养不良，最后死在灯里，15 天暗淡也随之重新开始。

## 原因
- 灯里的巨灵（`WonderLamp.inner`）还会被游戏 tick（需求、体温、疾病照常结算）。1.52.0 热修只在回灯和擦出时恢复一次，挡不住中间这段时间的冻伤和饥饿。
- 灯里的巨灵"死亡"时，`Pawn.Kill` 前缀 → `CatchDeath` 又把他 `ReturnToLamp` 一遍，`dimUntil` 被重设为 15 天以后，所以暗淡重新开始。
- 只有被杀才回灯，只被击倒的巨灵会留在原地。

## 改动（Source/Genie.cs）
- `GenieLampFreeze`：给 `Pawn.Tick` / `Pawn.TickInterval` / `Pawn.TickRare` 加前缀，未生成并且 `ParentHolder is WonderLamp` 的 pawn 不运行（先判断 `Spawned`，开销可以忽略）。灯里不冻伤、不挨饿、不生病。
- `CatchDeath`：已经在灯里的巨灵直接 `Refresh` 后取消死亡，不再重新回灯、不重设暗淡。`ReturnToLamp`：已经在这盏灯里时直接返回。
- `GenieDowned`：在 `Pawn_HealthTracker.MakeDowned` 加后缀 → `Genie.CatchDowned`。灯在手里时延到下一刻回灯（和被杀一样暗淡 15 天）；灯不在手里时照常倒地。
- 唤醒：`Genie.ReviveSilver=1000`，`SilverOn`/`TakeSilver` 用这张地图上没被禁止的白银。`WonderLamp.GetGizmos` 在暗淡时显示"花1000银唤醒"按钮，白银不够或灯不在地图上时灰显；`WonderLamp.Revive()` 扣银、结束暗淡，再调用 `Rub` 把巨灵放到灯旁边（`Rub` 里有 `Refresh`，出来是满状态）。
- 召唤信件和 `Defs/Items_Races124.xml` 里的神灯描述都补充了这些规则。

## 检查（Source/Races124Checks.cs，CheckGenie124）
新增 5 条：击倒回灯；灯里不 tick（反射调用 TickInterval ×10，饥饿不变、没有坏状态）；灯里被杀不会死、暗淡不重设；没有可用白银时按钮灰显、无法唤醒；花 1000 银立刻召回、扣款正确、满状态。
另外修正了一条过期检查：1.58.3 给许愿对话加了"让我更强"选项，选项数从 7 变成 8，旧检查一直按 7 算，会失败。

## 验证
- `Source\Build.ps1`：全部 PASS。
- `Source\Test.ps1 -Only Races124`：PASS，476 条（含存读档）。

## 其它
- 备份：`ArtSource/backup-1.58.3`（1.7、About、Source、使用说明.txt，外加 Defs/Items_Races124.xml）。
- `使用说明.txt`：巨灵"灯之束缚"段补一句，更新日志顶部加一行，和备份对比只多了这两处。
- About 版本保持 1.58.3：这是热修，1.58.4 留给性能审计。
