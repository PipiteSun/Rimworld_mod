# 背景特性 1.32.0 实现报告

要求见 `ArtSource/Traits-1.32.0-spec.md`。版本 1.31.1 → 1.32.0。

## 改动的文件

新文件
- `Defs/Traits_Backgrounds.xml`：8 个 TraitDef、7 个 ThoughtDef、1 个隐藏辅助属性 `LP_SoldierCohesion`。
- `Source/BackgroundTraits.cs`：设置、出现权重、StatPart、局外人组件、ThoughtWorker、Harmony 补丁。
- `Source/BackgroundTraitChecks.cs`：检查组 `Traits`（`Test.ps1 -Only Traits`），14 条。
- `ArtSource/Traits-1.32.0-spec.md`、本报告。

修改
- `Source/Core.cs`：`ExposeData` 加 `ExposeBackgrounds()`；设置页左栏在“巨人”分组后加 `BackgroundSettings.Draw(l)`；`WriteSettings` 加 `BackgroundTraits.ApplySettings()`。
- `Source/Checks.cs`：完整检查里登记 `CheckTraits` / `PrepareTraitsSave` / `CheckTraitsAfterLoad`；PASS 摘要加一句；自测开始时调用 `SuppressBackgroundsForTests()`（见下文“测试隔离”）。
- `Bridges/VPE/Source/VPEBridge.cs`：`ClassExperience.Grant` 记下命中的路径，侍僧的牧师路径、贤者的法师路径经验 ×1.25。
- `Bridges/VPE/Source/BridgeChecks.cs`：加 1 条 VPE 检查（侍僧牧师经验 ×1.25）。
- `About/About.xml`：版本 1.32.0，description 加 1.32.0 更新说明。
- `使用说明.txt`：标题版本、内容一览一行、第二部分新增【1.32.0 背景特性】、更新日志 1.32.0。
- 两个 DLL 已重新编译（`1.7/Assemblies/LichPhylactery.dll`、`Bridges/VPE/Assemblies/LichPhylactery.VPEBridge.dll`）。

移走
- `Defs/CraftTraits.xml`：0 字节的空文件（今天 01:42 由 Isekai 那边的会话误留，内容在 IsekaiKillMilestones 里），游戏载入时会报红字 `unknown parse failure`，会让 Test.ps1 的日志检查失败、玩家日志里也有红字。已**移到** `ArtSource/backup-1.31.1/Defs/CraftTraits.xml`（没有删除）。根目录的 `ItemsPotions.cs`、`README.txt` 和 `Source/CraftTraits.cs` 也是 0 字节的空文件，不影响载入和编译，没动。

备份：`ArtSource/backup-1.31.1/`（按相对路径）包括 About.xml、使用说明.txt、Source/Core.cs、Source/Checks.cs、Bridges/VPE/Source/VPEBridge.cs、Bridges/VPE/Source/BridgeChecks.cs、两个旧 DLL 和 Defs/CraftTraits.xml。BridgeChecks.cs 是改完以后才想起来备份的：备份是把我加的 7 行精确删掉还原出来的，字节数与原文件一致（36733）。

## 共同规则的实现

- 8 个特性都带 `exclusionTags: LP_Background`。原版 `TraitDef.ConflictsWith` 两个方向都检查 conflictingTraits 和 exclusionTags（已看 IL 确认），所以一人最多一个背景；贵族 `conflictingTraits: Ascetic` 对苦行一方同样生效。和 Isekai、人类异禀、神兽血裔的特性都没有互斥。
- 出现权重：`TraitDef.commonality` 是私有字段，用 Harmony 的 `FieldRefAccess` 写。权重 = 0.3 × 出现率/100，开关关掉时为 0。游戏启动时（`[StaticConstructorOnStartup]`）、设置页每帧绘制时和保存设置时都会写一次，所以拖动滑条立即生效。滑条按 5% 取整。已有的特性不受影响，老存档不补发。
- 死灵造物：`Constructs.Create` 和红袍的 `FactionConstructs.MakeBound` 本来就会清掉生成出的全部特性；另加 `TraitSet.GainTrait` 前缀：背景特性加给死灵造物（有 `Hediff_Construct`）或非类人时直接拒绝。动物、机械体本来就没有特性。
- 心情：记忆型用 ThoughtDef（`durationDays` 1）；情境型写 ThoughtWorker，除民间英雄的“英雄在此”外都带 `requiredTraits`，worker 里也再判断一次特性。描述里写了效果数字。

## 各特性实际怎么做的

