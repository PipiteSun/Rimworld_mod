# DND边缘大陆 1.46.0「背景故事」报告

日期 2026-10-26。版本 1.45.1 → 1.46.0。

## 改动文件

新增
- `Defs/Backstories_DND.xml`：139 条 `LP_Bs_*` 背景（童年 46、成年 93），由生成器产出后拷贝过来。
- `Defs/BackstoryPools.xml`：分配规则表（`LichPhylactery.BackstoryPoolDef`，defName `LP_BsPool_*`）。
- `Source/Backstories.cs`：规则 Def、`BackstoryTraitExtension`、分配逻辑、背景特性联动、体型兜底、设置。
- `Source/BackstoryChecks.cs`：`Test.ps1 -Only Backstories`（8 条检查）。
- `ArtSource/Backstories/backstories.txt`、`gen_backstories.py`、`out/Backstories_DND.xml`。

修改
- `Source/BackgroundTraits.cs`：`ExposeBackgrounds()` 里调用 `ExposeBackstories()`；`BackgroundSettings.Draw` 末尾画 `BackstorySettings.Draw`（设置放在 1.32.0 背景特性组下面）。
- `Source/BackgroundTraitChecks.cs`：`SuppressBackgroundsForTests()` 也设置 `Backstories.TestSuppressed=true`。
- `About/About.xml`：1.46.0，描述末尾加一句背景故事。
- `使用说明.txt`：标题版本、第一部分一览一行、第二部分新增【1.46.0 背景故事】、【1.32.0 背景特性】设置后补一句、更新日志 1.46.0。
- 程序集重新构建（`Assemblies/`、各 Bridge）。

备份：`ArtSource/backup-1.45.1/`（About.xml、使用说明.txt、BackgroundTraits.cs、BackgroundTraitChecks.cs、Test.ps1；Test.ps1 实际没改：组名直接对应 `CheckBackstories()`）。

## 1.7 原版核对

- BackstoryDef XML：`<skillGains><Cooking>2</Cooking></skillGains>` 与原版一致；`<workDisables>` 原版两种写法都有（`<li>` 列表和 `Hauling, Mining` 逗号写法），生成器的逗号写法可以直接用，没改。
- `modExtensions` 类名 `LichPhylactery.BackstoryTraitExtension` 和模组命名空间一致，没改。
- 生成顺序（IL 反汇编 `PawnGenerator.TryGenerateNewPawnInternal`）：选异种型 → `GiveAppropriateBioAndNameTo` → `GenerateTraits` → `GenerateBodyType` → `GenerateGenes` → `GenerateSkills`。也就是说背景在**基因之前**分配（不是之后），所以种族识别不能用模组按基因判断的那套（`Items.Is` 等，此时基因还没加），改用原版传进来的 `XenotypeDef` 参数和 `pawn.def`（HAR 种族）。时机仍满足“特性和技能之前”。
- 原版 `FillBackstorySlotShuffled` 只检查 shuffleable、类别和“成年 requiredWorkTags 与童年 workDisables 不重叠”；兵种需要的工作是生成后整只小人丢弃重来。我把这三条都放进合法性判断（兵种 requiredWorkTags、`isFighter` 或 `MustBeCapableOfViolence` 时加 Violent），另外查请求里强制特性与背景 forced/disallowed traits、特性 requiredWorkTags 的冲突。
- `GetBodyTypeFor`：有成年背景时直接返回 `adulthood.BodyTypeFor(gender)`，DND 背景不指定体型会得到空。加了后缀：结果为空且成年背景是 DND 背景时，按原版没有成年背景时的办法（50% 瘦，否则按性别）补上。

## 规则表实际 defName

