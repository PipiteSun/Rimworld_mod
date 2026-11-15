# 1.152.0 基因工作台拆成独立模组「基因操作」

Peter：把基因工作台从 DND mod 里拿出来，单独做成一个 mod，叫「基因操作」。

## 新模组 Mods\GeneOperations（peter.geneoperations 1.0.0）
- 依赖 Harmony + Biotech。Defs/GeneWorkbench.xml、Defs/Research.xml、Source/GeneWorkbench.cs、Textures/GeneOperations/*（贴图原样复制）、ArtSource/draw_gene_workbench.py（输出路径改成 Textures/GeneOperations）。
- 内部名 LP_ → GO_：GO_GeneWorkbench、GO_EnterGeneWorkbench、GO_CarryToGeneWorkbench（JobDef 和 WorkGiverDef 同名，和原来一样）、GO_CreateXenogermWorkbench、研究 GO_GeneWorkbenchTech。命名空间 GeneOperations，Harmony id peter.geneoperations。
- 研究改放原版主研究页 (10,0.3)，在异种遗传学 (9,0.3) 右边，那里原版和 DLC 都空着。造价、耗电、提取时间等数值全部不变。
- 去掉了 1.27.2 的旧存档修复（MigrateLegacyNode、RemoveDuplicateGenepacks、GeneWorkbenchSaveFix），新模组不需要存档兼容。自带基因库仍然用单独的存档标签（goGenepacks），否则和装配器的 innerContainer 撞名。
- 自测：pwsh Source\Test.ps1（原版 + 全部 DLC + Harmony + 本模组，-gotest），检查从 LP 的 GeneWorkbenchChecks 搬过来，加一次存读档。结果 PASS 57 项，日志无报错。

## LichPhylactery 1.152.0
- 删除 Defs/GeneWorkbench.xml、Source/GeneWorkbench.cs、Source/GeneWorkbenchChecks.cs、Textures/GeneWorkbench/、研究 LP_GeneWorkbenchTech。
- Checks.cs 去掉 CheckGeneWorkbench / PrepareGeneWorkbenchSave / RewriteLegacyGeneWorkbench / CheckGeneWorkbenchAfterLoad；MarilithChecks、OozefolkChecks 里的 “可提取” 改成内联同一条原版规则；ResearchTab.Added 和 ResearchTabChecks 去掉这项研究；几处注释和 XML 注释改了说法。
- ArtSource/GeneWorkbench/draw_gene_workbench.py 和旧报告留着（历史资料）。
- 验证：Build.ps1 通过；Test.ps1 -Only Bank,Oozefolk,Marilith 83 项通过（含存读档）。
- 备份 ArtSource/backup-1.151.0-geneops（改动前的文件、旧 dll、旧贴图）。
