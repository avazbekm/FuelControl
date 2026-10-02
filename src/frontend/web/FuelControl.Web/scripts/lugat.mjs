// Desktop lug'atidan (Til.cs) PWA uchun JSON yasaydi: kalit → [lotin, kirill, ruscha].
// Ishga tushirish: npm run lugat
import { readFileSync, writeFileSync } from 'node:fs';

const manba = readFileSync(new URL('../../../desktop/FuelControl.Desktop/Services/Til.cs', import.meta.url), 'utf8');
const s = String.raw`"((?:[^"\\]|\\.)*)"`;
const qator = new RegExp(String.raw`\["([^"]+)"\]\s*=\s*\(\s*` + s + String.raw`\s*,\s*` + s + String.raw`\s*,\s*` + s + String.raw`\s*\)`, 'g');
const ochir = (x) => JSON.parse(`"${x}"`);
const lugat = {};
for (const m of manba.matchAll(qator)) lugat[m[1]] = [ochir(m[2]), ochir(m[3]), ochir(m[4])];
writeFileSync(new URL('../src/app/core/lugat.json', import.meta.url), JSON.stringify(lugat, null, 1) + '\n');
console.log(`${Object.keys(lugat).length} ta kalit yozildi.`);
