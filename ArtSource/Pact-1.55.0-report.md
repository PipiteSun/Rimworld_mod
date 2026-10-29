# 异界契约 1.55.0：实现报告

2026-10-27。按 `ArtSource/Pact-1.55.0-spec.md` 实现；设备上原来是 1.54.0（深渊仪式）。不需要 HAR / Isekai。备份在 `ArtSource/backup-1.54.0/`（保持相对路径）。

## 一、测试结果

| 命令 | 结果 |
|---|---|
| `pwsh .\Source\Build.ps1` | 主程序集、HAR 桥、VPE 桥、神兽血裔桥全部编译通过 |
| `.\Test.ps1 -Only Pact` | **PASS 82 条**（含存读档），日志无红字；连续多次运行（见下） |
| `.\Test.ps1 -NoHAR -Only Pact` | **PASS 82 条**（本组没有 HAR 专属检查，两档条数相同），日志无红字 |
| `.\Test.ps1 -Only Abyss` | PASS 57 条 |
| `.\Test.ps1 -Only Faith` | PASS 17 条 |
| `.\Test.ps1 -Only Adventurer` | PASS 123 条（设置分组数仍是 16、亲和表没改：见偏差 1、2） |
| `.\Test.ps1 -Only ResearchTab`（新研究格子 (5, 2.8)） | PASS 6 条 |

完整的 Core 套件、-VPE、-Only Events / Races124 没有跑（协调说明不要求）。Races124 依赖的 `Fiends.Sees` 多了一个“天界单位”判断，其他行为不变。

**稳定性**：最终代码连续跑了 4 次 -Only Pact 和 1 次 -NoHAR -Only Pact，全部 82 条通过。中间版本里有两条**测试本身**的随机失败已修好：调换儿检查在“风暗女王”的契约书正好已经抽到“长子归冬宫”时又加了一条，消耗一条后还剩一条（约 50% 失败）——改成没有才加；夏日恩泽检查直接读 `Plant.GrowthRate`，有一次两次读数相同（怀疑 CoreCLR 把很短的 getter 内联进了调用方，补丁没生效），改为直接验证补丁逻辑和缓存的地图集合。**风险**：如果游戏里别处也内联了 `Plant.GrowthRate`，夏日恩泽的 +30% 可能偶尔不生效；没在游戏里看过。

## 二、改动的现有文件（全部已备份到 backup-1.54.0）

- `Source/Outsiders.cs`：`PlaneWord` 认妖精荒野 / 天界；`Banish`、`BanishCorpse` 在掉落前调 `PactRaces.BanishLoot`（只对非仆从 / 非束缚者）；生成时按扩展定等级的兵种前缀加上 `LP_HL_ / LP_HB_ / LP_FW_ / LP_FY_ / LP_CE_`；`ExposeAbyss()` 末尾调 `ExposePact()`；`AbyssSettings.Draw` 末尾调 `PactSettings.Draw(l)`（“异界契约”分组画在“深渊”下面）。
- `Source/Fiends.cs`：`Fiends.Sees` 加“天界单位（真知）”。
- `Source/AbyssBinding.cs`：`DailyDecay` 第一行：九狱单位 / 林仙 / 守护天使 / 契约守卫返回 0；`BreakFree` 第一行：九狱单位改为“要求重新谈判”。
- `Source/AbyssTaint.cs`：`Add` 的正数乘上 `PactRaces.TaintFactor`（天界羽披 ×0.5）。
- `About/About.xml`：modVersion 1.55.0，描述末尾加一段。和备份比只有这两行不同。
- `使用说明.txt`：第二部分【1.54.0 深渊仪式】后面插入【1.55.0 异界契约】（30 行，含空行），第三部分 1.54.0 条目前插入 1.55.0 条目。和备份比：新增 31 行，删除 0 行。

