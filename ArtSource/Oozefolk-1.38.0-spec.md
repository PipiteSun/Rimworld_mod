# DND边缘大陆 流浆人实现要求 1.38.0
原型 DND Plasmoid。路线：异种型+基因，不走HAR。种族判定：LP_OozeCore 激活即流浆人（静态 Oozefolk.Is(pawn)，Notify_GenesChanged 缓存）。巨躯规则 BodySize.cs 现在算种族基础体型；流浆人是人类 1.0，不受影响。

## 一、异种型与基因
异种型 LP_Oozefolk 流浆人，inheritable true，非超凡，factionlessGenerationWeight 0.5。自定义基因 selectionWeight 0。代谢目标约 -1（偏差大就去掉 Pain_Reduced）。
- LP_OozeCore 流浆之躯（本源，-2）：钝击伤害 ×0.5；酸蚀伤害 ×0.25（DamageDef defName 含 Acid 的全部）；毒素抗性 +50%、环境毒素抗性 +50%；出血 ×0.5（无原版属性则 Harmony 后缀 HediffSet 出血率计算 ×0.5，核对 1.7 方法名）；伤口不感染（HediffComp_Infecter 跳过）；不留疤（HediffComp_GetsPermanent 不转永久）。
- LP_OozeProtoplasm 原生质（-1）：资源条，照原版 Gene_Hemogen/Gene_Resource，最大 100，开局 30，不衰减。
- LP_OozeDigest 万物消化（-1）：食物中毒 ×0；AteRawFood 无效；每吃一顿 +3 原生质；解锁吞没。
- LP_OozePseudopod 伪足（-1）：hediff 给的天然近战 LP_OozeLash 酸性伪足（照牛头人犄角）：钝击 9 + 酸蚀 LP_OozeAcid 4，冷却 2.0；酸蚀对衣服耐久 ×2。操作托底 60%（照象鼻 50% 的写法）。
- LP_OozeAmorphous 无定形（-1）：能力渗过；囚犯越狱期间 Building_Door.PawnCanOpen 对上锁门返回 true；越狱概率 ×2（和 1.32.0 罪犯 ×0.75 相乘）。
- LP_OozeSplit 分裂繁殖（0）：能力分裂；原版怀孕概率 ×0（Fertility 0）。
- LP_OozeChill 凝滞（缺点 +2）：舒适最低温 +8；所在格 <0°C 时 hediff LP_OozeSluggish 凝滞 移速 -25%、操作 -10%（每 250 刻判断，回暖 1 小时消失）；寒冷伤害（冻伤、LP_FrostSlow 冬狼咬、LP_FrostStaffChill 寒霜法杖、寒冰魔）×1.5。
- LP_OozeDesiccate 怕干（缺点 +2）：火焰伤害 ×1.5；易燃性 +0.25。
- 肤色 4 选 1 出生随机（照元素裔）：LP_SkinOozeGreen 胆绿(118,150,104)、LP_SkinOozeBlue 渊蓝(96,132,156)、LP_SkinOozeAmber 琥珀(176,140,82)、LP_SkinOozeViolet 雾紫(138,116,152)。
- 外观：Hair_BaldOnly、Beard_NoBeardOnly；头部附件 LP_OozeSheen 流光（头顶高光 + 下垂胶滴，三向，按肤色着色，照牛角锚点）；体型 Thin/Standard。
- 原版基因：Immunity_Strong、Pain_Reduced。
不做：改体型、半透明着色器。

