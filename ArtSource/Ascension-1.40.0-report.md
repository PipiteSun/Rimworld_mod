# DND边缘大陆 1.40.0 报告：成神之路

需求：ArtSource/Ascension-1.40.0-spec.md。备份：ArtSource/backup-1.39.0（改动前的 About/About.xml、使用说明.txt、Source/Core.cs、Twilight.cs、TwilightDeities.cs、Faith.cs、NecromancyGenes.cs、NetherilAscension.cs、Defs/Twilight.xml、Defs/Research.xml，以及 1.7/Assemblies 主 dll）。实际上 Twilight.cs、TwilightDeities.cs、Faith.cs、NecromancyGenes.cs、NetherilAscension.cs、Twilight.xml 一个字都没改（全部用 Harmony 挂在已有方法上），备份只是保险。贴图预览：ArtSource/Ascension-1.40.0-preview.png（脚本拼的，不是游戏截图）。

## 测试结果

| 命令 | 结果 |
|---|---|
| `Source\Build.ps1` | PASS（主程序集 + HAR / VPE / 神兽血裔桥接；桥接源码没动） |
| `Source\Test.ps1 -Only Ascension` | PASS：10 条（9 条检查 + 存读档 1 条），日志无红字。前后 4 个版本各跑一次都过，最后一次是交付的这版 |
| `Source\Test.ps1 -Only ResearchTab` | PASS：6 条 |
| `Source\Test.ps1 -Only Faith`（额外，回归） | PASS：17 条（成神挂了信仰的几个方法：Faith.Gain、PrayerDone、MiracleCost、ThoughtWorker_Dark 等） |
| `Source\Test.ps1 -Only Twilight`（额外，回归） | PASS：114 条（挂了 Twilight.Reward 和 TwilightSkills.Tick）。第一次跑日志里有一条原版红字“Accessing TicksAbs but gameStartAbsTick is not set yet”（生成开局小人时原版记录离婚故事，栈里没有本模组代码），重跑一次干净通过 |
| 临时冒烟组（跑完已删） | PASS：22 个神职能力的效果各施放一次；异端勇者、火巨人和霜巨人的巨人之战、末日卫士三波、吉斯洋基、众神的勇士、夺火者三波、天穹之门三波位面生物各生成一次，日志无红字。冒烟时发现并修掉一个问题：试炼袭击没先确定入场点，小人被放到地图外（-1000,-1000） |
| 完整套 / -Only Events / -Only Factions | 没跑（按要求；后两个是已知的无关旧失败） |

-Only Ascension 的 10 条（需求第六节）：
1. 研究“神格学”前没有“成神之路”按钮；研究后神选者（神眷 70）有，普通殖民者和神眷 50 的神选者没有；研究 3000 点、中世纪、前置信仰与神术、在 DND边缘大陆 页签。
7. 已有 1 名半神时，第二名圣徒的升神仪式被名额挡住（说明里写着“名额”），名额改 2 后放行。
8. 仪式完成：LP_Demigod 1 级、神职 = 洛山达的“黎明”、两个能力（破晓之光、不灭晨曦）都在、火花被消耗、进度关闭。
9. 半神推进 1 年生理年龄不变（对照的普通殖民者变了）。
12. 命匣 19 级不能开半神巫妖路，20 级可以。
15. 神龛：死后复生计时 1 天；神力 60 时死去立即复生，神力剩 10；命匣生命上限 ×2。
16. 泰坦觉醒：异种型换成 LP_AwakenedTitan，原有基因全保留（另加了一个内生基因“暗视”也还在），多了 LP_DivineBlood；`PregnancyUtility.GetInheritedGenes` 给孩子的基因里没有 LP_DivineBlood。
19. 篡火者成功率 = 50% + (心灵敏感度-1)×50% + 8%/残片（最多 5）+ 弑神者 10%，95% 封顶；roll 0.99 失败：主祭死亡、尸体还在、火花消失、地图上神迹残片 +5、进度清空。
21. 神灵议会：倒地过 → 神力 0、等级仍 2、进入冷却；没倒地 → 等级 3（神力上限 200、强度 ×1.5）。
24. 存读档：半神巫妖路的进度（第 0 步、7 个灵魂、命匣引用）、半神等级 3 / 神力 77 / 火焰神职和能力、神龛标记、黎明的神域赐福都保留。

