// A1-ARTIFACT-01 diag8 : parse UnityFS directory -> definitive asset list; verify 19 materials
const fs = require('fs');
const zlib = require('zlib');
const DATA_BR = 'D:/NewOne/Client-Dice/Builds/WxMiniGame/Build/WxMiniGame.data.br';
const OUT = 'D:/NewOne/Temp/diag8_result.txt';
const OUT_JSON = 'D:/NewOne/Temp/bundle_directory.json';
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

// blocksInfo block is LZ4 at offset 64, compressed 288 -> uncompressed 486
const bi = lz4Block(tj.slice(64, 64 + 288), 486);
log('decompressed blocksInfo+dir = ' + bi.length + ' bytes');
let q = 16;
const blockCount = bi.readUInt32BE(q); q += 4;
log('blockCount=' + blockCount);
const blocks = [];
for (let i = 0; i < blockCount; i++) {
  const us = bi.readUInt32BE(q), cs = bi.readUInt32BE(q + 4), fl = bi.readUInt16BE(q + 8);
  blocks.push({ us, cs, fl }); q += 10;
}
let totU = 0, totC = 0;
for (const b of blocks) { totU += b.us; totC += b.cs; }
log('blocks total uncompressed=' + totU + ' compressed=' + totC);
log('(sanity) bundle data region expected ~ size(' + 518837 + ') - header/dir');

// directory
const nodeCount = bi.readUInt32BE(q); q += 4;
log('nodeCount=' + nodeCount);
const nodes = [];
for (let i = 0; i < nodeCount; i++) {
  if (q + 20 > bi.length) { log('  !! directory truncated at node ' + i); break; }
  const off = Number(bi.readBigInt64BE(q)); const size = Number(bi.readBigInt64BE(q + 8));
  const nf = bi.readUInt32BE(q + 16); q += 20;
  let s = q; while (q < bi.length && bi[q] !== 0) q++; const name = bi.slice(s, q).toString('utf8'); q++;
  nodes.push({ off, size, nf, name });
}
log('parsed nodes=' + nodes.length);
for (let i = 0; i < nodes.length; i++) log('  [' + i + '] off=' + nodes[i].off + ' size=' + nodes[i].size + ' flags=' + nodes[i].nf + '  ' + nodes[i].name);

// ---- verify 19 materials against directory names ----
const names = [];
for (let i = 1; i <= 9; i++) names.push('Die Border ' + i + '_阶段丙修复');
for (let i = 1; i <= 9; i++) names.push('Die One Side ' + i + '_阶段丙修复');
names.push('Die Outline_阶段丙修复');
log('');
log('--- 19 materials vs DIRECTORY ---');
let hit = 0;
for (const n of names) {
  const found = nodes.some(x => x.name.includes(n) || x.name.includes(n + '.mat'));
  if (found) hit++;
  log((found ? 'HIT ' : 'MISS') + '  ' + n);
}
log('DIRECTORY HITS: ' + hit + ' / 19');

// ---- decompress every block and search raw for the 19 names (byte-level, post-decompress) ----
let cursor = 64 + 288; // start of block data (may need alignment)
// Unity aligns block data to 16 bytes
cursor = Math.ceil(cursor / 16) * 16;
log('');
log('block data starts at ' + cursor);
let decompressedAll = Buffer.alloc(0);
for (let i = 0; i < blocks.length; i++) {
  const b = blocks[i];
  const comp = tj.slice(cursor, cursor + b.cs);
  cursor += b.cs;
  let out;
  const ct = b.fl & 0x3f;
  if (ct === 0) out = comp.slice(0, b.us);
  else if (ct === 2 || ct === 3) out = lz4Block(comp, b.us);
  else out = Buffer.alloc(0);
  decompressedAll = Buffer.concat([decompressedAll, out]);
}
log('total decompressed block data = ' + decompressedAll.length + ' bytes');
let rawHits = 0;
for (const n of names) {
  const nb = Buffer.from(n, 'utf8');
  let c = 0, p = 0; while ((p = decompressedAll.indexOf(nb, p)) !== -1) { c++; p++; }
  if (c > 0) rawHits++;
  log((c > 0 ? 'RAW-HIT ' : 'RAW-MISS') + ' ' + c + '  ' + n);
}
log('RAW-DECOMPRESSED HITS: ' + rawHits + ' / 19');

fs.writeFileSync(OUT, lines.join('\n'), 'utf8');
fs.writeFileSync(OUT_JSON, JSON.stringify({ blockCount, blocks, nodes }, null, 2), 'utf8');
