# 1.29.0 DND 枪械与法杖 — 实装报告

规格：`ArtSource/Guns-1.29.0-spec.md`（含 Peter 的两条补充：法杖心灵熵、黑曜石用现成的）。
备份：`ArtSource/backup-1.28.0/`（About.xml、使用说明.txt、LoadFolders.xml、Source 下 Checks.cs / Build.ps1 / Test.ps1 / Core.cs / ItemsIsekai.cs / ItemChecks.cs / TwilightDeities.cs，Bridges/VPE 下 BridgeChecks.cs / VPEBridge.cs / Manual.cs / Things.xml，Bridges/Isekai/Patches/IsekaiTraders.xml）。没有提交 git。

## 一、做了什么

### 新文件
| 文件 | 内容 |
|---|---|
| `Defs/Guns.xml` | 研究页签“熔火锻造”、研究“符文火器”“奥术法杖”、13 件武器与投射物、`LP_Frost` 寒霜伤害、状态“符文灼印”`LP_RuneBrand`、“寒霜缠身”`LP_FrostStaffChill` |
| `Patches/Guns_Traders.xml` | 矮人铁匠商队（D、F 组、黑曜石）、黑网（D 组、法杖、约 2% 噩耗/天火降世） |
| `Source/Guns.cs` | 魔力系数（`Arcana`）、武器组件 `CompArcaneWeapon`、投射物 `LP_ArcaneBullet` / `LP_ArcaneShell`、射击动词 `Verb_ArcaneShoot`、驱散锁 `GunMap`、目标侧倍率、山岳之怒装备限制、AI 不捡法杖、制作品质、天火同步、敌人装备、黑网稀有进货、Isekai 宝箱掉落、两个设置项 |
| `Source/GunChecks.cs` | 新检查组 `Guns`（含存读档）与 `CheckGunsIsekai` |
| `Textures/LP/Weapons/*.png` | 13 张 64×64 武器图 |
| `ArtSource/RaceArt/draw_gun_art.py` | 武器贴图脚本；预览 `ArtSource/Guns-1.29.0-preview.png` |
| `Bridges/VPE/Defs/ClassArtificer.xml` | 器械师路径、`LP_ArtificePower`、状态“过载射击”“魔力充能” |
| `Bridges/VPE/Defs/ClassAbilities_Artificer.xml` | 9 个能力 |
| `Bridges/VPE/Defs/ClassThings_Artificer.xml` | 符文炮台、炮台枪、炮台弹 |
| `Bridges/VPE/Source/Artificer.cs` | 器械师逻辑（制作权限、品质/共鸣登记、维修、过载、充能、炮台） |
| `Bridges/VPE/Textures/LP/Classes/Artificer/` | 9 个图标 + `Background.png`（齿轮与符文锤） |
| `ArtSource/RaceArt/draw_artificer_art.py` | 器械师美术脚本（直接导入 draw_class_art.py 的画法与配色） |

### 改动的已有文件
- `Source/Core.cs`：设置读写加 `ExposeGuns()`，设置页右栏末尾加 `GunSettings.Draw`。
- `Source/ItemsIsekai.cs`：宝箱挂钩里加 1.29.0 武器掉落（独立随机数状态，不影响神灯概率）。
- `Source/ItemChecks.cs`：宝箱检查的期望件数加上武器掉落；`-Isekai` 档调用 `CheckGunsIsekai`。
- `Source/Checks.cs`：完整检查加 `CheckGuns` / `PrepareGunsSave` / `CheckGunsAfterLoad`，PASS 文本补充。
- `Source/TwilightDeities.cs`：**顺手修的旧问题**——维拉基斯附体者倒下化灰时，若倒在浅水里，原代码在水上放不了灰（灰烬污物不能放水面），现在落到旁边能放的格子。完整测试第一次就是因为随机地图把它放到了水里而失败（我的改动让红袍法师生成武器时多用了随机数，碰巧暴露）。
- `Bridges/VPE/Source/BridgeChecks.cs`：路径数 3→4、能力 27→36、神经成型器 27→36、属性列表加 `LP_ArtificePower`，新增 `ArtificerChecks`。
- `Bridges/VPE/Source/Manual.cs`、`Bridges/VPE/Defs/Things.xml`：冒险者手册文本改成“四条路”，含手工。
- `About/About.xml`：1.29.0 + 更新说明；`使用说明.txt`：标题、设置说明、内容一览、第二部分【1.29.0 DND 枪械与法杖】、更新日志。
- `Build.ps1`、`Test.ps1`、`LoadFolders.xml` 不需要改（新 .cs 自动参与编译；-Only/-VPE/-Isekai 现有参数够用）。

