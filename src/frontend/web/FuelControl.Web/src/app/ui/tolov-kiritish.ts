import { Component, computed, inject, input, model } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Til } from '../core/til';
import { pul, sonOl } from '../core/format';
import type { Tolov, TolovTuri } from '../api/turlar';
import { Ikon } from './ikon';

export type TolovTanlovi = TolovTuri | 'Aralash';
export type Qismlar = Record<TolovTuri, string>;
export const BOSH_QISMLAR = (): Qismlar => ({ Naqd: '', Plastik: '', Click: '' });
const TURLAR: TolovTuri[] = ['Naqd', 'Plastik', 'Click'];

/** Tanlovdan API uchun to'lovlar ro'yxati; aralashda yig'indi summaga teng bo'lmasa null. */
export function tolovlarYasa(turi: TolovTanlovi, q: Qismlar, summa: number): Tolov[] | null {
  if (turi !== 'Aralash') return [{ turi, summa }];
  const r = TURLAR.map((t) => ({ turi: t, summa: Math.round(sonOl(q[t]) ?? 0) })).filter((x) => x.summa > 0);
  return r.reduce((s, x) => s + x.summa, 0) === summa && r.length ? r : null;
}

/** Mavjud to'lovlardan tanlov holatini tiklash (tahrirlash dialogi uchun). */
export function tanlovniTikla(t: Tolov[]): { turi: TolovTanlovi; qismlar: Qismlar } {
  const q = BOSH_QISMLAR();
  for (const x of t) q[x.turi] = String(x.summa);
  return { turi: t.length === 1 ? t[0].turi : 'Aralash', qismlar: q };
}

/** To'lov turi: Naqd / Plastik / Click / Aralash (aralashda qismlar va qoldiq jonli tekshiriladi). */
@Component({
  selector: 'tolov-kiritish',
  imports: [FormsModule, Ikon],
  template: `
    <div class="segment">
      @for (t of tanlovlar; track t.turi) {
        <button type="button" [class.tanlangan]="turi() === t.turi" (click)="turi.set(t.turi)">
          <ikon [nomi]="t.ikon" [olcham]="16" /> <span class="nom">{{ til.t(t.turi) }}</span>
        </button>
      }
    </div>
    @if (turi() === 'Aralash') {
      <div class="qismlar">
        @for (t of turlar; track t) {
          <div class="maydon">
            <label [for]="'q-' + t">{{ til.t(t) }}</label>
            <input class="kiritish son" [id]="'q-' + t" inputmode="numeric" autocomplete="off"
                   [ngModel]="qismlar()[t]" (ngModelChange)="qismOzgardi(t, $event)" placeholder="0" />
          </div>
        }
      </div>
      <div class="qator ora kichik-matn">
        <span class="ikkilamchi">{{ til.t('AralashIzoh') }}</span>
        <b class="son" [class.qizil]="qoldiq() !== 0" [class.yashil]="qoldiq() === 0 && summa() > 0">{{ til.t('Qoldiq') }}: {{ pul(qoldiq()) }}</b>
      </div>
    }
  `,
  styles: `
    :host { display: flex; flex-direction: column; gap: 10px; }
    .qismlar { display: grid; grid-template-columns: repeat(3, minmax(0, 1fr)); gap: 8px; }
    .qismlar .kiritish { padding: 0 10px; }
    @media (max-width: 380px) { .segment .nom { font-size: 12px; } .segment ikon { display: none; } }
  `,
})
export class TolovKiritish {
  protected readonly til = inject(Til);
  readonly summa = input.required<number>();
  readonly turi = model<TolovTanlovi>('Naqd');
  readonly qismlar = model<Qismlar>(BOSH_QISMLAR());
  protected readonly turlar = TURLAR;
  protected readonly pul = pul;
  protected readonly tanlovlar: { turi: TolovTanlovi; ikon: string }[] = [
    { turi: 'Naqd', ikon: 'cash' }, { turi: 'Plastik', ikon: 'card' }, { turi: 'Click', ikon: 'phone' }, { turi: 'Aralash', ikon: 'mix' },
  ];
  protected readonly qoldiq = computed(() =>
    this.summa() - TURLAR.reduce((s, t) => s + Math.round(sonOl(this.qismlar()[t]) ?? 0), 0));

  qismOzgardi(t: TolovTuri, v: string) {
    this.qismlar.set({ ...this.qismlar(), [t]: v });
  }
}
