# DND边缘大陆 1.27.0：职业路径（VPE 桥接）实现要求

> 日期：2026-10-25。对照 1.26.0（一体化基因工作台线程正在写）。
> Peter 的要求：按 VPE 的思路加入法师、战士、牧师三条路径；学习能力要达到技能要求（战士=格斗，牧师=社交，法师=智识）。
> 相关文档：[DND职业与VPE原版协调评估.md](DND职业与VPE原版协调评估.md)、[DND职业能力构思-法师战士游侠.md](DND职业能力构思-法师战士游侠.md)、[DND职业构思.md](DND职业构思.md)（牧师）。
> 这版只做三条路径本身。角色卡/共用经验池/法术位（DND职业体系实现方案.md）不做，Peter 已选 VPE 路线。

---

## 一、总体规则

| 项目 | 规则 |
|---|---|
| 加载 | 全部放在 `Bridges/VPE/`，`LoadFolders.xml` 加 `<li IfModActive="vanillaexpanded.vpsycastse">Bridges/VPE</li>`。没装 VPE 时一个 Def、一行代码都不加载，主程序集不引用 VEF.dll / VanillaPsycastsExpanded.dll |
| 路径 | 三个 `PsycasterPathDef`（子类 `LP_ClassPathDef`）：`LP_Path_Fighter` 战士、`LP_Path_Wizard` 法师、`LP_Path_Cleric` 牧师，放在 VPE 灵能页签里单独一个页签 **“DND职业”**（`tab`） |
| 解锁路径 | 照 VPE：要有心灵链接，花 1 个灵能点；**另加**职业技能 ≥ 6（战士格斗、法师智识、牧师社交）。不满足时路径显示锁住，`lockedReason` 显示“需要格斗 6 级”等 |
| 学习能力 | 照 VPE：前置能力已学 + 有灵能点。**另加**每个能力自己的技能要求（见各表“技能”列：1 环 6、2 环 9、3 环 12、4 环 15、5 环 18）。不满足时能力按钮灰显，悬停提示“需要格斗 12 级”。神经成型器（VPE 自动为每个能力生成）同样受技能要求限制（`ignoreLockRestrictionsForNeurotrainers=false`，并在成型器使用判断里加同一条） |
| 施放 | 已学会的能力不因技能下降而失效（只卡学习，不卡施放），但效果随当前技能变化 |
| 强度 | 三个新属性（StatDef，显示在角色属性里，类别“灵能”）：**战技强度**（LP_MartialPower，看格斗）、**奥术强度**（LP_ArcanePower，看智识）、**神恩强度**（LP_DivinePower，看社交）。公式 **0.5 + 技能/20**：6 级 0.8，10 级 1.0，15 级 1.25，20 级 1.5。各能力的伤害、治疗、持续时间、半径按下表乘这个倍率。心灵敏感度**不**再乘（避免双重放大），只影响精神力消耗照 VPE 原规则 |
| 资源 | 精神力 + 心灵熵，照 VPE。战士的技能熵低、精神力低（它是“战技”不是法术）；法师熵高；牧师居中 |
| 经验 | 在 VPE 原有“精神力上涨给经验”之外：**已解锁某条职业路径的人，职业技能每获得 100 点技能经验，VPE 灵能经验 +1**（每人每天上限 50）。多条路径取各自技能分别算。只有解锁了路径才算，避免所有殖民者凭空涨灵能 |
| 入门 | 新物品 **冒险者手册**（LP_AdventurersManual，炼金桌制作：纸 20 + 金 10，研究“炼金术”；黑网商队/法鲁恩殖民者商队有售 ~800 银）。使用：没有心灵链接且任一职业技能 ≥ 8 的人获得 1 级心灵链接（和神经成型器一样），同时免费解锁对应路径（技能最高的那个）；已有链接的人用了只解锁路径（仍要技能 ≥ 6）。一本只能用一次 |
| 和现有内容 | 巫妖的魂火箭/魂能缝合、夺心魔灵能、力能龙裔吐息等都不进路径，照旧。巫妖可以学法师路径（设定上它本来就是法师）；夺心魔智识高也能学法师。元素裔/巨灵不影响 |

