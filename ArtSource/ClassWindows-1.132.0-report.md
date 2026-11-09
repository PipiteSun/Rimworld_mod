# 剩下 4 个窗口改版 1.132.0 报告

依据：ArtSource/NewContent3-1108-spec.md 第 A 项。开工时 About.xml 是 1.131.0。只改界面，不改任何数值和逻辑（所有按钮仍调用原来的 ClassLevelInfo.AddLevel / ClassLegendary.Learn / ClassSubclass.Pick / Choose / Bond / Bard.Ballad）。

## 结果
- 编译：`Source\Build.ps1` 通过（主程序集 + HAR / VPE / 神兽血裔桥）。
- 测试：`Test.ps1 -Only ClassWindows` **6 条全过**，并且自测后真的打开 4 个窗口画了几十帧：职业窗口把 13 个职业逐个选一遍；职业窗口和子职业窗口右栏拉到底确认能滚动；日志 `[LICH-UI] class windows: drew 4 windows (13 classes), right panels scroll`，无红字。截图 `StandaloneMods/Validation/Lich-Core-Only/CW-0…3-*.png`，我看过职业 / 子职业两张，布局正确。
  - 第一次跑时日志里有两条 `Root level exception in OnGUI(): Prefs.get_UIScale` 空引用，发生在游戏启动、Prefs 还没初始化时（与模组无关），重跑没有再出现。
- 备份：ArtSource/backup-1.131.0（Classes.cs、ClassSubclass.cs、ClassBard.cs、LPUI.cs、Checks.cs、About.xml、使用说明.txt）。
- 没跑完整测试，没有 git 提交 / 推送。

## 改动文件
- 新：`Source/ClassOverview.cs`（一览数据 OverviewRow / ClassOverview.Build / Height / Draw）、`Source/ClassWindowsChecks.cs`（-Only ClassWindows + 画窗口截图）。
- 改：`Source/Classes.cs`（Dialog_ChooseClass 重写）、`Source/ClassSubclass.cs`（Dialog_ChooseSubclass、Dialog_ChooseCompanion 重写）、`Source/ClassBard.cs`（Dialog_BardBallad 重写）、`Source/LPUI.cs`（加 Bar / Icon / RowBg / Tiny 和三种颜色常量）、`Source/Checks.cs`（两行截图钩子，照 1.124.0 LPUI 的写法）、About.xml、使用说明.txt。

## 窗口
- **选职业**（1020×740，按屏幕夹）：左栏职业列表（背景图、名字与等级、资源 / 技能、下一级获得或不能加的原因、加点按钮；点行选中），有传奇职业点时最上面仍是传奇职业能力。右栏：选中职业的资源（悬停看公式）、技能（悬停看原来的完整等级表）、下一级变化（原 NextLines），然后「法术与能力一览」。
- **一览**（按职业等级分组，组标题「Lv N（还差 x 级）」，已到等级金色）：职业能力、子职业（没选时列出各子职业在该等级的能力，悬停对比）、子职业分支、额外攻击 / 奖励专长 / 魔能爆光束 / 宿敌（沿用 ClassLevelInfo.GainsAt）、法术环位（法师来自法术书，其余 8 个施法职业来自 1.130–1.131 的法术表；每环一行「X环法术（N 个，已会 M）」，悬停列出全部法术名，会的打 ✓）、邪术师秘法玄奥 6–9 环、吟游诗人秘法奥秘、职业水晶（11 / 13 / 15 / 17）；然后「传奇职业能力」组和「冒险者 11 / 15 / 20 / 30 级」的秘传槽组。每行：图标（未获得灰显）、名字、类别、状态（已获得 / 待选择 / 需要 Lv N / 需冒险者 N 级 / 需传奇职业点）。
- **选子职业**（920×680）：左栏按「职业 N 级：选一个子职业」「子职业·分支：选一个」分组的选项（徽记 + 名字 + 一行说明），右栏完整说明、每级能力（图标、描述，未到等级灰显并写差几级）、之后还要选的分支；右栏底部固定「选择「X」」按钮，点两次确认，不能选时显示原因。
- **游侠伙伴**（560×620）：现在的伙伴一栏；列表每行头像、名字、种类 / 野性等级 / 体型 / 健康 / 主人，按钮「结为伙伴」。
- **传颂之歌**（580×600）：说明按实际高度、歌者的激昂强度；每行派系图标（派系颜色）、名字、好感与关系、首领，按钮「唱给他们」。没选就关掉仍退还乐韵、清冷却（原逻辑没动）。

## 决定与偏差
- 「图标」：子职业用徽记，能力 / 法术用能力图标，伙伴用动物头像，派系用派系图标；「特性」类行（额外攻击等）用职业背景图。
- 一览里法术按「环」一行而不是每个法术一行（每个施法职业有 40–90 个法术，逐个列会上千行），完整名单在悬停里。
- 「已获得」的判定：职业能力 = 职业等级够（或已经会）；带技能门槛的终极能力只有真的会了才算；法术环位 = 能施放这一环；秘法玄奥 / 秘法奥秘 / 分支 = 已选（到了等级没选显示金色「待选择」）；职业水晶 = 已吸收；秘传槽按冒险者等级。
- 一览按（选中职业、CasterSpells.Version）缓存，最多 30 帧刷新一次；只在 UI 线程用，不涉及渲染线程。
- 伙伴和传颂之歌窗口内容少，没有做成两栏（单栏列表 + 标题条）。

## 没有在游戏里看过的部分
- 伙伴、传颂之歌两个窗口的截图我没看（测试里画过、没有报错）；伙伴列表在测试地图上可能是空的。
- 真实存档里多职业、有传奇职业点时左栏顶部传奇区的样子；小分辨率（1024×600 以下）下两栏的挤压情况只靠 Fit 和滚动保证。
