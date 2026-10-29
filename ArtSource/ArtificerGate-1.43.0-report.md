# 1.43.0 DND武器需要器械师 — 报告

## 改动文件
备份在 `ArtSource/backup-1.42.0/`（同相对路径）。

- `Bridges/VPE/Source/Artificer.cs`：`RequiredAbility` 新增 12 件武器；`MayCraft` 在设置关掉时直接放行；静态构造里给武器和产出它的所有配方说明追加“制作需要：器械师「…」。”（`AnnotateDescriptions`，已有就不重复；原有 11 件也一起标注）。
- `Bridges/VPE/Source/BridgeChecks.cs`：`ArtificerChecks` 新增 6 条检查。
- `Bridges/VPE/Defs/ClassAbilities_Artificer.xml`：奥术工坊、符文铭刻说明列出新增武器。
- `Bridges/VPE/Defs/ClassArtificer.xml`：器械师路径说明改为“所有可制作的 DND 武器（符文枪械、法杖、DND近战武器和精灵长弓）”都要对应被动。
- `Source/Guns.cs`：设置 `dndWeaponsNeedArtificer`（默认 true，`ExposeGuns` 里按默认 true 读档）；设置页“法杖每发心灵熵”滑条下面加复选框“DND武器需要器械师”与提示。
- `About/About.xml`：版本 1.43.0，说明末尾加一句更新。
- `使用说明.txt`：第二部分新增【1.43.0 DND武器需要器械师】，更新日志加 1.43.0。

## 武器表

| 被动 | 武器（defName） | 配方 |
|---|---|---|
| 奥术工坊 | 食人魔大棒 LP_OgreClub | 产出它的配方 |
| 奥术工坊 | 矮人投掷锤 LP_DwarvenThrowingHammer | 同上 |
| 奥术工坊 | 精灵长弓 LP_ElvenLongbow | 同上 |
| 奥术工坊 | 半兽人巨斧 LP_OrcishGreataxe | 同上 |
| 奥术工坊 | 银剑 LP_SilverSword | 同上 |
| 奥术工坊 | 霜巨人斧 LP_FrostGiantAxe | LP_MakeFrostGiantAxe（已验证 ProducedThingDef 命中） |
| 符文铭刻 | 霜刺长枪 LP_FrostSpear | 产出它的配方 |
| 符文铭刻 | 天穹之剑 LP_EmpyreanBlade | 同上 |
| 符文铭刻 | 云巨人晨星锤 LP_CloudMorningstar | 同上 |
| 符文铭刻 | 风暴三叉戟 LP_StormTrident | 同上 |
| 符文铭刻 | 火巨人熔炉重锤 LP_FireGiantMaul | 同上 |
| 符文铭刻 | 熔核破城锤 LP_MoltenMaul | Make_LP_MoltenMaul（已验证） |

原有的 11 件（5 件奥术工坊、5 件符文铭刻、山岳之怒）不变。

## 决定
- 分档：人族/种族武器和精灵长弓放 1 环“奥术工坊”，巨人与天界武器放 2 环“符文铭刻”。
- 大师工艺不变：`Arcana.CraftedQuality` 开头要求武器有 `CompProperties_ArcaneWeapon`（只在 Guns.xml 的符文枪械/法杖上），新增武器没有这个组件，所以品质 +1 自然不会作用到它们，无需改钩子。
- 不装 VPE：门槛只在桥接程序集里（Harmony 补丁 + 说明标注都在桥接），主模组只多一个设置字段，不装 VPE 时不生效。
- 设置默认开；旧配置文件没有这个键时按 true 读出。
- 传说/不可制作武器（LP_Doom、LP_Skyfire、LP_SkySpear、LP_Godsbane、LP_Relic_*、LP_BalorWhip）和投掷物未加入。矮人投掷锤按需求列入奥术工坊。

## 测试
- `Build.ps1`：主程序集、HAR、VPE、神兽血裔桥接全部 PASS。
- `Test.ps1 -VPE`：PASS，440 条（仅职业）。新增 6 条全部 OK：
  - a crafting-20 pawn without artificer passives cannot craft silver sword or storm trident（同时核对两个特殊配方的 `Needed`）
  - with the setting off anyone may craft the DND weapons（检查后恢复设置）
  - arcane workshop unlocks silver sword and frost giant axe but not storm trident
  - rune inscription unlocks storm trident and molten maul
  - legendary weapons are not gated
  - gated weapon and recipe descriptions name the needed artificer passive once
