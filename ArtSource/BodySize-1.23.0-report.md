# DND边缘大陆 1.23.0 巨躯规则 实现报告

日期：2026-10-24。规格：`ArtSource/BodySize-1.23.0-spec.md`。备份：`ArtSource/backup-1.22.1/`（含旧 DLL）。

## 0. 开工前核对：原版 HealthScale 怎样起作用（反编译 1.7 Assembly-CSharp.dll）

用 System.Reflection.Metadata 扫了所有调用 `Pawn.get_HealthScale` 的原版方法并看了 IL：

| 原版方法 | 公式 | 含义 |
|---|---|---|
| `Pawn.get_HealthScale` | `CurLifeStage.healthScaleFactor × RaceProps.baseHealthScale` | 本模组 postfix 再乘巨躯因子 |
| `BodyPartDef.GetMaxHealth` | `CeilToInt(hitPoints × HealthScale)` | 部位生命上限 |
| `HediffSet.CalculateBleedRate` / `Hediff.BleedRateScaled` | `失血 ÷ HealthScale` | 越大流血越慢 |
| `Hediff_Injury.PainOffset` / `Hediff_MissingPart.PainOffset` | `疼痛 ÷ HealthScale` | 越大越不疼 |
| `Pawn_HealthTracker.HealthTickInterval` | `愈合量 × HealthScale × 0.01 × InjuryHealingFactor` | 越大愈合越快 |
| `Pawn_HealthTracker.LethalDamageThreshold` | `150 × HealthScale` | 致死伤害阈值 |
| 另有 `SummaryHealthPercentImpact`、`ApplySmallPawnDamagePropagation` | 按 HealthScale 归一 | 健康百分比显示、小体型伤害传递 |

结论：1.22.1 的火巨人/龙体 ×1.5 只放大了部位生命；改成 HealthScale 因子后，失血、疼痛、愈合、致死阈值也一起按比例变化（这是规格要求的“生命总量”）。

另外核对：`Need_Food.MaxLevel` 游戏中读 `MaxNutrition` 属性，原版按 15 刻缓存；`MaxNutrition` 含 `StatPart_BodySize`。牛头人冲撞 `Minotaur.Charge` 直接 `TakeDamage(15×BodySize)`，不经过 `MeleeDamageFactor`，所以原本就不会被巨躯近战倍率再乘一次，不需要改代码。

## 1. 改动文件

新增：
- `Source/BodySize.cs`：巨躯规则本体。设置项（`Settings.bodySizeRule`、`bodySizeStrength`）；`BodySize.Size/HealthFactor/MeleeFactor`；按角色缓存（`ConditionalWeakTable`，线程安全、不泄漏）；`get_HealthScale` postfix；`MeleeDamageFactor` 的 `StatPart_BodySizeMelee`（说明显示“巨躯：×130%”）；`Notify_GenesChanged` postfix 刷新缓存与胃容量；伤口缩放 `Rescale`；`Registry` 上的规则版本记录与迁移 `MigrateBodySize`。
- `Source/BodySizeChecks.cs`：规格 D 节的检查。

修改：
- `Source/LimbHealth.cs`：`PartHealth.WithoutReinforcement` 删掉火巨人 ×1.5 和龙体 ×1.5；只在迁移测量 1.22.1 旧值时（`BodySize.LegacyPartFactor`）乘旧系数。巫妖强化层、`TemperingCompatibility` 不动。
- `Source/ForceDragon.cs`：删掉 `BodyHealthFactor`；`SetAge` 后调用 `BodySize.Notify_SizeChanged`（龙龄变化刷新缓存和胃容量）。
- `Source/Core.cs`：设置保存 `ExposeBodySize`；设置页左栏（巨魔复活下面）加开关和滑条；`WriteSettings`（关设置窗口）调用 `BodySize.ApplySettings`；`Registry.ExposeData` 存规则版本；`Registry.FinalizeInit` 最先调用 `MigrateBodySize`。
- `Source/Checks.cs`：接入 `CheckBodySize`（Run 末尾）、`CheckBodySizeConstructs(skeleton,golem)`、`CheckBodySizeAfterLoad`（AfterLoad 最前，比较完移除测试角色）。
- `Source/ForceDragonChecks.cs`：龙体检查改成新口径（HealthScale 因子 1.5、部位上限 = ceil(hp×HealthScale)）。
- `Source/MinotaurGithChecks.cs`：冲撞检查加上“牛头人巨躯近战 >1 但冲撞仍是 15×体型”。
- `Source/HealthCompatibilityChecks.cs`：火巨人 + 锻体、巨魔 + 泰坦成长的组合检查，并加入存读档/复活快照。
- `Defs/FireGiant.xml`、`Defs/ForceDragon.xml`、`Defs/Troll.xml`、`Defs/Minotaur.xml`：基因描述改为巨躯规则说法。
- `使用说明.txt`：标题 1.23.0、设置页说明、内容一览、新增第二部分【巨躯规则】、更新日志 1.23.0、旧条目加注释。
- `About/About.xml`：版本 1.23.0，描述补一句。
- `1.7/Assemblies/LichPhylactery.dll`、`Bridges/MythicBloodlines/Assemblies/LichPhylactery.MythicBridge.dll`：重新编译（pwsh 7 的 `Source/Build.ps1`）。

