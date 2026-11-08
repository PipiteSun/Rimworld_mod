# UI 整理 1.124.0 报告

日期：2026-11-07。只改排版与可读性，不改任何数值、公式和玩法。原文件备份：`ArtSource/backup-1.123.0-ui/`。

## 1. 盘点（Source 下的自定义窗口 / 分页 / Gizmo）

| 界面 | 文件 | 发现的问题 | 处理 |
|---|---|---|---|
| 成神之路 Dialog_Ascension | Ascension.cs | Listing 无滚动，三条路线说明 + 按钮在 620×520 里会溢出；尺寸不夹屏幕 | 标题固定，下面整块滚动；Fit |
| 篡火仪式 Dialog_UsurperShards | Ascension.cs | 尺寸不夹屏幕 | Fit |
| 圣徒的奉献 Dialog_SaintOffering | AscensionPaths.cs | 无标题；顶部说明固定 60 高（神名长时被截）；物品名过长换行重叠；空列表无提示 | 标题；说明按实际高度；名字截断 + 悬停；空提示；复位 |
| 掌控天气 Dialog_ControlWeather | ClassCasters.cs | 窗口固定 330 高，天气多于 6 种时按钮画到窗口外 | 高度随天气数（≥330，夹屏幕）；按钮区滚动；每帧不再重算天气列表 |
| 选择宿敌 Dialog_ChooseFavored | ClassMartial2.cs | 9 个按钮刚好塞满，小屏幕会压到关闭按钮 | 统一标题；选项区滚动；复位 |
| 神选仪式 Dialog_ChosenRitual | Faith.cs | 无标题 | 加标题，窗口 300→340 高 |
| 耐瑟升神仪式 Dialog_NetherilAscension | NetherilAscension.cs | 主祭多时把按钮挤出窗口；窗口里再套 220 高小滚动区 | 标题固定，主祭 / 协祭 / 按钮整块滚动；空协祭提示 |
| 探索公告板 Window_QuestBoard | QuestBoard.cs | 顶部说明固定 44 高；委托标题、数据行、已接委托标题 / 状态不截断（长名字溢出到按钮下面） | 说明按实际高度；LabelFit；try/finally 复位 |
| 系统宿主 SystemHostWindow | SystemHostWindow.cs | 绝对坐标排版，小屏幕记录区被压没；统计行、任务名、记录行不截断 | Fit（测试分辨率下仍 840×710）；内容区 <560 时整块滚动；LabelFit；复位 |
| 改命 Dialog_FateRewrite | SystemHostFate.cs | 三栏内容高 = 外框高，总是出现空滚动条；名字不截断 | 内容高修正；LabelFit；小屏幕整块滚动；复位 |
| 附魔强化 Window_GearEnhance | GearEnhanceWindow.cs | 中栏效果说明很长时画出框外；列表第二行、材料行不截断；中途出错会留下 MiddleCenter / 灰色 | 中栏滚动（换装备回到顶部）；LabelFit；LPUI.Unavailable；小屏幕整块滚动；复位 |
| 血肉催转 Dialog_FleshCatalysis | TwistedMeatCatalysis.cs | 价格行（多日价格串）、说明行可能溢出；状态未兜底复位 | LabelFit；Unavailable；小屏幕整块滚动；复位 |
| 血肉再生 Window_FleshRegen | TwistedMeatItems.cs | 同上 | 统一标题；LabelFit；Unavailable；复位 |
| 死灵塑形 Dialog_ConstructDesign | ConstructDesign.cs | 940×660 不夹屏幕，按 rect.height 相对排版在矮屏幕上会挤坏 | Fit；内容区 <460 时整块滚动；复位（含 GUI.enabled） |
| 冒险者分页 ITab_Adventurer / 属性页 | AdventurerUI.cs / AttributePage.cs | 已有滚动、高度按屏幕、finally 复位（1.52.0 热修） | 不动 |
| 模组设置 | Core.cs | 已是两列高 Listing + 跳转（1.52.0 热修） | 不动 |
| 角色编辑器 DND 面板 | CharEditorCompat.cs | 已按屏幕夹高度 | 不动 |
| 神力条 Gizmo_DivinePower、自动施放 Command_ClassAbility | Demigod.cs / ClassAutoCast.cs | 状态已复位，尺寸固定合理 | 不动 |
| 选择职业 / 子职业 / 兽伴、传颂之歌 | Classes.cs / ClassSubclass.cs / ClassBard.cs | — | **未改**：这些文件有其他线程未提交的改动（吟游诗人 1.123.0），按规则不碰；下次可用同样的 LPUI 方式整理 |
| 各种 Gallery 窗口 | *Checks.cs / *Gallery.cs | 只用于测试截图 | 不动 |

