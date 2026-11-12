# 生产专长 1.141.0 实现要求（第一批 8 个）

Peter 2026-11-09 18:30 「按照你的完成加入」，同意第一批。设计文档 https://claude.ai/artifact/UhFdKmn7rsPA3mGoaTxDnU 。
基线：设备 1.140.0（GitHub bb1f2a1）。版本 1.141.0。备份 ArtSource/backup-1.140.0（只备份要改的旧文件）。报告 ArtSource/ProductionFeats-1.141.0-report.md。

## 规则
- 新文件：Defs/Adventurer_Feats5.xml、Source/AdventurerFeats5.cs、Source/ProductionFeatsChecks.cs（测试组名 ProductionFeats）。Adventurer_Feats4.xml 已被 1.140.0 战斗专长占用，不要碰。
- defName 不能以数字结尾（1.7 会把尾数字当 thingID）。全部 LP_Feat_Xxx 形式。
- 每个专长都是 LichPhylactery.LP_FeatDef，有 bonusAbilities、iconPath LP/Feats/Xxx、order 接在现有最大值之后、description 写清楚数值。玩家可见文字不出现 d20/检定/掷骰，概率写「X% 几率」。
- 不写 enemyTags（敌人不选）。
- 能用 statFactors/statOffsets 的就用（Hediff_Adventurer.BuildStage 已经会合并）；其余用 Harmony，热路径先用 Feats.Has 快速返回。
- 新增概率统一走 Feats2.TestRoll ?? Rand.Value，方便测试。
- 图标：8 个新图标，沿用 ArtSource/RaceArt/draw_feats*.py 的画法生成，放 Textures/LP/Feats/。
- 只跑 Test.ps1 -Only ProductionFeats（再加 -Only Feats2,FeatsBuff 确认老专长没坏）。不要跑完整测试。游戏要关着才能编译。
- About.xml 改 1.141.0；使用说明.txt 加【1.141.0 生产专长】一节 + 更新日志一行。

## 8 个专长

| defName | 名称 | 前置 | 效果 | 附带 |
|---|---|---|---|---|
| LP_Feat_PracticedHand | 学以致用 | 起源专长（origin true），无要求 | 日常工作换算的冒险者经验 ×1.5；每天全额部分 80 → 120（之后仍 ×0.5） | 感知或智力 |
| LP_Feat_MasterArtisan | 名匠 | 8 级，手工 12，敏捷或智力 13（ability 只能填一个：写 FeatWorker 判断 Dex 或 Int ≥13，ExtraPrereq 显示「敏捷或智力 13」） | 所有制作品质 30% 几率提升一档（最高传说）；做出大师级以上作品时心情 +4「得意之作」1 天（新 ThoughtDef LP_FeatMasterworkPride） | 敏捷或智力 |
| LP_Feat_RuneSmith | 符文锻师 | 4 级，手工 10，力量或智力 13（同上） | 在 LP_RuneAnvil、LP_GiantAnvil、LP_FireGiantForge 工作速度 ×1.4；在这些台上做的东西 25% 几率品质提升一档；完成时 20% 几率返还四分之一的金属材料（stuff 是 Metallic 的那部分，向下取整，至少 1 才生成） | 力量或智力 |
| LP_Feat_MasterAlchemist | 炼金大师 | 起源专长，手工或医疗 6（任一满足：FeatWorker 判断） | 在 LP_AlchemyTable、LP_Alembic（若是设施则只算炼金桌）工作速度 ×1.35；在炼金桌做出的产品 30% 几率多出一份（只限 products 里第一个产品数量 ×1，照抄 Feats2ChefSnack 的 MakeRecipeProducts 后缀） | 智力或体质 |
| LP_Feat_MasterBuilder | 筑城大师 | 4 级，建造 8 | statFactors ConstructionSpeed ×1.35；statOffsets ConstructSuccessChance +0.15；修理速度 ×1.5（原版没有单独属性的话用 JobDriver_Repair 的工作量后缀，做不到就在报告写偏差，改成 ConstructionSpeed 已经包含修理） | 力量或体质 |
| LP_Feat_GreenThumb | 丰收之手 | 起源专长，种植 6 | PlantWorkSpeed ×1.3；PlantHarvestYield +0.15（offset）；能力「催生」LP_FeatGrowth 每天 1 次（usesPerDay 1，午夜恢复，照鼓舞领袖的给能力方式）：自身半径 6 格内所有作物（Plant，非树木也行，Blighted 不管）生长进度 +0.2，上限 1（成熟），有绿色光效 | 感知或体质 |
| LP_Feat_Surgeon | 外科圣手 | 8 级，医疗 12，敏捷 13 | MedicalSurgerySuccessChance ×1.25（和治疗者叠乘，最终仍受原版上限）；手术速度 ×1.5（MedicalOperationSpeed）；手术失败不会是致命失败（Recipe_Surgery.CheckSurgeryFail 里把 catastrophic/致死分支降成普通失败）；安装 DND 植入物和义体（本 mod 与铬骨霓虹的植入手术 recipe）不消耗药品（找不到稳妥做法就在报告写偏差，只做前三条） | 敏捷或感知 |
| LP_Feat_Fabricate | 造物之手 | 12 级，手工 15，智力 15，已有名匠（ExtraPrereq「已学名匠」） | 能力「造物术」LP_FeatFabricate 每天 1 次：施法者正在做账单（当前 job 是 DoBill 且有 UnfinishedThing 或 bill 工作进度）时立刻完成剩余工作量的 50%（最多 60000 tick 等价工作）；这件作品品质至少「优秀」（施法后给施法者 1 天 hediff LP_FeatFabricated，下一次 GenerateQualityCreatedByPawn 结果 < Excellent 时提到 Excellent，然后移除）。没在做账单时能力不可用并提示「需要正在工作台前制作」 | 智力 |

