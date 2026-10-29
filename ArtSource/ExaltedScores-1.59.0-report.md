# 超凡属性特性 1.59.0 报告

规格：ArtSource/ExaltedScores-1.59.0-spec.md　备份：ArtSource/backup-1.58.6（整个模组，除 ArtSource/backup-*）　版本：1.58.6 → 1.59.0

## 做了什么

- **Source/Exalted.cs（新）**：`Exalted` 静态类（掩码计算、登记、属性、信件、周期、各项规则）+ 12 个 Harmony 挂钩 + 设置。
- **Source/ExaltedChecks.cs（新）**：`-Only Exalted`，27 条检查 + 1 条存读档 = 28。
- **Defs/Exalted.xml（新）**：`LP_ExaltedStagger`（踉跄：移速 ×0.1、MeleeCooldownFactor ×1.5，90 刻）、`LP_ExaltedPresence`（威仪：4 档严重度）、`LP_ExaltedPresenceMood`（ThoughtWorker_Hediff，+3/+3/+6/+6）。
- **Textures/LP/Exalted/**：12 张 128×128 徽记（Str/Dex/Con/Int/Wis/Cha + `_Mythic`），由 `ArtSource/RaceArt/draw_exalted_159.py` 生成（沿用 draw_class_art.py 画风：金框纹章，神话外圈紫焰）。
- **改动的已有文件**
  - `Adventurer.cs`：Hediff_Adventurer 新字段（exaltedMask/mythicMask 不存档；exaltedSent/mythicSent/exaltedInit/holdUntil/holdStart/holdUses/scarTick 存档）；BuildStage 开头算掩码、登记、合进属性；TickInterval 硬撑到期判定 + 250 刻 `Exalted.Tick`；PostRemoved 清登记；FinalizeInit 调 `Exalted.Reset()`；`Adventurer.Registered`；设置 Expose 和“冒险者”分组里画两项。
  - `AdventurerUI.cs`：六维 ≥20 金色 / 神话紫色 + 悬停说明；新小节「超凡」（徽记 + 名字，暂停的灰色，缺图时画文字徽记）。
  - `AdventurerFeats.cs`：哨兵借机攻击 `Feats.Moved` 里疾影同灵活。
  - `AdventurerEnemies.cs`：`Cost` 乘 `Exalted.CostFactor`（超凡 +4%、神话 +8%）。
  - `MindFlayer.cs`：`PsychicStrike` 先知免疫、明察眩晕 ×0.5。
  - `Growth.cs`：典籍研读计时 ×1.3（洞彻心智）；`Gain` 派系声望 ×1.5（君临）。
  - `NetherilScrolls.cs`：耐瑟卷轴研读进度 ×1.3。
  - `About/About.xml`：1.59.0 + 一句；`使用说明.txt`：第一部分概述一行、第二部分【1.59.0 超凡属性特性】、更新日志一段（三处精确锚点各插一次；与备份 diff 只多 23 行、0 行删除）。

## 决定

- **掩码**：`Exalted.Compute(h)` 看 `h.Eff(a)`（有效值 = 冒险者系统最终数值，超过上限不算）；先判原始值 <20 直接跳过，不算上限。合并成一个 int（低 6 位超凡、高 6 位神话），按 thingIDNumber 存进并发字典（属性计算可能不在主线程），另记人数 `Exalted.Count`。所有挂钩第一步看 `Count==0`（威仪相关看 `chaCarriers.Count==0`）。
- **开关**：`On = adventurer && exaltedScores && (!EffectsHandedToIsekai || exaltedCoexist)`；关闭时掩码为 0。
- **数值不乘“属性效果强度”、不乘种族亲和**，放在 BuildStage 最前面（共存模式勾选覆盖时，六维效果关着、超凡照样生效）。
- **信件**：每个冒险者状态第一次算掩码（老存档读档、刚生成的人）只记基准（exaltedInit），之后新得到的项各发一封（神话另一封），只发给玩家阵营。
- **神话传奇点**用已投点数，不乘亲和。神话的项一定也是超凡项；战斗力折算里神话项按 +8%（不再另加超凡的 4%）。
- **硬撑**：`ShouldBeDowned` 后缀——第一次本该倒地时开一个窗口（480 / 720 刻），窗口内返回 false；到期由冒险者状态 TickInterval 比较时间戳后调 `CheckForStateChange` 重判（重判期间不再开新窗口）。次数按“本轮开始的刻”计，每 60000 刻一轮。
- **心灵效果减半**：hediff（心灵震荡、夺心魔支配、群脑恍惚、妖精魅惑）在 AddHediff 后缀里把剩余时间减半；心灵震爆的眩晕在 PsychicStrike 里 ×0.5；控制类精神状态（恐慌、狂暴、支配、魅惑等，非心情引起）由携带者 250 刻检查把剩余时间减半，每个状态只减一次。
- **显形**：携带者每 250 刻对半径内敌人里带原版隐身组件的调 `DisruptInvisibility`，并记“显形到 now+310 刻”，由 `IsPsychologicallyInvisible` 后缀读取（原版被打断后的显形窗口可能短于 250 刻）；伪装的魅魔走 1.54 的 `AbyssEvents.RevealSuccubus` / `Guise.Set(false)`。
- **威仪光环**：一人一个 hediff，严重度 1（威仪）/ 2（君临），本轮已被别的携带者刷新过就取较高的一档，不叠加。持续 560 刻、每 250 刻刷新。
- **君临逃跑**豁免：剧情首领（storyLevel）、领队（IsLeader）、宿敌冠军、神明附体者、神力灌注 ≥3、传奇怪物。

## 偏差（构思与设备冲突，以设备为准）

1. **“远域之触”在设备上不存在**（1.58.0 没有这个特性）。心灵抗性的重叠只有心智堡垒（免疫控制）和法术抗体（50%）：它们挡下的效果不会落地，没落地的也就谈不上减半，自然“取一次”。
2. **“和鼓舞领袖取一次”**：设备上的鼓舞领袖给的是伤害护盾（LP_FeatInspired），不给心情和崩溃阈值。定为：正被“鼓舞”的人只拿威仪心情，不再降崩溃阈值。
3. **先知的袭击预警只发信，不延后**：原版 `IncidentWorker_Raid.TryExecuteWorker` 被任务和本模组的袭击记录（AdventurerRaidStart 等）当作“执行完立刻有袭击者”来用，排队延后会让这些调用拿到空的袭击组，读档时还会重复延后。改为袭击生成后立刻来一封“先知的预感”（来者人数、领头的、最高冒险者等级、带超凡的人数）。
4. **疾病封顶**：1.7 原版的食物中毒、肠道蠕虫、肌肉寄生虫没有 lethalSeverity（不会致死），名单里保留但不起作用；流感、瘟疫、疟疾、睡眠病停在 lethalSeverity − 0.02。封顶和金刚不坏的失血封顶（0.58）都在 250 刻检查里做，极端大出血时最多越过一个周期的量。
5. **“击倒类 hediff”**：原版和本模组都没有击倒 hediff（图腾之魂“扑倒”是眩晕）。撼山免疫 LP_ExaltedStagger 和 defName 含 “Knock” 的状态，不挡眩晕；说明文字写“免疫踉跄和击倒类状态”。
6. **读书速度**：原版书用 `ReadingSpeed` 统计（×1.3）；属性典籍按研读计时（OnReadingTick 的刻数 ×1.3）；耐瑟卷轴按 `ProgressRate` ×1.3。研习卷（每小时给冒险者经验）不算“研读速度”，没改。
7. **借机攻击**：设备上的借机攻击就是“哨兵”专长，疾影复用灵活的判定（Feats.Moved）。
8. **说服 +15%**：`NegotiationAbility` 和 `ConversionPower`（文化 DLC 不在时跳过）各 ×1.15。
9. **精神状态减半有延迟**：控制类精神状态不在开始那一刻减，而是携带者下一次 250 刻检查时把剩余时间减半（很多来源在开始状态之后才设持续时间，开始时减会被覆盖）。
10. **徽记**是脚本画的扁平徽章（draw_class_art 画风），不是手绘；缺图时 UI 有文字徽记兜底。
11. **新加入、已经 ≥20 的人不来信**（第一次计算只记基准）——与“旧档补算不发信”用同一条规则。

## 新增 Harmony 钩子与周期代码

| 钩子（Exalted.cs） | 目标 | 快速返回 | 频率 |
|---|---|---|---|
| ExaltedDamage（前缀，High） | Pawn.PreApplyDamage | `Count==0`；伤害不伤血 | 每次伤害 |
| ExaltedBuilding（前缀 + 后缀） | Thing.TakeDamage | `Count==0`；前缀非建筑、后缀非人 / 非近战 | 每次伤害 |
| ExaltedHediffAdded（前缀 + 后缀） | Pawn_HealthTracker.AddHediff | `Count==0`；前缀先查撼山掩码再比 def | 每次加状态 |
| ExaltedShot（后缀） | ShotReport.HitReportFor | `Count==0`；近战 verb | 射击 / AI 评估 |
| ExaltedMeleeHit（后缀，HigherThanNormal） | Verb_MeleeAttack.GetNonMissChance | `chaCarriers.Count==0` | 每次近战 |
| ExaltedHold（后缀） | Pawn_HealthTracker.ShouldBeDowned | `!__result`、`Count==0`、已倒地 | 状态变化时 |
| ExaltedNoDecay（前缀） | SkillRecord.Interval | `Count==0` | 技能衰退间隔 |
| ExaltedLearn（前缀 + 后缀） | SkillRecord.Learn | `Count==0`、xp≤0、掩码没有智（后缀看 `__state<0`） | 每次学习 |
| ExaltedDowned（后缀） | Pawn_HealthTracker.MakeDowned | `chaCarriers.Count==0` | 倒地时 |
| ExaltedKill（前缀 + 后缀） | Pawn.Kill | `chaCarriers.Count==0` | 死亡时 |
| ExaltedProphet（前缀 + 后缀） | IncidentWorker_Raid.TryExecuteWorker | `Count==0`、非玩家家园、地图上没有先知 | 袭击时 |
| ExaltedReveal（后缀） | InvisibilityUtility.IsPsychologicallyInvisible | `!__result`、`revealed.Count==0` | 隐身判断 |

已有代码里的调用点：BuildStage（算掩码 / 登记 / 属性，只在属性缓存重建时）、Feats.Moved（哨兵已决定出手后才查）、AdventurerEnemies.Cost（生成袭击组时）、MindFlayer.PsychicStrike、典籍 OnReadingTick、耐瑟 ProgressRate、Growth.Gain（`Count==0` 直接返回）。

周期：
- **Hediff_Adventurer.TickInterval（已有）**：每次比较一个 int（`holdUntil>0 && now>=holdUntil`）；原有的 250 刻钩里加 `Exalted.Tick`：重算掩码（原始值都 <20 时 6 次比较）、与缓存不同才 Dirty、补登记、发信；掩码为 0 到此返回。没有新增每刻扫描。
- **携带者自己（250 刻）**：体 → 遍历自己的状态封顶疾病 / 失血、10 天消伤疤；感 → 精神状态减半（自己）+ 显形（遍历本图已生成小人，按距离平方过滤）；魅 → 光环（遍历本阵营已生成小人）。
- 计时都用 `GenTicks.TicksGame` 时间戳：硬撑（holdStart / holdUntil，存档）、金刚不坏消伤疤（scarTick，存档）、无影落空（evadeTick，不存档）、显形到期（revealed，不存档）。

## 测试结果

最终 DLL：
- `-Only Exalted`：**PASS 28 / FAIL 0**（含存读档）
- `-Isekai -Only Exalted`：**PASS 28 / FAIL 0**（共存模式分支实际跑到：默认掩码 0，勾选后巨人之力生效）
- `-Only Adventurer`：**PASS 123 / FAIL 0**
- `-Only Growth`（改了典籍计时和声望）：**PASS 13 / FAIL 0**
- `-Only MindFlayer`（改了心灵震爆）：**PASS 170 / FAIL 0**
- `-Only Netheril`（改了卷轴进度）：31 条通过后停在已知的老问题 netheril 22（1.42.0 起就有，没为它改代码）
- 没改 HeroTraits / Feats2 的代码，没跑这两组。

开发中修过的问题：1.7 的 XpTotalEarned 不含当级经验（检查改用 xpSinceLastLevel）；`Learn(direct)` 仍乘热情系数（举一反三改用 ignoreLearnRate，按所需经验的 10% 精确给）；原版隐身被打断后的显形窗口短于扫描周期（加“显形到期”表）；读档时属性缓存在 FinalizeInit 清登记之前就建好了（Reset 后按已算好的掩码重登，250 刻检查也补登记）。

## 游戏内未验证

- 冒险者分页的金色 / 紫色数字、「超凡」小节排版和徽记在实际分辨率下的样子；设置窗口两项的位置。
- 真实战斗里的硬撑（没有直接倒地的伤害组合下 8 / 12 秒后正确倒地；撑住期间意识为 0 的表现）。
- 真实近战里的踉跄、穿透、撼山拆墙；无影“落空”飘字；威仪 −8% 对 AI 的影响。
- 影魔 / 其他模组隐身单位在明察 / 先知附近的显形表现；魅魔被看穿的信件。
- 君临击倒 / 击杀时的逃跑；先知的预感信（含任务袭击）。
- 天才对无热情技能的实际学习速度、举一反三在长期工作中的累积。
- 长时间运行的性能（只在自检里跑过路径，没有做 A/B 计时）。
