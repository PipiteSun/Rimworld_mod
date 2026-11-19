# 1.178.0 死灵仪式台面板 + 造物材料全图吸收

需求（Peter，2026-11-17）：死灵仪式台做独特界面，不要全在原版工作单清单里；强化死灵造物的材料搜索改为全图（原话说 15 格，代码里实际是 12 格）。

## 改动
- Source/AltarPanel.cs（新）：AltarPanel（分类、不可用原因、材料文字、排入/持续、可执行者、近处命匣）+ Dialog_AltarPanel 窗口。
  - 左栏：服务命匣（更换、主人、存活/复生倒计时、等级与献祭进度条、统御条、精神力、造物列表，点行跳到造物）。
  - 右上：仪式 / 献祭 / 造物 / 植入物 / 锻造与抄写 五个分页，每行图标、材料、能否排入（缺研究、没选命匣、没读魔像手册等），「排入 +1」「持续」；巫妖仪式是「指定执行者…」。
  - 右下：工作队列，↑↓、−+、暂停/继续、设置（原版 Dialog_BillConfig）、删除。
  - 底层仍是原版 Bill_Production：工人、存档、执行者限制不变。器官封存、旧“重塑巫妖身体”单不显示。
- Defs/Core.xml：LP_Altar 去掉 ITab_Bills，描述改写。Source/Gameplay.cs：仪式台多「仪式台面板」按钮，检视文字改短。
- Source/Remolding.cs：Supplies 全图（ThingsInGroup(HaulableEver)，去掉 12 格，保留可达 / 未禁用 / 未预订 / 未起雾），胚体唤醒与重塑池共用；缺料提示改成“地图上可用…不足”。
- Source/ConstructDesign.cs、Defs/ConstructSeeds.xml、MagicItems_Devices.xml、Remolding.xml：文字同步。
- 英文：新 Languages/English/LPStrings/AltarPanel.xml，改 ConstructDesign/Remolding/Gameplay LPStrings 与 DefInjected。
- 命匣仍须在台子 12 格内（没改）。

## 验证
- Build.ps1 通过。
- Test.ps1 -Only AltarPanel：111 checks PASS（含存读档），截图 ArtSource/AltarPanel-1.178.0/AltarPanel-1/2/3.png。
- Test.ps1 -Only KnowledgeFeedback,Remolding：152 PASS（Remolding 单独跑会因依赖 KnowledgeFeedback 的知识死灵 id 报错，原有问题）。
- ConstructDesign 组在同一次运行中全部通过。

备份：ArtSource/backup-1.177.0-altarui（改动前的源码、Defs、语言、About、使用说明、dll）。
