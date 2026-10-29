# LichPhylactery 1.27.1 胃容量与“龙之长眠” 实现报告

规格：ArtSource/Satiety-1.27.1-spec.md。备份：ArtSource/backup-1.27.0/（保持相对路径，含 1.27.0 的 LichPhylactery.dll）。

## 0. 开工前核对原版（ilspycmd 反编译 1.7 Assembly-CSharp.dll）

| 原版位置 | 核对结果 | 对实现的影响 |
|---|---|---|
| `Need_Food.MaxLevel` | 游戏中 = `GetStatValue(MaxNutrition, applyPostProcess:true, cacheStaleAfterTicks:15)`；**不在游戏中**（生成开局角色、读档、FinalizeInit 时）= `BodySize × CurLifeStage.foodMaxFactor`，不读属性 | F 做成 MaxNutrition 的 StatPart，另给 `get_MaxLevel` 加后缀：非游戏状态也乘 F，两边一致 |
| `MaxNutrition` StatDef | 基础值 1，parts = `StatPart_BodySize` + `StatPart_LifeStageMaxFood`，cacheable | “原版最大营养”= 体型 × 生命阶段；F 变化时 `ClearCacheForThing` |
| `Need.CurLevel` | setter 截到 [0, MaxLevel]；存档直接读写 `curLevelInt`（读档不截断） | 缩放 CurLevel 时先清属性缓存 |
| 饥饿阈值 | `RaceProperties.FoodLevelPercentageWantEat`：杂食/肉食/不吃 **0.30**、蛋食 0.40、草食/树食 0.45；`PercentageThreshHungry` = WantEat × **0.8**（人 24%），`PercentageThreshUrgentlyHungry` = × **0.4**（人 12%），`CurLevelPercentage ≤ 0` 为 Starving | 龙改为 想吃 45% / 饥饿 20% / 非常饿 8%（规格值），补丁 `get_CurCategory` 和两个阈值 getter |
| `HungerLevelUtility.HungerMultiplier` | Fed **×1**、Hungry **×0.5**、UrgentlyHungry **×0.25**、Starving **×0** | 500%→225% 全程是 Fed（×1），天数计算不受减速影响 |
| `FoodFallPerTickAssumingCategory` | `lifeStage.hungerRateFactor × race.baseHungerRate × 2.6666667E-05 × 分类系数 × hediff 饥饿倍率 × 性格 × 床`，再加哺乳，再 × 代谢率曲线（未被覆盖基因的 biostatMet 之和）；**与体型无关**；成年人每天 1.6 营养 | 1.7 没有 HungerRate StatDef，“饥饿速率”= hediff 倍率 × 代谢曲线（× 性格）；龙龄 hediff 抵消代谢曲线 |
| `JobGiver_GetFood.GetPriority` | 无需求 0；分类 < Starving 且 `ShouldBeFedBySomeone` → 0；分类 < minCategory → 0；百分比 > maxLevelPercentage → 0；百分比 < WantEat → **9.5**；否则 0 | 后缀：原版给 0 时，成年龙低于 45% 或暴食中给 9.5（同样检查以上三条，另外排除征召、精神崩溃、倒地） |
| `FoodUtility.WillIngestStackCountOf` | `StackCountForNutrition(NutritionWanted, 单份营养)`，若 `maxNumToIngestAtOnce > 0` 再取较小，至少 1；药物用 defaultNumToIngestAtOnce | 原版饭菜（MealBaseIngredientless）上限 **1**，巧克力 4，生肉没有上限 |
| `Thing.IngestedCalculateAmounts` | 同样受 `maxNumToIngestAtOnce` 限制 | 两处都后缀：成年龙一次最多 3 份 |
| `Need_Food.NeedInterval` | 每 150 刻；只有 Starving 时营养不良 +0.0011325×(0.8～1.2)（每天约 0.453），否则减少 | 225% 时是 Fed，不会营养不良 |
| `ThoughtWorker_NeedFood` | Fed 无；Hungry 阶段0；UrgentlyHungry 阶段1；Starving 2+营养不良阶段 | 龙在 P≥100% 时没有饥饿心情 |
| 其他读阈值的地方 | `FeedPatientUtility`（≤ Hungry+2%）、`WorkGiver_Warden_DeliverFood/Feed`、`PawnUtility.WillSoonHaveBasicNeed`（Hungry+5%）、`PawnGenerator`（初始饱食取 Hungry/Urgent 中点）、`Need_Food.DrawOnGUI` 阈值刻度 | 都走被补丁的 getter，对龙一致；其他种族阈值不变 |
| `Caravan_NeedsTracker.TrySatisfyFoodNeed` | 分类 ≥ Hungry 时吃一次 `Ingested(NutritionWanted)` | 商队里龙低于 100% 才吃，不暴食（未改，见待定） |
| `Pawn_AgeTracker.RecalculateLifeStageIndex` | 生命阶段（儿童→成年）在这里更新 | 后缀：立即同步 F（龙成年时 1→5） |
| `GameComponent.FinalizeInit` | `Game.FinalizeInit` 先调组件，再把 ProgramState 设为 Playing | 旧存档迁移用 `LongEventHandler.ExecuteWhenFinished` 延到载入结束 |

