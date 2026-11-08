# DND边缘大陆 1.131.0 性能审计热修报告

> 日期：2026-11-08。版本不变（热修，没有改 About.xml / 使用说明）。
> 备份：`ArtSource/backup-1.131.0-perf/`（Source 下 8 个 .cs：CasterSpells、WizardSpellbook、WizardSpells、WizardSpells2、Classes、CasterEffects、CastApproach、PerfChecks；Defs/WizardSpells2.xml。相对路径同原位置）。
> 范围：11-06 审计之后新加的代码——1.128～1.131 法师法术书与各施法职业法术表（CasterSpells / CasterEffects）、1.129 射程外施放（CastApproach）、1.129 枪械心灵敏感度热修（Guns.cs）、1.123 吟游诗人、1.124 窗口、植入物 1.112～1.114、职业 / 子职业代码；另顺手看了早先审计没覆盖的受伤钩子。
> 原则：行为不变。所有改动都是“同一输入同一输出”的缓存、去 LINQ 分配、把按名字查定义换成静态引用、以及没人带某状态时的快速返回。会改变行为的项目一律没做，列在第四节。
> 开始前核对：没有 RimWorld 进程在运行。

## 一、结论

- **按钮栏（选中施法者时每帧每个能力都走的路径）**：分配字节大幅下降（牧师 GizmoDisabled −68%、法师 −57%，法术归属 ClassOf 与标签 Tags 变成 0 分配），时间 −8～−28%（这条本身噪声大，见第三节）。
- **法术书 / 法术表窗口**：法术行、职业页签、页签标题改为按（法术数据版本，游戏刻）记忆，不再每帧对 ~200 个法术过滤 + 排序 + 拼字串；重算本身也快了（牧师窗口的可备 + 秘法奥秘候选 −53%、分配 −71%）。
- **受伤钩子**：有法术状态在场时每次受伤查的 12 个状态定义改成静态引用，惩击判断去掉 LINQ 闭包。
- **所有人的所有能力的 CanCast**（AI 每次思考、按钮每帧）：弱智术 / 变形术的检查在没人中招时直接返回（原来每次都按名字查定义再扫状态表）。
- **整图（100 人、空闲殖民地）**：DND 混合人口比普通人口多出的开销 +15.4% → +11.2%，在 ±20% 噪声内，这次改的热点都在界面和战斗里，空闲地图走不到。
- 试过把“施放时算哪个职业”（CasterSpells.CastList）按人按帧记忆：实测它本身只要 ~90 ns，记忆（指纹 + 锁 + 字典）比重算更贵，所以撤掉了，只保留去分配的改法。

## 二、改动清单（文件:行为修改后的位置）

### CasterSpells.cs（1.130/1.131 职业法术表）
| 位置 | 问题 | 修法 | 预期收益 |
|---|---|---|---|
| `ListNamed` / `ListOf`（:55-57） | 每个法术表数据都 `Lists.FirstOrDefault(l=>l.defName==n)` 线性找 + 闭包分配；CastList 对每本书都调一次，按钮栏每帧每个能力调 6～7 次 CastList | 随 `Lists` 一起建 `byName` 字典；`ListOf(LP_ClassDef)` 改 for 循环 | 每次 CastList 少 1～3 次 LINQ 分配 |
| `AnyCircle`（:81-86） | 只取决于定义，却每次扫全部法术表（Active 对秘法奥秘、CircleFor、窗口排序每帧都调） | 按法术记在 ConcurrentDictionary 里，法术表数量变了重建 | 窗口候选列表 −53% |
| `CastList`（:113-120） | `c.casterBooks.Any(b=>b.granted.Contains(d))` 闭包分配；`foreach` 列表枚举 | `AnyGranted` 循环；for 循环 | 分配 1368 → 408 B / 23 个能力 |
| `PreparedCount` / `KnownCount` / `SecretCap` / `ArcanumAt`（:201,222,258,247） | LINQ Count / FirstOrDefault，按钮栏标签每帧、窗口每行每帧都调 | for 循环 | 0 分配 |
| `Tick250`（:328） | 没有法术表数据的人每 250 刻 `Lists.Any(l=>Level(h,l)>0)` 分配一次 | `AnyList` 循环 | 微 |
| `Version` / `Touch`（:59） | （新）法术数据版本号：备法、学法、遗忘、戏法、玄奥、奥秘、仪式开始 / 结束、同步、新建法术表、隐藏按钮、学会 / 失去能力（Harmony 后缀 `SpellbookAbilityVersion`）、职业等级（`ClassSystem.SetLevel`）都 +1 | 窗口缓存靠它失效 | — |
| `Dialog_CasterSpells`（:349-356, :377, :390-408） | 每帧 `ListsOf(pawn).ToList()`、`Tabs(h)`（4 个 LINQ 计数 + 拼字串）、法术行（全表过滤 + 排序 + 对 ~200 个法术算 AnyCircle） | 三者按（Version，TicksGame）记忆；窗口里的每个按钮操作都会 Touch | 窗口打开期间每帧从几十～几百 µs 降到 ~0 |

