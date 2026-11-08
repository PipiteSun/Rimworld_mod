# 法师法术 4–9 环 1.129.0 实现报告

依据：ArtSource/WizardSpells-1.129.0-spec.md（Peter：完成到九环）。版本 1.129.0（开工时 About.xml 是 1.128.0，没有被占用）。在 1.128.0 法术书之上做。

## 结果
- 编译：`Source\Build.ps1` 通过（主程序集 + HAR / VPE / 神兽血裔桥）。
- 测试（只跑这两组）：
  - `Test.ps1 -Only Spellbook`：**28 条全过**（1.128.0 的 14 条 + 新增 13 条 + 存读档 1 条，存读档那条加了每天次数和应急术），最终版本连续 3 次全过。开发中修掉：急制术取材料用错了过滤（改用原版 `Bill.IsFixedOrAllowedIngredient`）；祈愿术检查里测试小人穿着护甲导致 8 点割伤为 0（测试脱掉护甲）。
  - `Test.ps1 -Only Classes`：**76 条全过**。
- 没跑完整测试，没有 git 提交 / 推送。
- 备份：ArtSource/backup-1.128.0-wizard（改动前的 WizardSpellbook.cs / WizardSpells.cs / SpellbookChecks.cs / WizardSpellbook.xml / About.xml / 使用说明.txt 等）。

## 新文件 / 改动
- 新：`Defs/WizardSpells2.xml`（51 个法术、约 20 个状态、思想 LP_WS_MansionThought、建筑 LP_WS_ForceWall / LP_WS_TeleportCircle）、`Source/WizardSpells2.cs`（效果、放逐、商队按钮、反制 / 禁施法 / 心灵屏障 / 宅邸 / 克隆体的 Harmony）、`Source/SpellbookHighChecks.cs`（13 条检查）、`ArtSource/RaceArt/draw_wizard_spells.py`、`Textures/LP/Classes/WizardSpells/*.png`（89 个）、`ArtSource/WizardSpells-1.129.0-icons.png`。
- 改：`ClassSubclass3.cs`（EvoEffects 加 6 个新塑能法术）、`WizardSpellbook.cs`（环位 → 等级 / 法力表、九环上限、每天次数、蓄力型升环、专注可以挂在区域 / 放逐上、时间停止不进冷却、以太化施法回来、仪式也算次数和长冷却、GameComponent 存放逐和克隆体）、`WizardSpells.cs`（Hediff_WS 加施法者 / 强度 / 原阵营 / 收起的武器、反制登记；法术区域加火墙 / 黑触手 / 厌恶之地 / 延时火球；伤害前缀加以太化、心灵屏障、预警术、石肤术；控制判定被心灵屏障挡下）、`WizardSpellbook.xml`（38 个图标路径改到新图标）、`SpellbookChecks.cs`（定义计数改为 89 个）、About.xml、使用说明.txt。

## 规则
- 环位 → 法师等级 / 法力：四环 7/2，五环 9/3，六环 11/4，七环 13/5，八环 15/6，九环 17/9（`Spellbook.LevelFor / ManaFor / MaxCircle`）。
- 每天次数：八环法术合计每天 2 次、九环合计每天 1 次（`Spellbook.DailyCap`，按游戏天数计，仪式施法也算）；次元门每天 2 次（`perDay`）。急制术每天一次、探知术每 3 天、通晓传奇每 15 天、祈愿术每 7 天：用能力冷却实现，仪式施法完成时也会开始冷却。
- 控制类成功率沿用 1.128.0：目标每比施法者高 1 级 −5%（最低 10%），传奇生物 / 剧情首领减半；心智类另外可被心智抗性抵抗，目标有心灵屏障时一定失败。
- 塑能学派覆盖：火墙术、锥形寒冰、连锁闪电、阳炎射线、延时爆裂火球、阳炎爆（这 6 个的 effect 加进了 ClassSubclass3.cs 的 EvoEffects，法术塑形、强效塑能、超限导能都对它们生效；H1 检查）。

