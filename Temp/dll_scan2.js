// A1-UNBLOCK-03 : search SDK DLLs for UnityEngine.*Module assembly-ref names
const fs = require('fs');
const path = require('path');
const OUT = 'D:/NewOne/Temp/dll_modules.txt';
const lines = [];
function log(s) { lines.push(s); }
const dir = 'D:/NewOne/Client-Dice/Library/PackageCache/com.qq.weixin.minigame@a09d4b29da/Runtime/Plugins';
const modules = ['UnityEngine.CoreModule', 'UnityEngine.AssetBundleModule', 'UnityEngine.AudioModule',
  'UnityEngine.TextRenderingModule', 'UnityEngine.ImageConversionModule', 'UnityEngine.UIModule',
  'UnityEngine.UI', 'UnityEngine.UnityWebRequestModule', 'UnityEngine.UnityWebRequestAssetBundleModule',
  'UnityEngine.UnityWebRequestTextureModule', 'UnityEngine.UnityWebRequestAudioModule',
  'UnityEngine.ParticleSystemModule', 'UnityEngine.VideoModule', 'UnityEngine.AnimationModule',
  'UnityEngine.PhysicsModule', 'UnityEngine.Physics2DModule', 'UnityEngine.JSONSerializeModule',
  'UnityEngine.IMGUIModule', 'UnityEngine.SpriteMaskModule', 'UnityEngine.SpriteShapeModule',
  'UnityEngine.TilemapModule', 'UnityEngine.TerrainModule', 'UnityEngine.AIModule',
  'UnityEngine.DirectorModule', 'UnityEngine.ScreenCaptureModule', 'UnityEngine.InputLegacyModule',
  'UnityEngine.InputModule', 'UnityEngine.TextCoreTextEngineModule', 'UnityEngine.TextCoreFontEngineModule',
  'UnityEngine.UIModule', 'UnityEngine.SubsystemsModule', 'UnityEngine.UnityAnalyticsModule'];
const dlls = fs.readdirSync(dir).filter(f => f.endsWith('.dll'));
for (const d of dlls) {
  const buf = fs.readFileSync(path.join(dir, d));
  log('--- ' + d + ' ---');
  for (const m of modules) {
    const c = (() => { let c = 0, p = 0; const b = Buffer.from(m, 'latin1'); while ((p = buf.indexOf(b, p)) !== -1) { c++; p++; } return c; })();
    if (c > 0) log('  ' + c + '\t' + m);
  }
  // also any 'UnityEngine.' + Module substring
  const re = /UnityEngine\.[A-Za-z]+Module/g; const seen = {};
  const s = buf.toString('latin1'); let mm;
  while ((mm = re.exec(s)) !== null) { seen[mm[0]] = (seen[mm[0]] || 0) + 1; }
  const keys = Object.keys(seen);
  if (keys.length) log('  [all *Module refs] ' + keys.map(k => k + '=' + seen[k]).join(', '));
}
fs.writeFileSync(OUT, lines.join('\n'), 'utf8');
