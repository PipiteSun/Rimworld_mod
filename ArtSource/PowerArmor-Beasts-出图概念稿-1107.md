# 动力甲（1.126.0）与 DND 野兽第三批（1.127.0）出图概念稿

2026-11-07。用途：拿去 ChatGPT / Midjourney 等出图，出好后发回来，由我们抠图、缩放、做遮罩（_m）、切成游戏贴图。
来源：`ArtSource/PowerArmor-1.126.0-report.md`（含 11-07 按 40K 参考图重画的方向）、`ArtSource/BeastsDND2-1.127.0-report.md`、`Shelved/NewContent-1.126-1.127/Defs/*.xml`。只读，没有改模组。

## 一、通用要求（每条提示词都已带上，这里是解释）

**画风**：RimWorld 原版贴图风格 = 粗深色描边（约 3～4px @256）、平涂色块 + 一层体积阴影、低饱和、没有细碎噪点和写实质感、正交视角（不要透视、不要镜头畸变）。
**背景**：透明（如果工具不支持透明，就用纯白或纯品红 #FF00FF 平底，我们来抠）。不要地面投影、不要文字、不要水印、不要边框。
**视角**：每件出三张，同一比例、同一尺寸、主体居中：
- 正面（south，面朝观众）
- 侧面（east，**朝右**；朝左那张游戏自己镜像）
- 背面（north，背对观众）

可以让 AI 出一张三视图并排的设定图（turnaround sheet），也可以分三次出；关键是三张大小、高度对齐。

**游戏里的尺寸**（出图请用 1024×1024 或更大，我们缩）：
- 身甲：256×256，每件还要 5 种体型（男 / 女 / 瘦 / 壮硕 / 肥胖）。AI 出一套标准男性体型即可，其余体型我们拉伸改画。
- 头盔：256×256。
- 动物：256×256，鹰头马身兽 / 斧喙鸟 / 狮鹫三种驮兽另要一套「背鞍袋」版本。
- 建筑：奥械锻炉占 3×2 格（三向），充能座 1×1 格偏高，电池是 128×128 物品图标。

**身甲 / 头盔的特殊要求**：只画盔甲本身，像穿在隐形人身上——**不要画头、脸、手的皮肤**，身甲的领口留空（头单独画在上面）。身甲画面里人体约占画布高度的 70%，脚底贴近下沿，参照原版海军装甲的大小。
**可染色部分**：标了「甲色主体」的部分游戏里会按玩家选的颜色染色，请用干净统一的一种颜色（我们按它做遮罩）；金边、黄铜、皮带、罩袍等不染色的部分照常上色。
**徽记**：一律 DND 风格（展翼太阳、八芒旭日、齿轮、耐瑟瑞尔符文、星辰、羊皮纸誓约、盾形纹章），**不要任何 40K 标志、双头鹰、骷髅翼、文字和字母**。

---

## 二、奥能动力甲（1.126.0）—— 2026-11-08 按当前第七版贴图重写

当前设计方向：钢铁侠 × 战锤 40K × 光环混合，但不用三者的标志、头盔和文字；造型按游戏内截图的比例和画风——Q 版比例（头盔约占全身一半多）、圆润外形、叶片式肩甲从肩后往两侧斜着撑开。徽记仍是 DND 风格（展翼太阳、八芒旭日、齿轮、符文）。

**通用画风（每条提示词末尾都加上）**
- 中文：RimWorld 游戏内角色贴图风格，Q 版比例，头盔约占全身一半多；外形圆润：圆角胶囊形躯干、圆头胶囊形手臂和腿、圆拳套、圆护膝、圆底靴子；干净平涂加柔和明暗（边缘略暗、左上亮右下暗），大部件描粗黑边，小细节不描边；低饱和；正交视角。三视图并排：正面、右侧面、背面，同一比例对齐。透明背景，不要文字、水印、地面阴影，不要钢铁侠 / 战锤40K / 光环的原作标志和头盔造型。
- English: RimWorld in-game pawn sprite style, chibi proportions with the helmet about half the total height; rounded shapes: rounded-capsule torso, capsule arms and legs, round fists, round knee pads, round-soled boots; clean flat colors with soft shading (slightly darker edges, light from top-left), thick black outline on the major pieces only, muted palette, orthographic. Turnaround sheet: front, right side, back, same scale and aligned. Transparent background, no text, no watermark, no ground shadow, no Iron Man / Warhammer 40K / Halo logos or helmet designs.

