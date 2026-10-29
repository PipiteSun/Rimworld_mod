# 1.48.0 冒险者等级（第一阶段）实现报告

依据：`ArtSource/Leveling-1.48.0-spec.md`（权威）、`Leveling-1.48.0-design.md` 第三～五、十节。备份 `ArtSource/backup-1.47.0`（1.7、About、Bridges、Defs、Patches、Source、使用说明.txt、LoadFolders.xml）。本阶段没有做敌人等级、专长、史诗恩赐。

## 一、加了什么

### 主模组（不依赖 VPE、Isekai）
| 文件 | 内容 |
|---|---|
| `Source/Adventurer.cs`（新） | `Adventurer` 静态类（经验表、熟练加值、六维生成、种族表、经验/里程碑/击杀分配/任务、效果系数、设置变化时缩放伤口）；`Hediff_Adventurer`（等级、经验、六维、待加点、里程碑、史诗恩赐标记、VPE 已发/攒下的路径点；`CurStage` 按属性合成并缓存）；`GameComponent_Adventurer`（旧存档补生成标记、袭击记录、升级信件合并、伤害记录）；Harmony 挂钩；设置分组 |
| `Source/AdventurerUI.cs`（新） | “冒险者”检视分页（挂到所有人形种族，含 HAR）、加点窗口、升级信件 `Letter_AdventurerLevel`；自测进程启动时抑制生成 |
| `Source/AdventurerChecks.cs`（新） | `-Only Adventurer` 检查组 + `-Isekai` 共存检查 |
| `Defs/Adventurer.xml`（新） | `LP_Adventurer`（HediffDef）、`LP_AdventurerLevelLetter`（LetterDef） |
| `Languages/English`、`Languages/ChineseSimplified` `/Keyed/LP_Adventurer.xml`（新） | 分页标题键 `LP_AdventurerTab`（“冒险者”） |
| `Source/LimbHealth.cs` | `PartHealth.WithoutReinforcement` 乘冒险者部位生命系数（与巫妖强化、巨躯规则相乘，只取整一次） |
| `Source/Core.cs` | 设置存取 `ExposeAdventurer`、左栏绘制 `AdventurerSettings.Draw`（在“成神”下面）、关设置窗口时 `Adventurer.ApplySettings` |
| `Source/PortalEvent.cs`、`Twilight.cs`、`DragonFlyover.cs`、`TowerCrash.cs`、`NetherilQuest.cs`、`Demigod.cs` | 各加一行里程碑 |
| `Source/NetherilScrolls.cs` | 研习卷每小时 +50 冒险者经验 |
| `Source/ItemChecks.cs` | `-Isekai` 档调用 `CheckAdventurerIsekai` |

挂钩：`PawnGenerator.GeneratePawn` 后缀（生成）；`Pawn_AgeTracker.BirthdayBiological` 后缀（13 岁）；`Pawn_HealthTracker.AddHediff` 后缀（加上死灵造物/召唤物状态时拿掉冒险者）；`Pawn_SkillTracker.Learn` 前缀（学习系数）；`Thing.TakeDamage` 后缀 + `Pawn.Kill` 前后缀（击杀分配）；`IncidentWorker_Raid.TryExecuteWorker` 前后缀 + `Pawn_DraftController.set_Drafted`（袭击）；`Quest.End`（任务）；`GameComponent.FinalizeInit`（旧存档补生成）。

### VPE 桥（Bridges/VPE）
| 文件 | 内容 |
|---|---|
| `Source/AdventurerVPE.cs`（新） | 升级 +1 路径点 / 攒点；`InitializeFromPsylink` 后缀发放攒点；路径主属性表；主属性 13 解锁；职业强度加值；自动加点用的主属性 |
| `Source/AdventurerVPEChecks.cs`（新） | `-VPE` 里的 10 条冒险者检查 |
| `Source/VPEBridge.cs` | 注册钩子；`Classes.Power` 两个重载加冒险者加值；`LP_ClassPathDef.SkillOk` 加“或主属性 ≥ 13”；职业强度属性说明多一行“冒险者” |
| `Source/Manual.cs` | 已有心灵链接时按 `SkillOk` 选路径（主属性 13 也算）；读完给 200 训练经验（只到 3 级） |
| `Source/Mentor.cs` | 每课每个学生 +50 冒险者经验 |
| `Source/BridgeChecks.cs` | 调用 `AdventurerChecks()` |
| `Defs/Paths.xml`、`Class*.xml` | 8 条路径的 `lockedReason` 写上主属性条件 |

### 其他
About.xml 1.48.0 + 描述末尾一句；使用说明.txt 标题改 1.48.0、第二部分新增【1.48.0 冒险者等级】、更新日志加 1.48.0 条目。

