# LichPhylactery 贴图美化预览 Preview-1104 (2026-11-04)

本文件夹只是预览，没有改动 Textures\ 或任何备份。
- Textures\ ：按原相对路径镜像的 741 张新贴图 + 对应 _m 遮罩
- beautify.py ：生成脚本（模组根目录运行：E:\Python\python.exe ArtSource\Preview-1104\beautify.py [可选:文件名过滤]）
- metrics.json ：每张图前后的颜色数 / 细节密度
流程：读 backup-art-1102 原图（若备份比现有图小则读现有高清图）-> 物品/武器裁边并缩放到画布约 83%（穿戴图不移位，保证各朝向对齐）-> 超大图缩到 256 -> 中值滤波去细纹 -> 调色板压到 12 色（重灾区 7 色）无抖动 -> 用 ArtRedraw-1102\outline.py 补原版粗描边 -> 遮罩同步变换并在新描边处清零。
ElvenLongbow.png 为 AI 重画。

满意后应用（会覆盖 Textures 里同名文件，请先自行备份；在模组根目录的 PowerShell 中运行）：
Copy-Item -Path .\ArtSource\Preview-1104\Textures\* -Destination .\Textures\ -Recurse -Force

2026-11-04 剑类修正：DefenderSword/FrostBrand/SwordVengeance/VorpalSword/LP\Weapons\Legacy\Sword/EmpyreanBlade/MinotaurGith\Weapons\SilverSword 为 AI 重画（剑柄加长，握柄约 1/4~1/3）。SilverSword 原本不在 9 个文件夹内，这次新加入预览。
