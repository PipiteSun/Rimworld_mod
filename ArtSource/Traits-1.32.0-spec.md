# 背景特性 实现要求（1.32.0）

范围：只做 8 个背景特性，进原版特性随机池。不做：阵营、信徒、种族互动、职业天赋；派系/种族加权（这一版纯随机）；给已有存档里的老小人补发。

## 共同规则
- 新文件 Defs/Traits_Backgrounds.xml，必要的 C# 放 Source/BackgroundTraits.cs，检查放 Source/BackgroundTraitChecks.cs（Test.ps1 -Only Traits）。
- 8 个特性都带 exclusionTags: LP_Background，一个人最多一个背景。
- 默认 commonality = 0.3。
- 设置页加：开关"背景特性"（默认开）：关掉时 commonality 设为 0，不再生成新的，已有的保留；滑条"背景特性出现率"0%～300%，默认 100%：commonality = 0.3 × 滑条，改设置后立即生效。
- 不会出现在：死灵造物、动物、机械体（死灵造物生成时过滤掉背景特性）。
- 心情走 ThoughtDef + requiredTraits，情境型写 ThoughtWorker。描述里写清效果数字。

## 8 个特性（中文名 / defName：效果 —— 实现提示）
1. 侍僧 LP_Background_Acolyte：冥想心灵专注获取 +20%；参加任何意识形态仪式后心情 +3"神圣时光"（1 天）；装了 VPE 时牧师路径经验 +25%。—— MeditationFocusGain +0.2；仪式结束给参与者记忆（Harmony 后缀到仪式结果，找最稳入口）；VPE 经验走 Bridges/VPE。
2. 罪犯 LP_Background_Criminal：作为囚犯时越狱几率 ×2；他做狱卒时看守的囚犯越狱几率 ×0.5（若难以按看守者归属，可改为"殖民地有罪犯背景殖民者时囚犯越狱 ×0.75"并在报告注明）；和黑网商队交易价格好 10%；作为囚犯时招募抵抗下降 ×1.3。
3. 民间英雄 LP_Background_FolkHero：被征召时 6 格内友方殖民者心情 +3"英雄在此"；他倒地时全殖民地 -3"英雄倒下"（1 天）。—— 情境 ThoughtWorker，每 250 tick 缓存，不要每次查全图。
4. 贵族 LP_Background_Noble：社交影响 +15%；disabledWorkTags Cleaning, Hauling；卧室印象低于"体面"时 -4"配不上我的身份"；装了 Royalty 时获得荣誉 ×1.2。与原版苦行 Ascetic 互斥。
5. 贤者 LP_Background_Sage：研究速度 +10%；读书收益 +30%；装了 VPE 时法师路径经验 +25%；殖民地没有任何书架和研究台时 -2（缓存判断）。
6. 士兵 LP_Background_Soldier：征召时瞄准时间 -10%（StatPart，仅征召）；和另一名士兵背景友军 ≤3 格时双方射击、近战命中 +5%（缓存）。
7. 隐士 LP_Background_Hermit：独占卧室 +3，同住卧室 -4（屏蔽原版同住心情换成我们的）；冥想心灵专注 +30%；社交互动频率 ×0.5。
8. 局外人 LP_Background_Outlander：无屋顶且不在居住区时移速 +10%（每 60 tick 判断）；采集野生植物产量 +25%；不受坏天气心情影响（disallowedThoughts）；连续 3 天一直在屋顶下 -3"闷坏了"（计时存在小人组件里，存档保留）。

## 互斥
8 个背景之间 exclusionTags；贵族 × Ascetic；不和 Isekai、人类异禀、神兽血裔任何特性互斥。

## 测试（≤15）
1. 八个特性加载、commonality=0.3
2. 生成 300 个小人没人有两个背景
3. 300 个随机小人里有背景的比例 3%～25%
4. 开关关掉后 0 个，滑条 200% 时比例上升
5. 死灵造物没有背景
6. 侍僧冥想专注 +20%
7. 罪犯越狱 ×2
8. 民间英雄征召时附近殖民者有"英雄在此"
9. 贵族不能分配清洁搬运、贵族与苦行不同时出现
10. 士兵征召后瞄准系数 0.9
11. 隐士同住 -4
12. 局外人户外 +10% 室内不加
13. 存读档后特性和局外人计时保留
