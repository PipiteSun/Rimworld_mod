# 深渊仪式：实现要求（1.54.0）

2026-10-27，本线程写。来源：[DND深渊与异界契约构思.md](DND深渊与异界契约构思.md) 第一批。Peter 03:33 定了排期：深渊与异界契约是第 3 项，**1.54.0 = 第一批 深渊仪式**，1.55.0 = 第二批 异界契约（要求等本版做完再写）。设备排在 1.52.1（传奇怪）和 1.53.0（物理职业第一批）之后。

所有设计决定由本线程做出，写在第十节，实现时照做，偏差写进报告。Peter 不需要回答问题。

**这一版不需要 HAR，也不需要 Isekai**。装了 HAR 时多一个可召目标（六臂蛇魔）。

---

## 零、现状核对（设备 1.52.0，只读，2026-10-27）

- **恶魔现有写法**（`Source/Fiends.cs`、`Defs/Fiends.xml`）：判定 `Fiends.Is(p)=Races124.Has(p,"LP_FiendCore")`；寒冰魔 `LP_IceDevil`、炎魔 `LP_Balor`；击倒即消散 `FiendBanishOnDowned`（`Pawn_HealthTracker.MakeDowned` 后缀）、死亡消散 `FiendBanishOnDeath`、`Fiends.Banish / BanishCorpse / Loot / DropAt / Defer`；常量 `ColdIronFactor=1.5`、`HolyFactor=2`；魔鬼之眼（`Fiends.seer` + 一组 Harmony）；炎魔袭击信件加“深渊裂口·”（`Fiends.abyssRaid`）。
- **传送门失控已经做了**（1.28.0，`Source/PortalEvent.cs`、`Defs/Events_Portal.xml`）：裂口建筑 `LP_PlanarRift`（`Building_PlanarRift` + `CompPlanarRift`，600 生命，8 小时一波，第 9 波后崩溃），数据驱动的来源位面 `PortalPlaneDef`（`LP_Plane_NineHells / ElementalFire / Feywild / Shadowfell`，`spawns` 列表含 `kind/count/fromWave/scale/maxTotal/side/special`），`side="thay"` 用塞伊派系，巨灵可以 `LP_CommandRift` 命令关闭，驱散粉尘关闭（`DispelClosesRift`），掉 `LP_PlanarResidue 位面残渣`，`PortalEvent.SpotOk` 选位置。**本版的“深渊裂口”就是加一个新的 `PortalPlaneDef`，不另写裂口建筑。**
- **神选血脉转换**（`Source/FaithRaces.cs`）：`FaithRaces.Convert(p,xeno)`（只换异种基因）、`CannotConvert(p)`（巫妖 / 夺心魔 / 巨灵 / 泰坦 / 巨人 / 奥术半神不能换）；提夫林已有 `Hediff_InfernalPact`（“地狱契约”增益，过期给 `LP_PactOverdraw`）——**名字已被占用**，本版不要再用 InfernalPact。
- **豁免**（`Source/Legendary.cs`）：`Legendary.Save(p,ability,dc,source)`、`Legendary.DC(caster,ability)`、`Legendary.Mod`。本版所有豁免都用它。
- **材料**：`LP_ColdIron 寒铁`、`LP_Mithral`、`LP_Adamantine`；原版 `Silver`；投掷圣水 `LP_HolyWater`（炼金）、信仰圣水 `LP_BlessedWater`（1.39.0）。
- **书**：耐瑟瑞尔卷轴是原版书（`BookBase` + `CompProperties_Book`，`thingClass=LichPhylactery.Book_Netheril`，效果在 BookOutcomeDoer）。恶魔学典籍照这个写。
- **研究页**（`Defs/Research.xml`，单页 `LP_FireGiantTech`）：信仰行 Y 2.8 已用 X 0/1/2/3（3 是 法袍织造）。**本版用 (4, 2.8)**。开工前再核对 1.52.1 / 1.53.0 有没有占用这个格子，占了就顺延到 (5, 2.8)。

---

## 一、外来者层（主程序集，新文件 `Source/Outsiders.cs`）

### 1.1 `LP_OutsiderExtension`（DefModExtension，挂在 GeneDef 上）

| 字段 | 类型 | 说明 |
|---|---|---|
| `plane` | 枚举 `Abyss / Hells / Feywild / Celestia / Elemental / FarRealm` | 来源位面（本版只用 Abyss / Hells） |
| `banishOnDown` | bool，默认 true | 击倒即遣返（**只对非玩家派系**，见 1.3） |
| `coldIron` / `silver` / `holy` | float | 克制倍率（被这种材料的武器打 / 被圣水打） |
| `nonMagicFactor` | float，默认 1 | 非克制、非魔法武器伤害倍率（本版只有影魔用） |
| `taintImmune` | bool | 免疫深渊腐化 |
| `levelRange` | IntRange | 作为敌人 / 仆从生成时的冒险者等级 |
| `trueNameChance` | float | 被击败时掉真名卷的概率 |
| `priority` | int | 一个人有多条带扩展的基因时取最大的 |

- `Outsiders.Of(Pawn)`：返回扩展；**按 pawn 缓存**（`ConditionalWeakTable` 或 `Dictionary<int,…>`，基因变化时清缓存：`Pawn_GeneTracker.Notify_GenesChanged` 后缀）。
- `Outsiders.Is(p)`、`IsDemon(p)`（plane=Abyss）、`IsDevil(p)`（plane=Hells）。

### 1.2 迁移现有恶魔（行为不变）

- `LP_FiendCore` 加扩展：`plane=Hells`、`coldIron=1.5`、`holy=2`、`banishOnDown=true`、`trueNameChance=.4`。
- `LP_BalorFlame` 加扩展：`plane=Abyss`、`priority=1`，其余同上（炎魔在 DND 里是深渊恶魔）。
- `Fiends.Is` 改成 `Outsiders.Is(p) && Outsiders.Of(p).banishOnDown`，现有 Harmony（消散、掉落、魔鬼之眼）全部照常工作。寒铁 ×1.5、圣水 ×2 的现有入口改成读扩展倍率。
- **验收**：`-Only` 现有寒冰魔 / 炎魔的检查全部照过（Races124 / Fiends 相关组）。

### 1.3 遣返规则（`Fiends.Banish` 泛化为 `Outsiders.Banish`）

