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

## 二、奥能动力甲（1.126.0）

共同轮廓（按 Peter 给的 40K 重甲参考图）：**超大圆顶肩甲**（每侧比身宽多出约 1/3，宽边滚边 + 一圈铆钉，下沿再垂一层甲片）、方正厚胸甲、分节腰甲、粗腿甲 + 圆徽护膝、满身铆钉；胸口一颗嵌在黄铜框里的蓝色奥能核心（发柔和蓝光）；背后推进管或核心背包。整体是「侏儒奇械师 + 耐瑟瑞尔奥术」的秘铸骑士，不是机械科幻：用黄铜、铆钉、符文代替电线和屏幕。

### 1. 奥械侍从甲（入门档，参考图③）
- **外形**：最朴素的一档。圆顶大肩甲（单层，没有尖刺），方胸甲上两排铆钉，胸口小号蓝色核心（黄铜方框），背后两支短粗推进管。
- **配色**：甲色主体做旧铁灰；黄铜色铆钉和核心框；推进管口发暗。
- **材质 / 细节**：工地式做旧——刮痕、凹坑、掉漆露出底下的暗铁；肩甲上一块警示斜纹板（纹章色 + 黑色斜条）。
- **徽记**：左肩小小的齿轮印（侏儒工坊标）。

**中文提示词**
> 一套厚重的奇幻动力盔甲，DND 侏儒工坊制造的入门侍从款，只画盔甲本身、不画头和皮肤（像穿在隐形人身上，领口空着）。超大的圆顶肩甲带宽滚边和一圈铆钉，方正厚胸甲上两排铆钉，胸口一颗嵌在黄铜方框里的发蓝光的奥术核心，分节腰甲，粗壮腿甲配圆形护膝，背后两支短粗的推进管。做旧铁灰色金属，有刮痕凹坑和掉漆，肩上一块黄黑警示斜纹板，左肩一个小齿轮徽记。RimWorld 游戏贴图风格：粗深色描边、平涂加一层体积阴影、低饱和、正交视角、无透视。三视图并排：正面、右侧面、背面，同一比例对齐。透明背景，不要文字、水印和地面阴影，不要任何战锤40K标志。

**English prompt**
> Heavy fantasy power armor, entry-level "squire" model built by D&D gnome artificers. Armor only, no head or skin (as if worn by an invisible person, empty neck opening). Oversized rounded pauldrons with thick rims and rivet rows, boxy chest plate with two rows of rivets, a glowing blue arcane core set in a brass square frame on the chest, segmented waist, thick leg armor with round knee guards, two short stubby thrusters on the back. Weathered iron-grey metal with scratches, dents and chipped paint, a yellow-and-black hazard stripe plate on one shoulder, small gear emblem on the left pauldron. RimWorld game sprite style: thick dark outline, flat colors with one layer of cel shading, muted low saturation, orthographic, no perspective. Turnaround sheet: front, right side, back, same scale and aligned. Transparent background, no text, no watermark, no ground shadow, no Warhammer 40K insignia.

### 2. 奥械侍从盔（配侍从甲）
- **外形**（重画方向）：呼吸管面罩盔——圆顶全盔，脸前两只圆形镜片（发淡蓝光），下面一块格栅口罩，两根软管从口罩绕到后脑。
- **配色**：同侍从甲做旧铁灰；镜框和软管接头黄铜色；软管深橡胶色。
- **注**：Def 里旧描述写的是「一条横贯的蓝色目镜缝」，重画时改成了双圆镜，下次改描述时同步。

**中文提示词**
> 奇幻动力盔甲的头盔，侏儒工坊入门款：圆顶全盔，脸前两只发淡蓝光的圆形镜片，下方一块格栅呼吸口罩，两根黑色软管从口罩绕到后脑，黄铜镜框和接头，做旧铁灰色金属带刮痕和铆钉。只画头盔，不画脖子和人。RimWorld 游戏贴图风格：粗深色描边、平涂加一层阴影、低饱和、正交视角。三视图：正面、右侧面、背面，同一比例。透明背景，不要文字水印，不要战锤40K标志。

**English prompt**
> Fantasy power-armor helmet, gnome artificer entry model: rounded full helm, two round faintly glowing blue goggle lenses, a grilled rebreather mask below, two black hoses running from the mask to the back of the helm, brass lens rims and fittings, weathered iron-grey metal with scratches and rivets. Helmet only, no neck or person. RimWorld sprite style: thick dark outline, flat colors with one layer of shading, muted palette, orthographic. Turnaround: front, right side, back, same scale. Transparent background, no text, no watermark, no Warhammer 40K insignia.