## 做了什么

- **框架**（Source/Ascension.cs）：设置“成神”分组（左栏信仰之下：开关、名额 1～3、难度 0.5～2.0）；`LP_DivinePortfolioDef` 神职（12 个，Defs/Ascension.xml）和 `LP_PortfolioExtension`（Patches/Ascension.xml 挂到 11 位神上，LP_DeityDef 的 C# 类没动）；世界组件 `LP_AscensionTracker`（每人一条 `AscensionProgress`、半神名单、已升神名单、神域赐福、神龛、被篡的神、灵魂安息、反魔场）；Pawn.GetGizmos 后缀加“成神之路”按钮和小窗口（路线、当前试炼、条件、进度条、下一步、按钮、放弃）；神性火花（`CompDivineSpark` 记神职、来源路线、主人）；升神仪式（JobDriver，12 小时，可打断，期间“神性震荡”）；圣坛按钮登记在 `Building_FaithAltar.ExtraGizmos`；通用的试炼袭击生成（原版 RaidEnemy 参数 + `PawnGroupMakerUtility`，点数 × 难度）。
- **半神**（Source/Demigod.cs）：`Hediff_Demigod` + `HediffComp_Demigod { level, portfolio, divinePower, lastTrialTick }`，阶段名、属性按需求；金色神力条 gizmo；每天神力结算；僭越 -0.1；神灵议会（按钮 → 1 天后众神的勇士，领队用 `Twilight.MakeVessel` 附体 → 倒没倒地结算）；升入神域（光柱、世界小人、赐福、心情）；不老（同奥术半神的写法）；`MiracleCost` 后缀 ×0.5；12 个被动与 12 个神域赐福。
- **神职能力**（Source/DemigodAbilities.cs）：22 个原版 AbilityDef（`Ability_Divine` 扣神力），效果按需求第三节。
- **四条路线**（Source/AscensionPaths.cs）：圣徒、半神巫妖、泰坦、篡火者的每一步和计数钩子；奥术半神订阅 `NetherilAscension.ArcaneDemigodGranted`，读档时用 `IsArcaneDemigod` 补。
- **Defs**：Defs/Ascension.xml（12 神职、22 能力、2 种火花、圣徒圣印、永夜冠冕、天穹之矛、诸神之祸、圣徒/弑神者、半神与 9 个能力状态、凯兰沃的末日卫士派系与 2 个兵种、觉醒泰坦与觉醒神血、6 个心情、2 个天象、2 个工作）；Defs/Research.xml 加神格学（X2 / Y2.8，信仰行最右）。
- **美术**：ArtSource/RaceArt/draw_ascension.py（`python -I ArtSource/RaceArt/draw_ascension.py <模组根目录> [预览图]`）→ Textures/Ascension/，38 张：22 个能力图标、4 件物品、2 把武器（图标即手持贴图）、4 个界面图标（成神之路、半神、众神的试炼、升入神域）、觉醒泰坦异种型与觉醒神血基因图标、神辉（南/北/东）、命匣神龛（原命匣图加金光和神纹）。末日卫士穿原版长袍染灰。

## 和需求不一样的地方

