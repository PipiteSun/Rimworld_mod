# 1.64.0 红鹰商业联盟 实现报告

> 2026-10-28。规格：`ArtSource/RedHawk-1.64.0-spec.md`。开工前设备版本 1.63.0（含传奇抗性热修），已备份到 `ArtSource/backup-1.63.0`（About、Defs、Source、Patches、Bridges、1.7（含编译好的 DLL）、LoadFolders.xml、使用说明.txt）。其他备份和其他线程的文件都没动。

## 改动的文件

新文件：
- `Defs/RedHawk.xml`：FactionDef `LP_RedHawkLeague`；8 个 PawnKind `LP_RH_Merchant/Slaver/Appraiser/PactBroker/Mercenary/Crossbowman/Gladiator/Leader` + `LP_RH_SlaveStock`（继承原版 `SlaveBase`）；5 个 TraderKindDef `LP_Caravan_RHSlaver`、`LP_Caravan_RHMaterials`、`LP_Caravan_RHPact`、`LP_Base_RedHawk`、`LP_Orbital_RedHawk`。
- `Source/RedHawk.cs`：`RedHawk` 静态类（MakeSlave / MakePremium / RaceKey / Requirement）、`StockGenerator_RHRaceSlaves`（+ `RHSlaveEntry`、`RHTier`）、`StockGenerator_RHDefList`（+ `RHDefEntry`）、`StockGenerator_RHPactScrolls`、Harmony postfix `RedHawkTraderGate`（`TraderKindDef.CalculatedCommonality`）。
- `Source/RedHawkChecks.cs`：检查组 `RedHawk`（12 项 + 存档前 1 项 + 读档后 1 项）。
- `Bridges/HAR/Patches/RedHawk_HAR.xml`：往三个奴隶生成器的 `entries` 加独眼巨人 / 双头巨人 / 六臂蛇人（`LP_CyclopsColonist` / `LP_EttinColonist` / `LP_MarilithColonist`，权重 0.5，稀有档）。
- `Textures/Factions/RedHawkLeague.png`（128×128，和现有派系图标同规格）；脚本 `ArtSource/RaceArt/draw_redhawk.py`；预览 `ArtSource/Preview/RedHawk-1.64.0.png`。

共享文件（最小改动）：
- `Source/Core.cs`：设置字段 `factionRedHawk=true` + 一条 `Scribe_Values.Look`（放在 `factionBlackNet` 后面）。
- `Source/Factions.cs`：`Toggles` 加 `"redhawk"`，`ToggleOn` 加一个 case。
- `Source/FactionIncidents.cs`：现有「派系」分组里、黑网下面加一个「红鹰商业联盟」勾选框。没有新设置分组（19 组不变，检查第 1 项断言过）。
- `使用说明.txt`：用精确锚点「三、更新日志」标题 + 空行 + `1.63.0：清理版本。` 插入一行 1.64.0 日志。插入前锚点计数 1；插入后「1.64.0：红鹰商业联盟」出现 1 次，`1.63.0：清理版本。` 仍 1 次；和备份逐行比较只多了这一行（1903 → 1904 行）。
- `About/About.xml`：`modVersion` 1.64.0，描述末尾加一句 1.64.0 简介。

## 测试

只跑了指定的两次（外加一次修复后的重跑）：
- `pwsh Source\Test.ps1 -Only RedHawk`：第一次在第 11 项（飞空商船）失败——我用反射调 `IncidentWorker_OrbitalTraderArrival.CanSpawn` 时当成了静态方法（它是实例方法）。改成用 `OrbitalTraderArrival` 事件的 Worker 调用后重跑：**PASS: 14 checks (only RedHawk); save/reload.**
- `pwsh Source\Test.ps1 -NoHAR -Only RedHawk`：**PASS: 14 checks (only RedHawk); save/reload.**
- 两次日志里都没有和本版本有关的 WARN/ERROR（只有原来就有的显卡层、别的模组依赖和两个 StaticConstructorOnStartup 提示）。

