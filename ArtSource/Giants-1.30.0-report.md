# 1.30.0 巨人通用层 + 霜巨人（第一批）实装报告

设备版本：1.29.0 → **1.30.0**（About.xml `modVersion`）。备份：`ArtSource/backup-1.29.0/`（按相对路径保存改动前的文件）。
风暴巨人、云巨人不在本次范围（血统 rank 1、2 已预留）。

## 一、改动文件

| 文件 | 改动 |
|---|---|
| `Source/GiantKin.cs`（新） | `GiantKinExtension`（rank、element、label、adultSize、renderScale）、`GiantKin.Of/Is/Rank/Size/RenderSize`（按 `Notify_GenesChanged` 失效的缓存）、殖民地巨人普查；`GiantApparelExtension`/`GiantWeaponExtension`、`GiantGear.CanWear/Release/AttireMood`；“合身的巨人之衣”；阶序四种心情、`GiantOrdningState`（首领记录、挑战首领、30天冷却）；设置“巨人穿普通衣服”“阶序心情” |
| `Source/FrostGiant.cs`（新） | 霜巨人基因逻辑：失温跳过、寒气/寒霜迟缓免疫、雪原行者（积雪移动消耗 Transpiler）、“寒冷”心情去除、冬狼之主（驯服倍率、主人加成 StatPart、不反噬）、冬之号角（能力组件、命中加成）、霜巨人斧寒霜迟缓、战利品之心（袭击打退钩子、战利品市值心情、殖民地制品记录）、塞伊霜巨人掠夺者 `FrostGiantRaid`、剧本组件 `ScenPart_IceborneKin`、红袍商队冬狼幼崽 `StockGenerator_WinterWolfPup` |
| `Source/GiantChecks.cs`（新） | 检查组 `Giants`，16 条（见第四节） |
| `Source/FireGiant.cs` | `Size()` 改用 `GiantKin.Size(pawn,2f)`；体型/渲染补丁改为遍历巨人血统（+巨魔、造物）；删除“火巨人只能穿巨人衣服”的白名单判断（`GiantClothing` 改名 `ForgeApparel`，只剩力能龙宝库计价用），穿衣走 `GiantGear.CanWear`；装备检查加“只有巨人能用这件武器”“只有巨人能穿”；裸体心情对霜巨人也生效 |
| `Source/BodySize.cs` | 巨躯体型来源改为 `GiantKin.Size`；“成年火巨人生命不低于×150%”扩大到成年巨人血统（霜巨人同样） |
| `Source/Satiety.cs` | 胃容量来源改为 `GiantKin.Size`：成年霜巨人×200%，儿童随年龄（旧存档仍按 1.27.1 的记录同步） |
| `Source/Beasts.cs` | 驯服倍率加冬狼之主一档（与种族加成取较大，上限100%） |
| `Source/Fiends.cs` | 寒气侵体对霜巨人不生效 |
| `Source/Items.cs` | 限时状态给予处跳过霜巨人的寒霜迟缓；种族键 `Giant`、`FrostGiant` |
| `Source/Loxodon.cs` | 恐惧免疫加“冬之号角” |
| `Source/GiantMigration.cs` | 霜巨人战帮（权重 20、冬季×2；其余压到 48/24/8）、冬狼要生肉（每只 1.0）、礼物（冬狼幼崽 70% / 霜巨人斧 30%）、同族按血统 |
| `Source/Factions.cs` | 塞伊战斗组调用 `FrostGiantRaid.Apply`；霜巨人掠夺者/战士的装备 |
| `Source/ForceDragonHoard.cs` | 跟着改名 `ForgeApparel` |
| `Source/Core.cs` | 设置存取与界面（左栏，种族设置之后的“巨人”分组） |
| `Source/Checks.cs` | 完整测试里登记 `CheckGiants`、`PrepareGiantsSave`、`CheckGiantsAfterLoad` |
| `Source/FireGiantChecks.cs` | 老检查改为新规则：“火巨人能穿普通风衣和巨人衣服”；卸下血统时检查 `GiantGear.Exclusive` |
| `Source/TwilightDeities.cs`、`Source/EventChecks.cs` | 顺手修的老问题：维拉基斯容器倒在河中间时灰烬放不下（相邻8格都是水），放宽到4格内；检查同步放宽（完整测试第一次因此失败，见第四节） |
| `Defs/Giants.xml`（新） | 合身的巨人之衣、阶序井然/统领众巨/氏族之首/同族之语、火↔霜世仇（RacialRelationDef，-5，60天后+2） |
| `Defs/FrostGiant.xml`（新） | 异种型、8 个基因、冬之号角/狂猎号角两个能力、冬之号角/狂猎状态、战利品两种心情、两个兵种、剧本“冰原之子” |
| `Defs/Items_Giants.xml`（新） | 冬狼皮、霜巨人斧（+锻造配方）、冬狼皮大氅、霜巨人号角（+配方） |
| `Defs/FireGiant.xml` | 火巨人本源加血统扩展（rank 3、2.0/2.0）；异种型与基因描述去掉“只能穿专属服装”；锻工外衣：专属扩展、改穿**中层**、自己的穿戴贴图 |
| `Defs/FireGiantEquipment.xml`、`Defs/MoltenGuard.xml` | 熔铁战盔、熔铁重甲、炉心战盔、熔核重甲加专属扩展，描述改为“只有巨人能穿” |
| `Defs/Beasts.xml` | 冬狼屠宰产出冬狼皮 |
| `Defs/FactionTraders.xml` | 红袍商队：冬狼皮 40～120、25% 一只冬狼幼崽 |
| `Textures/Giants/…`、`Textures/FireGiant/Worn/Tunic_*` | 新贴图（第三节） |
| `ArtSource/RaceArt/draw_giants_130.py`（新）、`ArtSource/Giants-1.30.0-preview.png` | 画图脚本与预览 |
| `About/About.xml`、`使用说明.txt` | 版本号、描述；内容一览加霜巨人一行与剧本；详细说明加【1.30.0 巨人通用规则】【1.30.0 霜巨人】；更新日志顶部 1.30.0；旧的“火巨人不能穿普通服装”加了“已被 1.30.0 取代” |

