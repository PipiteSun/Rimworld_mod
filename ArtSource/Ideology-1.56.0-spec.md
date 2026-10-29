# DND边缘大陆 1.56.0「文化之神」实现要求（文化 DLC 第一批）

写于 2026-10-27。Peter 03:33 把"神殿与神的相关文化内容"排为第 4 项，协调线程给定版本 **1.56.0**。来源构思：[DND文化DLC构思.md](DND文化DLC构思.md)（第一批 + 第三批里的神殿房间和神像）。Peter 已说过所有取舍由线程决定，本文所有决定都已经定了，实现时不再问。

**设备顺序**：1.52.1 → 1.53.0 → 1.54.0 → 1.55.0 之后才轮到本版。开工前等协调线程说"设备空闲"并给出最新备份标签；About.xml 必须是协调线程告知的那个版本。

---

## 零、范围一览

| 部分 | 内容 |
|---|---|
| 框架 | 新文件夹 `Bridges/Ideology`（只放 Defs、贴图、补丁），C# 写在**主程序集**里，所有入口都用 `ModsConfig.IdeologyActive` 挡住 |
| 模因 | 16 个：9 位可信奉神 + 2 位 NPC 专用神 + 众神殿、不跪之人、魔网至上、永夜不朽、巨人位阶 |
| 戒律 | 7 个新议题：亡灵观、巨人观、魔法观、种族观、吞噬大脑、成神观、供奉 |
| 神眷联动 | 神祇模因锁定守护神；神眷影响确信度；改宗修正；祈祷乘确信度；信徒特性跟着改宗 |
| 仪式 | 9 场神祇庆典（固定日期）+ 灰袍葬礼 |
| 角色 | 神官（新的专家角色）；道德导师在神祇文化里改名"大祭司"，并在圣坛上多一个"求取神谕"按钮 |
| 神殿与神 | 神殿房间、10 座神像（9 神 + 众神像）、神赐遗物作为文化圣物 |
| 派系 | 9 个 DND 派系补上 DND 模因和戒律；设置里加"重建 DND 派系文化"按钮（给旧存档用） |
| 杂项 | 新生儿补文化；设置分组"文化" |
| **不做**（留给后续批次） | 加冕礼、卡苏斯之夜、龙之献礼、乌宁格之争、永夜之礼；龙之教团、灰烬之誓、自然之道模因；龙观、契约观；龙祭司等其他角色；文化风格、文化服装、武器类别；模因事件；深渊和契约的文化内容 |

---

## 一、框架

### 1.1 加载

- `LoadFolders.xml` 在 `v1.7` 里加一行（放在 VPE 和 HAR 之间都行）：
  `<li IfModActive="ludeon.rimworld.ideology">Bridges/Ideology</li>`
- `Bridges/Ideology/` 下面放：`Defs/`、`Patches/`、`Textures/`。**不建独立 dll**：文化 DLC 的类型（`Ideo`、`Precept`、`RitualOutcomeEffectWorker` 等）本来就在 Assembly-CSharp 里，主程序集可以直接引用，所以 Build.ps1 不用改。（这一点改了构思里"单独做 dll"的说法。）
- C# 文件放在主 `Source/`：`Ideo.cs`（入口、同步、设置）、`IdeoPrecepts.cs`（心情 worker、StatPart、历史事件）、`IdeoRituals.cs`（庆典和葬礼的结果 worker、日期）、`IdeoTemple.cs`（神殿房间、神像）、`IdeoChecks.cs`。
- **静态入口** `IdeoBridge.Active => ModsConfig.IdeologyActive && DefDatabase<MemeDef>.GetNamedSilentFail("LP_Meme_Pantheon") != null`，算一次后缓存。每个对外方法第一行都是 `if(!Active)return;`（或者返回中性值 1f / null）。
- Harmony：对文化类型的补丁放在单独的 `IdeoPatches.Apply(harmony)` 里，**只在 `Active` 时调用**，不要用 `[HarmonyPatch]` 属性自动打。

### 1.2 `LP_IdeoDeityExtension`（DefModExtension，挂在神祇模因上）

```
deity            LP_DeityDef   对应的神（如 LP_Deity_Lathander）
deityTitle       string        文化窗口里显示的神格称号
festivalQuadrum  Quadrum       庆典季
festivalDay      int           庆典日（1～15）
festivalPrecept  PreceptDef    这位神的庆典戒律（第五节）
statue           ThingDef      这位神的神像（第七节）
```

众神殿模因也挂一个，`deity` 留空，`statue = LP_Statue_Pantheon`。

辅助方法（`IdeoBridge`）：
- `LP_DeityDef DeityOf(Ideo ideo)`：文化里带扩展且 `deity` 不为空的那个模因的神，没有就 null。**按 `ideo` 缓存**，在文化的模因变化时清缓存（1.5 节的每 2500 tick 检查顺便刷新）。
- `bool HasPantheon(Ideo ideo)`、`bool HasMeme(Pawn p, MemeDef m)`（读 `p.Ideo`，同样走缓存）。
- `Ideo PlayerIdeo => Faction.OfPlayer.ideos?.PrimaryIdeo`。

### 1.3 历史事件（主程序集里原有的地方发出）

新文件 `Bridges/Ideology/Defs/Ideo_HistoryEvents.xml` 定义 `HistoryEventDef`（都带 `doer` 参数）。主程序集加一个**不依赖文化**的静态方法：

```
public static class IdeoEvents {
  public static void Record(string defName, Pawn doer)  // 没装文化或找不到 def 就直接 return
}
```

在下面这些**已有方法的末尾**各加一行 `IdeoEvents.Record(...)`（实现时按设备上实际方法名找；找不到挂点就删掉那一条，报告写明）：

| 历史事件 | 挂在哪 | 每人限频 |
|---|---|---|
| `LP_HE_LichRevived` 巫妖复生 | 命匣复活 | 无 |
| `LP_HE_SoulDrained` 汲取灵魂 | 巫妖汲取灵魂 | 无 |
| `LP_HE_ConstructRaised` 造出死灵造物 | 死灵造物胚体完成 | 无 |
| `LP_HE_BrainDevoured` 吞噬大脑 | 夺心魔吞噬大脑 | 无 |
| `LP_HE_SpellCast` 施放法术 | 模组自己的职业能力 / 神术 / 卷轴施放（VPE 桥里的路径能力也调用同一个方法） | 同一人每 2500 tick 最多 1 次（存在一个 `Dictionary<int,int>` 里，按 thingIDNumber） |
| `LP_HE_DemigodRisen` 半神升级 | 1.40.0 半神等级 +1 的地方 | 无 |
| `LP_HE_GiantKilled` 杀死巨人 | `Pawn.Kill` 后缀，死者是巨躯规则判定的巨人且有 `dinfo.Instigator` 是人形 | 无 |