## 二、我自己定的细节

1. **总开关关掉**：照样生成角色卡（只是数据），但不再获得经验、属性没有效果、属性不能解锁职业路径、分页隐藏；打开即恢复。这样关了再开不用补生成。
2. **种族表**补了构思里没写的：寒冰魔 力+2 体+1、炎魔 力+2 魅+1；双头/独眼/六臂蛇人的“血裔”基因异种（LP_EttinKin、LP_CyclopsKin、LP_MarilithKin）与 HAR 种族同值。巫妖按“生成时已经是巫妖”判断，之后才成巫妖的人属性不变（不重算）。
3. **体质排序键**：基础 5，健壮体型 +3、Tough +3，坚韧/再生类基因（Robust、LP_HalfOrcRelentless、LP_TrollRegeneration、名字含 Regenerat 的基因）+2（只算一次）。技能被完全禁用记 -1（排最后）。
4. **背景特性**也按生成时算；生成后才获得的背景特性不追加属性。
5. **效果的数值形式**：倍率类用 statFactors（近战伤害、负重、射击精度、免疫、研究、医疗品质、驯兽、社交影响），百分点类用 statOffsets（闪避 +1/点、移速 +0.05/点、疼痛休克阈值 +0.02/点、全局学习 +0.03/点、心灵敏感度 +0.04/点、交易价格 +0.015/点）。
6. **部位生命**：系数 = 1 + max(0, (等级−1)×(1% + 0.5%×max(0,体调)) + 4%×体调) × 属性效果强度。1 级不加；**负体质不减部位生命**（避免读档补生成时把受伤的心脏/大脑直接打到 0）。调低“属性效果强度”或关开关时，已有伤口按新旧上限比例缩小（同巨躯规则的做法）。
7. **学习速度**：只对正的、非 direct 的技能经验生效；系数 max(0.5, 1 + 5%×调整值×强度)。手工、建造、采矿、艺术、烹饪不受影响（没有对应属性）。
8. **击杀**：伤害只记玩家阵营人形造成的（炮塔、动物不算），按殖民者之间的比例分满 combatPower。伤害记录不存档（读档会丢掉正在进行的战斗的协助记录），1 天没更新就清掉。受害者必须敌对、不是囚犯、不是玩家阵营。
9. **守住袭击**：只跟踪原版 `IncidentWorker_Raid` 发起的袭击（DND 事件自己刷出来的怪物群，如传送门、冬狼、兽群，不在这个通道里，只靠击杀经验）。参战 = 袭击开始时已征召、期间征召过、或打过袭击者。结束 = 袭击者全部倒下/死亡/离图/不再敌对；超过 3 天强制结算。“我方未失败” = 地图还在、还有站着的自由殖民者。
10. **任务**：接任务的人得点数 × 0.5；其他在玩家家园地图上的自由殖民者各得 × 0.5 × 1/4。没有接任务人的任务只给家里的 1/4。
11. **里程碑分给谁**：传送门、神明附体者、法师塔给当时地图上的自由殖民者；龙过境决斗给出战的龙；拉洛克差事每件（key larloch1/2/3）给全体自由殖民者；议会试炼给半神本人，**每个神性等级各一次**（key council2…5），否则一个半神只能拿一次 2500。法师塔“清场”= 玩家自己拆掉残骸（塞伊/同盟拆走不算）。神明附体者在倒地离体或死亡时都走 `Twilight.Reward`，所以两种都算。
12. **训练**：冒险者手册读完给 200（受经验倍率影响），不能超过 3 级下限 300。训练假人目前模组里没有，没做。
13. **自动加点**：职业主属性（VPE 第一条已解锁的 DND 路径，按 order）优先，否则最高的一项（同分按力敏体智感魅）；能 +2 就 +2，主属性 19 时 +1 主属性 +1 次高项。敌人、访客、无阵营的人永远自动。补生成的旧殖民者若到 4 级以上，待加点留给玩家（自动加点开关开时自动）。
14. **升级信件**：每 60 刻合并发一次；只发给玩家阵营的人；有待加点时信里有“给某某加点”按钮。
15. **VPE 职业强度**：能力按所属路径的主属性；按技能的属性（战技/奥术/神恩强度、光环、武僧架势）按技能对应（格斗取力/敏较高、智识与手工看智力、社交看魅力、种植与驯兽看感知）。属性部分乘“属性效果强度”（Isekai 共存时为 0），熟练部分不乘。
16. **攒点发放**：挂在 VPE 的 `Hediff_PsycastAbilities.InitializeFromPsylink`；另外每次升级前也会先尝试发放一次。
17. **Isekai 共存模式**设置名照规格；存的是“共存模式开不开”（默认开）。没有 Isekai 时灰显、点了不改值。
18. **分页标题**用了 Keyed 翻译（模组以前没有 Languages 文件夹），中文和英文各一个键。
19. **自测抑制**：和背景特性、信仰一样，自测进程（`-lichtest`）启动时 `Adventurer.TestSuppressed=true`，别的检查组生成的小人没有冒险者状态，旧检查的数值不受影响；冒险者检查组自己临时打开。

