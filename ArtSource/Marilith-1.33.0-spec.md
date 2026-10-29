# DND边缘大陆 1.33.0 实现要求：六臂蛇人（Humanoid Alien Races 可玩种族）

2026-10-26 04:20，Peter：“使用 alien humanoid 完成 六臂蛇人及相关基因，需要可以作为殖民者。”
Peter 选了 HAR 独立种族方案，并要求可玩。本文写取舍、数值、接口和测试；没写到的按模组惯例（1.24.0 种族、1.30.0 巨人、1.27.0 VPE 联动）。

**版本**：1.33.0（设备当前 1.32.0）。备份 `ArtSource/backup-1.32.0/`（只备份本次会改的文件 + About.xml + 使用说明.txt + LoadFolders.xml），报告 `ArtSource/Marilith-1.33.0-report.md`，更新 `About/About.xml` 版本与描述、`使用说明.txt` 加 1.33.0 一节。

## 零、已替 Peter 做的默认决定（报告里列出，Peter 可改）
| 问题 | 决定 |
|---|---|
| 身体 | 真六臂 + 真蛇尾（HAR 自定义 BodyDef），没有腿和脚 |
| 副手武器 | 背包里的近战武器自动当副手，最多 2 把；每把需要一对完好的额外手臂（第 3/4 臂一对、第 5/6 臂一对，一对里至少一只手能用就算） |
| 体型 | 1.6（成年），儿童按原版人类生命阶段比例缩放；渲染约 1.3 |
| 性别 | 两性都有，女性 85%（纯女性会让同族无法繁殖） |
| 来源 | 可选为开局殖民者、流浪者加入/难民；敌人：塞伊远征军大型袭击（≥1000 点）里低权重出现 |
| 与巨人关系 | 不是巨人，不走 GiantKin；不能穿巨人专属衣服；普通衣服按身体部位规则穿 |

## 一、HAR 联动方式（必须是可选的）
1. 在 `E:\SteamLibrary\steamapps\workshop\content\294100` 找 HAR（packageId 一般是 `erdelf.HumanoidAlienRaces`，核对 About.xml），记下版本和当前游戏版本对应的 `AlienRace.dll`，按这个版本写 XML 和代码。报告写明 HAR 版本和 workshop 编号。
2. 照 VPE 联动：新建 `Bridges/HAR/`（Defs、Source、Textures、Languages），`LoadFolders.xml` 用 `IfModActive` HAR 的实际 packageId 加载。About.xml 加 `loadAfter` HAR，不加硬依赖。没装 HAR 时模组照常加载、无红字，蛇人不出现。
3. 需要引用 `AlienRace.dll` 的 C# 编进单独的联动程序集（照 `Bridges/VPE/Source` + Build.ps1 做法），主程序集不直接引用 HAR 类型。主程序集里识别蛇人用 `DefModExtension MarilithExtension`（放主程序集）或 defName `LP_Marilith`。
4. 如果确认无法做成可选，先在报告里说明原因再改成硬依赖，并在 About.xml 写清楚。

## 二、种族与身体
### 种族 `LP_Marilith 六臂蛇人`（`AlienRace.ThingDef_AlienRace`）
- 复制原版 Human 的 race/统计基础，改：`baseBodySize` 1.6、`baseHealthScale` 照人类（巨躯规则自己给生命倍率，不要在种族上再乘）、`MoveSpeed` 4.4、`ComfyTemperatureMin` 5 / `Max` 40（冷血）、`ToxicResistance` 0.5、`MeleeDodgeChance` +8、`CarryingCapacity` ×1.2、`WorkSpeedGlobal` +0.1。
- HAR 设置：女性 85%；肤色 3 种（青绿、墨绿、赤金）；发色原版；头用原版人类头部；`humanRecipeImport` 开；吃人类食物、正常心情；可与人类恋爱结婚。
- 生育：允许与人类生育，孩子种族跟母亲。开工前确认 HAR 对 Biotech 怀孕的处理，报告写结果。
### 身体 `LP_MarilithBody`
- 头、颈、躯干、内脏照原版 Human BodyDef 复制。
- 6 组 肩/臂/手/手指：复用原版 `Shoulder`、`Arm`、`Hand`、`Finger` 等 BodyPartDef，带 `ManipulationLimbCore/Segment/Digit` 标签，原版和 EPOE 等的仿生臂手术能直接用。customLabel：左上臂、右上臂、左中臂、右中臂、左下臂、右下臂（肩、手同理）。
- 蛇尾代替双腿：新 BodyPartDef `LP_SerpentTail 蛇尾`（`MovingLimbCore`）→ `LP_SerpentTailMid 尾段`（`MovingLimbSegment`）→ `LP_SerpentTailTip 尾尖`（`MovingLimbDigit`）。没有 Legs/Feet 部位组：鞋、裤、护腿自然穿不上；覆盖躯干+腿的衣服可以穿。
- 命中权重：6 条上肢合计约人类两臂的 1.5 倍；蛇尾约等于人类双腿。
- 不做蛇尾义肢（蛇尾断了靠再生/治疗，巨魔再生等照常生效）。
### 操作与额外手臂
- 原版操作取手臂平均，六臂不超过 100%。常驻 hediff `LP_SixArms 六臂`（代码保证存在；不可治疗、不可移除）：操作 +0.4 × (完好额外手臂对数 / 2)。额外手臂断了加成跟着降。描述：“六条手臂一起干活，操作额外提高。”

