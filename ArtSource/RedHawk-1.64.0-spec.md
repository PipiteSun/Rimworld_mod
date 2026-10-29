# 红鹰商业联盟 实现要求

> 2026-10-28 写，对照设备 1.62.0 的 Defs（只读查阅）。版本 **1.64.0**（协调员已定，排在清理版 1.63.0 和传奇抗性热修之后；文中 X.Y.0 = 1.64.0）。
> Peter 原话：「给游戏内加入派系。红鹰商业联盟。他有以下商队：1. 奴隶商队，会出售十几个不同种族的奴隶。2. 魔法材料商队，会出售不同的 dnd 魔法材料。3. 契约卷轴，会出售不同的异界契约物品。」
> 所有设计决定由本线程做出（Peter 规则：不要问他），实现后写进报告的「决定」一节。

## 一、派系本体

| 项 | 值 |
|---|---|
| defName / 名称 | `LP_RedHawkLeague` / 红鹰商业联盟（fixedName） |
| 父类 | `LP_FactionBase`（Defs/Factions.xml 已有） |
| 科技 | Medieval |
| 成员称呼 | 红鹰商人 / 红鹰商人；首领头衔「总商」 |
| 初始好感 | +10（中立偏友好），不是 permanentEnemy、不是 naturalEnemy |
| 据点 | 有（settlementGenerationWeight 1，requiredCountAtGameStart 1，和其他 LP 派系一样） |
| 颜色 | 暗红 + 金：(0.62,0.12,0.10) ~ (0.80,0.22,0.14) |
| 图标 | `Factions/RedHawkLeague`：展翅红鹰叼一枚金币，RimWorld 风格（手绘、粗描边、平涂 + 阴影、低饱和） |
| 设置开关 | `FactionExtension.toggle = redhawk`，放进**现有派系设置分组**（不新增设置分组，AdventurerChecks 的 19 组计数不变）；旧存档用已有的「添加派系」按钮补上 |
| 意识形态 | fixedIdeo，名「金与契」，说明「一切交易都要签字，一切签字都要兑现。」；forcedMemes（MayRequire Ideology）`Structure_Ideological` + `Individualist`；`disallowedPrecepts` 排除 `Slavery_Abhorrent`、`Slavery_Disapproved`（奴隶贩子不能反奴隶制）。给 deityPresets 避免中文神名随机报错（1.20.0 的教训）：「瓦奥克 · 财富之神」 |

**描述**：
> 横跨费伦各大港口的商业联盟，旗帜上是一只叼着金币的红鹰。红鹰不问来历、只认契约：奴隶、龙鳞、秘银、深渊的灵魂币，乃至异界宗主亲笔签下的契约书，只要价钱合适都能买到。
> 联盟对巫妖、夺心魔或恶魔契约者都没有偏见——在他们眼里，这些只是更大的主顾。

**成员种族**（xenotypeChances，其余为人类）：半精灵 0.12、提夫林 0.10、轻足半身人 0.08、山地矮人 0.06、岩侏儒 0.05、半兽人 0.06、大地精 0.04、吉斯洋基 0.02。

**好感**：不挂种族好感 situation（不在乎巫妖/夺心魔）。和其他派系的关系照原版随机（不强制敌对）。买他们的奴隶**不**影响费伦拓荒同盟的好感（决定：避免新增跨派系机制）。

## 二、PawnKind（前缀 `LP_RH_`）

全部 `canGeneratePawnRelations` 照原版商人；生成奴隶时必须 false（1.11.0 的配偶名 NRE 教训）。服装红金配色（apparelColor）。