| 谁 | 击倒 | 死亡 |
|---|---|---|
| 敌对 / 野生外来者 | 遣返（黑烟 + 掉落 + 按 `trueNameChance` 掉真名卷） | 尸体消散，同样掉落 |
| 玩家的**仆从**（限时召唤，`LP_AbyssThrall`） | 遣返（不掉落真名卷） | 同 |
| 玩家的**束缚者**（永久殖民者，`LP_PlanarBinding`） | **不遣返**，像普通殖民者一样倒地 | 尸体消散（“它回到了深渊”），装备和物品掉在原地，不能复活 |
| 戴了**位面锚链**的（6.1） | 不遣返，可以俘虏 | 正常留尸体 |
| 六臂蛇人（玩家开局 / HAR 种族） | 照现在（不受影响） | 照现在 |

### 1.4 克制

`Pawn.PreApplyDamage` 前缀（**先看 `Outsiders.Of(victim)` 为空就直接返回**）：

- 武器材质 `LP_ColdIron` → ×`coldIron`；`Silver` → ×`silver`；
- 圣水投掷物（`LP_HolyWater`）→ ×`holy`（现有逻辑）；信仰的光辉伤害 `LP_Radiant` 对外来者 ×1.25（新加）；
- `nonMagicFactor`：武器不是魔法物品（1.45.0 的魔法物品判断）且品质 < 杰作，也不是克制材质 → ×`nonMagicFactor`。

---

## 二、深渊腐化（每个人）

`Hediff_AbyssalTaint`，defName `LP_AbyssalTaint`，label **深渊腐化**，severity 0～1 显示成百分比。

| 阶段 | 范围 | 效果 |
|---|---|---|
| 低语 | 0.10–0.29 | 心情 -3“耳边的低语”（ThoughtWorker）；近战伤害 +3% |
| 阴影 | 0.30–0.59 | 近战伤害 +6%、疼痛阈值 +10%（PainShockThreshold）；社交影响 -10%；善神神眷获取 ×0.5（Faith 里加一个系数钩子） |
| 恶魔之印 | 0.60–0.89 | 外观：小角（头部附件，复用提夫林角贴图缩小、换色）+ 红眼；火焰伤害 ×0.8；**每季 10%** 精神崩溃“深渊狂乱”（复用原版 Berserk 精神状态，新 MentalStateDef 只换名字和信件）；费伦拓荒同盟好感每天 -1（殖民地里每有一个这阶段的人） |
| 堕落 | 0.90–1.00 | 到 1.00 触发**化魔**（2.2） |

- 0.10 以下不显示 hediff（没有就不加；到 0 移除）。
- “恶魔之印”以后不会自然下降；之前每天自然 -0.5%。

### 2.1 来源与去除（全部乘设置“腐化速度”25%～200%，再乘种族系数）

| 来源 | 腐化 |
|---|---|
| 深渊仪式主祭（一 / 二 / 三 / 四阶） | +5% / +10% / +20% / +35% |
| 护阵者 | 主祭的一半 |
| 炼魂（6.1） | +5% |
| 读恶魔学典籍 | 每小时 +0.5% |
| 喝恶魔血 | +8% |
| 魅魔梦境吸取的对象 | 每晚 +3% |
| 夸塞魔魔宠的主人 | 每天 +0.3% |
| 化魔边缘的“灵魂抵押” | +30% |

| 去除 | 腐化 |
|---|---|
| 在圣坛向善神祈祷（现有祈祷完成时） | -5%（不额外花神眷） |
| 饮用 `LP_BlessedWater` | -10%（给它加一个 IngestionOutcomeDoer） |
| 阿斯莫的治疗类能力作用在目标身上 | -8%（设备上有治疗之手就接，没有就跳过并写报告） |
| 在驱魔结界范围里睡觉 | 每天 -1% |

**种族系数**：六臂蛇人 0（`taintImmune`）；提夫林、坎比恩、魅魔 ×0.5；巫妖 ×0.5 且没有心情惩罚；夺心魔 / 半夺心魔、吉斯洋基 ×0.75；精灵 ×1.5；阿斯莫 ×2（阿斯莫到“阴影”阶段时天界启示能力不可用，降回 0.30 以下恢复）；动物、机械体不获得。系数写成一张 XML 表（`LP_TaintFactorDef` 或者挂在种族基因上的扩展），不要散在代码里。

### 2.2 化魔

腐化到 100%：

- 普通人（`FaithRaces.CannotConvert(p)==null`）→ 变成**坎比恩**（`FaithRaces.Convert(p, LP_Cambion)`，不可逆），腐化重置为 0.6，信件“XX 的身体终于向深渊屈服了”。
- 已经是坎比恩 / 提夫林 / 魅魔 → 得到 hediff `LP_AbyssFavored 深渊宠儿`（伤害 +10%；狄瓦、阿斯莫对它心情 -5），腐化停在 1.0。
- 巫妖 → hediff `LP_TaintedPhylactery 命匣受污`（复活时间 ×1.5），腐化停在 1.0。
- 其他不能换血脉的（夺心魔、巨灵、泰坦、巨人、奥术半神）→ 同“深渊宠儿”。
- **死亡时腐化 ≥ 0.9**：50% 在 6 小时后尸体起身成敌对劣魔（信件“它的灵魂化作了怨魂”），尸体消失。

---

## 三、束缚与仆从

### 3.1 仆从 `LP_AbyssThrall`

- HediffWithComps，带剩余时间（显示“还能留 2 天 3 小时”）。到时 → `Outsiders.Banish`（装备掉落）。
- 玩家派系、可征召，不计入殖民者心情（不会因为它死亡难过），**没有心情需求**（hediff 里把 mood 需求禁用；做不动就给一个 +100 的固定心情）。
- **不计入袭击点数**：`StorytellerUtility.DefaultThreatPointsNow` 计算殖民者部分时排除带这个 hediff 的人；做不动写报告。
- 同时最多 8 个（设置可调 0～16）。

### 3.2 束缚者 `LP_PlanarBinding`

HediffWithComps，severity = **服从度** 0～100，label“位面束缚（服从 72）”。

