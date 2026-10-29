# DND边缘大陆 1.27.4：职业路径美术重绘报告

日期：2026-10-25。Peter 看过 1.27.0 的图标后要求“换成更像 RimWorld 画风的”，本版只改美术，游戏内容、Defs 数值和代码不变。

## 做法
- 参考 VPE 自带的能力图标（128×128，粗黑描边、平涂、右下暗部，卡其/青灰/铁锈/米白配色）和路径背景（950×1515 塔罗牌式深色底 + 黑色剪影 + 细边框）。
- `ArtSource/RaceArt/draw_class_art.py` 重写：4 倍超采样绘制遮罩，逐层“描边 → 平涂 → 暗部”叠加后缩到 128；背景按 VPE 尺寸生成；冒险者手册贴图 64×64 同风格。
- 27 个图标：战士（回气、战吼、绊摔、动作如潮、武器专精、不屈意志、旋风斩、守护姿态、战意再临）、法师（魔法飞弹、法师护甲、火球术、睡眠术、闪电束、急速术、石墙术、传送术、解离术）、牧师（治疗伤口、祝福、次级复原术、神导术、神圣打击、驱散亡灵、群体治疗、死亡守卫、复生术）。
- 背景剪影：战士 = 盾与交叉双剑 + 星；法师 = 法杖宝珠 + 星月；牧师 = 日轮十字 + 圣杯。
- `Bridges/VPE/Defs/Paths.xml`：去掉 1.27.0 写的 `width`/`height`（VPE 自带路径没有这两项，背景按 VPE 自己的方式绘制），补上 `altBackground`（指向同一张图）。

## 文件
- 新贴图：`Bridges/VPE/Textures/LP/Classes/{Fighter,Wizard,Cleric}/*.png`（27）、`Background_{Fighter,Wizard,Cleric}.png`（3）、`Manual.png`。
- 改动：`About/About.xml`（1.27.4）、`使用说明.txt`（标题 + 更新日志）、`Bridges/VPE/Defs/Paths.xml`、`ArtSource/RaceArt/draw_class_art.py`。
- 预览：`ArtSource/Classes-1.27.4-preview.png`。备份：`ArtSource/backup-1.27.3/`（旧 About、使用说明、Paths.xml、旧脚本与旧贴图）。

## 未验证
- 没有进游戏看过。Paths.xml 去掉 width/height 后背景的显示方式与 VPE 自带路径一致，但未在界面里确认。
- 没有重新跑 Build.ps1 / Test.ps1（本版没有代码与 Def 数值改动；Paths.xml 只减字段、加 altBackground）。