| defName | 名称 | 用途 | 要点 |
|---|---|---|---|
| LP_RH_Merchant | 红鹰商人 | 通用商人 / 据点 | 人类或成员种族，皮甲 + 短剑 |
| LP_RH_Slaver | 奴隶贩子 | 奴隶商队商人 | 半兽人或人类，皮鞭（原版无则短棍），携带 **制伏短棍**（1.61.0 `制伏短棍` 已有，按真实 defName） |
| LP_RH_Appraiser | 材料鉴定师 | 魔法材料商队商人 | 岩侏儒或矮人，法袍 |
| LP_RH_PactBroker | 契约掮客 | 契约卷轴商队商人 | 固定提夫林，长袍 + 手杖 |
| LP_RH_Mercenary | 红鹰佣兵 | 护卫 / 袭击 | 链甲 + 长剑/斧，技能中等 |
| LP_RH_Crossbowman | 红鹰弩手 | 护卫 / 袭击 | 弓/弩（Medieval 远程） |
| LP_RH_Gladiator | 角斗士 | 护卫 / 袭击 | 食人魔 / 牛头人 / 半兽人随机，重武器；少量（commonality 低） |
| LP_RH_Leader | 红鹰总商 | 首领 | 人类或半精灵，高社交，好装备 |

pawnGroupMakers：
- Combat（只在敌对时出现，commonality 正常但 `raidCommonalityFromPointsCurve` 为 0.5——商人不爱打仗）：佣兵 10、弩手 8、角斗士 2。
- Peaceful：商人 10、佣兵 6、弩手 4。
- Trader ×3：见下节，每种商队一个 Trader 组，商人 = 对应商人 kind，护卫 佣兵 4~6 + 弩手 2~4（+ 奴隶商队再加角斗士 1），驮兽 Muffalo/Dromedary/Alpaca。
- Settlement：商人 6、佣兵 10、弩手 8、角斗士 2。

## 三、三种商队（`caravanTraderKinds`）

每个 TraderKindDef 都挂现有的 `LichPhylactery.TraderGroupExtension`（traderKind 指向对应商人 PawnKind，照费伦拓荒同盟写法），让对应商队配对应商人。

### 1. 奴隶商队 `LP_Caravan_RHSlaver`（label：红鹰奴隶商队，commonality 1）

- 白银 1000~1600。
- **新 StockGenerator `LichPhylactery.StockGenerator_RHRaceSlaves`**：一次生成 **10~14 名奴隶，每人不同种族**（Peter：「十几个不同种族」）。种族池见下表，按权重无放回抽取。
  - 每条 entry：`xenotype`（defName 字符串）或 `pawnKind`（HAR 种族用）、`weight`、`tier`（普通/稀有）。
  - 字符串懒解析：解析不到的 defName 直接跳过，不报错（这样 HAR 桥未加载、或某个种族将来改名都不炸）。
  - 生成用一个通用 PawnKind `LP_RH_SlaveStock`（Human 种族，Slave 背景过滤，年龄 16~45，无武器，粗布衣）+ `PawnGenerationRequest.ForcedXenotype`；HAR entry 用它自己的 pawnKind。`canGeneratePawnRelations=false`，`forceNoIdeo` 不要（照原版奴隶）。
  - 15% 的奴隶是「精品」：额外随机 1 个 DND 背景特性（1.4x 的 8 背景特性池）或一项技能 +4；价格自然更高（原版市价）。
  - 价格：原版奴隶市价（种族基因自带的市价因子已经让稀有种族更贵），`price` 不额外设。
  - 至少 3 个稀有档（池里 tier=稀有）保证出现，其余随机，避免一整队都是地精。
- `StockGenerator_BuySlaves`（收奴隶）、`StockGenerator_BuyExpensiveSimple`、BuyTradeTag Art。
- 另卖：制伏短棍 1~2、缚网弩 0~1、安眠粉囊 3~6（1.61.0 制伏武器，按设备真实 defName），镣铐类原版无则不加。

**种族池**（基础 30 种，全部是设备上已有的 XenotypeDef）：

