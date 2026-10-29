# DND边缘大陆 1.58.0「强力特性」实现报告

规格：`ArtSource/TraitsFeats-1.58.0-spec.md`（只做 1.58.0；1.58.1～1.58.3 另开会话）。备份：`ArtSource/backup-1.57.0`。
Peter 的要求照做：没有 d20 掷骰层，全部是百分比 / 倍率映射；所有决定由线程自己做（见“决定”）。

## 一、改动文件

新文件
- `Defs/HeroTraits.xml`：26 个 TraitDef（金 6、紫 13、青 7）、10 个 HediffDef、3 个 ThoughtDef、偿契信件 LetterDef、7 个能力。
- `Source/HeroTraits.cs`：DefOf、`HeroInfo` 缓存、分类、生成掷骰、敌人掷骰、加点规则（倍率 / 上限 / 属性提升）、`DndCreatureType`、存档状态 `Hediff_HeroRecord`、`GameComponent_HeroTraits`、特性变动 / 13 岁生日 / 悬停说明挂钩、设置“DND 特性”。
- `Source/HeroTraitsCombat.cs`：所有战斗规则和对应 Harmony 挂钩。
- `Source/HeroDestiny.cs`：天命（阶段、预言、命运未尽、邪契魔能爆与三支能力、偿契、魔网之子、神之勇士与神明干预）、能力效果组件、`Hediff_HeroTorment`、偿契信件。
- `Source/HeroDeeds.cs`：传说事迹的判定与授予（击杀、心灵震爆、支配、复活、命匣、绝境英雄的袭击采样）。
- `Source/HeroTraitsIsekai.cs`：天赋·某项 × Isekai 白色属性特性的按 degree 互斥。
- `Source/HeroTraitsChecks.cs`：自检组 `-Only HeroTraits`（25 条）。
- `Bridges/Isekai/Patches/IsekaiHeroTraits.xml`：Isekai 启用时双方 conflictingTraits（17 个条件补丁，每个独立，`success Always`）。
- `Bridges/VPE/Source/HeroTraitsVPE.cs`：魔网之子的 VPE 灵能熵 / 冷却。

改动的旧文件（都是“没有 1.58 特性时行为不变”的插入）
- `Defs/Adventurer_Talents.xml`：四个天赋 commonality 0.15 → 0，加 exclusionTag `LP_PowerTrait`。
- `Source/AdventurerAffinity.cs`：`LP_AdventurerAffinityExt` 新字段（scoreCapOffset、legendPointsPerLevelOffset、extraAsiLevels、asiBonusPoints、legendAllFactor、paragonScore、giftedScore、removeWeak、legendStrongFactor、legendFloor/Factor、capHigherOf/Bonus）；ScoreMult / LegendMult 末尾经 HeroTraits 取较大；悬停说明写出特性来源；生成后缀里调用 `HeroTraits.OnGenerated`。
- `Source/Adventurer.cs`：`AsiAt / LegendAt`（LevelPerks 与升级信件共用）、`ASIPoints`、`Cap` 加特性上限；`RaidRecord.lastStand`（存档）；袭击结算调用 `HeroDeeds.RaidDefended`。
- `Source/AdventurerUI.cs`：自测进程置 `HeroTraits.TestSuppressed`。
- `Source/AdventurerLegend.cs`：传奇之胚（成为传奇 +10、500/1000 杀翻倍，`KillBonusFor`）。
- `Source/AdventurerEpic.cs`：救命部分拆成 `Epic.Rescue`；`EpicFate` 先判命运未尽；宿敌冠军掷强力特性 / 事迹并写进来信；同调者击杀冠军 20% 多掉一件魔法物品。
- `Source/AdventurerEnemies.cs`：剧情首领 `EnemyBoss`、袭击领队 `EnemyLeader`。
- `Source/Legendary.cs`：传奇单位去掉 `LP_PowerTrait`；骇人威仪先看屠龙者 / 宿敌克星。
- `Source/MagicItems.cs`：戒指上限 +同调者。`Source/Guns.cs`：法杖 ×魔网之子、魔网崩溃豁免。`Source/NetherilAscension.cs`、`Bridges/VPE/Source/NetherilVPE.cs`：魔网震颤 / 崩溃豁免。
- `Source/Faith.cs`：祈祷神眷 ×神之勇士（只加一行，1.56.0 的 SetPatron 不动）。`Source/BackgroundTraits.cs`：职业经验 ×神之勇士。`Source/Ascension.cs`：`AscensionProgress.Add` ×神之勇士。`Source/Demigod.cs`：半神等级起点 +1。
- `Source/MindFlayerPowers.cs`（支配结束 → 心智堡垒）、`Source/MindResist.cs`（`DNDMind.TryResist` 先看心智堡垒 / 法术抗体）、`Source/LegendaryMonsters.cs`（巫妖克星不会被吞魂）。
- `Source/Core.cs`：左栏“背景特性”后加“DND 特性”分组，`ExposeHeroTraits`。`Source/AdventurerChecks.cs`：分组数 17 → 18。`Source/AdventurerChecks5.cs`：天赋 commonality 检查改为 0 + 共用名额。
- `About/About.xml`：1.58.0，描述末尾加一句。`使用说明.txt`：第一部分一行、第二部分【1.58.0 强力特性】、更新日志一行（diff 确认只多了这 19 行）。

