# DND边缘大陆 1.18.0 牛头人与吉斯洋基报告

日期：2026-10-24。范围：新增两个可遗传异种型——牛头人 `LP_Minotaur`、吉斯洋基 `LP_Githyanki`——及其全部机制、贴图、自检。

需求来源：`ArtSource/MinotaurGith-spec.md` **不存在**（开工和收尾时都检查过），按协调会话转来的要点实现。

## 0. 接手时 1.17.1 的状态

| 项目 | 状态 |
|---|---|
| `About.xml` | 1.17.1 |
| `ArtSource/RaceArt-1.17.1-report.md` | 在 |
| `Build.ps1`（pwsh 7.6） | 通过 |
| `Test.ps1`（Core） | 3 次中 2 次 `PASS: 1411 checks`；1 次 `FAIL after 410: gene triggers again after the cooldown`（1.15.0 共享组件的 OncePerCooldown 老检查，偶发，与 1.17.1 无关） |

结论：1.17.1 已收尾。改动前的文件备份在 `ArtSource/backup-1.17.1/`（Checks.cs、ConstructDesignChecks.cs、CommonRaceGallery.cs、GeneIconGallery.cs、MindFlayerTadpole.cs、MindFlayerPowers.cs、MindFlayer.cs、Gameplay.cs、Core.cs、SharedComponents.xml、CommonRaces.xml、MindFlayer.xml、About.xml、使用说明.txt、draw_race_art.py、旧 DLL）。

## 1. 改动文件

| 文件 | 类型 | 说明 |
|---|---|---|
| `Defs/Minotaur.xml` | 新增 | 异种型、5 个自定义基因（蛮牛冲撞、犄角、牛鼻、迷宫感、牛头人体型）、蛮牛冲撞能力、犄角武器/迷宫/食量 hediff、牛头人↔半兽人关系 |
| `Defs/Githyanki.xml` | 新增 | 异种型、5 个自定义基因（银剑传承、星界跳跃、巫妖女王的信徒、宿敌、吉斯肤色）、银剑武器与配方、灵能伤害 `LP_SilverPsychic` 与伤势 `LP_PsychicWound`、“银剑拒绝了你”与“巫妖的荣光”心情、吉斯洋基↔夺心魔关系 |
| `Source/Minotaur.cs` | 新增 | 冲撞、犄角/迷宫/体型基因类、与巨魔并肩补丁、两族共用的能力组件 `CompMinotaurGithAbility` |
| `Source/Githyanki.cs` | 新增 | 银剑灵能伤害、锻造限制、缴获来源、星界跳跃、巫妖信徒心情与劝降补丁、宿敌伤害/命中补丁 |
| `Source/Knockback.cs` | 新增 | 共享推人函数 `DNDPush`（见第 5 节第 2 条） |
| `Source/MinotaurGithChecks.cs` | 新增 | 自检 118 项（Core 总数 1411 → 1529），含存读档 4 项 |
| `Source/MindFlayerPowers.cs` | 修改 | 念力投掷的推人逻辑改为调用 `DNDPush`（行为不变） |
| `Source/MindFlayerTadpole.cs` | 修改 | 新增 `TadpoleProgressExtension`（基因上的化形速度倍率），`SpeedFactor` 乘上它；健康页提示加“血脉抗拒” |
| `Source/Checks.cs` | 修改 | 挂入 `CheckMinotaurGith()`、`PrepareMinotaurGithSave()`、`CheckMinotaurGithAfterLoad()`，结果摘要加一句 |
| `Source/CommonRaceGallery.cs`、`Source/GeneIconGallery.cs`、`Source/ConstructDesignChecks.cs` | 修改 | 新增两族图鉴（去掉帽子，方便看牛角/尖耳对位）、图标对照窗口 `TextureGallery`，截图流程末尾加 `MinotaurGith.png`、`MinotaurGithIcons.png`（在测试档目录里） |
| `ArtSource/RaceArt/draw_minotaur_gith.py` | 新增 | 新贴图的绘制脚本（复用 `draw_race_art.py` 的绘制函数） |
| `Textures/MinotaurGith/` | 新增 | 19 张贴图（见第 3 节） |
| `About/About.xml` | 修改 | 1.17.1 → 1.18.0，描述加一句 |
| `使用说明.txt` | 修改 | 顶部新增 1.18.0 条目 |
| `1.7/Assemblies/LichPhylactery.dll` | 重新编译 | |