第一次（有 HAR）的结果摘录：奴隶商队三次 11/13/11 名，每次种族全不同，稀有档 6/7/4；35 名奴隶全是成年人形、有异种型、`joinStatus=JoinAsSlave`、无亲属；33 个基础种族全部解析并生成成年个体（含力能龙裔）；HAR 三个种族按各自种族生成；材料商队 13 种材料、龙鳞恰好 2 色；契约商队 2 张契约书宗主不同、名字带宗主；契约书照常出「出价信」；关契约后灵魂币 68 枚、无契约书和契约器物，再关深渊后契约商队权重 0；据点 8 名奴隶 + 10 堆材料 / 契约物 + 2 张契约书；飞空商船 5 名奴隶 + 1 张契约书，敌对时 `CanSpawn` 为 false；派系开关关 / 开正确；存档读档后 3 张契约书宗主不变。

没跑全套（按要求）。

## 决定

1. **奴隶的「奴隶身份」**：原版商队库存里的奴隶不是 `GuestStatus.Slave`（反编译确认：`StockGenerator_Slaves` 只生成人，`Pawn.PreTraded` 在玩家买下时按 `guest.joinStatus` 决定是否成为奴隶）。我照原版：库存里设 `joinStatus = JoinAsSlave`，买下后由原版流程变成奴隶。检查第 3 项查的是这个字段。
2. **奴隶所属派系**：用出售方派系（红鹰），派系为空或为玩家时用红鹰派系。原版是随机挑一个可见派系；用红鹰更简单，也避免「买别人派系的人」引出的好感问题。
3. **力能龙裔**留在池里：用 `DevelopmentalStage.Adult` 生成成年体，检查确认能正常生成、异种型正确。
4. **种族池条目数**：规格写「基础 30 种」，但表里列出的是 33 个异种型（元素裔 4 种分开算）。按表全收：普通 14（权重 3）+ 稀有 19（权重 1），据点和飞空商船用同一个池（XML 里写了三遍，HAR 补丁一次加到三处）。
5. **精品奴隶**（15%）：没有 DND 背景特性时一半机会加一个（避开冲突特性），否则随机一项可用技能 +4。价格不另设，用原版市价。
6. **稀有档保底**：商队 ≥3，据点 ≥2，飞空商船 ≥1（规格只给了商队的 3）。
7. **商队护卫数量**：原版 Trader 组的护卫人数按点数算，XML 不能固定「佣兵 4~6 + 弩手 2~4」。改为权重 佣兵 5 / 弩手 3（奴隶商队加角斗士 1）。
8. **奴隶贩子的武器**：原版没有皮鞭，主武器是中世纪 / 新石器基础近战；制伏短棍放进**随身物品**（`inventoryOptions`），不进 `weaponTags`——1.61.0 的规则是制伏武器不给 NPC 当主武器（Nonlethal 检查断言没有 PawnKind 用 `LP_Nonlethal` 武器标签）。
9. **服装**：原版没有皮甲 / 链甲，佣兵 = 原版装备 + 必带简易头盔，商人 / 掮客 / 鉴定师 / 总商穿长袍；所有兵种 `apparelColor` 暗红并忽略意识形态颜色（PawnKind 只能给一个颜色，金色做不到）。契约掮客的「手杖」用新石器基础近战（木棒之类）。
10. **材料 / 契约物品的收购**：`StockGenerator_RHDefList.HandlesThingDef` 对列表里所有东西返回 true，所以商人自然收购自己卖的东西，没有另写「收购版」生成器；契约商队另外收 `LP_Pact` 标签物品和天界羽披。
11. **契约开关**：契约专属物品（`Pact_Items.xml` 里的：契约书、缚绳、地狱钢叉、狂猎号角、妖精银弓）在条目上标 `requires=pact`；真名卷、恶魔学典籍、位面锚链、灵魂币、恶魔血属于深渊（1.54.0），照常出售；灵魂币和恶魔血 `pactOffFactor=2`。契约 + 深渊都关时，用 `TraderKindDef.CalculatedCommonality` 的 postfix 把契约商队的权重设成 0（原版商队选取和 `FactionCanBeGroupSource` 都走这个值）。
12. **派系开关关掉时**：同一个 postfix 也让带 `faction` 的商人（飞空商船）在派系关掉时不来；其余照现有派系规则（新世界不生成、不袭击、不派商队，已有存档保留派系）。
13. **飞空商船敌对检查**：反编译确认 1.7 的 `IncidentWorker_OrbitalTraderArrival.CanSpawn` 会找 `TraderKindDef.faction` 的派系并要求有殖民者 `CanTradeWith`（敌对不行），所以不用另加。
14. **契约书宗主**：设备上的 `CompPactScroll` 是懒定宗主（第一次显示名字时随机），但已有 `SetPatron` 且宗主存档。生成库存时就逐张 `SetPatron` 互不相同的宗主，没有新增 `presetPatron` 字段，阅读 / 隐藏条款 / 偿契流程完全没改。
15. **图标颜色**：RimWorld 的派系图标按派系颜色整体染色，现有 6 个图标都是白 / 灰剪影。所以贴图同样是灰阶，金币用最亮的白色 + 深色描边和鹰区分；游戏里看到的是暗红色的鹰和浅红的硬币。预览图里另画了一张红 / 金 / 羊皮纸的「本色示意」。
16. **成员称呼**：`pawnSingular`/`pawnsPlural` 都是「红鹰商人」，首领「总商」；`basicMemberKind` 用红鹰佣兵（同其他派系，基础成员是战斗兵种）。
17. **好感**：初始 +10（`FactionExtension.initialGoodwill`），不挂种族好感，和其他派系的关系照原版随机；买奴隶不影响费伦拓荒同盟。

