# 1.73.0 职业体系第二版报告（法师 / 牧师 / 德鲁伊 / 术士 / 器械师）

规格：ArtSource/Classes-1.73.0-spec.md。备份：ArtSource/backup-1.72.0（Source、Defs、Bridges/VPE、About、使用说明.txt、Assemblies）。建立在 1.72.0（ArtSource/Classes-1.72.0-report.md）之上。

## 改动

### 主程序集
- **Defs/Classes2.xml（新）**：5 个 `LP_ClassDef`、5 个强度属性（奥术 / 野性 / 生长 / 天赋 / 器械，`StatPart_ClassPower`，天赋另挂 `StatPart_SorcererBloodline`）、`LP_ArcaneForce` / `LP_Lightning` 伤害、五个职业的全部状态（defName 不变，`VEF_MeleeAttackDamageFactor` → `MeleeDamageFactor`）、`LP_BlessedThought`、魔法飞弹子弹、石墙、符文炮台（含炮管与子弹）、冒险者手册与配方、45 个原版能力（defName 与旧 VEF 能力相同）、通用目标模板 `LP_ClassAllyBase` / `LP_ClassLocBase`。
- **Defs/Classes.xml**：三个物理职业加 `<primary>`。
- **Source/ClassCasters.cs（新）**：五个职业的效果（`ClassCasters.Apply / Invalid / AfterCast`）、`Dialog_ControlWeather`、`MagicMissileBullet`、`Hediff_WildShape`、`HediffComp_Conjured`、`HediffComp_GiveOnRemoved`、`Building_RuneTurret`、`Artificer`（制作门槛、魔力共鸣 / 大师工艺登记、说明文字、维修、过载）、`CompUseEffect_Manual`，Harmony 挂钩（见下）。
- **Source/Classes.cs**：`ClassResource.Mana`；`LP_ClassDef.primary / caster / bloodline`；主属性按 `primary` 取；术士血脉门槛 −2；巫妖禁德鲁伊；神恩共用池（`PoolKey` / `PoolLevel`）；法师读书回法力（`Studying`）；`ClassBonus`（施法职业等级加成）；`FreeCast`（魔涌）；`XpFactor`（职业经验加成 → 冒险者经验）；选职业窗口改成滚动区；提示信文字改成八职业。
- **Source/ClassAbilities.cs**：`CompProperties_ClassEffect` 加 `maxRange / sorcery / single / fixedSeverity`；强效 ×1.5；施放后调 `ClassCasters.AfterCast`；悬停写“不消耗资源”“魔涌中免费”。
- **Source/ClassMartial.cs**：`PortalEvent.WizardHook` 改看法师职业等级（原来在 VPE 桥）。
- **Source/Adventurer.cs**：`GainXP` 乘 `ClassSystem.XpFactor`。**Guns.cs**：「DND武器需要器械师」说明改成不需要 VPE。**VPEBridgeHook.cs**：桥加载检查改看 `LP_WanderingMentor` 事件。
- **Source/ClassChecks.cs**：`CheckClassesCasters`（9 条）。
- **Patches/AdventurersManual_Traders.xml**：奇物商人卖冒险者手册（从桥移来）。
- **Textures/LP/Classes/**：Wizard、Cleric、Druid、Sorcerer、Artificer 文件夹，Background_Wizard / Background_Cleric、Manual.png 从桥移来。

### VPE 桥
- **删除**：Abilities.cs、Classes2.cs、Druid.cs、Sorcerer.cs、Artificer.cs、Manual.cs、Classes2Checks.cs；Defs 的 Abilities_Wizard、Abilities_Cleric、ClassDruid、ClassSorcerer、ClassArtificer、ClassAbilities_Artificer、ClassThings_Artificer、Things、ClassRecipes、Damages、Hediffs、Stats。
- **新 Defs/ClassLegacy2.xml**：45 个旧 VEF 能力空壳 + 德鲁伊 / 术士 / 器械师路径（法师、牧师路径仍在 Paths.xml）。
- **VPEBridge.cs** 精简：只剩 `Classes`（常量与转到主程序集的强度）、`LP_ClassPathDef`（`CanPawnUnlock` 恒 false）、`LP_ClassAbilityExt`（只读 XML）、提尔审判之庭、旧神经成型器拒用。删掉技能门槛补丁、`StatPart_ClassSkill`、VPE 路径经验（`ClassExperience` / `ClassState` 变空壳）。
- **Martial.cs**：迁移改按版本号（`lpClassMigratedVersion`，Version 2），1.72.0 迁过的存档再跑一遍；八条都隐藏后 `DND职业` 页签整个拿掉；旧类名空壳增加 `Hediff_ClassStrike`、`Hediff_ClassScaled`、`Hediff_WildShape`、`MagicMissileBullet`、`Building_RuneTurret`、`MapComponent_ClassAuras`、`ClassState`。
- **Mentor.cs**：八个职业的导师都按职业等级（不再有 VPE 路径学生）。**HeroTraitsVPE.cs**：魔法学徒不再学 VPE 路径第一个能力（主程序集给魔能飞弹）。**AdventurerVPE.cs / PerfChecks.cs**：`Artificer.PathName` → `Classes.ArtificerPath`。
- 检查：BridgeChecks.cs 精简成基础设施 + 还有效的组；AdventurerVPEChecks.cs 重写（法术狙击期望改 ×1.75）；MentorChecks.cs、NonlethalVPEChecks.cs、MartialChecks.cs 改成职业等级。

## 能力 → 职业等级

| 等级 | 法师 | 牧师 | 德鲁伊 | 术士 | 器械师 |
|---|---|---|---|---|---|
| 1 | 魔法飞弹、法师护甲 | 治疗伤口、祝福 | 安抚野兽、纠缠术 | 混沌箭、燃烧之手 | 奥术工坊（被动）、快速维修 |
| 3 | 火球术 | 次级复原术 | 召唤野兽 | 魅惑人类 | 过载射击 |
| 5 | 睡眠术 | 神导术 | 树肤术 | 超魔·孪生 | 符文铭刻（被动） |
| 9 | 闪电束、急速术 | 神圣打击、驱散亡灵 | 荒野变形、植物滋长 | 龙族之翼、冰风暴 | 魔力充能、大师工艺（被动） |
| 13 | 石墙术 | 群体治疗 | 兽王号令 | 群体暗示 | 山岳锻造（被动） |
| 17 | 传送术 | 死亡守卫 | 召雷术 | 超魔·强效 | 符文炮台 |
| 20 | 解离术（智识 18） | 复生术（社交 18） | 掌控天气（种植 18） | 魔涌（社交 18） | 魔力共鸣（被动，手工 18） |

原则：旧 VPE 的第 1 环两项放 1 级，第 2 环分到 3 / 5，第 3 环两项放 9，第 4 环分到 13 / 17，终极 20。器械师按规格让符文铭刻落在 5 级（工坊 1、铭刻 5）。

## 资源数值

- 法力（法师 / 术士 / 德鲁伊，每个职业一条）：上限 2 + 职业等级/2；睡觉时每 250 刻 +0.5；法师研究 / 读书 / 在卷轴书桌或研究台干活时每 250 刻 +0.1。冥想不回法力。
- 神恩（牧师）：与圣武士同一条，上限 = 2 + （牧师 + 圣武士等级，最多按 20）/2；祈祷 / 冥想 +0.2（信徒 ×1.25），睡觉 +0.5。
- 器械师：没有资源，能力只看冷却（被动 0，主动 0）。
- 消耗：
  - 法师：魔法飞弹 1、法师护甲 1、火球术 2、睡眠术 1、闪电束 2、急速术 1、石墙术 2、传送术 1、解离术 3。
  - 牧师：治疗伤口 1、祝福 1、次级复原术 1、神导术 1、神圣打击 1、驱散亡灵 2、群体治疗 2、死亡守卫 2、复生术 3。
  - 德鲁伊：安抚野兽 1、纠缠术 1、召唤野兽 2、树肤术 1、荒野变形 2、植物滋长 2、兽王号令 2、召雷术 2、掌控天气 3。
  - 术士：混沌箭 1、燃烧之手 1、魅惑人类 1、超魔·孪生 1、龙族之翼 1、冰风暴 2、群体暗示 2、超魔·强效 1、魔涌 3（魔涌期间术士法术免费、冷却 ×0.25）。
- 冷却全部照搬 VEF 的 `cooldownTime`；召雷术下雨时冷却减半、魔涌冷却 ×0.25 都在施放后缩放（不重复调 `StartCooldown`，避免和耐瑟瑞尔冷却系数叠两次）。

## 决定

1. 神恩共用一条（规格允许）：存在圣武士名下，上限按牧师 + 圣武士等级之和（最多按 20 级算，所以最多 12）；新学其中一个职业时这条回满。法力不共用：法师、术士、德鲁伊各一条（三个职业玩法差别大，分开更好懂）。
2. 施法职业强度加成：用这门技能当主或副技能的职业等级合计，每比 1 高 1 级 +2%，最多 +40%（社交会同时算牧师、术士、圣武士；智识算法师、术士）。格斗 / 射击仍按 1.72.0 的物理职业合计。
3. 主属性门槛（13）沿用：法师智力、牧师感知、德鲁伊感知、术士魅力、器械师智力（和旧路径的 lockedReason 一致）。
4. 器械师门槛：奥术工坊档 = 器械师 1 级（或会这个能力）、符文铭刻档 = 5 级（或会这个能力）、山岳锻造 = 会这个能力（13 级或迁移保留）。大师工艺、魔力共鸣也看“会不会这个能力”。
5. 器械师所有能力不消耗资源，过载射击等照旧冷却。
6. 复生术：去掉“精神力接近满”的要求，改为消耗 3 神恩（终极统一 3）。
7. 掌控天气：原来的浮动菜单换成窗口（Peter 的游戏里浮动菜单选不了）。
8. 冒险者手册移到主模组（不装 VPE 也有）：只给职业门槛豁免 + 训练经验，不再解锁 VPE 路径 / 给心灵链接；条件：没满 3 个职业、手里没有没用掉的豁免。奇物商人照旧卖。
9. 职业经验加成（侍僧→圣武士 / 牧师、隐士→武僧、贤者→法师、神之勇士、职业天赋）改成冒险者经验 ×，取所学职业里最高的一项（不叠乘），没有职业的人不受影响。
10. 魔法学徒专长：装 VPE 时也一律给魔能飞弹（VPE 路径都没了）。
11. 云游导师仍然只在装了 VPE 时出现（事件定义在桥里）；八个职业的学生都按职业等级、上课给冒险者经验。移到主程序集留给下一阶段。
12. 旧神经成型器：VPE 在定义加载阶段生成，桥里的补丁来不及拦，所以没有停掉生成；继续拒绝使用（79 种）。
13. 迁移改版本号（Version 2）：1.72.0 跑过的存档也会再跑一遍；`Migrate` 只处理还剩下的旧能力 / 路径，已迁过的人不受影响。
14. 魔网之子（施法不进冷却）和仪式余韵对新能力也生效（原来只挂在 VEF 能力上）：在 `AfterCast` 里处理；魔网之子只对消耗资源的能力触发。
15. 选职业窗口加滚动条、加高到 620。

## Harmony 挂钩

主程序集新增：
- `RecipeDef.PawnSatisfiesSkillRequirements` 后缀 `ArtificerRecipeGate`（器械师制作门槛）。
- `Thing.TakeDamage` 前缀 `ArtificerOverchargeHit`（过载射击；快速返回：伤害来源不是小人）。
- `Pawn_HealthTracker.CheckForStateChange` 前缀 `ClassDeathWard`（死亡守卫）。
- `Pawn.Kill` 后缀 `ConjuredNoCorpse`（召唤物不留尸体）。
- 不是挂钩但改了行为：`Arcana.SensitivityFloorHooks` / `QualityBonusHooks` 登记（魔力共鸣、大师工艺）、`PortalEvent.WizardHook`。

VPE 桥：`HideMovedPaths`（页签构造函数后缀）现在也删空页签；删掉 `PrereqsCompleted_Comp_Patch`、`PrereqsCompleted_Pawn_Patch`、`ClassExperience`（Pawn_SkillTracker.Learn）、`SurgeFreePsyfocus`、`ArtificerRecipeGate`、`OverchargeHit`、`DeathWardTrigger`、`ClassKillHooks`；`Psytrainer_CanBeUsedBy_Patch` 只剩拒用旧神经成型器。

## 测试（只跑指定组）

| 命令 | 结果 |
|---|---|
| `Test.ps1 -Only Classes` | PASS 21（1.72.0 的 12 条 + 新增 9 条） |
| `Test.ps1 -VPE -Only Classes,Martial,Adventurer,Nonlethal` | PASS 22（迁移两遍 + 存读档、八条隐藏 + 空页签、物理被动、导师、法术狙击 ×1.75、器械师制伏武器门槛） |
| `Test.ps1 -Only Adventurer,Guns,Faith` | PASS 409 |

没跑完整套件。器械师门槛没有专门的 Guns 组检查，放在 `-Only Classes` 第 4 条（不装 VPE）和 `-VPE -Only Nonlethal`。

## 没测到 / 需要在游戏里看

- 在游戏里真正施放这 45 个能力（目标框、预热）：只测了治疗伤口、强效 / 魔涌、门槛和资源；火球、闪电束、石墙、传送、解离、召唤狼、熊形、召雷、天气窗口、混沌箭、龙翼、冰风暴、符文炮台都是原样搬的代码，没在游戏里点过。
- 法师读书回法力（按 job 判断：研究、Reading、卷轴书桌、研究台）。
- 真的旧存档（1.71.0 和 1.72.0 两种都只用假数据测了）。
- “职业”页 8 个职业的能力树长度、选职业窗口滚动。
- 完整 `-VPE` 套件：Run() 现在只跑 Martial / Classes / 导师 / 秘术残卷 / 冒险者，没有跑。
- 旧的 VPE 神经成型器仍会出现在交易 / 掉落里（能看不能用）。
