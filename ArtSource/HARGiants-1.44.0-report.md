# DND边缘大陆 1.44.0 报告：双头巨人、独眼巨人（Humanoid Alien Races 可选联动）

需求：`ArtSource/HARGiants-1.44.0-spec.md`。备份：`ArtSource/backup-1.43.0/`（本次改动过的原文件 + About.xml + 使用说明.txt + LoadFolders.xml）。版本 1.43.0 → **1.44.0**（About.xml `modVersion`）。开工时 RimWorld 没在运行。

## 一、改动文件

新增：
| 文件 | 内容 |
|---|---|
| `Bridges/HAR/Defs/HARGiants_Race.xml` | 两个 HAR 种族（`LP_Ettin`、`LP_Cyclops`，`ParentName="Human"`，列表 `Inherit="False"`）、双头巨人主头偏移与 8 个身体附件（第二个头 3 款 + 头发 3 款 + 双盔另一半 + 儿童版）、独眼附件、独眼巨人 4 个 HeadTypeDef、6 个 PawnKind（两族殖民者、双头蛮兵、流浪的双头巨人、独眼巨人牧人、独眼巨人护卫）、RaceSettings（权重 3）、剧本「山脚的弃儿」 |
| `Bridges/HAR/Defs/HARGiants_Bodies.xml` | `LP_EttinBody`、`LP_CyclopsBody`、`LP_EttinHead`、`LP_CyclopsEye`、部位组 `LP_EttinSecondHead`（由 `ArtSource/HARGiants-gen_body.py` 从原版 Human 身体生成） |
| `Bridges/HAR/Defs/HARGiants_Genes.xml` | 异种型 `LP_EttinKin`、`LP_CyclopsKin`，12 个基因，孤首 hediff + 心情，内讧精神状态 + 轻度崩溃，羊群心情 |
| `Bridges/HAR/Defs/HARGiants_Items.xml` | 双盔、独眼巨锤（钢/木两张配方）、独眼透镜 |
| `Bridges/HAR/Patches/HARGiants_Patches.xml` | 眼睛手术加上独眼；内讧的思考树节点（原地站着，饿极困极照样吃睡） |
| `Bridges/HAR/Textures/HARGiants/…` | 52 张：第二个头 3 款×3 向、头发 3 款×3 向、双盔穿戴 3 向 + 第二个头附件 3 向、独眼巨人头 4 款×3 向、独眼 3 向（北向空图）、物品 3、基因 8、异种型 2 |
| `Source/HARGiants.cs` | 全部规则（主程序集，不引用 HAR） |
| `Source/HARGiantsChecks.cs`、`Source/HARGiantsGallery.cs` | 检查组 `GiantKinHAR`、四向截图 |
| `ArtSource/RaceArt/draw_hargiants_144.py`、`ArtSource/HARGiants-gen_body.py`、`ArtSource/HARGiants-gen_addons.py` | 贴图脚本、身体生成、附件偏移生成 |
| `ArtSource/HARGiants-1.44.0-spec.md`、`-report.md`、`-preview.png`、`-ingame.png` | 需求、报告、贴图预览、游戏内四向截图 |

