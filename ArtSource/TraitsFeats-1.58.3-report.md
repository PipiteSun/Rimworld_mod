# 1.58.3 持续成长 — 实现报告

规格：ArtSource/TraitsFeats-1.58.3-spec.md；备份：ArtSource/backup-1.58.1（1.58.2 按 Peter 要求跳过）。

## 新增文件
- Defs/Growth.xml：6 本典籍（LP_Tome_STR…CHA，LP_TomeBase 继承 BookBase）、4 件传承武器（LP_Legacy_Sword/Hammer/Bow/Staff）、4 个能力、2 个 hediff、命名 RulePack。
- Patches/Growth.xml：黑网商队 StockGenerator_RandomTome（5%）。
- Source/Growth.cs：Growth 静态类、Book_Tome、BookOutcome_Tome、CompLegacyWeapon、CompAbilityEffect_LegacyPower、GameComponent_Growth、StockGenerator_RandomTome 与全部补丁。
- Source/GrowthChecks.cs：测试组 Growth（13 条，含存读档）。
- ArtSource/RaceArt/draw_growth_158.py → Textures/LP/Items/Tomes/Tome_*.png（6）、Textures/LP/Weapons/Legacy/*.png（4）。

## 决定
- 典籍走原版阅读（BookOutcomeDoer 累计阅读 tick），没有新做读书工作；读满 30 小时（75000 tick）生效，书立即销毁。“同项一本”记在 Hediff_Adventurer.tomes，CanRead 补丁拒绝第二本。
- 许愿“让我更强”是巨灵愿望列表里的新选项，记 wishAbility，每人一次。
- 传承武器：先到 10 杀者绑定（按 thingIDNumber 记各人击杀）；觉醒＝主人冒险者等级 ≥10；升华＝主人是传奇冒险者；每阶 +15% 伤害。没有冒险者 hediff 的人永远是沉睡阶段。
- 实战磨砺只算殖民者，按 records 增量采样，第一次只记基准（老存档不会暴涨）。
- “友好派系”定为同盟殖民地 / 余烬氏族 / 黑网三家；价格优惠只给买入；专属物品是往三级以上派系的商队（交易者 Pawn）库存里放一件，每个交易者一次；情报信给的是按当前威胁点数 ×0.8～1.2 的估算。
- 军事援助冷却：原版 1 天，三级头衔每次请求后把 lastMilitaryAidRequestTick 往前推半天。

## 偏差
- 规格里的“远古遗迹宝箱掉典籍”没有做（1.7 没有稳定的遗迹宝箱生成钩子）；以宿敌 25% / 传奇怪 15% / 耐瑟瑞尔差事 / 异世界宝箱 3% / 季度礼物 5% 代替。
- 测试 13 条（规格上限 20）：一条检查里合并了多个断言。

## 新增 Harmony 钩子与周期代码
| 钩子 | 目标 | 作用 | 快速返回 |
|---|---|---|---|
| GrowthTomeCanRead | Book 可读判定 | 同项典籍第二本拒读 | 非 Book_Tome 立即返回 |
| GrowthQuestRep | Quest.End postfix | 成功任务 +3 声望 | 设置关 / 非三家派系 |
| GrowthGiftRep | Faction.TryAffectGoodwillWith postfix | 送礼每 20 好感 +1 | reason 不含 Gift |
| GrowthTradeRep | Tradeable.GetPriceFor postfix | 一级头衔买入 ×0.95 | 非玩家买入 / 非三家 |
| GrowthAidRep | FactionDialogMaker.CallForAid postfix | 三级头衔援助冷却减半 | Rank<3 |
| GrowthStockRep | TradeSession.SetupWith prefix | 三级商队带专属物品 | 已放过 / Rank<3 |
| GrowthRepInfo | Faction.GetInfoText postfix | 显示声望与头衔 | 非三家 |
| HeroDamage（已有） | PreApplyDamage | 传承武器伤害系数、战锤 ×0.9 | legacyDefs 为空 / 不是传承武器 |
| HeroMeleeHit（已有） | 近战命中 | 升华长剑 3 格光环 +5% | swordOwners 为空 |
| HeroRange（已有） | 射程 | 升华巨弓 +3 | 非传承武器 |
| HeroDeeds.OnKilled（已有） | 击杀 | 传承击杀计数、援助声望、主人死亡 | — |
| GameComponent_Growth | 每 2500 tick | 季度切换时情报信 / 礼物 | — |
| GameComponent_HeroTraits.Tick2500（已有） | 每 2500 tick | 磨砺采样、传承能力同步 | 设置关 |

## 测试
- `-Only Growth`：13 条 PASS（含存读档：典籍、许愿、磨砺三组数组、传承主人与击杀数、派系声望）。
- `-Only Adventurer`：123 PASS；`-Only HeroTraits`：25 PASS；`-Only Feats2`：20 PASS。
- `-Only Netheril`：第一次在 netheril 32 处 JoyTickCheckEnd 补丁里出现一次空引用（job 没开起来），重跑后 32–35 都过，停在已知的旧问题 netheril 22。
- 构建：主程序 + HAR / VPE / Mythic Bloodlines 桥接全部 PASS。

## 没做 / 风险
- 遗迹宝箱掉落没做（见偏差）。
- 实战磨砺速度（120000）是估算值，需要实际游戏几十天后再调整。
- 专属物品只在交易者 Pawn 身上，轨道商船没有。
- 情报信只是估算，不保证与之后实际的袭击一致。
