# 1.81.0 子职业 + 通用能力 — 报告

规格与全部“决定”：ArtSource/Subclasses-spec.md。备份：ArtSource/backup-1.80.0-subclasses（改动前的 Classes.cs、ClassAbilities.cs、ClassMartial2.cs、ClassUI.cs、ClassChecks.cs、ClassChecks2.cs、BeastLevels.cs、Classes4.xml、About.xml、使用说明.txt）。没有提交。

## 内容
- 9 个子职业（每个职业 3 个）+ 每个职业 2 个通用能力（7、11 级），共 45 个能力（36 个子职业能力 + 诡术师 3 个法术 + 6 个通用）。
- 选法：职业 3 级 → 征召栏“选择子职业（N）”按钮 / 冒险者分页职业页的“子职业”行 → Dialog_ChooseSubclass（点两次确认，不能改）。分支：图腾（熊/鹰/狼）、风暴（沙漠/海洋/冻原）、猎手之选（3 级）、防御战术（7 级）。兽王另有“选择伙伴”按钮（Dialog_ChooseCompanion）。
- 旧存档：读档第一刻发“子职业”提醒信（职业 ≥ 3 未选的殖民者）；之后新到 3 级的发一条消息（只一次）。
- 美术：ArtSource/RaceArt/draw_subclasses.py → Textures/LP/Classes/Subclass/（45 图标、9 个 Emblem_*、ThornTrapThing），预览 ArtSource/Preview/Subclasses-icons.png。

## 文件
- 新：Defs/Subclasses.xml、Source/ClassSubclass.cs（LP_SubclassDef、SubclassData、ClassSubclass、Building_ThornTrap、两个窗口、GameComponent_Subclasses、7 个 Harmony 补丁）、Source/ClassSubclassChecks.cs。
- 改（最小挂钩）：Classes.cs（ClassData.sub 存档；ClassOf 回落到子职业；Sync 补发；CannotCast / Pay 走诡术师法力）、ClassAbilities.cs（Apply / Invalid 链、AfterCast、法力提示）、ClassMartial2.cs（Out / In / SneakOk / Unaware / Tick250 / Refreshed / Reset、Hediff_Rage 每秒与结束、伤害前缀的伙伴与庇护、命中补丁、野外穿行的飞檐走壁）、ClassUI.cs（子职业行）、BeastLevels.GrowthCap（伙伴成长上限；Harmony 后缀因方法被内联不生效，改成一行挂钩）、ClassChecks.cs（调用三处）。

## 测试
- `Test.ps1 -Only Classes`：PASS 54（原 43 + 新 11：定义、选择门槛、3/7/11/15 发放、分支保存、狂战士、图腾·熊、风暴·冻原、游荡者三个子职业 + 通用、游侠三个子职业 + 通用、旧存档提醒、存读档）。
- `Test.ps1 -Only Adventurer`：PASS 156。
- `Test.ps1 -VPE -Only Classes`：PASS 7。
- 观察到的旧抖动：1.73.0 的 “cure wounds heals an ally …” 在 13 次 -Only Classes 里失败 2 次（在任何子职业小人出现之前，子职业挂钩此时都是空集合直接返回），重跑通过。

## 没测到（只编译 + 代码检查）
狂乱的每秒追加攻击（需要真实近战任务）、报复 / 群敌杀手（走 Fiends.Defer，下一刻才执行）、鹰之滑翔 / 狼之扑倒 / 熊之嘲讽、风暴光环的附加效果与 Hediff_Rage 计时、偷取法术（需要敌人施放原版能力）、寻宝直觉（迷锁地牢生成）、野性直觉的袭击加速、原野之眼提示、非凡训练三种指令、分享法术复制增益与救起、伙伴攻击算魔法、法师之手开门、荆棘陷阱被踩触发、追猎者乱舞与暗处命中的命中补丁、鹰之射程 +3、伙伴野性等级随游侠升级（测试进程关了野性等级，只测了成长上限）、子职业行 / 窗口的界面排版。

## 已知取舍
- 伪装身份按备选做成“暗影潜行时长 ×2”；提前半天的袭击预警、看见陷阱没做（原版无对应机制）。
- 原版没有鹰，鹰之灵用食火鸡外形。
- 追猎者的乱舞按“落空再判定一次”实现（不是真的再出一刀）。
