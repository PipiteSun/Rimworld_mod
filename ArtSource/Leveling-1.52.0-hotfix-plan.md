# 1.52.0 热修计划：冒险者分页排版（未实施）

状态：**只写计划，没有改代码**。Peter 正在试玩，要求暂不修改模组。备份已做好：`ArtSource/backup-1.52.0`（About、Bridges、Source、使用说明.txt、LoadFolders.xml，494 个文件）。
曾经临时改过 `Source/AdventurerUI.cs` 和 `Source/AdventurerChecks.cs`，已从备份原样恢复（哈希逐一核对，Source/About/Bridges/使用说明.txt 与备份 0 处差异）；DLL 没有被替换（游戏占用，编译输出的 .new 已删除）。

## 诊断

`Source/AdventurerUI.cs` 第 25 行：

```csharp
public ITab_Adventurer(){size=new Vector2(430f,716f);labelKey="LP_AdventurerTab";}
```

- 分页固定 430×716。原版 ITab 的矩形是从检视面板顶部往上量 `size.y`，716 像素在 1080p + 界面缩放 >1、或者 RimHUD 把检视面板加高的情况下，会超出屏幕顶端。
- `FillTab` 从矩形顶部（y=12）开始顺序绘制：标题（等级、熟练加值）→ 经验条 → 六维 → 共存提示 → 种族 → VPE …… 所以**被裁掉的正好是最上面的标题和经验条**，截图里第一眼看到的是六维格。
- 内容总高度通常只有 300 像素左右，所以 716 高的面板下面大半是空的。
- 没有滚动区：专长、传奇属性点多的时候反而可能超出底部。
- 空白项：没有专长、还不是传奇时，这两段完全不画，看起来像缺了内容。

和 Isekai 共存模式本身无关；共存模式只是把“部位生命”那一行换成了灰色提示。

## 修复方案（等 Peter 同意后再做）

1. **尺寸**：宽 630，高 `Clamp(UI.screenHeight − 300, 220, 480)`，在 `UpdateSize()` 覆写里每帧设置（原版 ITab_Pawn_Gear 也是这样做的），不再依赖固定 716。
   - 加一个 `public static float HeightFor(float screenHeight)`，方便检查。
2. **滚动区**：`FillTab` 只负责 `ContractedBy(10)` 的外框 + `Widgets.BeginScrollView`；原来的绘制代码原样搬到 `float DrawContents(Rect,Pawn,Hediff_Adventurer)`，坐标从 (0,0) 开始，返回实际高度，下一帧用它作为滚动内容高度。`finally` 里复位 Font/Anchor/GUI.color 并 `EndScrollView`。
   - 这样标题、经验条、熟练加值、加点/恩赐/传奇按钮永远在可视区顶端，不依赖屏幕位置。
3. **不留空白**：
   - 不是传奇且传奇等级开启时，显示灰色“传奇属性点：成为传奇冒险者后解锁”。
   - 没有专长时显示灰色“专长：暂无（属性提升时可以改选专长）”。
   - 共存模式继续显示六维、灰色提示、种族/天赋、VPE 路径点、里程碑、传奇、专长。
4. **RimHUD**：RimHUD 保留原版 ITab 机制，只是检视面板更高；高度按屏幕封顶（留 300 像素给检视面板和底栏），足够放下。
5. **检查**：`AdventurerChecks.cs` 在“冒险者 inspect tab is attached to humans”后加一条：`HeightFor(1080)==480`、`HeightFor(700)==400`、`HeightFor(400)==220`、宽 ≤ 640。
6. **流程**：退出游戏 → `pwsh Source\Build.ps1` → `Test.ps1 -Only Adventurer` → `使用说明.txt` 更新日志加“1.52.0 热修：冒险者分页排版”。About.xml 版本保持 1.52.0（1.52.1 留给传奇怪物）。

已验证：上面这套改动在本机编译通过（Roslyn 无错误），只是因为游戏占着 DLL 没能替换，随后按要求全部撤回。

## 附：开发者模式里的“六臂蛇人……坠落仓加入”

本模组**没有**定义这样的事件：Defs、Bridges、Languages、Patches、Source 里都没有“坠落仓”；创意工坊所有 XML、所有模组 DLL、原版简体中文翻译里也都没有这个词（原版叫“逃生舱坠毁”）。

六臂蛇人能“掉下来加入”的来源是 HAR 的种族设置 `Bridges/HAR/Defs/Marilith_Race.xml` 里的 `LP_MarilithRaceSettings`（1.33.0 加入）：

- `alienrefugeekinds` → `LP_MarilithColonist`：原版 `RefugeePodCrash`（逃生舱坠毁：一个人坐逃生舱掉到地图上，倒地受伤，救起来可以招募）生成难民时，HAR 会按权重换成六臂蛇人殖民者。
- `alienwandererkinds` → `LP_MarilithColonist`：原版 `WandererJoin`（流浪者加入）同理。
- `startingColonists` → 开局人物也可能出现。

所以开发者菜单里那一项大概率是原版 `RefugeePodCrash`（或 HAR/其他模组翻译的某个调试项）挑中了 `LP_MarilithColonist`，不是本模组单独的事件。若要确认确切文字，需要 Peter 提供截图里的完整标签。
