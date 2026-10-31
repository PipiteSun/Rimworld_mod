# 1.78.0 新职业：野蛮人 / 游侠 / 游荡者 — 报告（2026-10-30）

需求与数值见 `ArtSource/Classes3-spec.md`。备份：`ArtSource/backup-1.76.0-classes3`（动手前的 Classes*.cs、ClassChecks.cs、Adventurer.cs、Classes*.xml、About.xml、使用说明.txt）。

## 改动
新文件
- `Defs/Classes4.xml`：3 个 LP_ClassDef、3 个强度属性（LP_RagePower 狂怒强度 / LP_HuntPower 猎术强度 / LP_CunningPower 诡术强度）、13 个状态、21 个职业能力 + 6 个传奇能力。
- `Source/ClassMartial2.cs`：被动能力位、怒气、宿敌、偷袭、主动效果、宿敌窗口 `Dialog_ChooseFavored`、`Hediff_Rage`、`Hediff_ClassMark`，Harmony：`Pawn.PreApplyDamage`（前缀）、`Verb_MeleeAttack.GetNonMissChance`、`ShotReport.HitReportFor`、`Pawn_HealthTracker.CheckForStateChange`/`ShouldBeDowned`（不灭之怒）、`Pawn_PathFollower.CostToMoveIntoCell`（野外穿行）、`Pawn.Kill`（死亡印记）、`BeastLevels.Companion`（游侠算动物伙伴主人）。所有挂钩先判 `Martial.Cache.Count==0` / 空集合。
- `Source/ClassChecks2.cs`：11 条 + 存读档 2 条。
- `ArtSource/RaceArt/draw_classes3.py` → `Textures/LP/Classes/{Barbarian,Ranger,Rogue}/*.png`（21 图标 + 3 背景）、`Textures/LP/Classes/Legendary/*.png`（6 个）；预览 `ArtSource/Preview/Classes3-icons.png`、`Classes3-backgrounds.png`。

改的共用文件（只动职业相关的几处）
- `Classes.cs`：`ClassResource.Rage`；ClassData 新字段 `rageTick`、`favored`（存档）；怒气标签 / 说明；Regen 跳过怒气；Tick250 调 `ClassMartial2.Tick250`；狂暴中施法职业能力“狂暴中无法施法”；`StatPart_ClassPower` 新增 `alternateSkill`、`ability`（`ClassSystem.Power(p,s,alt,ability)`、`BestAbility`）；经验 ×1.25 背景；提示信改成十一个职业。
- `ClassMartial.cs`：MartialInfo 加 barbarian/ranger/rogue/flags；Refresh 把新职业算进物理职业（游荡者不进 `best`，所以没有额外攻击档位）；击晕免疫、精神崩溃免疫认狂战之魂；分页说明行；“选择宿敌”按钮。
- `ClassAbilities.cs`：效果分派、目标检查各加一行。
- `ClassChecks.cs`：数量 8→11 职业、79→100 能力、16→22 传奇；调用新检查。
- `Adventurer.cs` 第 394 行 `Eff` 末尾 `+ClassMartial2.ScoreBonus(this,a)`（原初斗士力量 +4，同武艺天赋加在上限之后）。
- `About.xml` modVersion 1.77.0 → **1.78.0**；`使用说明.txt` 标题、第二部分【1.78.0 新职业…】、第三部分更新日志一条（精确锚点插入，没动别人的条目）。

## 测试
- `Test.ps1 -Only Classes`：**PASS 43 checks**（含新 11 条与两条存读档）。
- `Test.ps1 -Only Adventurer`：**PASS 156 checks**。
- `Test.ps1 -VPE -Only Classes`：**PASS 7 checks**。
- 三次 Test.ps1 都以“Errors found in test log”退出，唯一的错误是 `multiple Verse.HediffDefs named LP_Petrifying`（Items_Scrolls.xml 与另一线程新加的 WildMonsters.xml 重名），不是职业的文件，没有动。

