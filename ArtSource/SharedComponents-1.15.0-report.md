# DND边缘大陆 1.15.0 共享组件报告

日期：2026-10-24。范围：只做给后续常见种族用的四个底层组件，**没有新增任何种族、真实基因或异种型**，现有巫妖/火巨人/巨魔/力能龙的数值与行为不变。玩家在游戏里暂时看不到新内容（心智抗性为 0 时隐藏；种族关系示例默认关闭；保命冷却只有挂了扩展的基因才会出现）。

## 改动文件

| 文件 | 类型 | 说明 |
|---|---|---|
| `Source/SmallBody.cs` | 新增 | 小体型扩展 `SmallBodyExtension`、体型/渲染/搬运补丁 |
| `Source/MindResist.cs` | 新增 | `DNDMind` 工具类、心灵类负面心情按抗性减弱的补丁 |
| `Source/OncePerCooldown.cs` | 新增 | 冷却保命扩展、worker 基类、降伤示例、冷却 hediff、伤害补丁 |
| `Source/RacialRelation.cs` | 新增 | `RacialRelationDef`、`ThoughtWorker_DNDRacialRelation`、地图普查缓存与同处天数记录 |
| `Source/SharedComponentChecks.cs` | 新增 | 四个组件的自检（47 项，含存读档 2 项） |
| `Defs/SharedComponents.xml` | 新增 | `LP_MindResist`、`LP_CheatDeathCooldown`、示例心情 `LP_RacialRelation_TrollFireGiant` 与示例关系 `LP_Relation_TrollFireGiant`（默认关闭） |
| `Source/Checks.cs` | 修改 | 挂入 `CheckSharedComponents()`、`PrepareSharedSave()`、`CheckSharedComponentsAfterLoad()`，结果摘要加一句 |
| `About/About.xml` | 修改 | `modVersion` 1.14.0 → 1.15.0 |
| `使用说明.txt` | 修改 | 顶部新增 1.15.0 条目 |
| `1.7/Assemblies/LichPhylactery.dll` | 重新编译 | |

改动前备份：`ArtSource/backup-1.14.0/`（Checks.cs、About.xml、使用说明.txt、旧 DLL）。新增文件没有旧版本，删掉即可回退。

## 1. 小体型 `LichPhylactery.SmallBodyExtension`

- 先查了 VEF：本机 VEF 1.7 的 `GeneExtension` 确实有 `bodySizeFactor`、`bodyScaleFactor` 等字段，但本模组没有声明 VEF 依赖，Core 测试环境也不加载 VEF，所以**自己用 Harmony 实现，不引入依赖**。如果以后某个基因同时用了 VEF 的体型字段，两者会相乘。
- 补丁：`Pawn.BodySize` 后置（实际体型，原版据此算搬运量、商队载重、被远程命中率等），根渲染节点 `ScaleFor` 后置（整体显示），给原版 `CarryingCapacity` 加一个 StatPart（额外搬运倍率），基因增删时刷新绘制。
- **叠加规则：全部相乘。** 火巨人与巨魔之间原来取较大值，这一点不变；这个结果再乘力能龙龙龄、构造体，再乘所有小体型基因的乘积。例如火巨人 2× 加矮人 0.8 得 1.6×；两个小体型基因 0.8 和 0.5 得 0.4×。
- 体型倍率不随年龄变化，儿童本来就小（原版生命阶段）。原版的进食速度不随 BodySize 变化，所以小体型**不会**让人吃得更少；如果要矮人或半身人吃得少，请在基因上另配 `HungerRate`。
- 没有挂扩展的基因时，所有补丁第一步就直接返回（缓存的基因列表为空）。

