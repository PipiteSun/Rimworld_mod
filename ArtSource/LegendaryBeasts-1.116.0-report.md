# 1.116.0 传奇怪物去 HAR 化报告

## 范围

“传奇怪物”按 HAR 联动里还剩下的怪物种族理解：五色真龙（红 / 蓝 / 绿 / 黑 / 白，各有青年 / 成年 / 远古三档兵种），以及贝希尔、眼魔、半巫妖。1.102.0 已经改成“人类 + 异种型”的六臂蛇人、双头巨人、独眼巨人这次没有动。死亡骑士本来就是人类兵种，也没有动。可玩的人形种族都不受影响。

## 做法

- 种族、身体、生命阶段、兵种从 `Bridges/HAR/Defs` 移到主模组：`Defs/ChromaticDragons_Races.xml`、`Defs/LegendaryMonsters_Races.xml`。父定义改成 `AnimalThingBase`，写法照 `WildMonsters.xml`。
- 所有 defName 不变：种族、兵种、BodyDef、BodyPartDef、LifeStageDef、头型（头型只留给旧存档读档用）。
- 贴图从 `Bridges/HAR/Textures` 移到 `Textures/ChromaticDragons/<颜色>`、`Textures/LegendaryMonsters/<名字>`。兵种的生命阶段用同一张 `Naked_Hulk`（Graphic_Multi，drawSize 1.5，和 HAR 人形身体网格一样大），根节点的放大补丁照旧按年龄档放大。
- 动物设定：野性 100%，不可训练，不会被捕食，不出现在野兽群发狂里（`canArriveManhunter=false`），不在任何生态区刷新，不可交易，没有性别，不得病。需求仍由“真龙之躯 / XX之躯”关掉。
- `Bridges/HAR` 只剩 `BridgeChecks.cs`：原来的蛇人 / 巨人检查，加上两条“HAR 启用时这些怪是普通动物种族”的检查。

## 数值补偿

| 项目 | 以前（HAR 人形） | 现在（动物型） |
|---|---|---|
| 近战命中 / 闪避 | 来自格斗技能 12 / 16 / 20（龙）、约 14（传奇怪） | 生命阶段加偏移：龙 +20 / +24 / +28 命中、+12 / +16 / +20 闪避；传奇怪 +22 命中、+14 闪避。成年龙命中实测 ≥90% |
| 超过 20 级的传奇近战 | 冒险者 hediff 的传奇属性点全投近战 | `StatPart_LegendaryBeastMelee`：按兵种等级下限给同样的点数（每级 2 点 × 3%）。远古龙 ×1.66，半巫妖 ×1.36，其余 ×1 |
| 等级、六维、DC | 冒险者 hediff | 直接读兵种的传奇扩展（`Legendary.LevelOf` 取等级下限） |
| 生命、护甲、体型、近战系数 | 生命阶段 + 真龙强度 | 不变 |

## 跟着改的代码

- `Source/LegendaryBeasts.cs`（新）：旧存档转换、传奇近战补偿。
- `ChromaticDragons.cs`：伤害补丁里“贝希尔打龙 ×1.5、半巫妖怕圣水银器 ×2”以前只看人形受害者，龙和半巫妖改成动物后会失效，现在不看人形；设置不再灰显。
- `LegendaryMonsters.cs`：`HarAvailable` 改名 `Available`；设置里去掉“需要 HAR”。
- `MindFlayer.cs`：吞噬新鲜尸体以前要求人形，传奇怪的尸体现在也算（“吞眼魔脑髓”要用它）；脑髓按人形公式算，智识取智力的一半。
- `QuestBoardMonsters.cs`、`Mythal.cs`、`Legendary.cs` 和几个 Defs 文件里的注释或说明文字。

## 旧存档

原版 `Pawn.ExposeData` 不看种族，会把背景、技能、衣物、基因等人形部件照读进来。`LegendaryBeastLoad` 在 PostLoadInit 时把这八个种族身上的这些部件清掉，同时清掉冒险者 hediff 和性别。地图上的、世界角色里的、尸体里的都会走这一步。身体（BodyDef）没变，伤口、缺失部位、传奇抗性和“真龙之躯 / XX之躯”都保留。Peter 的 Autosave-1 里有一条世界角色青年红龙，就是这种情况。HAR 留在存档里的 AlienComp 字段，新种族上没有对应的组件，读档时会直接跳过。

## 自检

- `-Only LegendaryMonsters`：20 条通过，含存读档。新加的一项把带着人形部件和冒险者 hediff 的贝希尔、红龙作为世界角色存下去，再读档，确认它们读回来是干净的；同时还存了一具眼魔尸体。用 `-Full`（Peter 的完整模组列表）也是 20 条通过，日志里只有已知的 GD3 红字。
- `-Only ChromaticDragons`：23 条通过，包括新的旧存档清理检查。之后的“数值核对”失败：成年龙对 5 个 20 级殖民者 178 秒，目标是 60～150 秒。生命 72、利器护甲 0.55 和改动前一样（前面的检查断言过这两个值），超出的原因在殖民者一侧：它们的近战系数现在是 ×1.50（1.81.0 改成每点调整值 +10% 的线性公式）。所以这一项和这次改动无关，我没有改它的目标区间。“巢穴动作”一项偶尔失败（龙在 130 刻内飞离巢穴中心），重跑就通过。
- `-NoHAR -Only LegendaryMonsters,ChromaticDragons`：40 条通过，同样停在数值核对。
- `-Only Legendary,Cleanup,QuestBoard,GiantKinHAR,Marilith`：前 105 条通过，最后公告板读档那一项失败；单独跑 `-Only QuestBoard` 是 35 条全过。`-Only Mythal,Events2`：187 条全过。

## 备份

`ArtSource/backup-1.115.0-legendbeasts`：改动前的 Bridges/HAR（Defs、Source、dll）、改到的 Source 和 Defs 文件、主程序集 dll、About.xml、使用说明.txt。
