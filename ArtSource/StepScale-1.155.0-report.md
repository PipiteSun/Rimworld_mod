# 1.155.0 每级属性效果滑条

Peter 的需求：每 +2 属性（每 1 点调整值）带来的倍率要能在设置里调，例如每级 ×1.01 / ×1.05 / ×1.1；默认保持现在的数值。

## 设计

一个全局滑条「每级属性效果」k（设置 → 冒险者，「属性效果强度」下面），0%～300%，20% 以下步长 1%，以上步长 5%，默认 100%。

六维的数值效果原来分两类，k 对两类都生效：

| 类型 | 原公式 | 新公式 | 例（k = 50%） |
|---|---|---|---|
| 线性（近战伤害、移速、心灵敏感度 10%；部位生命 15%；研究每点智力 6%；职业 5% / 奥术、天赋 10%） | 1 + per × 调整值′ | 1 + per × k × 调整值′ | 力量 20 近战 ×1.5 → ×1.25 |
| 负重表（负重、采矿、射击精度、工作速度、医疗、社交等，以及 Offset 类的 M − 1） | M = Curve(属性) | M = Curve(属性)^k | 力量 20 负重 ×4 → ×2 |

表类每 +2 属性约 ×4^(0.2k)：100% ×1.32，50% ×1.15，10% ×1.03。
k 在调整值这一层，和「属性效果强度」（在 ×1 和完整效果之间插值）相乘，种族亲和、天赋的整体倍数不变。

Peter 的例子：10% → 近战每级 +1%（×1.01），50% → +5%（×1.05），100% → +10%（×1.1，就是现在）。

不受影响：属性数值、上限、专长门槛、豁免；野兽野性等级（BeastLevels 直接用 Curve / MeleeFactor）；超凡、传奇属性点、专长的固定数值。

## 代码

- `Source/Adventurer.cs`
  - `Adventurer.StepScale`（设置 `adventurerStepPercent`/100，`stepForced` 测试 / 重算用）、`Step(per)`、`ScoreCurve(score)`、`StepCurve`
  - `AMult` 用 `ScoreCurve`；`LinearValue`、`ConPart`、`PerPointValue`、`ClassPowerBonus` 用 `Step(per)`
  - `CurStage` 缓存键加上 `stageStep`，改设置立刻重建
  - `ApplySettings`：k 调低时 `RescaleStep` 按部位生命比例缩小伤口；`FinalizeInit` 记录 `appliedStep`
  - 设置存档 `adventurerStepPercent`（默认 100，限 0～300），滑条标签实时显示近战 / 部位生命 / 负重表的每级数值，悬停有完整说明
  - 「冒险者」分页属性悬停文字里的 +10% / +15% / +6% 和「每 +10 ×4」跟着 k 变
- `Source/AttributePage.cs`：「属性」页公式里的百分比、曲线值用缩放后的数值
- `Source/Classes.cs`：职业强度说明里的「主属性调整值 ×5%」用缩放后的数值
- `Source/StepScaleChecks.cs`：新检查组

## 测试

`pwsh Source\Test.ps1 -Only 'StepScale,AttributePage'` → PASS 11（StepScale 6 + AttributePage 5）。
StepScale 检查：100% 时曲线和每点值与原来完全相同；50% 时力量 20 近战 1.5 → 1.25、负重 4 → 2、体质 16 部位生命 1.45 → 1.225、缓存状态重建；10% 时近战 ×1.05；调低后伤口按比例缩小；属性页公式显示 5%。

## 备份

`ArtSource/backup-1.154.0-stepscale`：Adventurer.cs、AttributePage.cs、Classes.cs、OnlyChecks.cs（未改）、About.xml、使用说明.txt、LichPhylactery.dll。
