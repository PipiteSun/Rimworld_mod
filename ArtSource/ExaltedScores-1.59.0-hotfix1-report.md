# 1.59.0 热修1：超凡属性特性看不到（版本号保持 1.59.0）

## 现象
Peter（装着 Isekai）有角色属性达到 20，但「冒险者」分页和特性栏里都看不到超凡特性。

## 原因
1. **Peter 的设置**（`Config\Mod_LichPhylactery_LichMod.xml`，只读查看）：文件里只有一项非默认值 `adventurerEnemyLegendCoexist=True`，其余全是默认，即：
   - 冒险者等级 `adventurer` = 开；超凡总开关 `exaltedScores` = 开
   - Isekai 共存模式 `adventurerIsekaiCoexist` = 开（检测到 Isekai 时的默认）
   - 共存模式下超凡特性仍生效 `exaltedCoexist` = 关（1.59.0 的默认）
   
   所以 `Exalted.On` = false，`Compute` 返回 0：超凡掩码恒为 0。
2. **分页隐藏**：「超凡」小节只在 `exaltedSent | exaltedMask` 非 0 时画；共存模式下掩码一直是 0、从没发过信（`exaltedSent`=0），小节整个不画；数字的金色也只看掩码，所以也是白色。没有任何地方提示“效果被共存模式关了”。
3. **特性栏**：超凡本来就不是 TraitDef，特性栏里不会有。
4. **存档核对**（`Saves\龙宫.rws`，只读解析，玩家派系 = 龙宫 loadID 59）：
   - 龙王（Hokojin，18 级）：力量 20（原始 20，上限 20，无典籍 / 许愿 / 威能），有效值 20
   - 邪魔（原石，玛丽莉丝，17 级）：力量 20（同上），有效值 20
   - 两人 `lpExaltedInit=True`、`lpExaltedSent` 缺省（0）：算过基准，但因掩码为 0 从没获得过。
   
   分页显示的 20 是原始值 `scores[i]`，掩码用的有效值是 `min(scores, Cap)`；这两人上限都是 20，两个值一致，**不是显示值与有效值不一致的问题**。
5. 缓存重算：加点（`SpendPoints` → `Dirty`）、读档（`stage=null` + `Exalted.Reset` 重登）、上限变化（250 刻 `Tick` 比较补 `Dirty`）都会重算，没有发现别的漏洞；加了一条加点 19→20 立即生效的检查。

## 改动
- **a) 分页永远显示**（`Exalted.Raw`、`Exalted.OffReason`、`AdventurerUI.DrawExalted`、六维数字着色、`Exalted.Tip`）
  - 新增 `Exalted.Raw(h)`：只看有效值、不看开关的掩码；`Compute` = `On ? Raw : 0`。
  - 有效值 ≥20 时「超凡」小节和数字一定显示：生效时金色 / 神话紫色；没生效时灰色，格子标“（未生效）”，标题“超凡（效果未生效）”。
  - 悬停写原因：`Isekai 共存模式：效果已关，可在设置「冒险者」中开启（勾选“共存模式下超凡特性仍生效”）`；或“超凡属性特性已在设置里关闭”/“冒险者等级已在设置里关闭”。降到 20 以下的仍是“暂停”。
- **b) 默认值与迁移**（`Settings.exaltedCoexist` / `exaltedVersion` / `MigrateExalted`）
  - “共存模式下超凡特性仍生效”默认改为开（超凡大多是规则效果，和 Isekai 数值不叠加）。
  - 新增设置版本 `exaltedVersion`（当前 1）。读入没有这个键的旧设置文件时迁移一次：`exaltedCoexist=true`、版本 1。之后玩家自己关掉会连同版本 1 一起存，不会再被改。Peter 的设置文件读入后就是开。
  - 设置项悬停说明同步更新。
- **c) 健康页标记**（`Hediff_ExaltedMark`、`Defs/Exalted.xml` 的 `LP_ExaltedMark`、`Exalted.SyncMark`）
  - 一个显示用状态列出全部达标项，例如“超凡·巨人之力、神话·无影”；没有阶段、不改任何属性、不算疾病、不能治。
  - 由冒险者状态每 250 刻的 `Exalted.Tick` 同步（不在每刻做）；生效时金色（有神话时紫色），效果没生效时灰色并带“（未生效）”，悬停写原因和各项效果。
  - 冒险者等级 / 超凡总开关关、人死了、降到 20 以下时标记 mask 置 0，由 `ShouldRemove` 移除。
- **d)** 未发现其它原因；加检查确认加点到 20 后掩码立即重建。
- 读档后第一次 250 刻：龙王、邪魔会各收到一封“超凡·巨人之力”的来信（迁移后效果第一次生效，属于新获得）。

## 文件
- `Source/Exalted.cs`、`Source/AdventurerUI.cs`、`Source/ExaltedChecks.cs`、`Defs/Exalted.xml`、`使用说明.txt`（【1.59.0 超凡属性特性】段末尾加一行）
- 备份：`ArtSource/backup-1.59.0-exalted-fix`（以上文件 + `Adventurer.cs` + 旧 dll）。`Genie.cs` 没有动。
- 使用说明与备份 diff：只多 1 行，编码 / 换行不变。

## 测试
| 命令 | 结果 |
|---|---|
| `Test.ps1 -Only Exalted` | PASS 33（原 28 + 热修 5；无 Isekai 时 Isekai 那条占位） |
| `Test.ps1 -Isekai -Only Exalted` | PASS 33（Isekai 分支实际执行：共存 + 覆盖关 → 灰色、原因含“Isekai 共存模式”和「冒险者」，标记“未生效”；打开覆盖 → 生效） |
| `Test.ps1 -Only Adventurer` | 第一次 FAIL @69（`CheckAdventurerStage3` 传奇冒险者那条，社交影响 ×1.1 / 心情 / 好感组合检查）；重跑两次都 PASS 123 |
| `Test.ps1 -Isekai -Only Adventurer` | PASS 126 |

新增检查：
- 热修 a：超凡关着时 `Raw`=1，悬停写原因；重新打开恢复普通金色悬停。
- 热修 d：加点 19 → 20 立即得到巨人之力。
- 热修 c：标记标签“超凡·巨人之力”/“超凡·巨人之力、神话·无影”，无阶段，可见，悬停含效果；关掉后待移除；19 不加。
- 热修 b：按 Peter 的设置文件格式读入 → 共存仍生效 = 开、版本 1；带版本 1 的“关”保持关；迁移只做一次。
- 热修 a/c（-Isekai）：见上表。

Adventurer 第一次的失败：日志已被后面的运行覆盖，没能确定是哪一项不满足；标记状态没有数值，超凡规则本身没改，连跑两次和 Isekai 版都通过，按偶发处理，没有改。
