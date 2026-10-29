# 1.50.0 传奇等级与传奇属性点（冒险者等级第四阶段）实现报告

依据：`ArtSource/Leveling-1.50.0-spec.md`（协调给的原文）。备份 `ArtSource/backup-1.49.0`（1.7、About、Bridges、Defs、Languages、Patches、Source、使用说明.txt、LoadFolders.xml）。没有改 IsekaiKillMilestones 模组。

## 一、加了什么

| 文件 | 内容 |
|---|---|
| `Source/AdventurerLegend.cs`（新） | `Legend` 静态类：传奇经验表（`XpFor`/`Step`）、开关（`On`、`EnemyOn`、`EnemyOnFor`、`AutoFor`）、八项效果（`AddStats`、`HealthFactor`、`RangedFactor`、`DamageTaken`、`EffectText`、`Summary`）、获得与花费（`Grant`、`Became`、`AutoSpend`、`Prefs`、`RoleHook`、`Respec`）、关掉开关时缩伤口（`ApplySettings`）、敌人（`PointsFor20`、`ExtraLevels`、`EnemyCap`、`LeaderLevel`、`StoryPoints`、`StoryLevel`、`RaiseEnemy`）、重铸之石（`MakeStone`、`CannotRespec`）；`CompUseEffect_LegendRespec`；Harmony `Pawn.PreApplyDamage` 前缀 `LegendToughness`（伤害承受）；设置字段与 `LegendSettings.Draw`（画在“冒险者”分组末尾） |
| `Source/Adventurer.cs` | `Hediff_Adventurer` 新字段 `legendPoints`、`legendSpent[8]`（存档 `lpLegendPoints`、`lpLegendSpent`）；`LevelCap`（传奇且开关开 = 40，否则 20）、`CappedAt20`、`Settle()`（按上限结算经验）、`SpendLegend(i,n)`；`AddXp` 改为调 `Settle`；`LevelPerks` 21 级以上只给 2 点；`LevelUp` 传奇等级只在偶数级调 VPE 钩子、自动投点；`RaiseTo` 上限改用 `LevelCap`；`NextXp` 改用传奇表（20 级非传奇指向 21 级）；`BuildStage` 加传奇属性；`PartFactor` 的等级成长截在 20 级；`PartHealthFactor` 乘血量点；`LoadFixup` 补数组；读档等级上限 40；`ApplySettings` 处理“传奇等级”开关；升级信件写“升到了传奇 Lv N” |
| `Source/AdventurerEpic.cs` | 成为传奇时 `Legend.Became`（补发 500/1000 杀的点、结算经验），信件加一段；`NextLevelXp` 用传奇表；`CheckKills` 传奇的 500/1000 杀各 +3；`NemesisLevel` 可到 40；`MakeChampion` 走 `Legend.RaiseEnemy`；`NemesisReward` 传奇击杀者 +2、30% 掉重铸之石；冠军信件显示“传奇 Lv N” |
| `Source/AdventurerEnemies.cs` | `Target(p,per,cap,noise)`：领队到 20 级后按超出点数升传奇；`LevelGroup`、`LevelLoose`、`Cost`（裁人）都用它；`Story` 按威胁点数可升传奇 |
| `Source/AdventurerUI.cs` | 分页高度 690；标题“传奇 Lv N”；经验条“20 级（已满，需成为传奇）”+ 悬停说明；“传奇属性点：余 N”按钮 + 八项 4×2 小格（悬停看效果）；新窗口 `Dialog_LegendPoints`（八项 +1/+5、下一点预览、合计效果）；升级信件多一个“投传奇属性点”按钮 |
| `Source/NetherilQuest.cs` | 拉洛克差事二（斩杀红袍）的报酬加一块重铸之石，信件文字同步 |
| `Bridges/VPE/Source/AdventurerVPE.cs` | `Legend.RoleHook = Role`：第一条 DND 职业路径，战士/武僧/圣武士 = melee，其他 = caster |
| `Defs/Adventurer_Legend.xml`（新） | `LP_LegendRespecStone` 传奇重铸之石（ResourceBase，市场价 6000，可用、用后消失，不可制作） |
| `Patches/Adventurer_Legend.xml`（新） | 黑网商队 `LP_Caravan_BlackNetwork` 每次 15% 带一块（复用 `StockGenerator_NetherilChance`） |
| `Textures/LP/Items/LegendRespecStone.png`（新） | 图标，由 `ArtSource/RaceArt/draw_legend_stone.py` 画（暗紫多面晶石 + 金色裂纹） |
| `Source/AdventurerChecks4.cs`（新）、`AdventurerChecks.cs`、`Bridges/VPE/Source/AdventurerVPEChecks.cs` | 新检查 12 条 + 存读档 1 条；VPE 2 条 |
| 其他 | About.xml 1.50.0 + 描述末尾一句；使用说明.txt 标题、第二部分【1.50.0 传奇等级与传奇属性点】、1.49.0 节宿敌冠军“最高 20”加注、更新日志 1.50.0 |

