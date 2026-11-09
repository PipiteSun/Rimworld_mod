# 1.140.0 报告：盘点第 3 / 4 / 5 项 + 施法职业专长

依据：《DND边缘大陆缺口与优化盘点（1.139.0）》第一节第 3、4、5 行；Peter「完成345」「修改法师等法术职业获得专长」。

## 先查重
- 子职业：盘点列的 15 个（月亮结社、勇士、奉献 / 复仇之誓、战争 / 知识 / 坟墓领域、咒剑 / 天界宗主、死灵 / 预言 / 防护 / 幻术学派、风流剑客 / 魂刃）在 Defs 里都没有，全做了。法师学派 11-08 曾取消过，这次按「完成345」一并做。
- 亡灵：只有半巫妖和死亡骑士（1.52.1，塞伊 ≥5000 点 10% 带队、拉洛克古墓守卫）。噬尸鬼 / 尸妖 / 怨灵 / 吸血鬼都没有，做了四种；死亡骑士沿用 1.52.1 的，没有再做一个。
- 专长：双持者、盾牌大师、长柄武器大师、弩专家、冲锋者、擒抱者都没有；史诗恩赐原有 6 个（命运、不朽、威能、迅捷、心灵、战斗），照 5e 2024 补 4 个。

## 结果
- 编译通过；`Test.ps1 -Only Subclass6,UndeadHorde,CombatFeats` **32 条全过**（Subclass6 17 含读档、UndeadHorde 7 含读档、CombatFeats 8 含读档）。回归组见本文末尾。
- 备份：ArtSource/backup-1.139.0（About.xml、使用说明.txt、Factions.xml、Adventurer_Epic.xml、Adventurer.cs、Classes.cs、AdventurerUI.cs、ClassAbilities.cs、ClassSubclassChecks.cs、ClassLevelInfo.cs、EnemyRaces.cs、CharEditorCompat.cs）。

## 施法职业的专长：我选的做法
问题：战士有 11 个职业奖励专长（1、2 级和之后每个偶数级），武僧、圣武士 1、2 级各 1 个，施法职业一个都没有，只能用 4/8/12/16 的属性提升换——而施法者最需要属性提升（主属性决定强度），所以实际上法师几乎不会选专长，战争施法者 / 法术狙击 / 元素使这些法术类专长形同虚设。
做法：**施法奖励专长**——法师、牧师、德鲁伊、术士、邪术师、吟游诗人的职业等级 1、5、10、15 级各 1 个免费专长（多职业施法者按最高的施法职业算一条水位线，不重复发），不占属性提升；但**只能换法术类专长**（战争施法者、法术狙击、魔法学徒、元素使 ×4、仪式施法者、心灵感应、妖精之触、坚毅心智 ×3、治疗者、幸运、警觉、坚韧、观察者、技能专家、音乐家、演员，以及施法职业自己的两个职业专属专长）。选法术类专长时先扣它，再扣职业奖励专长，最后才扣属性提升；属性提升照旧能换任何专长。旧存档读档后按当前等级一次性补发（走 SyncFighterFeats 的水位线）；NPC 施法者立即随机选法术类专长。没有削任何东西。
没做：没有新增专门的施法专长（现有法术类专长 21 个，够选 4 次）；圣武士、器械师算半施法者，没给（它们已有圣武士 1、2 级奖励专长 / 器械师职业专长）。

## 文件
- 新：`Defs/Subclasses6.xml`、`Source/ClassSubclass6.cs`、`Source/Subclass6Checks.cs`；`Defs/UndeadHorde.xml`、`Source/UndeadHorde.cs`、`Source/UndeadHordeChecks.cs`；`Defs/Adventurer_Feats4.xml`、`Source/AdventurerFeats3.cs`、`Source/CombatFeatsChecks.cs`；`ArtSource/RaceArt/draw_subclasses6.py` → `Textures/LP/Classes/Subclass6/*`（69）、`Textures/LP/Classes/Subclass/Emblem_*`（15）、`Textures/LP/Feats/*`（6）、`Textures/LP/Undead/Xeno_*`（4），预览 `ArtSource/Preview/Subclasses6-icons.png`。
- 改：见使用说明 1.140.0 更新日志（Factions.xml、Adventurer_Epic.xml、ClassAbilities.cs、Classes.cs、Adventurer.cs、AdventurerUI.cs、CharEditorCompat.cs、ClassLevelInfo.cs、HeroTraits.cs、AdventurerFeats2.cs、HeroTraitsCombat.cs、LegendaryMonsters.cs、ChromaticDragons.cs、EnemyRaces.cs、ClassSubclassChecks.cs）。