## 1. 改动的文件

新增：
- `Source/Satiety.cs`：常量、F(p) 计算与 60 刻缓存、`StatPart_Stomach`（启动时注入 MaxNutrition）、F 变化缩放、暴食、Registry 存档字段与旧存档迁移、Harmony 补丁（MaxLevel 非游戏状态、SetInitialLevel、NeedInterval、RecalculateLifeStageIndex、CurCategory、两个阈值、GetFood 优先级、单次进食数量×2 处、饱食度提示）。
- `Source/SatietyChecks.cs`：本次检查，接入 Core（`Checks.cs` 的 Run / AfterLoad）。
- 本报告。

修改：
- `Source/ForceDragon.cs`：`Hunger(age)` 改为 `Satiety.DragonHunger(age)`；`Hediff_DragonAge` 成年时也除以代谢率倍率（缓存键始终含代谢率）；`Gene_ForceDragonHeart` 新字段 `binging`（存档键 `dragonBinging`）。
- `Source/BodySize.cs`：`Notify_SizeChanged` 先 `Satiety.Sync` 再截上限。
- `Source/Core.cs`：Registry `FinalizeInit` 调 `FinalizeSatiety()`，`ExposeData` 调 `ExposeSatiety()`（存档键 `satietyVersion`、`stomachFactors`）。
- `Source/Checks.cs`：调用两组新检查，PASS 文字加一项。
- `Source/BodySizeChecks.cs`：胃容量检查改为 原版体型×2 再×巨胃2 = ×4、百分比 50%。
- `Source/ForceDragonChecks.cs`：幼龙/成年龙饥饿速率检查改为 57.3% / 63.0%。
- `About/About.xml`：版本 1.27.1，1.27.1 条目放在 1.27.0 条目之前（1.27.0 保留）。
- `使用说明.txt`：标题 1.27.1；内容一览里火巨人、巨魔、力能龙裔、牛头人、象人、食人魔、天穹神裔的食量/胃容量；巨躯规则“胃容量”一句补充；新章节【1.27.1 胃容量与龙之长眠】；更新日志 1.27.1 放最前；1.23.1、1.23.2、1.14.0 里过时的龙食量句子按惯例加“（已被 1.27.1 取代）”。
- `Defs/ForceDragon.xml`（异种型简介、龙心效果说明）、`Defs/FireGiant.xml`、`Defs/Troll.xml`、`Defs/Loxodon.xml`、`Defs/Minotaur.xml`、`Defs/Ogre.xml`、`Defs/Titan.xml`、`Defs/Genie.xml`：只改描述文字，补胃容量。
- Bridges 没有新增 Defs 文件。

## 2. 规则与最终数值

胃容量倍率 F（乘在原版 MaxNutrition 上）：
- 成年力能龙裔（龙之心激活且不是婴儿/儿童）：**5**。
- 其他：max(1, max(火巨人, 巨魔) × 小体型扩展)，不算龙龄；构造体 1。

| 种族（成年） | F（显示上限） | 原版体型 | 相对普通人的实际胃容量 |
|---|---|---|---|
| 力能龙裔（幼龙～伟龙） | 500% | 1.0～1.2 | 5.0～6.0 |
| 火巨人 | 200%（儿童 1+年龄/18） | 2.0 | 4.0 |
| 巨魔 | 150% | 1.5 | 2.25 |
| 牛头人、象人 | 130% | 1.3 | 1.69 |
| 食人魔 | 160% | 1.6 | 2.56 |
| 巨灵 | 150% | 1.5 | 2.25 |
| 天穹神裔 | 250% | 2.5 | 6.25 |
| 寒冰魔、炎魔 | 不吃东西 | — | — |
| 矮人、鸟人、普通人、幼年龙、构造体 | 100% | — | 不变 |

龙的阈值（CurLevelPercentage）：想吃 45%（P=225%，仍是 Fed）、饥饿 20%（P=100%）、非常饿 8%（P=40%）、≤0 挨饿。暴食：低于 45% 开始，吃到 95%（P=475%）退出。饭菜等有单次上限的食物一次最多 3 份。

龙的总饥饿速率 H = 2.75 × 龙龄体型 / (1.6 × 3)，龙龄 hediff 倍率 = H / 代谢率倍率（力能龙基因代谢合计 < −5，倍率 225%）：

