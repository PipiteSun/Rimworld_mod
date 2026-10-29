# DND边缘大陆 1.45.0 魔法物品 · 实现报告

2026-10-26 · 依据 `ArtSource/MagicItems-1.45-spec.md` 第一、二节（以及《DND物品构思二》第七节、《魔法饰品构思》）。设备原版本 1.44.0，备份在 `ArtSource/backup-1.44.0`（Defs、Source、About、Patches、Bridges、使用说明.txt、LoadFolders.xml）。

## 做了什么

**规则**
- 戒指：`LP_RingBase` 加 `AccessorySlot{slot=Ring,max=2}`。两枚不同的戒指可同戴，同一种不行，戴第三枚顶掉最早的（`AccessorySlotLimit`）。旧说明“一次只能戴一枚戒指”全部改成“最多戴两枚不同的戒指”。
- 奇物层：`LP_Accessory` 标签“饰品”→“奇物”（defName 不变），描述里的“饰品栏”一并改名。新物品按位置分槽（戒指/手/护腕/头顶/眼/颈/胸/印记），两件都标了位置时按槽判冲突，没标位置的旧物品照原版规则；伊奥恩石算自己的“Ioun”槽（可戴两颗同样的）。
- 精金漏洞：`ItemMaterials.ArmorFactor` 只认贴身/中层/外层/头盔层（新增 `ArmorLayer`）。
- 诅咒物品：`CompCursed`（伪装名与描述、未鉴定标签、鉴定/解咒状态、存档）；拦截 `Pawn_ApparelTracker.TryDrop/Remove/Wear`、`Pawn_EquipmentTracker.TryDropEquipment/TryTransferEquipmentToContainer/Remove`、`EquipmentUtility.CanEquip`；死亡、物品被毁、截肢（`Notify_LostBodyPart` 时放行）照常脱落。识破卷轴现在顺便鉴定整张地图上的诅咒品；“鉴定”按钮 → 研究者带到研究台研究 2 小时（`JobDriver_IdentifyItem`，需要附魔学）。新卷轴“解除诅咒卷轴” `LP_ScrollRemoveCurse`（炼金桌、教会牧师、巫妖抄写 `LP_ScribeRemoveCurse`）。设置“诅咒物品”默认开。黑网 `StockGenerator_LPMagicGoods`：好货里 30% 换成未鉴定诅咒品。
- 不加同调。

**物品（57 个新 ThingDef，含活动杖、碎念珠、誓约戒匣、石魔像胚体与卷轴）**
- 魔化兵器 7：火焰舌（燃火开关能力）、霜寒之刃、锐锋剑、龙杀手、守护者之剑（守御开关能力）、闪电标枪、复仇之剑（诅咒）。命中效果在 `Verb_MeleeAttackDamage.ApplyMeleeDamageToTarget` 后处理；龙杀手倍率并进 `ItemMaterials.WeaponFactor`；偏折失效用 `LP_DeflectSuppressed`（`ForceDragon.Avoid` 认它）。器械师门槛：前四件“符文铭刻”、闪电标枪“奥术工坊”（`Bridges/VPE/Source/Artificer.cs`）。
- 穿戴奇物 12、魔法饰品 12、圣物与印记 13、神奇器物 8：全部照规格表。条件效果用 `Hediff_MagicItem`（体型、持弓、无躯干护甲、战嚎人数、巫妖豁免五种条件）；次数类用 `CompMagicCharges`（每日次数、珠子、护盾值、抵抗次数）。
- 魔像手册 → 石魔像：巫妖读完得 `LP_GolemLore`，死灵仪式台 `LP_MakeStoneGolem`（300 石块）解锁；石魔像是第四种死灵造物 kind="StoneGolem"（统御 4、维护 0.5/天、体型 2、近战 ×4、灰岩外观基因、无血肉储备），驱散粉尘对它造成 20 伤害。复用血肉巨像那一套（`Gameplay.cs`、`ConstructDesign.cs`、`ConstructHuman.cs`、`Appearance.cs` 各加一个分支）。
- 魂石：死亡时收灵魂；`Souls.Unfit` 拒绝汲取；死灵仪式台新增“汲取魂石里的灵魂” `LP_OfferSpiritStone`；复生卷轴对魂石里的灵魂不限死亡时间和腐烂程度，成功后魂石碎掉。
- 圣甲虫在 `Hediff_Lich.DrainPulse` 和死亡律令里各挡一次；自由行动戒指让力场囚笼落空。

