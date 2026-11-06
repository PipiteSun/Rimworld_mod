# 1.122.0 二周目 与 系统宿主 — 报告

2026-11-06。要求见 `ArtSource/SecondLifeSystemHost-1.122.0-spec.md`。备份：`ArtSource/backup-1.121.0`（Adventurer.cs、About.xml、使用说明.txt、trait_rarity.py、LichPhylactery.dll）。没有推 GitHub。

## 改动文件
新增
- `Source/SecondLife.cs` — 二周目：`SecondLife`（Past / XpFactor / OnLevelUp / Memory / TipLine）、`GameComponent_SecondLife`（前世等级与“已走完”存档）、`SecondLifeStartup`（自测进程把两个新特性 commonality 置 0、系统面板不弹窗）、`SecondLifeTip`（Trait.TipString 后缀，二周目 / 系统宿主的状态行）。
- `Source/SystemHost.cs` — 系统宿主：`HostTask` / `HostReward` / `HostProgress`、`SystemHost`（任务、记录差值、领奖 Claim、惩罚、物品池）、`GameComponent_SystemHost`（每 250 刻推进、排队弹窗）、`SystemHostGizmos`（Pawn.GetGizmos 后缀：「系统面板」「改命符」按钮）。
- `Source/SystemHostWindow.cs` — 系统面板（照搬百战觉醒 MilestoneWindow 布局）。
- `Source/SystemHostFate.cs` — 改命符：`FateRewrite`（档位 / 标记 / 锁定 / 候选 / Apply）、`CompFateCharm`（地上的改命符按钮）、`Dialog_FateRewrite`（改命窗口）。
- `Source/SecondLifeChecks.cs` — 检查组 SecondLife、SystemHost（含改命符）及各自存读档部分。
- `Defs/SecondLifeSystemHost.xml` — LP_SecondLife、LP_SystemHost、LP_FateRewriteCharm、LP_SystemPenalty。
- `Textures/LP/SystemHost/SystemPanel.png`、`FateCharm.png`；脚本 `ArtSource/RaceArt/draw_systemhost.py`（沿用 draw_class_art 的粗描边平涂画法）。

修改
- `Source/Adventurer.cs` — 只加三处钩子：GainXP 在其他倍率之后 `amount*=SecondLife.XpFactor(p,h)`；GainTrainingXP 同样乘；Hediff_Adventurer.LevelUp 末尾 `SecondLife.OnLevelUp(this)`。没碰属性天赋部分。
- `ArtSource/trait_rarity.py` — 表里加两行（LP_SecondLife Rare、LP_SystemHost Epic，kind 奇遇特性）；XML 里的 ext 与标签颜色是按同一张表手写的，-Only TraitRarity 检查两边一致。
- `About/About.xml` — 1.122.0 + 更新日志一句。
- `使用说明.txt` — 更新日志开头加 1.122.0 一段（锚点「三、更新日志」标题后，保留 BOM / CRLF）。
- `1.7/Assemblies/LichPhylactery.dll`（及三个桥 DLL 重编）。

## 测试
编译前确认游戏关闭、没有其他 Build/Test 在跑、Source 里近 5 分钟没有别的文件改动。`Build.ps1` 全部 PASS。
- `-Only SecondLife`：**16 PASS**（含存读档）
- `-Only SystemHost`：**42 PASS**（含改命符、存读档）
- `-Only TraitRarity`：**7 PASS**
- `-Only Adventurer`：**179 PASS**

覆盖：定义/档位/颜色/commonality；前世等级 8～14 且稳定、存读档不变；经验 ×2 → 到前世等级 ×1、传奇不受影响；前世记忆加研究进度（= 花费 30%）、没研究改热情、热情全满跳过；升级触发记忆与“前世的路走完了”信；冒险者关闭时不生效。系统宿主：首次发布 3 个不同任务、L=6 时各目标值、按钮只给宿主、窗口 840×710 强制暂停；只能接受列表里的一个、7 天期限；六种任务都按 records 差值完成；7 天后再发布；三种奖励各能领（属性点 +1 且能在分页花掉、背包改命符、物品来自正确池）；同一奖励不能领两次、只能领队首；无冒险者时属性项拒绝并保留奖励；超时上惩罚（工作速度实测 ×0.85、3 天）；放弃不罚；失去特性后任务作废、奖励仍可领、按钮在领完后消失；存读档后任务 / 奖励 / 记录保留且可领；没 Isekai 时走 DND 池。改命符：同档替换成功并消耗、发信；跨档不可选且不消耗；冲突（法术护体 vs 法术之血）、已拥有、事迹被排除；诅咒只换诅咒；事迹 / 系统宿主 / 天命 / 基因特性锁定；光谱按程度算档（屠龙志士优秀不能换惧龙普通；原版勤奋可换同特性另一程度）；最后一个用完物品消失、按钮消失；地上的改命符有按钮。

