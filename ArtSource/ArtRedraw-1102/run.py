# 2026-11-02 DND 装备贴图原版化：备份 → 64 像素武器高清重画 → 补原版粗外描边 → 对比图。
# 用法（模组根目录下）：
#   $env:PATH="E:\Python;E:\Python\Library\bin;"+$env:PATH
#   E:\Python\python.exe ArtSource\ArtRedraw-1102\run.py            # 写回 Textures
#   E:\Python\python.exe ArtSource\ArtRedraw-1102\run.py --preview  # 只写到 ArtRedraw-1102\preview 看效果
import json, os, re, shutil, subprocess, sys

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(os.path.dirname(HERE))
BACKUP = os.path.join(ROOT, "ArtSource", "backup-art-1102")
PREVIEW = "--preview" in sys.argv
OUTROOT = os.path.join(HERE, "preview") if PREVIEW else ROOT
sys.path.insert(0, HERE)
import outline  # noqa: E402

# 补丁里改过穿戴图路径的三件（scan 只读 Defs）
PATCHED_WORN = ["Textures/LP/Apparel/ElvenChain", "Textures/LP/Apparel/GithAstralArmor", "Textures/LP/Apparel/MinotaurHornHelm"]
SKIP = re.compile(r"\\Projectiles\\|\\Proj_|_m\.png$|(north|south|east|west)m\.png$", re.I)


def targets():
    lst = os.path.join(HERE, "equip.json")
    subprocess.run([sys.executable, "-I", os.path.join(HERE, "scan.py"), ROOT, lst], check=True)
    files = {p: v[0] for p, v in json.load(open(lst, encoding="utf8")).items()}
    for d in PATCHED_WORN:
        full = os.path.join(ROOT, d.replace("/", "\\"))
        for f in os.listdir(full):
            if f.endswith(".png"):
                p = os.path.join(full, f)
                files.setdefault(p, "icon" if "_Icon" in f else "worn")
    return {p: r for p, r in files.items() if not SKIP.search(p) and os.path.exists(p)}


def mask_of(p):
    b = p[:-4]
    for m in (b + "_m.png", b + "m.png"):
        if os.path.exists(m):
            return m
    return None


def rel(p):
    return os.path.relpath(p, ROOT)


def main():
    files = targets()
    # 1) 备份（只在第一次运行时拷）
    n = 0
    for p in list(files) + [m for m in map(mask_of, files) if m]:
        b = os.path.join(BACKUP, rel(p))
        if not os.path.exists(b):
            os.makedirs(os.path.dirname(b), exist_ok=True)
            shutil.copy2(p, b)
            n += 1
    print("backup new", n, "->", BACKUP)
    # 2) 64 像素武器高清重画（输出到临时目录，作为描边的输入）
    hd_dir = os.path.join(HERE, "hd")
    sys.argv = [sys.argv[0], ROOT]
    import weapons_hd
    hd = weapons_hd.render_all(hd_dir)
    hd_src = {os.path.normpath(p): os.path.join(hd_dir, os.path.relpath(p, p.split("\\Textures\\")[0])) for p in hd.values()}
    # 3) 描边
    log = []
    for p, role in sorted(files.items()):
        src = hd_src.get(os.path.normpath(p), os.path.join(BACKUP, rel(p)))
        dst = os.path.join(OUTROOT, rel(p))
        os.makedirs(os.path.dirname(dst), exist_ok=True)
        m = mask_of(p)
        ms = os.path.join(BACKUP, rel(m)) if m else None
        md = os.path.join(OUTROOT, rel(m)) if m else None
        e, r = outline.process(src, dst, role, ms, md)
        log.append({"file": rel(p), "role": role, "had": None if e is None else round(float(e), 2), "added": round(float(r), 2),
                    "hd": src.startswith(hd_dir)})
    json.dump(log, open(os.path.join(HERE, "log.json"), "w", encoding="utf8"), ensure_ascii=False, indent=0)
    changed = sum(1 for x in log if x["added"] > 0 or x["hd"])
    print("files", len(log), "changed", changed)


if __name__ == "__main__":
    main()
