# 1.52.0 补丁：耐瑟卷轴娱乐修复 + 娱乐优先 + 取消每日上限

About 版本保持 1.52.0。备份：`ArtSource/backup-1.52.0-netheril2/`（NetherilScrolls.cs、NetherilQuest.cs、NetherilChecks.cs、使用说明.txt、LichPhylactery.dll，均为改动前）。

## 1. 没心灵链接的人原地打转

**原因（反射查 IL 确认）**：原版 `JoyGiver_Read.TryGiveJob` → `BookUtility.TryGetRandomBookToRead`，两个候选筛选 lambda 都只调 `BookUtility.IsValidBook`（私有；查禁止、阅读策略、预订可达、迷雾、政治、真空），**不调 `CanReadBook`**。我们的心灵链接/智识限制挂在 `CanReadBook` 上，所以没心灵链接的人会被原版挑中耐瑟卷轴，job 开不了，下一次思考又挑同一卷 → 原地打转。我们自己的冥想 JoyGiver、自动研读、右键菜单本来就走 `CanReadBook`，没有这个问题。

**修法**：
- 新增 `Netheril.CanRead(Pawn, Thing)`（不是耐瑟卷轴一律 true；否则 = `CannotRead(p)==null`）。
- `NetherilValidBook`：`BookUtility.IsValidBook` 的 postfix，读不了的耐瑟卷轴不算候选。原版在候选筛选这一步就去掉它，会挑别的书或别的娱乐，job 不会开始后再失败，也就不会每次思考刷失败。

## 2. 有心灵链接时娱乐优先读耐瑟卷轴

`NetherilJoyFirst`：`JobGiver_GetJoy.TryGiveJob` 的 prefix，只作用于 `JobGiver_GetJoy` 本身（聚会、卧床等子类照旧），且这个 JobGiver 允许 `LP_NetherilMeditativeReading`（`JoyGiverAllowed`）。条件全满足才先给卷轴：
- 有娱乐需求且娱乐没满；不是卧床养伤；
- `Netheril.CannotRead(p)==null`（心灵链接、智识 ≥6）；
- 心灵熵 < 上限 90%（`Netheril.StopAt`）；
- `NetherilIdle.PickBook(p)` 找到对他有用、能到、没禁止、可预订的卷（卡苏斯 → 有用的残卷 → 研习卷，同类挑最近）。

满足就用我们的冥想 JoyGiver 出 job；否则 return true，完全走原版随机。

## 3. 取消每日经验上限

- 删掉 `Netheril.DailyCap`、`NetherilQuestState.AllowXP/GivenToday/NotifyCapped` 和 `xpToday/xpDay/cappedToday` 字段；`Netheril.GiveXP` 直接给全额（VPE 灵能经验或研习度）。
- 旧存档里的 `lpXpToday/lpXpDay/lpCappedToday` 不再 Scribe，读档时忽略（RimWorld 不读的节点不报错）。
- 研习卷检视面板“今日已达上限”、信息卡“每人每天最多 40 点”去掉；空闲研读选卷里研习卷始终 rank 2。
- 冒险者经验（`Adventurer.GainXP`）本来就没有每日上限，不用改。VPE 桥接里“技能经验 → 职业灵能经验每天 50”是另一套系统，没动。

## 测试（`.\Test.ps1 -Only Netheril`）

- #6 改为：读研习卷 8 小时得 48 经验（超过旧上限 40），之后再 `GiveXP(5)` 照样全额。
- #31 最后一段改为：一天已得 50 经验后研习卷仍被选。
- #34 新增：没心灵链接、娱乐低，地图上只放研习卷 + 一本小说 → 原版读书 JoyGiver 30 次里 0 次选耐瑟卷轴（且有给 job）；`IsValidBook` 卷轴/小说/有链接者 = false/true/true；冥想 JoyGiver 和自动研读都不给 job。
- #35 新增：有心灵链接、娱乐低，地图上有研习卷 + 小说 → `JobGiver_GetJoy` 10/10 次选耐瑟卷轴；心灵熵 95% 时 `NetherilJoyFirst.Try` 返回 null（走原版）。

结果：老 #22 照旧失败并中断整组（与本次无关）。临时把 #22 包成 `if(false)` 后跑：**PASS: 34 checks (only Netheril); save/reload**（#22 不计数），日志无红字；随后已还原 #22 并重新编译最终 DLL。

## 改动文件

- `Source/NetherilScrolls.cs`：`CanRead`、`NetherilValidBook`、`NetherilJoyFirst`、`GiveXP` 去上限、文案、`Rank`。
- `Source/NetherilQuest.cs`：去掉每日计数字段与方法、存读档。
- `Source/NetherilChecks.cs`：#6、#31 改写，#34、#35 新增。
- `使用说明.txt`：娱乐类型、空闲研读、研习卷、残卷余效、自检条数；更新日志加一行。
- `1.7/Assemblies/LichPhylactery.dll`：重新编译。