```xml
<GeneDef>
  <defName>LP_DwarfStature</defName>
  ...
  <modExtensions>
    <li Class="LichPhylactery.SmallBodyExtension">
      <bodySize>0.8</bodySize>        <!-- 实际体型倍率，范围 (0,4] -->
      <renderScale>0.85</renderScale> <!-- 可省略；省略或≤0 时跟随 bodySize -->
      <carryFactor>1.1</carryFactor>  <!-- 在“搬运量已随体型缩小”的基础上再乘；默认 1 -->
    </li>
  </modExtensions>
</GeneDef>
```

## 2. 心智抗性 `LP_MindResist`

- StatDef：取值 0~1，默认 0，`hideAtValue=0`，分类为抗性。基因、hediff、装备都用 `statOffsets` 叠加，最高到 100%。
- 工具方法（`LichPhylactery.DNDMind`）：
  - `float Resistance(Pawn)`：当前抵抗率 0~1。
  - `bool TryResist(Pawn, float chanceFactor=1f)`：掷骰，抵抗成功返回 true；`chanceFactor` 用来表示强效果难以抵抗，例如传 0.5 表示抵抗率减半。
  - `float Attenuate(Pawn, float value)`：返回 value×(1-抗性)，用于削弱伤害、严重度或心情惩罚。
- **已实现可选项：** 原版以心灵敏感度缩放的**负面**心情（心灵嗡鸣、仇恨光环、仇恨颂唱等，即 ThoughtDef 的 `effectMultiplyingStat=PsychicSensitivity`）乘以 (1-抗性)。正面心情（安抚等）不受影响。抗性为 0 时数值完全不变。
- 注意（已处理）：原版 `StatDef.SetImmutability` 会把“没有任何定义引用”的属性标成不可变，并永久缓存。因此 `Resistance()` 直接调用 StatWorker，不走缓存；调用频率很低，开销可以接受。以后有真实基因引用这个属性后，属性页显示也会正常刷新。

```xml
<statOffsets><LP_MindResist>0.5</LP_MindResist></statOffsets>
```
```csharp
if(DNDMind.TryResist(target)) { MoteMaker.ThrowText(..., "抵抗！"); return; }
float severity = DNDMind.Attenuate(target, 0.4f);
```

## 3. 冷却保命 `OncePerCooldownExtension` / `OncePerCooldownWorker` / `Hediff_OncePerCooldown`

这是“同等设计”：可以子类化的是 **worker**（`OnTriggered`），冷却状态由 `Hediff_OncePerCooldown` 保存。

- 挂钩点：`DamageWorker_AddInjury.FinalizeAndAddInjury(Pawn, Hediff_Injury, …)` 前置，也就是伤势严重度已经确定、即将加到身上的那一刻。在这里用原版 `WouldDieAfterAddingHediff` 和 `WouldBeDownedAfterAddingHediff` 判断，比在 `PreApplyDamage` 里预测更准。原版的“殖民者秒杀保护”在我们之后照常执行。
- 触发条件：pawn 有挂扩展且处于激活状态的基因，没有对应冷却 hediff，并且这一处伤势会致死（`onDeath`）或让人从站立变成倒地（`onDowned`）。满足时调用 `OnTriggered`，然后加上冷却 hediff，时长取 `cooldownDays`，用原版 `HediffComp_Disappears` 计时，健康页显示剩余时间和来源基因名。
- 同一刻的后续伤势（同一次攻击打到多个部位）交给 `OnFollowUpInjury`，避免打头时触发、打脑时又死掉。
- 存档：冷却 hediff 保存 `sourceGene` 和 `triggeredTick`，worker 不需要存档。开发中发现，基类 Hediff 已经用 `"source"` 作为存档键（武器来源），所以我改用 `"sourceGene"`，读档时不会再报找不到引用。
- 本次**没有挂到任何种族上**。只有降伤示例 `OncePerCooldownWorker_ReduceDamage`，以及测试里运行时注册的测试基因（`LPTest_*`，不写进 XML，存档前会移除）。
- 加载时会校验：worker 必须是具体的子类，冷却 hediff 必须使用 `Hediff_OncePerCooldown`，至少开启 onDowned 或 onDeath 之一。