**通用盔甲结构**：胶囊形躯干从肩一直包到腰下，胸前两块胸板 + 中缝，胸口一颗发蓝光的奥能核心；三节收窄的腹甲；腰带 + 两片圆头胯甲 + 护裆；手臂贴着躯干两侧（上臂藏在肩甲后、深色肘关节、粗前臂护甲外侧一条辅色条、圆拳套）；两条腿分开（大腿外侧辅色条、圆护膝、小腿、圆底靴）；肩甲是每侧 3～5 片圆头叶片，从肩后斜向外下方层层展开，压在身体和手臂后面。

### 1. 奥械侍从甲 + 奥械侍从盔（入门款，光环战地甲味道）
- 甲：橄榄绿甲色、铁灰色叶片肩甲、颈上一条旧围巾（土黄）、帆布腰带挂一排弹药袋、黄铜框圆形蓝核心、背上一根细天线和两支短推进管。
- 盔：橄榄绿圆角胶囊盔，前面一大块琥珀色反光面罩（带一道斜高光），下巴是深灰呼吸格栅，两侧各一根短软管。
- 提示词：一套奇幻动力盔甲入门款，橄榄绿甲片，铁灰色叶片肩甲每侧三片从肩后斜着往外下方展开，颈上一条旧土黄围巾，帆布腰带挂着弹药袋，胸口一颗黄铜框的蓝色发光核心，背上一根细天线和两支短推进管；头盔是橄榄绿圆角胶囊盔，前面一大块琥珀色反光面罩，下巴深灰呼吸格栅和两根短软管。【加通用画风】
- English: Fantasy power armor, entry model: olive green plates, iron-grey leaf-shaped pauldrons (three per side fanning out and down from behind the shoulders), an old khaki scarf, canvas belt with ammo pouches, a brass-framed glowing blue core on the chest, a thin antenna and two short thrusters on the back; helmet is an olive rounded-capsule helm with a large reflective amber visor, dark grey rebreather grille at the chin and two short hoses. [+ style]

### 2. 秘铸骑士动力甲 + 秘铸骑士盔（主力款，红金）
- 甲：红色甲色、金色叶片肩甲、前臂和大腿外侧金色辅甲条、金框圆形蓝核心、三节腹甲、展翼太阳金腰扣、金护膝、背后两支推进管。
- 盔：红色圆角胶囊盔，甲色尖颌面甲镶一圈金边，一道 V 形蓝色发光目镜，盔顶一条金脊，耳侧各一枚金圆护（不是金色整张面具）。
- 提示词：一套红色奇幻骑士动力盔甲，金色叶片肩甲每侧三片斜着往外下方展开，前臂和大腿外侧有金色辅甲条，胸口一颗金框的圆形蓝色发光核心，三节腹甲，腰扣是金色展翼太阳，金色圆护膝，背后两支推进管；头盔是红色圆角胶囊盔，红色尖颌面甲镶金边，一道 V 形蓝色发光目镜，盔顶一条金脊，耳侧金色圆护。【加通用画风】
- English: Red fantasy knight power armor: gold leaf pauldrons (three per side), gold trim strips on forearms and outer thighs, gold-framed round glowing blue chest core, three-segment abdomen, gold winged-sun belt buckle, round gold knee pads, two thrusters on the back; helmet: red rounded-capsule helm with a red pointed-chin faceplate rimmed in gold, a V-shaped glowing blue visor, a gold crest ridge, round gold ear guards. [+ style]

### 3. 圣誓骑士动力甲（圣武士款，黑金）
- 甲：黑色甲色、金色叶片肩甲、六角形蓝核心外一圈金色八芒旭日、腰侧金色活塞关节、金护膝、腰前一片白色纹章布帘（中间印旭日），背后两支推进管。配黑色的骑士盔。
- 提示词：一套黑色奇幻圣武士动力盔甲，金色叶片肩甲每侧三片斜着展开，胸口一颗六角形蓝色发光核心，外面一圈金色八芒旭日，腰两侧金色活塞关节，金色圆护膝，腰前垂一片白色布帘，中间印着金色旭日，背后两支推进管；头盔同骑士盔造型，黑色配金边。【加通用画风】
- English: Black fantasy paladin power armor: gold leaf pauldrons, a hexagonal glowing blue core surrounded by a gold eight-pointed sunburst, gold piston joints at the waist, round gold knee pads, a white cloth tabard at the waist with a gold sun emblem, two thrusters; helmet same design as the knight helm in black with gold trim. [+ style]

