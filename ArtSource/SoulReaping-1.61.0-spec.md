# 夺魂仪式 1.61.0 实现要求

来源构思：`DND特性掠夺仪式构思.md`（写于 1.58.5）。Peter 2026-10-28 03:52 把"完成夺魂仪式"列进任务，并说"你可以自行决定内容"：所有决定由线程自定，不问 Peter，记进报告"决定"一节。不做 d20，游戏内只写「成功率 X%」。游戏内文字中文（defName 只出现在代码里）。
设备版本：1.60.0（迷锁地牢）。开工前完整备份 `ArtSource/backup-1.60.0`（上一个完整备份 backup-1.59.0）。1.60.0 注意：设置分组计数检查在 `Source/AdventurerChecks.cs` 现为 19（本版不加分组，保持 19）；`Core.cs` Settings.ExposeData 里 ExposeMythal() 在 ExposeAbyss() 之后；使用说明.txt 的新段落接在【1.60.0 迷锁地牢】之后；本文件拷到 `ArtSource/SoulReaping-1.61.0-spec.md`；报告 `ArtSource/SoulReaping-1.61.0-report.md`；预览 `ArtSource/Preview/SoulReaping-1.61.0.png`。测试 `-Only SoulReaping`，再各跑一次 `-Only Abyss`、`-Only HeroTraits`、`-NoIdeology -Only SoulReaping`、`-Isekai -Only SoulReaping`（若 -Isekai 开关不存在，用默认含 Isekai 的跑法代替，写偏差）。**编译前在线程里确认 Peter 已退出游戏，并确认没有 RimWorldWin64 进程。**

## 〇、通用

1. 先读设备：`AbyssRitual.cs`（法阵 Building、Gizmo、FloatMenu、自写 Job、`NextOffering` 搬运、`Chance`、`Judge`、`TestRoll`、祭品扣库存、炼魂）、`AbyssTaint.cs`（腐化加值与种族倍率）、`AbyssThinness.cs`、`AbyssBinding.cs`（召来未束缚恶魔的入口）、`Outsiders.cs`（`Outsiders.Is`）、`AbyssRaces.cs`（`SoulGone`）、`HeroTraits*.cs`（`IsGold/IsPurple`、`LP_PowerTrait`、天命 degree 与进化、`Hediff_HeroRecord`、Isekai 互斥 `HeroIsekai.Blocks`）、`Talents`（1.51.0）、背景特性与传说事迹列表、`MindFlayer.cs`（支配 hediff、吞噬大脑脑髓产量）、巫妖 `LP_Altar` 与命匣（献祭经验、等级）、`Core.cs` 设置「深渊」分组、`IdeoPatches.cs`/Bridges/Ideology 的历史事件写法、1.58.6 性能约定。构思和设备冲突以设备为准，写进报告"偏差"。
2. 新文件 `Source/SoulReaping.cs`（筛选/分阶/成功率/结果/存档）、`Source/SoulReapingUI.cs`（选择窗口）、`Source/SoulReapingChecks.cs`；Defs `Defs/SoulReaping.xml`；Ideology 部分放 `Bridges/Ideology/Defs/SoulReaping_Ideo.xml`；Isekai 阶数名单放 `Bridges/Isekai/Defs/SoulReaping_Isekai.xml`（若 Isekai 桥目录名不同，照设备）。
3. 性能：只有点击和仪式结算时计算；魂伤/排异期是普通 hediff（stage + 2500 tick 判定），不新增每 tick 扫描。排异期"精神崩溃脱落"挂在精神状态开始的现有钩子上（若没有，Harmony Postfix `MentalStateHandler.TryStartMentalState`，先判断 pawn 有无排异 hediff 快速返回）。报告列出新增 Harmony 钩子。
4. 不加新研究：需要已研究 深渊学 `LP_Research_Abyssology`；三阶、四阶还要求主祭读过任意一本恶魔学典籍（照召唤恶魔的同一判断）。

## 一、入口与流程

