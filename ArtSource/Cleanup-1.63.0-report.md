# DND边缘大陆 1.63.0 清理版本 — 实现报告

规格：`ArtSource/Cleanup-1.63.0-spec.md`。备份：`ArtSource/backup-1.62.0`（开工时模组根目录除 ArtSource 外全部，含旧 `1.7/Assemblies/LichPhylactery.dll`，2825 个文件）。
备注：`Textures/LP/Exalted/Con.png` 文件名是 Windows 保留名，普通 Copy-Item 复制不了，备份里缺这一张（backup-1.61.0 里有，原图未动）。

About.xml 已改为 1.63.0 + 描述一段；使用说明.txt：更新日志加一行 1.63.0（锚点“三、更新日志”标题 + 1.62.0 行，插入后核对只出现 1 次，总行数 1902 → 1903），另外改了两处过时说明（法术抗体“代价”、狗头人刺坑），各自锚点核对出现 1 次。

## 一、警报（新文件 `Source/Cleanup163.cs`）

- 设置：“命匣等”分组最上面加“DND 警报”开关（`Settings.dndAlerts`，默认开，新字段有默认值，老存档 / 老设置照常读）。
- 基类 `Alert_LP`：结果缓存 60 刻；目标用 `AlertReport.CulpritsAre(List<GlobalTargetInfo>)`，点击跳目标；标签带数量。
- 巨魔尸体将会复活（High）：所有地图上 `Troll.Gene(尸体).reviveTick>=0` 的尸体（敌我都算；复活开关关掉时不显示；次数上限在复活规则里已经决定是否排期）。列出每具剩余小时数，说明“用火烧掉或火化”。
- 蝌蚪化形中：殖民者 / 囚犯 / 奴隶有 `Hediff_Tadpole`；显示进度、剩余天数（或“已被抑灵剂冻结”）、阻止办法（取出蝌蚪手术、抑灵剂）。
- 龙蛋即将孵化：`CompDragonEgg` 剩余 ≤3 天（按当前宝库孵化速度算；不在宝库里不孵化 = 不提示）。设备上只有力能龙蛋用这个组件。
- 巫妖复生中：本方命匣（主人是本方）且主人已死、`revivalTick>=0`；显示剩余时间 / 已就绪和命匣所在地图；跳命匣。
- 命匣不在殖民地（High）：被非本方的人拿着；在商队里（跳商队）；在别的地图；在家园区外。被拉洛克收走的（耐瑟任务）不算。不在任何地图也不在商队的（如运输舱飞行中）没有可跳目标，不显示。
- 脑髓不足：本方夺心魔 `ValuePercent < 20%`。

1.1 巨魔尸体过滤与“标记焚烧”（`Defs/Cleanup.xml` + `Cleanup163.cs`）
- 两个 SpecialThingFilterDef：「巨魔尸体」(LP_AllowTrollCorpses) 和「非巨魔尸体」(LP_AllowNonTrollCorpses)，判断都是 `CleanupRules.IsTrollCorpse` = 尸体内的小人有激活的巨魔再生基因（复活规则也按这个基因判断）。
  **偏差**：单独一个「巨魔尸体」过滤器在原版里只能“排除巨魔”，做不到“只烧巨魔”，所以另加了「非巨魔尸体」——取消勾选它就只烧巨魔。
- 选中单个巨魔尸体有按钮“标记焚烧”：解除禁用，在最近的火葬场（任何带“火化尸体”配方的本方工作台）最前面放一张“不收非巨魔尸体”的火化账单（已有就挪到最前，不重复加），由原版搬运 / 火化工作去拿；没有火葬场时只提示。没有新加工作类型（改动小的做法）。

## 二、已知错误

