# 1.150.0 以太动力甲 报告（2026-11-12）

Peter 要求：把 `output\AetherPowerArmors\` 里保留的两套（堡垒重甲 钢灰金、奇械甲 青铜）装进游戏，作为 DND 后期装甲。

## 新增
- `Defs/AetherPowerArmor.xml`
  - 研究 `LP_Research_AetherArmor` 以太动力甲（4000，Spacer，前置 PoweredArmor + LP_MythicMetallurgy + LP_Research_WondrousCraft，(6,0.7)）
  - 研究 `LP_Research_AetherBastion` 誓约堡垒甲（6000，前置上一个，(7,0.7)）
  - `LP_AetherArtificerArmor` 奇械动力甲、`LP_AetherAcolyteHelm` 奇械呼吸盔（不可染色）
  - `LP_AetherBastionArmor` 誓约堡垒重甲、`LP_AetherBastionHelm` 堡垒巨盔（CutoutComplex + _m 遮罩，灰色甲面可染色）
  - 父类 ArmorMachineableBase / ArmorHelmetMakeableBase，制造台 FabricationBench，UnfinishedTechArmor，generateCommonality 0
  - 头盔 drawData.bodyTypeScales Thin 0.85
- `Textures/LP/AetherArmor/{ArtificerArmor,AcolyteHelm,BastionArmor,BastionHelm}/`：5 体型 × 3 方向身甲，3 方向头盔，图标；堡垒两件带 `_m` / `m` 遮罩。共 78 张。

| 物品 | 利/钝/热 | 耐久 | 质量 | 穿戴加成 | 造价 | 手工 |
|---|---|---|---|---|---|---|
| 奇械动力甲 | 1.10/0.48/0.56 | 380 | 12 | 移速 -0.2，全局工作速度 +8% | 塑钢100 秘银40 高级零件4 | 8 |
| 奇械呼吸盔 | 1.10/0.48/0.56 | 140 | 1.5 | 瞄准时间 -10% | 塑钢35 秘银10 高级零件1 | 8 |
| 誓约堡垒重甲 | 1.30/0.60/0.70 | 520 | 20 | 移速 -0.45，痛觉休克阈值 +10% | 塑钢150 精金50 秘银30 高级零件6 | 10 |
| 堡垒巨盔 | 1.30/0.60/0.70 | 180 | 2.5 | 痛觉休克阈值 +5% | 塑钢50 精金15 高级零件2 | 10 |

（参考：原版海军动力甲 1.06/0.45/0.54。）

## 改动
- `Defs/PowerArmorVisualTest.xml` 清空为 `<Defs />`（LP_PATest_Armor / LP_PATest_Helm 删除）。`Textures/LP/PowerArmorTest/` 留着没删，可手动删。
- About.xml modVersion 1.150.0 + 描述一句；使用说明.txt 加 1.150.0 一段。

## 备份
`ArtSource/backup-1.149.0-aetherarmor/`：About/About.xml、使用说明.txt、Defs/PowerArmorVisualTest.xml。

## 未做 / 注意
- 没改 C#，无需编译；XML 已在本地校验格式。未进游戏实测。
- 研究位置 (6,0.7)(7,0.7) 只对照了 Research.xml，别的文件里如果有研究占了同一格会重叠（只影响显示）。
- 不依赖搁置的 Shelved/NewContent-1.126-1.127，也不依赖已删除的器械师/巨人锻造等。
