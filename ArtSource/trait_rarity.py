# 2026-10-29 特性稀有度（1.66.0 热修）：表 → XML（TraitRarityExt + 标签颜色）。用法：python trait_rarity.py Defs\HeroTraits.xml ...（可重复跑）
import re,sys,os
HEX={"Common":None,"Uncommon":"#7FBF6A","Rare":"#5B9BD5","Epic":"#B070E0","Legendary":"#E08A3A","Mythic":"#E0B040"}
MARK={"Curse":"#D05050","Deed":"#50C0C0"}
T={}
def add(names,tier,kind,mark=None,degrees=None):
    for n in names.split(): T[n]=(tier,kind,mark,degrees or {})
P="性格特性"
add("LP_Order LP_Morality LP_RaceAttitude LP_Magephobe LP_ArcaneCraving LP_PlaneTouched LP_GithExile LP_DragonServant","Common",P)
add("LP_DragonStance","Common",P,degrees={"1":("Uncommon",None)})
add("LP_GiantStance LP_UndeadStance","Common",P,degrees={"-1":("Uncommon",None)})
add("LP_HiveSurvivor LP_TreasureHunter LP_Tactician LP_ArcaneGift LP_WildMagic LP_Tinkerer LP_NatureBond","Uncommon",P)
add("LP_Devotee LP_Atheist","Common","信仰")
add("LP_Background_Acolyte LP_Background_Criminal LP_Background_FolkHero LP_Background_Noble LP_Background_Sage LP_Background_Soldier LP_Background_Hermit LP_Background_Outlander","Uncommon","背景特性")
add("LP_GiftedScore LP_Hardened LP_Edge LP_Insight LP_LegendSeed LP_TotemSpirit LP_FavoredEnemy LP_Attuner LP_ElementTouched LP_SpellWarded LP_Duelist","Rare","强力特性")
add("LP_CG_Mighty LP_CG_Swift LP_CG_Stalwart LP_CG_Brilliant LP_CG_Eagle LP_CG_Commanding LP_CG_Flurry LP_CG_Rage LP_CG_Deadeye LP_CG_Lethal LP_CG_Bulwark LP_CG_Regen LP_CG_Arcane","Rare","武艺天赋")
add("LP_DefyFate LP_Diligent LP_TalentGifted LP_TalentLateBloomer LP_TalentTempered LP_TalentMortalWill","Epic","强力特性")
add("LP_HeroicSpark LP_Paragon","Legendary","强力特性")
add("LP_DivineInfusion","Legendary","神力灌注")
add("LP_Prophesied LP_PactDestiny LP_WeaveChild LP_DivineChampion","Mythic","天命")
add("LP_Revenant","Epic","诅咒之力","Curse",{"1":("Uncommon","None")})
add("LP_Lycanthrope","Epic","诅咒之力","Curse")
add("LP_FarRealmTouched","Rare","诅咒之力","Curse")
add("LP_DeedDragonslayer LP_Giantbane LP_Lichbane LP_LastStand LP_Godslayer","Rare","传说事迹","Deed")
add("LP_DeedGodslayer LP_MindFortress","Epic","传说事迹","Deed")
add("LP_DeathReturned LP_Saint","Uncommon","传说事迹","Deed")

def color(dn,deg):
    tier,kind,mark,dg=T[dn]
    if deg in dg:
        tier=dg[deg][0]
        if dg[deg][1] is not None: mark=None if dg[deg][1]=="None" else dg[deg][1]
    return MARK[mark] if mark else HEX[tier]

def ext(dn):
    tier,kind,mark,dg=T[dn]
    s='<li Class="LichPhylactery.TraitRarityExt"><tier>%s</tier><kind>%s</kind>'%(tier,kind)
    if mark: s+='<mark>%s</mark>'%mark
    if dg: s+='<degrees>'+''.join('<li>%s:%s%s</li>'%(k,v[0],(':'+v[1]) if v[1] else '') for k,v in dg.items())+'</degrees>'
    return s+'</li>'

def plain(lab):
    return re.sub(r'<[^>]+>','',re.sub(r'<!\[CDATA\[|\]\]>','',lab))

seen=set()
def fix_block(b):
    m=re.search(r'<defName>(.*?)</defName>',b); dn=m.group(1)
    if dn not in T: return b
    seen.add(dn)
    if 'TraitRarityExt' in b: b=re.sub(r'\s*<li Class="LichPhylactery\.TraitRarityExt">.*?</li>(?=\s*(<li|</modExtensions>))','',b,flags=re.S)  # 重跑时先去掉
    # 标签
    dd=re.search(r'<degreeDatas>(.*)</degreeDatas>',b,re.S)
    inner=dd.group(1)
    def fixli(mm):
        li=mm.group(0)
        g=re.search(r'<degree>(-?\d+)</degree>',li); deg=g.group(1) if g else "0"
        def lab(x):
            t=plain(x.group(1)); c=color(dn,deg)
            return '<label>'+('<![CDATA[<color=%s>%s</color>]]>'%(c,t) if c else t)+'</label>'
        return re.sub(r'<label>(.*?)</label>',lab,li,count=1,flags=re.S)
    new=re.sub(r'<li>.*?</li>(?=\s*(<li>|$))',fixli,inner,flags=re.S)
    b=b[:dd.start(1)]+new+b[dd.end(1):]
    # modExtension（TraitDef 一级）
    dd=re.search(r'<degreeDatas>.*</degreeDatas>',b,re.S)
    me=[x for x in re.finditer(r'<modExtensions>',b) if not (dd and dd.start()<x.start()<dd.end())]
    if me:
        i=me[0].end(); b=b[:i]+'\n      '+ext(dn)+b[i:]
    else:
        i=b.rstrip().rfind('\n') if False else len(b.rstrip())
        b=b.rstrip()+'\n    <modExtensions>\n      '+ext(dn)+'\n    </modExtensions>\n  '
    return b

for f in sys.argv[1:]:
    s=open(f,encoding='utf-8-sig',newline='').read();nl='\r\n' if '\r\n' in s else '\n'
    bom=open(f,'rb').read(3)==b'\xef\xbb\xbf'
    n=re.sub(r'(<TraitDef[^>]*>)(.*?)(</TraitDef>)',lambda m:m.group(1)+fix_block(m.group(2)).replace('\r\n','\n').replace('\n',nl)+m.group(3),s,flags=re.S)
    open(f,'w',encoding='utf-8-sig' if bom else 'utf-8',newline='').write(n)
missing=set(T)-seen
print("done",len(seen),"missing",missing)
