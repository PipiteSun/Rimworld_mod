# 1.40.1 修复：坠落的云堡 剧本说明显示 "Cannot read data."

## 现象
主菜单“选择剧本”里点 坠落的云堡（LP_FallenCloudKeep），右侧说明面板只显示 "Cannot read data."。

## 根因
`Defs/CloudGiant.xml` 里开局物资“小型雕像 ×2”写的是 `<stuff>Marble</stuff>`。`Marble` 是天然大理石岩石（建筑类 ThingDef），没有 `stuffProps`，不是可用材料（可用材料是大理石砖块 `BlocksMarble`）。

原版生成“开局携带”列表时调用 `GenLabel.ThingLabel(SculptureSmall, Marble, 2)` → `ThingDef.LabelAsStuff`，这个属性不检查 `stuffProps` 是否为空，直接空引用。`Scenario.GetFullInformationText()` 捕获异常后就只显示 "Cannot read data."。剧本里四个 ScenPart_StartingThing_Defined 共用同一个汇总列表，所以四个都会抛异常。

把 Marble 临时改回去时，检查里打出的堆栈：

```
System.NullReferenceException: Object reference not set to an instance of an object.
   at Verse.ThingDef.get_LabelAsStuff()
   at RimWorld.GenLabel.NewThingLabel(BuildableDef entDef, ThingDef stuffDef, Int32 stackCount)
   at RimWorld.GenLabel.ThingLabel(BuildableDef entDef, ThingDef stuffDef, Int32 stackCount)
   at RimWorld.ScenPart_StartingThing_Defined.GetSummaryListEntries(String tag)+MoveNext()
   at RimWorld.ScenSummaryList.SummaryList(Scenario scen, String tag)
   at RimWorld.ScenSummaryList.SummaryWithList(Scenario scen, String tag, String intro)
```

## 其他剧本
本模组 Defs 里所有的 `<stuff>` / `*Stuff` 值只有 Leather_Plain、Steel、WoodLog、Marble 四种，只有 Marble 写错了。新加的检查逐个调用本模组全部 15 个剧本：每个 ScenPart 的 `Summary` 都调一遍，再调一遍 `GetFullInformationText()`。修复后没有剧本出错。

## 改动
- `Defs/CloudGiant.xml`：`<stuff>Marble</stuff>` → `<stuff>BlocksMarble</stuff>`。顺带修好了开局雕像的材料：原来开局会用天然岩石当材料来生成雕像。
- `Source/GiantChecks.cs`：新增 `CheckScenarioSummaries()`，接在 CheckGiants 最后（Giants 组 +1 条）。出错时把剧本名、组件类型和异常堆栈写进日志。
- `About/About.xml`：modVersion 1.40.1。
- `使用说明.txt`：标题版本 1.40.1；更新日志加一行。
- 备份：`ArtSource/backup-1.40.0/`（CloudGiant.xml、About.xml、GiantChecks.cs、使用说明.txt、LichPhylactery.dll）。

## 测试
`pwsh ./Source/Build.ps1`：

```
PASS: LichPhylactery built against installed RimWorld 1.7 and Harmony.
PASS: HAR race bridge built.
PASS: VPE class bridge built.
PASS: Mythic Bloodlines bridge built.
```

`pwsh ./Source/Test.ps1 -Only Giants`（修复后）：

```
PASS: 33 checks (only Giants); save/reload.
```

日志：`[LICH-TEST] OK all 15 mod scenarios show their full description on the scenario select page`

反向验证（临时改回 Marble，跑完已恢复）：

```
FAIL after 29 checks
System.Exception: all 15 mod scenarios show their full description on the scenario select page — broken: LP_FallenCloudKeep/ScenPart_StartingThing_Defined, LP_FallenCloudKeep/ScenPart_StartingThing_Defined, LP_FallenCloudKeep/ScenPart_StartingThing_Defined, LP_FallenCloudKeep/ScenPart_StartingThing_Defined, LP_FallenCloudKeep（全文）
```