## 二、每个特性的实际数值

名额：金 / 紫 / 1.51.0 天赋共用一个（`LP_PowerTrait`），commonality 0，生成后缀掷一次 `RollKind`；新特性顶掉旧的（`Give` 先清空名额）。传说事迹不占名额。

| 档位 | 种族 | 金 | 紫 | 1.51.0 天赋 |
|---|---|---|---|---|
| 普通 | 人类、半兽人、两种半身人、三种侏儒（含地侏）、半精灵、狗头人、地精三族 | 1% | 4% | 2% |
| 中等 | 其他非传奇 DND 种族（精灵三支、矮人两支、巨魔、食人魔、元素裔、提夫林……） | 0.4% | 3% | 2% |
| 传奇 | 亲和表里 `L` 的种族 + 半神 | 0 | 1%（只抽反骨、勤勉、六个强化天赋） | 0 |

都乘“强力特性出现率”（0～300%，默认 100%）。金色 6 项平分（极致六支算一项、天命各一项），紫色各项平分（分支算一项，天赋·某项排除种族本来擅长的项和 Isekai 对应特性）。

| 特性 | 数值 |
|---|---|
| 英雄之资 | 所有传奇项 ×1.5（取较大）、21～40 级每级 3 传奇点、六维上限 +2（22）、属性提升每次 3 点 |
| 极致·X | X ×2.5、X 上限 26、对应两个传奇项 ×2 |
| 天赋·X | X ×1.5 |
| 反骨 | 笨拙 ×0.5 → ×1；属性提升 3 点 |
| 金刚之躯 / 锋芒 / 灵光 | 传奇 血量承受回复 / 近战远程 / 心灵学习 ×1.5；灵光：智力、感知较高一项上限 +2 |
| 勤勉 | 6、10、14、18 级各 +1 次属性提升 |
| 传奇之胚 | 成为传奇 +10 传奇点；500/1000 杀各 6 点（原 3） |
| 图腾之魂·熊 | 健康 <50% 时非心灵伤害 ×0.65；疼痛休克阈值 +10% |
| 图腾之魂·狼 | 标记 480 刻：其他同阵营近战命中 +15%；近战命中 10% 眩晕 60 刻（体型 <3）；移速 +0.3 |
| 图腾之魂·鹰 | 掩体减免 ×0.5（有神射手时不生效）；带武器的远程射程 ×1.15 |
| 宿敌猎人·X | 对宿敌伤害 +25%、近战命中 +15%、射击瞄准 ×1.15；每天最多提示一次；杀满 50 → 宿敌克星 +40%、免疫宿敌的眩晕和骇人威仪 |
| 同调者 | 身上魔法物品的 equippedStatOffsets 与 Hediff_MagicItem 阶段属性再加 25%；戒指 3 枚；击杀宿敌冠军 20% 多掉一件 |
| 元素之触·X | 带武器（含徒手）命中 +3 点（火 Burn / 冰 LP_Frost + 减速 2 秒 / 雷 LP_Shock + 10% 跳 2 格 / 酸 LP_AlchemyAcid）；受到该元素 ×0.5；该元素法术 ×1.3；同元素元素裔 ×0.25 且无附伤 |
| 法术抗体 | 敌方法术伤害 ×0.65；敌方眩晕 / 控制类精神状态 / 心智判定 50% 无效；友方给的增益（原版 GiveHediff 能力）50% 落空；心灵敏感度 -20% |
| 决斗者 | 贴身敌人 =1：近战命中 +25%、闪避 +25%，闪避后 50% 下一刻反击一次；≥3：命中 -10%（250 刻更新） |
| 预言之子 | 命运未尽冷却 20 / 12 天；预言目标 +25%；显现倒地 5 格友军命中 +10%（1 小时）；升华：下一级所需经验 ×100%、全殖民地 +8（10 天）、命运主宰（每天一次必中 ×2） |
| 邪契者 | 魔能爆 12 伤害 / 穿透 30%、道数 1/2/3/4（1/5/11/17 级）、冷却 240 刻；邪魔吸收 15+等级（≤60，1 天）、显现点燃、升华火焰 ×0.5 + 直坠地狱（眩晕 10 秒，每秒 6 灼烧）；妖精步 8 格 / 30 秒、显现 20% 迷惑 3 秒、升华魅惑 6 格 10 秒；旧日敏感度 +20%、显现眩晕 1 秒（同目标 10 秒）、升华触须半径 3、15 秒、每秒 3 伤害 |
| 偿契 | 15～20 天一次，期限 3 天；邪魔：处决或 500 银；妖精：派对 / 婚礼或优秀以上艺术品；旧日：冥想 8 小时或读耐瑟瑞尔卷轴 1 小时；失败：宗主之怒 10 天 + 心情 -10；连败 2 次降一阶；觉醒阶连败 2 次特性消失 + 背弃之契 -15（15 天） |
| 魔网之子 | 法杖 ×1.2；VPE 熵 ×0.75 / 无 VPE 敏感度 +15%；显现 VPE 15% 不进冷却 / 无 VPE 法杖射程 +3；升华魔网涌动 3600 刻（熵 0、冷却 0），魔网震颤 / 崩溃豁免 |
| 神之勇士 | 牧师 / 圣武士路径经验 ×1.3、祈祷神眷 ×2、敌对类型 +20%；显现成神进度 ×1.5、领域神术冷却 ×0.75；升华神明干预（每季一次）；成神半神等级从 2 起步 |
| 屠龙者 | 对龙 +30%、龙威免疫、龙的无武器伤害 ×0.7；5 点 |
| 巨人克星 | 近战打体型 ≥1.5 +30%、传奇血量至少 ×1.25；3 点（需近战参与击杀 3 个体型 ≥2 的巨人） |
| 巫妖克星 | 对不死 +30%、吞魂之嚎免疫、传奇心灵至少 ×1.25；3 点 |
| 弑神者 | 对神力 +35%、他们的无武器伤害 ×0.65、六维上限 +1、1 枚神迹残片；5 点 |
| 心智堡垒 | 心智判定必定抵抗、控制类精神状态免疫、心灵类伤害 ×0.5、精神崩溃阈值 -5%；3 点 |
| 冥界归来 | 死亡类负面记忆不进来、对不死 +20%、每 30 天一次倒地后 2 小时不失血；2 点 |
| 绝境英雄 | 疼痛休克阈值 +10%、+1 次属性提升、25% 英雄之资（已有金色改 +3 传奇点） |

