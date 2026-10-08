// Nasiya/mijoz qidiruv qoidasi — server (`GET /nasiyalar?q=`, `/nasiyalar/mijozlar?q=`) bilan bir xil (docs/smena-hisobi-topshiriq.md §8.3).
// Klient o'zi filtrlaydigan joylarda (masalan, Nasiyalar sahifasi hamma nasiyani yuklab filtrlaydi) shu funksiya ishlatiladi.

/** Apostrof turlari (`'`, `‘`, `’`, `` ` ``, `ʻ`, `ʼ`) butunlay e'tiborga olinmaydi; katta-kichik harf farqlanmaydi. */
export function ismNorm(s: string): string {
  return s.toLowerCase().replace(/['‘’`ʻʼ]/g, '').replace(/\s+/g, ' ').trim();
}

/** Telefon: faqat raqamlar, boshidagi 998 tushiriladi (+998, bo'shliq, chiziqlar e'tiborga olinmaydi). */
function telRaqam(s: string): string {
  const d = s.replace(/\D/g, '');
  return d.startsWith('998') ? d.slice(3) : d;
}

export interface QidiruvObekti { mijozIsmi: string; telefon: string; mashinaRaqami: string }

/**
 * Ism — so'z boshi ham, ichidagi qism ham mos keladi; telefon — kamida 3 raqam yozilsa raqamlar bo'yicha;
 * mashina raqami — bo'shliqsiz va katta harfda solishtiriladi. Bo'sh so'rov hammasiga mos.
 */
export function qidiruvMos(n: QidiruvObekti, so: string): boolean {
  const t = so.trim();
  if (!t) return true;
  const ism = ismNorm(n.mijozIsmi), q = ismNorm(t);
  if (ism.includes(q) || ism.replace(/ /g, '').includes(q.replace(/ /g, ''))) return true;
  const qd = telRaqam(t);
  if (qd.length >= 3 && telRaqam(n.telefon).includes(qd)) return true;
  const raqam = t.replace(/\s+/g, '').toUpperCase();
  return raqam.length > 0 && n.mashinaRaqami.replace(/\s+/g, '').toUpperCase().includes(raqam);
}
