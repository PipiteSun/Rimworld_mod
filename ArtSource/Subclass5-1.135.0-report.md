# 术士子职业补完 1.135.0 报告

依据：ArtSource/NewContent3-1108-spec-DEF.md 的 E 项（原定 D——战斗大师、符文骑士、法师学派——已按 Peter「不需要完成 1.135」取消，D 没开始写，没有要回滚的东西；E 顺延为 1.135.0）。开工时版本 1.134.0。

## 先查重
- Defs 里术士原有子职业：异变心灵、龙魂、恶鬼之血（Subclasses2.xml）。神圣之魂、暗影魔法、时钟之魂都没有实装，三个都做了，没有跳过的。

## 结果
- 编译通过；`Test.ps1 -Only Subclass5` **10 条全过**（定义 1 条 + 每个子职业 3 条：3 级选择、核心效果、终极），日志无红字。
- 备份：ArtSource/backup-1.134.0（ClassAbilities.cs、Classes.cs、About.xml、使用说明.txt）。没跑完整测试，没有 git 提交 / 推送。

## 文件
- 新：`Defs/Subclasses5.xml`、`Source/ClassSubclass5.cs`、`Source/Subclass5Checks.cs`、`ArtSource/RaceArt/draw_subclasses5.py`、`Textures/LP/Classes/Subclass5/*.png`（13 个图标）、`Textures/LP/Classes/Subclass/Emblem_{DivineSoul,ShadowMagic,ClockworkSoul}.png`、预览 `ArtSource/Preview/Subclasses5-icons.png`。
- 改：`ClassAbilities.cs`（ClassEffects.Apply / Invalid 链、Power 乘 Subclass5.PowerFactor、施放后 Subclass5.AfterCast，各一处）、`Classes.cs`（Tick250 里调 Subclass5.Tick250）、About.xml、使用说明.txt。

## 实现（数值照抄设计）
- 挂在现有 LP_SubclassDef 框架：术士 3 级选，能力 3 / 7 / 11 / 15 级；消耗术士法力，强度看天赋强度；神圣之魂的亲和用 `<choices>`（键 dsAffinity）。
- 复用：治疗伤口的 `Heal` 效果壳、HediffComp_Conjured 召唤（ArcanaEffects.Summon）、LP_ACB_Shield 护盾、LP_StaunchBleeding、HediffCompProperties_ClassAura（天使形态的光环）、ClassSubclass.Fear（影犬的恐惧）、CastRangeExt（神恩庇佑、天使形态、神迹、暗影步、影之形态、秩序领域、秩序军团是自身 / 瞬移 / 全图类：approach=false；其余默认可以走近再放）。
- NPC：NpcClasses 会随机选到新子职业；神迹、秩序军团、暗影步（基地 / 地形类）不给 NPC 自动施放（补丁 NpcClasses.AiUsable）。
- 旧存档：GameComponent_Subclass5 读档后给「术士 3 级以上、还没选子职业」的殖民者发一封信（已经选过子职业的不能改，所以不提示）。

## 决定与偏差
- 神恩庇佑：做成每天 1 次的主动增益（1 小时手术成功率 +20%、包扎质量 +20%），没做「让自己的一次法术命中成功率 +20%」。
- 黄昏亲和：没有给混沌箭加「可选暗蚀伤害」，改为术士法术对血肉生物的伤害 +20%（按施法中的伤害判断）。
- 神恩共鸣的「法力恢复 ×2」：冥想或祈祷时每 250 刻额外 +0.2 术士法力（与武僧冥想回气同速），信奉神明的意识形态再 ×1.25。
- 天使形态没有做发光照明（原版状态没法给小人加光源）。
- 神迹被动和坟墓之力：在原版「检查倒地 / 死亡」之前判定（和死亡守卫同一处）以及 Pawn.Kill 前拦一次；神迹被动每天 1 次必定成功，坟墓之力每天判定 1 次（成功率 50%），火焰、神圣伤害（按受到的最后一次伤害类型）不触发。
- 影犬用原版座狼（没有就森林狼），召出后立刻去咬目标；猎物移除灵能隐形并挂「影犬猎物」。
- 影之形态的「远程命中 -30%」做成黑暗里敌人的远程伤害 30% 直接落空。
- 秩序之手：只做「让己方失败的豁免重新判定」（每天 2 次，同一张地图的己方都算）；原版没有暴击机制，「把暴击变成普通命中」和秩序领域的「免疫暴击」「法术成功率最低 60%」没做。秩序领域的「命中率最低 70%」改为近战命中 +30%、射击精度 +0.06。
- 机械亲和：修理没有单独的属性，用建造速度 ×1.3（同时会让建造变快）。
- 秩序军团：Biotech 的民兵机（没有 Biotech 时用原版剪刀机），玩家阵营、灵体召唤计时 1 天，到时消散；没有重新着色。没有机械师控制的玩家机械体在原版规则下可能被当成「无人控制」，我没在游戏里验证它们的行为。
- 魔涌狂野魔法：自燃、熵涌、混乱精魂三个坏结果改为从其余五个里重抽。

## 没有在游戏里看过的部分
- 选子职业窗口里三个新子职业的样子、图标在按钮栏的效果、天使光环和影之形态的状态显示。
- 影犬实际追咬、秩序构装在战斗里的行为、暗影步在真实黑暗地图上的使用（测试用了「黑暗」的测试开关）。
