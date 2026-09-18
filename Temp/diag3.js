// A1-ARTIFACT-01 diag3 : extract archive members, inspect data.tj3d structure
const fs = require('fs');
const zlib = require('zlib');
const DATA_BR = 'D:/NewOne/Client-Dice/Builds/WxMiniGame/Build/WxMiniGame.data.br';
const OUTDIR = 'D:/NewOne/Temp/extracted/';
const OUT = 'D:/NewOne/Temp/diag3_result.txt';
const lines = [];
function log(s) { lines.push(s); }
function mkdirp(d) { try { fs.mkdirSync(d, { recursive: true }); } catch (e) {} }
mkdirp(OUTDIR);

const buf = fs.readFileSync(DATA_BR);
const raw = zlib.brotliDecompressSync(buf);

const headerSize = raw.readUInt32LE(18);
let p = 22; const files = [];
while (p < headerSize + 18) {
  const off = raw.readUInt32LE(p), size = raw.readUInt32LE(p + 4), nameLen = raw.readUInt32LE(p + 8);
  if (nameLen <= 0 || nameLen > 300) break;
  const name = raw.slice(p + 12, p + 12 + nameLen).toString('utf8');
  files.push({ off, size, name });
  p += 12 + nameLen;
}
log('members:');
for (const f of files) log('  ' + f.name + '  off=' + f.off + ' size=' + f.size);

for (const f of files) {
  const data = raw.slice(f.off, f.off + f.size);
  const safe = f.name.replace(/[\/\\]/g, '_');
  fs.writeFileSync(OUTDIR + safe, data);
}

// inspect data.tj3d
const tj = raw.slice(files[0].off, files[0].off + files[0].size);
log('');
log('--- data.tj3d header ---');
log('first 64 hex : ' + tj.slice(0, 64).toString('hex'));
log('first 80 asc : ' + tj.slice(0, 80).toString('latin1').replace(/[^\x20-\x7e]/g, '.'));
log('u32[0..7]    : ' + Array.from({ length: 8 }, (_, i) => tj.readUInt32LE(i * 4)).join(', '));
// Unity SerializedFile signature guess: magic at end "TuanjieFS"/"UnityFS" for bundles
log('has "UnityFS" : ' + (tj.indexOf(Buffer.from('UnityFS')) >= 0));
log('has "TuanjieFS": ' + (tj.indexOf(Buffer.from('TuanjieFS')) >= 0));
log('first byte    : 0x' + tj[0].toString(16) + '  (bundle sig if 0x55="U")');

// search data.tj3d for material-like names
const names = [];
for (let i = 1; i <= 9; i++) names.push('Die Border ' + i + '_阶段丙修复');
for (let i = 1; i <= 9; i++) names.push('Die One Side ' + i + '_阶段丙修复');
names.push('Die Outline_阶段丙修复');
log('');
log('--- search 19 names INSIDE data.tj3d ---');
let hits = 0;
for (const n of names) {
  const c = (() => { let c = 0, q = 0; const nb = Buffer.from(n, 'utf8'); while ((q = tj.indexOf(nb, q)) !== -1) { c++; q++; } return c; })();
  if (c > 0) hits++;
  log((c > 0 ? 'HIT ' : 'MISS') + ' ' + c + '  ' + n);
}
log('hits inside data.tj3d: ' + hits + '/19');

// Also: are DemoB "Die *" original material names present? and mesh names?
for (const probe of ['Die_Voxel_Borders', 'Die_Voxel_Outline_0', 'Die_Voxel_Planes_0', 'Die Border 1', 'Gamepads_Sprite_Atlas_01', 'Border_Texture_Mat_Base_Color', 'Splash Screen', 'SplashScreen', 'tuanjie_builtin_extra']) {
  const c = (() => { let c = 0, q = 0; const nb = Buffer.from(probe, 'utf8'); while ((q = tj.indexOf(nb, q)) !== -1) { c++; q++; } return c; })();
  log('probe ' + c + '  ' + probe);
}

fs.writeFileSync(OUT, lines.join('\n'), 'utf8');
