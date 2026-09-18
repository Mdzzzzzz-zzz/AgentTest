// A1-ARTIFACT-01 stat : verify package component sizes
const fs = require('fs');
const path = require('path');
const ROOT = 'D:/NewOne/Client-Dice/Builds/WxMiniGame';
const OUT = 'D:/NewOne/Temp/stat_result.txt';
const lines = [];
function log(s) { lines.push(s); }
function walk(dir) { let out = []; for (const e of fs.readdirSync(dir, { withFileTypes: true })) { const p = path.join(dir, e.name); if (e.isDirectory()) out = out.concat(walk(p)); else out.push({ p, size: fs.statSync(p).size }); } return out; }
const files = walk(ROOT);
let total = 0;
for (const f of files) { total += f.size; log(String(f.size).padStart(9) + '  ' + f.p.replace(ROOT, '.') ); }
const td = files.filter(f => f.p.includes('TemplateData')).reduce((a, b) => a + b.size, 0);
log('');
log('TemplateData total = ' + td + ' (' + files.filter(f => f.p.includes('TemplateData')).length + ' files)');
log('GRAND TOTAL = ' + total + ' bytes');
log('Budget 4 MiB = 4194304 ; headroom = ' + (4194304 - total) + ' bytes (' + ((4194304 - total) / 4194304 * 100).toFixed(2) + '%)');
fs.writeFileSync(OUT, lines.join('\n'), 'utf8');