| 级 | 规则 | 匹配 | 童年 / 成年 | 概率 |
|---|---|---|---|---|
| 跳过 | LP_BsPool_Skip | 兵种 LP_Skeleton, LP_Golem, LP_Scholar, LP_StoneGolem, LP_FH_Skeleton, LP_FH_Golem, LP_LarlochSkeleton, LP_FH_IceDevil, LP_FH_Balor；异种型 LP_IceDevil, LP_Balor；派系 Empire | — | — |
| 兵种 | LP_BsPool_RedWizard | LP_FH_RedWizard, LP_FH_RedTrader | LP_Arcane / LP_Arcane | 100% |
| 兵种 | LP_BsPool_LichNPC | LP_Larloch, LP_LarlochDemilich | LP_Arcane / LP_Race_Lich | 100% |
| 兵种 | LP_BsPool_Clergy | LP_FC_Paladin, LP_FC_Priest, LP_FC_ChurchPriest, LP_Doomguide, LP_DoomJudge | LP_Faith / LP_Faith | 100% |
| 兵种 | LP_BsPool_Thrall | LP_IC_Thrall | LP_Illithid / LP_Illithid | 100% |
| 种族 | LP_BsPool_FireGiant / FrostGiant / CloudGiant / StormGiant | LP_FireGiant / LP_FrostGiant / LP_CloudGiant / LP_StormGiant | 本族×3 + LP_Race_Giant×1（两个槽位） | 设置，默认 75% |
| 种族 | LP_BsPool_Titan | LP_Titan, LP_AwakenedTitan | LP_Race_Titan×3 + Giant×1 | 同上 |
| 种族 | LP_BsPool_Ettin | 异种型 LP_EttinKin；HAR 种族 LP_Ettin | LP_Race_Ettin×3 + Giant×1 | 同上 |
| 种族 | LP_BsPool_Cyclops | 异种型 LP_CyclopsKin；HAR 种族 LP_Cyclops | LP_Race_Cyclops×3 + Giant×1 | 同上 |
| 种族 | LP_BsPool_Marilith | 异种型 LP_MarilithKin；HAR 种族 LP_Marilith | LP_Race_Marilith | 同上 |
| 种族 | LP_BsPool_GenasiFire/Water/Earth/Air | LP_GenasiFire/Water/Earth/Air | LP_Race_GenasiX / LP_Race_Genasi | 同上 |
| 种族 | Troll, ForceDragon, HalfOrc, Elf, Dwarf, MindFlayer, Minotaur, Githyanki, Loxodon, Ogre, Aarakocra, Genie, Oozefolk, Aasimar, Tiefling | 同名 `LP_` 异种型 | LP_Race_同名 | 同上 |
| 派系 | LP_BsPool_FaerunColony | LP_FaerunColony | LP_Faerun×3、LP_Faith×1 | 60% |
| 派系 | LP_BsPool_Thay | LP_FaerunHostile | LP_Thay×3、LP_Faerun×1 | 60% |
| 派系 | LP_BsPool_EmberClan | LP_EmberClan | LP_Ember | 60% |
| 派系 | LP_BsPool_Illithid | LP_IllithidColony | LP_Illithid | 70% |
| 派系 | LP_BsPool_BlackNet | LP_BlackNetwork | LP_Faerun / LP_Underworld×3、LP_Faerun×1 | 60% |
| 派系 | LP_BsPool_Zealots | LP_Zealots | LP_Faith / LP_Zealot | 70% |
| 派系 | LP_BsPool_Larloch | LP_Larloch | LP_Arcane | 70% |
| 派系 | LP_BsPool_KelemvorChurch | LP_KelemvorChurch | LP_Faith | 70% |
| 派系 | LP_BsPool_GiantWanderers | LP_GiantWanderers | LP_Faerun | 30% |
| 原版人类 | LP_BsPool_VanillaHuman | Human 种族 + 原版人类异种型（或无），派系不在上面 | LP_Faerun | 设置，默认 10% |

## 测试结果

- `Test.ps1 -Only Backstories`：**PASS 8 条**（连续两次通过；第二次与 Traits 一起跑：`-Only Backstories,Traits` PASS 22 条）。
  1. 139 条加载、标题齐全、每个种族规则的成年类别都有背景、每个规则类别都有背景；抽查 LP_Bs_RedWizard（智识+5 社交+2，禁 ManualDumb|Cleaning，贵族 0.4）、LP_Bs_Thrall、LP_Bs_ForgeSmith。
  2. 种族率 100%：10 个同盟精灵童年+成年全来自 LP_Race_Elf。
  3. 种族率 0%：10 个精灵没有任何 LP_Race_* 背景。
  4. 原版派系（OutlanderCivil）原版人类：120 个槽位里费伦 11%（一次运行 5%）；0% 时 30 人里 0 个。
  5. 塞伊 30 个战斗兵种都能暴力；红袍法师童年、成年都属于 LP_Arcane（DND 背景占比 28/30）。
  6. 黑网掮客生成 10 个，两槽背景齐全，没有背景相关错误。
  7. 联动概率临时 100%：6/6 拿到“塞伊红袍法师”成年背景的红袍法师成为贵族；强制贤者的 5 个不再拿贵族。
  8. HAR：六臂蛇人、双头巨人、独眼巨人在 100% 时成年背景来自本族类别（巨人也会抽到共用的 LP_Race_Giant）。
