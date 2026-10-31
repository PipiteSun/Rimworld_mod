# DND边缘大陆 1.82.0 修补：五个遗留问题

2026-10-31。版本号不变（1.82.0）。

| 问题 | 原因 | 改动 |
|---|---|---|
| 影魔半透明（1.63.0 待办） | 没做 | `AbyssRaces.cs`：`PawnRenderNodeWorker.GetFinalizedMaterial` 后缀，影魔（活着、不是头像 / 造型台 / 隐身）换成原版透明着色器、alpha ×0.5 的材质副本（按原材质缓存）。原版用 material.color 写 MaterialPropertyBlock，所以 alpha 会带过去。 |
| 地精埋伏（1.63.0 待办） | 没做 | `Cleanup163b.cs` `GoblinAmbush`：`PawnsArrivalModeWorker_EdgeWalkIn.Arrive` 前缀；敌对派系、队伍里有 `LP_FH_GoblinRaider`、到达方式是边缘走入时 25% 改成 `EdgeWalkInGroups`（分几路从不同边缘进），发红色提示。袭击信件的到达说明随之变成分路。 |
| 死亡骑士化灰（LegendaryMonsters #4） | 游戏逻辑没问题；自检加麻醉后，死亡骑士的超凡体质“硬撑”把倒地延后了 | `LegendaryMonsterChecks.cs`：先用掉本期硬撑、清传奇疼痛窗口，麻醉严重度设 1 再做状态检查 |
| 元素使·火烧伤（Feats2 #4 偶发 0 处） | 不穿甲的随机部位火焰伤害可能被衣服挡掉 | `Feats2Checks.cs`：第二下改打躯干、穿甲 2，最多三下 |
| 黄昏之年特性名（Twilight / Events） | 特性名带 `<color>` 标签，检查直接比对字符串 | `EventChecks.cs`：两处比对前 `StripTags()` |

使用说明.txt 更新日志 1.82.0 下面加了一行修补说明。

## 测试

`Test.ps1 -Only 'Cleanup,Abyss,LegendaryMonsters,Feats2,Twilight'`：PASS 236（含存读档）。新增检查：Cleanup 26（地精埋伏：10% 掷出时分路、50% 不变、没有地精不变）、Abyss 影魔半透明。