| 特性 | 实现 |
|---|---|
| 侍僧 | `statFactors MeditationFocusGain ×1.2`（MayRequireAnyOf 皇权/生物科技，原版这个属性只在这两个 DLC 下存在）。仪式：Harmony 后缀 `LordJob_Ritual.AddParticipantThoughts`，给 `totalPresenceTmp` 里每个在场的侍僧 +3“神圣时光”（1天）。VPE：`Grant` 里 ×1.25。 |
| 罪犯 | 后缀 `PrisonBreakUtility.InitiatePrisonBreakMtbDays`：囚犯是罪犯时越狱平均间隔 ÷2（几率 ×2）；同一地图上有罪犯背景的自由殖民者时再 ÷0.75（几率 ×0.75），检视说明里各加一行。黑网：`TradePriceImprovement` 的 StatPart，交易对象派系是 `LP_BlackNetwork` 时 +0.10。招募：`InteractionWorker_RecruitAttempt.Interacted` 前缀记下抵抗、后缀里把这次降低的量再多降 30%。 |
| 民间英雄 | `ThoughtWorker_HeroPresent`：每张地图缓存“被征召的民间英雄”名单（250 刻刷新），殖民者只和名单里的人比距离（≤6 格，本人除外）。倒地：后缀 `Pawn_HealthTracker.MakeDowned`（私有方法，按名字打补丁），玩家阵营的英雄倒地时给各地图和玩家商队里的其他自由殖民者 -3“英雄倒下”（1天）。 |
| 贵族 | `statOffsets SocialImpact +0.15`，`disabledWorkTags Cleaning, Hauling`，`conflictingTraits Ascetic`。卧室：`ThoughtWorker_NobleBedroom` 看自己床所在房间（营房也算；床在野外或没有床时不算），印象 <40（原版“体面”起点）时 -4。荣誉：前缀 `Pawn_RoyaltyTracker.GainFavor`，正数 ×1.2 四舍五入。 |
| 贤者 | `statFactors ResearchSpeed ×1.1、ReadingSpeed ×1.3`。看过 IL：`Book.OnBookReadTick` 每刻的收益 = 读书速度 × 房间加成 × 刻数，所以读书收益 +30%。VPE：`Grant` 里法师路径 ×1.25。“无书可读”：所有地图的 `allBuildingsColonist` 里没有 `Building_Bookcase` 和 `Building_ResearchBench` 时 -2，结果缓存 250 刻。 |
| 士兵 | `AimingDelayFactor` 的 StatPart：征召中 ×0.9。命中：射击精度和近战命中在 StatPart 之后还有原版技能曲线，直接用 StatPart 乘会被曲线吃掉，所以改成隐藏属性 `LP_SoldierCohesion`（3 格内有另一名同阵营、未倒地的士兵背景小人时为 1.05），挂在这两个属性的 `postProcessStatFactors` 上：曲线之后乘 1.05，再受原版上下限约束。“有没有战友”按小人缓存 120 刻。 |
| 隐士 | `statFactors MeditationFocusGain ×1.3`，`disallowedThoughts SleptInBarracks`。`ThoughtWorker_HermitRoom`：床所在房间里还有别人的非医疗床、或者自己的床有别的主人时 -4（第 1 档）；否则房间是卧室时 +3（第 0 档）。社交：前缀 `Pawn_InteractionsTracker.TryInteractRandomly`，一半概率当作已经聊过直接返回，原版随后重新按平均间隔等待，主动社交频率约 ×0.5。 |
| 局外人 | 给所有类人 ThingDef 加 `CompBackground`（启动时注入）：每 60 刻判断一次，只有局外人才计时；站在无屋顶、又不在居住区的格子上时 `outdoors=true`，`MoveSpeed` 的 StatPart ×1.1；在屋顶下时累加 `roofedTicks`，否则清零；不在地图上（商队等）算在野外。`roofedTicks` 存进存档（字段名 `lpOutlanderRoofedTicks`，0 时不写）。满 3 天（180000 刻）-3“闷坏了”。野生植物：`PlantHarvestYield` 的 StatPart，当前工作目标是 `sown == false` 的植物时 ×1.25（原版收获产量 >1 时按倍数加产量，已看 IL 确认）。坏天气：`disallowedThoughts SoakingWet`，另有 BlowingSand（奥德赛）、GrayPall（异象），都用 MayRequire。 |

我核对过 IL 或反射的入口方法：`LordJob_Ritual.AddParticipantThoughts`、`PrisonBreakUtility.InitiatePrisonBreakMtbDays`、`InteractionWorker_RecruitAttempt.Interacted`、`Pawn_RoyaltyTracker.GainFavor`、`Pawn_HealthTracker.MakeDowned`、`Pawn_InteractionsTracker.TryInteractRandomly`、`TraitSet.GainTrait`、`ThingComp.CompTickInterval`（`Pawn.TickInterval` 会调用）、`StatWorker.FinalizeValue`（先算 StatPart，再算曲线，再算 `postProcessStatFactors`，最后夹上下限）。

## 简化与偏差（请过目）

