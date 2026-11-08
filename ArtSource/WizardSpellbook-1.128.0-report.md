# 法师法术书 1.128.0 实现报告

依据：ArtSource/WizardSpellbook-1.128.0-spec.md（Peter 2026-11-08 批准）。版本号 1.128.0（开工时 About.xml 是 1.127.0，没有被占用）。

## 结果
- 编译：`Source\Build.ps1` 通过（主程序集 + HAR / VPE / 神兽血裔桥）。Build.ps1 编译整个 Source 文件夹，会带上别的线程当时的源码（今天另有线程改过 SecondLife.cs / SystemHost.cs / SecondLifeChecks.cs，一起编译通过）。
- 测试（只跑了这两组）：
  - `Test.ps1 -Only Spellbook`：**15 条全过**（14 条 + 存读档 1 条），最终版本连续 3 次全过。开发中修掉的问题：法术书初始化时机（代码 bug）；魔法阵把怪物往一个方向弹、那边被挡住时没弹出去（代码改成找阵外最近的空格）；测试里随机生成的敌人穿着护甲时 8 点火焰会被完全挡掉、油在部分地面上铺不上（测试改为脱掉护甲 / 不要求铺油）。
  - `Test.ps1 -Only Classes`：**76 条全过**（最终版本跑过；开发中共跑 6 次，有 2 次分别在“风暴先驱（冻原）光环伤害”“游侠·巨像杀手对受伤敌人”失败——都是别的子职业检查里“随机敌人没受伤”的偶发失败，本版没有碰这些代码，重跑通过）。
- 没跑完整测试（按要求）。没有 git 提交 / 推送。

## 新文件
- `Defs/WizardSpellbook.xml`：38 个 AbilityDef、2 个伤害（LP_WS_Poison 毒素、LP_WS_Psychic 心灵）、约 25 个状态、法术区域 LP_WS_SpellZone、3 张残页 LP_WS_SpellPage1/2/3、3 个抄录配方 LP_WS_CopySpell1/2/3、工作 LP_WS_RitualCast / LP_WS_Study。
- `Patches/WizardSpellbook_Traders.xml`：奇物商人（Caravan_Outlander_Exotic、Orbital_Exotic）卖残页；魔力湖泊商队靠残页继承的 LP_Scroll 交易标签出售。
- `Source/WizardSpellbook.cs`：WizardSpellExt（环位 / 学派 / 仪式 / 专注 / 升环）、SpellbookData（存在 ClassData.book）、Spellbook（法术表、自选、戏法、备法、升环、专注、仪式、研读、抄录、残页）、CompSpellPage、RecipeWorker_CopySpell、两个 JobDriver、窗口 Dialog_Spellbook / Dialog_Upcast / Dialog_SpellChoice、Harmony（备法按钮、专注受伤、残页掉落、抄录只给法师、魔宠射程）、GameComponent_Spellbook（换存档清静态登记）。
- `Source/WizardSpells.cs`：38 个法术的效果（effect 以 WS_ 开头）、Hediff_WS、魔宠 Familiars、秘法锁（MapComponent_WizardLocks + 门的伤害 / 按钮钩子）、SpellZone（油腻 / 蛛网 / 臭云 / 警报 / 魔法阵）、伤害前缀（闪现、防护能量、魔法阵、隐形攻击现形）。
- `Source/SpellbookChecks.cs`：`-Only Spellbook`。
- `ArtSource/WizardSpellbook-1.128.0-icons.png`：38 个法术的图标预览。

## 改动的旧文件（备份在 ArtSource/backup-1.127.0-wizard）
- `Classes.cs`：ClassData 加 `book`（存档键 spellbook）；ClassOf / CastClassOf / StatValue / Sync / Tick250 / CannotCast / FreeCast 各加一个 Spellbook 挂钩。
- `ClassAbilities.cs`：没准备的法术不出按钮；施放时 BeginCast/EndCast（升环）与 AfterCast（隐形现形）；Power 乘升环；Invalid / Apply 接 WizardSpells。
- `ClassLegendary.cs`：Cost 加升环法力。
- `ClassSubclass3.cs`：塑能学派的 EvoEffects 加 6 个新塑能法术（法术塑形、强效塑能、强力戏法等都按这个集合判断）。
- `ClassMartial2.cs`：ClassWoodstride 加飞行术的 Fliers。
- `ClassAutoCast.cs`：按钮右键打开升环窗口（Dialog，不用浮动菜单）。
- `About/About.xml`（版本 1.128.0 + 描述）、`使用说明.txt`（内容一览 + 更新日志）。