- 深渊法阵 `LP_AbyssCircle` 新 Gizmo「夺魂仪式」（图标 `LP/SoulReaping/Reap`），和现有仪式并列；第二个 Gizmo「秽魂转嫁」（图标 `LP/SoulReaping/Offload`）。
- 点击 → 窗口 `Dialog_SoulReaping`（不用 FloatMenu，选项太多）：
  1. 左列：本地图上的囚犯/奴隶（不合格的灰掉并写原因，见二）。
  2. 中列：选中目标的特性列表：特性名、阶（一~四，颜色同 1.58.0：白/紫/金/金色天命）、成功率、祭品、后果一行；不能夺的灰掉写原因。
  3. 右列：主祭下拉（合格殖民者；显示现有强力特性、已夺数量、冷却）。挤掉强力特性或必须放弃旧夺来特性时红字说明，需要勾选「我明白」才能开始；第三个夺来特性要先选放弃哪个。
  4. 「开始」→ 复用法阵 Job：主祭把目标搬上法阵（`NextOffering` 同款），护阵者照旧可参加，引导时长同该阶深渊仪式（若深渊仪式无分阶时长，统一 2 小时 = 5000 tick）。
- 祭品从库存/法阵附近扣，规则同深渊仪式（灵魂币 `LP_SoulCoin`、恶魔血、神迹残片，以设备实际 defName 为准）。

## 二、目标条件

可以：本殖民地囚犯（任何派系）；本殖民地奴隶（装了 Ideology 才会有）。倒地敌人必须先抓成囚犯。

不可以（灰掉原因）：
| 情况 | 原因文字 |
|---|---|
| 非囚犯非奴隶 | 他不是你的俘虏 |
| `Outsiders.Is` | 外来者的本质不在凡人灵魂里 |
| `AbyssRaces.SoulGone` | 这具身体里没有灵魂了 |
| 巫妖（命匣不在法阵 3 格内） | 它的灵魂在命匣里 |
| 死灵造物、机械体、动物、年龄 < 冒险者最低年龄 | 没有成形的灵魂可剥 |
| 带魂伤 hediff | 灵魂还没愈合 |
| 大脑缺失 | 没有可以撬开的心智 |

心灵失聪者可以夺，成功率 −20%。

## 三、特性分阶

### 3.1 默认归类（`SoulReaping.TierOf(TraitDef, degree)`，结果缓存到字典）

| 阶 | 归类 | 基础成功率 | 祭品 | 主祭腐化 | 薄弱度 |
|---|---|---|---|---|---|
| 一 | 其余所有可夺特性（原版、其他模组、Isekai 白色） | 85% | 灵魂币 2 | +5 | +5 |
| 二 | 1.51.0 天赋、1.58.0 紫色、Isekai 紫色/稀有 | 70% | 灵魂币 6 + 恶魔血 1 | +10 | +10 |
| 三 | 1.58.0 金色（英雄之资、极致·X）、Isekai 金色原型 | 55% | 灵魂币 15 + 恶魔血 1 | +20 | +15 |
| 四 | 天命：预言之子、邪契者 `LP_PactDestiny`、魔网之子 | 35% | 灵魂币 15 + 神迹残片 1 | +35 | +25 |

腐化单位、种族倍率、护阵者吃一半：全部照 1.54.0 现有函数。薄弱度失败 ×2。

### 3.2 扩展 `LP_TraitTheftExt`（DefModExtension）

字段：`forbidden`（bool）、`reason`（string，灰掉原因）、`tier`（int 0=默认）、`mindTrait`（bool，夺心魔加成）、`bad`（bool，允许转嫁）。用 XML Patch 挂上：
- forbidden：8 个背景特性、7 个传说事迹、神之勇士、性取向（Gay/Bisexual/Asexual）、`LP_DivineInfusion`、IsekaiKillMilestones 的里程碑特性（`MayRequire` 该模组 packageId；packageId 以设备 About.xml 为准）。
- mindTrait：原版 `Nerves`、`NaturalMood`、`FastLearner`、`PsychicSensitivity`、`TooSmart`、`GreatMemory`（存在的才挂）。
- bad：原版 `Pyromaniac`、`ChemicalInterest`、`ChemicalFascination`、`Wimp`、`Abrasive`、`Gourmand`、`Greedy`、`Jealous`、`Annoying_Voice`、`CreepyBreathing`、`DislikesMen`、`DislikesWomen`、`BodyPurist`、`Ugly`（Beauty 负值）以及光谱负端（Industriousness/NaturalMood/Nerves 的负 degree）。
- 另外运行时判断不可夺：基因 forcedTraits 给的特性（`pawn.genes` 里有 forcedTraits 包含该 def）→ 原因「来自血脉」；`trait.sourceGene != null` 同理。

### 3.3 光谱特性

按目标的 degree 原样拿走。只有正端可被"夺"（degree > 0，Beauty/Industriousness/NaturalMood/Nerves/PsychicSensitivity 这类以"正面"为准；PsychicSensitivity 两端都算可夺，因为玩家可能想要任意一端）。主祭已有同 def 时只能替换，窗口写「会把你的『懒惰』换成『勤奋』」。

