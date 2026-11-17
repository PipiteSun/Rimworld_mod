import sys,os; sys.path.insert(0,r"C:/Users/25390/AppData/Local/Temp/claude/471c1c3823b0935b/5cb7e058-ef4d-4f07-8ae8-c86b6478a7a9/scratchpad")
from geneart2 import ICONS2
from PIL import Image
os.makedirs(r"C:/Users/25390/AppData/Local/Temp/claude/471c1c3823b0935b/5cb7e058-ef4d-4f07-8ae8-c86b6478a7a9/scratchpad/new2",exist_ok=True)
ks=list(ICONS2); cols=9; s=Image.new("RGBA",(cols*132+4,2*268),(52,58,64,255))
for i,k in enumerate(ks):
    mod,rel=k.split("/",1)
    x=(i%cols)*132+4; y=(i//cols)*268
    s.alpha_composite(Image.open(f"{mod}/ArtSource/backup-geneicons-1117/{rel}.png").convert("RGBA").resize((128,128)),(x,y+4))
    im=ICONS2[k][1]().render(); im.save(r"C:/Users/25390/AppData/Local/Temp/claude/471c1c3823b0935b/5cb7e058-ef4d-4f07-8ae8-c86b6478a7a9/scratchpad/new2/"+k.replace("/","_")+".png"); s.alpha_composite(im,(x,y+136))
s.save(r"C:/Users/25390/AppData/Local/Temp/claude/471c1c3823b0935b/5cb7e058-ef4d-4f07-8ae8-c86b6478a7a9/scratchpad/v5_batch2.png")