类型加成（宿敌、预言目标、神之勇士、屠龙者、巨人克星、巫妖克星、弑神者、冥界归来）合计封顶 +60%。

神之勇士的敌对类型与神迹：

| 神 | 敌对类型（+20%） | 神明干预 |
|---|---|---|
| 洛山达 | 不死 | 全员伤口治愈 60%、止血 |
| 提尔 | 邪魔 | 全员近战伤害 ×1.5，30 秒 |
| 密斯特拉 | 异怪 | 打断施法中的敌人，敌人眩晕 5 秒 |
| 凯兰沃 | 不死 | 倒地者止血 + 1 天不流血 |
| 马拉尔 | 野兽 | 敌人恐慌逃跑 10 秒（不能恐慌的眩晕 10 秒） |
| 塔洛斯 | 巨人 | 最多 6 道闪电，每道 30 电击 |
| 班恩 | 神力（灌注 / 附体 / 半神） | 敌人惊惧 1 小时 |
| 莎尔 | 异怪 | 殖民者隐身 30 秒 |
| 苏尔特 | 龙 | 敌人着火 + 20 火焰伤害 |

每位神另外算“供奉其敌对神的派系”（派系 FaithExtension）。

## 三、决定

1. 1.51.0 四个天赋并进同一次掷骰：普通 / 中等 2%、传奇 0（原 commonality 0.15 在原版特性池里大约这个量级），效果不变；它们的名字不上色（老检查和文本按原名）。
2. 档位：人类和半兽人归普通；所有非传奇、又不在普通名单里的 DND 种族归中等（规格只点名了精灵 / 矮人一系）。
3. 生成率自检用固定随机种子掷 10000 次（规格写 1000 人），避免 ±50% 的统计抖动。
4. 天赋·某项 × Isekai 白色属性是按 degree 的，原版 conflictingTraits 做不到 → 生成时不抽这一支、GainTrait 时拦掉后来的那个（力大 / 敏捷 / 坚韧 / 聪慧 / 开悟 / 巧舌）。
5. “法术 / 神术伤害”= 人造成的、不带武器的伤害（能力、魔能爆、吐息）加上法杖；近战（武器或徒手）、枪械、手雷都不算。
6. 元素归类按 DamageDef 名字（Acid/Corros → 酸，Frost/Cold/Ice… → 冰，Shock/Lightning… → 雷，Flame/Fire/Burn/Heat… → 火）。
7. 妖精的“迷惑”“魅惑”都做成眩晕（失神），不改阵营；首领 = 领队、剧情等级、宿敌冠军、传奇冒险者、传奇单位、半神、神明附体者。
8. 直坠地狱 = 眩晕 10 秒 + 每秒 6 灼烧（共 60）；异界触须 = 眩晕 15 秒 + 每秒 3 魔能伤害。
9. 偿契降阶后“锁阶”：等级条件满足也不回升，下一次偿契成功解锁。连续失败计数在降阶时清零。觉醒阶连败两次 → 特性消失。
10. 邪魔活力每次击杀叠加，最多 60，持续 1 天。
11. 神明干预：第 3 名殖民者在同一张地图倒地时触发，每位勇士每季（15 天）一次，多个勇士只触发一个。
12. 弑神者的“神力灌注 ≥5 级敌人”理解为冒险者等级 ≥5 的神力灌注者，另加神明附体者和半神。
13. 屠龙者的龙：五色真龙、非人形的龙（Isekai 龙、古龙），力能龙裔只算首领。
14. 巨人克星的“近战参与”：最后一击是近战，或参与者当时拿近战武器 / 空手。
15. 绝境英雄：地图上至少 2 名能使用暴力的殖民者时才判定，250 刻采样，记在袭击记录里（存档）；觉醒的英雄之资顶掉原来的紫色 / 天赋。
16. 敌人的“仇敌派系”就是玩家派系。“敌人携带强力特性”关闭时，敌对派系的人自然生成也不掷。
17. 魔网之子：设备上没有“黄昏之年魔网紊乱”，对应到 1.37.0 的魔网震颤（冷却 ×2）和魔网崩溃；15% 不进冷却只在装 VPE 时（规格给了无 VPE 的替代效果）。
18. 神之勇士的祈祷 ×2 不论守护神是不是本神；成神进度只放大正数（随机取整）。
19. 冥界归来的“死亡类事件”= defName 含 Died / Death / Dead 且是负面的记忆。
20. 心灵震爆在设备上没有伤害（是眩晕），“-50%”落在心灵类伤害上；免疫走 DNDMind.TryResist（支配、震爆、心灵束缚都经过它）。
21. 宿敌提示每个猎人每天最多一次（找到宿敌后 1 天内不再扫）。
22. 预言之子只靠预言兑现升华（不看传奇）。
23. 预言目标 +25%：龙 / 巨人 / 不死（拉洛克）/ 宿敌冠军 / 神力 / 袭击领队。

