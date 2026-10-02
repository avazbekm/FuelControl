import { Injectable, signal, effect } from '@angular/core';

export type TemaRejimi = 'tizim' | 'yorug' | 'qorongi';
const KALIT = 'fc.tema';

/** Yorug' / qorong'i rejim. `tizim` — telefon sozlamasiga ergashadi. <html data-theme> orqali CSS tokenlari almashadi. */
@Injectable({ providedIn: 'root' })
export class Tema {
  readonly rejim = signal<TemaRejimi>(this.oqi());
  private readonly media = matchMedia('(prefers-color-scheme: dark)');
  readonly qorongimi = signal(false);

  constructor() {
    this.media.addEventListener('change', () => this.qolla());
    effect(() => {
      const r = this.rejim();
      try { localStorage.setItem(KALIT, r); } catch { /* */ }
      this.qolla();
    });
  }

  almashtir() {
    this.rejim.set(this.qorongimi() ? 'yorug' : 'qorongi');
  }

  private qolla() {
    const r = this.rejim();
    const q = r === 'qorongi' || (r === 'tizim' && this.media.matches);
    this.qorongimi.set(q);
    const html = document.documentElement;
    if (r === 'tizim') html.removeAttribute('data-theme');
    else html.setAttribute('data-theme', r === 'qorongi' ? 'dark' : 'light');
    document.querySelector('meta[name="theme-color"]')?.setAttribute('content', q ? '#0C1120' : '#F2F6FF');
  }

  private oqi(): TemaRejimi {
    try {
      const r = localStorage.getItem(KALIT);
      if (r === 'yorug' || r === 'qorongi' || r === 'tizim') return r;
    } catch { /* */ }
    return 'tizim';
  }
}
