# 普通种族 1.57.0 实现要求

版本：1.57.0（设备当前 1.56.0）。改动前先把设备上的模组复制到 ArtSource/backup-1.56.0。
原则：全部是 Biotech 异种型 + 基因（不用 HAR），复用 1.15.0 的共享组件（SmallBody、LP_MindResist、OncePerCooldown、RacialRelation）和 LimbHealth；每个种族最多 2 个被动自定义机制 + 至多 1 个主动技能；代谢 -2～+2。所有可见文字用中文标签。

## 一、范围
| 种族 | 异种 defName | 说明 |
| 高等精灵 | LP_Elf（保留旧 defName，改标签为「高等精灵」） | 旧存档不丢；加 艺术天赋 + 学习快 |
| 木精灵 | LP_WoodElf | 植物天赋 + 移速快，偏棕肤 |
| 卓尔精灵 | LP_Drow | 第三亚种，怕光夜战 |
| 山地矮人 | LP_Dwarf（保留旧 defName，改标签「山地矮人」） | 近战天赋 + 矮人护甲训练 |
| 丘陵矮人 | LP_HillDwarf | 强免疫 + 矮人坚韧 |
| 轻足半身人 | LP_HalflingLightfoot | 好看 + 社交天赋 + 躲到大个子身后 |
| 敦实半身人 | LP_HalflingStout | 毒抗 + 强免疫 |
| 岩侏儒 | LP_RockGnome | 制作天赋 + 发条玩具 |
| 森林侏儒 | LP_ForestGnome | 驯兽天赋 + 与小动物交谈 |
| 地侏 | LP_DeepGnome | 石肤伪装、轻度怕光 |
| 半精灵 | LP_HalfElf | 社交 + 两项自选天赋 |
| 狗头人 | LP_Kobold | 陷阱匠、群体战术、龙之仆从 |
| 地精 / 大地精 / 熊地精 | LP_Goblin / LP_Hobgoblin / LP_Bugbear | 地精三族 |
（若设备上旧精灵/矮人 defName 不是 LP_Elf/LP_Dwarf，以设备为准，同样保留旧名改标签。）
不做：海精灵、影裔精灵、灰矮人、彩色龙裔、变形者、罗丝的眷顾、地精战帮独立派系。
实现前先读设备：1.16.0 精灵/矮人/半兽人 Defs 与代码、SmallBody.cs、MindResist.cs、OncePerCooldown.cs、RacialRelation.cs、LimbHealth.cs，以及冒险者「种族亲和」表、背景故事表、派系 xenotypeSet、设置窗口的种族开关。1.16.0 已有的机制（冥想之息、石之耐力、酒鬼之肝等）沿用不重做。

## 二、共享做法
- 亚种 = 独立异种型，每族一个共享核心基因（LP_ElfBlood、LP_DwarfBlood、LP_HalflingBlood、LP_GnomeBlood、LP_GoblinoidBlood；已有则扩展），亚种基因共用 exclusionTag（LP_ElfLineage 等）。
- 小体型用 SmallBody：半身人 0.7、侏儒 0.6、地侏 0.6、狗头人 0.6、地精 0.6；矮人沿用旧值。不减饭量。
- LP_MindResist：侏儒 +60%（三亚种），半精灵 +25%，精灵沿用旧值。
- 种族关系全部写进 RacialRelation XML 表。
- 性能：所有"身边有几个友方"类 StatPart 用 250 tick 缓存；Harmony 补丁先判断异种/基因。