1. **Isekai 伤害系数**：需求说用“1.30.1 法师/牧师能力已经在用的同一帮助函数”。grep 下来设备上没有这个函数：1.30.1 只让战士走原版近战结算（所以 Isekai 的近战加成自然生效），法师/牧师（包括 1.39.0 的神术）都没乘 Isekai。神职能力照此不乘 Isekai，强度 = 基础 × 等级强度 × 心灵敏感度（下限 0.2），伤害再 × 永夜冠冕 1.15。天穹之矛走原版近战，品质和 Isekai 的近战加成照常生效。
2. **父神**：“坠落的神裔”剧本没有存父神，所以一律在开路时从 7 位里随机。
3. **虔信的计时**：一次圣坛祈祷（1 小时）记 1 小时；圣坛 10 格内冥想按每小时抽查一次记。心情每小时检查，掉到 30% 以下累计清零。
4. **神性火花坠落**用原版空投舱，没有改色。
5. **命匣生命 ×2**：物品的生命上限是缓存属性，StatPart 不生效，改成 Harmony 后缀 `Thing.MaxHitPoints`；变成神龛那一刻当前生命也 ×2。
6. **“吉斯洋基好感永久 -50”**：设备上没有吉斯洋基派系（他们是塞伊的雇佣兵和殖民者种族）。做法：吉斯洋基对不死神职半神的社交好感 -50（社交心情“僭越的巫妖”），“巫妖女王的信徒”（`Githyanki.SeesLich`）从此对所有人失效。
7. **反魔场**只拦原版能力（RimWorld.Ability）和神明附体的神技；VPE/VEF 的灵能不经过原版 Ability，拦不到。
8. **不灭晨曦**：致命伤改成不致命并给伤口止血，不强制倒地（同 1.39.0 凯兰沃赐福的写法）。
9. **熔炉祝福**只加在近战命中上，远程武器没有附加伤害。
10. **灵魂安息**：用独立的到期表（不是凯兰沃的永久“安息”），挡原版复生（`ResurrectionUtility.TryResurrect`）和夺心魔吞脑（`MindFlayer.CannotDevour`）。敌人的尸体在 8 格内也一起标上。
11. **野性被动**：挂在原版狂暴动物的找目标（`JobGiver_Manhunter.FindPawnTarget`）上；正常狩猎时被他打的动物反击不算。
12. **死亡被动**：“殖民者死亡全员心情惩罚 ×0.5”作用在 KnowColonistDied、ColonistLost、KnowColonistExecuted、KnowGuestExecuted、KnowPrisonerDiedInnocent 这几条上。
13. **精神崩溃阈值 -10%** 做成属性 MentalBreakThreshold 加 -0.10（原版铁石心肠是 -0.18 的写法）。
14. **神灵议会**按每小时检查一次“领主结束”；半神不在殖民地地图时袭击推迟 6 小时；倒地按每 60 刻检查一次（一瞬间倒地又被救起来也算）。
15. **巨人之战**：“巨人王”是这次挑战里第一个生成的巨人（带标记状态“巨人王”，名字中间插“巨人王”），30% 按巨人王的致死伤害阈值算。父神苏尔特：塞伊的霜巨人掠夺者；否则余烬氏族的火巨人领队，余烬氏族不敌对时改由塞伊远征军派火巨人来（不会把友好的余烬氏族变成敌人）。巨人王跑掉或别人打死而泰坦伤害不够，可以重新挑战。
16. **天穹之门**：3 波在第 0、2、4 小时；用随机一个位面的“野生/敌对野兽”出生表（不含塞伊那一方的生物），数量 × 难度倍率，从地图边缘冲进来并狂暴。
17. **夺火者三波**在火花坠落后 2 小时、18 小时、34 小时，第一波塞伊、第二波吉斯洋基雇佣兵（塞伊派系的 LP_FH_GithMercenary）、第三波对立神派系，都直冲火花。
18. **抗拒审判**三波在第 2 小时、第 1 天、第 2 天到；审判者的“冻结复生”对半径 15 格内所有巫妖命匣和巨魔尸体生效（包括你自己的）。第三波打完而命匣完好时，火花出现在命匣旁（审判者是死是被俘都一样）。末日卫士的武器用 MedievalMeleeDecent 标签（长剑等），没有法杖；近战伤害换成 LP_Radiant，打亡灵/死灵造物 ×1.5。
19. **篡火者的开启和计数**：记“最后一个打中附体首领的殖民者”，首领离体（倒地）或死亡时算给他；没有殖民者打中过的（比如被炮台打倒）不算。开启不要求研究（按钮仍在研究后出现、仪式要研究）。
20. **弑神者的 ×0.7**：作用在附体首领不带武器造成的伤害（神技都是空手的 DamageInfo）；不改其他神技效果（狂乱、恐惧等）。
21. **“X 派系袭击频率 ×1.5”**：挂在 `FactionDef.RaidCommonalityFromPoints` 上，固定信仰（FaithExtension）里有被篡之神的派系 ×1.5（和 1.37.0 卷轴加权同一个地方）。
22. **圣徒的“装 VPE 时神恩强度 +0.15”**：神恩强度属性 LP_DivinePower 存在时（装 VPE）挂一个 StatPart。没装 VPE 时神术用“0.5 + 社交/20”，圣徒的社交 +2 已经算进去。
23. **奥术半神**：接入时不检查名额（耐瑟瑞尔的仪式本来就成了），但之后占名额。
24. **LP_Demigod 的图标**：HediffDef 本身不显示图标，这张图用在金色神力条上和奥术神职的图标上。
25. **名字**：暴政能力“支配”的状态 defName 是 LP_DivineDominated（LP_Dominated 已被夺心魔用掉）。其余 defName 照需求。
26. 新增“放弃这条路”按钮（需求没写；代价不返还）。