| 龙龄 | 体型 | H | 龙龄 hediff 倍率 | 每天营养 | 500%→225% |
|---|---|---|---|---|---|
| 幼龙 | 1.00 | 57.3% | ×25.5% | 0.917 | 3.0 天 |
| 少年龙 | 1.05 | 60.2% | ×26.7% | 0.963 | 3.0 天 |
| 成年龙 | 1.10 | 63.0% | ×28.0% | 1.008 | 3.0 天 |
| 古龙 | 1.15 | 65.9% | ×29.3% | 1.054 | 3.0 天 |
| 伟龙 | 1.20 | 68.8% | ×30.6% | 1.100 | 3.0 天 |

（以前成年龙是 基因225% × 龙龄140%～200% = 315%～450%。）幼年龙（婴儿、儿童）保持 1.23.1 的 100%，F=1，阈值原版。性格（如老饕 ×150%）、装备、其他健康状态照常相乘。

保持百分比：每个角色记录上次的 F（Registry `stomachFactors`，键 thingIDNumber，只记 F≠1）。基因增减（`Notify_GenesChanged`）、龙龄变化（`SetAge`）、生命阶段变化时立即同步，另外每 150 刻 NeedInterval 兜底（火巨人/巨魔儿童随年龄连续长）。新需求在 `SetInitialLevel` 时按当前 F 记录。旧存档（没有 `satietyVersion`）载入结束后：F≠1 的角色 CurLevel ×F（不超过上限）一次，之后版本为 1。迁移完成前 `Sync` 不动，避免和 FinalizeInit 里的铁胃等迁移重复放大。

界面：饱食度提示加“相当于常人饱食度 X%（上限 F）”，龙再加一行长眠说明（暴食中注明）。需求条没改（满条 = 500%），条上的原版阈值刻度对龙移到 20%/8%。

## 3. 测试

- `.\Build.ps1`：PASS（主程序集、VPE 联动、神兽血裔联动都编译通过）。
- `.\Test.ps1`（Core）：**PASS: 3337 checks**（1.27.0 为 3298）。跑了 6 次：4 次全部检查通过，但日志里都有读档红字 `Exception registering RimWorld.Genepack … same key`，脚本因此报“Errors found in test log”；1 次在存档那一步卡住超时；1 次停在老的重塑池检查 `wound proportions … survive mid-remolding save/load`（1.20.0、1.24.0 报告里记录过的老问题）。
- 对照：把 1.27.0 的 DLL（备份）换回去跑了 3 次（之后已换回 1.27.1 的 DLL，哈希核对一致）：2 次停在老的随机检查（进贡银币、神罚 10 格），1 次 PASS 3298 checks，**同样有 Genepack 红字**。所以 Genepack 红字是 1.26.0 起的老问题，不是本次改动带来的。
- 生成时的小现象（原版顺序，1.27.0 同样如此）：新生成的火巨人先按普通体型设好 80% 饱食，再加基因让原版体型翻倍，所以开局显示 40%；巨胃部分已按比例放大，不会更低。
- 新增检查（SatietyChecks.cs）：StatPart 只注册一次；阈值常量；成年龙上限=原版×5、属性说明“龙之胃：×500%”；五个龙龄的 H、实际 Fed 速率、上限；成年龙与伟龙逐个 NeedInterval 模拟 500%→225% ≈ 3 天（±0.05），到 225% 时 Fed、无饥饿心情、无营养不良；P=300% 不想吃、P=200% 想吃且 Fed 无心情、暴食到 95% 退出、退出后 P=300% 不再想吃、征召不强制；P=90% Hungry（有饥饿心情）、P=30% UrgentlyHungry、P=0 Starving；龙一次 3 份饭、普通人 1 份，实际 `Ingested` 吃掉 3 份；提示文字；幼年龙上限/速率/阈值原版；火巨人×2（“巨胃：×200%”）、巨魔×1.5、牛头人×1.3、象人×1.3、食人魔×1.6、神裔×2.5、矮人×1；加/去龙之心时百分比保持 60%；旧存档迁移只执行一次；存读档后记录、龙饱食和暴食状态不变。

## 4. 未做 / 待定

- **待 Peter 确认**：①成年龙 500%→225% 约 3 天（常量 `LongSleepDays`）；②大体型上限 = 体型倍率×100%（`Satiety.Compute`）。注意原版最大饱食本身已经乘体型，所以火巨人实际胃容量是普通人的 4 倍、神裔 6.25 倍；如果想要“显示上限=体型倍率”但不叠加原版体型，需要改公式。
- 商队里的龙仍按原版商队规则（低于 100% 才吃，一次一份），没有做商队暴食。
- 需求条没改成“常人刻度”，也没有在条上标 45% 的“想吃”刻度。
- `stomachFactors` 不清理已不存在的角色（只记 F≠1 的角色，量很小）。
- 只跑了 Core 档；Full、-Mythic、-VPE、-Isekai 档没有跑。
- 没有改存档、没有手动启动游戏（只跑了测试脚本的隔离进程）。