---

## 二、战士（LP_Path_Fighter，格斗，战技强度 M）

定位：近战自我增益 + 小范围控制。精神力消耗低（0.05～0.15），熵低。全部 `showUndrafted=false` 除 回气。

| 环 | 技能 | 能力 | 效果 | 消耗/冷却 | 实现 |
|---|---|---|---|---|---|
| 1 | 格斗 6 | **回气** LP_SecondWind | 自己 600 刻内治疗共 20×M 点伤势（优先流血伤口） | 精神力 0.08，熵 5，冷却 2 小时 | 自定义 Ability 类 + Hediff `LP_SecondWind`（HediffComp 每 60 刻治疗 2×M） |
| 1 | 格斗 6 | **战吼** LP_BattleCry | 半径 4×√M 内敌对人形与动物获得 “震慑” 5 秒：移动 ×0.7、命中 ×0.8 | 精神力 0.06，熵 8，冷却 1 小时 | 自定义 Ability（范围施加 Hediff `LP_Shaken`，持续 300×M 刻） |
| 2 | 格斗 9 | **绊摔** LP_Trip（前置：战吼） | 近战距离（范围 1.5）目标击晕 1.5×M 秒并击退 1 格；体型 ≥ 2 的目标只减速不击退 | 精神力 0.08，熵 6，冷却 30 秒 | 自定义 Ability，复用主程序集 `Knockback` |
| 2 | 格斗 9 | **动作如潮** LP_ActionSurge（前置：回气） | 自己 10×M 秒：近战冷却 ×0.6、移动 ×1.3 | 精神力 0.12，熵 10，冷却 1 天 | Hediff `LP_ActionSurge`（statFactors MeleeCooldownFactor 0.6、MoveSpeed 1.3）`durationTimeStatFactors` LP_MartialPower |
| 3 | 格斗 12 | **武器专精** LP_WeaponMastery（前置：动作如潮） | 自己 1 天：近战伤害 ×(1 + 0.3×M)（6 级 ×1.24，20 级 ×1.45） | 精神力 0.15，熵 8，冷却 1 天 | Hediff `LP_WeaponMastery`，用 VEF 的 `VEF_MeleeAttackDamageFactor`；严重度 = M，按严重度分 5 档（0.8/1.0/1.15/1.3/1.5） |
| 3 | 格斗 12 | **不屈意志** LP_Indomitable（前置：绊摔） | 自己 1 小时×M：疼痛 ×0、不会因疼痛倒地、免疫击晕，精神崩溃阈值 −15% | 精神力 0.15，熵 10，冷却 1 天 | Hediff `LP_Indomitable`（painFactor 0 + 主程序集 OncePerCooldown 不用；击晕免疫用 Harmony 前缀 `StunHandler.StunFor` 同现有近战击晕的写法） |
| 4 | 格斗 15 | **旋风斩** LP_Whirlwind（前置：武器专精） | 对相邻 8 格所有敌对目标各打一次当前近战武器伤害 ×M | 精神力 0.15，熵 12，冷却 1 分钟 | 自定义 Ability：取 `pawn.meleeVerbs.TryGetMeleeVerb` 的伤害，直接 `TakeDamage`（伤害类型用武器的） |
| 4 | 格斗 15 | **守护姿态** LP_GuardStance（前置：不屈意志） | 自己 30×M 秒：护甲（利器/钝器）+20%×M，但移动 ×0.7；半径 3 内友军受到的伤害 30% 转到自己身上 | 精神力 0.15，熵 12，冷却 1 天 | Hediff `LP_GuardStance` 分档护甲；伤害转移用 Harmony 后缀 `Pawn.PreApplyDamage`（友军受伤时） |
| 5 | 格斗 18 | **战意再临** LP_SurgeOfBattle（前置：旋风斩 + 守护姿态） | 自己 20 秒：近战冷却 ×0.5、伤害 ×1.5、免疫击晕；期间每次近战击杀使所有战士能力立即冷却完 | 精神力 0.3，熵 20，冷却 2 天 | Hediff `LP_SurgeOfBattle` + Harmony 后缀 `Pawn.Kill`（杀手带此 Hediff 时重置冷却） |