## 决定与偏差
1. **二周目数据存放**：独立 `GameComponent_SecondLife`（按 thingIDNumber），不改 Hediff_Adventurer 字段，避免和另一线程撞 Adventurer.cs。失去再得到特性时沿用原来的前世等级。“第一次拥有”= 第一次被查询到有特性（经验、升级、悬停都会查）。
2. **经验 ×2 的范围**：GainXP（含 scaled=false 分支）和训练经验 GainTrainingXP 都乘（“所有冒险者经验”）；敌人 RaiseTo 直接定级不走经验，不受影响。传奇等级自然不受影响（前世最高 14）。
3. **前世记忆**：热情抽中但全满时直接跳过（照要求，不改去研究）；研究抽中但没在研究时改热情。研究进度用 `ResearchManager.AddProgress(proj, proj.Cost×0.3)`。
4. **第 6 种任务**：用“俘获 1 名囚犯”（记录 PeopleCaptured），没做“救起倒地同伴”（没有现成记录，要加 Harmony）。击杀用记录 Kills，动物也算，所以任务文字写成“击杀 N 个敌人或野兽”。
5. **任务节奏**：特性第一次被扫描到时立即发布第一批，之后每 7 天；有进行中的任务时不发布，结束后若已过 7 天立即发布。不接的任务保留到下一次发布再换。任务发布和完成都会排队弹出系统面板（同百战觉醒：没有系统面板开着时才弹）。
6. **领奖**：属性点加到 `Hediff_Adventurer.asiLeft`（和升级属性点同一处分配）；面板按钮“打开冒险者分页”会关掉面板、选中人物并打开分页。没有冒险者等级时属性框禁用并说明原因，领取也会被拒并保留奖励。
7. **随机物品**：有 Isekai 时与百战觉醒完全相同（六种 ×1，发奖时抽定）；没装时 DND 池：治疗药水 ×2、强效治疗药水 ×1、英雄药水 ×1、位面残渣 ×5、秘银 ×10，随机一种。
8. **系统面板布局**：照 MilestoneWindow（840×710、forcePause、doCloseX + 关闭按钮、点外面不关、确认框、领取记录滚动区）；上方任务区是进度条 + “放弃任务”按钮，或三个“接受任务”框；奖励框高度 170（原 185），给任务区腾地方。
9. **改命符入口**：背包里有改命符的殖民者有小人按钮「改命符（N）」；地上的改命符选中后有「使用改命符」按钮。改命窗口三列：人物（持有者在前 + 同地图或同远行队的自由殖民者）/ 特性（不可改的灰显并写原因）/ 同档可选。全程不用浮动菜单。
10. **改命符不可改清单**（`FateRewrite.Story` + 规则）：基因赋予（sourceGene）、开局剧本指定（ScenForced，规格没写，我加的）、传说事迹（Deed 标记）、系统宿主、`Isekai_*`、`IKM_*`、天命四个（LP_Prophesied / LP_PactDestiny / LP_WeaveChild / LP_DivineChampion）、神力灌注 LP_DivineInfusion、信徒 LP_Devotee（程度 = 信奉的神，Faith 记着状态）。右列也不出现这些。无神论者、性格特性、诅咒、天赋、武艺天赋、背景特性、二周目等都可以改。
11. **“种族不允许”**：按 PawnKindDef.disallowedTraits / disallowedTraitsWithDegree、激活基因的 suppressedTraits、特性 requiredWorkTags / requiredWorkTypes 与此人禁用的工作判断。HAR 种族自己的特性白/黑名单没有接（主程序集不引用 HAR）。
12. **改命符不出现在商人和掉落**：tradeability None、generateCommonality 0、没有 tradeTags / thingSetMakerTags。开发者模式可直接生成。
13. **自测进程**：两个新特性 commonality 在 -lichtest 时置 0（XML 原值另存供检查），避免随机出现在其他检查组的小人身上让经验翻倍；系统面板在自测时不弹窗。
14. 特性悬停标头的“种类”写“奇遇特性”（〔稀有 · 奇遇特性〕/〔史诗 · 奇遇特性〕）。没有加设置项（避免改 Core.cs 和设置分组计数）。
15. 玩家可见文字没有 d20 / 掷骰 / 检定 / DC（检查组也断言了两段描述）。

## 没在游戏里看过的部分
- 系统面板和改命窗口的实际画面（布局、文字是否溢出、滚动区、灰显效果）——检查只构造了窗口、没有渲染。
- 小人按钮和两张贴图在游戏里的观感（贴图本身看过 PNG）。
- 弹窗排队的实际体验（自测里关闭了弹窗）；7 天发布一次在真实游戏节奏里是否太频繁。
- 研究任务 1500+150L 点在 7 天内是否现实（按规格数值，没调）。
- 前世记忆、走完前世、惩罚、改命的信件在信件栏里的样子。
- 装 Isekai 时的随机物品池（Core 档没装 Isekai，只测了 DND 池和“池跟随 Isekai 是否加载”）。
