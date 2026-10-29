# DND边缘大陆 1.17.0 夺心魔报告

日期：2026-10-24。范围：新增夺心魔异种型及其全部配套（脑髓、吞噬大脑、心灵震爆、蝌蚪寄生与化形、上古脑、六项灵能、半夺心魔）。五个阶段**全部完成**，每个阶段都单独编译并通过了 Core 主测试，然后跑了完整测试矩阵（见“测试结果”）。

需求来源：`ArtSource/MindFlayer-spec.md` **不存在**（开工时和收尾时都检查过），所以按协调会话转来的要点实现（等同于项目文件 `dnd-genes/夺心魔实现要求.md` 与 `夺心魔构思.md` 的摘要）。我没有读到原文，原文里如果有要点之外的细节，可能没覆盖到，见文末“需要你决定的点”。

## 0. 1.16.0 的实际状态（接手时）

| 项目 | 状态 |
|---|---|
| `About.xml` 版本号 | 已是 1.16.0 |
| `ArtSource/CommonRaces-1.16.0-report.md` | **缺失**，我已补写 |
| `ArtSource/backup-1.15.0` | 存在（Checks.cs、ConstructDesignChecks.cs、OncePerCooldown.cs、RacialRelation.cs、SharedComponentChecks.cs、SharedComponents.xml、About.xml、使用说明.txt、旧 DLL） |
| Source 中的 1.16.0 文件 | `CommonRaces.cs`、`CommonRaceChecks.cs`、`CommonRaceGallery.cs`、`Defs/CommonRaces.xml` 都在；使用说明已有 1.16.0 条目 |
| git | 这个文件夹不是 git 仓库 |
| `Build.ps1` | 通过 |
| `Test.ps1`（Core） | 通过：`PASS: 1237 checks … common races (half-orc, elf, dwarf).`，图鉴截图 `CommonRaces.png` 也生成了 |
| 上次会话留下的其他档结果 | Appearance `FAIL after 328`（力能龙食性检查 NRE），Full `FAIL after 512`（矮人搬运量） |

结论：1.16.0 的功能做完了，断线时是卡在“跑其他档和写报告”。复跑过程中我找到并修了以下**测试夹具不稳定**的问题（都只改测试，不改游戏逻辑）：

1. `CheckDwarf` 屋顶检查：出生点本来就在厚岩顶下时，“恢复原屋顶”等于没离开，检查必然失败。改成先设为无顶验证，再恢复原屋顶。（Appearance 档复现过）
2. `CheckDwarf` 搬运量：原来与常数 75×0.8×0.9 比较；Full 档的其他模组改了搬运量基础值（实测 61.56）。改成与同环境普通人的比值比较（72%）。
3. 半兽人/矮人的近战伤害采样：`TryGetMeleeVerb` 随机挑 verb，偶尔挑到非伤害类 verb（返回 null，报“Non-static method requires a target”）。改为取第一个伤害类近战 verb。
4. `CheckHalfOrc` 致命伤：原来用 100 点切割打大脑，伤害会拆到头、颅骨、大脑几处，大脑有时只受 7.5 点没死（日志实测：`Cut@Brain:7.5,Crack@Skull:15,Cut@Head:15`），于是“不屈”按“只致倒”正常触发，检查误报。改用原版处决的割喉（一处伤势、必死）。
5. 1.14.0 力能龙：素食对照用的 `victim` 可能已被吐息打死（NRE），改用新生成的对照人；吐息 13 点伤害有时直接打掉手指/脚趾（记为缺失部位而不是伤口），“吐息造成伤害”检查改为伤口或缺失部位任一增加即可。
6. 巫妖断肢重塑（`Checks.cs`）：夹具假设一次 250 刻就能补满最后 250 点进度，只有随机工人的心灵敏感度≥100% 才成立。改为按实际敏感度留出余量。

另有一处我**改过又撤回**的：曾想在“同一刻触发冷却后死亡”时退还冷却（`OncePerCooldown.cs`），但上面第 4 条查明是测试夹具问题，就撤回了，`OncePerCooldown.cs` 与 1.16.0 相同。

## 1. 改动文件