1. 耐瑟 #22（×2.4）：根因——奥术半神同时会被授予半神层 `LP_Demigod`（心灵敏感度 +20%），原版心灵熵上限的 statFactors 里有心灵敏感度，于是 2 × 1.2 = 2.4。修法：新 StatPart `StatPart_ArcaneEntropyOnce`（NetherilAscension.cs）在心灵熵上限里把半神层那部分敏感度除掉，只乘一次奥术 ×2。半神层敏感度对其他属性不变。
2. 耐瑟 #31：本轮开工时 `-Only Netheril` 只停在 #22；#22 修好后 36 项全过，#31 没有再失败（见“测试”）。没有找到需要改的逻辑。
3. Events #86/#87（迁徙）：检查过时。1.34.1 起迁徙队员挂在隐藏派系“巨人游群”下，好感记在原派系（`GiantGroup.Origin`）。改检查：队员派系 = 游群、`Origin` = 余烬 / 同盟；食人魔家族、泰坦队、“送食物好感 +12”都改看 `Origin`。逻辑没改。
4. 完整测试停在 #925（半兽人剧本袭击准时到达）：根因——测试殖民地财富很低，剧本袭击点数 = max(威胁 × 0.6, 派系最低点数 × 1.05) 只有 23～35；塞伊最低点数 21，但原版“单人花费上限曲线”在这么低的点数下连最便宜的兵也不让出，一个人都生成不出来，`TryExecute` 失败（没有报错），袭击就悄悄没了（真实游戏里小殖民地第 6 天同样会发生）。修法（`Scenarios.cs` ScheduledRaid.Fire）：落地前用 `GeneratePawnKindsExample` 试生成（独立随机数），生成不出来就把点数 ×1.35 逐步加，最多 12 次；没指定到达方式时，失败还会改用边缘走入、中心空投再试；最后仍失败写一条 Warning。`ScheduledRaid.Points`（检查核对的“0.6 倍威胁”）不变。
5. SharedComponents #42 / “bridge xenotype colonist halves relation mood”：检查过时。1.57.0 起两族之桥只减半负面心情，检查却在心情已转为正面阶段之后测倍率。改为：正面阶段倍率不变、把同处时间清零回到负面阶段后 ×0.5。另外 `-Only SharedComponents` 读档后检查找不到夹具（准备函数叫 PrepareSharedSave，-Only 找的是 PrepareSharedComponentsSave），加了同名转发。
6. 夺心魔奴仆没有 LP_Thralled：完整测试 Factions 里那条“illithid thralls carry LP_Thralled …”失败的实际原因是夺心魔兵种偶尔被生成成普通人（原版挑异种型时把派系和兵种的表合起来算），检查报 flayer-not-flayer。修法（**真 bug**，Factions.cs `FactionGroups.FixFlayers`）：殖巢战斗组里不是夺心魔的夺心魔兵种，换成强制夺心魔异种型重新生成的一个。之后 -Only Factions 326 项通过。
7. KnowledgeFeedback / ConstructDesign 不能 -Only：这两组用完整检查里的巫妖（worker）和他的骷髅、魔像、四个知识亡灵。`OnlyChecks.cs` 加 `LichWorkerFixture()`：-Only 这两组时照完整检查的仪式账单做一份（固定种子）。ConstructDesign 另外在装 HAR 时崩：旧种族迁移检查把小人的 def 换成旧种族再生成，HAR 的 AlienComp 强转 `ThingDef_AlienRace` 失败；检查里模拟旧种族前先拿掉 HAR 组件、升级回人类后放回。
   KnowledgeFeedback 还暴露了一个**真 bug**（不是检查问题）：`Exalted.cs` 举一反三的后缀用 `__state` 存“学习前等级”，-1 表示不触发；知识亡灵的学习前缀（Priority.First）返回 false 时 Harmony 跳过这个前缀，`__state` 留在默认 0，被当成“从 0 级学起”，满级知识亡灵每学一次就对 0～19 级各举一反三一次，经验连锁传给主人。改为存“等级 + 1”，0 = 不触发。
8. Legendary #7 传奇疼痛休克：根因——传奇单位固定体质 25，自带超凡体质“硬撑”，它也拦倒地（而且不看移动能力），和传奇抗性谁先拦取决于补丁顺序。检查里先把当天的硬撑用掉，只测传奇抗性；生成用固定种子；割伤只落在不影响移动、不致命的外部部位（原来会割掉脚 / 脚趾，移动为 0 时按规则照常倒地、不扣次数）。

## 三、不稳定检查（做了的）

