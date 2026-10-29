# DND边缘大陆 1.38.0 报告：流浆人

需求：ArtSource/Oozefolk-1.38.0-spec.md。备份：ArtSource/backup-1.37.0（改动前的 About/About.xml、使用说明.txt、Source/Core.cs、MindFlayerTadpole.cs、Scenarios.cs、FactionChecks.cs、Defs/Factions.xml、FactionTraders.xml，以及 1.7/Assemblies 主 dll 和 HAR / VPE / 神兽血裔三个桥接 dll）。预览图：ArtSource/Oozefolk-1.38.0-preview.png（贴图脚本直接拼的，不是游戏内截图）。

## 测试结果

| 命令 | 结果 |
|---|---|
| `Source\Build.ps1` | PASS（主程序集 + HAR / VPE / 神兽血裔桥接） |
| `Source\Test.ps1 -Only Oozefolk` | PASS：53 条（52 条存档前 + 1 条存读档）；日志无红字。最后一版跑了 2 次都过；“衣服掉落”那条早先偶发失败过一次，已改成检查死者身上全部衣物落在 4 格内（之前的版本又连跑 5 次都过） |
| `Source\Test.ps1 -Only Factions,RaceScenarios,Tadpoles` | 存档前 477 条全过（含同盟种族比例、六个种族剧本、蝌蚪）；读档后那条失败是老问题：`-Only Factions` 找的是 PrepareFactionsSave，实际方法名是 PrepareFactionSave（单数），所以存档前没准备数据。和 `-Only Scenarios` 的已知问题一样，跟这次改动无关 |
| `Source\Test.ps1`（完整 Core，按要求只跑了一次） | 第 168 条失败：`LP_ProtoplasmGel` 可燃但没有耐久（ConfigErrors）。已修（凝胶易燃性改 0）。没有再跑完整套；改为把同样的 ConfigErrors 检查放进 -Only Oozefolk，覆盖这次新增的 33 个定义（物品、配方、基因、能力、伤病、异种型、兵种、剧本、心情、伤害）。完整套里第 168 条之后的检查这次没跑到 |

测试档默认加载 HAR，-Only Oozefolk 也是带 HAR 跑的。

-Only Oozefolk 对应需求第六节：
- **1** 新定义都通过 ConfigErrors；剧本描述三段、最后一段以“注意”开头；12 个生成的流浆人都有本源、四种肤色正好一种（12 人里四种都出现了）、瘦/标准体型、光头；**实测代谢 -1**；原生质 30/100；五个能力齐全；8 个自定义基因都可提取。
- **2**（可选）倍率：钝击 0.5、强酸（AcidBurn 和强酸瓶的 LP_AlchemyAcid）0.25、火焰 1.5、冻伤 1.5、割伤 1。和普通人比实际伤势：强酸 ×0.25、烧伤 ×1.50；出血 0.240 对 0.480。毒素抗性 ≥50%，易燃性比人类高 0.25。钝击的实际伤势没比（原版钝击会随机转到内部骨骼，总量不稳定），只查倍率。
- **4** 吞没人形尸体：+25（10→35）、饱食 20%→70%、尸体消失、衣服（裤子、衬衣）掉在 1 格内、旁边的殖民者有“目睹吞没”；吞没麝牛（体型 2.4）+30、饱食 +60%；流浆人尸体不能吞。
- **5** 分裂：原生质 90→10、饱食 80%→55%、父体分裂虚弱；新生儿同异种型、14 个内源基因一致、只有一种流浆人肤色、父体后加的外源基因（俊美）没复制；同派系、唯一亲代是父体；没有背景特性；原生质不够和婴儿都不能分裂；冷却等于设置（30 天）；生育能力 0。
- **6** 渗过：敌对派系的门对流浆人是锁着的，渗到门另一侧（-10）；隔墙不行（提示“中间隔着墙……”）；直接点门落到门另一侧；原生质不足时不动。
- **9** 双手缺失：流浆人操作 60%，普通人 0%；伪足是 9 钝击 + 4 LP_OozeAcid；重塑长回一只手（-40）。
- **10** -5°C 凝滞 1 小时、移速 ×0.75；戴稳形护带 -5°C 不凝滞、-20°C 凝滞、上限 120；冬狼寒霜迟缓 240→360 刻（普通人 240）。火焰 ×1.5 在第 2 条里。
- **11** 蝌蚪对流浆人无效（Immunity 返回原因，Implant 失败）。**12** 屠宰流浆人尸体只得到 LP_ProtoplasmGel ×3。
- 剧本组件：第一名流浆人原生质 60，塞伊远征军排在第 6 天，点数 300（实测 300）。只排进队列、不落地。
- **13** 存读档：原生质 47、渗过冷却 1234、凝滞剩余 1777 刻、分裂出生记录都保留。

## 基因表（实测）