## 三、和规格的偏差

- 规格第三节“旧小人起始等级 = min(5, 每 10 个人形击杀 +1 级)”：照做，起点为 1 级（0～9 击杀 1 级、10～19 击杀 2 级……）。
- 规格第六节“部位生命 +4%/体调”：负体质不扣（见二.6），属于有意偏差。
- 规格第四节“守住袭击”只覆盖原版袭击通道（见二.9）。
- 议会试炼里程碑按神性等级分 key（见二.11）。
- 规格第十节第 9 条“13 岁成长时刻补上”：实现挂在 13 岁生日（原版成长时刻就在生日里触发），所有 13 岁生日都会补，不止殖民地孩子。

## 四、测试

构建：`pwsh Source\Build.ps1` 四个程序集都通过。

| 命令 | 结果 |
|---|---|
| `Test.ps1 -Only Adventurer` | **PASS 34 条**（含存读档）；加入 Isekai 检查调用后为 35 条，在下面一行的组合里又跑过 |
| `Test.ps1 -Only Adventurer,Backstories,Ascension` | **PASS 58 条**（回归：生成和议会挂钩不影响背景故事、成神） |
| `Test.ps1 -VPE` | **PASS 452 条**（原有职业检查全过 + 新的 10 条冒险者检查） |
| `Test.ps1 -Isekai -Only Adventurer` | **PASS 38 条**（冒险者 34 + 共存 4 条） |
| `Test.ps1 -Isekai`（物品档） | **FAIL**：旧检查“opening a real Isekai chest awards its prize plus 1 DND items”多掉了一件（两次分别是 LP_VorpalSword、LP_RevivalCore）。宝箱代码这次没有动；看起来是 1.45 魔法物品加了宝箱掉落而这条检查的期望数没更新（旧的过期检查）。因为它在前面失败，物品档里的 `CheckAdventurerIsekai` 没跑到，所以改用上一行的组合覆盖了共存检查 |

`-Only Adventurer` 覆盖规格第十节：1 生成（标准数组 + 人类 +1 + 特性，排序按技能，士兵 +1 力）；2 半兽人力+2 体+1、食人魔力+3 智−2、全部 DND 异种都在种族表里；3 2200 经验 → 5 级、熟练 +3；4 两人各一半伤害各得 combatPower × 0.5、囚犯不给；5 4 级待加点 1、19 加 2 被拒、18 加 2 到 20、自动加点；6 力 16 近战 ×1.12、负重 ×1.24，强度 0% 为 ×1；部位生命 ×1.16、负体质不减；7 学习 ×1.15 / ×0.8 / 最低 ×0.5、手工不受影响；8 存读档（等级、经验、属性、里程碑、待加点、攒点、补生成标记）；9 13 岁以下没有、13 岁生日补 1 级、死灵造物没有；10 补生成按击杀估等级且只做一次；另外：袭击分配、里程碑只给一次、经验倍率、训练只到 3 级、总开关。`-VPE`：升级给路径点、没有灵能时攒点并在第一次心灵链接时发放、智力 13 解锁法师、战士取力/敏较高、总开关关掉不再解锁、职业强度 1.14 公式、强度 0% 只剩熟练部分、自动加点主属性跟职业。`-Isekai`：检测到 Isekai 时默认六维效果关、等级照常、关掉共存模式后效果恢复。

全套检查（不带 -Only）没有跑（不要求）。

## 五、没有在实际游戏里试过的

- 分页、加点窗口、升级信件的界面排版（只在检查里调用了逻辑，没截图）；分页在 HAR 种族、尸体上的显示。
- 真实袭击的开始/结束判定（检查里直接构造了袭击记录结算）、征召记录、空投袭击、袭击者逃跑的情形。
- 任务完成经验（`Quest.End` 挂钩没有在检查里触发）。
- 各事件里程碑的实际触发（传送门关闭、神明附体者被击退、龙决斗、拆塔、拉洛克差事、议会试炼）——只验证了 `Milestone` 本身。
- 研习卷、云游导师、冒险者手册的经验（挂钩已加，没有单独检查）。
- 原版成长时刻的完整流程（检查里直接调用 13 岁生日）。
- 大存档首次读档补生成的耗时（会遍历所有活着的世界小人一次）。
- 经验节奏是否合适（构思说“前线战斗员第一年 5 级左右”，需要实机看；可用经验倍率调）。
