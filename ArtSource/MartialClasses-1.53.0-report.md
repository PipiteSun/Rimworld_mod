# DND边缘大陆 1.53.0：物理职业第一批（武人之道 + 战士射击支）实现报告

> 日期：2026-10-27。按 `ArtSource/MartialClasses-1.53.0-spec.md` 实现。设备版本 1.52.1 → 1.53.0。
> 备份：`ArtSource/backup-1.52.1/`（改动前的 22 个源文件 / 定义 / 说明 + 两个旧 DLL；基准是 `backup-1.52.0-final`）。
> 预览图：`ArtSource/Preview/MartialClasses-1.53.0.png`。没有新研究项目（没占用任何研究格子）。

## 一、做了什么

### 共用层：武人之道（VPE 桥 `Bridges/VPE/Source/Martial.cs`）
- `LP_ClassPathDef.martial`：战士（Paths.xml）、圣武士（ClassPaladin.xml）、武僧（ClassMonk.xml）设 true。
- **物理路径缓存** `Martial.Cache`（HashSet<int>）+ `MartialInfo`（是否战士 / 圣武士 / 武僧、额外攻击档位、VPE 等级与加成）。只在 `MapComponent_ClassAuras.Refresh` 原有的那次遍历里（每 250 刻）更新，另在 `Hediff_PsycastAbilities.UnlockPath` 后缀里立即刷新这个人。读档时 `MartialState.FinalizeInit` 清空，250 刻内重建。
- **斗志**：`ClassDamageMods`（`Pawn.PreApplyDamage` 后缀）里 `Martial.Is(instigator)` 后 +0.4%；`ClassKillHooks` 击杀 +3%；新 GameComponent `MartialState`（`furyGiven` + `furyHour`，按 `TicksGame/2500` 分桶）每人每小时封顶 15%；需要 `HasPsylink`；召唤物、友军、囚犯、倒地目标（命中时）不算。
- **VPE 等级加成**：`Classes.Power` 两个重载对格斗、射击加 `Martial.VpeBonus` = min(0.4, 0.02×(等级−1))，值从缓存取；`StatPart_ClassSkill` 说明多一行“VPE 灵能等级 N：+x%”。
- **额外攻击**：状态 `LP_ExtraAttack`（3 档，`MeleeCooldownFactor` + 原版 `RangedCooldownFactor`），250 刻刷新里按档位给 / 改严重度 / 移除；只有武僧路径的人只在 `Monk.Unarmed` 时生效；冒险者关或没有冒险者状态时看 VPE 等级。追击在 `ClassMeleeHit` 里（`inFollowUp` 防递归，副手那一刀不追击），飘字“追击”。
- **登记**：`Martial.Register()`（VPEBridge 静态构造里调）登记 `Styles.SlotsHook`、`ITab_Adventurer.ClassLinesHook`、`ITab_Adventurer.LevelUpLinesHook`、`Feats.AimHook`。

### 战斗风格（主程序集 `Source/AdventurerStyles.cs` + `Defs/Adventurer_Styles.xml`）
- `LP_FightingStyleDef`（label、description、iconPath、order、enemyTags、workerClass）× 10，图标 `Textures/LP/Adventurer/Styles/`。
- `Hediff_Adventurer` 新增存档字段 `styles`、`styleReadyTick`、`styleAdaptSlot`、`styleRolled`；不存档的 `styleMask`（生效风格位掩码）。冒险者状态原有的 250 刻钩里调 `Styles.Refresh(h)`：重算掩码、`Styles.Users`（HashSet<int>）、`protectors` / `interceptors` / `blindFighters`（HashSet<Pawn>），按装备给 / 移除 `LP_StyleDefense`、`LP_StyleDueling`、`LP_StyleGreatWeapon`、`LP_StyleUnarmed`。
- 槽位：`SlotsHook`（桥）——有物理路径 1 个，战士且冒险者 ≥ 10（冒险者关时 VPE ≥ 10）2 个。第一次选立即生效；改选后 `styleReadyTick = now + 180000`、`styleAdaptSlot = 该槽`，适应期间这一槽无效、不能再改选。
- 敌人：`AdventurerEnemies` 的 `LevelGroup` / `LevelLoose` / `Story` 定级后 `Styles.EnemyRoll`：等级 ≥ 3、50%、按 `Feats.EnemyRoles` 的 melee / ranged 随机 1 个（每人只掷一次）。
- 效果挂钩：箭术、投掷 → `FeatRange`（射程）和 `FeatSharpshooterCover`（`HitReportFor`，乘 `factorFromShooterAndDist`）；守护 → `AdventurerMeleeHit`（近战命中率）与 `HitReportFor`；拦截 → `FeatDamage`（`PreApplyDamage` 前缀）；盲斗 → `HitReportFor` 把 `factorFromCoveringGas` 设 1 + 新挂钩 `InvisibilityUtility.IsPsychologicallyInvisible`；徒手擒抱 → 新挂钩 `StyleMeleeHit`；双武器 → `Marilith.OffhandSlots`（至少 1）/ `OffhandUser`（“拿作副手”菜单对有风格的人也显示）/ `MarilithOffhand.FollowUp`（只有风格的人 30%，蛇人 / 双头巨人基因副手仍 100%）。

