# DND边缘大陆 1.39.0 报告：信仰

需求：ArtSource/Faith-1.39.0-spec.md。备份：ArtSource/backup-1.38.0（改动前的 About/About.xml、使用说明.txt、Source/Core.cs、Twilight.cs、TwilightDeities.cs、Defs/Research.xml、Defs/Twilight.xml、Bridges/VPE/Defs/Damages.xml，以及 1.7/Assemblies 主 dll 和 HAR / VPE / 神兽血裔三个桥接 dll）。贴图预览：ArtSource/Faith-1.39.0-preview.png（脚本直接拼的，不是游戏截图）。

## 测试结果

| 命令 | 结果 |
|---|---|
| `Source\Build.ps1` | PASS（主程序集 + HAR / VPE / 神兽血裔桥接） |
| `Source\Test.ps1 -Only Faith` | PASS：17 条（16 条检查 + 存读档 1 条；需求说 10～15 条，研究那条单列了所以多一条），日志无红字。最终版连跑多次都过（见下方“稳定性”） |
| `Source\Test.ps1 -Only ResearchTab` | PASS：6 条（研究都在 LP_FireGiantTech、坐标不重叠、前置在左、DND 内容只挂本页签研究） |
| `Source\Test.ps1 -VPE` | PASS：434 条。BridgeChecks 的 8 条路径 / 72 个能力不变；`LP_Radiant damage exists` 照样过（这个伤害现在由主模组定义） |
| `Source\Test.ps1 -Only Twilight`（额外） | PASS：114 条。因为改了神祇类名和 `Twilight.Deities`，单独跑了黄昏之年这一组：8 位灌注神、灌注、附体、袭击钩子、存读档都过 |
| `Source\Test.ps1 -Only Events`（额外） | 第 87 条失败 `migration group arrives: ember`，**和本版无关**：1.34.1 起迁徙巨人被改挂到隐藏派系“巨人游群”，这条老检查还在比对原来的余烬氏族，所以 -Only Events 自 1.34.1 起就一直过不去。黄昏之年那部分改用 `-Only Twilight` 跑过了 |
| 完整套 `Source\Test.ps1` | 没跑（按要求可选） |

测试档默认带 HAR。开发过程中修掉的问题：遗物武器继承了原版的 recipeMaker（ConfigErrors）；`LP_ShadowVeil` 和 1.28.0 传送门的状态重名（改名 LP_SharVeil）；世界生成时 `Faction.OfPlayer` 还不存在（记录钩子报红字，改成 `Faction.IsPlayer`）；检查里点火、遗传两处随机性改成确定的写法。

