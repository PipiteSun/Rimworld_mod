# 耐瑟热修 2026-10-28：“以命匣换卷”点了没反应

版本：1.61.0（About.xml 没改）。

## 查到的情况
- Player.log / Player-prev.log 里没有拉洛克、命匣或耐瑟相关的异常。只有一条无关的 `Inspect string for LP_Phylactery… contains empty lines`（命匣检视文字，以前就有）。
- 存档 名高级.rws（13:37:34）：`lpStage=envoy`，`lpEnvoyPending=True`，`lpLarlochHold` 是空的，Erisen 身上没有 LP_PhylacteryLost。也就是说选项 B 根本没执行。Erisen 是玩家殖民者，在 0 号地图，有 LP_Lich，命匣 LP_Phylactery1546382 在同一张地图上，绑定的就是他，所以 B 按钮的条件是满足的。
- 用同样的条件走一遍代码路径（信件 Choices → B 的 action → 浮动菜单 → 点巫妖），基础模组下能过，用 Peter 的 104 个模组（`-Full`）也能过。逻辑本身（ChooseB、OfferableLiches、给卷轴、改阶段）没问题。

## 原因（排除法得出）
A 和 B 是整个模组里唯一这样写的地方：点信件按钮先关掉对话框，再在同一次点击里弹一个 FloatMenu 选人。剩下唯一的嫌疑就是这个菜单在 Peter 的游戏里没起作用（没出来，或点了不执行），所以既没报错也没反应。真实鼠标点击没法在自检里复现，所以这一点是推断的，没有直接证实。

## 改动（只动耐瑟的文件）
- `Source/NetherilQuest.cs`
  - `Letter_LarlochEnvoy`：“以人换卷”“以命匣换卷”改为 `link` 到信件的下一页（新的 `Pick` 方法），每个可选的殖民者 / 巫妖一个按钮，再加一个“返回”。不再用 FloatMenu。交换不成时弹一条 RejectInput 消息，不会再一声不响。
  - 新增 `NetherilQuest.RecallEnvoy()`：在 refused/scheduled 阶段、使者不在时，退回一次拒绝并立刻派使者来。入口有两个：模组设置里耐瑟瑞尔任务线下面的“让拉洛克的使者马上再来”按钮（只在游戏中、而且处在这两个阶段时显示），以及开发者动作 `DND边缘大陆 / 拉洛克使者立即再来`（`NetherilDebug`）。
- `Source/NetherilChecks.cs`：新增 netheril 36：通过信件下一页选巫妖执行 B，检查命匣离开地图并进了 hold、巫妖有 LP_PhylacteryLost、殖民地拿到卡苏斯之卷（PlayerHolds）、stage=scroll、信件被移除；另外检查 RecallEnvoy 能退回一次拒绝（2→1）、使者在场，第二次调用会被拒绝。
- `使用说明.txt`：更新日志加了一行 1.61.0 热修。
- 备份：`ArtSource/backup-netheril-hotfix-1028/`（NetherilQuest.cs、NetherilChecks.cs、LichPhylactery.dll、使用说明.txt，都是改之前的版本）。

## 测试
- `.\Test.ps1 -Only Netheril`：FAIL after 32 checks，停在已知的老问题 #22（升神），#36 在它前面，通过了。
- 临时跳过 #22：`PASS: 35 checks (only Netheril); save/reload.` 跑完已经恢复 #22 并重新编译。
- 第一版诊断检查（还是浮动菜单写法）在 `-Full -Only Netheril` 下，任务部分 #14–#16、#36 都过了，后面停在 #9（装 VPE 时的法术学习，和这次无关）。

## 给 Peter 的补救
- 名高级.rws（13:37）里使者还在等（大约还剩 4.6 游戏小时）。重启游戏读这个档，打开“拉洛克的邀约”，点“以命匣换卷”，在下一页点“Erisen的命匣（等级 1）”就行。
- 如果读了更晚的档、使者已经走了（记成了第 2 次拒绝）：在模组设置里点“让拉洛克的使者马上再来”，或者用开发者动作，会退回那次拒绝，使者马上再来。
