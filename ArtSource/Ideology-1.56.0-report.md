# DND边缘大陆 1.56.0「文化之神」实现报告

写于 2026-10-27。依据 [Ideology-1.56.0-spec.md](Ideology-1.56.0-spec.md)。开工时 About.xml = 1.55.0，备份在 `ArtSource/backup-1.55.0`（使用说明、About.xml、LoadFolders.xml、主 dll、Source 全部 .cs/.ps1）。

## 一、做了什么

| 部分 | 实际内容 |
|---|---|
| 框架 | `LoadFolders.xml` 在 VPE 和 HAR 之间加 `<li IfModActive="ludeon.rimworld.ideology">Bridges/Ideology</li>`。`Bridges/Ideology/` 只有 Defs / Patches / Textures；C# 在主程序集：`Ideo.cs`（扩展、IdeoBridge 接口、IdeoEvents、存档 IdeoState、同步、设置、派系重建、启动）、`IdeoPrecepts.cs`（IdeoMapCache、心情 worker、StatPart、神祇对立好感）、`IdeoRituals.cs`（目标过滤、品质 comp、庆典 / 葬礼结果、圣坛按钮、神迹布道消耗）、`IdeoTemple.cs`（神殿房间、神像）、`IdeoPatches.cs`（全部 Harmony）、`IdeoChecks.cs`。Build.ps1 没改。 |
| 入口 | `IdeoBridge.Active` = 文化启用且 `LP_Meme_Pantheon` 已加载（启动完成后算一次缓存）；`IdeoBridge.Linked` = Active 且设置「文化联动」开着。所有对外乘数没装文化时返回 1 / null。 |
| 模因 | 16 个（`Ideo_Memes.xml`）+ 16 个 `IdeoIconDef`（`LP_Icon_*`）+ `LP_IdeoDeityExtension`。 |
| 戒律 | 6 个新议题 + 沿用 1.13.0 的吞噬大脑议题；21 个新 PreceptDef、14 个情境心情、19 个记忆心情；6 个历史事件。 |
| 神眷联动 | 守护神锁定 / 文化改奉（-15、无冷却）、众神殿冷却减半、确信度漂移、显圣 ±5%、神选 +3%、祈祷乘确信度、改宗修正、信徒特性跟着改宗。 |
| 仪式 | 9 场庆典（固定日期、圣坛限定、品质因子、四档神眷、难忘奖励、错过 -5）+ 灰袍葬礼。 |
| 角色 | 神官 `LP_Role_Cleric` + 能力 `LP_Ability_DivinePreach`；大祭司改名；圣坛按钮：举办庆典（众神殿）、集体祈祷、求取神谕、血祭。 |
| 神殿与神 | 10 座神像、`LP_Temple`、神像每日神眷与心情、神赐遗物作为文化圣物（relicChance、换圣物、圣物柜 +0.5/天、丢失 -20）。 |
| 派系 | 7 个 DND 派系补模因（XML）和戒律（`LP_FactionIdeoExtension` + `IdeoGenerator.GenerateIdeo` 后缀）；设置按钮「重建 DND 派系文化」。 |
| 杂项 | 新生儿文化；设置左栏新分组「文化」（第 17 组）。 |
| 贴图 | `ArtSource/RaceArt/draw_ideo.py`：16 模因图标、16 文化图标、10 神像（各带 `_m` 遮罩，CutoutComplex 按材料染色）、3 按钮图标、6 议题图标、1 角色图标，共 62 张；预览 `ArtSource/Preview/Ideology-1.56.0.png`。 |

## 二、实际 defName

