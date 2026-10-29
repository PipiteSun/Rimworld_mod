# 1.51.0 种族加点亲和 — 实现报告

日期：2026-10-26。需求：ArtSource/Leveling-1.51.0-spec.md（含 19:44 两档种族 / 稀有天赋补充和 19:46 “取较大不相乘”细化）。备份：ArtSource/backup-1.50.1（1.50.1 的完整副本，结构同前几次备份）。

## 文件
- 新增 `Source/AdventurerAffinity.cs`：
  - `LP_AdventurerAffinityExt`（strongScores、weakScores、strongLegend、legendAll，另加 `legendary` 表示传奇种族档），挂在 XenotypeDef 或种族 ThingDef 上覆盖字典条目。
  - `AdventurerAffinity`（一张表：strong/weak/legend 标记、legendAll、legendary；`StrongMult` 1.5 / 1.75）。
  - `Affinity` 静态类：亲和字典（按行文本写成，便于对照规格）、`For(pawn)` 查表、`On`（设置 + `forcedOn` 测试/比较用）、`ScoreMult` / `LegendMult`（含天赋取较大）、`Apply`（只乘正调整值）、界面用 `Tag` / `LegendTag` / `Tip` / `LegendTip`。
  - `TalentDefOf` + `Talents`：四个特性、`XpFactor`、`Strip`（传奇种族去掉；自测进程里 `StripAll` 去掉所有人的，免得随机天赋干扰其他检查）、GeneratePawn 后缀（Priority.High，早于冒险者状态生成）、TraitSet.GainTrait / RemoveTrait 后缀（刷新效果）。
  - 设置 `adventurerAffinity`（默认开）。
- 新增 `Defs/Adventurer_Talents.xml`：LP_TalentGifted 天赋异禀、LP_TalentLateBloomer 大器晚成、LP_TalentTempered 百炼成钢、LP_TalentMortalWill 凡人之志；commonality 0.15，exclusionTags LP_PointTalent。
- `Source/Adventurer.cs`：
  - `Hediff_Adventurer.AMod(a)` = 有效调整值（正调整值 × 倍率）。六维效果（BuildStage 全部 14 项）、技能学习速度（LearnFactor）、部位生命（PartFactor，含“体质为正时每级 +0.5%×调整值”）、VPE 职业强度（ClassPowerBonus）、悬停说明全部改用 AMod。`Mod()`、`Cap()`、`Eff()`、专长前置不变 → 数值、上限、门槛不受影响。
  - 亲和与天赋缓存在状态里（`Affinity`、`Talent`，不存档），`Dirty()` 清空；每 250 刻 `RefreshAffinity()` 检查种族/天赋是否变了（变成巫妖、半夺心魔、换异种）并刷新。
  - `LevelPerks`：大器晚成 21～40 级 +3 点、1～20 级每 4 级 +1；百炼成钢 20 级也给属性提升。`ASIPoints`（2 或 3），`ApplyASI(first)` = +ASIPoints，`ApplyASI(first,second)` = +(ASIPoints−1)/+1；自动加点同样处理，碰到上限时把剩下的点按顺序填。
  - 凡人之志：`GainXP` 和训练经验 ×1.25。
  - `ApplySettings`：关掉“种族亲和”时 `RescaleAffinity` 比较开关前后的部位生命（`Affinity.forcedOn`），变少的人伤口按比例缩小（同属性效果强度、传奇等级开关的做法）。
  - 设置面板“冒险者”分组加“种族亲和”勾选框（在“属性效果强度”下面）。
- `Source/AdventurerLegend.cs`：`Legend.E(h,i)` = 已投点数 × 传奇倍率；血量、近战、远程、心灵、回复、承受、工作、学习全部按有效点数算（`RangedFactor` / `DamageTaken` 改为 float），承受仍是线性、最低 5%。`EffectText(i,n,k)` 按倍率显示并标“（亲和 ×k）”。
- `Source/AdventurerUI.cs`：分页六维每项后标“擅长”（绿）/“笨拙”（红），悬停加“种族亲和（种族，档）：这项每点收益 ×1.5”；新行“种族：xx（普通/传奇种族）天赋：xx”，悬停看整张亲和表和天赋说明；传奇属性八格标“擅长”、按倍率显示效果。加点窗口：标注同上，按钮写“+2”或“+3”，悬停按钮看加点后的效果（临时加点算 EffectText，按倍率）；百炼成钢的勾选框显示“+2/+1”，先勾的 +2、后勾的 +1。投点窗口：标注、本点效果和“下一点”按倍率。分页高度 690→716。
- 检查：新增 `Source/AdventurerChecks5.cs`（11 条 + 1 条存读档），`AdventurerChecks.cs` 接入，`AdventurerChecks4.cs` 那一组临时关掉亲和（原有数值按 ×1 写）。