修改（原文件在 backup-1.43.0）：
| 文件 | 改动 |
|---|---|
| `Source/Marilith.cs` | `MarilithExtension` 改为继承通用的 `RaceSizeExtension`；副手连击改为通用的 `OffhandExtension`（挂在基因上，六臂连击 max 2 按额外手臂对数，左右开弓 max 1 要双头身体和两只手）；根节点渲染缩放改为所有带 `RaceSizeExtension` 的种族通用 |
| `Bridges/HAR/Defs/Marilith_Genes.xml` | 六臂连击挂 `OffhandExtension`（max 2、perExtraArmPair） |
| `Source/BodySize.cs` | 巨躯体型里的种族一项改为 `RaceSize.SizeFactor` |
| `Source/Satiety.cs` | 胃容量 F 再和种族扩展的 `stomach` 取较大值 |
| `Source/GiantKin.cs` | `GiantGear.CanWear/WearReason` 认 `GeneWearExtension`（透镜只有独眼巨人能戴）；阶序设置说明加双头、独眼 |
| `Source/GiantMigration.cs` | 两种新队伍、权重、讨食（双头 ×4）、礼物、信件文字 |
| `Source/Factions.cs` | 塞伊双头蛮兵、同盟商队独眼巨人护卫 |
| `Source/MindFlayer.cs` | 吞噬两头都在的双头巨人：只毁掉那颗大脑所在的头，不杀 |
| `Source/LichSpells.cs` | `Frighten` 跳过独眼巨人本源（凝视免疫） |
| `Source/LimbHealth.cs` | 巫妖强化肢体的主要部位加 `LP_EttinHead`、`LP_CyclopsEye` |
| `Source/Checks.cs` | 完整检查加 `CheckGiantKinHAR`；`-Only GiantKinHAR` 通过后截图 |
| `Bridges/HAR/Source/BridgeChecks.cs` | `RunGiants`（一条合并的 HAR 类型检查） |
| `Bridges/VPE/Source/Artificer.cs` | 独眼巨锤 → 器械师「奥术工坊」 |
| `About/About.xml`、`使用说明.txt` | 1.44.0、描述；标题、可选联动、内容一览、剧本、【1.44.0 双头巨人、独眼巨人】、更新日志 |

## 二、数值

| 项 | 双头巨人 LP_Ettin | 独眼巨人 LP_Cyclops |
|---|---|---|
| 体型（种族 baseBodySize） | 2.0 | 1.8 |
| 渲染（根节点） | ×1.4 | ×1.35 |
| 巨躯规则（默认强度） | 生命 ×1.5、近战 ×1.3 | 生命 ×1.4、近战 ×1.24 |
| 胃容量 F | 180% | 170% |
| 移速 / 学习 | 4.0 / ×0.85 | 4.2 / ×0.85 |
| 阶序 | rank 5（element ettin） | rank 6（element cyclops） |
| 代谢 | 本源 -2、值守 -1、开弓 -1、不和 +2 = -2 | 本源 -2、匠心 -1、牧者 -1、视界 +2 = -2 |
| 生成权重 | 开局/流浪者/难民各 3 | 同左 |

其他：孤首（视力 ×0.75、心情 -8）；意识下限 60%×剩下那颗大脑的效率；轮流值守 RestFallRateFactor ×0.5、10 格 250 刻；内讧 2500 刻、轻度崩溃权重 0.5（同原版悲伤游荡）、每 15 天 10%（心情 >70% 不触发）；单眼视界 -3.5 点（透镜 -1.75）、AimingDelayFactor +0.1；匠心 ConstructionSpeed ×1.2、锻造类配方 GeneralLaborSpeed ×1.15；牧者 TameAnimalChance/TrainAnimalChance/AnimalGatherYield ×1.25；独眼 hitPoints 15（人类眼 10 ×1.5）。
物品：双盔 锐 0.50 / 钝 0.25 / 热 0.18、HP 130、钢 70、mood 2；独眼巨锤 钝 32 / 3.0 秒、15% 晕 60 刻、钢 100 或木 150；独眼透镜 玻璃钢 10 + 金 5。
出场：迁徙两种队伍各 8%；双头蛮兵 combatPower 160、≥800 点 10%；独眼巨人护卫 10%。

## 三、我自己做的决定

