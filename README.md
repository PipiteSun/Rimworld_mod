# DND边缘大陆（LichPhylactery）

把《龙与地下城》（D&D）的种族、职业、冒险者等级、魔法物品、神祇与怪物带进 RimWorld 的大型内容模组。从最初的“命匣与巫妖”一路扩展而来，packageId 仍是 `local.lich.phylactery`，旧存档可以直接继续用。

- **当前版本**：1.99.2
- **游戏版本**：RimWorld 1.7
- **详细说明**：游戏内容、各系统数值和完整更新日志见 [`使用说明.txt`](使用说明.txt)

## 依赖

| 类型 | 模组 | 说明 |
| --- | --- | --- |
| 必需 | Harmony | 放在本模组之前 |
| 必需 | Royalty（皇权 DLC） | |
| 必需 | Biotech（生物科技 DLC） | 种族都是可遗传的异种型 |
| 可选 | Ideology（文化 DLC） | 文化之神：DND 神祇模因、戒律、庆典、神像；派系带上 DND 意识形态 |
| 可选 | Vanilla Psycasts Expanded | 器械师相关的武器制作门槛等联动（职业本身已不需要 VPE） |
| 可选 | Humanoid Alien Races（HAR） | 六臂蛇人、双头巨人、独眼巨人、五色真龙和部分传奇怪；HAR 要放在本模组之前 |
| 可选 | Odyssey、Isekai Leveling、神兽血裔·独立版、Character Editor 等 | 只在同时启用时加载 `Bridges/` 里的对应联动，不启用时本模组完全不变 |

## 安装

1. 把整个文件夹放进 `RimWorld/Mods/`，文件夹名随意（本地开发用 `LichPhylactery`）。
2. 在模组列表里启用“DND边缘大陆”，排在 Harmony、Royalty、Biotech（以及 HAR，如果装了）之后。
3. 设置在“选项 → 模组设置 → DND边缘大陆”，几乎每个系统、派系和事件都有开关。

`1.7/Assemblies/` 和 `Bridges/*/Assemblies/` 里已经带了编译好的 DLL，直接能玩。如果改了 `Source/` 里的代码，用 PowerShell 7 在 `Source` 目录运行 `.\Build.ps1` 重新编译，再完全重启游戏；`.\Test.ps1` 会启动游戏跑模组自带的自检。

## 主要内容

**种族**
- 巫妖与命匣：死灵仪式转化，喂养命匣升级，统御骷髅仆役、血肉巨像、知识死灵和石魔像，死后从命匣复生。
- 巨人谱系：风暴、云、火、霜巨人（可遗传、巨躯巨胃、阶序与世仇），以及需要 HAR 的双头巨人、独眼巨人。
- 力能龙裔、巨魔、夺心魔、牛头人、吉斯洋基、元素裔、象人、食人魔、鸟人、流浆人、阿斯莫、提夫林、坎比恩。
- 普通种族：高等 / 木 / 卓尔精灵，山地 / 丘陵矮人，半身人、侏儒、半精灵、半兽人、狗头人、地精三族。
- 传奇种族：巨灵（神灯许愿）、天穹神裔；22 个契约种族（魔鬼、妖精宫廷、天界使者、深渊恶魔等）只在“立约者”开局可选。
- 二十多个种族专属开局剧本。

**冒险者系统**
- 冒险者等级 1～20、六维属性（3.5 版负重表倍率）、购点开局，击杀、任务、工作和事件给经验。
- 传奇等级 21～40 与传奇属性点、史诗恩赐、超凡 / 神话属性特性。
- 十二个职业（战士、武僧、圣武士、野蛮人、游侠、游荡者、法师、牧师、德鲁伊、术士、器械师、邪术师）及子职业、传奇职业能力、自动施放；NPC 也会按派系拿职业。
- 通用、种族、起源和职业专属专长；强力特性、武艺天赋、DND 性格特性与诅咒之力，按稀有度上色。
- 敌人等级、精英与宿敌冠军，野兽等级与兽群首领。

**世界与事件**
- 派系：费伦拓荒同盟、塞伊远征军、余烬氏族、幽暗殖巢、黑网、红鹰商业联盟。
- 事件：传送门失控、巨人迁徙、黄昏之年、龙过境、法师塔坠落、五色龙勒索、深渊入侵、狂猎等。
- 迷锁地牢（无尽口袋地图）、探索公告板委托、耐瑟瑞尔卷轴与拉洛克任务线。
- DND 野兽和怪物（猫头鹰熊、地狱犬、锈蚀怪、眼魔、贝希尔、死亡骑士、五色真龙……）。

**信仰与超凡**
- 圣坛与 11 位神祇、神眷与神术、神赐遗物、信仰任务；成神之路通往半神与神域。
- 深渊仪式召唤与束缚恶魔，与九狱 / 妖精宗主签订异界契约。

**物品与制作**
- 研究全部集中在“DND边缘大陆”页签。
- 炼金药水、卷轴、魔法枪械与法杖、魔化兵器、约 45 件奇物、34 件 DND 衣甲与套装、制伏武器。
- DND 工作台（附魔台、卷轴书桌、符文铁砧、巨人铁砧、法袍织机）、基因工作台、附魔强化 +1～+10、血肉艺术品与皮雕。

## 仓库结构

```
About/         模组信息（About.xml、预览图）
1.7/           主程序集
Defs/ Patches/ Languages/ Textures/   游戏数据与贴图
Bridges/       可选联动（按 LoadFolders.xml 在对应模组启用时加载）
Source/        C# 源码、Build.ps1、Test.ps1
ArtSource/     贴图源文件与各版本报告（版本备份 backup-* 不进仓库）
```

---

## English summary

**DND边缘大陆 (LichPhylactery)** is a large RimWorld 1.7 content mod that brings Dungeons & Dragons into the rim: liches and phylacteries, giants, dragonborn, trolls, mind flayers and dozens of other heritable races; adventurer levels 1–40 with six ability scores, twelve classes with subclasses, feats and legendary traits; D&D factions, events, an endless pocket-map dungeon, deities and ascension, demon summoning and planar pacts; plus magic items, armor sets, alchemy and new workbenches. All D&D research lives in one research tab.

- **Version:** 1.99.2 · **Game:** RimWorld 1.7 · **packageId:** `local.lich.phylactery`
- **Required:** Harmony, Royalty, Biotech
- **Optional:** Ideology, Vanilla Psycasts Expanded, Humanoid Alien Races (load before this mod), and several other mods with small bridges that only load when present
- **Install:** drop the folder into `RimWorld/Mods/`, enable it after its dependencies. Prebuilt DLLs are included; rebuild with `Source/Build.ps1` (PowerShell 7) if you change the code.
- The in-game text is Chinese only. Full documentation and changelog: `使用说明.txt`.
