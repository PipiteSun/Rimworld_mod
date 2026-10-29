# 特性稀有度（1.66.0 热修，2026-10-29）

Peter：「再次给所有DND天赋分级，然后配上颜色」。按 1.65.0 / 1.66.0 六维乘法倍率之后的实际强度重新分级（对照 dnd-genes/DND特性稀有度构思.md 的六档方案，档位按现在的强度重排）。只改颜色和悬停标头，不改效果、不改出现概率。

## 色表
普通 无色 / 优秀 #7FBF6A / 稀有 #5B9BD5 / 史诗 #B070E0 / 传说 #E08A3A / 神话 #E0B040；诅咒 #D05050、事迹 #50C0C0 是性质标记，档位写在悬停标头。

## 分级（77 个特性）
- 普通：守序/混乱、仁善/邪恶、世界公民/纯血至上、惧龙、巨人仆从、巫妖信徒、龙之眷从、吉斯流亡者、魔法恐惧、秘法上瘾、位面错乱、信徒×11、无神者
- 优秀：8 背景、屠龙志士、巨人杀手、不死猎人、殖巢幸存者、寻宝狂、战术家、奥术天赋、狂野魔法、机械迷、自然之子、安息之魂
- 稀有：天赋·X、金刚之躯、锋芒、灵光、传奇之胚、图腾之魂、宿敌猎人、同调者、元素之触、法术抗体、决斗者、13 武艺天赋
- 史诗：反骨、勤勉、天赋异禀、大器晚成、百炼成钢、凡人之志
- 传说：英雄之资、极致·X、神力灌注
- 神话：预言之子、邪契者、魔网之子、神之勇士
- 诅咒：复仇亡魂 史诗、兽化症 史诗、远域之触 稀有
- 事迹：弑神者(DeedGodslayer)、心智堡垒 史诗；屠龙者、巨人克星、巫妖克星、绝境英雄、弑神者(LP_Godslayer) 稀有；冥界归来、圣徒 优秀

## 决定
1. 和 10-28 构思相比：职业天赋从稀有降到优秀（只是 25% 路径经验）；六个强化天赋从史诗降到稀有（条件性，倍率改乘法后不如直接加属性效率的特性）；反骨、勤勉升到史诗（多给 5～8 点属性，和百炼成钢同级）；安息之魂改绿（诅咒已解除，剩下的只有 +15% 伤害）。
2. 颜色仍写在 XML 标签里（1.58.0 起的做法，游戏里已验证能显示）；档位放在 TraitDef 的 modExtension `LichPhylactery.TraitRarityExt`（tier / kind / mark / degrees），ArtSource/trait_rarity.py 按同一张表改标签，-Only TraitRarity 检查两边一致。
3. 悬停标头由 Source/TraitRarity.cs 的 Trait.TipString 后缀加在最前面（缓存）。
4. 强力特性的生成池（代码里的 gold / purple 集合）和概率不变；设置说明里的“金 / 紫”改成档位说法。
5. 超凡属性（20 金、24 紫）不是特性，这次没改：图标是画好的金框 / 紫焰，改文字颜色会和图标不一致。

## 文件
新增 Source/TraitRarity.cs、TraitRarityChecks.cs、ArtSource/trait_rarity.py。改 Defs/HeroTraits、CombatGifts、DndTraits、Adventurer_Talents、Traits_Backgrounds、Faith、Twilight、Ascension（.xml）；Source/HeroTraits.cs（设置文字 + 色例）、HeroTraitsChecks.cs（标签颜色检查改看稀有度表）、DndTraits.cs、CombatGifts.cs（设置文字）；ArtSource/traits_xlsx.py（稀有度列和页）；About.xml、使用说明.txt。备份 ArtSource/backup-1.66.0-rarity。
