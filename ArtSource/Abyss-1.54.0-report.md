# 深渊仪式 1.54.0：实现报告

2026-10-27。按 `ArtSource/Abyss-1.54.0-spec.md` 实现；设备上原来是 1.53.0。不需要 HAR / Isekai，装了 HAR 时三阶菜单多一个六臂蛇魔。备份在 `ArtSource/backup-1.53.0/`（保持相对路径）。

## 一、测试结果

| 命令 | 结果 |
|---|---|
| `pwsh .\Source\Build.ps1` | 主程序集、HAR 桥、VPE 桥、神兽血裔桥全部编译通过 |
| `.\Test.ps1 -Only Abyss` | **PASS 57 条**（含存读档），日志无红字 |
| `.\Test.ps1 -NoHAR -Only Abyss` | **PASS 55 条**（少两条 HAR 专属：六臂蛇魔是外来者、六臂蛇人能命令裂口），日志无红字 |
| `.\Test.ps1 -Only Faith` | PASS 17 条 |
| `.\Test.ps1 -Only Events` | 86 条通过后停在 #87「migration group arrives: ember」（已知的旧失败，照协调说明忽略） |
| `.\Test.ps1 -Only Adventurer`（设置分组数 15→16、亲和表新增 7 行） | PASS 123 条 |
| `.\Test.ps1 -Only ResearchTab`（新研究格子 (4, 2.8)） | PASS 6 条 |
| `.\Test.ps1 -Only Races124`（规格 1.2 的验收：寒冰魔 / 炎魔迁移后照过） | PASS 471 条（连续 3 次）。中间有两次停在 #376「smite reaches 10 cells」：泰坦在 `Step(30)` 里因为附近随机出现的敌对单位触发了“逃跑”而走了一格；把代码和定义整体换回 1.53.0 也会随地图随机出现，属于旧的随机失败，与本版无关 |

完整的 Core 套件没有跑（协调说明不要求）。`-VPE` 没有跑：坎比恩的术士血脉（VPE 桥）只编译过、没有自检。

## 二、改动的现有文件（全部已备份到 backup-1.53.0）

- `Source/Fiends.cs`：`Fiends.Is` = 外来者扩展且 `banishOnDown`；`Banish / BanishCorpse` 转给 `Outsiders`；击倒补丁改为 `Outsiders.BanishOnDown`（束缚者、戴锚链的不遣返，身边有带锚链的殖民者时自动锁住），死亡补丁改为 `Outsiders.VanishOnDeath`；`Schedule` 改为 public。
- `Source/PortalEvent.cs`：`PlanarSpawn.minPoints`；`side="abyss"`（深渊魔群 + 集体进攻 Lord）；`special="night"`；`PortalPlaneDef.randomPool / holyClosable / bonusReward / bonusCount`；传送门失控只从 `randomPool` 位面里抽；`SpotOk` 驱魔结界内不开；`Night / HolyNear / CanCommand`（巨灵或六臂蛇人）；`CompPlanarRift.points / forceBig / holyHits`（存档）；带点数和“强制炎魔”的 `Open` 重载（四阶失败第一波就放炎魔）；关闭时额外掉落。
- `Source/ItemsMaterials.cs`：寒铁 / 银对外来者读扩展倍率（原来写死的寒铁 ×1.5 那一行）。
- `Source/ItemsThrowables.cs`：圣水目标与倍率读扩展。
- `Source/Races124.cs`：`FireProof` 改回按 `LP_FiendCore` 基因判断（否则新恶魔会全部免火）。
- `Source/Core.cs`：`ExposeAbyss()`；设置右栏在「五色龙」后加「深渊」分组。
- `Source/AdventurerChecks.cs`：设置分组数 15→16。
- `Source/AdventurerAffinity.cs`：亲和表加坎比恩、魅魔、劣魔、巴路古拉、哈兹魔、纳巴苏、影魔 7 行（冒险者检查要求有种族加值的种族都在表里）。
- `Source/EventChecks.cs`：两处位面检查只看随机池里的 4 个位面（深渊位面另由 -Only Abyss 检查）。
- `Defs/Fiends.xml`：`LP_FiendCore` 加扩展（Hells，寒铁 1.5、银 1.5、圣水 2、真名卷 40%）；`LP_BalorFlame` 加扩展（Abyss，优先级 1，寒铁 1.5、圣水 2、真名卷 40%）。
- `Defs/Research.xml`：`LP_Research_Abyssology` 深渊学（4, 2.8），前置天界与地狱血统，1500，Medieval。格子开工前核对过是空的。
- `Bridges/VPE/Source/Classes2.cs`：`Bloodlines` 加 `LP_Cambion`，`Bloodline()` 另按 `LP_CambionBlood` 基因算（遗传的孩子也算）。BridgeChecks 里没有写死的血脉数量，不用改。
- `About/About.xml`：modVersion 1.54.0，描述末尾加一段。和备份比只有这两行不同。
- `使用说明.txt`：第二部分 1.53.0 详细说明后面插入【1.54.0 深渊仪式】，第三部分 1.53.0 条目前面插入 1.54.0 条目。和备份比：新增 29 行，删除 0 行。

