import { DestroyRef, Directive, ElementRef, afterNextRender, inject } from '@angular/core';

/**
 * Katta summa o'z katagiga sig'masa, shrift kichraytiriladi (Avalonia'dagi Viewbox/"Moslashuvchan" yondashuvi): 11 xonali summa
 * ("15 345 000 000 so'm") karta chetidan chiqib ketmasin, kesilmasin. Faqat `white-space: nowrap` bo'lgan, bloklangan kenglikdagi
 * elementga qo'yiladi (grid katagi, flex ustuni): kenglik, matn yoki shrift (til/qorong'i rejim) o'zgarsa qayta hisoblanadi.
 * Kichrayish pastki chegarasi — asl shriftning 55 %: undan ham uzun matn bo'lmaydi (long = 19 xona).
 */
@Directive({ selector: '[sigdir]' })
export class Sigdir {
  private readonly el = inject<ElementRef<HTMLElement>>(ElementRef).nativeElement;
  private kadr = 0;
  /** Oxirgi hisoblangan holat (kenglik + matn): o'zgarmagan bo'lsa qayta o'lchanmaydi. */
  private imzo = '';

  constructor() {
    const ro = new ResizeObserver(() => this.rejala());
    const mo = new MutationObserver(() => this.rejala());
    afterNextRender(() => {
      ro.observe(this.el);
      if (this.el.parentElement) ro.observe(this.el.parentElement);
      mo.observe(this.el, { childList: true, characterData: true, subtree: true });
      document.fonts?.ready.then(() => { this.imzo = ''; this.rejala(); });
      this.rejala();
    });
    inject(DestroyRef).onDestroy(() => { ro.disconnect(); mo.disconnect(); cancelAnimationFrame(this.kadr); });
  }

  private rejala() {
    cancelAnimationFrame(this.kadr);
    this.kadr = requestAnimationFrame(() => this.moslash());
  }

  private moslash() {
    const el = this.el;
    const imzo = `${el.parentElement?.clientWidth ?? 0}|${el.clientWidth}|${el.textContent}|${document.documentElement.lang}`;
    if (imzo === this.imzo) return;
    el.style.fontSize = '';                                   // asl o'lcham (stil varag'idan)
    const asl = parseFloat(getComputedStyle(el).fontSize);
    const sigim = el.clientWidth;
    const kerak = el.scrollWidth;
    if (sigim > 0 && kerak > sigim && asl > 0) el.style.fontSize = `${Math.max(asl * 0.55, Math.floor(((asl * sigim) / kerak) * 98) / 100)}px`;
    this.imzo = `${el.parentElement?.clientWidth ?? 0}|${el.clientWidth}|${el.textContent}|${document.documentElement.lang}`;
  }
}