### 3.4 强力特性名额与互斥（选择阶段预判）

- 新特性属于 `LP_PowerTrait` 且主祭已有一个 → 必须确认挤掉；成功后旧的直接删除。
- 其他 exclusionTag / `ConflictsWith` 冲突 → 同样显示挤掉提示（原版规则：冲突的旧特性被删）。
- `HeroIsekai.Blocks`、天命 × Isekai 金色原型：会被拦就直接灰掉，原因写明。
- 设置「允许夺取 Isekai 特性」关 → Isekai 特性灰掉。

### 3.5 天命

夺来的天命回到觉醒阶（最低 degree），分支保留（邪契者宗主），之后按主祭自己的冒险者等级进化（调用现有进化检查一次）。邪契者：3 天内宗主来信「新的债务人」（若 1.55.0 有偿契/债务 API 就转移债务；没有就只发信，写偏差）。

## 四、成功率

`SoulReaping.Chance(caster, target, tier, def, wardens)` = `AbyssRitual.Chance` 的同一套主祭部分（若 `Chance` 和阶/目标绑定，抽出公共部分，不改原数值；写偏差）
  − 基础换成 3.1 的数值
  − （目标感知调整值 + 魅力调整值）× 3%（冒险者系统关时按 0）
  − 目标冒险者等级 × 1%，传奇冒险者再 −10%
  − 目标有 心智堡垒：−20%；心灵失聪：−20%
  + 目标正被夺心魔支配：+10%
  + 主祭是夺心魔/半夺心魔且 mindTrait：+10%
  − 巫妖仪式桌路线：−10%
  夹在 5%～95%。窗口里悬停成功率显示逐项明细（只写百分比）。

结果判定照 `AbyssRitual.Judge`：成功 / 差距 < 20% 碎魂 / 差距 ≥ 20% 附魂。测试用 `TestRoll` 固定。被打断（主祭倒地、被征召、崩溃、目标被救走）= 附魂。

## 五、结果

### 5.1 成功
- 目标失去特性；主祭获得（3.3/3.4/3.5 规则）；在 `Hediff_HeroRecord` 加存档字段 `List<StolenTraitRecord> stolen`（def、degree、来源名字、tick）。若主祭没有 HeroRecord 就照现有方法添加。上限 2；放弃的旧夺来特性直接删除。
- 检视/特性悬停显示「（夺）夺自 某某」：Harmony Postfix `Trait.TipString`（或现有特性提示钩子），只对 stolen 列表里的特性追加一行；特性名后的灰色「（夺）」加在 `Trait.LabelCap` 的 Postfix 上，同样只查一次缓存的 HashSet。
- 主祭获得排异期 `LP_SoulRejection` 15 天：心情 −8 想法「这不是我的灵魂」（精神病态、巫妖无）。期间开始精神崩溃 → 本次夺来的特性脱落删除，发信。巫妖主祭没有排异期。
- 目标被夺 `LP_HE_SoulReaped` 事件（Ideology 桥用）。
- 神力灌注者（敌人）被夺任意特性成功时额外掉 1 枚神迹残片。

### 5.2 目标代价
| 阶 | 目标 |
|---|---|
| 一 | 魂伤 `LP_SoulWound` 10 天：意识 −10%，心情 −10，抵抗/意志 −30%（直接扣 guest.resistance/will 的 30%，一次性） |
| 二 | 魂伤 30 天：意识 −20%，心情 −15；50% 一项有热情的技能降一档 |
| 三 | 重魂伤 `LP_SoulWoundGrave` 60 天：意识 −30%、心灵敏感度 −30%；永久特性 空洞 `LP_Hollowed`（不再随机得到强力特性：在 1.58.0 随机强力特性入口查一下；社交影响 −20%） |
| 四 | 目标死亡（设置关掉时改为三阶的结果）。死因显示「灵魂被撕裂」（自定义 DamageDef 或 hediff 致死，大脑完整） |

