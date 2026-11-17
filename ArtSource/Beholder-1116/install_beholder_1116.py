import sys
sys.stdout.reconfigure(encoding="utf-8")
src=open("E:/SteamLibrary/steamapps/common/RimWorld/Mods/LichPhylactery/ArtSource/Beasts-1115/install_beasts_1115.py",encoding="utf-8").read(); src=src[:src.index("res={}")]
OUT=sys.argv[1]; sys.argv=[sys.argv[0],OUT]; g={}; exec(src,g)
from PIL import Image,ImageDraw,ImageFont
SRC="E:/SteamLibrary/steamapps/common/RimWorld/output/LP-Beasts/LP_Beholder/LP_Beholder_"
TEX="E:/SteamLibrary/steamapps/common/RimWorld/Mods/LichPhylactery/Textures/LegendaryMonsters/Beholder/Naked_Hulk_"
new={};old={}
for v in ["south","east","north"]:
  o=Image.open(TEX+v+".png").convert("RGBA");bb=o.getchannel("A").getbbox();frac=max(bb[2]-bb[0],bb[3]-bb[1])/o.width
  old[v]=o;new[v]=g["fit"](g["key"](Image.open(SRC+v+".png")),o.width,min(frac,.96));new[v].save(f"{OUT}/Naked_Hulk_{v}.png");print(v,o.size,round(frac,2))
T=200;f=ImageFont.truetype("C:/Windows/Fonts/msyh.ttc",16)
sh=Image.new("RGB",(3*(T+8)+90,2*(T+30)+10),(30,30,34));d=ImageDraw.Draw(sh)
for r,(lab,ims) in enumerate([("旧",old),("新",new)]):
  d.text((10,r*(T+30)+T//2),lab,font=f,fill=(255,230,160))
  for j,v in enumerate(["south","east","north"]):
    bg=Image.new("RGBA",(T,T),(104,112,72,255));bg.alpha_composite(ims[v].resize((T,T),Image.LANCZOS));sh.paste(bg.convert("RGB"),(90+j*(T+8),r*(T+30)+24));d.text((90+j*(T+8),r*(T+30)+2),v,font=f,fill=(220,220,220))
sh.save(OUT+"/preview-beholder-1116.png");print("ok")
