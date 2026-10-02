# FuelControl — Web (Angular PWA) topshirig'i

Bu hujjat web sessiyasi uchun. Loyiha: `D:\Prepared\FuelControl`. Desktop (Avalonia) va backend (ASP.NET Core API) alohida sessiyalar yozgan; sizning vazifangiz — **telefon va brauzer uchun PWA**. Asosiy foydalanuvchi — **boshliqlar** (hisobotlarni telefondan, tashqaridan ko'rish); ikkinchi — operatorlar (telefonda sotuv kiritish). Asl TZ: `C:\Users\siddi\Downloads\FuelControl — texnik topshiriq (TZ).docx`.

Dizayn hujjatlari: `docs/backend-topshiriq.md` (domen va API tavsifi — majburiy o'qing). Desktop dizayni — "Liquid Glass" (iOS 26 uslubi): shaffof shisha kartalar, pill (to'liq yumaloq) tugmalar, yumshoq rangli mesh fon, ko'k `#2F6BFF` asosiy rang, ingichka chiziqli ikonkalar, emoji yo'q. Qorong'i rejim bor. Mobil PWA ham **shu uslubda** bo'lsin (CSS'da `backdrop-filter: blur` ishlaydi — desktop'dan farqli, bu yerda haqiqiy shisha effekti qiling). Ranglar: fon `#F2F6FF`, matn `#0B1530`, ikkilamchi `#4F607D`, yashil `#0E7A4A`, qizil `#D7262B`, qorong'ida fon `#0C1120`, matn `#F1F4FA`.

## 1. Texnologiya

- **Angular 20+** (standalone components, signals), TypeScript, **PWA** (`@angular/pwa`: service worker, manifest, "Bosh ekranga qo'shish").
- API klienti **OpenAPI'dan generatsiya**: API ishga tushganda `http://localhost:5000/openapi/v1.json` (portni `src/FuelControl.Api/Properties/launchSettings.json` dan tekshiring). Generator: `openapi-typescript` + `openapi-fetch` yoki `ng-openapi-gen` — bittasini tanlang, generatsiya buyrug'ini `package.json` skriptiga qo'ying.
- Real vaqt: **SignalR** (`@microsoft/signalr`), hub `/hub`, token `?access_token=` orqali; hodisalar: `SotuvQoshildi`, `SotuvOzgardi`, `SmenaOzgardi`, `NarxOzgardi`.
- Auth: `POST /auth/login` {login, parolYokiPin} → JWT (12 soat), `localStorage`da; har so'rovga `Authorization: Bearer`. 401 → login sahifasi. Ruxsatlar JWT javobidagi `foydalanuvchi.ruxsatlar` massivida (matn: "Boshqaruv", "SotuvKiritish", …) — menyu va tugmalar shunga qarab.
- Enum'lar JSON'da **matn** ("Naqd", "Operator").
- Pul: butun so'm, formati `16 600 000` (bo'shliq ming ajratgich); litr 2 xona `237 323.06`.
- **3 til**: o'zbek lotin (standart), o'zbek kirill, rus — o'z i18n xizmati (JSON lug'at), til tanlash login va sozlamalarda. Desktop lug'ati `src/FuelControl.Desktop/Services/Til.cs` da — kalit va tarjimalarni **shundan oling**, bir xil bo'lsin.
- Joylashuv: `src/FuelControl.Web/` (Angular loyiha). Build natijasi keyin API'ning `wwwroot`iga joylanadi (Docker'da api konteyneri statik beradi) — `angular.json`da `outputPath` ni `../FuelControl.Api/wwwroot` qilib qo'ying, lekin **API loyihasiga boshqa tegmang**.
- Dev'da API boshqa portda — `proxy.conf.json` bilan `/api`siz to'g'ridan-to'g'ri yo'llar (`/auth`, `/sotuvlar`, …) va `/hub` ni proxy qiling.

## 2. Ekranlar (mobil-birinchi, 390 px dan boshlab; planshet/desktop'da ham chiroyli)

