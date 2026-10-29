# DND边缘大陆 1.58.6：性能优化报告

> 日期：2026-10-27。设备版本 1.58.5 → 1.58.6（原计划编号 1.58.4 让给了审计文件，审计文件仍叫 `ArtSource/Performance-1.58.4-audit.md`，本报告里的 P1～P19、R1～R5 编号都指它）。
> 备份：`ArtSource/backup-1.58.5`（整个模组，不含已有的 backup-* 文件夹；backup-1.58.3、backup-1.58.3-art 没有动）。
> 开始前核对：没有 RimWorld 进程在运行。自 13:00 审计以来，审计涉及的源码里只有 `Genie.cs` 被改过（神灯热修），另外新增了 1.58.5 的艺术品（`ArtworkChecks.cs`、`Defs/Artworks.xml`），以这些为基础继续做。
> 原则：行为不变。审计第五节那 4 项会改变行为的优化**没有做**。还有几处“同一刻内记忆”的改动，在同一刻内结果不变，单列在第三节。

## 一、结论

- **单次调用的开销（微基准，同一批小人，修改前后对比，单位 ns/次）：**
  - HealthScale：234 → 67（−71%），每次分配 344 → 24 字节（24 字节是基准自身的装箱）。
  - 部位生命 GetPartHealth：266～355 → 88～176（−50～67%）。
  - BodySize：人形 118 → 71（−40%）；DND 种族 310 → 147（−53%）。
  - 能力 CanCast：2180 → 280（−87%），每次分配 1392 → 120 字节。
  - VEF 的 IsEnabledForPawn：6814 → 3978（−42%），分配 1832 → 600 字节。
  - 守护姿态找守护者：1527 → 343（−78%），分配 1224 → 0 字节。
  - 威胁判断 ThreatDisabled：−34～44%。
  - 属性（清掉缓存后重算）：MoveSpeed −20～23%（分配 328 → 24 字节）；PsychicSensitivity −23～28%（分配 1360 → 56 字节）；WorkSpeedGlobal −9～17%；GeneralLaborSpeed −8～12%。
  - 堆叠判断 CanStackWith（装了神兽血裔时）：88 → 56。
  - 职业技能经验：1736 → 1415。
- **整图（100 个小人、空闲殖民地、不打仗）**：不同进程之间的绝对毫秒数波动约 ±20%，所以看同一进程内“DND 混合人口比普通人口多出的开销”（下表）：
  - 主程序集：修改前 +15.7% / +19.1%，修改后 +17.8% / +15.8%，在噪声范围内看不出差别。
  - 带 VPE：修改前 +24.8% / +26.2%，修改后 +16.6% / +20.2%，多出的开销少了约 7 个百分点。
  - 原因：这次修掉的热点大多在战斗、索敌、属性重算和界面里，空闲地图上走得少。打仗时（大量 CanHitTargetFrom、ThreatDisabled、HitReportFor、受伤钩子）的收益应该比空闲地图明显，但这次的计时组没有做战斗场景（见第四节）。

| 同一会话交替运行（每轮 1000 刻，5 轮中位数，ms/刻） | 普通 100 人 | DND 混合 100 人 | 混合比普通多 |
|---|---|---|---|
| 旧 1.58.5 第 1 轮 | 0.967 | 1.119 | +15.7% |
| 新 1.58.6 第 1 轮 | 0.793 | 0.935 | +17.8% |
| 旧 1.58.5 第 2 轮 | 0.780 | 0.929 | +19.1% |
| 新 1.58.6 第 2 轮 | 0.951 | 1.102 | +15.8% |
| 旧 VPE 第 1 轮 | 0.940 | 1.173 | +24.8% |
| 新 VPE 第 1 轮 | 0.898 | 1.047 | +16.6% |
| 旧 VPE 第 2 轮 | 0.939 | 1.185 | +26.2% |
| 新 VPE 第 2 轮 | 0.899 | 1.081 | +20.2% |

（旧版本是 `backup-1.58.5` 的源码加上新的 PerfChecks.cs 另外编译的，DLL 交替换上；跑完已换回新 DLL。）

- 原有的两个计时组（每组 3 轮平均，含第一轮预热，噪声更大）：
  - AbyssPerformance：修改前 0.956 / 0.883，修改后 1.129 / 0.998。
  - PactPerformance：修改前 0.842 / 0.902，修改后 0.911 / 0.904。
  - 它们的人口是普通殖民者，修改后第一轮又分别出现了 1.464 和 1.075 的离群值，没有参考价值，只照实记录。

