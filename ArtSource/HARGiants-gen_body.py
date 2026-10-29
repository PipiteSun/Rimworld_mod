# 1.44.0 生成 Bridges/HAR/Defs/HARGiants_Bodies.xml：从原版 Human 身体（Data/Core/Defs/Bodies/Bodies_Humanlike.xml）复制，
#  双头巨人 LP_EttinBody：两个颈、两个头（主头=原版 Head「左头」，第二个头=LP_EttinHead「右头」，部位组只有 LP_EttinSecondHead，普通帽子盖不到）；
#  独眼巨人 LP_CyclopsBody：两只眼换成一只 LP_CyclopsEye「独眼」。
# 用法：python -I HARGiants-gen_body.py <RimWorld\Data\Core\Defs\Bodies\Bodies_Humanlike.xml> <输出 xml>
import copy, sys
import xml.etree.ElementTree as ET

src, out = sys.argv[1], sys.argv[2]
human = next(b for b in ET.parse(src).getroot().iter("BodyDef") if b.findtext("defName") == "Human")
core = human.find("corePart")

def children(rec):
    p = rec.find("parts")
    return [] if p is None else list(p)

def find(rec, d):
    return next(c for c in children(rec) if c.findtext("def") == d)

def set_text(rec, tag, text, after="def"):
    e = rec.find(tag)
    if e is None:
        e = ET.Element(tag)
        idx = list(rec).index(rec.find(after)) + 1
        rec.insert(idx, e)
    e.text = text

def set_groups(rec, groups):
    g = rec.find("groups")
    if g is None:
        g = ET.SubElement(rec, "groups")
    for x in list(g):
        g.remove(x)
    for name in groups:
        ET.SubElement(g, "li").text = name

def walk(rec):
    yield rec
    for c in children(rec):
        yield from walk(c)

PART_CN = {"Skull": "头骨", "Brain": "大脑", "Eye": "眼", "Ear": "耳", "Nose": "鼻子", "Jaw": "下颌", "Tongue": "舌头"}

def label_head(head, prefix):
    # 左头的左眼、右头的大脑……（原版左右眼、左右耳带 customLabel left/right；这里统一写中文）
    for r in walk(head):
        d = r.findtext("def")
        if r is head:
            continue
        cn = PART_CN.get(d, None)
        if cn is None:
            continue
        old = (r.findtext("customLabel") or "").lower()
        side = "左" if old.startswith("left") else "右" if old.startswith("right") else ""
        set_text(r, "customLabel", prefix + "的" + side + cn)

# 原版 Human 的 customLabel 是英文（中文靠原版 DefInjected 翻译 Human 身体），新身体要自己写中文。
EN_CN = {"lung": "肺", "ear": "耳", "eye": "眼", "kidney": "肾", "shoulder": "肩", "clavicle": "锁骨", "arm": "臂", "humerus": "肱骨", "radius": "桡骨", "hand": "手",
         "pinky": "手小指", "ring finger": "手无名指", "middle finger": "手中指", "index finger": "手食指", "thumb": "手拇指",
         "leg": "腿", "femur": "股骨", "tibia": "胫骨", "foot": "脚",
         "little toe": "脚小趾", "fourth toe": "脚第四趾", "middle toe": "脚中趾", "second toe": "脚第二趾", "big toe": "脚大趾"}

def translate(body):
    for r in walk(body.find("corePart")):
        e = r.find("customLabel")
        if e is None or not e.text or not e.text.isascii():
            continue
        t = e.text.lower()
        side = "左" if t.startswith("left ") else "右" if t.startswith("right ") else ""
        rest = t.split(" ", 1)[1] if side else t
        if rest not in EN_CN:
            raise SystemExit("untranslated body part label: " + e.text)
        e.text = side + EN_CN[rest]

# ---------- 双头巨人 ----------
ettin = copy.deepcopy(human)
ettin.find("defName").text = "LP_EttinBody"
ettin.find("label").text = "双头巨人"
eparts = ettin.find("corePart").find("parts")
neck = find(ettin.find("corePart"), "Neck")
second = copy.deepcopy(neck)
set_text(neck, "customLabel", "左颈")
head = find(neck, "Head")
set_text(head, "customLabel", "左头")
label_head(head, "左头")
set_text(second, "customLabel", "右颈")
shead = find(second, "Head")
shead.find("def").text = "LP_EttinHead"
set_text(shead, "customLabel", "右头")
label_head(shead, "右头")
for r in walk(shead):
    set_groups(r, ["LP_EttinSecondHead"])
    for tag in ("woundAnchorTag", "flipGraphic", "visibleHediffRots"):
        e = r.find(tag)
        if e is not None:
            r.remove(e)
eparts.insert(list(eparts).index(neck) + 1, second)

# ---------- 独眼巨人 ----------
cyc = copy.deepcopy(human)
cyc.find("defName").text = "LP_CyclopsBody"
cyc.find("label").text = "独眼巨人"
chead = find(find(cyc.find("corePart"), "Neck"), "Head")
cp = chead.find("parts")
eyes = [c for c in list(cp) if c.findtext("def") == "Eye"]
at = list(cp).index(eyes[0])
for e in eyes:
    cp.remove(e)
eye = ET.Element("li")
ET.SubElement(eye, "def").text = "LP_CyclopsEye"
ET.SubElement(eye, "coverage").text = "0.1"
g = ET.SubElement(eye, "groups")
for name in ("FullHead", "Eyes"):
    ET.SubElement(g, "li").text = name
cp.insert(at, eye)
translate(ettin)
translate(cyc)

def indent(e, level=0):
    pad = "\n" + "  " * level
    if len(e):
        if not (e.text or "").strip():
            e.text = pad + "  "
        for c in e:
            indent(c, level + 1)
        if not (c.tail or "").strip():
            c.tail = pad
    if level and not (e.tail or "").strip():
        e.tail = pad

defs = ET.Element("Defs")
defs.append(ET.Comment(" 1.44.0 双头巨人、独眼巨人的身体（只在启用 HAR 时加载）。本文件由 ArtSource/HARGiants-gen_body.py 从原版 Human 身体生成，不要手改。 "))
for xml in ("""<BodyPartGroupDef><defName>LP_EttinSecondHead</defName><label>第二个头</label><listOrder>205</listOrder></BodyPartGroupDef>""",
            """<BodyPartDef><defName>LP_EttinHead</defName><label>头</label><hitPoints>25</hitPoints><skinCovered>true</skinCovered><canScarify>true</canScarify><canSuggestAmputation>false</canSuggestAmputation><bleedRate>2</bleedRate><executionPartPriority>400</executionPartPriority></BodyPartDef>""",
            """<BodyPartDef><defName>LP_CyclopsEye</defName><label>独眼</label><hitPoints>15</hitPoints><permanentInjuryChanceFactor>15</permanentInjuryChanceFactor><delicate>true</delicate><skinCovered>false</skinCovered><canScarify>true</canScarify><socketed>true</socketed><beautyRelated>true</beautyRelated><tags><li>SightSource</li></tags><hitChanceFactors><li><key>Blunt</key><value>0</value></li></hitChanceFactors></BodyPartDef>"""):
    defs.append(ET.fromstring(xml))
defs.append(ettin)
defs.append(cyc)
indent(defs)
text = ET.tostring(defs, encoding="unicode")
open(out, "w", encoding="utf-8").write('<?xml version="1.0" encoding="utf-8"?>\n' + text + "\n")
print("ok", sum(1 for _ in walk(ettin.find("corePart"))), sum(1 for _ in walk(cyc.find("corePart"))))
