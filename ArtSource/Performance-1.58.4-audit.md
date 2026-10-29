# DND边缘大陆 1.58.4 性能审计（第一阶段：只读）

> 日期：2026-10-27。设备版本 1.58.3。本阶段**没有改任何源码 / DLL，没有运行 Build.ps1 / Test.ps1，没有启动游戏**。
> 范围：`Source/*.cs`（非 *Checks / *Gallery，约 2.7 MB）和 `Bridges/*/Source/*.cs`。HAR / Ideology / Isekai / Jewelry 桥没有运行时 C#。
> 方法：按系统分 6 组逐文件阅读，每条都回到源码核对过（引用的代码就是当前文件里的原文）。原版的调用路径用 IL 反查过：Assembly-CSharp、VEF.dll、VanillaPsycastsExpanded.dll，只读元数据。
> 下面的调用频率都是估计值，没有实测。“确认”指代码和调用路径都核对过；“待验证”指原版调用频率还没在 1.6/1.7 里确认。
> 修改原则（第二阶段）：**行为必须不变**。每条修法都标了是否“完全等价”；不完全等价的给了等价替代方案。

---

## 〇、先说结论：五个共用的根因

大部分热点不是某个系统写得特别差，而是下面 5 个共用的写法出现在了热路径上。第二阶段先修这些公共基础，收益覆盖面最大：

| # | 根因 | 位置 | 影响面 |
|---|---|---|---|
| R1 | **按字符串查 Def**：`Items.Has(p,"LP_X")`、`Items.Worn(p,"LP_X")`、`Races124.Has(p,"LP_X")`、`Subraces.Has`。每次调用都要对整串字符串做哈希（ConcurrentDictionary），再线性扫一遍 hediff / 服装 / 基因。**查不到的 Def 不缓存**：`Items.cs:15-16`、`Races124.cs:17` 的写法是 `if(!TryGetValue || d==null) d=GetNamedSilentFail(...)`，没装 HAR 时 HAR 专属基因每次都要再查一次 DefDatabase，再写一次字典（要拿锁）。`Races124.Has` 先查 Def 再判断 `p.genes==null`，所以动物也要付这笔开销 | Items.cs:15-18,72；Races124.cs:17,20；CommonRaceSubraces.cs:21-23 | 几乎所有补丁 |
| R2 | **`Races124.PawnsNear` 每次调用都复制整张地图的小人列表**：`foreach(var p in map.mapPawns.AllPawnsSpawned.ToList())…yield return p` | Races124.cs:41-43 | 约 40 个调用点，其中几个在每次能力判断、每次受伤时调用 |
| R3 | **组件查找不缓存**：`Map/Game/World.GetComponent<T>()` 是对组件列表的线性 `is T` 扫描，大模组列表下有 50～150 个组件。Faith.Comp、Ascension.Tracker、Items.MapState、MythicBridge.State、Classes.State、MapComponent_Legendary 等每次都重新查 | 见下文各条 | 路径步、堆叠、光照属性、射击 |
| R4 | **`Adventurer.Get(p)` 每次都线性扫 hediff 列表**（Adventurer.cs:33），而 `Feats.Has`、`Epic.Has`、`PartHealthFactor`、`LearnFactor` 都经过它。1.58.1 以后几乎人人都有起源专长，`feats.Count>0` 这个短路已经起不到作用 | Adventurer.cs:33；AdventurerFeats.cs:85；AdventurerEpic.cs:34 | 射程、命中、学习、部位血量、衰老 |
| R5 | **每次都重算的 CurStage / ShouldRemove**：原版 `StatWorker.GetValueUnfinalized` 每次属性计算会对**每个 hediff 读两次 CurStage**（IL 已确认）。几个 hediff 的 CurStage 里有 `ModsConfig.IsActive`（每次都 `ToLower`）、`GetStatValue`、基因扫描或 `GetModExtension` | Adventurer.cs:472；MagicItems.cs:123；Gameplay.cs:33；ForceDragon.cs:214；Marilith.cs:142；Classes2.cs:166 | 带这些状态的小人的每一次属性计算 |

---

## 一、发现（按对 TPS 的影响从高到低）

> 表头：**位置**、**频率**、**问题**、**修法**、**风险**。“等价”指结果和现在完全一样。

### P1【高】魔鬼之眼的 5 个前缀在 `Pawn.Tick` 和索敌里对每个小人提前计算 `Fiends.Sees`
- **位置**：`Fiends.cs:142-167`（5 对 Prefix + Finalizer），`Fiends.cs:106`（`Sees`）。
- **频率**：`Pawn.Tick` 是**每个已生成小人每 tick 一次**（动物也算）；`Verb.CanHitTargetFrom` 在索敌时按候选目标 × 武器调用，大战里每 tick 可达数千次；另有 `BestAttackTarget`、`FindBestReachableMeleeTarget`、`TryCastNextBurstShot`。
- **问题**：
  ```cs
  static void Prefix(Pawn __instance,out Pawn __state){__state=Fiends.seer;if(Fiends.Sees(__instance))Fiends.seer=__instance;}
  public static bool Sees(Pawn p)=>p!=null && (Races124.Has(p,"LP_IceDevilSight") || Twilight.SeesInvisible(p) || Outsiders.Of(p)?.plane==OutsiderPlane.Celestia || Feats2.Observer(p));
  ```
  每次调用的开销：一次字符串字典查找、一次基因扫描、一次 `GetTrait`、一次 `Outsiders.Of` 字典查找，再加一次 `[ThreadStatic]` 读写（Mono 上较慢）和 Harmony 的 finalizer / try 包装。可这个结果**只在** `DevilSightInvisibility`（:170）里用到，而且只在 `__result==true` 时，也就是目标真的隐身时，这非常少见。估计 300 个小人时，光 `Pawn.Tick` 一项每 tick 就要 50～100 µs，大战里加上 `CanHitTargetFrom` 每 tick 约 0.5～1 ms。
- **修法（等价，改为惰性求值）**：把 `seer` 换成一个 ThreadStatic 的“行动者栈”（`Pawn[]` + `depth`）。前缀只做压栈（`stack[depth++]=p; __state=depth-1`），finalizer 恢复 `depth=__state`。把 `Sees` 判断挪进 `IsPsychologicallyInvisible` 的后缀：`if(__result) for(i<depth) if(Fiends.Sees(stack[i])){__result=false;break;}`。现在的语义是“嵌套调用链上有任何一个小人在它的前缀运行时满足 Sees，seer 就不为空”，栈的写法与此完全一致。可以进一步加一个全局“可能有看破者”的计数作为门槛，但需要失效钩子，先不做。
- **风险**：低。唯一的区别是 Sees 的求值时机从前缀入口移到了隐身判断时，两者都在同一个调用栈里。**确认**（频率是估计值）。

### P2【高】`Races124.PawnsNear` 整表复制，以及挂在它上面的三个热调用
- **位置**：`Races124.cs:41-43`（复制）。热调用方：
  1. `TwilightDeities.cs:356-359`：原版 `Ability.GizmoDisabled` / `get_CanCast` 后缀 → `TwilightSkills.Silenced`（:252-256）→ `PawnsNear`。**每个小人的每个能力**，在每次 GUI 事件（选中时）和 AI 判断能否施放时都会调用；`Silenced` 没有任何廉价的提前返回。
  2. `Bridges/VPE/Source/VPEBridge.cs:27-30`：VEF `Ability.IsEnabledForPawn` 后缀，同样走 `Silenced`。VEF 自己的 `Pawn.TryGetAttackVerb` 补丁会对每个非玩家控制施法者的每个自动施放能力调用 IsEnabledForPawn（IL 已确认），而 `TryGetAttackVerb` 在 AI 战斗思考、`TryStartAttack`、`PatherTick` 里都会被调用。例：10 个敌方施法者 × 5 个能力，每轮 AI 评估就是约 50 次整表复制。
  3. `Bridges/VPE/Source/Abilities.cs:169-179`：`GuardStanceShare`（`Pawn.PreApplyDamage` 前缀）。**每一次有阵营的小人受到外部伤害**都调用 `Guardian` → `PawnsNear`，没有“有没有人开着守护姿态”的门槛。
  - 其他周期调用方（每 60～250 tick）：寒冰魔寒气光环（`Fiends.cs:39`）、炎魔 `AICanTargetNow`（`Races124.cs:99`）、龙 / 传奇怪 AI（`Legendary.EnemiesNear`、`Monsters.Foes`；贝希尔每 60 tick 对 1.9 格半径做一次全图复制）、`MapComponent_ClassAuras`（`Classes2.cs:146`）、泰坦、风暴巨人等，共约 40 处。
