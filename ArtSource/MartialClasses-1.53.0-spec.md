# DND边缘大陆 1.53.0：物理职业第一批（武人之道 + 战士补完）实现要求

> 日期：2026-10-27。Peter 03:33 定的任务顺序第 2 项"完成物理职业第一批"，版本号 1.53.0（协调分配）。来源：《DND物理职业充实构思.md》第三、四节。所有细节由本线程定，不留待定问题。
> 设备上的 1.52.0 已只读核对：`Bridges/VPE/Source/VPEBridge.cs`、`Abilities.cs`、`Classes2.cs`、`AdventurerVPE.cs`、`Monk.cs`、`Defs/Abilities_Fighter.xml`、`Paths.xml`、`Stats.xml`，主程序集 `AdventurerFeats.cs`、`AdventurerUI.cs`、`Marilith.cs`。下文的类名、方法名以这些文件为准。
> 排在 1.52.1 传奇怪物之后。实现前从协调拿最新备份标签。
> **Peter 的第 7 项要求是性能检查（不要有重复计算降低 TPS）**，所以本版每个新挂钩都按第八节的性能规则写。

时间单位：1 秒 = 60 刻，1 小时 = 2500 刻，1 天 = 60000 刻。M = 战技强度（格斗），R = 射术强度（射击，新）。

---

## 一、范围

| 做 | 不做（以后的批次） |
|---|---|
| 斗志（打仗回精神力）、战技强度吃 VPE 等级、额外攻击、10 种战斗风格、冒险者分页"职业特性"小节 | 野蛮人、游侠、游荡者（第二、三批） |
| 战士改格斗/射击双技能路径，射击支 5 个能力，格斗支加冲锋、缴械击，5 个能力冷却调整 | 武器精通、进阶路径、物理专长第二批、职业武器、物理职业敌人 |

"物理路径" = 战士、圣武士、武僧。`LP_ClassPathDef` 加字段 `public bool martial;`，Paths.xml / ClassPaladin.xml / ClassMonk.xml 里这三条设 true（以后的野蛮人、游侠、游荡者也设）。

---

## 二、共用层：武人之道（`Bridges/VPE/Source/Martial.cs`）

### 1. 物理路径缓存（性能关键）

- `Martial.Cache`：`HashSet<int>` 存"至少解锁一条 martial 路径"的人的 `thingIDNumber`，另存每人的 `MartialInfo`（是否战士、是否只有武僧、额外攻击档位、VPE 等级加成）。
- 只在现有 `MapComponent_ClassAuras.Refresh`（每 250 刻）那一次遍历里更新，**不新开 MapComponent、不新开遍历**。另外在 VPE 解锁路径后立即刷新这个人（Harmony 后缀 `Hediff_PsycastAbilities` 的解锁路径方法，实现时核对方法名；找不到就只靠 250 刻刷新）。
- 所有命中、伤害、击杀挂钩第一步都是 `Martial.Is(pawn)`（一次 HashSet 查找），不是的人直接返回。

### 2. 斗志

| 项目 | 数值 |
|---|---|
| 命中 | 物理路径者对**敌对且未倒地**的目标造成 > 0 的伤害：精神力 +0.4%（`pawn.psychicEntropy.OffsetPsyfocusDirectly(0.004f)`） |
| 击杀 | 击杀敌对目标：+3% |
| 上限 | 每人每游戏小时（按 `TicksGame / 2500` 分桶）最多 +15%；没有心灵链接（`psychicEntropy` 为空或 `PsylinkLevel == 0`）不加 |
| 不算 | 召唤物（`LP_Conjured`）、友军、囚犯、自己的动物；建筑 |
| 挂钩 | 命中：现有 `ClassDamageMods`（`Pawn.PreApplyDamage` 后缀）里，`absorbed == false` 时，取 `dinfo.Instigator as Pawn` 判断 → 近战和射击一个挂钩全覆盖。击杀：现有 `ClassKillHooks`（`Pawn.Kill` 后缀） |
| 存档 | 新 `GameComponent` `MartialState`：`Dictionary<int,float> furyGiven` + `int furyHour`，换小时清空（照 `ClassState.Allow` 的写法） |
| 显示 | 不加新界面元素，放在冒险者分页"职业特性"小节里一行"斗志：本小时 +6.4% / 15%" |