-Only Faith 的 16 条（需求第十一节）：
1. 11 位神：信徒 degree 1～11 按需求顺序，9 位可信奉，每位都有领域神术、遗物、赐福/降罚、喜爱的祭品、图标；老 8 位仍是黄昏之年的灌注神；敌对关系双向（洛山达的敌对神 = 莎尔、马拉尔、塔洛斯）。
2. 本版 69 个新定义都过 ConfigErrors，30 张贴图都读得到。
3. 两项研究在 LP_FireGiantTech（800 / X0 Y2.8，1200 / X1 Y2.8，前置在左），圣坛、圣徽、圣水由“信仰与神术”解锁。
4. AddFavor 夹到 ±100，FavorChanged 收到实际变化量和原因，进历史记录。
5. 守护神：第一次选免费；换神旧神 -30、15 天冷却；`ignoreCooldown` 能绕过；伊尔斯维不能选。
6. 祈祷：娱乐会挑祈祷工作；+0.5，信徒 ×2，每天 +5 封顶；无神者不祈祷；献神迹残片 +10；“喜爱的祭品”配方跟着守护神（洛山达：土豆可以、钢不行）。
7. 圣徽：戴上得到 3 个通用神术 + 守护神的领域神术，摘下收回，无神者什么都没有。
8. P 倍率 = (0.5 + 社交/20 + 圣徽 0.1) × 心灵敏感度；信徒消耗减半（3 → 1.5）、神恩强度 +0.15；神眷不足按钮灰掉并写原因；领域神术神眷 <20 不能放。
9. 驱逐邪祟：活人 25，骷髅（塞伊的骷髅兵种，确实是死灵造物）50。
10. 召雷：三道落雷都在目标 2 格内，伤害的 instigator 是施法者。
11. 祝圣圣水：1.9 格内两人受伤、浇灭火、巨魔再生停住；喝下去流感严重度 ×0.9、+2 心情。
12. 季度显圣：洛山达 60 → 赐福 3 天（地图条件、+4 心情）；-40 → 降罚。
13. 朝圣者：有圣坛和守护神时能触发，3～5 名同一位神的信徒来到圣坛，接待完成按需求给神眷（+8 / 守护神 +12）。
14. 神选仪式：扣 50 神眷和 3 片残片，得到“神选者（洛山达）”（和信徒叠到消耗 ×0.25），转成阿斯莫（只换异种基因，内生基因原样）；巫妖不能转。
15. 阿斯莫（实测代谢 -1）、提夫林（实测 0）：本源基因、恰好一个天界启示、能力、神圣/火焰 ×0.5、阿斯莫打巫妖 ×1.25、两个阿斯莫的孩子一定继承天界之责。
16. 设置：信仰关闭时作为、任务、祈祷、神术都不生效且不报错，信仰任务频率 0% 时不出任务，特性权重 0.3 / 0.1 恢复。
存读档：守护神、更换冷却、各神神眷、历史记录都保留。

稳定性：最终版 -Only Faith 连跑 2 次 + 后台再跑 3 次，5 次全部 PASS（17 条）。

## 老 8 位神的实际 defName（设备上 1.28.0 的名字，保留不改）

| 神 | defName | key | 黄昏之年灌注 degree（LP_DivineInfusion） | 信徒 degree（LP_Devotee） |
|---|---|---|---|---|
| 班恩 | LP_Deity_Bane | Bane | 1 | 7 |
| 苏尔特 | **LP_Deity_Surtr**（需求写 Surtur） | Surtr | 2 | 9 |
| 伊尔斯维 | **LP_Deity_Ilsvi**（需求写 Ilsensine） | Ilsvi | 3 | 10 |
| 提尔 | LP_Deity_Tyr | Tyr | 4 | 2 |
| 洛山达 | LP_Deity_Lathander | Lathander | 5 | 1 |
| 维拉基斯 | LP_Deity_Vlaakith | Vlaakith | 6 | 11 |
| 塔洛斯 | LP_Deity_Talos | Talos | 7 | 6 |
| 马拉尔 | LP_Deity_Malar | Malar | 8 | 5 |
| 新：密斯特拉 | LP_Deity_Mystra | Mystra | 0（不灌注） | 3 |
| 新：凯兰沃 | LP_Deity_Kelemvor | Kelemvor | 0 | 4 |
| 新：莎尔 | LP_Deity_Shar | Shar | 0 | 8 |

两套等级是分开的：原来的 `degree` 字段仍是神力灌注特性的等级（0 = 不进灌注，`Twilight.Deities` 只列 degree>0 的 8 位），新增的 `devoteeDegree` 是信徒特性的等级。

## 给成神线程的接口（实际名字与签名）

命名空间都是 `LichPhylactery`。

