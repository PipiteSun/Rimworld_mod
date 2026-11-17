# 1.163.1 叙事者列表小头像

- 问题：LP_DungeonMasterTiny.png 是整张立绘缩小，列表里人很小，和原版叙事者的脸部特写不一致。
- 处理：从原画 output\AH-NewEquip\Storytellers\AH_Thalia.png（1536²）裁脸部，116x124，眉到下巴撑满、眼睛约 1/3 高。大立绘不变，无代码改动。
- 命令：`python ArtSource\Storyteller\make_tiny.py <原画> Textures\UI\HeroArt\Storytellers\LP_DungeonMasterTiny.png 648 375 260`
- 旧图备份：ArtSource\backup-1.163.0-tiny\
- 同批修了异象人形的伊瑟（1.14.1，裁剪参数 678 632 240）。
