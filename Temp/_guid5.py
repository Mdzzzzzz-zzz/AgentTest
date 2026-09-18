import base64, re, os

R = r"D:\NewOne\Client-Dice"
scene = os.path.join(R, r"Assets\Scenes\BattleM1.unity")
s = open(scene, "rb").read().decode("utf-8-sig", "replace")
TOK = re.compile(r"guid:\s*([^\s,}\r\n]+)")
scene_guids = list(dict.fromkeys(TOK.findall(s)))

# 所有 .meta 的 guid（原样）
metas = {}
for root, dirs, files in os.walk(os.path.join(R, "Assets")):
    for f in files:
        if not f.endswith(".meta"):
            continue
        p = os.path.join(root, f)
        t = open(p, "rb").read().decode("utf-8-sig", "replace")
        m = re.search(r"guid:\s*([^\s\r\n]+)", t)
        if m:
            metas.setdefault(m.group(1), p)

print("Assets .meta guid 总数:", len(metas))
print()

def b64_of_hexguid(h):
    raw = bytes.fromhex(h)
    return base64.b64encode(raw).decode("ascii")

print("=== 假设检验：hex guid 的 base64(16字节) 是否是某个 .meta guid 的前缀 ===")
hit_prefix = 0
for g in scene_guids:
    if not re.fullmatch(r"[0-9a-fA-F]{32}", g):
        continue
    b = b64_of_hexguid(g)
    prematch = [mg for mg in metas if mg.startswith(b[:22])]
    if prematch:
        hit_prefix += 1
        print(f"  {g}  -> base64 {b}  命中前缀 {len(prematch)} 个")
        for mg in prematch[:3]:
            print("        ", os.path.relpath(metas[mg], R), f"guid={mg[:40]}...")
print(f"  前缀假设命中数: {hit_prefix}")
print()

print("=== 直接检验：某 .meta guid 是否包含该 hex 的 base64 全串 ===")
hit_full = 0
for g in scene_guids:
    if not re.fullmatch(r"[0-9a-fA-F]{32}", g):
        continue
    b = b64_of_hexguid(g)
    pm = [mg for mg in metas if b[:-2] in mg or b in mg]
    if pm:
        hit_full += 1
        print(f"  {g} base64={b} -> {[os.path.relpath(metas[x], R) for x in pm[:3]]}")
print(f"  全串假设命中数: {hit_full}")
print()

print("=== 反向样本：随便取一个 base64 的 .mat meta，看它长什么样 ===")
shown = 0
for mg, p in metas.items():
    if mg.endswith("=") and p.endswith(".mat.meta"):
        print("  ", os.path.relpath(p, R))
        print("     guid =", mg)
        dec = base64.b64decode(mg + "=" * (-len(mg) % 4))
        print("     解码字节数 =", len(dec), " hex =", dec.hex())
        print("     base64(解码前16字节) =", base64.b64encode(dec[:16]).decode())
        shown += 1
        if shown >= 3:
            break
print()
print("=== 对照样本：一个 hex32 的 .mat meta ===")
shown = 0
for mg, p in metas.items():
    if re.fullmatch(r"[0-9a-fA-F]{32}", mg) and p.endswith(".mat.meta"):
        print("  ", os.path.relpath(p, R), "guid =", mg)
        shown += 1
        if shown >= 3:
            break

# 场景里出现最多的 hex guid 对应的 .mat 文件是否存在（按文件名猜）
print()
print("=== 场景 guid 前 10 名按出现次数 ===")
cnt = collections.Counter(TOK.findall(s)) if (collections := __import__("collections")) else None
for g, c in cnt.most_common(12):
    known = "有 .meta" if g in metas else "无 .meta"
    print(f"  {c:>3}x  {g}  {known}")