| 文件 | 类型 | 说明 |
|---|---|---|
| `Defs/MindFlayer.xml` | 新增 | 异种型、4 个专属基因、脑髓不足/亡灵之味/群脑/心灵震荡 hediff 与心情、目睹心情、吞噬与心灵震爆能力、HistoryEvent、意识形态议题与 3 条戒律（`MayRequire` Ideology） |
| `Defs/MindFlayerTadpole.xml` | 新增 | 蝌蚪物品、植入/摘除手术、寄生 hediff（5 阶段）与心情、背景“化形者”、关系 `LP_Ceremorph` |
| `Defs/ElderBrain.xml` | 新增 | 上古脑建筑、脑浆罐、召唤/恍惚 hediff、陨灭与挨饿心情 |
| `Defs/MindFlayerPowers.xml` | 新增 | 读心、支配、心灵束缚、念力投掷、灵能隐匿、心灵风暴；支配心理状态；`LP_Unseen`（后备）；抑灵剂、抑制 hediff、半夺心魔基因 |
| `Source/MindFlayer.cs` | 新增 | 脑髓资源基因与资源条、吞噬大脑、心灵震爆、群脑联络、死亡断裂补丁、能力组件 |
| `Source/MindFlayerTadpole.cs` | 新增 | 植入、抗性、5 阶段推进、每日判定、化形、手术 worker、屠宰取蝌蚪补丁 |
| `Source/ElderBrain.cs` | 新增 | 上古脑建筑、传承记忆、脑浆罐与使用效果、召唤 |
| `Source/MindFlayerPowers.cs` | 新增 | 六项灵能、支配目标补丁、半夺心魔与抑灵剂 |
| `Source/MindFlayerChecks.cs` | 新增 | 夺心魔自检（约 170 项，含 5 项存读档） |
| `Source/Core.cs` | 修改 | 三个设置项与设置界面 |
| `Source/Checks.cs` | 修改 | 挂入 `CheckMindFlayer()`、`PrepareMindFlayerSave()`、`CheckMindFlayerAfterLoad()`，结果摘要加一句 |
| `Source/CommonRaceGallery.cs`、`Source/ConstructDesignChecks.cs` | 修改 | 图鉴改成可复用，截图流程末尾加 `MindFlayer.png` |
| `Source/CommonRaceChecks.cs`、`Source/ForceDragonChecks.cs`、`Source/Checks.cs`（重塑那一行） | 修改 | 上面第 0 节的测试夹具修复 |
| `Source/ScenarioChecks.cs` | 修改 | 只给“巫妖开局绑定命匣”检查的失败信息加了诊断字段，逻辑不变 |
| `About/About.xml` | 修改 | 1.16.0 → 1.17.0，描述加一句夺心魔 |
| `使用说明.txt` | 修改 | 顶部新增 1.17.0 条目（面向玩家） |
| `ArtSource/CommonRaces-1.16.0-report.md` | 新增 | 补写 1.16.0 报告 |
| `1.7/Assemblies/LichPhylactery.dll` | 重新编译 | |

备份：`ArtSource/backup-1.16.0/`（改动前的 Checks.cs、Core.cs、CommonRaceChecks.cs、ForceDragonChecks.cs、ConstructDesignChecks.cs、ScenarioChecks.cs、OncePerCooldown.cs、GeneIconGallery.cs、CommonRaces.xml、About.xml、使用说明.txt、旧 DLL）。说明：CommonRaceChecks.cs 和 ForceDragonChecks.cs 的备份是在第 0 节第 1、2 条和第 5 条前半修复之后拍的，其余修复在备份之后；CommonRaceGallery.cs 没有进备份，它只改了几行，删掉 `ShowMindFlayerGallery`/`ShowRaceGallery` 两个包装即可还原。新增文件没有旧版本，删掉即可回退。

## 2. 基因表与代谢

