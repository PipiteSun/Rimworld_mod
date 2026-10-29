# 1.57.0 半精灵"多才多艺"：两组互斥天赋基因 LP_HalfElfTalentA_<技能> / LP_HalfElfTalentB_<技能>，12 技能各一。
# 与原版强天赋相同：能力 +4，代谢 -1，复杂度 2。图标沿用原版 UI/Icons/Genes/Skills/<技能>/Strong。
# 运行：python gen_halfelf_talents_157.py  → 覆盖 Defs/CommonRaces_HalfElfTalents.xml
import os
SKILLS = [("Shooting", "射击"), ("Melee", "近战"), ("Construction", "建造"), ("Mining", "采矿"),
          ("Cooking", "烹饪"), ("Plants", "种植"), ("Animals", "驯兽"), ("Crafting", "手工"),
          ("Artistic", "艺术"), ("Medicine", "医疗"), ("Social", "社交"), ("Intellectual", "智识")]
GROUPS = [("A", "第一"), ("B", "第二")]
out = ['<?xml version="1.0" encoding="utf-8"?>',
       '<!-- 由 ArtSource/RaceArt/gen_halfelf_talents_157.py 生成，不要手改。1.57.0 半精灵"多才多艺"天赋基因。 -->',
       '<Defs>']
for g, gl in GROUPS:
    for s, sl in SKILLS:
        out += [
            '  <GeneDef>',
            f'    <defName>LP_HalfElfTalent{g}_{s}</defName>',
            f'    <label>多才多艺：{sl}（{gl}项）</label>',
            f'    <description>半精灵的{gl}项天赋：{sl}能力+4（能力像技能等级的加值）。同一组的天赋互斥；两组各选一项，生成的半精灵两项天赋不会相同。</description>',
            f'    <iconPath>UI/Icons/Genes/Skills/{s}/Strong</iconPath>',
            '    <displayCategory>Aptitudes</displayCategory>',
            f'    <exclusionTags><li>LP_HalfElfTalent{g}</li></exclusionTags>',
            f'    <aptitudes><{s}>4</{s}></aptitudes>',
            '    <biostatCpx>2</biostatCpx>',
            '    <biostatMet>-1</biostatMet>',
            '    <selectionWeight>0</selectionWeight>',
            '  </GeneDef>']
out.append('</Defs>')
here = os.path.dirname(os.path.abspath(__file__))
path = os.path.join(here, '..', '..', 'Defs', 'CommonRaces_HalfElfTalents.xml')
with open(path, 'w', encoding='utf-8', newline='\n') as f:
    f.write('\n'.join(out) + '\n')
print('wrote', os.path.normpath(path), len(GROUPS) * len(SKILLS), 'genes')