| 需求里的名字 | 实际 | 说明 |
|---|---|---|
| `LP_DeityDef` | `public partial class LP_DeityDef : Def` | **原来叫 DeityDef，本版改名**（XML 元素 `LichPhylactery.LP_DeityDef`）。字段：`key`、`degree`（灌注）、`devoteeDegree`、`alignment`（`DeityAlignment.Good/Neutral/Evil`）、`worshippable`、`epithet`、`enemies`、`domainAbility`、`relic`、`likedOfferings`（List&lt;string&gt;，ThingDef 或 ThingCategoryDef 的 defName）、`blessing`/`curse`（Type，`FaithManifest` 子类）、`iconPath`、`color`。辅助：`Icon`、`EnemyOf(LP_DeityDef)`（双向）、`Enemies`、`Likes(ThingDef)`、`Good`/`Evil`、`FullLabel` |
| `WorldComponent_Faith` | `public class WorldComponent_Faith : WorldComponent` | `favor`（Dictionary&lt;LP_DeityDef,float&gt;）、`patron`、`patronCooldownUntil`、`lastManifest`、`nextManifestTick`、`questCooldown`、`relicGranted`、`history`（List&lt;FavorEntry&gt;）等；`Get(god)`、`Blessed(key)`、`Cursed(key)` |
| `FaithUtility` | `public static class FaithUtility` | 见下 |
| `float GetFavor(LP_DeityDef god)` | 同名同签名 | |
| `void AddFavor(LP_DeityDef god, float amount, string reason)` | 同名同签名 | 夹到 -100～100；reason 进 Dev 模式日志和圣坛“神眷一览”里的历史；**不乘**神眷获取倍率（倍率在内部的 `Faith.Gain` 里乘） |
| `LP_DeityDef Patron { get; }` | 同名 | |
| `void SetPatron(LP_DeityDef god, bool ignoreCooldown = false)` | 同名同签名 | 换神扣旧神 30 并开始 15 天冷却；另有 `string CannotSetPatron(LP_DeityDef)` 查原因 |
| `bool IsDevoteeOf(Pawn p, LP_DeityDef god)` | 同名同签名 | |
| `LP_DeityDef DevoteeGod(Pawn p)` | 同名同签名 | |
| `bool IsAtheist(Pawn p)` | 同名同签名 | |
| `bool IsChosen(Pawn p, out LP_DeityDef god)` | 同名同签名 | |
| `IEnumerable<Building> Altars(Map map)` | 同名同签名 | 玩家的 LP_FaithAltar |
| `event Action<LP_DeityDef,float,string> FavorChanged` | 同名 | 参数是夹值后的**实际变化量** |
| `LP_DeityDef DegreeToDeity(int)` / `int DeityToDegree(LP_DeityDef)` | 同名同签名 | 信徒 degree |
| `float MiracleCost(Pawn, AbilityDef)` | 同名同签名 | 普通静态方法，可以 Harmony 后缀 |
| 另：`AllDeities`、`Worshippable` | 属性 | 全部 11 位 / 可信奉 9 位 |
| 圣坛 `LP_Altar` / `Building_Altar` | **`LP_FaithAltar` / `Building_FaithAltar`** | 设备上 `LP_Altar` 和 `LichPhylactery.Building_Altar` 早就是死灵仪式台，只能改名 |
| `Building_Altar.ExtraGizmos` | **`Building_FaithAltar.ExtraGizmos`**：`public static readonly List<Func<Building_FaithAltar,IEnumerable<Gizmo>>>` | 每个登记的函数返回要加的按钮（可多个、可空），出错只记一次日志 |
| TraitDef `LP_Devotee` | 同名，degree 1～11（洛山达=1、提尔=2、密斯特拉=3、凯兰沃=4、马拉尔=5、塔洛斯=6、班恩=7、莎尔=8、苏尔特=9、伊尔斯维=10、维拉基斯=11） | 10、11 权重 0，只在敌人身上 |
| TraitDef `LP_Atheist` | 同名 | 与信徒互斥（exclusionTags `LP_Faith`） |
| HediffDef `LP_Chosen`（`Hediff_Chosen.deity`） | 同名；类 `LichPhylactery.Hediff_Chosen`，字段 `public LP_DeityDef deity` | 标签“神选者（洛山达）” |

其他可能用得上的：`Faith.God(string key)`、`Faith.Gain(god, amount, reason)`（正数乘倍率）、`Faith.CompleteChosen(Pawn, bool convert)`、`FaithRaces.Convert(Pawn, XenotypeDef)`、`FaithManifest.Start(comp, god, bool blessing)`、`Miracles.Power(Pawn)`（P 值）。