## 三、新文件

- 代码：`Source/Outsiders.cs`（扩展、缓存、遣返、克制、真名卷、外来者登记表、存档状态、设置）、`AbyssTaint.cs`、`AbyssBinding.cs`、`AbyssRaces.cs`、`AbyssRitual.cs`（法阵、仪式与成功率、炼魂、物品、锚链、审讯）、`AbyssThinness.cs`、`AbyssEvents.cs`、`AbyssChecks.cs`（组名 Abyss）。
- 定义：`Defs/Abyss_Races.xml`、`Abyss_Items.xml`、`Abyss_Buildings.xml`、`Abyss_Events.xml`；`Patches/Abyss.xml`（深渊狂乱借原版狂暴思考树、六臂蛇人 `LP_AbyssalHide` 加外来者扩展、两个异域商人 ~10% 卖典籍）。
- 贴图：`ArtSource/RaceArt/draw_abyss_154.py`（复用 draw_race_art / draw_races_124 / draw_minotaur_gith 的形状函数）生成 98 个文件：`Textures/Abyss/{Horns,Eyes,Heads,Taint,Wings,Tails,Quasit,Xenotypes,Genes,Abilities}`、`Abyss/Faction`、`Textures/Items/Abyss/*`、`Textures/LP/Abyss/*`；预览 `ArtSource/Preview/Abyss-1.54.0.png`。新定义引用的 65 个贴图路径都核对过存在。

## 四、新 defName

