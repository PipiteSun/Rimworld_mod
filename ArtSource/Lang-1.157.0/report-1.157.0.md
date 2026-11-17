# 1.157.0 英文语言包 报告

## 结果
- 游戏语言选 English：模组的 Def 文本（物品、种族、基因、职业、法术、事件、研究、剧本……）和代码里的界面文字都显示英文；选中文照旧显示中文。
- 共 16519 条文本全部译完：Def 字段 10167 处（`Languages/English/DefInjected`，联动桥在 `Bridges/<桥>/Languages/English/DefInjected`），代码字符串 8273 条（`Languages/English/LPStrings/*.xml`）+ 2 条手工模板（`_Manual.xml`）。
- 英文模式加载：语言注入错误 0 条。

## 做法
- 代码：`Source/LPLang.cs` 提供 `"中文".Loc()` / `"模板{0}".LocF(...)`；中文语言直接返回原文。约 1 万处字面量由 Roslyn 改写器自动包上（跳过 const、case、比较、Log、检查类等）。
- 手工修补：被误包的内部键改回原文（知识死灵专长、亲和表属性缩写/传奇项），文字比较改为本地化或中英双认（AnyTarget 拒绝原因、技能分组“其他”、反骨、摔角、契约 300 银、冒险者小队剧本、阅读进度、属性效果行、子职业行、深渊裂口前缀、扭曲肉说明、法术特效锥形/半径/直线关键词改英文正则）；两句拼接改成 LocF 模板（契约秘密、探索公告板等阶）。
- 工具链：`ArtSource/Lang-1.157.0`（export → make_pool.py → batches → tr → gen.py；`chk.py` 对齐检查；`manual_cs.tsv` 手工条目）。

## 测试
- 编译：4 个程序集通过。
- 中文：`-Only Adventurer,ReadingProgress,QuestBoard,Spellbook,AttributePage,AutoCast,CastRange,MindControl` 278 项通过（QuestBoard 奖励统计偶发随机失败，重跑通过）。
- 1.156.0 就存在的失败（与本次无关）：Faith（神器已删，relic 为空）、Pact（魔鬼钢叉 LP_InfernalPitchfork 已删）、Netheril 23（法杖已删）、EventPawn（Letter_E3 没走事件信件基类）、Classes、CasterSpells。
- 英文：`-Lang English -Only LPUI` 8 项通过，截图（升神、公告板、附魔窗口）都是英文。英文模式下按中文比对文字的检查会失败，属正常。

## 已知小问题
- 少数中文“碎片拼接”的句子英文语序稍生硬（如数量 + 单位）；附魔窗口里耐久那行英文较长被截断。
- 想加别的语言：复制 `Languages/English` 改名并翻译即可。

备份：ArtSource/backup-1.156.0-English。About.xml modVersion 按协调要求设为 1.158.0（另一线程的 1.158.0 贴图条目保留）。
