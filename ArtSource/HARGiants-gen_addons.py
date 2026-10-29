import re, sys
path = sys.argv[1]
text = open(path, encoding="utf-8").read()

# 偏移（世界单位，身体中心为原点）：南/北两头并排 ±SEP；东向右头在近处（前、低、画在主头前），西向右头在远处（后、高、画在主头后）。
# HAR 对东向的偏移（附件和 headOffsetDirectional 都是）会把 x 取反，所以东向的 x 反着写。层：南北 -0.283（身体、衣服之上，主头之下），东 0（最前），西 -0.283。
SEP = 0.26
L_BACK = -0.283
def offsets(dl, child=False):
    k = 0.8 if child else 1.0
    y = 0.30 if child else 0.34
    s = (f"<south><offset>({-SEP*k:.3f}, {y:.2f})</offset><layerOffset>{L_BACK+dl:.4f}</layerOffset></south>"
         f"<north><offset>({SEP*k:.3f}, {y:.2f})</offset><layerOffset>{L_BACK+dl:.4f}</layerOffset></north>"
         f"<east><offset>({-0.16*k:.3f}, {y-0.07:.2f})</offset><layerOffset>{dl:.4f}</layerOffset></east>"
         f"<west><offset>({-0.06*k:.3f}, {y+0.10:.2f})</offset><layerOffset>{L_BACK+dl:.4f}</layerOffset></west>")
    return s

def addon(name, path_, cond, dl, color=None, shader=None, size=1.0, child=False):
    lines = ["            <li>", f"              <name>{name}</name>", f"              <path>{path_}</path>"]
    if color: lines.append(f"              <colorChannel>{color}</colorChannel>")
    if shader: lines.append(f"              <shaderType>{shader}</shaderType>")
    lines += [f"              <drawSize>({size}, {size})</drawSize>", "              <scaleWithPawnDrawsize>true</scaleWithPawnDrawsize>",
              "              <inFrontOfBody>true</inFrontOfBody>", "              <layerInvert>false</layerInvert>", "              <userCustomizable>false</userCustomizable>",
              "              <offsets>" + offsets(dl, child) + "</offsets>", "              <conditions>"]
    lines += ["                " + c for c in cond]
    lines += ["              </conditions>", "            </li>"]
    return "\n".join(lines)

part = "<BodyPart><bodyPart>LP_EttinHead</bodyPart></BodyPart>"
fresh = "<RotStage>Fresh,Rotting</RotStage>"
nohelm = "<Not><ApparelDef><apparel>LP_EttinTwinHelm</apparel></ApparelDef></Not>"
heads = [("A", "<HeadType>Male_AverageNormal</HeadType>"), ("B", "<HeadType>Male_AverageWide</HeadType>"),
         ("F", "<Or><HeadType>Female_AverageNormal</HeadType><HeadType>Female_AverageWide</HeadType></Or>")]
items = []
items.append("            <!-- 第二个头（右头）：挂在 LP_EttinHead 部位上，部位缺失时隐藏；头型决定款式（男两款、女一款），头用肤色通道、头发用发色通道。\n"
             "                 南北两头并排（主头在 HAR 的 headOffsetDirectional 里挪到另一边），画在身体和衣服之上、主头之下；东向右头在近处画在最前，西向在远处画在后面。\n"
             "                 layerInvert 关掉（HAR 默认北向把层翻到身体后面）。本段由 ArtSource/HARGiants-gen_addons.py 生成。 -->")
for k, cond in heads:
    items.append(addon("EttinSecondHead" + k, "HARGiants/Ettin/Addons/SecondHead" + k, [part, "<BodyType>Hulk</BodyType>", cond, fresh], 0, color="skin"))
for k, cond in heads:
    items.append(addon("EttinSecondHair" + k, "HARGiants/Ettin/Addons/SecondHair" + k, [part, "<BodyType>Hulk</BodyType>", cond, nohelm], 0.0004, color="hair"))
items.append("            <!-- 双盔的另一半（戴在第二个头上），不跟颜色通道：钢铁本色。 -->")
items.append(addon("EttinTwinHelmSecond", "HARGiants/Ettin/Addons/TwinHelmSecond", [part, "<Or><BodyType>Hulk</BodyType><BodyType>Child</BodyType></Or>", "<ApparelDef><apparel>LP_EttinTwinHelm</apparel></ApparelDef>"], 0.0008, shader="Cutout"))
items.append("            <!-- 儿童：同一套贴图缩小（近似对位）。 -->")
items.append(addon("EttinSecondHeadChild", "HARGiants/Ettin/Addons/SecondHeadA", [part, "<Or><BodyType>Child</BodyType><BodyType>Baby</BodyType></Or>", fresh], 0, color="skin", size=0.8, child=True))
block = "<bodyAddons Inherit=\"False\">\n" + "\n".join(items) + "\n          </bodyAddons>"

start = text.index("<bodyAddons Inherit=\"False\">")
end = text.index("</bodyAddons>", start) + len("</bodyAddons>")
text = text[:start] + block + text[end:]
# 主头：南北挪到另一边；东向主头在远处（后、高），西向在近处（前、低）。
text = re.sub(r"<headOffsetDirectional>.*?</headOffsetDirectional>",
              "<headOffsetDirectional>\n            <south><offset>(%.2f, 0)</offset></south>\n            <north><offset>(%.2f, 0)</offset></north>\n            <east><offset>(0.06, 0.12)</offset></east>\n            <west><offset>(-0.04, -0.07)</offset></west>\n          </headOffsetDirectional>" % (SEP, -SEP),
              text, count=1, flags=re.S)
open(path, "w", encoding="utf-8").write(text)
print("ok")