### WizardSpellbook.cs（1.128/1.129 法师法术书）
| 位置 | 问题 | 修法 | 预期收益 |
|---|---|---|---|
| `Bookable` / `CantripList` / `IsBookable`（:95-101） | `Bookable` 是 Where+Concat 的惰性序列，`CannotPick` 的 `Bookable.Contains(a)`（窗口每行每帧）、`PickCandidates`、残页生成每次都重新走一遍 | 按 AbilityDef 数量记一份 List + HashSet | 每行每帧省一次 ~90 项的线性扫描 |
| `Tags`（:103-104） | 窗口每行每帧 new List + string.Join | 按法术记住（只取决于定义） | 4568 → 1105 ns / 49 个，分配 4896 → 0 B |
| `PreparedCount`（:125） | LINQ Count 闭包（按钮栏“备法（x/y）”每帧） | for 循环 | 0 分配 |
| `CannotCast`（:184） | `Items.Has(p,"LP_WS_Feebleminded")` 每帧每个法术都查 | 用新的 `Hediff_Feebleminded.Live` 计数做快速返回 | 没人中弱智时省一次定义查找 + 状态表扫描 |
| `Data` / `Write` / `LearnCantrip` / `SetPrepared` / `Sync` / `StartRitual` / `FinishRitual` | — | 加 `CasterSpells.Touch()` | 窗口缓存失效 |
| `Dialog_Spellbook.Draw`（:454-459） | 每帧 `BookList`（能力表 Select/Where/Distinct/OrderBy）或 `PickCandidates`（~90 个法术 × Knows 线性查能力）+ 排序 | 法术行按（tab，Version，TicksGame）记忆 | 窗口打开期间每帧 ~30 µs → ~0 |
| `SpellbookAbilityVersion`（:613-617） | （新）`Pawn_AbilityTracker.GainAbility / RemoveAbility` 后缀 → Touch | 能力表变了窗口缓存失效 | — |
| `Hediff_Feebleminded`（:619-624）+ `Defs/WizardSpells2.xml:118` | （新）弱智术状态类，计数 Live；`GameComponent_Spellbook.FinalizeInit` 读档后按实际状态重数一遍（老存档里已有的弱智术还是旧类，不会自己登记——这样只会让计数偏高、多扫几次，不会漏掉效果） | — | — |

### WizardSpells2.cs
| 位置 | 问题 | 修法 | 预期收益 |
|---|---|---|---|
| `WizardNoCasting`（:560-566，`Ability.CanCast` 后缀） | 所有人的所有能力（含心灵能力、AI 每次思考）每次 CanCast 都 `Items.Has(p,"LP_WS_Feebleminded")`：按名字查定义 + 扫状态表 | `Hediff_Feebleminded.Live<=0 && Hediff_WS.Live<=0` 直接返回 | 原版止痛灵能 CanCast 304 → 248 ns |