## 二、改动清单（文件:行，行号是修改后的位置）

### 公共基础（审计第〇节 R1～R5）
- **新文件 `Source/PerfCache.cs`：**
  - `DefRef<T>`：静态的按名字 Def。查不到时记下 DefCount，Def 数量变了才重查，所以自检运行时注册的新 Def 仍然能查到。
  - `DefCache<T>`：按运行时拼出的名字查，查不到的也缓存。
  - `GameComp<T>` / `WorldComp<T>` / `MapComp<T>`：组件缓存。以 Game / World / Map 对象和它的组件列表引用一起作键；读档时整个列表会被换掉，所以缓存自动失效；查不到组件时不缓存 null。
  - `HediffIndex<T>`：按小人 thingIDNumber 登记单例状态。PostAdd / 读档 PostLoadInit 时登记，PostRemoved 时标记失效；没登记过的小人直接返回 null，失效时回落到原来的扫描。
  - `GeneVersion`：任何 Notify_GenesChanged 都递增。
  - `RenderTags.IsRoot`：渲染根节点的判断。
  - `MemoryDefs.ObservedCorpse`：判断是否“看到尸体”的想法。
- **R1 按名字查 Def：**
  - `Items.cs:13-15`：Thing / Hediff 改用 DefCache，查不到的也缓存。
  - `Items.cs:71`：Worn(string) 没穿衣服时直接返回。
  - `Items.cs:79`：Dispelled 改用 DefRef。
  - `Races124.cs:14-18`：Gene / Xeno / Ability 改用 DefCache；Has 先判断 `genes==null`。
  - `CommonRaceSubraces.cs:19-25`：G / H 改用 DefCache，同时修掉了原来在属性钩子里写普通 Dictionary 的问题。
- **R2 全图复制：**
  - `Races124.cs:39-52`：PawnsNear 不再 `AllPawnsSpawned.ToList()`，只快照调用时半径内的人，产出时照旧再检查一次（见第三节第 1 条）。
  - 新增 FirstPawnNear。
- **R3 组件查找：**
  - 31 个 `Current.Game?.GetComponent<…>()` getter 改为 `GameComp<…>.Get()`。
  - `Faith.Comp`、`Ascension.Tracker`、`Twilight.World` 改为 `WorldComp`。
  - 地图组件统一改为 `MapComp`：ItemsMap、MapComponent_Faith / Legendary / DragonLair / Outsiders / PlanarThinness、GunMap。
- **R4 `Adventurer.Get`：**
  - `Adventurer.cs:32-41`：按小人登记（registry）。`Hediff_Adventurer` 新增 PostAdd / PostRemoved（`Adventurer.cs:543-546`），读档时也登记。
  - `Feats.Has`、`Epic.Has`、LearnFactor、PartHealthFactor 都因此变成一次字典查找。
- **R4 同类：** `Necro.Lich` / `Necro.Construct`（`Core.cs:48-50`）、`ForceDragon.AgeOf`（`ForceDragon.cs:40`）、`Demigods.Of`（`Demigod.cs`）改用 HediffIndex，对应状态类加了登记（`PerfCache.cs`、`Core.cs`、`Gameplay.cs`、`ForceDragon.cs:209-213`、`Demigod.cs:63-67`）。
- **R5 CurStage：**
  - `Adventurer.cs:50`：IsekaiActive 缓存 ModsConfig 的结果。
  - `AdventurerEpic.cs:38`：IkmActive 同上。
  - `HeroDestiny.cs:21`：VPEActive 同上（保留 forceVPE 优先）。
  - 其余见 P14。