## 新 defName、复用与新写
| 环 | defName | 法术 | 实现 |
|---|---|---|---|
| 3 | LP_WS_Counterspell | 反制法术 | 复用 SelfBuff（状态 LP_WS_Counterspell）+ 新 Harmony：Ability.Activate 前缀 |
| 4 | LP_WS_Polymorph | 变形术 | 新写；友军复用荒野变形 LP_WildShape |
| 4 | LP_WS_Stoneskin | 石肤术 | 新写（状态 + 专注，伤害前缀 ×0.6） |
| 4 | LP_WS_WallOfFire | 火墙术 | 新写，复用 1.128.0 法术区域（直线格） |
| 4 | LP_WS_BlackTentacles | 黑触手 | 新写，复用法术区域 |
| 4 | LP_WS_Banishment | 放逐术 | 新写（Banishment：世界小人 + 到时回原地） |
| 4 | LP_WS_Confusion | 困惑术 | 新写（原版 Berserk） |
| 4 | LP_WS_PhantasmalKiller | 魅影杀手 | 新写（Hediff_WS 每 2 秒） |
| 4 | LP_WS_ArcaneEye | 秘法之眼 | 新写（揭开迷雾） |
| 4 | LP_WS_Fabricate | 急制术 | 新写（原版 GenRecipe 产物，品质按法师技能） |
| 4 | LP_WS_FaithfulHound | 忠犬守卫 | 新写（原版哈士奇 + 隐形状态） |
| 4 | LP_WS_Blight | 枯萎术 | 新写，复用 1.128.0 死灵伤害 |
| 5 | LP_WS_ConeOfCold | 锥形寒冰 | 新写，复用 ClassLegendary.Cone 扇形 |
| 5 | LP_WS_DominatePerson | 支配人类 | 新写（换阵营 + 状态 + 专注） |
| 5 | LP_WS_HoldMonster | 定身怪物 | 新写（同定身类人，可升环） |
| 5 | LP_WS_Telekinesis | 心灵遥控 | 新写，物品复用法师之手 |
| 5 | LP_WS_ConjureElemental | 召唤元素 | 新写（元素裔异种型） |
| 5 | LP_WS_TeleportationCircle | 传送法阵 | 新写（建筑 LP_WS_TeleportCircle，贴图复用束缚法阵着蓝色） |
| 5 | LP_WS_ModifyMemory | 修改记忆 | 新写 |
| 5 | LP_WS_Scrying | 探知术 | 新写 |
| 5 | LP_WS_LegendLore | 通晓传奇 | 新写（原版 OpportunitySite_ItemStash 任务） |
| 6 | LP_WS_ChainLightning | 连锁闪电 | 新写，复用 LP_Lightning / EMP |
| 6 | LP_WS_WallOfForce | 力场墙 | 新写（建筑 LP_WS_ForceWall，原版墙贴图着蓝色） |
| 6 | LP_WS_TrueSeeing | 真知术 | 新写 + **复用识破卷轴** ItemScrolls.TrueSeeing |
| 6 | LP_WS_CircleOfDeath | 死亡法阵 | 新写 |
| 6 | LP_WS_FleshToStone | 石化术 | 新写，第 3 道 **复用石化卷轴** ItemScrolls.StartPetrify |
| 6 | LP_WS_MoveEarth | 移山术 | 新写 |
| 6 | LP_WS_Contingency | 应急术 | 新写（存法术，受伤后缀里触发） |
| 6 | LP_WS_Sunbeam | 阳炎射线 | 新写（光耀 = 圣武士的 LP_Radiant） |
| 6 | LP_WS_CreateUndead | 创造不死生物 | **复用巫妖骷髅仆役** Constructs.Create |
| 7 | LP_WS_DelayedBlastFireball | 延时爆裂火球 | 新写，复用法术区域 + 原版爆炸 |
| 7 | LP_WS_DimensionDoor | 次元门 | 新写，复用 Races124.Blink |
| 7 | LP_WS_FingerOfDeath | 指死术 | 新写 + 骷髅仆役 |
| 7 | LP_WS_ReverseGravity | 反转重力 | 新写 |
| 7 | LP_WS_Etherealness | 以太化 | 复用 SelfBuff + 新状态 |
| 7 | LP_WS_PlaneShift | 异界传送 | 新写 + 商队按钮 |
| 7 | LP_WS_MagnificentMansion | 魔法宅邸 | 新写 + 商队按钮 |
| 8 | LP_WS_ForceCage | 力场牢笼 | 新写，复用力场墙 |
| 8 | LP_WS_Maze | 迷宫放逐 | 新写，复用放逐 |
| 8 | LP_WS_DominateMonster | 支配怪物 | 同支配人类的效果 |
| 8 | LP_WS_Feeblemind | 弱智术 | 新写 |
| 8 | LP_WS_MindBlank | 心灵屏障 | 复用 TargetBuff + 新钩子 |
| 8 | LP_WS_Antipathy | 厌恶之地 | 新写，复用法术区域 |
| 8 | LP_WS_Sunburst | 阳炎爆 | 新写 |
| 8 | LP_WS_Clone | 克隆体 | 新写 |
| 9 | LP_WS_TimeStop | 时间停止 | 新写 |
| 9 | LP_WS_Wish | 祈愿术 | 新写 |
| 9 | LP_WS_Gate | 异界之门 | 新写（深渊 / 九狱 PawnKind LP_AH_Hezrou → LP_HL_ChainDevil → LP_AH_Dretch） |
| 9 | LP_WS_PowerWordKill | 强力真言·杀 | 新写 |
| 9 | LP_WS_Foresight | 预警术 | 复用 TargetBuff + 伤害前缀 |
| 9 | LP_WS_MassPolymorph | 群体变形 | 复用变形术 |