### 3. 秘铸骑士动力甲（主力档，参考图②）
- **外形**：标准轮廓里最「骑士」的一件。双层圆顶大肩甲，胸甲厚而方，腰前一片铆钉挂帘（纹章色布面 + 金属铆钉），圆徽护膝，背后两支推进管。
- **配色**：甲色主体宝蓝（游戏默认，可染色）；金色宽滚边、金铆钉；挂帘纹章色（暗红）。
- **材质**：秘银 + 精金，干净的打磨甲面，不做旧。
- **徽记**：胸前 DND 展翼太阳浮雕（核心就在太阳中心）；护膝上小圆徽。

**中文提示词**
> 一套厚重的奇幻骑士动力盔甲，秘银精金秘铸，只画盔甲、不画头和皮肤（像穿在隐形人身上，领口空着）。双层超大圆顶肩甲，宽宽的金色滚边和金铆钉，方正厚胸甲中央一个展翼太阳浮雕，太阳中心嵌着发蓝光的奥术核心，分节腰甲，腰前垂一片暗红色带铆钉的纹章挂帘，粗腿甲和圆徽护膝，背后两支推进管。甲面宝蓝色、干净打磨。RimWorld 游戏贴图风格：粗深色描边、平涂加一层体积阴影、低饱和、正交视角。三视图并排：正面、右侧面、背面，同一比例对齐。透明背景，不要文字、水印、地面阴影，不要任何战锤40K标志。

**English prompt**
> Heavy fantasy knight power armor forged from mithral and adamantine. Armor only, no head or skin (invisible wearer, empty neck opening). Double-layered oversized rounded pauldrons with wide gold trim and gold rivets, boxy thick breastplate with a winged-sun relief in the center, a glowing blue arcane core set in the sun's center, segmented waist, a dark red riveted heraldic tabard flap hanging in front, thick greaves with round medallion knee guards, two thrusters on the back. Royal blue polished plates. RimWorld sprite style: thick dark outline, flat colors with one layer of cel shading, muted palette, orthographic. Turnaround sheet: front, right side, back, aligned. Transparent background, no text, no watermark, no ground shadow, no Warhammer 40K insignia.

### 4. 秘铸骑士盔（骑士 / 三种变体通用）
- **外形**（重画方向）：鬃冠骑士盔——前突的楔形面甲，T 形目缝（缝里透淡蓝光），盔顶一条纹章色短鬃冠，两颊铆钉。
- **配色**：甲色主体宝蓝（可染色）；面甲金色；鬃冠暗红。
- **注**：Def 旧描述是「十字形目镜」，重画改成 T 形目缝。

**中文提示词**
> 奇幻骑士动力盔甲的头盔：前突的楔形金色面甲，T 形目缝透出淡蓝光，盔体宝蓝色，盔顶一条暗红色短鬃冠，两颊各一排铆钉，金色滚边。只画头盔，不画脖子和人。RimWorld 游戏贴图风格：粗深色描边、平涂加一层阴影、低饱和、正交视角。三视图：正面、右侧面、背面，同一比例。透明背景，不要文字水印，不要战锤40K标志。

**English prompt**
> Fantasy knight power-armor helmet: forward-jutting wedge-shaped gold faceplate with a T-shaped visor slit glowing faint blue, royal blue helm shell, short dark red crest along the top, rivet rows on the cheeks, gold trim. Helmet only, no neck or person. RimWorld sprite style: thick dark outline, flat colors with one layer of shading, muted palette, orthographic. Turnaround: front, right side, back, same scale. Transparent background, no text, no watermark, no Warhammer 40K insignia.

### 5. 圣誓骑士动力甲（圣武士变体，参考图①）
- **外形**：骑士轮廓外罩一件破旧褐色长袍——袍子从腰下开衩成几片下摆，背后有一个卷起的兜帽；胸甲做成骨白色的肋骨浮雕；挂着羊皮纸誓约条（红蜡封）和几段铁链；宽皮带；**头后方一圈铜色尖刺光环**（光环画在身甲贴图里，位置在头的后面）。
- **配色**：甲色主体深黑（可染色）；骨白肋骨；褐袍；羊皮纸米黄 + 红蜡；铜色光环。
- **徽记**：誓约条上用印章图形（八芒旭日小印）代替文字。
- **注**：Def 旧描述是「亮银甲面镶金边、八芒旭日」，重画走了参考图①的暗色朝圣骑士方向，下次改描述时同步。