- 被夺过二阶以上的目标存一个标记（在魂伤 hediff 上或 `LP_Hollowed`/小型 WorldComponent 的 pawn 集合），之后被吞噬大脑时脑髓 ×0.7（Postfix 吞噬结算；找不到干净挂点就写偏差）。
- 心情：法阵 10 格内目睹者 −6「目睹夺魂」3 天；殖民地全体 −3「我们做了夺魂的事」5 天。精神病态、嗜血者、邪恶阵营、巫妖、夺心魔无（设备有阵营判断就用，没有就只用前两个）。装 Ideology 且文化对夺魂有戒律时，不给这两条普通想法（戒律接管）。
- 派系：目标派系非敌对时好感 −20（原版 `Faction.TryAffectGoodwillWith`，reason 用原版 MemberHarmed 或新 HistoryEventDef）。
- 魂伤期间被招募 → 对主祭的永久负面关系想法 −40「他夺走了我的灵魂」（social thought，durationDays 60 大幅衰减即可）。

### 5.3 碎魂（小失败）
目标照样失去特性、照样吃该阶魂伤；主祭什么都得不到；主祭「魂火反噬」`LP_SoulBacklash` 昏迷 6 小时；腐化再加一半；薄弱度 ×2。

### 5.4 附魂（大失败）
目标保留特性，吃该阶魂伤（四阶也不死，吃三阶）。主祭得到目标身上最坏的一个 `bad` 特性（没有 → 得 15 天魂伤）；按阶召来一个未束缚敌对恶魔（调用深渊仪式"召来但未束缚"同一函数，一阶劣魔群/二阶巴路古拉/三阶纳巴苏/四阶炎魔，按设备实际 defName）；四阶大失败按原规则开裂口。

### 5.5 冷却、限制
- 同一主祭 7 天一次（存在 HeroRecord 或 SoulReaping 的 pawn 字典里）。
- 阿斯莫未到阴影阶段拒绝主持、无暴力者不能主持二阶以上：调用深渊仪式现有判断。
- 主祭不能是目标本人；主祭必须能走到法阵、有意识。

## 六、秽魂转嫁

- 窗口同一个，模式切到「转嫁」：右列选"被治疗者"（殖民者，要求能站上法阵），中列显示他的 `bad` 特性（基因给的、背景特性、性取向不行），左列选接收的囚犯/奴隶。主祭可以是被治疗者本人或另一人。
- 一律一阶：85%（同公式）、灵魂币 2、腐化 +3、薄弱度 +5。
- 成功：特性移到囚犯身上（光谱按 degree，囚犯已有同光谱则覆盖），囚犯吃一阶魂伤；被治疗者无排异期，不计入"夺来的两个"。
- 失败（碎魂/附魂都一样）：特性留着，祭品扣掉，主祭魂火反噬，不召恶魔。
- 事件 `LP_HE_SoulOffloaded`。

## 七、巫妖

- 巫妖主祭：法阵上照常拿 1.54.0 巫妖加成；腐化 ×0.5；无排异期、无"这不是我的灵魂"。
- **巫妖仪式桌路线**：`LP_Altar` 新 Gizmo「夺魂」，只有巫妖本人可主持，无护阵者；不扣灵魂币/恶魔血/神迹残片，改扣命匣献祭经验：当前升级所需经验 × 10%/25%/50%/100%（不够就灰掉）；成功率 −10%；不加腐化不加薄弱度；失败附魂不召恶魔（改为巫妖昏迷 12 小时）。命匣经验 API 照设备。
- **魂匣藏性**：巫妖主祭成功时窗口里可勾「存进命匣」：特性不装到自己身上，存进命匣（命匣 Thing/Comp 加存档列表 `storedTraits`），容量 `1 + 命匣等级 / 5`。命匣检视显示已存特性。命匣 Gizmo「赐予特性」：选一个存着的特性 + 一名受该巫妖统御的殖民者（非死灵造物；"统御"的判断照设备，没有就用"同派系殖民者且巫妖在地图上"），遵守强力名额/2 个夺来上限/互斥预判，立即生效 + 排异期；不需要 Job 引导（简化，写进决定）。
- 四阶目标死亡且主祭是巫妖：照常加一次灵魂献祭经验（调用现有献祭入口）。
- 夺巫妖：命匣在法阵 3 格内时可选；成功后命匣摧毁、巫妖真正死亡；窗口黑底警告。

## 八、Ideology（可选，装了才加载）

- HistoryEventDef `LP_HE_SoulReaped`、`LP_HE_SoulOffloaded`；主程序集按 1.56.0 写法在 `ModsConfig.IdeologyActive` 下记录。
- IssueDef `LP_Issue_SoulReaping`「夺魂」，三条戒律：可憎（做了 −10 心情 / 知道 −4）、可接受、神圣（做了 +5 / 殖民地 7 天内做过 +2）。
- 默认：深渊之门、契约之律、永夜不朽 → 神圣；主脑合一 → 可接受；九神神 meme、众神殿、晨主之光、灰烬之誓 → 可憎（PatchOperation 往 MemeDef 的 requireOne/默认戒律补；具体 defName 照设备）。
- 奴隶是合法目标；奴隶被夺后照原版奴隶受伤处理。

