# DND边缘大陆：信仰内容实现要求（版本 1.39.0）

依据与边界：
- 沿用 1.28.0 黄昏之年的 LP_DeityDef（8 位神）、LP_DivineInfusion（神力灌注）、LP_DivineVessel（神明附体）、LP_MiracleShard（神迹残片）、LP_Radiant（神圣伤害）。
- 神眷值 + 圣坛 + 守护神框架，但不做会走路的神祇化身。
- 信徒 LP_Devotee 按神分 degree + 无神者。
- 种族：阿斯莫、提夫林。
- 成神不在本文：另一个线程在本文之上做半神 LP_Demigod、神格、神性火花、成神任务链。第八节是给它的接口，名字定死。
- 圣武士职业在职业线程做；本文的"神术"不进 VPE 路径。

## 一、总览
| 框架 | 神祇扩到 11 位；每位神一个神眷值（-100～100，殖民地级）；守护神在圣坛上选 |
| 道具 | 圣坛、圣徽、圣水、9 件神赐遗物 |
| 特性 | 信徒（11 个 degree，一位神一个）、无神者 |
| 法术 | 神术 12 个：3 个通用 + 9 位可信奉神各 1 个领域神术；消耗神眷值，不需要 VPE |
| 任务 | 朝圣者、神谕试炼、寻回圣物、亵渎者 4 个；另有季度显圣（赐福/降罚） |
| 种族 | 阿斯莫、提夫林两个可玩异种型；神选仪式可把信徒转成阿斯莫或提夫林 |
| 研究 | DND边缘大陆研究页加 2 项：信仰与神术、天界与地狱血统 |
| 设置 | 左栏新分组"信仰"：信仰系统开关、神眷获取倍率 50%～200%、信仰任务频率 0%～200%、季度显圣开关 |

## 二、神祇与神眷值
### 2.1 神祇
LP_DeityDef 加字段（老 8 位补上）：alignment（Good/Neutral/Evil）、worshippable、enemies（守护神是 A 时 A 的敌人神眷每天 -0.2）、domainAbility、relic、likedOfferings（ThingDef/ThingCategoryDef 列表，献祭 ×3）、blessing/curse（worker 类名）、iconPath、color。
新增 3 位神（只加 Def，不进黄昏之年灌注列表）：凯兰沃、密斯特拉、莎尔。
| 洛山达 LP_Deity_Lathander 善 可信奉 敌对莎尔 |
| 提尔 LP_Deity_Tyr 善 可 敌对班恩 |
| 密斯特拉 LP_Deity_Mystra 善 可 敌对莎尔 |
| 凯兰沃 LP_Deity_Kelemvor 中 可 （巫妖，见下） |
| 马拉尔 LP_Deity_Malar 恶 可 敌对洛山达 |
| 塔洛斯 LP_Deity_Talos 恶 可 敌对洛山达 |
| 班恩 LP_Deity_Bane 恶 可 敌对提尔 |
| 莎尔 LP_Deity_Shar 恶 可 敌对密斯特拉、洛山达 |
| 苏尔特 LP_Deity_Surtur 恶 可（火巨人、余烬氏族的神） 无 |
| 伊尔斯维 LP_Deity_Ilsensine 恶 否（只作敌方灌注） |
| 维拉基斯 LP_Deity_Vlaakith 恶 否 |
老 8 位神 defName 以设备上 1.28.0 实际名字为准，保留设备名字并写进报告。

