import { Component, ElementRef, effect, inject, input, model } from '@angular/core';
import { orqagaBogla } from '../core/orqaga';
import { Ikon } from './ikon';

/**
 * Dialog (desktop'dagi "parda + dialog" bilan bir xil): telefonda pastdan chiqadi, keng ekranda o'rtada.
 * Parda bosilsa, Escape yoki Android "orqaga" bosilsa yopiladi. Ichidagi `data-avto` maydon (sichqonchali qurilmada) fokus oladi.
 * Ishlatilishi: `<oyna [(ochiq)]="ochiq" [sarlavha]="...">…<div class="amallar">…</div></oyna>` — komponent doim shablonda turadi.
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
            <h2>{{ sarlavha() }}</h2>
            <button type="button" class="tugma ikonli kichik" (click)="yop()" aria-label="×"><ikon nomi="x" [olcham]="18" /></button>
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
  readonly kenglik = input(520);
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
