// Pul, litr va vaqt formatlari. Pul — butun so'm "16 600 000"; litr — 2 xona "237 323.06"; vaqt — Toshkent.

export const TZ = 'Asia/Tashkent';

function guruhla(butun: string): string {
  return butun.replace(/\B(?=(\d{3})+(?!\d))/g, ' ');
}

export function pul(n: number | null | undefined): string {
  if (n == null || !isFinite(n)) return '0';
  const r = Math.round(Math.abs(n));
  // Yaxlitlanganda 0 bo'lsa (−0.3 va h.k.) ishorasiz "0".
  return (n < 0 && r > 0 ? '−' : '') + guruhla(r.toString());
}

export function litr(n: number | null | undefined): string {
  if (n == null || !isFinite(n)) return '0.00';
  const [b, k] = Math.abs(n).toFixed(2).split('.');
  const m = n < 0 && (b !== '0' || k !== '00') ? '−' : '';
  return `${m}${guruhla(b)}.${k}`;
}

/** Server UTC qaytaradi; zona belgisi bo'lmasa ham UTC deb o'qiymiz. */
export function sana(s: string | Date): Date {
  if (s instanceof Date) return s;
  return new Date(/[zZ]|[+-]\d\d:\d\d$/.test(s) || s.length <= 10 ? s : s + 'Z');
}

const vaqtF = new Intl.DateTimeFormat('en-GB', { timeZone: TZ, hour: '2-digit', minute: '2-digit', hour12: false });
const kunF = new Intl.DateTimeFormat('en-GB', { timeZone: TZ, day: '2-digit', month: '2-digit', year: 'numeric' });
const isoF = new Intl.DateTimeFormat('en-CA', { timeZone: TZ, year: 'numeric', month: '2-digit', day: '2-digit' });

export const soat = (s: string | Date) => vaqtF.format(sana(s));
export const kun = (s: string | Date) => kunF.format(sana(s)).replace(/\//g, '.');
export const kunSoat = (s: string | Date) => `${kun(s)} ${soat(s)}`;

/** Toshkent bo'yicha yyyy-MM-dd. */
export function isoKun(d: Date = new Date()): string {
  return isoF.format(d);
}

/** yyyy-MM-dd ga n kun qo'shish (kalendar bo'yicha, zonasiz). */
export function kunQosh(iso: string, n: number): string {
  const d = new Date(iso + 'T00:00:00Z');
  d.setUTCDate(d.getUTCDate() + n);
  return d.toISOString().slice(0, 10);
}

export function oyBoshi(iso: string): string {
  return iso.slice(0, 8) + '01';
}

export function oyOxiri(iso: string): string {
  const d = new Date(iso.slice(0, 8) + '01T00:00:00Z');
  d.setUTCMonth(d.getUTCMonth() + 1);
  d.setUTCDate(0);
  return d.toISOString().slice(0, 10);
}

export function oyQosh(iso: string, n: number): string {
  const d = new Date(iso.slice(0, 8) + '01T00:00:00Z');
  d.setUTCMonth(d.getUTCMonth() + n);
  return d.toISOString().slice(0, 10);
}

/** Davomiylik "5 s 20 min" (birliklar lug'atdan). */
export function davomiylik(boshi: string, oxiri: string | null, s: string, min: string): string {
  const ms = (oxiri ? sana(oxiri) : new Date()).getTime() - sana(boshi).getTime();
  const daq = Math.max(0, Math.floor(ms / 60000));
  const h = Math.floor(daq / 60);
  return h ? `${h} ${s} ${daq % 60} ${min}` : `${daq} ${min}`;
}

/** "16 600 000" kabi matndan son (bo'shliq, vergul ham qabul). */
export function sonOl(matn: string | number | null | undefined): number | null {
  if (matn == null) return null;
  const t = String(matn).replace(/[\s\u00A0]/g, '').replace(',', '.');
  if (!t) return null;
  const n = Number(t);
  return isFinite(n) ? n : null;
}

/** Toshkent bo'yicha kun (yyyy-MM-dd) va soat ("HH:mm") dan UTC ISO vaqt. */
export function toshkentdan(kunIso: string, soatMin: string): string {
  const [y, m, d] = kunIso.split('-').map(Number);
  const [h, min] = soatMin.split(':').map(Number);
  return new Date(Date.UTC(y, m - 1, d, h - 5, min)).toISOString();
}

/** "dd.MM" (Toshkent) — qisqa sana. */
export function kunQisqa(s: string | Date): string {
  return kun(s).slice(0, 5);
}

/** "9 soat 40 daqiqa" / "24 soat" uchun: [soat, daqiqa]. */
export function davomiylikSD(boshi: string, oxiri: string | null): [number, number] {
  const ms = (oxiri ? sana(oxiri) : new Date()).getTime() - sana(boshi).getTime();
  const daq = Math.max(0, Math.floor(ms / 60000));
  return [Math.floor(daq / 60), daq % 60];
}

/** Qo'shish/ayirish belgisi bilan: "+15 995 270" / "−7 330 000" / "0". */
export function ishoraPul(n: number): string {
  return n > 0 && Math.round(n) > 0 ? '+' + pul(n) : pul(n);
}

/** Litr, oxiridagi ".00" siz: 6840 → "6 840", 6428.2 → "6 428.20". */
export function litrQisqa(n: number | null | undefined): string {
  const s = litr(n);
  return s.endsWith('.00') ? s.slice(0, -3) : s;
}

/** Ismning bosh harflari (avatar): "Alisher Karimov" → "AK". */
export function harflar(ism: string | null | undefined): string {
  return (ism ?? '').split(/\s+/).filter(Boolean).map((s) => s[0]).slice(0, 2).join('').toUpperCase();
}

/** "yyyy-MM-dd" → "dd.MM". */
export function kunOy(iso: string): string {
  return iso.slice(8, 10) + '.' + iso.slice(5, 7);
}

/** "yyyy-MM-dd" → "dd.MM.yyyy". */
export function kunToliq(iso: string): string {
  return iso.slice(8, 10) + '.' + iso.slice(5, 7) + '.' + iso.slice(0, 4);
}

/** Toshkentdagi hozirgi vaqt: kun "yyyy-MM-dd" va soat "HH:mm". */
export function hozirToshkent(): { kun: string; soat: string } {
  const d = new Date();
  return { kun: isoKun(d), soat: soat(d) };
}

/** Ikki "yyyy-MM-dd" orasidagi kun farqi (b − a). */
export function kunFarqi(a: string, b: string): number {
  return Math.round((new Date(b + 'T00:00:00Z').getTime() - new Date(a + 'T00:00:00Z').getTime()) / 864e5);
}
