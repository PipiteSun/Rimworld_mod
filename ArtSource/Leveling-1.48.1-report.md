# 1.48.1 冒险者等级第二阶段（敌人等级 + 12 专长）实现报告

依据：`ArtSource/Leveling-1.48.1-spec.md`（权威，协调给的原文）、`Leveling-1.48.0-design.md` 第六、八节。备份 `ArtSource/backup-1.48.0`（1.7、About、Bridges、Defs、Patches、Source、Languages、使用说明.txt、LoadFolders.xml）。

## 一、加了什么

### 主模组
| 文件 | 内容 |
|---|---|
| `Source/AdventurerEnemies.cs`（新） | `LP_AdventurerKindExt`（levelBonus、primary、secondary、role）；`AdventurerEnemies`：等级公式、领队/精英加成、殖民地上限、战斗力折算裁人、`LevelGroup`/`LevelLoose`/`Story`；`PawnGroupMakerUtility.GeneratePawns` 最外层后缀（Priority.Last） |
| `Source/AdventurerFeats.cs`（新） | `LP_FeatDef`、`FeatWorker`（+ 战争施法者/法术狙击/鼓舞领袖子类）、`Feats` 静态逻辑、`Hediff_FeatInspired`、鼓舞能力的 comp，以及所有专长的 Harmony 挂钩 |
| `Source/AdventurerChecks2.cs`（新） | `-Only Adventurer` 第二阶段 27 条检查（由 `CheckAdventurer` 调用） |
| `Source/Adventurer.cs` | 状态新增 feats / featUsed / featDay / storyLevel（存档）；`RaiseTo`（敌人直接升级、沿途自动加点）、专长 API（TryUse、UsesLeft、CanTakeFeat、TakeFeat、TryEnemyFeat）；`BuildStage` 合进专长 stat；`BaseArray` 用兵种模板；自动加点：职业主属性 → 兵种主属性 → 最高项，敌人每次 50% 换专长；生成后缀里非敌对外人 1～3 级；征召记录（警觉）；哨兵名单每 250 刻刷新；设置 4 项 |
| `Source/AdventurerUI.cs` | 分页列出专长图标、名字、今天剩余次数（悬停看说明和前置）、“剧情等级”；加点窗口新增“属性提升 / 改选专长”两个标签页；分页高度 560 |
| `Source/AdventurerChecks.cs` | 调用第二阶段检查；存读档夹具加幸运 + 警觉、幸运用掉 2 次 |
| `Defs/Adventurer_Feats.xml`（新） | 12 个 `LichPhylactery.LP_FeatDef`、3 个状态（LP_FeatToughResolve、LP_FeatSentinelSlow、LP_FeatInspired）、能力 LP_FeatInspire |
| `Patches/Adventurer.xml`（新） | 给 27 个兵种加 `LP_AdventurerKindExt` |
| `Textures/LP/Feats/*.png`（新，12 张 128px） | `ArtSource/RaceArt/draw_feats.py` 生成（复用 draw_class_art.py 的描边/平涂/右下暗部和调色板） |
| 剧情代码 | `Twilight.MakeVessel`（神明附体 15）、`NetherilQuest`（拉洛克 18、半巫妖主人 14、营地精英法师 13、使者 12、古墓半巫妖 12）、`TowerCrash`（法师塔主人 13）、`AscensionPaths`（凯兰沃的审判者 16）；`Ascension.KindPawns`（末日卫士波次按点数定级）、`PortalEvent`（塞伊的人按战斗力）、`Stampede`（补的炎魔按战斗力） |

### VPE 桥
| 文件 | 内容 |
|---|---|
| `Bridges/VPE/Source/AdventurerVPE.cs` | 登记 `Feats.HasClassHook`（有 DND 职业路径）；`VEF.Abilities.Ability.GetRangeForPawn` 后缀（法术狙击 ×1.5） |
| `Bridges/VPE/Source/AdventurerVPEChecks.cs` | 5 条：战争施法者前置、牧师路径后可选两个法系专长、法术狙击射程 12 → 18、VEF 施法工作受保护、施法中受伤工作不变 |

其他：About.xml 1.48.1 + 描述末尾一句；使用说明.txt 标题 1.48.1、第二部分【1.48.1 敌人等级与专长】、1.48.0 节两处过时句加注、更新日志 1.48.1 条目。

## 二、我自己定的细节（决定）

