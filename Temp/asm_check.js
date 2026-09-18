// A1-UNBLOCK-03 : ScriptAssemblies + locate editor builtin module deps
const fs = require('fs');
const path = require('path');
const OUT = 'D:/NewOne/Temp/asm_check.txt';
const lines = [];
function log(s) { lines.push(s); }
try { log('== Library/ScriptAssemblies =='); for (const x of fs.readdirSync('D:/NewOne/Client-Dice/Library/ScriptAssemblies')) log('  ' + x); } catch (e) { log('err ' + e.message); }

// search likely editor roots for BuiltInPackages
const roots = ['C:/Program Files/Tuanjie', 'C:/Program Files/Unity', 'D:/Program Files/Tuanjie', 'D:/Tuanjie', 'C:/Tuanjie'];
function findPkg(root, target, depth) {
  if (depth > 8) return null;
  let e; try { e = fs.readdirSync(root, { withFileTypes: true }); } catch { return null; }
  for (const x of e) {
    if (!x.isDirectory()) continue;
    const p = path.join(root, x.name);
    if (x.name === target) return p;
    const r = findPkg(p, target, depth + 1);
    if (r) return r;
  }
  return null;
}
log('');
for (const r of roots) {
  if (!fs.existsSync(r)) { log('(no) ' + r); continue; }
  log('root exists: ' + r);
  const t = findPkg(r, 'com.unity.modules.textrendering', 0);
  log('  textrendering dir: ' + (t || 'not found'));
  const u = findPkg(r, 'com.unity.ugui', 0);
  log('  ugui dir: ' + (u || 'not found'));
  if (u) { try { log('  ugui package.json: ' + fs.readFileSync(path.join(u, 'package.json'), 'utf8').replace(/\s+/g, ' ').slice(0, 800)); } catch (e) {} }
}
fs.writeFileSync(OUT, lines.join('\n'), 'utf8');
