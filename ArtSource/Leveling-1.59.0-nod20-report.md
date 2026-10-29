# 1.59.0 热修：去 d20 措辞

玩家看到的文字里不再出现 d20、掷骰、“+N ≥ DC” 公式；豁免和交涉一律写成成功率百分比。判定逻辑和数值完全不变（`Legendary.Save` 仍是 1～20 随机 + 调整值 + 熟练 ≥ 难度，本来就是 5% 一档）。

## 换算

成功率 = (21 − 难度 + 属性调整值 + 熟练加值 + 额外加值) ÷ 20，限 0～100%（不含「幸运」再试一次）。

“基础成功率”= 1 级冒险者（调整值 0、熟练 +2）：

| 难度 | 10 | 12 | 13 | 14 | 15 | 16 | 17 | 18 | 20 | 21 |
|---|---|---|---|---|---|---|---|---|---|---|
| 基础成功率 | 65% | 55% | 50% | 45% | 40% | 35% | 30% | 25% | 15% | 10% |

没有冒险者等级的人（动物、机械、儿童）比基础再低 10%。

## 代码

- `Source/Legendary.cs`：新增 `SaveChance(mod,prof,bonus,dc)`、`SaveChance(pawn,a,dc,source)`、`SaveChanceText(...)`（“成功率 N%”）。`D20()` / `Save` 不动。
- `Source/Adventurer.cs`：熟练加值悬停两行改为“每点属性调整值和熟练加值各 +5% 豁免成功率 / 交涉成功率”。
- `Source/ChromaticDragons.cs`：新增 `NegotiateChance`；勒索信写“难度 15/18，某人的成功率 N%”，按钮“交涉（成功率 N%）”，结果消息显示成功率；来袭信的威仪写难度和 +5% 规则。
- `Source/Pacts.cs`：审阅 / 律师 / 谈判按钮和契约书说明显示难度 + 成功率。
- `Source/Outsiders.cs`：真名驱逐菜单显示难度 + 目标的成功率。
- `Source/LegendaryMonsters.cs`：半巫妖信件写难度 21、基础成功率 10%。
- `Source/AbyssRitual.cs`：三封信“掷出 X%”→“本次结果 X%”。
- `Source/HeroTraits.cs`：设置说明“掷骰”→“随机”。
- `Source/LegendaryChecks.cs`：新增 1 条检查：SaveChance(0,2,0,15)=40%、(5,6,0,15)=85%、上下限，且与 1～20 全部点数实测通过数 ÷ 20 一致。

## 定义与文档

- Defs：`Abyss_Races.xml`(7)、`LegendaryMonsters.xml`(2)、`Pact_Items.xml`(1)、`Pact_Patrons.xml`(2)、`Pact_Races.xml`(13)、`ChromaticDragons_Items.xml`(1)、`Adventurer_Feats.xml`(幸运)。固定难度写“难度 N，基础成功率 X%”，随施放者变化的写“难度 = 8 + 某某的熟练加值和某属性调整值；自己的属性调整值和熟练加值每点 +5% 成功率”。吐息“每 6 秒掷一次（1/3）”→“每 6 秒有 1/3 几率充好”。“投掷”不动。
- `使用说明.txt`：16 行改写 + 1 行更新记录（在“1.59.0 热修（神灯）”前），行数 1852→1853，逐行比对只有这些行变化。
- `About/About.xml`：描述里的“d20 + 调整值 + 熟练加值”改为成功率说法（其他热修没写进 About，所以没加更新记录句）。

## 剩余命中

只剩代码注释、测试名/测试输出（英文开发者日志）、变量名（`TestD20`、`D20()`、`roll`）、XML 注释（BackstoryPools.xml:4、Twilight.xml:225）和更新记录那一行本身。

备份：`ArtSource/backup-1.59.0-nod20`。

## 编译与测试（2026-10-28）

- Build.ps1：主程序集和 HAR / VPE / Mythic 桥全部 PASS，没有编译错误。
- `-Only Adventurer`：PASS 123 条，存读档正常。
- `-Only ChromaticDragons`：两次 PASS 23 条（包含交涉检查）；另外三次失败在吐息或威仪的随机检查上（第 10～12 条，每次不一样），换成旧 DLL 也一样会失败，属于原本就不稳定的检查。
- `-Only Legendary`：失败在第 7 条“pain shock”（传奇抗性拦疼痛倒地）。旧 DLL 同样失败（失败在第 5 条，旧版少一条检查），是以前就有的问题，和这次改动无关。新加的成功率检查排在它前面，已经通过。
