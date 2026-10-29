# 1.60.0 迷锁地牢 报告

日期：2026-10-28。设计依据：`dnd-genes/DND地牢构思二.md`（丙线）。Peter 说“你可以自行决定内容”，下面的取舍都是线程自己定的。

## 做了什么

- **迷锁之门** `LP_MythalGate`（`Building_MythalGate : MapPortal`）：原版口袋地图入口。核实过：`MapPortal`、`PocketMapExit`、`PocketMapUtility.GeneratePocketMap/DestroyPocketMap`、`WorldObjectDef PocketMap`、`BiomeDef Underground` 都在本体（Assembly-CSharp / Data/Core），IL 里没有 `AnomalyActive` 判断，所以不需要异象，不用退回世界地点方案。
- **状态机**（全部存在门上，门每 250 刻处理一次）：沉睡 → 开启 → 已解开 → 坍塌 → 封闭（默认 3 天）→ 开启下一层。封闭时没有口袋地图。
- **楼层生成** `GenStep_MythalFloor`（`MapGeneratorDef LP_MythalFloor`）：70×70（30 层起 90×90），3×3 / 4×4 网格房间，随机生成树 + 额外通道，走廊宽 3 格；整图厚岩顶 + 迷锁石墙（连接图集）+ 每间一盏迷锁灯。入口房放出口；守护者房 = 房间图上离入口最远；每 3 层一间迷锁泉；1～2 间宝藏房。
- **怪物**：远古敌对派系，每间房一个 `LordJob_DefendPoint`；点数 =(150+35×层)×难度，守护者 30%（每 5 层 +20%）；前 5 层不超过殖民地袭击点数。人形怪冒险者等级下限 = 层 ÷ 3（守护者 +3 走 `AdventurerEnemies.Story`，里程碑 +6）。
- **8 个主题**（`MythalThemeDef`，XML 可扩展）：狗头人矿坑、地精营地、猫头鹰熊林窟、亡者墓穴、红袍法师塔、龙教巢穴、幽暗地域、深渊裂隙。兵种用名字引用，不存在（没 HAR）时跳过。
- **里程碑首领**（每 10 层，`Mythal.Bosses`）：10 青年五色龙 / 力能古龙、20 红袍法师、30 死亡骑士 / 骷髅、40 成年五色龙 / 力能古龙、50 眼魔 / 夺心魔、60 炎魔 / 赫兹魔；70 起轮回，碎片 ×2。额外 `Adventurer.Milestone(p,"LP_Mythal"+层)`。
- **7 条迷锁规则**（`MythalRuleDef` + `Hediff_MythalRule`）：无、重力、狂暴、生命回流、时间凝滞、破碎（8 层起）、祝福（只我方）。第 1～4 层无规则。状态离开迷锁地图自动消失。
- **奖励**：通关经验 = 危险度 × 0.5 × 经验倍率（按 `Adventurer.GainXP`，待满 1 小时或在场全额，否则 25%）；守护者房掉 5+层/5 碎片、卷轴、白银、主题战利品、魔法物品（概率，里程碑必掉）；出现回程门（同一个出口 Def 再接一扇，原出口链接不变）。
- **迷锁碎片** `LP_MythalShard`（商人不收）与**门上兑换**（9 项，含重铸下一层规则）。
- **撤退回卷**：无人进入 5 天 → 地图销毁，同层下次重新生成。**全员倒地** 6 小时 → 吐回门口（活着，思想 `LP_MythalExpelled` −6），该层回卷；已解开的层照常坍塌。
- **Isekai**：危险度 × 60%（设置 30%～100%）；通关经验反射调用 `IsekaiLeveling.IsekaiComponent.GainXP(int,string)` 同额给 Isekai（设置可关，找不到方法就只给冒险者经验）。
- **设置分组「迷锁地牢」**（`Source/Core.cs` SettingsRight 末尾，组数检查 18 → 19，在 `AdventurerChecks.cs`）。
- **自动放门** `GameComponent_Mythal`：殖民者附近 15～30 格（找不到放宽到 8～45 格）的 7×7 平地，只放一次；自测进程（`-lichtest`）不自动放。

## 自己定下的取舍（与设计文档不同之处）

