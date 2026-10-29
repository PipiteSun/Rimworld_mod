# 1.25.1：力能龙 → 力能龙裔（改名报告）

日期：2026-10-25。备份：`ArtSource/backup-1.25.0`（1.7、About、Bridges、Defs、Patches、Source、使用说明.txt、LoadFolders.xml，共 174 个文件）。

原则：只改给玩家看的文字（label、description、resourceDescription、customSummary、信件/消息/检视文本、设置说明、使用说明）。**没有改任何 defName、类名、存档键**：异种型仍是 `LP_ForceDragon`，基因 `LP_ForceDragonHeart`，龙蛋 `LP_ForceDragonEgg`，剧本组件 `LP_ForceDragonClutch`，代码里的种族键仍是 `"ForceDragon"`。旧存档不受影响。

搜索范围：Defs、Patches、Bridges、Source（*.cs）、About.xml、使用说明.txt。模组里没有 Languages 目录；Patches 里没有“力能龙”。

## 1. 改动的文件

| 文件 | A 类替换（处） | 备注 |
|---|---|---|
| `Defs/ForceDragon.xml` | 16 | 异种型名称与说明、各基因说明、资源条说明、龙蛋说明 |
| `Defs/Scenarios.xml` | 6 | 剧本组件名、龙蛋剧本说明、人物页摘要/槽位说明 |
| `Defs/Items_Materials.xml` | 2 | 龙鳞蜕说明 |
| `Defs/Items_Throwables.xml` | 2 | 驱散粉尘及其健康状态说明 |
| `Defs/Items_RaceGear.xml` | 2 | 蜕鳞甲说明 |
| `Bridges/MythicBloodlines/Defs/MythicBridge.xml` | 2 | 狻猊心情说明、“力能龙之肉”状态说明 |
| `Source/ForceDragonHoard.cs` | 4 | 开局提示、龙蛋检视文本、剧本组件摘要、日志警告 |
| `Source/Core.cs` | 4 | 模组设置里的三条说明 |
| `Source/Items.cs` | 1 | `RaceLabel("ForceDragon")`（“只有××才能制作”提示） |
| `About/About.xml` | 3 | 另改版本号 `modVersion` 1.25.1；描述末尾加 1.25.1 一句 |
| `使用说明.txt` | 15 | 标题版本号；第一、二部分；新增 1.25.1 更新记录 |

A 类合计：Defs/Bridges/Source/About 42 处，使用说明 15 处（不算标题版本号和新增的 1.25.1 条目）。全部改完后检查过：没有“力能龙裔裔”；所有改动的 XML 都能正常解析。

## 2. A 类改动清单（已改为“力能龙裔”）

**Defs/ForceDragon.xml**
- L8 异种型 label：`力能龙` → `力能龙裔`
- L9 异种型说明：“力能龙的强大是攒出来的钱…” → “力能龙裔的强大…”（第一句“源自《史诗等级手册》的力能巨龙”讲的是 DND 原型，没改）
- L44 力能龙之心说明：“力能龙的本源”“力能龙不受EMP影响” → 力能龙裔（基因名本身是 B 类，没改）
- L53 力能资源条说明：“力能龙体内流转…” → 力能龙裔
- L74 力场免疫：“力能龙对一切力场效果免疫，包括别的力能龙的吐息” → 两处都改
- L136 肉食者、L152 龙之傲慢、L170 囤积癖、L188 龙龄：说明开头的“力能龙” → 力能龙裔
- L161/L162 龙之傲慢效果条目：“对非力能龙发起侮辱”“非力能龙的同住者” → 非力能龙裔
- L179 囤积癖效果条目：“力能龙对小偷陷入杀意狂暴” → 力能龙裔
- L316 龙蛋说明：“必须放在力能龙的宝库里…孵出的是一只力能龙幼龙婴儿” → “必须放在力能龙裔的宝库里…孵出的是一个龙龄为幼龙的力能龙裔婴儿”

**Defs/Scenarios.xml**
- L28 剧本组件 label：`力能龙的宝库与龙蛋` → `力能龙裔的宝库与龙蛋`
- L474 龙蛋剧本说明：“一条年老的力能龙” → “一位年老的力能龙裔”；“开局的力能龙已是古龙” → 力能龙裔；“力能龙只吃肉” → 力能龙裔
- L482 customSummary：“一条力能龙和两名仆从” → “一名力能龙裔和两名仆从”
- L487 人物页槽位说明：`力能龙` → `力能龙裔`