- 初始 = 仪式质量 × 100，最低 40（事件给的按事件写）。
- **每天衰减**（HediffComp，`CompTick` 每 2500 刻结算一次 = 每天的 1/24）：恶魔 -2/天。持有它的真名卷（地图上或殖民者背包里有一张写着它名字的真名卷）→ 衰减减半。
- **加**：“重申真名”——束缚者的主人（`binder` 字段，仪式主祭）对它做的新社交互动 `LP_ReassertName`，每天最多 1 次，+8；睡在束缚法阵上（6.3）每天 +3。
- **减**：主人死亡 -40；被殖民地的寒铁武器打到 -5（每次）；主人腐化 ≥ 0.6 时衰减 ×1.5（“主人也在堕落，它不再畏惧”）。
- **< 20**：信件提示一次（“它在试探束缚”）。**= 0：挣脱**——移除束缚，派系改成 `LP_AbyssHorde`，给一个 `LordJob_AssaultColony`，信件。
- 束缚者：**不育**（加原版 Sterile；魅魔例外，见 4.5）；年龄冻结（生物年龄不再增长）；可以升级（冒险者等级照常）。
- **召唤上限**：同时最多 `1 + 殖民地最高冒险者等级 / 5` 个束缚者（向下取整）+ 设置“额外上限”0～5。满了仪式菜单里束缚选项灰掉。

### 3.3 真名

- 每个外来者第一次生成时在 Pawn 上存一个真名（`Outsiders.TrueName(p)`：3～4 个音节随机拼，存到一个 GameComponent 的 `Dictionary<int thingIDNumber,string>`，不要给每个 pawn 加 comp）。
- **真名卷** `LP_TrueNameScroll`：一个物品，带 CompTrueName（`name`、`kind`=外来者 PawnKind、`pawnId`）。label 显示“真名卷：XX（巴路古拉）”。
- 用途（物品右键 / 能力）：
  1. 对**它记录的那个人**（是你的束缚者）：持有即衰减减半（3.2）。
  2. 对**它记录的那个人**（是敌人，在地图上）：使用者对它做魅力豁免对抗——它做魅力豁免，DC = `Legendary.DC(使用者, 魅力)`，失败立刻遣返；用掉卷。
  3. 仪式召唤**同种**外来者：成功率 +10%（不消耗）。
  4. 对深渊裂口使用：直接关闭裂口（消耗）。
- 价值 400 银。

---

## 四、种族与单位（`Defs/Abyss_Races.xml`，`Source/AbyssRaces.cs`）

全部是**原版人类 + 异种型**（夸塞魔是动物）。恶魔共用新本源基因 `LP_AbyssCore 深渊之躯`（判定 + 扩展 `plane=Abyss, coldIron=1.5, holy=2`；不吃不睡照 `LP_FiendCore` 的做法；裸体不在意）。可玩的坎比恩**不带** `LP_AbyssCore`（它是凡人生的）。

异种型：恶魔 `inheritable=false`、`factionlessGenerationWeight=0`；坎比恩 `inheritable=true`、`factionlessGenerationWeight` 0.5（原版人类按设备现有的权重，坎比恩只占很小一部分）。所有自定义基因 `selectionWeight=0`。代谢不用凑（恶魔不进玩家可编辑范围），坎比恩目标 -1。

### 4.1 劣魔 Dretch `LP_Dretch`

| 项 | 值 |
|---|---|
| 体型 | `SmallBodyExtension` 0.7 / 渲染 0.8；Body_Fat；灰白皮 `LP_SkinDretch`；小号恶魔角 |
| 基因 | `LP_AbyssCore`；`LP_DretchStench 恶臭`：2 格内其他人形心情 -2（ThoughtWorker，**按地图外来者登记表查，见第九节**）；`disabledWorkTags`：只留 搬运 / 采矿 / 清洁 / 粗加工（屠宰、切石、采集）；全局工作速度 ×0.8 |
| 敌人 | PawnKind `LP_AH_Dretch`，combatPower 35，等级 1–2，徒手 |
| 仆从 | 一阶仪式一次 3～6 只，存在 3 天 |
| 死亡 | 留一滩原版污物（Filth_Vomit 换色或新 filth `LP_Filth_Ichor`） |

### 4.2 夸塞魔 Quasit `LP_Quasit`（动物，魔宠）

- 新动物 ThingDef（不是人形）：体型 0.3，飞行外观（不需要真飞行，原版小动物移动），trainability None（不能训练、不能驯服，只能仪式得到），`LP_AbyssCore` 不适用 → 在种族 ThingDef 上挂 `LP_OutsiderExtension`（`Outsiders.Of` 也要查 `pawn.def` 上的扩展）。
- 召来时和主祭建立原版羁绊（`RelationsUtility.TryDevelopBondRelation` / 直接加 Bond 关系），跟随主人。
- 规则：不战斗时（最近 10 秒没有攻击 / 被攻击）隐身（原版 `HediffComp_Invisibility`）；主人心灵敏感度 +10%（主人身上的 hediff，魔宠在同地图时生效）；主人腐化每天 +0.3%。
- 死亡：主人心情 -8（原版羁绊动物死亡即可）。同一时间一个主人最多一只。
- 敌人：不做。

### 4.3 巴路古拉 Barlgura `LP_Barlgura`

| 项 | 值 |
|---|---|
| 体型 | 1.6 / 1.3（吃巨躯规则）；Body_Hulk；猿脸头部附件 + 獠牙；橙褐 `LP_SkinBarlgura` |
| 基因 | `LP_AbyssCore`；`LP_BarlguraMight`：近战伤害 +20%、负重 +40%；`LP_BarlguraLeap` 能力**跃击**：8 格内跳到目标附近（复用吉斯洋基 `Githyanki.Step` 位移），落地 1 格内敌人敏捷豁免 DC 13，失败倒地 1 秒（stun），冷却 6 小时，AI 会用；`LP_BarlguraStill` **静立隐形**：3 秒不移动不攻击 → 隐身，移动 / 攻击打破；禁用 智识 / 艺术 / 医疗 / 社交；嗜杀：看到处决心情 +5 |
| 敌人 | `LP_AH_Barlgura`，combatPower 140，等级 4–6 |
| 束缚 | 初始等级 5 |

### 4.4 哈兹魔 Hezrou `LP_Hezrou`

| 项 | 值 |
|---|---|
| 体型 | 1.8 / 1.4；Body_Hulk；蟾蜍头（头部附件盖住原头，宽口）；疣状绿灰 `LP_SkinHezrou` |
| 基因 | `LP_AbyssCore`；`LP_HezrouHide`：利 / 钝护甲 +20%；`LP_HezrouStench` **恶臭**：每 300 刻对 3 格内敌对人形做体质豁免 DC 13，失败 hediff `LP_Nauseated 恶心`（操作 -20%、移速 ×0.9，10 秒；不叠加；成功的人 1 分钟内不再检定）；水中行动（1.24.0 `AmphibiousExtension`） |
| 敌人 | `LP_AH_Hezrou`，combatPower 180，等级 6–8 |
| 束缚 | 初始等级 7 |