- **问题**：每次调用都分配一个 200～400 项的新列表并遍历全部，即使半径只有 1.5～12 格。提尔的神力灌注者几乎从不存在，所以上面 1、2 两处的计算几乎 100% 是浪费。
- **修法**：
  - (a) **等价**：在 `PawnsNear` 内部不复制，直接用下标遍历 `AllPawnsSpawned`，先把符合条件的小人收进一个小列表再逐个 yield。快照语义和顺序都不变，分配从整表降到命中的几个。
  - (b) **等价**：`Silenced`、`Guardian` 等只读、只要布尔值或第一个结果的调用方，改用不分配的直接下标循环，过滤顺序不变，所以选中的守护者也一样。
  - (c) **等价门槛**：维护一个活着的 `Hediff_DivineVessel`（key=="Tyr"）登记表，在 PostAdd / PostLoadInit 加入，PostRemoved 移除，`Silenced` 在登记表为空时直接返回 false。`LP_GuardStance` 同理，加一个存活计数（它是普通 `HediffWithComps`，需要一个小的计数 comp 或自定义类）。计数只可能多算、不可能少算，多算时会退回到原来的精确判断。
  - (d) 不等价，只作备选：用 `GenRadial` 按格扫描，结果会变成按距离排序，`FirstOrDefault` 类调用方可能挑中不同的目标。第二阶段**不采用**。
- **风险**：(a)(b) 极低；(c) 低到中（读档后要重建，已有检查 `EventChecks.cs:447`“提尔审判之庭”覆盖）。**确认**。

### P3【高】冒险者状态的 `CurStage` 每次都调用 `ModsConfig.IsActive`
- **位置**：`Adventurer.cs:472` → `EffectScale`（:44）→ `EffectsHandedToIsekai`（:43）→ `IsekaiActive=>ModsConfig.IsActive(IsekaiId)`（:42）。
- **频率**：每个冒险者（13 岁以上的人形，包括 NPC）每次属性计算**至少 2 次**；疼痛、能力、饥饿等原版遍历 hediff 读 CurStage 时也会触发。中等殖民地每 tick 上千次。
- **问题**：`ModsConfig.IsActive(string)` 的实现是 `activeModsHashSet.Contains(id.ToLower())`（IL 已确认），每次都要对 28 个字符做 `ToLower`，在 Mono 上很可能还会分配新字符串，然后再做一次哈希。同一个 `EffectScale` 还在 `LearnFactor`（P10）、`PartHealthFactor`（P5）、`ClassPowerBonus` 里被调用。同类写法：`Epic.IkmActive`（AdventurerEpic.cs:38，击杀路径）、`HeroDestiny.VPEActive`（HeroDestiny.cs:21）。
- **修法（等价）**：游戏运行期间激活的模组列表不会变，所以缓存一次：`static bool? isekai; IsekaiActive=>isekai??=ModsConfig.IsActive(IsekaiId);`。`IkmActive` 同样处理；`VPEActive` 要保留前面的 `forceVPE??` 测试覆盖。
- **风险**：无（检查代码是直接读 `ModsConfig` 的，见 AdventurerChecks.cs:167）。**确认**（Mono 的 ToLower 是否分配待 profiler 确认）。

### P4【高】`Pawn.get_BodySize` 上的后缀叠加（每个小人都付）
`BodySize` 是原版最热的 getter 之一：弹道拦截每格每人、命中报告、目标打分、饥饿、MaxNutrition、负重、MassUtility、市场价值都用它。
- **4a `SmallBody.cs:31-36, 53-60, 42-45`**：每次人形调用都对 **24 个**带 `SmallBodyExtension` 的基因定义各做一次 `HasActiveGene`，约 24 × 基因数 ≈ 500～1000 次迭代。根节点渲染缩放（每帧）和负重 StatPart 也走同一个循环。
  - 修法（等价）：一次性建 `Dictionary<GeneDef,SmallBodyExtension>`，沿用 `Genes` 的 DefCount 重建规则，只遍历一遍小人的基因列表。用一个小的“已乘过”表保持“每个 def 只算一次”的语义。复杂度从 O(24·G) 降到 O(G)。
- **4b `ConstructHuman.cs:67-70`**：`var h=Necro.Construct(__instance);if(h!=null && __instance.def==ThingDefOf.Human)…h.LegacyRace…`。**每个小人**（动物、机械体都算）都先扫一遍 hediff，才判断是不是人类；对构装体还要调用 `LegacyRace=>Necro.Def<ThingDef>(kind)` → `DefDatabase.GetNamed("LP_"+kind)`（Core.cs:45、ConstructHuman.cs:16），**每次 BodySize 都拼接一次字符串**。`ConstructDesign.cs:76-77`（HealthScale）同理。
  - 修法（等价）：先 `if(__instance.def!=ThingDefOf.Human)return;`；`LegacyRace` 缓存到一个不存档的字段（kind 变化时重置）。可选：给 `Hediff_Construct` 加存活计数作为门槛。
- **4c `ForceDragon.cs:254-257`**：`ForceDragon.Age(__instance)` 对**每个小人**扫 hediff 找 `Hediff_DragonAge`。
  - 修法（等价）：先 `if(__instance.genes==null)return;`。DragonAge 在没有心脏基因时会自己移除（ShouldRemove），没有基因的小人不可能带着它。
- **4d `FireGiant.cs:96-99`**：没有提前返回，每次都做 `GiantKin.Of`（ConditionalWeakTable，Mono 上要加锁）和巨魔基因扫描。动物因为 `genes==null` 会很快返回。
  - 修法（等价）：在 `TrollAppearance.Size` 里用缓存的 GeneDef；GiantKin 已有缓存，可以接受。
- **4e**：6 个 `PawnRenderNodeWorker.ScaleFor` 后缀（SmallBody:57、ForceDragon:258、FireGiant:100、ChromaticDragons:675、Marilith:164、ItemsPotions:110）每个节点每帧都比较一次字符串 `tagDef?.defName=="Root"`。改为和 `PawnRenderNodeTagDefOf.Root` 做引用比较（等价）。只影响帧率。
- **风险**：低。**确认**。

### P5【高】`Pawn.get_HealthScale` 与 `PartHealth.Maximum`（部位血量）叠加
`HealthScale` 被 `BodyPartDef.GetMaxHealth` 读取，所以每次能力重算的每个部位、疼痛、流血、致死判定、伤害结算都会读它；健康页打开时每帧每部位读一次。
- **5a `ChromaticDragons.cs:671`**：
  ```cs
  if(__instance.health?.hediffSet!=null && __instance.RaceProps.Humanlike)foreach(var c in Dragons.Colors)if(Items.Has(__instance,"LP_DragonBlood_"+c)){__result*=1.05f;break;}
  ```
  每次人形调用都**分配 5 个新字符串**，再做 5 次字典查找和 5 次 `HasHediff` 扫描（几乎没人有龙血，所以 5 次都会落空）。修法（等价）：一次性解析成 `static HediffDef[5]`，只扫一遍 hediff。
- **5b `NetherilQuest.cs:529-530`** `LarlochAvatarHealth`：每个小人都做一次 `Items.Has(__instance,"LP_LarlochAvatar")`。修法：缓存 HediffDef，或加化身存活计数。
- **5c `ConstructDesign.cs:76-77`**：同 4b。
- **5d `LimbHealth.cs:38-50`** `PartHealth.Maximum`（转译进 GetPartHealth、CalculatePartEfficiency、健康卡等）：每个部位每次调用要做 4～5 次列表扫描，依次是 `GetMaxHealth`（走上面的 HealthScale）、`TemperingCompatibility.Apply`、`Adventurer.PartHealthFactor`（`Adventurer.cs:213`：`Get` 扫描 + `Legendary.Is` 两次字典查找 + P3 的 ModsConfig + 约 3 次字典查找）、`Subraces.PartHealthFactor`（按字符串查基因），最后是它自己的扫描。一个身体约 50 个部位。
  - 修法：在 `Hediff_Adventurer` 上缓存 `PartHealthFactor`（在 `Dirty()` 里清空；`forcedScale` 与 `Affinity.forcedOn` 这两个测试钩子要绕过缓存或并入缓存键）。其余因子按小人按 tick 记忆。
- **风险**：5a～5c 低；5d 中（多个系统都参与，记忆必须在同一 tick 内升级时失效）。**确认**（HealthScale 的调用频率待验证）。

