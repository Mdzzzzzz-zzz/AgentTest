// A1-ARTIFACT-01 diag2 : deep diagnostics on decompressed data.br
const fs = require('fs');
const zlib = require('zlib');
const DATA_BR = 'D:/NewOne/Client-Dice/Builds/WxMiniGame/Build/WxMiniGame.data.br';
const OUT = 'D:/NewOne/Temp/diag2_result.txt';
const lines = [];
function log(s) { lines.push(s); }

const buf = fs.readFileSync(DATA_BR);
const raw = zlib.brotliDecompressSync(buf);
log('decompressed bytes: ' + raw.length);

function cnt(needle, enc) { return countOccurrences(raw, Buffer.from(needle, enc || 'utf8')); }
function countOccurrences(h, n) { let c = 0, p = 0; while ((p = h.indexOf(n, p)) !== -1) { c++; p++; } return c; }

// ---- parse TuanjieWebData1.0 file table ----
try {
  const magic = raw.slice(0, 18).toString('latin1');
  log('magic: ' + JSON.stringify(magic));
  const headerSize = raw.readUInt32LE(18);
  log('headerSize: ' + headerSize);
  let p = 22; // after magic(18) + headerSize(4)
  let idx = 0;
  const files = [];
  while (p < headerSize + 18) {
    const off = raw.readUInt32LE(p); const size = raw.readUInt32LE(p + 4);
    const nameLen = raw.readUInt32LE(p + 8);
    if (nameLen <= 0 || nameLen > 300 || p + 12 + nameLen > raw.length) break;
    const name = raw.slice(p + 12, p + 12 + nameLen).toString('utf8');
    files.push({ off, size, name });
    log('  file[' + idx + '] name=' + JSON.stringify(name) + ' off=' + off + ' size=' + size);
    p += 12 + nameLen; idx++;
    if (idx > 200) break;
  }
} catch (e) { log('file table parse error: ' + e); }

log('');
// ---- token counts ----
const tokens8 = ['data.tj3d', 'RuntimeInitialize', 'tj3d', 'Border', 'One Side', 'Outline',
  'Die ', 'Die Border ', 'Die One Side ', 'Splash', 'splash', 'BattleM1', 'Resources',
  'Jersey10', '骰子数字深度遮挡', '阶段丙修复', 'DemoC', 'DemoB', 'DemoA', 'Texture2D',
  'TextMeshPro', 'RogueSprite', 'goblin', 'MonoBehaviour', 'material', 'Material', '.mat'];
for (const t of tokens8) log('utf8    ' + cnt(t) + '\t' + t);
log('');
for (const t of ['Border', 'One Side', 'Outline', 'Die ', '阶段丙修复', 'material']) log('utf16le ' + cnt(t, 'utf16le') + '\t' + t);

log('');
// ---- context around the single 'Die Border ' hit ----
const hitName = Buffer.from('Die Border 1_阶段丙修复', 'utf8');
const pos = raw.indexOf(hitName);
log('offset of "Die Border 1_阶段丙修复": ' + pos);
if (pos >= 0) {
  const a = Math.max(0, pos - 160), b = Math.min(raw.length, pos + 160);
  log('ctx hex  : ' + raw.slice(a, b).toString('hex'));
  log('ctx ascii: ' + raw.slice(a, b).toString('latin1').replace(/[^\x20-\x7e]/g, '.'));
}
// search contexts for any 'Border' occurrences (ascii)
log('');
let q = 0, n = 0;
while ((q = raw.indexOf(Buffer.from('Border', 'utf8'), q)) !== -1 && n < 40) {
  const a = Math.max(0, q - 40), b = Math.min(raw.length, q + 60);
  log('  @' + q + ' ... ' + raw.slice(a, b).toString('latin1').replace(/[^\x20-\x7e]/g, '.'));
  q++; n++;
}
log('total "Border" ascii matches shown: ' + n);

fs.writeFileSync(OUT, lines.join('\n'), 'utf8');