### 4.5 魅魔 / 梦魔 Succubus `LP_Succubus`（**可玩**，束缚者）

一个异种型，女性叫魅魔、男性叫梦魔（label 按性别显示，`labelFemale`/自定义）。生成时 70% 女性。

| 项 | 值 |
|---|---|
| 外观 | 人类身体；蝠翼（身体附件，复用炎魔翼形状缩小、换紫黑）、细尾、小角；肤色 `LP_SkinSuccubus`（正常肤色 60% / 绯红 40%） |
| `LP_AbyssCore` | 有，但**不带 Sterile**（这是唯一例外，见“生育”） |
| `LP_SuccubusGuise` **变形伪装**（开关能力） | 开启：隐藏角 / 翼 / 尾 / 肤色（这些 gene 的渲染节点不画，皮肤用普通肤色）；提夫林陌生人那类“看到异族”的心情不触发；非玩家派系交易按普通人。被魔鬼之眼（`Fiends.Sees`）或 5 格内的阿斯莫看到时自动关闭 |
| `LP_SuccubusKiss` **魅惑之吻**（能力，接触，冷却 1 天） | 目标感知豁免，DC = `Legendary.DC(魅魔, 魅力)`。失败：敌人 → 复用 1.52.0 绿龙的“被魅惑”精神状态 12 小时改成 60 秒（战斗用）；**囚犯** → 招募抵抗 -15（原版 resistance）；殖民者 → 与魅魔的关系 +20 意见。成功：24 小时内对该魅魔免疫 |
| `LP_SuccubusDream` **梦境吸取** | 每天 6 点结算（rare tick）：前一晚和魅魔同床（同一张床的另一人）→ 对方休息 -20%、腐化 +3%；魅魔恢复 10% 生命、心情“餍足”+6 两天；连续 3 天没有吸取 → 心情“饥渴”-4。营养需求 ×0.2 |
| 抗性 | 火焰、毒素伤害 ×0.5；社交影响 +25%、谈判价格 +10%（TradePriceImprovement） |
| 弱点 | 不能在圣坛 5 格内工作（WorkGiver 前置检查）；看到圣徽持有者心情 -3 |
| 生育 | 和人类生育时孩子的异种型强制为**坎比恩**（`PregnancyUtility` 生成婴儿后设置，像天界之子那个钩子 `FaithCelestialChild`）；魅魔怀孕：原版只有女性怀孕，梦魔让人类女性怀孕同样出坎比恩 |
| 敌人 | `LP_AH_Succubus`，combatPower 160，等级 7–9（只在深渊入侵里少量出现） |
| 束缚 | 三阶仪式；初始等级 8 |

### 4.6 坎比恩 Cambion `LP_Cambion`（**可玩，可遗传**）

| 基因 | 效果 |
|---|---|
| `LP_CambionBlood 深渊血统`（本源，判定） | 寒铁对它 ×1.25；腐化系数 ×0.5；术士路径门槛 -2（加进 VPE 桥的 `ClassRules.Bloodlines`，**BridgeChecks 的计数要更新**） |
| `LP_CambionWings 恶魔之翼` | 小号蝠翼外观；能力**滑翔跳跃**：6 格，冷却 4 小时（复用 `Githyanki.Step`） |
| `LP_CambionFire` | 火焰伤害 ×0.5 |
| `LP_CambionPresence 深渊气魄` | 社交 +2 级熟练感（原版 aptitude）；威吓 / 心理战社交成功率 +20% |
| 外观 | 角（提夫林角换形状）、金红眼 |

来源：魅魔的孩子、化魔、坎比恩父母遗传；随机流浪者 / 难民里极少量（上面的权重）。冒险者种族亲和（1.51.0）：擅长 魅力、敏捷；笨拙 感知。

### 4.7 纳巴苏 Nabassu `LP_Nabassu`

| 项 | 值 |
|---|---|
| 外观 | 1.6 / 1.3；石像鬼般灰黑皮 `LP_SkinNabassu`；蝠翼；长獠牙 |
| `LP_NabassuDevour` **噬魂** | 它杀死的人形尸体加 hediff / comp 标记 `LP_SoulDevoured`：不能被任何复活（原版复活血清 `ResurrectionUtility`、神术复活、巫妖献祭 / 复活都检查这个标记，返回失败并提示“灵魂已被吞噬”）；纳巴苏获得 1 层 `LP_SoulGorged 饱魂`（生命 +5%、伤害 +5%，最多 10 层，存档保留）；被噬魂的尸体 24 小时后 30% 起身成敌对劣魔 |
| `LP_NabassuGaze` **石化凝视**（能力，6 格，冷却 30 秒，AI 会用） | 一人体质豁免 DC 14，失败移速 ×0.5 10 秒（hediff `LP_Petrifying`） |
| 敌人 | `LP_AH_Nabassu`，combatPower 260（每层饱魂 +20，袭击点数计算时按层数） |
| 束缚 | 三阶仪式；初始等级 10；仍然噬魂，饱魂层数保留；服从度衰减 ×1.5 |
| 掉落 | 饱魂之牙（6.2），40% |

### 4.8 影魔 Shadow demon `LP_ShadowDemon`（敌人专属）

- 人类身体全身着色成半透明黑（shader `TransparentPostLight` 或在 PawnRenderer 上加透明度；做不动就用纯黑皮肤 + 黑烟 fleck），无头发。
- `LP_OutsiderExtension.nonMagicFactor = 0.5`；在**光照 ≥ 50%** 的格子里受到伤害 ×1.5、移速 ×0.7（在 PreApplyDamage 里读 `map.glowGrid.GroundGlowAt(pos)`，移速用 StatPart，**StatPart 结果按 pawn 缓存 250 刻**）。
- 只在夜间的深渊入侵波次出现。`LP_AH_ShadowDemon`，combatPower 150，等级 6–8。不可召唤、不可束缚。

### 4.9 现有恶魔接进仪式

- **六臂蛇魔**（`LP_Marilith`，只有装了 HAR 才出现在三阶菜单里）：生成一个六臂蛇人殖民者，加位面束缚，初始等级 10。`LP_OutsiderExtension` 加到六臂蛇人的深渊之躯基因（设备上的实际 defName），`plane=Abyss, banishOnDown=false, taintImmune=true`。六臂蛇人当主祭：成功率 +10%。
- **炎魔**（`LP_Balor`）：四阶仪式的仆从，存在 1 天；结束时 30% **挣脱**（改成 `LP_AbyssHorde` 派系当场开打，死亡爆炸照常），70% 正常遣返。不能束缚。