---

## 三、法师（LP_Path_Wizard，智识，奥术强度 A）

定位：远程伤害 + 工具法术。精神力 0.1～0.5，熵高。伤害用自定义 DamageDef **奥术力** LP_ArcaneForce（不受护甲…不，照 Bomb 规则按钝器护甲减免 50%，不点火）。

| 环 | 技能 | 能力 | 效果 | 消耗/冷却 | 实现 |
|---|---|---|---|---|---|
| 1 | 智识 6 | **魔法飞弹** LP_MagicMissile | 远程 28 格，发射 3 发追踪飞弹，每发 4×A 奥术力（6 级共 9.6，20 级共 18） | 精神力 0.08，熵 8，冷却 10 秒 | `VEF.Abilities.Ability_ShootProjectile` 发 1 发 + 自定义类连射 3 发；投射物 `LP_MagicMissileBullet`（主程序集 Projectile 子类，必中：`Projectile_Explosive` 不行，用 `Bullet` 子类覆盖 `Impact` 直接伤害） |
| 1 | 智识 6 | **法师护甲** LP_MageArmor | 自己 1 天×A：利器/钝器护甲 +0.25（像穿了一层防弹衣） | 精神力 0.1，熵 6，冷却 1 天 | Hediff `LP_MageArmor` statOffsets ArmorRating_Sharp/Blunt 0.25 |
| 2 | 智识 9 | **火球术** LP_Fireball（前置：魔法飞弹） | 范围 24，半径 2.5 爆炸，火焰伤害 15×A，点燃 | 精神力 0.15，熵 15，冷却 30 秒 | `VEF.Abilities.Ability_Explode`，`powerStatFactors` LP_ArcanePower；复用 1.22.0 火球卷轴的数值 |
| 2 | 智识 9 | **睡眠术** LP_Sleep（前置：法师护甲） | 单体，目标昏睡 8×A 秒（机械体、意识 ≤ 30% 的目标无效；带 心智抗性 的目标按主程序集 MindResist 判定减半/免疫） | 精神力 0.12，熵 12，冷却 1 分钟 | Hediff `LP_MagicSleep`（意识 setMax 0.1）`durationTimeStatFactors` LP_ArcanePower；复用 `MindResist` |
| 3 | 智识 12 | **闪电束** LP_LightningBolt（前置：火球术） | 从施法者到目标一条直线（最长 20 格），线上每个目标受 20×A 电击伤害（原版 `EMP` 对机械体、我们新加的 `LP_Lightning` 对生物：钝器护甲减免，1 秒击晕）；穿透 | 精神力 0.18，熵 18，冷却 30 秒 | 自定义 Ability：`GenSight.PointsOnLineOfSight` 取线，逐格伤害 + 闪电特效 `FleckMaker.ThrowLightningGlow` |
| 3 | 智识 12 | **急速术** LP_Haste（前置：睡眠术） | 单体友军 1 小时×A：移动 ×1.5、工作速度 ×1.3、近战冷却 ×0.8；结束后 疲乏 10 分钟（移动 ×0.7） | 精神力 0.15，熵 12，冷却 1 小时 | Hediff `LP_Haste` + 结束时加 `LP_HasteFatigue`（HediffComp 自定义 `PostRemoved`） |
| 4 | 智识 15 | **石墙术** LP_WallOfStone（前置：闪电束） | 在目标格为中心、垂直于施法方向摆一条 5 格长的石墙（LP_ConjuredWall，耐久 200×A，1 天后消散）；有东西的格子跳过 | 精神力 0.25，熵 20，冷却 1 小时 | 自定义 Ability 生成 Building `LP_ConjuredWall`（`CompLifespan`） |
| 4 | 智识 15 | **传送术** LP_Teleport（前置：急速术） | 自己传送到视线内任意可站立格（范围 20×A，上限 40） | 精神力 0.2，熵 15，冷却 2 分钟 | 自定义 Ability：`pawn.Position = cell` + 两端 `FleckMaker` 效果；`rangeStatFactors` LP_ArcanePower |
| 5 | 智识 18 | **解离术** LP_Disintegrate（前置：石墙术 + 传送术） | 单体 30 格：60×A 奥术力伤害，无视护甲 50%；目标是建筑/机械体时伤害 ×2；杀死的人形目标不留尸体（变灰尘） | 精神力 0.4，熵 30，冷却 1 天 | 自定义 Ability；`armorPenetration 0.5`；死亡用 `Pawn.Kill` 后 `corpse.Destroy()` + 灰尘 filth |

