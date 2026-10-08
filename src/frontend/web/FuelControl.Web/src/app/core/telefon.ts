// Telefon formati hamma joyda `+998 XX XXX XX XX` (docs/smena-hisobi-topshiriq.md §8.1).

/** Telefondagi milliy raqamlar: istalgan ko'rinishdan (`+998 90 123 45 67`, `998901234567`, `90-123-45-67`) 998 siz, ko'pi bilan 9 ta. */
export function telefonRaqamlar(s: string | null | undefined): string {
  let d = (s ?? '').replace(/\D/g, '');
  if (d.startsWith('998')) d = d.slice(3);
  return d.slice(0, 9);
}

/** 9 tagacha raqamni `XX XXX XX XX` ko'rinishida guruhlaydi (qisman ham: "90 12"). */
export function telefonGuruh(raqamlar: string): string {
  const g = [raqamlar.slice(0, 2), raqamlar.slice(2, 5), raqamlar.slice(5, 7), raqamlar.slice(7, 9)];
  return g.filter(Boolean).join(' ');
}

/** Ko'rsatish: istalgan qiymat → `+998 XX XXX XX XX` (raqam bo'lmasa bo'sh qator). To'liq bo'lmasa — mavjud qismi. */
export function telefonFormat(s: string | null | undefined): string {
  const r = telefonRaqamlar(s);
  return r ? '+998 ' + telefonGuruh(r) : '';
}

/** Telefon to'liqmi (998 dan keyin aynan 9 raqam). */
export function telefonTolami(s: string | null | undefined): boolean {
  return telefonRaqamlar(s).length === 9;
}
