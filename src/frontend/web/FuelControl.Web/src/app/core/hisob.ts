// Smena hisobi formulasi (docs/smena-hisobi-topshiriq.md §1.6). Server — yagona haqiqat manbai; klient jonli ko'rsatish uchun
// aynan shuni hisoblaydi (yopish jadvali). Hamma pul — butun so'm, litr — 2 xona; yaxlitlash butun sonlarda (suzuvchi nuqta xatosisiz).

/** Litr (2 xona) → "sentlar" (butun son). */
export const litrSent = (l: number) => Math.round(l * 100);
export const sentLitr = (s: number) => s / 100;

/** `Litr = Oxiri − Boshi` (2 xona). */
export function segmentLitri(boshi: number, oxiri: number): number {
  return sentLitr(litrSent(oxiri) - litrSent(boshi));
}

/** `Summa = round(Litr × Narx)` so'mgacha, 0.5 → yuqoriga (MidpointRounding.AwayFromZero; manfiyda ham). Butun sonlarda hisoblanadi. */
export function segmentSummasi(litr: number, narx: number): number {
  const p = litrSent(litr) * narx; // sent × so'm — butun
  return p >= 0 ? Math.floor((p + 50) / 100) : -Math.floor((-p + 50) / 100);
}

export interface Segment { boshi: number; oxiri: number; narx: number }

export interface SegmentNatijasi extends Segment { litr: number; summa: number }

export function segmentHisobla(s: Segment): SegmentNatijasi {
  const litr = segmentLitri(s.boshi, s.oxiri);
  return { ...s, litr, summa: segmentSummasi(litr, s.narx) };
}

export interface HisobKirish {
  qaytim: number; ochishTerminal: number; ochishDepozit: number;
  segmentlar: Segment[];
  yopishTerminal: number; yopishDepozit: number;
  nasiyaJami: number; qaytganNasiya: number; xarajatJami: number;
  sanalganNaqd?: number | null;
}

export interface HisobNatijasi {
  jamiLitr: number; savdo: number;
  plastik: number; depozitFarqi: number;
  kutilgan: number; farq: number | null;
}

/**
 * `Kutilgan = Qaytim + Savdo + QaytganNasiya − Plastik − DepozitFarqi − NasiyaJami − XarajatJami`; `Farq = SanalganNaqd − Kutilgan`.
 * `Plastik = YopishTerminal − OchishTerminal`; `DepozitFarqi = YopishDepozit − OchishDepozit` (manfiy bo'lishi mumkin).
 */
export function smenaHisobla(k: HisobKirish): HisobNatijasi {
  const s = k.segmentlar.map(segmentHisobla);
  const savdo = s.reduce((a, x) => a + x.summa, 0);
  const jamiLitr = sentLitr(s.reduce((a, x) => a + litrSent(x.litr), 0));
  const plastik = k.yopishTerminal - k.ochishTerminal;
  const depozitFarqi = k.yopishDepozit - k.ochishDepozit;
  const kutilgan = k.qaytim + savdo + k.qaytganNasiya - plastik - depozitFarqi - k.nasiyaJami - k.xarajatJami;
  return { jamiLitr, savdo, plastik, depozitFarqi, kutilgan, farq: k.sanalganNaqd == null ? null : k.sanalganNaqd - kutilgan };
}

/** Yopishdagi plastik ifodasi: bir nechta summa bo'lsa "7 830 000 + 300 000 = 8 130 000", aks holda (eski smena ham) bitta raqam. */
export function plastikIfodasi(s: { plastikSummalari?: number[] | null; yopishTerminal: number | null }, pul: (n: number | null) => string): string {
  const q = s.plastikSummalari ?? [];
  return q.length > 1 ? `${q.map((x) => pul(x)).join(' + ')} = ${pul(s.yopishTerminal)}` : pul(s.yopishTerminal);
}

/** NaqdSavdo (hisobot) = Savdo − Plastik − Depozit(farqi) − Nasiya. */
export const naqdSavdo = (savdo: number, plastik: number, depozitFarqi: number, nasiya: number) => savdo - plastik - depozitFarqi - nasiya;

/** Smenaning "Nasiya berildi" ro'yxati: eski qarz nasiyalari (docs §8.11) kirmaydi — ular NasiyaJami'ga ham kirmaydi, faqat qaytish qatori "Eski qarz" belgisi bilan chiqadi. Server ham chiqarib tashlaydi, bu himoya. */
export function berilganNasiyalar<T extends { eskiQarz: boolean }>(royxat: T[]): T[] {
  return royxat.filter((n) => !n.eskiQarz);
}

/** Ochiq smenada narx o'zgarishida qayd etilgan aparat segmentlari (tartib bo'yicha). */
export function narxSegmentlari<T extends { aparatId: number; narxOzgarishida: boolean }>(korsatkichlar: T[] | null | undefined, aparatId: number): T[] {
  return (korsatkichlar ?? []).filter((k) => k.aparatId === aparatId && k.narxOzgarishida);
}

/**
 * Aparatning joriy pult ko'rsatkichi (docs §8.13) — Savdo kartasi, smenani yopishdagi "Oldingi" va narx o'zgarishi dialogida bitta manba
 * (desktop `SmenaHisobi.Oldingi`): ochiq smenadagi OXIRGI narx o'zgarishi segmentining `oxiri`, segment bo'lmasa `TotalLitr`.
 */
