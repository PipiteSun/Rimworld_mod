# DND边缘大陆 1.47.0 DND工作台 报告

2026-10-26 · 按 `ArtSource/Workbenches-1.47.0-spec.md` 实现。开工时设备版本 1.46.0（About.xml 核对），没有 RimWorld 进程在跑。备份在 `ArtSource/backup-1.46.0`（Defs、Patches、Source、About、Bridges 非贴图文件、使用说明.txt、LoadFolders.xml、1.7/Assemblies，共 316 个文件）。

## 做了什么

- **5 张新工作台 + 4 个附件**（`Defs/Workbenches.xml`），defName、尺寸、造价、研究、工作类型都按规格：
  - 附魔台 `LP_EnchantingTable` 3×2（附魔学，制作，紫光半径 3）
  - 卷轴书桌 `LP_ScribeDesk` 2×1（卷轴抄写，**研究**工作，暖光半径 2）
  - 符文铁砧 `LP_RuneAnvil` 3×2（符文锻造，锻造，木/化学燃料只在工作时每天 40，橙光）
  - 巨人铁砧 `LP_GiantAnvil` 4×3（巨人锻造，锻造，不发光）
  - 法袍织机 `LP_ArcaneLoom` 3×1（法袍织造，裁缝，淡蓝光半径 2）
  - 附件：蒸馏器 `LP_Alembic`（→炼金桌 +15%）、奥术聚能石 `LP_ArcaneFocusStone`（→附魔台、书桌 +15%，微光）、符文淬火槽 `LP_RuneQuenchTrough` 2×1（→两张铁砧 +10%）、守护神像 `LP_DeityStatue`（→圣坛 +10%、美观 10、祈祷娱乐 +20%）。每张台各最多连一个。
  - 炼金桌、圣坛加了 `CompProperties_AffectedByFacilities`；5 张新台都能连原版工具柜。
- **3 项研究**（`Defs/Research.xml`）：符文锻造 (0,1.4) 500、卷轴抄写 (2,1.4) 1000、高阶卷轴 (4,1.4) 2500；规格里列的现有研究描述都改了，写上新工作台。
- **6 张卷轴配方**（`Defs/Recipes_Scrolls.xml`）：材料、智识门槛、工作量、研究都照规格表。
- **配方搬家**：只改了 `recipeUsers`，配方和物品的 defName 都没动（归属表见下）。
- **代码** `Source/Workbenches.cs`：
  - `StatPart_WorkbenchWorker` 挂在 `WorkTableWorkSpeedFactor` 上（启动时注册，写法和 BodySize 一样）：正在交互格上做工作单的工匠体型 ≥1.8 时巨人铁砧 ×1.25；在织机上做 5 件种族披风时 ×1.15。原版 `Toils_Recipe.DoRecipeWork` 每 tick 都读这项值、不缓存（看过 IL）。
  - 鉴定：`CursedItems.Bench` 改成接受研究台**或**卷轴书桌（`Workbenches.CanIdentifyAt`），等待时间 `IdentifyTicks(bench)`：书桌 3333 tick（÷1.5），研究台照旧 5000。
  - 守护神像：`JobDriver_Pray` 把 `PrayJoyFactor(altar)` 传给 `JoyTickCheckEnd` 的 extraJoyGainFactor。
  - 读档组件 `WorkbenchMigration`（GameComponent，存 `workbenchVersion`）：新游戏直接记为 1；旧存档第一次读档时补完研究、清孤儿工作单、发一封信，然后记为 1，以后不再执行。