## 二、能力（Source/Oozefolk.cs；原生质不足时按钮灰掉，提示“原生质不足（需要 X）”）
- 吞没（右键尸体浮动菜单 + 能力按钮，复用吞噬大脑 Job）：2 小时（5000 刻），可打断。尸体消失，衣物武器掉原地。人形新鲜 +25 原生质、饱食 +50%；动物 原生质=体型×15（上限 30），饱食=体型×30%（上限 60%）；腐烂 ×0.5；干尸不可吞；机械体、巫妖造物、流浆人尸体不可吞。旁观殖民者 15 格内 -3“目睹吞没”1 天（食人者、精神病态、流浆人除外）。吞人形尸体记一次原版吃人肉历史事件（核对 1.7 defName）。
- 分裂（成年）：80 原生质 + 饱食 25%，引导 1 小时。生成新生儿：同异种型，复制父体全部内源基因（外源不复制），肤色随机，父体为唯一 Parent，同派系。父体 LP_OozeSplitWeak 分裂虚弱 3 天（意识 -20%、移速 -20%）。冷却 30 天（设置 15–60）。
- 渗过：10 原生质，冷却 1 小时；目标 3 格内、与自己之间隔一扇门（任何门，含上锁/敌方），瞬移到另一侧。复用 Githyanki.Step，胶滴特效。不能穿墙/岩壁。
- 重塑：40 原生质，冷却 2 天，对自己，引导 6 小时：恢复一个缺失的非要害外部部位（手、脚、指、趾、耳、鼻；不含眼和内脏，大的优先），否则清除一个永久伤。
- 析出凝胶：25 原生质，引导 30 秒，产出 1 个 LP_ProtoplasmGel。

## 三、物品（Defs/Oozefolk.xml，配方在炼金桌，研究用现有 DND炼金术）
- LP_ProtoplasmGel 原生质凝胶：材料，市值 15，堆叠 25；屠宰流浆人尸体出 3 个，不出人肉人皮（Harmony 后缀 Pawn.ButcherProducts）。
- LP_GelDressing 凝胶敷料：药品，效力 0.9，最大治疗质量 1.0，市值 14；凝胶 1 + 布 3 → 2 个。
- 强酸瓶替代配方：凝胶 2 + 钢 5。黏胶袋替代配方：凝胶 2 + 布 5。
- LP_StabilizerBand 稳形护带：LP_Accessory 层饰品，只对流浆人有效：凝滞触发温度 0°C → -15°C，原生质上限 +20；缝纫台 皮革 20 + 凝胶 5。

## 四、派系、剧本、兼容
- 费伦拓荒同盟兵种 LP_FC_OozeWanderer 流浆人游民（combatPower 55，Combat/Peaceful 权重 1.5）；塞伊不收；黑网商队少量凝胶。
- 剧本 LP_OozeEscape 实验池逃亡：3 名流浆人（一名原生质 60），凝胶敷料 10、强酸瓶 4、包装生存食品 30、钢 200、木 300、布 60、稳形护带 1；第 6 天塞伊追捕小队 300 点（自定义 ScenPart）。
- 兼容：巨躯规则不用改；Isekai 正常（婴儿不继承等级）；VPE 职业正常；背景特性正常进池，分裂婴儿不给；夺心魔蝌蚪对流浆人无效（植入当天死亡，照火巨人免疫接口）；LP_OozeAcid 含 Acid 自动打断巨魔再生；流浆人尸体不能做骷髅类死灵造物（查造物尸体判定）；基因全部可提取。

## 五、贴图
基因图标 8 个 256px（流浆之躯、原生质、万物消化、伪足、无定形、分裂繁殖、凝滞、怕干；肤色用原版肤色图标模板）；能力图标 5 个 128px（吞没、分裂、渗过、重塑、析出凝胶）；头部附件流光三向 128px（白灰底图由肤色着色）；物品 3 个（凝胶、敷料、护带）；预览图。

## 六、测试 OozefolkChecks.cs，-Only Oozefolk（约 8 条）
1 异种型生成带本源和 4 肤色之一，代谢 -2~0；4 吞没人形尸体 +25、尸体消失、衣物掉落；5 分裂 -80、新生儿同异种型同内源基因、亲子关系、父体虚弱；6 渗过上锁门到另一侧；9 双手缺失操作 ≥60%；10 -5°C 凝滞、火焰 ×1.5；13 存读档原生质/冷却/凝滞不变；可选：2 钝击×0.5/强酸×0.25，11 蝌蚪无效，12 屠宰得凝胶。剧本、兵种、配方、贴图不自动测，报告注明请在游戏里看。

## 七、收尾
About.xml、使用说明.txt、报告 ArtSource/Oozefolk-1.38.0-report.md；提醒 Peter 重启游戏。默认取舍（代 Peter 定）：分裂出婴儿、不能和别族怀孕、吞没算吃人肉、渗过只穿门、越狱无视锁门。