- 研究：LP_Research_Abyssology
- 异种型：LP_Dretch、LP_Barlgura、LP_Hezrou、LP_Succubus、LP_Nabassu、LP_ShadowDemon、LP_Cambion
- 基因：LP_AbyssCore、LP_AbyssUndying、LP_DretchStature、LP_DretchStench、LP_DretchToil、LP_SkinDretch、LP_BarlguraStature、LP_BarlguraMight、LP_BarlguraLeap、LP_BarlguraStill、LP_BarlguraBrute、LP_SkinBarlgura、LP_HezrouStature、LP_HezrouHide、LP_HezrouStench、LP_SkinHezrou、LP_SuccubusNature、LP_SuccubusHorns、LP_SuccubusWings、LP_SuccubusTail、LP_SuccubusGuise、LP_SuccubusKiss、LP_SuccubusDream、LP_SkinSuccubus、LP_CambionBlood、LP_CambionHorns、LP_CambionWings、LP_CambionFire、LP_CambionPresence、LP_NabassuStature、LP_NabassuDevour、LP_NabassuGaze、LP_SkinNabassu、LP_ShadowDemonForm、LP_SkinShadowDemon
- 能力：LP_BarlguraLeap、LP_SuccubusKiss、LP_NabassuGaze、LP_CambionGlide
- 动物与兵种：LP_Quasit（ThingDef）、LP_QuasitFamiliar、LP_AH_Dretch、LP_AH_Barlgura、LP_AH_Hezrou、LP_AH_Succubus、LP_AH_Nabassu、LP_AH_ShadowDemon
- 派系 / 背景：LP_AbyssHorde、LP_Bs_DemonSpawn、LP_Bs_DemonVeteran（背景分类 LP_AbyssBio）
- 状态：LP_AbyssalTaint、LP_AbyssFavored、LP_TaintedPhylactery、LP_SoulMortgaged、LP_PlanarBinding、LP_AbyssThrall、LP_PlanarShackled、LP_RitualStupor、LP_SuccubusDisguise、LP_SuccubusAppetite、LP_Nauseated、LP_NabassuPetrify、LP_BarlguraHidden、LP_SoulDevoured、LP_SoulGorged、LP_QuasitNature、LP_QuasitVeil、LP_QuasitBond、LP_DemonBloodRush、LP_DemonBloodDrawn、LP_FangWearer、LP_FangGorged
- 物品 / 建筑 / 配方：LP_SoulCoin、LP_DemonBlood、LP_Demonomicon、LP_TrueNameScroll、LP_DimensionalShackles、LP_GorgedFang、LP_AbyssCircle、LP_BindingCircle、LP_WardingGlyph、LP_MakeDimensionalShackles、LP_ExtractDemonBlood、ThingCategoryDef LP_AbyssItems、RulePackDef LP_Namer_Demonomicon / LP_Description_Demonomicon
- 事件 / 位面 / 信件：LP_AbyssWhisper、LP_SuccubusVisit、LP_AbyssIncursion、LP_NabassuHunt、LP_Plane_Abyss、LP_AbyssWhisperLetter、LP_SuccubusRevealLetter
- 其他：MentalStateDef LP_AbyssFrenzy、InteractionDef LP_ReassertName、LP_TaintFactorDef LP_TaintFactors、PrisonerInteractionModeDef LP_InterrogateTrueName、WorkGiverDef LP_WardenInterrogateTrueName、JobDef LP_AbyssRitual / LP_AbyssWard / LP_AbyssRefine / LP_UseTrueName / LP_ApplyShackles / LP_InterrogateTrueName / LP_RedeemSoul
- 心情：LP_AbyssWhisperThought、LP_AasimarSeesFavored、LP_DretchStenchThought、LP_SuccubusHolySymbolThought、LP_SoulCoinScream、LP_SuccubusKissed、LP_SuccubusSated、LP_SuccubusStarved、LP_HauntedDreams、LP_RejectedAbyss、LP_AbandonedBySuccubus、LP_BarlguraBloodthirst

开工前 grep 过现有名字：`LP_Petrifying`、`LP_Shackled`（风暴巨人）、`LP_Bs_AbyssSpawn`（六臂蛇人背景）已被占用，所以本版用 `LP_NabassuPetrify`、`LP_PlanarShackled`、`LP_Bs_DemonSpawn`。

## 五、性能（规格第九节）

**测法**（`AbyssChecks.CheckAbyssPerformance`，-Only Abyss 每次都跑）：测试地图上放 100 个殖民者。“关”= 100 个普通人；“开”= 其中 10 个换成深渊仆从（劣魔 ×4、哈兹魔 ×2、巴路古拉、魅魔、纳巴苏、影魔：恶臭、静立隐形、梦境吸取、饱魂都在跑），另外 90 人都带 15% 腐化，再加一个驱魔结界。先预热一轮不计，然后关 / 开交替各 3 轮、每轮 1500 刻，取平均。

| 运行 | 关（ms/刻） | 开（ms/刻） | 差 |
|---|---|---|---|
| -Only Abyss（最终） | 2.404 / 2.169 / 1.876 → 2.150 | 2.215 / 1.963 / 1.947 → 2.042 | −5.0% |
| -NoHAR -Only Abyss（最终） | 2.072 / 1.494 / 1.245 → 1.603 | 1.620 / 1.365 / 1.271 → 1.419 | −11.5% |
| -Only Abyss（之前一次） | 2.314 | 2.170 | −6.2% |