```xml
<modExtensions>
  <li Class="LichPhylactery.OncePerCooldownExtension">
    <workerClass>LichPhylactery.OncePerCooldownWorker_ReduceDamage</workerClass>
    <cooldownHediff>LP_CheatDeathCooldown</cooldownHediff>
    <cooldownDays>3</cooldownDays>
    <damageFactor>0.1</damageFactor>
    <moteText>真走运！</moteText>
    <onDowned>true</onDowned>
    <onDeath>true</onDeath>
  </li>
</modExtensions>
```
以后实现半兽人的“12小时不倒地”：写一个 `OncePerCooldownWorker` 子类，在 `OnTriggered` 里给 pawn 加一个 12 小时的 hediff，并配合 `ShouldBeDowned` 补丁（或者降伤）。目前还没写。

## 4. 种族关系心情 `RacialRelationDef` + `ThoughtWorker_DNDRacialRelation`

- 一条关系配置：观察者异种型 → 同地图（不含自己）存在目标异种型时，激活 `thought` 的第 0 阶段。设了 `daysToAccept>0` 时，观察者与目标同处一张地图累计满这些天数后，改为第 1 阶段（例如精灵对矮人从 -2 变成 +2）。`halvedBy` 里的任一异种型作为自由殖民者在同一地图时，通过 `MoodMultiplier` 让心情×0.5（半精灵的“两族之桥”）。关系是单向的，双向需要配两条。
- 性能：每张地图的异种型人口普查最多每 250 刻重建一次，心情计算只读缓存（测试里连续读取 5000 次也没有重建）。同处天数在 GameComponent 里每 250 刻累计一次，只遍历自由殖民者，并且只有存在启用中、配置了天数的关系时才运行。记录按“pawn 唯一ID|关系名”存成字符串字典（不引用 pawn，存档安全），60 天没再见到的记录会清掉。
- 示例：`LP_Relation_TrollFireGiant`，巨魔看到火巨人时 -1，相处 15 天后变成 +1，**默认关闭（`enabled=false`）**。理由：现有存档里可能已经同时有巨魔和火巨人，直接启用会给老存档凭空加心情；关闭则零影响。只有测试会在运行时临时打开它。

```xml
<ThoughtDef>
  <defName>LP_RacialRelation_ElfDwarf</defName>
  <workerClass>LichPhylactery.ThoughtWorker_DNDRacialRelation</workerClass>
  <stages>
    <li><label>矮人的粗鲁</label><description>…</description><baseMoodEffect>-2</baseMoodEffect></li>
    <li><label>矮人朋友</label><description>…</description><baseMoodEffect>2</baseMoodEffect></li>
  </stages>
</ThoughtDef>
<LichPhylactery.RacialRelationDef>
  <defName>LP_Relation_ElfDwarf</defName>
  <observer>LP_Elf</observer>
  <target>LP_Dwarf</target>
  <thought>LP_RacialRelation_ElfDwarf</thought>
  <daysToAccept>30</daysToAccept>
  <halvedBy><li>LP_HalfElf</li></halvedBy>
  <!-- <enabled>false</enabled> 可整体关闭 -->
</LichPhylactery.RacialRelationDef>
```

## 测试结果（全部用 pwsh 7 运行 `Source/Build.ps1` 和 `Source/Test.ps1`）

构建：`PASS: LichPhylactery built against installed RimWorld 1.7 and Harmony.`

| 档 | 结果 |
|---|---|
| Core（共跑 5 次） | 5/5 `PASS: 1172 checks; … race scenarios; shared components (small body, mind resistance, once-per-cooldown, racial relations).` |
| Appearance | `PASS: 1372 checks` |
| VisualCompat | `PASS: 11210 visual checks` |
| Warcasket | `PASS: 1223 checks` |
| HealthCompat None / Human / Mythic | `PASS: 286 / 269 / 267 health compatibility checks` |
| HealthCompat Both | 本模组检查 `PASS: 271`，但日志过滤命中第三方报错，脚本退出码 1 |
| Full | `FAIL after 932 checks: scholar passion only scales XP forwarded to the master`（CheckKnowledgeFeedback） |
| HealthCompat Full | 本模组检查 `PASS: 271`，日志里有第三方报错，退出码 1 |
| VisualCompat Full | 本模组检查 `PASS: 11210`，日志里有第三方报错，退出码 1 |

