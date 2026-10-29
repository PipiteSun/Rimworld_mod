# DND边缘大陆 1.37.0 报告：耐瑟瑞尔卷轴任务线

需求：ArtSource/Netheril-1.37.0-spec.md。备份：ArtSource/backup-1.36.0（Source、Defs、Patches、About、使用说明.txt、Bridges/VPE 的 Source/Defs/Patches/Assemblies、Bridges/Isekai/Patches、1.7/Assemblies 主 dll）。

## 测试结果

| 命令 | 结果 |
|---|---|
| `.\Test.ps1 -Only Netheril` | PASS：25 条（24 条存档前 + 1 条存读档往返；日志无红字） |
| `.\Test.ps1 -Only ResearchTab` | PASS：6 条（新研究在 DND边缘大陆 页签、不重叠、前置在左） |
| `.\Test.ps1 -VPE` | PASS：434 条（原有职业桥接检查全过，另加 3 条耐瑟 VPE 检查） |

完整套没跑（需求里是可选）。另外临时写过一个冒烟组，生成了三个站点地图（营地 / 古墓 / 浮城残骸），又在家园地图跑了亡灵袭击、使者、匣柩和塞伊来抢，全程没有报错。跑完已经删掉，没有留在源码里。

-Only Netheril 的 25 条和需求第 8 节一一对应（8 和 17 的存读档部分合成 AfterLoad 里的一条）：
1 三种卷轴是原版书、不消耗、卡苏斯不可交易、品质倍率 0.6～1.6；2 没有心灵链接 → “需要心灵链接”，智识 4 → 智识原因；3 研习卷 1 小时熵 +8、巫妖 +4；4 经验去向正确、精神力不变；5 研习度过 100 → 心灵链接 +1；6 每日 40（30+10+0）、换日清零、到上限只涨熵；7 熵 >90% 停读，玩家强制阅读不停；9 残卷 24 点学会惑控学派、等级合规的法术，心灵链接等级不变；10 学满 2 个后只给余效；11 预言学派在心灵链接 1 时停在 24，升到 6 级后学会；12 卡苏斯之卷唯一（第二次生成被拒，多出来的那卷消失）；13 读完一遍得一层回响（×1.05 / +5% / +2%）；14 使者触发两分支；15 选项 A；16 选项 B（命匣离图、LP_PhylacteryLost、杀死后不复活、尸体销毁也不进命匣）；18 差事超时 → 拒绝 +1；19 第三次拒绝 → 古墓站点；20 祭坛三项条件；21 成功率公式和上下限；22 成功：hediff、熵上限 ×2、不需进食、神谕、ArcaneDemigodGranted 触发、已解读、不能再办第二次；23 失败：石像不可复原、魔网崩溃下原版心灵法术和法杖都用不了、卷轴遗失、遍数保留；24 卷轴被毁 → 待重现 → 浮城残骸站点；25 有卷轴时塞伊袭击权重 ×1.25；8+17 存读档后残卷学派、名字、读者进度保留，任务停在差事二，期限和营地站点都在。

VPE 的 3 条（Bridges/VPE/Source/NetherilVPE.cs）：钩子已登记，LP_Path_* 不进任何学派；研习卷 1 小时 VPE 经验 +6（100 → 106），精神力不变；残卷学会 1 级塑能法术（Bolt），路径点不变，也没有解锁路径。

## VPE 路径 → 学派（以测试环境里实际加载的 defName 为准）

| 学派 | VPE 路径 |
|---|---|
| 塑能 Evocation | VPE_Conflagrator、VPE_Staticlord、VPE_Frostshaper |
| 防护 Abjuration | VPE_Protector |
| 惑控 Enchantment | VPE_Puppeteer、VPE_Warlord、VPE_Empath |
| 幻术 Illusion | VPE_Nightstalker |
| 预言 Divination | VPE_Chronopath、VPE_Harmonist |
| 咒法 Conjuration | VPE_Skipmaster、VPE_Archotechist |
| 变化 Transmutation | VPE_Wildspeaker、VPE_Technomancer，以及所有表外路径（测试环境里是 VPE_Archon；VPE_Animat 和其他模组的路径加载了也归这里） |
| 死灵 Necromancy | VPE_Necropath |
| 排除 | LP_Path_Fighter/Wizard/Cleric/Artificer/Druid/Paladin/Sorcerer/Monk |