Pastki tab-bar (telefon) / yon menyu (keng ekran). Faqat ruxsati bor bo'limlar ko'rinadi.

1. **Kirish** — foydalanuvchi ro'yxati emas, login + parol/PIN maydoni (operator uchun raqamli PIN klaviaturasi ixtiyoriy). Til tanlash. "Eslab qolish".
2. **Boshqaruv** (`GET /boshqaruv/bugun`) — bugungi savdo, litr, sotuv soni, ochiq smenalar, oy kamomati; to'lov turlari ulushi (naqd/plastik/click), yoqilg'i turlari; 14 kunlik ustun grafik; operatorlar; oxirgi sotuvlar (real vaqtda yangilanadi).
3. **Hisobotlar** (`GET /hisobot?dan&gacha&operatorId&guruh=operator|kun|oy`) — sana oralig'i, tez davr (bugun/kecha/7 kun/shu oy/o'tgan oy), operator filtri, guruhlash; jadval: operator×yoqilg'i×to'lov turi + jami; yakun KPI (jami, litr, naqd, plastik, click, kamomat, avans, bekor soni). **Excel/CSV eksport** (Eksport ruxsati) — brauzerda fayl yuklab olish.
4. **Smenalar** (`GET /smenalar`, `/smenalar/{id}` sotuvlari `GET /sotuvlar?smenaId=`) — ro'yxat (operator, boshlandi, davomiylik, sotuv soni, jami, farq/kamomat badge), tafsilot: kutilgan vs topshirilgan (naqd/plastik/click/jami). Smena ochish/yopish (`POST /smenalar/och`, `POST /smenalar/{id}/yop` {naqd, plastik, click, izoh}) — ruxsat bo'lsa; yopish formasida farq jonli hisoblanadi, kamomat qizil.
5. **Sotuv kiritish** (operator, `POST /sotuvlar`) — aparat tanlash (katta plitkalar: raqam, yoqilg'i, narx, L totalizator), summa yoki litr (avto-hisob), to'lov turi Naqd/Plastik/Click/Aralash (aralashda qismlar yig'indisi tekshiriladi), saqlash. **IdempotencyKey** — har sotuv uchun `crypto.randomUUID()`; **offline navbat**: internet yo'q bo'lsa sotuv IndexedDB'ga yoziladi va ulanganda yuboriladi (bir xil kalit — ikki marta yozilmaydi). Bugungi sotuvlarim ro'yxati. Sotuvni tahrirlash/bekor qilish (ruxsat bo'lsa, sabab majburiy).
6. **Operatorlar hisobi** (`GET /operatorlar/{id}/hisob?oy=`) — kartalar: maosh, qoldiq; oylik ko'rsatkichlar; harakatlar; **Avans/to'lov berish** (`POST /operatorlar/{id}/harakat`, AvansBerish ruxsati).
7. **Audit** (`GET /audit?q=`) — ro'yxat, qidiruv.
8. Sozlamalar PWA'da **kerak emas** (Admin desktop'dan qiladi) — faqat til va qorong'i rejim, chiqish.

Aloqa yo'q bo'lsa — yuqorida "Aloqa yo'q" bannerи; SignalR qayta ulanadi.

## 3. Ish tartibi

1. API'ni ishga tushiring (`dotnet run --project src/FuelControl.Api`, dev admin: `admin / admin1234`, `appsettings.Development.json`), OpenAPI'dan klient generatsiya qiling.
2. Skelet: auth, layout (tab-bar/yon menyu), i18n, tema, PWA manifest/service worker.
3. Ekranlar: Boshqaruv → Hisobotlar → Smenalar → Sotuv (offline navbat bilan) → Operatorlar → Audit.
4. `ng build` xatosiz, Lighthouse PWA tekshiruvi o'tsin, telefon o'lchamida (390×844) va desktop'da skrinshot bilan tekshiring.
5. Desktop va API kodiga tegmang. Har bosqich oxirida nazoratchi sessiyaga (siddi-5d) qisqa hisobot: nima qilindi, qanday tekshirildi, ochiq savollar. Noaniq joyda taxmin qilmay so'rang.
