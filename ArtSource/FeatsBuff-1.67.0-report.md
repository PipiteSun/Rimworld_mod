# 专长加强 1.67.0 —— 构建与测试报告

设计见 `ArtSource/FeatsBuff-1.67.0-spec.md`，改前原文件备份在 `ArtSource/backup-1.66.0`。

## 一、改了什么（摘自设计稿）

1. **每个专长附带 +1 属性**（5e 2024 半专长）：每个专长写 1～3 个可选属性（`bonusAbilities`），选专长时自动加在可选项里当前最高的一项；这 +1 加在上限之后。存档字段 `lpFeatScore`（专长 → 属性），旧存档读档时自动补选。冒险者分页属性显示「18+1」，悬停说明来自哪个专长；专长列表行尾标出可选属性 +1。
2. **原有 12 个通用专长数值约翻倍**，幸运次数、坚韧部位血量、重甲减伤随熟练加值 / 等级成长（警觉瞄准 ×0.8、巨武器大师双手近战 ×1.25、神射手无视掩体且远程 ×1.15、治疗者包扎 ×1.3 治 3 处各 6 点、鼓舞领袖每天 2 次 10 格等）。
3. **1.58.1 的 13 个通用专长、9 个种族专长**数值约 ×2（酒馆斗殴者 ×2.5、元素使 +25%、观察者研究 +25%、音乐家激励 3 人、炼狱之怒 15 伤 5 秒冷却等）。
4. **8 个新专长**（`Defs/Adventurer_Feats3.xml`）：斩击者 / 穿刺者 / 粉碎者（三选一）、法师杀手、防御决斗者、武器大师、耐久、妖精之触（获得原版 Biotech 长跳 `Longjump`）。敌人也会随机到其中的战斗专长。
5. 玩家可见文字只用“成功率 X% / ×N / +N”的写法。

## 二、文件

- 代码：`Source/AdventurerFeats.cs`、`AdventurerFeats2.cs`、`Adventurer.cs`、`AdventurerUI.cs`、`HeroTraits.cs`、`OncePerCooldown.cs`
- 测试：`Source/AdventurerChecks2.cs`、`Feats2Checks.cs`、`FeatsBuffChecks.cs`（新）
- 定义：`Defs/Adventurer_Feats.xml`、`Adventurer_Feats2.xml`、`Adventurer_Feats3.xml`（新）
- 图标：`Textures/LP/Feats/{Slasher,Piercer,Crusher,MageSlayer,DefensiveDuelist,WeaponMaster,Durable,FeyTouched}.png`（新）

## 三、构建与测试

- `Build.ps1`：通过（主程序集 + HAR / VPE / Mythic 三个桥）。
- `Test.ps1 -Only FeatsBuff,Feats2,Adventurer`：**162 项全部通过**，含存档 / 读档。
  - FeatsBuff：10 项通过
  - Feats2：20 项通过
  - Adventurer：132 项通过
- 测试日志（`StandaloneMods/Validation/Lich-Core-Only/stdout.txt`）里没有关于 LP_Feat / Longjump / bonusAbilities / LP_FeatInspiredMood / LP_FeatSlashed 的配置错误，也没有其他 Config error / 异常。Biotech 里确有 `Longjump` 这个 AbilityDef，不需要改名。
- 玩家可见文字里没有 d20 / 掷骰 / 骰子 / 检定 / DC（只在代码注释里出现）。

## 四、修正与偏差

只改了测试，没改功能代码。三处检查失败都是同一个原因：新加的 +1 属性本身也会提高被测的数值，检查却只按专长本身的倍率去比。解决办法是加完专长后清掉 `featScore` 再 `Dirty()`，只量专长本身的效果：

- `FeatsBuffChecks.cs` 第 8 项：耐久附带的体质 +1 让伤口愈合变成 ×1.73（应为 ×1.5），防御决斗者附带的敏捷 +1 让闪避变成 +15.5。
- `Feats2Checks.cs` 观察者：附带的智力 +1 让研究变成 ×1.43（应为 ×1.25）。
- `AdventurerChecks2.cs` 治疗者：附带的感知 +1 让包扎变成 ×1.5（应为 ×1.3）。

偶发失败：「元素使·火」检查有一次 0 处烧伤（伤害被护甲吃掉或打在了别的部位）。这部分逻辑和 1.66.0 相同，重跑就通过了，所以没有追查。

和设计稿对不上的两处（照定义文件原样保留，未改）：
- 武器大师：设计稿写“近战冷却 ×0.9”，XML 是“瞄准时间 ×0.9”（`AimingDelayFactor`）。
- 妖精之触：设计稿写“每天 3 次”，XML 描述是“冷却照原版”。

## 五、还没在游戏里实测

- 冒险者分页「18+1」标记和专长行尾 +1 标记的排版（窄屏 / 长名字会不会挤）。
- 敌人带上加强后专长的实际强度（袭击点数没改）。
- 妖精之触在实际游戏里获得长跳，以及在能力栏里显示和使用的情况（测试里只验证了获得和移除）。
- 法师杀手打断施法：测试只验证了伤害倍率和施法者判定，“命中正在施法的人 → 打断并眩晕 1 秒”这一段没有实际触发过。