- 模因：`LP_Meme_Lathander / Tyr / Mystra / Kelemvor / Malar / Talos / Bane / Shar / Surtr / Ilsvi / Vlaakith / Pantheon / Defiant / Weave / Undeath / Ordning`。
- 议题：`LP_Issue_Undead / Giants / Arcane / Races / Apotheosis / Offerings`；吞噬大脑 = 已有的 `LP_BrainDevouring`。
- 戒律：`LP_Undead_Abhorrent / Wary / Acceptable / Sacred`，`LP_Giants_Feared / Respected / Supreme`，`LP_Arcane_Forbidden / Cautious / Revered / Supreme`，`LP_Races_Human / Many / Own`，`LP_Apotheosis_Blasphemy / Acceptable / Sacred`，`LP_Offerings_None / Quarterly / Blood`；吞噬大脑沿用 `LP_BrainDevouring_Abhorrent / Acceptable / Sacred`。
- 历史事件：`LP_HE_LichRevived / SoulDrained / ConstructRaised / SpellCast / DemigodRisen / GiantKilled`；吞噬大脑沿用 `LP_DevouredBrain`。
- 仪式：`LP_Festival_<神>`（PreceptDef）、`LP_FestivalPattern_<神>`、`LP_FestivalOutcome_<神>`、`LP_Target_FaithAltar`；`LP_Funeral_GreyRobe` / `LP_FuneralGreyRobe` / `LP_FuneralGreyRobeOutcome`。
- 角色：`LP_Role_Cleric`、`LP_Ability_DivinePreach`、`LP_NamerRoleCleric`。
- 状态：`LP_SworeOath`、`LP_WeaveNight`、`LP_GreatHunt`、`LP_SharShelter`、`LP_ForgeFire`。
- 神殿：`LP_Statue_<神>` ×9 + `LP_Statue_Pantheon`、`LP_Temple`；好感 `LP_Goodwill_DeityRivalry`。
- 心情：`LP_Thought_*`（情境）、`LP_Mem_*`（记忆），见 `Ideo_Precepts.xml` / `Ideo_Roles.xml` / `Ideo_Rituals.xml`。
- 开工前 grep 了规格里所有新名字（`LP_Meme_`、`LP_Issue_`、`LP_Role_Cleric`、`LP_Statue_`、`LP_Temple`、`LP_SworeOath`、`LP_WeaveNight`、`LP_GreatHunt`、`LP_SharShelter`、`LP_ForgeFire`、`LP_HE_`、`LP_Funeral_GreyRobe`、`LP_Offerings_` 等），**没有撞名**。唯一的重复是概念上的：1.13.0 已经有「吞噬大脑」议题（见决定 1）。

## 三、决定 / 偏离规格

