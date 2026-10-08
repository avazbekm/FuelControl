import { Directive, ElementRef, inject } from '@angular/core';

/**
 * Enter bilan keyingi maydonga o'tish (docs §8.4). Konteynerga qo'yiladi (`<form enterKeyingi>` yoki sahifa ildizi):
 * matn maydonida Enter bosilsa fokus DOM tartibidagi keyingi maydonga o'tadi (forma yuborilmaydi); oxirgi maydondan keyin —
 * `button[type=submit]` yoki `[data-enter-oxirgi]` tugmasiga o'tadi, lekin tugma Enter bilan o'zi bosilmaydi.
 * `[data-enter-otkaz]` maydon Enter tartibidan chiqariladi (ixtiyoriy izoh kabi). Textarea'da Enter — yangi qator (tegilmaydi).
 */
@Directive({
  selector: '[enterKeyingi]',
  host: { '(keydown)': 'bosildi($event)', '(focusin)': 'fokus($event)' },
})
export class EnterKeyingi {
  private readonly ildiz = inject<ElementRef<HTMLElement>>(ElementRef).nativeElement;

  private nishonlar(): HTMLElement[] {
    const r = this.ildiz.querySelectorAll<HTMLElement>('input, select, button[type=submit], button[data-enter-oxirgi]');
    return [...r].filter((e) => {
      if (e.hasAttribute('data-enter-otkaz') || (e as HTMLInputElement).disabled || (e as HTMLInputElement).readOnly) return false;
      if (e instanceof HTMLInputElement && ['checkbox', 'radio', 'hidden', 'button', 'submit', 'file'].includes(e.type)) return false;
      return e.getClientRects().length > 0; // ko'rinadigan
    });
  }

  private keyingisi(joriy: HTMLElement): HTMLElement | null {
    return this.nishonlar().find((e) => e !== joriy && !!(joriy.compareDocumentPosition(e) & Node.DOCUMENT_POSITION_FOLLOWING)) ?? null;
  }

  protected bosildi(e: KeyboardEvent) {
    if (e.key !== 'Enter' || e.isComposing || e.shiftKey || e.ctrlKey || e.altKey || e.metaKey) return;
    const t = e.target;
    if (!(t instanceof HTMLInputElement) || ['checkbox', 'radio', 'button', 'submit', 'file'].includes(t.type)) return;
    e.preventDefault(); // implicit submit yo'q
    const o = (k: HTMLElement) => {
      k.focus();
      if (k instanceof HTMLInputElement && !['date', 'datetime-local', 'month'].includes(k.type)) k.select();
    };
    const k = this.keyingisi(t);
    if (k) return o(k);
    // Tugma hozirgina yoqilishi mumkin (oxirgi raqam yozilgan, ko'rinish hali yangilanmagan) — bir kadrdan keyin qayta uriniladi.
    requestAnimationFrame(() => requestAnimationFrame(() => { if (document.activeElement === t) { const k2 = this.keyingisi(t); if (k2) o(k2); } }));
  }

  /** Mobil klaviatura: keyingi maydon bo'lsa "keyingi", oxirgisida "tayyor". */
  protected fokus(e: FocusEvent) {
    const t = e.target;
    if (!(t instanceof HTMLInputElement)) return;
    const k = this.keyingisi(t);
    t.enterKeyHint = k && !(k instanceof HTMLButtonElement) ? 'next' : 'done';
  }
}