### 3. 战技强度、射术强度吃 VPE 等级

- `Classes.Power(Pawn,SkillDef)` 和 `Classes.Power(Pawn,LP_ClassAbilityExt)`：技能是格斗或射击时，另加 `Martial.VpeBonus(p)` = min(0.4, 0.02 × (VPE 灵能等级 − 1))。灵能等级取 `Hediff_PsycastAbilities.level`。数值从 `MartialInfo` 缓存里取，不在每次求值时找 hediff。
- 施法职业的智识、社交、种植、驯兽不加。
- `StatPart_ClassSkill.ExplanationPart` 多一行"VPE 灵能等级 N：+x%"。

### 4. 额外攻击

| 档 | 条件 | 近战冷却 | 近战追击概率 | 射击冷却 |
|---|---|---|---|---|
| 1 | 任一物理路径 + 冒险者 ≥ 5 | ×0.8 | 15% | ×0.85 |
| 2 | 战士路径 + 冒险者 ≥ 11 | ×0.7 | 25% | ×0.75 |
| 3 | 战士路径 + 冒险者 ≥ 20 | ×0.6 | 35% | ×0.65 |

- 冒险者总开关关闭或这个人没有冒险者状态时，改看 VPE 灵能等级（5 / 11 / 20）。
- 只有武僧路径（没有战士、圣武士）的人：只在 `Monk.Unarmed` 时生效。
- 实现：状态 `LP_ExtraAttack`（3 个 stage，`MeleeCooldownFactor`；射击冷却用原版 `RangedCooldownFactor`，1.7 没有这个属性就 Harmony 后缀 `VerbProperties.AdjustedCooldown`，乘缓存里的系数）。在 250 刻刷新里按缓存档位 `Items.Give`/改 stage，不满足时移除。
- 追击：现有 `ClassMeleeHit`（`Verb_MeleeAttackDamage.ApplyMeleeDamageToTarget` 后缀）里，`Martial.Is` 且档位 > 0 且不在追击中（静态 `bool inFollowUp` 防递归）→ `Rand.Chance(p)` → `ClassMelee.Strike(caster,t)`，飘字"追击"。追击走原版近战结算，Isekai 力量、武器品质照常生效；追击不再触发追击，但可以触发巨武器大师和斗志。

### 5. 战斗风格（主程序集，和专长放一起）

战斗风格的效果挂钩大多和 1.48.1 专长在同一处（射程、命中、近战命中、受伤前），所以**写在主程序集**：`Source/AdventurerStyles.cs` + `Defs/Adventurer_Styles.xml`。主程序集不知道 VPE，靠桥登记的委托：

- `Styles.SlotsHook : Func<Pawn,int>`（桥注册）：有物理路径 = 1；战士路径且冒险者 ≥ 10（或冒险者关时 VPE 灵能 ≥ 10）= 2；否则 0。
- `LP_FightingStyleDef : Def`（label、description、iconPath、order、`workerClass`），存在 `Hediff_Adventurer` 里：`List<LP_FightingStyleDef> styles`、`int styleReadyTick`（改选后生效的刻）。冒险者总开关关闭时风格失效（同专长）。
- **选择**：冒险者分页"职业特性"小节里，空槽位显示可选风格列表（复用热修 2 的"选择 → 确定？"两次点击）。已有风格可以"改选"，改选后新风格 3 天（180000 刻）后生效，期间这一槽显示"适应中（剩 x 天）"。第一次选不用等。
- **敌人**：带冒险者等级 ≥ 3 的敌人，按 `Feats.EnemyRoles` 的 melee / ranged，50% 随机得 1 个对应风格（近战：防御、决斗、巨武器、守护、双武器；远程：箭术、防御、投掷）。敌人没有 VPE 也可以有（`SlotsHook` 只管殖民者选择）。

