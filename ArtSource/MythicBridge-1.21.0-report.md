# DND边缘大陆 1.21.0 报告：神兽血裔联动（DND 侧）

日期：2026-10-24。Peter 在项目对话里确认可以修改 LichPhylactery 后才开始动手。改前备份在 `ArtSource/backup-1.20.0/`（About、Defs、Patches、Source、使用说明.txt、旧 LichPhylactery.dll，共 120 个文件）。神兽血裔（1.4.4）和人类异禀都没有改，1.4.4 的接口也没有发现 bug。

## 1. 结构

| 文件 | 说明 |
|---|---|
| `LoadFolders.xml`（新） | 1.7 版加载 `/`、`1.7`，并且只在 `IfModActive="local.standalone.mythicbloodlines"` 时加载 `Bridges/MythicBloodlines`。 |
| `Bridges/MythicBloodlines/Source/MythicBridge.cs`（新） | 联动逻辑，命名空间 `LichPhylactery.MythicBridge`。 |
| `Bridges/MythicBloodlines/Source/BridgeChecks.cs`（新） | 联动检查，只在两个模组都启用时运行。 |
| `Bridges/MythicBloodlines/Assemblies/LichPhylactery.MythicBridge.dll`（新） | 联动程序集，引用 LichPhylactery.dll 和 MythicBloodlines.dll。 |
| `Bridges/MythicBloodlines/Defs/MythicBridge.xml`（新） | 16 个种族心情、3 个短期健康状态。 |
| `Bridges/MythicBloodlines/Patches/TrollBlood.xml`（新） | 巨魔血可饮用（只有饕餮会喝）。 |
| `Source/MythicBridgeHook.cs`（新） | 测试入口：按类型名反射调用联动检查。主程序集**不引用** MythicBloodlines.dll，也不引用联动程序集。 |
| `Source/Checks.cs` | `Run()` 里加 `CheckMythicBridge()`，`AfterLoad()` 里加 `CheckMythicBridgeAfterLoad()`。 |
| `Source/Build.ps1` | 主程序集编译完后，再编译联动程序集；找不到 MythicBloodlines.dll 时跳过，并打印 SKIP。 |
| `Source/Test.ps1` | 新增 `-Mythic` 和 `-Aptitudes` 开关：在 Core 档的模组列表里，把神兽血裔或人类异禀插到本模组之前，跑完整的 `-lichtest`。 |
| `About/About.xml` | 1.21.0，描述加一句联动说明。依赖不变（`loadAfter` 里本来就有神兽血裔）。 |
| `使用说明.txt` | 标题版本改为 1.21.0；“内容一览”的种族段落后加了“神兽血裔联动”一段；更新日志开头加了 1.21.0 条目。 |
| `1.7/Assemblies/LichPhylactery.dll` | 重新编译（只多了测试入口）。 |

没装神兽血裔时，联动文件夹根本不会加载：Core 档检查确认联动程序集和 `LPMB_*` 定义都不存在，日志也没有报错。

## 2. 实现

种族一律按基因判断。血裔用 `BloodlineAPI.GetBloodline`。DND 种族沿用 1.20.0 的判断：
- 巨魔：`Troll.Is`
- 矮人：`CommonRaces.IsDwarf`
- 火巨人：`FireGiant.Active`
- 力能龙：`ForceDragon.Active`
- 夺心魔：`MindFlayer.IsMindFlayer`
- 精灵：异种型 LP_Elf，或带激活的 `LP_ElfBloodline`（1.20.0 没有现成的精灵判断，这条是我补的）
- 巫妖：有巫妖之躯，或带激活的 `LP_LichGene`

**1. 种族心情**
- 原有的 `RacialRelationDef` 是按异种型判断的，所以我另写了 `ThoughtWorker_MythicRelation`，按基因判断，原机制不变。每对关系拆成两个方向的 ThoughtDef，共 16 个。
- 生效条件：同一地图上有对方种族的本殖民地成员或囚犯（不算自己），访客和敌人不算。这和原机制一致。
- 地图普查每 250 刻最多重建一次，不存档。
- 数值：狻猊—火巨人 ±2，狻猊—力能龙 +2（龙一方 +3），句芒—精灵 +2，句芒—火巨人 −2，白泽—夺心魔 −3，白泽—巫妖 −2，饕餮—巨魔 +2，饕餮—矮人 −1。

