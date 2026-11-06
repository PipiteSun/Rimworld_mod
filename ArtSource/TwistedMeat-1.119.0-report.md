# 扭曲肉资源化 1.119.0 报告

规格：ArtSource/TwistedMeat-spec.md（Peter 2026-11-05，经协调线程转达）。备份：ArtSource/backup-1.118.0（About.xml、使用说明.txt、LoadFolders.xml、Source/Gameplay.cs、Source/Test.ps1、旧 DLL）。

## 结构
- 所有引用异象（Anomaly）的 XML 在 **Bridges/Anomaly**（LoadFolders `IfModActive="ludeon.rimworld.anomaly"`）：Defs/TwistedMeat_Items.xml、TwistedMeat_Recipes.xml、TwistedMeat_Buildings.xml，Patches/TwistedMeat.xml，Textures/TwistedMeat/*.png。
- C# 在主程序集（异象的类型本来就在 Assembly-CSharp 里），所有 Def 按名字判空，`TwistedMeat.Active` = 异象启用且有 Meat_Twisted。
  - Source/TwistedMeat.cs：核心工具、FleshCount 计数器、GameComponent_TwistedMeat（回响、每日计数、解剖累计、催转周期、鬼婆、词条、滋养、畸变胚体）、回响补丁。
  - Source/TwistedMeatItems.cs：启动时追加描述、再生膏瘤块、狂乱之瓶、畸变皮甲好感、血肉再生词条（附魔台按钮 + 窗口 + 美观 StatPart）。
  - Source/TwistedMeatAnomaly.cs：焚化扭曲肉、远域解剖 / 异怪学、研究门槛、异怪伤害、生物铁每日上限、食尸鬼、远域献飨（PsychicRitualDef 子类 + Toil）、鬼婆集市、交易价格。
  - Source/TwistedMeatCatalysis.cs：血肉催转（冷却来源、对话窗口、JobDriver、建筑按钮）。
  - Source/TwistedMeatFlesh.cs：孵化槽、血肉壁垒、血肉兽仆从、畸变巨像、死灵滋养、巫妖吞食。
  - Source/TwistedMeatChecks.cs：`Test.ps1 -Anomaly -Only TwistedMeat`。
- 改动的现有文件：Source/Gameplay.cs（4 处单行：死灵仪式台的生肉判定接受畸变膏、献祭经验按 FleshUnits 计）、Source/Test.ps1（新开关 -Anomaly）、LoadFolders.xml、About.xml、使用说明.txt。
- 贴图脚本：ArtSource/RaceArt/draw_twistedmeat.py（预览 ArtSource/Preview/TwistedMeat.png）。

## 新 defName
物品 / 建筑：LP_AberrantPaste、LP_AberrantSalve、LP_MorphDraught、LP_FrenzyFlask、LP_Proj_FrenzyFlask、LP_EyeOfHadar、LP_AberrantHideArmor、LP_FleshIncubator、LP_FleshWall。
状态 / 想法：LP_AberrantTumor、LP_MorphDraught、LP_FarRealmSated、LP_FleshMinion、LP_AberrantGolem、LP_FleshNourished、LP_FarWhispers、LP_AberrantHideSocial。
配方：LP_StabilizeRot、LP_BrewTrollRegenAberrant、LP_BrewAberrantSalve、LP_BrewMorphDraught、LP_MakeFrenzyFlask、LP_FarDissection、LP_ExtractBioferrite、LP_TemperColdIronBio、LP_Offer_Twisted、LP_MakeAberrantGolem、LP_RemoveAberrantTumor（畸变皮甲由 recipeMaker 生成）。
其他：LP_Research_FleshShaping（1.0, 5.6）、LP_Research_Aberrology（2.0, 5.6）、LP_FarRealmFeast（灵能仪式）、LP_HagMarketTrader、LP_HagMarket（事件）、LP_FleshCatalysis（作业）。

## 设计决定 / 偏差
- A：只改规格列出的 7 个配方（骷髅、知识死灵胚体仍按 1:1 收扭曲肉，和以前一样）。描述在启动时追加一句；旧存档里已有的工作单原料过滤不会自动勾上畸变膏。
- B/C：畸变再生膏 40 银；蜕形药剂“疼痛阈值 +20%”用 painFactor 0.833 实现；社交影响在测试里实际是 -27%（本模组的属性系统对社交影响偏移有缩放）。狂乱之瓶用原版 PanicFlee + 480 刻自动恢复，机械体免疫。
- C5：没有通用“词条”系统，所以“血肉再生”是附魔台上的独立按钮 + 小窗口（照附魔强化窗口的样子），5 份畸变膏，没改 GearEnhanceWindow.cs。“每天恢复 1 点小伤”= 每天治好最轻的一处伤（最多 6 点）。
- D1：每日上限按“每天 3 次配方（30 份）”实现（AvailableOnNow + ShouldDoNow）。寒铁替代配方产出 5 寒铁（原配方 30 钢 + 10 银 → 10）。
- D3：每 30 份 +1 神眷（按 Faith.Gain 的倍率），在 ConsumeIngredient 里累计（1.59.0 教训）。
- D4：解剖放在炼金桌；每 50 份 = 1 点基础异象知识；500 份时直接 FinishProject 异怪学，异怪学不能手动研究。“异怪类”= 异象实体、实体派系，以及 defName / 异种型含 Beholder、MindFlayer、Illithid、Mimic、Aboleth、Gibbering、Nothic 的。
- D5：鬼婆集市用妖精宫廷派系出商队（PactEvents.SpawnCaravan）；礼物从 4 个印记 + 复仇之剑里随机。
- D6：哈达之眼 = 眼部奇物（数值见使用说明）。奖励分档：<60% 秘银，≥60% 精金，≥90% 30% 哈达之眼。失败 50/50 血肉兽群（洞口涌出，点数为当前威胁的一半，最少 300）或一天的高强度心灵哭嚎（原版 PsychicDrone）。
- D7：回响只计扭曲肉（畸变膏做的时候已经计过）：配方原料、食尸鬼吃、孵化槽、滋养、吞食、血肉催转、远域献飨。加成方式是 Anomaly 的 ThreatBig/ThreatSmall 事件 BaseChanceThisGame ×(1+回响)。
- E：来源 = 灵能仪式点（全部异象灵能仪式冷却，含远域献飨）、深渊法阵（炼魂，1 天冷却 → 按“至少剩 1 天”规则实际上不能缩短）、契约讲台（各宗主的 60 天拒签冷却）、信仰圣坛（更换守护神 15 天、祈唤行星使 1 年）。扭曲肉放在建筑 12 格内（和死灵胚体的“12 格内材料”一致），派工靠窗口里选人，不用 FloatMenu。
- F1：孵化 6 小时一只（规格没写时间）；血肉兽是玩家派系的指刺兽（Fingerspike）。
- F2：生命 480，用异象的血肉团贴图（CornerFiller 链接）；“怕酸”= 伤害类型名含 Acid 的 ×2。
- F3：防弹背心外观（原版贴图染成肉色），利器 0.85。在炼金桌做（不是裁缝台）。
- F4：生命 +20% 用“受到伤害 ×0.833”实现（等效），每小时治 1.5 点。
- F5：知识死灵（魂封大脑 / 眼睛 / 手臂 / 心脏就是现在的四种知识死灵）上的开关；从整张地图取扭曲肉。
- F6：死灵之躯没有饥饿需求，所以做成按钮“吞食扭曲肉”（12 格内 10 份 → 冷却最久的死灵能力：灵魂箭、恐惧凝视、律令死亡、寒冰射线、修补造物）；食尸鬼 / 死灵之躯吃扭曲肉时去掉“吃了扭曲肉”心情。
- F7：缚链基因商会是独立模组（SlaveGeneTraders）→ 跳过。
- 跳过：蝌蚪培育。原版饲料 / 生物燃料：扭曲肉属于 MeatRaw 类别，原版 Data 里没有任何把 Meat_Twisted 排除出配方的写法，所以按 MeatRaw 类别收原料的配方（如动物饲料）应当已经接受；只查了定义，没在游戏里验证；未修改。

## 测试
- `Test.ps1 -Anomaly -Only TwistedMeat`：**PASS 23 条（含存读档），日志无红字**。中途修了一个红字：血肉壁垒最初用 CornerOverlay 链接，自动生成的蓝图没有 cornerOverlayPath → 加载时 ArgumentNullException（folderPath），改成 CornerFiller。
- 受影响组（不带异象，确认没有异象时照常加载）：`-Only 'Faith,GearEnhance,ConstructDesign'` 第一轮在 Faith 的“faith off … trait weights come back”（FaithSettingsOff）失败，这条检查的是信仰关闭时的事迹 / 任务 / 祈祷 / 神迹和特性权重，本次改动没有碰这些；用旧 DLL 做对照没跑起来（其他线程已经加了新的 Def）。`-Only 'GearEnhance,ConstructDesign'`：**PASS 150 条**。
- 没跑完整测试。

## 游戏内未验证
- 远域献飨整个仪式流程（选人、送祭品、质量面板里的“旧日支配者邪术师”一行）；测试只验证了结算函数。
- 鬼婆集市商队实际到达 / 离开留礼物、交易界面里的 3 银 / 18 银 / ×1.5 价格。
- 玩家派系的指刺兽在长时间游戏里的行为（会不会乱跑、能否征召）。
- 血肉壁垒的贴图链接观感、孵化槽 2×2 贴图。
- 血肉催转窗口的布局；畸变皮甲穿在身上的颜色。
- 回响对实际事件频率的影响（只验证了基础概率 ×1.05）。

## 补充（2026-11-05 16:06，Peter「生成地图时出错」复现）
- 用 Peter 的实际模组列表（Test.ps1 -Full，隔离档）新建世界 + 地图：本模组没有红字。日志里只有 GD3.HarmonyInit（其他模组）和世界生成时原版中文“超凡者”起名规则缺 r_deityName（原版自动改用英文重试，发生在派系神名生成，不是地图生成）。
- 新工具 Test.ps1 -Full -LoadSave <存档名>（Source/LoadSaveProbe.cs）：把存档复制进隔离档读取并跑约 1500 帧。读取「传奇之门」：正常，只有 GD3 噪音。
- 修复：狂乱之瓶的投掷动作带 forcedMissRadius，但弹体不是 Projectile_Explosive → 原版定义检查报配置错误（会在加载时出红字）。去掉 forcedMissRadius（XML，不用编译）。
- 测试修正：吞食检查把扭曲肉放到巫妖脚下（全模组档里巫妖会走开，超出 12 格）。
- 复测：-Full -Only TwistedMeat 23 条通过；-Anomaly -Only TwistedMeat 23 条通过、无红字。
