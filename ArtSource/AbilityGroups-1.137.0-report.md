# 1.137.0 技能分组 报告

## 玩法
- 能力 ≥ 6 个（或已经建了分组）的玩家角色，技能栏最后多一个「技能分组：全部」按钮（三张法术卡图标）。
  - 左键：切换下一组（全部 → 各分组 → 全部）。没有分组时左键 / 右键都打开设置窗口。
  - 右键：打开分组设置窗口（不用浮动菜单）。
- 法术书（备法）、施法职业「的法术」窗口、职业窗口的标题栏右边有「技能分组…」按钮。
- 设置窗口：左栏「全部（N）」+ 各分组（新建、删除点两次、上移 / 下移，金色 = 技能栏当前组）；右栏改名（16 字以内）、「技能栏只显示这一组」、全选 / 全不选 / 只看还没分组的，下面是这个人全部有按钮的能力（按职业排，被动职业能力不列），勾选是否在本组。一个能力可以在多个组里。
- 选了某组：技能栏只显示该组的能力按钮（所有 Command_Ability：DND 职业能力、法术、原版心灵能力、基因能力）；征召、攻击、备法等其他按钮不受影响。
- 只影响显示：不在当前组的能力照常冷却；开着自动施放的照样自动放；不改 Spellbook.Hidden / CanCast。自动施放角标（15px）照旧。

## 实现
- Source/AbilityGroups.cs：数据（GameComponent_AbilityGroups，按 pawn thingIDNumber 存，组内存 defName；空的不存，读档不清理“已不存在的人”以免误删冬眠舱 / 运输舱里的人）、Pawn.GetGizmos 后缀（Priority.Last）、Command_AbilityGroup、Dialog_AbilityGroups。
- 热路径：每个选中的人每帧一次字典查找 + 当前组 HashSet<AbilityDef> 查找；集合只在改分组时重建。实测同一人 9 个能力：有分组 0.052 ms / 全部 0.047 ms 每次 GetGizmos。
- 改动：WizardSpellbook.cs、CasterSpells.cs、Classes.cs 各一行（窗口按钮）；Checks.cs 两行（截图挂钩）。
- 备份：ArtSource/backup-1.136.0-abilitygroups（旧 dll + 改动前的源文件、About.xml、使用说明.txt）。

## 测试
- `Test.ps1 -Only AbilityGroups`：8 条全过（无分组原样 + 切换按钮；当前组过滤且保留自动施放按钮类；循环切换 / 空组 / 一能力多组；删除移动时当前组指针；过滤后仍可施放；过滤后自动施放照常；耗时；存读档）。
- 回归 `-Only AutoCast,Convenience,CasterSpells`：33 条全过。
- 截图（StandaloneMods/Validation/Lich-Core-Only）：AG-Bar-Heal.png、AG-Bar-All.png、AG-Window.png、AG-CasterSpells.png。

## 未在实机验证
- Peter 完整模组列表下的实际游戏（其他模组加的能力按钮、VPE 灵能——VPE 灵能不是原版 Command_Ability，不受分组影响，用 VPE 自己的灵能组）。
- 多选几个人时每人各显示一个切换按钮（不合并）。
- 设置窗口里改名时的中文输入法。