**2. 甘霖解巨魔之火**
- 1.20.0 的“被火打断再生”不是健康状态，而是 `Gene_TrollRegeneration.suppressedUntilTick`，火和酸共用这一个时间点。
- 为了区分两者，联动在 `PostApplyDamage` 上记录巨魔受到酸蚀伤害的时间（判断 defName 或伤势 defName 是否含 Acid，实测原版 `AcidBurn`），结束时间与原版阻断相同，存档保存。
- 甘霖事件触发时：没有酸蚀，阻断立即结束；有酸蚀，阻断缩短到酸蚀结束的时刻。

**3. 饕餮进食**
- **巨魔血**：原本不可食用。补丁把它改成可饮用，`NeverForNutrition`（AI 不会自己去喝），营养 0.05，一次一瓶；同时补丁 `FoodUtility.WillEat`，让非饕餮不会把它当食物。按事件里消耗前读取的 ThingDef 判断，饮用后获得 12 小时的“巨魔血余力”：
  - 每 250 刻愈合 0.125 点伤势（巨魔是 0.5），失血恢复也是巨魔的 25%
  - 不长肢体，没有复活逻辑，着火时暂停
- **火巨人、力能龙的肉**：两者都是 Human 种族，屠宰出来的都是原版 `Meat_Human`，只看 ThingDef 区分不出来（spec 预料到了这种情况）。我的做法：
  - 补丁 `Corpse.ButcherProducts`，把火巨人、力能龙屠宰出的人肉记下来源（存档保存）
  - 补丁 `ThingWithComps.CanStackWith`，来源不同的肉不合并；补丁 `Thing.SplitOff`，拆分时保留来源
  - 事件里查这块肉的来源：火巨人 → 24 小时“火巨人之肉”（火焰伤害 ×0.5）；力能龙 → 24 小时“力能龙之肉”（力场伤害 ×0.5）
  - 伤害名单写在 XML（`DamageFactorExtension`），在 `Pawn.PreApplyDamage` 里生效：火焰包括 Flame、Burn、含 Flame/Incendiary 的伤害，排除 Acid；力场包括 `LP_Force` 和含 Force 的伤害
  - 所以只有通过屠宰得到的肉有效；1.21.0 之前已经屠宰好的肉没有来源记录，按普通人肉处理
- 三个状态都是可见的短期健康状态，重复进食只刷新时间。
- 脑浆罐、夺心魔蝌蚪本来就不可食用，这次只做了测试。

**4. 狻猊与宝库**
- 天工点化成功事件给物品加标记，存的是物品引用，随存档保存。
- 在 `DragonHoard.Value` 的后置补丁里，宝库房间内带标记的物品额外加 10%×`ItemValue`。这是在原有市值（以及火巨人锻造品双倍）基础上算的；打包家具按外层的打包物计价。

**5. 白泽识破**
- “支配人类”（`LP_Dominated`）是真正的原版精神状态，神兽血裔按原版流程净化，只测试了。
- 精神状态解除后，30 秒的“被支配”健康状态还会残留；它到期时按原有逻辑结束，会让人恍惚。联动订阅者会在下一次识破时移除它，走的是同一条原有结束流程。
- “心灵束缚”只是一次性降低信念，没有持续状态，所以不需要解除。
- 另外，订阅者会解放幽暗殖巢奴仆的 `LP_Thralled`（被支配）。这种状态不是精神状态，解放流程与斩首解放相同：清醒、倒地、脱离派系。
- 订阅者只在 execute=true 时执行，execute=false 只回答能否解除。

## 3. 测试

所有测试都用 pwsh 7。构建：`PASS: LichPhylactery built…` + `PASS: Mythic Bloodlines bridge built.`

| 档 | 1.20.0 | 1.21.0 | 说明 |
|---|---|---|---|
| Core（不装神兽血裔） | 1959 | **PASS 1960**，日志无报错 | +1 是“联动只在启用神兽血裔时加载”这项检查 |
| Core `-Mythic` | — | **PASS 2062**，日志无报错 | 新增 102 项联动检查，其中 2 项在读档后 |
| Core `-Mythic -Aptitudes`（DND + 神兽血裔 + 人类异禀） | — | **PASS 2062**，日志无报错 | |
| Appearance | 2159 | **PASS 2160**，日志无报错 | +1 同上 |
| Warcasket | 2010 | **PASS 2011**，日志无报错 | +1 同上 |
| VisualCompat | 11210 | **PASS 11210**，日志无报错 | |
| HealthCompat None / Human / Mythic | 284～286 / 267～269 / 267 | **286 / 269 / 267**，日志无报错 | |
| HealthCompat Both | 271（退出码 1：HA/MB 分类配置错误） | **PASS 271，退出码 0** | 神兽血裔 1.4.4 修好分类优先级后，这个第三方报错消失了 |

