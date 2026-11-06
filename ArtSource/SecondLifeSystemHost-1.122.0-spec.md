# 1.122.0 二周目 与 系统宿主 实现要求

Peter 2026-11-06 原话：「完成 二周目。系统宿主 做如下修改：可以直接调用‘百战觉醒’的UI和代码，然后把属性点奖励改成1，特性奖励改成 修改特性的（把同样等级的特性直接修改成自选的另一个的） 宝物，随机物品奖励保持不变」。
设计来源：Claude Doc「DND网文风格新内容构思」第一节。所有决定由线程自己做，不问 Peter；偏差写进报告「决定」一节。

## 〇、版本与协作规则
- 版本 1.122.0（设备当前 1.121.0）。备份改动前的文件到 ArtSource/backup-1.121.0（含 DLL）。报告写 ArtSource/SecondLifeSystemHost-1.122.0-report.md，本文件拷贝为 ArtSource/SecondLifeSystemHost-1.122.0-spec.md。
- 另一个线程（普通属性天赋改为整体倍率，热修不升版本）可能正在改 LichPhylactery 的 Source 并编译。不要改它的文件（大概率是 AdventurerAffinity.cs / AttributePage.cs / Adventurer.cs 的属性天赋部分）；必须改 Adventurer.cs 时只加最小的钩子。编译前确认：没有 pwsh Build.ps1 / Test.ps1 进程在跑，且 Source 里除本版本文件外近 5 分钟没有新改动；否则每 2 分钟再看一次，最多等 30 分钟。
- 游戏必须关闭才能编译；RimWorld 在运行就在会话里说明并等待。
- 只跑定向测试：-Only SecondLife、-Only SystemHost、-Only TraitRarity、-Only Adventurer。不跑全套。
- 玩家可见文字不出现 d20 / 掷骰 / 检定 / DC，概率写「成功率 X%」或「X% 几率」。
- 不修改 IsekaiKillMilestones（百战觉醒）模组本身。「调用百战觉醒的 UI 和代码」= 把它的 MilestoneWindow / ProgressStore.Claim / Rewards 的结构移植进 DND 模组（新文件 Source/SystemHost*.cs），这样不装 Isekai 也能用。参考源码：Mods/IsekaiKillMilestones/Source/MilestoneWindow.cs、Progress.cs。

## 一、二周目 LP_SecondLife
- TraitDef，标签「二周目」，稀有档（TraitRarityExt tier Rare，名字蓝色，按 trait_rarity.py 流程同步）。进入普通特性池，commonality 0.1。描述：「前世走到过更远的地方。这一世重走一遍，进步飞快，偶尔想起前世的事。」
- 前世等级：第一次拥有此特性时随机 8～14，存档保存（放在 LP_Adventurer 的 hediff 字段或独立小 Comp，线程自选）。
- 冒险者等级低于前世等级时，所有冒险者经验 ×2（在 Adventurer.GainXP 里乘，放在其他倍率之后；传奇等级 21+ 不受影响）。达到前世等级后提示信件「前世的路走完了」，之后无加成。
- 前世记忆：等级 ≤ 前世等级的每次升级，25% 几率触发一项（各半）：当前正在研究的项目进度 +30% 该项目花费（没有研究就改为技能）；随机一项技能热情 +1（无→小→大，已大火则换一项，全满则跳过）。发信件说明。
- 悬停说明显示「前世等级 N，当前 M 级，经验 ×2 / 已结束」。
- 没有冒险者系统（总开关关闭）时特性只显示，不生效。