## 二、我自己定的细节（决定）

1. **经验表**：21 级所需 18592（= 1.49.0 表 19→20 差值 16600 × 1.12，不会比 20 级便宜），之后每级 ×1.063；累计四舍五入到 10。40 级累计约 82.5 万（20 级 118,300 的 **6.97 倍**），40 级那一级约 5.9 万。
   - **决定（1.50.0 跟进，Peter 选的改法）**：起点保留 19→20 差值 × 1.12，把每级增长从 1.12 降到 1.063，让 40 级累计落在约 7 倍（自检要求 6～8 倍）。最初版本是起点 9850 + ×1.12（21 级比 20 级便宜），已弃用。
2. **“传奇等级 21～40”设置**（默认开，装 Isekai 也开）：关掉后上限回 20、传奇属性点没效果但保留；关掉时血量点消失，伤口按比例缩小（同 1.48.0 改属性效果强度的做法）。传奇属性点不乘“属性效果强度”，Isekai 共存时照样生效（同专长/恩赐）。
3. **21～40 级**：不给属性提升/专长/恩赐；熟练加值停在 +6（`Proficiency` 本来就截在 20 级）；**部位生命的等级成长也停在 20 级**，血量只走传奇属性点。VPE 路径点在 22、24……40 级发（共 10 点），没有灵能时照旧攒着。
4. **击杀里程碑点数**：传奇达到 500/1000 杀当场 +3；成为传奇之前就达到的，在成为传奇那一刻补发（按 `milestonesDone` 里的 kills500/kills1000，不会重复）。宿敌冠军 +2 不补发（没有记录）。
5. **20 级非传奇的经验**：击杀里程碑（30%）和宿敌奖励（50%）按“20→21 级所需”算（以前 20 级是 0），因为经验现在会攒下来、成为传奇后结算。
6. **八项效果**：血量 = `Legend.HealthFactor` 乘在 `Adventurer.PartHealthFactor`（`PartHealth.WithoutReinforcement` 的倍率入口，与等级/体质成长、巨躯、巫妖强化相乘，只取整一次）；近战 `MeleeDamageFactor` 系数 ×(1+3%n)；心灵 `PsychicSensitivity` 偏移 +3%n（同感知的写法）；回复 `InjuryHealingFactor`、`ImmunityGainSpeed` 系数各 ×(1+5%n)；工作 `WorkSpeedGlobal` 偏移 +2%n；学习 `GlobalLearningFactor` 偏移 +3%n。都进 `BuildStage`，与六维同名修正合并（1.49.0 的合并逻辑）。
7. **远程的折算**：原版命中率 ≈ 每格精度^距离（ShotReport），所以 `ShootingAccuracyPawn` 乘 (1+2%n)^(1/25)，**25 格（原版“中距离”）处命中率正好 ×(1+2%n)**；12 格约 ×(1+2%n)^0.48，40 格约 ×(1+2%n)^1.6。掩体、武器精度、天气不受影响，最终命中仍封顶 100%。
8. **伤害承受**：每点 −1.5%；累计 50% 后每点 −0.75%；累计 70%（60 点）后改为每点只减“剩余伤害”的 1.25%（起步正好 −0.375%，即四分之一），所以永远到不了 100%。40 点 = −55%，60 点 = −70%，100 点 ≈ −81%。只作用于 `harmsHealth` 的伤害；`Pawn.PreApplyDamage` 前缀，和半神伤害修正同样写法。
9. **投点窗口**：殖民者自己的人可开；外人只显示余额和八项。投下的点只能用重铸之石退回（窗口里写明）。
10. **自动投点**：偏好的几项轮流 +1（点数少的先加）。殖民者靠 VPE 桥的 `RoleHook`：第一条 DND 职业路径（按 order），战士/武僧/圣武士 = 近战，其他 = 施法者；没有职业或没装 VPE = 工作 + 学习。敌人用 1.48.1 的 `Feats.EnemyRoles`：施法者优先，其次远程，否则近战；领队（`IsLeader`）再加承受。
11. **重铸之石**：只有投过点的传奇能用（`CanBeUsedBy` 给理由）；退回全部点，血量点变少时伤口按比例缩小；开了自动投点的殖民者会立刻按偏好重投。来源：宿敌冠军倒下处 30%（独立于魔法物品）；黑网每次 15%，市场价 6000（买价按交涉在 8000 上下）；拉洛克差事二。
12. **传奇敌人**：
    - 开关 = 冒险者总开关 + “传奇等级” + “敌人冒险者等级” + 本开关（Isekai 共存时读另一份，默认关），所以共存模式下“跟随敌人等级开关”且默认关。
    - 袭击领队：先按 1.48.1 算（含上限）；不带浮动的等级 ≥ 20 时改为 20 + floor((每人点数 − 20 级门槛)/60)，门槛 = (19 − 加成) × 45（领队 720）。上限：开了“敌人等级上限”时 = 殖民地最高 + N（至少 20，最高 40），否则 40。普通兵、精英兵种（不是领队）不升传奇。
    - 剧情首领（`AdventurerEnemies.Story`）：每人点数 = 一个玩家家园地图当前威胁点数 × 20%，按领队加成算门槛（即威胁点数 3600 起），够了才升传奇，否则保持 12～18。
    - 宿敌冠军：殖民地最高 + 2，上限 40（需要敌人传奇开着，否则 20）。
    - 升到 21 级以上的敌人直接标 `legendary`（不要求里程碑），点数自动投。战斗力折算沿用“每级 +3% 成本”，裁人时领队按传奇等级算成本。