**研究**（`LP_FireGiantTech` 页签，坐标全部空着，没有顺移）：附魔学 (2,0.7)、魔化兵器 (3,0)、奇物工艺 (3,0.7)、圣物祝圣 (3,1.4)、神奇器物 (4,0.7)、魔像学 (5,0.7)。所有新配方只挂这些研究。

**联动**
- Isekai：三级以上宝箱额外 15% 一件魔法物品（锐锋剑、复苏核心、流星戒指、不可移动之杖、魔像手册各占两份，其余一份），按宝箱编号播种。
- Jewelry（`Bridges/Jewelry`，`LoadFolders.xml` 里 `IfModActive="kikohi.jewelry"`）：只有 XML，4 个“附魔首饰”配方。
- 商队：`Patches/MagicItems.xml`（同盟商队、矮人铁匠、精灵草药商、教会牧师、红袍商人、黑网）。敌人死亡掉落表在 `MagicEffects.Drops`。

**美术**：`ArtSource/RaceArt/draw_magic_items_145.py`（复用 draw_race_art / draw_item_art 的 Pic、轮廓与调色），60 张：`Textures/Items/Magic/*`（57 张物品图 + 光环、标枪飞行图）、`Textures/Items/Scrolls/RemoveCurse`、`Textures/Necromancy/StoneGolem(_south)`。

## 决定

1. **奇物分槽**：用 `AccessorySlot` 扩展而不是改 bodyPartGroup——手套和戒指本来都碰“手”，按部位会互相顶掉。旧的手部圣物（提尔之手等）没标槽，仍按原版规则和戒指冲突，没去动。护盾胸针、圣甲虫、匠神齿轮、审判徽记、复苏核心同在“胸”槽，一次一件；四种印记单独一个“印记”槽。颈部九件一次一件（DND 一个护身符位）。
2. **闪电标枪**：和模组里所有投掷物一样做成可反复投掷的武器（原版手雷规则），“5 支一组”只写进描述。
3. **火球项链**：射程 12、爆炸半径 2.5（规格的“6格”我理解成小半径的意思，6 格射程太近会炸到自己）。
4. **流星戒指**：照明只在夜里；召唤流星白天也能用（冷却半天 = 每天两次）。
5. **储法戒指**：巫妖不用到仪式台，任何地方花 30 精神力即可存恐惧凝视/死灵冲击。卷轴只能从背包或 3 格内存入。
6. **净化封印**：效果记在人身上（`LP_PuritySeals` 状态，严重度=张数），要求贴时穿着躯干衣物；不为全部原版衣服加组件。
7. **伤口闭合护符**：失血停在 90%（会倒地但不会死）。
8. **灵能兜帽 / 法术转向**：拦截原版 `Ability.Activate`（夺心魔、巫妖、灵能都是原版能力）；VEF/VPE 的职业能力不拦。灵能判断按 `IsPsycast` 或 defName 含 Psy/Mind/Dominat/Fear/Gaze/Bind/Blast/Brain。
9. **灵能兜帽来源**：没有吉斯洋基商人，放在红袍商人（塞伊远征军里有吉斯洋基雇佣兵）。
10. **复苏核心**：不论成败都会碎；巨魔、巫妖死亡时不启动（各有自己的复活）；死亡骑士套装（1.45.1）留了 `MagicEffects.DeathHooks` 挂钩。
11. **所有奇物/圣物在炼金桌制作**（智识 6）；“重新祝圣殉道者念珠”也在炼金桌（2 小时 + 金 5），没放到信仰圣坛，避免圣坛自己的条件挡住。
12. **魔像手册**：魔像学研究后由巫妖在炼金桌撰写（石块 30 + 金 20，只有巫妖能做），也会出现在 Isekai 宝箱。
13. **英灵号角**的英灵战士是临时的己方小人（会出现在殖民者栏 1 小时，消散时直接移除，不留尸体、不触发死亡心情）。
14. **审判徽记赠礼**：同盟好感第一次 ≥75 时空投一枚（每局一次）。
15. **珠宝联动**：珠宝模组的宝石存在首饰实例里，配方读不到，所以改成“珠宝戒指 + 2 颗对应宝石 + 熔岩结晶（抗寒用 5 寒铁）”。蓝宝石、红宝石、钻石是珠宝模组的 defName，玉石用原版 Jade。
16. **敌人掉落**：红袍法师（火球项链 8%、每种印记 2.5%，规格的“红袍高阶法师”用红袍法师兵种）、吉斯洋基雇佣兵（锐锋剑 5%、星界护符 6%）、同盟圣武士（圣光冠环 5%）、兽人战士/半兽人奴兵（战嚎图腾 10%）。
17. **防探测护符**：代码里没找到“白泽识破”，护符覆盖识破卷轴、探测魔杖和读心（夺心魔 + 传心头盔）；驱散粉尘照样能让隐形现形。