### P6【高】`Pawn_AgeTracker.get_BiologicalTicksPerTick` 上有 3 个后缀，每个小人每 tick 扫 3 遍 hediff
- **位置**：
  - `AdventurerEpic.cs:251-252` `EpicAgeless`：`Epic.Has(___pawn,Epic.Immortal)` → `Adventurer.Get` 扫描
  - `Demigod.cs:324-325` `DemigodAgelessHook`：`Demigods.Is` 扫描
  - `NetherilAscension.cs:237-238` `DemigodAgeless`：`Items.Has(p,"LP_ArcaneDemigod")`，字符串查找 + 扫描
- **频率**：`Pawn.TickInterval → AgeTickInterval → TickBiologicalAge → get_BiologicalTicksPerTick`（IL 已确认），已生成小人几乎每 tick 一次，**包括所有成年动物**（`Adult` 只比较 TicksToAdulthood）。
- **问题**：持有不朽之恩 / 神性 / 奥术半神的小人每局只有 0～3 个，但所有成年小人每 tick 都要付 3 次全表扫描。
- **修法（等价）**：给三种状态各加一个静态存活计数或超集 HashSet（在 PostAdd、`ExposeData` 的 PostLoadInit、AddBoon 时加，PostRemoved 时减）。后缀开头先查 `Live==0 || !set.Contains(id)` 就返回，后面保留现有的精确判断。可以把三个后缀合并成一个。
- **风险**：低（集合只会多算；检查代码直接 `h.boons.Clear()` 只会留下多余的条目，没有害处）。**确认**。

### P7【中高】暗影步行 `ShadowWalking.Active` 在寻路 / 可达性 / 门判断里按 defName 字符串比较 hediff
- **位置**：`ShadowWalking.cs:81-126` 的补丁 → `ShadowWalking.Active` → `FleshImplants.Has(p,"ShadowLeg")`（`FleshImplants.cs:9-12`）：
  ```cs
  string full=name switch {"ShadowLeg"=>"LP_ShadowLeg",…};
  for(…)if(h.def.defName==full && …)return true;
  ```
- **频率**：
  - `Reachability.CanReach`：只在 `__result && peMode!=OnCell` 时跳过，所以每次失败的或 OnCell 的 CanReach 都要扫描（工作、食物、搬运扫描里到处都是）。
  - `Building_Door.CanPhysicallyPass`：没有任何提前返回（待验证：区域 BFS 是否会走到这里）。
  - `GenClosest.ClosestThingReachable`：每次调用。
  - `PawnCanOccupy`、`CostToMoveIntoCell`、`BuildingBlockingNextPathCell`、`NextCellDoorToWaitForOrManuallyOpen`：每个移动中小人的每一步。
- **问题**：每次调用都对每个 hediff 做字符串相等比较，而几乎没人装了暗影之腿。
- **修法**：
  - (a) 等价：用缓存的 `HediffDef` 做引用比较。
  - (b) 收益更大：维护全局 `ShadowWalking.Count`（`HediffComp_ShadowLeg` 加入 / 移除时增减，读档时重数），所有钩子先判断 `Count==0` 就返回。
- **风险**：(a) 无；(b) 低（漏减只会多做功，不会出错）。**确认**。

### P8【中高】射程 / 命中 / 威胁判断这一组热点（战斗 AI）
- **8a `AdventurerFeats.cs:311-316`** `FeatRange`（`VerbProperties.AdjustedRange`，经由 `Verb.EffectiveRange`，按候选目标、预热、视线、射程圈调用）：每个持枪人形每次调用都执行 `Feats.Has(p,Sharpshooter)` → `Adventurer.Get` 扫描。
  - 修法：把 `Feats.Has` 做便宜（R4）。方案 a：在 AddFeat 和读档时维护 `Dictionary<LP_FeatDef,HashSet<int>>` 超集，Has 先查集合再做原判断，等价。方案 b：在 `HeroInfo` 缓存里加专长标志位。
- **8b `MagicItemsCombat.cs:158-164`** `EagleEyesRange`（同一个 AdjustedRange）：穿任何衣服的远程小人都要付一次 `Items.Worn(p,"LP_EyesEagle")` 的字符串哈希加服装扫描。修法（等价）：缓存 ThingDef；`Items.Worn` 在没穿衣服时先返回。
- **8c `ItemsRaceGear.cs:171-174`** `BoneTalismanThreat`（`Pawn.ThreatDisabled`，按“搜索者 × 候选目标”对调用，战斗中每秒数千次）：
  ```cs
  if(!__result && __0?.Thing is Pawn searcher && searcher.HostileTo(__instance) && RaceGear.UndeadIgnore(__instance,searcher))__result=true;
  ```
  先做了最贵的 `HostileTo`。修法（等价）：改成从便宜到贵的顺序，先查目标是否戴了骨质护符（缓存 def），再查搜索者是不是构装体，最后才 `HostileTo`。
- **8d `MagicApparel.cs:304-305`** `RangerWildAnimals`（同一个 ThreatDisabled）：野生动物作为搜索者时，对每个目标都做 `SetHediff(__instance,"LP_Set_Ranger")`（字符串加扫描），包括没穿衣服的目标。修法（等价）：先判断目标服装为空就返回，再用缓存的 def。
- **8e `ShotReport.HitReportFor` 共 7 个后缀**：
  - `AdventurerFeats.cs:319-339`：`Feats.Has` 扫描
  - `MagicItemsCombat.cs:139-156`：3 次 Worn 加 1 次 Has
  - `MagicApparel.cs:231-235`：2 次 Worn
  - `ItemsRaceGear.cs:132-136`、`ItemsWondrous.cs:65-69`：各 1 次
  - `CommonRaceSubraceCombat.cs:13-27`：4～6 次 `Subraces.Has` 字符串查找加基因扫描
  - `HeroTraitsCombat.cs:255`：这一个没问题

  单次命中报告约 8 次字符串字典查找、7 次服装扫描、5 次基因扫描。频率：每发子弹、征召悬停每帧（IL 确认 `GetShootingTargetScore` 不调用它）。修法（等价）：用缓存的 def 静态字段，服装为空时先返回；子种族改为按小人的基因标志位掩码缓存（在 `Notify_GenesChanged` 里清空）。
- **8f `AdventurerFeats.cs:344-348, 174-181`** 哨兵 `TryEnterNextPathCell`：只要有任何哨兵（袭击敌人也会掷到这个专长），**每个移动的小人每一步**都要做一次 `Has(mover,Mobile)` 扫描和一次 `sentinels.ToList()` 分配。修法（等价）：先用便宜的地图 / 相邻判断，只有某个哨兵真会出手时才查 Mobile；用复用的列表下标遍历。
- **风险**：都低。**确认**。

### P9【中高】热门属性上的 StatPart：每次属性计算都有分配或扫描
属性缓存时间短，WorkSpeedGlobal / GeneralLaborSpeed 在做工时约每 tick 读一次，MoveSpeed 每走一格读一次（待验证 1.7 的 `cacheStaleAfterTicks`）。
- **9a `Demigod.cs:280-306`** `StatPart_Ascension.Factor`（挂在 10 个属性上：MoveSpeed、WorkSpeedGlobal、PsychicSensitivity、ShootingAccuracyPawn 等，动物也算）：里面有一个捕获参数 `p` 的 lambda `d=>d.HostileTo(p)`。C# 规则下闭包对象在**方法入口**就分配，所以对所有属性、所有小人，**每次计算都分配一次**，哪怕只有 "aim" 分支用到它。"move" 分支还对每个小人做 `Demigods.Key(p)` 扫描和 `Items.Has(p,"LP_CallWindHaste")`。修法（等价）：把 lambda 移进单独的静态辅助方法，并用 `Demigods.active.Count>0` 作门槛；HediffDef 缓存到静态字段。
- **9b `Faith.cs:690-729`** `StatPart_Faith`（7 个属性）：在任何提前返回之前就做了 `Find.World.GetComponent`（R3）；不分属性都先算 `DevoteeGod(p)`（`DegreeToDeity` 是带闭包的 LINQ，信徒每次都分配），而只有 work / aim 两个分支用到它；还有多次字符串圣物查找。修法（等价）：缓存组件；把求神明的代码挪进 work / aim 分支；`DegreeToDeity` 改成按 degree 索引的数组（保持“取第一个匹配”的语义）。
- **9c `Genasi.cs:84-104`** `StatPart_ElementalFocus`（MoveSpeed / MiningSpeed / Flammability）：先算 `Genasi.Element(p)`（4 次 `"LP_Element"+e` 字符串拼接、4 次字典查找、4 次基因扫描），再判断是哪个属性、有没有戴元素符（又拼接一次字符串）。**每个穿衣小人每次算移速约 5～6 次字符串分配加 4 次基因扫描。** 修法（等价）：先按 stat 分支，只查与该属性相关的那一个符（缓存 ThingDef），戴了才求元素。`CloakFactor` 用缓存的 def。
- **9d 没装 HAR 时查不到的基因**（R1）：
  - `StatPart_ColdBlood`（Marilith.cs:128,169，挂在 MoveSpeed 上，所有小人）
  - `StatPart_CyclopsSmith`（HARGiants.cs:182,200，GeneralLaborSpeed，每个做工 tick）
  - `StatPart_CyclopsSight`（ShootingAccuracyPawn）
  - `StatPart_EttinWatch`（RestFallRateFactor）

  每次计算：一次 ConcurrentDictionary 落空、一次 `GetNamedSilentFail`、一次写入（加锁）。修法（等价）：查不到的结果也缓存（带 `DefCount` 戳，这样测试运行时注册的基因仍能解析）；`Races124.Has` 先判断 `p.genes==null`；这几个 StatPart 用静态 GeneDef 字段，为 null 时直接返回。
