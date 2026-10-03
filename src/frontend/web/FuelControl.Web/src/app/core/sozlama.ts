import { Injectable } from '@angular/core';

const KALIT = 'fc.apiAsos';

/** Manzilni tozalaydi: bo'sh (shu domen) yoki "https://api.misol.uz" (oxirida "/" yo'q). Yaroqsiz bo'lsa — bo'sh. */
export function apiManzilTozala(v: unknown): string {
  if (typeof v !== 'string') return '';
  const t = v.trim().replace(/\/+$/, '');
  if (!t) return '';
  try {
    const u = new URL(t);
    return u.protocol === 'http:' || u.protocol === 'https:' ? u.origin + (u.pathname === '/' ? '' : u.pathname.replace(/\/+$/, '')) : '';
  } catch {
    console.warn('sozlama.json: "api" yaroqsiz manzil, shu domen ishlatiladi:', v);
    return '';
  }
}

/**
 * Ish vaqtidagi sozlama: sayt ildizidagi `sozlama.json` → `{ "api": "" }`.
 * Bo'sh "api" — API shu domenda (wwwroot'dan beriladi); aks holda to'liq manzil (Cloudflare Pages + boshqa domendagi API).
 * Fayl yo'q (404) yoki buzuq bo'lsa — bo'sh. Faqat tarmoq uzilgan bo'lsa (offline) oxirgi ma'lum qiymat ishlatiladi,
 * aks holda cross-origin o'rnatishda offline navbat noto'g'ri domenga yuborilardi.
 * Service worker bu faylni network-first (ngsw dataGroups freshness) beradi, shuning uchun yangi qiymat darhol o'qiladi.
 */
@Injectable({ providedIn: 'root' })
export class Sozlama {
  /** API asosiy manzili; "" — shu domen. Oxirida "/" yo'q. */
  api = '';

  /** Ilova boshlanishidan oldin chaqiriladi (APP_INITIALIZER) — bundan keyin hech bir so'rov yuborilmagan bo'ladi. */
  async yukla(): Promise<void> {
    let javob: Response;
    try {
      javob = await fetch('sozlama.json', { cache: 'no-store', signal: AbortSignal.timeout(4000) });
    } catch {
      this.api = this.saqlangan(); // tarmoq yo'q yoki sekin
      return;
    }
    if (!javob.ok) { this.api = ''; return; } // fayl yo'q — shu domen
    try {
      this.api = apiManzilTozala((await javob.json())?.api);
      try { localStorage.setItem(KALIT, this.api); } catch { /* */ }
    } catch {
      this.api = ''; // buzuq JSON
    }
  }

  /** `/hub` va boshqa yo'llar uchun to'liq manzil (SignalR). */
  yol(yol: string): string {
    return this.api + yol;
  }

  private saqlangan(): string {
    try { return apiManzilTozala(localStorage.getItem(KALIT)); } catch { return ''; }
  }
}