## 三、相关基因与异种型
HAR 种族只生成异种型 `LP_MarilithKin 六臂蛇人`（100%）。基因可被提取、放进基因工作台（1.26.0）。装到人类身上时需要额外手臂的基因无效，描述写“需要多于两条手臂”。
| 基因 | 效果 | 代谢 |
|---|---|---|
| `LP_MarilithBlades 六臂连击` | 副手连击（第四节） | -2 |
| `LP_SerpentGlide 蛇行` | 泥地、沼泽、浅水移动消耗减半（Harmony 在 pawn 专属走格消耗上处理，参考霜巨人雪原行者、水中行动）；深水可游（复用“水中行动”组件） | -1 |
| `LP_SerpentConstrict 缠绕` | 能力：近身缠住一名敌人 3 秒（无法行动，每秒 6 点钝伤，体型差修正），期间自己不能移动；冷却 12 小时 | -1 |
| `LP_BladeStorm 剑刃风暴` | 能力：对周围 1 格所有敌人，主手 + 每把可用副手各攻击一次；冷却 2 天；不连锁副手追加 | -1 |
| `LP_AbyssalHide 深渊之躯` | 火焰/烧伤、冷冻、电击伤害 ×0.75；毒素抗性 +0.5 | -1 |
| `LP_ColdBlood 冷血`（缺点） | 环境温度低于 0°C 时移速 -15%（StatPart） | +2 |
| 外观 | `LP_SkinSerpentTeal 青绿鳞`、`LP_SkinSerpentDark 墨绿鳞`、`LP_SkinSerpentGold 赤金鳞` | 0 |
异种型代谢合计约 -4，`inheritable true`。

## 四、副手连击
- 副手：背包里的近战武器，按“品质×DPS”取最高，最多 = 可用额外手臂对数（0～2）。
- 主手持近战武器或空手时，每次普通近战命中后，每把副手各追加一次攻击：用那把武器自己的原版近战 Verb（照 1.30.1 战士旋风斩做法），伤害 ×0.5（设置滑块 0.25～1.0，默认 0.5）。品质、材料、Isekai 近战加成、百战觉醒照常生效；击杀计入 Isekai 里程碑。
- 主手持远程武器时不触发。主动技能（旋风斩、剑刃风暴、缠绕）的命中不触发，追加攻击本身也不再触发。
- 装了双持类模组（Tacticowl、Dual Wield 等，按 packageId 检测）时自动关闭并写日志一行。
- 副手真实贴图本版不做；额外手臂贴图自带固定弯刀剪影。

## 五、外观（RimWorld 风格：手绘粗描边、平涂 + 阴影、低饱和）
- 身体：自定义裸体贴图，上半身照壮硕体型（Hulk）轮廓，腰以下没有腿；蛇人统一用一个体型（建议 Hulk，衣服贴图直接借用；报告说明）。
- HAR bodyAddons：1) 额外两对手臂，身体后层，南/北/东三向，持弯刀剪影；对应部位缺失时隐藏。2) 蛇尾，盘在身体下方，颜色跟肤色，东向往后拖长，绑定 `LP_SerpentTail`。
- 脚本 `ArtSource/RaceArt/draw_marilith_133.py`，预览 `ArtSource/Marilith-1.33.0-preview.png`。1.6 体型看起来比人类大一圈但不压邻格。
- 核对南北东三向、穿防弹衣/风衣/板甲/帽子时层级正确，额外手臂和蛇尾不被衣服盖住。

## 六、和现有系统的兼容（逐项核对，报告写结论）
先全模组 grep `ThingDefOf.Human`、`"Human"`、`race.body`、`BodyDefOf.Human` 等，把只认人类的判断列出来，改成 `RaceProps.Humanlike` 或额外认 `LP_Marilith`。
| 系统 | 要求 |
|---|---|
| 巨躯规则（1.23.0） | 按 BodySize 1.6 自动给生命 ×1.3、近战 ×1.18；确认不是只认 Human/基因 |
| LimbHealth | 六臂和蛇尾部位吃到倍率；HealthCompat 检查通过 |
| 巨人衣服规则（1.30.0） | 蛇人不是巨人：不能穿巨人专属衣服、不给合身心情 |
| Isekai 特性 | 蛇人能抽到 Isekai 特性；检查本模组 Isekai 联动和 IsekaiKillMilestones、HumanAptitudes、MythicBloodlines 是否只认 Human，如有改成类人并各升小版本 |
| VPE 职业（1.27.0） | 能走战士/法师/牧师；旋风斩命中不连锁副手 |
| 基因工作台 | 蛇人基因可提取/植入；人类装六臂连击无效且描述说明 |
| 1.32.0 背景特性 | 蛇人能随机到 |
| 夺心魔吞噬大脑/蝌蚪 | 蛇人尸体算类人、有大脑 |
| 巫妖、巨魔复活等尸体系统 | 不报错 |
| 原版手术 | 仿生臂能装到 6 条手臂任一条；没有腿部手术 |

## 七、测试（Peter：减少测试数量）
- 新测试组 `Marilith`，≤15 项：种族加载、无 HAR 时不报错（可静态检查）、身体 6 手 1 尾无腿、鞋裤不可穿、操作加成随断臂变化、副手数随断臂变化、追加伤害比例和不连锁、蛇行地形消耗、缠绕/剑刃风暴冷却、巨躯规则生命倍率、Isekai 特性可抽到、VPE 路线可选、基因可提取、人类装六臂连击无效。
- `pwsh Source/Test.ps1 -Only Marilith` 迭代；完整测试只跑一次。旧不稳定项不重跑，报告注明。
- 测试需要 HAR 启用：确认 Test.ps1 用的模组列表有 HAR；没有就只在测试配置里加，不改 Peter 的游戏模组配置。

## 八、收尾
- 报告：改动文件、HAR 版本、默认决定、兼容结论、测试结果、遗留问题。
- 提醒 Peter：在模组列表启用 HAR 并放在本模组之前，然后重启游戏。
