import sys,os
sys.path.insert(0,os.path.dirname(__file__))
from geneart import ICONS
from PIL import Image,ImageDraw,ImageFont
S=os.path.dirname(__file__)
R="E:/SteamLibrary/steamapps/common/RimWorld/"
L=R+"Mods/LichPhylactery/Textures/"
V=R+"_VanillaTextures/Biotech/UI/Icons/Genes/"
F=ImageFont.truetype("C:/Windows/Fonts/msyh.ttc",15); FB=ImageFont.truetype("C:/Windows/Fonts/msyhbd.ttc",18)
items=list(ICONS.items())
news=[]
for k,(lab,fn) in items:
    im=fn().render(); im.save(f"{S}/new/{k.replace('/','_')}.png"); news.append(im)
W=150; cols=len(items)//2
bg=(38,42,46)
def cell(sheet,im,x,y,box=None):
    if box: ImageDraw.Draw(sheet).rectangle([x,y,x+127,y+127],fill=box)
    sheet.alpha_composite(im,(x,y))
H=40+ 2*(30+150+150) + 30+160+ 20
sheet=Image.new("RGBA",(cols*W+20,H),bg+(255,)); d=ImageDraw.Draw(sheet)
y=10
for half in range(2):
    sub=items[half*cols:(half+1)*cols]
    for i,(k,(lab,fn)) in enumerate(sub):
        x=10+i*W
        d.text((x+64,y+12),lab,font=F,fill=(230,230,230),anchor="mm")
        cell(sheet,Image.open(L+k+".png").convert("RGBA").resize((128,128)),x,y+28,(55,60,66))
        cell(sheet,news[half*cols+i],x,y+28+150,(55,60,66))
    d.text((4,y+28+60),"旧",font=FB,fill=(200,120,120)); d.text((4,y+28+150+60),"新",font=FB,fill=(130,200,130))
    y+=30+300+10
d.text((10,y),"原版 Biotech 基因图标（参照）",font=F,fill=(200,200,200))
van=["Gene_Firespew","Gene_FireResistant","Gene_Furskin","Gene_Darkvision","Gene_BodyHulk","Gene_Hemogenic"]
for i,v in enumerate(van):
    cell(sheet,Image.open(V+v+".png").convert("RGBA"),10+i*W,y+25,(55,60,66))
sheet.save(S+"/compare.png")
# in-game-size check (gene list renders ~ 64-90px)
small=Image.new("RGBA",(len(news)*70+10,160),bg+(255,))
for i,(k,_) in enumerate(items):
    small.alpha_composite(Image.open(L+k+".png").convert("RGBA").resize((64,64),Image.LANCZOS),(5+i*70,5))
    small.alpha_composite(news[i].resize((64,64),Image.LANCZOS),(5+i*70,85))
small.save(S+"/compare_small.png")