## 九、设置（放在已有「深渊」分组里，不增加分组数）

| 设置 | 默认 |
|---|---|
| 启用夺魂仪式 | 开（关掉后两个 Gizmo 不出现，已有记录保留） |
| 允许夺取 Isekai 特性 | 开 |
| 四阶夺魂杀死目标 | 开（关：改为三阶结果） |

## 十、贴图（RimWorld 手绘风：粗黑描边、平涂 + 一层阴影、低饱和）

`Textures/LP/SoulReaping/`：`Reap.png`（紫黑手掌从人形剪影里抽出一缕发光灵魂）、`Offload.png`（灵魂从左人流向右人，污浊绿色）、`Grant.png`（命匣上浮起一枚发光符印）、`LP_SoulWound` 无需图标。64×64，用 Python/PIL 脚本 `ArtSource/RaceArt/draw_soul_reaping.py` 绘制；预览图拼三张。

## 十一、文档

- About.xml：版本 1.61.0，描述末尾加一句「1.61.0：深渊法阵新增夺魂仪式与秽魂转嫁，巫妖可用命匣藏性」——**精确锚点替换**，改后 diff 只应有这两处。
- 使用说明.txt 末尾（或版本列表惯例位置）加【1.61.0 夺魂仪式】：入口、可夺/不可夺、四阶成功率与代价、失败、转嫁、巫妖、Ideology、设置。精确锚点，替换后检查文件行数只增加新段落的行数。

## 十二、自检 SoulReaping（约 25 条）

1. 分阶：普通原版特性=一、天赋/紫=二、英雄之资=三、预言之子=四。
2. 禁止：背景特性、传说事迹、神之勇士、性取向、基因给的特性 → 不可夺且有原因。
3. 目标条件：殖民者、外来者、SoulGone、儿童、带魂伤 → 不合格；囚犯合格。
4. 成功率：固定六维主祭 + 目标感知/魅力调整 → 期望值；心灵失聪 −20；夹 5/95；夺心魔 mindTrait +10；巫妖桌 −10。
5. 成功（TestRoll）：目标失去、主祭获得、stolen 记录 1 条、排异期 hediff、腐化增加、祭品扣除、薄弱度增加。
6. 强力名额：主祭有紫色时夺金色 → 紫色消失、只有一个 LP_PowerTrait。
7. 第三个夺来特性需放弃旧的 → 旧的被删、仍为 2。
8. Isekai 互斥预判（-Isekai 跑）：被 Blocks 拦的显示为不可夺。
9. 光谱替换：主祭懒惰（−1）夺目标勤奋（+2）→ 主祭勤奋 +2。
10. 天命回到觉醒阶。
11. 四阶成功：目标死亡且大脑完整；设置关 → 目标活着有空洞。
12. 三阶：目标得空洞 + 重魂伤。
13. 碎魂：目标失去、主祭未获得、反噬 hediff。
14. 附魂：目标保留、主祭得到 bad 特性、地图上出现敌对恶魔。
15. 排异期内精神崩溃 → 夺来特性脱落。
16. 冷却 7 天：第二次灰掉。
17. 转嫁：坏特性从殖民者移到囚犯、不计 stolen、无排异期。
18. 巫妖桌：扣命匣经验、无腐化。
19. 魂匣藏性：存进命匣 → 容量限制 → 赐予殖民者后特性出现 + 排异期。
20. 夺巫妖（命匣在旁）→ 命匣摧毁、巫妖死亡。
21. 神力灌注者被夺 → 掉神迹残片。
22. 吞噬大脑系数 0.7（被夺过二阶以上）。
23. 存档往返：stolen、冷却、命匣 storedTraits、魂伤剩余时间不变。
24. 设置关闭 → Gizmo 不出现。
25. -NoIdeology 下全组不报错；Ideology 下记录历史事件、可憎戒律时不给普通想法。

## 十三、交付

报告写：完成内容、决定、偏差、新增 defName 列表、新增 Harmony 钩子、测试结果（每组数量）、未在游戏里看到的部分。完成后告诉协调：设备版本 1.61.0、备份名、defName 列表、设备空闲。