### 4. 秘法炮手动力甲（法师款，紫银）
- 甲：深紫蓝甲色、银色叶片肩甲上一道发光符文线、两肩后各立一根紫色导能晶（黄铜杆）、紫框圆核心、胸前和大腿上的发光符文线、背后一颗大核心外套一圈符文环，没有推进器。配深紫的骑士盔。
- 提示词：一套深紫蓝色的奇幻战斗法师动力盔甲，银色叶片肩甲上有一道发光的淡紫符文线，两肩后各立一根黄铜杆顶着的紫色水晶，胸口一颗紫框的蓝色发光核心，胸前和大腿上有发光符文线，背后一颗大核心外面套一圈符文环，没有推进器；头盔同骑士盔造型，深紫配银边。【加通用画风】
- English: Deep purple-blue battle-mage power armor: silver leaf pauldrons with a glowing violet rune line, a purple crystal on a brass rod behind each shoulder, purple-framed glowing blue chest core, glowing rune lines on chest and thighs, a large back core ringed by runes, no thrusters; helmet same as the knight helm in deep purple with silver trim. [+ style]

### 5. 奇械动力甲（器械师款，青铜）
- 甲：青铜色甲色、深铁色尖头叶片肩甲、胸前一个黄铜齿轮中间嵌着核心、腰侧黄铜活塞、腰上皮质工具袋、背上双核心背包（两颗核心并排）。配青铜色侍从盔。
- 提示词：一套青铜色的奇幻器械师动力盔甲，深铁色尖头叶片肩甲每侧三片，胸前一个黄铜齿轮中间嵌着蓝色发光核心，腰两侧黄铜活塞关节，腰上挂皮质工具袋，背上一台双核心背包（两颗蓝核心并排）；头盔同侍从盔造型，青铜色配琥珀面罩。【加通用画风】
- English: Bronze artificer power armor: dark-iron pointed leaf pauldrons, a brass gear on the chest with a glowing blue core in its hub, brass piston joints at the waist, leather tool pouches, a twin-core backpack; helmet same as the squire helm in bronze with an amber visor. [+ style]

### 6. 誓约堡垒重甲 + 堡垒巨盔（最高档，钢灰金）
- 甲：钢灰甲色、最宽最厚，每侧 5 片金色尖头叶片肩甲、胸前并排三颗金框蓝核心、腰前白色布帘、更粗的腿、背后大推进器。
- 盔：比别的头盔方一点的圆角宽盔，一条窄窄的蓝色目缝，下面呼吸格栅，两侧护颊翼，金色盔脊和金颈甲带一排铆钉。
- 提示词：一套极其厚重的奇幻堡垒动力盔甲，钢灰色，比普通动力甲宽一圈，金色尖头叶片肩甲每侧五片斜着往外下方展开，胸前并排三颗金框的蓝色发光核心，腰前垂一片白色布帘，腿特别粗，背后一对大推进器；头盔是方一点的圆角宽盔，一条窄窄的蓝色发光目缝，下面是呼吸格栅，两侧护颊翼，金色盔脊和金色颈甲带一排铆钉。【加通用画风】
- English: Extremely heavy bastion power armor in steel grey, noticeably wider than the others: five gold pointed leaf pauldrons per side, three gold-framed glowing blue cores side by side on the chest, a white cloth tabard, very thick legs, a pair of big thrusters on the back; helmet: a squarer rounded wide helm with a narrow glowing blue visor slit, a rebreather grille below, cheek guards on both sides, a gold crest ridge and a gold riveted gorget. [+ style]

---

## 三、动力甲配套设施

### 10. 奥械锻炉（工作台，3×2 格，要电）
- **外形**：侏儒奇械师式电炉。中间一座刻着耐瑟瑞尔符文的圆鼓熔炉（炉口透橙蓝两色光），一侧是关节式黄铜机械臂，另一侧是压铸台（铁砧形压模 + 液压杆）；底座是铆钉铁台，背后有几根排气管。
- **配色**：暗铁 + 黄铜 + 炉口橙光，符文淡蓝。
- **视角**：建筑要俯视约 30° 的 RimWorld 建筑视角（不是正交侧视），出正面 / 右侧 / 背面三张；占地横 3 纵 2。

**中文提示词**
> 奇幻侏儒奇械师的魔法电炉工作台，RimWorld 建筑贴图：一座铆钉铁底座的长条工作台，中间是圆鼓形熔炉，炉身刻着发淡蓝光的符文，炉口透出橙色火光，一侧是关节式黄铜机械臂，另一侧是带液压杆的压铸台，背后几根排气管。暗铁和黄铜配色。RimWorld 游戏贴图风格：粗深色描边、平涂加一层体积阴影、低饱和，略微俯视的建筑视角，占地横 3 格纵 2 格。三张：正面、右侧面、背面。透明背景，不要文字水印和地面阴影。