1. **体型只走种族一个来源**：两族本源基因的 `GiantKinExtension` 写 `adultSize 1 / renderScale 1`，所以 `GiantKin.Size`、`FireGiantBodySize`、`FireGiantRenderSize` 这几处取最大值都是 1，不会和种族 2.0/1.8 叠乘（检查 12 核对：BodySize 2.0/1.8、GiantKin.Size 1、生命 1.5/1.4、渲染 1.4/1.35、胃 1.8/1.7）。巨躯规则、渲染、胃容量改为读种族上的通用 `RaceSizeExtension`（`GiantRaceExtension` 继承它，六臂蛇人的扩展也继承它）。
2. **儿童体型选了“HAR 生命阶段 + GiantKin 成长曲线”的分工（同六臂蛇人）**：真实 BodySize 由原版人类生命阶段缩小；巨躯规则、渲染、胃容量用 `GiantKin.Growth`（18 岁长足）。
3. **第二个头是独立的 BodyPartDef `LP_EttinHead`**（不是第二个原版 Head）：这样原版“有没有头”（`HasHead`）只看主头，主头没了不会再画一个主头；第二个头的所有子部位（头骨、大脑、眼耳鼻颚舌）复用原版 BodyPartDef，眼耳颚手术两个头都能选。第二个头的部位组只有 `LP_EttinSecondHead`，普通帽子盖不到。两个颈都用原版 Neck（呼吸/进食/说话通道）。
4. **双脑规则（反编译 1.7 核对）**：原版 `ShouldBeDead` = 致死能力（意识）失能 / 核心部位效率 0 / 致死伤害阈值；意识 = `CalculateTagEfficiency(ConsciousnessSource)` 两脑平均 − 疼痛，再乘血液循环等。所以原版下丢一个头意识只剩 50%、不会判死；两个头都没了是 0 才死——死亡判定不需要改。只在 `CalculateTagEfficiency` 加后缀：意识来源取 max(原版, 60%×剩下那颗大脑效率)；其他标签（视力、听力、进食、说话、通道）在失去一个头后只算剩下的那个头。没有加改写死亡的 Harmony。
5. **“没了一个头”的定义**：那个颈、头或头里的大脑缺失都算（大脑被打烂但头还在也给孤首）。孤首在头恢复（比如再生）时会去掉。
6. **轮流值守 / 左右开弓对人类无效**：值守要“双头巨人身体且两个头都在”，开弓要“双头巨人身体且两只手都能用”，描述写明“需要两个头”“需要两个头各管一只手”。
7. **内讧**：自建 `MentalState_EttinArguing` + 思考树补丁（`JobGiver_StandAndBeSociallyActive`，站在原地，每 300 刻冒一句吵架的话）；15 天判定的时间存在基因上，第一次在 0～15 天内随机，免得一群双头巨人同一天吵。
8. **单眼视界的“-30%”按原版点数折算为 -3.5 点**：原版射击精度是“点数”再查曲线（触发欢 -5、谨慎 +5）。射击 10 级（0.97/格）-3.5 点 ≈ 0.953/格，20 格命中率 ×0.70；透镜 -1.75 点 ≈ ×0.85。用 StatPart 实现（原版在 FinalizeValue 里先套 StatPart 再查曲线）。
9. **独眼是单独的 BodyPartDef `LP_CyclopsEye`（hitPoints 15）**，再用补丁把它加进所有 `appliedOnFixedBodyParts` 含 Eye 的配方（仿生眼、阿凯奥特眼、摘除/致盲……包括其他模组的）。检查 8 核对仿生眼能装。
10. **独眼画法**：独眼巨人头型贴图只画脸型和粗眉骨（按肤色染色），眼睛是跟着头走的 HAR 身体附件（本色、不染肤色），独眼缺失或戴了遮住整个头/眼睛的头盔时不画。
11. **独眼巨人头型**：HeadTypeDef 带 `requiredGenes LP_CyclopsCore`（原版人类随机不到）；原版挑头时种族基因还没加上，HAR 的交集会变空、头型为 null（第一次截图时就是这样），所以生成后（和出生、生成到地图时）在代码里补一个同性别的独眼头；HAR 的头路径指到 `HARGiants/Cyclops/Heads/`。
12. **第二个头的贴图要有眼睛**：1.7 的原版头有黑点眼睛（导出参考贴图核对），第二个头照这个画法画了眼睛；体型按导出的原版头框（128 里 x 32～68、y 30～70）。
13. **附件层级与偏移（截图核对过四向）**：南北两头并排 ±0.26；第二个头画在身体和衣服之上、主头之下（`layerOffset -0.283`）；东向右头在近处（前、低，画在最前），西向在远处（后、高，画在后面）。HAR 默认 `layerInvert` 会在北向把附件翻到身体后面，已关掉；HAR 对东向偏移（附件和主头的 `headOffsetDirectional`）会把 x 取反，东向的 x 反着写。
14. **双盔**：不是材料做的（固定钢 70），所以主头的穿戴贴图和第二个头的附件都用钢铁本色，不随材料变色。只有巨人能戴；别的巨人戴只用得上主头那一顶（它们没有第二个头）。
15. **迁徙**：两种新队伍挂在费伦拓荒同盟名下（好感、礼物记到同盟）；“各占 8%”做成新队伍权重 = 8% ÷ (1 − 2×8%) × 其余可用队伍的权重和，原有队伍的权重常数不动（不装 HAR 时和以前完全一样）。双头巨人“讨食营养 ×2”理解为比普通巨人再翻倍（两张嘴）：Appetite 4（普通巨人 2）；礼物给一顶双盔（规格没写双头巨人的礼物）。
16. **器械师门槛**：报告里巨人武器大多在「符文铭刻」，但和独眼巨锤最像的霜巨人斧（钢制、巨人专用近战）在「奥术工坊」，所以独眼巨锤跟霜巨人斧放「奥术工坊」。双盔、透镜不设门槛（报告里衣服/饰品都不设）。
17. 双头蛮兵和独眼巨人护卫的武器由代码发（独眼巨锤钢 / 棍棒木各一半），蛮兵另戴双盔。蛮兵在塞伊所有袭击处理的最后一步插入，免得被前面霜巨人/蛇人的“顶掉普通随从”误删。
18. 两族都不在术士血脉里（同火巨人、霜巨人，不是魔法血统）。

