# DND边缘大陆 1.52.0 报告：五色真龙与通用传奇层

需求：`ArtSource/ChromaticDragons-1.52.0-spec.md`（第一、二、三、五、六、七节的 1.52.0 部分、第九节）。版本 1.51.0 → **1.52.0**。备份：`ArtSource/backup-1.51.0/`（本次改动过的原文件 + About.xml + 使用说明.txt + LoadFolders.xml）。开工时 RimWorld 没在运行。按 Peter 的新安排，**1.52.1（传奇怪）没有开始**，等 1.52.0 试玩之后再做。

## 一、文件

新增：
| 文件 | 内容 |
|---|---|
| `Source/Legendary.cs` | 通用传奇层：`LP_LegendaryExtension`（挂兵种或种族）、`LP_LegendaryRulesDef`（控制类名单）、`Legendary`（豁免、熟练、DC、FearImmune、Setup、抗性、动作、威仪、显示、击杀奖励）、`Hediff_LegendaryResistance`、`Hediff_LegendaryActions`、四个传奇动作（侦测 / 尾击 / 振翼 / 骇人一瞥）、`ChromaticFlight`（PawnFlyer 飞跃）、`MapComponent_Legendary`，以及拦截补丁（StunFor、TryStartMentalState、AddHediff、ShouldBeDowned/CheckForStateChange、MakeDowned 倒地即死、TryCastNextBurstShot 触发动作、GeneratePawn、Pawn.Kill、Isekai 威压光环） |
| `Source/ChromaticDragons.cs` | 五色龙规则：`LP_DragonExtension`、`Dragons`（颜色表、生成与外观、吐息、魅惑、豁免加值、撤退、来袭 / 勒索 / 付款 / 谈判 / 拒绝 / 交人、龙过境联动、龙巢与宝藏、战利品、元素抗性）、`Hediff_DragonBody`（充能吐息、魅惑、飞跃、撤退）、`GameComponent_Dragons`、`Letter_DragonExtortion`、两个 IncidentWorker、`MapComponent_DragonLair` + `LairActions`（15 个巢穴动作）、`GenStep_DragonLair`、`StockGenerator_DragonLairMap`、`CompUseEffect_DragonLairMap`、强度 StatPart、HealthScale / 渲染 / 剥皮 / 元素伤害补丁、设置 |
| `Source/LegendaryChecks.cs`、`Source/ChromaticDragonChecks.cs` | 检查组 `Legendary`（11 条，无需 HAR）、`ChromaticDragons`（22 条 + HAR 联动 1 条） |
| `Defs/Legendary.xml` | 传奇抗性 / 传奇点 / 惊惧 / 巢穴沙尘 / 巢穴黑暗 hediff、控制类名单、`LP_DragonCharmed` 精神状态、记录“屠龙”、传说“斩杀传奇” |
| `Defs/ChromaticDragons_Items.xml` | `LP_DragonPoison` 伤害、5 个“真龙之躯”、5 种龙鳞、龙牙、5 个真龙之心、5 种龙之血（药剂 + hediff + 炼金桌配方）、龙巢情报（由 `ArtSource/ChromaticDragons-gen_defs.py` 生成） |
| `Defs/ChromaticDragons_Events.xml` | 派系「五色龙群」、龙教徒、4 条背景故事、勒索信、两个事件、龙巢地点与 GenStep |
| `Patches/ChromaticDragons.xml` | 被魅惑借用原版“精神错乱游荡”思考树分支；费伦同盟杂货商队加龙巢情报库存 |
| `Bridges/HAR/Defs/ChromaticDragons_Race.xml` | 抽象父类 + 5 个 HAR 种族、5 个龙头型、3 个生命阶段、15 个兵种（生成脚本同上） |
| `Bridges/HAR/Defs/ChromaticDragons_Body.xml` | `LP_DragonBody`、`LP_DragonWing`、部位组龙尾 / 龙翼 |
| `Bridges/HAR/Textures/ChromaticDragons/…` | 5 色 × 3 向身体（256px）、15 张透明头 |
| `Textures/ChromaticDragons/…` | 龙鳞 5、心 5、龙之血 5、龙牙、龙巢情报、派系图标、龙巢地点图标 |
| `ArtSource/RaceArt/draw_dragons_152.py`、`ArtSource/ChromaticDragons-gen_defs.py` | 贴图脚本、定义生成脚本 |
| `ArtSource/Preview/ChromaticDragons-1.52.0.png` | 五色 × 三档（按游戏渲染缩放）+ 一个人做比例 |

