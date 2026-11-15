# 精灵衰老 1.156.0

Peter：精灵 18 岁后按正常速度的 5% 衰老。

- 改动：`Defs/CommonRaces.xml` 的 `LP_ElfLongevity` 曲线 `(13,1)(18.5,0.25)` → `(18,1)(18.01,0.05)`；基因与高等精灵描述同步。
- 范围：高等精灵 / 木精灵 / 卓尔（三者都带 LP_ElfLongevity）。半精灵（LP_HalfElfBlood，×0.6）不变。
- 对比 5e PHB：精灵约 100 岁成年、寿命约 750 岁。本模组精灵 18 岁成年（RimWorld 生命阶段），之后每 20 年老 1 岁；生理 80 岁约在 1260 岁，比 PHB 更长寿但成年更早。
- 检查：CommonRaceChecks.CheckElf 改为 5%，`-Only CommonRaces` 25 通过。
- 备份：ArtSource/backup-1.155.0-ElfAging。
