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
