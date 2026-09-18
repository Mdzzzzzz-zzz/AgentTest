import base64, re, os, collections

R = r"D:\NewOne\Client-Dice"
meta = os.path.join(R, r"Assets\Scripts\Runtime\Gameplay\BattleFlow.cs.meta")
txt = open(meta, "rb").read().decode("utf-8-sig", "replace")
m = re.search(r"guid:\s*([^\s\r\n]+)", txt)
g = m.group(1)
print("meta guid 原文 :", g)
print("长度           :", len(g))

pad = g + "=" * (-len(g) % 4)
raw = base64.b64decode(pad)
print("base64 解码     :", len(raw), "字节 =", len(raw) * 8, "位")
print("解码后 hex      :", raw.hex())
print("hex 前 32 位    :", raw.hex()[:32])

scene = os.path.join(R, r"Assets\Scenes\BattleM1.unity")
s = open(scene, "rb").read().decode("utf-8-sig", "replace")

HEX = re.compile(r"guid:\s*([0-9a-fA-F]{32})\b")
B64 = re.compile(r"guid:\s*([A-Za-z0-9+/]{20,}={1,2})")
TOK = re.compile(r"guid:\s*([^\s,}\r\n]+)")

toks = TOK.findall(s)
print()
print("=== BattleM1.unity 中 guid: 出现", len(toks), "处 ===")
print("  纯32位hex形态 :", len(HEX.findall(s)))
print("  含=的base64形态:", len(B64.findall(s)))
print("  不同值总数     :", len(set(toks)))
kinds = collections.Counter(
    "hex32" if re.fullmatch(r"[0-9a-fA-F]{32}", t)
    else ("b64" if re.fullmatch(r"[A-Za-z0-9+/]{20,}={1,2}", t) and t.endswith("=")
          else "other")
    for t in toks)
print("  分类           :", dict(kinds))
print("  其它形态样例   :", [t for t in dict.fromkeys(toks) if not re.fullmatch(r"[0-9a-fA-F]{32}", t)][:6])

# 关键测试：场景里的脚本 guid 能否在任何 .meta 里找到
target = "b88ecb60d95a1634aa03daecb10559d0"
found_meta = []
misc = collections.Counter()
for root, dirs, files in os.walk(os.path.join(R, "Assets")):
    for f in files:
        if not f.endswith(".meta"):
            continue
        p = os.path.join(root, f)
        try:
            t = open(p, "rb").read().decode("utf-8-sig", "replace")
        except Exception:
            continue
        mm = re.search(r"guid:\s*([^\s\r\n]+)", t)
        if not mm:
            continue
        v = mm.group(1)
        if v == target:
            found_meta.append(p)
        key = ("hex32" if re.fullmatch(r"[0-9a-fA-F]{32}", v)
               else ("b64" if v.endswith("=") else "other"))
        misc[key] += 1

print()
print("=== 全 Assets 下 .meta 的 guid 形态统计 ===")
print(" ", dict(misc))
print()
print(f"=== 场景引用的脚本 guid {target} 在 .meta 中的命中：{len(found_meta)} 处 ===")
for p in found_meta:
    print("  ", p)

# 把 meta 的 base64 guid 解码后，取前16字节的hex，看是否等于场景的 hex guid
print()
print("=== 关键比对 ===")
print("  场景引用的脚本 guid :", target)
print("  .meta 解码后的 hex  :", raw.hex())
print("  两者前32位是否相符  :", raw.hex()[:32], "vs", target, "=>", raw.hex()[:32] == target)