### 2.2 神眷值
- WorldComponent_Faith（存档）：Dictionary<LP_DeityDef,float> favor，-100～100，开局 0；patron（可空）；patronChangeCooldownUntilTick；每位神季度显圣计时；任务冷却。
- 获取（全部 × 神眷获取倍率）：在圣坛祈祷（新工作 1 小时）守护神 +0.5/人次，信徒 ×2，每天全殖民地上限 +5；献祭：每 100 银市值 +1，喜欢的祭品 ×3，神迹残片 +10；各神作为（下表）；任务（第五节）；每天回落：非守护神每天向 0 回落 0.5，守护神不回落。
各神作为（只对守护神和敌对神结算）：
| 洛山达 | 加：收获作物 +0.05/次；击杀不死者 +2 | 减：每个死灵造物每天 -0.2；巫妖复生 -10 |
| 提尔 | 释放囚犯 +3；（Ideology）审判仪式 +3 | 摘取器官 -5；处决囚犯 -3 |
| 密斯特拉 | 施放法师/牧师路径或神术 +0.1/次；炼金桌产物 +0.5 | 摧毁魔法物品 -1 |
| 凯兰沃 | 尸体入葬 +1；击杀敌方不死者 +2 | 巫妖复生 -15；巨魔复活 -5；汲取灵魂 -3；殖民地有巫妖每天 -0.5 |
| 马拉尔 | 狩猎击杀 +0.5；殖民者近战击杀动物 +1 | 驯服动物 -1 |
| 塔洛斯 | 雷击/火灾发生在地图上 +1；雷暴中祈祷 ×2 | 殖民者扑灭火 -0.1 |
| 班恩 | 奴役囚犯 +3；（Ideology）镇压奴隶 +0.2 | 释放奴隶 -3 |
| 莎尔 | 夜里（22:00～5:00）祈祷 ×2；殖民者失去亲人未崩溃 +2 | 银焰/神圣伤害击杀敌人 -0.5（只对莎尔是守护神时） |
| 苏尔特 | 锻造台/熔炉产出武器或护甲 +0.5 | 殖民者被冻伤 -1 |
（钩子尽量挂已有 Harmony 点：RecordsUtility.Notify_*、Pawn.Kill、配方完成、巫妖/巨魔复活的现有方法。写不进去就删那条，报告说明。）
- 阈值（守护神）：≥20 可施放领域神术；≥50 季度赐福；≥80 可做神选仪式；≤-30 季度降罚。任何神 ≤-50 时，该神信徒殖民者心情 -6"神弃"。

### 2.3 守护神
- 圣坛按钮"选择守护神"，列出 worshippable 神（图标 + 当前神眷）。
- 一个殖民地一位。更换：旧神 -30，15 天冷却。没有圣坛守护神保留但不能祈祷献祭。
- 不解析原版意识形态神名。
- 派系固定信仰：费伦拓荒同盟 → 洛山达/提尔/密斯特拉随机；塞伊 → 班恩；余烬氏族 → 苏尔特；猎巫狂热分子 → 提尔；原版海盗 → 塔洛斯；部落 → 马拉尔；其他无。

### 2.4 季度显圣
每 15 天对守护神结算（设置可关）：≥50 → 赐福 3 天；≤-30 → 降罚 1 天。信件 + 地图光色。
| 洛山达 | 赐福：作物生长 +25%，殖民者 +4 心情"黎明的眷顾" | 降罚：作物生长 -25% |
| 提尔 | 囚犯抗拒下降 ×1.5 | 罪行最多的殖民者 -8"良心不安" |
| 密斯特拉 | 心灵敏感度 +10%，精神力回复 +20% | 随机一名施法者精神力清空 |
| 凯兰沃 | 看到尸体无负面心情；死亡时 25% 改为倒地（每次显圣最多 1 次） | 地图上 2～3 具尸体消失 |
| 马拉尔 | 狩猎命中 +15%，驯兽成功 ×1.5 | 2 只野兽发狂 |
| 塔洛斯 | 雷暴期间殖民地建筑不会被雷击 | 立即一场雷暴，落雷优先打殖民地附近 |
| 班恩 | 奴隶压制下降 ×0.5 | 一名奴隶开始反抗 |
| 莎尔 | 黑暗中无"在黑暗中"心情，夜间工作速度 +10% | 一名殖民者失去一段非血缘关系 |
| 苏尔特 | 锻造速度 +25%，熔炉燃料消耗 ×0.5 | 一处殖民地建筑起火 |

