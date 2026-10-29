# DND 建筑贴图重画（2026-10-29）

不改版本号，不需要重新编译，只换贴图并改了 4 处 XML。

## 已重画（RimWorld 原版画风：粗深色描边、平涂加阴影、低饱和、加了细节）
- 工作台（南/北/东三个朝向加材料遮罩）：附魔台、抄写台、符文铁砧、巨人铁砧、奥术织机、符文淬火槽、炼金台、神祇圣坛、契约讲台、地狱熔炉（绿火，不吃材料，所以没有遮罩）
- 单格建筑：蒸馏器、奥术聚焦石、神像
- 法阵：深渊法阵、银质深渊法阵、束缚法阵、守护符文
- 迷锁地牢：大门（开启/封印）、出口、灯、喷泉（地牢墙壁 Wall_Atlas 保持原样）
- 耐瑟瑞尔祭坛、位面裂隙、法师塔残骸
- 意识形态神像 10 尊：拉森德、提尔、密斯特拉、凯兰沃、马拉、塔洛斯、班恩、莎尔、苏尔特、万神

## XML 改动（加 `<shaderType>CutoutComplex</shaderType>`，让材料只给桌体上色，桌上的书和瓶子保持原色）
- Defs\Workbenches.xml：抄写台 ScribeDesk、奥术织机 ArcaneLoom
- Defs\Items_Alchemy.xml：炼金台 AlchemyTable
- Defs\Faith.xml：圣坛 Faith/Altar

## 备份与源码
- 原贴图和 3 个原 XML：ArtSource\backup-art-1029\（81 个文件）。想还原，把里面的文件按原路径拷回即可
- 绘制脚本（Python）：ArtSource\ArtRedraw-1029\
- 前后对比图：项目文件 art-redraw-1029/建筑贴图重画-前后对比.png

## 注意
还没有在游戏里看过。如果某个贴图显示成粉色或者材料颜色不对，把 Player.log 发过来。