- **其它代码小改**：`Faith.cs` 附魔台、卷轴书桌的产物也算密斯特拉的神眷事迹（和炼金桌一样）；`HARGiants.cs` 独眼巨人“锻造类配方 +15%”的判定加上名字含 Anvil 的工作台（原来按 Smithy/Forge 判断，搬家后会丢加成）。
- **测试** `Source/WorkbenchChecks.cs`：`CheckWorkbenches` / `PrepareWorkbenchesSave` / `CheckWorkbenchesAfterLoad`，`-Only Workbenches` 自动识别，共 30 条（加上 ResearchTab 5 条 = 35）。
- **改了其它检查组里过时的断言**（工作台名变了，否则它们以后会红）：`GunChecks.cs`（法杖→附魔台、枪→符文铁砧）、`ItemChecks.cs`（种族装备表 FueledSmithy→LP_RuneAnvil）、`Races124Checks.cs`（元素符→附魔台）、`MinotaurGithChecks.cs`（银剑→符文铁砧）、`Bridges/VPE/Source/BridgeChecks.cs`（冒险者手册→卷轴书桌）。这几组按要求没有单独跑，只有 VPE 组跑了。
- **美术** `ArtSource/RaceArt/draw_workbenches_147.py` → `Textures/LP/Workbenches/`，34 个文件：6 张非正方形（5 张台 + 淬火槽）各南/北/东三向，3 个 1×1 附件单图；用材料建造的 5 件（附魔台、两张铁砧、淬火槽、神像）是 `CutoutComplex`，带遮罩（红=材料色），Graphic_Multi 的遮罩按 1.7 命名 `_southm/_northm/_eastm`，单图 `_m`（看过 `Graphic_Multi.Init` 的 IL）。总预览 `ArtSource/Preview/Workbenches-1.47.0.png`。
- **文本**：物品描述里写“在锻造台/炼金桌/裁缝台制作”的都改成新台名（26 处，含 HAR 桥）；`使用说明.txt` 新增【1.47.0 DND工作台】工作台一览，更新日志加 1.47.0；`About.xml` 版本 1.47.0，description 末尾加 1.47.0 概述。

## 配方归属表（1.46.0 → 1.47.0）

开工时先在游戏里把 1.46.0 所有本模组配方的 `AllRecipeUsers` 导出来核对（含 1.45.1 的继承链：盔甲走 `LP_DNDArmorBase`←`ArmorSmithableBase`→原版锻造台，法袍走 `LP_DNDRobeBase`/`LP_DNDClothBase`→原版裁缝台），再按规格搬。

| 新工作台 | 配方 | 原来在 |
|---|---|---|
| **符文铁砧** | 8 把符文火器 `Make_LP_RunicPistol/RunicMusket/RunicRepeater/HammerCannon/MountainWrath/SilverPistol/LavaPiercer/ThunderGun`（`LP_SmithGunBase`） | 原版锻造台 |
| 符文铁砧 | `Make_LP_DwarvenRuneShield`、`DwarvenThrowingHammer`、`ElvenChain`、`MinotaurHornHelm`、`GithAstralArmor`、`OrcishGreataxe`、`SilverSword`、`FrostSpear` | 原版锻造台 |
| 符文铁砧 | 魔化兵器 `Make_LP_FrostBrand`、`DragonSlayer`、`DefenderSword`（`LP_MagicBladeBase`）、`Make_LP_JavelinLightning` | 原版锻造台 |
| 符文铁砧 | 1.45.1 骑士锻造/传奇战甲 10 件：`PaladinPlate`、`GreatHelmOath`、`ChainShirt`、`BattleClericBreastplate`、`DwarvenRuneplate`、`BeardedHelm`、`DragonknightArmor`、`DragonHeadHelm`、`LoxodonWardenArmor`、`CelestialPanoply`（`LP_DNDArmorBase`） | 原版锻造台 |
| 符文铁砧 + 熔火锻炉 | `Make_LP_DragonShedArmor`、`Make_LP_EmpyreanBlade` | 锻造台 + 熔火锻炉 |
| 符文铁砧 + 手工制作点 | `Make_LP_ElvenLongbow` | 锻造台 + 制作点 |
| **巨人铁砧** | `LP_MakeFrostGiantAxe`、`Make_LP_FrostGiantAxe`、`Make_LP_StormTrident`、`Make_LP_AbyssCoralArmor`、`Make_LP_CloudMorningstar`；HAR：`LP_MakeCyclopsMaulSteel/Wood`、`Make_LP_CyclopsMaul`、`Make_LP_EttinTwinHelm` | 原版锻造台 |
| 巨人铁砧 + 手工制作点 | `Make_LP_OgreClub` | 锻造台 + 制作点 |
| **附魔台** | `MagicItems_Recipes.xml` 里除圣物、书桌之外的 22 个（附魔学 5、奇物工艺 13、神奇器物 4）；`Make_LP_FireStaff/FrostStaff/MissileStaff`；`LP_MakeElementalFocus_Fire/Water/Earth/Air`；珠宝联动 `LP_EnchantJewelry_*` 4 个（继承 `LP_MagicRecipeBase`，自动跟着走） | 炼金桌 |
| **圣坛** | `LP_MakePrayerBeads`、`LP_MakeScarabProtection`、`LP_MakeMartyrsBeads`、`LP_ReconsecrateMartyrsBeads`、`LP_MakePuritySeal`、`LP_MakeHaloOfLight`、`LP_MakeWardingBondCase`（新子基类 `LP_RelicRecipeBase`） | 炼金桌 |
| **卷轴书桌** | `LP_MakeScrollRemoveCurse`、`LP_MakeManualOfGolems`（新子基类 `LP_DeskMagicRecipeBase`）、VPE `LP_MakeAdventurersManual`、新增 `LP_WriteScroll*` 6 个 | 炼金桌 / 新 |
| **法袍织机**（只在织机） | `Make_LP_ArchmageRobe`、`WizardHat`、`MorninglordVestments`、`LichShroud`、`CourtAttire`、`LP_MakeElementalWeave` | 原版裁缝台 |
| 法袍织机 + 原版裁缝台 | `Make_LP_WinterWolfCloak`、`CloudRobe`、`IllithidTendrilCloak`、`FeatherCloak`、`StabilizerBand` | 原版裁缝台 |
| **炼金桌**（留下 21 个） | 药水 9（含强化 2）、投掷物 6（炼金之火、强酸瓶、圣水、黏胶袋 + 凝胶版 2）、雾瓶、风暴宝珠、寒铁、凝胶敷料；`Make_LP_PsionicSuppressant(Bulk)` 新加炼金桌（药物实验室照留） | — |
| 不动 | 死灵仪式台全部（含巫妖抄的 `LP_ScribeRemoveCurse`、死亡骑士装）、熔火锻炉全部（火焰舌、熔核装备、`LP_EmberCollarPlate/RivetHelm`）、圣坛原有 5 个、制作点（号角、独眼透镜、食人魔大棒）、普通 1.45.1 衣服（冒险者旅行装、游侠皮甲等 10 件）、`Make_LP_FireGiantTunic`（裁缝台）、灶台婴儿食品 | — |