## 三、道具
研究 信仰与神术 LP_Research_Faith（费用 800，无前置）解锁圣坛、圣徽、圣水。
### 3.1 圣坛 LP_Altar（类 Building_Altar）
- 2×1，可选材料（木/石/金属 60 + 银 50），美观 +8。
- 按钮：选择守护神；显示所有可信奉神神眷（悬浮列阈值）。
- 祈祷：JobDef LP_Pray，在圣坛前 1 小时，满足娱乐（原版冥想的 joy 类型），+3 心情"祈祷过"1 天；娱乐里自动出现，也可右键命令。无神者不祈祷。
- 献祭配方：LP_Offer_Silver（银 100）、LP_Offer_Shard（神迹残片 1）、LP_Offer_Liked（守护神喜欢的祭品，按 likedOfferings 动态 ingredientFilter，60 市值一份）。无产出，只加神眷。
- 发光颜色 = 守护神颜色。成神线程的仪式地点。可多座。
### 3.2 圣徽 LP_HolySymbol
- 饰品层 LP_Accessory，圣坛制作：银 30 + 金 5，工作量 1500。
- 一个 ThingDef；显示的神 = 殖民地守护神（无则"沉寂"，不给神术）。
- 佩戴获得 3 个通用神术 + 守护神领域神术；神恩强度 +0.1。无神者戴无效。
### 3.3 圣水 LP_HolyWater
- 圣坛制作：草药 1 + 银 10，每瓶消耗守护神神眷 0.5。叠 10。
- 投掷物（照 1.22.0）：半径 1.9 内 12 点 LP_Radiant，灭火，巨魔再生停 1 小时（效果减半）。
- 可饮用：一个感染/瘟疫类疾病 -10% 严重度，+2 心情。
### 3.4 神赐遗物（9 件）
不可制作。来源：神谕试炼/寻回圣物奖励；守护神神眷第一次到 80 时赐一件（一神一次）。市值 2500，传奇品质。
| 洛山达 晨星 LP_Relic_MorningStar 钉头锤：对不死者 ×2，命中 10% 点燃；6～9 点全工作速度 +5% |
| 提尔 提尔之手 LP_Relic_HandOfTyr 护手：社交冲突 -50%；对人形击杀数 ≥10 的敌人近战 +15% |
| 密斯特拉 银焰之泪 LP_Relic_SilverTear 饰品：心灵敏感度 +20%，神术与法术冷却 ×0.8 |
| 凯兰沃 灰烬天平 LP_Relic_AshScales 饰品：战斗致死时尸体"申诉"30 天：复生术/复生卷轴必成且无复活精神病；巫妖无效 |
| 马拉尔 野兽之爪 LP_Relic_BeastClaw 护手：近战 +20%，流血 ×1.5；驯兽 +4 |
| 塔洛斯 风暴之矛 LP_Relic_StormSpear 长矛：命中 20% 落雷（25 伤害，半径 1.5，不打持有者） |
| 班恩 黑手护手 LP_Relic_BlackHand 护手：近战命中 25% 让敌人逃跑 5 秒（心智抗性抵抗）；奴隶压制下降 ×0.5 |
| 莎尔 暗夜之泪 LP_Relic_NightTear 饰品：免疫失去亲人心情；黑暗中闪避 +15% |
| 苏尔特 熔炉之心 LP_Relic_ForgeHeart 饰品：火焰伤害 ×0.5；锻造速度 +30%；近战附带 5 点烧伤 |

