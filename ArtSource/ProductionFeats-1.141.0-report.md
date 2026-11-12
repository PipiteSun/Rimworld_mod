# 生产专长 1.141.0 实现报告

2026-11-09。规格：ArtSource/ProductionFeats-1.141.0-spec.md。基线 1.140.0 → 版本 **1.141.0**（About.xml 改版本前复查过，1.141.0 没被别的线程占用）。备份 ArtSource/backup-1.140.0（只有改动的旧文件 + 旧 dll）。

## 新文件
- `Defs/Adventurer_Feats5.xml`：8 个 `LichPhylactery.LP_FeatDef`（LP_Feat_PracticedHand 学以致用、LP_Feat_MasterArtisan 名匠、LP_Feat_RuneSmith 符文锻师、LP_Feat_MasterAlchemist 炼金大师、LP_Feat_MasterBuilder 筑城大师、LP_Feat_GreenThumb 丰收之手、LP_Feat_Surgeon 外科圣手、LP_Feat_Fabricate 造物之手；order 49～56，都有 bonusAbilities、iconPath LP/Feats/Xxx，没有 enemyTags），思绪 LP_FeatMasterworkPride（+4，1 天），状态 LP_FeatFabricated（1 天），能力 LP_FeatGrowth「催生」、LP_FeatFabricate「造物术」。
- `Source/AdventurerFeats5.cs`：`Feats5`（常量、WorkMult / DailyFull、BoostChance / Boost、Growth、FabricateReason / Fabricate、ExtraProducts）、FeatWorker_MasterArtisan / RuneSmith / MasterAlchemist / Fabricate（二选一前置 + ExtraPrereq 文本）、CompProperties_Feat5 / CompAbilityEffect_Feat5、Ability_Fabricate、StatPart_Feats5Bench（WorkSpeedGlobal）、StatPart_Feats5Repair（ConstructionSpeed）、Feats5Startup（挂属性部件）；Harmony：Feats5Quality（QualityUtility.GenerateQualityCreatedByPawn 的 Pawn+SkillDef 重载后缀，Priority.Low，照 ArtificerCraftQuality 的 TargetMethod）、Feats5Products（GenRecipe.MakeRecipeProducts 后缀，照 Feats2ChefSnack）、Feats5NoFatalSurgery（SurgeryOutcome_Death.Apply 前缀）、Feats5NoCatastrophicSurgery（SurgeryOutcome_Failure.Apply 前缀）。
- `Source/ProductionFeatsChecks.cs`：检查组 `ProductionFeats`（`Test.ps1 -Only ProductionFeats`），10 条 + 读档 1 条。
- `ArtSource/RaceArt/draw_feats5_1141.py` → `Textures/LP/Feats/{PracticedHand,MasterArtisan,RuneSmith,MasterAlchemist,MasterBuilder,GreenThumb,Surgeon,Fabricate}.png`（只写这 8 个）；预览 `ArtSource/Preview/ProductionFeats-icons.png`。

## 改动的旧文件（原件在 backup-1.140.0）
- `Source/AdventurerWork.cs`：Learned 里倍率 `m*=Feats5.WorkMult(p)`（有学以致用 ×1.5）；Diminish 的 DailyFull 换成 `Feats5.DailyFull(h.pawn)`（120 / 80）。
- `Source/AdventurerUI.cs`：冒险者分页「日常工作」一行的全额上限改为显示本人的值（`Feats5.DailyFull(h.pawn)`）。
- `Source/Feats2Checks.cs`：第 1 条的专长计数 41 → 49（新专长 order ≥ 13，被 Feats2.Is 算进去）。
- `About/About.xml`：modVersion 1.141.0，描述末尾加「1.141.0 生产专长 8 个：…」。
- `使用说明.txt`：第 90 行加「生产专长（1.141.0）」玩家说明；「三、更新日志」头下加 1.141.0 开发日志一行。
- `1.7/Assemblies/LichPhylactery.dll`（及三个联动 dll）重新编译。