## 二、霜巨人数值

| 项 | 数值 |
|---|---|
| 异种型 | `LP_FrostGiant`，可遗传，代谢合计 **-3**，战斗力系数 1.6 |
| 霜巨人本源 `LP_FrostGiantCore`（-3） | 血统 rank 4；体型 2.0（显示 1.4，儿童随年龄）；巨躯：生命×150%、近战×130%；胃×200%；移速×85%；舒适下限 -80℃；冻伤伤害×0；不失温；寒气侵体、寒霜迟缓无效；不在乎裸体；给能力“冬之号角” |
| 雪原行者 `LP_FrostSnowWalker`（-1） | 积雪不增加移动消耗；没有“寒冷”（EnvironmentCold）心情 |
| 畏火 `LP_FrostGiantFrailty`（+2） | 火焰伤害×1.5；舒适上限 -22℃ |
| 冬狼之主 `LP_FrostWolfLord`（-1） | 驯冬狼×3、北极狼/森林狼×1.5（上限100%）；驯服失败/误伤不反噬；主人的冬狼近战伤害×1.15、移速×1.1 |
| 战利品之心 `LP_FrostLootHeart`（0） | 打退袭击 3 天 +5；非殖民地制造的武器护甲每 1000 银 +1，最多 +4，2500 刻重算 |
| 外表 | `LP_SkinFrost`（164,186,204）、`LP_HairFrost` 霜白（222,236,246）、Body_Hulk、`LP_FrostBraids` 结霜发辫 |
| 冬之号角 `LP_WinterHornBlast` | 需装备霜巨人号角；15 格友方 60 秒近战命中 +15%、恐惧免疫；冬狼另 `LP_WildHunt` 移速×1.3；冷却 1 天 |
| 霜巨人斧 | 巨人专用；劈砍 30 / 2.8 秒，钝 14 / 2 秒；命中寒霜迟缓 4 秒；钢或寒铁 80，锻造台，研究锻造，手工 6 |
| 冬狼皮大氅 | 巨人专属外层，保暖 +40，利器 0.20 / 钝器 0.06 / 热 0.12，心情 +2；缝纫台，冬狼皮 60 |
| 霜巨人号角 | 饰品层（腰）；象牙/犀牛角/猛犸象牙 1 + 寒铁 10，手工制作点 |
| 冬狼皮 | 保暖 27（北极狼皮 24），利器 1.04，价值 3.6 |
| 霜巨人掠夺者 | combatPower 180；<800 点不出现，≥800 点 35%（冬×2、夏×0.5），≥2500 点一半概率两个；斧 60% / 钢长矛 40%，冬狼皮大氅，随行冬狼 1～2 |

