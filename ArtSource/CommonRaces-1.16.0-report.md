# DND边缘大陆 1.16.0 常见种族报告（补写）

日期：2026-10-24。做 1.16.0 的会话在跑完整测试和图鉴截图时掉线，没有留下报告。这份报告是 1.17.0 会话接手后根据现有文件和复跑结果补写的。

范围：三个可遗传的 DND 常见种族——半兽人 `LP_HalfOrc`、精灵 `LP_Elf`、矮人 `LP_Dwarf`（不含亚种），外貌和大部分能力用原版基因，专属机制用 1.15.0 的共享组件；以及三条默认启用的种族关系心情。

## 改动文件

| 文件 | 类型 | 说明 |
|---|---|---|
| `Defs/CommonRaces.xml` | 新增 | 三个异种型、8 个专属基因、hediff、心情、3 条种族关系 |
| `Source/CommonRaces.cs` | 新增 | 不屈坚韧（冷却保命 worker、疼痛休克与意识保底补丁）、野蛮攻击/矮人对巨魔的近战倍率补丁、冥想之息、酒鬼之肝、石之耐力 |
| `Source/CommonRaceChecks.cs` | 新增 | 三个种族的自检（含存读档） |
| `Source/CommonRaceGallery.cs` | 新增 | 图鉴截图窗口 `CommonRaces.png` |
| `Source/Checks.cs`、`ConstructDesignChecks.cs`、`OncePerCooldown.cs`、`RacialRelation.cs`、`SharedComponentChecks.cs` | 修改 | 挂入检查与截图，并为半兽人和种族关系做配套修改（这几个文件在 backup-1.15.0 里有改动前的版本；具体改了哪些行是从备份推断的，原会话没有留记录） |
| `Defs/SharedComponents.xml` | 修改 | 同上 |
| `About/About.xml` | 修改 | 1.15.0 → 1.16.0 |
| `使用说明.txt` | 修改 | 1.16.0 条目 |

备份：`ArtSource/backup-1.15.0/`。

## 种族与代谢

| 种族 | 专属基因 | 原版基因 | 代谢 |
|---|---|---|---|
| 半兽人 | 不屈坚韧（每天一次本该倒地时撑 12 小时：不疼痛休克、意识保底约 30%）、野蛮攻击（近战 10% 概率 ×150%） | Skin_Green、Jaw_Heavy、Beauty_Ugly、AptitudeStrong_Melee、MeleeDamage_Strong、Robust、DarkVision、AptitudePoor_Social、Aggression_Aggressive、Learning_Slow | -1 |
| 精灵 | 冥想之息（有心灵链接时睡 8 小时约 +20% 精神力，睡满 2 小时醒来 +3 心情）、精灵血统（心智抗性 50%）、精灵长寿（18 岁后衰老 25%） | Ears_Pointed、Body_Thin、Beauty_Pretty、AptitudeStrong_Shooting、LowSleep、Delicate、MeleeDamage_Weak | -1 |
| 矮人 | 矮人体型（实际 ×0.8、显示 ×0.85、搬运再 ×0.9、对巨魔近战 +10%）、酒鬼之肝（酒精不成瘾、宿醉减半、喝酒 +4 心情）、石之耐力（厚岩顶下 +3 心情、采矿/劳动 +10%） | Beard_Always、Jaw_Heavy、DarkVision、Robust、ToxResist_Partial、AptitudeStrong_Mining、AptitudeStrong_Crafting、MoveSpeed_Slow | -2 |

种族关系（默认启用）：精灵↔矮人 -2，同住 60 天后 +2；矮人看火巨人 -3（只算本殖民地的火巨人）。巨魔↔火巨人示例仍默认关闭。

## 接手时的状态与修复

- 版本号已是 1.16.0，`Build.ps1` 通过，Core `PASS: 1237 checks`，`CommonRaces.png` 截图正常生成。
- 掉线前留下的其他档结果：Appearance `FAIL after 328`，Full `FAIL after 512`。复跑定位到 4 个**测试夹具**问题（游戏逻辑没改）：
  1. 矮人屋顶检查：出生点本来就在厚岩顶下时“恢复原屋顶”等于没离开。改为先设无顶验证再恢复。
  2. 矮人搬运量：与常数比较在 Full 档失败（其他模组改了基础值，实测 61.56）。改为与同环境普通人的比值比较。
  3. 半兽人/矮人近战采样：`TryGetMeleeVerb` 偶尔挑到非伤害类 verb。改为取第一个伤害类近战 verb。
  4. 1.14.0 力能龙食性检查：对照的人可能已被吐息打死（死者没有饮食需求，NRE）。改用新生成的对照人。
  5. 半兽人“致命伤不触发不屈”：100 点切割会拆到头、颅骨、大脑几处，大脑有时没死，于是“不屈”按“只致倒”正常触发，检查误报。改用原版处决的割喉伤害（一处、必死）。不屈坚韧本身的逻辑没有改。
- 修复后 Appearance 档 `PASS: 1437 checks`。完整矩阵结果与 1.17.0 一起跑，见 `MindFlayer-1.17.0-report.md` 的“测试结果”。

## 未完成 / 需要决策（沿用 1.16.0 会话的范围说明）

1. 不做亚种（高等精灵、木精灵、卓尔、山地/丘陵矮人、灰矮人等）。
2. 新基因借用原版图标，没有专属贴图。
3. 小体型不影响食量（原版进食速度不看体型），矮人吃得和常人一样多。