### 4.10 敌对派系 `LP_AbyssHorde 深渊魔群`

隐藏、永久敌对、没有据点、不进世界生成（照 1.52.0 `LP_ChromaticDragons` 的写法）。仪式失败的恶魔、挣脱的束缚者、深渊入侵的恶魔都归它。

---

## 五、深渊仪式（`Source/AbyssRitual.cs`）

### 5.1 研究

`LP_Research_Abyssology 深渊学`：研究页 (4, 2.8)，前置 `LP_Research_Planetouched`，1500，Medieval。描述写清楚“需要恶魔学典籍才能召二阶以上”。解锁：深渊法阵、束缚法阵、驱魔结界、位面锚链配方。

### 5.2 建筑 深渊法阵 `LP_AbyssCircle`

- 5×5，贴地（`altitudeLayer=FloorEmplacement`，可走过，`passability=Standable`），材料 银 20 + 寒铁 10 + 木灰 / 无（不用 stuff），建造工作量 3000，美观 -10。
- 贴图：暗红符文圈（RimWorld 画风，平涂 + 粗描边）。
- 有一个开关“银边”（升级按钮：再花 银 40，成功率 +5%，贴图换银色描边）。
- 同一张地图最多 1 个。

### 5.3 仪式流程

1. 选中法阵 → Gizmo“开始深渊仪式” → FloatMenu 列出可选目标（阶、名字、**当前成功率**、所需祭品；不满足的灰掉并写原因）。
2. 选主祭（FloatMenu 列出可用殖民者，按成功率排序）。阿斯莫腐化 < 0.5 时不能当主祭（“他拒绝”）。
3. 可选：右键法阵“担任护阵者”（0～3 人）。
4. 主祭 Job `LP_AbyssRitual`：去拿祭品（尸体 / 囚犯 / 灵魂币，囚犯照救援的方式扛起来；**囚犯必须是倒地或被抓住的**，不要求自愿），放到法阵中心，然后站着念 2 小时（四阶 6 小时），有进度条和紫色 fleck；护阵者 Job `LP_AbyssWard` 去四角站着。主祭被打断（倒地、征召、精神崩溃）= 仪式失败（按“裂口”结果处理一阶以外的阶）。
5. 结束：囚犯 / 尸体被消耗（囚犯死于处决，原版处决心情照常给），灵魂币扣掉。

### 5.4 成功率

```
成功率 = 基础(阶)
       + (主祭魅力调整 + 主祭智力调整) × 4%
       + 熟练加值 × 2%
       + 护阵者人数 × 4%
       + 真名卷（同种）10%
       + 银边 5%
       + 六臂蛇人主祭 10% / 巫妖主祭 5% / 提夫林主祭 5%
       - 薄弱度 / 10 %
限制在 5%～95%
```

基础：一阶 85%、二阶 70%、三阶 55%、四阶 35%。主祭社交技能每级 +0.5%（上限 +10%）。

**仪式质量**（用于束缚初始服从度）= 0.4 + (成功率 - 掷骰) 的余量 × 2，限制 0.4～1.0。

### 5.5 目标表

| 阶 | 目标 | 结果 | 祭品 | 需要典籍 |
|---|---|---|---|---|
| 一 | 劣魔群 | 3～6 只仆从，3 天 | 1 具人形尸体 或 2 灵魂币 | 否 |
| 一 | 夸塞魔 | 魔宠（永久） | 同上 | 否 |
| 二 | 巴路古拉 | 仆从 5 天 **或** 束缚 | 1 名囚犯 或 6 灵魂币 + 1 恶魔血 | 巴路古拉 |
| 二 | 哈兹魔 | 仆从 5 天 或 束缚 | 同上 | 哈兹魔 |
| 三 | 魅魔 / 梦魔（召唤时选性别） | 束缚 | 1 名囚犯 + 15 灵魂币，或灵魂抵押 | 魅魔 |
| 三 | 纳巴苏 | 束缚 | 同上 | 纳巴苏 |
| 三 | 六臂蛇魔（需 HAR） | 束缚 | 同上 | 六臂蛇魔 |
| 四 | 炎魔 | 仆从 1 天，结束 30% 挣脱 | 3 名囚犯 + 1 神迹残片 | 炎魔 |

**灵魂抵押**：选一名殖民者（确认弹窗），代替祭品。该殖民者立刻腐化 +30%，得到 hediff `LP_SoulMortgaged 灵魂抵押`：死后不能复活（同噬魂标记），尸体消失。在圣坛向善神祈祷时多一个选项“赎回灵魂”（花 300 神眷，移除）。

### 5.6 结果

| 结果 | 条件 | 发生什么 |
|---|---|---|
| 成功 | 掷骰 ≤ 成功率 | 目标在法阵中心出现（黑烟 + 音效），按表成为仆从 / 束缚者 / 魔宠 |
| 召来但未束缚 | 掷骰 > 成功率，差 < 20 | 目标出现但属于 `LP_AbyssHorde`，就地开打；劣魔群改为 2～4 只敌对 |
| 裂口 | 差 ≥ 20 | 在法阵中心打开一个 `LP_PlanarRift`，来源位面 `LP_Plane_Abyss`（7.4）；四阶失败时第一波就有炎魔（不受点数门槛）。裂口没关之前法阵不能用 |

所有结果：薄弱度 +5/+10/+15/+25（按阶），失败再 ×2；主祭和护阵者加腐化（2.1）。

### 5.7 炼魂

法阵的另一个 Gizmo“炼魂”：把一名囚犯带到法阵处决 → 得到 3 枚灵魂币；主祭腐化 +5%、薄弱度 +3；原版处决心情照常。冷却 1 天。

---

## 六、物品与建筑

### 6.1 物品（`Defs/Abyss_Items.xml`）

