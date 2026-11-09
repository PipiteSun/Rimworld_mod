# 1.133.0 魔力湖泊第三、四批图标：通用秘传（金色）和湖印（湖绿），画法同 draw_manalake.py 的能力图标（环数点 = 秘传环 / 湖印阶）。
# 用法（模组根目录）：$env:PATH="E:\Python;E:\Python\Library\bin;"+$env:PATH; E:\Python\python.exe ArtSource/ManaLake/draw_lore.py
# 图标列表来自 gen_lore.ps1 写出的 icons2.json。
import json, os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from draw_manalake import ability_icon, FONT
from PIL import ImageFont

def main():
    font = ImageFont.truetype(FONT, 250)
    with open(os.path.join(os.path.dirname(os.path.abspath(__file__)), "icons2.json"), encoding="utf-8-sig") as f:
        entries = json.load(f)
    for e in entries:
        ability_icon(e, font)
    print("icons:", len(entries))

if __name__ == "__main__":
    main()