（龙相关、深渊、契约的历史事件留给后续批次。）

### 1.4 文化 ↔ 守护神（改 `Faith.cs` 两处）

规则：
1. 玩家主文化有**神祇模因**（`DeityOf(PlayerIdeo) != null`）且设置"文化锁定守护神"开着：
   - 守护神就是那位神。`FaithUtility.CannotSetPatron` 开头加：`var lock=IdeoBridge.PatronLockReason(god); if(lock!=null) return lock;` → 返回"守护神由文化「X」决定"。
   - 圣坛"选择守护神"按钮仍然显示，灰掉写这个原因。
2. 主文化模因变了（重塑、换主文化）导致神变了：调用 `FaithUtility.SetPatron(新神, ignoreCooldown:true)`，**但旧神只扣 15**（不是手动更换的 30）。做法：`WorldComponent_Faith.SetPatron` 加一个可选参数 `float penalty=-1`（-1 = 用原来的 `Faith.ChangePenalty`），文化同步时传 15；不设冷却。信件："殖民地正式改奉 X"。
3. 主文化是**众神殿**且没有主神：守护神自由选；`CannotSetPatron` 里冷却按一半算（`patronCooldownUntil` 写入时用 `ChangeCooldownDays/2`）。
4. 主文化没有任何 DND 模因：完全照 1.39.0，不介入。
5. 主文化的神 `worshippable=false`（伊尔斯维、维拉基斯，只有 NPC 用，玩家选不到这两个模因，正常不会发生）：不锁。

### 1.5 检查节奏（性能要求）

Peter 的第 7 项是"性能检查，不要有重复计算降低 TPS"，所以本版**没有任何每 tick 的逻辑**：
- `WorldComponent_Faith` 里加一个 2500 tick 的检查（如果已经有 rare tick 就并进去）：比较玩家主文化的神和上次缓存的神，变了就走 1.4 第 2 条；同时清 `DeityOf` 缓存。
- 每天一次（已有的 `lastDay` 日结算里调用 `IdeoBridge.Daily()`）：确信度漂移（4.1）、圣物神眷（7.4）。
- 心情 worker 都是 `ThoughtWorker`（原版本来就每 250 tick 左右重算，按 pawn 缓存），内部只读缓存和计数，不扫整张地图；需要计数的（死灵造物数、驯服的狼熊数、巨躯殖民者数）用一个每 2500 tick 刷新的地图级缓存 `IdeoMapCache`（`MapComponent`）。
- StatPart 只读 `pawn.Ideo` 的缓存模因集合和地图时间，不分配。

---

## 二、模因（`Bridges/Ideology/Defs/Ideo_Memes.xml`）

### 2.1 通用写法

- `category = Normal`，`impact = 2`（众神殿、魔网至上、巨人位阶 = 1）。
- 神祇模因和众神殿：`exclusionTags` 带 `LP_DeityMeme`（主神只能一个）；众神殿另带 `LP_PantheonMeme`，**不带** `LP_DeityMeme`，所以众神殿可以和一个主神共存。不跪之人带 `LP_DeityMeme` 和 `LP_PantheonMeme`，三者互斥。
- 结构模因不加限制（原版模因没有限制结构的字段，不硬做）。
- `generalRules` / `symbolPacks`：给每个模因写中文名词（神名、"晨曦""黎明"之类），让文化名字生成得像样；`descriptionMaker` 可以只写一句固定描述。
- `agreeableTraits` / `disagreeableTraits`：用 `LP_Devotee` 对应 degree（洛山达 1 … 维拉基斯 11）、`LP_Atheist`、1.32.0 背景特性（`LP_` 前缀以设备为准）。
- 图标 `iconPath = UI/Memes/LP/<名字>`（第十节贴图）。
- 文化图标：每个模因再配一个 `IdeoIconDef`（`memes` 里写这个模因），白色剪影风格，文化生成时会挑到。
- `randomizationSelectionWeightFactor`：XML 里写 1；启动时（和 `Faith.ApplySettings` 一样的位置）按设置"DND 模因出现在随机 NPC 文化的权重"乘一遍（默认 0.3）。NPC 专用的两个永远是 0。

### 2.2 神祇模因（11 个）

| 模因 | defName | 神 | requireOne（必带戒律） | 同意 | 不同意 | 庆典 |
|---|---|---|---|---|---|---|
| 晨主之光 | `LP_Meme_Lathander` | `LP_Deity_Lathander` | 亡灵观：憎恶 | 信徒（洛山达）、民间英雄 | 信徒（莎尔）、无神者 | 绿草节 |
| 正义之秤 | `LP_Meme_Tyr` | `LP_Deity_Tyr` | 原版 `Execution_RespectedIfGuilty` 或 `Execution_HorribleIfInnocent` 二选一 | 信徒（提尔）、士兵、贵族 | 罪犯、信徒（班恩） | 盾会 |
| 魔网之女 | `LP_Meme_Mystra` | `LP_Deity_Mystra` | 魔法观：尊崇 | 信徒（密斯特拉）、贤者 | 信徒（莎尔） | 魔网之夜 |
| 亡者之判 | `LP_Meme_Kelemvor` | `LP_Deity_Kelemvor` | 亡灵观：憎恶；原版 `Corpses_DontCare` | 信徒（凯兰沃）、侍僧 | — | 亡者之宴 |
| 兽王之猎 | `LP_Meme_Malar` | `LP_Deity_Malar` | — | 信徒（马拉尔）、嗜血 | — | 大猎 |
| 风暴之怒 | `LP_Meme_Talos` | `LP_Deity_Talos` | — | 信徒（塔洛斯）、纵火狂 | 胆小 | 风暴之歌 |
| 黑暗之主 | `LP_Meme_Bane` | `LP_Deity_Bane` | 原版 `Slavery_Honorable` | 信徒（班恩）、贵族 | 善良类、信徒（提尔） | 暴政之日 |
| 夜之女士 | `LP_Meme_Shar` | `LP_Deity_Shar` | 原版 `Darklight_Preferred` | 信徒（莎尔）、局外人 | 信徒（洛山达、密斯特拉） | 无光守夜 |
| 熔炉巨人 | `LP_Meme_Surtr` | `LP_Deity_Surtr` | 巨人观：至上 | 信徒（苏尔特） | — | 熔炉之火 |
| 主脑合一 | `LP_Meme_Ilsvi` | `LP_Deity_Ilsvi` | 吞噬大脑：神圣 | — | — | 无（NPC 专用：`playerCanUse`/编辑器隐藏，见下） |
| 巫妖女王 | `LP_Meme_Vlaakith` | `LP_Deity_Vlaakith` | 亡灵观：神圣 | — | — | 无（NPC 专用） |