## 各专长的做法
| 专长 | 做法 |
|---|---|
| 学以致用 | AdventurerWork：倍率 ×1.5，每日全额 120（之后仍 ×0.5）。 |
| 名匠 | Feats5Quality 后缀：30% 提一档（最高传说）；最终品质 ≥ 大师级时给「得意之作」。 |
| 符文锻师 | StatPart_Feats5Bench：当前 job 目标 A 是 LP_RuneAnvil / LP_GiantAnvil / LP_FireGiantForge 时 WorkSpeedGlobal ×1.4；Feats5Quality 在这三张台 25%（与名匠取较高几率、只判一次、只提一档）；Feats5Products 20% 把 Metallic stuff 材料的 1/4（向下取整，≥1）作为额外产品放出。 |
| 炼金大师 | StatPart_Feats5Bench：LP_AlchemyTable ×1.35（LP_Alembic 是设施，不算）；Feats5Products 在炼金桌 30% 多一份 `products[0]`（数量 = 配方数量，材料 / 品质照第一件）。 |
| 筑城大师 | statFactors ConstructionSpeed 1.35、statOffsets ConstructSuccessChance 0.15；StatPart_Feats5Repair：curDriver 是 JobDriver_Repair 时 ConstructionSpeed 再 ×1.5（原版修理量按 ConstructionSpeed 算）。 |
| 丰收之手 | statFactors PlantWorkSpeed 1.3、statOffsets PlantHarvestYield 0.15；grantAbility LP_FeatGrowth（usesPerDay 1，照鼓舞领袖：GizmoDisabled 看 UsesLeft，Feats.Use 扣次数）：半径 6 格所有 Plant 生长 +0.2、上限 1，绿色尘雾特效。 |
| 外科圣手 | statFactors MedicalSurgerySuccessChance 1.25、MedicalOperationSpeed 1.5；1.7 的 Recipe_Surgery.CheckSurgeryFail 只是转调 SurgeryOutcomeEffectDef.GetOutcome，分支都在 outcome 列表里，所以前缀 SurgeryOutcome_Death.Apply（直接返回 false）和 SurgeryOutcome_Failure.Apply（totalDamage > 20 或不限于手术部位的，返回 false），剩下原版「轻微失败」（20 点、限手术部位）照常。 |
| 造物之手 | FeatWorker_Fabricate（ExtraPrereq「已学名匠」）；grantAbility LP_FeatFabricate，abilityClass Ability_Fabricate 覆写 QueueCastingJob：不开施法 job（会打断账单），按下直接 Feats5.Fabricate：curDriver 是 JobDriver_DoBill 且 workLeft > 0（或目标 B 的 UnfinishedThing 已初始化且 workLeft > 0）时减掉剩余的 50%（最多 60000），同步 uft.workLeft，给 LP_FeatFabricated 1 天；Feats5Quality 最后看这个状态：结果 < 优秀提到优秀，然后移除。没在做账单时按钮灰掉并提示「需要正在工作台前制作」。 |

## 测试
- `Test.ps1 -Only ProductionFeats`：**11/11 通过**（10 条 + 读档 1 条）。第一次跑时日志里有一条世界生成的「Could not generate a pawn (Tribal_ChiefMelee)」红字（WorldGenStep，与本改动无关，重跑干净）。
- `Test.ps1 -Only Feats2,FeatsBuff`：**31/31 通过**（Feats2 第 1 条计数已改为 49）。
- 没跑完整测试。

## 偏差
1. **外科圣手「安装 DND 植入物和义体不消耗药品」没做**：药品是账单流程里当作原料消耗的（Bill_Medical → Toils_Recipe），没有不动原版流程又稳妥的截取点；只做了前三条（成功率、速度、不致命 / 不灾难）。
2. order 用 49～56 而不是紧接 39：Adventurer_Feats2.xml 的种族专长已经占了 40～48，「接在现有最大值之后」按全部专长的最大值算。
3. 造物术走 Ability 子类覆写 QueueCastingJob 而不是 CompAbilityEffect.Apply：按原版施法会先开一个施法 job 把正在做的账单打断（没有 UnfinishedThing 的配方进度会丢），所以按钮按下直接生效、不开 job。Comp 里仍保留 Apply（万一被别的途径施放）。
4. 灾难性失败的判定按「outcome 的 totalDamage > 20 或 applyEffectsToPart = false」：原版基础 outcome 列表里致命、灾难（65）、荒唐（65、全身）三种都被跳过，Biotech 的绝育失败（10 点）和轻微失败（20 点）照常；如果别的模组的 outcome 列表里全是重失败，会落到「没有失败 = 成功」。
5. 修理 ×1.5 用 ConstructionSpeed 的 StatPart（只在 JobDriver_Repair 时生效），和 statFactors 的 ×1.35 相乘，修理总共约 ×2。
6. 催生对半径内所有植物（含野草、树）生效，规格写「非树木也行」，按最宽的取。

## 没在游戏里验证的
- 造物术在真实账单上的表现（workLeft 减半、UnfinishedThing 同步、按钮在未征召工作中显示）——检查只验证了前置、hediff 保底与消失、没做账单时的提示。
- 工作台速度在真实账单 tick 里的生效（检查用 Wait job 指向工作台量 WorkSpeedGlobal 比值）。
- 外科圣手走 Bill_Medical 的完整手术流程（检查直接调用 outcome.Apply）。
- 符文锻师返还金属、炼金大师多一份在工作台旁的落地摆放；得意之作、催生 / 造物术的特效和文字在界面上的样子；8 个图标在专长分页里的观感。

设备空闲（测试进程已退出，没有 _LichTest_* 残留）。没有动 git。