## 四、与要求的差异

1. **“意识下限 60%”是加在大脑那一项上的**（原版再扣疼痛、乘血液循环）：刚丢一个头时伤口疼，整体意识会低于 60%；疼痛下去后是 60%。检查 3 在去掉伤口疼痛后核对正好 60%。我认为这样比“最终意识不低于 60%”更符合“两脑平均但有下限”的写法，也不会让疼痛失效。
2. **孤首视力 ×0.75 乘在“剩下那个头的视力”上**（剩下的两只眼按常人算，所以完好时是 75%）。如果完全照原版四只眼平均，丢一个头后视力会先掉到约 83% 再 ×0.75 只剩 62%，进食、说话也会因为两张嘴平均而减半，太重，所以失去一个头后视力/进食/说话只看剩下的头。
3. **内讧不发精神崩溃信件以外的特殊效果**：只是站着吵、不干活；“加入轻度崩溃候选”照做（权重 0.5，同原版悲伤游荡）。
4. **睡眠中被惊醒**没有做游戏内自动测试（检查 5 只核对了“10 格内有敌人能被察觉”和睡眠下降），惊醒走原版 `RestUtility.WakeUp`。
5. **儿童的第二个头**用成人贴图缩到 0.8 倍、偏移按比例缩，只做了截图里的大致对位（儿童没有第二个头的头发）。
6. **深水**：两族都不涉及，未改。
7. 规格写“手工台”：用了手工制作点和机械加工台。

## 五、兼容结论（第五节逐项）

| 系统 | 结论 |
|---|---|
| 巨躯规则 / LimbHealth | 种族体型进巨躯规则（生命 1.5/1.4、近战 1.3/1.24，检查 12）；部位生命按 HealthScale，第二个头、独眼都吃倍率；巫妖“强化肢体”的主要部位加了这两个部位 |
| 巨人衣服 / 武器 / 合身心情 | `GiantKin.Of` 认两族（检查 11：rank 5/6，双盔、锻工外衣能穿，人类穿不了）；巨人武器、`LP_GiantAttire` 心情按 GiantKin 通用 |
| 阶序 | rank 5/6 进入高低位判断（检查 11：双头“统领众巨”、独眼“阶序井然”）；氏族之首按 element 同种计算 |
| 六臂蛇人副手 | 改成通用 `OffhandExtension` 后 `-Only Marilith` 19 条照旧 PASS |
| 夺心魔吞噬大脑 | 两头都在的双头巨人：只毁掉那个头，另一个活（孤首，检查 3）；只剩一个头或尸体照原版 |
| 蝌蚪 | 化形只 `SetXenotype` 并补回内源基因，种族/身体不换（代码核对，未改） |
| 巫妖仪式、神选 | 没有种族门槛，身体保留（代码核对，未改） |
| 泰坦觉醒 | 只对天穹神裔（`IsPlainTitan`：泰坦异种型）；两族不是泰坦，不能走这条路，觉醒本身也只换异种型、不换 HAR 种族/身体——两族不涉及，未改 |
| Isekai | `-Isekai -Only GiantKinHAR` 15 条 PASS（两族都挂 Isekai 组件） |
| 背景特性 / IKM 特性 | HAR 不限制两族（HAR 联动检查），`allowHumanBios` 开 |
| VPE 职业 | 没有种族门槛，同六臂蛇人；不在术士血脉里。没跑 `-VPE`（规格只要 -Isekai） |
| 原版手术 | 仿生眼能装独眼（检查 8）；双头巨人两个头的眼耳颚都是原版部位，手术能选（`humanRecipeImport`） |
| 没有 HAR | `-NoHAR -Only GiantKinHAR,Marilith,Giants` 35 条 PASS，日志无红字（种族、物品、剧本、补丁都不加载；独眼巨锤的器械师条目按名字查不到时跳过） |
| 剧本说明 | Giants 组“所有剧本描述能显示”现在是 20 个（含山脚的弃儿） |