- "善良类"特性：原版 `Kind`。"嗜血" `Bloodlust`，"纵火狂" `Pyromaniac`，"胆小" = 原版 `Nerves` 负档（-2）。
- **NPC 专用**：原版 `MemeDef` 如果没有现成的"玩家不可选"字段，就用 Harmony 后缀 `IdeoUIUtility`/`Dialog_ChooseMemes` 里列可选模因的方法把这两个过滤掉（实现时找具体方法），并且 `randomizationSelectionWeightFactor = 0`，只通过派系 `forcedMemes` 出现。
- **固定神名**：文化带神祇模因时，文化基础里的神名换成我们的神。Harmony 后缀 `IdeoFoundation_Deity` 生成神祇的方法（实现时找到，原版叫 `GenerateDeities` / `RandomizeDeities` 之类）：第一位神 = `deity.label`，称号 = `deityTitle`，性别按神（洛山达、提尔、凯兰沃、马拉尔、塔洛斯、班恩、苏尔特、伊尔斯维 男；密斯特拉、莎尔、维拉基斯 女）；众神殿时额外加 2 位随机可信奉善神 / 中立神。其他原版随机神删掉。

### 2.3 神祇模因的被动（只看玩家 pawn 自己的文化）

| 神 | 被动 | 写法 |
|---|---|---|
| 洛山达 | 当地时间 5～8 点全局工作速度 +5% | `StatPart` 挂 `WorkSpeedGlobal` |
| 洛山达 | 殖民地每有一个死灵造物 -1 心情，上限 -5 | `ThoughtWorker` + `IdeoMapCache.constructCount` |
| 提尔 | 原版审判仪式和释放囚犯给提尔的神眷再 ×2 | 在 1.39.0 这两个作为的 `Faith.Deed` 调用处乘 `IdeoBridge.DeedFactor("Tyr")` |
| 密斯特拉 | 身上有魔法物品（1.45.0 魔法物品 / 魔法服装）+2 心情"魔网的礼物" | `ThoughtWorker`，用 1.45.0 已有的"是不是魔法物品"判定 |
| 凯兰沃 | 入葬给凯兰沃的神眷再 ×2；看尸体不难受 | 同提尔；看尸体靠必带的原版 `Corpses_DontCare` |
| 马拉尔 | 每只殖民地驯服的狼 / 熊 / 冬狼 `LP_WinterWolf` +1 心情，上限 +3 | `ThoughtWorker` + `IdeoMapCache.sacredBeasts` |
| 塔洛斯 | 雷暴天气 +4 心情"风暴之子" | `ThoughtWorker`，用 `Faith.Storm(map)` |
| 班恩 | 奴隶压制下降速度 ×0.7（奴隶主人派系的主文化是班恩时） | `StatPart` 挂 `SlaveSuppressionFallRate` |
| 莎尔 | 夜里（`Faith.Night`）全局工作速度 +8% | `StatPart` 挂 `WorkSpeedGlobal`（和洛山达的写在同一个 StatPart 里） |
| 苏尔特 | 锻造速度 +10% | `StatPart` 挂 `SmithingSpeed` |

`DeedFactor(key)`：主文化神的 `key` 等于参数时返回 2，否则 1；没装文化返回 1。

### 2.4 非神祇模因（5 个）

| 模因 | defName | requireOne | 同意 / 不同意 | 被动 |
|---|---|---|---|---|
| 众神殿 | `LP_Meme_Pantheon` | — | 同意：任何善 / 中立神的信徒、侍僧、贤者；不同意：无神者 | 祈祷神眷 ×1.25（在 `Faith.PrayerReward` 里乘）；守护神更换冷却减半（1.4 第 3 条）；庆典可以选任何可信奉神（5.4） |
| 不跪之人 | `LP_Meme_Defiant` | — | 同意：无神者；不同意：所有信徒 degree | 全员受神术（敌我）效果 ×0.75（在神术的效果倍率里乘，和 1.39.0 无神者的 ×0.5 取较小值，不叠）；心智抗性 `LP_MindResist` +5%（StatPart）；圣坛对这个文化的人"不受欢迎"：殖民地有圣坛时 -2"讨厌的神龛"（ThoughtWorker） |
| 魔网至上 | `LP_Meme_Weave` | 魔法观：魔网至上 | 同意：贤者、信徒（密斯特拉）；不同意：— | 耐瑟卷轴研读经验 ×1.15（在 1.37/1.42 研读给经验的地方乘 `IdeoBridge.ScrollXpFactor(p)`）；模组施法能力（神术、卷轴、巫妖法术）冷却 ×0.95——**VPE 路径能力不改**（VPE 不归本版管） |
| 永夜不朽 | `LP_Meme_Undeath` | 亡灵观：神圣 | 同意：巫妖异种型的殖民者（不是特性，在 `IdeoBridge` 里用代码加确信度：巫妖在这个文化里每天确信度 +1%）；不同意：信徒（洛山达、凯兰沃） | 巫妖复生时全体 +3"主人归来"（由亡灵观"神圣"的历史事件给出，不另写） |
| 巨人位阶 | `LP_Meme_Ordning` | 巨人观：至上 | 同意：信徒（苏尔特）；不同意：— | 巨躯殖民者对非巨躯殖民者的社交影响 +10%（`StatPart` 挂 `SocialImpact`，只在主文化带本模因时） |

模因对立（原版 `MemeDef` 没有现成的对立表时，用 `GoodwillSituationDef` + worker 读两边主文化）：

| 一方 | 另一方 | 好感 |
|---|---|---|
| 晨主之光 | 夜之女士、永夜不朽 | -30 |
| 正义之秤 | 黑暗之主 | -30 |
| 魔网之女 | 夜之女士 | -30 |
| 亡者之判 | 永夜不朽 | -40 |
| 熔炉巨人 | — | — |

