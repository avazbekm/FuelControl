// Build vaqti va git commit'ini src/app/core/versiya.ts ga yozadi (npm start/build/watch/build:pages oldidan avtomatik ishlaydi).
// Ilova buni kirish sahifasida, yon menyuda va Sozlamalarda ko'rsatadi — foydalanuvchi qaysi versiyani ochganini bilish uchun.
// versiya.ts .gitignore'da (har build'da qayta yoziladi). git yo'q muhitda (masalan, .git'siz Docker konteksti) commit "nomalum" bo'ladi, build yiqilmaydi.
import { execSync } from 'node:child_process';
import { writeFileSync } from 'node:fs';

const t = new Intl.DateTimeFormat('en-CA', {
  timeZone: 'Asia/Tashkent', year: 'numeric', month: '2-digit', day: '2-digit', hour: '2-digit', minute: '2-digit', hour12: false,
}).formatToParts(new Date()).reduce((a, p) => ({ ...a, [p.type]: p.value }), {});
let kommit = '';
try { kommit = execSync('git rev-parse --short HEAD', { stdio: ['ignore', 'pipe', 'ignore'] }).toString().trim(); } catch { /* git yo'q yoki .git yo'q */ }
const vaqt = `${t.year}-${t.month}-${t.day} ${t.hour === '24' ? '00' : t.hour}:${t.minute}`;
const belgi = kommit ? `${vaqt} · ${kommit}` : vaqt;
writeFileSync(
  new URL('../src/app/core/versiya.ts', import.meta.url),
  `// Avtomatik yaratiladi (scripts/versiya.mjs) — qo'lda tahrirlamang, git'ga qo'shilmaydi.\n` +
  `export const BUILD_VAQTI = '${vaqt}';\n` +
  `export const BUILD_KOMMIT = '${kommit || 'nomalum'}';\n` +
  `/** Ko'rsatish uchun tayyor belgi: "2026-10-06 16:51 · 1f43a18" (commit noma'lum bo'lsa — faqat vaqt). */\n` +
  `export const BUILD_BELGISI = '${belgi}';\n`,
);
console.log(`versiya.ts: ${belgi}`);