### 冒险者分页“职业特性”（`Source/AdventurerUI.cs`）
- 位置：可选专长列表之后、史诗恩赐列表之前。没装 VPE（`ClassLinesHook` 为空）且不是带风格的敌人时整节不画；没有物理路径时灰色占位“职业特性：解锁战士、圣武士或武僧路径后出现”。
- 桥给 3 行（额外攻击档位 / 斗志本小时计数 / VPE 等级加成，各带悬停说明，格式“文字|说明”）；战斗风格行主程序集自己画：① ② 两格，图标、名字、适应中剩余天数、“改选 / 收起”按钮；空槽或点了改选的槽下面列出 10 个风格（“选择 → 确定？”两次点击，复用热修 2 的 `Confirm`）。
- 升级信件：`GameComponent_Adventurer.LevelGains` 逐级调 `LevelUpLinesHook`（5 级第 1 档、10 级战士多一个风格槽、11 / 20 级第 2 / 3 档）。

### 战士补完（`Abilities_Fighter.xml`、`MartialAbilities.cs`、`ClassMartial.xml`）
- `LP_Path_Fighter`：`alternateSkill` Shooting、`martial` true，描述 / lockedReason 改为“格斗或射击 6 级”。
- `LP_ClassAbilityExt.bestSkill` + `SkillFor(pawn)`：回气、战吼看格斗、射击较高的一项（门槛与强度）；`Requirement` 文字写“需要格斗或射击 6 级”。
- `AdventurerVPE.SkillAbility`：射击 → 敏捷；`Role`：战士射击 > 格斗时 `"ranged"`（`Legend.AutoSpend` 已有 ranged 类）。
- 新属性 `LP_MarksmanPower`（射术强度，Stats.xml，displayPriority 4213）；战技强度说明补了 bestSkill 和 VPE 加成。
- 冷却：动作如潮 15000（状态加 `RangedCooldownFactor` 0.6）、武器专精 2500（持续 5000 × 战技强度，远程伤害同档系数）、不屈意志 15000、守护姿态 5000、战意再临 60000；描述同步。
- 7 个新能力（环 / 技能 / order）：冲锋 1/格斗/3、缴械击 3/格斗/3（前置绊摔）、稳固瞄准 1/射击/4、压制射击 2/射击/3、精准射击 3/射击/4、连射 4/射击/3、死神之眼 5/射击/2。图标 `Bridges/VPE/Textures/LP/Classes/Fighter/`（`ArtSource/RaceArt/draw_martial_153.py`，沿用 draw_class_art.py 的工具与战士铁锈配色）。
- 新状态：`LP_ExtraAttack`、`LP_SteadyAim`、`LP_Suppressed`、`LP_RapidFire`、`LP_DeadEye`、`LP_Hobbled`、`LP_ChargeStrike`（`Hediff_ClassStrike`），另加看不见的 `LP_SuppressingVolley`；主程序集 `LP_StyleDefense/Dueling/GreatWeapon/Unarmed`、`LP_Grappled`。冲锋飞行 `LP_ChargeFlyer`（ParentName PawnFlyerBase，`RespawnPawn` 后落地一击）。
- `Martial.ShotFlags`（射击状态位）：`Hediff_MartialFlag`（稳固瞄准 / 被压制 / 死神之眼 / 连射 / 武器专精）在 PostAdd / PostRemoved 维护；`Feats.AimHook` = `Martial.Aim`：稳固瞄准 ×1.15、被压制 ×0.75、死神之眼 ≥ 100（必中 + 无视掩体）。
- `Martial.Shots`（Dictionary<Projectile,ShotMod>）：压制射击 ×0.5、精准射击 ×(1.5+0.5R)、穿甲 +0.3；精准射击模式（要害 / 打腿 ×0.7 + `LP_Hobbled` / 打手 ×0.5 + 缴械判定）存在 `MartialState.precisionMode`，征召时 `Pawn.GetGizmos` 后缀给切换按钮。
- `SurgeKillReset`：近战击杀 + 战意再临，或远程击杀（`Def.isRanged` 或武器是远程）+ 死神之眼 → 战士能力冷却清零。