只算一次（取最负的那对），和 1.19.0 种族好感叠加。

---

## 三、戒律（`Ideo_Precepts.xml`）

数字是心情值。"自己做"= `PreceptComp_SelfTookMemoryThought`，"知道"= `PreceptComp_KnowsMemberThought`，"情境"= `ThoughtWorker_Precept` 子类。每个议题的默认档写在第一列加粗。随机生成时（不是派系强制）默认档权重 2，其他档 1。

### 3.1 亡灵观 `LP_Issue_Undead`

| 档 | defName | 巫妖复生 / 造死灵造物 / 汲取灵魂（自己 / 知道） | 情境 |
|---|---|---|---|
| 憎恶 | `LP_Undead_Abhorrent` | -10 / -4；汲取灵魂 -6 / -3 | 殖民地每个死灵造物 -1（上限 -5）；殖民地有巫妖 -3 |
| 警惕 | `LP_Undead_Wary` | -3 / -1 | — |
| **可接受** | `LP_Undead_Acceptable` | 0 | — |
| 神圣 | `LP_Undead_Sacred` | +4 / +2；巫妖复生"知道" +3"主人归来" | 殖民地有命匣 +2"永恒的守护" |

### 3.2 巨人观 `LP_Issue_Giants`

| 档 | defName | 效果 |
|---|---|---|
| 恐惧 | `LP_Giants_Feared` | 殖民地每个巨躯殖民者 -1（上限 -3，巨躯本人不算）；`LP_HE_GiantKilled` 自己 +3 |
| **尊重** | `LP_Giants_Respected` | 无 |
| 至上 | `LP_Giants_Supreme` | 巨躯殖民者 +3"生来伟大"；非巨躯殖民者 -1"渺小"；`LP_HE_GiantKilled` 自己 -5 |

巨躯判定用现有巨躯规则（按种族体型，HAR 也算，见记忆里的巨躯规则）。

### 3.3 魔法观 `LP_Issue_Arcane`

| 档 | defName | `LP_HE_SpellCast` 自己 / 知道 | 情境 |
|---|---|---|---|
| 禁绝 | `LP_Arcane_Forbidden` | -6 / -2 | 自己身上有魔法物品 -3 |
| **谨慎** | `LP_Arcane_Cautious` | -1 / 0 | — |
| 尊崇 | `LP_Arcane_Revered` | +1 / 0 | 殖民地有附魔台（1.47.0）+2 |
| 魔网至上 | `LP_Arcane_Supreme` | +2 / 0 | 不会施法的殖民者 -2"不被魔网眷顾"（"会施法" = 有任何模组施法能力或 VPE 灵能等级 ≥ 1） |

"魔网至上"档只在文化带"魔网至上"模因时能选（`PreceptDef.requiredMemes`）。

### 3.4 种族观 `LP_Issue_Races`

| 档 | defName | 效果 |
|---|---|---|
| 人类至上 | `LP_Races_Human` | 非基准人类殖民者每人 -2；基准人类 +2（和原版 `HumanPrimacy` 模因能共存） |
| **众族共存** | `LP_Races_Many` | 殖民地异种型 ≥ 4 种 +3"众族之城" |
| 本族至上 | `LP_Races_Own` | 和"文化创始者异种型"相同 +2，不同 -1。创始者异种型 = 文化第一次被玩家派系采用时，殖民者里最多的那个异种型，存在 `IdeoBridge` 的存档字典里（key = ideo id） |

异种型计数放 `IdeoMapCache`。

### 3.5 吞噬大脑 `LP_Issue_BrainEating`

| 档 | defName | `LP_HE_BrainDevoured` 自己 / 知道 | 情境 |
|---|---|---|---|
| 可憎 | `LP_BrainEating_Abhorrent` | -10 / -5 | — |
| **可接受** | `LP_BrainEating_Acceptable` | 0 | — |
| 神圣 | `LP_BrainEating_Sacred` | +6 / +2 | 殖民地有上古脑 +2 |

### 3.6 成神观 `LP_Issue_Apotheosis`

| 档 | defName | 效果 |
|---|---|---|
| 亵渎 | `LP_Apotheosis_Blasphemy` | 殖民地有半神 -4；`LP_HE_DemigodRisen` 知道 -6 |
| **可接受** | `LP_Apotheosis_Acceptable` | 0 |
| 神圣使命 | `LP_Apotheosis_Sacred` | `LP_HE_DemigodRisen` 知道 +6"见证神迹"；半神每日神力的信徒加成里，**同文化成员**也算作信徒（每人 +0.25/天；在 1.40.0 算神力来源的地方加 `IdeoBridge.ExtraDevotees(demigod)`） |

### 3.7 供奉 `LP_Issue_Offerings`

| 档 | defName | 效果 |
|---|---|---|
| **无要求** | `LP_Offerings_None` | — |
| 每季献祭 | `LP_Offerings_Quarterly` | 每季（15 天）殖民地要在圣坛完成至少一次献祭配方（1.39.0 三个配方任一），没完成从第 16 天起 -3"神没有得到供奉"，直到完成；本档下献祭神眷 ×1.5 |
| 血祭 | `LP_Offerings_Blood` | 圣坛多一个按钮"血祭"：选一名囚犯，殖民者把他带到圣坛旁处决（复用原版处决作业，目标格在圣坛 2 格内），完成后守护神神眷 +15；只在守护神是恶神（`alignment == Evil`）时可用，善神 / 中立神时按钮灰掉写原因。不做成配方（配方不能拿囚犯当材料） |

"每季献祭"的计时存在 `WorldComponent_Faith` 新字段 `lastOfferingTick`（献祭完成时写），日结算里判断。

---

## 四、神眷 ↔ 文化

### 4.1 确信度（`IdeoBridge.Daily()`）

对每名玩家殖民者 `p`，若 `p.Ideo == PlayerIdeo` 且 `DeityOf(PlayerIdeo)` 不为空（或众神殿）：

| 条件（看守护神神眷） | 每天确信度 |
|---|---|
| ≥ 80 | +1% |
| ≥ 50 | +0.5% |
| ≤ -30 | -1% |

另：季度显圣赐福开始时全体 +5%，降罚开始时 -5%（在 1.39.0 显圣开始的地方调用 `IdeoBridge.OnManifest(bool blessing)`）；神选仪式完成时 +3%（`Faith.CompleteChosen` 末尾）。用 `p.ideo.OffsetCertainty(x)`。设置"神眷影响确信度"关掉时整条跳过。