## 文件

新增：`Defs/MagicItems_Weapons.xml`、`MagicItems_Wondrous.xml`、`MagicItems_Relics.xml`、`MagicItems_Devices.xml`、`MagicItems_Recipes.xml`；`Patches/MagicItems.xml`；`Source/MagicItems.cs`、`MagicItemsCursed.cs`、`MagicItemsCombat.cs`、`MagicItemsAbilities.cs`、`MagicItemsChecks.cs`；`Bridges/Jewelry/Defs/JewelryEnchant.xml`；`ArtSource/RaceArt/draw_magic_items_145.py`；60 张贴图。

修改：`Defs/Research.xml`（6 项研究）、`Items_Wondrous.xml`（戒指槽与说明）、`Items_RaceGear.xml`（奇物层标签）、`Ascension.xml`/`Oozefolk.xml`（“饰品栏”→“奇物栏”）；`Source/Items.cs`（设置、防探测）、`ItemsScrolls.cs`（解除诅咒、识破鉴定、魂石复生）、`ItemsThrowables.cs`（闪电、烟雾、驱散伤石魔像）、`ItemsMaterials.cs`（精金护甲层、龙杀手）、`ItemsIsekai.cs`、`ForceDragon.cs`、`ForceDragonPowers.cs`、`LichSpells.cs`、`Gameplay.cs`、`ConstructDesign.cs`、`ConstructHuman.cs`、`Appearance.cs`、`ItemChecks.cs`（两枚戒指的旧检查）；`Bridges/VPE/Source/Artificer.cs`、`BridgeChecks.cs`；`LoadFolders.xml`；`About/About.xml`；`使用说明.txt`。

## 测试

- `pwsh .\Test.ps1 -Only MagicItems,ResearchTab` → **PASS 50**（MagicItems 42 + 存读档 3，ResearchTab 5）。覆盖：57 个定义与贴图、研究坐标、两枚戒指/同名/第三枚、自由行动戒指、诅咒（脱不下、换不了、识破鉴定、设置关闭、解除诅咒卷轴）、复仇之剑放不下、火焰舌让巨魔再生中断（关火无效）、锐锋剑条件与斩首、龙杀手倍率与偏折失效、守御、护盾胸针/圣甲虫 12 次/伤口闭合护符/食人魔手套、圣光冠环 5 次、殉道者念珠 300、誓约戒指转伤、不可移动之杖、无尽水瓶、魔像手册与石魔像（含驱散伤害与存读档）、黑网 30% 诅咒品、Isekai 掉落、精金护甲层。
- `pwsh .\Test.ps1 -Only Items` → **PASS 460**（第一次在“次元袋 +60 搬运量”上失败一次：+59.4，随机小人带了 0.99 的搬运系数，重跑通过，和本版改动无关）。
- `pwsh .\Test.ps1 -VPE` → **PASS 441**（器械师新增 1 条：新魔化兵器的门槛与不可制作的两把）。
- 日志无红字。

## 已知缺口

- 奇物层不画在身上（和以前的戒指、护符一样）；圣光冠环单独画了头顶光环。
- 研究台“鉴定”任务、各能力按钮、水晶球窗口、捉弄袋/英灵号角召唤只做了代码路径，没有逐个进游戏点；自测覆盖的是识破卷轴鉴定。
- 珠宝联动没有在测试档里加载（测试档没装珠宝模组）。
- 法术转向与灵能兜帽不拦 VEF/VPE 的能力。
- 水晶球的“预估威胁点数”是按当前世界威胁点数估的（工业以上派系 ×1.2），不是据点真实生成时的点数。
