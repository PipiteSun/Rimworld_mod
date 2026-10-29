# 1.24.0 实现要求：四个普通种族 + 四个传奇种族（含贴图与专属物品）

依据：[DND种族构思三.md](DND种族构思三.md)。基于设备上的 1.23.3。Peter 2026-10-25 指定范围：
- 普通种族：**元素裔、象人、食人魔、鸟人**
- 传奇种族：**寒冰魔（仅敌方）、炎魔（仅敌方派系）、泰坦（不特地调血量，只走巨躯规则）、巨灵**
- 全部带 RimWorld 画风的专属贴图，以及每族对应的特殊物品。

构思里写清楚的不重复，这里只写取舍、数值和接口。没写到的细节按模组现有惯例（1.18.0 牛头人/吉斯洋基、1.22.0 物品的做法）。

---

## 0. 总则

| 项目 | 规定 |
|---|---|
| 版本 | About.xml 1.23.3 → **1.24.0**；使用说明.txt 加 1.24.0 条目（面向玩家，按族写"怎么玩、怎么得到、物品怎么来"） |
| 备份 | 开工前把要改的文件复制到 `ArtSource/backup-1.23.3/`；先确认 1.23.3 已收尾（Build 过、Core 过） |
| 文件 | 新 Defs：`Genasi.xml`、`Loxodon.xml`、`Ogre.xml`、`Aarakocra.xml`、`Fiends.xml`（寒冰魔+炎魔）、`Titan.xml`、`Genie.xml`、`Items_Races124.xml`；新 Source：对应的 `.cs` 与 `Races124Checks.cs`；派系兵种加到 `FactionPawnKinds.xml`/`Factions.xml` |
| 体型 | 全部用现成 `SmallBodyExtension`（能放大）。巨躯规则（1.23.0）自动生效，**不再单独写生命倍率**。`BodySize.Size()` 已经乘了 SmallBody，不用改 |
| 种族判定 | 按 1.20.0 的"基因规则"（本源基因激活即算本族），不按异种型名 |
| 异种型 | 普通四族 `inheritable=true`；寒冰魔/炎魔/泰坦/巨灵 `inheritable=false`、`factionlessGenerationWeight=0`，另加原版 `Sterile` |
| 基因 | 全部非超凡（biostatArc 0）。自定义基因 `selectionWeight=0`（不进随机池） |
| 代谢 | 目标：元素裔 -1、象人 -1、食人魔 +1、鸟人 -2、寒冰魔 -4、炎魔 -6、泰坦 -5、巨灵 -4（传奇族的代谢不重要，不用凑） |
| 贴图 | 新脚本 `ArtSource/RaceArt/draw_races_124.py <模组根目录>`，复用 `draw_race_art.py`/`draw_minotaur_gith.py`/`draw_item_art.py` 的形状函数和调色；图标 256px、头部/身体附件 128px、武器 128px；预览 `ArtSource/Preview/Races-1.24.0.png`（图鉴四向 + 图标对照） |
| 自检 | `Races124Checks.cs` 挂进 `Checks.cs`，含存读档往返；pwsh 7 跑 Build、Core、Appearance、VisualCompat、HealthCompat；Full 档已知在力能龙 #340 处偶发失败，确认与本次无关即可 |
| 报告 | `ArtSource/Races-1.24.0-report.md`：改动文件、每族基因表与代谢实测、素材清单、测试矩阵、与本文的差异、需要 Peter 决定的点 |

---

## 一、元素裔 Genasi（4 个异种型共用一个本源）

**结构**：4 个 XenotypeDef `LP_GenasiFire 火元素裔`、`LP_GenasiWater 水元素裔`、`LP_GenasiEarth 土元素裔`、`LP_GenasiAir 风元素裔`。共用本源基因 `LP_GenasiCore 元素血脉`（0 代谢，只做判定和外观说明），各自一条血脉基因，血脉基因互斥标签 `DND_Element`（**巨灵复用这 4 条血脉基因**，见七）。

**共用原版基因**：`Hair_SnowWhite`（白发，元素裔的发色像元素）可选；缺点各自定。