- `Test.ps1 -Only Giants`：**PASS 33 条**。
- 全套没跑（按要求）。

## 决定

- 原版人类默认 10%；不用原版派系背景筛选器；巫妖背景只给巫妖 NPC 兵种，玩家仪式巫妖保留原背景；已有小人不补发；背景特性只做概率联动（按说明）。
- 每个槽位单独掷骰：先给童年、成年各自按优先级掷出用哪条规则，再挑背景。挑童年时不看马上要被换掉的原版成年背景（原版成年背景可能禁用暴力，会让战斗兵种的童年一条都挑不到），挑成年时看新的童年。
- 挑背景：先按权重挑一个“有合法背景”的类别，再在类别里平均挑一条（×3/×1 是类别权重）。
- 种族规则：本族类别×3，巨人共用类别×1；元素裔成年只有共用类别。
- 带 LP_Race_* 类别的背景只给这个种族（规则自己列出的类别除外，例如巫妖 NPC 规则的 LP_Race_Lich）。比如“独眼之神的祭司”（半兽人+神职）不会给精灵牧师，“元素调律师”（元素裔+施法者）不会给人类红袍法师。没有这条时，种族率 0% 的精灵仍会从神职里抽到半兽人祭司。
- 背景特性“没满”= 不算基因特性少于 4 个（背景特性是加在原版 2~3 个特性之外的）。冲突检查：已有特性双向冲突、兵种 disallowedTraits、请求 ProhibitedTraits、背景 disallowedTraits、特性禁用兵种需要的工作。
- 自测：和 1.32.0 背景特性一样，其他检查组期间压住 DND 背景故事（`Backstories.TestSuppressed`），只在 Backstories 组打开，免得随机背景改动老检查小人的技能/可做工作。
- 设置滑条 5% 一档；切换只影响之后生成的小人。

## 偏差

1. 生成器写文件加了 `newline='\n'`（Windows 下默认写 CRLF，仓库文本是 LF）。其他照原样。生成器只出 XML，开头注释提到的“可读总表”没有实现（原脚本里就没有）。
2. `backstories.txt` 非注释行数 139，与说明一致。
3. 1.7 里背景分配在基因生成**之前**（说明假设在之后），所以种族按传入的异种型和 HAR 的 `pawn.def` 判断，没用模组按基因判断的工具。
4. 规则 Def 类名用 `BackstoryPoolDef`（模组 C# 类都不带 LP_ 前缀），XML 里的 defName 用 `LP_BsPool_*`。
5. 设备上没有“塞伊巫妖首领”兵种：巫妖 NPC 规则只含拉洛克（LP_Larloch）和半巫妖（LP_LarlochDemilich）。拉洛克的使者（LP_LarlochEnvoy）是活人，走拉洛克派系规则。
6. “丘陵流放者(1.44.0)”在设备上是剧本“山脚的弃儿”，不是派系，所以没有对应派系规则；只做了巨人游群（LP_GiantWanderers）30%。
7. 红袍法师类包含红袍商人 LP_FH_RedTrader；神职兵种包含同盟的教会牧师 LP_FC_ChurchPriest。
8. 跳过范围补充：帝国派系整个跳过；兵种带 backstoryFiltersOverride 或类名含 CreepJoiner 的跳过；红袍骷髅/血肉巨像/石魔像/拉洛克的骷髅等死灵造物兵种和召唤来的寒冰魔、炎魔（兵种与异种型）跳过。
9. 新增体型兜底补丁（见上），否则 DND 成年背景会让体型为空。
10. 测试细节：原版约 25% 的小人（以及派系首领）拿有名有姓的固定人物，背景故事按要求不动它们；测试生成时把这种小人丢掉重生成。测试 4 按背景槽位数统计费伦占比（120 个槽位）。测试 1 的“每个种族类别至少 1 条成年”按种族规则的成年类别检查（元素裔四个血脉类别按设计只有童年）。测试 7 一直生成到 6 个红袍法师拿到“塞伊红袍法师”成年背景（最多 80 个），没拿到贵族的必须是特性满了或冲突。
11. 因为原版的固定人物不动，实际出现率会比名义概率低一些（没有专门测量）。
