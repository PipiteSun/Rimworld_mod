# 1.102.0 六臂蛇人 / 双头巨人 / 独眼巨人：HAR 种族 → 异种型

日期：2026-11-03。备份：`ArtSource/backup-1.101.0-xenotypes`（改动前的源码、整个 `Bridges/HAR`、About.xml、使用说明.txt、旧 dll）。

## 做法（参考 Alpha Genes）
Alpha Genes 不换身体：特殊外形用基因的 `renderNodeProperties`（挂在 Body / Head 上的贴图节点），功能用基因的统计、能力、状态（VEF `hediffsToBodyParts`）。这里照做：

- 三族 = 原版 `Human` + 异种型 `LP_MarilithKin` / `LP_EttinKin` / `LP_CyclopsKin`，定义移到主模组 `Defs/Marilith_Xenotype.xml`、`Defs/HARGiants_Xenotypes.xml`、`Defs/HARGiants_Items.xml`，贴图移到主模组 `Textures/Marilith`、`Textures/HARGiants`。装不装 HAR 都加载。
- 本源基因带原来种族上的东西：新基因 `LP_MarilithCore`（蛇身本源），以及原有的 `LP_EttinCore` / `LP_CyclopsCore`。上面挂 `RaceSizeExtension`（体型、渲染、胃容量；以前挂在种族 ThingDef 上）、`bodyType Hulk`、`forcedHeadTypes`、统计差值（移速、舒适温度、学习等）、`marketValueFactor`，以及渲染节点。
- 渲染节点：`Source/XenoParts.cs` 的 `PawnRenderNodeProperties_LPPart` / `PawnRenderNode_LPPart` / `PawnRenderNodeWorker_LPPart`。偏移按 HAR 附件换算（HAR 的 (x,y)+layerOffset，前面 +0.3、后面 -0.3，东向 x 取反）。蛇人另把身体贴图换成 `Marilith/Bodies/Naked_*`（`MarilithBodyGraphic`），双头巨人主头照 HAR 的 headOffsetDirectional 挪开（`EttinHeadOffset`）。
- 体型：`RaceSize.Of(pawn)` 先看种族、再看生效基因（按角色缓存，基因变化失效）；`RaceSizeBodySize` 在 get_BodySize 上乘基因那一项。

## 规则上的变化（身体只剩人类的部位）
- 六臂蛇人：额外四条手臂挂在两侧肩膀上——一侧肩膀缺失或是义肢，那侧两条额外手臂没了（操作 +0.1/手，副手对数 = 完好的侧数）。只盖腿脚的衣服穿不上（`MarilithNoLegwear`）。蛇人兵种 85% 女性（`MarilithGender`）。人类的腿当作蛇尾。
- 双头巨人：第二个头不是部位。主头所在的颈 / 头 / 大脑缺失时（`CheckForStateChange` 前缀 + `Pawn.Kill` 前缀保险），清掉那个颈以下的伤口和缺失、加“孤首”。两个头都在时意识下限 60%。夺心魔吃大脑同理。
- 独眼巨人：人类两个眼部位当一只眼，视力取最差的；任一缺失就失明、额头的眼睛不画。
- 去掉：HAR 的开局 / 流浪者 / 难民随机权重（现在开局在人物编辑里选异种型）；独眼的眼部手术补丁（直接是原版眼睛）。

## 旧存档
`LegacyRaceSave`：`BackCompatibility.BackCompatibleDefName` 把 `LP_Marilith/LP_Ettin/LP_Cyclops`（和 `Corpse_*`）读成 `Human`/`Corpse_Human`。`Pawn.ExposeData` 的 PostLoadInit 后缀把挂在旧身体（保留在 `Defs/Legacy_RaceBodies.xml`）上的伤口、缺失、义肢、手术单，按“每层部位定义 + 同定义序号”搬到人类身体（独眼 → 左眼），没有对应的（额外手臂、蛇尾、第二个头）丢掉；第二个头缺失 → 孤首；旧蛇人补蛇身本源。Peter 的 Autosave-3 里有一个 HAR 独眼巨人（LP_Cyclops19368）。没有用真实旧存档跑过读档（测试脚本不能指定存档），只做了部件映射和转换函数的自检。

## 自检
- `Test.ps1 -Only Marilith`：19 条通过（含 HAR 联动 1 条、旧存档转换 1 条），截图 Marilith-*.png 正常（六臂、蛇尾、无腿上半身，断左肩只剩一侧手臂）。
- `Test.ps1 -Only GiantKinHAR`：14 条通过，截图 HARGiants-*.png 正常（双头、双盔、孤首居中、独眼、缺眼不画）。
- `Test.ps1 -NoHAR -Only Marilith,GiantKinHAR,RedHawk,Abyss`：104 条通过；`-Only RedHawk,Abyss,Backstories,GiantKinHAR`：95 条通过；日志无红字。
- 测试里改了几处与本次无关的随机性：头部改成一击打掉（多次小伤会先打坏眼睛并累积到全身致死，原版死法）；轮流值守的敌人放到 10 格内；断肩后原版会扔下主手武器，测试里重新装上。
- 已知：`-Only AdventurerStage5` 在第 1 条（人类 / 精灵的 Adventurer.Get 为 null，AdvAt 空引用）就失败，发生在本次改动的段落之前，那一带是另一个线程正在改的职业代码，没有动。偶发：缠绕挤压被护甲挡成 0（随机护甲），重跑通过。