## 2. 规则实现要点

- S = max(火巨人, 巨魔) × 小体型扩展乘积（牛头人 1.3、矮人 0.8…）× 力能龙龄体型（每级 +5%）。与 `get_BodySize` 里这几个补丁一一对应，直接按来源相乘，不用 `pawn.BodySize / baseBodySize`——那样会把其他模组（VEF 基因、Isekai 等）和构造体旧种族体型也算进去。
- 生命 H = 1 + 0.5·k·max(0,S−1)；有龙体或是成年火巨人（体型已到 2.0）时 H = max(H, 1.5)（Peter 2026-10-24 决定：滑条只提高、不降低成年火巨人生命）。近战 M = 1 + 0.3·k·max(0,S−1)。死灵造物（有 `Hediff_Construct`）H = M = 1。
- 关闭开关或强度 0%：M = 1；H = max(火巨人?1.5:1, 龙体?1.5:1)。
- 缓存：只缓存“来源”（S、是否火巨人/龙体/构造体），倍率按当前规则现算，所以改设置和迁移测量不需要清缓存。失效条件：基因变化（postfix）、龙龄变化（SetAge）、健康状态数量变化、基因数量变化、整数岁数变化、250 刻兜底（儿童体型随年龄连续增长）。
- 迁移：`Registry.bodySizeVersion`（新开局 −1，1.22.1 存档读档时缺字段 = 0，记录后 = 1）和记录时的开关/强度。读档或关设置窗口时，记录的规则与当前规则不同，就对每个角色（地图、世界、临时、命匣里的灵魂）测量“旧规则最大 / 新规则最大”，只在变小的部位按比例缩放伤口。

## 3. 新旧数值表（强度 100%，成年，人类部位：躯干 40、手臂 30）

| 种族 | 体型 S | 1.22.1 生命 | 1.23.0 生命 | 躯干/手臂 旧→新 | 1.22.1 近战 | 1.23.0 近战 |
|---|---|---|---|---|---|---|
| 普通人 | 1.0 | ×1 | ×1 | 40/30 → 40/30 | ×1 | ×1 |
| 矮人 | 0.8 | ×1 | ×1（小体型不减） | 不变 | ×1 | ×1 |
| 牛头人 | 1.3 | ×1 | ×1.15 | 40/30 → 46/35 | ×1 | ×1.09 |
| 巨魔 | 1.5 | ×1 | ×1.25 | 40/30 → 50/38 | ×1 | ×1.15 |
| 火巨人 | 2.0 | ×1.5（部位） | ×1.5（HealthScale） | 60/45 → 60/45 | ×1 | ×1.3 |
| 火巨人 开炉心过载 | 2.0 | 同上 | 同上 | 同上 | ×1.25 | ×1.3×1.25 = ×1.625 |
| 火巨人儿童（9岁，测试） | 1.5 | ×1.5 | ×1.25 | 躯干 42 → 35（含儿童生命阶段系数） | ×1 | ×1.15 |
| 火巨人 + 龙体 | 2.0 | ×2.25 | ×1.5 | 90/68 → 60/45 | ×1 | ×1.3 |
| 力能龙 幼龙（有龙体） | 1.0 | ×1.5 | ×1.5 | 60/45 → 60/45 | ×1 | ×1 |
| 力能龙 伟龙（有龙体） | 1.2 | ×1.5 | ×1.5（max(1.1,1.5)） | 不变 | ×1 | ×1.06（龙爪再 ×2，合计 ×2.12） |
| 力能龙 伟龙（无龙体） | 1.2 | ×1 | ×1.1 | 40/30 → 44/33 | ×1 | ×1.06 |
| 骷髅仆役 / 血肉巨像 / 知识死灵 | — | 材料生命 | 不变 | 不变 | 巨像 ×5 | 不变 |