| 血脉基因 | 效果 | 原版搭配 | 代谢 |
|---|---|---|---|
| `LP_ElementFire 炽焰血脉` | Flammability 0（不会被点燃），火焰伤害 **×0.5**（不是免疫，和火巨人区分）；MaxTemp +20 / MinTemp +10；能力 **火花**：6 格内一个目标点燃（小火，Fire severity 0.3），冷却 12 小时 | 自定义肤色 `LP_SkinEmber`（赭红带黑纹，用原版肤色模板）、Aggression_Aggressive（缺点） | -1 |
| `LP_ElementWater 潮汐血脉` | **水中行动**（新共享组件，见下）；MinTemp +10；能力 **水流**：以目标格为中心 3×3 灭火，并让格内敌人湿透（移速 ×0.8 10 秒，用 hediff），冷却 1 天 | `LP_SkinTide` 蓝青肤色、Delicate（缺点） | -1 |
| `LP_ElementEarth 岩石血脉` | MiningSpeed ×1.3、MiningYield +10%（上限 100%）、钝器护甲 +15%、移速 ×0.95；**无主动能力** | `LP_SkinStone` 灰褐肤色、MoveSpeed_Slow 不要再加（已有 ×0.95）、Learning_Slow（缺点） | -1 |
| `LP_ElementAir 疾风血脉` | 移速 ×1.1；能力 **风跃**：8 格内跳到可站立的格子，需要视线（不穿墙），冷却 8 小时，没有精神力消耗；复用吉斯洋基 `Githyanki.Step` 的位移代码，特效换成尘土/风 | `LP_SkinSky` 浅灰蓝肤色、Body_Thin、AptitudePoor_Melee（缺点） | -1 |

**新共享组件：水中行动 `AmphibiousExtension`**（挂在基因上）：带它的角色进入浅水/深水/沼泽/浅海时**不减速**（Harmony：`Pawn_PathFollower.CostToMoveIntoCell` 后缀，地形 `IsWater` 或 `passability` 为水类时把地形附加成本去掉，保留基础移动成本），并且 `TerrainDef.extraDraftedPerceivedPathCost` 也不算。深水原版不可通行，**不改**（会牵动寻路）。放在 `SharedComponents` 系列里（`Amphibious.cs`），以后特里同/海精灵用。

**种族关系**：火元素裔 ↔ 火巨人 +2/+2（远亲）；水元素裔 → 火巨人 -1；其他不设。

**派系**：费伦拓荒同盟加兵种 `LP_FC_Genasi 元素裔游民`（combatPower 60，`xenotypeSet` 四个元素裔各 25%），进 Combat/Peaceful/Settlement 组，权重 3。

---

## 二、象人 Loxodon

异种型 `LP_Loxodon 象人`，可遗传。

| 基因 | 效果 | 代谢 |
|---|---|---|
| `LP_LoxodonStature 象人体型` 自定义 | SmallBodyExtension：bodySize 1.3、renderScale 1.15、carryFactor 1.15（合计搬运约 1.5 倍）；食量 hediff ×1.2（照牛头人） | 0 |
| `LP_LoxodonTrunk 象鼻` 自定义 | 外观：自绘象鼻，头部附件（照牛鼻 `PawnRenderNode_AttachmentHead`，南/东两向，和牛鼻、原版鼻子互斥）；**第三只手**：搬运/建造/制作时若双手都受伤（Manipulation < 50%），象鼻把 Manipulation 托底到 50%（用 `HediffDef` 带 `capMods` 的 `setMax`? 不行——用 Harmony 在 `PawnCapacityWorker_Manipulation.CalculateCapacityLevel` 后缀取 max(result, 0.5)） | 0 |
| `LP_LoxodonCalm 沉稳` 自定义 | 免疫**恐惧类**效果：巫妖恐惧凝视（`LP_FearGaze` 的 hediff/精神状态）、原版 `PanicFlee`、骨质护符/触须披风的恐惧心情；`LP_MindResist` +0.2 | -1 |
| `LP_LoxodonHide 厚皮` 自定义 | 钝器护甲 +10%、锐器护甲 +5%；MaxTemp +5 | -1 |
| `LP_SkinLoxodon` 灰肤色 | 原版肤色模板，灰色 (150,150,155) | 0 |
| Body_Hulk、AptitudeStrong_Construction、Aggression_DeadCalm | 原版 | 0 / -1 / -1（以游戏实际值为准） |
| MoveSpeed_Slow、Beauty_Ugly 不加（象人不丑）、AptitudePoor_Shooting | 缺点，原版 | +1 / +1 |

目标合计约 -1。头发：加 `Hair_BaldOnly`（象人没头发）。

**种族关系**：象人 ↔ 牛头人 +2/+2（同为兽首人）；象人 → 食人魔 -2。

**派系**：费伦拓荒同盟加 `LP_FC_LoxodonWarden 象人卫士`（combatPower 90，重甲近战），Combat/Settlement 权重 2。

---

## 三、食人魔 Ogre

