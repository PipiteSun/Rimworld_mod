# 1.26.0：基因工作台（实现报告）

日期：2026-10-25。备份：`ArtSource/backup-1.25.1`（1.7、About、Bridges、Defs、Patches、Source、Textures、使用说明.txt、LoadFolders.xml，共 494 个文件；模组没有 Languages 目录）。

## 0. 开工前核对原版（反编译 1.7 Assembly-CSharp.dll，ILSpy 库）

| 原版 | 关键事实 | 对设计的影响 |
|---|---|---|
| `Building_GeneExtractor` | 30000 刻；断电暂停，累计 60000 刻弹出；结束调 `GeneUtility.ExtractXenogerm(pawn, 60000×(12～20天))`；囚犯/奴隶得 `XenogermHarvested_Prisoner`；随机取 1～4 个基因，权重排除古菌（`biostatArc>0`）、黑色素、代谢超出 -5～5 | 定向提取沿用这些数字和副作用；可选基因用同一套排除规则 |
| `GeneUtility.ExtractXenogerm` | 加 `XenogermLossShock`（基因缺失冲击）+ `XenogermReplicating`（基因重构，中文“基因重构”）；正在重构时再提取会致死 | 直接调用原版函数；正在重构的人不可选（原版只是弹确认框，这里按需求直接禁止） |
| `Building_GeneAssembler` | `GetGenepacks`、`GetGeneBankHoldingPack`、私有 `CheckAllContainersValid` 都只看 `ConnectedFacilities`（相连设施）；`IThingHolder` 是古菌胶囊容器 | 补三个 Harmony 补丁让自带基因库也算；不改 `ConnectedFacilities` 本身（小 getter，CoreCLR 下可能被内联，补丁不可靠） |
| `WorkGiver_CreateXenogerm` | `ThingRequest.ForDef(ThingDefOf.GeneAssembler)`，只认原版装配器 | 新 WorkGiverDef（研究工作）用子类只改 `PotentialWorkThingRequest` |
| `JobDriver_CarryGenepackToContainer` / `Toils_Haul.DepositHauledThingInContainer` | 用 `TryGetInnerInteractableThingOwner` 找容器，对装配器子类会拿到古菌胶囊容器 | 入库那一步临时把目标改成工作台的基因库 |
| `CompGenepackContainer` | 基因组老化/修复在 `CompTickRare`；只有 Rare 刻建筑才会调用 | 工作台是 Normal 刻（装配需要），每 250 刻手动调一次 |
| `WorkGiver_CarryToGeneExtractor` | 工作类型“搬运”，`Building_Enterable` 专用 | 写了同逻辑的搬运 WorkGiver/JobDriver |
| 研究 | 原版没有叫“GeneAssembly/基因组装”的研究；装配器、提取器、基因库都要 `Xenogermination`（中文“异种遗传学”） | 工作台用 `Xenogermination` |

## 1. 设计

- `LP_GeneWorkbench`：`thingClass = LichPhylactery.Building_GeneWorkbench : Building_GeneAssembler`。5×5，可旋转，交互格 `(0,0,3)`（正面外一格）。组件：`CompPowerTrader`（400W／待机100W）、`CompFlickable`、`CompGenepackContainer`（8格）、`CompAffectedByFacilities`（可连原版基因库、基因处理器）、`CompGeneWorkbench`（定向提取）。检视页 `ITab_ContentsGenepackHolder`。造价 钢400、零部件10、高级零部件4；工作量26000（原版 12000+9000+5000）；建造技能6。
- **装配**：完全是原版装配器的代码，复杂度/代谢/古菌/时长都不动。补丁：
  - `GetGenepacks` 后缀：加上自带库的基因组（按工作台自己是否有电归入“有电/无电”）。
  - `GetGeneBankHoldingPack` 后缀：右键“从基因库弹出”能找到自带库。
  - `CheckAllContainersValid` 前缀（仅工作台）：选中的基因组在相连库或自带库里都算有效，否则照原版取消。
  - `JobDriver_CarryGenepackToContainer.MakeNewToils` 后缀 + `ThingOwnerUtility.TryGetInnerInteractableThingOwner` 后缀：搬运工入库时投进自带库，不会掉进古菌胶囊容器（古菌胶囊照常进装配器容器）。