| 基因 | 来源 | 代谢 | 说明 |
|---|---|---|---|
| `LP_Cerebrum` 脑髓代谢 | 自定义，**超凡**（arc 2，cpx 4） | -2 | 脑髓资源条；给吞噬大脑、心灵震爆、支配、念力投掷、灵能隐匿、心灵风暴；有意识形态时代码另给心灵束缚 |
| `LP_MindFlayerSterile` 夺心魔不育 | 自定义 | 0 | `Fertility ×0` |
| `LP_PsionicSense` 灵能感知 | 自定义 | -1 | 心智抗性 +30%，给读心 |
| `LP_HiveLink` 群脑联络 | 自定义 | 0 | 同图本阵营夺心魔每个：心灵敏感度 +10%、全局学习 +5%，封顶 3；同伴死亡“群脑断裂”-8（3 天） |
| `PsychicAbility_Extreme` | 原版 | -5 | 规格里的 “PsychicHypersensitive” 在 1.7 叫这个名字 |
| `Learning_Fast` | 原版 | -3 | |
| `AptitudeStrong_Intellectual` | 原版 | -1 | 智识天赋 |
| `AptitudePoor_Social` | 原版 | +1 | |
| `UVSensitivity_Intense` | 原版 | +4 | |
| `DarkVision` | 原版 | -1 | |
| `Beauty_VeryUgly` | 原版 | +2 | |
| `Hair_BaldOnly` | 原版 | 0 | 规格写 Hair_Bald，1.7 的名字是 Hair_BaldOnly |
| `Beard_NoBeardOnly` | 原版 | 0 | 要点里没有；截图发现男性夺心魔会长胡子，我加上了 |
| `Skin_Purple` | 原版 | 0 | 原版没有“紫灰”，用最接近的紫色 |
| `Delicate` | 原版 | +3 | 体弱 |
| **合计** | | **-3** | 测试实测打印 `mind flayer metabolism total is about -3: -3` |

异种型 `inheritable=false`、`factionlessGenerationWeight=0`，不会随机出现在其他派系；图标全部借用原版（GeneIconGallery 的惯例：自定义基因用原版图标路径）。

## 3. 各阶段实现要点

### 第 1 阶段：异种型、脑髓、吞噬大脑、心灵震爆
- 脑髓 0～100（`Gene_Resource`，紫色条），新夺心魔 60，每天 -8。低于 30/10/0 分三档 `LP_CerebrumHunger`（意识 -5%/-15%/-25%，后两档移动也降；心情 -6/-12/-20）。归零时每小时 15% 进入原版狂暴（理由“脑饥狂乱”），设置可关。
- 吞噬：`Verb_CastAbilityTouch` 3 秒引导。人形 `(20+3×智识)×心灵敏感度`，封顶 80；动物 `3+3.5×体型`，限制在 3～10；6 小时内新鲜人形尸体 ×1/3。吸收目标最高技能 20% 经验（按“自身经验”算，不算天赋基因带来的等级，见第 5 节）。
- 杀死方式：给大脑加 `MissingBodyPart`。测试验证了巨魔被吃掉大脑后 `reviveTick<0` 不复活，尸体大脑为空（巫妖献祭的完整度计算自动降低），没有另写判断。
- 巫妖/死灵造物：可以下口但失败，目标不死，施法者得 6 小时“亡灵之味”（意识 -10%、会呕吐、心情 -5）。机械体、心灵失聪、未倒地、自己：不能吞噬。
- 目睹：12 格内有视线的清醒人形（非夺心魔、非半夺心魔）得 `LP_WitnessedBrainDevour` -25（4 天，可叠 3 层减半），比原版直接吃人肉（-20）重一档；精神变态、嗜血免疫。目标派系好感 -15（原版自己的死亡惩罚另算，实测共 -19）。
- 意识形态（仅加载 Ideology 时注册）：议题“吞噬大脑”，戒律 憎恶（默认权重 1，信徒得知 -4）、可以接受、神圣（吞噬者 +6，目睹心情被抵消）。有 Ideology 时记录 `LP_DevouredBrain` 历史事件。
- 心灵震爆：前方 9 格 35° 锥形，只打敌对；`DNDMind.TryResist`；击晕 4 秒×心灵敏感度（0.25～2.5 截断）+ 1 小时“心灵震荡”（意识 -20%）；10 脑髓，冷却 1 天。