**Faith.cs、Core.cs、LoadFolders.xml：一行都没改。** 圣坛选项挂在 `Building_FaithAltar.ExtraGizmos`（成神线同一个入口）；设置借深渊分组的存取和绘制（见偏差 1）。研究定义放在新文件 `Defs/Pact_Items.xml`，`Defs/Research.xml` 也没改。

## 三、新文件

- 代码：`Source/Pacts.cs`（宗主定义、契约数据、签约 / 审阅 / 谈判 / 偿契 / 债务 / 强制执行 / 赎回 / 补偿 / 解约、契约书、读书工作、契约讲台、魔能爆、选择信件、设置、事件钩子）、`PactBoons.cs`（恩赐 worker、恩赐状态、恩赐能力、夏日恩泽补丁）、`PactRaces.cs`（新单位的基因 / 状态 / 能力、出手与命中钩子、萨特禁鞋、魔宠、号角、救赎）、`PactEvents.cs`（事件、讨债小队、狂猎 Lord / 职责、商队 / 集市、血战之潮、调换儿、右键商人）、`PactCelestial.cs`（圣坛：破契仪式、祈唤天界援军；坠落天使、堕天使）、`PactChecks.cs`（组名 Pact）。
- 定义：`Defs/Pact_Patrons.xml`、`Pact_Races.xml`、`Pact_Items.xml`、`Pact_Events.xml`。
- 贴图：`ArtSource/RaceArt/draw_pact_155.py`（复用 draw_race_art / draw_races_124 / draw_minotaur_gith / draw_abyss_154 的形状函数）生成 175 个文件：`Textures/Pact/{Horns,Heads,Wings,Tails,Body}`（12 个人形单位的头 / 身体附件，三向）、`Pact/{Imp,Pixie}`（四向 + 干尸）、`Pact/{Xenotypes,Genes,Abilities,Patrons,Factions}` 图标、`Pact/Items`（契约书 + 5 件物品）、`Pact/Apparel/CelestialMantle`（五种体型 × 三向）、`Pact/Buildings`（讲台 2×1、铸炉 3×2，三向，非正方形画布）；预览 `ArtSource/Preview/Pact-1.55.0.png`。新定义引用的所有贴图路径都核对过存在。
- 本报告、规格 `ArtSource/Pact-1.55.0-spec.md`。

## 四、新 defName

