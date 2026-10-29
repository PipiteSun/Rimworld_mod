# 1.36.0 德鲁伊、圣武士、术士、武僧 — 实现报告

规格：/mnt/project-files/dnd-genes/剩余职业实现要求.md。备份：ArtSource/backup-1.35.0（只备份了改动过的旧文件）。

## 内容
- VPE“DND职业”分页从 4 条路径增加到 8 条：德鲁伊（种植/驯兽，order 5）、圣武士（格斗/社交，6）、术士（社交/智识，7）、武僧（格斗，8）。每条 9 个能力、5 环，门槛 6/9/12/15/18。
- 双技能路径：`LP_ClassPathDef.alternateSkill`，两门技能任一达到门槛即可解锁；每个能力用 `LP_ClassAbilityExt.skill` 绑定一门技能；终极要两支的 4 环都学会（`ClassRules.AllPrereqsLearned`，只对四条新路径生效）且主技能 18。
- 新属性：LP_PrimalPower（野性，驯兽）、LP_VerdantPower（生长，种植）、LP_InnatePower（天赋，社交 + 术士血脉 +0.15）。
- 心灵系数改为数据驱动：路径 `classPsychic`（法师、牧师、德鲁伊、术士为 true），能力 `psychicMode` 可覆盖（圣武士神术、武僧“气”为 Yes）。法师、牧师数值与 1.30.1 一致（已检查）。
- 术士血脉：力能龙裔、四种元素裔、巨灵、泰坦、风暴巨人、云巨人、六臂蛇人，门槛 −2，天赋强度 +0.15。巫妖不能学圣武士、德鲁伊（手册也会跳过）。
- 冒险者手册：考虑新路径；`Classes.Paths` 按 order 排序，同分时老路径优先。失败提示加上种植、驯兽。
- 云游导师：新增 LP_Mentor_Druid / Paladin / Sorcerer / Monk，导师的主副技能都拉到 16。
- 背景特性：侍僧 → 圣武士、隐士 → 武僧，路径经验 ×1.25。
- 美术：36 个图标 + 4 张背景（ArtSource/RaceArt/draw_class_art2.py，复用 draw_class_art.py 的画法）。
- 没有改主程序集，也没有新研究。

## 与规格的偏差
- 半径类数值按属性线性缩放，没用 √。
- 熊形没有换贴图，只有尘土特效；伤害/减伤/禁用工作照做。
- 武僧“徒手”只认空手或短棍（MeleeWeapon_Club）。
- 乘风步不能穿过敌人。
- 召唤野兽用原版森林狼（Wolf_Timber）加灵体状态。
- 测试用 `Test.ps1 -VPE`，没有单独的 -Only 分组（那需要改主程序集）。

## 测试（设备，Test.ps1 -VPE）
- 第 1 次：编译通过；旧检查“手册在没有其他技能 ≥6 时拒绝”失败（智识 8 的法师现在还能领悟术士）→ 改检查。
- 第 2 次：血脉天赋强度检查失败（力能龙裔有社交天赋差 −4，检查基准写错）→ 改检查；顺手给测试小人先清掉自带武器。
- 第 3 次：**PASS，431 项**（含 1.36.0：8 路径 72 能力定义、德鲁伊任一技能解锁、终极门槛、巫妖限制、血脉门槛与加成、心灵敏感度翻倍/不变、法师牧师回归、荒野变形收武器与系数、至圣斩对亡灵 ×2、35 个新能力各施放一次无异常、颤心掌爆发、存读档：熊形+收起的武器、誓仇标记、魔涌、空灵之身）。
- 日志里有 3 条 WARN “Could not resolve reference … Lord_N … curParent=MentorVisit”，出现在存读档阶段，来自 1.31.0 云游导师自检的访问记录，不是这一版引入的，不影响结果。

## 改动文件
Bridges/VPE/Source：VPEBridge.cs、Abilities.cs、BridgeChecks.cs（改为 partial）、Manual.cs、Mentor.cs；新增 Classes2.cs、Druid.cs、Paladin.cs、Sorcerer.cs、Monk.cs、Classes2Checks.cs。
Bridges/VPE/Defs：Paths.xml、Stats.xml、Events_Mentor.xml；新增 ClassDruid.xml、ClassPaladin.xml、ClassSorcerer.xml、ClassMonk.xml。
Bridges/VPE/Textures/LP/Classes/{Druid,Paladin,Sorcerer,Monk}/（40 张）。About/About.xml、使用说明.txt、ArtSource/RaceArt/draw_class_art2.py。
