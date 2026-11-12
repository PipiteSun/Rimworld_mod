# 银行 1.142.0 实现报告

> **改版（同日，Peter 给了一张玻璃储物柜参考图：「做成像是这样子的储存，然后里面的展示依然维持金子和银原本的图像」）**：
> - 外观改成两座并排的玻璃金柜（浅钢色柜框、玻璃窗、两根立柱、下方通风抽屉面板和黄铜锁牌），不可旋转。
> - 不再画自制的币堆 / 金锭叠层（16 张已删）。左柜管左边两格、右柜管右边两格，柜窗里直接摆原版银 / 金物品图（`Graphic_StackCount` 按堆量取 a/b/c 那张），每 7 堆多一个，最多 6 个（`IconCount`）；金银混放按比例分配、交替摆放，大小 0.56 格。
> - 分三层印：柜体（Building 层）→ 物品图（BuildingOnTop，逐个抬高一点）→ `BankVault_Front`（柜体同图、柜窗挖空只留反光和立柱，Cutout）。
> - 运行时怪癖（实测）：这座 2×2 用 `Graphic_Single` 时本体印出来上下颠倒（换 Graphic_Multi 三个朝向同一张就正了；前层用 Printer_Plane 默认 UV 是正的）；Transparent 材质整体发虚。所以本体用 Graphic_Multi，前层用 Cutout，物品图按 `Graphic.TryGetTextureAtlasReplacementInfo` 取贴图集 UV（和原版 Graphic.Print 一样）。
> - 贴图现在 5 张：`BankVault_south/east/north`、`BankVault_Front`、`BankVault_Icon`。测试第 1、5、6 条和读档条改成检查柜窗物品图（`ColumnIcons`）；`-Only 'Bank,ResearchTab'` 仍 17 条通过；游戏内截图 `ArtSource/Preview/Bank-1.142.0-ingame.png`。
> **第三版（Peter 自己优化的储存柜参考图，要求改成 RimWorld 画风）**：一座整宽的保险金柜：上半一扇宽玻璃窗（后壁 / 侧壁 / 底板有纵深，左上角两道反光），下半双开保险门（四个铰链、铆钉、中间深色方板密码转盘 + 青色锁舌、右门通风格栅），深色底座。窗里左半摆左边两格、右半摆右边两格的原版银 / 金物品图（窗中心往上 0.45 格，左右各 ±0.4 格，物品图 0.44 格）。`-Only 'Bank,ResearchTab'` 17 条通过，游戏内截图已更新。
> 下文是第一版的记录，叠层相关的描述已被上面取代。

2026-11-10。设计稿：「银行（金银储存）设计稿」https://claude.ai/artifact/KM1kG8JEW9qKHFMSCSfaB7 。基线 1.141.0 → **1.142.0**。备份 ArtSource/backup-1.141.0（About.xml、使用说明.txt、Research.xml、旧 LichPhylactery.dll）。只做第一批（储存与展示）；第二批（利息 / 银票 / 金库房间）没做。

## 新文件
- `Defs/Bank.xml`：`LP_BankVault`「银行」，thingClass `LichPhylactery.Building_LPBank`，2×2 可旋转，钢铁 200 / 塑钢 50 / 金 20 / 零部件 4，工作量 8000、建造 8 级，耐久 1500、可燃性 0、美观 6，家具分类；`maxItemsInCell` 20、`fixedStorageSettings` 与 `defaultStorageSettings` 只列 Silver / Gold、默认优先级 Preferred、`ignoreStoredThingsBeauty`、`Blueprint_Storage`；检视页 ITab_Storage + `ITab_BankLedger`。
- `Source/BankVault.cs`：
  - `Building_LPBank : Building_Storage`：`Tier(堆数)`（0 / 1–5 / 6–12 / 13–19 / 20 → 空 / 少 / 半 / 快满 / 满）；`Refresh` 在 Notify_ReceivedThing / Notify_LostThing / TickRare / PostMapInit 时按每格银、金各自的堆数重算 8 个档位（整组替换），变了才 `MapMeshDirty(Things)`；`Print` 用 `Printer_Plane` 在每格中心印 0.8 格的叠层（混放时左银右金各 0.4×0.8）；`Summary()`（银 / 金 / 库存 x/80 堆 / 总价值）用在 `LabelMouseover` 和检视字符串；`GetGizmos` 去掉原版给每一堆钱的「选择」按钮；`DeSpawn` 后每格只留一堆，其余 `GenPlace` 就近落地；`InBank(t)` 用建筑网格判断（任何线程可读）。
  - `BankArt`（启动时加载 16 张叠层材质，并把 `StatPart_BankBeauty` 挂到 Beauty：每 500 金 +1，最多 +6）。
  - Harmony：`SectionLayer_ThingsGeneral.TakePrintFrom` 前缀（不印银行格里的银 / 金）、`Thing/ThingWithComps.DrawGUIOverlay` 前缀（不画数量标签）、`Thing.TakeDamage` 前缀（免伤）、`ThingSelectionUtility.SelectableByMapClick` 后缀（点选 / 框选只选中银行）、`MouseoverReadout.MouseoverReadoutOnGUI` 转译（把唯一一处 `GetThingList` 换成 `BankReadout.Things`，银行格里不逐堆列出钱）。
  - `ITab_BankLedger`「金库清单」：银 / 金各自总数、堆数、价值，库存与总价值；滚动列出每一堆，「可用」勾选＝解禁 / 禁止，「取出」放到银行外一圈第一块没有物品的空地并设为禁止。
