# 2026-11-02 把 1.77.0 画的 64 像素枪械 / 法杖改为 128 像素重画（原版武器贴图是 128，64 的在游戏里放大后发糊、出锯齿）。
# 画法沿用 ArtRedraw-1030\weapons.py 的部件函数；主轴坐标 ×2，构图、朝向、长度不变，drawSize 不变所以游戏里尺寸不变。
# 用法：E:\Python\python.exe ArtSource\ArtRedraw-1102\weapons_hd.py <模组根目录> <输出目录>
import json, os, sys

HERE = os.path.dirname(os.path.abspath(__file__))
R1030 = os.path.join(os.path.dirname(HERE), "ArtRedraw-1030")
sys.path.insert(0, R1030)
ARGS = sys.argv[1:]
sys.argv = sys.argv[:2]                          # weapons.py 在导入时读 argv（第 2 个以后会被当成 defName 过滤）
import weapons as wp                             # noqa: E402


def render_all(out_dir, size=128):
    rows = json.load(open(os.path.join(R1030, "weapons_axes.json"), encoding="utf8"))
    done = {}
    for r in rows:
        if r["W"] >= size or r["def_"] in wp.SHARED or r["path"] in done.values():
            continue
        name = r["def_"][3:]
        c = wp.Canvas(size, size, seed=hash(name) & 0xffff)
        # weapons_axes.json 的主轴坐标本来就按 128 归一，128 画布上直接用
        w = wp.W(c, tuple(r["p0"]), tuple(r["p1"]), size / 128, wp.VS.get(name, 1.75))
        wp.AXIAL[name](w, w.L)
        rel = os.path.relpath(r["path"], r["path"].split("\\Textures\\")[0])
        dst = os.path.join(out_dir, rel)
        os.makedirs(os.path.dirname(dst), exist_ok=True)
        c.save(dst)
        done[r["def_"]] = r["path"]
        print("hd", r["def_"], "->", rel)
    return done


if __name__ == "__main__":
    render_all(ARGS[1] if len(ARGS) > 1 else os.path.join(HERE, "hd"))
