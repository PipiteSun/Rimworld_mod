# 1.88.0 职业专属专长 — 报告

需求（Peter，经协调者，2026-10-31）：给十二个职业设计职业专属专长，只有这个职业的人能选（带职业等级前置），走现有“可选专长”界面；不和已有专长 / 职业能力 / 子职业重复；线程自己决定、直接实现。

## 设计
- 每个职业 2 个：**职业 3 级**一个（偏“入门强化”：强度 / 资源），**职业 7 级**一个（偏“进阶”：冷却 / 获取 / 消耗 + 风格数值）。共 24 个。
- 和普通专长一样消耗一次属性提升（或战士奖励专长），附带 +1 属性（1.67.0 规则）。
- 只对有这个职业等级的人显示；等级不够时灰色写“需要X职业 N 级”。
- 不新增主动能力，避免和职业能力表重叠；效果全部是对本职业已有能力的放大，或少量原版数值。
- 数值参照：普通专长每个大约 1～2 项 ×1.1～1.5 的效果；职业强度典型值 1.0～2.0，所以 +15%/+20% 约等于本职业能力强 10% 左右；冷却 ×0.75～0.9。
- 职业等级降到要求以下（角色编辑器）时专长保留但效果暂停；冒险者总开关关掉全部失效。

| 职业 | 3 级 | 7 级 |
|---|---|---|
| 战士 | 战场老兵：战技 / 射术强度 +15%，斗志上限 +2，疼痛休克阈值 +10% | 百战之躯：斗志获取 ×1.5，冷却 ×0.8，近战命中 / 射击精度 ×1.05 |
| 武僧 | 内息调和：气上限 +2，气恢复 ×1.5，冥想专注 +10% | 无影身法：战技强度 +20%，近战闪避 +12，移速 +0.4，冷却 ×0.85 |
| 圣武士 | 坚守圣誓：神恩强度 +15%，心智抗性 +20%，神恩上限 +1 | 圣焰狂信：近战伤害 ×1.08，冷却 ×0.8，神恩获取 ×1.25 |
| 法师 | 奥术学者：奥术强度 +15%，法力上限 +3，研究 ×1.1 | 法术塑形：法力消耗 −1（最少 1），冷却 ×0.85 |
| 牧师 | 虔诚祷者：神恩获取 ×1.5，神恩强度 +10%，包扎品质 ×1.1 | 神眷之手：神恩上限 +2，冷却 ×0.8，免疫获取 ×1.15 |
| 德鲁伊 | 荒野之子：野性 / 生长强度 +15%，种植速度 / 驯服 ×1.15 | 自然守望者：法力上限 +3，冷却 ×0.85，毒素抗性 +30% |
| 术士 | 魔力之泉：法力获取 ×1.5，法力上限 +2 | 血脉共鸣：天赋强度 +25%，心灵敏感 +10%，冷却 ×0.9 |
| 器械师 | 熟练工匠：器械强度 +15%，冷却 ×0.85，一般劳动 ×1.1 | 工坊大师：器械强度 +20%，冷却 ×0.75 |
| 野蛮人 | 怒火中烧：怒气上限 +2，怒气获取 ×1.5 | 野性体魄：狂怒强度 +20%，伤口愈合 ×1.3，疼痛休克 +10% |
| 游侠 | 荒野猎手：猎术强度 +15%，射击精度 ×1.05，冷却 ×0.9 | 追猎大师：冷却 ×0.8，移速 +0.3，瞄准时间 ×0.9 |
| 游荡者 | 阴影老手：诡术强度 +15%，近战闪避 +10 | 致命精准：冷却 ×0.8，近战命中 ×1.08，射击精度 ×1.05 |
| 邪术师 | 契约深化：契约强度 +15%，契约法力上限 +1 | 宗主眷顾：契约强度 +10%，冷却 ×0.8，心智抗性 +25% |

（“冷却”“消耗”都只算本职业的能力，含子职业与传奇能力。）

## 实现
- `Defs/Adventurer_ClassFeats.xml`：24 个 LP_FeatDef（category Class，order 60–83，enemyTags any）。
- `Source/AdventurerFeats.cs`：LP_FeatDef 新字段 `requiredClass / classLevel / powerStats / powerBonus / resourceBonus / gainFactor / cooldownFactor / costReduce`；`FeatWorker.Unmet` 先查职业等级；`PrereqText` 第一项写职业等级。
- `Source/AdventurerFeats2.cs`：`Feats2.Visible` 只对有该职业的人显示。
- `Source/ClassFeats.cs`（新）：生效判定、职业强度 / 资源上限 / 获取 / 冷却 / 消耗的计算，`Ability.StartCooldown` 前缀（只改 Ability_Class）。
- `Source/Classes.cs`：`MaxResource` + ResourceBonus；`AddResource` 正数 × GainFactor；`StatPart_ClassPower` 加 PowerBonus 并在说明里列出。
- `Source/ClassLegendary.cs`：`Cost` 末尾减 CostReduce（最少 1）。
- 图标：`ArtSource/RaceArt/draw_class_feats_188.py` → `Textures/LP/Feats/Class/*.png`（专长圆底 + 职业能力图标 + 金色档位标 Ⅰ/Ⅱ）。
- 测试：`Source/ClassFeatsChecks.cs`，`Feats2Checks.cs` 的 35 个专长计数排除职业专长。

## 测试（2026-10-31）
- `-Only ClassFeats` 8 / 8（定义、显示与前置、强度与资源上限、获取与冷却、降级暂停、法师消耗与法力上限、神恩共用、器械师冷却与总开关）。
- `-Only 'Classes,NpcClasses'` 74；`-Only Adventurer` 176；`-Only 'Feats2,FeatsBuff'` 30。
- 回归中 `Classes,NpcClasses` 第一次日志里出现 “ThingsListAt out of bounds”、第二次失败（细节被下一次覆盖），第三次全过；判断是已知的冲锋落点不稳定，与本次改动无关。

## 版本
- 同一文件夹里另一线程已经把 About.xml 改到 1.89.0；本次占 1.88.0，modVersion 保持 1.89.0，About 描述和使用说明更新日志里按版本顺序插入 1.88.0。
- 备份：`ArtSource/backup-1.87.0-classfeats`（AdventurerFeats.cs、AdventurerFeats2.cs、Classes.cs、ClassLegendary.cs、AdventurerUI.cs、About.xml、使用说明.txt）。
