# DND边缘大陆 1.33.0 报告：六臂蛇人（Humanoid Alien Races 可选联动）

规格：`ArtSource/Marilith-1.33.0-spec.md`。备份：`ArtSource/backup-1.32.0/`（本次改动过的原文件 + About.xml + 使用说明.txt + LoadFolders.xml）。

## 版本与 HAR
- 设备上的模组版本：**1.33.0**（About.xml `modVersion`）。
- HAR：创意工坊 **839005762**，packageId `erdelf.HumanoidAlienRaces`，用 `1.7/Assemblies/AlienRace.dll`（AssemblyVersion 1.0.0.0，文件日期 2026-10-26 02:49，Peter 今天订阅的版本）。游戏版本 1.7.4985 rev1269。XML 写法按 HAR GitHub 源码与 wiki（1.5+ 的 `<conditions>` 语法）核对。
- 联动是**可选的**：没装 HAR 时 `Bridges/HAR` 不加载，主程序集不引用 HAR 类型；`Test.ps1 -NoHAR -Only Marilith` 通过（无红字，蛇人定义、联动程序集、敌人兵种都不存在）。About.xml 只加了 `loadAfter`，没有硬依赖。

## Bridges/HAR 结构（以后的种族，如流浆人，可以照抄）
```
Bridges/HAR/
  Defs/Marilith_Race.xml    种族（AlienRace.ThingDef_AlienRace，ParentName="Human"）、BodyDef、蛇尾 BodyPartDef/Group、PawnKind、AlienRace.RaceSettings
  Defs/Marilith_Genes.xml   异种型、基因、状态、能力
  Source/BridgeChecks.cs    只放需要 AlienRace.dll 类型的代码（这里只有自检）→ 编成 Assemblies/LichPhylactery.HARBridge.dll
  Assemblies/               Build.ps1 生成
  Textures/Marilith/        Bodies、Addons（身体附件，含 _m 遮罩与 _west）、Genes、Abilities、Xenotype
```
- `LoadFolders.xml`：`<li IfModActive="erdelf.humanoidalienraces">Bridges/HAR</li>`。**注意 id 要全小写**：1.7 的 `ModsConfig.IsActive`/`IfModActive` 把查询转小写后在“已启用列表”里精确匹配；玩家的 ModsConfig 里是小写，所以必须写小写（Test.ps1 生成的测试 ModsConfig 也改成写小写）。
- `Source/Build.ps1`：主程序集之后编 HAR 联动程序集（引用主程序集 + 创意工坊 839005762 的 AlienRace.dll），找不到 HAR 时 `SKIP`。
- 主程序集识别蛇人：种族 ThingDef 上的 `DefModExtension LichPhylactery.MarilithExtension`（`Marilith.Is(p)`）；所有规则（六臂、副手、蛇行、冷血、能力、巨躯、渲染缩放、袭击）都在主程序集 `Source/Marilith.cs`，对 HAR 不存在时全部用 `GetNamedSilentFail`。主程序集按名字反射调用联动检查（`Source/HARBridgeHook.cs`，同 VPE 的做法）。
- 新种族照抄时：新建 `Defs/<种族>.xml`（`ParentName="Human"` 继承人类，再覆盖 body/体型/统计；`alienRace` 里的列表记得 `Inherit="False"`），种族上挂自己的 DefModExtension 供主程序集识别，贴图放 `Textures/<种族>/`。

