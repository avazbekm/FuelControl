// Kodda ishlatilgan lug'at kalitlarini lug'atlar bilan solishtiradi: yetishmayotganlarini chiqaradi.
// Ishga tushirish: node scripts/lugat-tekshir.mjs   
// Topadi: til.t('Kalit'), t('Kalit'), kalit: 'Kalit', 'Prefiks_' + ... (dinamik kalitlar uchun faqat prefiks ogohlantiriladi).
import { readFileSync, readdirSync, statSync } from 'node:fs';
import { join } from 'node:path';

const root = new URL('../src/app/', import.meta.url).pathname.replace(/^\/([A-Za-z]:)/, '$1');
const oqi = (f) => JSON.parse(readFileSync(new URL('../src/app/core/' + f, import.meta.url), 'utf8'));
const lugat = { ...oqi('lugat.json'), ...oqi('lugat-web.json') };

function fayllar(d) {
  return readdirSync(d).flatMap((n) => {
    const p = join(d, n);
    return statSync(p).isDirectory() ? (n === 'mock' ? [] : fayllar(p)) : /\.(ts|html)$/.test(n) ? [p] : [];
  });
}

const topilgan = new Map();
for (const f of fayllar(root)) {
  if (f.endsWith('til.ts')) continue; // izohdagi "til.t('Kalit')" namunasi
  const m = readFileSync(f, 'utf8');
  for (const x of m.matchAll(/\bt\(\s*'([A-Za-z][A-Za-z0-9_]*)'/g)) topilgan.set(x[1], f);
  for (const x of m.matchAll(/(?:kalit|qisqaKalit|sarlavhaKalit)\s*:\s*'([A-Za-z][A-Za-z0-9_]*)'/g)) topilgan.set(x[1], f);
  for (const x of m.matchAll(/'((?:Nasiya|Tolov|Smena|Xarajat|Bak|Narx|Hisobot|Operator|Audit|Sozlama|Ruxsat|Savdo|Boshqaruv|Aparat|Rol|Harakat|Holat|Guruh|Tur)_[A-Za-z0-9_]*)'/g)) topilgan.set(x[1], f);
}
const yoq = [...topilgan].filter(([k]) => !(k in lugat) && !k.endsWith('_'));
for (const [k, f] of yoq.sort()) console.log(`YO'Q: ${k}   (${f.replace(root, '')})`);
console.log(`${topilgan.size} ta kalit tekshirildi, ${yoq.length} tasi lug'atda yo'q.`);
