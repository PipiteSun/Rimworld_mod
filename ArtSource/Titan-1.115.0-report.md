# 战士子职业「泰坦战士」（1.115.0，2026-11-04）

战士的第三个子职业（另两个是奥法骑士、灵能战士）。战士 3 级选，选定不能改；能力在战士 3 / 7 / 11 / 15 级获得，和战士另两个子职业的节奏一样。

| 等级 | 能力 | 类型 | 效果 |
|---|---|---|---|
| 3 | 泰坦之躯 LP_TT_TitanBody | 被动 | 体型 ×(1 + 2.5% × 战士等级)，20 级 ×1.5；战士满 20 级后冒险者传奇等级（21～40）接着算，上限 ×2.0。部位生命在职业等级成长之外再 ×(1 + 3% × 战士等级)，20 级 +60% |
| 3 | 巨力之握 LP_TT_MightyGrip | 被动 | 搬运容量 ×(1 + 2% × 战士等级)；近战打中体型比自己小的敌人伤害 ×1.15，成功率 25% 击退 1 格 |
| 3 | 撼地践踏 LP_TT_Stomp | 主动，斗志 2，冷却 15 秒 | 半径 2.9 内敌人受 10 + 力量调整值 × 3 钝伤；比自己小的敌人成功率 60% + 体型差 × 30%（最高 95%）眩晕 2 秒；不伤友军 |
| 7 | 不动如山 LP_TT_Immovable | 被动 | 疼痛休克阈值 +10%，免疫击退，眩晕时长 ×0.5 |
| 11 | 泰坦化 LP_TT_Titanize | 主动，斗志 3，每天一次 | 60 秒：体型 ×1.25、部位生命 ×1.25、近战伤害 ×1.25、移速 ×0.9；结束休息值 −20% |
| 15 | 远古泰坦 LP_TT_AncientTitan | 被动 | 泰坦化 120 秒且不疲劳；泰坦化期间近战命中成功率 20% 触发小型践踏（半径 1.9、伤害 ×0.5、不耗斗志） |

## 决定
- **等级节奏**：照战士模板 3 / 7 / 11 / 15。设计稿的「泰坦化约 10 级」放到 11 级；巨力之握、撼地践踏和核心被动一起在 3 级。
- **体型只进 Pawn.BodySize，不进巨躯规则**：原版 `Pawn.HealthScale` = 生命阶段系数 × 种族 baseHealthScale，不看 BodySize（已用 IL 确认）；本模组巨躯规则（BodySize.cs）的 HealthScale / 近战加成只认它自己列出的来源（巨人血统、巨魔、小体型、龙龄、种族体型），不含泰坦之躯。所以泰坦战士的部位生命只吃这里的 +3%/级（泰坦化再 ×1.25），**不会因为体型变大再叠一次**；近战也不吃巨躯的 +30%/体型。
- **部位生命按战士等级（最多 20）**，不吃传奇等级——和现有规则一致（传奇等级不加等级成长，血量走传奇属性点）。体型才吃传奇等级。
- **绘制倍率 = √体型**（20 级 ×1.22，2.0 时 ×1.41），照双头巨人体型 2.0 画 ×1.4 的现有做法；线性放大会把 2.0 的人画成两格宽。头、衣服、身体附件随渲染根节点一起缩放；武器另用补丁按同一倍率放大（只放大贴图，握持位置不动）。
- **泰坦化的 ×1.25 乘在上限之外**（最大 2.0 × 1.25 = 2.5）。
- **泰坦化冷却**：用现有终极能力习惯的「每天一次」（60000 刻），不做长休计数。
- **泰坦化结束 / 部位上限变小时**：受伤部位的伤口按“新最大 / 旧最大”缩小（同 BodySize.Rescale 的做法），免得上限缩水把部位打烂或直接致死。
- **力量调整值**用 `Hediff_Adventurer.AMod(Str)`（含种族亲和），伤害公式不再乘战技强度，按设计稿原样。
- **撼地践踏不打倒地的敌人**（免得变成处决）；中立、友军都不打。
- **小型践踏**：设计稿只说“小型”，定为半径 1.9（身边一圈）、伤害 ×0.5。命中时先结算小型践踏、后结算击退，免得被打的目标先被推出范围。
- **免疫击退**作用于本模组共享的 `DNDPush.Move`（绊摔、念动推送、冲撞、震退、巨力之握……都走它）；其他模组自己的击退不管。
- **搬运容量**加在原版 CarryingCapacity（搬运一次的量）上；背包负重 `MassUtility.Capacity` 本来就按 BodySize 算，体型变大时自然变大。
- **共享注册表不改**：另有线程在改子职业文件，所以 Tick250 / 能力检查 / 能力效果的分派不编辑 Classes.cs、ClassAbilities.cs，而是用 Harmony 挂在 `ClassSystem.Tick250`、`ClassEffects.Invalid`、`ClassEffects.Apply` 上。唯一的共享文件改动是 Knockback.cs 一行（`DNDPush.Move` 太小，补丁可能被内联，直接加判断）。
- **类名 TitanWarrior**：`Titan` 已经被泰坦怪物（Source/Titan.cs）占用。检查组名 `-Only TitanWarrior`。