异种型 `LP_Ogre 食人魔`，可遗传。能穿普通衣物（照巨魔 1.5，不做专属服装）。

| 基因 | 效果 | 代谢 |
|---|---|---|
| `LP_OgreStature 食人魔体型` 自定义 | SmallBodyExtension：bodySize 1.6、renderScale 1.3、carryFactor 1.1；食量 hediff ×1.3。巨躯规则自动给生命 ×1.3、近战 ×1.18 | -2 |
| `LP_OgreDull 蠢笨` 自定义 | 研究速度 ×0.5、TradePriceImprovement -15%（交易被压价）、`LP_MindResist` -0.1（不能低于 0，用 statOffset 即可，Stat 有 minValue 0） | +2 |
| `LP_SkinOgre` 黄褐肤色 | 原版肤色模板 (190,160,100) | 0 |
| Body_Hulk、Jaw_Heavy、Brow_Heavy、MeleeDamage_Strong | 原版 | 0 / 0 / 0 / -2 |
| Learning_Slow、Aggression_Aggressive、AptitudePoor_Social、AptitudePoor_Intellectual、Beauty_Ugly | 缺点，原版 | +2 / +2 / +1 / +1 / +1 |

目标合计约 +1（偏弱，靠体型）。

**种族关系**：食人魔 → 半兽人 +1；矮人 → 食人魔 -2；精灵 → 食人魔 -2；食人魔 ↔ 巨魔 0。

**派系**：塞伊远征军加 `LP_FH_OgreBrute 食人魔打手`（combatPower 95）Combat/Settlement 权重 3；余烬氏族加 `LP_EC_OgreSlave 食人魔奴兵`（combatPower 90）Combat 权重 2。费伦拓荒同盟不要。

---

## 四、鸟人 Aarakocra

异种型 `LP_Aarakocra 鸟人`，可遗传。Peter 选的是鸟人（真飞），但**不改寻路**：飞行做成主动位移。

| 基因 | 效果 | 代谢 |
|---|---|---|
| `LP_AarakocraWings 羽翼` 自定义 | 外观：自绘翅膀，**身体附件**（`PawnRenderNode_AttachmentBody` 或等价节点，parentTagDef Body；南向画在身体后面——用 `drawData` 的 layer 比 Body 低；北向画在身体前面；东向一侧翅膀），三向；能力 **翱翔**：12 格内任意可站立格，**忽略墙、栅栏、水、建筑**（不要求视线），但目标格不能在**厚屋顶**（山体）下；冷却 4 小时；位移复用 `Githyanki.Step`，加一串羽毛/尘土 fleck 表示飞过；征召和非征召都能用（用来越墙干活） | -2 |
| `LP_AarakocraHollowBones 空心骨` 自定义 | SmallBodyExtension：bodySize 0.9、renderScale 1.0；钝器护甲 -10%（骨头脆） | +1 |
| `LP_AarakocraSkyborn 天生飞禽` 自定义 | 移速 ×1.1；**怕封闭**：在有屋顶的室内连续待满 1 小时后心情 -3"囚于屋檐"（离开 10 分钟消失；睡觉时不算，否则没法住房子）；在无屋顶处工作有 +2"天空在上" | -1 |
| `LP_SkinAarakocra` 浅褐肤色 | 原版肤色模板 (200,170,120) | 0 |
| Body_Thin、AptitudeStrong_Shooting、Ears_Pointed（可选，当羽冠） | 原版 | 0 / -1 |
| Delicate、AptitudePoor_Melee | 缺点，原版 | +1 / +1 |

目标合计约 -2。

**种族关系**：鸟人 ↔ 精灵 +1/+1；鸟人 → 夺心魔 -3（天空种族厌恶地底）。

**派系**：费伦拓荒同盟加 `LP_FC_AarakocraScout 鸟人斥候`（combatPower 65，弓/弩），Combat/Peaceful 权重 2。

---

## 五、寒冰魔 Ice Devil（仅敌方）

异种型 `LP_IceDevil 寒冰魔`，不可遗传，不进玩家可选（`factionlessGenerationWeight=0`，并在 `Factions.cs` 的 `FactionGroups.Process` 里保证**只有塞伊远征军**生成它）。"仅出现在敌方"的落实：

