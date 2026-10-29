# DND边缘大陆 1.22.0「物品与炼金」实现报告

需求：`ArtSource/Items-1.22.0-spec.md`。旧版备份：`ArtSource/backup-1.21.0/`（本次没有改动备份）。
编译：`Source/Build.ps1`（pwsh 7），主程序集与神兽血裔联动程序集都 PASS。

## 1. 进度说明

上一次会话完成了 A、B1、B2、B6、B5，并且已经写完 B3 卷轴和 B4 奇物的代码、定义、贴图和检查，只是还没跑测试。本次会话：

1. 确认 B3/B4 半成品能编译、检查全部通过（第一次 Core 停在强酸瓶，见 4.2；重跑 PASS 2428）。
2. 补上 C 默认值里缺的部分：**抽巨魔血冷却的 `LP_RecentlyBled` HediffDef 之前根本没有定义**（`Items.Give` 找不到定义就静默返回），也就是冷却从来没生效过。已补定义并加检查。
3. 成品原来没有任何商队在卖（种族装备、药水、卷轴、奇物、雷鸣石、驱散粉尘），补上货单。
4. D 联动：神兽血裔（饕餮药水翻倍、句芒省草药、寒铁克白泽）、Isekai（新 Bridges/Isekai + 宝箱挂钩）、VPE（确认已经覆盖，补一项护盾检查）。
5. 版本号、About.xml、使用说明.txt（内容一览 + 详细说明各一节“物品与炼金”、更新日志 1.22.0）。
6. 测试：新增 `Test.ps1 -Isekai`，以及 C/商队/联动/存读档的检查。

## 2. 改动文件

| 文件 | 改动 |
|---|---|
| `Source/Items.cs` | 公共工具（Def 缓存、限时状态、种族判断、联动挂钩表）、设置项“抽巨魔血冷却”、`ItemsState`（延后动作、巨魔皮每日修复）、`ItemsMap`（识破/魔杖揭示、防护邪恶结界）、伤害倍率、驱散护盾腰带与隐形揭示 |
| `Source/ItemsAlchemy.cs` | 炼金速度按智识与医疗平均；智识经验一半转给医疗 |
| `Source/ItemsPotions.cs` | 八种药水效果、巫妖/造物不能喝、巨魔再生剂与血瘾、英雄药水免恐惧、隐形攻击现形、巨人之力放大、巨魔被火/酸标记 |
| `Source/ItemsThrowables.cs` | 炼金投掷物、雷鸣石、黏胶袋、驱散粉尘、投弹手与掉落 |
| `Source/ItemsMaterials.cs` | 六种材料：熔岩结晶、龙鳞蜕、巨魔皮、秘银、精金、寒铁，矮人采秘银×2 |
| `Source/ItemsRaceGear.cs` | 十件种族装备、种族制作限制、种族加成属性、装备附带能力、饰品层 |
| `Source/ItemsScrolls.cs` | 八种卷轴、失误规则、防护邪恶结界、石化与石像、巫妖抄写（精神力扣除） |
| `Source/ItemsWondrous.cs` | 次元袋、精灵斗篷/靴、疾行靴、心灵屏障头环、戒指、健康护符、伊奥恩石、探测魔杖 |
| `Source/ItemsIsekai.cs` **（本次新增）** | Isekai 地下城宝箱额外掉落：按类型名反射挂到 `IsekaiDungeons.TreasureChest.Award`，不引用其程序集 |
| `Source/ItemChecks.cs` | 1.22.0 全部检查；本次加了 C 默认值、商队货单、Isekai/VPE 联动、存读档（抽血冷却、石像、疾行靴冷却），以及 `-lichitemsonly` 模式 |
| `Source/Checks.cs` | 挂入物品检查与存读档；本次：`FullModList` 在启用 Isekai 时也为真；新增只跑物品检查的分支（跳过 UI 截图） |
| `Source/Test.ps1` | 本次新增 `-Isekai`：Core 档插入 Isekai 与地下城附加包，传 `-lichitemsonly` |
| `Source/SharedComponentChecks.cs` | 本次：对照组“没有基因的人照常死亡”改用 99999 伤害（Isekai 放大部位生命值后 100 点打不穿大脑） |
| `Source/MinotaurGithChecks.cs` | 本次：迷宫感移速 +10% 在完整列表/Isekai 下只要求比例在 1.05～1.12 |
| `Source/Core.cs`、`Factions.cs`、`CommonRaces.cs`、`MindFlayer.cs`、`Minotaur.cs`、`ForceDragon.cs` | 上次：设置页、抽血冷却、巨斧野蛮重击、触须披风心灵震爆+2、角盔冲撞、驱散让龙偏折失效 |
| `Source/FactionChecks.cs`、`RemoldingChecks.cs` | 上次：失败信息补充诊断数据，不改判定 |
| `Source/Build-MoltenDefs.ps1`、`Defs/MoltenGuard.xml` | 研究页签“熔火锻造”改名“DND边缘大陆”（defName 不变） |
| `Defs/Items_*.xml`（7 个） | 全部新物品、研究、配方、Hediff、能力。本次：`Items_Potions.xml` 补 `LP_RecentlyBled`；`Items_Throwables.xml` 驱散名单补 `VPE_GuardianSkipBarrier`；`Items_Materials.xml` 寒铁描述补白泽 |
| `Patches/Items.xml` | 材料进货（上次）；本次加入七个派系商队的成品货单 |
| `Bridges/Isekai/Patches/IsekaiTraders.xml` **（新）** | 只在启用 Isekai 时加载：原版奇物商人（`Caravan_Outlander_Exotic`、`Orbital_Exotic`）卖治疗药水 |
| `LoadFolders.xml` | 加 `IfModActive="jellycreative.isekaileveling"` → `Bridges/Isekai` |
| `Bridges/MythicBloodlines/Source/MythicBridge.cs` | 饕餮治疗药水×2、句芒炼药退还一半草药、寒铁对白泽 +25% |
| `Bridges/MythicBloodlines/Source/BridgeChecks.cs` | 对应联动检查 |
| `Textures/Items/**`（76 张） | 上次用 `ArtSource/RaceArt/draw_item_art.py` 画：物品、投掷物飞行图、材料三档堆叠图、炼金桌三向、能力图标、伊奥恩光点、石像 |
| `About/About.xml` | 1.22.0，描述补一句 |
| `使用说明.txt` | 标题 1.22.0；设置页说明；内容一览“物品与炼金”和联动；详细说明【物品与炼金】；更新日志 1.22.0 |

