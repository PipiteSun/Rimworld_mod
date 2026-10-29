# DND边缘大陆 1.58.1「专长第二批」实现报告

规格：`ArtSource/TraitsFeats-1.58.1-spec.md`。备份：`ArtSource/backup-1.58.0`。没有 d20，全部百分比 / 倍率。

## 一、先补测 1.58.0 改到的共享系统

| 测试 | 结果 |
|---|---|
| `-Only Faith` | PASS 17 |
| `-Only Ascension` | PASS 15 |
| `-VPE -Only HeroTraits` | 第一次 FAIL：`-VPE -Only` 走 VPE 桥的检查组，桥里没有 `HeroTraitsChecks`。补上 `Bridges/VPE/Source/HeroTraitsVPEChecks.cs`（魔网之子 VPE 熵 ×0.75 / 涌动期间 0 且不进冷却；神之勇士牧师 / 圣武士路径经验 ×1.3）后 PASS（3 条） |

1.58.0 的共享改动没有引起旧组失败。

## 二、改动文件

新文件
- `Defs/Adventurer_Feats2.xml`：27 个专长 def（通用 13 种 = 18 个 def：元素使 4 支、坚毅心智 3 支；种族 9），5 个能力，6 个状态，2 个心情，物品“点心” LP_FeatSnack。
- `Source/AdventurerFeats2.cs`：`Feats2`（缓存标记、种族 / 同组前置、起源专长、各专长规则）、6 个 FeatWorker、能力组件 `CompAbilityEffect_FeatPower`、`Hediff_FeatDisguised`、`StatPart_GnomishCunning`、Harmony 补丁、设置“起源专长”。
- `Source/Feats2Checks.cs`：`-Only Feats2`（20 条）。
- `Bridges/VPE/Source/Feats2VPEChecks.cs`、`HeroTraitsVPEChecks.cs`：`-VPE -Only Feats2 / HeroTraits`。
- `ArtSource/RaceArt/draw_feats2_158.py` → `Textures/LP/Feats/*.png`（25 张）、`Textures/LP/Items/FeatSnack.png`。

改动
- `Source/AdventurerFeats.cs`：`LP_FeatDef` 加 `category`、`requiredXenotypesAny`、`requiredGenesAny`、`requireLich`、`exclusiveGroup`、`element`、`origin`、`grantAbility`；`FeatWorker.Unmet` 加种族与同组判断；基类 Gained / Lost 发放 / 收回 `grantAbility`。
- `Source/Adventurer.cs`：`originFeat`、`originFeatGiven`（存档）；AddFeat / RemoveFeat 让 1.58 缓存失效；TryAdd 发起源专长。
- `Source/AdventurerUI.cs`：种族专长只对本族显示；起源专长标“（起源）”。
- `Source/HeroTraits.cs`：HeroInfo 加专长标记（Feats2.Build 合进去）；“DND 特性”分组加“起源专长”；GameComponent 读档补发起源专长、刷新观察者集合、音乐家名单。
- `Source/HeroTraitsCombat.cs`：OutFactor（斗殴者、元素使，社交斗殴在敌对判断之前）、InFactor（元素使无视 1.58.0 的元素抗性和邪魔火抗）、命中后、射击（精灵精准）、冷却 / 熵（仪式余韵）。
- `Source/HeroDestiny.cs`（仪式余韵的冷却 / VPE 熵）、`HeroDeeds.cs`（嗜血再生）、`AdventurerEpic.cs`（EpicFate 先判命匣之誓）、`Fiends.cs`（`Sees` 加观察者）、`OncePerCooldown.cs`（好运连连的冷却与“借来的好运”）、`AdventurerChecks2.cs`（旧检查只数第一批 12 个专长）。
- `Bridges/VPE/Source/HeroTraitsVPE.cs`：魔法学徒 / 仪式施法者的 VPE 钩子。
- `About/About.xml` 1.58.1；`使用说明.txt` 三处（diff 确认只多了 23 行）。

## 三、每个专长的实际规则