## 四、神术（12 个）
### 4.1 规则
- 原版 RimWorld.Ability（AbilityDef），不依赖 VPE，不进 VPE 路径。
- 来源：佩戴圣徽；摘下移除（Apparel.Notify_Equipped/Unequipped 后缀）。
- 消耗：守护神神眷（殖民地共用）。信徒（信守护神）×0.5；神选者 ×0.5（可叠到 ×0.25）。不够时灰掉写原因。
- 倍率 P = 神恩强度 × 心灵敏感度（下限 0.2，不设上限），同 1.30.1 牧师：有 VPE 用 LP_DivinePower 统计值；无 VPE 用 0.5 + 社交/20。圣徽 +0.1、神选者 +0.25 加在神恩强度上。复用 1.30.1 牧师的倍率辅助方法。
- Isekai：伤害用 DamageInfo，instigator = 施法者、不填武器（同 1.30.1 牧师）；治疗量护盾量 × P，再乘牧师治疗已用的 Isekai 系数（若有）。
- 数值为 P=1 时。
### 4.2 通用
| 神圣庇护 LP_Miracle_Sanctuary 神眷 3 冷却 1 天：一名友军护盾吸收 40×P，12 小时 |
| 驱逐邪祟 LP_Miracle_Banish 4，6 小时：单体 25×P LP_Radiant（原规则不死者/邪魔 ×2）；对神明附体者再 ×1.5；对神力灌注者 30%×P 移除灌注特性 |
| 圣化之地 LP_Miracle_Consecrate 6，2 天：半径 5 区域 1 天，敌方不死者/邪魔每秒 2×P 神圣伤害，巫妖/巨魔复活计时区内暂停，友军心情 +3 |
### 4.3 领域（守护神神眷 ≥20）
| 洛山达 黎明之光 LP_Miracle_Dawn 10，3 天：半径 6 友军止血、每处伤口 -10×P 严重度；作物 +20% 生长 |
| 提尔 真言审判 LP_Miracle_Judgment 10，2 天：对敌伤害 = (10 + 人形击杀数/10×5，上限 60)×P；对囚犯抗拒 -20×P、意志 -10×P |
| 密斯特拉 魔网涌流 LP_Miracle_Weave 12，3 天：一名友军能力冷却清零，精神力 +30%（无 VPE 只清冷却） |
| 凯兰沃 安息 LP_Miracle_Rest 8，2 天：半径 8 敌方不死者 30×P 神圣伤害；人形尸体"已安息"（不能汲取灵魂、复生、做死灵造物胚体）。巫妖施放自身受 20 伤害 |
| 马拉尔 狩猎之怒 LP_Miracle_Hunt 8，2 天：半径 10 殖民地动物近战 +50%×P、移速 +20%，12 小时 |
| 塔洛斯 召雷 LP_Miracle_Thunder 12，2 天：目标区域 3 道落雷，每道 25×P（instigator = 施法者），随机偏 2 格 |
| 班恩 暴君之令 LP_Miracle_Dread 10，2 天：半径 6 敌人逃跑 10 秒（LP_MindResist 抵抗）；巨躯/神明附体免疫 |
| 莎尔 暗影帷幕 LP_Miracle_Veil 10，3 天：半径 5 友军隐形 20×P 秒（原版心灵隐身），攻击即显形 |
| 苏尔特 熔炉之怒 LP_Miracle_ForgeWrath 10，2 天：锥形 6 格 25×P 烧伤并点燃；火巨人 ×1.25 |

## 五、任务
IncidentDef（照 1.28.0/1.31.0 写法），受"DND 事件频率"总滑条和"信仰任务频率"共同调节；只有建了圣坛并选了守护神才触发。
| 朝圣者 LP_Faith_Pilgrims：每 20～40 天。3～5 名信同一神（随机可信奉神，50% 是守护神）的访客来，请求在圣坛祈祷 1 天（访客 lord + LP_Pray）。信件：接待/拒绝。接待完成该神 +8（守护神 +12），20% 一名请求加入；拒绝 -5；圣坛被毁或朝圣者被打 -15 并离开 |
| 神谕试炼 LP_Faith_Trial：守护神 ≥30，每 30～60 天。10 天内试炼按神：洛山达 收获 200 份作物；提尔 释放 1 囚犯或审判；密斯特拉 施放 10 次神术/法术；凯兰沃 入葬 3 具尸体；马拉尔 狩猎 5 只；塔洛斯 熬过一场雷暴（直接降一场）；班恩 奴役 1 囚犯；莎尔 夜里祈祷 5 次；苏尔特 锻造 3 件武器或护甲。成功 +15，30% 遗物（已有则 2 神迹残片）；失败 -10 |
| 寻回圣物 LP_Faith_LostRelic：守护神 ≥40，每 60～90 天。原版物品藏匿点类任务，敌对神对应派系守卫（无则海盗），据点放该神遗物，15 天期限。带回放圣坛（右键"供奉"）+20，可留用；过期 -5 |
| 亵渎者 LP_Faith_Desecrators：守护神有敌对神且其派系敌对，每 30～60 天。小袭击（点数 ×0.6），领队必带敌对神神力灌注，优先打圣坛。圣坛被毁 -20；打退 +10，掉 1 神迹残片 |
天界之子（出生钩子）：守护神善神且 ≥60，新生儿 10% 阿斯莫；恶神 ≥60，10% 提夫林。

