# FuelControl — Backend (server) topshirig'i

Bu hujjat backend sessiyasi uchun. Desktop (Avalonia) UI tayyor va `src/frontend/desktop/FuelControl.Desktop` da; hozir u `Services/MockMalumot.cs` dagi xotiradagi ma'lumot bilan ishlaydi. Vazifa: haqiqiy server yozish, keyin desktop'ni unga ulash.

Asl TZ: `C:\Users\siddi\Downloads\FuelControl — texnik topshiriq (TZ).docx` (o'qib chiqing). Quyida TZ'dan keyin qabul qilingan **qo'shimcha qarorlar** ham bor — ular TZ'dan ustun.

## 1. Loyiha tuzilmasi (FuelControl.slnx ga qo'shing)

```
src/backend/FuelControl.Contracts/   — DTO + enum'lar (desktop va PWA bilan umumiy), faqat POCO, bog'liqliksiz
src/backend/FuelControl.Core/        — domen modellari va biznes qoidalari (hisoblash, smena, totalizator, ruxsat tekshiruvi)
src/backend/FuelControl.Api/         — ASP.NET Core Minimal API (.NET 10), EF Core + SQLite (WAL), JWT, SignalR, OpenAPI
tests/FuelControl.Core.Tests/ — xUnit, biznes qoidalari uchun
deploy/                      — docker-compose.yml (api + caddy + cloudflared), Dockerfile
```

Kod uslubi: desktop'dagidek **o'zbekcha identifikatorlar** (Sotuv, Smena, Foydalanuvchi, Ruxsat…), izohlar o'zbekcha. Mavjud modellarni `src/frontend/desktop/FuelControl.Desktop/Models/Modellar.cs` dan asos qilib oling — nomlar bir xil bo'lsin, desktop'ga ulash oson bo'ladi.

## 2. Domen (Core) — TZ + qo'shimcha qarorlar

- **Foydalanuvchi**: Id, ToliqIsm, Login, Rol (Operator/Boshliq/Admin), Faol, OylikMaosh, parol/PIN **xeshi** (PBKDF2 yoki BCrypt), `Ruxsatlar` — **har foydalanuvchiga alohida ruxsatlar to'plami** (rolga bog'liq emas; rol faqat standart to'plamni beradi). Ruxsatlar ro'yxati: Boshqaruv, SotuvKiritish, Smenalar, Hisobotlar, Operatorlar, Audit, Sozlamalar, SmenaOchish, SmenaYopish, SotuvTahrirlash, SotuvBekorQilish, AvansBerish, Eksport. Standart: Operator = SotuvKiritish+SmenaOchish+SmenaYopish; Boshliq = hammasi, Sozlamalar'dan tashqari; Admin = hammasi.
- **YoqilgiTuri**: Nomi, Narx (long, so'm), Rang (hex). Admin yaratadi/tahrirlaydi/o'chiradi (aparatga biriktirilgan bo'lsa o'chirilmaydi). Narx o'zgarsa `NarxTarixi` ga yoziladi.
- **Aparat**: Raqam, YoqilgiTuriId, **TotalLitr (decimal)** — totalizator, pultdagi "Total → L". Yaratishda boshlang'ich qiymat kiritiladi; har faol sotuvda `TotalLitr += Litr`. Summa (S) totalizatori **kerak emas**.
- **Smena**: Operator, Boshlandi, Tugadi?, Kutilgan{Naqd,Plastik,Click} (sotuvlardan hisoblanadi), Topshirilgan{Naqd,Plastik,Click}? (yopishda operator kiritadi — **har to'lov turi alohida**), Izoh. Farq = Topshirilgan − Kutilgan; manfiy = kamomat → operator hisobiga `Kamomat` harakati, musbat → `Ortiqcha`. Smena ochilishi: operator ochadi (SmenaOchish ruxsati) **yoki birinchi sotuvda avtomatik**. Bir operatorda bir vaqtda faqat bitta ochiq smena.
- **Sotuv**: Smena, Operator, Aparat, Narx (sotuv paytidagi, saqlab qo'yiladi), Litr (2 xona), Summa (long), Vaqt, Tolovlar[] (Naqd/Plastik/**Click**; aralash = bir nechta qator, yig'indi = Summa), Holati (Faol/BekorQilingan), BekorSababi, BekorQilgan, **IdempotencyKey (Guid)** — bir xil kalit bilan qayta yuborilsa ikkinchi marta yozilmaydi. Hisob: summa kiritilsa litr = summa/narx (2 xona), litr kiritilsa summa = litr×narx (so'mgacha).
- **Sotuvni tahrirlash** (SotuvTahrirlash ruxsati): aparat, summa, to'lov turi o'zgaradi, **sabab majburiy**; eski/yangi qiymat auditga; totalizator va smena yakunlari qayta hisoblanadi (eski ayriladi, yangi qo'shiladi). **Bekor qilish** (SotuvBekorQilish): o'chirilmaydi, holat o'zgaradi, hisobotga kirmaydi, totalizator/smena yakunidan ayriladi.
- **HisobHarakati** (operator hisob-varaqasi): Operator, Sana, Turi (Maosh/Avans/Kamomat/Ortiqcha/Tolov), Summa (operator foydasiga +, hisobidan −), Izoh, KimYozdi. Qoldiq = Σ. Oylik maosh har oy boshida avtomatik `Maosh` harakati sifatida yoziladi (Hangfire shart emas — API ishga tushganda/so'rovda "shu oy uchun yozilganmi" tekshiruvi yetarli).
- **AuditYozuvi**: Kim, Vaqt, Amal, Tafsilot — barcha o'zgartiruvchi amallar (sotuv tahrir/bekor, narx, ruxsat, smena ochish/yopish, avans, foydalanuvchi/aparat/yoqilg'i CRUD, totalizator tuzatish).
- Pul — `long` so'm, litr — `decimal(18,2)`. Vaqt — Toshkent (UTC+5), bazada UTC saqlang.

## 3. API (Minimal API, JWT)

- `POST /auth/login` {login, parolYokiPin} → {token, foydalanuvchi, ruxsatlar}. Ketma-ket 5 xato → 15 min blok. Token 12 soat.
- `GET /me`
- Yoqilg'i: `GET/POST/PUT/DELETE /yoqilgilar`, `GET /yoqilgilar/narx-tarixi`
- Aparatlar: `GET/POST/PUT /aparatlar` (TotalLitr bilan)
- Foydalanuvchilar (Sozlamalar ruxsati): `GET/POST/PUT /foydalanuvchilar`, `PUT /foydalanuvchilar/{id}/ruxsatlar`, `POST /foydalanuvchilar/{id}/pin`
- Smenalar: `GET /smenalar?dan&gacha&operatorId`, `GET /smenalar/joriy`, `POST /smenalar/och`, `POST /smenalar/{id}/yop` {naqd, plastik, click, izoh}
- Sotuvlar: `POST /sotuvlar` (IdempotencyKey bilan; smena yo'q bo'lsa ochadi), `GET /sotuvlar?dan&gacha&operatorId&smenaId`, `PUT /sotuvlar/{id}` {aparatId, summa, tolovlar, sabab}, `POST /sotuvlar/{id}/bekor` {sabab}
- Hisobot: `GET /hisobot?dan&gacha&operatorId&guruh=operator|kun|oy` → operator×yoqilg'i×to'lov turi qatorlari + jami (naqd/plastik/click/litr/summa, kamomat, avans, bekor soni)
- Boshqaruv paneli: `GET /boshqaruv/bugun` (KPI, to'lov ulushlari, yoqilg'i ulushlari, 14 kunlik, operatorlar, oxirgi sotuvlar)
- Operator hisobi: `GET /operatorlar/{id}/hisob?oy=`, `POST /operatorlar/{id}/harakat` {turi: Avans|Tolov, summa, izoh}
- Audit: `GET /audit?q=&dan&gacha`
- SignalR hub `/hub`: `SotuvQoshildi`, `SotuvOzgardi`, `SmenaOzgardi`, `NarxOzgardi` — desktop va PWA real vaqtda yangilanadi.
- Ruxsat tekshiruvi **serverda** (endpoint filter / policy): ruxsat yo'q → 403. Operator faqat o'z sotuv/smenasini ko'radi va yopadi.
- OpenAPI (`/openapi/v1.json` + Scalar/Swagger UI) — PWA klienti shundan generatsiya qilinadi.
- Xatolar: ProblemDetails, o'zbekcha xabar.

## 4. Ma'lumotlar bazasi

EF Core + SQLite, WAL, `Data Source=/data/fuelcontrol.db` (Docker volume). Migratsiyalar ishga tushganda avtomatik (`Migrate()`). Seed: admin (login `admin`, parol birinchi ishga tushishda env'dan), yoqilg'ilar AI-92/AI-95/Dizel, 5 aparat.

## 5. Docker

`deploy/docker-compose.yml`: `api` (ASP.NET + keyin PWA statik fayllari), `caddy` (HTTPS reverse proxy), `cloudflared` (tunnel) — `restart: unless-stopped`. Kunlik zaxira: `/data/zaxira/fuelcontrol-YYYYMMDD.db` (SQLite `VACUUM INTO`), 30 kun saqlanadi — `POST /zaxira` endpoint ham (Sozlamalar → "Hozir nusxa olish").

## 6. Ish tartibi (muhim)

1. Avval `Contracts` + `Core` + testlar (hisoblash, smena yopish/kamomat, totalizator, sotuv tahrir/bekor qayta hisoblash, idempotency).
2. Keyin `Api` (baza, auth, endpoint'lar, SignalR, OpenAPI).
3. `dotnet build` va `dotnet test` **xatosiz** bo'lsin; API'ni `dotnet run` qilib `/openapi/v1.json` ochilishini tekshiring.
4. Desktop'ga tegmang — ulash keyingi bosqich, nazoratchi sessiya bilan kelishib qilinadi.
5. Har bosqich tugaganda qisqa hisobot bering: nima qilindi, qanday tekshirildi, ochiq savollar.
