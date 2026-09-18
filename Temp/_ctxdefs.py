import os, re
p = r"C:/Program Files/Tuanjie/Hub/Editor/2022.3.62t14/Editor/Data/PlaybackEngines/WeixinMiniGameSupport/WeixinMiniGamePlayerBuildProgram.exe"
data = open(p, "rb").read()
for enc, label in (("ascii", "ASCII"), ("utf-16-le", "UTF16LE")):
    try:
        txt = data.decode(enc, errors="ignore")
    except Exception:
        continue
    print(f"########## {label} ##########")
    for m in re.finditer(r"UNITY_(WEBGL|WEIXINMINIGAME)", txt):
        s = max(0, m.start() - 300); e = min(len(txt), m.end() + 300)
        seg = txt[s:e]
        seg = "".join(ch if (32 <= ord(ch) < 127) else ("\n" if ord(ch) in (10,13) else ".") for ch in seg)
        print(f"--- @{m.start()} ---")
        print(seg)
        print()
