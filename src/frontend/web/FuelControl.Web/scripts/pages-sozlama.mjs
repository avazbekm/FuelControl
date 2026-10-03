// `npm run build:pages` oxirida ishlaydi: API_MANZIL muhit o'zgaruvchisi berilgan bo'lsa dist/pages/sozlama.json ga yozadi.
// Cloudflare Pages'da: Settings → Variables and Secrets → API_MANZIL = https://api.misol.uz (Build uchun).
// Berilmasa public/sozlama.json dagi qiymat (standart: bo'sh = shu domen) qoladi — Pages'da bu ISHLAMAYDI, ogohlantiramiz.
// sozlama.json service worker'ning ngsw.json hash ro'yxatida emas, shuning uchun uni build'dan keyin o'zgartirish xavfsiz.
import { readFileSync, writeFileSync, existsSync } from 'node:fs';

const fayl = new URL('../dist/pages/sozlama.json', import.meta.url);
if (!existsSync(fayl)) { console.error('dist/pages/sozlama.json topilmadi — avval `ng build --configuration production,pages`.'); process.exit(1); }

const manzil = (process.env.API_MANZIL ?? '').trim().replace(/\/+$/, '');
if (manzil) {
  if (!/^https?:\/\/[^\s/]+/i.test(manzil)) { console.error(`API_MANZIL yaroqsiz: "${manzil}" (masalan: https://api.misol.uz)`); process.exit(1); }
  const j = { ...JSON.parse(readFileSync(fayl, 'utf8')), api: manzil };
  writeFileSync(fayl, JSON.stringify(j, null, 2) + '\n');
  console.log(`sozlama.json: api = ${manzil}`);
} else {
  const hozirgi = JSON.parse(readFileSync(fayl, 'utf8')).api ?? '';
  if (hozirgi) console.log(`sozlama.json: api = ${hozirgi} (public/sozlama.json dan)`);
  else console.warn('OGOHLANTIRISH: API manzili berilmagan (API_MANZIL yo\'q, sozlama.json da "api" bo\'sh). Pages\'da ilova API\'ni o\'z domenidan izlaydi va ishlamaydi.');
}