## 二、数值

### 魔力系数（`Source/Guns.cs` 的 `Arcana`）
| 组 | 公式 | 敏感度 0 / 50% / 100% / 200% / 300% |
|---|---|---|
| G 法杖 | 敏感度，夹在 0.2～2.5；0 不能用 | 不能用 / 0.5 / 1 / 2 / 2.5 |
| F 矮人枪械 | 0.5 + 0.5 × 敏感度，上限 2 | 0.5 / 0.75 / 1 / 1.5 / 2 |
| D、E | 恒为 1 | 1 |

- 保底：矮人用 F 组 ≥100%；敌方红袍法师用 G 组 ≥100%；VPE 魔力共鸣 F、G ≥150%（取最大）。
- 魔力充能 +0.5（只 F、G）；法杖持有者没有心灵链接时 ×0.7。
- 心灵熵：有心灵链接的法杖持有者每次施放（连发只算第一发）+2（设置“法杖每发心灵熵”0～20，默认 2）；熵满时按钮灰掉、AI 不用。F 组不加熵。
- 目标侧：穿寒铁材质服装或心智抗性 >0 → F、G 伤害 ×0.75；符文灼印 → F、G ×1.15；圣银手枪对死灵造物/巫妖/魔鬼 ×1.5（复用 1.22.0 圣水的 `ItemThrowables.HolyTarget`）。
- 驱散粉尘：落点 5×5 内 10 秒（600 刻）法杖不能施放（不存档的短效锁）。
- 武器信息页：“魔力系数 ×1.35（心灵敏感度 135%）”，没有心灵链接/充能时在后面注明。
- 法杖敏感度 0：攻击按钮灰掉并显示“法杖毫无回应（心灵敏感度为 0）”（优先于原版“未征召”提示），`Verb.Available` 为假，AI 不会捡（`JobGiver_PickUpOpportunisticWeapon.ShouldEquipWeapon`）。

### 武器（伤害为系数 1.0 时）
| 武器 | 组 | 伤害 | 穿甲 | 射程 | 瞄准 | 冷却 | 材料 / 特效 |
|---|---|---|---|---|---|---|---|
| 圣银手枪 | D | 14 | 20% | 24 | 0.6 | 1.6 | 白银60 钢30 零部件2；亡灵/魔鬼 +50% |
| 熔岩穿甲枪 | D | 22 火焰 | 45% | 30 | 1.4 | 2.6 | 钢70 零部件3 熔岩结晶6 黑曜石10；25% 点燃，巨魔阻断再生 |
| 雷鸣铳 | D | 16 EMP | 15% | 26 | 1.0 | 2.0 | 钢60 零部件4；半径1.5 EMP，人形眩晕1秒 |
| 噩耗 | E | 45 | 70% | 48 | 3.5 | 5 | 不能制作；头/躯干 15% 贯穿身后 8 格内下一人 |
| 天火降世 | E | 30 火焰 | 45% | 32.9 | 1.19 | 1.89 | 不能制作；= 电荷长枪数值、瞄准冷却 -30%（启动时同步）；15% 点燃 |
| 符文火铳 | F | 18 | 30% | 32 | 1.6 | 2.4 | 钢60 木材20 秘银8 |
| 符文手铳 | F | 13 | 20% | 22 | 0.4* | 1.5 | 钢35 秘银4 |
| 雷锤手炮 | F | 14 爆炸 r1.9 | 15%* | 24* | 1.5* | 3.2* | 钢80 秘银10 零部件2；眩晕1秒，不点火不伤己方 |
| 符文连珠铳 | F | 3×11 | 18% | 28 | 1.2* | 2.6 | 钢90 秘银12 零部件3 |
| 山岳之怒 | F | 34 | 60% | 42 | 2.8 | 4 | 钢140 精金15 秘银15；只限矮人；符文灼印3秒 |
| 魔法飞弹杖 | G | 3×6 | 10%* | 26 | 1.0* | 2.2 | 木材40 白银20；必中 |
| 火焰法杖 | G | 16 火焰 | 20%* | 28 | 1.2* | 2.4 | 木材40 熔岩结晶6；30% 点燃 |
| 寒霜法杖 | G | 12 寒霜 | 15%* | 26 | 1.0* | 2.0 | 木材30 寒铁10 秘银5；4秒移速 -40% |