| defName | 名称 | 说明 | 来源 |
|---|---|---|---|
| `LP_SoulCoin` | 灵魂币 | 堆叠 50，价值 30，不能制作；殖民者背包里 ≥ 10 枚心情 -2“它们在尖叫”。**巫妖**：命匣菜单（或巫妖身上的能力）多一个“吸收灵魂币”，1 枚 = 现有献祭一具尸体所给命匣经验的 25%（沿用设备上现有的命匣献祭经验入口，不新开一套） | 炼魂、恶魔掉落（劣魔 10%×1，其他 50%×1～3） |
| `LP_DemonBlood` | 恶魔血 | 堆叠 10，价值 60；**可以喝**：hediff `LP_DemonBloodRush`（近战伤害 +15%、疼痛 -10%，1 天），腐化 +8%；炼金桌材料（本版不加新配方，留给以后） | 恶魔掉落（非劣魔 60%），束缚法阵里关着的外来者囚犯每 5 天“抽血”手术 1 次（原版 RecipeDef，`LP_ExtractDemonBlood`，不伤命） |
| `LP_Demonomicon` | 恶魔学典籍 | 原版书（照耐瑟瑞尔卷轴写法，`CompProperties_Book`）。每本生成时随机记载一种恶魔（巴路古拉 / 哈兹魔 / 魅魔 / 纳巴苏 / 六臂蛇魔 / 炎魔，权重 3/3/2/2/1/0.5；没 HAR 不出六臂蛇魔），标题带名字。读完（累计 4 小时）→ 殖民地“已知恶魔”加这一种（存 GameComponent）。阅读时每小时腐化 +0.5% | 深渊低语事件、塞伊法师掉落 5%、商人（异域商人 10%） |
| `LP_TrueNameScroll` | 真名卷 | 见 3.3 | 外来者被击败、审讯 |
| `LP_DimensionalShackles` | 位面锚链 | 对一个倒地的外来者使用（物品右键 → Job）：加 hediff `LP_Shackled 位面锚链`，不再遣返，可以俘虏；原版装备槽不用 | 符文铁砧（1.47.0）：银 30 + 寒铁 20 + 位面残渣 2，需要深渊学 |

**审讯真名**：新的囚犯交流方式 `LP_InterrogateTrueName`（PrisonerInteractionModeDef，只对戴锚链的外来者囚犯显示），每次狱卒交流 15% 得到它的真名卷，得到后自动切回“维持”。

### 6.2 装备（只有一件）

| defName | 名称 | 效果 | 来源 |
|---|---|---|---|
| `LP_GorgedFang` | 饱魂之牙 | 饰品（1.45.0 的饰品 / 腰带层，照设备上现有饰品槽写法）：佩戴者每击杀一个人形 +1 层 `LP_FangGorged`（伤害 +1%，最多 10 层，死亡清零）；佩戴时腐化每天 +0.2% | 纳巴苏 40% |

### 6.3 建筑

| defName | 名称 | 说明 |
|---|---|---|
| `LP_AbyssCircle` | 深渊法阵 | 5.2 |
| `LP_BindingCircle` | 束缚法阵 | 3×3 贴地，银 15 + 寒铁 10。**床放在上面**：束缚者的床在法阵范围里时服从度每天 +3；戴锚链的外来者囚犯关在有束缚法阵的房间里不会越狱（原版越狱判定跳过）。每张地图上限 4 |
| `LP_WardingGlyph` | 驱魔结界 | 1×1 贴地符文，银 10 + 圣水 1（`LP_HolyWater` 或 `LP_BlessedWater` 任一）。半径 6：敌对外来者移速 ×0.7（StatPart，读缓存）；睡在里面的人腐化每天 -1%；地图薄弱度每天 -1（最多算 5 个）；裂口不会在结界范围里打开（`PortalEvent.SpotOk` 加判断） |

---

## 七、位面薄弱度与事件

### 7.1 `MapComponent_PlanarThinness`（`Source/AbyssThinness.cs`）

- 值 0～100，存档。地图检视面板（左下角 / 地图信息）不常驻显示；**有深渊法阵的地图**在法阵检视里显示“位面薄弱度 37”，并在设置里可以打开“在地图上显示”。
- 来源：仪式（5.6）、炼魂 +3、地图上击杀外来者 +1、开着的深渊裂口每天 +3、地图上每件恶魔学典籍 / 饱魂之牙每天 +0.2。
- 每天 -2，驱魔结界每个 -1（最多 5 个）。
- **每天一次**（`MapComponentTick` 里按 60000 刻判断）掷事件：
  - ≥ 20：深渊低语，概率 (值-10)/200 每天
  - ≥ 60：深渊入侵，概率 (值-50)/400 每天
  - 触发后薄弱度 -15，同一事件冷却 10 天；乘设置“薄弱度事件频率”0～200%。
- 薄弱度只在**有过深渊仪式或开过深渊裂口的地图**上计算（没有就整个组件什么都不做）。

### 7.2 深渊低语 `LP_AbyssWhisper`

触发：薄弱度掷骰，**或**故事叙述者随机（`baseChance` 0.3，要求殖民地有人腐化 ≥ 0.3 或已经研究深渊学）。一名殖民者（优先腐化最高的）梦见深渊，身边出现一本恶魔学典籍。ChoiceLetter：

- **留下**：典籍留在原地，该殖民者腐化 +5%。
- **烧掉**：典籍销毁，殖民者心情 +3“拒绝了深渊”3 天。
- **交给费伦拓荒同盟**：典籍销毁，同盟好感 +10（同盟敌对时这个选项灰掉）。

### 7.3 魅魔来访 `LP_SuccubusVisit`

触发：故事叙述者，`baseChance` 0.4，最早第 30 天，殖民地 ≥ 5 人，冷却 60 天，需要设置“允许束缚成殖民者”开着。

1. 原版“流浪者加入”的信件（文字一样，看不出来）。来的人是普通人类外观，异种型暂时是基准人，带隐藏 hediff `LP_SuccubusDisguise`（`Visible=false`，不在健康页显示）。
2. 10～20 天后（或被魔鬼之眼 / 阿斯莫 5 格内看到时立刻）**揭晓**：`FaithRaces.Convert` 的同类写法换成 `LP_Succubus`（性别随原来的人），信件，ChoiceLetter：
   - **束缚它**：需要地图上有深渊法阵；加位面束缚，服从度 50，主人 = 殖民地社交最高的人。
   - **驱逐**：它离开地图（不打）；它最亲近的殖民者心情 -8“被抛下”10 天。
   - **动手**：它变成 `LP_AbyssHorde`，开打。
3. 揭晓前它在殖民地生活期间照样能“梦境吸取”（被吸取的人有提示性心情“噩梦缠身”，玩家可以猜）。

### 7.4 深渊入侵 `LP_AbyssIncursion`

- 触发：薄弱度掷骰（7.1）。**不进故事叙述者随机池**（只有薄弱度能触发）。
- 新 `PortalPlaneDef` `LP_Plane_Abyss`（label 深渊，暗红紫色），**不加进 `LP_PortalBreach` 的随机位面池**（现有传送门失控事件不变）。需要给 `PlanarSpawn.side` 加一个值 `abyss` → 生成到 `LP_AbyssHorde`。
- 波次（沿用现有 8 小时一波、`WaveFactor`、第 9 波崩溃）：

