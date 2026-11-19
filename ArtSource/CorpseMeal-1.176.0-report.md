# 1.176.0 吃尸体不涨饱食度 报告（2026-11-17）

问题（Peter）：炎巨神族的 Nikolay "Rubber" Bryant 吃尸体，饱食度不涨。

## 原因
游戏日志（Player.log，11:45～11:51）里他吃美洲狮、马、Murkling 三具尸体时各报了一次错：
`JobDriver threw exception in toil FinalizeIngest ... NullReferenceException at FoodUtility.GetMeatSourceCategoryFromCorpse ← AlphaMemes.IngestedNutrientPaste`。

过程是这样的：
1. 他胃口很大（Big and Small 的大体型），一口想要的营养比整具尸体还多，原版就让他把整具尸体一口吃掉，尸体随即销毁。
2. Alpha Memes 在「吃下去」之后有个补丁，要再查一下这具尸体是不是虫肉。
3. 原版查肉类的函数直接去读尸体里的那个生物，而尸体已经销毁、里面是空的，于是报空引用。
4. 报错发生在「把营养加到饱食度」之前，整个步骤中断：尸体没了，饱食度没涨。

不是 DND 的饱食 / 胃容量代码造成的。但 DND 的巨胃种族（火巨人等）和 Alpha Memes 一起用时，同样会中。

## 修法
新文件 Source/CorpseMealCompat.cs：给原版那个查肉类的函数加一个前置检查，尸体已经没有内容时直接返回「未定义」，不再报错。之后的步骤照常走，营养正常加上。

## 验证
- Build.ps1 通过；Test.ps1 -Only BodySize 35 项全过（含存读档）。新检查：正常尸体仍报出真实肉类；销毁后的尸体再被查询不再抛错。
- 按 Peter 的要求用完整 mod 列表（116 个启用的 mod，含 Alpha Memes、Big and Small）再验证：Test.ps1 -Full -Only CorpseMeal 通过。新自检（Source/CorpseMealChecks.cs）让一个玩家殖民者一口吃掉整具美洲狮尸体，文化临时带上「必须吃虫肉」「必须吃真菌」两条戒律（Alpha Memes 只在这两条都有时才去查尸体，Peter 存档里的文化正好两条都有）：没有报错，营养 5.2 加到饱食度（0 → 满），补丁被用到 1 次。
- 注意：吃完后角色会呕吐。这是 Alpha Memes 这两条戒律本身的效果——吃了既不是虫肉也不是真菌的东西就呕吐，并且先把原有饱食度清零，再加上这一顿的营养。

## 另外看到的
同一份日志里他近战打豚鼠时有一次原版近战代码的空引用（Verb_MeleeAttack.TryCastShot），只出现一次，和这个问题无关，没有处理。

## 备份
ArtSource/backup-1.175.0-corpsemeal。
