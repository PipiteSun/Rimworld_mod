# 1.35.0 研究页签统一：布局表

目标页签：**LP_FireGiantTech「DND边缘大陆」**（defName 不变，保存兼容）。删除页签：**LP_ForgeTab「熔火锻造」**。
所有研究定义集中在 `Defs/Research.xml`（原来分散在 Guns.xml、Items_Alchemy.xml、MoltenGuard.xml）。

扫描结果：本模组原有 5 项研究（LP_FireGiantTech 3 项、LP_ForgeTab 2 项），原版 Main 页签上 0 项；Bridges（VPE、HAR、MythicBloodlines、Isekai）和 Patches 里都没有研究定义或研究补丁。
「分散在原版」的实际含义：约 20 件 DND 装备/建筑/配方直接用原版研究（锻造、长剑、板甲、异种遗传学、药物生产、巨弓）解锁，所以它们显示在原版页签的解锁列表里。1.35.0 新增 6 项本页签研究接管这些解锁，原版研究改作它们的前置。

网格：X 步长 1.0，Y 步长 0.7。同一页签内的前置一律在左边（X 更小）；原版研究属于跨页签前置，不限制 X。

| defName | 名称 | 原页签 | 原坐标 | 新坐标 | 横排（主题） | 前置 | 科技 / 点数 |
|---|---|---|---|---|---|---|---|
| LP_ArcaneStaves | 奥术法杖 | LP_ForgeTab 熔火锻造 | (0, 1) | (1, 0.0) | 炼金/物品/枪械/法杖 | LP_DNDAlchemy | 中世纪 / 1000 |
| LP_DNDAlchemy | DND炼金术 | LP_FireGiantTech | (0, 1.5) | (0, 0.7) | 炼金/物品/枪械/法杖 | 药物生产 | 工业 / 1200 |
| LP_MythicMetallurgy | 秘金冶炼 | LP_FireGiantTech | (1, 1.5) | (1, 0.7) | 炼金/物品/枪械/法杖 | LP_DNDAlchemy、锻造 | 工业 / 2500 |
| LP_RunicFirearms | 符文火器 | LP_ForgeTab 熔火锻造 | (0, 0) | (1, 1.4) | 炼金/物品/枪械/法杖 | LP_DNDAlchemy、锻造 | 工业 / 1200 |
| LP_GiantForging | 巨人锻造（新） | — | — | (0, 2.1) | 巨人谱系（火/霜/风暴/云巨人） | 锻造 | 中世纪 / 600 |
| LP_GiantPlate | 巨人重甲（新） | — | — | (1, 2.1) | 巨人谱系 | LP_GiantForging、板甲 | 中世纪 / 900 |
| LP_MoltenForging | 熔核锻造 | LP_FireGiantTech | (0, 0) | (2, 2.1) | 巨人谱系（熔火） | 板甲、高级制造 | 太空 / 4000 |
| LP_RacialArmory | 异族军械（新） | — | — | (0, 3.5) | 其他种族 | 锻造 | 中世纪 / 600 |
| LP_RacialBlades | 异族利刃（新） | — | — | (1, 3.5) | 其他种族 | LP_RacialArmory、长剑 | 中世纪 / 900 |
| LP_AstralPlate | 星界板甲（新） | — | — | (1, 4.2) | 其他种族 | LP_RacialArmory、板甲 | 中世纪 / 900 |
| LP_GeneWorkbenchTech | 基因工作台（新） | — | — | (0, 4.9) | 基因工作台 | 异种遗传学 | 工业 / 800 |

预留横排（联动目前都没有研究，留空即可；以后加研究就放这里，不会与核心重叠）：

| Y | 预留给 |
|---|---|
| 5.6 ~ 6.3 | VPE 职业联动（Bridges/VPE） |
| 7.0 ~ 7.7 | HAR / 六臂蛇人联动（Bridges/HAR） |
| 8.4 ~ 9.1 | 神兽血裔联动（Bridges/MythicBloodlines） |
| 9.8 | Isekai 联动（Bridges/Isekai） |

巫妖/亡灵、龙裔/龙两个主题目前没有任何研究（它们的内容靠仪式、基因和能力解锁），所以没有单独的横排。

## 解锁改动（原版研究 → 本页签研究）

| 内容 | 原解锁 | 新解锁 |
|---|---|---|
| 熔火锻炉 LP_FireGiantForge（建筑） | 锻造 | 巨人锻造 |
| 火巨人熔炉重锤 | 锻造 | 巨人锻造 |
| 火巨人熔铁战盔、火巨人熔铁重甲 | 板甲 | 巨人重甲 |
| 霜巨人斧（LP_MakeFrostGiantAxe）、海渊珊瑚甲、云巨人晨星锤 | 锻造 | 巨人锻造 |
| 矮人投掷锤、矮人符文盾、牛头人角盔、蜕鳞甲 | 锻造 | 异族军械 |
| 精灵长弓（继承自原版巨弓的配方） | 巨弓 | 异族军械 |
| 半兽人巨斧、银剑、天穹之剑 | 长剑 | 异族利刃 |
| 星界战甲 | 板甲 | 星界板甲 |
| 基因工作台（建筑） | 异种遗传学 | 基因工作台 |
| 抑灵剂 | 药物生产 | DND炼金术 |

火巨人剧本（流亡）开局除了锻造，另外直接完成巨人锻造。