## 新 defName

| 类别 | defName |
|---|---|
| ResearchProjectDef | LP_Research_Theogony 神格学 |
| LP_DivinePortfolioDef | LP_Portfolio_Dawn / Justice / Tyranny / Storm / Fire / Wild / Mind / Death / Magic / Dark / Undeath / Arcane |
| AbilityDef（22） | LP_DA_DawnLight、LP_DA_UnfadingDawn、LP_DA_JudgmentHammer、LP_DA_JusticeShield、LP_DA_Dominate、LP_DA_CrownOfDread、LP_DA_ThunderWrath、LP_DA_CallWind、LP_DA_Meteor、LP_DA_ForgeBlessing、LP_DA_PackCall、LP_DA_PredatorForm、LP_DA_PsionicBlast、LP_DA_WillCrush、LP_DA_SoulRest、LP_DA_JudgmentGate、LP_DA_WeaveMissiles、LP_DA_Antimagic、LP_DA_OblivionFog、LP_DA_ShadowStep、LP_DA_SoulHarvest、LP_DA_DeadArmy |
| HediffDef | LP_Demigod 半神、LP_UnfadingDawn、LP_JusticeShield、LP_DivineDominated、LP_CallWindHaste、LP_ForgeBlessing、LP_PredatorForm、LP_OblivionFog、LP_SummonedTemp、LP_FreezeRevivalAura、LP_GiantKing |
| ThingDef | LP_DivineSpark 神性火花、LP_UsurpedSpark 篡夺的神性火花、LP_SaintSeal 圣徒圣印、LP_EternalNightCrown 永夜冠冕、LP_SkySpear 天穹之矛、LP_Godsbane 诸神之祸 |
| TraitDef | LP_Saint 圣徒、LP_Godslayer 弑神者 |
| FactionDef / PawnKindDef | LP_KelemvorChurch 凯兰沃的末日卫士（隐藏、永久敌对）；LP_Doomguide 末日卫士、LP_DoomJudge 凯兰沃的审判者 |
| XenotypeDef / GeneDef | LP_AwakenedTitan 觉醒泰坦；LP_DivineBlood 觉醒神血 |
| ThoughtDef | LP_DivineRadiance 神性的光辉（+4）、LP_NewGodRises 新神崛起（+8/10 天）、LP_GodWatches 我们的神在看着我们（+15/30 天）、LP_GodWatchesForever 神的故乡（+3，永久）、LP_DawnPresence 晨光普照（+4）、LP_GithBetrayal 僭越的巫妖（好感 -50） |
| GameConditionDef | LP_DivineTremor 神性震荡、LP_WrathStorm 神怒风暴 |
| JobDef | LP_AscensionRite、LP_SkyGate |
| 设置键 | lpAscension、lpDemigodCap、lpAscensionDifficulty |
| 存档组件 | LP_AscensionTracker（世界组件） |

## 改动的文件

新增：Source/Ascension.cs、AscensionPaths.cs、Demigod.cs、DemigodAbilities.cs、AscensionChecks.cs；Defs/Ascension.xml；Patches/Ascension.xml；ArtSource/RaceArt/draw_ascension.py；Textures/Ascension/（38 张）；ArtSource/Ascension-1.40.0-spec.md、Ascension-1.40.0-preview.png、本报告。

