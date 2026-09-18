// A1-ARTIFACT-01 asm : list player assemblies from ScriptingAssemblies.json
const fs = require('fs');
const OUT = 'D:/NewOne/Temp/asm_result.txt';
const j = JSON.parse(fs.readFileSync('D:/NewOne/Temp/extracted/ScriptingAssemblies.json', 'utf8'));
const names = j.names || [];
const lines = [];
lines.push('total assemblies in player = ' + names.length);
lines.push('');
const nonUnity = names.filter(n => !n.startsWith('UnityEngine.'));
lines.push('non-UnityEngine assemblies (' + nonUnity.length + '):');
for (const n of nonUnity) lines.push('  ' + n);
lines.push('');
lines.push('engine module count = ' + names.filter(n => n.startsWith('UnityEngine.') && n.endsWith('Module.dll')).length);
lines.push('test/nunit related:');
for (const n of names) if (/test|nunit|TestRunner/i.test(n)) lines.push('  ' + n);
fs.writeFileSync(OUT, lines.join('\n'), 'utf8');