- **9e `CommonRaceSubraceCombat.cs:123-126`** `StatPart_HobgoblinNumbers`（WorkSpeedGlobal），以及 `HobCount`、`PackCount`、`BigFriendNear`：传给 `Cached` 的是捕获参数的 lambda，**缓存命中时也会分配**。修法（等价）：先 `TryGetValue`，未命中才构造闭包。
- **9f `AdventurerFeats2.cs:286-295`** `StatPart_GnomishCunning`（GeneralLaborSpeed）：做工的小人每 tick 都要做一次 `DefDatabase.GetNamedSilentFail("LP_Feat_GnomishCunning")`、一次 LINQ、一次扫描。修法（等价）：def 用静态字段；`Mechanical(recipe)` 按 RecipeDef 记忆。
- **9g `StormGiant.cs:37-39`** `StatPart_StormChild`：先查基因、后查天气，天气还要做一次 `defName` 字符串哈希。修法（等价）：把顺序换成 Spawned → 天气（`HashSet<WeatherDef>`）→ 基因。
- **风险**：都低。**确认**。

### P10【中】`Pawn_SkillTracker.Learn`（做工时每个小人每 tick 一次）上的几个钩子
- **10a `Bridges/VPE/Source/VPEBridge.cs:146-158`** `ClassExperience`：**每个小人**每次获得技能经验都要执行一次 `Classes.Psycasts(p)`，这是 VPE 的 `GetFirstHediffOfDef`，线性扫描。有职业的人还要做一次 `Classes.State`（`Game.GetComponent`），到每日 50 点上限后也照做不误。修法（等价）：懒建一个 `HashSet<SkillDef>`，收录所有 `LP_ClassPathDef.requiredSkill/alternateSkill`，不在集合里的技能直接返回（建造、采矿、烹饪、医疗、艺术就零开销了）；缓存 ClassState。
- **10b `Adventurer.cs:727-730`** `AdventurerLearn`：建造、手工、采矿、烹饪、艺术这 5 项在 `Get` 之前就返回，这一点很好。另外 7 项每次要做 `Get` 扫描、P3 的 ModsConfig 检查和 AMod 计算。修法：P3 修好后大部分开销就没了；另把 6 个学习系数缓存在 hediff 里，随 stage 一起重建，并在 `Get` 之前先判断 `EffectScale<=0`。
- **10c `ItemsWondrous.cs:91-94`** `IounLearning`：对每件穿着的衣物做一次 `GetComp<CompIounStone>()`。修法：先用 `IsIoun(def)` 判断（等价的前提是只有 `LP_IounStone` 带这个 comp，**需要核对 XML**）。
- **风险**：很低。**确认**。

### P11【中】`ForbidUtility.IsForbidden` 后缀：门槛计数只增不减
- **位置**：`AbyssRaces.cs:365-371`；计数在 `AbyssRaces.cs:170, 233, 235`，以及 `AbyssEvents.cs:162`。
- **频率**：每个工作扫描候选物各一次，中型殖民地每 tick 1k～10k+ 次。
- **问题**：`AbyssRaces.succubi<=0` 是本局全局的计数：`Gene_SuccubusDream.PostAdd`（任何生成过的魅魔，包括袭击者、世界小人）、每次读档的 PostLoadInit、伪装 hediff 都会让它增加，**从来不减**。所以只要世界上出现过一个魅魔，之后每次未被禁止的 IsForbidden 都要付一次 `Outsiders.Of` 字典查找（约 30 ns × 1 万 ≈ 每 tick 0.3 ms）。1.54.0 报告里写的“地图上从没出现过魅魔”并不准确。
- **修法（等价）**：在现有的 `OutsiderRegisterSpawn` / `OutsiderRegisterDespawn` 钩子里维护“**已生成并带有效 `Gene_SuccubusDream` 的小人数**”，`OutsiderGenesChanged` 时重算，`MapComponent_Outsiders.Rebuild` / FinalizeInit 时重建。`NearAltar` 只在 `IsSuccubus(pawn)` 时生效，而做工作的魅魔一定是已生成的。
- **风险**：低。**确认**。

### P12【中】神兽血裔桥：`CanStackWith` / `SplitOff` 线性查 GameComponent，`WillEat` 用 `object[] __args`（只影响装了 Mythic 的玩家）
- **位置**：
  - `Bridges/MythicBloodlines/Source/MythicBridge.cs:222-235`：`var s=MythicBridge.State;if(s==null || !s.AnyMeat)return;`，其中 `State=>Current.Game?.GetComponent<BridgeState>()`（:31）。“没记录过肉”的快速返回排在组件扫描**之后**。`CanStackWith` 是搬运、存储格评估、合并堆叠里最热的路径之一（IL 已确认调用方）。
  - `:260-270` `TrollBloodOnlyForTaotie`：`static void Postfix(object[] __args,…)`，Harmony 在补丁序言里每次都分配数组并装箱，这一步在 `!__result` 返回之前。WillEat 是食物搜索的验证器，每个候选食物调用一次。
- **修法（等价）**：
  - 缓存组件：在 `BridgeState(Game)` 构造里赋 `cached=this`，或以 `Current.Game` 引用作键。
  - WillEat 改为按下标绑定参数（`Pawn __0, object __1`；要确认 Harmony 2.3 接受 object 类型，否则写两个强类型后缀）。
  - `TrollBlood` 查不到时加一个“已查过”标志。
- **风险**：很低。**确认**。

### P13【中】`Thing.TakeDamage` 后缀 `TrollCorpseFire` 用 `object[] __args`，所有东西的每一次受伤都会分配
- **位置**：`Troll.cs:230-235`：`static void Postfix(Thing __instance,object[] __args){if(!(__instance is Corpse corpse) || !(__args[0] is DamageInfo dinfo)…`
- **频率**：所有 Thing 的每次 TakeDamage，包括子弹、近战、物体和植物着火时的火焰 tick、爆炸波及的每一格、老化损耗。Harmony 每次都分配 object[] 并把 DamageInfo 装箱。
- **修法（等价，零风险）**：改为 `static void Postfix(Thing __instance,DamageInfo dinfo)`。`ChromaticDragons.cs:686` 已经这样写了。`TrollDeath`（Troll.cs:223，Pawn.Kill）也用了 `__args`，但它很少触发，可以顺手一起改。

### P14【中】每次读取都重算的 CurStage（只影响带这些状态的小人，但每次属性计算都要读两次）
| 位置 | 每次读取时做的事 | 修法（按 tick 记忆，同一 tick 内等价） |
|---|---|---|
| `MagicItems.cs:123-138` `Hediff_MagicItem.CurStageIndex` | 只有 "waaagh" 有 120 tick 缓存；其余分支每次都算：bow 要做 `weaponTags.Any(lambda)`，每次分配；unarmored 要扫服装；thickRoof 要查屋顶网格；dark 要查光照网格；element 要做 P9c 的基因扫描；`Ext` 每次 `GetModExtension` | `if(cachedTick==now) return cachedIndex;`，`Ext` 缓存到字段 |
| `Gameplay.cs:33-44` `Hediff_Construct.CurStage` | 学者构装体：每次都 `Necro.Mentoring(master)` → `master.GetStatValue(PsychicSensitivity)`，就为了拼缓存键 | 导师值按 tick 记忆，或在 `Maintain()`（250 tick）里刷新 |
| `ForceDragon.cs:214-222` `Hediff_DragonAge.CurStage` | 两次基因表扫描（HasActiveGene 加代谢求和） | 按 tick 记忆 |
| `Marilith.cs:142-146` `Hediff_SixArms.CurStage` | 4 次 `CalculatePartEfficiency` | 按 tick 记忆 |
| `Classes2.cs:166-167` `Hediff_ClassScaled.CurStage`（保护光环覆盖范围内所有队友） | 每次 `GetModExtension` | 字段缓存（完全等价） |
| `MoltenGuard.cs:41-42` | `Complete` 中的两个 LINQ Any 闭包（状态最多持续约 90 秒） | 按 tick 记忆 |

