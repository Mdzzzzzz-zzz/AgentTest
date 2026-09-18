// A1-ARTIFACT-01 diag5 : search inside data.tj3d for asset paths; brute-force LZ4 blocksInfo
const fs = require('fs');
const zlib = require('zlib');
const DATA_BR = 'D:/NewOne/Client-Dice/Builds/WxMiniGame/Build/WxMiniGame.data.br';
const OUT = 'D:/NewOne/Temp/diag5_result.txt';
const lines = [];
function log(s) { lines.push(s); }
const raw = zlib.brotliDecompressSync(fs.readFileSync(DATA_BR));
const tj = raw.slice(241, 241 + 518837);

function cntAll(buf, needle) { let c = 0, p = 0; const n = Buffer.from(needle, 'utf8'); while ((p = buf.indexOf(n, p)) !== -1) { c++; p++; } return c; }

log('--- plaintext search INSIDE data.tj3d (' + tj.length + ' bytes) ---');
for (const t of ['Assets/', '.mat', 'DemoC/Generated', 'DemoC', '阶段丙修复', '阶段丙', 'Die Border 1_阶段丙修复',
  'Assets/DemoC', 'Assets/DemoA', 'Assets/DemoB', 'CAB-', 'Splash Screen', 'Gamepads_Sprite_Atlas_01',
  'RuntimeInitializeOnLoads', 'SerializedFile', 'tuanjie_default_resources']) {
  log('  ' + cntAll(tj, t) + '\t' + t);
}
// dump contexts of every 'Assets/' occurrence
log('');
let p = 0, k = 0, n = cntAll(tj, 'Assets/');
log('contexts of "Assets/" (total ' + n + '):');
while ((p = tj.indexOf(Buffer.from('Assets/'), p)) !== -1 && k < 60) {
  const a = Math.max(0, p - 16), b = Math.min(tj.length, p + 80);
  log('  @' + p + '  ' + tj.slice(a, b).toString('latin1').replace(/[^\x20-\x7e]/g, '.'));
  p++; k++;
}
// dump contexts of every '阶段丙修复'
log('');
log('contexts of "阶段丙修复":');
p = 0; k = 0;
const nb = Buffer.from('阶段丙修复');
while ((p = tj.indexOf(nb, p)) !== -1 && k < 60) {
  const a = Math.max(0, p - 40), b = Math.min(tj.length, p + 40);
  log('  @' + p + '  ' + tj.slice(a, b).toString('latin1').replace(/[^\x20-\x7e]/g, '.'));
  p++; k++;
}

// ---- LZ4 decoder ----
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
function printable(buf, max) { let pr = 0; const L = Math.min(buf.length, max || buf.length); for (let i = 0; i < L; i++) if (buf[i] >= 0x20 && buf[i] < 0x7f) pr++; return pr; }
function hasDir(buf) { return buf.includes(Buffer.from('Assets/')) || buf.includes(Buffer.from('.mat')) || buf.includes(Buffer.from('CAB-')); }

log('');
log('--- brute-force LZ4 blocksInfo (offsets 0..600, cbi sizes 200..320) ---');
let best = null;
for (let off = 0; off < 600; off++) {
  for (const cs of [288, 256, 240, 224, 512]) {
    if (off + cs > tj.length) continue;
    const out = lz4Block(tj.slice(off, off + cs), 4096);
    const pr = printable(out, 486);
    if (pr > 250 || hasDir(out)) {
      log('  CANDIDATE off=' + off + ' cs=' + cs + ' printable486=' + pr + ' hasDir=' + hasDir(out));
      if (!best) best = { off, cs, out };
    }
  }
}
if (best) {
  log('best: off=' + best.off + ' cs=' + best.cs);
  log('decompressed ascii: ' + best.out.slice(0, 600).toString('latin1').replace(/[^\x20-\x7e]/g, '.'));
} else {
  log('  no LZ4 candidate found in 0..600 (bundle content encrypted or non-LZ4)');
}

fs.writeFileSync(OUT, lines.join('\n'), 'utf8');
