import re, os, collections

R = r"D:\NewOne\Client-Dice"
sc = os.path.join(R, r"Assets\Scenes\BattleM1.unity")
s = open(sc, "rb").read().decode("utf-8-sig", "replace")

# 切成文档块
blocks = re.split(r"^--- !u!(\d+) &(\d+)\s*$", s, flags=re.M)
# blocks: [pre, cid, fid, body, cid, fid, body, ...]
docs = {}
i = 1
while i + 2 < len(blocks) + 1 and i + 1 < len(blocks):
    cid = int(blocks[i]); fid = int(blocks[i + 1]); body = blocks[i + 2]
    docs[fid] = (cid, body)
    i += 3

def objname(fid):
    if fid in docs:
        m = re.search(r"m_Name:\s*(.*)", docs[fid][1])
        if m:
            n = m.group(1).strip()
            try:
                return n.encode().decode("unicode_escape")
            except Exception:
                return n
    return None

# 所有 .meta guid（Assets）
metas = {}
for root, dirs, files in os.walk(os.path.join(R, "Assets")):
    for f in files:
        if f.endswith(".meta"):
            t = open(os.path.join(root, f), "rb").read().decode("utf-8-sig", "replace")
            mm = re.search(r"guid:\s*([^\s\r\n]+)", t)
            if mm:
                metas.setdefault(mm.group(1), os.path.join(root, f))

BUILTIN = "0000000000000000e000000000000000"

missing_by_name = collections.defaultdict(collections.Counter)
ok_by_name = collections.defaultdict(collections.Counter)
rend_count = 0

for fid, (cid, body) in docs.items():
    if cid != 23:      # MeshRenderer
        continue
    rend_count += 1
    go = re.search(r"m_GameObject:\s*\{fileID:\s*(\d+)\}", body)
    name = objname(int(go.group(1))) if go else "?"
    mats = re.findall(r"-\s*\{fileID:\s*\d+,\s*guid:\s*([0-9a-fA-F]{32}),", body)
    for g in mats:
        if g == BUILTIN:
            ok_by_name[name]["<builtin>"] += 1
        elif g in metas:
            ok_by_name[name][os.path.basename(metas[g])] += 1
        else:
            missing_by_name[name][g] += 1

print(f"场景 MeshRenderer 总数: {rend_count}")
print()
print("=== 引用了【缺失材质】的 GameObject ===")
tot_missing = 0
for name, c in missing_by_name.items():
    n = sum(c.values()); tot_missing += n
    print(f"  {name}")
    for g, k in c.most_common():
        print(f"      {k}x  {g}   (全项目无 .meta)")
print(f"  ---- 缺失材质引用总数: {tot_missing}")
print()
print("=== 引用了【已解析材质】的 GameObject（前 20） ===")
for name, c in list(ok_by_name.items())[:20]:
    print(f"  {name}: {dict(c)}")
print()
print("=== 全部 GameObject 名称（找骰子相关） ===")
names = []
for fid, (cid, body) in docs.items():
    if cid == 1:
        n = objname(fid)
        if n:
            names.append(n)
for n in names:
    print("   ", n)