风险：低。按 tick 记忆和现在的唯一区别：同一 tick 内中途受伤，新的阶段要到下一 tick 才生效。

### P15【中】热路径上不缓存的组件查找（R3）
| 位置 | 热路径 | 修法（等价） |
|---|---|---|
| `ItemsScrolls.cs:181-184` `EvilWardOccupy` | `PawnCanOccupy`，**每个移动中小人的每一步**都要做一次 `map.GetComponent<ItemsMap>()`，而驱邪结界几乎从不存在 | 全局 `ItemsMap.TotalWards` 计数为 0 时直接返回，或按 `map.uniqueID` 缓存组件 |
| `Items.cs:156-167` ShieldState / ForcedVisible | 每 tick 每个护盾携带者一次，每帧一次 | 缓存 def，并按地图缓存组件 |
| `LegendaryMonsters.cs:162-163, 508-509` `AntiMagicShield` | 护盾激活时每 tick、每帧都调用 | 全局“眼魔 + 反魔护符”计数为 0 时返回 |
| `Legendary.cs:378-386` `LegendaryActionTrigger` | `TryCastNextBurstShot`，每发子弹 / 每刀，后面还有 `legends.ToList()` | 静态“已生成传奇”计数作门槛；按下标遍历 |
| `Faith.cs:851-857` `FaithGrowth` | 每棵植物每次生长更新，每次两次组件扫描 | 缓存 World / Map 组件，先判断 `manifestGod` |
| `AscensionPaths.cs:533-539` `AscensionRecords` | **每一次** `Records.Increment`（开枪、搬运、采矿……），在检查记录类型之前就做 GetComponent 和 LINQ 闭包 | 先判断 `def!=AnimalsTamed && def!=CorpsesBuried` 就返回（零成本，完全等价） |
| `Guns.cs:173-176` `Verb_ArcaneShoot.Available()` | 选武器 / AI / 每帧按钮都会调用，每次都算 PsychicSensitivity 并查 GunMap | 按小人按 tick 记忆；按地图缓存组件 |
| `Pacts.cs:167` `Pacts.Of` | Comp 扫描在 `activeCount==0` 判断之前 | 调换顺序 |
| Faith.Comp / Ascension.Tracker / Twilight.World / Netheril.State / IdeoState.Get / Adventurer.State / Epic.State / Growth.State / Classes.State / Martial.State / Mentor.State | 从 StatPart、想法、植物、受伤等路径间接调用 | 统一改成“以 `Find.World` / `Current.Game` 引用作键”的静态缓存，读档时引用变化会自动失效 |

### P16【中低】`Pawn.PreApplyDamage` / 近战命中上每一击都做的扫描与分配
- `Classes2.cs:94-102` `ClassDamageMods` → `Paladin.ApplyEnmity`（`Paladin.cs:33`：`hediffs.OfType<Hediff_Enmity>().FirstOrDefault()`，**每次受伤都分配**）、`Monk.Deflect`（`Items.Hediff("LP_Deflecting")` 加扫描）。
- `Classes2.cs:82-88` `ClassMeleeHit` → `Paladin.OnMeleeHit`：**每一刀**都做 2 次字符串 def 查找和 2 次 `GetFirstHediffOfDef`，没有提前返回。
- `AdventurerFeats.cs:359-372` `FeatDamage`：每次受伤都做 `armorCategory.defName!="Blunt"` 字符串比较、`Has(HeavyArmor)` 扫描、`Items.Hediff("LP_FeatInspired")` 和 GetFirstHediffOfDef。
- `AdventurerLegend.cs:186-192` `LegendToughness`：每击一次 `Get` 扫描。
- `MagicApparel.cs:247-263` `CallDragons`：龙骑士套装持有者每次被敌人打中，都要对阵营小人做一次 `.ToList()` 再逐个判断 `ForceDragon.Active`。
- **修法**：全部改成静态 def 字段加一遍 for 循环（等价）。`CallDragons` 加一个 60 tick 节流（**不完全等价**，放进单独一节由 Peter 决定）。
- **附带**：这和 `MartialClasses-1.53.0-report.md` 第三节第 16、17 行及第 90 行“热路径里没有 LINQ、没有遍历 hediff 列表”的说法不符。新增部分确实有快速返回，但同一个钩子里原有的圣武士 / 武僧逻辑没有。其他桥接行（第 8～15、18、19 行）都核对属实。

### P17【中低】一些周期性全图扫描与 O(n²) 的想法计算
| 位置 | 频率 | 问题 | 修法 |
|---|---|---|---|
| `Legendary.cs:302, 319-324` | **每 60 tick 每张地图** | 全图 `AllPawnsSpawned` 扫描，每人一次 `WearsAmulet`，后者是 LINQ 加 defName 比较（`LegendaryMonsters.cs:172`） | `WearsAmulet` 改 for 循环加缓存 def（等价） |
| `PortalEvent.cs:154-160, 204-220` `CompPlanarRift.Feud` | 裂隙开着时每 120 tick 一次 | 每对小人之间做 O(n²) 的 LINQ（`Side()` 每次都 `OfType().FirstOrDefault()`），**最后**才判断这个位面有没有 rival 一方；只有妖精荒野有，其余位面算完全部丢弃 | 把 `hasRival` 判断提到最前面；每人的 `Side` 先算一次存起来（等价） |
| `Ascension.cs:314-315, 213` `ThoughtWorker_DivineRadiance` | 每个殖民者每次重算想法 | `AllSparks` 遍历每个殖民者的背包并逐件 `TryGetComp`，整体是殖民者数的平方 | 按地图按 tick（或 250 tick）缓存 any |
| `FaithRaces.cs:158-160` `TieflingStranger` | 同上 | 殖民者两两之间做基因扫描 | 按地图缓存“新提夫林”列表 |
| `MagicApparel.cs:170-200` 骑士旗 / 食人魔皮 StatPart | 缓存失效时（250 / 60 tick）全图扫描 | 食人魔皮先做 HostileTo 再算距离；两个 per-pawn 缓存都不清理 | 调换顺序（等价）；按地图建穿戴者普查 |
| `AdventurerFeats.cs:170-173` `RefreshSentinels` | 250 tick，所有地图所有小人 | 动物也要做一次 hediff 扫描 | 先判断 `Humanlike`（等价） |
| `MagicItems.cs:194-213` | 每 60 tick | 所有地图 × 所有小人做 `MagicLights.Wants` | 可以接受；可选改为由穿脱事件维护穿戴者集合 |
| `Faith.cs:476`、`Ascension.cs:376` 仪式 `FailOn` | 仪式期间每 tick（分别持续 5000 / 30000 tick） | `ChosenCount`（PawnsFinder 加扫描）、`ShardsOn`、`RiteBlocked` → `AllSparks` / `LivingDemigods` 每 tick 都重算 | 按 tick 记忆（等价）；如果改成“每 250 tick 检查一次”，失败会晚最多 250 tick，**不等价**，留给 Peter 决定 |
| `PactRaces.cs:84-90` `NearestTree` | 树精到达时一次 | 树数 × 小人数 × 2，外加 LINQ，会卡顿几十毫秒 | 先建一次“已占用的树”集合（等价） |