## 无 VPE 时原版心灵法术的归类（括号里是法术等级）

| 学派 | 原版法术 |
|---|---|
| 塑能 | 恒星狭缝 SolarPinhole(1)、闪电风暴 Flashstorm(5) |
| 防护 | 熵值倾泻 EntropyDump(2)、子弹护盾 BulletShield(6)、熵值链接 EntropyLink |
| 惑控 | 眩晕 Stun(1)、负担 Burden(1)、召唤 Beckon(3)、眩晕脉冲 VertigoPulse(3)、狂暴 Berserk(5)、狂暴脉冲 BerserkPulse(6)、猎杀脉冲 ManhunterPulse(6)，以及所有 WordOf* 话语类法术 |
| 幻术 | 致盲脉冲 BlindingPulse(2)、烟雾弹 Smokepop(4)、隐身 Invisibility(5) |
| 预言 | 专注 Focus(4)、远跃 Farskip(5) |
| 咒法 | 碎块跳跃 Chunkskip(1)、水跃 Waterskip(2)、混沌跳跃 ChaosSkip(3)、跳跃 Skip(4)、筑墙 Wallraise(4)、群体混沌跳跃 MassChaosSkip(6) |
| 死灵 | 痛觉阻断 Painblock(1)、神经震荡 Neuroquake(6) |
| 变化 | 表外的所有心灵法术（其他 DLC 或模组加的） |

原版没有远视、加速或治疗类的心灵法术，所以预言学派只剩专注和远跃，变化学派没装别的模组时是空的（读到 24 点会一直提示“暂时没有你能理解的东西”）。死灵学派在原版里没有对应法术，这里放的是痛觉阻断和神经震荡。

## 仪式框架

没用 Ideology 的原版仪式框架，做的是祭坛按钮 + 选人对话框（Dialog_NetherilAscension）+ 确认框（显示成功率）+ 自定义 LordJob（LordJob_NetherilAscension / LordToil_NetherilChant / DutyDef LP_NetherilChant / JobDef LP_NetherilChant）。这样不装 Ideology 也能用。现有的巫妖仪式是死灵仪式台上的工作单，没法套进“多人 6 小时、随时可能中断”的流程，所以没有复用。主祭倒下或离开、协祭不足 3 人、祭坛没了，仪式都会中断：卷轴完好，所有人精神力清零。

## 研究前置

LP_Research_NetherilArcana 耐瑟瑞尔奥术（1500）的前置是 **LP_ArcaneStaves 奥术法杖**（本页签里最贴近“法术”的一项，它又以 DND炼金术为前置）。位置在 X 2 / Y 0，在奥术法杖右边。

## 和需求不同的地方

