# 1.71.0 探索公告板 报告（2026-10-29）

## 内容
- 新建筑 `LP_QuestBoard`「探索公告板」：杂项，60 木材，2×1，可搬运，无研究（研究页签规则不受影响）。选中后「查看委托」打开窗口。
- 每天刷新 6 份委托（`GameComponent_QuestBoard`，每 250 刻推进；地图上有公告板才刷新），同时最多接 3 份。
- 等阶：青铜 / 白银 / 黄金 / 秘银 / 传说，威胁 300 / 700 / 1400 / 2500 / 4000 点，按殖民地最高冒险者等级 1 / 5 / 9 / 13 / 17 解锁（冒险者等级关掉时全开）。
- 三类：
  - 守卫家园：6～12 小时后敌对派系 Combat 组（无合适派系 → 狂暴野兽）从地图边来袭，全部倒下 / 被俘 / 离开即完成。
  - 远征讨伐：世界地图上的「怪物巢穴」（`LP_QB_MonsterLair` + `GenStep_QuestBoardLair`，无派系），永久狂暴的野兽，威胁 ×0.8；野兽战斗力至少 点数÷8（30～180），优先当地生物群系。
  - 清剿据点：敌对派系的原版 Outpost 站点，消灭守军即完成。
  - 站点距离 (2+阶)～(5+2×阶) 格，期限 12+2×阶 天（TimeoutComp），过期失败。
- 接取：选一名殖民者为接取人，生成原版任务（`Quest.MakeRaw` + 空根 `LP_QuestBoardContract` + `QuestPart_QuestBoard` 提供查看目标），`quest.points` = 委托点数。
- 完成：`Quest.End(Success)` → 现有 `AdventurerQuest` 补丁 → `Adventurer.QuestDone`：接取人“点数 × 0.5”，其他殖民者 ×0.25（都乘经验倍率）。报酬（银币 + IsekaiLoot 普通 / 稀有 / 魔法物品池）空投到殖民地。
- 设置：“冒险者”分组新增「探索公告板」开关（`Settings.questBoard`，默认开）；设置分组数不变。
- 不依赖 Isekai：只参考了 Isekai 公会公告板（GuildBoardBuilding.xml、IncidentWorker_IsekaiHunt 的手工任务拼法），没有引用其程序集。
- 文字只写点数、等级和百分比，没有骰子用语。

## 文件
- `Source/QuestBoard.cs`（QuestBoard 静态类、BoardContract、GameComponent_QuestBoard、QuestPart_QuestBoard、GenStep_QuestBoardLair、Building_QuestBoard、Window_QuestBoard、设置）
- `Source/QuestBoardChecks.cs`（`-Only QuestBoard`）
- `Defs/QuestBoard.xml`
- `Source/Adventurer.cs`：ExposeQuestBoard() 调用 + 设置复选框
- 贴图 `Textures/LP/QuestBoard/QuestBoard_{south,north,east}.png`，脚本 `ArtSource/RaceArt/draw_questboard_171.py`，预览 `ArtSource/Preview/QuestBoard-1.71.0.png`
- `About/About.xml` 1.71.0，`使用说明.txt`（第一部分摘要、第二部分【1.71.0 探索公告板】、更新日志）
- 备份 `ArtSource/backup-1.70.0`

## 测试（简单测试）
- `.\Test.ps1 -Only QuestBoard`：PASS 20 条（含存读档）。覆盖定义 / 贴图、等阶门槛、每日刷新、守卫家园全流程（来袭 → 打退 → 成功、接取人 +150 经验、空投）、远征讨伐全流程（站点 → 实际生成 80×80 巢穴地图 → 消灭 → 成功）、清剿据点过期失败、上限与开关、读档后已接委托和今日委托不变。
- `.\Test.ps1 -Only QuestBoard,ResearchTab`：PASS 26 条。
- 没跑全量测试（按 Peter 的要求只做定向测试）。

## 已知 / 取舍
- 接取时直接接受任务（任务分页里没有“接受”按钮这一步）；委托不能在公告板上放弃（与原版已接任务一致）。
- 守卫家园的敌人不走原版袭击事件，所以不算原版“守住袭击”的那份袭击经验，只拿任务经验。
- 远征讨伐成功后巢穴地图由原版在远征队离开时收起。