1. **罪犯看守**：原版没有“哪个狱卒在看守哪个囚犯”的概念，按要求里的备选方案做：同一张地图上有罪犯背景的自由殖民者时，所有囚犯越狱几率 ×0.75（不是 ×0.5）。
2. **侍僧、隐士的冥想专注**用 `statFactors`（×1.2 / ×1.3），没用要求里提示的 `+0.2` 加法：原版基础值是 0.5，加 0.2 实际是 +40%。
3. **仪式入口**选的是 `AddParticipantThoughts`：仪式正常结束、有人参加时才会走到（取消、无人参加时不给）；“在场的人”用原版的出席名单（参与者和观众都算）。如果某种仪式的 LordJob 子类完全重写了 `ApplyOutcome` 而不调用基类，就不会给心情（原版里没发现这种情况，没逐个验证）。
4. **士兵命中 +5%** 走隐藏属性 + `postProcessStatFactors`，是在原版技能曲线之后乘。
5. **原版上限照旧**：植物收获产量上限 150%、交易价格改善上限 39.5%。技能已经很高的局外人或罪犯可能会碰到上限，加成就少一些。
6. **隐士社交 ×0.5** 只管他主动发起的随机互动，别人找他搭话不受影响。
7. **民间英雄“英雄倒下”** 只在玩家阵营的英雄倒地时触发。
8. **测试隔离**：自测开始时把背景出现权重压成 0，并清掉地图上现有小人的背景；背景检查组自己临时按默认设置恢复，结束后再压回去。这样贵族不搬运、士兵改命中、局外人改移速之类的随机背景就不会让老检查不稳定。只影响自测进程，正常游戏不受影响。

## 测试

检查组 `Traits` 共 14 条，再加 VPE 档 1 条，新增检查合计 15 条：
1 八个特性加载、权重 0.3；2 300 个随机小人没人有两个背景；3 有背景的比例在 3%～25%；4 关掉开关后权重 0、100 人里 0 个，200% 时权重 0.6、比例上升；5 死灵造物没有背景（权重临时调到 50 去生成骷髅，GainTrait 也被拒绝）；6 侍僧冥想专注 ×1.2；7 罪犯囚犯越狱几率 ×2，殖民地有罪犯 ×0.75；8 民间英雄征召时 6 格内有“英雄在此”，9 格外没有，解除征召后消失；9 贵族不能分配清洁、搬运，与苦行双向互斥，700 个随机小人里没有同时带两者的；10 士兵征召瞄准 ×0.9，3 格内有士兵战友时射击、近战命中 ×1.05；11 隐士独占卧室 +3，同住 -4，营房心情被屏蔽；12 局外人户外移速 ×1.1，屋顶下没有加成，屋顶下 3 天 -3，淋湿心情被屏蔽；13（读档后）背景特性和屋顶计时 12345 保留；14 民间英雄倒地时全殖民地 -3（英雄本人没有），持续 1 天。VPE 档：侍僧牧师路径 1000 社交经验得到 12.5 灵能经验，每日计数仍计 10。

结果
- `pwsh Source/Test.ps1 -Only Traits`：最后一次 **PASS: 14 checks (only Traits); save/reload**，日志没有红字。中间失败过几次，都已修好：囚犯牢房标记要靠 `Room.Notify_RoomShapeChanged` 刷新（测试夹具问题）；夹具小人自己随机带了背景；`MakeDowned` 不认 `forceDowned`，改为直接调用；新加的辅助属性被原版标成“不可变”而被永久缓存（产品 bug，已在启动时清掉标记）；营房的 `Room.Owners` 是空的（产品 bug，改为逐张床查主人）；屋顶会影响移速，改为同一格比较。
- `pwsh Source/Test.ps1 -VPE`：**PASS: 277 checks (classes only)**（原来 276 条，加 1 条）。
- **完整检查（只跑了一次）：FAIL after 1234 checks**，失败的是 `Thayan raid only uses red wizards, ...`（`FactionChecks.cs:142`）。原因与本次改动无关：1.30.0 起塞伊远征军在 ≥800 点的战斗组里有 35%×季节系数的概率塞进霜巨人掠夺者和冬狼，而派系检查生成 1800 点战斗组时没有固定 `FrostGiantRaid.TestRoll`，检查的兵种名单里也没有这两种。这是老的随机失败，只是没在已知清单里。按要求没有重跑，所以完整检查里排在后面的组（包括背景特性组在完整模组列表下的那一轮）这次没跑到。前 1234 条全部通过，日志里除了这条失败和同一处派系清理时的一条“Tried to destroy already-destroyed thing”，没有其他红字，也没有任何和背景特性有关的报错。

## 待办

1. 建议把那条随机检查修成确定性的（在 `CheckFactions` 的塞伊循环前设 `FrostGiantRaid.TestRoll=1f`，结束后清空），然后再跑一次完整检查，确认背景特性组在完整流程里也通过。需要的话我来改。
2. 设置页的开关和滑条没有做界面截图，建议进游戏看一眼左栏“巨人”下面的“背景特性”分组。
3. 平衡（0.3 的权重、各项数值）按要求原样实现，没调整。
4. 侍僧的仪式心情、罪犯的招募和黑网价格、贵族荣誉、贤者“无书可读”、局外人采集野生植物、隐士社交频率没有单独写自动检查（检查数量限制在 15 条以内）；代码路径都很短，入口方法都核对过。
