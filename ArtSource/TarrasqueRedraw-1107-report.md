# 塔拉斯克贴图重绘（2026-11-07）

只换贴图，不改 XML、不需编译，版本号不变。

参考：Peter 提供的 2024 版《怪物图鉴》塔拉斯克图 + 网上资料（四足巨兽、全身鳞甲、背上成排红褐色环纹弯角、张开的巨口和黑色长舌、钩状尾端、头上前伸和后掠的大角、多只小眼）。

画风：边缘世界原版风格，粗黑描边，平涂 + 右下阴影 + 左上高光，低饱和土褐色。

| 文件 | 旧 | 新 |
|---|---|---|
| Textures/Things/Pawn/Animal/LP_E3/Tarrasque/Tarrasque_{east,south,north}.png | 256px 绿色卡通 | 512px 重绘 |
| 同目录 Dessicated_Tarrasque_{east,south,north}.png | 256px | 512px，灰化干尸版 |
| Textures/Things/Item/LP_E3/TarrasqueShell.png | 128px | 128px 鳞甲片 |
| Textures/Things/Item/LP_E3/TarrasqueTooth.png | 128px | 128px 巨牙 |

西向由游戏自动镜像东向。drawSize 仍是 7.5（Events3.xml），512px 只是更清晰，不改变在游戏里的大小。

备份：ArtSource\backup-tarrasque-1107\（8 张旧图）。生成脚本：ArtSource\TarrasqueRedraw-1107\（paint.py + tarrasque.py，PIL + numpy）。
恢复旧图：把备份目录里的文件复制回原路径即可。

## 2026-11-08 关节衔接优化

Peter 要求关节连接处没有直接的分界线。腿、脖子、尾巴和身体之间：
- 描边在关节处渐隐（只保留腿下半部和外轮廓的描边）；
- 关节附近颜色与身体平滑渐变融合，身体本身不再画内部轮廓；
- 光影按整只怪的轮廓计算，不再在每个部件边缘各自出现高光/阴影带；
- 前景部件对身体投柔和接触阴影，代替硬线表现前后关系；
- 背面视角去掉了尾根上的那块甲片，尾巴直接从身体长出。
六张身体贴图（含干尸版）重新出图，物品图标不变。上一版（有分界线）在项目文件 tarrasque-redraw-1107/new/。
