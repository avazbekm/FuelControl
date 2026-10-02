import { Injectable, signal, computed, inject, effect } from '@angular/core';
import type { HubConnection } from '@microsoft/signalr';
import { Subject } from 'rxjs';
import { Auth } from './auth';
import type { Sotuv, Smena } from '../api/turlar';

export type HubHodisa =
  | { turi: 'SotuvQoshildi' | 'SotuvOzgardi'; sotuv: Sotuv }
  | { turi: 'SmenaOzgardi'; smena: Smena }
  | { turi: 'NarxOzgardi'; malumot: unknown };

/**
 * Tarmoq holati va SignalR (/hub). Kirgan foydalanuvchi uchun ulanadi, uzilsa cheksiz qayta urinadi.
 * Sahifalar `hodisa$` ga obuna bo'lib ma'lumotni yangilaydi.
 */
@Injectable({ providedIn: 'root' })
export class Aloqa {
  private readonly auth = inject(Auth);
  readonly onlayn = signal(navigator.onLine);
  readonly hubHolati = signal<'uzilgan' | 'ulanmoqda' | 'ulangan'>('uzilgan');
  /** Banner: internet yo'q yoki hub qayta ulanmoqda. */
  readonly muammo = computed<'yoq' | 'aloqaYoq' | 'qaytaUlanmoqda'>(() =>
    !this.onlayn() ? 'aloqaYoq' : this.auth.kirganmi() && this.hubHolati() !== 'ulangan' && this.oldinUlangan() ? 'qaytaUlanmoqda' : 'yoq');
  private readonly oldinUlangan = signal(false);
  readonly hodisa$ = new Subject<HubHodisa>();
  /** Qayta ulanganda (yoki internet qaytganda) — sahifalar to'liq yangilanadi, navbat yuboriladi. */
  readonly tiklandi$ = new Subject<void>();

  private hub: HubConnection | null = null;
  private qaytaTaymer: ReturnType<typeof setTimeout> | undefined;

  constructor() {
    addEventListener('online', () => { this.onlayn.set(true); this.tiklandi$.next(); this.boshla(); });
    addEventListener('offline', () => this.onlayn.set(false));
    effect(() => (this.auth.kirganmi() ? this.boshla() : this.toxta()));
  }

  private async boshla() {
    if (!this.auth.kirganmi()) return;
    // SignalR alohida chunk'da — login sahifasi va birinchi ochilish yengilroq.
    const { HubConnectionBuilder, HubConnectionState, LogLevel } = await import('@microsoft/signalr');
    if (!this.auth.kirganmi()) return;
    if (!this.hub) {
      const hub = new HubConnectionBuilder()
        .withUrl('/hub', { accessTokenFactory: () => this.auth.token() ?? '' })
        .withAutomaticReconnect({ nextRetryDelayInMilliseconds: (c) => Math.min(30000, 1000 * 2 ** Math.min(c.previousRetryCount, 5)) })
        .configureLogging(LogLevel.Warning)
        .build();
      hub.on('SotuvQoshildi', (s: Sotuv) => this.hodisa$.next({ turi: 'SotuvQoshildi', sotuv: s }));
      hub.on('SotuvOzgardi', (s: Sotuv) => this.hodisa$.next({ turi: 'SotuvOzgardi', sotuv: s }));
      hub.on('SmenaOzgardi', (s: Smena) => this.hodisa$.next({ turi: 'SmenaOzgardi', smena: s }));
      hub.on('NarxOzgardi', (m: unknown) => this.hodisa$.next({ turi: 'NarxOzgardi', malumot: m }));
      hub.onreconnecting(() => this.hubHolati.set('ulanmoqda'));
      hub.onreconnected(() => { this.hubHolati.set('ulangan'); this.tiklandi$.next(); });
      // Avtomatik qayta ulanish ham to'xtasa — o'zimiz qayta boshlaymiz.
      hub.onclose(() => { this.hubHolati.set('uzilgan'); this.keyinroq(); });
      this.hub = hub;
    }
    if (this.hub.state !== HubConnectionState.Disconnected) return;
    try {
      this.hubHolati.set('ulanmoqda');
      await this.hub.start();
      this.hubHolati.set('ulangan');
      if (this.oldinUlangan()) this.tiklandi$.next();
      this.oldinUlangan.set(true);
    } catch {
      this.hubHolati.set('uzilgan');
      this.keyinroq();
    }
  }

  private keyinroq() {
    clearTimeout(this.qaytaTaymer);
    if (this.auth.kirganmi()) this.qaytaTaymer = setTimeout(() => this.boshla(), 10000);
  }

  private toxta() {
    clearTimeout(this.qaytaTaymer);
    const h = this.hub;
    this.hub = null;
    this.hubHolati.set('uzilgan');
    this.oldinUlangan.set(false);
    h?.stop();
  }
}