## 3. 与需求的差异

1. **研究页签**：没有新建页签，把原来的“熔火锻造”页签（defName `LP_FireGiantTech`）改名“DND边缘大陆”，改动最小，旧存档不受影响。
2. **炼金技能**：`workSkill=Intellectual`，速度用新属性“炼金速度”按智识与医疗的平均等级算；经验的一半转给医疗。
3. **隐形药水**：用的是原版核心的 `HediffComp_Invisibility`（不需要 Anomaly），所以不需要替代方案。
4. **精灵靴**：原版没有“夜间潜入触发警报”这一机制，只做了“走动不吵醒人和动物”。
5. **矮人符文盾**：没有用 VEF 的盾牌框架，自己写了远程格挡（25%，山体下 45%，不挡近战），也就是需求里“护盾腰带式格挡”的备选方案。这是上一次会话的实现，没有留下不用 VEF 的原因记录；好处是不依赖 VEF。
6. **饰品栏位**：本模组环境下的 VEF 没有饰品层，自建了 `LP_Accessory` 服装层（戒指、护符、伊奥恩石、骨质护符）。戒指一次只能戴一枚。
7. **伊奥恩石**：做成一种物品，生成时随机七种技能之一（需求允许的“通用石”方案）。
8. **寒铁**：神兽血裔里没有九尾血脉，只对白泽生效。
9. **Isekai 宝箱**：Isekai 地下城附加包的奖励是 C# 里掷出的，没有 XML 掉落表可以补丁，所以用反射挂到 `TreasureChest.Award`：开箱时按宝箱等级 40%/55%/70%/85% 概率、1/1/2/3 次额外掉落（治疗药水、炼金之火、传送/火球/防护邪恶/识破卷轴；三级以上宝箱每次有 1/3 机会改出强效治疗药水、石化或复生卷轴）。“奇物商人”Isekai 本身没有，按原版奇物商人处理。顺带发现：Isekai 自己给奇物商人加魔核的补丁写的是 `Caravan_Outlander_ExoticGoods`/`Orbital_ExoticGoods`，原版并没有这两个名字（原版是 `Caravan_Outlander_Exotic`/`Orbital_Exotic`），那两条补丁实际不生效；这不是本模组的问题，没有改它。
10. **VPE**：VPE 的隐身灵能加的就是原版“心灵隐形”，已被识破/魔杖/驱散的揭示覆盖；护盾类（Overshield、Fire/Ice/Death Shield、动物护盾、守护跃障）被驱散粉尘直接移除。
11. **数量与配方**：需求没给具体数量的地方按原版量级定：投掷物一组用 20 化学燃料 + 10 布/钢，圣水 15 白银 + 2 草药，黏胶袋 15 布 + 10 化学燃料，寒铁 30 钢 + 10 白银 → 10（保持 3:1），骨质护符 10 人皮 + 20 白银。
12. **治疗药水止血**：把出血伤口视为一次简单包扎（质量 0.5），不是删除伤口。

## 4. 测试矩阵

| 档位 | 结果 |
|---|---|
| `Build.ps1` | 主程序集 PASS，神兽血裔联动 PASS |
| Core `Test.ps1`（最终代码） | **PASS 2481**（加入新检查后 3 次运行都 PASS，其中 2 次在最终主程序集上） |
| `-Mythic`（最终代码） | **PASS 2593**；中间一次 FAIL“吞噬矮人”（见 4.2），重跑 PASS |
| `-Isekai`（物品与联动 + 存读档） | **PASS 540**，日志无红字；包括一次真实的 Isekai 宝箱开箱、奇物商人货单 |
| `-Full`（完整 98 项模组列表） | 本模组检查 **PASS 2645**；脚本退出码 1，原因是日志里的第三方报错（GD3 静态构造、Character Editor `[Ref 78A3A6EB]`、`colonist_booze_tracker` 空键），与 1.20.0 报告相同，日志里没有本模组的报错。VPE 护盾驱散在这一档实测通过 |