带 * 的是规格没写、我补的值。法杖都能当钝器（9）。精度、重量、工作量按同类原版枪械估的。噩耗、天火降世市价 6000。

### 研究与制作
- 页签“熔火锻造”`LP_ForgeTab`；符文火器 `LP_RunicFirearms` 1200 点（前置 DND炼金术 + 锻造）；奥术法杖 `LP_ArcaneStaves` 1000 点（前置 DND炼金术）。
- D、F 在燃料/电力锻造台（手工 6，山岳之怒手工 10）；G 在炼金桌（智识 6，速度用炼金速度）。
- 品质：矮人做 F 组 +1（最多大师）；VPE 大师工艺再 +1（叠加，最多传说）。挂在 `GenRecipe.PostProcessProduct`。

### 黑曜石
- 已加载内容里找到：**Odyssey DLC（ludeon.rimworld.odyssey）的 `Obsidian`**（`Data/Odyssey/Defs/ThingDefs_Items/Items_Resource_Stuff.xml`）。熔岩穿甲枪的材料和矮人商队直接引用它，都写了 `MayRequire="Ludeon.RimWorld.Odyssey"`：没装 Odyssey 时配方不要黑曜石、商队不卖。没有自建物品，也就没画黑曜石贴图。
- 创意工坊里还有 Alpha Biomes 的 `AB_Obsidianstone` 等和一个旧 TR 模组的 `Obsidian`，都不是已加载的通用材料，没用。

### 获取与敌人（设置“敌人携带魔法火器”默认开）
- 同盟矮人护卫 `LP_FC_DwarfGuard`：15% 符文火铳 + 15% 符文手铳；已是投弹手的不换。
- 塞伊红袍法师 `LP_FH_RedWizard`：70% 火焰法杖 / 30% 寒霜法杖（替换原来的近战武器）。按小人编号播种，不动全局随机数。
- 矮人铁匠商队：D 组 1～2 种、F 组 1～3 种、黑曜石 20～60。黑网：D 组 1～2 种、法杖 1～2 种（贵价），另 2% 进一件噩耗或天火降世。
- Isekai 地下城宝箱（沿用 1.22.0 的 `TreasureChest.Award` 挂钩）：2 级以上 25% 掉一件 D/F/G，4 级以上再 8% 掉噩耗或天火降世，品质按原版奖励品质。宝箱掉落不受“敌人携带”开关影响。

### VPE 器械师（Bridges/VPE）
- `LP_Path_Artificer`（`LP_ClassPathDef`，页签 DND职业，手工 ≥6），器械强度 `LP_ArtificePower` = 0.5 + 手工/20（`StatPart_ClassSkill`），手工经验 → VPE 经验沿用 1.27.0 的规则（100:1，每天 50）。
| 环 | 能力 | 说明 |
|---|---|---|
| 1 (6) | 奥术工坊（被动） | 可做三根法杖、符文手铳、符文火铳 |
| 1 (6) | 快速维修 | 友方主武器与每件服装各 +25% 耐久，冷却 1 天 |
| 2 (9) | 符文铭刻（被动） | 可做 D 组、雷锤手炮、符文连珠铳 |
| 2 (9) | 过载射击 | 10 秒内下一次远程命中 ×(1+器械强度)，武器 -5% 耐久 |
| 3 (12) | 魔力充能 | 友方 30 秒魔力系数 +0.5（只 F、G） |
| 3 (12) | 大师工艺（被动） | 本页武器品质 +1，与矮人叠加，最高传说 |
| 4 (15) | 山岳锻造（被动） | 可做山岳之怒 |
| 4 (15) | 符文炮台 | 目标格 60 秒炮台：符文火铳数值 × 器械强度，冷却 2 天 |
| 5 (18) | 魔力共鸣（被动） | 自己用 F、G 敏感度不足 150% 按 150% 算 |
- 被动能力没有按钮（`ShowGizmoOnPawn` 为假），学会即生效。制作权限挂在 `RecipeDef.PawnSatisfiesSkillRequirements`：有 VPE 时研究之外还要会对应被动；没有 VPE 时只看研究。