### Classes.cs
| 位置 | 问题 | 修法 | 预期收益 |
|---|---|---|---|
| `ClassSystem.ClassOf`（:92-101） | 不在职业能力表里的法术（WS_/CS_ 全部）每次都顺着 传奇 → 子职业 → 秘传 → 法术书 → 全部法术表 查一遍（CasterSpells.ClassOf 要对每张表查字典）；CannotCast、Cost、CostReduce、Hidden、CastClassOf 每帧每个能力各调几次 | 结果只取决于定义，按能力记在 ConcurrentDictionary；AbilityDef / LP_ClassDef / CasterListDef / LP_SubclassDef 任一数量变了重建 | 3364 → 1399 ns / 72 个能力，分配 384 → 0 B |
| `SetLevel`（:216） | — | `CasterSpells.Touch()` | 职业等级变了窗口缓存失效 |

### CasterEffects.cs（1.130/1.131 受伤钩子）
| 位置 | 问题 | 修法 | 预期收益 |
|---|---|---|---|
| `CasterSpellDamage`（:560-577） | 有任何 CS 状态在场时每次受伤 `Items.Hediff("…")` 查缓存字典 6 次 | 6 个静态 `DefRef<HediffDef>` | 每次受伤省 6 次字符串哈希查找 |
| `CasterSpellRebuke`（:579-589） | `hediffs.Any(x=>x is Hediff_CS && OnHit.ContainsKey(…))` 闭包分配；`Items.Hediff("LP_CS_Rebuke")` | `HasOnHit` 循环；静态 DefRef | 每次敌方命中 0 分配 |

### WizardSpells.cs（1.128/1.129 受伤钩子）
| 位置 | 问题 | 修法 | 预期收益 |
|---|---|---|---|
| `WizardSpellDamage`（:560-585） | 有法术状态在场时每次受伤 `Items.Has(p,"…")` 按名字查 7～8 次 | 6 个静态 DefRef（防护能量的 `ProtHediffs[k]` 已是按类型分支，保留） | 每次受伤省 6～7 次字符串哈希查找 |

### CastApproach.cs（1.129 射程外施放）
| 位置 | 问题 | 修法 | 预期收益 |
|---|---|---|---|
| `Approachable` / `ValidFarMemo`（:104-121）及三个钩子（:191, :208, :217） | 选目标时 ValidateTarget 前缀、OnGUI 前缀、DrawHighlight 后缀每帧对同一目标各算一次 `CanHitTarget`（视线）和 `ValidateFar`（`CanReach` 可达 + 能力检查）；OnGUI / DrawHighlight 还通过 `ValidateTarget` 再把 Approachable 算一遍 | 同一帧、同一动词、同一目标的结果记住（只记 showMessages=false 的那条；showMessages=true 照旧算并发消息） | 选目标时每帧 3 次视线 + 2 次可达 → 各 1 次 |

### PerfChecks.cs（自检）
- 新增 `SpellBench`：17 级法师（法术书写满、戏法学满，属性固定 Int 18）和 9 级牧师（备法备满，Wis 18）的按钮栏热路径（Hidden、GizmoDisabled、GetGizmos、CastList）、两个窗口的法术行、ClassOf、Tags 的微基准，写进 `[LICH-PERF]` 日志和 `LichPerf.txt`。

## 三、前后对比（`Test.ps1 -Only Perf`，同一机器同一时段；修改前 = 备份源码 + 新 PerfChecks 另编译换上）

