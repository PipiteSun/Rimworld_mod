# DND边缘大陆 1.44.0 实现要求：双头巨人、独眼巨人（HAR 种族）

## 零、模板与原则
- 完全照 1.33.0 六臂蛇人：放进 Bridges/HAR/（Defs、Textures），LoadFolders 已有 HAR 条件。规则写在主程序集、用 DefModExtension 判断（照 MarilithExtension）。没装 HAR 时两个种族不出现、无红字（-NoHAR 跑一次）。
- 两个都是巨人：本源基因挂 GiantKinExtension，所以 GiantKin.Of(pawn) 认它们 → 能穿巨人专属衣服、拿巨人专属武器、吃阶序心情、巨人迁徙规则通用。阶序位：双头巨人 rank 5、独眼巨人 rank 6（排在霜巨人之下）。
- 体型只走一个来源：种族 baseBodySize。本源基因不要再挂体型扩展；核对 GiantKin.Size()、FireGiantRenderSize/BodySize 的取最大值不会把种族体型和基因体型叠乘。儿童体型按 HAR 生命阶段或 GiantKin.Size 成长曲线二选一，报告写明。
- 可遗传：孩子跟母亲种族；和人类可以恋爱生育（照六臂蛇人设置）。
- 种族生成权重各 3（人类 100，六臂蛇人 5），可作为开局殖民者、流浪者加入、难民。
- 无神祇规则，信仰系统照普通殖民者。

## 一、双头巨人 LP_Ettin
种族与身体：
- baseBodySize 2.0，渲染约 1.4（同霜巨人）；移速 4.0；学习 ×0.85；胃 180%（接入巨人胃表）。
- 身体 LP_EttinBody：照原版 Human，两个颈、两个头：左头/右头各有头骨、大脑、双眼、双耳、鼻、下颌（customLabel 左头的…/右头的…）。四肢一套。
- 双脑规则（先反编译核对 1.7 原版意识和死亡判定）：两个大脑都带 ConsciousnessSource；意识 = 两脑效率平均，但只要还有一个脑完好，意识下限 60%。失去一个头不会死：原版若判死，用 Harmony 改成两个都没了才死。失去一个头后得到永久 hediff LP_EttinLoneHead 孤首：心情 -8，视力 ×0.75，轮流值守失效。
- 视力：四只眼，原版按 SightSource 平均。
- 外观：主头用原版人类头（发型、胡子、帽子照常）；第二个头是 HAR bodyAddon，自画 3 款粗犷头（男2女1），颜色通道肤色+发色，三向；部位缺失时隐藏。
- 衣服：普通帽子只盖主头（第二个头部位放进新部位组 LP_EttinSecondHead）。专属 LP_EttinTwinHelm 双盔同时覆盖两个头。
基因（异种型 LP_EttinKin，HAR 种族 100% 生成；可提取）：
- LP_EttinCore 双头巨人本源：GiantKinExtension rank 5；不在乎裸体。代谢 -2
- LP_EttinWatch 轮流值守：RestFallRateFactor ×0.5；睡觉时 10 格内出现敌对角色立即醒来（250 刻检查，只对睡着的双头巨人）；需要两个头都在。-1
- LP_EttinTwinGrip 左右开弓：复用六臂蛇人副手系统，背包里 1 把近战武器当副手，伤害照六臂蛇人设置滑块；需要两只手都能用。把副手逻辑改成通用扩展（如 OffhandExtension { int max; bool perExtraArmPair; }），六臂蛇人保持原行为。-1
- LP_EttinQuarrel 两头不和（缺点）：新精神状态 LP_EttinArguing 内讧：原地争吵 1 小时不工作；加入轻度崩溃候选（权重照原版发呆）；另每 15 天 10% 随机触发一次，心情 >70% 不触发。+2
- 外观：LP_SkinEttinGrey 灰褐皮、LP_SkinEttinOchre 赭土皮（出生随机）。0
装到人类身上：轮流值守、左右开弓无效，描述写“需要两个头”/“需要两只手”。

## 二、独眼巨人 LP_Cyclops
- baseBodySize 1.8，渲染约 1.35；移速 4.2；学习 ×0.85；胃 170%。
- 身体 LP_CyclopsBody：照 Human，只有一只眼（LP_CyclopsEye 独眼，SightSource，生命照人类单眼 ×1.5）；没了就失明；可以装原版仿生眼（humanRecipeImport，确认义眼配方能装到这个部位）。
- 头：HAR 种族专属头型（HeadTypeDef 2男2女），额头正中一只大眼；发型、胡子、帽子照常（核对帽子位置）。
基因（异种型 LP_CyclopsKin）：
- LP_CyclopsCore 独眼巨人本源：GiantKinExtension rank 6；不在乎裸体；免疫“凝视”类效果（全模组 grep Gaze/凝视，如巫妖恐惧凝视，在施加处跳过）。-2
- LP_CyclopsCraft 独眼匠心：建造速度 +20%，锻造类配方工作速度 +15%。-1
- LP_CyclopsShepherd 牧者：驯服、训练、动物采集产量 +25%；殖民地有绵羊/山羊时 +2 心情“羊群”。-1
- LP_CyclopsFlatSight 单眼视界（缺点）：ShootingAccuracyPawn -30%（按原版写法折算，报告写数值）；远程瞄准时间 +10%。+2
- 外观：LP_SkinCyclopsRuddy 赤褐皮、LP_SkinCyclopsOlive 橄榄皮。0

