# LichPhylactery 1.27.1 饱食度上限与“龙之长眠” 实现要求

设备模组路径：E:\SteamLibrary\steamapps\common\RimWorld\Mods\LichPhylactery（当前 1.27.0，已编译测试通过）。
编译/测试：在 Source 目录用 pwsh 跑 `.\Build.ps1`、`.\Test.ps1`（Core）。老的随机 flake 可重跑；读档红字 "Exception registering RimWorld.Genepack … same key" 是 1.26.0 的间歇问题，不是本次改动。
反编译：Assembly-CSharp.dll 在 RimWorld 根目录，可用 ilspycmd。动手前先核对下面“需要核对”的原版细节，以原版代码为准调整实现。

## 0. 备份
改任何文件前，把要改的文件原样复制到 ArtSource/backup-1.27.0/（保持相对路径）。

## 1. Peter 的需求
- 力能龙裔饱食度上限 500%，一次吃很多，吃饱后很长时间不用吃；饱食度掉到 225% 才去找吃的（龙可以长眠）。
- 其他大体型种族的饱食度上限也提高。
- 待 Peter 最终确认的两项按推荐值先做：①成年龙从 500% 饿到 225% 约 3 天；②大体型种族上限 = 本模组体型倍率 ×100%。数值都写成常量，方便之后改。

“百分比”定义：相对于该角色在没有本规则时的原版最大营养（原版 = MaxNutrition stat，含 StatPart_BodySize）。

## 2. 胃容量倍率 F(p)
- 成年力能龙裔（Heart 基因激活且 !ForceDragon.Juvenile）：F = 5。
- 其他（含幼年龙）：F = max(1, BodySize.Size(p) 中来自本模组的体型倍率，但对力能龙去掉龙龄那一项)。即火巨人 1+clamp01(年龄/18)（成年 2.0），巨魔成年 1.5，SmallBodyExtension bodySize>1 的基因（牛头人/象人 1.3、食人魔 1.6、巨灵/寒冰魔 1.5、泰坦/炎魔 2.5）。矮人、鸟人等 <1 的不改（F=1）。构造体 F=1。
- 实现：给 MaxNutrition StatDef 加 StatPart（仿 StatPart_BodySizeMelee 的启动注入），ExplanationPart 写“巨胃：×F”或“龙之胃：×500%”。
- 需要核对：Need_Food.MaxLevel 是否就是 MaxNutrition stat × …；stat 缓存何时刷新。F 变化（儿童成年、龙龄、基因增减）时要让 stat 刷新。

## 3. 保持百分比（防止上限变化后突然变饿）
- 每个角色记录上一次应用的 F（可存在 Need_Food 外的 per-pawn 存档组件或 GameComponent 字典里，存档键名自定）。
- F 改变时按比例缩放 CurLevel（CurLevel *= 新F/旧F），让显示百分比不突降。
- 旧存档首次加载：所有 F≠1 的角色按 F 放大 CurLevel 一次（不超过 MaxLevel）。

## 4. 力能龙裔（成年）阈值，按“常人百分比”
以 P = CurLevel / (MaxLevel/F) 计（即 500% 满）：
- P < 225%：主动去找吃的（JobGiver_GetFood 的优先级 / FoodLevelPercentageWantEat 判定），**但饥饿分类仍是 Fed，不扣心情**。
- P < 100%：HungerCategory.Hungry（原版“饥饿”心情）。
- P < 40%：UrgentlyHungry。
- P ≤ 0：Starving，原版营养不良照常。
即 CurLevelPercentage 阈值：想吃 0.45，Hungry 0.20，UrgentlyHungry 0.08。
- 需要核对：原版 Hungry/UrgentlyHungry 阈值属性名与值，JobGiver_GetFood.GetPriority 逻辑、FoodLevelPercentageWantEat 用法，FoodFallPerTickAssumingCategory 是否按分类乘系数（若 Hungry 以下减速，计算时注意）；营养不良 hediff 只在 Starving 时增长；其他读取 CurCategory 的地方（工作优先、休闲、精神崩溃、商队）不会因为 F 乱触发。
- 其他种族不改阈值（仍按原版百分比），因此大胃种族一顿撑的时间按 F 变长，饥饿速率不变。

## 5. 一次吃很多（暴食直到吃满）
- 成年龙一旦因为 P<225% 开始进食，进入“暴食”状态（存在 Gene_ForceDragonHeart，存档），持续去吃直到 CurLevelPercentage ≥ 0.95，然后退出。
- 需要核对：原版单次进食数量 FoodUtility.WillIngestStackCountOf（受 maxNumToIngestAtOnce 和 NutritionWanted 限制）。暴食状态下让 GetFood 优先级保持想吃，直到吃满；可选：对龙放宽单次数量上限，减少来回走动次数。
- 征召、精神崩溃、倒地时不强制；没食物可吃时不刷错误日志。

## 6. 饥饿速率：成年龙 500%→225% ≈ 3 天
- 原版每日营养 = 1.6 × 饥饿速率（与体型无关）。常人基准胃容量 = 原版 MaxNutrition（成年龙龙龄体型 1.0~1.2）。
- 目标总饥饿速率 H(age) = 2.75 × SizeFactor(age) / (1.6 × LongSleepDays)，LongSleepDays = 3 → 幼龙 57.3%、少年龙 60.2%、成年龙 63.0%、古龙 65.9%、伟龙 68.8%。
- Hediff_DragonAge 成年时 hungerRateFactor = H(age) / MetabolismFactor(met)（像现有幼崽那样抵消基因代谢率），使 HungerRate stat 最终 = H(age)（不计其他 hediff/装备）。若 1.7 的饥饿速率还包含其他基因倍率，按实际核对补偿。
- 幼年龙（婴儿、儿童）保持 1.23.1 的 100% 不变，F=1，阈值原版。

## 7. 界面
- 饱食度需求的 tooltip 加一行：“相当于常人饱食度 X%（上限 500%）”，大胃种族写“上限 F×100%”。
- 不强行改需求条显示（满条 = 500%）。

## 8. 测试（新增 Source/SatietyChecks.cs 并接入 Checks/Test.ps1 的 Core）
至少：成年龙 MaxLevel = 原版×5；伟龙/成年龙 HungerRate ≈ H(age)；500%→225% 用时约 3 天（模拟或按速率算）；P=300% 不想吃、P=200% 想吃且分类 Fed；P=90% Hungry、P=30% UrgentlyHungry；暴食到 ≥95% 退出；幼年龙 MaxLevel 不变、速率仍 100%；成年火巨人上限 = 原版×2、巨魔×1.5、牛头人×1.3、矮人×1；F 变化保持百分比；旧存档迁移只执行一次；P=225% 不会出现营养不良 hediff 或饥饿心情。

## 9. 文档
- About.xml：版本 1.27.1，更新日志新条目放在 1.27.0 条目之上（保留 1.27.0）。
- 使用说明.txt：1.27.1 条目放最前；力能龙裔章节的食量说明改掉（原“每升一级食量提高”等），大体型种族加“巨胃”说明。
- Defs/ForceDragon.xml 中提到食量的描述同步更新；Loxodon/Minotaur/Ogre/Titan 等描述里的“饭量”文字视情况补一句胃容量。
- 报告 ArtSource/Satiety-1.27.1-report.md：改了哪些文件、核对到的原版数值、测试结果、未做/待定项。