结论：开着深渊内容没有可测的变慢（在 < 2% 的要求之内）。要注意：单轮之间的波动就有 ±20%（同一配置 2.07 / 1.25），而且“开”的那 10 个恶魔不吃不睡，本身比普通殖民者便宜，所以负数不代表变快，只能说补丁的开销小于测量噪声。没有在 Dev 模式的性能分析器里看过。

**本版新增的 Harmony 补丁和快速返回条件**

| 补丁 | 目标 | 快速返回 |
|---|---|---|
| OutsiderGenesChanged | `Pawn_GeneTracker.Notify_GenesChanged` 后缀 | 只在基因变化时运行；清缓存 + 更新登记表 |
| OutsiderRegisterSpawn / Despawn | `Pawn.SpawnSetup` 后缀 / `Pawn.DeSpawn` 前缀 | 不是外来者（缓存查询）也不是伪装魅魔就返回 |
| OutsiderDamage | `Pawn.PreApplyDamage` 前缀 | `Outsiders.Of(victim)==null`（按 pawn 缓存的并发字典）第一行返回 |
| OutsiderOnGenerated、AbyssRaceOnGenerated | `PawnGenerator.GeneratePawn` 后缀 | 不是外来者 / 没有基因就返回 |
| OutsiderKilled | `Pawn.Kill` 前后缀 | 只在死亡时；`Outsiders.Is` 缓存查询 |
| TaintPrayerCleanse / TaintPrayerScope / TaintFavorFactor | `Faith.PrayerDone` / `PrayerReward` / `Gain` | 只在祈祷时；`Gain` 先看线程静态 `praying==null` |
| TaintBlessedWater、TaintHealingHands | 祝圣圣水饮用、治疗之手 | 只在使用时 |
| TaintRevelationBlocked | `Ability.GizmoDisabled` 后缀 | 已禁用 / 不是阿斯莫就返回（只在画按钮时调用） |
| TaintedPhylacteryDelay | `Phylactery.ScheduleRevival` 后缀 | 灵魂没有“命匣受污”就返回 |
| ThrallNoGrief | `PawnDiedOrDownedThoughtsUtility.TryGiveThoughts` 前缀 | `AbyssBinding.thralls==0` 就返回 |
| ThrallNoThreat | `Map.PlayerPawnsForStoryteller` getter 后缀 | `thralls==0` 就不包一层过滤 |
| SuccubusCambionChild | `PregnancyUtility.ApplyBirthOutcome` 后缀 | 只在出生时 |
| SuccubusAltarForbid | `ForbidUtility.IsForbidden(Thing,Pawn)` 后缀（热路径） | 已禁止 / 地图上从没出现过魅魔（`succubi==0`）/ `Outsiders.Of(pawn)==null` 依次返回 |
| SoulGoneResurrect、SoulGoneOffering | `ResurrectionUtility.TryResurrect` 前缀、`Souls.Unfit` 后缀 | 只在复活 / 献祭时 |
| InterrogateRowFilter | `ITab_Pawn_Visitor.DrawExclusiveInteractionRow` 前缀 | 不是“审讯真名”这一行就返回 |
| ShackledNoBreak | `PrisonBreakUtility.CanParticipateInPrisonBreak` 后缀 | 本来就不能越狱 / 没戴锚链就返回 |
| AbyssResearchHidden | `ResearchProjectDef.IsHidden` getter 后缀 | 已隐藏 / 不是深渊学就返回 |
| HolyWaterRift | `GenExplosion.DoExplosion` 前缀 | 不是圣水 / 祝圣圣水爆炸就返回 |
| AltarRedeemSoul | `Building_FaithAltar.GetFloatMenuOptions` 后缀 | 小人没有“灵魂抵押”就不加选项 |
| 改过的旧补丁 FiendBanishOnDowned / FiendBanishOnDeath | 击倒 / 死亡 | 先看 `Fiends.Is`（现在是缓存的扩展查询） |