1. **Isekai 宝箱**：宝箱掉落本来就写在 C#（Source/ItemsIsekai.cs 按类型名挂 TreasureChest.Award），Bridges/Isekai 里只有商人补丁。所以研习卷 10%、3 级以上秘术残卷 5% 也写在 ItemsIsekai.cs，用按宝箱编号播种的独立随机数，不影响原来神灯的概率。
2. **设置位置**：“事件”分组实际在设置页右栏，两个开关放在这个分组的末尾，不是左栏。
3. **半巫妖 / 拉洛克的法术**：现有的恐惧凝视、死灵冲击是巫妖专用的 Psycast（aiCanUse=false，要绑定命匣），NPC 没法用。所以半巫妖之躯（LP_NetherilUndead）和化身（LP_LarlochAvatar）自带一套简单 AI：附近有敌人时自己放恐惧凝视（4.9 格，冷却 30 秒）和死灵冲击（3.9 格，15×1.5 伤害，化身 ×2）；化身另有生命汲取光环。效果复用 LichSpells 的代码。原有代码里没有“塞伊 NPC 巫妖首领”，造型是新做的（灰白皮肤、光头、暗紫长袍）。
4. **以命匣换卷**：命匣不销毁，而是从地图上拿走，存进任务状态里的“拉洛克的库房”。这样巫妖的等级、法术和精神力上限都照旧（Hediff_Lich.Active 仍为真）。复活靠 Harmony 拦掉 ProcessRevival / ScheduleRevival，尸体被毁时的灵魂收容也跳过（改了 Core.cs 一行）。献祭不可用，因为仪式台找不到 12 格内的命匣。
5. **“已解读”**是单独的标记（deciphered），不占卷轴的下落状态。下落状态有 none / larloch（在古墓里）/ site（在浮城残骸里）/ player / lost 五种。已解读的卷轴之后被毁，照样会重现（可以继续拿回响），但不会再有仪式。古墓地图收起时卷轴跟着没了，这不算“遗失”：下次进古墓会重新放一卷。
6. **使者等一天没回复、被打死或被赶走**都算一次拒绝；重现时的使者不回复，算“去古墓硬抢”。
7. **差事三**：二号差事一完成，匣柩立刻送到（离殖民地中心 14 格以内），6 小时后塞伊来抢（LordJob_AssaultThings 直扑匣柩），24 小时后结算。没有塞伊远征军（设置里关了）时，差事二自动算完成；浮城残骸改由随机敌对派系占着。
8. **塞伊精英法师**用的是现有的 LP_FH_RedWizard 加上 hediff LP_ThayEliteMage（受伤 ×0.6），名字中间插“精英法师”，没有新开 PawnKind。
9. **奥术半神的不老和不需进食**：不老是 Harmony 把成年后的生理衰老速度设为 0，不需进食是去掉饥饿需求；精神力回复 ×2 落在原版的“冥想精神力增长”属性上（MeditationFocusGain）。
10. **卡苏斯的妄念**接到原版思维树 MentalStateNonCritical（Patches/Netheril.xml）：能读卡苏斯之卷就去读，不能就在耐瑟祭坛 3～8 格内游荡，再不行就在殖民地里游荡。它只会在读完第 3、5 遍时被触发，不进随机崩溃池。
11. **耐瑟祭坛的冥想焦点**用原版的“艺术”和“庄严”（需要 Royalty；没有 Royalty 时这部分不加载）。
12. **秘术残卷的色签**是给每个学派各画一张贴图（LP_NetherilArcaneScroll_<学派>），由 Book_Netheril.Graphic 按学派选，不是运行时染色。
13. **卷轴不进原版随机书池**（pickWeight 0、thingSetMakerTags 清空），只从需求列的那几个渠道来。
14. 古墓里放卷轴的“石柜”用的是原版小架子（ShelfSmall，石材）。
15. **死魔区不存在**：任何地方都没有相关逻辑。

## 留给以后的接口

- `NetherilAscension.IsArcaneDemigod(Pawn)`：公开静态方法。
- `NetherilAscension.ArcaneDemigodGranted`：`public static event Action<Pawn>`，授予奥术半神时触发（订阅者抛异常会被记日志、不影响授予）。
- 魔网神选敌视、疫斑者（魔网崩溃时不刷疫斑者）都没做，以后要做的话可以在 Succeed / Fail 里接上。
- `NetherilHooks`（VPE 等级 / 经验 / 法术池 / 学法术四个委托）、`NetherilAscension.CooldownFactor(Pawn)`、`NetherilAscension.WeaveCollapsed(Map)`，供其他桥接用。
- 死魔区不做。

## 最终 defName 列表