**English prompt**
> Fantasy gnome-artificer arcane electric forge workbench, RimWorld building sprite: a long riveted iron base, a round bulging furnace in the center engraved with faint blue glowing runes and an orange glowing furnace mouth, an articulated brass robotic arm on one side, a hydraulic press-casting table on the other, a few exhaust pipes at the back. Dark iron and brass. RimWorld sprite style: thick dark outline, flat colors with one layer of cel shading, muted palette, slight top-down building view, footprint 3 wide by 2 deep. Three images: front, right side, back. Transparent background, no text, no watermark, no ground shadow.

### 11. 奥能充能座（1×1 格，偏高）
- **外形**：齐胸高的符文立柱（石质或铁质，四面刻符文），顶上一个黄铜爪托着一颗发光的奥能晶，底座有电缆接口。
- **配色**：深灰石 / 铁 + 黄铜爪 + 亮蓝晶体。
- **视角**：只要一张正面（建筑不转向）。

**中文提示词**
> 奇幻魔法充能座，RimWorld 建筑贴图：一根齐胸高的方形符文立柱，深灰铁石材质，四面刻着发淡蓝光的符文，顶上一只黄铜爪托着一颗发亮蓝光的晶体，底座边上有电缆接口。RimWorld 游戏贴图风格：粗深色描边、平涂加一层体积阴影、低饱和，略微俯视的建筑视角，占地 1 格、比一格略高。单张正面。透明背景，不要文字水印和地面阴影。

**English prompt**
> Fantasy arcane charging pylon, RimWorld building sprite: a chest-high square rune pillar of dark grey stone-iron, faint blue glowing runes on each face, a brass claw on top holding a bright blue glowing crystal, a cable socket at the base. RimWorld sprite style: thick dark outline, flat colors with one layer of cel shading, muted palette, slight top-down building view, 1 tile footprint, slightly taller than wide. Single front view. Transparent background, no text, no watermark, no ground shadow.

### 12. 奥能电池（物品图标）
- **外形**：拳头大的黄铜壳晶匣，侧面开窗，里面封着一团静止的蓝光；两端铆钉盖，一端有接口。
- **视角**：单张，物品图标（像原版零件 / 电池图标那样略斜放）。

**中文提示词**
> 奇幻魔法电池物品图标：一个拳头大的黄铜外壳晶匣，侧面开着玻璃窗，里面封着一团静止的蓝色光团，两端是铆钉端盖，一端有金属接口。RimWorld 游戏物品图标风格：粗深色描边、平涂加一层阴影、低饱和，单个物品居中略斜放。透明背景，不要文字水印和地面阴影。

**English prompt**
> Fantasy arcane battery item icon: a fist-sized brass-cased crystal cell with a glass window on its side showing a still ball of blue light inside, riveted end caps, a metal connector on one end. RimWorld item icon style: thick dark outline, flat colors with one layer of shading, muted palette, single object centered at a slight angle. Transparent background, no text, no watermark, no ground shadow.

---

## 四、DND 野兽第三批（1.127.0）

动物三视图约定：**正面 = 面朝观众**（south），**侧面 = 头朝右的完整侧身**（east），**背面 = 背对观众**（north）。RimWorld 动物是正交侧视 / 正视，不要俯视、不要动态透视；四足动物站立姿态，翅膀收拢（或半张，能装进方形画布）。画布里主体约占 80%。三种驮兽（鹰头马身兽、斧喙鸟、狮鹫）额外再出一套「背上驮着皮质鞍袋」的版本，三视图同样。

| # | 名称 | 体型参照 | 主色 |
|---|---|---|---|
| 13 | 伪龙 | 家猫 | 红褐 |
| 14 | 翼猫 | 家猫 | 虎斑灰褐 + 白羽 |
| 15 | 巨型火甲虫 | 狗 | 黑褐 + 橙红光腺 |
| 16 | 罗斯牛 | 牦牛 | 深褐长毛 |
| 17 | 鹰头马身兽 | 马 | 棕白 |
| 18 | 斧喙鸟 | 鸵鸟 | 灰褐 + 角质喙 |
| 19 | 狮鹫 | 虎~熊 | 金褐 + 白头 |
| 20 | 钩爪恐兽 | 熊 | 灰绿外骨骼 |
| 21 | 相位蜘蛛 | 小马驹 | 暗紫 + 淡蓝相位纹 |
| 22 | 暗斗篷 | 小狗 | 黑紫 |
| 23 | 血蝠蝇 | 鸡 | 暗红褐 |

