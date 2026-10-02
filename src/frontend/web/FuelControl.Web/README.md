# FuelControl.Web — mobil PWA (Angular 22)

Boshliqlar uchun hisobot/boshqaruv paneli, operatorlar uchun sotuv kiritish (offline navbat bilan).

## Buyruqlar

| Buyruq | Nima qiladi |
|---|---|
| `npm start` | dev server (http://localhost:4200), API yo'llari `proxy.conf.json` orqali `localhost:5000` ga |
| `npm run build` | production build → `../../../backend/FuelControl.Api/wwwroot` (service worker bilan) |
| `npm run api` | ishlab turgan API'dan (`/openapi/v1.json`) `src/app/api/schema.d.ts` ni qayta generatsiya |
| `npm run lugat` | desktop `Til.cs` dan `src/app/core/lugat.json` ni qayta yasash |

## Tuzilma

- `api/` — `openapi-fetch` klienti + generatsiya qilingan sxema; `turlar.ts` — qulay turlar (`Son<>` .NET'ning `number | string` ini toraytiradi).
- `core/` — auth (JWT, "eslab qolish"), til (uz/uzk/ru), tema, aloqa (SignalR `/hub`, onlayn holat), navbat (IndexedDB offline sotuvlar, IdempotencyKey), formatlar.
- `qobiq/` — tab-bar (telefon) / yon menyu (≥900px), aloqa banneri; bo'limlar ruxsatlarga qarab (`core/bolimlar.ts`).
- `sahifalar/` — kirish, boshqaruv, hisobotlar, smenalar (+tafsilot/yopish), sotuv, operatorlar (+hisob), audit, sozlamalar.

Marshrutlash hash rejimida (`/#/smenalar`) — API yo'llari bilan bir domenda to'qnashmaydi.
Lug'at: asosiy kalitlar `lugat.json` (Til.cs dan, qo'lda tahrirlamang), PWA'ga xoslari `lugat-web.json`.
