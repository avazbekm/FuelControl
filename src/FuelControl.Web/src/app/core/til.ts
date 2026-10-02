import { Injectable, signal, computed } from '@angular/core';
import lugat from './lugat.json';
import lugatWeb from './lugat-web.json';

export type TilKodi = 'uz' | 'uzk' | 'ru';

// Asosiy lug'at desktop'dagi Til.cs dan generatsiya qilinadi (`npm run lugat`); lugat-web.json — faqat PWA'ga xos kalitlar.
const LUGAT: Record<string, string[]> = { ...lugat, ...lugatWeb };
const INDEKS: Record<TilKodi, number> = { uz: 0, uzk: 1, ru: 2 };
const KALIT = 'fc.til';

function saqlangan(): TilKodi {
  try {
    const k = localStorage.getItem(KALIT);
    if (k === 'uz' || k === 'uzk' || k === 'ru') return k;
  } catch { /* xotira yopiq */ }
  return 'uz';
}

/** Interfeys tili: uz (lotin), uzk (kirill), ru. Shablonda `til.t('Kalit')` — signal o'qiydi, til almashsa o'zi yangilanadi. */
@Injectable({ providedIn: 'root' })
export class Til {
  readonly kod = signal<TilKodi>(saqlangan());
  readonly qisqa = computed(() => ({ uz: 'UZ', uzk: 'ЎЗ', ru: 'RU' })[this.kod()]);
  /** Intl uchun locale (oy/kun nomlari). */
  readonly locale = computed(() => ({ uz: 'uz-Latn-UZ', uzk: 'uz-Cyrl-UZ', ru: 'ru-RU' })[this.kod()]);

  constructor() {
    document.documentElement.lang = this.locale();
  }

  t(kalit: string, ...args: unknown[]): string {
    const q = LUGAT[kalit];
    let s = q ? q[INDEKS[this.kod()]] : kalit;
    if (args.length) s = s.replace(/\{(\d+)\}/g, (_, i) => String(args[+i] ?? ''));
    return s;
  }

  tanla(kod: TilKodi) {
    this.kod.set(kod);
    document.documentElement.lang = this.locale();
    try { localStorage.setItem(KALIT, kod); } catch { /* */ }
  }
}