修改（原文件在 backup-1.51.0）：
| 文件 | 改动 |
|---|---|
| `Source/MagicItemsCombat.cs` | 龙杀手 `IsDragon` → `Dragons.Is`（力能龙裔、五色龙、名字带 Dragon 的非人形生物；龙教徒这类人形兵种不再算龙） |
| `Source/AdventurerEpic.cs` | `Epic.ShowsCR` 认传奇单位；检视面板第一行“挑战等级 CR x · 冒险者等级 y” |
| `Source/DragonFlyover.cs` | 没有成年力能龙裔时调 `Dragons.OnFlyoverEnd`（25% 五色龙勒索），有力能龙裔时逻辑不变 |
| `Source/Adventurer.cs` | `PartHealthFactor`：传奇单位不吃冒险者部位生命 / 传奇血量 |
| `Source/Core.cs` | 设置存档 `ExposeChromatic`、右栏「真龙与传奇怪」 |
| `Source/Checks.cs` | 完整检查加 `CheckLegendary`、`CheckChromaticDragons` |
| `Bridges/HAR/Source/BridgeChecks.cs` | `RunDragons` |
| `About/About.xml`、`使用说明.txt` | 1.52.0；【1.52.0 五色真龙】说明、更新日志 |

## 二、数值（最终）

| | 青年 | 成年 | 远古 |
|---|---|---|---|
| 体型 / 渲染缩放 | 2.5 / ×2.0 | 4.0 / ×2.8 | 6.0 / ×3.8 |
| 生命倍率 | 6 | **24**（规格 16，见偏差 1） | 40 |
| 护甲 利 / 钝 | 40 / 30% | 55 / 40% | 70 / 55% |
| 咬 / 爪 / 尾 | 28 / 18 / 22（生命阶段 meleeDamageFactor 1 / 1.6 / 2.5） | 45 / 28.8 / 35.2 | 70 / 45 / 55 |
| 移速 | 4.6 | 4.8 | 5.0 |
| 吐息 | 3×9，锥 6 / 线 12 | 4×12，8 / 16 | 5×18，11 / 22 |
| 等级 | 10–12 | 18–20 | 31–35（传奇） |
| 传奇抗性 / 传奇点 / 威仪 | 0 / 0 / 无 | 3 / 3 / 18 格 | 3 / 3 / 24 格 |
| cp | 600 | 1500 | 3500 |

数值核对（自检里算，不打仗）：有效生命 = 生命倍率 × 100 ÷ (1 − 利器护甲)；“标准殖民者”每秒伤害 = 12 × 近战伤害系数（20 级：力量 20；40 级：再加 20 点传奇近战）。成年龙 5333 对 5 × 14.4 = **74 秒**；远古龙 13333 对 5 × 23.9 = **112 秒**（目标 60–150）。规格的 ×16 算出来是 49 秒，所以成年改成 ×24。

## 三、决定 / 偏差

