# DND边缘大陆 1.41.0 报告：成神开局剧本

需求：ArtSource/Ascension-1.41.0-spec.md（原文另存，没有改 1.40.0 的需求文件）。备份：ArtSource/backup-1.40.1（改动前的 Defs/Scenarios.xml、Defs/Titan.xml、Defs/Faith.xml、Source/Scenarios.cs、Titan.cs、Ascension.cs、AscensionPaths.cs、AscensionChecks.cs、About/About.xml、使用说明.txt、1.7/Assemblies/LichPhylactery.dll）。Source/Scenarios.cs 最后没改，备份只是保险。开工前确认过 About.xml 是 1.40.1，RimWorld 没在运行。没有新增美术。

## 测试结果

| 命令 | 结果 |
|---|---|
| `Source\Build.ps1` | PASS（主程序集 + HAR / VPE / 神兽血裔桥接；桥接源码没动） |
| `Source\Test.ps1 -Only Ascension` | PASS：15 条（原 10 条 + 新 5 条，含存读档），日志无红字。第一次跑 15 条都过了，但日志里有一条原版红字：我的检查用 `ThingsOfDef(MinifiedThing)` 找小型化圣坛，原版要求改用 `ThingsInGroup`。改了检查代码后重跑干净通过 |
| `Source\Test.ps1 -Only Giants` | PASS：33 条；日志 `OK all 19 mod scenarios show their full description on the scenario select page`（原来 15 个 + 新 4 个，坠落的神裔改过的描述也能显示） |
| 完整套 / -VPE | 没跑（按要求） |

新加的 5 条（每个剧本生成一次：剧本里的组件按顺序作用在测试小人身上，开局研究跑剧本自己的 `ScenPart_StartingResearch`，开局物品放到地图上数）：
1. 耐瑟遗徒：法师心灵链接 4、智识 ≥10、卡苏斯的回响 2 层、拉洛克好感 0、使者固定在第 10 天（`stage=scheduled`、`envoyTick = 现在 + 10 天`）、耐瑟瑞尔奥术和神格学已研究、研习卷 ×2（优秀）/秘术残卷/金 100/秘银 50 在地图上。
2. 末日将至：命匣 20 级、3 具骷髅、多一座死灵仪式台；半神巫妖路第 0 步，万魂 10/20（进度条 50%），凯兰沃神眷 -50，有“成神之路”按钮；先遣队计时 = 第 5 天；先遣队落地后（2 名末日卫士，凯兰沃派系）试炼仍是第 0 步、波数 0；神格学已研究；玻璃钢 50。
3. 朝圣之路：生成时三人都拿到信徒特性，开局后三人都信第一位的神，第一位是这位神的神选者，第二位不是；守护神 = 该神，神眷 60；圣徒之路第 0 步“试炼一”，有按钮，窗口里有“放弃这条路”；小型化圣坛、圣徽、圣水 5、神迹残片 5 在地图上；信仰与神术和神格学已研究。
4. 黄昏猎神者：老兵射击/格斗 12、心灵敏感度 140%；黄昏之年计时 = 第 3 天；把时钟拨到那一刻后，世界组件真的开始了黄昏之年（游戏第 0 年，说明不受“第 2 年起”限制）；神格学已研究；残片 5、精良长剑、精良突击步枪。
5. 坠落的神裔：开局存下父神（在 7 位父神里），泰坦路第 0 步、父神和神职对得上，有按钮；开局信件的标题和正文写着父神名字；放弃后重开还是同一位父神；神格学已研究。

几个剧本的检查也都确认了：说明全文能显示（没有“Cannot read data”），描述里写了“推荐路线”和“难度”。

## 做了什么

- **Source/AscensionScenarios.cs（新）**：`AscensionStart` 公共小工具（选首领 = 人物页第一位符合条件的人、提技能、写信）和四个剧本组件：
  - `ScenPart_NetherilHeir`：心灵链接（装了 VPE 走 `NetherilHooks.Level` 到 8，没装到 4）、智识、`LP_KarsusEcho` 严重度 2；`NetherilQuest.Larloch` 好感设 0；把 1.37.0 的任务状态设成 `scheduled`、`envoyTick` = 第 10 天，后面由 1.37.0 原有的每小时结算发出使者（任务代码没改）。
  - `ScenPart_DoomLich`：接在 `LP_LichAscension`（level 20、骷髅 3、巨像 0）后面，开半神巫妖路、万魂计数设 10；组件自己存 `fireTick`，到点在 `Tick()` 里用 1.40.0 的 `Ascension.KindPawns` / `Arrive` 派末日卫士直扑命匣。
  - `ScenPart_Pilgrimage`：`Notify_PawnGenerated` 给开局人物随机信徒特性；开局时统一成第一位的神，加 `LP_Chosen`，设守护神、神眷 60，放一座小型化木制圣坛，开圣徒路。
  - `ScenPart_TwilightHunter`：射击/格斗取较高的一项提到 12；心灵敏感度不够就换“心灵敏感”特性（还不够换“心灵超敏”）；把 `WorldComponent_Twilight.startTick` 设成第 3 天。
