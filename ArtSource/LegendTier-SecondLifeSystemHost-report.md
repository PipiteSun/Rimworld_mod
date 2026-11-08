# 热修：二周目 / 系统宿主 改为传说档（版本号保持 1.127.0）

日期：2026-11-08（Peter 要求）

## 档位变化
| 特性 | defName | 原档位 | 新档位 | 标签颜色 |
|---|---|---|---|---|
| 二周目 | LP_SecondLife | 稀有 Rare（蓝 #5B9BD5） | 传说 Legendary | 橙 #E08A3A |
| 系统宿主 | LP_SystemHost | 史诗 Epic（紫 #B070E0） | 传说 Legendary | 橙 #E08A3A |

两个特性都只有一个程度；kind 仍为「奇遇特性」。出现率（commonality 0.1 / 0.05）和所有数值未改。

## 改动文件
- `Defs/SecondLifeSystemHost.xml`：TraitRarityExt `<tier>Legendary</tier>`，标签 `<color=#E08A3A>`，文件头注释
- `ArtSource/trait_rarity.py`：表里两个特性改为 Legendary（重跑脚本不会改回）
- `Source/SecondLifeChecks.cs`：唯一硬编码旧档位的检查（第 32–34 行）改为断言传说档 / #E08A3A
- `Source/SecondLife.cs`、`Source/SystemHost.cs`：只改注释里的档位
- `About/About.xml`、`使用说明.txt`：1.122.0 说明里的档位文字改为传说
- 备份：`ArtSource/backup-1.127.0-legendtier/`（About.xml 和 使用说明.txt 的备份是在改动后反向还原写出的，内容与原文一致，编码为无 BOM UTF-8）

## 改命符
`SystemHostFate.cs` 按 TraitRarity 读取档位，没有硬编码。系统宿主本身仍然锁定、不能改写；二周目现在只能和其他传说档特性互换（例如英雄之资、极致）。

## 编译与测试
- `Build.ps1`：PASS（主程序集和 HAR / VPE / Mythic 桥接）
- `-Only TraitRarity`：PASS 7
- `-Only SystemHost`：PASS 42
- `-Only SecondLife`：第一次失败，因为检查硬编码了旧档位。修改该检查后 PASS 16