## 默认决定（Peter 可改）
| 问题 | 决定 |
|---|---|
| 身体 | 真六臂 + 真蛇尾（HAR 自定义 BodyDef `LP_MarilithBody`），没有腿和脚 |
| 副手 | 背包近战武器自动当副手，按 `MeleeWeapon_AverageDPS`（已含品质与材料）取最高；最多 = 可用额外手臂对数（一对里至少一只手能用就算） |
| 六臂操作加成 | 按“手”算：每只能用的额外手 +10%，四只 +40%（整对时与规格的 0.4×对数/2 相同，断一只手就降一半）。原版操作取所有手臂平均，所以断两条额外手臂后约 87%（4/6 + 20%） |
| 体型 | 1.6（成年），儿童随年龄长大；渲染：根节点 ×1.3（同巨人做法，不用 HAR 的 customDrawSize），衣服、额外手臂、蛇尾一起缩放 |
| 体型类型 | 成年统一 Hulk（衣服直接借原版壮硕体型贴图），儿童 Child/Baby |
| 性别 | 女性 85% |
| 来源 | 开局：HAR 的“角色种类”下拉框可选“六臂蛇人殖民者”，原版开局随机权重 5（人类 100）；流浪者加入、难民权重 5；敌人：塞伊远征军 ≥1000 点战斗袭击 15% 一名“六臂蛇人剑舞者”（顶掉 2 名普通随从，背包 1～2 把副手刀剑，≥2500 点 2 把） |
| 与巨人 | 不是巨人，不走 GiantKin，不能穿巨人专属衣甲、没有合身心情 |
| 能力 defName | 基因与能力分开命名：能力是 `LP_SerpentConstrictAbility`、`LP_BladeStormAbility`（基因仍是 `LP_SerpentConstrict`、`LP_BladeStorm`） |
| 缠绕 | 任何有这个基因的人都能用（不要求蛇尾）；对方晕 3 秒并每秒 6 点钝伤×体型比（0.5～2）；施法者进入 3 秒忙碌姿态（不能移动） |
| 剑刃风暴 | 只有两条手臂时只有主手那一击 |
| 鳞色 | 三个外观基因不放进异种型（异种型不能随机一种），生成时按 40/35/25 随机加一个为内源基因，遗传时随父母；HAR 皮肤色通道也给了这三种色作后备 |
| 深渊之躯 | 毒素抗性 +50% 与种族的 50% 相加 = 100%（蛇人毒素免疫），按规格原样保留 |
| 冷血 | 只在地图上（Spawned）且环境 <0℃ 时生效 |
| 剑舞者 | 只做敌人兵种（`LP_FH_MarilithBlademaster`），不进 XML 兵种表，由 `MarilithRaid` 代码插入 |

## 怀孕（HAR × Biotech）
HAR 默认用**母亲的 PawnKind**生孩子（wiki：childKindDef 默认母亲的），所以孩子的种族随母亲，符合规格，没有另写代码。`RaceRestrictionSettings.CanReproduce(蛇人, Human)` 双向为真（自检）。人类母亲生的孩子是人类，可能继承蛇人基因（混血异种），其中六臂连击在两条手臂的身体上无效。

## 兼容结论（第六节）
grep 结果：全模组只认 `ThingDefOf.Human` 的地方都是死灵造物（构造体固定为 Human 种族，与蛇人无关）、测试夹具，以及蝌蚪/巨魔血配方的 `recipeUsers` Human（HAR `humanRecipeImport` 会把带 Human 的配方自动加给蛇人，自检确认）。
| 系统 | 结论 |
|---|---|
| 巨躯规则 1.23.0 | 原来只认基因/血统来的体型（`BodySize.Size`），种族自带的 1.6 不算。**已改**：`BodySize.Size` 再乘 `Marilith.SizeFactor`（成年 1.6、儿童随年龄），生命 ×1.3、近战 ×1.18（自检） |
| LimbHealth | 部位生命跟 HealthScale 走，六臂与蛇尾都吃到 ×1.3（蛇尾 40→52，自检）。巫妖“强化肢体”列表加了 `LP_SerpentTail`（原来只有 Arm/Leg 等）。没跑 `-HealthCompat` 档 |
| 巨人衣服规则 1.30.0 | 只认 GiantKin 基因，蛇人不是巨人：专属衣甲穿不上、无合身心情，未改 |
| Isekai 特性 | 本模组的 Isekai 联动不认种族；HAR 不限制这个种族的特性（自检：背景特性/IKM 特性 8 个都允许）；`-Isekai -Only Marilith`：Isekai 给蛇人挂上 IsekaiComponent（20 项通过）。IsekaiKillMilestones、HumanAptitudes 只认 `RaceProps.Humanlike`；MythicBloodlines 唯一认 Human 的是“同伴开局给人类天赋”的剧本专用代码（只给原版人类，不是种族门槛）。**三个模组都不需要改，没有升版本** |
| VPE 职业 1.27.0 | `-VPE`：蛇人能解锁战士/法师/牧师（技能被背景禁用时同人类一样不能走）；旋风斩命中不连锁副手（279 项通过，含新增 2 项） |
| 基因工作台 | 六个蛇人基因都可提取（自检）；人类装六臂连击 0 个副手，描述写“需要多于两条手臂” |
| 1.32.0 背景特性 | HAR 不限制，`allowHumanBios` 开，蛇人能随机到 |
| 夺心魔 | 蛇人尸体是类人、有大脑，`CannotDevour` 允许（自检） |
| 巫妖、巨魔复活等尸体系统 | `Necro.Integrity` 对蛇人尸体正常；没有红字 |
| 原版手术 | 仿生臂 6 个肩都能装、腿部手术 0 个部位（自检） |
| 双持模组 | 按 packageId 含 `dualwield`/`tacticowl` 检测，自动关闭副手连击并在日志写一行（本机没装，未实测） |