## 二、和要求不一样的地方（都是核对 1.7 / VPE 后的必要调整或小细节）
1. **连射多一发**：原版 `burstShotsLeft` 在 `Verb.WarmupComplete` 里才设为 `ShotsPerBurst`，在 `TryStartCastOn` 后缀里加会被覆盖。改为 `Verb.WarmupComplete` 后缀：第一发打完、仍在连发（`state == Bursting`）时 `burstShotsLeft++`（`FieldRefAccess`，不改共享的 verbProps）。
2. **射击冷却**：1.7 有原版 `RangedCooldownFactor`（`AdjustedCooldown` 里确实乘它），直接写在状态里，没有 Harmony `AdjustedCooldown`。
3. **子弹移除时机**：`Bullet.Impact` 先调 `base.Impact` 再结算伤害，若在 `Projectile.Impact` 后缀里移除，子弹的伤害倍率会失效。所以子弹在 `Bullet.Impact` 后缀移除，其他投射物在 `Projectile.Impact` 后缀移除；另每 2500 刻清已销毁的。
4. **ShotReport 字段名**：烟雾是 `factorFromCoveringGas`；必中把 `factorFromShooterAndDist`、`factorFromEquipment`、`factorFromWeather`、`factorFromCoveringGas` 设 1，`factorFromTargetSize` ≥ 1、`offsetFromDarkness` ≥ 0，`coversOverallBlockChance` 设 0。
5. **VPE 灵能树一行最多 3 个**（`PsycastsUIUtility.abilityTreeXOffsets` 只有 1～3 个的偏移，第 4 个会越界）。战士第 1、3 环现在各 4 个，所以加了 `DoPathAbilities` 前缀：只对有 >3 个能力的 DND 路径接管，照原样画，间距按宽度收紧（≤ 42 px）。其他路径不受影响。
6. **压制射击的“每 6 刻一发”**：用一个看不见的状态 `LP_SuppressingVolley` 挂在施法者身上，在这个人自己的状态刻里打（不新开循环），6 发打完自己移除。
7. **ShotFlags 的键**用 `Pawn` 而不是 `int`（这样 2500 刻清理能判断已销毁 / 死亡）。读档后 `Martial.Reset` 清空，状态在下一次刻里按“代数”重新登记。
8. **盲斗的隐身**：`ShadowWalking` 是穿墙寻路，没有可见判断；接的是原版 `InvisibilityUtility.IsPsychologicallyInvisible`（3 格内有盲斗者时视为不隐身）。
9. **投掷**：判断为 weaponTags 以 `Grenade` 开头（GrenadeDestructive / EMP / Smoke / Flame）或“手雷”分类（本模组炼金投掷物都在这个分类）。矮人投掷锤是一个能力（`LP_HammerThrow`），不走武器射击，所以不吃投掷风格。
10. **战斗风格的改选**：多存了 `styleAdaptSlot`（哪一槽在适应）和 `styleRolled`（敌人是否已掷）；适应期间不能再改选（防止两个槽交替改选绕过 3 天）。
11. **精准射击·打手**：用缴械击的公式，但施法者一方取敏捷调整值，强度项用射术强度 R。
12. **追击**：除了普通近战，冲锋 / 旋风斩 / 疾风连击等走 `ClassMelee.Strike` 的能力结算也会按概率追击（同一个 `ApplyMeleeDamageToTarget` 挂钩）；副手那一刀不追击。
13. **斗志“造成 > 0 的伤害”**：在 `PreApplyDamage` 后缀判断（护甲之前）：没被吸收、拨挡后伤害仍 > 0 就算。
14. **钩子登记位置**：`ClassLinesHook`、`LevelUpLinesHook` 放在 `ITab_Adventurer` 上；登记在 `Martial.Register()`（由 VPEBridge 静态构造调用）而不是 `AdventurerVPE.Register()`。
15. **另外改了** `Bridges/VPE/Defs/Hediffs.xml`（动作如潮加射击冷却、武器专精换成 `Hediff_MartialFlag` 类）——要求的文件表里没列但必须改。旧存档里已有的武器专精状态（旧类）由 250 刻刷新补登记。
16. **测试路由**：以前 `Test.ps1 -VPE` 会忽略 `-Only`。现在 `-VPE -Only <组>` 只跑 VPE 桥里的 `<组>Checks / <组>PrepareSave / <组>AfterLoad`（`BridgeChecks.RunOnly / AfterLoadOnly`，主程序集 `VPEBridgeHook.cs` 转发）。全量 `-VPE` 的定义检查同步改成战士 16 个能力、总数 79、神经成型器 79、强度属性多 `LP_MarksmanPower`。
17. `LP_Grappled` 写的是 MoveSpeed ×0，原版 MoveSpeed 最低 0.15 格/秒，所以实际是“几乎不能动”。
18. 冲锋的“期间伤害 ×(1+0.3M)”只作用在落地那一下（`LP_ChargeStrike` 结算完立刻移除）。