1. **兵种对应**：模组里没有叫“塞伊红袍大法师 / 火巨人督军 / 吉斯洋基队长”的兵种，按最接近的取：红袍法师 `LP_FH_RedWizard` +5、火巨人领队 `LP_EC_FireGiant` +4、吉斯洋基雇佣兵 `LP_FH_GithMercenary` +3。其他头目：炎魔 +5、凯兰沃的审判者 +5、拉洛克（兵种）+5、夺心魔 +4、拉洛克的使者 +4、半巫妖 +4、寒冰魔 +3、被奴役的风暴巨人 +3、同盟骑士 +3、符印巨魔/火巨人铁匠/霜巨人掠夺者/圣武士/末日卫士 +2。只有属性模板（不加等级）的：牧师、教会牧师（感/体）；弓手、精灵游侠、鸟人斥候（敏/感）；兽人战士、牛头人、食人魔打手、矮人护卫、象人卫士（力/体）。
2. **领队与精英不叠加**：加成 = max(领队 +3, 兵种 levelBonus)。“领队” = 兵种 `factionLeader` 或派系首领本人。
3. **公式取整**：基础等级 = 1 + floor(每人点数 / 45)；浮动 ±2 均匀随机；加成后再限 1～上限（上限 = 殖民地最高等级 + N，N 默认 3，关掉时 20）。殖民地最高等级看所有地图、商队里的自由殖民者，没有时按 1。
4. **只升不降**：`RaiseTo` 只往上升（生成时已经是 1 级）；剧情等级打 `storyLevel` 标记，袭击公式不再改它，也不受上限。剧情等级同样受“敌人冒险者等级”开关控制。
5. **组的分类**：Combat、Settlement（含 RangedOnly）且派系敌对 → 公式；其余组（商队、访客）→ 1～3 级。另外在 `GeneratePawn` 后缀里，任何非玩家、非敌对派系的人一生成就 1～3 级（覆盖访客、难民、世界生成时的派系成员）。没有派系的人保持 1 级。
6. **人形计数**：只算组里有冒险者状态的人（13 岁以上人形、非死灵造物/召唤物）；动物、机械体、骷髅等不分点数，也不删。
7. **专长存储**：`feats`（Def 列表）+ `featUsed`（defName → 今天用掉几次）+ `featDay`（本地日序号）。次数按 pawn 所在地图经度换算本地时间，读到时发现换日就清零（懒刷新，不用每刻检查）。
8. **专长效果不乘“属性效果强度”**，Isekai 共存时照样生效（设计第十节“只保留等级和专长”）；总开关关掉时专长全部失效。
9. **敌人选专长**：每次属性提升 50% 尝试；候选 = 前置满足、`enemyTags` 含 any 或与兵种类别相交。类别：兵种扩展的 role；主属性是智/感/魅或有法系（DND 路径/圣徽）→ caster；拿远程武器 → ranged，否则 melee。鼓舞领袖没有 enemyTags，敌人不选（AI 不会用主动能力）。没有候选时照常加属性。
10. **12 个专长的具体做法**：
    - 警觉：原版没有通用的“被突袭眩晕”，按原版的两个偷袭机制处理——`Verb.TryStartCastOn` 里目标有警觉时 surpriseAttack 置否（偷袭原本必中、不能闪避），并屏蔽 `stunTargetOnCastStart` 的出手眩晕；预热减半挂在 `Stance_Warmup` 构造函数，只对有武器来源的非近战 verb，征召后 120 刻内。
    - 坚韧：`Pawn_HealthTracker.MakeDowned` 后缀；流血伤口按 0.3～0.5 品质包扎（止血），加 1 小时 painFactor 0.5 的状态；每天 1 次（usesPerDay=1）。
    - 幸运：近战在 `GetNonMissChance` 后缀里由我们掷骰（先掷；没中且攻方幸运重掷；中了且守方幸运重掷），把结果以 0/1 交给原版；远程在 `Projectile.Launch` 前缀里改落点（打空→改为命中目标；命中→改成目标附近 1～8 格的随机格）。重掷用的概率：近战是原版命中率；远程是 `ShotReport` 的瞄准命中率（不含掩体）。命中率为 0 或 1 时不消耗次数。
    - 巨武器大师：双手 = 主手近战武器质量 ≥ 2.5 千克。`Verb_MeleeAttack.TryCastShot` 前后缀判断“这一下把站着的目标打倒/打死”；这一轮攻击结束（`TryCastNextBurstShot` 后缀）时清掉冷却姿态，并对身边第一个站着的敌人 `TryMeleeAttack`；冷却 5 秒（300 刻，不存档）。
    - 神射手：`VerbProperties.AdjustedRange` 后缀（只对有武器来源的远程 verb）×1.25；`ShotReport.HitReportFor` 后缀把 coversOverallBlockChance 减半。
    - 哨兵：`Pawn_PathFollower.TryEnterNextPathCell` 前后缀，移动者从哨兵相邻格走到不相邻格时触发；哨兵按格斗命中率掷一次（目标不闪避），命中调用原版 `ApplyMeleeDamageToTarget`，再加 2 秒移速 ×0.5；每个哨兵每秒最多一次；不要求征召，但要清醒、没被眩晕、没有非攻击性的精神状态、能使用暴力。哨兵名单每 250 刻刷新，名单为空时移动挂钩直接返回。
    - 战争施法者：前置 = VPE 桥登记的“有 DND 职业路径”或佩戴圣徽。效果 = 当前工作是施法（`Job.ability` 不为空，或工作驱动类名以 JobDriver_CastAbility 开头，含 VEF 的 JobDriver_CastAbilityOnce）时，`Pawn_JobTracker.Notify_DamageTaken` 和 `StunHandler.Notify_DamageApplied` 直接跳过（不中断工作、不被伤害眩晕）。
    - 治疗者：`TendUtility.CalculateBaseTendQuality` 后缀 ×1.15（不超过药品品质上限，原本就超过的不再加）；`DoTend` 后缀给伤员最小的一处非永久伤 Heal(2)。
    - 重甲大师：重甲 = 躯干上的衣物锐器护甲 ≥ 80%（防弹背心 100% 就算）；`Pawn.PreApplyDamage` 前缀，伤害的 armorCategory 是 Sharp 或 Blunt 时 −2（在护甲之前），减到 0 就算吸收。
    - 灵活：Def 里 statOffsets MoveSpeed +0.2；哨兵不对灵活的人触发。
    - 鼓舞领袖：原版 AbilityDef `LP_FeatInspire`（自身施放、1 秒预热），获得专长时加能力、失去时去掉；能力按钮在今天次数用完时灰掉。6 格内、同派系或盟友/中立派系的人形（含自己）得 `LP_FeatInspired`，Severity = 吸收量 = max(1, 10 + 3 × 魅力调整值)，1 天消失；吸收在 `Pawn.PreApplyDamage` 前缀（同神圣庇护的写法）。
    - 法术狙击：神术 = `Verb_CastAbility` 且能力是 `Ability_Miracle`，走 `AdjustedRange` ×1.5；VPE 侧挂 `VEF.Abilities.Ability.GetRangeForPawn`，对所有 VPE 心灵法术（`def.Psycast()!=null`，含 DND 职业能力）×1.5（在 maxRange 截断之后乘）。前置同战争施法者。
