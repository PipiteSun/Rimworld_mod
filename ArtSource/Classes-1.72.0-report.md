# 1.72.0 职业体系脱离 VPE —— 第一版报告（战士 / 武僧 / 圣武士）

规格：ArtSource/Classes-1.72.0-spec.md。备份：ArtSource/backup-1.71.0（Source、Defs、Bridges/VPE、About、使用说明.txt、Assemblies）。

## 改动

### 主程序集（不引用 VEF / VPE）
- **Source/Classes.cs**：`LP_ClassDef`（mainSkill / alternateSkill / order / resource / martial / legacyPath / background / color / abilities[ability, level, capstoneSkill]），`ClassData`（存在 `Hediff_Adventurer.classes`，Scribe 键 `lpClasses`：levels、res、kept、freeUnlocks、migrated），`ClassSystem`（职业点、门槛、加点、能力同步、资源、施放检查与扣费、强度公式、主职业 / 角色），`StatPart_ClassPower`，`Dialog_ChooseClass`（窗口，不用浮动菜单），`GameComponent_Classes`（读旧档的提示信、斗志每小时计数、精准射击模式），设置 `classPsychic` / `classEntropy`。
- **Source/ClassAbilities.cs**：`Ability_Class`（原版 `RimWorld.Ability`：资源不足时灰掉、施放先扣资源、被动不出按钮、悬停写消耗和强度），`CompProperties_ClassEffect` / `CompAbilityEffect_Class` / `ClassEffects`（34 个能力的效果，从桥里原样搬来），`ClassMelee`、`Whirlwind`、`Charge`(+`LP_ChargeFlyer`)、`SuppressingFire`(+`Hediff_SuppressingVolley`)、`PrecisionShot`。
- **Source/ClassMartial.cs**：`Martial`（物理职业缓存、斗志、射击状态位、登记子弹、缴械、追击、分页行、风格槽），`MonkCore`、`PaladinCore`，状态类（`Hediff_MartialFlag`、`Hediff_StrikeFactor`、`Hediff_ScaledBuff`+`ScaledBuffExtension`、`HediffComp_ClassHeal`、`HediffComp_ClassAura`、`Hediff_Enmity`、`Hediff_QuiveringPalm`），Harmony 挂钩（见下），`ClassStartup` 登记 `Styles.SlotsHook` / `ITab_Adventurer.ClassLinesHook` / `Feats.AimHook`。
- **Source/ClassUI.cs**：冒险者分页的「职业」页（职业点、每个职业的等级 / 资源条 / 学习或 +1 级按钮、能力树每行 4 个、背景用原路径塔罗牌图）。
- **Source/ClassChecks.cs**：`-Only Classes`。
- **Defs/Classes.xml**：3 个 `LP_ClassDef`、`LP_ClassPowerBase` + 战技 / 射术 / 神恩强度（`StatPart_ClassPower`）、`LP_ClassBuffBase` 与三职业全部状态（defName 不变，`VEF_MeleeAttackDamageFactor` → 原版 `MeleeDamageFactor`）、`LP_ChargeFlyer`、`LP_EnmityAvenged`、34 个原版 `AbilityDef`（defName 与旧 VEF 能力相同）。
- **Textures/LP/Classes/**：Fighter、Monk、Paladin 文件夹和 Background_Fighter.png 从 Bridges/VPE/Textures 移来。
- 修改：`Adventurer.cs`（`classes` 字段 + 存档、250 刻调用 `ClassSystem.Tick250`、`PrimaryAbility` 主职业优先、升级信件写职业点、设置）、`AdventurerUI.cs`（顶部「冒险者 / 职业」两页、信件选项「给 X 选择职业」、待办写职业点、职业特性文案）、`AdventurerFeats.cs`（`IsCaster` 认新职业）、`AdventurerLegend.cs`（`Role` 认新职业）、`VPEBridgeHook.cs`（桥加载检查改看 `LP_ArcanePower`）。

### VPE 桥
- **删除**：Paladin.cs、Monk.cs、MartialAbilities.cs；Defs 的 Abilities_Fighter.xml、ClassMonk.xml、ClassPaladin.xml、ClassMartial.xml。
- **新 Defs/ClassLegacy.xml**：三条路径的 34 个 VEF 能力空壳（`Ability_ClassLegacy`，无按钮无效果）+ 武僧 / 圣武士路径定义（战士路径仍在 Paths.xml）。路径和旧能力保留，只为老存档读得进来、迁移时数个数。
- **Martial.cs 重写**：`ClassMigration` + `GameComponent_ClassMigration`（读档一次），`HideMovedPaths`（VPE 页签构造函数后缀，过滤 pathsByTab），`MartialWideTree`（保留），旧类名空壳（`Hediff_MartialFlag`、`Hediff_Enmity`、`Hediff_QuiveringPalm`、`Hediff_SuppressingVolley`、`LP_ChargeFlyer`、`MartialState`）。
- Abilities.cs：删掉战士能力、击晕免疫、守护姿态分担、战意再临（留 `Hediff_ClassStrike`）。Classes2.cs：删掉 ClassMelee、近战命中 / 受伤 / 恐慌挂钩，`ClassKillHooks` 只留召唤物不留尸体，`MapComponent_ClassAuras` 变空壳。
- VPEBridge.cs：`Classes.Paths` 不含三条旧路径；`LP_ClassPathDef.CanPawnUnlock` 对它们恒 false；`MartialBonus` 转给主程序集；旧路径的神经成型器拒用。
- Manual.cs：冒险者手册另给一次「职业门槛豁免」（`freeUnlocks`）；VPE 路径或豁免任一能给就能用。
- Mentor.cs：三职业导师的学生按职业等级算，导师本人给冒险者等级 + 职业等级，上课给“冒险者下一级所需经验 × 25% + 50”，不给 VPE 经验。
- Hediffs.xml / Stats.xml：移走的定义删掉。BridgeChecks / Classes2Checks / PerfChecks：只改到能编译（见“未测”）。MartialChecks.cs 重写为 `-VPE -Only Martial` 与 `-VPE -Only Classes`。

## 能力 → 职业等级

| 等级 | 战士 | 武僧 | 圣武士 |
|---|---|---|---|
| 1 | 回气、冲锋、稳固瞄准 | 武艺（被动）、疾风连击 | 至圣斩、圣疗 |
| 3 | 战吼、压制射击 | 拨挡箭矢 | 誓仇 |
| 5 | 绊摔、动作如潮 | 震慑拳 | 守护灵光（被动） |
| 9 | 武器专精、不屈意志、精准射击 | 乘风步、开掌技 | 神圣武器、勇气灵光（被动） |
| 13 | 缴械击、旋风斩、连射 | 金刚之躯 | 援护换位 |
| 17 | 守护姿态 | 颤心掌 | 放逐邪恶 |
| 20 | 战意再临（格斗 18）、死神之眼（射击 18） | 空灵之身（格斗 18） | 圣光化身（格斗 18） |

战士两支：格斗支 11 个、射击支 5 个，1/3/9/13/20 两支各有，5/17 只有格斗支。

## 资源数值

- 上限 = 2 + 职业等级/2（四舍五入，`2+(lvl+1)/2`）：1 级 3、5 级 5、11 级 8、20 级 12。新职业资源满。
- 消耗：基础 1，中阶 2，终极 3，被动 0。冷却与 1.71.0 相同。
  - 战士：回气 1、冲锋 1、稳固瞄准 1、战吼 1、压制射击 1、绊摔 1、动作如潮 2、武器专精 2、不屈意志 2、精准射击 1、缴械击 1、旋风斩 2、连射 2、守护姿态 2、战意再临 3、死神之眼 3。
  - 武僧：疾风连击 1、拨挡箭矢 1、震慑拳 1、乘风步 1、开掌技 2、金刚之躯 2、颤心掌 2、空灵之身 3。
  - 圣武士：至圣斩 1、圣疗 1、誓仇 1、神圣武器 2、援护换位 1、放逐邪恶 2、圣光化身 3。
- 恢复（每 250 刻，在职业者自己的冒险者状态里）：睡觉 +0.5（所有资源）；气：冥想 +0.2；神恩：冥想或祈祷（job defName 含 Pray / Worship）+0.2，信徒 / 神选者 ×1.25；斗志：命中 +0.08、击杀 +0.6（= 原精神力斗志 ×20），每人每小时最多 +3，有心灵链接时照旧加精神力。
- 「职业能力消耗心灵熵」开着时：每 1 点资源 +5 心灵熵（只对有心灵链接的人）。

## 决定（Peter 交给我定的）

1. 职业数据放在冒险者状态里的 `ClassData`（深存档），未分配职业点不单存，算成 `min(冒险者等级,20) − 职业等级总和`；所以“没有职业数据的旧存档 = 职业点等于冒险者等级”自动成立。
2. 门槛：第一个职业 主或副技能 ≥ 6；兼修 冒险者 ≥ 3 且主或副技能 ≥ 8。另外沿用 1.48.0 的规则：对应主属性 ≥ 13 也算达标（战士力量 / 敏捷较高者，武僧敏捷，圣武士魅力）。
3. 冒险者手册的豁免同时跳过技能和“冒险者 3 级”两道门槛，但不跳过“最多 3 个职业”和巫妖禁圣武士。
4. 旧的每环技能要求（6/9/12/15/18）去掉，只留终极的技能 18。
5. 物理职业强度加成用“物理职业等级合计”：+0.02 ×（合计 − 1），上限 +40%（合计最多 20，所以实际最多 +38%）。
6. 额外攻击档位改看职业等级：任一物理职业 5 级第 1 档，战士 11 / 20 级第 2 / 3 档；战斗风格第 2 槽改为战士 10 级。
7. 斗志做成战士的职业资源（换算 ×20），同时保留原来的加精神力。
8. 回气、战吼的“较高的一项”= 战技强度与射术强度取大。
9. 能力 defName 与旧 VEF 能力相同（不同 Def 类型可以同名），迁移按名字对应。
10. 迁移：每个学过的职业至少 1 级（就算冒险者等级的点不够，“不拿走能力”优先，于是这些人的职业等级总和可以暂时超过冒险者等级，职业点为 0，直到冒险者等级追上）；超过换算等级的能力记在 `kept`；退回 VPE 点 = 删掉的能力数 + 删掉的路径数。
11. 三条旧路径定义和 34 个能力空壳保留（老存档能读），不在页签显示、不能解锁；VPE 自动生成的这 34 个神经成型器还在（交易 / 掉落可能出现），但不能用。
12. 精准射击、压制射击的目标框射程写死 45（原版能力的射程不能随武器变），选目标时按当前武器射程拒绝。
13. 被动能力（武艺、两种灵光）也是原版能力，但不出按钮。
14. 心灵系数设置只影响原来 `psychicMode=Yes` 的能力（武僧 5 个、圣武士 5 个）。
15. 设置放在“冒险者”分组，不新开分组（分组总数仍是 19，现有检查不变）。
16. 灵光、武僧架势、物理职业缓存改在职业者自己的冒险者状态 250 刻里刷新，不再扫整张地图。
17. NPC / 敌人暂时不分配职业（职业点照算，但不自动选）。

## Harmony 挂钩

主程序集（新）：
- `Verb_MeleeAttackDamage.ApplyMeleeDamageToTarget` 后缀 `ClassMeleeHit`：至圣斩 / 神圣武器、追击。快速返回：不是物理职业者。
- `Pawn.PreApplyDamage` 前缀 + 后缀 `ClassDamageMods`：守护姿态分担；誓仇、武器专精 / 死神之眼远程倍率、拨挡箭矢、斗志。快速返回：物理职业缓存为空 / 射击状态位为空。
- `Pawn.Kill` 后缀 `ClassKillHooks`：誓仇得报、斗志、战意再临 / 死神之眼重置冷却。快速返回：缓存为空。
- `MentalStateHandler.TryStartMentalState` 前缀 `ClassCourage`；`StunHandler.StunFor` 前缀 `ClassStunImmunity`。
- `Projectile.DamageAmount` / `ArmorPenetration` getter、`Bullet.Impact`、`Projectile.Impact` 后缀（登记子弹，Shots 为空时返回）；`Verb.WarmupComplete` 后缀（连射）。
- `Pawn.GetGizmos` 后缀 `ClassGizmos`：「选择职业（N）」、精准射击模式。

VPE 桥：
- 新：`ITab_Pawn_Psycasts` 构造函数后缀 `HideMovedPaths`。
- 改：`CompPsytrainer.CanBeUsedBy` 后缀拒用旧路径神经成型器；`LP_ClassPathDef.CanPawnUnlock`（重写，不是挂钩）。
- 删（移到主程序集）：ClassMeleeHit、ClassDamageMods、CourageNoPanic、StunImmunity、GuardStanceShare、SurgeKillReset、MartialUnlockPath、MartialShot*、MartialRapidFireBurst、MartialPrecisionGizmo。

## 测试（只跑指定组）

| 命令 | 结果 |
|---|---|
| `Test.ps1 -Only Classes` | PASS 12 |
| `Test.ps1 -VPE -Only Classes`（迁移 + 存读档） | PASS 5 |
| `Test.ps1 -VPE -Only Martial` | PASS 7 |
| `Test.ps1 -Only Adventurer` | PASS 132（与之前相同） |
| `Test.ps1 -VPE -Only Adventurer`（额外跑的） | FAIL：旧检查“法术狙击射程 ×1.5”，实际 ×1.75——1.67.0 起 `SpellSniperRange` 就是 1.75，跟本次无关 |

没跑完整套件（按要求）。

## 没测到 / 需要在游戏里看

- 在游戏里真正点按钮施放（选目标、预热、目标框）：冲锋飞行落地、压制射击连射、精准射击子弹、援护换位距离、45 格的目标框。
- 「职业」页、选职业窗口、升级信件按钮的样子（文字长度、能力树排版）。
- 睡觉回资源、真实的祈祷 job 名（按 defName 含 Pray / Worship 判断）、信徒加成。
- 真的旧存档（只用假数据测了迁移）；中途卸掉 VPE 的存档。
- 云游导师完整流程（事件、来信、离开）。只测了上课给经验。
- 「职业能力消耗心灵熵」开着时的行为。
- 完整 `-VPE` 套件里关于这三条路径的旧检查已经过时（BridgeChecks 的 Definitions 仍期望 8 条路径 / 79 个能力、Unlocking 用战士路径、Classes2Checks 的圣武士 / 武僧解锁、MentorChecks 用战士路径的 VPE 学生），我只改到能编译，没有跑也没有重写。
- 主程序集的“神恩强度”属性现在不装 VPE 也存在：信仰神术（FaithMiracles）原来没有 VPE 时用简单公式，现在会读属性（多出冒险者魅力加成）；Demigod.cs 给它挂的圣者加成（StatPart_SaintDivine）不装 VPE 时也会生效。没跑 Faith / Ascension 组。