原版锻造台、电锻造台上现在没有本模组的配方（测试确认）；炼金桌只剩上表 21 个。

## 决定（规格没写或和现状不一致的地方）

1. **规格没列的锻造台配方**（矮人投掷锤、半兽人巨斧、吉斯洋基银剑、霜刺长枪、霜寒之刃/龙杀手/守护者之剑）：都去**符文铁砧**，因为规格要求原版锻造台上不留 LP 配方，它们都是各族/魔化兵刃，归符文铁砧最顺。
2. **精灵长弓**原来是“锻造台 + 制作点”：锻造台换成符文铁砧，制作点保留（和食人魔大棒同理）。
3. **`Make_LP_FrostGiantAxe`、`Make_LP_CyclopsMaul`**：这两个是从原版近战基类继承出来的隐式配方，1.46.0 里**不需要任何研究**（与专门的 `LP_MakeFrostGiantAxe`、`LP_MakeCyclopsMaul*` 重复）。defName 不改所以没删，改到巨人铁砧并挂上“巨人锻造”。
4. **元素织袍**在规格第一节算“种族披风”、第五节算“法袍织造”：按第五节，**只在织机**。
5. **巫妖裹尸长袍**：1.45.1 实际放在原版裁缝台（研究法袍织造），不在死灵仪式台，所以按规则搬到织机。
6. **工具柜不打补丁**：原版 `CompProperties_Facility.ResolveReferences` 会自动把所有在 `linkableFacilities` 里写了 ToolCabinet 的工作台加进 `linkableBuildings`（看过 IL），所以只在新台上写 ToolCabinet，不需要 `Patches/ToolCabinet.xml`。测试确认工具柜能连 5 张新台。
7. **材料着色**：附魔台、两张铁砧、淬火槽、守护神像用材料建造，用 `CutoutComplex` + 遮罩，只有台面/石体按材料变色，符文、火光、铁件保持原色。
8. **附件的造价**规格没写：蒸馏器 钢30 + 零部件1；奥术聚能石 玉5 + 秘银5 + 钢20；淬火槽 石砖60 + 钢20；守护神像 石砖80。
9. **卷轴书桌**用 `ResearchSpeed` 做速度属性，房间角色用实验室；新卷轴不需要巫妖、谁都能写。
10. **符文铁砧/巨人铁砧**去掉了 `constructEffect`：材料建造的建筑写了它会报配置错误（建造动画本来就按材料）。
11. **旧存档的信**只在真的清掉了工作单时才发（新游戏不发）；信里按新工作台分组列出这存档里被清的单子。清理只看原版锻造台、电锻造台、炼金桌、两种裁缝台上、配方属于本模组、而这张台已经不能做的单子；原版配方和其它工作台不碰。
12. 附魔台、卷轴书桌的产物也给密斯特拉加神眷（原来只有炼金桌，魔法物品搬走后会丢掉这条）。
13. **卷轴材料价值低于规格目标**：规格表的材料（例如传送卷轴 布10 + 秘银3）只占成品市价的 12–20%，达不到规格写的 40–55%。两者冲突时我**按规格表的具体材料**做了，没擅自加料；工作量和智识门槛（6–14）也是成本。要拉到 40–55% 需要大约把各卷轴的主材料加 2–3 倍，可以下一版调。测得的比例：识破 12%、防护邪恶 12%、火球 20%、传送 17%、石化 20%、复生 14%。