## 实现要点
- 子职业挂在 LP_SubclassDef 框架上，写法同 1.135.0 的 Subclass5：主动效果 `S6_` 走 ClassEffects 链，被动在 Tick250（永久被动用 LP_S6PassiveBase 的 hediff，有能力就挂、失去就摘），补丁第一步都查 `Subclass6.Users`，没人学时零开销。能直接复用的效果（SelfBuff / PowerBuff / TargetBuff / Heal / SacredFlame / TurnUndead / Vow / BattleCry / CharmPerson）都复用了。
- 等级节奏沿用各职业原有的：德鲁伊、法师 2/6/10/14；牧师 1/2/6/8/17；邪术师 1/3/7/11/15（咒剑、天界也有 5 级魔能祈唤的选择）；战士、圣武士、游荡者 3/7/11/15。
- 亡灵军团：人形兵种（LP_FHBase，塞伊派系）+ 外表异种型 + 「之躯」hediff（Hediff_UndeadBody）；生成（PawnGenerator.GeneratePawn 后缀）就挂之躯；塞伊多一组 commonality 30 的 Combat 编组（红袍 3、噬尸鬼 12、尸妖 5、怨灵 2、吸血鬼 1），所以大约四分之一的塞伊袭击是亡灵军团；设置「真龙与传奇怪 → 亡灵军团」关掉后 FactionGroups.Process 后缀把亡灵换成普通远征军。`Subclass6.IsUndead` / 驱散亡灵 / 光耀翻倍都认它们。
- 战斗专长走 1.58 的 HeroInfo 缓存（新字段 f3 位），伤害 / 命中后的钩子接在 HeroCombat 已有的调用点，没有新加全局补丁；弩专家的预热 / 冷却另挂 Stance_Warmup 构造和 VerbProperties.AdjustedCooldown。

## 决定与偏差（照 5e 改编时的取舍）
- 勇士「额外战斗风格」没有接 1.53 的战斗风格系统（那是按选择的风格给掩码），改成固定的命中 / 精度 ×1.08、护甲 +5%。
- 月亮结社的「战斗变形」不换贴图 / 不变成动物，是一个 1 小时的强化状态（近战 ×1.4、护甲 +15%、移速 ×1.15）；元素形态的灼烧用现有 ClassAura（光耀伤害定义），描述写「灼烧」。
- 知识领域「读心」：目标近战闪避 −20、受伤 ×1.15；人形目标心智抗性没挡住再震慑。「过往之景」做成范围震慑 + 眩晕。
- 坟墓领域「通往坟墓」：下一次被友军攻击 ×2 后标记消失（Fiends.Defer 下一刻移除）。「死亡哨兵」：对伤势 >20 的友军施放治疗伤口 / 治疗真言时再治一次同样的量。
- 咒剑「幽影仆从」没召唤实体，是身边 2.5 格的光环劈砍；「诅咒大师」= 诅咒 ×1.5 并且被诅咒者打自己 ×0.8。
- 预言学派「预兆」照 1.135 时钟之魂的「秩序之手」做：自己失败的豁免每天 2 次（14 级 3 次）重判。
- 幻术学派「幻象现实」= 5 × 奥术强度个镜影（复用 1.81 的 LP_MirrorImage）。
- 风流剑客「无畏剑客」：目标身边没有自己的其他友军时近战 ×1.2（单挑）。
- 长柄武器：定义为质量 ≥2 kg 且带刺击 / 砍击工具的近战武器，所以长剑也算（原版没有长柄标签）。弩专家：中世纪以下科技的远程武器。
- 恩赐「法术追忆」= 职业能力消耗 −1（最少 1）、冷却 ×0.8；「恢复」= 每天一次伤势 >30 时治一半 + 平时每 250 刻 +1。
- 亡灵没有做「尸妖杀人起尸」「吸血鬼魅惑 / 化雾」；怨灵的「半实体」只减刀枪箭矢（光耀、火焰、银器、魔法、爆炸照常）。
- 怪物图鉴（1.136）没有登记这四种亡灵（它登记的是 DND 怪物 / 野兽 / 真龙，人形兵种不在里面）。

## 测试
- 新组：`-Only Subclass6` 17、`-Only UndeadHorde` 7、`-Only CombatFeats` 8。
- 回归（都过）：`Classes,Subclasses,NpcClasses` 94、`Adventurer` 179、`ClassFeats,Feats2,FeatsBuff` 40、`Subclass5,Warlock,EnemyRaces,LegendaryMonsters` 39、`ClassUI,ClassWindows` 13、`TitanWarrior` 8。四个旧计数检查按新内容改了：牧师 2 级现在有 1 个施法奖励专长（ClassChecks）、恩赐 6 → 10（AdventurerChecks3）、通用专长 35 → 41（Feats2Checks）、邪术师宗主 3 个的计数排除咒剑 / 天界（WarlockNpcChecks）、塞伊袭击允许的兵种加 4 种亡灵（FactionChecks）。
- 没过但不是这次的：`Factions` 每次在不同的随机商队检查上挂（157 / 167 / 170 条处），是记录里早有的派系组抽样不稳；`Subclass2` 第 13 条「塑能学派第二次超限导能反噬 0→0」在 1.139.0 的基线（stash 掉本次改动）上同样失败，是之前就有的；`TitanWarrior` 单独跑 8 条全过，排在 Subclass5 后面合跑时「远古践踏」一条偶发（随机护甲挡掉钝击）。
- 测试里遇到的坑：ClsPawn 1 级的人没有职业点，`ClassSystem.AddLevel` 会静默返回 false，要用 `SetLevel`；随机衣服会把 0 穿透的 12 点切割伤害全挡掉，测试伤口要写穿透 1；测试地图第 0 天，「上次用的日子」不能用 0 当未用过。