## 改动与新增文件
新增：
- `Bridges/HAR/Defs/Marilith_Race.xml`、`Bridges/HAR/Defs/Marilith_Genes.xml`
- `Bridges/HAR/Source/BridgeChecks.cs`、`Bridges/HAR/Assemblies/LichPhylactery.HARBridge.dll`
- `Bridges/HAR/Textures/Marilith/…`：身体 6 张（Hulk/Child × 南东北）、额外手臂 32 张 + 32 张遮罩（4 臂 × 成人/儿童 × 南东北西）、蛇尾 6 张、基因 6、能力 2、异种型 1
- `Source/Marilith.cs`、`Source/MarilithChecks.cs`、`Source/MarilithGallery.cs`、`Source/HARBridgeHook.cs`
- `ArtSource/RaceArt/draw_marilith_133.py`、`ArtSource/Marilith-gen_body.py`（生成 BodyDef 那一段）、`ArtSource/RaceArt/marilith_ref/`（自测从游戏导出的原版 Naked_Hulk/Child 轮廓，画身体用）
- `ArtSource/Marilith-1.33.0-spec.md`、`-report.md`、`-preview.png`（图标 + 拼合四向）、`-ingame.png`（游戏内南/东/北截图：裸身、防弹衣、风衣、板甲、断臂、人类、儿童）
修改（原文件在 backup-1.32.0）：
- `About/About.xml`（1.33.0、描述、loadAfter HAR）、`LoadFolders.xml`、`使用说明.txt`（标题、联动说明、【1.33.0 六臂蛇人】、更新日志）
- `Source/Build.ps1`（HAR 联动程序集）、`Source/Test.ps1`（默认加 HAR、`-NoHAR`）
- `Source/BodySize.cs`（种族体型进巨躯规则）、`Source/Core.cs`（设置保存与“六臂蛇人”分组）、`Source/Factions.cs`（塞伊袭击插入剑舞者）、`Source/LimbHealth.cs`（蛇尾可强化）、`Source/Checks.cs`（接入 Marilith 组与截图）
- `Bridges/VPE/Source/BridgeChecks.cs`（蛇人职业路径、旋风斩不连锁 2 项）
- 主程序集 `1.7/Assemblies/LichPhylactery.dll` 与 VPE/神兽血裔联动 dll 重新编译

## 测试
- `pwsh Source/Test.ps1 -Only Marilith`：**PASS 19 项**（主程序集 12 + HAR 联动 6 + 存读档 1），日志无红字；同时截图并导出原版身体轮廓。
- `-NoHAR -Only Marilith`：PASS 1 项（没有 HAR 时蛇人内容与联动程序集都不加载，无红字）。
- `-Isekai -Only Marilith`：PASS 20 项（多一项 Isekai 组件）。
- `-VPE`：PASS 279 项（含新增 2 项）。
- 完整测试：按 Peter 4:46 的新指示（只需针对性测试）**没有跑**。注意：从这一版起 Test.ps1 所有档默认带 HAR，下次跑完整测试时 HAR 会参与，旧检查若因 HAR 的补丁出问题会第一次在那时暴露；需要旧行为时加 `-NoHAR`。
- 已知旧不稳定项不受影响（炎魔火焰光环、巫妖测试启动卡住、泰坦神罚射程、塞伊战斗组生成、基因冷却再触发）。

## 没做到 / 遗留
- **深水游泳**：规格说“深水可游（复用水中行动组件）”，但 1.24.0 的“水中行动”本身就不改深水通行性；1.7 的寻路网格没有按人物开放深水的机制，要做得重写寻路，本版不做。蛇行只把泥地、沼泽、浅水的走路消耗减半。
- 副手武器真实贴图不做（按规格）；额外手臂自带固定弯刀剪影。
- 缠绕、剑刃风暴 `aiCanUse=false`：敌方蛇人不会主动放这两个能力（副手连击对敌人照常生效）。
- 身体附件的儿童版是成人版按比例缩小，只做了基本对齐检查（截图里儿童的尾巴和手臂位置正常但很小）。
- 没跑 `-HealthCompat`、`-VisualCompat` 档。

## 提醒 Peter
在模组列表启用 **Humanoid Alien Races**（创意工坊 839005762），放在 DND边缘大陆 **之前**，然后完全重启游戏。开局时在人物编辑的“角色种类”下拉框里选“六臂蛇人殖民者”；设置里“六臂蛇人”分组可调副手追加伤害。