### 13. 伪龙（小宠物）
- **外形**：家猫大小的小龙，身形修长像蜥蜴 + 猫，大眼睛显得聪明亲人；一对蝙蝠式膜翼，长尾巴末端一根弯曲的毒刺；四只小爪。不要威猛，要可爱的魔宠。
- **配色**：红褐鳞片，腹部浅沙色，翼膜偏橙红半透明，尾刺深紫。
- **注**：名字不能带 Dragon 只是代码问题，外形就是迷你龙。

**中文提示词**
> 一只家猫大小的迷你小龙（DND 伪龙），可爱的魔宠：修长的蜥蜴身体，大眼睛，红褐色鳞片，浅沙色腹部，一对橙红色的蝙蝠式膜翼，长尾巴末端一根弯曲的深紫色毒刺，四只小爪子，站立姿态。RimWorld 游戏动物贴图风格：粗深色描边、平涂加一层体积阴影、低饱和、正交视角。三视图：正面（面朝观众）、右侧面（头朝右）、背面，同一比例对齐。透明背景，不要文字、水印、地面阴影。

**English prompt**
> A house-cat-sized miniature dragon (D&D pseudodragon), a cute familiar: slender lizard body, big intelligent eyes, reddish-brown scales, pale sand belly, a pair of orange-red bat-like membrane wings, a long tail ending in a curved dark purple stinger, four small clawed feet, standing pose. RimWorld animal sprite style: thick dark outline, flat colors with one layer of cel shading, muted palette, orthographic. Turnaround: front (facing viewer), right side (head to the right), back, same scale, aligned. Transparent background, no text, no watermark, no ground shadow.

### 14. 翼猫（小宠物）
- **外形**：普通家猫，肩上长一对收拢的羽翼（鸟翼，不是蝙蝠翼），表情好奇。
- **配色**：灰褐虎斑猫毛；翅膀白色带灰褐斑纹；眼睛金黄。
- **小细节**：眼睛可以稍微亮一点，暗示它能看见隐形的东西。

**中文提示词**
> 一只长着一对羽毛翅膀的家猫（DND 翼猫）：灰褐色虎斑猫，肩上一对收拢的白色鸟翼带灰褐斑纹，金黄色眼睛略微发亮，表情好奇，站立姿态。RimWorld 游戏动物贴图风格：粗深色描边、平涂加一层体积阴影、低饱和、正交视角。三视图：正面（面朝观众）、右侧面（头朝右）、背面，同一比例对齐。透明背景，不要文字、水印、地面阴影。

**English prompt**
> A house cat with a pair of feathered wings (D&D tressym): grey-brown tabby fur, folded white bird wings with grey-brown markings on its shoulders, slightly glowing golden eyes, curious expression, standing pose. RimWorld animal sprite style: thick dark outline, flat colors with one layer of cel shading, muted palette, orthographic. Turnaround: front (facing viewer), right side (head to the right), back, same scale, aligned. Transparent background, no text, no watermark, no ground shadow.

### 15. 巨型火甲虫（家畜）
- **外形**：狗那么大的甲虫，圆胖的甲壳，六条腿，一对短口器（口器尖端带余火的橙红色）；**眼睛上方一对、腹部两侧一对发橙红光的腺体**，像小灯。性情温顺，造型憨一点。
- **配色**：黑褐甲壳带暗铜光泽；光腺橙红；口器尖橙红。

**中文提示词**
> 一只狗那么大的巨型甲虫（DND 火甲虫），憨厚温顺：圆胖的黑褐色甲壳带暗铜光泽，六条腿，一对短口器尖端泛着橙红余火，眼睛上方一对、腹部两侧一对发橙红光的圆形腺体像小灯。RimWorld 游戏动物贴图风格：粗深色描边、平涂加一层体积阴影、低饱和、正交视角，发光只用亮色平涂表现。三视图：正面（面朝观众）、右侧面（头朝右）、背面，同一比例对齐。透明背景，不要文字、水印、地面阴影。

**English prompt**
> A dog-sized giant beetle (D&D giant fire beetle), docile and chunky: round dark brown carapace with a dull copper sheen, six legs, short mandibles with ember-orange tips, a pair of glowing orange-red glands above the eyes and another pair on the sides of the abdomen like little lamps. RimWorld animal sprite style: thick dark outline, flat colors with one layer of cel shading, muted palette, orthographic; show glow with bright flat colors only. Turnaround: front (facing viewer), right side (head to the right), back, same scale, aligned. Transparent background, no text, no watermark, no ground shadow.