---

## 四、牧师（LP_Path_Cleric，社交，神恩强度 D）

定位：治疗、增益、对亡灵。精神力 0.1～0.5，熵中。“神圣”伤害：新 DamageDef **神圣** LP_Radiant（对亡灵/夺心魔类/炎魔寒冰魔 ×2：亡灵 = 主程序集 `Necro.IsUndead`/骷髅/巫妖/死灵造物；邪魔 = 1.24.0 的 寒冰魔/炎魔 基因）。

| 环 | 技能 | 能力 | 效果 | 消耗/冷却 | 实现 |
|---|---|---|---|---|---|
| 1 | 社交 6 | **治疗伤口** LP_CureWounds | 单体（含自己）立即治疗共 20×D 点伤势，优先流血最严重的伤口 | 精神力 0.1，熵 8，冷却 30 秒 | 自定义 Ability：遍历 `Hediff_Injury` 按出血量排序 `Heal(amount)` |
| 1 | 社交 6 | **祝福** LP_Bless | 单体友军 1 小时×D：近战命中 +10%、射击精度 +10%、心情 +3（“受到祝福”） | 精神力 0.08，熵 6，冷却 10 秒 | Hediff `LP_Blessed` + Thought；`durationTimeStatFactors` LP_DivinePower |
| 2 | 社交 9 | **次级复原术** LP_LesserRestoration（前置：治疗伤口） | 单体：移除一个非永久疾病（食物中毒、流感、瘟疫、肌肉寄生虫、肠道蠕虫、感染）——取最严重的一个；同时止住所有出血 6 小时 | 精神力 0.15，熵 10，冷却 1 小时 | 自定义 Ability：疾病列表硬编码 + 允许 `HediffDef.isBad && makesSickThought`；止血复用 VPE 的 `VPE_BlockBleeding` 式 Hediff（自建 `LP_StaunchBleeding` tendable=false，bleedRate ×0） |
| 2 | 社交 9 | **神导术** LP_Guidance（前置：祝福） | 单体友军 1 小时×D：全局工作速度 +15%、学习速度 +30% | 精神力 0.1，熵 6，冷却 10 秒 | Hediff `LP_Guided` statFactors WorkSpeedGlobal 1.15 / GlobalLearningFactor 1.3 |
| 3 | 社交 12 | **神圣打击** LP_SacredFlame（前置：神导术） | 单体 20 格：20×D 神圣伤害（对亡灵/邪魔 ×2），忽略掩体 | 精神力 0.12，熵 12，冷却 15 秒 | 自定义 Ability 直接 `TakeDamage`（不走投射物） |
| 3 | 社交 12 | **驱散亡灵** LP_TurnUndead（前置：次级复原术） | 半径 6×√D 内敌对亡灵：恐惧逃跑 10×D 秒（有精神状态的用 `PanicFlee`；骷髅/死灵造物没有精神状态则改为击晕）；对巫妖只减速 ×0.5 | 精神力 0.2，熵 15，冷却 1 分钟 | 自定义 Ability：复用主程序集的亡灵判断 |
| 4 | 社交 15 | **群体治疗** LP_MassHeal（前置：神圣打击） | 半径 5 内所有友军各治疗 15×D 点 | 精神力 0.3，熵 20，冷却 1 小时 | 自定义 Ability（治疗伤口的范围版） |
| 4 | 社交 15 | **死亡守卫** LP_DeathWard（前置：驱散亡灵） | 单体友军 1 天：当本应因伤倒地时，立即治疗 30×D 点并移除本状态（一天内只触发一次）；期间疼痛 ×0.5 | 精神力 0.25，熵 15，冷却 1 天 | Hediff `LP_DeathWard` + Harmony 前缀 `Pawn_HealthTracker.MakeDowned`（有此 Hediff 且是伤势导致时拦截） |
| 5 | 社交 18 | **复生术** LP_Raise（前置：群体治疗 + 死亡守卫） | 目标 3 天内死亡的人形尸体复活（复用 1.22.0 复生卷轴逻辑），复活精神病概率按 D 降低（D ≥ 1.25 时不发生），复活者 2 天 “复生虚弱” | 精神力 0.5（要精神力满才能放），熵 40，冷却 5 天 | 自定义 Ability 调主程序集 `ItemScrolls` 的复生函数（抽成公共方法）；目标参数 `canTargetCorpses` |

