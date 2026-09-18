// A1-UNBLOCK-03 : locate WeChat SDK files
const fs = require('fs');
const path = require('path');
const OUT = 'D:/NewOne/Temp/sdk_locate.txt';
const lines = [];
function log(s) { lines.push(s); }
function trylist(d) {
  try { const e = fs.readdirSync(d, { withFileTypes: true }); log('== ' + d + ' (' + e.length + ') =='); for (const x of e.slice(0, 60)) log('  ' + (x.isDirectory() ? '[D] ' : '    ') + x.name); }
  catch (err) { log('!! ' + d + ' : ' + err.message); }
}
trylist('D:/NewOne/Client-Dice/Library');
trylist('D:/NewOne/Client-Dice/Packages');
// find the sdk dir
function find(dir, depth, name) {
  if (depth > 6) return;
  let e; try { e = fs.readdirSync(dir, { withFileTypes: true }); } catch { return; }
  for (const x of e) {
    const p = path.join(dir, x.name);
    if (x.isDirectory() && x.name.includes(name)) { log('FOUND dir: ' + p); }
    if (x.isDirectory() && (x.name === 'com.qq.weixin.minigame' || x.name.startsWith('com.qq.weixin.minigame@'))) { walk(p, 0); }
    if (x.isDirectory() && depth < 4) find(p, depth + 1, name);
  }
}
function walk(dir, d) {
  if (d > 4) return;
  let e; try { e = fs.readdirSync(dir, { withFileTypes: true }); } catch { return; }
  for (const x of e) { const p = path.join(dir, x.name); log('  '.repeat(d + 1) + (x.isDirectory() ? 'D ' : 'F ') + x.name + (x.isDirectory() ? '' : '  (' + fs.statSync(p).size + ')')); if (x.isDirectory()) walk(p, d + 1); }
}
find('D:/NewOne/Client-Dice', 0, 'com.qq.weixin.minigame');
fs.writeFileSync(OUT, lines.join('\n'), 'utf8');
