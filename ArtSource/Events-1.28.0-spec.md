# DND边缘大陆 1.28.0 实现要求：传送门失控 + 巨人迁徙 + 黄昏之年

2026-10-25，Peter 原话：“传送门失控：地图上开一个位面裂口，按来源位面往外放地狱犬、闪光犬、冬狼这些。巨人迁徙：一队饿着的巨人路过，会找你讨吃的，有可能留下居住。然后完成‘黄昏之年’相关内容。”

依据：[DND事件构思二.md](DND事件构思二.md) 3.3 / 3.5，[DND敌人神力灌注构思.md](DND敌人神力灌注构思.md)（黄昏之年 = 神力灌注特性 + 神明附体状态 + 派系对应神祇 + 世界条件）。构思里写清楚的不重复，这里只写取舍、数值和接口。没写到的细节按模组现有惯例（1.24.0 种族、1.25.0 野兽的做法）。

## 0. 总则

| 项目 | 规定 |
|---|---|
| 模组 | `E:\SteamLibrary\steamapps\common\RimWorld\Mods\LichPhylactery`，当前 **1.27.4**（开工前看 About.xml 确认）→ **1.28.0** |
| 备份 | 开工前把要改的文件和 `1.7/Assemblies/LichPhylactery.dll` 复制到 `ArtSource/backup-1.27.4/`；本需求存为 `ArtSource/Events-1.28.0-spec.md` |
| 编译测试 | pwsh 7（`%LOCALAPPDATA%\Microsoft\WindowsApps\pwsh.exe`），在 `Source` 下 `.\Build.ps1`、`.\Test.ps1`。Test.ps1 会备份/恢复 Player.log。偶发失败先重跑。OK 文案不写 "config error" 之类 |
| 文件 | 新 Defs：`Events_Portal.xml`、`Events_GiantMigration.xml`、`Twilight.xml`（特性、状态、世界条件、事件、神迹残片、派系扩展补丁）；新 Source：`PortalEvent.cs`、`GiantMigration.cs`、`Twilight.cs`、`TwilightDeities.cs`、`EventChecks.cs`（挂到 Checks.cs） |
| 原则 | 不加属性倍率，只加规则（Isekai 管数值）；事件生成的敌人不锁 Isekai 等级。种族判定按基因规则。文本中文，Def 前缀 `LP_` |
| 设置 | 右栏新开“事件”分组：传送门失控开关、巨人迁徙开关、黄昏之年开关、黄昏强度滑条 50%～200%（默认 100%）、原版派系（海盗/部落）神力开关（默认关）、平时灌注概率是否为 0（默认否） |
| 贴图 | 位面裂口建筑 1 张（2×2，紫黑旋涡，RimWorld 画风，照 `draw_item_art.py` 的手法新写 `draw_events_128.py`）；神迹残片图标 1 张；附体发光用原版 `HediffCompProperties_AttachPoints`/发光组件 + 一张可染色的光晕贴图（8 色）；特性无图 |
| 自检 | `EventChecks.cs`：见第 4 节；Build、Core 通过；Full 档若停在旧检查，确认与本次无关 |
| 收尾 | 版本号 1.28.0；About.xml 描述补一句；使用说明.txt 在“内容一览”和“详细说明”里各加“事件”一节（传送门、巨人迁徙、黄昏之年三段），更新日志顶部加 1.28.0；报告 `ArtSource/Events-1.28.0-report.md`（改动文件表、与本文差异、测试矩阵、需要 Peter 决定的点） |

---

## 1. 传送门失控 `LP_PortalBreach`

**事件**：`IncidentDef LP_PortalBreach`，category ThreatBig，玩家家园地图，baseChance 约等于原版“疯狂动物群”的一半；`minRefireDays 20`；最早第 1 年第 2 季度。殖民地里有法师路径（VPE 桥接存在时判断）、元素裔、或 15 天内用过传送卷轴时权重 ×2（写进 `CanFireNowSub` 里的 `chanceFactor`）。