其他按规格的间隔写法：恶臭（哈兹魔）每 300 刻、按格子扫 3 格；劣魔恶臭心情只遍历本地图外来者登记表；腐化、服从度、梦境吸取、魔宠、饱魂之牙、结界睡眠都是 hediff / gene 的每小时（2500 刻）结算；薄弱度每天一次（地图没激活时 `MapComponentTick` 第一行返回）；登记表的魅魔识破检查每 250 刻、表为空直接返回；移速 StatPart（影魔怕光、结界减速）非外来者第一行返回、结果按 pawn 缓存 250 刻。没有加任何对所有 pawn 的每刻补丁（束缚者年龄冻结原本想补 `AgeTickInterval`，改成束缚状态每小时把生物年龄拨回去）。

## 六、与规格的差异

1. **“不吃不睡”拆出来**：`LP_AbyssCore` 只管判定和扩展；不吃不睡是另一条基因 `LP_AbyssUndying`，魅魔不带（要同床才能梦境吸取，规格又给了它食量 ×0.2）。魅魔的食量用连带状态 `LP_SuccubusAppetite`（饥饿 ×0.2），因为 1.7 没有 HungerRateMultiplier 属性。
2. **各族扩展挂在体型基因上**（优先级 1）：等级范围和真名卷掉率按族不同。真名卷掉率规格只给了魔鬼 40%，其他由我定：劣魔 5%，巴路古拉 / 哈兹魔 / 影魔 20%，魅魔 / 纳巴苏 30%。扩展多了一个字段 `lightBane`（影魔怕光）。
3. **遣返规则保留老行为**：玩家派系、没束缚的寒冰魔 / 炎魔照旧击倒即遣返（Races124 的老检查依赖这一点）。夸塞魔的扩展 `banishOnDown=false`：魔宠倒地不遣返、死了留尸体（规格没写，留尸体才有干尸贴图的用处）。
4. **仆从不掉恶魔战利品**（只掉身上的东西），免得召劣魔再杀掉刷灵魂币。
5. **位面锚链自动上锁**：敌对外来者倒地下一刻就遣返，手动给“倒地的外来者”戴锚链实际来不及；所以殖民者背包里带着锚链、站在 5 格内时自动锁住（用掉一条）。手动右键选项也保留（对不会立刻遣返的外来者有用）。
6. **祭品搬运**：只扛尸体和囚犯到法阵中心；灵魂币、恶魔血、神迹残片直接从地图存货里扣。没倒地的囚犯先被主祭定住（`LP_RitualStupor`，意识上限 10%，6 小时）再扛走。
7. **失败的裂口开在法阵旁边**（10 格内第一块空的 2×2），不在法阵中心：裂口是不可通行的建筑，压在贴地法阵上会把法阵挤掉。法阵照样锁住直到裂口关上。
8. **赎回灵魂 30 神眷**（规格 300）：设备上神眷上限是 100，300 永远付不起。
9. **束缚法阵的床**：床在法阵上或紧挨着法阵（外扩 1 格）都算，以防非建筑物（isEdifice=false）上不让放床。
10. **AH 魅魔兵种固定女性**（规格是生成时 70% 女）；仪式里自己选性别，魅魔来访的人 70% 女。
11. **“耳边的低语”-3**在 10% 以上所有阶段都有（规格只写在低语阶段那一行）。
12. **深渊狂乱**：新 MentalStateDef 继承原版 Berserk，思考树用补丁插一个同样的分支。每季 10% 换算成每小时的概率。
13. **劣魔工作**：保留搬运、采矿、清洁、粗活（ManualDumb）、暴力、灭火；屠宰属于烹饪、切石属于制作，原版工作类型拆不开，所以这两样不能做。
14. **巴路古拉嗜杀**：地图上有处决时巴路古拉得到 +5 心情（不判断“看见”）。
15. **驱魔结界材料**：银 10 + 炼金圣水 `LP_HolyWater` 1（costList 不能写“二选一”）。
16. **审讯真名**：狱卒用单独的工作（每个囚犯 8 小时一次），不挂在原版谈话排程上；ITab 里只对戴锚链的外来者囚犯显示这一行。
17. **束缚者**：年龄冻结 = 每小时把生物年龄拨回束缚时的值；不育 = 没有 Sterile 基因的（如六臂蛇人）加原版“已绝育”，魅魔除外。六臂蛇人束缚者死后照旧留尸体（六臂蛇人规则不变）。
18. **仆从心情**：用状态阶段的 `disablesNeeds Mood`，不是 Harmony。
19. **灵魂币给巫妖**：固定 87.5 经验 = 一具普通尸体（50 + 技能合计 60 × 5）的 25%。
20. **典籍进度按书算**（这本书累计被读满 4 小时），不是按读者。深渊低语事件另加了 earliestDay 10、minRefireDays 10。
21. **地图上显示薄弱度**：开了设置时画在屏幕上方中间；法阵检视面板里一直显示。
22. **魅魔不能在圣坛 5 格内工作**：做成“圣坛 5 格内的东西对没伪装的魅魔算禁止”。“看到圣徽”= 12 格内有殖民者戴着圣徽。识破距离：魔鬼之眼 8 格、阿斯莫 5 格。
23. **四阶失败的裂口**：`forceBig` 让炎魔跳过点数门槛、而且第一波就出（加了检查）。
24. **测试里改了两个旧检查**（EventChecks 位面数量、AdventurerChecks 设置分组数）和亲和表，见第二节。