export function joriyKorsatkich<T extends { aparatId: number; narxOzgarishida: boolean; oxiri: number }>(
  aparat: { id: number; totalLitr: number }, korsatkichlar: T[] | null | undefined,
): { qiymat: number; segment: T | null } {
  const s = narxSegmentlari(korsatkichlar, aparat.id);
  const oxirgi = s.length ? s[s.length - 1] : null;
  return { qiymat: oxirgi ? oxirgi.oxiri : aparat.totalLitr, segment: oxirgi };
}

export interface BakOgohi { chiqadi: number; bakda: number }

/**
 * Bakdagidan ko'p litr chiqsa ogohlantirish (docs §8.15, faqat klientda, bloklamaydi).
 * Ochiq smenada: chiqyapti = aparatning shu smenadagi jami litri = narx o'zgarishida qayd etilgan segmentlar (`oldingiSegmentlar`; tuzatilayotgani kirmaydi)
 * + kiritilayotgan qator (`yangi − boshi`); bor = `BakQoldiq` (kamaytirilmaydi). Yopilgan smena tuzatishida: chiqyapti = segmentning yangi litri,
 * bor = `BakQoldiq` + segmentning eski litri (u yopishda bakdan ayrilgan edi) — `oldingiSegmentlar` berilmaydi.
 * Qator bo'sh yoki boshidan kichik (alohida xato) bo'lsa ogohlantirilmaydi; chiqadigan 0 L bo'lsa ham.
 */
export function bakOgohi(boshi: number, yangi: number | null | undefined, bakda: number, oldingiSegmentlar: { litr: number }[] = []): BakOgohi | null {
  if (yangi == null || litrSent(yangi) < litrSent(boshi)) return null;
  const chiqadiSent = litrSent(segmentLitri(boshi, yangi)) + oldingiSegmentlar.reduce((x, g) => x + litrSent(g.litr), 0);
  return chiqadiSent > 0 && chiqadiSent > litrSent(bakda) ? { chiqadi: sentLitr(chiqadiSent), bakda } : null;
}

// ---- Pult krugi (docs §8.16): ba'zi pultlar `KrugHajmi` (H, masalan 10 000) da 0 ga qaytadi. API'da ko'rsatkich doim UMUMIY litr (T) —
// krug faqat ekranda ko'rsatish va kiritish qoidasi; klient pult qiymatini umumiy litrga o'giradi. Hisob butun "sent"larda (suzuvchi nuqtasiz).

/**
 * Umumiy qiymat T → ekrandagi pult va krug: `krug = floor(T / H) + 1`, `pult = T − (krug − 1) × H`.
 * Masalan H = 10 000, T = 10 560.52 → pult 560.52, krug 2; T = 10 000 → pult 0.00, krug 2. `H` yo'q (null/0) bo'lsa krug yo'q: pult = T.
 */
export function krugKorinish(jami: number, krugHajmi?: number | null): { pult: number; krug: number | null } {
  const h = litrSent(krugHajmi ?? 0);
  if (h <= 0) return { pult: jami, krug: null };
  const t = litrSent(jami);
  const krug = Math.max(Math.floor(t / h), 0) + 1;
  return { pult: sentLitr(t - (krug - 1) * h), krug };
}

/** Kiritilgan pult qiymatining natijasi: `xato` — `0 ≤ p < H` buzilgan; `jami` — API'ga yuboriladigan umumiy qiymat; `oshdi` — krug oshgan. */
export interface PultNatijasi { xato: boolean; jami: number; krug: number | null; oshdi: boolean }

/**
 * Operator pultda ko'rgan qiymat `p` → umumiy litr (docs §8.16). `boshi` (B) — shu qatorning boshlanishi (umumiy litr): yopish va narx o'zgarishida "Oldingi",
 * 14-band va yopilgan smena tuzatishida segmentning `Boshi`. `kB = floor(B / H) + 1`, `pB = B − (kB − 1) × H`:
 * `p ≥ pB` — krug o'zgarmaydi, umumiy = `(kB − 1) × H + p`; `p < pB` — pult 0 dan qayta boshlangan, umumiy = `kB × H + p`, krug +1.
 * Bir kiritishda faqat bitta krug aniqlanadi. `H` yo'q bo'lsa `p` o'zi umumiy qiymat. `p` `[0, H)` dan tashqarida bo'lsa `xato`.
 */
export function pultJamiga(p: number, boshi: number, krugHajmi?: number | null): PultNatijasi {
  const h = litrSent(krugHajmi ?? 0);
  if (h <= 0) return { xato: false, jami: p, krug: null, oshdi: false };
  const pc = litrSent(p);
  const { pult: pB, krug: kB } = krugKorinish(boshi, krugHajmi);
  const k = kB ?? 1;
  if (pc < 0 || pc >= h) return { xato: true, jami: p, krug: k, oshdi: false };
  if (pc >= litrSent(pB)) return { xato: false, jami: sentLitr((k - 1) * h + pc), krug: k, oshdi: false };
  return { xato: false, jami: sentLitr(k * h + pc), krug: k + 1, oshdi: true };
}

/** Sozlamalardagi ikki maydon ("Pult ko'rsatkichi" + "Krug") → umumiy qiymat: `(krug − 1) × H + pult`. */
export function krugdanJamiga(pult: number, krug: number, krugHajmi: number): number {
  return sentLitr((krug - 1) * litrSent(krugHajmi) + litrSent(pult));
}