| 基因 | defName | 代谢（实际） | 需求里的值 |
|---|---|---|---|
| 流浆之躯（本源） | LP_OozeCore | -2 | -2 |
| 原生质 | LP_OozeProtoplasm | **0** | -1 |
| 万物消化 | LP_OozeDigest | -1 | -1 |
| 伪足 | LP_OozePseudopod | -1 | -1 |
| 无定形 | LP_OozeAmorphous | -1 | -1 |
| 分裂繁殖 | LP_OozeSplit | 0 | 0 |
| 凝滞 | LP_OozeChill | **+3** | +2 |
| 怕干 | LP_OozeDesiccate | +2 | +2 |
| 流光（外观） | LP_OozeSheen | 0 | （需求里是头部附件，没写成基因） |
| 四种肤色 | LP_SkinOoze* | 0 | 0 |
| 光头、无胡子 | Hair_BaldOnly、Beard_NoBeardOnly | 0 | 0 |
| 强健免疫 | Immunity_Strong | -1 | -1 |
| 痛觉减弱 | Pain_Reduced | **去掉** | -1 |
| **合计（实测）** | | **-1** | 按需求原值是 -4 |

## 和需求不一样的地方

1. **代谢**：按需求原值合计 -4，去掉 Pain_Reduced 也还是 -3，进不了测试要求的 -2～0。所以：去掉 Pain_Reduced；原生质 -1→0（只是个资源条，本身没有收益，能力都要花它）；凝滞 +2→+3（它同时有舒适下限 +8、0°C 以下凝滞、寒冷伤害/寒霜状态 ×1.5 三项惩罚）。合计 -1，正好是目标值。
2. **流光**做成一个单独的外观基因 LP_OozeSheen（代谢 0、复杂度 0），头部附件挂在它上面（锚点照象鼻 / 牛角的写法，层 66，useSkinShader 按肤色着色）。多画了一个异种型图标（需求的贴图清单里没有）。
3. **肤色随机**：异种型基因表里放胆绿作默认；生成时（PawnGenerator 后缀）按人物编号播种的随机数在四种里重新选一种（不消耗全局随机数流）。分裂出来的婴儿同样随机。
4. **体型**：没放体型基因；生成时成年的壮硕/肥胖体型改成瘦削或标准（各一半）。
5. **吞没算吃人肉**：开着意识形态时记原版 `AteHumanMeat` 和 `AteHumanMeatDirect` 两个历史事件（1.7 里的 defName 已核对）；没开意识形态时，原版的戒律不起作用，改为直接给原版“吃了人肉”的心情（食人者给食人者版本）。
6. **寒冷 ×1.5**：冻伤以及名字里带 Frost / Cold / Freez 的伤害按 ×1.5 结算；冬狼的寒霜迟缓、寒霜法杖的寒霜缠身、寒冰魔的寒气侵体本身不是伤害而是减速状态，改成**持续时间 ×1.5**。
7. **火焰 ×1.5** 同时作用于 Flame 和 Burn（烧伤）。
8. **目睹吞没**只给殖民者（需求原文就是“旁观殖民者”），最多叠 3 次（每次 ×0.75）。
9. **渗过**：除了点门另一侧的空地，也可以直接点门本身（落到门另一侧离直线最近的空格）；中间只能有一扇门；不能渗进没探索过（有迷雾）的地方。
10. **骷髅类死灵造物**：查了造物来源——本模组的骷髅、巨像、知识死灵都是用生肉胚体在仪式台培育的，不用尸体；流浆人尸体屠宰只出凝胶、没有肉，所以天然做不成造物，没加代码。VPE 的“复生术”是复活不是造物，没有拦。
11. **分裂婴儿不给背景特性**：生成时就去掉，另外把婴儿编号记在存档组件里，之后长大时的成长时刻也不会抽到背景特性（TraitSet.GainTrait 前缀）。
12. **婴儿的意识形态**：原版不允许给新生儿指定意识形态（会报红字），所以没跟父体，照原版规则处理。
13. **能力图标 128px**（按需求）；1.24.0 的能力图标其实是 256px，游戏里显示没区别。
14. **原生质凝胶易燃性 0**（完整套检查到“可燃但没有耐久会一直烧”）。
15. **越狱开门**：只在原版判定“正在越狱”时生效，任何本来打不开的门（玩家的门、上锁的门）都算。
16. **剧本追捕队**：用自定义组件 ScenPart_OozeEscape，排进 1.24.0 的同一个限时袭击队列（ScheduledRaid）；为了“固定 300 点”给 ScheduledRaid 加了一个 fixedPoints 字段（默认 0 时行为不变）。点数不会低于塞伊远征军能组队的下限。
17. **黑网**：凝胶 -3～6（有时没有）。
18. **稳形护带**：饰品层 LP_Accessory，部位腰部（和戒指、护符不冲突）；配方由“DND炼金术”解锁（遵守“DND 内容只挂本页签研究”的约定）；贴图受皮革颜色着色。
19. **吃生食不难受**挂在原版 ThoughtDef.nullifyingGenes 上（AteRawFood）。
20. **重塑**没有可修的地方时按钮直接灰掉（“没有缺失的外部部位，也没有永久伤疤”）。
21. 吞没的能力冷却 1 秒、引导 83.33 秒（= 5000 刻），用原版 CastAbilityOnThingMelee 走过去再引导；右键菜单里排的也是这个能力的任务，所以按钮和右键完全一样。