- **定向提取**（`CompGeneWorkbench`，组件自己是 `IThingHolder`，被提取者放在组件的容器里）。为什么放组件：装配器的 `IThingHolder` 已被古菌胶囊容器占用、方法不是虚的；组件容器会被地图持有树收录（原版基因库同样做法），人仍被地图计入、存档正常；组件在 `CompTick` 里给里面的人跑 tick（与原版提取器一样被提取者需求照常变化）。
  - 按钮“指定提取基因” → 人员菜单（殖民者、奴隶、囚犯；不合格的列出原因）→ 基因菜单（中文名、种系/异种、复杂度、代谢，带基因图标）。
  - 能走的殖民者/奴隶直接下令走进去（`LP_EnterGeneWorkbench`，不预约建筑，免得和装配研究员抢）；囚犯/倒地/不能走的由搬运工送（`LP_CarryToGeneWorkbench`，工作类型“搬运”，同原版提取器）。选中的殖民者被打断后，可以右键工作台重新下令。
  - 30000 刻，断电暂停、累计 60000 刻弹出；完成：单基因基因组进自带库（满了放旁边），`GeneUtility.ExtractXenogerm`（同原版 12～20 天），囚犯/奴隶心情，人从交互格出来；基因不移除。
  - 取消（等待或进行中）：放人、无产出、无状态。
  - 选择“稳健”而非“花哨”：没有模仿原版 `Building_Enterable` 的整套进入流程（工作台已经继承装配器，不能再继承它），改用最少的两个 JobDriver + 一个 WorkGiver，和原版提取器的行为一一对应。

## 2. 改动的文件

| 文件 | 内容 |
|---|---|
| `Defs/GeneWorkbench.xml`（新） | 工作台 ThingDef、两个 JobDef（进入／搬入）、两个 WorkGiverDef（搬运：带到基因工作台；研究：在基因工作台创建异种胚芽），全部中文 |
| `Source/GeneWorkbench.cs`（新） | 建筑类、提取组件、JobDriver×2、WorkGiver×2、Harmony 补丁×5 |
| `Source/GeneWorkbenchChecks.cs`（新） | 55 项检查 |
| `Source/Checks.cs` | 接入 `CheckGeneWorkbench` / `PrepareGeneWorkbenchSave` / `CheckGeneWorkbenchAfterLoad`，PASS 文案加 1.26.0 |
| `Textures/GeneWorkbench/`（新） | `GeneWorkbench_south/north/east.png`（640×640，128 像素/格；西向由游戏镜像）、`ExtractGene.png`（128×128） |
| `ArtSource/GeneWorkbench/draw_gene_workbench.py`（新） | 贴图绘制脚本：`python -I <脚本> <模组根目录> [原版PNG目录]` |
| `ArtSource/Preview/GeneWorkbench-1.26.0.png`（新） | 预览图（另复制到 `C:\Users\25390\OneDrive\桌面\GeneWorkbench-1.26.0.png`） |
| `About/About.xml` | 版本 1.26.0，描述末尾加一句 |
| `使用说明.txt` | 标题版本、内容一览一行、第二部分【1.26.0 基因工作台】、更新日志 1.26.0 |
| `1.7/Assemblies/LichPhylactery.dll` | 重新编译 |

## 3. 贴图

程序绘制（PIL），对照从 `Data/Biotech/Assets.zip` 解出的原版基因装配器、提取器、基因库 DDS：粗黑外轮廓，柔和渐变与倒角，原版生物科技的浅灰面板 + 青色玻璃/屏幕 + 琥珀/绿色指示灯。原版是 64 像素/格，这里用 128 像素/格。朝南：后排 8 格基因库柜，中央青色玻璃舱和素色软垫床（被提取者躺在这里），左侧装配器式穹顶，右侧带螺旋屏的控制塔，前排操作台。按钮图标：青色双螺旋中一个琥珀色基因被箭头抽进基因组。