## 测试结果

| 命令 | 退出码 | 结果 |
|---|---|---|
| `.\Build.ps1` | 0 | 主程序集、HAR、VPE、神兽血裔联动都编译通过 |
| `.\Test.ps1 -Only Workbenches,ResearchTab` | 0 | `PASS: 35 checks (only Workbenches,ResearchTab); save/reload.` 日志里没有红字（只有 EOS/Vulkan 加载器和其它模组 About 的环境警告，和本模组无关）。改完其它组的断言、About、使用说明之后用最终 DLL 又跑了一次，同样 35 条通过。 |
| `.\Test.ps1 -VPE -Only Artificer`（第 1 次） | 1 | `FAIL after 1 checks`：`[vpe mentor] paying 500 silver keeps the mentor 3 more days (had 100, now 100 …)`。VPE 档会忽略 `-Only`、跑完整个 VPE 桥接组；这是第一条导师检查，和工作台无关。 |
| `.\Test.ps1 -VPE -Only Artificer`（第 2 次） | 0 | `PASS: 441 checks (classes only)`。器械师相关都通过，包括 “manual recipe sits on the scribe desk”、“arcane workshop unlocks the staves, runic pistol and runic musket only”、“a crafting-20 pawn without artificer passives cannot craft silver sword or storm trident”。导师那条是偶发失败。 |

Workbenches 组覆盖：9 个定义与三向贴图/图标、尺寸、研究门槛、工作类型、符文铁砧燃料；3 项研究坐标/费用/前置；完整配方归属表、原版锻造台无 LP 配方、炼金桌恰好 21 个、裁缝台；工具柜和 4 个附件的连接与数值；实地放炼金桌 + 蒸馏器（0.56 → 0.644）、圣坛 + 神像（祈祷 ×1.2）；巨人铁砧体型 2 的火巨人比人类快 25%（0.70 对 0.56，通过真实的工作台属性测）；织机种族披风 ×1.15；熔火锻炉仍拒绝非火巨人；6 张卷轴的产物、研究、智识、工作量；书桌鉴定 3333 tick；孤儿判定；**存读档**：原版锻造台上的符文手铳单子被清、原版单子保留、只来一封信且列出符文铁砧/符文手铳、异族军械→符文锻造和附魔学→卷轴抄写补完、高阶卷轴不补、再调一次不重复发信。

## 没做 / 没验证

- 没有进游戏看贴图的实际效果（尤其是 CutoutComplex 遮罩、东西向、4×3 巨人铁砧的交互格位置），只看了预览图。美术是程序画的，比较朴素。
- 其它检查组（Guns、Items、Races124、MinotaurGith、MagicItems 等）里改了过时断言但**没有跑**（按要求只跑目标组）；全套也没跑。
- 卷轴书桌鉴定只测了时长和工作台判定，没有让小人真的走过去鉴定一次。
- 符文铁砧的燃料消耗、工作台在室外/无房间时的系数没有单独实测。
- 珠宝（Jewelry）联动环境没有装，附魔首饰 4 个配方是靠继承 `LP_MagicRecipeBase` 跟着搬到附魔台的，没在带珠宝的档里跑。
- 顺带看到：1.45.1 衣甲遮罩文件名是 `_south_m.png`，而 1.7 `Graphic_Multi` 读的是 `_southm`；没有核实衣甲渲染时是否另有路径，不在本次范围，没动。