### P18【低】其他小项（全部等价，顺手修）
- `Amphibious.cs:22-40` `Pawn.get_WaterCellCost`：每次都对两三个两栖基因做扫描，并对所有衣物和**所有 hediff** 调 `GetModExtension`。预先算好“带扩展的衣物 / hediff def 集合”，为空时跳过对应的循环。
- `Faith.cs:866-875`、`Demigod.cs:360-368`：每次 `TryGainMemory` 都调用 4 次文化相关的 `StartsWith`，`DeathDemigodMemory` 前面完全没有门槛。改成 `HashSet<ThoughtDef>`（序数比较）。`AscensionPaths.cs:570` 的 `StartsWith("LP_Doom")` 同理。
- `Demigod.cs:371-372` `JusticeReveal`：`IsPsychologicallyInvisible` 后缀每次都分配一个闭包（捕获参数）。把它移到辅助方法里。
- `Loxodon.cs:37-44` `ThoughtNullified`：先做 `Fiends.Is` 再比较 def；改成按 ThoughtDef 缓存的 bool，先判断它。
- `Races124.cs:57-62` `Hediff_GeneLinked.ShouldRemove`、`Pacts.cs:612` `Hediff_PactSigner.ShouldRemove`（每个健康 tick 两次 `FirstOrDefault` 闭包加 GetComponent）、矮人 / 半身人 hediff：缓存来源引用。
- `MindResist.cs:30-35`：精神压制期间每次重算心情都绕过缓存，做一次完整属性计算。按小人加 250 tick 缓存（仅精神压制期间生效）。
- `Twilight.ByDegree`、`FaithUtility.DegreeToDeity`、`Faith.God(key)`：LINQ 加闭包，改成数组或字典。
- `Satiety.cs` 中 CurCategory、阈值、GetPriority 每次都要走一次 ConditionalWeakTable（加锁）。可选：加一个“有成年力场龙”的全局计数作门槛。
- `ItemsPotions.cs:21` `IsPotion`（WillEat × 2、毒品策略 × 2）：每次都用 `Any` 装箱枚举器，改成启动时建好的 HashSet。
- `ConstructColonists.cs:30-36` `ThingDef.get_AllRecipes`：对构装体 def 每次都 `Concat.Distinct.ToList`，改成静态缓存。
- `ConstructHuman.cs:63-66` `get_ThingID`：每个小人每次读 ThingID 都做 hediff 扫描，主要在存档时发生。加 legacyThingID 计数作门槛。
- `Bridges/VPE`：
  - `Abilities.cs:398-403` DeathWard（CheckForStateChange）、`:162` StunImmunity（4 次字符串查找）、`Classes2.cs:119` 恐慌的 `Contains`、`Artificer.cs:86` Overcharge：都改用静态 def。
  - `AdventurerVPE.cs:73-76` SpellSniper：把 `Feats.Has` 放到 `def.Psycast()` 后面判断，或先判断 `Adventurer.Enabled`。
- `Troll.cs:63-67`：`EnsureMetabolism` 放在了 250 tick 门槛之前，移到门槛之后。
- `ChromaticDragons.cs:469-482`：列表为空时也每 250 tick 做 3 次 `ToList`，加 `Count>0` 判断。
- `DragonFlyover.cs:375,378`、`StormGiant.cs:39`：调换判断顺序。
- `Marilith.cs:131`、`CloudGiant.cs:26`：每走一格都对地形名做两次不区分大小写的 `IndexOf`，按 TerrainDef 缓存。
- `ForceDragonHoard.cs:118-126`：每次在房间里开始搬运都分配 LINQ，先判断房间里有没有床。
- `RacialRelation.cs:100-132`：每 250 tick 拼一次字符串键。改键会涉及存档格式迁移，**本版不建议改**。

### P19【只影响帧率，不影响 TPS】
- `AdventurerUI.cs:69-293`：冒险者页签每帧都立即构建所有悬停说明（XpSourcesTip 的 StringBuilder 加 LINQ、6 次 EffectText、8 次 LegendTip、每个专长的 PrereqText、`Feats.All` / `Boons` 的 OrderBy）。改用 `TipSignal(()=>…, id)` 懒加载，排好序的列表缓存一次。
- `Demigod.cs:94` 神力 gizmo 的悬停说明：每帧都算 `DailyGain`，要扫两遍殖民者。改为懒加载。
- `NetherilScrolls.cs:283` 卡苏斯卷轴的检视文字、`Outsiders.cs:224-227` 真名卷轴的检视文字：每帧都 `PawnById`，遍历所有世界小人。缓存解析结果。
- `PactCelestial.cs:103-114`（祭坛 gizmo，每帧 3 次 PlayerPawns 加 `ColonyMaxLevel`）、`Pacts.cs:681-694`（讲台 gizmo）、`Faith.cs:384`（祭坛 gizmo）。
- `Items.cs:117` `MapComponentUpdate`：每个结界每帧都做 `w.rect.Cells.ToList()`，缓存格子列表。
- `AbyssTaint.cs:136-141`、`AbyssRaces.cs:238-243`、`PactRaces.cs:315-317`、`Appearance.cs:84`：渲染节点的 `CanDrawNow` 每帧都做 LINQ `OfType`，或先查基因再判断 Severity。

---

## 二、热路径钩子在 1.58.3 的状态总表（主要的）

| 原版方法 | 本模组后缀数 | 普通小人（没有任何 DND 特性）每次调用付出的开销 | 对应条目 |
|---|---|---|---|
| `Pawn.Tick` | 1 | `Fiends.Sees`（字符串、基因、特性、字典） | P1 |
| `Pawn_AgeTracker.get_BiologicalTicksPerTick` | 3 | 3 次 hediff 扫描加 1 次字符串查找 | P6 |
| `Pawn.get_BodySize` | 4 | 24 × 基因扫描、hediff 扫描 × 2、CWT | P4 |
| `Pawn.get_HealthScale` | 4 | 5 次字符串分配、约 6 次 hediff 扫描 | P5 |
| `Verb.CanHitTargetFrom` | 1 | `Fiends.Sees` | P1 |
| `VerbProperties.AdjustedRange` | 3 | hediff 扫描、字符串加服装扫描 | P8 |
| `Pawn.ThreatDisabled` | 2 | `HostileTo`、字符串加 hediff | P8 |
| `ShotReport.HitReportFor` | 7 | 约 8 次字符串、7 次服装扫描、5 次基因扫描 | P8 |
| `ForbidUtility.IsForbidden` | 1 | 出现过魅魔后多一次字典查找 | P11 |
| `Pawn_SkillTracker.Learn` | 4 | Psycasts 扫描、`Get` 扫描、ModsConfig | P10 |
| `Pawn_PathFollower.*` / `Reachability.CanReach` / 门 | 约 10 | ShadowLeg 字符串扫描、ItemsMap 组件扫描、哨兵 | P7、P15、P8f |
| `Ability.CanCast` / `GizmoDisabled` / VEF `IsEnabledForPawn` | 若干 | 整表复制 | P2 |
| `Pawn.PreApplyDamage` | 22 | 守护姿态整表复制、Enmity LINQ 等 | P2、P16 |
| `Thing.TakeDamage` | 10 | `object[]` 装箱 | P13 |
| `ThingWithComps.CanStackWith` / `Thing.SplitOff` | 各 1（Mythic） | Game 组件扫描 | P12 |
| 属性 StatPart（MoveSpeed / WorkSpeedGlobal / GeneralLaborSpeed …） | 32 个 StatPart | 闭包分配、World 组件、字符串拼接、未缓存的 def | P9 |

---

## 三、已检查、无需修改（附上让它们足够便宜的那个提前返回）

**冒险者 / 专长 / 英雄特性**
- `HeroTraits` 的所有战斗钩子（`HeroDamage`、`HeroAfterHit`、`HeroMeleeHit/Dodge`、`HeroShot`、`HeroRange`、`HeroStun`、`HeroMental` 等）：都走 `HeroTraits.Info`，即 `Dictionary<Pawn,HeroInfo>`，对没有特性的小人缓存 null，在特性 / 专长增减时失效，每天清理一次。
- 战斗风格（`StyleMeleeHit`、`StyleBlindSight`，以及 AdjustedRange / HitReport 里的风格部分）：`Users.Count` 加 HashSet 查找。
- Feats2 的伤害、索敌（`disguised.Count`）、交易、食物钩子；`Observer` 用的是 HashSet。
- `AdventurerDamage` / `AdventurerKill`（TakeDamage / Kill 上的冒险者经验钩子）：先判断 Pawn、Dead、`totalDamageDealt`、KillCounts。
- **实战磨砺（Growth）每 2500 tick 的采样**（`Growth.cs:58-79`，从 `HeroTraits.cs:477` 调用）：每个殖民者每 2500 tick 只做一次 `float[6]` 加约 10 次列表查找，可以忽略。Growth 的伤害、守护、射程、剑钩子都以 `legacyDefs.Count` 或 `swordOwners.Count` 作门槛。
- 六维、等级、熟练加值：都是存好的字段或纯算术，不会重新扫描。六维的属性效果已缓存在 `Hediff_Adventurer.stage`，由 `Dirty()` 或 EffectScale 变化触发重建。**唯一的问题是 CurStage 里那次 ModsConfig 调用（P3）**。
- 种族亲和与稀有天赋：缓存在 hediff 上，每 250 tick 通过 `RefreshAffinity` 刷新。
- `GameComponent_Adventurer`、`GameComponent_HeroTraits`、`GameComponent_Epic`、`GameComponent_Growth`：全部按模数分时触发。
- BackgroundTraits 的 StatPart 与想法：各自都有 Drafted、`story==null`、`TradeSession.Active`、特性判断或 120 tick 缓存作门槛。