## 四、偏差（和规格不同、以设备为准）

- 改名：邪契者 `LP_PactDestiny`（`LP_PactBound` 是 1.55.0 的签约状态）、屠龙者 `LP_DeedDragonslayer`（`LP_DragonSlayer` 是 1.45.0 龙杀手）、弑神者 `LP_DeedGodslayer`（`LP_Godslayer` 是 1.40.0 篡火者特性）。
- 神之勇士 degree 1～9（设备上可信奉的神是 9 位，规格写 8 位；degree = 信徒等级）。
- 同调者“开宝箱掉率 +20%”没做（宝箱掉落在 Isekai 地下城里，没有现成的单人钩子），只做了宿敌冠军。
- 偿契连败触发讨债者（规格写“可选”）没做。
- 法术抗体“友方增益 50% 无效”只覆盖原版“给状态”类能力（CompAbilityEffect_GiveHediff），VPE 和信仰神术的增益不经过它。

## 五、新增 Harmony 钩子与周期代码

快速返回：`HeroTraits.Info(pawn)` 是按 pawn 的字典缓存，没有 1.58 特性的人缓存为 null；特性变动（GainTrait / RemoveTrait）、记录状态新建时失效；读档、换存档时整体清空；每天清掉已销毁的 pawn。

| 钩子 | 方法 | 快速返回条件 | 频率估计 |
|---|---|---|---|
| HeroDamage | `Pawn.PreApplyDamage` 前缀 | 伤害无效 → 返回；受害者和攻击者 Info 都是 null → 返回（两次字典查找） | 每次伤害 |
| HeroAfterHit | `Thing.TakeDamage` 后缀 | 自己追加的伤害；命匣只记最后一击；不是人打人 → 返回；两边 Info 都 null → 返回 | 每次伤害（含建筑） |
| HeroMeleeHit | `Verb_MeleeAttack.GetNonMissChance` 后缀（优先级高，幸运之前） | 狼标记表空、增益集合空、攻击者 Info null → 加值 0 返回 | 每次近战 |
| HeroDodge | `Verb_MeleeAttack.GetDodgeChance` 后缀 | 目标不是人；攻击者没有命运主宰且目标不是决斗者 → 返回 | 每次近战 |
| HeroShot | `ShotReport.HitReportFor` 后缀（优先级低） | 近战 / 非人；Info null 且增益集合空 → 返回 | 每次射击 + 瞄准提示 |
| HeroRange | `VerbProperties.AdjustedRange` 后缀 | 射程 ≤0 / 非人 / Info null → 返回 | 频繁（AI 选目标），O(1) |
| HeroStun | `StunHandler.StunFor` 前缀 | ticks≤0 / 非人 / Info null / 来源不是敌对的人 → 放行 | 每次眩晕 |
| HeroMental | `MentalStateHandler.TryStartMentalState` 前缀 | 心情引起的；Info null；没有心智堡垒 / 法术抗体；不是控制类 → 放行 | 每次精神状态尝试 |
| HeroWardedBuff | `CompAbilityEffect_GiveHediff.Apply` 前缀 | 目标 Info null 或不是法术抗体 → 放行 | 每次给状态能力 |
| HeroCooldown | `Ability.StartCooldown` 前缀 | ticks≤0；Info null；不是魔网之子也不是神之勇士 → 返回 | 每次施法 |
| HeroEntropy | `Pawn_PsychicEntropyTracker.TryAddEntropy` 前缀 | 数值≤0；Info null 或不是魔网之子 → 返回 | 每次加熵 |
| HeroDowned | `Pawn_HealthTracker.MakeDowned` 后缀 | Info null 且没有升华的神之勇士 → 返回 | 每次倒地 |
| HeroDeathMemory | `MemoryThoughtHandler.TryGainMemory(Thought_Memory,Pawn)` 前缀 | Info null 或不是冥界归来 → 放行（结果按 ThoughtDef 缓存） | 每次得到记忆 |
| HeroKill | `Pawn.Kill` 前缀 + 后缀（First） | 已死；拷一份伤害分成（同 LegendaryKill），后缀只处理参与者 | 每次死亡 |
| HeroResurrected | `ResurrectionUtility.TryResurrect` 后缀 | 没复活成 → 返回 | 罕见 |
| HeroPhylacteryDestroyed | `Phylactery.Destroy` 前缀 | 不是被打毁（KillFinalize）→ 返回 | 罕见 |
| HeroExecution / Gathering / Wedding | 处决、聚会、婚礼后缀 | 只写时间戳 | 罕见 |
| HeroGain / HeroLose | `TraitSet.GainTrait / RemoveTrait` 后缀 | 不是 1.58 特性（HashSet）→ 返回 | 特性变动 |
| HeroIsekaiGain | `TraitSet.GainTrait` 前缀 | 不是天赋·某项、名字不以 Isekai_ 开头 → 放行 | 特性变动 |
| HeroBirthday | `Pawn_AgeTracker.BirthdayBiological` 后缀 | 不是 13 岁 → 返回 | 每人每年一次 |
| HeroTip | `Trait.TipString` 后缀 | 不是 1.58 特性 → 返回 | 只在界面 |
| HeroHediffAdded | `Pawn_HealthTracker.AddHediff` 后缀 | 类型 / def 比较 | 每次加状态 |
| HeroWeaveEntropy（VPE 桥） | `AbilityExtension_Psycast.GetEntropyUsedByPawn` 后缀 | Info null / 不是魔网之子 → 系数 1 | 每次 VPE 施法 |
| HeroWeaveCooldown（VPE 桥） | `VEF.Abilities.Ability.Cast` 后缀 | 不是灵能 / 没有冷却 / 不是魔网之子 → 返回 | 每次 VPE 施法 |

