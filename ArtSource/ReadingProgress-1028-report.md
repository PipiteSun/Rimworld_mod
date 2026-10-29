# 阅读进度（2026-10-28，版本号不变 1.61.0）

## 覆盖的物品

| 物品 | 原来的进度存储 | 显示 |
|---|---|---|
| 6 本属性典籍 LP_Tome_STR/DEX/CON/INT/WIS/CHA | BookOutcome_Tome.read（按人，刻数，存档） | 阅读进度：x / 30 小时（%）；该属性已从典籍得到过 → 已获得奖励 |
| 耐瑟研习卷 LP_NetherilStudyScroll | NetherilQuestState.studyPoints（按人，存档）；VPE 时进 VPE 经验 | 研习度 x / 下一档（下一级心灵链接，%）；6 级或过了 1600 → 心灵链接奖励已全部获得；装 VPE 时“灵能经验持续累积（没有终点）”，不画进度条 |
| 秘术残卷 LP_NetherilArcaneScroll | BookOutcome_NetherilArcane.progress / learned（按人按卷，存档） | 残卷进度 x / 24（%），已领悟 n / 2 个；学满 → 已获得奖励；卡住时注明灵能等级不够 |
| 卡苏斯之卷 LP_KarsusScroll | NetherilQuestState.karsusTicks / karsusPasses（按人，存档） | 第 n 遍：x / 40 小时（%），已读完 n 遍；满 10 遍 → 回响已满 |
| 恶魔学典籍 LP_Demonomicon | Book_Demonomicon.readTicks（按书，存档） | 阅读进度：x / 4 小时（%）；读满 → 已读完；殖民地已知该恶魔 → 已获得奖励 |
| 契约书 LP_PactScroll | **原来没有**：讲台上 4 小时的 Wait toil，中断就从头来 | 新增按人按卷累计（GameComponent_ReadingProgress.pact，存档 lpPactRead）；阅读进度：x / 4 小时（%） |

奖励数值、要求都没改。

## 显示位置

- 物品检视面板（CompReadingProgress.CompInspectStringExtra，启动时挂到上面 11 个 ThingDef，不改 XML）：选中的人 + 本地图殖民者 + 持有者中有进度的，正在读的、未完成、进度高的排前，最多 3 人，多出的写“另有 N 人有进度”；没人读过写“还没有人读过”。按书缓存，120 刻或 2 秒实时刷新。
- 信息卡：基础分类“阅读进度”一项（值是第一名，说明里是完整列表）。
- 读者工作描述（JobDriver.GetReport 后缀）：“（进度 62%）”/“（已获得奖励）”，按人缓存 60 刻。
- 读者检视面板多一行“阅读 X：完整进度”（秘术残卷、卡苏斯之卷已有耐瑟“研读中”行，不重复）。
- 读者头顶进度条：Book.OnBookReadTick 后缀里维持原版 ProgressBar 特效，阅读 job Cleanup 时清掉；只给玩家派系、有终点且未完成的进度画。契约书本来就有讲台进度条（现在从已读进度开始）。

## 改动文件

- 新增 `Source/ReadingProgress.cs`：ReadingProgress（Kind/Get/ItemLines/缓存/进度条）、CompReadingProgress、GameComponent_ReadingProgress、PactRead、补丁 ReadingProgressPactToils（JobDriver_ReadPact.MakeNewToils 透传后缀：读 toil 开始时扣掉已读，结束时记住或清零）、ReadingProgressReport、ReadingProgressPawnInspect、ReadingProgressBarTick、ReadingProgressCleanup、ReadingProgressStartup。
- 新增 `Source/ReadingProgressChecks.cs`：-Only ReadingProgress。
- `使用说明.txt`：更新日志最上面加一条“1.61.0 热修（阅读进度）”（只加这一行，diff 已核对）。
- 备份：`ArtSource/backup-reading-progress-1028/使用说明.txt`（其余都是新文件）。
- 没碰 About.xml、Checks.cs、Core 设置、任何 Defs。

## 测试

- `Build.ps1`：PASS（主程序集 + HAR / VPE / Mythic 桥接）。
- `Test.ps1 -Only ReadingProgress`：PASS 10 条（含存读档）。
- `Test.ps1 -Only Netheril`（没改耐瑟代码，只做回归）：跑 3 次——一次到第 31 条失败（新生成的测试读者被 NetherilIdle.Blocked 挡住，三次选书都为空，同一跑第 30 条走同一路径通过，判断是随机测试小人状态），两次停在已知的老问题第 22 条（arcane demigod）。和本次改动无关。

## 没覆盖

- 原版书（小说、技能书、蓝图等）不归本模组管，不显示。
- 一次性使用的法术卷轴（CompUsable，用了立即生效）不是“阅读进度”类，不涉及。


## 续：阅读速度受研究速度影响（2026-10-28 下午）

Peter：阅读速度要和研究速度相关。

### 规则

