# 1.68.0 立约者开局 报告

日期：2026-10-29。备份：`ArtSource/backup-1.67.0`（About、Defs、Source、使用说明.txt）。

## 改动

1. **普通开局移除契约种族**（`Source/PactStart.cs`）
   - 判定 `PactStart.IsPactRace`：异种的任一基因挂着 `LP_OutsiderExtension` 且 `banishOnDown=true`，或者是凡人血统的萨特、调换儿、坎比恩。共 22 个：
     - 九狱：链魔、骨魔、角魔、艾林魔、阿姆尼祖、寒冰魔、炎魔
     - 妖精：萨特、林仙、红帽、调换儿、狂猎骑手、狂猎首领
     - 天界：狄瓦、行星使
     - 深渊：劣魔、巴路古拉、哈兹魔、魅魔、纳巴苏、影魔、坎比恩
   - 六臂蛇人（HAR，深渊之躯 `banishOnDown=false`）早于契约系统，不算，仍可在普通开局选。
   - Harmony 转译补丁 `PactStartXenotypeMenu`：把 `CharacterCardUtility.LifestageAndXenotypeOptions`（异种下拉）、它的“任意非超凡异种”随机项 lambda，以及 `Dialog_CreateXenotype.DrawSearchRect`（自定义异种模板）里的 `DefDatabase<XenotypeDef>.AllDefs` 换成 `PactStart.Choosable()`。实测替换了 5 处。
   - 只有剧本里带 `ScenPart_PactStart` 时才列出契约种族。游戏内遇到、召唤、束缚、签约、遗传都不受影响。

2. **新剧本「立约者」**（`Defs/Pact_Scenario.xml`，`LP_PactScenario`）
   - 4 人（8 选 4）：立约者（人类，必须保留）、萨特、坎比恩、劣魔；都可以在人物编辑里换成任意种族。
   - 新组件 `LP_PactStart`（立约者开局）：开局物品是一座未安装的木制契约讲台和 3 份宗主各不相同的契约书；开局后给外来者殖民者戴上位面锚链（倒下不会被遣返，死后留下尸体）。在剧本编辑器里可以调契约书数量（0～10）和锚链开关。
   - 开局研究：位面血统、深渊学、位面契约学。
   - 开局物品：800 银、3 灵魂币、30 应急食品、15 药品、20 零部件、左轮、钢长剑，另散落 300 钢、300 木。

3. **文档**：About.xml 升级到 1.68.0 并补了说明；使用说明.txt 加了快速上手的一行、第二部分的【1.68.0 立约者开局】和更新日志。

## 测试

- `Test.ps1 -Only PactStart`：9 项全部 PASS（种族名单、补丁处数、普通开局列出 53 个异种且没有契约种族、立约者开局列出全部异种、剧本加载、其他剧本都不放开契约种族、开局研究、讲台和契约书、锚链）。
- `Test.ps1 -Only ScenarioSummaries,Pact,Abyss,PactStart`：148 PASS。
- `Test.ps1 -Only Giants`（含全部剧本说明检查，现在共 22 个模组剧本）：33 PASS。
- 还没测：在建角界面里实际点开下拉。补丁处数和 IL 都检查过了，但界面本身没有截图验证。