| 单位 | count | fromWave | 其他 |
|---|---|---|---|
| `LP_AH_Dretch` | 3~6 | 1 | scale |
| `LP_AH_Barlgura` | 1~2 | 2 | scale |
| `LP_AH_Hezrou` | 1~1 | 3 | |
| `LP_AH_ShadowDemon` | 1~2 | 3 | 只在夜里（`special="night"`，PortalEvent 里加这个判断：白天这一行跳过） |
| `LP_AH_Succubus` | 1~1 | 4 | maxTotal 2 |
| `LP_FH_Balor`（炎魔） | 1~1 | 5 | maxTotal 1；**只有袭击点数 ≥ 3500**（事件生成时的点数）或仪式四阶失败才有 |

- 关闭方式：现有的（打掉生命、驱散粉尘、巨灵命令）+ **真名卷**直接关（3.3）+ **投掷圣水**：3 格内落下 5 瓶圣水关闭（`CompPlanarRift` 计数，存档）+ **六臂蛇人命令**（和巨灵同一个 Job，`CompPlanarRift.GenieChance` 改成“巨灵或六臂蛇人”）。
- 关闭掉落：现有位面残渣 + 恶魔血 1～2。
- 信件标题“深渊入侵”，正文说明怎么关。

### 7.5 纳巴苏猎魂 `LP_NabassuHunt`

- 故事叙述者 ThreatSmall，`baseChance` 0.25，最早第 40 天，殖民地最高冒险者等级 ≥ 8，冷却 45 天；只在夜里（22 点～4 点）执行，白天推迟。
- 一只纳巴苏从地图边缘进入（`LP_AbyssHorde`），等级 9 + 殖民地最高等级 / 4。AI：原版 `LordJob_AssaultColony`；**目标优先**：给它的 Lord 加一个 duty 选目标时偏向 8 格内没有其他殖民者的人（做不动就用默认 AI，写报告）。
- 黎明时还活着 → 飞走（离开地图），带走已经噬魂的层数，下一次来时继承（存 GameComponent，最多 10 层）。
- 信件“猎魂者”。

### 7.6 事件频率

所有新事件吃 1.31.0 的“事件频率”滑条和本版的“薄弱度事件频率”。

---

## 八、设置（新分组“深渊”，放在“真龙与传奇怪”下面）

| 设置 | 默认 | 范围 |
|---|---|---|
| 启用深渊内容 | 开 | 关掉：研究隐藏、事件不触发、已有的束缚者保留 |
| 腐化速度 | 100% | 25%～200% |
| 薄弱度事件频率 | 100% | 0%～200% |
| 允许束缚恶魔成为殖民者 | 开 | 关掉：二阶只有仆从，三阶整阶灰掉，魅魔来访不触发 |
| 束缚者额外上限 | 0 | 0～5 |
| 仆从上限 | 8 | 0～16 |
| 在地图上显示薄弱度 | 关 | |

---

## 九、性能（Peter 03:33 的第 7 项：不要有重复计算降低 TPS）

这一版加了几个光环和全局规则，必须按下面写：

1. **外来者登记表**：`MapComponent_Outsiders` 维护本地图上的外来者列表（`Pawn.SpawnSetup` / `DeSpawn` 后缀登记，读档时重建）。恶臭光环、驱魔结界、魔鬼之眼、魅魔被识破的检查**只遍历这个表**，不扫 `mapPawns.AllPawns`。表为空时所有相关补丁第一行就返回。
2. **伤害补丁**：`PreApplyDamage` 前缀先 `Outsiders.Of(victim)` 判空（缓存），再看武器；不要每次伤害都 `GetComp`、`DefDatabase` 查找（defs 全部静态缓存）。
3. **光环间隔**：恶臭（劣魔 / 哈兹魔）每 300 刻；腐化、服从度、梦境吸取、魔宠、结界睡眠全部按 2500 刻（`HediffComp.CompPostTickInterval` / `TickInterval(delta)` 累加的写法，照 `Gene_IceDevilCold`）；薄弱度每天一次。
4. **StatPart**（影魔移速、结界减速、腐化的近战加成）：结果按 pawn 缓存 250 刻；非外来者、没有腐化的人第一行返回。
5. **心情 ThoughtWorker**（低语、恶臭、灵魂币尖叫）：先看地图外来者表 / pawn 自身 hediff，不做范围扫描以外的事；灵魂币检查只看背包。
6. **不加任何每刻（Tick）对所有 pawn 的补丁**。如果需要 `Pawn.Tick` 级别的东西，用 hediff / gene 的间隔 tick。
7. 报告里列出本版新增的所有 Harmony 补丁和它们的“快速返回”条件；用 Dev 模式的性能分析或简单计时，在 100 人地图上对比开关深渊内容的 TPS，写进报告（差距应 < 2%）。

---

## 十、本线程做的决定（Peter 不需要回答）

1. 第一批只做深渊（恶魔）线；九狱契约、妖精、天界、远域全部在 1.55.0 及以后。
2. 不用 HAR；六臂蛇魔只在装了 HAR 时出现在三阶菜单。
3. 外来者层做成数据驱动（`LP_OutsiderExtension`），现有寒冰魔 / 炎魔迁移过去，行为不变。
4. 玩家的**束缚者击倒不遣返**（否则永久殖民者一倒地就没了，太伤）；死亡才消散。仆从和敌人击倒即遣返。
5. 束缚者会挣脱（恶魔每天 -2 服从度），这是深渊线的核心代价。
6. 深渊入侵复用 1.28.0 裂口，只加一个位面定义；**现有传送门失控事件不变**（不会随机抽到深渊）。
7. 魅魔 / 梦魔同一异种型，按性别显示名字；可玩、可束缚；能生坎比恩。梦境吸取只写成“同床休息”，内容保持 PG。
8. 坎比恩是可遗传、可在野外极少量出现的普通可玩种族；化魔的终点。
9. 夸塞魔是动物魔宠（不做“借眼”和变形外观，构思里的这两条删掉）。
10. 影魔的“无形”只做成伤害减半 + 怕光，不做穿墙。
11. 四阶只做炎魔；“呼唤恶魔领主之名”和恶魔领主留到第四批。
12. 灵魂币可以给巫妖吸收，换算 = 献祭一具尸体的 25%。
13. 噬魂和灵魂抵押都阻止复活，统一用一个标记检查。
14. 研究放 (4, 2.8)，前置“天界与地狱血统”。