### 第 2 阶段：蝌蚪
- 蝌蚪物品（离开宿主 15 天腐烂消失）。植入手术（暴行，挂在 Human 上，只对成年人形开放）；摘除手术（2 份药品，原版 `Recipe_RemoveHediff`）。
- 寄生 hediff 挂在大脑上，严重度=进度，5 阶段（潜伏/触须/侵蚀/改写/化形）逐级提高心灵敏感度，后期降意识、说话、进食，后 4 阶段有负面心情。
- 每天进度 = (1-心智抗性)×(巨魔 0.5)÷化形天数（默认 5，设置 1～15）。每天一次判定：巨魔 20% 排出、心灵失聪 30% 蝌蚪饿死。火巨人、巫妖、死灵造物、机械体、夺心魔直接免疫（手术会做，但蝌蚪死掉并提示）。
- 化形：先记下各技能自身经验 → `SetXenotype(LP_MindFlayer)`（内源基因全部保留，夺心魔基因作为异种基因）→ 移除非基因特质 → 成年背景换成 `LP_Ceremorphed`“化形者” → 每项技能清零后补回 50% 经验 → 切到植入者阵营（囚犯直接释放并入籍）→ 与植入者建立 `LP_Ceremorph` 关系（好感 +15）→ 脑髓 50。
- “从夺心魔尸体取蝌蚪”：直接做成原版屠宰的附带产物，50% 概率（即第 3 阶段之后的最终数值），没有做第 2 阶段那个 100% 的临时配方。

### 第 3 阶段：上古脑
- 3×3 建筑（暂借死灵仪式台贴图染紫），建好后需要一具夺心魔尸体放在相邻格才苏醒（算第 1 个大脑）。
- 等级按吸收的人形大脑数：1/4/10/20/35 → 1～5 级。只计“本阵营夺心魔在同一地图上吞噬的人形大脑”，动物不算，半夺心魔不算。
- 蝌蚪：1～5 级分别每 5/4/3/2/1 天一条。
- 传承记忆：最多 5 份（满了丢最早的），“灌装脑浆罐”把一份做成物品，只有夺心魔能用，继承 80% 经验。
- 饥饿：原版 `CompRefuelable`，养料=草药/生肉/饭菜，每天 5 份，容量 60。断粮：停产，每饿一天丢一份记忆，同图夺心魔心情 -5。
- 召唤（默认关）：3 级以上每 10 天生成一个无派系流浪者，从地图边缘走向上古脑，到 4 格内倒下（心灵恍惚 12 小时）。
- 被摧毁（`KillFinalize`）：所有本阵营夺心魔 -15 心情 10 天、-30 脑髓，同图的击晕 5 秒；拆除不惩罚（记忆随建筑消失）。

### 第 4 阶段：灵能
| 灵能 | 消耗 | 冷却 | 抵抗 | 效果 |
|---|---|---|---|---|
| 读心 | 3 | 1 小时 | 不判定 | 信件显示特质、前三技能、心情与最在意的 3 条心情；对本殖民地囚犯抵抗 -3 |
| 支配人类 | 25 | 2 天 | `TryResist` | 只能对非本阵营的人形；进入 `LP_Dominated`（沿用原版杀意狂暴的 AI，用补丁把目标换成**原阵营**最近的同伴），30 秒后结束并倒地（心灵恍惚 12 小时） |
| 心灵束缚 | 20 | 1 天 | `TryResist` | 仅 Ideology 时由代码授予；信念 -40%，归零改信施法者的意识形态 |
| 念力投掷 | 8 | 6 小时 | 不判定 | 沿施法方向甩出最多 4 格，10 钝伤，踉跄 1 秒 |
| 灵能隐匿 | 15 | 1 天 | — | 原版 `PsychicInvisibility` 1 小时 |
| 心灵风暴 | 40 | 5 天 | `TryResist` ×0.5 | 周围 8 格敌对目标击晕 6 秒 + 心灵震荡 |

关于“有 Royalty 用原版隐形，否则自写 LP_Unseen”：查了 1.7 的数据，`PsychicInvisibility` 和 `HediffComp_Invisibility` 都在 **Core** 里，不需要 Royalty。所以始终用原版隐形；`LP_Unseen` 仍然定义了，只在原版定义缺失时作为后备。