| 风格 defName | 效果 | 实现 |
|---|---|---|
| LP_Style_Archery 箭术 | 远程命中 ×1.08，射程 +2 格（只算武器射击，不算能力） | 命中：主程序集现有 `ShotReport.HitReportFor` 后缀（`FeatSharpshooterCover` 那里）乘系数；射程：`FeatRange` 后缀里加 |
| LP_Style_Defense 防御 | 身上有外套层或中间层且该件利器护甲 ≥ 0.4 时，利器、钝器护甲 +0.08 | 条件状态 |
| LP_Style_Dueling 决斗 | 主手单手近战武器（质量 < `Feats.TwoHandedMass` 2.5 kg）且没有副手武器时，近战伤害 ×1.2 | 条件状态，原版 `MeleeDamageFactor` |
| LP_Style_GreatWeapon 巨武器 | 双手近战武器（`Feats.TwoHanded`）时近战伤害 ×1.15、近战冷却 ×0.95 | 条件状态 |
| LP_Style_Protection 守护 | 相邻（8 格）友军被攻击时，攻击者命中 ×0.85（近战和远程） | 近战：现有近战命中率后缀（幸运那处）；远程：`HitReportFor` 后缀。只查 `Styles.protectors`（HashSet，在 250 刻刷新里更新）为空时直接返回 |
| LP_Style_Interception 拦截 | 3 格内友军受到伤害时，减 3 + 熟练加值（最低 0），每个拦截者每 10 秒一次，飘字"拦截" | `Pawn.PreApplyDamage` 前缀，同样先查 `Styles.interceptors` |
| LP_Style_TwoWeapon 双武器 | 背包里一把近战武器当副手：每次近战命中 30% 用副手再砍一刀 | 复用 `Marilith` 副手连击：`Marilith.OffhandSlots` 加一条"有双武器风格 → 至少 1"，触发概率按 30%（蛇人、双头巨人本来的规则不变，取较大）。如果 1.52.x 的六臂蛇人副手热修已加了右键"拿作副手"，同一个菜单对有双武器风格的人也显示 |
| LP_Style_BlindFighting 盲斗 | 射击不受烟雾影响；3 格内能看见暗影行走、隐身的单位 | 烟雾：`HitReportFor` 后缀把烟雾系数还原为 1（核对 `ShotReport` 的烟雾字段名）；隐身：接 `ShadowWalking` 现有的可见判断（观察者专长若已有接口就复用） |
| LP_Style_Thrown 投掷 | 投掷物（手榴弹、燃烧瓶、EMP 手雷、矮人投掷锤、本模组 `ItemsThrowables` 的投掷物）射程 ×1.3、命中 ×1.1 | 判断：武器 `weaponTags` 含 `GrenadeDestructive`/`GrenadeEMP`/`GrenadeSmoke` 或本模组投掷物的 tag；同样在 `FeatRange`、`HitReportFor` 两处 |
| LP_Style_Unarmed 徒手 | 没拿武器时拳脚伤害 ×1.6；徒手命中 20% 抓住目标：`LP_Grappled` 2 秒（移动 ×0） | 条件状态 + 主程序集近战命中后缀 |

- **条件状态**：一个 `LP_StyleActive` 状态，stage 按"防御 / 决斗 / 巨武器 / 徒手"的组合写死 4 个 bit 位太多，改成 4 个独立的小状态 `LP_StyleDefense`、`LP_StyleDueling`、`LP_StyleGreatWeapon`、`LP_StyleUnarmed`，**在冒险者状态的稀有刻（250 刻）里按装备给/移除**，不在每次攻击时检查装备。装备变化最多晚 4 秒生效，可以接受。
- 冒险者状态现在没有 250 刻钩的话，加在 `Hediff_Adventurer.Tick` 里 `pawn.IsHashIntervalTick(250)`。

### 6. 冒险者分页"职业特性"小节

位置：专长之后、史诗恩赐之前。没有物理路径的人画灰色占位行"职业特性：解锁战士、圣武士或武僧路径后出现"。

```
职业特性
  额外攻击 第 2 档：近战冷却 ×0.70，追击 25%，射击冷却 ×0.75        （悬停：档位表）
  斗志：本小时 +6.4% / 15%                                            （悬停：规则）
  VPE 灵能等级 8：战技强度、射术强度 +14%
  战斗风格 ① 巨武器 [改选]   ② （冒险者 10 级解锁）
```

- 主程序集 `AdventurerUI` 加委托 `ClassLinesHook : Func<Pawn,List<string>>`（桥注册，返回前三行；只在分页打开时调用，不缓存也可以）。战斗风格那行主程序集自己画。
- 升级信件：冒险者 5、10、11、20 级时，信件分项里多一行（"额外攻击 第 1 档"、"可以再选一个战斗风格"）。主程序集加 `LevelUpLinesHook : Func<Pawn,int,IEnumerable<string>>`，桥注册。