| 微基准（ns / 次，B / 次） | 修改前 | 修改后 | 变化 |
|---|---|---|---|
| 法师 17 级 `Spellbook.Hidden` ×49 个能力 | 13793 / 1176 | 9971 / 1176 | −28% |
| 法师 17 级 `GizmoDisabled` ×49 | 117112 / 9712 | 108230 / 4168 | −8%，分配 −57% |
| 法师 17 级 `GetGizmos` ×49 | 11220 / 1360 | 9631 / 1360 | −14% |
| 牧师 9 级 `Spellbook.Hidden` ×23 | 5846 / 3008 | 5683 / 960 | −3%，分配 −68% |
| 牧师 9 级 `GizmoDisabled` ×23 | 81592 / 32404 | 91137 / 10280 | +12%（噪声，见下），分配 −68% |
| 牧师 9 级 `GizmoDisabled` ×23（Touch 后） | 62213 / 26552 | 60078 / 10280 | −3%，分配 −61% |
| 牧师 9 级 `CasterSpells.CastList` ×23 | 2049 / 1368 | 1815 / 408 | −11%，分配 −70% |
| 法师法术书窗口三页法术行 | 29860 / 8688 | 26025 / 8304 | −13%（且不再每帧算） |
| 牧师法术表窗口（可备 + 秘法奥秘候选） | 174424 / 93640 | 82082 / 27016 | −53%，分配 −71%（且不再每帧算） |
| `ClassSystem.ClassOf` ×72 个能力 | 3364 / 384 | 1399 / 0 | −58% |
| `Spellbook.Tags` ×49 | 4568 / 4896 | 1105 / 0 | −76% |
| 原版止痛灵能 `CanCast` | 304 / 120 | 248 / 120 | −18%（弱智术快速返回） |
| 原版止痛灵能 `GizmoDisabled` | 2671 / 168 | 2501 / 168 | 噪声内 |
| 整图 100 人：普通 vs DND 混合（ms / 刻，5 轮中位） | 0.810 vs 0.935（+15.4%） | 1.044 vs 1.161（+11.2%） | 噪声内 |

- GizmoDisabled 这一组同一次运行里“Touch 后重算”反而比“不 Touch”快 30%，说明相邻两条微基准之间的时间噪声有 ±30%（GC、JIT 排布），时间数字只能看趋势；**分配字节是稳定的**，这是这次最可靠的指标。
- 剩下的每个能力 ~2～3 µs 的 GizmoDisabled 开销里，原版自己的 `Ability.GizmoDisabled`（止痛灵能）就要 2.5 µs，我们这边的职业 / 法术检查链已经和原版同一量级。

## 四、没做的（以及原因）

| 项目 | 原因 |
|---|---|
| `JobDriver_CastApproach.Step` 每刻 `CanCastNow`（视线）+ `ValidIgnoringDistance` | 改成隔几刻检查会推迟“到位就施放”的那一刻，行为变化 |
| `CasterSpellsGizmo` / `SpellbookGizmo` 每帧拼按钮标签、算待选数 | 原版 gizmo 也是每帧重建；把 Command 对象跨帧复用收益小、风险在 UI 状态 |
| `ClassSystem.Regen` 每 250 刻 `levels.ToList()` + `new HashSet` | 每人每 250 刻一次，收益可忽略 |
| `Hediff_CS.HediffTick`「自由行动」每秒 LINQ 过滤 6 个状态名 | 每秒一次，收益可忽略 |
| `ClassLegendary.Torrent` / `FreeCast` 里的 `Items.Has(字符串)` | 每次只是一次字典查找 + 状态表扫描；要省掉得给魔力奔流加计数类，收益小 |
| `Subclass2.CannotCast` 没有子职业用户时 4 次 `ClassSubclass.Has` | 每次 ~200 ns，改判断顺序会动到 1.103 的逻辑，收益小 |
| `Arcana.TargetFactor`（枪械 PreApplyDamage）每次 `GetCompProperties` | 原版 comps 列表只有几项的循环，缓存不划算 |
| `RuneShieldBlock`（ItemsRaceGear.cs:123）每次受伤算 `BlockChance` | 1.58.6 之前的老代码，这次范围外 |
| 1.129.0 枪械心灵敏感度热修（Guns.cs） | 看过：倍率按投射物缓存一次（`ArcaneFactor`），命中后 `TargetFactor` 只在带武器的伤害上走，没有新热点 |
| 1.112～1.114 植入物（DndImplants.cs） | 看过：所有钩子都先看 `Live` 计数，`Flags` 按小人缓存，没有新热点 |
| 1.123 吟游诗人（ClassBard.cs） | 看过：受伤钩子先看 `Users.Count / Live`，`SecretClass` 用 HashSet，没有新热点 |
| “施放时算哪个职业” `CastList` 按人按帧记忆 | 做了、量了、撤了：它本身 ~90 ns，记忆的指纹 + 锁 + 字典比重算还贵 |