1. **吞噬大脑不新建议题**：1.13.0 的 `Defs/MindFlayer.xml` 已有 `LP_BrainDevouring`（憎恶 / 可以接受 / 神圣）和历史事件 `LP_DevouredBrain`，再建 `LP_Issue_BrainEating` 会让文化里出现两个吞噬大脑议题。改为在 `Patches/Ideo_Vanilla.xml` 里给它补规格 3.5 的心情：可憎档自己 -10（新）、知道 -4 → -5；神圣档知道 +2（新）、殖民地有上古脑 +2（新）；自己 +6 原来就有。伊尔斯维模因必带 `LP_BrainDevouring_Sacred`。
2. **庆典和灰袍葬礼用 `MemeDef.requiredRituals`** 带出（原版给仪式戒律的专用字段），不放在 `requireOne` 里；亡灵观等普通戒律仍在 `requireOne`。
3. **NPC 专用模因**用原版字段 `hiddenInChooseMemes`，不需要 Harmony。
4. **Harmony**：文化补丁不用属性，`IdeoStartup`（StaticConstructorOnStartup，Def 加载完之后）在 Active 时用单独的 Harmony id `local.lich.phylactery.ideology` 打上，共 13 个（规格列的 + 儿童补文化）。`LichMod` 构造时 Def 还没加载，没法判断 Active，所以不在那里打。
5. **2500 tick 检查放在新的 `GameComponent` IdeoState**，不并进 `WorldComponent_Faith`（Faith.cs 只加挂点，改动更小）；`lastOfferingTick`、神谕、众神殿庆典计数、大祭司改名标记、创始族裔也存在 IdeoState。日结算按规格挂在 `WorldComponent_Faith.Daily` → `IdeoBridge.Daily()`。
6. **神名**：只对有神的结构（有神祇的文化基础）改；意识形态类结构（如塞伊、幽暗殖巢、拉洛克）本来没有神，不硬加。众神殿没有主神时放 3 位随机善 / 中立神。神的图标用 1.39.0 的神祇小图标。
7. **大猎 / 熔炉之火的祭品**：开始时用 `RitualOutcomeComp.BlockingIssues` 检查（不够不能开始，窗口里写原因），**庆典结束时**献上（原版没有现成的「开始时消耗」钩子，结束时扣更不容易丢东西）。
8. 庆典里「白天 / 夜里开始」按仪式开始的时刻算（当前时刻减去已进行的时间）。
9. **神官优先主持**：没有改原版 RitualRoleDef 的主持人候选；改为品质因子「有神官主持 +15%」（任何参与者是神官即可），灰袍葬礼也有这一项。
10. **集体祈祷**复用 1.39.0 的 `LP_Pray` 作业（`Faith.PrayJobFor`）；「这一次上限 ×1.5」做成按下后 6 小时内每日祈祷上限 ×1.5。
11. **血祭**简化：选一名囚犯后把他的囚犯互动设为原版「处决」并记下，囚犯死亡时（不论谁动手）守护神是恶神就 +15。没有做「带到圣坛 2 格内」（原版处决作业不支持指定地点）。
12. **神殿不算原版礼拜室**：原版礼拜室只影响冥想时对礼拜室的偏好（+100 评分），所以没有打补丁；神殿和礼拜室分开也能玩。
13. **新生儿**：1.7 原版 `Pawn_IdeoTracker.SetIdeo` 对婴儿直接 return（婴儿按接触过的文化，长成儿童时随机加入）。改为：出生时给婴儿一个月份量的母亲（→父亲→玩家主文化）文化接触；外加 `TryJoinIdeoFromExposures` 后缀，长成儿童后仍然没有文化的直接补上父母 / 玩家主文化。检查 15 验证的是接触。
14. **重建派系文化**：原版 `FactionIdeosTracker.IdeoGenerationParmsForFaction_BackCompatibility` 不带 forcedMemes，所以自己写了 `IdeoFactions.ParmsFor`（照 FactionGenerator 新建派系的参数）。旧文化加进派系的次要文化列表，留在世界里。
15. **凯兰沃教会**原来没有 forcedMemes，新建节点时加了 `Structure_TheistAbstract` 一起放进去：只给一个神祇模因会让原版生成神名时 `StructureMeme` 为空报错（`-Only Ascension` 里抓到的）。
16. 塞伊的影响力：原版对 forcedMemes 不按影响力上限裁剪，`Supremacist` 保留没删。
17. **施法事件和魔网至上冷却**只认模组自己的 `Ability_Miracle`（神术）、`LichPsycast`（巫妖法术）、`Ability_Divine`（半神能力）；施法事件另外也算原版灵能。**VPE 路径能力不记事件、不改冷却，VPE 桥没动**。卷轴法术走的是这些类或 VPE。
18. 神谕的「最想要的作为」是一张固定表（按 `Faith.Deed` 的理由关键词匹配，如提尔「审判仪式 / 释放了」），在 `Faith.Deed` 里判断，10 天内一次 +5。
19. 神像通行性改为 `PassThroughOnly`（`Impassable` + 可射过会报 ConfigError）。
20. 众神殿「举办庆典」：第一次为某位神办时把这位神的庆典戒律加进文化（原版仪式必须属于文化里的某个戒律），之后它每年按固定日期提醒；「错过 -5」只对文化主神的庆典生效。年度计数在庆典结算时记，按「取消」不占名额。
21. 5 个状态（立下誓言等）没画图标：hediff 在健康页不显示图标。
22. 「文化联动」关掉时历史事件也不再发出（戒律心情整体停用）。
23. 锻造类加成（苏尔特 +10%、熔炉之火 +25%）和 1.39.0 一样挂在锻造台上的「全局工作速度」（原版没有锻造速度属性）。
24. 自检进程：除 `-Only Ideology` 外，`IdeoStartup.TestSuppressed` 关掉联动、DND 模因随机权重为 0（和 `Faith.TestSuppressed` 同样的做法），免得随机的玩家文化抽到「每季献祭」等戒律改了信仰检查的数字。文化检查组里临时关掉原版「经典模式」（自测开局是经典模式，原版经典模式下不改确信度）。
25. 设置分组从 16 变成 17，`AdventurerChecks.cs` 里的数量断言改成 17。

## 四、没做到的

- 规格「不做」清单里的内容（加冕礼、卡苏斯之夜、龙 / 契约 / 深渊的文化内容、文化风格 / 服装等）。
- VPE 路径能力的施法事件和冷却（见决定 17）。
- 神官没有改原版仪式主持人候选（见决定 9）；血祭不要求带到圣坛（决定 11）；神殿不算原版礼拜室（决定 12）。
- 拿到第二件同名遗物（原版圣物任务）是否会重复：没有专门处理；1.39.0 自己的赐予 / 寻回路径照旧会转成神迹残片。原版圣物任务给的是文化圣物本身（同一件），不会重复生成。

## 五、测试结果

（构建：`pwsh Source\Build.ps1` 通过，主 dll + HAR / VPE / 神兽血裔桥。）

