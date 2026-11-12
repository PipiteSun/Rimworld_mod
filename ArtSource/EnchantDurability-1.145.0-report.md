# 1.145.0 附魔强化加耐久上限

Peter 的要求：附魔（附魔强化）除了护甲，还要提高装备的耐久度上限。

## 数值
- 每级耐久上限 +10%：+5 ×1.5，+10 ×2。武器和衣物都加（原来的伤害/命中/护甲/价值加成不变）。
- 物品信息卡里“耐久度”一栏的说明写“附魔强化 +N：×…”；检视文字、附魔台窗口的效果描述也列出“耐久上限+X%”。

## 当前耐久的处理
- 等级变化时按耐久百分比换算当前耐久：满的仍是满的，不会因为上限变高显得破损、掉价或算作破衣。
- 1.145.0 之前已经强化过的装备：读档时换算一次（例如 +5 满耐久的防弹背心 200/200 → 300/300）。
- 强化成功照旧修满到新上限；掉级（+6 以上失败、圣武士祝福转移等）按比例缩回。

## 实现
- Source/GearEnhance.cs：MaxHitPoints 上挂 StatPart_GearEnhance（kind "hp"，HpPer=0.1）。耐久上限是原版的不变属性，按物品永久缓存在 StatWorker.immutableStatCache，ClearStatCache 现在会清掉它并调用 SyncHp 换算当前耐久。
- CompGearEnhance 新存 lpEnhanceHpLevel（当前耐久按哪级上限算），旧档缺这项 = 0，PostLoadInit 时换算。

## 测试
- `Test.ps1 -Only GearEnhance`：19 项全部通过（新增两项：耐久数值/换算/说明；存读档后超出原上限的耐久保留 140/140）。
- 第一版只加上限、当前耐久不动，测试发现装备价值因“不满耐久”下降（StatPart_Health），所以改成按百分比换算。

## 备份
ArtSource/backup-1.144.0-enchantdur（GearEnhance.cs、GearEnhanceChecks.cs、About.xml、使用说明.txt、旧 dll）。