1. **击倒即消散**：寒冰魔、炎魔被击倒（Downed）或濒死时不留活口——Harmony 在 `Pawn_HealthTracker.MakeDowned` 后缀：若是魔鬼/恶魔（基因 `LP_FiendCore`），立刻 `Destroy`，原地放一团黑烟 + 消息"寒冰魔被遣返九狱"，并**掉落战利品**（见物品）。这样不能俘虏、不能招募、不能变成殖民者。
2. 死亡（直接打死）同样消散，尸体不留（`Corpse` 生成后立即销毁，掉同样的战利品）。
3. 不出现在商队、拓荒者、派系驻地的和平组里（只在 Combat 和 Settlement 组）。

| 基因 | 效果 |
|---|---|
| `LP_FiendCore 九狱之躯`（寒冰魔和炎魔共用） | 判定标记；Sterile 效果（加原版 Sterile）；不会因裸体不爽；不吃不睡（原版 `Hunger`? 1.7 没有"不吃"基因——用 hediff 把饥饿速度 ×0 并禁用睡眠需求，照机关人构思的思路；对敌人来说只是避免袭击者路上饿倒） |
| `LP_IceDevilStature 寒冰魔体型` | SmallBodyExtension：1.5 / 1.3 |
| `LP_IceDevilCold 寒霜之躯` | 低温伤害免疫（Frostbite/Hypothermia 相关：MinTemp -200）、火焰伤害 ×0.5、Flammability 0；**寒气光环**：半径 5 格的敌方（对寒冰魔敌对的）角色每 60 刻叠一层 `LP_Chilled 寒气侵体`（移速 ×0.85、近战冷却 ×1.1，叠 3 层上限，离开光环 30 秒后消退） |
| `LP_IceDevilStinger 冰刺尾` | 附加近战 Verb"尾刺"（刺，18 点，3 秒），命中追加 `LP_Chilled` 1 层 |
| `LP_IceDevilSight 魔鬼之眼` | 对隐身目标（力能龙伟龙隐形、夺心魔灵能隐匿、精灵斗篷/靴子的隐身）**照常瞄准**：Harmony 让这些隐身判定对寒冰魔的 Verb 返回可见。做不动就跳过并在报告里说 |
| 外观 | `LP_SkinIceDevil` 冰蓝肤色、Body_Hulk、自绘**恶魔角**（头部附件，三向；炎魔共用形状、换颜色）、Hair_BaldOnly、Beard_NoBeardOnly |

**兵种**：`LP_FH_IceDevil 寒冰魔`，combatPower 220，武器：无（尾刺+徒手）或 `LP_FrostSpear`（见物品，30% 概率携带，掉落时算缴获）。进塞伊远征军 Combat/Settlement 组权重 1.5；在 `FactionGroups.Process` 里：**袭击点数 < 1500 时移除所有寒冰魔**，≥1500 最多 2 个。

**弱点联动**：1.22.0 的寒铁武器对 `LP_FiendCore` 持有者伤害 ×1.5（如果 1.22.0 寒铁已经有"对妖精/魔鬼"加成就接上，没有就新加这条），圣水投掷物对其伤害 ×2。

---

## 六、炎魔 Balor（仅敌方派系）

异种型 `LP_Balor 炎魔`，不可遗传，同样走"击倒即消散"。

| 基因 | 效果 |
|---|---|
| `LP_FiendCore` | 同上 |
| `LP_BalorStature 炎魔体型` | SmallBodyExtension：**2.5** / 1.6。巨躯规则 → 生命 ×1.75、近战 ×1.45，不另调 |
| `LP_BalorFlame 深渊烈焰` | 火焰完全免疫、Flammability 0；低温伤害 ×1.5、MinTemp +30（怕冷）；**烈焰光环**：每 120 刻，2 格内每个可燃物/角色 20% 概率着火（自己人不点——塞伊的其他兵种站旁边会烧到，这是设定的一部分，保留） |
| `LP_BalorWings 炎魔之翼` | 外观：身体附件翅膀（和鸟人共用形状，换成暗红+黑，画大一号）；能力 **振翼**：4 格内敌人击退 2 格（复用 `DNDPush`）并倒地 2 秒，冷却 1 天，AI 会用 |
| `LP_BalorDeathburst 死亡爆炸` | 消散时半径 4 的火焰爆炸（Flame 伤害 40，点燃地面），延迟 1 秒（先出字"炎魔的身体开始崩解"），对所有人生效 |
| 外观 | `LP_SkinBalor` 暗红肤色、Body_Hulk、恶魔角（红黑版）、Hair_BaldOnly |

**武器**：`LP_BalorWhip 炎魔火鞭`（见物品），炎魔 100% 携带，消散时掉落。

