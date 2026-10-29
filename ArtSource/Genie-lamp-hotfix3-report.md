# 1.59.0 热修：巨灵野性回归后反复回灯

问题（Peter）：元素战士巨灵精神崩溃，变成"野性回归"后回到了神灯；花 1000 银唤醒后，他又立刻回到神灯。

## 原因
- 原版 `MentalBreakWorker_RunWild.TryStart` 会调用 `ChangeKind(WildMan)` 和 `SetFaction(null)`，崩溃的人直接变成没有派系的野人。
- `Gene_GenieCore.Check` 每 250 刻检查一次：只要 `Faction!=OfPlayer` 就当作巨灵被别的派系带走，调用 `ReturnToLamp`。
- 唤醒（`Revive` → `Rub`）时只恢复了状态，派系还是空的，所以下一次 Check 又把他送回灯里。

## 改动（Source/Genie.cs）
- `GenieNoRunWild`：给 `MentalBreakWorker_RunWild.BreakCanOccur` 加后缀，巨灵返回 false，原版会改选别的崩溃。
- `Genie.Reclaim(p)`：囚犯先清掉囚犯身份；如果种类是 WildMan，改回 Colonist；然后 `SetFaction(OfPlayer)`，并清掉精神状态。
- `Gene_GenieCore.Check`：在地图上、没有派系、又不是囚犯的巨灵（也就是已经变野的）直接 `Reclaim`，不再回灯。被别的派系俘虏或带走时照旧回灯。
- `WonderLamp.Rub`（擦出和唤醒都会走这里）：生成后调用 `Reclaim`。旧存档里已经变野、回了灯的巨灵，出来就是殖民者，不会马上又回灯。

## 检查（Races124Checks.CheckGenie124）
新增 3 条：巨灵不能野性回归；变野的巨灵留在外面，恢复成殖民者；变野、回了灯的巨灵花 1000 银唤醒后是殖民者，连续 Check 两次也不会回灯。

## 验证
- `Source\Build.ps1`：全部 PASS。
- `Source\Test.ps1 -Only Races124`：PASS，479 条（含存读档）。

## 其它
- 备份：`ArtSource/backup-1.59.0-runwild`。
- `使用说明.txt`：巨灵"灯之束缚"段补一句，更新日志顶部加一行，和备份对比只多了这两处。
- About 保持 1.59.0。