## 可调默认值

- 设置左栏“流浆人分裂冷却”：15～60 天，默认 30。
- 代码常量（Source/Oozefolk.cs 顶部 `Oozefolk` 类）：原生质上限 100、开局 30、每顿 +3、护带 +20；吞没 人形 +25 / 饱食 50%、动物 体型×15（≤30）/ 体型×30%（≤60%）、腐烂 ×0.5；分裂 80 + 饱食 25%；渗过 10、距离 3；重塑 40；析出凝胶 25；酸蚀 ×0.25、出血 ×0.5、寒冷 ×1.5、操作下限 60%、越狱 ×2、伪足酸蚀对衣服 ×2；凝滞阈值 0°C / 护带 -15°C、持续 2500 刻；屠宰凝胶 3；目睹距离 15。
- XML（Defs/Oozefolk.xml）：能力冷却（渗过 1 小时、重塑 2 天）和引导时间、伪足 9+4 / 2 秒、凝滞 ×0.75 / -10%、分裂虚弱 3 天、目睹 -3 / 1 天、物品市值和配方、兵种 55 点和 1.5 权重（Defs/Factions.xml）。

## 改动的文件

新增：
- Defs/Oozefolk.xml（异种型、基因、伤病、能力、心情、伤害、物品、配方、兵种、剧本组件、剧本）
- Source/Oozefolk.cs（全部逻辑、Harmony 补丁、存档组件、设置）
- Source/OozefolkChecks.cs（-Only Oozefolk）
- ArtSource/RaceArt/draw_oozefolk.py；Textures/Oozefolk/（异种型图标 1、基因图标 9 张 256px、能力图标 5 张 128px、流光三向 128px、物品 3 张 128px，共 21 张）
- ArtSource/Oozefolk-1.38.0-spec.md、Oozefolk-1.38.0-preview.png、本报告

**共享文件**（都在 backup-1.37.0 里有原件）：
- Source/Core.cs：设置的 ExposeData 和设置页各加一行调用（ExposeOozefolk、OozefolkSettings.Draw）。
- Source/MindFlayerTadpole.cs：Tadpoles.Immunity 在火巨人那一行后面加流浆人。
- Source/Scenarios.cs：ScheduledRaid 加 fixedPoints（存档字段 fixedPoints，默认 0 不变）。
- Source/FactionChecks.cs：同盟种族比例检查里把 LP_FC_OozeWanderer 和 1.24.0 的兵种一起单独统计。
- Defs/Factions.xml：费伦拓荒同盟 Combat、Peaceful 组加 LP_FC_OozeWanderer 1.5。
- Defs/FactionTraders.xml：黑网商队加原生质凝胶。
- About/About.xml（1.38.0 + 描述）、使用说明.txt（内容一览一行、剧本列表一行、第二部分【1.38.0 流浆人】、更新日志）。
- 重新编译的 1.7/Assemblies/LichPhylactery.dll 和三个桥接 dll（桥接源码没动）。

对原版方法加的 Harmony 补丁（都只在对象是流浆人/有对应基因时起作用）：Pawn.PreApplyDamage（酸、冷）、Thing.TakeDamage（伪足酸蚀对衣服）、HediffSet.CalculateBleedRate、HediffComp_Infecter.CheckMakeInfection、HediffComp_GetsPermanent.IsPermanent（setter）、PawnCapacityWorker_Manipulation.CalculateCapacityLevel、PrisonBreakUtility.InitiatePrisonBreakMtbDays、Building_Door.PawnCanOpen、Pawn.ButcherProducts、Pawn_GeneTracker.Notify_GenesChanged（清种族缓存）、PawnGenerator.GeneratePawn（肤色、体型）、Ability.StartCooldown（分裂冷却）、TraitSet.GainTrait（分裂婴儿）；本模组自己的 Items.Give、Fiends.Chill（寒冷状态时长）。没有动 VPE 桥接（BridgeChecks 的 8 条路径 / 72 个能力不变），没有加研究。

## 没有自动测、请在游戏里看

- 右键尸体的“吞没××（2小时）”菜单、走过去引导 2 小时、中途征召能打断；能力按钮在原生质不够时灰掉并写“原生质不足（需要 X）”。
- 原生质资源条的样子（绿色，标题“原生质”，提示文字）。
- 流光头饰在四种肤色、三个朝向上的效果（预览图是脚本拼的近似头形，不是游戏截图）；体型、光头。
- 剧本“实验池逃亡”开局（物资、护带、第 6 天“实验池的追捕者”来信）。
- 同盟袭击/访客里的流浆人游民、黑网卖凝胶、炼金桌三个凝胶配方、缝纫台的稳形护带。
- 囚犯越狱时流浆人能开门。
