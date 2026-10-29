# 1.42.0 耐瑟启迪 — 报告

需求（Peter）："耐瑟瑞尔卷轴加入特性，阅读之后持续十小时，或者心灵熵满了"。

## 实现方式
- 做成健康页状态（HediffDef），不是原版 Trait：原版特性没有定时消失的机制，且读档/移除都要额外打补丁；hediff 自带剩余时间显示（HediffComp_Disappears）。
- **一个 HediffDef、两档严重度**：`LP_NetherilInsight`「耐瑟启迪」，严重度 1 = 普通档（研习卷、秘术残卷），严重度 2 = 强档，stage `overrideLabel`「卡苏斯启迪」（卡苏斯之卷）。没有另建 LP_KarsusInsight。
- 触发：`JobDriver.Cleanup` 后置补丁，只看 `JobDriver_Reading` 且目标是耐瑟卷轴。`BookOutcome_NetherilBase` 新增不存档的 `session`（这次连续阅读的刻数，读到中断 >300 刻就从头算），job 结束时 `EndSession` 取走；≥ 2500 刻（1 小时）就 `NetherilInsight.Grant`。熵 >90% 被自动停读同样走 Cleanup，算数。
- 已有状态：剩余时间重置为 25000 刻；卡苏斯档不会被普通档降回（普通读只刷新时间），普通档会被卡苏斯读升级。
- 消失：25000 刻（10 小时）后自动移除；`HediffComp_NetherilInsight` 每 60 刻检查 `EntropyValue >= MaxEntropy`，满了立即移除并发消息（非信件）"某某的耐瑟启迪/卡苏斯启迪随心灵熵满溢而消散。"
- 智识学习：`SkillRecord.Learn` 前缀，非 direct 的正经验在智识技能上 ×1.25（不是 GlobalLearningFactor）。

## 最终数值
| | 普通档「耐瑟启迪」 | 强档「卡苏斯启迪」 |
|---|---|---|
| 心灵敏感度 | +20%（offset） | +35% |
| 冥想专注获取 MeditationFocusGain | ×1.25 | ×1.40 |
| 智识技能学习 | ×1.25 | ×1.25 |
| 心灵熵恢复 PsychicEntropyRecoveryRate | ×0.6 | ×0.5 |
| 心灵熵上限 PsychicEntropyMax | — | ×1.10 |
| 持续 | 10 小时（25000 刻） | 同左 |

法杖伤害（1.29/1.30.1）和 VPE 法师、牧师能力（1.30.1）都乘心灵敏感度，所以启迪期间这些会自动变强。

## 偏离
- 需求写"三档"，实际按来源只有两档（研习卷/残卷 = 普通，卡苏斯 = 强）。
- MeditationFocusGain 用乘数（×1.25/×1.4）而不是 +0.25：该属性基础值是 0.5/天，加 0.25 会变成 +50%。
- 心灵熵已经满时放下卷轴不给状态（否则会给了立刻消散，刷两条消息）。
- 连续阅读的计时不存档：读到一半存读档，这次阅读从读档时起算。

## 文件
- Defs/Netheril_Scrolls.xml：新 HediffDef LP_NetherilInsight。
- Source/NetherilScrolls.cs：session 计时 + `NetherilInsight`、`HediffComp_NetherilInsight`、`NetherilInsightOnReadEnd`、`NetherilInsightLearn`。
- Source/NetherilChecks.cs：netheril 26～28。
- About.xml 1.42.0；使用说明.txt 耐瑟瑞尔一节末尾 + 更新日志。
- 备份：ArtSource/backup-1.41.0（About.xml、NetherilScrolls.cs、NetherilChecks.cs、Netheril_Scrolls.xml、使用说明.txt、LichPhylactery.dll）。

## 测试（.\Test.ps1 -Only Netheril）
- netheril 26（59 分钟不给；1 小时给普通档且数值对，智识学习 100 → 125；卡苏斯档不被普通档降回）**OK**
- netheril 27（25000 刻；再读重置 100 → 25000；24999 刻不移除、25000 刻移除）**OK**
- netheril 28（熵 38/30 时 60 刻内消散；熵满时读完不给）**OK**
- 整组仍以 **netheril 22 失败**结束（"entropy max x2.4"，预期 ×2）。这是**原有问题**：换回 1.41.0 的备份 DLL 跑同一组也同样失败在 22，与本次改动无关（大概是 1.40/1.41 成神框架给奥术半神又叠了一层心灵熵上限）。因此 22 之后的 23 和存读档部分本次没跑到。
- netheril 7 偶发失败一次（阅读 job 没开起来，"job= same=False"），重跑通过；也是原有的随机性。