失败的 4 档**与本次改动无关**，已验证：把 1.14.0 的旧 DLL 换回去，并移除 SharedComponents.xml 后重跑这三类档（Both、Full、VisualFull），出现的失败**完全相同**，跑完已恢复新版文件（DLL 哈希已比对）。具体原因：
- `Config error in HA_Category / MB_Category: Multiple gene categories share the same displayPriorityInXenotype`：HumanAptitudes 和 MythicBloodlines 两个独立模组的基因分类优先级冲突，两个同时加载时就会出现。
- `Error in static constructor of GD3.HarmonyInit`：完整模组列表里第三方 GD3 模组初始化报错。
- Full 档的知识死灵回馈检查失败：完整模组列表里有其他模组改动了学习经验，属于既有问题。没有为了让测试变绿而关掉任何测试。

说明：之前提到的 1.14.0 主测试偶发失败（重塑中途存读档的伤势比例检查，上一次结果是 ratio=0.111 expected=0.070），这次 Core 跑了 5 次都没出现，该检查打印 `ratio=0.07007407 expected=0.07007407`。

新组件自检原文摘录（Core stdout，`[LICH-TEST] OK …`）：
```
small body gene scales real body size to 80%: 0.8
carrying capacity follows body size and the extra carry factor: 54 from 75
caravan mass capacity follows native body size: 28 from 35
small body multiplies with fire giant size instead of replacing it
small body multiplies with troll size
gene statOffsets grant 60% mind resistance: 0.6
TryResist succeeds at the resistance rate: 1213/2000
TryResist chance factor scales the roll: 608/2000
psychic drone mood is reduced by mind resistance: -4.8 from -12
stacked resistance clamps to 100%
lethal brain hit triggers the lucky gene and starts a visible cooldown
reduce-damage example keeps only 10% of the lethal hit: brain 2, new wounds 10
second lethal hit during cooldown is not prevented
cooldown expires through the native disappear timer and the gene is ready again
example racial relation validates and ships disabled
troll seeing a fire giant on the map gets the initial relation mood
mood checks reuse the cached map census instead of scanning pawns
after enough days together the mood turns into the accepted stage
a bridge xenotype colonist on the map halves the relation mood
cooldown hediff, its source and remaining time survive save/load
racial relation days together survive save/load
```

## 未完成 / 需要决策

1. **小体型与食量**：原版进食速度不看 BodySize，小体型目前不会少吃。要不要让矮人/半身人少吃，请在以后的基因上配 `HungerRate`，或者决定是否让本组件统一处理。
2. **`onDowned` 的实测**：致死触发、冷却阻断、冷却到期后再次触发都测过；“只致倒不致死”这条分支走的是同一段代码（原版 `WouldBeDownedAfterAddingHediff`），但没有单独做倒地场景的测试。做半兽人时建议补测。
3. **半兽人“12小时不倒地”**：只留了 worker 接口，还没实现（需要额外的 `ShouldBeDowned` 补丁）。
4. **种族关系的“看到”**：目前按“同一地图上存在（已生成的人形、非尸体，包括访客、囚犯和敌人）”判断，没有做视线或距离判断；“减半”只看自由殖民者。如果想改成只算殖民地成员或需要视线，需要定一下。
5. **示例关系默认关闭**：巨魔与火巨人的数值（-1 → 15天后 +1）只是演示；如果要正式启用，请确认数值。
6. 自定义异种型（玩家在编辑器里自己做的）的 `Xenotype` 不是 XenotypeDef，不会命中任何种族关系。