## Harmony 钩子（全部在 Source/Subclass_Titan.cs）与快速返回
| 补丁 | 目标 | 快速返回 |
|---|---|---|
| TitanWarriorTick（后缀） | ClassSystem.Tick250 | `Users` 为空且这人没选泰坦战士 → 返回；缓存键（能力位、战士等级、冒险者等级、泰坦化）没变 → 不重算 |
| TitanWarriorInvalid（后缀） | ClassEffects.Invalid | 已有结果或效果名不以 `TT` 开头 → 返回 |
| TitanWarriorApply（前缀） | ClassEffects.Apply | 效果名不以 `TT` 开头 → 照常执行原方法 |
| TitanWarriorBodySize（后缀） | Pawn.get_BodySize | `Users.Count==0` → 返回；之后一次字典查找 |
| TitanWarriorPartHealth（后缀） | PartHealth.WithoutReinforcement | `Users.Count==0` → 返回；倍率 1 → 返回 |
| TitanWarriorRenderSize（后缀） | PawnRenderNodeWorker.ScaleFor | `Users.Count==0` 或不是根节点 → 返回 |
| TitanWarriorWeaponOwner（前缀 + Finalizer） | PawnRenderUtility.DrawEquipmentAndApparelExtras | `Users.Count==0` → 倍率记 1 |
| TitanWarriorWeaponMatrix（转译） | PawnRenderUtility.DrawEquipmentAiming、DrawCarriedWeapon | `Matrix4x4.TRS` 换成 `TitanWarrior.WeaponTRS`；倍率为 1 时原样 |
| TitanWarriorDamage（前缀） | Pawn.PreApplyDamage | `Users.Count==0`、正在践踏（Busy）、不伤血 → 返回；攻击方不在缓存里 → 返回 |
| TitanWarriorStun（前缀） | StunHandler.StunFor | `Users.Count==0` 或时长 ≤ 1 → 返回 |
| （直接改）Knockback.cs | DNDPush.Move | `TitanWarrior.Immovable` 第一步就是 `Users.Count>0` |

缓存：`TitanWarrior.Users`（thingIDNumber → Entry：能力位、战士等级、冒险者等级、是否泰坦化、体型 / 绘制 / 部位 / 搬运倍率）。只在 Tick250 发现缓存键变化、泰坦化开始 / 结束时重算；重算时才通知体型变化（清最大饱食缓存）、标记渲染脏、刷新部位缓存。读档时 GameComponent_TitanWarrior.FinalizeInit 清空并为地图上的泰坦战士重建。