**裂口建筑** `LP_PlanarRift`（2×2，不可选建造，`Building` 子类，生命 600，不可被玩家拆除，可以被攻击；`useHitPoints`；发紫光 `CompGlower`）：生成在距离殖民地建筑 ≥ 12 格的空地（用原版的 `RCellFinder.TryFindRandomSpotJustOutsideColony` 或 `CellFinderLoose.TryGetRandomCellWith`，要求 2×2 全可站立、无屋顶）。信件：“传送门失控”（ThreatBig），写明来源位面和“关上它：打掉裂口，或者用驱散粉尘 / 法师能力直接关闭”。

**来源位面**（事件生成时随机，等权；数据写成 `PortalPlaneDef`，便于以后加）：

| 位面 defName | 名称 | 每波生物（PawnKind，已有） | 升级后追加 |
|---|---|---|---|
| `LP_Plane_NineHells` | 九狱 | 地狱犬 ×2～3 | 第 3 波起 1 名寒冰魔（`LP_FH_IceDevil`，不受 1500 点门槛限制，最多 1 个，倒地消失规则照 1.24.0） |
| `LP_Plane_ElementalFire` | 元素火位面 | 地狱犬 ×2～3 | 第 3 波起 1 名敌对火裔元素裔（用塞伊远征军的元素裔兵种，没有就用基线人类 + 火裔异种型），身上常驻着火免疫 |
| `LP_Plane_Feywild` | 妖精荒野 | 闪光犬 ×2 + 魔化兽 ×1（它们互相敌对：闪光犬归一个临时派系，魔化兽归另一个，两边都敌对玩家） | 第 3 波起各 +1 |
| `LP_Plane_Shadowfell` | 幽影界 | 冬狼 ×2～3 | 第 3 波起 1 只隐身的魔化兽（加原版 Anomaly 的隐身 hediff；没有 Anomaly 则用 1.22.0 隐形药水的状态） |

**波次**：裂口生成后立刻第 1 波，之后每 8 小时一波（`CompPlanarRift` 计时），第 n 波生物数量 = 基础 × (1 + 0.25×(n-1))，上限 ×2.5。生物属于一个隐藏的临时敌对派系（照 `LP_Zealots` 的做法，或直接用无派系“疯狂动物”，推荐无派系狂暴：原版 `MentalStateDefOf.Manhunter`，这样不用新派系；寒冰魔/元素裔必须有派系，用塞伊远征军）。生物从裂口相邻格冒出，带 `FleckDefOf.PsycastAreaEffect` 之类的特效。

**关闭**：
- 打掉 600 生命 → 关闭。
- 对裂口使用驱散粉尘（1.22.0 `LP_DispelDust` 投掷到裂口 3 格内）→ 立刻关闭。在 `DispelDust` 的落地处理里加一句“命中裂口则关闭”。
- VPE 法师路径的能力：没有现成的“关闭”能力，不做；改为**法师路径殖民者攻击裂口时伤害 ×3**（按 1.27.0 的路径判断）。
- 巨灵：`LP_GenieCore` 激活的殖民者对裂口有右键“命令裂口关闭”（社交检定：社交 ×6%，上限 80%，失败则本波提前刷出；每个裂口只能试一次）。
- **3 天不关**：第 9 波之后，裂口“崩溃”：一次性放出当前波次 ×2 的生物，然后自己关闭（信件“裂口崩溃”）。

**关闭奖励**：裂口位置掉 `LP_PlanarResidue 位面残渣` 3～5 份（新材料，市值 60，`stackLimit 50`，分类“炼金材料”）。炼金桌新配方：位面残渣 2 + 任意药水 1 → “强化药水”（同种药水持续时间 ×2，照 1.22.0 药水的写法新增 2 个最常用的即可：强效治疗、抗火；其余药水不做）。野兽（闪光犬、冬狼、地狱犬、魔化兽）裂口关闭后退出狂暴状态，留在地图上变成普通野生，可以驯服。

---

## 2. 巨人迁徙 `LP_GiantMigration`

**事件**：`IncidentDef LP_GiantMigration`，category Misc，玩家家园地图，频率约原版“旅行者”的一半，`minRefireDays 25`；余烬氏族或费伦拓荒同盟存在时才触发（巨人队属于其中之一）。需要余烬氏族未被设置关闭。