### 4.2 祈祷乘确信度

`Faith.PrayerReward` 里 `g *= IdeoBridge.PrayerFactor(p)`：
- 没装文化 / 没有 DND 神祇模因或众神殿：1。
- 否则：`0.5 + p.ideo.Certainty`（确信 100% = ×1.5，20% = ×0.7）；再乘众神殿 ×1.25（2.4）；在神殿里再 ×1.25（7.2）。
- 每日上限 `PrayerDailyCap` 不变。

### 4.3 改宗修正

Harmony 后缀 `InteractionWorker_ConvertIdeoAttempt.CertaintyReduction(Pawn initiator, Pawn recipient)`（实现时确认签名），结果乘下表所有适用项：

| 条件 | 倍率 |
|---|---|
| 接收者是 X 神信徒，发起者的文化主神是 X | ×1.5 |
| 接收者是 X 神信徒，接收者自己的文化主神是 X（别人劝他离开） | ×0.5 |
| 接收者是无神者，发起者文化有神祇模因或众神殿 | ×0.5 |
| 接收者是无神者，发起者文化是不跪之人 | ×1.5 |
| 发起者是神选者 `LP_Chosen` | ×1.3 |
| 发起者是半神 `LP_Demigod` | ×(1 + 0.1 × 等级) |
| 发起者担任神官（第六节） | ×1.2 |

### 4.4 信徒特性跟着改宗

Harmony 后缀 `Pawn_IdeoTracker.SetIdeo(Ideo)`（只对人形、非新生成的 pawn：`!PawnGenerator.IsBeingGenerated(pawn)`）：
- 新文化主神是 Y，pawn 现在是"信徒（X）"且 X ≠ Y：30% 换成"信徒（Y）"（X 与 Y 敌对时 15%），用 `Rand.ChanceSeeded(…, pawn.thingIDNumber ^ GenTicks.TicksGame)`。
- 新文化是不跪之人且 pawn 是信徒：20% 去掉信徒特性。
- 有信件就不发，只在 pawn 的日志里写一行（`PlayLogEntry_Interaction` 不需要，静默即可），报告里说明。

---

## 五、仪式（`Ideo_Rituals.xml`）

### 5.1 9 场神祇庆典

- 每位可信奉神一个 `PreceptDef`（`ParentName="FestivalBase"`，`issue = Ritual`，`preceptClass = Precept_Ritual`），`ritualPatternBase` = 每神一个 `RitualPatternDef`（`ParentName="CelebrationPartyChat"`），改：
  - `ritualOutcomeEffect` = 每神一个 `RitualOutcomeEffectDef`，`workerClass = LichPhylactery.RitualOutcomeEffectWorker_LPFestival`（继承原版 `RitualOutcomeEffectWorker_FromQuality`，四档沿用原版庆典的心情和确信度，再加 5.2 的神眷和"难忘"奖励）。
  - `ritualObligationTargetFilter` = `LP_Target_FaithAltar`（新 `RitualObligationTargetFilterDef`，worker 只接受 `LP_FaithAltar`）；`ritualTargetFilter` 同理。
  - `ritualExpectedDesc` 写中文："每年{DATE}，{MEMBERS}要向X举行庆典。"
- 神祇模因的 `requireOne` 再加上自己的庆典戒律（所以带模因就必有庆典）。
- **固定日期**：原版日期触发器会随机日期。Harmony 后缀 `Precept_Ritual` 初始化日期的地方（`RitualObligationTrigger_Date` 里的日期字段，实现时找到），如果 `precept.def` 是我们的庆典，把日期设成扩展里的 `festivalQuadrum` + `festivalDay`。
- 品质因子（`RitualOutcomeComp`，用原版现成的 comp 类）：参与人数（原版曲线）、房间印象（原版）、**有神官主持** +15%、**神殿**（7.2）+10%、**该神神像在 8 格内** +10%、**该神遗物在 8 格内** +15%。

| 神 | 庆典名 | 日期 | 额外要求 | 额外品质 |
|---|---|---|---|---|
| 洛山达 | 绿草节 | Aprimay 1 | — | 白天开始 +10% |
| 提尔 | 盾会 | Jugust 8 | — | **每 4 年（`GenDate.Year % 4 == 0`）是"大盾会"**：神眷结果 ×1.5 |
| 密斯特拉 | 魔网之夜 | Septober 15 | — | 夜里开始 +10%；每名会施法的参与者 +3% |
| 凯兰沃 | 亡者之宴 | Decembary 1 | — | 地图上有已入葬的墓 / 石棺 +10% |
| 马拉尔 | 大猎 | Septober 1 | 开始时圣坛 3 格内要有一具体型 ≥ 0.7 的动物尸体，开始时消耗 | — |
| 塔洛斯 | 风暴之歌 | Jugust 1 | — | 雷暴中 +25% |
| 班恩 | 暴政之日 | Decembary 8 | — | 每名在场奴隶 +5%（上限 +20%） |
| 莎尔 | 无光守夜 | Decembary 8 | — | 夜里 +20%；圣坛所在格亮度 < 30% 再 +10% |
| 苏尔特 | 熔炉之火 | Aprimay 8 | 开始时圣坛 3 格内要有市值合计 ≥ 300 的武器或护甲，开始时消耗 | — |

"消耗"用一个通用 `RitualOutcomeComp`/开始条件：扩展字段 `consumeFilter`（ThingFilter）+ `consumeMinValue` 或 `consumeMinBodySize`；不够就在仪式窗口里显示原因并不能开始。

### 5.2 庆典结果

| 档 | 守护神神眷 | 其他 |
|---|---|---|
| 糟糕 | -3 | 原版 |
| 平淡 | +2 | 原版 |
| 不错 | +6 | 原版 |
| 难忘 | +12 | 原版 + 下表 |
| 错过义务 | -5 | 原版错过心情 |

神眷加给**这场庆典的神**（不是守护神时也加，用 `Faith.Gain`，不受 `Relevant` 限制）。"错过"挂原版义务过期的地方（实现时找；找不到就删掉这一条）。

"难忘"奖励：