共享文件（原件在 backup-1.39.0）：
- Source/Core.cs：设置 ExposeData 加 `ExposeAscension()`，设置页左栏信仰之后加 `AscensionSettings.Draw(l)`。
- Defs/Research.xml：Y 2.8 加神格学。
- About/About.xml（1.40.0 + 描述一句）、使用说明.txt（标题、研究一行、内容一览一行、第二部分【1.40.0 成神之路】、更新日志）。
- 重新编译的 1.7/Assemblies/LichPhylactery.dll 和三个桥接 dll（桥接源码没动）。

Harmony 补丁（都先判断对应的人/物/状态，平时几乎不做事）：Pawn.GetGizmos、Pawn.PreApplyDamage（两处）、Pawn_HealthTracker.PostApplyDamage、Pawn.Kill、Pawn_GuestTracker.CapturedBy、TendUtility.DoTend、Pawn_RecordsTracker.Increment、RimWorld.Ability.Activate / get_CanCast / StartCooldown、Bill_Production.Notify_IterationCompleted、Pawn_AgeTracker.get_BiologicalTicksPerTick、DamageWorker_AddInjury.FinalizeAndAddInjury、Verb_MeleeAttackDamage.ApplyMeleeDamageToTarget、ResurrectionUtility.TryResurrect、MemoryThoughtHandler.TryGainMemory、InvisibilityUtility.IsPsychologicallyInvisible、JobGiver_Manhunter.FindPawnTarget、WeatherEvent_LightningStrike.FireEvent、PrisonBreakUtility.InitiatePrisonBreakMtbDays、ThoughtWorker_Dark.CurrentStateInternal、FactionDef.RaidCommonalityFromPoints、PregnancyUtility.GetInheritedGenes、Thing.get_MaxHitPoints；本模组的 FaithUtility.MiracleCost、Faith.Gain、Faith.PrayerDone、NetherilAscension.CooldownFactor、Twilight.Reward、TwilightSkills.Tick、Phylactery.ScheduleRevival / get_Capacity、Hediff_Lich.get_Power、Hediff_Construct.get_Upkeep、Githyanki.SeesLich、MindFlayer.CannotDevour。另外订阅了 `NetherilAscension.ArcaneDemigodGranted`；`Phylactery` 加了一个 partial 覆盖 `Graphic`（神龛贴图）。

## Peter 试玩时请注意

- **要完全重启游戏**才会加载新 dll 和贴图。
- 入口：研究“神格学”（信仰行最右）→ 选中符合条件的殖民者，底部会多一个“成神之路”按钮。圣徒要先是神选者且神眷≥60；巫妖要命匣 20 级；泰坦要成年；篡火者要等黄昏之年（开发者模式“立即开始黄昏之年”可以试）。
- 名额默认 1：想同时养两位半神，到设置“成神”里调到 2～3。
- 难度：所有试炼袭击的点数 = max(当前袭击点数, 下限) × 难度倍率，下限是圣徒证道 2500、巨人之战 3000、神灵议会 2000+1000×等级、夺火者 1000、末日卫士 600。数值都是估的，觉得太难/太简单先调“试炼难度倍率”，再告诉我改基数。
- 半神巫妖路一开就扣凯兰沃 50 神眷，成神后凯兰沃固定 -100；篡火者成神后被篡的神也固定 -100。如果守护神正好是这位神，殖民地的神术和赐福会受影响。
- 篡火仪式失败会死人（95% 封顶）；开仪式前的窗口里能看到成功率，献神迹残片能加。
- 神灵议会要神力满：1 级每天大约 +2（加上信徒和好友），神力上限 100，所以第一次试炼要等一段时间。
- 没自动测、请在游戏里看：“成神之路”窗口的排版、各按钮的说明文字、神力条、能力的瞄准和特效、神龛贴图、觉醒泰坦头顶的神辉位置（贴图比泰坦原光环大一圈，层在它上面）、天穹之矛/诸神之祸手持的角度、奉献弹窗、升入神域的光柱、末日卫士的灰袍。