1. **成年龙生命 ×24**（规格 ×16）：按规格第六节的核对公式，×16 只有 49 秒，低于 60 秒；×24 = 74 秒。远古 ×40 = 112 秒，不改。核对公式里的“生命 × 100”“殖民者每秒 12 点 × 近战系数”是我定的口径（规格没写标准装备的具体数字）。
2. **传奇单位不吃冒险者体质的部位生命和传奇血量**，传奇属性点全部投在近战：生命倍率已经写死在 XML，不然远古龙的 33 级体质 + 自动投的血量 / 承受会再乘一截、核对就失控。
3. **贴图**：身体贴图是整条龙（含头），每色预先上色；头型贴图是透明的；`skinShader Cutout`、生成后 `skinColorOverride` 白色。规格写的是“灰阶身体 + HAR 颜色通道 + 每色一个头”。改的理由：龙头放在人形头的位置上很难对准四个朝向，整条画进身体最稳；五种头的造型（后掠大角 / 独角 / 头冠 / 前伸弯角骷髅感 / 圆头鬃冠）都画在身体里。
4. **画面大小**：没有用 HAR 的按生命阶段改 drawSize（我没有去读 HAR 1.7 源码确认它是否支持），用的是规格允许的备选——根节点 `ScaleFor` 补丁按年龄档乘 2.0 / 2.8 / 3.8（不挂 RaceSizeExtension，所以不进巨躯规则）。
5. **吐息不是 AbilityDef**：在“真龙之躯”hediff 里每秒判定——每 6 秒掷 1/3 充能，充好且射程内有看得见的敌人就喷（成年力能龙裔优先）。这样敌方 AI 一定会用，不依赖原版 AI 施法。所以没有画 5 个吐息能力图标；hediff 在原版里没有图标，传奇抗性 / 传奇点 / 惊惧 / 被魅惑的图标也没画。
6. 吐息**不伤同派系**（护卫不会被自己的龙烧死）。毒气锥对绿龙自己的毒素免疫靠 `ToxicResistance +1`，没有对 Biotech 毒气单独写伤害免疫。
7. 疼痛倒地：每用 1 次传奇抗性，60 秒内不因疼痛倒地；只在真正的状态检查里消耗（WouldBeDowned 这类预判不消耗）。“真龙之躯”疼痛 ×0.6。
8. 外力精神状态：拦所有“不是心情引起”的精神状态（不区分施放者是谁）；自发崩溃由真龙之躯 `blocksMentalBreaks` 关掉。控制类名单按设备上的实际 defName：LP_FrostSlow、LP_Entangled、LP_Tanglefoot、LP_MagicSleep、LP_Dominated、LP_DivineDominated、LP_Petrifying、LP_PsychicDaze、LP_BaneFear、LP_FeatSentinelSlow、惊惧、巢穴黑暗 / 沙尘、麻醉、紧张性崩溃、灵能眩晕 / 失明。**LP_Chilled（寒气）没放进去**：它是一个按层数分阶段的 hediff，放进去会拦掉所有寒气，而不只是第 3 层冻僵。巫妖“死亡律令”对传奇单位的特殊处理没做（属于 1.52.1 一起做的范围更合适）。
9. 振翼“倒地 2 秒”用原版眩晕 120 刻；振翼后的飞行和飞跃都用原版 `PawnFlyer`。
10. 绿龙魅惑：新精神状态 `LP_DragonCharmed`，行为借用原版“精神错乱游荡”（到处走、不攻击），不是“走向绿龙”；30 秒，受伤立刻醒；豁免成功 24 小时免疫。
11. **没做**：白龙冰面行走（雪地不减速）、绿龙 / 黑龙水中行动、黑龙巢穴常驻黑暗、吐息以外的“优先攻击力能龙裔”（只有吐息选目标时优先）。
12. 骇人威仪的免疫：恐惧免疫统一成 `Legendary.FearImmune`（机械体、象人沉稳、号角、英雄药水、亡灵 / 巫妖、传奇单位自己）；没有找到“圣武士勇气灵光”的现成 hediff，没接；独眼巨人不算（它免疫的是凝视）。
13. 勒索：谈判只能谈一次（成功后重发一封信，只剩付 / 拒绝）；“交出一名殖民者”列出身价最低的 3 名供选择，交出的人按原版“被绑架”处理。付款从储存区按偏好拿（红蓝：金子、玉、艺术品；绿黑：银子），不够再拿别的值钱东西。
14. 来袭的护卫数量按威胁点数在区间内取；动物护卫（邪座狼、冬狼）狂暴、不进龙的 Lord。远古“每年一次”按绝对游戏刻计。冷却：IncidentDef 20 天 + 触发后再随机 20–30 天（除以 DND 事件频率）。
15. 撤退：成年以上、不在自己巢里、生命低于 35% 时离开地图，算“击退”（60% 显露它的巢）；击杀成年龙 30% 显露一座同色远古龙巢。
16. **龙巢地图**是简化版：原版地图中央挖一个 25×25 的石窟（按颜色用花岗岩 / 砂岩 / 石灰岩 / 板岩 / 大理石，东面一条洞口，岩顶），中央宝藏堆，巢主旁边“睡着”（等待任务），护卫两倍守点。醒来条件只做了“敌人走近 10 格”和原版 AI 自己的反应，没有专门写“开枪就醒”。宝藏金银珠宝按价值配（金 30%、玉 15%、金雕塑、其余银），魔法物品走 1.45.0 的随机池。
17. 龙巢情报只加在费伦同盟的“杂货商队”（LP_Caravan_FaerunGoods）上。
18. 真龙之心做成 5 种（每色一个），1 颗在炼金桌炼 3 瓶龙之血（规格没写数量）；龙之血卖价 1500。龙鳞：钢的利器 ×1.6、钝器 ×1、热 ×1（红、白 ×2）；电 / 毒 / 酸没有对应护甲统计，穿着对应龙鳞做的衣甲时该伤害 −50%。
19. 龙杀手的龙判定统一成 `Dragons.Is`，保留“名字带 Dragon 的非人形生物”（Isekai 生物扩展的真龙）。
20. Isekai：生成后设 `forgeLocked`；Isekai 的 NPC“威压”在源码里是 `AuraPressureAIComponent.IsEligibleCaster`，已用 Harmony 让传奇单位不放（找不到时补丁自动跳过）。
21. 没装 HAR 时“真龙与传奇怪”一组画成灰色并写“需要 Humanoid Alien Races”，但滑条仍可调（没有效果）。
22. 自检里的测试传奇兵种是代码里造的 PawnKindDef（注册进 DefDatabase，否则存读档报“找不到兵种”）；`Legendary.Ext` 的缓存按对象引用（Def 的哈希按 shortHash，测试兵种会撞）。
23. 报告文件名按协调要求写成 `Dragons-1.52.0-report.md`（规格写的是 ChromaticDragons-1.52.0-report.md）。