11. **前置**：12 个专长都没有等级门槛（只能在 4 级以后的属性提升里换，所以 minLevel 字段保留但都是 1）。技能/属性门槛按规格。
12. **设置**：“敌人冒险者等级”（平时存 `adventurerEnemyLevels` 默认开；Isekai 共存模式下存 `adventurerEnemyLevelsCoexist` 默认关，界面上同一个勾选框，标题后注明“Isekai 共存中”）；“敌人等级上限：殖民地最高等级 + N”（勾选 + 0～10 滑条）。

## 三、和规格的偏差

- **战斗力折算的做法（规格 6，近似）**：没有改 `PawnGroupMakerUtility` 的挑人成本，而是在袭击组生成完之后按差额删人：目标 = 组里原来的总战斗力（Σ combatPower）；每个可删人形的成本 = combatPower × (1 + 0.03 × (预计等级 − 1))，预计等级 = 不带浮动的公式等级（含加成和上限）；只要总成本超过目标，就删掉最便宜的普通人形，直到再删会离目标更远。不删领队、精英（levelBonus > 0）、神明附体者、商人，至少留 1 个人形。删人后再按“总点数 ÷ 剩下的人形数”定等级，所以剩下的人等级略高（人少但精）。只对 Combat 组裁人；据点守军（Settlement）只定等级不裁。原版袭击的每人点数大约等于平均战斗力，所以普通袭击（2～4 级）通常只删 0～2 人；精英多、上限高时删得多。被删的人直接销毁、从世界角色池移除。
- **兵种名（规格 3）**：见二.1，用最接近的现有兵种，没有新建兵种。
- **领队 +3 与精英加成取较大值**（规格没说叠不叠加），见二.2。
- **警觉“惊讶眩晕”（规格 二.1）**：按原版的偷袭机制解释（见二.10）；原版之外的“伏击时全员眩晕”并不存在，所以没有更多可挡的。
- **幸运远程重掷**只用瞄准命中率，不重算掩体。
- **剧情敌人（规格 7）**：加了拉洛克、半巫妖、使者、红袍营地精英法师、法师塔主人、凯兰沃的审判者、神明附体首领；拉洛克的使者 12 级（不是首领，但属于剧情敌人，取下限）。没有加：宿敌冠军（模组里没有这个角色）、龙过境的龙、巨人迁徙（非敌对）、神明召来的帮手。
- **DND 事件覆盖（规格 1）**：走袭击组的事件（法师塔、红袍营地、古墓、信仰事件、成神之路的袭击、所有派系后处理加的人）自动覆盖；直接生成的只补了末日卫士波次、裂口的塞伊人、补出来的炎魔。其他直接生成的敌对人形保持 1 级。
- **难民（规格 1）**：只有“属于非敌对派系”的难民会是 1～3 级；没有派系的人保持 1 级。
- **法术狙击（规格 12）**：VPE 侧对所有 VPE 心灵法术生效，不只 DND 职业能力。