| 专长 | 前置 | 规则 |
|---|---|---|
| 魔法学徒 | 无 | VPE：智识 / 社交 / max(种植, 驯兽) 最高的一项 → 法师 / 牧师 / 德鲁伊，学会该路径等级最低的能力（不占路径点、不解锁路径）；无 VPE：魔能飞弹（必中 8 魔能，射程 20，冷却 20000 刻） |
| 酒馆斗殴者 | 无 | 徒手伤害 ×1.5，社交斗殴 ×2；徒手命中 25% 推开 1 格 |
| 工匠 | 无 | GeneralLaborSpeed ×1.1；谈判人买武器 / 衣甲 / 工具 ×0.9 |
| 技能专家 | 无 | 等级最高、兴趣 < 大 的 3 个技能兴趣 +1 级 |
| 音乐家 | 魅力 13 或艺术 6 | 演奏时 10 格殖民者 +4（0.5 天）；每天 1 次：一位听众“激励”6 小时（手术 / 谈判 / 射击 ×1.2） |
| 元素使·X | 职业路径或圣徽；四选一 | 该元素伤害的基因 / 状态伤害系数 <1 时按 1；1.58.0 元素抗性、邪魔火抗不算；该元素伤害 ×1.1 |
| 观察者 | 智力或感知 13 | ResearchSpeed ×1.1；走 Fiends.Sees 的“看得见隐身”通道 |
| 演员 | 魅力 13 | TradePriceImprovement +0.05，NegotiationAbility ×1.15；伪装 3600 刻：敌人射击目标分数 ×0.1 |
| 心灵感应 | 无 | 安抚心灵：30 格，正在精神崩溃的殖民者 RecoverFromState，冷却 1 天 |
| 坚毅心智 | 三选一 | 精神崩溃阈值 -0.08 / 毒素抗性 +0.4 / 免疫获得速度 ×1.25 |
| 厨师 | 烹饪 6 | 做饭（产物是 meal）20% 多一份点心：营养 0.3、心情 +5（1 天）、伤口愈合 ×1.25（6 小时） |
| 毒师 | 手工或医疗 6 | 涂毒：消耗 1 份草药（先身上后地图），10 次近战命中附加 LP_DrowVenom |
| 仪式施法者 | 智力或感知 13 + 职业路径或圣徽 | 引导 41.6 秒（1 游戏小时）；冷却剩最久的原版能力 ResetCooldown、VPE 能力 cooldown=0；“仪式余韵”1 小时：下一次施法冷却 0、熵 0；冷却 1 天 |
| 矮人坚毅 | 山地 / 丘陵矮人 | 毒抗 +0.25；每天第一次喝酒（Alcohol）治 15 |
| 龙威 | 力能龙裔 | 自己不带武器的伤害命中：25% PanicFlee 300 刻（首领、传奇单位无效） |
| 炼狱之怒 | 提夫林、阿斯莫 | 被敌人打中后蓄势，下一击 +8 Flame；600 刻冷却 |
| 精灵精准 | 三种精灵 | 射击 ×1.08；卓尔地面光照 <0.3、高等 / 木精灵天光 ≥0.5 再 ×1.05 |
| 熔岩之握 | 火巨人 | 徒手或钝器近战命中 30% 点燃；在 LP_FireGiantForge 做东西品质 +1 档 10% |
| 嗜血再生 | 巨魔 | 近战击杀治 30（从最重的伤开始） |
| 命匣之誓 | 巫妖 | 命匣和本人同一张地图时：伤害 ≥999 的致命一击或丢了头 → 恢复头部、Epic.Rescue、倒地 |
| 好运连连 | 半身人 | 半身人的幸运冷却 ×2/3（3 天 → 2 天）；触发时 3 格内一个同阵营人形得到“借来的好运”（3 天内下一次致倒地 / 致死的伤 ×0.1） |
| 侏儒机巧 | 三种侏儒 | LP_MindResist +0.2；做发条玩具 / 零部件 / 机械制品时 GeneralLaborSpeed ×1.2 |

起源专长：得到冒险者等级时（生成、13 岁生日；开关“起源专长”开着）发一次，标记 `originFeatGiven`；读档时没有标记的人补发一次。表按规格；已有同名专长改发技能专家，技能专家也有就不发；起源专长也能被敌人得到；死灵造物和动物没有冒险者等级，所以没有。

敌人专长表：酒馆斗殴者（近战）、元素使（施法）、观察者、坚毅心智（任何）、毒师（近战）、种族专长（任何 / 近战 / 远程，按种族前置过滤）。法术狙击原本就是施法类。

## 四、决定

1. 元素使、坚毅心智的“选一项”做成每支一个 def + `exclusiveGroup`（同组只能选一个），选择就在原来的专长列表里完成，不加新窗口。
2. 魔法学徒、技能专家的“选”改为自动选：魔法学徒按技能最高的路径（法师 / 牧师 / 德鲁伊，不含术士），技能专家选等级最高的 3 个。设备上没有 VSE，没写 VSE 兴趣接口；兴趣值超过“大”的（其他模组的自定义兴趣）不动。
3. 音乐家的“一次性 +20%”做成 6 小时的状态（手术成功率、谈判、射击精度 ×1.2），不追踪“下一次”。
4. 魔能飞弹的“每天 3 次”做成 20000 刻冷却。
5. 演员的“不优先瞄准”只影响射击目标选择（AttackTargetFinder.GetShootingTargetScore ×0.1），近战选目标不变。
6. 观察者的“暗影行走”：设备上的暗影行走是穿墙，不是隐身；按“隐身单位”（心灵隐形、隐身药水、暗影帷幕、黑暗术）处理。
7. 龙威的“龙息”= 力能龙裔不带武器的伤害；“龙裔血统”按力能龙裔异种型。
8. 命匣之誓的“即死”= 单次伤害 ≥999（死亡律令是 9999）或头部缺失；“斩首类”按被砍头处理（设备上的 1.19.0“斩首”是领队机制，不是即死）。
9. 工匠：1.7 没有锻造 / 裁缝速度属性，只加一般劳动速度。
10. 仪式施放的“引导 1 小时”用能力预热（41.6 秒 = 2500 刻）实现，被打断就不生效；只在有能力冷却中时可用。