## 2. 共享工具 Source/LPUI.cs

- 常量：`Gap 6`、`SectionGap 10`、`RowH 24`、`ListRowH 28`、`ButtonH 32`、`MainButtonH 36`、`TitleH 34`、`CloseXRoom 30`、`ScrollBarW 16`、`ScreenMargin 40`、`MinWindow 240`。
- `Fit(w,h)` / `FitTo(w,h,screenW,screenH)`：窗口初始尺寸夹在 `UI.screenWidth/Height − 40` 内（最小 240）。
- `Reset()`：字号 Small、左上对齐、自动换行、白色。各窗口 `DoWindowContents` 用 `try{…}finally{LPUI.Reset();}`。
- `Title(rect,text)`：中号字标题，右边让出关闭按钮，返回下一行 y。
- `LabelFit(rect,text,tip)`：放得下照常画；放不下截断成一行并悬停看全文（含富文本的不截，只加悬停）。
- `Para`、`ScrollText`、`BeginScroll/EndScroll`（高 Listing 防自动分列）、`Button`（截断 + 悬停，可无背景）、`Unavailable`（灰色居中说明）、`Row`（均分按钮位）、`BeginMinHeight`（按绝对坐标排版的窗口被压矮时整块滚动）。

## 3. 测试

- 编译：`pwsh Source\Build.ps1` 通过（主程序集 + HAR / VPE / Mythic 桥）。
- 新检查组 `-Only LPUI`（`Source/LPUIChecks.cs`，Checks.cs 加了两行钩子）：8 项通过——Fit 在大屏不变、1024×600 / 640×480 下缩小、有下限；Row 均分；9 个窗口初始尺寸都在测试屏幕内；系统宿主仍 840×710；天气窗口不比原来矮。自测后依次打开 8 个窗口截图（`StandaloneMods/Validation/Lich-Core-NoHAR-Only/UI-0…7.png`），画窗口时日志无异常；截图人工看过，布局正确。
- 回归：`-NoHAR -Only 'LPUI,GearEnhance,QuestBoard,SystemHost,SecondLife,Ascension,Faith,ConstructDesign'` 跑到 239 项时在 `faith off`（设置关闭 / 特性权重）失败；`-Only Faith` 单独跑 18 项通过——是组合顺序造成的，与界面无关。`-Only Netheril` 单独跑在 `NetherilIdleChecks`（空闲阅读 JoyTickCheckEnd 补丁空引用 / netheril 30）失败，组合跑时也失败在同一处；本次没有碰耐瑟空闲阅读代码（只改了升神仪式窗口），属于既有问题（记忆里 Netheril 22 / 32 也一直不稳），需要另查。
- 日志里的贴图红字（PowerArmor、LP_BeastsDND2 等）来自其他线程进行中的内容，不是本次改动。
- 跑测试时与其他线程共用 HAR 创意工坊链接（`Mods/_LichTest_2009463077`）会撞车，所以用 `-NoHAR` 档并等对方测试结束再跑。

## 4. 版本

1.124.0（About.xml 描述与使用说明更新日志已按顺序插入；modVersion 已被近战武器线程改成 1.125.0，保持不降）。没有 git push。