---

## 十一、测试（Peter 只要求跑新组：`.\Test.ps1 -Only Abyss`；另跑 `-NoHAR -Only Abyss`、`-Only Faith`、`-Only Events`（#87 是已知旧问题，不用管））

`Source/AbyssChecks.cs`，测试组 `Abyss`，约 30 条：

- 外来者层：寒冰魔、炎魔迁移后 `Fiends.Is` 照旧、击倒消散、寒铁 ×1.5、圣水 ×2；炎魔 `plane=Abyss`、寒冰魔 `Hells`；银对魔鬼生效（寒冰魔 silver 设成 1.5）。
- 遣返规则表的 5 行各一条（敌人 / 仆从 / 束缚者击倒 / 束缚者死亡 / 锚链）。
- 腐化：来源乘种族系数（六臂蛇人 0、阿斯莫 ×2）；四个阶段的效果；“恶魔之印”后不下降；化魔变坎比恩、不能换血脉的人得“深渊宠儿”、巫妖得“命匣受污”；圣水 -10%。
- 束缚：衰减（恶魔 -2/天）、真名卷减半、重申 +8、主人死亡 -40、到 0 挣脱换派系；上限公式。
- 仪式：成功率公式（给固定六维的主祭验算 3 个数）、三种结果（用 `TestRoll` 固定掷骰）、祭品被消耗、薄弱度和腐化增加、裂口打开且法阵锁住。
- 单位：6 个异种型和夸塞魔能生成、基因齐、体型；巴路古拉跃击落地豁免；哈兹魔恶臭恶心；魅魔魅惑之吻（豁免失败 / 成功免疫）、生育坎比恩；纳巴苏噬魂标记阻止复活血清、饱魂层数存读档；影魔光照伤害系数。
- 真名卷：生成名字、对敌人使用的豁免、关裂口。
- 位面锚链：戴上后不遣返、能俘虏、审讯 15% 出卷（固定掷骰）。
- 事件：深渊低语三个选项；魅魔来访揭晓转换；深渊入侵波次（白天跳过影魔、炎魔点数门槛）、圣水 5 瓶关闭、六臂蛇人命令；纳巴苏夜间推迟。
- 薄弱度：增长、每天衰减、结界 -1 上限 5 个、没有仪式的地图组件不工作。
- 存档往返：腐化、服从度、真名、已知恶魔、饱魂、薄弱度、裂口圣水计数。
- 不装 HAR：三阶菜单没有六臂蛇魔，典籍不出六臂蛇魔，没有红字。
- 设置关闭“允许束缚”：三阶灰掉、魅魔来访不触发。

---

## 十二、版本与文件

- **版本**：About.xml → **1.54.0**（modVersion + 描述末尾加一段“深渊仪式”）；使用说明.txt 加 1.54.0 条目（面向玩家：怎么研究、怎么画法阵、每阶能召什么、腐化怎么去、束缚怎么维持、裂口怎么关）。
- **备份**：开工前把要改的现有文件复制到 `ArtSource/backup-<协调给的上一版标签>/`。
- **设备上放一份本文**：`ArtSource/Abyss-1.54.0-spec.md`。
- **报告**：`ArtSource/Abyss-1.54.0-report.md`：改动文件、新 defName 全表、测试结果、性能对比数、与本文的差异、没做的。
- **新文件**：
  - `Source/Outsiders.cs`（扩展、缓存、遣返、克制、真名、登记表）、`Source/AbyssTaint.cs`（腐化、化魔）、`Source/AbyssBinding.cs`（仆从、束缚、上限、重申）、`Source/AbyssRaces.cs`（种族能力）、`Source/AbyssRitual.cs`（法阵、Job、成功率、炼魂）、`Source/AbyssThinness.cs`（薄弱度）、`Source/AbyssEvents.cs`（4 个事件）、`Source/AbyssChecks.cs`。
  - `Defs/Abyss_Races.xml`、`Defs/Abyss_Items.xml`、`Defs/Abyss_Buildings.xml`、`Defs/Abyss_Events.xml`（派系、PawnKind、事件、位面定义、精神状态、心情、交流方式）。
  - 改动：`Source/Fiends.cs`（迁移）、`Defs/Fiends.xml`（加扩展）、`Source/PortalEvent.cs`（`side=abyss`、`special=night`、圣水计数、六臂蛇人命令、结界不开裂口）、`Defs/Research.xml`、六臂蛇人 HAR 基因 XML（加扩展）、`Bridges/VPE/Source`（坎比恩血统 + BridgeChecks 计数）、`Faith` 的祈祷完成钩子（腐化 -5%、赎回灵魂）、`FaithRaces`（复用 Convert）、复活入口（噬魂 / 抵押标记）。
- **贴图**（RimWorld 画风：手绘、粗描边、平涂 + 阴影、低饱和；脚本 `ArtSource/RaceArt/draw_abyss_154.py <模组根目录>`，复用现有形状函数和调色）：
  - 种族附件：巴路古拉猿脸、哈兹魔蟾蜍头、魅魔翼 / 尾 / 角、坎比恩翼 / 角、纳巴苏翼 / 獠牙、劣魔角，各三向；影魔效果；6 套肤色。
  - 夸塞魔动物四向 + 尸体。
  - 物品：灵魂币、恶魔血、恶魔学典籍（封面 1 张 + 6 种恶魔各一个封面徽记可以只做颜色变体）、真名卷、位面锚链、饱魂之牙。
  - 建筑：深渊法阵（普通 / 银边）、束缚法阵、驱魔结界。
  - 图标：腐化 4 阶、位面束缚、仆从、灵魂抵押、饱魂、恶心、石化中、深渊宠儿、命匣受污；能力 跃击 / 魅惑之吻 / 变形伪装 / 石化凝视 / 滑翔跳跃；研究图标。
  - 预览：`ArtSource/Preview/Abyss-1.54.0.png`（所有单位四向 + 物品图标）。

---

## 十三、不做的（写进报告“没做”）

九狱 / 妖精 / 天界 / 远域 / 元素契约与契约讲台（1.55.0）；弗洛魔、钳魔、嗡魔、纳法斯尼、星裔（HAR 批）；位面地点、恶魔领主、深渊钢 / 深渊之刃等装备（第四批）；夸塞魔借眼和变形；影魔穿墙；恶魔血炼金配方；邪契者特性；邪术师路径。