## 偏差（和规格不同的地方）

- **迷锁碎片不卖**：`LP_MythalShard` 的 `tradeability=None`（迷锁地牢的货币，故意不能交易），商人卖不出来，从材料商队库存里拿掉了。
- **天界羽披不卖**：`LP_CelestialMantle` 的 `tradeability=Sellable`（只能卖给商人），商人卖不出来；改成契约商队和据点**收购**它。
- **契约书名字的格式**：规格要「契约书（梅菲斯托菲勒斯）」，设备上的 `CompPactScroll.TransformLabel` 早就显示「契约书：梅菲斯托菲勒斯」，检视面板也已经写「九狱 / 妖精宗主 X 的契约书（期限：…）」。为了不改契约系统的共享代码，保留现有格式。
- **奴隶的 guest status**：见决定 1——库存里是 `joinStatus=JoinAsSlave`，不是 `GuestStatus.Slave`（买下后才是，和原版一致）。
- **基础池 33 个而不是 30 个**：见决定 4。
- **图标尺寸**：规格写 64×64「和派系图标同规格」，现有派系图标实际是 128×128，按现有的画。
- **据点和飞空商船的材料**：「随机挑 8 / 6 种」是从一张 21 条的表里挑（含 5 色龙鳞、4 种元素符各算一条），所以据点里可能出现 2 色以上的龙鳞；「恰好 2 色」只对材料商队。

## 没做 / 没验证

- **Isekai**：没有跑 `-Isekai`（按要求只跑两次指定测试）。代码没有对 Isekai 做任何处理；Isekai 给库存奴隶的等级照它自己的规则，不处理。
- **Ideology 关闭**（`-NoIdeology`）没跑；memes / precepts 都带 `MayRequire`。
- 没进游戏实际交易过（商队到来、交易界面、买下后变成奴隶都只在自检里间接验证）。护卫人数、袭击频率 ×0.5 只检查了数值，没实战。
- 图标只看了预览图，没在世界地图上看过。