| 神 | 难忘奖励 | 写法 |
|---|---|---|
| 洛山达 | 3 天"黎明的眷顾" | 直接调用 1.39.0 季度显圣赐福的开始方法（洛山达、3 天），**不**重置季度显圣计时 |
| 提尔 | 主持人获得 `LP_SworeOath` 30 天：近战命中 +5、社交冲突概率 ×0.5 | 新 HediffDef |
| 密斯特拉 | 参与者精神力（Royalty psyfocus）补满，`LP_WeaveNight` 3 天：心灵敏感度 +10% | 新 HediffDef |
| 凯兰沃 | 地图上所有人形尸体加入 1.39.0 的"已安息"列表；参与者 +5"与亡者和解" 10 天 | 复用 `WorldComponent_Faith.rested` |
| 马拉尔 | 参与者 `LP_GreatHunt` 5 天：近战命中 +5、射击精度 +5% | 新 HediffDef |
| 塔洛斯 | 3 天塔洛斯赐福（同洛山达的写法） | 复用 |
| 班恩 | 地图上所有奴隶压制度 +20% | 直接改 `Need_Suppression` |
| 莎尔 | 参与者 `LP_SharShelter` 5 天：近战闪避 +8 | 新 HediffDef |
| 苏尔特 | 参与者 `LP_ForgeFire` 5 天：锻造速度 +25% | 新 HediffDef |

### 5.3 众神殿的庆典

众神殿模因 `requireOne` 不带庆典；但众神殿文化里，圣坛多一个按钮"举办庆典"：选一位可信奉神 → 用那位神的庆典 `RitualPatternDef` 开一场**不带义务**的仪式（原版 `canStartAnytime`）；每位神每年一次，每年最多 4 场不同神（计数存 `IdeoBridge` 存档字段，按年清零）。

### 5.4 灰袍葬礼 `LP_Funeral_GreyRobe`

- 新 `PreceptDef`，和原版 `Funeral` 同一个议题（互相替换），`ritualPatternBase` 复制原版 `FuneralStandard`，只换 `ritualOutcomeEffect`（worker 继承原版葬礼结果 worker）。
- `requiredMemes`：亡者之判 或 众神殿（任一；如果原版 `requiredMemes` 是"全部"语义，就改用代码 `CanBeAdded` 检查）。亡者之判模因的 `requireOne` 带它。
- 结果：原版葬礼效果照常；"不错"及以上：死者尸体加入"已安息"列表，凯兰沃神眷 +2；"难忘"再 +3。
- 永夜不朽文化不能选（`LP_Meme_Undeath` 在它的排斥里）。

---

## 六、角色

### 6.1 神官 `LP_Role_Cleric`

- `Precept_RoleSingle` 专家角色，`requiredMemes` 任一神祇模因或众神殿。角色贴图 / 角色服装要求：本版**不要求**服装（文化服装在后续批次）。
- 角色效果（`roleEffects`）：神术消耗 ×0.75（`FaithUtility.MiracleCost` 里乘 `IdeoBridge.MiracleCostFactor(p)`，与信徒、神选者折扣相乘）；社交影响 +10%。
- 授予能力 `LP_Ability_DivinePreach`"神迹布道"：复用原版改宗能力（`Convert`）的效果 comp，效力 ×2，额外消耗守护神神眷 5（不够时能力禁用写原因），冷却同原版改宗能力。
- 庆典、灰袍葬礼的默认主持人优先神官（`RitualRoleDef` 的 organizer 允许角色列表里加神官）。
- 圣坛按钮"**集体祈祷**"：殖民地有神官且神官在地图上才出现；点击后地图上所有空闲（非征召、非倒地、非精神崩溃）的同文化信徒去圣坛祈祷（复用 `LP_PrayAtAltar` 作业）。冷却 1 天。本次集体祈祷里每人神眷不计入每日上限的一半（即这一次的上限 ×1.5）。

### 6.2 大祭司（不是新角色）

- 文化生成或模因变化后（1.5 的 2500 tick 检查里顺带做），如果文化有神祇模因或众神殿，把原版道德导师角色（`IdeoRole_Moralist` 的那个 `Precept_Role`）的 `name` 改成"大祭司"——**只在名字还是原版自动生成的名字时改**（存一个"我们改过"的标记，玩家手动改名后不再覆盖）。
- 圣坛按钮"**求取神谕**"：殖民地主文化有神祇模因 / 众神殿，且道德导师在地图上。冷却 15 天。效果：信件告诉你：
  1. 下一次季度显圣是赐福还是降罚（按当前神眷预测；如果离显圣还有 > 7 天就写"现在神眷是 X，若保持则……"）；
  2. 守护神当前"最想要的作为"：从 1.39.0 该神作为表的加分项随机一条；10 天内做到一次，额外 +5 神眷（用一个存档字段记"神谕作为 key + 截止 tick"，在 `Faith.Deed` 里判断）。

---

## 七、神殿与神

### 7.1 神像（`Bridges/Ideology/Defs/Ideo_Statues.xml`）

- 10 个 `ThingDef`：`LP_Statue_Lathander`、`LP_Statue_Tyr`、`LP_Statue_Mystra`、`LP_Statue_Kelemvor`、`LP_Statue_Malar`、`LP_Statue_Talos`、`LP_Statue_Bane`、`LP_Statue_Shar`、`LP_Statue_Surtr`、`LP_Statue_Pantheon`（众神像）。
- 2×2，`stuffCategories` Stony + Metallic，材料 150 + 金 20，工作量 20000，美观 60（按材料系数），可搬运（minifiable），研究 `LP_Research_Faith`（已有），放在和圣坛同一个建造分类。
- 扩展 `LP_StatueExtension { deity }`（众神像 deity 空）。
- **放在 `Bridges/Ideology` 里**：没装文化时没有神像（神像的作用都和文化有关，1.39.0 不需要它）。
- 效果：
  - 守护神的神像（或众神像，文化是众神殿时）在任一玩家家园地图：守护神每天 +0.3 神眷（日结算；不论有几座只算一次）。
  - 文化有神祇模因、但所有家园地图都没有该神神像：信奉这个文化的殖民者 -1"神没有立像"（ThoughtWorker，读 `IdeoMapCache.statues`）；有神像时 +1"神像庇佑"。
  - 神殿判定需要它（7.2）。

### 7.2 神殿 `LP_Temple`（`RoomRoleDef`）