强度 50%：牛头人 ×1.075/×1.045，巨魔 ×1.125/×1.075，成年火巨人 ×1.5（保底）/×1.15。强度 200%：牛头人 ×1.3/×1.18，巨魔 ×1.5/×1.3，火巨人 ×2.0/×1.6。强度 0% 或关闭：火巨人、龙体 ×1.5，其余 ×1，近战全为 ×1。成年火巨人生命在任何设置下都不低于 ×1.5。

## 4. 测试结果

编译：pwsh 7 运行 `Source/Build.ps1`，主程序集和神兽血裔联动程序集都通过。

**成年火巨人保底 ×1.5 修改后复测（最终版本）**：Core **PASS 2536**（新增“强度 50%：成年火巨人保持 ×1.5、体型加成减半”一项），HealthCompat None/Human/Mythic/Both **PASS 354/406/336/408**，日志干净。除 BodySize.cs 和检查外只改了设置提示文字（Core.cs）和文档，Full 没有重跑；下表是保底修改之前的结果。

| 档位 | 最终结果 |
|---|---|
| Core | **PASS 2535**，日志干净（共跑 5 次：4 次 PASS，1 次失败在老检查“精灵链甲闪避 +5%”，与本次改动无关，下一次通过） |
| `-Mythic` | **PASS 2647**，日志干净（之前两次分别失败在“力场伤害伤到普通人”“天工点化标记物品”，都是随机老检查，重跑通过） |
| `-HealthCompat -HealthProvider None` | **PASS 354** |
| `-HealthCompat -HealthProvider Human`（人类异禀） | **PASS 406** |
| `-HealthCompat -HealthProvider Mythic`（神兽血裔） | **PASS 336** |
| `-HealthCompat -HealthProvider Both` | **PASS 408** |
| `-Full`（完整模组列表） | **PASS 2701**；脚本退出码 1 只因已知第三方报错：GD3.HarmonyInit 静态构造、Character Editor `[Ref 78A3A6EB]`、`colonist_booze_tracker` 空键（与 1.20.0/1.22.0 报告相同），日志里没有本模组的报错 |

新增检查（`[LICH-TEST] OK …`）全部通过，按规格 D 节：
1. 成年火巨人 HealthScale 1.5、所有部位上限与 1.22.1 测量值相同、近战 ×1.3、属性说明含“巨躯：×130%”。
2. 巨魔 1.25/×1.15，牛头人 1.15/×1.09，矮人 1/×1，普通人 1/×1；巨魔原版部位上限 = ceil(hp×HealthScale)；火巨人儿童（9 岁）躯干 42→35。
3. 幼龙龙体 ×1.5；伟龙 S=1.2 时仍 ×1.5（不乘 1.1）、近战 ×1.06；火巨人+龙体 ×1.5，旧值测量为 ×2.25。
4. 骷髅仆役、血肉巨像：倍率均为 1，部位上限与 1.22.1 测量值相同。
5. 巫妖强化 2 层叠在巨魔 ×1.25 之上：上限 = ceil(ceil(hp×HealthScale)×1.4)。
6. 强度 0% 与关闭：火巨人/龙体/混血 ×1.5，巨魔、牛头人 ×1，近战 ×1；强度 200%：火巨人 2.0/×1.6，巨魔 1.5/×1.3，牛头人 1.3/×1.18。
7. 模拟 1.22.1 存档（火巨人+龙体，手臂/腿 60% 伤、躯干 30% 伤）迁移后比例 0.3/0.6/0.6 不变、无部位被毁、未死亡；同设置再迁移不改任何东西；强度调到 50% 时巨魔、火巨人伤口比例不变，龙体混血不变；调回 100% 伤口绝对值不变（不白治）。
8. 存读档后 HealthScale、近战属性、两个倍率都不变；读 1.23.0 存档不会再次缩放伤口；规则记录随存档保存。
9. 牛头人冲撞伤害 19.5 = 15×1.3，没有再乘巨躯近战 ×1.09。
10. 生命兼容档：火巨人 + 锻体（人类异禀/神兽血裔）上限 = round(ceil(hp×1.5)×1.5)，巨魔 + 泰坦成长 HealthScale = 1.25×1.5 相乘；两者都加入存档/复活快照，比较通过。
另外：体型变化时胃容量立即刷新（普通人加火巨人本源 1→2，移除后回到 1 且饱食截到上限）。