## 机制（对照规格）
1. 法术书：存在法师职业数据里；12 个原有能力照旧自动学会；每个法师等级自选 2 个已能施放环位的法术（老存档 = 法师等级 × 2 待选）；环位 → 等级 / 法力：戏法 1/0、一环 1/1、二环 3/1、三环 5/2。新等级或老存档有待选时发一条消息。
2. 抄录：卷轴书桌三个配方（每环一个），残页或对应卷轴 + 每环 50 白银、每环 2 小时（工作量 5000/10000/15000，速度看研究速度）；成功率 60% + 智力调整值 × 5%（≤95%），书士会退还一半白银、+15%；失败只扣材料；已经会 / 环位不够时材料退回。只有法师能接这个账单（Bill.PawnAllowedToStartAnew）。残页：敌方人形施法者（ClassSystem.Spellcaster）死亡 15% 掉一张（按冒险者等级决定最高环），奇物商人与魔力湖泊出售。卷轴对应：真知 → 侦测魔法，石化 → 定身类人，恐惧凝视 → 恐惧术，防护邪恶 → 魔法阵，死灵冲击 → 吸血之触。
3. 备法：上限 = 法师等级 + 智力调整值（≥1）；按钮“备法（已备/上限）”；戏法、仪式不占名额；只有已准备的法术显示按钮且能施放（CanCast 拒绝，所以自动施放也跳过）；关窗时如果改过备法就锁定，睡醒（Tick250 检测睡→醒）或“去研读（1 小时）”后解锁；老存档把已学会的全部准备上（超上限保留）；之后新学会的有空位自动准备。只对玩家的人生效，NPC 不建法术书。
4. 戏法：1 级 3 个、4 级 / 10 级 +1，魔法飞弹算一个。
5. 仪式：窗口里“仪式施法”，非征召、原地引导 2500 刻、不耗法力（要目标的会先进入选目标）；准备了也能用按钮花法力立即施放。
6. 升环：**没有简化**——右键按钮或窗口里的“升环”按钮打开窗口选环位，之后一直按这个环位施放；每环 +1 法力；power 型 +25% 效果、target 型 +1 目标、radius 型半径 +1；上限是自己能放的最高环。
7. 专注：缓慢术、吸血之触；新专注结束旧的；受到 ≥10 点伤害时维持成功率 70% + 体质调整值 × 5%（Pawn.PostApplyDamage 后缀）。
8. 燃烧之手、魅惑人类可以写进书：没有术士等级时按法师施放（扣法师法力、用奥术强度），有术士等级的人照旧算术士。
9. 塑能学派覆盖火焰箭、冷冻射线、电爆之握、雷鸣波、灼热射线、粉碎音波。