**队伍组成**（随机，权重）：
- 60%：余烬氏族，火巨人 1～2 + 人类/半兽人奴兵 2～3（现有兵种）。
- 30%：食人魔一家（费伦拓荒同盟兵种里的食人魔 2～3；1.24.0 里食人魔归同盟）。
- 10%：泰坦 1 + 人类追随者 2（泰坦用 1.24.0 的 PawnKind，归费伦拓荒同盟；没有现成 PawnKind 就新建 `LP_FC_TitanWanderer`）。

**行为**（新 `LordJob_GiantMigration`，照原版 `LordJob_TravelAndExit` + 扎营）：从地图一边进入，走到地图中段距殖民地 ≥ 15 格的空地扎营（生成 1 个篝火 + 睡袋，1 天），然后走向对边离开。派系敌对时不触发（余烬 -20 开局算非敌对，-75 以下才算敌对）。

**讨食**（扎营时发一封 `ChoiceLetter`“饥饿的巨人”，写明人数和需要的食物营养值 = 队伍每人每天营养 × 2，按 1.27.1 的饱食上限算，火巨人 2 倍、泰坦 2.5 倍、食人魔 1.6 倍）：
- **给**：从殖民地储存区扣除对应营养值的食物（优先干肉饼、简餐、生肉；不够时按钮灰掉并提示差多少）。派系好感 +12；离开时在营地留下礼物：余烬 → 熔岩结晶 3 份或一件 1.7 火巨人锻造品（随机）；食人魔 → 食人魔大棒 1 把；泰坦 → 一张随机卷轴 + 神迹残片 1 份。
- **不给**：无好感变化；15% 概率夜里 1～2 名队员去偷粮（原版“偷窃”任务 `JobDefOf.Steal` 搬走 1 堆食物后回营地，玩家可以拦）。
- **攻击**（玩家主动打）：整队转敌对（派系好感按原版规则扣），按巨躯规则血量，掉落照兵种。

**留下居住**（Peter 要求：“有可能留下居住”）：扎营结束时，有 **35%**（殖民地有火巨人/食人魔/泰坦同族时 60%）概率再发一封信“想留下的巨人”：队里随机一名巨人（火巨人/食人魔/泰坦，不是奴兵）说想留下。
- 接受：他加入殖民地（照原版“流浪者加入”，带原派系关系：余烬氏族成员加入 → 余烬好感 -5“带走了族人”）；其余人继续走。
- 拒绝：全队离开，无后果。
- 给过食物时，这封信的概率 +20%。

**种族联动**（都是心情，不翻脸）：余烬火巨人队在扎营期间，殖民地有巨魔 → 队里火巨人“厌恶巨魔”心情；殖民地火巨人看到同族队伍 +3“氏族路过”。不做“交出巨魔”的要求。

---

## 3. 黄昏之年（神力灌注 / 神明附体）

### 3.1 本文替 Peter 定的默认值（构思第七节的 9 个问题）

| 问题 | 默认 |
|---|---|
| 1 两档都做？ | 都做 |
| 2 原版派系（海盗 → 塔洛斯、部落 → 马拉尔） | 做，默认**关**，设置可开；其他模组派系不管 |
| 3 频率/长度 | 最早第 2 年、每年 25%、45 天（设置 15～120） |
| 4 概率 | 平时 3%、黄昏 15%、附体首领 50%（点数 ≥ 2000）；设置可把平时改成 0 |
| 5 俘获灌注者可招募 | 是 |
| 6 离体按神不同 | 是（提尔投降、伊尔斯维脑损伤、维拉基斯化灰、其余正常） |
| 7 班恩挂塞伊 | 是 |
| 8 神名用费伦原名 | 是 |
| 9 实现要求 | 即本文 |

### 3.2 定义

