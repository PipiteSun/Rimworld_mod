# 1.66.0 武艺天赋 — 实现报告

规格：ArtSource/CombatGifts-1.66.0-spec.md。备份：ArtSource/backup-1.65.0（Source、Defs、Patches、About、Bridges、Languages、1.7/Assemblies、使用说明.txt）。

## 新增

- `Defs/CombatGifts.xml`：13 个 TraitDef（LP_CG_Mighty / Swift / Stalwart / Brilliant / Eagle / Commanding / Flurry / Rage / Deadeye / Lethal / Bulwark / Regen / Arcane），commonality 0，exclusionTags `LP_CombatGift`，标签 `<color=#5B9BD5>`（CDATA，同 HeroTraits.xml），描述写入门数值。
- `Source/CombatGifts.cs`：
  - 规则：`CombatGifts`（按 thingIDNumber 缓存天赋下标的并发字典，特性变动时失效）。阶段 `Stage(h)` = 传奇 2 / 10 级 1 / 否则 0，加巨灵许愿的 `cgBonus`，封顶 2。
  - 属性类：`Hediff_Adventurer.Eff(a)` = min(属性, 上限) + 2/3/4，走 1.65.0 倍率曲线（例：力量 20 + 巨力化境 = 有效 24 = ×7）。
  - 附带效果和战技数值：`AddStats` 由冒险者状态的 `BuildStage` 调用（不乘属性效果强度，传奇单位也生效）。
  - 狂暴之血 / 再生之体：`Tick250` 由冒险者状态 `TickInterval` 调用。
  - 伤害前缀（神射远程、法力奔涌法术、致命一击暴击 + 护甲穿透）；命中后缀（连斩排队，下一刻追加一击）。
  - 生成后缀、13 岁生日、获得 / 失去 / 悬停说明、击杀记录、袭击组裁剪。
  - `GameComponent_CombatGifts`：老存档补掷标记 `lpCgBackfilled`、连斩队列。
  - 设置（在「DND 特性」组内，诅咒之力下面）：武艺天赋、敌人也会拥有武艺天赋、普通人形获得率 0～30%（默认 8%）。
- `Source/CombatGiftsChecks.cs`：`-Only CombatGifts`，15 项。

## 改动的已有文件

- `Adventurer.cs`：`cgBonus` / `cgRegenTick`（存档 `lpCgBonus` / `lpCgRegen`）、`cgRage`（不存档）；`Eff` 加天赋加值；`BuildStage` 调 `CombatGifts.AddStats`；`TickInterval` 调 `CombatGifts.Tick250`；`Milestone` 完成 DND 里程碑后 25% 战斗觉醒；属性悬停第一行显示「+N」。
- `AdventurerUI.cs`：冒险者分页属性行显示「14+2/20」。
- `HeroTraits.cs`：`EnemyBoss`（不带显式 roll 时）先给 50% 武艺天赋；设置读写和绘制接到「DND 特性」组。
- `Genie.cs`：许愿菜单多一项「让我更强 · 武艺」。
- `About/About.xml`：版本 1.66.0，描述末尾加 1.66.0 段；`使用说明.txt`：更新日志顶部加 1.66.0 段（锚点各匹配 1 次，和备份比对只有新增的行：使用说明 1912 → 1917 行）。

## 决定 / 偏离

1. **属性 +N 不改存的属性值**：加在 `Eff` 里、上限之后，所以“上限同样提高”是自动的，升阶、失去天赋都不用迁移数据。基础上限仍是 20（`Cap` 不变），加点上限不变。
2. **阶段不单独存**：每次由冒险者等级 / 传奇状态算（很便宜）；只存巨灵升的阶数 `cgBonus`。没有冒险者状态的人没有属性加值和数值类效果（命中类效果照常），悬停里写明。
3. **狂暴之血疼痛**：规格只写了入门 ×0.5，精通 / 化境定为 ×0.4 / ×0.3。触发与解除每 250 刻检查一次（同其他条件状态）。
4. **连斩追加**：命中后排到下一刻执行（同决斗者反击，避免在伤害结算中途再打一次），追加的那一击不会再触发追加；队列上限 64。
5. **铁壁「不会踉跄」**：用原版 `StaggerDurationFactor` ×0，不另打补丁。
6. **致命一击护甲穿透**：绝对值 +0.10 / 0.15 / 0.20，加在这次伤害上（含暴击判定失败时）。
7. **法力奔涌的“法杖 / 灵能 / 神术”**：沿用 1.58.0 `HeroCombat.Spell`（人造成的不带武器的伤害 + 法杖组 G）。
8. **再生之体**：发现缺失非要害部位时开始计时，满周期长回最上层的缺失部位（子部位一起回来）；要害 = 根部位或带 vital 标签的部位。
9. **敌人 combatPower**：原版没有按人的战力，只有兵种的。做法：`PowerFactor(p)` = 1.06 / 1.09 / 1.12，并在敌对袭击组生成后（排在黄昏之年裁剪之后）按多出来的战力从末尾裁掉没有天赋的成员——多出来的不足半个人时不裁。
10. **击杀觉醒的记录**：存在冒险者里程碑列表里，key `kills_cg100` / `kills_cg300`（kills 开头，不算 DND 里程碑，不会让人成为传奇）。已经有天赋时到达门槛也只标记。
11. **首领 50%**：挂在 `HeroTraits.EnemyBoss`（剧情首领、宿敌冠军、迷锁地牢首领都走这里）；传奇怪人形在生成后缀里按 50%。
12. **传奇种族**：设置获得率的一半（默认 4%）。13 岁生日补掷一次（同强力特性）。
13. **巨灵**：单独一个选项，不替换原来的“属性 +1”；已经化境的人选项变灰。失去武艺天赋时 `cgBonus` 清零。
14. **关闭设置**：不再生成、不觉醒、不补掷，已有的保留并照常生效（同其他特性设置）。

## 测试

- `-Only CombatGifts`：**15 PASS**（定义 / 名额 / 与强力特性并存 / 属性 +2+3+4 / 阶段 / 连斩 / 致命一击 / 狂暴阈值 / 铁壁 / 再生 / 法力奔涌 + 神射 / 获得率 + 生成后缀已挂 / 战斗觉醒 + 巨灵 / 老存档只补一次 + 设置关闭 / 敌人战力与裁剪），含存读档。
- `-Only HeroTraits,Adventurer`：**158 PASS**。
- 第一次就全部通过，没有重跑。没跑完整套件。

## 游戏内未测

- 真实生成路径的随机（测试进程里随机生成被关掉，检查用显式 roll）；新开局时开局殖民者 35% 的实际体感。
- 读老存档时的补掷和信件（检查直接调用 `Backfill`，`LoadedGame` 只验证了标记）。
- 巨灵许愿对话框、设置窗口里新增三项的排版、冒险者分页「+N」显示。
- 实战里连斩下一刻的追加（检查直接调用 `FlurryNow`）、狂暴在战斗中的切换节奏、再生在真实时间流逝下的长回。
- 实际袭击的裁剪效果；装 Isekai / VPE 时的表现（VPE 心灵熵分支只按当前环境测了一支）。
