// A1-ARTIFACT-01 wasmprobe : decompress wasm.br, size + symbol probes
const fs = require('fs');
const zlib = require('zlib');
const WASM = 'D:/NewOne/Client-Dice/Builds/WxMiniGame/Build/WxMiniGame.wasm.br';
const OUT = 'D:/NewOne/Temp/wasmprobe_result.txt';
const lines = [];
function log(s) { lines.push(s); }
const comp = fs.readFileSync(WASM);
let raw;
try { raw = zlib.brotliDecompressSync(comp); } catch (e) { log('decompress error: ' + e); }
if (raw) {
  log('wasm.br compressed   = ' + comp.length + ' bytes (' + (comp.length / 1048576).toFixed(2) + ' MiB)');
  log('wasm decompressed    = ' + raw.length + ' bytes (' + (raw.length / 1048576).toFixed(2) + ' MiB)');
  log('brotli ratio         = ' + (raw.length / comp.length).toFixed(2) + 'x');
  log('magic hex            = ' + raw.slice(0, 4).toString('hex'));
  function cnt(n) { let c = 0, p = 0; const b = Buffer.from(n, 'latin1'); while ((p = raw.indexOf(b, p)) !== -1) { c++; p++; } return c; }
  log('');
  log('--- symbol probes in decompressed wasm ---');
  for (const t of ['TestRunner', 'UnityEngine.TestTools', 'NUnit', 'test-framework', 'PlaymodeTestsController',
    'TestResultRenderer', 'UnityEngine.TestRunner', 'AssemblyNameFilter', 'IL2CPP', 'il2cpp',
    'UnityEngine.GameObject', 'UnityEngine.Physics', 'UnityEngine.Animation', 'UnityEngine.Video',
    'UnityEngine.Terrain', 'UnityEngine.XR', 'UnityEngine.AI', 'UnityEngine.Networking',
    'UnityEngine.ParticleSystem', 'UnityEngine.UI', 'UnityEngine.Audio', 'UnityEngine.VR']) {
    log('  ' + cnt(t) + '\t' + t);
  }
}
fs.writeFileSync(OUT, lines.join('\n'), 'utf8');