| 档 | 种族（defName） | 权重 |
|---|---|---|
| 普通 | 半兽人 LP_HalfOrc、高等精灵 LP_Elf、木精灵 LP_WoodElf、山地矮人 LP_Dwarf、丘陵矮人 LP_HillDwarf、轻足半身人 LP_HalflingLightfoot、敦实半身人 LP_HalflingStout、岩侏儒 LP_RockGnome、森林侏儒 LP_ForestGnome、半精灵 LP_HalfElf、狗头人 LP_Kobold、地精 LP_Goblin、大地精 LP_Hobgoblin、熊地精 LP_Bugbear | 各 3 |
| 稀有 | 卓尔精灵 LP_Drow、地侏 LP_DeepGnome、提夫林 LP_Tiefling、阿斯莫 LP_Aasimar、火/水/土/风元素裔 LP_GenasiFire/Water/Earth/Air、象人 LP_Loxodon、鸟人 LP_Aarakocra、食人魔 LP_Ogre、牛头人 LP_Minotaur、巨魔 LP_Troll、吉斯洋基 LP_Githyanki、流浆人 LP_Oozefolk、力能龙裔 LP_ForceDragon、萨特 LP_Satyr、林仙 LP_Dryad、调换儿 LP_Changeling | 各 1 |
| HAR 桥（仅 HAR 启用） | 独眼巨人（LP_Cyclops 的玩家/派系 pawnKind）、双头巨人（LP_Ettin）、六臂蛇人（LP_Marilith） | 各 0.5 |

**不进池**（决定）：巫妖、夺心魔/半夺心魔（只走黑网的灰色货源）、所有恶魔/魔鬼/天界生物（外来者倒地会被遣返，卖不了）、巨人四族/泰坦/巨灵/五色真龙（传奇级，不能当商品批发）。力能龙裔要先确认是可正常生成的普通 xenotype（若它带龙形态或特殊成长，生成成年体即可；若生成报错，从池里拿掉并写进报告）。巨魔奴隶照常可再生/复活，不用特殊处理。

HAR 条目放在 `Bridges/HAR/Patches/RedHawk_HAR.xml`，用 PatchOperationAdd 往 `LP_Caravan_RHSlaver`、`LP_Base_RedHawk`、`LP_Orbital_RedHawk` 的奴隶 generator `<entries>` 里加；没有 HAR 时这个文件不加载。

### 2. 魔法材料商队 `LP_Caravan_RHMaterials`（label：红鹰魔法材料商队，commonality 1）

- 白银 1000~1500。
- **新 StockGenerator `LichPhylactery.StockGenerator_RHDefList`**：给一串 `{def(字符串), countRange, chance}`，懒解析、缺了跳过；可选 `pickRange` 表示「从这组里随机挑 N 种」。三个商队都用它，以后加东西也只改 XML。
- 库存（defName 按设备 1.62.0 核对过）：

| 物品 | 数量 | 出现率 |
|---|---|---|
| 秘银 LP_Mithral | 40~100 | 100% |
| 精金 LP_Adamantine | 20~60 | 100% |
| 寒铁 LP_ColdIron | 60~150 | 100% |
| 熔岩结晶 LP_LavaCrystal | 15~40 | 100% |
| 龙鳞蜕 LP_DragonShed | 5~15 | 70% |
| 五色龙鳞（红/蓝/绿/黑/白 LP_DragonScale_*，**随机挑 2 色**） | 各 10~25 | 100% |
| 龙牙 LP_DragonFang | 3~8 | 50% |
| 巨魔血 LP_TrollBlood | 6~15 | 80% |
| 巨魔皮 LP_TrollHide | 30~80 | 60% |
| 冬狼皮 LP_Leather_WinterWolf | 30~80 | 50% |
| 天空琥珀 LP_SkyAmber | 2~5 | 50% |
| 雾瓶 LP_FogFlask | 3~8 | 50% |
| 四种元素符 LP_ElementalFocus_*（**随机挑 2 种**） | 各 1~2 | 60% |
| 恶魔血 LP_DemonBlood | 3~8 | 40% |
| 祝圣圣水 LP_BlessedWater | 4~10 | 60% |
| 迷锁碎片 LP_MythalShard | 5~15 | 30%（只是零头，不冲击迷锁地牢的商店） |
| 五色龙之血 LP_DragonBlood_*（随机 1 色） | 1 | 15% |

- 收购：上表全部（StockGenerator_BuySingleDef 或一个 BuyDefList 子类），外加 Art、BuyExpensiveSimple。
- 龙之心、神性火花、圣物、传承武器、神灯**不卖**（都是里程碑/传奇战利品，不该能买）。