**兵种**：`LP_FH_Balor 炎魔`，combatPower 450，`factionLeader=false`。塞伊远征军 Combat/Settlement 组权重 0.5；`FactionGroups.Process`：**点数 < 3500 移除**，≥3500 最多 1 个；塞伊的"斩首领队崩解"规则对炎魔不生效（它不是领队）。有炎魔的袭击，信件标题加"深渊裂口"字样（在现有的派系袭击信件处理里加一句）。

---

## 七、巨灵 Genie（传奇，可玩）

**核心循环**：神灯 → 擦灯召出巨灵 → 巨灵是殖民者，带一年一次的许愿 → 巨灵死了回灯里，过些天再擦出来。

### 异种型与基因

异种型 `LP_Genie 巨灵`，不可遗传，Sterile，`factionlessGenerationWeight=0`。生成时**随机**带 4 条元素血脉基因之一（复用一节的 `LP_ElementFire/Water/Earth/Air`，巨灵的异种型里不写血脉，由召唤代码加为 xenogene），显示名随血脉：伊弗利特（火）、玛瑞德（水）、道（土）、金尼（风）——用 `Gene_GenieCore` 的 `LabelCap`/`Pawn` 名字前缀体现，异种型名统一"巨灵"。

| 基因 | 效果 |
|---|---|
| `LP_GenieCore 巨灵本源` | 判定；**灯之束缚**：巨灵记录自己的神灯（Thing ID）；神灯不在玩家手里（被卖、被偷、被毁）时巨灵在 1 天后"化烟离去"（Destroy，消息）；巨灵死亡/消散时**不留尸体**，神灯进入"暗淡" 15 天，之后能再擦出同一个巨灵（保留名字、技能、血脉，伤势清空）；巨灵不能被招募、不能被俘（敌方巨灵本次不做，所以只是保险）；巨灵可以穿普通衣物 |
| `LP_GenieStature 巨灵体型` | SmallBodyExtension：1.5 / 1.3 |
| `LP_GenieAgeless` | 加原版 Ageless；不需要睡觉以外的需求照常（巨灵要吃饭，省得写新系统） |
| `LP_GenieWish 许愿` | 能力 **实现愿望**（无目标，弹对话框）：**每 60 天一次**（冷却存档），选 6 个之一：① 丰收——地图上所有玩家作物长到成熟；② 和平——15 天内所有敌对袭击事件不触发（Harmony 让 `IncidentWorker_RaidEnemy.CanFireNowSub` 对玩家地图返回 false，并发信"巨灵的屏障"）；③ 痊愈——地图上所有殖民者的伤口、疾病（含上瘾不算）治愈；④ 财富——在巨灵脚下生成 2000 银；⑤ 晴空——当前地图天气改为晴天并持续 5 天（GameCondition）；⑥ 顿悟——所有殖民者随机一项技能 +2 级。许愿后巨灵获得 `LP_GenieFatigue 倦怠` 15 天：工作速度 ×0.5、血脉能力不可用、心情 -5 |
| 血脉 | 四条元素血脉之一（见一节），巨灵的血脉能力冷却 ×0.5（在能力的 `CooldownTicks` 判定里乘） |
| 外观 | `LP_SkinGenie`：肤色随血脉——召唤时按血脉给对应元素裔的肤色基因（直接复用 `LP_SkinEmber/Tide/Stone/Sky`）；Body_Hulk；自绘**巨灵耳饰/头箍**（头部附件，三向，金色） |

### 神灯 `LP_WonderLamp 神灯`（奇物，1.22.0 奇物体系）

- 不能制作。来源：黑网商队必带 1 盏（stock 1，价 3500）；塞伊红袍商人 20% 概率带；Isekai 地下城宝箱（1.22.0 联动）5%；塞伊远征军驻地可能有（选做）。
- 右键"擦拭神灯"（Job，3 秒）：若灯"沉睡"→ 召出巨灵加入殖民地（随机血脉、随机名字前缀"伊弗利特/玛瑞德/道/金尼"）；若灯"暗淡"→ 显示剩余天数；若巨灵在场 → 什么也不发生。
- 神灯是 `Graphic_Single`，有"沉睡/有主/暗淡"三种状态，检视面板显示。
- 卖掉有巨灵的神灯：巨灵 1 天后离去（见上）。

---

## 八、泰坦 Titan / 天穹神裔 Empyrean（传奇，可玩）

异种型 `LP_Titan 天穹神裔`，不可遗传，Sterile。只能从剧本 **坠落的神裔** 或开发者模式得到。可以穿普通衣物。**血量不特地调整**：只靠巨躯规则（2.5 体型 → 生命 ×1.75、近战 ×1.45）。

