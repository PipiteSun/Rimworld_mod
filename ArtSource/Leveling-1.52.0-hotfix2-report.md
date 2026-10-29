# 1.52.0 热修2：专长与史诗恩赐在冒险者分页内选择

About.xml 仍是 1.52.0。备份：`ArtSource/backup-1.52.0-tab2`（AdventurerUI.cs、AdventurerChecks6.cs、Adventurer.cs、AdventurerFeats.cs、AdventurerEpic.cs、LichPhylactery.dll、使用说明.txt）。

## 改了什么

只改了 `Source/AdventurerUI.cs`、`Source/AdventurerChecks6.cs`、`使用说明.txt`。没碰耐瑟研读和六臂蛇人副手的文件。

### 分页（ITab_Adventurer）
- **可选专长**：殖民者有完整的属性提升（pendingASI > 0）时，已有专长下面出现金色标题“可选专长（消耗一次属性提升，剩 N 次）”，12 个 LP_FeatDef 按 order 一行一个：
  - 36px 图标、名字（每日次数）、一行效果（描述去掉第一句点题的话，过长截断）、“前置：…”；
  - 前置不满足 / 已经有的：文字变灰，前置行用红色写原因（`CanTakeFeat` 返回的原因，例如“力量 ≥ 13”“已经有了”），按钮灰显（已有的显示“已有”）；
  - 悬停看完整描述、前置和不能选的原因。
- **史诗恩赐**：有恩赐点时，下面再列“史诗恩赐（剩 N 个恩赐点，每个恩赐只能选一次）”，6 个恩赐一行一个；威能之恩下面是六个小按钮“力量 +4 … 魅力 +4”，悬停看该项加后的数值。
- **确认方式**：选了内联的两次点击——第一次点“选择”，按钮变成“确定？”（悬停写明代价），同一个按钮再点一次才生效；点另一个专长 / 恩赐 / 威能的另一项会改为等待那个；换检视的人时清掉。没有用 Dialog_MessageBox，避免再弹窗。
- 只剩拆开的属性点（asiLeft > 0、pendingASI = 0）时显示灰色一行“拆开的属性点不能换专长：需要一次完整的属性提升”。
- **加点栏**：去掉了原来打开窗口的“改选专长”“史诗恩赐”按钮，换成“选专长（N）”“选恩赐（N）”快捷按钮（只在有对应点数时出现），点了把滚动区滚到对应列表。
- 分页尺寸规则不变（630 宽，高 = 屏幕高 − 300，限制 220～480），新列表都在同一个滚动区里。

### 删掉的窗口
`Dialog_AdventurerASI`（属性提升 / 改选专长 / 史诗恩赐三个标签）和 `Dialog_LegendPoints`（传奇投点）已经没有任何调用（敌人自动选专长、恩赐走 `AutoAssign` / `AutoBoon`，不经过界面），整段删除。`AffinityColor` 挪进 `ITab_Adventurer`。

### 升级信件
`Letter_AdventurerLevel` 原来的“给某某加点 / 选史诗恩赐”和“投传奇属性点”两类按钮合成一个：每个还有点没用的玩家殖民者一个“打开某某的冒险者分页（属性点 2、史诗恩赐 1、传奇属性点 3）”（`Letter_AdventurerLevel.Pending`）。点了调用 `ITab_Adventurer.OpenFor(p)`：`CameraJumper.TryJumpAndSelect` 选中并跳过去，选中的是这个人时 `InspectPaneUtility.OpenTab(typeof(ITab_Adventurer))`（在商队里时只会选中商队）。

### 新的静态接口（检查也用）
- `ITab_Adventurer.Confirm(ref armed, key)`：第一次记下 key 返回 false，同一个 key 再来一次返回 true 并清空。
- `ITab_Adventurer.PickFeat(h, f, ref armed)` / `PickBoon(h, b, ability, ref armed)`：不能选时清空 armed、返回 false；能选时经过 Confirm，确认后调用 `TakeFeat` / `TakeBoon`。
- `FeatKey` / `BoonKey`（威能之恩每一项属性是不同的 key）、`FeatEffect`、`OpenFor`。

## 自检
`pwsh Source\Build.ps1` 通过（主程序集 + HAR / VPE / 血脉三个桥）。

`.\Test.ps1 -Only Adventurer`：**122 条全过**（原 120 + 2，含存读档）。新增在 `AdventurerChecks6.cs` 的 `CheckAdventurerTabPicks`：
1. 分页选专长：力量 10 时重甲大师被拒且点两次也不生效；警觉第一次点只待确认（属性提升仍是 1），第二次点拿到专长、属性提升变 0；之后幸运点两次也拿不到；`FeatEffect(警觉)` 以“被偷袭”开头。
2. 分页选恩赐：威能之恩先点力量、再点敏捷只是换成等待敏捷，再点一次敏捷才生效（敏捷 10 → 14，力量不变）；迅捷之恩点两次拿到；恩赐点用完后命运之恩点两次也拿不到。

没有在游戏里实际打开分页看画面（自检进程没有界面截图），排版靠代码：专长行 56px、恩赐行 46px（威能之恩 70px）。

## 给 Peter
需要重启游戏载入新 DLL。旧存档无需处理：待选的属性提升、恩赐点都还在，打开冒险者分页往下滚就能看到列表。