- **派系扩展** `DeityExtension { deity: LP_DeityDef }`。新 `LP_DeityDef`（简单 Def：label、颜色、描述、灌注等级索引、附体技能配置）8 条：班恩、苏尔特、伊尔斯维、提尔、洛山达、维拉基斯、塔洛斯、马拉尔。挂法：`LP_FaerunHostile`→班恩，`LP_EmberClan`→苏尔特，`LP_IllithidColony`→伊尔斯维，`LP_Zealots`→提尔，`LP_FaerunColony`→洛山达（只在它对玩家敌对时生效），吉斯洋基雇佣兵 PawnKind（塞伊里的 ~3%）→维拉基斯（**按兵种**而不是派系：PawnKind 扩展优先于派系扩展）。原版 `Pirate`/`PirateWaster`→塔洛斯，`TribeRough`/`TribeSavage`→马拉尔，用 Patch 加扩展，设置关闭时代码里忽略。
- **特性** `LP_DivineInfusion`：8 个 degree（1～8 对应上面顺序），每个 degree 的 label “神力灌注（班恩）”，`commonality 0`，不能被原版随机抽到（额外在 `PawnGenerator` 后缀里检查，若玩家殖民者意外带上则不移除，保留）。被动写 XML 统计（心智抗性等），触发效果写代码（`Harmony` 后缀：近战命中 `Verb_MeleeAttackDamage.ApplyMeleeDamageToTarget`、倒地 `Pawn_HealthTracker.MakeDowned`、致命伤 `PreApplyDamage`）。
- **健康状态** `LP_DivineVessel 神明附体`：一个 HediffDef，`HediffComp_DivineVessel` 按 `deity` 字段切换行为；发光颜色按神；标签前缀“××的化身容器”（用 `pawn.Name` 不改，只在 `LabelInBrackets`/信件里写）。免疫：眩晕、精神崩溃、Isekai 威压类心情（如有）。附体者不可被俘后保留状态：倒地瞬间触发离体并移除。
- **世界条件** `LP_TwilightYear 黄昏之年`：`GameConditionDef`，天空颜色偏暗红（`skyColors`），全世界；`durationDays` 设置值；前 3 天殖民者心情 -3“不祥的黄昏”。
- **事件** `LP_TwilightOmen 黄昏之兆`：信件 + 15 天后开始黄昏之年（用 `GameComponent` 存倒计时，不依赖事件队列以免被删）。触发：`WorldComponent_Twilight` 每天检查：第 ≥ 2 年、本年未触发、25% 概率（按年掷一次，在每年第 1 天掷）。开发者模式调试菜单“立即开始黄昏之年”“立即结束黄昏之年”。
- **材料** `LP_MiracleShard 神迹残片`：市值 300，`stackLimit 25`，可卖给同盟商队和黑网（加进它们的 `TraderKindDef` 收购列表）；无配方。
- **心情** `LP_RepelledVessel 击退了神的使者` +5，3 天，全殖民地，附体者倒地时。

### 3.3 生成挂钩

- Harmony 后缀 `PawnGroupMakerUtility.GeneratePawns`（或 `IncidentWorker_RaidEnemy.TryExecuteWorker` 之后遍历 lord 成员，照 1.24.0 寒冰魔裁剪的 `FactionGroups.Process` 位置）：
  - 解析每个 pawn 的神（PawnKind 扩展 > 派系扩展；无神则跳过）。
  - 每人以 p（平时 3%、黄昏 15%，× 黄昏强度）加特性。
  - 袭击点数 ≥ 2000 且黄昏之年进行中，50% × 强度 让领队（lord 的第一名/已有“领队”标记者）加附体；一场最多一个。
  - 点数系数：灌注者 `combatPower ×1.3`、附体 ×2 —— 实现为生成后按超出的战斗力移除末尾的普通成员（照寒冰魔裁剪的方式），不改 PawnKind。
- 猎巫狂热分子、余烬、殖巢、塞伊现有的生成路径都要走到这个挂钩；写检查确认。

### 3.4 八位神的技能

