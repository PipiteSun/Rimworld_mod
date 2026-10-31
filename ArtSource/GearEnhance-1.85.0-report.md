# 1.85.0 附魔强化（2026-10-31）

取代 Isekai 的装备强化（Peter 不再用 Isekai）。

## 文件
- `Defs/Research.xml`：LP_Research_GearEnhancing「装备强化」(4,0) 1500，前置附魔学；LP_Research_GearEnhancingHigh「高阶强化」(5,0) 3000，前置装备强化。
- `Defs/GearEnhance.xml`：LP_EnhanceGearLow（+1～+5：4 秘银 + 25 白银，3000 工作量，智识 6）、LP_EnhanceGearHigh（+6～+10：4 精金 + 10 黄金，6000，智识 10），都只在附魔台，workerClass `RecipeWorker_GearEnhance`，ingredientValueGetter `RitualCount`（白银、黄金按件数算）。
- `Source/GearEnhance.cs`：GearEnhance 静态类（常数、成功率、Apply、Transform）、CompGearEnhance（level/target，标签前缀 +N、检视文字、信息卡、选目标按钮）、StatPart_GearEnhance、RecipeWorker_GearEnhance（不消耗装备）、Harmony：MakeRecipeProducts 前缀（掷成功率、无产物）、Bill.IsFixedOrAllowedIngredient 后缀（只接选了目标、等级在本档的装备）。启动时给所有武器（Primary）和衣物 ThingDef 加组件。
- `Source/GearEnhanceChecks.cs`：`Test.ps1 -Only GearEnhance`（12 条 + 读档 1 条）。

## 数值
- 成功率 L→L+1：100/95/90/80/70/60/50/40/30/20%；+ (智识与手工平均 − 8) × 1.5%（−10%～+18%）；奥术聚能石 +5%；最低 5%。
- 失败：+5 以下只丢材料；+6 起掉一级（不低于 +5）。成功修满耐久，到达目标清除标记。
- 每级：MeleeWeapon/RangedWeapon_DamageMultiplier ×(1+6%)；Accuracy* +1.5%（≤99%）；衣物 ArmorRating_Sharp/Blunt/Heat ×(1+5%)+1%；MarketValue ×(1+15%)。

## 测试
`pwsh Test.ps1 -Only 'GearEnhance,ResearchTab'` → PASS 19 checks（含原生工作单在附魔台真实跑一次、存读档）。

## 注意
- 装备要在地上或储存区，身上的不会被拿去强化。
- 备份：ArtSource/backup-1.84.0-gearenhance（About.xml、使用说明.txt、Research.xml 改动前版本）。