## 七、没做的

- 规格第十三节：九狱 / 妖精 / 天界 / 远域 / 元素契约与契约讲台（1.55.0）；弗洛魔、钳魔、嗡魔、纳法斯尼、星裔；位面地点、恶魔领主、深渊钢 / 深渊之刃；夸塞魔借眼和变形；影魔穿墙；恶魔血炼金配方；邪契者特性；邪术师路径。
- 纳巴苏猎魂“偏向 8 格内没有其他殖民者的人”：用的是原版集体进攻 AI。纳巴苏“每层饱魂 +20 袭击点数”：没做。
- 坎比恩“威吓 / 心理战社交成功率 +20%”：没做（只有社交天赋 +2）。
- 魅魔伪装时“看到异族的心情不触发、交易按普通人”：只做了外观和识破。
- 影魔半透明 shader / 黑烟 fleck：用的是近黑肤色 + 发光的眼睛。
- 状态（腐化四阶、束缚、仆从、抵押、饱魂、恶心、石化、深渊宠儿、命匣受污）的图标：RimWorld 的 HediffDef 没有图标字段，没画；研究也没有图标。基因、异种型、能力、派系图标都画了。

## 八、没在游戏里实际看过（只有自检）

- 所有附件贴图在真实小人身上的位置（角、猿脸、蟾蜍头、獠牙、眼睛、翼、尾按 draw_races_124 的画框画的，预览图是示意身体）；夸塞魔、法阵、物品贴图的实际观感。
- 仪式的完整工作流程：菜单、主祭去扛囚犯 / 尸体、护阵者站四角、念咒进度条和被打断；炼魂工作；镶银边。
- 真名卷右键使用、位面锚链右键、审讯真名的狱卒工作和 ITab 那一行的显示、抽恶魔血手术、赎回灵魂（圣坛右键）。
- 恶魔学典籍被当成阅读娱乐读、交易商卖典籍。
- 夸塞魔跟随主人、不战斗时隐身；魅魔变形伪装开关、梦境吸取（同床判定）、圣坛禁区；重申真名社交互动的出现频率。
- 深渊狂乱的行为、地图上的薄弱度显示、事件的故事叙述者权重。
- VPE 联动（坎比恩术士血脉）没有跑 -VPE。