### 品质提档统一做法
一个 Harmony 后缀（QualityUtility.GenerateQualityCreatedByPawn，Pawn+SkillDef 重载，参照 ArtificerCraftQuality 的 TargetMethod），优先级 Priority.Low：
- 名匠 30%、符文锻师 25%（只在当前工作台属于那三张台时）：取最高几率，只判一次，只提一档。
- 造物术 hediff 的下限最后应用。
- 和器械师、熔岩之握、火巨人等现有加成照常叠加，不改它们。
- 「当前工作台」：p.CurJob?.GetTarget(TargetIndex.A).Thing 是 Building_WorkTable 时取 def.defName。

### 学以致用
改 AdventurerWork：Learned 里倍率乘 1.5（有专长时），Diminish 的 DailyFull 换成 Feats.Has(p, PracticedHand) ? 120 : 80。这是唯一要动的老文件之一，先备份。

### 工作台速度
StatWorker 后缀或 StatPart 都行：在 WorkSpeedGlobal 的 GetValueUnfinalized 后缀里判断，只在 pawn 当前 job 的工作台属于列表时乘系数（热路径：Feats.Has 先短路）。如果已有类似 Fire giant 熔炉加速的写法，照抄。

## 测试（ProductionFeats，约 10 条，不要多）
1. 8 个 def 都加载，图标存在，defName 都不以数字结尾。
2. 学以致用：同样技能经验下冒险者经验 ×1.5，每日全额到 120。
3. 名匠：TestRoll 0 时品质 +1 档；TestRoll 0.99 时不变；传说不再提。
4. 符文锻师：在符文铁砧 job 下品质提档，在普通台不提；与名匠同时只提一档。
5. 筑城大师 / 丰收之手 / 外科圣手：对应属性数值正确（statFactors/offsets）。
6. 丰收之手「催生」：半径内作物 Growth +0.2，上限 1，每天一次。
7. 外科圣手：致命失败分支不出现（能测就测，测不了写报告）。
8. 造物之手：前置要名匠；hediff 让下一次品质下限为优秀，然后 hediff 消失。
9. 炼金大师：TestRoll 0 时炼金桌产品多一份，普通台不多。
10. 名匠/造物之手 的 ExtraPrereq 文本存在。

## 完成后
报告写：改了哪些文件、偏差、未在游戏里测的东西。告诉协调线程设备空闲。git 只在 Peter 要求时推送。
