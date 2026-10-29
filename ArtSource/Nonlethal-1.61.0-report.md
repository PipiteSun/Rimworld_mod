# 1.61.0 制伏武器 — 实现报告

需求：ArtSource/Nonlethal-1.61.0-spec.md（原文写 1.62.0；计划中的 1.61.0 夺魂仪式已暂停，本次用 1.61.0）。
备份：ArtSource/backup-1.60.0（改动前整份模组，不含 ArtSource，2780 个文件）。

## 完成内容

- **两种制伏伤害**（Defs/Nonlethal.xml，worker `LichPhylactery.DamageWorker_LPSubdue`）：
  - `LP_Subdue` 制伏打击（钝击护甲类），用于短棍、束缚网、各武器的近战敲击；
  - `LP_SleepMagic` 安眠魔法（无护甲），用于安眠箭、安眠法杖、安眠粉囊；爆炸时有淡紫粉尘和烟雾音效。
  - worker 只给 `LP_Subdued` 加严重度：没有伤口、不流血、不断肢、不碰衣物；建筑和物品不受影响。
- **增量公式**：伤害 × 制伏伤害倍率 ÷ (40 × 体型 × 生命值倍率)。安眠魔法再 × (1 − 心智抗性)。护甲：躯干天生护甲 + 覆盖躯干的衣物护甲 − 穿甲，按 (1 − 0.75 × 有效护甲) 折算。上限 1.3。
- **免疫**：机械体、非血肉、死灵造物、巫妖（`Items.Undead`，含巫妖基因），飘字「免疫制伏」；心智抗性 ≥100% 时安眠魔法同样「免疫制伏」；已倒地的目标飘字「已经昏迷了」，不再增加。
- **状态 `LP_Subdued`「制伏」**：晕眩（移动 ×0.9，操作 −10%）→ 0.4 摇摇欲坠（意识最多 70%，移动 ×0.7）→ 0.7 快撑不住（意识最多 40%，移动 ×0.5）→ 1.0 昏迷倒地（意识最多 10%）。不能治疗、不感染、没有疼痛。`HediffComp_SubdueRecover`：最后一次受击后 2500 刻（1 小时）内不消退，之后每小时 −0.1；鼠标提示显示还要多久开始消退。
- **`LP_Netted`「缚网」**：移动 ×0.5，6 秒（360 刻）。
- **五件武器**（描述都以规定的「非致命：……适合抓俘虏。」结尾）：
  1. `LP_SubduingCudgel` 制伏短棍：近战，棍头 12 / 戳击 9，冷却 1.6 秒；木质材料 40 + 钢 20；符文铁砧。
  2. `LP_NetCrossbow` 缚网弩：射程 22，瞄准 1.2 秒，冷却 2.6 秒；投射物 `LP_Proj_Net`（`Projectile_LPNet`）：制伏 8 + 眩晕 150 刻 + 缚网 360 刻；木 50、钢 30、布 30；符文铁砧。
  3. `LP_SleepBow` 安眠短弓：射程 26，瞄准 1.0 秒，冷却 1.8 秒；`LP_Proj_SleepArrow` 安眠 10；木质材料 60 + 秘银 3；符文铁砧。
  4. `LP_SleepStaff` 安眠法杖：继承 `LP_StaffBase`，`CompProperties_ArcaneWeapon` G 组、`Verb_ArcaneShoot`，投射物 `LP_Bolt_Sleep`（`LP_ArcaneBullet`）安眠 16 × 魔力系数；射程 28，瞄准 1.0 秒，冷却 2.2 秒；木 40、秘银 8、零部件 1；附魔台；研究要制伏战术 + 奥术法杖。
  5. `LP_SleepDustPouch` 安眠粉囊：照原版手雷写法反复投掷，射程 12，冷却 8 秒；`LP_Proj_SleepDust`（`Projectile_Explosive`）半径 2.4、安眠 22，伤到范围内所有血肉生物（含友军）；布 20、秘银 2、草药 2；符文铁砧。