| 命令 | 结果 |
|---|---|
| `Test.ps1 -Only Ideology` | **PASS 15 条**，存读档通过，日志无红字 |
| `Test.ps1 -Only Faith` | **PASS 17 条** |
| `Test.ps1 -NoIdeology -Only Faith` | **PASS 17 条**，文化桥不加载、日志无红字 |
| `Test.ps1 -Only Ascension` | **PASS 15 条** |
| `Test.ps1 -Only Adventurer` | **PASS 123 条**（设置分组 17） |
| `Test.ps1 -Only Factions` | 130 条通过后停在已知的旧问题「combat group generates for LP_FaerunHostile … LP_WinterWolf」（1.48.1 起就有，和本版无关） |

修过的问题（开发中由测试抓到）：TraitRequirement 的 XML 写法（应为 `<LP_Devotee>4</LP_Devotee>`）、神像 ConfigError、凯兰沃教会缺结构模因导致原版报错、重建用的原版参数不带 forcedMemes。

## 六、游戏里还没看过的

- 选模因窗口里 16 个模因的排布、文化图标的染色效果；文化名字生成（中文 generalRules）读起来是否通顺。
- 文化基础里神的显示（名字、称号、图标）。
- 9 场庆典在日历上的提醒、仪式窗口里的品质因子与「不能开始」原因、庆典来信的附加文字。
- 灰袍葬礼替换原版葬礼后，殖民者死亡时的提示。
- 神官角色的指派、神迹布道的按钮与禁用原因；圣坛四个新按钮的外观。
- 神像的贴图（材料染色、遮罩）和 2×2 占位；神殿房间标签。
- 信徒特性随改宗变化（静默，没有信）。
- 「重建 DND 派系文化」按钮在真实旧存档（龙巢存档）上的效果。
- 性能：没有每 tick 逻辑（IdeoState / IdeoMapCache 各每 2500 tick 一次，心情 worker 只读缓存）；Peter 之后的 TPS 检查可以重点看 `ThoughtWorker_LPPrecept` / `ThoughtWorker_LPMeme`（每个殖民者原版约 250 tick 重算一次）和 `RoomRoleWorker_LPTemple`（房间角色重算时扫房间里的东西，和原版礼拜室一样）。

## 七、改动的共享文件（告诉协调线程）

| 文件 | 改动 |
|---|---|
| `Source/Faith.cs` | `CannotSetPatron` 开头加文化锁定；`WorldComponent_Faith.SetPatron(god, penalty=-1, cooldown=true)`（冷却乘 `IdeoBridge.PatronCooldownFactor`）；`PrayerReward` 乘 `PrayerFactor`、上限乘 `PrayerCapFactor`；`PrayerDone` 神殿心情；`MiracleCost` 乘 `MiracleCostFactor`；`Deed` 调 `OnDeed`（神谕）；提尔审判 / 释放、凯兰沃入葬乘 `DeedFactor`；`Daily` 调 `IdeoBridge.Daily()`；`Quarterly` 调 `OnManifest`；`CompleteChosen` 调 `OnChosen`；献祭乘 `OfferingFactor` 并记 `OfferingDone`。 |
| `Source/FaithMiracles.cs` | `Miracles.Taken` 与 `IdeoBridge.DefiantTaken` 取较小值。 |
| `Source/Core.cs` | `ExposeData` 加 `ExposeIdeo()`；`SettingsLeft` 在「成神」后加 `("文化",IdeoSettings.Draw)`。 |
| `Source/AdventurerChecks.cs` | 设置分组数 16 → 17。 |
| `Source/Demigod.cs` | 试炼升级后 `IdeoEvents.Record("LP_HE_DemigodRisen")`；`DailyGain` 加 `IdeoBridge.ExtraDevoteeGain`。 |
| `Source/NetherilScrolls.cs` | `Netheril.GiveXP` 乘 `ScrollXpFactor`。 |
| `Source/NecromancyGenes.cs` | 命匣复活成功后 `LP_HE_LichRevived`。 |
| `Source/Gameplay.cs` | `Constructs.Create` 末尾 `LP_HE_ConstructRaised`；汲取灵魂工作单 `LP_HE_SoulDrained`。 |
| `LoadFolders.xml` | 加 `Bridges/Ideology`。 |
| `Source/Test.ps1` | 新开关 `-NoIdeology`。 |
| VPE 桥 | **没动**。 |
| `About/About.xml`、`使用说明.txt` | 1.56.0 版本号、简介、更新日志、新章节。 |

## 八、提醒 Peter

退出并重启游戏后生效。旧存档要在设置「文化」分组里点一下「重建 DND 派系文化」才能看到派系的新模因；龙巢存档用这个按钮即可，用完不需要再启用那个一次性模组 `LongChaoIdeoRebuild`。