---

## 五、文件结构

```
Bridges/VPE/
  Defs/
    Paths.xml              三条路径（LP_ClassPathDef）
    Stats.xml              LP_MartialPower / LP_ArcanePower / LP_DivinePower
    Abilities_Fighter.xml  Abilities_Wizard.xml  Abilities_Cleric.xml
    Hediffs.xml            所有增益/减益 Hediff
    Damages.xml            LP_ArcaneForce / LP_Lightning / LP_Radiant
    Things.xml             冒险者手册、LP_ConjuredWall、LP_MagicMissileBullet
    Recipes.xml            冒险者手册配方（recipeUsers LP_AlchemyTable）
    Patches/Traders.xml    黑网/法鲁恩商队加手册
  Source/
    VPEBridge.cs           [StaticConstructorOnStartup] Harmony "local.lich.phylactery.vpebridge"
    ClassPathDef.cs        LP_ClassPathDef : PsycasterPathDef（requiredSkill/requiredSkillLevel/CanPawnUnlock）
    ClassAbilityExt.cs     LP_AbilityExtension_Class : AbilityExtension_Psycast（requiredSkillLevel）+ Harmony 后缀 PrereqsCompleted + 成型器判断
    ClassStats.cs          StatPart_ClassSkill（0.5 + 技能/20）
    ClassXP.cs             Harmony 后缀 Pawn_SkillTracker.Learn → 灵能经验（每日上限）
    Abilities*.cs          各自定义 Ability 类（继承 VEF.Abilities.Ability，覆盖 Cast）
    Manual.cs              冒险者手册 CompUseEffect
    BridgeChecks.cs        自检（下节）
  Textures/LP/Classes/     27 个能力图标 + 3 张路径背景（python 生成，ArtSource/RaceArt/draw_class_art.py）
  Assemblies/LichPhylactery.VPEBridge.dll
```