新增文件没有旧版本，删掉即可回退；修改的文件从 `backup-1.17.1` 拷回。

## 2. 基因表与代谢

### 牛头人（合计 **-1**，测试实测 `LP_Minotaur metabolism total -1`）

| 基因 | 来源 | 代谢 | 说明 |
|---|---|---|---|
| `LP_MinotaurCharge` 蛮牛冲撞 | 自定义 | -1 | 给能力 `LP_BullRush` |
| `LP_MinotaurHorns` 犄角 | 自定义 | 0 | 自绘牛角外观 + 犄角近战（刺，11 点，2.6 秒）；与原版头骨基因互斥 |
| `LP_MinotaurSnout` 牛鼻 | 自定义 | 0 | 自绘牛鼻外观（与原版鼻子基因互斥），取代要点里的 `Nose_Pig` |
| `LP_MinotaurMaze` 迷宫感 | 自定义 | 0 | 有屋顶处移速 ×1.1 |
| `LP_MinotaurStature` 牛头人体型 | 自定义 | 0 | `SmallBodyExtension`：体型 1.3、显示 1.15、搬运再 ×1.15；食量 ×1.2 |
| `Body_Hulk` | 原版 | 0 | |
| `MeleeDamage_Strong` | 原版 | -2 | |
| `AptitudeStrong_Melee` | 原版 | -1 | |
| `Furskin` | 原版 | -1 | |
| `Hair_DarkBrown` | 原版 | 0 | 毛皮颜色跟随发色，所以加棕发基因得到棕色毛皮，不需要新贴图 |
| `Learning_Slow` | 原版 | +2 | |
| `Aggression_Aggressive` | 原版 | +2 | |

### 吉斯洋基（合计 **-2**，测试实测 `LP_Githyanki metabolism total -2`）

| 基因 | 来源 | 代谢 | 说明 |
|---|---|---|---|
| `LP_GithSilverSword` 银剑传承 | 自定义 | -1 | 银剑灵能伤害、锻造资格；带预留互斥标签 `DND_GithLineage` |
| `LP_GithAstralStep` 星界跳跃 | 自定义 | -1 | 给能力 `LP_AstralStep` |
| `LP_GithLichDevotee` 巫妖女王的信徒 | 自定义 | 0 | 有巫妖心情 +3；巫妖劝降 ×1.3 |
| `LP_GithNemesis` 宿敌 | 自定义 | 0 | 对夺心魔伤害 ×1.25、近战命中 +10%；`LP_MindResist` +0.3；蝌蚪化形 ×0.5 |
| `LP_GithSkin` 吉斯肤色 | 自定义（原版肤色模板） | 0 | 皮肤 (176,176,116) 灰黄绿 |
| `PsychicAbility_Enhanced` | 原版 | -2 | 要点里的“灵能增强类”在 1.7 叫这个名字 |
| `AptitudeStrong_Melee` | 原版 | -1 | |
| `Body_Thin` | 原版 | 0 | |
| `Ears_Pointed` | 原版 | 0 | |
| `Beard_NoBeardOnly` | 原版 | 0 | 要点里没有；截图发现男性吉斯洋基会长胡子，我加上了 |
| `AptitudePoor_Social` | 原版 | +1 | |
| `Aggression_Aggressive` | 原版 | +2 | |

两个异种型 `inheritable=true`、`factionlessGenerationWeight=0`，不会随机出现在其他派系；全部非超凡基因。

## 3. 素材清单