**中文提示词**
> 一套厚重的奇幻圣武士动力盔甲，只画盔甲和衣物、不画头和皮肤（像穿在隐形人身上，领口空着）。超大圆顶肩甲带铆钉滚边，深黑色甲面，胸甲是骨白色肋骨浮雕，外罩一件破旧的褐色长袍，腰下开衩成几片下摆，背后有卷起的兜帽，宽皮带，身上挂着几条带红色蜡封的羊皮纸誓约条（只画印章图案，不要文字）和铁链，头部后方立着一圈铜色尖刺光环。RimWorld 游戏贴图风格：粗深色描边、平涂加一层体积阴影、低饱和、正交视角。三视图并排：正面、右侧面、背面，同一比例对齐。透明背景，不要文字、水印、地面阴影，不要任何战锤40K标志。

**English prompt**
> Heavy fantasy paladin power armor. Armor and cloth only, no head or skin (invisible wearer, empty neck opening). Oversized rounded riveted pauldrons, black armor plates, bone-white ribcage relief on the breastplate, a tattered brown robe over it split into several tails below the waist with a rolled hood on the back, wide leather belt, parchment oath strips with red wax seals (seal symbols only, no writing) and iron chains hanging from the armor, a copper spiked halo standing behind the head. RimWorld sprite style: thick dark outline, flat colors with one layer of cel shading, muted palette, orthographic. Turnaround sheet: front, right side, back, aligned. Transparent background, no text, no watermark, no ground shadow, no Warhammer 40K insignia.

### 6. 秘法炮手动力甲（法师变体）
- **外形**：骑士轮廓，**没有推进管**；背后一颗大奥能核心外套一个旋转的符文环；两肩各架一根导能晶管（淡紫水晶）；胸甲、肩甲上刻发光的符文线；手甲接着胸口核心的导能线。
- **配色**：甲色主体深紫蓝（可染色）；符文线和晶体淡青紫光；黄铜框。
- **徽记**：胸前一颗八角星辰 + 耐瑟瑞尔符文。

**中文提示词**
> 一套厚重的奇幻战斗法师动力盔甲，只画盔甲、不画头和皮肤（像穿在隐形人身上，领口空着）。超大圆顶肩甲带铆钉滚边，每侧肩上架一根淡紫色导能水晶管，深紫蓝色甲面，胸甲和肩甲上刻着发青紫光的符文线条，胸前一颗八角星辰徽记，胸口奥术核心连着导线通到手甲，背后一颗大奥术核心外面套一个符文环，没有推进器。黄铜框和铆钉。RimWorld 游戏贴图风格：粗深色描边、平涂加一层体积阴影、低饱和、正交视角，发光部分只用浅色平涂表现。三视图并排：正面、右侧面、背面，同一比例对齐。透明背景，不要文字、水印、地面阴影，不要任何战锤40K标志。

**English prompt**
> Heavy fantasy battle-mage power armor. Armor only, no head or skin (invisible wearer, empty neck opening). Oversized rounded riveted pauldrons each carrying a pale violet focusing-crystal tube, deep purple-blue plates engraved with softly glowing cyan-violet rune lines on chest and shoulders, an eight-pointed star emblem on the chest, conduits running from the chest core to the gauntlets, a large arcane core on the back ringed by a rune circle, no thrusters. Brass frames and rivets. RimWorld sprite style: thick dark outline, flat colors with one layer of cel shading, muted palette, orthographic; show glow with light flat colors only. Turnaround sheet: front, right side, back, aligned. Transparent background, no text, no watermark, no ground shadow, no Warhammer 40K insignia.

### 7. 奇械动力甲（器械师变体，参考图③）
- **外形**：侍从甲同款粗犷轮廓，但肩甲上加一排短尖钉；胸前一个大齿轮，核心嵌在齿轮中心；背后一台双联核心背包（两颗核心并排、外露管线）；腰间挂工具袋、扳手、备用线圈。
- **配色**：甲色主体青铜色（可染色）；黄铜齿轮；皮质工具袋。
- **徽记**：胸前齿轮本身就是徽记（侏儒工坊）。

**中文提示词**
> 一套厚重的奇幻器械师动力盔甲，只画盔甲、不画头和皮肤（像穿在隐形人身上，领口空着）。超大圆顶肩甲带一排短尖钉和铆钉滚边，青铜色甲面，胸前一个大黄铜齿轮，齿轮中心嵌着发蓝光的奥术核心，背后一台双联核心背包（两颗核心并排、露着铜管），腰间挂满皮质工具袋、扳手和铜线圈，粗腿甲和圆护膝。略带使用痕迹。RimWorld 游戏贴图风格：粗深色描边、平涂加一层体积阴影、低饱和、正交视角。三视图并排：正面、右侧面、背面，同一比例对齐。透明背景，不要文字、水印、地面阴影，不要任何战锤40K标志。

