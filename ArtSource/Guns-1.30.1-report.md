# 1.30.1 — 法杖去上限 + VPE 职业吃心灵敏感度 / 战士兼容 Isekai

备份：`ArtSource/backup-1.30.0/`（Source/Guns.cs、GunChecks.cs、ItemChecks.cs、Races124Checks.cs，Defs/Guns.xml，使用说明.txt，About/About.xml，Bridges/VPE 下 Abilities.cs、VPEBridge.cs、BridgeChecks.cs、Hediffs.xml、Paths.xml、Abilities_Fighter.xml、Stats.xml）。没有提交 git。每次编译、测试前都确认过 RimWorld 没在运行（全程没在跑）。

## 一、法杖魔力系数去掉上限
- `Source/Guns.cs`：G 组系数由 `Clamp(敏感度, 0.2, 2.5)` 改为 `Max(敏感度, 0.2)`，删掉 `StaffMax`。敏感度 0 仍不能用；F 组 0.5 + 0.5 × 敏感度、上限 2 不动；矮人保底、器械师“魔力共鸣”保底不动。
- 武器信息页“魔力系数”说明、三根法杖描述、`Defs/Guns.xml` 注释、使用说明.txt 第二部分与更新日志、About.xml（1.30.1）同步改成“最低 0.2，不封顶”。
- 检查（`Source/GunChecks.cs`，原规格写的是 Checks.cs，实际法杖检查在 GunChecks.cs）：敏感度 0 / 0.5 / 1 / 2 / 3 / 5 → 法杖 0 / 0.5 / 1 / 2 / **3** / **5**；F 组同表 0.5 / 0.75 / 1 / 1.5 / 2 / 2。

## 二、VPE 职业（Bridges/VPE）
1. **法师、牧师吃心灵敏感度**（Peter 确认：叠乘，不替换）。按协调说明没有给 `LP_ArcanePower`/`LP_DivinePower` 加 StatPart（那会把持续时间、半径、射程一起放大），而是：
   - `Classes.PsychicFactor(p, path)` = max(0.2, 心灵敏感度)，只对法师、牧师路径，不封顶；战士、器械师 = 1。
   - `ClassAbility` 重写 VEF 的 `GetPowerForPawn()`（× 系数），所以 `Power` 和**能力说明里显示的数值**一起变；`Strength`（魔法飞弹每发伤害、状态严重度）也 × 系数。
   - 受影响：火球、闪电束、解离、魔法飞弹、石墙耐久、治疗伤口、群体治疗、神圣打击、死亡守卫储存量。持续时间、半径、射程不变。
2. **战士兼容 Isekai**：
   - Isekai 的近战加成在哪：`ForgeStatPatches.xml` 把 `StatPart_RefineMeleeDamage`（每级 +8%，+10 = ×1.8）挂在小人的 `MeleeDamageFactor` 上，力量加成在 `IsekaiStatPatches.xml` 也挂在 `MeleeDamageFactor` 上；不是 Verb/DamageInfo 补丁。原版 `VerbProperties.AdjustedMeleeDamageAmount` 会读这个属性，VEF 的 `VEF_MeleeAttackDamageFactor` 也是在这个方法上的补丁。
   - 武器专精（`VEF_MeleeAttackDamageFactor` 状态）本来就在这条路上，没改；绊摔、战吼没有伤害。
   - **旋风斩**改为：每个相邻敌人调用该小人近战动词的 `ApplyMeleeDamageToTarget`（原版近战伤害结算，不掷命中），结算期间挂临时状态“旋风之势”`LP_WhirlwindFury`（自定义 `Hediff_ClassStrike`，`VEF_MeleeAttackDamageFactor` = 战技强度），结算完立即移除。所以伤害 = 武器近战伤害 × 近战伤害系数（含 Isekai 精炼/力量）× VEF 系数 × 战技强度。说明文本同步改了。
3. 描述：Paths.xml 法师、牧师路径描述，Abilities_Fighter.xml 旋风斩描述，使用说明.txt【1.27.0】段落与 1.30.1 更新日志，About.xml 1.30.1。

## 三、新增检查
- `-VPE`（BridgeChecks.cs）：
  - 夹具 `Make()` 去掉随机的“心灵敏感”特性，让原有法师/牧师断言（火球 22.5、治疗 30 等）在 100% 敏感度下保持不变。
  - `PsychicChecks`：系数函数 0→0.2、0.5/1/3/5 原样；敏感度 0.5 / 1 / 3 的真实小人（运行时临时状态设定敏感度，检查完摘掉，不进存档）——火球 11.25 / 22.5 / 67.5，解离、魔法飞弹同倍率，半径 2.5、睡眠时长不变；治疗 15 / 30 / 90（实际治疗量也对），群体治疗、神圣打击同倍率，祝福时长不变；战士回气不随敏感度变。
  - 旋风斩：结算后没有残留状态；同一击 × 战技强度 1 → 1.5（11.9 → 17.8）；给巨人之力药水（近战伤害系数 ×1.3）后跟着 ×1.3（17.8 → 23.2）。
- `-Isekai`（GunChecks.cs `CheckGunsIsekai`）：同一小人同一把长剑，Isekai 精炼 +10 后，旋风斩使用的近战伤害结算路径 ×1.8（23 → 41.4），与 `MeleeDamageFactor` 比值一致。VPE 不在 -Isekai 档里，所以旋风斩本身的 × 战技强度放在 -VPE 档检查。
- 另：`Races124Checks.cs` 的“smite reaches 10 cells”失败信息加了诊断（原因、坐标），检查本身没改。

## 四、测试结果
| 命令 | 结果 |
|---|---|
| `Test.ps1 -Only Guns` | **PASS: 259 checks**（法杖 ×3、×5 都过） |
| `Test.ps1 -VPE` | 第 1 次 249 项全过，但存档日志报红：运行时造的敏感度测试状态被已销毁的夹具带进了存档 → 改为检查完摘掉；第 2 次 **PASS: 249 checks**，无红字 |
| `Test.ps1 -Isekai` | 第 1 次 FAIL（1.22.0 老检查“被炼金之火烧死的巨魔不复活”，在新检查之前就停了）；第 2 次 **PASS: 621 checks** |
| `Test.ps1`（完整） | **3 次都没跑通，都挂在和本次改动无关的老检查上**：① 1.24.0 泰坦“神罚射程 10 格”（地图相关）；② 1.20.0 派系“塞伊远征军战斗组生成”；③ 1.20.0 共享组件“基因冷却后再次触发”（随机生成的小人吃致命脑伤）。这三处都不涉及法杖系数或 VPE 职业（完整档不加载 VPE）。1.30.0 的报告也记录过完整测试有多处老偶发、读档后半段没完整跑通。按 Peter“少跑”的要求没有继续重跑 |

结论：本次改动涉及的三组（Guns、VPE、Isekai）都通过；完整套件这次没有拿到一次 PASS，失败点都是旧的随机/地图相关检查。建议另开一次专门修这些老偶发检查（固定随机种子或挑格子），之后再跑完整套件确认。

## 五、没做的 / 注意
- 法师/牧师吃敏感度后，心灵失聪（敏感度 0）的法师仍按 0.2 倍施法（规格要求的下限）。
- 石墙耐久、死亡守卫储存量也走 Power，所以同样吃敏感度。
- 旋风斩现在必中、按武器真实近战伤害结算（原来是用单一数值直接 TakeDamage），数值会随武器与 Isekai 加成变化，可能比以前高。
- 没有进游戏手动试玩。