## 文件
- 新增：Source/Subclass_Titan.cs、Source/Subclass_TitanChecks.cs、Defs/Subclass_Titan.xml、ArtSource/RaceArt/draw_titan_subclass.py、Textures/LP/Classes/Titan/（TitanBody、MightyGrip、Stomp、Immovable、Titanize、AncientTitan、Emblem_Titan）、ArtSource/Preview/Titan-icons.png、本报告
- 改动：Source/Knockback.cs（1 行）、About/About.xml（版本 1.115.0 + 描述末尾一句）、使用说明.txt（1.115.0 更新日志一条，接在 1.114.0 热修之后）
- 备份：ArtSource/backup-1.114.0-titan/（旧 dll、About.xml、使用说明.txt、Knockback.cs）
- 图标：用现有 ArtSource 画法（draw_class_art 的 Icon：粗黑描边、平涂 + 右下暗部、低饱和）新画，`python -I ArtSource/RaceArt/draw_titan_subclass.py <模组根目录>`，只写自己的文件。

## 测试
`pwsh Source/Test.ps1 -Only TitanWarrior`：8 条全过，存读档通过。
1. 定义：战士第 3 个子职业，6 个能力 3/7/11/15，有图标、算战士，徽记独立
2. 战士 20：体型 ×1.5、绘制 ×√1.5、躯干生命 ×1.6、HealthScale 不变、搬运 ×1.4、缓存复用
3. 体型公式：传奇 30 级 ×1.75、上限 2.0、战士没满 20 不吃冒险者等级
4. 巨力之握：小目标 ×1.15 + 击退，友军不加成
5. 撼地践踏：扣斗志 2（打中回的斗志另算）、敌人受伤眩晕、友军不受伤、成功率公式
6. 不动如山：推不动、眩晕 120→60、疼痛休克 +10%
7. 泰坦化（11 级）：体型 / 部位 ×1.25、60 秒、结束休息 −20%、体型复原
8. 远古泰坦：120 秒、命中触发免费小型践踏、不疲劳、武器矩阵放大且转译补丁已挂上

## 没在游戏里验证
- 实际画面：放大后的人物、衣服、头发和武器位置是否好看（武器只放大贴图，握持位置没随体型外移）；和 Big and Small / Alpha Genes 等改体型模组叠加时的观感（体型是相乘的）。
- 体型对原版的连带效果（食量、被近战命中率、门 / 床等）在真实对局里的感觉；性能（get_BodySize 是热点，只多一次空判断 + 字典查找）。
- 泰坦化进行中存档、读档后的倍率（读档时会重建缓存，没单独测）。
- NPC 战士 / 自动施放是否会用撼地践踏、泰坦化（没写专门的 AI）。
- 真实战斗里泰坦化结束时的伤口缩放。

---

# 热修：战棺（VFE 海盗）适配不同体型（1.115.0 不升版本，2026-11-04）

Peter：「给战棺装备不同体型做一下适配」。由协调线程定下的范围：A + B + 顺手查 C + 覆盖泰坦战士。

## 改动
- **A 巨人可以装战棺**：`GiantKin.CanWear` 第一步把战棺（`VFEPirates.WarcasketDef`）当作例外放行，不看「巨人穿普通衣服」设置；`FireGiantWarcasket.cs` 原来挡火巨人的 5 个补丁（铸造厂拒绝理由、预约、工作、登记、最终安装）删掉，只留类型查找。画面照常随根节点按体型放大（火巨人 ×2 体型的画面倍率）。熔火血脉没有和战棺冲突的逻辑（火巨人没有烧衣服之类的代码，火焰免疫照常），所以没加额外处理。
- **B 身体类型**：自检里给五种身体类型（男 / 女 / 瘦 / 胖 / 壮）穿整套战棺截图对照：VEF 的 isUnifiedApparel 让外壳贴图在五种体型下**本来就一样大**，所以没有做尺寸缩放（第一版写了缩放补丁，截图看不需要就删了）。真正不同的是头的位置：侧面 x 偏移 男 0.04 / 瘦、胖 0.09 / 女、壮 0.10，头盔跟着头走就偏离外壳。现在装着战棺时头统一放到男性体型的位置（`WarcasketHeadOffset`，`BaseHeadOffsetAt` 后缀，排最后）。
- **C 额外部位**：装着战棺时六臂蛇人的额外手臂、蛇尾，双头巨人的第二个头（含头发、双盔另一半）不画（`XenoParts.Visible` 一行判断）；双头巨人主头不再往一侧让位，回到正中，和战棺头盔对齐。独眼巨人的独眼照原来的规则（全头盔遮住就不画）。
- **泰坦战士**：战棺在根节点下面，随泰坦之躯（√体型）和泰坦化（√(体型 × 1.25)）一起缩放；泰坦化开始 / 结束时缓存变化会标记渲染脏（原来就有）。另外修正：泰坦放大后武器的**握持位置**也按画面倍率外移（`WeaponTRS` 把相对身体中心的偏移乘同一倍率，`DrawEquipmentAndApparelExtras` 前缀记下身体中心）。

