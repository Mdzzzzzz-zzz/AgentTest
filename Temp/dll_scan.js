// A1-UNBLOCK-03 : scan SDK plugin DLLs for engine type references + list Logs
const fs = require('fs');
const path = require('path');
const OUT = 'D:/NewOne/Temp/dll_scan.txt';
const lines = [];
function log(s) { lines.push(s); }

// 1) list Logs
try { log('== Logs =='); for (const x of fs.readdirSync('D:/NewOne/Client-Dice/Logs')) log('  ' + x + '  (' + fs.statSync('D:/NewOne/Client-Dice/Logs/' + x).size + ')'); } catch (e) { log('logs err ' + e.message); }

// 2) scan DLLs
const dir = 'D:/NewOne/Client-Dice/Library/PackageCache/com.qq.weixin.minigame@a09d4b29da/Runtime/Plugins';
const tokens = ['AssetBundle', 'AssetBundleCreateRequest', 'AudioClip', 'AudioSource', 'Microphone',
  'VideoPlayer', 'VideoClip', 'ParticleSystem', 'Terrain', 'NavMesh', 'Cloth', 'Tilemap',
  'WebCamTexture', 'TextMesh', 'GUIText', 'Font', 'JsonUtility', 'UnityWebRequest', 'DownloadHandler',
  'ScreenCapture', 'EncodeToPNG', 'Sprite', 'Shader', 'Material', 'Texture2D', 'SkinnedMeshRenderer',
  'Animator', 'PlayableDirector', 'UIElements', 'EventSystem', 'Canvas', 'ProfilerRecorder'];
log('');
log('== DLL scan (ascii substring counts) ==');
const dlls = fs.readdirSync(dir).filter(f => f.endsWith('.dll'));
for (const d of dlls) {
  const buf = fs.readFileSync(path.join(dir, d));
  const found = [];
  for (const t of tokens) {
    let c = 0, p = 0; const b = Buffer.from(t, 'latin1');
    while ((p = buf.indexOf(b, p)) !== -1) { c++; p++; }
    if (c > 0) found.push(t + '=' + c);
  }
  log('');
  log('--- ' + d + ' (' + buf.length + ' bytes) ---');
  log('  ' + (found.join(', ') || '(none of the probed tokens)'));
}
fs.writeFileSync(OUT, lines.join('\n'), 'utf8');