Full 档里 `熔核禁卫` 的炉心过载检查实测：近战 1.3 → 1.625（×1.25），即火巨人开炉心过载总倍率 ×1.625。

Full 档的不稳定：1.23.0 跑了 6 次 Full，失败位置每次不同（圣水、巨魔酸蚀记录、红袍商队生成、炉心过载移速等随机检查）。为了确认不是这次改出来的，我临时换回 `backup-1.22.1` 里的旧 DLL 跑了一次 Full，旧版也失败在随机位置（念力投掷被护甲偏转），跑完已换回新 DLL（哈希核对一致）。唯一一个由本次改动引起的 Full 失败是火巨人旧检查“生命 ≥ 旧值×1.49”：其他模组让原版 HealthScale 不是整数时，新算法只取整一次，可能比 1.22.1 少 1 点（44 → 65，旧为 66）。已把该检查改成“与 1.22.1 测量值相差不超过 1”。

## 5. 偏离要求 / 需要确认的地方

1. **（已解决）强度 0% 等同关闭**（D.6 写“强度 0% 时火巨人/龙体仍是旧的 ×1.5”）。因此滑条在 0% 和 1%～99% 之间对火巨人不连续：0% 时 ×1.5，50% 时 ×1.25，100% 时又回到 ×1.5。巨魔、牛头人是连续的。如果希望连续，可以改成“火巨人在规则开启时也保底 ×1.5”，但那样火巨人儿童就不会再变脆（与 B 节矛盾）。**Peter 决定：成年火巨人生命保底 ×1.5**（H = max(体型规则, 1.5)，儿童仍随年龄增长，龙体照旧取较大值）。现在成年火巨人：0%/关闭/50%/100% 都是 ×1.5，200% 为 ×2.0，滑条连续且只升不降；伤口迁移按实测最大值比较，成年火巨人不会再出现“变小”，不缩放伤口。强度 0% 仍等同关闭（影响的只有火巨人儿童：0% 时 ×1.5，开启时随年龄）。
2. **关闭规则时的旧 ×1.5 也走 HealthScale**，不是 1.22.1 的“只改部位生命”。所以关闭后火巨人的失血、疼痛、愈合、致死阈值也按 ×1.5 变化（比 1.22.1 更耐打一点）；部位生命数值与 1.22.1 相同。这样开关前后只有一套生命路径，迁移和兼容更简单。
3. 规则开启后，火巨人（以及巨魔、牛头人）多了上面第 0 节列出的副作用：流血慢、疼痛轻、愈合快、致死阈值高。这是原版 HealthScale 的固有效果，符合“生命总量”的意思，但比单纯加部位生命强一些。
4. 设置变化的伤口缩放对**所有**已载入角色（含世界角色、商队、命匣里的灵魂），不只地图上的角色，避免以后回到地图时数值不一致。
5. 牛头人冲撞原本就不经过 MeleeDamageFactor，没有改代码，只加了检查。
6. 胃容量：原版本来就按 15 刻刷新最大饱食；这次在基因变化和龙龄变化时立即清缓存，并把超出新上限的饱食截掉（体型变小时以前会留着超量饱食）。
7. 完整模组列表下（原版 HealthScale 被其他模组改成非整数时），部位上限可能比 1.22.1 少 1 点（取整一次而不是两次）。核心环境下完全相同。
8. `Source/MoltenGuardChecks.cs`、`Source/FireGiantChecks.cs` 也改了（检查信息加数值、取整容差），已备份。
9. 测试里的 1.22.1 迁移是“把记录版本设为 0 再调用同一个迁移函数”，只传入测试角色，避免改动其他检查的伤口；完整存档的读档路径用同一个函数。
