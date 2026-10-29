# 耐瑟卷轴：连续累积 + 空闲自动研读 + 阅读/冥想娱乐（1.52.0 补丁）报告

About.xml 仍是 1.52.0。需求：ArtSource/Netheril-1.52.0-idle-spec.md（另加 Peter 22:39 的"娱乐类型加入阅读和冥想"）。备份：ArtSource/backup-1.52.0-netheril/（NetherilScrolls.cs、NetherilQuest.cs、NetherilChecks.cs、Netheril_Scrolls.xml、NetherilVPE.cs、使用说明.txt、LichPhylactery.dll）。

## 测试
`.\Test.ps1 -Only Netheril`：新加的 29–33 全部通过，然后照旧停在已知的老问题 #22（奥术半神熵上限 ×2.4，期望 ×2），后面的检查就没跑到。
为了验证 #22 后面的检查，我临时把 #22 改成不判定跑了一遍：**PASS: 32 checks (only Netheril); save/reload**（33 条减去跳过的 #22）。跑完已经改回去，DLL 也重新编过（代码里 #22 仍是原样）。
没跑 `-VPE`（任务说明只要求 -Only Netheril）；VPE 桥接用的 `ReadHour` 还在（现在等于按刻连续读 2500 刻），桥接编译通过。

## 改动
- Source/NetherilScrolls.cs
  - `BookOutcome_NetherilBase`：原版每次阅读 tick 用游戏刻差值累计（间隔 ≤300 刻算连续），满 60 刻 `Settlement` 一次：`Accrue(读者, 刻数)`（每小时量 × 刻数 ÷ 2500）→ 计入任务线阅读时长 → 读者阅读钟跨过整小时就判定读错 → 检查自动研读 2 小时上限。`ReadTicks(p,n)` / `ReadHour(p)` 给检查和 VPE 桥接用。`EndSession` 先结算不足 60 刻的零头。
  - 三种卷轴的 `ReadHour` 换成 `Accrue` + `HourEntropy` + `InspectLine`；残卷新增 `LearnedOut`、`Stuck`。
  - `NetherilIdle`（条件、选卷、2 小时上限、停读请求）、`JobGiver_NetherilIdleStudy`、`NetherilIdleJoyTick`（JoyUtility.JoyTickCheckEnd 前缀）、`NetherilReadingInspect`（Pawn.GetInspectString 后缀）。
  - `NetherilJoy` + `NetherilJoySplit`（Need_Joy.GainJoy 前缀）+ `JoyGiver_NetherilRead`；`Book_Netheril` 信息卡/检视加"娱乐类型：阅读、冥想"。
- Source/NetherilQuest.cs：`karsusHours`（整小时）→ `karsusTicks`（刻），存档键 `lpKarsusTicks`；旧档的 `lpKarsusHours` 读入时 ×2500。新增 `readClock`（`lpReadClock`，读错用的阅读钟零头）、`AddReadTicks`、`TickReadClock`、`AddKarsusTicks`。设置新增 `netherilIdleStudy`（`lpNetherilIdleStudy`，默认开），放在"耐瑟瑞尔任务线""拉洛克亡灵袭击"下面。
- Defs/Netheril_ThinkTree.xml（新文件）：ThinkTreeDef `LP_NetherilIdleStudy`，JoyGiverDef `LP_NetherilMeditativeReading`。
- Source/NetherilChecks.cs：旧检查改成按刻驱动，新增 29–33。
- 使用说明.txt：耐瑟瑞尔一节加"阅读规则""娱乐类型""空闲时自动研读"三段，研习卷/残卷/卡苏斯条目改写，设置行与自检条数更新，更新日志加"1.52.0 补丁"。

不需要存档迁移版本号：新字段读不到时都是空表；旧的 `lpNetherilTicks`（每卷"本小时零头"）不再读取，最多丢掉不到 1 小时的零头。

## 冒险者经验
**有，而且原来是按小时给的。** 1.48.0 起研习卷每读满 1 小时 `Adventurer.GainXP(r, Adventurer.StudyXP)`（50，走普通 GainXP，经过 XpScale 和"凡人之志"倍率，**不是**训练上限 GainTrainingXP）。现在改成每次结算 `GainXP(r, 50 × 刻数/2500)`，每小时总量和倍率都不变；训练上限本来就不适用，保持不变。秘术残卷、卡苏斯之卷原来不给冒险者经验，没有新增。

## 思考树插入
- `insertTag Humanlike_PostMain`，`insertPriority -10`。原版 Humanlike 里这个插入点在 `MainColonistBehaviorCore`（工作、吃饭、睡觉、娱乐、冥想、穿衣等全部）**之后**、"空闲殖民者"（IdleJoy、闲逛）**之前**，所以只有主体行为什么都没给出时才轮到研读。节点外层是 `ThinkNode_ConditionalColonist` + `ThinkNode_Tagger(Misc)`（不用 Idle 标签，免得算进"殖民者空闲"提示）。
- 我在创意工坊目录里搜过：除了自带完整种族思考树的模组（Spirit、Zombie 等，它们自己定义 Humanlike_PostMain 位置）以外，没有别的模组往 Humanlike_PostMain 插东西。VPE 的冥想走原版 `JobGiver_Meditate`（在主体行为里，排在我们前面），不冲突；RimHUD 只改界面，不碰思考树。
- 原版阅读 job 中途会因"娱乐满了"或"日程表不是娱乐时间"结束：自动研读 job 设 `ignoreJoyTimeAssignment`，并在 JoyTickCheckEnd 前缀里把满娱乐动作改成 None，所以只会在读满 2 小时（或熵 >90%）时结束。job 靠 `job.jobGiver is JobGiver_NetherilIdleStudy` 识别（原版存档会保存 jobGiver）。
- 停读不在原版阅读 tick 中途直接 EndCurrentJob（后面原版还要用 CurJob 做娱乐判定），而是记下请求，在同一 tick 的 JoyTickCheckEnd 前缀里用 `EndJobWith` 结束（和原版娱乐满了停的方式一样）；没有娱乐需求的读者立刻结束。

