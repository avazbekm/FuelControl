import { Component, DestroyRef, ElementRef, effect, inject, input, model, untracked } from '@angular/core';
import { orqagaBogla } from '../core/orqaga';
import { Ikon } from './ikon';

/**
 * Dialog (docs/dizayn dialoglari): sarlavha qatorida rangli ikona, sarlavha, ikkinchi qator va yopish tugmasi.
 * Telefonda pastdan chiqadi, keng ekranda o'rtada. Parda bosilsa, Escape yoki Android "orqaga" bosilsa yopiladi.
 * Ichidagi `data-avto` maydon (sichqonchali qurilmada) fokus oladi. Dialog ustida dialog ochilsa (masalan, tasdiq), Escape faqat eng ustkisini yopadi.
 * Ishlatilishi: `<oyna [(ochiq)]="ochiq" sarlavha="…" tagsarlavha="…" ikon="book" ikonRang="to-rang">…<div class="amallar">…</div></oyna>`.
 */
/** Hozir ochiq oynalar, ochilish tartibida (oxirgisi — eng ustkisi). */
const ochiqlar: Oyna[] = [];

@Component({
  selector: 'oyna',
  imports: [Ikon],
  host: { '(document:keydown.escape)': 'escBosildi()' },
  template: `
    @if (ochiq()) {
      <div class="parda" (click)="yop()">
        <div class="dialog" [style.max-width.px]="kenglik()" (click)="$event.stopPropagation()" role="dialog" aria-modal="true" [attr.aria-label]="sarlavha()">
          <div class="dialog-bosh">
            @if (ikon()) { <span class="ikon-doira katta" [class]="ikonRang()"><ikon [nomi]="ikon()" [olcham]="20" [qalinlik]="2" /></span> }
            <div class="matnlar">
              <h2>{{ sarlavha() }}</h2>
              @if (tagsarlavha()) { <span class="tagsarlavha">{{ tagsarlavha() }}</span> }
            </div>
            <button type="button" class="yopish" (click)="yop()" aria-label="×"><ikon nomi="x" [olcham]="18" [qalinlik]="2" /></button>
          </div>
          <ng-content />
        </div>
      </div>
    }
  `,
})
export class Oyna {
  readonly ochiq = model(false);
  readonly sarlavha = input('');
  readonly tagsarlavha = input('');
  readonly ikon = input('');
  /** `ikon-doira` rang sinfi: to-rang | yashil | sariq | qizil | moviy | binafsha (bo'sh — ko'k). */
  readonly ikonRang = input('');
  readonly kenglik = input(600);
  private readonly el = inject<ElementRef<HTMLElement>>(ElementRef);

  constructor() {
    orqagaBogla(this.ochiq, false);
    effect(() => {
      const och = this.ochiq();
      untracked(() => {
        const i = ochiqlar.indexOf(this);
        if (och && i < 0) ochiqlar.push(this);
        else if (!och && i >= 0) ochiqlar.splice(i, 1);
      });
    });
    inject(DestroyRef).onDestroy(() => { const i = ochiqlar.indexOf(this); if (i >= 0) ochiqlar.splice(i, 1); });
    effect(() => {
      if (this.ochiq() && matchMedia('(pointer: fine)').matches) {
        setTimeout(() => this.el.nativeElement.querySelector<HTMLElement>('[data-avto]')?.focus(), 60);
      }
    });
  }

  protected escBosildi() { if (this.ochiq() && ochiqlar[ochiqlar.length - 1] === this) this.yop(); }

  yop() { this.ochiq.set(false); }
}