| 基因 | 效果 | 代谢 |
|---|---|---|
| `LP_TitanStature 神裔之躯` | SmallBodyExtension：**2.5** / 1.6、carryFactor 1.2；食量 hediff ×1.5；移速 ×0.9 | -2 |
| `LP_TitanDivinity 神威` | `Gene_Resource` 资源条"神威"0～100：每天自然 +10；每次击杀敌人 +5；心情 ≥ 80% 时每天额外 +10。两个能力消耗它 | -2 |
| `LP_TitanWeather 天候随心` | 泰坦**在地图上**时，每天黎明检查一次心情：≥ 80% → 当天天气转晴（`WeatherDef Clear`）；≤ 30% → 转雷雨（`RainyThunderstorm`）；中间不干预。只影响泰坦所在地图。设置里加一个开关"神裔影响天气"（默认开） | 0 |
| `LP_TitanUnshakable 不可动摇` | 免疫眩晕（StunFor 前缀，照力能龙 EMP 免疫写法）；`LP_MindResist` +0.5 | -1 |
| 能力 **神罚**（基因 `LP_TitanDivinity` 给） | 10 格内一格落一道闪电（原版 `WeatherEvent_LightningStrike` 同款：EMP+火花+Flame 伤害），消耗神威 30，冷却 1 天 | |
| 能力 **神裔之息** | 自身 5 格内所有友方：每处伤口 -10 严重度，解除流血，消耗神威 40，冷却 2 天 | |
| 外观 | `LP_SkinTitan` 金铜肤色、Body_Hulk、自绘**光环**（头部附件，三向，淡金色圆环，北向也画）、Hair_SnowWhite 可选 | 0 |
| 缺点 | Aggression_Aggressive（+2）、**傲慢**：复用力能龙"龙之傲慢"那套心情（殖民地里有比自己市值/头衔更高的存在时 -3）——如果复用不便，就做一条简单的"神裔的傲慢"：泰坦不是殖民地里体型最大的角色时 -3 | |

目标合计约 -5。

**剧本 `LP_FallenEmpyrean 坠落的神裔`**：1 名泰坦（成年，随机技能偏近战/社交）+ 2 名人类追随者（原版基线人类）；开局物资照"龙蛋"剧本的规格减半；开局 10 天后一次塞伊远征军袭击（现有的"限时派系袭击" ScenPart）；费伦拓荒同盟好感 +20。

---

## 九、物品（`Items_Races124.xml`，全部走 1.22.0 的 `RaceGearExtension`/`CompGrantAbility`/`CompItemAbility` 体系）