- 11 种读物（6 本属性典籍、耐瑟研习卷、秘术残卷、卡苏斯之卷、恶魔学典籍、契约书）的**奖励进度**按读者的研究速度累计：`ReadSpeed.Factor = Clamp(研究速度, 0.25, 3)`。所需小时数不变，只改累计快慢。
- 研究速度按人缓存 250 刻（`ReadSpeed`，在 ReadingProgress.cs），不在每刻算属性。
- 各处怎么乘：
  - 典籍（Growth.cs `BookOutcome_Tome.OnReadingTick`）：刻数 × 洞彻心智 × 阅读速度，零头按人留到下次（原来 `RoundToInt` 每刻 1 刻时会把 ×1.3 吃掉，顺手修了）。
  - 恶魔学典籍（AbyssRitual.cs `BookOutcome_Demonomicon.OnReadingTick`）：刻数 × 阅读速度（进度属于书，速度看当前读者）。深渊腐化按书的小时数结算，读得快腐化来得也快（每读完一本的总腐化不变）。
  - 耐瑟三卷（NetherilScrolls.cs `BookOutcome_NetherilBase`）：真正的阅读 tick 记下读者速度；研习卷经验（研习度 / 装 VPE 时灵能经验）、秘术残卷进度和余效经验、卡苏斯遍数刻数乘速度。心灵熵、读错判定、耐瑟启迪的连续 1 小时、冒险者经验仍按实际时间。`ReadTicks`（检查 / VPE 桥接）按 ×1，所以耐瑟老检查不受影响。
  - 契约书（ReadingProgress.cs 读 toil 包装）：`tickIntervalAction` 里每段多扣（或少扣）(速度 − 1) × dt 刻的剩余时间，至少留 1 刻让原版倒计时收尾。
- 界面：每人进度文字后面加「，阅读速度 ×1.35（研究速度），约还需 11.1 小时」（物品检视面板、信息卡、读者检视行都用同一段文字；恶魔学典籍没有读者时写「读得多快看读者的研究速度」）；信息卡说明和各书的说明文字加一句「阅读速度 = 读者的研究速度（最低 ×0.25，最高 ×3）」。

### 改动文件

- `Source/ReadingProgress.cs`：新增 `ReadSpeed`（Factor / Raw / Scale / Forget / Note），进度文字加速度和预计剩余时间，契约书读 toil 加 `PactRead.Tick`。
- `Source/Growth.cs`、`Source/AbyssRitual.cs`、`Source/NetherilScrolls.cs`：上面的乘速度和说明文字。
- `Source/ReadingProgressChecks.cs`：新增 3 条（9–11）。
- `使用说明.txt`：「1.61.0 热修（阅读进度）」这一行补上阅读速度说明、检查数 10 → 13（diff 只有这一行）。
- 备份（这次新碰的文件）：`ArtSource/backup-reading-progress-1028/` 下的 NetherilScrolls.cs、Growth.cs、AbyssRitual.cs、ReadingProgress.cs、ReadingProgressChecks.cs；使用说明.txt 的早先备份没覆盖。

### 测试

- 编译前确认没有 RimWorld 进程、没有别的 Build.ps1 / Test.ps1 在跑。
- `Build.ps1`：PASS（主程序集 + HAR / VPE / Mythic 桥接）。
- `Test.ps1 -Only ReadingProgress`：PASS 13 条（含存读档）。新检查实测：智识 20 → 研究速度 2.19、阅读速度 ×2.19；读 600 刻：卡苏斯 1313 vs 150 刻、契约书 1313 vs 150 刻（×2.19 vs ×0.25）；智识 0 → 研究速度 0.1，按 ×0.25 算。
- 没跑 -Only Netheril 等其他组（按要求只跑 ReadingProgress）。耐瑟老检查走 `ReadTicks`（×1），设计上不受影响。

## 续 2：阅读速度去掉 ×3 上限（2026-10-28）

Peter：「阅读速度随着研究速度提升没有上限」。

- `ReadSpeed`：删掉 `Max`，`Raw` = max(研究速度, 0.25)；研究速度是 NaN / 无穷时按 ×1。`Note` 改成「阅读速度 = 读者的研究速度（最低 ×0.25，无上限）。」——信息卡和各书说明（典籍、研习卷、残卷、卡苏斯之卷、恶魔学典籍）都用这一句。
- 大倍数下的安全性（逐个看过，未改）：典籍 / 恶魔学典籍按“越过门槛”结算，卡苏斯按整遍 while 循环，残卷进度 `Mathf.Min` 封顶 24，契约书 toil 倒计时 `Clamp(…,1,ReadTicks)` 不会变负，`PactRead.Set` 超过 4 小时就清掉，界面百分比 `Clamp01`。零头仍按人留到下次。
- `使用说明.txt`：同一行「（最低 ×0.25，最高 ×3）」→「（最低 ×0.25，无上限）」，检查数 13 → 14。
- 检查：9 改成 max 而不是 clamp；新增 12：智识 20 + 研究速度 +6 的测试状态 → 研究速度 13.99、阅读速度 ×13.99；卡苏斯读 600 刻 → 8396 刻；契约书 toil 剩 200 刻再读 600 刻 → 停在 1；进度文字 50% 并写出 ×13.99。
- `Build.ps1` PASS；`Test.ps1 -Only ReadingProgress` PASS 14 条（第一次跑因原版开局生成殖民者时的离婚故事 TicksAbs 日志错误被判失败，与阅读无关，重跑干净通过）。