## 和需求不一样的地方

1. **名字冲突**（设备上已有，改名）：圣坛 `LP_Altar`/`Building_Altar` → `LP_FaithAltar`/`Building_FaithAltar`；圣水 `LP_HolyWater`（1.22.0 炼金桌的圣水，只伤不死者）→ 新的 **`LP_BlessedWater`「祝圣圣水」**，老圣水原样保留；暗影帷幕的状态 → `LP_SharVeil`（`LP_ShadowVeil` 是 1.28.0 传送门的）。神术的 AbilityDef 名字照需求不变。
2. **神祇类名**：设备上是 `DeityDef`，按接口要求改成 `LP_DeityDef`（Twilight.cs、TwilightDeities.cs、Defs/Twilight.xml）。存档里只存 defName，旧档不受影响。老神 defName 按设备保留：`LP_Deity_Surtr`、`LP_Deity_Ilsvi`。
3. **LP_Radiant** 原来只在 VPE 桥接里定义（不装 VPE 时代码退回“神罚”）。现在移到主模组 Defs/Faith.xml，从 Bridges/VPE/Defs/Damages.xml 删掉（数值不变），装不装 VPE 都有。
4. **神圣伤害对巨魔**：LP_Radiant 的伤口是“烧伤”，而 1.12.0 的巨魔规则把伤口是烧伤的伤害一律当火（再生停 6 小时、死后不复活）。所以祝圣圣水、阿斯莫、神术的神圣伤害对巨魔实际是**火的完整效果**，比需求的“停 1 小时（效果减半）”强；1 小时的那段代码也在，但被火规则盖过。要真正减半得改 LP_Radiant 的伤口类型或巨魔规则，会影响 VPE 牧师，这次没动。
5. **各神作为没做 / 做法不同的**（需求说写不进去就删掉并说明）：
   - 密斯特拉“摧毁魔法物品 -1”：没做（没有可靠的“魔法物品被毁”钩子）。
   - 密斯特拉“施放法师/牧师路径”：只认原版灵能和神术；VPE 的 VEF 能力不经过原版 Ability，没算。
   - 塔洛斯“火灾 +1”：没做，只算落雷，且每天最多 +3（落雷在雷暴里很频繁）。
   - 班恩“镇压奴隶 +0.2”：没做（没找到干净的钩子）。
   - 提尔“审判仪式 +3”：按“仪式完成”历史事件里带的仪式 defName 含 Trial 判断，没在测试里验证过。
   - 莎尔“殖民者失去亲人未崩溃 +2”：近似为“殖民者死去时殖民地里还有活着、没在精神崩溃的血亲”。
   - 汲取灵魂 -3 挂在“命匣得到献祭经验”上（Phylactery.Feed），也就是每次喂命匣都算。
   - 马拉尔“狩猎击杀”= 击杀者当时的工作是“狩猎”。