## 三、各种族
1. 精灵：高等在旧 LP_Elf 上加 AptitudeStrong_Artistic、Learning_Fast，白肤金银发。木：AptitudeStrong_Plants、MoveSpeed_Quick，棕肤深褐/红发。卓尔 LP_Drow：尖耳、深灰/黑紫肤、白发；DarkVision、UVSensitivity_Intense、灵能敏感提高、AptitudeStrong_Melee。[自定义] 阳光敏感 LP_DrowSunlight：白天（CurSkyGlow≥0.6）且无屋顶时射击与近战命中 ×0.85。[自定义] 蛛后之毒 LP_DrowPoison：近战命中 15% 概率给目标 LP_DrowVenom（意识 -20%、移速 -20%，6 小时），对机械体无效。[主动] 黑暗术 LP_DrowDarkness：无需灵能链接，冷却 1 天，射程 15，半径 3，12 秒内范围里非卓尔敌对单位获得 LP_MagicalDarkness（命中 -30%），黑烟 mote。代谢目标 0。精灵↔卓尔 -5/-5。
2. 矮人：山地 LP_Dwarf 加 AptitudeStrong_Melee；[自定义] 矮人护甲训练：MoveSpeed StatPart，装备里负的移速 offset ×0.5。丘陵 LP_HillDwarf：Immunity_Strong；[自定义] 矮人坚韧：LimbHealth 部位血量 ×1.15。两亚种共有 [自定义] 矮人记仇 LP_DwarfGrudge：玩家派系人形被某派系成员杀死时，殖民地所有带该基因者获得「血仇：<派系>」-3 心情 1 年（同派系刷新不叠加）；对该派系成员射击/近战命中 +5%（GameComponent 字典 派系→到期 tick，存档保存）。
3. 半身人：核心 MoveSpeed_Quick、AptitudeStrong_Plants、AptitudeStrong_Cooking、Mood_Optimist，体型 0.7。[自定义] 半身人的幸运 LP_HalflingLuck（OncePerCooldown）：一次伤害会致倒地或死亡时伤害 ×0.1，飘字「真走运！」，3 天冷却（可见 hediff「运气用光了」）。[自定义] 勇敢：不会进入 PanicFlee 及模组里的恐惧类精神状态，恐惧负面心情减半。轻足 LP_HalflingLightfoot：Beauty_Pretty、AptitudeStrong_Social；[自定义] 躲到大个子身后：2 格内有体型≥1 友方时被远程命中率 ×0.85（ShotReport 后缀，250 tick 缓存）。敦实 LP_HalflingStout：ToxResist_Partial、Immunity_Strong。
4. 侏儒：核心 AptitudeStrong_Intellectual、Learning_Fast、Mood_Optimist、尖耳；缺点 MeleeDamage_Weak、AptitudePoor_Melee、MoveSpeed_Slow；体型 0.6；心智抗性 +60%（侏儒狡黠）。岩 LP_RockGnome：AptitudeStrong_Crafting；[物品] 发条玩具 LP_ClockworkToy：小型单人娱乐建筑，美观 +3，配方 钢30 + 零部件1，放在符文铁砧（按 1.47.0 工作台规则；不放原版锻造台），不加研究。森林 LP_ForestGnome：AptitudeStrong_Animals；[自定义] 与小动物交谈：驯服体型≤0.5 动物成功率 ×2，失败不反击。地侏 LP_DeepGnome：DarkVision、UVSensitivity_Mild；[自定义] 石肤伪装：连续 120 tick 未移动未攻击获得 hediff，被远程命中率 ×0.7，移动/攻击即移除（250 tick HediffComp 检查 + 攻击时移除）。侏儒↔矮人 +2；侏儒→夺心魔 -5。
5. 半精灵 LP_HalfElf：尖耳、AptitudeStrong_Social、Beauty_Pretty。[自定义] 多才多艺：两组互斥基因 LP_HalfElfTalentA_<技能> / LP_HalfElfTalentB_<技能>，12 技能各一（同原版强天赋 +4，代谢 -1），exclusionTag LP_HalfElfTalentA / B；异种定义不放天赋基因，生成 LP_HalfElf pawn 时随机各加一个且技能不同；玩家可在基因编辑器自选。[自定义] 两族之桥：地图上有玩家派系半精灵时，精灵与人类/矮人之间负面种族关系减半。心智抗性 +25%；若精灵有长寿机制，半精灵衰老 ×0.6。代谢目标 0。
6. 狗头人 LP_Kobold：AptitudeStrong_Crafting、AptitudeStrong_Mining、DarkVision、UVSensitivity_Mild、Tail_Smooth、小角（1.7 有则用 Headbone_MiniHorns，否则模组已有小角或跳过）、红褐/蓝灰肤色；缺点 MeleeDamage_Weak、Delicate、Beauty_Ugly；体型 0.6；代谢 +2。[自定义] 群体战术：近战目标 1 格内每多一个本方单位近战命中 +5%（最多 +15%），缓存。[自定义] 陷阱匠：建陷阱速度 ×2；狗头人建造的陷阱伤害 +30%（不便实现则改成"有狗头人殖民者时所有玩家陷阱 +30%"并写入报告）。[建筑] 狗头人刺坑 LP_KoboldSpikePit：一次性陷阱（复用原版尖刺陷阱），木材 20，需要至少一个狗头人殖民者才能放置（中文提示）。[自定义] 龙之仆从：玩家派系有力能龙或五色真龙时心情 +6「侍奉巨龙」，每个狗头人给力能龙宝库价值 +2%（最多 +20%，接口不便就只做心情）；没有龙时 -2「失去主人」；250 tick 检查，按地图缓存。关系：狗头人↔侏儒 -3、狗头人→巫妖 -2。
7. 地精三族（共享 LP_GoblinoidBlood）：地精 LP_Goblin：MoveSpeed_Quick、DarkVision、Ears_Pointed、绿肤；缺点 Beauty_Ugly、MeleeDamage_Weak；体型 0.6；代谢 +1；[自定义] 灵巧逃脱：被近战命中后 2 秒移速 +30%（刷新不叠加）；[自定义] 小个子的怒火：打体型更大的目标伤害 +15%。大地精 LP_Hobgoblin：AptitudeStrong_Shooting、Aggression_Aggressive、DarkVision、橙红肤色；代谢 -1；[自定义] 群体之助：3 格内每个友方人形 +3% 命中与全局工作速度（最多 +9%），缓存；[自定义] 铁律：征召时不触发精神崩溃。熊地精 LP_Bugbear：Body_Hulk、MeleeDamage_Strong、Furskin、缺点 Learning_Slow；代谢 -1；[自定义] 突袭：目标 600 tick 内未攻击也未被攻击时第一击伤害 ×1.5；[自定义] 长臂：近战命中 +10%（基因 statOffsets）。关系：地精族↔矮人 -3，大地精↔精灵 -3，地精族↔半兽人 +2。