---

## 三、战士补完

### 1. 双技能路径

- `Paths.xml` 的 `LP_Path_Fighter`：加 `<alternateSkill>Shooting</alternateSkill>`、`<martial>true</martial>`；`lockedReason`/描述写"需要格斗或射击 6 级"。
- `LP_ClassAbilityExt` 加 `public bool bestSkill;`：true 时 `Skill` 返回路径主副技能里当前等级较高的那个（学习门槛和强度都按它）。**回气、战吼**设 true。其余 7 个旧能力不变（看格斗）。
- `AdventurerVPE.SkillAbility`：`Shooting` → 敏捷。`PathAbility` 战士仍取力量、敏捷较高者。
- `AdventurerVPE.Role`：战士路径且射击 > 格斗时返回 `"ranged"`（`Legend` 自动投点已有 ranged 类就用；没有就仍返回 `"melee"`，实现时核对）。
- 新属性 **射术强度** `LP_MarksmanPower`（Stats.xml，`StatPart_ClassSkill` skill = Shooting，displayPriority 4213）。说明："DND 战士射击支的强度倍率：0.5 + 射击等级/20，另加冒险者敏捷调整值与 VPE 灵能等级加成。"
- 战技强度说明补一句"战士的回气、战吼看格斗和射击里较高的一项"。
- 冒险者手册、云游导师不用改代码（`Level` 已是 max(主,副)，导师把主副技能都抬到 16）。确认导师给战士也拿上枪时，`LP_Mentor_Fighter` 的 PawnKind 不改（仍是近战导师）。
- 终极：战意再临（格斗支，不变）和新的死神之眼（射击支）。死神之眼只要连射一个前置，**不走** `ClassRules.AllPrereqsLearned` 的双支要求（那条只对 1.36.0 新路径生效，战士本来就不受影响，核对一下）。

### 2. 冷却与数值调整（Abilities_Fighter.xml）

| 能力 | 冷却 现 → 新 | 其他改动 |
|---|---|---|
| 动作如潮 LP_ActionSurge | 60000 → 15000 | 状态 `LP_ActionSurge` 加射击冷却 ×0.6（同额外攻击的射击冷却写法） |
| 武器专精 LP_WeaponMastery | 60000 → 2500 | 持续时间从 1 天改成 5000 × M 刻（2 小时 × M）；射击伤害同样 ×（与近战同档的系数），在 `ClassDamageMods` 里按 `dinfo.Instigator` 有此状态且 `dinfo.Weapon` 是远程武器时乘 |
| 不屈意志 LP_Indomitable | 60000 → 15000 | |
| 守护姿态 LP_GuardStance | 60000 → 5000 | |
| 战意再临 LP_SurgeOfBattle | 120000 → 60000 | |

描述文字里的冷却同步改。

### 3. 新能力（7 个）

技能门槛照旧 1～5 环 = 6/9/12/15/18。位置写 `level` 和 `order`，射击支放在树的右侧（order 大于格斗支）。