### 审计条目
| 条目 | 改动位置 | 做法 |
|---|---|---|
| P1 魔鬼之眼 | `Fiends.cs:104-110`、`:144-171`；测试 `Feats2Checks.cs:37`、`Races124Checks.cs:267` | 前缀只压栈 / 出栈；`Sees` 挪到隐身判断返回 true 时才算 |
| P2 提尔审判之庭 / 守护姿态 / 全图复制 | `Twilight.cs:217-229`（现存化身容器登记）、`TwilightDeities.cs:253`、`Abilities.cs:170-186`（VPE）、`Races124.cs:39` | 没有已生成的化身容器就直接返回；按原顺序原地遍历；DefRef |
| P3 ModsConfig | 见 R5 | 缓存 |
| P4 BodySize | `SmallBody.cs:27-48`、`ConstructHuman.cs:16-18,69`、`ForceDragon.cs:40`；6 处 ScaleFor 的 `RenderTags.IsRoot` | 只扫一遍基因表；先判断是不是人类；按 kind 缓存 LegacyRace；龙龄按小人登记 |
| P5 HealthScale / 部位生命 | `ChromaticDragons.cs:674-678`、`NetherilQuest.cs:530`、`ConstructDesign.cs`（随 Construct 登记） | 静态 DefRef；PartHealthFactor 因 R4 变便宜 |
| P6 衰老三个后缀 | `NetherilAscension.cs:20`；Epic / Demigod 随 R4 | 静态 Def；登记查找 |
| P7 暗影之腿 | `ShadowWalking.cs:26-30`、`:78-80`、`FleshImplants.cs:9-16` | 装过的小人登记；按 Def 引用比较 |
| P8 射程 / 命中 / 威胁 / 哨兵 | `MagicItemsCombat.cs:142-170`、`MagicItems.cs:33-37`、`ItemsRaceGear.cs:131-136,171-176`、`MagicApparel.cs:115-117,314`、`ItemsWondrous.cs`、`CommonRaceSubraces.cs:91-107`、`AdventurerFeats.cs:174-191` | 静态 DefRef；按武器定义记住是不是弓；骨质护符先查佩戴；哨兵先看会不会出手再查“灵活”，不再每步 ToList |
| P9 属性部件 | `Demigod.cs:285-312`、`Faith.cs:690-730`、`Genasi.cs:22-24,96-104`、`ItemsRaces124.cs:24`、`Marilith.cs:128-133`、`HARGiants.cs:114,180-186`、`CommonRaceSubraces.cs:40-80`、`AdventurerFeats2.cs:286-302`、`StormGiant.cs:39` | 闭包移出热路径；神明只在用到的分支里算；先按属性分支再算血脉；HAR 专属基因用静态 Def（没装 HAR 时直接返回）；缓存命中不再分配；配方结果按配方记住；先看天气 |
| P10 技能经验 | `VPEBridge.cs:146-165`、`Adventurer.cs:206-210` | 没有职业路径用到的技能直接返回；先判断 EffectScale |
| P11 魅魔禁用门槛 | `Outsiders.cs:265-280,330`、`AbyssRaces.cs:368` | 看各地图登记表里有没有已生成的魅魔（登记表或基因变化时重算）；原来的只增计数仍保留在前面 |
| P12 神兽血裔桥 | `MythicBridge.cs:31-34,259-276` | 组件缓存；WillEat 拆成两个强类型补丁，不再用 `object[] __args` |
| P13 巨魔 `__args` | `Troll.cs:221-235` | TakeDamage / Kill 按位置绑定参数 |
| P14 CurStage | `MagicItems.cs:120-140`、`Gameplay.cs:45-48`、`ForceDragon.cs:216-226`、`Classes2.cs:165-170`（VPE） | 扩展按定义缓存；条件、导师系数、龙鳞 / 代谢同一刻只算一次（龙龄另按 GeneVersion 失效） |
| P15 组件 / 记录 | 见 R3；`AscensionPaths.cs:536`、`Pacts.cs:167-168` | 记录钩子先看记录类型；Pacts.Of 先看计数；ById 不用 LINQ |
| P16 每一击 | `Paladin.cs:23-38`、`Monk.cs:33-35`（VPE）、`AdventurerFeats.cs:369-371` | 静态 DefRef；Enmity 不用 LINQ |
| P17 周期扫描 / 平方级想法 | `LegendaryMonsters.cs:173-175`、`PortalEvent.cs:207`、`AdventurerFeats.cs:172`、`MagicApparel.cs:172-205`、`Ascension.cs:316-320`、`FaithRaces.cs:159-167` | 护符用 for 循环；没有 rival 方的位面直接返回；只看人形；食人魔皮先比距离、后判断敌对；两个缓存只清理过期条目；神火 / 新提夫林同一地图同一刻只算一次 |
| P18 小项 | `Amphibious.cs:18-35`、`Faith.cs:866`、`Demigod.cs:365`、`Loxodon.cs:40-48`、`ItemsPotions.cs:21-23`、`ConstructColonists.cs:30-40`、`ChromaticDragons.cs`（空列表不 ToList）、`DragonFlyover.cs:375,378`、`Marilith.cs:131-133`、`Faith.cs:101,245`、`Twilight.cs:39-40`、`Demigod.cs:375`（JusticeReveal） | 预先算好定义集合；尸体想法 / 恐惧与裸体想法 / 药水按定义记住；合并后的配方列表缓存；调换判断顺序；神明查找不用 LINQ |
| P19 界面（帧率） | `AdventurerUI.cs`（10 处悬停说明只在鼠标停在上面时才拼）、`Demigod.cs:107`、`Items.cs:117,126-129`（结界格子缓存） | 同左 |