### 第 5 阶段：半夺心魔
- 抑灵剂（制药台，需“药物生产”研究；3 中性胺+1 草药）。服用刷新 3 天药效（`LP_PsionicSuppression`）。
- 化形进度 <50% 时服药：冻结进度，并加上异种基因 `LP_HalfIllithidGene`（给读心、吞噬大脑）。≥50% 时服药无效。
- 半夺心魔吞噬不需要也不获得脑髓；每吞噬一个人形大脑依次解锁心灵震爆、念力投掷、灵能隐匿（最多 3 项）；灵能免费，只受冷却限制；目睹吞噬不难过。
- “每次 25% 概率抑制失效”：实现为**每吞噬一个大脑**25% 概率移除抑制（测试 51/200）。
- 断药 3 天（药效结束）后进度恢复；最终化形时异种基因被替换，半夺心魔基因随之消失。
- 我额外加了一条：蝌蚪被摘除/排出/饿死后，半夺心魔基因在一小时内移除（否则“吃药→摘蝌蚪”就能白拿永久灵能）。

## 4. 测试结果

全部用 pwsh 7 运行 `Source/Build.ps1` 与 `Source/Test.ps1`。构建：`PASS: LichPhylactery built against installed RimWorld 1.7 and Harmony.`

**各阶段（Core 档，每阶段完成时）：**

| 阶段 | 结果 |
|---|---|
| 1.16.0 接手时 | `PASS: 1237 checks` |
| 第 1+2 阶段 | `PASS: 1343 checks`（第 1 阶段先单独跑过，暴露了经验到账和阶段标签两个问题，修好后与第 2 阶段一起通过） |
| 第 3 阶段 | `PASS: 1368 checks` |
| 第 4+5 阶段 | `PASS: 1405 checks` |
| 最终 | `PASS: 1411 checks`（夺心魔自检约 173 项，其中存读档 5 项：脑髓与饥饿、蝌蚪进度与植入者、上古脑、半夺心魔） |

**最终矩阵（全部修复之后）：**

| 档 | 结果 |
|---|---|
| Core | 最后连续 6 次 6/6 `PASS: 1411 checks`；`MindFlayer.png` 截图正常（已复制到 `ArtSource/Preview/MindFlayer-1.17.0.png`） |
| Appearance | 3/3 `PASS: 1611 checks` |
| Warcasket | `PASS: 1462 checks` |
| VisualCompat | `PASS: 11210 visual checks` |
| HealthCompat None / Human / Mythic | `PASS: 286 / 269 / 267` |
| HealthCompat Both、HealthCompat Full、VisualCompat Full | 本模组检查全部 PASS（271 / 271 / 11210），脚本因日志里的第三方报错退出码 1：`Config error in HA_Category / MB_Category`（HumanAptitudes 与 MythicBloodlines 分类优先级冲突）和 `Error in static constructor of GD3.HarmonyInit`。日志里没有任何与 LP_/LichPhylactery/夺心魔相关的警告或报错 |
| Full | FAIL，每次停在**力能龙/火巨人**的不同位置（如 `wyrmling scales give 30% sharp…`、`overdrive applies real damage…`、`native ingredient hauling… LP_FireGiantMaul`），都在夺心魔检查之前 |

**Full 档与本次无关的验证：** 我把 DLL 换回 `backup-1.16.0` 里的 1.16.0 版本、并把 4 个夺心魔定义文件临时移走，跑了 Core×5 和 Full×1：Core 5/5 通过（1237），Full 同样失败（`FAIL after 161 checks: overdrive applies real damage and move stat multipliers`）。跑完已恢复（DLL 哈希比对一致，4 个定义文件都已放回）。