**物品**
- Items_Materials.xml L34 龙鳞蜕：“力能龙蜕下的鳞片”“力能龙每晋升一级龙龄” → 力能龙裔
- Items_Throwables.xml L276/L307 驱散粉尘：“力能龙的偏折、朦胧与错位失效” → 力能龙裔
- Items_RaceGear.xml L327 蜕鳞甲：“用力能龙蜕下的鳞片”“力能龙自己不屑于穿” → 力能龙裔

**神兽血裔联动（MythicBridge.xml）**
- L44 狻猊种族心情说明：“力能龙也爱好东西” → 力能龙裔
- L325 “力能龙之肉”状态说明：“饕餮吞下力能龙的血肉” → 力能龙裔（状态名是 B 类，没改）

**代码里的玩家文本**
- ForceDragonHoard.cs L103 开局消息：“请自行为力能龙建一间独占卧室” → 力能龙裔
- ForceDragonHoard.cs L190 龙蛋检视：“需要放在力能龙的宝库里才会孵化” → 力能龙裔
- ForceDragonHoard.cs L199 剧本组件摘要：“开局人物页中的力能龙已是…” → 力能龙裔
- ForceDragonHoard.cs L205 日志警告：“找不到力能龙开局角色” → 力能龙裔
- Items.cs L64 `RaceLabel`：`"ForceDragon"` 返回 `力能龙裔`（只改返回值，键不变）
- Core.cs 设置页：巨躯规则说明“…牛头人和力能龙（按龙龄）”、“力能龙龙龄晋升门槛”、宝库被窃狂暴说明“外人从力能龙的宝库…力能龙对小偷…” → 力能龙裔

**About.xml**
- 版本 1.25.0 → 1.25.1
- 描述：“火巨人、巨魔和力能龙异种人”“力能龙是DND史诗巨龙的人形化身”“龙蛋（力能龙）” → 力能龙裔
- 描述末尾加：“1.25.1 异种族“力能龙”改名为“力能龙裔”（只改显示名称，旧存档不受影响）。”

**使用说明.txt**
- 标题“使用说明（1.25.1）”
- 第一部分：设置说明（L6）、种族一览“力能龙裔：可遗传…”（L14）、神兽血裔种族心情（L30，两处）、饕餮进食（L32，两处，“力能龙之肉”状态名没改）、狻猊与宝库（L33）、巨躯规则（L45）
- 第二部分：驱散粉尘（L227）、蜕鳞甲（L256）、龙鳞蜕（L261）、巨躯规则四处（L274、L276、L277、L279）
- 第三部分新增“1.25.1：异种族“力能龙”改名为“力能龙裔””一条，说明改了什么、没改什么、旧存档兼容、备份位置

## 3. B 类：本轮未改（龙本身或具体部件/特性的专有名）

| 原文 | 所在位置 | 建议改法 |
|---|---|---|
| 力能龙之心（基因名） | `Defs/ForceDragon.xml` L43（`LP_ForceDragonHeart`）；`Source/ForceDragonPowers.cs` L133 能力禁用提示“需要力能龙之心。”；使用说明 L29；更新日志多处 | 若要统一：“力能龙裔之心”，或更简洁的“龙裔之心”。基因名和提示要一起改 |
| 力能龙蛋（物品名） | `Defs/ForceDragon.xml` L315（`LP_ForceDragonEgg`）；`Source/ForceDragonHoard.cs` L73 信件“产下了N枚力能龙蛋”、L185 消息“一枚力能龙蛋孵化了”；`Source/Core.cs` 设置“力能龙蛋孵化天数”；`Defs/Scenarios.xml` L476 剧本摘要“快孵化的力能龙蛋” | 保留“力能龙蛋”也说得通；要统一可改“力能龙裔之卵”或简称“龙蛋”。物品名、信件、设置、剧本摘要五处要一起改 |
| 力能龙之肉（神兽血裔联动健康状态名） | `Bridges/MythicBloodlines/Defs/MythicBridge.xml` L324（`LPMB_DragonFlesh`）；About.xml 1.21.0 段；使用说明 L32 | “力能龙裔之肉”。说明文字已经改成“力能龙裔的血肉” |
| 龙龄阶段：幼龙、少年龙、成年龙、古龙、伟龙 | `Source/ForceDragon.cs` L29 `AgeLabels`；基因/能力说明里的“少年龙解锁”“伟龙隐形”等；剧本里的“一条古龙” | 建议保留（这是 DND 龙龄术语）；如要改，可用“幼年龙裔…伟龙裔”，但能力说明要全部跟着调 |
| 龙爪、龙鳞、龙体、龙之傲慢、伟龙隐形、龙龄（基因/健康状态名） | `Defs/ForceDragon.xml` L90、L106、L121、L151、L187、L240 | 文本里没有“力能龙”，本轮不涉及；建议保留 |
| 龙鳞蜕（材料名）、蜕鳞甲 | `Defs/Items_Materials.xml`（`LP_DragonShed`）、`Defs/Items_RaceGear.xml` | 建议保留 |
| 剧本名“龙蛋” | `Defs/Scenarios.xml` L473 | 建议保留 |