## 五、线程安全与失效
- 新缓存里跟定义绑定的（ClassOf、AnyCircle、Tags、Bookable）都是 ConcurrentDictionary 或一次建好的只读列表，按 DefCount 重建；渲染线程不会碰到这些路径，但即便碰到也只是多算一次。
- 窗口缓存只在窗口对象里，主线程。失效条件：法术数据版本（`CasterSpells.Version`）或游戏刻变化。版本在所有改法术书 / 法术表 / 能力表 / 职业等级的入口 +1（第二节列出）；暂停时窗口里的任何操作都会 +1，所以不会出现“点了没反应”。
- `Hediff_Feebleminded.Live`：PostAdd / PostRemoved / 读档 PostLoadInit 计数，`GameComponent_Spellbook.FinalizeInit` 再按实际状态重数（老存档里旧类的弱智术）。新游戏 / 换存档时 GameComponent 构造清零。
- CastApproach 的按帧记忆只在同一帧、同一动词对象、同一目标时命中；下命令（showMessages=true）那条不走记忆。

## 六、构建与测试
- `pwsh .\Build.ps1`：主程序集、HAR / VPE / 神兽血裔三个桥全部 PASS。
- `Test.ps1 -Only Perf`：PASS（修改前后各跑一次，数字见第三节；用于对比的旧版 DLL 临时编译在 `Mods\_LichPerfOld`，跑完已删除，Defs 也已换回新版）。
- `-Only 'Spellbook,CasterSpells,CastRange'`：56 条 PASS（28 + 21 + 7）。第一次跑时 CastRange 第 2 条失败——同一帧里先在选目标界面外、再开始选目标后各问一次 ValidateTarget，按帧记忆把第一次的“拒绝”带到了第二次；记忆键加上当前选目标来源和双方格子后通过。
- `-Only 'Classes,NpcClasses,Guns'`：342 条 PASS。
- `-Only Bard`：16 条 PASS。
- `-Only 'Classes,NpcClasses,Bard,Guns'`（四组合跑，358 条）：新 DLL 跑了 5 次，2 次 PASS，2 次停在吟游诗人第 14 条「crystals: 不可抗拒之舞 / 力量真言·震慑 / 群体隐形」，1 次停在职业组第 54 条「storm herald (tundra): aura hurts the adjacent foe (injury 0 → 0)」；旧 DLL 合跑 1 次 PASS。两条失败都是依赖随机的检查（随机生成的敌人、随机部位伤害、前面几组留下的小人），加诊断输出的那次合跑通过时各项子条件全部为真，单独跑 Bard 也通过；按 [[classes-1-78]] 记过的“合跑共用测试场景”问题处理，没有改测试代码（临时加的诊断已撤回，`ClassBardChecks.cs` 与改前逐字节一致）。
- 没有改 About.xml / 使用说明 / 版本号；没有 git 提交。
- 已知、与本次无关：LP_GelCubeBody 无冻伤部位的报错；Netheril 组不稳定；Nonlethal 组断言 19 个设置分组而 Core.cs 有 20 个。