## Harmony 与快速返回
| 补丁 | 目标 | 快速返回 |
|---|---|---|
| WarcasketHeadOffset（后缀，Priority.Last） | PawnRenderer.BaseHeadOffsetAt | 没装 VFE 海盗（CasketType 为空）→ 返回；儿童 / 婴儿、身上没有战棺外壳 → 返回 |
| （直接改）GiantKin.CanWear | — | `Kind(def)` 按定义缓存，没装海盗时 0 |
| （直接改）XenoParts.Visible | — | `Encased` 第一步 CasketType 为空就返回 false |
| TitanWarriorWeaponOwner / WeaponMatrix（已有，加了身体中心） | 同前 | 同前 |

`WarcasketFit.Kind` 按 ThingDef 缓存（0 = 不是战棺、1 = 外壳、2 = 头盔 / 肩甲）；`Encased` 只遍历身上的衣服（几件）。

## 文件
- 新增：Source/WarcasketFit.cs、Source/WarcasketFitChecks.cs
- 改动：Source/GiantKin.cs（1 行）、Source/XenoParts.cs（1 行）、Source/FireGiantWarcasket.cs（删拦截补丁）、Source/FireGiantWarcasketChecks.cs（改成验证“能用”，铸造厂要先生成才有电力组件）、Source/Subclass_Titan.cs（武器握持位置）、使用说明.txt（1.115.0 热修一条）
- 备份：ArtSource/backup-warcasket-1104/

## 测试
- `Test.ps1 -Warcasket -Only WarcasketFit`：5 条全过，存读档通过。① 火巨人能用铸造厂（和普通人同样的理由“未通电”）、能登记、能穿所有普通人能穿的战棺件（战棺内衬 Bodysuit 普通人也穿不了，不算）② 五种身体类型装战棺时头在男性位置（东、西两侧），没装时不动 ③ 双头巨人、六臂蛇人装战棺额外部位隐藏、主头回正中，脱下恢复 ④ 泰坦 20 级装战棺根节点 ×√1.5，泰坦化 ×√1.875，结束复原 ⑤ 武器握持偏移按倍率外移
- `Test.ps1 -Only TitanWarrior`：8 条仍全过。
- 截图在 StandaloneMods/Validation/Lich-Warcasket-Only/WarcasketShots/（五种体型南 / 东、双头巨人、六臂蛇人、泰坦 20、泰坦化）。

## 没在游戏里验证
- 地图上（非肖像）的实际观感，尤其火巨人 ×2 画面倍率下的战棺、双头巨人 / 六臂蛇人走动时。
- 真走一遍铸造厂流程（巨人进去 → 设计 → 装上）和之后脱战棺（VFE 的“壳棺”状态）。
- 泰坦武器握持外移后，近战挥砍、远程瞄准姿势的观感。
- 其他战棺附加模组（毒素战棺、御械型无畏战棺）的贴图在各体型下是否也一致（检查只对照了基础战棺，但头部偏移修正对所有战棺外壳都生效）。
