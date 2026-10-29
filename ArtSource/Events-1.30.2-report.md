# DND边缘大陆 1.30.2 报告：1.28.0 事件的两处修改

需求：`ArtSource/Events-1.30.1-spec.md`（目标版本改为 1.30.2）。开工时 About.xml 是 1.30.1，游戏没在运行。备份在 `ArtSource/backup-1.30.1/`（PortalEvent.cs、GiantMigration.cs、EventChecks.cs、Events_GiantMigration.xml、About.xml、使用说明.txt、1.7/Assemblies/LichPhylactery.dll）。

## 改动
1. **裂口崩溃后野兽平静**（`Source/PortalEvent.cs`、`Source/GiantMigration.cs`）
   - 用的是 `wip-1.30.1` 的半成品。设备上的 PortalEvent.cs（17:59）比半成品（20:50）早，逐行对比后差异只有半成品自己的改动，所以直接合并进来；注释里的版本号改成了 1.30.2。
   - 崩溃时，之前各波还活着的野兽立即平静（和关闭裂口用同一个 `CalmBeasts`）。最后一波照常狂暴，记进 `EventState.pendingCalms`（存档键 `lpPendingCalms`），1 小时后平静。崩溃仍然不掉残渣。有派系的寒冰魔、元素裔不受影响。
   - 补上了半成品缺的部分：`EventState.GameComponentTick` 每 250 刻调用一次 `PortalEvent.TickCalms`。
2. **食人魔一家归余烬氏族**
   - `Defs/Events_GiantMigration.xml`：`LP_FC_OgreWanderer` 的父级改成 `LP_ECBase`。defName 保持不变，兼容旧存档；它本来就不在任何袭击组或商队组里，所以同盟不会再用这个兵种。
   - `GiantMigration.cs`：新增 `IsEmberKind`，ember 和 ogre 两种队伍都归余烬氏族，余烬氏族的设置开关也同时管这两种。留下居住的信件按队伍所属派系写“余烬好感 -5”（`Join` 本来就是按派系扣好感）。讨食好感走 `lord.faction`，所以自动按余烬算。礼物仍是食人魔大棒。
   - `PortalEvent.cs`：设置里巨人迁徙的说明改了。旧存档里已经在路上的食人魔队伍仍属同盟，走完这一趟为止。
3. `使用说明.txt`：标题改为 1.30.2；第一部分、第二部分里的对应句子改了，1.28.0 更新日志加了“已被取代”的注释；更新日志新增 1.30.2 一节。`About.xml`：版本号改为 1.30.2，描述加了一句。

## 检查（EventChecks.cs）
- 新增：崩溃后旧波野兽平静；最后一波仍狂暴并登记为 1 小时后平静；时间没到不会平静；时间到了以后，通过真实的 GameComponentTick 走完平静流程。
- 新增：留在殖民地的食人魔会扣余烬氏族好感（信件里写着 -5）；食人魔流民归余烬、泰坦流浪者归同盟。
- 修改：“三种组成”里的食人魔队伍派系改为余烬氏族；“只用于迁徙的兵种”这项检查同时核对同盟和余烬两边的兵种组。

## 测试
- `Build.ps1`：PASS（主体、VPE 桥、Mythic 桥）。
- `Test.ps1 -Only Events`：**PASS: 236 checks**，含存档往返。日志里新增的各项都显示 OK（崩溃那次放出 12 个，之前各波 6 只全部平静，最后一波 12 只按时平静）。
- `Test.ps1`（完整）：**跑了 1 次，FAIL after 1214 checks**，失败在 1.20.0 的老检查 `combat group generates for LP_FaerunHostile: 15`（FactionChecks，塞伊远征军战斗组随机生成）。1.30.1 的完整测试也挂在这一项上，它不涉及事件和迁徙代码。按要求没有重跑；失败点之后的检查这次没有跑到。