按构思第三节逐条实现，D 值在黄昏之年中生效（×1.5 × 黄昏强度）。实现要点：
- 所有附体技能用 `HediffComp` 的 `CompPostTick` 计时自动触发或 Harmony 事件，不写 AI。
- 班恩“暴君之令”缴械：调用 `pawn.equipment.TryDropEquipment`，心智抗性 `LP_MindResist` 作为抵抗概率。
- 苏尔特“炉心”：伤害次数计数存在 comp 里，近战伤害加成用 `DamageInfo` 后缀乘。
- 伊尔斯维“低语光环”：狂乱 = `MentalStateDefOf.Berserk` 10 秒（用自定义短时 MentalState 或 `Berserk` + 定时恢复）；免疫沿用蝌蚪免疫判断。
- 提尔“审判之庭”：10 格内敌方的 `Ability.CanCast` 后缀返回 false（原版 Ability、VPE 的 Ability 子类都走同一基类，确认 VPE 的类继承自 `RimWorld.Ability`）。“罪行”读 `pawn.records`：Kills humanlike、器官摘取 BodiesStripped? 不存在时用 `RecordDefOf.KillsHumanlikes` + 食人心情记录；巫妖、死灵造物算罪行 10。投降 = 倒地瞬间移除状态、加 `Hediff` 使其“可俘虏且不流血”，招募难度 ×0.5 用现有的夺心魔式 `RecruitDifficulty` 后缀。
- 洛山达“光柱”：复用 `LP_Radiant` 伤害；复苏用 1.15.0 的“冷却保命”框架（半兽人不屈坚韧在用）。
- 维拉基斯“星界突袭”：复用 1.18.0 星界跳跃；“女王的复仇”生成 2 名吉斯洋基雇佣兵（塞伊兵种）；化灰 = `pawn.Corpse.Destroy()` + 灰烬 filth，发信说明。
- 塔洛斯：落雷用 `WeatherEvent_LightningStrike`；强制雷雨 = 附体者在地图上时 `map.weatherManager.TransitionTo(WeatherDefOf.RainyThunderstorm)` 每 2 小时重设。
- 马拉尔：狩猎号令 = 地图 2～4 只野生动物 `Manhunter`；驯兽反噬 = 殖民地动物 30 秒 `ManhunterPermanent`→ 定时恢复（简单做法：30 秒 `Manhunter` 后 `RecoverFromState`）。
- **牧师路径神圣伤害对附体者 ×1.5**（VPE 桥接里加，无 VPE 时不做）。

### 3.5 奖励与俘获

- 灌注者被俘/招募保留特性，效果照常（黄昏之年里同样加强）。
- 附体者倒地/死亡掉 1～2 神迹残片，全殖民地 +5 心情 3 天。
- 提尔投降者：招募难度 ×0.5。

---

## 4. 自检要点（`EventChecks.cs`）

1. 传送门：事件可触发、裂口生成在空地、4 个位面各生成第 1 波正确生物、第 3 波追加单位、驱散粉尘关闭、打掉关闭、巨灵命令（成功/失败各一次用固定随机）、3 天崩溃、关闭后位面残渣掉落与野兽退出狂暴、强化药水 2 个配方存在、存读档往返（裂口计时和波次保留）。
2. 巨人迁徙：三种组成各生成一次、扎营、讨食营养值计算（火巨人/泰坦/食人魔各一人的队）、给食物扣除正确并留礼物、不给偷粮触发、留下居住接受后加入且派系好感变化、拒绝后离开、敌对派系不触发。
3. 黄昏之年：条件开关、15 天倒计时存读档、天色、心情前 3 天；8 位神灌注特性各生成一次（固定随机）并验证被动统计；8 位神附体各 1 次：光环、自动技能至少触发一次（推进 tick）、离体效果（提尔投降可俘虏、伊尔斯维脑损伤、维拉基斯化灰、苏尔特爆燃、其余正常）；点数裁剪（2000 点袭击里灌注者 ×1.3）；神迹残片掉落与心情；猎巫/塞伊/余烬/殖巢四条生成路径都挂到钩子；原版派系开关关闭时海盗不带神力；特性 commonality 0 且随机殖民者不带。
4. 全部：Build、Core PASS；Appearance/HealthCompat 不受影响。

## 5. 顺序

传送门 → 巨人迁徙 → 黄昏之年（先框架 + 班恩/苏尔特/伊尔斯维/提尔，再洛山达/维拉基斯，最后塔洛斯/马拉尔）→ 说明与报告。若时间不够，塔洛斯/马拉尔可以留到 1.28.1，在报告里写明。