## 新 defName 与复用 / 新写的效果
| 环 | defName | 法术 | 效果 |
|---|---|---|---|
| 戏法 | LP_WS_FireBolt | 火焰箭 | 新写 WS_FireBolt |
| 戏法 | LP_WS_RayOfFrost | 冷冻射线 | 新写（复用 LP_Frost 伤害） |
| 戏法 | LP_WS_ShockingGrasp | 电爆之握 | 新写（LP_Lightning / EMP） |
| 戏法 | LP_WS_PoisonSpray | 毒雾喷溅 | 新写 |
| 戏法 | LP_WS_Mending | 修复术 | 新写 |
| 戏法 | LP_WS_Prestidigitation | 奇术手法 | 新写 |
| 戏法 | LP_WS_Light | 光亮术 | 复用光明领域光球 Subclass3.LightOrb（寿命改 12 小时） |
| 戏法 | LP_WS_MageHand | 法师之手 | 新写 |
| 1 | LP_WS_FogCloud | 云雾术 | 新写（原版 BlindSmoke 烟雾） |
| 1 | LP_WS_Grease | 油腻术 | 新写区域 + 原版 Filth_Fuel |
| 1 | LP_WS_Thunderwave | 雷鸣波 | 新写（复用 DNDPush 击退） |
| 1 | LP_WS_FalseLife | 虚假生命 | **复用 PowerBuff + LP_ShieldSpell** |
| 1 | LP_WS_FindFamiliar | 寻获魔宠 | 新写 |
| 1 | LP_WS_Alarm | 警报术 | 新写区域 |
| 1 | LP_WS_ComprehendLanguages | 通晓语言 | **复用 SelfBuff** |
| 1 | LP_WS_DetectMagic | 侦测魔法 | 新写 |
| 2 | LP_WS_ScorchingRay | 灼热射线 | 新写 |
| 2 | LP_WS_Shatter | 粉碎音波 | 新写 |
| 2 | LP_WS_Web | 蛛网术 | 新写区域 |
| 2 | LP_WS_HoldPerson | 定身类人 | 新写（成功率按 Subclass2.Chance） |
| 2 | LP_WS_Invisibility | 隐形术 | **复用 TargetBuff** + 新状态 LP_WS_Invisible |
| 2 | LP_WS_Darkness | 黑暗术 | **复用邪术师 Warlock.Darkness** |
| 2 | LP_WS_MindSpike | 心灵尖刺 | 新写 |
| 2 | LP_WS_EnhanceAbility | 增强属性 | 新写（窗口四选一） |
| 2 | LP_WS_ArcaneLock | 秘法锁 | 新写 |
| 2 | LP_WS_Knock | 敲击术 | 新写 |
| 3 | LP_WS_DispelMagic | 解除魔法 | 新写 |
| 3 | LP_WS_Fly | 飞行术 | **复用 TargetBuff** + ClassWoodstride 无视地形 |
| 3 | LP_WS_Fear | 恐惧术 | 新写（复用 ClassLegendary.Cone 扇形） |
| 3 | LP_WS_Slow | 缓慢术 | 新写（专注） |
| 3 | LP_WS_StinkingCloud | 臭云术 | 新写区域 |
| 3 | LP_WS_VampiricTouch | 吸血之触 | 新写（专注） |
| 3 | LP_WS_AnimateDead | 活化死尸 | 复用巫妖骷髅仆役 Constructs.Create("Skeleton") |
| 3 | LP_WS_Blink | 闪现术 | **复用 SelfBuff** + 伤害前缀 |
| 3 | LP_WS_ProtectionFromEnergy | 防护能量 | 新写（窗口五选一） |
| 3 | LP_WS_MagicCircle | 魔法阵 | 新写区域（窗口四选一） |
| 3 | LP_WS_SpeakWithDead | 亡者交谈 | 新写（窗口二选一） |
| 3 | LP_WS_Sending | 送讯术 | 新写（原版 Dialog_Negotiation） |

仪式 8 个：寻获魔宠、警报术、通晓语言、侦测魔法、秘法锁、魔法阵、亡者交谈、送讯术。可升环 5 个：云雾术（半径）、雷鸣波（+25%）、灼热射线（+1 道）、定身类人（+1 目标）、活化死尸（+1 具）。
自动施放：修复、奇术手法、光亮、法师之手、云雾、寻获魔宠、警报、侦测魔法、增强属性、秘法锁、敲击、解除魔法、防护能量、魔法阵、亡者交谈、送讯、活化死尸不进自动施放（要开窗口 / 选物品 / 仪式类）。

## 简化与偏差（请 Peter 过目）
- 寻获魔宠：原版没有猫头鹰 / 渡鸦，形态是黑猫、老鼠；DND 野兽第三批（现在在 Shelved 里）启用时多出翼猫、伪龙。魔宠会尽量完成驯服与服从训练并认法师为主人、建立羁绊；法术射程 +3 用 Verb.EffectiveRange 后缀实现。
- 云雾术：用原版 BlindSmoke 烟雾，散去时间由原版气体决定（大约半分钟，不是精确 30 秒）。
- 黑暗术：直接用邪术师的黑暗术——施放时范围内的敌人得到 30 秒“黑暗”状态（远程精度大降），不是持续存在、对“射入 / 穿过”也生效的区域。
- 通晓语言：“招募速度 +20%”做成谈判能力 ×1.2（同时略微影响交易）。
- 侦测魔法：魔法物品用信件列出并可点击定位，不做地图高亮；“魔法物品”= DND 物品里交易标签含 Magic / Wondrous / Relic / Scroll / Arcane / Legend 的，加上任何传奇品质物品。
- 定身类人、恐惧术：除了成功率，目标的心智抗性也可以抵抗（和睡眠术、魅惑一致）。
- 粉碎音波：“石像类”按非血肉生物判断。
- 隐形术：没用 Royalty 的心灵隐形 Hediff（避免依赖 DLC），用原版隐形组件做了自己的状态，攻击或施法现形。
- 解除魔法清单：敌人——DND（LP_ 开头）的限时增益（护盾、镜影、急速、祝福等职业 / 法术 buff）和任何隐形；友军——DND 的限时有害状态（睡眠、缓慢、冷冻、黑暗、缠绕、咒术、动摇……），以及石化（LP_Petrifying、LP_NabassuPetrify）、咒术 LP_WL_Hexed、神力支配 LP_DivineDominated、龙之魅惑 LP_DragonCharmed，和游荡（魅惑）/ 狂暴（支配）/ 惊逃精神状态。
- 飞行术：只免地形 / 雪 / 植物减速和移动 ×1.6，不能飞过墙。
- 吸血之触：“期间出现免费按钮”做成同一个按钮在专注期间不耗法力、冷却 2 秒。
- 魔法阵：“进不来”做成每半秒把阵里那类敌对生物弹出阵外（并眩晕 0.5 秒）；“元素”= 外来者元素位面或 defName 含 Elemental，“妖精”= 外来者妖精荒野位面。
- 亡者交谈：“揭示据点”= 一封信指出该派系离这里最近的据点（原版世界地图上据点本来就看得见）；研究点用 ResearchManager.AddProgress 加到当前研究，没有研究时白白散去。
- 敲击术：古代容器 / 休眠舱（Building_Casket）打开，能被黑客入侵的东西立刻破解，别人的门直接震碎；自己的门不需要。
- 警报术叫醒施法者 = 打断睡觉的工作。
- 抄录：三个配方按环位分开（配方的材料数量固定，所以没法一个配方按环收费）；书士会的“花费减半”是完成时退还一半白银。
- 研读：从窗口点“去研读（1 小时）”，走到最近的研究台或卷轴书桌（不是自动工作）。