## 常量表（Netheril / NetherilIdle）
| 项 | 每小时 | 每 60 刻 |
|---|---|---|
| 结算间隔 SettleTicks | — | 60 刻 |
| 研习卷心灵熵 StudyEntropy | 8（巫妖 ×0.5） | 0.192 |
| 研习卷经验 StudyXP | 6 × 品质 | 0.144 × 品质 |
| 研习卷冒险者经验 Adventurer.StudyXP | 50 | 1.2 |
| 残卷心灵熵 ArcaneEntropy | 4 | 0.096 |
| 残卷进度 ArcaneProgress | 1 × 品质（死灵+巫妖 ×1.5） | 0.024 × 品质 |
| 残卷余效经验 ArcaneResidueXP | 2 × 品质，不涨熵 | 0.048 × 品质 |
| 卡苏斯心灵熵 KarsusEntropy | 6 | 0.144 |
| 卡苏斯一遍 KarsusPassTicks | 40 小时 | 100000 刻 |
| 每日经验上限 DailyCap | 40/人/天，连续扣 | — |
| 读错 | 每累计读满 1 小时判定，max(0,(8−智识)×5%)，额外 +2 × 当小时熵（MisreadExtra=2） | — |
| 自动停读 StopAt | 熵 > 上限 90%，每次结算检查 | — |
| 自动研读最长 NetherilIdle.MaxTicks | 2 小时 = 5000 刻 | — |
| 自动研读门槛 NetherilIdle.EntropyBelow | 熵 < 上限 50% | — |
| 娱乐分摊 | 每次娱乐获得 50% 阅读 + 50% 冥想 | — |
| 冥想类 JoyGiver baseChance | 1.5（原版阅读 3） | — |
| 耐瑟启迪 | 一次连续读满 2500 刻，job 结束时给（不变） | — |

## 检查（-Only Netheril，现在 33 条）
- #3、#4、#6、#9、#10、#11、#13、#26 改成按刻驱动（`ReadTicks(…, 2500)`，#26 的阅读改成真读刻数，不再直接写 session），数值不变。
- 29 研习卷读 600 刻：熵 +1.92、经验 +1.44 × 品质（±0.05），检视面板"研读中：本次 +1.44 …"。
- 30 合格的空闲殖民者拿到阅读 job（思考树定义在 Humanlike_PostMain、优先级 < 0、ignoreJoyTimeAssignment）；没心灵链接 / 熵 50% / 设置关时拿不到。
- 31 卡苏斯之卷（0/7 遍）优先于研习卷；只剩研习卷时挑研习卷；当日 40 用完后不给。
- 32 自动研读：娱乐满了不停，读满 5000 刻提出停读，在 JoyTickCheckEnd 那一步结束，结束后有耐瑟启迪。
- 33 读耐瑟卷轴后阅读和冥想两种耐受度都上升、增量相等、娱乐值上升；冥想类 JoyGiver 能挑到耐瑟卷轴。

## 偏离之处
1. 读错的额外熵是"这个读者 1 小时熵 × 2"，巫妖读研习卷是 +8 而不是 +16（巫妖减半也作用在读错上，和原来"当小时熵 ×3"含巫妖减半一致）。
2. 读错判定在残卷余效（学满 2 个后）时不掷骰，因为余效不涨熵；阅读钟照样走。
3. 秘术残卷"本次读错过"指这一次连续阅读里读错过（和原来一样在放下后重新开始时清掉）。
4. 卡住的残卷（进度 24、没法术可学）每次结算都会再试着领悟，所以灵能等级一够、只要还在读，下一次结算（≤60 刻）就学会；提示文字从"再读 1 小时"改成"再拿起来读"。
5. 自动研读最长 2 小时按实际阅读刻数算（走路去拿卷轴的时间不算）。读的时候来了新工作不会打断（原版阅读每 600 刻只检查更高优先级的需求类行为），最多等到 2 小时读完。
6. 每人开关没做（玩家可以禁止卷轴或用区域限制），设置是全局开关。
7. 自动研读 job 用 Misc 标签而不是 Idle，免得读书的人被"殖民者空闲"提示算进去。
8. 冥想类娱乐的读卷：条件同手动阅读（能读、能到、没禁止），另外熵 > 90% 时不去（去了也会马上被自动停读放下）；优先挑"对他有用"的卷，没有就挑最近的。
9. 旧档里"本卷本小时零头"（lpNetherilTicks）不再读，最多少算不到 1 小时；卡苏斯之卷遍内进度按小时 ×2500 换算保留。

## Peter 需要做的
重启游戏（DLL 和 Defs 都改了）。设置在"事件"分组末尾多了"空闲时自动研读耐瑟卷轴"（默认开）。
