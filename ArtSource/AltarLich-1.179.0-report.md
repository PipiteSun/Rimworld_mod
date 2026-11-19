# 1.179.0 仪式台可重新安装 + 巫妖独自开局 + 开局背景

- LP_Altar：minifiedDef MinifiedThing，分类 BuildingsProduction。拆下后工作队列、选中命匣随建筑本身保存。
- 命匣初成：pawnCount 1；描述、摘要、开局对话改写（导师死于瘟疫、同门被猎巫者烧死、古墓独居多年、缝出骷髅和巨像）。
- 新背景 LP_CryptRecluse「墓室独居者」：成年背景，智识 +3 / 手工 +2 / 医疗 +1，不禁用工作（只有一个人，社交不能禁），只在剧本里出现。
- 英文：ScenarioDef/Scenarios.xml、BackstoryDef/Scenarios.xml、ThingDef/Core.xml。
- 检查：AltarPanelChecks 加两条（拆装往返保留队列与命匣；剧本 pawnCount==1）。

## 验证
- Build.ps1 通过；改动的 XML 用 minidom 解析通过。
- 游戏内检查没跑成：2026-11-18 01:14 游戏本体更新（1.7.5013 rev95，Waylith 运行时），之后 Test.ps1 任何组都卡在载入阶段 360 秒超时，包括把 Core.xml 换回旧版、只跑 Bank 组也一样，所以与本次改动无关。

备份：ArtSource/backup-1.178.0-altarlich。