- **研究** `LP_Research_Subduing` 制伏战术：DND边缘大陆页签 (1, 4.9)，前置符文锻造 (0, 1.4)，800。
- **猎人排除**：拿五件之一作主武器时 `WorkGiver_HunterHunt.HasHuntingWeapon` 返回 false。
- **设置**「制伏伤害倍率」50%–200%（步长 5%），默认 100%，放在“枪械”分组「法杖每发心灵熵」下面；字段 `Settings.subdueDamagePercent`，Scribe 写在 `ExposeGuns` 末尾（键 `subdueDamagePercent`）。设置分组数仍是 19。
- **器械师门槛**：Bridges/VPE/Source/Artificer.cs `RequiredAbility` 加 5 条，全部「奥术工坊」；描述自动追加“制作需要：器械师「奥术工坊」。”。
- **商人**（Patches/Nonlethal.xml）：原版奴隶贩 `Caravan_Outlander_PirateMerchant` 和黑网商队 `LP_Caravan_BlackNetwork` 各加两个 `StockGenerator_RareArcane`：25% 一根制伏短棍、25% 一把缚网弩。
- **贴图**：Textures/LP/Nonlethal/ 9 张（SubduingCudgel、NetCrossbow、SleepBow、SleepStaff、SleepDustPouch 128px；Proj_Net、Proj_SleepArrow、Proj_SleepBolt、Proj_Dust 64px），脚本 ArtSource/RaceArt/draw_nonlethal.py（PIL，粗描边、平涂 + 暗边亮边、低饱和、淡紫光点），预览 ArtSource/Preview/Nonlethal-1.61.0.png。
- **文档**：About.xml 版本 1.61.0 + 描述末尾一句；使用说明.txt 概览一行、第二部分【1.61.0 制伏武器】（紧跟【1.60.0 迷锁地牢】）、更新日志一行。两份文件都和 backup-1.60.0 比对过：About.xml 只变了 modVersion 和 description 两行；使用说明.txt 只多了 12 行（概览 1、小节 9 + 空行 1、日志 1），其他行一行没动。

## 决定

- 用自有状态 `LP_Subdued`，不用原版疼痛 / 眩晕：原版钝击会骨折、打头可能致死。
- 意识上限 10% 而不是 0：意识 0 会致死；10% 已经低于醒着的门槛，人会倒地。
- 不看疼痛（止痛药没用）；体型和生命值倍率都算（火巨人体型 2 × 生命值 1.5 → 一下只有人类的 1/3）。
- 亡灵和机械免疫；精灵按心智抗性减半（读 `DNDMind.Resistance`）。
- 安眠粉囊伤友军；NPC 派系不用这些武器（武器标签只有 `LP_Nonlethal`，没有任何 PawnKind 引用）；不加新能力；猎人不拿它们打猎。
- 交易标签只留 `LP_NonlethalStock` / `LP_NonlethalStaff`，普通武器商不卖，只有上面两种商队按概率进货（复用已有的 `StockGenerator_RareArcane`，不加新类）。
- 每件武器的近战敲击（弩托、弓臂、杖身）也是制伏打击：拿着这些武器的人不会误杀。
- 护甲按期望值折算、不掷骰，也不磨损衣物（规则说“不损坏衣物”；用原版护甲函数会磨衣服）。
- 束缚网的眩晕和缚网对任何活着、没倒地的目标都生效（网是实物，机械体也会被缠住）；网上那 8 点制伏伤害照常受免疫规则限制。

## 偏差（以设备上的实际为准）

1. **版本号**：1.61.0（需求写 1.62.0）。
2. **移动减益改成乘法**（×0.9 / ×0.7 / ×0.5），不是 −10% / −30% / −50% 的加法：意识被压到 40% 时移动能力本身也会掉，再减 0.5 就到了倒地线——第一轮测试里人在 0.9 就倒了。改成乘法后要到 1.0 才倒（测试有专门断言）。缚网同样改为 ×0.5。
3. **安眠法杖叠加 1.29.0 法杖规则里的目标倍率**：已有补丁 `ArcaneTargetFactor`（Pawn.PreApplyDamage 前缀）对所有魔力武器生效——寒铁护甲或有心智抗性 −25%、符文灼印 +15%。所以安眠法杖打精灵是 16 × 0.75 × 0.5；安眠短弓和粉囊只吃 ×(1 − 心智抗性)。这是“遵循 1.29.0 法杖规则”的直接结果，没有为它开例外。
4. **深渊熔炉也能做**：1.54.0 起 `LP_InfernalForge` 本来就能做所有符文铁砧的配方，四件铁砧武器也跟着出现在那里。
5. **“单手”**：原版没有单手标记；短棍只是一件轻型近战武器（质量 1.2）。
6. **木质短棍实际伤害**：工具伤害 12 按材料的钝击倍率和品质缩放（原版规则），所以用木头做的短棍一下略低于 0.30；测试用的是规定伤害 12。
7. **研究坐标** (1, 4.9) 本来就空着，没有右移。
8. **手动攻击倒地目标**：每下飘字「已经昏迷了」、不加严重度，但强制攻击的命令本身不会被取消（小人会一直敲到你取消命令）。

## 新 defName

