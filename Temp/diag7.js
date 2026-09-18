// A1-ARTIFACT-01 diag7 : properly LZ4-decompress blocksInfo of data.tj3d
const fs = require('fs');
const zlib = require('zlib');
const DATA_BR = 'D:/NewOne/Client-Dice/Builds/WxMiniGame/Build/WxMiniGame.data.br';
const OUT = 'D:/NewOne/Temp/diag7_result.txt';
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
    if (offset === 0 || offset > d) break;
    let ml = token & 0x0f;
    if (ml === 15) { let b; do { b = src[s++]; ml += b; } while (b === 255); }
    ml += 4; let mp = d - offset;
    for (let i = 0; i < ml; i++) { if (mp < 0 || d >= dst.length) return dst; dst[d++] = dst[mp++]; }
  }
  return dst;
}

log('header end=51 cbiSize=288 ubiSize=486');
for (let off = 51; off <= 130; off++) {
  const out = lz4Block(tj.slice(off, off + 288), 4096);
  // find where it was truncated: check last nonzero region ~ ubiSize
  let pr = 0; for (let i = 0; i < 486; i++) if (out[i] >= 0x20 && out[i] < 0x7f) pr++;
  const blockCount = out.readUInt32BE(16);
  const plausible = blockCount >= 1 && blockCount <= 200;
  if (pr > 120 || plausible) {
    log('off=' + off + ' printable486=' + pr + ' blockCount@16=' + blockCount + (plausible ? ' [PLAUSIBLE]' : ''));
    log('   hex : ' + out.slice(0, 96).toString('hex'));
    log('   ascii: ' + out.slice(0, 96).toString('latin1').replace(/[^\x20-\x7e]/g, '.'));
  }
}
fs.writeFileSync(OUT, lines.join('\n'), 'utf8');
