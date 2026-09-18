// A1-ARTIFACT-01 : decompress WxMiniGame.data.br and search for 19 dice materials.
// Output written to files under D:\NewOne\Temp\ (stdout is unreliable in this sandbox).
const fs = require('fs');
const zlib = require('zlib');

const DATA_BR = 'D:/NewOne/Client-Dice/Builds/WxMiniGame/Build/WxMiniGame.data.br';
const OUT_JSON = 'D:/NewOne/Temp/material_probe_result.json';
const OUT_TXT = 'D:/NewOne/Temp/material_probe_result.txt';

const lines = [];
function log(s) { lines.push(s); }

// 19 expected material names (rename suffix _阶段丙修复 = "stage-B fix")
const names = [];
for (let i = 1; i <= 9; i++) names.push('Die Border ' + i + '_阶段丙修复');
for (let i = 1; i <= 9; i++) names.push('Die One Side ' + i + '_阶段丙修复');
names.push('Die Outline_阶段丙修复');

const out = {
  file: DATA_BR,
  compressedBytes: 0,
  decompressedBytes: 0,
  decompressError: null,
  headerHex: '',
  headerAscii: '',
  total: names.length,
  utf8: { hits: 0, rows: [] },
  utf16le: { hits: 0, rows: [] },
  combined: { hits: 0, rows: [] },
  genericTokens: {},
  anyStageB: 0,
  anyStageBUtf16: 0,
};

let raw = null;
try {
  const buf = fs.readFileSync(DATA_BR);
  out.compressedBytes = buf.length;
  raw = zlib.brotliDecompressSync(buf);
  out.decompressedBytes = raw.length;
  out.headerHex = raw.slice(0, 64).toString('hex');
  out.headerAscii = raw.slice(0, 64).toString('latin1').replace(/[^\x20-\x7e]/g, '.');
} catch (e) {
  out.decompressError = String(e && e.stack ? e.stack : e);
}

function countOccurrences(haystack, needle) {
  if (needle.length === 0) return 0;
  let count = 0, pos = 0;
  while ((pos = haystack.indexOf(needle, pos)) !== -1) { count++; pos += 1; }
  return count;
}
function findFirst(haystack, needle) {
  const p = haystack.indexOf(needle);
  return p;
}

if (raw) {
  for (const name of names) {
    const n8 = Buffer.from(name, 'utf8');
    const n16 = Buffer.from(name, 'utf16le');
    const c8 = countOccurrences(raw, n8);
    const c16 = countOccurrences(raw, n16);
    const row = { name, utf8: c8, utf16le: c16, hit: (c8 > 0 || c16 > 0) };
    if (c8 > 0) { out.utf8.hits++; out.utf8.rows.push(row); }
    if (c16 > 0) { out.utf16le.hits++; out.utf16le.rows.push(row); }
    out.combined.rows.push(row);
    if (row.hit) out.combined.hits++;
  }
  out.anyStageB = countOccurrences(raw, Buffer.from('_阶段丙修复', 'utf8'));
  out.anyStageBUtf16 = countOccurrences(raw, Buffer.from('_阶段丙修复', 'utf16le'));

  // generic tokens
  const tokens = {
    'Die Border ': ['Die Border ', 'utf8'],
    'Die One Side ': ['Die One Side ', 'utf8'],
    'Die Outline': ['Die Outline', 'utf8'],
    'Die Border _utf16': ['Die Border ', 'utf16le'],
    'Die One Side _utf16': ['Die One Side ', 'utf16le'],
  };
  for (const [label, [tok, enc]] of Object.entries(tokens)) {
    out.genericTokens[label] = countOccurrences(raw, Buffer.from(tok, enc));
  }
}

// ---- human readable report ----
log('=== A1-ARTIFACT-01 material probe ===');
log('file: ' + out.file);
log('compressed bytes   : ' + out.compressedBytes);
log('decompressed bytes : ' + out.decompressedBytes);
log('decompress error   : ' + (out.decompressError || '(none)'));
log('header hex  : ' + out.headerHex);
log('header ascii: ' + out.headerAscii);
log('');
log('--- 19 material strings ---');
for (const r of out.combined.rows) {
  log((r.hit ? 'HIT ' : 'MISS') + '  utf8=' + r.utf8 + ' utf16le=' + r.utf16le + '  ' + r.name);
}
log('');
log('combined hits (any encoding): ' + out.combined.hits + ' / ' + out.total);
log('utf8 hits    : ' + out.utf8.hits + ' / ' + out.total);
log('utf16le hits : ' + out.utf16le.hits + ' / ' + out.total);
log('');
log('occurrences of suffix "_阶段丙修复" (utf8)   : ' + out.anyStageB);
log('occurrences of suffix "_阶段丙修复" (utf16le): ' + out.anyStageBUtf16);
log('generic token counts: ' + JSON.stringify(out.genericTokens));

fs.writeFileSync(OUT_JSON, JSON.stringify(out, null, 2), 'utf8');
fs.writeFileSync(OUT_TXT, lines.join('\n'), 'utf8');
// also a tiny sentinel so we know the script reached the end
fs.writeFileSync('D:/NewOne/Temp/_probe_done.flag', 'ok ' + out.combined.hits + '/' + out.total, 'utf8');