| 检查 | 根因 | 修法 |
|---|---|---|
| 完整测试 Factions 塞伊战斗组（LP_WinterWolf 变体） | 塞伊的霜巨人掠袭者会带冬狼（FrostGiant.cs 本来就这样），检查只认人形 | 允许冬狼 |
| Events 维拉基斯化身（突袭找不到目标 / 倒在浅水灰放不下）、拉山德光柱 | 化身放在随机空地（可能是水、树林），殖民者随机 2～8 格 | 新 `DryOpenCell(10)`：10 格内无建筑无人不起雾，水改土、树砍掉；殖民者固定东 2 / 东 5 格 |
| Events “只有巨灵能命令裂口” | 随机开局里第一个殖民者偶尔本身就是巨灵 / 六臂蛇人 | 挑一个不能命令的殖民者 |
| Events 迁徙扎营“篝火和睡袋” | **真 bug**：睡袋占 1×2，只查了一格，转向后另一格压在篝火上，生成时把篝火抹掉 | `GiantMigration.OnCamp` 先定朝向，整块都空才放 |
| 完整测试 Guns “符文铁砧配方只有一个工作台” | 契约的地狱铸炉共享符文铁砧所有配方，检查过时 | 允许地狱铸炉 |
| Factions 费伦同盟种族比例 | 约 100 人的随机样本上下浮动；1.57.0 起亚种（山地矮人、木精灵、半精灵、半身人、侏儒）分走份额，检查只认 LP_Dwarf / LP_Elf | 整组战斗组生成用固定种子；矮人 / 精灵按族系算；人类下限 25% → 20% |
| Factions 塞伊兵种白名单 | 漏了 HAR 的双头巨人蛮兵和被奴役的风暴巨人 | 补上 |
| -Only Factions / Scenarios 读档检查 | 准备函数名和组名不一致（PrepareFactionSave / PrepareScenarioSave），-Only 找不到夹具 | 加同名转发 |
| Items 次元袋 +60 | 搬运量乘操作能力，随机的人偶尔带慢性病，+60 变 +58.8 | 量之前去掉负面健康状态 |
| Giants 风暴巨人挨雷击 | 前面检查留下的爆炸也被算进“这次雷击的爆炸” | 只看雷击后新出现的爆炸 |
| ChromaticDragons 吐息形状 | Peter 的偏好里开了开发者选项“测试用小地图”（testMapSizes），Test.ps1 照抄偏好，地图变小找不到 30×9 的空地 | Test.ps1 复制偏好后固定关掉 testMapSizes |
| 开发者模式下的定义检查报错（Peter 开了开发者模式后 Test.ps1 因日志红字失败） | 龙 / 传奇怪兵种缺 initialWillRange；眼魔 / 半巫妖身体没有会冻伤的部位；传奇怪动作在定义检查之后才登记；魔法物品状态的阶段 minSeverity 全是 0；耐瑟冥想式阅读的娱乐类型 | 补意志范围（10）；眼柄 / 漂浮囊加冻伤易感；动作放进初始表；`Patches/Cleanup.xml` 补递增 minSeverity（阶段仍由代码选）；`JoyGiverDef_CrossKind` 只跳过那一条检查 |
| CommonRaces 狗头人刺坑“一次性” | 本版改成 3 次（规格 4.4） | 检查跟着改 |
| -Isekai 宝箱掉落 | 典籍、神灯用全局随机数 | 也按宝箱编号播种（**还没跑 -Isekai 验证**） |
| 知识回馈 | 见二.7 | — |
| 传奇疼痛休克 | 见二.8 | — |

## 追加：敌人等级开关合并（协调者 20:50 加进本版）

- 问题：「敌人冒险者等级」和「敌人可达传奇等级」各存两份（`adventurerEnemyLevels` / `adventurerEnemyLevelsCoexist`，`adventurerEnemyLegend` / `adventurerEnemyLegendCoexist`），按是否 Isekai 共存选一份。Peter 平时那份是关的，所以所有敌人都是 1 级。
- 改动：两对字段各合并成一个（保留 `adventurerEnemyLevels`、`adventurerEnemyLegend`，删掉两个 Coexist 字段），默认开。`AdventurerEnemies.OnFor` / `Legend.EnemyOnFor` 不再按模式区分（参数保留）；`Process`、`LevelLoose`、`Story` 都经过 `AdventurerEnemies.On`，自动用合并后的开关。设置页各只有一个勾选框，说明文字和使用说明两处同步。
- 存档迁移（`Settings.ExposeMergedToggle`，Cleanup163.cs）：新键 `lpEnemyLevels163` / `lpEnemyLegend163` 存 1 / 0。读到只有旧键、还没有新键的老设置时，**旧设置一律重置为开启**（两个开关都开，不看旧值——协调者 20:53 改的规则：共存那份旧默认是关，按“任一份开”合并 Peter 仍会是 1 级）。之后按新键保存 / 读取。旧键不读不写，留在老设置文件里也照常加载不报错。
- 改了的检查：AdventurerChecks2（默认值：两种模式都开）、AdventurerChecks4（传奇默认值、不再保存 / 还原 Coexist 字段）。ExaltedChecks 里用旧键 `adventurerEnemyLegendCoexist` 的老设置加载测试不受影响。
- -Only Cleanup 新增第 19 项（两对都测）：老设置只有一份开 → 开；两份都关 → 也重置为开；有新键时按新键（0 → 关、1 → 开）；新设置默认两种模式都开。