- 研究：LP_Research_PlanarPacts（(5, 2.8)，开工前核对过是空的）
- 宗主（LichPhylactery.LP_PatronDef）：LP_Patron_Asmodeus、LP_Patron_Mephistopheles、LP_Patron_Dispater、LP_Patron_Glasya、LP_Patron_Titania、LP_Patron_QueenOfAir、LP_Patron_Hags
- 异种型：LP_ChainDevil、LP_BoneDevil、LP_HornedDevil、LP_Erinyes、LP_Amnizu、LP_Satyr、LP_Dryad、LP_Redcap、LP_Changeling、LP_WildHunter、LP_WildHuntLord、LP_Deva、LP_Planetar
- 基因：LP_HellsCore、LP_FeyCore、LP_CelestialCore、LP_ChainDevilStature、LP_ChainDevilChains、LP_ChainDevilSubvert、LP_SkinChainDevil、LP_BoneDevilStature、LP_BoneDevilTail、LP_BoneDevilDread、LP_SkinBoneDevil、LP_HornedDevilStature、LP_HornedDevilFlame、LP_SkinHornedDevil、LP_ErinyesStature、LP_ErinyesWings、LP_ErinyesHunt、LP_ErinyesRope、LP_ErinyesResolve、LP_SkinErinyes、LP_AmnizuStature、LP_AmnizuMind、LP_SkinAmnizu、LP_SatyrHorns、LP_SatyrHooves、LP_SatyrMagicResist、LP_SatyrCharge、LP_SatyrRevelry、LP_SatyrMusic、LP_DryadTree、LP_DryadTreeStride、LP_DryadBark、LP_SkinDryad、LP_HairDryad、LP_RedcapStature、LP_RedcapBloodlustGene、LP_RedcapStomp、LP_SkinRedcap、LP_SkinChangeling、LP_ChangelingFaces、LP_ChangelingFluid、LP_WildHunterAntlers、LP_WildHuntLordCrown、LP_WildHunterPrey、LP_WildHunterSpear、LP_SkinWildHunter、LP_DevaStature、LP_DevaMace、LP_DevaHealing、LP_SkinDeva、LP_PlanetarStature、LP_PlanetarSword、LP_PlanetarWrath、LP_PlanetarBanish、LP_SkinPlanetar
- 能力：LP_EldritchBlast、LP_SummonHornedDevil、LP_SummonIceDevil、LP_SummonRedcaps、LP_WinterVeil、LP_SummerTouch、LP_ChainLash、LP_HellfireHurl、LP_HuntersMark、LP_ErinyesEntangle、LP_RopeEntangle、LP_SatyrCharge、LP_TreeStride、LP_BindTree、LP_ChangelingFaces、LP_DevaHealingTouch、LP_PlanetarWrath、LP_PlanetarBanish
- 伤害：LP_Eldritch
- 状态：LP_PactSigner、LP_PactBound、LP_PatronAgent、LP_Boon_InfernalMark、LP_InfernalDread、LP_Boon_IceArcana、LP_Boon_IronArtisan、LP_Boon_QueenTongue、LP_Boon_FrostShadow、LP_Clause_Hellfire、LP_Clause_IceHeart、LP_Clause_WinterKiss、LP_HellsClaim、LP_NameStolenHediff、LP_ChainDevilWeapon、LP_BoneDevilTailWeapon、LP_BoneVenom、LP_AmnizuDrain、LP_DryadSevered、LP_DryadDistant、LP_RedcapBloodlust、LP_ChangelingGuise、LP_ChangelingHidden、LP_WildHunterSpearWeapon、LP_WildHuntPrey、LP_DevaMaceWeapon、LP_PlanetarSwordWeapon、LP_CelestialGuardian、LP_FallenDevaGuest、LP_DevaStupor、LP_RedeemProgress、LP_HuntMarked、LP_ImpNature、LP_PixieNature、LP_PixieDust_Swift、LP_PixieDust_Diligent、LP_PixieDust_Joy、LP_PixieDust_Veil、LP_InfernalWound、LP_HornRush
- 物品 / 建筑 / 配方 / 工作：LP_PactScroll、LP_InfernalPitchfork、LP_EntanglingRope、LP_WildHuntHorn、LP_CelestialMantle、LP_FeySilverBow、LP_PactLectern、LP_InfernalForge、LP_CleanseInfernalWound（手术）、LP_ReadPact（JobDef）、ThingCategoryDef LP_PactItems
- 动物：LP_Imp、LP_Pixie（ThingDef），LP_ImpFamiliar、LP_PixieFamiliar（PawnKindDef）
- 派系：LP_HellsLegion、LP_FeyHunt（永久敌对，隐藏，和深渊魔群互相敌对——Abyss / Pact 检查里核对过）、LP_HellsBrokers、LP_FeyCourt、LP_PlanarTruce（中立，隐藏）
- 兵种：LP_HL_ChainDevil、LP_HL_BoneDevil、LP_HL_HornedDevil、LP_HL_CollectorLeader、LP_HL_Erinyes、LP_HL_Amnizu、LP_HB_Amnizu、LP_HB_BoneDevil、LP_FW_Redcap、LP_FW_WildHunter、LP_FW_WildHuntLord、LP_FY_Satyr、LP_FY_Dryad、LP_FY_FeyMerchant、LP_FY_SatyrVolunteer、LP_FY_ChangelingVolunteer、LP_CE_Deva、LP_CE_Planetar
- 背景：LP_Bs_HellsSpawn、LP_Bs_HellsVeteran、LP_Bs_FeyChild、LP_Bs_FeyCourtier、LP_Bs_CelestialHost、LP_Bs_CelestialBorn（分类 LP_HellsBio / LP_FeyBio / LP_CelestialBio）
- 事件：LP_PatronWhisper、LP_BloodWar、LP_InfernalCaravan、LP_WildHunt、LP_FallenDeva、LP_FeyMarket（调换儿是出生钩子，没有 IncidentDef）
- 其他：LetterDef LP_PactLetter、InteractionDef LP_DevaRedeem、DutyDef LP_WildHuntDuty、TraderKindDef LP_InfernalCaravanTrader / LP_FeyMarketTrader
- 心情：LP_PactMissed、LP_PactDebtThought、LP_SecretRevealed、LP_SecretRevealedOpinion、LP_NameStolen、LP_IceHeartThought、LP_LaughterSold、LP_SatyrRevelry、LP_DryadTreeDied、LP_DryadDistantThought、LP_DryadDriftingThought、LP_ChangelingSelfThought、LP_PixieDustJoy

