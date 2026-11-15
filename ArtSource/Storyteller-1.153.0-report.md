# 1.153.0 叙事者「地下城主 塔莉亚」

- 定义：Defs/Storyteller_DM.xml（StorytellerDef `LP_DungeonMaster`，IncidentDef `LP_DmFateRoll`）
- 代码：Source/StorytellerDM.cs；检查 Source/StorytellerDMChecks.cs（`Test.ps1 -Only StorytellerDM`，19 项全过，含存读档）
- 备份：ArtSource/backup-1.152.0-storyteller（旧 dll、About.xml、使用说明.txt）

## 机制
- 威胁、商队、访客、任务：照原版卡桑德拉经典。
- 战役组件 `StorytellerComp_DmCampaign`：第 5 天起，平均每 3 天从本模组的事件（baseChance>0，Misc / ThreatSmall / FactionArrival）里按 baseChance 权重挑一个能触发的；第 30 天后每次有 15% 改挑 ThreatBig（真龙来袭、传送门失控、冬狼寒潮等）。目前池子 31 个事件。
- 命运组件 `StorytellerComp_DmFateRoll`：第 5 天起每 5 天整点（只在第一张家园地图）触发 `LP_DmFateRoll`，掷 d20：
  - 1：本模组大威胁，找不到可触发的就本模组小威胁，再没有就原版敌袭。
  - 2–5：本模组小威胁。
  - 6–15：只发一条消息。
  - 16–19：一个正面 DND 事件（白名单 `DungeonMaster.GoodEvents`）+ 一名殖民者灵感。
  - 20：正面事件 + 最多三人灵感 + 每人 300 冒险经验 + 空投一件魔法物品（IsekaiLoot.MagicLoot）。

## 立绘
占位图由 ArtSource/Storyteller/draw_placeholder.py 生成：Textures/UI/HeroArt/Storytellers/LP_DungeonMaster.png（580×620）和 LP_DungeonMasterTiny.png（116×124），透明底。正式图替换这两个文件即可（抠掉品红底）。

提示词：
> RimWorld storyteller portrait, hand-drawn RimWorld art style, thick dark outline, flat low-saturation fills, minimal shading, waist-up female dungeon master in a muted purple hooded robe sitting behind a folding wooden game-master screen, one hand raising a glowing pale-teal twenty-sided die, scattered notes and a small dice bag on the screen top, calm knowing smile, warm candle light, front-facing three-quarter view, centred, plain solid magenta background (#FF00FF), no text, no frame, 580x620