| 环 | 技能 | 能力 | 前置 | 效果 | 精神力 / 熵 / 冷却 | 实现 |
|---|---|---|---|---|---|---|
| 1 | 格斗 6 | **冲锋** LP_Charge | 无 | 目标 8 格内敌人：跳到它身边（`PawnFlyer`），落地立刻一次近战结算，期间伤害 ×(1 + 0.3×M)；体型 ≤ 1 的目标击晕 60 刻，飘字"撞倒" | 0.05 / 4 / 1200 | 自定义 `LP_ChargeFlyer : PawnFlyer`（覆盖落地 `RespawnPawn`，照 1.36.0 龙族之翼的 flyer 写法）；伤害加成用 `Hediff_ClassStrike`（旋风斩同款）只在那一击存在 |
| 3 | 格斗 12 | **缴械击** LP_DisarmingStrike | 绊摔 | 相邻敌人一次近战结算；然后判定缴械：成功率 = clamp(0.5 + 0.1 ×（施法者力量调整值 + 熟练加值 − 目标的）+ 0.2 ×（M − 1）, 0.15, 0.9)，体型 ≥ 2 再 ×0.5。成功：`equipment.TryDropEquipment` 后把武器放到目标 2 格外（`GenPlace.TryPlaceThing` 在一个随机距离 2 的格子）。机械体、动物、没拿武器的只吃那一击 | 0.06 / 5 / 1800 | 目标没有冒险者状态时调整值、熟练加值按 0 / +2 |
| 1 | 射击 6 | **稳固瞄准** LP_SteadyAim | 无 | 自己 900 刻：射击预热 ×0.5、远程命中 ×1.15、移动 ×0.5 | 0.04 / 4 / 1800 | 状态：`AimingDelayFactor` 0.5、`MoveSpeed` 0.5；命中走第二节的 `AimHook`（见下） |
| 2 | 射击 9 | **压制射击** LP_SuppressingFire | 稳固瞄准 | 目标格（射程 = 当前武器射程）半径 3：连射 6 发当前武器的子弹（每 6 刻一发，落点区域内随机格，伤害 ×0.5），区域内敌人获得"被压制"360 刻：移动 ×0.6、远程命中 ×0.75 | 0.08 / 6 / 3600 | 发射用 `Projectile.Launch`（launcher = 施法者，equipment = 武器），伤害倍率登记到 `Martial.Shots`（下）；状态 `LP_Suppressed`；没拿远程武器时不能施放（`ValidateTarget` 提示"需要远程武器"） |
| 3 | 射击 12 | **精准射击** LP_PrecisionShot | 压制射击 | 单体：立刻对目标打一发当前武器的子弹，必中，伤害 ×(1.5 + 0.5×R)，穿甲 +0.3。模式：**要害**（只有上面的效果）/ **打腿**（伤害 ×0.7，目标移动 ×0.5 持续 600 刻）/ **打手**（伤害 ×0.5，按缴械击的公式判定打落武器） | 0.08 / 6 / 1200 | 必中：`Projectile.Launch` 用 `ProjectileHitFlags.IntendedTarget` 且 usedTarget = 目标；倍率、穿甲登记到 `Martial.Shots`。模式：学会的人征召时多一个切换按钮 `Command_Action`（`Pawn.GetGizmos` 后缀，只对 `Martial.Is` 且学会此能力的人加），模式存 `MartialState` |
| 4 | 射击 15 | **连射** LP_RapidFire | 精准射击 | 自己 600 × R 刻：射击冷却 ×0.5；连发武器（`burstShotCount > 1`）每轮多 1 发 | 0.12 / 10 / 5000 | 状态；多一发：Harmony 后缀 `Verb.TryStartCastOn`，施法者有状态且是 `Verb_LaunchProjectile` 且 burst > 1 → `burstShotsLeft++`（`AccessTools.FieldRefAccess<Verb,int>("burstShotsLeft")`）。不改 `verbProps`（共享 Def） |
| 5 | 射击 18 | **死神之眼** LP_DeadEye | 连射 | 自己 1200 刻：远程必中、射击伤害 ×1.5、无视掩体；期间每次射杀，所有战士能力立刻冷却完成 | 0.3 / 20 / 60000 | 必中和无视掩体：`AimHook`；伤害：`ClassDamageMods`；射杀重置：`SurgeKillReset` 的条件里加"击杀者有 `LP_DeadEye` 且是远程伤害"（现在只认战意再临） |

**`AimHook`**：主程序集 `FeatSharpshooterCover`（`ShotReport.HitReportFor` 后缀）里加一个委托 `Feats.AimHook : Func<Pawn,float>`（桥注册，返回瞄准系数；≥ 100 表示必中），乘到 `AimOnTargetChance` 用的字段上（核对 `ShotReport` 的 `factorFromShooterAndDist`；必中同时把 `coversOverallBlockChance` 设 0）。桥里的实现只查一次 `Martial.Is` + 缓存的"当前射击状态位"（稳固瞄准 / 被压制 / 死神之眼，在状态 `PostAdd`/`PostRemoved` 里维护一个 `Dictionary<int,byte>`），不在每发子弹时遍历 hediff。

**`Martial.Shots`**：`Dictionary<Projectile,ShotMod>`（伤害倍率、穿甲加值）。Harmony 后缀 `Projectile.DamageAmount`、`Projectile.ArmorPenetration` 两个 getter（核对 1.7 的属性名），字典为空时直接返回。`Projectile.Impact` 后缀里移除；另每 2500 刻清掉已销毁的。