## 五、偏差

- 魔法学徒、技能专家没有手选界面（见决定 2）。
- 元素使对 1.58.0 自己的元素抗性也生效（规格只说目标抗性，等价）。

## 六、新增 Harmony 钩子与周期代码

伤害、命中、射击类全部挂在 1.58.0 已有的钩子里（`HeroDamage`、`HeroAfterHit`、`HeroShot`、`HeroCooldown`、`HeroEntropy`），快速返回不变：两边 `HeroTraits.Info` 都是 null 就返回。专长标记合进同一个缓存，AddFeat / RemoveFeat 时失效。

| 钩子 | 方法 | 快速返回条件 | 频率估计 |
|---|---|---|---|
| Feats2GeneFactor | `Pawn_GeneTracker.FactorForDamage` 后缀 | 系数 ≥1，或伤害来源不是人 → 返回；再查攻击者缓存 | 每次伤害（有基因的目标） |
| Feats2HediffFactor | `HediffSet.FactorForDamage` 后缀 | 同上 | 每次伤害 |
| Feats2CrafterPrice | `Tradeable.GetPriceFor` 后缀 | 不是买入或没有谈判人 → 返回 | 交易界面里 |
| Feats2DisguiseTarget | `AttackTargetFinder.GetShootingTargetScore` 后缀 | 伪装集合为空 → 返回 | AI 选射击目标 |
| Feats2ChefSnack | `GenRecipe.MakeRecipeProducts` 后缀 | 工人为空 / 配方不是 meal → 返回 | 每次完成工作单 |
| Feats2DwarfAle | `Thing.Ingested` 后缀 | 吃的人缓存为 null 或没有矮人坚毅 → 返回 | 每次进食 |
| Feats2MagmaForge | `QualityUtility.GenerateQualityCreatedByPawn` 后缀（低优先级） | 缓存为 null 或没有熔岩之握 → 返回 | 每次做出带品质的东西 |
| StatPart_GnomishCunning | GeneralLaborSpeed 的 StatPart | 当前工作单不是机械类 → 不变 | 读取属性时 |
| VPE：HeroWeaveCooldown 扩展 | `VEF Ability.Cast` 后缀 | 不是灵能 / 没进冷却 / 缓存 null → 返回 | 每次 VPE 施法 |

旧钩子里新加的判断：`EpicFate` 先查命匣之誓（每次 Kill 一次缓存查找）、`Fiends.Sees`（观察者集合，空集合直接跳过）、`OncePerCooldownInjury`（借来的好运集合，空集合直接跳过）、`HeroKill` 的击杀者（嗜血再生）。

周期：`GameComponent_HeroTraits` 250 刻多一项“音乐家”（只遍历正在演奏的玩家音乐家）；名单在 2500 刻刷新。读档时一次性补发起源专长、重建观察者集合。没有新增每刻代码。

## 七、测试

- `-Only Feats2`：PASS 20（连续 3 次；第一版有两处不稳的检查：观察者用随机小人的研究速度会被下限截住、仪式施法者的冷却标志要到下一刻才清掉，改成看冒险者状态的系数和剩余冷却刻数后稳定）。
- `-Only Adventurer`：PASS 123（旧检查只数第一批 12 个专长）。
- `-VPE -Only Feats2`：PASS（魔法学徒学会法师路径第一个能力、不占点不解锁；仪式施法者重置 VPE 冷却、余韵期间熵 0）。
- 一次批量运行时日志过滤报过错：`SmithingSpeed`、`TailoringSpeed` 在 1.7 不存在（已删掉）。另有一次 Feats2 运行报“Errors found in test log”但没留下具体行，之后连续 3 次干净，没能复现。

## 八、没做 / 风险

- 心灵感应、伪装、毒师、魔能飞弹、点心没有专门的测试（规则简单，靠能力组件）；命匣之誓、熔岩之握的锻炉品质、龙威、音乐家只测了数值来源，没在游戏里实际触发。
- 起源专长在开关打开的读档时补发；关着时不补，之后打开会在下次读档补。
- `-Only Feats2` 之外的旧组没有重跑（规格只要求这三组）。