## 4. 测试

`pwsh Source/Build.ps1`：主程序集和神兽血裔联动都编译通过。

> **更正（1.27.2，2026-10-25）**：下面“2 次 PASS、日志红字过滤没有命中”的说法是错的。那两次 LichResult 写的是 `PASS: 3297 checks`，但日志里已经有读档时的红字“Exception registering RimWorld.Genepack … same key”，`Test.ps1` 随后因日志红字判了失败（退出码非 0）。我当时只看了 LichResult 的 PASS 行，没看 Test.ps1 的退出码。这条红字就是工作台的存档 bug（自带基因库与装配器的容器同名，读档时基因组被读两遍、超凡胶囊丢失），已在 1.27.2 修复，见 `ArtSource/GenepackFix-1.27.2-report.md`。

`pwsh Source/Test.ps1`（Core 档，隔离存档目录，不碰 Peter 的存档）：最终代码跑了 3 次，2 次 LichResult 为 `PASS: 3297 checks`（1.25.x 是 3241；新增 55 项工作台检查，加上 `LP_` 定义 ConfigErrors 循环多出 1 项）；~~日志红字过滤没有命中~~（错误，见上方更正）。另 1 次失败是老的偶发检查“圣水对魔鬼双倍伤害”（第 2078 项，在工作台检查之前；1.25.0 报告里也出现过）。

开发中的其他失败：老的偶发检查“smite reaches 10 cells”“dragon egg start…hoard”“dragon's own bedroom…”“Skeleton registry rebuild”；还有我自己测试写错的两处（刚生成的小人要等一刻才接下令的工作；清理循环边遍历边改列表），都已修正。

新增检查覆盖：
- 定义：5×5、中文、组件、400W、8 格库、可连基因库和基因处理器、研究、造价、贴图加载、两个 WorkGiver。
- 古菌和黑色素基因被排除。
- 自带库：进入 `GetGenepacks`；断电时算作未供电；`GetGeneBankHoldingPack` 能找到；原版装配对话框会列出；装配中基因组被拿走时照原版取消；古菌胶囊仍进装配器容器。
- 原版“存入基因组”搬运工作把地上的基因组送进自带库。
- 殖民者提取：自己走进去；仍被地图计入；需求照常下降；满功率 400W；30000 刻；单基因基因组进库；基因缺失冲击 + 12～20 天基因重构；基因不移除；重构中不能再提取。
- 囚犯提取：由搬运工送入；得到原版“基因被采集”心情；库满时基因组放在地上。
- 断电暂停；取消时放人、不产出、不加状态；取消待命的走路命令。
- 存读档：提取进行中的人、基因、剩余时间、库内容都保留。

## 5. 没在真实游戏里点过的地方 / 待确认

- 两级浮动菜单、按钮、检视文字、躺在工作台里的人物绘制位置（画在建筑正中，躺姿同原版提取器）只在代码层面检查过，没在真实游戏里用鼠标点过，也没截图看。
- 贴图只在预览图里和原版对比过，没在游戏地图里看过实际效果和东、西朝向的观感。交互格在正面外一格，贴图正面是前排操作台。
- 研究员在工作台装配异种胚芽用的是原版 JobDriver。这里只检查了 WorkGiver 指向工作台、`CanBeWorkedOnNow` 通过，没有跑完一整次装配。
- 原版对“正在基因重构的人”是弹确认框后照样提取（会致死）；这里按需求直接不让选。若想保留原版行为可以改。
- 研究前置用原版 `Xenogermination`（异种遗传学），因为原版没有单独的“基因组装”研究。如果想更晚解锁（比如再加“基因处理器”研究）可以加。
- 造价、耗电（400W，待机 100W）、工作量 26000、建造技能 6 是我定的，可以调。