全部由 `python -I ArtSource/RaceArt/draw_minotaur_gith.py <模组根目录>` 生成，沿用 1.17.1 的画法（粗黑轮廓、平涂、单侧暗面），复用 `draw_race_art.py` 的形状函数和调色。

| 类别 | 贴图 | 尺寸 |
|---|---|---|
| 异种型图标 | `MinotaurGith/Xenotypes/Minotaur`、`Githyanki` | 256 |
| 基因图标（要求的 8 个） | `MinotaurGith/Genes/Charge`、`Horns`、`Maze`、`Stature`、`SilverSword`、`AstralStep`、`LichDevotee`、`Nemesis` | 256 |
| 基因图标（额外） | `MinotaurGith/Genes/Snout`（牛鼻） | 256 |
| 能力图标 | `MinotaurGith/Abilities/BullRush`、`AstralStep` | 256 |
| 牛角头饰 | `MinotaurGith/Horns/BullHorns_south/_east/_north` | 128（与原版头部附件同画框） |
| 牛鼻 | `MinotaurGith/Snout/BullSnout_south/_east`（北向不可见，和原版猪鼻一样不需要北向图） | 128 |
| 银剑 | `MinotaurGith/Weapons/SilverSword`（横放、剑柄在左，灰银刃+浅紫灵能纹） | 128（与原版长剑同规格） |

不需要新做的：棕色毛皮（原版 Furskin 按发色着色，加 `Hair_DarkBrown` 即可）；吉斯洋基肤色（原版肤色基因模板，图标自动按肤色着色）。

**对位方法**：先在测试里临时把原版的头部、MiniHorns、猪鼻、长剑贴图从游戏里导出成 PNG（量出头部在 128px 画框中约为 x 41-87、y 39-89，猪鼻锚点 y 70-85），按同一画框画牛角和牛鼻，离线叠在导出的毛皮头上检查，再进游戏截图确认。临时导出代码已删除。第一版牛鼻盖住了毛皮头的眼睛，已下移到猪鼻锚点。

**预览图**：`ArtSource/Preview/Minotaur-Gith-1.18.0.png`（游戏内图鉴四向 + 图标对照）。截图里牛头人的牛角在南/东/北/西四向都贴在头上、没有偏移，牛鼻在眼睛下方；带毛皮的角色原版就可能有发型，所以有的牛头人会扎辫子。

## 4. 测试结果

全部用 pwsh 7.6 运行 `Source/Build.ps1` 与 `Source/Test.ps1`。构建：`PASS: LichPhylactery built against installed RimWorld 1.7 and Harmony.`

`MinotaurGithChecks.cs` 覆盖：两个异种型能生成且基因全在、代谢合计、图标和贴图加载（128px 规格）、牛角/牛鼻渲染节点、冲撞（停在敌人前、击退 2 格、伤害 15×体型、眩晕、撞墙停下且墙无损、友方挡路、最多 8 格、通过能力组件触发）、犄角 Verb（锐器、比拳头重且慢、移除基因后消失）、体型 1.3/显示 1.15/搬运 1.5 倍/食量 1.5 倍、室内移速、巨魔并肩 +5%（含真实伤害采样）、银剑（与钢长剑同数值、商人不卖、配方材料与研究、只有基因持有者能开工、灵能伤害 8×敏感度、对夺心魔 ×2、心灵失聪无效、伤害实例真的出现在命中结果里、非吉斯手里等于钢长剑、拒绝心情、敌方吉斯洋基可携带）、星界跳跃（距离/占位/落点、精神力消耗与不足）、宿敌（心智抗性 0.3、蝌蚪一天进度是常人的 35%、伤害 ×1.25 只对夺心魔、命中 +10%）、种族关系心情（-8/-6、+2/+2、离开后消失）、巫妖信徒心情、巫妖劝降 ×1.3（且普通狱卒无加成）、存读档往返（异种型、基因数、体型、犄角、食量、冲撞冷却、优秀品质银剑、心智抗性）、测试结束清理掉落物并检查无遗留。随机角色沿用 `canGeneratePawnRelations:false`、去掉植入物与伤势的惯例。