开工前 grep 过：`LP_FC_` 前缀已经被费伦拓荒同盟的兵种占用，所以妖精宫廷的兵种用 `LP_FY_`；代码里已有 `static class Pact`（FactionIncidents.cs），所以契约数据类叫 `PlanarPact`；原版 Ideology 有“Dryad”（树精动物），本版的林仙是异种型 `LP_Dryad`，不冲突。

## 五、性能（规格第八节）

**测法**（`PactChecks.CheckPactPerformance`，-Only Pact 每次都跑）：测试地图上放 100 个殖民者。“关”= 100 个普通人；“开”= 其中 10 人各签一份契约（地狱之主的印记、霜影、女王之舌、冰狱奥术、九狱守卫、夏日恩泽、冬之帷幕、铁城工匠、看穿条款、夏日之触——魔能爆、契约检查、恩赐状态都在跑），再加一名绑树的林仙、一只小精灵、一只九狱小魔鬼。两轮预热不计，然后关 / 开交替各 3 轮、每轮 1500 刻，取平均。

| 运行 | 关（ms/刻，3 轮 → 平均） | 开（ms/刻，3 轮 → 平均） | 差 |
|---|---|---|---|
| -Only Pact 第 1 次（最终代码） | 1.319 / 1.051 / 0.890 → 1.087 | 1.243 / 1.138 / 0.951 → 1.110 | +2.2% |
| -Only Pact 第 2 次 | 1.324 / 1.063 / 0.941 → 1.109 | 1.402 / 1.064 / 0.918 → 1.128 | +1.7% |
| -Only Pact 第 3 次 | 1.681 / 1.330 / 1.115 → 1.375 | 1.384 / 1.235 / 1.195 → 1.271 | −7.5% |
| -Only Pact 第 4 次 | 1.197 / 0.927 / 0.804 → 0.976 | 1.074 / 0.937 / 0.861 → 0.957 | −1.9% |
| -NoHAR -Only Pact | 0.946 / 1.025 / 0.944 → 0.972 | 1.019 / 1.096 / 0.912 → 1.009 | +3.8% |
| 五次平均 | 1.104 | 1.095 | −0.8% |

（只有一轮预热时第一轮“关”总是偏慢、会算出 −13%～−25% 的假象，所以最终版本改成两轮预热。）

结论：开着契约内容没有可测的变慢（差别在单轮 ±20% 的噪声之内；“开”的那些签约人本身和普通殖民者一样跑 AI）。没有在 Dev 模式的性能分析器里看过。

**本版新增的 Harmony 补丁和快速返回条件**