## 四、测试

| 命令 | 结果 |
|---|---|
| `Source/Build.ps1` | 主程序集、HAR、VPE、神兽血裔联动全部 PASS |
| `Test.ps1 -Only Legendary` | **PASS 11 条**，存读档，日志无红字 |
| `Test.ps1 -Only ChromaticDragons` | **PASS 23 条**（主程序集 22 + HAR 联动 1），存读档，日志无红字；最后一轮连跑 3 次都过 |
| `Test.ps1 -NoHAR -Only Legendary,ChromaticDragons` | **PASS 12 条**，无红字（真龙不加载、事件不触发） |
| `Test.ps1 -Only Adventurer` | **PASS 115 条**（和 1.51.0 一样） |
| 完整测试 / -VPE / -Isekai | 按要求没跑 |

覆盖：熟练加值（含 21–40 级）、同 DC 下高属性 / 高等级成功更多、无等级按 +0；传奇单位固定等级 / 六维 / CR 文本 / 无天赋 / 传奇点全投近战；传奇抗性拦眩晕、狂暴、LP_FrostSlow、疼痛倒地各一次并扣次数，0 次后不再拦，缺腿倒地不拦，每日恢复，巢穴 +1；传奇动作触发、扣点、4 秒回点、远处不触发；断尾 / 断翼；骇人威仪失败惊惧、成功 24 小时免疫、英雄药水免疫；击杀里程碑和 ≥10% 伤害的传奇点；倒地即死。五色成年龙的种族 / 身体 / 体型 / 生命 / 本色免疫 / 无需求 / 龙头 / 无装备；三个年龄档；红龙锥（侧面 2 格中、身后不中、豁免减半 12→6）、蓝龙线（偏 2 格不中、建筑受伤、机械 ×2）、白龙寒气 1 层、黑龙衣甲耐久 −30%；威仪、魅惑（受伤即醒）、火巨人 +2；来袭门槛与远古每年一次、白龙带狂暴冬狼；勒索要价 6%、谈判减半、付款先拿金子、拒绝 / 超时开打；龙过境联动 25%；龙巢地点与宝藏价值 / 魔法物品 / 重铸之石；巢穴动作的提示圈与“引出 20 格停”；剥皮出龙鳞 50 / 心 / 牙、不出肉，红鳞板甲热 ×2、利 ×1.6，龙之血、黑鳞抗酸，配方。

调试中修掉的问题：damageFactors 的 XML 写法（要写成 `<Flame>0</Flame>`）；CompShield.energy 不可访问（改反射）；NPC 的 Respec 会自动重投（改成直接全投近战）；测试兵种缓存撞哈希；生成的龙带原版随机旧伤（现在登场时治好、补回缺失部位）。

## 五、没在游戏里试过（请 Peter 试玩时看）

- **外观**：没有截游戏内图。龙身贴图挂在人形“Hulk”身体上，四个朝向的位置、大小（×2.0 / 2.8 / 3.8）、伤口贴花的位置、名字标签都只在预览图里看过。
- 真龙在真实袭击里的 AI：吐息频率、飞跃、撤退、护卫跟随；勒索信的按钮和一天超时；龙巢地点进入后的地图、巢主“睡觉”与醒来、巢穴动作的节奏。
- 事件在说书人下的实际出现频率；龙过境联动。
- 装了 Isekai / VPE 时（forgeLocked、威压光环跳过、生命 ×2 / 伤害 ×1.5）。
- 剥皮工作本身（只测了产物）、龙鳞装备的实际制作菜单。

## 六、提醒 Peter

启用 **Humanoid Alien Races**（创意工坊 839005762，放在 DND边缘大陆之前），完全重启游戏。最快看到真龙：开发者模式生成“成年红龙”等兵种，或用开发者工具触发“真龙勒索”“真龙来袭”（需要殖民地最高冒险者等级 ≥8，否则不会触发）。