## 二、系统宿主 LP_SystemHost
- TraitDef，标签「系统宿主」，史诗档（紫）。普通特性池 commonality 0.05。描述：「脑海里有个声音不停发布任务。」
- 任务循环：宿主每 7 天收到一次「任务发布」，在系统窗口里从 3 个随机任务中选 1 个接受（也可以不接）。期限 7 天。同一时间只有一个任务。
  - 任务类型（数值按冒险者等级 L）：击杀敌人 3+L/2 个；完成制作 10+L 次；治疗他人 6 次；收获作物 15+L 次；研究点数 1500+150L；救起倒地的同伴/带回囚犯 1 次（线程可换成别的易判定类型）。
  - 进度用 pawn.records 快照差值统计（Kills、ThingsCrafted、TimesTendedOther、PlantsHarvested、ResearchPointsResearched 等），尽量不加 Harmony。
  - 完成：加入一次待领取奖励，弹窗（同百战觉醒：排队，没有别的系统窗口时弹出）。
  - 超时：上 3 天「系统惩罚」hediff（全局工作速度 ×0.85）。主动放弃不罚。
  - 失去特性后不再发任务，已有的待领取奖励仍可领取（同百战觉醒）。
- 系统窗口：照搬 MilestoneWindow 布局（840×710，强制暂停，关闭可暂存，候选不重抽，确认对话框，领取记录滚动区）。标题「<名字> · 系统宿主」；上方显示当前任务和进度条，或「接受任务」三选一；下方三个奖励框：
  1. 属性成长：「+1 属性点」，加到冒险者可分配属性点（现有 asiLeft 一类字段，与升级属性点同处分配）。按钮「打开冒险者分页」。没有 LP_Adventurer 时此项禁用并说明。
  2. 改命符：「改命符 ×1」，放入背包。
  3. 随机物品：与百战觉醒完全相同——在发奖时从 Isekai_SmallManaCore、Isekai_ManaCore、Isekai_BigManaCore、Isekai_ReinforcementCore、Isekai_ManaEssence、Isekai_StarFragment 中随机一种 ×1 放入背包。没装 Isekai 时改从 DND 物品池抽（治疗药水、强效治疗药水、英雄药水、位面残渣、秘银 ×10 一类，线程定），并在报告注明。
- 宿主身上有小人按钮「系统面板」打开窗口（不用浮动菜单）。按钮图标和改命符贴图按 RimWorld 手绘风（粗描边、平涂加阴影、低饱和）。

## 三、改命符 LP_FateRewriteCharm
- 物品，市场价 2000，不出现在商人和掉落里（只由系统宿主奖励和开发者模式获得）。
- 使用：对持有者或任何玩家殖民者使用，打开「改命」窗口（不用浮动菜单）：左列该人的特性，右列与所选特性「同一档位」的可选特性，选中后确认，旧特性换成新特性，消耗 1 个改命符，发信件。
- 档位：TraitRarity.Tier(def, degree)；没有 TraitRarityExt 的特性（原版等）算普通档。光谱特性按程度各自算档位，右列可以选到具体程度。
- 标记必须一致：诅咒只换诅咒，普通特性只换非诅咒；事迹（Deed）不能换出也不能换入。
- 不可改的特性（左列灰显）：基因赋予的特性、Isekai_ 开头的特性、百战觉醒 IKM_*、系统宿主本身、成神/天命等由剧情授予且有存档数据的特性（线程看代码定清单，写进报告）。右列也不出现这些。
- 右列排除：已拥有的、和其余特性冲突的（conflictingTraits、exclusionTags）、该种族不允许的。

## 四、测试（新组）
- SecondLife：特性存在且档位正确；前世等级 8～14 且存档后不变；低于前世等级经验 ×2，达到后 ×1；传奇等级不受影响；前世记忆触发能加研究进度或热情。
- SystemHost：发任务、接受、记录差值推进、完成后生成奖励；三种奖励各能领取（属性点 +1、背包里有改命符、随机物品来自正确池）；同一奖励不能领两次；超时上惩罚 hediff；存档读档后任务和奖励保留；没有 Isekai 时随机物品走 DND 池。
- 改命符：同档位替换成功、跨档位不可选、诅咒只换诅咒、事迹和基因特性不可选、冲突特性被排除、使用后物品消耗。
- TraitRarity 组仍全过（新特性都有 ext）。

## 五、收尾
- About.xml 版本 1.122.0 + 更新日志一行；使用说明.txt 加一段（精确锚点编辑）。
- 报告写：改动文件、测试结果、决定与偏差、未在游戏里看过的部分。
- 完成后不推 GitHub（Peter 没要求）。
