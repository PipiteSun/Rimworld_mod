# 1.27.2：基因工作台存档修复（报告）

日期：2026-10-25。基于 1.27.1。备份：`ArtSource/backup-1.27.1`（1.7、About、Bridges、Defs、Patches、Source、Textures、使用说明.txt、LoadFolders.xml，共 550 个文件）。

## 1. 现象与根因

现象：读档时偶尔出现红字 `Exception registering RimWorld.Genepack GenepackNNN in loaded object directory … same key`。

根因：
- 基因工作台（1.26.0 起）继承原版 `Building_GeneAssembler`，又挂了原版 `CompGenepackContainer`。
- 两者存档时都在建筑节点下写一个叫 `innerContainer` 的子节点：组件的（基因组）在前，装配器的（超凡胶囊）在后。原因是组件的 `PostExposeData` 在装配器 `base.ExposeData()` 里先跑。
- 读档时原版 `Scribe_Deep` 用 `curXmlParent[label]`，只取第一个同名节点。于是装配器的容器又把组件的基因组反序列化了一遍：同一个 ID 的基因组出现两个对象，交叉引用注册时报重复键。
- 真正的超凡胶囊节点没人读，胶囊丢失。
- 只有自带库里有基因组时才报，所以看起来“偶发”。

复现（独立的临时复现模组 + 单独隔离档案，跑完已删除，没碰 Peter 的存档）：自带库 2 个基因组、装配器 3 个超凡胶囊，存读档一次后：
- 每个基因组两份：一份在 `CompGenepackContainer`，一份在 `Building_GeneWorkbench`；
- 4 行红字；
- 超凡胶囊 0 个。
测试档 `Lich-Core/Saves/Lich-Roundtrip.rws` 的工作台节点也确认是两个同名 `innerContainer`。

额外危害：
- 原版装配完成或取消时，会把装配器容器 `TryDropAll` 倒到地上，复制品就被放出来了（可以刷基因组）。
- 存过档的工作台里的超凡胶囊会消失。

## 2. 修复

| 文件 | 改动 |
|---|---|
| `Source/GeneWorkbench.cs` | 新增 `CompLP_GenepackContainer : CompGenepackContainer`，只重写 `PostExposeData`：基因组存到 `lpGenepacks`，`leftToLoad`、`autoLoad` 不变。`Building_GeneWorkbench.ExposeData` 在读档（LoadingVars）时、调用 base 之前跑 `GeneWorkbench.MigrateLegacyNode`。新增 `GeneWorkbench.RemoveDuplicateGenepacks` 和 `GameComponent GeneWorkbenchSaveFix`（`LoadedGame` 时去重）。 |
| `Defs/GeneWorkbench.xml` | 基因库组件加 `<compClass>LichPhylactery.CompLP_GenepackContainer</compClass>`，属性类仍是原版 `CompProperties_GenepackContainer`。 |
| `Source/GeneWorkbenchChecks.cs` | 新增检查：迁移单元检查、去重检查、存读档检查，以及真实旧格式读档检查（见第 3 节）。 |
| `Source/Checks.cs` | 存档写完、读档之前调用 `RewriteLegacyGeneWorkbench()`，把其中一台工作台在存档文件里改写回 1.26～1.27.1 的旧格式。 |
| `About/About.xml`、`使用说明.txt` | 版本 1.27.2；1.27.2 条目放在 1.27.1 之前。 |
| `ArtSource/GeneWorkbench-1.26.0-report.md` | 更正当时“2 次 PASS、日志无红字”的错误说法。 |

迁移规则（`MigrateLegacyNode`）：
- 节点已有 `lpGenepacks`（新格式），或 `innerContainer` 少于 2 个，就什么都不做。
- 否则把第一个 `innerContainer`（旧的基因库）改名为 `lpGenepacks`，保留 `Class` 等属性。
- 再从剩下那个（装配器的）`innerList` 里删掉 `def=Genepack` 的条目：原版装配器只放超凡胶囊，这些条目是“带着 bug 读过又存过”的档里的复制品。
- 函数是幂等的。

为什么用改存档标签、而不是改装配器：装配器的 `ExposeData` 里标签是写死的；原版检视页、搬运、`Genepack.ParentContainer` 都按 `CompGenepackContainer` 基类识别，子类不影响它们。

旧存档效果：
- 在 1.26～1.27.1 里没有“读档后又存档”的：基因组和超凡胶囊完整找回，读档无红字（测试里真实验证）。
- 读过又存过的：装配器里那份重复条目在读档前剔除。已经被倒到地上或搬进别的库的复制品，读档后按 ThingID 只留一份，优先留在基因库里的。这种情况第一次读档仍会有一次红字，再存一次档就干净了。
- 已经丢失的超凡胶囊找不回来。

## 3. 测试

`pwsh Source/Build.ps1`：主程序集、VPE 联动、神兽血裔联动都编译通过。

`pwsh Source/Test.ps1`（Core 档，隔离存档目录）。**这次以 Test.ps1 的退出码为准**，并统计所有日志红字行（同 Test.ps1 的过滤规则）。

修好测试本身之后跑了 4 次：

| 次 | Test.ps1 退出码 | LichResult | 日志红字行 |
|---|---|---|---|
| 1 | 1 | FAIL after 1509：巨魔血成瘾（老偶发，在工作台检查之前） | 1（就是这条检查失败） |
| 2 | **0** | **PASS: 3348 checks** | **0** |
| 3 | 1 | FAIL after 1698：死灵冲击卷轴（老偶发，在工作台检查之前） | 1（就是这条检查失败） |
| 4 | **0** | **PASS: 3348 checks** | **0** |

- 1.27.1 是 3337 项，新增 11 项。
- 两次通过的日志里都没有 `Exception registering`、`same key`。

开发中另有 4 次失败，都已处理：
- 2 次是老偶发（精灵锁子甲闪避、炎魔火焰光环）；
- 2 次是我写的“改写旧格式”测试步骤漏拷了 `Class` 属性，造成 `Can't load abstract class Verse.ThingOwner`。这是测试里模拟旧档的步骤，不是迁移代码本身（迁移代码会拷属性），已修正。

新增/改动的检查：
- 迁移：旧格式第一个 `innerContainer` 改名为 `lpGenepacks`；装配器只剩超凡胶囊、重复基因组条目被剔除；迁移幂等；新格式不动；工作台基因库使用新组件。
- 去重：同 ID 复制品放在地上时，保留库里的、销毁地上的；第二次清理找不到东西。
- 存读档（新格式）：自带库的基因组 ID 集合不变；装配器容器恰好是 3 个超凡胶囊、没有基因组；全图没有重复 ID 的基因组。
- 存读档（真实旧格式）：存档写完后，把第二台工作台在 `.rws` 里改写回旧格式（基因库容器改回 `innerContainer` 并放在装配器容器前面，再往装配器容器塞一份重复基因组条目），然后读档。结果：基因组回到自带库，2 个超凡胶囊恢复，重复条目被丢弃，没有红字。

## 4. 待确认 / 没覆盖的

- “已被倒到地上的复制品”去重只在运行中模拟过（手动造了一个同 ID 的基因组），没有用真实的“旧版本读档、取消装配、存档”的完整存档验证。
- 去重只扫描各地图（含地图上所有容器），不扫描世界上的商队或运输舱。复制品被商队带走的情况不处理。
- 我自己没有 Peter 的真实存档可测；建议 Peter 用一个有基因工作台的旧档读一次，看日志有没有这条红字、库里和装配器里的东西对不对。