- worker `RoomRoleWorker_LPTemple`：房间里同时有 `LP_FaithAltar` 和任一 `LP_Statue_*` → 分数高于原版礼拜室（原版 `WorshipRoom` 的分数 +1，实现时看原版数值）。房间标签"神殿"。
- 原版对"礼拜室"的要求（文化需要礼拜室时）：如果原版是按 `RoomRoleDef` 精确判定的，就 Harmony 让 `LP_Temple` 也算礼拜室；实现时确认，做不到就在报告里说明（神殿和礼拜室分开也能玩）。
- 效果：在神殿里祈祷 ×1.25（4.2）；庆典品质 +10%（5.1）；在印象 ≥ 85（原版"极其令人印象深刻"档）的神殿里祈祷，心情记忆用"在宏伟的神殿祈祷"+5（代替"祈祷过"+3）。

### 7.3 神赐遗物 = 文化圣物

- 9 件 `LP_Relic_*` 在 `Bridges/Ideology/Patches/Ideo_Relics.xml` 里补上 `relicChance`（原版字段名以 1.7 为准；写 1），这样原版随机圣物也可能是它们。
- 文化生成或模因变化时（2500 tick 检查顺带做，只做一次并记标记）：文化有神祇模因、有 `Precept_Relic` 圣物戒律、且圣物还**没有在世界上生成过**，把第一个圣物戒律的物品改成该神的遗物（`Precept_ThingDef.ThingDef`）。已经生成过的不动。
- 拿到第二件同名遗物（玩家已有一件该神遗物时，1.39.0 的赐予 / 寻回 / 原版圣物寻回又给了一件）：照 1.39.0 规则转成 2 份神迹残片（1.39.0 只在自己的路径里转，这里只需要确认原版圣物任务给的不会重复；重复了就在报告里说明，不强求）。
- 日结算：玩家主文化的每件圣物，如果放在家园地图的圣物柜（原版 `CompRelicContainer`）里，该遗物所属神每天 +0.5 神眷。
- 圣物被毁 / 丢失：原版心情照常，该神神眷 -20（Harmony 后缀原版圣物丢失 / 被毁通知，实现时找方法）。

---

## 八、NPC 派系文化（`Bridges/Ideology/Patches/Ideo_Factions.xml` + 代码）

### 8.1 补模因（XML 补丁）

`PatchOperationAdd` 到各 `FactionDef/forcedMemes`（保留原有的 `ideoName`、`ideoDescription` 和原版模因）：

| 派系 | 加的模因 |
|---|---|
| `LP_FaerunColony` 费伦拓荒同盟 | `LP_Meme_Pantheon` |
| `LP_FaerunHostile` 塞伊远征军 | `LP_Meme_Bane`、`LP_Meme_Weave` |
| `LP_EmberClan` 余烬氏族 | `LP_Meme_Surtr`、`LP_Meme_Ordning` |
| `LP_IllithidColony` 幽暗殖巢 | `LP_Meme_Ilsvi` |
| `LP_Zealots` 猎巫狂热分子 | `LP_Meme_Tyr` |
| `LP_Larloch` 拉洛克 | `LP_Meme_Undeath`、`LP_Meme_Weave` |
| `LP_KelemvorChurch` 凯兰沃教会 | `LP_Meme_Kelemvor`（它原来没有 `forcedMemes` 节点就用 `PatchOperationConditional` 先建） |
| `LP_BlackNetwork` 黑网 | 不加（第四批契约之律） |
| `LP_ChromaticDragons` 龙教徒 | 不加（第二批龙之教团） |

注意原版文化模因影响力上限：塞伊 = `Structure_Ideological` + `Supremacist` + 班恩 2 + 魔网 1，检查生成时不超；超了就去掉原有的 `Supremacist`（在报告里写）。

### 8.2 补戒律（代码）

新扩展 `LP_FactionIdeoExtension { List<PreceptDef> precepts }` 用补丁挂到派系上。Harmony 后缀原版生成文化的方法（`IdeoGenerator.GenerateIdeo(IdeoGenerationParms)`），`parms.forFaction` 带扩展时，把列出的戒律替换进对应议题：

| 派系 | 戒律 |
|---|---|
| 费伦拓荒同盟 | 亡灵观 憎恶、种族观 众族共存、魔法观 谨慎、原版 `Slavery_Abhorrent` |
| 塞伊远征军 | 亡灵观 可接受、魔法观 魔网至上（模因已强制）、种族观 本族至上 |
| 余烬氏族 | 巨人观 至上（模因已强制）、种族观 本族至上 |
| 幽暗殖巢 | 吞噬大脑 神圣（模因已强制）、种族观 本族至上 |
| 猎巫狂热分子 | 魔法观 禁绝、亡灵观 憎恶 |
| 拉洛克 | 亡灵观 神圣（模因已强制）、成神观 可接受 |
| 凯兰沃教会 | 亡灵观 憎恶、成神观 亵渎 |

### 8.3 旧存档：重建按钮

设置"文化"分组里的按钮"**重建 DND 派系文化**"（只在游戏进行中可点，有确认框）：
- 对 8.1 表里有加模因的每个 DND 派系：如果它的主文化缺任一应有的 DND 模因，就用该派系的 `FactionDef` 参数重新生成一个文化（走同一个 `IdeoGenerator`，所以 8.2 的戒律也会补上），加进世界文化列表，设为该派系主文化；旧文化保留（已有的 NPC pawn 仍信旧文化，新生成的 pawn 用新文化）。
- 完成后一封信：列出重建了哪些派系。
- 这是 2026-10-24 龙巢存档那个一次性模组 `LongChaoIdeoRebuild` 做法的正式版。报告里提醒 Peter：龙巢存档可以用这个按钮，用完不需要再启用那个一次性模组。

---

## 九、杂项

### 9.1 新生儿补文化

已知问题："新生儿拿不到文化"。Harmony 后缀原版出生（`PregnancyUtility.ApplyBirthOutcome` 或 1.7 实际的出生方法；实现时找）：新生儿 `ideo?.Ideo == null` 时，设成母亲的文化，否则父亲，否则玩家主文化。确信度用原版默认。修好以后，1.39.0 "天界之子"出生钩子不用改。

### 9.2 设置（左栏新分组"文化"，`SettingsLeft` 里加在"成神"后面；只在 `ModsConfig.IdeologyActive` 时画，否则画一行灰字"未启用文化 DLC"）

| 设置 | 默认 | 说明 |
|---|---|---|
| 文化联动 | 开 | 关掉：模因被动、戒律的心情、确信度漂移、改宗修正、守护神锁定、神像效果全部停用（Def 仍然加载，避免存档报错）；派系补的模因保留 |
| DND 模因出现在随机 NPC 文化的权重 | 0.3（0～1） | 2.1 |
| 神眷影响确信度 | 开 | 4.1 |
| 文化锁定守护神 | 开 | 1.4 |
| 重建 DND 派系文化 | 按钮 | 8.3 |