## 四、测试

构建：`pwsh Source\Build.ps1` 四个程序集都通过（游戏没在跑，DLL 没被占用）。

| 命令 | 结果 |
|---|---|
| `Test.ps1 -Only Adventurer` | **PASS 62 条**（原 35 + 新 27，含存读档）。第一次跑时日志里有一条原版红字（世界生成时 `SpouseRelationUtility.DoDivorce` 记录传说调用 TicksAbs，与本模组无关）；顺手把专长本地日改用 `GenTicks.TicksAbs`，之后两次都干净通过 |
| `Test.ps1 -VPE` | **PASS 457 条**（1.48.0 为 452，新增 5 条） |
| `Test.ps1 -Only Ascension` | PASS 15 条（改了 `Ascension.KindPawns`，回归） |
| `Test.ps1 -Only Factions,Ascension` | FAIL（两次都是）“combat group generates for LP_FaerunHostile … LP_WinterWolf”：霜巨人袭击带的冬狼不是人形，属于记录过的旧随机失败（1.44.0 就有）。自测进程里其他检查组生成的小人没有冒险者状态，新挂钩对它们不做任何事 |
| `Test.ps1 -Only Ascension,Netheril` | FAIL 在旧的“netheril 22”（1.42.0 起已知的失败），Ascension 部分通过 |

新检查对应规格第三节：点数换算（1000 点/10 人 → 基础 3 级，300 次随机落在 1～5）；领队/精英加成（红袍法师 +5 → 8 级，上限 4 时 4 级，5000 点封顶 20）与上限 = 殖民地最高 + 3；同 4000 点 20 个雇佣枪手：1 级不删、5 级删到 18 人；真实 2000 点袭击组每个人形都有等级且不超上限；访客 1～3 级；剧情 15 级不受上限并带标记；红袍法师模板（基础数组 智 15/体 14，自动加到 16 级后智力最高）；专长前置（巨武器大师格斗 8、重甲大师力量 13）与改选消耗、不能重复；幸运 3 次用完后不能再用、午夜重置；巨武器大师（需要双手武器、触发后追打身边的敌人、5 秒冷却）；重甲大师（防弹背心下锐器/钝器 10 → 8，烧伤不变，没重甲不减）；神射手射程 30.9 → 38.6；警觉预热 100 → 50；坚韧止血 + 状态、一天一次；治疗者品质 0.204 → 0.235、额外治 2 点；鼓舞领袖护盾 19（吸收 5 后剩 14）、一天一次；存读档专长与今天剩余次数不变；共存模式下敌人等级默认关。`-VPE`：战争施法者需要职业路径、VEF 施法工作（VFEA_UseAbility）受保护、受伤后工作不变；法术狙击 12 → 18。

## 五、没有在实际游戏里试过的

- 分页（专长图标那一段）和加点窗口“改选专长”标签页的排版、滚动；分页高度从 410 改到 560 后在小分辨率下的样子。
- 真实袭击里的裁人效果和人数变化体感；据点守军的等级；黄昏之年神明附体者实际出现时的 15 级。
- 哨兵在真实移动中的触发频率和性能（挂在每一步寻路上，名单为空时直接返回）；哨兵对逃跑的袭击者是否太强。
- 幸运远程改落点、巨武器大师在真实近战里的连击（检查里直接调用了触发和追击函数，没有完整打一架）。
- 警觉对动物/特殊敌人偷袭的实际效果；战争施法者在原版神术施法时被打的情况（检查只覆盖了 VEF 施法工作）。
- “鼓舞”能力的按钮、施放特效、AI 不会用。
- 午夜重置在不同经度地图、商队里的表现（商队里按经度 0 算）。
- 旧存档读档：已有的敌人、访客不会补等级（只对新生成的人生效）；1.48.0 存档里的冒险者状态读进来专长为空。
