# 1.82.0 邪术师 + NPC 职业 报告

规格：ArtSource/WarlockNpc-spec.md（含「决定」一节）。备份：ArtSource/backup-1.81.0-warlock-npc（改动前的 Classes.cs、ClassSubclass.cs、ClassAbilities.cs、ClassChecks.cs、ClassSubclassChecks.cs、MindResist.cs、Legendary.cs、Subclasses.xml、About.xml、使用说明.txt）。

## 新文件
- Defs/ClassWarlock.xml — 职业、契约强度、9 个状态、24 个能力（7 职业 + 15 宗主 + 2 传奇）、3 个宗主（LP_SubclassDef，带 invoc1 / invoc2 分支）
- Source/ClassWarlock.cs — Warlock（资源、短休、被动位、咒术、效果）、Hediff_Hex、GameComponent_Warlock、WarlockDamage / WarlockKill 补丁
- Source/NpcClasses.cs — NPC 职业分配、升级、AI 施放、两个设置
- Source/WarlockNpcChecks.cs — 13 条检查
- ArtSource/RaceArt/draw_warlock.py → Textures/LP/Classes/Warlock（22 图标 + Background）、Legendary（PatronAvatar、EldritchSoul）、Subclass（Emblem_Fiend / Archfey / GreatOldOne）；预览 ArtSource/Preview/Warlock-icons.png、Warlock-background.png

## 改动（都是一两行的挂钩）
- Classes.cs：ClassResource.Pact；LP_ClassDef.subclassLevel；ClassData.npc（存档 "npc"）；MaxResource / ResourceLabel / ResourceTip 加契约法力；Regen 跳过契约法力；Tick250 开头 NpcClasses.Tick、Regen 后 Warlock.Tick250；FreeCast 加宗主化身；ExposeClasses → ExposeNpcClasses；ClassSettings.Draw → NpcClasses.Draw；旧存档提示信改成十二个职业
- ClassAbilities.cs：Power × Warlock.PowerFactor；Invalid / Apply 链加 Warlock
- ClassSubclass.cs：PickLevelFor(c)（NeedsPick / CanPick / 提醒 / 职业页行）；SubclassChoice.distinctFrom（CanChoose）
- MindResist.cs：TryResist 减 Warlock.SavePenalty（咒术）
- Legendary.cs：SaveBonus 减 Warlock.SaveBonusPenalty（咒术 −3）
- ClassChecks.cs / ClassSubclassChecks.cs：旧检查的数量（12 职业、110 能力、24 传奇；子职业检查只数野蛮人 / 游侠 / 游荡者）+ 调用 CheckWarlock
- About.xml 1.81.0 → 1.82.0，描述末尾加一句；使用说明.txt 标题、第一部分一行、第二部分【1.82.0 邪术师与 NPC 职业】、更新日志一条

## 测试
- `Test.ps1 -Only 'Classes,NpcClasses'`：PASS 68（第一次跑在 NPC 治疗检查失败——AI 先挑了次级复原术，已改成治疗类优先；第二次碰到已知的 1.73.0「治疗伤口」偶发失败，第三次全过）
- `Test.ps1 -Only Adventurer`：PASS 176
- `Test.ps1 -VPE -Only Classes`：PASS 7
新检查 13 条：邪术师 6（定义、门槛 + 宗主、短休、魔能爆光束 / 射程、祈唤不重复 + 苦痛、咒术伤害 / 豁免），NPC 6（设置开关、派系风格、等级拆分 + 升级、传奇、战斗施放、治疗倒地友军）+ 读档 1。

## 没测到的
- 真实袭击里 AI 施放的整体手感、频率和性能（只测了单人 TryCast）；大规模袭击下的帧时间
- 支配人类 / 力场监牢 / 掷入地狱 / 黑暗谵妄 / 创造奴仆 / 秘法大师 / 宗主化身 在游戏里的实际效果（编译、定义、效果分发都过了，没有逐个施放检查）
- 魔鬼视觉在真实黑暗里的切换、邪魔 + 异界契约 ×1.25
- 选宗主窗口在 1 级的实际界面（逻辑测了，没截图）；新图标在游戏里的观感
- 剧情首领后来被 RaiseTo 提级时 NPC 职业的追加（Grow 逻辑测了，剧情路径没测）
- Isekai 共存模式、HAR 种族 NPC
