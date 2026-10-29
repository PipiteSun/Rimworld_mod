# 1.58.5 血肉艺术品与皮雕 报告

日期：2026-10-27。版本 1.58.3 → 1.58.5（1.58.4 留给性能审计线程）。

## 内容
| 档 | 血肉艺术品 | 皮雕 | 用料 | 工作量 | 美观 | 尺寸 / drawSize |
|---|---|---|---|---|---|---|
| 小 | LP_FleshArtSmall | LP_LeatherCarvingSmall | 50 | 18000 | 50 | 1×1 / 1 |
| 大 | LP_FleshArtLarge | LP_LeatherCarvingLarge | 100 | 30000 | 100 | 1×1 / 3 |
| 巨型 | LP_FleshArtGrand | LP_LeatherCarvingGrand | 400 | 105000 | 400 | 2×2 / 4 |

- 全部在原版艺创台 `TableSculpting` 上做，艺术技能、GeneralLaborSpeed，出品质、CompArt（标题 NamerArtSculpture + 自己的中文描述规则包）、可打包、艺术冥想焦点、Art 交易标签。
- 皮雕：材料 Fabric / Leathery，`recipeMaker` 自动生成 `Make_LP_LeatherCarving*`，未完成品 `LP_UnfinishedLeatherCarving`，Tailor 特效与声音；灰阶贴图按材料色相乘；可上漆；生命 70/120/240、重 1.5/3.5/14、易燃性 1.2。
- 血肉艺术品：生肉不是材料，所以不写 recipeMaker，另写 `LP_Make_FleshArtSmall/Large/Grand`：MeatRaw 分类 50/100/400 块（默认过滤不勾 `Meat_Human`），未完成品 `LP_UnfinishedFleshArt`（无材料），ButcherFlesh 特效与声音。固定色贴图、不能上漆、无 CompRottable。没有材料就算不出市值，`MarketValue` 手填 165/330/1180（≈ 同量生肉 + 工作量）。
- 艺术描述：`LP_ArtDescription_FleshArt`、`LP_ArtDescription_LeatherCarving`，句式照原版 ArtDescription_Sculpture 的中文译文，[image] 等仍由原版规则包生成。

## 设计取舍
- 两个基类都直接继承 `ArtBuildingBase`、照抄 `SculptureBase` 的字段，而不是继承 `SculptureBase`：子 Def 的 `comps` 列表只会追加，继承会出现两个 CompProperties_Art。
- 皮雕同时接受布料和皮革（“皮雕”本身就是皮革工艺），玩家可在账单里只勾布料。
- 不加心情、不加 C# 逻辑，玩法与原版雕塑完全对齐。

## 文件
- 新增：`Defs/Artworks.xml`、`Source/ArtworkChecks.cs`、`ArtSource/RaceArt/draw_artworks_158.py`、`Textures/LP/Art/`（6 个 Graphic_Random 文件夹共 14 张 + 2 张未完成品）、预览 `ArtSource/Preview/Artworks-1.58.5.png`。
- 修改：`About/About.xml`（modVersion 1.58.5、描述末尾一句）、`使用说明.txt`（第一部分一行、第二部分【1.58.5 血肉艺术品与皮雕】、更新日志一行；与备份 diff 只多 8 行）、`1.7/Assemblies/LichPhylactery.dll`。
- 备份：`ArtSource/backup-1.58.3-art`（About.xml、使用说明.txt、LichPhylactery.dll）。

## 验证
- `Source\Build.ps1`：主程序集与 HAR / VPE / 神兽血裔桥全部 PASS。
- `Source\Test.ps1 -Only Artworks`：6 条全过（含存读档），日志无红字。样例：50 块牛肉做出的小血肉艺术品“节奏 No.IX9VII”，描述“这件作品用拧结的肌束向我们描述了一个营地……”；100 布做出的大皮雕，描述“这件缝制雕像描绘的是……”；两件巨型 2×2 正常放置，美观 400。