**拿不准、先没改的（一并列出）**
- **历史更新日志**：使用说明第三部分 1.23.3 及更早的条目（22 处“力能龙”）和 About.xml 描述里 1.23.0～1.23.3 那几句（4 处）。使用说明第三部分的惯例是“原文保留”，所以没动；1.25.1 条目里注明了“更早的更新日志仍写作‘力能龙’”。About.xml 的描述会显示在游戏模组列表里，如果你希望那里统一，告诉我就改。
- 异种型说明第一句“源自《史诗等级手册》的力能巨龙”：讲的是 DND 原型生物，没改。
- 代码注释（BodySize.cs、ForceDragon.cs、ForceDragonHoard.cs、ItemsMaterials.cs、SmallBody.cs、ItemChecks.cs、ForceDragonChecks.cs、联动的 MythicBridge.cs/BridgeChecks.cs）和 Scenarios.xml L469 的 XML 注释：玩家看不到，没改。

## 4. 测试结果

测试里没有断言旧显示名“力能龙”的地方，所以测试代码没改。

- **Build.ps1**：通过（主程序集 + 神兽血裔联动程序集）。
- **Core**（`Test.ps1`）：跑了 4 次，**2 次 `PASS: 3241 checks`**（和 1.25.0 报告里的数量一致，日志过滤没有命中）。另外 2 次失败都是老检查：“Golem after resurrection actually completes cleaning”（第 2905 项，1.25.0 报告里同一区域也有偶发）和“smite reaches 10 cells”（第 2139 项，这一项在 1.25.0 原样的对照运行里也失败过）。最后一次运行是通过的。
- **Mythic**（`Test.ps1 -Mythic`，因为改了联动 XML）：没有一次通过。
  - 第 1 次：游戏启动 5 秒后卡住（日志停在 Harmony 加载），360 秒超时，还没开始检查。
  - 之后 4 次：3 次停在第 1315 项“[mythic bridge] real acid damage is recorded for trolls: AcidBurn”，1 次过了这一项、停在第 2178 项寒冰魔“chill wears off away from the aura”。
  - **1.25.0 原样对照**（临时拷回备份里的 11 个文件，编译跑 3 次，之后按哈希恢复，确认一致并重新编译）：3 次也都失败，分别停在第 662 项（基因冷却）、第 1699 项（精灵锁子甲闪避）、第 2249 项（神罚射程）。能跑到第 1315 项的 2 次都过了酸蚀检查。
  - 判断：酸蚀这一项**与本次改名无关**，属于已知的随机护甲偏转抖动。依据：原版 AcidBurn 的护甲类别是 Sharp，测试只给巨魔 2 点伤害、没有穿甲、命中部位随机，护甲偏转后伤害为 0，就不会记录酸蚀；这一项和显示文本没有任何关系。改名后它失败的次数比对照组多（4 次里 3 次，对照组 2 次里 0 次），但样本太少，看不出差异。Mythic 档无论改不改都过不了，失败点分散在多个老检查上。
- **Full**（`Test.ps1 -Full`）：跑了 3 次都失败，分别停在第 1030 项（牛头人“troll bond raises real melee damage by 5%: 1.000”）、第 2210 项（“real club damage in human hands is 60%: 1.000”）、第 1366 项（同样是上面的酸蚀检查）。三项都是“实际伤害”类检查，比值 1.000 或者没有记录，符合完整模组列表下已知的随机护甲偏转抖动；1.24.0、1.25.0 的 Full 也从来没有通过过。**与本改动无关。**
- 没有在游戏里实际打开看文字；文本正确性靠逐条替换计数（每条都核对了期望出现次数）和替换后的检索来保证。