### 16. 罗斯牛（家畜）
- **外形**：牦牛 / 麝牛体型，一身蓬乱长毛垂到膝下，一对向前弯的粗角，**三只眼睛**（额头正中第三只），厚重的肩峰。
- **配色**：深褐长毛，口鼻和蹄子灰黑，角骨黄，三只眼睛暗红或琥珀。

**中文提示词**
> 一头牦牛体型的奇幻牛（DND 罗斯牛）：一身蓬乱的深褐色长毛垂到膝下，厚重的肩峰，一对向前弯曲的骨黄色粗角，额头正中多一只眼睛（共三只琥珀色眼睛），灰黑口鼻和蹄子，站立姿态。RimWorld 游戏动物贴图风格：粗深色描边、平涂加一层体积阴影、低饱和、正交视角。三视图：正面（面朝观众）、右侧面（头朝右）、背面，同一比例对齐。透明背景，不要文字、水印、地面阴影。

**English prompt**
> A yak-sized fantasy ox (D&D rothe): shaggy dark brown long fur hanging below the knees, heavy shoulder hump, a pair of thick forward-curving bone-yellow horns, a third eye in the middle of the forehead (three amber eyes total), grey-black muzzle and hooves, standing pose. RimWorld animal sprite style: thick dark outline, flat colors with one layer of cel shading, muted palette, orthographic. Turnaround: front (facing viewer), right side (head to the right), back, same scale, aligned. Transparent background, no text, no watermark, no ground shadow.

### 17. 鹰头马身兽（坐骑 / 驮兽）
- **外形**：前半身是鹰（鹰头、鹰喙、羽毛胸、前腿是鹰爪），后半身是马（马身、马后腿、马尾）；背上一对收拢的大翅膀。比狮鹫温和，表情平和。
- **配色**：棕褐羽毛，头颈白色，马身栗棕，喙和爪暗黄。
- **另需**：鞍袋版（背上两侧挂皮质鞍袋、一条横跨的皮带）。

**中文提示词**
> 一只鹰头马身兽（DND hippogriff），温和的骑乘飞兽：前半身是鹰（白色鹰头、暗黄鹰喙、棕褐羽毛胸、前腿是鹰爪），后半身是栗棕色的马身、马后腿和马尾，背上一对收拢的棕褐大翅膀，表情平和，站立姿态。RimWorld 游戏动物贴图风格：粗深色描边、平涂加一层体积阴影、低饱和、正交视角。三视图：正面（面朝观众）、右侧面（头朝右）、背面，同一比例对齐。再出一套同样三视图、背上驮着两只皮质鞍袋的版本。透明背景，不要文字、水印、地面阴影。

**English prompt**
> A hippogriff (D&D), a gentle riding flyer: front half eagle (white eagle head, dull yellow beak, brown feathered chest, eagle talons as forelegs), back half a chestnut horse body with horse hind legs and tail, a pair of large folded brown wings on the back, calm expression, standing pose. RimWorld animal sprite style: thick dark outline, flat colors with one layer of cel shading, muted palette, orthographic. Turnaround: front (facing viewer), right side (head to the right), back, same scale, aligned. Also a second turnaround of the same animal carrying two leather saddlebags on its back. Transparent background, no text, no watermark, no ground shadow.

### 18. 斧喙鸟（廉价坐骑 / 驮兽）
- **外形**：不会飞的高大走禽，鸵鸟 / 恐鸟体型，长腿、粗脖子，**一张斧刃似的大硬喙**（侧看像一把斧头），翅膀退化成小短翅，脾气急的眼神。
- **配色**：灰褐羽毛，脖子偏浅，喙是暗角质灰带骨白刃口，腿灰黄。
- **另需**：鞍袋版。

**中文提示词**
> 一只不会飞的高大走禽（DND 斧喙鸟），鸵鸟体型：长而粗壮的腿，粗脖子，一张侧看像斧刃的巨大硬喙（暗灰角质、刃口骨白），退化的小短翅，灰褐羽毛，脖子颜色偏浅，凶巴巴的眼神，站立姿态。RimWorld 游戏动物贴图风格：粗深色描边、平涂加一层体积阴影、低饱和、正交视角。三视图：正面（面朝观众）、右侧面（头朝右）、背面，同一比例对齐。再出一套同样三视图、背上驮着两只皮质鞍袋的版本。透明背景，不要文字、水印、地面阴影。

