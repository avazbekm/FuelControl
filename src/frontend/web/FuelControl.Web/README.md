# FuelControl.Web — mobil PWA (Angular 22)

Smena hisobi: operator smenani ochadi (3 ta qo'lda qoldiq), kun davomida nasiya, qarz qaytishi va xarajat yozadi, oxirida pult ko'rsatkichlari, terminal, depozit va sanalgan naqdni kiritadi — kutilgan naqd va kamomat jonli hisoblanadi. Boshliqlar uchun boshqaruv paneli, smenalar, nasiyalar, hisobotlar, operatorlar hisobi, audit va sozlamalar. Spetsifikatsiya: `docs/smena-hisobi-topshiriq.md`, dizayn: `docs/dizayn/*.dc.html`.

## Buyruqlar

| Buyruq | Nima qiladi |
|---|---|
| `npm start` | dev server (http://localhost:4200), API yo'llari `proxy.conf.json` orqali `localhost:5000` ga. **API'ga yangi yo'l qo'shilsa, uni `proxy.conf.json` ga ham qo'shing** (aks holda dev server HTML qaytaradi va sahifa "aloqa yo'q" deydi), so'ng `npm start` ni qayta ishga tushiring |
| `npm run build` | production build → `../../../backend/FuelControl.Api/wwwroot` (service worker bilan). **To'g'ridan-to'g'ri `npx ng build` emas, `npm run build` ishlating**: `prebuild` `scripts/versiya.mjs` ni ishga tushirib `src/app/core/versiya.ts` (build vaqti va commit; git'da kuzatilmaydi) ni yaratadi — usiz build "versiya" moduli topilmasligidan yiqiladi. Xuddi shu `npm start`, `npm run watch` va `npm run build:pages` uchun ham avtomatik. git bo'lmagan muhitda (masalan, `.git`siz Docker konteksti) commit "nomalum" bo'ladi, build yiqilmaydi |
| `npm run build:pages` | Cloudflare Pages uchun build (`pages/_headers`, `API_MANZIL`) |
| `npm run api` | ishlab turgan API'dan (`/openapi/v1.json`) `src/app/api/schema.d.ts` ni qayta generatsiya |
| `npm run lugat` | desktop `Til.cs` dan `src/app/core/lugat.json` ni qayta yasash |
| `node scripts/lugat-tekshir.mjs` | kodda ishlatilgan lug'at kalitlari lug'atlarda borligini tekshiradi |

## Tuzilma

- `api/` — `openapi-fetch` klienti, generatsiya qilingan sxema (`schema.d.ts`), `model.ts` — sxemadan qulay tur nomlari.
- `core/` — `server.ts` (API bilan yagona fasad), auth (JWT, "eslab qolish"), til (uz/uzk/ru), tema, aloqa (SignalR `/hub`, onlayn holat), `hisob.ts` (smena formulasi — yopish jadvalida jonli hisob, server bilan bir xil), `telefon.ts`, formatlar.
- `qobiq/` — tab-bar (telefon) / yon menyu (≥900px), aloqa banneri; bo'limlar ruxsatlarga qarab (`core/bolimlar.ts`).
- `ui/` — umumiy komponentlar: dialog (`oyna`), son va telefon kiritish, belgilar, `dialoglar/` (nasiya, qarz qaytdi, xarajat, bakka kirim), Enter bilan keyingi maydonga o'tish.
- `sahifalar/` — kirish, savdo (+smenani yopish), nasiyalar, boshqaruv, smenalar, hisobotlar, operatorlar (+hisob), audit, sozlamalar.

Marshrutlash hash rejimida (`/#/smenalar`) — API yo'llari bilan bir domenda to'qnashmaydi.
Lug'at: asosiy kalitlar `lugat.json` (Til.cs dan, qo'lda tahrirlamang), faqat PWA'ga xos yoki ataylab farqli matnlar `lugat-web.json`.
Yangilanish: yangi versiya aniqlansa (service worker) ilova ochilishida o'zi qayta yuklanadi, aks holda "Yangi versiya" xabari chiqadi va keyingi sahifa almashishida yangilanadi.