### 3. 契约卷轴商队 `LP_Caravan_RHPact`（label：红鹰契约卷轴商队，commonality 0.8）

- 白银 1200~2000；商人固定 契约掮客（提夫林）。
- 库存：

| 物品 | 数量 | 出现率 |
|---|---|---|
| 契约书 LP_PactScroll | 2~4 张，**每张来自不同宗主** | 100% |
| 灵魂币 LP_SoulCoin | 15~40 | 100% |
| 真名卷 LP_TrueNameScroll | 1~2 | 60% |
| 恶魔学典籍 LP_Demonomicon | 1 | 50% |
| 位面锚链 LP_DimensionalShackles | 1~2 | 60% |
| 恶魔血 LP_DemonBlood | 4~10 | 70% |
| 缚绳 LP_EntanglingRope | 1~2 | 50% |
| 地狱钢叉 LP_InfernalPitchfork | 1 | 25% |
| 狂猎号角 LP_WildHuntHorn | 1 | 15% |
| 妖精银弓 LP_FeySilverBow | 1 | 15% |
| 天界羽披 LP_CelestialMantle | 1 | 10% |

- **契约书指定宗主**：设备上 `CompProperties_PactScroll` 已有，先读 Source 看宗主是生成时定还是阅读时定。
  - 若生成时随机定：商队生成后逐张改成互不相同的宗主（7 位：阿斯莫蒂尔斯/梅菲斯托菲勒斯/迪斯帕特/格拉西娅/提泰妮娅/风暗女王/巫婆集会）。
  - 若阅读时才定：给 comp 加一个可存档的 `presetPatron`，阅读时优先用它。
  - 交易界面要能看出是谁的契约：`TransformLabel` → 「契约书（梅菲斯托菲勒斯）」，描述里加一行宗主的阵营（九狱 / 妖精宫廷）。阅读、隐藏条款、偿契等流程**完全不改**。
- 收购：契约类物品、灵魂币、恶魔血（BuyTradeTag `LP_Pact` + 灵魂币/恶魔血单项）。
- **开关联动**：如果设置里「异界契约」被关掉，契约书和契约专属物品从库存剔除，换成多一份灵魂币和恶魔血；深渊也关掉时，这个商队不来（在派系 `caravanTraderKinds` 的选取里排除；实现方式自选——最小侵入的是对选商队的方法做 Harmony postfix，或在 TraderGroupExtension 上加 `requiresSetting`）。

## 四、据点交易和飞空商船

- **据点** `LP_Base_RedHawk`（红鹰商站，baseTraderKinds）：一次给齐三类，但每类量少一些：奴隶 6~8（不同种族，同一个 generator、同一个池）、材料表随机挑 8 种、契约书 1~2 + 灵魂币 10~25 + 其余契约物品随机 2 种；白银 2000~3500；收购同三商队之和。
- **飞空商船** `LP_Orbital_RedHawk`（红鹰飞空商船，`orbital=true`，`faction = LP_RedHawkLeague`，commonality 0.6）：用通讯台呼叫，空投交易。库存：奴隶 3~5（不同种族）、材料表随机挑 6 种、契约书 1、灵魂币 10~20；白银 1500~2500。和联盟敌对时不会来（原版按 faction 过滤；实现时确认 1.7 的 TradeShip 对 `TraderKindDef.faction` 做了敌对检查，没有就加）。
- 访客（visitorTraderKinds）：原版 `Visitor_Outlander_Standard`。

## 五、可选联动

- **HAR**：只有第三节的 HAR 种族条目（`Bridges/HAR/Patches/RedHawk_HAR.xml`），LoadFolders 已经有 HAR 桥，不改 LoadFolders。
- **Isekai**：不需要新文件。奴隶按 Isekai 自己的规则生成等级；检查确认有/无 Isekai 都能生成。若 Isekai 给奴隶带了高等级，不做处理（写进报告）。
- **Ideology**：没有 Ideology 时 memes/precepts 由 MayRequire 跳过。
- **VPE**：无。