**English prompt**
> A tall flightless bird (D&D axe beak), ostrich-sized: long sturdy legs, thick neck, a huge hard beak shaped like an axe blade in profile (dark grey horn with a bone-white cutting edge), small stubby vestigial wings, grey-brown feathers with a lighter neck, ill-tempered eyes, standing pose. RimWorld animal sprite style: thick dark outline, flat colors with one layer of cel shading, muted palette, orthographic. Turnaround: front (facing viewer), right side (head to the right), back, same scale, aligned. Also a second turnaround carrying two leather saddlebags on its back. Transparent background, no text, no watermark, no ground shadow.

### 19. 狮鹫（高阶坐骑 / 掠食者）
- **外形**：鹰首、鹰翼、鹰爪前腿，狮子的身体和后腿、狮尾（尾端一撮毛）；比鹰头马身兽更壮、更凶，肩膀肌肉厚，翅膀更大。
- **配色**：白色鹰头带金褐斑，羽毛金褐，狮身沙金色，喙爪暗黄。
- **另需**：鞍袋版。

**中文提示词**
> 一只威猛的狮鹫（DND griffon）：白色鹰头带金褐斑纹、锐利的暗黄鹰喙，前腿是鹰爪，金褐色羽毛胸和一对收拢的大翅膀，后半身是沙金色的狮子身体、狮子后腿和尾端带一撮毛的狮尾，肩部肌肉厚实，凶猛的眼神，站立姿态。RimWorld 游戏动物贴图风格：粗深色描边、平涂加一层体积阴影、低饱和、正交视角。三视图：正面（面朝观众）、右侧面（头朝右）、背面，同一比例对齐。再出一套同样三视图、背上驮着两只皮质鞍袋的版本。透明背景，不要文字、水印、地面阴影。

**English prompt**
> A fierce griffon (D&D): white eagle head with golden-brown markings and a sharp dull-yellow beak, eagle talons as forelegs, golden-brown feathered chest and a pair of large folded wings, back half a sandy-gold lion body with lion hind legs and a tufted lion tail, heavily muscled shoulders, fierce eyes, standing pose. RimWorld animal sprite style: thick dark outline, flat colors with one layer of cel shading, muted palette, orthographic. Turnaround: front (facing viewer), right side (head to the right), back, same scale, aligned. Also a second turnaround carrying two leather saddlebags on its back. Transparent background, no text, no watermark, no ground shadow.

### 20. 钩爪恐兽（敌对掠食者）
- **外形**：来自幽暗地域的高大怪物，直立或半直立的两足姿态，秃鹫似的头（光秃、长弯喙），全身硬外骨骼分节，**两条前臂末端是巨大的骨钩**（像镰刀弯钩），粗短后腿。
- **配色**：灰绿 / 石灰色外骨骼，骨钩骨白偏黄，眼睛小而暗红。

**中文提示词**
> 一只来自地底的高大怪物（DND 钩爪恐兽）：半直立的两足姿态，秃鹫一样光秃的头和长弯喙，全身分节的灰绿色坚硬外骨骼，两条前臂末端是巨大的骨白色镰刀状弯钩，粗短有力的后腿，小小的暗红眼睛，凶恶。RimWorld 游戏动物贴图风格：粗深色描边、平涂加一层体积阴影、低饱和、正交视角。三视图：正面（面朝观众）、右侧面（头朝右）、背面，同一比例对齐。透明背景，不要文字、水印、地面阴影。

**English prompt**
> A tall underground monster (D&D hook horror): semi-upright bipedal stance, a bald vulture-like head with a long hooked beak, segmented grey-green hard exoskeleton, both forearms ending in huge bone-white sickle-shaped hooks, short powerful hind legs, small dark red eyes, menacing. RimWorld animal sprite style: thick dark outline, flat colors with one layer of cel shading, muted palette, orthographic. Turnaround: front (facing viewer), right side (head to the right), back, same scale, aligned. Transparent background, no text, no watermark, no ground shadow.

### 21. 相位蜘蛛（敌对掠食者）
- **外形**：马驹大小的蜘蛛，八条细长腿，前端一对大毒牙；身体上有一层**半透明的淡蓝相位纹**（像身体一部分在别的位面），边缘可以有一点虚化——但要用平涂表现，不要特效光晕（游戏里贴图边缘要干净）。
- **配色**：暗紫黑甲壳，腹部淡蓝相位斑纹，眼睛一簇淡蓝。

**中文提示词**
> 一只小马驹大小的巨型蜘蛛（DND 相位蜘蛛）：八条细长的腿，前端一对大毒牙，暗紫黑色甲壳，腹部和腿上有淡蓝色像裂隙一样的相位斑纹，一簇淡蓝色眼睛，边缘干净（不要光晕和粒子特效）。RimWorld 游戏动物贴图风格：粗深色描边、平涂加一层体积阴影、低饱和、正交视角。三视图：正面（面朝观众）、右侧面（头朝右）、背面，同一比例对齐。透明背景，不要文字、水印、地面阴影。

