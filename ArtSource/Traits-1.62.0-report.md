# DND边缘大陆 1.62.0 DND 性格特性与诅咒之力 — 实现报告

规格：`ArtSource/Traits-1.62.0-spec.md`（标题写 1.63.0，按协调者要求实际版本 1.62.0）。备份：`ArtSource/backup-1.61.0`（开工时设备 1.61.0 + 当天热修，模组根目录除 ArtSource 外全部，2799 个文件）。

## 完成清单

文件：
- `Source/DndTraits.cs`：DndTraitDefOf、LP_TraitWeightExtension、DndInfo 缓存（`DndTraits.Info(p)`，没有这些特性 = null）、生成、规则、Hediff_DndRecord（存档数据）、Hediff_Beastform、能力组件、心情 / 好感 Worker、StatPart、GameComponent_DndTraits（250 / 2500 刻）、全部 Harmony 挂钩、设置。
- `Source/DndTraitsChecks.cs`：`Test.ps1 -Only DndTraits`，22 项（含存读档）。
- `Defs/DndTraits.xml`：18 个性格特性 TraitDef（24 种程度）、3 个诅咒 TraitDef、6 个状态、30 个心情 / 好感、2 个能力。
- `Patches/DndTraits.xml`：塞伊（LP_FaerunHostile）邪恶 ×3 守序 ×2、费伦拓荒同盟仁善 ×2、余烬氏族混乱 ×2、猎巫狂热分子守序 ×3、狗头人（XenotypeDef）龙之眷从 ×5。
- `Bridges/VPE/Source/DndTraitsVPE.cs`：VPE 施法 5% 浪涌；`-VPE -Only DndTraits` 2 项。
- 美术：`ArtSource/RaceArt/draw_dndtraits_162.py` → `Textures/LP/DndTraits/`（狼 / 熊 / 虎 × 头 / 身 × 南东北 18 张 + 心灵冲击、化身野兽 2 个图标），预览 `ArtSource/Preview/DndTraits-1.62.0.png`。

改动的已有文件（都是一两行接入）：
- `Source/HeroTraits.cs`：`IsSlot` 含诅咒；`KindCurse=4`，`RollKind` 四参数重载（三参数版本不变，旧检查不受影响）；`Choices` 加诅咒；`OnGenerated` 按“诅咒之力”开关掷红色；`EnemyBoss` 15% 红色（不给复仇亡魂）；`LetterNote` 列出红色；`ExposeHeroTraits` 调 `ExposeDndTraits`；「DND 特性」分组末尾画新设置。
- `Source/BackgroundTraits.cs`：`ClassXpFactor` 外面包 `DndTraits.ClassXp`（职业天赋 ×1.25 与已有倍率取较高）。
- `Source/ItemThrowables.cs`：`HolyTarget` 加兽化症。
- `Bridges/VPE/Source/HeroTraitsVPE.cs`：VPE 灵能熵再乘 `DndTraits.VpeEntropyFactor`（奥术天赋 ×0.9）。
- `About/About.xml`（1.62.0 + 描述一段）、`使用说明.txt`（第一部分一行、第二部分新章节、更新日志一行）。

规则（全部按规格一～六实现，数值见使用说明）：
- 生成后缀 `DndGenerate` 排在 1.58.0 `TalentGenerate`（Priority.High）之后；只用原版 3 个名额里的空位（强力特性、传说事迹不算）；有空位 40% 给一个、再有空位 15% 给第二个（× 出现率）；每人最多 2 个、阵营最多 1 个；读档不补发。实测 200 人约 30% 至少一个。
- 同一特性不同程度的互斥（仁善 × 心理变态 / 嗜血 / 巫妖信徒，邪恶 × 善良 / 不死猎人，狂野魔法 × 奥术天赋 / 魔法恐惧）在 `TraitSet.GainTrait` 前缀里挡（任何来源都挡）。
- 类型伤害（屠龙志士、巨人杀手、不死猎人、复仇亡魂 / 安息之魂）在自己的 `PreApplyDamage` 前缀里补差：总加成 = min(60%, 1.58.0 部分 + 本版部分)。
- 制伏联动：`Nonlethal.Subdue` 后缀，仁善者把敌人打到倒地 +2「手下留情」。
- 迷锁地牢联动（寻宝狂）：进入地牢层 +5（250 刻检查）、`Mythal.RollLoot` 后缀 10% 多一件（复制宝物堆里一件，否则随机魔法物品）。
- 设置全部放进已有「DND 特性」分组，分组数不变（AdventurerChecks 不用改）。

## 偏差