## 六、文字规则

所有新文字中文；不写 d20/掷骰/检定/DC/「难度 = …」公式。涉及成功率一律写「成功率 X%」。（本版本基本没有成功率文字。）

## 七、美术

- `Textures/Factions/RedHawkLeague.png`（64×64 和派系图标同规格）：红鹰 + 金币盾徽。
- 不做新物品贴图（全部复用已有物品）。PawnKind 只用原版服装 + 颜色。
- 画图脚本 `ArtSource/RaceArt/draw_redhawk.py`，预览 `ArtSource/Preview/RedHawk-X.Y.0.png`。

## 八、代码文件

新文件（不碰共享大文件，能不改就不改）：
- `Defs/RedHawk.xml`：FactionDef、8 个 PawnKind + `LP_RH_SlaveStock`、5 个 TraderKindDef。
- `Source/RedHawk.cs`：`StockGenerator_RHRaceSlaves`、`StockGenerator_RHDefList`（+ 收购版）、契约书宗主预设、契约开关联动。
- `Source/RedHawkChecks.cs`：检查组 `RedHawk`。
- `Bridges/HAR/Patches/RedHawk_HAR.xml`。
- 只做最小改动：设置里派系开关（Core.cs 的派系分组）、`使用说明.txt`（精确锚点 + diff，别重复插入）、`About.xml`。

## 九、检查（`Test.ps1 -Only RedHawk`，约 12 项，跑一次 + `-NoHAR -Only RedHawk` 一次）

1. 派系 Def 加载，三种商队、据点、飞空商船都在，标签中文。
2. 奴隶商队生成 3 次：每次 10~14 名奴隶，**同一次内种族互不相同**，≥3 个稀有档。
3. 所有生成的奴隶：是人形、有合法 xenotype、guest status = 奴隶、没有亲属关系、无报错。
4. 种族池 30 个基础条目全部解析成功（列出解析失败的名字；力能龙裔失败则按第三节处理）。
5. 无 HAR：池里没有 HAR 条目且不报错；有 HAR：至少能生成一次 HAR 奴隶（强制选该条目）。
6. 材料商队：≥8 种不同材料，五色龙鳞恰好 2 色，没有龙之心/神性火花/圣物。
7. 契约商队：2~4 张契约书，宗主互不相同，标签带宗主名；存档读档后宗主不变。
8. 契约书照常能在契约讲台阅读（走一次已有阅读流程的最短检查，不测恩赐）。
9. 关掉「异界契约」：契约商队库存里没有契约书。
10. 据点交易：联盟据点的 TraderKind 是 `LP_Base_RedHawk`，库存三类都有。
11. 飞空商船：faction 是红鹰；联盟敌对时不能被选中。
12. 关掉派系开关：新世界里没有这个派系；旧存档的「添加派系」按钮能补上。

不跑全套；Peter 自己玩测。

## 十、交付

- 开工前备份 `ArtSource/backup-<当时设备版本>`。
- 报告 `ArtSource/RedHawk-X.Y.0-report.md`（含「决定」一节和偏差），规格副本 `ArtSource/RedHawk-X.Y.0-spec.md`。
- 编译前游戏必须关闭（在线程里问 Peter）；另一线程在编译时先确认没有 RimWorldWin64 进程。

## 十一、本线程已做的决定（汇总）

1. 中立贸易派系，有据点，初始好感 +10；敌对时才派佣兵袭击，袭击频率 ×0.5。
2. 奴隶商队每次 10~14 名、各不同种族，池 30 种（+HAR 3 种）；不卖巫妖、夺心魔、外来者、巨人/泰坦/巨灵/真龙。
3. 价格用原版奴隶市价，不额外加种族倍率。
4. 魔法材料全部复用已有物品，不新增材料；龙之心等里程碑战利品不卖。
5. 契约书每张指定不同宗主并显示在名字上；契约流程本身不改。
6. 据点和飞空商船都有，库存是三类的缩小合集。
7. 买奴隶不影响费伦拓荒同盟好感。
8. 派系开关放在现有派系设置分组，不新增分组。