## 图标（全部未画，暂用旧图标）
38 个都复用了现有图标，预览：ArtSource/WizardSpellbook-1.128.0-icons.png。对应关系：火焰箭 Subclass3/EvoPotent、冷冻射线 Sorcerer/IceStorm、电爆之握 Subclass2/ArcaneJolt、毒雾喷溅 Subclass3/RotTouch、修复术 Artificer/QuickRepair、奇术手法 Bard/WordsOfCreation、光亮术 Subclass3/LightOrb、法师之手 Subclass/MageHand、云雾术 Subclass3/Cloak、油腻术 Subclass4/AlchExperimental、雷鸣波 Subclass2/ThunderBlade、虚假生命 Subclass2/ProtectiveSpirit、寻获魔宠 Ranger/BeastBond、警报术 Barbarian/DangerSense、通晓语言 Bard/UniversalSpeech、侦测魔法 Subclass/TreasureSense、灼热射线 Subclass3/StarBolt、粉碎音波 Legendary/EarthShatter、蛛网术 Druid/Entangle、定身类人 Subclass2/FleshPrison、隐形术 Subclass/ATInvisibility、黑暗术 Warlock/Darkness、心灵尖刺 Subclass2/PsychicSpike、增强属性 Subclass2/BattleReady、秘法锁 Subclass4/FortFortify、敲击术 Subclass/SecondStoryWork、解除魔法 Paladin/DispelEvil、飞行术 Sorcerer/DragonWings、恐惧术 Oathbreaker/DreadfulAspect、缓慢术 Bard/Confusion、臭云术 Subclass3/SporeHalo、吸血之触 Subclass2/Siphon、活化死尸 Oathbreaker/ControlUndead、闪现术 Subclass3/ShadowStep、防护能量 Subclass2/AbsorbElements、魔法阵 Subclass2/GuardianField、亡者交谈 Warlock/DissonantWhispers、送讯术 Subclass2/EmissaryOfPeace。备法按钮用 LP/Classes/Manual，残页用卷轴图 Items/Scrolls/RemoveCurse。

## 测试覆盖（-Only Spellbook）
1 定义与计数；2 自选 / 戏法名额与环位门槛；3 备法上限、隐藏、满额、睡醒 / 研读解锁；4 升环（上限、法力、×1.5、实际施放扣法力）；5 专注（维持、打断、替换、体质成功率）；6 抄录（成功率、失败、成功、重复、环位不足、退料、卷轴对应、残页掉落率）；7 借用术士法术；8 攻击法术；9 闪现 / 防护能量 / 施法现形 / 虚假生命；10 区域；11 魔宠与射程；12 修复 / 法师之手 / 秘法锁 / 敲击 / 解除魔法；13 活化死尸；14 仪式与送讯；15 存读档。
没有自动测试的：几个选择窗口和法术书窗口的界面（游戏里打开看一下最好）、仪式 / 研读工作的完整 1 小时流程（只测了完成时的结算）。