### 两个 bug
1. 失去冒险者状态后专长缓存残留：`Hediff_Adventurer.PostRemoved` 里调用 `HeroTraits.Invalidate` 和 `Feats2.Sync`（`Adventurer.cs:543-546`）。
2. 渲染 / 属性线程可能写入的字典改为线程安全：
   - `Dragons.colorCache`（`ChromaticDragons.cs:40-42`）和 `Monsters.cache`（`LegendaryMonsters.cs:22-24`）改为 ConcurrentDictionary.GetOrAdd。
   - 子种族的 Def 字典改为 DefCache；邻近计数缓存加锁（`CommonRaceSubraces.cs:40-55`）。

### 点心 LP_FeatSnack 的配置错误（单列）
- 问题：`Defs/Adventurer_Feats2.xml:381` 的父类 ResourceBase 把 tickerType 设为 Never，可是这个物品带 CompRottable。结果是游戏里报红字配置错误，全量自测停在第 34 条。
- 修法：按 1.58.1 的原意（描述和 `daysToRotStart` 4 天都说明它会腐烂），加上 `<tickerType>Rare</tickerType>`，保留腐烂组件。
- 验证：`-Only Feats2` 20 条 PASS；全量自测越过了第 34 条，停在第 925 条，原因见第四节。

### 性能自检（新）
- `Source/PerfChecks.cs`，`Test.ps1 -Only Perf`：
  - 人口：100 个普通殖民者对比 100 人 DND 混合人口（40 个冒险者各带 2 个专长、30 个 LP 异种型、10 个深渊仆从、20 个强力 / 背景特性小人，一半持枪）。
  - 计时：固定种子，关掉叙事者，预热两轮，之后交替跑 5 轮，每轮 1000 刻，取中位数。
  - 微基准：BodySize、HealthScale、衰老、部位生命、射程、命中、索敌、威胁、禁用、进食、技能、可达性、8 项清缓存后的属性、ThingID、堆叠、能力 CanCast / GizmoDisabled，分别测普通小人、冒险者、DND 种族各一次。
  - 输出：每次调用的纳秒数和分配字节写进 `[LICH-PERF]` 日志行，并写到存档目录的 `LichPerf.txt`。只记录，不按阈值判失败。
- `Bridges/VPE/Source/PerfChecks.cs`，`-VPE -Only Perf`：在上面的基础上给 32 人加职业路径（8 条路径各 4 人），另外测职业技能经验、IsEnabledForPawn、守护者、职业光环刷新。

## 三、为了性能做的“等价”取舍（请知悉）
1. `Races124.PawnsNear`：快照只收调用时在半径内的人。遍历途中才从半径外进来的人不会出现，原来会。原来遍历中途被杀、被推走的人照样会再检查一次，这一点不变。所有调用方都只是挑目标或施加效果，实际效果不变。
2. 按刻记忆（同一刻内与原来逐位相同，跨刻立即更新）：
   - 魔法物品的阶段（弓、无甲、黑暗、厚岩顶、元素）。
   - 学者的导师系数（主人变了立即更新）。
   - 龙龄的龙鳞 / 代谢（基因变了立即更新）。
   - 神火、新提夫林这两个想法。

   暂停时这些值停在暂停那一刻，和原版属性缓存的行为一样。
3. 按定义记住的判断（结果只取决于定义，定义在游戏中不变）：是不是弓、是不是药水、地形是不是泥沼、配方是不是机械 / 锻造、想法种类、ScaledHediff / 魔法物品的扩展、合并后的配方列表（原版两边的列表没换就复用）。
4. 登记表（冒险者、巫妖、死灵造物、半神、龙龄、化身容器、暗影之腿、魅魔）：
   - 依赖原版在添加 / 移除状态时调用 PostAdd / PostRemoved。原版只有生成小人时的 `HediffSet.Clear` 不调用 PostRemoved，那时这些状态还没加上，所以不受影响。
   - 如果别的模组绕过 RemoveHediff、直接从 `hediffs` 列表里删掉冒险者等状态，登记会残留到下一次访问。这是理论上的风险，实测没有遇到。