旧钩子里加的调用（不是新补丁）：`EpicFate`（每次 Kill 先查一次 Info）、`TalentGenerate`（生成时掷一次）、`Legendary.Presence`、`DNDMind.TryResist`、`AccessorySlotLimit`（穿戒指时）、`Arcana.FactorFor`（法杖每发一次 Info 查找）、`SoulImmune`。

周期代码
- `GameComponent_HeroTraits.GameComponentTick`：每刻只判断反击队列是否为空。
  - 250 刻（偏移 113）：决斗者贴身计数（每个决斗者查 8 格）；宿敌提示（只扫有猎人的地图，每个猎人找到后 1 天内不再扫）；旧日偿契的冥想 / 研读计时（只看有当期要求的人）；绝境英雄采样（只在有进行中的袭击时）；狼标记过期。
  - 2500 刻（偏移 1213）：刷新名单（每张地图遍历一次 AllPawnsSpawned，只做字典查找）；有天命的殖民者升阶与偿契；殖民地半神（预言 4）；每天一次清理缓存。
- `Hediff_HeroRecord.TickInterval`：每 250 刻按签名决定要不要重建属性（同调者的魔法物品、魔网之子）。
- `Hediff_HeroTorment`：受刑期间每 60 刻一次伤害（只有中招的人）。
- 没有新增每刻的地图扫描。