## 六、神选仪式
- 圣坛按钮：守护神 ≥80，选一名信守护神的信徒；消耗神眷 50 + 神迹残片 3；仪式 2 小时，可打断（不扣资源）。
- 结果：HediffDef LP_Chosen（类 Hediff_Chosen，字段 deity，标签"神选者（洛山达）"），永久：神恩强度 +0.25、神术消耗 ×0.5、心智抗性 +20%、守护神颜色微光。一人一个；同一神每殖民地最多 3 名。
- 种族转换（确认窗口可选，默认勾选）：善神 → 异种基因换成阿斯莫；恶神 → 提夫林；凯兰沃不转。只替换 xenogenes 并设异种型，内生基因保留；巫妖、夺心魔、巨灵、泰坦、霜/火巨人、LP_ArcaneDemigod 不能转（灰掉写原因，后者仍可得 LP_Chosen）。

## 七、种族
研究 天界与地狱血统 LP_Research_Planetouched（费用 1200，前置 信仰与神术），只用于基因相关配方；派系生成不需要。外观照 1.24.0：人类头 + 原版附件 + 肤色；新画 1 张可染色阿斯莫光环附件。
### 7.1 阿斯莫 LP_Aasimar
- 原版基因：Beauty_Pretty、AptitudeStrong_Social、AptitudeStrong_Medicine、DarkVision；浅金/银肤，金/银发。
- 天界抗性 LP_Gene_CelestialResistance：LP_Radiant 与死灵类伤害 ×0.5（巫妖死灵冲击、汲取灵魂、恐惧凝视；按设备实际 Def）。
- 治疗之手 LP_Gene_HealingHands：主动，触摸友军每处伤口 -（等级/2 + 社交/2），冷却 1 天，不长肢体。
- 天界启示三选一（exclusionTags LP_AasimarRevelation）：光辉之魂（一次跳跃，原版长跳 Verb，冷却 1 天）、光辉消耗（开关光环：半径 2 敌人每秒 2 神圣伤害，自己每 10 秒 1 点烧伤）、亡者裹布（半径 4 活物敌人逃跑 8 秒）。
- 天界光环 LP_Gene_Halo：头顶光环附件，无数值。
- 对巫妖、死灵造物伤害 +25%；看到巫妖 -5、死灵造物 -4；巫妖看到阿斯莫 -5。代谢 -1，可遗传。
- 生成：费伦拓荒同盟 8%（一半带信徒），猎巫狂热分子 5%。
### 7.2 提夫林 LP_Tiefling
- 原版基因：Headbone_Horns（或 MiniHorns）、Tail_Smooth、DarkVision、MaxTemp_LargeIncrease、AptitudeStrong_Social、Beauty_Pretty；红/紫肤。
- 地狱抗性 LP_Gene_InfernalResistance：火焰/烧伤 ×0.5。
- 地狱叱喝 LP_Gene_HellishRebuke：被近战命中 25% 反击 10 烧伤（instigator=提夫林，冷却 10 秒）。
- 魔鬼契约 LP_Gene_InfernalPact：主动，-10 心情"与魔鬼讨价还价"3 天，换 24 小时射击与近战伤害 +20%、精神力获取 +20%；之后 12 小时"契约透支"（意识 -10%）。冷却 5 天。
- 魔鬼血统 LP_Gene_FiendishBlood：非提夫林殖民者初见 -2"异类"（住满 1 年消失）；对阿斯莫 -3，住满 1 年变 +3"宿命之友"。
- 代谢 0，可遗传。生成：塞伊远征军 10%（红袍里），黑网商人 15%。
### 7.3 巨躯规则、Isekai 宝箱、VPE 职业照常识别。阿斯莫神圣伤害对巨魔像火一样阻断再生，减半（复用圣水接口）。

