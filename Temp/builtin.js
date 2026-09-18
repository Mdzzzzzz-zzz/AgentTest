// A1-UNBLOCK-03 : list BuiltInPackages + check module deps
const fs = require('fs');
const path = require('path');
const BP = 'C:/Program Files/Tuanjie/Hub/Editor/2022.3.62t14/Editor/Data/Resources/PackageManager/BuiltInPackages';
const OUT = 'D:/NewOne/Temp/builtin.txt';
const lines = [];
function log(s) { lines.push(s); }
let dirs = [];
try { dirs = fs.readdirSync(BP, { withFileTypes: true }).filter(d => d.isDirectory()).map(d => d.name); } catch (e) { log('err ' + e.message); }
log('module dirs count=' + dirs.length);
log(dirs.join('\n'));
log('');
// print deps for a few key ones
for (const m of ['com.unity.modules.ui', 'com.unity.modules.imgui', 'com.unity.modules.textrendering', 'com.unity.modules.uielements', 'com.unity.modules.assetbundle', 'com.unity.modules.unitywebrequest']) {
  const pj = path.join(BP, m, 'package.json');
  log('--- ' + m + ' ---');
  try { log(fs.readFileSync(pj, 'utf8').replace(/\s+/g, ' ')); } catch (e) { log('  (missing package.json)'); }
}
fs.writeFileSync(OUT, lines.join('\n'), 'utf8');