另外，在 DND 联动生效的情况下，我又跑了一次神兽血裔自己的测试：`-WithLich` PASS 262，`-WithAptitudes -WithLich` PASS 508，与 1.4.4 相同。Full 档没有跑：它用的是完整模组列表，1.20.0 时就因为其他第三方报错退出码为 1，而且本次改动在 Core 档已经覆盖。

联动检查（102 项）覆盖：
- 定义：16 个心情、3 个状态、巨魔血可饮用，都加载正常。
- 种族心情：每个种族的测试人物都按基因被识别；8 对关系双向的数值都正确；对方离开地图后心情消失；敌人不计入；无关种族没有心情；自定义异种型（原版人类加基因）也算。
- 甘霖：火焰会阻断巨魔再生；甘霖立即解除阻断；有酸蚀时阻断保留到酸蚀结束；原版 `AcidBurn` 的真实伤害会被记录；对非巨魔照常只是灭火。
- 饕餮：只有饕餮会把巨魔血当食物；真实的“饕餮进食”工作能喝下巨魔血，获得 12 小时状态；重复喝只刷新时间，不叠加；愈合速度是巨魔的 1/4；不长手指，不会获得巨魔复活。屠宰火巨人和力能龙得到的肉有来源标记，拆分后保留，不和普通肉合并；吃下后获得 24 小时状态；火焰和力场伤害各减半（实测 5/5），割伤不变（10）；普通人肉没有效果；脑浆罐、蝌蚪不能吃。
- 宝库：天工点化成功的物品被标记，未点化的不标记；宝库内带标记的物品按 110% 计价（实测 352 对 320），不带标记的按 100%；放到宝库外不加价。
- 识破：对不受控制的人仍然拒绝；能解放殖巢奴仆，并照常收费、进入冷却；支配人类按原版精神状态解除，残留的状态在下一次识破时清除。
- 存读档：天工点化标记和火巨人肉的来源都保留。

## 4. 偏离要求或我自己做的取舍

1. **心情全部按双向设置**：需求里只有“饕餮—巨魔”写了“双方”，其余没写方向。我按双向做（力能龙一方 +3）。如果只要单向，删掉对应方向的 ThoughtDef 即可。反方向的标签和描述是我自己写的。
2. **肉的来源**按屠宰时打标记来判断，没有新建肉的 ThingDef。代价是：屠宰前已有的肉、通过交易得到的肉都没有来源。
3. **巨魔血**原本不能吃，我用补丁把它改成可饮用，同时用补丁保证只有饕餮会喝（`WillEat`）。这只在联动启用时生效。
4. **识破**额外解放了殖巢奴仆的 `LP_Thralled`，这一条需求里没有。“心灵束缚”没有持续状态，所以没有可解除的东西。
5. **精灵的判断**：1.20.0 没有现成的精灵判断，我用的是异种型 LP_Elf 或精灵血统基因。
6. **测试脚本**：`Test.ps1 -Mythic/-Aptitudes` 会把模组插在本模组之前。原有的 HealthCompat 档仍然把模组接在本模组后面，我没有改；实测这种顺序下联动也能正常加载，没有报错。
7. **测试数量**：Core、Appearance、Warcasket 都多了 1 项，是新增的加载检查。

## 5. 已知问题和注意事项

- 如果先同时启用两个模组，之后单独关掉神兽血裔再读档：联动的健康状态会作为找不到的定义被丢弃，`BridgeState` 也会作为找不到的类被跳过，游戏会在日志里各提示一次，其余内容不受影响。这一点写进了使用说明。
- 天工点化标记和肉的来源保存的是物品引用。存档前会清掉已销毁的物品，以及既不在地图上、也不在任何容器里的物品（测试里一开始就因为这类“悬空”物品，读档时报了空键，已修复）。
- 种族心情不提供模组设置开关（原有的种族关系也没有）。