## 六、测试

| 命令 | 结果 |
|---|---|
| `pwsh Source/Build.ps1` | 主程序集、HAR、VPE、神兽血裔联动全部 PASS |
| `Test.ps1 -Only GiantKinHAR` | **PASS 14 条**（主程序集 13 + HAR 联动 1），日志无红字；通过后截了南/东/北/西四张图 |
| `Test.ps1 -Only Marilith` | **PASS 19 条** |
| `Test.ps1 -Only Giants` | **PASS 33 条**（剧本说明 20 个） |
| `Test.ps1 -NoHAR -Only GiantKinHAR,Marilith,Giants` | **PASS 35 条**，无红字 |
| `Test.ps1 -Isekai -Only GiantKinHAR` | **PASS 15 条**（多一条 Isekai） |
| 完整测试 | 按规格没跑 |

GiantKinHAR 14 条：①两族内容只在启用 HAR 时加载 ②HAR 联动（附件绑部位、异种型限制、特性/背景不受限、开局/流浪/难民权重 3、能和人类生育）③双头身体（2 颈 2 头 2 脑 4 眼、肤色基因）④真伤害打烂右头不死：孤首、意识 60%、视力 75%、进食=意识；夺心魔吞一颗大脑只毁一个头 ⑤两个头都没了才死 ⑥轮流值守 ×0.5、人类无效、察觉 10 格内敌人 ⑦左右开弓 1 把（挑好的）、人类 0、断一只手 0 ⑧内讧（轻度崩溃，人类不会）⑨独眼（唯一视力来源、没了失明、仿生眼能装、有独眼头型）⑩单眼视界 -3.5 / 透镜 -1.75 点、瞄准 ×1.1、人类戴不了透镜 ⑪凝视免疫 ⑫巨人、阶序、双盔/锻工外衣、帽子盖不到第二个头 ⑬体型 2.0/1.8 未叠乘 ⑭剧本说明。

调试中修掉的问题：剧本 `customSummary` 不是 KindDefs 组件的字段（XML 红字）；独眼巨人头型为 null（见决定 11）；第二个头盖住主头、北向被翻到身体后面、东向两头重合（见决定 13）。

已知旧不稳定项本次都没有出现。

## 七、截图

`ArtSource/HARGiants-1.44.0-ingame.png`（左上南、右上东、左下北、右下西）。上排双头巨人：裸身、牛仔帽、双盔、没了右头、没了左头、儿童；下排独眼巨人：裸身、牛仔帽、简易头盔+透镜、没了独眼、儿童，和一个人类对照。贴图预览 `ArtSource/HARGiants-1.44.0-preview.png`。

## 八、提醒 Peter

在模组列表启用 **Humanoid Alien Races**（创意工坊 839005762），放在 DND边缘大陆 **之前**，然后**完全重启游戏**。开局在人物编辑的“角色种类”下拉框里选“双头巨人殖民者”“独眼巨人殖民者”，或者选剧本“山脚的弃儿”（建议丘陵）。需要看的：第二个头在各种帽子/发型下的对位、儿童的第二个头、内讧的实际表现、睡着时被敌人惊醒。