## 四、接入现有系统（照已有种族的做法）
1. 派系：普通种族加入已有普通种族所在派系 xenotypeSet；卓尔、狗头人、地精三族额外加入现有敌对 DND 派系（如 LP_FaerunHostile），地精三族权重偏高；不新建派系。
2. 冒险者种族亲和表：高等精灵 智力/敏捷、木精灵 敏捷/感知、卓尔 敏捷/魅力、山地矮人 力量/体质、丘陵矮人 体质/感知、轻足 敏捷/魅力、敦实 敏捷/体质、岩侏儒 智力/体质、森林侏儒 智力/敏捷、地侏 智力/力量、半精灵 魅力+两项随机（不便则 魅力/敏捷）、狗头人 敏捷/智力、地精 敏捷/体质、大地精 体质/智力、熊地精 力量/敏捷，按表现有格式。
3. 背景故事：每个新种族至少 1 条童年 + 1 条成年中文背景（地精三族、狗头人可共用），照 1.46.0 种族限定写法。
4. 设置窗口：照现有种族开关加入已有种族分组，不新增分组。
5. 剧本「冒险者小队」：5 人（人类、高等精灵、山地矮人、轻足半身人、半兽人），起始物品照普通开局，材料必须是 stuff。
6. 若有按异种的 Isekai 评级/职业亲和表，补上新异种。
7. 本版本不改研究页。

## 五、种族关系新增
精灵(任一亚种)↔卓尔 -5/-5；侏儒↔矮人 +2/+2；侏儒→夺心魔 -5；狗头人↔侏儒 -3/-3；狗头人→巫妖 -2；地精族↔矮人 -3/-3；大地精↔精灵 -3/-3；地精族↔半兽人 +2/+2；半身人↔矮人 +1/+1。只计殖民者与囚犯。半精灵在场时精灵↔人类/矮人负面值减半。

## 六、美术
每个新自定义基因一个图标（约 20 个）、黑暗术、发条玩具、狗头人刺坑贴图、各异种图标；外观用原版头发/耳朵/肤色，不画新身体。预览图 ArtSource/CommonRaces-1.57.0-preview.png。

## 七、测试（-Only CommonRaces，≤25 项）
每族 2～3 项：异种存在且代谢 -2～+2；体型/心智抗性；幸运触发与冷却；勇敢挡恐慌；卓尔白天露天命中下降、黑暗术给敌人 hediff；矮人记仇心情与命中加成并能存读档；半精灵生成有两个不同天赋；狗头人刺坑可建、群体战术缓存；地精三族各一项；剧本存在且 5 人异种正确；种族亲和表含全部新异种。
