import json,sys,os,random,glob
from PIL import Image,ImageDraw,ImageFont
sys.path.insert(0,os.path.dirname(os.path.abspath(__file__)))
import restyle as R
MOD=os.path.abspath(os.path.join(os.path.dirname(__file__),'..','..'))
ab=json.load(open(os.path.join(os.path.dirname(__file__),'abil.json'),encoding='utf-8'))
skip=('Items/','LP/Weapons','LP/Apparel','Things/','Necromancy/Implants')
keys=sorted(k for k in ab if not k.startswith(skip))
random.seed(int(sys.argv[2]) if len(sys.argv)>2 else 7)
by={}
for k in keys: by.setdefault(os.path.dirname(k),[]).append(k)
pick=[]
for d in sorted(by): pick+=random.sample(by[d],min(2 if len(by[d])>8 else 1,len(by[d])))
random.shuffle(pick); pick=pick[:36]
VPE=sorted(glob.glob(r'E:/SteamLibrary/steamapps/workshop/content/294100/2842502659/Textures/Abilities/**/*.png',recursive=True))
random.seed(3); vref=random.sample(VPE,10)
sz=88; cols=12; pad=6
rows=len(pick)//4
H=40+sz+30+rows*(sz+pad)+20
img=Image.new('RGBA',(cols*(sz+pad)+pad,H),(52,54,56,255))
d=ImageDraw.Draw(img)
try: font=ImageFont.truetype('msyh.ttc',16)
except: font=ImageFont.load_default()
d.text((pad,10),'VPE 参考 (Vanilla Psycasts Expanded)',fill=(230,230,230),font=font)
for i,f in enumerate(vref):
    img.alpha_composite(Image.open(f).convert('RGBA').resize((sz,sz),Image.LANCZOS),(pad+i*(sz+pad),34))
y0=34+sz+12
d.text((pad,y0),'DND 图标：每组 = 现在 | 方案A（保留蓝灰人形） | 方案B（人形改VPE卡其色）',fill=(230,230,230),font=font)
y0+=24
for i,k in enumerate(pick):
    src=Image.open(os.path.join(MOD,ab[k][0])).convert('RGBA')
    R.KHAKI_FIG=False; a=R.restyle(src)
    R.KHAKI_FIG=True; b=R.restyle(src)
    c=(i%4)*3; r=i//4
    x=pad+c*(sz+pad); y=y0+r*(sz+pad)
    d.rectangle([x-3,y-3,x+3*sz+2*pad+2,y+sz+2],outline=(80,82,86))
    for j,im in enumerate((src,a,b)):
        img.alpha_composite(im.resize((sz,sz),Image.LANCZOS),(x+j*(sz+pad),y))
img.save(sys.argv[1]); print(len(pick),'icons')