## 四、测试结果
- **通过：**
  - `-Only Adventurer,Feats2,HeroTraits,Growth`：181 条
  - `Legendary,ChromaticDragons,LegendaryMonsters`：53 条
  - `Races124`：476 条（第一次随机失败在已知的 smite 不稳定项，重跑通过）
  - `Twilight,Vessels`：174 条（包括提尔审判之庭）
  - `Items,MagicItems,MagicApparel,Satiety,ForceDragon,Troll,Marilith`：777 条
  - `Abyss,Pact`：139 条
  - `Faith,Ascension`：32 条
  - `Ideology`：15 条
  - `CommonRaces`：25 条
  - `CommonRaces116`：70 条
  - `GiantKinHAR`：14 条
  - `Fiends124`：34 条
  - `Genie124`：35 条
  - `Artworks`：6 条（单独跑两次都通过）
  - `Feats2`：20 条（点心修复后）
  - `Perf`、`AbyssPerformance`、`PactPerformance`、`-VPE -Only Perf` 都 PASS
  - `-VPE` 全量：482 条
  - `-Mythic -Only MythicBridge`：113 条
  - `Giants`：33 条（新 DLL 单独跑 5 次，3 次通过）
- **已确认是修改前就有的问题**（换回 1.58.5 的 DLL 复现，失败位置相同）：
  - `Events`：第 86 条“migration group arrives: ember”，每次都失败。
  - `Netheril`：第 22 项，记忆里记录的老失败。
  - `SharedComponents`：第 42 条“bridge xenotype halves the relation mood”，旧 DLL 也每次失败；另有一次失败在第 32 条的冷却。
  - `Giants`：ordning 检查随机失败。原因是开局地图上随机出现了第三个殖民地巨人（诊断结果 `cg=3`）；旧 DLL 三次里失败一次。
  - `Genie124,Artworks,Fiends124` 一起跑时，小血肉艺术品可能做出“极差”品质，美观 −5，检查失败；旧 DLL 三次里失败两次，另一次是神灯检查失败。单独跑 Artworks 时会通过。
  - `KnowledgeFeedback`、`ConstructDesign` 单独 `-Only` 时会越界或失败（依赖全量自检里的前置对象），旧 DLL 相同。
  - 全量自测（不带 -Only）：修掉点心之后停在第 925 条“the LP_HalfOrcScenario raid lands on time”，旧 DLL 两次也停在同一条。按指示没有深挖。
- 定位 Giants 和 Artworks 时临时加的诊断文字已经撤掉；这两个测试文件与备份逐行一致。

## 五、没做的（以及原因）
- 审计第五节里 4 项会改变行为的优化：PawnsNear 改按距离排序、仪式 FailOn 降频、CallDragons 节流、RacialRelation 改键（需要迁移存档格式）。
- 会在同一刻内改变结果、或者收益太小的几项：
  - P15 法杖 `Available()` 按刻记忆：心灵熵在同一刻内会变。
  - P10c 艾恩石：GetComp 的开销和替代写法差不多。
  - P18 中 MindResist 的心情缓存：测试里会在同一刻内改抗性。
  - 六臂和熔核禁卫的 CurStage：受伤后要在同一刻内生效。
  - 巨魔 EnsureMetabolism 挪到门槛之后：会推迟补上代谢状态。
  - 龙宝库搬运的 LINQ、`Hediff_GeneLinked` / `Hediff_PactSigner` 的 ShouldRemove 进一步缓存：契约查找已经去掉了 LINQ。
  - 界面里的 PawnById（真名卷轴、卡苏斯检视）、祭坛 gizmo、`ColonyMaxLevel`：只在选中时每帧调用。
- 神灯热修新加的三个 Tick 前缀（`Genie.cs:275-278`）：第一个判断就是 `p.Spawned`（一次字段比较），地图上的小人立即返回，不需要再加门槛。已核对，没改。
- 战斗场景计时：敌我交战有死亡，不确定性太大，没有加进 -Only Perf；战斗相关的收益只有微基准的数据。