## 决定（Peter 授权自行决定）
1. 能力按 spec 顺序放在 1/3/5/9/13/17/20，每级一个（1 级只有一个能力，但宿敌 / 偷袭被动常驻）。
2. 怒气：上限沿用 2 + 等级/2；“脱战” = 不在狂暴中且 600 刻（10 秒）没有攻防，之后每 250 刻 −0.1（= 每小时 −1）；不设每小时获取上限。
3. 狂暴减伤按伤害的护甲类别（Sharp / Blunt）判断，所以子弹也算；狂暴伤害加成 1.15 + 0.1 × 狂怒强度（强度 1 时 = +25%）。
4. 危险感知：子弹 15% × 狂怒强度（最多 30%）完全闪开；爆炸 ×0.5。野性怒吼：恐慌逃跑只对非首领（传奇怪物、剧情首领、体型 ≥ 3 除外）的人形和动物。
5. 不灭之怒：本应倒地时给 10 秒“不灭之怒”状态（无痛、ShouldBeDowned 返回 false），每次狂暴一次；到时伤势还在就照常倒下。
6. 宿敌类型 = DndCreatureType 的 7 种 + 人形（Humanlike）+ 机械体；13 级第二种；选定不能改（窗口，非浮动菜单）。命中：近战 +20% 加在最终命中率上，射击 ×1.2；屠戮宿敌翻倍；宿敌克星改成必中（射击把掩体清零、射手系数拉满）。
7. 猎人印记 defName 用 `LP_RangerMark`（`LP_HuntersMark` 已是艾林魔的“追猎”）。
8. 野外穿行用移动代价挂钩：地形 pathCost + 积雪 + 植物 pathCost 少 75%。
9. 兽伴：每 250 刻给主人是游侠的同图动物 300 刻状态（近战伤害 ×(1.1 + 0.1 × 猎术)、移动 ×1.1、护甲 +10% × 猎术，最多 30%）；对主人宿敌伤害 +20%。
10. 偷袭：伤害 ×(1 + (0.2 + 0.03 × (等级−1)) × 诡术强度)，每秒一次（影舞 0.5 秒且不看条件）；条件含“目标没把你当目标”（mindState.enemyTarget / 当前工作目标）和“你在隐形”。只对敌对目标。
11. 游荡者是物理职业（强度合计、战斗风格槽）但不进额外攻击档位。游侠进档位（5 级第 1 档）。
12. 幸运一击只做近战（落空改命中，并且这一下 ×2），每天一次。
13. 刺杀：目标没察觉 ×(2 + 诡术)，否则 ×1.5；偷袭照常叠加。
14. 传奇主动消耗：大地崩裂怒气 3；万箭齐发、死亡印记无资源（只看 1 天冷却）。游侠、游荡者学传奇不加资源上限（没有资源）。
15. 经验 ×1.25：野蛮人·异乡人、游侠·异乡人或游侠向导经历、游荡者·罪犯。导师事件仍只在 VPE 桥（未改）。

## 没测到 / 需要实机看
- 不灭之怒真正撑住倒地（ShouldBeDowned 路径）与 10 秒后倒下；野外穿行的实际走速；隐匿伏击 / 暗影潜行对敌方 AI 索敌的效果。
- 宿敌的近战 / 射击命中加成、宿敌克星必中；幸运一击；淬毒刃的毒素；死亡印记击杀重置冷却；兽伴状态与对宿敌加伤；齐射 / 万箭齐发拿真实远程武器时的伤害（测试里施法者没拿武器，只测了不报错）。
- “选择宿敌”按钮与窗口、分页里的宿敌 / 偷袭说明行；原初斗士的 +4 在冒险者分页属性旁不显示“+4”标签（数值已生效）。
- 新图标、背景在游戏里的观感（预览图已检查）。
- 实战中怒气积攒 / 衰减的节奏是否合适。