**English prompt**
> Heavy fantasy artificer power armor. Armor only, no head or skin (invisible wearer, empty neck opening). Oversized rounded pauldrons with a row of short spikes and riveted rims, bronze plates, a large brass gear on the chest with a glowing blue arcane core in its hub, a twin-core backpack (two cores side by side with exposed copper pipes), leather tool pouches, wrenches and copper coils hanging from the belt, thick greaves with round knee guards, lightly worn. RimWorld sprite style: thick dark outline, flat colors with one layer of cel shading, muted palette, orthographic. Turnaround sheet: front, right side, back, aligned. Transparent background, no text, no watermark, no ground shadow, no Warhammer 40K insignia.

### 8. 誓约堡垒重甲（最高档）
- **外形**：所有动力甲里最宽最厚——躯干比别的甲宽约 24%；肩甲不是圆顶，而是**方形塔盾式肩甲**（像两面竖着的小塔盾，金边，顶上各三根尖刺）；胸甲上并排三颗奥能核心；腿甲粗得像城柱；腰前一段铁链，腰下褐色布袍下摆。
- **配色**：甲色主体深钢灰（可染色）；金边；褐袍；三颗蓝核心。
- **徽记**：两面塔盾肩甲上各一个盾形纹章（城垛图案，代表「移动城墙」）。

**中文提示词**
> 一套极其厚重宽大的奇幻堡垒动力盔甲，比普通盔甲宽出一圈，只画盔甲、不画头和皮肤（像穿在隐形人身上，领口空着）。两边肩甲是方形塔盾式（像竖着的小塔盾，金色滚边，顶上各三根尖刺，盾面有城垛纹章），加宽加厚的胸甲上并排三颗发蓝光的奥术核心，满身铆钉，腰前挂一段铁链，腰下褐色布袍下摆，腿甲粗得像城柱。深钢灰色甲面。RimWorld 游戏贴图风格：粗深色描边、平涂加一层体积阴影、低饱和、正交视角。三视图并排：正面、右侧面、背面，同一比例对齐。透明背景，不要文字、水印、地面阴影，不要任何战锤40K标志。

**English prompt**
> Extremely massive, extra-wide fantasy "bastion" power armor, noticeably wider than normal armor. Armor only, no head or skin (invisible wearer, empty neck opening). Square tower-shield pauldrons (like small upright tower shields with gold trim, three spikes on top of each, a battlement heraldic emblem on each face), widened thick breastplate with three glowing blue arcane cores side by side, rivets everywhere, an iron chain across the waist, brown cloth tails below the waist, pillar-thick leg armor. Dark steel-grey plates. RimWorld sprite style: thick dark outline, flat colors with one layer of cel shading, muted palette, orthographic. Turnaround sheet: front, right side, back, aligned. Transparent background, no text, no watermark, no ground shadow, no Warhammer 40K insignia.

### 9. 堡垒巨盔（配堡垒甲，体型 ≥1）
- **外形**（重画方向）：骷髅面盔——方形厚盔，正面铸成骨白色骷髅面（眼窝里一条窄窄的蓝色光缝），两侧护颊翼，盔后一圈铜色尖刺光环。下沿和肩甲连成一体的厚颈甲。
- **配色**：盔体深钢灰；骷髅面骨白；铜色光环；眼缝淡蓝。
- **注**：骷髅面是 DND 死亡守望 / 誓约骑士的意象，画成普通人类骷髅即可，不要加翅膀（避免像 40K 的骷髅翼）。

**中文提示词**
> 奇幻堡垒动力盔甲的巨型头盔：方形厚重盔体，深钢灰色，正面铸成骨白色的骷髅面，眼窝里一条窄窄的蓝色光缝，两侧伸出护颊翼，下沿是厚颈甲，盔后立一圈铜色尖刺光环，铆钉滚边。不要翅膀。只画头盔，不画人。RimWorld 游戏贴图风格：粗深色描边、平涂加一层阴影、低饱和、正交视角。三视图：正面、右侧面、背面，同一比例。透明背景，不要文字水印，不要战锤40K标志。

**English prompt**
> Massive fantasy bastion power-armor helmet: square heavy dark steel-grey helm, front cast as a bone-white skull face with a narrow glowing blue slit in the eye sockets, cheek-guard wings on both sides, thick gorget at the bottom, a copper spiked halo behind the helm, riveted rims. No wings on the skull. Helmet only, no person. RimWorld sprite style: thick dark outline, flat colors with one layer of shading, muted palette, orthographic. Turnaround: front, right side, back, same scale. Transparent background, no text, no watermark, no Warhammer 40K insignia.

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
- 两处 Def 描述和当前贴图不一致（侍从盔 / 骑士盔的目镜写法、圣誓甲的亮银配色），新图定下来后一并改描述。
