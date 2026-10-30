# DND边缘大陆 1.69.0 事件小人入口 报告

日期：2026-10-29　备份：ArtSource/backup-1.68.0

## 要解决的问题
靠信件选项推进的 DND 事件（巨人迁徙讨食、龙、拉洛克使者等），信件一旦被右键关掉，就再也没有地方答复，事件只能按“不答复”处理。

## 做法
- 新基类 `LP_EventLetter`（Source/EventPawnAccess.cs）：
  - `lpResolved`：选了会结束事件的选项就 `Resolve()`，之后不再出现。
  - `Live`：事件还在等答复（小人还在、还没答复过）；`EventThings`：能打开它的小人 / 物件。
  - 原来各信件里的 `ArchivedOnly` 判断换成 `Closed`，所以档案（历史 → 消息）里打开也能答复。
- 入口（不在信件里弹浮动菜单，避开 1.61.0 那种“点了没反应”）：
  - 选中事件小人：底部按钮「事件：信件标题」（Pawn.GetGizmos 后置补丁；法师塔残骸在自身 GetGizmos 里加）。
  - 选中任意殖民者，右键事件小人 / 残骸 / 契约书 / 龙蛋：「事件：信件标题（打开对话框）」（`FloatMenuOptionProvider_EventPawn`）。
  - 两者都调用原信件的 `OpenLetter()`，打开的是同一个对话框、同样的选项。
- 旧存档：`lpVer`（新信件 = 1，旧信件读档 = 0）。事件状态本身记着答没答过的信件（`StateTracked`：巨人讨食 / 预言、五色龙勒索、龙过境、拉洛克、法师塔、英雄偿契、魅魔）以前关掉的现在也能重新答；其余旧信（深渊低语、云巨人、契约等）照旧只在信件栏里能答，防止重复领取。

## 审计结果
| 事件 | 信件类 | 入口 | 何时消失 |
|---|---|---|---|
| 巨人迁徙：饥饿的巨人 | Letter_GiantMigration(food) | 队里任意一人 | 已答复 / 拔营 |
| 巨人迁徙：先知的预言 | Letter_GiantMigration(prophet) | 风暴巨人先知 | 已答复 / 拔营 |
| 巨人迁徙：想留下的巨人 | Letter_GiantMigration(stay) | 那名巨人 | 加入 / 离开地图 |
| 五色龙勒索 | Letter_DragonExtortion | 龙、护卫 | 已答复 / 龙死或离开 |
| 龙过境：另一条龙 | Letter_DragonVisit | 古龙、仆从 | 已答复 / 超时离开 |
| 上位者的使节 | Letter_CloudChoice(envoy) | 云巨人使节 | 已答复 / 使节离开 |
| 收购龙蛋 | Letter_CloudChoice(egg) | 买主、龙蛋（无买主时 2 天） | 已答复 / 蛋没了 |
| 拉洛克的邀约 | Letter_LarlochEnvoy | 使者、骷髅护卫 | 已答复 / 使者离开 |
| 法师塔坠落 | Letter_TowerCrash | 残骸（按钮 + 右键） | 已决定处理方式 / 残骸没了 |
| 深渊低语 | ChoiceLetter_AbyssWhisper | 做梦的殖民者、那本书 | 已答复 / 书没了 |
| 魅魔现形 | ChoiceLetter_SuccubusReveal | 魅魔 | 已处置 |
| 异界契约（契约书、偿契、巫婆集会、夏宫来客、解约、重新谈判、调换儿、妖精代价交易、血战之潮） | ChoiceLetter_Pact | 签约人 / 孩子、契约书、血战双方 | 已答复 / 内部条件失效 |
| 英雄偿契 | ChoiceLetter_HeroPact | 那名殖民者 | 已交付 |

不改的：
- 红袍招揽 / 上缴灵魂 / 进贡（Letter_FactionChoice）：只有信使，没有地图上的小人。
- 朝圣者（ChoiceLetter_FaithPilgrims）：答复前朝圣者还不在地图上，信件本来就不能右键关掉。
- 冒险者升级信（Letter_AdventurerLevel）：已有冒险者页签。
- 神灯 / 巨灵许愿：本来就是巨灵身上的能力按钮。
- 红鹰、黑网、九狱商队、妖精集市：原版右键商人交易；九狱赎人、妖精代价交易本来就在右键里。

## 测试
- `Test.ps1 -Only EventPawn`：19 项全过（含存读档）。覆盖：所有选择信件都走基类；关掉讨食信后巨人身上有按钮、右键有选项、按钮打开的对话框带真实选项、在里面答复后入口消失、已结束的事件打不开；档案里打开仍有选项；旧存档规则；使节离开地图时关闭；法师塔残骸按钮 + 右键 +「自己拆」；契约信（调换儿）；读档后关掉的信仍能从巨人打开、答复过的仍是结束状态。
- 回归：`-Only Events2,Pact,Netheril,Abyss` 全过；`-Only ChromaticDragons,HeroTraits` 49 项全过（第一次跑时五色龙“交涉 20 点减半”一项失败，重跑通过，偶发，和信件无关——测试里勒索信本来就不发）。
- `-Only Events`：巨人迁徙部分全过，之后停在已有的失败“divine infusion trait: 8 degrees named after the gods”——Defs/Twilight.xml 的神力灌注等级标签带了 `<color>` 颜色标签（1.68.0 备份里就是这样），检查还在比对纯文本标签。与本次无关，没改。

（真实鼠标点击无法在 Test.ps1 里模拟；测试里直接调用按钮的 action 和右键选项，打开的是真实的 Dialog_NodeTree。）
