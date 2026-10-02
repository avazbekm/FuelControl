import { Component, computed, inject, input } from '@angular/core';
import { Til } from '../core/til';
import { pul, litr, soat, kunSoat } from '../core/format';
import type { Sotuv } from '../api/turlar';

/** Sotuv qatori (ro'yxatlar uchun): vaqt, aparat/yoqilg'i, operator, summa, litr, to'lov turlari. */
@Component({
  selector: 'sotuv-qator',
  template: `
    <span class="vaqt son">{{ toliqVaqt() ? kunSoat(s().vaqt) : soat(s().vaqt) }}</span>
    <span class="matnlar">
      <span class="asosiy-matn">{{ til.t('Ap') }} {{ s().aparatRaqami }} · {{ s().yoqilgiNomi }}</span>
      <span class="ikkinchi">
        @if (operatorKorsat()) { {{ s().operatorIsmi }} · }
        {{ litr(s().litr) }} {{ til.t('L') }}
        @if (bekor()) { · <span class="qizil">{{ til.t('Bekor') }}</span>@if (s().bekorSababi) {: {{ s().bekorSababi }}} }
      </span>
    </span>
    <span class="ong">
      <span class="summa">{{ pul(s().summa) }}</span>
      <span class="tolovlar">
        @for (t of s().tolovlar; track t.turi) {
          <span class="nuqta" [style.background]="'var(--q-' + t.turi.toLowerCase() + ')'"></span>{{ til.t(t.turi) }}
        }
      </span>
    </span>
    <ng-content />
  `,
  styles: `
    :host { display: contents; }
    .vaqt { font-size: 13px; color: var(--matn-2); min-width: 42px; flex: none; }
    .summa { font-weight: 700; }
    .tolovlar { font-size: 12px; color: var(--matn-2); display: inline-flex; align-items: center; gap: 4px; }
    .nuqta { width: 7px; height: 7px; border-radius: 999px; display: inline-block; margin-left: 4px; }
  `,
})
export class SotuvQator {
  protected readonly til = inject(Til);
  readonly s = input.required<Sotuv>();
  readonly operatorKorsat = input(true);
  readonly toliqVaqt = input(false);
  protected readonly bekor = computed(() => this.s().holati === 'BekorQilingan');
  protected readonly pul = pul;
  protected readonly litr = litr;
  protected readonly soat = soat;
  protected readonly kunSoat = kunSoat;
}