**开发过程（Core 档）**：第一次跑通前修了 4 处**测试夹具**问题（游戏逻辑没改）：饥饿速度随当前饥饿档位变化（改为都按“吃饱”档比较，并去掉美食家等随机特质）；加屋顶会让格子变暗、黑暗本身降移速（改为同一格比较有无迷宫感）；近战伤害每次有随机浮动、不同角色的近战伤害系数不同（改为同一人、同一随机种子比较银剑和钢长剑）；1.7 刚给的心灵链接 `HasPsylink` 不会立刻变 true（代码和测试都改用 `GetPsylinkLevel()`，这一条是真的代码问题）。另外实测发现牛头人代谢 -1 本身让食量 ×1.25，于是把食量 hediff 从 ×1.5 改成 ×1.2。

**最终矩阵**（全部修复之后）：

| 档 | 结果 |
|---|---|
| Core | `PASS: 1529 checks`（1.17.1 为 1411，新增 118 项），共跑 11 次，10 次通过；失败的那次见下 |
| Appearance | 2/2 `PASS: 1729 checks` |
| Warcasket | `PASS: 1580 checks` |
| VisualCompat | `PASS: 11210 visual checks` |
| HealthCompat None / Human / Mythic | `PASS: 286 / 269 / 267`（None 第一次是游戏 PASS 后没在 360 秒内退出，脚本报超时；重跑正常） |
| HealthCompat Both、HealthCompat Full、VisualCompat Full | 本模组检查全部 PASS（271 / 271 / 11210），脚本因日志里的第三方报错退出码 1：`Config error in HA_Category / MB_Category`（HumanAptitudes 与 MythicBloodlines）、`Error in static constructor of GD3.HarmonyInit`，与 1.17.0 报告记录的相同 |
| Full | `FAIL after 338 checks: wyrmling scales give 30% sharp and 15% blunt armor`——力能龙的老检查，在夺心魔和本次检查之前，1.17.0 报告已确认与新内容无关 |

Full、Health-Full、VisualFull、Appearance、Core 的日志里没有任何提到 Minotaur / Gith / SilverSword / LP_ / LichPhylactery 的警告或报错。

**Core 的一次失败**：矩阵里第 3 次 Core 停在 `FAIL after 1240 checks: Skeleton registry rebuild wakes omitted pawn`（`ConstructLifecycleChecks.cs` 的老检查，在本次检查之后运行）。这条检查只看巫妖主人的精神力和造物统御容量，我的代码不碰这两样；本次检查结束会清掉所有新出现的物品并恢复派系好感，测试用的假巫妖也已销毁。之前的报告里没记录过这条偶发。之后又连跑 8 次 Core 全部通过，没能复现；我没有拿 1.17.1 做对照（11 次里只出现 1 次，对照要跑很多次才有意义），也没有为了变绿去改它。如果你以后再见到它，告诉我，我再细查。

## 5. 与构思的差异 / 我自己做的取舍