13. **分页**：标题 21 级起显示“传奇 Lv N”；20 级非传奇（或开关关）经验条写“20 级（已满，需成为传奇）”。

## 三、和规格的偏差

1. **经验增长率**：规格同时要求“20→21 = 19→20 差值 × 1.12（=18592）”、“每级 ×1.12”和“40 级累计约 20 级的 7 倍”，三者不能同时成立（从 18592 起每级 ×1.12 是 12.3 倍）。按 Peter 的选择保留起点和约 7 倍，每级增长改为 ×1.063（`Legend.Growth`）。
2. 剧情首领“可到 21～40”的点数来源规格没写，我用“当前威胁点数 × 20%”。宿敌冠军用“殖民地最高 + 2”而不是点数公式。
3. “自动投点”对没装 VPE 的殖民者无法区分近战/施法职业，一律按“其他”（工作 + 学习）。

## 四、测试

构建：`pwsh Source\Build.ps1` 四个程序集都通过（游戏没在跑）。

| 命令 | 结果 |
|---|---|
| `Test.ps1 -Only Adventurer` | **PASS 103 条**（1.49.0 是 90 条；新增 12 条 + 存读档 1 条） |
| `Test.ps1 -VPE` | **PASS 459 条**（1.49.0 是 457 条；新增 2 条） |
| `Test.ps1 -Only Netheril`（顺手跑，因为改了差事二报酬） | FAIL 在 netheril 22（奥术半神）——1.48.1 起就有的旧失败，与本次无关；差事相关检查在它之前都通过 |

新检查：重铸之石定义（可用、无配方、黑网有货）；20 级非传奇攒经验不升级、成为传奇后一次结算到 23 级并得 6 + 3（之前的 500 杀）点；经验表 21 级所需 ≥ 20 级且逐级递增、40 级累计 6.97 倍（6～8 倍之间）；传奇每级 2 点、500 杀 +3；近战 10 点 = ×1.30（属性效果 0% 时也一样）、血量 10 点 = 部位生命 ×1.2、远程 10 点在 25 格 = ×1.2；伤害承受 40 点 = −55%（< 70%）、34 点 = −50.5%、递减、10 万点也到不了 100%，真实 `PreApplyDamage` 10 → 4.5；重铸之石只有传奇能用、60 点全退、伤口 12 → 10；领队 900 点/人 → 23 级传奇且自动投 远程 2/血量 2/承受 2，非领队同点数停在 20；剧情首领 900 → 23、300 → 15，徒手兵投近战/血量；殖民地最高 25 → 冠军 27 级传奇投 14 点，关掉敌人传奇时冠军停在 20；默认值（传奇开、自动投关、敌人传奇开、共存模式默认关、敌人等级关时也关）；存读档（传奇点余额、八项点数）。VPE：传奇 21～24 级 +2 路径点、共 8 点；战士 → 近战/血量，法师 → 心灵/血量。

最后给 `TickInterval` 加了“传奇且经验够就结算”（旧存档、重新打开开关）之后重新构建，又跑了一次 `-Only Adventurer`：PASS 103；`-VPE` 是在这处改动之前跑的。

跟进（经验曲线改为 18592 起 ×1.063）：重新构建，`-Only Adventurer` PASS 103。

全套检查（不带 -Only）没有跑（不要求）。

## 五、没有在实际游戏里试过的

- 分页（高度 690，新增按钮和八项小格）在各种组合下（专长多、恩赐、半神、VPE 行都有时）是否放得下；投点窗口排版。
- 成为传奇时一次结算很多级：升级信件只显示最高等级；VPE 路径点一次发多点。
- 远程命中折算在实战里的体感；伤害承受和其他减伤（护甲、巨躯、半神）叠加后的强度。
- 黑网商队实际出现重铸之石的频率和买价；宿敌冠军掉落；拉洛克差事二报酬里的石头。
- 传奇敌人在真实袭击里出现的频率（领队 720 点/人的门槛在大多数袭击里达不到；剧情首领要威胁点数 3600 以上）。
- 关掉/打开“传奇等级”开关时伤口缩放；重新打开后攒下的经验在 250 刻内结算（`TickInterval`）。
- 旧存档：1.49.0 存档读进来没有传奇点字段，按 0 处理；已经是 20 级传奇、经验超过 21 级的人读档后 250 刻内结算（会一次弹升级信件）。