## 六、测试结果

- `pwsh Test.ps1 -Only HeroTraits`：PASS 25 条（含存读档）。第一次出现一次失败：神明干预检查里随机生成的小人带旧伤疤，伤口合计超过阈值；检查夹具改为生成后清掉伤口，之后连续通过。
- `pwsh Test.ps1 -Only Adventurer`：PASS 123 条（设置分组数 18、天赋 commonality 0 的检查已同步）。
- `pwsh Test.ps1 -Isekai -Only HeroTraits`：PASS 25 条（Isekai 互斥：双方 conflictingTraits、图腾 × 狂战士之血、法术抗体 × 开悟、天赋·力量在“力大”身上被拦、天赋·敏捷照常）。
- 日志里没有红字（Test.ps1 的日志过滤通过）。
- 按要求没有跑全量；`-VPE` 没跑（VPE 桥只确认能编译）。

自检 25 条：定义与上色、出现率、传奇种族不出金、名额上限、倍率取较大、英雄之资、极致·力量、勤勉、元素之触·火、法术抗体、决斗者、宿敌猎人·龙、类型封顶 60%、天命升阶、命运未尽、命运未尽与命运之恩不双触发、偿契失败降阶 / 特性消失、偿契成功、神明干预、屠龙者、绝境英雄、敌人首领、快速返回、Isekai 互斥、存读档。

## 七、没做 / 风险

- 没做：同调者的宝箱掉率、偿契连败的讨债者、VPE / 信仰神术的“友方增益 50% 无效”。
- 旧存档里已有的人不会补发强力特性（只有新生成的人和 13 岁生日）；1.51.0 天赋保留。
- 改了多处共享代码（命运之恩、宿敌冠军、剧情首领、袭击领队、法杖、魔网崩溃、祈祷、成神进度、半神起点、戒指、心智抗性、吞魂），没有特性的人行为不变，但除 Adventurer 组外没有跑对应的旧检查组（Faith、Ascension、Pact、Netheril、MagicItems、LegendaryMonsters 等）。
- VPE 桥的两个钩子没有在游戏里跑过（`-VPE` 未跑）。
- 特性名用富文本颜色：不支持富文本的地方（日志、个别模组界面）会显示标签原文；我们自己的信件和消息都用去掉标签的名字。
- “法术伤害”按“没有武器”判定，其他模组的能力若带武器 def 会被当成普通攻击。