## 三、贴图（draw_giants_130.py）

- 基因图标 256：霜巨人本源、雪原行者、畏火、冬狼之主、战利品之心（冰蓝 + 白，粗描边平涂）。
- 结霜发辫三向 128（与牛头人牛角同一画框，头部附件 layer 80）。
- 能力图标“冬之号角”128（1.27.4 职业图标的卡牌画法）。
- 物品：霜巨人斧（横放）、冬狼皮大氅、霜巨人号角、冬狼皮 a/b/c。
- 穿戴：冬狼皮大氅、锻工外衣各五体型三向 256，用熔铁重甲的穿戴贴图当轮廓蒙版重新上色（锻工外衣画成近白灰，由布料材质着色）。
- 预览：`ArtSource/Giants-1.30.0-preview.png`。

## 四、测试

测试结果（以 Test.ps1 输出为准）：
- `-Only Giants`（最后一次）：`PASS: 16 checks (only Giants); save/reload.`
- `-HealthCompat`：`PASS: 354 health compatibility checks; native HP, combined tempering cap, reinforcement, materials, Titan, injury ratios, full save/load and resurrection.`
- 完整 Core：跑了 4 次，**没有一次全过**：
  1. `FAIL after 2744 checks` — `Vlaakith departs: the body turns to ash, nothing to capture … terrain=WaterShallow`（老问题：容器倒在河中间，相邻8格都是水放不下灰。已修，见第一节）。
  2. `Lich test timeout` — 游戏启动时在 Harmony 加载后卡住（日志停在 Harmony 那一行），没有进入测试。
  3. `FAIL after 3025 checks` — 我新写的雪原行者检查选到了积雪不影响移动的格子（连普通人都不变慢）。改为挑“普通人确实变慢”的格子；之前的 3025 条（含 Events、Guns 等全部老组）都过了。
  4. `FAIL after 2162 checks` — `the flame aura ignites everyone within 2 cells, Thayan allies included: near True/0.7 at (1, 0, 0) mate False/0.7 at (-1, 0, 0) balor False weather Clear`（1.27.3 报告里列过的老偶发“炎魔火焰光环”，和本次改动无关）。
  按 Peter 的要求没有再为老偶发重跑；改过的雪原行者检查已在 `-Only Giants` 里通过。完整测试的读档后半段这次没有完整跑通过一次。

`Giants` 组 16 条：火巨人体型/生命/近战/渲染/胃不变；火巨人能穿原版风衣；普通人穿不了锻工外衣（提示“只有巨人能穿”）；三件专属衣甲 +6；火+霜同住双方 -5；阶序（火巨人统领众巨、霜巨人阶序井然、同族之语）；霜巨人体型 2.0/渲染 1.4/胃 200%/生命×1.5/近战×1.3；-80℃ 不失温、冻伤免疫、寒气与寒霜迟缓免疫；火焰×1.5；驯冬狼×3（上限100%）；积雪不减速（普通人照常减速）；冬之号角（没号角灰掉、吹响后友方免疫恐惧、冬狼狂猎、1 天冷却）；霜巨人斧只有巨人能用；塞伊远征军存在；1000 点含霜巨人掠夺者（披大氅、带冬狼）、700 点不含；存读档后体型与号角冷却不变。

**未自动测试，请在游戏里看**：剧本“冰原之子”、巨人迁徙的霜巨人战帮（讨食、生肉、礼物、留下）、物品配方（霜巨人斧、冬狼皮大氅、霜巨人号角）、红袍商队货单、挑战首领、战利品之心两种心情、雪地里实际走路的手感、贴图在游戏里的对位（发辫、大氅、锻工外衣）。