6. **种族数值**：需求的原版基因合计代谢阿斯莫 -4、提夫林 -6，要到 -1 / 0 只能靠自定义基因补正：阿斯莫本源“天界之责”+3（其余自定义基因 0），提夫林本源“魔鬼血统”+4、“魔鬼契约”+2。实测 -1 / 0。
7. **治疗之手**的“等级/2 + 社交/2”理解为“医疗等级/2 + 社交等级/2”。
8. **天界启示三选一**：异种型里默认放光辉之魂，生成时（派系、开局、神选转换、天界之子）按小人编号在三个里重新挑一个。肤色（浅金/银白）、发色（金/银）、提夫林肤色（暗红/紫）同样处理。
9. **研究“天界与地狱血统”**：本模组没有基因相关配方，所以它用来解锁神选仪式里的“改换血脉”选项。派系生成不需要它。
10. **神选仪式**：神迹残片从整张地图上扣（不要求放在圣坛旁）；开始和完成时都检查条件，资源在完成时才扣。
11. **凯兰沃的赐福**“死亡时 25% 改为倒地”：做成“致命伤改成轻伤、人活下来”，不强制倒地。
12. **灰烬天平**“复生术/复生卷轴必成且无复活精神病”：本模组的复生卷轴本来就没有复活后遗症，申诉期内去掉它“死亡超过 1 天/腐烂”的限制；原版复活血清、VPE 的复生不受影响。
13. **魔网涌流**：清零的是原版能力（RimWorld.Ability）的冷却，VPE 灵能的冷却清不了；有心灵链接就 +30% 精神力（不看装没装 VPE）。
14. **寻回圣物**：用原版“物品藏匿点 + 前哨站”两部分的据点，把遗物放进藏匿点；带回来放到圣坛 4 格内自动算“供奉”（没做右键菜单）。过期时如果遗物已经在你的地图上或在殖民者身上，不算失败。
15. **朝圣者**“20% 一名请求加入”：直接加入（有信件），没有再弹一次选择。朝圣者的祈祷不加神眷，接待完成时一次性结算。
16. **任务间隔**“每 20～40 天”等：用叙事者权重 + 最短间隔（朝圣者 20 天、神谕试炼 30、寻回圣物 60、亵渎者 30）实现，不是精确区间。
17. **神圣伤害 ×2**：驱逐邪祟、圣化之地、安息、圣水、光辉消耗对不死者/邪魔/夺心魔 ×2（和 VPE 牧师的 HolyVulnerable 同一规则）。祝圣圣水对活人也有伤害（需求原文“半径内 12 点 LP_Radiant”）。
18. **遗物的槽位**：都在饰品层 LP_Accessory。提尔之手、野兽之爪、黑手护手占“双手”（和戒指、元素符冲突）；银焰之泪、暗夜之泪放“眼部”，熔炉之心放“躯干”，灰烬天平放“腰部”，这样都能和圣徽（颈部）一起戴。圣徽和 1.22.0 的生命护符都在颈部，二选一。
19. **野兽之爪“流血 ×1.5”** 做成近战命中追加 3 点割伤；“驯兽 +4”做成驯兽、训练成功率各 +12%（饰品没法直接加技能等级）。
20. **锻造速度**：原版没有“锻造速度”属性，加成挂在全局工作速度上、只在做锻造类工作单（锻造工作类型的工作台：原版锻造台、熔火锻炉等）时生效。
21. **季度显圣计时**：全局每 15 天一次（从第一次选守护神起算），`lastManifest` 记每位神上次显圣的时间。
22. **新增的贴图**比需求清单多：2 个异种型图标、11 个基因图标、5 个种族能力图标（共 58 张）。
23. **自测时**信徒/无神者不随机生成（和背景特性一样，免得改动其他检查组的小人）；Faith 检查组自己临时打开。

## 改动的文件

新增：
- Defs/Faith.xml（3 位新神、LP_Radiant、信徒/无神者、圣坛、工作给予、祈祷/神选工作、献祭配方、圣徽、祝圣圣水、9 件遗物、神选者、心情、显圣条件）
- Defs/Faith_Miracles.xml（12 个神术与 3 个状态）、Defs/Faith_Incidents.xml（4 个事件 + 朝圣者信件）、Defs/Faith_Races.xml（阿斯莫、提夫林、14 个基因、4 个能力、2 个状态、5 个心情）、Patches/Faith.xml（派系固定信仰、种族出现率）
- Source/Faith.cs、FaithMiracles.cs、FaithIncidents.cs、FaithRaces.cs、FaithChecks.cs
- ArtSource/RaceArt/draw_faith.py；Textures/Faith/（58 张）
- ArtSource/Faith-1.39.0-spec.md、Faith-1.39.0-preview.png、本报告