1. **`Headbone_Horns` 在 1.7 不存在**（只有 `Headbone_MiniHorns`、`Headbone_CenterHorn`）。“用自绘牛角替换”做成自定义头骨基因 `LP_MinotaurHorns`，带原版 `Headbone` 互斥标签，同一个基因也提供犄角近战，所以“犄角”图标就是这个基因的图标。
2. **力能龙没有“力场冲击”这个能力**（力能龙的能力是吐息、吞噬护盾、弹力法球、力墙、力场囚笼、念动法球）。冲撞的击退复用的是夺心魔“念力投掷”的推人逻辑：我把它抽成共享的 `DNDPush.Destination/Move`，念力投掷改为调用它（行为不变，夺心魔检查照常通过）。眩晕用原版 `StunHandler.StunFor`。
3. **`Hunger_High` 在 1.7 不存在**，而且体型不影响食量（实测）。牛头人代谢 -1 按原版曲线本身让食量 ×1.25，所以“牛头人食量”hediff 定为 ×1.2，合计约 1.5 倍；并入“牛头人体型”基因，没有单独的基因。
4. **体型 1.3**：搬运“+50%”理解为总量约为普通人 1.5 倍（原版搬运量已随体型 ×1.3，再 ×1.15）。renderScale 1.15。与 `Body_Hulk` 叠加是整体等比缩放，世界里不会穿模；图鉴的固定大小肖像框里牛角尖会被框顶裁掉一点（只是肖像取景，游戏地图上正常）。
5. **牛鼻**：鼻子可以作为基因外观挂上（和原版猪鼻同样的渲染节点），所以做了自定义 `LP_MinotaurSnout` 代替 `Nose_Pig`，多画了一个牛鼻基因图标。
6. **巨魔并肩**做了：条件是同一地图上同时有本殖民地的牛头人和巨魔**自由殖民者**，被征召的牛头人或巨魔近战伤害 ×1.05（按伤害理解“近战+5%”，不是命中）。
7. **银剑**：不是按材料做的武器，固定 40 等离子钢 + 120 白银，数值等于钢长剑（剑柄 9 钝、剑尖/剑刃 23），品质可变；研究前置用原版 `LongBlades`（长剑的前置），没有新开研究。`tradeability=Sellable`：商人不出售，玩家可以卖掉。缴获来源：非玩家阵营的银剑传承基因持有者生成时 25% 概率手持银剑（目前两族都不在任何派系里随机出现，所以这条只对其他模组/开发者模式/以后的派系生效）。灵能伤害是自定义的 `LP_SilverPsychic`（原版 `Psychic` 伤害属于《异象》DLC，测试档不一定加载），不受护甲减免，留下“灵能灼伤”。
8. **星界跳跃的精神力**：有心灵链接时消耗 5% 精神力；没有心灵链接的吉斯洋基没有精神力可扣，只受冷却限制（否则这个基因对大多数吉斯洋基是废的）。
9. **“巫妖招募吉斯洋基俘虏抵抗下降 30%”** 理解为：巫妖作为发起者的劝降谈话，这次降低的抵抗 ×1.3。
10. **“看到巫妖 +3”与“吉斯洋基→巫妖 +3”** 是同一条心情（巫妖不是异种型，不能用种族关系框架）：同一地图上有本殖民地（成员或囚犯）的巫妖。
11. **宿敌命中 +10%** 只做了近战命中（加在原版近战命中率上，封顶 100%）；远程命中没有改。伤害 +25% 对一切伤害生效（含银剑灵能那一段）。
12. **蝌蚪 ×0.5** 做成通用的 `TadpoleProgressExtension`（挂在基因上），与巨魔的减半、心智抗性的减速相乘：吉斯洋基实际约为常人的 35%。
13. 吉斯洋基没有加 `Hair_BaldOnly`（要点里是可选），保留头发；加了 `Beard_NoBeardOnly`。

## 6. 需要你决定的点

1. **要点之外的原文细节**：`MinotaurGith-spec.md` 不在 ArtSource 里，如果原文还有别的要求，请把文件放进来，我对照补齐。
2. **数值**：冲撞 15×体型 / 冷却 1 天、犄角 11 点 2.6 秒、银剑灵能 8×敏感度、银剑 40 等离子钢 + 120 白银、劝降 ×1.3、宿敌 ×1.25 与 +10%，需要你确认手感。
3. **星界跳跃对无心灵链接者免费**（见第 5 节第 8 条），如果希望必须有心灵链接才能用，改一行即可。
4. **银剑缴获来源**目前实际上很少触发（两族不在任何派系里），要不要给某个原版派系加一点吉斯洋基成员（要点说不做新派系，所以我没动）。
5. **美术**：程序绘制，风格和 1.17.1 一致；牛头人头部是原版毛皮头 + 牛角 + 牛鼻，不是完整的牛头造型。如果想要更像牛的整颗头，需要单独画头部贴图（HeadTypeDef），工作量较大。