## 三、新增 / 改动的 Harmony 挂钩与快速返回条件（给第 7 项性能检查）

| # | 挂钩 | 程序集 | 频率 | 快速返回条件（第一步） |
|---|---|---|---|---|
| 1 | `Verb_MeleeAttackDamage.ApplyMeleeDamageToTarget` 后缀 `StyleMeleeHit`（新） | 主 | 每一刀 | `Styles.Users.Count==0`；施法者不在 `Styles.Users` |
| 2 | `InvisibilityUtility.IsPsychologicallyInvisible` 后缀 `StyleBlindSight`（新） | 主 | 索敌时 | `!__result` 或 `Styles.blindFighters.Count==0` |
| 3 | `VerbProperties.AdjustedRange` 后缀 `FeatRange`（改） | 主 | 很频繁 | 新增部分：`Styles.Users.Count==0` 或攻击者不在 `Users` |
| 4 | `ShotReport.HitReportFor` 后缀 `FeatSharpshooterCover`（改） | 主 | 每发子弹 / 悬停 | 近战或非小人直接返回；`AimHook` 内 `ShotFlags.Count==0` 返回 1；风格部分 `Users` 查找；守护 `protectors.Count>0` |
| 5 | `Verb_MeleeAttack.GetNonMissChance` 后缀 `AdventurerMeleeHit`（改） | 主 | 每一刀 | 新增部分：`Styles.protectors.Count==0` |
| 6 | `Pawn.PreApplyDamage` 前缀 `FeatDamage`（改） | 主 | 每次受伤 | 新增部分：`Styles.interceptors.Count==0` |
| 7 | `Marilith.OffhandSlots / OffhandUser`（被 `Verb_MeleeAttack.TryCastShot` 前缀调用，改） | 主 | 每一刀 | 双武器部分：`Styles.Users.Contains` 失败即返回 |
| 8 | `Hediff_PsycastAbilities.UnlockPath` 后缀 `MartialUnlockPath`（新） | 桥 | 解锁路径时 | 不是 martial 的 `LP_ClassPathDef` |
| 9 | `Projectile.DamageAmount` getter 后缀 `MartialShotDamage`（新） | 桥 | 每发子弹 | `Martial.Shots.Count==0` |
| 10 | `Projectile.ArmorPenetration` getter 后缀 `MartialShotPenetration`（新） | 桥 | 每发子弹 | `Martial.Shots.Count==0` |
| 11 | `Bullet.Impact` 后缀 `MartialShotBulletImpact`（新） | 桥 | 每发子弹命中 | `Martial.Shots.Count==0` |
| 12 | `Projectile.Impact` 后缀 `MartialShotImpact`（新） | 桥 | 每个投射物命中 | `Martial.Shots.Count==0`（子弹跳过，由 #11 处理） |
| 13 | `Verb.WarmupComplete` 后缀 `MartialRapidFireBurst`（新） | 桥 | 每次开火 | `Martial.ShotFlags.Count==0`；不是 `Verb_LaunchProjectile` 或不在连发 |
| 14 | `Pawn.GetGizmos` 后缀 `MartialPrecisionGizmo`（新） | 桥 | 只在选中时（UI） | `!Martial.Is(p)` 或没征召 |
| 15 | `PsycastsUIUtility.DoPathAbilities` 前缀 `MartialWideTree`（新） | 桥 | 只在灵能页签打开时（UI） | 不是 `LP_ClassPathDef`，或没有 >3 个能力的行（按路径缓存） |
| 16 | `Verb_MeleeAttackDamage.ApplyMeleeDamageToTarget` 后缀 `ClassMeleeHit`（改） | 桥 | 每一刀 | 追击：`Martial.inFollowUp` 或 `!Martial.Is(caster)`（另加一个自检用 `Hits++`） |
| 17 | `Pawn.PreApplyDamage` 后缀 `ClassDamageMods`（改） | 桥 | 每次受伤 | 远程倍率：`ShotFlags.Count==0`；斗志：`!Martial.Is(instigator)` |
| 18 | `Pawn.Kill` 后缀 `ClassKillHooks`（改） | 桥 | 每次死亡 | 斗志：`!Martial.Is(killer)` |
| 19 | `Pawn.Kill` 后缀 `SurgeKillReset`（改） | 桥 | 每次死亡 | 原来就是一次 `Items.Has`；死神之眼部分：`Martial.Flags` 在 `ShotFlags.Count==0` 时直接返回 0 |