## 五、与要求的差异

1. **火巨人的渲染其实是 2.0，不是 1.4**：1.29.0 的火巨人体型和渲染都按 `FireGiant.Size()`（成年 2.0）。我没动火巨人（要求火巨人和 1.29.0 相同），霜巨人按要求的数字用 2.0 体型 / 1.4 渲染。所以现在霜巨人看上去比火巨人矮一截。
2. 霜巨人体型没有挂 `SmallBodyExtension`（那个扩展不随年龄长大），而是写在 `GiantKinExtension` 的 adultSize/renderScale 上，和火巨人一起走 `GiantKin.Size`（儿童随年龄长大）。数值一致，巨躯规则照常生效。
3. **冬狼咬的寒霜迟缓仍叫 `LP_FrostSlow`**：1.29.0 改名的是寒霜法杖的状态（`LP_FrostStaffChill`，就是为了不和冬狼重名）。霜巨人免疫 `LP_FrostSlow` 和 `LP_Chilled`；法杖的“寒霜缠身”照常生效。
4. 1.7 的 GeneDef 没有 `nullifiedThoughts` 字段：裸体心情并进火巨人原有的裸体补丁，“寒冷”心情用一个小补丁去掉。
5. **锻工外衣改穿中层**（原来是外层）：熔铁/熔核重甲同时占中层和外层，冬狼皮大氅是外层，不改的话任何三件专属衣甲都穿不到一起，“+6”永远达不到。现在“熔铁战盔 + 锻工外衣 + 冬狼皮大氅”可以同穿。
6. 霜巨人掠夺者没进 XML 兵种表，由代码按点数加进战斗组（800 点以下一个都没有），“冬季权重×2、夏季×0.5”做成出现概率 35% 的季节倍率；每个霜巨人顶掉 2 名普通随从、每只冬狼顶掉 1 名，避免袭击变强太多。
7. 巨人迁徙要求派系不敌对，所以霜巨人战帮算**费伦拓荒同盟**的流浪战士（新兵种 `LP_FC_FrostGiantWanderer`），不是塞伊远征军。
8. 发色只能给一个基因，用了“霜白发色”（白中带一点冰蓝），没做白/冰蓝随机。
9. 原版没有“骨头”材料：号角配方改为 1 根象牙（或犀牛角、Odyssey 的猛犸象牙）+ 10 寒铁。不是霜巨人戴号角时得到另一个能力“狂猎号角”（只给冬狼加成）；火巨人戴也是这样（冬之号角写在霜巨人本源上）。
10. “非玩家制造”的判断：从 1.30.0 起记录殖民地做出来的武器护甲（品质由殖民地生成的），更早做的会被算成战利品。“打退袭击”= 敌对派系的袭击集团在玩家主地图上被移除且没有能动的成员。
11. 雪原行者只改走路的移动消耗，寻路仍按原版成本绕开深雪。
12. 阶序的“殖民地”= 同一张地图上的自由殖民者和奴隶；氏族之首按“近战、手工里较高的那一项”比；挑战首领用原版“辱骂”互动（失败时直接给旧首领“被辱骂”记忆）。
13. 塞伊 1000 点含霜巨人的检查把出现概率固定为 100%（测试用随机数），游戏里是 35%（季节倍率另算）。
14. 设置放在左栏“神裔影响天气”之后的“巨人”分组。

## 六、需要 Peter 决定的点

1. 火巨人渲染 2.0、霜巨人 1.4 是否统一？（A：火巨人也改 1.4，和泰坦、炎魔的“显示小于体型”一致；B：霜巨人改 2.0，和火巨人一样大；C：保持现状）
2. 寒霜法杖的“寒霜缠身”要不要也对霜巨人无效？
3. 霜巨人掠夺者的出现概率（800 点以上 35%，冬季 70%）是否合适？
4. 畏火的舒适上限 -22℃ 让霜巨人在约 4℃ 以上就嫌热，夏天很难受。是否保留？
5. 号角配方（象牙 1 + 寒铁 10）是否可以，还是要换别的材料？
6. 锻工外衣改穿中层是否可以？
7. 霜巨人战帮挂在费伦拓荒同盟下面是否可以？