## 追加：五色龙显示变大

`ChromaticDragons.cs` 的 `Dragons.RenderScale`（幼龙 / 成年 / 远古）由 2.0 / 2.8 / 3.8 改为 2.6 / 3.8 / 5.2，只影响画面大小，不改体型和数值。龙的贴图由另一个线程负责，没有动。

## 四、遗留小项

做了：
- 4.1 白龙：冰 / 雪 / 冰川地形（defName 含 Ice/Snow/Glacier）或有积雪的格子，移动消耗只算基础步长（`Pawn_PathFollower.CostToMoveIntoCell` 后缀）。绿龙 / 黑龙：浅水、沼泽、泥地同样按基础步长，寻路的按角色水格成本为 0；在水里被远程命中 ×0.7（目标体型系数）。深水原版不可通行，没改通行性。黑龙巢穴：`GenStep_DragonLair.Build` 后缀，黑龙巢穴那张地图注册永久日食（只在该地图的条件管理器里）。黑龙在黑暗中远程命中 ×1.1（射手系数，上限 1）、近战命中 +10%（上限 100%）。
  **没做**：白龙“不因寒冷减速”——白龙本来不会失温（设备上没有找到寒冷减速的来源），没有另写。
- 4.3 影魔：暗处被远程命中 ×0.8。魅魔 / 梦魔：来访的伪装魅魔被识破（含新加的“倒地时现形”）时，12 格内的殖民者得到「被欺骗」（社交记忆，心情 -5、好感 -10，5 天）。
- 4.4 狗头人刺坑：新类 `Building_KoboldSpikePit`：利刃（Stab）20、穿甲 15%、体型 >1.5 ×0.5、狗头人建的 ×1.3 照旧；触发 3 次后坑塌掉（销毁），要重新布置（原版自动重建的蓝图逻辑不在这条路径上）。`trapDestroyOnSpring` 改为 false，统计值显示 20。1.57.0 报告没有给出具体数值建议，按规格默认值。熊地精“突袭只在近战生效”1.57.0 已经如此，未改。
- 4.5 法术抗体：只抵抗敌对施法者——原来“友方给的增益 50% 落空”的代价删掉；敌对施法者给的状态仍 50% 无效。特性描述和使用说明同步。遗迹 / 古代宝箱（原版 `Building_Crate.Open`）：同调者在场（同地图本方殖民者）15% 多一件魔法饰品（带 AccessorySlot 的物品）；5% 一本属性典籍；按箱子编号播种。
- 4.2 眼柄：眼魔掉 2～4 根眼柄（设备上已有 `LP_BeholderStalk`“眼魔眼柄”，没另加 LP_BeholderEyestalk）；卷轴书桌上 6 个“抄写 X 卷轴”配方启动时各生成一个“（眼柄）”版本：最后一项主要魔法材料减半（向上取整，至少 1）+ 1 根眼柄。