**English prompt**
> A pony-sized giant spider (D&D phase spider): eight long thin legs, a pair of large venom fangs, dark purple-black carapace, pale blue rift-like phase markings on the abdomen and legs, a cluster of pale blue eyes, clean edges (no glow halo or particle effects). RimWorld animal sprite style: thick dark outline, flat colors with one layer of cel shading, muted palette, orthographic. Turnaround: front (facing viewer), right side (head to the right), back, same scale, aligned. Transparent background, no text, no watermark, no ground shadow.

### 22. 暗斗篷（敌对伏击者）
- **外形**：像一块黑色斗篷的乌贼状怪物——一顶钟形 / 斗篷形的身体，边缘是膜翼裙边，下面垂着一圈短触须；地上时触须着地、斗篷半张。没有明显的脸，只有一对小眼睛。
- **配色**：黑紫，膜翼内侧暗红紫，触须尖略浅。

**中文提示词**
> 一只像黑色斗篷的乌贼状怪物（DND 暗斗篷）：钟形斗篷一样的身体，边缘是一圈膜翼裙边半张着，身体下面垂着一圈短触须着地，没有明显的脸、只有一对小眼睛，黑紫色，膜翼内侧暗红紫色。RimWorld 游戏动物贴图风格：粗深色描边、平涂加一层体积阴影、低饱和、正交视角。三视图：正面（面朝观众）、右侧面（朝右）、背面，同一比例对齐。透明背景，不要文字、水印、地面阴影。

**English prompt**
> A squid-like monster shaped like a black cloak (D&D darkmantle): bell-shaped cloak-like body with a membranous skirt rim half spread, a ring of short tentacles underneath resting on the ground, no obvious face, just a pair of small eyes, black-purple with dark reddish-purple inner membranes. RimWorld animal sprite style: thick dark outline, flat colors with one layer of cel shading, muted palette, orthographic. Turnaround: front (facing viewer), right side (facing right), back, same scale, aligned. Transparent background, no text, no watermark, no ground shadow.

### 23. 血蝠蝇（敌对虫群）
- **外形**：鸡大小（游戏里很小）的怪物，蝙蝠翅膀 + 毛茸茸的小身体 + 蚊子一样的长针状口器，四条细爪腿。轮廓要简单粗大，缩到很小也认得出：翅膀张开、长口器朝前。
- **配色**：暗红褐毛，翼膜暗肉色，口器暗红，腹部可以略鼓、透一点血色。

**中文提示词**
> 一只鸡大小的吸血小怪物（DND 血蝠蝇）：毛茸茸的暗红褐色小身体，一对张开的暗肉色蝙蝠翅膀，像蚊子一样朝前伸的长针状暗红口器，四条细细的爪腿，腹部略鼓透出血色。轮廓简单粗大，缩小后也能认出。RimWorld 游戏动物贴图风格：粗深色描边、平涂加一层体积阴影、低饱和、正交视角。三视图：正面（面朝观众）、右侧面（头朝右）、背面，同一比例对齐。透明背景，不要文字、水印、地面阴影。

**English prompt**
> A chicken-sized bloodsucking critter (D&D stirge): small furry dark reddish-brown body, a pair of spread flesh-toned bat wings, a long needle-like dark red mosquito proboscis pointing forward, four thin clawed legs, slightly swollen blood-tinted abdomen. Simple bold silhouette that reads at small size. RimWorld animal sprite style: thick dark outline, flat colors with one layer of cel shading, muted palette, orthographic. Turnaround: front (facing viewer), right side (head to the right), back, same scale, aligned. Transparent background, no text, no watermark, no ground shadow.

---

## 五、出图后发回来时

- 每件发三视图（驮兽另加鞍袋版），原图分辨率越高越好，不要压缩成 JPG（PNG 最好）。
- 不满意的部位直接标注（例如「肩甲再大一点」），我们处理时一起改。
- 我们这边会做：抠图 → 统一描边粗细和饱和度 → 缩到 256 → 身甲拉伸出 5 种体型 → 做可染色遮罩 → 覆盖进 `Textures/LP/PowerArmor/**` 和 `Textures/Things/Pawn/Animal/LP_BeastsDND2/**`，换之前先备份现有贴图。
- 动力甲部分已按 2026-11-08 第七版贴图重写；Def 描述已同步。