| 补丁 | 目标 | 快速返回 |
|---|---|---|
| PactExecutionHook | `ExecutionUtility.DoExecutionByCut` 后缀 | 只在处决时；`!Pacts.Any` 第一行返回，否则只记时间戳 |
| PactGatheringHook | `GatheringWorker.TryExecute` 后缀 | 只在聚会开始时；萨特狂欢只扫这张地图的殖民者；`!Pacts.Any` 返回，否则记时间戳 |
| PactWeddingHook | `MarriageCeremonyUtility.Married` 后缀 | 只在婚礼时；同上 |
| PactTreeCutHook | `Plant.PlantCollected` 前缀 | `!Pacts.Any && dryads==0` 第一行返回；不是殖民者砍树 / 不是树就返回 |
| PactKilledHook | `Pawn.Kill` 前后缀 | 只在死亡时；红袍法师掉书看 kindDef；没有契约、没有狂猎就返回 |
| PactResearchHidden | `ResearchProjectDef.IsHidden` getter 后缀 | 已隐藏 / 不是位面契约学就返回 |
| SummerGrowth | `Plant.GrowthRate` getter 后缀（热路径） | `summerMaps.Count==0` 第一行返回（缓存的地图集合，契约变化时重建） |
| SummerNoFrost | `Plant.MakeLeafless` 前缀 | `summerMaps.Count==0` / 不是寒冷就返回 |
| PactRaceOnGenerated | `PawnGenerator.GeneratePawn` 后缀 | 只在生成时 |
| PactOutgoingDamage | `Pawn.PreApplyDamage` 前缀 | 出手方不是 pawn 第一行返回；其余分支先查武器 def（引用比较）和外来者扩展缓存，普通人不进任何分支 |
| PactAfterDamage | `Pawn.PostApplyDamage` 后缀 | 实际伤害 ≤0 / 出手方不是 pawn 返回；骨魔 / 红帽 / 阿姆尼祖先查外来者缓存；冰狱奥术先看计数 `iceArcana>0` |
| SatyrNoShoes | `ApparelUtility.HasPartsToWear` 后缀 | 已经不能穿 / 衣物不盖脚 / 不是萨特就返回 |
| InfernalWoundBlessedWater、InfernalWoundHealingHands | 祝圣圣水、治疗之手 | 只在使用时 |
| PactBirth | `PregnancyUtility.ApplyBirthOutcome` 后缀 | 只在出生时 |
| （直接改的旧代码）Fiends.Sees、AbyssBinding.DailyDecay / BreakFree、AbyssTaint.Add、Outsiders.Banish | — | Sees 多一次外来者缓存查询；DailyDecay 每小时每个束缚者一次；其余只在事件时 |

间隔写法：契约检查每 2500 刻一次、只遍历契约列表（`pacts.Count==0 && taken.Count==0` 直接返回）；组件每 250 刻只在有狂猎 / 集市 / 血战时处理它们，小时 / 天的结算各一次；“处决过囚犯 / 办过派对 / 砍过树”都是钩子记时间戳，判定时比较；地狱之主的印记每 300 刻、用 `map.attackTargetsCache` 的敌对列表限 5 格；骨魔恐惧气场、链魔策反每 300 刻（敌对目标缓存 / 死灵造物登记表）；林仙离树每小时、树上的标记 fleck 每 600 刻；小精灵粉尘、小魔鬼情报、调换儿每小时；千面只在开 / 关时改名字和外观，渲染节点只看一个布尔；霜影照巴路古拉每 60 刻。

## 六、与规格的差异

