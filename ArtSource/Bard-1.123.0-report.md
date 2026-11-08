# 1.123.0 吟游诗人（第 13 个职业）实装报告

设计稿：https://claude.ai/artifact/2cmJyfL7Zi4Z9kzD4jqcLk（2026-11-06）。Peter「完成 该部分」后按设计稿全部实装。

## 文件
- `Defs/ClassBard.xml`：LP_Class_Bard、LP_BardicPower、状态（LP_BD_*、LP_ACB_Dancing / Foresight）、心情、幻象分身 LP_BD_Phantasm、13 个职业能力、2 个传奇、4 个学院 15 个学院能力（学院里带魔法奥秘分支 secret1/secret2，学识另有 secret3）。
- `Defs/Adventurer_ClassFeats.xml`：天生歌喉、名动四方。
- `Defs/ManaLake_Arcana.xml`：4 颗水晶能力；`Defs/ManaLake_Crystals.xml` 由 `ArtSource/ManaLake/gen_crystals.ps1` 重新生成（加了 ClassBard.xml 的颜色），52 枚。
- `Source/ClassBard.cs`：Bard（资源、成功率、魔法奥秘、被动位、所有效果）、状态类、幻象建筑、传颂之歌窗口、GameComponent_Bard（威名之歌记账）、Harmony：BardDamage / BardChat / BardSatireRaid。
- `Source/ClassBardChecks.cs`：`-Only Bard` 16 条。
- 挂进现有代码：`ClassResource.Melody`；`ClassSystem.MaxResource / ResourceLabel / ResourceTip / Regen / Tick250 / Sync / StatValue / CannotCast / Pay`（新 `CastClassOf`）；`ClassEffects.Invalid / Apply`、`Ability_Class.Activate / Tooltip`；`ClassSubclass.CanChoose`（Bard.ChoiceBlock）、`Unsurprisable`（预知术）；`ArtificerCraftQuality.Tiers`（无双技艺）；`ClassRestless`；`ClassLevelInfo`；`NpcClasses.Skip`；`Bards.Start`（巡演者 3～7 级吟游诗人）。
- 美术：`ArtSource/RaceArt/draw_bard.py`（28 个能力 + 2 传奇 + 4 徽记 + 背景 + 幻象），`draw_class_feats_188.py` 加了 2 个专长，`draw_manalake.py` 重画 52 个水晶图标。

## 测试
- `-Only Bard` 16/16。
- `-Only 'Classes,NpcClasses,ClassFeats'`、`-Only 'ManaLake,Adventurer'` 191、`-Only 'Events3,AutoCast,ClassAudit,Bard'` 247、`-VPE -Only Classes` 7，全部通过。
- 改了旧检查里的数量：职业 12→13、职业能力 110→123、传奇 24→26、职业专长 24→26（3 级 12→13）、水晶 48→52；ClassUI 资源上限（乐韵 4 级 = 6）；ManaLake 的设置组数 19→20（与本次无关，是之前版本加了一组设置后没改的旧数字）。

## 和设计稿的差别
- 激昂强度直接取社交、艺术里较高的一项（不再按能力分“看社交 / 看艺术”）。
- 乐韵的演奏 / 艺术加成改成每 250 刻另 +0.1（同法师研究），而不是 ×1.5。
- 万事通：所有技能 +2（原版 aptitudes），没有“不超过吟游等级”的封顶。
- 博闻强识：自动挑兴趣最低的 3 个技能，不弹窗选。
- 反迷惑之歌：心灵冲击减半改成心灵敏感度 −30% + 心智抗性 +30%。
- 银舌：“安抚崩溃 +20%”换成社交影响 +20%。普世言谈：去掉和敌对派系通讯。
- 终焉之歌“以 1 点余量站起来”改成倒地时治好一半伤势、失血 −30%（尽力站起）。
- 传颂之歌恶名在外时好感减半（不是 0），并让对方下次袭击 −10%。