**共享文件**（都在 backup-1.38.0 里有原件）：
- Source/Core.cs：设置的 ExposeData 加 `ExposeFaith()`，设置页左栏在流浆人之后加 `FaithSettings.Draw(l)`。
- Source/Twilight.cs：`DeityDef` → `partial class LP_DeityDef`；`Twilight.Deities` 只列 degree>0 的 8 位灌注神。
- Source/TwilightDeities.cs：类名替换（10 处），逻辑没动。
- Defs/Twilight.xml：元素名改 `LichPhylactery.LP_DeityDef`，8 位神补上信仰字段。
- Defs/Research.xml：Y 2.8 加两项研究，页头注释加一句。
- Bridges/VPE/Defs/Damages.xml：删掉 LP_Radiant（移到主模组）。BridgeChecks.cs 没动。
- About/About.xml（1.39.0 + 描述）、使用说明.txt（标题、研究一段、内容一览两行、第二部分【1.39.0 信仰】、更新日志）。
- 重新编译的 1.7/Assemblies/LichPhylactery.dll 和三个桥接 dll（桥接源码没动）。

对原版/本模组方法加的 Harmony 补丁（都先判断信仰开关或对应的人/物）：Pawn_RecordsTracker.Increment、Plant.PlantCollected、Plant.GrowthRate、Pawn.Kill、HistoryEventsManager.RecordEvent、GenGuest.PrisonerRelease/SlaveRelease/EnslavePrisoner、RimWorld.Ability.Activate/StartCooldown、Bill_Production.Notify_IterationCompleted、ResurrectionUtility.TryResurrect、Pawn_HealthTracker.AddHediff/PostApplyDamage、WeatherEvent_LightningStrike.FireEvent、GameConditionManager.RegisterCondition、Pawn_InteractionsTracker.SocialFightChance、ThoughtWorker_Dark、MemoryThoughtHandler.TryGainMemory、CompRefuelable.ConsumeFuel、InteractionWorker_RecruitAttempt.Interacted、DamageWorker_AddInjury.FinalizeAndAddInjury、Pawn.PreApplyDamage、Pawn_ApparelTracker.Notify_ApparelAdded/Removed、Verb.TryStartCastOn、Verb_MeleeAttackDamage.ApplyMeleeDamageToTarget、PawnGenerator.GeneratePawn、PregnancyUtility.ApplyBirthOutcome、LichMod.WriteSettings；本模组的 Phylactery.Feed、ItemScrolls.CannotRevivify、Souls.Unfit。另外订阅了 `NetherilAscension.ArcaneDemigodGranted`。

## Peter 游玩时请注意

- 先研究“信仰与神术”，建圣坛（杂项菜单），在圣坛上**先选守护神**，之后才有祈祷、献祭和信仰任务。“神眷一览”按钮能看全部神眷、阈值和最近的变化原因。
- 神术花的是**殖民地共用的神眷**，放多了会把守护神的神眷拉低，影响赐福和领域神术（≥20）。
- 圣徽要穿在饰品栏（颈部），和生命护符冲突。
- 神眷的平衡全凭估计：每天祈祷上限 +5、非守护神每天回落 0.5、敌对神每天 -0.2。如果涨得太快/太慢，设置里的“神眷获取倍率”可以先调，再告诉我改基数。
- 朝圣者、神谕试炼、寻回圣物、亵渎者都只在有圣坛和守护神时出现；寻回圣物要有敌对的派系；亵渎者要守护神的敌对神有一个敌对的派系（比如信洛山达时的海盗/部落，信提尔时的塞伊）。
- 神圣伤害对巨魔现在等于火（见偏差 4），对自己殖民地的巨魔扔祝圣圣水要小心。
- 阿斯莫、提夫林的外观（光环位置、角、尾巴、肤色）没在游戏里截图看过，只看了贴图；光环颜色随发色。
- 没自动测、请在游戏里看：圣坛右键“在圣坛祈祷”、娱乐自动祈祷、献祭工作单、“选择守护神”菜单的图标、神选仪式的确认窗口、各神术的瞄准与特效、朝圣者信件的“接待/拒绝”和他们的祈祷行为、寻回圣物的据点、亵渎者袭击、季度显圣的天色、圣坛光色随守护神变化。
