import sys
sys.stdout.reconfigure(encoding="utf-8")
src=open("E:/SteamLibrary/steamapps/common/RimWorld/Mods/LichPhylactery/ArtSource/Beasts-1115/install_beasts_1115.py",encoding="utf-8").read(); src=src[:src.index("res={}")]
OUT=sys.argv[1]; sys.argv=[sys.argv[0],OUT]; g={}; exec(src,g)
import numpy as np
from scipy import ndimage as nd
from PIL import Image,ImageDraw,ImageFont,ImageFilter
SRC="E:/SteamLibrary/steamapps/common/RimWorld/output/LP-Beasts/LP_Demilich/LP_Demilich_"
TEX="E:/SteamLibrary/steamapps/common/RimWorld/Mods/LichPhylactery/Textures/LegendaryMonsters/Demilich/Naked_Hulk_"
def skull_only(img):
  # 头骨 = 最大那块深色描边的凸包；凸包外的尘旋、烟雾、原图光环都去掉
  from scipy.spatial import ConvexHull
  a=np.asarray(img).astype(np.float32);al=a[...,3]/255
  lum=a[...,:3].mean(-1);dark=(lum<70)&(al>.5)
  lab,n=nd.label(nd.binary_closing(dark,iterations=2));sizes=nd.sum(dark,lab,range(1,n+1));k=1+int(np.argmax(sizes))
  ys,xs=np.where(lab==k);pts=np.stack([xs,ys],1);h=ConvexHull(pts)
  m=Image.new("L",img.size,0);ImageDraw.Draw(m).polygon([tuple(map(float,pts[i])) for i in h.vertices],fill=255)
  m=m.filter(ImageFilter.MaxFilter(5))
  a[...,3]*=np.asarray(m)/255;return Image.fromarray(a.astype(np.uint8),"RGBA")
def ring(c,frac_w):
  # 半巫妖下方的淡绿浮空光环（自己画，干净半透明，替代原图的光环和尘旋）
  W=c.width;bb=c.getchannel("A").getbbox();cx=(bb[0]+bb[2])/2;y=bb[3]+W*.07
  lay=Image.new("RGBA",c.size,(0,0,0,0));d=ImageDraw.Draw(lay);rw=W*frac_w/2;rh=W*.035
  d.ellipse((cx-rw,y-rh,cx+rw,y+rh),outline=(170,235,200,200),width=max(2,W//64))
  lay=lay.filter(ImageFilter.GaussianBlur(W/256));glow=lay.filter(ImageFilter.GaussianBlur(W/64))
  out=Image.new("RGBA",c.size,(0,0,0,0));out.alpha_composite(glow);out.alpha_composite(lay);out.alpha_composite(c);return out
new={};old={}
for v in ["south","east","north"]:
  o=Image.open(TEX+v+".png").convert("RGBA");old[v]=o
  sk=skull_only(g["key"](Image.open(SRC+v+".png")))
  # 头骨占 62% 画幅、略偏上，下方留给光环（旧图头骨约占 60%）
  bb=sk.getchannel("A").point(lambda x:255 if x>24 else 0).getbbox();sk=sk.crop(bb);W=o.width;s=.62*W/max(sk.size)
  sk=sk.resize((round(sk.width*s),round(sk.height*s)),Image.LANCZOS)
  c=Image.new("RGBA",(W,W),(0,0,0,0));c.alpha_composite(sk,((W-sk.width)//2,int(W*.40-sk.height/2)))
  new[v]=ring(c,.42);new[v].save(f"{OUT}/Naked_Hulk_{v}.png")
T=200;f=ImageFont.truetype("C:/Windows/Fonts/msyh.ttc",16)
sh=Image.new("RGB",(3*(T+8)+90,2*(T+30)+10),(30,30,34));d=ImageDraw.Draw(sh)
for r,(lab,ims) in enumerate([("旧",old),("新",new)]):
  d.text((10,r*(T+30)+T//2),lab,font=f,fill=(255,230,160))
  for j,v in enumerate(["south","east","north"]):
    bg=Image.new("RGBA",(T,T),(104,112,72,255));bg.alpha_composite(ims[v].resize((T,T),Image.LANCZOS));sh.paste(bg.convert("RGB"),(90+j*(T+8),r*(T+30)+24));d.text((90+j*(T+8),r*(T+30)+2),v,font=f,fill=(220,220,220))
sh.save(OUT+"/preview-demilich-1116.png");print("ok")
