import { Component, ElementRef, effect, input, model, output, viewChild } from '@angular/core';

/** Probel bilan guruhlangan butun qism: "1234567" → "1 234 567". */
function guruhla(b: string): string {
  return b.replace(/\B(?=(\d{3})+(?!\d))/g, ' ');
}

/**
 * Son kiritish maydoni: yozish paytida minglar probel bilan guruhlanadi ("4 878 000"), kasr (`kasr` > 0) — nuqta yoki vergul bilan, ko'pi bilan `kasr` xona.
 * Qiymat — son yoki bo'sh bo'lsa `null`. `manfiy` — "−" belgisiga ruxsat. Stil `sinf` orqali (`kiritish pul`, `kiritish katta` …).
 * Ishlatilishi: `<son-kiritish [(qiymat)]="x" sinf="kiritish pul" placeholder="0" />`.
 */
@Component({
  selector: 'son-kiritish',
  template: `
    <input #el type="text" [class]="sinf()" [attr.id]="inputId()" [attr.data-avto]="avto() ? '' : null" [attr.inputmode]="kasr() ? 'decimal' : 'numeric'" autocomplete="off"
           [attr.placeholder]="placeholder()" [attr.aria-label]="ariaLabel() || null" [disabled]="bloklangan()" [readOnly]="faqatOqish()"
           (input)="yozildi()" (blur)="tugadi()" (keydown.enter)="enter.emit()" />
  `,
  host: { style: 'display: contents' },
})
export class SonKiritish {
  readonly qiymat = model<number | null>(null);
  readonly kasr = input(0);
  readonly manfiy = input(false);
  readonly sinf = input('kiritish pul');
  readonly placeholder = input('0');
  readonly inputId = input<string | null>(null);
  readonly ariaLabel = input('');
  readonly bloklangan = input(false);
  readonly faqatOqish = input(false);
  /** Enter bosilganda (forma yuborish uchun). */
  readonly enter = output<void>();
  readonly avto = input(false);
  private readonly el = viewChild.required<ElementRef<HTMLInputElement>>('el');
  /** Foydalanuvchi yozayotgan matn (qiymatdan farq qilishi mumkin: "12." yoki "0.0"). */
  private matn = '';
  private fokusda = false;

  constructor() {
    effect(() => {
      const q = this.qiymat();
      const inp = this.el().nativeElement;
      // Tashqaridan o'zgargan (yoki dastlabki) qiymat; foydalanuvchi yozayotganda tegmaymiz.
      if (this.fokusda && this.taxlil(this.matn) === q) return;
      this.matn = this.korsat(q);
      inp.value = this.matn;
    });
  }

  private korsat(q: number | null): string {
    if (q == null || !isFinite(q)) return '';
    const m = q < 0 ? '−' : '';
    const k = this.kasr();
    if (!k) return m + guruhla(Math.round(Math.abs(q)).toString());
    const [b, d] = Math.abs(q).toFixed(k).split('.');
    return m + guruhla(b) + '.' + d;
  }

  private taxlil(m: string): number | null {
    const manfiy = this.manfiy() && /^[\s ]*[-−–]/.test(m);
    let t = m.replace(/[\s −–-]/g, '').replace(',', '.');
    if (!this.kasr()) t = t.split('.')[0];
    if (!t || t === '.') return null;
    const n = Number(t);
    return isFinite(n) ? (manfiy ? -n : n) : null;
  }

  protected yozildi() {
    const inp = this.el().nativeElement;
    this.fokusda = true;
    const pos = inp.selectionStart ?? inp.value.length;
    // Kursordan oldingi "ma'noli" belgilar soni — formatlangandan keyin kursorni shu joyga qaytaramiz.
    const oldin = inp.value.slice(0, pos).replace(/[^\d.,]/g, '').length;
    const manfiy = this.manfiy() && /^[\s ]*[-−–]/.test(inp.value);
    let xom = inp.value.replace(/[^\d.,]/g, '').replace(',', '.');
    let kasr = '';
    const nuqta = xom.indexOf('.');
    if (nuqta >= 0) {
      if (this.kasr()) kasr = '.' + xom.slice(nuqta + 1).replace(/\./g, '').slice(0, this.kasr());
      xom = xom.slice(0, nuqta);
    }
    xom = xom.replace(/^0+(?=\d)/, '');
    const korsatma = (manfiy ? '−' : '') + guruhla(xom) + kasr;
    this.matn = korsatma;
    inp.value = korsatma;
    // kursor
    let say = 0, yangi = korsatma.length;
    for (let i = 0; i < korsatma.length; i++) {
      if (say >= oldin) { yangi = i; break; }
      if (/[\d.]/.test(korsatma[i])) say++;
    }
    if (say < oldin) yangi = korsatma.length;
    try { inp.setSelectionRange(yangi, yangi); } catch { /* ba'zi input turlarida mumkin emas */ }
    this.qiymat.set(this.taxlil(korsatma));
  }

  protected tugadi() {
    this.fokusda = false;
    const inp = this.el().nativeElement;
    this.matn = this.korsat(this.qiymat());
    inp.value = this.matn;
  }

  focus() { this.el().nativeElement.focus(); }
}
