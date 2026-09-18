// A1-UNBLOCK-03 : extract printable strings from wx-runtime.dll and filter interesting
const fs = require('fs');
const OUT = 'D:/NewOne/Temp/wxruntime_strings.txt';
const buf = fs.readFileSync('D:/NewOne/Client-Dice/Library/PackageCache/com.qq.weixin.minigame@a09d4b29da/Runtime/Plugins/wx-runtime.dll');
function strings(b, min) {
  const out = []; let i = 0;
  while (i < b.length) {
    if (b[i] >= 0x20 && b[i] < 0x7f) { let j = i; while (j < b.length && b[j] >= 0x20 && b[j] < 0x7f) j++; const s = b.slice(i, j).toString('latin1'); if (s.length >= min) out.push(i + '  ' + s); i = j; } else i++;
  }
  return out;
}
const all = strings(buf, 4);
const hasBSJB = buf.includes(Buffer.from('BSJB', 'latin1'));
const lines = [];
lines.push('file size=' + buf.length + ' ; PE-magic=' + buf.slice(0, 2).toString('latin1') + ' ; hasBSJB(managed metadata)=' + hasBSJB);
lines.push('total strings(>=4)=' + all.length);
lines.push('');
lines.push('=== AssemblyRef-ish / UnityEngine / System / *Module ===');
for (const s of all) { const t = s.slice(s.indexOf('  ') + 2); if (/^(UnityEngine|mscorlib|System|netstandard|Assembly-CSharp|Wx|LitJson|WeChatWASM|Unity\.)/.test(t) || /Module/.test(t) || /\.dll$/.test(t)) lines.push('  ' + s); }
lines.push('');
lines.push('=== engine-type-name hits ===');
const toks = ['AssetBundle', 'AudioClip', 'AudioSource', 'Microphone', 'VideoPlayer', 'ParticleSystem', 'Terrain', 'NavMesh', 'Cloth', 'Tilemap', 'WebCamTexture', 'TextMesh', 'GUIText', 'Font', 'JsonUtility', 'UnityWebRequest', 'DownloadHandler', 'ScreenCapture', 'EncodeToPNG', 'Sprite', 'Shader', 'Material', 'Texture2D', 'SkinnedMeshRenderer', 'Animator', 'PlayableDirector', 'UIElements', 'EventSystem', 'Canvas', 'ProfilerRecorder', 'GameObject', 'MonoBehaviour', 'Transform', 'Camera'];
for (const s of all) { const t = s.slice(s.indexOf('  ') + 2); if (toks.some(k => t === k)) lines.push('  ' + s); }
fs.writeFileSync(OUT, lines.join('\n'), 'utf8');