1. **兑换任何时候都能用**（文档写封闭期间）：玩家拿到碎片就能花，少一道限制。重铸规则只在下一层还没生成时可用。
2. **房间不做“进门亮起/休眠领主惊醒”**：怪物用守点领主守自己的房间，效果相近，少一套唤醒逻辑。没有战争迷雾。
3. **全员倒地不等 5 天回卷**，6 小时后就吐回门口并回卷（否则人会流血死在里面）。硬核“吞噬”选项没做。
4. **宝藏直接散在房间地上**，没有宝箱容器 / 拟形宝箱；宝箱档位（铜银金玉）简化为“数量随层数 × (1+层/10)”。
5. **迷锁规则选了能用状态（Hediff）实现的 7 条**：禁火、禁法、黑暗、反魔要改伤害 / 灵能 / 光照，留到下一版；加了“破碎”（受伤 +25%）。
6. **第一版先 8 个主题**（文档 11 个）：熔火锻窟、冰封巨魔巢、远域回廊没做。
7. **房间事件（守护符文、秘门、谜语门、被困冒险者、圣坛、迷锁行商、迷锁回声）没做**，留给第二版。
8. **VPE 通关职业经验没做**（文档写不吃每日上限），冒险者经验和 Isekai 经验照给。
9. **精灵“迷锁共鸣”、卓尔“认得路”、70 层后换色光环、通关记录（最快）没做**；门上只记“最深解开”。
10. 守护者 / 里程碑首领没挂 `LP_LegendaryExtension`（文档建议），用剧情等级 + `HeroTraits.EnemyBoss` 强化。
11. 怪物全部用远古敌对派系生成，不借用费伦敌对派系（那个派系可在设置里关掉）。

## 文件

- 新增：`Source/Mythal.cs`、`Source/MythalChecks.cs`、`Defs/Mythal.xml`、`Textures/LP/Mythal/{GateOpen,GateSealed,Exit,Lamp,Fountain,Shard,Wall_Atlas}.png`、`ArtSource/RaceArt/draw_mythal_160.py`、`ArtSource/Preview/Mythal-1.60.0.png`。
- 改动（共享文件）：`Source/Core.cs`（`ExposeMythal();` 接在 `ExposeAbyss();` 后；SettingsRight 加 `("迷锁地牢",MythalSettings.Draw)`），`Source/AdventurerChecks.cs`（设置组数 18 → 19），`About/About.xml`（1.60.0 + 描述），`使用说明.txt`（快速上手一行、第二部分【1.60.0 迷锁地牢】、更新日志一行；对比备份只有新增 17 行，没有删改）。
- 新 defName：`LP_MythalGate`、`LP_MythalExit`、`LP_MythalWall`、`LP_MythalLamp`、`LP_MythalFountain`、`LP_MythalShard`、`LP_MythalFloor`（MapGeneratorDef + GenStepDef）、`LP_MythalExpelled`、`LP_MythalTheme_*`（8）、`LP_MythalRule_*`（7 条规则 Def + 6 个同名 HediffDef）。
- 备份：`ArtSource/backup-1.59.0`（整个模组；`Textures/LP/Exalted/Con.png` 是 Windows 保留名，用 `\\?\` 路径单独复制过）。

## 测试

- `Test.ps1 -Only Mythal`：**PASS 19 条**（含存读档：门的状态、层数、主题、规则、守护者、停留时间和正在进行的口袋地图）。日志无红字。
- `Test.ps1 -VPE`：PASS 482 条。
- `Test.ps1 -Isekai -Only Mythal`：PASS 19 条（装 Isekai 时能玩）。
- `Test.ps1 -NoHAR -Only Mythal`：PASS 19 条（没 HAR 时首领、主题替身正常）。
- 没跑完整套件（按要求不追旧的随机失败）。

## 贴图

RimWorld 手绘风：粗描边、平涂 + 暗边亮边、低饱和石灰色，淡蓝符文光是唯一亮色。墙图集按原版连接图集排列（第 i 块在 i%4 列、自下往上 i/4 行，上1右2下4左8），已对照创意工坊里现成的墙图集核对方向。

## 已知限制 / 下一步

- 口袋地图存在时整张图都在跑；没人在里面超过 1 天时怪物不休眠（文档的性能建议①没做）。封闭期间没有地图，不占性能。
- 第二版建议：房间事件、其余主题与规则、VPE 经验、宝箱容器与档位、精灵共鸣。
