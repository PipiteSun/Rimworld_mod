import sys
out = sys.argv[1]
I = "  "
def li(d, label=None, cov=None, groups=(), depth=None, height=None, extra="", parts=None, lvl=0):
    p = I * lvl
    s = [p + "<li>", p + I + f"<def>{d}</def>"]
    if label: s.append(p + I + f"<customLabel>{label}</customLabel>")
    if cov is not None: s.append(p + I + f"<coverage>{cov}</coverage>")
    if height: s.append(p + I + f"<height>{height}</height>")
    if depth: s.append(p + I + f"<depth>{depth}</depth>")
    if extra: s += [p + I + x for x in extra.split("\n")]
    if groups:
        s.append(p + I + "<groups>")
        s += [p + I * 2 + f"<li>{g}</li>" for g in groups]
        s.append(p + I + "</groups>")
    if parts:
        s.append(p + I + "<parts>")
        for q in parts: s += q(lvl + 2)
        s.append(p + I + "</parts>")
    s.append(p + "</li>")
    return s

def L(*a, **k): return lambda lvl: li(*a, lvl=lvl, **k)

def arm(side, tier, upper):
    # side: 左/右; tier: 上/中/下
    left = side == "左"
    hand_group = ("LeftHand" if left else "RightHand") if upper else None
    fingers = []
    for name, cov in (("小指", .06), ("无名指", .07), ("中指", .08), ("食指", .07), ("拇指", .08)):
        g = ["Hands"] + ([hand_group] if hand_group else []) + (["MiddleFingers"] if (name == "中指" and upper) else [])
        fingers.append(L("Finger", f"{side}{tier}手{name}", cov, g))
    flip = "<flipGraphic>true</flipGraphic>" if left else ""
    anchor = (f"<woundAnchorTag>{'Left' if left else 'Right'}Shoulder</woundAnchorTag>" + ("\n" + flip if flip else "")) if upper else flip
    return L("Shoulder", f"{side}{tier}肩", .06, ["Shoulders"], extra=anchor, parts=[
        L("Clavicle", f"{side}{tier}锁骨", .09, ["Torso"], depth="Inside", height="Top", extra=flip),
        L("Arm", f"{side}{tier}臂", .77, ["Arms"], extra=flip, parts=[
            L("Humerus", f"{side}{tier}肱骨", .1, ["Arms"], depth="Inside"),
            L("Radius", f"{side}{tier}桡骨", .1, ["Arms"], depth="Inside"),
            L("Hand", f"{side}{tier}手", .14, ["Hands"], height="Bottom", extra=flip, parts=fingers)])])

def head():
    return L("Neck", None, .075, ["Neck"], height="Top", parts=[
        L("Head", None, .80, ["UpperHead", "FullHead", "HeadAttackTool"], parts=[
            L("Skull", None, .18, ["UpperHead", "Eyes", "FullHead"], depth="Inside", parts=[
                L("Brain", None, .8, ["UpperHead", "Eyes", "FullHead"])]),
            L("Eye", "左眼", .07, ["FullHead", "Eyes"], extra="<woundAnchorTag>LeftEye</woundAnchorTag>\n<flipGraphic>true</flipGraphic>\n<visibleHediffRots>\n  <li>South</li>\n  <li>West</li>\n</visibleHediffRots>"),
            L("Eye", "右眼", .07, ["FullHead", "Eyes"], extra="<woundAnchorTag>RightEye</woundAnchorTag>\n<visibleHediffRots>\n  <li>South</li>\n  <li>East</li>\n</visibleHediffRots>"),
            L("Ear", "左耳", .07, ["UpperHead", "FullHead"], extra="<flipGraphic>true</flipGraphic>"),
            L("Ear", "右耳", .07, ["UpperHead", "FullHead"]),
            L("Nose", None, .10, ["FullHead"]),
            L("Jaw", None, .15, ["Teeth", "FullHead", "Mouth"], parts=[L("Tongue", None, .001, ["FullHead"], depth="Inside")])])])

organs = [L("Ribcage", None, .036, ["Torso"], depth="Inside"), L("Sternum", None, .015, ["Torso"], depth="Inside"),
          L("Pelvis", None, .025, ["Torso"], depth="Inside", height="Bottom"), L("Spine", None, .025, ["Torso"], depth="Inside"),
          L("Stomach", None, .025, ["Torso"], depth="Inside"), L("Heart", None, .020, ["Torso"], depth="Inside"),
          L("Lung", "左肺", .025, ["Torso"], depth="Inside"), L("Lung", "右肺", .025, ["Torso"], depth="Inside"),
          L("Kidney", "左肾", .017, ["Torso"], depth="Inside"), L("Kidney", "右肾", .017, ["Torso"], depth="Inside"),
          L("Liver", None, .025, ["Torso"], depth="Inside"), head()]
arms = []
for tier, upper in (("上", True), ("中", False), ("下", False)):
    arms += [arm("左", tier, upper), arm("右", tier, upper)]
tail = L("LP_SerpentTail", None, .28, ["LP_SerpentTail"], height="Bottom", parts=[
    L("LP_SerpentTailMid", None, .45, ["LP_SerpentTail"], height="Bottom", parts=[
        L("LP_SerpentTailTip", None, .35, ["LP_SerpentTail"], height="Bottom")])])
waist = L("Waist", None, 0, ["Waist"], height="Bottom")

lines = ["  <!-- 六臂蛇人身体：头、颈、躯干、内脏照原版 Human；6 组肩/臂/手/手指复用原版 BodyPartDef（仿生臂等手术直接能用）；",
         "       蛇尾代替双腿（没有 Legs/Feet 部位组：鞋、裤、护腿穿不上）。6 条上肢命中权重合计约人类两臂的 1.5 倍，蛇尾约等于人类双腿。",
         "       本段由 ArtSource/Marilith-gen_body.py 生成。 -->",
         "  <BodyDef>", "    <defName>LP_MarilithBody</defName>", "    <label>六臂蛇人</label>", "    <corePart>",
         "      <def>Torso</def>", "      <height>Middle</height>", "      <depth>Outside</depth>", "      <groups>", "        <li>Torso</li>", "      </groups>", "      <parts>"]
for q in organs + arms + [waist, tail]:
    lines += q(4)
lines += ["      </parts>", "    </corePart>", "  </BodyDef>"]
open(out, "w", encoding="utf-8").write("\n".join("    " + x if False else x for x in lines) + "\n")