## 规则细节与我做的决定
- 只乘正调整值：`Apply(mod,mult) = mod>0 ? mod×mult : mod`。半兽人智力 −2 研究 −8%，和没有亲和一样。
- 两档种族照补充 A：巫妖、力能龙裔、火/霜/风暴/云巨人、双头/独眼巨人（HAR 种族和异种）、泰坦/觉醒泰坦、巨灵、夺心魔、六臂蛇人（HAR 和异种）、阿斯莫、寒冰魔/炎魔是 ×1.75；笨拙一律 ×0.5。
- **偏差：** 原规格的自检“火巨人传奇血量 10 点 = +30%”是按 ×1.5 写的；补充 A 把火巨人划为传奇种族 ×1.75，所以现在是 +35%。检查按 +35% 写，另加一条“半兽人传奇近战 10 点 = +45%”验证普通种族 ×1.5。
- 半夺心魔：表里用夺心魔的那一行（智 | 力 | 心灵学习），但算普通种族（×1.5）——它本质是人类宿主；补充 A 只点名“夺心魔”。查表顺序：巫妖 → 半夺心魔 → HAR 种族 def → 异种 → 人类。
- 人类 ×1.1 只给 `Human` 条目，也就是所有查不到的异种（原版异种、其他 mod 异种）都拿 ×1.1。
- 天赋与种族取较大（细化 19:46）：天赋异禀 = max(种族六维倍率, 1.5)，所以半兽人笨拙的智力也变成 ×1.5，精灵擅长的智力仍是 ×1.5；凡人之志 = max(种族传奇倍率, 1.5)，人类从 ×1.1 变 ×1.5。
- “种族亲和”开关只管种族倍率；天赋特性在开关关掉时照常生效（关掉时天赋异禀/凡人之志按 max(1, 1.5) = 1.5）。
- 天赋只在普通种族：GeneratePawn 后缀里去掉传奇种族身上的天赋（用 forcedTraits 生成火巨人也会被去掉，检查验证）。之后才变成巫妖的人保留天赋（不追溯去掉）。
- 大器晚成的 1～20 级额外点数在成为传奇之前就能投（投点窗口和效果只看“传奇等级”开关，不看传奇称号）；已经升过级的人获得这个特性不补发。
- 百炼成钢没有做“三项各 +1”，只支持 +3 或 +2/+1。
- 亲和不存档，读档后按种族重新查；特性由原版存档。

## 验证
- `Source/Build.ps1`：主程序集 + HAR / VPE / Mythic Bloodlines 联动均 PASS。第一次测试日志里有 “System.Boolean is not a Def” 等错误：[DefOf] 类里放了常量和 bool 字段，已拆成 `TalentDefOf`（只放 TraitDef）+ `Talents`。
- `Source/Test.ps1 -Only Adventurer`：PASS，115 条（1.50.1 是 103 条），含存读档。新增：亲和表覆盖 + 分档；精灵智力 +2 研究 +12% = 人类 +8% 的 1.5 倍、智识学习 ×1.15、人类传奇血量 10 点 +22%；半兽人智力 +2 ×0.5、−2 不乘、传奇近战 10 点 +45%；火巨人传奇血量 10 点 +35%、体质 +2 按 +3.5 算但数值/调整值/上限不变；HAR 双头巨人按 LP_Ettin 查表（体 ×1.75、智 ×0.5、传奇近战 ×1.75）；开关关掉全部 ×1 且伤口缩小；天赋只在普通种族（forcedTraits 生成的火巨人被去掉、精灵保留）、稀有 0.15、互斥；天赋异禀取较大；大器晚成 20 级前 5 点、21 级 +3；百炼成钢 20 级共 5 次属性提升、+3 与 +2/+1；凡人之志经验 +25%、人类传奇 ×1.5；存读档后亲和与天赋不变。
- `Source/Test.ps1 -VPE`：第一次在加载 mod 阶段（MVCF 之后）卡住到 360 秒超时，日志里没有错误、也还没开始跑检查；第二次 PASS，459 条。看起来是加载期偶发卡住，不是这次的改动。