**图标**：7 个能力图标，照 `ArtSource/RaceArt/draw_class_art.py` 的战士配色（铁锈主色），放 `Bridges/VPE/Textures/LP/Classes/Fighter/`；10 个战斗风格图标照 1.48.1 的 `draw_feats.py` 风格，放主模组 `Textures/LP/Adventurer/Styles/`。出一张预览 `ArtSource/Preview/MartialClasses-1.53.0.png`。

---

## 四、新状态一览（Bridges/VPE/Defs/ClassMartial.xml，文件名带 Class 前缀）

`LP_ExtraAttack`（3 stage）、`LP_SteadyAim`、`LP_Suppressed`、`LP_RapidFire`、`LP_DeadEye`、`LP_Hobbled`（打腿）、`LP_ChargeStrike`（冲锋那一击）。主程序集 `Defs/Adventurer_Styles.xml`：10 个 `LP_FightingStyleDef` + `LP_StyleDefense`、`LP_StyleDueling`、`LP_StyleGreatWeapon`、`LP_StyleUnarmed`、`LP_Grappled`。全部 `isBad` 照效果设，增益类 `everCurableByItem false`，带 `HediffComp_Disappears` 的写明时长。

---

## 五、需要改动的现有文件

| 文件 | 改动 |
|---|---|
| `Bridges/VPE/Source/VPEBridge.cs` | `LP_ClassPathDef.martial`；`LP_ClassAbilityExt.bestSkill` 与 `Skill`；`Classes.Power` 两个重载加 VPE 等级加成；`StatPart_ClassSkill` 说明行；`VPEBridge` 静态构造里调 `Martial.Register()` |
| `Bridges/VPE/Source/Classes2.cs` | `MapComponent_ClassAuras.Refresh` 里加 `Martial.Refresh(p)`（同一个循环）；`ClassMeleeHit` 加追击；`ClassDamageMods` 加斗志、武器专精射击、死神之眼；`ClassKillHooks` 加斗志击杀 |
| `Bridges/VPE/Source/Abilities.cs` | `SurgeKillReset` 条件加死神之眼远程击杀 |
| `Bridges/VPE/Source/AdventurerVPE.cs` | `SkillAbility` 射击 → 敏捷；`Role` 远程战士；注册 `Styles.SlotsHook`、`AdventurerUI.ClassLinesHook`、`LevelUpLinesHook`、`Feats.AimHook` |
| `Bridges/VPE/Defs/Paths.xml`、`ClassPaladin.xml`、`ClassMonk.xml` | `martial`、战士 `alternateSkill` |
| `Bridges/VPE/Defs/Abilities_Fighter.xml` | 冷却、武器专精持续时间、回气/战吼 `bestSkill`、7 个新能力 |
| `Bridges/VPE/Defs/Stats.xml` | `LP_MarksmanPower`；战技强度说明 |
| 主程序集 `Source/AdventurerFeats.cs` | `AimHook`；`FeatRange`、`HitReportFor`、近战命中、`PreApplyDamage` 四处挂钩里接战斗风格 |
| 主程序集 `Source/Adventurer.cs` | `Hediff_Adventurer.styles`、`styleReadyTick` 存档；250 刻条件状态刷新；`LevelUpLinesHook` 接进升级信件 |
| 主程序集 `Source/AdventurerUI.cs` | "职业特性"小节（第二节 6） |
| 主程序集 `Source/AdventurerEnemies.cs` | 敌人 50% 风格 |
| 主程序集 `Source/Marilith.cs` | `OffhandSlots` 认双武器风格 |
| 新文件 | `Bridges/VPE/Source/Martial.cs`、`MartialAbilities.cs`、`MartialChecks.cs`（`BridgeChecks` 的 partial）、`Bridges/VPE/Defs/ClassMartial.xml`；主程序集 `Source/AdventurerStyles.cs`、`Defs/Adventurer_Styles.xml` |
| `About.xml`、`使用说明.txt` | 1.53.0 + 更新日志；使用说明加【1.53.0 物理职业：武人之道与战士射击支】 |

备份：改动前的文件复制到 `ArtSource/backup-<协调给的最新标签>/`。报告 `ArtSource/MartialClasses-1.53.0-report.md`，本要求复制为 `ArtSource/MartialClasses-1.53.0-spec.md`。