仪式（新 10）：急制术、忠犬守卫、传送法阵、修改记忆、探知术、通晓传奇、移山术、魔法宅邸、厌恶之地、克隆体。专注（新 10）：石肤术、火墙术、黑触手、放逐术、魅影杀手、支配人类、召唤元素、阳炎射线、反转重力、支配怪物。可升环：定身怪物（+1 目标）；延时爆裂火球是“蓄力”型（右键或法术书里选 0～5 秒，不加法力）。

## 简化（全部写进了法术说明）
- 反制法术：拦的是 `Ability.Activate`（职业能力、原版 / 皇权心灵能力、其他 Ability），不拦武器射击和非 Ability 的技能。对方等级 = 我们的环位，或心灵能力的 level，都没有时按 3。
- 变形术：不换贴图；武器收进背包、结束时拿回；Ability.CanCast 和职业能力都被挡。
- 火墙术只烧站在墙格上的敌人，不点燃地面；黑触手的钝器伤害只打被缠住（眩晕中）的敌人。
- 放逐术：“外来者”= 有外来者扩展的种族（恶魔、魔鬼、妖精、天界、元素、远域）或魔法阵判定的恶魔魔鬼 / 元素；暂时放逐的人存成世界小人，到时回原格（被占就最近的空格），敌对人形回来时重新加一个进攻集群。
- 秘法之眼：揭开的迷雾不会再盖回去（原版迷雾不会恢复），不是 1 小时。
- 急制术：选“第一个没暂停、该做、有产物、工作量够低”的账单；材料从地图上最近的取。
- 忠犬守卫：原版哈士奇 + 隐形 + 近战伤害 ×1.5（不是精确的 15 × 强度一口）。
- 枯萎术：“植物系”= 虫族或血肉类型名里带 Plant / Tree / Insect。
- 心灵遥控：不能自选落点——物品拿到身边、友军拉到身边、敌人往远离施法者方向抛 6 格。
- 召唤元素：模组里没有元素生物，用对应的元素裔（火 / 水 / 土 / 风异种型）殖民者战士代替，近战至少 10 级、没有武器。
- 传送法阵：目的地自动选（优先主基地的另一座法阵），不能手选。
- 探知术：不能真的提前警报——列出 3 天内**已经排定**的威胁事件（多半来自任务），并按原版威胁点数估计一次袭击的规模。
- 通晓传奇：原版物资藏匿点任务，魔法物品塞进藏匿点的物品里（优秀品质）；生成任务失败时物品直接给法师。
- 移山术：只做“挖掉”，没做“整平”。
- 应急术：可存护盾术、虚假生命、闪现术、石肤术、法师护甲、镜影术（需要目标的迷踪步、传送术不在列表）。
- 阳炎射线 / 阳炎爆的光耀用圣武士的 LP_Radiant 伤害，防护能量的“火焰”不减它。
- 创造不死生物 / 指死术：食尸鬼、僵尸都是巫妖的骷髅仆役（改名“食尸鬼仆役 / 僵尸仆役”），永久。
- 延时爆裂火球：蓄力做成“落点火珠过 N 秒才炸”，不是加施法前摇。
- 以太化：不被当目标 = 隐形组件，不受伤 = 伤害前缀全挡；不能穿墙；攻击或放伤害法术就结束。
- 异界传送 / 魔法宅邸的商队版：商队按钮（Caravan.GetGizmos），扣这位法师 5 点法力；宅邸挡的是以商队为目标的大小威胁事件，“舒适卧室”做成休息 ×1.2 + 心情 +10 的思想。
- 力场牢笼：用力场墙围一圈（半径约 2.5）。迷宫放逐不吃等级差。
- 心灵屏障：挡掉一切精神状态（MentalStateHandler.TryStartMentalState 前缀）。
- 厌恶之地：做成“每 2 秒成功率 50% 推出区域并停 1 秒”，不改寻路代价。
- 克隆体：需要生物科技 + 地图上有生长缸；“灵魂移入克隆体”= 原小人死后下一刻在主基地复活（技能、等级、职业、特性都在），身上装备和衣物掉在死去的地方。
- 时间停止：友军也会被定住（只有施法者能动）。
- 祈愿术：免费施放列表 = 1～8 环全部法师法术（含没学会的）；痊愈 = 每个殖民者修复最多 40 个健康问题；传说物品 = 随机一件 DND 魔法 / 奇物 / 圣物，传说品质。
- 异界之门：商队 = 原版商队到来事件，生成点设在法师身边（原版可能仍从地图边走过来）。
- 强力真言·杀：“伤势过半”= 原版健康总览 ≤ 50%；还没过半的普通敌人受 30% 伤害。
- 预警术：没做“不会被偷袭”。

## 注意
- 图标：89 个全部重画（脚本 draw_wizard_spells.py，复用 draw_class_art 的形状库，粗描边 + 平涂 + 右下暗部）。预览 ArtSource/WizardSpells-1.129.0-icons.png。

## 测试覆盖（新增 13 条，在 -Only Spellbook 里）
H1 定义与环位表；H2 每天次数；H3 反制法术；H4 变形术（敌 / 友）；H5 放逐 / 异界传送 / 放逐专注；H6 支配人类；H7 火墙直线 / 延时火球蓄力 / 厌恶之地分类；H8 连锁闪电 / 死亡法阵 / 阳炎爆；H9 石化术；H10 应急术；H11 力场墙 / 力场牢笼 / 时间停止；H12 祈愿术；H13 急制术。另外存读档那条加了每天次数和应急术。
没有自动测试：各个选择窗口、商队按钮、克隆体复活、通晓传奇的任务生成、传送法阵、移山术、召唤元素和异界之门的生成（都需要在游戏里看一下）。