**耐瑟瑞尔 / 信仰 / 意识形态**
- **耐瑟卷轴每 60 tick 的积累**（`NetherilScrolls.cs:125-146`）：每个读者每阅读 tick 只做两次 `Dictionary<int,int>` 操作；每 60 tick 结算一次（一次 GetComponent 加几次字典操作），可以忽略。
- `NetherilInsightOnReadEnd`（JobDriver.Cleanup）：先判断 `is JobDriver_Reading`。
- `NetherilIdleJoyTick`（JoyTickCheckEnd）：先判断 `job.def!=Reading`。
- `NetherilJoySplit`（GainJoy）：先判断 `joyKind.defName!="Reading"`。
- BookUtility 的两个钩子：先判断 `!__result`。
- `NetherilInsightLearn`：先判断 Intellectual。
- `NetherilJoyFirst`：先比较类型。
- `StatPart_LPIdeo` 和 LP 戒律 / 模因的想法：读的是每 2500 tick 刷新的 `IdeoMapCache`，这个写法很好，可以作为全局普查的范本。
- `StatPart_SaintDivine`（一次 `HasTrait`）、`StatPart_WorkbenchWorker`（先判断 `is Building`）、`ShrineHitPoints`（先判断 `is Phylactery`）。
- `TwilightMalarNight`（StatPart_Glow.ActiveFor）：普通小人在 `GetTrait` 这一步就返回。
- `FaithDark`（ThoughtWorker_Dark）：先判断 `__result.Active`。
- `FaithSocialFight`：先判断 `__result<=0`。
- `FaithHistory`：先判断 Enabled，再比较 3 次序数字符串。
- `FaithRecords`：先判断 Enabled 和 PlayerColonist。
- 各 World / Map / Game 组件的 tick（`WorldComponent_Faith`、`LP_AscensionTracker`、`WorldComponent_Twilight`、`NetherilQuestState`、`IdeoState`、`MapComponent_Faith`）：都按模数触发，或在为空时直接返回。

**深渊 / 契约 / 种族**
- `PawnCapacitiesHandler.GetLevel`：`__result<.31f && Consciousness`。
- `get_InPainShock`：先判断 `__result`。
- `CalculateCapacityLevel`：`__result>=.5f`。
- `Thought.MoodOffset`（半身人）：`__result>=0` 加按 ThoughtDef 缓存。
- `Plant.GrowthRate` / `MakeLeafless`（PactBoons）：先判断 `summerMaps.Count==0`。
- `Plant.get_GrowthRate`（MagicItemsCombat）：空字典查找。
- `StatPart_AbyssMove`：先判断 `Outsiders.Of==null`。
- `PlayerPawnsForStoryteller`、`TryGiveThoughts`：用 `thralls>0` 作门槛。
- `ResearchProjectDef.IsHidden` 两个钩子：引用比较。
- 所有光环与邻居查找，除了 P2 列出的那些：Hezrou 恶臭、火焰光环、看破、卸除伪装都用 GenRadial 按格扫描；炼狱印记和骨魔用的是 `attackTargetsCache`。
- 全部 Gene / Hediff 的 TickInterval：都有 60、120、250、300、2500 或 60000 tick 的累加门槛。

**巨人 / 龙 / 怪物**
- `BodySizeHealth` 与 `StatPart_BodySizeMelee`：先判断 `genes==null`，再用 CWT 缓存（以 hediff 数、基因数、年龄、250 tick 戳校验）。
- `GiantKin.Of` / `ColonyGiants`：有缓存，且有失效机制。
- 流浆人的流血、感染、伤疤、操作、门钩子：`__result` 门槛或缓存的 `Is`。
- `EttinTagEfficiency`、`StatPart_DragonStrength`（一次字典查找）、`StatPart_WolfLord`。
- 各 GameComponent（Monsters、CloudGiantState、Ordning、Migration、TrollRevivals、DragonLair）：都按模数触发，或在为空时返回。

**物品 / 核心**
- 三个 `StatWorker.StatOffsetFromGear` 后缀：先判断 `__result<0` 和是哪个属性，或判断扩展是否为 null。
- `Hediff.Severity`：补丁打在 **setter** 上，不是 getter，并且先判断 `value>0.9`。
- `Need_Food.get_MaxLevel`：先判断 ProgramState。
- `WillIngestStackCountOf` / `IngestedCalculateAmounts`：先比较 cap。
- `StatPart_Stomach`：MaxNutrition 由原版缓存 15 tick。
- `StatPart_MindShield`、`StatPart_AlchemySkill`、`StatPart_RedRobeCommand`。
- Bill / Apparel / Equipment 的各个过滤钩子：先判断 `__result` 或类型。
- `ApparelGraphicRecordGetter`：只在图形被标脏时调用。
- `OncePerCooldown`、`TemperingCompatibility`。
- `GeneWorkbench` 的 Tick / CompTick。

**VPE 桥**
- Martial 的投射物和 Verb 钩子（`Martial.cs:238-265`，第三节第 8～15、18、19 行）：都以 `Count==0` 作门槛，与 1.53.0 报告一致。
- `MapComponent_ClassAuras.Refresh`（每 250 tick）：100 个小人时每 250 tick 不到 0.5 ms，可以接受（只做可选的 def 静态化）。
- `Hediff_MartialFlag`：一次 int 比较。
- `MentorState`：先判断 `visits.Count>0`。
- `StatPart_ClassSkill` / `StatPart_Bloodline`：只被 LP 能力的 `powerStatFactors` 等字段引用，而且这些属性是 `cacheable=false`。实际上只在 UI 里，以及 NPC 自动施放传送时才会算到。**不建议改**：改成可缓存会改变时序语义。
- MythicBridge 的 `ThoughtWorker_MythicRelation`：用的是缓存的普查。

---

## 四、正确性附带发现（不是性能问题，第二阶段先问 Peter）

1. `HeroTraits.Info` 里缓存了冒险者 hediff 上的 Feats2 标志位。`AdventurerExcluded`（Adventurer.cs:720-725，例如变成构装体时）移除 hediff 时没有调用 `HeroTraits.Invalidate`，所以专长效果会一直残留，直到下一次特性或专长变化。修法：`Hediff_Adventurer.PostRemoved` 里调用 `Invalidate`，再调用 `Feats2.Sync`。
2. 有几个 plain `Dictionary` 会在可能并行的渲染预处理 / 属性代码里被**写入**：`Dragons.colorCache`（ChromaticDragons.cs:40）、`Monsters.cache`（LegendaryMonsters.cs:22）、`Subraces.genes/hediffs/cacheValue`（CommonRaceSubraces.cs）。`Items.cs:12` 的注释说明当初改用 ConcurrentDictionary 就是这个原因。建议也改成 ConcurrentDictionary，或在启动时预先填好。（ScaleFor 在 1.5 及以后是否在并行线程里运行，**待验证**。）
3. `ArtSource/Abyss-1.54.0-report.md` 里对魅魔门槛的描述（“地图上从没出现过魅魔”）不准确，实际是本局全局计数，而且只增不减（P11）。
4. `ArtSource/MartialClasses-1.53.0-report.md` 第三节第 16、17 行和第 90 行的说法只对新增部分成立（P16）。

---

## 五、第二阶段建议的施工顺序

1. **公共基础**：R1 Def 静态化、查不到的 def 也缓存、`Races124.Has` 先判断 genes；R2 `PawnsNear` 不再复制；R3 组件引用缓存；R4 `Feats.Has` / `Epic.Has` 超集集合；P3 ModsConfig 缓存。改动集中、等价、风险低，大约一半的条目会随之自动变便宜。
2. **每 tick 每小人**：P1 魔鬼之眼惰性求值、P6 衰老门槛、P4 / P5 BodySize 与 HealthScale、P7 暗影之腿计数、P11 魅魔计数。
3. **战斗**：P2c 提尔登记表加守护姿态计数、P8、P13、P16。
4. **StatPart 与 CurStage**：P9、P14。
5. **装了 Mythic 的玩家**：P12。
6. 其余 P15～P18 顺手修；P19（帧率）最后做。
7. 每一步都先跑 `-Only Perf`（见第六节）拿到修改前的数字，修完再跑一次做对比。然后跑全量 `-VPE` 和 `-Full`，确认行为没变。

不等价的改动（PawnsNear 改按距离排序、仪式 FailOn 降频、`CallDragons` 节流、`RacialRelation` 键格式）**默认都不做**，需要 Peter 点头才做。

---

## 六、计时方案