说明：`-Full` 和 `-Isekai` 跑在加“寒铁克白泽”联动之前的程序集上；之后只改了联动程序集（Isekai 档不加载它）和一句寒铁描述，`-Mythic` 与 Core 已在最终代码上重跑通过。

需求 E 各项的覆盖：

| 需求 | 检查 |
|---|---|
| 每个新 Def 加载、有贴图、配方与研究前置 | 药水、投掷物（含飞行贴图）、材料（三档堆叠）、种族装备、卷轴、石像、奇物、伊奥恩光点；研究费用/前置/页签；炼金桌四向贴图 |
| 药水效果与时长；巫妖/造物不能喝 | `CheckPotions`（上次）全部通过 |
| 炼金之火、强酸瓶命中巨魔后再生中断、复活被阻止 | 通过（强酸夹具本次加固，见 4.2） |
| 圣水对造物和巫妖有效、对活人无效 | 通过 |
| 驱散粉尘让护盾腰带与龙偏折失效 30 秒后恢复 | 通过；另加隐形、VPE 护盾 |
| 传送、复生（1 天内成功；超过 1 天、巫妖尸体、无脑尸体失败）、石化与复原、识破解除支配 | 通过（传送用临时口袋地图测“外地 → 主基地床边”） |
| 卷轴失误概率 | 公式 + 2000 次统计 |
| 次元袋、秘银重量、精金建筑伤害、寒铁加成 | 通过 |
| 抽血冷却 | **本次新增**：默认 3 天、冷却中不能再抽、结束后可抽、设置改成 5 天生效、存读档保留 |
| 种族装备本族加成只在本族生效 | 通过 |
| 新矿脉在地图生成中出现（多次统计） | 6000 次原版矿脉抽选：秘银 > 精金 > 0，均少于铀 |
| 有/没有神兽血裔、Isekai 时都能加载无红字 | Core（都没有）、-Mythic、-Isekai、-Full；联动文件夹“恰好在启用时加载”各有检查 |
| 存读档后限时 Hediff、冷却、石像正确 | 巨人之力/再生剂剩余时间（上次）；**本次**加抽血冷却、石像（同一个人、读档后仍可复原）、疾行靴能力冷却 |

### 4.2 偶发失败记录

- **强酸瓶（本次第一次 Core）**：`acid vial marks the troll with acid and stops regeneration`。强酸按锐器护甲结算，测试巨魔随机生成的衣服偶尔把伤害整个挡掉（伤害 0 就不会留“酸蚀”）。测试改为先脱掉巨魔的衣服；这是推断出的原因，之后 Core 没有再出现。玩法没改。
- **吞噬矮人（-Mythic 一次）**：`downed Faerun dwarf can be devoured: 心灵失聪者的大脑无法吞噬`——随机生成的矮人带了心灵失聪，属于需求里提到的老随机用例，重跑通过，没有改。
- **贡品、重塑池伤势比例**：本次的 Core/Mythic/Isekai/Full 运行里都没有出现。
- **Isekai 档里的老检查**：最初用 `-Isekai` 跑整套 `-lichtest`，依次在一次性冷却对照组、精灵衰老、牛头人迷宫移速、上古脑唤醒、熔核禁卫过载上失败——都是 Isekai 随机改人物数值（体质、衰老、移速固定加值）让夹具假设失效，每次换一项。前两项已按 1.20.0 的 `FullModList` 办法放宽，之后改为 `-Isekai` 只跑物品与联动检查（`-lichitemsonly`）。其余老检查仍然不适配“Core + Isekai”，`-Full` 档里它们照常通过。

## 5. 需要 Peter 决定的点

1. **商队货单**（`Patches/Items.xml`）：数量和出现概率是我定的，比如复生卷轴只有教会牧师带、约五分之一的概率出现（`-3~1`），隐形药水只有黑网卖。要不要调整？
2. **Isekai 宝箱额外掉落**：概率 40%～85%，三级以上宝箱可能出复生卷轴（每次额外掉落约 8%～9%）。偏多还是偏少？
3. **矮人符文盾**用的是自己的格挡，没接 VEF 盾牌框架。要不要改用 VEF（会多一个 VEF 依赖或条件补丁）？
4. **精灵靴**的“夜间潜入不触发警报”原版没有对应机制，现在没做。需要的话得先定义“警报”指什么（比如不触发袭击预警、不惊醒守卫）。
5. **`-Isekai` 测试档**只跑物品与联动检查。要不要以后逐步把老检查也改成兼容 Isekai？
6. **复生卷轴 1 天、抽血冷却 3 天（设置页 1～15 天）、触须披风 -2 心情**按需求 C 的默认值做了，确认即可。
