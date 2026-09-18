// A1-UNBLOCK-03 : list strings containing key tokens in 2 DLLs
const fs = require('fs');
const base = 'D:/NewOne/Client-Dice/Library/PackageCache/com.qq.weixin.minigame@a09d4b29da/Runtime/Plugins/';
const OUT = 'D:/NewOne/Temp/dll_ctx.txt';
const lines = [];
function strings(b, min) { const out = []; let i = 0; while (i < b.length) { if (b[i] >= 0x20 && b[i] < 0x7f) { let j = i; while (j < b.length && b[j] >= 0x20 && b[j] < 0x7f) j++; const s = b.slice(i, j).toString('latin1'); if (s.length >= min) out.push(s); i = j; } else i++; } return out; }
for (const f of ['wx-runtime.dll', 'Unity.FontABTool.dll']) {
  const b = fs.readFileSync(base + f);
  const ss = strings(b, 3);
  lines.push('===== ' + f + ' =====');
  for (const tok of ['AssetBundle', 'AudioSource', 'AudioClip', 'Microphone', 'Canvas', 'Font', 'ParticleSystem', 'VideoPlayer', 'Terrain', 'NavMesh']) {
    const hits = ss.filter(s => s.includes(tok));
    lines.push('  [' + tok + '] ' + (hits.length ? hits.join(' | ') : '(none)'));
  }
  lines.push('');
}
fs.writeFileSync(OUT, lines.join('\n'), 'utf8');
