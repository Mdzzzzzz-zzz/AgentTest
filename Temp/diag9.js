// A1-ARTIFACT-01 diag9 : final evidence — map 19 materials to SerializedFiles + context fragments
const fs = require('fs');
const zlib = require('zlib');
const DATA_BR = 'D:/NewOne/Client-Dice/Builds/WxMiniGame/Build/WxMiniGame.data.br';
const OUT = 'D:/NewOne/Temp/diag9_result.txt';
const OUT_JSON = 'D:/NewOne/Temp/material_proof.json';
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
const bi = lz4Block(tj.slice(64, 64 + 288), 486);
let q = 16; const blockCount = bi.readUInt32BE(q); q += 4;
const blocks = [];
for (let i = 0; i < blockCount; i++) { blocks.push({ us: bi.readUInt32BE(q), cs: bi.readUInt32BE(q + 4), fl: bi.readUInt16BE(q + 8) }); q += 10; }
q += 4; // nodeCount
const nodes = [];
for (let i = 0; i < 6; i++) { const off = Number(bi.readBigInt64BE(q)); const size = Number(bi.readBigInt64BE(q + 8)); q += 20; let s = q; while (bi[q] !== 0) q++; const name = bi.slice(s, q).toString('utf8'); q++; nodes.push({ off, size, name }); }

let cursor = Math.ceil((64 + 288) / 16) * 16;
let all = Buffer.alloc(0);
for (const b of blocks) { const comp = tj.slice(cursor, cursor + b.cs); cursor += b.cs; const ct = b.fl & 0x3f; all = Buffer.concat([all, ct === 0 ? comp.slice(0, b.us) : lz4Block(comp, b.us)]); }
log('decompressed asset stream = ' + all.length + ' bytes; SerializedFiles=' + nodes.length);

function sfOf(pos) { for (const n of nodes) { if (pos >= n.off && pos < n.off + n.size) return n.name; } return '?'; }

const names = [];
for (let i = 1; i <= 9; i++) names.push('Die Border ' + i + '_阶段丙修复');
for (let i = 1; i <= 9; i++) names.push('Die One Side ' + i + '_阶段丙修复');
names.push('Die Outline_阶段丙修复');
const proof = [];
let hit = 0;
log('');
log('idx | name | occurrences | SerializedFile | offset');
for (const n of names) {
  const nb = Buffer.from(n, 'utf8');
  const poss = []; let p = 0; while ((p = all.indexOf(nb, p)) !== -1) { poss.push(p); p++; }
  if (poss.length) hit++;
  const sf = poss.length ? sfOf(poss[0]) : '-';
  log('  ' + (poss.length ? 'HIT ' : 'MISS') + ' | ' + n + ' | x' + poss.length + ' | ' + sf + ' | ' + poss.join(','));
  // context fragment around first match: 12 bytes before .. +20 after
  let frag = '';
  if (poss.length) { const a = Math.max(0, poss[0] - 12), b = Math.min(all.length, poss[0] + nb.length + 12); frag = all.slice(a, b).toString('latin1').replace(/[^\x20-\x7e\u4e00-\u9fff]/g, '.'); }
  proof.push({ name: n, occurrences: poss.length, serializedFile: sf, offsets: poss, context: frag });
}
log('');
log('FINAL: ' + hit + ' / 19 material names present in decompressed data.tj3d asset stream');
log('');
log('--- 3 sample context fragments ---');
for (const pr of proof.slice(0, 2).concat(proof.slice(18))) log('  [' + pr.name + ']  ...' + pr.context + '...');

fs.writeFileSync(OUT, lines.join('\n'), 'utf8');
fs.writeFileSync(OUT_JSON, JSON.stringify({ decompressedBytes: all.length, serializedFiles: nodes, materialProof: proof, hits: hit, total: 19 }, null, 2), 'utf8');
