# 1.35.0 研究页签统一：报告

需求（Peter）：所有 DND边缘大陆 的内容整合到一个科技研究界面，不要分散在原版、熔火、DND 三个界面。

## 结果
- 唯一页签：**LP_FireGiantTech「DND边缘大陆」**（保留原 defName，旧存档里这个页签下的研究进度不受影响）。
- 删除页签：**LP_ForgeTab「熔火锻造」**，它的两项研究（符文火器、奥术法杖）并入。
- 迁移数量：来自 LP_FireGiantTech 的 3 项（DND炼金术、秘金冶炼、熔核锻造），来自 LP_ForgeTab 的 2 项（符文火器、奥术法杖），原版 Main 页签 0 项（本模组没有研究放在原版页签上）；联动文件夹（VPE、HAR、神兽血裔、Isekai）0 项（都没有研究定义）。
- 「原版」部分：约 20 件 DND 装备/建筑原来直接用原版研究解锁，在原版页签的解锁列表里出现。新增 6 项本页签研究接管：巨人锻造、巨人重甲、异族军械、异族利刃、星界板甲、基因工作台（原版研究改作它们的前置）；抑灵剂改由 DND炼金术 解锁。现在页签共 11 项。
- 研究 defName 一个都没改。
- 完整布局表和解锁改动表见 `ResearchTab-1.35.0-spec.md`。

## 改动文件（备份在 ArtSource/backup-1.34.1，同样的相对路径）
- 新增 `Defs/Research.xml`：页签 + 全部 11 项研究。
- 从 `Defs/Guns.xml`、`Defs/Items_Alchemy.xml`、`Defs/MoltenGuard.xml` 删掉原来的页签/研究定义；`Source/Build-MoltenDefs.ps1` 不再生成页签和熔核锻造研究（否则重跑脚本会造出重复定义）。
- 解锁改到新研究：`FireGiant.xml`、`FireGiantEquipment.xml`、`Items_Giants.xml`、`Items_RaceGear.xml`（含精灵长弓新加的 recipeMaker 覆盖）、`Items_Races124.xml`、`Githyanki.xml`、`GeneWorkbench.xml`、`MindFlayerPowers.xml`。
- `Defs/Scenarios.xml`：火巨人剧本开局多给“巨人锻造”。
- 新增 `Source/ResearchTab.cs`：GameComponent `ResearchTabState`。旧存档第一次读取时，原版前置全部完成的新研究直接记为完成（例如已有锻造 → 巨人锻造、异族军械完成），已经能做的东西不会突然锁上；新开局不做这一步。
- 新增 `Source/ResearchTabChecks.cs`：检查组 ResearchTab。
- 旧检查按新规则更新：`GunChecks.cs`（页签改为 DND边缘大陆）、`MinotaurGithChecks.cs`（银剑 → 异族利刃，前置长剑）、`GeneWorkbenchChecks.cs`（→ 基因工作台研究，前置异种遗传学）、`ScenarioChecks.cs`（火巨人剧本有两项开局研究）。
- VPE 联动文案：`Bridges/VPE/Defs/ClassArtificer.xml`、`ClassAbilities_Artificer.xml`、`Source/Artificer.cs` 注释里“熔火锻造页”改为“符文火器/奥术法杖”。
- `About/About.xml` 1.35.0 + 简短更新说明；`使用说明.txt` 标题版本、快速上手新增“研究”一段（完整研究树）、第二部分相关句子、更新日志 1.35.0。

## 测试
- 构建：`Build.ps1` 通过（主程序集 + HAR、VPE、神兽血裔三个联动）。
- `Test.ps1 -Only ResearchTab`：**PASS 6 checks**，并做了存读档，日志没有红字。检查内容：(a) 本模组所有研究都在 DND边缘大陆 页签（11 项）；(b) 页签内任意两项坐标相差都不小于 0.5；(c) 页签内的前置都在左边；(d) LP_ForgeTab 不存在、本模组只有一个页签；(e) 本模组的物品/建筑/配方只由本页签研究解锁；(f) 旧存档迁移（只有锻造 → 只完成巨人锻造和异族军械）。
  第一次跑时 (e) 抓到精灵长弓通过原版父定义 Bow_Great 继承了“巨弓”研究，已改为异族军械。
- `-Only Items,Giants`：PASS 492。
- `-Only Guns,MinotaurGith,GeneWorkbench`：PASS 442。
- `-Only Scenarios`：存档前的 159 项全部通过（包括改过的火巨人剧本检查），读档后的“a queued scenario raid survives save/load”失败。原因是老问题，跟这次改动无关：存档准备方法叫 `PrepareScenarioSave`（单数），而 -Only 模式找的是 `PrepareScenariosSave`，所以 -Only Scenarios 时根本没放存档样本。完整测试里不受影响。没有动它。
- `-VPE`：PASS 279（职业路径，含器械师）。
- 没有跑完整套件（Peter 说测试可以简短）。熔核锻造/火巨人装备的检查组需要传角色参数，只在完整套件里跑，这次没跑；它们只检查“熔核锻造”本身，没变。

## 需要注意
- 旧存档：页签内部名没变，原来的 5 项研究进度保留；6 项新研究按上面的规则自动补完成。精灵长弓的前置从“巨弓”变成“锻造”（经异族军械）：旧存档里只研究了巨弓、没研究锻造的话，读档后要先研究异族军械才能再做精灵长弓。
- 巫妖/亡灵、龙裔/龙没有研究（靠仪式和基因），所以没有单独的横排。联动横排（Y 5.6 起）留空。
- 必须**完全重启 RimWorld**才能生效（定义和 DLL 都变了）。
