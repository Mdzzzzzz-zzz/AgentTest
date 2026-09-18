import os, re, sys
PE = r"C:/Program Files/Tuanjie/Hub/Editor/2022.3.62t14/Editor/Data/PlaybackEngines/WeixinMiniGameSupport"
targets = [
    "UnityEditor.WeixinMiniGame.Extensions.dll",
    "WeixinMiniGamePlayerBuildProgram.exe",
    "WeixinMiniGamePlayerBuildProgram.Data.dll",
]
pat = re.compile(rb"UNITY_[A-Z0-9_]{3,40}")
for t in targets:
    p = os.path.join(PE, t)
    if not os.path.exists(p):
        print("MISSING", t); continue
    data = open(p, "rb").read()
    found = set()
    # ascii
    for m in pat.findall(data):
        found.add(m.decode("ascii"))
    # utf-16le
    try:
        txt = data.decode("utf-16-le", errors="ignore")
    except Exception:
        txt = ""
    for m in re.findall(r"UNITY_[A-Z0-9_]{3,40}", txt):
        found.add(m)
    print(f"=== {t} ({len(data):,} bytes) ===")
    print("  " + (", ".join(sorted(found)) if found else "(无 UNITY_* 宏字符串)"))
