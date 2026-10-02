import { Injectable, computed, signal } from '@angular/core';

/** Chrome/Edge/Samsung Internet'ning o'rnatish hodisasi (standart TS turlarida yo'q). */
interface BeforeInstallPromptEvent extends Event {
  prompt(): Promise<void>;
  userChoice: Promise<{ outcome: 'accepted' | 'dismissed' }>;
}

const KECHIKTIRISH = 'fc.ornatishKeyinroq';
const KECHIKTIRISH_KUN = 7;

/**
 * "Bosh ekranga qo'shish". Android/desktop'da `beforeinstallprompt` ushlanadi va o'z tugmamiz chaqiradi;
 * iOS'da bunday API yo'q — Ulashish → "Bosh ekranga qo'shish" yo'riqnomasi ko'rsatiladi.
 * Hodisa sahifa yuklanishi bilan keladi, shuning uchun servis ilova boshlanishida (App) yaratiladi.
 */
@Injectable({ providedIn: 'root' })
export class Ornatish {
  private hodisa: BeforeInstallPromptEvent | null = null;
  private readonly media = matchMedia('(display-mode: standalone)');

  /** Brauzer o'rnatish oynasini ko'rsata oladi (Android/desktop Chromium). */
  readonly tayyor = signal(false);
  /** Ilova sifatida ochilgan (o'rnatilgan). */
  readonly ornatilgan = signal(this.standalonemi());
  /** iOS/iPadOS — o'rnatish faqat qo'lda (Ulashish menyusi). */
  readonly ios = /iPhone|iPad|iPod/.test(navigator.userAgent) || (navigator.platform === 'MacIntel' && navigator.maxTouchPoints > 1);
  /** Login sahifasida "Keyinroq" bosilgan bo'lsa, bir hafta taklif qilinmaydi. */
  readonly kechiktirilgan = signal(this.kechiktirilganmi());
  /** Taklif ko'rsatish mumkinmi (sozlamalarda — kechiktirishga qaramay). */
  readonly mumkin = computed(() => !this.ornatilgan() && (this.tayyor() || this.ios));

  constructor() {
    addEventListener('beforeinstallprompt', (e) => {
      e.preventDefault(); // brauzerning o'z mini-panelini emas, bizning tugmani ko'rsatamiz
      this.hodisa = e as BeforeInstallPromptEvent;
      this.tayyor.set(true);
    });
    addEventListener('appinstalled', () => {
      this.hodisa = null;
      this.tayyor.set(false);
      this.ornatilgan.set(true);
    });
    this.media.addEventListener('change', () => this.ornatilgan.set(this.standalonemi()));
  }

  /** Brauzer o'rnatish oynasini ochadi. true — foydalanuvchi o'rnatdi. */
  async ornat(): Promise<boolean> {
    const h = this.hodisa;
    if (!h) return false;
    await h.prompt();
    const { outcome } = await h.userChoice;
    // Hodisa bir martalik — keyingisini brauzer yana yuboradi.
    this.hodisa = null;
    this.tayyor.set(false);
    return outcome === 'accepted';
  }

  keyinroq() {
    try { localStorage.setItem(KECHIKTIRISH, String(Date.now())); } catch { /* */ }
    this.kechiktirilgan.set(true);
  }

  private standalonemi(): boolean {
    return this.media.matches || (navigator as Navigator & { standalone?: boolean }).standalone === true;
  }

  private kechiktirilganmi(): boolean {
    try {
      const t = Number(localStorage.getItem(KECHIKTIRISH));
      return !!t && Date.now() - t < KECHIKTIRISH_KUN * 864e5;
    } catch {
      return false;
    }
  }
}
