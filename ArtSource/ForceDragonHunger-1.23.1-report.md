# DND边缘大陆 1.23.1 力能龙幼崽食量 报告

日期：2026-10-24。请求：力能龙幼崽的饥饿速度保持100%，不要225%；补充说明：225% 来自正常的基因复杂度，在“力能龙之心”里加补偿，让幼崽成年前都保持100%。

版本说明：协调说明写的是 1.22.1 → 1.22.2，但开工时模组已经是 1.23.0（巨躯规则已完成），所以按顺序发成 **1.23.1**，备份放在 `ArtSource/backup-1.23.0/`（含旧 DLL 和联动 DLL）。

## 225% 从哪来

反射读了原版 `Need_Food.FoodFallPerTickAssumingCategory` 的调用：食物下降 = 生命阶段基础饥饿 × 饥饿档倍率 × `HediffSet.GetHungerRateFactor`（各 hediff 阶段的 hungerRateFactor）× 特性 × 床 × **`GeneTuning.MetabolismToFoodConsumptionFactorCurve`(未被覆盖基因的 biostatMet 之和)**。

力能龙异种型的代谢合计：专属基因 −1（心−2、爪−1、鳞−1、龙体−1、肉食+1、傲慢+2、囤积+1），原版基因约 −7（光滑尾−1、美丽−2、强健−2、强健消化−2、暗视−1、不育+1），合计低于 −5，曲线封顶在 −5 → **225%**。另外 `龙龄` hediff 本身还有 ×140%（幼龙）～×200%（伟龙）。

## 改动

- `Source/ForceDragon.cs`：新增 `ForceDragon.Juvenile`（非成年发育阶段）、`Metabolism`（与原版同口径：未被覆盖基因的代谢和）、`MetabolismFactor`（原版曲线）。`Hediff_DragonAge.CurStage`：幼崽的 hungerRateFactor = 1 / 基因代谢倍率（225% 时 ×44.4%），不再加龙龄食量；成年后照旧 `Hunger(age)`。缓存键加上“是否幼崽”和代谢值，长大或换基因会立即重算。没有新补丁，原版 `GetHungerRateFactor` 每次都读 CurStage，无缓存问题。
- `Source/ForceDragonChecks.cs`：新增 4 条检查——成年幼龙仍是基因225% × 龙龄140%；孵出的幼龙婴儿合计100%；8岁幼龙儿童合计100%；儿童升到成年龙龄（3级）仍是100%。
- `Defs/ForceDragon.xml`：力能龙之心效果说明加一行“幼崽食量”。
- `About/About.xml`：1.23.1，描述补一句。
- `使用说明.txt`：标题 1.23.1、内容一览的力能龙一行、1.14.0 力能龙龙龄食量一句加注、更新日志新增 1.23.1。
- 重新编译 `1.7/Assemblies/LichPhylactery.dll` 与 `Bridges/MythicBloodlines/Assemblies/LichPhylactery.MythicBridge.dll`。

## 数值

| 对象 | 基因代谢倍率 | 龙龄倍率 | 合计 |
|---|---|---|---|
| 幼崽（婴儿/儿童，任何龙龄） | 225% | ×44.4% | **100%** |
| 成年幼龙 | 225% | ×140% | 315%（不变） |
| 成年龙（3级） | 225% | ×170% | 382.5%（不变） |
| 伟龙 | 225% | ×200% | 450%（不变） |

（以上是与基因相关的倍率；生命阶段本身的基础饥饿、特性、床等原版因素照常。）

## 测试

- `pwsh -File .\Build.ps1`：PASS（主程序集 + 神兽血裔联动）。
- `pwsh -File .\Test.ps1`（Core）第一次：FAIL after 1604 checks，失败在无关的旧检查“死灵冲击卷轴”（目标被打到只剩已缺失部位，hp 1->1），与本改动无关。
- 重跑一次：**PASS: 2540 checks**，日志无红字。
