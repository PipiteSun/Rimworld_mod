import sys,glob,os,re
from PIL import Image,ImageDraw,ImageFont
S=os.path.dirname(os.path.abspath(__file__)); sys.path.insert(0,S)
from geneart import ICONS
import geneart2
done={k for k in ICONS}|{k.split("Textures/",1)[1] for k in geneart2.ICONS2 if k.startswith("Lich")}
F=ImageFont.truetype("C:/Windows/Fonts/msyh.ttc",13)
defs="".join(open(f,encoding="utf-8",errors="ignore").read() for f in glob.glob("Defs/**/*.xml",recursive=True))
lab={}
for m in re.finditer(r"<GeneDef[^>]*>(.*?)</GeneDef>",defs,re.S):
    b=m.group(1); i=re.search(r"<iconPath>(.*?)</iconPath>",b); l=re.search(r"<label>(.*?)</label>",b)
    if i and l: lab.setdefault(i.group(1),l.group(1))
B="ArtSource/backup-geneicons-1117/Textures/"
fs=[]
for d in sys.argv[2:]:
    fs+=sorted(glob.glob(B+d+"/*.png"))
fs=[f.replace(chr(92),"/") for f in fs]; fs=[f for f in fs if f[len(B):-4] not in done]
print(len(fs))
cols=11; rows=(len(fs)+cols-1)//cols
s=Image.new("RGBA",(cols*164,rows*184),(52,58,64,255)); d=ImageDraw.Draw(s)
for i,f in enumerate(fs):
    k=f[len(B):-4]; x=(i%cols)*164+2; y=(i//cols)*184+2
    s.alpha_composite(Image.open(f).convert("RGBA").resize((160,160)),(x,y)); d.text((x,y+162),os.path.basename(k)[:12]+" "+lab.get(k,"?"),font=F,fill=(220,220,220))
s.save(sys.argv[1])