### 6.1 不编译能做什么
- `Source/PerformanceChecks.cs` 只有微基准（巫妖 / 构装体），而且依赖全量 `Run()` 里的 `worker`。单独跑 `-Only Performance` 会因 `worker==null` 抛 NRE。
- **已有现成的 100 小人每 tick 计时**：
  - `AbyssChecks.cs:317-344` `CheckAbyssPerformance()`：90 个外来者加 10 个基线小人，先预热，再交替做 3 轮 off / on。计时代码是 `Step(120); Stopwatch; Step(1500)`，换算成 ms/tick，输出 `[LICH-PERF]` 行。
  - `PactChecks.cs:429-447` `CheckPactPerformance()`：100 人，同样的写法。
- `OnlyChecks.cs:16-26` 用反射查找 `"Check"+组名`，所以 **`Test.ps1 -Only AbyssPerformance` / `-Only PactPerformance` 不改代码就能单独跑**；加 `-Full` 时用 Peter 真实的模组列表。其中 “off” 那一轮就是“100 个普通殖民者 + 本模组全部常驻钩子”的每 tick 毫秒数，可以作为修改前的基线。（这一阶段没有运行，因为会启动第二个 RimWorld。）
- 局限：人数和 tick 数是写死的；小人没有 DND 职业、专长和种族；`-VPE -Only X` 会被路由到桥接检查（`VPEBridgeHook.cs:20`），所以 `-VPE -Only AbyssPerformance` 跑不了。

### 6.2 第二阶段新增 `-Only Perf`（主程序集）和 `-VPE -Only Perf`（桥）
**文件与路由**
- `Source/PerfChecks.cs`：`partial LichChecks.CheckPerf()`，由 `InvokeGroup("CheckPerf")` 自动发现。核心逻辑放在 `public static class LichPerf { public static Action<List<Pawn>> Decorate; public static … Run(Map map) }`。
- `Bridges/VPE/Source/PerfChecks.cs`：`static void PerfChecks()`（由 `BridgeChecks.Invoke` 调用），设置 `LichPerf.Decorate = AddClasses` 后调用 `LichPerf.Run`。在 `-VPE` 下主程序集的组不会被调用，所以必须由桥来驱动。

**人口（约 100 人，确定性）**
1. 用 `Rand.PushState(20261027)` 包住生成过程，在 finally 里 PopState。
2. 临时解除测试抑制，在 finally 里恢复（照 `HeroTraitsChecks.cs:32-33,148` 的写法）：
   - 解除：`Adventurer.TestSuppressed`、`HeroTraits.TestSuppressed`、`Talents.StripAll`、`BackgroundTraits.TestSuppressed`（加 `ApplySettings()`）、`Backstories.TestSuppressed`、`Feats2.TestSuppressed`。
   - **保留**：`Faith.TestSuppressed` 和 `Twilight.TestRoll=1f`，避免触发事件和信件。
3. 用地图中心 14 格内可站立的空格放人（`AbyssChecks.cs:321` 的写法）。
4. 人口构成，复用现有生成器：
   - 40 个基线冒险者：照 `AbyHuman` 生成，`Adventurer.TryAdd(p,1..20,true)`，再从 `Feats.All` 里 `AddFeat`，用 `Styles.Pick` 选风格。
   - 30 个 DND 种族：用 `RacePawn(xeno)`（`CommonRaceChecks.cs:14`）轮换 `LP_` 异种型。
   - 10 个外来者：`AbyssRaces.Make` 加 `MakeThrall`。
   - 10 个契约签订者：`Pacts.Sign`。
   - 10 个强力特性小人：`GainTrait`。
   - 在 `-VPE` 下由 `Decorate` 再给约 30 人加职业，照 `Mentor.MakeMentor`（`Mentor.cs:55-63`）的步骤；8 条路径各 4 人。完成后调用 `Martial.Refresh` 预热缓存。
5. 工作环境：建一个储存区，放约 20 堆混合物品（钢、简单餐、生肉、LP 物品），让搬运、合并、食物搜索、学习这些钩子都真的跑起来。

**整图计时**
- `double Run(int n){GC.Collect();Step(120);var sw=Stopwatch.StartNew();Step(n);sw.Stop();return sw.Elapsed.TotalMilliseconds/n;}`，其中 `Step` 就是 `Find.TickManager.DoSingleTick()`（`Checks.cs:19`）。先预热 2 次，再交替 3 轮 off / on。
- 计时期间设置 `DebugSettings.enableStoryteller=false`，之后恢复；每轮用 `Rand.PushState(seed+k)`，让 off 和 on 看到同样的随机流。
- “off / on”对比方式：
  - **A（最稳，默认）**：100 个基线小人对 100 人的 DND 混合人口，格子和物品相同。
  - **B（隔离桥的开销）**：同一批人口，`new Harmony("local.lich.phylactery.vpebridge").UnpatchAll(...)`，再移除 `MapComponent_ClassAuras`；计时后用 `PatchAll` 恢复。Mythic 的 ID 是 `local.lich.phylactery.mythicbridge`。主程序集的 ID 风险较大，不用。
  - **C**：用现有设置开关做子系统级对比（`adventurer`、`backgroundTraits`、`dndBackstories` 等）。

**热点微基准**（复用 `Measure`，即 `PerformanceChecks.cs:13-23` 的 5 轮预热，并记录当前线程的分配字节数；桥里要复制一份）：
- 调用级：
  - `p.skills.Learn(Construction/Intellectual,.01f)`，先清掉 `givenToday`
  - `CanStackWith`（Mythic 配置下）
  - `FoodUtility.WillEat`
  - `ability.IsEnabledForPawn(out _)`、敌方施法者的 `TryGetAttackVerb`
  - `GuardStanceShare.Guardian`、`TwilightSkills.Silenced`
  - `MapComponent_ClassAuras.Refresh` × 100
  - `p.BodySize`、`p.HealthScale`、`p.ageTracker` 的 BiologicalTicksPerTick 读取
  - `verb.EffectiveRange`、`p.ThreatDisabled(searcher)`、`ShotReport.HitReportFor`
  - `ForbidUtility.IsForbidden`
- **属性热点**：MoveSpeed、MeleeHitChance、MeleeDodgeChance、ShootingAccuracyPawn、WorkSpeedGlobal、GeneralLaborSpeed、PsychicSensitivity、IncomingDamageFactor，外加 `LP_MartialPower`。每次读取前先 `stat.Worker.ClearCacheForThing(p)`（元数据里有这个方法），或直接 `stat.Worker.GetValue(StatRequest.For(p))`（1.7 下是否绕过缓存待确认）。分别测基线小人、DND 种族小人、职业加专长小人，差值就是本模组 StatPart / hediff 带来的开销。
- 能力：先 `Notify_CapacityLevelsDirty()`，再 `GetLevel(Moving)`。
- 分配：P1、P2、P4b、P5a、P9a、P9c、P12、P13 修改前每次调用应该都有非零字节，修改后应该为 0。这一项可以当作“修好了”的客观证据。

**输出**
- `[LICH-PERF]` 行写进 `…/Validation/<配置名>/stdout.txt`，`Measure` 的结果写进同一文件夹里的 `LichPerformance.txt`。
- `Test.ps1` 只检查 `LichResult.txt` 是否以 PASS 开头（`Test.ps1:62-64`）。所以计时数字只供参考，最后用 `Check(baseMs>0 && onMs>0,"…")` 结束，**绝不按阈值判失败**（噪声约 ±20%）。

**注意事项**
- 整个进程超时 360 秒（`Test.ps1:60`）。预算：100 次生成（几秒），加 (2 + 6) × 1620 tick ≈ 1.3 万 tick，按每 tick 1～4 ms 计约 15～50 秒，再加微基准。总时长控制在 150 秒以内；`-Full` 配置下降到每轮 1000 tick。
- 存档往返之前要清理所有计时小人、尸体、Lord、世界小人副本和物品（照 `AbClean` 的写法，`AbyssChecks.cs:35-46`），否则存档会变大，其他 `AfterLoad` 检查也会看到多余的小人。
- 第一轮总是偏慢（JIT 和寻路预热），要丢掉。
- 可选的战斗子场景（测 P1、P2）：在约 20 格外生成 10 个敌方职业施法者，只跑 600 tick，结果只作参考（有死亡，不确定性大）。
- 已知不稳定的检查（VPE 导师付 500 银、Races124 的 smite）不要放进计时组。
- 运行 Build.ps1 会覆盖 `1.7/Assemblies/LichPhylactery.dll`，Test.ps1 会另开一个 RimWorld 并改写共用的 Logs。**必须等 Peter 关掉游戏再做。**