**测试稳定性（重要）：** 中途 Core 档一度有约三分之一的概率在**老检查**里失败（巫妖重塑、死灵造物、冰霜射线等）。原因查明是**我的夺心魔测试没打扫干净**：被杀/被摧毁的角色和上古脑会把身上的物品、上古脑里的养料、拆除返还的材料掉在地上（实测一次清出 13 件：钢铁、零部件、草药、钢渣块、大麻烟、强化剂、仙馔蜜、棍棒），后面的巫妖工作单和造物测试会去用这些东西。现在夺心魔检查开头给地图拍快照，结束时清掉所有新出现的物品并恢复派系好感，再加一项检查确认没有遗留。之后又修了几个随机夹具（见第 0 节和下面）：

- 我的检查：随机角色可能带“心灵失聪”特质（吞不了、蝌蚪会饿死）、带“精神变态/嗜血”特质（不会被吓到），随机出生点可能在墙边挡住视线；灵能测试原来要求 13×13 空地，有的地图没有。现在都改成重新生成或固定站位。
- 一个真正的逻辑问题（测试抓到的）：化形时先换异种型再删特质，夺心魔的“体弱”基因会压制原有的“坚韧”特质，被压制的特质删不掉。改成先删特质再换异种型，并加了回归检查。
- 仍会偶发的老问题：1.14.0 “重塑中途存读档的伤势比例”检查（`ratio=0.0926 expected=0.068`）在最终矩阵的 Core 里出现过 1 次，这是 1.15.0 报告里就记录过的既有偶发问题，与夺心魔无关；我没有为了变绿去改它。

## 5. 与构思的差异 / 我自己做的取舍

1. 需求原文缺失，按要点实现（见开头）。
2. 原版基因名按 1.7 实际名字：`PsychicAbility_Extreme`、`Hair_BaldOnly`、`Skin_Purple`（没有紫灰色）。
3. 屠宰取蝌蚪直接是 50%，没有做临时 100% 配方。
4. 灵能隐匿始终用原版隐形（原版在 Core 里）。
5. 动物尸体不能吞噬（只有倒地的活动物）；尸体只给 1/3 脑髓，经验吸收不打折。
6. 经验按“自身经验”计算：原版技能等级会叠加天赋基因（夺心魔的智识天赋），直接用 `XpTotalEarned` 会把天赋等级也算进去（开发中实测出现过“给了 11000 只到账 10000”）。现在吸收、化形保留、传承记忆都只算角色自己学到的经验；化形后新异种型的天赋照常叠加在显示等级上。
7. 支配人类沿用原版杀意狂暴的行为；30 秒后倒地用的是“心灵恍惚”12 小时（与上古脑召唤共用）。
8. 群脑联络、读心、念力投掷、心灵风暴、心灵束缚的具体数值是我定的（要点里只给了冷却）。
9. 目睹心情 -25（原版直接吃人肉 -20），“比吃人重一档”按这个理解。
10. 蝌蚪只能寄生成年人（儿童没有成年背景槽，化形会出问题）。
11. 上古脑只计人形大脑；初始化的那具尸体算第 1 个。
12. 没有做任何恐惧、复活或再生效果。

## 6. 需要你决定的点

1. **要点之外的原文细节**：`MindFlayer-spec.md` 不在 ArtSource 里，如果原文有要点没列的内容，请把文件放进来，我对照补齐。
2. **灵能是否要按上古脑等级解锁**：现在每个夺心魔一出生就有全部 6 项灵能（只受脑髓和冷却限制）。
3. **数值**：目睹 -25、好感 -15、支配 25 脑髓/2 天、风暴 40 脑髓/5 天、上古脑每天 5 份养料，需要你确认手感。
4. **意识形态戒律的默认权重**：“憎恶”权重 1，新生成的意识形态大多会带上它（信徒得知有人被吞噬 -4）；要不要改成默认不出现。
5. **25% 抑制失效**的含义：我做成“每吞噬一个大脑”；如果原意是“每次服药”，改一行即可。
6. **蝌蚪摘除后取消半夺心魔**是我加的规则，如果你希望半夺心魔永久保留，删掉 `Gene_HalfIllithid.CheckHost` 即可。
7. **美术**：上古脑、蝌蚪、脑浆罐、抑灵剂都借用现有贴图染色；夺心魔外貌是原版无发+紫色皮肤，没有章鱼头/触须。截图见测试档目录 `MindFlayer.png`。
