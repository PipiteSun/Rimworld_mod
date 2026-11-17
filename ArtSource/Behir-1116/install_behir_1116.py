import sys,os,importlib.util
sys.stdout.reconfigure(encoding="utf-8")
spec=importlib.util.spec_from_file_location("b","E:/SteamLibrary/steamapps/common/RimWorld/Mods/LichPhylactery/ArtSource/Beasts-1115/install_beasts_1115.py")
src=open(spec.origin,encoding="utf-8").read(); src=src[:src.index("res={}")]
sys.argv=[sys.argv[0],sys.argv[1]]; g={}; exec(src,g)
from PIL import Image,ImageDraw,ImageFont
OUT=sys.argv[1]; TEX="E:/SteamLibrary/steamapps/common/RimWorld/Mods/LichPhylactery/Textures/LegendaryMonsters/Behir"
img=g["fit"](g["key"](Image.open("E:/SteamLibrary/steamapps/common/RimWorld/output/LP-Beasts/LP_Behir/LP_Behir-east.png")),256,0.98)
for v in ["east","south","north"]: img.save(f"{OUT}/Naked_Hulk_{v}.png")
T=240; f=ImageFont.truetype("C:/Windows/Fonts/msyh.ttc",16)
sh=Image.new("RGB",(4*(T+8)+8,T+44),(30,30,34)); d=ImageDraw.Draw(sh)
ims=[Image.open(f"{TEX}/Naked_Hulk_{v}.png").convert("RGBA") for v in ["south","east","north"]]+[img]
for j,(h,im) in enumerate(zip(["旧 south","旧 east","旧 north","新（四向通用）"],ims)):
  d.text((8+j*(T+8),8),h,font=f,fill=(255,230,160)); bg=Image.new("RGBA",(T,T),(104,112,72,255)); bg.alpha_composite(im.resize((T,T),Image.LANCZOS)); sh.paste(bg.convert("RGB"),(8+j*(T+8),36))
sh.save(OUT+"/preview-behir-1116.png"); print("ok")
