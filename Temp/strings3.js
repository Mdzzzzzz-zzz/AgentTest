// A1-UNBLOCK-03 : pin assembly refs + unitywebrequestassetbundle
const fs = require('fs');
const base = 'D:/NewOne/Client-Dice/Library/PackageCache/com.qq.weixin.minigame@a09d4b29da/Runtime/Plugins/';
const OUT = 'D:/NewOne/Temp/dll_ctx2.txt';
const lines = [];
function strings(b, min) { const out = []; let i = 0; while (i < b.length) { if (b[i] >= 0x20 && b[i] < 0x7f) { let j = i; while (j < b.length && b[j] >= 0x20 && b[j] < 0x7f) j++; const s = b.slice(i, j).toString('latin1'); if (s.length >= min) out.push(s); i = j; } else i++; } return out; }
const toks = ['UnityEngine.CoreModule', 'UnityEngine.AssetBundleModule', 'UnityEngine.TextRenderingModule',
  'UnityEngine.UnityWebRequestModule', 'UnityEngine.UnityWebRequestAssetBundleModule',
  'DownloadHandlerAssetBundle', 'DownloadHandlerWXAssetBundle', 'UnityWebRequestAssetBundle',
  'UnityWebRequest', 'DownloadHandler', 'Font', 'AssetBundle', 'TextRenderingModule', 'AssetBundleModule'];
for (const f of ['wx-runtime.dll', 'Unity.FontABTool.dll', 'wx-perf.dll', 'LitJson.dll']) {
  const b = fs.readFileSync(base + f);
  const ss = strings(b, 2);
  lines.push('===== ' + f + ' =====');
  for (const t of toks) { const exact = ss.filter(s => s === t).length; const sub = ss.filter(s => s.includes(t)).length; lines.push('  ' + t + '  exact=' + exact + ' sub=' + sub); }
  lines.push('');
}
fs.writeFileSync(OUT, lines.join('\n'), 'utf8');