## 八、给成神线程的接口（名字定死）
- LichPhylactery.LP_DeityDef；LichPhylactery.WorldComponent_Faith。
- public static class FaithUtility：float GetFavor(LP_DeityDef god)；void AddFavor(LP_DeityDef god, float amount, string reason)（夹 -100～100，reason 进日志和悬浮历史）；LP_DeityDef Patron { get; }；void SetPatron(LP_DeityDef god, bool ignoreCooldown = false)；bool IsDevoteeOf(Pawn p, LP_DeityDef god)；LP_DeityDef DevoteeGod(Pawn p)；bool IsAtheist(Pawn p)；bool IsChosen(Pawn p, out LP_DeityDef god)；IEnumerable<Building> Altars(Map map)；event Action<LP_DeityDef,float,string> FavorChanged；LP_DeityDef DegreeToDeity(int)；int DeityToDegree(LP_DeityDef)；float MiracleCost(Pawn, AbilityDef)（可被后缀修改）。
- 圣坛 ThingDef LP_Altar，类 LichPhylactery.Building_Altar；静态 Building_Altar.ExtraGizmos 列表供外部注册按钮。
- TraitDef LP_Devotee degree 1～11 按 2.1 表顺序（洛山达=1 … 维拉基斯=11）；TraitDef LP_Atheist；HediffDef LP_Chosen（Hediff_Chosen.deity）。

## 九、特性
- 信徒 LP_Devotee（11 degree）：祈祷神眷 ×2；神术消耗 ×0.5（信守护神时）；守护神是本神 +4"侍奉我的神"，是本神敌对神 -6"侍奉敌神"；神恩强度 +0.15；本神 ≤-50 时 -6"神弃"。小被动：洛山达 种植 +10%；提尔 社交冲突 -25%；密斯特拉 心灵敏感度 +10%；凯兰沃 手术成功 +5%、看到尸体无心情；马拉尔 狩猎命中 +10%；塔洛斯 雷暴里 +3 心情；班恩 镇压 +20%；莎尔 黑暗无心情惩罚；苏尔特 锻造 +10%。degree 10/11 只出现在敌人。
- 无神者 LP_Atheist：不受信仰心情；不能祈祷、不能用圣徽；受神术效果 ×0.5；心智抗性 +10%。
- 随机：信徒权重 0.3（与 1.32.0 背景特性各算各的，不受一人一个背景特性限制）；无神者 0.1；两者互斥。派系固定信仰成员 25% 带对应信徒。与神力灌注不冲突。关闭信仰系统后不再随机出现，已有保留。

## 十、贴图
ArtSource/RaceArt/draw_faith.py：圣坛（2×1 南/北/东）、圣徽、圣水、9 遗物、12 神术图标、阿斯莫光环、11 神小图标（64px）。约 36 张。

## 十一、检查（FaithChecks.cs，-Only Faith，10～15 条）
挑最关键：神祇 Def 完整；AddFavor 夹值 + FavorChanged + 存读档；选/换守护神；祈祷加神眷；圣徽增删神术；P 倍率与信徒半价、神眷不足不能放；驱逐邪祟对骷髅 ×2；召雷 instigator；圣水；季度显圣一例；朝圣者事件生成；神选仪式 + 转阿斯莫保留内生基因、巫妖不能转；阿斯莫/提夫林基因与遗传；研究在 LP_FireGiantTech；设置关闭不报错。

## 十二、交付
- 新 Defs：Faith.xml、Faith_Miracles.xml、Faith_Incidents.xml、Faith_Races.xml、Patches/Faith.xml。研究写进 Defs/Research.xml（tab LP_FireGiantTech，X 步长 1 / Y 0.7，前置在左；信仰与神术在左，天界与地狱血统右一格）。新 Source：Faith.cs、FaithMiracles.cs、FaithIncidents.cs、FaithRaces.cs、FaithChecks.cs。
- 与 1.37.0 共存：订阅 NetherilAscension.ArcaneDemigodGranted → 密斯特拉 -20；LP_WeaveCollapse 开始时 -10。
- 设置写进 Core.cs 左栏"信仰"分组。
