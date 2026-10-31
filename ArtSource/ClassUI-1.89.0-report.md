# 1.89.0 升级时的部位生命与施法强度变化（只改显示）

Peter：「帮我加入 升级之后部位生命，和施法强度 的变化」。数值和平衡都没改。
版本：About.xml 当时是 1.87.0，另一线程（classfeats，备份 backup-1.87.0-classfeats）已在代码里占用 1.88.0，所以本次用 1.89.0。
备份：`ArtSource/backup-1.87.0-classui/`（About.xml、使用说明.txt、Source/ClassUI.cs、Classes.cs、Adventurer.cs）。

## 改动

| 文件 | 内容 |
|---|---|
| `Source/ClassLevelInfo.cs`（新） | 全部计算与文字：`Growth/PartWith`（同 `LevelGrowth` 公式的“下一级”部位生命）、`Stats/PowerWith`（StatPart_ClassPower：相关职业等级合计 +2%/级、上限 40%；冒险者等级变化时另加熟练加值 ×2%）、`RowLine`、`ClassTip`（每级表 + 下一级获得）、`NextLines`、`ResourceFormula/ResourceTip`、`AddLevel`（加点 + 前后对比消息）、`LevelUpLines`（挂到 `ITab_Adventurer.LevelUpLinesHook`，保留原有挂钩）。 |
| `Source/ClassUI.cs` | 职业名悬停换成每级表；每个职业标题下多一行直显「部位生命 +X% → 下一级 +Y%　<强度> ×A → 下一级 ×B」；资源条悬停用真实公式；加点按钮走 `ClassLevelInfo.AddLevel`。 |
| `Source/Classes.cs`（Dialog_ChooseClass） | 每行高 84→100，多一行同样的前后对比；悬停同职业名悬停 + 资源公式；加点走 `ClassLevelInfo.AddLevel`。 |
| `Source/Adventurer.cs` | 升级信件的部位生命行改为「部位生命 +a% → +b%」（合计倍率，含体质 / 传奇 / 坚韧）。 |
| `Source/ClassUIChecks.cs`（新）+ `ClassChecks.cs` 一行 | 3 条检查，挂在 `CheckClasses` 末尾。 |
| About.xml / 使用说明.txt | 1.89.0 说明。 |

## 公式（从代码读出）
- 部位生命合计 = (1 + LevelGrowth × 效果强度) × 体质 × 传奇血量 × 坚韧；LevelGrowth = 平均每级成长 × (等级 − 1)，职业等级按职业生命骰（野蛮人 7%，战士 / 圣武士 / 游侠 6%，法师 / 术士 4%，其余及未分配 5%）。
- 职业强度 = 0.5 + 技能/20 + 主属性调整值 ×5% + 0.02 × (熟练 − 2) + min(40%, 2% × (相关职业等级合计 − 1)) + 专长。格斗 / 射击类按所有物理职业合计，其余按用这门技能的职业合计。
- 资源上限：法力 2 + 等级；契约法力 2 + 等级/4；斗志 / 气 / 怒气 2 + ⌈等级/2⌉；神恩按牧师 + 圣武士合计（最多 20）；每个传奇职业能力 +2，职业专长另加。

## 测试
- Build.ps1：主程序集与三个桥都通过。
- `Test.ps1 -Only Classes`：**PASS 70 checks + 存读档**。新增 3 条：
  - 预算的“下一级”= 加点后真实值（部位生命 1.766/1.766，战技 1.08/1.08，奥术 1.08/1.08）；
  - 升级信件 4→5 级：部位生命与奥术强度「×1.00 → ×1.02（熟练加值）」；
  - 7 种资源各自的上限公式，“现在”合计 = MaxResource（职业 4 级：法力 6、契约法力 3、其他 4）。
- 第一次跑在第 54 条（游侠子职业：巨人杀手 ×1.00）失败，没到新检查；原样重跑通过，是已有的不稳定检查，与本次无关。
- 未提交 git。
