import re, os, collections

R = r"D:\NewOne\Client-Dice"
sc = os.path.join(R, r"Assets\Scenes\BattleM1.unity")
s = open(sc, "rb").read().decode("utf-8-sig", "replace")

CLASSES = {
    1: "GameObject", 4: "Transform", 20: "Camera", 21: "Material(内联)",
    23: "MeshRenderer", 33: "MeshFilter", 43: "Mesh(内联)", 54: "Rigidbody(3D)",
    50: "Rigidbody2D", 58: "CircleCollider2D", 61: "BoxCollider2D",
    60: "PolygonCollider2D", 65: "BoxCollider(3D)", 64: "MeshCollider(3D)",
    135: "SphereCollider(3D)", 136: "CapsuleCollider(3D)",
    114: "MonoBehaviour", 115: "MonoScript", 1045: "EditorBuildSettings",
    108: "Light", 111: "Animation", 212: "SpriteRenderer", 222: "CanvasRenderer",
    223: "Canvas", 224: "RectTransform", 95: "Animator", 198: "ParticleSystem",
    10001: "PrefabInstance", 1001: "PrefabInstance",
    74: "Terrain", 154: "TerrainCollider", 109: "AudioSource",
    82: "AudioListener", 157: "LightmapSettings", 196: "NavMeshSettings",
    29: "OcclusionCullingSettings", 104: "RenderSettings", 128: "RenderSettings?",
    129: "RenderSettings?", 30: "GraphicsSettings", 1046: "?",
}
ids = collections.Counter(int(m.group(1)) for m in re.finditer(r"^--- !u!(\d+) &", s, re.M))
print("=== BattleM1.unity 组件类型普查（按出现数排序） ===")
for cid, n in ids.most_common():
    print(f"  {n:>5}  !u!{cid:<6} {CLASSES.get(cid, '(未登记)')}")

print()
print("=== 是否含 2D 物理 / SpriteRenderer ===")
for cid, name in [(50, "Rigidbody2D"), (58, "CircleCollider2D"), (61, "BoxCollider2D"),
                  (60, "PolygonCollider2D"), (212, "SpriteRenderer"), (223, "Canvas"),
                  (95, "Animator"), (198, "ParticleSystem"), (74, "Terrain"),
                  (111, "Animation"), (109, "AudioSource")]:
    print(f"  {name:<20} {ids.get(cid, 0)} 个")

print()
print("=== Resources 目录清单 ===")
res = os.path.join(R, "Assets", "Resources")
tot = 0
for root, dirs, files in os.walk(res):
    for f in files:
        if f.endswith(".meta"):
            continue
        p = os.path.join(root, f)
        sz = os.path.getsize(p)
        tot += sz
        print(f"  {sz:>10,}  {os.path.relpath(p, R)}")
print(f"  ---- 合计 {tot:,} 字节 ({tot/1024:.0f} KB)")

print()
print("=== 代码里对 Resources 的引用 ===")
for root, dirs, files in os.walk(os.path.join(R, "Assets")):
    for f in files:
        if not f.endswith(".cs"):
            continue
        p = os.path.join(root, f)
        t = open(p, "rb").read().decode("utf-8", "replace")
        for m in re.finditer(r'Resources\.Load[^;\n]*', t):
            print(f"  {os.path.relpath(p, R)}: {m.group(0)[:110]}")
