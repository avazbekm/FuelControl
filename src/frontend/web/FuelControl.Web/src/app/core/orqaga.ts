import { DestroyRef, Injectable, WritableSignal, effect, inject, untracked } from '@angular/core';

/**
 * Dialog/varaq ochilganda tarixga bitta yozuv qo'shadi — Android "orqaga" (va standalone'da yagona orqaga yo'l)
 * sahifadan chiqib ketmay, avval dialogni yopadi. URL o'zgarmaydi, shuning uchun router navigatsiya qilmaydi.
 */
@Injectable({ providedIn: 'root' })
export class Orqaga {
  private readonly stek: (() => void)[] = [];
  /** O'zimiz chaqirgan history.back() natijasidagi popstate'ni e'tiborsiz qoldirish uchun. */
  private ozimiz = 0;

  constructor() {
    addEventListener('popstate', () => {
      if (this.ozimiz) { this.ozimiz--; return; }
      this.stek.pop()?.();
    });
  }

  och(yop: () => void) {
    this.stek.push(yop);
    history.pushState({ fcDialog: this.stek.length }, '');
  }

  /** Dialog UI orqali yopildi — qo'shgan yozuvimizni tarixdan olib tashlaymiz. */
  yopildi(yop: () => void) {
    const i = this.stek.lastIndexOf(yop);
    if (i < 0) return;
    this.stek.splice(i, 1);
    this.ozimiz++;
    history.back();
  }

  /** Dialog navigatsiya sababli yopildi (yangi sahifa allaqachon tarixda) — orqaga qaytmaymiz. */
  unut(yop: () => void) {
    const i = this.stek.lastIndexOf(yop);
    if (i >= 0) this.stek.splice(i, 1);
  }
}

/**
 * Signalni "orqaga" tugmasiga bog'laydi: signal "ochiq" holatga o'tganda tarixga yozuv qo'shiladi,
 * orqaga bosilsa `yopiq` qiymat o'rnatiladi. Komponent konstruktorida chaqiriladi.
 * Qaytaradi: `unut()` — navigatsiya bilan yopilganda (masalan, varaqdagi havola bosilganda) chaqiriladi.
 */
export function orqagaBogla<T>(s: WritableSignal<T>, yopiq: T): { unut: () => void } {
  const o = inject(Orqaga);
  let ochiq = false;
  let tashqaridan = false;
  const yop = () => { tashqaridan = true; s.set(yopiq); };
  effect(() => {
    const endi = s() !== yopiq;
    untracked(() => {
      if (endi && !ochiq) { ochiq = true; o.och(yop); }
      else if (!endi && ochiq) {
        ochiq = false;
        if (tashqaridan) tashqaridan = false;
        else o.yopildi(yop);
      }
    });
  });
  const unut = () => { if (ochiq) { ochiq = false; o.unut(yop); } };
  // Dialog ochiq turganda komponent yo'q qilinsa (sahifa almashdi) — stekda o'lik yozuv qolmasin.
  inject(DestroyRef).onDestroy(unut);
  return { unut };
}