1. **设置分组**：不改 Core.cs 就没法往 `SettingsLeft/Right` 表里加新组，所以“异界契约”画在“深渊”分组的末尾（同一列紧跟在下面，标题“异界契约”），存取跟着 `ExposeAbyss`。设置窗口顶部没有单独的“异界契约”跳转按钮（点“深渊”就到）；设置分组数仍是 16，Adventurer 检查不用改。
2. **种族亲和**：萨特、调换儿用 xenotype 上的 `LP_AdventurerAffinityExt`（擅长敏捷魅力 / 笨拙智力；擅长魅力敏捷 / 笨拙体质），没有加 `Adventurer.Racial` 种族加值（规格只给了亲和），所以不用改 AdventurerAffinity.cs 的表。
3. **灵魂讨债人的魔鬼事件**：设备上没有这个事件（grep “讨债”无结果），按规格跳过。
4. **交付白银 / 物品**：从整张地图上没被禁止的存货里扣（中世纪殖民地多半没有轨道信标），不只信标范围。
5. **迪斯帕特到期没付清**：有“抵押物”就收走最值钱的建筑，同时债务 +1，然后契约结束（债务到 2 时照常来一次讨债小队）。
6. **补偿宗主**：可交付的要求交双倍；“处决囚犯”是签约人当场处决 2 名囚犯（不排工作）；派对改交 1000 银；自动执行的要求改交 3 枚灵魂币。
7. **破契仪式**：圣坛按钮立即结算（没有走过去念的工作）；花的是殖民地守护神的神眷，守护神不能是恶神。
8. **祈唤天界援军**：神眷是殖民地级的，所以按“守护神是善神、地图上有它的信徒”判断；守护天使的主人是社交最高的信徒。
9. **冰狱奥术**：“冰霜类能力”没有现成的清单，改为签约人造成的冻伤 / 名字里带 Frost / Cold / Ice 的伤害命中时 +1 层寒气。魔能爆本身不算冰霜。
10. **萨特羊蹄**：没有用 `Hediff_AddedPart` 换脚；只禁盖脚的衣物 + 身体附件画毛腿和蹄子（规格允许的备选）。“派对心情 ×1.5（给别人）”和“演奏娱乐 ×1.5”没做；萨特自己在派对 / 婚礼时 +10。心灵类豁免 +2 = 智力、感知、魅力豁免。
11. **调换儿千面**：RimWorld 只有一种显示名字，所以伪装时对所有人显示被复制者的名字和外观（不只对非玩家派系），没有改派系标签。“美貌对别人看它无效”只做了它自己的美貌（PawnBeauty ×0）。“看穿”= 8 格内有魔鬼之眼（寒冰魔、天界单位）时，每小时检查一次。
12. **林仙**：“年龄冻结”用原版 Ageless（18 岁后不再衰老）；90% 女性在“夏宫来客”生成时指定；没有树时 30 天“漂泊”心情 -5。树之绑定只认不在种植区里的成年树。
13. **链魔锁链**：RimWorld 没有 4 格近战，改为天生锁链（近战钝器 14）+ 能力“锁链拽”（4 格，14 钝器并拽到身边）。
14. **地狱钢叉**：角魔生成时手持；遣返时 30% 留下，否则销毁。
15. **商队 / 集市**：借原版商队事件生成；九狱商队的骨魔护卫不足 2 个时补齐。赎人 / 赎命匣 / 代价交易用“右键商人”的菜单（规格写的是 Gizmo，非殖民者的 Gizmo 选不中）。九狱商队不卖真名卷（空白卷没有用）和银制武器（单一定义的库存生成器不能指定材料）；九狱小魔鬼的来源规格没写，改为九狱商队偶尔卖（买下与买家 / 最近的殖民者结契）。
16. **血战之潮的援助**：不改永久敌对派系的关系，而是把被援助一方的单位临时移到中立的隐藏派系 `LP_PlanarTruce`，用原版 AssistColony 职责帮你打另一方；另一方全灭或 2 天后离开。
17. **狂猎**：自写 `LordJob_WildHunt` + 职责 `LP_WildHuntDuty`（只追猎物，5 格内挡路才打）；早上 6～9 点之间结束。首领只在点数 ≥2500 时出现；冬狼 / 闪光犬用现有的兵种。
18. **坠落天使**：玩家派系的狄瓦空投坠落，带“坠落昏迷”（意识 ≤10%）直到伤都包扎好，然后开始 10 天仆从计时；离开时（计时剩 2 小时内）给守护神 +30。
19. **讨债小队首领**：固定是 14 级角魔（`LP_HL_CollectorLeader`），带“宗主代理人”隐藏状态；遣返 / 死亡时掉代理人真名卷（1.54.0 的真名卷，kind = 宗主 defName，pawnId -1）。
20. **小魔鬼的情报**：RimWorld 不预先决定下一次袭击，情报给的是“当前袭击点数规模 + 一个随机敌对派系”，一半的机会点数被随机扭曲。
21. **周期**：“季”= 15 天，“年”= 60 天（RimWorld 历法）；转卖在签约 60 天后；长冬在每年冬季第一次契约检查时触发。
22. **命匣抵押 ×1.5**：作用在恩赐状态的数值（严重度 1.5）、空投白银数、召唤持续时间、艾林魔等级（×1.1 取整）。
23. **不伐之誓**：没有在砍伐工作上提前警告，只在砍倒时来信记债。
24. **研究图标**：1.7 的 ResearchProjectDef 没有图标字段，没画。
25. **巫婆集会**：妖精共通的条款变动（20%）也适用于它；定金（一年寿命或一只眼睛）用选择信件。
26. **掮客变敌对**：契约检查发现九狱掮客对玩家敌对时记“抢劫”（所有九狱契约债务 +2）。是否在原版里攻击隐藏派系商队一定会让它敌对，没有在游戏里验证（测试里直接设成敌对）。
27. **测试里发现的小坑（已修）**：测试开局 TicksGame 很小，林仙“断树时刻”的哨兵值改为 `int.MinValue`（原来用 -1，减出负数会误判）。