- `Source/Build.ps1`：加 VPE 桥接段，引用 `$lootOutput`、`workshop\content\294100\2023507013\1.7\Assemblies\VEF.dll`、`2842502659\1.7\Assemblies\VanillaPsycastsExpanded.dll`。注意现有 Mythic 段用 `return` 跳过，会结束整个脚本：VPE 段必须放在 Mythic 段**之前**，或把两段的 SKIP 改成 if 块。
- `Source/Test.ps1`：加 `-VPE` 开关：Core 档加 `oskarpotocki.vanillafactionsexpanded.core` 和 `vanillaexpanded.vpsycastse`（放在本模组前），传 `-lichclassesonly`，只跑职业检查 + 存读档（像 -Isekai 的 -lichitemsonly）。
- 主程序集只加一个新文件 `Source/VPEBridgeHook.cs`（反射调 `LichPhylactery.VPEBridge.BridgeChecks.Run/AfterLoad`，同 MythicBridgeHook），`Checks.cs` 只改两处：ClassesOnly 分支、AfterLoad 调用。等 1.26.0 线程写完 Checks.cs 后再改。
- `LoadFolders.xml` 加一行；`About.xml` 1.27.0 + 更新日志；`使用说明.txt` 加“DND职业（需要 VPE）”一节——都等 1.26.0 线程完成后再动。
- 备份：改动前的文件复制到 `ArtSource/backup-1.26.0/`。

## 六、自检（BridgeChecks，-lichclassesonly）

1. 三条路径、27 个能力、3 个属性、3 个伤害、手册都存在；每个能力的 LP_AbilityExtension_Class 都有 path/level/requiredSkillLevel，且 requiredSkillLevel 与环数对应（6/9/12/15/18）。
2. 生成一个格斗 5 的人 + 心灵链接 1：`LP_Path_Fighter.CanPawnUnlock` = false；格斗调到 6 → true；法师/牧师路径仍 false。
3. 格斗 6 解锁战士路径，PrereqsCompleted(回气)=true，PrereqsCompleted(动作如潮)=false（前置未学）；学会回气后格斗 8 时动作如潮仍 false，格斗 9 → true。
4. 战技强度：格斗 6 = 0.8，10 = 1.0，20 = 1.5（三属性各测一个）。
5. 回气治疗量随 M 变化（格斗 6 vs 20 两个人各施放一次，600 刻后治疗差额比 ≈ 1.5/0.8）。
6. 火球术爆炸半径、睡眠术持续时间随 A；治疗伤口治疗量随 D；神圣打击对骷髅伤害 ×2。
7. 石墙术生成 5 段墙（遇障碍跳过），1 天后消散（强制推进 CompLifespan）。
8. 死亡守卫：目标受致倒伤害后未倒地且 Hediff 已移除。
9. 经验：解锁战士路径的人 `skills.Learn(格斗, 1000)` 后 VPE 经验 +10；未解锁者 +0；一天内超过 5000 技能经验后不再增加。
10. 冒险者手册：无链接、智识 8 的人使用后有 1 级心灵链接且法师路径已解锁；已解锁后手册消耗。
11. 存读档：学会的能力、Hediff（守护姿态）、经验日上限计数、召唤石墙都在。
12. 没装 VPE 时（Core 档）：`LichPhylactery.VPEBridge.BridgeChecks` 类型不存在，`LP_Path_Fighter` Def 不存在（主程序集 Hook 里检查，同 Mythic）。

## 七、报告

`ArtSource/Classes-1.27.0-report.md`：数值表（上面三表）、文件清单、测试结果、以及下面留给 Peter 的问题。

## 八、留给 Peter 决定的问题

1. 技能门槛 6/9/12/15/18 和强度公式 0.5 + 技能/20 是否合适？（另一种：门槛 4/8/12/16/20 更贴 5e 的 1/5/11/17 分段。）
2. 已学会的能力在技能掉下去之后**要不要失效**？现在按不失效做。
3. 技能经验转灵能经验的比例（100:1，每天 50 上限）会不会让战士/工匠升得比纯灵能者快？
4. 冒险者手册能给 1 级心灵链接，这会不会让心灵链接太容易拿？备选：手册只解锁路径，链接仍靠原版/VPE 途径。
5. VPE 会自动为 27 个能力生成神经成型器（任务奖励池里可能出现）。要不要把我们的能力从成型器里排除？
6. 德鲁伊（驯兽）、游侠、圣武士等后续路径的顺序。