---

## 六、兼容与存档

- 老存档：战士路径多一个副技能、多 7 个可学能力；已学能力不变；冷却缩短立即生效。`MartialState` 是新 GameComponent，旧档里没有时自动新建。`Hediff_Adventurer.styles` 读档为空时补空列表。
- 没装 VPE：桥不加载，斗志、额外攻击、射击支都没有；`Styles.SlotsHook` 为空 → 殖民者没有战斗风格槽，敌人照样可以随机到风格。
- Isekai：追击、冲锋、缴械击都走原版近战结算，Isekai 力量照常；射击支的子弹是武器自己的子弹，Isekai 的远程加成照常。不读 Isekai 数据。
- 百战觉醒、其他双持模组：双武器风格在 `Marilith.OffhandUser` 已排除双持模组的情况下同样排除。

---

## 七、自检（`MartialChecks`，`Test.ps1 -VPE -Only Martial`）

Peter 说测试简短即可，他自己实机玩。只做下面 12 条，全量套件不跑。

1. ★ 7 个新能力、`LP_MarksmanPower`、10 个风格 Def、新状态都存在；新能力 `requiredSkillLevel` 与环数对应，射击支 `skill` = Shooting。
2. ★ 战士路径：格斗 0 射击 6 可以解锁；都是 5 不行；格斗 6 射击 0 的人能学冲锋、不能学稳固瞄准。
3. ★ 回气 `bestSkill`：格斗 4 射击 12 的人能学回气，强度 = 射术强度。
4. ★ 射术强度：射击 10 = 1.0（+ 冒险者、VPE 加成另算）；VPE 灵能 11 级的人战技强度比 1 级多 0.2，法师奥术强度不变。
5. ★ 斗志：物理路径者对敌人造成伤害后精神力 +0.4%；连续 50 次后本小时只加到 15%；非物理路径者不加；对倒地目标不加。
6. ★ 额外攻击：冒险者 5 级圣武士有第 1 档；11 级战士第 2 档、11 级圣武士仍第 1 档；只有武僧的人拿武器时没有、空手时有。
7. 追击：把追击概率临时设 1，近战一次 → 目标受到两次近战伤害，且没有无限递归。
8. ★ 战斗风格：1 个槽；战士冒险者 10 级 2 个槽；改选后 `styleReadyTick` = 现在 + 180000，期间旧风格无效、新风格未生效。
9. 决斗：单手剑 + 无副手 → 有 `LP_StyleDueling`；换双手武器 250 刻后变成 `LP_StyleGreatWeapon`（有巨武器风格时）。
10. ★ 压制射击、精准射击：登记进 `Martial.Shots` 的子弹伤害倍率正确（精准 R=1 时 ×2.0），命中后从字典移除。
11. 死神之眼：有状态时 `AimHook` 返回必中；远程击杀后战士能力冷却为 0。
12. ★ 存读档：战斗风格、`styleReadyTick`、精准射击模式、斗志本小时计数都在；`Martial.Cache` 读档后 250 刻内重建。

另跑一次 `-Only Adventurer`（主程序集专长/分页没被弄坏）。

---

## 八、性能规则（为 Peter 第 7 项预先做好）

1. **不新开每刻循环**：250 刻刷新挂在现有 `MapComponent_ClassAuras.Refresh` 的同一次遍历里；主程序集条件风格挂在冒险者状态已有的 250 刻钩里。
2. **热路径先查缓存**：`PreApplyDamage`、`ApplyMeleeDamageToTarget`、`HitReportFor`、`AdjustedRange`、`AdjustedCooldown`、`Projectile.DamageAmount` 这些每发子弹、每一刀都会走的挂钩，第一行都是 HashSet / 空字典判断，不满足立即返回；不在这些地方用 LINQ、不遍历 hediff 列表、不调 `GetStatValue`（除了原版本来就会算的）。
3. **数值放进状态**：额外攻击、风格、射击增益都用 hediff 的 stat 系数，让原版属性缓存去算，不写会每次重新求值的 StatPart。
4. **字典有清理**：`Martial.Shots`、斗志计数、风格保护者集合都有清理时机（命中、换小时、250 刻刷新）。
5. 报告里列出本版新加的每个 Harmony 挂钩和它的"快速返回条件"，给第 7 项性能检查直接用。
