import base64, re, os
R = r"D:/NewOne/Client-Dice"
meta = os.path.join(R, r"Assets\Scripts\Runtime\Gameplay\BattleFlow.cs.meta")
b = open(meta, "rb").read()
print("meta 前 120 字节 repr:", repr(b[:120]))
print("meta 总字节:", len(b))
txt = b.decode("utf-8-sig", "replace")
m = re.search(r"guid:/s*([^/s/r/n]+)", txt)
if not m:
    print("!! 仍未匹配到 guid")
else:
    g = m.group(1)
    print("meta guid 原文 :", g, " 长度:", len(g))
    pad = g + "=" * (-len(g) % 4)
    try:
        raw = base64.b64decode(pad, validate=False)
        print("base64 解码     :", len(raw), "字节 ->", raw.hex())
    except Exception as e:
        print("base64 失败:", e)

scene = os.path.join(R, r"Assets\Scenes\BattleM1.unity")
sb = open(scene, "rb").read()
s = sb.decode("utf-8-sig", "replace")
print()
print("=== 场景中含 b88ecb60 的行 ===")
hits = [ (i,l.strip()) for i,l in enumerate(s.splitlines(),1) if "b88ecb60" in l ]
print("命中行数:", len(hits))
for i,l in hits[:6]:
    print(f"  {i}: {l[:130]}")
print()
hexg = re.findall(r"guid:/s*([0-9a-fA-F]{32})(?![0-9a-zA-Z+/=])", s)
print("场景 32位hex guid 出现:", len(hexg), "处，不同值:", len(set(hexg)))
b64g = re.findall(r"guid:/s*([A-Za-z0-9+/]{40,}={0,2})", s)
print("场景 base64 形态   :", len(b64g), "处，不同值:", len(set(b64g)))
for v in list(dict.fromkeys(b64g))[:4]:
    print("   -", v[:64], f"(len={len(v)})")
