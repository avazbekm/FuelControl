import { Component, ElementRef, effect, input, model, viewChild } from '@angular/core';
import { telefonGuruh, telefonRaqamlar } from '../core/telefon';

const PREFIKS = '+998 ';

/**
 * Telefon kiritish maydoni (docs §8.1): doim `+998 ` bilan boshlanadi, prefiksni o'chirib bo'lmaydi; foydalanuvchi faqat 9 raqam yozadi,
 * bo'shliqlar o'zi qo'yiladi → `+998 XX XXX XX XX`. Istalgan ko'rinishdagi qiymat joylashtirilsa ham to'g'ri ishlaydi
 * (raqam bo'lmagan belgilar tashlanadi, boshidagi 998 tushiriladi). Qiymat — formatlangan qator yoki raqam yo'q bo'lsa bo'sh.
 */
@Component({
  selector: 'telefon-kiritish',
  template: `
    <input #el type="tel" inputmode="numeric" autocomplete="off" [class]="sinf()" [attr.id]="inputId()" [attr.data-avto]="avto() ? '' : null"
           [attr.aria-label]="ariaLabel() || null" [disabled]="bloklangan()" (input)="yozildi()" (keydown)="tugma($event)"
           (focus)="fokus()" (click)="kursorTuzat()" (select)="kursorTuzat()" (blur)="tugadi()" />
  `,
  host: { style: 'display: contents' },
})
export class TelefonKiritish {
  readonly qiymat = model('');
  readonly sinf = input('kiritish son');
  readonly inputId = input<string | null>(null);
  readonly ariaLabel = input('');
  readonly avto = input(false);
  readonly bloklangan = input(false);
  private readonly el = viewChild.required<ElementRef<HTMLInputElement>>('el');

  constructor() {
    effect(() => {
      const q = this.qiymat();
      const inp = this.el().nativeElement;
      const kerak = PREFIKS + telefonGuruh(telefonRaqamlar(q));
      if (inp.value !== kerak) inp.value = kerak;
    });
  }

  /** Maydon matnidan milliy raqamlar: prefiks butun bo'lsa undan keyingisi, aks holda butun matn. */
  private raqamlar(matn: string): string {
    let d: string;
    if (matn.startsWith('+998')) {
      d = matn.slice(4).replace(/\D/g, '');
      if (d.length > 9 && d.startsWith('998')) d = d.slice(3); // "+998 998901234567" — to'liq raqam o'rtaga joylandi
    } else {
      d = telefonRaqamlar(matn);
    }
    return d.slice(0, 9);
  }

  protected yozildi() {
    const inp = this.el().nativeElement;
    const kursor = inp.selectionStart ?? inp.value.length;
    const oldin = inp.value.slice(0, kursor);
    // Kursordan oldingi milliy raqamlar soni (prefiksdagi 998 hisobga olinmaydi)
    const sana = oldin.startsWith('+998') ? oldin.slice(4).replace(/\D/g, '').length : telefonRaqamlar(oldin).length;
    const r = this.raqamlar(inp.value);
    const matn = PREFIKS + telefonGuruh(r);
    inp.value = matn;
    // kursorni shu raqamdan keyingi joyga qaytarish
    let say = 0, joy = PREFIKS.length;
    for (let i = PREFIKS.length; i < matn.length && say < sana; i++) { joy = i + 1; if (/\d/.test(matn[i])) say++; }
    if (sana === 0) joy = PREFIKS.length;
    try { inp.setSelectionRange(joy, joy); } catch { /* */ }
    this.qiymat.set(r ? matn : '');
  }

  protected tugma(e: KeyboardEvent) {
    const inp = this.el().nativeElement;
    const a = inp.selectionStart ?? 0, b = inp.selectionEnd ?? 0;
    // Prefiksga tegib bo'lmaydi
    if (e.key === 'Backspace' && a === b && a <= PREFIKS.length) e.preventDefault();
    if (e.key === 'ArrowLeft' && a === b && a <= PREFIKS.length) e.preventDefault();
    if (e.key === 'Home') { e.preventDefault(); inp.setSelectionRange(PREFIKS.length, PREFIKS.length); }
    if (e.key === 'Delete' && a < PREFIKS.length && b <= PREFIKS.length) e.preventDefault();
  }

  protected fokus() {
    const inp = this.el().nativeElement;
    if (!inp.value.startsWith(PREFIKS)) inp.value = PREFIKS;
    setTimeout(() => this.kursorTuzat(), 0);
  }

  /** Kursor/belgilash prefiksga tushmasin. */
  protected kursorTuzat() {
    const inp = this.el().nativeElement;
    const a = inp.selectionStart ?? 0, b = inp.selectionEnd ?? 0;
    if (a < PREFIKS.length) { try { inp.setSelectionRange(PREFIKS.length, Math.max(PREFIKS.length, b)); } catch { /* */ } }
  }

  protected tugadi() {
    // faqat prefiks qolgan bo'lsa — qiymat bo'sh
    if (!this.qiymat()) this.el().nativeElement.value = PREFIKS;
  }

  focus() { this.el().nativeElement.focus(); }
}
