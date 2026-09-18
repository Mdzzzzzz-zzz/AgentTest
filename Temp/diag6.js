// A1-ARTIFACT-01 diag6 : exhaustive LZ4 sweep of data.tj3d for bundle directory
const fs = require('fs');
const zlib = require('zlib');
const DATA_BR = 'D:/NewOne/Client-Dice/Builds/WxMiniGame/Build/WxMiniGame.data.br';
const OUT = 'D:/NewOne/Temp/diag6_result.txt';
const lines = [];
function log(s) { lines.push(s); }
const raw = zlib.brotliDecompressSync(fs.readFileSync(DATA_BR));
const tj = raw.slice(241, 241 + 518837);

function lz4Block(src, dstSize) {
  const dst = Buffer.alloc(dstSize); let s = 0, d = 0;
  while (s < src.length) {
    const token = src[s++];
    let lit = token >> 4;
    if (lit === 15) { let b; do { b = src[s++]; lit += b; } while (b === 255); }
    for (let i = 0; i < lit; i++) { if (s >= src.length || d >= dst.length) return dst; dst[d++] = src[s++]; }
    if (s >= src.length || d >= dst.length) break;
    const offset = src[s++] | (src[s++] << 8);
    if (offset === 0) break;
    let ml = token & 0x0f;
    if (ml === 15) { let b; do { b = src[s++]; ml += b; } while (b === 255); }
    ml += 4; let mp = d - offset;
    for (let i = 0; i < ml; i++) { if (mp < 0 || d >= dst.length) return dst; dst[d++] = dst[mp++]; }
  }
  return dst;
}
const NEED = [Buffer.from('Assets/'), Buffer.from('CAB-'), Buffer.from('.mat')];
function isDir(b) { for (const n of NEED) if (b.includes(n)) return true; return false; }

let found = 0;
for (let off = 0; off + 288 <= tj.length; off++) {
  const out = lz4Block(tj.slice(off, off + 288), 4096);
  if (isDir(out)) {
    log('HIT off=' + off + '  ' + out.slice(0, 200).toString('latin1').replace(/[^\x20-\x7e]/g, '.'));
    found++;
    if (found > 20) break;
  }
}
log('found=' + found + ' (LZ4 blocksInfo candidates with asset dir), scanned ' + tj.length + ' offsets');
if (found === 0) log('=> NO valid LZ4 block-info anywhere in data.tj3d => content compressed+encrypted');

// Also: where exactly are the surviving plaintext strings? list nearby "word-like" runs in the tail region
log('');
log('--- printable runs >= 6 chars in data.tj3d (first 80) ---');
let i = 0, shown = 0;
while (i < tj.length && shown < 80) {
  if (tj[i] >= 0x20 && tj[i] < 0x7f) {
    let j = i; while (j < tj.length && tj[j] >= 0x20 && tj[j] < 0x7f) j++;
    const run = tj.slice(i, j).toString('latin1');
    if (run.length >= 6) { log('  @' + i + ' (' + run.length + ') ' + run); shown++; }
    i = j;
  } else i++;
}

fs.writeFileSync(OUT, lines.join('\n'), 'utf8');
