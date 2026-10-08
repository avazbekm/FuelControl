import { Component, ElementRef, effect, inject, input, model } from '@angular/core';
import { orqagaBogla } from '../core/orqaga';
import { Ikon } from './ikon';

/**
 * Dialog (docs/dizayn dialoglari): sarlavha qatorida rangli ikona, sarlavha, ikkinchi qator va yopish tugmasi.
 * Telefonda pastdan chiqadi, keng ekranda o'rtada. Parda bosilsa, Escape yoki Android "orqaga" bosilsa yopiladi.
 * Ichidagi `data-avto` maydon (sichqonchali qurilmada) fokus oladi.
 * Ishlatilishi: `<oyna [(ochiq)]="ochiq" sarlavha="…" tagsarlavha="…" ikon="book" ikonRang="to-rang">…<div class="amallar">…</div></oyna>`.
 */
@Component({
  selector: 'oyna',
  imports: [Ikon],
  host: { '(document:keydown.escape)': 'ochiq() && yop()' },
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
      if (this.ochiq() && matchMedia('(pointer: fine)').matches) {
        setTimeout(() => this.el.nativeElement.querySelector<HTMLElement>('[data-avto]')?.focus(), 60);
      }
    });
  }

  yop() { this.ochiq.set(false); }
}