## 三、物品
- LP_EttinTwinHelm 双盔：巨人专属头盔（GiantApparelExtension mood 2），覆盖主头+第二个头；防护照原版平顶盔 ×1.1；锻造台，钢 70；研究「巨人锻造」。
- LP_CyclopsMaul 独眼巨锤：巨人专用近战（照霜巨人斧的只有巨人能用）；钝 power 32 / cooldown 3.0，命中 15% 击晕 1 秒；锻造台，钢 100 或木 150；研究「巨人锻造」。
- LP_CyclopsLens 独眼透镜：饰品层（LP_Accessory），只有独眼巨人能戴：单眼视界惩罚 -30% → -15%；手工台，玻璃钢 10 + 金 5，研究「巨人锻造」。
- 1.43.0 器械师门槛：照 ArtificerGate-1.43.0-report.md，独眼巨锤挂到对应被动下（巨人/锻造类默认 山岳锻造，以报告分组为准）；双盔、透镜若报告里衣服/饰品不设门槛就不设。
- 贴图自带 Worn（头盔三向）。新研究不需要。

## 四、出场
- 巨人迁徙（1.28.0 + 1.34.1 巨人游群 LP_GiantWanderers）：加两种队伍：双头巨人 1（权重 8，讨食营养 ×2）；独眼巨人牧群 = 独眼巨人 1 + 绵羊 3～5（权重 8，礼物：2 只绵羊或独眼巨锤）。其余权重按比例压缩。
- 塞伊远征军：点数 ≥800 的袭击 10% 带 1 名 LP_FH_EttinBrute 双头蛮兵（combatPower 160，独眼巨锤或棍棒，双盔）。独眼巨人不进敌对派系。
- 费伦拓荒同盟商队：10% 带 1 名独眼巨人护卫。
- 剧本 LP_HillOutcasts 山脚的弃儿：1 双头巨人 + 1 独眼巨人 + 1 人类 + 4 只绵羊；物资：棍棒 2、钢 150、干肉饼 60、药草 15；描述建议丘陵。加进 Giants 组“所有剧本描述能显示”检查（19 → 20）。

## 五、兼容（逐项核对，报告写结论）
巨躯规则/LimbHealth（第二个头部位也吃倍率）；巨人衣服/武器/LP_GiantAttire 心情；阶序 rank 5/6 进入高/低位判断，LP_ClanHead 同种计算；六臂蛇人副手改通用后 -Only Marilith 仍 PASS；夺心魔吞噬大脑：双头巨人吞一个脑 = 毁一个头另一个活；蝌蚪只换异种型身体保留；巫妖仪式、神选仪式允许，身体保留；泰坦觉醒：两族算巨人允许，若觉醒需要换 HAR 种族/身体则禁止并说明；Isekai、背景特性、VPE 职业照六臂蛇人（跑 -Isekai）；原版手术：义眼能装独眼，双头巨人两个头的眼耳颚手术都能选。

## 六、测试（测试从简）
新组 GiantKinHAR，≤15 项：两族加载；双头巨人 2 头 2 脑；打掉一个头不死、得到孤首；两个头都没了才死；睡眠下降 ×0.5；副手 1 把；独眼巨人 1 只眼、失去即失明；射击惩罚数值；凝视免疫；两族 GiantKin.Of 非空、能穿双盔/锻工外衣；体型 2.0/1.8 未叠乘；剧本描述能显示；人类装轮流值守无效。
跑：Build；Test -Only GiantKinHAR、-Only Marilith、-Only Giants、-NoHAR 各一次（-Isekai 一次）。完整测试不跑。

## 七、收尾
报告写改动文件、决定、偏差、兼容结论、测试结果，若能截图则附游戏内截图（南/北/东三向，含戴帽和双盔）。

---
（另：备份到 ArtSource/backup-1.43.0/；贴图脚本 ArtSource/RaceArt/draw_hargiants_144.py，预览 ArtSource/HARGiants-1.44.0-preview.png；报告 ArtSource/HARGiants-1.44.0-report.md。）