第二批（Peter 22:11 让先停下；已做完、自检通过的保留，**都还没进游戏试玩**）：
- 半巫妖谈判：本方巫妖 10 格内有没受过伤的半巫妖时出现按钮“与半巫妖谈判（成功率 X%）”，50% + 社交×3% + 冒险者等级×1%，最多 90%；成功它离开地图（进世界角色），留下 1 份秘术残卷或 1 块神迹残片（按半巫妖编号播种，各一半）；失败它立刻做一次自己能做的传奇动作。每个半巫妖一次（`Hediff_MonsterBody.parleyed`，新存档字段有默认值）。
- 吞眼魔脑髓：夺心魔吞噬眼魔（活体或新鲜尸体，走原来的吞噬）后脑髓补满，得到 10 天「眼魔之识」（心灵敏感度 +20%，所有能力冷却 ×0.8）。
- 拉洛克情报：使者信件多“购买情报（1500 白银）”，从家园区的白银里扣；按殖民地最高冒险者等级给（眼魔巢穴 ≥16、无尽墓穴 ≥24、龙巢 ≥远古龙等级，和原来商人卖情报的门槛一样）；已经有显露的同类地点就指给你，没有就按原来的方式生成；每次来访一次（`NetherilQuestState.intelEnvoyId`，默认 -1）。
- 纳巴苏：自动选攻击目标时优先 30 格内落单的（5 格内没有同派系未倒地的人）；石化凝视在附近有落单目标时只凝视落单的。
- 敌方卓尔黑暗术：能力改为 AI 可用；只对目标格 3 格内至少 3 个它的敌人的地方放；原来的冷却 1 天，所以每人每战最多一次。玩家的卓尔不会自动放。

暂未做（列为待办）：
- 伪装魅魔的社交想法：查了所有 ThoughtDef，没有针对魅魔的“恶魔类负面社交想法”（唯一相关的圣徽想法本来就在伪装时关闭），这一项现在无需改动；以后若加了这类想法再处理。
- 影魔半透明（渲染）。
- 地精劫掠者袭击 25% 以边缘埋伏方式到达。
- 规格第五节全部（照规格不做）。

## 测试

Peter 21:59 说不用再跑完整测试和 -VPE / -Isekai / -NoHAR 全套，之后只跑指定的 -Only 组。最终 DLL：`1.7/Assemblies/LichPhylactery.dll`（2026-10-28 22:17 编译，含全部改动；HAR / VPE / 神兽血裔联动也重新编译）。

新检查组 `Test.ps1 -Only Cleanup`：25 项（6 个警报、巨魔过滤、标记焚烧、白龙冰雪、黑龙沼泽 / 水中、黑龙巢穴日食 + 黑暗命中、法术抗体友方 / 敌方、宝箱两条、刺坑、魅魔识破、眼柄配方、敌人等级开关合并、半巫妖谈判公式 / 两个分支、吞眼魔脑、拉洛克情报、纳巴苏、卓尔黑暗术）。

| 运行（最终 DLL） | 结果 |
|---|---|
| -Only Cleanup | PASS 25 |
| -Only Adventurer | PASS 131 |
| -Only ChromaticDragons | 第一次 FAIL（第 12 项“骇人威仪吓到豁免失败的殖民者”，规格三清单里原有的不稳定项），重跑 PASS 23 |

本轮中间跑过（之后的改动不影响这些组的除外）：Netheril PASS 36、Events PASS 236、SharedComponents PASS 46、KnowledgeFeedback PASS 116 ×2、ConstructDesign PASS 133、Legendary PASS 13、Scenarios PASS 161 ×3、Factions PASS 326、CommonRaces PASS 25、Exalted PASS 33。
完整测试最后一次（22:00 前）跑到第 1591 项（种族比例，已改）；在那之前依次推进过 925 → 1159 → 3007 → 3304 → 3419。没有跑到全绿。
-Only LegendaryMonsters：第 4 项“倒地的死亡骑士一小时后化灰”失败（加麻醉剂没倒地）。推测和传奇疼痛休克同根（超凡体质硬撑），试改后那次运行没出结果，按 Peter 的要求停手，改动已还原，列为待办。

## 还没处理的不稳定检查 / 待办

- LegendaryMonsters #4 死亡骑士化灰（见上）。
- ChromaticDragons 骇人威仪（时好时坏）。
- 规格三清单里没跑到 / 没复现的：炎魔火焰光环、巫妖测试启动卡住、泰坦神罚射程、基因冷却再次触发、-VPE 云游导师 / 老牧师、-Isekai 宝箱（已按宝箱播种，未验证）、Races124 随机项（#376 smite）、Giants 阶级、Artworks 血肉艺术品品质、老 lich-construct（骸骨登记重建、重塑存读档伤口比例）、死灵冲击卷轴、巨魔血成瘾、genepack-haul。
- -Only 组名和准备函数名不一致、读档检查可能找不到夹具的组（只在 -Only 时有影响）：BodySize、FleshImplants、ForceDragon、MythicBridge、Remolding、Satiety、VPEBridge。
- 完整测试连续全绿、-VPE / -Isekai / -NoHAR。