- **Source/Titan.cs**：`ScenPart_FallenEmpyrean` 的开局多一步 `Awaken(titan)`：定下父神、开泰坦路、发信写明父神和神职。`Apply` 没动（Races124 的老检查还在调用它）。
- **Source/Ascension.cs**：`LP_AscensionTracker.fathers`（ThingID → 神，存档键 `lpTitanFathers`）+ `FatherOf`；`Ascension.Start(p, path, letter=true)` 多一个参数，剧本传 false，改发自己的说明信。
- **Source/AscensionPaths.cs**：`FatherPool`、`Father(p, assign)`；泰坦开路时先用存下的父神，没有才随机并存下；`OnStart` 跟着加了 `letter` 参数；开路确认框里有父神就写出名字。
- **Source/AscensionChecks.cs**：`AscScenarios()` 5 条，跑完把守护神、神眷、耐瑟任务状态、黄昏计时和研究都恢复原样；`AReset` 也清 `fathers`。
- **Defs/Scenarios.xml**：4 个 ScenPartDef + 4 个 ScenarioDef（照种族剧本的写法：站立开局、固定人数、开局研究、物品、散落钢木、开场对话）。
- **Defs/Titan.xml**：坠落的神裔加神格学研究，描述和摘要加推荐路线、难度和父神说明。
- **Defs/Faith.xml**：`LP_FaithAltar` 加 `minifiedDef`（MinifiedThing）和 `BuildingsMisc` 分类。
- About/About.xml 1.41.0 + 描述一句；使用说明.txt：标题、内容一览剧本一节、泰坦觉醒一句、第二部分【1.41.0 成神开局剧本】、更新日志。

## 和需求不一样 / 需求没写死、我定的地方

1. **圣坛小型化**：原来的圣坛不能小型化，所以给 `LP_FaithAltar` 加了 `minifiedDef`。从此所有圣坛都可以拆下搬走（原版家具都是这样）。开局那座由剧本组件自己做成小型化（木制）放下，没有走原版“开局物品”，因为我不确定原版的开局物品会不会自动把建筑小型化。
2. **朝圣之路的神怎么随机**：每个开局人物生成时各带一位随机神的信徒特性（人物页上能看到），开局时其余两人改信第一位的神。所以重新随机第一位，神就会跟着换；只重新随机第二、三位只换那个人，开局后照样统一。人物页上第二、三位显示的特性可能和第一位不同，开局才改过来。
3. **首领**：都是人物页第一位符合条件的人（法师要心灵敏感度 >0 且智识没被禁用；老兵要能打架），不符合就顺延；巫妖照“命匣初成”的规则。
4. **VPE 灵能等级 8**：用循环 `ChangePsylinkLevel` 一直加到 `NetherilHooks.Level ≥ 8`（最多 30 次）。这次没跑 -VPE，**装了 VPE 的情况没测过**。没装 Royalty 时心灵链接给不了（照 `ScenPart_StarterBoon` 的写法）。
5. **末日卫士先遣队**：点数 = 当前殖民地袭击点数 × 0.5，**不乘**“试炼难度倍率”（需求写的是殖民地点数 × 0.5），至少 2 名；目标是命匣，固定第 5 天。用的是 1.40.0 的生成方法，不走原版 RaidEnemy（凯兰沃派系是隐藏派系，没有原版袭击用的人员组）。
6. **黄昏之年第 3 天**：直接设黄昏之兆的倒计时，所以不会有原版那封“黄昏之兆”信，剧本说明信里写了。设置里关掉“黄昏之年”也照样开始（剧本强制）；持续天数照设置。
7. **心灵敏感度 ≥110%** 是换特性做到的（去掉迟钝/失聪，加“心灵敏感”，还不够加“心灵超敏”）。如果是基因压得很低，可能还是不到 110%（检查里用的是普通人类，140%）。
8. **“精工”**按原版品质“精良”（Excellent）做；研习卷“优秀”也是 Excellent。
9. **开局信件**：已经开启的路不再发通用的“踏上了××之路”信，每个剧本发一封自己的说明信（推荐路线、当前试炼、后面几步、怎么放弃）。耐瑟遗徒和黄昏猎神者没有提前开的路，信里写清楚路怎么开。
10. **父神的修复是通用的**：不只坠落的神裔，任何泰坦第一次开路随机出的父神都会存下来，放弃后重开不再重抽。
11. **成神任务线关掉时**：路不开（信里说明），父神照样存下，耐瑟/黄昏/先遣队照常。
12. **其他开局物品**：照已有种族剧本补了白银、生存食品、药品、零部件和散落的钢、木（数量写在使用说明里）；末日将至还多给 120 人肉喂命匣（同“命匣初成”）。两卷研习卷写成两条开局物品，因为书不能堆叠。
13. **到场方式**用站立（同其他种族剧本），不是坠毁舱。
14. 难度：耐瑟遗徒“中”，末日将至“难”，朝圣之路“中”，黄昏猎神者“很难”，坠落的神裔我标了“中”（需求没写）。

## 新 defName

| 类别 | defName |
|---|---|
| ScenarioDef | LP_NetherilHeirScenario 耐瑟遗徒、LP_DoomLichScenario 末日将至、LP_PilgrimageScenario 朝圣之路、LP_TwilightHunterScenario 黄昏猎神者（LP_FallenEmpyrean 坠落的神裔是改的） |
| ScenPartDef | LP_NetherilHeir、LP_DoomLich、LP_Pilgrimage、LP_TwilightHunter |
| C# 类 | ScenPart_NetherilHeir、ScenPart_DoomLich、ScenPart_Pilgrimage、ScenPart_TwilightHunter、AscensionStart |
| 存档键 | LP_AscensionTracker 的 lpTitanFathers；ScenPart_DoomLich 的 fireTick（随剧本存档） |

## Peter 试玩时请注意

- **要完全重启游戏**才会加载新 dll 和 Defs。新剧本在选择剧本页里种族剧本的后面。
- 朝圣之路：开局先选中地上的小型化圣坛，点“安装”，再给神选者戴上圣徽。神是谁看第一位开局人物的“信徒”特性。
- 末日将至：第 5 天的先遣队直扑命匣，开局就要守住它。
- 没自动测、请在游戏里看：人物页上重新随机时信徒特性是否跟着换、开局信件的排版、装了 VPE 时耐瑟法师的灵能等级是不是 8、先遣队落地的位置、小型化圣坛的图标。
