// A1-ARTIFACT-01 diag4 : LZ4-decompress UnityFS bundle directory of data.tj3d
const fs = require('fs');
const zlib = require('zlib');
const DATA_BR = 'D:/NewOne/Client-Dice/Builds/WxMiniGame/Build/WxMiniGame.data.br';
const OUT = 'D:/NewOne/Temp/diag4_result.txt';
const lines = [];
function log(s) { lines.push(s); }

const raw = zlib.brotliDecompressSync(fs.readFileSync(DATA_BR));
const tj = raw.slice(241, 241 + 518837);

// ---- LZ4 block decompressor (raw block format, no frame header) ----
function lz4Block(src, dstSize) {
  const dst = Buffer.alloc(dstSize);
  let s = 0, d = 0;
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
    ml += 4;
    let mp = d - offset;
    for (let i = 0; i < ml; i++) { if (mp < 0 || d >= dst.length) return dst; dst[d++] = dst[mp++]; }
  }
  return dst;
}

// ---- parse UnityFS header ----
let o = 0;
const magic = tj.slice(0, 8).toString('latin1'); o = 8;
const ver = tj.readUInt32BE(o); o += 4;
function readStr() { const s = o; while (tj[o] !== 0) o++; const str = tj.slice(s, o).toString('utf8'); o++; return str; }
const verStr = readStr();
const unityVer = readStr();
const bundleSize = Number(tj.readBigInt64BE(o)); o += 8;
const cbiSize = tj.readUInt32BE(o); o += 4;
const ubiSize = tj.readUInt32BE(o); o += 4;
const flags = tj.readUInt32BE(o); o += 4;
const hdrEnd = o;
log('magic=' + JSON.stringify(magic) + ' ver=' + ver + ' verStr=' + verStr + ' unity=' + unityVer);
log('bundleSize=' + bundleSize + ' cbiSize=' + cbiSize + ' ubiSize=' + ubiSize + ' flags=0x' + flags.toString(16));
log('header end offset = ' + hdrEnd);
const compression = flags & 0x3f;
log('compression type = ' + compression + ' (0=none,1=LZMA,2=LZ4,3=LZ4HC)');
log('encryptionFlag(0x200) = ' + ((flags & 0x200) ? 'SET' : 'unset'));

// ---- try to decompress blocksInfo from several candidate offsets ----
function looksLikeDir(buf) {
  return buf.includes(Buffer.from('Assets/')) || buf.includes(Buffer.from('CAB-')) || buf.includes(Buffer.from('.mat'));
}
let bi = null, usedOff = -1;
for (let off = hdrEnd; off <= hdrEnd + 32; off++) {
  const chunk = tj.slice(off, off + cbiSize);
  if (compression === 2 || compression === 3) {
    const out = lz4Block(chunk, ubiSize + 4096); // allow slack
    // count printable ratio
    let pr = 0; for (let i = 0; i < Math.min(out.length, 486); i++) if (out[i] >= 0x20 && out[i] < 0x7f) pr++;
    if (pr > 200 || looksLikeDir(out)) {
      bi = out; usedOff = off;
      log('LZ4 succeed at offset ' + off + ' printable=' + pr);
      break;
    }
  }
}
if (!bi) {
  log('LZ4 attempts failed at all offsets ' + hdrEnd + '..' + (hdrEnd + 32));
  // dump raw bytes at hdrEnd for diagnosis
  log('bytes@hdrEnd: ' + tj.slice(hdrEnd, hdrEnd + 48).toString('hex'));
} else {
  log('blocksInfo raw (first 486) hex: ' + bi.slice(0, Math.min(bi.length, 486)).toString('hex'));
  log('blocksInfo ascii: ' + bi.slice(0, Math.min(bi.length, 486)).toString('latin1').replace(/[^\x20-\x7e]/g, '.'));
  // parse blocksInfo
  let q = 0;
  const hash = bi.slice(0, 16); q = 16;
  const blockCount = bi.readUInt32BE(q); q += 4;
  log('blockCount=' + blockCount);
  const blocks = [];
  for (let i = 0; i < blockCount && i < 5000; i++) {
    const us = bi.readUInt32BE(q), cs = bi.readUInt32BE(q + 4), fl = bi.readUInt16BE(q + 8);
    blocks.push({ us, cs, fl });
    q += 10;
  }
  for (let i = 0; i < Math.min(blocks.length, 12); i++) log('  block[' + i + '] uncomp=' + blocks[i].us + ' comp=' + blocks[i].cs + ' flags=0x' + blocks[i].fl.toString(16));
  // directory (combined) follows
  if (flags & 0x40) {
    if (q + 4 <= bi.length) {
      const nodeCount = bi.readUInt32BE(q); q += 4;
      log('nodeCount=' + nodeCount);
      const nodes = [];
      for (let i = 0; i < nodeCount && i < 20000; i++) {
        if (q + 20 > bi.length) break;
        const off = Number(bi.readBigInt64BE(q)); const size = Number(bi.readBigInt64BE(q + 8));
        const nf = bi.readUInt32BE(q + 16); q += 20;
        let s = q; while (q < bi.length && bi[q] !== 0) q++; const name = bi.slice(s, q).toString('utf8'); q++;
        nodes.push({ off, size, nf, name });
      }
      log('parsed nodes: ' + nodes.length);
      const mats = nodes.filter(n => n.name.includes('DemoC') || n.name.includes('阶段丙修复') || n.name.endsWith('.mat'));
      log('--- directory node names (first 60) ---');
      for (let i = 0; i < Math.min(nodes.length, 60); i++) log('  [' + i + '] ' + nodes[i].name + '  (size=' + nodes[i].size + ')');
      log('--- material-ish nodes ---');
      for (const n of mats) log('  MAT ' + n.name + ' size=' + n.size);
    }
  }
}

fs.writeFileSync(OUT, lines.join('\n'), 'utf8');