存档键都用 `lpIdeo` 前缀，写进 `Settings.ExposeIdeo()`，在 `Core.cs` 的 `ExposeData` 里调用。

---

## 十、贴图（RimWorld 画风：手绘感、粗描边、平涂 + 右下阴影、低饱和）

新脚本 `ArtSource/RaceArt/draw_ideo.py`，输出到 `Bridges/Ideology/Textures/`：

| 类 | 数量 | 说明 |
|---|---|---|
| 模因图标 `UI/Memes/LP/*` | 16 | 128px，原版模因图标风格（圆形底 + 符号），符号取 1.39.0 神祇小图标的元素放大重画 |
| 文化图标 `UI/IdeoIcons/LP/*` | 16 | 白色剪影（原版文化图标会被染色），和模因图标同一符号 |
| 神像 | 10 | 2×2 建筑，单张南向（`rotatable=false`），各神有辨识物：洛山达太阳、提尔天平与蒙眼、密斯特拉星环、凯兰沃骷髅天平、马拉尔兽爪、塔洛斯闪电、班恩黑手、莎尔黑圆盘、苏尔特熔炉巨人、众神像多面立柱 |
| 能力 / 按钮图标 | 3 | 神迹布道、集体祈祷、求取神谕 |
| 状态图标 | 5 | `LP_SworeOath`、`LP_WeaveNight`、`LP_GreatHunt`、`LP_SharShelter`、`LP_ForgeFire`（hediff 不一定要图标；有就画，没地方显示就省） |

约 50 张。出一张预览图 `ArtSource/Preview/Ideology-1.56.0.png`（模因图标 + 神像）。

---

## 十一、检查（`IdeoChecks.cs`，`Test.ps1 -Only Ideology`，≤ 15 条）

Test.ps1 加开关 `-NoIdeology`：Core 档的 `ModsConfig` 字符串去掉 `ludeon.rimworld.ideology`，档名加 `-NoIdeology` 后缀（照 `-NoHAR` 的写法）。

1. 文化 DLC 启用时 `IdeoBridge.Active` 为真，16 个模因、7 个议题、9 个庆典、灰袍葬礼、神官、10 座神像、`LP_Temple` 都加载。
2. 用 `IdeoGenerator` 生成一个带 `LP_Meme_Lathander` 的文化：主神名字是"洛山达"；`requireOne` 带出亡灵观"憎恶"和绿草节；绿草节日期 = Aprimay 1。
3. 把这个文化设为玩家主文化，跑 2500 tick 检查 → 守护神 = 洛山达；`CannotSetPatron(提尔)` 返回"由文化决定"。
4. 重塑：把模因换成 `LP_Meme_Tyr` → 守护神变提尔，洛山达神眷 -15，没有冷却。
5. 众神殿文化：守护神可以手动换，冷却是原来的一半。
6. 守护神神眷 60 → `Daily()` 后同文化殖民者确信度 +0.5%；-40 → -1%。
7. 祈祷：确信度 100% 的殖民者 `PrayerReward` 是确信度 20% 的 1.5/0.7 倍（在神殿里再 ×1.25）。
8. 改宗：洛山达信徒被洛山达文化的发起者劝说，`CertaintyReduction` 是基础值的 1.5 倍；神官发起再 ×1.2。
9. 历史事件：在"亡灵观：憎恶"文化里调用巫妖复生的事件，复生者拿到 -10 心情，旁人 -4。
10. 庆典结果 worker：直接用"难忘"结算凯兰沃庆典 → 凯兰沃神眷 +12，地图上的人形尸体都进了"已安息"列表。
11. 灰袍葬礼"不错" → 死者"已安息"，凯兰沃 +2。
12. 神殿：圣坛 + 洛山达神像在同一个房间 → 房间角色是 `LP_Temple`；日结算洛山达 +0.3。
13. 圣物：带神祇模因且有圣物戒律的新文化，圣物物品是该神的遗物。
14. 派系：新生成的塞伊远征军文化含 `LP_Meme_Bane`、`LP_Meme_Weave`，魔法观是"魔网至上"；按钮重建后旧派系的主文化换新且旧文化仍在。
15. 新生儿：母亲有文化时新生儿文化 = 母亲的文化。

另外跑：
- `Test.ps1 -Only Faith`（1.39.0 的 17 条照常全过，确认信仰没被改坏）。
- `Test.ps1 -NoIdeology -Only Faith`：文化桥不加载、没有红字、信仰 17 条全过（**零回归是本版硬要求**）。
- `Test.ps1 -Only Ascension`（成神神力那一处改动）。
- 全量测试可选（Peter 2026-10-26：测试可以简短，他自己玩来反馈）。

---

## 十二、交付惯例

- 开工前：确认 About.xml 是协调线程告知的设备版本；备份到 `ArtSource/backup-<协调给的标签>`；把本文复制为 `ArtSource/Ideology-1.56.0-spec.md`。
- 开工前在设备上 grep 一遍所有新 defName（`LP_Meme_`、`LP_Issue_`、`LP_Role_Cleric`、`LP_Statue_`、`LP_Temple`、`LP_SworeOath` 等），撞名就改名并写进报告。特别注意 1.53～1.55 深渊 / 契约 / 物理职业批次可能已经用掉一些名字。
- 报告 `ArtSource/Ideology-1.56.0-report.md`：做了什么、实际 defName、偏离本文的地方（"决定"一节）、没做到的、测试结果、游戏里没看过的东西清单。
- About.xml 版本号 1.56.0；`使用说明.txt` 加更新日志和新章节"文化（需要文化 DLC）"：怎么选神祇模因、守护神锁定、庆典日期表、神官和大祭司、神殿和神像、旧存档按钮。
- 完成后提醒 Peter 退出并重启游戏；旧存档要点一下"重建 DND 派系文化"才能看到派系的新模因。
- 和其他线程的接口：本文给 `FaithUtility.CannotSetPatron`、`WorldComponent_Faith.SetPatron`（新可选参数）、`Faith.PrayerReward`、`FaithUtility.MiracleCost`、1.40.0 神力来源、耐瑟研读经验各加了一个乘数 / 挂点。报告里列出改动的共享文件，告诉协调线程。