| 物品 | 类型 | 效果 | 获得 |
|---|---|---|---|
| `LP_ElementalFocus_Fire/Water/Earth/Air` 炽焰符 / 潮汐符 / 岩石符 / 疾风符 | 饰品（`LP_Accessory` 层） | 对应血脉的元素裔或巨灵佩戴：该血脉能力冷却 ×0.5、被动加倍（火：火焰伤害 ×0.25；水：灭火范围 5×5；土：挖矿 ×1.6；风：移速 ×1.2）。**其他人**佩戴：得到该血脉的被动弱化版（火：Flammability ×0.5；水：水中行动；土：挖矿 ×1.15；风：移速 ×1.05），没有能力 | 炼金桌，由**对应血脉的元素裔**制作（RaceGearExtension race 判定扩展成"血脉"），材料：秘银 20 + 对应材料（火：熔岩结晶 5；水：药草 20；土：精金 5；风：布 30） |
| `LP_IvoryHorn 象牙号角` | 奇物，可装备到 `LP_Accessory` | 能力 **号角**：使用者 8 格内友方 30 秒：恐惧免疫 + 移速 ×1.1；象人吹范围 12 格；冷却 1 天 | 象人在手工台制作：象牙? 没有象牙材料——改用骨头? 原版没有骨头材料；用 **白银 50 + 钢 20**，RaceGearExtension race=Loxodon |
| `LP_OgreClub 食人魔大棒` | 近战武器（钝，品质可变，材料木/金属） | 钝击 power 30、cooldown 3.2、钝击附带 1 格击退 20% 概率；**体型 < 1.4 的使用者**：伤害 ×0.6、移速 -0.2（太重） | 食人魔在锻造台/手工台制作：材料 80；塞伊的食人魔打手 60% 携带，缴获 |
| `LP_FeatherCloak 风羽披风` | 衣物（Shell 层，不覆盖手臂） | 鸟人穿：翱翔距离 +4、冷却 ×0.75；其他人穿：移速 ×1.08、保暖同原版斗篷；美观一般 | 鸟人在裁缝台制作：布 60 + 装置零件? 不要——布 60 + 精灵长弓用的材料?——**布 60 + 皮革 20**，RaceGearExtension race=Aarakocra |
| `LP_IceDevilStinger 寒冰魔尾刺` | 材料（`ResourceBase`，堆叠 10，不能制作） | 寒冰魔消散时掉 1 根 | |
| `LP_FrostSpear 霜刺长枪` | 近战武器（刺，固定材料，品质可变） | 刺 power 24 / cooldown 2.4、钝 12；命中附加 `LP_Chilled` 1 层；对 `LP_FiendCore`（寒冰魔/炎魔）**无效**（魔鬼不怕自己的寒气） | 锻造台：寒铁 40 + 寒冰魔尾刺 1；研究前置 `LP_ColdIron` 的研究（1.22.0 秘金冶炼） |
| `LP_BalorHeart 炎魔之心` | 材料（堆叠 1） | 炎魔消散时掉 1 颗 | 本次只做掉落 + 能卖（2500 银）+ 作为火焰舌的材料预留（火焰舌是物品构思二的 1.25.0 内容，这次不做） |
| `LP_BalorWhip 炎魔火鞭` | **短程远程武器**（range 4.9、warmup 0.6、cooldown 1.8、单发），投射物"烈焰鞭击"：Flame 伤害 18、命中点燃、把目标向持有者**拉近 2 格**（DNDPush 反向） | 非火免者（没有 `LP_FiendCore`、火巨人本源、炽焰血脉）使用：每次射击自己手部受 2 点 Burn；AI 炎魔用它 | 炎魔消散时掉落（品质优秀）；不能制作 |
| `LP_EmpyreanBlade 天穹之剑` | 近战武器（锐，等离子钢固定，品质可变） | 剑刃 power 32 / cooldown 2.6；命中 25% 追加一次 EMP 10 + Flame 6（雷霆）；**体型 < 2 的使用者**打不动：伤害 ×0.5（和火巨人熔核武器的"只能火巨人用"不同，这里是打折不是禁止） | 泰坦或火巨人在锻造台/熔火锻炉制作：等离子钢 60 + 秘银 20 + 金 10；剧本"坠落的神裔"自带 1 把 |
| `LP_WonderLamp 神灯` | 奇物（见七） | 擦灯召巨灵 | 黑网 / 红袍商人 / Isekai 宝箱 |

元素符四件和火鞭的"拉近"是本批最复杂的两件，先做其他的。

---

## 十、派系与 FactionGroups 改动

| 派系 | 新兵种 | 组 / 权重 | 点数限制 |
|---|---|---|---|
| 费伦拓荒同盟 | 元素裔游民 60、象人卫士 90、鸟人斥候 65 | Combat 3/2/2，Peaceful 2/1/2，Settlement 3/2/1 | 无 |
| 塞伊远征军 | 食人魔打手 95、寒冰魔 220、炎魔 450 | Combat 3/1.5/0.5，Settlement 3/1.5/0.5 | 寒冰魔 ≥1500 且 ≤2；炎魔 ≥3500 且 ≤1（在 `FactionGroups.Process` 里裁） |
| 余烬氏族 | 食人魔奴兵 90 | Combat 2，Settlement 2 | 无 |

现有"殖民地里有某族 → 好感"规则：象人、元素裔、鸟人在殖民地 → 费伦拓荒同盟 +5（并进现有 `LP_Goodwill_ColonyHalfOrc` 那类 situation 的做法）；食人魔在殖民地 → 费伦拓荒同盟 -5、塞伊 +0（不加）。

---

## 十一、素材清单（`draw_races_124.py` 一次生成）

