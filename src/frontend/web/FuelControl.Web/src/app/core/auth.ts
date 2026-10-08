import { Injectable, signal, computed, inject } from '@angular/core';
import { Router } from '@angular/router';
import { apiSozla, ApiXato } from '../api/api';
import { Server } from './server';
import { sana } from './format';
import type { FoydalanuvchiDto, Ruxsat } from '../api/model';

const KALIT = 'fc.sessiya';

interface Sessiya { token: string; muddati: string; foydalanuvchi: FoydalanuvchiDto }

/** JWT sessiyasi. "Eslab qolish" — localStorage (12 soatgacha), aks holda sessionStorage (tab yopilguncha). */
@Injectable({ providedIn: 'root' })
export class Auth {
  private readonly router = inject(Router);
  private readonly server = inject(Server);
  readonly sessiya = signal<Sessiya | null>(this.oqi());
  readonly foydalanuvchi = computed(() => this.sessiya()?.foydalanuvchi ?? null);
  readonly kirganmi = computed(() => this.sessiya() !== null);
  /** 401 sababli chiqarilganda login sahifasida xabar ko'rsatish uchun. */
  readonly tugadi = signal(false);
  private readonly ruxsatlar = computed(() => new Set(this.foydalanuvchi()?.ruxsatlar ?? []));

  constructor() {
    apiSozla(() => this.token(), () => this.chiqish(true));
  }

  token(): string | null {
    const s = this.sessiya();
    if (!s) return null;
    if (sana(s.muddati).getTime() <= Date.now()) {
      queueMicrotask(() => this.chiqish(true));
      return null;
    }
    return s.token;
  }

  bor(r: Ruxsat): boolean {
    return this.ruxsatlar().has(r);
  }

  async kirish(login: string, parolYokiPin: string, eslab: boolean) {
    const j = await this.server.kirish(login, parolYokiPin);
    const s: Sessiya = { token: j.token, muddati: j.tokenMuddati, foydalanuvchi: j.foydalanuvchi };
    this.yoz(s, eslab);
    this.tugadi.set(false);
    this.sessiya.set(s);
  }

  /** Ruxsatlar o'zgargan bo'lishi mumkin — /me dan yangilaymiz. */
  async yangila() {
    const s = this.sessiya();
    if (!s) return;
    try {
      const f = await this.server.men();
      const yangi = { ...s, foydalanuvchi: f };
      this.yoz(yangi, localStorage.getItem(KALIT) !== null);
      this.sessiya.set(yangi);
    } catch (e) {
      if (!(e instanceof ApiXato)) return; // aloqa yo'q — eski ma'lumot bilan ishlaymiz
    }
  }

  chiqish(muddatTugadi = false) {
    if (!this.sessiya()) return;
    try { localStorage.removeItem(KALIT); sessionStorage.removeItem(KALIT); } catch { /* */ }
    this.sessiya.set(null);
    this.tugadi.set(muddatTugadi);
    this.router.navigateByUrl('/kirish');
  }

  private yoz(s: Sessiya, eslab: boolean) {
    try {
      localStorage.removeItem(KALIT); sessionStorage.removeItem(KALIT);
      (eslab ? localStorage : sessionStorage).setItem(KALIT, JSON.stringify(s));
    } catch { /* */ }
  }

  private oqi(): Sessiya | null {
    try {
      const m = localStorage.getItem(KALIT) ?? sessionStorage.getItem(KALIT);
      if (!m) return null;
      const s = JSON.parse(m) as Sessiya;
      return sana(s.muddati).getTime() > Date.now() ? s : null;
    } catch {
      return null;
    }
  }
}