| 类别 | defName | 中文名 |
|---|---|---|
| ThingDef（书） | LP_NetherilStudyScroll | 耐瑟瑞尔研习卷（1500 银） |
| ThingDef（书） | LP_NetherilArcaneScroll | 耐瑟瑞尔秘术残卷（3000 银） |
| ThingDef（书） | LP_KarsusScroll | 卡苏斯之卷（8000，不能交易） |
| ThingDef（建筑） | LP_NetherilAltar | 耐瑟祭坛（3×3，金 100 + 秘银 LP_Mithral 50 + 玻璃钢 50） |
| ThingDef（建筑） | LP_LarlochOfferingBox | 拉洛克的祭箱 |
| ThingDef（建筑） | LP_PhylacteryCasket | 命匣匣柩（生命 800） |
| HediffDef | **LP_ArcaneDemigod** | 奥术半神 |
| HediffDef | **LP_KarsusEcho** | 卡苏斯的回响 |
| HediffDef | **LP_PhylacteryLost** | 失匣巫妖 |
| HediffDef | LP_NetherilUndead | 半巫妖之躯 |
| HediffDef | LP_LarlochAvatar | 影巫王的化身 |
| HediffDef | LP_ThayEliteMage | 塞伊精英法师 |
| MentalStateDef | LP_KarsusObsession | 卡苏斯的妄念 |
| AbilityDef | LP_Oracle | 神谕 |
| FactionDef | LP_Larloch | 影巫王拉洛克的扈从（隐藏） |
| PawnKindDef | LP_LarlochEnvoy、LP_LarlochDemilich、LP_Larloch、LP_LarlochSkeleton | 拉洛克的使者、半巫妖、影巫王拉洛克、拉洛克的骷髅 |
| IncidentDef | LP_LarlochEnvoy | 拉洛克的使者（baseChance 0，由任务状态触发） |
| LetterDef | LP_LarlochChoiceLetter | 拉洛克的邀约 |
| SitePartDef / GenStepDef | LP_RedWizardCamp、LP_LarlochCrypt、LP_NetherilWreck | 红袍法师营地、拉洛克的古墓、沉没的耐瑟浮城残骸 |
| GameConditionDef | LP_WeaveTremor、LP_WeaveCollapse | 魔网震颤、魔网崩溃 |
| ThoughtDef | LP_CompanionSentAway、LP_WitnessedAscension、LP_AscensionFailed | 同伴被送走 -5/10 天、见证升神 +5/15 天、升神失败 -8/10 天 |
| ResearchProjectDef | LP_Research_NetherilArcana | 耐瑟瑞尔奥术（1500，前置奥术法杖） |
| DutyDef / JobDef | LP_NetherilChant | 升神吟诵 |
| RulePackDef | LP_Namer_NetherilStudy、LP_Namer_NetherilArcane、LP_Namer_Karsus、LP_Description_Netheril | 卷轴书名与描述 |
| 设置键 | lpNetherilQuest、lpLarlochRaids | 耐瑟瑞尔任务线、拉洛克亡灵袭击 |

## 文件

- 新 Defs：Defs/Netheril_Scrolls.xml、Defs/Netheril_Quest.xml；研究写进 Defs/Research.xml；新补丁 Patches/Netheril.xml（黑网库存、妄念思维树）。
- 新源码：Source/NetherilScrolls.cs、NetherilQuest.cs、NetherilAscension.cs、NetherilChecks.cs；Bridges/VPE/Source/NetherilVPE.cs（桥接没有新 Defs）。
- 小改：Core.cs（失匣巫妖不收灵魂）、ItemsScrolls.cs（石像 permanent）、Guns.cs（魔网崩溃时法杖施放不出来）、PortalEvent.cs（设置存取和绘制）、TowerCrash.cs（拆残骸 15% 研习卷）、ItemsIsekai.cs（宝箱卷轴）、Bridges/VPE/Source/BridgeChecks.cs（调用耐瑟检查；路径 8 条、能力 72 个都没变）。
- 贴图：ArtSource/RaceArt/draw_events_128.py 追加 `1.37.0` 分支（`python -I ArtSource/RaceArt/draw_events_128.py <模组根目录> 1.37.0`）：研习卷、秘术残卷底图 + 8 张学派色签、卡苏斯之卷、耐瑟祭坛（384px）、奥术半神光环（north/south/east）、古墓和浮城残骸的站点图标。
- About.xml 1.37.0，描述补了一句；使用说明.txt 新增【1.37.0 耐瑟瑞尔卷轴】一节和 1.37.0 更新日志。

DLL 已经重新编译（主程序集 + HAR / VPE / 神兽血裔桥接）。**要完全重启游戏才会生效。**
