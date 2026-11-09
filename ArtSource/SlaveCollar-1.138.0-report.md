# 1.138.0 奴隶麻醉项圈 报告

## 玩法
- 新衣物「奴隶麻醉项圈」（需要意识形态 DLC）：研究「DND炼金术」后在机械加工台制作，钢铁 30、零部件 1、药品 1，制作技能 5，工作量 4000。
- 奴隶衣物，颈部，Overhead 层（和帽子不冲突），压制度 +15%（同普通奴隶项圈）。贴图沿用原版奴隶项圈，染成紫色。
- 触发：戴着它的**奴隶**
  - 进入「叛乱」心理状态（要去煽动别人叛乱的那个人）；
  - 被卷进一场奴隶叛乱（大叛乱 / 局部叛乱 / 单人逃跑，都是 LordJob_SlaveRebellion）；
  - 进入任何攻击性心理状态（狂暴、杀人狂怒等，MentalStateDef.IsAggro）。
- 效果：心理状态立即结束、移出叛乱小队、停下当前工作，加「项圈麻醉」（意识上限 10%，30000 tick ≈ 半天后自己消失，止痛 ×0.5），压制度 +30%，头顶飘「麻醉！」和一缕烟，右下角一条消息（叛乱时合并成一条，列出被放倒的人）。
- 麻药囊充填 2 天（120000 tick），期间不再触发。奴隶身上有「麻醉项圈：就绪 / 充填中」按钮（说明里有剩余时间和触发次数；开发者上帝模式下点一下立即充满），地上的项圈检视信息也显示状态。
- 不触发：殖民者戴着、没戴项圈的奴隶、悲伤游荡之类不伤人的崩溃。

## 实现
- Bridges/Ideology/Defs/SlaveCollar.xml：LP_AnestheticCollar（HatMakeableBase，recipeUsers 改成 TableMachining）、LP_CollarAnesthetic。放在意识形态 Bridge 里，没有意识形态时不加载。
- Source/SlaveCollar.cs：CompProperties_AnestheticCollar / CompAnestheticCollar（lastFired、timesFired 存档）、SlaveCollar.TryFire / FireRebellion、两个 Harmony 后缀：
  - MentalStateHandler.TryStartMentalState（成功开始且是叛乱或攻击性状态、并且是奴隶）；
  - SlaveRebellionUtility.StartSlaveRebellion（6 参数版本；2 参数版本只是转调它）：成功后扫地图上所有 LordJob_SlaveRebellion 的成员。
- 防重入（busy 标志），出错只记一条 Warning 不影响原版流程。
- 备份：ArtSource/backup-1.137.0（旧 dll、About.xml、使用说明.txt）。

## 测试
- `Test.ps1 -Only SlaveCollar`：9 条全过（定义与配方；叛乱心理状态被放倒且压制 +30%；充填中不触发、充好后狂暴也触发；殖民者 / 无项圈 / 悲伤游荡不触发；真实调用 StartSlaveRebellion：两个戴项圈的被放倒并移出小队、没戴的照常叛乱；半天消退；醒来；按钮与检视信息；存读档）。

## 未在实机验证
- Peter 完整模组列表下的实际叛乱（其他模组改写叛乱流程时可能不经过这两个入口）。
- 戴着的外观（沿用原版贴图 + 紫色，没有新画贴图）。