1. 版本号：规格写 1.63.0，实际 1.62.0（1.61.0 被制伏武器占用，1.62.0 即本版）；夺魂仪式不存在（暂停），所有夺魂联动跳过（含“性格特性按第 1 档”“复仇亡魂凶手清空”“兽化症保留阶段”）。检查第 21 项（夺魂档位）换成“仁善手下留情 + 邪恶 / 仁善的处决心情”。
2. “阵营算 1 个”理解为：一个人最多一个阵营特性（守序 / 混乱 或 仁善 / 邪恶），不会两条光谱都有。
3. “不占原版上限以外的额外名额”理解为：只填原版 3 个名额的空位；原版已经满 3 个的人不给。为了接近 25% 的目标，第一个的几率定为 40%（规格没给具体数），实测约 30%。
4. 性格特性和诅咒都没有用 TraitDef 的 commonality（都是 0），由代码掷，和 1.58.0 强力特性一致；规格“走原版 commonality”不采用。
5. LP_TraitWeightExtension 除 FactionDef / ThingDef 外也读 XenotypeDef（本模组的种族多数是异种型，狗头人就是）。
6. 守序“不会纵火、乱跑类崩溃”挡在 `MentalStateHandler.TryStartMentalState`（心情引起时），挡掉 FireStartingSpree、RunWild、GiveUpExit、Wander_*；没有挂 `MentalBreakWorker.BreakCanOccur`（子类会重写）。
7. 邪恶 / 仁善的处决、摘器官、奴役心情按 ThoughtDef 名字识别（含 Execut / OrganHarvest / Harvested 和 Enslav / Slavery、且有负面阶段的记忆）；文化戒律的“情境”心情（不是记忆的）不处理。
8. 救援善举挂在 `JobDriver_TakeToBed` 的救援工作成功结束时；包扎囚犯挂 `TendUtility.DoTend`。
9. 秘法上瘾的“想用”只是心情（超过 5 天 -6、用了 +4），小人不会自己去找药水 / 卷轴吃（原版化学兴趣的自动服用没做）。魔法消耗品 = defName 以 LP_ 开头且含 Potion / Scroll / Elixir。
10. 寻宝狂：法师塔坠落有挂钩；“神迹残片 / 宝箱事件”设备上没有合适的事件挂钩，未做。拆古代遗迹 = 拆除无派系的建筑（废墟墙等）时额外掉 20% 材料。
11. 位面错乱的“裂隙野兽不攻击”挂在 `Pawn.ThreatDisabled`（带 Hediff_PlanarSpawn 的搜索者），取决于野兽的寻敌是否走这个判断；传送类能力冷却 -20% 只对原版 / 本模组能力（RimWorld.Ability）生效，VPE 能力不在内。传送类按能力组件类名和 defName 关键词判断（Skip / Teleport / Blink / Step / Soar / Jump）。
12. 狂野魔法：装了 VPE 时 5% 浪涌对 VPE 施法和原版 / 本模组能力都生效；没装 VPE 时按规格只有每天一次（0.5～1.5 天一次）。皮肤变绿复用 1.27 法师塔的变色状态（改成绿色，1 天）。“全队治疗”= 同地图同派系的人各恢复 5 点伤势。
13. 复仇亡魂：尸体在商队里（不在地图上）时等回到地图再复活；“凶手在地图上时不能隐身”理解为凶手对复仇亡魂所在地图无法隐身（IsPsychologicallyInvisible 返回 false）。杀死凶手的奖励 = 下一级跨度的 50%（不乘经验倍率），正常击杀经验照常另算。「亡魂之躯」不会饿死用 hungerRateFactor 0（不再饿）。
14. 兽化症：外观按规格用 HediffDef 的 renderNodeProperties 画头（兽面）+ 身披（兽皮披肩），没有做全身覆盖。失控 = 25 格内有敌人就强制近战攻击，没有时每次化身判定一次 10% 原版狂暴；HP < 30% 触发在两次化身之间至少隔 1 天，避免刚结束就再发作。
15. 远域之触：“提前提示”= 地图上出现敌对夺心魔时每天最多一次警告；“心灵熵上限 +20”用 PsychicEntropyMax 的 StatPart（有这个属性时）；心灵冲击用银剑的灵能伤害类型（LP_SilverPsychic）、锥形 60°、每天 3 次 = 3 次充能、用完 1 天恢复。研读耐瑟瑞尔卷轴 1% 改为“获得耐瑟启迪时 1%”。
16. 奥术天赋没装 VPE 时的心灵敏感度 +10% 是加法（+0.10）。

## 未做

- 夺魂仪式联动（功能不存在）。
- 寻宝狂的神迹残片 / 宝箱事件心情。
- 秘法上瘾的自动服用。
- VPE 传送能力的冷却 -20%。

## 测试结果

2026-10-28，`Source/Build.ps1` 全部通过（主程序集、HAR、VPE、神兽血裔联动）。

| 运行 | 结果 |
|---|---|
| `Test.ps1 -Only DndTraits` | PASS 22（含存读档） |
| `Test.ps1 -Only HeroTraits` | PASS 25（名额回归） |
| `Test.ps1 -Only Nonlethal` | PASS 11 |
| `Test.ps1 -Only MindControl` | PASS 5（日志里有两条原版“Could not generate a pawn after 70/100 tries”，不是本版内容，检查本身通过） |
| `Test.ps1 -Only SharedComponents` | 42 项后失败：“a bridge xenotype colonist on the map halves the relation mood”。**原有的问题**：1.57.0 起两族之桥只减半负面心情，而这条检查在心情已经转为正面阶段（stage 1）之后才测倍率，所以拿到 1 而不是 0.5（性能报告里也记作原有失败）。本版没有改 RacialRelation；世界公民的过滤只对带这个特性的人生效，自检进程里不随机生成性格特性。 |
| `Test.ps1 -VPE -Only DndTraits` | PASS（职业天赋经验 ×1.25 取较高不叠加；奥术天赋灵能熵 ×0.9；狂野魔法 VPE 施法浪涌、法术 ×1.1） |

测试中修掉的问题（都在新内容里）：类型封顶检查改用力能龙裔做龙（设备上没有非人形的龙 PawnKind）；敏感度检查改为同一个人前后比较（冒险者属性会改基准值）；第一个性格特性几率 50% → 40%（实测 36% 偏高）；杀死凶手的经验检查改为“至少一半”（正常击杀经验另算）；手下留情检查改用真实的制伏短棍命中。