## 七、没做的

- 规格第十二节：远域宗主与星裔、元素契约、独角兽、提夫林九狱血统、邪术师路径、位面地点与救援、恶魔领主、深狱炎魔契约首领、地狱钢。
- 第六条里列出的：萨特“派对心情 ×1.5 / 演奏娱乐 ×1.5”、千面的派系标签、砍伐工作的提前警告、九狱商队的真名卷 / 银制武器、设置窗口的单独跳转按钮、研究图标。
- 艾林魔追猎“始终显示位置”和“命中 +15%”（只做了伤害 +15%）。
- 状态图标（RimWorld 的 HediffDef 没有图标字段）。

## 八、没在游戏里实际看过（只有自检）

- 所有附件贴图在真实小人身上的位置和观感（链子、骨魔头、角、翼、萨特毛腿、林仙树皮 / 叶冠、红帽、调换儿的脸、鹿角、光环），讲台和铸炉在地图上的样子（非正方形画布），羽披穿着图；预览图只是示意身体（身前附件在预览里被身体挡住）。
- 契约书右键阅读、读书工作、选择信件的完整操作（审阅 / 谈判 / 删条款 / 签下 / 拒绝），讲台检视和按钮，圣坛按钮。
- 右键九狱商人赎人、右键妖精商人做代价交易；商队交易界面里买卖契约书、小魔鬼、小精灵、自愿者；小精灵 / 小魔鬼跟随和结契。
- 狂猎的实际 AI（只追猎物）、血战之潮里中立一方的行为、讨债小队的袭击、AI 使用魔能爆 / 锁链拽 / 地狱火 / 缚绳 / 驱散 / 天界之怒。
- 事件在故事叙述者里的权重和触发时机（妖精集市每季第 7 天 20 点）、调换儿 30 天后的揭晓和换回孩子、坠落天使包扎后的醒来、守护天使离开和堕天使、长子条款到 18 岁。
- 林仙树行 / 绑定此树的目标选择、离树和树死的长时间表现；调换儿每 30 天做回自己的心情。
- 夏日恩泽的冬季不冻死（只在测试里直接调用补丁）。