- DamageDef：`LP_Subdue`、`LP_SleepMagic`
- ToolCapacityDef / ManeuverDef：`LP_SubdueStrike`
- HediffDef：`LP_Subdued`、`LP_Netted`
- ThingDef（武器）：`LP_SubduingCudgel`、`LP_NetCrossbow`、`LP_SleepBow`、`LP_SleepStaff`、`LP_SleepDustPouch`
- ThingDef（投射物）：`LP_Proj_Net`、`LP_Proj_SleepArrow`、`LP_Bolt_Sleep`、`LP_Proj_SleepDust`
- RecipeDef（recipeMaker 生成）：`Make_LP_SubduingCudgel`、`Make_LP_NetCrossbow`、`Make_LP_SleepBow`、`Make_LP_SleepStaff`、`Make_LP_SleepDustPouch`
- ResearchProjectDef：`LP_Research_Subduing`
- C# 类：`Nonlethal`、`DamageWorker_LPSubdue`、`HediffCompProperties_SubdueRecover` / `HediffComp_SubdueRecover`、`NonlethalShotExt`、`Projectile_LPNet`、`NonlethalNoHunting`

## Harmony 补丁

| 方法 | 类型 | 快速返回 | 调用频率 |
|---|---|---|---|
| `WorkGiver_HunterHunt.HasHuntingWeapon(Pawn)` | Postfix `NonlethalNoHunting` | `__result` 已是 false 就直接返回；否则只查主武器 defName 是否在 5 个名字的 HashSet 里 | 低：狩猎工作扫描和打猎任务检查时，每个猎人每次几次 |

其他都不是补丁：伤害 worker 只在制伏伤害命中时运行；恢复 comp 只在带“制伏”状态的人身上跑（每个 hediff 计时间隔一次，就是一次比较和一次乘法）。安眠法杖复用已有的法杖补丁（`ArcaneTargetFactor`、`ArcaneCommandDisable`、`StaffAIPickup`、`ArcaneCraftQuality`），没有新增负担。

## 测试

- `Test.ps1 -Only Nonlethal`：**PASS 11 条**（最终版本连续两次通过）。内容：
  1. 定义 / 贴图 / 研究 / 配方 / 不进 NPC 武器和普通交易标签 / 设置分组仍是 19；
  2. 人类挨短棍（12）：每下 +0.30，第 4 下倒地，不到 1.0 不会提前倒地，零伤口、意识 ≤10%；再打 20 下没有变化，还活着；
  3. 恢复：1 小时内不消退（600 刻后仍是 1.300），之后每小时 −0.1（500 刻 → 1.280），低于 1.0 醒来；
  4. 精灵（心智抗性 50%）吃安眠魔法 0.125，人类 0.250，短棍对精灵不减；机械体（Mech_Scyther）、死灵造物、巫妖（巫妖基因）免疫；
  5. 体型 2 增量减半（公式）；火巨人（体型 2 × 生命值 1.5）一下 +0.100；
  6. 真实射出一张束缚网：眩晕、缚网 360 刻、移动 100% → 45%、+0.20 制伏、无伤口；
  7. 短弓算猎具；安眠短弓 / 缚网弩 / 安眠法杖不算；
  8. 敌人挨 3 发安眠魔弹（16）倒地、活着、无伤，可以抓；抓走后仍带“制伏”，醒来还是俘虏；
  9. 安眠粉囊爆散（22，半径 2.4）：中心和 2 格外的自己人都 +0.55，5 格外的不受影响，无伤口；
  10. 设置 50% → +0.15、200% → +0.60；
  11. 存读档：严重度与最后受击时刻都保留。
  - 第一次跑时第 9 条失败过一次：测试人摆在了测试夹具保证空旷的 9×9 范围外（随机地图上可能是石墙或树）。已把它们挪进空旷区，之后两次都通过。另一处测试改动：原先要求铁砧配方只能在铁砧上做，因为偏差 4 改成“铁砧能做”。
- `Test.ps1 -Only ResearchTab`：**PASS 6 条**（新研究不重叠，前置在左边）。
- `Test.ps1 -VPE -Only Nonlethal`：**PASS**（3 条：桥已加载 + 2 条新检查——五件都要奥术工坊且描述写明；制作 / 智识 20 但没有奥术工坊的人做不了安眠法杖和短棍，设置关掉后可以，学会奥术工坊后可以）。
- 没有跑完整套件（按要求）。每次构建和测试前都确认过 RimWorldWin64 没在运行。

## 游戏里还没亲眼看过的

- 贴图拿在手上的角度（短棍 −65°、法杖 −45° 沿用基类、弓弩横放）、地上和背包里的样子；投射物飞行时的朝向和大小。
- 粉囊爆散的紫色粉尘、烟雾音效、弩 / 弓的音效。
- AI 实际行为：征召后自动用安眠武器开火、目标倒地后会不会停手、强制攻击倒地目标时一直飘字的观感。
- 商队实际进货概率（只验证了补丁加载、没有报错）。
- 设置窗口里滑条的位置和文字。
- 和 Isekai、HAR 种族同时启用时的数值（只在 Core 档测过；HAR 是默认带的）。