## 三、Isekai 结论
读的是 `workshop/294100/3657580708`（ISEKAI RPG LEVELING，自带源码）：
- 武器精炼不是 Harmony 挂在 Bullet.Impact/DamageInfo 上，而是 **StatPart**：`StatPart_RefineRangedDamage` 打补丁加到原版属性 `RangedWeapon_DamageMultiplier` 上（每级 +5%，+10 = ×1.5）；`CompForgeEnhancement` 由 `ForgeCompInjector` 自动注入所有武器（我们的 13 件也有）。原版 `Projectile.DamageAmount` 读武器的这个属性。
- 另有 `Patch_ProjectileDamage_CreatureScaling`：Harmony 后缀挂 `Projectile.DamageAmount` getter，按发射者敏捷加人形远程伤害（向上取整）。它的注释写明“重写了 getter 的投射物会跳过”。
- 我们的做法：`LP_ArcaneBullet` / `LP_ArcaneShell` 重写 `DamageAmount`，但**先调用 `base.DamageAmount`**（Harmony 补丁改的是基类方法本体，base 调用照样经过后缀），再乘魔力系数。所以精炼、敏捷加成都在系数之前生效，不会被跳过。
- 实测（-Isekai 档）：同一小人同一火焰法杖，原伤害 16 → Isekai 敏捷后 17；精炼 +10 后 25（×1.5）；魔力系数 ×0.72（无心灵链接）→ 最终 12 → 18，期望 25 × 0.72 ≈ 18.5（取整），断言“精炼后伤害 = Isekai 倍率 × 魔力系数”（±1.5 取整误差）通过。

## 四、测试结果（Peter 要求少跑，完整套件只在最后跑通）
| 命令 | 结果 |
|---|---|
| `Test.ps1 -Only Guns`（迭代 12 次） | 最终 **PASS: 256 checks (only Guns); save/reload** |
| `Test.ps1 -VPE` | **PASS: 230 checks**（器械师相关 32 条） |
| `Test.ps1 -Isekai` | **PASS: 615 checks**（含法杖 + 精炼叠加、宝箱件数） |
| `Test.ps1`（完整 Core） | **PASS: 3867 checks**，日志无红字。完整套件跑了 3 次：第 1 次挂在 1.28.0 维拉基斯化灰（见上，已修游戏逻辑），第 2 次挂在我自己的存档后检查没设 map，第 3 次通过 |

迭代中修掉的问题：EMP 伤害读不到穿甲（改读配置值）；E 组没有材料却可熔炼（设为不可熔炼）；**`LP_FrostSlow` 与 1.25.0 冬狼冰咬重名**（会覆盖冬狼的减速，改名 `LP_FrostStaffChill`）；`ThingComp.SpecialDisplayStats` 默认返回 null；原版未征召提示盖住法杖提示；`Pawn.HasPsylink` 按刻缓存（改看心灵链接等级）。

`Guns` 检查组覆盖：13 件定义/贴图/动词/投射物/数值，配方与研究对应（E 组不能做），黑曜石来源与配方，魔力系数公式（敏感度 0/0.5/1/2/3），法杖敏感度 0 拒绝施放（按钮灰、AI 不捡），无心灵链接 ×0.7、心灵熵（首发 +2、连发后续不加、熵满拒绝、设置 0 免费），F 组不加熵，驱散锁 10 秒，实际投射物伤害 = 原伤害 × 系数，矮人保底与品质，山岳之怒非矮人拒绝，噩耗头/躯干判定与身后目标，雷鸣铳 EMP 眩晕范围，雷锤不伤己方与系数，寒铁/心智抗性 -25%、符文灼印 +15%、圣银对亡灵 ×1.5（含真实 `PreApplyDamage`），天火与电荷长枪同步，敌人装备（含真实生成红袍与关掉设置），商人，宝箱分级，存读档。

## 五、没做的 / 和规格不一样的
- **黑曜石贴图**：没有自建黑曜石（用 Odyssey 的），所以没画。
- **法杖心灵熵**：按 Peter 的补充做（有心灵链接加熵，没有 ×0.7），替代了原说明里的“法杖没有心灵热量代价”。
- **噩耗“命中头部或躯干”**：噩耗弹自己选命中部位（原版随机部位规则），头/颈及其子部位或躯干算；被贯穿的第二人吃同样伤害但不再判定贯穿。原版战斗日志里这一枪没有条目。
- **雷鸣铳**：子弹本身是原版 EMP 伤害（对人形无伤害），命中处半径 1.5 的 EMP 爆发 + 人形眩晕 1 秒，按规格做。
- **双手持握**：RimWorld 没有双手武器机制，法杖只在描述里写“双手长杖”。
- **符文炮台**用原版迷你炮台的底座贴图加蓝色调，炮管用符文火铳贴图；没有单独画炮台。
- 规格没给的瞄准/冷却/射程（见表中 *）是我定的，可以再调。
- 游戏内实机手动试玩没有做，只有自动测试。
