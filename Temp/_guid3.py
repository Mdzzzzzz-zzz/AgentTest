import base64, re, os, collections

R = r"D:\NewOne\Client-Dice"
scene = os.path.join(R, r"Assets\Scenes\BattleM1.unity")
s = open(scene, "rb").read().decode("utf-8-sig", "replace")
TOK = re.compile(r"guid:\s*([^\s,}\r\n]+)")
scene_guids = list(dict.fromkeys(TOK.findall(s)))
print("BattleM1.unity 引用到的不同 guid 数:", len(scene_guids))

# 收集所有 .meta 的 guid：原样 + （若是 base64）解码后的 hex 作为别名
raw_map = {}          # 原样字符串 -> 文件
alias_map = {}        # 解码hex(全长) / 前32位 -> 文件
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
        raw_map.setdefault(v, p)
        if v.endswith("="):
            try:
                dec = base64.b64decode(v + "=" * (-len(v) % 4))
                alias_map.setdefault(dec.hex(), p)
                alias_map.setdefault(dec.hex()[:32], p)
            except Exception:
                pass

print("Assets 下 .meta 总数(有 guid):", len(raw_map))
print()

resolved, dangling = [], []
for g in scene_guids:
    if g in raw_map:
        resolved.append((g, raw_map[g], "原样命中"))
    elif g in alias_map:
        resolved.append((g, alias_map[g], "base64解码命中"))
    else:
        dangling.append(g)

print("=== 解析结果 ===")
print(f"  命中: {len(resolved)} / {len(scene_guids)}")
print(f"  悬空: {len(dangling)} / {len(scene_guids)}")
print()
print("=== 悬空 guid（场景引用了但找不到 .meta） ===")
for g in dangling:
    ctx = [l.strip()[:100] for l in s.splitlines() if g in l]
    print(f"  {g}  出现 {len(ctx)} 处")
    for c in ctx[:2]:
        print("      ", c)
print()
print("=== 命中的样例 ===")
for g, p, how in resolved[:8]:
    print(f"  {g}  <- {os.path.relpath(p, R)}   [{how}]")

# 反向：有多少 base64 形式的 .meta 的解码前32位，恰好等于某个 hex32 形式的 .meta
hex32_metas = {v for v in raw_map if re.fullmatch(r"[0-9a-fA-F]{32}", v)}
overlap = [v for v in alias_map if v in hex32_metas]
print()
print(f"=== base64 解码结果与 hex32 形式重叠: {len(overlap)} 个 ===")
print("  (若为 0，说明两种形态是彼此独立的两套 GUID 空间)")