| 类别 | 贴图 | 规格 |
|---|---|---|
| 异种型图标（11） | `Races124/Xenotypes/GenasiFire`、`GenasiWater`、`GenasiEarth`、`GenasiAir`、`Loxodon`、`Ogre`、`Aarakocra`、`IceDevil`、`Balor`、`Titan`、`Genie` | 256 |
| 基因图标 | 元素血脉 4 + 元素血脉本源 1；象人体型、象鼻、沉稳、厚皮；食人魔体型、蠢笨；羽翼、空心骨、天生飞禽；九狱之躯、寒冰魔体型、寒霜之躯、冰刺尾、魔鬼之眼；炎魔体型、深渊烈焰、炎魔之翼、死亡爆炸；巨灵本源、巨灵体型、许愿；神裔之躯、神威、天候随心、不可动摇（肤色基因用原版模板自动着色，不画） | 256 |
| 能力图标 | 火花、水流、风跃、翱翔、振翼、实现愿望、神罚、神裔之息、号角 | 256 |
| 头部附件 | 象鼻（南/东）、恶魔角（南/东/北，两套颜色：冰蓝、红黑）、巨灵头箍（南/东/北）、泰坦光环（南/东/北） | 128，照牛角的画框与锚点（头部约 x 41-87、y 39-89） |
| 身体附件 | 鸟人翅膀（南/东/北，浅褐）、炎魔翅膀（南/东/北，暗红黑，放大 1.2） | 128，照原版身体贴图画框；先导出原版 Male_Average 身体对位，再进游戏截图确认 |
| 物品 | 元素符 ×4、象牙号角、食人魔大棒、风羽披风（含 Worn 五体型三向——照 1.22.0 精灵锁子甲/触须披风的 Worn 做法）、寒冰魔尾刺（a/b/c 堆叠三档）、霜刺长枪、炎魔之心、炎魔火鞭 + 投射物"烈焰鞭击"、天穹之剑、神灯（三态：沉睡/有主/暗淡） | 武器 128 横放，材料照 1.22.0 |
| 预览 | `ArtSource/Preview/Races-1.24.0.png` | 图鉴四向（11 族）+ 图标对照 |

---

## 十二、自检要点（`Races124Checks.cs`）

- 11 个异种型能生成、基因全在、代谢合计与本文一致（传奇族只记录不断言）。
- 体型/渲染/搬运/食量：象人 1.3、食人魔 1.6、鸟人 0.9、寒冰魔 1.5、炎魔 2.5、巨灵 1.5、泰坦 2.5；巨躯规则生命/近战倍率抽样（炎魔、泰坦 ×1.75/×1.45）。
- 元素裔：火花点燃、火焰伤害 ×0.5、不被点燃；水流灭火 3×3 + 湿透；水中行动不减速（浅水格移动成本对比）；土裔挖矿 ×1.3；风跃距离/视线/冷却。
- 象人：象鼻渲染节点、Manipulation 托底 0.5、恐惧免疫（恐惧凝视对象人无效）、心智抗性 0.2。
- 食人魔：大棒体型打折、研究 ×0.5、压价。
- 鸟人：翱翔穿墙、不能落到厚屋顶下、冷却；囚于屋檐心情出现与消失；翅膀节点。
- 寒冰魔/炎魔：击倒即消散 + 掉落；死亡无尸体；寒气光环叠层与消退；尾刺 Verb；炎魔死亡爆炸；火鞭拉近与非火免者烧手；点数阈值裁兵（1000 点无寒冰魔，2000 点 ≤2 寒冰魔且无炎魔，4000 点 ≤1 炎魔）；寒铁 ×1.5、圣水 ×2。
- 巨灵：擦灯召唤、随机血脉与肤色、许愿 6 项各执行一次（和平阻断袭击、丰收、痊愈、财富 2000、晴空、顿悟 +2）、倦怠、冷却 60 天、神灯卖掉 1 天后巨灵离去、巨灵死亡 → 灯暗淡 15 天 → 再擦出同名同血脉。
- 泰坦：神威增减、神罚闪电、神裔之息治疗、眩晕免疫、天候随心（高心情→晴、低心情→雷雨、开关关闭无效）、傲慢心情、剧本能生成。
- 存读档往返：每族一个角色（体型、能力冷却、巨灵的灯绑定、泰坦神威、寒气层数）。
- 测试结束清理掉落物、恢复天气与派系好感。

---

## 十三、本文替 Peter 做的默认选择（报告里列出，等他改）

1. 元素裔做成 4 个异种型（不是 1 个 + 互斥基因），为了派系随机生成方便。
2. 鸟人"真飞"做成 12 格穿墙位移能力，不改寻路。
3. 食人魔进塞伊远征军和余烬氏族；以巨躯规则下 ×1.3 生命作为平衡，不额外削。
4. 寒冰魔/炎魔"击倒即消散"，彻底不能俘虏；战利品替代尸体。
5. 巨灵血脉复用元素裔的 4 条基因；许愿 6 项不含"复活"。
6. 泰坦不做传奇抗性，只做眩晕免疫 + 心智抗性 0.5；体型 2.5。
7. 只加一个剧本（坠落的神裔）；四个普通种族靠派系出现，不另开剧本。
8. 炎魔之心本次只是掉落物，火焰舌留给 1.25.0。
