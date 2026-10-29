# DND边缘大陆 1.17.1 种族贴图报告

日期：2026-10-24。范围：半兽人、精灵、矮人、夺心魔四个种族里借用原版或旧贴图的地方，全部换成专属贴图。

## 绘制方式

这台电脑上的会话没有图像生成工具，所以贴图是用 Python（Pillow）按矢量形状程序绘制的：粗黑轮廓、平涂主色、单侧暗面，和之前重绘的巨魔、巫妖基因图标同一路子。源脚本 `ArtSource/RaceArt/draw_race_art.py`，改形状或颜色后重跑即可覆盖贴图：

```
python -I ArtSource/RaceArt/draw_race_art.py <模组根目录>
```

对照图：`ArtSource/Preview/RaceArt-1.17.1.png`（全部新图）、`ArtSource/Preview/MindFlayer-1.17.1.png`（游戏内夺心魔四向）。

## 新贴图（31 张）

| 类别 | 贴图 | 原来借用 |
|---|---|---|
| 异种型图标 | `CommonRaces/Xenotypes/HalfOrc`、`Elf`、`Dwarf`，`MindFlayer/Xenotype` | Gene_JawHeavy、Gene_EarPointed、Gene_UnisexBeards、Gene_PsychicBonding |
| 半兽人基因 | `CommonRaces/Genes/Relentless`（包扎的裂心）、`Savage`（带血斧） | Gene_Unstoppable、Gene_Killthirst |
| 精灵基因 | `ElfTrance`（闭眼与新月）、`ElfBloodline`（灵能环中的叶）、`ElfLongevity`（沙漏） | QuickSleeper、EnhancedPsychicAbility、Ageless |
| 矮人基因 | `DwarfStature`（矮人与高个轮廓）、`DwarfLiver`（啤酒杯）、`DwarfStoneEndurance`（山与镐） | BodyThin、PartialToxicityResistance、Tough |
| 夺心魔基因 | `MindFlayer/Genes/Cerebrum`、`Sterile`、`PsionicSense`、`HiveLink`、`HalfIllithid` | HemogenDrain、Sterile、EnhancedPsychicAbility、PsychicBonding、PsychicallyDull |
| 夺心魔能力 | `MindFlayer/Abilities/DevourBrain`、`MindBlast`、`ReadMind`、`Dominate`、`PsychicBind`、`TelekineticThrow`、`Unseen`、`PsychicStorm` | EntropyDump、BlindingPulse、WordOfTrust、Berserk、WordOfLove、Chunkskip、Invisibility、Neuroquake |
| 意识形态议题 | “吞噬大脑”议题复用 DevourBrain 图标 | UI/Issues/Cannibalism |
| 物品 | `MindFlayer/Items/Tadpole`、`BrainJar`、`PsionicSuppressant` | 盲视植入物染色、血原质包、青霉素类药 |
| 建筑 | `MindFlayer/ElderBrain`（512px，3×3 俯视的石池与大脑） | 死灵仪式台染色 |
| 身体 | `MindFlayer/Tentacles/FaceTentacles_south/east/north` | 无（新增） |

图标都是 256×256，建筑 512×512。物品和建筑原来的 `<color>` 染色已删掉；脑浆罐和抑灵剂从 `Graphic_StackCount` 改为 `Graphic_Single`。上古脑的“唤醒”“灌装脑浆罐”按钮图标也换成新图（`Source/ElderBrain.cs`）。

## 夺心魔面部触须

挂在 `LP_Cerebrum`（脑髓代谢）基因上的头部附着渲染节点，写法照巨魔獠牙；`colorType=Skin`，所以触须颜色跟随皮肤（夺心魔是原版紫色皮肤）。南/东/西可见，北向是空图。半夺心魔没有这个基因，所以不长触须。

## 改动文件

`Defs/CommonRaces.xml`、`Defs/MindFlayer.xml`、`Defs/MindFlayerPowers.xml`、`Defs/MindFlayerTadpole.xml`、`Defs/ElderBrain.xml`、`Source/ElderBrain.cs`、`Source/CommonRaceGallery.cs`（只改图鉴标题）、`About/About.xml`（1.17.0 → 1.17.1）、`使用说明.txt`；新增 `Textures/CommonRaces/`、`Textures/MindFlayer/`、`ArtSource/RaceArt/`。备份：`ArtSource/backup-1.17.0/`。

## 测试

`Build.ps1` 通过；Core `PASS: 1411 checks`（两次）；Appearance `PASS: 1611 checks`；VisualCompat `PASS: 11210 visual checks`。现有检查已经覆盖“异种型和自定义基因图标能加载”“能力图标能加载”“蝌蚪、上古脑有贴图”。

## 可以再改的

1. 程序绘制比原来 imagegen 重绘的图标更几何、更干净；觉得哪张不像，可以指名让我改形状，或者换回生成式重绘。
2. “支配人类”的手偏抽象，“精灵冥想”的闭眼在小尺寸下比较细。
3. 半兽人、精灵、矮人的头部外观仍然是原版基因组合，没有画专属头部（原版组合已经能区分）。