- `Source/BankChecks.cs`：`-Only Bank`（10 条 + 读档 1 条）；`-Only BankShot`（截图，两张：Bank-Map.png、Bank-Ledger.png，存到 Lich-Core-Only 存档目录）。
- `Languages/ChineseSimplified|English/Keyed/LP_Bank.xml`：检视页标签 `LP_BankLedgerTab`。
- `ArtSource/RaceArt/draw_bank_1142.py`（复用 draw_workbenches_147 的 Sheet）→ `Textures/LP/Bank/`：BankVault_south/east/north（256²）、BankVault_Icon（128²）、Silver_/Gold_ Few/Half/Most/Full（128²）、SilverHalfCell_/GoldHalfCell_ ×4（64×128），共 20 张。预览 `ArtSource/Preview/Bank-1.142.0.png`，游戏内截图拼图 `ArtSource/Preview/Bank-1.142.0-ingame.png`。

## 改动的旧文件
- `Defs/Research.xml`：加 `LP_Banking`「银行业」1200，前置 ComplexFurniture，DND 页 (2, 4.9)（制伏战术右边）。
- `Source/Checks.cs`：加 `CaptureBank()` 钩子和 `BankShot` 截图开关（两行）。
- `About/About.xml`：modVersion 1.142.0，描述末尾加银行一句。
- `使用说明.txt`：第 91 行玩家说明「银行（1.142.0）」；更新日志加 1.142.0 一行。
- 4 个 dll 重新编译。

## 和设计稿的偏差
1. **通行性用 PassThroughOnly（同原版货架），不是完全不可通行**：完全不可通行的格子没有区域（region），原版按区域找物品（配方取材、建造取料）会找不到银行里的钱。小人仍会绕开（寻路代价 70），只是不会被完全挡死。
2. **叠层贴图 128×128（半格 64×128）而不是 64×64**：模组建筑统一 128 像素一格，64 在近景会糊。
3. **银行格里的钱不能点选**（设计稿说再点一次可轮换选中单堆）：改为点格子 / 框选只选中银行，单堆在「金库清单」里管理；否则框选一下会选中几十堆钱。
4. **「取出」是直接放到旁边空地并禁止**，不是派搬运工：银行默认「优先」，不禁止的话马上又被搬回去。
5. **中文字串写在代码里**（模组惯例），只有检视页标签走 Keyed。
6. 原版银和金本来就 `useHitPoints=false`、不可燃，所以「防火防爆」在原版下本来就成立；`TakeDamage` 前缀是给改了银 / 金耐久的其他模组兜底。

## 测试
- `Test.ps1 -Only 'Bank,ResearchTab'`：PASS 17 条（Bank 10 + 读档 1，ResearchTab 6）。
- `Test.ps1 -Only BankShot`：截图两张，看过：币堆按档显示、格里的钱不再画出来、混放格左银右金、朝东的银行叠层跟着格子、原版每堆一个的「选择」按钮已去掉、「金库清单」正常。

## 没在游戏里验证的
- 鼠标悬停左下角读数（截图里鼠标不在银行上）；转译和过滤函数本身由检查 9 验证。
- 和 Adaptive Storage Framework / LWM Deep Storage 同时加载的情况。
- 来访商队实际交易界面里的银数（检查 7 验证了原版交易用的同一组条件：在储存里、PlayerSellableNow、Touch 可达）。

设备空闲（测试进程已退出）。没有动 git。
