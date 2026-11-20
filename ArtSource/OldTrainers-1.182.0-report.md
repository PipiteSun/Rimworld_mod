# 1.182.0 移除旧职业能力训练器（2026-11-19）

Peter：游戏内还能获得一些技能训练器，训练的是旧版 DND 技能。

## 查到的来源
- 本模组没有自定义 SkillDef，所以没有旧技能的原版神经训练器。
- 主模组自己的灵能（魂火箭、恐惧凝视等）都是 0 级，原版不给它们生成训练器。
- 唯一来源：启用 VPE 时，VPE 给每个灵能自动生成灵能训练器（ThingDefGenerator_Neurotrainer.ImpliedThingDefs 补丁），其中 79 个是 1.73.0 起已改为冒险者职业能力的旧能力（Bridges/VPE/Defs/ClassLegacy*.xml，Ability_ClassLegacy，名字带“（旧）”）。1.99.1 只把它们下架（商人/奖励/分类），定义仍在，开发者菜单等处可见。

## 改动
- Bridges/VPE/Source/VPEClean.cs：新增 `VPECleanMod : Mod`，在 Mod 构造时给 ImpliedThingDefs 挂 Priority.Last 后置补丁，筛掉赋予 Ability_ClassLegacy 的训练器 → 不再生成定义。原下架逻辑留作兜底（正常为空）。
- 保留：79 个旧能力定义和 8 条旧路径（老存档迁移成职业等级要用）。
- Bridges/VPE/Source/VPECleanChecks.cs：检查改为“训练器数量 0、筛掉数 = 旧能力数、DefDatabase/奖励/分类里都没有”。
- About 1.182.0、README、使用说明更新日志。备份 ArtSource/backup-1.181.0-oldtrainers。

## 验证
- Build.ps1 全部通过（主程序集 + HAR/VPE/Mythic 桥）。
- Test.ps1 -VPE -Only VPEClean：游戏载入完 MVCF 后卡住 360 秒超时，日志里没有本改动相关报错。和 1.180.0 记录的“游戏 1.7.5013 更新后 Test.ps1 卡在载入”是同一个问题，所以这一版只验证了编译。
- 老存档如果还留着这些训练器物品（1.99.1 起已拿不到），读档时会有一次找不到定义的提示，物品消失。检查了 Peter 最近的自动存档，里面没有。