非 Harmony 的周期工作（都挂在已有循环上，没有新开每刻循环）：
- `MapComponent_ClassAuras.Refresh`（原有每 250 刻、原有那次遍历）：每个有 `CompAbilities` 的人形调 `Martial.Refresh(p)`（一次 VPE 状态查找 + 遍历已解锁路径；没有 martial 路径的人在第一次 Remove 后直接返回）。每 2500 刻 `Martial.Cleanup()`（Shots、ShotFlags 里已销毁 / 死亡的）。
- `Hediff_Adventurer.TickInterval`（原有每 250 刻钩）：`Styles.Refresh(h)`，没有风格、掩码为 0、不在 `Users` 的人直接返回。
- `Hediff_MartialFlag.TickInterval`：只是一次 int 比较（读档后重新登记射击状态位用），只存在于有射击状态的人身上，时长 6～72 秒。
- `Hediff_SuppressingVolley`：只在压制射击后存在约 36 刻。
- 热路径里没有 LINQ、没有遍历 hediff 列表、没有新增 `GetStatValue`。数值（额外攻击、风格、射击增益）都写成状态的属性系数，由原版属性缓存去算，没有新 StatPart（只有原有 `StatPart_ClassSkill` 的说明多一行）。

## 四、测试结果
- `pwsh Source/Build.ps1`：主程序集、HAR 桥、VPE 桥、神兽血裔桥全部编译通过。
- `pwsh Source/Test.ps1 -VPE -Only Martial`：**PASS 24 条**（1 条桥已加载 + spec 第七节 1～11 共 20 条 + 存档夹具 1 条 + 读档 2 条），日志无红字。
- `pwsh Source/Test.ps1 -Only Adventurer`：**PASS 123 条**。
- 全量 `-VPE`（spec 不要求，因为改了全量的定义检查顺手跑）：第一次在牧师检查“test ally bleeds before restoration”失败（夹具伤口没流血，和本版无关、不确定性的老检查），重跑 **PASS 482 条**（1.52.1 是 460；多出的 22 条是 7 个新能力各 3 条定义检查 + 射术强度属性 1 条）。

## 五、没在实机里验证的
- 冲锋的飞行与落地（`LP_ChargeFlyer.RespawnPawn` 后结算）——自检只检查了定义存在。
- 压制射击 6 发按刻发射、落点散布、被压制效果的实际观感。
- 连射在真实连发武器上的多一发（`WarmupComplete` 后缀）。
- 精准射击的模式按钮、打腿 / 打手实际效果。
- 灵能页签里战士树第 1、3 环 4 个能力的排版（`MartialWideTree`）。
- 冒险者分页“职业特性”小节的排版、风格选择列表、改选按钮。
- 守护、拦截、盲斗（隐身单位）、投掷、徒手擒抱在实战中的表现；敌人在袭击里随机到的风格；升级信件里的新行。
- 斗志在真实战斗中的回复节奏（自检用的是直接调用 `PreApplyDamage`）。
