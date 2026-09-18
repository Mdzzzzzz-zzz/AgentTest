import os, re, collections

R = r"D:\NewOne\Client-Dice"
targets = [
    "fc234e7c22b37ad4e91dc1be3614dc5f",   # 场景引用 54 次，type:2 材质
    "b88ecb60d95a1634aa03daecb10559d0",   # 场景引用 1 次，m_Script
    "5c8b75a6a56602d408693f01a692bfb4",
    "ee4ddb14ce56b67479a3cc0da06d495e",
    "da333294192d00c4bb1c5d2208ec1cfd",
    "61c2201f570a24b4585ea0871053be99",
    "6deed5a23c6bbcd4e947152c06d5a850",
]

# 全项目字节级搜索（含 Library / Packages / Temp）
roots = ["Assets", "Packages", "Library", "Temp"]
hitmap = collections.defaultdict(list)
scanned = 0
for r in roots:
    base = os.path.join(R, r)
    if not os.path.isdir(base):
        continue
    for root, dirs, files in os.walk(base):
        for f in files:
            p = os.path.join(root, f)
            try:
                if os.path.getsize(p) > 200 * 1024 * 1024:
                    continue
                b = open(p, "rb").read()
            except Exception:
                continue
            scanned += 1
            try:
                s = b.decode("latin-1")
            except Exception:
                continue
            for t in targets:
                if t in s:
                    hitmap[t].append((os.path.relpath(p, R), s.count(t)))
print("扫描文件数:", scanned)
print()
for t in targets:
    hits = hitmap.get(t, [])
    print(f"=== {t} -> {len(hits)} 个文件命中 ===")
    for p, c in hits[:8]:
        print(f"    {c:>4}x  {p}")
    if not hits:
        print("    （全项目零命中）")
    print()

# 两种 guid 形态分别对应哪些扩展名
form = collections.defaultdict(collections.Counter)
for root, dirs, files in os.walk(os.path.join(R, "Assets")):
    for f in files:
        if not f.endswith(".meta"):
            continue
        p = os.path.join(root, f)
        t = open(p, "rb").read().decode("utf-8-sig", "replace")
        m = re.search(r"guid:\s*([^\s\r\n]+)", t)
        if not m:
            continue
        v = m.group(1)
        k = "base64" if v.endswith("=") else ("hex32" if re.fullmatch(r"[0-9a-fA-F]{32}", v) else "other")
        # 该 meta 描述的资产类型 = 去掉 .meta
        desc = f[:-5]
        _, ext = os.path.splitext(desc)
        form[k][ext or "(无扩展名)"] += 1
print("=== guid 形态 x 资产扩展名 ===")
for k in ("base64", "hex32", "other"):
    if k in form:
        print(f"  [{k}] 共 {sum(form[k].values())} 个")
        for ext, c in form[k].most_common(12):
            print(f"        {ext:<16} {c}")
